using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Lcs.Model;
using LcsWidzew.UI;

namespace Lcs.EdytorZobrazowania2;

/// <summary>
/// Etap E3 (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 5 sposób 1, § 8): symbole z palety na torze, w węźle albo wolne, opisy przy elementach.
/// Kształty – wspólny kod pulpitu <see cref="PulpitSymbole"/> (ten sam co w symulatorze), w stanie zasadniczym; na torze ukośnym obrócone
/// wzdłuż toru. Opis jedzie z elementem (przesunięcie <see cref="ObrOpis.D"/>), przeciąga się go myszą. Zaznaczenie, cień wstawianego
/// symbolu i ⚠ – tylko w edytorze.
/// </summary>
public partial class ObrView
{
    /// <summary>Rodzaj elementu wstawianego narzędziem Symbol (<see cref="ObrSymbole.Paleta"/>).</summary>
    public string SymbolTyp { get; set; }
    /// <summary>Spacja w narzędziu Symbol – następne symbole ostrzem w drugą stronę (domyślnie w stronę środka obrazu).</summary>
    public bool SymbolOdwroc { get; set; }
    /// <summary>Zaznaczone elementy (id).</summary>
    public readonly HashSet<string> SelEl = new();

    /// <summary>Klik narzędziem Symbol: rodzaj, odcinek / węzeł (null – wolny), położenie T, kierunek, punkt [j.].</summary>
    public event Action<string, string, double, int, int[]> SymbolPlaced;
    /// <summary>Element przeciągnięty: id, nowe T (na torze) albo nowy punkt (wolny).</summary>
    public event Action<string, double, int[]> ElementMoved;
    /// <summary>Opis przeciągnięty: element, numer opisu, nowe przesunięcie [j.].</summary>
    public event Action<string, int, int[]> OpisMoved;
    /// <summary>PPM na elemencie: punkt [j.], element.</summary>
    public event Action<int[], string> ElementMenuAt;

    (string Id, Point Start, double T, int[] P)? elDrag;
    (string Id, int Idx, int[] D0, Point Start, int[] D)? opisDrag;

    static readonly Typeface Mono = new("Consolas");
    static readonly Brush PipGrey = new SolidColorBrush(Color.FromRgb(0xa0, 0xa0, 0xa0));
    static readonly Brush Ghost = new SolidColorBrush(Color.FromArgb(0x90, 0x9a, 0x9a, 0x9a)), SelSym = new SolidColorBrush(Color.FromRgb(0x00, 0xa0, 0xff));
    static readonly Pen SelSymPen = new(SelSym, 1.5);

    public void Select(IEnumerable<string> wezly, IEnumerable<string> odcinki, IEnumerable<string> elementy)
    {
        SelW.Clear(); SelE.Clear(); SelEl.Clear();
        if (wezly != null) SelW.UnionWith(wezly);
        if (odcinki != null) SelE.UnionWith(odcinki);
        if (elementy != null) SelEl.UnionWith(elementy);
        SelectionChanged?.Invoke(); InvalidateVisual();
    }

    // ------------------------------------------------------------------ trafienia
    /// <summary>Środek opisu na obrazie [j.].</summary>
    /// <param name="idx">Numer opisu (odstępy SBL – numer pola); −1 – szukany w liście opisów.</param>
    public static (double X, double Y)? OpisPkt(ObrObraz o, ObrElement el, ObrOpis d, int idx = -1)
    {
        if (el.Typ == ObrSymbole.Peron && el.P != null) return (el.P[0] + d.D[0], el.P[1] + d.D[1]);
        if (el.Typ == ObrSymbole.OdstepySbl)
        {
            int i = idx >= 0 ? idx : el.Opisy.IndexOf(d);
            var pola = ObrSymbole.PolaSbl(o, el);
            if (i < 0 || i >= pola.Count) return null;
            var (ax, ay, bx, by, _) = pola[i];
            return ((ax + bx) / 2 + d.D[0], (ay + by) / 2 + d.D[1]);
        }
        return ObrSymbole.Pozycja(o, el) is var (x, y, _, _) ? (x + d.D[0], y + d.D[1]) : null;
    }

    /// <summary>Opis pod punktem ekranu (prostokąt napisu z marginesem 3 px).</summary>
    public (ObrElement El, int Idx)? HitOpis(Point s)
    {
        if (Obraz == null) return null;
        var (x, y) = ToImage(s);
        foreach (var el in Obraz.Elementy)
            for (int i = 0; i < el.Opisy.Count; i++)
            {
                var d = el.Opisy[i];
                if (string.IsNullOrEmpty(d.Tekst) || el.Typ is ObrSymbole.NumerToru or ObrSymbole.Peron || OpisPkt(Obraz, el, d, i) is not var (ox, oy)) continue;
                var ft = Tekst(d.Tekst, RozmiarOpisu(el.Typ), Brushes.White);
                if (el.Typ == ObrSymbole.Przejazd) ox += ft.Width / 2;
                if (Math.Abs(x - ox) * Zoom <= ft.Width * Zoom / 2 + 3 && Math.Abs(y - oy) * Zoom <= ft.Height * Zoom / 2 + 3) return (el, i);
            }
        return null;
    }

    /// <summary>Element pod punktem ekranu: symbol do 10 px od swojego punktu; peron – w prostokącie; napis i numer toru – na napisie.</summary>
    public ObrElement HitElement(Point s)
    {
        if (Obraz == null) return null;
        var (x, y) = ToImage(s);
        ObrElement best = null; double bd = double.MaxValue;
        foreach (var el in Obraz.Elementy.Where(e => e.Typ is not (ObrGraf.Skrzyzowanie or ObrGraf.RozjazdKrzyzowy)))
        {
            if (el.Typ is ObrSymbole.Peron or ObrSymbole.PolePip && el.P != null)
            {
                var r = ObrSymbole.Rozmiar(el);
                if (x >= el.P[0] && x <= el.P[0] + r[0] && y >= el.P[1] && y <= el.P[1] + r[1]) return el;
                continue;
            }
            if (el.Typ == ObrSymbole.OdstepySbl)
            {
                // na kresce dowolnego pola (do 8 px)
                foreach (var (ax, ay, bx, by, _) in ObrSymbole.PolaSbl(Obraz, el))
                    if (ObrGraf.Rzut(new[] { (int)ax, (int)ay }, new[] { (int)bx, (int)by }, x, y).Dist * Zoom <= 8) return el;
                continue;
            }
            if (el.Typ == ObrSymbole.Zwrotnica) continue;             // zwrotnica – przez opis albo węzeł rozjazdu
            if (ObrSymbole.Pozycja(Obraz, el) is not var (ex, ey, _, _)) continue;
            double d = Math.Sqrt((ex - x) * (ex - x) + (ey - y) * (ey - y)) * Zoom;
            if (d <= 10 && d < bd) { bd = d; best = el; }
        }
        return best;
    }

    /// <summary>Wskaż na elemencie albo opisie: zaznaczenie i początek przeciągania; true – obsłużone.</summary>
    bool ElementDown(Point p, bool shift)
    {
        if (HitOpis(p) is var (oel, idx))
        {
            if (!shift && !SelEl.Contains(oel.Id)) Select(null, null, new[] { oel.Id });
            opisDrag = (oel.Id, idx, oel.Opisy[idx].D.ToArray(), p, oel.Opisy[idx].D.ToArray());
            return true;
        }
        var el = HitElement(p);
        if (el == null) return false;
        if (shift) { if (!SelEl.Remove(el.Id)) SelEl.Add(el.Id); SelectionChanged?.Invoke(); InvalidateVisual(); return true; }
        if (!SelEl.Contains(el.Id)) Select(null, null, new[] { el.Id });
        if (ObrSymbole.NaTorze(el.Typ) || ObrSymbole.Wolny(el.Typ)) elDrag = (el.Id, p, el.T, el.P?.ToArray());
        return true;
    }

    void ElementMove(Point p)
    {
        if (opisDrag is var (oid, idx, d0, st, _))
        {
            // opis – krok 2 j. (drobniej niż siatka torów)
            var (x0, y0) = ToImage(st); var (x1, y1) = ToImage(p);
            var d = new[] { d0[0] + (int)Math.Round((x1 - x0) / 2) * 2, d0[1] + (int)Math.Round((y1 - y0) / 2) * 2 };
            opisDrag = (oid, idx, d0, st, d);
            preview = Clone(Obraz);
            preview.Elementy.First(e => e.Id == oid).Opisy[idx].D = d;
        }
        if (elDrag is var (eid, est, t0, p0))
        {
            preview = Clone(Obraz);
            var el = preview.Elementy.First(e => e.Id == eid);
            var (x, y) = ToImage(p);
            if (ObrSymbole.Wolny(el.Typ) && p0 != null)
            {
                // położenie bezwzględne (nie krok siatki od starego miejsca) – peron i pole PIP między tory, napis na siatkę
                var (sx, sy) = ToImage(est);
                double nx = p0[0] + x - sx, ny = p0[1] + y - sy;
                el.P = el.Typ is ObrSymbole.Peron or ObrSymbole.PolePip ? MiejsceProstokata(nx, ny, ObrSymbole.Rozmiar(el), eid)
                    : new[] { ZobTory.Grid(nx, G), ZobTory.Grid(ny, G) };
                elDrag = (eid, est, t0, p0);
            }
            else if (ObrGraf.Odcinek(preview, el.Na) is ObrOdcinek e)
            {
                // symbol jedzie po swoim torze (punkty siatki na torze)
                var (a, b) = ObrGraf.Konce(preview, e);
                var q = ObrGraf.NaOdcinku(a, b, x, y, G);
                double len = Math.Sqrt(Math.Pow(b[0] - a[0], 2) + Math.Pow(b[1] - a[1], 2));
                el.T = len < 1e-9 ? 0 : Math.Sqrt(Math.Pow(q[0] - a[0], 2) + Math.Pow(q[1] - a[1], 2)) / len;
            }
        }
    }

    void ElementUp()
    {
        if (opisDrag is var (oid, idx, d0, _, d))
        {
            opisDrag = null; preview = null; ReleaseMouseCapture();
            if (d[0] != d0[0] || d[1] != d0[1]) OpisMoved?.Invoke(oid, idx, d);
        }
        if (elDrag is var (eid, _, t0, p0))
        {
            var el = preview?.Elementy.FirstOrDefault(e => e.Id == eid);
            elDrag = null; preview = null; ReleaseMouseCapture();
            if (el == null) return;
            if (ObrSymbole.Wolny(el.Typ) && el.P != null && p0 != null && (el.P[0] != p0[0] || el.P[1] != p0[1])) ElementMoved?.Invoke(eid, 0, el.P);
            else if (!ObrSymbole.Wolny(el.Typ) && Math.Abs(el.T - t0) > 1e-9) ElementMoved?.Invoke(eid, el.T, null);
        }
    }

    // ------------------------------------------------------------------ wstawianie
    /// <summary>Miejsce wstawianego symbolu pod kursorem: odcinek / węzeł / punkt wolny; null – symbol na torze poza torem.</summary>
    public (string Na, double T, int Kier, int[] P)? MiejsceSymbolu(Point s)
    {
        if (Obraz == null || SymbolTyp == null) return null;
        var (x, y) = ToImage(s);
        double tol = Math.Max(12, 16 / Zoom);
        if (ObrSymbole.Wolny(SymbolTyp))
        {
            if (SymbolTyp is ObrSymbole.Peron or ObrSymbole.PolePip)
            {
                // kursor w środku prostokąta
                var r = ObrSymbole.Rozmiar(new ObrElement { Typ = SymbolTyp });
                return (null, 0, 1, MiejsceProstokata(x - r[0] / 2.0, y - r[1] / 2.0, r, null));
            }
            return (null, 0, 1, new[] { ZobTory.Grid(x, G), ZobTory.Grid(y, G) });
        }
        if (SymbolTyp is ObrSymbole.Koziol or ObrSymbole.OdstepySbl or ObrSymbole.KoniecSzlaku)
        {
            var (w, dw) = Obraz.Wezly.Where(n => ObrGraf.Przy(Obraz, n.Id).Count == 1).Select(n => (n, d: Math.Sqrt(Math.Pow(n.P[0] - x, 2) + Math.Pow(n.P[1] - y, 2))))
                .Where(z => z.d <= tol * 2).OrderBy(z => z.d).FirstOrDefault();
            var nt = SymbolTyp == ObrSymbole.Koziol ? null : ObrSymbole.NaTorzeW(Obraz, x, y, tol, G);
            // koniec toru – chyba że kliknięto bliżej na innym torze (np. tor szlaku bez końca obok końców innych torów)
            bool innyTor = nt is var (e0, _, p0) && e0.A != w?.Id && e0.B != w?.Id && Math.Sqrt(Math.Pow(p0[0] - x, 2) + Math.Pow(p0[1] - y, 2)) < dw;
            if (w != null && !innyTor) return (w.Id, 0, 1, w.P);
            // szlak wewnętrzny (obie stacje na obrazie, tor bez końca): w dowolnym miejscu toru, strona stacji – sama, spacja – odwrotnie
            if (nt is not var (es, ts, ps)) return null;
            return (es.Id, ts, ObrSymbole.StronaStacji(Obraz, es, ts, SymbolOdwroc), ps);
        }
        if (ObrSymbole.NaTorzeW(Obraz, x, y, tol, G) is not var (e, t, p)) return null;
        return (e.Id, t, ObrSymbole.Kierunkowy(SymbolTyp) ? ObrSymbole.KierDomyslny(Obraz, e, p[0], p[1], SymbolOdwroc)
            : SymbolTyp == ObrSymbole.StrzalkaBlokady ? ObrSymbole.StronaStacji(Obraz, e, t, SymbolOdwroc) : 1, p);
    }

    /// <summary>
    /// Lewy górny róg peronu / pola PIP [j.] z przyciąganiem (uwaga właściciela 2026-10-26: przyciąganie peronów słabe).
    /// W pionie: na środku między najbliższym torem poziomym nad i pod prostokątem (tory nachodzące na niego w poziomie),
    /// gdy odstęp torów mieści prostokąt i jest najwyżej 4 razy wyższy; inaczej siatka. W poziomie: lewa albo prawa krawędź
    /// do węzła albo symbolu tych torów albo do krawędzi innego peronu / pola (do 12 px); inaczej siatka.
    /// </summary>
    /// <param name="x">Lewy górny róg bez przyciągania [j.].</param>
    /// <param name="bez">Przeciągany element – pomijany przy krawędziach.</param>
    public int[] MiejsceProstokata(double x, double y, int[] r, string bez)
    {
        int w = r[0], h = r[1];
        int gx = ZobTory.Grid(x, G), gy = ZobTory.Grid(y, G);
        if (Obraz == null) return new[] { gx, gy };
        double cy = y + h / 2.0;
        // tory poziome nachodzące na prostokąt w poziomie: y, x od, x do
        var tory = Obraz.Odcinki.Select(e => ObrGraf.Konce(Obraz, e)).Where(k => k.Item1 != null && k.Item2 != null && k.Item1[1] == k.Item2[1])
            .Select(k => (Y: k.Item1[1], X0: Math.Min(k.Item1[0], k.Item2[0]), X1: Math.Max(k.Item1[0], k.Item2[0])))
            .Where(t => t.X1 > x && t.X0 < x + w).ToList();
        var nad = tory.Where(t => t.Y <= cy).OrderByDescending(t => t.Y).Cast<(int Y, int X0, int X1)?>().FirstOrDefault();
        var pod = tory.Where(t => t.Y >= cy).OrderBy(t => t.Y).Cast<(int Y, int X0, int X1)?>().FirstOrDefault();
        int py = gy;
        if (nad is var (ya, _, _) && pod is var (yb, _, _) && yb - ya >= h && yb - ya <= 4 * h)
            py = (int)Math.Round((ya + yb - h) / 2.0);
        // krawędzie w poziomie: węzły i symbole torów nad i pod, krawędzie innych prostokątów
        var xs = new List<double>();
        foreach (var t in new[] { nad, pod }) if (t is var (ty, x0, x1)) { xs.Add(x0); xs.Add(x1); }
        double y0 = nad?.Y ?? py, y1 = pod?.Y ?? py + h;
        foreach (var el in Obraz.Elementy.Where(e => e.Id != bez))
        {
            if (el.Typ is ObrSymbole.Peron or ObrSymbole.PolePip && el.P != null) { var rr = ObrSymbole.Rozmiar(el); xs.Add(el.P[0]); xs.Add(el.P[0] + rr[0]); }
            else if (ObrSymbole.NaTorze(el.Typ) && ObrSymbole.Pozycja(Obraz, el) is var (ex, ey, _, _) && ey >= y0 - 1 && ey <= y1 + 1) xs.Add(ex);
        }
        double tol = Math.Max(4, 12 / Zoom), bd = tol; int px = gx;
        foreach (var c in xs)
        {
            if (Math.Abs(c - x) < bd) { bd = Math.Abs(c - x); px = (int)Math.Round(c); }
            if (Math.Abs(c - (x + w)) < bd) { bd = Math.Abs(c - (x + w)); px = (int)Math.Round(c) - w; }
        }
        return new[] { px, py };
    }

    void SymbolClick(Point p)
    {
        if (MiejsceSymbolu(p) is var (na, t, kier, pt)) SymbolPlaced?.Invoke(SymbolTyp, na, t, kier, pt);
        else Info?.Invoke(SymbolTyp == ObrSymbole.Koziol ? "Kozioł: kliknij przy końcu toru" : $"{ObrSymbole.NazwaTypu(SymbolTyp)}: kliknij na torze");
    }

    // ------------------------------------------------------------------ rysowanie
    FormattedText Tekst(string s, double size, Brush b) => new(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Mono, size, b, 1.0);

    static double RozmiarOpisu(string typ) => typ switch
    {
        ObrSymbole.Semafor or ObrSymbole.Tarcza or ObrSymbole.Peron or ObrSymbole.Napis => 10,
        ObrSymbole.Zwrotnica or ObrSymbole.NumerToru => 9.5,
        ObrSymbole.OpisOdcinka or ObrSymbole.Wykolejnica => 9,
        _ => 8,
    };

    static Brush BarwaOpisu(string typ) => typ switch
    {
        ObrSymbole.Semafor or ObrSymbole.Powtarzacz => PulpitSymbole.SigName,
        ObrSymbole.Tarcza => PulpitSymbole.TmName,
        ObrSymbole.Zwrotnica => PulpitSymbole.White,
        ObrSymbole.Przejazd => PulpitSymbole.CrossPink,
        ObrSymbole.Peron => PulpitSymbole.Label,
        ObrSymbole.Napis => PulpitSymbole.Grey,
        _ => PulpitSymbole.SwLabel,
    };

    /// <param name="odLewej">Punkt to lewy brzeg napisu (opis przejazdu – jak na pulpicie), inaczej środek.</param>
    void TekstW(DrawingContext dc, string s, double x, double y, double size, Brush b, bool odLewej = false)
    {
        if (string.IsNullOrEmpty(s)) return;
        var ft = Tekst(s, size, b);
        dc.DrawText(ft, new Point(odLewej ? x : x - ft.Width / 2, y - ft.Height / 2));
    }

    /// <summary>Symbole i opisy obrazu w jednostkach obrazu (transformacja powiększenia), kod kształtów – <see cref="PulpitSymbole"/>.</summary>
    void DrawSymbole(DrawingContext dc, ObrObraz o)
    {
        dc.PushTransform(new MatrixTransform(Zoom, 0, 0, Zoom, Off.X, Off.Y));
        try
        {
            foreach (var el in o.Elementy.Where(e => e.Typ is not (ObrGraf.Skrzyzowanie or ObrGraf.RozjazdKrzyzowy)))
                Symbol(dc, o, el, PulpitSymbole.Grey);
            foreach (var el in o.Elementy) Opisy(dc, o, el);
            // cień symbolu pod kursorem (narzędzie Symbol)
            if (!Czysty && Tool == ObrTool.Symbol && SymbolTyp != null && hoverScreen is Point hs && MiejsceSymbolu(hs) is var (na, t, kier, pt))
            {
                var g = new ObrObraz { Wezly = o.Wezly, Odcinki = o.Odcinki, Szer = o.Szer, Wys = o.Wys };
                var ghost = new ObrElement { Typ = SymbolTyp, Na = na, T = t, Kier = kier, P = pt, Styl = SymbolTyp == ObrSymbole.Semafor ? ObrSymbole.SemManewrowy : SymbolTyp == ObrSymbole.Powtarzacz ? ObrSymbole.PowtSemafora : null };
                if (SymbolTyp is ObrSymbole.Peron or ObrSymbole.Przejazd or ObrSymbole.PolePip) ghost.Rozm = ObrSymbole.Rozmiar(new ObrElement { Typ = SymbolTyp });
                if (SymbolTyp == ObrSymbole.OdstepySbl) { ghost.Pola = 4; ghost.Rozm = new[] { ObrGraf.Wezel(o, na) != null ? 3 : 0, -1 }; }
                g.Elementy.Add(ghost);
                if (SymbolTyp is ObrSymbole.NumerToru or ObrSymbole.Napis or ObrSymbole.OpisOdcinka) { var q = ObrSymbole.Pozycja(g, ghost).Value; dc.DrawRectangle(null, new Pen(Ghost, 1), new Rect(q.X - 10, q.Y - 6, 20, 12)); }
                else Symbol(dc, g, ghost, Ghost);
            }
            if (!Czysty && preview == null)
                foreach (var el in o.Elementy.Where(e => SelEl.Contains(e.Id)))
                {
                    var r = Obrys(o, el);
                    if (r != null) dc.DrawRoundedRectangle(null, new Pen(SelSym, 1.5 / Zoom), r.Value, 3, 3);
                }
        }
        finally { dc.Pop(); }
    }

    /// <summary>Prostokąt zaznaczenia elementu [j.].</summary>
    Rect? Obrys(ObrObraz o, ObrElement el)
    {
        if (el.Typ is ObrSymbole.Peron or ObrSymbole.PolePip && el.P != null) { var r = ObrSymbole.Rozmiar(el); return new Rect(el.P[0] - 3, el.P[1] - 3, r[0] + 6, r[1] + 6); }
        if (el.Typ == ObrSymbole.OdstepySbl && ObrSymbole.PolaSbl(o, el) is { Count: > 0 } pl)
        {
            double x0 = pl.Min(q => Math.Min(q.Ax, q.Bx)), x1 = pl.Max(q => Math.Max(q.Ax, q.Bx)), y0 = pl.Min(q => Math.Min(q.Ay, q.By)), y1 = pl.Max(q => Math.Max(q.Ay, q.By));
            return new Rect(x0 - 4, y0 - 18, x1 - x0 + 8, y1 - y0 + 24);
        }
        if (el.Typ == ObrSymbole.Zwrotnica && el.Opisy.Count > 0 && OpisPkt(o, el, el.Opisy[0]) is var (zx, zy)) return new Rect(zx - 12, zy - 8, 24, 16);
        if (ObrSymbole.Pozycja(o, el) is not var (x, y, _, _)) return null;
        return new Rect(x - 14, y - 12, 28, 24);
    }

    /// <summary>Kształt elementu w barwie stanu zasadniczego (cień – <paramref name="c"/> półprzezroczysty).</summary>
    void Symbol(DrawingContext dc, ObrObraz o, ObrElement el, Brush c)
    {
        if (el.Typ == ObrSymbole.PolePip)
        {
            // okno numeru pociągu w stanie zasadniczym (szlak wolny): szare pole z białą ramką (wzór GrM; na pulpicie – PanelView.DrawTrainField)
            if (el.P == null) return;
            var r = ObrSymbole.Rozmiar(el);
            if (c == Ghost) dc.DrawRectangle(null, new Pen(Ghost, 1.2), new Rect(el.P[0], el.P[1], r[0], r[1]));
            else dc.DrawRectangle(PipGrey, new Pen(PulpitSymbole.White, 1.2), new Rect(el.P[0], el.P[1], r[0], r[1]));
            return;
        }
        if (el.Typ == ObrSymbole.OdstepySbl)
        {
            // odstępy SBL: wycięcie toru szlakowego pod rzędem, kreski pól jak tor (szare) – w rzędzie i w kolumnie
            var pola = ObrSymbole.PolaSbl(o, el);
            if (pola.Count == 0 || ObrSymbole.Pozycja(o, el) is not var (ex, ey, eux, euy)) return;
            int n = el.Pola ?? 0;
            if (n > 0 && c != Ghost) dc.DrawLine(new Pen(PulpitSymbole.Bg, 8), new Point(ex - eux * 2, ey - euy * 2), new Point(ex + eux * (n * ObrSymbole.PoleSbl + 2), ey + euy * (n * ObrSymbole.PoleSbl + 2)));
            var pen = new Pen(c, PulpitSymbole.Tor) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
            foreach (var (ax, ay, bx, by, _) in pola) dc.DrawLine(pen, new Point(ax, ay), new Point(bx, by));
            return;
        }
        if (el.Typ == ObrSymbole.Peron)
        {
            if (el.P == null) return;
            var r = ObrSymbole.Rozmiar(el);
            if (c == Ghost) dc.DrawRectangle(null, new Pen(Ghost, 1.4), new Rect(el.P[0], el.P[1], r[0], r[1]));
            else PulpitSymbole.Peron(dc, new Rect(el.P[0], el.P[1], r[0], r[1]));
            return;
        }
        if (ObrSymbole.Pozycja(o, el) is not var (x, y, ux, uy)) return;
        var bg = PulpitSymbole.Bg;
        double ang = Math.Atan2(uy, ux) * 180 / Math.PI;
        dc.PushTransform(new RotateTransform(ang, x, y));
        try
        {
            switch (el.Typ)
            {
                case ObrSymbole.Semafor:
                    dc.DrawRectangle(bg, null, new Rect(x - 9, y - 4, 18, 8));       // przerwa w torze pod sygnalizatorem (PanelView: 9 j. z każdej strony)
                    if (el.Styl == ObrSymbole.SemWjazdowy) { PulpitSymbole.Wjazdowy(dc, x, y, 1, c, bg); PulpitSymbole.KoniecWyjazdu(dc, x, y, 1, c); }
                    else if (el.Styl == ObrSymbole.SemWjazdowyBez) PulpitSymbole.Wjazdowy(dc, x, y, 1, c, bg, false);
                    else PulpitSymbole.Semafor(dc, x, y, 1, c, el.Styl != ObrSymbole.SemPociagowy);
                    break;
                case ObrSymbole.Tarcza: dc.DrawRectangle(bg, null, new Rect(x - 9, y - 4, 18, 8)); PulpitSymbole.Tarcza(dc, x, y, 1, c); break;
                case ObrSymbole.Powtarzacz: dc.DrawRectangle(bg, null, new Rect(x - 9, y - 4, 18, 8)); PulpitSymbole.Powtarzacz(dc, x, y, 1, c, el.Styl != ObrSymbole.PowtTarcza); break;
                case ObrSymbole.Pk: dc.DrawRectangle(bg, null, new Rect(x - 9, y - 4, 18, 8)); PulpitSymbole.Pk(dc, x, y, 1, c == Ghost ? c : PulpitSymbole.SwLabel); break;
                case ObrSymbole.Przerwa: PulpitSymbole.Przerwa(dc, x, y, c == Ghost ? Ghost : bg); break;
                case ObrSymbole.Wykolejnica: PulpitSymbole.Wykolejnica(dc, x, y, c); break;
                case ObrSymbole.Koziol: PulpitSymbole.Koziol(dc, x, y, 1, c); break;
                case ObrSymbole.StrzalkaBlokady: case ObrSymbole.KoniecSzlaku: PulpitSymbole.StrzalkaNeutralna(dc, new Point(x, y), new Vector(1, 0), c, bg); break;
                case ObrSymbole.Przejazd:
                    {
                        var r = ObrSymbole.Rozmiar(el);
                        if (c == Ghost) dc.DrawLine(new Pen(Ghost, 3), new Point(x, y - r[0]), new Point(x, y + r[1]));
                        else PulpitSymbole.Przejazd(dc, x, y - r[0], y + r[1]);
                        break;
                    }
            }
        }
        finally { dc.Pop(); }
        if (el.Typ == ObrSymbole.NumerToru && el.Opisy.FirstOrDefault() is ObrOpis nt && !string.IsNullOrEmpty(nt.Tekst))
        {
            // niewidoczny – w edytorze ramka przerywana, na pulpicie i w PNG nic
            if (ObrSymbole.Ukryty(el)) { if (!Czysty) dc.DrawRectangle(null, new Pen(Ghost, 0.8) { DashStyle = DashStyles.Dash }, PulpitSymbole.PoleNumeruToru(x + nt.D[0], y + nt.D[1])); }
            else dc.DrawRectangle(bg, null, PulpitSymbole.PoleNumeruToru(x + nt.D[0], y + nt.D[1]));
        }
    }

    /// <summary>Opisy elementu; zwrotnica – numer z „+” po stronie położenia zasadniczego (PanelView).</summary>
    void Opisy(DrawingContext dc, ObrObraz o, ObrElement el)
    {
        for (int i = 0; i < el.Opisy.Count; i++)
        {
            var d = el.Opisy[i];
            if (opisDrag is var (oid, idx, _, _, dd) && oid == el.Id && idx == i && preview == null) d = new ObrOpis { Rola = d.Rola, Tekst = d.Tekst, D = dd };
            if (OpisPkt(o, el, d, i) is not var (x, y)) continue;
            if (string.IsNullOrEmpty(d.Tekst))
            {
                // pusty opis – miejsce widać tylko w edytorze
                if (!Czysty && el.Typ is not (ObrSymbole.Peron or ObrSymbole.Napis)) dc.DrawRectangle(null, new Pen(Ghost, 0.8) { DashStyle = DashStyles.Dot }, new Rect(x - 6, y - 5, 12, 10));
                continue;
            }
            if (ObrSymbole.Ukryty(el)) { if (!Czysty) TekstW(dc, d.Tekst, x, y, RozmiarOpisu(el.Typ), Ghost); continue; }
            TekstW(dc, d.Tekst, x, y, RozmiarOpisu(el.Typ), BarwaOpisu(el.Typ), el.Typ == ObrSymbole.Przejazd);
            if (el.Typ == ObrSymbole.Zwrotnica && i == 0)
            {
                var plus = ObrSymbole.OdcinekPlus(o, el.Na, el.Styl == ObrSymbole.PlusBok);
                int pd = plus != null ? ObrGraf.Kier(o, plus, el.Na).X : 0;
                if (pd == 0) pd = -(ObrSymbole.Iglica(o, el.Na)?.X ?? 1);
                TekstW(dc, "+", x + pd * (5 + d.Tekst.Length * 3.2), y - 2, 8, PulpitSymbole.White);
            }
        }
    }
}

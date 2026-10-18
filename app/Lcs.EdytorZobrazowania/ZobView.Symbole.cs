using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorZobrazowania;

/// <summary>
/// Symbole na torach obrazu (etapy Z3–Z4): zwrotnice, sygnalizatory, wykolejnice, kozły, przerwy izolacyjne, numery torów, przejazdy,
/// strzałki blokady, opisy odcinków – kształty jak na pulpicie (<c>PanelView</c>, Ie-104 § 8; wzory SimRail), w stanie zasadniczym,
/// w jednostkach obrazu, obrócone wzdłuż toru. Opis – miejsce
/// domyślne jak na pulpicie albo przeciągnięte (<see cref="ZobSymbol.Opis"/>). Zaznaczenie i ramki – tylko w edytorze.
/// </summary>
public partial class ZobView
{
    /// <summary>Projekt UT (nazwy, rodzaje, komory, kierunki) – tylko do odczytu.</summary>
    public UtProject Ut { get; set; }
    /// <summary>Zaznaczony symbol: rodzaj i id obiektu UT.</summary>
    public (ZobRodzaj R, string Ut)? SelectedSym { get; private set; }
    /// <summary>Klik narzędziem symbolu (Zwrotnica, Sygnalizator, Wykolejnica, Kozioł): punkt i tor obrazu pod kursorem.</summary>
    public event Action<int[], string> SymbolClick;
    /// <summary>Opis symbolu przeciągnięty: rodzaj, obiekt UT, nowy środek opisu [j.].</summary>
    public event Action<ZobRodzaj, string, int[]> OpisMoved;
    (ZobRodzaj R, string Ut, int[] Pt)? opisDrag;

    static readonly Brush SigName = B(0xe8, 0xc0, 0x00), TmName = B(0x18, 0xc0, 0xc0), SwLabel = B(0xb4, 0xb4, 0xb4), White = B(0xff, 0xff, 0xff), Azure = B(0x00, 0xa0, 0xff),
        CrossPink = B(0xf0, 0x80, 0xf0), Green = B(0x00, 0xe6, 0x00);
    static readonly Typeface Mono = new("Consolas");

    public void SelectSym(ZobRodzaj? r, string ut)
    {
        SelectedSym = r is ZobRodzaj rr && ut != null ? (rr, ut) : null;
        if (SelectedSym != null) { Selected = null; SelectedPeron = null; SelectedNapis = null; }
        SelectionChanged?.Invoke(); InvalidateVisual();
    }

    // ------------------------------------------------------------------ geometria symbolu
    /// <summary>Kierunek symbolu na obrazie (jednostkowy, w stronę ostrza) i tor obrazu pod symbolem.</summary>
    (double Dx, double Dy, ZobTor Tor) Dir(ZobRodzaj r, ZobSymbol s)
    {
        if (Ut == null || Obraz == null) return (1, 0, null);
        var t = ZobSymbole.TorSymbolu(Ut, Obraz, s);
        if (t == null) return (1, 0, null);
        int utDir = r switch
        {
            ZobRodzaj.Zwrotnica => Ut.Turnouts.FirstOrDefault(z => z.Id == s.Ut)?.Dir ?? 1,
            _ => Ut.Devices.FirstOrDefault(d => d.Id == s.Ut)?.Dir ?? 1,
        };
        if (r == ZobRodzaj.Szlak) { var (sx, sy) = ZobSymbole.NaZewnatrz(Ut, Obraz, s); return (sx, sy, t); }
        if (r is ZobRodzaj.Przerwa or ZobRodzaj.NumerToru or ZobRodzaj.Przejazd) { var (kx, ky) = ZobSymbole.Kierunek(t, s.P, 1); return (kx, ky, t); }
        if (r == ZobRodzaj.Kozioł)
        {
            // na zewnątrz toru obrazu – od przedostatniego punktu do końca
            var (a, b) = Same(t.Pkt[0], s.P) ? (t.Pkt[1], t.Pkt[0]) : (t.Pkt[^2], t.Pkt[^1]);
            return (Math.Sign(b[0] - a[0]), Math.Sign(b[1] - a[1]), t);
        }
        var (dx, dy) = ZobSymbole.Kierunek(t, s.P, utDir);
        return (dx, dy, t);
    }

    static bool Same(int[] a, int[] b) => Math.Abs(a[0] - b[0]) <= 0.5 && Math.Abs(a[1] - b[1]) <= 0.5;

    /// <summary>Gałąź zwrotnicy na obrazie: kierunek toru odgałęźnego od punktu zwrotnicy (null – tor odgałęźny nie narysowany).</summary>
    (int Dx, int Dy)? Branch(ZobSymbol s)
    {
        var z = Ut?.Turnouts.FirstOrDefault(x => x.Id == s.Ut); if (z == null || Obraz == null) return null;
        foreach (var t in Obraz.Tory.Where(t => t.Ut?.Tor == z.Branch && t.Pkt.Count >= 2))
        {
            if (Same(t.Pkt[0], s.P)) return (Math.Sign(t.Pkt[1][0] - s.P[0]), Math.Sign(t.Pkt[1][1] - s.P[1]));
            if (Same(t.Pkt[^1], s.P)) return (Math.Sign(t.Pkt[^2][0] - s.P[0]), Math.Sign(t.Pkt[^2][1] - s.P[1]));
        }
        return null;
    }

    /// <summary>Domyślny środek opisu [j.] – jak na pulpicie: sygnalizator pod torem przy ostrzu w prawo (nad – w lewo), zwrotnica po stronie
    /// przeciwnej niż odgałęzienie, od strony odgałęzienia; wykolejnica pod torem.</summary>
    public int[] OpisDomyslny(ZobRodzaj r, ZobSymbol s)
    {
        var (dx, dy, _) = Dir(r, s);
        int x = s.P[0], y = s.P[1];
        switch (r)
        {
            case ZobRodzaj.Sygnalizator:
                // prostopadle do toru, po prawej stronie kierunku ostrza (na torze poziomym: ostrze w prawo – pod torem)
                return new[] { (int)Math.Round(x - dy * 17), (int)Math.Round(y + dx * 17) };
            case ZobRodzaj.Zwrotnica:
                var br = Branch(s);
                int tip = br is var (bx, _) && bx != 0 ? -bx : (int)-dx;
                bool down = br is var (_, by) && by > 0;
                return new[] { x - tip * 8, down ? y - 12 : y + 13 };
            case ZobRodzaj.Wykolejnica: return new[] { x, y + 17 };
            case ZobRodzaj.Szlak:
                // oznaczenie szlaku nad strzałką (na torze pionowym – z prawej)
                return dy == 0 ? new[] { x, y - 14 } : new[] { x + 16, y };
            case ZobRodzaj.Przejazd:
                { int up = s.Zasieg?[0] ?? 28; return new[] { x + 8 + 4 * Label(r, s).Length, y - up - 4 }; }
            case ZobRodzaj.NumerToru or ZobRodzaj.OpisOdcinka or ZobRodzaj.Przerwa: return new[] { x, y };
            default: return new[] { x, y + 15 };
        }
    }

    /// <summary>Środek opisu [j.]; numer toru i opis odcinka – sam punkt symbolu.</summary>
    public int[] OpisAt(ZobRodzaj r, ZobSymbol s) => r is ZobRodzaj.NumerToru or ZobRodzaj.OpisOdcinka or ZobRodzaj.Przerwa ? s.P : s.Opis ?? OpisDomyslny(r, s);

    string Label(ZobRodzaj r, ZobSymbol s)
    {
        if (Ut == null) return "";
        if (r == ZobRodzaj.Zwrotnica) return Ut.Turnouts.FirstOrDefault(z => z.Id == s.Ut)?.Number ?? Ut.Crossings.FirstOrDefault(c => c.Id == s.Ut)?.Number ?? "?";
        if (r == ZobRodzaj.NumerToru) return Ut.Tracks.FirstOrDefault(t => t.Id == s.Ut)?.Number ?? "?";
        if (r == ZobRodzaj.OpisOdcinka) return Ut.Sections.FirstOrDefault(x => x.Id == s.Ut) is UtSection sc ? (sc.Name.Length > 0 ? sc.Name : "?") : "?";
        if (r == ZobRodzaj.Przejazd) return Ut.LevelCrossings.FirstOrDefault(x => x.Id == s.Ut) is UtLevelCrossing lc ? (lc.Name.Length > 0 ? lc.Name : UtLevelCrossing.CatName(lc.Cat)) : "?";
        if (r == ZobRodzaj.Szlak)
        {
            if (Ut.Lines.FirstOrDefault(x => x.Id == s.Ut) is not UtLine l) return "?";
            var e = l.Konce.ElementAtOrDefault(s.Koniec ?? 0);
            return e != null && e.Oznaczenie.Length > 0 ? e.Oznaczenie : l.Numer;
        }
        if (r == ZobRodzaj.Przerwa) return "";
        var d = Ut.Devices.FirstOrDefault(x => x.Id == s.Ut);
        if (d == null) return "?";
        if (d.Kind == UtDevice.Semafor && d.Funkcja == UtDevice.FPk) return d.Name.StartsWith("PK") ? d.Name : "PK " + d.Name;
        return r == ZobRodzaj.Kozioł ? "" : d.Name;
    }

    FormattedText Txt(string s, double size, Brush b) => new(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Mono, size, b, 1.0);

    // ------------------------------------------------------------------ rysowanie
    void DrawSymbols(DrawingContext dc)
    {
        if (Obraz == null || Ut == null) return;
        dc.PushTransform(new MatrixTransform(Zoom, 0, 0, Zoom, Off.X, Off.Y));
        foreach (ZobRodzaj r in Enum.GetValues<ZobRodzaj>())
            foreach (var s in ZobSymbole.Lista(Obraz, r))
            {
                if (s.P == null) continue;
                bool sel = SelectedSym is var (sr, su) && sr == r && su == s.Ut;
                DrawSymbol(dc, r, s, sel);
            }
        dc.Pop();
    }

    void DrawSymbol(DrawingContext dc, ZobRodzaj r, ZobSymbol s, bool sel)
    {
        var (dx, dy, tor) = Dir(r, s);
        double ang = Math.Atan2(dy, dx) * 180 / Math.PI;
        var c = new Point(s.P[0], s.P[1]);
        var grey = Grey; var bg = Bg;
        // symbol bez toru albo przypięty do usuniętego obiektu – czerwony (znacznik edytora)
        if (tor == null && ZobSymbole.NaLinii(r) || ZobSymbole.RodzajUt(Ut, s.Ut) != r) grey = B(0xff, 0x40, 0x40);
        if (r is ZobRodzaj.Przejazd or ZobRodzaj.OpisOdcinka) ang = 0;      // linia przejazdu zawsze pionowo, opis poziomo
        dc.PushTransform(new RotateTransform(ang, c.X, c.Y));
        double cx = c.X, cy = c.Y;
        string label = Label(r, s); Brush lc = SwLabel; double ls = 9;
        switch (r)
        {
            case ZobRodzaj.Sygnalizator:
                {
                    var d = Ut.Devices.FirstOrDefault(x => x.Id == s.Ut);
                    double h = 6.2, tipX = cx + 8;
                    var pen = new Pen(grey, 2.4) { LineJoin = PenLineJoin.Miter };
                    lc = SigName; ls = 10;
                    if (d?.Kind == UtDevice.TarczaMan) { dc.DrawGeometry(null, pen, Chevron(tipX, cy, h, 10)); lc = TmName; }
                    else if (d?.Kind == UtDevice.TarczaOstrz) { dc.DrawGeometry(null, new Pen(grey, 1.8), Chevron(cx + 6, cy, h * 0.8, 9)); dc.DrawEllipse(grey, null, new Point(cx - 1, cy), 1.8, 1.8); ls = 8; }
                    else if (d?.Funkcja == UtDevice.FOdstepowy) { ls = 8; lc = SwLabel; }        // na szlaku SBL – sam numer (jak na pulpicie)
                    else if (d?.Funkcja == UtDevice.FPk) { dc.DrawGeometry(null, new Pen(grey, 1.6), Tri(cx + 5, cy, 4.5, false)); ls = 8; lc = SwLabel; }
                    else if (d?.Funkcja == UtDevice.FPowtarzacz) { dc.DrawGeometry(null, new Pen(grey, 1.6), Tri(cx + 6, cy, h * 0.8, false)); ls = 8; }
                    else if (d?.Funkcja == UtDevice.FWjazdowy)
                    {
                        // wjazdowy z funkcją końca przebiegu wyjazdowego (Ie-104 § 8 ust. 3 lit. f): mały trójkąt za semaforem, w stronę szlaku
                        dc.DrawGeometry(grey, null, Tri(tipX, cy, h + 1, true));
                        dc.DrawRectangle(bg, null, new Rect(cx + 15 - 6, cy - 4, 12, 8));
                        dc.DrawGeometry(grey, null, TriBack(cx + 18, cy, 5));
                    }
                    else
                    {
                        // pociągowy: pełny trójkąt; z sygnalizacją manewrową (komora biała – Ms2) także otwarty grot za nim (symbol 52.6)
                        dc.DrawGeometry(grey, null, Tri(tipX, cy, h + 1, true));
                        if (d != null && d.LightsOrDefault.Contains('B')) dc.DrawGeometry(null, pen, Chevron(tipX - 11.5, cy, h + 1, 9));
                    }
                    if (sel) dc.DrawRectangle(null, new Pen(Azure, 1.6), new Rect(cx - 17, cy - h - 4, 28, 2 * h + 8));
                    break;
                }
            case ZobRodzaj.Zwrotnica:
                {
                    // stan zasadniczy: nieczynne ramię z przerwą przy iglicy (jak na pulpicie)
                    var z = Ut.Turnouts.FirstOrDefault(x => x.Id == s.Ut);
                    var br = Branch(s);
                    dc.Pop();
                    if (z != null && br is var (bx, by))
                    {
                        bool straightLive = z.Zas != "-";
                        var gapPen = new Pen(bg, 6) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
                        double l = 9 / Math.Sqrt(bx * bx + by * by);
                        if (straightLive) dc.DrawLine(gapPen, new Point(cx + bx * 2, cy + by * 2), new Point(cx + bx * l, cy + by * l));
                        else { int tip = bx != 0 ? -bx : 1; dc.DrawLine(gapPen, new Point(cx - tip * 2, cy), new Point(cx - tip * 9, cy)); }
                    }
                    dc.PushTransform(Transform.Identity);
                    lc = White; ls = 9.5;
                    if (sel) dc.DrawRectangle(null, new Pen(Azure, 1.6), new Rect(cx - 12, cy - 10, 24, 20));
                    break;
                }
            case ZobRodzaj.Wykolejnica:
                dc.DrawRectangle(grey, null, new Rect(cx - 2.5, cy - 9, 5, 18));
                if (sel) dc.DrawRectangle(null, new Pen(Azure, 1.6), new Rect(cx - 6, cy - 12, 12, 24));
                break;
            case ZobRodzaj.Przerwa:
                {
                    // przerwa izolacyjna – przerwa w linii toru jak na pulpicie (granica odcinków); kółko – znacznik edytora
                    dc.DrawLine(new Pen(bg, 7) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat }, new Point(cx - 1.8, cy), new Point(cx + 1.8, cy));
                    if (!Czysty) dc.DrawEllipse(null, NodePen, c, 4.5, 4.5);
                    if (!Czysty && grey != Grey) dc.DrawEllipse(null, new Pen(grey, 1.2), c, 4.5, 4.5);
                    if (sel) dc.DrawRectangle(null, new Pen(Azure, 1.6), new Rect(cx - 7, cy - 7, 14, 14));
                    break;
                }
            case ZobRodzaj.NumerToru:
                // numer toru w przerwie linii, na czarnym tle (PanelView: oznaczenia torów stacyjnych)
                dc.DrawRectangle(bg, null, new Rect(cx - 10, cy - 6, 20, 12));
                ls = 9.5;
                if (sel) dc.DrawRectangle(null, new Pen(Azure, 1.6), new Rect(cx - 12, cy - 8, 24, 16));
                break;
            case ZobRodzaj.OpisOdcinka:
                ls = 9; lc = grey == Grey ? SwLabel : grey;
                if (sel) dc.DrawRectangle(null, new Pen(Azure, 1.6), new Rect(cx - Math.Max(8, label.Length * 3) - 3, cy - 7, 2 * Math.Max(8, label.Length * 3) + 6, 14));
                break;
            case ZobRodzaj.Przejazd:
                {
                    // linia przejazdu w poprzek torów z „Y” na końcach (PanelView: DrawCrossMark), opis różowy
                    int up = s.Zasieg?[0] ?? 28, down = s.Zasieg?[1] ?? 28;
                    var pink = grey == Grey ? CrossPink : grey;
                    var pp = new Pen(pink, 2.6);
                    dc.DrawLine(new Pen(pink, 3), new Point(cx, cy - up + 14), new Point(cx, cy + down - 14));
                    foreach (var (yy, side) in new[] { (cy - up, -1), (cy + down, 1) })
                    {
                        dc.DrawLine(pp, new Point(cx, yy), new Point(cx, yy - side * 14));
                        dc.DrawLine(pp, new Point(cx, yy - side * 10), new Point(cx - 6, yy - side * 16));
                        dc.DrawLine(pp, new Point(cx, yy - side * 10), new Point(cx + 6, yy - side * 16));
                    }
                    lc = pink; ls = 8;
                    if (sel) dc.DrawRectangle(null, new Pen(Azure, 1.6), new Rect(cx - 9, cy - up - 3, 18, up + down + 6));
                    break;
                }
            case ZobRodzaj.Szlak:
                {
                    // strzałka blokady w stanie zasadniczym: szara dwustronna, zielona kreska – szlak wolny i bez przebiegu (ROZWOJ § 2a);
                    // oś wzdłuż toru, „+” – na zewnątrz posterunku
                    dc.DrawLine(new Pen(bg, 9) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat }, new Point(cx - 18, cy), new Point(cx + 18, cy));
                    var sg = grey == Grey ? Grey : grey;
                    dc.DrawLine(new Pen(sg, 6) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat }, new Point(cx - 7, cy), new Point(cx + 7, cy));
                    foreach (var dd in new[] { 1, -1 })
                    {
                        var g = new StreamGeometry();
                        using (var cc = g.Open()) { cc.BeginFigure(new Point(cx + dd * 16, cy), true, true); cc.LineTo(new Point(cx + dd * 6, cy - 6.5), true, false); cc.LineTo(new Point(cx + dd * 6, cy + 6.5), true, false); }
                        g.Freeze(); dc.DrawGeometry(sg, null, g);
                    }
                    lc = SigName; ls = 10;
                    if (sel) dc.DrawRectangle(null, new Pen(Azure, 1.6), new Rect(cx - 19, cy - 9, 38, 18));
                    break;
                }
            case ZobRodzaj.Kozioł:
                {
                    // nawias zamykający koniec toru, otwarty w stronę toru
                    double bx = cx + 1, tt = -6;
                    var pen = new Pen(grey, 2.4) { StartLineCap = PenLineCap.Square, EndLineCap = PenLineCap.Square };
                    dc.DrawLine(pen, new Point(bx, cy - 8), new Point(bx, cy + 8));
                    dc.DrawLine(pen, new Point(bx, cy - 8), new Point(bx + tt, cy - 8));
                    dc.DrawLine(pen, new Point(bx, cy + 8), new Point(bx + tt, cy + 8));
                    if (sel) dc.DrawRectangle(null, new Pen(Azure, 1.6), new Rect(cx - 9, cy - 12, 14, 24));
                    break;
                }
        }
        dc.Pop();
        if (label.Length > 0)
        {
            var o = OpisAt(r, s);
            if (opisDrag is var (dr, du, dp) && dr == r && du == s.Ut) o = dp;
            var ft = Txt(label, ls, lc);
            dc.DrawText(ft, new Point(o[0] - ft.Width / 2, o[1] - ft.Height / 2));
            if (r == ZobRodzaj.Zwrotnica && Ut.Turnouts.FirstOrDefault(x => x.Id == s.Ut) is UtTurnout z)
            {
                // „+” – położenie zasadnicze (Ie-104 § 8 ust. 8 lit. b): przy ramieniu zasadniczym, z dala od ostrza
                var br = Branch(s);
                int tip = br is var (bx, _) && bx != 0 ? -bx : 1;
                bool down = br is var (_, by) && by > 0;
                double px = o[0] - tip * (5 + label.Length * 3.2);
                double py = z.Zas == "-" ? s.P[1] + (down ? 13 : -12) - 2 : o[1] - 2;
                var plus = Txt("+", 8, White);
                dc.DrawText(plus, new Point(px - plus.Width / 2, py - plus.Height / 2));
            }
            if (r == ZobRodzaj.Szlak)
            {
                // zielona kreska nad strzałką – szlak wolny, brak przebiegu na szlak (stan zasadniczy)
                dc.DrawRectangle(Green, null, new Rect(o[0] - 6, o[1] - ft.Height / 2 - 5, 12, 3));
            }
            if (s.Opis != null && SelectedSym is var (sr, su) && sr == r && su == s.Ut)
                dc.DrawLine(new Pen(Azure, 0.8) { DashStyle = DashStyles.Dot }, new Point(s.P[0], s.P[1]), new Point(o[0], o[1]));
        }
    }

    static Geometry Tri(double tipX, double cy, double h, bool filled)
    {
        var g = new StreamGeometry();
        using (var c = g.Open()) { c.BeginFigure(new Point(tipX, cy), filled, true); c.LineTo(new Point(tipX - 1.5 * h, cy - h), true, false); c.LineTo(new Point(tipX - 1.5 * h, cy + h), true, false); }
        g.Freeze(); return g;
    }
    static Geometry TriBack(double tipX, double cy, double h)
    {
        var g = new StreamGeometry();
        using (var c = g.Open()) { c.BeginFigure(new Point(tipX, cy), true, true); c.LineTo(new Point(tipX + 1.5 * h, cy - h), true, false); c.LineTo(new Point(tipX + 1.5 * h, cy + h), true, false); }
        g.Freeze(); return g;
    }
    static Geometry Chevron(double tipX, double cy, double h, double len)
    {
        var g = new StreamGeometry();
        using (var c = g.Open()) { c.BeginFigure(new Point(tipX - len, cy - h), false, false); c.LineTo(new Point(tipX, cy), true, false); c.LineTo(new Point(tipX - len, cy + h), true, false); }
        g.Freeze(); return g;
    }

    // ------------------------------------------------------------------ trafienia
    /// <summary>Opis symbolu pod punktem ekranu.</summary>
    public (ZobRodzaj R, string Ut)? HitOpis(Point p)
    {
        if (Obraz == null || Ut == null) return null;
        var (x, y) = ToImage(p);
        foreach (ZobRodzaj r in Enum.GetValues<ZobRodzaj>())
            foreach (var s in ZobSymbole.Lista(Obraz, r).Where(s => s.P != null))
            {
                if (r is ZobRodzaj.NumerToru or ZobRodzaj.Przerwa) continue;
                var l = Label(r, s); if (l.Length == 0) continue;
                var o = OpisAt(r, s); double w = Math.Max(8, l.Length * 6) / 2 + 2, h = 7;
                if (Math.Abs(x - o[0]) <= w && Math.Abs(y - o[1]) <= h) return (r, s.Ut);
            }
        return null;
    }

    /// <summary>Symbol pod punktem ekranu (do 12 j. od punktu symbolu).</summary>
    public (ZobRodzaj R, string Ut)? HitSym(Point p)
    {
        if (Obraz == null) return null;
        var (x, y) = ToImage(p);
        (ZobRodzaj, string)? best = null; double bd = 12;
        foreach (ZobRodzaj r in Enum.GetValues<ZobRodzaj>())
            foreach (var s in ZobSymbole.Lista(Obraz, r).Where(s => s.P != null))
            {
                double d = Math.Sqrt((x - s.P[0]) * (x - s.P[0]) + (y - s.P[1]) * (y - s.P[1]));
                if (d < bd) { bd = d; best = (r, s.Ut); }
            }
        return best;
    }

    /// <summary>Wskaż: opis – przeciąganie, symbol – zaznaczenie (true – obsłużone).</summary>
    bool SymDown(Point p)
    {
        if (HitOpis(p) is var (r, u))
        {
            var s = ZobSymbole.Lista(Obraz, r).First(x => x.Ut == u);
            SelectSym(r, u); opisDrag = (r, u, OpisAt(r, s)); return true;
        }
        if (HitSym(p) is var (r2, u2)) { SelectSym(r2, u2); return true; }
        return false;
    }

    void SymMove(Point p)
    {
        if (opisDrag is var (r, u, _)) { var (gx, gy) = Snap(p); opisDrag = (r, u, new[] { gx, gy }); }
    }

    bool SymUp()
    {
        if (opisDrag is not var (r, u, pt)) return false;
        opisDrag = null;
        var s = ZobSymbole.Lista(Obraz, r).FirstOrDefault(x => x.Ut == u);
        var cur = s != null ? OpisAt(r, s) : null;
        if (cur != null && (cur[0] != pt[0] || cur[1] != pt[1])) OpisMoved?.Invoke(r, u, pt);
        InvalidateVisual();
        return true;
    }
}

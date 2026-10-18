using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorZobrazowania;

/// <summary>
/// Symbole na torach obrazu (etap Z3): zwrotnice, sygnalizatory, wykolejnice, kozły – kształty jak na pulpicie (<c>PanelView</c>,
/// Ie-104 § 8 ust. 3, 8, 10; wzory SimRail), w stanie zasadniczym (szare), w jednostkach obrazu, obrócone wzdłuż toru. Opis – miejsce
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

    static readonly Brush SigName = B(0xe8, 0xc0, 0x00), TmName = B(0x18, 0xc0, 0xc0), SwLabel = B(0xb4, 0xb4, 0xb4), White = B(0xff, 0xff, 0xff), Azure = B(0x00, 0xa0, 0xff);
    static readonly Typeface Mono = new("Consolas");

    public void SelectSym(ZobRodzaj? r, string ut)
    {
        SelectedSym = r is ZobRodzaj rr && ut != null ? (rr, ut) : null;
        if (SelectedSym != null) Selected = null;
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
            default: return new[] { x, y + 15 };
        }
    }

    public int[] OpisAt(ZobRodzaj r, ZobSymbol s) => s.Opis ?? OpisDomyslny(r, s);

    string Label(ZobRodzaj r, ZobSymbol s)
    {
        if (Ut == null) return "";
        if (r == ZobRodzaj.Zwrotnica) return Ut.Turnouts.FirstOrDefault(z => z.Id == s.Ut)?.Number ?? Ut.Crossings.FirstOrDefault(c => c.Id == s.Ut)?.Number ?? "?";
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
        if (tor == null || ZobSymbole.RodzajUt(Ut, s.Ut) != r) grey = B(0xff, 0x40, 0x40);
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

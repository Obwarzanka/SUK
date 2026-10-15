using System.Globalization;
using System.Windows;
using System.Windows.Media;
using LcsWidzew.Model;

namespace LcsWidzew.UI;

/// <summary>
/// Znaki umowne planu schematycznego wg Ig-10 [D-27], załącznik nr 8 – wspólne dla planu w edytorze i okna „Plan stacji”.
/// Wymiary z załącznika podane są w milimetrach arkusza; u – jednostki rysunku na 1 mm.
/// Pulpit dyżurnego (zobrazowanie) rysuje się osobno, wg Ie-104.
/// </summary>
public sealed class Ig10
{
    readonly DrawingContext dc;
    readonly double u, dip;
    static readonly Typeface Font = new("Arial");

    static Brush Fz(byte r, byte g, byte b) { var x = new SolidColorBrush(Color.FromRgb(r, g, b)); x.Freeze(); return x; }
    public static readonly Brush Ink = Brushes.Black, Elec = Fz(0x00, 0x40, 0xd0), Grey = Fz(0x80, 0x80, 0x80), Paper = Brushes.White;

    public Ig10(DrawingContext dc, double u, double pixelsPerDip) { this.dc = dc; this.u = u; dip = pixelsPerDip; }

    Pen P(double mm, Brush b = null, double[] dash = null)
    {
        var p = new Pen(b ?? Ink, Math.Max(0.05, mm * u)) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat, LineJoin = PenLineJoin.Round };
        // długości kresek w mm → w jednostkach grubości pióra (WPF)
        if (dash != null) p.DashStyle = new DashStyle(dash.Select(d => d / Math.Max(0.05, mm)), 0);
        return p;
    }
    static Vector Unit(Vector v) => v.Length < 1e-9 ? new Vector(1, 0) : v / v.Length;
    static Vector Norm(Vector v) => new(-v.Y, v.X);

    /// <summary>Tekst o wysokości „mm” (wysokość wersalików, jak w tabelach załącznika 8).</summary>
    public void Text(string s, double x, double y, double mm, Brush b = null, int align = 0, bool paper = false)
    {
        if (string.IsNullOrEmpty(s)) return;
        var ft = new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Font, Math.Max(0.5, mm * u * 1.4), b ?? Ink, dip);
        double x0 = align < 0 ? x : align > 0 ? x - ft.Width : x - ft.Width / 2;
        if (paper) dc.DrawRectangle(Paper, null, new Rect(x0 - 0.6 * u, y - ft.Height / 2, ft.Width + 1.2 * u, ft.Height));
        dc.DrawText(ft, new Point(x0, y - ft.Height / 2));
    }

    // ------------------------------------------------------------------ tory
    /// <summary>Oś toru: główny 0,5 mm, boczny 0,35 mm; zelektryfikowany niebieski (§ 4 ust. 2); rozebrany / nieczynny – kreska 2, odstęp 1.</summary>
    public void Track(Edge e, IList<Point> pts, Brush color = null, double? mm = null)
    {
        if (pts.Count < 2) return;
        double w = mm ?? (e.Main ? 0.5 : 0.35);
        var pen = e.Dismantled ? P(0.35, color ?? Grey, new double[] { 2, 1 }) : P(w, color ?? (e.Elec ? Elec : Ink));
        var g = new StreamGeometry();
        using (var c = g.Open()) { c.BeginFigure(pts[0], false, false); c.PolyLineTo(pts.Skip(1).ToList(), true, true); }
        g.Freeze(); dc.DrawGeometry(null, pen, g);
    }

    /// <summary>Numer toru w przerwie osi toru (—— 8 ——), tekst 2,5 mm.</summary>
    public void TrackNumber(string num, Point a, Point b, Brush color = null) => Text(num, (a.X + b.X) / 2, (a.Y + b.Y) / 2, 2.5, color, 0, true);

    // ------------------------------------------------------------------ rozjazdy
    /// <summary>
    /// Rozjazd zwyczajny: kreska poprzeczna na początku rozjazdu, numer nad nią (1,8 mm) i klin między torami.
    /// Klin czarny – położenie zasadnicze na tor prosty (główny), klin jasny z kreską wzdłuż odgałęzienia – na tor zwrotny;
    /// zwrotnica nieuzależniona (ręczna) – klin kreskowany prostopadle.
    /// </summary>
    public void Switch(Station L, SwitchDef sw, Brush color = null, bool number = true)
    {
        var ink = color ?? Ink;
        var n = L.Nodes[sw.Node]; var o = new Point(n.X, n.Y);
        Vector Leg(string eid, out double len)
        {
            var e = L.Edges[eid]; var pts = e.Pts.Select(q => new Point(q.X, q.Y)).ToList(); if (e.A != sw.Node) pts.Reverse();
            len = (pts[1] - pts[0]).Length; return Unit(pts[1] - pts[0]);
        }
        var up = Leg(sw.Plus, out var lp); var um = Leg(sw.Minus, out var lm);
        bool plusStraight = Math.Abs(up.Y) <= Math.Abs(um.Y) + 1e-6;
        // klin: 8 mm wzdłuż toru prostego, po torze zwrotnym do końca jego pierwszego odcinka (łącznik bywa krótki)
        double ls = plusStraight ? lp : lm, lc = plusStraight ? lm : lp;
        double wl = Math.Min(8 * u, 0.6 * ls), wc = Math.Min(wl, 0.95 * lc);
        var p1 = o + up * (plusStraight ? wl : wc); var p2 = o + um * (plusStraight ? wc : wl);
        var tri = new StreamGeometry();
        using (var c = tri.Open()) { c.BeginFigure(o, true, true); c.LineTo(p1, true, false); c.LineTo(p2, true, false); }
        tri.Freeze();
        if (sw.Manual)
        {
            // nieuzależniona – kreskowanie prostopadłe do toru zasadniczego
            dc.DrawGeometry(null, P(0.18, ink), tri);
            var basis = plusStraight ? up : um; var other = plusStraight ? p2 : p1;
            for (double t = 1 * u; t < wl; t += 0.8 * u)
            {
                var a = o + basis * t; var far = o + Unit(other - o) * (t / Math.Max(1e-6, Vector.Multiply(Unit(other - o), basis)));
                dc.DrawLine(P(0.18, ink), a, far);
            }
        }
        else if (plusStraight) dc.DrawGeometry(ink, P(0.18, ink), tri);
        else { dc.DrawGeometry(null, P(0.18, ink), tri); dc.DrawLine(P(0.5, ink), o, p1); }
        // kreska początku rozjazdu (poprzeczna do toru przed rozjazdem)
        var tipU = sw.Tip != null && L.Edges.ContainsKey(sw.Tip) ? Leg(sw.Tip, out _) : -up;
        var nrm = Unit(Norm(tipU));
        dc.DrawLine(P(0.18, ink), o + nrm * 1.0 * u, o - nrm * 1.0 * u);
        // numer – po stronie przeciwnej do odgałęzienia
        bool minusDown = um.Y > up.Y;
        if (number) Text(sw.Label, o.X + tipU.X * 1.5 * u, o.Y + (minusDown ? -2.8 : 2.8) * u, 1.8, ink);
    }

    // ------------------------------------------------------------------ koniec toru, wykolejnica, kontrola niezajętości
    /// <summary>Kozioł oporowy: „]” na końcu toru (odcinek a 1,5 mm w obie strony osi, haczyki b 1 mm w stronę toru).</summary>
    public void BufferStop(Point end, Vector intoTrack, Brush color = null)
    {
        var d = Unit(intoTrack); var nr = Norm(d); var pen = P(0.18 * 2, color);
        var a = end + nr * 1.5 * u; var b = end - nr * 1.5 * u;
        dc.DrawLine(pen, a, b); dc.DrawLine(pen, a, a + d * 1 * u); dc.DrawLine(pen, b, b + d * 1 * u);
    }

    /// <summary>Wykolejnica: belka na toku (2 × 0,5 mm) i grot pod kątem 20° w stronę wykolejenia, opis (np. Wk2).</summary>
    public void Derailer(Point p, Vector along, string label, Brush color = null)
    {
        var d = Unit(along); var nr = Norm(d); if (nr.Y < 0) nr = -nr;   // wykolejenie na zewnątrz – w dół rysunku
        dc.DrawLine(P(0.5, color), p - nr * 0.2 * u, p + nr * 1.8 * u);
        var dir = Unit(d * Math.Cos(20 * Math.PI / 180) + nr * Math.Sin(20 * Math.PI / 180));
        var tip = p + nr * 0.8 * u + dir * 6 * u;
        dc.DrawLine(P(0.18, color), p + nr * 0.8 * u, tip);
        var back = -dir; var side = Norm(dir);
        var head = new StreamGeometry();
        using (var c = head.Open()) { c.BeginFigure(tip, true, true); c.LineTo(tip + back * 2 * u + side * 0.75 * u, true, false); c.LineTo(tip + back * 2 * u - side * 0.75 * u, true, false); }
        head.Freeze(); dc.DrawGeometry(color ?? Ink, null, head);
        Text(label, tip.X, tip.Y + 2.2 * u, 1.8, color);
    }

    /// <summary>Licznik osi: dwie kropki ⌀1 mm w odstępie 2 mm na osi toru.</summary>
    public void AxleCounter(Point p, Vector along, Brush color = null)
    {
        var d = Unit(along);
        dc.DrawLine(P(0.18, color), p - d * 2 * u, p + d * 2 * u);
        dc.DrawEllipse(color ?? Ink, null, p - d * 1 * u, 0.5 * u, 0.5 * u);
        dc.DrawEllipse(color ?? Ink, null, p + d * 1 * u, 0.5 * u, 0.5 * u);
    }

    /// <summary>Złącze izolowane (na styku dwóch szyn izolowanych): kreska poprzeczna 3 mm z poprzeczką 0,75 mm.</summary>
    public void InsulatedJoint(Point p, Vector along, Brush color = null)
    {
        var d = Unit(along); var nr = Norm(d); if (nr.Y > 0) nr = -nr;
        dc.DrawLine(P(0.18, color), p, p + nr * 3 * u);
        dc.DrawLine(P(0.18, color), p + nr * 3 * u - d * 0.375 * u, p + nr * 3 * u + d * 0.375 * u);
    }

    /// <summary>Granice odcinków kontroli niezajętości: (punkt, kierunek toru, rodzaj „lo”/„ot”).</summary>
    public static List<(Point P, Vector Along, string Det)> SectionBoundaries(Station L)
    {
        var res = new List<(Point, Vector, string)>();
        string Det(string sec) => L.Sections.TryGetValue(sec, out var sd) ? sd.Detection : "lo";
        foreach (var n in L.Nodes.Values)
        {
            var es = n.Edges.Select(x => L.Edges[x]).Where(x => !x.Link).ToList();
            if (es.Count < 2 || es.Select(x => x.Sec).Distinct().Count() < 2) continue;
            (Point P, Vector U) At(Edge e, double dist)
            {
                var pts = e.Pts.Select(q => new Point(q.X, q.Y)).ToList(); if (e.A != n.Id) pts.Reverse();
                var v = Unit(pts[1] - pts[0]); return (pts[0] + v * Math.Min(dist, (pts[1] - pts[0]).Length * 0.4), v);
            }
            if (n.Switch != null && L.Switches.TryGetValue(n.Switch, out var sw))
            {
                // granica przy rozjeździe – na torze spoza odcinka zwrotnicowego, kawałek od początku rozjazdu
                foreach (var e in es.Where(x => x.Sec != sw.Sec)) { var (p, v) = At(e, 14); res.Add((p, v, Det(e.Sec) == "ot" || Det(sw.Sec) == "ot" ? "ot" : "lo")); }
                continue;
            }
            var (p0, v0) = At(es[0], 0);
            res.Add((p0, v0, es.Any(x => Det(x.Sec) == "ot") ? "ot" : "lo"));
        }
        return res;
    }

    // ------------------------------------------------------------------ perony, przejazdy
    /// <summary>Peron: obrys 0,18 mm (krawędzie wzdłuż torów), opis 1,8 mm.</summary>
    public void Platform(PlatformDef p, Brush color = null)
    {
        dc.DrawRectangle(Paper, P(0.18, color), new Rect(p.X, p.Y, p.W, p.H));
        Text(p.Name, p.X + p.W / 2, p.Y + p.H / 2, 1.8, color);
    }

    /// <summary>Przejazd kolejowo-drogowy w poziomie szyn: krawędzie drogi przerywane (kreska 2, odstęp 1) z zakończeniami, kilometr / opis.</summary>
    public void Crossing(CrossingDef c, Brush color = null)
    {
        double w = 1.5 * u; var pen = P(0.18, color, new double[] { 2, 1 }); var pe = P(0.18, color);
        foreach (var s in new[] { -1, 1 })
        {
            double x = c.X + s * w;
            dc.DrawLine(pen, new Point(x, c.Y0), new Point(x, c.Y1));
            dc.DrawLine(pe, new Point(x, c.Y0), new Point(x + s * 1 * u, c.Y0));
            dc.DrawLine(pe, new Point(x, c.Y1), new Point(x + s * 1 * u, c.Y1));
        }
        Text(c.Name, c.X, c.Y0 - 2 * u, 1.8, color);
    }

    // ------------------------------------------------------------------ sygnalizatory
    /// <summary>Kolory komór (od słupa): G zielone, O pomarańczowe, R czerwone, W białe, B niebieskie.</summary>
    public static char[] LampColors(SignalDef s, int n)
    {
        char[] Pick(string full) => Enumerable.Range(0, n).Select(i => i < full.Length ? full[i] : full[^1]).ToArray();
        return s.Type switch
        {
            "tm" => n >= 2 ? Pick("BW") : new[] { 'W' },
            "to" or "sp" => n >= 2 ? Pick("OG") : new[] { 'O' },
            "odstepowy" => n switch { 1 => new[] { 'R' }, 2 => new[] { 'G', 'R' }, _ => Pick("GOR") },
            _ => n switch { 1 => new[] { 'R' }, 2 => new[] { 'G', 'R' }, 3 => s.ForShunts ? Pick("GRW") : Pick("GOR"), 4 => s.ForShunts ? Pick("GORW") : Pick("GORO"), _ => Pick("GOROW") }
        };
    }

    /// <summary>
    /// Semafor / tarcza świetlna: słupek od toru po prawej stronie w kierunku jazdy (2 mm), podstawa „⊢” i komory ⌀2 mm
    /// zwrócone do nadjeżdżającego pociągu. Semafor i tarcza ostrzegawcza – komory przekreślone (⊘), tarcza manewrowa – ⊗.
    /// lit – wypełnienie komór (stan na żywo); null – sam symbol.
    /// </summary>
    public void Signal(Station L, SignalDef s, Brush[] lit = null, Brush color = null)
    {
        if (s.Type == "pk") return;   // PK – punkt końcowy przebiegu istnieje tylko na pulpicie
        var ink = color ?? Ink;
        var n = L.Nodes[s.Node]; var o = new Point(n.X, n.Y);
        double dir = s.Dir >= 0 ? 1 : -1;
        var along = new Vector(dir, 0); var right = new Vector(0, dir);   // oś Y w dół: na wschód – prawa strona to dół
        var bse = o + right * 3 * u;
        dc.DrawLine(P(0.18, ink), o, bse);
        dc.DrawLine(P(0.18, ink), bse - along * 0.0 + new Vector(0, -1) * 1 * u, bse + new Vector(0, 1) * 1 * u);
        var c0 = bse - along * 2 * u;
        dc.DrawLine(P(0.18, ink), bse, c0);
        int k = s.LampCount; double r = 1 * u;
        for (int i = 0; i < k; i++)
        {
            var c = c0 - along * (r + i * 2 * r);
            dc.DrawEllipse(lit != null && i < lit.Length ? lit[i] : Paper, P(0.18, ink), c, r, r);
            if (lit != null) continue;
            if (s.Type == "tm") { dc.DrawLine(P(0.18, ink), c + new Vector(-0.7, -0.7) * r, c + new Vector(0.7, 0.7) * r); dc.DrawLine(P(0.18, ink), c + new Vector(-0.7, 0.7) * r, c + new Vector(0.7, -0.7) * r); }
            else dc.DrawLine(P(0.18, ink), c + new Vector(-0.7, 0.7) * r, c + new Vector(0.7, -0.7) * r);
        }
        // opis po stronie podstawy (za słupkiem w kierunku jazdy)
        Text(s.Label, bse.X + along.X * 1 * u, bse.Y, 1.8, s.Type == "odstepowy" ? Grey : ink, dir > 0 ? -1 : 1);
    }
}

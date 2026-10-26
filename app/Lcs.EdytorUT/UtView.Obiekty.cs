using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Obiekty inżynieryjne (§ 5m): znaki Ig-10 zał. 8 rysowane po osi toru w mm arkusza (s. 41 most, s. 42 linia w tunelu i przejścia,
/// s. 43 tunel pod linią, przepust, wiadukt nad drogą, s. 44 wiadukt drogowy, skrzyżowanie torów w dwóch poziomach, s. 63 ściana i mur
/// oporowy). Wymiary, których tabelki Ig-10 nie podają (skrzydełka, odstęp boku od osi), odczytane z rysunku załącznika (<see cref="Wym"/>), zaakceptowane przez właściciela 2026-10-22.
/// Narzędzie Obiekt: obiekt wzdłuż toru – klik OD i klik DO (albo przeciągnięcie), ściana / mur – po stronie kursora przy DO;
/// obiekt poprzeczny – klik (prostopadle) albo przeciągnięcie wzdłuż osi przeszkody; Ctrl + klik na innym torze – dołożenie toru
/// do zaznaczonego obiektu wzdłuż toru.
/// </summary>
public partial class UtView
{
    /// <summary>Wymiary znaków [mm arkusza] odczytane z rysunków Ig-10 zał. 8 (tabelki podają tylko grubość linii i kreskowanie).</summary>
    public static class Wym
    {
        /// <summary>Most, linia w tunelu, tunel pod linią, przejście, kładka: bok od osi toru (przeszkody) – połowa szerokości znaku 6 mm.</summary>
        public const double Bok = 3;
        /// <summary>Skrzydełka mostu i tunelu: 7 mm wzdłuż, 3 mm na zewnątrz (ok. 23°).</summary>
        public const double SkrzDl = 7, SkrzOdch = 3;
        /// <summary>Wiadukt (s. 43, 44): bok 5 mm od osi toru dolnego / górnego, skrzydełka 3,4 mm wzdłuż przeszkody i 1,4 mm na zewnątrz.</summary>
        public const double WiadBok = 5, WiadSkrzDl = 3.4, WiadSkrzOdch = 1.4;
        /// <summary>Wiadukt drogowy (s. 44): połowa szerokości znaku wzdłuż toru 3,5 mm, krawędzie drogi 2,5 mm od osi drogi.</summary>
        public const double WiadDrogBok = 3.5, WiadDrogKraw = 2.5;
        /// <summary>Przepust: skrzydełka 1,5 mm na zewnątrz, 3 mm w bok.</summary>
        public const double PrzepDl = 1.5, PrzepOdch = 3;
        /// <summary>Ściana / mur oporowy: druga linia 1 mm dalej od toru, grupy kresek co 10 mm (ściana 3, mur 2), kreski co 0,5 mm.</summary>
        public const double MurGrub = 1, MurKrok = 10, MurKreska = 0.5;
        /// <summary>Linia 0,18 mm, kreskowanie: kreska 1 mm, odstęp 1 mm.</summary>
        public const double Linia = 0.18, Kreska = 1, Odstep = 1;
    }

    /// <summary>Rodzaj obiektu dla narzędzia Obiekt.</summary>
    public string ObKind = Obiekty.Most;
    /// <summary>Obiekt: tor, km od, km do, kąt przeszkody, zasięg, strona.</summary>
    public event Action<string, double, double, double, double, int> ObiektPlace;
    /// <summary>Dołożenie toru do obiektu: id obiektu, tor.</summary>
    public event Action<string, string> ObiektDoloz;

    (string Track, double S)? obFrom;           // klik OD (obiekt wzdłuż toru)
    (string Track, double S, Point At)? obPress; // wciśnięcie na torze

    public (string Track, double S)? ObFrom => obFrom;
    public void ObCancel() { obFrom = null; obPress = null; InvalidateVisual(); }

    bool ObPress(Point s)
    {
        // drugi klik (DO) – na torze pierwszego kliknięcia, także obok osi; Ctrl + klik – tor pod kursorem
        bool ctrl = Mods.HasFlag(System.Windows.Input.ModifierKeys.Control);
        var id = !ctrl && obFrom != null ? obFrom.Value.Track : HitTrack(s);
        if (id == null || Track(id) is not UtTrack tt) return false;
        obPress = (id, Geometry(tt).Nearest(ToWorld(s), 0.2).S, s);
        return true;
    }

    void ObRelease(Point at)
    {
        var (track, s, p0) = obPress.Value; obPress = null;
        var r = Geometry(Track(track));
        bool click = (p0 - at).Length < 8;
        var inf = Obiekty.Info(ObKind);
        if (click && Mods.HasFlag(System.Windows.Input.ModifierKeys.Control))
        {
            if (SelectedObj is (Kontrola.KObiekt, var oid) && Doc.P.Obiekty.FirstOrDefault(x => x.Id == oid) is UtObiekt o && Obiekty.Wzdluz(o)) ObiektDoloz?.Invoke(oid, track);
            else DraftInfo?.Invoke("Obiekt: Ctrl + klik na torze dokłada go do zaznaczonego obiektu wzdłuż toru (most, wiadukt, tunel) – najpierw zaznacz obiekt");
            InvalidateVisual(); return;
        }
        if (inf.Pol == Obiekty.Pol.Poprzeczny)
        {
            double ang = 90, reach = 10;
            if (!click)
            {
                var (pp, az) = r.At(s); var v = ToWorld(at) - pp;
                double a = Math.Atan2(v.Y, v.X) - az;
                ang = Math.Round(((a * 180 / Math.PI) % 180 + 180) % 180);
                if (ang < 1) ang = 90;
                reach = Math.Max(4, Math.Round(Math.Sqrt(v.X * v.X + v.Y * v.Y) / r.K));
            }
            ObiektPlace?.Invoke(track, Math.Round(s, 2), Math.Round(s, 2), ang, reach, 1);
            InvalidateVisual(); return;
        }
        // wzdłuż toru: przeciągnięcie – od–do od razu; klik – OD, drugi klik – DO
        (string Track, double S)? from = click ? obFrom : (track, s);
        if (from == null) { obFrom = (track, s); DraftInfo?.Invoke($"{inf.Name}: km od {s:0.0} na torze – kliknij w miejscu końca (DO); PPM / Esc – od nowa"); InvalidateVisual(); return; }
        var rf = Geometry(Track(from.Value.Track));
        double s1 = Math.Clamp(rf.Nearest(ToWorld(at), 0.2).S, 0, rf.Length), s0 = from.Value.S;
        obFrom = null;
        if (Math.Abs(s1 - s0) < 1) { DraftInfo?.Invoke($"{inf.Name}: kliknij na torze w miejscu początku, potem w miejscu końca obiektu (albo przeciągnij wzdłuż toru)"); InvalidateVisual(); return; }
        int side = DragDir(from.Value.Track, s1, at).Side;
        ObiektPlace?.Invoke(from.Value.Track, Math.Round(Math.Min(s0, s1), 2), Math.Round(Math.Max(s0, s1), 2), 90, 10, side);
        InvalidateVisual();
    }

    // ------------------------------------------------------------------ kształt na ekranie
    /// <summary>Próbka osi obiektu wzdłuż toru: punkt osi, kierunek i normalna na ekranie, odsunięcie skrajnych torów obiektu [px].</summary>
    record struct ObPt(Point P, Vector D, Vector N, double Lo, double Hi, XY W, double Az);

    List<ObPt> ObSamples(UtObiekt o, double delta = 0)
    {
        var t = Track(o.Track); if (t == null) return null;
        var r = Geometry(t); if (r.Elements.Count == 0) return null;
        double a = Math.Clamp(o.Lo + delta, 0, r.Length), b = Math.Clamp(o.Hi + delta, 0, r.Length);
        var extra = (o.Tory ?? new()).Select(x => (R: Track(x.Track) is UtTrack u ? Geometry(u) : null, x)).Where(x => x.R != null && x.R.Elements.Count > 0).ToList();
        int n = Math.Clamp((int)Math.Ceiling((b - a) / Math.Max(0.25, mpp * 3)), 1, 600);
        var res = new List<ObPt>();
        for (int i = 0; i <= n; i++)
        {
            double s = a + (b - a) * i / n; var (p, az) = r.At(s);
            var sp = ToScreen(p); var d = ScreenDir(az); var nn = new Vector(-d.Y, d.X);
            double lo = 0, hi = 0;
            foreach (var (rx, x) in extra)
            {
                double sx = rx.Nearest(p, 0.5).S; if (sx < Math.Min(x.S0, x.S1) - 1 + delta || sx > Math.Max(x.S0, x.S1) + 1 + delta) continue;
                double off = Vector.Multiply(ToScreen(rx.At(sx).P) - sp, nn); lo = Math.Min(lo, off); hi = Math.Max(hi, off);
            }
            res.Add(new ObPt(sp, d, nn, lo, hi, p, az));
        }
        return res;
    }

    /// <summary>Obiekt poprzeczny na ekranie: środek, kierunek osi przeszkody, prostopadła, końce.</summary>
    (Point C, Vector U, Vector V, Point A, Point B, double Az)? ObCross(UtObiekt o, double delta = 0)
    {
        var t = Track(o.Track); if (t == null) return null;
        var r = Geometry(t); if (r.Elements.Count == 0) return null;
        var (p, az) = r.At(Math.Clamp(o.S0 + delta, 0, r.Length));
        double ra = az + o.Angle * Math.PI / 180; var dw = Element.Dir(ra);
        var c = ToScreen(p); var A = ToScreen(p - dw * (o.Reach * r.K)); var B = ToScreen(p + dw * (o.Reach * r.K));
        var u = B - A; if (u.Length < 1e-6) u = ScreenDir(ra); u.Normalize();
        return (c, u, new Vector(-u.Y, u.X), A, B, az);
    }

    /// <summary>Obrys obiektu na ekranie (trafianie, zaznaczanie prostokątem).</summary>
    List<Point> ObiektPoly(UtObiekt o, double delta = 0)
    {
        double k = PxPerMm; var inf = Obiekty.Info(o.Kind);
        if (inf.Pol == Obiekty.Pol.Poprzeczny)
        {
            if (ObCross(o, delta) is not var (c, u, v, A, B, _)) return null;
            double w = Math.Max(6, (o.Kind == Obiekty.WiaduktDrog ? Wym.WiadDrogBok : Wym.Bok) * k);
            var ea = A; var eb = B;
            if (o.Kind == Obiekty.WiaduktDrog) { double h = Math.Max((B - c).Length, Wym.WiadBok * k); ea = c - u * h; eb = c + u * h; }
            return new() { ea + v * w, eb + v * w, eb - v * w, ea - v * w };
        }
        var sm = ObSamples(o, delta); if (sm == null || sm.Count < 2) return null;
        if (inf.Pol == Obiekty.Pol.ZBoku)
        {
            var bl = MurLine(o, sm); var outer = bl.Select((q, i) => q.P + q.Out * Math.Max(5, (Wym.MurGrub + 1) * k)).ToList(); var inner = bl.Select(q => q.P - q.Out * 4).ToList();
            inner.Reverse(); outer.AddRange(inner); return outer;
        }
        double H = Math.Max(5, (o.Kind is Obiekty.Wiadukt or Obiekty.WiaduktTor ? Wym.WiadBok : Wym.Bok) * k);
        var L = sm.Select(q => q.P + q.N * (q.Hi + H)).ToList(); var R = sm.Select(q => q.P + q.N * (q.Lo - H)).ToList();
        R.Reverse(); L.AddRange(R); return L;
    }

    /// <summary>Linia ściany / muru: punkt przy torze (Offset po stronie Side) i kierunek „od toru” na ekranie.</summary>
    List<(Point P, Vector Out)> MurLine(UtObiekt o, List<ObPt> sm)
    {
        double K = Geometry(Track(o.Track)).K;
        return sm.Select(q =>
        {
            var p = ToScreen(q.W + Element.Right(q.Az) * (o.Side * o.Offset * K)); var v = p - q.P;
            if (v.Length < 1e-6) v = q.N * o.Side; v.Normalize(); return (p, v);
        }).ToList();
    }

    public (string Kind, string Id)? HitObiekt(Point s)
    {
        if (Doc == null || !Shown("budowle")) return null;
        foreach (var o in Doc.P.Obiekty)
            if (ObiektPoly(o) is List<Point> poly && Inside(poly, s)) return (Kontrola.KObiekt, o.Id);
        return null;
    }

    // ------------------------------------------------------------------ rysowanie
    void DrawObiekty(DrawingContext dc, double dpi)
    {
        if (Doc == null || !Shown("budowle")) return;
        foreach (var o in Doc.P.Obiekty)
        {
            bool sel = SelectedObj is (Kontrola.KObiekt, var id) && id == o.Id;
            DrawObiekt(dc, o, sel ? Blue : DevInk, DragDelta(Kontrola.KObiekt, o.Id), dpi, true);
        }
        // podgląd narzędzia
        if (tool == UtTool.Obiekt && hover is XY h)
        {
            var inf = Obiekty.Info(ObKind);
            (string Track, double S)? from = obFrom ?? (obPress is var (pt, ps, pa) && dragging ? (pt, ps) : null);
            if (inf.Pol != Obiekty.Pol.Poprzeczny && from is var (ft, fs) && Track(ft) is UtTrack t)
            {
                var r = Geometry(t); double s1 = Math.Clamp(r.Nearest(h, 0.2).S, 0, r.Length);
                var tmp = new UtObiekt { Kind = ObKind, Track = ft, S0 = Math.Min(fs, s1), S1 = Math.Max(fs, s1), Side = DragDir(ft, s1, ToScreen(h)).Side };
                DrawObiekt(dc, tmp, Red, 0, dpi, false);
                Label(dc, $"{inf.Name}: {Math.Abs(s1 - fs):0.#} m" + (inf.Pol == Obiekty.Pol.ZBoku ? $" po {(tmp.Side > 0 ? "prawej" : "lewej")}" : "") + " – klik: koniec", ToScreen(h) + new Vector(12, 10), Red, dpi);
            }
            else if (inf.Pol == Obiekty.Pol.Poprzeczny && obPress is var (qt, qs, qa) && Track(qt) is UtTrack t2)
            {
                var r = Geometry(t2); var (pp, az) = r.At(qs); var v = h - pp; double len = Math.Sqrt(v.X * v.X + v.Y * v.Y);
                bool drag = (ToScreen(h) - qa).Length >= 8;
                double ang = drag ? ((Math.Atan2(v.Y, v.X) - az) * 180 / Math.PI % 180 + 180) % 180 : 90;
                var tmp = new UtObiekt { Kind = ObKind, Track = qt, S0 = qs, S1 = qs, Angle = ang < 1 ? 90 : ang, Reach = drag ? Math.Max(4, len / r.K) : 10 };
                DrawObiekt(dc, tmp, Red, 0, dpi, false);
            }
            else if (HitTrack(ToScreen(h)) != null)
                Label(dc, inf.Pol == Obiekty.Pol.Poprzeczny ? $"{inf.Name}: klik – prostopadle, przeciągnięcie – wzdłuż osi przeszkody" : $"{inf.Name}: klik w miejscu początku (OD)", ToScreen(h) + new Vector(12, 10), Red, dpi);
        }
    }

    void DrawObiekt(DrawingContext dc, UtObiekt o, Brush ink, double delta, double dpi, bool labels)
    {
        double k = PxPerMm; double pw = Math.Max(1, Wym.Linia * k * 1.4);
        var pen = new Pen(ink, pw) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
        var dash = new Pen(ink, pw) { DashStyle = new DashStyle(new[] { Math.Max(2, Wym.Kreska * k) / pw, Math.Max(2, Wym.Odstep * k) / pw }, 0) };
        Brush paper = Schemat ? Brushes.White : Paper;
        double M(double mm, double min = 3) => Math.Max(min, mm * k);
        void Poly(Pen p, IEnumerable<Point> pts)
        {
            var l = pts.ToList(); if (l.Count < 2) return;
            var g = new StreamGeometry();
            using (var c = g.Open()) { c.BeginFigure(l[0], false, false); for (int i = 1; i < l.Count; i++) c.LineTo(l[i], true, true); }
            g.Freeze(); dc.DrawGeometry(null, p, g);
        }
        // oś toru pod obiektem przerywana (tunel, tor dolny): zamalowanie osi tłem i kreski
        void UnderTrack(List<Point> axis)
        {
            if (BgOn && !PrintMode) { Poly(dash, axis); return; }
            Poly(new Pen(paper, Math.Max(3, 0.7 * k)) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat }, axis);
            Poly(new Pen(TrackInk, Math.Max(1.2, 0.35 * k)) { DashStyle = new DashStyle(new[] { Math.Max(2, Wym.Kreska * k) / Math.Max(1.2, 0.35 * k), Math.Max(2, Wym.Odstep * k) / Math.Max(1.2, 0.35 * k) }, 0) }, axis);
        }
        var inf = Obiekty.Info(o.Kind);
        Point labelAt; Vector labelN;
        if (inf.Pol == Obiekty.Pol.Poprzeczny)
        {
            if (ObCross(o, delta) is not var (c, u, v, A, B, az)) return;
            switch (o.Kind)
            {
                case Obiekty.Przepust:
                {
                    Poly(dash, new[] { A, B });
                    foreach (var (e, sg) in new[] { (A, -1.0), (B, 1.0) })
                        foreach (var sv in new[] { -1.0, 1.0 }) Poly(pen, new[] { e, e + u * (sg * M(Wym.PrzepDl, 2)) + v * (sv * M(Wym.PrzepOdch)) });
                    break;
                }
                case Obiekty.WiaduktDrog:
                {
                    // s. 44: prostokąt nad torami (boki wzdłuż drogi), krawędzie drogi kreskowane przez prostokąt, tor pod spodem przerywany
                    double w = M(Wym.WiadDrogBok), h = Math.Max((B - c).Length, M(Wym.WiadBok));
                    var ea = c - u * h; var eb = c + u * h;
                    var tr = Geometry(Track(o.Track)); double ds = Math.Max(1, w / Math.Max(1e-6, (ToScreen(tr.At(Math.Min(tr.Length, o.S0 + 1)).P) - c).Length));
                    var ax = new List<Point>(); for (int i = -6; i <= 6; i++) ax.Add(ToScreen(tr.At(Math.Clamp(o.S0 + delta + ds * i / 6, 0, tr.Length)).P));
                    UnderTrack(ax);
                    Poly(pen, new[] { ea + v * w, eb + v * w, eb - v * w, ea - v * w, ea + v * w });
                    foreach (var sv in new[] { -1.0, 1.0 }) Poly(dash, new[] { ea - u * M(5) + v * (sv * M(Wym.WiadDrogKraw)), eb + u * M(5) + v * (sv * M(Wym.WiadDrogKraw)) });
                    foreach (var (e, sg) in new[] { (ea, -1.0), (eb, 1.0) })
                        foreach (var sv in new[] { -1.0, 1.0 }) Poly(pen, new[] { e + v * (sv * w), e + v * (sv * (w + M(Wym.WiadSkrzOdch, 2))) + u * (sg * M(Wym.WiadSkrzDl)) });
                    break;
                }
                default:
                {
                    // tunel pod linią, przejścia (s. 42, 43): prostokąt wzdłuż osi przeszkody; kładka – ciągły ze schodami na końcach
                    double w = M(Wym.Bok);
                    bool kl = o.Kind == Obiekty.Kladka;
                    foreach (var sv in new[] { -1.0, 1.0 }) Poly(kl ? pen : dash, new[] { A + v * (sv * w), B + v * (sv * w) });
                    Poly(pen, new[] { A + v * w, A - v * w }); Poly(pen, new[] { B + v * w, B - v * w });
                    if (kl)
                    {
                        for (int i = 1; i <= 4; i++) foreach (var (e, sg) in new[] { (A, 1.0), (B, -1.0) }) { var q = e + u * (sg * i * M(0.8, 1.5)); Poly(pen, new[] { q + v * w, q - v * w }); }
                    }
                    else
                        foreach (var (e, sg) in new[] { (A, -1.0), (B, 1.0) })
                            foreach (var sv in new[] { -1.0, 1.0 }) Poly(pen, new[] { e + v * (sv * w), e + v * (sv * (w + M(Wym.SkrzOdch))) + u * (sg * M(Wym.SkrzDl)) });
                    break;
                }
            }
            labelAt = B; labelN = u;
        }
        else
        {
            var sm = ObSamples(o, delta); if (sm == null || sm.Count < 2) return;
            var first = sm[0]; var last = sm[^1];
            if (inf.Pol == Obiekty.Pol.ZBoku)
            {
                // s. 63: dwie linie wzdłuż toru po stronie ściany, grupy kresek ukośnych (ściana – 3, mur – 2) co 10 mm
                var bl = MurLine(o, sm); double g = M(Wym.MurGrub, 2);
                Poly(pen, bl.Select(q => q.P)); Poly(pen, bl.Select(q => q.P + q.Out * g));
                int nk = o.Kind == Obiekty.Sciana ? 3 : 2; double step = Math.Max(30, Wym.MurKrok * k), tick = M(Wym.MurKreska, 2);
                double acc = step / 2;
                for (int i = 1; i < bl.Count; i++)
                {
                    var a = bl[i - 1].P; var b = bl[i].P; double len = (b - a).Length; if (len < 1e-6) continue;
                    var dd = (b - a) / len;
                    while (acc <= len)
                    {
                        for (int j = 0; j < nk; j++)
                        {
                            var q = a + dd * (acc + j * tick);
                            Poly(pen, new[] { q, q + dd * tick + bl[i].Out * g });
                        }
                        acc += step;
                    }
                    acc -= len;
                }
                labelAt = bl[bl.Count / 2].P + bl[bl.Count / 2].Out * (g + 2); labelN = bl[bl.Count / 2].Out;
            }
            else
            {
                bool wiad = o.Kind is Obiekty.Wiadukt or Obiekty.WiaduktTor;
                double H = M(wiad ? Wym.WiadBok : Wym.Bok);
                var L = sm.Select(q => q.P + q.N * (q.Hi + H)).ToList(); var R = sm.Select(q => q.P + q.N * (q.Lo - H)).ToList();
                if (o.Kind == Obiekty.Tunel)
                {
                    UnderTrack(sm.Select(q => q.P).ToList());
                    foreach (var x in o.Tory ?? new()) if (Track(x.Track) is UtTrack tx) { var rx = Geometry(tx); var ax = new List<Point>(); int nx = Math.Clamp((int)((x.S1 - x.S0) / Math.Max(0.25, mpp * 3)), 1, 600); for (int i = 0; i <= nx; i++) ax.Add(ToScreen(rx.At(Math.Clamp(x.S0 + delta + (x.S1 - x.S0) * i / nx, 0, rx.Length)).P)); UnderTrack(ax); }
                    Poly(dash, L); Poly(dash, R);
                }
                else { Poly(pen, L); Poly(pen, R); }
                Poly(pen, new[] { L[0], R[0] }); Poly(pen, new[] { L[^1], R[^1] });
                // skrzydełka: most / tunel – na zewnątrz wzdłuż toru; wiadukt – w poprzek toru (wzdłuż przeszkody)
                foreach (var (q, e, sg) in new[] { (first, (L[0], R[0]), -1.0), (last, (L[^1], R[^1]), 1.0) })
                {
                    if (wiad)
                    {
                        Poly(pen, new[] { e.Item1, e.Item1 + q.N * M(Wym.WiadSkrzDl) + q.D * (sg * M(Wym.WiadSkrzOdch, 2)) });
                        Poly(pen, new[] { e.Item2, e.Item2 - q.N * M(Wym.WiadSkrzDl) + q.D * (sg * M(Wym.WiadSkrzOdch, 2)) });
                    }
                    else
                    {
                        Poly(pen, new[] { e.Item1, e.Item1 + q.D * (sg * M(Wym.SkrzDl)) + q.N * M(Wym.SkrzOdch) });
                        Poly(pen, new[] { e.Item2, e.Item2 + q.D * (sg * M(Wym.SkrzDl)) - q.N * M(Wym.SkrzOdch) });
                    }
                }
                // wiadukt nad drogą (s. 43): krawędzie drogi kreskowane pod obiektem
                if (o.Kind == Obiekty.Wiadukt && o.Width > 0)
                {
                    var tr = Geometry(Track(o.Track)); double sm0 = (o.Lo + o.Hi) / 2 + delta;
                    var (pm, azm) = tr.At(Math.Clamp(sm0, 0, tr.Length)); var rdW = Element.Dir(azm + o.Angle * Math.PI / 180);
                    double sin = Math.Max(0.2, Math.Abs(Math.Sin(o.Angle * Math.PI / 180)));
                    var mid = sm[sm.Count / 2];
                    foreach (var sg in new[] { -1.0, 1.0 })
                    {
                        var ew = tr.At(Math.Clamp(sm0 + sg * o.Width / 2 / sin, 0, tr.Length)).P; var es = ToScreen(ew);
                        var rd = ToScreen(ew + rdW) - es; if (rd.Length < 1e-9) continue; rd.Normalize();
                        double dn = Math.Abs(Vector.Multiply(rd, mid.N)); if (dn < 0.1) continue;
                        double up = (mid.Hi + H + M(4)) / dn, dw = (-(mid.Lo - H) + M(4)) / dn;
                        if (Vector.Multiply(rd, mid.N) < 0) (up, dw) = (dw, up);
                        Poly(dash, new[] { es - rd * dw, es + rd * up });
                    }
                }
                var mm = sm[sm.Count / 2]; labelAt = mm.P + mm.N * (mm.Hi + H + 2); labelN = mm.N;
            }
        }
        if (labels && (LabelsOn || PrintMode))
        {
            string km = Kilometraz.Text(Doc.P, o.Track, o.S0 + delta);
            var txt = (o.Name.Length > 0 ? o.Name : inf.Name) + (o.Nad.Length > 0 ? " – " + o.Nad : "") + (km.Length > 0 ? " " + km : "");
            var ft = new FormattedText(txt, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Arial"), FontPx(k) * 0.85, ink, dpi);
            // tekst zawsze poziomo, środkiem odsunięty od znaku w stronę normalnej (nie nachodzi na linie)
            if (labelN.Length > 1e-9) labelN.Normalize();
            var cc = labelAt + labelN * (Math.Abs(labelN.X) * ft.Width / 2 + Math.Abs(labelN.Y) * ft.Height / 2 + 3);
            dc.DrawText(ft, new Point(cc.X - ft.Width / 2, cc.Y - ft.Height / 2));
        }
    }
}

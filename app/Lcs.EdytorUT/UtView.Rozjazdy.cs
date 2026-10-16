using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Rozjazdy na płótnie: znaczniki (PR – kreska w poprzek toru, KR – kreski na obu torach, ŚR – kropka), klin położenia zasadniczego
/// (wzdłuż toru na wprost albo na zwrot), numer; skrzyżowania i rozjazdy krzyżowe (łuki przejazdów, końce a–d).
/// Narzędzie Rozjazd: wciśnij na torze w miejscu początku rozjazdu (PR) i przeciągnij w stronę odgałęzienia – puszczenie na torze
/// sąsiednim tworzy łącznik; typ krzyżowy / skrzyżowanie – klik na przecięciu torów. We Wskaż: klik – zaznaczenie, przeciągnięcie – przesunięcie wzdłuż toru.
/// </summary>
public partial class UtView
{
    public string SelectedTurnout { get; private set; }
    public string SelectedCrossing { get; private set; }
    /// <summary>Typ wstawianego rozjazdu (z katalogu).</summary>
    public string RzType = "Rz 60E1-190-1:9";
    /// <summary>Rozjazd do wstawienia (z przeciągnięcia myszą).</summary>
    public event Action<RzPlace> TurnoutPlace;
    /// <summary>
    /// Wstawienie rozjazdu: tor, km PR, kierunek, strona, tor docelowy łącznika (albo null), odstęp toru odgałęźnego [m] (0 – samo
    /// odgałęzienie do KR) i jego koniec [m od PR wzdłuż toru].
    /// </summary>
    public record RzPlace(string Track, double S, int Dir, int Side, string Target, double Offset, double EndAlong);
    /// <summary>Skrzyżowanie / rozjazd krzyżowy: klik w punkcie.</summary>
    public event Action<XY> CrossingPlace;
    public event Action<string, double> TurnoutMoved;

    public void SelectTurnout(string id)
    {
        SelectedTurnout = id; SelectedCrossing = null; SelectedTrack = null; SelectedVertex = -1; SelectedDevice = null;
        InvalidateVisual(); SelectionChanged?.Invoke();
    }
    public void SelectCrossing(string id)
    {
        SelectedCrossing = id; SelectedTurnout = null; SelectedTrack = null; SelectedVertex = -1; SelectedDevice = null;
        InvalidateVisual(); SelectionChanged?.Invoke();
    }

    /// <summary>Rozjazd, którego skos wyznacza wierzchołek <paramref name="i"/> toru (wierzchołek 2 odgałęzienia rozjazdu pojedynczego), albo null.</summary>
    public UtTurnout SkewTurnout(string track, int i) => Doc != null && i == 2 ? Rozjazdy.SkewOwner(Doc.P, track) : null;

    /// <summary>Wierzchołek odgałęzienia ustawiany przez rozjazd (nie do przeciągania ani usuwania).</summary>
    public bool Locked(string track, int i)
    {
        if (Doc == null) return false;
        var t = Track(track); if (t == null) return false;
        foreach (var r in Doc.P.Turnouts.Where(r => r.Branch == track))
            if (!r.BranchAtEnd ? i <= 1 : i >= t.Vertices.Count - 2) return true;
        return false;
    }

    // ------------------------------------------------------------------ narzędzie
    (string Track, double S)? rzFrom; XY rzTo;

    bool RzPress(Point s)
    {
        var typ = Katalog.Find(RzType);
        if (typ == null || typ.IsCrossing) return false;
        var id = HitTrack(s); if (id == null) return false;
        var r = Geometry(Track(id));
        rzFrom = (id, r.Nearest(ToWorld(s), 0.2).S); rzTo = ToWorld(s);
        return true;
    }

    /// <summary>
    /// Z przeciągnięcia: kierunek i strona względem toru, odstęp toru odgałęźnego (puszczenie obok toru, nie na torze) i jego koniec.
    /// </summary>
    RzPlace RzFromDrag(string track, double s, Point at)
    {
        var to = ToWorld(at);
        var r = Geometry(Track(track)); var (p, az) = r.At(s);
        var v = to - p; var d = Element.Dir(az);
        double along = v.X * d.X + v.Y * d.Y, across = d.X * v.Y - d.Y * v.X;   // across > 0 – w prawo od kierunku toru
        int dir = along >= 0 ? 1 : -1;
        int side = Math.Sign(across) * dir; if (side == 0) side = 1;
        var target = HitTrack(at); if (target == track) target = null;
        double off = 0;
        if (target == null && Katalog.Find(RzType) is RozjazdTyp typ) off = Rozjazdy.OffsetFor(Math.Abs(across) / r.K, typ, NewR > 0 ? NewR : 300);
        return new RzPlace(track, s, dir, side, target, off, Math.Abs(along) / r.K);
    }

    void RzRelease(Point s)
    {
        var (track, km) = rzFrom.Value; rzFrom = null;
        if ((ToScreen(Geometry(Track(track)).At(km).P) - s).Length < 8) { DraftInfo?.Invoke("Rozjazd: przeciągnij od miejsca początku rozjazdu w stronę odgałęzienia (obok toru – tor równoległy, na tor sąsiedni – łącznik)"); InvalidateVisual(); return; }
        TurnoutPlace?.Invoke(RzFromDrag(track, km, s));
        InvalidateVisual();
    }

    // ------------------------------------------------------------------ zaznaczanie i przesuwanie
    (string Id, double Grab, double S)? rzDrag;

    string HitTurnout(Point s)
    {
        if (Doc == null) return null;
        string best = null; double bd = 9;
        foreach (var t in Doc.P.Turnouts)
        {
            var g = Rozjazdy.Geometry(Doc.P, t); if (g == null) continue;
            var pr = ToScreen(g.PR);
            foreach (var q in new[] { pr, ToScreen(g.SR), pr + ScreenDir(g.Az) * 12 })
            { double d = (q - s).Length; if (d < bd) { bd = d; best = t.Id; } }
        }
        return best;
    }

    string HitCrossing(Point s)
    {
        if (Doc == null) return null;
        string best = null; double bd = 10;
        foreach (var c in Doc.P.Crossings)
            if (Rozjazdy.CrossGeo(Doc.P, c) is var (p, _, _, _)) { double d = (ToScreen(p) - s).Length; if (d < bd) { bd = d; best = c.Id; } }
        return best;
    }

    /// <summary>
    /// Wskaż: wciśnięcie na rozjeździe – zaznaczenie i przesuwanie wzdłuż toru (bez wcześniejszego zaznaczania). Drugi rozjazd łącznika
    /// przesuwa cały łącznik – przeciągany jest wtedy rozjazd pierwszy (po swoim torze).
    /// </summary>
    bool PressTurnout(Point s)
    {
        if (HitTurnout(s) is not string id) return false;
        var t = Doc.P.Turnouts.First(x => x.Id == id);
        var m = t.BranchAtEnd && t.Partner != null ? Doc.P.Turnouts.FirstOrDefault(x => x.Id == t.Partner) ?? t : t;
        if (SelectedTurnout != id) SelectTurnout(id);
        var r = Geometry(Track(m.Track));
        rzDrag = (m.Id, r.Nearest(ToWorld(s), 0.2).S - m.S, m.S);
        return true;
    }

    void DragTurnout(Point s)
    {
        var (id, grab, _) = rzDrag.Value; var t = Doc.P.Turnouts.First(x => x.Id == id);
        var r = Geometry(Track(t.Track));
        rzDrag = (id, grab, Math.Round(r.Nearest(ToWorld(s), 0.2).S - grab, 2));
        InvalidateVisual();
    }

    void EndTurnoutDrag(Point s)
    {
        var (id, _, ns) = rzDrag.Value; rzDrag = null;
        if (dragging) TurnoutMoved?.Invoke(id, ns);
        InvalidateVisual();
    }

    // ------------------------------------------------------------------ rysowanie
    static readonly Brush WedgeInk = Frozen(Color.FromRgb(0x10, 0x10, 0x10)), RzSel = Frozen(Color.FromArgb(0xb0, 0xff, 0x90, 0x00));
    static readonly Pen RzTick = FrozenPen(Color.FromRgb(0x10, 0x10, 0x10), 1.6), RzPreview = FrozenPen(Color.FromRgb(0xe0, 0x30, 0x10), 2), SlipPen = FrozenPen(Color.FromRgb(0x18, 0x18, 0x18), 1.6);

    void DrawTurnouts(DrawingContext dc, double dpi)
    {
        if (Doc == null) return;
        // przeciąganie: podgląd na kopii projektu (łącznik – oba rozjazdy i tor łączący na nowym miejscu)
        var src = Doc.P; UtTurnout moving = null;
        if (rzDrag is var (did, _, ds) && dragging)
        {
            src = UtDocument.Deserialize(UtDocument.Serialize(Doc.P));
            moving = src.Turnouts.FirstOrDefault(x => x.Id == did);
            if (moving != null) { moving.S = ds; Rozjazdy.Sync(src); }
        }
        foreach (var t0 in Doc.P.Turnouts)
        {
            var t = src.Turnouts.FirstOrDefault(x => x.Id == t0.Id) ?? t0;
            var g = Rozjazdy.Geometry(src, t); if (g == null) continue;
            DrawTurnout(dc, t, g, t.Id == SelectedTurnout || (SelectedTurnout != null && t.Partner == SelectedTurnout), dpi, Rozjazdy.Check(src, t).Count > 0);
        }
        if (moving != null && src.Tracks.FirstOrDefault(x => x.Id == moving.Branch) is UtTrack mb)
        {
            dc.DrawGeometry(null, RzPreview, Line(Trasa.Compute(mb).Sample(0.5), 0));
            var orig = Doc.P.Turnouts.First(x => x.Id == moving.Id);
            double dlt = moving.S - orig.S;
            if (hover is XY hv) Label(dc, $"rozjazd {moving.Number}{(moving.Partner != null ? " (łącznik)" : "")}: km {moving.S:0.00} (Δ {(dlt >= 0 ? "+" : "−")}{Math.Abs(dlt):0.00} m)", ToScreen(hv) + new Vector(12, 10), Red, dpi);
        }
        foreach (var c in Doc.P.Crossings) DrawCrossing(dc, c, c.Id == SelectedCrossing, dpi);
        if (tool == UtTool.Rozjazd) DrawRzPreview(dc, dpi);
    }

    /// <summary>
    /// Piksele na mm arkusza dla znaków Ig-10: wg skali rysunku, ale w stałym przedziale 2,2–3,6 px/mm (klin 8 mm: 18–29 px) –
    /// znaki czytelne przy oddaleniu i nie puchną przy przybliżaniu.
    /// </summary>
    public double PxPerMm => (Doc?.P.MetresPerSheetMm ?? 0.5) * stretch / mpp;

    /// <summary>Wielkość napisów [px]: tekst 1,8 mm arkusza, ale w granicach 10–15 px (czytelny przy każdym przybliżeniu).</summary>
    public static double FontPx(double k) => Math.Clamp(1.8 * 1.4 * k, 10, 15);
    /// <summary>Napisy przy znakach (numery rozjazdów, nazwy urządzeń) – gdy znaki mają co najmniej 1 px/mm.</summary>
    public bool LabelsOn => PxPerMm >= 1;

    /// <summary>Znak rozjazdu z bazy symboli Ig-10 (Dane/symbole_ig10.json): zaczepiony w PR, wzdłuż toru w stronę KR, po stronie odgałęzienia.</summary>
    public static string SymbolFor(UtTurnout t) => t.Manual ? "rz_nieuzal" : t.Zas == "-" ? "rz_uzal_zwrot" : "rz_uzal_wprost";

    /// <summary>
    /// Rozjazd wg Ig-10 (zał. 8 s. 18) i planów schematycznych: kreska początku w PR (2 mm), numer nad torem przed PR, czarny klin
    /// w kącie między osią toru zasadniczego a odgałęzieniem, do końca rozjazdu (KR), po stronie kierunku zamkniętego; od strony
    /// otwartej odstęp 0,5 mm. Zwrotnica nieuzależniona – klin biały z kreskowaniem. Klin liczony z geometrii rozjazdu na ekranie,
    /// więc zgadza się z rysunkiem toru w każdym widoku (Teren, Schemat ×4).
    /// </summary>
    void DrawTurnout(DrawingContext dc, UtTurnout t, TurnoutGeo g, bool sel, double dpi, bool err)
    {
        var pr = ToScreen(g.PR);
        if (pr.X < -150 || pr.Y < -150 || pr.X > ActualWidth + 150 || pr.Y > ActualHeight + 150) return;
        double k = PxPerMm;
        var dir = ScreenDir(g.Az);
        if (sel) dc.DrawEllipse(RzSel, null, pr + dir * (4 * k), Math.Max(9, 6 * k), Math.Max(9, 6 * k));
        // odgałęzienie z geometrii typu: PR – ŚR (łuk R) – KR na zwrot
        var br = Trasa.Compute(new[] { new UtVertex { X = g.PR.X, Y = g.PR.Y }, new UtVertex { X = g.SR.X, Y = g.SR.Y, R = g.Typ.R }, new UtVertex { X = g.KRDiv.X, Y = g.KRDiv.Y } }, g.K);
        var pts = br.Sample(0.5).Select(q => ToScreen(q.P)).ToList();
        double endAlong = (ToScreen(g.KRMain) - pr) * dir;
        var n = DrawWedge(dc, pr, dir, pts, endAlong, t.Zas != "-", t.Manual, k);
        DrawSwitchStart(dc, pr, dir, n, k, t.Number, dpi);
        if (err) { var es = ToScreen(g.SR); dc.DrawEllipse(null, ErrPen, es, 10, 10); ErrLabel(dc, $"{t.Number}: " + string.Join("; ", Rozjazdy.Check(Doc.P, t)), es, dpi); }
    }

    static readonly Pen StartPen = FrozenPen(Color.FromRgb(0x10, 0x10, 0x10), 1.3), WedgeOutline = FrozenPen(Color.FromRgb(0x10, 0x10, 0x10), 1);

    /// <summary>Kreska początku rozjazdu (2 mm w poprzek toru) i numer nad torem przed nią, po stronie odgałęzienia.</summary>
    void DrawSwitchStart(DrawingContext dc, Point pr, Vector d, Vector n, double k, string nr, double dpi)
    {
        var nn = new Vector(-d.Y, d.X);
        dc.DrawLine(StartPen, pr - nn * k, pr + nn * k);
        if (string.IsNullOrEmpty(nr) || !LabelsOn) return;
        var ft = new FormattedText(nr, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Arial"), FontPx(k), WedgeInk, dpi);
        var c = pr - d * (2.5 * k + ft.Width / 2) + n * (1.6 * k + ft.Height / 2);
        dc.DrawText(ft, new Point(c.X - ft.Width / 2, c.Y - ft.Height / 2));
    }

    /// <summary>
    /// Klin w kącie między torem (od <paramref name="pr"/> w kierunku <paramref name="d"/>) a odgałęzieniem <paramref name="branch"/>
    /// (punkty ekranu od PR) do <paramref name="endAlong"/> px wzdłuż toru. <paramref name="closedBranch"/> – zamknięte odgałęzienie
    /// (klin przy odgałęzieniu, odstęp przy torze), inaczej zamknięty tor na wprost. Zwraca normalną w stronę odgałęzienia.
    /// </summary>
    Vector DrawWedge(DrawingContext dc, Point pr, Vector d, IList<Point> branch, double endAlong, bool closedBranch, bool manual, double k)
    {
        var nn = new Vector(-d.Y, d.X);
        var last = branch.Count > 0 ? branch[^1] : pr;
        var n = (last - pr) * nn >= 0 ? nn : -nn;
        // punkty odgałęzienia w układzie (wzdłuż, w poprzek) do końca rozjazdu
        var pl = new List<(double A, double L)>();
        foreach (var q in branch)
        {
            double a = (q - pr) * d, l = (q - pr) * n;
            if (a > endAlong)
            {
                if (pl.Count > 0) { var (a0, l0) = pl[^1]; double u = (endAlong - a0) / Math.Max(1e-9, a - a0); pl.Add((endAlong, l0 + (l - l0) * u)); }
                break;
            }
            pl.Add((a, l));
        }
        if (pl.Count < 2) return n;
        double latEnd = pl[^1].L;
        if (latEnd < 2.5) return n;                                     // za mały, żeby było widać – sama kreska i numer
        double gap = Math.Min(0.5 * k, 0.3 * latEnd);
        // początek klina: miejsce, gdzie odgałęzienie odchodzi od toru o odstęp
        int i0 = pl.FindIndex(x => x.L >= gap); if (i0 < 1) i0 = 1;
        var (pa, pll) = pl[i0 - 1]; var (qa, qll) = pl[i0];
        double a0s = pa + (qa - pa) * ((gap - pll) / Math.Max(1e-9, qll - pll));
        // długość klina najwyżej 12 mm arkusza (jak na planach schematycznych), krawędź poprzeczna na końcu
        if (endAlong - a0s > 12 * k)
        {
            endAlong = a0s + 12 * k;
            int ie = pl.FindIndex(x => x.A >= endAlong);
            if (ie > 0) { var (ea, el) = pl[ie - 1]; var (fa, fl) = pl[ie]; pl = pl.Take(ie).ToList(); pl.Add((endAlong, el + (fl - el) * ((endAlong - ea) / Math.Max(1e-9, fa - ea)))); }
        }
        Point P(double a, double l) => pr + d * a + n * l;
        var poly = new List<Point>();
        if (closedBranch)
        {
            poly.Add(P(a0s, gap));
            for (int i = i0; i < pl.Count; i++) poly.Add(P(pl[i].A, pl[i].L));
            poly.Add(P(endAlong, gap));
        }
        else
        {
            poly.Add(P(a0s, 0));
            for (int i = i0; i < pl.Count; i++) poly.Add(P(pl[i].A, pl[i].L - gap));
            poly.Add(P(endAlong, 0));
        }
        var geo = new StreamGeometry();
        using (var c = geo.Open()) { c.BeginFigure(poly[0], true, true); foreach (var q in poly.Skip(1)) c.LineTo(q, true, false); }
        geo.Freeze();
        if (!manual) { dc.DrawGeometry(WedgeInk, null, geo); return n; }
        // nieuzależniona: klin biały z obwódką i kreskowaniem w poprzek toru
        dc.DrawGeometry(Brushes.White, WedgeOutline, geo);
        double lo = closedBranch ? gap : 0;
        for (int j = 1; j <= 5; j++)
        {
            double a = a0s + (endAlong - a0s) * j / 6;
            int i = pl.FindIndex(x => x.A >= a); if (i < 1) continue;
            var (xa, xl) = pl[i - 1]; var (ya, yl) = pl[i];
            double l = xl + (yl - xl) * ((a - xa) / Math.Max(1e-9, ya - xa)) - (closedBranch ? 0 : gap);
            dc.DrawLine(WedgeOutline, P(a, lo), P(a, l));
        }
        return n;
    }

    void DrawCrossing(DrawingContext dc, UtCrossing c, bool sel, double dpi)
    {
        if (Rozjazdy.CrossGeo(Doc.P, c) is not var (p, ends, slips, _)) return;
        var ps = ToScreen(p);
        if (sel) dc.DrawEllipse(RzSel, null, ps, 16, 16);
        var typ0 = Katalog.Find(c.Type);
        // łuki przejazdów – tylko w widoku Teren (kontrola geometrii); na schemacie Ig-10 ich nie rysuje
        if (!Schemat)
            foreach (var sl in slips)
            {
                var g = new StreamGeometry();
                using (var cx = g.Open()) { cx.BeginFigure(ToScreen(sl[0]), false, false); foreach (var q in sl.Skip(1)) cx.LineTo(ToScreen(q), true, true); }
                dc.DrawGeometry(null, SlipPen, g);
            }
        if (typ0?.Kind != RozjazdTyp.St)
        {
            // Ig-10 zał. 8 s. 21: kreska w punkcie matematycznym, kliny w obu kątach ostrych między torami (odcinek a = 8 mm od środka)
            double k = PxPerMm;
            Vector Dir(XY e) { var v = ToScreen(e) - ps; v.Normalize(); return v; }
            var dA = Dir(ends[1]); Vector n = default;
            foreach (var (along, other) in new[] { (ends[1], ends[3]), (ends[0], ends[2]) })
            {
                var d = Dir(along); var o = ps + Dir(other) * (40 * k);
                var nn = DrawWedge(dc, ps, d, new[] { ps, o }, 8 * k, true, false, k);
                if (along == ends[1]) n = nn;
            }
            var nA = new Vector(-dA.Y, dA.X);
            dc.DrawLine(StartPen, ps - nA * k, ps + nA * k);
            if (LabelsOn)
            {
                var ft = new FormattedText(c.Number, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Arial"), FontPx(k), WedgeInk, dpi);
                var tc = ps - n * (2.2 * k + ft.Height / 2);
                dc.DrawText(ft, new Point(tc.X - ft.Width / 2, tc.Y - ft.Height / 2));
            }
            return;
        }
        // skrzyżowanie: znak z bazy Ig-10 w punkcie matematycznym; strona – w którą stronę od toru A idzie tor B na końcu +x
        var va = ends[1] - p; var vb = ends[3] - p;
        int side = va.X * vb.Y - va.Y * vb.X >= 0 ? 1 : -1;
        string sid = typ0?.Kind switch { RozjazdTyp.St => "st", RozjazdTyp.Rkp => "rkp", _ => "rkpd_uzal_wprost" };
        SymbolPainter.Draw(dc, Symbole.Find(sid), ps, ToScreen(ends[1]) - ps, side, PxPerMm, c.Number, dpi);
        var kind = new FormattedText(typ0?.Kind ?? "?", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, UiFont, 10, Hint, dpi);
        dc.DrawText(kind, ps + new Vector(8 * PxPerMm, 2));
    }

    void DrawRzPreview(DrawingContext dc, double dpi)
    {
        if (rzFrom is not var (track, km) || hover is not XY h) return;
        if ((ToScreen(Geometry(Track(track)).At(km).P) - ToScreen(h)).Length < 8) return;
        var pl = RzFromDrag(track, km, ToScreen(h));
        var (dir, side, target) = (pl.Dir, pl.Side, pl.Target);
        var tmp = new UtProject { Tracks = Doc.P.Tracks.ToList() };
        string info;
        if (target != null)
        {
            var (t1, t2, link, err) = Rozjazdy.NewCrossover(tmp, track, km, dir, side, target, RzType, "", "");
            if (err != null) { Label(dc, err, ToScreen(h) + new Vector(12, 10), Red, dpi); return; }
            tmp.Tracks.Add(link); tmp.Turnouts.Add(t1); tmp.Turnouts.Add(t2); Rozjazdy.Sync(tmp);
            info = "łącznik";
        }
        else
        {
            var (t, br) = Rozjazdy.NewSingle(tmp, track, km, dir, side, RzType, "", pl.Offset, pl.EndAlong, NewR > 0 ? NewR : 300);
            tmp.Tracks.Add(br); tmp.Turnouts.Add(t); Rozjazdy.Sync(tmp);
            info = (side > 0 ? "prawy" : "lewy") + (pl.Offset > 0 ? $", tor równoległy w odstępie {pl.Offset.ToString("0.##", CultureInfo.GetCultureInfo("pl-PL"))} m (łuk R {(NewR > 0 ? NewR : 300):0})" : ", odgałęzienie do KR");
            if (Rozjazdy.Check(tmp, t) is var ch && ch.Count > 0) info += " – " + string.Join("; ", ch);
        }
        foreach (var t in tmp.Turnouts)
        {
            var g = Rozjazdy.Geometry(tmp, t); if (g == null) continue;
            var br = tmp.Tracks.First(x => x.Id == t.Branch);
            var sp = Trasa.Compute(br).Sample(0.5);
            if (sp.Count >= 2) dc.DrawGeometry(null, RzPreview, Line(sp, 0));
            dc.DrawLine(RzPreview, ToScreen(g.PR), ToScreen(g.KRMain));
        }
        Label(dc, $"{RzType} – {info}", ToScreen(h) + new Vector(12, 10), Red, dpi);
    }
}

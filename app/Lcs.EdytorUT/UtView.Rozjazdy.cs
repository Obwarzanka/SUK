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
    /// <summary>Rozjazd do wstawienia: tor, km PR, kierunek, strona, tor docelowy łącznika (albo null).</summary>
    public event Action<string, double, int, int, string> TurnoutPlace;
    /// <summary>Skrzyżowanie / rozjazd krzyżowy: klik w punkcie.</summary>
    public event Action<XY> CrossingPlace;
    public event Action<string, double> TurnoutMoved;

    public void SelectTurnout(string id)
    {
        SelectedTurnout = id; SelectedCrossing = null; SelectedTrack = null; SelectedVertex = -1;
        InvalidateVisual(); SelectionChanged?.Invoke();
    }
    public void SelectCrossing(string id)
    {
        SelectedCrossing = id; SelectedTurnout = null; SelectedTrack = null; SelectedVertex = -1;
        InvalidateVisual(); SelectionChanged?.Invoke();
    }

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

    /// <summary>Kierunek i strona z wektora przeciągnięcia względem toru.</summary>
    (int Dir, int Side) RzDirSide(string track, double s, XY to)
    {
        var r = Geometry(Track(track)); var (p, az) = r.At(s);
        var v = to - p; var d = Element.Dir(az);
        double along = v.X * d.X + v.Y * d.Y, across = d.X * v.Y - d.Y * v.X;   // across > 0 – w prawo od kierunku toru
        int dir = along >= 0 ? 1 : -1;
        int side = Math.Sign(across) * dir; if (side == 0) side = 1;
        return (dir, side);
    }

    void RzRelease(Point s)
    {
        var (track, km) = rzFrom.Value; rzFrom = null;
        var w = ToWorld(s);
        if ((ToScreen(Geometry(Track(track)).At(km).P) - s).Length < 8) { DraftInfo?.Invoke("Rozjazd: przeciągnij od miejsca początku rozjazdu w stronę odgałęzienia (na tor sąsiedni – łącznik)"); InvalidateVisual(); return; }
        var (dir, side) = RzDirSide(track, km, w);
        var target = HitTrack(s);
        TurnoutPlace?.Invoke(track, km, dir, side, target == track ? null : target);
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
            foreach (var q in new[] { g.PR, g.SR, g.PR + Element.Dir(g.Az) * (12 * mpp) })
            { double d = (ToScreen(q) - s).Length; if (d < bd) { bd = d; best = t.Id; } }
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

    /// <summary>Wskaż: wciśnięcie na zaznaczonym rozjeździe – przesuwanie wzdłuż toru.</summary>
    bool PressTurnout(Point s)
    {
        if (SelectedTurnout == null || HitTurnout(s) != SelectedTurnout) return false;
        var t = Doc.P.Turnouts.First(x => x.Id == SelectedTurnout);
        var r = Geometry(Track(t.Track));
        rzDrag = (t.Id, r.Nearest(ToWorld(s), 0.2).S - t.S, t.S);
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
        foreach (var t in Doc.P.Turnouts)
        {
            var tt = t;
            if (rzDrag is var (did, _, ds) && did == t.Id) tt = new UtTurnout { Type = t.Type, Track = t.Track, S = ds, Dir = t.Dir, Side = t.Side, Zas = t.Zas, Number = t.Number };
            var g = Rozjazdy.Geometry(Doc.P, tt); if (g == null) continue;
            DrawTurnout(dc, tt, g, t.Id == SelectedTurnout || (SelectedTurnout != null && t.Partner == SelectedTurnout), dpi, Rozjazdy.Check(Doc.P, t).Count > 0);
            if (!ReferenceEquals(tt, t)) dc.DrawLine(RzPreview, ToScreen(g.PR), ToScreen(g.KRDiv));
        }
        foreach (var c in Doc.P.Crossings) DrawCrossing(dc, c, c.Id == SelectedCrossing, dpi);
        if (tool == UtTool.Rozjazd) DrawRzPreview(dc, dpi);
    }

    /// <summary>Wymiar znaku w mm arkusza (skala rysunku) na piksele ekranu, nie mniej niż <paramref name="minPx"/>.</summary>
    double MmPx(double mm, double minPx) => Math.Max(mm / 1000 * (Doc?.P.DrawingScale ?? 1000) / mpp, minPx);

    /// <summary>Piksele na mm arkusza dla znaków Ig-10: skala rysunku, nie mniej niż 2,6 px/mm (klin 8 mm ≥ 21 px).</summary>
    public double PxPerMm => MmPx(1, 2.6);

    /// <summary>Znak rozjazdu z bazy symboli Ig-10 (Dane/symbole_ig10.json): zaczepiony w PR, wzdłuż toru w stronę KR, po stronie odgałęzienia.</summary>
    public static string SymbolFor(UtTurnout t) => t.Manual ? "rz_nieuzal" : t.Zas == "-" ? "rz_uzal_zwrot" : "rz_uzal_wprost";

    void DrawTurnout(DrawingContext dc, UtTurnout t, TurnoutGeo g, bool sel, double dpi, bool err)
    {
        var pr = ToScreen(g.PR);
        if (pr.X < -80 || pr.Y < -80 || pr.X > ActualWidth + 80 || pr.Y > ActualHeight + 80) return;
        double k = PxPerMm;
        if (sel) dc.DrawEllipse(RzSel, null, ToScreen(g.PR + Element.Dir(g.Az) * (4 * k * mpp)), 6 * k, 6 * k);
        var dir = ToScreen(g.PR + Element.Dir(g.Az)) - pr;
        SymbolPainter.Draw(dc, Symbole.Find(SymbolFor(t)), pr, dir, t.Side, k, t.Number, dpi);
        if (err) dc.DrawEllipse(null, ErrPen, ToScreen(g.SR), 10, 10);
    }

    void DrawCrossing(DrawingContext dc, UtCrossing c, bool sel, double dpi)
    {
        if (Rozjazdy.CrossGeo(Doc.P, c) is not var (p, ends, slips, _)) return;
        var ps = ToScreen(p);
        if (sel) dc.DrawEllipse(RzSel, null, ps, 16, 16);
        foreach (var sl in slips)
        {
            var g = new StreamGeometry();
            using (var cx = g.Open()) { cx.BeginFigure(ToScreen(sl[0]), false, false); foreach (var q in sl.Skip(1)) cx.LineTo(ToScreen(q), true, true); }
            dc.DrawGeometry(null, SlipPen, g);
        }
        // znak z bazy Ig-10 w punkcie matematycznym; strona – w którą stronę od toru A idzie tor B na końcu +x
        var typ0 = Katalog.Find(c.Type);
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
        var (dir, side) = RzDirSide(track, km, h);
        var target = HitTrack(ToScreen(h));
        var tmp = new UtProject { Tracks = Doc.P.Tracks.ToList() };
        string info;
        if (target != null && target != track)
        {
            var (t1, t2, link, err) = Rozjazdy.NewCrossover(tmp, track, km, dir, side, target, RzType, "", "");
            if (err != null) { Label(dc, err, ToScreen(h) + new Vector(12, 10), Red, dpi); return; }
            tmp.Tracks.Add(link); tmp.Turnouts.Add(t1); tmp.Turnouts.Add(t2); Rozjazdy.Sync(tmp);
            info = "łącznik";
        }
        else
        {
            var (t, br) = Rozjazdy.NewSingle(tmp, track, km, dir, side, RzType, "");
            tmp.Tracks.Add(br); tmp.Turnouts.Add(t); Rozjazdy.Sync(tmp);
            info = Rozjazdy.Check(tmp, t) is var ch && ch.Count > 0 ? string.Join("; ", ch) : $"{(side > 0 ? "prawy" : "lewy")}";
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

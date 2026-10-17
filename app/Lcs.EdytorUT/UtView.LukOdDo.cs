using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Narzędzie „Łuk od–do” (K): klik na torze w miejscu początku łuku, klik w miejscu końca (na prostej za załamaniem) – wierzchołek
/// w przecięciu tych prostych, wierzchołki pośrednie znikają, promień tak, żeby łuk zaczynał się i kończył w zaznaczonych miejscach
/// (<see cref="Budowa.ArcBetween"/>). Do odrysowania łuków ze zdjęcia: widać, gdzie kończą się proste. PPM / Esc – od nowa.
/// </summary>
public partial class UtView
{
    (string Track, XY P)? arcFrom;
    /// <summary>Łuk od–do gotowy: tor, nowe wierzchołki, R [m], uwaga.</summary>
    public event Action<string, List<UtVertex>, double, string> ArcBetweenDone;

    /// <summary>Punkt na osi toru (na boku wieloboku) najbliżej kursora, gdy kursor jest przy torze.</summary>
    (string Track, XY P)? ArcPoint(Point s, string only = null)
    {
        var id = only ?? HitTrack(s); if (id == null || Track(id) is not UtTrack t) return null;
        var (sg, q, _) = Budowa.NearestSegment(t.Vertices, ToWorld(s));
        if (sg < 0 || (ToScreen(q) - s).Length > 12) return null;
        return (id, q);
    }

    /// <summary>Wynik dla drugiego punktu (z kontrolą wierzchołków ustawianych przez rozjazd).</summary>
    (List<UtVertex> V, double R, string Note, string Error) ArcCandidate(string id, XY p1, XY p2)
    {
        var t = Track(id);
        int keep = 0; for (int i = 0; i < t.Vertices.Count; i++) if (Locked(id, i) || SkewTurnout(id, i) != null) keep = i + 1;
        var res = Budowa.ArcBetween(t.Vertices, p1, p2, KAt(p1), keep);
        // wierzchołki na końcu ustawiane przez drugi rozjazd łącznika – nie mogą zniknąć
        if (res.Error == null)
            for (int i = 0; i < t.Vertices.Count; i++)
                if (Locked(id, i) && !res.V.Any(q => XY.Dist(q.P, t.Vertices[i].P) < 1e-6)) return (null, 0, null, "tu wierzchołki ustawia rozjazd – łuku nie można zmienić");
        return res;
    }

    void ArcClick(Point s)
    {
        if (arcFrom == null)
        {
            arcFrom = ArcPoint(s);
            if (arcFrom is var (aid, _) && IsAlign(aid)) { arcFrom = null; DraftInfo?.Invoke(AlignInfo); return; }
            DraftInfo?.Invoke(arcFrom != null ? "Łuk od–do: kliknij koniec łuku (na prostej za załamaniem)" : "Łuk od–do: kliknij na torze w miejscu początku łuku");
            InvalidateVisual(); return;
        }
        var (id, p1) = arcFrom.Value;
        if (ArcPoint(s, id) is not var (_, p2)) { DraftInfo?.Invoke("Łuk od–do: koniec łuku zaznacz na tym samym torze"); return; }
        var r = ArcCandidate(id, p1, p2);
        if (r.Error != null) { DraftInfo?.Invoke("Łuk od–do: " + r.Error); return; }
        arcFrom = null; InvalidateVisual();
        ArcBetweenDone?.Invoke(id, r.V, r.R, r.Note);
    }

    public bool ArcKey(Key k)
    {
        if (tool != UtTool.LukOdDo || arcFrom == null) return false;
        if (k is Key.Escape or Key.Back) { arcFrom = null; InvalidateVisual(); return true; }
        return false;
    }

    void DrawArcBetween(DrawingContext dc, double dpi)
    {
        if (tool != UtTool.LukOdDo || hover is not XY h) return;
        var hs = ToScreen(h);
        if (arcFrom is not var (id, p1))
        {
            if (ArcPoint(hs) is var (_, q)) { var qs = ToScreen(q); dc.DrawEllipse(null, SnapPen, qs, 6, 6); }
            Label(dc, "kliknij początek łuku na torze", hs + new Vector(12, 8), DraftInk, dpi);
            return;
        }
        var ps = ToScreen(p1); dc.DrawEllipse(DraftInk, null, ps, 4, 4);
        if (ArcPoint(hs, id) is not var (_, p2)) { Label(dc, "koniec łuku – na tym samym torze", hs + new Vector(12, 8), Red, dpi); return; }
        dc.DrawEllipse(null, SnapPen, ToScreen(p2), 6, 6);
        var r = ArcCandidate(id, p1, p2);
        if (r.Error != null) { Label(dc, r.Error, hs + new Vector(12, 8), Red, dpi); return; }
        var res = Trasa.Compute(r.V, KAt(p1));
        dc.DrawGeometry(null, new Pen(DraftInk, 2) { DashStyle = new DashStyle(new double[] { 4, 2 }, 0) }, Line(res.Sample(0.5), 0));
        Label(dc, $"łuk R {r.R:0.#} m" + (r.Note != null ? " – " + r.Note : "") + " – klik", hs + new Vector(12, 8), DraftInk, dpi);
    }
}

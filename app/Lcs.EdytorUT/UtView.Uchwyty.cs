using System.Windows;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Uchwyty na osi toru (wielobok, Wskaż, zaznaczony tor): kwadracik w środku prostej – przesunięcie prostej równolegle (sąsiednie proste
/// zachowują kierunek, <see cref="Budowa.ShiftStraight"/>); romb na początku / końcu łuku – przesunięcie wzdłuż prostej, R dobiera się
/// tak, żeby łuk zaczynał się / kończył w tym miejscu (styczna T, <see cref="Budowa.RadiusFor"/>). Wierzchołka nie trzeba widzieć.
/// </summary>
public partial class UtView
{
    (string Track, int Kind, int Idx)? hDrag;     // Kind 0 – prosta, 1 – początek łuku, 2 – koniec łuku
    List<UtVertex> hPreview; bool hMoved;
    /// <summary>Wierzchołki toru zmienione uchwytem: tor, nowe wierzchołki, nazwa do cofania, opis.</summary>
    public event Action<string, List<UtVertex>, string, string> VerticesChanged;

    /// <summary>Uchwyty prostych: element prosty między wierzchołkami Seg−1 i Seg, środek, kierunek.</summary>
    List<(int Seg, XY Mid, double Az)> StraightHandles(UtTrack t)
    {
        var res = new List<(int, XY, double)>(); if (t.Align != null) return res;
        var r = Geometry(t);
        foreach (var e in r.Elements.Where(e => e.Kind == ElKind.Prosta))
        {
            int seg = e.Vertex; if (seg < 1 || seg >= t.Vertices.Count) continue;
            if (Locked(t.Id, seg) || Locked(t.Id, seg - 1) || SkewTurnout(t.Id, seg) != null) continue;
            if ((ToScreen(e.At(e.Len).P) - ToScreen(e.Start)).Length < 50) continue;
            res.Add((seg, e.At(e.Len / 2).P, e.Az0));
        }
        return res;
    }

    /// <summary>Uchwyty początku (Kind 1) i końca (Kind 2) łuków: punkt i kierunek stycznej.</summary>
    List<(int Vi, int Kind, XY P, double Az)> ArcEndHandles(UtTrack t)
    {
        var res = new List<(int, int, XY, double)>(); if (t.Align != null) return res;
        var r = Geometry(t);
        for (int i = 1; i < t.Vertices.Count - 1; i++)
        {
            if (t.Vertices[i].R <= 0 || Locked(t.Id, i)) continue;
            var cur = r.Elements.Where(e => e.Vertex == i && e.Kind != ElKind.Prosta).ToList(); if (cur.Count == 0) continue;
            res.Add((i, 1, cur[0].Start, cur[0].Az0));
            var (pe, ae) = cur[^1].At(cur[^1].Len); res.Add((i, 2, pe, ae));
        }
        return res;
    }

    bool PressHandles(UtTrack t, Point s)
    {
        if (tool != UtTool.Wskaz) return false;
        foreach (var (vi, kind, p, _) in ArcEndHandles(t))
            if ((ToScreen(p) - s).Length <= HandlePx)
            {
                if (t.Vertices[vi].LockR) { DraftInfo?.Invoke($"W{vi + 1}: promień zablokowany – odblokuj go w panelu toru"); return true; }
                hDrag = (t.Id, kind, vi); hPreview = null; hMoved = false; Select(t.Id, vi); return true;
            }
        foreach (var (seg, m, _) in StraightHandles(t))
            if ((ToScreen(m) - s).Length <= HandlePx) { hDrag = (t.Id, 0, seg); hPreview = null; hMoved = false; Select(t.Id); return true; }
        return false;
    }

    void DragHandle(Point s)
    {
        if (hDrag is not var (id, kind, idx) || Track(id) is not UtTrack t) return;
        if (downAt is Point d && (s - d).Length > ClickTol) hMoved = true;
        if (!hMoved) return;
        var w = ToWorld(s); double k = Trasa.KFor(t); var v = t.Vertices;
        if (kind == 0)
        {
            var a = v[idx - 1].P; var b = v[idx].P; var n = Element.Right(Trasa.Az(a, b));
            double off = ((w - a).X * n.X + (w - a).Y * n.Y) / k; off = Math.Round(off * 100) / 100;
            hPreview = Budowa.ShiftStraight(v, idx, k, off);
            DraftInfo?.Invoke($"Prosta W{idx}–W{idx + 1} przesunięta równolegle o {off:0.00} m {(off >= 0 ? "w prawo" : "w lewo")} (sąsiednie proste – ten sam kierunek)");
        }
        else
        {
            // styczna = odległość od wierzchołka do kursora wzdłuż prostej
            var vp = v[idx].P; var dir = kind == 1 ? Element.Dir(Trasa.Az(v[idx - 1].P, vp)) : Element.Dir(Trasa.Az(vp, v[idx + 1].P));
            double T = (kind == 1 ? -((w - vp).X * dir.X + (w - vp).Y * dir.Y) : (w - vp).X * dir.X + (w - vp).Y * dir.Y) / k;
            var nv = T > 0.5 ? Budowa.RadiusFor(v, idx, k, rr => (kind == 1 ? rr.Tangent : rr.Tangent2).TryGetValue(idx, out var q) ? q / rr.K : double.NaN, T) : null;
            if (nv != null) { hPreview = nv; DraftInfo?.Invoke($"Łuk W{idx + 1}: {(kind == 1 ? "początek" : "koniec")} {T:0.0} m od wierzchołka → R {nv[idx].R:0} m"); }
            else DraftInfo?.Invoke($"Łuk W{idx + 1}: tak się nie da (łuk się nie mieści)");
        }
        InvalidateVisual();
    }

    void EndHandleDrag()
    {
        var (id, kind, idx) = hDrag.Value; hDrag = null;
        if (hMoved && hPreview != null)
            VerticesChanged?.Invoke(id, hPreview, kind == 0 ? "Przesunięcie prostej" : "Początek / koniec łuku",
                kind == 0 ? $"Prosta W{idx}–W{idx + 1} przesunięta równolegle" : $"Łuk W{idx + 1}: R {hPreview[idx].R:0} m");
        hPreview = null; InvalidateVisual();
    }

    void DrawHandles(DrawingContext dc, UtTrack t)
    {
        if (tool != UtTool.Wskaz || t.Align != null) return;
        var fill = new SolidColorBrush(Color.FromArgb(0xe0, 0xff, 0xff, 0xff)); var pen = new Pen(new SolidColorBrush(Color.FromRgb(0x30, 0x70, 0xc0)), 1.4);
        foreach (var (_, m, _) in StraightHandles(t)) { var q = ToScreen(m); dc.DrawRectangle(fill, pen, new Rect(q.X - 4, q.Y - 4, 8, 8)); }
        foreach (var (_, _, p, _) in ArcEndHandles(t))
        {
            var q = ToScreen(p); var g = new StreamGeometry();
            using (var c = g.Open()) { c.BeginFigure(new Point(q.X, q.Y - 5), true, true); c.LineTo(new Point(q.X + 5, q.Y), true, false); c.LineTo(new Point(q.X, q.Y + 5), true, false); c.LineTo(new Point(q.X - 5, q.Y), true, false); }
            g.Freeze(); dc.DrawGeometry(fill, pen, g);
        }
    }
}

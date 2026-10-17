using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Narzędzie „Łuk z punktów” (J): klik na łuku toru (albo przy załamaniu), potem dowolnie wiele kliknięć na osi prawdziwego łuku
/// na zdjęciu – promień (Tab: także krzywe przejściowe L) dobiera się metodą najmniejszych kwadratów, łuk styczny do prostych
/// toru (<see cref="Budowa.FitArc"/>). Przy każdym punkcie jego odchyłka; Backspace – usuń ostatni punkt, Enter albo PPM –
/// zastosuj, Esc – od nowa. Do odrysowania łuków ze zdjęcia dokładniej niż jednym kliknięciem.
/// </summary>
public partial class UtView
{
    (string Track, int Vi)? fitArc;
    public readonly List<XY> FitPts = new();
    /// <summary>Dopasowanie także krzywych przejściowych L (Tab).</summary>
    public bool FitWithL { get; private set; }
    /// <summary>Wierzchołek (kierunki prostych) też dopasowywany do punktów – domyślnie; W – stałe proste.</summary>
    public bool FitFreeVertex { get; private set; } = true;
    public ArcFit FitResult { get; private set; }
    /// <summary>Łuk z punktów gotowy: tor, wierzchołek, R, L, opis.</summary>
    public event Action<string, int, double, double, XY?, string> ArcFitDone;

    /// <summary>Wierzchołek łuku pod kursorem: element osi najbliżej kliknięcia (łuk / przejściowa – jego wierzchołek, prosta – bliższy wierzchołek).</summary>
    (string Track, int Vi)? FitVertexAt(Point s)
    {
        var id = HitTrack(s); if (id == null || Track(id) is not UtTrack t || t.Vertices.Count < 3) return null;
        var r = Geometry(t); var w = ToWorld(s);
        var (sNear, _) = r.Nearest(w, 0.5); double acc = 0; int vi = -1;
        foreach (var e in r.Elements)
        {
            if (sNear * r.K <= acc + e.Len + 1e-9) { if (e.Kind != ElKind.Prosta) vi = e.Vertex; break; }
            acc += e.Len;
        }
        if (vi <= 0 || vi >= t.Vertices.Count - 1)
        {
            double best = double.MaxValue;
            for (int i = 1; i < t.Vertices.Count - 1; i++) { double d = XY.Dist(t.Vertices[i].P, w); if (d < best) { best = d; vi = i; } }
        }
        return (id, vi);
    }

    void FitClick(Point s)
    {
        if (fitArc == null)
        {
            if (FitVertexAt(s) is not var (id, vi)) { DraftInfo?.Invoke("Łuk z punktów: kliknij łuk toru (albo przy załamaniu)"); return; }
            if (Locked(id, vi) || SkewTurnout(id, vi) != null) { DraftInfo?.Invoke("Łuk z punktów: ten łuk ustawia rozjazd – wybierz inny"); return; }
            fitArc = (id, vi); FitPts.Clear(); FitResult = null; Select(id, vi);
            DraftInfo?.Invoke($"Łuk z punktów (W{vi + 1}): klikaj punkty na osi łuku na zdjęciu – im więcej i dalej od siebie, tym dokładniej; Tab – także krzywe przejściowe; Enter – zastosuj");
            InvalidateVisual(); return;
        }
        FitPts.Add(ToWorld(s)); Refit();
    }

    void Refit()
    {
        if (fitArc is not var (id, vi) || Track(id) is not UtTrack t) { FitResult = null; return; }
        FitResult = FitPts.Count == 0 ? null : FitFreeVertex ? Budowa.FitArcFree(t.Vertices, vi, FitPts, Trasa.KFor(t), FitWithL) : Budowa.FitArc(t.Vertices, vi, FitPts, Trasa.KFor(t), FitWithL);
        DraftInfo?.Invoke(FitInfo());
        InvalidateVisual();
    }

    string FitInfo()
    {
        if (FitResult == null) return "Łuk z punktów: kliknij punkty na osi łuku";
        if (FitResult.Error != null) return "Łuk z punktów: " + FitResult.Error;
        string vx = FitFreeVertex ? (FitResult.Vertex is XY nv && fitArc is var (fid, fvi) && Track(fid) is UtTrack ft ? $", wierzchołek przesunięty o {XY.Dist(nv, ft.Vertices[fvi].P) / Trasa.KFor(ft):0.00} m" : FitPts.Count < 4 ? " (wierzchołek – od 4 punktów)" : "") : " (proste stałe – W)";
        return $"Łuk z punktów: R {FitResult.R:0} m" + (FitWithL || FitResult.L > 0 ? $", L {FitResult.L:0} m" : "") + vx +
               $" – {FitPts.Count} pkt, odchyłka średnia {FitResult.Rms * 100:0.#} cm, największa {FitResult.Max * 100:0.#} cm · Enter – zastosuj";
    }

    /// <summary>Zastosowanie (Enter / PPM); false – nie ma czego.</summary>
    public bool FitApply()
    {
        if (fitArc is not var (id, vi) || FitResult == null || FitResult.Error != null) return false;
        var r = FitResult; string info = FitInfo().Replace(" · Enter – zastosuj", "");
        fitArc = null; FitPts.Clear(); FitResult = null; InvalidateVisual();
        ArcFitDone?.Invoke(id, vi, r.R, r.L, r.Vertex, info);
        return true;
    }

    public bool FitKey(Key k)
    {
        if (tool != UtTool.LukPunkty) return false;
        if (k == Key.Tab) { FitWithL = !FitWithL; if (fitArc != null) Refit(); else DraftInfo?.Invoke(FitWithL ? "Łuk z punktów: dopasowanie R i krzywych przejściowych L" : "Łuk z punktów: dopasowanie samego R"); return true; }
        if (k == Key.W) { FitFreeVertex = !FitFreeVertex; if (fitArc != null) Refit(); else DraftInfo?.Invoke(FitFreeVertex ? "Łuk z punktów: wierzchołek (proste) też dopasowywany" : "Łuk z punktów: proste stałe, tylko R / L"); return true; }
        if (fitArc == null) return false;
        if (k == Key.Escape) { fitArc = null; FitPts.Clear(); FitResult = null; DraftInfo?.Invoke("Łuk z punktów: kliknij łuk toru"); InvalidateVisual(); return true; }
        if (k == Key.Back) { if (FitPts.Count > 0) { FitPts.RemoveAt(FitPts.Count - 1); Refit(); } return true; }
        if (k == Key.Enter) { if (!FitApply()) DraftInfo?.Invoke("Łuk z punktów: najpierw kliknij punkty na osi łuku"); return true; }
        return false;
    }

    void DrawArcFit(DrawingContext dc, double dpi)
    {
        if (tool != UtTool.LukPunkty) return;
        if (fitArc is not var (id, vi) || Track(id) is not UtTrack t)
        {
            if (hover is XY h && FitVertexAt(ToScreen(h)) is var (hid, hvi) && Track(hid) is UtTrack ht)
            { dc.DrawEllipse(null, SnapPen, ToScreen(ht.Vertices[hvi].P), 7, 7); Label(dc, $"łuk W{hvi + 1} – kliknij", ToScreen(h) + new Vector(12, 8), DraftInk, dpi); }
            return;
        }
        dc.DrawEllipse(null, SnapPen, ToScreen(t.Vertices[vi].P), 7, 7);
        var pen = new Pen(DraftInk, 2) { DashStyle = new DashStyle(new double[] { 4, 2 }, 0) };
        if (FitResult is { Error: null } fr)
        {
            var c = t.Vertices.Select(x => x.Clone()).ToList(); c[vi].R = fr.R; c[vi].L = fr.L;
            if (fr.Vertex is XY nv) { c[vi].X = nv.X; c[vi].Y = nv.Y; dc.DrawLine(new Pen(DraftInk, 1) { DashStyle = DashStyles.Dot }, ToScreen(t.Vertices[vi].P), ToScreen(nv)); }
            dc.DrawGeometry(null, pen, Line(Trasa.Compute(c, Trasa.KFor(t)).Sample(0.5), 0));
        }
        var red = new Pen(Red, 1.6);
        for (int i = 0; i < FitPts.Count; i++)
        {
            var q = ToScreen(FitPts[i]);
            dc.DrawLine(red, q + new Vector(-5, -5), q + new Vector(5, 5)); dc.DrawLine(red, q + new Vector(-5, 5), q + new Vector(5, -5));
            if (FitResult is { Error: null } f2 && i < f2.Dev.Count) Label(dc, $"{f2.Dev[i] * 100:0.#} cm", q + new Vector(7, -16), DraftInk, dpi);
        }
        if (hover is XY hv) Label(dc, FitInfo().Replace("Łuk z punktów: ", ""), ToScreen(hv) + new Vector(12, 8), FitResult?.Error != null ? Red : DraftInk, dpi);
    }
}

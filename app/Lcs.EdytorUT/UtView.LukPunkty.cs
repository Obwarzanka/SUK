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
/// Punkty szybciej (§ 5h): przeciągnięcie – malowanie po osi toru (co 2 m), Shift + klik przy linii OSM dwa razy – węzły OSM z odcinka
/// linii między kliknięciami, G – punkty śladu (narzędzie Ślad) blisko toru, O – węzły OSM do 8 m od toru. Punkty odstające (pomyłka,
/// węzeł OSM przy rozjeździe) są pomijane (<see cref="Slad.Odstajace"/>, X – wyłącz / włącz).
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
    public event Action<string, int, ArcFit, string> ArcFitDone;
    /// <summary>Wierzchołki łuku złożonego (łuki bez prostej między sobą), do którego należy wybrany łuk; jeden – zwykły łuk.</summary>
    public List<int> FitGroup { get; private set; } = new();
    /// <summary>Tor z elementów: dopasowana trasa (podgląd), zastosowanie – <see cref="AlignFitDone"/>.</summary>
    public UtAlign FitAlign { get; private set; }
    public event Action<string, UtAlign, string> AlignFitDone;
    /// <summary>Pomijanie punktów odstających (domyślnie; X – przełącz).</summary>
    public bool FitRobust { get; private set; } = true;
    /// <summary>Indeksy pominiętych punktów (odstających).</summary>
    public HashSet<int> FitOut { get; private set; } = new();
    /// <summary>Odchyłki wszystkich punktów (także pominiętych) [m w terenie].</summary>
    public List<double> FitDev { get; private set; } = new();
    (XY P, long A, long B)? fitNetFrom;

    /// <summary>Wierzchołek łuku pod kursorem: element osi najbliżej kliknięcia (łuk / przejściowa – jego wierzchołek, prosta – bliższy wierzchołek).</summary>
    (string Track, int Vi)? FitVertexAt(Point s)
    {
        var id = HitTrack(s); if (id == null || Track(id) is not UtTrack t) return null;
        if (t.Align != null) return (id, -1);                 // tor z elementów – dopasowanie elementów
        if (t.Vertices.Count < 3) return null;
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
            if (vi < 0)
            {
                fitArc = (id, -1); FitPts.Clear(); FitResult = null; FitAlign = null; Select(id); FitGroup = new();
                DraftInfo?.Invoke("Łuk z punktów (tor z elementów): klikaj punkty na osi toru na zdjęciu – dopasują się długości i promienie elementów w tym miejscu; Enter – zastosuj");
                InvalidateVisual(); return;
            }
            if (Locked(id, vi) || SkewTurnout(id, vi) != null) { DraftInfo?.Invoke("Łuk z punktów: ten łuk ustawia rozjazd – wybierz inny"); return; }
            fitArc = (id, vi); FitPts.Clear(); FitResult = null; Select(id, vi);
            FitGroup = Budowa.ArcGroup(Track(id).Vertices, vi, Trasa.KFor(Track(id)));
            DraftInfo?.Invoke($"Łuk z punktów (W{vi + 1}): klikaj punkty na osi łuku na zdjęciu – im więcej i dalej od siebie, tym dokładniej; Tab – także krzywe przejściowe; Enter – zastosuj");
            InvalidateVisual(); return;
        }
        if (Mods.HasFlag(ModifierKeys.Shift) && NetAt(s) is var (q, na, nb))
        {
            if (fitNetFrom is not var (fq, fa, fb)) { fitNetFrom = (q, na, nb); DraftInfo?.Invoke("Łuk z punktów: Shift + klik przy linii OSM w miejscu końca odcinka – dodadzą się węzły OSM z odcinka"); InvalidateVisual(); return; }
            fitNetFrom = null;
            var path = Osm.Path((fq, fa, fb), (q, na, nb));
            if (path == null) { DraftInfo?.Invoke("Łuk z punktów: te miejsca nie leżą na połączonych liniach OSM"); return; }
            int before = FitPts.Count;
            FitPts.AddRange(path.Where(p => FitPts.All(x => XY.Dist(x, p) > 0.5)));
            Refit(); DraftInfo?.Invoke($"Łuk z punktów: dodano {FitPts.Count - before} punktów z linii OSM ({OsmPunkty.Attribution}) – " + FitInfo().Replace("Łuk z punktów: ", ""));
            return;
        }
        FitPts.Add(ToWorld(s)); Refit();
    }

    void Refit()
    {
        FitOut = new(); FitDev = new();
        if (fitArc == null || Track(fitArc.Value.Track) is not UtTrack) { FitResult = null; return; }
        FitOn(FitPts);
        // punkty odstające: pominięte, dopasowanie jeszcze raz (do 3 razy, aż zbiór się nie zmienia)
        if (FitRobust && FitPts.Count >= 6)
            for (int it = 0; it < 3 && FitResult is { Error: null }; it++)
            {
                var g = FitGeometry(); if (g == null) break;
                var outl = Slad.Odstajace(FitPts.Select(q => Budowa.Dist(g, q) / g.K).ToList());
                if (outl.SetEquals(FitOut)) break;
                var keep = FitPts.Where((q, i) => !outl.Contains(i)).ToList();
                var prev = (FitResult, FitAlign, FitOut);
                FitOut = outl; FitOn(keep);
                if (FitResult?.Error != null) { (FitResult, FitAlign, FitOut) = prev; break; }
            }
        if (FitGeometry() is TrasaResult gg) FitDev = FitPts.Select(q => Budowa.Dist(gg, q) / gg.K).ToList();
        DraftInfo?.Invoke(FitInfo());
        InvalidateVisual();
    }

    /// <summary>Dopasowanie do podanych punktów (bez pominiętych).</summary>
    void FitOn(List<XY> pts)
    {
        var (id, vi) = fitArc.Value; var t = Track(id);
        var kk = Trasa.KFor(t);
        if (vi < 0 && t.Align != null)
        {
            var (fa, rms, max, dev, err) = pts.Count == 0 ? (null, 0, 0, null, null) : TrasaElem.Fit(t.Align, kk, pts);
            FitAlign = fa; FitResult = pts.Count == 0 ? null : new ArcFit(0, 0, rms, max, dev ?? new(), err);
            return;
        }
        FitResult = pts.Count == 0 ? null : FitGroup.Count > 1 ? Budowa.FitArcGroup(t.Vertices, FitGroup, pts, kk, FitWithL, FitFreeVertex)
            : FitFreeVertex ? Budowa.FitArcFree(t.Vertices, vi, pts, kk, FitWithL) : Budowa.FitArc(t.Vertices, vi, pts, kk, FitWithL);
    }

    /// <summary>Oś toru po dopasowaniu (podgląd), null – brak wyniku.</summary>
    TrasaResult FitGeometry()
    {
        if (fitArc is not var (id, vi) || Track(id) is not UtTrack t || FitResult is not { Error: null } fr) return null;
        if (vi < 0) return FitAlign != null ? TrasaElem.Compute(FitAlign, Trasa.KFor(t)) : null;
        var c = t.Vertices.Select(x => x.Clone()).ToList(); c[vi].R = fr.R; c[vi].L = fr.L;
        if (fr.Vertex is XY nv) { c[vi].X = nv.X; c[vi].Y = nv.Y; }
        if (fr.Parts != null) foreach (var (pi, pp, pr, pl) in fr.Parts) { c[pi].X = pp.X; c[pi].Y = pp.Y; c[pi].R = pr; c[pi].L = pl; }
        return Trasa.Compute(c, Trasa.KFor(t));
    }

    string FitInfo()
    {
        if (FitResult == null) return "Łuk z punktów: kliknij punkty na osi łuku";
        if (FitResult.Error != null) return "Łuk z punktów: " + FitResult.Error;
        return FitInfoCore().Replace(" · Enter – zastosuj", (FitOut.Count > 0 ? $", pominięte odstające: {FitOut.Count} (X – bez pomijania)" : "") + " · Enter – zastosuj");
    }

    string FitInfoCore()
    {
        if (FitAlign != null && fitArc is (_, -1))
            return $"Łuk z punktów (tor z elementów): {string.Join(" | ", FitAlign.Els.Where(e => !e.Prosta).Select(e => e.Luk ? $"R {Math.Abs(e.R0):0}" : $"L {e.Len:0}"))} – {FitPts.Count} pkt, odchyłka średnia {FitResult.Rms * 100:0.#} cm, największa {FitResult.Max * 100:0.#} cm · Enter – zastosuj";
        string vx = FitFreeVertex ? (FitResult.Vertex is XY nv && fitArc is var (fid, fvi) && Track(fid) is UtTrack ft ? $", wierzchołek przesunięty o {XY.Dist(nv, ft.Vertices[fvi].P) / Trasa.KFor(ft):0.00} m" : FitPts.Count < 4 ? " (wierzchołek – od 4 punktów)" : "") : " (proste stałe – W)";
        if (FitResult.Parts != null)
            return $"Łuk złożony z punktów ({FitResult.Parts.Count} łuki): R {string.Join(" / ", FitResult.Parts.Select(q => q.R.ToString("0")))} m" +
                   (FitWithL ? $", L {string.Join(" / ", FitResult.Parts.Select(q => q.L.ToString("0")))} m" : "") + (FitFreeVertex ? "" : " (proste stałe – W)") +
                   $" – {FitPts.Count} pkt, odchyłka średnia {FitResult.Rms * 100:0.#} cm, największa {FitResult.Max * 100:0.#} cm · Enter – zastosuj";
        return $"Łuk z punktów: R {FitResult.R:0} m" + (FitWithL || FitResult.L > 0 ? $", L {FitResult.L:0} m" : "") + vx +
               $" – {FitPts.Count} pkt, odchyłka średnia {FitResult.Rms * 100:0.#} cm, największa {FitResult.Max * 100:0.#} cm · Enter – zastosuj";
    }

    /// <summary>Zastosowanie (Enter / PPM); false – nie ma czego.</summary>
    public bool FitApply()
    {
        if (fitArc is not var (id, vi) || FitResult == null || FitResult.Error != null) return false;
        var r = FitResult; string info = FitInfo().Replace(" · Enter – zastosuj", ""); var fa = FitAlign;
        fitArc = null; FitPts.Clear(); FitResult = null; FitAlign = null; InvalidateVisual();
        if (vi < 0) { if (fa == null) return false; AlignFitDone?.Invoke(id, fa, info); return true; }
        ArcFitDone?.Invoke(id, vi, r, info);
        return true;
    }

    public bool FitKey(Key k)
    {
        if (tool != UtTool.LukPunkty) return false;
        if (k == Key.Tab) { FitWithL = !FitWithL; if (fitArc != null) Refit(); else DraftInfo?.Invoke(FitWithL ? "Łuk z punktów: dopasowanie R i krzywych przejściowych L" : "Łuk z punktów: dopasowanie samego R"); return true; }
        if (k == Key.W) { FitFreeVertex = !FitFreeVertex; if (fitArc != null) Refit(); else DraftInfo?.Invoke(FitFreeVertex ? "Łuk z punktów: wierzchołek (proste) też dopasowywany" : "Łuk z punktów: proste stałe, tylko R / L"); return true; }
        if (fitArc == null) return false;
        if (k == Key.Escape) { fitArc = null; fitNetFrom = null; FitPts.Clear(); FitResult = null; FitOut = new(); FitDev = new(); DraftInfo?.Invoke("Łuk z punktów: kliknij łuk toru"); InvalidateVisual(); return true; }
        if (k == Key.Back) { if (FitPts.Count > 0) { FitPts.RemoveAt(FitPts.Count - 1); Refit(); } return true; }
        if (k == Key.O) { _ = AddOsmPoints(); return true; }
        if (k == Key.G) { AddSladPoints(); return true; }
        if (k == Key.X) { FitRobust = !FitRobust; Refit(); DraftInfo?.Invoke((FitRobust ? "Łuk z punktów: punkty odstające pomijane – " : "Łuk z punktów: bez pomijania punktów odstających – ") + FitInfo().Replace("Łuk z punktów: ", "")); return true; }
        if (k == Key.Enter) { if (!FitApply()) DraftInfo?.Invoke("Łuk z punktów: najpierw kliknij punkty na osi łuku"); return true; }
        return false;
    }

    /// <summary>Punkty z OpenStreetMap (osie torów w widoku, do 8 m od wybranego toru) dodane do punktów dopasowania (klawisz O).</summary>
    public async Task<int> AddOsmPoints()
    {
        if (fitArc is not var (id, _) || Track(id) is not UtTrack t) return 0;
        DraftInfo?.Invoke("Łuk z punktów: pobieranie osi torów z OpenStreetMap…");
        try
        {
            if (await OsmPobierz() < 0 || Osm == null) return -1;
            var nodes = Osm.Nodes.Values;
            var r = Geometry(t);
            var near = OsmPunkty.NearTrack(nodes, r).Where(p => FitPts.All(q => XY.Dist(q, p) / r.K > 1)).ToList();
            if (near.Count == 0) { DraftInfo?.Invoke($"Łuk z punktów: OpenStreetMap – {nodes.Count} węzłów torów, żaden nie leży do 8 m od tego toru"); return 0; }
            FitPts.AddRange(near); Refit();
            DraftInfo?.Invoke($"Łuk z punktów: dodano {near.Count} punktów z OpenStreetMap ({OsmPunkty.Attribution}) – " + FitInfo().Replace("Łuk z punktów: ", ""));
            return near.Count;
        }
        catch (Exception ex) { DraftInfo?.Invoke("Łuk z punktów: OpenStreetMap – " + Pobieranie.Reason(ex)); return -1; }
    }

    /// <summary>Punkty śladu (narzędzie Ślad) do 8 m od wybranego toru dodane do punktów dopasowania (klawisz G).</summary>
    public int AddSladPoints()
    {
        if (fitArc is not var (id, _) || Track(id) is not UtTrack t) return 0;
        if (SladPts.Count == 0) { DraftInfo?.Invoke("Łuk z punktów: brak śladu – narysuj go narzędziem Ślad (F)"); return 0; }
        var r = Geometry(t);
        var near = SladPts.Where(p => Budowa.Dist(r, p) / r.K <= 8 && FitPts.All(q => XY.Dist(q, p) / r.K > 0.5)).ToList();
        FitPts.AddRange(near); Refit();
        DraftInfo?.Invoke($"Łuk z punktów: dodano {near.Count} punktów śladu – " + FitInfo().Replace("Łuk z punktów: ", ""));
        return near.Count;
    }

    void DrawArcFit(DrawingContext dc, double dpi)
    {
        if (tool != UtTool.LukPunkty) return;
        if (fitArc is not var (id, vi) || Track(id) is not UtTrack t)
        {
            if (hover is XY h && FitVertexAt(ToScreen(h)) is var (hid, hvi) && Track(hid) is UtTrack ht && hvi >= 0)
            { dc.DrawEllipse(null, SnapPen, ToScreen(ht.Vertices[hvi].P), 7, 7); Label(dc, $"łuk W{hvi + 1} – kliknij", ToScreen(h) + new Vector(12, 8), DraftInk, dpi); }
            return;
        }
        var pen = new Pen(DraftInk, 2) { DashStyle = new DashStyle(new double[] { 4, 2 }, 0) };
        if (vi < 0)
        {
            if (FitAlign != null) dc.DrawGeometry(null, pen, Line(TrasaElem.Compute(FitAlign, Trasa.KFor(t)).Sample(0.5), 0));
        }
        else dc.DrawEllipse(null, SnapPen, ToScreen(t.Vertices[vi].P), 7, 7);
        if (vi >= 0 && FitResult is { Error: null } fr)
        {
            if (fr.Vertex is XY nv) DashLine(dc, new Pen(DraftInk, 1) { DashStyle = DashStyles.Dot }, ToScreen(t.Vertices[vi].P), ToScreen(nv));
            if (fr.Parts != null)
                foreach (var (pi, pp, _, _) in fr.Parts) DashLine(dc, new Pen(DraftInk, 1) { DashStyle = DashStyles.Dot }, ToScreen(t.Vertices[pi].P), ToScreen(pp));
            if (FitGeometry() is TrasaResult fg) dc.DrawGeometry(null, pen, Line(fg.Sample(0.5), 0));
        }
        var red = new Pen(Red, 1.6); var grey = new Pen(Brushes.Gray, 1.6);
        // przy wielu punktach (malowanie, OSM) mniejsze znaczniki, a odchyłki co najmniej 70 px od siebie, żeby napisy się nie zlewały
        double sz = FitPts.Count > 60 ? 2.5 : 5; Point? lastLab = null;
        for (int i = 0; i < FitPts.Count; i++)
        {
            var q = ToScreen(FitPts[i]); bool off = FitOut.Contains(i); var pp = off ? grey : red;
            dc.DrawLine(pp, q + new Vector(-sz, -sz), q + new Vector(sz, sz)); dc.DrawLine(pp, q + new Vector(-sz, sz), q + new Vector(sz, -sz));
            if (FitResult is { Error: null } && i < FitDev.Count && (off || lastLab is not Point lp || (q - lp).Length >= 70))
            {
                Label(dc, off ? $"pominięty {FitDev[i] * 100:0} cm" : $"{FitDev[i] * 100:0.#} cm", q + new Vector(7, -16), off ? Hint : DraftInk, dpi);
                if (!off) lastLab = q;
            }
        }
        if (fitNetFrom is var (nf, _, _)) dc.DrawEllipse(null, SnapPen, ToScreen(nf), 6, 6);
        if (hover is XY hv) Label(dc, FitInfo().Replace("Łuk z punktów: ", ""), ToScreen(hv) + new Vector(12, 8), FitResult?.Error != null ? Red : DraftInk, dpi);
    }
}

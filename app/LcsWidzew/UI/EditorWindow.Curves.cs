using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LcsWidzew.Model;

namespace LcsWidzew.UI;

/// <summary>Plan: punkty załamania torów (bez węzłów), narzędzie Łuk (3 punkty / styczny) i łuk o promieniu w narożniku.</summary>
public partial class EditorWindow
{
    /// <summary>Narzędzie Łuk: punkt na łuku (drugie kliknięcie); null – czeka na punkt na łuku.</summary>
    Point? arcMid;

    /// <summary>Tor ciągu od chainFrom do nid – odcinek dziedziczony jak w narzędziu Tor; null – tor pionowy bez kierunku.</summary>
    string AddChainEdge(string nid)
    {
        var (ax, _) = doc.Pos(chainFrom); var (bx, _) = doc.Pos(nid);
        int vdir = 0;
        if (Math.Abs(ax - bx) < 0.5) { vdir = VerticalDir(chainFrom, nid); if (vdir == 0) return null; }
        // pierwszy tor ciągu: przedłużenie toru (koniec toru) albo odgałęzienie zwrotnicy dziedziczy jego odcinek
        if (chainSec == null && chkSameSec.IsChecked == true)
        {
            var swAt = doc.Switches.Select(x => (JsonObject)x.Value).FirstOrDefault(x => StationDoc.Sv(x["node"]) == chainFrom);
            var at = doc.EdgesAt(chainFrom);
            if (swAt != null) chainSec = StationDoc.Sv(swAt["sec"]);
            else if (at.Count == 1 && !doc.Signals.Any(x => StationDoc.Sv(x.Value["node"]) == chainFrom)) chainSec = StationDoc.Sv(at[0]["sec"]);
        }
        var endEdges = doc.EdgesAt(nid);
        var eid = doc.AddEdge(chainFrom, nid, chkSameSec.IsChecked == true ? chainSec : null, Post);
        // ciąg dochodzący do końca istniejącego toru (lub wychodzący z niego) – jeden odcinek, bez przypadkowej przerwy izolacyjnej
        if (chkSameSec.IsChecked == true && endEdges.Count == 1 && !doc.Signals.Any(x => StationDoc.Sv(x.Value["node"]) == nid) && !doc.Switches.Any(x => StationDoc.Sv(x.Value["node"]) == nid))
        {
            var target = StationDoc.Sv(endEdges[0]["sec"]); var mine = StationDoc.Sv(doc.Edge(eid)["sec"]);
            if (target != mine && !st.Blocks.Values.Any(b => b.SecsWE.Contains(target)))
                foreach (var x in doc.EdgeObjs.Where(x => StationDoc.Sv(x["sec"]) == mine)) x["sec"] = target;
        }
        if (vdir != 0) doc.Edge(eid)["dir"] = vdir;
        chainSec = StationDoc.Sv(doc.Edge(eid)["sec"]);
        chainFrom = nid;
        return eid;
    }

    /// <summary>Kliknięcie narzędziem Łuk: początek → punkt na łuku → koniec (Ctrl+klik: koniec łuku stycznego).</summary>
    void ArcClick(Point w)
    {
        if (chainFrom == null || !doc.Nodes.ContainsKey(chainFrom))
        {
            doc.Snapshot(); chainFrom = NodeAt(w, true); chainSec = null; arcMid = null; Rebuild();
            status.Text = "Łuk: kliknij punkt na łuku (Ctrl+klik – od razu koniec łuku stycznego do toru)"; return;
        }
        bool tangent = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (arcMid == null && !tangent) { arcMid = Snap(w); status.Text = "Łuk: kliknij koniec łuku (węzeł, tor albo puste miejsce)"; panel.InvalidateVisual(); return; }
        ArcFinish(w, tangent && arcMid == null);
    }

    /// <summary>Koniec łuku: nowy tor od początku do kliknięcia, kształt po okręgu, promień w torze.</summary>
    string ArcFinish(Point w, bool tangent)
    {
        var a = doc.Pos(chainFrom);
        (double X, double Y)? tdir = tangent ? doc.TangentAt(chainFrom) : null;
        if (tangent && tdir == null) { status.Text = "✗ Łuk styczny: w początku musi kończyć się jeden tor (styczna z jego kierunku)"; return null; }
        doc.Snapshot();
        var nid = NodeAt(w, true);
        if (nid == chainFrom) { doc.Undo(); Rebuild(); return null; }
        var b = doc.Pos(nid);
        var (pts, R) = tangent ? doc.ArcTangent(a, tdir.Value, b) : doc.Arc3(a, (arcMid.Value.X, arcMid.Value.Y), b);
        var eid = AddChainEdge(nid);
        if (eid == null) { doc.Undo(); Rebuild(); status.Text = "✗ Łuk: końce w tym samym miejscu w poziomie – kierunek toru nieokreślony"; return null; }
        doc.SetEdgeShape(eid, pts, true);
        if (R > 0) doc.Edge(eid)["radius"] = Math.Round(R); else doc.Edge(eid).Remove("radius");
        arcMid = null;
        Changed((R > 0 ? $"Łuk R = {R:0} m" : "Punkty w linii – tor prosty") + $", długość {StationDoc.D(doc.Edge(eid)["len"]):0.#} m. Dalej: punkt na łuku i koniec (Ctrl+klik – łuk styczny), PPM kończy");
        Select("edge", eid);
        return eid;
    }

    /// <summary>Punkt załamania toru (bez węzła) pod kursorem: (tor, indeks).</summary>
    (string E, int I)? PickBend(Point w)
    {
        double bd = R * 0.8; (string, int)? best = null;
        foreach (var e in st.Edges.Values.Where(x => !x.Link))
            for (int i = 1; i < e.Pts.Count - 1; i++)
            {
                var d = Math.Sqrt(Math.Pow(e.Pts[i].X - w.X, 2) + Math.Pow(e.Pts[i].Y - w.Y, 2));
                if (d < bd) { bd = d; best = (e.Id, i); }
            }
        return best;
    }

    /// <summary>Dwuklik na torze: nowy punkt załamania dokładnie na torze (kształt się nie zmienia, dopóki go nie przeciągniesz).</summary>
    void BendAdd(string eid, Point w)
    {
        doc.Snapshot(); doc.InsertEdgePt(eid, w.X, w.Y);
        Changed("Dodano punkt załamania – przeciągnij go na zdjęcie; PPM na punkcie – usuń. Alt+dwuklik – zaznacz cały tor");
        Select("edge", eid);
    }

    /// <summary>Uchwyty punktów załamania (zaznaczony tor, tory przy zaznaczonym węźle, tor pod kursorem) i podgląd łuku.</summary>
    void BendOverlay(DrawingContext dc, double scale)
    {
        var ids = new HashSet<string>();
        if (selKind == "edge" && selId != null) ids.Add(selId);
        if (selKind == "node" && selId != null && st.Nodes.TryGetValue(selId, out var sn)) foreach (var x in sn.Edges) ids.Add(x);
        if (tool == Tool.Select && PickEdge(mouse) is { } he) ids.Add(he);
        var br = new SolidColorBrush(Color.FromRgb(0x00, 0x90, 0xe0)); double h = 3.2 / scale;
        foreach (var id in ids)
            if (st.Edges.TryGetValue(id, out var e))
                for (int i = 1; i < e.Pts.Count - 1; i++) dc.DrawRectangle(Brushes.White, new Pen(br, 1.2 / scale), new Rect(e.Pts[i].X - h, e.Pts[i].Y - h, 2 * h, 2 * h));
        // podgląd łuku
        if (tool == Tool.Arc && chainFrom != null && doc.Nodes.ContainsKey(chainFrom))
        {
            var a = doc.Pos(chainFrom); var m = Snap(mouse);
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(0xff, 0x6a, 0x00)), 1.6 / scale) { DashStyle = DashStyles.Dash };
            List<(double X, double Y)> pts = null; double R = 0;
            if (arcMid is Point mid) (pts, R) = doc.Arc3(a, (mid.X, mid.Y), (m.X, m.Y));
            else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && doc.TangentAt(chainFrom) is { } t) (pts, R) = doc.ArcTangent(a, t, (m.X, m.Y));
            else dc.DrawLine(pen, new Point(a.X, a.Y), m);
            if (arcMid is Point mm) dc.DrawEllipse(null, pen, mm, 4 / scale, 4 / scale);
            if (pts != null)
            {
                for (int i = 0; i < pts.Count - 1; i++) dc.DrawLine(pen, new Point(pts[i].X, pts[i].Y), new Point(pts[i + 1].X, pts[i + 1].Y));
                var ft = new FormattedText(R > 0 ? $"R = {R:0} m, {doc.PlanLen(pts):0} m" : "prosta", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12 / scale, Brushes.OrangeRed, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(ft, new Point(m.X + 10 / scale, m.Y + 6 / scale));
            }
        }
    }

    /// <summary>Panel węzła: łuk o zadanym promieniu w narożniku między dwoma torami.</summary>
    void FilletButton(string nid)
    {
        if (dispMode || doc.EdgesAt(nid).Count != 2 || doc.Switches.Any(s => StationDoc.Sv(s.Value["node"]) == nid)) return;
        props.Children.Add(Btn("Łuk w narożniku – promień R [m]…", (a, e) => Safe(() =>
        {
            var r = Ask("Łuk w narożniku", ("Promień łuku [m] (tory przy węźle muszą mieć proste odcinki dłuższe od stycznej)", "300"));
            if (r == null || !double.TryParse(r[0].Trim().Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var R)) return;
            doc.Snapshot();
            try { var msg = doc.FilletNode(nid, R); Changed("Narożnik zaokrąglony: " + msg); }
            catch (InvalidOperationException ex) { doc.Undo(); Rebuild(); status.Text = "✗ " + ex.Message; }
        })));
    }

    /// <summary>Test (--editshot … luki out.json): punkty załamania, łuk przez 3 punkty, łuk styczny, łuk w narożniku, przyciąganie.</summary>
    public string CurvesForTest(string outJson)
    {
        var log = new System.Text.StringBuilder(); FuzzMode = true; int bad = 0;
        void Ok(bool c, string m) { log.AppendLine((c ? "OK  " : "BŁĄD") + " " + m); if (!c) bad++; }
        doc = StationDoc.New("TL", "Łuki (test)"); path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lcs_luki.json"); dispMode = false; Rebuild(true);
        PlanForTest();
        double ux = doc.Ux; Ok(doc.RealScale && doc.LenAuto, $"nowy obszar: skala rzeczywista 1:{doc.ScaleAlong:0}, długości z rysunku");
        // tor prosty 400 m, punkt załamania, przesunięcie o 30 m w bok
        var n0 = doc.AddNode("TL", 0, 0); var n1 = doc.AddNode("TL", 400 * ux, 0); var e0 = doc.AddEdge(n0, n1, null, "TL"); Rebuild();
        BendAdd(e0, new Point(200 * ux, 3));
        Ok(doc.EdgePts(e0).Count == 3 && Math.Abs(doc.EdgePts(e0)[1].Y) < 0.01, "dwuklik: punkt załamania dokładnie na torze");
        Ok(Math.Abs(StationDoc.D(doc.Edge(e0)["len"]) - 400) < 0.1, "długość bez zmian po dodaniu punktu");
        doc.Snapshot(); doc.MoveEdgePt(e0, 1, 200 * ux, 30 * ux); Changed();
        double want = 2 * Math.Sqrt(200 * 200 + 30 * 30);
        Ok(Math.Abs(StationDoc.D(doc.Edge(e0)["len"]) - want) < 0.2, $"przesunięty punkt: długość {StationDoc.D(doc.Edge(e0)["len"]):0.#} m (oczekiwane {want:0.#})");
        Ok(PickBend(new Point(200 * ux + 1, 30 * ux)) is { } pb && pb.E == e0 && pb.I == 1, "punkt załamania wskazywany myszą");
        Right(new Point(200 * ux, 30 * ux));
        Ok(doc.EdgePts(e0).Count == 2 && Math.Abs(StationDoc.D(doc.Edge(e0)["len"]) - 400) < 0.1, "PPM usuwa punkt, długość wraca do 400 m");
        // łuk przez 3 punkty na okręgu R = 300 m, kąt 60°
        double R = 300; (double, double) P(double deg) => (1000 * ux + R * Math.Sin(deg * Math.PI / 180) * ux, 200 * ux + (R - R * Math.Cos(deg * Math.PI / 180)) * ux);
        chkSnap.IsChecked = false;   // rysowanie po podkładzie – bez przyciągania
        SetTool(Tool.Arc);
        var pa = P(0); var pm = P(30); var pe = P(60);
        ArcClick(new Point(pa.Item1, pa.Item2)); ArcClick(new Point(pm.Item1, pm.Item2)); var ea = selId; ArcClick(new Point(pe.Item1, pe.Item2)); ea = selId;
        double rr = StationDoc.D(doc.Edge(ea)["radius"]), ll = StationDoc.D(doc.Edge(ea)["len"]), arcLen = R * Math.PI / 3;
        Ok(Math.Abs(rr - R) <= 1, $"łuk przez 3 punkty: R = {rr:0} m (oczekiwane {R:0})");
        Ok(Math.Abs(ll - arcLen) / arcLen < 0.005, $"długość po łuku {ll:0.#} m (oczekiwane {arcLen:0.#})");
        // łuk styczny – ciąg dalej od końca łuku: jeszcze 30° tego samego okręgu
        var p90 = P(90);
        var et = ArcFinish(new Point(p90.Item1, p90.Item2), true);
        Ok(et != null && Math.Abs(StationDoc.D(doc.Edge(et)["radius"]) - R) <= 2, $"łuk styczny od końca łuku: R = {StationDoc.D(doc.Edge(et ?? ea)["radius"]):0} m (oczekiwane {R:0})");
        SetTool(Tool.Select);
        // łuk w narożniku: dwa tory pod kątem 90°, R = 100 m → styczne 100 m
        var c0 = doc.AddNode("TL", 0, 800 * ux); var c1 = doc.AddNode("TL", 300 * ux, 800 * ux); var c2 = doc.AddNode("TL", 600 * ux, 500 * ux);
        var f1 = doc.AddEdge(c0, c1, null, "TL"); var f2 = doc.AddEdge(c1, c2, null, "TL"); Rebuild();
        double before = StationDoc.D(doc.Edge(f1)["len"]) + StationDoc.D(doc.Edge(f2)["len"]);
        doc.Snapshot(); var msg = doc.FilletNode(c1, 100); Changed();
        double after = StationDoc.D(doc.Edge(f1)["len"]) + StationDoc.D(doc.Edge(f2)["len"]);
        double defl = Math.PI / 4, T = 100 * Math.Tan(defl / 2), exp = before - 2 * T + 100 * defl;
        Ok(Math.Abs(after - exp) < 0.5 && StationDoc.D(doc.Edge(f1)["radius"]) == 100, $"łuk w narożniku R = 100 m (odchylenie 45°): {msg}; razem {after:0.#} m (oczekiwane {exp:0.#})");
        bool threw = false; try { doc.FilletNode(c1, 5000); } catch (InvalidOperationException) { threw = true; }
        Ok(threw, "za duży promień – odrzucony z komunikatem");
        // przyciąganie wyłączone – punkt w miejscu kliknięcia
        chkSnap.IsChecked = false; var sp = Snap(new Point(123.456, 77.777));
        Ok(Math.Abs(sp.X - 123.46) < 0.01 && Math.Abs(sp.Y - 77.78) < 0.01, "bez przyciągania – punkt tam, gdzie kliknięto");
        chkSnap.IsChecked = true; var sp2 = Snap(new Point(123.456, 77.777));
        Ok(Math.Abs(sp2.X % Grid) < 1e-6, "z przyciąganiem – punkt na siatce");
        doc.Save(outJson);
        var re = Station.Load(outJson);
        Ok(re.Edges[ea].Radius == Math.Round(rr) && re.Edges[ea].Pts.Count > 8, "zapis i wczytanie łuku (promień, punkty)");
        var sim = new LcsWidzew.Sim.Simulator(re); Ok(sim != null, "model z łukami wczytuje się do symulatora");
        panel.FitAll();
        log.AppendLine($"błędów testu: {bad}, błędów edytora: {Errors.Count}");
        return log.ToString();
    }
}

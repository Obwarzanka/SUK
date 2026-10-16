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
    /// <summary>Wyginany w łuk tor (przeciąganie toru narzędziem Łuk albo Ctrl+przeciągnij w „Wskaż”).</summary>
    string arcBend; Point arcBendStart;

    /// <summary>Narzędzie Tor w trybie „łukiem”: każdy kolejny odcinek – łuk styczny do poprzedniego.</summary>
    bool arcMode;
    System.Windows.Controls.Primitives.ToggleButton arcModeBtn;
    void SetArcMode(bool on)
    {
        arcMode = on;
        if (arcModeBtn != null) { arcModeBtn.IsChecked = on; arcModeBtn.Background = new SolidColorBrush(on ? Color.FromRgb(0x0a, 0x4a, 0x66) : Color.FromRgb(0x26, 0x26, 0x26)); }
        if (on && tool != Tool.Track) SetTool(Tool.Track);
        status.Text = on ? "Tor ŁUKIEM: klikaj wzdłuż toru – każdy odcinek to łuk styczny do poprzedniego (pierwszy prosty albo styczny do toru, z którego wychodzisz). C – proste" : "Tor: odcinki proste (C – łukiem)";
        panel.InvalidateVisual();
    }

    /// <summary>
    /// Punkt przyciągnięcia pod kursorem: węzeł (zasięg ok. 10 px), tor (rzut na tor) albo siatka / punkt kliknięty.
    /// To samo, co wybiera kliknięcie narzędziem Tor / Łuk – podgląd pokazuje, gdzie trafi punkt.
    /// </summary>
    (Point P, string Kind, string Id) SnapTarget(Point w)
    {
        if (PickNode(w) is { } n && st.Nodes.TryGetValue(n, out var nd)) return (new Point(nd.X, nd.Y), "node", n);
        if (PickEdge(w) is { } e && st.Edges.TryGetValue(e, out var ed))
        {
            Point best = w; double bd = double.MaxValue;
            for (int i = 0; i < ed.Pts.Count - 1; i++)
            {
                var a = ed.Pts[i]; var b = ed.Pts[i + 1]; double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
                double t = l2 == 0 ? 0 : Math.Clamp(((w.X - a.X) * dx + (w.Y - a.Y) * dy) / l2, 0, 1);
                var q = new Point(a.X + t * dx, a.Y + t * dy); var d = (q - w).Length;
                if (d < bd) { bd = d; best = q; }
            }
            return (best, "edge", e);
        }
        return (Snap(w), "grid", null);
    }

    /// <summary>Tor pod upuszczonym węzłem (nie jego własny) – odgałęzienie.</summary>
    string DropEdge(string dn)
    {
        if (!doc.Nodes.ContainsKey(dn)) return null;
        var (x, y) = doc.Pos(dn); var own = doc.EdgesAt(dn).Select(e => StationDoc.Sv(e["id"])).ToHashSet();
        string best = null; double bd = R * 0.8;
        foreach (var e in st.Edges.Values.Where(q => !q.Link && !own.Contains(q.Id)))
            for (int i = 0; i < e.Pts.Count - 1; i++) { var d = DistSeg(new Point(x, y), e.Pts[i], e.Pts[i + 1]); if (d < bd) { bd = d; best = e.Id; } }
        return best;
    }

    /// <summary>Znacznik przyciągnięcia (narzędzia rysujące, przeciąganie węzła): zielony – węzeł, pomarańczowy – tor.</summary>
    void SnapMarker(DrawingContext dc, double scale)
    {
        bool drawing = tool is Tool.Track or Tool.Arc or Tool.Turnout or Tool.Signal or Tool.TrackEnd or Tool.Derailer;
        if (!drawing || dragging) return;
        var (p, k, _) = SnapTarget(mouse);
        double h = 6 / scale;
        if (k == "node") dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x50, 0x20, 0xc0, 0x40)), new Pen(new SolidColorBrush(Color.FromRgb(0x10, 0xa0, 0x30)), 2 / scale), new Rect(p.X - h, p.Y - h, 2 * h, 2 * h));
        else if (k == "edge")
        {
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(0xff, 0x6a, 0x00)), 2 / scale);
            dc.DrawLine(pen, p + new Vector(-h, -h), p + new Vector(h, h)); dc.DrawLine(pen, p + new Vector(-h, h), p + new Vector(h, -h));
        }
        else dc.DrawEllipse(null, new Pen(Brushes.Gray, 1 / scale), p, h * 0.5, h * 0.5);
    }

    /// <summary>Międzytorze z paska [m] (2–50, domyślnie 4,75).</summary>
    double Gap => double.TryParse(gapBox.Text.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var g) ? Math.Clamp(g, 2, 50) : 4.75;

    /// <summary>Punkty całego ciągu toru przez węzły bez zwrotnic (do końców / zwrotnic) – w kolejności od końca A.</summary>
    List<(double X, double Y)> RunPts(string eid) => RunPtsEnds(eid).Pts;
    (List<(double X, double Y)> Pts, string A, string B) RunPtsEnds(string eid)
    {
        var e0 = st.Edges[eid]; var pts = e0.Pts.ToList();
        bool Pass(string n) => st.Nodes[n].Edges.Count == 2 && st.Nodes[n].Switch == null;
        string node = e0.B, edge = eid;
        for (int g = 0; g < 500 && Pass(node); g++)
        {
            var nx = st.Nodes[node].Edges.First(x => x != edge); if (nx == eid) break; var ne = st.Edges[nx]; if (ne.Link) break;
            var p = ne.Pts.ToList(); if (ne.B == node) p.Reverse(); pts.AddRange(p.Skip(1));
            node = ne.A == node ? ne.B : ne.A; edge = nx;
        }
        var endB = node;
        node = e0.A; edge = eid;
        for (int g = 0; g < 500 && Pass(node); g++)
        {
            var nx = st.Nodes[node].Edges.First(x => x != edge); if (nx == eid) break; var ne = st.Edges[nx]; if (ne.Link) break;
            var p = ne.Pts.ToList(); if (ne.A == node) p.Reverse(); pts.InsertRange(0, p.Take(p.Count - 1));
            node = ne.A == node ? ne.B : ne.A; edge = nx;
        }
        return (pts, node, endB);
    }

    /// <summary>Tor równoległy do toru pod kursorem: punkty, strona, promień.</summary>
    (List<(double X, double Y)> Pts, double? R, string A, string B)? ParallelPreview(Point w)
    {
        if (PickEdge(w) is not { } eid) return null;
        var (pts, ra, rb) = RunPtsEnds(eid);
        // strona: po której stronie toru jest kursor (w metrach)
        int best = 0; double bd = double.MaxValue;
        for (int i = 0; i < pts.Count - 1; i++) { var d = DistSeg(w, pts[i], pts[i + 1]); if (d < bd) { bd = d; best = i; } }
        double tx = (pts[best + 1].X - pts[best].X) / doc.Ux, ty = (pts[best + 1].Y - pts[best].Y) / doc.Uy;
        double cx = (w.X - pts[best].X) / doc.Ux, cy = (w.Y - pts[best].Y) / doc.Uy;
        int side = (-ty) * cx + tx * cy >= 0 ? 1 : -1;
        var off = doc.OffsetPolyline(pts, Gap, side);
        return (off, doc.FitRadius(off), ra, rb);
    }

    /// <summary>Klik narzędziem „Tor równoległy”: nowy tor obok w międzytorzu; końce w istniejących węzłach (wolnych) – połączone.</summary>
    void ParallelClick(Point w)
    {
        if (ParallelPreview(w) is not { } pv) { status.Text = "Tor równoległy: najedź na istniejący tor i kliknij po tej stronie, po której ma powstać nowy"; return; }
        var pts = pv.Pts;
        doc.Snapshot();
        // koniec w istniejącym węźle tylko, gdy leży w tym samym miejscu (≤ 0,5 m) i nie jest końcem toru bazowego
        string End((double X, double Y) p)
        {
            var ex = st.Nodes.Values.Where(n => n.Id != pv.A && n.Id != pv.B && doc.EdgesAt(n.Id).Count < 3 && doc.PlanLen(n.X, n.Y, p.X, p.Y) <= 0.5).Select(n => n.Id).FirstOrDefault();
            return ex ?? doc.AddNode(Post, p.X, p.Y);
        }
        var a = End(pts[0]); var b = End(pts[^1]);
        if (a == b) { doc.Undo(); Rebuild(); status.Text = "✗ Tor równoległy: oba końce w tym samym węźle"; return; }
        if (Math.Abs(doc.Pos(a).X - doc.Pos(b).X) < 0.5) { doc.Undo(); Rebuild(); status.Text = "✗ Tor równoległy pionowy – narysuj go narzędziem Tor"; return; }
        var eid = doc.AddEdge(a, b, null, Post);
        doc.SetEdgeShape(eid, pts, true);
        if (pv.R is double r) doc.Edge(eid)["radius"] = r;
        Changed($"Tor równoległy w międzytorzu {Gap:0.##} m, długość {StationDoc.D(doc.Edge(eid)["len"]):0.#} m" + (pv.R is double rr ? $", łuk R = {rr:0} m" : "") + ". Połącz go rozjazdami (⌐ Rozjazd) albo przeciągnij końce na tory.");
        Select("edge", eid);
    }

    /// <summary>Odległość [m] od punktu do najbliższego toru (innego niż tory w węźle „except”), do 30 m; null – brak.</summary>
    double? NearGap(Point m, string except)
    {
        var own = except != null && st.Nodes.TryGetValue(except, out var en) ? en.Edges.ToHashSet() : new HashSet<string>();
        double best = double.MaxValue;
        foreach (var e in st.Edges.Values.Where(x => !x.Link && !own.Contains(x.Id)))
            for (int i = 0; i < e.Pts.Count - 1; i++)
            {
                var a = e.Pts[i]; var b = e.Pts[i + 1]; double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
                double t = l2 == 0 ? 0 : Math.Clamp(((m.X - a.X) * dx + (m.Y - a.Y) * dy) / l2, 0, 1);
                double d = doc.PlanLen(m.X, m.Y, a.X + t * dx, a.Y + t * dy);
                if (d < best) best = d;
            }
        return best <= 30 ? best : null;
    }

    /// <summary>Węzeł, na który upuszczono przeciągany węzeł (połączenie torów); null – brak w zasięgu.</summary>
    string DropTarget(string dn)
    {
        if (!doc.Nodes.ContainsKey(dn)) return null;
        var (x, y) = doc.Pos(dn);
        return st.Nodes.Values.Where(n => n.Id != dn && n.X > Station.Hidden + 1 && OnPage(n.X))
            .Select(n => (n.Id, D: Math.Sqrt((n.X - x) * (n.X - x) + (n.Y - y) * (n.Y - y)))).Where(q => q.D <= R).OrderBy(q => q.D).Select(q => q.Id).FirstOrDefault();
    }

    /// <summary>Puszczenie przy wyginaniu toru: krótkie kliknięcie (narzędzie Łuk) – początek nowego łuku; przeciągnięcie – tor w łuk przez punkt puszczenia.</summary>
    void ArcBendUp(Point w)
    {
        var eid = arcBend; arcBend = null;
        if ((w - arcBendStart).Length * panel.Scale < 5)
        {
            if (tool == Tool.Arc) { doc.Snapshot(); chainFrom = NodeAt(arcBendStart, true); chainSec = null; arcMid = null; Rebuild(); status.Text = "Łuk: kliknij punkt na łuku (Ctrl+klik – od razu koniec łuku stycznego do toru)"; }
            else panel.InvalidateVisual();
            return;
        }
        BendTrack(eid, Snap(w));
    }

    /// <summary>Tor wygięty w łuk przez punkt m (końce toru w miejscu); promień zapisany, długość po łuku.</summary>
    public void BendTrack(string eid, Point m)
    {
        if (!st.Edges.TryGetValue(eid, out var e)) return;
        var a = (st.Nodes[e.A].X, st.Nodes[e.A].Y); var b = (st.Nodes[e.B].X, st.Nodes[e.B].Y);
        var (pts, R) = doc.Arc3(a, (m.X, m.Y), b);
        doc.Snapshot(); doc.SetEdgeShape(eid, pts, false);
        if (R > 0) doc.Edge(eid)["radius"] = Math.Round(R); else doc.Edge(eid).Remove("radius");
        Changed((R > 0 ? $"Tor wygięty w łuk R = {R:0} m" : "Tor wyprostowany") + $", długość {StationDoc.D(doc.Edge(eid)["len"]):0.#} m (Ctrl+Z cofa)");
        Select("edge", eid);
    }

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
        if ((chainFrom == null || !doc.Nodes.ContainsKey(chainFrom)) && PickNode(w) == null && PickEdge(w) is { } be)
        {
            arcBend = be; arcBendStart = w; Select("edge", be);
            status.Text = "Przeciągnij – tor wygina się w łuk przez kursor; samo kliknięcie – początek nowego łuku na tym torze"; return;
        }
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
        // podgląd: tor wyginany w łuk
        if (arcBend != null && st.Edges.TryGetValue(arcBend, out var be) && (mouse - arcBendStart).Length * scale >= 5)
        {
            var (bp, bR) = doc.Arc3((st.Nodes[be.A].X, st.Nodes[be.A].Y), (Snap(mouse).X, Snap(mouse).Y), (st.Nodes[be.B].X, st.Nodes[be.B].Y));
            var bpen = new Pen(new SolidColorBrush(Color.FromRgb(0xff, 0x6a, 0x00)), 2 / scale);
            for (int i = 0; i < bp.Count - 1; i++) dc.DrawLine(bpen, new Point(bp[i].X, bp[i].Y), new Point(bp[i + 1].X, bp[i + 1].Y));
            var bft = new FormattedText(bR > 0 ? $"R = {bR:0} m, {doc.PlanLen(bp):0} m" : "prosta", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12 / scale, Brushes.OrangeRed, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(bft, new Point(mouse.X + 10 / scale, mouse.Y + 6 / scale));
        }
        // przeciągany koniec toru nad innym węzłem – zostanie połączony; nad torem – odgałęzienie
        if (dragging && dragNode != null && dragSnap && DropTarget(dragNode) is { } dt && st.Nodes.TryGetValue(dt, out var dtn))
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(0x50, 0x20, 0xc0, 0x40)), new Pen(new SolidColorBrush(Color.FromRgb(0x10, 0xa0, 0x30)), 2 / scale), new Point(dtn.X, dtn.Y), 10 / scale, 10 / scale);
        else if (dragging && dragNode != null && dragSnap && DropEdge(dragNode) is { } dte && doc.Nodes.ContainsKey(dragNode))
        {
            var (px, py) = doc.Pos(dragNode); double hh = 8 / scale; var opn = new Pen(new SolidColorBrush(Color.FromRgb(0xff, 0x6a, 0x00)), 2.5 / scale);
            dc.DrawLine(opn, new Point(px - hh, py - hh), new Point(px + hh, py + hh)); dc.DrawLine(opn, new Point(px - hh, py + hh), new Point(px + hh, py - hh));
        }
        SnapMarker(dc, scale);
        // tor równoległy – podgląd
        if (tool == Tool.Parallel && ParallelPreview(mouse) is { } pv)
        {
            var ppn = new Pen(new SolidColorBrush(Color.FromRgb(0x00, 0x90, 0xe0)), 2 / scale) { DashStyle = DashStyles.Dash };
            for (int i = 0; i < pv.Pts.Count - 1; i++) dc.DrawLine(ppn, new Point(pv.Pts[i].X, pv.Pts[i].Y), new Point(pv.Pts[i + 1].X, pv.Pts[i + 1].Y));
            var pft = new FormattedText($"międzytorze {Gap:0.##} m" + (pv.R is double pr ? $", R = {pr:0} m" : ""), System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12 / scale, new SolidColorBrush(Color.FromRgb(0x00, 0x70, 0xc0)), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(pft, new Point(mouse.X + 10 / scale, mouse.Y + 8 / scale));
        }
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
        double Rc = 300; (double, double) P(double deg) => (1000 * ux + Rc * Math.Sin(deg * Math.PI / 180) * ux, 200 * ux + (Rc - Rc * Math.Cos(deg * Math.PI / 180)) * ux);
        chkSnap.IsChecked = false;   // rysowanie po podkładzie – bez przyciągania
        SetTool(Tool.Arc);
        var pa = P(0); var pm = P(30); var pe = P(60);
        ArcClick(new Point(pa.Item1, pa.Item2)); ArcClick(new Point(pm.Item1, pm.Item2)); var ea = selId; ArcClick(new Point(pe.Item1, pe.Item2)); ea = selId;
        double rr = StationDoc.D(doc.Edge(ea)["radius"]), ll = StationDoc.D(doc.Edge(ea)["len"]), arcLen = Rc * Math.PI / 3;
        Ok(Math.Abs(rr - Rc) <= 1, $"łuk przez 3 punkty: R = {rr:0} m (oczekiwane {Rc:0})");
        Ok(Math.Abs(ll - arcLen) / arcLen < 0.005, $"długość po łuku {ll:0.#} m (oczekiwane {arcLen:0.#})");
        // łuk styczny – ciąg dalej od końca łuku: jeszcze 30° tego samego okręgu
        var p90 = P(90);
        var et = ArcFinish(new Point(p90.Item1, p90.Item2), true);
        Ok(et != null && Math.Abs(StationDoc.D(doc.Edge(et)["radius"]) - Rc) <= 2, $"łuk styczny od końca łuku: R = {StationDoc.D(doc.Edge(et ?? ea)["radius"]):0} m (oczekiwane {Rc:0})");
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
        // łączenie torów: koniec jednego toru przeciągnięty na koniec drugiego
        chkSnap.IsChecked = false; SetTool(Tool.Select);
        var w0 = doc.AddNode("TL", 0, 1200 * ux); var w1 = doc.AddNode("TL", 200 * ux, 1200 * ux); var w2 = doc.AddNode("TL", 260 * ux, 1210 * ux); var w3 = doc.AddNode("TL", 500 * ux, 1210 * ux);
        var g1 = doc.AddEdge(w0, w1, null, "TL"); var g2 = doc.AddEdge(w2, w3, null, "TL"); Rebuild();
        Down(new Point(200 * ux, 1200 * ux), null); Move(new Point(230 * ux, 1205 * ux), null); Move(new Point(260 * ux + 0.5, 1210 * ux), null); Up(new Point(260 * ux + 0.5, 1210 * ux), null);
        Ok(!doc.Nodes.ContainsKey(w1) && doc.EdgesAt(w2).Count == 2 && StationDoc.Sv(doc.Edge(g1)["b"]) == w2, "koniec toru upuszczony na koniec drugiego – tory połączone");
        Ok(Math.Abs(StationDoc.D(doc.Edge(g1)["len"]) - Math.Sqrt(260 * 260 + 10 * 10)) < 0.2, $"długość połączonego toru {StationDoc.D(doc.Edge(g1)["len"]):0.#} m");
        var w4 = doc.AddNode("TL", 260 * ux, 1300 * ux); doc.AddEdge(w4, doc.AddNode("TL", 400 * ux, 1300 * ux), null, "TL");
        var w5 = doc.AddNode("TL", 100 * ux, 1350 * ux); doc.AddEdge(doc.AddNode("TL", 0, 1350 * ux), w5, null, "TL"); Rebuild();
        bool refused = false; try { doc.WeldNodes(w5, w2); doc.WeldNodes(w4, w2); } catch (InvalidOperationException) { refused = true; }
        Ok(refused && doc.EdgesAt(w2).Count <= 3, "więcej niż 3 tory w węźle – połączenie odrzucone");
        Rebuild();
        // wyginanie toru w łuk: narzędzie Łuk – przeciągnij środek toru 400 m o 40 m w bok
        var b0 = doc.AddNode("TL", 0, 1600 * ux); var b1 = doc.AddNode("TL", 400 * ux, 1600 * ux); var bt = doc.AddEdge(b0, b1, null, "TL"); Rebuild();
        SetTool(Tool.Arc);
        Down(new Point(200 * ux, 1600 * ux), null); Move(new Point(200 * ux, 1580 * ux), null); Up(new Point(200 * ux, 1560 * ux), null);
        double sag = 40, Rb = (200 * 200 + sag * sag) / (2 * sag);
        Ok(Math.Abs(StationDoc.D(doc.Edge(bt)["radius"]) - Rb) <= 1 && doc.EdgePts(bt).Count > 8, $"tor wygięty przeciągnięciem: R = {StationDoc.D(doc.Edge(bt)["radius"]):0} m (oczekiwane {Rb:0})");
        Ok(chainFrom == null, "przeciągnięcie toru nie zaczyna nowego łuku");
        SetTool(Tool.Select);
        // przyciąganie do toru: punkt rzutowany na tor (znacznik pomarańczowy), węzeł – zielony
        var k0 = doc.AddNode("TL", 0, 1800 * ux); var k1 = doc.AddNode("TL", 400 * ux, 1800 * ux); var kt = doc.AddEdge(k0, k1, null, "TL"); Rebuild();
        var (sp0, sk0, si0) = SnapTarget(new Point(100 * ux, 1800 * ux + 0.3 * R));
        Ok(sk0 == "edge" && si0 == kt && Math.Abs(sp0.Y - 1800 * ux) < 0.01, $"przyciąganie do toru: {sk0} (punkt rzutowany na tor)");
        // równoległy tor 4,75 m obok – kliknięcie w połowie odstępu nie łapie żadnego (zasięg ok. 10 px na ekranie)
        ZoomForTest(200 * ux, 1800 * ux, 6);
        double halfGap = 4.75 / 2 * doc.Uy;
        Ok(R < halfGap && SnapTarget(new Point(100 * ux, 1800 * ux - halfGap)).Kind == "grid", $"zasięg łapania {R:0.##} j. (≈ {R / doc.Ux:0.##} m) przy przybliżeniu 6 – mniej niż pół rozstawu torów");
        panel.FitAll();
        // koniec toru upuszczony na środek innego toru – odgałęzienie (3 tory w węźle)
        var t0 = doc.AddNode("TL", 0, 2000 * ux); var t1 = doc.AddNode("TL", 400 * ux, 2000 * ux); var tt = doc.AddEdge(t0, t1, null, "TL");
        var u0 = doc.AddNode("TL", 150 * ux, 2100 * ux); var u1 = doc.AddNode("TL", 250 * ux, 2060 * ux); doc.AddEdge(u0, u1, null, "TL"); Rebuild();
        SetTool(Tool.Select);
        Down(new Point(250 * ux, 2060 * ux), null); Move(new Point(230 * ux, 2030 * ux), null); Move(new Point(220 * ux, 2000 * ux + 0.2), null); Up(new Point(220 * ux, 2000 * ux + 0.2), null);
        var junction = doc.Nodes.Select(k => k.Key).FirstOrDefault(k => doc.EdgesAt(k).Count == 3 && Math.Abs(doc.Pos(k).Y - 2000 * ux) < 0.5);
        Ok(junction != null && !doc.Nodes.ContainsKey(u1), "koniec toru upuszczony na środek toru – odgałęzienie (węzeł z 3 torami)");
        // narzędzie Tor w trybie „łukiem”: prosty odcinek, potem dwa kliknięcia na okręgu R = 400 m stycznym do prostej
        SetTool(Tool.Track); SetArcMode(true);
        double Rr = 400; Point C(double deg) => new(600 * ux + Rr * Math.Sin(deg * Math.PI / 180) * ux, 2400 * ux + (Rr - Rr * Math.Cos(deg * Math.PI / 180)) * ux);
        Down(new Point(300 * ux, 2400 * ux), null); Down(new Point(600 * ux, 2400 * ux), null);
        var s1 = selId; Down(C(15), null); var s2 = selId; Down(C(30), null); var s3 = selId;
        Right(new Point()); SetArcMode(false);
        Ok(doc.Edge(s1)?["radius"] == null, "tryb łukiem: pierwszy odcinek prosty");
        Ok(Math.Abs(StationDoc.D(doc.Edge(s2)["radius"]) - Rr) <= 2 && Math.Abs(StationDoc.D(doc.Edge(s3)["radius"]) - Rr) <= 2, $"tryb łukiem: kolejne odcinki to łuki styczne R = {StationDoc.D(doc.Edge(s2)["radius"]):0} / {StationDoc.D(doc.Edge(s3)["radius"]):0} m (oczekiwane {Rr:0})");
        SetTool(Tool.Select);
        // tor równoległy: do prostego toru (kt, y = 1800 m) w międzytorzu 4,75 m – po stronie kursora (poniżej)
        gapBox.Text = "4.75"; SetTool(Tool.Parallel); panel.FitAll();
        int nBefore = doc.Edges.Count;
        ParallelClick(new Point(200 * ux, 1800 * ux + 1.5 * doc.Uy));
        var pEdge = selId; var pp = doc.EdgePts(pEdge);
        Ok(doc.Edges.Count == nBefore + 1 && pp.All(q => Math.Abs((q.Y - 1800 * ux) / doc.Uy - 4.75) < 0.02), $"tor równoległy do prostego w międzytorzu 4,75 m: {(pp[0].Y - 1800 * ux) / doc.Uy:0.00} m");
        Ok(Math.Abs(StationDoc.D(doc.Edge(pEdge)["len"]) - 400) < 0.2, $"długość toru równoległego {StationDoc.D(doc.Edge(pEdge)["len"]):0.#} m");
        Ok(NearGap(new Point(100 * ux, 1800 * ux + 2 * doc.Uy), null) is double ng && Math.Abs(ng - 2) < 0.05, "odległość do toru obok przy rysowaniu (2,00 m)");
        // tor równoległy do łuku R = 300 m (ea) po zewnętrznej stronie – R = 304,75 m
        var mid = st.Edges[ea].Pts[st.Edges[ea].Pts.Count / 2];
        var cc = (1000 * ux, 200 * ux + 300 * ux);   // środek okręgu łuku (w metrach × ux)
        var outDir = new Vector(mid.X - cc.Item1, mid.Y - cc.Item2); outDir.Normalize();
        ParallelClick(new Point(mid.X + outDir.X * 1.5 * ux, mid.Y + outDir.Y * 1.5 * ux));
        var pr = StationDoc.D(doc.Edge(selId)["radius"]);
        Ok(Math.Abs(pr - 304.75) <= 1.5, $"tor równoległy do łuku R = 300 m: R = {pr:0} m (oczekiwane ok. 305)");
        // nasyp – dane toru
        doc.Snapshot(); doc.Edge(pEdge)["nasyp"] = 3.5; Changed();
        Ok(st.Edges[pEdge].Embank == 3.5, "wysokość toru na nasypie zapisana (3,5 m)");
        SetTool(Tool.Select);
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

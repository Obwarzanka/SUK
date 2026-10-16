using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using LcsWidzew.Model;

namespace LcsWidzew.UI;

/// <summary>
/// Narzędzia planu schematycznego (układ torowy rysowany po podkładzie w skali, tory w dowolnym kierunku):
/// peron wzdłuż toru (obrócony prostokąt) i rozjazd jak w terenie (prosty łącznik / odgałęzienie pod dowolnym kątem).
/// Na zobrazowaniu (obraz pulpitu) zostają narzędzia pulpitowe: peron – prostokąt, rozjazd – ukos–pion–ukos.
/// </summary>
public partial class EditorWindow
{
    /// <summary>Peron na planie: 1. i 2. punkt – krawędź peronu wzdłuż toru, 3. – szerokość (w bok).</summary>
    readonly List<Point> platPts = new();

    bool PlanTools => panel.PlanView && !dispMode;

    Point PlanPick(Point w) { var (p, k, _) = SnapTarget(w); return k == "grid" ? Snap(w) : p; }

    void PlanPlatformClick(Point w)
    {
        var p = Snap(w);      // krawędź peronu leży ok. 1,7 m od osi toru – bez łapania toru (siatka, prowadnice)
        if (platPts.Count < 2)
        {
            if (platPts.Count == 1 && (p - platPts[0]).Length * panel.Scale < 4) return;
            platPts.Add(p);
            status.Text = platPts.Count == 1 ? "PERON: kliknij koniec krawędzi peronu (wzdłuż toru – np. drugi koniec krawędzi przy torze). PPM / Esc – rezygnacja"
                                             : $"PERON: długość {doc.PlanLen(platPts[0].X, platPts[0].Y, platPts[1].X, platPts[1].Y):0.#} m – przesuń w bok na szerokość peronu i kliknij";
            return;
        }
        var (o, len, wid, rot) = PlatGeom(platPts[0], platPts[1], w);
        platPts.Clear();
        if (len < 0.5 || wid < 0.05) { status.Text = "✗ Peron za mały – zacznij od nowa"; return; }
        var r = Ask("Peron", ("Nazwa", "Peron "));
        if (r == null) return;
        doc.Snapshot();
        doc.Platforms.Add(new JsonObject { ["name"] = r[0], ["x"] = Math.Round(o.X, 2), ["y"] = Math.Round(o.Y, 2), ["w"] = Math.Round(len, 2), ["h"] = Math.Round(wid, 2), ["rot"] = Math.Round(rot, 3) });
        Changed($"Dodano peron {r[0]} – {len / doc.Ux:0.#} × {wid / doc.Uy:0.##} m");
        if (!System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift) && !FuzzMode) SetTool(Tool.Select);
    }

    /// <summary>Prostokąt peronu z krawędzi a–b i punktu szerokości c: narożnik, długość, szerokość (j. planu), obrót [°].</summary>
    static (Point O, double Len, double Wid, double Rot) PlatGeom(Point a, Point b, Point c)
    {
        var d = b - a; double len = d.Length; if (len < 1e-9) return (a, 0, 0, 0);
        var u = d / len; var n = new Vector(-u.Y, u.X);
        double off = Vector.Multiply(c - a, n);
        var o = off >= 0 ? a : a + n * off;
        return (o, len, Math.Abs(off), Math.Atan2(u.Y, u.X) * 180 / Math.PI);
    }

    void DrawPlanPlatformPreview(DrawingContext dc, double scale)
    {
        if (tool != Tool.Platform || !PlanTools || platPts.Count == 0) return;
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(0x00, 0xa0, 0xff)), 2 / scale);
        if (platPts.Count == 1) { dc.DrawLine(pen, platPts[0], Snap(mouse)); return; }
        var (o, len, wid, rot) = PlatGeom(platPts[0], platPts[1], mouse);
        var pd = new PlatformDef { X = o.X, Y = o.Y, W = len, H = wid, Rot = rot };
        PlatOutline(dc, pen, pd, 0);
        var ft = Ft($"{len / doc.Ux:0.#} × {wid / doc.Uy:0.##} m", Math.Clamp(9 / scale, 0.05, 30), Brushes.DeepSkyBlue);
        dc.DrawText(ft, new Point(mouse.X + 10 / scale, mouse.Y + 6 / scale));
    }

    /// <summary>Obrys peronu (obrócony), z marginesem m [px ekranu].</summary>
    void PlatOutline(DrawingContext dc, Pen pen, PlatformDef p, double m = 3)
    {
        double g = m / Math.Max(0.004, panel.Scale);
        var q = new PlatformDef { X = p.X, Y = p.Y, W = p.W + 2 * g, H = p.H + 2 * g, Rot = p.Rot };
        double r = p.Rot * Math.PI / 180; q.X -= g * Math.Cos(r) - g * Math.Sin(r); q.Y -= g * Math.Sin(r) + g * Math.Cos(r);
        var c = q.Corners(); var geo = new StreamGeometry();
        using (var x = geo.Open()) { x.BeginFigure(c[0], false, true); x.LineTo(c[1], true, false); x.LineTo(c[2], true, false); x.LineTo(c[3], true, false); }
        geo.Freeze(); dc.DrawGeometry(null, pen, geo);
    }

    // ------------------------------------------------------------------ rozjazd na planie
    /// <summary>
    /// Rozjazd na planie: klik na torze (miejsce zwrotnicy – w środku toru tor się dzieli), potem klik na drugim torze –
    /// łącznik prosty między torami ze zwrotnicami na obu końcach, albo klik w wolnym miejscu / na końcu toru – odgałęzienie
    /// prostym torem do tego punktu. Kąt dowolny (jak na zdjęciu), bez ukosu–pionu–ukosu pulpitu.
    /// </summary>
    void PlanTurnoutClick(Point w)
    {
        var (p, k, id) = SnapTarget(w); if (k == "grid") p = Snap(w);
        if (turnFrom == null)
        {
            string n0 = null;
            if (k == "node" && doc.EdgesAt(id).Count == 2 && doc.IsPlainNode(id)) n0 = id;
            else if (k == "edge") { doc.Snapshot(); n0 = doc.SplitEdge(id, p.X, p.Y, Post); Rebuild(); }
            if (n0 == null) { status.Text = "ROZJAZD: kliknij tor w miejscu zwrotnicy (początek rozjazdu – iglica)"; return; }
            turnFrom = n0;
            status.Text = $"ROZJAZD: zwrotnica w {n0} – kliknij drugi tor (łącznik ze zwrotnicą po drugiej stronie) albo wolne miejsce / koniec toru (odgałęzienie). PPM – rezygnacja";
            return;
        }
        var a = turnFrom; turnFrom = null;
        if (!doc.Nodes.ContainsKey(a)) return;
        var own = doc.EdgesAt(a).Select(e => StationDoc.Sv(e["id"])).ToHashSet();
        doc.Snapshot();
        string b; bool sw2 = false;
        if (k == "node" && id != a)
        {
            int c = doc.EdgesAt(id).Count;
            if (c >= 3 || (c == 2 && !doc.IsPlainNode(id))) { doc.Undo(); Rebuild(); status.Text = "✗ W tym węźle jest już zwrotnica albo inny obiekt – wskaż miejsce na torze"; return; }
            b = id; sw2 = c == 2;
        }
        else if (k == "edge" && !own.Contains(id)) { b = doc.SplitEdge(id, p.X, p.Y, Post); sw2 = true; }
        else if (k == "grid") b = doc.AddNode(Post, p.X, p.Y);
        else { doc.Undo(); Rebuild(); status.Text = "✗ Drugi koniec na tym samym torze – wskaż inny tor albo wolne miejsce"; return; }
        if (doc.PlanLen(doc.Pos(a).X, doc.Pos(a).Y, doc.Pos(b).X, doc.Pos(b).Y) < 1) { doc.Undo(); Rebuild(); status.Text = "✗ Za blisko zwrotnicy"; return; }
        try
        {
            doc.AddEdge(a, b, null, Post);
            var n1 = NextSwitchNum(); doc.AddSwitch(a, n1, Post, true);
            string msg = $"Rozjazd: zwrotnica {n1}";
            if (sw2) { var n2 = NextSwitchNum(); doc.AddSwitch(b, n2, Post, true); doc.CrossoverSecs($"{Post}:{n2}"); msg = $"Łącznik rozjazdowy: zwrotnice {n1} i {n2}"; }
            else msg += ", odgałęzienie do " + b;
            Changed(msg + $" ({doc.PlanLen(doc.Pos(a).X, doc.Pos(a).Y, doc.Pos(b).X, doc.Pos(b).Y):0.#} m)");
        }
        catch (InvalidOperationException ex) { doc.Undo(); Rebuild(); status.Text = "✗ " + ex.Message; }
    }

    void DrawPlanTurnoutPreview(DrawingContext dc, double scale)
    {
        if (tool != Tool.Turnout || !PlanTools || turnFrom == null || !doc.Nodes.ContainsKey(turnFrom)) return;
        var a = doc.Pos(turnFrom); var b = PlanPick(mouse);
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(0x00, 0xa0, 0xff)), 2 / scale) { DashStyle = DashStyles.Dash }, new Point(a.X, a.Y), b);
    }

    // ------------------------------------------------------------------ sygnalizator przy torze (plan)
    /// <summary>Pierwszy klik narzędziem Semafor na planie: miejsce na torze (punkt, węzeł / tor).</summary>
    (Point P, string Kind, string Id)? sigAt;

    /// <summary>Kierunek toru A→B w punkcie (wektor jednostkowy na rysunku planu).</summary>
    Vector TrackTangentAB((Point P, string Kind, string Id) at)
    {
        if (at.Kind == "node")
        {
            foreach (var e in doc.EdgesAt(at.Id))
            {
                var p = doc.EdgePts(StationDoc.Sv(e["id"])).Select(q => new Point(q.X, q.Y)).ToList(); int d = doc.EdgeDir(e, at.Id);
                if (StationDoc.Sv(e["a"]) != at.Id) p.Reverse();
                for (int i = 1; i < p.Count; i++) { var v = p[i] - p[0]; if (v.Length > 1e-6) { v.Normalize(); return v * d; } }
            }
            return new Vector(1, 0);
        }
        var pts = doc.EdgePts(at.Id).Select(q => new Point(q.X, q.Y)).ToList(); int best = 0; double bd = double.MaxValue;
        for (int i = 0; i < pts.Count - 1; i++) { var dd = DistSeg(at.P, (pts[i].X, pts[i].Y), (pts[i + 1].X, pts[i + 1].Y)); if (dd < bd) { bd = dd; best = i; } }
        var t = pts[best + 1] - pts[best]; if (t.Length < 1e-9) t = new Vector(1, 0); t.Normalize(); return t;
    }

    /// <summary>
    /// Kierunek obowiązywania (±1 wg A→B toru) i strona z miejsca drugiego kliknięcia: sygnalizator stoi po prawej stronie toru
    /// patrząc w kierunku jazdy, dla której obowiązuje (Ctrl – po lewej).
    /// </summary>
    (int Dir, string Side, Vector Travel) SigFacing((Point P, string Kind, string Id) at, Point w, bool left)
    {
        var t = TrackTangentAB(at); var c = w - at.P;
        bool rightOfAB = t.X * c.Y - t.Y * c.X > 0;        // oś Y w dół: prawa strona kierunku (1, 0) to dół
        int dir = rightOfAB ^ left ? 1 : -1;
        return (dir, left ? "L" : "P", t * dir);
    }

    void PlanSignalClick(Point w)
    {
        bool left = System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control);
        if (sigAt == null)
        {
            var (p, k, id) = SnapTarget(w);
            if (k == "grid") { status.Text = "SEMAFOR: kliknij tor w miejscu sygnalizatora"; return; }
            if (k == "node" && doc.EdgesAt(id).Count > 2) { status.Text = "✗ Sygnalizator nie może stać w węźle zwrotnicy – kliknij tor obok"; return; }
            sigAt = (p, k, id);
            status.Text = "SEMAFOR: kliknij po tej stronie toru, po której ma stać – obowiązuje dla jazdy, przy której jest po prawej (strzałka). Ctrl – sygnalizator po lewej stronie toru. PPM – rezygnacja";
            return;
        }
        var at = sigAt.Value; sigAt = null;
        var (dir, side, _) = SigFacing(at, w, left);
        var r = Ask("Nowy sygnalizator", ("Nazwa (np. S1, Tm12, A; dla PK – np. PK1)", ""), ("Typ", "wyjazdowy|wyjazdowy bez funkcji manewrowej|wjazdowy|tm|to|sp|pk – punkt końcowy przebiegu"),
            ("Powtarzany semafor / sygnalizator SBL / szlak (dla to / sp)", ""), ("Przebiegi kończące się w PK (dla pk)", "pociągowe i manewrowe|pociągowe|manewrowe"));
        if (r == null || r[0].Trim() == "") return;
        doc.Snapshot();
        try
        {
            var nid = at.Kind == "node" ? at.Id : doc.SplitEdge(at.Id, at.P.X, at.P.Y, Post);
            bool noMs = r[1] == "wyjazdowy bez funkcji manewrowej"; bool pk = r[1].StartsWith("pk");
            var key = doc.AddSignal(nid, r[0].Trim(), noMs ? "wyjazdowy" : pk ? "pk" : r[1], dir, Post, null, pk ? null : r[2].Trim());
            if (noMs) doc.Signals[key]["shunt"] = false;
            if (pk) doc.Signals[key]["routes"] = r.Length > 3 ? (r[3].StartsWith("pociągowe i") ? "PM" : r[3].StartsWith("poc") ? "P" : "M") : "PM";
            if (side == "L") doc.Signals[key]["side"] = "L";
            Changed($"Dodano {r[1]} {r[0]} – obowiązuje dla jazdy {(dir > 0 ? "zgodnie z" : "przeciwnie do")} kierunku toru A→B, {(side == "L" ? "po lewej" : "po prawej")} stronie"); Select("sig", key);
        }
        catch (InvalidOperationException ex) { doc.Undo(); Rebuild(); status.Text = "✗ " + ex.Message; }
    }

    void DrawPlanSignalPreview(DrawingContext dc, double scale)
    {
        if (tool != Tool.Signal || !PlanTools || sigAt is not { } at) return;
        bool left = System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control);
        var (dir, side, tr) = SigFacing(at, mouse, left);
        var br = new SolidColorBrush(Color.FromRgb(0x00, 0xa0, 0xff)); var pen = new Pen(br, 2 / scale);
        var rgt = new Vector(-tr.Y, tr.X); if (side == "L") rgt = -rgt;
        double m = 14 / scale; var b0 = at.P + rgt * m; var tip = b0 + tr * 3 * m;
        dc.DrawLine(pen, at.P, b0); dc.DrawLine(pen, b0, tip);
        var nrm = rgt * 0.5 * m; var bk = tip - tr * 0.8 * m;
        var g = new StreamGeometry(); using (var x = g.Open()) { x.BeginFigure(tip, true, true); x.LineTo(bk + nrm, true, false); x.LineTo(bk - nrm, true, false); }
        g.Freeze(); dc.DrawGeometry(br, null, g);
        var ft = Ft($"obowiązuje dla jazdy {(dir > 0 ? "A→B" : "B→A")} toru, {(side == "L" ? "po lewej" : "po prawej")}", Math.Clamp(9 / scale, 0.05, 30), br);
        dc.DrawText(ft, new Point(tip.X + 6 / scale, tip.Y - ft.Height / 2));
    }

    /// <summary>Paski narzędzi: na planie – układ torowy (bez narzędzi obrazów pulpitu), na zobrazowaniu – tylko obraz.</summary>
    void ApplyModeToolbars()
    {
        foreach (var el in planOnly) el.Visibility = dispMode ? Visibility.Collapsed : Visibility.Visible;
        foreach (var el in dispOnly) el.Visibility = dispMode ? Visibility.Visible : Visibility.Collapsed;
        if (toolBtns.TryGetValue(Tool.Platform, out var pb)) pb.ToolTip = (dispMode ? "Peron na obrazie pulpitu: przeciągnij prostokąt." : "Peron na planie: klik – początek krawędzi peronu, klik – koniec (wzdłuż toru, także skośnego), klik – szerokość w bok. Przyciąganie do siatki i prowadnic (nie do osi toru).") + "  [P]";
        ApplyUtToolbars();
        if (toolBtns.TryGetValue(Tool.Turnout, out var tb) && !Schem) tb.ToolTip = "Rozjazd na planie (jak w terenie): klik na torze w miejscu zwrotnicy, potem klik na drugim torze – prosty łącznik ze zwrotnicami na obu końcach, albo klik w wolnym miejscu / na końcu toru – odgałęzienie. Kąt dowolny, tory mogą być skośne (rysowanie po zdjęciu).  [R]";
    }
    readonly List<UIElement> planOnly = new(), dispOnly = new();

    /// <summary>Test (--editshot … plantools out.json): peron wzdłuż skośnego toru, łącznik i odgałęzienie na torach skośnych, (+)/(−) zwrotnic, skala bez zmian.</summary>
    public string PlanToolsForTest(string outJson)
    {
        var log = new System.Text.StringBuilder(); FuzzMode = true; int bad = 0;
        void Ok(bool c, string m) { log.AppendLine((c ? "OK  " : "BŁĄD") + " " + m); if (!c) bad++; }
        // na Twoim układzie (tylko odczyt): podział (+)/(−) zwrotnic taki sam jak dawniej (nachylenie względem poziomu)
        var real = System.IO.Path.Combine(AppContext.BaseDirectory, "Data", "station.json");
        if (System.IO.File.Exists(real))
        {
            var rs = Station.Load(real); int diff = 0, tot = 0;
            foreach (var sw in rs.Switches.Values) { tot++; var n = rs.Nodes[sw.Node]; var legs = n.Edges.Where(x => x != sw.Tip).OrderBy(x => rs.LegSlope(x, n)).ToList(); if (legs[1] != sw.Minus) diff++; }
            Ok(diff == 0, $"Data/station.json: odgałęzienie (−) zwrotnic jak dawniej – różnic {diff} z {tot}");
        }
        doc = StationDoc.New("TT", "Narzędzia planu (test)"); path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lcs_plantools.json"); dispMode = false; Rebuild(true);
        PlanForTest(); chkSnap.IsChecked = false; ApplyModeToolbars();
        double ux = doc.Ux, uy = doc.Uy; var scale0 = (doc.ScaleAlong, doc.ScaleAcross);
        // dwa tory równoległe pochylone o 5° (jak na obróconym zdjęciu), rozstaw 4,75 m
        double ang = 5 * Math.PI / 180; Vector U = new(Math.Cos(ang), Math.Sin(ang)), N = new(-Math.Sin(ang), Math.Cos(ang));
        Point P(double s, double off) => new Point(0, 0) + U * (s * ux) + N * (off * uy);
        var a0 = doc.AddNode("TT", P(0, 0).X, P(0, 0).Y); var a1 = doc.AddNode("TT", P(800, 0).X, P(800, 0).Y);
        var b0 = doc.AddNode("TT", P(0, 4.75).X, P(0, 4.75).Y); var b1 = doc.AddNode("TT", P(800, 4.75).X, P(800, 4.75).Y);
        var ta = doc.AddEdge(a0, a1, null, "TT"); var tb = doc.AddEdge(b0, b1, null, "TT"); Rebuild(); panel.FitAll();
        // peron 200 × 6 m przy torze b (po drugiej stronie niż tor a)
        SetTool(Tool.Platform); NoPrompt = true;
        PlanPlatformClick(P(300, 6.5)); PlanPlatformClick(P(500, 6.5));
        var (o, len, wid, rot) = PlatGeom(platPts[0], platPts[1], P(400, 12.5));
        Ok(Math.Abs(len / ux - 200) < 0.05 && Math.Abs(wid / uy - 6) < 0.05 && Math.Abs(rot - 5) < 0.01, $"peron wzdłuż toru: {len / ux:0.#} × {wid / uy:0.##} m, obrót {rot:0.##}°");
        var pd = new PlatformDef { X = o.X, Y = o.Y, W = len, H = wid, Rot = rot };
        Ok(pd.Contains(P(400, 9.5)) && !pd.Contains(P(400, 4.75)) && !pd.Contains(P(250, 9.5)), "peron obejmuje pas przy torze, nie obejmuje toru");
        doc.Snapshot(); doc.Platforms.Add(new JsonObject { ["name"] = "Peron 1", ["x"] = o.X, ["y"] = o.Y, ["w"] = len, ["h"] = wid, ["rot"] = rot }); platPts.Clear(); Changed();
        Ok(st.Platforms.Count == 1 && Math.Abs(st.Platforms[0].Rot - 5) < 0.01 && PickPlatform(P(400, 9.5)) == 0 && PickPlatform(P(400, 2)) < 0, "peron w modelu (obrót), wskazywanie myszą w obróconym prostokącie");
        // łącznik: z toru a (s = 100) na tor b (s = 145) – 1:9,5
        SetTool(Tool.Turnout);
        PlanTurnoutClick(P(100, 0)); var swA = turnFrom; PlanTurnoutClick(P(145, 4.75));
        Ok(doc.Switches.Count == 2, $"łącznik na torach skośnych: {doc.Switches.Count} zwrotnice");
        Rebuild();
        foreach (var sw in st.Switches.Values)
        {
            var me = st.Edges[sw.Minus]; bool cross = st.Nodes[me.A].Switch != null && st.Nodes[me.B].Switch != null;
            Ok(cross, $"zwrotnica {sw.Label}: odgałęzienie (−) to łącznik ({sw.Minus}), (+) – tor zasadniczy");
        }
        // odgałęzienie z toru a w dół-w prawo (prawie poziomo przy torze nachylonym 5°: 0° względem poziomu – dawniej uznane za tor na wprost)
        var nb = doc.Nodes.Count;
        var br0 = P(400, 0); PlanTurnoutClick(br0);
        var end = new Point(br0.X + 200 * ux, br0.Y); PlanTurnoutClick(end);
        Rebuild();
        var bsw = st.Switches.Values.First(s => s.Label == "3");
        Ok(st.Edges[bsw.Minus].Pts.All(q => Math.Abs(q.Y - br0.Y) < 0.1), "odgałęzienie poziome od toru nachylonego 5°: (−) to odgałęzienie, nie tor zasadniczy");
        Ok(new LcsWidzew.Sim.Simulator(doc.Build()) != null, "model z rozjazdami skośnymi wczytuje się do symulatora");
        Ok((doc.ScaleAlong, doc.ScaleAcross) == scale0, "skala planu bez zmian");
        // paski: na planie bez „Obraz” / „Nowy obraz”, na zobrazowaniu bez narzędzi układu
        Ok(planOnly.All(x => x.Visibility == Visibility.Visible) && dispOnly.All(x => x.Visibility == Visibility.Collapsed), $"plan: {planOnly.Count} elementów układu widocznych, {dispOnly.Count} elementów obrazów ukrytych");
        doc.Save(outJson);
        Ok(Station.Load(outJson).Platforms[0].Rot != 0, "zapis i wczytanie obrotu peronu");
        SetTool(Tool.Select); ZoomForTest(P(300, 2).X, P(300, 2).Y, panel.ActualWidth / (420 * ux));
        log.AppendLine($"błędów testu: {bad}, błędów edytora: {Errors.Count}");
        return log.ToString();
    }
}

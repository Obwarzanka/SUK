using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LcsWidzew.Model;

namespace LcsWidzew.UI;

/// <summary>
/// Tryb „Zobrazowanie” edytora: obrazy pulpitu (EbiScreen) niezależne od układu torowego. Przesuwanie węzłów, punktów
/// załamania i grup nie zmienia połączeń ani długości – zmienia tylko to, co widzi dyżurny. Tory układu nieumieszczone
/// na zobrazowaniu nie są widoczne na pulpicie, dopóki użytkownik ich nie umieści.
/// </summary>
public partial class EditorWindow
{
    bool dispMode;
    readonly HashSet<string> dSel = new();             // zaznaczone węzły zobrazowania
    readonly HashSet<int> dSelLabels = new(), dSelPlats = new();
    // przeciąganie
    bool dDrag; Point dStart; bool dMoved;
    (string Edge, int Idx)? dPoint;                    // przeciągany punkt załamania toru
    Dictionary<string, (double X, double Y)> dNodeOrig;
    Dictionary<string, List<(double X, double Y)>> dEdgeOrig;
    Dictionary<int, (double X, double Y)> dLabelOrig, dPlatOrig;
    Point? dBand; Point dBandEnd;
    ListBox dHiddenList;

    Station DS => st?.Display;
    /// <summary>Geometria edytowana w bieżącym trybie: zobrazowanie albo plan schematyczny (układ).</summary>
    Station V => dispMode && DS != null ? DS : st;
    readonly HashSet<int> dSelCross = new();
    Dictionary<int, (double X, double Y0, double Y1)> dCrossOrig;
    static bool DecoTool(Tool t) => t is Tool.Label or Tool.Platform or Tool.Crossing;
    void DClearSel() { dSel.Clear(); dSelLabels.Clear(); dSelPlats.Clear(); dSelCross.Clear(); if (selKind is "label" or "platform" or "crossing") { selKind = null; selIdx = -1; } }
    int DSelCount => dSel.Count + dSelLabels.Count + dSelPlats.Count + dSelCross.Count;
    int PickCrossing(Point w)
    {
        for (int i = 0; i < V.Crossings.Count; i++) { var c = V.Crossings[i]; if (Math.Abs(w.X - c.X) < 6 && w.Y > c.Y0 - 16 && w.Y < c.Y1 + 16) return i; }
        return -1;
    }
    /// <summary>Pojedynczy napis / peron / przejazd zaznaczony na obrazie – panel właściwości jak w planie.</summary>
    void DSyncSingle()
    {
        selKind = null; selIdx = -1;
        if (dSel.Count > 0 || DSelCount != 1) return;
        if (dSelLabels.Count == 1) { selKind = "label"; selIdx = dSelLabels.First(); }
        else if (dSelPlats.Count == 1) { selKind = "platform"; selIdx = dSelPlats.First(); }
        else if (dSelCross.Count == 1) { selKind = "crossing"; selIdx = dSelCross.First(); }
    }
    double PickR => 9 / Math.Max(0.05, panel.Scale);

    /// <summary>Przełączenie trybu edytora: układ torowy ⇄ zobrazowanie.</summary>
    void ToggleDisplayMode()
    {
        if (!dispMode)
        {
            bool created = doc.Disp == null;
            if (created) { doc.Snapshot(); doc.EnsureDisplay(); dirty = true; }
            dispMode = true; DClearSel(); doc.DecoOnDisplay = true; doc.EnsureDisplayDeco();
            SetTool(Tool.Select); selKind = null; selId = null; ClearMulti();
            if (st != null && pageBox.SelectedIndex >= st.Pages.Count) pageBox.SelectedIndex = 0;   // zobrazowanie – obraz po obrazie
            RebuildCore(true);
            status.Text = created ? "Utworzono zobrazowanie z obecnego rysunku (jednorazowo). Od teraz obraz pulpitu i układ torowy edytuje się osobno."
                                  : "Tryb ZOBRAZOWANIE – przesuwasz elementy obrazu pulpitu; połączenia i długości (układ torowy) się nie zmieniają.";
        }
        else
        {
            dispMode = false; DClearSel(); doc.DecoOnDisplay = false;
            pageBox.SelectedIndex = pageBox.Items.Count - 1;          // plan – cały węzeł na jednym arkuszu
            RebuildCore(true);
            status.Text = "PLAN SCHEMATYCZNY (układ torowy) – cały węzeł: połączenia, zwrotnice, sygnalizatory, odcinki, szlaki. Na tym liczy komputer zależności; na pulpit trafia zobrazowanie.";
        }
        panel.PlanView = !dispMode;
        modeBtn.Content = dispMode ? "Tryb: ZOBRAZOWANIE (obrazy pulpitu)  ⇄" : "Tryb: PLAN SCHEMATYCZNY (układ torowy)  ⇄";
        modeBtn.Background = dispMode ? new SolidColorBrush(Color.FromRgb(0x5a, 0x3a, 0x10)) : new SolidColorBrush(Color.FromRgb(0x1f, 0x3d, 0x5a));
        // w zobrazowaniu – tylko narzędzia obrazu: wskaż, napis, peron, przejazd (kopie pulpitu, niezależne od planu)
        foreach (var b in toolButtons) b.Visibility = dispMode && !toolBtns.Any(x => x.Value == b && (x.Key == Tool.Select || DecoTool(x.Key))) ? Visibility.Collapsed : Visibility.Visible;
        planBtn.Visibility = dispMode ? Visibility.Collapsed : Visibility.Visible;
        autoBtn.Visibility = planBtn.Visibility;
        ShowProps();
        panel.InvalidateVisual();
    }

    public string PlanSkewForTest()
    {
        doc.EnsureDisplay();
        var disp0 = doc.Disp.ToJsonString();
        int r0 = new LcsWidzew.Sim.Simulator(doc.Build()).RouteTable.Values.Sum(l => l.Count);
        doc.Snapshot(); int k = doc.PlanSkewAll(3); RebuildCore();
        int r1 = new LcsWidzew.Sim.Simulator(doc.Build()).RouteTable.Values.Sum(l => l.Count);
        PlanForTest();
        return $"skosy: {k}; przebiegi {r0} -> {r1}; obraz pulpitu bez zmian: {doc.Disp.ToJsonString() == disp0}; problemy: {doc.Problems().Count}" + System.Environment.NewLine + string.Join(System.Environment.NewLine, doc.Problems().Select(p => p.Msg).Take(12));
    }

    public string SplitMergeForTest()
    {
        if (!dispMode) ToggleDisplayMode();
        int r0 = new LcsWidzew.Sim.Simulator(doc.Build()).RouteTable.Values.Sum(l => l.Count);
        var e = DS.Edges.Values.First(x => !x.Link && x.Pts.Count == 2 && x.Pts[0].X > Station.Hidden + 1 && Math.Abs(x.Pts[0].X - x.Pts[1].X) > 60 && OnPage(x.Pts[0].X));
        var mid = new Point((e.Pts[0].X + e.Pts[1].X) / 2, (e.Pts[0].Y + e.Pts[1].Y) / 2);
        int n0 = doc.Nodes.Count;
        trackOp = "split"; TrackOpDown(mid);
        var nn = doc.Nodes.Select(x => x.Key).Except(DS.Nodes.Keys.Where(k => false)).Last();
        var newNode = doc.Nodes.Select(x => x.Key).FirstOrDefault(k => doc.DPos(k) is { } dp && Math.Abs(dp.X - mid.X) < 2 && Math.Abs(dp.Y - mid.Y) < 2);
        string log = $"podział {e.Id}: węzły {n0} -> {doc.Nodes.Count}, nowy węzeł na obrazie w miejscu kliknięcia: {newNode}; ";
        trackOp = "merge"; if (newNode != null) TrackOpDown(new Point(doc.DPos(newNode).Value.X, doc.DPos(newNode).Value.Y));
        int r1 = new LcsWidzew.Sim.Simulator(doc.Build()).RouteTable.Values.Sum(l => l.Count);
        return log + $"scalenie: węzły {doc.Nodes.Count}; przebiegi {r0} -> {r1}; status: {status.Text}";
    }

    /// <summary>Test (--editshot … deco out.json): napisy, perony i przejazdy – osobne kopie zobrazowania i planu.</summary>
    public string DecoForTest(string outJson)
    {
        var log = new System.Text.StringBuilder(); FuzzMode = true;
        string Arr(string k) => doc.Root[k].ToJsonString();
        string DArr(string k) => doc.Disp?[k]?.ToJsonString() ?? "brak";
        void Ok(string what, bool cond) => log.AppendLine($"{(cond ? "OK " : "BŁĄD")} {what}");
        if (!dispMode) ToggleDisplayMode();
        Ok($"kopie w zobrazowaniu po wczytaniu (napisy {doc.Disp["labels"]?.AsArray().Count}, perony {doc.Disp["platforms"]?.AsArray().Count}, przejazdy {doc.Disp["crossings"]?.AsArray().Count})",
            DArr("labels") == Arr("labels") && DArr("platforms") == Arr("platforms") && DArr("crossings") == Arr("crossings"));
        string planL = Arr("labels"), planP = Arr("platforms"), planC = Arr("crossings");
        // 1. przesunięcie napisu, peronu i przejazdu na obrazie
        var onPage = Enumerable.Range(0, DS.Labels.Count).FirstOrDefault(i => OnPage(DS.Labels[i].X), -1);
        if (onPage >= 0) { DClearSel(); dSelLabels.Add(onPage); }
        if (DS.Platforms.Count > 0) dSelPlats.Add(0);
        if (DS.Crossings.Count > 0) dSelCross.Add(0);
        DBeginDrag(new Point(), null); dDrag = false; DApplyDelta(40, 24); DCommit("test");
        Ok("przesunięcie na obrazie zmienia kopię pulpitu", DArr("labels") != planL || DArr("platforms") != planP);
        Ok("przesunięcie na obrazie nie rusza planu", Arr("labels") == planL && Arr("platforms") == planP && Arr("crossings") == planC);
        var b1 = doc.Build();
        Ok("pulpit (Display) rysuje kopie zobrazowania, plan – swoje",
            b1.Display.Platforms.Count > 0 && b1.Platforms.Count > 0 && (b1.Display.Platforms[0].X != b1.Platforms[0].X || b1.Display.Platforms[0].Y != b1.Platforms[0].Y));
        // 2. nowy napis, peron i przejazd narzędziami w zobrazowaniu
        int dl = DS.Labels.Count, dp = DS.Platforms.Count, dc = DS.Crossings.Count;
        var pg = panel.Page; double x = pg.X0 + 200, y = 300;
        AutoAnswers.Enqueue(new[] { "TEST napis", "start" }); SetTool(Tool.Label); Down(new Point(x, y), null); Up(new Point(x, y), null);
        AutoAnswers.Enqueue(new[] { "Peron T" }); SetTool(Tool.Platform); Down(new Point(x, y + 40), null); Move(new Point(x + 120, y + 56), null); Up(new Point(x + 120, y + 56), null);
        AutoAnswers.Enqueue(new[] { "1.234", "Y" }); SetTool(Tool.Crossing); Down(new Point(x + 300, y), null); Move(new Point(x + 300, y + 80), null); Up(new Point(x + 300, y + 80), null);
        SetTool(Tool.Select);
        Ok($"dodane na obrazie: napisy {dl}->{DS.Labels.Count}, perony {dp}->{DS.Platforms.Count}, przejazdy {dc}->{DS.Crossings.Count}", DS.Labels.Count == dl + 1 && DS.Platforms.Count == dp + 1 && DS.Crossings.Count == dc + 1);
        Ok("dodanie na obrazie nie rusza planu", Arr("labels") == planL && Arr("platforms") == planP && Arr("crossings") == planC);
        // 3. zaznaczenie i usunięcie dodanego przejazdu (Del)
        var cr = DS.Crossings[^1]; DDown(new Point(cr.X, (cr.Y0 + cr.Y1) / 2), null); DUp(new Point(cr.X, (cr.Y0 + cr.Y1) / 2), null);
        Ok($"klik zaznacza przejazd (panel: {selKind})", dSelCross.Count == 1 && selKind == "crossing");
        DKey(Key.Delete);
        Ok("Del usuwa przejazd z pulpitu", DS.Crossings.Count == dc && Arr("crossings") == planC);
        // 4. plan schematyczny: przesunięcie napisu w planie nie rusza pulpitu
        ToggleDisplayMode();
        string dispL = DArr("labels");
        doc.Snapshot(); doc.Labels[0]["x"] = StationDoc.D(doc.Labels[0]["x"]) + 100; Changed("test");
        Ok("w planie edytowana kopia planu", Arr("labels") != planL);
        Ok("zmiana w planie nie rusza pulpitu", DArr("labels") == dispL);
        // 5. cofnij w planie, zapis i ponowne wczytanie
        DoUndo();
        Ok("Ctrl+Z przywraca napis planu", Arr("labels") == planL);
        doc.Save(outJson);
        var re = Station.Load(outJson);
        Ok("po zapisie i wczytaniu: pulpit ma kopie zobrazowania", re.Display.Labels.Count == dl + 1 && re.Labels.Count == st.Labels.Count);
        int r0 = new LcsWidzew.Sim.Simulator(re).RouteTable.Values.Sum(l => l.Count);
        log.AppendLine($"przebiegi po zapisie: {r0}; błędów edytora: {Errors.Count}");
        return log.ToString();
    }

    public void PlanForTest() { if (dispMode) ToggleDisplayMode(); else { pageBox.SelectedIndex = pageBox.Items.Count - 1; panel.PlanView = true; RebuildCore(true); } }

    /// <summary>Test (--editshot … display): konwersja, przesunięcie na obrazie, podział toru w układzie, kontrola przebiegów.</summary>
    public string DisplayForTest(string outJson)
    {
        var log = new System.Text.StringBuilder();
        int routes0 = new LcsWidzew.Sim.Simulator(doc.Build()).RouteTable.Values.Sum(l => l.Count);
        ToggleDisplayMode();
        log.AppendLine($"konwersja: display={doc.Disp != null}, węzły {doc.DispNodes.Count}, tory {doc.DispEdges.Count}");
        // przesunięcie wszystkich węzłów posterunku z pierwszego obrazu o 0, +60 (obraz), układ bez zmian
        var post = st.Pages[0].Posts.FirstOrDefault();
        var layoutBefore = doc.Root["nodes"].ToJsonString();
        foreach (var n in DS.Nodes.Values.Where(n => n.Post == post && n.X > Station.Hidden + 1)) dSel.Add(n.Id);
        DBeginDrag(new Point(), null); dDrag = false; DApplyDelta(0, 60); DCommit("test");
        log.AppendLine($"przesunięto na obrazie {dSel.Count} węzłów; układ niezmieniony: {doc.Root["nodes"].ToJsonString() == layoutBefore}");
        int routes1 = new LcsWidzew.Sim.Simulator(doc.Build()).RouteTable.Values.Sum(l => l.Count);
        log.AppendLine($"przebiegi przed {routes0}, po przesunięciu obrazu {routes1}");
        // tryb układu: podział toru (np. nowy sygnalizator) – obraz toru dzielony, nic nie znika
        ToggleDisplayMode();
        var e0 = doc.EdgeObjs.First(e => doc.OnDisplay(StationDoc.Sv(e["id"])) && StationDoc.Sv(e["sec"]).Contains(":JT"));
        var (ax, ay) = doc.Pos(StationDoc.Sv(e0["a"])); var (bx, by) = doc.Pos(StationDoc.Sv(e0["b"]));
        doc.Snapshot(); var nn = doc.SplitEdge(StationDoc.Sv(e0["id"]), (ax + bx) / 2, (ay + by) / 2, null); Changed("test");
        var halves = doc.EdgesAt(nn).Select(e => StationDoc.Sv(e["id"])).ToList();
        log.AppendLine($"podział toru {StationDoc.Sv(e0["id"])} w układzie: węzeł {nn} na obrazie {doc.DPos(nn) != null}, połówki na obrazie {halves.Count(doc.OnDisplay)}/2");
        doc.MergeNode(nn); Changed("test");
        log.AppendLine($"scalenie z powrotem: tor na obrazie {doc.OnDisplay(StationDoc.Sv(doc.EdgesAt(StationDoc.Sv(e0["a"])).First(e => StationDoc.Sv(e["sec"]) == StationDoc.Sv(e0["sec"]))["id"]))}");
        // nowy tor w układzie – niewidoczny na obrazie do umieszczenia
        var x1 = doc.AddNode(post, 99000, 99000); var x2 = doc.AddNode(post, 99100, 99000); var ne = doc.AddEdge(x1, x2, null, post); Changed("test");
        log.AppendLine($"nowy tor w układzie na obrazie: {doc.OnDisplay(ne)}; pulpit X węzła: {st.Display.Nodes[x1].X}");
        doc.PlaceFromLayout(ne); Changed("test");
        log.AppendLine($"po umieszczeniu: {doc.OnDisplay(ne)}");
        doc.DeleteNode(x1); doc.DeleteNode(x2); Changed("test");
        doc.Save(outJson);
        log.AppendLine("walidacja: " + string.Join(" | ", doc.Validate().Take(3)));
        return log.ToString();
    }

    /// <summary>Test losowy trybu zobrazowania (--editshot plik dfuzz N seed): przeciąganie węzłów i punktów załamania,
    /// zaznaczanie prostokątem, strzałki, usuwanie z obrazu, umieszczanie, podział i scalanie torów, cofanie.
    /// Sprawdza: końce torów obrazu w węzłach obrazu, tor na obrazie ⇒ jego węzły na obrazie, operacje obrazu nie zmieniają
    /// układu torowego ani tablicy przebiegów, cofnięcie przywraca stan sprzed operacji.</summary>
    public string DFuzz(int n, int seed)
    {
        var rnd = new Random(seed); var log = new System.Text.StringBuilder(); FuzzMode = true;
        if (!dispMode) ToggleDisplayMode();
        string Layout() { var r = (JsonObject)doc.Root.DeepClone(); r.Remove("display"); return r.ToJsonString(); }
        int Routes() => new LcsWidzew.Sim.Simulator(doc.Build()).RouteTable.Values.Sum(l => l.Count);
        string layout0 = Layout(); int routes0 = Routes();
        var counts = new Dictionary<string, int>();
        Point P() { var pg = panel.Page; return new Point(pg.X0 + rnd.NextDouble() * (pg.X1 - pg.X0), 150 + rnd.NextDouble() * 1000); }
        List<Node> Vis() => DS.Nodes.Values.Where(x => x.X > Station.Hidden + 1 && OnPage(x.X)).ToList();
        Point Near() { var ns = Vis(); if (ns.Count == 0 || rnd.Next(5) == 0) return P(); var q = ns[rnd.Next(ns.Count)]; return new Point(q.X + rnd.Next(-2, 3), q.Y + rnd.Next(-2, 3)); }
        Point OnEdge()
        {
            var es = DS.Edges.Values.Where(x => !x.Link && x.Pts.Count >= 2 && x.Pts[0].X > Station.Hidden + 1 && OnPage(x.Pts[0].X)).ToList();
            if (es.Count == 0) return P();
            var e = es[rnd.Next(es.Count)]; int i = rnd.Next(e.Pts.Count - 1); double t = 0.2 + rnd.NextDouble() * 0.6;
            return new Point(e.Pts[i].X + t * (e.Pts[i + 1].X - e.Pts[i].X), e.Pts[i].Y + t * (e.Pts[i + 1].Y - e.Pts[i].Y));
        }
        void Check(string op)
        {
            if (doc.Disp == null) { Errors.Add($"{op}: brak sekcji display"); return; }
            var layoutEdges = doc.EdgeObjs.ToDictionary(e => StationDoc.Sv(e["id"]));
            foreach (var (eid, _) in doc.DispEdges)
            {
                if (!layoutEdges.TryGetValue(eid, out var le)) { Errors.Add($"{op}: tor obrazu bez toru w układzie"); continue; }
                var pts = doc.DPts(eid);
                if (pts == null || pts.Count < 2) { Errors.Add($"{op}: tor obrazu z mniej niż 2 punktami"); continue; }
                string a = StationDoc.Sv(le["a"]), b = StationDoc.Sv(le["b"]);
                var pa = doc.DPos(a); var pb = doc.DPos(b);
                if (pa == null || pb == null) { Errors.Add($"{op}: tor na obrazie, a jego węzeł nie"); continue; }
                bool Same((double X, double Y) p, (double X, double Y) q) => Math.Abs(p.X - q.X) < 0.2 && Math.Abs(p.Y - q.Y) < 0.2;
                if (!Same(pts[0], pa.Value) || !Same(pts[^1], pb.Value)) Errors.Add($"{op}: koniec toru obrazu poza węzłem");
            }
        }
        for (int i = 0; i < n; i++)
        {
            int op = rnd.Next(17);
            string name = op switch { 0 or 1 => "przeciągnięcie węzła", 2 => "przeciągnięcie punktu załamania", 3 => "punkt załamania", 4 => "usunięcie punktu (PPM)", 5 => "prostokąt", 6 => "strzałki", 7 => "Del", 8 => "umieszczenie", 9 => "podział toru", 10 => "scalenie torów", 11 => "cofnij", 12 => "ponów", 14 => "przeciągnięcie napisu/peronu/przejazdu", 15 => "nowy napis/peron/przejazd", 16 => "Del napisu/peronu/przejazdu", _ => "obraz" };
            bool layoutOp = op is 9 or 10 or 11 or 12;
            string before = doc.Root.ToJsonString(); string layBefore = layoutOp ? null : Layout();
            try
            {
                switch (op)
                {
                    case 0: case 1: { var a = Near(); Down(a, null); var b = new Point(a.X + rnd.Next(-80, 80), a.Y + rnd.Next(-50, 50)); Move(b, null); Up(b, null); break; }
                    case 2:
                    {
                        var bends = DS.Edges.Values.Where(x => x.Pts.Count > 2 && OnPage(x.Pts[1].X)).ToList(); if (bends.Count == 0) break;
                        var e = bends[rnd.Next(bends.Count)]; var p = e.Pts[1 + rnd.Next(e.Pts.Count - 2)];
                        dSel.Add(e.A); var a = new Point(p.X, p.Y); Down(a, null); var b = new Point(a.X + rnd.Next(-60, 60), a.Y + rnd.Next(-40, 40)); Move(b, null); Up(b, null); break;
                    }
                    case 3: Safe(() => DAddBend(OnEdge())); break;
                    case 4:
                    {
                        var bends = DS.Edges.Values.Where(x => x.Pts.Count > 2 && OnPage(x.Pts[1].X)).ToList(); if (bends.Count == 0) break;
                        var e = bends[rnd.Next(bends.Count)]; var p = e.Pts[1 + rnd.Next(e.Pts.Count - 2)]; Right(new Point(p.X, p.Y)); break;
                    }
                    case 5: { var a = Near(); dBand = a; var b = new Point(a.X + rnd.Next(20, 400), a.Y + rnd.Next(20, 250)); dBandEnd = b; Up(b, null); break; }
                    case 6: Safe(() => DKey(new[] { Key.Left, Key.Right, Key.Up, Key.Down }[rnd.Next(4)])); break;
                    case 7: if (rnd.Next(3) == 0) Safe(() => DKey(Key.Delete)); break;
                    case 8:
                    {
                        var btns = props.Children.OfType<Button>().Where(b => (b.Content as string ?? "").StartsWith("Umieść")).ToList();
                        if (dHiddenList != null && dHiddenList.Items.Count > 0) dHiddenList.SelectedIndex = rnd.Next(dHiddenList.Items.Count);
                        if (btns.Count > 0) btns[rnd.Next(btns.Count)].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                        break;
                    }
                    case 9: trackOp = "split"; Safe(() => TrackOpDown(OnEdge())); trackOp = null; break;
                    case 10: trackOp = "merge"; Safe(() => TrackOpDown(Near())); trackOp = null; break;
                    case 11: Safe(DoUndo); break;
                    case 12: Safe(DoRedo); break;
                    case 13: if (st.Pages.Count > 0) pageBox.SelectedIndex = rnd.Next(st.Pages.Count); break;
                    case 14: case 16:
                    {
                        // klik na napisie, peronie albo przejeździe obrazu, potem przeciągnięcie albo Del
                        var pts = DS.Labels.Where(l => OnPage(l.X)).Select(l => new Point(l.X + 2, l.Y))
                            .Concat(DS.Platforms.Where(p => OnPage(p.X)).Select(p => new Point(p.X + p.W / 2, p.Y + p.H / 2)))
                            .Concat(DS.Crossings.Where(c => OnPage(c.X)).Select(c => new Point(c.X, (c.Y0 + c.Y1) / 2))).ToList();
                        if (pts.Count == 0) break;
                        var a = pts[rnd.Next(pts.Count)]; Down(a, null);
                        if (op == 14) { var b = new Point(a.X + rnd.Next(-60, 60), a.Y + rnd.Next(-40, 40)); Move(b, null); Up(b, null); }
                        else { Up(a, null); Safe(() => DKey(Key.Delete)); }
                        break;
                    }
                    case 15:
                    {
                        var t = new[] { Tool.Label, Tool.Platform, Tool.Crossing }[rnd.Next(3)]; SetTool(t);
                        AutoAnswers.Clear();
                        AutoAnswers.Enqueue(t switch { Tool.Label => new[] { "N" + rnd.Next(99), "start|middle|end".Split('|')[rnd.Next(3)] }, Tool.Platform => new[] { "Peron " + rnd.Next(9) }, _ => new[] { "km " + rnd.Next(99), "Y" } });
                        var a = P(); Down(a, null); var b = new Point(a.X + rnd.Next(-150, 150), a.Y + rnd.Next(-90, 90)); Move(b, null); Up(b, null);
                        if (rnd.Next(4) == 0) Right(b);
                        SetTool(Tool.Select);
                        break;
                    }
                }
            }
            catch (Exception ex) { Errors.Add($"NIEPRZECHWYCONY ({name}) " + ex.GetType().Name + ": " + ex.Message + " @ " + (ex.StackTrace ?? "").Split((char)10).FirstOrDefault(l => l.Contains("LcsWidzew"))?.Trim()); }
            bool changed = doc.Root.ToJsonString() != before;
            if (changed) counts[name] = counts.GetValueOrDefault(name) + 1;
            if (!dispMode) { Errors.Add($"{name}: edytor wyszedł z trybu zobrazowania"); ToggleDisplayMode(); }
            Check(name);
            if (!layoutOp && Layout() != layBefore) Errors.Add($"{name}: operacja na obrazie zmieniła układ torowy");
            // cofnięcie operacji obrazu ma przywrócić dokładnie stan sprzed niej
            if (!layoutOp && changed && rnd.Next(4) == 0)
            {
                Safe(DoUndo);
                if (doc.Root.ToJsonString() != before) Errors.Add($"{name}: Ctrl+Z nie przywrócił stanu sprzed operacji");
                Safe(DoRedo);
            }
            if (i % 5 == 0)
            {
                panel.InvalidateVisual(); panel.UpdateLayout();
                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(400, 300, 96, 96, PixelFormats.Pbgra32); rtb.Render(panel);
                if (panel.RenderError != null) Errors.Add("RYSOWANIE: " + panel.RenderError);
                try { var x = doc.Build(); if (Layout() == layout0 && Routes() != routes0) Errors.Add("przebiegi zmieniły się przy niezmienionym układzie"); } catch (Exception ex) { Errors.Add("MODEL: " + ex.Message); }
            }
        }
        log.AppendLine("Wykonane zmiany: " + string.Join(", ", counts.OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Value}")));
        foreach (var g in Errors.GroupBy(x => x)) log.AppendLine($"{g.Count(),4} × {g.Key}");
        log.AppendLine($"Koniec: węzły obrazu {doc.DispNodes.Count}, tory obrazu {doc.DispEdges.Count} z {doc.EdgeObjs.Count()}, błędów {Errors.Count}");
        return log.ToString();
    }

    // ------------------------------------------------------------------ trafienia
    string DPickNode(Point w)
    {
        if (DS == null) return null;
        string best = null; double bd = PickR;
        foreach (var n in DS.Nodes.Values)
        {
            if (n.X <= Station.Hidden + 1 || !OnPage(n.X)) continue;
            var d = Math.Sqrt(Math.Pow(n.X - w.X, 2) + Math.Pow(n.Y - w.Y, 2));
            if (d < bd) { bd = d; best = n.Id; }
        }
        return best;
    }

    (string Edge, int Idx)? DPickPoint(Point w)
    {
        if (DS == null) return null;
        foreach (var e in DS.Edges.Values)
            for (int i = 1; i < e.Pts.Count - 1; i++)
                if (Math.Abs(e.Pts[i].X - w.X) < PickR * 0.8 && Math.Abs(e.Pts[i].Y - w.Y) < PickR * 0.8) return (e.Id, i);
        return null;
    }

    (string Edge, int Seg)? DPickSeg(Point w)
    {
        if (DS == null) return null;
        foreach (var e in DS.Edges.Values)
        {
            if (e.Link || e.Pts[0].X <= Station.Hidden + 1) continue;
            for (int i = 0; i < e.Pts.Count - 1; i++)
            {
                var a = e.Pts[i]; var b = e.Pts[i + 1];
                double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
                double t = l2 == 0 ? 0 : Math.Clamp(((w.X - a.X) * dx + (w.Y - a.Y) * dy) / l2, 0, 1);
                if (Math.Sqrt(Math.Pow(a.X + t * dx - w.X, 2) + Math.Pow(a.Y + t * dy - w.Y, 2)) < PickR * 0.6) return (e.Id, i);
            }
        }
        return null;
    }

    // ------------------------------------------------------------------ mysz
    bool DDown(Point w, MouseButtonEventArgs e)
    {
        if (DS == null) return true;
        bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        // dwuklik na torze – nowy punkt załamania
        if (e?.ClickCount == 2 && DAddBend(w)) return true;
        var nid = DPickNode(w);
        if (nid != null)
        {
            if (shift) { if (!dSel.Remove(nid)) dSel.Add(nid); ShowProps(); panel.InvalidateVisual(); return true; }
            if (!dSel.Contains(nid)) { DClearSel(); dSel.Add(nid); ShowProps(); }
            DBeginDrag(w, null);
            return true;
        }
        if (DPickPoint(w) is { } pp) { DBeginDrag(w, pp); return true; }
        // napis, przejazd, peron (kopie zobrazowania)
        foreach (var (idx, set) in new[] { (PickLabel(w), dSelLabels), (PickCrossing(w), dSelCross), (PickPlatform(w), dSelPlats) })
        {
            if (idx < 0) continue;
            if (shift) { if (!set.Remove(idx)) set.Add(idx); DSyncSingle(); ShowProps(); panel.InvalidateVisual(); return true; }
            if (!set.Contains(idx)) { DClearSel(); set.Add(idx); DSyncSingle(); ShowProps(); }
            DBeginDrag(w, null); return true;
        }
        // puste miejsce: przeciąganie – przesuwanie widoku (jak w planie); Shift+przeciąganie – zaznaczanie prostokątem
        if (!shift)
        {
            if (DSelCount > 0) { DClearSel(); ShowProps(); panel.InvalidateVisual(); }
            if (!bandMode) return false;
        }
        dBand = w; dBandEnd = w;
        panel.InvalidateVisual();
        return true;
    }

    /// <summary>Nowy punkt załamania toru w klikniętym miejscu (dwuklik na torze).</summary>
    bool DAddBend(Point w)
    {
        if (DPickSeg(w) is not { } sg || DPickNode(w) != null) return false;
        var pts = doc.DPts(sg.Edge); if (pts == null) return true;
        doc.Snapshot(); var g = Snap(w); pts.Insert(sg.Seg + 1, (g.X, g.Y)); doc.SetDPts(sg.Edge, pts);
        Changed("Dodano punkt załamania toru na obrazie (przeciągnij go; PPM – usuń)");
        return true;
    }

    void DBeginDrag(Point w, (string, int)? point)
    {
        dDrag = true; dMoved = false; dStart = w; dPoint = point;
        dNodeOrig = dSel.Where(n => DS.Nodes.ContainsKey(n)).ToDictionary(n => n, n => (DS.Nodes[n].X, DS.Nodes[n].Y));
        dEdgeOrig = DS.Edges.Values.Where(x => point != null ? x.Id == point.Value.Item1 : dSel.Contains(x.A) || dSel.Contains(x.B)).ToDictionary(x => x.Id, x => x.Pts.ToList());
        dLabelOrig = dSelLabels.Where(i => i < DS.Labels.Count).ToDictionary(i => i, i => (DS.Labels[i].X, DS.Labels[i].Y));
        dPlatOrig = dSelPlats.Where(i => i < DS.Platforms.Count).ToDictionary(i => i, i => (DS.Platforms[i].X, DS.Platforms[i].Y));
        dCrossOrig = dSelCross.Where(i => i < DS.Crossings.Count).ToDictionary(i => i, i => (DS.Crossings[i].X, DS.Crossings[i].Y0, DS.Crossings[i].Y1));
    }

    void DMove(Point w, MouseEventArgs e)
    {
        if (dBand != null && (e == null || e.LeftButton == MouseButtonState.Pressed)) { dBandEnd = w; panel.InvalidateVisual(); return; }
        if (!dDrag || (e != null && e.LeftButton != MouseButtonState.Pressed)) return;
        if (!dMoved && (w - dStart).Length * panel.Scale < 4) return;
        dMoved = true;
        var a = Snap(dStart); var b = Snap(w); double dx = b.X - a.X, dy = b.Y - a.Y;
        if (dPoint is { } pp)
        {
            var g = Snap(w); var pts = dEdgeOrig[pp.Edge].ToList(); pts[pp.Idx] = (g.X, g.Y);
            DS.Edges[pp.Edge].Pts.Clear(); DS.Edges[pp.Edge].Pts.AddRange(pts);
        }
        else DApplyDelta(dx, dy);
        panel.InvalidateVisual();
    }

    /// <summary>Podgląd przesunięcia grupy: węzły, tory (całe – gdy oba końce w grupie, inaczej tylko koniec), napisy, perony.</summary>
    void DApplyDelta(double dx, double dy)
    {
        foreach (var (n, p) in dNodeOrig) { DS.Nodes[n].X = p.X + dx; DS.Nodes[n].Y = p.Y + dy; }
        foreach (var (eid, orig) in dEdgeOrig)
        {
            var ed = DS.Edges[eid]; bool ia = dSel.Contains(ed.A), ib = dSel.Contains(ed.B);
            var pts = orig.ToList();
            if (ia && ib) pts = pts.Select(q => (q.X + dx, q.Y + dy)).ToList();
            else { if (ia) pts[0] = (pts[0].X + dx, pts[0].Y + dy); if (ib) pts[^1] = (pts[^1].X + dx, pts[^1].Y + dy); }
            ed.Pts.Clear(); ed.Pts.AddRange(pts);
        }
        foreach (var (i, p) in dLabelOrig) { DS.Labels[i].X = p.X + dx; DS.Labels[i].Y = p.Y + dy; }
        foreach (var (i, p) in dPlatOrig) { DS.Platforms[i].X = p.X + dx; DS.Platforms[i].Y = p.Y + dy; }
        foreach (var (i, p) in dCrossOrig) { DS.Crossings[i].X = p.X + dx; DS.Crossings[i].Y0 = p.Y0 + dy; DS.Crossings[i].Y1 = p.Y1 + dy; }
    }

    void DUp(Point w, MouseButtonEventArgs e)
    {
        if (dBand is Point bf)
        {
            dBand = null;
            var r = new Rect(bf, w);
            foreach (var n in DS.Nodes.Values) if (n.X > Station.Hidden + 1 && r.Contains(new Point(n.X, n.Y)) && OnPage(n.X)) dSel.Add(n.Id);
            for (int i = 0; i < DS.Labels.Count; i++) if (r.Contains(new Point(DS.Labels[i].X, DS.Labels[i].Y))) dSelLabels.Add(i);
            for (int i = 0; i < DS.Platforms.Count; i++) { var p = DS.Platforms[i]; if (r.Contains(new Rect(p.X, p.Y, p.W, p.H))) dSelPlats.Add(i); }
            for (int i = 0; i < DS.Crossings.Count; i++) { var c = DS.Crossings[i]; if (r.Contains(new Point(c.X, c.Y0)) && r.Contains(new Point(c.X, c.Y1))) dSelCross.Add(i); }
            DSyncSingle(); ShowProps(); panel.InvalidateVisual();
            status.Text = $"Zaznaczono na obrazie: węzły {dSel.Count}, napisy {dSelLabels.Count}, perony {dSelPlats.Count}, przejazdy {dSelCross.Count} – przeciągnij węzeł grupy albo strzałki (krok siatki)";
            return;
        }
        if (!dDrag) return;
        dDrag = false;
        if (!dMoved) { panel.InvalidateVisual(); return; }
        DCommit(dPoint != null ? "Przesunięto punkt załamania toru na obrazie" : $"Przesunięto na obrazie: węzły {dNodeOrig.Count}, napisy {dLabelOrig.Count}, perony {dPlatOrig.Count}, przejazdy {dCrossOrig.Count}");
    }

    /// <summary>Zapis podglądu (geometria zobrazowania) do dokumentu.</summary>
    void DCommit(string msg)
    {
        doc.Snapshot();
        foreach (var n in dNodeOrig.Keys) doc.SetDPos(n, DS.Nodes[n].X, DS.Nodes[n].Y);
        foreach (var eid in dEdgeOrig.Keys) if (doc.OnDisplay(eid)) doc.SetDPts(eid, DS.Edges[eid].Pts.ToList());
        foreach (var i in dLabelOrig.Keys) { var l = (JsonObject)doc.Labels[i]; l["x"] = Math.Round(DS.Labels[i].X, 1); l["y"] = Math.Round(DS.Labels[i].Y, 1); }
        foreach (var i in dPlatOrig.Keys) { var p = (JsonObject)doc.Platforms[i]; p["x"] = Math.Round(DS.Platforms[i].X, 1); p["y"] = Math.Round(DS.Platforms[i].Y, 1); }
        foreach (var i in dCrossOrig.Keys) { var c = (JsonObject)doc.Crossings[i]; c["x"] = Math.Round(DS.Crossings[i].X, 1); c["y0"] = Math.Round(DS.Crossings[i].Y0, 1); c["y1"] = Math.Round(DS.Crossings[i].Y1, 1); }
        Changed(msg);
    }

    void DRight(Point w)
    {
        if (DPickPoint(w) is { } pp)
        {
            var pts = doc.DPts(pp.Edge); if (pts == null) return;
            doc.Snapshot(); pts.RemoveAt(pp.Idx); doc.SetDPts(pp.Edge, pts);
            Changed("Usunięto punkt załamania toru na obrazie");
            return;
        }
        DClearSel(); ShowProps(); panel.InvalidateVisual();
    }

    /// <summary>Ctrl+A w zobrazowaniu: wszystko na bieżącym obrazie.</summary>
    void DSelectAll()
    {
        DClearSel();
        double x0 = panel.Page?.X0 ?? -1e7 + 2, x1 = panel.Page?.X1 ?? 1e7;
        dBand = new Point(x0 - 1, -1e6); DUp(new Point(x1 + 1, 1e6), null);
    }

    /// <summary>Usunięcie całego zobrazowania (pulpit pusty, do narysowania od nowa). Układ torowy bez zmian; Ctrl+Z cofa.</summary>
    void DClearAll()
    {
        int n = doc.Disp != null ? doc.DispNodes.Count : 0;
        if (!NoPrompt && !FuzzMode && MessageBox.Show(this, $"Usunąć CAŁE zobrazowanie ({n} węzłów na obrazach, napisy, perony, przejazdy pulpitu)?{(char)10}{(char)10}Pulpit będzie pusty – tory umieścisz i narysujesz od nowa. Układ torowy (plan, odcinki, sygnalizatory, przebiegi) zostaje bez zmian. Ctrl+Z cofa, przy zapisie edytor robi kopię pliku.", "Zobrazowanie", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        doc.Snapshot(); doc.ClearDisplay(); DClearSel();
        Changed("Usunięto całe zobrazowanie – pulpit pusty, wszystkie tory na liście „Nieumieszczone” (Ctrl+Z cofa)");
    }

    /// <summary>Przesunięcie zaznaczenia strzałkami (krok siatki).</summary>
    bool DKey(Key k)
    {
        double g = Grid; (double dx, double dy) = k switch { Key.Left => (-g, 0.0), Key.Right => (g, 0.0), Key.Up => (0.0, -g), Key.Down => (0.0, g), _ => (0.0, 0.0) };
        if (k == Key.Delete) { DHideSel(); return true; }
        if (dx == 0 && dy == 0) return false;
        if (DSelCount == 0) return true;
        DBeginDrag(new Point(), null); dDrag = false;
        DApplyDelta(dx, dy);
        DCommit("Przesunięto zaznaczenie na obrazie");
        return true;
    }

    void DHideSel()
    {
        if (dSel.Count == 0 && DSelCount > 0)
        {
            doc.Snapshot();
            foreach (var i in dSelLabels.OrderByDescending(x => x)) if (i < doc.Labels.Count) doc.Labels.RemoveAt(i);
            foreach (var i in dSelPlats.OrderByDescending(x => x)) if (i < doc.Platforms.Count) doc.Platforms.RemoveAt(i);
            foreach (var i in dSelCross.OrderByDescending(x => x)) if (i < doc.Crossings.Count) doc.Crossings.RemoveAt(i);
            DClearSel(); Changed("Usunięto z obrazu pulpitu (plan schematyczny bez zmian)");
            return;
        }
        if (dSel.Count == 0) return;
        if (!NoPrompt && MessageBox.Show(this, $"Usunąć z obrazu pulpitu {dSel.Count} węzłów i tory, które do nich dochodzą?\nUkład torowy (połączenia, odcinki, sygnalizatory) zostaje bez zmian – elementy można umieścić ponownie.", "Zobrazowanie", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        doc.Snapshot(); foreach (var n in dSel) doc.HideNode(n); DClearSel();
        Changed("Usunięto z obrazu pulpitu (układ torowy bez zmian)");
    }

    string DHover(Point w)
    {
        var g = Snap(w); string pos = $"x={g.X:0} y={g.Y:0}";
        var n = DPickNode(w);
        if (n != null) return $"{pos}   węzeł {n}{(DS.Nodes[n].Switch != null ? ", zwrotnica " + DS.Switches[DS.Nodes[n].Switch].Label : "")}{(DS.Nodes[n].Signals.Count > 0 ? ", " + string.Join(", ", DS.Nodes[n].Signals.Select(Station.Lbl)) : "")} – przeciągnij (Shift – do zaznaczenia)";
        if (DPickPoint(w) != null) return $"{pos}   punkt załamania toru – przeciągnij, PPM – usuń (uchwyty widać przy zaznaczonym węźle)";
        if (DPickSeg(w) is { } s) return $"{pos}   tor {s.Edge}, odcinek {DS.Edges[s.Edge].Sec} – dwuklik: nowy punkt załamania";
        return pos + "   (przeciąganie – przesuwanie widoku, Shift+przeciąganie – zaznaczenie prostokątem)";
    }

    // ------------------------------------------------------------------ rysowanie
    void DDraw(DrawingContext dc, double scale)
    {
        if (DS == null) return;
        var az = new SolidColorBrush(Color.FromRgb(0x00, 0xa0, 0xff));
        var orange = new SolidColorBrush(Color.FromRgb(0xff, 0x9a, 0x20));
        // punkty załamania torów (uchwyty)
        double h = 2.6 / scale;
        foreach (var e in DS.Edges.Values)
        {
            if (e.Link || e.Pts.Count < 3 || !(dSel.Contains(e.A) || dSel.Contains(e.B) || dPoint?.Edge == e.Id)) continue;
            for (int i = 1; i < e.Pts.Count - 1; i++) dc.DrawRectangle(az, null, new Rect(e.Pts[i].X - h, e.Pts[i].Y - h, 2 * h, 2 * h));
        }
        foreach (var n in dSel) if (DS.Nodes.TryGetValue(n, out var nd)) dc.DrawEllipse(null, new Pen(az, 2 / scale), new Point(nd.X, nd.Y), 7 / scale + 2, 7 / scale + 2);
        foreach (var i in dSelLabels) if (i < DS.Labels.Count) dc.DrawRectangle(null, new Pen(az, 1.5 / scale), new Rect(DS.Labels[i].X - 4, DS.Labels[i].Y - 8, 16, 16));
        foreach (var i in dSelPlats) if (i < DS.Platforms.Count) { var p = DS.Platforms[i]; dc.DrawRectangle(null, new Pen(az, 1.5 / scale), new Rect(p.X, p.Y, p.W, p.H)); }
        foreach (var i in dSelCross) if (i < DS.Crossings.Count) { var c = DS.Crossings[i]; dc.DrawRectangle(null, new Pen(az, 1.5 / scale), new Rect(c.X - 5, c.Y0 - 5, 10, c.Y1 - c.Y0 + 10)); }
        if (tool == Tool.Platform && rectFrom is Point rf) dc.DrawRectangle(null, new Pen(az, 1.5 / scale), new Rect(rf, Snap(mouse)));
        if (tool == Tool.Crossing && crossFrom is Point cfr) dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(0xf0, 0x80, 0xf0)), 3 / scale), cfr, new Point(cfr.X, Snap(mouse).Y));
        if (dBand is Point bf) dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x20, 0x00, 0xa0, 0xff)), new Pen(az, 1 / scale) { DashStyle = DashStyles.Dash }, new Rect(bf, dBandEnd));
        // napis trybu w rogu widoku
        var v = panel.ViewWorld;
        var ft = Ft("ZOBRAZOWANIE – obraz pulpitu (połączenia nie zmieniają się)", 11 / scale, new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)));
        dc.DrawText(ft, new Point(v.Left + 8 / scale, v.Top + 6 / scale));
    }

    // ------------------------------------------------------------------ panel boczny
    void DShowProps()
    {
        props.Children.Add(T("Zobrazowanie (obraz pulpitu EbiScreen)", 14, Brushes.White, true));
        props.Children.Add(T("Elementy obrazu są powiązane z układem torowym, ale ich położenie jest niezależne: tory mogą się zbliżać i krzyżować – połączenia tworzy tylko układ torowy.\n\n• przeciągnij węzeł – przesuwa go razem z końcami torów\n• przeciąganie pustego miejsca – przesuwanie widoku, kółko – powiększenie\n• Shift+klik / Shift+przeciąganie (prostokąt) – zaznaczenie grupy; przeciąganie węzła grupy i strzałki – przesuwanie\n• dwuklik na torze – punkt załamania, PPM na punkcie – usunięcie\n• Del – usunięcie z obrazu (układ bez zmian)\n• Napis (N), Peron (P), Przejazd (X) – elementy pulpitu; plan schematyczny ma własne, osobne", 10.5, Brushes.Gray));
        props.Children.Add(T($"Na obrazie: napisy {DS?.Labels.Count ?? 0}, perony {DS?.Platforms.Count ?? 0}, przejazdy {DS?.Crossings.Count ?? 0}", 11, Brushes.LightGray));
        int total = doc.EdgeObjs.Count(e => !StationDoc.IsLink(e)), shown = doc.EdgeObjs.Count(e => !StationDoc.IsLink(e) && doc.OnDisplay(StationDoc.Sv(e["id"])));
        props.Children.Add(T($"Tory na obrazie: {shown} z {total}", 12, Brushes.White, true));
        if (dSel.Count == 0 && DSelCount > 0)
        {
            props.Children.Add(T($"Zaznaczone: napisy {dSelLabels.Count}, perony {dSelPlats.Count}, przejazdy {dSelCross.Count}", 11.5, Brushes.LightGray));
            props.Children.Add(Btn("Usuń z obrazu pulpitu (Del)", (a, e) => Safe(DHideSel)));
        }
        if (dSel.Count > 0)
        {
            props.Children.Add(T($"Zaznaczone węzły: {dSel.Count}", 11.5, Brushes.LightGray));
            props.Children.Add(Btn("Wyrównaj do wspólnego Y (pierwszy zaznaczony)", (a, e) => Safe(() =>
            {
                var y = DS.Nodes[dSel.First()].Y;
                DBeginDrag(new Point(), null); dDrag = false;
                foreach (var n in dSel) DS.Nodes[n].Y = y;
                foreach (var (eid, orig) in dEdgeOrig)
                {
                    var ed = DS.Edges[eid]; var pts = orig.ToList();
                    if (dSel.Contains(ed.A)) pts[0] = (pts[0].X, y); if (dSel.Contains(ed.B)) pts[^1] = (pts[^1].X, y);
                    if (dSel.Contains(ed.A) && dSel.Contains(ed.B)) pts = pts.Select(q => (q.X, y)).ToList();
                    ed.Pts.Clear(); ed.Pts.AddRange(pts);
                }
                DCommit("Wyrównano zaznaczone węzły na obrazie");
            })));
            props.Children.Add(Btn("Usuń z obrazu pulpitu (Del)", (a, e) => Safe(DHideSel)));
        }
        if (DSelCount == 0) props.Children.Add(Btn("Usuń całe zobrazowanie (rysuję od nowa)…", (a, ev) => Safe(DClearAll)));
        // tory układu nieumieszczone na obrazie
        var hidden = doc.EdgeObjs.Where(e => !StationDoc.IsLink(e) && !doc.OnDisplay(StationDoc.Sv(e["id"]))).ToList();
        props.Children.Add(T($"Nieumieszczone na obrazie: {hidden.Count}", 12, hidden.Count > 0 ? Brushes.Orange : Brushes.Gray, true));
        if (hidden.Count > 0)
        {
            props.Children.Add(T("Tory układu, których dyżurny nie widzi. Zaznacz i umieść – pojawią się w miejscu z rysunku układu, potem przeciągnij je na właściwe miejsce obrazu.", 10.5, Brushes.Gray));
            dHiddenList = new ListBox { Height = 160, Background = Brushes.Black, Foreground = Brushes.White, SelectionMode = SelectionMode.Extended };
            foreach (var e in hidden.OrderBy(e => StationDoc.Sv(e["sec"])))
                dHiddenList.Items.Add(new ListBoxItem { Content = $"{Station.Lbl(StationDoc.Sv(e["sec"]))}{(StationDoc.Sv(e["track"]) is { Length: > 0 } t ? " tor " + t : "")}  ({StationDoc.Sv(e["id"])})", Tag = StationDoc.Sv(e["id"]) });
            props.Children.Add(dHiddenList);
            props.Children.Add(Btn("Umieść zaznaczone (wg rysunku układu)", (a, ev) => Safe(() =>
            {
                var ids = dHiddenList.SelectedItems.OfType<ListBoxItem>().Select(x => (string)x.Tag).ToList();
                if (ids.Count == 0) { status.Text = "Zaznacz tory na liście"; return; }
                doc.Snapshot(); foreach (var id in ids) doc.PlaceFromLayout(id);
                Changed($"Umieszczono na obrazie: {ids.Count} torów");
            })));
            props.Children.Add(Btn("Umieść wszystkie nieumieszczone", (a, ev) => Safe(() =>
            {
                doc.Snapshot(); foreach (var e in hidden) doc.PlaceFromLayout(StationDoc.Sv(e["id"]));
                Changed($"Umieszczono na obrazie: {hidden.Count} torów");
            })));
        }
    }
}

public partial class EditorWindow
{
    /// <summary>Test (--editshot … dclear out.json): usunięcie całego zobrazowania, układ bez zmian, cofanie; zaznaczanie obszarem (plan i obraz), Ctrl+A.</summary>
    public string DClearForTest(string outJson)
    {
        var log = new System.Text.StringBuilder(); FuzzMode = true; int bad = 0;
        void Ok(bool c, string m) { log.AppendLine((c ? "OK  " : "BŁĄD") + " " + m); if (!c) bad++; }
        // plan: zaznaczanie obszarem bez Shift
        PlanForTest(); SetTool(Tool.Select); SetBandMode(true);
        var ns = st.Nodes.Values.OrderBy(n => n.X).ToList(); var c = ns[ns.Count / 2];
        var empty = new Point(c.X + 0.37, c.Y - 9999);   // pusty punkt daleko od torów
        var r0 = new Rect(c.X - 60, c.Y - 40, 120, 80);
        int inRect = st.Nodes.Values.Count(n => r0.Contains(new Point(n.X, n.Y)) && OnPage(n.X));
        Down(new Point(r0.Left, r0.Top - 9000), null);
        // prostokąt zaczęty w pustym miejscu nad planem, zakończony pod nim – zaznacza węzły w pasie X
        Up(new Point(r0.Right, r0.Bottom + 9000), null);
        int band = st.Nodes.Values.Count(n => n.X >= r0.Left && n.X <= r0.Right && OnPage(n.X));
        Ok(selKind == "multi" && multi.Count == band && band > 0, $"plan: zaznaczanie obszarem bez Shift – {multi.Count} węzłów (oczekiwane {band})");
        SetBandMode(false);
        // zobrazowanie
        ToggleDisplayMode();
        string layout() { var r = (JsonObject)doc.Root.DeepClone(); r.Remove("display"); return r.ToJsonString(); }
        int Routes() => new LcsWidzew.Sim.Simulator(doc.Build()).RouteTable.Values.Sum(l => l.Count);
        string before = doc.Root.ToJsonString(), lay0 = layout(); int rt0 = Routes(); int dn0 = doc.DispNodes.Count;
        DClearAll();
        Ok(doc.DispNodes.Count == 0 && doc.DispEdges.Count == 0 && doc.Labels.Count == 0, $"zobrazowanie usunięte ({dn0} węzłów → 0), napisy pulpitu 0");
        Ok(DS.Nodes.Values.All(n => n.X <= Station.Hidden + 1), "na pulpicie nic nie jest rysowane (wszystko ukryte)");
        Ok(layout() == lay0, "układ torowy (plan) bez zmian");
        Ok(Routes() == rt0, $"tablica przebiegów bez zmian ({rt0})");
        var tmp = outJson + ".tmp.json"; doc.Save(tmp); var re = StationDoc.Load(tmp);
        Ok(re.DispNodes.Count == 0 && re.Labels.Count >= 0 && Station.Load(tmp).Display != null, "zapis: puste zobrazowanie zostaje puste po wczytaniu (bez ponownej konwersji)");
        // umieszczenie wszystkiego i Ctrl+A
        doc.Snapshot(); foreach (var e in doc.EdgeObjs.ToList()) if (!StationDoc.IsLink(e)) doc.PlaceFromLayout(StationDoc.Sv(e["id"])); Changed();
        DSelectAll();
        int onPage = DS.Nodes.Values.Count(n => n.X > Station.Hidden + 1 && OnPage(n.X));
        Ok(dSel.Count == onPage && onPage > 0, $"Ctrl+A na obrazie: {dSel.Count} węzłów (na obrazie {onPage})");
        // zaznaczanie obszarem na obrazie bez Shift
        DClearSel(); SetBandMode(true);
        var dn = DS.Nodes.Values.Where(n => n.X > Station.Hidden + 1 && OnPage(n.X)).OrderBy(n => n.X).ToList(); var dc = dn[dn.Count / 2];
        Down(new Point(dc.X - 30, dc.Y - 5000), null); Up(new Point(dc.X + 30, dc.Y + 5000), null);
        int exp = dn.Count(n => n.X >= dc.X - 30 && n.X <= dc.X + 30);
        Ok(dSel.Count == exp && exp > 0, $"obraz: zaznaczanie obszarem bez Shift – {dSel.Count} węzłów (oczekiwane {exp})");
        SetBandMode(false);
        while (doc.Root.ToJsonString() != before && doc.Undo()) { }
        Rebuild();
        Ok(doc.Root.ToJsonString() == before, "Ctrl+Z przywraca zobrazowanie");
        doc.Save(outJson);
        log.AppendLine($"błędów testu: {bad}, błędów edytora: {Errors.Count}");
        return log.ToString();
    }
}

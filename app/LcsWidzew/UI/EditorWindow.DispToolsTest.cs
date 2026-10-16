using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LcsWidzew.Model;

namespace LcsWidzew.UI;

public partial class EditorWindow
{
    /// <summary>Test (--editshot p.png zobrnarz out.json): całe zobrazowanie usunięte i narysowane od nowa narzędziami obrazu,
    /// kliknięciami jak myszą po dawnym obrazie: najpierw zwrotnice, sygnalizatory, kozły i wykolejnice (narzędzia Z / S / K / W),
    /// potem ciągi torów (T) – klik na węźle początkowym, punkty załamania, klik na węźle końcowym albo Enter. Sprawdza: układ torowy
    /// i przebiegi bez zmian, urządzenia i końce ciągów w dawnych miejscach, końce torów obrazu w węzłach; potem zaznaczenie toru
    /// i semafora (panel właściwości UT), Del toru, przypisanie rysunku innemu torowi UT i punktu innemu elementowi, miarka, szukanie, Ctrl+Z.</summary>
    public string DispToolsForTest(string outJson)
    {
        var log = new System.Text.StringBuilder(); FuzzMode = true; int bad = 0;
        void Ok(bool c, string m) { log.AppendLine((c ? "OK  " : "BŁĄD") + " " + m); if (!c) bad++; System.IO.File.WriteAllText(outJson + ".postep", log.ToString()); }
        if (!dispMode) ToggleDisplayMode();
        chkSnap.IsChecked = false;                                         // klikanie w dokładne miejsca dawnego obrazu
        string Layout() { var r = (JsonObject)doc.Root.DeepClone(); r.Remove("display"); return r.ToJsonString(); }
        int Routes() => new LcsWidzew.Sim.Simulator(doc.Build()).RouteTable.Values.Sum(l => l.Count);
        string lay0 = Layout(); int routes0 = Routes();
        var origN = doc.DispNodes.ToDictionary(x => x.Key, x => doc.DPos(x.Key).Value);
        var origE = doc.DispEdges.ToDictionary(x => x.Key, x => doc.DPts(x.Key));
        Point P((double X, double Y) q) => new(q.X, q.Y);
        void Click(Point p) { Down(p, null); Up(p, null); }

        // 1. puste zobrazowanie
        DClearAll();
        Ok(doc.DispNodes.Count == 0 && doc.DispEdges.Count == 0, $"zobrazowanie usunięte (było węzłów {origN.Count}, torów {origE.Count})");

        // 2. urządzenia w dawnych miejscach – narzędzia obrazu, wybór z listy elementów UT
        int placed = 0;
        foreach (var t in new[] { Tool.Switch, Tool.Signal, Tool.TrackEnd, Tool.Derailer })
            foreach (var it in DUnplaced(t).ToList())
            {
                if (!origN.TryGetValue(it.Node, out var op) || Placed(it.Node)) continue;
                SetTool(t); AutoAnswers.Enqueue(new[] { $"[{it.Id}]" }); Click(P(op)); AutoAnswers.Clear();
                if (Placed(it.Node)) placed++; else Ok(false, $"nie umieszczono {it.Text}: {status.Text}");
            }
        SetTool(Tool.Select);
        Ok(placed > 0 && Layout() == lay0, $"umieszczono urządzeń: {placed}; układ bez zmian");

        // 3. ciągi torów – klikami po dawnym obrazie
        int chains = 0, guard = 0;
        while (guard++ < 500)
        {
            var c = AllChains().FirstOrDefault(x => !ChainOnDisplay(x) && x.Edges.All(origE.ContainsKey) && (Placed(x.From) || Placed(x.To)))
                 ?? AllChains().FirstOrDefault(x => !ChainOnDisplay(x) && x.Edges.All(origE.ContainsKey));
            if (c == null) break;
            if (!Placed(c.From) && Placed(c.To)) c = Reverse(c);
            // dawna łamana ciągu od From do To
            var poly = new List<Point>(); var cur = c.From;
            foreach (var e in c.Edges)
            {
                var pts = origE[e].Select(P).ToList(); if (st.Edges[e].A != cur) pts.Reverse();
                foreach (var q in pts) if (poly.Count == 0 || (poly[^1] - q).Length > 0.05) poly.Add(q);
                cur = OtherEnd(e, cur);
            }
            SetTool(Tool.Track);
            AutoAnswers.Enqueue(new[] { $"[{c.Edges[0]}]" });
            Click(Placed(c.From) ? P(doc.DPos(c.From).Value) : poly[0]); AutoAnswers.Clear();
            if (dChain == null || dChain.Edges[0] != c.Edges[0]) { Ok(false, $"nie rozpoczęto ciągu {ChainDesc(c)}: {status.Text}"); break; }
            foreach (var q in poly.Skip(1).Take(poly.Count - 2)) Click(q);
            if (Placed(c.To)) Click(P(doc.DPos(c.To).Value)); else { Click(poly[^1]); DChainKey(Key.Enter); }
            if (!ChainOnDisplay(c)) { Ok(false, $"ciąg nie na obrazie: {ChainDesc(c)}: {status.Text}"); DChainCancel(""); break; }
            chains++;
        }
        SetTool(Tool.Select);
        int missing = origE.Keys.Count(e => !doc.OnDisplay(e));
        Ok(missing == 0, $"narysowano ciągów: {chains}; torów dawnego obrazu bez rysunku: {missing}");
        // urządzenia i końce ciągów w dawnych miejscach, końce torów w węzłach
        int moved = 0, endBad = 0;
        foreach (var t in new[] { Tool.Switch, Tool.Signal, Tool.TrackEnd, Tool.Derailer })
            foreach (var n in t switch { Tool.Switch => st.Switches.Values.Select(x => x.Node), Tool.Signal => st.Signals.Values.Select(x => x.Node), Tool.TrackEnd => st.TrackEnds.Keys, _ => st.Derailers.Values.Select(x => x.Node) })
                if (origN.TryGetValue(n, out var op) && doc.DPos(n) is { } np && Dist(op, np) > 0.2) moved++;
        foreach (var (eid, _) in doc.DispEdges)
        {
            var le = doc.Edge(eid); var pts = doc.DPts(eid); var pa = doc.DPos(StationDoc.Sv(le["a"])); var pb = doc.DPos(StationDoc.Sv(le["b"]));
            if (pa == null || pb == null || Dist(pts[0], pa.Value) > 0.2 || Dist(pts[^1], pb.Value) > 0.2) endBad++;
        }
        Ok(moved == 0, $"urządzenia w dawnych miejscach (przesunięte: {moved})");
        Ok(endBad == 0, $"końce torów obrazu w węzłach (błędne: {endBad})");
        Ok(Layout() == lay0 && Routes() == routes0, $"układ torowy i przebiegi bez zmian ({routes0})");
        var shotState = doc.Root.ToJsonString();

        // 4. zaznaczenie toru i semafora – panel właściwości z UT
        var se = DS.Edges.Values.First(x => !x.Link && x.Pts.Count >= 2 && OnPage(x.Pts[0].X) && (P(x.Pts[0]) - P(x.Pts[^1])).Length > 30);
        var mid = new Point((se.Pts[0].X + se.Pts[1].X) / 2, (se.Pts[0].Y + se.Pts[1].Y) / 2);
        Click(mid);
        Ok(selKind == "edge" && selId == se.Id && props.Children.OfType<Button>().Any(b => (b.Content as string ?? "").StartsWith("Przypisz")), $"klik na torze obrazu zaznacza tor {se.Id} (panel: {selKind})");
        var sg = st.Signals.Values.First(s => doc.DPos(s.Node) is { } q && OnPage(q.X));
        Click(P(doc.DPos(sg.Node).Value));
        Ok(selKind is "sig" or "sw" && props.Children.OfType<ComboBox>().Any(), $"klik na semaforze {sg.Label} – właściwości z UT (panel: {selKind} {selId})");
        // 5. Del toru, przypisanie rysunku innemu torowi UT
        Click(mid); DKey(Key.Delete);
        Ok(!doc.OnDisplay(se.Id) && Layout() == lay0, $"Del zdejmuje tor {se.Id} z obrazu, układ bez zmian");
        var other = DS.Edges.Values.First(x => !x.Link && x.Id != se.Id && x.Pts.Count >= 2 && OnPage(x.Pts[0].X) && (P(x.Pts[0]) - P(x.Pts[^1])).Length > 30);
        var oPts = doc.DPts(other.Id);
        Click(new Point((other.Pts[0].X + other.Pts[1].X) / 2, (other.Pts[0].Y + other.Pts[1].Y) / 2));
        AutoAnswers.Enqueue(new[] { $"[{se.Id}]" }); DRebindEdge(other.Id); AutoAnswers.Clear();
        Ok(doc.OnDisplay(se.Id) && !doc.OnDisplay(other.Id) && Layout() == lay0, $"rysunek toru {other.Id} przypisany torowi UT {se.Id} ({status.Text})");
        DoUndo(); DoUndo();
        Ok(doc.OnDisplay(se.Id) && doc.OnDisplay(other.Id) && doc.DPts(other.Id).SequenceEqual(oPts), "Ctrl+Z ×2 przywraca oba tory");
        // 6. przypisanie punktu innemu elementowi UT
        var sg2 = st.Signals.Values.Where(s => s.Node != sg.Node && Placed(s.Node)).Skip(1).First();
        var p2 = doc.DPos(sg2.Node).Value; doc.Snapshot(); doc.HideNode(sg2.Node); Changed("test");
        Click(P(doc.DPos(sg.Node).Value));
        AutoAnswers.Enqueue(new[] { $"[{sg2.Name}]" }); DRebindNode(sg.Node); AutoAnswers.Clear();
        Ok(Placed(sg2.Node) && !Placed(sg.Node) && Layout() == lay0, $"punkt {sg.Label} przypisany sygnalizatorowi {sg2.Label}");
        DoUndo(); DoUndo();
        Ok(Placed(sg.Node) && Placed(sg2.Node) && Dist(doc.DPos(sg2.Node).Value, p2) < 0.01, "Ctrl+Z przywraca oba sygnalizatory");
        // 7. miarka i szukanie
        SetTool(Tool.Measure); Click(new Point(100, 100)); Click(new Point(180, 160)); MeasureKey(Key.Enter);
        Ok(measures.Count == 1 && measures[0].Done && status.Text.Contains("j."), "miarka na obrazie: " + status.Text.Split("·")[0].Trim());
        measures.Clear(); SetTool(Tool.Select);
        Find(sg.Label);
        Ok(dSel.Contains(sg.Node) || dEdge != null || selKind != null, $"szukanie „{sg.Label}” na obrazie: {status.Text}");
        // 8. zaznaczanie kilku torów i przesuwanie
        try {
        DClearSel();
        var hz = DS.Edges.Values.Where(x => !x.Link && x.Pts.Count >= 2 && OnPage(x.Pts[0].X) && Math.Abs(x.Pts[0].Y - x.Pts[1].Y) < 0.01 && Math.Abs(x.Pts[0].X - x.Pts[1].X) > 30).ToList();
        var t1 = hz[0]; var t2 = DS.Edges.Values.First(x => !x.Link && x.Pts.Count >= 2 && x.Pts[0].X > Station.Hidden + 1 && OnPage(x.Pts[0].X) && (new Point(x.Pts[0].X, x.Pts[0].Y) - new Point(x.Pts[1].X, x.Pts[1].Y)).Length > 30 && x.A != t1.A && x.A != t1.B && x.B != t1.A && x.B != t1.B);
        var a1 = doc.DPos(t1.A).Value; var b1 = doc.DPos(t1.B).Value;
        var m1 = new Point((t1.Pts[0].X + t1.Pts[1].X) / 2, t1.Pts[0].Y); var m2 = new Point((t2.Pts[0].X + t2.Pts[1].X) / 2, (t2.Pts[0].Y + t2.Pts[1].Y) / 2);
        Down(m1, null); var dbg = $"[po kliknięciu: {selKind} {selId}, węzły {dSel.Count}, przeciąganie {dDrag}, skala {panel.Scale:0.###}]"; Move(m1 + new Vector(0, 40), null); dbg += $"[po ruchu: {dMoved}, A {DS.Nodes[t1.A].Y - a1.Y:0.#}]"; Up(m1 + new Vector(0, 40), null);
        Ok(Math.Abs(doc.DPos(t1.A).Value.Y - a1.Y - 40) < 0.2 && Math.Abs(doc.DPos(t1.B).Value.Y - b1.Y - 40) < 0.2 && Layout() == lay0, $"przeciągnięcie toru {t1.Id} przesuwa oba końce, układ bez zmian {dbg} {status.Text}");
        DoUndo();
        Ok(Dist(doc.DPos(t1.A).Value, a1) < 0.01, "Ctrl+Z cofa przesunięcie toru");
        DClearSel();
        // Shift+klik (bez klawiatury w teście): końce obu torów w zaznaczeniu
        foreach (var t in new[] { t1, t2 }) { dSel.Add(t.A); dSel.Add(t.B); }
        DSyncSingle(); ShowProps();
        Ok(DSelEdges().Contains(t1.Id) && DSelEdges().Contains(t2.Id), $"zaznaczone dwa tory ({string.Join(", ", DSelEdges())})");
        var a2 = doc.DPos(t2.A).Value;
        DKey(Key.Down);
        Ok(Math.Abs(doc.DPos(t1.A).Value.Y - a1.Y - Grid) < 0.2 && Math.Abs(doc.DPos(t2.A).Value.Y - a2.Y - Grid) < 0.2, "strzałka przesuwa oba zaznaczone tory o krok siatki");
        Down(m2 + new Vector(0, Grid), null); Move(m2 + new Vector(30, Grid), null); Up(m2 + new Vector(30, Grid), null);
        Ok(Math.Abs(doc.DPos(t1.A).Value.X - a1.X - 30) < 0.2 && Math.Abs(doc.DPos(t2.A).Value.X - a2.X - 30) < 0.2, "przeciągnięcie toru z grupy przesuwa całą grupę");
        DoUndo(); DoUndo();
        Ok(Dist(doc.DPos(t1.A).Value, a1) < 0.01 && Dist(doc.DPos(t2.A).Value, a2) < 0.01, "Ctrl+Z ×2 cofa przesunięcia grupy");
        var ch = ChainOf(t1.Id); DClearSel(); foreach (var n in DChainNodes(ch)) dSel.Add(n);
        Ok(DSelEdges().Count >= ch.Edges.Count(doc.OnDisplay), $"cały ciąg (Ctrl+klik): węzły {dSel.Count}, tory {DSelEdges().Count}");
        DClearSel(); ShowProps();
        } catch (Exception ex) { Ok(false, "WYJĄTEK w kroku 8: " + ex.GetType().Name + ": " + ex.Message + " @ " + (ex.StackTrace ?? "").Split((char)10).FirstOrDefault(l => l.Contains("LcsWidzew"))?.Trim()); }
        // 9. Del na obrazie: semafor na torze – ukryty symbol, tory zostają; zwrotnica – komunikat; grupa; Shift+Del
        try {
        bool OnTr(string n) => doc.EdgesAt(n).Count(x => doc.OnDisplay(StationDoc.Sv(x["id"]))) >= 2;
        var sm = st.Signals.Values.First(s => Placed(s.Node) && OnTr(s.Node) && st.Nodes[s.Node].Switch == null);
        var smp = P(doc.DPos(sm.Node).Value); int onDisp0 = doc.DispEdges.Count;
        DClearSel(); Click(smp); DKey(Key.Delete);
        Ok(Placed(sm.Node) && doc.DispEdges.Count == onDisp0 && doc.SymHidden(sm.Name), $"Del na semaforze {sm.Label} (na torze): symbol ukryty, tory zostają ({status.Text})");
        doc.Save(outJson + ".ukryty.json");
        var reh = Station.Load(outJson + ".ukryty.json");
        Ok(reh.Display.HiddenSyms.Contains(sm.Name) && reh.Display.Signals.ContainsKey(sm.Name), "pulpit po wczytaniu: symbol ukryty, sygnalizator działa w zależnościach");
        SetTool(Tool.Signal); AutoAnswers.Enqueue(new[] { $"[{sm.Name}]" }); Click(new Point(smp.X + 200, smp.Y + 200)); AutoAnswers.Clear(); SetTool(Tool.Select);
        Ok(!doc.SymHidden(sm.Name) && Dist(doc.DPos(sm.Node).Value, (smp.X, smp.Y)) < 0.01, "narzędzie Semafor przywraca ukryty symbol w jego miejscu");
        var swn = st.Switches.Values.First(s => Placed(s.Node) && OnTr(s.Node)).Node; var before9 = doc.Root.ToJsonString();
        DClearSel(); Click(P(doc.DPos(swn).Value)); DKey(Key.Delete);
        Ok(doc.Root.ToJsonString() == before9 && status.Text.Contains("Shift+Del"), "Del na zwrotnicy z torami – bez zmian, komunikat");
        var ch9 = AllChains().First(c => c.Edges.Count >= 2 && c.Edges.All(doc.OnDisplay));
        DClearSel(); foreach (var n in DChainNodes(ch9)) dSel.Add(n); DKey(Key.Delete);
        Ok(ch9.Edges.All(e => !doc.OnDisplay(e)) && Layout() == lay0, $"Del grupy (cały ciąg {ChainDesc(ch9)}) – tory ciągu zdjęte");
        DoUndo();
        DClearSel(); Click(smp); dShiftDel = true; DHideSel();
        Ok(!Placed(sm.Node) && doc.DispEdges.Count < onDisp0, "Shift+Del – punkt razem z torami");
        DoUndo();
        Ok(doc.DispEdges.Count == onDisp0 && Placed(sm.Node), "Ctrl+Z przywraca");
        } catch (Exception ex) { Ok(false, "WYJĄTEK w kroku 9: " + ex.GetType().Name + ": " + ex.Message + " @ " + (ex.StackTrace ?? "").Split((char)10).FirstOrDefault(l => l.Contains("LcsWidzew"))?.Trim()); }
        DClearSel(); ShowProps();
        Ok(doc.Root.ToJsonString() == shotState, "stan po testach = stan po narysowaniu");
        doc.Save(outJson);
        var re = Station.Load(outJson);
        Ok(re.Display != null && new LcsWidzew.Sim.Simulator(re).RouteTable.Values.Sum(l => l.Count) == routes0, "zapis i ponowne wczytanie");
        panel.FitPage(); Click(P(doc.DPos(sg.Node).Value));      // zrzut: zaznaczony semafor i jego właściwości z UT
        log.AppendLine($"błędów testu: {bad}; błędów edytora: {Errors.Count}");
        foreach (var g in Errors.GroupBy(x => x)) log.AppendLine($"{g.Count(),4} × {g.Key}");
        return log.ToString();
    }
}

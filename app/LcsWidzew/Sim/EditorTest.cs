using System.IO;
using System.Text;
using LcsWidzew.Model;

namespace LcsWidzew.Sim;

/// <summary>Test operacji edytora bez okna: budowa małej stacji od zera, szlaki, odstępy, przebiegi.</summary>
public static class EditorTest
{
    public static void Run(string outPath)
    {
        var sb = new StringBuilder();
        void Out(string s) => sb.AppendLine(s);
        try
        {
            var d = StationDoc.New("KO", "Koluszki testowe");
            // tor główny zasadniczy 1 i tor 3 z rozjazdami
            var w = d.AddNode("KO", 100, 500); var a1 = d.AddNode("KO", 400, 500); var z1 = d.AddNode("KO", 600, 500);
            var z2 = d.AddNode("KO", 1200, 500); var b1 = d.AddNode("KO", 1400, 500); var e = d.AddNode("KO", 1900, 500);
            var t3a = d.AddNode("KO", 650, 560); var t3b = d.AddNode("KO", 1150, 560);
            d.AddEdge(w, a1, null, "KO"); d.AddEdge(a1, z1, null, "KO"); var mid = d.AddEdge(z1, z2, null, "KO"); d.AddEdge(z2, b1, null, "KO"); d.AddEdge(b1, e, null, "KO");
            d.AddEdge(z1, t3a, null, "KO"); d.AddEdge(t3a, t3b, null, "KO"); d.AddEdge(t3b, z2, null, "KO");
            d.AddSwitch(z1, "1", "KO", true); d.AddSwitch(z2, "2", "KO", true);
            // semafory: wjazdowe A (zachód, jazda na wschód), B (wschód), wyjazdowe
            d.AddSignal(a1, "A", "wjazdowy", 1, "KO");
            d.AddSignal(b1, "B", "wjazdowy", -1, "KO");
            var s1 = d.SplitEdge(mid, 700, 500, "KO"); d.AddSignal(s1, "D1", "wyjazdowy", -1, "KO");
            string EdgeBetween(string x, string y) => StationDoc.Sv(d.EdgesAt(x).First(q => StationDoc.Sv(q["a"]) == y || StationDoc.Sv(q["b"]) == y)["id"]);
            var s2 = d.SplitEdge(EdgeBetween(s1, z2), 1100, 500, "KO");
            d.AddSignal(s2, "F1", "wyjazdowy", 1, "KO");
            var s3 = d.SplitEdge(EdgeBetween(t3a, t3b), 700, 560, "KO");
            d.AddSignal(s3, "D3", "wyjazdowy", -1, "KO");
            var s4 = d.SplitEdge(EdgeBetween(s3, t3b), 1100, 560, "KO");
            d.AddSignal(s4, "F3", "wyjazdowy", 1, "KO");
            Out("Walidacja 1: " + string.Join(" | ", d.Validate()));
            // szlaki: zachodni SBL 3 odstępy, wschodni blokada liniowa (1 odstęp)
            d.AddBlock("1W", a1, w, false, "Sąsiad Zachód", "1", "1", 1, true);
            d.AddBlock("1E", b1, e, false, "Sąsiad Wschód", "1", "1", -1, false);
            Out("Bloki: " + string.Join(", ", d.Blocks.Select(b => $"{b.Key}:{StationDoc.Sv(b.Value["type"])} odstępy={((System.Text.Json.Nodes.JsonArray)b.Value["secsWE"]).Count}")));
            d.SetOdstepy("1W", 4);
            d.SetOdstepy("1E", 1);
            Out("Po zmianie: " + string.Join(", ", d.Blocks.Select(b => $"{b.Key}:{StationDoc.Sv(b.Value["type"])} odstępy={((System.Text.Json.Nodes.JsonArray)b.Value["secsWE"]).Count}")));
            Out("Sygnalizatory odstępowe: " + string.Join(" ", d.Signals.Where(s => StationDoc.Sv(s.Value["type"]) == "odstepowy").Select(s => s.Key)));
            d.MoveNode(t3a, 660, 580); d.MoveNode(t3b, 1140, 580);
            Out("Walidacja 2: " + string.Join(" | ", d.Validate()));
            var st = d.Build();
            Out($"Station: węzły {st.Nodes.Count}, krawędzie {st.Edges.Count}, zwrotnice {st.Switches.Count} ({string.Join(" ", st.Switches.Values.Select(s => s.Label + ":tip" + s.TipDir))}), semafory {st.Signals.Count}, szlaki {st.Blocks.Count}");
            var sim = new Simulator(st); new Trains(sim);
            foreach (var (k, l) in sim.RouteTable) if (l.Count > 0) Out($"  {k,-8} {string.Join(", ", l.Select(r => r.Type + "→" + Station.Lbl(r.End) + "[" + string.Join(" ", r.Sw.Select(q => Station.Lbl(q.Num) + q.Pos)) + "]"))}");
            sim.User = new User { Name = "T", Role = "dyżurny ruchu" };
            Out("A D1 Pociąg (przez 1-?): " + sim.Exec("A D1 Pociąg").Msg);
            for (int i = 0; i < 100; i++) sim.Tick(0.1);
            Out("F1 1E Pociąg: " + sim.Exec("F1 1E Pociąg").Msg);
            var tmp = Path.Combine(Path.GetTempPath(), "lcs_edtest.json");
            d.Save(tmp); var st2 = Station.Load(tmp);
            Out("Zapis/odczyt OK: " + st2.Edges.Count + " krawędzi");
            d.DeleteBlock("1E"); d.DeleteNode(t3a);
            Out("Po usunięciu: " + string.Join(" | ", d.Validate()) + $" ; switches={d.Switches.Count}");
            d.Build();
            // tory pionowe (łącznik rozjazdowy narysowany pionowo) i scalanie węzłów
            var v = StationDoc.New("PV", "Pion");
            var vw1 = v.AddNode("PV", 100, 300); var vm1 = v.AddNode("PV", 500, 300); var vm1b = v.AddNode("PV", 700, 300); var ve1 = v.AddNode("PV", 1100, 300);
            var vw2 = v.AddNode("PV", 100, 400); var vm2 = v.AddNode("PV", 500, 400); var ve2 = v.AddNode("PV", 1100, 400);
            v.AddEdge(vw1, vm1, null, "PV"); v.AddEdge(vm1, vm1b, null, "PV"); v.AddEdge(vm1b, ve1, null, "PV"); v.AddEdge(vw2, vm2, null, "PV"); v.AddEdge(vm2, ve2, null, "PV");
            var ve = v.AddEdge(vm1, vm2, null, "PV"); v.Edge(ve)["dir"] = 1;       // z toru górnego na dolny – jazda na wschód
            v.AddSwitch(vm1, "1", "PV", true); v.AddSwitch(vm2, "2", "PV", true);
            v.AddSignal(v.SplitEdge(StationDoc.Sv(v.EdgesAt(vw1).First()["id"]), 300, 300, "PV"), "A", "wyjazdowy", 1, "PV");
            v.AddSignal(v.SplitEdge(StationDoc.Sv(v.EdgesAt(ve2).First()["id"]), 900, 400, "PV"), "B", "wyjazdowy", 1, "PV");
            Out("Pion – walidacja: " + string.Join(" | ", v.Validate()));
            var vs = v.Build(); var vsim = new Simulator(vs);
            Out("Pion – zwrotnice: " + string.Join(" ", vs.Switches.Values.Select(x => $"{x.Label}:tip{x.TipDir}")) + "; przebiegi A: " + string.Join(", ", vsim.RouteTable["PV:A"].Select(r => r.Id + "[" + r.SwText + "]")));
            // łącznik z punktem pośrednim: ukos – pion – ukos, jak na wzorze
            var x = StationDoc.New("PX", "Łącznik");
            var xa = x.AddNode("PX", 100, 300); var xs1 = x.AddNode("PX", 400, 300); var xb = x.AddNode("PX", 900, 300);
            var xc = x.AddNode("PX", 100, 400); var xs2 = x.AddNode("PX", 432, 400); var xd = x.AddNode("PX", 900, 400);
            var xj1 = x.AddNode("PX", 416, 316); var xj2 = x.AddNode("PX", 416, 384);
            x.AddEdge(xa, xs1, null, "PX"); x.AddEdge(xs1, xb, null, "PX"); x.AddEdge(xc, xs2, null, "PX"); x.AddEdge(xs2, xd, null, "PX");
            x.AddEdge(xs1, xj1, null, "PX"); var xv = x.AddEdge(xj1, xj2, null, "PX"); x.Edge(xv)["dir"] = 1; x.AddEdge(xj2, xs2, null, "PX");
            x.AddSwitch(xs1, "1", "PX", true); x.AddSwitch(xs2, "2", "PX", true);
            Out("Łącznik – odcinki: " + string.Join(" ", x.EdgeObjs.Where(q => new[] { xj1, xj2 }.Contains(StationDoc.Sv(q["a"])) || new[] { xj1, xj2 }.Contains(StationDoc.Sv(q["b"]))).Select(q => StationDoc.Sv(q["a"]) + "-" + StationDoc.Sv(q["b"]) + ":" + StationDoc.Sv(q["sec"]))));
            x.AddSignal(x.SplitEdge(StationDoc.Sv(x.EdgesAt(xa).First()["id"]), 250, 300, "PX"), "A", "wyjazdowy", 1, "PX");
            x.AddSignal(x.SplitEdge(StationDoc.Sv(x.EdgesAt(xd).First()["id"]), 700, 400, "PX"), "B", "wyjazdowy", 1, "PX");
            var xsim = new Simulator(x.Build());
            Out("Łącznik – przebiegi A: " + string.Join(", ", xsim.RouteTable["PX:A"].Select(r => r.Id + "[" + r.SwText + "] " + string.Join(" ", r.Secs.Select(Station.Lbl)))));
            x.Save(Path.Combine(Path.GetTempPath(), "lcs_lacznik.json"));
            Out("Pion – vm1b zwykły węzeł: " + v.IsPlainNode(vm1b));
            v.MergeNode(vm1b); Out("Pion – po scaleniu: tory " + v.Edges.Count + ", walidacja: " + string.Join(" | ", v.Validate()));
            v.MoveNode(vm2, 500, 450); Out("Pion – po przesunięciu: " + string.Join(" | ", v.Validate()) + "; dir=" + v.Edge(ve)?["dir"]);
            // rodzaje blokad: PBL dwukierunkowa (zachód), SBL jednokierunkowa (wschód); semafor wyjazdowy bez Ms2
            {
                var k = StationDoc.New("KB", "Blokady");
                var w0 = k.AddNode("KB", 20, 500); var wa = k.AddNode("KB", 250, 500); var wb = k.AddNode("KB", 1700, 500); var w1 = k.AddNode("KB", 1990, 500);
                k.AddEdge(w0, wa, null, "KB"); var mid2 = k.AddEdge(wa, wb, null, "KB"); k.AddEdge(wb, w1, null, "KB");
                k.AddSignal(wa, "A", "wjazdowy", 1, "KB"); k.AddSignal(wb, "B", "wjazdowy", -1, "KB");
                var nf = k.SplitEdge(mid2, 1200, 500, "KB"); k.AddSignal(nf, "F", "wyjazdowy", 1, "KB"); k.Signals["KB:F"]["shunt"] = false;
                var nt = k.SplitEdge(StationDoc.Sv(k.EdgesAt(nf).First(q => StationDoc.Sv(q["a"]) == nf || StationDoc.Sv(q["b"]) == nf)["id"]), 1000, 500, "KB");
                k.AddSignal(nt, "Tm1", "tm", 1, "KB");
                k.AddBlock("1W", wa, w0, false, "Zachód", "1", "1", 1, "pol", 1);      // PBL dwukier., kierunek przyjazdu
                k.AddBlock("1E", wb, w1, false, "Wschód", "1", "1", 1, "sbl1", 3);     // SBL jednokier. na wyjazd
                var ks = k.Build(); var km = new Simulator(ks); var kt = new Trains(km); km.User = new User { Name = "T", Role = "d" }; km.Page = ks.Pages[0].Id;
                Out("Blokady: " + string.Join("; ", ks.Blocks.Values.Select(b => $"{b.Id} {b.TypeShort}, kier. {b.DefDir}")));
                Out("Przebiegi od F (bez Ms2): " + string.Join(", ", km.RouteTable.GetValueOrDefault("KB:F", new()).Select(r => r.Id)) + " | od Tm1: " + string.Join(", ", km.RouteTable.GetValueOrDefault("KB:Tm1", new()).Select(r => r.Id)));
                Out("1E Wbl: " + km.Exec("1E Wbl").Msg);
                kt.Queue.Add(new TimetableEntry { T = 2, Num = "111", Cat = "IC", From = "1W", To = "1E", Stop = false });
                kt.Queue.Add(new TimetableEntry { T = 30, Num = "222", Cat = "IC", From = "1W", To = "1E", Stop = false });
                Out(km.Exec("A F Pociąg").Msg); Out(km.Exec("F 1E Pociąg").Msg);
                bool bk = false, shown = false;
                for (int i = 0; i < 4 * 900; i++)
                {
                    km.Tick(0.25);
                    if (!bk && km.Blocks["1W"].AwaitEnd == 0 && !km.L.Blocks["1W"].SecsWE.Any(km.SecOcc) && km.T > 0)
                    {
                        if (!shown) { shown = true; Out($"{km.Clock()} szlak 1W wolny, czeka na blok końcowy; kolejny pociąg zablokowany: {km.Blocks["1W"].AwaitEnd != null}"); }
                        if (km.T > 200) { Out("1W Bkon: " + km.Exec("1W Bkon").Msg); bk = true; }
                    }
                    if (i % 40 == 0 && km.Sigs["KB:A"].Route == null && km.Routes.Count == 0) { km.Exec("A F Pociąg"); km.Exec("F 1E Pociąg"); }
                }
                foreach (var ev in km.Events.Where(ev => ev.Kategoria is "Ruch" || ev.TypObiektu == "Blokada liniowa")) Out($"   {ev.Czas} {ev.Zdarzenie}");
            }
            // PK – punkt końcowy przebiegu; złącza SZLAK WEWNĘTRZNY łączące dwie stacje
            {
                var p = StationDoc.New("PA", "Stacja A"); p.Posts["PB"] = "Stacja B";
                var a0 = p.AddNode("PA", 100, 500); var aA = p.AddNode("PA", 300, 500); var aF = p.AddNode("PA", 700, 500); var aW = p.AddNode("PA", 900, 500); var aE = p.AddNode("PA", 1100, 500);
                p.AddEdge(a0, aA, null, "PA"); p.AddEdge(aA, aF, null, "PA"); p.AddEdge(aF, aW, null, "PA"); p.AddEdge(aW, aE, null, "PA");
                p.AddSignal(aA, "A", "wjazdowy", 1, "PA"); p.AddSignal(aF, "PK1", "pk", 1, "PA"); p.Signals["PA:PK1"]["routes"] = "P";
                p.AddSignal(aW, "W", "wjazdowy", -1, "PA");
                var b0 = p.AddNode("PB", 1500, 500); var bW = p.AddNode("PB", 1700, 500); var bE = p.AddNode("PB", 2100, 500);
                p.AddEdge(b0, bW, null, "PB"); p.AddEdge(bW, bE, null, "PB");
                p.AddSignal(bW, "Y", "wjazdowy", 1, "PB");
                p.AddPort(aE, aW, new[] { "9X", "", "17", "1", "", BlockDef.Types.First(t => t.Code == "pol").Name, "WYJAZD z tej stacji", "1" });
                p.AddPort(b0, bW, new[] { "9Y", "", "17", "1", "", BlockDef.Types.First(t => t.Code == "pol").Name, "WYJAZD z tej stacji", "1" });
                Out("Złącza przed połączeniem: " + string.Join(", ", p.Ports.Select(q => q.Key + ":" + StationDoc.Sv(q.Value["id"]))) + " | " + string.Join(" | ", p.Validate().Where(q => q.Contains("Złącze"))));
                p.Save(Path.Combine(Path.GetTempPath(), "lcs_porty.json"));
                var bid = p.PairPorts(aE, b0, 1800);
                p.Save(Path.Combine(Path.GetTempPath(), "lcs_porty2.json"));
                { var p3 = new StationDoc((System.Text.Json.Nodes.JsonObject)System.Text.Json.Nodes.JsonNode.Parse(p.Root.ToJsonString())); p3.SetOdstepy(bid, 2); p3.Save(Path.Combine(Path.GetTempPath(), "lcs_porty3.json")); }
                var ps = p.Build(); var pm = new Simulator(ps);
                var pb = ps.Blocks[bid];
                Out($"Po połączeniu: szlak {bid} {pb.TypeShort} wewn={pb.Internal} kier.={pb.DefDir} złącza={p.Ports.Count} " + string.Join(" | ", p.Validate().Where(q => q.Contains("Złącze") || q.Contains(bid))));
                Out("Przebiegi od A: " + string.Join(", ", pm.RouteTable["PA:A"].Select(r => r.Type + "→" + Station.Lbl(r.End))));
                p.Signals["PA:PK1"]["routes"] = "M";
                Out("PK1 tylko manewrowe – od A: " + string.Join(", ", new Simulator(p.Build()).RouteTable["PA:A"].Select(r => r.Type + "→" + Station.Lbl(r.End))));
                pm.User = new User { Name = "T", Role = "d" }; pm.Page = ps.Pages[0].Id;
                Out("A PK1 Pociąg: " + pm.Exec("A PK1 Pociąg").Msg);
            }
            // dokument rzeczywisty: Widzew – kopia, przesunięcie i podział szlaku
            var real = StationDoc.Load(App.StationPath);
            real.SetOdstepy("2A", 2);
            var rs = real.Build(); var rsim = new Simulator(rs);
            {
                var before = real.EdgeObjs.ToDictionary(q => StationDoc.Sv(q["id"]), q => StationDoc.Sv(q["sec"]));
                var probe = new StationDoc((System.Text.Json.Nodes.JsonObject)System.Text.Json.Nodes.JsonNode.Parse(real.Root.ToJsonString()));
                foreach (var sw in probe.Switches.Select(q => q.Key).ToList()) probe.CrossoverSecs(sw);
                var ch = probe.EdgeObjs.Where(q => before[StationDoc.Sv(q["id"])] != StationDoc.Sv(q["sec"])).Select(q => $"{StationDoc.Sv(q["id"])}:{before[StationDoc.Sv(q["id"])]}→{StationDoc.Sv(q["sec"])}").ToList();
                Out($"Napraw łączniki na układzie: zmienione tory {ch.Count}: {string.Join(" ", ch.Take(30))}");
            }
            var e1 = rsim.RouteTable["LW:E"].Where(r => Station.Lbl(r.End) == "S3" && r.Type == "P").ToList();
            Out("E→S3 przed: " + string.Join(" ; ", e1.Select(r => r.Variant + ":" + r.SwText)));
            real.SetRule("LW:E", "LW:S3", "P", e1[^1].SwKey, null, true);
            real.SetRule("LW:E", "LW:S3", "P", e1[0].SwKey, true, null);
            var rsim2 = new Simulator(real.Build());
            Out("E→S3 po (ostatni zasadniczy, pierwszy wyłączony): " + string.Join(" ; ", rsim2.RouteTable["LW:E"].Where(r => Station.Lbl(r.End) == "S3" && r.Type == "P").Select(r => r.Variant + ":" + r.SwText)));
            {
                var es3 = new Simulator(real.Build()).RouteTable["LW:E"].Where(r => r.Type == "P").Take(3).ToList();
                Out("Prędkości przebiegów od E: " + string.Join(", ", es3.Select(r => $"{Station.Lbl(r.End)} V{r.VMax:0}[{r.SwText}]")));
                real.SetRule("LW:E", es3[0].End, "P", es3[0].SwKey, null, null, 30);
                var es4 = new Simulator(real.Build()).RouteTable["LW:E"].First(r => r.End == es3[0].End && r.SwKey == es3[0].SwKey);
                Out($"Po ustaleniu 30 km/h: V{es4.VMax:0} (ustalona {es4.VSet:0}, wyliczona {es4.VCalc:0})");
            }
            Out($"Widzew po edycji 2A: odstępy {rs.Blocks["2A"].SecsWE.Count}, przebiegi {rsim.RouteTable.Values.Sum(l => l.Count)}");
        }
        catch (Exception ex) { Out("BŁĄD: " + ex); }
        File.WriteAllText(outPath, sb.ToString());
    }
}

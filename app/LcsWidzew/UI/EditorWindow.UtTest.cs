using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Input;
using LcsWidzew.Model;
using LcsWidzew.Sim;

namespace LcsWidzew.UI;

public partial class EditorWindow
{
    /// <summary>
    /// Test edytora układu torowego (--editshot … ut out.json): mijanka na planie schematycznym Ig-10 budowana jak myszą –
    /// tory w pasach (jeden narysowany od prawej), rozjazdy przeciągnięciem wg typu, łącznik, odgałęzienie z torem, długość
    /// wpisana z klawiatury, semafory przypięte do toru i obracane, szlaki; potem przebiegi w symulatorze, przesunięcie rozjazdu,
    /// przeniesienie toru na inny pas, usunięcie rozjazdu, cofanie. Zrzuty planu.
    /// </summary>
    public string UtForTest(string outJson)
    {
        var log = new System.Text.StringBuilder(); int bad = 0; FuzzMode = true;
        void Ok(bool c, string m) { log.AppendLine((c ? "OK  " : "BŁĄD") + " " + m); if (!c) bad++; }
        doc = StationDoc.New("MJ", "Mijanka", schem: true); path = Path.Combine(Path.GetTempPath(), "lcs_ut_mijanka.json"); dispMode = false; Kind = EditorKind.Uklad;
        Rebuild(true); PlanForTest(); ApplyModeToolbars(); chkSnap.IsChecked = true;
        Ok(Schem, $"plan schematyczny Ig-10: 1:{doc.ScaleAlong:0} / 1:{doc.ScaleAcross:0}");
        double ux = doc.Ux, bh = BandH;
        Point P(double s, double band) => new(s * ux, band * bh);
        var c = P(900, 1); ZoomForTest(c.X, c.Y, 3 / ux);
        void Show(Point p) { panel.CenterOn(p.X, p.Y); panel.UpdateLayout(); mouse = p; }
        void Click(Tool t, Point p) { if (tool != t) SetTool(t); Show(p); Down(p, null); Up(p, null); }
        void Drag(Tool t, Point a, Point b) { if (tool != t) SetTool(t); Show(a); Down(a, null); mouse = b; Move(new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2), null); Move(b, null); Up(b, null); }
        int Routes() { try { return new Simulator(doc.Build()).RouteTable.Values.Sum(l => l.Count); } catch { return -1; } }

        // 1. tor 1 (szlak – stacja – szlak) od lewej, tor 2 od PRAWEJ do lewej
        Click(Tool.Track, P(0, 0)); Click(Tool.Track, P(1800, 0)); Right(new Point());
        Click(Tool.Track, P(1280, 1)); Click(Tool.Track, P(520, 1)); Right(new Point());
        Ok(doc.Edges.Count == 2, $"dwa tory: {doc.Edges.Count}");
        Ok(doc.EdgeObjs.All(e => doc.Pos(StationDoc.Sv(e["b"])).X > doc.Pos(StationDoc.Sv(e["a"])).X), "kierunek A→B obu torów w prawo (rosnący km), także toru narysowanego od prawej");
        // tor nachodzący na istniejący – odrzucony
        int e0 = doc.Edges.Count; Click(Tool.Track, P(1400, 1)); Click(Tool.Track, P(1000, 1)); Right(new Point());
        Ok(doc.Edges.Count == e0, "tor nachodzący na tor w tym samym pasie – odrzucony: " + status.Text);
        Ok(!doc.Nodes.Any(n => doc.EdgesAt(n.Key).Count == 0), "porzucony początek toru nie zostaje jako pusty węzeł");

        // 2. rozjazdy przeciągnięciem: zachodni w prawo-w dół na koniec toru 2, wschodni w lewo-w dół
        turnKindBox.SelectedItem = "Rz 1:9 R190";
        AutoAnswers.Enqueue(new[] { "1", "na wprost (+ na tor prosty)" });      // numer wpisuje projektant
        Drag(Tool.Turnout, P(477, 0), P(560, 1)); log.AppendLine("rozjazd zachodni: " + status.Text);
        Drag(Tool.Turnout, P(1323, 0), P(1240, 1)); log.AppendLine("rozjazd wschodni: " + status.Text);
        Ok(doc.Switches.Count == 2, $"zwrotnice: {string.Join(", ", doc.Switches.Select(x => x.Key))}");
        var sw1 = doc.Switches.Select(x => (JsonObject)x.Value).First(x => StationDoc.Sv(x["label"]) == "1"); var sw2 = doc.Switches.Select(x => (JsonObject)x.Value).First(x => StationDoc.Sv(x["label"]) == "2");
        Ok(StationDoc.Sv(sw1["kind"]) == "Rz 1:9 R190" && StationDoc.Sv(sw1["ratio"]) == "1:9", "typ rozjazdu zapisany w zwrotnicy (Rz 1:9 R190, skos 1:9)");
        Ok(doc.TurnoutHand(StationDoc.Sv(sw1["node"])) == "prawy" && doc.TurnoutHand(StationDoc.Sv(sw2["node"])) == "lewy", $"kierunek rozjazdów: 1 – {doc.TurnoutHand(StationDoc.Sv(sw1["node"]))}, 2 – {doc.TurnoutHand(StationDoc.Sv(sw2["node"]))}");
        // odcinek zwrotnicowy 1: od PR do KR (a ≈ 10,5 m przed, b ≈ 16,6 m za punktem środkowym)
        var z1 = StationDoc.Sv(sw1["sec"]); var zl = doc.EdgeObjs.Where(e => StationDoc.Sv(e["sec"]) == z1 && StationDoc.IsHorizontal(e)).Sum(e => StationDoc.D(e["len"]));
        var (_, _, ta, tb) = StationDoc.TurnoutGeom("Rz 1:9 R190");
        Ok(Math.Abs(zl - (ta + tb)) < 1.5, $"odcinek zwrotnicowy {Station.Lbl(z1)} na torze zasadniczym: {zl:0.#} m (PR–KR {ta + tb:0.#} m)");
        var sk = doc.MinusLeg(StationDoc.Sv(sw1["node"])); var skp = doc.EdgePts(StationDoc.Sv(sk["id"]));
        double rdx = Math.Abs(skp[^1].X - skp[0].X) / ux, rdy = Math.Abs(skp[^1].Y - skp[0].Y) / doc.Uy;
        Ok(Math.Abs(rdx / rdy - 9) < 0.4, $"skos rozjazdu 1: {rdx:0.#} m wzdłuż / {rdy:0.##} m w poprzek = 1:{rdx / rdy:0.#}");

        // 3. odgałęzienie z toru 2 w dół z nowym torem (bocznica) + kozioł; łącznik między torami 1 i 2
        turnKindBox.SelectedItem = "Rz 1:12 R500";
        Drag(Tool.Turnout, P(700, 1), P(1000, 2)); log.AppendLine("odgałęzienie: " + status.Text);
        Ok(doc.Switches.Count == 3 && doc.EdgeObjs.Any(e => StationDoc.IsHorizontal(e) && Math.Abs(doc.EdgePts(StationDoc.Sv(e["id"]))[0].Y - 2 * bh) < 0.1), "odgałęzienie 1:12 z nowym torem w pasie 2");
        var endN = doc.Nodes.Where(n => doc.EdgesAt(n.Key).Count == 1 && Math.Abs(doc.Pos(n.Key).Y - 2 * bh) < 0.1).Select(n => n.Key).FirstOrDefault();
        AutoAnswers.Enqueue(new[] { "kozioł oporowy – tylko symbol", "", "" }); if (endN != null) Click(Tool.TrackEnd, new Point(doc.Pos(endN).X, doc.Pos(endN).Y));
        Ok(endN != null && doc.Bumpers.ContainsKey(endN), "kozioł na końcu bocznicy");
        turnKindBox.SelectedItem = "Rz 1:9 R190";
        AutoAnswers.Enqueue(new[] { "4", "5", "na zwrot (+ na odgałęzienie)" });
        Drag(Tool.Turnout, P(820, 0), P(900, 1)); log.AppendLine("łącznik: " + status.Text);
        Ok(doc.Switches.Count == 5, $"łącznik 1→2: zwrotnice {doc.Switches.Count}");
        Ok(doc.Switches["MJ:4"] is JsonObject s4 && StationDoc.Sv(s4["zas"]) == "-", "położenie zasadnicze zwrotnicy 4 – na zwrot (zas: -)");
        { var bs = doc.Build(); var w4 = bs.Switches["MJ:4"]; Ok(w4.Plus == w4.Diverging && w4.Minus != w4.Diverging, "zwrotnica 4: (+) = odgałęzienie, prędkość na bok dalej na odgałęzieniu"); }
        // numer zajęty – odrzucony
        AutoAnswers.Enqueue(new[] { "1", "na wprost (+ na tor prosty)" }); int scn = doc.Switches.Count;
        Drag(Tool.Turnout, P(1100, 1), P(1150, 2)); Ok(doc.Switches.Count == scn && status.Text.Contains("już istnieje"), "zajęty numer rozjazdu odrzucony: " + status.Text);
        // skos przez tor pośredni – odrzucony
        int sc = doc.Switches.Count; Drag(Tool.Turnout, P(1100, 0), P(1250, 2));
        Ok(doc.Switches.Count == sc, "skos przecinający tor pośredni odrzucony: " + status.Text);

        // 4. tor z długością wpisaną z klawiatury (tor wyciągowy od końca toru 1 nie – w wolnym pasie -1)
        Click(Tool.Track, P(600, -1)); mouse = P(900, -1);
        foreach (var k in new[] { Key.D1, Key.D5, Key.D0, Key.Enter }) UtKey(k);
        Right(new Point());
        var typed = doc.EdgeObjs.FirstOrDefault(e => Math.Abs(doc.EdgePts(StationDoc.Sv(e["id"]))[0].Y + bh) < 0.1);
        Ok(typed != null && Math.Abs(StationDoc.D(typed["len"]) - 150) < 0.05, $"tor z długością wpisaną z klawiatury: {StationDoc.D(typed?["len"]):0.##} m");
        if (typed != null) { doc.Snapshot(); doc.DeleteEdge(StationDoc.Sv(typed["id"])); Changed(); }

        // 5. semafory przypięte do toru: domyślnie dla jazdy w prawo; F / klik na nim – obrót
        void Sig(string name, string typ, Point p, bool left)
        {
            AutoAnswers.Enqueue(new[] { name, typ, "", "" }); Click(Tool.Signal, p);
            var key = "MJ:" + name;
            if (left && doc.Signals.ContainsKey(key)) { Select("sig", key); UtKey(Key.F); }
            log.AppendLine($"semafor {name}: {SigTravelText(key)}");
        }
        Sig("A", "wjazdowy", P(300, 0), false); Sig("B", "wjazdowy", P(1500, 0), true);
        Sig("F1", "wyjazdowy", P(560, 0), true); Sig("F2", "wyjazdowy", P(600, 1), true);
        Sig("D1", "wyjazdowy", P(1240, 0), false); Sig("D2", "wyjazdowy", P(1200, 1), false);
        Ok(doc.Signals.Count == 6, $"sygnalizatory: {doc.Signals.Count}");
        Rebuild();
        Ok(SigTravelText("MJ:A").Contains("w prawo") && SigTravelText("MJ:B").Contains("w lewo") && SigTravelText("MJ:F1").Contains("w lewo") && SigTravelText("MJ:D2").Contains("w prawo"), "kierunki: A, D1, D2 – w prawo; B, F1, F2 – w lewo");
        // obrót klikiem narzędziem Semafor (dwa razy – wraca)
        var dA = (int)StationDoc.D(doc.Signals["MJ:A"]["dir"]); var pa = doc.Pos(StationDoc.Sv(doc.Signals["MJ:A"]["node"]));
        Click(Tool.Signal, new Point(pa.X, pa.Y)); bool flipped = (int)StationDoc.D(doc.Signals["MJ:A"]["dir"]) == -dA; Click(Tool.Signal, new Point(pa.X, pa.Y));
        Ok(flipped && (int)StationDoc.D(doc.Signals["MJ:A"]["dir"]) == dA, "klik na semaforze narzędziem Semafor – obrót (i z powrotem)");
        Select("sig", "MJ:A"); UtKey(Key.F); UtKey(Key.F);
        // przesunięcie semafora wzdłuż toru (zostaje na torze)
        SetTool(Tool.Select); Drag(Tool.Select, new Point(pa.X, pa.Y), new Point(pa.X + 20 * ux, pa.Y + 0.4 * bh));
        var pa2 = doc.Pos(StationDoc.Sv(doc.Signals["MJ:A"]["node"]));
        Ok(Math.Abs(pa2.X - pa.X - 20 * ux) < 0.01 && Math.Abs(pa2.Y - pa.Y) < 0.01, $"semafor przesunięty wzdłuż toru o {(pa2.X - pa.X) / ux:0.#} m, w poprzek o {(pa2.Y - pa.Y) / doc.Uy:0.##} m");

        // 6. szlaki
        AutoAnswers.Enqueue(new[] { "1Z", "Sąsiad Zachód", "1", "1", "zewnętrzny", "liniowa bez odstępów", "WYJAZD z tej stacji" }); Click(Tool.Szlak, new Point(pa2.X, pa2.Y));
        var pb = doc.Pos(StationDoc.Sv(doc.Signals["MJ:B"]["node"]));
        AutoAnswers.Enqueue(new[] { "1W", "Sąsiad Wschód", "1", "1", "zewnętrzny", "liniowa bez odstępów", "WYJAZD z tej stacji" }); Click(Tool.Szlak, new Point(pb.X, pb.Y));
        Ok(doc.Blocks.Count == 2, $"szlaki: {string.Join(", ", doc.Blocks.Select(b => b.Key))}");
        SetTool(Tool.Select);

        // 7. kontrola (jak użytkownik: najpierw „Nadaj identyfikatory odcinków”), przebiegi, nastawienie w symulatorze
        doc.Snapshot(); doc.AutoNameSections(); Changed();
        var val = doc.Validate(); Ok(val.Count == 0, "Sprawdź bez uwag"); log.AppendLine("Sprawdź: " + (val.Count == 0 ? "bez uwag" : string.Join(" | ", val)));
        int nr = Routes(); log.AppendLine($"Przebiegi: {nr}");
        string[] cmds = { "A D1 Pociąg", "A D2 Pociąg", "B F1 Pociąg", "B F2 Pociąg", "F1 1Z Pociąg", "F2 1Z Pociąg", "D1 1W Pociąg", "D2 1W Pociąg" };
        int SetRoutes(bool show)
        {
            int okc = 0;
            foreach (var cmd in cmds)
            {
                try
                {
                    var s1 = new Simulator(doc.Build()); new Trains(s1); s1.User = new User { Name = "T", Role = "dyżurny ruchu" };
                    var m = s1.Exec(cmd).Msg; bool good = m.Contains("w realizacji") || m.Contains("nastaw"); if (good) okc++;
                    if (show) log.AppendLine($"  {cmd}: {m}");
                }
                catch (Exception ex) { if (show) log.AppendLine($"  {cmd}: wyjątek {ex.Message}"); }
            }
            return okc;
        }
        Ok(SetRoutes(true) == cmds.Length, "przebiegi pociągowe nastawiają się (8 z 8)");
        try { var stt = doc.Build(); foreach (var sw in stt.Switches.Values) log.AppendLine($"  zwrotnica {sw.Label}: iglica {sw.Tip}, (+) {sw.Plus}, (−) {sw.Minus}, typ {sw.Kind}, na bok {sw.VBok:0} km/h"); }
        catch (Exception ex) { Ok(false, "model: " + ex.Message); }

        // 8. przesunięcie rozjazdu wzdłuż toru (zwrotnica z PR / KR i końcem odgałęzienia), cofnięcie
        var n1 = StationDoc.Sv(sw1["node"]); var p1 = doc.Pos(n1); var grp = doc.TurnoutNodes(n1);
        Drag(Tool.Select, new Point(p1.X, p1.Y), new Point(p1.X - 15 * ux, p1.Y));
        var p1b = doc.Pos(n1);
        Ok(Math.Abs(p1b.X - (p1.X - 15 * ux)) < 0.01 && Math.Abs(p1b.Y - p1.Y) < 0.01, $"rozjazd 1 przesunięty wzdłuż toru o {(p1b.X - p1.X) / ux:0.#} m ({grp.Count} węzłów rozjazdu)");
        Ok(SetRoutes(false) == cmds.Length, "po przesunięciu rozjazdu przebiegi dalej się nastawiają");
        DoUndo(); Ok(Math.Abs(doc.Pos(n1).X - p1.X) < 0.01, "Ctrl+Z – rozjazd wraca");

        // 9. przeniesienie toru 2 na pas niżej – skosy wracają do 1:9
        var t2 = doc.EdgeObjs.First(e => StationDoc.IsHorizontal(e) && Math.Abs(doc.EdgePts(StationDoc.Sv(e["id"]))[0].Y - bh) < 0.1 && doc.EdgePts(StationDoc.Sv(e["id"])).Max(q => q.X) > 1000 * ux && doc.EdgePts(StationDoc.Sv(e["id"])).Min(q => q.X) < 1150 * ux);
        var t2p = doc.EdgePts(StationDoc.Sv(t2["id"])); var mid2 = new Point((t2p[0].X + t2p[^1].X) / 2, t2p[0].Y);
        int r0 = Routes();
        Drag(Tool.Select, mid2, new Point(mid2.X, mid2.Y + bh * 1.1)); log.AppendLine("przeniesienie toru: " + status.Text);
        var sk2 = doc.MinusLeg(StationDoc.Sv(sw2["node"])); var q2 = doc.EdgePts(StationDoc.Sv(sk2["id"]));
        double ddx = Math.Abs(q2[^1].X - q2[0].X) / ux, ddy = Math.Abs(q2[^1].Y - q2[0].Y) / doc.Uy;
        log.AppendLine($"  skos rozjazdu 2 po przeniesieniu: {ddx:0.#} / {ddy:0.##} m = 1:{ddx / Math.Max(0.01, ddy):0.#}");
        DoUndo(); Ok(Routes() == r0, $"Ctrl+Z po przeniesieniu toru – przebiegi {Routes()} = {r0}");

        // 10. usunięcie rozjazdu (łącznik z obiema zwrotnicami), cofnięcie
        var cross = doc.Switches.Select(x => (Key: x.Key, O: (JsonObject)x.Value)).First(x => StationDoc.Sv(x.O["label"]) == "4");
        int swc = doc.Switches.Count, edc = doc.Edges.Count;
        Select("sw", cross.Key); DeleteSel();
        Ok(doc.Switches.Count == swc - 2, $"usunięcie łącznika: zwrotnice {swc} → {doc.Switches.Count}, tory {edc} → {doc.Edges.Count}; {status.Text}");
        Ok(doc.Problems().Count == 0, "po usunięciu łącznika – Sprawdź bez uwag" + (doc.Problems().Count > 0 ? ": " + string.Join(" | ", doc.Problems().Select(p => p.Msg)) : ""));
        Ok(SetRoutes(false) == cmds.Length, "po usunięciu łącznika przebiegi dalej się nastawiają");
        DoUndo(); Ok(doc.Switches.Count == swc, "Ctrl+Z – łącznik wraca");

        // 11. rozjazdy krzyżowe i skrzyżowanie: osobne grupy torów (pasy -2, -3, -4), skos przez tor środkowy
        string Yard(double x0, string kind, string N, string e1, string e2)
        {
            foreach (var bd in new[] { -2, -3, -4 }) { Click(Tool.Track, P(x0, bd)); Click(Tool.Track, P(x0 + 600, bd)); Right(new Point()); }
            turnKindBox.SelectedItem = kind;
            AutoAnswers.Enqueue(new[] { N, e1, e2 });
            Drag(Tool.Turnout, P(x0 + 300, -3), P(x0 + 360, -4));
            var msg = status.Text; log.AppendLine($"{kind}: {msg}");
            // tarcze manewrowe na lewych końcach, znaczniki końca przebiegu na prawych
            foreach (var bd in new[] { -2, -3, -4 })
            {
                AutoAnswers.Enqueue(new[] { $"Tm{N}{-bd}", "tm", "", "" }); Click(Tool.Signal, P(x0 + 30, bd));
                var en = doc.NodeNear(P(x0 + 600, bd).X, P(x0 + 600, bd).Y, 0.5);
                AutoAnswers.Enqueue(new[] { "znacznik końca przebiegu manewrowego (np. 12k)", $"k{N}{-bd}", "" }); if (en != null) Click(Tool.TrackEnd, new Point(doc.Pos(en).X, doc.Pos(en).Y));
            }
            SetTool(Tool.Select);
            return msg;
        }
        HashSet<string> Ends(string sig) { try { var sm2 = new Simulator(doc.Build()); return sm2.RouteTable.TryGetValue("MJ:" + sig, out var l) ? l.Select(r => Station.Lbl(r.End)).ToHashSet() : new(); } catch (Exception ex) { log.AppendLine("  wyjątek: " + ex.Message); return new(); } }
        string Set(HashSet<string> h) => string.Join(" ", h.OrderBy(x => x));
        void YardShot(double xc, string name) { var yc = P(xc, -3); ZoomForTest(yc.X, yc.Y, 3.5 / ux); selKind = null; ShowProps(); UpdateLayout(); panel.InvalidateVisual(); UpdateLayout(); var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); rtb.Render(this); var enc = new System.Windows.Media.Imaging.PngBitmapEncoder(); enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb)); using var f = File.Create(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outJson)) ?? ".", name)); enc.Save(f); log.AppendLine("zrzut: " + name); }
        Yard(2000, "Rkp 1:9 R190 (krzyżowy podwójny)", "20", "21", "22");
        Ok(doc.Switches.ContainsKey("MJ:20ab") && doc.Switches.ContainsKey("MJ:20cd") && doc.Switches.ContainsKey("MJ:21") && doc.Switches.ContainsKey("MJ:22"), "Rkp 20: połówki 20ab / 20cd i zwrotnice 21, 22 na końcach skosu");
        if (doc.Switches.ContainsKey("MJ:20ab")) Ok(doc.Pos(StationDoc.Sv(doc.Switches["MJ:20ab"]["node"])).X < doc.Pos(StationDoc.Sv(doc.Switches["MJ:20cd"]["node"])).X, "20ab bliżej początku linii (z lewej), 20cd dalej");
        var r3 = Ends("Tm203"); var r2 = Ends("Tm202");
        log.AppendLine($"  Rkp: z Tm203 → {Set(r3)}; z Tm202 → {Set(r2)}");
        Ok(r3.IsSupersetOf(new[] { "k203", "k204" }) && r2.IsSupersetOf(new[] { "k202", "k203", "k204" }), "Rkp: jazdy na wprost po obu torach i na zwrot w obie strony");
        Yard(3000, "Rkj 1:9 R190 (krzyżowy pojedynczy)", "30", "31", "32");
        Ok(doc.Switches.ContainsKey("MJ:30a") && doc.Switches.ContainsKey("MJ:30b"), "Rkj 30: zwrotnice 30a / 30b");
        r3 = Ends("Tm303"); r2 = Ends("Tm302");
        log.AppendLine($"  Rkj: z Tm303 → {Set(r3)}; z Tm302 → {Set(r2)}");
        Ok(r3.IsSupersetOf(new[] { "k303", "k304" }) && r2.IsSupersetOf(new[] { "k302", "k304" }) && !r2.Contains("k303"), "Rkj: na wprost oba tory, jedno połączenie (z toru na skos), bez drugiego");
        Yard(4000, "Skrz 1:9 (skrzyżowanie torów)", "40", "41", "42");
        r3 = Ends("Tm403"); r2 = Ends("Tm402");
        log.AppendLine($"  Skrz: z Tm403 → {Set(r3)}; z Tm402 → {Set(r2)}");
        Ok(r3.SetEquals(new[] { "k403" }) && r2.IsSupersetOf(new[] { "k402", "k404" }) && !r2.Contains("k403"), "skrzyżowanie: tor na wprost, skos na wprost, bez połączeń");
        Ok(doc.EdgeObjs.Count(e => StationDoc.Sv(e["sec"]) == "MJ:z40") >= 2, "skrzyżowanie: wspólny odcinek z40 toru i skosu (wykluczenie jazd)");
        var vv = doc.Validate().Where(v => !v.Contains("automatycznie")).ToList();
        Ok(vv.Count == 0, "Sprawdź po rozjazdach krzyżowych" + string.Concat(vv.Select(v => " | " + v)));
        { var z4 = P(845, 0.5); ZoomForTest(z4.X, z4.Y, 6 / ux); selKind = null; ShowProps(); UpdateLayout(); panel.InvalidateVisual(); UpdateLayout(); var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); rtb.Render(this); var enc = new System.Windows.Media.Imaging.PngBitmapEncoder(); enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb)); using var f = File.Create(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outJson)) ?? ".", "ut_zasadnicze.png")); enc.Save(f); }
        YardShot(2300, "ut_rkp.png"); YardShot(3300, "ut_rkj.png"); YardShot(4300, "ut_skrz.png");
        // usunięcie Rkp – razem ze zwrotnicami na końcach skosu, tory ciągłe
        int sw0 = doc.Switches.Count; Select("sw", "MJ:20ab"); DeleteSel();
        Ok(doc.Switches.Count == sw0 - 4 && Ends("Tm203").SetEquals(new[] { "k203" }), $"usunięcie Rkp 20: zwrotnice {sw0} → {doc.Switches.Count}, z Tm203 → {Set(Ends("Tm203"))}");
        DoUndo(); Ok(doc.Switches.Count == sw0, "Ctrl+Z – Rkp wraca");
        // usunięcie skrzyżowania – Del na torze skosu
        var sk40 = doc.EdgeObjs.FirstOrDefault(e => StationDoc.Sv(e["sec"]) == "MJ:z40" && !StationDoc.IsHorizontal(e));
        if (sk40 != null) { Select("edge", StationDoc.Sv(sk40["id"])); DeleteSel(); }
        Ok(sk40 != null && !doc.EdgeObjs.Any(e => StationDoc.Sv(e["sec"]) == "MJ:z40") && !doc.Switches.ContainsKey("MJ:41"), "usunięcie skrzyżowania 40 (Del na skosie) ze zwrotnicami 41, 42");
        DoUndo(); swc = doc.Switches.Count;

        doc.Save(outJson);
        Ok(Station.Load(outJson).Switches.Count == swc, "zapis i wczytanie");
        var dir = Path.GetDirectoryName(Path.GetFullPath(outJson)) ?? ".";
        void Shot(string name) { UpdateLayout(); panel.InvalidateVisual(); UpdateLayout(); var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); rtb.Render(this); var enc = new System.Windows.Media.Imaging.PngBitmapEncoder(); enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb)); using var f = File.Create(Path.Combine(dir, name)); enc.Save(f); log.AppendLine("zrzut: " + name); }
        var w0 = P(500, 0.5); ZoomForTest(w0.X, w0.Y, 6 / ux); Select("sw", "MJ:1"); Shot("ut_rozjazd.png");
        // podgląd rozjazdu w trakcie przeciągania
        SetTool(Tool.Turnout); var pv = P(1100, 1); Show(pv); Down(pv, null); mouse = P(1180, 2); Move(mouse, null); Shot("ut_podglad_rozjazdu.png"); utTurn = null; SetTool(Tool.Select);
        ZoomForTest(c.X, c.Y, panel.ActualWidth / (1900 * ux)); selKind = null; ShowProps(); Shot("ut_plan.png");
        log.AppendLine($"błędów testu: {bad}, błędów edytora: {Errors.Count}");
        return log.ToString();
    }
}

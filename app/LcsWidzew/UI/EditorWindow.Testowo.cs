using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using LcsWidzew.Model;
using LcsWidzew.Sim;

namespace LcsWidzew.UI;

public partial class EditorWindow
{
    /// <summary>
    /// Stacja testowa „Testowo” (--editshot p.png testowo out.json [skos°]): budowana narzędziami edytora jak myszą na planie
    /// (tory skośne o kilka stopni – jak po zdjęciu): tor 1 zasadniczy z dwoma szlakami (1Z – SBL dwukierunkowa, 3 odstępy;
    /// 1W – PBL dwukierunkowa), tory 2 i 3, bocznica 4 z kozłem i wykolejnicą, rozjazdy, semafory wjazdowe, wyjazdowe, tarcza
    /// manewrowa, perony, przejazd, numery torów i odcinki; zobrazowanie pulpitu ułożone osobno (poziomo, jak na pulpicie).
    /// Do ręcznego testowania wszystkiego: plan, pulpit, przebiegi, blokady, pociągi.
    /// </summary>
    public string TestowoForTest(string outJson, double skewDeg)
    {
        var log = new System.Text.StringBuilder(); int bad = 0;
        void Ok(bool c, string m) { log.AppendLine((c ? "OK  " : "BŁĄD") + " " + m); if (!c) bad++; }
        doc = StationDoc.New("TT", "Testowo"); path = Path.GetFullPath(outJson); dispMode = false; Rebuild(true);
        PlanForTest(); chkSnap.IsChecked = false; ApplyModeToolbars();
        double ux = doc.Ux, uy = doc.Uy, ang = skewDeg * Math.PI / 180;
        Vector U = new(Math.Cos(ang), Math.Sin(ang)), N = new(-Math.Sin(ang), Math.Cos(ang));
        Point P(double s, double off) => new Point(0, 0) + U * (s * ux) + N * (off * uy);
        (double S, double Off) Inv(double x, double y) { var v = new Vector(x, y); return (Vector.Multiply(v, U) / ux, Vector.Multiply(v, N) / uy); }
        ZoomForTest(P(1200, 0).X, P(1200, 0).Y, 3 / ux);
        void Click(Tool t, Point p) { if (tool != t) SetTool(t); panel.CenterOn(p.X, p.Y); panel.UpdateLayout(); mouse = p; Down(p, null); Up(p, null); }
        void Track(params Point[] pts) { foreach (var p in pts) Click(Tool.Track, p); Right(new Point()); }
        void Turnout(Point a, Point b, string what) { Click(Tool.Turnout, a); Click(Tool.Turnout, b); log.AppendLine($"{what}: {status.Text}"); }
        // sygnalizator: klik na torze, klik po prawej stronie toru w kierunku jazdy (east – zgodnie z A→B toru 1)
        void Sig(string name, string typ, bool east, double s, double off) { var p = P(s, off); var side = p + N * ((east ? 2.5 : -2.5) * uy); AutoAnswers.Enqueue(new[] { name, typ, "", "" }); Click(Tool.Signal, p); Click(Tool.Signal, side); log.AppendLine($"{name}: {status.Text}"); }

        // --- tory
        Track(P(0, 0), P(2400, 0));                      // tor 1 – zasadniczy, ze szlakami po obu stronach
        Track(P(700, 4.75), P(1700, 4.75));              // tor 2
        Track(P(760, 9.5), P(1640, 9.5));                // tor 3
        Track(P(1050, -6), P(1450, -6));                 // tor 4 – bocznica (ślepa)
        // --- rozjazdy (plan: prosto pod kątem jak w terenie)
        Turnout(P(650, 0), P(700, 4.75), "rozjazd 1 (tor 1 → tor 2, zachód)");
        Turnout(P(712, 4.75), P(760, 9.5), "rozjazd 2 (tor 2 → tor 3, zachód)");
        Turnout(P(1750, 0), P(1700, 4.75), "rozjazd 3 (tor 1 → tor 2, wschód)");
        Turnout(P(1688, 4.75), P(1640, 9.5), "rozjazd 4 (tor 2 → tor 3, wschód)");
        Turnout(P(1000, 0), P(1050, -6), "rozjazd 5 (tor 1 → bocznica)");
        Ok(doc.Switches.Count == 5, $"zwrotnice: {doc.Switches.Count}");
        // --- sygnalizatory
        Sig("A", "wjazdowy", true, 450, 0); Sig("B", "wjazdowy", false, 1950, 0);
        Sig("F1", "wyjazdowy", false, 680, 0); Sig("F2", "wyjazdowy", false, 735, 4.75); Sig("F3", "wyjazdowy", false, 790, 9.5);
        Sig("D1", "wyjazdowy", true, 1720, 0); Sig("D2", "wyjazdowy", true, 1665, 4.75); Sig("D3", "wyjazdowy", true, 1610, 9.5);
        Sig("Tm1", "tm", false, 1080, -6);
        Ok(doc.Signals.Count == 9, $"sygnalizatory: {doc.Signals.Count}");
        // --- kozioł i wykolejnica na bocznicy
        AutoAnswers.Enqueue(new[] { "kozioł oporowy – tylko symbol", "", "" }); Click(Tool.TrackEnd, P(1450, -6));
        AutoAnswers.Enqueue(new[] { "Wk1" }); Click(Tool.Derailer, P(1100, -6));
        // --- szlaki (klik na semaforze wjazdowym – edytor idzie torem do końca)
        var sbl = BlockDef.Types.First(t => t.Code == "sbl").Name; var pol = BlockDef.Types.First(t => t.Code == "pol").Name;
        AutoAnswers.Enqueue(new[] { "1Z", "Zachodnia", "1", "1", "zewnętrzny", sbl, "WYJAZD z tej stacji", "3" }); Click(Tool.Szlak, P(450, 0)); log.AppendLine("szlak 1Z: " + status.Text);
        AutoAnswers.Enqueue(new[] { "1W", "Wschodnia", "1", "1", "zewnętrzny", pol, "WYJAZD z tej stacji", "1" }); Click(Tool.Szlak, P(1950, 0)); log.AppendLine("szlak 1W: " + status.Text);
        Ok(doc.Blocks.Count == 2, $"szlaki: {string.Join(", ", doc.Blocks.Select(b => b.Key))}");
        // --- perony (krawędź 1,7 m od osi toru) i przejazd na szlaku
        AutoAnswers.Enqueue(new[] { "Peron 1" }); Click(Tool.Platform, P(1450, -1.7)); Click(Tool.Platform, P(1700, -1.7)); Click(Tool.Platform, P(1575, -7.7));
        AutoAnswers.Enqueue(new[] { "Peron 2" }); Click(Tool.Platform, P(880, 11.2)); Click(Tool.Platform, P(1130, 11.2)); Click(Tool.Platform, P(1000, 17.2));
        SetTool(Tool.Select);
        doc.Snapshot();
        doc.Crossings.Add(new JsonObject { ["name"] = "km 1,250", ["kind"] = "Y", ["x"] = Math.Round(P(250, 0).X, 1), ["y0"] = Math.Round(P(250, 0).Y - 12 * uy, 1), ["y1"] = Math.Round(P(250, 0).Y + 12 * uy, 1) });
        doc.Labels.Add(new JsonObject { ["x"] = Math.Round(P(1200, -25).X, 1), ["y"] = Math.Round(P(1200, -25).Y, 1), ["text"] = "Testowo", ["anchor"] = "middle" });
        // --- numery torów (tory stacyjne), odcinki na sygnalizatorach, identyfikatory
        foreach (var e in doc.EdgeObjs.Where(e => !StationDoc.SzlakEdge(e)))
        {
            var pts = doc.EdgePts(StationDoc.Sv(e["id"])); var (s0, o0) = Inv(pts[0].X, pts[0].Y); var (s1, o1) = Inv(pts[^1].X, pts[^1].Y);
            if (Math.Abs(o0 - o1) > 0.5) continue;                     // łączniki rozjazdowe
            string tr = Math.Abs(o0) < 0.5 && Math.Min(s0, s1) >= 650 && Math.Max(s0, s1) <= 1750 ? "1" : Math.Abs(o0 - 4.75) < 0.5 ? "2" : Math.Abs(o0 - 9.5) < 0.5 ? "3" : Math.Abs(o0 + 6) < 0.5 ? "4" : null;
            if (tr != null) e["track"] = tr;
        }
        doc.SplitSectionsAtSignals();
        // tor stacyjny: najdłuższy odcinek toru (między sygnalizatorami) – JT<nr>
        foreach (var g in doc.EdgeObjs.Where(e => StationDoc.Sv(e["track"]) is { Length: > 0 } && !StationDoc.SzlakEdge(e)).GroupBy(e => StationDoc.Sv(e["track"])).ToList())
        {
            var sec = g.GroupBy(e => StationDoc.Sv(e["sec"])).OrderByDescending(x => x.Sum(e => StationDoc.D(e["len"]))).First().Key;
            foreach (var x in doc.EdgeObjs.Where(x => StationDoc.Sv(x["sec"]) == sec)) x["sec"] = $"TT:JT{g.Key}";
        }
        doc.AutoNameSections();
        Changed("Testowo – numery torów, odcinki");
        var val = doc.Validate(); log.AppendLine("Sprawdź: " + (val.Count == 0 ? "bez uwag" : string.Join(" | ", val)));
        // --- zobrazowanie pulpitu: osobny rysunek, poziomo (1 px ≈ 0,75 m wzdłuż, 10 px na metr w poprzek)
        doc.EnsureDisplay();
        foreach (var e in doc.EdgeObjs.ToList()) doc.PlaceFromLayout(StationDoc.Sv(e["id"]));
        Point Pul(double x, double y) { var (s, o) = Inv(x, y); return new Point(Math.Round(100 + s * 0.75, 1), Math.Round(560 + o * 10, 1)); }
        foreach (var (nid, _) in doc.Nodes.ToList()) { var (x, y) = doc.Pos(nid); var q = Pul(x, y); doc.SetDPos(nid, q.X, q.Y); }
        foreach (var e in doc.EdgeObjs) { var id = StationDoc.Sv(e["id"]); doc.SetDPts(id, doc.EdgePts(id).Select(p => { var q = Pul(p.X, p.Y); return (q.X, q.Y); }).ToList()); }
        doc.Disp["labels"] = new JsonArray();
        doc.Disp["platforms"] = new JsonArray(
            new JsonObject { ["name"] = "Peron 1", ["x"] = Pul(P(1450, -1.7).X, P(1450, -1.7).Y).X, ["y"] = 516, ["w"] = 187.5, ["h"] = 30 },
            new JsonObject { ["name"] = "Peron 2", ["x"] = Pul(P(880, 11.2).X, P(880, 11.2).Y).X, ["y"] = 668, ["w"] = 187.5, ["h"] = 30 });
        doc.Disp["crossings"] = new JsonArray(new JsonObject { ["name"] = "km 1,250", ["kind"] = "Y", ["x"] = 100 + 250 * 0.75, ["y0"] = 535, ["y1"] = 585 });
        Changed("Testowo – zobrazowanie");
        // --- model, przebiegi, polecenia
        try
        {
            var stt = doc.Build(); var sm = new Simulator(stt); new Trains(sm);
            int nr = sm.RouteTable.Values.Sum(l => l.Count); log.AppendLine($"Przebiegi: {nr}");
            foreach (var (k, l) in sm.RouteTable) if (l.Count > 0) log.AppendLine($"  {Station.Lbl(k),-4} {string.Join(", ", l.Select(r => r.Id))}");
            Ok(nr >= 25, "tablica przebiegów");
            foreach (var cmd in new[] { "A D1 Pociąg", "A D2 Pociąg", "A D3 Pociąg", "B F1 Pociąg", "B F2 Pociąg", "B F3 Pociąg", "F1 1Z Pociąg", "D1 1W Pociąg", "Tm1 F1 Manewr" })
            {
                var s1 = new Simulator(doc.Build()); new Trains(s1); s1.User = new User { Name = "T", Role = "dyżurny ruchu" };
                var m = s1.Exec(cmd).Msg; log.AppendLine($"  {cmd}: {m}"); Ok(m.Contains("w realizacji"), cmd);
            }
            foreach (var s in stt.Display.Signals.Values) log.AppendLine($"  pulpit {s.Label}: zwrot {(s.VDir > 0 ? "→" : "←")}, obowiązuje {(s.Dir > 0 ? "A→B" : "B→A")}");
        }
        catch (Exception ex) { Ok(false, "model: " + ex.Message); }
        doc.Save(outJson);
        Ok(File.Exists(outJson), "zapis: " + outJson);
        panel.FitAll();
        log.AppendLine($"błędów testu: {bad}, błędów edytora: {Errors.Count}");
        return log.ToString();
    }
}

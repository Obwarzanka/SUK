using System.IO;
using System.Windows;
using System.Windows.Input;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Test śladu i łuków ze śladu (PLAN_EDYTOR_UT.md § 5h) jak użytkownik. „Teren”: trasa prosta – przejściowa 60 – łuk R 600 – przejściowa 60
/// – prosta 120 – łuk R 1200 w lewo – prosta. Sieć OSM z niej: węzły co 12 m z rozrzutem ±0,4 m, jeden węzeł 4 m obok, bocznica
/// odchodząca z węzła w łuku. Narzędzie Ślad: dwa kliknięcia przy liniach – ślad po liniach (bez bocznicy), wykres krzywizny z łukami,
/// tor po śladzie (Enter, Enter), Ctrl+Z. Malowanie śladu na „zdjęciu” – R i L z dokładnością do metrów. Łuk z punktów: punkty
/// odstające pomijane (X), Shift + klik na linii OSM, G – punkty śladu, malowanie. Łuk z opisu planu: R, L, km początku, kąt.
/// </summary>
public static partial class Test
{
    static async Task RunSlad(MainWindow w)
    {
        var dir = Path.Combine(Path.GetTempPath(), "edytor_ut_test", "slad"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        w.SetSchemat(false);
        w.NewDoc(); w.SaveDoc(false, Path.Combine(dir, "stacja.ut.json"));
        var d = w.Doc;
        var o = Geo.ToPuwg92(51.7631, 19.5438); double k = Geo.ScaleFactor(o);
        Point S(XY p) => w.View.ToScreen(p);
        var truthA = new UtAlign { X = o.X, Y = o.Y, Az = 0.7, Els = new() {
            new() { Len = 200 }, new() { Len = 60, R1 = 600 }, new() { Len = 150, R0 = 600, R1 = 600 }, new() { Len = 60, R0 = 600 },
            new() { Len = 120 }, new() { Len = 220, R0 = -1200, R1 = -1200 }, new() { Len = 250 } } };
        var truth = TrasaElem.Compute(truthA, k); double L = truth.Length;

        // --- sieć OSM z „terenu”
        var net = new OsmSiec(); var rnd = new Random(21); long id = 1; var main = new List<long>();
        for (double s = 0; s <= L; s += 12)
        {
            var (q, az) = truth.At(s); double off = (rnd.NextDouble() - 0.5) * 0.8;
            if (Math.Abs(s - 300) < 6) off = 4;                       // węzeł 4 m obok (błąd mapowania)
            net.Nodes[id] = q + Element.Right(az) * (off * k); main.Add(id++);
        }
        net.Ways.Add(main.ToArray());
        int jn = main.Count / 3; var (jp, jaz) = truth.At(jn * 12); var side = new List<long> { main[jn] };
        for (int i = 1; i <= 15; i++) { net.Nodes[id] = jp + Element.Dir(jaz + 0.12) * (i * 12 * k); side.Add(id++); }
        net.Ways.Add(side.ToArray());
        net.Areas.Add((o + new XY(-3000, -3000), o + new XY(3000, 3000)));
        w.View.Osm = net;

        w.SetTool(UtTool.Slad);
        Ok(w.View.Tool == UtTool.Slad && w.SladPanel.Visibility == Visibility.Visible, "narzędzie Ślad (F), panel „Ślad” widoczny");
        var a0 = truth.At(5).P; var a1 = truth.At(L - 5).P;
        w.View.FitPoints(new[] { a0, a1 }); await Settle(w);
        w.View.SimClick(S(a0) + new Vector(4, 3));
        Ok(w.View.SladPts.Count == 1, "klik obok linii OSM (4 px) – punkt na linii");
        w.View.SimClick(S(a1) + new Vector(-3, 4));
        double len = w.View.SladLen;
        Ok(w.View.SladPts.Count >= main.Count - 3 && Math.Abs(len - (L - 10)) < 15,
            $"drugi klik – ślad po liniach przez {w.View.SladPts.Count} węzłów, długość {len:0.0} m (teren {L - 10:0.0} m), bez bocznicy");
        var (kr, luki) = w.View.SladKrzywizna();
        Ok(luki.Count == 2 && Math.Abs(luki[0].R - 600) < 60 && Math.Abs(luki[1].R + 1200) < 150,
            $"krzywizna śladu (baza {w.View.SladBazaUzyta:0} m): łuki z wykresu – {string.Join("; ", luki.Select(Slad.Opis))}");
        w.UpdateKrzyw();
        Ok(w.Krzyw.SladCount > 100 && w.Krzyw.SladLabels.Count == 2 && w.Krzyw.ViewRange.S1 - w.Krzyw.ViewRange.S0 > 900, $"wykres krzywizny: ślad ({w.Krzyw.SladCount} próbek) na km {w.Krzyw.ViewRange.S0:0}–{w.Krzyw.ViewRange.S1:0} m, podpisy {string.Join(" / ", w.Krzyw.SladLabels)}");
        Ok(Find<System.Windows.Controls.TextBlock>(w.SladPanel, t => t.Text.StartsWith("1. łuk w prawo")) != null, "panel: lista łuków ze śladu");

        KeyPress(w, Key.Enter);
        await WaitFor(() => !w.View.SladBusy, 60000);
        var pv = w.View.SladPreview;
        var arcs = pv?.A?.Els.Where(e => e.Luk).ToList() ?? new();
        Ok(pv?.A != null && arcs.Count == 2 && Math.Abs(arcs[0].R0 - 600) < 50 && Math.Abs(arcs[1].R0 + 1200) < 100 && pv.Rms < 0.5 && pv.Pominiete.Count >= 1 && (pv.Przeskoki?.Count ?? 0) == 0 && w.View.SladPreviewInfo().Contains("Łukiem z punktów"),
            $"Enter – tor po śladzie z OSM (węzły co 12 m, ±0,4 m – R do kilku %, przejściowych z takich danych nie widać: sama prawdziwa trasa odbiega od węzłów średnio o ok. 25 cm): {w.View.SladPreviewInfo()}");
        if (shotRails != null) { w.UpdateKrzyw(); await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_slad_osm.png")); }
        int nt = d.P.Tracks.Count;
        KeyPress(w, Key.Enter);
        var nw = d.P.Tracks.LastOrDefault();
        Ok(d.P.Tracks.Count == nt + 1 && nw?.Align == null && nw.Vertices.Count == 4 && nw.Vertices.Count(v => v.R > 0) == 2 && d.UndoName == "Tor po śladzie" && w.View.Geometry(nw).Ok && w.View.SladPts.Count == 0 && w.View.SelectedTrack == nw.Id && Math.Abs(w.View.Geometry(nw).Length - len) < 5,
            $"drugi Enter – nowy tor (wielobok: {nw?.Vertices.Count} wierzchołki, R {string.Join(" / ", nw?.Vertices.Where(v => v.R > 0).Select(v => v.R.ToString("0")) ?? new string[0])}) ({w.LastMessage})");
        {
            // po utworzeniu – Wskaż: klik na torze go zaznacza (w Śladzie kliknięcie dokładało punkty śladu)
            var gr = w.View.Geometry(nw); bool wsk = w.View.Tool == UtTool.Wskaz;
            w.View.Select(null); w.View.SimClick(w.View.ToScreen(gr.At(gr.Length * 0.4).P));
            Ok(wsk && w.View.SelectedTrack == nw.Id && w.View.SladPts.Count == 0, $"po utworzeniu toru narzędzie Wskaż – klik na nowym torze go zaznacza ({w.View.Tool})");
        }
        w.DoUndo();
        Ok(d.P.Tracks.Count == nt, "Ctrl+Z – toru po śladzie nie ma");
        w.SetTool(UtTool.Slad);
        // jako trasa z elementów (przycisk w panelu)
        w.View.SimClick(S(a0) + new Vector(4, 3)); w.View.SimClick(S(a1) + new Vector(-3, 4));
        KeyPress(w, Key.Enter); await WaitFor(() => !w.View.SladBusy, 60000); w.RefreshSladPanel();
        Click(BtnIn(w.SladPanel, "Utwórz jako trasę z elementów"));
        nw = d.P.Tracks.LastOrDefault();
        Ok(d.P.Tracks.Count == nt + 1 && nw?.Align != null && w.View.Geometry(nw).Ok, $"„Utwórz jako trasę z elementów” – tor z elementów ({nw?.Align?.Els.Count} elementów)");
        w.DoUndo();
        w.SetTool(UtTool.Slad);

        // --- Ctrl + klik – punkt ze zdjęcia mimo linii obok; Backspace
        UtView.TestMods = ModifierKeys.Control;
        w.View.SimClick(S(a0) + new Vector(4, 3));
        UtView.TestMods = ModifierKeys.None;
        Ok(w.View.SladPts.Count == 1 && XY.Dist(w.View.SladPts[0], w.View.ToWorld(S(a0) + new Vector(4, 3))) < 1e-6, "Ctrl + klik – punkt tam, gdzie kliknięto (ze zdjęcia)");
        KeyPress(w, Key.Back);
        Ok(w.View.SladPts.Count == 0, "Backspace – krok cofnięty");

        // --- malowanie śladu na „zdjęciu” (dokładnie po osi): R i L z dokładnością do metrów
        w.View.Osm = null;
        var p0 = truth.At(120).P; var p1 = truth.At(560).P;
        w.View.FitPoints(new[] { p0, p1, truth.At(340).P }); await Settle(w);
        // samo przeciągnięcie – przesuwa mapę, nie maluje
        var c0 = w.View.ToWorld(new Point(100, 100));
        w.View.Press(new Point(100, 100), MouseButton.Left);
        for (int i = 1; i <= 10; i++) w.View.Drag(new Point(100 + 8 * i, 100), true);
        w.View.Release(new Point(180, 100), MouseButton.Left);
        Ok(w.View.SladPts.Count == 0 && XY.Dist(w.View.ToWorld(new Point(180, 100)), c0) < 0.05 * XY.Dist(w.View.ToWorld(new Point(100, 100)), w.View.ToWorld(new Point(180, 100))),
            $"Ślad: przeciągnięcie bez Ctrl przesuwa mapę ({w.View.SladPts.Count} pkt)");
        w.View.FitPoints(new[] { p0, p1, truth.At(340).P }); await Settle(w);
        UtView.TestMods = ModifierKeys.Control;
        w.View.Press(S(p0), MouseButton.Left);
        for (double s = 120; s <= 560; s += 1.5) w.View.Drag(S(truth.At(s).P), true);
        w.View.Release(S(p1), MouseButton.Left);
        UtView.TestMods = ModifierKeys.None;
        Ok(w.View.SladPts.Count > 150 && Math.Abs(w.View.SladLen - 440) < 5, $"malowanie po osi toru: {w.View.SladPts.Count} punktów (co 2 m), {w.View.SladLen:0.0} m");
        KeyPress(w, Key.Enter);
        await WaitFor(() => !w.View.SladBusy, 60000);
        var pa = w.View.SladPreview?.A; var pe = pa?.Els ?? new();
        int ai = pe.FindIndex(e => e.Luk);
        Ok(ai > 0 && Math.Abs(pe[ai].R0 - 600) <= 6 && pe[ai - 1].Przejsciowa && Math.Abs(pe[ai - 1].Len - 60) <= 6 && w.View.SladPreview.Rms < 0.05,
            $"tor po namalowanym śladzie: R {(ai >= 0 ? pe[ai].R0 : 0):0} m (teren 600), L {(ai > 0 ? pe[ai - 1].Len : 0):0.0} m (60), odchyłka {w.View.SladPreview?.Rms * 100:0.0} cm");
        KeyPress(w, Key.Escape);
        Ok(w.View.SladPreview == null && w.View.SladPts.Count > 0, "Esc – propozycja odrzucona, ślad zostaje");
        // ślad zostaje do „G” w Łuku z punktów

        // --- Łuk z punktów: punkty odstające, linia OSM, punkty śladu
        w.View.Osm = net;
        var tv = new UtTrack { Vertices = new() { new UtVertex { X = truth.At(0).P.X, Y = truth.At(0).P.Y } } };
        // wielobok z prawdziwych prostych (przecięcie stycznych), łuk zgrubnie R 800 bez L
        var st1 = truth.At(100); var st2 = truth.At(500);
        XY Cross(XY pa1, double az1, XY pb1, double az2) { var d1 = Element.Dir(az1); var d2 = Element.Dir(az2); var ww = pb1 - pa1; double cr = d1.X * d2.Y - d1.Y * d2.X; double t = (ww.X * d2.Y - ww.Y * d2.X) / cr; return pa1 + d1 * t; }
        var vx = Cross(st1.P, st1.Az, st2.P, st2.Az);
        tv.Vertices.Add(new UtVertex { X = vx.X, Y = vx.Y, R = 800 }); tv.Vertices.Add(new UtVertex { X = st2.P.X, Y = st2.P.Y });
        d.Change("Tor do łuku", p => p.Tracks.Add(tv));
        w.SetTool(UtTool.LukPunkty);
        if (w.View.FitFreeVertex) KeyPress(w, Key.W);          // proste stałe
        if (!w.View.FitWithL) KeyPress(w, Key.Tab);           // R i L
        var rv = w.View.Geometry(d.P.Tracks.First(x => x.Id == tv.Id));
        w.View.FitPoints(new[] { st1.P, st2.P, vx }); await Settle(w);
        w.View.SimClick(S(rv.At(rv.Nearest(vx).S).P));
        Ok(w.View.SelectedVertex == 1, "Łuk z punktów: wybrany łuk W2");
        var good = new List<XY>(); for (double s = 210; s <= 460; s += 15) good.Add(truth.At(s).P);
        foreach (var q in good) w.View.SimClick(S(q));
        var bad1 = truth.At(300); var bad2 = truth.At(380);
        w.View.SimClick(S(bad1.P + Element.Right(bad1.Az) * (1.5 * k))); w.View.SimClick(S(bad2.P + Element.Right(bad2.Az) * (-2 * k)));
        var fr = w.View.FitResult;
        Ok(w.View.FitRobust && w.View.FitOut.Count == 2 && fr?.Error == null && Math.Abs(fr.R - 600) <= 5 && Math.Abs(fr.L - 60) <= 5,
            $"2 punkty kliknięte 1,5 i 2 m obok – pominięte ({w.View.FitOut.Count}), R {fr?.R:0} m, L {fr?.L:0} m, odchyłka {fr?.Rms * 100:0.#} cm");
        KeyPress(w, Key.X);
        var fx = w.View.FitResult;
        Ok(!w.View.FitRobust && w.View.FitOut.Count == 0 && fx.Rms > fr.Rms * 3, $"X – bez pomijania: R {fx?.R:0} m, L {fx?.L:0} m, odchyłka {fx?.Rms * 100:0.#} cm (gorzej)");
        KeyPress(w, Key.X);
        KeyPress(w, Key.Escape);
        // Shift + klik dwa razy na linii OSM – węzły z odcinka
        w.View.SimClick(S(rv.At(rv.Nearest(vx).S).P));
        UtView.TestMods = ModifierKeys.Shift;
        w.View.SimClick(S(truth.At(190).P) + new Vector(3, 0));
        w.View.SimClick(S(truth.At(480).P) + new Vector(0, 3));
        UtView.TestMods = ModifierKeys.None;
        var fo = w.View.FitResult;
        Ok(w.View.FitPts.Count >= 20 && fo?.Error == null && Math.Abs(fo.R - 600) <= 40 && w.View.FitOut.Count >= 1,
            $"Shift + klik dwa razy na linii OSM – {w.View.FitPts.Count} węzłów z odcinka, R {fo?.R:0} m, L {fo?.L:0} m, pominięte {w.View.FitOut.Count} (węzeł 4 m obok)");
        KeyPress(w, Key.Escape);
        // G – punkty śladu (namalowanego wyżej)
        w.View.SimClick(S(rv.At(rv.Nearest(vx).S).P));
        KeyPress(w, Key.G);
        var fg = w.View.FitResult;
        Ok(w.View.FitPts.Count > 100 && fg?.Error == null && Math.Abs(fg.R - 600) <= 3 && Math.Abs(fg.L - 60) <= 3, $"G – punkty śladu: {w.View.FitPts.Count} pkt, R {fg?.R:0} m, L {fg?.L:0} m");
        KeyPress(w, Key.Escape);
        // malowanie w Łuku z punktów
        w.View.SimClick(S(rv.At(rv.Nearest(vx).S).P));
        UtView.TestMods = ModifierKeys.Control;
        w.View.Press(S(truth.At(200).P), MouseButton.Left);
        for (double s = 200; s <= 470; s += 1.5) w.View.Drag(S(truth.At(s).P), true);
        w.View.Release(S(truth.At(470).P), MouseButton.Left);
        UtView.TestMods = ModifierKeys.None;
        var fm = w.View.FitResult;
        Ok(w.View.FitPts.Count > 100 && fm?.Error == null && Math.Abs(fm.R - 600) <= 3 && Math.Abs(fm.L - 60) <= 3, $"malowanie w Łuku z punktów: {w.View.FitPts.Count} pkt, R {fm?.R:0} m, L {fm?.L:0} m");
        if (shotRails != null) { await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_luk_malowany.png")); }
        KeyPress(w, Key.Escape);
        w.SetTool(UtTool.Wskaz);
        w.View.SladClear();

        // --- Łuk z opisu planu
        w.View.Select(tv.Id, 1); w.RefreshTrackPanel();
        Ok(w.ArcPlanBoxes.Count == 7, "panel łuku: pola „Łuk z opisu planu” (R, L, T, Ł, α, km PŁ, km KŁ)");
        Type(w.ArcPlanBoxes["R"], "1000"); Type(w.ArcPlanBoxes["L"], "60");
        w.ApplyArcPlan(tv.Id, 1);
        Ok(d.P.Tracks.First(x => x.Id == tv.Id).Vertices[1].R == 800 && w.LastMessage.Contains("nie mieści się"), $"R 1000 z planu nie mieści się między prostymi – bez zmiany, komunikat: {w.LastMessage}");
        w.RefreshTrackPanel();
        Type(w.ArcPlanBoxes["R"], "700");
        w.ApplyArcPlan(tv.Id, 1);
        var v1 = d.P.Tracks.First(x => x.Id == tv.Id).Vertices[1];
        Ok(v1.R == 700 && v1.L == 60 && d.UndoName == "Łuk z planu" && w.LastMessage.Contains("R: plan 700"), $"R i L z planu: {w.LastMessage}");
        // kilometraż: km 10.000 na początku toru, rośnie z torem
        d.Change("Kilometraż", p => p.KmPoints.Add(new UtKmPoint { Track = tv.Id, S = 0, Km = 10000, Line = "1", Dir = 1 }));
        var t700 = d.P.Tracks.First(x => x.Id == tv.Id); var c700 = t700.Vertices.Select(x => x.Clone()).ToList(); c700[1].R = 650;
        var r700 = Trasa.Compute(c700, Trasa.KFor(t700)); double sP = 0, acc = 0;
        foreach (var e in r700.Elements) { if (e.Vertex == 1 && e.Kind != ElKind.Prosta) { sP = acc / r700.K; break; } acc += e.Len; }
        string kmP = Kilometraz.Fmt(10000 + sP);
        w.RefreshTrackPanel();
        Type(w.ArcPlanBoxes["R"], ""); Type(w.ArcPlanBoxes["PŁ"], kmP);
        w.ApplyArcPlan(tv.Id, 1);
        var v2 = d.P.Tracks.First(x => x.Id == tv.Id).Vertices[1];
        Ok(Math.Abs(v2.R - 650) <= 2 && w.LastMessage.Contains("km początku"), $"km początku z planu ({kmP}) → R {v2.R} m (km z rysunku przy R 650): {w.LastMessage}");
        w.RefreshTrackPanel();
        Type(w.ArcPlanBoxes["α"], "30");
        w.CompareArcPlan(tv.Id, 1, null);
        Ok(w.LastMessage.Contains("kąt zwrotu: plan 30") && w.LastMessage.Contains("różnica"), $"kąt z planu inny niż na rysunku – różnica: {w.LastMessage}");
        SladLinia(o, k);
        SladDwaLuki(o, k);
        await SladPrzeskok(w, o, k);
    }

    /// <summary>
    /// Dwa łuki w tę samą stronę z krótką prostą między nimi (zgłoszenie 2026-10-27, Andrzejów: tor rozjeżdżał się ze śladem do 4 m,
    /// ciąg „pominiętych” punktów i ostrzeżenie „przeskok”). Prawdziwe ślady po liniach OSM (tory wchodzące łukiem do stacji Łódź
    /// Andrzejów, węzły © współtwórcy OpenStreetMap, ODbL): łuk R ~900 m, prosta ok. 90 m, łuk R ~430 m; baza dobrana z rozrzutu (55–61 m)
    /// zlewała oba łuki w jeden (odchyłka średnia ok. 70 cm). Teraz oba łuki, tor przy śladzie, bez pominiętych punktów i ostrzeżeń.
    /// </summary>
    static void SladDwaLuki(XY o, double k)
    {
        var slady = new (string Nazwa, XY P0, double[] D)[] {
            ("tor 1,35 km", new XY(431512.44, 541463.15), new double[] {
            0.00, 0.00, -43.49, 56.59, -56.05, 73.16, -65.02, 85.00, -81.35, 106.76, -86.58, 113.74, -107.90, 142.39, -129.08, 171.26,
            -152.62, 203.35, -385.10, 520.78, -395.75, 535.02, -406.37, 549.11, -414.21, 559.17, -421.98, 569.02, -431.78, 581.07, -440.71, 591.50,
            -449.55, 601.61, -458.38, 611.55, -481.00, 636.54, -487.25, 643.36, -509.47, 667.64, -526.43, 686.17, -549.62, 711.08, -565.46, 727.42,
            -570.47, 732.33, -577.52, 739.14, -591.02, 751.44, -604.09, 762.39, -616.78, 771.86, -630.75, 781.76, -642.98, 789.73, -659.18, 799.27,
            -672.86, 806.68, -691.63, 815.68, -700.80, 819.76, -710.21, 823.67, -715.31, 825.71, -719.23, 827.17, -727.17, 830.14, -732.12, 831.82,
            -741.52, 834.82, -751.10, 837.70, -760.75, 840.34, -770.46, 842.78, -780.23, 845.00, -790.04, 846.94, -796.28, 848.04, -798.07, 848.35,
            -807.95, 849.95, -813.37, 850.63, -817.17, 851.10, -822.43, 851.67, -825.82, 852.03, -834.23, 852.77, -847.22, 853.53, -858.71, 853.97,
            -871.15, 854.13, -894.42, 854.01, -921.25, 853.51, -933.79, 853.27, -936.54, 853.21, -942.30, 853.07, -948.76, 852.90,
            }),
            ("tor 0,7 km", new XY(431130.27, 541986.63), new double[] {
            0.00, 0.00, -12.23, 16.52, -24.46, 32.72, -33.97, 44.78, -43.47, 56.46, -55.60, 70.82, -64.46, 80.94, -73.28, 90.82,
            -95.98, 115.85, -102.23, 122.67, -141.65, 165.69, -164.76, 190.48, -180.25, 206.59, -185.46, 211.70, -192.94, 218.96, -211.39, 235.30,
            -224.56, 245.89, -237.92, 255.93, -250.94, 264.90, -267.60, 275.17, -283.19, 283.85, -296.59, 290.74, -307.98, 295.87, -317.72, 300.18,
            -324.65, 303.11, -331.35, 305.75, -335.56, 307.29, -343.85, 310.30, -348.75, 311.96, -358.20, 314.98, -367.88, 317.88, -377.61, 320.55,
            -387.40, 323.01, -397.26, 325.25, -407.13, 327.20, -413.51, 328.37, -414.65, 328.58, -422.19, 329.90, -427.97, 330.71, -432.06, 331.28,
            -439.98, 332.25, -450.94, 333.34, -465.24, 334.34, -478.94, 334.91, -493.87, 335.14, -512.81, 335.08, -538.59, 334.55, -541.00, 334.50,
            -555.57, 334.20,
            }) };
        foreach (var (nazwa, p0, dd) in slady)
        {
            var pts = new List<XY>(); for (int i = 0; i + 1 < dd.Length; i += 2) pts.Add(p0 + new XY(dd[i], dd[i + 1]));
            double kk = Geo.ScaleFactor(p0);
            var st = Slad.TorPoSladzie(pts, kk);
            var arcs = st.A?.Els.Where(e => e.Luk && Math.Abs(e.R0) < 3000).ToList() ?? new();
            Ok(st.A != null && arcs.Count == 2 && arcs[0].R0 > 600 && arcs[0].R0 < 1300 && Math.Abs(arcs[1].R0 - 430) < 30 && st.Rms < 0.2 && st.Max < 0.5 && st.Pominiete.Count == 0 && (st.Przeskoki?.Count ?? 0) == 0,
                $"Andrzejów, {nazwa} z OSM – dwa łuki z prostą ok. 90 m (baza dobrana {Slad.BazaAuto(pts, kk):0} m): " +
                (st.A == null ? st.Error : $"łuki R {string.Join(" / ", arcs.Select(e => e.R0.ToString("0")))}, baza {st.Baza:0} m, odchyłka {st.Rms * 100:0} / {st.Max * 100:0} cm, pominięte {st.Pominiete.Count}, ostrzeżenia {st.Przeskoki?.Count ?? 0}"));
        }
    }

    /// <summary>
    /// Przeskok śladu na sąsiedni tor (zgłoszenie 2026-10-27: ślad po liniach OSM poszedł przez połączenie torów na równoległy tor
    /// 4,5 m obok i z powrotem, a tor po śladzie po cichu pominął ten odcinek – kilka „pominięty 4xx cm”). Dwa tory proste z dwoma
    /// połączeniami, klik na torze 1, na torze 2 między połączeniami i znów na torze 1: ostrzeżenie o przeskoku zamiast samych pominięć.
    /// </summary>
    static async Task SladPrzeskok(MainWindow w, XY o0, double k)
    {
        var o = o0 + new XY(-3000, 2000); double az = 0.7;
        var dir = Element.Dir(az); var right = Element.Right(az);
        XY P(double s, double off) => o + dir * (s * k) + right * (off * k);
        var net = new OsmSiec(); long id = 1; var t1 = new List<long>(); var t2 = new List<long>(); var n1 = new Dictionary<int, long>(); var n2 = new Dictionary<int, long>();
        for (int s = 0; s <= 600; s += 10) { net.Nodes[id] = P(s, 0); n1[s] = id; t1.Add(id++); net.Nodes[id] = P(s, 4.5); n2[s] = id; t2.Add(id++); }
        net.Ways.Add(t1.ToArray()); net.Ways.Add(t2.ToArray());
        net.Ways.Add(new[] { n1[200], n2[230] }); net.Ways.Add(new[] { n2[330], n1[360] });
        net.Areas.Add((o + new XY(-3000, -3000), o + new XY(3000, 3000)));
        w.View.Osm = net;
        w.SetTool(UtTool.Slad); w.View.SladClear();
        w.View.FitPoints(new[] { P(0, 0), P(600, 0) }); await Settle(w);
        Point S(XY p) => w.View.ToScreen(p);
        w.View.SimClick(S(P(5, 0))); w.View.SimClick(S(P(280, 4.5))); w.View.SimClick(S(P(595, 0)));
        int na = w.View.SladPts.Count(q => Math.Abs((q - o).X * right.X + (q - o).Y * right.Y) / k > 3);
        KeyPress(w, Key.Enter);
        await WaitFor(() => !w.View.SladBusy, 60000);
        var pv = w.View.SladPreview; var pr = pv?.Przeskoki ?? new();
        Ok(na >= 5 && pv?.A != null && pr.Count == 1 && pr[0].S0 > 150 && pr[0].S1 < 420 && pr[0].Max > 3.5 && w.View.SladPreviewInfo().StartsWith("UWAGA: ślad przeskakuje na sąsiedni tor"),
            $"ślad przez połączenie na tor 4,5 m obok ({na} pkt) – ostrzeżenie o przeskoku: {(pv == null ? "brak propozycji" : w.View.SladPreviewInfo())}");
        if (shotRails != null) { await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_slad_przeskok.png")); }
        KeyPress(w, Key.Escape); KeyPress(w, Key.Escape);
        // ślad po jednym torze – bez ostrzeżenia
        w.View.SimClick(S(P(5, 0))); w.View.SimClick(S(P(595, 0)));
        KeyPress(w, Key.Enter);
        await WaitFor(() => !w.View.SladBusy, 60000);
        pv = w.View.SladPreview;
        Ok(pv?.A != null && (pv.Przeskoki?.Count ?? 0) == 0 && !w.View.SladPreviewInfo().Contains("UWAGA"), $"ślad po jednym torze – bez ostrzeżenia: {w.View.SladPreviewInfo()}");
        KeyPress(w, Key.Escape); KeyPress(w, Key.Escape);
        w.View.Osm = null; w.SetTool(UtTool.Wskaz);
    }

    /// <summary>
    /// Tor po śladzie na długiej linii z OSM (zgłoszenie 2026-10-20: tor z osi ORM nie dał się zaznaczyć ani edytować – dopasowanie robiło
    /// pętle i wybiegało za koniec śladu, łuki R ~80 km dawały wierzchołki kilometry dalej, łuki bez prostej się nie mieściły). Linia
    /// ok. 2,3 km z łukami R 4000, 1300, 3300 (w lewo), 1700 m, węzły co 10–15 m z rozrzutem ±0,45 m, kilka losowań: wynik to poprawny
    /// wielobok blisko prawdziwej osi (bez pętli, długość jak śladu, R nie mniejszy niż 140 m) albo komunikat – nigdy tor przez pół mapy.
    /// </summary>
    static void SladLinia(XY o, double k)
    {
        var truthA = new UtAlign { X = o.X + 3000, Y = o.Y, Az = 1.1, Els = new() {
            new() { Len = 250 }, new() { Len = 40, R1 = 4000 }, new() { Len = 180, R0 = 4000, R1 = 4000 }, new() { Len = 40, R0 = 4000 },
            new() { Len = 300 }, new() { Len = 70, R1 = 1300 }, new() { Len = 160, R0 = 1300, R1 = 1300 }, new() { Len = 70, R0 = 1300 },
            new() { Len = 200 }, new() { Len = 210, R0 = -3300, R1 = -3300 }, new() { Len = 260 },
            new() { Len = 60, R1 = 1700 }, new() { Len = 140, R0 = 1700, R1 = 1700 }, new() { Len = 60, R0 = 1700 }, new() { Len = 250 } } };
        var truth = TrasaElem.Compute(truthA, k);
        int good = 0, msg = 0, bad = 0; var info = new List<string>();
        for (int seed = 1; seed <= 6; seed++)
        {
            var rnd = new Random(seed * 101); var pts = new List<XY>();
            for (double s = 0; s <= truth.Length; s += 10 + rnd.NextDouble() * 5)
            { var (q, az) = truth.At(s); pts.Add(q + Element.Right(az) * ((rnd.NextDouble() * 2 - 1) * 0.45 * k)); }
            var st = Slad.TorPoSladzie(pts, k);
            if (st.A == null) { msg++; info.Add($"{seed}: {st.Error}"); continue; }
            var (v, _) = TrasaElem.ToPolygon(st.A, k); var r = Trasa.Compute(v, k);
            double away = r.Sample(2).Max(q => Budowa.Dist(truth, q.P)) / k;
            bool ok = r.Ok && away < 3 && Math.Abs(r.Length - truth.Length) < 20 && v.All(x => x.R == 0 || x.R >= 140);
            if (ok) good++; else bad++;
            info.Add($"{seed}: {(ok ? "" : "ZŁY ")}{v.Count} wierzch., R {string.Join("/", v.Where(x => x.R > 0).Select(x => x.R.ToString("0")))}, od osi ≤ {away:0.0} m, {r.Length:0}/{truth.Length:0} m");
        }
        Ok(bad == 0 && good >= 4, $"tor po śladzie na linii 2,3 km z OSM (6 losowań): dobre {good}, z komunikatem {msg}, złe {bad} – {string.Join("; ", info)}");
        // wielobok z projektu właściciela (tor z ORM sprzed poprawki): łuk R 79 915 m przy wieloboku zawracającym – dawniej oś 255 km
        // i zamrożony edytor (każde przeliczenie i klik), teraz załamanie z błędem
        var zly = new List<UtVertex> {
            new() { X = 433530.1, Y = 534678.0 }, new() { X = 433497.1, Y = 535001.0, R = 3870 }, new() { X = 433468.8, Y = 535310.2, R = 494 },
            new() { X = 433464.3, Y = 535343.2, R = 1154 }, new() { X = 433678.7, Y = 533116.4, R = 79915 }, new() { X = 433340.1, Y = 536632.3, R = 22826 },
            new() { X = 433339.9, Y = 536634.6 } };
        var rz = Trasa.Compute(zly, Geo.ScaleFactor(zly[0].P));
        Ok(rz.Length < 20000 && rz.Notes.Any(n => n.Error && n.Text.Contains("zupełnie się nie mieści")), $"łuk, który zupełnie się nie mieści – załamanie zamiast osi na setki km: długość {rz.Length / 1000:0.0} km, {rz.Notes.Count(n => n.Error)} błędów");
    }
}

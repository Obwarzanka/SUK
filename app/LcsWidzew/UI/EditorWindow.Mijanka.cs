using System.IO;
using System.Windows;
using LcsWidzew.Model;
using LcsWidzew.Sim;

namespace LcsWidzew.UI;

public partial class EditorWindow
{
    /// <summary>
    /// Test (--editshot … mijanka out.json [skos°]): prosta mijanka budowana narzędziami jak myszą na planie, na torze
    /// pochylonym jak na obróconym zdjęciu – tor 1 i tor 2, dwa rozjazdy, semafory wjazdowe i wyjazdowe, dwa szlaki, peron.
    /// Potem: kontrola, tablica przebiegów, nastawienie przebiegów w symulatorze, zobrazowanie. Zrzut planu i plik z wynikiem.
    /// </summary>
    public string MijankaForTest(string outJson, double skewDeg)
    {
        var log = new System.Text.StringBuilder(); int bad = 0;
        void Ok(bool c, string m) { log.AppendLine((c ? "OK  " : "BŁĄD") + " " + m); if (!c) bad++; }
        doc = StationDoc.New("MJ", "Mijanka"); path = Path.Combine(Path.GetTempPath(), "lcs_mijanka.json"); dispMode = false; Rebuild(true);
        PlanForTest(); chkSnap.IsChecked = false; ApplyModeToolbars();
        double ux = doc.Ux, uy = doc.Uy, ang = skewDeg * Math.PI / 180;
        Vector U = new(Math.Cos(ang), Math.Sin(ang)), N = new(-Math.Sin(ang), Math.Cos(ang));
        // s – metry wzdłuż toru 1, off – metry w bok (tor 2 – 4,75 m „pod” torem 1)
        Point P(double s, double off) => new Point(0, 0) + U * (s * ux) + N * (off * uy);
        // przybliżenie jak przy rysowaniu po zdjęciu: 1 m ≈ 3 px (zasięg łapania ≈ 3,3 m < rozstaw 4,75 m)
        var c = P(900, 2); ZoomForTest(c.X, c.Y, 3 / ux);
        // jak myszą: widok przesunięty tak, żeby punkt był na ekranie (przesuwanie widoku w trakcie rysowania)
        void Click(Tool t, Point p) { if (tool != t) SetTool(t); panel.CenterOn(p.X, p.Y); panel.UpdateLayout(); mouse = p; Down(p, null); Up(p, null); }
        // 1. tor 1 – szlak zachodni, stacja, szlak wschodni (jeden ciąg)
        Click(Tool.Track, P(0, 0)); Click(Tool.Track, P(1800, 0)); Right(new Point());
        // 2. tor 2 (krótszy) obok
        Click(Tool.Track, P(520, 4.75)); Click(Tool.Track, P(1280, 4.75)); Right(new Point());
        log.AppendLine($"tory: {doc.Edges.Count}, węzły: {doc.Nodes.Count}");
        // 3. rozjazdy: z toru 1 do końców toru 2 (1:9 → 42,75 m)
        Click(Tool.Turnout, P(477, 0)); Click(Tool.Turnout, P(520, 4.75));
        log.AppendLine("rozjazd zachodni: " + status.Text);
        Click(Tool.Turnout, P(1323, 0)); Click(Tool.Turnout, P(1280, 4.75));
        log.AppendLine("rozjazd wschodni: " + status.Text);
        Ok(doc.Switches.Count == 2, $"zwrotnice: {string.Join(", ", doc.Switches.Select(x => x.Key))}");
        // 4. semafory
        // semafor jak myszą: klik na torze, klik po prawej stronie toru patrząc w kierunku jazdy, dla której obowiązuje
        void Sig(string name, string typ, bool east, Point p) { var side = p + N * ((east ? 3 : -3) * uy); AutoAnswers.Enqueue(new[] { name, typ, "", "" }); Click(Tool.Signal, p); Click(Tool.Signal, side); log.AppendLine($"semafor {name}: {status.Text}"); }
        Sig("A", "wjazdowy", true, P(300, 0)); Sig("B", "wjazdowy", false, P(1500, 0));
        Sig("F1", "wyjazdowy", false, P(560, 0)); Sig("F2", "wyjazdowy", false, P(600, 4.75));
        Sig("D1", "wyjazdowy", true, P(1240, 0)); Sig("D2", "wyjazdowy", true, P(1200, 4.75));
        Ok(doc.Signals.Count == 6, $"sygnalizatory: {doc.Signals.Count}");
        // 5. szlaki
        AutoAnswers.Enqueue(new[] { "1Z", "Sąsiad Zachód", "1", "1", "zewnętrzny", "liniowa bez odstępów", "WYJAZD z tej stacji" }); Click(Tool.Szlak, P(300, 0));
        log.AppendLine("szlak Z: " + status.Text);
        AutoAnswers.Enqueue(new[] { "1W", "Sąsiad Wschód", "1", "1", "zewnętrzny", "liniowa bez odstępów", "WYJAZD z tej stacji" }); Click(Tool.Szlak, P(1500, 0));
        log.AppendLine("szlak W: " + status.Text);
        Ok(doc.Blocks.Count == 2, $"szlaki: {string.Join(", ", doc.Blocks.Select(b => b.Key))}");
        // 6. peron między torami? – przy torze 2 (od strony zewnętrznej), 200 m
        AutoAnswers.Enqueue(new[] { "Peron 1" });
        Click(Tool.Platform, P(800, 6.45)); Click(Tool.Platform, P(1000, 6.45)); Click(Tool.Platform, P(900, 12.45));
        Ok(doc.Platforms.Count == 1, $"peron: {status.Text}");
        SetTool(Tool.Select);
        // 7. kontrola i model
        var val = doc.Validate(); log.AppendLine("Sprawdź: " + (val.Count == 0 ? "bez uwag" : string.Join(" | ", val)));
        foreach (var pr in doc.Problems()) log.AppendLine("  problem: " + pr.Msg);
        try
        {
            var stt = doc.Build(); var sm = new Simulator(stt); new Trains(sm);
            foreach (var sw in stt.Switches.Values) log.AppendLine($"  zwrotnica {sw.Label}: iglica {sw.Tip}, (+) {sw.Plus}, (−) {sw.Minus}");
            foreach (var s in stt.Signals.Values) log.AppendLine($"  {s.Label} {s.Type} kier. {s.Dir} węzeł {s.Node}");
            int nr = sm.RouteTable.Values.Sum(l => l.Count);
            log.AppendLine($"Przebiegi: {nr}");
            foreach (var (k, l) in sm.RouteTable) if (l.Count > 0) log.AppendLine($"  {Station.Lbl(k),-4} {string.Join(", ", l.Select(r => r.Id))}");
            Ok(nr > 0, "tablica przebiegów niepusta");
            sm.User = new User { Name = "T", Role = "dyżurny ruchu" };
            foreach (var cmd in new[] { "A D1 Pociąg", "A D2 Pociąg", "B F1 Pociąg", "B F2 Pociąg", "F1 1Z Pociąg", "F2 1Z Pociąg", "D1 1W Pociąg", "D2 1W Pociąg" })
            {
                var s1 = new Simulator(doc.Build()); new Trains(s1); s1.User = sm.User;
                var m = s1.Exec(cmd).Msg; log.AppendLine($"  {cmd}: {m}"); Ok(m.Contains("w realizacji") || m.Contains("nastaw"), cmd);
            }
        }
        catch (Exception ex) { Ok(false, "model: " + ex.Message); }
        doc.Save(outJson);
        var dir = Path.GetDirectoryName(Path.GetFullPath(outJson)) ?? ".";
        void Shot(string name) { UpdateLayout(); panel.InvalidateVisual(); UpdateLayout(); var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); rtb.Render(this); var enc = new System.Windows.Media.Imaging.PngBitmapEncoder(); enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb)); using var f = File.Create(Path.Combine(dir, name)); enc.Save(f); log.AppendLine("zrzut: " + name); }
        var w0 = P(480, 2); ZoomForTest(w0.X, w0.Y, 4 / ux); Shot("mij_rozjazd_zach.png");
        var w1 = P(900, 6); ZoomForTest(w1.X, w1.Y, 2.2 / ux); Shot("mij_peron.png");
        ZoomForTest(c.X, c.Y, panel.ActualWidth / (1900 * ux)); Shot("mij_plan_calosc.png");
        ToggleDisplayMode(); panel.FitPage(); Shot("mij_zobrazowanie.png");
        log.AppendLine("zobrazowanie: " + status.Text);
        ToggleDisplayMode();
        ZoomForTest(c.X, c.Y, panel.ActualWidth / (1900 * ux));
        log.AppendLine($"błędów testu: {bad}, błędów edytora: {Errors.Count}");
        return log.ToString();
    }
}

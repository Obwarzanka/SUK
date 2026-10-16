using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Lcs.Przechylka;

/// <summary>
/// Kalkulator przechyłki i krzywej przejściowej. Bez argumentów – okno; <c>--test wynik.txt [zrzut.png]</c> – sprawdzenie obliczeń
/// i okna (ostatnia linia „błędów: N”).
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pl-PL");
        var app = new Application();
        var w = new MainWindow();
        if (args.Length >= 2 && args[0] == "--test")
        {
            string outPath = args[1], shot = args.Length >= 3 ? args[2] : null;
            w.Left = SystemParameters.VirtualScreenLeft - 3000; w.ShowActivated = false; w.ShowInTaskbar = false; w.WindowStartupLocation = WindowStartupLocation.Manual;
            w.ContentRendered += (s, e) => w.Dispatcher.BeginInvoke(() => { Test.Run(w, outPath, shot); app.Shutdown(); }, DispatcherPriority.ApplicationIdle);
        }
        app.Run(w);
    }

    public static void Save(Window w, string path)
    {
        w.UpdateLayout();
        var rtb = new RenderTargetBitmap((int)w.ActualWidth, (int)w.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(w);
        var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
        using var f = File.Create(path); enc.Save(f);
    }
}

static class Test
{
    public static void Run(MainWindow w, string outPath, string shot)
    {
        var log = new System.Text.StringBuilder(); int err = 0;
        void Ok(bool c, string what) { log.AppendLine((c ? "OK    " : "BŁĄD  ") + what); if (!c) err++; }
        var d = new Dopuszczalne();
        var r = Obliczenia.Licz(100, 600, 120, d);
        Ok(Math.Abs(r.Hr - 196.67) < 0.01 && Math.Abs(r.Hn - 76.67) < 0.01, $"V 100, R 600: h_r = {r.Hr:0.00} mm, przy h 120 niedobór {r.Hn:0.00} mm");
        Ok(Math.Abs(r.LRampa - 120) < 1e-9 && Math.Abs(r.LF - 95.24) < 0.01 && Math.Abs(r.LPsi - 27.86) < 0.01 && r.L == 120 && r.Decyduje == "rampa przechyłkowa",
            $"L: rampa {r.LRampa:0.0}, ψ {r.LPsi:0.0}, f {r.LF:0.0} → L = {r.L:0} m (rampa)");
        var p = Obliczenia.Licz(100, 600, null, d);
        Ok(p.HProponowana && p.H == 90 && p.Hn <= d.HnMax, $"bez h – proponowana {p.H:0} mm (niedobór {p.Hn:0.0} ≤ {d.HnMax:0})");
        var z = Obliczenia.Licz(160, 300, null, d);
        Ok(z.H == d.HMax && z.Uwagi.Any(u => u.Contains("promień za mały")), "R 300 przy 160 km/h – uwaga „promień za mały”");
        var n = Obliczenia.Licz(40, 1000, 100, d);
        Ok(n.Uwagi.Any(u => u.Contains("nadmiar")), "mała prędkość, duża przechyłka – uwaga o nadmiarze");
        // okno: wpisanie wartości jak użytkownik
        w.VBox.Text = "100"; w.RBox.Text = "600"; w.HBox.Text = "120";
        Ok(w.LText.Text.StartsWith("120"), $"okno: V 100, R 600, h 120 → „{w.LText.Text}”");
        w.HBox.Text = "";
        Ok(w.HText.Text.Contains("90"), $"okno: puste h – propozycja „{w.HText.Text}”");
        w.RBox.Text = "abc";
        Ok(w.LText.Text.Length == 0 || w.LText.Text == "–", "okno: zły promień – brak wyniku");
        w.RBox.Text = "600"; w.HBox.Text = "120";
        if (shot != null) Program.Save(w, shot);
        log.AppendLine($"błędów: {err}");
        File.WriteAllText(outPath, log.ToString());
    }
}

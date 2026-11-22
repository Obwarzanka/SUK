using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Lcs.Powtarzacz;

/// <summary>
/// Powtarzacz zdalnej kontroli przejazdów (UZK) – osobny program obok pulpitu LCS. Uruchomienie:
///   Powtarzacz.exe                          – praca z grą (mod SimRailMost ≥ 0.12, UDP 127.0.0.1:47134)
///   Powtarzacz.exe --port 47134             – inny port (jak UdpPortPowtarzacz w MelonPreferences.cfg)
///   Powtarzacz.exe --demo                   – dane przykładowe, bez gry
///   Powtarzacz.exe --demo --demo-t 20       – dane przykładowe od 20. sekundy cyklu
///   Powtarzacz.exe --widok rasp [--ekran II] – widok RASP-UZK (Kombud), ekran I / II / III; --widok ie104 – pola wg Ie-104
///   Powtarzacz.exe --shot zrzut.png [--live] – zrzut okna po 3 s (z --live – z gry, po 8 s) i zamknięcie
/// </summary>
public static class Program
{
    public static readonly string ErrorLog = Path.Combine(AppContext.BaseDirectory, "bledy.log");

    [STAThread]
    public static void Main(string[] args)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        app.DispatcherUnhandledException += (s, e) =>
        {
            try { File.AppendAllText(ErrorLog, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {e.Exception}{Environment.NewLine}"); } catch { }
            e.Handled = true;
            MessageBox.Show("Wystąpił błąd – program działa dalej.\n\n" + e.Exception.Message, "Powtarzacz", MessageBoxButton.OK, MessageBoxImage.Warning);
        };
        int shot = Array.IndexOf(args, "--shot");
        bool live = args.Contains("--live");
        bool demo = args.Contains("--demo") || (shot >= 0 && !live);
        int ip = Array.IndexOf(args, "--port");
        int port = ip >= 0 && ip + 1 < args.Length && int.TryParse(args[ip + 1], out var pv) ? pv : Ustawienia.Wczytaj().Port;
        int it = Array.IndexOf(args, "--demo-t");   // demo od chwili t [s] (zrzuty stanów, np. awaria od 5 s)
        double demoT = it >= 0 && it + 1 < args.Length && double.TryParse(args[it + 1], out var tv) ? tv : 0;
        string Arg(string n) { int i = Array.IndexOf(args, n); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        var w = new MainWindow(demo, port, demoT, Arg("--widok"), Arg("--ekran"));
        if (shot >= 0 && shot + 1 < args.Length)
        {
            var plik = Path.GetFullPath(args[shot + 1]);
            w.Loaded += (_, _) =>
            {
                var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(live ? 8 : 3) };
                t.Tick += (_, _) => { t.Stop(); Zrzut(w, plik); w.Close(); };
                t.Start();
            };
        }
        app.Run(w);
    }

    static void Zrzut(Window w, string plik)
    {
        var el = (FrameworkElement)w.Content;
        var dpi = VisualTreeHelper.GetDpi(w);
        var bmp = new RenderTargetBitmap((int)(el.ActualWidth * dpi.DpiScaleX), (int)(el.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bmp.Render(el);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var f = File.Create(plik);
        enc.Save(f);
    }
}

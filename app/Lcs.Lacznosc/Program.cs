using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Lcs.Lacznosc;

/// <summary>
/// Łączność z posterunkami. Uruchomienie:
///   Lacznosc.exe                       – praca z grą (mod SimRailMost 0.10+, UDP 127.0.0.1:47131)
///   Lacznosc.exe --demo                – dane przykładowe, bez gry i bez sieci
///   Lacznosc.exe --shot zrzut.png [--live] – zrzut okna po 2 s (z --live – z gry, po 8 s) i zamknięcie
///   --zbiorczy – start w widoku zbiorczym
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
            MessageBox.Show("Wystąpił błąd – program działa dalej.\n\n" + e.Exception.Message, "Łączność", MessageBoxButton.OK, MessageBoxImage.Warning);
        };
        int shot = Array.IndexOf(args, "--shot");
        bool live = args.Contains("--live");
        bool demo = args.Contains("--demo") || (shot >= 0 && !live);
        var w = new MainWindow(demo);
        if (args.Contains("--zbiorczy")) w.Loaded += (_, _) => w.PokazZbiorczy();
        if (shot >= 0 && shot + 1 < args.Length)
        {
            var plik = Path.GetFullPath(args[shot + 1]);
            w.Loaded += (_, _) =>
            {
                var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(live ? 8 : 2) };
                t.Tick += (_, _) =>
                {
                    t.Stop(); Zrzut(w, plik);
                    if (args.Contains("--okna"))   // zrzuty też okna rozmowy i opcji (…_rozmowa.png, …_opcje.png)
                    {
                        w.PokazZbiorczy();
                        var r = app.Windows.OfType<OknoRozmowy>().First();
                        r.UpdateLayout(); Zrzut(r, Path.ChangeExtension(plik, null) + "_rozmowa.png");
                        var o = new OknoOpcji(new Ustawienia(), new[] { "Gałkówek", "Rogów", "Żakowice Południowe", "Rokiciny" },
                            new() { ["Rogów"] = true, ["Żakowice Południowe"] = true, ["Gałkówek"] = false, ["Rokiciny"] = false }) { Owner = w };
                        o.Show(); o.UpdateLayout(); Zrzut(o, Path.ChangeExtension(plik, null) + "_opcje.png"); o.Close();
                    }
                    w.Close();
                };
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

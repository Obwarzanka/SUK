using System.IO;
using System.Windows;

namespace Lcs.EdytorZobrazowania2;

/// <summary>
/// Nowy edytor zobrazowania (obrazy pulpitu EbiScreen wg Ie-104, PLAN_EDYTOR_ZOBRAZOWANIA_V2.md): najpierw rysunek, potem przypięcie do UT.
/// Uruchomienie:
///   EdytorZobrazowania2.exe [plik.zob.json | plik.ut.json]
///   EdytorZobrazowania2.exe --test wynik.txt [zrzut.png]   – test automatyczny (operacje jak użytkownik), zrzut okna
///   EdytorZobrazowania2.exe --shot zrzut.png plik.zob.json  – sam zrzut okna z całym obrazem
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
            if (Test.Running) { Test.Fail(e.Exception); return; }
            MessageBox.Show("Wystąpił błąd – operacja została przerwana, edytor działa dalej.\n\n" + e.Exception.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
        };
        MainWindow w;
        if (args.Length > 1 && args[0] == "--test")
        {
            w = new MainWindow(null) { NoPrompt = true };
            Test.Start(w, args[1], args.Length > 2 ? args[2] : null);
        }
        else if (args.Length > 2 && args[0] == "--shot")
        {
            w = new MainWindow(args[2]) { NoPrompt = true };
            Test.Shot(w, args[1]);
        }
        else w = new MainWindow(args.Length > 0 ? args[0] : null);
        app.Run(w);
    }
}

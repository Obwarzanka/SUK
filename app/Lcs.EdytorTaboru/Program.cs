using System.IO;
using System.Windows;

namespace Lcs.EdytorTaboru;

/// <summary>
/// Edytor taboru (PLAN_EDYTOR_TABORU.md, etap B3). Uruchomienie:
///   EdytorTaboru.exe [tabor.json]              – bez pliku: wspólny katalog (app/Lcs.Tabor/Data/tabor.json w repozytorium)
///   EdytorTaboru.exe --test wynik.txt [zrzut.png] – test automatyczny (operacje jak użytkownik) na kopii katalogu, zrzuty okna
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
            w = new MainWindow(null, open: false) { NoPrompt = true };
            Test.Start(w, args[1], args.Length > 2 ? args[2] : null);
        }
        else w = new MainWindow(args.Length > 0 ? args[0] : null);
        app.Run(w);
    }
}

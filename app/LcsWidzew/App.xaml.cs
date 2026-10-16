using System.IO;
using System.Windows;

namespace LcsWidzew;

public partial class App : Application
{
    /// <summary>Plik układu: przy uruchomieniu z katalogu projektu – Data/station.json projektu (edycje nie giną przy kompilacji), inaczej kopia obok programu.</summary>
    public static string StationPath { get; set; } = FindStation();
    static string FindStation()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 6 && d != null; i++, d = d.Parent)
        {
            var p = Path.Combine(d.FullName, "Data", "station.json");
            if (File.Exists(Path.Combine(d.FullName, "LcsWidzew.csproj")) && File.Exists(p)) return p;
        }
        return Path.Combine(AppContext.BaseDirectory, "Data", "station.json");
    }

    public static string ErrorLog => Path.Combine(Path.GetDirectoryName(StationPath) ?? AppContext.BaseDirectory, "..", "bledy.log");
    public static void LogError(Exception ex, string where)
    {
        try { File.AppendAllText(ErrorLog, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {where}: {ex}\n\n"); } catch { }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // --station <plik> – inny plik układu (np. kopia)
        var args = e.Args.ToList();
        int si = args.IndexOf("--station");
        if (si >= 0 && si + 1 < args.Count) { StationPath = Path.GetFullPath(args[si + 1]); args.RemoveRange(si, 2); }
        var A = args.ToArray();
        DispatcherUnhandledException += (s, a) =>
        {
            LogError(a.Exception, "UI");
            a.Handled = true;
            if (Current?.MainWindow?.IsVisible == true || Current?.Windows.Count > 0)
                MessageBox.Show("Wystąpił błąd – operacja została przerwana, program działa dalej.\n\n" + a.Exception.Message + "\n\nSzczegóły: " + Path.GetFullPath(ErrorLog), "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
        };
        if (A.Length > 0 && A[0] == "--selftest")
        {
            if (A.Length > 3 && A[3] == "wzorce") Sim.SelfTest.RunPatterns(StationPath, A[1], A[2]);
            else if (A.Length > 3 && A[3].StartsWith("tylko:")) Sim.SelfTest.RunScenario(StationPath, A[1], A[2], A[3][6..]);
            else if (A.Length > 2) Sim.SelfTest.RunScenario(StationPath, A[1], A[2]);
            else Sim.SelfTest.Run(StationPath, A.Length > 1 ? A[1] : "selftest.txt");
            Shutdown();
            return;
        }
        if (A.Length > 1 && A[0] == "--trasy")
        {
            // diagnostyka: tablica przebiegów pliku i orientacja torów
            string Dump(Model.Station s) => string.Join(Environment.NewLine, new Sim.Simulator(s).RouteTable.SelectMany(kv => kv.Value.Select(r => $"{kv.Key} {r.Id} [{r.SwText}]")).OrderBy(x => x));
            File.WriteAllText(A[1] + ".a.txt", Dump(Model.Station.Load(StationPath)));
            { var rj = (System.Text.Json.Nodes.JsonObject)System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(StationPath)); var rr = Model.TrackOrientation.Apply(rj); var r2 = Model.TrackOrientation.Apply(rj);
              File.WriteAllText(A[1] + ".o.txt", $"migracja: odwrócone {rr.Flipped}, sprzeczne węzły {rr.Conflicts.Count}: {string.Join(" ", rr.Conflicts)} | normalizacja po migracji: odwrócone {r2.Flipped}, sprzeczne {r2.Conflicts.Count}: {string.Join(" ", r2.Conflicts)}"); }
            Shutdown(); return;
        }
        base.OnStartup(e);
        // zwykłe uruchomienie bez --station: wybór wersji układu (podstawowy albo projekt z Data/projekty)
        if (si < 0 && A.Length == 0)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var chosen = UI.LayoutChooser.Choose(StationPath);
            if (chosen == null) { Shutdown(); return; }
            StationPath = chosen;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
        }
        var w = new MainWindow();
        MainWindow = w;
        if (A.Length > 1 && A[0] == "--screenshot")
        {
            w.ShotPath = A[1]; w.WindowState = WindowState.Normal; w.Width = 1600; w.Height = 950;
            if (A.Length > 2) w.ShotSeconds = double.Parse(A[2], System.Globalization.CultureInfo.InvariantCulture);
            w.ShotCmds = A.Skip(3).ToArray();
        }
        w.Show();
    }
}

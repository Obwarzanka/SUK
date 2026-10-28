using System.Diagnostics;
using System.IO;
using System.Windows;
using Lcs.Logi;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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

    public static string ErrorLogDirectory => Diagnostyka.Silnik?.Katalog;
    public static void LogError(Exception ex, string where)
    {
        // logi techniczne (25): zredagowany wyjątek do ograniczonej kolejki, bez I/O na tym wątku
        ZdarzeniaProcesu.Blad(logApp, ex, where switch { "UI" => "UI_DISPATCHER", "serwer" => "NET_SERVER", "stanowisko" => "NET_CLIENT", _ => "OTHER" });
    }

    // ------------------------------------------------------------------ logi techniczne (S01.01, rozdział 25 planu)
    static ILogger logApp = NullLogger.Instance;
    static readonly long startProcesu = Stopwatch.GetTimestamp();
    static string logiRaport, wynikProby;
    public static string Wersja => typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>
    /// Start logów: JSONL w %LocalAppData%\SUK\LcsWidzew\Logs (albo katalog z --logi). Działa offline; błąd logów nie zatrzymuje programu.
    /// --logi-raport plik – stan i liczniki logów przy zamknięciu; --logi-proba – kontrolny błąd z testowymi sekretami (L01/L05) i pomiar emisji (B05).
    /// </summary>
    static void StartLogow(string katalog, string tryb, bool proba)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(logiRaport)) logiRaport = Path.GetFullPath(logiRaport);
            katalog = string.IsNullOrWhiteSpace(katalog) ? null : Path.GetFullPath(katalog);
            if (!Diagnostyka.Start(new OpcjeLogow { Aplikacja = "LcsWidzew", Katalog = katalog, Profil = ProfilLogow.Klient })) return;
            logApp = Diagnostyka.Modul("LcsWidzew.App", Wersja);
            ZdarzeniaProcesu.Uruchomiono(logApp, tryb);
            if (!proba) return;
            ProbaLogow.Wykonaj(logApp);
            var rytm = ProbaLogow.Pomiar(logApp);
            var zalew = ProbaLogow.Pomiar(logApp, 1, 5000, 0);
            wynikProby = $"pomiar_rytm_ticku: {rytm}\npomiar_zalew: {zalew}\n";
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            logiRaport = null;   // błędna ścieżka z wiersza poleceń – program działa bez logów technicznych
        }
    }

    static string Zdejmij(List<string> args, string nazwa, bool zWartoscia)
    {
        int i = args.IndexOf(nazwa);
        if (i < 0) return null;
        if (!zWartoscia) { args.RemoveAt(i); return ""; }
        if (i + 1 >= args.Count) { args.RemoveAt(i); return null; }
        var v = args[i + 1]; args.RemoveRange(i, 2); return v;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ZdarzeniaProcesu.Zatrzymano(logApp, (long)Stopwatch.GetElapsedTime(startProcesu).TotalMicroseconds);
        logApp = NullLogger.Instance;
        var st = Diagnostyka.Zatrzymaj();
        if (logiRaport != null)
            try { File.WriteAllText(logiRaport, (st?.ToString() ?? "logi=nie_uruchomione\n") + (wynikProby ?? "")); } catch { }
        base.OnExit(e);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // --station <plik> – inny plik układu (np. kopia)
        var args = e.Args.ToList();
        // --logi <katalog>, --logi-raport <plik>, --logi-proba – logi techniczne (testy na kopii danych)
        var logiKatalog = Zdejmij(args, "--logi", true);
        logiRaport = Zdejmij(args, "--logi-raport", true);
        bool logiProba = Zdejmij(args, "--logi-proba", false) != null;
        string tryb = args.Contains("--selftest") ? "selftest" : args.Contains("--trasy") ? "trasy" : args.Contains("--screenshot") ? "zrzut" : "pulpit";
        StartLogow(logiKatalog, tryb, logiProba);
        // EDR: dzienniki zapisywane do pliku obok układu tylko w pracy na pulpicie (selftest, zrzuty i testy nie zostawiają plików obok układu)
        Sim.Edr.ZapisDziennikow = tryb == "pulpit" && !args.Contains("--edrtest");
        int si = args.IndexOf("--station");
        if (si >= 0 && si + 1 < args.Count) { StationPath = Path.GetFullPath(args[si + 1]); args.RemoveRange(si, 2); }
        var A = args.ToArray();
        DispatcherUnhandledException += (s, a) =>
        {
            LogError(a.Exception, "UI");
            a.Handled = true;
            if (Current?.MainWindow?.IsVisible == true || Current?.Windows.Count > 0)
                MessageBox.Show("Wystąpił błąd – operacja została przerwana, program działa dalej.\n\n" + a.Exception.Message + "\n\n" +
                    (ErrorLogDirectory == null ? "Logi techniczne nie są dostępne." : "Katalog logów technicznych: " + ErrorLogDirectory),
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
        };
        if (A.Length > 0 && A[0] == "--selftest")
        {
            var coreOptions = CreateSimulatorOptions();
            if (A.Length > 3 && A[3] == "wzorce") Sim.SelfTest.RunPatterns(StationPath, A[1], A[2], coreOptions);
            else if (A.Length > 3 && A[3].StartsWith("tylko:")) Sim.SelfTest.RunScenario(StationPath, A[1], A[2], A[3][6..], coreOptions);
            else if (A.Length > 2) Sim.SelfTest.RunScenario(StationPath, A[1], A[2], options: coreOptions);
            else Sim.SelfTest.Run(StationPath, A.Length > 1 ? A[1] : "selftest.txt", coreOptions);
            Shutdown();
            return;
        }
        if (A.Length > 0 && A[0] == "--edrtest")
        {
            Sim.EdrTest.Run(StationPath, A.Length > 1 ? A[1] : "edrtest.txt", CreateSimulatorOptions());
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

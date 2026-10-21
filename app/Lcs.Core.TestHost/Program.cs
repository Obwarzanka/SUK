using System.Diagnostics;
using System.Text;
using Lcs.Logi;
using LcsWidzew.Sim;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lcs.Core.TestHost;

/// <summary>Proces regresji tego samego rdzenia co pulpit; bez WindowsDesktop, WPF, serwera i okna.</summary>
internal static class Program
{
    static readonly string Version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public static int Main(string[] arguments)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var args = arguments.ToList();
        ILogger log = NullLogger.Instance;
        var start = Stopwatch.GetTimestamp();
        string logsReport = null, probeResult = "";
        try
        {
            if (args.Count == 0 || args.Contains("--help"))
            {
                Console.WriteLine("Host testowy rdzenia LCS (.NET 10, bez WPF).\n" +
                    "--station kopia.json --selftest wynik.txt [scenariusz [wzorce|tylko:numer]]\n" +
                    "--boundary wynik.txt\n" +
                    "--station kopia.json --test wynik.txt\n" +
                    "--station kopia.json --mapa-wlasnosci mapa.json (mapa własności postId/kzId, S02A.01)\n" +
                    "--compare baza.txt wynik.txt [raport.txt] [--require-events]\n" +
                    "Opcje logów: --logi katalog --logi-raport plik --logi-proba\n" +
                    "Porównanie zachowuje kolejność i czasy domenowe; normalizuje wyłącznie końce linii.");
                return 0;
            }
            string logsDirectory = Remove(args, "--logi");
            logsReport = Remove(args, "--logi-raport");
            bool probe = RemoveFlag(args, "--logi-proba");
            if (logsReport != null) logsReport = Path.GetFullPath(logsReport);
            Diagnostyka.Start(new OpcjeLogow { Aplikacja = "Lcs.Core.TestHost", Katalog = logsDirectory, Profil = ProfilLogow.Klient });
            log = Diagnostyka.Modul("Lcs.Core.TestHost", Version);
            ZdarzeniaProcesu.Uruchomiono(log, args.Contains("--selftest") ? "selftest" : args.Contains("--compare") ? "porownanie" : "granica");
            if (probe)
            {
                ProbaLogow.Wykonaj(log);
                probeResult = $"pomiar_rytm_ticku: {ProbaLogow.Pomiar(log)}\npomiar_zalew: {ProbaLogow.Pomiar(log, 1, 5000, 0)}\n";
            }
            // Provider pozostaje w aplikacji; rdzeń korzysta wyłącznie z neutralnego ILogger.
            if (!KatalogZdarzen.Zarejestruj(new DefinicjaZdarzenia(CoreLogging.EventId, CoreLogging.EventCode,
                KontraktLogu.Strumien.Domenowy, CoreLogging.PolaZdarzeniaRejestratora.ToArray())))
                throw new InvalidOperationException("Nie można zarejestrować kontraktu rejestratora rdzenia.");
            foreach (var (id, kod, pola) in MapaWlasnosciLog.Definicje)
                if (!KatalogZdarzen.Zarejestruj(new DefinicjaZdarzenia(id, kod, KontraktLogu.Strumien.Diagnostyka, pola)))
                    throw new InvalidOperationException("Nie można zarejestrować kontraktu mapy własności.");

            string stationPath = Remove(args, "--station");
            if (args.Count == 0) throw new ArgumentException("Brak polecenia hosta testowego.");
            if (args[0] == "--boundary" && args.Count == 2)
            {
                string report = BoundaryVerification.Run();
                File.WriteAllText(args[1], report);
                Console.WriteLine(report);
                return 0;
            }
            if (args[0] == "--test" && args.Count == 2)
            {
                if (string.IsNullOrWhiteSpace(stationPath)) throw new ArgumentException("--test wymaga jawnego --station z kopią testową.");
                var result = AdapterTests.Run(Path.GetFullPath(stationPath), Path.GetFullPath(args[1]));
                File.WriteAllText(args[1], result.Report);
                Console.WriteLine(result.Report);
                return result.Equal ? 0 : 1;
            }
            if (args[0] == "--mapa-wlasnosci" && args.Count == 2)
            {
                if (string.IsNullOrWhiteSpace(stationPath)) throw new ArgumentException("--mapa-wlasnosci wymaga jawnego --station z kopią układu.");
                var map = OwnershipMap.Export(Path.GetFullPath(stationPath), Path.GetFullPath(args[1]),
                    Diagnostyka.Modul(MapaWlasnosciLog.Modul, MapaWlasnosciLog.Wersja));
                Console.WriteLine($"Mapa własności: {map.Posterunki.Count} KZ, {map.Obiekty.Count} obiektów, {map.Granice.Count} granic, " +
                    $"{map.Problemy.Count} problemów; sha256 {map.Sha256}");
                return 0;
            }
            if (args[0] == "--compare")
            {
                bool requireEvents = RemoveFlag(args, "--require-events");
                if (args.Count is < 3 or > 4) throw new ArgumentException("--compare wymaga dwóch raportów oraz opcjonalnej ścieżki wyniku.");
                var comparison = RegressionComparison.Compare(args[1], args[2], requireEvents);
                if (args.Count == 4) File.WriteAllText(args[3], comparison.Report);
                Console.WriteLine(comparison.Report);
                return comparison.Equal ? 0 : 1;
            }
            if (args[0] != "--selftest" || args.Count is < 2 or > 4)
                throw new ArgumentException("Nieznane polecenie lub zła liczba parametrów; użyj --help.");
            if (string.IsNullOrWhiteSpace(stationPath))
                throw new ArgumentException("Host testowy wymaga jawnego --station wskazującego kopię testową.");
            stationPath = Path.GetFullPath(stationPath);
            var options = new SimulatorOptions { RejestratorLogger = Diagnostyka.Modul(CoreLogging.Modul, CoreLogging.Wersja) };
            if (args.Count == 4 && args[3] == "wzorce") SelfTest.RunPatterns(stationPath, args[1], args[2], options);
            else if (args.Count == 4 && args[3].StartsWith("tylko:", StringComparison.Ordinal)) SelfTest.RunScenario(stationPath, args[1], args[2], args[3][6..], options);
            else if (args.Count == 3) SelfTest.RunScenario(stationPath, args[1], args[2], options: options);
            else if (args.Count == 2) SelfTest.Run(stationPath, args[1], options);
            else throw new ArgumentException("Nieznany filtr scenariusza; użyj wzorce albo tylko:numer.");
            Console.WriteLine($"Zapisano wynik: {Path.GetFullPath(args[1])}");
            return 0;
        }
        catch (Exception ex)
        {
            ZdarzeniaProcesu.Blad(log, ex, "CORE_TEST_HOST");
            Console.Error.WriteLine($"Test przerwany ({ex.GetType().Name}); szczegóły w zredagowanych logach technicznych.");
            return 2;
        }
        finally
        {
            ZdarzeniaProcesu.Zatrzymano(log, (long)Stopwatch.GetElapsedTime(start).TotalMicroseconds);
            var state = Diagnostyka.Zatrzymaj();
            if (logsReport != null)
                try { File.WriteAllText(logsReport, (state?.ToString() ?? "logi=nie_uruchomione\n") + probeResult); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Console.Error.WriteLine("Nie zapisano raportu logów: " + ex.GetType().Name); }
        }
    }

    static string Remove(List<string> args, string name)
    {
        int index = args.IndexOf(name);
        if (index < 0) return null;
        if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException("Brak wartości " + name);
        string value = args[index + 1];
        args.RemoveRange(index, 2);
        return value;
    }

    static bool RemoveFlag(List<string> args, string name)
    {
        int index = args.IndexOf(name);
        if (index < 0) return false;
        args.RemoveAt(index);
        return true;
    }
}

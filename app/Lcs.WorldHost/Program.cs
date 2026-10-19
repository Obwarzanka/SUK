using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lcs.Logi;
using LcsWidzew.Sim;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lcs.WorldHost;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (args.Length == 0 || args is ["--help"])
        {
            Console.WriteLine("Host świata SUK (.NET 10, bez WPF).\n" +
                "--station układ.json [--scenario podstawowy] [--world-id local] [--ticks liczba]\n" +
                "[--report wynik.json] [--events dziennik.txt] [--logi katalog] [--logi-raport stan.txt]\n" +
                "Zakończenie: Ctrl+C. Bez --ticks świat pracuje do zatrzymania.\n" +
                "To lokalny proces świata; tryb usługi i połączenie pulpitu mają osobne etapy.");
            return 0;
        }
        SilnikLogow? logs = null; WorldLoop? loop = null; SimulatorRuntime? runtime = null;
        string? report = null, events = null, logsReport = null;
        int code = 2;
        bool outputsValidated = false;
        long processStart = Stopwatch.GetTimestamp();
        Microsoft.Extensions.Logging.ILogger? logger = null;
        try
        {
            var values = Parse(args);
            if (!values.Remove("--station", out var station)) throw new ArgumentException("Wymagany jawny plik --station.");
            string scenario = values.GetValueOrDefault("--scenario", "podstawowy");
            string worldId = values.GetValueOrDefault("--world-id", "local");
            long? ticks = values.TryGetValue("--ticks", out var count) ? long.Parse(count, System.Globalization.CultureInfo.InvariantCulture) : null;
            report = Full(values.GetValueOrDefault("--report")); events = Full(values.GetValueOrDefault("--events"));
            logsReport = Full(values.GetValueOrDefault("--logi-raport")); station = Path.GetFullPath(station);
            var options = new WorldLoopOptions { WorldId = worldId, TickLimit = ticks }; options.Validate();
            // Jawne opcje CLI: żadnego odczytywania pliku z bieżącego katalogu ani domyślnego station.json.
            ProtectOutputs(station, report, events, logsReport);
            outputsValidated = true;
            WorldLog.Register();
            if (!KatalogZdarzen.Zarejestruj(new(CoreLogging.EventId, CoreLogging.EventCode, KontraktLogu.Strumien.Domenowy, CoreLogging.PolaZdarzeniaRejestratora.ToArray())))
                throw new InvalidOperationException("Niezgodny katalog rejestratora.");
            logs = SilnikLogow.Uruchom(new OpcjeLogow { Aplikacja = "Lcs.WorldHost", Profil = ProfilLogow.Serwer, Katalog = values.GetValueOrDefault("--logi") });
            logger = logs.Modul(WorldLog.Module, WorldLog.Version);
            var recorder = logs.Modul(CoreLogging.Modul, CoreLogging.Wersja);
            ZdarzeniaProcesu.Uruchomiono(logger, "world");
            loop = new WorldLoop(() => runtime = new SimulatorRuntime(station, scenario, logger, recorder, events != null), options, logger: logger);

            // Jawny ConsoleLifetime obsługuje Ctrl+C także bez domyślnych providerów i konfiguracji hosta.
            using var host = new HostBuilder().UseContentRoot(AppContext.BaseDirectory)
                .UseConsoleLifetime(o => o.SuppressStatusMessages = true)
                .ConfigureLogging(logging => { logging.DodajSukLogi(logs); logging.AddFilter("Microsoft", LogLevel.None); })
                .ConfigureServices(services =>
                {
                    services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(10));
                    services.AddSingleton(loop); services.AddHostedService<WorldService>();
                }).Build();
            await host.RunAsync();
            code = loop.Status.State == WorldState.Stopped ? 0 : 2;
        }
        catch (Exception ex)
        {
            loop?.RequestStop();
            if (logger != null && loop is { Completion.IsCompleted: false })
            {
                var state = loop.Status;
                using var scope = logger.BeginScope(new Dictionary<string, object> { ["worldId"] = state.WorldId,
                    ["worldRunId"] = state.WorldRunId, ["serverTick"] = state.ServerTick, ["simulationTimeMs"] = state.SimulationTimeMs });
                WorldLog.Failed(logger, ex, "WORLD_SHUTDOWN_INCOMPLETE");
            }
            if (logger != null) ZdarzeniaProcesu.Blad(logger, ex, "WORLD_PROCESS");
            Console.Error.WriteLine($"Host przerwany ({ex.GetType().Name}). Szczegóły w zredagowanych logach technicznych.");
        }
        finally
        {
            bool exportFailed = false;
            void WriteArtifact(Action write)
            {
                try { write(); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { code = 2; exportFailed = true; Console.Error.WriteLine("Nie zapisano wyniku hosta: " + ex.GetType().Name); }
            }
            if (events != null && loop?.Completion.IsCompleted == true && runtime?.FinalEvents != null)
                WriteArtifact(() => File.WriteAllText(events, runtime.FinalEvents));
            if (logger != null) ZdarzeniaProcesu.Zatrzymano(logger, (long)Stopwatch.GetElapsedTime(processStart).TotalMicroseconds);
            var finalLogs = logs?.Zatrzymaj();
            if (outputsValidated && logsReport != null) WriteArtifact(() => File.WriteAllText(logsReport, finalLogs?.ToString() ?? "logi=nie_uruchomione\n"));
            // Raport jest ostatni: kod obejmuje już błędy wszystkich wcześniejszych eksportów.
            if (report != null && loop != null) WriteArtifact(() =>
            {
                File.WriteAllText(report, JsonSerializer.Serialize(new { exitCode = code, world = loop.Status, exportFailed,
                    inputHashes = loop.Completion.IsCompleted ? runtime?.InputHashes : null,
                    shutdownComplete = loop.Completion.IsCompleted,
                    diagnostics = new { healthy = finalLogs != null && finalLogs.BledyZapisu == 0 && finalLogs.PowodNiegotowosci == null && finalLogs.ZamkniecieKompletne,
                        flushComplete = finalLogs?.ZamkniecieKompletne, dropped = finalLogs?.StratyRazem } },
                    new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } }));
            });
        }
        if (loop != null) Console.WriteLine($"Stan: {StateName(loop.Status.State)}, kroki: {loop.Status.ServerTick}, czas: {loop.Status.SimulationTimeMs} ms.");
        return code;
    }

    static string StateName(WorldState state) => state switch
    { WorldState.Stopped => "zatrzymany", WorldState.Faulted => "błąd", WorldState.Running => "działa", _ => "niegotowy" };
    static string? Full(string? path) => path == null ? null : Path.GetFullPath(path);
    static void ProtectOutputs(string station, params string?[] outputs)
    {
        var used = new HashSet<string>(SimulatorRuntime.KnownInputs(station).Values.Where(p => p != null).Select(p => Path.GetFullPath(p!)), StringComparer.OrdinalIgnoreCase);
        foreach (var output in outputs.Where(p => p != null))
            if (!used.Add(output!)) throw new ArgumentException("Pliki wejściowe i wyniki muszą mieć różne ścieżki.");
    }
    static Dictionary<string, string> Parse(string[] args)
    {
        var allowed = new HashSet<string> { "--station", "--scenario", "--world-id", "--ticks", "--report", "--events", "--logi", "--logi-raport" };
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i += 2)
            if (!allowed.Contains(args[i]) || i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal) || !result.TryAdd(args[i], args[i + 1]))
                throw new ArgumentException("Nieznana, powtórzona lub niepełna opcja; użyj --help.");
        return result;
    }
}

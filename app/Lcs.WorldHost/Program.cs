using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lcs.Logi;
using LcsWidzew.Sim;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
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
                "--service-profile Config/profile.json [--ticks liczba] — profil usługi lub lokalna próba tego profilu\n" +
                "--health Config/profile.json — uwierzytelniony odczyt lokalnego kanału zdrowia\n" +
                "Zakończenie: Ctrl+C. Bez --ticks świat pracuje do zatrzymania.\n" +
                "Instalacja usługi jest osobną operacją operatora; ten program jej nie wykonuje.");
            return 0;
        }
        SilnikLogow? logs = null; WorldLoop? loop = null; SimulatorRuntime? runtime = null;
        string? report = null, events = null, logsReport = null;
        int code = 2;
        bool outputsValidated = false;
        long processStart = Stopwatch.GetTimestamp();
        Microsoft.Extensions.Logging.ILogger? logger = null;
        ServiceProfile? profile = null; ServiceHealth? health = null;
        IHostLifetime? serviceLifetime = null;
        try
        {
            if (args is ["--health", var healthProfilePath])
            {
                if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Kanał usługi wymaga Windows.");
                var queryProfile = ServiceProfile.Load(healthProfilePath); queryProfile.ValidateHealthReader(healthProfilePath);
                var result = await HealthPipe.Query(queryProfile);
                Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } }));
                return result.Ready ? 0 : 3;
            }
            var values = Parse(args);
            if (values.Remove("--service-profile", out var profilePath))
            {
                if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Profil usługi wymaga Windows.");
                if (values.Keys.Any(k => k != "--ticks")) throw new ArgumentException("Profil usługi dopuszcza tylko opcję --ticks.");
                profile = ServiceProfile.Load(profilePath); profile.ValidateEnvironment(profilePath);
                if (WindowsServiceHelpers.IsWindowsService() && profile.AccountSid != ServiceProfile.VirtualAccountSid)
                    throw new UnauthorizedAccessException("SCM wymaga osobnego wirtualnego konta SUK.WorldHost.");
                values["--station"] = profile.StationPath; values["--world-id"] = profile.WorldId; values["--scenario"] = profile.Scenario;
                values["--logi"] = profile.LogsPath; values["--report"] = Path.Combine(profile.StatePath, "host.json");
                values["--logi-raport"] = Path.Combine(profile.StatePath, "logi.txt");
            }
            if (OperatingSystem.IsWindows() && WindowsServiceHelpers.IsWindowsService() && profile == null)
                throw new ArgumentException("Uruchomienie przez SCM wymaga jawnego profilu usługi.");
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
            ServiceLog.Register();
            if (!KatalogZdarzen.Zarejestruj(new(CoreLogging.EventId, CoreLogging.EventCode, KontraktLogu.Strumien.Domenowy, CoreLogging.PolaZdarzeniaRejestratora.ToArray())))
                throw new InvalidOperationException("Niezgodny katalog rejestratora.");
            foreach (var (mapId, mapCode, mapFields) in MapaWlasnosciLog.Definicje)
                if (!KatalogZdarzen.Zarejestruj(new(mapId, mapCode, KontraktLogu.Strumien.Diagnostyka, mapFields)))
                    throw new InvalidOperationException("Niezgodny katalog mapy własności.");
            foreach (var (inputId, inputCode, inputFields) in WejscieCykluLog.Definicje)
                if (!KatalogZdarzen.Zarejestruj(new(inputId, inputCode, KontraktLogu.Strumien.Diagnostyka, inputFields)))
                    throw new InvalidOperationException("Niezgodny katalog wejść cyklu.");
            logs = SilnikLogow.Uruchom(new OpcjeLogow { Aplikacja = "Lcs.WorldHost", Profil = ProfilLogow.Serwer, Katalog = values.GetValueOrDefault("--logi") });
            logger = logs.Modul(WorldLog.Module, WorldLog.Version);
            var recorder = logs.Modul(CoreLogging.Modul, CoreLogging.Wersja);
            var ownership = logs.Modul(MapaWlasnosciLog.Modul, MapaWlasnosciLog.Wersja);
            var cycleInputs = logs.Modul(WejscieCykluLog.Modul, WejscieCykluLog.Wersja);
            ZdarzeniaProcesu.Uruchomiono(logger, "world");
            loop = new WorldLoop(() => runtime = new SimulatorRuntime(station, scenario, logger, recorder, events != null, ownership, cycleInputs), options, logger: logger);
            var serviceLogger = logs.Modul(ServiceLog.Module, WorldLog.Version);
            if (profile != null)
            {
                health = new ServiceHealth(loop, profile, () =>
                { var state = logs.Stan(); return state.Zapis == StanZapisu.Gotowy && state.PowodNiegotowosci == null && state.Miejsce != StanMiejsca.Ograniczenie; },
                    beforeFailureStop: () => { if (OperatingSystem.IsWindows() && serviceLifetime is WindowsServiceLifetime scm) scm.ExitCode = 2; });
                using var scope = ServiceLog.Context(serviceLogger, loop.Status); ServiceLog.ProfileReady(serviceLogger);
            }

            // Jawny ConsoleLifetime obsługuje Ctrl+C także bez domyślnych providerów i konfiguracji hosta.
            var builder = new HostBuilder().UseContentRoot(AppContext.BaseDirectory)
                .UseConsoleLifetime(o => o.SuppressStatusMessages = true)
                .ConfigureLogging(logging => { logging.ClearProviders(); logging.DodajSukLogi(logs); logging.AddFilter("Microsoft", LogLevel.None); })
                .ConfigureServices(services =>
                {
                    services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(profile?.ShutdownTimeoutSeconds ?? 10));
                    if (OperatingSystem.IsWindows() && profile != null)
                    {
                        services.AddSingleton(profile);
                        services.AddSingleton(health!);
                        services.AddHostedService(sp =>
                        {
                            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
                            return new HealthPipe(profile, health!, loop, serviceLogger, sp.GetRequiredService<IHostApplicationLifetime>());
                        });
                        services.AddHostedService(sp => new ServiceMonitor(health!, loop, serviceLogger, sp.GetRequiredService<IHostApplicationLifetime>()));
                    }
                    services.AddSingleton(loop); services.AddHostedService<WorldService>();
                });
            if (OperatingSystem.IsWindows() && profile != null)
                builder.UseWindowsService(o => o.ServiceName = profile.ServiceName)
                    .ConfigureLogging(logging => { logging.ClearProviders(); logging.DodajSukLogi(logs); logging.AddFilter("Microsoft", LogLevel.None); });
            using var host = builder.Build();
            serviceLifetime = host.Services.GetRequiredService<IHostLifetime>();
            await host.RunAsync();
            code = loop.Status.State == WorldState.Stopped && health?.FailureCode == null ? 0 : 2;
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
                File.WriteAllText(report, JsonSerializer.Serialize(new { exitCode = code, world = loop.Status, exportFailed, serviceFailure = health?.FailureCode,
                    inputHashes = loop.Completion.IsCompleted ? runtime?.InputHashes : null,
                    shutdownComplete = loop.Completion.IsCompleted,
                    diagnostics = new { healthy = finalLogs != null && finalLogs.BledyZapisu == 0 && finalLogs.PowodNiegotowosci == null && finalLogs.ZamkniecieKompletne && finalLogs.Miejsce != StanMiejsca.Ograniczenie,
                        flushComplete = finalLogs?.ZamkniecieKompletne, dropped = finalLogs?.StratyRazem } },
                    new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } }));
            });
        }
        if (loop != null) Console.WriteLine($"Stan: {StateName(loop.Status.State)}, kroki: {loop.Status.ServerTick}, czas: {loop.Status.SimulationTimeMs} ms.");
        // Niezerowy wynik awarii procesu pozwala SCM zastosować jawnie skonfigurowany backoff, po opróżnieniu logów.
        if (code != 0 && OperatingSystem.IsWindows() && WindowsServiceHelpers.IsWindowsService()) Environment.Exit(code);
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
        var allowed = new HashSet<string> { "--station", "--scenario", "--world-id", "--ticks", "--report", "--events", "--logi", "--logi-raport", "--service-profile" };
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i += 2)
            if (!allowed.Contains(args[i]) || i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal) || !result.TryAdd(args[i], args[i + 1]))
                throw new ArgumentException("Nieznana, powtórzona lub niepełna opcja; użyj --help.");
        return result;
    }
}

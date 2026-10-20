using Lcs.Logi;
using Microsoft.Extensions.Logging;

namespace Lcs.WorldHost;

public static partial class WorldLog
{
    public const string Module = "WorldHost.Loop";
    public static string Version => typeof(WorldLog).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    public static void Register()
    {
        foreach (var (id, code, fields) in new (int, string, string[])[]
        {
            (3001, "WORLD.STARTING", []), (3002, "WORLD.READY", []),
            (3003, "WORLD.STOPPING", []), (3004, "WORLD.STOPPED", []),
            (3005, "WORLD.FAILED", []), (3006, "WORLD.METRICS", []),
            (3007, "WORLD.INPUT_REJECTED", []), (3008, "WORLD.DATA_LOADED", ["contentHash"])
        })
            if (!KatalogZdarzen.Zarejestruj(new(id, code, KontraktLogu.Strumien.Diagnostyka, fields)))
                throw new InvalidOperationException("Niezgodny katalog zdarzeń hosta świata.");
    }

    [LoggerMessage(3001, LogLevel.Information, "Uruchamianie świata", EventName = "WORLD.STARTING")]
    public static partial void Starting(ILogger log, string outcome = "Starting");
    [LoggerMessage(3002, LogLevel.Information, "Świat gotowy", EventName = "WORLD.READY")]
    public static partial void Ready(ILogger log, string outcome = "Ready");
    [LoggerMessage(3003, LogLevel.Information, "Zatrzymywanie świata", EventName = "WORLD.STOPPING")]
    public static partial void Stopping(ILogger log, string outcome = "Stopping");
    [LoggerMessage(3004, LogLevel.Information, "Świat zatrzymany", EventName = "WORLD.STOPPED")]
    public static partial void Stopped(ILogger log, string outcome = "Stopped");
    [LoggerMessage(3005, LogLevel.Error, "Błąd świata ({reasonCode})", EventName = "WORLD.FAILED")]
    public static partial void Failed(ILogger log, Exception exception, string reasonCode, string outcome = "Failed");
    [LoggerMessage(3006, LogLevel.Information, "Podsumowanie cyklu świata", EventName = "WORLD.METRICS")]
    public static partial void Metrics(ILogger log, IReadOnlyDictionary<string, long> counters);
    [LoggerMessage(3007, LogLevel.Warning, "Odmowa wejścia ({reasonCode})", EventName = "WORLD.INPUT_REJECTED")]
    public static partial void Rejected(ILogger log, string reasonCode, IReadOnlyDictionary<string, long> counters);
    [LoggerMessage(3008, LogLevel.Information, "Wczytano dane świata", EventName = "WORLD.DATA_LOADED")]
    public static partial void DataLoaded(ILogger log, string contentHash);
}

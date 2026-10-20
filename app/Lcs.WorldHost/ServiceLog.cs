using Lcs.Logi;
using Microsoft.Extensions.Logging;

namespace Lcs.WorldHost;

public static partial class ServiceLog
{
    public const string Module = "WorldHost.Service";
    public static void Register()
    {
        foreach (var (id, code) in new[] { (3010, "WORLD.SERVICE_PROFILE_READY"), (3011, "WORLD.SERVICE_HEALTH_CHANGED"),
            (3012, "WORLD.SERVICE_LIMIT_EXCEEDED"), (3013, "WORLD.SERVICE_PIPE_STOPPED"), (3014, "WORLD.SERVICE_FAILED"), (3015, "WORLD.SERVICE_PIPE_METRICS") })
            if (!KatalogZdarzen.Zarejestruj(new(id, code, KontraktLogu.Strumien.Diagnostyka)))
                throw new InvalidOperationException("Niezgodny katalog zdarzeń usługi.");
    }
    public static IDisposable? Context(ILogger log, WorldStatus s) => log.BeginScope(new Dictionary<string, object>
    { ["worldId"] = s.WorldId, ["worldRunId"] = s.WorldRunId, ["serverTick"] = s.ServerTick, ["simulationTimeMs"] = s.SimulationTimeMs });
    [LoggerMessage(3010, LogLevel.Information, "Sprawdzono profil i uprawnienia usługi", EventName = "WORLD.SERVICE_PROFILE_READY")]
    public static partial void ProfileReady(ILogger log);
    [LoggerMessage(3011, LogLevel.Information, "Zmiana zdrowia usługi ({reasonCode})", EventName = "WORLD.SERVICE_HEALTH_CHANGED")]
    public static partial void HealthChanged(ILogger log, string reasonCode);
    [LoggerMessage(3012, LogLevel.Error, "Przekroczono limit usługi ({reasonCode})", EventName = "WORLD.SERVICE_LIMIT_EXCEEDED")]
    public static partial void LimitExceeded(ILogger log, string reasonCode);
    [LoggerMessage(3013, LogLevel.Information, "Zamknięto lokalny kanał zdrowia", EventName = "WORLD.SERVICE_PIPE_STOPPED")]
    public static partial void PipeStopped(ILogger log);
    [LoggerMessage(3014, LogLevel.Error, "Błąd granicy usługi ({reasonCode})", EventName = "WORLD.SERVICE_FAILED")]
    public static partial void Failed(ILogger log, Exception exception, string reasonCode);
    [LoggerMessage(3015, LogLevel.Information, "Podsumowanie kanału zdrowia", EventName = "WORLD.SERVICE_PIPE_METRICS")]
    public static partial void PipeMetrics(ILogger log, IReadOnlyDictionary<string, long> counters);
}


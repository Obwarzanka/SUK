// SYSLIB1015: pola rekordu trafiają do właściwości strukturalnych także bez użycia w szablonie komunikatu.
#pragma warning disable SYSLIB1015
using Microsoft.Extensions.Logging;

namespace LcsWidzew.Sim;

/// <summary>
/// F24/25: diagnostyka pojedynczego wyjścia KZ, poza biernym Evaluate. Limity należą do bramki świata:
/// jeden start, 16 szczegółów zatwierdzeń, 16 odmów na kod powodu, każdy fail-stop oraz podsumowanie
/// przy zakończeniu i najwyżej co 3000 cykli. Adapter nie zapisuje treści poleceń, planu ani danych operatora.
/// Diagnostyka nie jest trwałym dziennikiem zatwierdzeń i nie dowodzi fizycznego wykonania przez kartę.
/// </summary>
public static partial class PlanWyjsciaLog
{
    public const string Modul = "LcsWidzew.PlanWyjscia";
    public static string Wersja => CoreLogging.Wersja;
    public const int IdGotowa = 4121, IdZatwierdzono = 4122, IdOdrzucono = 4123, IdBlad = 4124, IdPodsumowanie = 4125;
    public const string KodGotowa = "KZ.OUTPUT_GATE_READY", KodZatwierdzono = "KZ.OUTPUT_PLAN_COMMITTED",
        KodOdrzucono = "KZ.OUTPUT_PLAN_REJECTED", KodBlad = "KZ.OUTPUT_GATE_FAILED", KodPodsumowanie = "KZ.OUTPUT_GATE_SUMMARY";
    public const int MaxZatwierdzenWLogu = 16, MaxOdmowNaPowodWLogu = 16;

    /// <summary>Wszystkie pola są standardowym kontekstem schematu, bez dodatkowych pól modułu.</summary>
    public static IReadOnlyList<(int Id, string Kod, string[] Pola)> Definicje { get; } = Array.AsReadOnly(new (int, string, string[])[]
    {
        (IdGotowa, KodGotowa, Array.Empty<string>()),
        (IdZatwierdzono, KodZatwierdzono, Array.Empty<string>()),
        (IdOdrzucono, KodOdrzucono, Array.Empty<string>()),
        (IdBlad, KodBlad, Array.Empty<string>()),
        (IdPodsumowanie, KodPodsumowanie, Array.Empty<string>()),
    });

    /// <summary>
    /// Zapis ograniczonych metadanych decyzji bramki. effectId przechodzi walidację identyfikatora SUK,
    /// kod powodu i wynik dopuszczają tylko krótkie nazwy typowane; wyjątek redaguje adapter hosta.
    /// Błąd diagnostyki nie zmienia przyjętej decyzji ani stanu świata.
    /// </summary>
    public static void Zapisz(ILogger log, int eventId, string effectId, string reasonCode, string outcome,
        long tick, long timeMs, IReadOnlyDictionary<string, long> counters, Exception ex = null)
    {
        if (log == null) return;
        effectId = MapaWlasnosciLog.Identyfikator(effectId);
        reasonCode = Nazwa(reasonCode, wielkie: true);
        outcome = Nazwa(outcome, wielkie: false);
        tick = Math.Max(0, tick); timeMs = Math.Max(0, timeMs);
        try
        {
            switch (eventId)
            {
                case IdGotowa: LogGotowa(log, effectId, reasonCode, outcome, tick, timeMs, counters); break;
                case IdZatwierdzono: LogZatwierdzono(log, effectId, reasonCode, outcome, tick, timeMs, counters); break;
                case IdOdrzucono: LogOdrzucono(log, effectId, reasonCode, outcome, tick, timeMs, counters); break;
                case IdBlad: LogBlad(log, ex, effectId, reasonCode, outcome, tick, timeMs, counters); break;
                case IdPodsumowanie: LogPodsumowanie(log, effectId, reasonCode, outcome, tick, timeMs, counters); break;
            }
        }
        catch (Exception) { } // F24: awaria diagnostyki nie wykonuje, nie cofa ani nie powtarza skutku.
    }

    static string Nazwa(string nazwa, bool wielkie)
    {
        if (string.IsNullOrEmpty(nazwa) || nazwa.Length > 64) return null;
        return nazwa.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' || !wielkie && c is >= 'a' and <= 'z') ? nazwa : null;
    }

    [LoggerMessage(EventId = IdGotowa, EventName = KodGotowa, Level = LogLevel.Information, Message = "Bramka pojedynczego wyjścia KZ gotowa ({outcome})")]
    static partial void LogGotowa(ILogger log, string effectId, string reasonCode, string outcome, long serverTick,
        long simulationTimeMs, IReadOnlyDictionary<string, long> counters);

    [LoggerMessage(EventId = IdZatwierdzono, EventName = KodZatwierdzono, Level = LogLevel.Information, Message = "Plan wyjścia KZ zatwierdzony ({outcome})")]
    static partial void LogZatwierdzono(ILogger log, string effectId, string reasonCode, string outcome, long serverTick,
        long simulationTimeMs, IReadOnlyDictionary<string, long> counters);

    [LoggerMessage(EventId = IdOdrzucono, EventName = KodOdrzucono, Level = LogLevel.Warning, Message = "Plan wyjścia KZ odrzucony ({reasonCode})")]
    static partial void LogOdrzucono(ILogger log, string effectId, string reasonCode, string outcome, long serverTick,
        long simulationTimeMs, IReadOnlyDictionary<string, long> counters);

    [LoggerMessage(EventId = IdBlad, EventName = KodBlad, Level = LogLevel.Error, Message = "Bramka wyjścia KZ zatrzymana po błędzie ({reasonCode})")]
    static partial void LogBlad(ILogger log, Exception exception, string effectId, string reasonCode, string outcome,
        long serverTick, long simulationTimeMs, IReadOnlyDictionary<string, long> counters);

    [LoggerMessage(EventId = IdPodsumowanie, EventName = KodPodsumowanie, Level = LogLevel.Information, Message = "Bramka wyjścia KZ: podsumowanie decyzji i pominiętych szczegółów ({reasonCode})")]
    static partial void LogPodsumowanie(ILogger log, string effectId, string reasonCode, string outcome, long serverTick,
        long simulationTimeMs, IReadOnlyDictionary<string, long> counters);
}

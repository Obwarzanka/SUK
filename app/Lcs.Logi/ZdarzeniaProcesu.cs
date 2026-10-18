using Microsoft.Extensions.Logging;

namespace Lcs.Logi;

/// <summary>Wspólne zdarzenia procesu każdej aplikacji (PROC 1000–1099): start z wersjami, zatrzymanie, nieobsłużony wyjątek.</summary>
public static partial class ZdarzeniaProcesu
{
    public static readonly DefinicjaZdarzenia Start = new(1001, "PROC.STARTED", KontraktLogu.Strumien.Diagnostyka, "tryb", "runtimeVersion", "osVersion");
    public static readonly DefinicjaZdarzenia Stop = new(1002, "PROC.STOPPED", KontraktLogu.Strumien.Diagnostyka);
    public static readonly DefinicjaZdarzenia Wyjatek = new(1010, "PROC.UNHANDLED_EXCEPTION", KontraktLogu.Strumien.Diagnostyka);

    internal static IEnumerable<DefinicjaZdarzenia> Definicje => new[] { Start, Stop, Wyjatek };

    /// <summary>Start programu: tryb uruchomienia (stała programu, np. pulpit/selftest), wersja runtime i systemu.</summary>
    public static void Uruchomiono(ILogger log, string tryb) =>
        LogStart(log, Redakcja.Tekst(tryb ?? "", 32), Environment.Version.ToString(), Environment.OSVersion.VersionString, "Started");

    public static void Zatrzymano(ILogger log, long czasDzialaniaUs) => LogStop(log, czasDzialaniaUs < 0 ? 0 : czasDzialaniaUs, "Stopped");

    /// <summary>Kontrolowany zapis nieobsłużonego wyjątku; <paramref name="reasonCode"/> to stały kod miejsca (np. UI_DISPATCHER).</summary>
    public static void Blad(ILogger log, Exception ex, string reasonCode) =>
        LogWyjatek(log, ex, KontraktLogu.PoprawnyKodPowodu(reasonCode) ? reasonCode : "OTHER", "Failed");

    [LoggerMessage(EventId = 1001, EventName = "PROC.STARTED", Level = LogLevel.Information, Message = "Uruchomiono program (tryb {tryb})")]
    private static partial void LogStart(ILogger log, string tryb, string runtimeVersion, string osVersion, string outcome);

    [LoggerMessage(EventId = 1002, EventName = "PROC.STOPPED", Level = LogLevel.Information, Message = "Zatrzymano program")]
    private static partial void LogStop(ILogger log, long durationUs, string outcome);

    [LoggerMessage(EventId = 1010, EventName = "PROC.UNHANDLED_EXCEPTION", Level = LogLevel.Error, Message = "Nieobsłużony wyjątek ({reasonCode})")]
    private static partial void LogWyjatek(ILogger log, Exception ex, string reasonCode, string outcome);
}

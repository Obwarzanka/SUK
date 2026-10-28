// SYSLIB1015: pola rekordu nie muszą występować w szablonie komunikatu – trafiają jako właściwości strukturalne.
#pragma warning disable SYSLIB1015
using Microsoft.Extensions.Logging;

namespace LcsWidzew.Sim;

/// <summary>
/// Logi techniczne rejestru wejść cyklu (F24, rozdział 25; podzakres KZ 4100–4199 – strona hosta): KZ.CYCLE_INPUT_ROUTING_READY 4111
/// (skrót tabeli rozsyłania i liczniki), KZ.CYCLE_INPUT_ROUTING_FALLBACK 4112 (obiekt bez jednoznacznego właściciela – rozsyłany
/// ostrożnie; najwyżej <see cref="MaxObejscWLogu"/> na budowę tabeli), KZ.CYCLE_INPUT_ROUTING_UNAVAILABLE 4113 (brak mapy własności,
/// posterunków albo błąd budowy tabeli), KZ.CYCLE_INPUT_PHASE_VIOLATION 4114 (status poza fazą, faza zagnieżdżona lub reentrantna,
/// obiekt spoza mapy, limit doręczeń; najwyżej <see cref="MaxNaruszenWLogu"/> na kod w świecie, DELIVERY_LIMIT zawsze),
/// KZ.CYCLE_INPUT_RECEIVER_FAILED 4115 (odbiorca wyłączony po wyjątku; najwyżej <see cref="MaxBledowOdbiorcowWLogu"/>) i
/// KZ.CYCLE_INPUT_VIOLATIONS_SUMMARY 4116 (podsumowanie nieopisanych: okresowo, przy zatrzymaniu rejestru i na końcu świata). Rejestruje przejścia i podsumowania – nigdy statusu każdej karty w każdym cyklu (25 §5). Bez treści
/// danych poza identyfikatorem obiektu (tylko poprawny identyfikator SUK), kodem powodu i licznikami; błąd zapisu nie zmienia symulacji.
/// </summary>
public static partial class WejscieCykluLog
{
    public const string Modul = "LcsWidzew.WejscieCyklu";
    public static string Wersja => CoreLogging.Wersja;
    public const int MaxObejscWLogu = 16, MaxNaruszenWLogu = 16, MaxBledowOdbiorcowWLogu = 16;
    public const int IdTrasa = 4111, IdObejscie = 4112, IdBrakTrasy = 4113, IdNaruszenie = 4114, IdBladOdbiorcy = 4115, IdPodsumowanie = 4116;
    public const string KodTrasa = "KZ.CYCLE_INPUT_ROUTING_READY", KodObejscie = "KZ.CYCLE_INPUT_ROUTING_FALLBACK",
        KodBrakTrasy = "KZ.CYCLE_INPUT_ROUTING_UNAVAILABLE", KodNaruszenie = "KZ.CYCLE_INPUT_PHASE_VIOLATION", KodBladOdbiorcy = "KZ.CYCLE_INPUT_RECEIVER_FAILED",
        KodPodsumowanie = "KZ.CYCLE_INPUT_VIOLATIONS_SUMMARY";
    /// <summary>Powody 4113 (zamknięta lista).</summary>
    public const string PowodBrakuMapy = "OWNERSHIP_MAP_UNAVAILABLE", PowodBrakuPosterunkow = "NO_VALID_POSTS", PowodBleduBudowy = "ROUTING_BUILD_FAILED";

    /// <summary>Definicje do rejestracji w katalogu zdarzeń hosta: eventId, eventCode i pola ponad pola kontekstu schematu.</summary>
    public static IReadOnlyList<(int Id, string Kod, string[] Pola)> Definicje { get; } = Array.AsReadOnly(new (int, string, string[])[]
    {
        (IdTrasa, KodTrasa, new[] { "contentHash", "mapHash" }),
        (IdObejscie, KodObejscie, new[] { "typObiektu" }),
        (IdBrakTrasy, KodBrakTrasy, Array.Empty<string>()),
        (IdNaruszenie, KodNaruszenie, new[] { "typObiektu" }),
        (IdBladOdbiorcy, KodBladOdbiorcy, Array.Empty<string>()),
        (IdPodsumowanie, KodPodsumowanie, Array.Empty<string>()),
    });

    static string KodObejscia(string powod) => powod switch
    {
        TrasaWejsc.Powod.Kandydaci => "OWNER_AMBIGUOUS_CANDIDATES",
        TrasaWejsc.Powod.OdbiorcyMapy => "OWNER_UNKNOWN_DECLARED_RECEIVERS",
        _ => "OWNER_UNKNOWN_ALL_KZ",
    };

    /// <summary>Zbudowana tabela rozsyłania: ograniczona liczba rekordów 4112 i jeden rekord 4111.</summary>
    public static void Trasa(ILogger log, TrasaWejsc t)
    {
        if (log == null || t == null) return;
        try
        {
            var obejscia = t.Wpisy.Where(w => w.Powod != TrasaWejsc.Powod.Wlasciciel).ToList();
            int zapisane = 0;
            if (log.IsEnabled(LogLevel.Warning))
                foreach (var w in obejscia.Take(MaxObejscWLogu))
                {
                    LogObejscie(log, w.Rodzaj, MapaWlasnosciLog.Identyfikator(w.Id), KodObejscia(w.Powod),
                        new Dictionary<string, long>(StringComparer.Ordinal) { ["receivers"] = w.Odbiorcy.Length });
                    zapisane++;
                }
            if (!log.IsEnabled(LogLevel.Information)) return;
            var liczniki = new Dictionary<string, long>(StringComparer.Ordinal)
            {
                ["kz"] = t.KzIds.Length,
                ["objects"] = t.Wpisy.Length,
                ["owner"] = t.Wpisy.Count(w => w.Powod == TrasaWejsc.Powod.Wlasciciel),
                ["candidates"] = t.Wpisy.Count(w => w.Powod == TrasaWejsc.Powod.Kandydaci),
                ["declaredReceivers"] = t.Wpisy.Count(w => w.Powod == TrasaWejsc.Powod.OdbiorcyMapy),
                ["allKz"] = t.Wpisy.Count(w => w.Powod == TrasaWejsc.Powod.WszystkieKz),
                ["multiReceiver"] = t.Wpisy.Count(w => w.Odbiorcy.Length > 1),
                ["fallbackLogged"] = zapisane,
                ["fallbackSuppressed"] = obejscia.Count - zapisane,
            };
            LogTrasa(log, t.Sha256, t.SkrotMapy, liczniki, obejscia.Count == 0 ? "Accepted" : "Degraded");
        }
        catch (Exception) { }   // diagnostyka nie może przerwać cyklu ani zmienić symulacji
    }

    /// <summary>Brak tabeli rozsyłania (<paramref name="powod"/> z listy Powod*): wejścia KZ nie są tworzone (fail-closed dla odbiorców);
    /// przejściowy KZ obszaru nadal dostaje partie.</summary>
    public static void TrasaNiedostepna(ILogger log, string powod)
    {
        if (log == null) return;
        if (powod is not (PowodBrakuMapy or PowodBrakuPosterunkow or PowodBleduBudowy)) powod = PowodBleduBudowy;
        try { LogBrakTrasy(log, powod, "Degraded"); } catch (Exception) { }
    }

    /// <summary>Podsumowanie po wyczerpaniu limitów 4114/4115: liczby łączne, nieopisane (narastająco) i liczniki rodzajów (11 pól; adapter przyjmuje najwyżej 14).</summary>
    public static void Podsumowanie(ILogger log, IReadOnlyDictionary<string, long> liczniki)
    {
        if (log == null || liczniki == null) return;
        try { LogPodsumowanie(log, "LOG_LIMIT_SUMMARY", liczniki); } catch (Exception) { }
    }

    /// <summary>Naruszenie granicy faz (kod powodu z zamkniętej listy rejestru).</summary>
    public static void Naruszenie(ILogger log, string kod, string rodzaj, string id, long liczba)
    {
        if (log == null) return;
        try { LogNaruszenie(log, rodzaj, MapaWlasnosciLog.Identyfikator(id), kod, new Dictionary<string, long>(StringComparer.Ordinal) { ["violations"] = liczba }); }
        catch (Exception) { }
    }

    /// <summary>Wyjątek odbiorcy – odbiorca wyłączony; szczegóły wyjątku redaguje adapter hosta.</summary>
    public static void BladOdbiorcy(ILogger log, string kzId, long nrWejscia, Exception ex)
    {
        if (log == null) return;
        try { LogBladOdbiorcy(log, ex, MapaWlasnosciLog.Identyfikator(kzId), "RECEIVER_EXCEPTION", "Failed", new Dictionary<string, long>(StringComparer.Ordinal) { ["inputSeq"] = Math.Max(0, nrWejscia) }); }
        catch (Exception) { }
    }

    [LoggerMessage(EventId = IdTrasa, EventName = KodTrasa, Level = LogLevel.Information, Message = "Tabela rozsyłania wejść KZ gotowa ({outcome})")]
    static partial void LogTrasa(ILogger log, string contentHash, string mapHash, IReadOnlyDictionary<string, long> counters, string outcome);

    [LoggerMessage(EventId = IdObejscie, EventName = KodObejscie, Level = LogLevel.Warning, Message = "Wejścia KZ: obiekt bez jednoznacznego właściciela rozsyłany ostrożnie ({reasonCode})")]
    static partial void LogObejscie(ILogger log, string typObiektu, string targetId, string reasonCode, IReadOnlyDictionary<string, long> counters);

    [LoggerMessage(EventId = IdBrakTrasy, EventName = KodBrakTrasy, Level = LogLevel.Error, Message = "Wejścia KZ nie są rozsyłane – brak tabeli rozsyłania ({reasonCode})")]
    static partial void LogBrakTrasy(ILogger log, string reasonCode, string outcome);

    [LoggerMessage(EventId = IdNaruszenie, EventName = KodNaruszenie, Level = LogLevel.Warning, Message = "Naruszenie granicy faz wejść KZ ({reasonCode})")]
    static partial void LogNaruszenie(ILogger log, string typObiektu, string targetId, string reasonCode, IReadOnlyDictionary<string, long> counters);

    [LoggerMessage(EventId = IdPodsumowanie, EventName = KodPodsumowanie, Level = LogLevel.Warning, Message = "Wejścia KZ: podsumowanie naruszeń ponad limit rekordów ({reasonCode})")]
    static partial void LogPodsumowanie(ILogger log, string reasonCode, IReadOnlyDictionary<string, long> counters);

    [LoggerMessage(EventId = IdBladOdbiorcy, EventName = KodBladOdbiorcy, Level = LogLevel.Error, Message = "Odbiorca wejść KZ wyłączony po błędzie ({reasonCode})")]
    static partial void LogBladOdbiorcy(ILogger log, Exception exception, string kzId, string reasonCode, string outcome, IReadOnlyDictionary<string, long> counters);
}

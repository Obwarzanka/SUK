// SYSLIB1015: pola rekordu nie muszą występować w szablonie komunikatu – trafiają jako właściwości strukturalne.
#pragma warning disable SYSLIB1015
using Microsoft.Extensions.Logging;

namespace LcsWidzew.Sim;

/// <summary>
/// Logi techniczne mapy własności (F24, rozdział 25; podzakres KZ 4100–4199 – strona hosta): KZ.OWNERSHIP_MAP_BUILT 4101
/// (skrót i liczniki mapy), KZ.OWNERSHIP_UNRESOLVED 4102 (problem przypisania, co najwyżej <see cref="MaxProblemowWLogu"/>
/// rekordów na publikację, reszta tylko w licznikach) i KZ.OWNERSHIP_MAP_FAILED 4103. Bez treści danych układu poza
/// identyfikatorem obiektu (tylko gdy jest poprawnym identyfikatorem SUK) i kodem problemu; pełna mapa jest artefaktem JSON,
/// nie logiem. Provider i katalog należą do hosta; rdzeń używa wyłącznie neutralnego ILogger.
/// </summary>
public static partial class MapaWlasnosciLog
{
    public const string Modul = "LcsWidzew.MapaWlasnosci";
    public static string Wersja => CoreLogging.Wersja;
    public const int MaxProblemowWLogu = 16;
    public const int IdZbudowano = 4101, IdNierozstrzygniety = 4102, IdBlad = 4103;
    public const string KodZbudowano = "KZ.OWNERSHIP_MAP_BUILT", KodNierozstrzygniety = "KZ.OWNERSHIP_UNRESOLVED", KodBlad = "KZ.OWNERSHIP_MAP_FAILED";

    /// <summary>Definicje do rejestracji w katalogu zdarzeń hosta: eventId, eventCode i pola ponad pola kontekstu schematu.</summary>
    public static IReadOnlyList<(int Id, string Kod, string[] Pola)> Definicje { get; } = Array.AsReadOnly(new (int, string, string[])[]
    {
        (IdZbudowano, KodZbudowano, new[] { "contentHash" }),
        (IdNierozstrzygniety, KodNierozstrzygniety, new[] { "typObiektu" }),
        (IdBlad, KodBlad, Array.Empty<string>()),
    });

    /// <summary>Zapis zbudowanej mapy: jeden rekord 4101 i ograniczona liczba rekordów 4102. Błąd zapisu nie przerywa pracy.</summary>
    public static void Zapisz(ILogger log, MapaWlasnosci m)
    {
        if (log == null || m == null) return;
        try
        {
            int zapisane = 0;
            if (log.IsEnabled(LogLevel.Warning))
                foreach (var p in m.Problemy.Take(MaxProblemowWLogu)) { LogNierozstrzygniety(log, p.Rodzaj, Identyfikator(p.Id), p.Kod); zapisane++; }
            if (!log.IsEnabled(LogLevel.Information)) return;
            var liczniki = new Dictionary<string, long>(StringComparer.Ordinal)
            {
                ["posts"] = m.Posterunki.Count,
                ["objects"] = m.Obiekty.Count,
                ["assigned"] = m.Obiekty.Count(o => o.Status == MapaWlasnosci.Status.Przypisany),
                ["ambiguous"] = m.Obiekty.Count(o => o.Status == MapaWlasnosci.Status.Niejednoznaczny),
                ["unknown"] = m.Obiekty.Count(o => o.Status == MapaWlasnosci.Status.Nieznany),
                ["blockEnds"] = m.KonceBlokad.Count,
                ["blocks"] = m.Szlaki.Count,
                ["routes"] = m.LiczbaPrzebiegow,
                ["boundaries"] = m.Granice.Count,
                ["problems"] = m.Problemy.Count,
                ["problemsLogged"] = zapisane,
                ["problemsSuppressed"] = m.Problemy.Count - zapisane,
            };
            LogZbudowano(log, m.Sha256, liczniki, m.Problemy.Count == 0 ? "Accepted" : "Degraded", Math.Max(0, m.CzasBudowyUs));
        }
        catch (Exception) { }   // diagnostyka nie może przerwać startu świata ani zmienić symulacji
    }

    /// <summary>Błąd budowy mapy (dane niezgodne z modelem); szczegóły wyjątku redaguje adapter hosta.</summary>
    public static void Blad(ILogger log, Exception ex)
    {
        if (log == null) return;
        try { LogBlad(log, ex, "OWNERSHIP_MAP_BUILD_FAILED", "Failed"); } catch (Exception) { }
    }

    /// <summary>Identyfikator SUK do pola targetId: 1–128 znaków bez białych i sterujących; inaczej pole pomijane (pełna wartość jest w mapie JSON).</summary>
    internal static string Identyfikator(string id) =>
        !string.IsNullOrEmpty(id) && id.Length <= 128 && !id.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c == (char)0x2028 || c == (char)0x2029) ? id : null;

    [LoggerMessage(EventId = IdZbudowano, EventName = KodZbudowano, Level = LogLevel.Information, Message = "Zbudowano mapę własności KZ ({outcome})")]
    static partial void LogZbudowano(ILogger log, string contentHash, IReadOnlyDictionary<string, long> counters, string outcome, long durationUs);

    [LoggerMessage(EventId = IdNierozstrzygniety, EventName = KodNierozstrzygniety, Level = LogLevel.Warning, Message = "Mapa własności: nierozstrzygnięte przypisanie ({reasonCode})")]
    static partial void LogNierozstrzygniety(ILogger log, string typObiektu, string targetId, string reasonCode);

    [LoggerMessage(EventId = IdBlad, EventName = KodBlad, Level = LogLevel.Error, Message = "Nie zbudowano mapy własności KZ ({reasonCode})")]
    static partial void LogBlad(ILogger log, Exception exception, string reasonCode, string outcome);
}

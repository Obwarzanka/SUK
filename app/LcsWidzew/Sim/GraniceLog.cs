#pragma warning disable SYSLIB1015
using Microsoft.Extensions.Logging;

namespace LcsWidzew.Sim;

/// <summary>F24/25: wyłącznie ograniczone metadane adapterów, bez treści poleceń i danych operatora.</summary>
public static partial class GraniceLog
{
    public const string Modul = "LcsWidzew.Granice";
    public static string Wersja => CoreLogging.Wersja;
    public const int MaxSzczegolow = 16;
    public static IReadOnlyList<(int Id, string Kod, string[] Pola)> Definicje { get; } = Array.AsReadOnly(new (int, string, string[])[]
    {
        (4130, "KZ.BOUNDARY_READY", Array.Empty<string>()),
        (4131, "KZ.COMMAND_CONTEXT", Array.Empty<string>()),
        (4132, "KZ.TRAFFIC_PROJECTION", Array.Empty<string>()),
        (4133, "KZ.BOUNDARY_SUMMARY", Array.Empty<string>()),
    });

    internal static void Zapisz(ILogger log, int id, long tick, long time, long nr, IReadOnlyDictionary<string, long> counters)
    {
        if (log == null) return;
        // Identyfikatory pochodzą z liczników hosta. Żadnych nieufnych nazw/treści przed kolejką.
        string correlationId = nr > 0 ? "BOUNDARY-" + nr.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
        try
        {
            switch (id)
            {
                case 4130: Gotowe(log, tick, time, counters); break;
                case 4131: Kontekst(log, correlationId, tick, time, counters); break;
                case 4132: Projekcja(log, correlationId, tick, time, counters); break;
                case 4133: Podsumowanie(log, tick, time, counters); break;
            }
        }
        catch (Exception) { } // Diagnostyka nie zmienia decyzji ani skutków świata.
    }

    [LoggerMessage(EventId = 4130, EventName = "KZ.BOUNDARY_READY", Level = LogLevel.Information, Message = "Adaptery kontekstu operatora i ruchu gotowe")]
    static partial void Gotowe(ILogger log, long serverTick, long simulationTimeMs, IReadOnlyDictionary<string, long> counters);
    [LoggerMessage(EventId = 4131, EventName = "KZ.COMMAND_CONTEXT", Level = LogLevel.Information, Message = "Polecenie przyjęte do parsera z jawnym kontekstem")]
    static partial void Kontekst(ILogger log, string correlationId, long serverTick, long simulationTimeMs, IReadOnlyDictionary<string, long> counters);
    [LoggerMessage(EventId = 4132, EventName = "KZ.TRAFFIC_PROJECTION", Level = LogLevel.Information, Message = "Utworzono projekcję zatwierdzonego stanu dla ruchu")]
    static partial void Projekcja(ILogger log, string correlationId, long serverTick, long simulationTimeMs, IReadOnlyDictionary<string, long> counters);
    [LoggerMessage(EventId = 4133, EventName = "KZ.BOUNDARY_SUMMARY", Level = LogLevel.Information, Message = "Podsumowanie granic operatora i ruchu")]
    static partial void Podsumowanie(ILogger log, long serverTick, long simulationTimeMs, IReadOnlyDictionary<string, long> counters);
}

/// <summary>Liczniki świata; szczegóły do 16 każdego rodzaju, podsumowanie co 3000 cykli i na końcu.</summary>
internal sealed class DiagnostykaGranic(Simulator s, ILogger logger)
{
    long polecenia, projekcje, pominiete, ostatniCykl = -3000;
    long ostatniePolecenia = -1, ostatnieProjekcje = -1;
    long plan, sygnaly, utwierdzenia, limity;
    bool gotowe;

    IReadOnlyDictionary<string, long> Liczniki => new Dictionary<string, long>
    {
        ["commands"] = polecenia, ["projections"] = projekcje, ["detailsSuppressed"] = pominiete,
        ["committedPlan"] = plan, ["signals"] = sygnaly, ["lockedSections"] = utwierdzenia, ["speedLimits"] = limity,
    };
    void Zapisz(int id, long nr = 0) => GraniceLog.Zapisz(logger, id, s.NrCyklu, (long)Math.Max(0, Math.Round(s.T * 1000)), nr, Liczniki);
    void Gotowe() { if (!gotowe) { gotowe = true; Zapisz(4130); } }
    internal void Polecenie()
    {
        Gotowe(); polecenia++;
        if (polecenia <= GraniceLog.MaxSzczegolow) Zapisz(4131, polecenia); else pominiete++;
        PodsumujOkresowo();
    }
    internal void Projekcja(long numer, int liczbaSygnalow, int liczbaUtwierdzen, int liczbaLimitow)
    {
        Gotowe(); projekcje++; plan = numer; sygnaly = liczbaSygnalow; utwierdzenia = liczbaUtwierdzen; limity = liczbaLimitow;
        if (projekcje <= GraniceLog.MaxSzczegolow) Zapisz(4132, numer); else pominiete++;
        PodsumujOkresowo();
    }
    void PodsumujOkresowo() { if (s.NrCyklu - ostatniCykl >= 3000) PodsumujNaKoniec(); }
    internal void PodsumujNaKoniec()
    {
        if (!gotowe || ostatniePolecenia == polecenia && ostatnieProjekcje == projekcje) return;
        ostatniCykl = s.NrCyklu; ostatniePolecenia = polecenia; ostatnieProjekcje = projekcje;
        Zapisz(4133);
    }
}

public partial class Simulator
{
    public void PodsumujGranice() => Granice.PodsumujNaKoniec();
}

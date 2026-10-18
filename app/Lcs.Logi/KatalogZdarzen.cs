using System.Collections.Concurrent;

namespace Lcs.Logi;

/// <summary>Definicja zdarzenia z katalogu: stały eventId/eventCode, strumień i pola dozwolone ponad pola kontekstu schematu.</summary>
public sealed class DefinicjaZdarzenia
{
    public int Id { get; }
    public string Kod { get; }
    public string Strumien { get; }
    public IReadOnlyCollection<string> Pola => pola;
    readonly HashSet<string> pola;

    public DefinicjaZdarzenia(int id, string kod, string strumien, params string[] pola)
    {
        Id = id; Kod = kod; Strumien = strumien;
        this.pola = new HashSet<string>(pola ?? Array.Empty<string>(), StringComparer.Ordinal);
    }

    internal bool Dozwolone(string pole) => pola.Contains(pole);
}

/// <summary>
/// Katalog zdarzeń v1 (<c>kontrakty/logi/katalog_zdarzen_v1.json</c>): podzakresy eventId z prefiksami eventCode (kopia w kodzie,
/// zgodność sprawdza test) i rejestr definicji kodów. Rekord z eventId spoza katalogu, z rezerwy albo z niezgodnym kodem
/// dostaje kod LOG.EVENT_UNCATALOGUED i pole originalEventId – nie znika i nie udaje poprawnego zdarzenia.
/// </summary>
public static class KatalogZdarzen
{
    public readonly record struct Podzakres(int Od, int Do, string Prefiks);

    /// <summary>Podzakresy w kolejności z pliku katalogu; „REZERWA” nie przyjmuje rekordów.</summary>
    public static readonly IReadOnlyList<Podzakres> Podzakresy = new Podzakres[]
    {
        new(1000, 1099, "PROC"), new(1100, 1199, "CFG"), new(1200, 1999, "REZERWA"),
        new(2000, 2099, "GATE"), new(2100, 2199, "AUTH"), new(2200, 2299, "PROTO"), new(2300, 2399, "IPC"), new(2400, 2499, "ACCT"), new(2500, 2599, "SANCT"), new(2600, 2999, "REZERWA"),
        new(3000, 3099, "WORLD"), new(3100, 3199, "WORLD"), new(3200, 3299, "CKPT"), new(3300, 3399, "GROUP"), new(3400, 3499, "DEV"), new(3500, 3599, "INSTR"), new(3600, 3699, "REJ"), new(3700, 3999, "REZERWA"),
        new(4000, 4099, "ABI"), new(4100, 4199, "KZ"), new(4200, 4999, "KZDIAG"),
        new(5000, 5099, "TRAFFIC"), new(5100, 5199, "TT"), new(5200, 5299, "AIOP"), new(5300, 5399, "NBR"), new(5400, 5999, "REZERWA"),
        new(6000, 6099, "DESK"), new(6100, 6199, "SP"), new(6200, 6299, "VIEW"), new(6300, 6999, "REZERWA"),
        new(7000, 7099, "COMM"), new(7100, 7199, "MEDIA"), new(7200, 7999, "REZERWA"),
        new(8000, 8099, "UPD"), new(8100, 8199, "EDUT"), new(8200, 8299, "IMG"), new(8300, 8399, "CALC"), new(8400, 8499, "KOMP"), new(8500, 8599, "ADMIN"), new(8600, 8699, "ADMAPI"), new(8700, 8799, "DISP"), new(8800, 8999, "REZERWA"),
        new(9000, 9099, "LOG"), new(9100, 9199, "LOG"), new(9200, 9299, "LOG"), new(9300, 9999, "REZERWA"),
    };

    static readonly ConcurrentDictionary<int, DefinicjaZdarzenia> definicje = new();
    static long odrzuconeDefinicje;

    /// <summary>Liczba definicji odrzuconych przy rejestracji (zły zakres, kod, strumień lub nazwa pola).</summary>
    public static long OdrzuconeDefinicje => Interlocked.Read(ref odrzuconeDefinicje);

    public static Podzakres? PodzakresDla(int id)
    {
        foreach (var p in Podzakresy) if (id >= p.Od && id <= p.Do) return p;
        return null;
    }

    /// <summary>Kod zgodny z podzakresem: eventId poza rezerwą, prefiks kodu = prefiks podzakresu, poprawna postać kodu.</summary>
    public static bool ZgodnyZPodzakresem(int id, string kod)
    {
        if (!KontraktLogu.PoprawnyKodZdarzenia(kod)) return false;
        var p = PodzakresDla(id);
        if (p is null || p.Value.Prefiks == "REZERWA") return false;
        return kod.IndexOf('.') == p.Value.Prefiks.Length && kod.StartsWith(p.Value.Prefiks, StringComparison.Ordinal);
    }

    /// <summary>
    /// Rejestruje definicję kodu modułu. Nie rzuca wyjątku (logi nie mogą zatrzymać programu): definicja niezgodna z katalogiem
    /// albo sprzeczna z już zarejestrowaną jest odrzucana i liczona, a jej rekordy trafią jako LOG.EVENT_UNCATALOGUED.
    /// </summary>
    public static bool Zarejestruj(DefinicjaZdarzenia d)
    {
        bool ok = d != null && ZgodnyZPodzakresem(d.Id, d.Kod) && KontraktLogu.Strumien.Poprawny(d.Strumien)
            && d.Pola.All(p => KontraktLogu.PoprawnaNazwaPola(p) && !KontraktLogu.ZakazanaNazwa(p) && !KontraktLogu.Zastrzezone.Contains(p) && !KontraktLogu.PolaMostu.Contains(p));
        if (ok)
        {
            var istniejaca = definicje.GetOrAdd(d.Id, d);
            if (ReferenceEquals(istniejaca, d) || (istniejaca.Kod == d.Kod && istniejaca.Strumien == d.Strumien && istniejaca.Pola.Count == d.Pola.Count && d.Pola.All(istniejaca.Dozwolone)))
                return true;
        }
        Interlocked.Increment(ref odrzuconeDefinicje);
        return false;
    }

    public static DefinicjaZdarzenia Definicja(int id) => definicje.TryGetValue(id, out var d) ? d : null;
    public static IReadOnlyCollection<DefinicjaZdarzenia> Zarejestrowane => definicje.Values.OrderBy(d => d.Id).ToList();

    /// <summary>Rozpoznanie eventId/eventCode z rekordu: definicja z rejestru, kod zgodny z podzakresem bez definicji albo null (poza katalogiem).</summary>
    internal static DefinicjaZdarzenia Rozpoznaj(int id, string kod)
    {
        if (definicje.TryGetValue(id, out var d)) return d.Kod == kod ? d : null;
        return ZgodnyZPodzakresem(id, kod) ? new DefinicjaZdarzenia(id, kod, KontraktLogu.Strumien.Diagnostyka) : null;
    }

    static KatalogZdarzen()
    {
        foreach (var d in ZdarzeniaLogow.Wszystkie) Zarejestruj(d);
        foreach (var d in ZdarzeniaProcesu.Definicje) Zarejestruj(d);
    }
}

/// <summary>Kody infrastruktury logowania (9000–9299) – emituje je adapter i worker, nigdy przez własną kolejkę wejściową.</summary>
public static class ZdarzeniaLogow
{
    public const string ModulAdaptera = "Diagnostics.Adapter";
    public const string ModulWorkera = "Diagnostics.Writer";

    public static readonly DefinicjaZdarzenia Uruchomiono = new(9001, "LOG.STARTED", KontraktLogu.Strumien.Diagnostyka, "profile");
    public static readonly DefinicjaZdarzenia SpozaKatalogu = new(9002, "LOG.EVENT_UNCATALOGUED", KontraktLogu.Strumien.Diagnostyka, "originalEventId");
    public static readonly DefinicjaZdarzenia Proba = new(9003, "LOG.PROBE", KontraktLogu.Strumien.Diagnostyka, "opis");
    public static readonly DefinicjaZdarzenia Pominieto = new(9101, "LOG.DIAGNOSTICS_DROPPED", KontraktLogu.Strumien.Diagnostyka);
    public static readonly DefinicjaZdarzenia BladZapisu = new(9102, "LOG.SINK_FAILED", KontraktLogu.Strumien.Diagnostyka);
    public static readonly DefinicjaZdarzenia ZamkniecieNiepelne = new(9103, "LOG.SHUTDOWN_FLUSH_INCOMPLETE", KontraktLogu.Strumien.Diagnostyka);
    public static readonly DefinicjaZdarzenia ZaDuzy = new(9104, "LOG.RECORD_TOO_LARGE", KontraktLogu.Strumien.Diagnostyka, "originalEventId");
    public static readonly DefinicjaZdarzenia Zatrzymano = new(9105, "LOG.STOPPED", KontraktLogu.Strumien.Diagnostyka);
    public static readonly DefinicjaZdarzenia MaloMiejsca = new(9201, "LOG.DISK_SPACE_LOW", KontraktLogu.Strumien.Diagnostyka);
    public static readonly DefinicjaZdarzenia MiejscePrzywrocone = new(9202, "LOG.DISK_SPACE_RESTORED", KontraktLogu.Strumien.Diagnostyka);

    public static IEnumerable<DefinicjaZdarzenia> Wszystkie => new[] { Uruchomiono, SpozaKatalogu, Proba, Pominieto, BladZapisu, ZamkniecieNiepelne, ZaDuzy, Zatrzymano, MaloMiejsca, MiejscePrzywrocone };
}

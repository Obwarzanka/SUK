using System.Text.RegularExpressions;

namespace Lcs.Logi;

/// <summary>
/// Kontrakt rekordu logu v1 (<c>kontrakty/logi/rekord_logu_v1.schema.json</c>, rozdział 25 § 4 i § 6): stałe, limity B01,
/// lista dozwolonych pól kontekstu z typami i zależności pól. Adapter sprawdza pola przed kolejką; pole niezgodne
/// z kontraktem jest odrzucane (i liczone), a nie poprawiane na domysł.
/// </summary>
public static class KontraktLogu
{
    /// <summary>schemaVersion rekordu.</summary>
    public const int WersjaSchematu = 1;

    // B01 – rekord diagnostyczny (x-suk-limity w schemacie)
    public const int MaxBajtowRekordu = 8192;
    public const int MaxZnakowTekstu = 512;
    public const int MaxZnakowWyjatku = 4096;
    public const int MaxPol = 64;
    public const int MaxZnakowIdZewnetrznego = 64;
    public const int MaxZnakowIdentyfikatora = 128;
    public const int MaxLicznikow = 16;

    /// <summary>Zamknięta kategoria strumienia – nadawana w miejscu emisji (definicja zdarzenia), nigdy przez wywołującego.</summary>
    public static class Strumien
    {
        public const string Diagnostyka = "diagnostic";
        public const string Domenowy = "domain";
        public const string Audyt = "audit";
        public const string Odtworzenie = "replay";
        internal static bool Poprawny(string s) => s is Diagnostyka or Domenowy or Audyt or Odtworzenie;
    }

    /// <summary>Pola nadawane wyłącznie przez adapter; wartości przekazane przez moduł są odrzucane.</summary>
    internal static readonly HashSet<string> Zastrzezone = new(StringComparer.Ordinal)
    {
        "schemaVersion", "stream", "eventCode", "eventId", "severity", "moduleId", "moduleVersion", "processRunId", "recordSeq", "monoElapsedUs",
    };

    /// <summary>Pola dodawane przez most MEL→Serilog, które adapter zużywa albo pomija (nie trafiają do pliku).</summary>
    internal static readonly HashSet<string> PolaMostu = new(StringComparer.Ordinal) { "SourceContext", "EventId", "Scope", "{OriginalFormat}" };

    internal enum Rodzaj { Identyfikator, IdZewnetrzny, Uuid, Nieujemna, Grupa, Kanal, NazwaTypowana, KodPowodu, Liczniki }

    /// <summary>Nazwane pola kontekstu i korelacji ze schematu – dozwolone w każdym rekordzie, z kontrolą typu.</summary>
    internal static readonly Dictionary<string, Rodzaj> PolaKontekstu = new(StringComparer.Ordinal)
    {
        ["worldId"] = Rodzaj.Identyfikator,
        ["worldRunId"] = Rodzaj.Uuid,
        ["serverTick"] = Rodzaj.Nieujemna,
        ["simulationTimeMs"] = Rodzaj.Nieujemna,
        ["postId"] = Rodzaj.Identyfikator,
        ["kzId"] = Rodzaj.Identyfikator,
        ["kzEpoch"] = Rodzaj.Nieujemna,
        ["groupId"] = Rodzaj.Grupa,
        ["channelId"] = Rodzaj.Kanal,
        ["groupRunId"] = Rodzaj.Uuid,
        ["selectionEpoch"] = Rodzaj.Nieujemna,
        ["requestId"] = Rodzaj.IdZewnetrzny,
        ["correlationId"] = Rodzaj.IdZewnetrzny,
        ["effectId"] = Rodzaj.Identyfikator,
        ["effectState"] = Rodzaj.NazwaTypowana,
        ["controlLeaseId"] = Rodzaj.Identyfikator,
        ["controlEpoch"] = Rodzaj.Nieujemna,
        ["authorizationVersion"] = Rodzaj.Nieujemna,
        ["actorRef"] = Rodzaj.Identyfikator,
        ["targetId"] = Rodzaj.Identyfikator,
        ["reasonCode"] = Rodzaj.KodPowodu,
        ["outcome"] = Rodzaj.NazwaTypowana,
        ["durationUs"] = Rodzaj.Nieujemna,
        ["counters"] = Rodzaj.Liczniki,
        ["droppedCount"] = Rodzaj.Nieujemna,
        ["windowMs"] = Rodzaj.Nieujemna,
    };

    /// <summary>Zależności pól ze schematu („dependencies”): pole bez wymaganych pól jest odrzucane.</summary>
    internal static readonly (string Pole, string[] Wymaga)[] Zaleznosci =
    {
        ("groupId", new[] { "kzId" }),
        ("channelId", new[] { "groupId", "kzId" }),
        ("groupRunId", new[] { "groupId", "kzId" }),
        ("kzEpoch", new[] { "kzId" }),
        ("selectionEpoch", new[] { "kzId" }),
        ("effectState", new[] { "effectId" }),
        ("worldRunId", new[] { "worldId" }),
        ("serverTick", new[] { "worldId", "worldRunId" }),
        ("simulationTimeMs", new[] { "worldId", "worldRunId" }),
        ("controlEpoch", new[] { "controlLeaseId" }),
    };

    static readonly Regex NazwaPolaWzor = new(@"^[A-Za-z][A-Za-z0-9_]{0,63}$", RegexOptions.CultureInvariant);
    static readonly Regex IdZewnetrznyWzor = new(@"^[A-Za-z0-9][A-Za-z0-9._:-]{0,63}$", RegexOptions.CultureInvariant);
    static readonly Regex UuidWzor = new(@"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", RegexOptions.CultureInvariant);
    static readonly Regex NazwaTypowanaWzor = new(@"^[A-Z][A-Za-z]{1,39}$", RegexOptions.CultureInvariant);
    static readonly Regex KodPowoduWzor = new(@"^[A-Z][A-Z0-9_]{1,63}$", RegexOptions.CultureInvariant);
    static readonly Regex LicznikWzor = new(@"^[a-z][A-Za-z0-9]{0,47}$", RegexOptions.CultureInvariant);
    static readonly Regex ModulWzor = new(@"^[A-Z][A-Za-z0-9]*(\.[A-Z][A-Za-z0-9]*)*$", RegexOptions.CultureInvariant);
    static readonly Regex WersjaWzor = new(@"^[A-Za-z0-9][A-Za-z0-9.+_-]{0,63}$", RegexOptions.CultureInvariant);
    static readonly Regex KodZdarzeniaWzor = new(@"^[A-Z][A-Z0-9]{1,15}(\.[A-Z][A-Z0-9_]{0,47})+$", RegexOptions.CultureInvariant);

    /// <summary>Fragmenty nazw pól, które nigdy nie trafiają do logu (25 § 7); ochrona dodatkowa obok listy dozwolonych pól.</summary>
    static readonly string[] ZakazaneFragmenty =
    {
        "password", "passwd", "haslo", "hasło", "token", "jwt", "authorization", "cookie", "secret", "sekret",
        "privatekey", "apikey", "recoverycode", "enrollmentcode", "connectionstring",
    };

    public static bool PoprawnaNazwaPola(string n) => n != null && NazwaPolaWzor.IsMatch(n);
    public static bool PoprawnyModul(string m) => m != null && m.Length <= 64 && ModulWzor.IsMatch(m);
    public static bool PoprawnaWersja(string w) => w != null && WersjaWzor.IsMatch(w);
    public static bool PoprawnyKodZdarzenia(string k) => k != null && k.Length <= 64 && KodZdarzeniaWzor.IsMatch(k);
    public static bool PoprawnyKodPowodu(string k) => k != null && KodPowoduWzor.IsMatch(k);
    public static bool PoprawnaNazwaLicznika(string k) => k != null && LicznikWzor.IsMatch(k) && !ZakazanaNazwa(k);
    internal static bool PoprawnyIdZewnetrzny(string s) => s != null && IdZewnetrznyWzor.IsMatch(s);
    internal static bool PoprawnaNazwaTypowana(string s) => s != null && NazwaTypowanaWzor.IsMatch(s);
    internal static bool PoprawnyUuid(string s) => s != null && UuidWzor.IsMatch(s);

    /// <summary>Nazwa pola zawiera fragment typowy dla sekretu (bez rozróżniania wielkości liter).</summary>
    public static bool ZakazanaNazwa(string n)
    {
        if (n == null) return false;
        foreach (var f in ZakazaneFragmenty)
            if (n.Contains(f, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>Identyfikator SUK: 1–128 znaków bez spacji i znaków sterujących (C0, DEL, C1, U+2028/2029).</summary>
    internal static bool PoprawnyIdentyfikator(string s)
    {
        if (string.IsNullOrEmpty(s) || s.Length > MaxZnakowIdentyfikatora) return false;
        foreach (var c in s)
            if (c <= ' ' || (c >= '\u007F' && c <= '\u009F') || c == '\u2028' || c == '\u2029') return false;
        return true;
    }

    /// <summary>Poziom MEL (severity) i odpowiadający mu poziom Serilog (@l).</summary>
    internal static string Severity(Serilog.Events.LogEventLevel l) => l switch
    {
        Serilog.Events.LogEventLevel.Verbose => "Trace",
        Serilog.Events.LogEventLevel.Debug => "Debug",
        Serilog.Events.LogEventLevel.Information => "Information",
        Serilog.Events.LogEventLevel.Warning => "Warning",
        Serilog.Events.LogEventLevel.Error => "Error",
        _ => "Critical",
    };

    internal static Serilog.Events.LogEventLevel Poziom(Microsoft.Extensions.Logging.LogLevel l) => l switch
    {
        Microsoft.Extensions.Logging.LogLevel.Trace => Serilog.Events.LogEventLevel.Verbose,
        Microsoft.Extensions.Logging.LogLevel.Debug => Serilog.Events.LogEventLevel.Debug,
        Microsoft.Extensions.Logging.LogLevel.Information => Serilog.Events.LogEventLevel.Information,
        Microsoft.Extensions.Logging.LogLevel.Warning => Serilog.Events.LogEventLevel.Warning,
        Microsoft.Extensions.Logging.LogLevel.Error => Serilog.Events.LogEventLevel.Error,
        _ => Serilog.Events.LogEventLevel.Fatal,
    };
}

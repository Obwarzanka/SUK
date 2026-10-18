using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Serilog.Formatting.Compact;
using MelILogger = Microsoft.Extensions.Logging.ILogger;
using MelILoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;

namespace Lcs.Logi;

/// <summary>Tożsamość procesu w logach: processRunId, rosnący recordSeq i czas monotoniczny od startu procesu.</summary>
internal static class Proces
{
    public static readonly string RunId = Guid.NewGuid().ToString("D");
    static readonly long start = Stopwatch.GetTimestamp();
    static readonly long przesuniecieUs = Przesuniecie();
    static long seq;

    public static long NastepnyNumer() => Interlocked.Increment(ref seq);
    public static long MonoUs() => przesuniecieUs + (long)((Stopwatch.GetTimestamp() - start) * (1_000_000.0 / Stopwatch.Frequency));

    /// <summary>Jednorazowe dowiązanie licznika do startu procesu (później tylko zegar monotoniczny).</summary>
    static long Przesuniecie()
    {
        try
        {
            using var p = Process.GetCurrentProcess();
            var us = (long)((DateTime.UtcNow - p.StartTime.ToUniversalTime()).Ticks / 10);
            return us > 0 ? us : 0;
        }
        catch { return 0; }
    }
}

/// <summary>Rekord w kolejce: wyłącznie ograniczone, niemutowalne pola (bez referencji do świata ani obiektu wyjątku – 25 § 6).</summary>
internal sealed class RekordLogu
{
    public readonly DateTimeOffset Czas;
    public readonly LogEventLevel Poziom;
    public readonly string Szablon;
    public readonly KeyValuePair<string, object>[] Pola;
    public readonly string Wyjatek;
    public readonly ActivityTraceId Trace;
    public readonly ActivitySpanId Span;
    public readonly string Modul;
    public readonly int Bajty;

    public RekordLogu(DateTimeOffset czas, LogEventLevel poziom, string szablon, KeyValuePair<string, object>[] pola, string wyjatek, ActivityTraceId trace, ActivitySpanId span, string modul)
    {
        Czas = czas; Poziom = poziom; Szablon = szablon; Pola = pola; Wyjatek = wyjatek; Trace = trace; Span = span; Modul = modul;
        Bajty = Szacuj(szablon, pola, wyjatek);
    }

    /// <summary>Konserwatywny szacunek pamięci rekordu (nagłówki obiektów, tablica, teksty UTF-16, opakowania liczb).</summary>
    static int Szacuj(string szablon, KeyValuePair<string, object>[] pola, string wyjatek)
    {
        long b = 160 + 24 + 2L * szablon.Length + (wyjatek == null ? 0 : 24 + 2L * wyjatek.Length) + 32 + 16L * pola.Length;
        foreach (var kv in pola)
            b += kv.Value switch
            {
                string s => 24 + 2L * s.Length,
                KeyValuePair<string, long>[] a => 32 + a.Sum(x => 48L + 2L * x.Key.Length),
                null => 0,
                _ => 24,
            };
        return (int)Math.Min(b, int.MaxValue);
    }
}

/// <summary>Jedyne ujście gorącego loggera: zamienia zdarzenie na rekord i wpisuje do ograniczonej kolejki. Nigdy nie rzuca wyjątku.</summary>
internal sealed class AdapterKolejki : ILogEventSink
{
    readonly SilnikLogow silnik;
    public AdapterKolejki(SilnikLogow s) => silnik = s;
    public void Emit(LogEvent e)
    {
        try { silnik.Przyjmij(e); }
        catch { silnik.Strata(null, "ADAPTER_ERROR"); }
    }
}

/// <summary>Zapasowe ujście łańcucha FallbackChain: tylko liczy rekordy, których sink plikowy nie zapisał (bez I/O).</summary>
internal sealed class LicznikAwarii : ILogEventSink
{
    readonly SilnikLogow silnik;
    public LicznikAwarii(SilnikLogow s) => silnik = s;
    public void Emit(LogEvent e) => silnik.AwariaZapisu(e);
}

/// <summary>Wyjątek po redakcji – CompactJsonFormatter zapisuje jako @x wynik ToString(), czyli wyłącznie zredagowany tekst.</summary>
internal sealed class WyjatekZredagowany : Exception
{
    readonly string tekst;
    public WyjatekZredagowany(string tekst) : base("") => this.tekst = tekst;
    public override string ToString() => tekst;
    public override string StackTrace => null;
}

/// <summary>
/// Silnik logów SUK (25 § 3–§ 7): moduły dostają <see cref="MelILogger"/> (MEL), most Serilog.Extensions.Logging kieruje zdarzenia
/// do jednego ujścia – <see cref="AdapterKolejki"/> (lista dozwolonych pól, redakcja, limity, TryWrite bez czekania). Osobny wątek
/// workera ma prywatny logger Serilog z gotowym sinkiem File + CompactJsonFormatter (rotacja czasem i rozmiarem, retencja)
/// i nie odsyła niczego do kolejki wejściowej. Na wątku emitującym nie ma I/O.
/// </summary>
public sealed class SilnikLogow : IDisposable
{
    readonly OpcjeLogow opcje;
    readonly string katalog;
    readonly Logger goracy;
    readonly SerilogLoggerFactory fabryka;
    readonly LoggingLevelSwitch poziom;
    readonly LogEventLevel poziomBazowy;
    readonly Channel<RekordLogu> kanal;
    readonly Logger prywatny;
    readonly Thread watek;
    readonly ConcurrentDictionary<string, (string Id, string Wersja)> moduly = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<(string Modul, string Powod), StrongBox<long>> straty = new();
    readonly Dictionary<(string Modul, string Powod), long> zgloszone = new();
    readonly ConcurrentDictionary<string, Serilog.Events.MessageTemplate> szablony = new(StringComparer.Ordinal);
    static readonly Serilog.Parsing.MessageTemplateParser parser = new();
    readonly string wersjaBiblioteki = typeof(SilnikLogow).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    readonly int limitRekordow, rezerwaRekordow;
    readonly long limitBajtow, rezerwaBajtow;
    readonly object zamek = new();

    int wKolejce, maxWKolejce;
    long bajtyWKolejce, maxBajtow;
    long przyjete, przekazane, wlasne, bledyZapisu, obciete, zastapione, stratyRazem;
    volatile bool zatrzymany, przerwij;
    volatile StanZapisu stanZapisu = StanZapisu.Uruchamianie;
    volatile string powodNiegotowosci;
    volatile StanMiejsca stanMiejsca = StanMiejsca.Nieznany;
    double wolneProcent = double.NaN;
    long ostatniePodsumowanieUs, poczatekAwariiUs, bledyNaPoczatkuAwarii;
    double czasZamknieciaMs;
    bool zamkniecieKompletne = true;

    public string Katalog => katalog;
    public string Aplikacja => opcje.Aplikacja;
    /// <summary>Fabryka MEL dla modułów (kategoria = moduleId).</summary>
    public MelILoggerFactory Fabryka => fabryka;
    /// <summary>Gorący logger Serilog (do integracji z hostem MEL przez <see cref="RozszerzeniaHosta"/>).</summary>
    internal Logger Goracy => goracy;

    SilnikLogow(OpcjeLogow o)
    {
        o.Sprawdz();
        opcje = o;
        katalog = o.KatalogDocelowy();
        limitRekordow = o.RekordyKolejki;
        limitBajtow = o.BajtyKolejki;
        rezerwaRekordow = Math.Max(8, limitRekordow / 16);   // budżet poważnych błędów (25 § 6)
        rezerwaBajtow = Math.Max(8 * 1024, limitBajtow / 16);
        poziomBazowy = KontraktLogu.Poziom(o.MinimalnyPoziom);
        poziom = new LoggingLevelSwitch(poziomBazowy);
        kanal = Channel.CreateUnbounded<RekordLogu>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false, AllowSynchronousContinuations = false });

        var formatter = new OgraniczonyFormatter(new CompactJsonFormatter(), KontraktLogu.MaxBajtowRekordu,
            () => Interlocked.Increment(ref obciete), (e, kod) => { Interlocked.Increment(ref zastapione); Strata(ModulZdarzenia(e), "RECORD_TOO_LARGE"); });
        var cfg = new LoggerConfiguration().MinimumLevel.Verbose();
        prywatny = (o.ZapisTestowy != null
            ? cfg.WriteTo.FallbackChain(p => p.Sink(o.ZapisTestowy), f => f.Sink(new LicznikAwarii(this)))
            : cfg.WriteTo.FallbackChain(
                p => p.File(formatter, Path.Combine(katalog, o.Aplikacja + "-.jsonl"),
                    fileSizeLimitBytes: o.BajtyPliku, rollOnFileSizeLimit: true, rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: o.Pliki, retainedFileTimeLimit: o.WiekPliku,
                    buffered: true, flushToDiskInterval: o.OkresGrupowania, shared: false),
                f => f.Sink(new LicznikAwarii(this)))).CreateLogger();

        // Gorąca ścieżka: jedyne ujście = adapter pamięciowy. Bez sinku plikowego, sieciowego i EventLog (25 § 3.4–3.5).
        goracy = new LoggerConfiguration().MinimumLevel.ControlledBy(poziom).WriteTo.Sink(new AdapterKolejki(this)).CreateLogger();
        fabryka = new SerilogLoggerFactory(goracy, dispose: false);

        NadzorMiejsca(false);   // pierwszy pomiar przed pierwszym rekordem (start programu, nie tick)
        ostatniePodsumowanieUs = Proces.MonoUs();
        watek = new Thread(Petla) { IsBackground = true, Name = "SUK logi – zapis", Priority = ThreadPriority.BelowNormal };
        watek.Start();
    }

    /// <summary>Uruchamia silnik (katalog, kolejka, worker). Błędne opcje → ArgumentException; brak katalogu nie jest błędem uruchomienia, tylko stanem Niegotowy.</summary>
    public static SilnikLogow Uruchom(OpcjeLogow o) => new(o ?? throw new ArgumentNullException(nameof(o)));

    /// <summary>Logger modułu: kategoria = moduleId, wersja trafia do każdego rekordu. Rejestracja jest tania i powtarzalna.</summary>
    public MelILogger Modul(string moduleId, string moduleVersion)
    {
        if (!KontraktLogu.PoprawnyModul(moduleId)) return NullLogger.Instance;
        moduly[moduleId] = (moduleId, KontraktLogu.PoprawnaWersja(moduleVersion) ? moduleVersion : "nieznana");
        return fabryka.CreateLogger(moduleId);
    }

    /// <summary>Rejestruje moduł dla kategorii MEL innej niż moduleId (np. nazwa typu w hoście).</summary>
    public void ZarejestrujKategorie(string kategoria, string moduleId, string moduleVersion)
    {
        if (kategoria != null && KontraktLogu.PoprawnyModul(moduleId))
            moduly[kategoria] = (moduleId, KontraktLogu.PoprawnaWersja(moduleVersion) ? moduleVersion : "nieznana");
    }

    (string Id, string Wersja) ModulDla(string kategoria)
    {
        if (kategoria != null && moduly.TryGetValue(kategoria, out var m)) return m;
        return KontraktLogu.PoprawnyModul(kategoria) ? (kategoria, "niezarejestrowany") : ("External", "niezarejestrowany");
    }

    // =========================================================================== gorąca ścieżka (wątek emitujący)

    internal void Przyjmij(LogEvent e)
    {
        if (zatrzymany) { Strata(ModulZKategorii(e), "STOPPED"); return; }
        string kategoria = e.Properties.TryGetValue("SourceContext", out var sc) && sc is ScalarValue { Value: string k } ? k : null;
        var mod = ModulDla(kategoria);

        // Szybka odmowa przy pełnej kolejce – bez budowania rekordu i formatowania wyjątku (koszt przeciążenia na wątku ticku).
        bool powazny = e.Level >= LogEventLevel.Error;
        if (Volatile.Read(ref wKolejce) >= (powazny ? limitRekordow : limitRekordow - rezerwaRekordow)) { Strata(mod.Id, "QUEUE_FULL"); return; }
        // minimalny szacunek rekordu: z wyjątkiem do 8,5 KiB (4096 znaków UTF-16), bez – 256 B; dokładny limit sprawdza Wpisz
        if (Interlocked.Read(ref bajtyWKolejce) + (e.Exception != null ? 8704 : 256) > (powazny ? limitBajtow : limitBajtow - rezerwaBajtow)) { Strata(mod.Id, "QUEUE_BYTES"); return; }

        int id = 0; string kod = null;
        if (e.Properties.TryGetValue("EventId", out var ev) && ev is StructureValue sv)
            foreach (var p in sv.Properties)
            {
                if (p.Name == "Id" && p.Value is ScalarValue { Value: int i }) id = i;
                else if (p.Name == "Name" && p.Value is ScalarValue { Value: string n }) kod = n;
            }
        var def = KatalogZdarzen.Rozpoznaj(id, kod);
        bool spoza = def == null;
        if (spoza) def = ZdarzeniaLogow.SpozaKatalogu;

        int odrzucone = 0, obcieteNa = 0;
        var pola = new List<KeyValuePair<string, object>>(16 + e.Properties.Count);
        DodajWymagane(pola, def, e.Level, mod.Id, mod.Wersja);
        if (spoza) pola.Add(new("originalEventId", (long)id));
        KeyValuePair<string, long>[] liczniki = null;
        int limitPol = KontraktLogu.MaxPol - 20;   // @t, @mt, @l, @x, @tr, @sp i pola wymagane mieszczą się w 64

        foreach (var kv in e.Properties)
        {
            var nazwa = kv.Key;
            if (KontraktLogu.PolaMostu.Contains(nazwa)) continue;
            if (kv.Value is ScalarValue { Value: null }) continue;   // brak wartości = pole nie dotyczy (nie „grupa zasadnicza”, nie błąd)
            if (KontraktLogu.Zastrzezone.Contains(nazwa) || (spoza && nazwa == "originalEventId")) { odrzucone++; continue; }
            object wartosc;
            if (KontraktLogu.PolaKontekstu.TryGetValue(nazwa, out var rodzaj))
            {
                if (rodzaj == KontraktLogu.Rodzaj.Liczniki) { liczniki = Liczniki(kv.Value, ref odrzucone); continue; }
                if (!Kontekst(rodzaj, kv.Value, out wartosc)) { odrzucone++; continue; }
            }
            else if (!spoza && def.Dozwolone(nazwa))
            {
                if (!Dodatkowe(kv.Value, out wartosc, ref obcieteNa)) { odrzucone++; continue; }
            }
            else { odrzucone++; continue; }   // lista dozwolonych pól: nieznane pole nie trafia do rekordu
            if (pola.Count - 10 >= limitPol) { odrzucone++; continue; }
            pola.Add(new(nazwa, wartosc));
        }
        odrzucone += Zaleznosci(pola);

        if (odrzucone > 0 || obcieteNa > 0)
        {
            var l = new List<KeyValuePair<string, long>>(liczniki ?? Array.Empty<KeyValuePair<string, long>>());
            if (odrzucone > 0 && l.All(x => x.Key != "rejectedFields")) l.Add(new("rejectedFields", odrzucone));
            if (obcieteNa > 0 && l.All(x => x.Key != "truncatedFields")) l.Add(new("truncatedFields", obcieteNa));
            liczniki = l.Take(KontraktLogu.MaxLicznikow).ToArray();
        }
        if (liczniki is { Length: > 0 }) pola.Add(new("counters", liczniki));

        var szablon = SzablonZredagowany(e.MessageTemplate?.Text);
        var wyjatek = Redakcja.Wyjatek(e.Exception);
        var r = new RekordLogu(e.Timestamp.ToUniversalTime(), e.Level, szablon, pola.ToArray(), wyjatek,
            e.TraceId ?? default, e.SpanId ?? default, mod.Id);
        Wpisz(r);
    }

    readonly ConcurrentDictionary<string, string> szablonyZredagowane = new(StringComparer.Ordinal);

    /// <summary>Szablon @mt po redakcji (stałe teksty modułów – wynik w pamięci podręcznej, żeby nie powtarzać wzorców w ticku).</summary>
    string SzablonZredagowany(string t)
    {
        if (string.IsNullOrWhiteSpace(t)) return "(brak szablonu)";
        if (szablonyZredagowane.TryGetValue(t, out var z)) return z;
        z = Redakcja.Tekst(t);
        if (szablonyZredagowane.Count < 2048) szablonyZredagowane[t] = z;
        return z;
    }

    void DodajWymagane(List<KeyValuePair<string, object>> pola, DefinicjaZdarzenia def, LogEventLevel l, string modul, string wersja)
    {
        pola.Add(new("schemaVersion", (long)KontraktLogu.WersjaSchematu));
        pola.Add(new("stream", def.Strumien));
        pola.Add(new("eventCode", def.Kod));
        pola.Add(new("eventId", (long)def.Id));
        pola.Add(new("severity", KontraktLogu.Severity(l)));
        pola.Add(new("moduleId", modul));
        pola.Add(new("moduleVersion", wersja));
        pola.Add(new("processRunId", Proces.RunId));
        pola.Add(new("recordSeq", 0L));      // nadawany przy przyjęciu do kolejki
        pola.Add(new("monoElapsedUs", Proces.MonoUs()));
    }

    /// <summary>Przyjęcie do kolejki: limit liczby i bajtów (obowiązuje wcześniejszy), rezerwa dla Error/Fatal, TryWrite bez czekania.</summary>
    void Wpisz(RekordLogu r)
    {
        bool powazny = r.Poziom >= LogEventLevel.Error;
        int limR = powazny ? limitRekordow : limitRekordow - rezerwaRekordow;
        long limB = powazny ? limitBajtow : limitBajtow - rezerwaBajtow;
        int n = Interlocked.Increment(ref wKolejce);
        if (n > limR) { Interlocked.Decrement(ref wKolejce); Strata(r.Modul, "QUEUE_FULL"); return; }
        long b = Interlocked.Add(ref bajtyWKolejce, r.Bajty);
        if (b > limB) { Interlocked.Add(ref bajtyWKolejce, -r.Bajty); Interlocked.Decrement(ref wKolejce); Strata(r.Modul, "QUEUE_BYTES"); return; }
        r.Pola[8] = new("recordSeq", Proces.NastepnyNumer());
        if (!kanal.Writer.TryWrite(r))
        {
            Interlocked.Add(ref bajtyWKolejce, -r.Bajty); Interlocked.Decrement(ref wKolejce); Strata(r.Modul, "STOPPED"); return;
        }
        Interlocked.Increment(ref przyjete);
        Maks(ref maxWKolejce, n); Maks(ref maxBajtow, b);
    }

    static void Maks(ref int pole, int v) { int s; while (v > (s = Volatile.Read(ref pole)) && Interlocked.CompareExchange(ref pole, v, s) != s) { } }
    static void Maks(ref long pole, long v) { long s; while (v > (s = Volatile.Read(ref pole)) && Interlocked.CompareExchange(ref pole, v, s) != s) { } }

    internal void Strata(string modul, string powod, long ile = 1)
    {
        var klucz = (straty.Count < 256 || straty.ContainsKey((modul ?? "External", powod)) ? modul ?? "External" : "Inne", powod);
        var box = straty.GetOrAdd(klucz, _ => new StrongBox<long>());
        Interlocked.Add(ref box.Value, ile);
        Interlocked.Add(ref stratyRazem, ile);
    }

    string ModulZKategorii(LogEvent e) =>
        ModulDla(e.Properties.TryGetValue("SourceContext", out var sc) && sc is ScalarValue { Value: string k } ? k : null).Id;

    static string ModulZdarzenia(LogEvent e) =>
        e != null && e.Properties.TryGetValue("moduleId", out var m) && m is ScalarValue { Value: string s } ? s : "External";

    // ------------------------------------------------------------------ kontrola typów pól

    static bool Calkowita(object o, out long v)
    {
        switch (o)
        {
            case int i: v = i; return true;
            case long l: v = l; return true;
            case short s: v = s; return true;
            case byte b: v = b; return true;
            case sbyte sb: v = sb; return true;
            case ushort us: v = us; return true;
            case uint ui: v = ui; return true;
            case ulong ul when ul <= long.MaxValue: v = (long)ul; return true;
            default: v = 0; return false;
        }
    }

    static bool Kontekst(KontraktLogu.Rodzaj r, LogEventPropertyValue pv, out object w)
    {
        w = null;
        if (pv is not ScalarValue sv) return false;
        var o = sv.Value;
        switch (r)
        {
            case KontraktLogu.Rodzaj.Identyfikator:
                if (o is not string id || !KontraktLogu.PoprawnyIdentyfikator(id) || !ReferenceEquals(Redakcja.Maskuj(id), id)) return false;
                w = id; return true;
            case KontraktLogu.Rodzaj.IdZewnetrzny:
                if (o is not string ze || !KontraktLogu.PoprawnyIdZewnetrzny(ze)) return false;
                w = ze; return true;
            case KontraktLogu.Rodzaj.Uuid:
                if (o is Guid g) { w = g.ToString("D"); return true; }
                if (o is string u && KontraktLogu.PoprawnyUuid(u)) { w = u; return true; }
                return false;
            case KontraktLogu.Rodzaj.Nieujemna:
                if (!Calkowita(o, out var n) || n < 0) return false;
                w = n; return true;
            case KontraktLogu.Rodzaj.Grupa:
                if (!Calkowita(o, out var gr) || (gr != 1 && gr != 2)) return false;
                w = gr; return true;
            case KontraktLogu.Rodzaj.Kanal:
                var k = o is char c ? c.ToString() : o as string;
                if (k is not ("A" or "B")) return false;
                w = k; return true;
            case KontraktLogu.Rodzaj.NazwaTypowana:
                var t = o is Enum en ? en.ToString() : o as string;
                if (!KontraktLogu.PoprawnaNazwaTypowana(t)) return false;
                w = t; return true;
            case KontraktLogu.Rodzaj.KodPowodu:
                var kp = o is Enum ek ? ek.ToString() : o as string;
                if (!KontraktLogu.PoprawnyKodPowodu(kp)) return false;
                w = kp; return true;
            default:
                return false;
        }
    }

    static KeyValuePair<string, long>[] Liczniki(LogEventPropertyValue pv, ref int odrzucone)
    {
        IEnumerable<KeyValuePair<string, LogEventPropertyValue>> wejscie = pv switch
        {
            DictionaryValue d => d.Elements.Select(x => new KeyValuePair<string, LogEventPropertyValue>(x.Key.Value as string, x.Value)),
            StructureValue s => s.Properties.Select(x => new KeyValuePair<string, LogEventPropertyValue>(x.Name, x.Value)),
            _ => null,
        };
        if (wejscie == null) { odrzucone++; return null; }
        var wynik = new List<KeyValuePair<string, long>>();
        foreach (var kv in wejscie)
        {
            if (wynik.Count < KontraktLogu.MaxLicznikow - 2 && KontraktLogu.PoprawnaNazwaLicznika(kv.Key)
                && kv.Value is ScalarValue { Value: var o } && Calkowita(o, out var v) && v >= 0)
                wynik.Add(new(kv.Key, v));
            else odrzucone++;
        }
        return wynik.ToArray();
    }

    static bool Dodatkowe(LogEventPropertyValue pv, out object w, ref int obcieteNa)
    {
        w = null;
        if (pv is not ScalarValue sv) return false;
        switch (sv.Value)
        {
            case null: return true;
            case string s:
                w = Redakcja.Tekst(s);
                if (s.Length > KontraktLogu.MaxZnakowTekstu) obcieteNa++;
                return true;
            case bool b: w = b; return true;
            case char c: w = Redakcja.Tekst(c.ToString()); return true;
            case double d when double.IsFinite(d): w = d; return true;
            case float f when float.IsFinite(f): w = (double)f; return true;
            case decimal m: w = (double)m; return true;
            case Guid g: w = g.ToString("D"); return true;
            case DateTime dt: w = dt.ToUniversalTime().ToString("O"); return true;
            case DateTimeOffset dto: w = dto.UtcDateTime.ToString("O"); return true;
            case Enum en: w = Redakcja.Tekst(en.ToString()); return true;
            default:
                if (Calkowita(sv.Value, out var l)) { w = l; return true; }
                return false;
        }
    }

    /// <summary>Zależności pól ze schematu: pole bez wymaganego kontekstu jest usuwane (np. channelId bez groupId/kzId).</summary>
    static int Zaleznosci(List<KeyValuePair<string, object>> pola)
    {
        int usuniete = 0;
        bool zmiana = true;
        while (zmiana)
        {
            zmiana = false;
            foreach (var (pole, wymaga) in KontraktLogu.Zaleznosci)
            {
                int i = pola.FindIndex(x => x.Key == pole);
                if (i < 0) continue;
                foreach (var w in wymaga)
                    if (!pola.Exists(x => x.Key == w)) { pola.RemoveAt(i); usuniete++; zmiana = true; break; }
            }
        }
        return usuniete;
    }

    // =========================================================================== worker (osobny wątek)

    void Petla()
    {
        Task<bool> oczekiwanie = null;
        try
        {
            try { Directory.CreateDirectory(katalog); }
            catch (Exception ex) { UstawNiegotowy("KATALOG_NIEDOSTEPNY_" + ex.GetType().Name.Replace("Exception", "").ToUpperInvariant()); }
            ZapiszWlasny(ZdarzeniaLogow.Uruchomiono, LogEventLevel.Information, "Uruchomiono zapis logów (profil {profile})", null,
                new("profile", opcje.Profil.ToString()), new("outcome", "Started"),
                new("counters", new KeyValuePair<string, long>[]
                {
                    new("maxRecords", limitRekordow), new("maxBytes", limitBajtow), new("maxFileBytes", opcje.BajtyPliku),
                    new("maxFiles", opcje.Pliki), new("maxAgeDays", (long)opcje.WiekPliku.TotalDays),
                    new("groupingMs", (long)opcje.OkresGrupowania.TotalMilliseconds), new("shutdownMs", (long)opcje.LimitZamkniecia.TotalMilliseconds),
                }));
            if (odlozonyRekordMiejsca is { } o) { odlozonyRekordMiejsca = null; ZapiszWlasny(o.Item1, o.Item2, o.Item3, null, o.Item4); }
            var czytnik = kanal.Reader;
            long ostatniNadzor = Proces.MonoUs();
            while (true)
            {
                while (!przerwij && czytnik.TryRead(out var r)) Zapisz(r);
                if (przerwij) { Porzuc(czytnik); break; }
                Podsumuj(false);
                if (Proces.MonoUs() - ostatniNadzor >= (long)(opcje.OkresNadzoruMiejsca.Ticks / 10)) { NadzorMiejsca(true); ostatniNadzor = Proces.MonoUs(); }
                if (czytnik.Completion.IsCompleted) break;
                oczekiwanie ??= czytnik.WaitToReadAsync().AsTask();
                if (oczekiwanie.Wait(opcje.OkresGrupowania)) oczekiwanie = null;
            }
            Podsumuj(true);
            ZapiszWlasny(ZdarzeniaLogow.Zatrzymano, LogEventLevel.Information, "Zatrzymano zapis logów", null,
                new("outcome", "Stopped"),
                new("counters", new KeyValuePair<string, long>[]
                {
                    new("accepted", Interlocked.Read(ref przyjete)), new("written", Interlocked.Read(ref przekazane)),
                    new("dropped", Interlocked.Read(ref stratyRazem)), new("sinkFailures", Interlocked.Read(ref bledyZapisu)),
                    new("maxQueueRecords", Volatile.Read(ref maxWKolejce)), new("maxQueueBytes", Interlocked.Read(ref maxBajtow)),
                    new("truncated", Interlocked.Read(ref obciete)), new("replaced", Interlocked.Read(ref zastapione)),
                }));
        }
        catch (Exception ex)
        {
            // Awaria workera: bez logowania do własnej kolejki (25 § 6) – stan zdrowia i liczniki zostają dostępne.
            UstawNiegotowy("AWARIA_WORKERA_" + ex.GetType().Name.Replace("Exception", "").ToUpperInvariant());
        }
        finally
        {
            try { prywatny.Dispose(); } catch { }
            if (stanZapisu != StanZapisu.Niegotowy) stanZapisu = StanZapisu.Zatrzymany;
        }
    }

    void Zapisz(RekordLogu r)
    {
        Interlocked.Decrement(ref wKolejce);
        Interlocked.Add(ref bajtyWKolejce, -r.Bajty);
        long b0 = Interlocked.Read(ref bledyZapisu);
        try { prywatny.Write(Zdarzenie(r)); } catch { AwariaZapisu(null); }
        if (Interlocked.Read(ref bledyZapisu) != b0) return;
        Interlocked.Increment(ref przekazane);
        if (stanZapisu != StanZapisu.Gotowy) Gotowy();
    }

    void Gotowy()
    {
        bool poAwarii = stanZapisu == StanZapisu.Niegotowy && poczatekAwariiUs > 0;
        stanZapisu = StanZapisu.Gotowy; powodNiegotowosci = null;
        if (!poAwarii) return;
        long ile = Interlocked.Read(ref bledyZapisu) - bledyNaPoczatkuAwarii;
        ZapiszWlasny(ZdarzeniaLogow.BladZapisu, LogEventLevel.Error, "Zapis logów był niedostępny: utracono {droppedCount} rekordów", null,
            new("droppedCount", ile), new("windowMs", (Proces.MonoUs() - poczatekAwariiUs) / 1000), new("reasonCode", "SINK_WRITE_FAILED"), new("outcome", "Degraded"));
        poczatekAwariiUs = 0;
    }

    void UstawNiegotowy(string powod)
    {
        if (stanZapisu != StanZapisu.Niegotowy) { poczatekAwariiUs = Proces.MonoUs(); bledyNaPoczatkuAwarii = Interlocked.Read(ref bledyZapisu); }
        powodNiegotowosci = KontraktLogu.PoprawnyKodPowodu(powod) ? powod : "NIEGOTOWY";
        stanZapisu = StanZapisu.Niegotowy;
    }

    internal void AwariaZapisu(LogEvent e)
    {
        if (stanZapisu != StanZapisu.Niegotowy) UstawNiegotowy("ZAPIS_NIEUDANY");
        Interlocked.Increment(ref bledyZapisu);
        Strata(ModulZdarzenia(e), "SINK_FAILED");
    }

    /// <summary>Przekroczony limit zamknięcia: pozostałe rekordy są liczone jako utracone, bez zapisu.</summary>
    void Porzuc(ChannelReader<RekordLogu> czytnik)
    {
        long n = 0;
        while (czytnik.TryRead(out var r))
        {
            Interlocked.Decrement(ref wKolejce); Interlocked.Add(ref bajtyWKolejce, -r.Bajty);
            Strata(r.Modul, "SHUTDOWN_TIMEOUT"); n++;
        }
        zamkniecieKompletne = false;
        ZapiszWlasny(ZdarzeniaLogow.ZamkniecieNiepelne, LogEventLevel.Warning, "Zamknięcie logów przerwane po limicie: pominięto {droppedCount} rekordów", null,
            new("droppedCount", n), new("reasonCode", "SHUTDOWN_TIMEOUT"), new("outcome", "Degraded"));
    }

    /// <summary>Zbiorcze LOG.DIAGNOSTICS_DROPPED według modułu i powodu – pisane bezpośrednio przez worker, nigdy przez kolejkę.</summary>
    void Podsumuj(bool koniec)
    {
        if (stanZapisu == StanZapisu.Niegotowy && !koniec) return;
        long teraz = Proces.MonoUs();
        bool cos = false;
        foreach (var kv in straty)
        {
            long v = Interlocked.Read(ref kv.Value.Value);
            zgloszone.TryGetValue(kv.Key, out var bylo);
            if (v <= bylo) continue;
            zgloszone[kv.Key] = v;
            cos = true;
            ZapiszWlasny(ZdarzeniaLogow.Pominieto, LogEventLevel.Warning, "Pominięto diagnostykę: {droppedCount}", null,
                new("targetId", kv.Key.Modul), new("droppedCount", v - bylo), new("windowMs", Math.Max(0, (teraz - ostatniePodsumowanieUs) / 1000)),
                new("reasonCode", kv.Key.Powod), new("outcome", "Degraded"));
        }
        if (cos || koniec) ostatniePodsumowanieUs = teraz;
    }

    /// <summary>B08: wolne miejsce na dysku z logami; poniżej progu ograniczenia przepuszczane są tylko Warning i poważniejsze.</summary>
    void NadzorMiejsca(bool zWorkera)
    {
        (long wolne, long calk) m;
        try
        {
            if (opcje.WolneMiejsce != null) m = opcje.WolneMiejsce();
            else
            {
                var root = Path.GetPathRoot(katalog);
                var d = new DriveInfo(string.IsNullOrEmpty(root) ? katalog : root);
                m = (d.AvailableFreeSpace, d.TotalSize);
            }
        }
        catch { return; }
        if (m.calk <= 0) return;
        double proc = 100.0 * m.wolne / m.calk;
        wolneProcent = proc;
        var nowy = proc < opcje.ProgOgraniczeniaProcent ? StanMiejsca.Ograniczenie : proc < opcje.ProgOstrzezeniaProcent ? StanMiejsca.Ostrzezenie : StanMiejsca.Normalny;
        var stary = stanMiejsca;
        if (nowy == stary) return;
        poziom.MinimumLevel = nowy == StanMiejsca.Ograniczenie && poziomBazowy < LogEventLevel.Warning ? LogEventLevel.Warning : poziomBazowy;
        stanMiejsca = nowy;   // stan po zmianie poziomu – kto widzi nowy stan, widzi już nowy próg
        if (stary == StanMiejsca.Nieznany && nowy == StanMiejsca.Normalny) return;
        var liczniki = new KeyValuePair<string, long>[] { new("freePercent", (long)Math.Floor(proc)), new("freeMiB", m.wolne >> 20) };
        var w = new KeyValuePair<string, object>[]
        {
            new("reasonCode", nowy == StanMiejsca.Ograniczenie ? "DISK_BELOW_LIMIT" : nowy == StanMiejsca.Ostrzezenie ? "DISK_BELOW_WARNING" : "DISK_OK"),
            new("outcome", nowy == StanMiejsca.Normalny ? "Accepted" : "Degraded"),
            new("counters", liczniki),
        };
        var def = nowy == StanMiejsca.Normalny ? ZdarzeniaLogow.MiejscePrzywrocone : ZdarzeniaLogow.MaloMiejsca;
        var poz = nowy == StanMiejsca.Normalny ? LogEventLevel.Information : LogEventLevel.Warning;
        var szablon = nowy == StanMiejsca.Normalny ? "Wolne miejsce na dysku logów przywrócone" : "Mało miejsca na dysku logów ({reasonCode})";
        if (zWorkera) ZapiszWlasny(def, poz, szablon, null, w);
        else odlozonyRekordMiejsca = (def, poz, szablon, w);
    }
    (DefinicjaZdarzenia, LogEventLevel, string, KeyValuePair<string, object>[])? odlozonyRekordMiejsca;

    void ZapiszWlasny(DefinicjaZdarzenia def, LogEventLevel l, string szablon, string wyjatek, params KeyValuePair<string, object>[] dodatkowe)
    {
        var pola = new List<KeyValuePair<string, object>>(12 + dodatkowe.Length);
        DodajWymagane(pola, def, l, ZdarzeniaLogow.ModulWorkera, wersjaBiblioteki);
        pola[8] = new("recordSeq", Proces.NastepnyNumer());
        pola.AddRange(dodatkowe);
        var r = new RekordLogu(DateTimeOffset.UtcNow, l, szablon, pola.ToArray(), wyjatek, default, default, ZdarzeniaLogow.ModulWorkera);
        long b0 = Interlocked.Read(ref bledyZapisu);
        try { prywatny.Write(Zdarzenie(r)); } catch { AwariaZapisu(null); }
        if (Interlocked.Read(ref bledyZapisu) != b0) return;
        Interlocked.Increment(ref wlasne);
        if (stanZapisu == StanZapisu.Uruchamianie) stanZapisu = StanZapisu.Gotowy;
    }

    LogEvent Zdarzenie(RekordLogu r)
    {
        var props = new List<LogEventProperty>(r.Pola.Length);
        foreach (var kv in r.Pola) props.Add(new LogEventProperty(kv.Key, Wartosc(kv.Value)));
        return new LogEvent(r.Czas, r.Poziom, r.Wyjatek == null ? null : new WyjatekZredagowany(r.Wyjatek), Szablon(r.Szablon), props, r.Trace, r.Span);
    }

    static LogEventPropertyValue Wartosc(object v) => v switch
    {
        KeyValuePair<string, long>[] a => new StructureValue(a.Select(x => new LogEventProperty(x.Key, new ScalarValue(x.Value)))),
        _ => new ScalarValue(v),
    };

    Serilog.Events.MessageTemplate Szablon(string tekst)
    {
        if (szablony.TryGetValue(tekst, out var t)) return t;
        t = parser.Parse(tekst);
        if (szablony.Count < 2048) szablony[tekst] = t;
        return t;
    }

    // =========================================================================== stan i zamknięcie

    public StanLogow Stan()
    {
        var wgPowodu = new Dictionary<string, long>(StringComparer.Ordinal);
        var wgModulu = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var kv in straty)
        {
            long v = Interlocked.Read(ref kv.Value.Value);
            wgPowodu[kv.Key.Powod] = (wgPowodu.TryGetValue(kv.Key.Powod, out var x) ? x : 0) + v;
            wgModulu[kv.Key.Modul + "|" + kv.Key.Powod] = v;
        }
        return new StanLogow
        {
            ProcessRunId = Proces.RunId, Aplikacja = opcje.Aplikacja, Profil = opcje.Profil, Katalog = katalog,
            Zapis = stanZapisu, PowodNiegotowosci = powodNiegotowosci, Miejsce = stanMiejsca, WolneProcent = wolneProcent,
            Przyjete = Interlocked.Read(ref przyjete), PrzekazaneDoPliku = Interlocked.Read(ref przekazane), Wlasne = Interlocked.Read(ref wlasne),
            BledyZapisu = Interlocked.Read(ref bledyZapisu), Obciete = Interlocked.Read(ref obciete), Zastapione = Interlocked.Read(ref zastapione),
            StratyRazem = Interlocked.Read(ref stratyRazem), StratyWgPowodu = wgPowodu, StratyWgModuluIPowodu = wgModulu,
            WKolejce = Volatile.Read(ref wKolejce), MaxWKolejce = Volatile.Read(ref maxWKolejce), LimitRekordow = limitRekordow,
            BajtyWKolejce = Interlocked.Read(ref bajtyWKolejce), MaxBajtowWKolejce = Interlocked.Read(ref maxBajtow), LimitBajtow = limitBajtow,
            CzasZamknieciaMs = czasZamknieciaMs, ZamkniecieKompletne = zamkniecieKompletne,
        };
    }

    /// <summary>
    /// Zamknięcie: nowe rekordy są liczone jako STOPPED, worker opróżnia kolejkę w limicie (domyślnie 5 s – B04), potem zamyka plik.
    /// Po przekroczeniu limitu pozostałe rekordy są liczone jako SHUTDOWN_TIMEOUT. Wywołanie wielokrotne jest bezpieczne.
    /// </summary>
    public StanLogow Zatrzymaj(TimeSpan? limit = null)
    {
        lock (zamek)
        {
            if (zatrzymany) return Stan();
            zatrzymany = true;
        }
        var t0 = Stopwatch.GetTimestamp();
        kanal.Writer.TryComplete();
        var lim = limit ?? opcje.LimitZamkniecia;
        var oproznianie = TimeSpan.FromTicks(lim.Ticks * 9 / 10);   // 90% limitu na zapis kolejki, reszta na policzenie porzuconych i zamknięcie pliku
        if (!watek.Join(oproznianie))
        {
            przerwij = true;
            if (!watek.Join(lim - oproznianie)) zamkniecieKompletne = false;
        }
        czasZamknieciaMs = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
        try { fabryka.Dispose(); goracy.Dispose(); } catch { }
        return Stan();
    }

    public void Dispose() => Zatrzymaj();
}

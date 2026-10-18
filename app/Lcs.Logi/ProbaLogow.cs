using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Lcs.Logi;

/// <summary>Wynik pomiaru kosztu emisji na wątku wywołującym (B05) – czasy w nanosekundach.</summary>
public sealed class PomiarEmisji
{
    public int Liczba { get; init; }
    public long P50Ns { get; init; }
    public long P99Ns { get; init; }
    public long MaxNs { get; init; }
    public double SredniaNs { get; init; }
    public long AlokacjeBajtowNaRekord { get; init; }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture,
        $"liczba={Liczba} p50_ns={P50Ns} p99_ns={P99Ns} max_ns={MaxNs} srednia_ns={SredniaNs:0} alokacje_B_na_rekord={AlokacjeBajtowNaRekord}");
}

/// <summary>
/// Próba kontrolna L01/L05 i pomiar B05 w działającym programie (opcja --logi-proba): kontrolowany błąd z testowymi sekretami
/// i CR/LF, pola niedozwolone, zbyt długi tekst. Znacznik <see cref="ZnacznikSekretu"/> nie może pojawić się w żadnym pliku logów.
/// </summary>
public static partial class ProbaLogow
{
    /// <summary>Znacznik testowego sekretu (nie jest sekretem) – wyszukiwany w plikach po próbie.</summary>
    public const string ZnacznikSekretu = "SEKRET_TESTOWY_S0101";

    public static void Wykonaj(ILogger log)
    {
        // 1. kontrolowany błąd: sekrety w treści wyjątku i wyjątku wewnętrznym, próba wstrzyknięcia linii JSONL
        var pem = "-----BEGIN " + "PRIVATE KEY-----\n" + ZnacznikSekretu + "_PEM\n-----END " + "PRIVATE KEY-----";
        var wew = new IOException("Nie można odczytać C:\\Users\\Ktos\\klucz.pem: " + pem);
        var ex = new InvalidOperationException(
            "Kontrolowany błąd próby L01/L05: password=" + ZnacznikSekretu + "_A; Authorization: Bearer " + ZnacznikSekretu + "_B\r\n" +
            "{\"@t\":\"2026-01-01T00:00:00Z\",\"@mt\":\"wstrzyknieta linia\"}\u2028 token: " + ZnacznikSekretu + "_C", wew);
        ZdarzeniaProcesu.Blad(log, ex, "TEST_PROBE");

        // 2. pola niedozwolone i błędne: nazwy sekretów, CR/LF w identyfikatorze klienta, zły kanał bez grupy
        var stan = new List<KeyValuePair<string, object>>
        {
            new("password", ZnacznikSekretu + "_D"),
            new("token", ZnacznikSekretu + "_E"),
            new("ApiKey", ZnacznikSekretu + "_F"),
            new("correlationId", "linia\r\n{\"wstrzykniete\":1}"),
            new("channelId", "A"),
            new("eventCode", "AUTH.FALSZYWY"),
            new("opis", "tekst z CR/LF\r\nnowa linia\u2028separator i znak\u0007sterujący"),
            new("{OriginalFormat}", "Próba pól niedozwolonych: {opis}"),
        };
        log.Log(LogLevel.Warning, new EventId(ZdarzeniaLogow.Proba.Id, ZdarzeniaLogow.Proba.Kod), stan, null, (s, e) => "proba");

        // 3. tekst ponad limit 512 znaków
        LogProba(log, new string('x', 3 * KontraktLogu.MaxZnakowTekstu), "PROBE", 0);
    }

    /// <summary>
    /// Pomiar kosztu emisji typowego zdarzenia na wątku wywołującym: paczki po <paramref name="naPaczke"/> rekordów co
    /// <paramref name="pauzaMs"/> ms (rytm ticku, worker zdąża zapisać). Wynik nie obejmuje I/O, bo emisja go nie wykonuje.
    /// </summary>
    public static PomiarEmisji Pomiar(ILogger log, int paczki = 100, int naPaczke = 100, int pauzaMs = 2)
    {
        var czasy = new long[paczki * naPaczke];
        int k = 0;
        long alok0 = GC.GetAllocatedBytesForCurrentThread();
        for (int p = 0; p < paczki; p++)
        {
            for (int i = 0; i < naPaczke; i++)
            {
                long t0 = Stopwatch.GetTimestamp();
                LogProba(log, "pomiar emisji", "PROBE", k);
                czasy[k++] = Stopwatch.GetTimestamp() - t0;
            }
            if (pauzaMs > 0) Thread.Sleep(pauzaMs);
        }
        long alok = GC.GetAllocatedBytesForCurrentThread() - alok0;
        return Statystyka(czasy, alok);
    }

    public static PomiarEmisji Statystyka(long[] tikiStopwatch, long alokacje)
    {
        var ns = tikiStopwatch.Select(t => (long)(t * 1e9 / Stopwatch.Frequency)).OrderBy(x => x).ToArray();
        if (ns.Length == 0) return new PomiarEmisji();
        return new PomiarEmisji
        {
            Liczba = ns.Length,
            P50Ns = ns[(int)(0.50 * (ns.Length - 1))],
            P99Ns = ns[(int)(0.99 * (ns.Length - 1))],
            MaxNs = ns[^1],
            SredniaNs = ns.Average(),
            AlokacjeBajtowNaRekord = alokacje / ns.Length,
        };
    }

    [LoggerMessage(EventId = 9003, EventName = "LOG.PROBE", Level = LogLevel.Information, Message = "Próba kontrolna logów: {opis}")]
    private static partial void LogProba(ILogger log, string opis, string targetId, long durationUs);
}

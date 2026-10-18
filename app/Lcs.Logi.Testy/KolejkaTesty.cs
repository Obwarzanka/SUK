using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Lcs.Logi.Testy;

/// <summary>L02 / N43: przepełnienie limitu liczby i bajtów, zalew wyjątków, brak rekurencji, zamknięcie w limicie (B02–B04).</summary>
public class KolejkaTesty
{
    [Theory]
    [InlineData(ProfilLogow.Klient)]
    [InlineData(ProfilLogow.Serwer)]
    public void PrzepelnienieLiczbyRekordowJestDokladnieLiczone(ProfilLogow profil)
    {
        var sink = new SinkTestowy(zablokowany: true);
        var o = Pom.Opcje(Pom.NowyKatalog("kolejka"), sink); o.Profil = profil;
        var s = SilnikLogow.Uruchom(o);
        var log = s.Modul("Test.Kolejka", "1.0.0");
        int limit = profil == ProfilLogow.Serwer ? 4096 : 1024, rezerwa = Math.Max(8, limit / 16);
        int n = 3 * limit;
        var czasy = new long[n];
        for (int i = 0; i < n; i++)
        {
            long t0 = Stopwatch.GetTimestamp();
            ZdarzeniaProcesu.Zatrzymano(log, i);
            czasy[i] = Stopwatch.GetTimestamp() - t0;
        }
        var st = s.Stan();
        Assert.Equal(limit - rezerwa, st.Przyjete);                      // zwykłe rekordy nie zajmują rezerwy poważnych błędów
        Assert.Equal(n - (limit - rezerwa), st.StratyWgPowodu["QUEUE_FULL"]);
        Assert.True(st.WKolejce <= limit && st.BajtyWKolejce <= st.LimitBajtow);
        Assert.Equal(StanZapisu.Uruchamianie, st.Zapis);                // worker czeka w sinku (symulowany zatrzymany dysk)

        for (int i = 0; i < 2 * rezerwa; i++) ZdarzeniaProcesu.Blad(log, new IOException("x"), "TEST_PROBE");
        st = s.Stan();
        Assert.Equal(limit, st.Przyjete);                                 // błędy weszły do rezerwy
        Assert.Equal(n - (limit - rezerwa) + rezerwa, st.StratyWgPowodu["QUEUE_FULL"]);

        var pomiar = ProbaLogow.Statystyka(czasy, 0);
        Pom.Pomiar("L02_przepelnienie_" + profil, $"profil={profil} limit={limit} rezerwa={rezerwa} emitowane={n} przyjete={limit - rezerwa} max_bajty_kolejki={st.MaxBajtowWKolejce} limit_bajtow={st.LimitBajtow} emisja_przy_zablokowanym_zapisie: {pomiar}");
        Assert.True(pomiar.P99Ns < 1_000_000, $"p99 emisji {pomiar.P99Ns} ns przy zablokowanym zapisie");

        sink.Brama.Set();
        var k = s.Zatrzymaj();
        Pom.Zachowaj(sink.Linie, "L02_" + profil);
        var r = sink.Rekordy();
        Assert.Equal(limit, r.Count(x => x.Tekst("moduleId") == "Test.Kolejka"));
        long zgloszone = r.Where(x => x.Tekst("eventCode") == "LOG.DIAGNOSTICS_DROPPED" && x.Tekst("targetId") == "Test.Kolejka" && x.Tekst("reasonCode") == "QUEUE_FULL")
            .Sum(x => x.Liczba("droppedCount").Value);
        Assert.Equal(k.StratyWgPowodu["QUEUE_FULL"], zgloszone);
        var stop = r.Last();
        Assert.Equal("LOG.STOPPED", stop.Tekst("eventCode"));
        Assert.Equal(k.StratyRazem, stop.Licznik("dropped"));
        Assert.Equal(limit, stop.Licznik("accepted"));
        Assert.True(k.ZamkniecieKompletne);
    }

    [Fact]
    public void LimitBajtowObowiazujePrzedLimitemLiczby()
    {
        var sink = new SinkTestowy(zablokowany: true);
        var o = Pom.Opcje(Pom.NowyKatalog("bajty"), sink); o.MaxBajtowKolejki = 64 * 1024;
        var s = SilnikLogow.Uruchom(o);
        var log = s.Modul("Test.Bajty", "1.0.0");
        for (int i = 0; i < 1000; i++) ProbaLogowPomoc.Duzy(log, new string('a', 500));
        var st = s.Stan();
        Assert.True(st.Przyjete > 0 && st.Przyjete < 1024 - 64, $"przyjęte {st.Przyjete}");
        Assert.Equal(1000 - st.Przyjete, st.StratyWgPowodu["QUEUE_BYTES"]);
        Assert.False(st.StratyWgPowodu.ContainsKey("QUEUE_FULL"));
        Assert.True(st.MaxBajtowWKolejce <= 64 * 1024 - 64 * 1024 / 16);
        Pom.Pomiar("L02_bajty", $"limit_bajtow=65536 przyjete={st.Przyjete} max_bajty={st.MaxBajtowWKolejce} szacunek_na_rekord={st.MaxBajtowWKolejce / Math.Max(1, st.Przyjete)}");
        sink.Brama.Set();
        s.Zatrzymaj();
    }

    [Fact]
    public void ZalewWyjatkowMaOgraniczonaPamiec()
    {
        var sink = new SinkTestowy(zablokowany: true);
        var s = SilnikLogow.Uruchom(Pom.Opcje(Pom.NowyKatalog("wyjatki"), sink));
        var log = s.Modul("Test.Wyjatki", "1.0.0");
        Exception wew;
        try { throw new InvalidOperationException("głęboki " + new string('e', 2000)); } catch (Exception e) { wew = e; }
        var ex = new AggregateException(wew, wew, wew);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long m0 = GC.GetTotalMemory(true);
        var czasy = new long[10_000];
        for (int i = 0; i < czasy.Length; i++)
        {
            long t0 = Stopwatch.GetTimestamp();
            ZdarzeniaProcesu.Blad(log, ex, "TEST_PROBE");
            czasy[i] = Stopwatch.GetTimestamp() - t0;
        }
        long m1 = GC.GetTotalMemory(true);
        var st = s.Stan();
        Assert.True(st.Przyjete > 0 && st.Przyjete <= 1024);
        Assert.True(st.BajtyWKolejce <= st.LimitBajtow);
        Assert.Equal(10_000 - st.Przyjete, st.StratyRazem);
        Assert.True(st.StratyWgPowodu["QUEUE_BYTES"] > 0);         // wyjątki po 4 KiB: limit bajtów przed limitem liczby
        long przyrost = m1 - m0;
        var p = ProbaLogow.Statystyka(czasy, 0);
        Pom.Pomiar("L02_wyjatki", $"rekordy=10000 przyjete={st.Przyjete} bajty_szacowane={st.BajtyWKolejce} limit_bajtow={st.LimitBajtow} przyrost_pamieci_GC={przyrost} stosunek={(double)przyrost / Math.Max(1, st.BajtyWKolejce):0.00} emisja_wyjatku: {p}");
        Assert.True(przyrost < 4 * st.LimitBajtow, $"przyrost pamięci {przyrost} B");
        sink.Brama.Set();
        s.Zatrzymaj();
    }

    [Fact]
    public void AwariaSinkuNieWracaDoKolejki()
    {
        var sink = new SinkTestowy { Rzucaj = true };
        var s = SilnikLogow.Uruchom(Pom.Opcje(Pom.NowyKatalog("awaria"), sink));
        var log = s.Modul("Test.Awaria", "1.0.0");
        for (int i = 0; i < 50; i++) ZdarzeniaProcesu.Zatrzymano(log, i);
        Thread.Sleep(300);
        var st = s.Stan();
        Assert.Equal(50, st.Przyjete);                       // nic nie wróciło do kolejki wejściowej
        Assert.Equal(0, st.WKolejce);
        Assert.Equal(StanZapisu.Niegotowy, st.Zapis);
        Assert.Equal(50, st.StratyWgModuluIPowodu["Test.Awaria|SINK_FAILED"]);
        var k = s.Zatrzymaj();
        Assert.True(k.BledyZapisu >= 51);                      // + rekordy własne workera
        Assert.Equal(StanZapisu.Niegotowy, k.Zapis);
        Assert.Equal(0, k.PrzekazaneDoPliku);
    }

    [Fact]
    public void ZamkniecieMaLimitCzasu()
    {
        var sink = new SinkTestowy(zablokowany: true);
        var s = SilnikLogow.Uruchom(Pom.Opcje(Pom.NowyKatalog("zamkniecie"), sink));
        var log = s.Modul("Test.Zamkniecie", "1.0.0");
        for (int i = 0; i < 500; i++) ZdarzeniaProcesu.Zatrzymano(log, i);
        var t = Stopwatch.StartNew();
        var st = s.Zatrzymaj(TimeSpan.FromMilliseconds(300));
        t.Stop();
        Assert.True(t.ElapsedMilliseconds < 300 + 150, $"zamknięcie {t.ElapsedMilliseconds} ms przy limicie 300 ms");
        Assert.False(st.ZamkniecieKompletne);
        sink.Brama.Set();                                    // worker wznawia się i liczy porzucone rekordy
        SpinWait.SpinUntil(() => s.Stan().StratyWgPowodu.ContainsKey("SHUTDOWN_TIMEOUT"), 3000);
        var k = s.Stan();
        Assert.Equal(500, k.StratyWgPowodu["SHUTDOWN_TIMEOUT"]);
        Pom.Pomiar("B04_zamkniecie", $"limit_ms=300 czas_ms={t.ElapsedMilliseconds} porzucone={k.StratyWgPowodu["SHUTDOWN_TIMEOUT"]}");

        log.LogInformation("po zamknięciu");                  // bez wyjątku, liczone jako STOPPED
        Assert.Equal(1, s.Stan().StratyWgPowodu["STOPPED"]);
    }

    [Fact]
    public void ZwykleZamkniecieOprozniaKolejke()
    {
        var kat = Pom.NowyKatalog("oproznienie");
        var o = Pom.Opcje(kat); o.Profil = ProfilLogow.Serwer;
        var s = SilnikLogow.Uruchom(o);
        var log = s.Modul("Test.Oproznienie", "1.0.0");
        for (int i = 0; i < 3000; i++) ZdarzeniaProcesu.Zatrzymano(log, i);
        var t = Stopwatch.StartNew();
        var st = s.Zatrzymaj();
        Assert.True(st.ZamkniecieKompletne);
        Assert.True(t.ElapsedMilliseconds < 5000);
        Assert.Equal(st.Przyjete, st.PrzekazaneDoPliku);
        Assert.Equal(st.Przyjete, Pom.Rekordy(kat).Count(x => x.Tekst("moduleId") == "Test.Oproznienie"));
        Pom.Pomiar("B04_oproznienie", $"rekordy={st.Przyjete} straty={st.StratyRazem} czas_zamkniecia_ms={st.CzasZamknieciaMs:0.0}");
    }
}

internal static partial class ProbaLogowPomoc
{
    static ProbaLogowPomoc() => KatalogZdarzen.Zarejestruj(new DefinicjaZdarzenia(2191, "AUTH.TEST_TEKST", KontraktLogu.Strumien.Diagnostyka, "opis"));

    public static void Duzy(ILogger log, string opis) => LogDuzy(log, opis);

    [LoggerMessage(EventId = 2191, EventName = "AUTH.TEST_TEKST", Level = LogLevel.Information, Message = "Tekst testowy {opis}")]
    private static partial void LogDuzy(ILogger log, string opis);
}

using Microsoft.Extensions.Logging;
using Xunit;

namespace Lcs.Logi.Testy;

/// <summary>N43 / L01: poprawny rekord z polami kontraktu, błędne i zastrzeżone pola, kody spoza katalogu, moduły, limit 8 KiB.</summary>
public class RekordTesty
{
    [Fact]
    public void PoprawnyRekordMaPolaKontraktu()
    {
        var kat = Pom.NowyKatalog("rekord");
        var s = SilnikLogow.Uruchom(Pom.Opcje(kat));
        var log = s.Modul("Test.Modul", "1.2.3");
        var run = Guid.NewGuid();
        ZdarzeniaProcesu.Uruchomiono(log, "test");
        using (log.BeginScope(new Dictionary<string, object>
        {
            ["correlationId"] = "corr-1", ["requestId"] = "req-1", ["worldId"] = "swiat-1", ["worldRunId"] = run, ["serverTick"] = 5L,
            ["simulationTimeMs"] = 5000, ["kzId"] = "kz-1", ["groupId"] = 2, ["channelId"] = "B", ["postId"] = "LW",
        }))
            ZdarzeniaProcesu.Blad(log, new InvalidOperationException("kontrolowany błąd"), "TEST_PROBE");
        var st = s.Zatrzymaj();
        Pom.Zachowaj(kat, "rekord");

        Assert.Equal(StanZapisu.Zatrzymany, st.Zapis);
        Assert.Equal(2, st.Przyjete);
        Assert.Equal(0, st.StratyRazem);
        var r = Pom.Rekordy(kat);
        Assert.Equal("LOG.STARTED", r.First().Tekst("eventCode"));
        Assert.Equal("LOG.STOPPED", r.Last().Tekst("eventCode"));
        Assert.Equal(2, r.Last().Licznik("accepted"));
        Assert.Equal(2, r.Last().Licznik("written"));

        var start = r.Single(x => x.Tekst("eventCode") == "PROC.STARTED");
        var blad = r.Single(x => x.Tekst("eventCode") == "PROC.UNHANDLED_EXCEPTION");
        foreach (var x in r)
        {
            foreach (var p in new[] { "@t", "@mt", "schemaVersion", "stream", "eventCode", "eventId", "severity", "moduleId", "moduleVersion", "processRunId", "recordSeq", "monoElapsedUs" })
                Assert.True(x.Ma(p), $"brak {p}");
            Assert.EndsWith("Z", x.Tekst("@t"));
            Assert.Equal(1, x.Liczba("schemaVersion"));
            Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", x.Tekst("processRunId"));
            Assert.False(x.Ma("EventId") || x.Ma("SourceContext") || x.Ma("Scope"), "pola mostu MEL w rekordzie");
            Assert.True(x.ToString().Length < KontraktLogu.MaxBajtowRekordu);
        }
        Assert.Equal("Test.Modul", start.Tekst("moduleId"));
        Assert.Equal("1.2.3", start.Tekst("moduleVersion"));
        Assert.Equal(1001, start.Liczba("eventId"));
        Assert.Equal("Information", start.Tekst("severity"));
        Assert.False(start.Ma("@l"));
        Assert.Equal("test", start.Tekst("tryb"));
        Assert.Equal("Started", start.Tekst("outcome"));
        Assert.Equal("diagnostic", start.Tekst("stream"));

        Assert.Equal("Error", blad.Tekst("@l"));
        Assert.Equal("Error", blad.Tekst("severity"));
        Assert.Equal("TEST_PROBE", blad.Tekst("reasonCode"));
        Assert.Contains("kontrolowany błąd", blad.Tekst("@x"));
        Assert.Equal("corr-1", blad.Tekst("correlationId"));
        Assert.Equal(run.ToString("D"), blad.Tekst("worldRunId"));
        Assert.Equal(5, blad.Liczba("serverTick"));
        Assert.Equal(2, blad.Liczba("groupId"));
        Assert.Equal("B", blad.Tekst("channelId"));
        Assert.Equal("LW", blad.Tekst("postId"));
        Assert.True(blad.Liczba("recordSeq") > start.Liczba("recordSeq"));
        Assert.True(blad.Liczba("monoElapsedUs") >= start.Liczba("monoElapsedUs"));
        Assert.Equal(start.Tekst("processRunId"), blad.Tekst("processRunId"));
    }

    [Fact]
    public void BledneIZastrzezonePolaSaOdrzucane()
    {
        var sink = new SinkTestowy();
        var s = SilnikLogow.Uruchom(Pom.Opcje(Pom.NowyKatalog("bledne"), sink));
        var log = s.Modul("Test.Modul", "1.0.0");
        using (log.BeginScope(new Dictionary<string, object>
        {
            ["kzId"] = "kz-1", ["groupId"] = 3, ["channelId"] = "C", ["correlationId"] = "a b", ["reasonCode"] = "zly", ["kzEpoch"] = -1,
            ["schemaVersion"] = 7, ["stream"] = "audit", ["moduleId"] = "Obcy", ["dowolne"] = "x", ["worldRunId"] = "nie-uuid",
        }))
            ZdarzeniaProcesu.Zatrzymano(log, 10);
        s.Zatrzymaj();
        Pom.Zachowaj(sink.Linie, "bledne");
        var r = sink.Rekordy().Single(x => x.Tekst("eventCode") == "PROC.STOPPED");
        Assert.Equal("diagnostic", r.Tekst("stream"));
        Assert.Equal("Test.Modul", r.Tekst("moduleId"));
        Assert.Equal(1, r.Liczba("schemaVersion"));
        Assert.Equal("kz-1", r.Tekst("kzId"));
        foreach (var p in new[] { "groupId", "channelId", "correlationId", "reasonCode", "kzEpoch", "dowolne", "worldRunId" }) Assert.False(r.Ma(p), p);
        Assert.Equal(10, r.Licznik("rejectedFields"));
    }

    [Fact]
    public void ZaleznosciPolZeSchematu()
    {
        var sink = new SinkTestowy();
        var s = SilnikLogow.Uruchom(Pom.Opcje(Pom.NowyKatalog("zaleznosci"), sink));
        var log = s.Modul("Test.Modul", "1.0.0");
        using (log.BeginScope(new Dictionary<string, object> { ["groupId"] = 1, ["channelId"] = "A", ["serverTick"] = 3L, ["effectState"] = "Committed" }))
            ZdarzeniaProcesu.Zatrzymano(log, 1);
        s.Zatrzymaj();
        var r = sink.Rekordy().Single(x => x.Tekst("eventCode") == "PROC.STOPPED");
        foreach (var p in new[] { "groupId", "channelId", "serverTick", "effectState" }) Assert.False(r.Ma(p), p);
        Assert.Equal(4, r.Licznik("rejectedFields"));
    }

    [Fact]
    public void ZdarzeniaSpozaKatalogu()
    {
        var sink = new SinkTestowy();
        var s = SilnikLogow.Uruchom(Pom.Opcje(Pom.NowyKatalog("spoza"), sink));
        var log = s.Modul("Test.Modul", "1.0.0");
#pragma warning disable CA2254, CA1848
        log.LogWarning("Bez kodu {x}", 5);
        log.Log(LogLevel.Warning, new EventId(1500, "REZERWA.X"), "Rezerwa");
        log.Log(LogLevel.Error, new EventId(9101, "LOG.INNA_NAZWA"), "Zła nazwa");
        log.Log(LogLevel.Information, new EventId(2150, "AUTH.TEST_OK"), "Kod w podzakresie bez definicji");
#pragma warning restore CA2254, CA1848
        s.Zatrzymaj();
        Pom.Zachowaj(sink.Linie, "spoza");
        var r = sink.Rekordy().Where(x => x.Tekst("moduleId") == "Test.Modul").ToList();
        Assert.Equal(4, r.Count);
        Assert.Equal((9002L, 0L, "Warning"), (r[0].Liczba("eventId").Value, r[0].Liczba("originalEventId").Value, r[0].Tekst("severity")));
        Assert.Equal(1, r[0].Licznik("rejectedFields"));
        Assert.Equal((9002L, 1500L), (r[1].Liczba("eventId").Value, r[1].Liczba("originalEventId").Value));
        Assert.Equal((9002L, 9101L, "Error"), (r[2].Liczba("eventId").Value, r[2].Liczba("originalEventId").Value, r[2].Tekst("@l")));
        Assert.Equal("LOG.EVENT_UNCATALOGUED", r[2].Tekst("eventCode"));
        Assert.Equal((2150L, "AUTH.TEST_OK"), (r[3].Liczba("eventId").Value, r[3].Tekst("eventCode")));
    }

    [Fact]
    public void ModulyNiezarejestrowane()
    {
        var sink = new SinkTestowy();
        var s = SilnikLogow.Uruchom(Pom.Opcje(Pom.NowyKatalog("moduly"), sink));
#pragma warning disable CA1848
        s.Fabryka.CreateLogger("Microsoft.Hosting.Lifetime").LogInformation(new EventId(1003, "PROC.HOST"), "host");
        s.Fabryka.CreateLogger("zła kategoria!").LogInformation(new EventId(1003, "PROC.HOST"), "inna");
        Assert.Same(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, s.Modul("zly modul", "1"));
#pragma warning restore CA1848
        s.Zatrzymaj();
        var r = sink.Rekordy().Where(x => x.Tekst("eventCode") == "PROC.HOST").ToList();
        Assert.Equal(("Microsoft.Hosting.Lifetime", "niezarejestrowany"), (r[0].Tekst("moduleId"), r[0].Tekst("moduleVersion")));
        Assert.Equal("External", r[1].Tekst("moduleId"));
    }

    [Fact]
    public void FasadaAplikacjiNieZatrzymujeProgramu()
    {
        Assert.Same(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, Diagnostyka.Modul("Test.Fasada", "1.0.0"));
        Assert.False(Diagnostyka.Start(new OpcjeLogow { Aplikacja = "zła/nazwa", Katalog = Pom.NowyKatalog("fasada") }));
        Assert.False(Diagnostyka.Start(null));
        Assert.Null(Diagnostyka.Silnik);
        var kat = Pom.NowyKatalog("fasada");
        Assert.True(Diagnostyka.Start(Pom.Opcje(kat)));
        var log = Diagnostyka.Modul("Test.Fasada", "1.0.0");
        ZdarzeniaProcesu.Uruchomiono(log, "test");
        var st = Diagnostyka.Zatrzymaj();
        Assert.Equal(1, st.Przyjete);
        Assert.Equal(1, st.PrzekazaneDoPliku);
        Assert.Null(Diagnostyka.Zatrzymaj());
        ZdarzeniaProcesu.Zatrzymano(log, 1);                         // logger po zatrzymaniu – bez wyjątku
        Assert.Single(Pom.Rekordy(kat), x => x.Tekst("moduleId") == "Test.Fasada");
    }

    [Fact]
    public void HostMelZJednymDostawca()
    {
        var sink = new SinkTestowy();
        var s = SilnikLogow.Uruchom(Pom.Opcje(Pom.NowyKatalog("host"), sink));
        s.ZarejestrujKategorie("Lcs.Logi.Testy.Host", "Test.Host", "2.0.0");
        using (var f = LoggerFactory.Create(b => b.DodajSukLogi(s)))
            ZdarzeniaProcesu.Uruchomiono(f.CreateLogger("Lcs.Logi.Testy.Host"), "host");
        s.Zatrzymaj();
        var r = sink.Rekordy().Single(x => x.Tekst("eventCode") == "PROC.STARTED");
        Assert.Equal(("Test.Host", "2.0.0"), (r.Tekst("moduleId"), r.Tekst("moduleVersion")));
    }

    [Fact]
    public void RekordPonadLimitJestSkracanyAlboZastepowany()
    {
        Assert.True(KatalogZdarzen.Zarejestruj(new DefinicjaZdarzenia(2190, "AUTH.TEST_DUZY", KontraktLogu.Strumien.Diagnostyka,
            Enumerable.Range(1, 40).Select(i => "f" + i).ToArray())));
        var kat = Pom.NowyKatalog("duzy");
        var s = SilnikLogow.Uruchom(Pom.Opcje(kat));
        var log = s.Modul("Test.Duzy", "1.0.0");
        List<KeyValuePair<string, object>> Stan(int n) => Enumerable.Range(1, n).Select(i => new KeyValuePair<string, object>("f" + i, new string('ł', 500)))
            .Append(new("{OriginalFormat}", "Duży rekord")).ToList();
        log.Log(LogLevel.Information, new EventId(2190, "AUTH.TEST_DUZY"), Stan(12), null, (a, b) => "");   // 12 KB → skrócenie
        log.Log(LogLevel.Information, new EventId(2190, "AUTH.TEST_DUZY"), Stan(40), null, (a, b) => "");   // 40 KB → zastąpienie
        var st = s.Zatrzymaj();
        Pom.Zachowaj(kat, "duzy");
        var linie = Pom.Linie(kat);
        Assert.All(linie, l => Assert.True(System.Text.Encoding.UTF8.GetByteCount(l) <= KontraktLogu.MaxBajtowRekordu, $"{System.Text.Encoding.UTF8.GetByteCount(l)} B"));
        var r = linie.Select(Pom.Parsuj).Where(x => x.Tekst("moduleId") == "Test.Duzy").ToList();
        Assert.Equal(2, r.Count);
        Assert.Equal("AUTH.TEST_DUZY", r[0].Tekst("eventCode"));
        Assert.True(r[0].GetProperty("truncated").GetBoolean());
        Assert.All(Enumerable.Range(1, 12), i => Assert.True(r[0].Tekst("f" + i).Length <= 128));
        Assert.Equal("LOG.RECORD_TOO_LARGE", r[1].Tekst("eventCode"));
        Assert.Equal(2190, r[1].Liczba("originalEventId"));
        Assert.Equal(1, st.Obciete);
        Assert.Equal(1, st.Zastapione);
    }
}

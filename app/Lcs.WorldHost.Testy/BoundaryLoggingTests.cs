using System.Text.Json;
using Lcs.Logi;
using LcsWidzew.Model;
using LcsWidzew.Sim;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Lcs.WorldHost.Testy;

/// <summary>S02A.04/F24: rzeczywisty JSONL granic, limity, korelacja i niezależność działania od diagnostyki.</summary>
public sealed class BoundaryLoggingTests
{
    static Simulator New(ILogger? logger = null) => new(
        Station.Load(Path.Combine(AppContext.BaseDirectory, "TestData", "station_test.json")),
        options: new SimulatorOptions { GraniceLogger = logger! });

    static void Register()
    {
        foreach (var (id, code, fields) in GraniceLog.Definicje)
            Assert.True(KatalogZdarzen.Zarejestruj(new DefinicjaZdarzenia(id, code, KontraktLogu.Strumien.Diagnostyka, fields)),
                $"Zdarzenie {id}/{code} musi należeć do zadeklarowanego podzakresu katalogu.");
    }

    static OpcjeLogow Options(string directory) => new()
    {
        Aplikacja = "Lcs.WorldHost", Katalog = directory, Profil = ProfilLogow.Serwer,
    };

    static string Text(JsonElement record, string key) => record.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
        ? value.GetString() ?? "" : "";

    static IDisposable? WorldScope(ILogger logger, string world, string run) => logger.BeginScope(new Dictionary<string, object>
    {
        ["worldId"] = world, ["worldRunId"] = run,
    });

    static Desk DeskFor(Simulator s, string id, string name)
    {
        var sig = s.L.Signals.Values.First(x => x.IsTrain && x.Type != "odstepowy");
        var desk = s.AddDesk(id, name, false);
        desk.Page = s.L.Pages.First(p => p.Posts.Contains(sig.Post)).Id;
        desk.User = new User { Name = name, Role = "dyżurny ruchu" };
        s.Control[sig.Post] = desk.Id;
        return desk;
    }

    [Fact]
    public void RealJsonlHasVersionedCorrelatedBoundaryRecordsAndExactSuppressionCountersWithoutInputContents()
    {
        Register();
        string directory = TestArtifacts.Directory("S02A04-F24-granice");
        const string world = "swiat-test-granic";
        string run = Guid.NewGuid().ToString("D");
        const string secret = "S02A04_SEKRET_OPERATORA_NIE_LOGUJ";
        const string deskSecret = "S02A04_SEKRET_STANOWISKA_NIE_LOGUJ";
        const string commandSecret = "S02A04_SEKRET_POLECENIA_NIE_LOGUJ";
        string injection = "password=" + secret + "\r\n{\"wstrzykniete\":1}";
        long finalPlan = 0;
        int signals = 0, locks = 0, limits = 0;
        using (var engine = SilnikLogow.Uruchom(Options(directory)))
        {
            var logger = engine.Modul(GraniceLog.Modul, GraniceLog.Wersja);
            using (WorldScope(logger, world, run))
            {
                var s = New(logger);
                s.User = new User { Name = "HOST_" + secret, Role = "instruktor" };
                var desk = DeskFor(s, deskSecret, secret + injection);
                desk.User.Role = "ROLA_" + secret;
                s.Tick(0.1);
                for (int n = 0; n < 40; n++)
                {
                    Assert.False(s.ExecAs(desk, "NIEZNANY_KOD_" + commandSecret + " " + injection).Ok);
                    var plan = s.Kz.Evaluate(KrokKz.WszystkieStoj);
                    Assert.Equal("Executed", s.Wyjscia.Zatwierdz(plan).Stan);
                    var projection = s.ProjekcjaRuchu;
                    Assert.Same(projection, s.ProjekcjaRuchu);
                    finalPlan = projection.NumerPlanu;
                    signals = projection.Sygnaly.Count;
                    locks = projection.Utwierdzenia.Count;
                    limits = projection.LimityPrzebiegow.Count;
                }
                s.PodsumujGranice();
                s.PodsumujGranice();
            }
            var state = engine.Zatrzymaj();
            File.WriteAllText(Path.Combine(directory, "stan.txt"), state.ToString());
            Assert.True(state.ZamkniecieKompletne);
            Assert.Equal(0, state.StratyRazem);
            Assert.Equal(0, state.BledyZapisu);
        }

        var all = TestArtifacts.Records(directory);
        var boundary = all.Where(r => Text(r, "moduleId") == GraniceLog.Modul).ToArray();
        Assert.NotEmpty(boundary);
        var definitions = GraniceLog.Definicje.ToDictionary(x => x.Id, x => x.Kod);
        Assert.All(boundary, r =>
        {
            Assert.Equal(1, r.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(KontraktLogu.Strumien.Diagnostyka, Text(r, "stream"));
            Assert.Equal(GraniceLog.Wersja, Text(r, "moduleVersion"));
            int eventId = r.GetProperty("eventId").GetInt32();
            Assert.Equal(definitions[eventId], Text(r, "eventCode"));
            Assert.Equal(world, Text(r, "worldId"));
            Assert.Equal(run, Text(r, "worldRunId"));
            Assert.True(Guid.TryParse(Text(r, "processRunId"), out _));
            Assert.Equal(1, r.GetProperty("serverTick").GetInt64());
            Assert.Equal(100, r.GetProperty("simulationTimeMs").GetInt64());
            Assert.True(r.GetProperty("recordSeq").GetInt64() > 0);
            Assert.True(r.GetProperty("monoElapsedUs").GetInt64() >= 0);
        });
        Assert.Single(boundary, r => r.GetProperty("eventId").GetInt32() == 4130);
        var contexts = boundary.Where(r => r.GetProperty("eventId").GetInt32() == 4131).ToArray();
        var projections = boundary.Where(r => r.GetProperty("eventId").GetInt32() == 4132).ToArray();
        Assert.Equal(16, contexts.Length);
        Assert.Equal(16, projections.Length);
        Assert.Equal(Enumerable.Range(1, 16).Select(n => "BOUNDARY-" + n), contexts.Select(r => Text(r, "correlationId")));
        Assert.All(projections, r => Assert.Equal("BOUNDARY-" + r.GetProperty("counters").GetProperty("committedPlan").GetInt64(),
            Text(r, "correlationId")));
        var final = Assert.Single(boundary, r => r.GetProperty("eventId").GetInt32() == 4133
            && r.GetProperty("counters").GetProperty("commands").GetInt64() == 40);
        var counters = final.GetProperty("counters");
        Assert.Equal(40, counters.GetProperty("commands").GetInt64());
        Assert.Equal(40, counters.GetProperty("projections").GetInt64());
        Assert.Equal(48, counters.GetProperty("detailsSuppressed").GetInt64());
        Assert.Equal(finalPlan, counters.GetProperty("committedPlan").GetInt64());
        Assert.Equal(signals, counters.GetProperty("signals").GetInt64());
        Assert.Equal(locks, counters.GetProperty("lockedSections").GetInt64());
        Assert.Equal(limits, counters.GetProperty("speedLimits").GetInt64());
        string raw = string.Join('\n', all.Select(r => r.GetRawText()));
        foreach (string forbidden in new[] { secret, deskSecret, commandSecret, "password=", "NIEZNANY_KOD_" })
            Assert.DoesNotContain(forbidden, raw, StringComparison.Ordinal);
        Assert.DoesNotContain(all, r => r.TryGetProperty("wstrzykniete", out _) || r.TryGetProperty("password", out _)
            || r.TryGetProperty("operator", out _) || r.TryGetProperty("deskName", out _) || r.TryGetProperty("commandText", out _));
    }

    [Fact]
    public void BoundarySummaryFloodRespectsSmallQueueAndAccountsForDropsWhileSimulationProgresses()
    {
        Register();
        string directory = TestArtifacts.Directory("S02A04-F24-zalew-granic");
        var options = Options(directory);
        options.MaxRekordowKolejki = 16;
        options.MaxBajtowKolejki = 16 * 1024;
        options.MaxBajtowPliku = 16 * 1024;
        options.MaxPlikow = 3;
        using var engine = SilnikLogow.Uruchom(options);
        var logger = engine.Modul(GraniceLog.Modul, GraniceLog.Wersja);
        using (WorldScope(logger, "swiat-zalew-granic", Guid.NewGuid().ToString("D")))
        {
            var s = New(logger);
            // Wywołujemy ten sam licznik co parser, bez mnożenia domenowych wpisów nieznanych poleceń.
            // Każda zmiana wymusza podsumowanie; to celowy zalew ujścia, nie wyłączenie limitu 16 szczegółów.
            for (int n = 0; n < 20000; n++)
            {
                s.Granice.Polecenie();
                s.PodsumujGranice();
                if (n % 100 == 99) s.Tick(0.1);
            }
            Assert.Equal(200, s.NrCyklu);
            Assert.Equal(20.0, s.T, 8);
            Assert.False(s.Wyjscia.Zatrzymana);
        }
        var state = engine.Zatrzymaj();
        File.WriteAllText(Path.Combine(directory, "stan.txt"), state.ToString());
        Assert.True(state.MaxWKolejce <= state.LimitRekordow);
        Assert.True(state.MaxBajtowWKolejce <= state.LimitBajtow);
        Assert.True(state.StratyRazem > 0, "Zalew ma rzeczywiście wyczerpać kolejkę diagnostyki.");
        Assert.True(state.StratyWgModuluIPowodu.Where(p => p.Key.StartsWith(GraniceLog.Modul + "|", StringComparison.Ordinal))
            .Sum(p => p.Value) > 0);
        Assert.True(state.ZamkniecieKompletne);
        Assert.Equal(0, state.BledyZapisu);
        var files = System.IO.Directory.GetFiles(directory, "*.jsonl");
        Assert.InRange(files.Length, 1, 3);
        Assert.All(files, path => Assert.True(new FileInfo(path).Length <= 16 * 1024 + KontraktLogu.MaxBajtowRekordu));
        Assert.Contains(TestArtifacts.Records(directory), r => Text(r, "eventCode") == "LOG.DIAGNOSTICS_DROPPED");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThrowingBoundaryLoggerDoesNotChangeCommandResultCommittedStateOrTrafficProjection(bool throwsFromIsEnabled)
    {
        var throwing = new ThrowingLogger(throwsFromIsEnabled);
        string Run(ILogger? logger)
        {
            var s = New(logger);
            var sig = s.L.Signals.Values.First(x => x.IsTrain && x.Type != "odstepowy");
            var desk = DeskFor(s, "A", "Operator próby błędu logowania");
            var result = s.ExecAs(desk, sig.Name + " STOP");
            Assert.True(result.Ok);
            var projection = s.ProjekcjaRuchu;
            Assert.True(s.Sigs[sig.Name].Closed);
            Assert.Equal(s.Wyjscia.NastepnyNumer - 1, projection.NumerPlanu);
            s.PodsumujGranice();
            Assert.False(s.Wyjscia.Zatrzymana);
            return JsonSerializer.Serialize(new
            {
                result,
                projection.NumerPlanu,
                Signals = projection.Sygnaly.OrderBy(p => p.Key).Select(p => new { p.Key, p.Value.Aspekt, p.Value.BrakDanych }),
                Locks = projection.Utwierdzenia.OrderBy(p => p.Key),
                Limits = projection.LimityPrzebiegow.OrderBy(p => p.Key),
                Events = s.Events.Select(e => new { e.Kategoria, e.Zdarzenie, e.Operator, e.Obiekt }),
            });
        }

        Assert.Equal(Run(null), Run(throwing));
        Assert.True(throwing.Attempts > 0);
    }

    sealed class ThrowingLogger(bool fromIsEnabled) : ILogger
    {
        public int Attempts;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level)
        {
            if (!fromIsEnabled) return true;
            Attempts++;
            throw new IOException("Błąd kontrolny diagnostyki IsEnabled");
        }
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter)
        {
            Attempts++;
            throw new IOException("Błąd kontrolny diagnostyki Log");
        }
    }
}

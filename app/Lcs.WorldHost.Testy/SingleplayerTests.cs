using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Lcs.Logi;
using LcsWidzew.Model;
using LcsWidzew.Sim;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Lcs.WorldHost.Testy;

public sealed class SingleplayerTests
{
    static Station Load() => Station.Load(Path.Combine(AppContext.BaseDirectory, "TestData", "station_test.json"));
    static User Operator() => new() { Name = "Operator lokalny", Role = "dyżurny ruchu" };

    [Fact]
    public void CommandsAtZeroAndDuringPausePreserveTimeAndActuallyMoveTurnout()
    {
        var clock = new ManualClock(); using var host = new SingleplayerHost(Load(), "podstawowy", Operator(), clock: clock);
        var session = host.Session; var sim = session.Sim;
        string point = sim.L.Switches.Keys.First(); session.SetPage(sim.L.Pages.First(p => p.Posts.Contains(sim.L.Switches[point].Post)).Id);
        Assert.Equal(0, sim.T); Assert.False(host.Status.Ready);
        var command = session.Exec(point + " Minus"); Assert.True(command.Ok, command.Msg);
        Assert.Equal(0, sim.T); Assert.False(host.Status.Ready);
        clock.Advance(TimeSpan.FromSeconds(10)); host.Frame(true, 10);
        Assert.Equal(0, sim.T); Assert.Equal(0, host.Status.ServerTick);
        host.AdvanceTo(6);
        Assert.True(host.Status.Ready); Assert.Equal("-", sim.Sws[point].Pos);
    }

    [Theory]
    [InlineData(1, 2)] [InlineData(2, 5)] [InlineData(5, 12)] [InlineData(10, 24)]
    public void TempoUsesSameFixedStepsAndLegacyFloatingAccumulator(double speed, int minimum)
    {
        var clock = new ManualClock(); using var host = new SingleplayerHost(Load(), "podstawowy", clock: clock);
        clock.Advance(TimeSpan.FromMilliseconds(250)); host.Frame(false, speed);
        Assert.InRange(host.Status.ServerTick, minimum, minimum + 1);
        Assert.Equal(host.Status.ServerTick * 100, host.Status.SimulationTimeMs);
        Assert.Equal(host.Status.ServerTick * 0.1, host.Session.Sim.T, 10);
    }

    [Fact]
    public void PauseAndUtcRollbackNeverCreateTicksAndResumeDoesNotCatchUpPausedTime()
    {
        var clock = new ManualClock(); using var host = new SingleplayerHost(Load(), "podstawowy", clock: clock);
        clock.ShiftUtc(TimeSpan.FromDays(-10)); clock.Advance(TimeSpan.FromHours(3)); host.Frame(true, 10);
        Assert.Equal(0, host.Status.ServerTick);
        clock.Advance(TimeSpan.FromMilliseconds(100)); host.Frame(false, 1); Assert.Equal(1, host.Status.ServerTick);
        clock.ShiftUtc(TimeSpan.FromDays(20)); host.Frame(false, 1); Assert.Equal(1, host.Status.ServerTick);
    }

    [Fact]
    public void StoppedSessionAndOtherThreadCannotReachMutableWorldAndRestartIsFresh()
    {
        var clock = new ManualClock(); var host = new SingleplayerHost(Load(), "podstawowy", clock: clock);
        var stale = host.Session; string epoch = host.Status.WorldRunId;
        Exception? failure = null;
        var other = new Thread(() => { try { _ = stale.Sim; } catch (Exception ex) { failure = ex; } });
        other.Start(); Assert.True(other.Join(2000)); Assert.IsType<InvalidOperationException>(failure);
        clock.Advance(TimeSpan.FromMilliseconds(50)); host.Frame(false, 1); host.Dispose();
        Assert.False(host.Status.Ready); Assert.Equal(WorldState.Stopped, host.Status.State);
        Assert.Throws<ObjectDisposedException>(() => stale.Exec("KW")); Assert.Throws<ObjectDisposedException>(() => stale.Receive());
        using var next = new SingleplayerHost(Load(), "podstawowy", clock: clock);
        Assert.NotEqual(epoch, next.Status.WorldRunId); next.Frame(false, 1); Assert.Equal(0, next.Status.ServerTick);
    }

    [Fact]
    public void ExternalSchedulingDoesNotFreezeOrScaleQueueExpiryAndKeepsReadyFalseAtZero()
    {
        var clock = new ManualClock(); var runtime = new RuntimeStub();
        var loop = new WorldLoop(() => runtime, new() { ExternalSteps = true }, clock);
        loop.Start(); var pending = loop.Submit(new("A", "r0", InputKind.Command, "x"));
        clock.Advance(TimeSpan.FromSeconds(3)); loop.PumpInputs();
        Assert.Equal(InputOutcome.Expired, WorldLoopTests.Completed(pending).Outcome); Assert.Equal(0, loop.Status.ServerTick);
        Assert.False(loop.Ready.IsCompleted); loop.RequestSteps(1); loop.Pump(); Assert.True(loop.Status.Ready);
        loop.Finish();
    }

    [Fact]
    public void LocalSynchronousInputHasResultEvenWhenOwnerWasDescheduledPastBudget()
    {
        var clock = new AdvancingClock(); var runtime = new RuntimeStub();
        var loop = new WorldLoop(() => runtime, new() { ExternalSteps = true }, clock);
        loop.Start(); var pending = loop.Submit(new("A", "r0", InputKind.Command, "x")); loop.PumpInputs();
        Assert.Equal(InputOutcome.Executed, WorldLoopTests.Completed(pending).Outcome); loop.Finish();
    }

    sealed class AdvancingClock : TimeProvider
    {
        long timestamp;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => Interlocked.Add(ref timestamp, 5);
    }

    [Fact]
    public void LocalAdapterLoggingRemainsBoundedDuringFloodAndKeepsAdvancingWorld()
    {
        SingleplayerLog.Register(); var directory = TestArtifacts.Directory("singleplayer-zalew");
        using var logs = SilnikLogow.Uruchom(new() { Aplikacja = "LcsWidzew", Katalog = directory, MinimalnyPoziom = LogLevel.Debug,
            MaxRekordowKolejki = 16, MaxBajtowKolejki = 16384, MaxBajtowPliku = 16384, MaxPlikow = 3 });
        var log = logs.Modul(SingleplayerLog.Module, WorldLog.Version);
        using (var host = new SingleplayerHost(Load(), "podstawowy", logger: log, worldLogger: logs.Modul(WorldLog.Module, WorldLog.Version)))
        {
            for (int n = 0; n < 10000; n++)
            {
                SingleplayerLog.Operation(log, "FloodProbe");
                if (n % 50 == 0) host.Step();
            }
            Assert.Equal(200, host.Status.ServerTick);
        }
        var state = logs.Zatrzymaj(); File.WriteAllText(Path.Combine(directory, "stan.txt"), state.ToString());
        Assert.True(state.StratyRazem > 0); Assert.True(state.MaxWKolejce <= state.LimitRekordow);
        Assert.True(state.MaxBajtowWKolejce <= state.LimitBajtow); Assert.True(state.ZamkniecieKompletne);
        Assert.InRange(Directory.GetFiles(directory, "*.jsonl").Length, 1, 3);
    }

    [Theory]
    [InlineData("podstawowy")] [InlineData("usterki")] [InlineData("zima")] [InlineData("manewry")]
    public void ApprovedInputsResultsIndicationsAndDomainEventsMatchLegacyWithLoggedInOperator(string scenario)
    {
        var station = Load(); var baseline = new Simulator(station); new Trains(baseline).Load(scenario);
        var desk = baseline.AddDesk("PC", "Stanowisko", true); desk.Page = baseline.Page; desk.User = Operator(); baseline.User = desk.User;
        var legacy = new LocalSession(baseline, desk);
        using var host = new SingleplayerHost(Load(), scenario, Operator()); var actual = host.Session;
        string point = station.Switches.Keys.First(); string page = station.Pages.First(p => p.Posts.Contains(station.Switches[point].Post)).Id;
        legacy.SetPage(page); actual.SetPage(page);
        var input = new Dictionary<int, string> { [0] = point + " Minus", [70] = point + " Plus", [80] = "KW", [90] = "NIEZNANE_POLECENIE" };
        foreach (int n in Enumerable.Range(0, 3001))
        {
            if (input.TryGetValue(n, out var text)) Assert.Equal(legacy.Exec(text), actual.Exec(text));
            if (n is 95 or 110) { legacy.Login(n == 95 ? null : Operator()); actual.Login(n == 95 ? null! : Operator()); }
            if (n == 100) Assert.Equal(legacy.Exec(point + " Minus"), actual.Exec(point + " Minus"));
            if (n is 120 or 121) { legacy.AddBook("wpis lokalny", ""); actual.AddBook("wpis lokalny", ""); }
            if (n == 130) { legacy.Cancel(); actual.Cancel(); }
            if (n % 100 == 0)
            {
                var left = legacy.Receive(); var right = actual.Receive();
                Assert.Equal(left.T, right.T); Assert.Equal(left.Clock, right.Clock);
                Assert.Equal(left.Sws.Select(p => (p.Key, p.Value.Pos)), right.Sws.Select(p => (p.Key, p.Value.Pos)));
                Assert.Equal(legacy.GetBook().Entries.Count, actual.GetBook().Entries.Count);
            }
            if (n == 3000) break;
            baseline.Tick(0.1); host.Step();
        }
        string events(Simulator s) => string.Join('\n', s.Events.Select(e => $"{e.Id}|{e.T.ToString("R", CultureInfo.InvariantCulture)}|{e.Kategoria}|{e.Zdarzenie}|{e.Operator}|{e.Obiekt}"));
        var folder = TestArtifacts.Directory("singleplayer-" + scenario);
        File.WriteAllText(Path.Combine(folder, "legacy.zdarzenia"), events(baseline)); File.WriteAllText(Path.Combine(folder, "adapter.zdarzenia"), events(actual.Sim));
        Assert.Equal(events(baseline), events(actual.Sim)); Assert.Equal(3000, host.Status.ServerTick);
        Assert.Equal(baseline.Trains.List.Select(t => (t.Id, t.Head, t.V)), actual.Sim.Trains.List.Select(t => (t.Id, t.Head, t.V)));
    }

    [Fact]
    public void AdapterProducesCorrelatedBoundedRedactedLogsWithoutUserOrCommandText()
    {
        SingleplayerLog.Register(); var folder = TestArtifacts.Directory("singleplayer-N43");
        using var logs = SilnikLogow.Uruchom(new() { Aplikacja = "LcsWidzew", Katalog = folder, MinimalnyPoziom = LogLevel.Debug });
        var log = logs.Modul(SingleplayerLog.Module, WorldLog.Version); var world = logs.Modul(WorldLog.Module, WorldLog.Version);
        var user = Operator(); user.Name = "SEKRET_TESTOWY_NIE_LOGUJ_OPERATORA";
        using (var host = new SingleplayerHost(Load(), "podstawowy", user, logger: log, worldLogger: world))
        {
            host.Session.Exec("SEKRET_TESTOWY_PELNE_POLECENIE"); host.Step();
            ProbaLogow.Wykonaj(log);
        }
        var state = logs.Zatrzymaj(); File.WriteAllText(Path.Combine(folder, "stan.txt"), state.ToString());
        string all = string.Join('\n', Directory.GetFiles(folder, "*.jsonl").Select(File.ReadAllText));
        Assert.DoesNotContain("SEKRET_TESTOWY", all);
        var records = TestArtifacts.Records(folder).Where(r => r.GetProperty("eventCode").GetString()!.StartsWith("SP.")).ToArray();
        Assert.Contains(records, r => r.GetProperty("eventCode").GetString() == "SP.STARTED");
        Assert.Contains(records, r => r.GetProperty("eventCode").GetString() == "SP.STOPPED");
        Assert.All(records, r => Assert.True(r.TryGetProperty("worldRunId", out _)));
        Assert.Contains(records, r => r.TryGetProperty("requestId", out _)); Assert.True(state.ZamkniecieKompletne);
    }
}

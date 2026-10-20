using System.Globalization;
using System.Security.Cryptography;
using LcsWidzew.Model;
using LcsWidzew.Sim;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Lcs.WorldHost.Testy;

public sealed class SimulatorRuntimeTests
{
    [Theory]
    [InlineData("podstawowy")]
    [InlineData("usterki")]
    [InlineData("manewry")]
    public void HostMatchesLegacySimulatorWithSameScenarioAndCommandSequence(string scenario)
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "TestData", "station_test.json");
        byte[] before = SHA256.HashData(File.ReadAllBytes(fixture));
        string root = Path.Combine(Environment.GetEnvironmentVariable("SUK_WORLD_TEST_ARTIFACTS") ?? Path.GetTempPath(), "porownanie-" + scenario);
        Directory.CreateDirectory(root);
        string copy = Path.Combine(root, "station_test.json"); File.Copy(fixture, copy, true);
        var station = Station.Load(copy); var baseline = new Simulator(station); new Trains(baseline).Load(scenario);
        var desk = baseline.AddDesk("HOST", "Lokalne stanowisko hosta", true); desk.Page = baseline.Page;
        var runtime = new SimulatorRuntime(copy, scenario, NullLogger.Instance, NullLogger.Instance);
        var clock = new ManualClock(); var loop = new WorldLoop(() => runtime, clock: clock); loop.Start();
        string turnout = station.Switches.Keys.First();
        var requests = new Dictionary<int, (InputKind Kind, string Text)>
        {
            [5] = (InputKind.Command, turnout + " Minus"), [70] = (InputKind.Command, turnout + " Plus"),
            [90] = (InputKind.Command, "NIEZNANE_POLECENIE_TESTU"), [100] = (InputKind.Confirm, "brak"), [110] = (InputKind.Cancel, "")
        };
        for (int tick = 1; tick <= 3000; tick++)
        {
            baseline.Tick(0.1); clock.Advance(TimeSpan.FromMilliseconds(100)); loop.Pump();
            Assert.Equal(tick, loop.Status.ServerTick);
            if (!requests.TryGetValue(tick, out var request)) continue;
            var expected = request.Kind switch
            {
                InputKind.Command => baseline.ExecAs(desk, request.Text),
                InputKind.Confirm => baseline.ConfirmAs(desk, request.Text),
                InputKind.Cancel => Cancel(baseline, desk), _ => throw new InvalidOperationException()
            };
            var result = loop.Submit(new("adapter-testu", "r" + tick, request.Kind, request.Text)); loop.Pump();
            var completed = WorldLoopTests.Completed(result);
            Assert.Equal(expected.Ok ? InputOutcome.Executed : InputOutcome.Rejected, completed.Outcome);
            if (request.Kind != InputKind.Cancel) Assert.Equal(expected.Msg ?? "", completed.Message);
        }
        loop.Finish(); string expectedEvents = Events(baseline);
        File.WriteAllText(Path.Combine(root, "legacy.zdarzenia"), expectedEvents);
        File.WriteAllText(Path.Combine(root, "host.zdarzenia"), runtime.FinalEvents);
        Assert.NotEmpty(baseline.Events); Assert.Equal(expectedEvents, runtime.FinalEvents);
        var hashes = Assert.IsAssignableFrom<IDictionary<string, string>>(runtime.InputHashes);
        Assert.True(hashes.IsReadOnly); Assert.Throws<NotSupportedException>(() => hashes["station"] = "podmiana");
        Assert.Equal(baseline.T, loop.Status.Observation!.SimulationSeconds);
        Assert.Equal(baseline.Trains.List.Count, loop.Status.Observation.Trains);
        Assert.Equal(baseline.Routes.Count, loop.Status.Observation.Routes);
        Assert.Equal(baseline.Events.Count, loop.Status.Observation.Events);
        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(fixture))); Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(copy)));
        File.WriteAllText(Path.Combine(root, "wynik.txt"), $"ZGODNE: {scenario}; 3000 kroków po 0,1 s; 5 wejść; {baseline.Events.Count} zdarzeń; SHA256 układu: {Convert.ToHexStringLower(before)}\n");
    }

    [Fact]
    public void RuntimePublicSurfaceDoesNotExposeMutableSimulation()
    {
        Assert.All(typeof(SimulatorRuntime).GetProperties(), p => Assert.True(p.PropertyType == typeof(string) ||
            p.PropertyType == typeof(IReadOnlyDictionary<string, string>), "Publiczna właściwość musi być kopią/projekcją: " + p.Name));
        Assert.Empty(typeof(SimulatorRuntime).GetFields());
        Assert.All(typeof(WorldStatus).GetProperties(), p => Assert.False(typeof(System.Collections.IEnumerable).IsAssignableFrom(p.PropertyType) && p.PropertyType != typeof(string)));
    }

    static CmdResult Cancel(Simulator simulation, Desk desk) { simulation.CancelAs(desk); return new(true, ""); }
    static string Events(Simulator simulation) => string.Join('\n', simulation.Events.Select(e =>
        $"{e.Id}|{e.T.ToString("R", CultureInfo.InvariantCulture)}|{e.Czas}|{e.Kategoria}|{e.Zdarzenie}|{e.Obiekt}")) + "\n";
}

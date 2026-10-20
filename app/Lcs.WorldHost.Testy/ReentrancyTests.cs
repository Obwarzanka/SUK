using LcsWidzew.Model;
using LcsWidzew.Sim;
using Xunit;

namespace Lcs.WorldHost.Testy;

/// <summary>Regresje niezależnego review S02.02: odrzucone ponowne wejście nie może zostawić ukrytej komendy, kroku ani stopu.</summary>
public sealed class ReentrancyTests
{
    static Station Load() => Station.Load(Path.Combine(AppContext.BaseDirectory, "TestData", "station_test.json"));
    static User Operator() => new() { Name = "Operator próby review", Role = "dyżurny ruchu" };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectedCommandInsideCommandOrLocalApplyNeverEntersQueueOrMovesTurnout(bool insideApply)
    {
        var clock = new ManualClock();
        using var host = new SingleplayerHost(Load(), "podstawowy", Operator(), clock: clock);
        var sim = host.Session.Sim;
        string point = sim.L.Switches.Keys.First();
        string page = sim.L.Pages.First(p => p.Posts.Contains(sim.L.Switches[point].Post)).Id;
        host.Session.SetPage(page);
        bool called = false;
        host.Session.EventAdded += _ =>
        {
            if (called) return;
            called = true;
            Assert.Throws<InvalidOperationException>(() => host.Session.Exec(point + " Minus"));
        };

        if (insideApply) host.Session.Phone("*", "Wiadomość próby review");
        else Assert.False(host.Session.Exec("REVIEW_NIEZNANE_POLECENIE").Ok);
        Assert.True(called, "Próba musi rzeczywiście wejść do synchronicznego callbacku rdzenia.");

        host.Frame(true, 1);
        host.Step();
        Assert.Equal(insideApply ? 0L : 1L, host.Status.Accepted);
        Assert.Equal(insideApply ? 0L : 1L, host.Status.Completed);
        Assert.Equal(0, host.Status.Queued);
        Assert.Equal(1, host.Status.ServerTick);
        Assert.Null(sim.Sws[point].Moving);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectedFrameOrStepInsideCommandDoesNotLeaveRequestedTime(bool frame)
    {
        var clock = new ManualClock();
        using var host = new SingleplayerHost(Load(), "podstawowy", Operator(), clock: clock);
        bool called = false;
        host.Session.EventAdded += _ =>
        {
            if (called) return;
            called = true;
            clock.Advance(TimeSpan.FromMilliseconds(250));
            if (frame) Assert.Throws<InvalidOperationException>(() => host.Frame(false, 10));
            else Assert.Throws<InvalidOperationException>(host.Step);
        };
        Assert.False(host.Session.Exec("REVIEW_NIEZNANE_POLECENIE").Ok);
        Assert.True(called);

        host.Frame(true, 1);
        Assert.Equal(0, host.Status.ServerTick);
        host.Step();
        Assert.Equal(1, host.Status.ServerTick);
        Assert.Equal(0.1, host.Session.Sim.T, 10);
    }

    [Fact]
    public void RejectedDisposeInsideCommandDoesNotStopOrDisposeActiveHost()
    {
        using var host = new SingleplayerHost(Load(), "podstawowy", Operator());
        bool called = false;
        host.Session.EventAdded += _ =>
        {
            if (called) return;
            called = true;
            Assert.Throws<InvalidOperationException>(host.Dispose);
        };
        Assert.False(host.Session.Exec("REVIEW_NIEZNANE_POLECENIE").Ok);
        Assert.True(called);
        Assert.False(host.IsDisposed);
        Assert.Equal(WorldState.Starting, host.Status.State);
        Assert.Equal(1, host.Status.Completed);

        host.Step();
        Assert.True(host.Status.Ready);
        host.Dispose();
        Assert.Equal(WorldState.Stopped, host.Status.State);
        Assert.Equal(1, host.Status.Completed);
    }

    [Fact]
    public void RejectedFinishInsideExecuteDoesNotDisposeRuntimeOrPublishEarlyCompletion()
    {
        var clock = new ManualClock();
        var runtime = new RuntimeStub();
        var loop = new WorldLoop(() => runtime, new() { ExternalSteps = true }, clock);
        loop.Start(); loop.RequestSteps(1); loop.Pump();
        runtime.OnExecute = _ =>
        {
            Assert.Throws<InvalidOperationException>(loop.Finish);
            Assert.Equal(0, runtime.Disposals);
            Assert.True(loop.Status.Ready);
            Assert.False(loop.Completion.IsCompleted);
        };

        var pending = loop.Submit(new("review", "r0", InputKind.Command, "x"));
        loop.PumpInputs();
        Assert.Equal(InputOutcome.Executed, WorldLoopTests.Completed(pending).Outcome);
        Assert.Equal(1, loop.Status.Completed);
        Assert.Equal(0, runtime.Disposals);
        Assert.False(loop.Completion.IsCompleted);

        loop.Finish();
        Assert.Equal(1, runtime.Disposals);
        Assert.Equal(loop.Status, WorldLoopTests.Completed(loop.Completion));
    }

    [Fact]
    public void ApplyLocalRejectsNestedFinishPumpAndStepWithoutStopOrTimeSideEffects()
    {
        var clock = new ManualClock();
        var runtime = new RuntimeStub();
        var loop = new WorldLoop(() => runtime, new() { ExternalSteps = true }, clock);
        loop.Start(); loop.RequestSteps(1); loop.Pump();

        int value = loop.ApplyLocal(() =>
        {
            Assert.Throws<InvalidOperationException>(loop.Finish);
            Assert.Throws<InvalidOperationException>(loop.PumpInputs);
            Assert.Throws<InvalidOperationException>(() => loop.RequestSteps(3));
            Assert.Throws<InvalidOperationException>(() => loop.ApplyLocal(() => 9));
            Assert.True(loop.Status.Ready);
            Assert.Equal(0, runtime.Disposals);
            Assert.False(loop.Completion.IsCompleted);
            return 7;
        });

        Assert.Equal(7, value);
        loop.Pump();
        Assert.Equal(1, loop.Status.ServerTick);
        Assert.Single(runtime.Ticks);
        loop.Finish();
        Assert.Equal(1, runtime.Disposals);
        Assert.Equal(WorldState.Stopped, loop.Status.State);
        Assert.Equal(loop.Status, WorldLoopTests.Completed(loop.Completion));
    }
}

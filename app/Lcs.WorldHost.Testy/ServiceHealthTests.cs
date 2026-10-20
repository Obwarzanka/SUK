using Xunit;

namespace Lcs.WorldHost.Testy;

/// <summary>Niezależne próby gotowości procesu: obecność odpowiedzi nie zastępuje postępu świata.</summary>
public sealed class ServiceHealthTests
{
    static ServiceProfile Profile() => new()
    {
        AccountSid = "S-1-5-21-1-2-3-1001", DataRoot = Path.Combine(Path.GetTempPath(), "SUK-health-test"),
        FreshnessSeconds = 2, MaxWorkingSetMiB = 64
    };

    static (WorldLoop Loop, ManualClock Clock, RuntimeStub Runtime, ServiceHealth Health) Create(
        Func<bool>? diagnostics = null, Func<long>? memory = null)
    {
        var clock = new ManualClock(); var runtime = new RuntimeStub();
        var loop = new WorldLoop(() => runtime, clock: clock);
        var health = new ServiceHealth(loop, Profile(), diagnostics, memory ?? (() => 1024), clock);
        return (loop, clock, runtime, health);
    }

    static void FirstTick(WorldLoop loop, ManualClock clock)
    {
        loop.Start(); clock.Advance(TimeSpan.FromMilliseconds(100)); loop.Pump();
    }

    [Fact]
    public void NewAndInitializedWorldAreLiveButHaveNoProgressOrReadiness()
    {
        var (loop, _, _, health) = Create();
        foreach (bool initialize in new[] { false, true })
        {
            if (initialize) loop.Start();
            var status = health.Read();
            Assert.True(status.Live); Assert.False(status.Ready); Assert.False(status.Fresh);
            Assert.Null(status.ProgressAgeMs); Assert.Null(loop.Status.LastStepTimestamp);
            Assert.Equal("STARTING", status.ReasonCode); Assert.Equal(0, status.ServerTick);
        }
        loop.Finish();
    }

    [Fact]
    public void FirstCompletedTickPublishesImmutableReadySnapshot()
    {
        var (loop, clock, _, health) = Create(); FirstTick(loop, clock);
        var ready = health.Read();
        Assert.Equal(1, ready.SchemaVersion); Assert.Equal(Environment.ProcessId, ready.ProcessId);
        Assert.True(ready.Live); Assert.True(ready.Ready); Assert.True(ready.Fresh);
        Assert.True(ready.DiagnosticsHealthy); Assert.True(ready.ResourcesHealthy);
        Assert.Equal(0, ready.ProgressAgeMs); Assert.Equal(1, ready.ServerTick); Assert.Equal(100, ready.SimulationTimeMs);
        Assert.Equal(loop.Status.WorldId, ready.WorldId); Assert.Equal(loop.Status.WorldRunId, ready.WorldRunId);
        Assert.Equal(clock.GetTimestamp(), loop.Status.LastStepTimestamp);
        var changed = ready with { Ready = false, WorldRunId = "obca-epoka" };
        Assert.False(changed.Ready); Assert.True(health.Read().Ready); Assert.Equal(ready.WorldRunId, health.Read().WorldRunId);
        loop.Finish(); Assert.True(ready.Ready); Assert.False(health.Read().Ready);
    }

    [Theory]
    [InlineData(1999, true, "READY")]
    [InlineData(2000, false, "WORLD_STALE")]
    [InlineData(2001, false, "WORLD_STALE")]
    public void RepeatedLiveReadsDoNotRenewProgressAndExactFreshnessBoundaryIsExclusive(int elapsedMs, bool expected, string reason)
    {
        var (loop, clock, _, health) = Create(); FirstTick(loop, clock);
        long? completedAt = loop.Status.LastStepTimestamp;
        clock.Advance(TimeSpan.FromMilliseconds(elapsedMs));
        for (int read = 0; read < 20; read++)
        {
            var status = health.Read(); Assert.True(status.Live); Assert.Equal(expected, status.Ready);
            Assert.Equal(expected, status.Fresh); Assert.Equal(reason, status.ReasonCode);
            Assert.Equal(elapsedMs, status.ProgressAgeMs); Assert.Equal(1, status.ServerTick);
            Assert.Equal(completedAt, loop.Status.LastStepTimestamp);
        }
        loop.Finish();
    }

    [Fact]
    public void UtcRollbackAndForwardCannotRenewStaleWorldOrAgeFreshWorld()
    {
        var (loop, clock, _, health) = Create(); FirstTick(loop, clock);
        clock.ShiftUtc(TimeSpan.FromDays(500)); Assert.True(health.Read().Ready); Assert.Equal(0, health.Read().ProgressAgeMs);
        clock.ShiftUtc(TimeSpan.FromDays(-1000)); Assert.True(health.Read().Ready);
        clock.Advance(TimeSpan.FromSeconds(2)); Assert.False(health.Read().Ready);
        clock.ShiftUtc(TimeSpan.FromDays(10000)); Assert.Equal("WORLD_STALE", health.Read().ReasonCode);
        clock.ShiftUtc(TimeSpan.FromDays(-20000)); Assert.Equal(2000, health.Read().ProgressAgeMs);
        Assert.False(health.Read().Ready); loop.Finish();
    }

    [Fact]
    public void FreshCompletedTickRestoresReadinessAfterStaleness()
    {
        var (loop, clock, _, health) = Create(); FirstTick(loop, clock);
        clock.Advance(TimeSpan.FromSeconds(3)); Assert.Equal("WORLD_STALE", health.Read().ReasonCode);
        loop.Pump(); var recovered = health.Read();
        Assert.True(recovered.Ready); Assert.Equal("READY", recovered.ReasonCode); Assert.Equal(0, recovered.ProgressAgeMs);
        Assert.True(recovered.ServerTick > 1); Assert.False(loop.Completion.IsCompleted); loop.Finish();
    }

    [Theory]
    [InlineData("initialize")]
    [InlineData("tick")]
    [InlineData("observe")]
    public void InitializationTickAndObservationFailuresNeverBecomeReady(string point)
    {
        var (loop, clock, runtime, health) = Create(); runtime.ThrowAt = point;
        FirstTick(loop, clock); var failed = health.Read();
        Assert.True(failed.Live); Assert.False(failed.Ready); Assert.False(failed.Fresh);
        Assert.Null(failed.ProgressAgeMs); Assert.Null(loop.Status.LastStepTimestamp);
        Assert.Equal("WORLD_FAILED", failed.ReasonCode); Assert.Equal(WorldState.Faulted, failed.State);
        Assert.True(loop.Completion.IsCompletedSuccessfully); Assert.Equal(1, runtime.Disposals);
    }

    [Fact]
    public void FailedObservationCannotRefreshPreviouslyPublishedProgress()
    {
        var (loop, clock, runtime, health) = Create(); FirstTick(loop, clock);
        long? previous = loop.Status.LastStepTimestamp;
        runtime.ThrowAt = "observe"; clock.Advance(TimeSpan.FromSeconds(2)); loop.Pump();
        Assert.Equal(WorldState.Faulted, health.Read().State); Assert.False(health.Read().Ready);
        Assert.Equal("WORLD_FAILED", health.Read().ReasonCode); Assert.Equal(previous, loop.Status.LastStepTimestamp);
        Assert.Equal(2000, health.Read().ProgressAgeMs);
    }

    [Fact]
    public void DiagnosticsDegradationWithdrawsReadyWithoutStoppingAutonomousWorldAndCanRecover()
    {
        bool loggingHealthy = true;
        var (loop, clock, runtime, health) = Create(() => loggingHealthy); FirstTick(loop, clock);
        loggingHealthy = false; var degraded = health.Read();
        Assert.True(degraded.Live); Assert.True(degraded.Fresh); Assert.False(degraded.Ready);
        Assert.False(degraded.DiagnosticsHealthy); Assert.Equal("DIAGNOSTICS_UNHEALTHY", degraded.ReasonCode);
        clock.Advance(TimeSpan.FromMilliseconds(100)); loop.Pump();
        Assert.Equal(2, runtime.Ticks.Count); Assert.True(loop.Status.Ready); Assert.False(loop.Completion.IsCompleted);
        Assert.False(health.Read().Ready); loggingHealthy = true; Assert.True(health.Read().Ready);
        Assert.Equal("READY", health.Read().ReasonCode); loop.Finish();
    }

    [Theory]
    [InlineData(64L * 1024 * 1024, true)]
    [InlineData(64L * 1024 * 1024 + 1, false)]
    public void WorkingSetBudgetHasExactBoundaryAndRecoveryDoesNotRestartWorld(long bytes, bool expected)
    {
        long workingSet = bytes;
        var (loop, clock, _, health) = Create(memory: () => workingSet); FirstTick(loop, clock);
        string epoch = health.Read().WorldRunId; var status = health.Read();
        Assert.Equal(expected, status.ResourcesHealthy); Assert.Equal(expected, status.Ready);
        Assert.Equal(expected ? "READY" : "MEMORY_LIMIT", status.ReasonCode);
        Assert.True(status.Live); Assert.False(loop.Completion.IsCompleted);
        workingSet = 1024; Assert.True(health.Read().Ready); Assert.Equal(epoch, health.Read().WorldRunId); loop.Finish();
    }

    [Fact]
    public void StopAndTerminalServiceFailureWithdrawReadinessImmediately()
    {
        var (loop, clock, _, health) = Create(); FirstTick(loop, clock);
        health.Fail("SERVICE_STARTUP_TIMEOUT"); var stopping = health.Read();
        Assert.False(stopping.Ready); Assert.True(stopping.Live); Assert.Equal("SERVICE_STARTUP_TIMEOUT", stopping.ReasonCode);
        Assert.False(loop.Status.Ready); Assert.False(loop.Completion.IsCompleted);
        loop.Pump(); Assert.Equal(WorldState.Stopped, health.Read().State); Assert.False(health.Read().Ready);
        Assert.Equal("SERVICE_STARTUP_TIMEOUT", health.Read().ReasonCode);
    }

    [Fact]
    public void BlockedTickDoesNotBlockHealthAndStopWithdrawsReadyBeforeTickCanFinish()
    {
        var (loop, clock, runtime, health) = Create();
        using var started = new ManualResetEventSlim(); using var second = new ManualResetEventSlim();
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        CancellationToken testToken = TestContext.Current.CancellationToken;
        Exception? ownerFailure = null;
        runtime.OnTick = () => { if (runtime.Ticks.Count == 2) { entered.Set(); if (!release.Wait(TimeSpan.FromSeconds(5), testToken)) throw new TimeoutException("Brak zwolnienia kontrolowanej blokady testu."); } };
        var owner = new Thread(() =>
        {
            try
            {
                FirstTick(loop, clock); started.Set();
                if (!second.Wait(TimeSpan.FromSeconds(5), testToken)) throw new TimeoutException("Nie zlecono drugiego kroku testu.");
                clock.Advance(TimeSpan.FromMilliseconds(100)); loop.Pump();
            }
            catch (Exception error) { ownerFailure = error; }
        });
        owner.Start();
        try
        {
            Assert.True(started.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)); Assert.True(health.Read().Ready);
            second.Set(); Assert.True(entered.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            clock.Advance(TimeSpan.FromSeconds(2)); var stale = health.Read();
            Assert.True(stale.Live); Assert.False(stale.Ready); Assert.Equal("WORLD_STALE", stale.ReasonCode);
            Assert.Equal(1, stale.ServerTick);
            health.Stopping(); Assert.False(health.Read().Ready); Assert.Equal("STOPPING", health.Read().ReasonCode);
            Assert.False(loop.Status.Ready); Assert.False(loop.Completion.IsCompleted);
        }
        finally { second.Set(); release.Set(); Assert.True(owner.Join(TimeSpan.FromSeconds(5))); }
        Assert.Null(ownerFailure); Assert.True(loop.Completion.IsCompletedSuccessfully);
        Assert.Equal(WorldState.Stopped, health.Read().State); Assert.False(health.Read().Ready);
    }

    [Fact]
    public void RestartRequiresFreshTickAndGetsNewWorldRunEpoch()
    {
        var (oldLoop, oldClock, _, oldHealth) = Create(); FirstTick(oldLoop, oldClock);
        var oldReady = oldHealth.Read(); oldHealth.Stopping(); oldLoop.Pump();
        var (nextLoop, nextClock, _, nextHealth) = Create(); nextLoop.Start();
        Assert.Equal(oldReady.WorldId, nextHealth.Read().WorldId); Assert.NotEqual(oldReady.WorldRunId, nextHealth.Read().WorldRunId);
        Assert.False(nextHealth.Read().Ready); Assert.Null(nextHealth.Read().ProgressAgeMs);
        nextClock.Advance(TimeSpan.FromMilliseconds(100)); nextLoop.Pump(); Assert.True(nextHealth.Read().Ready);
        Assert.Equal(1, nextHealth.Read().ServerTick); Assert.False(oldHealth.Read().Ready); nextLoop.Finish();
    }

    [Fact]
    public void ServiceFailureRecordsExitConditionBeforeStopIsVisibleToWorldOwner()
    {
        var (loop, clock, _, _) = Create(); FirstTick(loop, clock);
        bool callbackCalled = false, readyDuringCallback = false; string? reasonDuringCallback = null;
        ServiceHealth? health = null;
        health = new ServiceHealth(loop, Profile(), workingSet: () => 1024, clock: clock, beforeFailureStop: () =>
        {
            callbackCalled = true; readyDuringCallback = loop.Status.Ready; reasonDuringCallback = health!.FailureCode;
        });
        health.Fail("HEALTH_PIPE_FAILED");
        Assert.True(callbackCalled); Assert.True(readyDuringCallback); Assert.Equal("HEALTH_PIPE_FAILED", reasonDuringCallback);
        Assert.False(loop.Status.Ready); Assert.False(health.Read().Ready); Assert.Equal("HEALTH_PIPE_FAILED", health.Read().ReasonCode);
        loop.Pump(); Assert.Equal(WorldState.Stopped, loop.Status.State);
    }
}

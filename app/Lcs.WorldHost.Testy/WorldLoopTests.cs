using Xunit;

[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace Lcs.WorldHost.Testy;

/// <summary>Próby kontraktu z jawnym stubem. Stub nie implementuje ani nie zalicza przyszłego KZ/Ada.</summary>
public sealed class WorldLoopTests
{
    static WorldInput Input(string producer = "A", string id = "r0", string text = "polecenie") => new(producer, id, InputKind.Command, text);

    static (WorldLoop Loop, ManualClock Clock, RuntimeStub Runtime) Started(WorldLoopOptions? options = null)
    {
        var clock = new ManualClock(); var runtime = new RuntimeStub();
        var loop = new WorldLoop(() => runtime, options, clock);
        loop.Start(); clock.Advance(TimeSpan.FromMilliseconds(100)); loop.Pump();
        Assert.True(loop.Status.Ready);
        return (loop, clock, runtime);
    }

    static WorldResult Result(Task<WorldResult> task)
    {
        Assert.True(task.IsCompletedSuccessfully, "Każde zakończone żądanie musi mieć jawny wynik.");
        return task.GetAwaiter().GetResult();
    }

    [Fact]
    public void ReadyRequiresFirstCompletedTickAndSuccessfulObservation()
    {
        var clock = new ManualClock(); var runtime = new RuntimeStub(); var loop = new WorldLoop(() => runtime, clock: clock);
        Assert.Equal(InputOutcome.NotReady, Result(loop.Submit(Input())).Outcome);
        loop.Start();
        Assert.Equal(WorldState.Starting, loop.Status.State); Assert.False(loop.Ready.IsCompleted); Assert.False(loop.Status.Ready);
        Assert.Equal(InputOutcome.NotReady, Result(loop.Submit(Input())).Outcome);
        clock.Advance(TimeSpan.FromMilliseconds(99)); loop.Pump();
        Assert.Empty(runtime.Ticks); Assert.False(loop.Ready.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(1)); loop.Pump();
        Assert.True(loop.Ready.IsCompletedSuccessfully); Assert.Equal(1, Completed(loop.Ready).ServerTick);
        Assert.Equal(WorldState.Running, loop.Status.State); Assert.NotNull(loop.Status.Observation);
        loop.Finish(); Assert.True(loop.Completion.IsCompletedSuccessfully); Assert.False(loop.Status.Ready);
    }

    [Fact]
    public void UtcRollbackAndAdvanceDoNotDriveSimulation()
    {
        var (loop, clock, runtime) = Started();
        clock.ShiftUtc(TimeSpan.FromDays(-2)); loop.Pump(); Assert.Single(runtime.Ticks);
        clock.Advance(TimeSpan.FromMilliseconds(100)); loop.Pump(); Assert.Equal(2, runtime.Ticks.Count);
        clock.ShiftUtc(TimeSpan.FromDays(5)); loop.Pump(); Assert.Equal(2, runtime.Ticks.Count);
        Assert.Equal(200, loop.Status.SimulationTimeMs); Assert.All(runtime.Ticks, x => Assert.Equal(0.1, x));
        loop.Finish();
    }

    [Fact]
    public void CatchUpHasBoundedPassesAndPreservesEveryPointOneSecondStep()
    {
        var (loop, clock, runtime) = Started(new() { CatchUpSteps = 3 });
        var pending = loop.Submit(Input()); clock.Advance(TimeSpan.FromMilliseconds(1000));
        foreach (int expected in new[] { 4, 7, 10 })
        {
            loop.Pump(); Assert.Equal(expected, runtime.Ticks.Count); Assert.False(pending.IsCompleted);
        }
        loop.Pump(); Assert.Equal(11, runtime.Ticks.Count); Assert.Equal(InputOutcome.Executed, Result(pending).Outcome);
        Assert.All(runtime.Ticks, x => Assert.Equal(0.1, x)); Assert.Equal(1100, loop.Status.SimulationTimeMs);
        Assert.True(loop.Status.MaxLatenessUs >= 900000); loop.Finish();
    }

    [Fact]
    public void ObservationPublishesInitialTickThenFiveTimesPerSimulatedSecond()
    {
        var (loop, clock, runtime) = Started(); var initial = loop.Status;
        Assert.Equal(1, initial.Publications);
        for (int n = 2; n <= 10; n++) { clock.Advance(TimeSpan.FromMilliseconds(100)); loop.Pump(); }
        Assert.Equal(new[] { 1, 2, 4, 6, 8, 10 }, runtime.ObservedAt);
        Assert.Equal(6, loop.Status.Publications); Assert.Equal(1, initial.ServerTick);
        Assert.Equal(0.1, initial.Observation!.SimulationSeconds);
        var changed = initial with { SimulationTimeMs = -1, Observation = initial.Observation with { Trains = 999 } };
        Assert.Equal(-1, changed.SimulationTimeMs); Assert.Equal(1000, loop.Status.SimulationTimeMs);
        Assert.NotEqual(999, loop.Status.Observation!.Trains); loop.Finish();
    }

    [Fact]
    public void ProducerRoundRobinRetainsFifoWithinEachProducer()
    {
        var (loop, _, runtime) = Started(new() { CommandsPerPass = 2 });
        var tasks = new List<Task<WorldResult>>();
        foreach (string producer in new[] { "A", "B", "C" })
            for (int n = 0; n < 3; n++) tasks.Add(loop.Submit(Input(producer, producer + n)));
        for (int n = 0; n < 5; n++) loop.Pump();
        Assert.Equal(new[] { "A0", "B0", "C0", "A1", "B1", "C1", "A2", "B2", "C2" }, runtime.Executed);
        Assert.All(tasks, t => Assert.Equal(InputOutcome.Executed, Result(t).Outcome));
        Assert.Equal(9, loop.Status.Completed); Assert.Equal(0, loop.Status.QueuedBytes); loop.Finish();
    }

    [Fact]
    public void PerProducerLimitDoesNotConsumeOtherProducerShareAndReleasesAfterTake()
    {
        var (loop, _, _) = Started(new() { QueueCapacity = 5, PerProducerCapacity = 2, CommandsPerPass = 1 });
        var a0 = loop.Submit(Input(id: "a0")); var a1 = loop.Submit(Input(id: "a1"));
        Assert.Equal(InputOutcome.QueueFull, Result(loop.Submit(Input(id: "a2"))).Outcome);
        var b0 = loop.Submit(Input("B", "b0")); Assert.False(b0.IsCompleted);
        loop.Pump(); Assert.Equal(InputOutcome.Executed, Result(a0).Outcome); Assert.False(a1.IsCompleted);
        var a2 = loop.Submit(Input(id: "a2")); Assert.False(a2.IsCompleted);
        loop.Finish(); Assert.All(new[] { a1, b0, a2 }, t => Assert.Equal(InputOutcome.Stopping, Result(t).Outcome));
        Assert.Equal(4, loop.Status.Accepted); Assert.Equal(1, loop.Status.Rejected);
    }

    [Fact]
    public void GlobalCountLimitIncludesAllProducersAndReleasesAfterTake()
    {
        var (loop, _, _) = Started(new() { QueueCapacity = 2, PerProducerCapacity = 2, CommandsPerPass = 1 });
        var first = loop.Submit(Input()); var second = loop.Submit(Input("B"));
        Assert.Equal(InputOutcome.QueueFull, Result(loop.Submit(Input("C"))).Outcome);
        loop.Pump(); Assert.Equal(InputOutcome.Executed, Result(first).Outcome);
        var third = loop.Submit(Input("C")); Assert.False(third.IsCompleted); loop.Finish();
        Assert.Equal(InputOutcome.Stopping, Result(second).Outcome); Assert.Equal(InputOutcome.Stopping, Result(third).Outcome);
    }

    [Fact]
    public void GlobalByteLimitAccountsForPayloadAndReturnsCapacityAfterExecution()
    {
        var (loop, _, _) = Started(new() { QueueByteCapacity = 2048, CommandsPerPass = 1 });
        // Jeden duży ładunek mieści się w budżecie, dwa przekraczają go mimo wolnych miejsc liczbowych.
        var first = loop.Submit(Input(text: new string('ą', 256)));
        Assert.Equal(InputOutcome.QueueFull, Result(loop.Submit(Input("B", text: new string('ć', 256)))).Outcome);
        loop.Pump(); Assert.Equal(InputOutcome.Executed, Result(first).Outcome);
        var next = loop.Submit(Input("B", text: new string('ć', 256))); Assert.False(next.IsCompleted);
        loop.Finish(); Assert.Equal(InputOutcome.Stopping, Result(next).Outcome); Assert.Equal(0, loop.Status.QueuedBytes);
    }

    public static IEnumerable<object?[]> InvalidInputs()
    {
        yield return new object?[] { null };
        yield return new object?[] { new WorldInput(null!, "r", InputKind.Command, "x") };
        yield return new object?[] { new WorldInput("a", null!, InputKind.Command, "x") };
        yield return new object?[] { new WorldInput("a", "r", InputKind.Command, null!) };
        yield return new object?[] { Input("") }; yield return new object?[] { Input("a b") };
        yield return new object?[] { Input(id: "") }; yield return new object?[] { Input(id: new string('r', 65)) };
        yield return new object?[] { Input(text: new string('x', 513)) }; yield return new object?[] { Input(text: "x\ny") };
        yield return new object?[] { Input(text: "x\0y") };
        yield return new object?[] { new WorldInput("a", "r", (InputKind)999, "x") };
    }

    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public void InvalidAdmissionReturnsResultWithoutCrashingOrExecuting(WorldInput? input)
    {
        var (loop, _, runtime) = Started();
        Assert.Equal(InputOutcome.Invalid, Result(loop.Submit(input!)).Outcome);
        loop.Pump(); Assert.Empty(runtime.Executed); Assert.Equal(0, loop.Status.Accepted); Assert.Equal(1, loop.Status.Rejected);
        loop.Finish();
    }

    [Fact]
    public void AdmissionAcceptsExactMaximumLengthsAndEmptyCancelText()
    {
        var (loop, _, runtime) = Started();
        var tasks = new[] { loop.Submit(Input(new string('a', 64), new string('b', 64), new string('ą', 512))),
            loop.Submit(new WorldInput("c", "d", InputKind.Cancel, "")) };
        loop.Pump(); Assert.All(tasks, t => Assert.Equal(InputOutcome.Executed, Result(t).Outcome)); Assert.Equal(2, runtime.Executed.Count);
        loop.Finish();
    }

    [Theory]
    [InlineData(79, InputOutcome.Executed)]
    [InlineData(80, InputOutcome.Expired)]
    [InlineData(81, InputOutcome.Expired)]
    public void InputAgeUsesMonotonicClockWithExactExpiryBoundary(int milliseconds, InputOutcome outcome)
    {
        var (loop, clock, runtime) = Started(new() { MaxInputAge = TimeSpan.FromMilliseconds(80) });
        var task = loop.Submit(Input()); clock.ShiftUtc(TimeSpan.FromDays(-10));
        clock.Advance(TimeSpan.FromMilliseconds(milliseconds)); loop.Pump();
        Assert.Equal(outcome, Result(task).Outcome); Assert.Equal(outcome == InputOutcome.Executed ? 1 : 0, runtime.Executed.Count);
        Assert.Equal(outcome == InputOutcome.Expired ? 1 : 0, loop.Status.Expired); loop.Finish();
    }

    [Fact]
    public void UtcJumpAloneDoesNotExpireAcceptedInput()
    {
        var (loop, clock, _) = Started(); var input = loop.Submit(Input()); clock.ShiftUtc(TimeSpan.FromDays(999)); loop.Pump();
        Assert.Equal(InputOutcome.Executed, Result(input).Outcome); loop.Finish();
    }

    [Fact]
    public void CommandCountBudgetLeavesExcessPending()
    {
        var (loop, _, runtime) = Started(new() { CommandsPerPass = 2 });
        var tasks = Enumerable.Range(0, 5).Select(n => loop.Submit(Input(id: "r" + n))).ToArray();
        loop.Pump(); Assert.Equal(2, runtime.Executed.Count); Assert.False(tasks[2].IsCompleted); Assert.Equal(3, loop.Status.Queued);
        loop.Pump(); Assert.Equal(4, runtime.Executed.Count); Assert.False(tasks[4].IsCompleted);
        loop.Finish(); Assert.Equal(InputOutcome.Stopping, Result(tasks[4]).Outcome);
    }

    [Fact]
    public void TimeBudgetStopsBetweenCommandsAndRecordsExecutionCost()
    {
        var (loop, clock, runtime) = Started(new() { CommandBudget = TimeSpan.FromMilliseconds(5) });
        runtime.OnExecute = _ => clock.Advance(TimeSpan.FromMilliseconds(3));
        var tasks = Enumerable.Range(0, 3).Select(n => loop.Submit(Input(id: "r" + n))).ToArray(); loop.Pump();
        Assert.Equal(2, runtime.Executed.Count); Assert.False(tasks[2].IsCompleted); Assert.True(loop.Status.BudgetStops > 0);
        loop.Pump(); Assert.Equal(InputOutcome.Executed, Result(tasks[2]).Outcome); loop.Finish();
    }

    [Fact]
    public void DueTickPreemptsFurtherCommandsEvenWhenCommandBudgetRemains()
    {
        var (loop, clock, runtime) = Started(new() { CommandBudget = TimeSpan.FromMilliseconds(20) });
        clock.Advance(TimeSpan.FromMilliseconds(99)); runtime.OnExecute = _ => clock.Advance(TimeSpan.FromMilliseconds(2));
        var first = loop.Submit(Input(id: "first")); var second = loop.Submit(Input(id: "second")); loop.Pump();
        Assert.Equal(InputOutcome.Executed, Result(first).Outcome); Assert.False(second.IsCompleted); Assert.Single(runtime.Ticks);
        loop.Pump(); Assert.Equal(2, runtime.Ticks.Count); Assert.Equal(2, Result(second).ServerTick); loop.Finish();
    }

    [Theory]
    [InlineData("initialize", "WORLD_INITIALIZE")]
    [InlineData("tick", "WORLD_CYCLE")]
    [InlineData("observe", "WORLD_CYCLE")]
    public void StartupFailuresResolveReadyAndCompletionAndDisposeOnce(string point, string code)
    {
        var clock = new ManualClock(); var runtime = new RuntimeStub { ThrowAt = point }; var loop = new WorldLoop(() => runtime, clock: clock);
        loop.Start(); if (!loop.Completion.IsCompleted) { clock.Advance(TimeSpan.FromMilliseconds(100)); loop.Pump(); }
        Assert.Equal(WorldState.Faulted, loop.Status.State); Assert.Equal(code, loop.Status.FailureCode);
        Assert.True(loop.Ready.IsCanceled); Assert.True(loop.Completion.IsCompletedSuccessfully); Assert.Equal(1, runtime.Disposals);
        loop.Pump(); loop.Finish(); Assert.Equal(1, runtime.Disposals);
    }

    [Fact]
    public void FactoryFailureHasTerminalResultWithoutRuntime()
    {
        var loop = new WorldLoop(() => throw new IOException("test inicjalizacji")); loop.Start();
        Assert.Equal(WorldState.Faulted, loop.Status.State); Assert.Equal("WORLD_INITIALIZE", loop.Status.FailureCode);
        Assert.True(loop.Ready.IsCanceled); Assert.True(loop.Completion.IsCompletedSuccessfully);
    }

    [Fact]
    public void ExecuteFailureResolvesFailingAndEveryOtherAcceptedRequest()
    {
        var (loop, _, runtime) = Started(); runtime.ThrowAt = "execute";
        var tasks = Enumerable.Range(0, 6).Select(n => loop.Submit(Input(n % 2 == 0 ? "A" : "B", "r" + n))).ToArray();
        loop.Pump(); Assert.Equal(InputOutcome.Failed, Result(tasks[0]).Outcome);
        Assert.All(tasks.Skip(1), t => Assert.Equal(InputOutcome.Stopping, Result(t).Outcome));
        Assert.Equal(WorldState.Faulted, loop.Status.State); Assert.Equal("WORLD_CYCLE", loop.Status.FailureCode);
        Assert.Equal(6, loop.Status.Cancelled); Assert.Equal(0, loop.Status.Queued); Assert.Equal(0, loop.Status.QueuedBytes);
        Assert.True(loop.Completion.IsCompletedSuccessfully); Assert.Equal(1, runtime.Disposals);
    }

    [Fact]
    public void TickFailureAfterReadyResolvesQueuedRequests()
    {
        var (loop, clock, runtime) = Started(); var pending = loop.Submit(Input()); runtime.ThrowAt = "tick";
        clock.Advance(TimeSpan.FromMilliseconds(100)); loop.Pump();
        Assert.Equal(InputOutcome.Stopping, Result(pending).Outcome); Assert.Equal(WorldState.Faulted, Completed(loop.Completion).State);
        Assert.True(loop.Ready.IsCompletedSuccessfully); Assert.False(loop.Status.Ready);
    }

    [Fact]
    public void DisposeFailureResolvesAllRequestsAndReportsFaultedShutdown()
    {
        var (loop, _, runtime) = Started(); runtime.ThrowAt = "dispose"; var pending = loop.Submit(Input()); loop.Finish();
        Assert.Equal(InputOutcome.Stopping, Result(pending).Outcome); Assert.Equal("WORLD_SHUTDOWN", loop.Status.FailureCode);
        Assert.Equal(WorldState.Faulted, Completed(loop.Completion).State); Assert.False(loop.Status.Ready);
        loop.Finish(); Assert.Equal(1, runtime.Disposals);
    }

    [Fact]
    public void StopBeforeStartCancelsReadinessWithoutConstructingRuntime()
    {
        bool constructed = false; var loop = new WorldLoop(() => { constructed = true; return new RuntimeStub(); });
        loop.RequestStop(); loop.Start(); Assert.False(constructed); Assert.True(loop.Ready.IsCanceled);
        Assert.Equal(WorldState.Stopped, Completed(loop.Completion).State);
        Assert.Equal(InputOutcome.Stopping, Result(loop.Submit(Input())).Outcome);
    }

    [Fact]
    public void ConcurrentStopWithdrawsReadinessAndPreventsQueuedAndFutureEffects()
    {
        var (loop, clock, runtime) = Started();
        var first = loop.Submit(Input(id: "first")); var later = loop.Submit(Input(id: "later"));
        runtime.OnExecute = _ =>
        {
            var stopper = new Thread(loop.RequestStop); stopper.Start(); Assert.True(stopper.Join(TimeSpan.FromSeconds(2)));
            Assert.False(loop.Status.Ready);
        };
        loop.Pump(); Assert.Equal(InputOutcome.Executed, Result(first).Outcome);
        // Jeżeli zamknięcie następuje dopiero na kolejnym Pump, wymuszamy je na tym samym właścicielu.
        loop.Pump(); Assert.Equal(InputOutcome.Stopping, Result(later).Outcome);
        int ticks = runtime.Ticks.Count; clock.Advance(TimeSpan.FromSeconds(1)); loop.Pump(); loop.Finish();
        Assert.Equal(ticks, runtime.Ticks.Count); Assert.Single(runtime.Executed); Assert.Equal(1, runtime.Disposals);
        Assert.Equal(InputOutcome.Stopping, Result(loop.Submit(Input(id: "future"))).Outcome);
    }

    [Fact]
    public void RuntimeCallbacksAndShutdownHaveExactlyOneOwnerThread()
    {
        var (loop, _, runtime) = Started(); loop.Submit(Input()); loop.Pump();
        Exception? pumpError = null, finishError = null;
        var other = new Thread(() => { pumpError = Record.Exception(loop.Pump); finishError = Record.Exception(loop.Finish); });
        other.Start(); Assert.True(other.Join(TimeSpan.FromSeconds(2)));
        Assert.IsType<InvalidOperationException>(pumpError); Assert.IsType<InvalidOperationException>(finishError);
        Assert.Equal(WorldState.Running, loop.Status.State); loop.Finish();
        Assert.All(runtime.Threads, id => Assert.Equal(Environment.CurrentManagedThreadId, id));
        Assert.Throws<InvalidOperationException>(loop.Start);
    }

    [Fact]
    public void TickLimitStopsAtExactBoundaryEvenDuringCatchUp()
    {
        var clock = new ManualClock(); var runtime = new RuntimeStub();
        var loop = new WorldLoop(() => runtime, new() { TickLimit = 3 }, clock); loop.Start();
        clock.Advance(TimeSpan.FromSeconds(20)); loop.Pump();
        Assert.Equal(3, runtime.Ticks.Count); Assert.Equal(300, loop.Status.SimulationTimeMs);
        Assert.Equal(WorldState.Stopped, Completed(loop.Completion).State); Assert.Equal(1, runtime.Disposals);
    }

    [Fact]
    public void IndependentWorldsHaveDistinctRunEpochs()
    {
        var (a, _, _) = Started(new() { WorldId = "wspolny" }); var (b, _, _) = Started(new() { WorldId = "wspolny" });
        Assert.Equal(a.Status.WorldId, b.Status.WorldId); Assert.NotEqual(a.Status.WorldRunId, b.Status.WorldRunId);
        Assert.True(Guid.TryParse(a.Status.WorldRunId, out _)); a.Finish(); b.Finish();
    }

    [Fact]
    public void ConcurrentProducersOnlyEnqueueAndOwnerExecutesEveryAcceptedInputOnce()
    {
        var (loop, _, runtime) = Started();
        var tasks = new Task<WorldResult>[8][];
        var threads = Enumerable.Range(0, 8).Select(p => new Thread(() => tasks[p] = Enumerable.Range(0, 20)
            .Select(n => loop.Submit(Input("p" + p, "p" + p + ":" + n))).ToArray())).ToArray();
        foreach (var thread in threads) thread.Start();
        foreach (var thread in threads) Assert.True(thread.Join(TimeSpan.FromSeconds(3)));
        Assert.Empty(runtime.Executed);
        for (int pass = 0; pass < 10; pass++) loop.Pump();
        Assert.Equal(160, runtime.Executed.Count); Assert.Equal(160, runtime.Executed.Distinct().Count());
        for (int p = 0; p < 8; p++)
            Assert.Equal(Enumerable.Range(0, 20).Select(n => "p" + p + ":" + n), runtime.Executed.Where(id => id.StartsWith("p" + p + ":", StringComparison.Ordinal)));
        Assert.All(tasks.SelectMany(t => t), t => Assert.Equal(InputOutcome.Executed, Result(t).Outcome));
        loop.Finish(); Assert.All(runtime.Threads, id => Assert.Equal(Environment.CurrentManagedThreadId, id));
    }

    [Fact]
    public void StopDuringFirstTickNeverCompletesReadinessWithFalseSnapshot()
    {
        var clock = new ManualClock(); var runtime = new RuntimeStub(); var loop = new WorldLoop(() => runtime, clock: clock);
        runtime.OnTick = () => { var stopper = new Thread(loop.RequestStop); stopper.Start(); Assert.True(stopper.Join(TimeSpan.FromSeconds(2))); };
        loop.Start(); clock.Advance(TimeSpan.FromMilliseconds(100)); loop.Pump();
        Assert.True(loop.Ready.IsCanceled); Assert.False(loop.Status.Ready); Assert.Equal(WorldState.Stopped, Completed(loop.Completion).State);
    }

    internal static T Completed<T>(Task<T> task)
    {
        Assert.True(task.IsCompletedSuccessfully, "Odczytujemy wyłącznie już zakończone zadanie; brak oczekiwania na wątku właściciela.");
        return task.GetAwaiter().GetResult();
    }
}

internal sealed class ManualClock : TimeProvider
{
    long timestamp, utcTicks = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero).Ticks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => Interlocked.Read(ref timestamp);
    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref utcTicks), TimeSpan.Zero);
    public void Advance(TimeSpan elapsed) { Interlocked.Add(ref timestamp, elapsed.Ticks); Interlocked.Add(ref utcTicks, elapsed.Ticks); }
    public void ShiftUtc(TimeSpan offset) => Interlocked.Add(ref utcTicks, offset.Ticks);
}

internal sealed class RuntimeStub : IWorldRuntime
{
    internal List<double> Ticks { get; } = [];
    internal List<int> ObservedAt { get; } = [];
    internal List<string> Executed { get; } = [];
    internal List<int> Threads { get; } = [];
    internal int Disposals;
    internal string? ThrowAt;
    internal Action<WorldInput>? OnExecute;
    internal Action? OnTick;
    void Call(string point) { Threads.Add(Environment.CurrentManagedThreadId); if (ThrowAt == point) throw new IOException("Jawny stub awarii " + point); }
    public void Initialize() => Call("initialize");
    public void Tick(double seconds) { Call("tick"); Ticks.Add(seconds); OnTick?.Invoke(); }
    public WorldResult Execute(WorldInput input, long serverTick)
    {
        Call("execute"); Executed.Add(input.RequestId); OnExecute?.Invoke(input);
        return new(input.RequestId, InputOutcome.Executed, serverTick);
    }
    public WorldObservation Observe() { Call("observe"); ObservedAt.Add(Ticks.Count); return new(Ticks.Count * 0.1, 0, 0, Executed.Count); }
    public void Dispose() { Disposals++; Call("dispose"); }
}

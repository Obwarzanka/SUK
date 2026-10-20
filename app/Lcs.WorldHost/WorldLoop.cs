using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lcs.WorldHost;

/// <summary>
/// Jeden właściciel stanu i zegara. Start/Pump/Finish muszą działać na tym samym wątku.
/// Producenci używają wyłącznie Submit/RequestStop/Status. Brak callbacków odbiorcy w ticku.
/// </summary>
public sealed class WorldLoop
{
    public static readonly TimeSpan Step = TimeSpan.FromMilliseconds(100);
    readonly WorldLoopOptions options;
    readonly TimeProvider clock;
    readonly Func<IWorldRuntime> factory;
    readonly ILogger log;
    readonly object gate = new();
    readonly Dictionary<string, Queue<PendingInput>> producers = new(StringComparer.Ordinal);
    readonly Queue<string> rotation = new();
    readonly Dictionary<string, long> denials = new(StringComparer.Ordinal);
    readonly Dictionary<string, long> reportedDenials = new(StringComparer.Ordinal);
    readonly string runId = Guid.NewGuid().ToString("D");
    readonly TaskCompletionSource<WorldStatus> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly TaskCompletionSource<WorldStatus> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    WorldStatus status;
    IWorldRuntime? runtime;
    int owner, queued;
    long queuedBytes, start, tick, publications, accepted, rejected, expired, completed, cancelled;
    long maxTickUs, maxLatenessUs, budgetStops;
    WorldState state;
    bool stopRequested;
    bool initialized, pumping, applying;
    long requestedTicks;
    string? failure;
    WorldObservation? observation;

    sealed record PendingInput(WorldInput Input, long At, int Bytes, TaskCompletionSource<WorldResult> Result);

    public WorldLoop(Func<IWorldRuntime> factory, WorldLoopOptions? options = null, TimeProvider? clock = null, ILogger? logger = null)
    {
        this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        this.options = options ?? new(); this.options.Validate();
        this.clock = clock ?? TimeProvider.System; log = logger ?? NullLogger.Instance;
        status = MakeStatus();
    }

    public WorldStatus Status => Volatile.Read(ref status);
    public Task<WorldStatus> Ready => ready.Task;
    public Task<WorldStatus> Completion => completion.Task;

    public Task<WorldResult> Submit(WorldInput input)
    {
        lock (gate)
        {
            var request = input?.RequestId ?? "";
            InputOutcome? denial = !Valid(input) ? InputOutcome.Invalid :
                stopRequested || state is WorldState.Stopping or WorldState.Stopped or WorldState.Faulted ? InputOutcome.Stopping :
                state != WorldState.Running && !(options.ExternalSteps && initialized && state == WorldState.Starting) ? InputOutcome.NotReady : null;
            // Konserwatywny budżet tekstów UTF-16 i obiektów wejścia/TCS/kolejek; count ogranicza też narzut CLR.
            int bytes = denial != null ? 0 : 768 + 2 * (input!.ProducerId.Length + input.RequestId.Length + input.Text.Length);
            string? reason = denial switch { InputOutcome.Invalid => "INVALID_INPUT", InputOutcome.Stopping => "WORLD_STOPPING", InputOutcome.NotReady => "WORLD_NOT_READY", _ => null };
            if (denial == null)
            {
                reason = queued >= options.QueueCapacity ? "QUEUE_COUNT_LIMIT" : queuedBytes + bytes > options.QueueByteCapacity ? "QUEUE_BYTE_LIMIT" :
                    producers.TryGetValue(input!.ProducerId, out var q) && q.Count >= options.PerProducerCapacity ? "PRODUCER_LIMIT" : null;
                if (reason != null) denial = InputOutcome.QueueFull;
            }
            if (denial is { } d)
            {
                rejected++; CountDenial(reason!); return Task.FromResult(new WorldResult(request, d, Interlocked.Read(ref tick)));
            }
            var result = new TaskCompletionSource<WorldResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!producers.TryGetValue(input!.ProducerId, out var queue))
            {
                producers.Add(input.ProducerId, queue = new()); rotation.Enqueue(input.ProducerId);
            }
            queue.Enqueue(new(input, clock.GetTimestamp(), bytes, result));
            queued++; queuedBytes += bytes; accepted++;
            return result.Task;
        }
    }

    static bool Valid(WorldInput? i) => i != null && WorldLoopOptions.ValidId(i.ProducerId) && WorldLoopOptions.ValidId(i.RequestId) &&
        Enum.IsDefined(i.Kind) && i.Text is { Length: <= 512 } && !i.Text.Any(char.IsControl);

    public void RequestStop()
    {
        lock (gate)
        {
            stopRequested = true;
            // Gotowość znika już przy żądaniu, nie po zakończeniu trwającego kroku.
            Volatile.Write(ref status, status with { Ready = false });
        }
    }

    public void Start()
    {
        lock (gate)
        {
            if (state != WorldState.New) throw new InvalidOperationException("Świat można uruchomić tylko raz.");
            owner = Environment.CurrentManagedThreadId; state = WorldState.Starting;
            Volatile.Write(ref status, MakeStatus());
        }
        try
        {
            using var scope = Context(); WorldLog.Starting(log);
            if (IsStopping()) { Finish(); return; }
            runtime = factory(); runtime.Initialize();
            start = clock.GetTimestamp(); lock (gate) initialized = true; PublishStatus();
        }
        catch (Exception ex) { Fail(ex, "WORLD_INITIALIZE"); }
    }

    public void Pump() => PumpCore(true);

    /// <summary>Obsługa wejść lokalnego pulpitu w pauzie, również przy T=0; nie wykonuje zaległych kroków.</summary>
    public void PumpInputs()
    {
        CheckOwner();
        if (!options.ExternalSteps) throw new InvalidOperationException("Oddzielna obsługa wejść wymaga lokalnego harmonogramu.");
        PumpCore(false);
    }

    public void RequestSteps(int count)
    {
        EnsureIdle();
        if (!options.ExternalSteps || count is < 0 or > 120) throw new ArgumentOutOfRangeException(nameof(count));
        if (state is WorldState.Stopping or WorldState.Stopped or WorldState.Faulted || IsStopping()) throw new ObjectDisposedException(nameof(WorldLoop));
        requestedTicks = checked(requestedTicks + count);
    }

    internal void EnsureIdle()
    {
        CheckOwner();
        if (pumping || applying) throw new InvalidOperationException("Operacja lokalna nie może wejść ponownie w trwającą mutację świata.");
    }

    /// <summary>Zaufane operacje lokalnej sesji (logowanie, książka, konfiguracja); wykonywane na tym samym właścicielu, bez przechowywania callbacku.</summary>
    public T ApplyLocal<T>(Func<T> action)
    {
        CheckOwner();
        if (!options.ExternalSteps || !initialized || IsStopping()) throw new ObjectDisposedException(nameof(WorldLoop));
        if (pumping || applying) throw new InvalidOperationException("Operacja lokalna nie może wejść ponownie w trwającą mutację świata.");
        applying = true;
        try { using var scope = Context(); return action(); }
        finally { applying = false; }
    }

    bool StepDue() => options.ExternalSteps ? tick < requestedTicks : clock.GetElapsedTime(start) >= Step * (tick + 1);

    void PumpCore(bool allowSteps)
    {
        CheckOwner();
        if (state is WorldState.Stopped or WorldState.Faulted) return;
        if (state == WorldState.New) throw new InvalidOperationException("Najpierw uruchom świat.");
        if (pumping || applying) throw new InvalidOperationException("Cykl świata nie może wejść ponownie w trwającą mutację.");
        pumping = true;
        try
        {
            if (IsStopping()) { FinishCore(); return; }
            int steps = 0;
            while (allowSteps && !IsStopping() && StepDue() && steps++ < options.CatchUpSteps)
            {
                var before = clock.GetTimestamp();
                if (!options.ExternalSteps) maxLatenessUs = Math.Max(maxLatenessUs, (long)(clock.GetElapsedTime(start, before) - Step * (tick + 1)).TotalMicroseconds);
                // Jedyna droga zmiany czasu symulacji. Nie ma pomijania kroków ani zwiększania dt przy opóźnieniu.
                using (Context(tick + 1)) runtime!.Tick(Step.TotalSeconds);
                tick++; maxTickUs = Math.Max(maxTickUs, (long)clock.GetElapsedTime(before).TotalMicroseconds);
                if (tick % 2 == 0 || tick == 1) { observation = runtime!.Observe(); publications++; }
                bool first;
                lock (gate)
                {
                    first = state == WorldState.Starting && !stopRequested;
                    if (first) state = WorldState.Running;
                    Volatile.Write(ref status, MakeStatus());
                    if (first) ready.TrySetResult(status);
                }
                if (first) { using var scope = Context(); WorldLog.Ready(log); }
                if (tick % 50 == 0) LogMetrics();
                if (options.TickLimit is long limit && tick >= limit) RequestStop();
            }
            if (IsStopping()) { FinishCore(); return; }
            var sliceStart = clock.GetTimestamp();
            int handled = 0;
            while (handled < options.CommandsPerPass && !IsStopping())
            {
                // Synchroniczny adapter lokalny musi rozstrzygnąć własne pierwsze wejście nawet po wywłaszczeniu wątku.
                if ((handled > 0 || !options.ExternalSteps || allowSteps) && clock.GetElapsedTime(sliceStart) >= options.CommandBudget || allowSteps && StepDue())
                { budgetStops++; break; }
                var item = Take(); if (item == null) break;
                handled++;
                WorldResult result;
                if (clock.GetElapsedTime(item.At) >= options.MaxInputAge)
                { expired++; lock (gate) CountDenial("INPUT_EXPIRED"); result = new(item.Input.RequestId, InputOutcome.Expired, tick); }
                else
                {
                    using var scope = Context();
                    try { result = runtime!.Execute(item.Input, tick); completed++; }
                    catch { item.Result.TrySetResult(new(item.Input.RequestId, InputOutcome.Failed, tick)); cancelled++; throw; }
                }
                item.Result.TrySetResult(result);
            }
            PublishStatus();
        }
        catch (Exception ex) { Fail(ex, "WORLD_CYCLE"); }
        finally { pumping = false; }
    }

    PendingInput? Take()
    {
        lock (gate)
        {
            if (stopRequested || rotation.Count == 0) return null;
            string producer = rotation.Dequeue(); var q = producers[producer]; var item = q.Dequeue();
            if (q.Count == 0) producers.Remove(producer); else rotation.Enqueue(producer);
            queued--; queuedBytes -= item.Bytes; return item;
        }
    }

    public void Finish()
    {
        EnsureIdle();
        FinishCore();
    }

    void FinishCore()
    {
        if (state is WorldState.Stopped or WorldState.Faulted) return;
        RequestStop();
        lock (gate) state = WorldState.Stopping;
        PublishStatus(); using (Context()) WorldLog.Stopping(log);
        try { runtime?.Dispose(); }
        catch (Exception ex) { failure = "WORLD_SHUTDOWN"; using var scope = Context(); WorldLog.Failed(log, ex, failure); }
        lock (gate)
        {
            foreach (var item in producers.Values.SelectMany(q => q))
            { item.Result.TrySetResult(new(item.Input.RequestId, InputOutcome.Stopping, tick)); cancelled++; }
            producers.Clear(); rotation.Clear(); queued = 0; queuedBytes = 0;
            state = failure == null ? WorldState.Stopped : WorldState.Faulted;
        }
        PublishStatus(); LogMetrics();
        using (Context()) WorldLog.Stopped(log, state == WorldState.Stopped ? "Stopped" : "Failed");
        ready.TrySetCanceled(); completion.TrySetResult(Status);
    }

    void Fail(Exception ex, string code)
    {
        failure = code; using (Context()) WorldLog.Failed(log, ex, code); FinishCore();
    }

    void LogMetrics()
    {
        using var scope = Context();
        var s = Status;
        WorldLog.Metrics(log, new Dictionary<string, long> { ["accepted"] = s.Accepted, ["rejected"] = s.Rejected,
            ["expired"] = s.Expired, ["completed"] = s.Completed, ["cancelled"] = s.Cancelled, ["queued"] = s.Queued,
            ["queuedBytes"] = s.QueuedBytes, ["maxTickUs"] = s.MaxTickUs, ["maxLatenessUs"] = s.MaxLatenessUs, ["budgetStops"] = s.BudgetStops });
        KeyValuePair<string, long>[] counts;
        lock (gate) counts = denials.ToArray();
        foreach (var (reason, count) in counts)
        {
            long previous = reportedDenials.GetValueOrDefault(reason);
            if (count > previous) WorldLog.Rejected(log, reason, new Dictionary<string, long> { ["count"] = count, ["sincePrevious"] = count - previous });
            reportedDenials[reason] = count;
        }
    }

    void CountDenial(string reason) => denials[reason] = denials.GetValueOrDefault(reason) + 1;

    bool IsStopping() { lock (gate) return stopRequested; }
    void CheckOwner() { if (Environment.CurrentManagedThreadId != owner) throw new InvalidOperationException("Cykl świata ma jednego właściciela wątku."); }
    IDisposable? Context(long? currentTick = null) => log.BeginScope(new Dictionary<string, object> { ["worldId"] = options.WorldId,
        ["worldRunId"] = runId, ["serverTick"] = currentTick ?? tick, ["simulationTimeMs"] = (currentTick ?? tick) * 100 });
    WorldStatus MakeStatus() => new(state, state == WorldState.Running && !stopRequested, options.WorldId, runId,
        tick, tick * 100, publications, queued, queuedBytes, accepted, rejected, expired, completed, cancelled,
        maxTickUs, maxLatenessUs, budgetStops, observation, failure);
    void PublishStatus() { lock (gate) Volatile.Write(ref status, MakeStatus()); }
}

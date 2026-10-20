namespace Lcs.WorldHost;

public enum WorldState { New, Starting, Running, Stopping, Stopped, Faulted }
public enum InputKind { Command, Confirm, Cancel }
public enum InputOutcome { Executed, Rejected, Expired, QueueFull, Invalid, NotReady, Stopping, Failed }

/// <summary>Wyłącznie zaufane wejście lokalnego adaptera. ProducerId służy sprawiedliwości kolejki, nie uwierzytelnieniu.</summary>
public sealed record WorldInput(string ProducerId, string RequestId, InputKind Kind, string Text);
public sealed record WorldResult(string RequestId, InputOutcome Outcome, long ServerTick, string Message = "");
public sealed record WorldObservation(double SimulationSeconds, int Trains, int Routes, int Events);

/// <summary>Niemutowalna projekcja techniczna. Nie udostępnia mutable Simulator, kart, KZ ani list zdarzeń.</summary>
public sealed record WorldStatus(WorldState State, bool Ready, string WorldId, string WorldRunId,
    long ServerTick, long SimulationTimeMs, long Publications, int Queued, long QueuedBytes,
    long Accepted, long Rejected, long Expired, long Completed, long Cancelled,
    long MaxTickUs, long MaxLatenessUs, long BudgetStops, WorldObservation? Observation, string? FailureCode);

public sealed record WorldLoopOptions
{
    /// <summary>Lokalny właściciel zleca kroki (pulpit/pauza/testy). Limity kolejki nadal używają rzeczywistego zegara monotonicznego.</summary>
    public bool ExternalSteps { get; init; }
    public string WorldId { get; init; } = "local";
    public int QueueCapacity { get; init; } = 1024;
    public long QueueByteCapacity { get; init; } = 1024 * 1024;
    public int PerProducerCapacity { get; init; } = 64;
    public int CommandsPerPass { get; init; } = 16;
    public TimeSpan CommandBudget { get; init; } = TimeSpan.FromMilliseconds(2);
    public TimeSpan MaxInputAge { get; init; } = TimeSpan.FromSeconds(2);
    public int CatchUpSteps { get; init; } = 8;
    public long? TickLimit { get; init; }

    internal void Validate()
    {
        if (!ValidId(WorldId) || QueueCapacity is < 1 or > 65536 || QueueByteCapacity is < 1024 or > 67108864 ||
            PerProducerCapacity < 1 || PerProducerCapacity > QueueCapacity || CommandsPerPass is < 1 or > 1024 ||
            CommandBudget <= TimeSpan.Zero || CommandBudget > TimeSpan.FromMilliseconds(20) ||
            MaxInputAge <= TimeSpan.Zero || MaxInputAge > TimeSpan.FromMinutes(1) || CatchUpSteps is < 1 or > 64 || TickLimit is <= 0)
            throw new ArgumentException("Niepoprawne limity cyklu świata.");
    }

    internal static bool ValidId(string? value) => value is { Length: > 0 and <= 64 } &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or ':');
}

/// <summary>Wszystkie metody wywołuje jeden właściciel. Implementacja nie czeka na UI, sieć ani dysk.</summary>
public interface IWorldRuntime : IDisposable
{
    void Initialize();
    void Tick(double seconds);
    WorldResult Execute(WorldInput input, long serverTick);
    WorldObservation Observe();
}

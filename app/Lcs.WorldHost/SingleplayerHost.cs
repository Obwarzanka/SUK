using LcsWidzew.Model;
using LcsWidzew.Sim;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lcs.WorldHost;

/// <summary>Adapter in-process: Dispatcher pozostaje właścicielem istniejących okien i rdzenia, WorldLoop jedynym wykonawcą Tick.</summary>
public sealed class SingleplayerHost : IDisposable
{
    readonly int owner = Environment.CurrentManagedThreadId;
    readonly TimeProvider clock;
    readonly WorldLoop loop;
    readonly LocalRuntime runtime;
    readonly ILogger logger;
    long lastFrame, request;
    double accumulator;
    bool disposed;

    public SingleplayerHost(Station station, string scenario, User? user = null, string? page = null,
        string deskName = "Stanowisko", SimulatorOptions? options = null, TimeProvider? clock = null,
        ILogger? logger = null, ILogger? worldLogger = null)
    {
        this.clock = clock ?? TimeProvider.System; this.logger = logger ?? NullLogger.Instance;
        SingleplayerLog.Register();
        runtime = new LocalRuntime(station, scenario, user, page, deskName, options);
        loop = new WorldLoop(() => runtime, new() { ExternalSteps = true, CatchUpSteps = 64 }, this.clock, worldLogger);
        loop.Start();
        CheckWorld();
        Session = new HostedSession(this, runtime.Session!);
        lastFrame = this.clock.GetTimestamp();
        using var scope = Context(); SingleplayerLog.Started(this.logger);
    }

    public ILcsSession Session { get; }
    public WorldStatus Status => loop.Status;
    public bool IsDisposed => disposed;

    public void Frame(bool paused, double speed)
    {
        Check(); loop.EnsureIdle();
        if (!double.IsFinite(speed) || speed is < 0 or > 10) throw new ArgumentOutOfRangeException(nameof(speed));
        long now = clock.GetTimestamp();
        double elapsed = Math.Clamp(clock.GetElapsedTime(lastFrame, now).TotalSeconds, 0, 0.25); lastFrame = now;
        if (!paused && speed > 0) accumulator += elapsed * speed;
        int steps = 0;
        while (accumulator >= 0.1 && steps < 120) { accumulator -= 0.1; steps++; }
        loop.RequestSteps(steps); loop.Pump(); CheckWorld();
    }

    /// <summary>Deterministyczny krok dla zrzutów i scenariuszy, tą samą ścieżką WorldLoop co zwykłe granie.</summary>
    public void Step()
    {
        Check(); loop.EnsureIdle(); loop.RequestSteps(1); loop.Pump(); lastFrame = clock.GetTimestamp(); CheckWorld();
    }

    public void AdvanceTo(double seconds, Action? afterStep = null)
    {
        Check();
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        while (runtime.Session!.Sim.T < seconds) { Step(); afterStep?.Invoke(); }
    }

    internal void Check()
    {
        if (Environment.CurrentManagedThreadId != owner) throw new InvalidOperationException("Lokalny świat ma jednego właściciela wątku.");
        if (disposed) throw new ObjectDisposedException(nameof(SingleplayerHost));
    }

    internal T Apply<T>(string operation, Func<T> action)
    {
        Check();
        using var scope = Context(); SingleplayerLog.Operation(logger, operation);
        return loop.ApplyLocal(action);
    }
    internal void Apply(string operation, Action action) => Apply(operation, () => { action(); return true; });

    internal CmdResult Command(InputKind kind, string text)
    {
        Check(); loop.EnsureIdle();
        string id = "local-" + ++request;
        using var scope = Context();
        using var correlation = logger.BeginScope(new Dictionary<string, object> { ["requestId"] = id, ["correlationId"] = Status.WorldRunId + "-" + id });
        SingleplayerLog.Operation(logger, kind.ToString());
        var task = loop.Submit(new("pulpit", id, kind, text));
        loop.PumpInputs(); CheckWorld();
        if (!task.IsCompleted) throw new InvalidOperationException("Lokalne polecenie nie otrzymało wyniku w swoim przebiegu.");
        var result = task.GetAwaiter().GetResult();
        return new(result.Outcome == InputOutcome.Executed, result.Outcome is InputOutcome.Executed or InputOutcome.Rejected ? result.Message :
            "Polecenie lokalne odrzucone: " + result.Outcome);
    }

    void CheckWorld()
    {
        if (loop.Status.State != WorldState.Faulted) return;
        var error = new InvalidOperationException("Lokalny świat przerwany: " + loop.Status.FailureCode);
        using var scope = Context(); SingleplayerLog.Failed(logger, error, "LOCAL_WORLD_FAILED"); throw error;
    }
    IDisposable? Context() => logger.BeginScope(new Dictionary<string, object> { ["worldId"] = Status.WorldId,
        ["worldRunId"] = Status.WorldRunId, ["serverTick"] = Status.ServerTick, ["simulationTimeMs"] = Status.SimulationTimeMs });

    public void Dispose()
    {
        if (disposed) return;
        Check(); loop.Finish(); disposed = true;
        using var scope = Context(); SingleplayerLog.Stopped(logger);
    }

    sealed class LocalRuntime(Station station, string scenario, User? user, string? page, string deskName, SimulatorOptions? options) : IWorldRuntime
    {
        public LocalSession? Session { get; private set; }
        public void Initialize()
        {
            if (!Rozklad.Scenarios.Any(s => s.Key == scenario)) throw new ArgumentException("Nieznany scenariusz.");
            var sim = new Simulator(station, options: options);
            if (page != null && page != "OV") sim.Page = page;
            var trains = new Trains(sim); _ = Sygnaly.Tab; _ = trains.Tabor; trains.Load(scenario);
            // S02A.01: mapa własności postId/kzId (tylko odczyt, log KZ.OWNERSHIP_* gdy pulpit podał logger); bez konsumenta w cyklu.
            if (options?.MapaWlasnosciLogger != null) sim.OpublikujWlasnosc();
            // S02A.02: tabela rozsyłania wejść KZ (log KZ.CYCLE_INPUT_ROUTING_READY, gdy pulpit podał logger).
            if (options?.WejscieCykluLogger != null) _ = sim.Wejscia.Trasa;
            var desk = sim.AddDesk("PC", deskName, true); desk.Page = sim.Page; desk.User = user; sim.User = user;
            Session = new LocalSession(sim, desk);
        }
        public void Tick(double seconds) => Session!.Sim.Tick(seconds);
        public WorldResult Execute(WorldInput input, long tick)
        {
            CmdResult result;
            if (input.Kind == InputKind.Cancel) { Session!.Cancel(); result = new(true, ""); }
            else result = input.Kind == InputKind.Confirm ? Session!.Confirm(input.Text) : Session!.Exec(input.Text);
            return new(input.RequestId, result.Ok ? InputOutcome.Executed : InputOutcome.Rejected, tick, result.Msg ?? "");
        }
        public WorldObservation Observe() { var s = Session!.Sim; return new(s.T, s.Trains.List.Count, s.Routes.Count, s.Events.Count); }
        // S02A.02: podsumowanie naruszeń wejść KZ bez własnego rekordu (4116), jeśli były.
        public void Dispose() => Session?.Sim.Wejscia.PodsumujNaKoniec();
    }

    sealed class HostedSession(SingleplayerHost host, LocalSession session) : ILcsSession
    {
        T Read<T>(Func<T> read) { host.Check(); return read(); }
        public bool IsHost => true;
        // Przejściowy dostęp zaufanych okien lokalnych, wyłącznie na właścicielu. Nie stanowi API zdalnego.
        public Simulator Sim => Read(() => session.Sim);
        public string DeskId => Read(() => session.DeskId);
        public string DeskName => Read(() => session.DeskName);
        public Station L => Read(() => session.L);
        public Dictionary<string, List<RouteDef>> RouteTable => Read(() => session.RouteTable);
        public Indication Last => Read(() => session.Last);
        public Indication Receive() => Read(session.Receive);
        public List<EventRecord> Events => Read(() => session.Events);
        public List<EventRecord> Alarms => Read(() => session.Alarms);
        public event Action<EventRecord> EventAdded { add { host.Check(); session.EventAdded += value; } remove { session.EventAdded -= value; } }
        public event Action<EventRecord> AlarmAdded { add { host.Check(); session.AlarmAdded += value; } remove { session.AlarmAdded -= value; } }
        public event Action AlarmsChanged { add { host.Check(); session.AlarmsChanged += value; } remove { session.AlarmsChanged -= value; } }
        public event Action Beep { add { host.Check(); session.Beep += value; } remove { session.Beep -= value; } }
        public event Action SpecialExecuted { add { host.Check(); session.SpecialExecuted += value; } remove { session.SpecialExecuted -= value; } }
        public event Action PendingExpired { add { host.Check(); session.PendingExpired += value; } remove { session.PendingExpired -= value; } }
        public event Action<string> Disconnected { add { } remove { } }
        public event Action Reset { add { } remove { } }
        public CmdResult Exec(string line) => host.Command(InputKind.Command, line);
        public CmdResult Confirm(string typed) => host.Command(InputKind.Confirm, typed);
        public void Cancel() => host.Command(InputKind.Cancel, "");
        public void AckAlarm(long id) => host.Apply("AckAlarm", () => session.AckAlarm(id));
        public void Login(User u) => host.Apply("Login", () => session.Login(u));
        public void SetPage(string page) => host.Apply("SetPage", () => session.SetPage(page));
        public string RequestControl(string post) => host.Apply("RequestControl", () => session.RequestControl(post));
        public void ReleaseControl(string post) => host.Apply("ReleaseControl", () => session.ReleaseControl(post));
        public void AnswerControl(string post, bool yes) => host.Apply("AnswerControl", () => session.AnswerControl(post, yes));
        public void Phone(string toDesk, string text) => host.Apply("Phone", () => session.Phone(toDesk, text));
        public List<PlanRow> GetPlan() => Read(session.GetPlan);
        public ScoreData GetScore() => Read(session.GetScore);
        public BookData GetBook() => Read(session.GetBook);
        public void AddBook(string text, string auto) => host.Apply("AddBook", () => session.AddBook(text, auto));
    }
}

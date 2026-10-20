using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lcs.Logi;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Lcs.WorldHost.Testy;

/// <summary>Rzeczywisty lokalny NamedPipe. Bez rejestracji SCM, praw administratora i komend do świata.</summary>
[SupportedOSPlatform("windows")]
public sealed class ServicePipeTests
{
    static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    static ServiceProfile Profile()
    {
        string world = "ipc_" + Guid.NewGuid().ToString("N")[..12];
        return new() { AccountSid = WindowsIdentity.GetCurrent().User!.Value, WorldId = world,
            PipeName = $"SUK.WorldHost.{world}.health", DataRoot = Path.Combine(Path.GetTempPath(), world) };
    }

    static (WorldLoop Loop, ManualClock Clock, ServiceHealth Health) World(ServiceProfile profile)
    {
        var clock = new ManualClock(); var loop = new WorldLoop(() => new RuntimeStub(), new() { WorldId = profile.WorldId }, clock);
        var health = new ServiceHealth(loop, profile, workingSet: () => 1024, clock: clock);
        loop.Start(); clock.Advance(TimeSpan.FromMilliseconds(100)); loop.Pump(); return (loop, clock, health);
    }

    static void Stop(HealthPipe pipe) => pipe.StopAsync(TestContext.Current.CancellationToken).GetAwaiter().GetResult();
    static ServiceHealthStatus Query(ServiceProfile profile, CancellationToken token) => HealthPipe.Query(profile, token).GetAwaiter().GetResult();
    // Keep the WorldLoop owner on this thread; pipe tasks use their own worker and finite protocol timeouts.
    static void OnOwner(Task task) => task.GetAwaiter().GetResult();
    static T OnOwner<T>(Task<T> task) => task.GetAwaiter().GetResult();

    [Fact]
    public void SecurityAllowsOnlyNamedLocalReadersAndDeniesNetworkTokens()
    {
        if (!OperatingSystem.IsWindows()) return;
        var profile = Profile() with { HealthClientSid = "S-1-5-21-1-2-3-5678" };
        var security = HealthPipe.Security(profile); Assert.True(security.AreAccessRulesProtected);
        Assert.Equal(profile.AccountSid, security.GetOwner(typeof(SecurityIdentifier))?.Value);
        var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().ToArray();
        Assert.Contains(rules, r => r.IdentityReference.Value == "S-1-5-2" && r.AccessControlType == AccessControlType.Deny && r.PipeAccessRights == PipeAccessRights.FullControl);
        foreach (string full in new[] { profile.AccountSid, "S-1-5-18" })
            Assert.Contains(rules, r => r.IdentityReference.Value == full && r.AccessControlType == AccessControlType.Allow && r.PipeAccessRights == PipeAccessRights.FullControl);
        foreach (string reader in new[] { "S-1-5-32-544", profile.HealthClientSid })
        {
            var rule = Assert.Single(rules, r => r.IdentityReference.Value == reader && r.AccessControlType == AccessControlType.Allow);
            Assert.Equal(PipeAccessRights.Read | PipeAccessRights.Synchronize, rule.PipeAccessRights);
            Assert.Equal((PipeAccessRights)0, rule.PipeAccessRights & (PipeAccessRights.Write | PipeAccessRights.CreateNewInstance | PipeAccessRights.ChangePermissions | PipeAccessRights.TakeOwnership));
        }
        Assert.DoesNotContain(rules, r => r.AccessControlType == AccessControlType.Allow && r.IdentityReference.Value is "S-1-1-0" or "S-1-5-11" or "S-1-5-32-545");
        Assert.Equal(8192, HealthPipe.MaxResponseBytes); Assert.Equal(250, HealthPipe.WriteTimeoutMs);
    }

    [Fact]
    public void RealQueryAuthenticatesServerIdentityAndReturnsCurrentWorldEpoch()
    {
        if (!OperatingSystem.IsWindows()) return;
        var profile = Profile(); var (loop, _, health) = World(profile); using var lifetime = new Lifetime();
        using var pipe = new HealthPipe(profile, health, loop, NullLogger.Instance, lifetime);
        try
        {
            OnOwner(pipe.StartAsync(TestContext.Current.CancellationToken)); Assert.True(pipe.Listening.IsCompletedSuccessfully);
            var answer = Query(profile, TestContext.Current.CancellationToken);
            Assert.True(answer.Live); Assert.True(answer.Ready); Assert.True(answer.Fresh);
            Assert.Equal(Environment.ProcessId, answer.ProcessId); Assert.Equal(loop.Status.WorldRunId, answer.WorldRunId);
            Assert.Equal(loop.Status.WorldId, answer.WorldId); Assert.Equal(1, answer.ServerTick); Assert.Equal(100, answer.SimulationTimeMs);
            Assert.Equal("READY", answer.ReasonCode); Assert.Equal(0, lifetime.Stops);
            health.Stopping(); Assert.False(Query(profile, TestContext.Current.CancellationToken).Ready);
        }
        finally { Stop(pipe); loop.Finish(); }
    }

    [Fact]
    public void LivePipeReportsStaleWorldAndRecoversOnlyAfterActualCompletedTick()
    {
        if (!OperatingSystem.IsWindows()) return;
        var profile = Profile(); var (loop, clock, health) = World(profile); using var lifetime = new Lifetime();
        using var pipe = new HealthPipe(profile, health, loop, NullLogger.Instance, lifetime);
        try
        {
            OnOwner(pipe.StartAsync(TestContext.Current.CancellationToken)); clock.Advance(TimeSpan.FromSeconds(2));
            var stale = Query(profile, TestContext.Current.CancellationToken);
            Assert.True(stale.Live); Assert.False(stale.Ready); Assert.False(stale.Fresh); Assert.Equal("WORLD_STALE", stale.ReasonCode);
            Assert.Equal(1, stale.ServerTick); loop.Pump(); var fresh = Query(profile, TestContext.Current.CancellationToken);
            Assert.True(fresh.Ready); Assert.True(fresh.ServerTick > stale.ServerTick); Assert.Equal(stale.WorldRunId, fresh.WorldRunId);
        }
        finally { Stop(pipe); loop.Finish(); }
    }

    [Fact]
    public void NonReadingClientAndConcurrentQueryFloodDoNotStopWorldOwner()
    {
        if (!OperatingSystem.IsWindows()) return;
        var profile = Profile(); var (loop, clock, health) = World(profile); using var lifetime = new Lifetime();
        using var pipe = new HealthPipe(profile, health, loop, NullLogger.Instance, lifetime);
        using var nonReading = new NamedPipeClientStream(".", profile.PipeName, PipeDirection.In, PipeOptions.Asynchronous);
        CancellationToken token = TestContext.Current.CancellationToken;
        try
        {
            OnOwner(pipe.StartAsync(token)); OnOwner(nonReading.ConnectAsync(token));
            var clients = Task.Run(async () => await Task.WhenAll(Enumerable.Range(0, 24).Select(async _ =>
            {
                try { return await HealthPipe.Query(profile, token); }
                catch (OperationCanceledException) when (!token.IsCancellationRequested) { return null; }
                catch (IOException) { return null; }
            })), token);
            for (int n = 0; n < 100; n++) { clock.Advance(TimeSpan.FromMilliseconds(100)); loop.Pump(); }
            var replies = OnOwner(clients.WaitAsync(TimeSpan.FromSeconds(5), token));
            Assert.Equal(24, replies.Length);
            Assert.All(replies.Where(r => r != null), r => { Assert.True(r!.Live); Assert.Equal(loop.Status.WorldRunId, r.WorldRunId); });
            // Excess clients may time out at the one-peer admission boundary. The endpoint must recover afterward.
            var recovered = Query(profile, token); Assert.True(recovered.Ready);
            string evidence = TestArtifacts.Directory("service-pipe-flood");
            File.WriteAllText(Path.Combine(evidence, "wynik.json"), JsonSerializer.Serialize(new
            { attempts = replies.Length, replies = replies.Count(r => r != null), boundedFailures = replies.Count(r => r == null), ticks = loop.Status.ServerTick, recovered.Ready }));
            Assert.Equal(101, loop.Status.ServerTick); Assert.True(loop.Status.Ready); Assert.Equal(0, lifetime.Stops);
            Assert.Null(health.FailureCode);
        }
        finally { Stop(pipe); loop.Finish(); }
    }

    [Fact]
    public void SecondServerCannotTakeOverExistingPipeOrStopFirstWorld()
    {
        if (!OperatingSystem.IsWindows()) return;
        var profile = Profile(); var (first, _, firstHealth) = World(profile); var (second, _, secondHealth) = World(profile);
        using var firstLifetime = new Lifetime(); using var secondLifetime = new Lifetime();
        using var firstPipe = new HealthPipe(profile, firstHealth, first, NullLogger.Instance, firstLifetime);
        using var secondPipe = new HealthPipe(profile, secondHealth, second, NullLogger.Instance, secondLifetime);
        try
        {
            OnOwner(firstPipe.StartAsync(TestContext.Current.CancellationToken));
            Assert.ThrowsAny<Exception>(() => OnOwner(secondPipe.StartAsync(TestContext.Current.CancellationToken)));
            Assert.Equal("HEALTH_PIPE_FAILED", secondHealth.FailureCode); Assert.False(secondHealth.Read().Ready);
            Assert.True(Query(profile, TestContext.Current.CancellationToken).Ready); Assert.True(first.Status.Ready); Assert.Equal(0, firstLifetime.Stops);
        }
        finally { Stop(secondPipe); Stop(firstPipe); second.Finish(); first.Finish(); }
    }

    [Fact]
    public void MissingAndUnresponsivePipesRespectCancellationWithoutWaitingForWorld()
    {
        if (!OperatingSystem.IsWindows()) return;
        var profile = Profile();
        using (var missing = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken))
        {
            missing.CancelAfter(100); var elapsed = Stopwatch.StartNew();
            Assert.ThrowsAny<OperationCanceledException>(() => Query(profile, missing.Token)); Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(3));
        }
        using var server = Server(profile); using var token = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        token.CancelAfter(500);
        var connected = server.WaitForConnectionAsync(token.Token);
        Assert.ThrowsAny<OperationCanceledException>(() => Query(profile, token.Token));
        Assert.True(connected.IsCompletedSuccessfully);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(8193)]
    public void InvalidLengthPrefixIsRejectedBeforeAllocatingOrReadingPayload(int count)
    {
        if (!OperatingSystem.IsWindows()) return;
        var profile = Profile(); byte[] frame = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(frame, count);
        Assert.IsType<InvalidDataException>(FakeResponse(profile, profile, frame));
    }

    [Theory]
    [InlineData("pid")]
    [InlineData("schema")]
    [InlineData("world")]
    [InlineData("epoch")]
    [InlineData("simulation-clock")]
    [InlineData("negative-tick")]
    [InlineData("not-live")]
    [InlineData("not-fresh")]
    [InlineData("memory")]
    [InlineData("diagnostics")]
    [InlineData("reason")]
    [InlineData("state")]
    [InlineData("missing-progress")]
    [InlineData("negative-progress")]
    public void ClaimedReadyCannotBypassIdentityEpochOrHealthInvariants(string problem)
    {
        if (!OperatingSystem.IsWindows()) return;
        var profile = Profile(); var valid = Answer(profile);
        var invalid = problem switch
        {
            "pid" => valid with { ProcessId = Environment.ProcessId + 1 }, "schema" => valid with { SchemaVersion = 2 },
            "world" => valid with { WorldId = "obcy-swiat" }, "epoch" => valid with { WorldRunId = "nie-guid" },
            "simulation-clock" => valid with { SimulationTimeMs = 999 }, "negative-tick" => valid with { ServerTick = -1, SimulationTimeMs = -100 },
            "not-live" => valid with { Live = false }, "not-fresh" => valid with { Fresh = false }, "memory" => valid with { ResourcesHealthy = false },
            "diagnostics" => valid with { DiagnosticsHealthy = false }, "reason" => valid with { ReasonCode = "STARTING" },
            "state" => valid with { State = WorldState.Starting }, "missing-progress" => valid with { ProgressAgeMs = null },
            "negative-progress" => valid with { ProgressAgeMs = -100 }, _ => throw new ArgumentOutOfRangeException(nameof(problem))
        };
        Assert.IsType<InvalidDataException>(FakeResponse(profile, profile, Frame(invalid)));
    }

    [Fact]
    public void ClientRejectsWrongPipeOwnerWithoutCreatingPipeAsAnotherAccount()
    {
        if (!OperatingSystem.IsWindows()) return;
        var actual = Profile(); var expected = actual with { AccountSid = "S-1-5-21-1-2-3-9876" };
        Assert.IsType<UnauthorizedAccessException>(FakeResponse(actual, expected, Frame(Answer(actual))));
    }

    [Fact]
    public void ClientAddsRoundTripTimeBeforeDecidingFreshness()
    {
        if (!OperatingSystem.IsWindows()) return;
        var profile = Profile(); var answer = Answer(profile) with { ProgressAgeMs = profile.FreshnessSeconds * 1000L - 1 };
        ServiceHealthStatus? received = null;
        Assert.Null(FakeResponse(profile, profile, Frame(answer), value => received = value));
        Assert.NotNull(received); Assert.True(received.Live); Assert.False(received.Ready); Assert.False(received.Fresh);
        Assert.Equal("WORLD_STALE", received.ReasonCode); Assert.True(received.ProgressAgeMs >= profile.FreshnessSeconds * 1000L);
    }

    [Fact]
    public void ServiceModuleProducesCorrelatedRedactedLifecycleAndPipeMetrics()
    {
        if (!OperatingSystem.IsWindows()) return;
        WorldLog.Register(); ServiceLog.Register(); string folder = TestArtifacts.Directory("service-L01");
        using var logs = SilnikLogow.Uruchom(new() { Aplikacja = "Lcs.WorldHost", Katalog = folder, Profil = ProfilLogow.Serwer });
        ILogger log = logs.Modul(ServiceLog.Module, WorldLog.Version);
        var profile = Profile(); var (loop, _, health) = World(profile); using var lifetime = new Lifetime();
        using var pipe = new HealthPipe(profile, health, loop, log, lifetime);
        try
        {
            using (ServiceLog.Context(log, loop.Status))
            {
                ServiceLog.ProfileReady(log); ServiceLog.HealthChanged(log, "READY");
                ServiceLog.Failed(log, new IOException("password=" + ProbaLogow.ZnacznikSekretu + ";\r\n{\"wstrzykniete\":1}"), "TEST_FAILURE");
                ProbaLogow.Wykonaj(log);
            }
            OnOwner(pipe.StartAsync(TestContext.Current.CancellationToken));
            Assert.True(Query(profile, TestContext.Current.CancellationToken).Ready);
        }
        finally { Stop(pipe); loop.Finish(); }
        var state = logs.Zatrzymaj(); File.WriteAllText(Path.Combine(folder, "stan.txt"), state.ToString());
        var records = TestArtifacts.Records(folder); string all = string.Join('\n', records.Select(r => r.GetRawText()));
        Assert.DoesNotContain(ProbaLogow.ZnacznikSekretu, all, StringComparison.Ordinal);
        Assert.DoesNotContain(records, r => r.TryGetProperty("wstrzykniete", out _) || r.TryGetProperty("password", out _));
        var service = records.Where(r => r.TryGetProperty("moduleId", out var module) && module.GetString() == ServiceLog.Module).ToArray();
        foreach (string code in new[] { "WORLD.SERVICE_PROFILE_READY", "WORLD.SERVICE_HEALTH_CHANGED", "WORLD.SERVICE_FAILED", "WORLD.SERVICE_PIPE_METRICS", "WORLD.SERVICE_PIPE_STOPPED" })
            Assert.Contains(service, r => r.GetProperty("eventCode").GetString() == code);
        Assert.All(service, record => { Assert.Equal(loop.Status.WorldRunId, record.GetProperty("worldRunId").GetString()); Assert.Equal(profile.WorldId, record.GetProperty("worldId").GetString()); });
        Assert.True(state.ZamkniecieKompletne); Assert.Equal(0, state.BledyZapisu);
    }

    static ServiceHealthStatus Answer(ServiceProfile profile) => new(1, Environment.ProcessId, profile.WorldId, Guid.NewGuid().ToString("D"),
        WorldState.Running, true, true, true, 0, 1, 100, true, true, "READY");

    static byte[] Frame(ServiceHealthStatus answer)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(answer, Json); byte[] frame = new byte[4 + body.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame, body.Length); body.CopyTo(frame, 4); return frame;
    }

    static NamedPipeServerStream Server(ServiceProfile profile) => NamedPipeServerStreamAcl.Create(profile.PipeName, PipeDirection.Out, 1,
        PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance, 0, 4096, HealthPipe.Security(profile));

    static Exception? FakeResponse(ServiceProfile actual, ServiceProfile expected, byte[] frame, Action<ServiceHealthStatus>? accepted = null)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); cancellation.CancelAfter(TimeSpan.FromSeconds(3));
        using var server = Server(actual);
        var response = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync(cancellation.Token);
            try { await server.WriteAsync(frame, cancellation.Token); }
            catch (Exception error) when (error is IOException or ObjectDisposedException) { /* Klient odrzucił tożsamość przed odczytem ramki. */ }
        }, cancellation.Token);
        Exception? error = Record.Exception(() => { var answer = Query(expected, cancellation.Token); accepted?.Invoke(answer); });
        server.Dispose();
        try { response.GetAwaiter().GetResult(); }
        catch (Exception responseError) when (responseError is IOException or ObjectDisposedException) { }
        return error;
    }

    sealed class Lifetime : IHostApplicationLifetime, IDisposable
    {
        readonly CancellationTokenSource started = new(), stopping = new(), stopped = new();
        int stops;
        public int Stops => Volatile.Read(ref stops);
        public CancellationToken ApplicationStarted => started.Token;
        public CancellationToken ApplicationStopping => stopping.Token;
        public CancellationToken ApplicationStopped => stopped.Token;
        public void StopApplication() { Interlocked.Increment(ref stops); stopping.Cancel(); }
        public void Dispose() { started.Dispose(); stopping.Dispose(); stopped.Dispose(); }
    }
}

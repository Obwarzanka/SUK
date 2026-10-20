using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lcs.WorldHost;

/// <summary>Jednokierunkowy lokalny IPC tylko do zdrowia procesu. Żaden odbiorca nie wywołuje kodu świata.</summary>
[SupportedOSPlatform("windows")]
public sealed class HealthPipe : BackgroundService
{
    public const int MaxResponseBytes = 8192;
    public const int WriteTimeoutMs = 250;
    static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };
    readonly ServiceProfile profile;
    readonly ServiceHealth health;
    readonly WorldLoop loop;
    readonly ILogger log;
    readonly IHostApplicationLifetime lifetime;
    readonly TaskCompletionSource listening = new(TaskCreationOptions.RunContinuationsAsynchronously);
    long clients, failed;
    public Task Listening => listening.Task;

    public HealthPipe(ServiceProfile profile, ServiceHealth health, WorldLoop loop, ILogger logger, IHostApplicationLifetime lifetime)
    { this.profile = profile; this.health = health; this.loop = loop; log = logger; this.lifetime = lifetime; }

    public static PipeSecurity Security(ServiceProfile profile)
    {
        var acl = new PipeSecurity(); acl.SetAccessRuleProtection(true, false);
        acl.SetOwner(new SecurityIdentifier(profile.AccountSid));
        // NETWORK: odrzucenie zdalnego tokenu również gdy jego użytkownik ma dopuszczony SID.
        acl.AddAccessRule(new PipeAccessRule(new SecurityIdentifier("S-1-5-2"), PipeAccessRights.FullControl, AccessControlType.Deny));
        foreach (string sid in new[] { profile.AccountSid, "S-1-5-18" })
            acl.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(sid), PipeAccessRights.FullControl, AccessControlType.Allow));
        foreach (string sid in new[] { "S-1-5-32-544", profile.HealthClientSid }.Where(s => s != null).Distinct()!)
            acl.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(sid), PipeAccessRights.Read | PipeAccessRights.Synchronize, AccessControlType.Allow));
        return acl;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken);
        await listening.Task.WaitAsync(TimeSpan.FromSeconds(3), cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Najwyżej jeden obsługiwany klient, 4 KiB bufora systemowego, bez wejścia/ramki od klienta.
                using var pipe = NamedPipeServerStreamAcl.Create(profile.PipeName, PipeDirection.Out, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance, 0, 4096, Security(profile));
                listening.TrySetResult();
                await pipe.WaitForConnectionAsync(stoppingToken);
                Interlocked.Increment(ref clients);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeout.CancelAfter(WriteTimeoutMs);
                try
                {
                    byte[] body = JsonSerializer.SerializeToUtf8Bytes(health.Read(), Json);
                    if (body.Length > MaxResponseBytes) throw new InvalidOperationException("Przekroczono limit odpowiedzi zdrowia.");
                    byte[] frame = new byte[body.Length + 4]; BinaryPrimitives.WriteInt32LittleEndian(frame, body.Length); body.CopyTo(frame, 4);
                    await pipe.WriteAsync(frame, timeout.Token);
                    // Bez WaitForPipeDrain/FlushFileBuffers: odbiorca nie może wstrzymać workera ani ticku.
                }
                catch (Exception ex) when (ex is IOException or OperationCanceledException)
                { Interlocked.Increment(ref failed); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            health.Fail("HEALTH_PIPE_FAILED"); listening.TrySetException(ex);
            using var scope = ServiceLog.Context(log, loop.Status); ServiceLog.Failed(log, ex, "HEALTH_PIPE_FAILED");
            lifetime.StopApplication();
        }
        finally
        {
            using var scope = ServiceLog.Context(log, loop.Status);
            ServiceLog.PipeMetrics(log, new Dictionary<string, long> { ["clients"] = Interlocked.Read(ref clients), ["failedWrites"] = Interlocked.Read(ref failed) });
            ServiceLog.PipeStopped(log);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint serverProcessId);

    public static async Task<ServiceHealthStatus> Query(ServiceProfile profile, CancellationToken cancellationToken = default)
    {
        string pipeName = profile.PipeName;
        if (!ServiceProfile.ValidPipeName(pipeName)) throw new ArgumentException("Niepoprawna nazwa kanału zdrowia.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(1000);
        long started = Stopwatch.GetTimestamp();
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.In, PipeOptions.Asynchronous,
            TokenImpersonationLevel.Anonymous);
        await pipe.ConnectAsync(timeout.Token);
        if (pipe.GetAccessControl().GetOwner(typeof(SecurityIdentifier))?.Value != profile.AccountSid ||
            !GetNamedPipeServerProcessId(pipe.SafePipeHandle, out uint serverPid))
            throw new UnauthorizedAccessException("Tożsamość serwera kanału zdrowia jest niezgodna z profilem.");
        byte[] size = new byte[4]; await pipe.ReadExactlyAsync(size, timeout.Token);
        int count = BinaryPrimitives.ReadInt32LittleEndian(size);
        if (count is < 1 or > MaxResponseBytes) throw new InvalidDataException("Niepoprawny rozmiar odpowiedzi zdrowia.");
        byte[] body = new byte[count]; await pipe.ReadExactlyAsync(body, timeout.Token);
        var result = JsonSerializer.Deserialize<ServiceHealthStatus>(body, Json) ?? throw new InvalidDataException("Brak odpowiedzi zdrowia.");
        if (result.SchemaVersion != 1 || result.ProcessId != serverPid || result.WorldId != profile.WorldId || !Guid.TryParse(result.WorldRunId, out _) || result.ServerTick < 0 ||
            result.ProgressAgeMs is < 0 || result.Fresh && (result.ProgressAgeMs == null || result.ProgressAgeMs >= profile.FreshnessSeconds * 1000L) ||
            result.SimulationTimeMs != result.ServerTick * 100 || result.Ready && (!result.Live || !result.Fresh ||
            !result.ResourcesHealthy || !result.DiagnosticsHealthy || result.ReasonCode != "READY" || result.State != WorldState.Running))
            throw new InvalidDataException("Niespójna odpowiedź zdrowia.");
        if (result.ProgressAgeMs is long age)
        {
            long conservativeAge = age + (long)Math.Ceiling(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            result = result with { ProgressAgeMs = conservativeAge };
            if (conservativeAge >= profile.FreshnessSeconds * 1000L)
                result = result with { Ready = false, Fresh = false, ProgressAgeMs = conservativeAge, ReasonCode = result.Ready ? "WORLD_STALE" : result.ReasonCode };
        }
        return result;
    }
}

internal sealed class ServiceMonitor(ServiceHealth health, WorldLoop loop, ILogger log, IHostApplicationLifetime lifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string? previous = null;
        using var registration = lifetime.ApplicationStopping.Register(health.Stopping);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var status = health.Read();
                if (previous != status.ReasonCode)
                {
                    using var scope = ServiceLog.Context(log, loop.Status); ServiceLog.HealthChanged(log, status.ReasonCode); previous = status.ReasonCode;
                }
                if (!status.ResourcesHealthy)
                {
                    health.Fail("MEMORY_LIMIT");
                    using var scope = ServiceLog.Context(log, loop.Status); ServiceLog.LimitExceeded(log, "MEMORY_LIMIT");
                    lifetime.StopApplication(); return;
                }
                await Task.Delay(250, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            health.Fail("HEALTH_MONITOR_FAILED");
            using var scope = ServiceLog.Context(log, loop.Status); ServiceLog.Failed(log, ex, "HEALTH_MONITOR_FAILED"); lifetime.StopApplication();
        }
    }
}

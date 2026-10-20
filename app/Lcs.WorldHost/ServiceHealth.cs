using System.Diagnostics;

namespace Lcs.WorldHost;

/// <summary>Odpowiedź aktywnego lokalnego procesu; UTC jest etykietą, nigdy dowodem świeżości.</summary>
public sealed record ServiceHealthStatus(int SchemaVersion, int ProcessId, string WorldId, string WorldRunId,
    WorldState State, bool Live, bool Ready, bool Fresh, long? ProgressAgeMs, long ServerTick, long SimulationTimeMs,
    bool DiagnosticsHealthy, bool ResourcesHealthy, string ReasonCode);

public sealed class ServiceHealth(WorldLoop loop, ServiceProfile profile, Func<bool>? diagnosticsHealthy = null,
    Func<long>? workingSet = null, TimeProvider? clock = null, Action? beforeFailureStop = null)
{
    readonly TimeProvider time = clock ?? TimeProvider.System;
    int stopping;
    string? failure;
    public string? FailureCode => Volatile.Read(ref failure);
    public void Stopping() { Interlocked.Exchange(ref stopping, 1); loop.RequestStop(); }
    public void Fail(string reason) { Volatile.Write(ref failure, reason); beforeFailureStop?.Invoke(); Stopping(); }
    public ServiceHealthStatus Read()
    {
        var s = loop.Status;
        long? age = s.LastStepTimestamp is long at ? Math.Max(0, (long)time.GetElapsedTime(at).TotalMilliseconds) : null;
        bool fresh = age is long ms && ms < profile.FreshnessSeconds * 1000L;
        bool diagnostics = diagnosticsHealthy?.Invoke() ?? true;
        bool resources = (workingSet?.Invoke() ?? Environment.WorkingSet) <= profile.MaxWorkingSetMiB * 1024L * 1024L;
        string reason = Volatile.Read(ref failure) ?? (s.State == WorldState.Faulted ? "WORLD_FAILED" :
            Volatile.Read(ref stopping) != 0 || s.State is WorldState.Stopping or WorldState.Stopped || s.State == WorldState.Running && !s.Ready ? "STOPPING" :
            !resources ? "MEMORY_LIMIT" : !diagnostics ? "DIAGNOSTICS_UNHEALTHY" :
            !s.Ready ? "STARTING" : !fresh ? "WORLD_STALE" : "READY");
        return new(1, Environment.ProcessId, s.WorldId, s.WorldRunId, s.State, true,
            reason == "READY", fresh, age, s.ServerTick, s.SimulationTimeMs, diagnostics, resources, reason);
    }
}

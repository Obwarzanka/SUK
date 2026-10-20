using System.Runtime.Versioning;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Lcs.WorldHost.Testy;

/// <summary>Polityka ServiceBase.ExitCode bez rejestracji SCM, konta usługi i ServiceBase.Run.</summary>
public sealed class ServiceLifetimeTests
{
    [SupportedOSPlatform("windows")]
    static WindowsServiceLifetime ScmLifetime(LifetimeStub lifetime) => new(new EnvironmentStub(), lifetime,
        NullLoggerFactory.Instance, Options.Create(new HostOptions()),
        Options.Create(new WindowsServiceLifetimeOptions { ServiceName = "SUK.WorldHost.UnitTest" }));

    [Fact]
    public async Task RequestedStopWhileInitializeIsBlockedKeepsSuccessfulServiceExitCode()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var application = new LifetimeStub(); using var scm = ScmLifetime(application);
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var runtime = new InitializingRuntime(entered, release);
        var loop = new WorldLoop(() => runtime);
        using var service = new WorldService(loop, application, hostLifetime: scm);
        Task starting = service.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.False(loop.Status.Ready); Assert.Equal(WorldState.Starting, loop.Status.State);
            application.StopApplication(); Assert.False(loop.Status.Ready);
        }
        finally { release.Set(); }
        await starting.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var final = await loop.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);
        Assert.Equal(WorldState.Stopped, final.State); Assert.False(final.Ready);
        Assert.Equal(0, final.ServerTick); Assert.Equal(0, scm.ExitCode); Assert.Equal(1, runtime.Disposals);
    }

    [Fact]
    public async Task InitializationFailureSetsNonzeroServiceExitCodeBeforeApplicationStop()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var application = new LifetimeStub(); using var scm = ScmLifetime(application);
        int? exitAtStop = null;
        application.OnStop = () => { if (OperatingSystem.IsWindows()) exitAtStop ??= scm.ExitCode; };
        var runtime = new RuntimeStub { ThrowAt = "initialize" };
        var loop = new WorldLoop(() => runtime);
        using var service = new WorldService(loop, application, hostLifetime: scm);
        Task starting = service.StartAsync(CancellationToken.None);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await starting.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        var final = await loop.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);
        Assert.Equal(WorldState.Faulted, final.State); Assert.False(final.Ready);
        Assert.Equal(2, scm.ExitCode); Assert.Equal(2, exitAtStop); Assert.Equal(1, runtime.Disposals);
    }

    [Fact]
    public void OperationalFailureSetsServiceExitBeforeWithdrawingWorldReadiness()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var application = new LifetimeStub(); using var scm = ScmLifetime(application);
        var clock = new ManualClock(); var loop = new WorldLoop(() => new RuntimeStub(), clock: clock);
        loop.Start(); clock.Advance(TimeSpan.FromMilliseconds(100)); loop.Pump(); Assert.True(loop.Status.Ready);
        bool? readyAtExitAssignment = null;
        var health = new ServiceHealth(loop, new ServiceProfile(), workingSet: () => 1024, clock: clock,
            beforeFailureStop: () => { if (OperatingSystem.IsWindows()) scm.ExitCode = 2; readyAtExitAssignment = loop.Status.Ready; });
        health.Fail("MEMORY_LIMIT");
        Assert.True(readyAtExitAssignment); Assert.Equal(2, scm.ExitCode);
        Assert.False(loop.Status.Ready); Assert.False(health.Read().Ready); Assert.Equal("MEMORY_LIMIT", health.FailureCode);
        loop.Pump(); Assert.Equal(WorldState.Stopped, loop.Status.State);
    }

    sealed class InitializingRuntime(ManualResetEventSlim entered, ManualResetEventSlim release) : IWorldRuntime
    {
        internal int Disposals;
        public void Initialize()
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Nie zwolniono kontrolowanej inicjalizacji testu.");
        }
        public void Tick(double seconds) { }
        public WorldResult Execute(WorldInput input, long tick) => new(input.RequestId, InputOutcome.Executed, tick);
        public WorldObservation Observe() => new(0, 0, 0, 0);
        public void Dispose() => Disposals++;
    }

    sealed class LifetimeStub : IHostApplicationLifetime, IDisposable
    {
        readonly CancellationTokenSource started = new(), stopping = new(), stopped = new();
        public CancellationToken ApplicationStarted => started.Token;
        public CancellationToken ApplicationStopping => stopping.Token;
        public CancellationToken ApplicationStopped => stopped.Token;
        internal Action? OnStop;
        public void StopApplication() { OnStop?.Invoke(); stopping.Cancel(); }
        public void Dispose() { started.Dispose(); stopping.Dispose(); stopped.Dispose(); }
    }

    sealed class EnvironmentStub : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "SUK.WorldHost.UnitTest";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

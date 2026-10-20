using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;

namespace Lcs.WorldHost;

/// <summary>Generic Host zarządza życiem procesu, dedykowany wątek jest jedynym właścicielem świata.</summary>
internal sealed class WorldService(WorldLoop loop, IHostApplicationLifetime lifetime, ServiceProfile? profile = null,
    IHostLifetime? hostLifetime = null) : BackgroundService
{
    CancellationTokenRegistration stopRegistration;
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        stopRegistration = lifetime.ApplicationStopping.Register(loop.RequestStop);
        await base.StartAsync(cancellationToken);
        try
        {
            var status = await loop.Ready.WaitAsync(TimeSpan.FromSeconds(profile?.StartupTimeoutSeconds ?? 30), cancellationToken);
            if (loop.Status.Ready && !lifetime.ApplicationStopping.IsCancellationRequested)
                Console.WriteLine($"Świat gotowy: {status.WorldRunId}, krok {status.ServerTick}, czas {status.SimulationTimeMs} ms.");
        }
        catch when (loop.Status.State != WorldState.Faulted &&
            (lifetime.ApplicationStopping.IsCancellationRequested || cancellationToken.IsCancellationRequested))
        { loop.RequestStop(); }
        catch
        {
            if (OperatingSystem.IsWindows() && hostLifetime is WindowsServiceLifetime service) service.ExitCode = 2;
            loop.RequestStop(); throw;
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.Factory.StartNew(() =>
    {
        try
        {
            loop.Start();
            while (!loop.Completion.IsCompleted)
            {
                if (stoppingToken.IsCancellationRequested) loop.RequestStop();
                loop.Pump();
                if (!loop.Completion.IsCompleted) stoppingToken.WaitHandle.WaitOne(5);
            }
        }
        finally
        {
            loop.Finish();
            if (OperatingSystem.IsWindows() && loop.Status.State == WorldState.Faulted && hostLifetime is WindowsServiceLifetime service)
                service.ExitCode = 2;
            lifetime.StopApplication();
        }
    }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        loop.RequestStop();
        await base.StopAsync(cancellationToken);
    }
    public override void Dispose() { stopRegistration.Dispose(); base.Dispose(); }
}

using Microsoft.Extensions.Hosting;

namespace Lcs.WorldHost;

/// <summary>Generic Host zarządza życiem procesu, dedykowany wątek jest jedynym właścicielem świata.</summary>
internal sealed class WorldService(WorldLoop loop, IHostApplicationLifetime lifetime) : BackgroundService
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken);
        try
        {
            var status = await loop.Ready.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            Console.WriteLine($"Świat gotowy: {status.WorldRunId}, krok {status.ServerTick}, czas {status.SimulationTimeMs} ms.");
        }
        catch { loop.RequestStop(); throw; }
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
        finally { loop.Finish(); lifetime.StopApplication(); }
    }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        loop.RequestStop();
        await base.StopAsync(cancellationToken);
    }
}

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Lcs.Logi;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Lcs.WorldHost.Testy;

public sealed class WorldLoggingTests
{
    [Fact]
    public void F24WorldLifecycleUsesVersionedCorrelatedJsonAndRedactsSecretsAndInjectedLines()
    {
        WorldLog.Register(); string directory = TestArtifacts.Directory("N43-kontekst-redakcja");
        using var engine = SilnikLogow.Uruchom(Options(directory)); var log = engine.Modul(WorldLog.Module, WorldLog.Version);
        var clock = new ManualClock(); var runtime = new RuntimeStub();
        var loop = new WorldLoop(() => runtime, new() { WorldId = "swiat-testowy" }, clock, log);
        loop.Start(); clock.Advance(TimeSpan.FromMilliseconds(100)); loop.Pump();
        // Próbki pól spoza listy, niepoprawnego correlationId, wielowierszowego tekstu i wyjątków.
        using (log.BeginScope(new Dictionary<string, object> { ["worldId"] = loop.Status.WorldId, ["worldRunId"] = loop.Status.WorldRunId,
            ["serverTick"] = 1L, ["simulationTimeMs"] = 100L })) ProbaLogow.Wykonaj(log);
        var invalid = loop.Submit(new("A", "invalid", InputKind.Command, "x\ny"));
        Assert.Equal(InputOutcome.Invalid, WorldLoopTests.Completed(invalid).Outcome);
        runtime.OnExecute = _ => throw new IOException("password=" + ProbaLogow.ZnacznikSekretu + "_WORLD;\r\n{\"wstrzykniete\":1}");
        var failure = loop.Submit(new("A", "failure", InputKind.Command, "treść polecenia nie jest polem diagnostyki")); loop.Pump();
        Assert.Equal(InputOutcome.Failed, WorldLoopTests.Completed(failure).Outcome);
        var state = engine.Zatrzymaj(); File.WriteAllText(Path.Combine(directory, "stan.txt"), state.ToString());
        var records = TestArtifacts.Records(directory); string all = string.Join('\n', records.Select(r => r.GetRawText()));
        Assert.DoesNotContain(ProbaLogow.ZnacznikSekretu, all, StringComparison.Ordinal);
        Assert.DoesNotContain("treść polecenia nie jest polem diagnostyki", all, StringComparison.Ordinal);
        Assert.DoesNotContain(records, r => r.TryGetProperty("wstrzykniete", out _));
        Assert.DoesNotContain(records, r => r.TryGetProperty("password", out _) || r.TryGetProperty("token", out _) || r.TryGetProperty("ApiKey", out _));
        var world = records.Where(r => Text(r, "eventCode").StartsWith("WORLD.", StringComparison.Ordinal)).ToArray();
        foreach (string code in new[] { "WORLD.STARTING", "WORLD.READY", "WORLD.FAILED", "WORLD.STOPPING", "WORLD.STOPPED", "WORLD.METRICS", "WORLD.INPUT_REJECTED" })
            Assert.Contains(world, r => Text(r, "eventCode") == code);
        Assert.All(world, record =>
        {
            Assert.Equal(1, record.GetProperty("schemaVersion").GetInt32()); Assert.Equal(WorldLog.Module, Text(record, "moduleId"));
            Assert.Equal(WorldLog.Version, Text(record, "moduleVersion")); Assert.Equal(loop.Status.WorldId, Text(record, "worldId"));
            Assert.Equal(loop.Status.WorldRunId, Text(record, "worldRunId")); Assert.True(Guid.TryParse(Text(record, "processRunId"), out _));
            long tick = record.GetProperty("serverTick").GetInt64(); Assert.Equal(tick * 100, record.GetProperty("simulationTimeMs").GetInt64());
            Assert.True(record.GetProperty("monoElapsedUs").GetInt64() >= 0); Assert.True(record.GetProperty("recordSeq").GetInt64() > 0);
        });
        var ready = Assert.Single(world, r => Text(r, "eventCode") == "WORLD.READY");
        Assert.Equal(1, ready.GetProperty("serverTick").GetInt64()); Assert.True(state.ZamkniecieKompletne); Assert.Equal(0, state.StratyRazem);
    }

    [Theory]
    [InlineData(16, 1024 * 1024, "QUEUE_FULL")]
    [InlineData(64, 16 * 1024, "QUEUE_BYTES")]
    public void HostLogFloodHasBoundedMemoryAndRotatedFilesWhileWorldKeepsTicking(int recordLimit, long byteLimit, string reason)
    {
        WorldLog.Register(); string directory = TestArtifacts.Directory("N43-zalew-rotacja-" + reason);
        var options = Options(directory); options.MaxRekordowKolejki = recordLimit; options.MaxBajtowKolejki = byteLimit;
        options.MaxBajtowPliku = 16 * 1024; options.MaxPlikow = 3;
        using var engine = SilnikLogow.Uruchom(options); var log = engine.Modul(WorldLog.Module, WorldLog.Version);
        var clock = new ManualClock(); var runtime = new RuntimeStub(); var loop = new WorldLoop(() => runtime, clock: clock, logger: log);
        loop.Start(); var elapsed = Stopwatch.StartNew();
        for (int n = 0; n < 10000; n++)
        {
            using (log.BeginScope(new Dictionary<string, object> { ["worldId"] = loop.Status.WorldId, ["worldRunId"] = loop.Status.WorldRunId,
                ["serverTick"] = loop.Status.ServerTick, ["simulationTimeMs"] = loop.Status.SimulationTimeMs }))
                WorldLog.Metrics(log, new Dictionary<string, long> { ["probe"] = n });
            if (n % 50 == 49) { clock.Advance(TimeSpan.FromMilliseconds(100)); loop.Pump(); }
        }
        loop.Finish(); var state = engine.Zatrzymaj(); elapsed.Stop();
        File.WriteAllText(Path.Combine(directory, "stan.txt"), state + $"ticki={runtime.Ticks.Count}\nczas_proby_ms={elapsed.Elapsed.TotalMilliseconds:F1}\n");
        Assert.Equal(200, runtime.Ticks.Count); Assert.Equal(WorldState.Stopped, loop.Status.State);
        Assert.True(state.MaxWKolejce <= state.LimitRekordow); Assert.True(state.MaxBajtowWKolejce <= state.LimitBajtow);
        Assert.True(state.StratyRazem > 0, "Zalew ma rzeczywiście wyczerpać mały budżet diagnostyki.");
        Assert.True(state.StratyWgModuluIPowodu.GetValueOrDefault(WorldLog.Module + "|" + reason) > 0);
        string[] files = System.IO.Directory.GetFiles(directory, "*.jsonl"); Assert.InRange(files.Length, 2, 3);
        Assert.All(files, file => Assert.True(new FileInfo(file).Length <= 16 * 1024 + KontraktLogu.MaxBajtowRekordu));
        Assert.Contains(TestArtifacts.Records(directory), r => Text(r, "eventCode") == "LOG.DIAGNOSTICS_DROPPED");
        Assert.True(state.ZamkniecieKompletne); Assert.Equal(0, state.BledyZapisu);
    }

    static OpcjeLogow Options(string directory) => new() { Aplikacja = "Lcs.WorldHost", Katalog = directory, Profil = ProfilLogow.Serwer };
    static string Text(JsonElement record, string key) => record.TryGetProperty(key, out var value) ? value.GetString() ?? "" : "";
}

internal static class TestArtifacts
{
    public static string Directory(string name)
    {
        string directory = Path.Combine(Environment.GetEnvironmentVariable("SUK_WORLD_TEST_ARTIFACTS") ?? Path.GetTempPath(), name, Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(directory); return directory;
    }

    public static JsonElement[] Records(string directory) => System.IO.Directory.GetFiles(directory, "*.jsonl").OrderBy(p => p, StringComparer.Ordinal)
        .SelectMany(File.ReadAllLines).Where(line => line.Length > 0).Select(line =>
        {
            Assert.True(Encoding.UTF8.GetByteCount(line + "\n") <= KontraktLogu.MaxBajtowRekordu);
            using var json = JsonDocument.Parse(line); Assert.Equal(JsonValueKind.Object, json.RootElement.ValueKind); return json.RootElement.Clone();
        }).ToArray();
}

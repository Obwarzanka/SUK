using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace Lcs.WorldHost.Testy;

public sealed class ProcessTests
{
    [Theory]
    [InlineData("--report", false)]
    [InlineData("--events", false)]
    [InlineData("--logi-raport", false)]
    [InlineData("--logi-raport", true)]
    public void CollidingOutputOrInvalidOptionsNeverOverwriteStation(string output, bool invalidOptions)
    {
        string directory = TestArtifacts.Directory("proces-kolizja"); string station = CopyStation(directory);
        byte[] before = SHA256.HashData(File.ReadAllBytes(station));
        var args = new List<string> { "--station", station, "--ticks", "1", output, station, "--logi", Path.Combine(directory, "logi") };
        if (invalidOptions) args.AddRange(["--world-id", "zły świat"]);
        Assert.Equal(2, Run(directory, args)); Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(station)));
    }

    public static IEnumerable<object[]> SidecarCollisions() =>
        from input in new[] { "station_test.zaleznosci.json", "station_test.rozklad.json", "station_test.tabor.json", "rozklad.json" }
        from output in new[] { "--report", "--events", "--logi-raport" }
        select new object[] { input, output };

    [Theory]
    [MemberData(nameof(SidecarCollisions))]
    public void CollidingOutputNeverOverwritesOtherKnownWorldInputs(string inputName, string output)
    {
        string directory = TestArtifacts.Directory("proces-kolizja-wejscia"); string station = CopyStation(directory);
        string input = Path.Combine(directory, inputName);
        File.WriteAllText(input, "dane wejściowe testu kolizji — nie wolno ich nadpisać");
        byte[] before = SHA256.HashData(File.ReadAllBytes(input));
        Assert.Equal(2, Run(directory, ["--station", station, "--ticks", "1", output, input, "--logi", Path.Combine(directory, "logi")]));
        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(input)));
    }

    [Fact]
    public void EventExportFailureReturnsFailureInBothExitCodeAndFinalReport()
    {
        string directory = TestArtifacts.Directory("proces-blad-eksportu"); string station = CopyStation(directory);
        string report = Path.Combine(directory, "wynik.json");
        Assert.Equal(2, Run(directory, ["--station", station, "--ticks", "1", "--events", directory,
            "--report", report, "--logi", Path.Combine(directory, "logi")]));
        using var json = JsonDocument.Parse(File.ReadAllText(report));
        Assert.Equal(2, json.RootElement.GetProperty("exitCode").GetInt32()); Assert.True(json.RootElement.GetProperty("exportFailed").GetBoolean());
        Assert.True(json.RootElement.GetProperty("shutdownComplete").GetBoolean());
    }

    [Fact]
    public void HeadlessProcessStopsAtTickLimitAndWritesManifestAndCorrelatedLogs()
    {
        string directory = TestArtifacts.Directory("proces-caly"); string station = CopyStation(directory);
        string report = Path.Combine(directory, "wynik.json"), events = Path.Combine(directory, "zdarzenia.txt"), logs = Path.Combine(directory, "logi");
        Assert.Equal(0, Run(directory, ["--station", station, "--ticks", "2", "--report", report, "--events", events,
            "--logi", logs, "--logi-raport", Path.Combine(directory, "stan-logow.txt")]));
        using var json = JsonDocument.Parse(File.ReadAllText(report)); var root = json.RootElement;
        Assert.Equal(0, root.GetProperty("exitCode").GetInt32()); Assert.True(root.GetProperty("shutdownComplete").GetBoolean());
        Assert.Equal("Stopped", root.GetProperty("world").GetProperty("State").GetString());
        Assert.Equal(2, root.GetProperty("world").GetProperty("ServerTick").GetInt64());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(station))), root.GetProperty("inputHashes").GetProperty("station").GetString());
        Assert.True(root.GetProperty("diagnostics").GetProperty("healthy").GetBoolean()); Assert.True(File.Exists(events));
        Assert.Contains(TestArtifacts.Records(logs), r => r.GetProperty("eventCode").GetString() == "WORLD.READY");
    }

    static string CopyStation(string directory)
    {
        string station = Path.Combine(directory, "station_test.json"); File.Copy(Path.Combine(AppContext.BaseDirectory, "TestData", "station_test.json"), station);
        return station;
    }

    static int Run(string directory, IEnumerable<string> arguments)
    {
        string dotnet = Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT") ?? throw new InvalidOperationException("Test procesu wymaga DOTNET_ROOT."), "dotnet.exe");
        var start = new ProcessStartInfo(dotnet) { WorkingDirectory = AppContext.BaseDirectory, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Lcs.WorldHost.dll"));
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Nie uruchomiono procesu hosta.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        bool finished = process.WaitForExit(15000); if (!finished) process.Kill(entireProcessTree: true);
        Assert.True(finished, "Host nie zakończył się w limicie testu 15 s.");
        File.WriteAllText(Path.Combine(directory, "stdout.txt"), stdout.GetAwaiter().GetResult());
        File.WriteAllText(Path.Combine(directory, "stderr.txt"), stderr.GetAwaiter().GetResult());
        return process.ExitCode;
    }
}

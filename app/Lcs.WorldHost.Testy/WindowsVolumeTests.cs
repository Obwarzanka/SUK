using System.Runtime.Versioning;
using System.Text.Json;
using Xunit;

namespace Lcs.WorldHost.Testy;

/// <summary>Dowód pochodzący z metadanych Windows, bez mapowania dysków i punktów ponownej analizy.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsVolumeTests
{
    [Theory]
    [InlineData(@"C:\")]
    [InlineData(@"D:\")]
    public void ExistingPhysicalVolumeRootIsRecognizedByOperatingSystemMetadata(string path)
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.True(Directory.Exists(path), "Próba wymaga rzeczywistego woluminu stanowiska testowego.");
        Assert.True(Probe(path));
    }

    [Fact]
    public void OrdinaryOwnedDirectoryIsNotPhysicalVolumeRoot()
    {
        if (!OperatingSystem.IsWindows()) return;
        string path = TestArtifacts.Directory("volume-ordinary-directory");
        Assert.True(Directory.Exists(path)); Assert.False(Probe(path));
    }

    [Fact]
    public void NonexistentPathCannotBecomePhysicalRootFromItsText()
    {
        if (!OperatingSystem.IsWindows()) return;
        string path = Path.Combine(TestArtifacts.Directory("volume-missing-directory"), Guid.NewGuid().ToString("N"));
        Assert.False(Directory.Exists(path)); Assert.False(Probe(path));
    }

    [Fact]
    public void ExistingDirectoryNamedLikeVolumeGuidStillHasNonRootFinalPathSuffix()
    {
        if (!OperatingSystem.IsWindows()) return;
        string path = Path.Combine(TestArtifacts.Directory("volume-synthetic-name"), "Volume{" + Guid.NewGuid().ToString("D") + "}");
        Directory.CreateDirectory(path); Assert.True(Directory.Exists(path)); Assert.False(Probe(path));
    }

    static bool Probe(string path)
    {
        bool physicalRoot = WindowsVolume.IsPhysicalRoot(path);
        string evidence = TestArtifacts.Directory("volume-os-metadata");
        File.WriteAllText(Path.Combine(evidence, "probe.json"), JsonSerializer.Serialize(new { path, physicalRoot }));
        return physicalRoot;
    }
}

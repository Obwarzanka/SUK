using System.Text;
using LcsWidzew.Model;
using LcsWidzew.Sim;
using Microsoft.Extensions.Logging;

namespace Lcs.Core.TestHost;

/// <summary>
/// Eksport mapy własności postId/kzId (S02A.01) z jawnej kopii układu do pliku JSON – artefakt przeglądu dla właściciela.
/// Tylko odczyt układu; wynik nie może nadpisać istniejącego pliku, układu ani jego zależności specjalnych.
/// </summary>
internal static class OwnershipMap
{
    internal static MapaWlasnosci Export(string stationPath, string outputPath, ILogger logger)
    {
        if (!outputPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Plik mapy musi mieć rozszerzenie .json.");
        var protectedPaths = new[] { stationPath, Zaleznosci.PlikDla(stationPath) }.Where(p => p != null).Select(Path.GetFullPath);
        if (protectedPaths.Any(p => string.Equals(p, outputPath, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Plik mapy nie może zastąpić układu ani jego zależności specjalnych.");
        if (File.Exists(outputPath)) throw new IOException("Plik mapy już istnieje – użyj nowej ścieżki (dowody pozostają nienaruszone).");
        var station = Station.Load(stationPath);
        var sim = new Simulator(station, options: new SimulatorOptions { MapaWlasnosciLogger = logger });
        var map = sim.Wlasnosc;
        using (var stream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            writer.Write(map.Json);
        return map;
    }
}

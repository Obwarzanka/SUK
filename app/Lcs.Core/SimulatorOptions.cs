using Microsoft.Extensions.Logging;

namespace LcsWidzew.Sim;

/// <summary>Zależności środowiska przekazywane przez pulpit lub host bez okien.</summary>
public sealed class SimulatorOptions
{
    /// <summary>Katalog plików rozkładu. Brak – katalog pliku wczytanego przez Station.Load.</summary>
    public string DataDirectory { get; init; }

    /// <summary>Logger rejestratora. Host zapewnia ograniczony adapter MEL; brak oznacza NullLogger.</summary>
    public ILogger RejestratorLogger { get; init; }

    /// <summary>Logger mapy własności KZ (S02A.01, KZ.OWNERSHIP_* 4101–4103); brak – mapa bez logów.</summary>
    public ILogger MapaWlasnosciLogger { get; init; }
}

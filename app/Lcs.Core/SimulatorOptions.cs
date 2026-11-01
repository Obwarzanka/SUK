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

    /// <summary>Logger rejestru wejść cyklu KZ (S02A.02, KZ.CYCLE_INPUT_* 4111–4116); brak – rejestr bez logów.</summary>
    public ILogger WejscieCykluLogger { get; init; }
    /// <summary>Logger jednego wyjścia KZ (S02A.03, 4121–4125), poza Evaluate.</summary>
    public ILogger PlanWyjsciaLogger { get; init; }
    /// <summary>Jawny kontekst poleceń i zatwierdzona projekcja ruchu (S02A.04, 4130–4133).</summary>
    public ILogger GraniceLogger { get; init; }
}

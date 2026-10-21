using System.IO;
using Lcs.Logi;
using LcsWidzew.Sim;

namespace LcsWidzew;

public partial class App
{
    /// <summary>Adapter pulpitu: ścieżki i provider logów pozostają poza neutralnym silnikiem.</summary>
    internal static SimulatorOptions CreateSimulatorOptions()
    {
        KatalogZdarzen.Zarejestruj(new DefinicjaZdarzenia(CoreLogging.EventId, CoreLogging.EventCode,
            KontraktLogu.Strumien.Domenowy, CoreLogging.PolaZdarzeniaRejestratora.ToArray()));
        foreach (var (id, kod, pola) in MapaWlasnosciLog.Definicje)
            KatalogZdarzen.Zarejestruj(new DefinicjaZdarzenia(id, kod, KontraktLogu.Strumien.Diagnostyka, pola));
        return new SimulatorOptions
        {
            DataDirectory = Path.GetDirectoryName(Path.GetFullPath(StationPath)),
            RejestratorLogger = Diagnostyka.Modul(CoreLogging.Modul, CoreLogging.Wersja),
            MapaWlasnosciLogger = Diagnostyka.Modul(MapaWlasnosciLog.Modul, MapaWlasnosciLog.Wersja),
        };
    }
}

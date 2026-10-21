using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lcs.Tabor;
using Lcs.Logi;
using LcsWidzew.Model;
using LcsWidzew.Sim;
using Microsoft.Extensions.Logging;

namespace Lcs.WorldHost;

/// <summary>Przejściowy adapter obecnego rdzenia. Jeden Simulator/Trains; podział KZ i Ada należą do S02A–S02D.</summary>
public sealed class SimulatorRuntime(string stationPath, string scenario, ILogger logger, ILogger recorder, bool exportEvents = true, ILogger? ownership = null) : IWorldRuntime
{
    Simulator? sim;
    Desk? desk;
    public string? FinalEvents { get; private set; }
    public IReadOnlyDictionary<string, string>? InputHashes { get; private set; }

    public void Initialize()
    {
        if (!Rozklad.Scenarios.Any(s => s.Key == scenario)) throw new ArgumentException("Nieznany scenariusz.");
        var station = Station.Load(stationPath);
        sim = new Simulator(station, options: new SimulatorOptions { RejestratorLogger = recorder, MapaWlasnosciLogger = ownership });
        var trains = new Trains(sim);
        _ = Sygnaly.Tab;
        _ = trains.Tabor;
        trains.Load(scenario);
        // S02A.01: mapa własności postId/kzId (tylko odczyt i log KZ.OWNERSHIP_*); jeszcze bez konsumenta w cyklu.
        if (ownership != null) sim.OpublikujWlasnosc();
        // Stanowisko lokalne jest zaufanym adapterem, nie kontem ani wejściem publicznego serwera.
        desk = sim.AddDesk("HOST", "Lokalne stanowisko hosta", true); desk.Page = sim.Page;
        // Ograniczony manifest znanych wejść i ich braku. Ścieżki i treść nie trafiają do diagnostyki.
        var paths = KnownInputs(stationPath);
        var hashes = paths.ToDictionary(p => p.Key, p => p.Value != null && File.Exists(p.Value)
            ? Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(p.Value))) : "missing");
        InputHashes = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(hashes);
        WorldLog.DataLoaded(logger, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(hashes)))));
    }

    public void Tick(double seconds) => sim!.Tick(seconds);
    public WorldResult Execute(WorldInput input, long serverTick)
    {
        var result = input.Kind switch
        {
            InputKind.Command => sim!.ExecAs(desk, input.Text),
            InputKind.Confirm => sim!.ConfirmAs(desk, input.Text),
            InputKind.Cancel => Cancel(),
            _ => throw new ArgumentException("Nieznany rodzaj wejścia.")
        };
        return new(input.RequestId, result.Ok ? InputOutcome.Executed : InputOutcome.Rejected, serverTick, result.Msg ?? "");
    }
    CmdResult Cancel() { sim!.CancelAs(desk); return new(true, "Anulowano oczekujące polecenie."); }
    public WorldObservation Observe() => new(sim!.T, sim.Trains.List.Count, sim.Routes.Count, sim.Events.Count);
    internal static IReadOnlyDictionary<string, string?> KnownInputs(string stationPath) => new SortedDictionary<string, string?>
    {
        ["station"] = stationPath, ["dependencies"] = Path.ChangeExtension(stationPath, ".zaleznosci.json"),
        ["timetable"] = RozkladPlik.PlikDla(stationPath), ["gtfs"] = Path.Combine(Path.GetDirectoryName(stationPath)!, "rozklad.json"),
        ["signals"] = Path.Combine(AppContext.BaseDirectory, "Data", "sygnaly_ie1.json"),
        ["stock"] = TaborPlik.SciezkaWspolna(), ["stationStock"] = TaborPlik.PlikUkladu(stationPath)
    };
    public void Dispose()
    {
        // Kopia powstaje na właścicielu po ostatnim kroku. Zapis do pliku należy do procesu po zakończeniu cyklu.
        if (exportEvents && sim != null) FinalEvents = string.Join('\n', sim.Events.Select(e =>
            $"{e.Id}|{e.T.ToString("R", CultureInfo.InvariantCulture)}|{e.Czas}|{e.Kategoria}|{e.Zdarzenie}|{e.Obiekt}")) + "\n";
    }
}

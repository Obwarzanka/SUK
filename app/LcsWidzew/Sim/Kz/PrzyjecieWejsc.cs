using SOdcinek = LcsWidzew.Sim.StatusOdcinka;
using SZwrotnica = LcsWidzew.Sim.StatusZwrotnicy;
using SWykolejnica = LcsWidzew.Sim.StatusWykolejnicy;
using SSygnalizator = LcsWidzew.Sim.StatusSygnalizatora;
using SZasilanie = LcsWidzew.Sim.StatusZasilania;

namespace LcsWidzew.Sim;

/// <summary>
/// Komputer zależności – przyjęcie zamrożonych statusów kart (S02A.02). Statusy przychodzą wyłącznie z rejestru wejść cyklu
/// (<see cref="RejestrWejscCyklu"/>) jako niezmienne rekordy całej partii fazy; karty nie wywołują już KZ i nie piszą jego
/// słowników. Jeden KZ C# całego układu jest przejściowym odbiorcą obszaru (do S02B): stosuje partię w kolejności zgłoszeń,
/// tą samą logiką co dotąd (bazowe zachowanie, kolejność dziennika i alarmów bez zmian).
/// </summary>
public partial class KomputerZaleznosci : IOdbiorcaPartiiWejsc
{
    void IOdbiorcaPartiiWejsc.Przyjmij(PartiaWejsc partia)
    {
        foreach (var s in partia.Statusy)
            switch (s)
            {
                case SOdcinek o: PrzyjmijOdcinek(o.Obiekt, o.Pojazd, o.Stan); break;
                case SZwrotnica z: StatusZwrotnicy(z.Obiekt, z.Polozenie, z.Przekladanie, z.PrzekroczonyCzas); break;
                case SWykolejnica d: StatusWykolejnicy(d.Obiekt, d.Zalozona); break;
                case SSygnalizator g: StatusSygnalizatora(g.Obiekt, g.Wyswietlany, g.UszkodzenieZezwalajacego, g.BrakLacznosci); break;
                case SZasilanie p: StatusZasilania(p.Siec, p.Ups, p.Agregat, p.NapiecieNastawcze, p.AgregatNieRuszyl); break;
                default: throw new InvalidOperationException("Nieznany rodzaj statusu karty.");
            }
    }

    /// <summary>Status karty odcinka: zajętość przez pojazd i stan urządzenia kontroli niezajętości (dawniej zapis karty do <see cref="Secs"/>).</summary>
    void PrzyjmijOdcinek(string id, bool pojazd, string stan)
    {
        var k = Secs[id]; k.TrainOcc = pojazd; k.Fault = stan;
        Urzadzenia.LogStatus("odcinek", id, (pojazd || stan != null ? "zajęty" : "wolny") + stan switch { "occ" => " (bez pojazdu)", "reset" => ", wymaga zerowania", "await" => ", czeka na 1. przejazd", _ => "" });
    }
}

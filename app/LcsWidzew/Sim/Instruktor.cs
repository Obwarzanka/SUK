namespace LcsWidzew.Sim;

/// <summary>
/// Instruktor: usterki urządzeń i scenariusze usterek. Usterka trafia do warstwy, w której powstaje w rzeczywistości
/// (Zrodla/EBILock_komputerownia.md, tabela 7.4) – na razie jeszcze wprost do stanu komputera zależności (etap 7, kroki 4–7).
/// </summary>
public class Instruktor
{
    readonly Simulator S;
    /// <summary>Usterki zaplanowane w scenariuszu: czas [s], rodzaj, obiekt, usunięcie, wykonana.</summary>
    public List<(double T, string Type, string Target, bool Clear, bool Done)> Queue = new();

    public Instruktor(Simulator s) { S = s; }

    /// <summary>Usterki scenariusza (klucz jak w Rozklad.Scenarios); obiekty spoza układu są pomijane.</summary>
    public void Load(string key)
    {
        Queue = key switch
        {
            "usterki" => new() { (1, "sigLamp", "LW:S1", false, false), (140, "secOcc", "LW:z53", false, false), (240, "secOcc", "LW:z53", true, false), (300, "swNoCtrl", "LW:57", false, false), (380, "mains", null, false, false), (470, "mains", null, true, false), (500, "nbrSilent", "2A", false, false) },
            "zima" => new() { (120, "swFrozen", "LW:11", false, false), (120, "swFrozen", "LW:53", false, false), (420, "swFrozen", "LW:11", true, false) },
            _ => new()
        };
        Queue = Queue.Where(f => f.Target == null || S.Secs.ContainsKey(f.Target) || S.Sws.ContainsKey(f.Target) || S.Sigs.ContainsKey(f.Target) || S.Blocks.ContainsKey(f.Target)).ToList();
    }

    /// <summary>Wprowadza usterki, których czas nadszedł.</summary>
    public void Tick()
    {
        for (int i = 0; i < Queue.Count; i++)
        {
            var f = Queue[i];
            if (!f.Done && f.T <= S.T) { Queue[i] = (f.T, f.Type, f.Target, f.Clear, true); ApplyFault(f.Type, f.Target, f.Clear); }
        }
    }

    // ------------------------------------------------------------------ usterki
    public static readonly (string Key, string Name, string Target)[] Faults =
    {
        ("secOcc", "Zajętość odcinka bez pojazdu (licznik osi albo obwód torowy)", "sec"),
        ("loMiscount", "Licznik osi – błąd zliczenia osi przy przejeździe (odcinek zostaje zajęty)", "sec"),
        ("swNoCtrl", "Zwrotnica – niespodziewany brak kontroli położenia", "sw"),
        ("swFrozen", "Zwrotnica – nie przestawia się (zamarznięcie)", "sw"),
        ("swRozpr", "Zwrotnica – sygnalizacja rozprucia", "sw"),
        ("sigLamp", "Semafor – nie wyświetla sygnału zezwalającego", "sig"),
        ("sigNodata", "Semafor – brak komunikacji z obiektem", "sig"),
        ("mains", "Zanik zasilania sieciowego (agregat samoczynny)", ""),
        ("mainsGenFail", "Zanik zasilania + agregat nie startuje", ""),
        ("nbrSilent", "Blokada – brak odpowiedzi posterunku sąsiedniego", "block"),
        ("kzLink", "Utrata łączności stanowiska z komputerem zależności", "")
    };

    public void ApplyFault(string type, string target, bool clear)
    {
        switch (type)
        {
            case "secOcc" when S.L.Sections.TryGetValue(target, out var sdo) && !sdo.AxleCounter:
                // obwód torowy: zajętość bez pojazdu (zwarcie toków, uszkodzenie); po ustąpieniu przyczyny odcinek wolny bez zerowania
                if (!clear) { S.Secs[target].Fault = "occ"; S.Alarm($"Odcinek {target} (obwód torowy): zajętość bez pojazdu", "Odcinek", target, "occ" + target, () => S.Secs[target].Fault != null); }
                else { S.Secs[target].Fault = null; S.Log("Zdarzenie", $"Odcinek {target} (obwód torowy): zajętość ustąpiła – odcinek wolny", "Odcinek", target); }
                break;
            case "loMiscount":
                if (S.L.Sections.TryGetValue(target, out var sdm) && !sdm.AxleCounter) { S.Log("Instruktor", $"Odcinek {target} kontrolowany obwodem torowym – błąd zliczenia osi nie dotyczy", "Odcinek", target); return; }
                S.Secs[target].Miscount = !clear;
                break;
            case "secOcc":
                if (!clear) { S.Secs[target].Fault = "occ"; S.Alarm($"Odcinek {target}: zajętość bez pojazdu", "Odcinek", target, "occ" + target, () => S.Secs[target].Fault != null); }
                else { S.Secs[target].Fault = "reset"; S.Alarm($"Odcinek {target}: wymagane potwierdzenie zerowania (ZeroLO)", "Odcinek", target, "rst" + target, () => S.Secs[target].Fault == "reset"); }
                break;
            case "swNoCtrl": S.Sws[target].NoCtrl = !clear; if (!clear) S.Alarm($"Zwrotnica {target}: niespodziewany brak kontroli położenia", "Zwrotnica", target, stillActive: () => S.Sws[target].NoCtrl); break;
            case "swFrozen": S.Sws[target].Frozen = !clear; break;
            case "kzLink":
                S.LinkDown = !clear;
                if (!clear) S.Alarm("Utrata łączności stanowiska z komputerem zależności – stan obiektów nieznany, polecenia nie są przyjmowane", "System", "", "kzlink", () => S.LinkDown);
                else S.Log("Zdarzenie", "Przywrócono łączność stanowiska z komputerem zależności", "System", "");
                break;
            case "swRozpr":
                if (!clear) { S.Sws[target].Rozpr = true; S.Alarm($"Zwrotnica {target}: sygnalizacja rozprucia", "Zwrotnica", target, stillActive: () => S.Sws[target].Rozpr); }
                else { var w = S.Sws[target]; w.Rozpr = false; w.Damaged = false; w.NoCtrl = false; }
                break;
            case "sigLamp": S.Sigs[target].LampFault = !clear; if (clear) S.Sigs[target].FailAlarm = false; break;
            case "sigNodata": S.Sigs[target].NoData = !clear; if (!clear) S.Alarm($"Sygnalizator {target}: brak komunikacji z obiektem", "Semafor", target, stillActive: () => S.Sigs[target].NoData); break;
            case "mains":
            case "mainsGenFail":
                if (!clear) { S.Power.Mains = false; S.Power.OutageAt = S.T; S.Power.GenFail = type == "mainsGenFail"; S.Alarm(type == "mains" ? "Zanik zasilania sieciowego – praca z UPS (Ie-20 §23)" : "Zanik zasilania – agregat nie uruchomił się (AGR / automatyk, §23 ust. 2)", "Zasilanie", "SIEĆ", stillActive: () => !S.Power.Mains); }
                else { S.Power.Mains = true; S.Power.Gen = false; S.Power.OutageAt = null; S.Power.GenFail = false; S.Log("Zdarzenie", "Przywrócono zasilanie sieciowe", "Zasilanie", "SIEĆ"); }
                break;
            case "nbrSilent": S.Blocks[target].NbrSilent = !clear; break;
        }
        S.Log("Instruktor", (clear ? "Usunięto usterkę: " : "Wprowadzono usterkę: ") + Faults.First(f => f.Key == type).Name + (target != null ? $" [{target}]" : ""), "Instruktor", target ?? "");
    }
}

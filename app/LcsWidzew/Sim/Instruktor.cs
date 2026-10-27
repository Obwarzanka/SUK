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
        ("swRozpr", "Zwrotnica – uszkodzenie iglic (brak kontroli; przy zajętym odcinku – rozprucie)", "sw"),
        ("sigLamp", "Semafor – nie wyświetla sygnału zezwalającego", "sig"),
        ("sigNodata", "Semafor – brak komunikacji z obiektem", "sig"),
        ("mains", "Zanik zasilania sieciowego (agregat samoczynny)", ""),
        ("mainsGenFail", "Zanik zasilania + agregat nie startuje", ""),
        ("nbrSilent", "Blokada – brak odpowiedzi posterunku sąsiedniego", "block"),
        ("kzLink", "Utrata łączności stanowiska z komputerem zależności", "")
    };

    public void ApplyFault(string type, string target, bool clear)
    {
        S.Wejscia.PozaOdbiorca();
        // usterka w karcie – natychmiastowa faza Usterka: każdy status karty trafia do KZ od razu (przed alarmem karty i wpisem instruktora; S02A.02)
        bool Karta(Func<bool> usterka) => S.Wejscia.WFazie(FazaWejscia.Usterka, usterka);
        switch (type)
        {
            case "secOcc":
            case "loMiscount":
                if (!Karta(() => S.Urzadzenia.Odcinki[target].Usterka(type, clear))) return;
                break;
            case "swNoCtrl":
            case "swFrozen":
            case "swRozpr":
                Karta(() => S.Urzadzenia.Zwrotnice[target].Usterka(type, clear));
                break;
            case "kzLink":
                S.LinkDown = !clear;
                if (!clear) S.Alarm("Utrata łączności stanowiska z komputerem zależności – stan obiektów nieznany, polecenia nie są przyjmowane", "System", "", "kzlink", () => S.LinkDown);
                else S.Log("Zdarzenie", "Przywrócono łączność stanowiska z komputerem zależności", "System", "");
                break;
            case "sigLamp":
            case "sigNodata":
                if (!S.Urzadzenia.Sygnalizatory.TryGetValue(target, out var ks)) { S.Log("Instruktor", $"Sygnalizator {target} nie ma karty (sygnalizator bez sterowania KZ)", "Semafor", target); return; }
                Karta(() => ks.Usterka(type, clear));
                break;
            case "mains":
            case "mainsGenFail":
                Karta(() => S.Urzadzenia.Zasilanie.Usterka(type, clear)); target = null;
                break;
            case "nbrSilent": S.Blocks[target].NbrSilent = !clear; break;
        }
        S.Log("Instruktor", (clear ? "Usunięto usterkę: " : "Wprowadzono usterkę: ") + Faults.First(f => f.Key == type).Name + (target != null ? $" [{target}]" : ""), "Instruktor", target ?? "");
    }
}

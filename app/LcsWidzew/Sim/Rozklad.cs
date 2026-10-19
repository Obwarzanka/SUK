using LcsWidzew.Model;

namespace LcsWidzew.Sim;

/// <summary>
/// Rozkład jazdy: kolejka pociągów scenariusza (podstawowy, manewry, rozkład rzeczywisty GTFS – RealTimetable.cs).
/// O czasie przekazuje pociąg ze szlaku posterunkowi sąsiedniemu (Otoczenie), a pociąg zaczynający bieg na torze
/// stacyjnym – do ruchu pociągów.
/// </summary>
public partial class Rozklad
{
    readonly Simulator S;
    public List<TimetableEntry> Queue = new();
    int nr = 1;

    public Rozklad(Simulator s) { S = s; }

    public void Tick()
    {
        foreach (var q in Queue)
        {
            if (q.Done || q.T > S.T) continue;
            if (q.StartSec != null) { if (S.Trains.SpawnOnTrack(q)) q.Done = true; continue; }
            if (S.Otoczenie.Wyprawienie(q)) q.Done = true;
        }
    }

    // ------------------------------------------------------------------ scenariusze
    public static readonly (string Key, string Name, string Desc)[] Scenarios =
    {
        ("podstawowy", "Ruch podstawowy", "ok. 25 min: odjazdy z Łodzi Fabrycznej, przejazdy przez tunel, Niciarnianą i Widzew; ŁKA, PR, IC i towarowe; zmiana czoła, odstawienie, lokomotywy manewrowe."),
        ("usterki", "Przeszkody w działaniu urządzeń (Ie-20 rozdz. 5)", "Jak podstawowy + semafor S1 nie otwiera się, zakłócenie licznika osi, brak kontroli zwrotnicy, zanik zasilania, brak odpowiedzi Andrzejowa."),
        ("zima", "Zima – mróz i opady (Ie-20 §4 ust. 5)", "Zwrotnice 11 i 53 (Widzew) zamarzają; profilaktyczne przestawianie i §24 ust. 6."),
        ("manewry", "Manewry", "Lokomotywy manewrowe na torze 200, torach sekcji (17k) i torze 204a (Widzew) – ćwiczenie przebiegów manewrowych, także złożonych."),
        ("ic_05", "Rozkład rzeczywisty 05:00–08:00", "Wszyscy przewoźnicy wg rozkładu GTFS (dane PKP PLK, tory i perony) – ŁKA, Polregio, PKP IC; dla IC próżne z/do Olechowa i zjazdy luzem (ptrc.gay/pkpic); jednostki zaczynające bieg w ŁW wyjeżdżają z bazy ŁKA."),
        ("ic_08", "Rozkład rzeczywisty 08:00–11:00", "Jak wyżej, przedpołudnie."),
        ("ic_15", "Rozkład rzeczywisty 15:00–18:00", "Jak wyżej, popołudniowy szczyt."),
        ("ic_18", "Rozkład rzeczywisty 18:00–21:00", "Jak wyżej, wieczór.")
    };

    List<TimetableEntry> BaseTimetable() => new()
    {
        new() { T = 1, Num = "19111", Cat = "ŁKA", StartSec = "LF:JT2", Track = "Fa 2", To = "1A", Stop = true, Dep = 90 },
        new() { T = 1, Num = "81102", Cat = "IC", StartSec = "LF:JT1", Track = "Fa 1", To = "1A", Stop = true, Dep = 420 },
        new() { T = 30, Num = "18100", Cat = "IC", From = "2A", To = null, Stop = true, Track = "ŁW 2 / ŁF 2", Dep = 220, Term = new() { Reverse = true, NewNum = "18101", To = "1A", NewDep = 900, AtPost = "LF" } },
        new() { T = 80, Num = "64410", Cat = "TOW", From = "2C", To = "1O", Stop = false, Track = "5" },
        new() { T = 150, Num = "19511", Cat = "ŁKA", From = "1M", To = "1M", Stop = true, Track = "4", Dep = 420, Term = new() { Reverse = true, NewNum = "15912", To = "1M" } },
        new() { T = 200, Num = "91017", Cat = "ŁKA", StartSec = "LF:JT6", Track = "ŁF 6 → ŁW 8", To = null, Stop = true, Dep = 260, Term = new() { Odstaw = true, AtPost = "LW" } },
        new() { T = 300, Num = "46021", Cat = "TOW", From = "2O", To = "1C", Stop = false, Track = "3" },
        new() { T = 470, Num = "19213", Cat = "PR", From = "2C", To = "1A", Stop = true, Track = "5", Dep = 650 },
        new() { T = 560, Num = "19210", Cat = "PR", From = "2A", To = null, Stop = true, Track = "ŁW 2 / ŁF", Dep = 760, Term = new() { Reverse = true, NewNum = "19211", To = "1A", NewDep = 1200, AtPost = "LF" } }
    };

    List<TimetableEntry> Locos() => new()
    {
        new() { T = 1, Num = "SM42", Cat = "MAN", StartSec = "LW:it10k", StartDir = 1, Loco = true },
        new() { T = 1, Num = "SM31", Cat = "MAN", StartSec = "LW:it17k", StartDir = 1, Loco = true },
        new() { T = 1, Num = "SM48", Cat = "MAN", StartSec = "LW:it35k", StartDir = -1, Loco = true },
    };

    public void Load(string key)
    {
        nr = 1;
        // rozkład układu z pliku <układ>.rozklad.json – zamiast wbudowanego ruchu podstawowego (Sim/RozkladPlik.cs)
        var plik = key == "podstawowy" ? RozkladPlik.Wczytaj(S.L.SourcePath) : null;
        Queue = plik ?? (key == "manewry" ? Locos() : key.StartsWith("ic_") ? RealTimetable(int.Parse(key[3..]), 3) : BaseTimetable());
        if (plik == null && (key == "podstawowy" || key.StartsWith("ic_"))) Queue.AddRange(Locos().Take(1));
        Queue = Queue.Where(q => q.StartSec != null ? S.L.Sections.ContainsKey(q.StartSec) : S.L.Blocks.ContainsKey(q.From) && !S.L.Blocks[q.From].Internal).ToList();
    }

    public void Enqueue(string from, string cat, string num, string to, bool stop)
    {
        Queue.Add(new TimetableEntry { T = S.T, Num = string.IsNullOrWhiteSpace(num) ? (90000 + nr++).ToString() : num, Cat = cat, From = from, To = string.IsNullOrEmpty(to) ? null : to, Stop = stop });
        S.Log("Instruktor", $"Dodano pociąg do kolejki ze szlaku {from}", "Instruktor", from);
    }
}

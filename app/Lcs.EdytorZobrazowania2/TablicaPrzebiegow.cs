using Lcs.Model;
using LcsWidzew.Sim;
using Stacja = LcsWidzew.Model.Station;

namespace Lcs.EdytorZobrazowania2;

/// <summary>
/// Etap 3 (ROZWOJ.md, decyzja właściciela 2026-10-26): tablica przebiegów projektowana w edytorze zobrazowania razem z układem.
/// Program tylko proponuje przebiegi wyznaczone z układu (ten sam kod co komputer zależności symulatora, <c>Lcs.Core</c>);
/// do tablicy trafia to, co przyjmie człowiek (<see cref="ObrProject.Przebiegi"/>). Tu bez okien: propozycje, kontrola, test nastawienia.
/// </summary>
public static class TablicaPrzebiegow
{
    /// <summary>Model symulatora z układu, przebiegi możliwe w układzie i mapy eksportu (semafory, tory symulatora → km UT).</summary>
    public record Model(Stacja St, List<RouteDef> Routes, DoSymulatora.Wynik W)
    {
        /// <summary>Komputer zależności na modelu <see cref="St"/> – wyszukiwanie dróg własnego przebiegu (<see cref="Drogi"/>).</summary>
        public Simulator Sim { get; init; }
    }

    /// <summary>Model symulatora z UT i obrazów oraz wszystkie przebiegi wyznaczone z układu (do 6 wariantów na koniec) – bez projektu przebiegów.</summary>
    public static Model ZUkladu(UtProject ut, ZobProject zob)
    {
        var w = DoSymulatora.Buduj(ut, zob, "edytor zobrazowania – przebiegi");
        var st = Stacja.Parse(w.Json.ToJsonString());
        var sim = new Simulator(st, routes: false);
        var res = new List<RouteDef>();
        foreach (var s in st.Signals.Values.Where(s => s.Type is "wjazdowy" or "wyjazdowy" or "tm"))
        {
            var raw = sim.RawRoutes(s.Name);
            foreach (var r in raw) r.VCalc = sim.RouteSpeed(r);
            res.AddRange(raw);
        }
        return new Model(st, Porzadek(res).ToList(), w) { Sim = sim };
    }

    /// <summary>
    /// Własny przebieg (decyzja właściciela 2026-10-27): drogi od sygnalizatora w układzie – bez limitu wariantów propozycji – spełniające
    /// warunki człowieka: rodzaj, koniec (null – dowolny), położenia wybranych zwrotnic (droga przez nie w tym położeniu), odcinek „przez”.
    /// Tym samym kodem co symulator (projekt przebiegów: dozwolone położenia – wszystkie poza przeciwnymi do wybranych), więc znaleziona
    /// droga po przyjęciu do tablicy trafi do symulatora.
    /// </summary>
    public static List<RouteDef> Drogi(Model m, string od, string typ, string @do = null, IEnumerable<(string Num, string Pos)> zwr = null, string przez = null)
    {
        var wym = zwr?.ToList() ?? new();
        if (!m.St.Signals.ContainsKey(od)) return new();
        var dozw = m.St.Switches.Keys.SelectMany(k => new[] { k + "+", k + "-" })
            .Where(x => !wym.Any(w => x == w.Num + (w.Pos == "+" ? "-" : "+")));
        var stare = m.St.Przebiegi;
        List<RouteDef> raw;
        m.St.Przebiegi = new() { new LcsWidzew.Model.RouteRule { Start = od, End = @do ?? "", Type = typ, Sw = string.Join(" ", dozw) } };
        try { raw = m.Sim.RawRoutes(od); }
        finally { m.St.Przebiegi = stare; }
        var res = raw.Where(r => r.Type == typ && (@do == null || r.End == @do) && wym.All(w => r.Sw.Contains(w)) && (przez == null || r.Secs.Contains(przez))).ToList();
        foreach (var r in res) r.VCalc = m.Sim.RouteSpeed(r);
        return Porzadek(res).ToList();
    }

    /// <summary>Droga przebiegu z tablicy w układzie (także własnego – spoza propozycji); null – nie ma jej w układzie.</summary>
    public static RouteDef Droga(Model m, ObrPrzebieg p) =>
        m.Routes.FirstOrDefault(r => Pasuje(p, r)) ??
        Drogi(m, p.Od, p.Typ, p.Do, Zwrotnice(p.Zwr)).FirstOrDefault(r => Pasuje(p, r));

    /// <summary>„LF:12+ LF:14-” → pary (zwrotnica, położenie).</summary>
    public static List<(string Num, string Pos)> Zwrotnice(string zwr) =>
        (zwr ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(x => x.Length > 1).Select(x => (x[..^1], x[^1..])).ToList();

    /// <summary>Kolejność w tablicy: pociągowe, potem manewrowe; początek, koniec, wariant (najmniej zwrotnic na bok, najkrótszy).</summary>
    public static IEnumerable<RouteDef> Porzadek(IEnumerable<RouteDef> l) =>
        l.OrderBy(r => r.Type == "P" ? 0 : 1).ThenBy(r => Stacja.Lbl(r.Start), StringComparer.Ordinal).ThenBy(r => Stacja.Lbl(r.End), StringComparer.Ordinal).ThenBy(r => r.Variant);

    public static ObrPrzebieg Z(RouteDef r, bool zasadniczy = false) => new() { Typ = r.Type, Od = r.Start, Do = r.End, Zwr = r.SwKey, Zasadniczy = zasadniczy };
    public static bool Pasuje(ObrPrzebieg p, RouteDef r) => p.Typ == r.Type && p.Od == r.Start && p.Do == r.End && (p.Zwr ?? "") == r.SwKey;

    /// <summary>Opis przebiegu do list: „P A → C1 przez 1+ 3-”.</summary>
    public static string Opis(string typ, string od, string @do, string zwr) =>
        $"{typ} {Stacja.Lbl(od)} → {Stacja.Lbl(@do)}" + (string.IsNullOrEmpty(zwr) ? "" : " przez " + string.Join(" ", zwr.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Stacja.Lbl)));
    public static string Opis(ObrPrzebieg p) => Opis(p.Typ, p.Od, p.Do, p.Zwr);

    /// <summary>Uwaga kontroli tablicy: błąd (przebieg nie pójdzie do symulatora albo jest niejednoznaczny) albo uwaga.</summary>
    public record Uwaga(bool Blad, string Tekst);

    /// <summary>
    /// Kontrola tablicy wobec układu: przebieg, którego nie ma w układzie (np. po zmianie UT), powtórzony, dwa zasadnicze dla jednej pary;
    /// uwagi – kilka wariantów bez zasadniczego, semafor bez przebiegu pociągowego, sygnalizator bez żadnego przebiegu.
    /// </summary>
    public static List<Uwaga> Sprawdz(List<ObrPrzebieg> proj, Stacja st, List<RouteDef> uklad, Model m = null)
    {
        var res = new List<Uwaga>();
        if (proj == null) { res.Add(new(false, "Tablica przebiegów nie jest zaprojektowana – symulator wyznaczy przebiegi z układu sam")); return res; }
        // własny przebieg spoza propozycji – w układzie, gdy jest taka droga (m – model do wyszukania)
        foreach (var p in proj.Where(p => !uklad.Any(r => Pasuje(p, r)) && (m == null || Droga(m, p) == null)))
            res.Add(new(true, $"{Opis(p)}: nie ma takiego przebiegu w układzie (zmieniony UT?) – symulator go pominie"));
        foreach (var g in proj.GroupBy(p => p.Klucz).Where(g => g.Count() > 1))
            res.Add(new(true, $"{Opis(g.First())}: wpisany {g.Count()} razy"));
        foreach (var g in proj.GroupBy(p => (p.Typ, p.Od, p.Do)))
        {
            int zas = g.Count(p => p.Zasadniczy);
            if (zas > 1) res.Add(new(true, $"{Opis(g.Key.Typ, g.Key.Od, g.Key.Do, null)}: {zas} przebiegi zasadnicze – może być jeden"));
            else if (zas == 0 && g.Count() > 1) res.Add(new(false, $"{Opis(g.Key.Typ, g.Key.Od, g.Key.Do, null)}: {g.Count()} warianty bez zasadniczego – zasadniczym będzie ten z najmniejszą liczbą zwrotnic na bok"));
        }
        foreach (var s in st.Signals.Values.Where(s => s.Type is "wjazdowy" or "wyjazdowy" or "tm").OrderBy(s => Stacja.Lbl(s.Name), StringComparer.Ordinal))
        {
            bool moze = uklad.Any(r => r.Start == s.Name);
            if (!moze) continue;
            if (!proj.Any(p => p.Od == s.Name)) res.Add(new(false, $"{(s.Type == "tm" ? "tarcza" : "semafor")} {Stacja.Lbl(s.Name)}: bez żadnego przebiegu w tablicy"));
            else if (s.IsTrain && uklad.Any(r => r.Start == s.Name && r.Type == "P") && !proj.Any(p => p.Od == s.Name && p.Typ == "P"))
                res.Add(new(false, $"semafor {Stacja.Lbl(s.Name)}: bez przebiegu pociągowego"));
        }
        return res;
    }

    /// <summary>Wynik testu jednego przebiegu: sygnał zezwalający, nastawiony bez sygnału (powody) albo odrzucony.</summary>
    public record Test(ObrPrzebieg P, string Wynik, string Tekst)
    {
        public const string Sygnal = "sygnał", BezSygnalu = "bez sygnału", Odrzucony = "odrzucony";
    }

    /// <summary>
    /// Test każdego przebiegu tablicy (ROZWOJ.md etap 3: „testy każdego przebiegu”): osobno, w stanie zasadniczym, polecenie dyżurnego
    /// z położeniami zwrotnic jako punktami pośrednimi (dokładnie ten wariant), do 30 s czasu symulacji. Nic nie zapisuje.
    /// </summary>
    public static List<Test> Testuj(UtProject ut, ZobProject zob, List<ObrPrzebieg> proj, IEnumerable<ObrPrzebieg> tylko = null)
    {
        var res = new List<Test>();
        if (proj == null || proj.Count == 0) return res;
        var json = DoSymulatora.Buduj(ut, zob, "edytor zobrazowania – test przebiegów", proj).Json.ToJsonString();
        foreach (var p in (tylko ?? proj).ToList())
        {
            var st = Stacja.Parse(json);
            var sim = new Simulator(st);
            var user = new User { Name = "Edytor", Role = "dyżurny ruchu" };
            sim.AddDesk("PC", "Test przebiegów w edytorze zobrazowania", true).User = user; sim.User = user;
            var rd = sim.RouteTable.GetValueOrDefault(p.Od)?.FirstOrDefault(r => Pasuje(p, r));
            if (rd == null) { res.Add(new(p, Test.Odrzucony, "brak w tablicy symulatora (nie ma go w układzie)")); continue; }
            sim.Page = st.Pages.FirstOrDefault(x => x.Posts.Contains(Stacja.PostOf(p.Od)))?.Id ?? sim.Page;
            var cmd = $"{p.Od} {string.Join(" ", rd.Sw.Select(q => q.Num + q.Pos))}{(rd.Sw.Count > 0 ? " " : "")}{p.Do} {(p.Typ == "M" ? "Manewr" : "Pociąg")}";
            var r0 = sim.Exec(cmd);
            if (!r0.Ok) { res.Add(new(p, Test.Odrzucony, r0.Msg)); continue; }
            Route nast = null;
            for (double t = 0; t < 30; t += 0.25)
            {
                sim.Tick(0.25);
                nast = sim.Routes.Values.FirstOrDefault(r => Pasuje(p, r));
                if (nast?.State == "set") break;
            }
            if (nast == null) res.Add(new(p, Test.Odrzucony, "przebieg zniknął w czasie nastawiania"));
            else if (nast.State == "set") res.Add(new(p, Test.Sygnal, $"{r0.Msg}; sygnał zezwalający"));
            else res.Add(new(p, Test.BezSygnalu, string.Join(", ", sim.SignalConditions(nast).Take(3)).DefaultIfEmptyText("brak sygnału zezwalającego po 30 s")));
        }
        return res;
    }

    static string DefaultIfEmptyText(this string s, string d) => string.IsNullOrWhiteSpace(s) ? d : s;
}

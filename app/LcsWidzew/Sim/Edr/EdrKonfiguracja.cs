using System.Globalization;
using LcsWidzew.Model;

namespace LcsWidzew.Sim;

/// <summary>
/// Posterunek ruchu w EDR (Konfiguracja – administrator, uwaga właściciela 2026-10-28): kod, nazwa, tory stacyjne (puste – z układu)
/// i dzienniki ruchu. Bez konfiguracji posterunki i dzienniki wynikają ze szlaków układu.
/// </summary>
public class EdrPosterunekUst
{
    public string Kod = "", Nazwa = "";
    public List<string> ToryStacyjne = new();
    public List<EdrDziennikUst> Dzienniki = new();
    public EdrPosterunekUst Kopia() => new() { Kod = Kod, Nazwa = Nazwa, ToryStacyjne = new(ToryStacyjne ?? new()), Dzienniki = (Dzienniki ?? new()).Select(d => d.Kopia()).ToList() };
}

/// <summary>
/// Dziennik ruchu posterunku: nazwa, posterunek sąsiedni, linia, rodzaj urządzeń, szlaki układu (tory szlakowe z układu) albo tory wpisane ręcznie,
/// zakładka – dzienniki z tym samym numerem stoją obok siebie w jednej zakładce (najwyżej dwa).
/// </summary>
public class EdrDziennikUst
{
    public string Id = "", Nazwa = "", Sasiad = "", Linia = "", Typ = "sbl";
    public List<string> Szlaki = new(), Tory = new();
    public int Zakladka;
    public EdrDziennikUst Kopia() { var d = (EdrDziennikUst)MemberwiseClone(); d.Szlaki = new(Szlaki ?? new()); d.Tory = new(Tory ?? new()); return d; }
}

/// <summary>Szlak z układu (do wyboru w konfiguracji dzienników): id, posterunki na końcach, tor, linia, sąsiad.</summary>
public class EdrSzlakUkladu { public string Id, Tor, Linia, Sasiad, Typ; public List<string> Posterunki = new(); }

/// <summary>Pole „Czas bez wypadku …” nad zegarem: napis i początek liczenia (ustawia administrator).</summary>
public class EdrBezWypadku { public string Tekst = "Czas bez wypadku spow. przez pracownika PLK", Od; }

/// <summary>Trasa pociągu (zakładka „Trasa pociągu”): posterunek, przyjazd i odjazd wg rozkładu, tor.</summary>
/// <summary>
/// Wiersz zakładki „Trasa pociągu” – kolumny jak w EDR.exe (E8c): przyjazd plan. / rzecz. (data, godz.), +/-, potwierdzenie (Z), Lp., stacja,
/// postój zamówiony / obliczony, typ postoju, P T, odjazd plan. / rzecz., +/-, rodzaj, numer; gotowość do odjazdu i skład – bez danych w rozkładzie.
/// </summary>
public class EdrTrasa
{
    public string Posterunek { get; set; }
    /// <summary>Przyjazd i odjazd planowy „dd-MM HH:mm” (zgodność z wcześniejszym zapisem).</summary>
    public string Przyjazd { get; set; }
    public string Odjazd { get; set; }
    public string Tor { get; set; }
    public string Postoj { get; set; }
    public string PrzPlanData { get; set; } = "";
    public string PrzPlanGodz { get; set; } = "";
    public string PrzRzeczData { get; set; } = "";
    public string PrzRzeczGodz { get; set; } = "";
    public string PrzOdch { get; set; } = "";
    /// <summary>„✓” – czas przejazdu przez posterunek zarejestrowany w ruchu.</summary>
    public string Z { get; set; } = "";
    public string Lp { get; set; } = "";
    public string PZam { get; set; } = "";
    public string PObl { get; set; } = "";
    public string Gwiazdka { get; set; } = "";
    public string PT { get; set; } = "";
    public string OdjPlanData { get; set; } = "";
    public string OdjPlanGodz { get; set; } = "";
    public string OdjRzeczData { get; set; } = "";
    public string OdjRzeczGodz { get; set; } = "";
    public string OdjOdch { get; set; } = "";
    public string Rodz { get; set; } = "";
    public string Nr { get; set; } = "";
    public string Gotowosc { get; set; } = "";
    public string CzasWpisu { get; set; } = "";
    public string Maszynista { get; set; } = "";
    public string SeriaPl { get; set; } = "";
    public string DlPl { get; set; } = "";
    public string BruttoPl { get; set; } = "";
    public string SeriaRz { get; set; } = "";
    public string DlRz { get; set; } = "";
    public string BruttoRz { get; set; } = "";
}

public partial class Edr
{
    /// <summary>Posterunki i dzienniki z konfiguracji (null – ze szlaków układu).</summary>
    List<EdrPosterunekUst> posterunkiUst;
    /// <summary>Ustawienia zasadnicze dzienników z pliku (po id dziennika) – przyjmowane przy budowie dzienników.</summary>
    Dictionary<string, EdrUstawienia> zapisaneUst = new();
    public EdrBezWypadku BezWypadku = new();
    /// <summary>Błąd konfiguracji z pliku (pominiętej) – pokazywany w oknie konfiguracji.</summary>
    public string BladKonfiguracji;
    /// <summary>Uwagi własne dyżurnego (posterunek|pociąg) i uwagi dyspozytora: eksploatacyjne, przewoźnika (pociąg); pociągi nie kursujące i potwierdzone przez dyspozytora.</summary>
    readonly Dictionary<string, string> uwagiWlasne = new(), uwagiEksp = new(), uwagiPrzew = new();
    readonly Dictionary<string, bool> kursuje = new();
    readonly HashSet<string> potwDysp = new();
    public static readonly string[] Typy = { "sbl", "blok", "pol", "pol1", "tel" };

    /// <summary>Konfigurację posterunków i dzienników zmienia administrator (w symulatorze także instruktor – dyspozytor).</summary>
    bool Admin(Desk k) => (k?.User?.Role ?? S.User?.Role) is "administrator" or "instruktor";

    /// <summary>Posterunki ruchu EDR: z konfiguracji albo z układu.</summary>
    public List<EdrPosterunekUst> Posterunki => posterunkiUst ?? DefinicjeZUkladu();
    bool JestPost(string p) => p != null && Posterunki.Any(x => x.Kod == p);

    string NazwaPosterunku(string p) => posterunkiUst?.FirstOrDefault(x => x.Kod == p) is { Nazwa: { Length: > 0 } n } ? n
        : S.L.Posts.TryGetValue(p, out var nz) && !string.IsNullOrEmpty(nz) ? nz : p;

    string NazwaUkladu(string p) => S.L.Posts.TryGetValue(p, out var n) && !string.IsNullOrEmpty(n) ? n : p;

    /// <summary>
    /// Dzienniki ze szlaków układu: po jednym na posterunek, sąsiada i linię; szlak wewnętrzny między dwoma posterunkami – po dzienniku
    /// na każdym końcu. Zakładki – kolejne dzienniki posterunku parami.
    /// </summary>
    List<EdrPosterunekUst> DefinicjeZUkladu()
    {
        var res = S.L.Posts.Keys.Select(p => new EdrPosterunekUst { Kod = p, Nazwa = NazwaUkladu(p) }).ToList();
        foreach (var b in S.L.Blocks.Values)
            foreach (var e in b.Ends)
            {
                if (string.IsNullOrEmpty(e.Post)) continue;
                var post = res.FirstOrDefault(x => x.Kod == e.Post);
                if (post == null) res.Add(post = new EdrPosterunekUst { Kod = e.Post, Nazwa = e.Post });
                var drugi = b.Ends.FirstOrDefault(x => x != e && x.Post != e.Post);
                var sasiad = drugi != null ? NazwaUkladu(drugi.Post) : b.Nbr ?? "";
                var id = $"{e.Post}|{sasiad}|{b.Line}";
                var d = post.Dzienniki.FirstOrDefault(x => x.Id == id);
                if (d == null)
                    post.Dzienniki.Add(d = new EdrDziennikUst { Id = id, Sasiad = sasiad, Linia = b.Line ?? "", Typ = b.Type ?? "sbl",
                        Nazwa = string.IsNullOrEmpty(b.Line) ? sasiad : $"{sasiad} L. {b.Line}", Zakladka = post.Dzienniki.Count / 2 + 1 });
                if (!d.Szlaki.Contains(b.Id)) d.Szlaki.Add(b.Id);
            }
        return res;
    }

    /// <summary>Szlaki układu do wyboru w konfiguracji.</summary>
    public List<EdrSzlakUkladu> SzlakiUkladu() => S.L.Blocks.Values.Select(b => new EdrSzlakUkladu
    {
        Id = b.Id, Tor = b.Tor ?? "", Linia = b.Line ?? "", Sasiad = b.Nbr ?? "", Typ = b.Type ?? "",
        Posterunki = b.Ends.Select(e => e.Post).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList()
    }).ToList();

    /// <summary>
    /// Buduje dzienniki z definicji (konfiguracja albo układ). Dziennik o tym samym id zachowuje strony i wpisy; ustawienia zasadnicze –
    /// bieżące albo z pliku, jeśli pasują do torów szlakowych.
    /// </summary>
    void ZbudujDzienniki(bool zPliku = false)
    {
        var stare = Dzienniki.ToDictionary(d => d.Id);
        Dzienniki.Clear();
        foreach (var post in Posterunki)
            foreach (var dj in post.Dzienniki)
            {
                var d = stare.GetValueOrDefault(dj.Id) ?? new EdrDziennik { Id = dj.Id };
                d.Post = post.Kod; d.Sasiad = dj.Sasiad ?? ""; d.Linia = dj.Linia ?? ""; d.Typ = dj.Typ ?? "sbl"; d.Zakladka = dj.Zakladka;
                d.Nazwa = !string.IsNullOrWhiteSpace(dj.Nazwa) ? dj.Nazwa : string.IsNullOrEmpty(d.Linia) ? d.Sasiad : $"{d.Sasiad} L. {d.Linia}";
                d.Szlaki = (dj.Szlaki ?? new()).Where(id => S.L.Blocks.TryGetValue(id, out var bb) && bb.Ends.Any(e => e.Post == post.Kod)).Distinct().ToList();
                d.Tory = new();
                // ustawienia domyślne: kierunek parzysty z edytora UT, kierunek zasadniczy nieustalony – ustala go człowiek w ustawieniach dziennika;
                // blokada jednokierunkowa – kierunek stały z układu
                var u = new EdrUstawienia { WymDw = d.Typ is "pol" or "pol1" or "tel" };
                foreach (var id in d.Szlaki)
                {
                    var b = S.L.Blocks[id]; var e = b.Ends.First(x => x.Post == post.Kod);
                    if (!string.IsNullOrEmpty(b.Tor) && !d.Tory.Contains(b.Tor)) d.Tory.Add(b.Tor);
                    if (b.EvenDir != 0 && u.Parzysty == 0) u.Parzysty = b.EvenDir == e.Outward ? 1 : -1;
                    u.WymDw |= b.Type is "pol" or "pol1" or "tel";
                    u.Tory.Add(new EdrTorUstawienia
                    {
                        Szlak = b.Id, Tor = b.Tor ?? "", Rodzaj = b.OneWay ? "Jednokierunkowy" : "Dwukierunkowy", Urzadzenie = b.TypeName,
                        Zasadniczy = b.OneWay && b.DefDir != 0 ? (b.DefDir == e.Outward ? 1 : -1) : 0,
                        Podstawa = b.Type switch { "pol" or "pol1" => Podstawy[1], "tel" => Podstawy[3], _ => Podstawy[0] }
                    });
                }
                // tory wpisane ręcznie (dziennik bez szlaku w układzie)
                foreach (var t in (dj.Tory ?? new()).Select(x => x.Trim()).Where(x => x != "" && !d.Tory.Contains(x)).Distinct())
                {
                    d.Tory.Add(t);
                    u.Tory.Add(new EdrTorUstawienia { Szlak = $"{dj.Id}#{t}", Tor = t, Rodzaj = "Dwukierunkowy", Urzadzenie = "",
                        Podstawa = d.Typ switch { "pol" or "pol1" => Podstawy[1], "tel" => Podstawy[3], _ => Podstawy[0] } });
                }
                u.Tory.Sort((a, b) => PorownajTory(a.Tor, b.Tor));
                if (u.Tory.Count == 2) { u.Tory[0].Strona = "lewy"; u.Tory[1].Strona = "prawy"; }
                d.Ustawienia = u;
                var z = stare.ContainsKey(dj.Id) && !(zPliku && zapisaneUst.ContainsKey(dj.Id)) ? stare[dj.Id].Ustawienia : zapisaneUst.GetValueOrDefault(dj.Id);
                Przyjmij(d, z != null && z != u && Sprawdz(d, z) == null ? z : u);
                d.Tory.Sort(PorownajTory);
                Dzienniki.Add(d);
            }
        blokOdcinka.Clear();
        foreach (var b in S.L.Blocks.Values) foreach (var o in b.SecsWE) blokOdcinka.TryAdd(o, b.Id);
        var szlakowe = blokOdcinka.Keys.ToHashSet();
        ToryStacyjne.Clear();
        foreach (var p in Posterunki)
            ToryStacyjne[p.Kod] = p.ToryStacyjne?.Count > 0 ? p.ToryStacyjne.Distinct().OrderBy(x => x, Comparer<string>.Create(PorownajTory)).ToList()
                : S.L.Edges.Values.Where(x => !string.IsNullOrEmpty(x.Track) && x.Sec != null && Station.PostOf(x.Sec) == p.Kod && !szlakowe.Contains(x.Sec))
                    .Select(x => x.Track).Distinct().OrderBy(x => x, Comparer<string>.Create(PorownajTory)).ToList();
        Wersja++;
    }

    /// <summary>Błąd konfiguracji posterunków (null – poprawna); puste id dziennika dostaje id z kodu posterunku i nazwy.</summary>
    string SprawdzPosterunki(List<EdrPosterunekUst> l)
    {
        if (l == null || l.Count == 0) return "Dodaj co najmniej jeden posterunek ruchu";
        foreach (var p in l) { p.Kod = (p.Kod ?? "").Trim(); p.Nazwa = (p.Nazwa ?? "").Trim(); p.Dzienniki ??= new(); p.ToryStacyjne ??= new(); }
        if (l.Any(p => p.Kod == "")) return "Posterunek musi mieć kod";
        if (l.GroupBy(p => p.Kod).FirstOrDefault(g => g.Count() > 1) is { } pg) return $"Kod posterunku {pg.Key} powtarza się";
        var ids = new HashSet<string>();
        foreach (var p in l)
        {
            foreach (var d in p.Dzienniki)
            {
                d.Nazwa = (d.Nazwa ?? "").Trim(); d.Sasiad = (d.Sasiad ?? "").Trim(); d.Linia = (d.Linia ?? "").Trim(); d.Szlaki ??= new(); d.Tory ??= new();
                if (d.Nazwa == "" && d.Sasiad == "") return $"Dziennik posterunku {p.Kod} musi mieć nazwę albo posterunek sąsiedni";
                if (!Typy.Contains(d.Typ)) return $"Dziennik {d.Nazwa}: nieznany rodzaj urządzeń „{d.Typ}”";
                if (d.Zakladka < 0) return $"Dziennik {d.Nazwa}: błędny numer zakładki";
                foreach (var s in d.Szlaki)
                {
                    if (!S.L.Blocks.TryGetValue(s, out var b)) return $"Dziennik {d.Nazwa}: nie ma szlaku {s} w układzie";
                    if (!b.Ends.Any(e => e.Post == p.Kod)) return $"Dziennik {d.Nazwa}: szlak {s} nie dochodzi do posterunku {p.Kod}";
                }
                if (string.IsNullOrWhiteSpace(d.Id)) { var baza = $"{p.Kod}|{(d.Sasiad != "" ? d.Sasiad : d.Nazwa)}|{d.Linia}"; d.Id = baza; for (int i = 2; ids.Contains(d.Id) || l.SelectMany(x => x.Dzienniki).Count(x => x.Id == d.Id) > 1; i++) d.Id = baza + "#" + i; }
                if (!ids.Add(d.Id)) return $"Dziennik {d.Nazwa}: powtórzony identyfikator";
            }
            if (p.Dzienniki.SelectMany(d => d.Szlaki).GroupBy(x => x).FirstOrDefault(g => g.Count() > 1) is { } sg) return $"Szlak {sg.Key} jest w dwóch dziennikach posterunku {p.Kod}";
            if (p.Dzienniki.Where(d => d.Zakladka > 0).GroupBy(d => d.Zakladka).FirstOrDefault(g => g.Count() > 2) is { } zg) return $"Posterunek {p.Kod}: w zakładce {zg.Key} mogą być najwyżej dwa dzienniki";
        }
        return null;
    }

    /// <summary>Czas od ostatniego wypadku do bieżącej chwili symulacji: „31 dni 23 godz. 39 min.”.</summary>
    public string CzasBezWypadku()
    {
        var teraz = Dzien0.AddSeconds(S.StartClock + S.T);
        var od = DateTime.TryParseExact(BezWypadku.Od ?? "", "dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var o) ? o : Dzien0.AddSeconds(S.StartClock);
        var r = teraz - od; if (r < TimeSpan.Zero) r = TimeSpan.Zero;
        int dni = (int)r.TotalDays;
        return $"{dni} {(dni == 1 ? "dzień" : "dni")} {r.Hours} godz. {r.Minutes} min.";
    }

    /// <summary>Polecenia konfiguracji (administrator) i dyspozytora: posterunki, czas bez wypadku, uwagi, kursuje / nie kursuje, potwierdzenie.</summary>
    CmdResult Konfiguracja(Desk k, EdrPolecenie p)
    {
        switch (p.Rodzaj)
        {
            case "posterunki":
            case "posterunkizukladu":
            {
                if (!Admin(k)) return new(false, "Posterunki i dzienniki konfiguruje administrator");
                List<EdrPosterunekUst> l = null;
                if (p.Rodzaj == "posterunki")
                {
                    l = (p.Posterunki ?? new()).Select(x => x.Kopia()).ToList();
                    if (SprawdzPosterunki(l) is string blad) return new(false, blad);
                }
                var przed = Dzienniki.Where(d => d.Strony.Any(s => s.Wiersze.Any(w => w.Rodzaj != "tekst"))).ToList();
                posterunkiUst = l; BladKonfiguracji = null;
                ZbudujDzienniki();
                var usuniete = przed.Where(d => !Dzienniki.Any(x => x.Id == d.Id)).Select(d => d.Nazwa).ToList();
                foreach (var post in Posterunki) foreach (var d in Dzienniki.Where(x => x.Post == post.Kod && x.Prowadzi == null)) Strona(d);
                try { ZapiszUstawienia(); } catch (Exception ex) { return new(false, $"Konfiguracja przyjęta, ale nie zapisana w pliku: {ex.Message}"); }
                return new(true, $"Posterunki ruchu: {Posterunki.Count}, dzienniki ruchu: {Dzienniki.Count}" + (l == null ? " (z układu)" : "") +
                    (usuniete.Count > 0 ? $"; usunięte dzienniki z wpisami: {string.Join(", ", usuniete)}" : ""));
            }
            case "bezwypadku":
            {
                if (!Admin(k)) return new(false, "Czas bez wypadku ustawia administrator");
                var od = (p.Wartosc ?? "").Trim();
                if (od != "" && !DateTime.TryParseExact(od, "dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) return new(false, "Data w postaci dd-MM-rrrr gg:mm");
                BezWypadku = new EdrBezWypadku { Tekst = string.IsNullOrWhiteSpace(p.Pole) ? new EdrBezWypadku().Tekst : p.Pole.Trim(), Od = od == "" ? null : od };
                Wersja++;
                try { ZapiszUstawienia(); } catch (Exception ex) { return new(false, $"Przyjęto, ale nie zapisano w pliku: {ex.Message}"); }
                return new(true, $"{BezWypadku.Tekst}: {CzasBezWypadku()}");
            }
            case "uwagi":
            {
                if (string.IsNullOrEmpty(p.Klucz)) return new(false, "Nie wybrano pociągu");
                var v = (p.Wartosc ?? "").Trim();
                if (p.Pole == "wlasne")
                {
                    if (!JestPost(p.Post)) return new(false, "Nie ma takiego posterunku");
                    if (!MojPost(p.Post, k)) return new(false, $"Posterunek {NazwaPosterunku(p.Post)} prowadzi inne stanowisko");
                    if (v == "") uwagiWlasne.Remove(p.Post + "|" + p.Klucz); else uwagiWlasne[p.Post + "|" + p.Klucz] = v;
                }
                else if (p.Pole is "ekspl" or "przew")
                {
                    if (!Admin(k)) return new(false, "Uwagi eksploatacyjne i przewoźnika wpisuje dyspozytor");
                    var sl = p.Pole == "ekspl" ? uwagiEksp : uwagiPrzew;
                    if (v == "") sl.Remove(p.Klucz); else sl[p.Klucz] = v;
                }
                else return new(false, "Nieznane pole uwag");
                Wersja++;
                return new(true, "Uwagi zapisane");
            }
            case "kursuje":
            case "potwierdz":
            {
                if (!Admin(k)) return new(false, p.Rodzaj == "kursuje" ? "Kursuje / nie kursuje zaznacza dyspozytor" : "Czas potwierdza dyspozytor");
                if (string.IsNullOrEmpty(p.Klucz)) return new(false, "Nie wybrano pociągu");
                if (p.Rodzaj == "kursuje") kursuje[p.Klucz] = p.Wartosc != "NK";
                else if (p.Wartosc == "nie") potwDysp.Remove(p.Klucz); else potwDysp.Add(p.Klucz);
                Wersja++;
                return new(true, p.Rodzaj == "kursuje" ? (p.Wartosc == "NK" ? "Pociąg nie kursuje" : "Pociąg kursuje") : "Czas potwierdzony przez dyspozytora");
            }
        }
        return null;
    }
}

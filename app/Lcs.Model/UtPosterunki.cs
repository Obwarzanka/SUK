namespace Lcs.Model;

/// <summary>
/// Posterunek ruchu (stacja, posterunek odgałęźny, mijanka…) – obszar w UT (PLAN_EDYTOR_UT.md § 7 pkt 5): tory przypisane do posterunku
/// (<see cref="UtTrack.Post"/>), rozjazdy i urządzenia dziedziczą posterunek z toru. Kod (np. „LW”) jest przedrostkiem nazw obiektów
/// w symulatorze, nazwa – opisem na obrazach.
/// </summary>
public class UtPost
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    /// <summary>Kod posterunku (np. „LW”) – wpisuje projektant.</summary>
    public string Kod { get; set; } = "";
    /// <summary>Nazwa (np. „Łódź Widzew”).</summary>
    public string Nazwa { get; set; } = "";
}

/// <summary>
/// Szlak z blokadą liniową (PLAN_EDYTOR_ZOBRAZOWANIA.md § 6 pkt 3, decyzja właściciela 2026-10-17: szlaki definiuje się w UT).
/// Końce: semafor wjazdowy posterunku albo koniec toru na granicy obszaru (posterunek sąsiedni spoza obszaru). Tory szlaku
/// program wyznacza z układu – po torach między końcami (<see cref="Szlaki.Droga"/>).
/// </summary>
public class UtLine
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    /// <summary>Oznaczenie szlaku (np. „1A”, „2ŁW”) – wpisuje projektant.</summary>
    public string Numer { get; set; } = "";
    /// <summary>Nazwa pełna (np. „Łódź Widzew – Łódź Andrzejów”) – propozycja z posterunków na końcach (<see cref="Szlaki.ProponujNazwy"/>).</summary>
    public string Nazwa { get; set; } = "";
    /// <summary>Nazwa skrócona (np. „ŁW – ŁA”).</summary>
    public string NazwaSkr { get; set; } = "";
    /// <summary>Numer linii kolejowej (np. „17”).</summary>
    public string Linia { get; set; } = "";
    /// <summary>Numer toru szlakowego (np. „1”, „2”).</summary>
    public string Tor { get; set; } = "";
    /// <summary>Rodzaj blokady liniowej (<see cref="Typy"/>, kody jak w symulatorze).</summary>
    public string Typ { get; set; } = Sbl;
    /// <summary>Stawność SBL: 2, 3 albo 4 (KOMPUTERY.pdf 1.6.1).</summary>
    public int Stawnosc { get; set; } = 3;
    /// <summary>Dwa końce szlaku.</summary>
    public List<UtLineEnd> Konce { get; set; } = new();

    public const string Sbl = "sbl", Sbl1 = "sbl1", Pol = "pol", Pol1 = "pol1", Tel = "tel";
    public static readonly (string Key, string Name)[] Typy =
    {
        (Sbl, "SBL dwukierunkowa (samoczynna)"), (Sbl1, "SBL jednokierunkowa"), (Pol, "PBL dwukierunkowa (półsamoczynna)"),
        (Pol1, "PBL jednokierunkowa"), (Tel, "bez blokady – telefoniczne zapowiadanie"),
    };
    public bool Samoczynna => Typ is Sbl or Sbl1;
}

/// <summary>Koniec szlaku: semafor wjazdowy (<see cref="Semafor"/>) albo koniec toru na granicy obszaru (<see cref="Track"/> + <see cref="AtStart"/>).</summary>
public class UtLineEnd
{
    /// <summary>Semafor wjazdowy posterunku na końcu szlaku (id urządzenia); null – koniec toru na granicy obszaru.</summary>
    public string Semafor { get; set; }
    public string Track { get; set; }
    public bool AtStart { get; set; }
    /// <summary>Posterunek sąsiedni spoza obszaru (koniec na granicy obszaru), np. „Łódź Andrzejów”.</summary>
    public string Sasiad { get; set; } = "";
    /// <summary>Oznaczenie końca szlaku na pulpicie (np. „1A”, „1G”) – puste: numer szlaku.</summary>
    public string Oznaczenie { get; set; } = "";
}

/// <summary>Kawałek toru należący do szlaku: tor, km od–do [m w terenie od początku toru] (S0 ≤ S1).</summary>
public record SzlakKawalek(string Track, double S0, double S1)
{
    public bool Contains(double s, double tol = 0.01) => s > S0 + tol && s < S1 - tol;
}

/// <summary>Droga szlaku: kawałki torów od końca 1 do końca 2, semafory odstępowe po drodze, uwagi; Err – drogi brak.</summary>
public record SzlakDroga(List<SzlakKawalek> Kawalki, List<UtDevice> Semafory, List<string> Uwagi, string Err)
{
    public double Dlugosc => Kawalki.Sum(k => k.S1 - k.S0);
}

/// <summary>Szlaki: droga po torach, przynależność miejsca toru do szlaku albo posterunku, kontrola.</summary>
public static class Szlaki
{
    /// <summary>Opis końca szlaku (do paneli i uwag).</summary>
    public static string Opis(UtProject p, UtLineEnd e)
    {
        if (e.Semafor != null)
        {
            var d = p.Devices.FirstOrDefault(x => x.Id == e.Semafor);
            var post = d != null ? Posterunki.Of(p, d.Track) : null;
            return d == null ? "semafor (usunięty)" : $"semafor {d.Name}" + (post != null ? $" ({post.Kod})" : "");
        }
        var t = p.Tracks.FirstOrDefault(x => x.Id == e.Track);
        return $"{(e.AtStart ? "początek" : "koniec")} toru {(t?.Number.Length > 0 ? t.Number : "?")}" + (e.Sasiad.Length > 0 ? $" – {e.Sasiad}" : " – granica obszaru");
    }

    /// <summary>Miejsce i kierunek wyjścia z końca szlaku w głąb szlaku: tor, km, kierunek wzdłuż km (+1 / −1).</summary>
    static (string Track, double S, int Dir)? Start(UtProject p, UtLineEnd e, Dictionary<string, TrasaResult> r)
    {
        if (e.Semafor != null)
        {
            var d = p.Devices.FirstOrDefault(x => x.Id == e.Semafor);
            // semafor wjazdowy działa dla jazdy w stronę posterunku – szlak leży w drugą stronę
            return d == null || !r.ContainsKey(d.Track) ? null : (d.Track, d.S, -d.Dir);
        }
        if (e.Track == null || !r.TryGetValue(e.Track, out var rt)) return null;
        return (e.Track, e.AtStart ? 0 : rt.Length, e.AtStart ? 1 : -1);
    }

    /// <summary>
    /// Droga szlaku po torach od końca 1 do końca 2: z końca 1 w głąb szlaku, przez styki końców torów (nie przez rozjazdy),
    /// do semafora albo końca toru końca 2.
    /// </summary>
    public static SzlakDroga Droga(UtProject p, UtLine l, Dictionary<string, TrasaResult> r = null, List<Styk> styki = null)
    {
        var res = new List<SzlakKawalek>(); var uw = new List<string>();
        if (l.Konce.Count != 2) return new(res, new(), uw, "szlak bez dwóch końców");
        r ??= Topologia.Routes(p);
        if (Start(p, l.Konce[0], r) is not var (track, s, dir)) return new(res, new(), uw, $"koniec 1 ({Opis(p, l.Konce[0])}) – brak toru albo semafora");
        if (Start(p, l.Konce[1], r) is not var (bTrack, bS, bDir)) return new(res, new(), uw, $"koniec 2 ({Opis(p, l.Konce[1])}) – brak toru albo semafora");
        styki ??= Topologia.Styki(p, r);
        var seen = new HashSet<string>();
        for (int k = 0; k < 500; k++)
        {
            var len = r[track].Length;
            // koniec 2 na tym torze, przed nami
            if (track == bTrack && (dir > 0 ? bS >= s - 1e-6 : bS <= s + 1e-6))
            {
                res.Add(new(track, Math.Min(s, bS), Math.Max(s, bS)));
                // z końca 2 szlak biegnie w stronę, z której przyszliśmy
                if (bDir != -dir) uw.Add($"koniec 2 ({Opis(p, l.Konce[1])}) zwrócony w drugą stronę – semafor wjazdowy ma działać dla jazdy ze szlaku do posterunku");
                return Wynik(p, res, uw, null);
            }
            double se = dir > 0 ? len : 0;
            res.Add(new(track, Math.Min(s, se), Math.Max(s, se)));
            if (!seen.Add(track + (dir > 0 ? "+" : "-"))) return Wynik(p, res, uw, "droga szlaku zawraca w pętli");
            bool atStart = se == 0;
            var dalej = Dalej(styki, track, atStart, se);
            var t = p.Tracks.First(x => x.Id == track);
            if (dalej.Count == 0) return Wynik(p, res, uw, $"droga szlaku urywa się na {(atStart ? "początku" : "końcu")} toru {(t.Number.Length > 0 ? t.Number : "?")} – nie dochodzi do końca 2 ({Opis(p, l.Konce[1])})");
            var (nt, fromStart) = Prosto(r, track, atStart, dalej);
            if (dalej.Count > 1) uw.Add($"na {(atStart ? "początku" : "końcu")} toru {(t.Number.Length > 0 ? t.Number : "?")} schodzą się {dalej.Count + 1} tory – droga szlaku poszła najbardziej na wprost (tor {Nr(p, nt)})");
            track = nt; s = fromStart ? 0 : r[nt].Length; dir = fromStart ? 1 : -1;
        }
        return Wynik(p, res, uw, "droga szlaku za długa");
    }

    static string Nr(UtProject p, string track) => p.Tracks.FirstOrDefault(x => x.Id == track) is UtTrack t && t.Number.Length > 0 ? t.Number : "(bez numeru)";

    /// <summary>Tory dalej za końcem toru (styki koniec – koniec): tor i czy wchodzi się od jego początku.</summary>
    static List<(string Track, bool FromStart)> Dalej(List<Styk> styki, string track, bool atStart, double se) =>
        styki.Where(x => x.Track == track && x.AtStart == atStart && x.OtherEnd).Select(x => (x.Other, x.OtherS < 1e-6))
            .Concat(styki.Where(x => x.Other == track && x.OtherEnd && Math.Abs(x.OtherS - se) < 0.06).Select(x => (x.Track, x.AtStart)))
            .Distinct().ToList();

    /// <summary>Spośród torów za stykiem – najbardziej na wprost (najmniejsza zmiana kierunku jazdy).</summary>
    static (string Track, bool FromStart) Prosto(Dictionary<string, TrasaResult> r, string track, bool atStart, List<(string Track, bool FromStart)> dalej)
    {
        var rt = r[track]; double azOut = atStart ? rt.At(0).Az + Math.PI : rt.At(rt.Length).Az;
        return dalej.OrderBy(x =>
        {
            var rn = r[x.Track]; double az = x.FromStart ? rn.At(0).Az : rn.At(rn.Length).Az + Math.PI;
            return Math.Abs(Math.IEEERemainder(az - azOut, 2 * Math.PI));
        }).First();
    }

    /// <summary>Propozycja szlaku z miejsca na torze: końce, droga, uwagi, błąd (null – można utworzyć).</summary>
    public record Propozycja(UtLineEnd A, UtLineEnd B, SzlakDroga Droga, List<string> Uwagi, string Err);

    /// <summary>
    /// Szlak z miejsca na torze (narzędzie Szlak, PLAN_EDYTOR_UT.md § 5j): od klikniętego miejsca w obie strony po torach – wzdłuż toru
    /// (przez rozjazdy po torze zasadniczym), przez styki końców torów (przy kilku – najbardziej na wprost) – do pierwszego semafora
    /// wjazdowego działającego w stronę marszu (ze szlaku do posterunku) albo do końca toru bez dalszego połączenia (granica obszaru).
    /// Semafor bez funkcji na torze posterunku też kończy szlak (z uwagą); na torze bez posterunku to odstępowy. Granicą posterunku są
    /// semafory wjazdowe, nie przypisanie toru (tor bywa jednym ciągiem przez stację i szlaki): gdy w marszu pierwszy jest semafor wjazdowy
    /// odwrócony (działający od stacji na szlak), miejsce leży w obrębie posterunku – błąd. Szlak nie wchodzi na tory posterunku: na jego
    /// granicy kończy się na ostatnim semaforze w stronę stacji (gdy nie jest wjazdowy – uwaga), nawet jeśli dalej stoi inny wjazdowy.
    /// </summary>
    public static Propozycja Proponuj(UtProject p, string track, double s, Dictionary<string, TrasaResult> r = null, List<Styk> styki = null)
    {
        r ??= Topologia.Routes(p); styki ??= Topologia.Styki(p, r);
        var uw = new List<string>();
        if (p.Tracks.FirstOrDefault(x => x.Id == track) is not UtTrack t0 || !r.TryGetValue(track, out var r0) || r0.Elements.Count == 0) return new(null, null, null, uw, "brak toru");
        var a = Idz(p, track, s, -1, r, styki, uw, out var ea); var b = Idz(p, track, s, 1, r, styki, uw, out var eb);
        if (a == null || b == null)
        {
            // tor stacyjny bez wyjścia na szlak w żadną stronę – to po prostu miejsce w obrębie posterunku
            if (t0.Post != null && (ea ?? eb ?? "").StartsWith("tor ")) return new(null, null, null, uw, $"tor {Nr(p, track)} leży w obrębie posterunku {Posterunki.Of(p, track)?.Kod} – najedź na tor szlaku (przed semaforem wjazdowym)");
            return new(a, b, null, uw, ea ?? eb);
        }
        var d = Droga(p, new UtLine { Konce = new() { a, b } }, r, styki);
        // rozjazdy na drodze: szlak bywa z bocznicą, ale zwykle to znak, że tory stacji nie mają posterunku albo semafor wjazdowy – funkcji
        var rz = p.Turnouts.Where(z => d.Kawalki.Any(k => k.Track == z.Track && k.Contains(z.S, 0.5))).Select(z => z.Number).Distinct().ToList();
        if (rz.Count > 0) uw.Add($"na drodze szlaku {(rz.Count > 1 ? "rozjazdy" : "rozjazd")} {string.Join(", ", rz.Take(6))}{(rz.Count > 6 ? "…" : "")} – czy to na pewno szlak? Przypisz tory stacji do posterunku albo ustaw semaforom funkcję „wjazdowy”");
        return new(a, b, d, uw.Concat(d.Uwagi).Distinct().ToList(), d.Err);
    }

    static UtLineEnd Idz(UtProject p, string track, double s, int dir, Dictionary<string, TrasaResult> r, List<Styk> styki, List<string> uw, out string err)
    {
        err = null; var seen = new HashSet<string>();
        string post0 = p.Tracks.FirstOrDefault(x => x.Id == track)?.Post;     // posterunek miejsca startu (tor szlaku – zwykle żaden)
        UtDevice ostatni = null;                                               // ostatni semafor w stronę marszu (kandydat na koniec przy granicy posterunku)
        for (int k = 0; k < 500; k++)
        {
            var t = p.Tracks.First(x => x.Id == track); double len = r[track].Length, se = dir > 0 ? len : 0;
            // semafory działające w stronę marszu, przed nami na tym torze
            var sem = p.Devices.Where(d => d.Kind == UtDevice.Semafor && d.Track == track && (dir > 0 ? d.S > s + 1e-6 : d.S < s - 1e-6))
                .OrderBy(d => dir * d.S).ToList();
            var wj = sem.FirstOrDefault(d => d.Funkcja == UtDevice.FWjazdowy || (d.Dir == dir && string.IsNullOrEmpty(d.Funkcja) && t.Post != null));
            if (wj != null && wj.Dir != dir)
            {
                var po = Posterunki.Of(p, wj.Track);
                err = $"to miejsce leży w obrębie posterunku{(po != null ? " " + po.Kod : "")} (za semaforem wjazdowym {wj.Name}) – najedź na tor szlaku przed semaforem wjazdowym";
                return null;
            }
            if (wj != null)
            {
                if (wj.Funkcja != UtDevice.FWjazdowy) uw.Add($"semafor {wj.Name} na końcu szlaku nie ma funkcji – ustaw „wjazdowy”");
                return new UtLineEnd { Semafor = wj.Id };
            }
            ostatni = sem.LastOrDefault(d => d.Dir == dir && d.Funkcja is not (UtDevice.FOdstepowy or UtDevice.FPowtarzacz)) ?? ostatni;
            if (!seen.Add(track + (dir > 0 ? "+" : "-"))) { err = "droga zawraca w pętli – wskaż końce ręcznie (Ctrl + klik)"; return null; }
            bool atStart = se == 0;
            var dalej = Dalej(styki, track, atStart, se);
            if (dalej.Count == 0)
            {
                // koniec toru: granica obszaru (zaznaczona albo tor poza posterunkami); tor posterunku bez zaznaczenia – brak wjazdowego
                if (t.Post != null && !(atStart ? t.OpenStart : t.OpenEnd)) { err = $"tor {Nr(p, track)} posterunku {Posterunki.Of(p, track)?.Kod} kończy się bez semafora wjazdowego w tę stronę – postaw go albo wskaż końce ręcznie (Ctrl + klik)"; return null; }
                return new UtLineEnd { Track = track, AtStart = atStart };
            }
            var (nt, fromStart) = Prosto(r, track, atStart, dalej);
            if (dalej.Count > 1) uw.Add($"na {(atStart ? "początku" : "końcu")} toru {Nr(p, track)} schodzą się tory – szlak poszedł najbardziej na wprost (tor {Nr(p, nt)})");
            // wejście na tor posterunku (innego niż miejsce startu): szlak kończy się na granicy – na ostatnim semaforze w stronę stacji
            // przed nią (jakakolwiek funkcja poza odstępowym – z uwagą), inaczej na pierwszym na torze posterunku
            var np = p.Tracks.First(x => x.Id == nt).Post;
            if (np != null && np != post0)
            {
                var kod = p.Posts.FirstOrDefault(x => x.Id == np)?.Kod;
                var kand = ostatni ?? p.Devices.Where(d => d.Kind == UtDevice.Semafor && d.Track == nt && d.Dir == (fromStart ? 1 : -1))
                    .OrderBy(d => fromStart ? d.S : -d.S).FirstOrDefault();
                if (kand == null) { err = $"na granicy posterunku {kod} (tor {Nr(p, nt)}) brak semafora w stronę stacji – postaw semafor wjazdowy albo wskaż końce ręcznie (Ctrl + klik)"; return null; }
                if (kand.Funkcja != UtDevice.FWjazdowy)
                    uw.Add($"semafor {kand.Name} na granicy posterunku {kod} ma funkcję „{(string.IsNullOrEmpty(kand.Funkcja) ? "brak" : kand.Funkcja)}” – na końcu szlaku powinien być wjazdowy");
                return new UtLineEnd { Semafor = kand.Id };
            }
            track = nt; s = fromStart ? -1e-3 : r[nt].Length + 1e-3; dir = fromStart ? 1 : -1;
        }
        err = "droga za długa"; return null;
    }

    /// <summary>
    /// Nazwy szlaku z końców: pełna „posterunek 1 – posterunek 2” (nazwa posterunku semafora wjazdowego albo posterunek sąsiedni przy
    /// końcu na granicy obszaru) i skrócona z kodów (posterunek sąsiedni – pierwsze litery wyrazów, np. „Łódź Andrzejów” → „ŁA”).
    /// </summary>
    public static (string Pelna, string Skr) ProponujNazwy(UtProject p, UtLine l)
    {
        (string N, string K) Koniec(UtLineEnd e)
        {
            if (e.Semafor != null && p.Devices.FirstOrDefault(x => x.Id == e.Semafor) is UtDevice d && Posterunki.Of(p, d.Track) is UtPost po)
                return (po.Nazwa.Length > 0 ? po.Nazwa : po.Kod, po.Kod.Length > 0 ? po.Kod : Skrot(po.Nazwa));
            if (e.Sasiad.Length > 0) return (e.Sasiad, Skrot(e.Sasiad));
            return ("?", "?");
        }
        var ks = l.Konce.Select(Koniec).ToList();
        if (ks.Count < 2) return ("", "");
        return ($"{ks[0].N} – {ks[1].N}", $"{ks[0].K} – {ks[1].K}");
    }

    /// <summary>Skrót nazwy posterunku: pierwsze litery wyrazów (wielkie), np. „Łódź Andrzejów” → „ŁA”, „Koluszki” → „K”.</summary>
    public static string Skrot(string nazwa) =>
        string.Concat(nazwa.Split(new[] { ' ', '-', '–' }, StringSplitOptions.RemoveEmptyEntries).Where(w => char.IsLetter(w[0])).Select(w => char.ToUpper(w[0])));

    static SzlakDroga Wynik(UtProject p, List<SzlakKawalek> k, List<string> uw, string err)
    {
        // semafory na szlaku (bez semaforów końców), w kolejności drogi
        var sem = new List<UtDevice>();
        foreach (var kw in k)
            sem.AddRange(p.Devices.Where(d => d.Kind == UtDevice.Semafor && d.Track == kw.Track && kw.Contains(d.S)).OrderBy(d => d.S));
        return new(k, sem, uw, err);
    }

    /// <summary>Drogi wszystkich szlaków (do przynależności miejsc i kontroli).</summary>
    public static Dictionary<string, SzlakDroga> Drogi(UtProject p, Dictionary<string, TrasaResult> r = null, List<Styk> styki = null)
    {
        r ??= Topologia.Routes(p); styki ??= Topologia.Styki(p, r);
        return p.Lines.ToDictionary(l => l.Id, l => Droga(p, l, r, styki));
    }

    /// <summary>Szlak, do którego należy miejsce toru (null – posterunek albo nic).</summary>
    public static UtLine At(UtProject p, Dictionary<string, SzlakDroga> drogi, string track, double s) =>
        p.Lines.FirstOrDefault(l => drogi.TryGetValue(l.Id, out var d) && d.Kawalki.Any(k => k.Track == track && k.Contains(s)));

    /// <summary>Kontrola szlaków: numer, końce, droga, funkcje semaforów na szlaku i na końcach.</summary>
    public static List<(bool Error, string Text, string Track, double S, string Id)> Check(UtProject p, Dictionary<string, SzlakDroga> drogi)
    {
        var res = new List<(bool, string, string, double, string)>();
        foreach (var l in p.Lines)
        {
            var nm = $"szlak {(l.Numer.Length > 0 ? l.Numer : "(bez numeru)")}";
            var d = drogi[l.Id];
            var (tr, s0) = d.Kawalki.Count > 0 ? (d.Kawalki[0].Track, (d.Kawalki[0].S0 + d.Kawalki[0].S1) / 2) : (l.Konce.FirstOrDefault()?.Track, 0.0);
            if (l.Numer.Length == 0) res.Add((true, "szlak bez numeru", tr, s0, l.Id));
            else if (p.Lines.Any(x => x.Id != l.Id && x.Numer == l.Numer) && p.Lines.First(x => x.Numer == l.Numer).Id == l.Id) res.Add((true, $"numer szlaku {l.Numer} powtórzony", tr, s0, l.Id));
            if (d.Err != null) { res.Add((true, $"{nm}: {d.Err}", tr, s0, l.Id)); continue; }
            foreach (var u in d.Uwagi) res.Add((false, $"{nm}: {u}", tr, s0, l.Id));
            foreach (var e in l.Konce)
            {
                if (e.Semafor != null && p.Devices.FirstOrDefault(x => x.Id == e.Semafor) is UtDevice sd && sd.Funkcja != UtDevice.FWjazdowy)
                    res.Add((false, $"{nm}: na końcu {Opis(p, e)} – funkcja semafora powinna być „wjazdowy”", sd.Track, sd.S, l.Id));
                if (e.Semafor == null && p.Tracks.FirstOrDefault(x => x.Id == e.Track) is UtTrack t && !(e.AtStart ? t.OpenStart : t.OpenEnd))
                    res.Add((false, $"{nm}: koniec {Opis(p, e)} nie jest zaznaczony jako granica obszaru", e.Track, 0, l.Id));
                if (e.Semafor == null && e.Sasiad.Length == 0) res.Add((false, $"{nm}: na końcu {Opis(p, e)} brak nazwy posterunku sąsiedniego", e.Track, 0, l.Id));
            }
            foreach (var sd in d.Semafory.Where(x => x.Funkcja is not (UtDevice.FOdstepowy or UtDevice.FPowtarzacz)))
                res.Add((false, $"{nm}: semafor {sd.Name} na szlaku – funkcja powinna być „odstępowy”", sd.Track, sd.S, l.Id));
            if (!l.Samoczynna && d.Semafory.Any(x => x.Funkcja == UtDevice.FOdstepowy))
                res.Add((false, $"{nm}: semafory odstępowe na szlaku bez SBL ({UtLine.Typy.First(x => x.Key == l.Typ).Name})", tr, s0, l.Id));
        }
        // semafor odstępowy poza szlakami
        foreach (var sd in p.Devices.Where(x => x.Kind == UtDevice.Semafor && x.Funkcja == UtDevice.FOdstepowy))
            if (At(p, drogi, sd.Track, sd.S) == null) res.Add((false, $"semafor odstępowy {sd.Name} nie leży na żadnym szlaku", sd.Track, sd.S, null));
        return res;
    }
}

/// <summary>Posterunki: przynależność torów i obiektów, kontrola.</summary>
public static class Posterunki
{
    /// <summary>Posterunek toru (null – tor bez posterunku).</summary>
    public static UtPost Of(UtProject p, string track) =>
        p.Tracks.FirstOrDefault(t => t.Id == track)?.Post is string pid ? p.Posts.FirstOrDefault(x => x.Id == pid) : null;

    /// <summary>Grupa nazw (nazwy semaforów i numery rozjazdów nie mogą się powtarzać w obrębie posterunku): id posterunku toru albo „”.</summary>
    public static string Grupa(UtProject p, string track) => p.Tracks.FirstOrDefault(t => t.Id == track)?.Post ?? "";

    /// <summary>Kontrola: kod pusty / powtórzony, tory bez posterunku (gdy posterunki są), posterunek bez torów.</summary>
    public static List<(bool Error, string Text, string Track, string Id)> Check(UtProject p, Dictionary<string, SzlakDroga> drogi, Dictionary<string, TrasaResult> r)
    {
        var res = new List<(bool, string, string, string)>();
        foreach (var x in p.Posts)
        {
            var first = p.Tracks.FirstOrDefault(t => t.Post == x.Id)?.Id;
            if (x.Kod.Length == 0) res.Add((true, $"posterunek {x.Nazwa} bez kodu", first, x.Id));
            else if (p.Posts.Any(y => y.Id != x.Id && y.Kod == x.Kod) && p.Posts.First(y => y.Kod == x.Kod).Id == x.Id) res.Add((true, $"kod posterunku {x.Kod} powtórzony", first, x.Id));
            if (first == null) res.Add((false, $"posterunek {x.Kod} bez torów", null, x.Id));
        }
        foreach (var t in p.Tracks.Where(t => t.Post != null && !p.Posts.Any(x => x.Id == t.Post)))
            res.Add((true, $"tor {(t.Number.Length > 0 ? t.Number : "?")}: posterunek usunięty", t.Id, null));
        if (p.Posts.Count == 0) return res;
        foreach (var t in p.Tracks.Where(t => t.Post == null && r.TryGetValue(t.Id, out var rt) && rt.Elements.Count > 0))
        {
            // tor w całości na szlaku nie potrzebuje posterunku
            double len = r[t.Id].Length;
            double naSzlaku = drogi.Values.SelectMany(d => d.Kawalki).Where(k => k.Track == t.Id).Sum(k => k.S1 - k.S0);
            if (naSzlaku < len - 0.5) res.Add((false, $"tor {(t.Number.Length > 0 ? t.Number : "?")} nie należy do posterunku ani do szlaku", t.Id, null));
        }
        return res;
    }
}

namespace Lcs.Model;

/// <summary>
/// Urządzenie przytorowe przypięte do toru (Ig-10 zał. 8): położenie = tor + kilometraż <see cref="S"/> [m w terenie od początku toru],
/// <see cref="Dir"/> +1 – działa dla jazdy w stronę rosnącego kilometrażu toru, −1 – przeciwnie; <see cref="Side"/> +1 – stoi po prawej
/// patrząc w kierunku działania, −1 – po lewej. XYZ znaku wynika z toru, więc urządzenie jedzie razem z torem.
/// </summary>
public class UtDevice
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Kind { get; set; } = Semafor;
    /// <summary>Nazwa (opis na planie), np. „A”, „Tm12”, „Wk3” – wpisuje projektant.</summary>
    public string Name { get; set; } = "";
    public string Track { get; set; }
    public double S { get; set; }
    public int Dir { get; set; } = 1;
    public int Side { get; set; } = 1;
    /// <summary>Semafor świetlny: liczba komór (2–5) – z <see cref="Lights"/>, zostaje dla starych plików.</summary>
    public int Heads { get; set; } = 3;
    /// <summary>
    /// Komory sygnalizatora świetlnego od masztu (od dołu) wg BSK ISDR „Oznaczenia urządzeń srk”: C – czerwona, Z – zielona,
    /// P – pomarańczowa, B – biała, N – niebieska. Puste – domyślne dla rodzaju (<see cref="DefaultLights"/>).
    /// </summary>
    public string Lights { get; set; }

    /// <summary>Komory wg BSK: semafor 2 – CZ, 3 – PCZ, 4 – BPCZ, 5 – BPCPZ; tarcza ostrzegawcza PZ; tarcza manewrowa BN.</summary>
    public static string DefaultLights(string kind, int heads) => kind switch
    {
        Semafor => heads switch { <= 2 => "CZ", 3 => "PCZ", 4 => "BPCZ", _ => "BPCPZ" },
        TarczaOstrz => "PZ",
        TarczaMan => "BN",
        _ => "",
    };
    /// <summary>Komory do rysowania (zapisane albo domyślne).</summary>
    public string LightsOrDefault => string.IsNullOrEmpty(Lights) ? DefaultLights(Kind, Heads) : Lights;
    /// <summary>Kolor komory świecącej w stanie zasadniczym (gruba kreska); puste – domyślny: semafor C, tarcza ostrzegawcza P, manewrowa N.</summary>
    public string Lit { get; set; }
    public char LitOrDefault => !string.IsNullOrEmpty(Lit) ? Lit[0] : Kind switch { Semafor => 'C', TarczaOstrz => 'P', TarczaMan => 'N', _ => ' ' };
    /// <summary>Maszt: <c>prosty</c>, <c>wysiegnik</c> (z wysięgnikiem), <c>karzel</c> (karzełkowy, bez masztu); puste – prosty (tarcza manewrowa z <see cref="Dwarf"/> – karzełkowy).</summary>
    public string Mast { get; set; }
    public string MastOrDefault => !string.IsNullOrEmpty(Mast) ? Mast : Dwarf ? MastKarzel : MastProsty;
    /// <summary>Pasy świetlne pod komorami (BSK ss5; od masztu pomarańczowy, potem zielony) – tylko semafor.</summary>
    public bool StripeZ { get; set; }
    public bool StripeP { get; set; }
    /// <summary>Wskaźnik W24 na maszcie (BSK ss4, ss5).</summary>
    public bool W24 { get; set; }
    /// <summary>Wskaźnik W21 z cyfrą (np. „5”, BSK ss6); puste – brak.</summary>
    public string W21 { get; set; }

    public const string MastProsty = "prosty", MastWysiegnik = "wysiegnik", MastBramka = "bramka", MastKarzel = "karzel";
    /// <summary>Mocowanie sygnalizatora: na maszcie (słup), na wysięgniku, na bramce sygnałowej nad torem, karzełkowy (na podłożu).</summary>
    public static readonly (string Key, string Name)[] Masts = { (MastProsty, "na maszcie (słup)"), (MastWysiegnik, "na wysięgniku"), (MastBramka, "na bramce"), (MastKarzel, "karzełkowy") };
    /// <summary>Kolory komór: litera i nazwa.</summary>
    public static readonly (char Key, string Name)[] Colors = { ('C', "czerwona"), ('Z', "zielona"), ('P', "pomarańczowa"), ('B', "biała"), ('N', "niebieska") };

    /// <summary>Światło komory: stałe, stałe lub pulsujące (na planie 2 kreski), tylko pulsujące (4 kreski) – uwaga właściciela 2026-10-17.</summary>
    public const int Stale = 0, StaleLubPulsujace = 1, TylkoPulsujace = 2;
    public static readonly (int Key, string Name)[] PulseModes = { (Stale, "stałe"), (StaleLubPulsujace, "stałe lub pulsujące"), (TylkoPulsujace, "tylko pulsujące") };

    /// <summary>
    /// Komory: litera koloru i rodzaj światła (<see cref="Stale"/>, <see cref="StaleLubPulsujace"/> – „*” po literze,
    /// <see cref="TylkoPulsujace"/> – „**”), np. „B*PCZ**”.
    /// </summary>
    public static List<(char C, int Pulse)> ParseLights(string s)
    {
        var res = new List<(char, int)>();
        foreach (var ch in s ?? "")
        {
            if (ch == '*' && res.Count > 0) res[^1] = (res[^1].Item1, Math.Min(TylkoPulsujace, res[^1].Item2 + 1));
            else if ("CZPBN".Contains(ch)) res.Add((ch, Stale));
        }
        return res;
    }
    public static string FormatLights(IEnumerable<(char C, int Pulse)> l) => string.Concat(l.Select(x => x.C + new string('*', Math.Clamp(x.Pulse, 0, 2))));
    public static bool ValidLights(string s) => !string.IsNullOrEmpty(s) && s.All(c => "CZPBN*".Contains(c)) && ParseLights(s).Count is >= 1 and <= 7;

    /// <summary>Układy gotowe (BSK ss3–ss9): nazwa, komory, świeci zasadniczo, maszt, W24, pasy świetlne (pomarańczowy i zielony).</summary>
    public static readonly (string Name, string Lights, char Lit, string Mast, bool W24, bool Stripes)[] Presets =
    {
        ("semafor 2-komorowy (C Z)", "CZ", 'C', MastProsty, false, false),
        ("semafor 3-komorowy bez manewrowej (P C Z)", "P*CZ", 'C', MastProsty, false, false),
        ("semafor 4-komorowy z manewrową (B P C Z)", "B*PCZ*", 'C', MastProsty, false, false),
        ("semafor 4-komorowy na wysięgniku, W24", "B*PCZ*", 'C', MastWysiegnik, true, false),
        ("semafor 5-komorowy (B P C P Z)", "B*PCP*Z*", 'C', MastProsty, false, false),
        ("semafor 5-komorowy z pasami i W24 (BSK ss5)", "B*PCP*Z*", 'C', MastProsty, true, true),
        ("tarcza ostrzegawcza (P Z)", "P*Z", 'P', MastProsty, false, false),
        ("tarcza manewrowa karzełkowa (B N)", "B*N", 'N', MastKarzel, false, false),
        ("tarcza manewrowa wysoka (B N)", "B*N", 'N', MastProsty, false, false),
    };
    /// <summary>Tarcza manewrowa karzełkowa (bez masztu).</summary>
    public bool Dwarf { get; set; }

    /// <summary>
    /// Funkcja semafora (decyzja właściciela 2026-10-17, PLAN_EDYTOR_UT.md § 7 pkt 4): wjazdowy, wyjazdowy, drogowskazowy, odstępowy,
    /// powtarzacz, PK; puste – nie określona („Sprawdź” przypomina). Od niej zależy symbol na pulpicie i rola w zależnościach.
    /// </summary>
    public string Funkcja { get; set; }

    public const string FWjazdowy = "wjazdowy", FWyjazdowy = "wyjazdowy", FDrogowskazowy = "drogowskazowy", FOdstepowy = "odstepowy",
        FPowtarzacz = "sp", FPk = "pk";
    public static readonly (string Key, string Name)[] Funkcje =
    {
        (FWjazdowy, "wjazdowy"), (FWyjazdowy, "wyjazdowy"), (FDrogowskazowy, "drogowskazowy"), (FOdstepowy, "odstępowy"),
        (FPowtarzacz, "powtarzacz (Sp)"), (FPk, "PK – punkt końcowy przebiegu"),
    };
    public static string FunkcjaName(string f) => Funkcje.FirstOrDefault(x => x.Key == f).Name ?? "";

    /// <summary>
    /// Tarcza ostrzegawcza albo powtarzacz (Sp): id semafora powtarzanego albo id szlaku (<see cref="UtLine"/>, informacja z sąsiedniej
    /// stacji); puste – nie wskazano („Sprawdź” przypomina). Wskazuje projektant, program może tylko zaproponować
    /// (<see cref="Urzadzenia.Propozycja"/>). W symulatorze – <c>repeats</c>.
    /// </summary>
    public string Powtarza { get; set; }
    /// <summary>Urządzenie powtarza sygnał innego semafora: tarcza ostrzegawcza albo semafor – powtarzacz.</summary>
    public bool Powtarzajacy => Kind == TarczaOstrz || (Kind == Semafor && Funkcja == FPowtarzacz);

    public const string Semafor = "semafor", TarczaOstrz = "to", TarczaMan = "tm", Koziol = "koziol", Wykolejnica = "wk",
        LicznikOsi = "lo", Zlacze = "zlacze", Ukres = "ukres", W4 = "w4", W5 = "w5";

    /// <summary>Rodzaje: klucz, nazwa w programie, miejsce w Ig-10, czy ma kierunek działania, przedrostek nazwy.</summary>
    public static readonly (string Key, string Name, string Ig10, bool Directed, string Prefix)[] Kinds =
    {
        (Semafor, "Semafor świetlny", "zał. 8, s. 27", true, ""),
        (TarczaOstrz, "Tarcza ostrzegawcza", "zał. 8, s. 27", true, "To"),
        (TarczaMan, "Tarcza manewrowa", "zał. 8, s. 29", true, "Tm"),
        (Wykolejnica, "Wykolejnica", "zał. 8, s. 52", true, "Wk"),
        (Koziol, "Kozioł oporowy", "zał. 8, s. 25", true, ""),
        (LicznikOsi, "Licznik osi", "zał. 8, s. 51", false, "lo"),
        (Zlacze, "Złącze izolowane", "zał. 8, s. 50", false, ""),
        (Ukres, "Ukres (W17)", "zał. 8, s. 24", false, ""),
        (W4, "Wskaźnik W4", "zał. 8, s. 33", true, ""),
        (W5, "Wskaźnik W5", "zał. 8, s. 30", true, ""),
    };

    public static (string Key, string Name, string Ig10, bool Directed, string Prefix) KindInfo(string k) => Kinds.FirstOrDefault(x => x.Key == k);
    public bool Directed => KindInfo(Kind).Directed;
}

/// <summary>Urządzenia: położenie na torze, kolejne nazwy, sprawdzanie.</summary>
public static class Urzadzenia
{
    /// <summary>Punkt na osi toru i kierunek działania [rad] (null – brak toru).</summary>
    public static (XY P, double Az, double K)? Pos(UtProject p, UtDevice d)
    {
        var t = p.Tracks.FirstOrDefault(x => x.Id == d.Track); if (t == null) return null;
        var r = Trasa.Compute(t); if (r.Elements.Count == 0) return null;
        var (pt, az) = r.At(Math.Clamp(d.S, 0, r.Length));
        return (pt, d.Dir < 0 ? az + Math.PI : az, r.K);
    }

    /// <summary>Nazwa zajęta przez inne urządzenie tego samego posterunku (nazwy powtarzają się między posterunkami, np. „A” na każdej stacji).</summary>
    public static bool NameTaken(UtProject p, string name, string track, string exceptId = null)
    {
        if (string.IsNullOrEmpty(name)) return false;
        var g = Posterunki.Grupa(p, track);
        return p.Devices.Any(x => x.Id != exceptId && x.Name == name && Posterunki.Grupa(p, x.Track) == g);
    }

    /// <summary>Podpowiedź nazwy: semafory – kolejna wolna litera, inne – przedrostek + kolejny numer; złącze, kozioł, ukres – bez nazwy.
    /// Wolna w posterunku toru <paramref name="track"/> (null – w całym obszarze).</summary>
    public static string NextName(UtProject p, string kind, string track = null)
    {
        var g = track != null ? Posterunki.Grupa(p, track) : null;
        var used = p.Devices.Where(x => g == null || Posterunki.Grupa(p, x.Track) == g).Select(x => x.Name).ToHashSet();
        if (kind == UtDevice.Semafor)
        {
            foreach (var c in "ABCDEFGHJKLMNPRSTUWYZ") if (!used.Contains(c.ToString())) return c.ToString();
            return "";
        }
        var pre = UtDevice.KindInfo(kind).Prefix; if (string.IsNullOrEmpty(pre)) return "";
        for (int i = 1; ; i++) if (!used.Contains(pre + i)) return pre + i;
    }

    /// <summary>Uwagi: poza torem, nazwa powtórzona, kozioł nie na końcu toru.</summary>
    public static List<string> Check(UtProject p, UtDevice d)
    {
        var res = new List<string>();
        var t = p.Tracks.FirstOrDefault(x => x.Id == d.Track);
        if (t == null) { res.Add("brak toru"); return res; }
        var r = Trasa.Compute(t);
        if (d.S < -0.01 || d.S > r.Length + 0.01) res.Add("poza torem");
        if (d.Name.Length > 0 && NameTaken(p, d.Name, d.Track, d.Id)) res.Add($"nazwa „{d.Name}” powtórzona");
        if (d.Kind == UtDevice.Semafor && string.IsNullOrEmpty(d.Funkcja)) res.Add("funkcja semafora nie określona (wjazdowy, wyjazdowy…)");
        if (d.Kind == UtDevice.Koziol && Math.Min(d.S, r.Length - d.S) > 1) res.Add("kozioł nie stoi na końcu toru");
        if (d.Powtarzajacy)
        {
            if (string.IsNullOrEmpty(d.Powtarza)) res.Add("nie wskazano semafora powtarzanego (pole „Powtarza”, przycisk „Zaproponuj”)");
            else if (p.Devices.FirstOrDefault(x => x.Id == d.Powtarza) is UtDevice m)
            {
                if (m.Kind != UtDevice.Semafor || m.Powtarzajacy || m.Funkcja == UtDevice.FPk) res.Add($"powtarza {UtDevice.KindInfo(m.Kind).Name.ToLower()} {m.Name} – to nie jest semafor z sygnałem do powtórzenia");
            }
            else if (!p.Lines.Any(x => x.Id == d.Powtarza)) res.Add("semafor powtarzany (albo szlak) usunięty – wskaż na nowo");
        }
        return res;
    }

    /// <summary>Opis tego, co powtarza urządzenie (do paneli i list): „semafor A (MJ)”, „szlak 1A”; null – nic albo usunięte.</summary>
    public static string PowtarzaOpis(UtProject p, string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (p.Devices.FirstOrDefault(x => x.Id == id) is UtDevice m)
            return $"semafor {m.Name}" + (Posterunki.Of(p, m.Track) is UtPost post && post.Kod.Length > 0 ? $" ({post.Kod})" : "");
        return p.Lines.FirstOrDefault(x => x.Id == id) is UtLine l ? $"szlak {l.Numer}" : null;
    }

    /// <summary>
    /// Propozycja semafora powtarzanego (tylko podpowiedź – zatwierdza projektant): pierwszy semafor przed urządzeniem w kierunku jego
    /// działania, zwrócony w tę samą stronę (bez powtarzaczy i PK), do <paramref name="maxLen"/> m. Po torze zasadniczym (przez rozjazdy
    /// na wprost), dalej przez styki końców torów. Null – brak w zasięgu.
    /// </summary>
    public static UtDevice Propozycja(UtProject p, UtDevice d, double maxLen = 3000, Dictionary<string, TrasaResult> r = null, List<Styk> styki = null)
    {
        r ??= Topologia.Routes(p); styki ??= Topologia.Styki(p, r);
        if (!r.ContainsKey(d.Track)) return null;
        string track = d.Track; double s = d.S; int dir = d.Dir >= 0 ? 1 : -1; double done = 0;
        var seen = new HashSet<string>();
        for (int k = 0; k < 200 && done < maxLen; k++)
        {
            var len = r[track].Length;
            var cand = p.Devices.Where(x => x.Id != d.Id && x.Track == track && x.Kind == UtDevice.Semafor && !x.Powtarzajacy && x.Funkcja != UtDevice.FPk
                    && x.Dir == dir && (dir > 0 ? x.S > s + 1e-6 : x.S < s - 1e-6))
                .OrderBy(x => Math.Abs(x.S - s)).FirstOrDefault();
            if (cand != null) return done + Math.Abs(cand.S - s) <= maxLen ? cand : null;
            double se = dir > 0 ? len : 0;
            done += Math.Abs(se - s);
            if (!seen.Add(track + (dir > 0 ? "+" : "-"))) return null;
            bool atStart = dir < 0;
            var rt = r[track]; var az = rt.At(se).Az + (dir < 0 ? Math.PI : 0);
            // dalej: tor dochodzący końcem do tego końca albo tor, w który ten koniec wchodzi (np. koniec odgałęzienia na torze zasadniczym)
            var dalej = styki.Where(x => x.Other == track && x.OtherEnd && Math.Abs(x.OtherS - se) < 0.06).Select(x => (T: x.Track, S: x.AtStart ? 0.0 : r[x.Track].Length, D: x.AtStart ? 1 : -1))
                .Concat(styki.Where(x => x.Track == track && x.AtStart == atStart).Select(x =>
                {
                    var oaz = r[x.Other].At(x.OtherS).Az;
                    return (T: x.Other, S: x.OtherS, D: Math.Cos(oaz - az) >= 0 ? 1 : -1);
                }))
                .Where(x => x.T != track && r.ContainsKey(x.T)).ToList();
            if (dalej.Count == 0) return null;
            (track, s, dir) = dalej[0];
        }
        return null;
    }
}

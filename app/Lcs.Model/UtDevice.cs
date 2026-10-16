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
    /// <summary>Pasy świetlne pod komorami: zielony, pomarańczowy (BSK ss5).</summary>
    public bool StripeZ { get; set; }
    public bool StripeP { get; set; }
    /// <summary>Wskaźnik W24 na maszcie (BSK ss4, ss5).</summary>
    public bool W24 { get; set; }
    /// <summary>Wskaźnik W21 z cyfrą (np. „5”, BSK ss6); puste – brak.</summary>
    public string W21 { get; set; }

    public const string MastProsty = "prosty", MastWysiegnik = "wysiegnik", MastKarzel = "karzel";
    public static readonly (string Key, string Name)[] Masts = { (MastProsty, "prosty"), (MastWysiegnik, "z wysięgnikiem"), (MastKarzel, "karzełkowy (bez masztu)") };
    /// <summary>Kolory komór: litera i nazwa.</summary>
    public static readonly (char Key, string Name)[] Colors = { ('C', "czerwona"), ('Z', "zielona"), ('P', "pomarańczowa"), ('B', "biała"), ('N', "niebieska") };

    /// <summary>Komory: litera koloru i czy może świecić pulsująco (w zapisie „*” po literze, np. „B*PCZ*”).</summary>
    public static List<(char C, bool Pulse)> ParseLights(string s)
    {
        var res = new List<(char, bool)>();
        foreach (var ch in s ?? "")
        {
            if (ch == '*' && res.Count > 0) res[^1] = (res[^1].Item1, true);
            else if ("CZPBN".Contains(ch)) res.Add((ch, false));
        }
        return res;
    }
    public static string FormatLights(IEnumerable<(char C, bool Pulse)> l) => string.Concat(l.Select(x => x.C + (x.Pulse ? "*" : "")));
    public static bool ValidLights(string s) => !string.IsNullOrEmpty(s) && s.All(c => "CZPBN*".Contains(c)) && ParseLights(s).Count is >= 1 and <= 7;

    /// <summary>Układy gotowe (BSK ss3–ss9): nazwa, komory, świeci zasadniczo, maszt, pasy, W24.</summary>
    public static readonly (string Name, string Lights, char Lit, string Mast, bool W24)[] Presets =
    {
        ("semafor 2-komorowy (C Z)", "CZ", 'C', MastProsty, false),
        ("semafor 3-komorowy bez manewrowej (P C Z)", "P*CZ", 'C', MastProsty, false),
        ("semafor 4-komorowy z manewrową (B P C Z)", "B*PCZ*", 'C', MastProsty, false),
        ("semafor 4-komorowy na wysięgniku, W24", "B*PCZ*", 'C', MastWysiegnik, true),
        ("semafor 5-komorowy (B P C P Z)", "B*PCP*Z*", 'C', MastProsty, false),
        ("tarcza ostrzegawcza (P Z)", "P*Z", 'P', MastProsty, false),
        ("tarcza manewrowa karzełkowa (B N)", "B*N", 'N', MastKarzel, false),
        ("tarcza manewrowa wysoka (B N)", "B*N", 'N', MastProsty, false),
    };
    /// <summary>Tarcza manewrowa karzełkowa (bez masztu).</summary>
    public bool Dwarf { get; set; }

    public const string Semafor = "semafor", TarczaOstrz = "to", TarczaMan = "tm", Koziol = "koziol", Wykolejnica = "wk",
        LicznikOsi = "lo", Zlacze = "zlacze", Ukres = "ukres";

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

    /// <summary>Podpowiedź nazwy: semafory – kolejna wolna litera, inne – przedrostek + kolejny numer; złącze, kozioł, ukres – bez nazwy.</summary>
    public static string NextName(UtProject p, string kind)
    {
        var used = p.Devices.Select(x => x.Name).ToHashSet();
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
        if (d.Name.Length > 0 && p.Devices.Any(x => x.Id != d.Id && x.Name == d.Name)) res.Add($"nazwa „{d.Name}” powtórzona");
        if (d.Kind == UtDevice.Koziol && Math.Min(d.S, r.Length - d.S) > 1) res.Add("kozioł nie stoi na końcu toru");
        return res;
    }
}

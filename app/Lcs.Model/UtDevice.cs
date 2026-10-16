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
    /// <summary>Semafor świetlny: liczba komór (2–5).</summary>
    public int Heads { get; set; } = 3;
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

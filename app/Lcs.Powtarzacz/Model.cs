using System.IO;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Lcs.Powtarzacz;

/// <summary>Stan jednego przejazdu (z gry albo z danych przykładowych).</summary>
public sealed class Przejazd
{
    // wartości TrafficControl.CrossingState w grze (flagi)
    public const int CsUnknown = 1, CsOpen = 2, CsSwitch = 4, CsClose = 8, CsHold = 32, CsBroken = 128;

    public string Nazwa = "";            // nazwa w grze, np. „L1_102.026_B”
    public string Linia = "";            // „1” – z nazwy
    public double Km = double.NaN;       // 102.026
    public string Kat = "";              // A–F – z nazwy (przyrostek)
    public bool NaPulpicie, Uzk, Swiatla, Top, Automatyczny, Zdalny, Potwierdzony, ZwolnionyRecznie;
    public bool? Droznik;
    public int O = -1, S = -1, Wj = -1, Wy = -1;
    public List<string> ToryWyl = new();
    public double X = double.NaN, Z = double.NaN;
    public DateTime Odebrano = DateTime.MinValue;

    /// <summary>SSP – kategoria B / C albo flagi elementu pulpitu (automatic / remote).</summary>
    public bool Ssp => Kat is "B" or "C" || Automatyczny || Zdalny;
    public string KmTekst => double.IsNaN(Km) ? Nazwa : Km.ToString("0.000", CultureInfo.InvariantCulture).Replace('.', ',');
    public string Opis => $"LK{Linia} km {KmTekst}" + (Kat.Length > 0 ? $" kat. {Kat}" : "");

    static bool Ma(int st, int bit) => st > 0 && (st & bit) != 0;

    /// <summary>Awaria (Broken w stanie ogólnym, sygnałów albo rogatek).</summary>
    public bool Awaria => Ma(O, CsBroken) || Ma(S, CsBroken) || Ma(Wj, CsBroken) || Ma(Wy, CsBroken);

    /// <summary>Sygnalizacja ostrzegawcza działa (sygnały drogowe zamknięte albo w zmianie).</summary>
    public bool Ostrzeganie => Ma(S, CsClose) || Ma(S, CsSwitch);

    /// <summary>
    /// otwarty / zamykanie / otwieranie / zamkniety / uszkodzony; null – brak danych. Gra (P0, 2026-11-21) w stanie ogólnym nie
    /// ustawia Switch: ruch rogatek tylko w stanach rogatek; przy zamykaniu stan ogólny już Close, przy otwieraniu już Open.
    /// </summary>
    public string Stan
    {
        get
        {
            if (O <= 0) return null;
            if (Awaria) return "uszkodzony";
            bool zamkn = Ma(O, CsClose) || Ma(O, CsHold);
            if (Ma(Wj, CsSwitch) || Ma(Wy, CsSwitch) || Ma(O, CsSwitch)) return zamkn ? "zamykanie" : "otwieranie";
            if (zamkn && Ma(Wj, CsOpen) && Ma(Wy, CsOpen)) return "zamykanie";   // czas ostrzegania – rogatki jeszcze w górze
            if (zamkn) return "zamkniety";
            if (Ma(O, CsOpen)) return "otwarty";
            return null;
        }
    }

    public static string Slownie(string stan) => stan switch
    {
        "otwarty" => "otwarty", "zamykanie" => "zamykanie", "otwieranie" => "otwieranie", "zamkniety" => "ZAMKNIĘTY",
        "uszkodzony" => "AWARIA", _ => "brak danych",
    };

    public static string Rog(int s) => s <= 0 ? "?" : Ma(s, CsBroken) ? "awaria" : Ma(s, CsSwitch) ? "w ruchu" : Ma(s, CsClose) || Ma(s, CsHold) ? "zamknięte" : Ma(s, CsOpen) ? "otwarte" : "?";

    static readonly Regex RxNazwa = new(@"^L(\d+)_(\d+\.\d+)(?:_([A-Fa-f]))?", RegexOptions.IgnoreCase);

    /// <summary>Wpis „p” z datagramu moda (PowtarzaczUdp.cs).</summary>
    public static Przejazd ZJson(JsonElement e)
    {
        var p = new Przejazd { Nazwa = Tekst(e, "n"), Odebrano = DateTime.Now };
        var m = RxNazwa.Match(p.Nazwa);
        if (m.Success)
        {
            p.Linia = m.Groups[1].Value;
            p.Km = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            p.Kat = m.Groups[3].Value.ToUpperInvariant();
        }
        p.NaPulpicie = Bool(e, "pul"); p.Uzk = Bool(e, "uzk"); p.Swiatla = Bool(e, "sw"); p.Top = Bool(e, "top");
        p.Automatyczny = Bool(e, "aut"); p.Zdalny = Bool(e, "zd"); p.Potwierdzony = Bool(e, "c"); p.ZwolnionyRecznie = Bool(e, "m");
        if (e.TryGetProperty("drz", out var d) && d.ValueKind is JsonValueKind.True or JsonValueKind.False) p.Droznik = d.GetBoolean();
        p.O = Int(e, "o"); p.S = Int(e, "s"); p.Wj = Int(e, "wj"); p.Wy = Int(e, "wy");
        if (e.TryGetProperty("tw", out var tw) && tw.ValueKind == JsonValueKind.Array) foreach (var t in tw.EnumerateArray()) if (t.GetString() is string s) p.ToryWyl.Add(s);
        if (e.TryGetProperty("x", out var x) && x.TryGetDouble(out var xv)) p.X = xv;
        if (e.TryGetProperty("z", out var z) && z.TryGetDouble(out var zv)) p.Z = zv;
        return p;
    }

    static string Tekst(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : "";
    static bool Bool(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.True;
    static int Int(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.TryGetInt32(out var i) ? i : -1;
}

/// <summary>Wpis dziennika powtarzacza (zdarzenia i usterki – Ie-20 § 21 ust. 8).</summary>
public sealed record Zdarzenie(DateTime Czas, string Przejazd, string Tekst, bool Alarm);

/// <summary>Wybrane przejazdy i ustawienia okna (%LOCALAPPDATA%\SUK\Powtarzacz\ustawienia.json).</summary>
public sealed class Ustawienia
{
    /// <summary>Nazwy przejazdów w grze pokazane na powtarzaczu; null – jeszcze nie wybierano (wybór domyślny).</summary>
    public List<string> Wybrane { get; set; }
    public int Port { get; set; } = 47134;
    /// <summary>Widok: „ie104” – pola wg Ie-104 § 13 / § 14; „rasp” – ekrany RASP-UZK (Kombud).</summary>
    public string Widok { get; set; } = "ie104";

    static string Plik => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SUK", "Powtarzacz", "ustawienia.json");
    public static Ustawienia Wczytaj() { try { return JsonSerializer.Deserialize<Ustawienia>(File.ReadAllText(Plik)) ?? new(); } catch { return new(); } }
    public void Zapisz() { try { Directory.CreateDirectory(Path.GetDirectoryName(Plik)); File.WriteAllText(Plik, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); } catch { } }
}

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lcs.Tabor;

/// <summary>Rodzaje pojazdów (PLAN_EDYTOR_TABORU.md § 2).</summary>
public static class Rodzaj
{
    public const string LokEl = "lokEl", LokSp = "lokSp", WagonPas = "wagonPas", WagonTow = "wagonTow", Ezt = "ezt", Szt = "szt", Bzt = "bzt";
    public static readonly (string Key, string Name)[] Wszystkie =
    {
        (LokEl, "lokomotywa elektryczna"), (LokSp, "lokomotywa spalinowa"), (WagonPas, "wagon pasażerski"), (WagonTow, "wagon towarowy"),
        (Ezt, "EZT – elektryczny zespół trakcyjny"), (Szt, "SZT – spalinowy zespół trakcyjny"), (Bzt, "BZT – bitrakcyjny zespół trakcyjny"),
    };
    public static string Nazwa(string r) => Wszystkie.FirstOrDefault(x => x.Key == r).Name ?? r;
    /// <summary>Zespół trakcyjny – hamulec elektropneumatyczny (krótkie napełnianie).</summary>
    public static bool Zespol(string r) => r is Ezt or Szt or Bzt;
    public static bool Wagon(string r) => r is WagonPas or WagonTow;
}

/// <summary>Źródło zasilania charakterystyki trakcyjnej.</summary>
public static class Zasilanie
{
    public const string Siec = "siec", Spalinowy = "spalinowy";
}

/// <summary>Charakterystyka trakcyjna F(v) dla jednego źródła zasilania (BZT ma dwie).</summary>
public class Charakterystyka
{
    public string Zasilanie { get; set; } = Lcs.Tabor.Zasilanie.Siec;
    /// <summary>Punkty (v [km/h], F [kN]) rosnąco po v; między punktami liniowo, za ostatnim F = P / v (gdy podana moc) albo 0.</summary>
    public List<double[]> Punkty { get; set; } = new();
    /// <summary>Moc na obwodzie kół [kW] – podpowiedź do krzywej F = min(F0, P/v) i przedłużenie za ostatnim punktem.</summary>
    public double Moc { get; set; }
    /// <summary>Vmax przy tym zasilaniu [km/h]; 0 – jak pojazd.</summary>
    public double VMax { get; set; }

    /// <summary>Siła pociągowa [kN] przy prędkości v [km/h].</summary>
    public double F(double v)
    {
        if (Punkty.Count == 0) return 0;
        if (v <= Punkty[0][0]) return Punkty[0][1];
        for (int i = 1; i < Punkty.Count; i++)
            if (v <= Punkty[i][0])
            {
                var (v0, f0, v1, f1) = (Punkty[i - 1][0], Punkty[i - 1][1], Punkty[i][0], Punkty[i][1]);
                return v1 > v0 ? f0 + (f1 - f0) * (v - v0) / (v1 - v0) : f1;
            }
        return Moc > 0 && v > 0 ? Math.Min(Punkty[^1][1], Moc * 3.6 / v) : 0;
    }

    /// <summary>Krzywa F = min(F0, P/v) w punktach co 10 km/h do vmax (podpowiedź w edytorze; dalej poprawia człowiek).</summary>
    public static List<double[]> ZMocy(double f0, double moc, double vmax)
    {
        var l = new List<double[]>();
        for (double v = 0; v <= vmax + 1e-9; v += 10) l.Add(new[] { v, Math.Round(Math.Min(f0, v > 0 ? moc * 3.6 / v : f0), 1) });
        return l;
    }
}

/// <summary>Opory ruchu jednostkowe w = a + b·v + c·v² [N/kN], v w km/h (wzór do potwierdzenia przez właściciela).</summary>
public class Opory
{
    public double A { get; set; } = 1.5;
    public double B { get; set; }
    public double C { get; set; } = 0.00025;
    public double W(double v) => A + B * v + C * v * v;
}

/// <summary>Pojazd (seria) – dane katalogowe; wartości przykładowe oznaczone <see cref="DoPotwierdzenia"/>.</summary>
public class Pojazd
{
    /// <summary>Oznaczenie serii, np. „EN57”, „111A” – identyfikator w składach.</summary>
    public string Seria { get; set; } = "";
    public string Nazwa { get; set; } = "";
    public string Rodzaj { get; set; } = Lcs.Tabor.Rodzaj.WagonPas;
    /// <summary>Długość ze zderzakami / sprzęgami [m].</summary>
    public double Dlugosc { get; set; }
    /// <summary>Masa własna [t].</summary>
    public double Masa { get; set; }
    /// <summary>Masa ładunku albo pasażerów przy pełnym załadowaniu [t].</summary>
    public double Ladunek { get; set; }
    /// <summary>Masa przypadająca na osie napędne [t] (przyczepność); 0 – pojazd bez napędu.</summary>
    public double MasaNapedna { get; set; }
    public int Osie { get; set; } = 4;
    /// <summary>Współczynnik mas wirujących ρ (masa zastępcza = m·(1+ρ)).</summary>
    public double MasyWirujace { get; set; } = 0.06;
    public double VMax { get; set; } = 100;
    public List<Charakterystyka> Trakcja { get; set; } = new();
    public Opory Opory { get; set; } = new();
    /// <summary>Masa hamująca [t] wg nastawienia („G”, „P”, „R”) w stanie próżnym.</summary>
    public Dictionary<string, double> MasaHamujaca { get; set; } = new();
    /// <summary>Masa hamująca w stanie ładownym (przestawiacz próżny / ładowny); brak – jak próżny.</summary>
    public Dictionary<string, double> MasaHamujacaLadowny { get; set; }
    /// <summary>Hamulec elektrodynamiczny – siła [kN] (tylko pod siecią / przy pracy silników); 0 – brak.</summary>
    public double HamulecED { get; set; }
    public string Zrodlo { get; set; } = "";
    public string Uwagi { get; set; } = "";
    public bool DoPotwierdzenia { get; set; } = true;

    public bool Napedny => Trakcja.Count > 0 && MasaNapedna > 0;
    public Charakterystyka Char(string zasilanie) => Trakcja.FirstOrDefault(c => c.Zasilanie == zasilanie);
}

/// <summary>Pozycja składu: seria, liczba (np. 2 × EN57), załadowanie 0–1.</summary>
public class PozycjaSkladu
{
    public string Seria { get; set; } = "";
    public int Liczba { get; set; } = 1;
    /// <summary>Załadowanie 0 (próżny) – 1 (pełny ładunek / komplet pasażerów).</summary>
    public double Zaladowanie { get; set; }
}

/// <summary>Skład pociągu (projektuje człowiek w edytorze taboru).</summary>
public class Sklad
{
    public string Id { get; set; } = "";
    public string Nazwa { get; set; } = "";
    /// <summary>Nastawienie hamulca składu: „G”, „P”, „R”.</summary>
    public string Nastawienie { get; set; } = "P";
    public List<PozycjaSkladu> Pojazdy { get; set; } = new();
    public string Uwagi { get; set; } = "";
}

/// <summary>Plik taboru <c>tabor.json</c>: katalog pojazdów, składy, składy domyślne kategorii (od B5).</summary>
public class TaborPlik
{
    public int Version { get; set; } = 1;
    public List<Pojazd> Pojazdy { get; set; } = new();
    public List<Sklad> Sklady { get; set; } = new();
    /// <summary>Kategoria pociągu (ŁKA, PR, IC, TOW…) → id składu domyślnego.</summary>
    public Dictionary<string, string> Domyslne { get; set; } = new();

    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public Pojazd Pojazd(string seria) => Pojazdy.FirstOrDefault(p => p.Seria == seria);
    public Sklad Sklad(string id) => Sklady.FirstOrDefault(s => s.Id == id);

    public static string Serialize(TaborPlik p) => JsonSerializer.Serialize(p, Json);
    public static TaborPlik Deserialize(string json) => JsonSerializer.Deserialize<TaborPlik>(json, Json) ?? new TaborPlik();
    public static TaborPlik Load(string path) => Deserialize(File.ReadAllText(path));

    /// <summary>Zapis przez plik tymczasowy; poprzednia wersja zostaje jako <c>.bak</c>.</summary>
    public void Save(string path)
    {
        path = Path.GetFullPath(path);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, Serialize(this));
        if (File.Exists(path)) File.Replace(tmp, path, path + ".bak");
        else File.Move(tmp, path);
    }

    /// <summary>Katalog wspólny (obok programu, <c>Data/tabor.json</c>) uzupełniony plikiem układu <c>&lt;układ&gt;.tabor.json</c> – wpisy układu zastępują wspólne o tej samej serii / id.</summary>
    public static TaborPlik Wczytaj(string stationPath = null)
    {
        var wsp = Path.Combine(AppContext.BaseDirectory, "Data", "tabor.json");
        var p = File.Exists(wsp) ? Load(wsp) : new TaborPlik();
        var wl = PlikUkladu(stationPath);
        if (wl != null && File.Exists(wl)) p.Uzupelnij(Load(wl));
        return p;
    }

    public static string PlikUkladu(string stationPath) =>
        stationPath == null ? null : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(stationPath)) ?? "", Path.GetFileNameWithoutExtension(stationPath) + ".tabor.json");

    public void Uzupelnij(TaborPlik u)
    {
        foreach (var x in u.Pojazdy) { Pojazdy.RemoveAll(q => q.Seria == x.Seria); Pojazdy.Add(x); }
        foreach (var x in u.Sklady) { Sklady.RemoveAll(q => q.Id == x.Id); Sklady.Add(x); }
        foreach (var kv in u.Domyslne) Domyslne[kv.Key] = kv.Value;
    }

    /// <summary>Kontrola pliku: brakujące serie w składach, pojazd napędny bez charakterystyki, skład bez napędu, dane niefizyczne.</summary>
    public List<string> Sprawdz()
    {
        var uw = new List<string>();
        foreach (var g in Pojazdy.GroupBy(p => p.Seria).Where(g => g.Count() > 1)) uw.Add($"seria {g.Key} jest w katalogu {g.Count()} razy");
        foreach (var p in Pojazdy)
        {
            if (string.IsNullOrWhiteSpace(p.Seria)) uw.Add("pojazd bez oznaczenia serii");
            if (p.Dlugosc <= 0 || p.Masa <= 0) uw.Add($"{p.Seria}: długość i masa muszą być dodatnie");
            if (p.MasaNapedna > p.Masa + p.Ladunek) uw.Add($"{p.Seria}: masa napędna większa niż masa pojazdu");
            bool nap = !Lcs.Tabor.Rodzaj.Wagon(p.Rodzaj);
            if (nap && (p.Trakcja.Count == 0 || p.MasaNapedna <= 0)) uw.Add($"{p.Seria}: pojazd napędny bez charakterystyki trakcyjnej albo masy napędnej");
            if (p.Rodzaj == Lcs.Tabor.Rodzaj.Bzt && (p.Char(Zasilanie.Siec) == null || p.Char(Zasilanie.Spalinowy) == null)) uw.Add($"{p.Seria}: BZT potrzebuje charakterystyki „sieć” i „spalinowy”");
            foreach (var c in p.Trakcja)
                for (int i = 1; i < c.Punkty.Count; i++)
                    if (c.Punkty[i][0] <= c.Punkty[i - 1][0]) { uw.Add($"{p.Seria} ({c.Zasilanie}): punkty F(v) nie rosną po v"); break; }
            if (p.MasaHamujaca.Count == 0) uw.Add($"{p.Seria}: brak masy hamującej");
        }
        foreach (var g in Sklady.GroupBy(s => s.Id).Where(g => g.Count() > 1)) uw.Add($"skład {g.Key} jest w pliku {g.Count()} razy");
        foreach (var s in Sklady)
        {
            foreach (var x in s.Pojazdy.Where(x => Pojazd(x.Seria) == null)) uw.Add($"skład {s.Id}: nie ma serii {x.Seria} w katalogu");
            if (s.Pojazdy.Count == 0) uw.Add($"skład {s.Id}: pusty");
            else if (!s.Pojazdy.Any(x => Pojazd(x.Seria)?.Napedny == true)) uw.Add($"skład {s.Id}: bez pojazdu napędnego");
        }
        foreach (var kv in Domyslne.Where(kv => Sklad(kv.Value) == null)) uw.Add($"kategoria {kv.Key}: nie ma składu {kv.Value}");
        return uw;
    }
}

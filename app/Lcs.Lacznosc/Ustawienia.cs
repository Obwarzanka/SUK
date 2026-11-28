using System.IO;
using System.Text.Json;

namespace Lcs.Lacznosc;

/// <summary>Ustawienia w %LocalAppData%\SUK\Lacznosc\ustawienia.json.</summary>
public sealed class Ustawienia
{
    public string Serwer { get; set; }
    /// <summary>Posterunek, dla którego działa terminal; puste – posterunek z gry.</summary>
    public string Stacja { get; set; }
    /// <summary>Przyciski odpowiedzi wysyłają od razu (bez „Wyślij”), gdy komplet danych.</summary>
    public bool OdRazu { get; set; }

    // ---- automaty (domyślnie wyłączone – wysyłają do gry bez kliknięcia)
    /// <summary>Automatyczna awizacja – zapytanie o drogę wolną do następnego posterunku.</summary>
    public bool Awizacja { get; set; }
    /// <summary>„szlak” – gdy pociąg zajmie szlak dojazdowy do naszej stacji; „min” – AwizacjaMin minut przed odjazdem wg rozkładu.</summary>
    public string AwizacjaTryb { get; set; } = "szlak";
    public int AwizacjaMin { get; set; } = 5;
    /// <summary>Posterunki wyłączone z automatów (nazwy) – nowi sąsiedzi są domyślnie włączeni.</summary>
    public List<string> BezAutomatow { get; set; } = new();
    /// <summary>Awizacja bez toru z rozmowy – pierwszy tor szlaku (zasadniczy).</summary>
    public bool TorDomyslny { get; set; } = true;
    /// <summary>Sam zgłasza „odjechał”, gdy pociąg opuści stację na szlak.</summary>
    public bool AutoOdjazd { get; set; }
    /// <summary>Sam zgłasza „przyjechał” posterunkowi, od którego przyjechał pociąg.</summary>
    public bool AutoPrzyjazd { get; set; }

    // ---- schemat
    /// <summary>Strona szlaku na schemacie: klucz „posterunek|sąsiad”, true – lewa.</summary>
    public Dictionary<string, bool> Strony { get; set; } = new();
    /// <summary>Tory szlaków poznane z komunikatów gry: klucz „posterunek|sąsiad”.</summary>
    public Dictionary<string, List<int>> Tory { get; set; } = new();
    /// <summary>Tory szlaków ustawione ręcznie (prawy klik na szlaku) – mają pierwszeństwo przed poznanymi.</summary>
    public Dictionary<string, List<int>> ToryReczne { get; set; } = new();
    /// <summary>Odcinki szlaku przy stacji poznane z pociągów wjeżdżających (nazwy obiektów gry „t…”): klucz – stacja.</summary>
    public Dictionary<string, List<string>> ObiektySzlaku { get; set; } = new();
    /// <summary>Odcinek szlaku bez skrótów stacji („l1_it2790”, „t…”) → „sąsiad|tor” (tor bywa pusty), poznane z pociągów: klucz – stacja.</summary>
    public Dictionary<string, Dictionary<string, string>> OdcinkiSasiadow { get; set; } = new();

    /// <summary>Tory z zapisów czatu (polecenia_*.jsonl, 2026-11-20…22) – na start, zanim program pozna je sam.</summary>
    public static readonly Dictionary<string, int[]> ToryZnane = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Koluszki|Gałkówek"] = new[] { 1, 2 },
        ["Koluszki|Rogów"] = new[] { 1, 2 },
        ["Koluszki|Żakowice Południowe"] = new[] { 2, 3 },
        ["Koluszki|Rokiciny"] = new[] { 2 },
        ["Łazy Ła|Łazy"] = new[] { 224 },
        ["Łazy Ła|Zawiercie"] = new[] { 3, 4 },
    };

    static string Plik => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SUK", "Lacznosc", "ustawienia.json");

    public static Ustawienia Wczytaj()
    {
        try { return JsonSerializer.Deserialize<Ustawienia>(File.ReadAllText(Plik)) ?? new(); } catch { return new(); }
    }

    public void Zapisz()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(Plik)); File.WriteAllText(Plik, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); } catch { }
    }

    public bool Automat(string sasiad) => !BezAutomatow.Contains(sasiad, StringComparer.OrdinalIgnoreCase);
}

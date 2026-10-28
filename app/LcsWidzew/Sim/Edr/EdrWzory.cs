using System.Text.Json;
using System.Text.RegularExpressions;

namespace LcsWidzew.Sim;

/// <summary>Pole wzoru telefonogramu: godzina, tor szlakowy dziennika, numer pociągu, tekst albo wybór z listy.</summary>
public class EdrPoleWzoru { public string Id, Etykieta, Rodzaj = "tekst"; public List<string> Opcje = new(); public bool Wymagane = true; }

/// <summary>
/// Skutek wzoru dla stanu bieżącego toru szlakowego z pola „tor” (albo każdego toru): status, rodzaj ruchu, podstawa prowadzenia ruchu
/// („telefoniczne” – telefoniczne zapowiadanie, „*” – przywrócenie zasadniczego z ustawień dziennika); po przejeździe pociągu z pola „pociag”.
/// </summary>
public class EdrSkutekWzoru { public string Status, Rodzaj, Podstawa; public bool WszystkieTory, PoPrzejezdzie; }

/// <summary>Wzór telefonogramu wg Ir-1 (Data/telefonogramy.json): numer, tytuł, treść z polami, pola, skutek dla szlaku.</summary>
public class EdrWzor { public string Nr, Tytul, Tresc; public List<string> Pola = new(); public EdrSkutekWzoru Skutek; }

/// <summary>
/// Wzory telefonogramów (E8e, PLAN_EDR.md): 22 wzory wg Ir-1 przepisane z EDR.exe (decyzja właściciela 2026-10-28). Treść powstaje z pól wzoru,
/// a wzory zamknięcia / otwarcia toru, zapowiadania telefonicznego i ruchu jednotorowego zmieniają stan bieżący szlaku same.
/// </summary>
public static class EdrWzory
{
    public static List<EdrWzor> Lista { get; private set; } = new();
    public static Dictionary<string, EdrPoleWzoru> Pola { get; private set; } = new();
    /// <summary>Błąd pliku wzorów (null – wczytany); bez pliku – tylko „[bez szablonu]”.</summary>
    public static string Blad { get; private set; }
    public static readonly string[] Wbudowane = { "godz_h", "godz_m", "od", "do", "od_do" };
    static readonly Regex Miejsce = new(@"\{( ?)([a-z_]+)\}", RegexOptions.CultureInvariant);

    static EdrWzory() => Wczytaj(Path.Combine(AppContext.BaseDirectory, "Data", "telefonogramy.json"));

    public static void Wczytaj(string path)
    {
        Lista = new(); Pola = new(); Blad = null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            static string S(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            static bool B(JsonElement e, string n, bool d) => e.TryGetProperty(n, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : d;
            foreach (var p in doc.RootElement.GetProperty("pola").EnumerateObject())
                Pola[p.Name] = new EdrPoleWzoru
                {
                    Id = p.Name, Etykieta = S(p.Value, "etykieta") ?? p.Name, Rodzaj = S(p.Value, "rodzaj") ?? "tekst", Wymagane = B(p.Value, "wymagane", true),
                    Opcje = p.Value.TryGetProperty("opcje", out var o) ? o.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : new()
                };
            foreach (var e in doc.RootElement.GetProperty("wzory").EnumerateArray())
            {
                var w = new EdrWzor { Nr = S(e, "nr"), Tytul = S(e, "tytul"), Tresc = S(e, "tresc") ?? "", Pola = e.TryGetProperty("pola", out var pl) ? pl.EnumerateArray().Select(x => x.GetString()).ToList() : new() };
                if (e.TryGetProperty("skutek", out var sk) && sk.ValueKind == JsonValueKind.Object)
                    w.Skutek = new EdrSkutekWzoru { Status = S(sk, "status"), Rodzaj = S(sk, "rodzaj"), Podstawa = S(sk, "podstawa"), WszystkieTory = B(sk, "wszystkieTory", false), PoPrzejezdzie = B(sk, "poPrzejezdzie", false) };
                Lista.Add(w);
            }
            if (Sprawdz() is string b) { Blad = b; Lista = new(); }
        }
        catch (Exception ex) { Blad = $"nie wczytano wzorów telefonogramów ({Path.GetFileName(path)}): {ex.Message}"; Lista = new(); }
    }

    /// <summary>Błąd wzorów (null – poprawne): numery bez powtórzeń, pola i miejsca w treści znane, skutek z torem ma pole „tor”, po przejeździe – pole „pociag”.</summary>
    public static string Sprawdz()
    {
        if (Lista.Any(w => string.IsNullOrWhiteSpace(w.Nr) || string.IsNullOrWhiteSpace(w.Tytul))) return "wzór bez numeru albo tytułu";
        if (Lista.GroupBy(w => w.Nr).FirstOrDefault(g => g.Count() > 1) is { } pw) return $"numer wzoru {pw.Key} powtórzony";
        foreach (var w in Lista)
        {
            if (w.Pola.FirstOrDefault(p => !Pola.ContainsKey(p)) is string np) return $"wzór {w.Nr}: nieznane pole {np}";
            foreach (Match m in Miejsce.Matches(w.Tresc))
            {
                var id = m.Groups[2].Value;
                if (!Wbudowane.Contains(id) && !w.Pola.Contains(id)) return $"wzór {w.Nr}: w treści {{{id}}} bez pola";
                if (id is "godz_h" or "godz_m" && !w.Pola.Contains("godz")) return $"wzór {w.Nr}: {{{id}}} bez pola godz";
            }
            if (w.Skutek != null && !w.Skutek.WszystkieTory && !w.Pola.Contains("tor")) return $"wzór {w.Nr}: skutek bez pola tor";
            if (w.Skutek?.PoPrzejezdzie == true && !w.Pola.Contains("pociag")) return $"wzór {w.Nr}: zamknięcie po przejeździe bez pola pociag";
        }
        return null;
    }

    public static EdrWzor Wzor(string nr) => string.IsNullOrEmpty(nr) ? null : Lista.FirstOrDefault(w => w.Nr == nr);

    /// <summary>
    /// Treść telefonogramu ze wzoru i pól (od – posterunek nadający, do – odbierający; tory – tory szlakowe dziennika, null – bez sprawdzania).
    /// Podgląd: puste pola jako „[etykieta]”, bez błędów.
    /// </summary>
    public static (string Tresc, string Blad) Tresc(EdrWzor w, Dictionary<string, string> pola, string od, string @do, IEnumerable<string> tory, bool podglad = false)
    {
        pola ??= new();
        var wart = new Dictionary<string, string>();
        foreach (var id in w.Pola)
        {
            var p = Pola[id];
            var v = (pola.GetValueOrDefault(id) ?? "").Trim();
            if (p.Rodzaj == "godzina" && v != "")
            {
                var g = Edr.Godzina(v);
                if (g is null or "---" || g.Contains('/')) { if (!podglad) return ("", $"{p.Etykieta}: błędna godzina"); g = v; }
                v = g;
            }
            if (p.Rodzaj == "tor" && v != "" && tory != null && tory.Any() && !tory.Contains(v, StringComparer.OrdinalIgnoreCase) && !podglad)
                return ("", $"{p.Etykieta}: tor {v} nie należy do szlaku (tory: {string.Join(", ", tory)})");
            if (p.Rodzaj == "wybor" && v != "" && p.Opcje.Count > 0 && !p.Opcje.Contains(v) && !podglad) return ("", $"{p.Etykieta}: wybierz z listy");
            if (v == "" && p.Wymagane && !(p.Rodzaj == "wybor" && p.Opcje.Contains(""))) { if (!podglad) return ("", $"Wypełnij pole „{p.Etykieta}”"); v = $"[{p.Etykieta}]"; }
            wart[id] = v;
        }
        var godz = wart.GetValueOrDefault("godz") ?? "";
        wart["godz_h"] = godz.Length == 5 ? godz[..2] : godz; wart["godz_m"] = godz.Length == 5 ? godz[3..] : godz;
        wart["od"] = string.IsNullOrWhiteSpace(od) ? "[posterunek]" : od.Trim(); wart["do"] = string.IsNullOrWhiteSpace(@do) ? "[posterunek]" : @do.Trim();
        wart["od_do"] = $"od {wart["od"]} do {wart["do"]}";
        var t = Miejsce.Replace(w.Tresc, m => wart.GetValueOrDefault(m.Groups[2].Value) is string v && v != "" ? m.Groups[1].Value + v : "");
        return (t, null);
    }

    /// <summary>Zmiany stanu bieżącego torów szlakowych dziennika ze skutku wzoru (bez skutku albo bez dziennika – brak zmian).</summary>
    public static List<EdrZmianaToru> Skutek(EdrWzor w, Dictionary<string, string> pola, EdrDziennik d)
    {
        var l = new List<EdrZmianaToru>();
        if (w?.Skutek == null || d == null) return l;
        var s = w.Skutek; pola ??= new();
        var tor = (pola.GetValueOrDefault("tor") ?? "").Trim();
        foreach (var t in d.Biezace.Tory.Where(t => s.WszystkieTory || string.Equals(t.Tor, tor, StringComparison.OrdinalIgnoreCase)))
            l.Add(new EdrZmianaToru
            {
                Szlak = t.Szlak, Status = s.Status, Rodzaj = s.Rodzaj, Podstawa = s.Podstawa == "telefoniczne" ? Edr.Podstawy[3] : s.Podstawa,
                PoPociagu = s.PoPrzejezdzie ? (pola.GetValueOrDefault("pociag") ?? "").Trim() : null
            });
        return l;
    }

    /// <summary>Opis skutku wzoru dla okna telefonogramu.</summary>
    public static string OpisSkutku(EdrWzor w)
    {
        var s = w?.Skutek;
        if (s == null) return "Wzór nie zmienia stanu bieżącego szlaku.";
        var cz = new List<string>();
        if (s.Status != null) cz.Add("tor " + s.Status.ToLowerInvariant() + (s.PoPrzejezdzie ? " po przyjeździe pociągu" : ""));
        if (s.Rodzaj != null) cz.Add(s.Rodzaj == "*" ? "rodzaj ruchu zasadniczy (z ustawień dziennika)" : "ruch " + s.Rodzaj.ToLowerInvariant());
        if (s.Podstawa != null) cz.Add(s.Podstawa == "*" ? "prowadzenie ruchu zasadnicze (z ustawień dziennika)" : "prowadzenie ruchu jak dla telefonicznego zapowiadania");
        return "Zmiana stanu bieżącego szlaku ze wzoru: " + string.Join(", ", cz) + (s.WszystkieTory ? " – wszystkie tory szlakowe" : " – tor z pola „Tor szlakowy nr”") + ".";
    }
}

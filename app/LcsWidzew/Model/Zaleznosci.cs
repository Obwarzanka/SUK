using System.Text.Json;
using System.Text.Json.Nodes;

namespace LcsWidzew.Model;

/// <summary>Wariant drogi ochronnej: odcinki i zwrotnice z położeniem (np. „MT:2+”), przestawiane i utwierdzane przy nastawianiu.</summary>
public class ZalOchronna
{
    public List<string> Odcinki = new(), Zwrotnice = new();
    public string Opis => string.Join(" ", Odcinki.Select(Station.Lbl).Concat(Zwrotnice.Select(Station.Lbl)));
}

/// <summary>
/// Zależności specjalne jednego przebiegu (tablica zależności projektowana przez człowieka; program niczego tu nie wpisuje sam).
/// Klucz: start, koniec, rodzaj („P” / „M”) i opcjonalnie położenia zwrotnic wariantu (puste – wszystkie warianty).
/// </summary>
public class ZalPrzebieg
{
    public string Start, End, Type = "P", Sw = "";
    /// <summary>Przebieg bez drogi ochronnej.</summary>
    public bool BezOchronnej;
    /// <summary>Warianty drogi ochronnej w kolejności – komputer zależności bierze pierwszy możliwy. Pusta lista – droga ochronna z układu.</summary>
    public List<ZalOchronna> DrogaOchronna = new();
    /// <summary>Przebiegi wykluczające się z tym (obustronnie), ponad wynikające z układu – klucze „P MT:A→MT:C2”.</summary>
    public List<string> Sprzeczne = new();
    /// <summary>Przebiegi, które mogą być nastawione jednocześnie mimo wspólnego odcinka (np. drogi ochronnej w grupie rozjazdów).</summary>
    public List<string> Dopuszczone = new();
    /// <summary>Ochrona boczna ponad wyznaczoną z układu: zwrotnica z położeniem („MT:4+”), wykolejnica, sygnalizator („Stój”).</summary>
    public List<string> OchronaDodaj = new(), OchronaUsun = new();
    /// <summary>Dodatkowe warunki: „MT:3+” (zwrotnica w położeniu), „MT:T1 wolny”, „MT:W1 założona” / „zdjęta”, „MT:B stój”.</summary>
    public List<string> Warunki = new();

    public string Klucz => Zaleznosci.Klucz(Type, Start, End);
    public bool Pasuje(string type, string start, string end, string swKey) => Type == type && Start == start && End == end && (string.IsNullOrEmpty(Sw) || Sw == swKey);
    public bool Pusty => !BezOchronnej && DrogaOchronna.Count == 0 && Sprzeczne.Count == 0 && Dopuszczone.Count == 0 && OchronaDodaj.Count == 0 && OchronaUsun.Count == 0 && Warunki.Count == 0;
}

/// <summary>
/// Plik zależności specjalnych <c>&lt;układ&gt;.zaleznosci.json</c> obok pliku układu. Nie nadpisuje go eksport „Do symulatora”
/// ani zapis układu. Brak pliku – przebiegi jak z układu (reguły routeRules dalej z pliku układu).
/// </summary>
public class Zaleznosci
{
    [System.Text.Json.Serialization.JsonIgnore] public string Plik;
    public List<ZalPrzebieg> Przebiegi = new();

    public static string Klucz(string type, string start, string end) => $"{type} {start}→{end}";
    public static string Norm(string k) => string.Join(" ", (k ?? "").Replace("->", "→").Split(' ', StringSplitOptions.RemoveEmptyEntries)).Replace(" →", "→").Replace("→ ", "→");
    public static string PlikDla(string stationPath) => stationPath == null ? null : Path.Combine(Path.GetDirectoryName(stationPath) ?? "", Path.GetFileNameWithoutExtension(stationPath) + ".zaleznosci.json");

    public IEnumerable<ZalPrzebieg> Dla(string type, string start, string end, string swKey) => Przebiegi.Where(p => p.Pasuje(type, start, end, swKey));

    public static Zaleznosci Wczytaj(string stationPath)
    {
        var z = new Zaleznosci { Plik = PlikDla(stationPath) };
        if (z.Plik == null || !File.Exists(z.Plik)) return z;
        z.Przebiegi = Parsuj(File.ReadAllText(z.Plik));
        return z;
    }

    static List<string> Lista(JsonNode n) => n is JsonArray a ? a.Select(x => x?.GetValue<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList() : new();

    public static List<ZalPrzebieg> Parsuj(string json)
    {
        var res = new List<ZalPrzebieg>();
        if (JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })?["przebiegi"] is not JsonArray arr) return res;
        foreach (var o in arr.OfType<JsonObject>())
        {
            var p = new ZalPrzebieg { Start = (string)o["start"], End = (string)o["end"], Type = (string)o["type"] ?? "P", Sw = (string)o["sw"] ?? "" };
            if (o["drogaOchronna"] is JsonValue v && v.TryGetValue<string>(out var s) && s == "brak") p.BezOchronnej = true;
            else if (o["drogaOchronna"] is JsonArray da)
                foreach (var w in da.OfType<JsonObject>()) p.DrogaOchronna.Add(new ZalOchronna { Odcinki = Lista(w["odcinki"]), Zwrotnice = Lista(w["zwrotnice"]) });
            p.Sprzeczne = Lista(o["sprzeczne"]).Select(Norm).ToList();
            p.Dopuszczone = Lista(o["dopuszczone"]).Select(Norm).ToList();
            if (o["ochronaBoczna"] is JsonObject ob) { p.OchronaDodaj = Lista(ob["dodaj"]); p.OchronaUsun = Lista(ob["usun"]); }
            p.Warunki = Lista(o["warunki"]);
            if (p.Start != null && p.End != null) res.Add(p);
        }
        return res;
    }

    public string DoJson()
    {
        static JsonArray A(IEnumerable<string> l) => new(l.Select(x => (JsonNode)x).ToArray());
        var arr = new JsonArray();
        foreach (var p in Przebiegi.Where(p => !p.Pusty))
        {
            var o = new JsonObject { ["start"] = p.Start, ["end"] = p.End, ["type"] = p.Type };
            if (!string.IsNullOrEmpty(p.Sw)) o["sw"] = p.Sw;
            if (p.BezOchronnej) o["drogaOchronna"] = "brak";
            else if (p.DrogaOchronna.Count > 0) o["drogaOchronna"] = new JsonArray(p.DrogaOchronna.Select(w => (JsonNode)new JsonObject { ["odcinki"] = A(w.Odcinki), ["zwrotnice"] = A(w.Zwrotnice) }).ToArray());
            if (p.Sprzeczne.Count > 0) o["sprzeczne"] = A(p.Sprzeczne);
            if (p.Dopuszczone.Count > 0) o["dopuszczone"] = A(p.Dopuszczone);
            if (p.OchronaDodaj.Count + p.OchronaUsun.Count > 0) o["ochronaBoczna"] = new JsonObject { ["dodaj"] = A(p.OchronaDodaj), ["usun"] = A(p.OchronaUsun) };
            if (p.Warunki.Count > 0) o["warunki"] = A(p.Warunki);
            arr.Add(o);
        }
        return new JsonObject { ["przebiegi"] = arr }.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    /// <summary>Zapis z kopią poprzedniej wersji (.bak).</summary>
    public void Zapisz()
    {
        if (Plik == null) throw new InvalidOperationException("Układ bez pliku – nie ma gdzie zapisać zależności.");
        if (File.Exists(Plik)) File.Copy(Plik, Plik + ".bak", true);
        File.WriteAllText(Plik, DoJson());
    }

    /// <summary>Rozbiór warunku: obiekt i stan („+”/„-” zwrotnicy, „wolny”, „założona”/„zdjęta”, „stój”).</summary>
    public static (string Obj, string Stan) Warunek(string w)
    {
        var t = (w ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (t.Length == 0) return ("", "");
        if (t.Length == 1 && (t[0].EndsWith('+') || t[0].EndsWith('-'))) return (t[0][..^1], t[0][^1..]);
        return (t[0], t.Length > 1 ? t[1].ToLowerInvariant() : "");
    }

    /// <summary>Kontrola danych wobec układu: nieistniejące obiekty, sprzeczności. Zwraca uwagi (puste – w porządku).</summary>
    public List<string> Sprawdz(Station L, IEnumerable<string> kluczePrzebiegow)
    {
        var uw = new List<string>(); var klucze = kluczePrzebiegow.ToHashSet();
        bool Zwr(string s) => s.Length > 1 && (s[^1] is '+' or '-') && L.Switches.ContainsKey(s[..^1]);
        foreach (var p in Przebiegi)
        {
            var k = p.Klucz;
            if (!klucze.Contains(k)) uw.Add($"{k}: brak takiego przebiegu w tablicy");
            foreach (var w in p.DrogaOchronna)
            {
                if (w.Odcinki.Count == 0) uw.Add($"{k}: wariant drogi ochronnej bez odcinków");
                foreach (var o in w.Odcinki) if (!L.Sections.ContainsKey(o)) uw.Add($"{k}: droga ochronna – brak odcinka {o}");
                foreach (var z in w.Zwrotnice) if (!Zwr(z)) uw.Add($"{k}: droga ochronna – „{z}” to nie zwrotnica z położeniem (np. MT:2+)");
                foreach (var g in w.Zwrotnice.Where(Zwr).GroupBy(z => z[..^1]).Where(g => g.Distinct().Count() > 1)) uw.Add($"{k}: droga ochronna – zwrotnica {g.Key} w dwóch położeniach");
            }
            foreach (var s in p.Sprzeczne.Concat(p.Dopuszczone)) if (!klucze.Contains(s)) uw.Add($"{k}: brak przebiegu {s}");
            foreach (var s in p.Sprzeczne.Intersect(p.Dopuszczone)) uw.Add($"{k}: {s} jednocześnie sprzeczny i dopuszczony");
            if (p.Sprzeczne.Contains(k) || p.Dopuszczone.Contains(k)) uw.Add($"{k}: przebieg wskazuje sam siebie");
            foreach (var e in p.OchronaDodaj.Concat(p.OchronaUsun))
                if (!Zwr(e) && !L.Derailers.ContainsKey(e) && !L.Signals.ContainsKey(e) && !L.Switches.ContainsKey(e)) uw.Add($"{k}: ochrona boczna – nieznany element {e}");
            foreach (var w in p.Warunki)
            {
                var (obj, st) = Warunek(w);
                bool ok = st switch
                {
                    "+" or "-" => L.Switches.ContainsKey(obj),
                    "wolny" => L.Sections.ContainsKey(obj),
                    "założona" or "zdjęta" => L.Derailers.ContainsKey(obj),
                    "stój" => L.Signals.ContainsKey(obj),
                    _ => false
                };
                if (!ok) uw.Add($"{k}: nieczytelny warunek „{w}” (np. MT:3+, MT:T1 wolny, MT:W1 założona, MT:B stój)");
            }
        }
        // sprzeczny w jednym wpisie, a dopuszczony w drugim
        foreach (var p in Przebiegi) foreach (var q in Przebiegi)
            if (p.Sprzeczne.Contains(q.Klucz) && q.Dopuszczone.Contains(p.Klucz)) uw.Add($"{p.Klucz} / {q.Klucz}: sprzeczne w jednym wpisie, dopuszczone w drugim");
        return uw.Distinct().ToList();
    }
}

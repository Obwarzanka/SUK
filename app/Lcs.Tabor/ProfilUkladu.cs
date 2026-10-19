using System.Text.Json.Nodes;

namespace Lcs.Tabor;

/// <summary>
/// Profil do przejazdu próbnego z układu symulatora (<c>station.json</c>, np. z „Do symulatora…”): kawałki toru o danym numerze połączone
/// po węzłach (A → B), z pochyleniem <c>poch</c>, prędkością <c>vmax</c> i siecią <c>elec</c>. Plik tylko do odczytu.
/// Prędkość toru bez <c>vmax</c>: szlak – bez ograniczenia (Vmax pociągu), tor stacyjny – 60 km/h (jak w symulatorze).
/// </summary>
public static class ProfilUkladu
{
    public record Tor(string Numer, int Kawalki, double Dlugosc);

    static JsonArray Edges(string path) => JsonNode.Parse(File.ReadAllText(path), documentOptions: new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip })?["edges"] as JsonArray ?? new JsonArray();
    static string S(JsonNode n, string k) => n?[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";
    static double D(JsonNode n, string k, double def = double.NaN) => n?[k] is JsonValue v && v.TryGetValue<double>(out var d) ? d : def;
    static bool B(JsonNode n, string k) => n?[k] is JsonValue v && (v.TryGetValue<bool>(out var b) ? b : v.TryGetValue<string>(out var s) && s == "true");

    /// <summary>Numery torów z najdłuższym ciągiem kawałków (do wyboru w oknie).</summary>
    public static List<Tor> Tory(string path)
    {
        var e = Edges(path);
        return e.Select(x => S(x, "track")).Where(n => n.Length > 0).Distinct()
            .Select(n => { var c = Ciag(e, n); return new Tor(n, c.Count, c.Sum(x => D(x, "len", 0))); })
            .Where(t => t.Kawalki > 0).OrderByDescending(t => t.Dlugosc).ToList();
    }

    /// <summary>Najdłuższy ciąg kawałków toru o numerze <paramref name="nr"/> (od A do B).</summary>
    static List<JsonNode> Ciag(JsonArray e, string nr)
    {
        var tor = e.Where(x => S(x, "track") == nr).ToList();
        var odA = tor.GroupBy(x => S(x, "a")).ToDictionary(g => g.Key, g => g.First());
        var bs = tor.Select(x => S(x, "b")).ToHashSet();
        List<JsonNode> best = new();
        foreach (var start in tor.Where(x => !bs.Contains(S(x, "a"))).DefaultIfEmpty(tor.FirstOrDefault()))
        {
            if (start == null) continue;
            var l = new List<JsonNode>(); var seen = new HashSet<JsonNode>();
            for (var x = start; x != null && seen.Add(x); x = odA.GetValueOrDefault(S(x, "b"))) l.Add(x);
            if (l.Sum(q => D(q, "len", 0)) > best.Sum(q => D(q, "len", 0))) best = l;
        }
        return best;
    }

    /// <summary>Profil toru <paramref name="nr"/>; <paramref name="odwrotnie"/> – jazda od B do A (pochylenia z przeciwnym znakiem).</summary>
    public static List<OdcinekProfilu> Profil(string path, string nr, bool odwrotnie = false)
    {
        var e = Edges(path);
        bool elecDane = e.Any(x => B(x, "elec"));
        var l = Ciag(e, nr).Select(x =>
        {
            double v = D(x, "vmax", 0);
            bool szlak = B(x, "szlak");
            double poch = D(x, "poch", 0);
            return new OdcinekProfilu
            {
                Dlugosc = D(x, "len", 0), Pochylenie = odwrotnie ? -poch : poch, VMax = v > 0 ? v : szlak ? 200 : 60,
                Siec = !elecDane || B(x, "elec"), Opis = $"{S(x, "id")} {S(x, "sec")}".Trim(),
            };
        }).Where(x => x.Dlugosc > 0).ToList();
        if (odwrotnie) l.Reverse();
        return l;
    }
}

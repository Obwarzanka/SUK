using System.Text.Json;
using System.Text.Json.Nodes;

namespace LcsWidzew.Sim;

/// <summary>
/// Rozkład jazdy układu z pliku <c>&lt;układ&gt;.rozklad.json</c> obok pliku układu (jak <c>.zaleznosci.json</c>; pisze człowiek).
/// Gdy plik jest, scenariusz „Ruch podstawowy” bierze pociągi z niego (rozkład wbudowany dotyczy obszaru Widzewa). Brak pliku – bez zmian.
/// Pociąg: <c>t</c> – czas od początku [s], <c>numer</c>, <c>kat</c> (ŁKA, PR, IC, TOW…), <c>ze</c> – szlak, z którego przyjeżdża
/// (posterunek sąsiedni spoza obszaru), albo <c>odcinek</c> + <c>kierunek</c> (±1 – A→B toru) – pociąg stoi na torze stacyjnym;
/// <c>na</c> – szlak, na który ma odjechać (puste – kończy bieg); <c>postoj</c> – kody posterunków z postojem (brak – bez postoju,
/// pusta lista – na każdym peronie); <c>odjazd</c> – czas odjazdu [s]; <c>lok</c> – lokomotywa manewrowa na <c>odcinek</c>.
/// </summary>
public static class RozkladPlik
{
    public static string PlikDla(string stationPath) => stationPath == null ? null : Path.Combine(Path.GetDirectoryName(stationPath) ?? "", Path.GetFileNameWithoutExtension(stationPath) + ".rozklad.json");

    /// <summary>Pociągi z pliku; null – brak pliku (albo plik bez listy „pociagi”).</summary>
    public static List<TimetableEntry> Wczytaj(string stationPath)
    {
        var plik = PlikDla(stationPath);
        if (plik == null || !File.Exists(plik)) return null;
        return Parsuj(File.ReadAllText(plik));
    }

    public static List<TimetableEntry> Parsuj(string json)
    {
        if (JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })?["pociagi"] is not JsonArray arr) return null;
        var res = new List<TimetableEntry>();
        foreach (var o in arr.OfType<JsonObject>())
        {
            var q = new TimetableEntry
            {
                T = Liczba(o["t"]) ?? 1, Num = (string)o["numer"], Cat = (string)o["kat"] ?? "PR", From = (string)o["ze"], To = Pusty((string)o["na"]),
                StartSec = (string)o["odcinek"], StartDir = (int)(Liczba(o["kierunek"]) ?? 1), Loco = o["lok"] is JsonValue lv && lv.TryGetValue<bool>(out var lb) && lb,
                Dep = Liczba(o["odjazd"]), Track = (string)o["tor"] ?? "",
            };
            if (q.Num == null || (q.From == null && q.StartSec == null)) continue;
            if (!Trains.Cats.ContainsKey(q.Cat)) q.Cat = "PR";
            if (q.Loco) q.Cat = "MAN";
            if (o["postoj"] is JsonArray pa)
            {
                q.Stop = true;
                var posts = pa.Select(x => (string)x).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet();
                if (posts.Count > 0) q.StopAt = posts;
            }
            res.Add(q);
        }
        return res;
    }

    static double? Liczba(JsonNode n) => n is JsonValue v && v.TryGetValue<double>(out var d) ? d : null;
    static string Pusty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;
}

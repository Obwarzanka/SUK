using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace Lcs.Lacznosc;

/// <summary>
/// Rozkład z publicznego API SimRail (to samo źródło co EDR): api/getEDRTimetables?serverCode=… – wszystkie pociągi serwera
/// z punktami rozkładu (nameForPerson, arrivalTime, departureTime, track, platform). Lista serwerów: servers-open.
/// Program pobiera tylko dane publiczne serwera, bez logowania.
/// </summary>
public static class RozkladSimRail
{
    static readonly HttpClient Http = new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }) { Timeout = TimeSpan.FromSeconds(40) };

    public sealed record Punkt(string Nazwa, DateTime? Przyjazd, DateTime? Odjazd, string Tor, string Peron, string Rodzaj, bool Postoj);
    public sealed record Pociag(string Nr, string Kategoria, string Od, string Do, string Lok, string Dlugosc, string Brutto, List<Punkt> Punkty);

    public static async Task<List<(string Kod, string Nazwa)>> SerweryAsync(CancellationToken ct)
    {
        var b = await Http.GetByteArrayAsync("https://api.simrail.eu:8084/servers-open", ct);
        using var doc = JsonDocument.Parse(b);
        var wynik = new List<(string, string)>();
        if (doc.RootElement.TryGetProperty("data", out var d))
            foreach (var s in d.EnumerateArray())
            {
                if (s.TryGetProperty("IsActive", out var a) && a.ValueKind == JsonValueKind.False) continue;
                wynik.Add((MostKlient.S(s, "ServerCode"), MostKlient.S(s, "ServerName")));
            }
        return wynik.Where(x => !string.IsNullOrEmpty(x.Item1)).OrderBy(x => x.Item1).ToList();
    }

    public static async Task<List<Pociag>> PobierzAsync(string serwer, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(serwer) || serwer.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_')) throw new ArgumentException("Nieprawidłowy kod serwera.");
        var b = await Http.GetByteArrayAsync("https://api.simrail.eu:8082/api/getEDRTimetables?serverCode=" + Uri.EscapeDataString(serwer), ct);
        return Czytaj(b);
    }

    public static List<Pociag> Czytaj(byte[] json)
    {
        using var doc = JsonDocument.Parse(json);
        var wynik = new List<Pociag>();
        foreach (var t in doc.RootElement.EnumerateArray())
        {
            var punkty = new List<Punkt>();
            if (t.TryGetProperty("timetable", out var tt) && tt.ValueKind == JsonValueKind.Array)
                foreach (var p in tt.EnumerateArray())
                    punkty.Add(new Punkt(MostKlient.S(p, "nameForPerson") ?? "", Data(p, "arrivalTime"), Data(p, "departureTime"),
                        Tekst(p, "track"), MostKlient.S(p, "platform"), MostKlient.S(p, "trainType"),
                        p.TryGetProperty("plannedStop", out var ps) && ps.ValueKind == JsonValueKind.Number && ps.GetDouble() > 0));
            var nr = MostKlient.S(t, "trainNoLocal");
            if (string.IsNullOrEmpty(nr)) continue;
            wynik.Add(new Pociag(nr, MostKlient.S(t, "trainName") ?? "", MostKlient.S(t, "startStation") ?? "", MostKlient.S(t, "endStation") ?? "",
                MostKlient.S(t, "locoType") ?? "", Tekst(t, "trainLength"), Tekst(t, "trainWeight"), punkty));
        }
        return wynik;
    }

    static DateTime? Data(JsonElement e, string n)
    {
        var s = MostKlient.S(e, n);
        return !string.IsNullOrEmpty(s) && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }

    static string Tekst(JsonElement e, string n) => !e.TryGetProperty(n, out var v) ? "" : v.ValueKind switch
    {
        JsonValueKind.String => v.GetString(),
        JsonValueKind.Number => v.GetDecimal().ToString("0.#", CultureInfo.InvariantCulture),
        _ => "",
    };

    /// <summary>Czy nazwa punktu rozkładu pasuje do nazwy posterunku (gra i API piszą nazwy różnie – porównanie luźne).</summary>
    public static bool Pasuje(string punkt, string stacja)
    {
        if (string.IsNullOrWhiteSpace(punkt) || string.IsNullOrWhiteSpace(stacja)) return false;
        string N(string s) => new string(s.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c)).ToArray()).Replace("ł", "l");
        var a = N(punkt); var b = N(stacja);
        return a == b || a.StartsWith(b) || b.StartsWith(a);
    }
}

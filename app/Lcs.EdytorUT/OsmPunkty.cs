using System.Globalization;
using System.Text.Json;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Punkty osi torów z OpenStreetMap (Overpass API) – tylko jako punkty do dopasowania łuku („Łuk z punktów”, klawisz O). Tory rysuje
/// projektant (decyzja z 2026-10-16: bez generatora z OSM). Dane © współtwórcy OpenStreetMap (ODbL).
/// </summary>
public static class OsmPunkty
{
    public const string Overpass = "https://overpass-api.de/api/interpreter";
    public const string Attribution = "Punkty: © współtwórcy OpenStreetMap (ODbL)";

    /// <summary>Węzły dróg kolejowych (rail, light_rail, narrow_gauge, tram) w prostokącie PUWG 1992 – w PUWG.</summary>
    public static async Task<List<XY>> Nodes(XY a, XY b, CancellationToken ct)
    {
        var (la1, lo1) = Geo.ToWgs84(a); var (la2, lo2) = Geo.ToWgs84(b);
        double s = Math.Min(la1, la2), n = Math.Max(la1, la2), w = Math.Min(lo1, lo2), e = Math.Max(lo1, lo2);
        string q = string.Format(CultureInfo.InvariantCulture, "[out:json][timeout:25];way[\"railway\"~\"^(rail|light_rail|narrow_gauge|tram)$\"]({0:0.000000},{1:0.000000},{2:0.000000},{3:0.000000});(._;>;);out skel qt;", s, w, n, e);
        var data = await Pobieranie.Retry(() => Pobieranie.Get(Overpass + "?data=" + Uri.EscapeDataString(q), ct), ct, 3);
        return Parse(data);
    }

    /// <summary>Węzły z odpowiedzi Overpass (JSON) – w PUWG 1992.</summary>
    public static List<XY> Parse(byte[] json)
    {
        var res = new List<XY>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("elements", out var els)) return res;
        foreach (var el in els.EnumerateArray())
            if (el.TryGetProperty("type", out var t) && t.GetString() == "node" && el.TryGetProperty("lat", out var la) && el.TryGetProperty("lon", out var lo))
                res.Add(Geo.ToPuwg92(la.GetDouble(), lo.GetDouble()));
        return res;
    }

    /// <summary>Węzły blisko osi toru (do <paramref name="maxDist"/> m w terenie), bez powtórzeń (co najmniej 2 m od siebie).</summary>
    public static List<XY> NearTrack(IEnumerable<XY> nodes, TrasaResult r, double maxDist = 8)
    {
        var res = new List<XY>();
        foreach (var p in nodes)
            if (Budowa.Dist(r, p) / r.K <= maxDist && res.All(q => XY.Dist(q, p) / r.K > 2)) res.Add(p);
        return res;
    }
}

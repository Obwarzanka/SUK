using System.Globalization;
using System.IO;
using System.Text.Json;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Punkty osi torów z OpenStreetMap (Overpass API) – tylko jako punkty do dopasowania łuku („Łuk z punktów”, klawisz O) i ślad
/// do narzędzia „Ślad”. Tory rysuje projektant (decyzja z 2026-10-16: bez generatora z OSM). Dane © współtwórcy OpenStreetMap (ODbL).
/// </summary>
public static class OsmPunkty
{
    public const string Overpass = "https://overpass-api.de/api/interpreter";
    /// <summary>Drugi serwer Overpass, gdy pierwszy jest przeciążony (502 / 504).</summary>
    public const string OverpassZapas = "https://overpass.kumi.systems/api/interpreter";
    public const string Attribution = "Punkty: © współtwórcy OpenStreetMap (ODbL)";

    static string Query(XY a, XY b)
    {
        var (la1, lo1) = Geo.ToWgs84(a); var (la2, lo2) = Geo.ToWgs84(b);
        double s = Math.Min(la1, la2), n = Math.Max(la1, la2), w = Math.Min(lo1, lo2), e = Math.Max(lo1, lo2);
        return string.Format(CultureInfo.InvariantCulture, "[out:json][timeout:25];way[\"railway\"~\"^(rail|light_rail|narrow_gauge|tram)$\"]({0:0.000000},{1:0.000000},{2:0.000000},{3:0.000000});(._;>;);out skel qt;", s, w, n, e);
    }

    /// <summary>Węzły dróg kolejowych (rail, light_rail, narrow_gauge, tram) w prostokącie PUWG 1992 – w PUWG.</summary>
    public static async Task<List<XY>> Nodes(XY a, XY b, CancellationToken ct) => (await Siec(a, b, ct)).Nodes.Values.ToList();

    /// <summary>
    /// Drogi kolejowe w prostokącie PUWG 1992 – węzły i kolejność węzłów w drogach. Najpierw API OpenStreetMap (<see cref="SiecApi"/>),
    /// potem Overpass: 2026-10-28 Overpass na 7 zapytań odpowiedział raz (po 35–55 s), API – w 5 s.
    /// </summary>
    public static async Task<OsmSiec> Siec(XY a, XY b, CancellationToken ct)
    {
        try { return await SiecApi(a, b, ct); }
        catch (Exception) when (!ct.IsCancellationRequested) { }
        var q = Query(a, b);
        byte[] data;
        try { data = await Pobieranie.Retry(() => Pobieranie.Get(Overpass + "?data=" + Uri.EscapeDataString(q), ct), ct, 3); }
        catch (Exception) when (!ct.IsCancellationRequested) { data = await Pobieranie.Retry(() => Pobieranie.Get(OverpassZapas + "?data=" + Uri.EscapeDataString(q), ct), ct, 2); }
        return ParseSiec(data);
    }

    public const string OsmApi = "https://api.openstreetmap.org/api/0.6/map";
    /// <summary>Największy kawałek zapytania API [°] (ok. 1,7 × 1,7 km) – API zwraca wszystkie dane obszaru (limit 50 000 węzłów).</summary>
    const double ApiLat = 0.015, ApiLon = 0.025;

    /// <summary>
    /// Sieć z API OpenStreetMap (<c>/api/0.6/map</c>): prostokąt dzielony na kawałki, po 3 naraz; kawałek ponad limit (400) – na cztery.
    /// Z odpowiedzi tylko drogi <c>railway</c> = rail, light_rail, narrow_gauge, tram (jak w <see cref="Query"/>); API daje drogi w całości.
    /// </summary>
    public static async Task<OsmSiec> SiecApi(XY a, XY b, CancellationToken ct)
    {
        var (la1, lo1) = Geo.ToWgs84(a); var (la2, lo2) = Geo.ToWgs84(b);
        double s = Math.Min(la1, la2), n = Math.Max(la1, la2), w = Math.Min(lo1, lo2), e = Math.Max(lo1, lo2);
        int ny = Math.Max(1, (int)Math.Ceiling((n - s) / ApiLat)), nx = Math.Max(1, (int)Math.Ceiling((e - w) / ApiLon));
        var res = new OsmSiec();
        using var gate = new SemaphoreSlim(3);
        async Task Part(double s0, double w0, double n0, double e0, int depth)
        {
            byte[] data;
            await gate.WaitAsync(ct);
            try
            {
                var url = string.Format(CultureInfo.InvariantCulture, "{0}?bbox={1:0.000000},{2:0.000000},{3:0.000000},{4:0.000000}", OsmApi, w0, s0, e0, n0);
                data = await Pobieranie.Retry(() => Pobieranie.Get(url, ct), ct, 3);
            }
            catch (System.Net.Http.HttpRequestException h) when (h.StatusCode == System.Net.HttpStatusCode.BadRequest && depth < 3)
            {
                gate.Release();
                double ms = (s0 + n0) / 2, mw = (w0 + e0) / 2;
                await Task.WhenAll(Part(s0, w0, ms, mw, depth + 1), Part(s0, mw, ms, e0, depth + 1), Part(ms, w0, n0, mw, depth + 1), Part(ms, mw, n0, e0, depth + 1));
                return;
            }
            catch { gate.Release(); throw; }
            gate.Release();
            var part = ParseApi(data);
            lock (res) res.Merge(part);
        }
        var parts = new List<Task>();
        for (int i = 0; i < ny; i++) for (int j = 0; j < nx; j++)
            parts.Add(Part(s + (n - s) * i / ny, w + (e - w) * j / nx, s + (n - s) * (i + 1) / ny, w + (e - w) * (j + 1) / nx, 0));
        await Task.WhenAll(parts);
        return res;
    }

    /// <summary>Odpowiedź API OpenStreetMap (XML <c>osm</c>): drogi kolejowe i ich węzły (PUWG 1992).</summary>
    public static OsmSiec ParseApi(byte[] xml)
    {
        var res = new OsmSiec();
        var nodes = new Dictionary<long, (double Lat, double Lon)>();
        using var r = System.Xml.XmlReader.Create(new MemoryStream(xml), new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit });
        List<long> nd = null; bool rail = false;
        while (r.Read())
        {
            if (r.NodeType == System.Xml.XmlNodeType.Element)
            {
                switch (r.Name)
                {
                    case "node":
                        if (long.TryParse(r.GetAttribute("id"), out var id) && double.TryParse(r.GetAttribute("lat"), NumberStyles.Float, CultureInfo.InvariantCulture, out var la)
                            && double.TryParse(r.GetAttribute("lon"), NumberStyles.Float, CultureInfo.InvariantCulture, out var lo)) nodes[id] = (la, lo);
                        break;
                    case "way": nd = new(); rail = false; if (r.IsEmptyElement) nd = null; break;
                    case "nd": if (nd != null && long.TryParse(r.GetAttribute("ref"), out var rf)) nd.Add(rf); break;
                    case "tag":
                        if (nd != null && r.GetAttribute("k") == "railway" && r.GetAttribute("v") is "rail" or "light_rail" or "narrow_gauge" or "tram") rail = true;
                        break;
                }
            }
            else if (r.NodeType == System.Xml.XmlNodeType.EndElement && r.Name == "way")
            {
                if (nd != null && rail && nd.Count > 1 && nd.All(nodes.ContainsKey))
                {
                    res.Ways.Add(nd.ToArray());
                    foreach (var x in nd) if (!res.Nodes.ContainsKey(x)) { var (la, lo) = nodes[x]; res.Nodes[x] = Geo.ToPuwg92(la, lo); }
                }
                nd = null;
            }
        }
        return res;
    }

    /// <summary>Słupki km (<c>railway=milestone</c>) w prostokącie PUWG 1992 z numerem linii z torów OSM obok (<see cref="KilometrazOsm"/>).</summary>
    public static async Task<List<SlupekKm>> Slupki(XY a, XY b, CancellationToken ct)
    {
        var (la1, lo1) = Geo.ToWgs84(a); var (la2, lo2) = Geo.ToWgs84(b);
        var bb = string.Format(CultureInfo.InvariantCulture, "({0:0.000000},{1:0.000000},{2:0.000000},{3:0.000000})", Math.Min(la1, la2), Math.Min(lo1, lo2), Math.Max(la1, la2), Math.Max(lo1, lo2));
        var q = $"[out:json][timeout:25];(node[\"railway\"=\"milestone\"]{bb};way[\"railway\"~\"^(rail|light_rail|narrow_gauge)$\"][\"ref\"]{bb};);out geom;";
        byte[] data;
        try { data = await Pobieranie.Retry(() => Pobieranie.Get(Overpass + "?data=" + Uri.EscapeDataString(q), ct), ct, 3); }
        catch (Exception) when (!ct.IsCancellationRequested) { data = await Pobieranie.Retry(() => Pobieranie.Get(OverpassZapas + "?data=" + Uri.EscapeDataString(q), ct), ct, 2); }
        return KilometrazOsm.Parse(data);
    }

    /// <summary>
    /// Sygnalizatory, zwrotnice, wykolejnice i kozły (<c>railway=signal|switch|derail|buffer_stop</c>) w prostokącie PUWG 1992 z drogami,
    /// na których leżą (kierunek „forward”), i tory z numerem (<c>railway:track_ref</c>) – <see cref="UrzadzeniaOsm"/>.
    /// </summary>
    public static async Task<OsmUrzadzenia> Urzadzenia(XY a, XY b, CancellationToken ct)
    {
        var (la1, lo1) = Geo.ToWgs84(a); var (la2, lo2) = Geo.ToWgs84(b);
        var bb = string.Format(CultureInfo.InvariantCulture, "({0:0.000000},{1:0.000000},{2:0.000000},{3:0.000000})", Math.Min(la1, la2), Math.Min(lo1, lo2), Math.Max(la1, la2), Math.Max(lo1, lo2));
        const string rail = "[\"railway\"~\"^(rail|light_rail|narrow_gauge)$\"]";
        var q = $"[out:json][timeout:25];node[\"railway\"~\"^(signal|switch|derail|buffer_stop)$\"]{bb}->.n;.n out;(way(bn.n){rail};way{rail}[\"railway:track_ref\"]{bb};);out geom;";
        byte[] data;
        try { data = await Pobieranie.Retry(() => Pobieranie.Get(Overpass + "?data=" + Uri.EscapeDataString(q), ct), ct, 3); }
        catch (Exception) when (!ct.IsCancellationRequested) { data = await Pobieranie.Retry(() => Pobieranie.Get(OverpassZapas + "?data=" + Uri.EscapeDataString(q), ct), ct, 2); }
        return UrzadzeniaOsm.Parse(data);
    }

    /// <summary>Węzły z odpowiedzi Overpass (JSON) – w PUWG 1992.</summary>
    public static List<XY> Parse(byte[] json) => ParseSiec(json).Nodes.Values.ToList();

    /// <summary>Odpowiedź Overpass (JSON): węzły (PUWG 1992) i drogi (lista identyfikatorów węzłów).</summary>
    public static OsmSiec ParseSiec(byte[] json)
    {
        var res = new OsmSiec();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("elements", out var els)) return res;
        foreach (var el in els.EnumerateArray())
        {
            if (!el.TryGetProperty("type", out var t)) continue;
            if (t.GetString() == "node" && el.TryGetProperty("lat", out var la) && el.TryGetProperty("lon", out var lo))
                res.Nodes[el.TryGetProperty("id", out var id) ? id.GetInt64() : -res.Nodes.Count - 1] = Geo.ToPuwg92(la.GetDouble(), lo.GetDouble());
            else if (t.GetString() == "way" && el.TryGetProperty("nodes", out var nd))
                res.Ways.Add(nd.EnumerateArray().Select(x => x.GetInt64()).ToArray());
        }
        res.Ways.RemoveAll(w => w.Any(n => !res.Nodes.ContainsKey(n)));
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

/// <summary>
/// Sieć torów z OpenStreetMap: węzły (PUWG 1992) i drogi. Najbliższy punkt na linii, droga po liniach między dwoma punktami
/// (Dijkstra po węzłach, przez rozjazdy – wspólne węzły dróg). Pobrane obszary łączą się (<see cref="Merge"/>).
/// </summary>
public class OsmSiec
{
    public readonly Dictionary<long, XY> Nodes = new();
    public readonly List<long[]> Ways = new();
    /// <summary>Prostokąty już pobrane (narożniki PUWG).</summary>
    public readonly List<(XY A, XY B)> Areas = new();
    Dictionary<long, List<long>> adj;

    public void Merge(OsmSiec o)
    {
        foreach (var (id, p) in o.Nodes) Nodes[id] = p;
        var have = Ways.Select(w => string.Join(",", w)).ToHashSet();
        foreach (var w in o.Ways) if (have.Add(string.Join(",", w))) Ways.Add(w);
        Areas.AddRange(o.Areas);
        adj = null;
    }

    /// <summary>Pobrana sieć na dysku (wspólna dla projektów) – raz pobrany obszar widać bez Overpass, który bywa przeciążony
    /// (2026-10-28: na 7 prób jedna odpowiedź po 35 s, reszta – przekroczenie czasu albo 502).</summary>
    public static string CacheFile = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LcsEdytorUT", "osm", "siec.json");

    public void Save(string file)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file));
        var o = new
        {
            nodes = Nodes.Select(kv => new[] { kv.Key, kv.Value.X, kv.Value.Y }).ToList(),
            ways = Ways,
            areas = Areas.Select(r => new[] { r.A.X, r.A.Y, r.B.X, r.B.Y }).ToList(),
        };
        var tmp = file + ".tmp";
        File.WriteAllBytes(tmp, JsonSerializer.SerializeToUtf8Bytes(o));
        File.Move(tmp, file, true);
    }

    /// <summary>Sieć z pliku; null – brak pliku albo plik uszkodzony.</summary>
    public static OsmSiec Load(string file)
    {
        try
        {
            if (!File.Exists(file)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllBytes(file));
            var r = doc.RootElement; var s = new OsmSiec();
            foreach (var n in r.GetProperty("nodes").EnumerateArray()) s.Nodes[(long)n[0].GetDouble()] = new XY(n[1].GetDouble(), n[2].GetDouble());
            foreach (var w in r.GetProperty("ways").EnumerateArray()) s.Ways.Add(w.EnumerateArray().Select(x => x.GetInt64()).ToArray());
            foreach (var a in r.GetProperty("areas").EnumerateArray()) s.Areas.Add((new XY(a[0].GetDouble(), a[1].GetDouble()), new XY(a[2].GetDouble(), a[3].GetDouble())));
            s.Ways.RemoveAll(w => w.Any(n => !s.Nodes.ContainsKey(n)));
            return s;
        }
        catch (Exception) { return null; }
    }

    /// <summary>Prostokąt (dowolne narożniki) w całości w pobranych obszarach – w przybliżeniu: jego środek i narożniki.</summary>
    public bool Covers(XY a, XY b)
    {
        bool In(XY p) => Areas.Any(r => p.X >= Math.Min(r.A.X, r.B.X) - 1 && p.X <= Math.Max(r.A.X, r.B.X) + 1 && p.Y >= Math.Min(r.A.Y, r.B.Y) - 1 && p.Y <= Math.Max(r.A.Y, r.B.Y) + 1);
        return In(a) && In(b) && In(new XY(a.X, b.Y)) && In(new XY(b.X, a.Y)) && In(new XY((a.X + b.X) / 2, (a.Y + b.Y) / 2));
    }

    public IEnumerable<(XY A, XY B)> Segments()
    {
        foreach (var w in Ways) for (int i = 1; i < w.Length; i++) yield return (Nodes[w[i - 1]], Nodes[w[i]]);
    }

    Dictionary<long, List<long>> Adj()
    {
        if (adj != null) return adj;
        adj = new();
        foreach (var w in Ways)
            for (int i = 1; i < w.Length; i++)
            {
                if (w[i] == w[i - 1]) continue;
                (adj.TryGetValue(w[i - 1], out var a) ? a : adj[w[i - 1]] = new()).Add(w[i]);
                (adj.TryGetValue(w[i], out var b) ? b : adj[w[i]] = new()).Add(w[i - 1]);
            }
        return adj;
    }

    /// <summary>Najbliższy punkt na linii: punkt, węzły odcinka, odległość [m w układzie]; null – pusta sieć.</summary>
    public (XY P, long A, long B, double Dist)? Nearest(XY p)
    {
        (XY, long, long, double)? best = null; double bd = double.MaxValue;
        foreach (var w in Ways)
            for (int i = 1; i < w.Length; i++)
            {
                var a = Nodes[w[i - 1]]; var b = Nodes[w[i]]; var d = b - a; double l2 = d.X * d.X + d.Y * d.Y;
                double t = l2 > 0 ? Math.Clamp(((p.X - a.X) * d.X + (p.Y - a.Y) * d.Y) / l2, 0, 1) : 0;
                var q = a + d * t; double dist = XY.Dist(p, q);
                if (dist < bd) { bd = dist; best = (q, w[i - 1], w[i], dist); }
            }
        return best;
    }

    /// <summary>
    /// Droga po liniach z punktu <paramref name="from"/> do <paramref name="to"/> (oba – punkty na liniach, np. z <see cref="Nearest"/>):
    /// punkty (z końcami), najkrótsza droga po węzłach. null – punkty na liniach niepołączonych.
    /// </summary>
    public List<XY> Path((XY P, long A, long B) from, (XY P, long A, long B) to)
    {
        var g = Adj();
        // ten sam odcinek – wprost
        if ((from.A == to.A && from.B == to.B) || (from.A == to.B && from.B == to.A)) return new() { from.P, to.P };
        // Dijkstra od dwóch końców odcinka startowego do dwóch końców odcinka końcowego
        var dist = new Dictionary<long, double>(); var prev = new Dictionary<long, long>();
        var pq = new PriorityQueue<long, double>();
        void Start(long n) { double d = XY.Dist(from.P, Nodes[n]); if (!dist.TryGetValue(n, out var o) || d < o) { dist[n] = d; prev[n] = long.MinValue; pq.Enqueue(n, d); } }
        Start(from.A); Start(from.B);
        var done = new HashSet<long>();
        while (pq.TryDequeue(out var u, out var du))
        {
            if (!done.Add(u)) continue;
            if (du > dist[u] + 1e-9) continue;
            if (!g.TryGetValue(u, out var nb)) continue;
            foreach (var v in nb)
            {
                double nd = du + XY.Dist(Nodes[u], Nodes[v]);
                if (!dist.TryGetValue(v, out var ov) || nd < ov) { dist[v] = nd; prev[v] = u; pq.Enqueue(v, nd); }
            }
        }
        long end = long.MinValue; double best = double.MaxValue;
        foreach (var n in new[] { to.A, to.B })
            if (dist.TryGetValue(n, out var d) && d + XY.Dist(Nodes[n], to.P) < best) { best = d + XY.Dist(Nodes[n], to.P); end = n; }
        if (end == long.MinValue) return null;
        var chain = new List<XY>();
        for (long n = end; n != long.MinValue; n = prev[n]) chain.Add(Nodes[n]);
        chain.Reverse();
        var res = new List<XY> { from.P };
        foreach (var q in chain) if (XY.Dist(res[^1], q) > 1e-6) res.Add(q);
        if (XY.Dist(res[^1], to.P) > 1e-6) res.Add(to.P);
        // drugi koniec odcinka startowego / końcowego odwiedzony bez potrzeby (zawrócenie) – bez niego
        if (res.Count >= 3 && Between(res[0], res[1], res[2])) res.RemoveAt(1);
        if (res.Count >= 3 && Between(res[^3], res[^2], res[^1])) res.RemoveAt(res.Count - 2);
        return res;
    }

    // b leży za c patrząc od a (droga a → b → c zawraca na odcinku)
    static bool Between(XY a, XY b, XY c) { var u = b - a; var v = c - b; return u.X * v.X + u.Y * v.Y < 0; }
}

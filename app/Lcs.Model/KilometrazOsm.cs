using System.Text.Json;

namespace Lcs.Model;

/// <summary>Słupek kilometrowy z OpenStreetMap (<c>railway=milestone</c>): miejsce (PUWG 1992), km linii [m], linia z toru OSM obok.</summary>
public record SlupekKm(long Id, XY P, double Km, string Line, bool Szacowany);

/// <summary>Propozycja punktu km ze słupka: tor, miejsce na torze, kierunek, odległość słupka od osi toru [m w terenie], uwagi.</summary>
public record PropozycjaKm(SlupekKm Slupek, string Track, double S, int Dir, double Dist, string Uwaga);

/// <summary>
/// Kilometraż ze słupków OSM – tylko propozycje do zatwierdzenia przez projektanta (nic nie wstawia się samo). Słupek nie ma w OSM
/// numeru linii: bierze go z najbliższego toru OSM z <c>ref</c> (numer linii PLK). Słupki jednej linii tworzą jedną oś kilometracji –
/// tor projektu, przy którym stoi najwięcej słupków tej linii; inna linia – osobna oś. Słupki z <c>source=interpolation</c> mają
/// położenie szacowane przez autorów mapy (nie z pomiaru).
/// </summary>
public static class KilometrazOsm
{
    /// <summary>Najdalej od osi toru projektu, przy której słupek trafia na ten tor [m w terenie].</summary>
    public const double MaxDoToru = 25;
    /// <summary>Najdalej od toru OSM z numerem linii, przy której słupek bierze ten numer [m].</summary>
    public const double MaxDoLinii = 60;

    /// <summary>Słupki km z odpowiedzi Overpass (węzły <c>railway=milestone</c> i drogi z <c>ref</c>, <c>out geom</c>).</summary>
    public static List<SlupekKm> Parse(byte[] json)
    {
        var res = new List<SlupekKm>(); var ways = new List<(string Ref, List<XY> Pts)>();
        var nodes = new List<(long Id, XY P, double Km, bool Szac)>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("elements", out var els)) return res;
        foreach (var el in els.EnumerateArray())
        {
            if (!el.TryGetProperty("tags", out var tags)) continue;
            string T(string k) => tags.TryGetProperty(k, out var v) ? v.GetString() ?? "" : "";
            string type = el.TryGetProperty("type", out var t) ? t.GetString() : "";
            if (type == "node" && T("railway") == "milestone" && el.TryGetProperty("lat", out var la) && el.TryGetProperty("lon", out var lo))
            {
                var km = Kilometraz.Parse(T("railway:position:exact")) ?? Kilometraz.Parse(T("railway:position"));
                if (km is double v) nodes.Add((el.GetProperty("id").GetInt64(), Geo.ToPuwg92(la.GetDouble(), lo.GetDouble()), v, T("source") == "interpolation"));
            }
            else if (type == "way" && T("ref").Length > 0 && el.TryGetProperty("geometry", out var g))
                ways.Add((T("ref"), g.EnumerateArray().Select(q => Geo.ToPuwg92(q.GetProperty("lat").GetDouble(), q.GetProperty("lon").GetDouble())).ToList()));
        }
        foreach (var (id, p, km, szac) in nodes)
        {
            string line = ""; double best = MaxDoLinii;
            foreach (var (r, pts) in ways)
                for (int i = 1; i < pts.Count; i++)
                    if (DoOdcinka(p, pts[i - 1], pts[i]) is var d && d < best) { best = d; line = r.Split(';')[0].Trim(); }
            res.Add(new SlupekKm(id, p, km, line, szac));
        }
        return res;
    }

    static double DoOdcinka(XY p, XY a, XY b)
    {
        var d = b - a; double l2 = d.X * d.X + d.Y * d.Y;
        double t = l2 > 0 ? Math.Clamp(((p.X - a.X) * d.X + (p.Y - a.Y) * d.Y) / l2, 0, 1) : 0;
        return XY.Dist(p, a + d * t);
    }

    /// <summary>
    /// Propozycje punktów km: słupki przy torach projektu (do <see cref="MaxDoToru"/> m), po jednej osi na linię. Pomija słupki,
    /// których km tej linii już jest w projekcie (do 1 m). Kierunek: z sąsiednich słupków tej linii (także spoza projektu).
    /// </summary>
    public static List<PropozycjaKm> Propozycje(UtProject p, IReadOnlyList<SlupekKm> slupki, Dictionary<string, TrasaResult> geom = null)
    {
        var res = new List<PropozycjaKm>();
        TrasaResult G(UtTrack t) => geom != null && geom.TryGetValue(t.Id, out var r) ? r : Trasa.Compute(t);
        var tory = p.Tracks.Select(t => (t, r: G(t))).Where(x => x.r.Elements.Count > 0).ToList();
        foreach (var linia in slupki.GroupBy(s => s.Line))
        {
            // kandydaci: tory projektu blisko słupka (rzut wewnątrz toru)
            var kand = new Dictionary<SlupekKm, List<(UtTrack T, TrasaResult R, double S, double D)>>();
            foreach (var s in linia)
            {
                if (p.KmPoints.Any(k => k.Line == s.Line && Math.Abs(k.Km - s.Km) < 1)) continue;
                var l = new List<(UtTrack, TrasaResult, double, double)>();
                foreach (var (t, r) in tory)
                {
                    var (sk, d) = r.Nearest(s.P, 1); double dt = d / r.K;
                    if (dt <= MaxDoToru && sk > 0.01 && sk < r.Length - 0.01) l.Add((t, r, sk, dt));
                }
                if (l.Count > 0) kand[s] = l;
            }
            // oś linii: tor z największą liczbą słupków tej linii (remis – bliżej słupków)
            var waga = kand.SelectMany(x => x.Value).GroupBy(x => x.T.Id).ToDictionary(g => g.Key, g => (N: g.Count(), D: g.Sum(x => x.D)));
            var wybor = kand.Select(x => (s: x.Key, c: x.Value.OrderByDescending(q => waga[q.T.Id].N).ThenBy(q => waga[q.T.Id].D).ThenBy(q => q.D).First())).ToList();
            foreach (var os in wybor.GroupBy(x => x.c.T.Id))
            {
                // kierunek z sąsiedniego słupka na tej samej osi – pewniejszy niż ze słupka spoza projektu
                var l = os.OrderBy(x => x.c.S).ToList();
                for (int i = 0; i < l.Count; i++)
                {
                    var (s, (t, r, sk, d)) = l[i];
                    int dir = 0;
                    if (l.Count >= 2) { var (a, b) = i + 1 < l.Count ? (l[i], l[i + 1]) : (l[i - 1], l[i]); dir = Math.Sign(b.s.Km - a.s.Km); }
                    if (dir == 0) dir = Kierunek(s, slupki, r.At(sk).Az);
                    var uw = new List<string>();
                    if (s.Szacowany) uw.Add("położenie szacowane w OSM");
                    if (s.Line.Length == 0) uw.Add("brak numeru linii w OSM – wpisz");
                    if (dir == 0) { dir = 1; uw.Add("kierunek do sprawdzenia"); }
                    res.Add(new PropozycjaKm(s, t.Id, sk, dir, d, string.Join(", ", uw)));
                }
            }
        }
        return res.OrderBy(x => x.Slupek.Line).ThenBy(x => x.Slupek.Km).ToList();
    }

    /// <summary>Kierunek wzrostu km linii względem toru (azymut w miejscu słupka) z najbliższego innego słupka tej linii; 0 – brak.</summary>
    static int Kierunek(SlupekKm s, IReadOnlyList<SlupekKm> all, double az)
    {
        var o = all.Where(x => x.Line == s.Line && x.Id != s.Id && Math.Abs(x.Km - s.Km) > 1 && XY.Dist(x.P, s.P) < 5000)
                   .OrderBy(x => XY.Dist(x.P, s.P)).FirstOrDefault();
        if (o == null) return 0;
        var v = o.P - s.P; var u = Element.Dir(az);
        double dot = v.X * u.X + v.Y * u.Y;
        return Math.Abs(dot) < 1e-6 ? 0 : Math.Sign(dot) * Math.Sign(o.Km - s.Km);
    }

    /// <summary>Punkt km z zatwierdzonej propozycji (numer linii można zmienić przed dodaniem).</summary>
    public static UtKmPoint Punkt(PropozycjaKm x, string line = null) =>
        new() { Line = line ?? x.Slupek.Line, Track = x.Track, S = x.S, Km = x.Slupek.Km, Dir = x.Dir };
}

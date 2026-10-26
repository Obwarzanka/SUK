using System.Globalization;

namespace Lcs.Model;

/// <summary>
/// Peron (Ig-10 zał. 8 s. 52, zał. 5 „Wykaz peronów”): krawędź wzdłuż toru <see cref="Track"/> od km <see cref="S0"/> do
/// <see cref="S1"/> [m w terenie od początku toru] po stronie <see cref="Side"/> (+1 – po prawej patrząc w stronę rosnącego km toru),
/// w odległości <see cref="Offset"/> od osi toru. Peron dwukrawędziowy (wyspowy) ma drugą krawędź przy torze <see cref="Track2"/>
/// (po stronie zwróconej do pierwszego toru) – wtedy szerokość wynika z międzytorza.
/// </summary>
public class UtPlatform
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    /// <summary>Numer peronu (na planie „per. 1”).</summary>
    public string Name { get; set; } = "";
    public string Track { get; set; }
    public double S0 { get; set; }
    public double S1 { get; set; }
    public int Side { get; set; } = 1;
    /// <summary>Odległość krawędzi peronu od osi toru [m].</summary>
    public double Offset { get; set; } = 1.725;
    /// <summary>Szerokość peronu jednokrawędziowego [m].</summary>
    public double Width { get; set; } = 4;
    /// <summary>Wysokość krawędzi nad główką szyny [m].</summary>
    public double Height { get; set; } = 0.76;
    /// <summary>Druga krawędź (peron wyspowy) – tor; null – peron jednokrawędziowy.</summary>
    public string Track2 { get; set; }
    public double Len => Math.Abs(S1 - S0);
}

/// <summary>
/// Przejazd kolejowo-drogowy albo przejście w poziomie szyn (Ig-10 § 4 ust. 1 pkt 13, zał. 8 s. 41, 53, 57, 60, 64): oś drogi
/// przecina tor <see cref="Track"/> w km <see cref="S"/> pod kątem <see cref="Angle"/> [°] do osi toru (90 – prostopadle);
/// droga szerokości <see cref="Width"/> sięga <see cref="Reach"/> [m] w obie strony od osi toru (przez sąsiednie tory).
/// </summary>
public class UtLevelCrossing
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    /// <summary>Opis, np. nazwa drogi (ul. Puszkina).</summary>
    public string Name { get; set; } = "";
    public string Track { get; set; }
    public double S { get; set; }
    public double Angle { get; set; } = 90;
    /// <summary>Kategoria A–F (przejście dla pieszych – „P”).</summary>
    public string Cat { get; set; } = "D";
    public double Width { get; set; } = 6;
    public double Reach { get; set; } = 15;
    /// <summary>Rogatki: brak, z napędem mechanicznym, z napędem elektrycznym (Ig-10 s. 53).</summary>
    public string Barriers { get; set; } = "";

    public const string RogatkiMech = "mech", RogatkiElektr = "elektr";
    public static readonly string[] Cats = { "A", "B", "C", "D", "E", "F", "P" };
    public static string CatName(string c) => c == "P" ? "przejście w poziomie szyn" : $"przejazd kat. {c}";
}

/// <summary>
/// Punkt kilometrażu linii kolejowej na torze: w km <see cref="S"/> toru [m od początku toru] kilometraż linii <see cref="Line"/>
/// wynosi <see cref="Km"/> [m]; <see cref="Dir"/> +1 – kilometraż linii rośnie w stronę rosnącego km toru. Między dwoma punktami
/// na torze kilometraż liczy się liniowo (kilometr skrócony / wydłużony), za skrajnym – po osi toru.
/// </summary>
public class UtKmPoint
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    /// <summary>Numer linii kolejowej (np. „17”).</summary>
    public string Line { get; set; } = "";
    public string Track { get; set; }
    public double S { get; set; }
    /// <summary>Kilometraż linii [m], np. 25452 = km 25,452.</summary>
    public double Km { get; set; }
    public int Dir { get; set; } = 1;
}

/// <summary>Kilometraż linii: km dowolnego miejsca toru (po osi), rzut na tor z kilometrażem, zapis „25.452”.</summary>
public static class Kilometraz
{
    /// <summary>Zapis km jak na planach Ig-10: km z trzema miejscami po kropce („25.452”).</summary>
    public static string Fmt(double m) => (m / 1000).ToString("0.000", CultureInfo.InvariantCulture);

    /// <summary>Odczyt km: „25.452”, „25,452”, „25+452” → metry.</summary>
    public static double? Parse(string s)
    {
        s = (s ?? "").Trim().Replace(',', '.');
        if (s.Contains('+')) { var p = s.Split('+'); if (double.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var k) && double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var m)) return k * 1000 + Math.Sign(k == 0 ? 1 : k) * m; return null; }
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v * 1000 : null;
    }

    /// <summary>Linie z punktami km na torze.</summary>
    public static IEnumerable<string> Lines(UtProject p, string track) => p.KmPoints.Where(k => k.Track == track).Select(k => k.Line).Distinct();

    /// <summary>Kilometraż linii w km <paramref name="s"/> toru, który ma punkty km tej linii (null – brak).</summary>
    public static double? OnTrack(UtProject p, string track, string line, double s)
    {
        var pts = p.KmPoints.Where(k => k.Track == track && k.Line == line).OrderBy(k => k.S).ToList();
        if (pts.Count == 0) return null;
        if (s <= pts[0].S) return pts[0].Km + pts[0].Dir * (s - pts[0].S);
        for (int i = 0; i + 1 < pts.Count; i++)
            if (s <= pts[i + 1].S)
            {
                double t = (s - pts[i].S) / Math.Max(1e-9, pts[i + 1].S - pts[i].S);
                return pts[i].Km + t * (pts[i + 1].Km - pts[i].Km);
            }
        return pts[^1].Km + pts[^1].Dir * (s - pts[^1].S);
    }

    /// <summary>Największa odległość punktu od toru z kilometrażem przy rzucie [m].</summary>
    public const double MaxProj = 150;

    /// <summary>
    /// Kilometraż miejsca: na torze z punktami km – po jego osi, inaczej rzut prostopadły na najbliższy tor z kilometrażem
    /// (do <see cref="MaxProj"/> m). Wszystkie linie, posortowane: tor własny, potem odległość.
    /// </summary>
    public static List<(string Line, double Km, string RefTrack, double Dist)> At(UtProject p, XY pt, string track = null, double s = double.NaN, Dictionary<string, TrasaResult> r = null)
    {
        var res = new List<(string, double, string, double)>();
        if (track != null && !double.IsNaN(s))
            foreach (var l in Lines(p, track)) if (OnTrack(p, track, l, s) is double km) res.Add((l, km, track, 0));
        foreach (var tid in p.KmPoints.Select(k => k.Track).Distinct())
        {
            if (tid == track) continue;
            var t = p.Tracks.FirstOrDefault(x => x.Id == tid); if (t == null) continue;
            var rt = r != null && r.TryGetValue(tid, out var rr) ? rr : Trasa.Compute(t); if (rt.Elements.Count == 0) continue;
            var (sk, dist) = rt.Nearest(pt, 1);
            if (dist > MaxProj) continue;
            foreach (var l in Lines(p, tid))
                if (res.All(x => x.Item1 != l) && OnTrack(p, tid, l, sk) is double km) res.Add((l, km, tid, dist));
        }
        return res.OrderBy(x => x.Item4).ToList();
    }

    /// <summary>Kilometraż miejsca na torze – pierwsza linia (tekst „km 25.452 (l. 17)”) albo pusty.</summary>
    public static string Text(UtProject p, string track, double s, Dictionary<string, TrasaResult> r = null)
    {
        var t = p.Tracks.FirstOrDefault(x => x.Id == track); if (t == null) return "";
        var rt = r != null && r.TryGetValue(track, out var rr) ? rr : Trasa.Compute(t); if (rt.Elements.Count == 0) return "";
        var a = At(p, rt.At(Math.Clamp(s, 0, rt.Length)).P, track, s, r);
        return a.Count == 0 ? "" : string.Join(", ", a.Select(x => $"km {Fmt(x.Km)}" + (a.Count > 1 || x.Line.Length > 0 ? $" (l. {x.Line})" : "")));
    }

    /// <summary>Uwagi: kilometraż nie rośnie zgodnie z kierunkiem wskazanym w punkcie, dwa punkty w tym samym miejscu.</summary>
    public static List<(string Text, string Track, double S)> Check(UtProject p)
    {
        var res = new List<(string, string, double)>();
        foreach (var g in p.KmPoints.GroupBy(k => (k.Track, k.Line)))
        {
            var pts = g.OrderBy(k => k.S).ToList();
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                double ds = pts[i + 1].S - pts[i].S, dk = pts[i + 1].Km - pts[i].Km;
                if (ds < 0.01) { res.Add(($"dwa punkty kilometrażu linii {g.Key.Line} w tym samym miejscu toru", g.Key.Track, pts[i].S)); continue; }
                if (Math.Sign(dk) != pts[i].Dir || Math.Sign(dk) != pts[i + 1].Dir)
                    res.Add(($"kilometraż linii {g.Key.Line}: km {Fmt(pts[i].Km)} → {Fmt(pts[i + 1].Km)} nie rośnie w kierunku wskazanym w punktach", g.Key.Track, pts[i].S));
                else if (Math.Abs(Math.Abs(dk) - ds) > Math.Max(5, 0.02 * ds))
                    res.Add(($"kilometraż linii {g.Key.Line}: między km {Fmt(pts[i].Km)} a {Fmt(pts[i + 1].Km)} jest {ds:0.##} m toru (kilometr {(Math.Abs(dk) > ds ? "skrócony" : "wydłużony")} o {Math.Abs(Math.Abs(dk) - ds):0.##} m) – sprawdź", g.Key.Track, pts[i].S));
            }
        }
        return res;
    }
}

/// <summary>Wierzchołek niwelety (załom profilu): km na torze [m], wysokość główki szyny [m n.p.m.], promień łuku pionowego [m] (0 – bez).</summary>
public class UtProfilePoint
{
    public double S { get; set; }
    public double Z { get; set; }
    public double Rv { get; set; }
}

/// <summary>
/// Profil podłużny toru (Z): niweleta – łamana załomów z łukami pionowymi (parabola, styczna T = Rv·|Δi| / 2), pochylenia ‰,
/// teren z NMT GUGiK (<see cref="UtTrack.Terrain"/>), nasyp / przekop. Uproszczenie terenu do odcinków stałego pochylenia –
/// Douglas–Peucker z najkrótszym odcinkiem (przeniesione z dawnego <c>Model/Profil.cs</c>).
/// </summary>
public static class Profil
{
    public record Odc(double S0, double S1, double Z0, double Z1)
    {
        public double Len => S1 - S0;
        /// <summary>Pochylenie [‰], dodatnie – wzniesienie w stronę rosnącego km toru.</summary>
        public double Promil => Len > 1e-9 ? (Z1 - Z0) / Len * 1000 : 0;
    }

    public static List<Odc> Sections(IReadOnlyList<UtProfilePoint> v) =>
        Enumerable.Range(0, Math.Max(0, v.Count - 1)).Select(i => new Odc(v[i].S, v[i + 1].S, v[i].Z, v[i + 1].Z)).ToList();

    /// <summary>Wysokość niwelety w km s (łuki pionowe w załomach; poza zakresem – pochylenie skrajnego odcinka); NaN – brak profilu.</summary>
    public static double Z(IReadOnlyList<UtProfilePoint> v, double s)
    {
        if (v == null || v.Count == 0) return double.NaN;
        if (v.Count == 1) return v[0].Z;
        int n = v.Count;
        double G(int i) => (v[i + 1].Z - v[i].Z) / Math.Max(1e-9, v[i + 1].S - v[i].S);
        // łuk pionowy w załomie i (wewnętrznym), jeśli s leży w jego zasięgu
        for (int i = 1; i < n - 1; i++)
        {
            if (v[i].Rv <= 0) continue;
            double g1 = G(i - 1), g2 = G(i), T = v[i].Rv * Math.Abs(g2 - g1) / 2;
            if (s >= v[i].S - T && s <= v[i].S + T)
            {
                double x = s - (v[i].S - T);
                return v[i].Z - g1 * T + g1 * x + Math.Sign(g2 - g1) * x * x / (2 * v[i].Rv);
            }
        }
        int k = 0; while (k < n - 2 && s > v[k + 1].S) k++;
        return v[k].Z + G(k) * (s - v[k].S);
    }

    /// <summary>Styczna łuku pionowego w załomie i [m] (0 – bez łuku).</summary>
    public static double Tangent(IReadOnlyList<UtProfilePoint> v, int i)
    {
        if (i <= 0 || i >= v.Count - 1 || v[i].Rv <= 0) return 0;
        double g1 = (v[i].Z - v[i - 1].Z) / Math.Max(1e-9, v[i].S - v[i - 1].S), g2 = (v[i + 1].Z - v[i].Z) / Math.Max(1e-9, v[i + 1].S - v[i].S);
        return v[i].Rv * Math.Abs(g2 - g1) / 2;
    }

    /// <summary>Wysokość terenu w km s (interpolacja liniowa próbek NMT); NaN – brak.</summary>
    public static double Ground(IReadOnlyList<UtProfilePoint> terrain, double s)
    {
        if (terrain == null || terrain.Count == 0) return double.NaN;
        if (s <= terrain[0].S) return terrain[0].Z;
        for (int i = 0; i + 1 < terrain.Count; i++)
            if (s <= terrain[i + 1].S) { double t = (s - terrain[i].S) / Math.Max(1e-9, terrain[i + 1].S - terrain[i].S); return terrain[i].Z + t * (terrain[i + 1].Z - terrain[i].Z); }
        return terrain[^1].Z;
    }

    /// <summary>Uproszczenie łamanej (Douglas–Peucker, tolerancja w metrach wysokości).</summary>
    public static List<(double S, double H)> Simplify(IReadOnlyList<(double S, double H)> p, double tol)
    {
        if (p.Count <= 2) return p.ToList();
        var keep = new bool[p.Count]; keep[0] = keep[^1] = true;
        var st = new Stack<(int A, int B)>(); st.Push((0, p.Count - 1));
        while (st.Count > 0)
        {
            var (a, b) = st.Pop(); double best = tol; int bi = -1;
            for (int i = a + 1; i < b; i++)
            {
                double t = (p[i].S - p[a].S) / Math.Max(1e-9, p[b].S - p[a].S), h = p[a].H + t * (p[b].H - p[a].H), d = Math.Abs(p[i].H - h);
                if (d > best) { best = d; bi = i; }
            }
            if (bi >= 0) { keep[bi] = true; st.Push((a, bi)); st.Push((bi, b)); }
        }
        return p.Where((_, i) => keep[i]).ToList();
    }

    /// <summary>Uproszczenie z najkrótszym odcinkiem [m]: odcinki krótsze niż minLen scalane z sąsiednim (usuwany załom, którego usunięcie najmniej oddala profil od terenu).</summary>
    public static List<(double S, double H)> Simplify(IReadOnlyList<(double S, double H)> p, double tol, double minLen)
    {
        var v = Simplify(p, tol);
        if (minLen <= 0 || v.Count <= 2) return v;
        var idx = v.Select(q => IndexOf(p, q.S)).ToList();
        double Err(int a, int b) { double e = 0; for (int i = a + 1; i < b; i++) { double t = (p[i].S - p[a].S) / Math.Max(1e-9, p[b].S - p[a].S); e = Math.Max(e, Math.Abs(p[i].H - (p[a].H + t * (p[b].H - p[a].H)))); } return e; }
        while (idx.Count > 2)
        {
            int k = -1; double shortest = minLen;
            for (int i = 0; i < idx.Count - 1; i++) { double l = p[idx[i + 1]].S - p[idx[i]].S; if (l < shortest) { shortest = l; k = i; } }
            if (k < 0) break;
            int cand = -1; double best = double.MaxValue;
            foreach (var j in new[] { k, k + 1 })
            {
                if (j == 0 || j == idx.Count - 1) continue;
                var e = Err(idx[j - 1], idx[j + 1]); if (e < best) { best = e; cand = j; }
            }
            if (cand < 0) break;
            idx.RemoveAt(cand);
        }
        return idx.Select(i => p[i]).ToList();
    }
    static int IndexOf(IReadOnlyList<(double S, double H)> p, double s) { for (int i = 0; i < p.Count; i++) if (p[i].S == s) return i; return 0; }

    /// <summary>Punkty toru co <paramref name="step"/> [m w terenie] z końcem: km i położenie.</summary>
    public static List<(double S, XY P)> Sample(TrasaResult r, double step)
    {
        var res = new List<(double, XY)>();
        if (r.Elements.Count == 0) return res;
        for (double s = 0; s < r.Length - 1e-6; s += step) res.Add((s, r.At(s).P));
        res.Add((r.Length, r.At(r.Length).P));
        return res;
    }

    /// <summary>
    /// Niweleta z terenu: uproszczenie próbek (tolerancja [m], najkrótszy odcinek [m]), wysokości zaokrąglone do 1 cm. NaN w próbkach
    /// (brak danych NMT) – pominięte.
    /// </summary>
    public static List<UtProfilePoint> FromTerrain(IReadOnlyList<UtProfilePoint> terrain, double tol = 0.3, double minLen = 100) => FromTerrain(terrain, tol, minLen, null);

    /// <summary>
    /// Niweleta z terenu z pominięciem próbek w zakresach <paramref name="bez"/> (most, wiadukt, tunel – NMT pokazuje tam dolinę albo
    /// wzgórze, § 5m): niweleta przechodzi przez obiekt prosto między terenem przed nim i za nim.
    /// </summary>
    public static List<UtProfilePoint> FromTerrain(IReadOnlyList<UtProfilePoint> terrain, double tol, double minLen, IReadOnlyList<(double S0, double S1)> bez)
    {
        bool Out(double s) => bez == null || !bez.Any(b => s > b.S0 && s < b.S1);
        var pts = terrain.Where(q => !double.IsNaN(q.Z) && Out(q.S)).Select(q => (q.S, q.Z)).ToList();
        if (pts.Count == 0) return new();
        return Simplify(pts, tol, minLen).Select(q => new UtProfilePoint { S = Math.Round(q.S, 2), Z = Math.Round(q.H, 2) }).ToList();
    }

    /// <summary>Uwagi o profilu: załomy nie po kolei, łuki pionowe nachodzą na siebie, różnica wysokości na styku torów.</summary>
    public static List<(string Text, string Track, double S)> Check(UtProject p, List<Styk> styki = null, Dictionary<string, TrasaResult> r = null)
    {
        var res = new List<(string, string, double)>();
        foreach (var t in p.Tracks)
        {
            var v = t.Profile; if (v == null || v.Count == 0) continue;
            for (int i = 0; i + 1 < v.Count; i++)
                if (v[i + 1].S <= v[i].S) res.Add(($"profil toru {t.Number}: załomy nie po kolei (km {v[i].S:0.#} i {v[i + 1].S:0.#})", t.Id, v[i].S));
            for (int i = 1; i + 2 < v.Count; i++)
                if (v[i].S + Tangent(v, i) > v[i + 1].S - Tangent(v, i + 1) + 1e-6) res.Add(($"profil toru {t.Number}: łuki pionowe w km {v[i].S:0.#} i {v[i + 1].S:0.#} nachodzą na siebie – zmniejsz Rv", t.Id, v[i].S));
        }
        if (p.Tracks.Count(t => t.Profile is { Count: > 0 }) < 2) return res;
        r ??= Topologia.Routes(p); styki ??= Topologia.Styki(p, r);
        foreach (var s in styki)
        {
            var a = p.Tracks.First(x => x.Id == s.Track); var b = p.Tracks.First(x => x.Id == s.Other);
            if (a.Profile is not { Count: > 0 } || b.Profile is not { Count: > 0 }) continue;
            double za = Z(a.Profile, s.AtStart ? 0 : r[a.Id].Length), zb = Z(b.Profile, s.OtherS);
            if (Math.Abs(za - zb) > 0.02) res.Add(($"styk torów {a.Number} / {b.Number}: niwelety różnią się o {Math.Abs(za - zb):0.00} m", a.Id, s.AtStart ? 0 : r[a.Id].Length));
        }
        return res;
    }
}

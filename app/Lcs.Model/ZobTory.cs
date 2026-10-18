namespace Lcs.Model;

/// <summary>
/// Tory obrazu (PLAN_EDYTOR_ZOBRAZOWANIA.md § 2, § 4, etap Z2): pokrycie kilometrażu toru UT kawałkami obrazu, kierunki co 45°,
/// łącznik „ukos 45° – pion – ukos 45°”, wybór toru UT łącznika. Geometria w jednostkach obrazu (całkowite, krok siatki).
/// </summary>
public static class ZobTory
{
    /// <summary>Kawałki toru UT na obrazie: przedziały km (od ≤ do), posortowane.</summary>
    public static List<(double A, double B)> Pokrycie(ZobObraz o, string utTrack, string exceptId = null) =>
        o.Tory.Where(t => t.Ut?.Tor == utTrack && t.Id != exceptId)
            .Select(t => (Math.Min(t.Ut.S0, t.Ut.S1), Math.Max(t.Ut.S0, t.Ut.S1))).OrderBy(x => x.Item1).ToList();

    /// <summary>Przedziały toru UT [0, len] jeszcze nie narysowane na obrazie (krótsze niż 0,5 m pomijane).</summary>
    public static List<(double A, double B)> Luki(ZobObraz o, string utTrack, double len, string exceptId = null)
    {
        var res = new List<(double, double)>(); double s = 0;
        foreach (var (a, b) in Pokrycie(o, utTrack, exceptId))
        {
            if (a - s > 0.5) res.Add((s, a));
            s = Math.Max(s, b);
        }
        if (len - s > 0.5) res.Add((s, len));
        return res;
    }

    /// <summary>Kawałek zachodzi na inny kawałek tego samego toru UT na obrazie (ten sam km dwa razy – § 3).</summary>
    public static bool Zachodzi(ZobObraz o, string utTrack, double s0, double s1, string exceptId = null)
    {
        double a = Math.Min(s0, s1), b = Math.Max(s0, s1);
        return Pokrycie(o, utTrack, exceptId).Any(x => Math.Min(b, x.B) - Math.Max(a, x.A) > 0.5);
    }

    public static int Grid(double v, int g) => (int)Math.Round(v / g) * g;

    /// <summary>
    /// Następny punkt łamanej od <paramref name="prev"/> (jak w poprzednim edytorze i w planie § 4): punkt siatki, z Shift – poziomo,
    /// z Ctrl – kierunek co 45° (poziomo, pionowo albo ukosem 1:1 – § 1 pkt 5).
    /// </summary>
    public static int[] Kierunek(int[] prev, double x, double y, int g, bool poziomo, bool ukos45 = true)
    {
        double dx = x - prev[0], dy = y - prev[1];
        if (poziomo) return new[] { Grid(x, g), prev[1] };
        if (!ukos45) return new[] { Grid(x, g), Grid(y, g) };
        double ang = Math.Atan2(dy, dx);
        int k = (int)Math.Round(ang / (Math.PI / 4));
        k = ((k % 8) + 8) % 8;
        int sx = k is 0 or 1 or 7 ? 1 : k is 3 or 4 or 5 ? -1 : 0, sy = k is 1 or 2 or 3 ? 1 : k is 5 or 6 or 7 ? -1 : 0;
        if (sy == 0) return new[] { Grid(x, g), prev[1] };
        if (sx == 0) return new[] { prev[0], Grid(y, g) };
        int d = Grid((Math.Abs(dx) + Math.Abs(dy)) / 2, g);
        return new[] { prev[0] + sx * d, prev[1] + sy * d };
    }

    /// <summary>Odcinek co 45° (poziomy, pionowy albo ukośny 1:1).</summary>
    public static bool Osiowy(int[] a, int[] b)
    {
        int dx = Math.Abs(b[0] - a[0]), dy = Math.Abs(b[1] - a[1]);
        return dx == 0 || dy == 0 || dx == dy;
    }

    /// <summary>
    /// Łącznik między torem obrazu w punkcie <paramref name="p"/> a torem na wysokości <paramref name="yB"/>, w stronę <paramref name="xB"/>
    /// (w lewo / w prawo): ukos 45° na jeden krok siatki – pion – ukos 45° na jeden krok, jak na pulpitach LCS (wzory SimRail:
    /// Łódź Widzew, Bełchów, Grodzisk Maz., Koluszki – kształt „[”). Koniec na torze B, 2 kroki siatki obok początku.
    /// null – tory bliżej niż 2 kroki siatki.
    /// </summary>
    public static List<int[]> Lacznik(int[] p, int xB, int yB, int g)
    {
        int dy = yB - p[1];
        if (Math.Abs(dy) < 2 * g) return null;
        int sx = xB >= p[0] ? 1 : -1, sy = Math.Sign(dy);
        var a = new[] { p[0] + sx * g, p[1] + sy * g };
        var b = new[] { a[0], yB - sy * g };
        var q = new[] { p[0] + sx * 2 * g, yB };
        var res = new List<int[]> { p, a };
        if (b[1] != a[1]) res.Add(b);
        res.Add(q);
        return res;
    }

    /// <summary>Najbliższy punkt łamanej: odcinek, punkt rzutu, odległość, ułamek długości łamanej (0 – pierwszy punkt).</summary>
    public static (int Seg, double X, double Y, double Dist, double Frac) Rzut(List<int[]> pts, double x, double y)
    {
        double total = 0; for (int i = 1; i < pts.Count; i++) total += Len(pts[i - 1], pts[i]);
        (int, double, double, double, double) best = (-1, 0, 0, double.MaxValue, 0);
        double acc = 0;
        for (int i = 1; i < pts.Count; i++)
        {
            double ax = pts[i - 1][0], ay = pts[i - 1][1], bx = pts[i][0], by = pts[i][1];
            double vx = bx - ax, vy = by - ay, l2 = vx * vx + vy * vy;
            double t = l2 == 0 ? 0 : Math.Clamp(((x - ax) * vx + (y - ay) * vy) / l2, 0, 1);
            double px = ax + t * vx, py = ay + t * vy, dd = Math.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
            double seg = Math.Sqrt(l2);
            if (dd < best.Item4) best = (i - 1, px, py, dd, total > 0 ? (acc + t * seg) / total : 0);
            acc += seg;
        }
        return best;
    }

    static double Len(int[] a, int[] b) => Math.Sqrt((double)(b[0] - a[0]) * (b[0] - a[0]) + (double)(b[1] - a[1]) * (b[1] - a[1]));

    /// <summary>Punkt siatki na odcinku najbliższy (x, y) – odcinki co 45° mają punkty siatki co krok.</summary>
    public static int[] NaOdcinku(int[] a, int[] b, double x, double y, int g)
    {
        int dx = Math.Sign(b[0] - a[0]), dy = Math.Sign(b[1] - a[1]);
        int n = Math.Max(Math.Abs(b[0] - a[0]), Math.Abs(b[1] - a[1])) / g;
        int[] best = a; double bd = double.MaxValue;
        for (int i = 0; i <= n; i++)
        {
            var c = new[] { a[0] + dx * i * g, a[1] + dy * i * g };
            double d = (c[0] - x) * (c[0] - x) + (c[1] - y) * (c[1] - y);
            if (d < bd) { bd = d; best = c; }
        }
        return best;
    }

    /// <summary>
    /// Tory UT łącznika między torami UT <paramref name="a"/> i <paramref name="b"/>: tor odgałęźny, którego rozjazdy leżą na obu torach
    /// (rozjazd na początku i na końcu), z km rozjazdu na torze A.
    /// </summary>
    public static List<(string Track, double KmA, bool StartAtA)> Laczniki(UtProject ut, string a, string b)
    {
        var res = new List<(string, double, bool)>();
        foreach (var t in ut.Tracks)
        {
            var zs = ut.Turnouts.Where(z => z.Branch == t.Id).ToList();
            var za = zs.FirstOrDefault(z => z.Track == a); var zb = zs.FirstOrDefault(z => z.Track == b);
            if (za == null || zb == null) continue;
            res.Add((t.Id, za.S, !za.BranchAtEnd));
        }
        return res;
    }

    /// <summary>
    /// Którym końcem tor UT <paramref name="track"/> styka się z torem UT <paramref name="other"/>: true – początkiem (km 0),
    /// false – końcem, null – nie wiadomo.
    /// </summary>
    public static bool? KoniecPrzy(UtProject ut, string track, string other, List<Styk> styki)
    {
        foreach (var s in styki)
            if (s.Track == track && s.Other == other) return s.AtStart;
        double len = ut.Tracks.FirstOrDefault(t => t.Id == track) is UtTrack tt ? Trasa.Compute(tt).Length : 0;
        foreach (var s in styki)
            if (s.Track == other && s.Other == track && s.OtherEnd) return s.OtherS < len / 2;
        // tor odgałęźny rozjazdu na torze other: początek przy rozjeździe (koniec – gdy rozjazd ustawia koniec odgałęzienia)
        foreach (var z in ut.Turnouts.Where(z => z.Branch == track && z.Track == other)) return !z.BranchAtEnd;
        return null;
    }

    /// <summary>Tory UT połączone z torem <paramref name="track"/> (styki końców i odgałęzienia rozjazdów).</summary>
    public static HashSet<string> Sasiednie(UtProject ut, string track, List<Styk> styki)
    {
        var res = new HashSet<string>();
        foreach (var s in styki)
        {
            if (s.Track == track) res.Add(s.Other);
            if (s.Other == track) res.Add(s.Track);
        }
        foreach (var z in ut.Turnouts)
        {
            if (z.Track == track && z.Branch != null) res.Add(z.Branch);
            if (z.Branch == track) res.Add(z.Track);
        }
        res.Remove(track);
        return res;
    }
}

namespace Lcs.Model;

/// <summary>
/// Oś widoku Schemat (PLAN_EDYTOR_UT.md § 5b, poprawka 2026-11-02): zamiast prostej pod stałym azymutem – linia po torach
/// (łańcuch: tor wyjściowy i tory, które go przedłużają na stykach), jak plan schematyczny ISDR. Punkt w układzie ma współrzędne
/// u – kilometraż wzdłuż osi [m układu] i v – odsunięcie w prawo od osi [m układu]. Widok rysuje u poziomo, v pionowo (rozciągnięte),
/// więc tory wzdłuż linii leżą poziomo także w łukach. Za końcami osi – przedłużenie po stycznej.
/// Oś to łamana z punktów co 2 m z normalnymi z geometrii toru; normalna między punktami liczona liniowo, więc przejście
/// z odcinka na odcinek jest ciągłe (bez szczelin i zakładek na zewnątrz łuku).
/// </summary>
public sealed class OsSchematu
{
    const double Step = 2, Ext = 5000, Cell = 50;
    readonly XY[] p; readonly XY[] n; readonly double[] s;
    readonly Dictionary<(int, int), List<int>> grid;
    readonly double az0;

    /// <summary>Tory osi (w kolejności), pusto – oś prosta.</summary>
    public IReadOnlyList<string> Tory { get; }
    /// <summary>Oś prosta (widok Teren albo oś pod stałym azymutem).</summary>
    public bool Prosta => grid == null;
    /// <summary>Długość osi po torach [m układu] (bez przedłużeń).</summary>
    public double Dlugosc { get; }

    OsSchematu(double az) { az0 = az; Tory = Array.Empty<string>(); }

    OsSchematu(List<(XY P, double Az)> pts, List<string> tory)
    {
        Tory = tory;
        // przedłużenia po stycznej na obu końcach
        var all = new List<(XY P, double Az)>(pts.Count + 2) { (pts[0].P - Element.Dir(pts[0].Az) * Ext, pts[0].Az) };
        all.AddRange(pts);
        all.Add((pts[^1].P + Element.Dir(pts[^1].Az) * Ext, pts[^1].Az));
        p = all.Select(x => x.P).ToArray(); n = all.Select(x => Element.Right(x.Az)).ToArray();
        s = new double[p.Length]; s[0] = -Ext;
        for (int i = 1; i < p.Length; i++) s[i] = s[i - 1] + XY.Dist(p[i - 1], p[i]);
        Dlugosc = s[^2] - s[1];
        grid = new();
        for (int i = 0; i + 1 < p.Length; i++)
        {
            double len = XY.Dist(p[i], p[i + 1]); int k = Math.Max(1, (int)Math.Ceiling(len / (Cell / 2)));
            for (int j = 0; j <= k; j++)
            {
                var q = p[i] + (p[i + 1] - p[i]) * (j / (double)k); var key = Key(q);
                if (!grid.TryGetValue(key, out var l)) grid[key] = l = new();
                if (l.Count == 0 || l[^1] != i) l.Add(i);
            }
        }
    }

    static (int, int) Key(XY q) => ((int)Math.Floor(q.X / Cell), (int)Math.Floor(q.Y / Cell));

    /// <summary>Oś prosta: azymut [rad] kierunku biegnącego w prawo.</summary>
    public static OsSchematu ProstaAz(double az) => new(az);

    /// <summary>Azymut osi [rad] w kilometrażu u.</summary>
    public double AzAt(double u)
    {
        if (Prosta) return az0;
        var (i, t) = Seg(u); var m = n[i] + (n[i + 1] - n[i]) * t;
        return Math.Atan2(-m.X, m.Y);
    }

    /// <summary>Kilometraż wzdłuż osi i odsunięcie w prawo [m układu].</summary>
    public (double U, double V) UV(XY q)
    {
        if (Prosta) { var d = Element.Dir(az0); var r = Element.Right(az0); return (q.X * d.X + q.Y * d.Y, q.X * r.X + q.Y * r.Y); }
        int best = Nearest(q);
        // pierwiastek w najbliższym odcinku albo w sąsiednich (przy wierzchołku łamanej)
        (double U, double V)? res = null; double bestV = double.MaxValue;
        foreach (int i in new[] { best, best - 1, best + 1, best - 2, best + 2 })
        {
            if (i < 0 || i + 1 >= p.Length) continue;
            if (Solve(i, q) is not double t) continue;
            var (u, v) = At(i, t, q);
            if (Math.Abs(v) < bestV) { bestV = Math.Abs(v); res = (u, v); }
            if (i == best) break;
        }
        if (res is var (ru, rv)) return (ru, rv);
        // poza zasięgiem normalnych (daleko po wewnętrznej stronie łuku) – rzut na odcinek
        var a = p[best]; var dd = p[best + 1] - a; double l2 = dd.X * dd.X + dd.Y * dd.Y;
        double tt = l2 > 0 ? Math.Clamp(((q.X - a.X) * dd.X + (q.Y - a.Y) * dd.Y) / l2, 0, 1) : 0;
        return At(best, tt, q);
    }

    /// <summary>Punkt w układzie z kilometrażu i odsunięcia [m układu].</summary>
    public XY World(double u, double v)
    {
        if (Prosta) return Element.Dir(az0) * u + Element.Right(az0) * v;
        var (i, t) = Seg(u);
        var m = n[i] + (n[i + 1] - n[i]) * t; double ml = Math.Sqrt(m.X * m.X + m.Y * m.Y);
        return p[i] + (p[i + 1] - p[i]) * t + m * (v / ml);
    }

    (double U, double V) At(int i, double t, XY q)
    {
        var l = p[i] + (p[i + 1] - p[i]) * t; var m = n[i] + (n[i + 1] - n[i]) * t; double ml = Math.Sqrt(m.X * m.X + m.Y * m.Y);
        return (s[i] + (s[i + 1] - s[i]) * t, ((q.X - l.X) * m.X + (q.Y - l.Y) * m.Y) / ml);
    }

    /// <summary>Odcinek i położenie w nim dla kilometrażu (poza osią – ekstrapolacja przedłużenia).</summary>
    (int I, double T) Seg(double u)
    {
        int lo = 0, hi = s.Length - 1;
        if (u <= s[0]) lo = 0;
        else if (u >= s[^1]) lo = s.Length - 2;
        else { while (hi - lo > 1) { int mid = (lo + hi) / 2; if (s[mid] <= u) lo = mid; else hi = mid; } }
        double len = s[lo + 1] - s[lo];
        return (lo, len > 0 ? (u - s[lo]) / len : 0);
    }

    /// <summary>t ∈ [0, 1], dla którego punkt leży na normalnej osi: (q − L(t)) × N(t) = 0, N – normalna liniowo między końcami.</summary>
    double? Solve(int i, XY q)
    {
        var a = p[i]; var d = p[i + 1] - a; var qa = q - a; var na = n[i]; var m = n[i + 1] - n[i];
        static double Cr(XY x, XY y) => x.X * y.Y - x.Y * y.X;
        double c0 = Cr(qa, na), c1 = Cr(qa, m) - Cr(d, na), c2 = -Cr(d, m);
        const double eps = 1e-9;
        if (Math.Abs(c2) < 1e-12 * Math.Max(1, Math.Abs(c1)))
        {
            if (Math.Abs(c1) < 1e-15) return null;
            double t = -c0 / c1; return t >= -eps && t <= 1 + eps ? Math.Clamp(t, 0, 1) : null;
        }
        double disc = c1 * c1 - 4 * c2 * c0; if (disc < 0) return null;
        double sq = Math.Sqrt(disc);
        // postać stabilna numerycznie (c2 bardzo małe przy prawie równoległych normalnych)
        double qq = -0.5 * (c1 + Math.Sign(c1 == 0 ? 1 : c1) * sq);
        double? best = null;
        foreach (var t in new[] { qq / c2, qq != 0 ? c0 / qq : double.NaN })
            if (t >= -eps && t <= 1 + eps && (best == null || Math.Abs(t - 0.5) < Math.Abs(best.Value - 0.5))) best = Math.Clamp(t, 0, 1);
        return best;
    }

    /// <summary>Najbliższy odcinek łamanej (siatka co 50 m, szukanie pierścieniami).</summary>
    int Nearest(XY q)
    {
        var (cx, cy) = Key(q); int best = -1; double bd = double.MaxValue;
        for (int r = 0; r <= 60; r++)
        {
            for (int ix = cx - r; ix <= cx + r; ix++)
                for (int iy = cy - r; iy <= cy + r; iy++)
                {
                    if (Math.Max(Math.Abs(ix - cx), Math.Abs(iy - cy)) != r) continue;
                    if (!grid.TryGetValue((ix, iy), out var l)) continue;
                    foreach (int i in l) { double d = DistSeg(i, q); if (d < bd) { bd = d; best = i; } }
                }
            if (best >= 0 && bd < r * Cell - Cell / 4) return best;
        }
        if (best >= 0) return best;
        for (int i = 0; i + 1 < p.Length; i++) { double d = DistSeg(i, q); if (d < bd) { bd = d; best = i; } }
        return best;
    }

    double DistSeg(int i, XY q)
    {
        var a = p[i]; var d = p[i + 1] - a; double l2 = d.X * d.X + d.Y * d.Y;
        double t = l2 > 0 ? Math.Clamp(((q.X - a.X) * d.X + (q.Y - a.Y) * d.Y) / l2, 0, 1) : 0;
        return XY.Dist(q, a + d * t);
    }

    // ------------------------------------------------------------------ oś z projektu

    /// <summary>
    /// Oś schematu projektu: łańcuch od toru <see cref="UtProject.AxisTrack"/>; bez niego – oś prosta pod <see cref="UtProject.AxisAz"/>
    /// (projekty sprzed 2026-11-02) albo najdłuższy łańcuch torów. <see cref="UtProject.AxisRev"/> odwraca kierunek. Bez torów – wschód.
    /// </summary>
    public static OsSchematu Dla(UtProject pr)
    {
        var r = pr.Tracks.ToDictionary(t => t.Id, t => Trasa.Compute(t));
        List<(string Track, double S0, double S1)> chain = null;
        if (pr.AxisTrack != null && r.TryGetValue(pr.AxisTrack, out var rs) && rs.Elements.Count > 0) chain = Lancuch(pr, r, pr.AxisTrack);
        else if (pr.AxisAz is double a) return ProstaAz((a * Math.PI / 180 + (pr.AxisRev ? Math.PI : 0)) % (2 * Math.PI));
        else
        {
            double bl = 0; var done = new HashSet<string>();
            foreach (var t in pr.Tracks.Where(t => r[t.Id].Elements.Count > 0).OrderByDescending(t => r[t.Id].Length))
            {
                if (done.Contains(t.Id)) continue;
                var c = Lancuch(pr, r, t.Id); double len = c.Sum(x => Math.Abs(x.S1 - x.S0));
                foreach (var x in c) if (Math.Abs(Math.Abs(x.S1 - x.S0) - r[x.Track].Length) < 0.01) done.Add(x.Track);
                if (len > bl + 1e-6) { bl = len; chain = c; }
            }
        }
        if (chain == null) return ProstaAz(Math.PI / 2);
        if (pr.AxisRev) chain = chain.AsEnumerable().Reverse().Select(x => (x.Track, x.S1, x.S0)).ToList();
        // punkty co 2 m z azymutem w kierunku osi
        var pts = new List<(XY P, double Az)>();
        foreach (var (id, s0, s1) in chain)
        {
            var rt = r[id]; double len = Math.Abs(s1 - s0); int k = Math.Max(1, (int)Math.Ceiling(len / Step)); double dir = Math.Sign(s1 - s0);
            for (int j = 0; j <= k; j++)
            {
                var (q, az) = rt.At(s0 + (s1 - s0) * j / k); if (dir < 0) az += Math.PI;
                if (pts.Count > 0 && XY.Dist(pts[^1].P, q) < 0.5) continue;
                pts.Add((q, az));
            }
        }
        if (pts.Count < 2) return ProstaAz(Math.PI / 2);
        return new OsSchematu(pts, chain.Select(x => x.Track).ToList());
    }

    /// <summary>
    /// Łańcuch torów od toru wyjściowego (cały, w jego kierunku): na każdym końcu – tor, który go przedłuża (koniec styka się z końcem
    /// albo leży na innym torze), z najmniejszym załamaniem, do 20°. Kawałki: tor i kilometraż od–do [m w terenie].
    /// </summary>
    public static List<(string Track, double S0, double S1)> Lancuch(UtProject pr, Dictionary<string, TrasaResult> r, string start)
    {
        var box = r.Where(x => x.Value.Elements.Count > 0).ToDictionary(x => x.Key, x =>
        {
            var sm = x.Value.Sample(5); return (X0: sm.Min(q => q.P.X) - 1, Y0: sm.Min(q => q.P.Y) - 1, X1: sm.Max(q => q.P.X) + 1, Y1: sm.Max(q => q.P.Y) + 1);
        });
        var used = new HashSet<string> { start };
        // dalej od końca kawałka (tor, kilometraż wyjścia, kierunek jazdy ±1)
        List<(string, double, double)> Dalej(string id, double sOut, int dir)
        {
            var res = new List<(string, double, double)>();
            while (true)
            {
                var (q, az) = r[id].At(sOut); if (dir < 0) az += Math.PI;
                (string Id, double S, int Dir, double Diff)? best = null;
                foreach (var (uid, ru) in r)
                {
                    if (used.Contains(uid) || !box.TryGetValue(uid, out var b) || q.X < b.X0 || q.X > b.X1 || q.Y < b.Y0 || q.Y > b.Y1) continue;
                    var (su, dist) = ru.Nearest(q, 0.25); if (dist > Topologia.Tol) continue;
                    if (XY.Dist(ru.At(su).P, q) > Topologia.Tol) continue;
                    if (su < Topologia.Tol / ru.K) su = 0; else if (su > ru.Length - Topologia.Tol / ru.K) su = ru.Length;
                    foreach (int d in new[] { 1, -1 })
                    {
                        if (d > 0 && su >= ru.Length || d < 0 && su <= 0) continue;
                        double a = ru.At(su).Az + (d < 0 ? Math.PI : 0);
                        double diff = Math.Abs(Math.IEEERemainder(a - az, 2 * Math.PI));
                        if (diff < 20 * Math.PI / 180 && (best == null || diff < best.Value.Diff)) best = (uid, su, d, diff);
                    }
                }
                if (best is not var (nid, ns, nd, _)) return res;
                double end = nd > 0 ? r[nid].Length : 0;
                res.Add((nid, ns, end)); used.Add(nid);
                id = nid; sOut = end; dir = nd;
            }
        }
        var rs = r[start];
        var fwd = Dalej(start, rs.Length, 1);
        var back = Dalej(start, 0, -1);
        var chain = back.AsEnumerable().Reverse().Select(x => (x.Item1, x.Item3, x.Item2)).ToList();
        chain.Add((start, 0, rs.Length));
        chain.AddRange(fwd);
        return chain;
    }
}

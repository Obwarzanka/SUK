namespace LcsWidzew.Model;

/// <summary>
/// Profil podłużny: wysokości terenu wzdłuż trasy (s [m] od początku, h [m n.p.m.]) → odcinki o stałym pochyleniu.
/// Załomy profilu wyznacza uproszczenie Douglasa–Peuckera z tolerancją wysokości (domyślnie 0,3 m).
/// </summary>
public static class Profil
{
    public record Odc(double S0, double S1, double H0, double H1)
    {
        public double Len => S1 - S0;
        /// <summary>Pochylenie [‰], dodatnie – wzniesienie w kierunku trasy.</summary>
        public double Promil => Len > 1e-9 ? (H1 - H0) / Len * 1000 : 0;
    }

    /// <summary>Uproszczenie łamanej profilu (tolerancja w metrach wysokości).</summary>
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

    /// <summary>
    /// Uproszczenie z najkrótszym odcinkiem [m]: po Douglasie–Peuckerze odcinki krótsze niż minLen są scalane z sąsiednim
    /// (usuwany ten załom, którego usunięcie najmniej oddala profil od terenu) – jak odcinki stałego pochylenia na profilu linii.
    /// </summary>
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
            // usuń początek (k) albo koniec (k + 1) krótkiego odcinka – skrajnych punktów trasy nie
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

    public static List<Odc> Sections(IReadOnlyList<(double S, double H)> v) =>
        Enumerable.Range(0, Math.Max(0, v.Count - 1)).Select(i => new Odc(v[i].S, v[i + 1].S, v[i].H, v[i + 1].H)).ToList();

    /// <summary>Wysokość profilu w punkcie s (interpolacja liniowa, poza zakresem – skrajna).</summary>
    public static double At(IReadOnlyList<(double S, double H)> v, double s)
    {
        if (v.Count == 0) return double.NaN;
        if (s <= v[0].S) return v[0].H;
        for (int i = 0; i < v.Count - 1; i++)
            if (s <= v[i + 1].S) { double t = (s - v[i].S) / Math.Max(1e-9, v[i + 1].S - v[i].S); return v[i].H + t * (v[i + 1].H - v[i].H); }
        return v[^1].H;
    }

    /// <summary>Punkty co krok [m] wzdłuż łamanej (metry w terenie), z końcami: (s, x, y) w jednostkach łamanej.</summary>
    public static List<(double S, double X, double Y)> Sample(IReadOnlyList<(double X, double Y)> pts, Func<(double X, double Y), (double X, double Y), double> len, double step)
    {
        var res = new List<(double, double, double)> { (0, pts[0].X, pts[0].Y) };
        double s = 0, next = step;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            double l = len(pts[i], pts[i + 1]); if (l < 1e-9) continue;
            while (next < s + l - 1e-6)
            {
                double t = (next - s) / l;
                res.Add((next, pts[i].X + t * (pts[i + 1].X - pts[i].X), pts[i].Y + t * (pts[i + 1].Y - pts[i].Y)));
                next += step;
            }
            s += l;
        }
        if (s - res[^1].Item1 > 1e-3) res.Add((s, pts[^1].X, pts[^1].Y));
        return res;
    }
}

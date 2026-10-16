namespace Lcs.Model;

/// <summary>
/// Budowa toru odcinkami stycznymi (prosta, łuk) na wieloboku stycznych, połączenia styczne dwóch końców (jeden łuk albo łuki
/// odwrotne) i promień łuku z położenia jego środka. Wymiary wejściowe w terenie (R, L, długości) – w układzie mnożone przez skalę
/// odwzorowania k. Wielobok: łuk o kącie α w wierzchołku V ma styczne T po obu stronach (z przejściowymi – <see cref="Tangent"/>).
/// </summary>
public static class Budowa
{
    const double Eps = 1e-9;

    /// <summary>Kierunek ostatniego odcinka wieloboku [rad] albo null (mniej niż 2 różne punkty).</summary>
    public static double? EndAz(IReadOnlyList<UtVertex> v)
    {
        for (int i = v.Count - 2; i >= 0; i--) if (XY.Dist(v[i].P, v[^1].P) > 1e-6) return Trasa.Az(v[i].P, v[^1].P);
        return null;
    }

    /// <summary>Styczna T [m w układzie] łuku R z przejściowymi L (w układzie) o kącie zwrotu α [rad].</summary>
    public static double Tangent(double r, double l, double alpha)
    {
        alpha = Math.Abs(alpha);
        if (r <= 0 || alpha < Eps) return 0;
        if (l <= 0) return r * Math.Tan(alpha / 2);
        double tau = Math.Atan(l / (2 * r)), p = l - r * Math.Sin(tau), q = l * l / (6 * r) + r * Math.Cos(tau);
        return p + q * Math.Tan(alpha / 2);
    }

    static double Norm(double a) => Math.IEEERemainder(a, 2 * Math.PI);

    /// <summary>
    /// Usuwa wierzchołki wewnętrzne bez załamania (na prostej) o indeksie ≥ <paramref name="from"/>; zwraca liczbę usuniętych
    /// przed indeksem <paramref name="countBefore"/> (do poprawienia liczników).
    /// </summary>
    public static int Simplify(List<UtVertex> v, int from, int countBefore = int.MaxValue)
    {
        int removedBefore = 0;
        for (int i = Math.Max(1, from); i < v.Count - 1;)
        {
            double a1 = Trasa.Az(v[i - 1].P, v[i].P), a2 = Trasa.Az(v[i].P, v[i + 1].P);
            bool dup = XY.Dist(v[i - 1].P, v[i].P) < 1e-6;
            if (dup || Math.Abs(Norm(a2 - a1)) < 1e-7) { v.RemoveAt(i); if (i < countBefore) removedBefore++; }
            else i++;
        }
        return removedBefore;
    }

    /// <summary>Prosta na przedłużeniu ostatniego odcinka o długości <paramref name="len"/> [m w terenie].</summary>
    public static List<UtVertex> Straight(IReadOnlyList<UtVertex> v, double len, double k)
    {
        var res = v.Select(x => x.Clone()).ToList();
        if (EndAz(v) is not double az || len <= 0) return res;
        var e = v[^1].P + Element.Dir(az) * (len * k);
        res.Add(new UtVertex { X = e.X, Y = e.Y });
        return res;
    }

    /// <summary>
    /// Łuk styczny do końca: promień R i przejściowe L [m w terenie], kąt zwrotu α [rad] ze znakiem (+ w prawo). Dodaje wierzchołek V
    /// (z R, L) w odległości T od końca i punkt końca łuku W = V + T w nowym kierunku.
    /// </summary>
    public static List<UtVertex> Arc(IReadOnlyList<UtVertex> v, double r, double l, double alpha, double k)
    {
        var res = v.Select(x => x.Clone()).ToList();
        if (EndAz(v) is not double az || r <= 0 || Math.Abs(alpha) < 1e-6) return res;
        double t = Tangent(r * k, l * k, alpha);
        var vv = v[^1].P + Element.Dir(az) * t; var w = vv + Element.Dir(az + alpha) * t;
        res.Add(new UtVertex { X = vv.X, Y = vv.Y, R = r, L = l });
        res.Add(new UtVertex { X = w.X, Y = w.Y });
        return res;
    }

    /// <summary>Kąt łuku R od końca toru do kierunku kursora: strona wg kursora, kąt środkowy do rzutu kursora na okrąg (0,5°–170°).</summary>
    public static double ArcAngleTo(IReadOnlyList<UtVertex> v, double r, double k, XY cursor)
    {
        if (EndAz(v) is not double az) return 0;
        var e = v[^1].P; var rt = Element.Right(az);
        double side = (cursor - e).X * rt.X + (cursor - e).Y * rt.Y >= 0 ? 1 : -1;
        var c = e + rt * (side * r * k);
        double a0 = Trasa.Az(c, e), a1 = Trasa.Az(c, cursor);
        double th = side > 0 ? a1 - a0 : a0 - a1;
        th = ((th % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI);
        if (th > 1.75 * Math.PI) th = 0;                                  // kursor tuż za początkiem – najmniejszy łuk
        return side * Math.Clamp(th, 0.5 * Math.PI / 180, 170 * Math.PI / 180);
    }

    /// <summary>Kąt łuku (ze znakiem strony), dla którego koniec jest równoległy do kierunku <paramref name="axis"/> (lub przeciwnego).</summary>
    public static double ArcAngleParallel(IReadOnlyList<UtVertex> v, double side, double axis)
    {
        if (EndAz(v) is not double az) return 0;
        double best = double.MaxValue;
        foreach (var target in new[] { axis, axis + Math.PI })
        {
            double th = side > 0 ? target - az : az - target;
            th = ((th % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI);
            if (th > 0.5 * Math.PI / 180 && th < best) best = th;
        }
        return best < Math.PI ? side * best : 0;
    }

    /// <summary>
    /// Połączenie styczne z końca <paramref name="e1"/> (kierunek <paramref name="az1"/>) do punktu <paramref name="e2"/> z dojazdem
    /// w kierunku <paramref name="az2"/>: wierzchołki pośrednie (z R). Styczne przecinają się przed oboma końcami – jeden łuk
    /// (R chciany albo największy, jaki się mieści); inaczej łuki odwrotne z wierzchołkami w ¼ odległości wzdłuż. Null – nie da się
    /// (cel za plecami). <paramref name="usedR"/> – promień użyty [m w terenie].
    /// </summary>
    public static List<UtVertex> Connect(XY e1, double az1, XY e2, double az2, double rWant, double l, double k, out double usedR, out string note)
    {
        usedR = 0; note = null;
        var d1 = Element.Dir(az1); var d2 = Element.Dir(az2); var dd = e2 - e1;
        double dist = dd.Length;
        if (dist < 1e-6) return new();
        double lat = d1.X * dd.Y - d1.Y * dd.X, along = d1.X * dd.X + d1.Y * dd.Y;
        double turn = Norm(az2 - az1);
        // na prostej – bez wierzchołków
        if (Math.Abs(turn) < 1e-7 && Math.Abs(lat) < 1e-4 && along > 0) return new();
        double cr = d1.X * d2.Y - d1.Y * d2.X;
        if (Math.Abs(cr) > 1e-6)
        {
            // przecięcie stycznych: e1 + t·d1 = e2 − u·d2
            double t = (dd.X * d2.Y - dd.Y * d2.X) / cr, u = (d1.X * dd.Y - d1.Y * dd.X) / cr;
            if (t > 1e-3 && u > 1e-3)
            {
                var x = e1 + d1 * t; double a = Math.Abs(turn), rk = rWant * k;
                double rmax = Math.Min(t, u) / Math.Tan(a / 2);
                if (rk > rmax) { rk = rmax; note = $"promień zmniejszony do {rk / k:0.#} m – większy się nie mieści"; }
                usedR = Math.Floor(rk / k * 10) / 10;
                var vx = new UtVertex { X = x.X, Y = x.Y, R = usedR, L = l > 0 && Tangent(usedR * k, l * k, a) <= Math.Min(t, u) ? l : 0 };
                return new() { vx };
            }
        }
        // łuki odwrotne: V1 = e1 + s·d1, V2 = e2 − s·d2
        if (along <= 1e-3 || d2.X * dd.X + d2.Y * dd.Y <= 1e-3) { note = "drugi koniec leży za początkiem – połączenie niemożliwe"; return null; }
        double s = along / 4;
        var v1 = e1 + d1 * s; var v2 = e2 - d2 * s; var m = v2 - v1; double ml = m.Length; if (ml < 1e-6) return null;
        double am = Trasa.Az(v1, v2), b1 = Math.Abs(Norm(am - az1)), b2 = Math.Abs(Norm(az2 - am));
        double t1 = Math.Tan(b1 / 2), t2 = Math.Tan(b2 / 2);
        double rmax2 = double.MaxValue;
        if (t1 > Eps) rmax2 = Math.Min(rmax2, s / t1);
        if (t2 > Eps) rmax2 = Math.Min(rmax2, s / t2);
        if (t1 + t2 > Eps) rmax2 = Math.Min(rmax2, ml / (t1 + t2));
        double r2 = rWant * k;
        if (r2 > rmax2) { r2 = rmax2; note = $"łuki odwrotne: promień zmniejszony do {r2 / k:0.#} m – większy się nie mieści"; }
        usedR = Math.Floor(r2 / k * 10) / 10;
        return new() { new UtVertex { X = v1.X, Y = v1.Y, R = b1 > 1e-7 ? usedR : 0 }, new UtVertex { X = v2.X, Y = v2.Y, R = b2 > 1e-7 ? usedR : 0 } };
    }

    /// <summary>Środek łuku (z przejściowymi) w wierzchołku i toru; null – brak łuku.</summary>
    public static XY? ArcMid(TrasaResult r, int i)
    {
        var els = r.Elements.Where(e => e.Vertex == i && e.Kind != ElKind.Prosta).ToList();
        if (els.Count == 0) return null;
        double half = els.Sum(e => e.Len) / 2;
        foreach (var e in els) { if (half <= e.Len) return e.At(half).P; half -= e.Len; }
        return els[^1].End;
    }

    /// <summary>
    /// Promień [m w terenie] łuku w wierzchołku i, przy którym środek łuku leży w odległości <paramref name="dist"/> [m w układzie]
    /// od wierzchołka (przeciąganie uchwytu); przejściowe L bez zmian. Bisekcja na łuku z sąsiednimi wierzchołkami.
    /// </summary>
    public static double RadiusForMid(IReadOnlyList<UtVertex> v, int i, double dist, double k)
    {
        if (i <= 0 || i >= v.Count - 1) return 0;
        double Mid(double r)
        {
            var loc = new[] { v[i - 1].Clone(), v[i].Clone(), v[i + 1].Clone() };
            loc[0].R = 0; loc[2].R = 0; loc[1].R = r;
            var res = Trasa.Compute(loc, k);
            if (!res.Ok) return double.NaN;
            return ArcMid(res, 1) is XY m ? XY.Dist(m, v[i].P) : 0;
        }
        double lo = 1, hi = 1;
        while (hi < 1e6 && Mid(hi * 2) is var d && !double.IsNaN(d) && d < dist) hi *= 2;
        hi *= 2;
        for (int it = 0; it < 60; it++)
        {
            double mid = (lo + hi) / 2, d = Mid(mid);
            if (double.IsNaN(d) || d > dist) hi = mid; else lo = mid;
        }
        return Math.Max(1, Math.Round(lo));
    }
    /// <summary>
    /// Część toru od kilometrażu <paramref name="s1"/> do <paramref name="s2"/> [m w terenie, od początku toru]. Cięcie na prostej –
    /// dokładnie w tym miejscu; w łuku (z przejściowymi) – na bliższym końcu łuku, bo wielobok styczny nie umie zacząć się w łuku.
    /// </summary>
    public static List<UtVertex> Trim(IReadOnlyList<UtVertex> v, double k, double s1, double s2)
    {
        if (s1 > s2) (s1, s2) = (s2, s1);
        var r = Trasa.Compute(v, k);
        if (r.Elements.Count == 0) return v.Select(x => x.Clone()).ToList();
        s1 = Math.Max(0, s1); s2 = Math.Min(r.Length, s2);
        // elementy z kilometrażem początku [m w układzie]
        var els = new List<(Element E, double A)>(); double acc = 0;
        foreach (var e in r.Elements) { els.Add((e, acc)); acc += e.Len; }
        (Element E, double A) At(double g) { foreach (var x in els) if (g <= x.A + x.E.Len + 1e-9) return x; return els[^1]; }
        var res = v.Select(x => x.Clone()).ToList();
        // koniec (najpierw – indeksy początku się nie zmienią)
        double g2 = s2 * r.K; var (e2, a2) = At(g2);
        if (e2.Kind == ElKind.Prosta)
        {
            var q = e2.At(g2 - a2).P; int j = e2.Vertex;
            res = res.Take(j).ToList(); res.Add(new UtVertex { X = q.X, Y = q.Y });
        }
        else
        {
            // łuk w wierzchołku j: koniec na początku (przed łukiem) albo na końcu (za łukiem) wszystkich elementów tego łuku
            int j = e2.Vertex; var arc = els.Where(x => x.E.Vertex == j && x.E.Kind != ElKind.Prosta).ToList();
            double ga = arc[0].A, gb = arc[^1].A + arc[^1].E.Len;
            if (g2 - ga < gb - g2) { var q = arc[0].E.At(0).P; res = res.Take(j).ToList(); res.Add(new UtVertex { X = q.X, Y = q.Y }); }
            else { var q = arc[^1].E.End; res = res.Take(j + 1).ToList(); res.Add(new UtVertex { X = q.X, Y = q.Y }); }
        }
        // początek
        double g1 = s1 * r.K; var (e1, a1) = At(g1);
        int n0 = res.Count;
        if (e1.Kind == ElKind.Prosta)
        {
            var q = e1.At(g1 - a1).P; int j = e1.Vertex;
            if (j < n0) { res = res.Skip(j).ToList(); res.Insert(0, new UtVertex { X = q.X, Y = q.Y }); res[0].R = 0; }
        }
        else
        {
            int j = e1.Vertex; var arc = els.Where(x => x.E.Vertex == j && x.E.Kind != ElKind.Prosta).ToList();
            double ga = arc[0].A, gb = arc[^1].A + arc[^1].E.Len;
            if (g1 - ga < gb - g1) { var q = arc[0].E.At(0).P; res = res.Skip(j).ToList(); res.Insert(0, new UtVertex { X = q.X, Y = q.Y }); }
            else if (j + 1 < n0) { var q = arc[^1].E.End; res = res.Skip(j + 1).ToList(); res.Insert(0, new UtVertex { X = q.X, Y = q.Y }); }
        }
        if (res.Count > 0) { res[0].R = 0; res[0].L = 0; res[^1].R = 0; res[^1].L = 0; }
        Simplify(res, 1);
        return res;
    }
}

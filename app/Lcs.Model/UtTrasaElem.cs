using System.Runtime.CompilerServices;

namespace Lcs.Model;

/// <summary>
/// Trasa z elementów: początek (X, Y w PUWG 1992), azymut początkowy [rad, w układzie] i ciąg elementów – prosta, łuk, krzywa
/// przejściowa (klotoida: krzywizna liniowo od 1/R0 do 1/R1). Pozwala na różne przejściowe na wejściu i wyjściu, przejściową między
/// łukami (łuk koszowy bez skoku krzywizny) i dowolną zmianę krzywizny. Edytuje się ją na wykresie krzywizny.
/// </summary>
public class UtAlign
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Az { get; set; }
    public List<UtAlignEl> Els { get; set; } = new();
    public UtAlign Clone() => new() { X = X, Y = Y, Az = Az, Els = Els.Select(e => e.Clone()).ToList() };
}

/// <summary>
/// Element trasy: długość [m w terenie], promień na początku i na końcu [m w terenie, + łuk w prawo, − w lewo, 0 – prosta].
/// Prosta: R0 = R1 = 0; łuk: R0 = R1; krzywa przejściowa: R0 ≠ R1 (np. 0 → 600, 600 → 900, 600 → 0).
/// </summary>
public class UtAlignEl
{
    public double Len { get; set; }
    public double R0 { get; set; }
    public double R1 { get; set; }
    public UtAlignEl Clone() => new() { Len = Len, R0 = R0, R1 = R1 };
    public bool Prosta => R0 == 0 && R1 == 0;
    public bool Luk => R0 != 0 && Math.Abs(R0 - R1) < 1e-9;
    public bool Przejsciowa => !Prosta && !Luk;
    public static double K(double r) => r == 0 ? 0 : 1 / r;
    public string Nazwa => Prosta ? "prosta" : Luk ? (R0 > 0 ? "łuk w prawo" : "łuk w lewo") : "przejściowa";
}

/// <summary>Trasa z elementów: obliczenie, zamiana z / na wielobok, przycinanie, tor równoległy, porządkowanie przejściowych.</summary>
public static class TrasaElem
{
    // klotoida – punkty co ≤ 5 m od początku (całkowanie Gaussa), dla każdego elementu raz
    static readonly ConditionalWeakTable<Element, XY[]> cache = new();

    /// <summary>Azymut klotoidy po s [m w układzie].</summary>
    public static double ClothoidAz(Element e, double s) => e.Az0 + e.K0 * s + (e.K1 - e.K0) * s * s / (2 * Math.Max(e.Len, 1e-12));

    static XY Integrate(Element e, double a, double b)
    {
        // Gauss–Legendre 5 punktów na odcinku [a, b]
        double[] x = { -0.9061798459386640, -0.5384693101056831, 0, 0.5384693101056831, 0.9061798459386640 };
        double[] w = { 0.2369268850561891, 0.4786286704993665, 0.5688888888888889, 0.4786286704993665, 0.2369268850561891 };
        double h = (b - a) / 2, m = (a + b) / 2, sx = 0, sy = 0;
        for (int i = 0; i < 5; i++) { double az = ClothoidAz(e, m + h * x[i]); sx += w[i] * Math.Cos(az); sy += w[i] * Math.Sin(az); }
        return new XY(sx * h, sy * h);
    }

    // łamana klotoidy co ≤ 1 m (do odległości punktu od osi w dopasowaniach)
    static readonly ConditionalWeakTable<Element, XY[]> polyCache = new();
    public static XY[] Polyline(Element e) => polyCache.GetValue(e, el =>
    {
        int n = Math.Max(2, (int)Math.Ceiling(el.Len / 1.0)); var a = new XY[n + 1]; a[0] = el.Start;
        for (int i = 1; i <= n; i++) a[i] = a[i - 1] + Integrate(el, el.Len * (i - 1) / n, el.Len * i / n);
        return a;
    });

    /// <summary>Punkt klotoidy po s [m w układzie] od jej początku.</summary>
    public static XY ClothoidAt(Element e, double s)
    {
        var pts = cache.GetValue(e, el =>
        {
            int n = Math.Max(1, (int)Math.Ceiling(el.Len / 5)); var a = new XY[n + 1]; a[0] = el.Start;
            for (int i = 1; i <= n; i++) a[i] = a[i - 1] + Integrate(el, el.Len * (i - 1) / n, el.Len * i / n);
            return a;
        });
        int nn = pts.Length - 1; double step = e.Len / nn;
        int j = Math.Clamp((int)Math.Floor(s / Math.Max(step, 1e-12)), 0, nn - 1);
        return pts[j] + Integrate(e, j * step, s);
    }

    /// <summary>Elementy osi z trasy z elementów (długości i promienie razy skala odwzorowania k).</summary>
    public static TrasaResult Compute(UtAlign a, double k)
    {
        var res = new TrasaResult { K = k };
        if (a == null || a.Els.Count == 0) { res.Notes.Add((0, "trasa bez elementów", true)); return res; }
        var cur = new XY(a.X, a.Y); double az = a.Az;
        for (int i = 0; i < a.Els.Count; i++)
        {
            var el = a.Els[i]; double len = el.Len * k;
            if (len <= 1e-9) { res.Notes.Add((i, $"element {i + 1} ({el.Nazwa}) ma długość 0", true)); continue; }
            Element e;
            if (el.Prosta) e = new Element(ElKind.Prosta, cur, az, len, 0, 0, false, default, 0, i);
            else if (el.Luk) e = new Element(ElKind.Luk, cur, az, len, el.R0 * k, 0, false, default, 0, i);
            else e = new Element(ElKind.Przejsciowa, cur, az, len, (el.R1 != 0 ? el.R1 : el.R0) * k, 0, false, default, 0, i, UtAlignEl.K(el.R0) / k, UtAlignEl.K(el.R1) / k, true);
            res.Elements.Add(e);
            (cur, az) = e.At(e.Len);
        }
        return res;
    }

    /// <summary>Przejściowe dopasowane do sąsiadów: promień na początku = na końcu poprzedniego elementu, na końcu = na początku następnego.</summary>
    public static void Normalize(UtAlign a)
    {
        var e = a.Els;
        for (int i = 0; i < e.Count; i++)
        {
            if (e[i].Prosta || e[i].Luk) continue;
            e[i].R0 = i > 0 ? e[i - 1].R1 : 0;
            if (i + 1 < e.Count && !e[i + 1].Przejsciowa) e[i].R1 = e[i + 1].R0;
            else if (i + 1 >= e.Count) e[i].R1 = e[i].R1;
        }
        // przejściowa, która po zmianach łączy te same krzywizny, to łuk (albo prosta)
        foreach (var x in e.Where(x => !x.Prosta && Math.Abs(x.R0 - x.R1) < 1e-9)) x.R1 = x.R0;
    }

    /// <summary>Wielobok → elementy (parabola 3. stopnia → klotoida tej samej długości). Max – największa odchyłka [m w terenie].</summary>
    public static (UtAlign A, double Max) FromPolygon(UtTrack t)
    {
        var r = Trasa.Compute(t.Vertices, Trasa.KFor(t)); double k = r.K;
        var a = new UtAlign();
        if (r.Elements.Count == 0) return (null, 0);
        a.X = r.Elements[0].Start.X; a.Y = r.Elements[0].Start.Y; a.Az = r.Elements[0].Az0;
        foreach (var e in r.Elements)
        {
            double len = e.Len / k, R = e.R / k;
            var el = e.Kind switch
            {
                ElKind.Prosta => new UtAlignEl { Len = len },
                ElKind.Luk => new UtAlignEl { Len = len, R0 = R, R1 = R },
                _ => e.Out ? new UtAlignEl { Len = len, R0 = R, R1 = 0 } : new UtAlignEl { Len = len, R0 = 0, R1 = R },
            };
            if (el.Prosta && a.Els.Count > 0 && a.Els[^1].Prosta) a.Els[^1].Len += len; else a.Els.Add(el);
        }
        var na = Compute(a, k);
        double max = r.Sample(2).Max(q => Budowa.Dist(na, q.P)) / k;
        return (a, max);
    }

    /// <summary>
    /// Elementy → wielobok (przybliżenie): każdy łuk między prostymi (albo do zmiany kierunku skrętu) – jeden wierzchołek w przecięciu
    /// stycznych na jego początku i końcu, R – najmniejszy z łuków grupy, przejściowe L1 / L2 – pierwsza i ostatnia przejściowa.
    /// Max – największa odchyłka [m w terenie].
    /// </summary>
    public static (List<UtVertex> V, double Max) ToPolygon(UtAlign a, double k)
    {
        var r = Compute(a, k); var els = a.Els;
        var v = new List<UtVertex> { new() { X = a.X, Y = a.Y } };
        int i = 0; double s = 0;
        var starts = new List<double>(); double acc = 0; foreach (var e in els) { starts.Add(acc); acc += e.Len; }
        while (i < els.Count)
        {
            if (els[i].Prosta) { i++; continue; }
            int j = i; int sign = Math.Sign(els[i].R0 != 0 ? els[i].R0 : els[i].R1);
            while (j + 1 < els.Count && !els[j + 1].Prosta)
            {
                int sg = Math.Sign(els[j + 1].R0 != 0 ? els[j + 1].R0 : els[j + 1].R1);
                // zmiana kierunku skrętu w przejściowej (esowanie) – osobne wierzchołki
                if (sg != 0 && sign != 0 && sg != sign) break;
                if (sign == 0) sign = sg;
                j++;
            }
            var (p0, az0) = r.At(starts[i]); var (p1, az1) = r.At(starts[j] + els[j].Len);
            var d0 = Element.Dir(az0); var d1 = Element.Dir(az1); double den = d0.X * d1.Y - d0.Y * d1.X;
            if (Math.Abs(den) > 1e-12)
            {
                var w = p1 - p0; double t = (w.X * d1.Y - w.Y * d1.X) / den; var x = p0 + d0 * t;
                var arcs = els.Skip(i).Take(j - i + 1).Where(e => e.Luk).ToList();
                double R = arcs.Count > 0 ? arcs.Min(e => Math.Abs(e.R0)) : Math.Abs(els[i].R1 != 0 ? els[i].R1 : els[i].R0);
                double L1 = els[i].Przejsciowa && els[i].R0 == 0 ? els[i].Len : 0, L2 = els[j].Przejsciowa && els[j].R1 == 0 ? els[j].Len : 0;
                v.Add(new UtVertex { X = x.X, Y = x.Y, R = Math.Round(R, 1), L = Math.Round(L1, 2), L2 = Math.Abs(L1 - L2) > 1e-6 ? Math.Round(L2, 2) : null });
            }
            i = j + 1;
        }
        var end = r.Elements.Count > 0 ? r.Elements[^1].End : new XY(a.X, a.Y);
        v.Add(new UtVertex { X = end.X, Y = end.Y });
        var pr = Trasa.Compute(v, k);
        double max = r.Sample(2).Max(q => Budowa.Dist(pr, q.P)) / k;
        return (v, max);
    }

    /// <summary>Część trasy od km s1 do s2 [m w terenie] (przejściowa przycięta – promienie w miejscach cięcia).</summary>
    public static UtAlign Trim(UtAlign a, double k, double s1, double s2)
    {
        var r = Compute(a, k); var (p, az) = r.At(s1);
        var res = new UtAlign { X = p.X, Y = p.Y, Az = az }; double acc = 0;
        foreach (var e in a.Els)
        {
            double e0 = acc, e1 = acc + e.Len; acc = e1;
            double c0 = Math.Max(s1, e0), c1 = Math.Min(s2, e1);
            if (c1 - c0 <= 1e-6) continue;
            double K(double s) => UtAlignEl.K(e.R0) + (UtAlignEl.K(e.R1) - UtAlignEl.K(e.R0)) * (s - e0) / Math.Max(e.Len, 1e-12);
            double Rr(double kk) => Math.Abs(kk) < 1e-12 ? 0 : 1 / kk;
            res.Els.Add(e.Prosta || e.Luk ? new UtAlignEl { Len = c1 - c0, R0 = e.R0, R1 = e.R1 } : new UtAlignEl { Len = c1 - c0, R0 = Rr(K(c0)), R1 = Rr(K(c1)) });
        }
        return res;
    }

    /// <summary>
    /// Trasa równoległa w odległości d [m w terenie] (+ po prawej): łuki współśrodkowe R − d, przejściowe z promieniami R − d.
    /// Przesunięta klotoida nie jest klotoidą, więc w każdym łuku (grupie między prostymi) długości przejściowych i łuku dobierane są
    /// przy stałym kącie zwrotu tak, żeby prosta za łukiem leżała dokładnie w odległości d; długości prostych – do początku
    /// następnego łuku (przesunięty początek łuku wzorcowego).
    /// </summary>
    public static UtAlign Offset(UtAlign a, double k, double d)
    {
        var orig = Compute(a, k); var starts = new List<double>(); double acc0 = 0;
        foreach (var e in a.Els) { starts.Add(acc0); acc0 += e.Len; }
        XY OffPt(double s) { var (p, az) = orig.At(s); return p + Element.Right(az) * (d * k); }
        double Ang(UtAlignEl e) => e.Len * (UtAlignEl.K(e.R0) + UtAlignEl.K(e.R1)) / 2;
        double R(double r) => r == 0 ? 0 : r - d;
        var p0 = OffPt(0);
        var res = new UtAlign { X = p0.X, Y = p0.Y, Az = a.Az };
        foreach (var e in a.Els)
        {
            var n = new UtAlignEl { R0 = R(e.R0), R1 = R(e.R1) };
            double kn = (UtAlignEl.K(n.R0) + UtAlignEl.K(n.R1)) / 2;
            n.Len = Math.Abs(kn) < 1e-15 ? e.Len : Ang(e) / kn;
            res.Els.Add(n);
        }
        // koniec trasy częściowej (elementy 0..last) w układzie
        (XY P, double Az) EndOf(int last) { var t = new UtAlign { X = res.X, Y = res.Y, Az = res.Az, Els = res.Els.Take(last + 1).ToList() }; var r = Compute(t, k); return r.Elements.Count > 0 ? r.Elements[^1].At(r.Elements[^1].Len) : (new XY(t.X, t.Y), t.Az); }
        int i = 0, n2 = res.Els.Count;
        while (i < n2)
        {
            if (res.Els[i].Prosta)
            {
                // prosta do miejsca, w którym zaczyna się następny element (przesunięty początek elementu wzorcowego)
                if (i + 1 < n2)
                {
                    var cur = i > 0 ? EndOf(i - 1) : (new XY(res.X, res.Y), res.Az);
                    var target = OffPt(starts[i + 1]); var dir = Element.Dir(cur.Item2); var w = target - cur.Item1;
                    res.Els[i].Len = Math.Max(0.001, (w.X * dir.X + w.Y * dir.Y) / k);
                }
                i++; continue;
            }
            int j = i; while (j + 1 < n2 && !res.Els[j + 1].Prosta) j++;
            var arcs = Enumerable.Range(i, j - i + 1).Where(x => res.Els[x].Luk).ToList();
            var trans = Enumerable.Range(i, j - i + 1).Where(x => res.Els[x].Przejsciowa).ToList();
            if (arcs.Count > 0 && trans.Count > 0 && j + 1 < n2)
            {
                var baseLen = res.Els.Select(x => x.Len).ToList();
                double aT = trans.Sum(x => Ang(res.Els[x])), aA = arcs.Sum(x => Ang(res.Els[x])), aTot = aT + aA;
                // za łukiem: prosta wzorcowa przesunięta o d
                var (lp, laz) = orig.At(starts[j + 1]); var linePt = lp + Element.Right(laz) * (d * k); var ln = Element.Right(laz);
                double Lat(double ts)
                {
                    double asc = (aTot - ts * aT) / aA; if (asc <= 0) return double.NaN;
                    foreach (var x in trans) res.Els[x].Len = baseLen[x] * ts;
                    foreach (var x in arcs) res.Els[x].Len = baseLen[x] * asc;
                    var (pe, _) = EndOf(j); var w = pe - linePt; return w.X * ln.X + w.Y * ln.Y;
                }
                double t0 = 1, f0 = Lat(t0), t1 = 1.02, f1 = Lat(t1);
                for (int it = 0; it < 30 && double.IsFinite(f0) && double.IsFinite(f1) && Math.Abs(f1) > 1e-7 && Math.Abs(f1 - f0) > 1e-15; it++)
                { double t2 = t1 - f1 * (t1 - t0) / (f1 - f0); t0 = t1; f0 = f1; t1 = Math.Clamp(t2, 0.2, 5); f1 = Lat(t1); }
                if (!double.IsFinite(f1) || Math.Abs(f1) > 0.05 * k) Lat(1);
            }
            i = j + 1;
        }
        return res;
    }

    /// <summary>
    /// Dopasowanie trasy z elementów do punktów (np. kliknięte na zdjęciu): długości elementów, których dotyczą punkty (i po jednym
    /// sąsiednim), i promienie łuków wśród nich – metodą najmniejszych kwadratów (simpleks Neldera–Meada); przejściowe biorą promienie
    /// od sąsiadów. Początek trasy zostaje. Wynik: nowa trasa, odchyłki punktów [m w terenie].
    /// </summary>
    public static (UtAlign A, double Rms, double Max, List<double> Dev, string Error) Fit(UtAlign a, double k, IReadOnlyList<XY> pts)
    {
        if (pts.Count < 3) return (null, 0, 0, null, "kliknij co najmniej 3 punkty");
        var r0 = Compute(a, k); var starts = new List<double>(); double acc = 0;
        foreach (var e in a.Els) { starts.Add(acc); acc += e.Len; }
        var ss = pts.Select(q => r0.Nearest(q, 0.5).S).ToList();
        int Idx(double s) { for (int i = a.Els.Count - 1; i >= 0; i--) if (s >= starts[i] - 1e-6) return i; return 0; }
        int lo = Math.Max(0, Idx(ss.Min()) - 1), hi = Math.Min(a.Els.Count - 1, Idx(ss.Max()) + 1);
        var vars = new List<(int I, bool R)>();
        for (int i = lo; i <= hi; i++) { vars.Add((i, false)); if (a.Els[i].Luk) vars.Add((i, true)); }
        int n = vars.Count;
        UtAlign Make(double[] x)
        {
            var c = a.Clone();
            for (int j = 0; j < n; j++)
            {
                var (i, isR) = vars[j];
                if (isR) { double r = Math.Exp(x[j]) * Math.Sign(a.Els[i].R0); c.Els[i].R0 = c.Els[i].R1 = r; }
                else c.Els[i].Len = Math.Exp(x[j]);
            }
            Normalize(c); return c;
        }
        double F(double[] x)
        {
            var c = Make(x); if (c.Els.Any(e => e.Len < 0.2)) return 1e18;
            var r = Compute(c, k); if (!r.Ok) return 1e18;
            double sum = 0; foreach (var q in pts) { var d = Budowa.Dist(r, q); sum += d * d; }
            return sum;
        }
        var x0 = vars.Select(v => Math.Log(v.R ? Math.Abs(a.Els[v.I].R0) : Math.Max(a.Els[v.I].Len, 0.5))).ToArray();
        // reszty: odległości punktów od osi ze znakiem (Levenberg–Marquardt); zła geometria – duże reszty
        double[] Res(double[] x)
        {
            var c = Make(x); var bad = Enumerable.Repeat(1e3, pts.Count).ToArray();
            if (c.Els.Any(e => e.Len < 0.2)) return bad;
            var r = Compute(c, k); if (!r.Ok) return bad;
            return pts.Select(q => Budowa.SignedDist(r, q)).ToArray();
        }
        var best = Budowa.LevMar(Res, x0, n, 80);
        if (F(best) > F(x0)) best = Budowa.NelderMead(F, x0, vars.Select(v => 0.1).ToArray(), n, 300 * n);
        var res = Make(best);
        foreach (var e in res.Els) { e.Len = Math.Round(e.Len, 2); if (e.Luk) e.R0 = e.R1 = Math.Round(e.R0); }
        Normalize(res);
        var rr = Compute(res, k); if (!rr.Ok) return (null, 0, 0, null, "nie udało się dopasować – dodaj punkty");
        var dev = pts.Select(q => Budowa.Dist(rr, q) / k).ToList();
        return (res, Math.Sqrt(dev.Sum(x => x * x) / dev.Count), dev.Max(), dev, null);
    }

    /// <summary>Wierzchołki toru z elementów = początek i koniec trasy (dla skali, końców, przyciągania).</summary>
    public static void SyncEnds(UtTrack t)
    {
        if (t.Align == null) return;
        var r = Compute(t.Align, Trasa.KFor(t));
        var end = r.Elements.Count > 0 ? r.Elements[^1].End : new XY(t.Align.X, t.Align.Y);
        t.Vertices = new() { new UtVertex { X = t.Align.X, Y = t.Align.Y }, new UtVertex { X = end.X, Y = end.Y } };
    }
}

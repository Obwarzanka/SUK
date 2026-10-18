namespace Lcs.Model;

/// <summary>
/// Tor (oś toru) jak trasa kolejowa: wielobok styczny – wierzchołki w PUWG 1992 – i w każdym wierzchołku wewnętrznym łuk kołowy
/// o promieniu R z krzywymi przejściowymi (parabola 3. stopnia) o długości L po obu stronach. Projektant klika wierzchołki
/// i wpisuje R, L; elementy (prosta – przejściowa – łuk – przejściowa – prosta) wynikają z geometrii (<see cref="Trasa"/>).
/// Kierunek toru: od pierwszego do ostatniego wierzchołka (kilometraż toru). Rozjazdy i urządzenia (kolejne etapy) przypina się
/// do toru po kilometrażu – tor nie jest cięty.
/// </summary>
public class UtTrack
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    /// <summary>Numer toru (np. „1”, „2a”, „101”) – wpisuje projektant.</summary>
    public string Number { get; set; } = "";
    /// <summary>Kategoria (Ig-10): <c>gz</c> – główny zasadniczy, <c>gd</c> – główny dodatkowy, puste – boczny.</summary>
    public string Cat { get; set; } = "";
    public bool Elec { get; set; }
    /// <summary>Prędkość największa [km/h], 0 – nie ustalona.</summary>
    public int Vmax { get; set; }
    public List<UtVertex> Vertices { get; set; } = new();
    /// <summary>Trasa z elementów (prosta / przejściowa / łuk) – gdy jest, geometria toru liczy się z niej, a <see cref="Vertices"/> to tylko początek i koniec.</summary>
    public UtAlign Align { get; set; }
    /// <summary>Początek / koniec toru na granicy obszaru (tor biegnie dalej poza projektem) – „Sprawdź” nie zgłasza otwartego końca.</summary>
    public bool OpenStart { get; set; }
    public bool OpenEnd { get; set; }
    /// <summary>Posterunek, do którego należy tor (id <see cref="UtPost"/>); null – brak (np. tor szlakowy).</summary>
    public string Post { get; set; }
    /// <summary>Niweleta: załomy profilu podłużnego (km toru, wysokość główki szyny, łuk pionowy) – <see cref="Lcs.Model.Profil"/>.</summary>
    public List<UtProfilePoint> Profile { get; set; } = new();
    /// <summary>Teren wzdłuż osi toru z NMT GUGiK (km, wysokość) – do porównania z niweletą (nasyp / przekop).</summary>
    public List<UtProfilePoint> Terrain { get; set; } = new();

    public const string MainZ = "gz", MainD = "gd";
}

/// <summary>Wierzchołek wieloboku stycznych: punkt w PUWG 1992, w wierzchołku wewnętrznym promień łuku i długość krzywych przejściowych [m, w terenie].</summary>
public class UtVertex
{
    public double X { get; set; }
    public double Y { get; set; }
    /// <summary>Promień łuku [m] (0 – bez łuku: załamanie).</summary>
    public double R { get; set; }
    /// <summary>Długość krzywej przejściowej (parabola 3. stopnia, mierzona wzdłuż stycznej) po każdej stronie łuku [m], 0 – bez.</summary>
    public double L { get; set; }
    /// <summary>Przechyłka toru w łuku h [mm] (0 – bez); z „Przejściowe z prędkości” albo wpisana.</summary>
    public double H { get; set; }
    /// <summary>Krzywa przejściowa na wyjściu z łuku [m]; puste – taka sama jak <see cref="L"/> (na wejściu).</summary>
    public double? L2 { get; set; }
    /// <summary>Promień zablokowany – dopasowania („Łuk z punktów”, „Dopasuj promienie”, „Wygładź łuk”) i uchwyt łuku go nie zmieniają.</summary>
    public bool LockR { get; set; }
    /// <summary>Przejściowa na wyjściu (L2 albo L).</summary>
    public double LOut => L2 ?? L;
    public XY P => new(X, Y);
    public UtVertex Clone() => new() { X = X, Y = Y, R = R, L = L, H = H, L2 = L2, LockR = LockR };
}

public enum ElKind { Prosta, Przejsciowa, Luk }

/// <summary>
/// Element trasy w układzie (PUWG 1992). Azymut w radianach od północy w prawo. R ze znakiem: + łuk w prawo, − w lewo.
/// Przejściowa: <see cref="Lx"/> – długość rzutu na styczną, <see cref="Out"/> – z łuku na prostą (wtedy <see cref="Anchor"/> to koniec na prostej).
/// </summary>
public record Element(ElKind Kind, XY Start, double Az0, double Len, double R, double Lx, bool Out, XY Anchor, double AnchorAz, int Vertex,
    double K0 = 0, double K1 = 0, bool Clo = false)
{
    public static XY Dir(double az) => new(Math.Cos(az), Math.Sin(az));
    /// <summary>Normalna w prawo od kierunku.</summary>
    public static XY Right(double az) => new(-Math.Sin(az), Math.Cos(az));

    /// <summary>Punkt i azymut po przejechaniu s [m w układzie] od początku elementu.</summary>
    public (XY P, double Az) At(double s)
    {
        s = Math.Clamp(s, 0, Len);
        switch (Kind)
        {
            case ElKind.Prosta: return (Start + Dir(Az0) * s, Az0);
            case ElKind.Luk:
            {
                var c = Start + Right(Az0) * R; double az = Az0 + s / R;
                return (c - Right(az) * R, az);
            }
            default:
            {
                if (Clo) return (TrasaElem.ClothoidAt(this, s), TrasaElem.ClothoidAz(this, s));    // klotoida trasy z elementów
                double rr = Math.Abs(R), sg = Math.Sign(R);
                if (!Out)
                {
                    double x = Trasa.XofS(s, rr, Lx);
                    return (Start + Dir(Az0) * x + Right(Az0) * (sg * x * x * x / (6 * rr * Lx)), Az0 + sg * Math.Atan(x * x / (2 * rr * Lx)));
                }
                double xb = Trasa.XofS(Len - s, rr, Lx);   // od końca na prostej wstecz
                return (Anchor - Dir(AnchorAz) * xb + Right(AnchorAz) * (sg * xb * xb * xb / (6 * rr * Lx)), AnchorAz - sg * Math.Atan(xb * xb / (2 * rr * Lx)));
            }
        }
    }
    public XY End => At(Len).P;
    public double Az1 => At(Len).Az;
}

/// <summary>Wynik obliczenia trasy toru.</summary>
public class TrasaResult
{
    public List<Element> Elements { get; } = new();
    /// <summary>Uwagi (wierzchołek, tekst) – błąd albo ostrzeżenie.</summary>
    public List<(int Vertex, string Text, bool Error)> Notes { get; } = new();
    /// <summary>Skala odwzorowania dla toru (długości w terenie = w układzie / K).</summary>
    public double K { get; init; } = 1;
    public double LenGrid => Elements.Sum(e => e.Len);
    /// <summary>Długość toru w terenie [m].</summary>
    public double Length => LenGrid / K;
    public bool Ok => Notes.All(n => !n.Error);
    /// <summary>Styczne w wierzchołkach: T (od wierzchołka do początku przejściowej / łuku) [m w układzie].</summary>
    public Dictionary<int, double> Tangent { get; } = new();
    /// <summary>Styczne na wyjściu (od wierzchołka do końca łuku / przejściowej) [m w układzie].</summary>
    public Dictionary<int, double> Tangent2 { get; } = new();

    /// <summary>Punkt i azymut toru w kilometrażu s [m w terenie od początku toru].</summary>
    public (XY P, double Az) At(double s)
    {
        double g = s * K;
        foreach (var e in Elements) { if (g <= e.Len) return e.At(g); g -= e.Len; }
        var last = Elements[^1]; return last.At(last.Len);
    }

    /// <summary>Punkty osi z azymutem: proste – końce, łuki i przejściowe co <paramref name="step"/> [m w układzie].</summary>
    public List<(XY P, double Az)> Sample(double step)
    {
        var res = new List<(XY, double)>();
        foreach (var e in Elements)
        {
            int n = e.Kind == ElKind.Prosta ? 1 : Math.Max(2, (int)Math.Ceiling(e.Len / step));
            for (int i = res.Count == 0 ? 0 : 1; i <= n; i++) res.Add(e.At(e.Len * i / n));
        }
        return res;
    }

    /// <summary>Najbliższy punkt osi: kilometraż [m w terenie] i odległość [m w układzie].</summary>
    public (double S, double Dist) Nearest(XY p, double step = 0.5)
    {
        double best = double.MaxValue, bs = 0, acc = 0;
        foreach (var e in Elements)
        {
            int n = e.Kind == ElKind.Prosta ? 1 : Math.Max(2, (int)Math.Ceiling(e.Len / step));
            var prev = e.At(0).P;
            for (int i = 1; i <= n; i++)
            {
                double s1 = e.Len * (i - 1) / n, s2 = e.Len * i / n; var cur = e.At(s2).P;
                var d = cur - prev; double l2 = d.X * d.X + d.Y * d.Y;
                double t = l2 > 0 ? Math.Clamp(((p.X - prev.X) * d.X + (p.Y - prev.Y) * d.Y) / l2, 0, 1) : 0;
                var q = prev + d * t; double dist = XY.Dist(p, q);
                if (dist < best) { best = dist; bs = acc + s1 + (s2 - s1) * t; }
                prev = cur;
            }
            acc += e.Len;
        }
        return (bs / K, best);
    }
}

/// <summary>
/// Obliczenie trasy z wieloboku stycznych. W wierzchołku i o kącie zwrotu α, promieniu R i przejściowej L (parabola 3. stopnia
/// y = x³ / (6·R·L), x wzdłuż stycznej): kąt przejściowej τ = atan(L / 2R), koniec E = (L, L² / 6R) leży na okręgu R
/// o stycznej τ – środek okręgu w (p, q) = (L − R·sin τ, L² / 6R + R·cos τ) od początku przejściowej; środek leży na dwusiecznej,
/// więc styczna T = p + q·tg(α / 2), a łuk kołowy ma kąt α − 2τ. Bez przejściowej: T = R·tg(α / 2).
/// R i L wpisuje się w terenie; w układzie mnoży się je przez skalę odwzorowania K (długości w terenie = w układzie / K).
/// </summary>
public static class Trasa
{
    /// <summary>Długość łuku paraboli 3. stopnia od 0 do x (Simpson).</summary>
    public static double SofX(double x, double r, double lx)
    {
        if (x <= 0) return 0;
        const int n = 32; double h = x / n, sum = 0;
        for (int i = 0; i <= n; i++)
        {
            double t = i * h, d = t * t / (2 * r * lx), f = Math.Sqrt(1 + d * d);
            sum += f * (i == 0 || i == n ? 1 : i % 2 == 1 ? 4 : 2);
        }
        return sum * h / 3;
    }
    /// <summary>x paraboli dla długości łuku s (Newton).</summary>
    public static double XofS(double s, double r, double lx)
    {
        if (s <= 0) return 0;
        double x = s;
        for (int i = 0; i < 30; i++)
        {
            double d = x * x / (2 * r * lx), f = SofX(x, r, lx) - s;
            x -= f / Math.Sqrt(1 + d * d);
            if (Math.Abs(f) < 1e-10) break;
        }
        return x;
    }

    public static double Az(XY a, XY b) => Math.Atan2(b.Y - a.Y, b.X - a.X);
    static double Norm(double a) { while (a > Math.PI) a -= 2 * Math.PI; while (a <= -Math.PI) a += 2 * Math.PI; return a; }

    /// <summary>Skala odwzorowania toru (w środku wieloboku).</summary>
    public static double KFor(UtTrack t)
    {
        if (t.Vertices.Count == 0) return 1;
        var v = t.Vertices; var m = (v[0].P + v[^1].P) * 0.5;
        return Geo.InPoland(m) ? Geo.ScaleFactor(m) : 1;
    }

    public static TrasaResult Compute(UtTrack t) => t.Align != null ? TrasaElem.Compute(t.Align, KFor(t)) : Compute(t.Vertices, KFor(t));

    public static TrasaResult Compute(IReadOnlyList<UtVertex> v, double k)
    {
        var res = new TrasaResult { K = k };
        // powtórzone punkty (podwójny klik) – pominięte
        var pts = new List<(UtVertex V, int I)>();
        for (int i = 0; i < v.Count; i++) if (pts.Count == 0 || XY.Dist(pts[^1].V.P, v[i].P) > 1e-6) pts.Add((v[i], i));
        if (pts.Count < 2) { res.Notes.Add((0, "tor musi mieć co najmniej 2 różne punkty", true)); return res; }

        int n = pts.Count;
        var cur = pts[0].V.P; double curAz = Az(pts[0].V.P, pts[1].V.P);
        for (int j = 1; j < n; j++)
        {
            var vj = pts[j].V; int vi = pts[j].I;
            double azIn = Az(pts[j - 1].V.P, vj.P);
            // wierzchołek wewnętrzny: łuk z przejściowymi
            List<Element> curve = null; XY tsPoint = vj.P, stPoint = vj.P; double azOut = azIn;
            if (j < n - 1)
            {
                azOut = Az(vj.P, pts[j + 1].V.P);
                double alpha = Norm(azOut - azIn), aa = Math.Abs(alpha), sg = Math.Sign(alpha);
                if (aa > 1e-9 && vj.R > 0)
                {
                    // przejściowe na wejściu L1 i na wyjściu L2 (L2 puste – jak L1): środek okręgu w odległości q1 od prostej wejściowej
                    // i q2 od wyjściowej; styczne T1 = m1 + (q2 − q1·cos α) / sin α, T2 = m2 + (q1 − q2·cos α) / sin α (przy L1 = L2: m + q·tg(α/2))
                    double R = vj.R * k, L = vj.L * k, L2 = vj.LOut * k;
                    double tau = L > 0 ? Math.Atan(L / (2 * R)) : 0, tau2 = L2 > 0 ? Math.Atan(L2 / (2 * R)) : 0;
                    if (aa < tau + tau2 - 1e-12)
                        res.Notes.Add((vi, $"krzywe przejściowe L = {vj.L:0.##}" + (vj.L2 != null ? $" / {vj.LOut:0.##}" : "") + $" m za długie dla kąta zwrotu {aa * 180 / Math.PI:0.###}° przy R = {vj.R:0.##} m", true));
                    else
                    {
                        double m1 = L > 0 ? L - R * Math.Sin(tau) : 0, q1 = L > 0 ? L * L / (6 * R) + R * Math.Cos(tau) : R;
                        double m2 = L2 > 0 ? L2 - R * Math.Sin(tau2) : 0, q2 = L2 > 0 ? L2 * L2 / (6 * R) + R * Math.Cos(tau2) : R;
                        double sa = Math.Sin(aa), ca = Math.Cos(aa);
                        double T = m1 + (q2 - q1 * ca) / sa, T2 = m2 + (q1 - q2 * ca) / sa;
                        res.Tangent[vi] = T; res.Tangent2[vi] = T2;
                        tsPoint = vj.P - Element.Dir(azIn) * T; stPoint = vj.P + Element.Dir(azOut) * T2;
                        curve = new();
                        var at = tsPoint; double az = azIn;
                        if (L > 0)
                        {
                            var e1 = new Element(ElKind.Przejsciowa, at, az, SofX(L, R, L), sg * R, L, false, default, 0, vi);
                            curve.Add(e1); (at, az) = e1.At(e1.Len);
                        }
                        double arcAng = aa - tau - tau2;
                        if (arcAng > 1e-12) { var e2 = new Element(ElKind.Luk, at, az, R * arcAng, sg * R, 0, false, default, 0, vi); curve.Add(e2); (at, az) = e2.At(e2.Len); }
                        if (L2 > 0) curve.Add(new Element(ElKind.Przejsciowa, at, az, SofX(L2, R, L2), sg * R, L2, true, stPoint, azOut, vi));
                    }
                }
                else if (aa > 0.1 * Math.PI / 180)
                    res.Notes.Add((vi, $"załamanie {aa * 180 / Math.PI:0.##}° bez łuku – wpisz promień R", false));
            }
            // prosta od bieżącego punktu do początku łuku (albo końca toru)
            double len = (tsPoint.X - cur.X) * Math.Cos(azIn) + (tsPoint.Y - cur.Y) * Math.Sin(azIn);
            // tolerancja 1 mm: wymiary katalogowe a, b są zaokrąglone (np. Rz 500 1:12: a = 20,797, styczna R·tg(α/2) = 20,7973)
            if (len < -1e-3)
                res.Notes.Add((vi, $"łuk w wierzchołku nie mieści się – brakuje {(-len) / k:0.##} m prostej przed nim (zmniejsz R lub L albo odsuń wierzchołki)", true));
            if (len > 1e-9) res.Elements.Add(new Element(ElKind.Prosta, cur, azIn, len, 0, 0, false, default, 0, vi));
            if (curve != null) { res.Elements.AddRange(curve); cur = stPoint; curAz = azOut; }
            else { cur = tsPoint; curAz = azOut; }
        }
        return res;
    }

    /// <summary>
    /// Tor równoległy w odległości d [m w terenie] (+ po prawej, − po lewej względem kierunku toru): proste przesunięte,
    /// wierzchołki na przecięciu przesuniętych stycznych, łuki współśrodkowe (R ∓ d), te same przejściowe.
    /// </summary>
    public static List<UtVertex> Offset(UtTrack t, double d)
    {
        var v = t.Vertices; double k = KFor(t), dg = d * k;
        var res = new List<UtVertex>();
        int n = v.Count;
        for (int i = 0; i < n; i++)
        {
            XY p;
            if (i == 0) p = v[0].P + Element.Right(Az(v[0].P, v[1].P)) * dg;
            else if (i == n - 1) p = v[i].P + Element.Right(Az(v[i - 1].P, v[i].P)) * dg;
            else
            {
                double a1 = Az(v[i - 1].P, v[i].P), a2 = Az(v[i].P, v[i + 1].P), half = Norm(a2 - a1) / 2;
                // przecięcie przesuniętych stycznych: na dwusiecznej, w odległości d / cos(α/2)
                p = v[i].P + Element.Right(a1 + half) * (dg / Math.Cos(half));
            }
            double r = v[i].R;
            if (r > 0 && i > 0 && i < n - 1)
            {
                double alpha = Norm(Az(v[i].P, v[i + 1].P) - Az(v[i - 1].P, v[i].P));
                r = Math.Max(1, r - Math.Sign(alpha) * d);   // łuk w prawo, tor po prawej – promień mniejszy
            }
            res.Add(new UtVertex { X = p.X, Y = p.Y, R = r, L = v[i].L });
        }
        return res;
    }
}

namespace Lcs.Model;

/// <summary>Kawałek wykresu krzywizny: km od–do [m w terenie], krzywizna na początku i na końcu [1/m, + w prawo], element osi.</summary>
public record KrzywiznaOdc(double S0, double S1, double K0, double K1, ElKind Kind, int Index);

/// <summary>Uwaga do łuku na wykresie krzywizny i w „Sprawdź”: km [m w terenie], tekst.</summary>
public record KrzywiznaUwaga(double S, string Text);

/// <summary>
/// Wykres krzywizny toru (krzywizna od kilometrażu) i kontrola łuków: skok krzywizny bez krzywej przejściowej, za krótka prosta
/// między łukami (warunki techniczne § 36, tabl. 3.15: tory główne Vmax / 2,5, nie mniej niż 20 m; pozostałe 10 m), promień za mały
/// dla prędkości, krzywa przejściowa krótsza niż wynika z prędkości (wzory kalkulatora przechyłki, wartości dopuszczalne przykładowe).
/// </summary>
public static class Krzywizna
{
    /// <summary>Krzywizna wzdłuż toru: dla każdego elementu osi krzywizna na początku i na końcu (w terenie).</summary>
    public static List<KrzywiznaOdc> Profile(TrasaResult r)
    {
        var res = new List<KrzywiznaOdc>(); double acc = 0;
        foreach (var e in r.Elements)
        {
            double s0 = acc / r.K, s1 = (acc + e.Len) / r.K; acc += e.Len;
            double kk = e.R == 0 ? 0 : r.K / e.R;           // krzywizna w terenie
            var (k0, k1) = e.Kind switch
            {
                ElKind.Prosta => (0.0, 0.0),
                ElKind.Luk => (kk, kk),
                _ when e.Clo => (e.K0 * r.K, e.K1 * r.K),
                _ => e.Out ? (kk, 0.0) : (0.0, kk),          // parabola 3. stopnia – krzywizna prawie liniowo od 0 do 1/R
            };
            res.Add(new KrzywiznaOdc(s0, s1, k0, k1, e.Kind, e.Vertex));
        }
        return res;
    }

    static bool Main(UtTrack t) => t.Cat is UtTrack.MainZ or UtTrack.MainD;

    /// <summary>Uwagi do łuków toru.</summary>
    public static List<KrzywiznaUwaga> Check(UtTrack t, TrasaResult r = null)
    {
        r ??= Trasa.Compute(t);
        var p = Profile(r); var res = new List<KrzywiznaUwaga>();
        double v = t.Vmax; bool main = Main(t);
        string R(double k) => Math.Abs(k) < 1e-9 ? "prosta" : $"R {1 / Math.Abs(k):0}";
        // 1. skok krzywizny (tory główne albo V > 40 km/h)
        if (main || v > 40)
            for (int i = 1; i < p.Count; i++)
                if (Math.Abs(p[i - 1].K1 - p[i].K0) > 1e-5)
                    res.Add(new(p[i].S0, $"skok krzywizny przy km {p[i].S0:0.0} ({R(p[i - 1].K1)} → {R(p[i].K0)}) bez krzywej przejściowej"));
        // 2. prosta między łukami
        double min = main ? Math.Max(20, v / 2.5) : 10;
        for (int i = 1; i < p.Count - 1; i++)
        {
            if (p[i].Kind != ElKind.Prosta || p[i - 1].Kind == ElKind.Prosta || p[i + 1].Kind == ElKind.Prosta) continue;
            double len = p[i].S1 - p[i].S0;
            if (len > 0.01 && len < min - 0.01)
            {
                bool odwrotne = Math.Sign(p[i - 1].K1 != 0 ? p[i - 1].K1 : p[i - 1].K0) != Math.Sign(p[i + 1].K0 != 0 ? p[i + 1].K0 : p[i + 1].K1);
                res.Add(new(p[i].S0, $"prosta między łukami{(odwrotne ? " odwrotnymi" : "")} {len:0.0} m krótsza od {min:0} m (warunki techniczne § 36{(main ? $", tor główny: V/2,5, nie mniej niż 20 m" : ", tory pozostałe: 10 m")})"));
            }
        }
        // 3–4. promień i przejściowa wobec prędkości
        if (v > 0)
        {
            var d = new Dopuszczalne();
            for (int i = 0; i < p.Count; i++)
            {
                if (p[i].Kind != ElKind.Luk || Math.Abs(p[i].K0) < 1e-9) continue;
                double rr = 1 / Math.Abs(p[i].K0);
                var w = Obliczenia.Licz(v, rr, null, d);
                if (w.VMax < v - 0.5) res.Add(new((p[i].S0 + p[i].S1) / 2, $"łuk R {rr:0} m: za mały promień dla {v:0} km/h (największa prędkość ≈ {w.VMax:0} km/h przy h ≤ {d.HMax:0} mm)"));
                double lin = i > 0 && p[i - 1].Kind == ElKind.Przejsciowa ? p[i - 1].S1 - p[i - 1].S0 : 0;
                double lout = i + 1 < p.Count && p[i + 1].Kind == ElKind.Przejsciowa ? p[i + 1].S1 - p[i + 1].S0 : 0;
                if (Math.Min(lin, lout) < w.L - 0.5)
                    res.Add(new(p[i].S0, $"łuk R {rr:0} m: krzywe przejściowe {lin:0} / {lout:0} m krótsze niż wynika z {v:0} km/h ({w.L:0} m, przechyłka {w.H:0} mm – wartości dopuszczalne przykładowe)"));
            }
        }
        return res.OrderBy(x => x.S).ToList();
    }
}

namespace Lcs.Przechylka;

/// <summary>
/// Wartości dopuszczalne do obliczeń. Domyślne są PRZYKŁADOWE (nie pochodzą ze źródeł projektu) – projektant wpisuje je
/// z obowiązujących przepisów (warunki techniczne dla budowli kolejowych, Id-1).
/// </summary>
public class Dopuszczalne
{
    /// <summary>Największa przechyłka [mm].</summary>
    public double HMax { get; set; } = 150;
    /// <summary>Największy niedobór przechyłki [mm].</summary>
    public double HnMax { get; set; } = 110;
    /// <summary>Szybkość przyrostu przyspieszenia niezrównoważonego ψ [m/s³].</summary>
    public double Psi { get; set; } = 0.5;
    /// <summary>Szybkość zmiany przechyłki f [mm/s].</summary>
    public double F { get; set; } = 35;
    /// <summary>Pochylenie rampy przechyłkowej 1:n, n = <see cref="NFactor"/>·V.</summary>
    public double NFactor { get; set; } = 10;
    /// <summary>Zaokrąglenie długości w górę [m].</summary>
    public double Round { get; set; } = 5;
    /// <summary>Prześwit toru (rozstaw szyn) [mm] – normalnotorowy 1435.</summary>
    public double Gauge { get; set; } = 1435;
    /// <summary>Szerokość główki szyny [mm] – rozstaw osi główek s = prześwit + główka.</summary>
    public double Head { get; set; } = 65;
    /// <summary>Rozstaw osi główek szyn s [mm] – na tę odległość działa przechyłka (1435 + 65 = 1500).</summary>
    public double S => Gauge + Head;
}

/// <summary>Wynik obliczeń przechyłki i długości krzywej przejściowej.</summary>
public record Wynik(
    double Hr, double H, bool HProponowana, double HMin, double Hn, double An,
    double LRampa, double LPsi, double LF, double L, string Decyduje,
    double VMax, List<string> Uwagi);

/// <summary>
/// Przechyłka i krzywa przejściowa. Prześwit toru 1435 mm; przechyłka działa na rozstawie osi główek szyn s = 1435 + 65 = 1500 mm:
/// h_r = s·V² / (g·3,6²·R) [mm] (dla s = 1500: 11,8·V²/R); niedobór h_n = h_r − h; a_n = g·h_n / s [m/s²];
/// L = max(L_rampa = n·h / 1000, n = k·V; L_ψ = V·|a_n| / (3,6·ψ); L_f = V·h / (3,6·f)), zaokrąglone w górę.
/// Bez podanej h – proponowana przechyłka równoważąca h_r zaokrąglona do 5 mm (najwyżej h_max); dodatkowo podawana najmniejsza
/// przechyłka, przy której niedobór nie przekracza dopuszczalnego.
/// </summary>
public static class Obliczenia
{
    public const double G = 9.81;

    public static double Hr(double v, double r, double s = 1500) => s * v * v / (G * 3.6 * 3.6 * r);

    public static Wynik Licz(double v, double r, double? hZad, Dopuszczalne d)
    {
        var uwagi = new List<string>();
        double hr = Hr(v, r, d.S);
        bool prop = hZad == null;
        double hmin = Math.Ceiling(Math.Max(0, hr - d.HnMax) / 5) * 5;
        double h = hZad ?? Math.Min(Math.Round(hr / 5) * 5, d.HMax);
        if (hmin > d.HMax) uwagi.Add($"promień za mały dla {v:0} km/h – nawet przy h = {d.HMax:0} mm niedobór przekracza {d.HnMax:0} mm");
        if (!prop && h > d.HMax) uwagi.Add($"przechyłka {h:0} mm większa od dopuszczalnej {d.HMax:0} mm");
        double hn = hr - h;
        if (hn > d.HnMax + 1e-9) uwagi.Add($"niedobór przechyłki {hn:0} mm większy od dopuszczalnego {d.HnMax:0} mm");
        if (hn < 0) uwagi.Add($"nadmiar przechyłki {-hn:0} mm (h większa od równoważącej)");
        double an = G * hn / d.S;
        double lr = d.NFactor * v * h / 1000, lp = d.Psi > 0 ? v * Math.Abs(an) / (3.6 * d.Psi) : 0, lf = d.F > 0 ? v * h / (3.6 * d.F) : 0;
        double lmax = Math.Max(lr, Math.Max(lp, lf));
        string dec = lmax == lr ? "rampa przechyłkowa" : lmax == lp ? "przyrost przyspieszenia ψ" : "zmiana przechyłki f";
        double l = d.Round > 0 ? Math.Ceiling(lmax / d.Round - 1e-9) * d.Round : lmax;
        double vmax = Math.Sqrt((Math.Min(h, d.HMax) + d.HnMax) * r * G * 3.6 * 3.6 / d.S);
        return new Wynik(hr, h, prop, Math.Min(hmin, d.HMax), hn, an, lr, lp, lf, l, dec, vmax, uwagi);
    }
}

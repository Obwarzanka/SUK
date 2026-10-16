using System.Globalization;
using System.Text.RegularExpressions;

namespace Lcs.Model;

/// <summary>
/// Układ współrzędnych PUWG 1992 (EPSG:2180, odwzorowanie Gaussa-Krügera na elipsoidzie GRS80, południk 19°, k0 = 0,9993).
/// Konwencja geodezyjna: <b>X – na północ, Y – na wschód</b> (jak na mapach geodezyjnych i w usługach Geoportalu).
/// Przeliczenie szeregami Krügera (rząd n⁴) – dokładność dla Polski poniżej 0,1 mm.
/// </summary>
public static class Geo
{
    const double A = 6378137.0, F = 1 / 298.257222101, K0 = 0.9993, FalseY = 500000, FalseX = -5300000;
    static readonly double Lon0 = 19 * Math.PI / 180;
    static readonly double N = F / (2 - F), E = Math.Sqrt(F * (2 - F));
    static readonly double Ar = A / (1 + N) * (1 + N * N / 4 + Math.Pow(N, 4) / 64);
    static readonly double N2 = N * N, N3 = N2 * N, N4 = N3 * N;
    // współczynniki szeregów Krügera do n⁴ (Karney 2011, wzory 35 i 36)
    static readonly double[] Alpha = { N / 2 - 2 * N2 / 3 + 5 * N3 / 16 + 41 * N4 / 180, 13 * N2 / 48 - 3 * N3 / 5 + 557 * N4 / 1440, 61 * N3 / 240 - 103 * N4 / 140, 49561 * N4 / 161280 };
    static readonly double[] Beta = { N / 2 - 2 * N2 / 3 + 37 * N3 / 96 - N4 / 360, N2 / 48 + N3 / 15 - 437 * N4 / 1440, 17 * N3 / 480 - 37 * N4 / 840, 4397 * N4 / 161280 };
    static readonly double[] Delta = { 2 * N - 2 * N2 / 3 - 2 * N3 + 116 * N4 / 45, 7 * N2 / 3 - 8 * N3 / 5 - 227 * N4 / 45, 56 * N3 / 15 - 136 * N4 / 35, 4279 * N4 / 630 };

    /// <summary>WGS84 (stopnie) → PUWG 1992 [m]: X – północ, Y – wschód.</summary>
    public static XY ToPuwg92(double lat, double lon)
    {
        double phi = lat * Math.PI / 180, dl = lon * Math.PI / 180 - Lon0;
        double t = Math.Sinh(Math.Atanh(Math.Sin(phi)) - E * Math.Atanh(E * Math.Sin(phi)));
        double xi = Math.Atan2(t, Math.Cos(dl)), eta = Math.Atanh(Math.Sin(dl) / Math.Sqrt(1 + t * t));
        double x = xi, y = eta;
        for (int j = 0; j < 4; j++)
        {
            x += Alpha[j] * Math.Sin(2 * (j + 1) * xi) * Math.Cosh(2 * (j + 1) * eta);
            y += Alpha[j] * Math.Cos(2 * (j + 1) * xi) * Math.Sinh(2 * (j + 1) * eta);
        }
        return new XY(FalseX + K0 * Ar * x, FalseY + K0 * Ar * y);
    }

    /// <summary>PUWG 1992 [m] → WGS84 (stopnie).</summary>
    public static (double Lat, double Lon) ToWgs84(XY p)
    {
        double xi = (p.X - FalseX) / (K0 * Ar), eta = (p.Y - FalseY) / (K0 * Ar);
        double x = xi, y = eta;
        for (int j = 0; j < 4; j++)
        {
            x -= Beta[j] * Math.Sin(2 * (j + 1) * xi) * Math.Cosh(2 * (j + 1) * eta);
            y -= Beta[j] * Math.Cos(2 * (j + 1) * xi) * Math.Sinh(2 * (j + 1) * eta);
        }
        double chi = Math.Asin(Math.Sin(x) / Math.Cosh(y));
        double phi = chi;
        for (int j = 0; j < 4; j++) phi += Delta[j] * Math.Sin(2 * (j + 1) * chi);
        double lon = Lon0 + Math.Atan2(Math.Sinh(y), Math.Cos(x));
        return (phi * 180 / Math.PI, lon * 180 / Math.PI);
    }

    /// <summary>
    /// Skala odwzorowania w punkcie: odległość w układzie = k · odległość na elipsoidzie.
    /// Na południku 19° k = 0,9993 (−70 cm/km), na krańcach Polski do ok. 1,0009.
    /// </summary>
    public static double ScaleFactor(XY p)
    {
        var (lat, _) = ToWgs84(p);
        double phi = lat * Math.PI / 180, s = Math.Sin(phi), w = 1 - F * (2 - F) * s * s;
        double r2 = A * A * (1 - F * (2 - F)) / (w * w);           // R² = M·N (średni promień krzywizny)
        double y = (p.Y - FalseY) / K0, q = y * y / r2;
        return K0 * (1 + q / 2 + q * q / 24);
    }

    /// <summary>
    /// Długość odcinka w terenie [m] – odległość w układzie podzielona przez skalę odwzorowania (Simpson: końce i środek)
    /// i sprowadzona na wysokość h [m n.p.m.] (bez wysokości – na elipsoidzie; 200 m n.p.m. to +3 cm/km).
    /// </summary>
    public static double GroundDistance(XY a, XY b, double h = 0)
    {
        double grid = XY.Dist(a, b);
        if (grid == 0) return 0;
        double k = (ScaleFactor(a) + 4 * ScaleFactor((a + b) * 0.5) + ScaleFactor(b)) / 6;
        return grid / k * (1 + h / 6371000);
    }

    /// <summary>Odległość geodezyjna na elipsoidzie GRS80 (Vincenty) [m] – do sprawdzania <see cref="GroundDistance"/>.</summary>
    public static double GeodesicDistance(double lat1, double lon1, double lat2, double lon2)
    {
        double b = A * (1 - F), L = (lon2 - lon1) * Math.PI / 180;
        double U1 = Math.Atan((1 - F) * Math.Tan(lat1 * Math.PI / 180)), U2 = Math.Atan((1 - F) * Math.Tan(lat2 * Math.PI / 180));
        double sU1 = Math.Sin(U1), cU1 = Math.Cos(U1), sU2 = Math.Sin(U2), cU2 = Math.Cos(U2);
        double lam = L, sinS = 0, cosS = 0, sig = 0, cos2a = 0, cos2sm = 0;
        for (int i = 0; i < 200; i++)
        {
            double sl = Math.Sin(lam), cl = Math.Cos(lam);
            sinS = Math.Sqrt(Math.Pow(cU2 * sl, 2) + Math.Pow(cU1 * sU2 - sU1 * cU2 * cl, 2));
            if (sinS == 0) return 0;
            cosS = sU1 * sU2 + cU1 * cU2 * cl; sig = Math.Atan2(sinS, cosS);
            double sa = cU1 * cU2 * sl / sinS; cos2a = 1 - sa * sa;
            cos2sm = cos2a != 0 ? cosS - 2 * sU1 * sU2 / cos2a : 0;
            double C = F / 16 * cos2a * (4 + F * (4 - 3 * cos2a));
            double prev = lam;
            lam = L + (1 - C) * F * sa * (sig + C * sinS * (cos2sm + C * cosS * (-1 + 2 * cos2sm * cos2sm)));
            if (Math.Abs(lam - prev) < 1e-13) break;
        }
        double u2 = cos2a * (A * A - b * b) / (b * b);
        double Ak = 1 + u2 / 16384 * (4096 + u2 * (-768 + u2 * (320 - 175 * u2))), Bk = u2 / 1024 * (256 + u2 * (-128 + u2 * (74 - 47 * u2)));
        double ds = Bk * sinS * (cos2sm + Bk / 4 * (cosS * (-1 + 2 * cos2sm * cos2sm) - Bk / 6 * cos2sm * (-3 + 4 * sinS * sinS) * (-3 + 4 * cos2sm * cos2sm)));
        return b * Ak * (sig - ds);
    }

    /// <summary>Azymut odcinka w układzie (od północy osi X, w prawo) [°], 0–360.</summary>
    public static double Azimuth(XY a, XY b) { var d = Math.Atan2(b.Y - a.Y, b.X - a.X) * 180 / Math.PI; return d < 0 ? d + 360 : d; }

    /// <summary>Obszar, na którym PUWG 1992 ma sens (Polska z zapasem).</summary>
    public static bool InPoland(XY p) => p.Y > 140000 && p.Y < 900000 && p.X > 100000 && p.X < 800000;

    /// <summary>
    /// Punkt wpisany przez użytkownika: „51.7631, 19.5438” (Google Maps, WGS84) albo „X Y” w PUWG 1992
    /// (np. „384120 537450”, kolejność geodezyjna: najpierw X – północ).
    /// </summary>
    public static bool TryParsePoint(string s, out XY p)
    {
        p = default;
        var m = Regex.Matches(s ?? "", @"[-+]?\d+(?:[.,]\d+)?");
        if (m.Count < 2) return false;
        if (!Num(m[0].Value, out var a) || !Num(m[1].Value, out var b)) return false;
        if (Math.Abs(a) <= 90 && Math.Abs(b) <= 180) { p = ToPuwg92(a, b); return true; }
        p = new XY(a, b);
        return InPoland(p);
    }

    static bool Num(string v, out double d) => double.TryParse(v.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out d);

    static readonly CultureInfo Pl = CultureInfo.GetCultureInfo("pl-PL");
    /// <summary>Metry z odstępem tysięcy: „384 120,45”.</summary>
    public static string M(double v, int dec = 2) => v.ToString("#,0." + new string('0', dec), Pl).Replace(' ', ' ');
}

/// <summary>Punkt w PUWG 1992 [m]: X – północ, Y – wschód.</summary>
public readonly record struct XY(double X, double Y)
{
    public static XY operator +(XY a, XY b) => new(a.X + b.X, a.Y + b.Y);
    public static XY operator -(XY a, XY b) => new(a.X - b.X, a.Y - b.Y);
    public static XY operator *(XY a, double k) => new(a.X * k, a.Y * k);
    public double Length => Math.Sqrt(X * X + Y * Y);
    public static double Dist(XY a, XY b) => (a - b).Length;
}

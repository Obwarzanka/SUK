using System.Globalization;

namespace Lcs.Model;

/// <summary>
/// Położenie obrazu: piksel (kolumna c, wiersz r; (0, 0) – lewy górny róg lewego górnego piksela) → PUWG 1992:
///   Y (wschód) = A·c + B·r + C,   X (północ) = D·c + E·r + F.
/// Obraz bez obrotu: A = m/px, E = −m/px, B = D = 0.
/// </summary>
public readonly record struct Affine(double A, double B, double C, double D, double E, double F)
{
    public static Affine NorthUp(double topX, double leftY, double mpp) => new(mpp, 0, leftY, 0, -mpp, topX);

    public XY World(double col, double row) => new(D * col + E * row + F, A * col + B * row + C);
    public double Det => A * E - B * D;
    public (double Col, double Row) Pixel(XY p)
    {
        double y = p.Y - C, x = p.X - F, det = Det;
        return ((E * y - B * x) / det, (A * x - D * y) / det);
    }
    /// <summary>Średni rozmiar piksela [m].</summary>
    public double Mpp => Math.Sqrt(Math.Abs(Det));
    /// <summary>Obrót obrazu względem północy [°], dodatni – w prawo (zgodnie z ruchem wskazówek).</summary>
    public double RotationDeg => Math.Atan2(-D, A) * 180 / Math.PI;
    public bool Valid => double.IsFinite(A + B + C + D + E + F) && Math.Abs(Det) > 1e-18;

    /// <summary>To samo położenie po przekształceniu świata: obrót o kąt [°, w prawo] i skala wokół punktu, potem przesunięcie.</summary>
    public Affine Then(XY pivot, double rotDeg, double scale, XY shift)
    {
        double t = -rotDeg * Math.PI / 180, cs = Math.Cos(t) * scale, sn = Math.Sin(t) * scale;
        // w układzie (wschód, północ): obrót w prawo o rotDeg = obrót matematyczny o −rotDeg; wprost na współczynnikach (bez odejmowania dużych współrzędnych)
        double de = C - pivot.Y, dn = F - pivot.X;
        return new Affine(cs * A - sn * D, cs * B - sn * E, pivot.Y + cs * de - sn * dn + shift.Y,
                          sn * A + cs * D, sn * B + cs * E, pivot.X + sn * de + cs * dn + shift.X);
    }

    /// <summary>
    /// Plik world (.pgw / .jgw / .tfw / .wld): A, D, B, E, C, F – C, F to <b>środek</b> lewego górnego piksela, x = wschód, y = północ.
    /// </summary>
    public static Affine FromWorldFile(string[] lines)
    {
        var v = lines.Where(l => l.Trim().Length > 0).Take(6).Select(l => double.Parse(l.Trim().Replace(',', '.'), CultureInfo.InvariantCulture)).ToArray();
        if (v.Length < 6) throw new FormatException("Plik world musi mieć 6 liczb");
        double a = v[0], d = v[1], b = v[2], e = v[3], c = v[4], f = v[5];
        return new Affine(a, b, c - a / 2 - b / 2, d, e, f - d / 2 - e / 2);
    }

    /// <summary>
    /// Dopasowanie podobieństwem (przesunięcie, obrót, jedna skala – obraz nie jest zniekształcany) metodą najmniejszych kwadratów.
    /// Obraz ma wiersze w dół, a świat północ w górę – dopasowanie uwzględnia to odbicie.
    /// </summary>
    public static Affine FitSimilarity(IReadOnlyList<UtCalPoint> pts)
    {
        if (pts.Count < 2) throw new ArgumentException("Potrzebne co najmniej 2 punkty");
        // u = kolumna, v = −wiersz; E = a·u − b·v + tE, N = b·u + a·v + tN
        double mu = pts.Average(p => p.Col), mv = pts.Average(p => -p.Row), mE = pts.Average(p => p.Y), mN = pts.Average(p => p.X);
        double s = 0, sa = 0, sb = 0;
        foreach (var p in pts)
        {
            double u = p.Col - mu, v = -p.Row - mv, e = p.Y - mE, n = p.X - mN;
            s += u * u + v * v; sa += u * e + v * n; sb += u * n - v * e;
        }
        if (s < 1e-12) throw new ArgumentException("Punkty obrazu się pokrywają");
        double a = sa / s, b = sb / s;
        double tE = mE - (a * mu - b * mv), tN = mN - (b * mu + a * mv);
        // E = a·c + b·r + tE, N = b·c − a·r + tN
        return new Affine(a, b, tE, b, -a, tN);
    }

    /// <summary>Dopasowanie afiniczne (6 parametrów) metodą najmniejszych kwadratów – od 3 punktów nie na jednej prostej.</summary>
    public static Affine FitAffine(IReadOnlyList<UtCalPoint> pts)
    {
        if (pts.Count < 3) throw new ArgumentException("Potrzebne co najmniej 3 punkty");
        double mc = pts.Average(p => p.Col), mr = pts.Average(p => p.Row), mE = pts.Average(p => p.Y), mN = pts.Average(p => p.X);
        double cc = 0, cr = 0, rr = 0, ce = 0, re = 0, cn = 0, rn = 0;
        foreach (var p in pts)
        {
            double c = p.Col - mc, r = p.Row - mr, e = p.Y - mE, n = p.X - mN;
            cc += c * c; cr += c * r; rr += r * r; ce += c * e; re += r * e; cn += c * n; rn += r * n;
        }
        double det = cc * rr - cr * cr;
        if (Math.Abs(det) < 1e-9 * Math.Max(1, cc * rr)) throw new ArgumentException("Punkty obrazu leżą na jednej prostej");
        double A = (ce * rr - re * cr) / det, B = (re * cc - ce * cr) / det;
        double D = (cn * rr - rn * cr) / det, E = (rn * cc - cn * cr) / det;
        return new Affine(A, B, mE - A * mc - B * mr, D, E, mN - D * mc - E * mr);
    }

    /// <summary>Dopasowanie wg trybu grafiki; null – za mało punktów.</summary>
    public static Affine? Fit(IReadOnlyList<UtCalPoint> pts, string mode) =>
        mode == UtBackground.AffineMode ? (pts.Count >= 3 ? FitAffine(pts) : null) : (pts.Count >= 2 ? FitSimilarity(pts) : null);
}

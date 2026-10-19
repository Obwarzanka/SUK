namespace Lcs.Model;

/// <summary>
/// Rozjazdy krzyżowe i skrzyżowania torów w zobrazowaniu i w symulatorze (decyzje właściciela 2026-10-19, ROZWOJ.md etap 9):
/// <list type="bullet">
/// <item>Rkpd i Rkp – dwie zwrotnice na końcach rozjazdu zwrócone do siebie ostrzami (Ie-104 § 8 pkt 9: obraz jednakowy dla rozjazdów
/// pełnych i niepełnych; jak 17ab / 17cd w układzie Widzewa): „ab” na końcu od mniejszego km toru A, „cd” na drugim; łączy je
/// wewnętrzny kawałek toru A, kawałka toru B wewnątrz rozjazdu nie rysuje się i nie ma go w symulatorze. Rkp ma jeden przejazd –
/// komputer zależności nie dopuszcza jazdy przez brakujący (<see cref="BrakPrzejazdu"/>; decyzja właściciela 2026-10-19);</item>
/// <item>St – skrzyżowanie: jeden węzeł na obu torach, przez krzyż tylko na wprost (Ie-104 § 8 pkt 2).</item>
/// </list>
/// Symbol końca na obrazie ma id złożone „id skrzyżowania:ab” / „…:cd”.
/// </summary>
public static class Krzyzowe
{
    public const string Ab = "ab", Cd = "cd";
    public static readonly string[] Konce = { Ab, Cd };

    public static string Id(string crossing, string koniec) => $"{crossing}:{koniec}";

    /// <summary>Id symbolu końca rozjazdu krzyżowego → skrzyżowanie i koniec (ab / cd); null – to nie koniec rozjazdu krzyżowego.</summary>
    public static (UtCrossing C, string Koniec)? Parse(UtProject ut, string id)
    {
        if (id == null || id.Length < 4 || id[^3] != ':') return null;
        var k = id[^2..]; if (k is not (Ab or Cd)) return null;
        var c = ut.Crossings.FirstOrDefault(x => x.Id == id[..^3]);
        return c != null && Rodzaj(c) != RozjazdTyp.St ? (c, k) : null;
    }

    /// <summary>Rodzaj: Rkp, Rkpd albo St – z katalogu, a dla typu spoza katalogu – z początku oznaczenia.</summary>
    public static string Rodzaj(UtCrossing c)
    {
        if (Katalog.Find(c.Type) is RozjazdTyp t && t.IsCrossing) return t.Kind;
        var n = c.Type ?? "";
        return n.StartsWith(RozjazdTyp.Rkpd) ? RozjazdTyp.Rkpd : n.StartsWith(RozjazdTyp.Rkp) ? RozjazdTyp.Rkp : RozjazdTyp.St;
    }

    /// <summary>Odległość końca (zwrotnicy) od środka wzdłuż toru [m w terenie] – A z katalogu; spoza katalogu – 16,6 m (1:9).</summary>
    public static double A(UtCrossing c) => Katalog.Find(c.Type) is RozjazdTyp t && t.A > 0 ? t.A : 16.6;

    /// <summary>Zwrot toru B względem A w środku: +1 – rosnący km toru B tworzy z rosnącym km toru A kąt ostry, −1 – rozwarty.</summary>
    public static int KierB(UtProject ut, UtCrossing c)
    {
        var ta = ut.Tracks.FirstOrDefault(t => t.Id == c.TrackA); var tb = ut.Tracks.FirstOrDefault(t => t.Id == c.TrackB);
        if (ta == null || tb == null) return 1;
        double aa = Trasa.Compute(ta).At(c.SA).Az, ab = Trasa.Compute(tb).At(c.SB).Az;
        return Math.Cos(ab - aa) >= 0 ? 1 : -1;
    }

    /// <summary>
    /// Węzły końca <paramref name="koniec"/> (ab / cd): tor i km – na obu torach (jeden węzeł zwrotnicy; najpierw tor A); St – brak.
    /// </summary>
    public static List<(string Track, double S)> Wezly(UtProject ut, UtCrossing c, string koniec)
    {
        var res = new List<(string, double)>();
        var kind = Rodzaj(c); if (kind == RozjazdTyp.St) return res;
        double a = A(c); int kb = KierB(ut, c), side = koniec == Ab ? -1 : 1;
        (string, double) OnA = (c.TrackA, c.SA + side * a), OnB = (c.TrackB, c.SB + side * kb * a);
        res.Add(OnA); res.Add(OnB);
        return res;
    }

    /// <summary>Tor i km symbolu końca na obrazie – na torze A.</summary>
    public static (string Track, double S)? Miejsce(UtProject ut, UtCrossing c, string koniec) =>
        Wezly(ut, c, koniec) is { Count: > 0 } w ? w[0] : null;

    /// <summary>
    /// Kierunek zwrotnicy końca na torze jej symbolu (jak <see cref="UtTurnout.Dir"/> – od ostrza do ramion): ramiona na zewnątrz rozjazdu.
    /// </summary>
    public static int Dir(UtProject ut, UtCrossing c, string koniec)
    {
        if (Miejsce(ut, c, koniec) is not var (tr, s)) return 1;
        double mid = tr == c.TrackA ? c.SA : c.SB;
        int toMid = Math.Sign(mid - s); if (toMid == 0) toMid = 1;
        return -toMid;
    }

    /// <summary>Przedziały km nierysowane na obrazie i pomijane w symulatorze: wnętrze rozjazdu krzyżowego na torze B.</summary>
    public static IEnumerable<(string Track, double A, double B)> Pominiete(UtProject ut)
    {
        foreach (var c in ut.Crossings.Where(c => Rodzaj(c) != RozjazdTyp.St))
        {
            double a = A(c);
            yield return (c.TrackB, c.SB - a, c.SB + a);
        }
    }

    /// <summary>
    /// Rkp – brakujący przejazd: tor, z którego ramienia zwrotnicy ab, i tor, na którego ramię zwrotnicy cd nie ma jazdy (w obie strony).
    /// Przejazd łukiem (Rozjazdy.CrossGeo): Slip &gt; 0 – z toru A przed środkiem na tor B za środkiem, więc brak z B (ab) na A (cd);
    /// Slip &lt; 0 – z toru B przed środkiem na tor A za środkiem, więc brak z A (ab) na B (cd). Rkpd i St – null.
    /// </summary>
    public static (string TorAb, string TorCd)? BrakPrzejazdu(UtCrossing c) =>
        Rodzaj(c) != RozjazdTyp.Rkp ? null : c.Slip > 0 ? (c.TrackB, c.TrackA) : (c.TrackA, c.TrackB);

    /// <summary>Oznaczenie końca na obrazie i w symulatorze, np. „3ab”.</summary>
    public static string Numer(UtCrossing c, string koniec) => $"{c.Number}{koniec}";
}

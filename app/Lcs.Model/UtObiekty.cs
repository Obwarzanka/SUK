namespace Lcs.Model;

/// <summary>
/// Obiekt inżynieryjny (Ig-10 § 4 ust. 1 pkt 15a i 20, zał. 8 s. 41–44 i 63; PLAN_EDYTOR_UT.md § 5m): most, wiadukt, tunel, przepust,
/// przejście, ściana / mur oporowy. Obiekt wzdłuż toru leży na torze <see cref="Track"/> od km <see cref="S0"/> do <see cref="S1"/>
/// [m w terenie od początku toru]; obiekt poprzeczny – w km <see cref="S0"/> (= <see cref="S1"/>), oś przeszkody pod kątem
/// <see cref="Angle"/> do osi toru, sięga <see cref="Reach"/> w obie strony. Obiekt wzdłuż toru może obejmować kilka torów
/// (<see cref="Tory"/> – tory dołożone, każdy z własnym km) albo leżeć osobno na każdym torze – wybiera projektant (pyt. 17).
/// </summary>
public class UtObiekt
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    /// <summary>Rodzaj (<see cref="Obiekty"/>: most, wiadukt, tunel…).</summary>
    public string Kind { get; set; } = Obiekty.Most;
    /// <summary>Nazwa albo numer obiektu.</summary>
    public string Name { get; set; } = "";
    /// <summary>Co obiekt przekracza (np. „ul. Piłsudskiego”, „rz. Ner”, „linia nr 17”).</summary>
    public string Nad { get; set; } = "";
    public string Track { get; set; }
    public double S0 { get; set; }
    public double S1 { get; set; }
    /// <summary>Kąt osi przeszkody (drogi, cieku, toru pod spodem) do osi toru [°], 90 – prostopadle.</summary>
    public double Angle { get; set; } = 90;
    /// <summary>Szerokość przeszkody / światło obiektu [m] (droga pod wiaduktem, przepust, przejście).</summary>
    public double Width { get; set; } = 6;
    /// <summary>Wiadukt drogowy: liczba ustrojów (osobne jezdnie obok siebie, każdy o szerokości <see cref="Width"/>) i odstęp między nimi [m].</summary>
    public int Ustroje { get; set; } = 1;
    public double OdstepUstrojow { get; set; } = 2;
    /// <summary>Wiadukt drogowy z kilkoma ustrojami: skrzydełka tylko na skrajnych krawędziach (wspólne), nie przy każdym ustroju.</summary>
    public bool SkrzydlaWspolne { get; set; }
    /// <summary>Obiekt poprzeczny: zasięg w obie strony od osi toru [m].</summary>
    public double Reach { get; set; } = 10;
    /// <summary>Ściana / mur oporowy: strona (+1 – po prawej patrząc w stronę rosnącego km toru) i odległość od osi toru [m].</summary>
    public int Side { get; set; } = 1;
    public double Offset { get; set; } = 3;
    /// <summary>Tory dołożone do obiektu wzdłuż toru (most pod torami 1 i 2) – null albo pusta: jeden tor.</summary>
    public List<UtObiektTor> Tory { get; set; }
    public string Uwagi { get; set; } = "";

    public double Len => Math.Abs(S1 - S0);
    public double Lo => Math.Min(S0, S1);
    public double Hi => Math.Max(S0, S1);
    /// <summary>Wszystkie tory obiektu z km: główny i dołożone.</summary>
    public IEnumerable<(string Track, double S0, double S1)> Wszystkie()
    {
        yield return (Track, Lo, Hi);
        foreach (var t in Tory ?? new()) yield return (t.Track, Math.Min(t.S0, t.S1), Math.Max(t.S0, t.S1));
    }
}

/// <summary>Tor dołożony do obiektu inżynieryjnego: km od–do na tym torze.</summary>
public class UtObiektTor
{
    public string Track { get; set; }
    public double S0 { get; set; }
    public double S1 { get; set; }
}

/// <summary>Rodzaje obiektów inżynieryjnych (Ig-10 zał. 8), kontrola, zakresy bez NMT dla profilu.</summary>
public static class Obiekty
{
    public const string Most = "most", Wiadukt = "wiadukt", WiaduktTor = "wiadukt_tor", Tunel = "tunel", WiaduktDrog = "wiadukt_drog",
        TunelPod = "tunel_pod", Przepust = "przepust", PrzejsciePod = "przejscie_pod", Kladka = "kladka", Sciana = "sciana", Mur = "mur";

    /// <summary>
    /// Nastawnie (Ig-10 zał. 8 s. 35–39, warstwa „budynek”): klucz „nastawnia_{p|n}_{r|m|e|k}” – parterowa (podłoga do 2 m nad główką szyny)
    /// albo piętrowa, ręczna, mechaniczna, elektryczna, komputerowa. Budynek z boku toru: km od–do = długość wzdłuż toru,
    /// <see cref="UtObiekt.Offset"/> – od osi toru do ściany od strony toru, <see cref="UtObiekt.Width"/> – głębokość budynku.
    /// </summary>
    public const string NastawniaPrefix = "nastawnia_";
    public static bool Nastawnia(string kind) => kind?.StartsWith(NastawniaPrefix) == true;
    /// <summary>Nastawnia piętrowa (podwójny obrys).</summary>
    public static bool Pietrowa(string kind) => Nastawnia(kind) && kind[NastawniaPrefix.Length] == 'p';
    /// <summary>Urządzenia nastawni: r – ręczna, m – mechaniczna (kreski skośne), e – elektryczna (kratka skośna), k – komputerowa (kratka prostopadła).</summary>
    public static char Urzadzenia(string kind) => Nastawnia(kind) ? kind[^1] : ' ';

    /// <summary>Położenie: wzdłuż toru (km od–do), poprzeczny (km), z boku toru (km od–do, strona, odległość).</summary>
    public enum Pol { Wzdluz, Poprzeczny, ZBoku }

    /// <summary>
    /// Rodzaj: klucz, nazwa, miejsce w Ig-10, położenie, czy niweleta na obiekcie nie idzie za terenem (NMT pokazuje dolinę albo wzgórze),
    /// domyślna szerokość [m].
    /// </summary>
    public record Rodzaj(string Key, string Name, string Ig10, Pol Pol, bool BezNmt, double Width);

    public static readonly Rodzaj[] Kinds =
    {
        new(Most, "most", "zał. 8, s. 41", Pol.Wzdluz, true, 0),
        new(Wiadukt, "wiadukt kolejowy nad drogą", "zał. 8, s. 43 (linia kolejowa w poziomie górnym)", Pol.Wzdluz, true, 8),
        new(WiaduktTor, "wiadukt nad innym torem", "zał. 8, s. 44 (skrzyżowanie torów w różnych poziomach)", Pol.Wzdluz, true, 0),
        new(Tunel, "linia kolejowa w tunelu", "zał. 8, s. 42", Pol.Wzdluz, true, 0),
        new(WiaduktDrog, "wiadukt drogowy nad torami", "zał. 8, s. 44 (linia kolejowa w poziomie dolnym)", Pol.Poprzeczny, false, 10),
        new(TunelPod, "tunel pod linią kolejową", "zał. 8, s. 43", Pol.Poprzeczny, false, 6),
        new(Przepust, "przepust", "zał. 8, s. 43", Pol.Poprzeczny, false, 1.5),
        new(PrzejsciePod, "przejście piesze pod torami", "zał. 8, s. 42", Pol.Poprzeczny, false, 4),
        new(Kladka, "przejście piesze nad torami (kładka)", "zał. 8, s. 42", Pol.Poprzeczny, false, 3),
        new(Sciana, "ściana oporowa", "zał. 8, s. 63", Pol.ZBoku, false, 0),
        new(Mur, "mur oporowy", "zał. 8, s. 63", Pol.ZBoku, false, 0),
        new(NastawniaPrefix + "n_r", "nastawnia parterowa ręczna", "zał. 8, s. 35", Pol.ZBoku, false, 6),
        new(NastawniaPrefix + "n_m", "nastawnia parterowa mechaniczna", "zał. 8, s. 36", Pol.ZBoku, false, 6),
        new(NastawniaPrefix + "n_e", "nastawnia parterowa elektryczna", "zał. 8, s. 36", Pol.ZBoku, false, 6),
        new(NastawniaPrefix + "n_k", "nastawnia parterowa komputerowa", "zał. 8, s. 38", Pol.ZBoku, false, 6),
        new(NastawniaPrefix + "p_r", "nastawnia piętrowa ręczna", "zał. 8, s. 37", Pol.ZBoku, false, 8),
        new(NastawniaPrefix + "p_m", "nastawnia piętrowa mechaniczna", "zał. 8, s. 37", Pol.ZBoku, false, 8),
        new(NastawniaPrefix + "p_e", "nastawnia piętrowa elektryczna", "zał. 8, s. 38", Pol.ZBoku, false, 8),
        new(NastawniaPrefix + "p_k", "nastawnia piętrowa komputerowa", "zał. 8, s. 39", Pol.ZBoku, false, 8),
    };

    /// <summary>Domyślna odległość ściany nastawni od osi toru [m].</summary>
    public const double NastawniaOdsun = 6;

    public static Rodzaj Info(string key) => Kinds.FirstOrDefault(k => k.Key == key) ?? Kinds[0];
    public static bool Wzdluz(UtObiekt o) => Info(o.Kind).Pol != Pol.Poprzeczny;

    /// <summary>Opis obiektu: rodzaj, nazwa, nad czym.</summary>
    public static string Opis(UtObiekt o) =>
        Info(o.Kind).Name + (o.Name.Length > 0 ? " " + o.Name : "") + (o.Nad.Length > 0 ? " (" + o.Nad + ")" : "");

    /// <summary>Zapas na końcach obiektu [m] przy niwelecie z terenu (przyczółki, portale).</summary>
    public const double ZapasNmt = 5;

    /// <summary>Zakresy km toru, na których niweleta nie idzie za terenem (most, wiadukt, tunel na tym torze), z zapasem.</summary>
    public static List<(double S0, double S1, UtObiekt O)> BezNmt(UtProject p, string track, double zapas = ZapasNmt)
    {
        var res = new List<(double, double, UtObiekt)>();
        foreach (var o in p.Obiekty ?? new())
        {
            if (!Info(o.Kind).BezNmt) continue;
            foreach (var (t, a, b) in o.Wszystkie()) if (t == track) res.Add((a - zapas, b + zapas, o));
        }
        return res.OrderBy(x => x.Item1).ToList();
    }

    /// <summary>Obiekty na torze (główny albo dołożony) z km na tym torze.</summary>
    public static IEnumerable<(UtObiekt O, double S0, double S1)> NaTorze(UtProject p, string track)
    {
        foreach (var o in p.Obiekty ?? new())
            foreach (var (t, a, b) in o.Wszystkie()) if (t == track) yield return (o, a, b);
    }

    /// <summary>Uwagi „Sprawdź”: brak toru, poza torem, długość, dwa obiekty w tym samym miejscu toru, obiekt bez nazwy.</summary>
    public static List<(bool Error, string Text, string Track, double S, string Id)> Check(UtProject p, Dictionary<string, TrasaResult> r)
    {
        var res = new List<(bool, string, string, double, string)>();
        foreach (var o in p.Obiekty ?? new())
        {
            var inf = Info(o.Kind); string nm = Opis(o);
            foreach (var (t, a, b) in o.Wszystkie())
            {
                if (t == null || !r.TryGetValue(t, out var rt)) { res.Add((true, nm + ": brak toru", null, 0, o.Id)); continue; }
                if (a < -0.01 || b > rt.Length + 0.01) res.Add((true, nm + ": wychodzi poza tor", t, Math.Clamp(a, 0, rt.Length), o.Id));
                if (inf.Pol != Pol.Poprzeczny && b - a < 1) res.Add((true, nm + ": km od – do krótszy niż 1 m", t, a, o.Id));
            }
            if (inf.Pol == Pol.Poprzeczny && (o.Angle < 10 || o.Angle > 170)) res.Add((true, nm + $": kąt z osią toru {o.Angle:0}° – zbyt ostry", o.Track, o.S0, o.Id));
            if ((o.Tory ?? new()).Any(x => x.Track == o.Track) || (o.Tory ?? new()).GroupBy(x => x.Track).Any(g => g.Count() > 1))
                res.Add((true, nm + ": ten sam tor dołożony dwa razy", o.Track, o.S0, o.Id));
            if (o.Name.Length == 0 && o.Nad.Length == 0) res.Add((false, inf.Name + " bez nazwy", o.Track, o.S0, o.Id));
        }
        // nakładanie się obiektów wzdłuż toru na tym samym torze (ściana / mur – tylko po tej samej stronie)
        var ws = (p.Obiekty ?? new()).Where(Wzdluz).ToList();
        for (int i = 0; i < ws.Count; i++)
            for (int j = i + 1; j < ws.Count; j++)
            {
                var (x, y) = (ws[i], ws[j]);
                bool bx = Info(x.Kind).Pol == Pol.ZBoku, by = Info(y.Kind).Pol == Pol.ZBoku;
                if (bx != by || (bx && x.Side != y.Side) || Nastawnia(x.Kind) != Nastawnia(y.Kind)) continue;
                foreach (var (t, a, b) in x.Wszystkie())
                    foreach (var (u, c, d) in y.Wszystkie())
                        if (t == u && Math.Min(b, d) - Math.Max(a, c) > 0.5)
                            res.Add((true, $"{Opis(x)} i {Opis(y)} nakładają się (km {Math.Max(a, c):0.#} – {Math.Min(b, d):0.#})", t, Math.Max(a, c), x.Id));
            }
        return res;
    }

    /// <summary>Dołożenie toru do obiektu wzdłuż toru: km od–do = rzut końców obiektu na ten tor.</summary>
    public static UtObiektTor Doloz(TrasaResult main, TrasaResult other, string track, UtObiekt o)
    {
        double a = other.Nearest(main.At(Math.Clamp(o.Lo, 0, main.Length)).P, 0.2).S, b = other.Nearest(main.At(Math.Clamp(o.Hi, 0, main.Length)).P, 0.2).S;
        return new UtObiektTor { Track = track, S0 = Math.Round(Math.Min(a, b), 2), S1 = Math.Round(Math.Max(a, b), 2) };
    }

    /// <summary>Rozdzielenie obiektu na kilku torach na osobne obiekty (każdy z nazwą i parametrami pierwszego).</summary>
    public static List<UtObiekt> Rozdziel(UtObiekt o)
    {
        var res = new List<UtObiekt>();
        foreach (var t in o.Tory ?? new())
            res.Add(new UtObiekt { Kind = o.Kind, Name = o.Name, Nad = o.Nad, Track = t.Track, S0 = t.S0, S1 = t.S1, Angle = o.Angle, Width = o.Width, Ustroje = o.Ustroje, OdstepUstrojow = o.OdstepUstrojow, SkrzydlaWspolne = o.SkrzydlaWspolne, Reach = o.Reach, Side = o.Side, Offset = o.Offset, Uwagi = o.Uwagi });
        o.Tory = null;
        return res;
    }

    // ------------------------------------------------------------------ zmiany torów (cięcie, łączenie, km po zmianie łuku)
    /// <summary>Przeniesienie km obiektów toru <paramref name="track"/> funkcją <paramref name="m"/> (np. po zmianie łuku).</summary>
    public static void MapKm(UtProject p, string track, Func<double, double> m)
    {
        foreach (var o in p.Obiekty ?? new())
        {
            if (o.Track == track) { o.S0 = Math.Round(m(o.S0), 2); o.S1 = Math.Round(m(o.S1), 2); }
            foreach (var t in (o.Tory ?? new()).Where(t => t.Track == track)) { t.S0 = Math.Round(m(t.S0), 2); t.S1 = Math.Round(m(t.S1), 2); }
        }
    }

    /// <summary>Przeniesienie obiektów z toru <paramref name="from"/> na <paramref name="to"/> z km + <paramref name="off"/> (łączenie torów).</summary>
    public static void Przenies(UtProject p, string from, string to, double off)
    {
        foreach (var o in p.Obiekty ?? new())
        {
            if (o.Track == from) { o.Track = to; o.S0 += off; o.S1 += off; }
            foreach (var t in (o.Tory ?? new()).Where(t => t.Track == from)) { t.Track = to; t.S0 += off; t.S1 += off; }
        }
    }

    /// <summary>Cięcie toru: obiekty za <paramref name="from"/> → tor <paramref name="to"/>, km − from.</summary>
    public static void Ciecie(UtProject p, string track, string to, double from, double e)
    {
        foreach (var o in p.Obiekty ?? new())
        {
            if (o.Track == track && o.Lo >= from - e) { o.Track = to; o.S0 -= from; o.S1 -= from; }
            foreach (var t in (o.Tory ?? new()).Where(t => t.Track == track && Math.Min(t.S0, t.S1) >= from - e)) { t.Track = to; t.S0 -= from; t.S1 -= from; }
        }
    }

    /// <summary>Usunięty kawałek toru: obiekt w całości w nim znika (tor dołożony – wypada z obiektu), częściowo – przycięty.</summary>
    public static int Usun(UtProject p, string track, Func<double, bool> gone)
    {
        int n = 0;
        foreach (var o in (p.Obiekty ?? new()).ToList())
        {
            foreach (var t in (o.Tory ?? new()).Where(t => t.Track == track && gone(Math.Min(t.S0, t.S1)) && gone(Math.Max(t.S0, t.S1))).ToList()) { o.Tory.Remove(t); n++; }
            if (o.Track != track) continue;
            if (gone(o.Lo) && gone(o.Hi))
            {
                // tor główny znika: pierwszy dołożony zostaje głównym
                if (o.Tory is { Count: > 0 }) { var t = o.Tory[0]; o.Tory.RemoveAt(0); o.Track = t.Track; o.S0 = t.S0; o.S1 = t.S1; }
                else p.Obiekty.Remove(o);
                n++; continue;
            }
            double a = o.Lo, b = o.Hi;
            if (gone(b) && !gone(a)) { while (gone(b) && b > a) b -= 0.5; }
            else if (gone(a) && !gone(b)) { while (gone(a) && a < b) a += 0.5; }
            o.S0 = a; o.S1 = b;
        }
        return n;
    }

    /// <summary>Po usunięciu torów: obiekty usuniętego toru głównego (pierwszy dołożony zostaje głównym), dołożone bez toru – wypadają.</summary>
    public static void Sprzatnij(UtProject p, Func<string, bool> exists)
    {
        p.Obiekty ??= new();
        foreach (var o in p.Obiekty)
        {
            o.Tory?.RemoveAll(t => !exists(t.Track));
            if (!exists(o.Track) && o.Tory is { Count: > 0 }) { var t = o.Tory[0]; o.Tory.RemoveAt(0); o.Track = t.Track; o.S0 = t.S0; o.S1 = t.S1; }
            if (o.Tory is { Count: 0 }) o.Tory = null;
        }
        p.Obiekty.RemoveAll(o => !exists(o.Track));
    }
}

namespace Lcs.Tabor;

/// <summary>
/// Pociąg do obliczeń: skład z katalogu przeliczony na sumy (PLAN_EDYTOR_TABORU.md § 2–3). Masy w tonach, siły w kN, prędkości w km/h
/// na wejściu i wyjściu; wewnątrz kroku SI. Ten sam kod liczy jazdę w symulatorze i podgląd w edytorze taboru.
/// </summary>
public class Pociag
{
    public const double G = 9.81;
    /// <summary>Współczynnik hamowania: siła hamowania pełnego = KH·g·masa hamująca (λ = 100 % ≈ 0,65 m/s²) – do kalibracji / potwierdzenia.</summary>
    public const double KH = 0.07;
    /// <summary>Czas przełączenia BZT między siecią a silnikiem spalinowym [s] – bez siły pociągowej (do potwierdzenia).</summary>
    public const double CzasPrzelaczeniaBzt = 20;
    /// <summary>Krok całkowania [s] – stały, żeby jazda była powtarzalna (rejestrator, gra sieciowa).</summary>
    public const double Krok = 0.1;

    public record Element(Pojazd P, int Liczba, double MasaT, double MasaHamT);

    public string Nazwa { get; }
    public List<Element> Elementy { get; } = new();
    public string Nastawienie { get; }
    public double Dlugosc { get; }
    /// <summary>Masa rzeczywista [t].</summary>
    public double Masa { get; }
    /// <summary>Masa zastępcza z masami wirującymi [t].</summary>
    public double MasaZastepcza { get; }
    public double MasaHamujaca { get; }
    /// <summary>Procent masy hamującej λ [%].</summary>
    public double Lambda => Masa > 0 ? MasaHamujaca / Masa * 100 : 0;
    public double VMax { get; }
    /// <summary>Wszystkie pojazdy to zespoły trakcyjne – hamulec elektropneumatyczny.</summary>
    public bool Zespol { get; }
    /// <summary>Czas napełniania / luzowania hamulca [s] wg nastawienia.</summary>
    public double CzasNapelniania { get; }
    public double CzasLuzowania { get; }

    public Pociag(Sklad s, TaborPlik kat)
    {
        Nazwa = s.Nazwa.Length > 0 ? s.Nazwa : s.Id;
        Nastawienie = s.Nastawienie is "G" or "P" or "R" ? s.Nastawienie : "P";
        foreach (var x in s.Pojazdy)
        {
            var p = kat.Pojazd(x.Seria) ?? throw new ArgumentException($"Skład {s.Id}: nie ma serii {x.Seria} w katalogu");
            double zal = Math.Clamp(x.Zaladowanie, 0, 1);
            double mh0 = MH(p.MasaHamujaca, Nastawienie), mh1 = p.MasaHamujacaLadowny != null ? MH(p.MasaHamujacaLadowny, Nastawienie) : mh0;
            Elementy.Add(new Element(p, Math.Max(1, x.Liczba), p.Masa + zal * p.Ladunek, mh0 + zal * (mh1 - mh0)));
        }
        Dlugosc = Elementy.Sum(e => e.Liczba * e.P.Dlugosc);
        Masa = Elementy.Sum(e => e.Liczba * e.MasaT);
        MasaZastepcza = Elementy.Sum(e => e.Liczba * e.MasaT * (1 + e.P.MasyWirujace));
        MasaHamujaca = Elementy.Sum(e => e.Liczba * e.MasaHamT);
        VMax = Elementy.Count == 0 ? 0 : Elementy.Min(e => e.P.VMax);
        Zespol = Elementy.Count > 0 && Elementy.All(e => Rodzaj.Zespol(e.P.Rodzaj));
        (CzasNapelniania, CzasLuzowania) = Zespol ? (2.0, 4.0) : Nastawienie == "G" ? (25.0, 50.0) : (5.0, 18.0);
    }

    /// <summary>Masa hamująca dla nastawienia; brak wpisu – najbliższe łagodniejsze (R → P → G).</summary>
    static double MH(Dictionary<string, double> d, string n)
    {
        foreach (var k in n switch { "R" => new[] { "R", "P", "G" }, "P" => new[] { "P", "G", "R" }, _ => new[] { "G", "P", "R" } })
            if (d.TryGetValue(k, out var v)) return v;
        return 0;
    }

    public static Pociag Z(TaborPlik kat, string skladId) => new(kat.Sklad(skladId) ?? throw new ArgumentException($"Nie ma składu {skladId}"), kat);

    /// <summary>Współczynnik przyczepności Curtiusa–Kniefflera (v w km/h) razy warunki szyn (1 – sucho, ok. 0,75 – mokro, ok. 0,5 – liście).</summary>
    public static double Mi(double v, double warunki) => (0.161 + 7.5 / (44 + Math.Max(0, v))) * warunki;

    /// <summary>Opór podstawowy całego pociągu [kN] przy prędkości v [km/h].</summary>
    public double Opor(double v) => Elementy.Sum(e => e.Liczba * e.P.Opory.W(v) * e.MasaT * G) / 1000;

    /// <summary>Opór od pochylenia [kN]: i [‰] = i [N/kN] (wzniesienie w kierunku jazdy – dodatni) plus opór łuku [N/kN].</summary>
    public double OporProfilu(double pochylenie, double oporLuku = 0) => (pochylenie + oporLuku) * Masa * G / 1000;

    /// <summary>Źródło zasilania, z którego pojazd korzysta na torze z siecią / bez sieci; null – brak (EZT bez sieci).</summary>
    public static string ZrodloDla(Pojazd p, bool siec) => p.Rodzaj switch
    {
        Rodzaj.Bzt => siec ? Zasilanie.Siec : Zasilanie.Spalinowy,
        Rodzaj.LokEl or Rodzaj.Ezt => siec ? Zasilanie.Siec : null,
        _ => p.Trakcja.Count > 0 ? p.Trakcja[0].Zasilanie : null,
    };

    /// <summary>Największa siła pociągowa [kN] przy v [km/h] (charakterystyka ograniczona przyczepnością); <paramref name="zrodloBzt"/> – bieżące zasilanie BZT.</summary>
    public double SilaMax(double v, bool siec, double warunki = 1, string zrodloBzt = null)
    {
        double f = 0;
        foreach (var e in Elementy.Where(e => e.P.Napedny))
        {
            var z = e.P.Rodzaj == Rodzaj.Bzt && zrodloBzt != null ? zrodloBzt : ZrodloDla(e.P, siec);
            var c = z == null ? null : e.P.Char(z);
            if (c == null) continue;
            if (v >= (c.VMax > 0 ? c.VMax : e.P.VMax)) continue;
            f += e.Liczba * Math.Min(c.F(v), Mi(v, warunki) * e.P.MasaNapedna * G);
        }
        return f;
    }

    /// <summary>Pełna siła hamowania [kN] przy v [km/h]: hamulec pneumatyczny z masy hamującej albo ED (większy), ograniczona przyczepnością wszystkich osi.</summary>
    public double SilaHamowania(double v, bool siec, double warunki = 1)
    {
        double pn = KH * G * MasaHamujaca;
        double ed = Elementy.Where(e => e.P.HamulecED > 0 && ZrodloDla(e.P, siec) == Zasilanie.Siec).Sum(e => e.Liczba * e.P.HamulecED);
        return Math.Min(Math.Max(pn, ed), Mi(v, warunki) * Masa * G);
    }

    /// <summary>Czy pociąg ruszy na pochyleniu [‰] (siła przy 0 km/h większa niż opór i składowa ciężaru).</summary>
    public bool MozeRuszyc(double pochylenie, bool siec = true, double warunki = 1) => SilaMax(0, siec, warunki) > Opor(0) + OporProfilu(pochylenie);
}

/// <summary>Polecenie maszynisty na krok: nastawa trakcji 0–1, hamowanie 0–1 (pełne = 1), warunki toru pod pociągiem.</summary>
public struct Sterowanie
{
    public double Nastawa, Hamowanie;
    /// <summary>Średnie pochylenie pod pociągiem [‰], dodatnie – wzniesienie w kierunku jazdy (ważone długością składu na kawałkach toru).</summary>
    public double Pochylenie;
    /// <summary>Średni opór łuku pod pociągiem [N/kN].</summary>
    public double OporLuku;
    /// <summary>Sieć trakcyjna nad czołem pociągu.</summary>
    public bool Siec;
    /// <summary>Warunki szyn (przyczepność): 1 – sucho.</summary>
    public double Warunki;
}

/// <summary>Stan jazdy pociągu (zmienia go tylko <see cref="Jazda.Krok"/>).</summary>
public class StanJazdy
{
    /// <summary>Prędkość [m/s].</summary>
    public double V;
    /// <summary>Bieżący stopień zahamowania 0–1 (napełnianie / luzowanie cylindrów).</summary>
    public double H;
    /// <summary>Bieżące zasilanie BZT; null – jeszcze nie ustalone.</summary>
    public string ZrodloBzt;
    /// <summary>Pozostały czas przełączenia BZT [s].</summary>
    public double Przelaczanie;
    /// <summary>Ostatni krok: siła pociągowa, hamowania, opory [kN], przyspieszenie [m/s²].</summary>
    public double F, B, W, A;
    /// <summary>Pociąg ma nastawę, ale nie rusza (za słaby na pochyleniu albo bez zasilania).</summary>
    public bool NieRusza;
    /// <summary>Pojazdy bez zasilania (EZT / lokomotywa elektryczna poza siecią).</summary>
    public bool BrakZasilania;
}

/// <summary>Całkowanie ruchu pociągu stałym krokiem <see cref="Pociag.Krok"/> (wynik nie zależy od długości klatki symulatora).</summary>
public static class Jazda
{
    /// <summary>Ruch o czas dt [s]; zwraca przebytą drogę [m]. Pociąg nie toczy się wstecz (na postoju trzyma hamulec postojowy).</summary>
    public static double Krok(Pociag p, StanJazdy s, Sterowanie u, double dt)
    {
        double droga = 0;
        while (dt > 1e-9)
        {
            double h = Math.Min(dt, Pociag.Krok);
            droga += Maly(p, s, u, h);
            dt -= h;
        }
        return droga;
    }

    static double Maly(Pociag p, StanJazdy s, Sterowanie u, double dt)
    {
        double war = u.Warunki > 0 ? u.Warunki : 1;
        // hamulec: napełnianie / luzowanie w czasie
        double cel = Math.Clamp(u.Hamowanie, 0, 1);
        s.H = cel > s.H ? Math.Min(cel, s.H + dt / p.CzasNapelniania) : Math.Max(cel, s.H - dt / p.CzasLuzowania);
        // BZT: zmiana zasilania przy wjeździe pod sieć / zjeździe spod sieci trwa
        if (p.Elementy.Any(e => e.P.Rodzaj == Rodzaj.Bzt))
        {
            var chce = u.Siec ? Zasilanie.Siec : Zasilanie.Spalinowy;
            if (s.ZrodloBzt == null) s.ZrodloBzt = chce;
            else if (s.ZrodloBzt != chce && s.Przelaczanie <= 0) s.Przelaczanie = Pociag.CzasPrzelaczeniaBzt;
            if (s.Przelaczanie > 0) { s.Przelaczanie -= dt; if (s.Przelaczanie <= 0) { s.Przelaczanie = 0; s.ZrodloBzt = chce; } }
        }
        double v = s.V * 3.6;
        double nast = Math.Clamp(u.Nastawa, 0, 1);
        double f = nast * (s.Przelaczanie > 0 ? SilaBezBzt(p, v, u.Siec, war) : p.SilaMax(v, u.Siec, war, s.ZrodloBzt));
        s.BrakZasilania = p.Elementy.Any(e => e.P.Napedny && Pociag.ZrodloDla(e.P, u.Siec) == null);
        double b = s.H * p.SilaHamowania(v, u.Siec, war);
        double w = p.Opor(v), prof = p.OporProfilu(u.Pochylenie, u.OporLuku);
        s.F = f; s.B = b; s.W = w + prof;
        double m = p.MasaZastepcza * 1000;   // kg
        double v0 = s.V;
        if (v0 <= 1e-6)
        {
            // postój: rusza tylko, gdy siła pokona opór przy 0 km/h i składową ciężaru; inaczej stoi (hamulec postojowy, bez staczania)
            double net = (f - b - w - prof) * 1000;
            s.NieRusza = nast > 0 && cel <= 0 && net <= 0;
            if (net <= 0) { s.V = 0; s.A = 0; return 0; }
            s.A = net / m; s.V = s.A * dt;
            return s.V * dt / 2;
        }
        s.NieRusza = false;
        s.A = (f - b - w - prof) * 1000 / m;
        s.V = Math.Max(0, v0 + s.A * dt);
        return (v0 + s.V) / 2 * dt;
    }

    /// <summary>Siła w czasie przełączania BZT: pojazdy inne niż BZT dalej ciągną.</summary>
    static double SilaBezBzt(Pociag p, double v, bool siec, double war) =>
        p.Elementy.Where(e => e.P.Napedny && e.P.Rodzaj != Rodzaj.Bzt).Sum(e =>
        {
            var z = Pociag.ZrodloDla(e.P, siec); var c = z == null ? null : e.P.Char(z);
            return c == null || v >= (c.VMax > 0 ? c.VMax : e.P.VMax) ? 0 : e.Liczba * Math.Min(c.F(v), Pociag.Mi(v, war) * e.P.MasaNapedna * Pociag.G);
        });

    /// <summary>Rozpędzanie od postoju pełną nastawą do v [km/h] na stałym pochyleniu: czas [s] i droga [m]; null – nie osiąga (zwraca też prędkość równowagi).</summary>
    public static (double? T, double? S, double VOsiagnieta) Rozpedzanie(Pociag p, double vDo, double pochylenie = 0, bool siec = true, double warunki = 1, double tMax = 1800)
    {
        var s = new StanJazdy(); var u = new Sterowanie { Nastawa = 1, Pochylenie = pochylenie, Siec = siec, Warunki = warunki };
        double t = 0, d = 0, vmax = 0;
        while (t < tMax)
        {
            d += Krok(p, s, u, Pociag.Krok); t += Pociag.Krok; vmax = Math.Max(vmax, s.V * 3.6);
            if (s.V * 3.6 >= vDo) return (t, d, vDo);
            if (s.NieRusza) return (null, null, 0);
        }
        return (null, null, vmax);
    }

    /// <summary>Droga hamowania [m] od v0 do v1 [km/h] przy stałym pochyleniu, z czasem napełniania hamulca; <paramref name="stopien"/> – część pełnego hamowania.</summary>
    public static double DrogaHamowania(Pociag p, double v0, double v1 = 0, double pochylenie = 0, double stopien = 1, bool siec = true, double warunki = 1)
    {
        var s = new StanJazdy { V = v0 / 3.6 }; var u = new Sterowanie { Hamowanie = stopien, Pochylenie = pochylenie, Siec = siec, Warunki = warunki };
        double d = 0;
        for (int i = 0; i < 200000 && s.V * 3.6 > v1 + 1e-9; i++)
        {
            d += Krok(p, s, u, Pociag.Krok);
            if (s.A >= 0 && s.H >= stopien) return double.PositiveInfinity;   // na spadku hamulec nie wystarcza
        }
        return d;
    }

    /// <summary>Prędkość równowagi [km/h] pełną nastawą na pochyleniu (siła = opory); 0 – nie rusza.</summary>
    public static double PredkoscRownowagi(Pociag p, double pochylenie, bool siec = true, double warunki = 1)
    {
        if (!p.MozeRuszyc(pochylenie, siec, warunki)) return 0;
        double v = 0;
        for (double x = 0; x <= Math.Max(p.VMax, 1); x += 0.5)
            if (p.SilaMax(x, siec, warunki) > p.Opor(x) + p.OporProfilu(pochylenie)) v = x; else break;
        return v;
    }
}

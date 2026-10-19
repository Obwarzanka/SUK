using System.Globalization;
using System.Text;
using Xunit;

namespace Lcs.Tabor.Testy;

/// <summary>Etap B1 (PLAN_EDYTOR_TABORU.md § 8): katalog, sumy składu, fizyka jazdy – pochylenie, hamowanie, przyczepność, BZT, powtarzalność.</summary>
public class TaborTesty
{
    static readonly TaborPlik Kat = TaborPlik.Load(Path.Combine(AppContext.BaseDirectory, "Data", "tabor.json"));
    static Pociag P(string id) => Pociag.Z(Kat, id);
    static StanJazdy Jedz(Pociag p, double v) => new() { V = v / 3.6 };

    [Fact]
    public void KatalogBezUwag()
    {
        Assert.Empty(Kat.Sprawdz());
        Assert.Equal(6, Kat.Pojazdy.Count);
        Assert.Equal(4, Kat.Sklady.Count);
        Assert.All(Kat.Pojazdy, p => Assert.True(p.DoPotwierdzenia));
    }

    [Fact]
    public void SprawdzWykrywaBledy()
    {
        var k = TaborPlik.Deserialize(TaborPlik.Serialize(Kat));
        k.Pojazdy.First(p => p.Seria == "36WEh").Trakcja.RemoveAt(1);
        k.Sklady.Add(new Sklad { Id = "x", Pojazdy = { new PozycjaSkladu { Seria = "111A" }, new PozycjaSkladu { Seria = "EP09" } } });
        var uw = k.Sprawdz();
        Assert.Contains(uw, x => x.Contains("36WEh") && x.Contains("spalinowy"));
        Assert.Contains(uw, x => x.Contains("EP09"));
        Assert.Contains(uw, x => x.Contains("bez pojazdu napędnego"));
    }

    [Fact]
    public void ZapisIOdczyt()
    {
        var dir = Directory.CreateTempSubdirectory("tabor").FullName;
        var f = Path.Combine(dir, "tabor.json");
        Kat.Save(f); Kat.Save(f);
        Assert.True(File.Exists(f + ".bak"));
        Assert.Equal(TaborPlik.Serialize(Kat), TaborPlik.Serialize(TaborPlik.Load(f)));
        // plik układu zastępuje serię wspólną
        var u = new TaborPlik { Pojazdy = { new Pojazd { Seria = "111A", Masa = 41, Dlugosc = 24.5, MasaHamujaca = { ["P"] = 50 } } } };
        var st = Path.Combine(dir, "uklad.json"); u.Save(TaborPlik.PlikUkladu(st));
        var k = TaborPlik.Deserialize(TaborPlik.Serialize(Kat)); k.Uzupelnij(TaborPlik.Load(TaborPlik.PlikUkladu(st)));
        Assert.Equal(41, k.Pojazd("111A").Masa);
        Assert.Equal(6, k.Pojazdy.Count);
    }

    [Fact]
    public void SumySkladu()
    {
        var p = P("EU07+6x111A");
        Assert.Equal(15.915 + 6 * 24.5, p.Dlugosc, 6);
        Assert.Equal(80 + 6 * 43, p.Masa, 6);                    // 111A załadowanie 0,5 × 6 t
        Assert.Equal(80 + 6 * 64, p.MasaHamujaca, 6);            // nastawienie R
        Assert.Equal(120, p.VMax);
        var t = P("ET22+40xEaos");
        Assert.Equal(120 + 40 * 80, t.Masa, 6);
        Assert.Equal(90 + 40 * 58, t.MasaHamujaca, 6);           // G, ładowne
        Assert.InRange(t.Lambda, 72, 73);
        Assert.Equal(25, t.CzasNapelniania);
        Assert.Equal(2, P("EN57").CzasNapelniania);             // zespół – hamulec EP
    }

    [Fact]
    public void PrzyspieszenieZRownaniaRuchu()
    {
        var p = P("EN57");
        var s = Jedz(p, 50);
        Jazda.Krok(p, s, new Sterowanie { Nastawa = 1, Siec = true, Warunki = 1 }, 0.001);
        double f = 41.8, w = p.Opor(50);
        Assert.Equal((f - w) * 1000 / (p.MasaZastepcza * 1000), s.A, 4);
        // pochylenie 10 ‰ zmniejsza przyspieszenie o 10 N/kN × ciężar / masa zastępcza
        var s2 = Jedz(p, 50);
        Jazda.Krok(p, s2, new Sterowanie { Nastawa = 1, Siec = true, Warunki = 1, Pochylenie = 10 }, 0.001);
        Assert.Equal(10 * p.Masa * Pociag.G / 1000 * 1000 / (p.MasaZastepcza * 1000), s.A - s2.A, 4);
    }

    [Fact]
    public void PodGoreWolniejZGorySzybciej()
    {
        var p = P("EN57");
        var poz = Jazda.Rozpedzanie(p, 80, 0); var gora = Jazda.Rozpedzanie(p, 80, 10); var dol = Jazda.Rozpedzanie(p, 80, -10);
        Assert.True(gora.T > poz.T && poz.T > dol.T, $"{gora.T} > {poz.T} > {dol.T}");
        Assert.True(Jazda.PredkoscRownowagi(P("ET22+40xEaos"), 6) < Jazda.PredkoscRownowagi(P("ET22+40xEaos"), 0));
    }

    [Fact]
    public void HamowanieZalezyOdPochyleniaILambdy()
    {
        var p = P("EU07+6x111A");
        double poz = Jazda.DrogaHamowania(p, 100), spadek = Jazda.DrogaHamowania(p, 100, 0, -10), wzn = Jazda.DrogaHamowania(p, 100, 0, 10);
        Assert.True(spadek > poz && poz > wzn, $"{spadek} > {poz} > {wzn}");
        var tow = P("ET22+40xEaos");
        Assert.True(Jazda.DrogaHamowania(tow, 80) > Jazda.DrogaHamowania(p, 80));   // λ 73 % i nastawienie G
        // EP w EZT: krótkie napełnianie – droga krótsza niż przy tej samej λ z G
        Assert.InRange(Jazda.DrogaHamowania(P("EN57"), 100), 400, 1000);
    }

    [Fact]
    public void CiezkiTowarowyNieRuszaNaWzniesieniu()
    {
        var t = P("ET22+40xEaos");
        Assert.True(t.MozeRuszyc(5));
        Assert.False(t.MozeRuszyc(12));
        var s = new StanJazdy();
        Assert.Equal(0, Jazda.Krok(t, s, new Sterowanie { Nastawa = 1, Siec = true, Warunki = 1, Pochylenie = 12 }, 5));
        Assert.True(s.NieRusza);
        // na mokrych szynach przyczepność ogranicza siłę rozruchu
        Assert.True(t.SilaMax(0, true, 0.5) < t.SilaMax(0, true, 1));
    }

    [Fact]
    public void EztBezSieciNieJedzie()
    {
        var p = P("EN57"); var s = new StanJazdy();
        Jazda.Krok(p, s, new Sterowanie { Nastawa = 1, Siec = false, Warunki = 1 }, 5);
        Assert.Equal(0, s.V);
        Assert.True(s.BrakZasilania && s.NieRusza);
    }

    [Fact]
    public void BztPrzelaczaZasilanie()
    {
        var p = P("36WEh"); var s = Jedz(p, 60);
        var siec = new Sterowanie { Nastawa = 1, Siec = true, Warunki = 1 };
        Jazda.Krok(p, s, siec, 1);
        Assert.Equal(Zasilanie.Siec, s.ZrodloBzt);
        Assert.InRange(s.F, 93, 96.01);                          // 96 kN przy 60 km/h, po 1 s pociąg jedzie już szybciej
        var bez = siec with { Siec = false };
        Jazda.Krok(p, s, bez, 1);
        Assert.True(s.Przelaczanie > 0); Assert.Equal(0, s.F);
        Jazda.Krok(p, s, bez, Pociag.CzasPrzelaczeniaBzt);
        Assert.Equal(Zasilanie.Spalinowy, s.ZrodloBzt);
        Assert.Equal(Kat.Pojazd("36WEh").Char(Zasilanie.Spalinowy).F(s.V * 3.6), s.F, 0);   // charakterystyka spalinowa (siła z początku ostatniego kroku 0,1 s)
        Assert.False(s.BrakZasilania);
    }

    [Fact]
    public void PowtarzalnoscNiezaleznaOdKlatki()
    {
        var p = P("EU07+6x111A");
        (double S, double V) Run(double klatka)
        {
            // klatki symulatora 0,5 s albo 0,1 s – zmiany sterowania w tych samych chwilach (liczone w krokach, bez sumowania czasu)
            var s = new StanJazdy(); double d = 0; int n = (int)Math.Round(120 / klatka);
            for (int k = 0; k < n; k++)
            {
                double t = k * klatka;
                d += Jazda.Krok(p, s, new Sterowanie { Nastawa = t < 90 - 1e-6 ? 1 : 0, Hamowanie = t < 90 - 1e-6 ? 0 : 0.7, Siec = true, Warunki = 1, Pochylenie = t < 40 - 1e-6 ? 4 : -3 }, klatka);
            }
            return (d, s.V);
        }
        var a = Run(0.5); var b = Run(0.5); var c = Run(0.1);
        Assert.Equal(a, b);
        Assert.Equal(a.S, c.S, 6); Assert.Equal(a.V, c.V, 6);
    }

    /// <summary>Tabela do oceny przez właściciela (kalibracja): zapis obok testów, <c>kalibracja_tabor.txt</c>.</summary>
    [Fact]
    public void RaportKalibracji()
    {
        var ci = CultureInfo.GetCultureInfo("pl-PL");
        var sb = new StringBuilder("Kalibracja taboru (dane przykładowe – do potwierdzenia)\n\n");
        foreach (var sk in Kat.Sklady)
        {
            var p = P(sk.Id);
            double vr = Math.Min(p.VMax, 100);
            string R(double i) { var r = Jazda.Rozpedzanie(p, vr, i); return r.T is double t ? string.Format(ci, "{0:0} s / {1:0} m", t, r.S) : string.Format(ci, "nie osiąga ({0:0} km/h)", r.VOsiagnieta); }
            string H(double i) { var d = Jazda.DrogaHamowania(p, vr, 0, i); return double.IsInfinity(d) ? "nie zatrzyma" : string.Format(ci, "{0:0} m", d); }
            sb.AppendLine(string.Format(ci, "{0}: {1:0} t, {2:0.0} m, λ {3:0} % ({4}), Vmax {5:0} km/h", p.Nazwa, p.Masa, p.Dlugosc, p.Lambda, p.Nastawienie, p.VMax));
            sb.AppendLine($"  0 → {vr:0} km/h: poziom {R(0)}; wzniesienie 10 ‰ {R(10)}; spadek 10 ‰ {R(-10)}");
            sb.AppendLine($"  hamowanie nagłe {vr:0} → 0: poziom {H(0)}; spadek 10 ‰ {H(-10)}; wzniesienie 10 ‰ {H(10)}");
            sb.AppendLine(string.Format(ci, "  prędkość równowagi: 10 ‰ {0:0} km/h, 20 ‰ {1:0} km/h; rusza na: 10 ‰ {2}, 20 ‰ {3}",
                Jazda.PredkoscRownowagi(p, 10), Jazda.PredkoscRownowagi(p, 20), p.MozeRuszyc(10) ? "tak" : "nie", p.MozeRuszyc(20) ? "tak" : "nie"));
            sb.AppendLine();
        }
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "kalibracja_tabor.txt"), sb.ToString());
        Assert.Contains("EN57", sb.ToString());
    }
}

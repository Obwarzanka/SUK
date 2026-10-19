namespace Lcs.Tabor;

/// <summary>
/// Automatyczny maszynista (PLAN_EDYTOR_TABORU.md § 3, decyzja § 9 pkt 3) – wspólny dla symulatora (Sim/TrainsFizyka.cs) i przejazdu próbnego
/// w edytorze taboru: prędkość dozwolona z zapasem, krzywe hamowania służbowego do ograniczeń przed czołem i do miejsca zatrzymania
/// z czasem napełniania hamulca i średnim pochyleniem na drodze hamowania; ostatnie metry do miejsca zatrzymania z ok. 5 km/h.
/// Prędkości w m/s.
/// </summary>
public static class Maszynista
{
    /// <summary>Zapas poniżej prędkości dozwolonej [m/s] (ok. 3 km/h).</summary>
    public const double ZapasV = 3 / 3.6;
    /// <summary>Hamowanie służbowe – część pełnego.</summary>
    public const double HamSluzbowe = 0.7;
    /// <summary>Dojazd do miejsca zatrzymania [m/s].</summary>
    public const double DojazdV = 5 / 3.6;

    /// <summary>Kawałek drogi przed czołem: początek od czoła [m], długość [m], pochylenie [‰], ograniczenie prędkości [m/s] (≥ 1e3 – brak).</summary>
    public record struct Przed(double Od, double Len, double I, double Lim);

    /// <summary>Najwyższa prędkość [m/s], z której pociąg zwolni do <paramref name="lim"/> na drodze <paramref name="d"/>, hamując służbowo na pochyleniu <paramref name="i"/>.</summary>
    public static double VZHamowania(Pociag p, double vAkt, double d, double lim, double i, bool siec)
    {
        double b = (HamSluzbowe * p.SilaHamowania(vAkt * 3.6, siec) + p.Opor(0) + p.OporProfilu(i)) / p.MasaZastepcza;   // kN / t = m/s²
        b = Math.Max(b, 0.05);
        double T = p.CzasNapelniania;
        d = Math.Max(0, d);
        // v·T/2 (napełnianie) + (v² − lim²) / 2b = d
        return (-b * T + Math.Sqrt(b * b * T * T + 4 * (lim * lim + 2 * b * d))) / 2;
    }

    /// <summary>
    /// Prędkość docelowa [m/s]: <paramref name="vDoz"/> – dozwolona pod składem (już z ograniczeniem pociągu), <paramref name="przed"/> – droga przed czołem,
    /// <paramref name="dStop"/> – odległość do miejsca zatrzymania (∞ – brak).
    /// </summary>
    public static double Docelowa(Pociag p, double vAkt, double vDoz, IReadOnlyList<Przed> przed, double dStop, bool siec)
    {
        double cel = Math.Max(vDoz - ZapasV, vDoz * 0.9);
        double ISr(double d)
        {
            double s = 0, w = 0;
            foreach (var k in przed) { double ov = Math.Min(k.Od + k.Len, d) - k.Od; if (ov <= 0) break; s += ov * k.I; w += ov; }
            return w > 0 ? s / w : 0;
        }
        foreach (var k in przed)
            if (k.Lim < 1e3 && k.Lim < cel) cel = Math.Min(cel, VZHamowania(p, vAkt, k.Od, Math.Max(0, k.Lim - ZapasV), ISr(k.Od), siec));
        if (double.IsFinite(dStop))
        {
            var vh = VZHamowania(p, vAkt, dStop - 1, 0, ISr(dStop), siec);
            cel = Math.Min(cel, dStop > 2 ? Math.Max(vh, DojazdV) : vh);
        }
        return Math.Max(0, cel);
    }

    /// <summary>Nastawa i hamowanie do prędkości docelowej (regulator maszynisty); <paramref name="dStop"/> &lt; 0,5 m – pełne hamowanie.</summary>
    public static void Steruj(ref Sterowanie u, double v, double cel, double dStop)
    {
        u.Nastawa = 0; u.Hamowanie = 0;
        if (dStop < 0.5) u.Hamowanie = 1;
        else if (v > cel + 0.3) u.Hamowanie = Math.Clamp(0.4 + (v - cel) * 0.5, 0, 1);
        else if (v > cel) { /* wybieg */ }
        else u.Nastawa = Math.Clamp((cel - v) / 0.5, 0, 1);
    }
}

/// <summary>Kawałek profilu przejazdu próbnego: długość [m], pochylenie [‰ w kierunku jazdy], prędkość dozwolona [km/h], sieć trakcyjna.</summary>
public class OdcinekProfilu
{
    public double Dlugosc, Pochylenie, VMax = 100;
    public bool Siec = true;
    public string Opis = "";
}

/// <summary>Przejazd próbny po profilu (edytor taboru): ten sam maszynista i fizyka co w symulatorze, pociąg rusza z początku i staje na końcu.</summary>
public static class Przejazd
{
    public record Probka(double T, double S, double V, double VDoz, double I, double F, double B);
    public record Wynik(List<Probka> Probki, double Czas, double Droga, bool Dojechal, string Uwaga);

    /// <summary>Pociąg stoi całą długością na początku profilu (czoło w punkcie <see cref="Pociag.Dlugosc"/>) i jedzie do końca profilu.</summary>
    public static Wynik Przejedz(Pociag p, IReadOnlyList<OdcinekProfilu> prof, double warunki = 1, double tMax = 7200)
    {
        double total = prof.Sum(x => x.Dlugosc);
        if (prof.Count == 0 || total <= p.Dlugosc + 1) return new(new(), 0, 0, false, "profil krótszy niż pociąg");
        var starts = new double[prof.Count]; for (int i = 1; i < prof.Count; i++) starts[i] = starts[i - 1] + prof[i - 1].Dlugosc;
        int At(double s) { int k = Array.BinarySearch(starts, s); if (k < 0) k = ~k - 1; return Math.Clamp(k, 0, prof.Count - 1); }
        var st = new StanJazdy(); double head = p.Dlugosc, t = 0;
        var probki = new List<Probka>(); double nextSample = 0; string uwaga = null; double stoi = 0;
        while (t < tMax)
        {
            double tail = head - p.Dlugosc;
            // pochylenie i ograniczenie pod składem
            double iSum = 0, vPod = 1e4;
            for (int k = At(tail); k < prof.Count && starts[k] < head; k++)
            {
                double ov = Math.Min(starts[k] + prof[k].Dlugosc, head) - Math.Max(starts[k], tail);
                if (ov > 0) { iSum += ov * prof[k].Pochylenie; vPod = Math.Min(vPod, prof[k].VMax / 3.6); }
            }
            double ip = iSum / p.Dlugosc;
            int hk = At(head - 1e-6);
            bool siec = prof[hk].Siec;
            var przed = new List<Maszynista.Przed>();
            for (int k = hk; k < prof.Count && starts[k] - head < 3000; k++)
            {
                double od = Math.Max(0, starts[k] - head), len = starts[k] + prof[k].Dlugosc - Math.Max(starts[k], head);
                if (len > 0) przed.Add(new(od, len, prof[k].Pochylenie, k == hk ? 1e4 : prof[k].VMax / 3.6));
            }
            double dStop = total - head;
            double vDoz = Math.Min(p.VMax / 3.6, vPod);
            double cel = Maszynista.Docelowa(p, st.V, vDoz, przed, dStop, siec);
            var u = new Sterowanie { Pochylenie = ip, Siec = siec, Warunki = warunki };
            Maszynista.Steruj(ref u, st.V, cel, dStop);
            double d = Jazda.Krok(p, st, u, Pociag.Krok);
            if (d >= dStop) { d = Math.Max(0, dStop); st.V = 0; }
            head += d; t += Pociag.Krok;
            if (t >= nextSample - 1e-9) { probki.Add(new(t, head - p.Dlugosc, st.V * 3.6, vDoz * 3.6, ip, st.F, st.B)); nextSample += 1; }
            if (head >= total - 0.01) { probki.Add(new(t, head - p.Dlugosc, 0, vDoz * 3.6, ip, 0, st.B)); return new(probki, t, head - p.Dlugosc, true, null); }
            if (st.NieRusza) { stoi += Pociag.Krok; if (stoi > 30) { uwaga = st.BrakZasilania ? "brak sieci trakcyjnej – pociąg nie rusza" : $"pociąg nie może ruszyć na wzniesieniu {ip:0.#} ‰"; break; } }
            else stoi = 0;
        }
        return new(probki, t, head - p.Dlugosc, false, uwaga ?? "przekroczony czas przejazdu");
    }

    /// <summary>Profil z tekstu: wiersze „długość [m]  pochylenie [‰]  Vmax [km/h]  [sieć 1 / 0]”.</summary>
    public static List<OdcinekProfilu> Parsuj(string text, out string blad)
    {
        blad = null; var l = new List<OdcinekProfilu>();
        static bool N(string s, out double v) => double.TryParse(s.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v);
        foreach (var (line, nr) in text.Split('\n').Select((x, i) => (x.Trim(), i + 1)))
        {
            if (line.Length == 0 || line.StartsWith("#")) continue;
            var p = line.Split(new[] { ' ', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length is < 3 or > 4 || !N(p[0], out var len) || !N(p[1], out var i) || !N(p[2], out var v) || len <= 0 || v <= 0 || (p.Length == 4 && p[3] is not ("0" or "1")))
            { blad = $"wiersz {nr}: „{line}” – potrzebne: długość pochylenie Vmax [sieć 1/0]"; return null; }
            l.Add(new OdcinekProfilu { Dlugosc = len, Pochylenie = i, VMax = v, Siec = p.Length < 4 || p[3] == "1" });
        }
        if (l.Count == 0) blad = "profil pusty";
        return l.Count == 0 ? null : l;
    }

    public static string Tekst(IEnumerable<OdcinekProfilu> prof) =>
        string.Join("\n", prof.Select(x => string.Format(System.Globalization.CultureInfo.GetCultureInfo("pl-PL"), "{0:0.#} {1:0.#} {2:0} {3}", x.Dlugosc, x.Pochylenie, x.VMax, x.Siec ? 1 : 0)));
}

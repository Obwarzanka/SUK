namespace Lcs.Powtarzacz;

/// <summary>Dane przykładowe (--demo, zrzuty): cykle zamykania i otwierania jak w grze (P0 2026-11-21) i awaria jednego przejazdu.</summary>
public static class Demo
{
    const int O = Przejazd.CsOpen, Sw = Przejazd.CsSwitch, C = Przejazd.CsClose, Br = Przejazd.CsBroken;

    public static List<Przejazd> Stan(TimeSpan t)
    {
        var w = new List<Przejazd>();
        Przejazd P(string n, bool pul, double faza, bool awaria = false, bool wyl = false)
        {
            var p = new Przejazd { Nazwa = n, NaPulpicie = pul, Uzk = pul, Odebrano = DateTime.Now };
            var m = System.Text.RegularExpressions.Regex.Match(n, @"^L(\d+)_(\d+\.\d+)_([A-F])");
            p.Linia = m.Groups[1].Value; p.Km = double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture); p.Kat = m.Groups[3].Value;
            // cykl 40 s: 0–10 otwarty, 10–12 ostrzeganie, 12–18 rogatki w dół, 18–30 zamknięty (od 20 potwierdzony), 30–36 w górę, 36–40 otwarty
            double s = (t.TotalSeconds + faza) % 40;
            (p.O, p.S, p.Wj, p.Wy) = s switch
            {
                < 10 => (O, O, O, O),
                < 12 => (C, C, O, O),
                < 15 => (C, C, C | Sw, O),
                < 18 => (C, C, C, C | Sw),
                < 30 => (C, C, C, C),
                < 33 => (O, C, C, O | Sw),
                < 36 => (O, C, O | Sw, O),
                _ => (O, O, O, O),
            };
            p.Potwierdzony = s is >= 20 and < 30;
            if (awaria && t.TotalSeconds % 60 is >= 5 and < 45) p.O |= Br;
            if (wyl) p.ToryWyl.Add("1");
            w.Add(p);
            return p;
        }
        P("L1_102.026_B", true, 0);
        P("L1_102.272_B", true, 3);
        P("L17_21.996_A", true, 15);
        P("L1_108.644_B", false, 22, awaria: true);
        P("L1_110.905_B", false, 30, wyl: true);
        return w;
    }
}

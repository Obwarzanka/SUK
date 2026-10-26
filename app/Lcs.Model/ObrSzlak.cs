namespace Lcs.Model;

/// <summary>
/// Szlaki na obrazie w wersji 2 (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 6 pkt 7, etap E6): odstępy SBL szlaku UT liczone po całej drodze szlaku (także przez kilka
/// torów – szlak wewnętrzny między stacjami obszaru LCS kończy się semaforem wjazdowym drugiej stacji), numery pól z nazw semaforów odstępowych,
/// nazwa sąsiada z numerem linii (szlak zewnętrzny – posterunek spoza obszaru z UT, szlak wewnętrzny – druga stacja obszaru; decyzja właściciela
/// 2026-10-22: z UT, do zmiany na obrazie). Bez WPF.
/// </summary>
public static class ObrSzlak
{
    /// <summary>Kawałek drogi szlaku od semafora wjazdowego na zewnątrz: tor, km od – do (kierunek jazdy od stacji), odległość od semafora na początku i końcu [m].</summary>
    public record Kawalek(string Track, double From, double To, double D0, double D1)
    {
        public double Km(double d) => From + Math.Sign(To - From) * (d - D0);
    }

    /// <summary>
    /// Odstępy szlaku: semafor wjazdowy stacji obrazu, droga od niego na zewnątrz (kawałki), długość drogi, granice odstępów (odległość od semafora
    /// i semafory odstępowe w tym miejscu – dwa kierunki w jednym miejscu to jedna granica; ostatnia granica – koniec szlaku).
    /// </summary>
    public record Odstepy(UtDevice Wjazdowy, List<Kawalek> Droga, double L, List<(double D, List<UtDevice> Sem)> Granice)
    {
        /// <summary>Kawałki toru pola między granicami na odległościach d0 – d1 (tor, km od – do, część pola od – do).</summary>
        public IEnumerable<(string Track, double KmA, double KmB, double FA, double FB)> Pole(double d0, double d1)
        {
            foreach (var k in Droga)
            {
                double a = Math.Max(d0, k.D0), b = Math.Min(d1, k.D1);
                if (b - a < 0.05) continue;
                yield return (k.Track, k.Km(a), k.Km(b), (a - d0) / Math.Max(1e-9, d1 - d0), (b - d0) / Math.Max(1e-9, d1 - d0));
            }
        }
    }

    /// <summary>
    /// Odstępy szlaku <paramref name="line"/> przy elemencie odstępów SBL: semafor wjazdowy – koniec szlaku z semaforem na torze pod odstępami (dwa takie –
    /// ten na posterunku obrazu); droga od niego na zewnątrz po kawałkach szlaku (kolejne kawałki łączą się w najbliższych końcach). Null – brak.
    /// </summary>
    public static Odstepy OdstepyUt(UtProject ut, ObrObraz o, ObrElement el, string line)
    {
        var ln = ut.Lines.FirstOrDefault(x => x.Id == line); if (ln == null) return null;
        var seg = el.Na == null ? null : ObrGraf.Przy(o, el.Na).FirstOrDefault();
        var track = seg == null ? null : o.Przypiecia.FirstOrDefault(p => p.El == seg.Id)?.Tor?.Tor;
        var droga = Szlaki.Drogi(ut).GetValueOrDefault(line);
        if (track == null || droga == null || droga.Kawalki.Count == 0) return null;
        var konce = ln.Konce.Select((k, i) => (I: i, D: k.Semafor == null ? null : ut.Devices.FirstOrDefault(d => d.Id == k.Semafor))).Where(x => x.D != null).ToList();
        var posty = o.Posterunki.ToHashSet();
        // semafor wjazdowy stacji obrazu: na torze pod odstępami albo na posterunku obrazu (szlak zaczyna się na torze stacji, rząd leży na torze szlakowym)
        // szlak wewnętrzny z obiema stacjami na obrazie: najpierw semafor narysowany najbliżej wewnętrznego końca rzędu (pola idą w stronę jego stacji)
        var pz = ObrSymbole.Pozycja(o, el); int np = el.Pola ?? 0;
        double Odl(UtDevice d)
        {
            var se = o.Elementy.FirstOrDefault(x => x.Typ == ObrSymbole.Semafor && o.Przypiecia.Any(p => p.El == x.Id && p.Ut == d.Id));
            if (se == null || pz is not var (x0, y0, ux, uy) || ObrSymbole.Pozycja(o, se) is not var (sx, sy, _, _)) return double.MaxValue;
            return Dist(new XY(sx, sy), new XY(x0 + ux * np * ObrSymbole.PoleSbl, y0 + uy * np * ObrSymbole.PoleSbl));
        }
        var wjk = konce.Where(x => x.D.Track == track || posty.Contains(ut.Tracks.FirstOrDefault(t => t.Id == x.D.Track)?.Post ?? ""))
            .OrderBy(x => Odl(x.D)).ThenBy(x => x.D.Track == track ? 0 : 1).ThenBy(x => posty.Contains(ut.Tracks.FirstOrDefault(t => t.Id == x.D.Track)?.Post ?? "") ? 0 : 1)
            .FirstOrDefault();
        if (wjk.D == null) return null;
        var wj = wjk.D;
        // kawałki w kolejności od semafora wjazdowego na zewnątrz
        var kaw = droga.Kawalki.ToList();
        if (wjk.I == ln.Konce.Count - 1 && kaw.Count > 1) kaw.Reverse();
        var r = ut.Tracks.ToDictionary(t => t.Id, t => Trasa.Compute(t));
        XY P(string tr, double s) => r.TryGetValue(tr, out var rr) && rr.Elements.Count > 0 ? rr.At(Math.Clamp(s, 0, rr.Length)).P : new XY(0, 0);
        var res = new List<Kawalek>(); double d = 0; XY prev = P(wj.Track, wj.S);
        foreach (var k in kaw)
        {
            // wejście kawałka: koniec bliższy poprzedniemu wyjściu (pierwszy – koniec przy semaforze wjazdowym)
            bool odS0 = res.Count == 0 ? Math.Abs(k.S0 - wj.S) <= Math.Abs(k.S1 - wj.S) : Dist(P(k.Track, k.S0), prev) <= Dist(P(k.Track, k.S1), prev);
            double from = odS0 ? k.S0 : k.S1, to = odS0 ? k.S1 : k.S0;
            if (res.Count == 0) from = wj.S;
            double len = Math.Abs(to - from);
            res.Add(new Kawalek(k.Track, from, to, d, d + len));
            d += len; prev = P(k.Track, to);
        }
        var gr = new List<(double D, List<UtDevice> Sem)> { (0, new()) };
        foreach (var k in res)
        {
            double lo = Math.Min(k.From, k.To), hi = Math.Max(k.From, k.To);
            foreach (var dev in ut.Devices.Where(x => x.Track == k.Track && x.Kind == UtDevice.Semafor && x.Funkcja == UtDevice.FOdstepowy && x.S > lo + 0.5 && x.S < hi - 0.5)
                         .OrderBy(x => Math.Abs(x.S - k.From)))
            {
                double dd = k.D0 + Math.Abs(dev.S - k.From);
                if (Math.Abs(gr[^1].D - dd) < 0.5) gr[^1].Sem.Add(dev); else gr.Add((dd, new() { dev }));
            }
        }
        gr.Add((d, new()));
        return new Odstepy(wj, res, d, gr);
    }

    static double Dist(XY a, XY b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>Numery pól odstępów z UT po kolei od stacji: nazwa semafora odstępowego na zewnętrznym końcu pola (dwa w jednym miejscu – „a/b”).</summary>
    public static List<string> Numery(UtProject ut, ObrObraz o, ObrElement el, string line)
    {
        int n = (el.Pola ?? 0) + Math.Max(0, ObrSymbole.Rozmiar(el)[0]);
        var od = OdstepyUt(ut, o, el, line);
        var res = new List<string>();
        for (int i = 0; i < n; i++)
            res.Add(od != null && i + 1 < od.Granice.Count - 1 ? string.Join("/", od.Granice[i + 1].Sem.Select(d => d.Name).Where(x => x.Length > 0)) : "");
        return res;
    }

    /// <summary>
    /// Nazwa sąsiada szlaku z numerem linii („Pruszków LK 447”): koniec szlaku bez semafora wjazdowego (posterunek spoza obszaru), a szlak wewnętrzny
    /// obszaru LCS (oba końce z semaforami) – stacja drugiego końca (nie posterunek obrazu <paramref name="o"/>).
    /// </summary>
    public static string NazwaSasiada(UtProject ut, string line, ObrObraz o = null)
    {
        var ln = ut.Lines.FirstOrDefault(x => x.Id == line); if (ln == null) return null;
        string LK(string n) => n + (ln.Linia.Length > 0 ? $" LK {ln.Linia}" : "");
        if (ln.Konce.FirstOrDefault(x => x.Semafor == null && x.Sasiad.Length > 0) is UtLineEnd zew) return LK(zew.Sasiad);
        var posty = o?.Posterunki.ToHashSet() ?? new();
        var stacje = ln.Konce.Where(k => k.Semafor != null).Select(k => Posterunki.Of(ut, ut.Devices.FirstOrDefault(d => d.Id == k.Semafor)?.Track)).Where(p => p != null).ToList();
        var inna = stacje.FirstOrDefault(p => !posty.Contains(p.Id)) ?? (o == null ? stacje.LastOrDefault() : null);
        if (inna != null) return LK(inna.Nazwa);
        return ln.Konce.FirstOrDefault(x => x.Sasiad.Length > 0) is UtLineEnd k2 ? LK(k2.Sasiad) : null;
    }

    /// <summary>Szlak najbliższego przypiętego elementu szlaku (strzałka blokady, odstępy SBL) do punktu obrazu; null – brak.</summary>
    public static string SzlakPrzy(ObrObraz o, double x, double y)
    {
        var pr = o.Przypiecia.Where(p => p.Ut != null && p.Stan is ObrPrzypiecie.Pewne or ObrPrzypiecie.Potwierdzone).ToDictionary(p => p.El, p => p.Ut);
        return o.Elementy.Where(e => e.Typ is ObrSymbole.StrzalkaBlokady or ObrSymbole.OdstepySbl && pr.ContainsKey(e.Id))
            .Select(e => (e, d: ObrSymbole.Pozycja(o, e) is var (ex, ey, _, _) ? Math.Sqrt((ex - x) * (ex - x) + (ey - y) * (ey - y)) : double.MaxValue))
            .OrderBy(z => z.d).Select(z => pr[z.e.Id]).FirstOrDefault();
    }

    /// <summary>
    /// Opisy szlaku z UT tam, gdzie człowiek nic nie wpisał (po F8): nazwa sąsiada w pustym napisie „sąsiad”, numery pustych pól odstępów.
    /// Wpisane teksty zostają (decyzja właściciela 2026-10-22, plan § 11 pkt 4 i 6).
    /// </summary>
    public static void UzupelnijZUt(UtProject ut, ObrObraz o)
    {
        foreach (var el in o.Elementy.Where(e => e.Typ == ObrSymbole.Napis && e.Styl == ZobNapis.Sasiad && e.P != null && e.Opisy.Count > 0 && e.Opisy[0].Tekst.Length == 0))
            if (SzlakPrzy(o, el.P[0], el.P[1]) is string l && NazwaSasiada(ut, l, o) is string n) el.Opisy[0].Tekst = n;
        foreach (var el in o.Elementy.Where(e => e.Typ == ObrSymbole.OdstepySbl))
        {
            var line = o.Przypiecia.FirstOrDefault(p => p.El == el.Id && p.Stan is ObrPrzypiecie.Pewne or ObrPrzypiecie.Potwierdzone)?.Ut;
            if (line == null) continue;
            var nr = Numery(ut, o, el, line);
            for (int i = 0; i < el.Opisy.Count && i < nr.Count; i++) if (el.Opisy[i].Tekst.Length == 0) el.Opisy[i].Tekst = nr[i];
        }
    }
}

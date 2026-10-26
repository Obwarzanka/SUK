namespace Lcs.Model;

/// <summary>
/// „Kompilacja” obrazu w wersji 2 do <see cref="ZobProject"/> (wersja 1) w pamięci (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 4, etap E5): na tej podstawie
/// działają bez zmian <see cref="DoSymulatora"/> i podgląd pulpitu. Tory: kolejne odcinki łańcucha przypięte do tego samego toru UT, z ciągłym km
/// i jednym stylem – jedna łamana <see cref="ZobTor"/>; km na końcu łamanej w rozjeździe – dokładnie km rozjazdu z UT. Symbole – w swoim punkcie
/// (zaokrąglonym do j.), opisy przesunięte – w <see cref="ZobSymbol.Opis"/>. Elementy bez przypięcia (pewnego albo potwierdzonego) – pominięte.
/// </summary>
public static class ObrKompilacja
{
    public record Wynik(ZobProject Zob, List<string> Uwagi);

    public static Wynik Kompiluj(UtProject ut, ObrProject p)
    {
        var uw = new List<string>();
        var z = new ZobProject { Ut = p.Ut, UtZapis = p.UtZapis, Siatka = p.Siatka };
        var len = ut.Tracks.ToDictionary(t => t.Id, t => Trasa.Compute(t).Length);
        foreach (var o in p.Obrazy)
        {
            var zo = new ZobObraz { Id = o.Id, Nazwa = o.Nazwa, Rodzaj = o.Rodzaj, Posterunki = o.Posterunki.ToList(), Szer = o.Szer, Wys = o.Wys, OpisyOdcinkow = o.OpisyOdcinkow };
            z.Obrazy.Add(zo);
            var pr = o.Przypiecia.Where(x => x.Stan is ObrPrzypiecie.Pewne or ObrPrzypiecie.Potwierdzone).ToDictionary(x => x.El, x => x);
            int pominiete = 0;
            // ---------- tory
            double? KmRozjazdu(string wezel, string track)
            {
                if (!pr.TryGetValue(wezel, out var q) || q.Ut == null) return null;
                // koniec rozjazdu krzyżowego (ab / cd) – km na obu torach z UT
                if (Krzyzowe.Parse(ut, q.Ut) is var (kc, kk)) return Krzyzowe.Wezly(ut, kc, kk).Where(x => x.Track == track).Select(x => (double?)x.S).FirstOrDefault();
                if (ut.Turnouts.FirstOrDefault(t => t.Id == q.Ut) is not UtTurnout zw) return null;
                if (zw.Track == track) return zw.S;
                if (zw.Branch == track) return zw.BranchAtEnd ? len.GetValueOrDefault(track) : 0;
                return null;
            }
            // km torów poza obrazem zebrane w punkcie przy krawędzi (z obrazu starego edytora)
            foreach (var w in o.Wezly.Where(w => w.Poza != null))
                foreach (var t in w.Poza) zo.Tory.Add(new ZobTor { Ut = new ZobUtTor { Tor = t.Tor, S0 = t.S0, S1 = t.S1 }, Pkt = new() { w.P.ToArray(), w.P.ToArray() } });
            foreach (var l in ObrPrzypinanie.Lancuchy(o))
            {
                ZobTor cur = null; double lastKm = 0;
                for (int i = 0; i < l.Odc.Count; i++)
                {
                    var (e, fwd) = l.Odc[i];
                    if (!pr.TryGetValue(e.Id, out var q) || q.Tor?.Tor == null) { cur = null; pominiete++; continue; }
                    double k0 = fwd ? q.Tor.S0 : q.Tor.S1, k1 = fwd ? q.Tor.S1 : q.Tor.S0;
                    bool bk = e.Styl == ObrOdcinek.BezKontroli;
                    bool dalej = cur != null && cur.Ut.Tor == q.Tor.Tor && cur.BezKontroli == bk && Math.Abs(lastKm - k0) < 0.5
                        && Math.Sign(cur.Ut.S1 - cur.Ut.S0) * Math.Sign(k1 - k0) >= 0;
                    if (!dalej)
                    {
                        cur = new ZobTor { Ut = new ZobUtTor { Tor = q.Tor.Tor, S0 = KmRozjazdu(l.Wezly[i], q.Tor.Tor) ?? k0 }, BezKontroli = bk };
                        cur.Pkt.Add(Pkt(o, l.Wezly[i]));
                        zo.Tory.Add(cur);
                    }
                    cur.Pkt.Add(Pkt(o, l.Wezly[i + 1]));
                    cur.Ut.S1 = KmRozjazdu(l.Wezly[i + 1], q.Tor.Tor) ?? k1;
                    lastKm = k1;
                }
            }
            if (pominiete > 0) uw.Add($"{o.Nazwa}: {pominiete} odcinków toru bez przypięcia – na pulpicie ich nie ma");
            if (ObrPrzypinanie.KmNieaktualne(ut, o).Count is int nk && nk > 0) uw.Add($"{o.Nazwa}: {nk} odcinków toru z km nieaktualnym (UT zmieniony po F8) – na pulpicie tory w złym miejscu; „Przypnij do UT” (F8)");
            // ---------- symbole
            int[] Opis(int[] p0, ObrElement el) => el.Opisy.FirstOrDefault() is ObrOpis d && !d.D.SequenceEqual(ObrSymbole.OpisDomyslny(o, el)) ? new[] { p0[0] + d.D[0], p0[1] + d.D[1] } : null;
            foreach (var w in o.Wezly.Where(w => pr.TryGetValue(w.Id, out var q) && q.Ut != null))
            {
                var zel = ObrSymbole.ZwrotnicaW(o, w.Id);
                zo.Zwrotnice.Add(new ZobSymbol { Ut = pr[w.Id].Ut, P = w.P.ToArray(), Opis = zel != null ? Opis(w.P, zel) : null });
            }
            foreach (var el in o.Elementy)
            {
                if (el.Typ is ObrSymbole.Zwrotnica or ObrGraf.Skrzyzowanie or ObrGraf.RozjazdKrzyzowy) continue;
                var q = pr.GetValueOrDefault(el.Id);
                if (el.Typ == ObrSymbole.Napis)
                {
                    if (el.P != null && el.Opisy.FirstOrDefault()?.Tekst is string tn && tn.Length > 0)
                        zo.Napisy.Add(new ZobNapis { Id = el.Id, Tekst = tn, P = el.P.ToArray(), Rodzaj = el.Styl ?? ZobNapis.Dowolny });
                    continue;
                }
                if (q?.Ut == null) { pominiete++; continue; }
                if (el.Typ == ObrSymbole.Peron)
                {
                    var r = ObrSymbole.Rozmiar(el);
                    if (el.P != null) zo.Perony.Add(new ZobPeron { Ut = q.Ut, Prost = new[] { el.P[0], el.P[1], r[0], r[1] } });
                    continue;
                }
                if (ObrSymbole.Pozycja(o, el) is not var (x, y, _, _)) continue;
                var pt = new[] { (int)Math.Round(x), (int)Math.Round(y) };
                switch (el.Typ)
                {
                    case ObrSymbole.Semafor: case ObrSymbole.Tarcza: case ObrSymbole.Powtarzacz: case ObrSymbole.Pk:
                        zo.Sygnalizatory.Add(new ZobSymbol { Ut = q.Ut, P = pt, Opis = Opis(pt, el), BezKonca = el.Styl == ObrSymbole.SemWjazdowyBez ? true : null }); break;
                    case ObrSymbole.Wykolejnica: zo.Wykolejnice.Add(new ZobSymbol { Ut = q.Ut, P = pt, Opis = Opis(pt, el) }); break;
                    case ObrSymbole.Koziol: zo.Kozly.Add(new ZobSymbol { Ut = q.Ut, P = pt }); break;
                    case ObrSymbole.Przerwa: zo.Przerwy.Add(new ZobSymbol { Ut = q.Ut, P = pt }); break;
                    case ObrSymbole.Przejazd:
                        {
                            var r = ObrSymbole.Rozmiar(el);
                            zo.Przejazdy.Add(new ZobSymbol { Ut = q.Ut, P = pt, Zasieg = r[0] == 28 && r[1] == 28 ? null : r.ToArray(), Opis = Opis(pt, el) });
                            break;
                        }
                    case ObrSymbole.NumerToru:
                        {
                            if (ObrSymbole.Ukryty(el)) break;      // niewidoczny – tylko do przypięcia
                            var d = el.Opisy.FirstOrDefault()?.D ?? new[] { 0, 0 };
                            zo.NumeryTorow.Add(new ZobSymbol { Ut = q.Ut, P = new[] { pt[0] + d[0], pt[1] + d[1] } });
                            break;
                        }
                    case ObrSymbole.OpisOdcinka:
                        {
                            var d = el.Opisy.FirstOrDefault()?.D ?? new[] { 0, 0 };
                            zo.Odcinki.Add(new ZobSymbol { Ut = q.Ut, P = new[] { pt[0] + d[0], pt[1] + d[1] } });
                            break;
                        }
                    case ObrSymbole.StrzalkaBlokady: zo.Szlaki.Add(new ZobSymbol { Ut = q.Ut, P = pt, Koniec = el.Pola ?? KoniecSzlaku(ut, q.Ut, o, el) }); break;
                }
            }
            // ---------- szlaki: odstępy SBL i okna numeru pociągu (E6)
            foreach (var el in o.Elementy.Where(e => e.Typ == ObrSymbole.OdstepySbl && pr.GetValueOrDefault(e.Id)?.Ut != null))
                Odstepy(ut, o, zo, el, pr[el.Id].Ut, len, uw);
            foreach (var el in o.Elementy.Where(e => e.Typ == ObrSymbole.PolePip && e.P != null && pr.GetValueOrDefault(e.Id)?.Ut != null))
                (zo.PolaPip ??= new()).Add(new ZobSymbol { Ut = pr[el.Id].Ut, P = el.P.ToArray(), Zasieg = ObrSymbole.Rozmiar(el).ToArray() });
            // „bez przypięcia” wybrane przez człowieka – świadomie poza symulatorem, bez uwagi
            var brak = o.Przypiecia.Where(x => x.Stan == ObrPrzypiecie.Brak).Select(x => x.El).ToHashSet();
            int bezEl = o.Elementy.Count(el => el.Typ is not (ObrSymbole.Zwrotnica or ObrSymbole.Napis or ObrGraf.Skrzyzowanie or ObrGraf.RozjazdKrzyzowy) && pr.GetValueOrDefault(el.Id)?.Ut == null && !brak.Contains(el.Id));
            if (bezEl > 0) uw.Add($"{o.Nazwa}: {bezEl} symboli bez przypięcia – na pulpicie ich nie ma (F8, lista „do wyjaśnienia”)");
        }
        return new Wynik(z, uw);
    }

    static int[] Pkt(ObrObraz o, string wezel) => ObrGraf.Wezel(o, wezel).P.ToArray();
    static int[] R(double x, double y) => new[] { (int)Math.Round(x), (int)Math.Round(y) };

    /// <summary>
    /// Odstępy SBL szlaku <paramref name="line"/> (etap E6): odstępy z UT po całej drodze szlaku – od semafora wjazdowego stacji obrazu przez semafory
    /// odstępowe do końca szlaku (szlak wewnętrzny obszaru LCS – do semafora wjazdowego drugiej stacji, także na innym torze UT); tor obrazu na drodze
    /// szlaku zastępują kreski pól (osobne <see cref="ZobTor"/> z km odstępu, pole na dwóch torach – dwa kawałki kreski), semafor odstępowy – na końcu
    /// swojego pola (pulpit pisze nad nim numer). Pole bez odstępu w UT – tylko napis z obrazu (uwaga).
    /// </summary>
    static void Odstepy(UtProject ut, ObrObraz o, ZobObraz zo, ObrElement el, string line, Dictionary<string, double> len, List<string> uw)
    {
        var ln = ut.Lines.FirstOrDefault(x => x.Id == line); if (ln == null) return;
        if (ObrSzlak.OdstepyUt(ut, o, el, line) is not ObrSzlak.Odstepy od) { uw.Add($"{o.Nazwa}: odstępy SBL szlaku {ln.Numer} – brak semafora wjazdowego szlaku na torze pod odstępami, pominięte"); return; }
        var wj = od.Wjazdowy; var gr = od.Granice;
        // tor obrazu na drodze szlaku – zastąpiony polami; przecięcie w km semafora wjazdowego – w punkcie jego symbolu
        foreach (var k in od.Droga)
        {
            double lo = Math.Min(k.From, k.To), hi = Math.Max(k.From, k.To);
            foreach (var zt in zo.Tory.Where(t => t.Ut.Tor == k.Track).ToList())
            {
                double a = zt.Ut.S0, b = zt.Ut.S1, zlo = Math.Min(a, b), zhi = Math.Max(a, b);
                if (Math.Min(zhi, hi) - Math.Max(zlo, lo) < 0.5) continue;
                if (zlo >= lo - 0.5 && zhi <= hi + 0.5) { zo.Tory.Remove(zt); continue; }
                double F(double km)
                {
                    var semP = Math.Abs(km - wj.S) < 0.5 && k.Track == wj.Track ? zo.Sygnalizatory.FirstOrDefault(s => s.Ut == wj.Id && ZobSymbole.NaTorze(zt, s.P))?.P : null;
                    return semP != null ? ZobTory.Rzut(zt.Pkt, semP[0], semP[1]).Frac : (km - a) / (b - a);
                }
                bool aIn = a > lo - 0.5 && a < hi + 0.5, bIn = b > lo - 0.5 && b < hi + 0.5;
                if (aIn) { double c = b > hi ? hi : lo; zt.Pkt = Ucinek(zt.Pkt, F(c), 1); zt.Ut.S0 = c; }
                else if (bIn) { double c = a < lo ? lo : hi; zt.Pkt = Ucinek(zt.Pkt, 0, F(c)); zt.Ut.S1 = c; }
                else
                {
                    // droga szlaku w środku łamanej – dwie części
                    double c1 = a < b ? lo : hi, c2 = a < b ? hi : lo;
                    var reszta = new ZobTor { Ut = new ZobUtTor { Tor = zt.Ut.Tor, S0 = c2, S1 = b }, Pkt = Ucinek(zt.Pkt, F(c2), 1), BezKontroli = zt.BezKontroli };
                    zt.Pkt = Ucinek(zt.Pkt, 0, F(c1)); zt.Ut.S1 = c1;
                    zo.Tory.Add(reszta);
                }
            }
        }
        var pola = ObrSymbole.PolaSbl(o, el);
        // pierwsze pole rzędu zaczyna się w punkcie semafora wjazdowego – węzeł semafora na pulpicie trafia w symbol (tor pod strzałką
        // blokady pulpit i tak wycina); pole: od strony stacji (I) na zewnątrz (O)
        var wjP = zo.Sygnalizatory.FirstOrDefault(s => s.Ut == wj.Id)?.P;
        for (int i = 0; i < pola.Count; i++)
        {
            var (ox, oy, ix, iy, kol) = pola[i];
            if (i == 0 && !kol && wjP != null) { ix = wjP[0]; iy = wjP[1]; }
            var opis = i < el.Opisy.Count ? el.Opisy[i] : null;
            if (i < gr.Count - 1)
            {
                // ostatnie pole bierze resztę szlaku
                double d0 = gr[i].D, d1 = i == pola.Count - 1 ? od.L : gr[i + 1].D;
                foreach (var (tr, kmA, kmB, fa, fb) in od.Pole(d0, d1))
                    zo.Tory.Add(new ZobTor { Ut = new ZobUtTor { Tor = tr, S0 = kmA, S1 = kmB }, Pkt = new() { R(ix + (ox - ix) * fa, iy + (oy - iy) * fa), R(ix + (ox - ix) * fb, iy + (oy - iy) * fb) } });
                if (i + 1 < gr.Count - 1 && i != pola.Count - 1)
                    foreach (var d in gr[i + 1].Sem) zo.Sygnalizatory.Add(new ZobSymbol { Ut = d.Id, P = R(ox, oy) });
            }
            else if (opis != null && opis.Tekst.Length > 0)
                zo.Napisy.Add(new ZobNapis { Tekst = opis.Tekst, P = R((ix + ox) / 2 + opis.D[0], (iy + oy) / 2 + opis.D[1]), Rodzaj = ZobNapis.Dowolny });
        }
        if (pola.Count > gr.Count - 1) uw.Add($"{o.Nazwa}: szlak {ln.Numer} – pól odstępów {pola.Count}, odstępów w UT {gr.Count - 1}; pola bez odstępu tylko z napisem");
    }

    /// <summary>Fragment łamanej między częściami długości f0 ≤ f1 (punkty w j., zaokrąglone).</summary>
    static List<int[]> Ucinek(List<int[]> p, double f0, double f1)
    {
        var cum = new List<double> { 0 };
        for (int i = 1; i < p.Count; i++) cum.Add(cum[^1] + Math.Sqrt(Math.Pow(p[i][0] - p[i - 1][0], 2) + Math.Pow(p[i][1] - p[i - 1][1], 2)));
        double total = cum[^1];
        int[] At(double f)
        {
            double L = Math.Clamp(f, 0, 1) * total;
            for (int i = 1; i < p.Count; i++)
                if (L <= cum[i] + 1e-9) { double s = cum[i] - cum[i - 1], t = s < 1e-9 ? 0 : (L - cum[i - 1]) / s; return R(p[i - 1][0] + (p[i][0] - p[i - 1][0]) * t, p[i - 1][1] + (p[i][1] - p[i - 1][1]) * t); }
            return p[^1].ToArray();
        }
        var res = new List<int[]> { At(f0) };
        for (int i = 1; i < p.Count - 1; i++) { double f = total > 0 ? cum[i] / total : 0; if (f > f0 + 1e-9 && f < f1 - 1e-9) res.Add(p[i].ToArray()); }
        res.Add(At(f1));
        return res;
    }

    /// <summary>Koniec szlaku przy strzałce: koniec z semaforem wjazdowym na torze strzałki (pierwszy taki), inaczej 0.</summary>
    public static int KoniecSzlaku(UtProject ut, string line, ObrObraz o, ObrElement el)
    {
        // szlak wewnętrzny obszaru LCS: oba końce z semaforami wjazdowymi, strzałka zwykle na torze szlakowym (nie na torze semafora) – koniec
        // z semaforem narysowanym najbliżej strzałki, potem semafor na posterunku obrazu, potem na torze pod strzałką
        var ln = ut.Lines.FirstOrDefault(x => x.Id == line); if (ln == null) return 0;
        var tor = o.Przypiecia.FirstOrDefault(x => x.El == el.Na)?.Tor?.Tor;
        var posty = o.Posterunki.ToHashSet();
        var poz = ObrSymbole.Pozycja(o, el);
        var pr = o.Przypiecia.Where(p => p.Ut != null && p.Stan is ObrPrzypiecie.Pewne or ObrPrzypiecie.Potwierdzone).ToList();
        double Odl(string sem)
        {
            if (poz is not var (x, y, _, _)) return double.MaxValue;
            return pr.Where(p => p.Ut == sem).Select(p => o.Elementy.FirstOrDefault(e => e.Id == p.El)).Where(e => e != null)
                .Select(e => ObrSymbole.Pozycja(o, e) is var (ex, ey, _, _) ? Math.Sqrt((ex - x) * (ex - x) + (ey - y) * (ey - y)) : double.MaxValue).DefaultIfEmpty(double.MaxValue).Min();
        }
        var konce = ln.Konce.Select((k, i) => (i, k.Semafor, D: k.Semafor == null ? null : ut.Devices.FirstOrDefault(d => d.Id == k.Semafor))).Where(z => z.D != null).ToList();
        if (konce.Count == 0) return 0;
        return konce.OrderBy(z => Odl(z.Semafor))
            .ThenBy(z => posty.Contains(ut.Tracks.FirstOrDefault(t => t.Id == z.D.Track)?.Post ?? "") ? 0 : 1)
            .ThenBy(z => z.D.Track == tor ? 0 : 1).First().i;
    }
}

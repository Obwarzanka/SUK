namespace Lcs.Model;

/// <summary>Uwaga „Sprawdź”: błąd albo ostrzeżenie, element rysunku (albo obiekt UT), akcja do zaproponowania (<see cref="ObrSprawdz.WezZUt"/>).</summary>
public record ObrUwaga(bool Blad, string Tekst, string El, string Akcja = null);

/// <summary>
/// „Sprawdź” (F7) obrazu w wersji 2 (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 9): uwagi rysunku (co 45°, kształt węzłów, symbole bez nazw),
/// przypięcia (element bez przypięcia, obiekt usunięty z UT, km nieaktualne po zmianie UT, km dwa razy na obrazie), opis różny od nazwy w UT („Weź z UT” – decyzja
/// właściciela 2026-10-22: opis może być inny, ale jest uwaga), element w strefie zarezerwowanej, Ie-104 § 4 ust. 15 (odstęp torów,
/// tarcza obok wykolejnicy), obraz poglądowy (§ 6 ust. 4–5), obiekty UT nieumieszczone. Bez WPF.
/// </summary>
public static class ObrSprawdz
{
    public const string WezZUt = "wezZUt";

    /// <summary>Opis elementu rysunku do list (odcinek toru, rozjazd 12, semafor A…).</summary>
    public static string Opis(ObrObraz o, string id)
    {
        if (ObrGraf.Odcinek(o, id) is ObrOdcinek e) { var (a, b) = ObrGraf.Konce(o, e); return $"tor ({a[0]}, {a[1]})–({b[0]}, {b[1]})"; }
        if (ObrGraf.Wezel(o, id) is ObrWezel w)
        {
            var nr = ObrSymbole.ZwrotnicaW(o, id)?.Opisy.FirstOrDefault()?.Tekst;
            return ObrGraf.RodzajWezla(o, id) == ObrGraf.Rodzaj.Rozjazd ? $"rozjazd {(string.IsNullOrEmpty(nr) ? "bez numeru" : nr)} ({w.P[0]}, {w.P[1]})" : $"węzeł ({w.P[0]}, {w.P[1]})";
        }
        if (o.Elementy.FirstOrDefault(x => x.Id == id) is ObrElement el)
        {
            var t = el.Opisy.FirstOrDefault()?.Tekst;
            return $"{ObrSymbole.NazwaTypu(el.Typ)} {t}".Trim();
        }
        return id;
    }

    /// <summary>Elementy rysunku wymagające przypięcia: odcinki, rozjazdy, symbole i opisy (bez napisów, numerów zwrotnic i skrzyżowań).</summary>
    public static IEnumerable<string> DoPrzypiecia(ObrObraz o) =>
        o.Odcinki.Select(e => e.Id)
            .Concat(o.Wezly.Where(w => ObrGraf.RodzajWezla(o, w.Id) == ObrGraf.Rodzaj.Rozjazd).Select(w => w.Id))
            .Concat(o.Elementy.Where(e => e.Typ is not (ObrSymbole.Napis or ObrSymbole.Zwrotnica or ObrGraf.Skrzyzowanie or ObrGraf.RozjazdKrzyzowy)).Select(e => e.Id));

    public static List<ObrUwaga> Sprawdz(UtProject ut, ObrObraz o)
    {
        var res = new List<ObrUwaga>();
        foreach (var (el, t) in ObrGraf.Uwagi(o)) res.Add(new(false, $"{Opis(o, el)}: {t}", el));
        var pr0 = o.Przypiecia.Where(p => p.Ut != null && p.Stan is ObrPrzypiecie.Pewne or ObrPrzypiecie.Potwierdzone).ToDictionary(p => p.El, p => p.Ut);
        foreach (var (el, t) in ObrSymbole.Uwagi(o))
        {
            // rozjazd bez numeru przypięty (np. z sąsiedztwa) – numer z UT jednym przyciskiem
            if (ObrGraf.Wezel(o, el) != null && pr0.TryGetValue(el, out var zid) && ObrPrzypinanie.NazwaUt(ut, zid) is string nr && nr.Length > 0)
                res.Add(new(false, $"{t.Replace(" (E albo PPM)", "")} – w UT rozjazd {nr}", el, WezZUt));
            else res.Add(new(false, t, el));
        }
        // przypięcia
        var pr = o.Przypiecia.ToDictionary(p => p.El, p => p);
        if (o.Przypiecia.Count == 0) res.Add(new(true, "obraz nie jest przypięty do UT – „Przypnij do UT” (F8)", null));
        else
            foreach (var id in DoPrzypiecia(o))
            {
                if (!pr.TryGetValue(id, out var p)) res.Add(new(true, $"{Opis(o, id)}: bez przypięcia – F8", id));
                else if (p.Stan == ObrPrzypiecie.DoWyjasnienia) res.Add(new(true, $"{Opis(o, id)}: do wyjaśnienia – {p.Powod}", id));
                else if (p.Stan != ObrPrzypiecie.Brak && p.Ut != null && ObrPrzypinanie.Opis(ut, p.Ut) == "(nie ma w UT)")
                    res.Add(new(true, $"{Opis(o, id)}: przypięty do obiektu, którego nie ma w UT – F8", id));
                else if (p.Tor?.Tor != null && !ut.Tracks.Any(t => t.Id == p.Tor.Tor)) res.Add(new(true, $"{Opis(o, id)}: tor UT usunięty – F8", id));
            }
        // km nieaktualne (UT albo rysunek zmieniony po F8) – jedna uwaga na łańcuch i tor UT, km od początku do końca łańcucha
        var nieakt = ObrPrzypinanie.KmNieaktualne(ut, o).ToDictionary(k => k.El);
        if (nieakt.Count > 0)
            foreach (var l in ObrPrzypinanie.Lancuchy(o))
            foreach (var g in l.Odc.Where(z => nieakt.ContainsKey(z.E.Id)).Select(z => (z.Fwd, K: nieakt[z.E.Id])).GroupBy(z => z.K.Tor))
            {
                var k = g.ToList();
                var (f0, a) = k[0]; var (f1, b) = k[^1];
                string Km(double s0, double s1) => double.IsNaN(s0) ? "" : $"km {s0:0}–{s1:0}";
                string stare = Km(f0 ? a.S0 : a.S1, f1 ? b.S1 : b.S0), nowe = Km(f0 ? a.N0 : a.N1, f1 ? b.N1 : b.N0);
                res.Add(new(true, $"{ObrPrzypinanie.Opis(ut, a.Tor)}: {stare} nieaktualne – " + (nowe.Length > 0 ? $"wg UT {nowe}" : "po F8 przypięcie inne") + " (UT albo rysunek zmieniony) – F8", a.El));
            }
        // opis różny od nazwy w UT
        foreach (var p in o.Przypiecia.Where(p => p.Ut != null && p.Stan is ObrPrzypiecie.Pewne or ObrPrzypiecie.Potwierdzone))
        {
            var naz = ObrPrzypinanie.NazwaUt(ut, p.Ut);
            if (string.IsNullOrEmpty(naz)) continue;
            var el = ObrGraf.Wezel(o, p.El) != null ? ObrSymbole.ZwrotnicaW(o, p.El) : o.Elementy.FirstOrDefault(x => x.Id == p.El);
            if (el?.Opisy.FirstOrDefault() is not ObrOpis d || el.Typ is ObrSymbole.Peron) continue;
            if (!string.Equals(d.Tekst?.Trim(), naz.Trim(), StringComparison.OrdinalIgnoreCase))
                res.Add(new(false, $"{Opis(o, p.El)}: opis „{d.Tekst}”, w UT „{naz}”", el.Id, WezZUt));
        }
        // szlak (E6): nazwa sąsiada i numery odstępów różne od UT
        foreach (var el in o.Elementy.Where(e => e.Typ == ObrSymbole.Napis && e.Styl == ZobNapis.Sasiad && e.P != null && e.Opisy.Count > 0))
            if (ObrSzlak.SzlakPrzy(o, el.P[0], el.P[1]) is string l && ObrSzlak.NazwaSasiada(ut, l, o) is string n && !string.Equals(el.Opisy[0].Tekst.Trim(), n, StringComparison.OrdinalIgnoreCase))
                res.Add(new(false, $"nazwa sąsiada „{el.Opisy[0].Tekst}”, w UT „{n}”", el.Id, WezZUt));
        foreach (var el in o.Elementy.Where(e => e.Typ == ObrSymbole.OdstepySbl && pr.TryGetValue(e.Id, out var p) && p.Ut != null && p.Stan != ObrPrzypiecie.Brak))
        {
            var nr = ObrSzlak.Numery(ut, o, el, pr[el.Id].Ut);
            int bez = nr.Count(x => x.Length == 0);
            if (Enumerable.Range(0, Math.Min(nr.Count, el.Opisy.Count)).Any(i => nr[i].Length > 0 && !string.Equals(el.Opisy[i].Tekst.Trim(), nr[i], StringComparison.OrdinalIgnoreCase)))
                res.Add(new(false, $"odstępy SBL: numery „{string.Join(" ", el.Opisy.Select(d => d.Tekst))}”, w UT „{string.Join(" ", nr.Where(x => x.Length > 0))}”", el.Id, WezZUt));
            if (bez > 0) res.Add(new(false, $"odstępy SBL: {bez} pól bez semafora odstępowego w UT – na pulpicie tylko napis", el.Id));
        }
        // strzałki blokady (uwaga właściciela 2026-10-26): koniec szlaku strzałki wybiera kompilacja po najbliższym narysowanym semaforze wjazdowym –
        // gdy żaden semafor końca szlaku nie jest przypięty, wybór jest na ślepo; dwie strzałki z tym samym końcem – druga stacja bez strzałki
        var konce = new Dictionary<(string Line, int K), List<string>>();
        var przyp = pr0.Values.ToHashSet();
        foreach (var el in o.Elementy.Where(e => e.Typ == ObrSymbole.StrzalkaBlokady))
        {
            if (!pr0.TryGetValue(el.Id, out var lid) || ut.Lines.FirstOrDefault(x => x.Id == lid) is not UtLine ln) continue;
            var sem = ln.Konce.Where(k => k.Semafor != null).Select(k => k.Semafor).ToList();
            int k = el.Pola ?? ObrKompilacja.KoniecSzlaku(ut, ln.Id, o, el);
            string nm = ln.Numer.Length > 0 ? ln.Numer : "(bez numeru)";
            if (el.Pola == null && sem.Count >= 2 && !sem.Any(przyp.Contains))
            {
                var bez = sem.Select(s => ut.Devices.FirstOrDefault(d => d.Id == s)?.Name ?? "?");
                res.Add(new(true, $"strzałka blokady szlaku {nm}: semafory końców szlaku ({string.Join(", ", bez)}) nie są przypięte na obrazie – nie wiadomo, którym końcem szlaku steruje strzałka; przypnij semafor wjazdowy przy strzałce (F8, „Do wyjaśnienia”)", el.Id));
            }
            if (!konce.TryGetValue((ln.Id, k), out var l)) konce[(ln.Id, k)] = l = new();
            l.Add(el.Id);
        }
        foreach (var ((lid, k), els) in konce.Where(z => z.Value.Count > 1))
        {
            var ln = ut.Lines.First(x => x.Id == lid);
            var sn = ln.Konce[k].Semafor is string s ? ut.Devices.FirstOrDefault(d => d.Id == s)?.Name : null;
            foreach (var id in els)
                res.Add(new(true, $"strzałka blokady szlaku {(ln.Numer.Length > 0 ? ln.Numer : "(bez numeru)")}: {els.Count} strzałki sterują tym samym końcem szlaku ({(sn != null ? "semafor " + sn : Szlaki.Opis(ut, ln.Konce[k]))}) – drugi koniec bez strzałki; przypnij semafor wjazdowy przy każdej strzałce", id));
        }
        // km dwa razy na obrazie
        foreach (var g in o.Przypiecia.Where(p => p.Tor?.Tor != null && p.Stan is ObrPrzypiecie.Pewne or ObrPrzypiecie.Potwierdzone).GroupBy(p => p.Tor.Tor))
        {
            var l = g.Select(p => (A: Math.Min(p.Tor.S0, p.Tor.S1), B: Math.Max(p.Tor.S0, p.Tor.S1), p.El)).OrderBy(x => x.A).ToList();
            for (int i = 1; i < l.Count; i++)
                if (l[i - 1].B - l[i].A > 0.5) res.Add(new(true, $"{ObrPrzypinanie.Opis(ut, g.Key)}: km {l[i].A:0}–{Math.Min(l[i - 1].B, l[i].B):0} dwa razy na obrazie", l[i].El));
        }
        // strefy zarezerwowane
        bool WStrefie(double x, double y) => o.Strefy.Any(s => s.Prost?.Length == 4 && x > s.Prost[0] && x < s.Prost[0] + s.Prost[2] && y > s.Prost[1] && y < s.Prost[1] + s.Prost[3]);
        foreach (var w in o.Wezly.Where(w => WStrefie(w.P[0], w.P[1]))) res.Add(new(false, $"{Opis(o, w.Id)} w strefie zarezerwowanej przez pulpit", w.Id));
        foreach (var el in o.Elementy) if (ObrSymbole.Pozycja(o, el) is var (x, y, _, _) && WStrefie(x, y)) res.Add(new(false, $"{Opis(o, el.Id)} w strefie zarezerwowanej przez pulpit", el.Id));
        bool pogl = o.Rodzaj == ZobObraz.Pogladowy;
        // odstęp torów ≥ 2 wysokości opisu (poglądowy ≥ 1) – Ie-104 § 4 ust. 15 pkt 1; opis 10 j.
        int min = pogl ? 10 : 20;
        var poz = o.Odcinki.Where(e => ObrGraf.Poziomy(o, e)).Select(e => { var (a, b) = ObrGraf.Konce(o, e); return (e.Id, Y: a[1], X0: Math.Min(a[0], b[0]), X1: Math.Max(a[0], b[0])); }).ToList();
        foreach (var a in poz)
            foreach (var b in poz)
                if (string.CompareOrdinal(a.Id, b.Id) < 0 && a.Y != b.Y && Math.Abs(a.Y - b.Y) < min && Math.Min(a.X1, b.X1) - Math.Max(a.X0, b.X0) > 16)
                    res.Add(new(false, $"tory za blisko: {Math.Abs(a.Y - b.Y)} j. – co najmniej {min} j. ({(pogl ? "1" : "2")} × wysokość opisu, Ie-104 § 4 ust. 15 pkt 1)", a.Id));
        // tarcza manewrowa tuż obok wykolejnicy – Ie-104 § 4 ust. 15 pkt 3
        foreach (var tm in o.Elementy.Where(e => e.Typ == ObrSymbole.Tarcza))
            foreach (var wk in o.Elementy.Where(e => e.Typ == ObrSymbole.Wykolejnica))
                if (ObrSymbole.Pozycja(o, tm) is var (tx, ty, _, _) && ObrSymbole.Pozycja(o, wk) is var (wx, wy, _, _) && Math.Abs(ty - wy) < 1 && Math.Abs(tx - wx) < 24)
                    res.Add(new(false, $"{Opis(o, tm.Id)} obok {Opis(o, wk.Id)} – wykolejnicę umieść w odcinku zwrotnicowym podzielonym na dwie części (Ie-104 § 4 ust. 15 pkt 3)", wk.Id));
        if (pogl)
            foreach (var el in o.Elementy.Where(e => e.Typ is ObrSymbole.Tarcza or ObrSymbole.Wykolejnica or ObrSymbole.OpisOdcinka or ObrSymbole.Peron or ObrSymbole.Pk))
                res.Add(new(false, $"obraz poglądowy: {Opis(o, el.Id)} – na obrazie poglądowym tylko symbole z Ie-104 § 6 ust. 4 (inne za zgodą PLK, § 6 ust. 5)", el.Id));
        foreach (var n in ObrPrzypinanie.Nieumieszczone(ut, o)) res.Add(new(false, $"nie ma na obrazie: {n.Opis}", n.Id));
        return res;
    }
}

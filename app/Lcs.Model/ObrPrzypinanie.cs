namespace Lcs.Model;

/// <summary>
/// „Przypnij do UT” (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 6, etap E4): porównanie grafu rysunku z UT. Kolejność:
/// 1. kotwice po opisach – zwrotnica „12” = rozjazd 12, semafor „A” = semafor A, numer toru „4a” = tor 4a (jednoznaczne w posterunkach obrazu);
/// 2. łańcuchy torów rysunku (odcinki między rozjazdami / końcami) – tor UT z ramion zakotwiczonych rozjazdów (iglica i na wprost – tor
///    zasadniczy, odgałęzienie – tor odgałęźny), z semaforów i numerów torów na łańcuchu; km rozkładane proporcjonalnie między kotwicami,
///    koniec łańcucha bez kotwicy – koniec toru UT;
/// 3. elementy bez nazwy – kandydaci z UT na torze łańcucha według km (jeden blisko – pewne);
/// 4. reszta – „do wyjaśnienia” z powodem i kandydatami. Przypięcia potwierdzone przez człowieka zostają i są kotwicami.
/// Rysunek nie jest zmieniany (poza przypięciami i węzłami w miejscu styku torów UT na torze narysowanym jedną linią – wygląd bez zmian).
/// Rozjazd narysowany z torem zasadniczym UT na ukos (odgałęzienie na wprost) – ramiona zamienione wg świadectw na łańcuchach. Bez WPF.
/// </summary>
public static class ObrPrzypinanie
{
    public record Wynik(int Pewne, int Potwierdzone, int DoWyjasnienia, int Brak);
    /// <summary>Obiekt UT nieumieszczony na obrazie (grupa jak w panelu UT, id, opis).</summary>
    public record Nieumieszczony(string Grupa, string Id, string Opis);

    /// <summary>Kontekst UT obrazu: tory obrazu (posterunki obrazu i szlaki do nich), długości torów.</summary>
    sealed class Ctx
    {
        public UtProject Ut;
        public ObrObraz O;
        public HashSet<string> Tory;
        public Dictionary<string, double> Len = new();
        /// <summary>Rozjazdy rysunku z ramionami odwrotnie niż w UT: na wprost na rysunku – odgałęzienie UT, ukos – tor zasadniczy.</summary>
        public HashSet<string> Odwr = new();
        public bool NaObrazie(string track) => track != null && Tory.Contains(track);
        public List<Styk> Styki => styki.GetValue(Ut, u => Topologia.Styki(u));
    }

    // UT w edytorze zobrazowania jest tylko do odczytu (wczytanie od nowa = nowy obiekt) – długości torów, styki i tory posterunków liczone raz na UT
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<UtProject, Dictionary<string, double>> dlugosci = new();
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<UtProject, List<Styk>> styki = new();
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<UtProject, Dictionary<string, HashSet<string>>> toryPost = new();

    /// <summary>Długości torów UT [m] (pamiętane dla tego UT).</summary>
    public static Dictionary<string, double> Dlugosci(UtProject ut) => dlugosci.GetValue(ut, u => u.Tracks.ToDictionary(t => t.Id, t => Trasa.Compute(t).Length));

    /// <summary>Tory UT obrazu z posterunkami <paramref name="o"/> (torów posterunków i szlaków do nich; pamiętane dla tego UT i zestawu posterunków).</summary>
    public static HashSet<string> ToryObrazu(UtProject ut, ObrObraz o)
    {
        var d = toryPost.GetValue(ut, _ => new());
        var key = string.Join("|", o.Posterunki.OrderBy(x => x));
        lock (d)
        {
            if (!d.TryGetValue(key, out var t)) d[key] = t = ZobUt.Tory(ut, new ZobObraz { Posterunki = o.Posterunki.ToList() }, out _).Keys.ToHashSet();
            return t;
        }
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<UtProject, OdcinkiResult> odcinkiUt = new();

    /// <summary>Odcinki kontroli niezajętości UT na torze <paramref name="track"/> w km <paramref name="s0"/>–<paramref name="s1"/>: nazwa (bez nazwy – „bez nazwy”) i km kawałka.</summary>
    public static List<(string Nazwa, double S0, double S1)> OdcinkiNaTorze(UtProject ut, string track, double s0, double s1)
    {
        var od = odcinkiUt.GetValue(ut, u => Odcinki.Compute(u));
        double lo = Math.Min(s0, s1), hi = Math.Max(s0, s1);
        return od.Pieces.Where(p => p.Track == track && Math.Min(p.S1, hi) - Math.Max(p.S0, lo) > 0.5)
            .OrderBy(p => p.S0).Select(p => (od.Names.TryGetValue(p.Comp, out var n) && n.Count > 0 ? string.Join(" / ", n.Select(x => x.Name)) : "bez nazwy", Math.Max(p.S0, lo), Math.Min(p.S1, hi))).ToList();
    }

    static Ctx Kontekst(UtProject ut, ObrObraz o) => new() { Ut = ut, O = o, Tory = ToryObrazu(ut, o), Len = Dlugosci(ut) };

    static string Norm(string s) => (s ?? "").Trim().ToUpperInvariant();
    static string Tekst(ObrElement el) => el.Opisy.FirstOrDefault()?.Tekst?.Trim() ?? "";

    /// <summary>Urządzenia UT odpowiadające rodzajowi elementu.</summary>
    public static bool Pasuje(string typ, UtDevice d) => typ switch
    {
        ObrSymbole.Semafor => d.Kind == UtDevice.Semafor && d.Funkcja != UtDevice.FPowtarzacz && d.Funkcja != UtDevice.FPk && d.Funkcja != UtDevice.FOdstepowy,
        ObrSymbole.Tarcza => d.Kind == UtDevice.TarczaMan,
        ObrSymbole.Powtarzacz => d.Kind == UtDevice.TarczaOstrz || d.Kind == UtDevice.Semafor && d.Funkcja == UtDevice.FPowtarzacz,
        ObrSymbole.Pk => d.Kind == UtDevice.Semafor && d.Funkcja == UtDevice.FPk,
        ObrSymbole.Wykolejnica => d.Kind == UtDevice.Wykolejnica,
        ObrSymbole.Koziol => d.Kind == UtDevice.Koziol,
        ObrSymbole.Przerwa => d.Kind is UtDevice.LicznikOsi or UtDevice.Zlacze,
        _ => false,
    };

    /// <summary>Miejsce obiektu UT (tor, km); null – obiekt bez miejsca albo nieznany.</summary>
    public static (string Track, double S)? Miejsce(UtProject ut, string id)
    {
        if (ut.Devices.FirstOrDefault(d => d.Id == id) is UtDevice d) return (d.Track, d.S);
        if (ut.Turnouts.FirstOrDefault(z => z.Id == id) is UtTurnout z) return (z.Track, z.S);
        if (ut.LevelCrossings.FirstOrDefault(x => x.Id == id) is UtLevelCrossing lc) return (lc.Track, lc.S);
        if (ut.Platforms.FirstOrDefault(x => x.Id == id) is UtPlatform pl) return (pl.Track, (pl.S0 + pl.S1) / 2);
        if (ut.Sections.FirstOrDefault(x => x.Id == id) is UtSection s) return (s.Track, s.S);
        return null;
    }

    /// <summary>Opis obiektu UT do listy (rozjazd 12, semafor A – tor 1 km 150, tor 4a…).</summary>
    public static string Opis(UtProject ut, string id)
    {
        string Tr(string t) => ut.Tracks.FirstOrDefault(x => x.Id == t) is UtTrack tt ? (tt.Number.Length > 0 ? $"tor {tt.Number}" : "tor bez numeru") : "tor ?";
        if (ut.Tracks.FirstOrDefault(t => t.Id == id) is UtTrack tr)
        {
            var rz = ut.Turnouts.Where(z => z.Branch == tr.Id && z.Number.Length > 0).Select(z => z.Number).ToList();
            return tr.Number.Length > 0 ? $"tor {tr.Number}" : rz.Count == 2 ? $"tor łączący {rz[0]}/{rz[1]}" : "tor bez numeru";
        }
        if (ut.Turnouts.FirstOrDefault(z => z.Id == id) is UtTurnout z2) return $"rozjazd {(z2.Number.Length > 0 ? z2.Number : "(bez numeru)")} – {Tr(z2.Track)} km {z2.S:0}";
        if (ut.Devices.FirstOrDefault(d => d.Id == id) is UtDevice d2)
        {
            var kind = (UtDevice.Kinds.FirstOrDefault(k => k.Key == d2.Kind).Name ?? d2.Kind).ToLowerInvariant();
            if (d2.Kind == UtDevice.Semafor && !string.IsNullOrEmpty(d2.Funkcja)) kind = "semafor " + UtDevice.FunkcjaName(d2.Funkcja);
            return $"{kind} {d2.Name}".Trim() + $" – {Tr(d2.Track)} km {d2.S:0}";
        }
        if (ut.LevelCrossings.FirstOrDefault(x => x.Id == id) is UtLevelCrossing lc) return $"przejazd {lc.Name} – {Tr(lc.Track)} km {lc.S:0}".Replace("  ", " ");
        if (ut.Platforms.FirstOrDefault(x => x.Id == id) is UtPlatform pl) return $"peron {pl.Name} – {Tr(pl.Track)}".Replace("  ", " ");
        if (ut.Sections.FirstOrDefault(x => x.Id == id) is UtSection s) return $"odcinek {s.Name} – {Tr(s.Track)} km {s.S:0}";
        if (ut.Lines.FirstOrDefault(x => x.Id == id) is UtLine ln) return $"szlak {ln.Numer}";
        return "(nie ma w UT)";
    }

    /// <summary>Nazwa obiektu UT do porównania z opisem na obrazie (numer rozjazdu, nazwa sygnalizatora, numer toru…); null – bez nazwy.</summary>
    /// <summary>
    /// Kandydaci do wyboru na liście „Do wyjaśnienia” w kolejności prawdopodobieństwa: dla toru rysunku – najpierw tory rozjazdów na końcach jego łańcucha
    /// i tory sąsiednich przypiętych odcinków, potem tory jeszcze nienarysowane, na końcu reszta (kolejność UT zostaje w grupie).
    /// </summary>
    public static List<string> KandydaciPoKolei(UtProject ut, ObrObraz o, ObrPrzypiecie p, List<Lancuch> lan = null, HashSet<string> nienarysowane = null)
    {
        var k = p.Kandydaci ?? new();
        if (ObrGraf.Odcinek(o, p.El) == null || k.Count < 2) return k;
        var l = (lan ?? Lancuchy(o)).FirstOrDefault(x => x.Odc.Any(z => z.E.Id == p.El));
        var blisko = new HashSet<string>();
        var pr = o.Przypiecia.Where(q => q.Stan is ObrPrzypiecie.Pewne or ObrPrzypiecie.Potwierdzone).GroupBy(q => q.El).ToDictionary(g => g.Key, g => g.First());
        foreach (var w in l == null ? new List<string>() : new List<string> { l.Start, l.Koniec })
        {
            if (pr.TryGetValue(w, out var pw) && ut.Turnouts.FirstOrDefault(z => z.Id == pw.Ut) is UtTurnout z) { blisko.Add(z.Track); if (z.Branch != null) blisko.Add(z.Branch); }
            foreach (var e in ObrGraf.Przy(o, w)) if (pr.TryGetValue(e.Id, out var pe) && pe.Tor?.Tor != null) blisko.Add(pe.Tor.Tor);
        }
        var nowe = nienarysowane ?? ObrSzkic.Nienarysowane(ut, o).ToHashSet();
        return k.OrderBy(id => blisko.Contains(id) ? 0 : nowe.Contains(id) ? 1 : 2).ToList();
    }

    public static string NazwaUt(UtProject ut, string id) =>
        ut.Turnouts.FirstOrDefault(z => z.Id == id)?.Number ?? ut.Devices.FirstOrDefault(d => d.Id == id)?.Name ?? ut.Tracks.FirstOrDefault(t => t.Id == id)?.Number
        ?? ut.Platforms.FirstOrDefault(p => p.Id == id)?.Name ?? ut.Sections.FirstOrDefault(s => s.Id == id)?.Name ?? ut.LevelCrossings.FirstOrDefault(x => x.Id == id)?.Name;

    // ------------------------------------------------------------------ łańcuchy torów rysunku
    /// <summary>Łańcuch: odcinki po kolei (z kierunkiem: true – od A do B), węzeł początkowy i końcowy, długość narastająco w węzłach.</summary>
    public sealed class Lancuch
    {
        public List<(ObrOdcinek E, bool Fwd)> Odc = new();
        public List<string> Wezly = new();
        public List<double> Poz = new();
        public string Start => Wezly[0];
        public string Koniec => Wezly[^1];
        public double Dl => Poz[^1];
    }

    static double Dl(int[] a, int[] b) => Math.Sqrt(Math.Pow(b[0] - a[0], 2) + Math.Pow(b[1] - a[1], 2));

    /// <summary>
    /// Kotwice niejednoznaczne między posterunkami obrazu (ten sam numer / nazwa w dwóch stacjach): kandydat z posterunku najbliższej (na rysunku)
    /// jednoznacznej kotwicy, gdy każdy posterunek kandydatów ma już kotwicę (jednoznaczną albo potwierdzoną przez człowieka); powtarzane, aż nic się
    /// nie zmienia (rozstrzygnięte stają się kotwicami dla dalszych).
    /// </summary>
    static string PostUt(UtProject ut, string id) => Posterunki.Of(ut, ut.Turnouts.FirstOrDefault(z => z.Id == id)?.Track ?? ut.Devices.FirstOrDefault(d => d.Id == id)?.Track)?.Id;
    static (double, double)? GdzieEl(ObrObraz o, string el) => ObrGraf.Wezel(o, el) is ObrWezel w ? (w.P[0], w.P[1])
        : o.Elementy.FirstOrDefault(e => e.Id == el) is ObrElement e && ObrSymbole.Pozycja(o, e) is var (x, y, _, _) ? (x, y) : null;
    /// <summary>Jednoznaczne kotwice rysunku (rozjazdy, sygnalizatory) z posterunkiem w UT.</summary>
    static List<((double, double)? P, string Post)> Kotwice(UtProject ut, ObrObraz o, Dictionary<string, ObrPrzypiecie> nowe) =>
        nowe.Values.Where(p => p.Ut != null && p.Stan is ObrPrzypiecie.Pewne or ObrPrzypiecie.Potwierdzone)
            .Select(p => (P: GdzieEl(o, p.El), Post: PostUt(ut, p.Ut))).Where(z => z.P != null && z.Post != null).ToList();

    static void RozstrzygnijPosterunkiem(UtProject ut, ObrObraz o, List<(string El, List<string> Kand)> niejedn, Dictionary<string, ObrPrzypiecie> nowe,
        Action<string, string, string, ZobUtTor, string, List<string>> set)
    {
        string Post(string id) => PostUt(ut, id);
        (double, double)? Gdzie(string el) => GdzieEl(o, el);
        for (bool zm = true; zm;)
        {
            zm = false;
            var kotwice = Kotwice(ut, o, nowe);
            foreach (var (el, kand) in niejedn.ToList())
            {
                if (Gdzie(el) is not var (x, y)) continue;
                var posty = kand.Select(Post).ToHashSet();
                // każdy posterunek kandydatów musi mieć już kotwicę (inaczej stacja bez kotwic wzięłaby kotwice sąsiedniej – człowiek potwierdza jedną)
                if (posty.Any(ps => !kotwice.Any(z => z.Post == ps))) continue;
                var naj = kotwice.Where(z => posty.Contains(z.Post)).OrderBy(z => Math.Pow(z.P.Value.Item1 - x, 2) + Math.Pow(z.P.Value.Item2 - y, 2)).FirstOrDefault();
                if (naj.Post == null) continue;
                var k = kand.Where(id => Post(id) == naj.Post).ToList();
                if (k.Count != 1) continue;
                set(el, ObrPrzypiecie.Pewne, k[0], null, null, null);
                niejedn.Remove((el, kand)); zm = true;
                kotwice.Add((Gdzie(el), naj.Post));
            }
        }
    }

    /// <summary>Łańcuchy: przez załamania (2 odcinki) i przez skrzyżowania (4 odcinki – na wprost), do rozjazdów i końców.</summary>
    public static List<Lancuch> Lancuchy(ObrObraz o)
    {
        var res = new List<Lancuch>(); var used = new HashSet<string>();
        bool Przechodni(string w) => ObrGraf.Przy(o, w).Count == 2 || ObrGraf.Przy(o, w).Count == 4;
        ObrOdcinek Dalej(string w, ObrOdcinek przez)
        {
            var przy = ObrGraf.Przy(o, w);
            if (przy.Count == 2) return przy.First(x => x != przez);
            var k = ObrGraf.Kier(o, przez, w);
            return przy.FirstOrDefault(x => x != przez && ObrGraf.Kier(o, x, w) is var q && q.X == -k.X && q.Y == -k.Y);
        }
        foreach (var e0 in o.Odcinki)
        {
            if (used.Contains(e0.Id)) continue;
            // cofnij się do początku łańcucha
            var e = e0; var w = e0.A; var guard = 0;
            while (Przechodni(w) && guard++ < 10000)
            {
                var p = Dalej(w, e); if (p == null || p == e0) break;
                w = ObrGraf.Drugi(p, w); e = p;
            }
            var l = new Lancuch(); l.Wezly.Add(w); l.Poz.Add(0);
            var cur = w; var ce = e; guard = 0;
            while (ce != null && !used.Contains(ce.Id) && guard++ < 10000)
            {
                used.Add(ce.Id);
                bool fwd = ce.A == cur; var nxt = ObrGraf.Drugi(ce, cur);
                l.Odc.Add((ce, fwd)); l.Wezly.Add(nxt); l.Poz.Add(l.Poz[^1] + Dl(ObrGraf.Wezel(o, cur).P, ObrGraf.Wezel(o, nxt).P));
                cur = nxt;
                ce = Przechodni(cur) ? Dalej(cur, ce) : null;
            }
            res.Add(l);
        }
        return res;
    }

    /// <summary>Ramię rozjazdu UT na rysunku: tor UT, km w węźle, kierunek km od węzła wzdłuż ramienia (+1 rosnący).</summary>
    static (string Track, double S, int Dir)? Ramie(Ctx c, string wezel, ObrOdcinek e, UtTurnout z)
    {
        var o = c.O;
        var odg = ObrGraf.Odgalezienie(o, wezel); if (odg == null) return null;
        var ig = ObrSymbole.Iglica(o, wezel);
        var k = ObrGraf.Kier(o, e, wezel);
        bool iglica = e != odg && ig is var (ix, iy) && k.X == ix && k.Y == iy;
        // ramię odgałęzienia UT: ukos na rysunku, a przy rozjeździe odwróconym (c.Odwr) – ramię na wprost
        if (!iglica && (e == odg) != c.Odwr.Contains(wezel))
        {
            if (z.Branch == null || !c.Len.TryGetValue(z.Branch, out var bl)) return null;
            return z.BranchAtEnd ? (z.Branch, bl, -1) : (z.Branch, 0, 1);
        }
        // Dir = +1: rozjazd zwrócony ku rosnącemu km (odgałęzienie i tor na wprost – km rośnie, iglica – maleje)
        return (z.Track, z.S, iglica ? -z.Dir : z.Dir);
    }

    /// <summary>
    /// Rozjazdy narysowane odwrotnie niż w UT (na rysunku na wprost idzie odgałęzienie UT – plan schematyczny rysuje tor główny prosto):
    /// świadectwa na łańcuchach ramion (sygnalizatory i urządzenia przypięte, numery torów, ramię rozjazdu na drugim końcu) porównane z torem
    /// zasadniczym i odgałęźnym UT – odległość w torach po stykach końców (ten sam tor 0, nieosiągalny 10). Odwrócony, gdy zamiana ramion
    /// pasuje lepiej (ściśle); bez świadectw – bez zmian.
    /// </summary>
    static void OdwrocRamiona(Ctx c, List<Lancuch> lan, Func<string, string> utOf)
    {
        var o = c.O; var ut = c.Ut;
        var hops = new Dictionary<(string, string), int>();
        int Hops(string a, string b)
        {
            if (a == b) return 0;
            if (!hops.TryGetValue((a, b), out var h)) hops[(a, b)] = h = Droga(c, a, b)?.Count ?? 10;
            return h;
        }
        // świadectwa ramienia: lista zbiorów torów (każdy zbiór – jedno świadectwo, np. numer toru z kilkoma torami UT)
        List<List<string>> Swiadectwa(string w, ObrOdcinek e)
        {
            var res = new List<List<string>>();
            var l = lan.FirstOrDefault(x => x.Wezly[0] == w && x.Odc[0].E == e || x.Wezly[^1] == w && x.Odc[^1].E == e);
            if (l == null) return res;
            foreach (var (oe, _) in l.Odc)
                foreach (var el in o.Elementy.Where(x => x.Na == oe.Id))
                {
                    if (utOf(el.Id) is string did && ut.Devices.FirstOrDefault(d => d.Id == did) is UtDevice dev) res.Add(new() { dev.Track });
                    else if (el.Typ == ObrSymbole.NumerToru && Tekst(el).Length > 0
                        && ut.Tracks.Where(t => c.NaObrazie(t.Id) && Norm(t.Number) == Norm(Tekst(el))).Select(t => t.Id).ToList() is { Count: > 0 } nt) res.Add(nt);
                }
            bool odStartu = l.Wezly[0] == w && l.Odc[0].E == e;
            var w2 = odStartu ? l.Wezly[^1] : l.Wezly[0]; var e2 = odStartu ? l.Odc[^1].E : l.Odc[0].E;
            if (w2 != w && utOf(w2) is string zid && ut.Turnouts.FirstOrDefault(z => z.Id == zid) is UtTurnout z2 && Ramie(c, w2, e2, z2) is var (tr, _, _)) res.Add(new() { tr });
            return res;
        }
        int Koszt(List<List<string>> sw, string track) => track == null ? 10 * sw.Count : sw.Sum(s => s.Min(t => Hops(track, t)));
        foreach (var w in o.Wezly.Where(w => ObrGraf.RodzajWezla(o, w.Id) == ObrGraf.Rodzaj.Rozjazd))
        {
            if (utOf(w.Id) is not string zid || ut.Turnouts.FirstOrDefault(z => z.Id == zid) is not UtTurnout z || z.Branch == null) continue;
            var odg = ObrGraf.Odgalezienie(o, w.Id); var ig = ObrSymbole.Iglica(o, w.Id);
            if (odg == null || ig is not var (ix, iy)) continue;
            var wprost = ObrGraf.Przy(o, w.Id).FirstOrDefault(e => e != odg && ObrGraf.Kier(o, e, w.Id) is var k && (k.X != ix || k.Y != iy));
            if (wprost == null) continue;
            var sw = Swiadectwa(w.Id, wprost); var sb = Swiadectwa(w.Id, odg);
            if (sw.Count + sb.Count == 0) continue;
            if (Koszt(sw, z.Branch) + Koszt(sb, z.Track) < Koszt(sw, z.Track) + Koszt(sb, z.Branch)) c.Odwr.Add(w.Id);
        }
    }

    /// <summary>
    /// Sygnalizator UT stojący na styku końców torów (do 1 m od końca swojego toru, styk z <paramref name="track"/>): +1 – km obu torów w tę samą
    /// stronę (koniec – początek), −1 – przeciwnie; null – nie na styku z tym torem.
    /// </summary>
    static int? NaStyku(Ctx c, string devTrack, double s, string track)
    {
        if (!c.Len.TryGetValue(devTrack, out var len)) return null;
        foreach (var atStart in new[] { true, false })
        {
            if (Math.Abs(s - (atStart ? 0 : len)) > 1) continue;
            foreach (var st in c.Styki.Where(x => x.OtherEnd))
            {
                bool otherStart;
                if (st.Track == devTrack && st.AtStart == atStart && st.Other == track) otherStart = st.OtherS < 1;
                else if (st.Other == devTrack && Math.Abs(st.OtherS - (atStart ? 0 : len)) < 1 && st.Track == track) otherStart = st.AtStart;
                else continue;
                return atStart == otherStart ? -1 : 1;
            }
        }
        return null;
    }

    // ------------------------------------------------------------------ przypinanie
    /// <summary>Przypnij obraz do UT (F8). Przypięcia potwierdzone i „bez przypięcia” wybrane przez człowieka zostają.</summary>
    public static Wynik Przypnij(UtProject ut, ObrObraz o)
    {
        var c = Kontekst(ut, o);
        var keep = o.Przypiecia.Where(p => p.Stan is ObrPrzypiecie.Potwierdzone or ObrPrzypiecie.Brak).ToList();
        // usunięte elementy rysunku – bez przypięć
        var ids = o.Wezly.Select(w => w.Id).Concat(o.Odcinki.Select(e => e.Id)).Concat(o.Elementy.Select(e => e.Id)).ToHashSet();
        keep.RemoveAll(p => !ids.Contains(p.El));
        var nowe = new Dictionary<string, ObrPrzypiecie>();
        foreach (var p in keep) nowe[p.El] = p;
        void Set(string el, string stan, string utId = null, ZobUtTor tor = null, string powod = null, List<string> kand = null)
        {
            if (nowe.TryGetValue(el, out var p0) && p0.Stan is ObrPrzypiecie.Potwierdzone or ObrPrzypiecie.Brak) return;
            nowe[el] = new ObrPrzypiecie { El = el, Stan = stan, Ut = utId, Tor = tor, Powod = powod, Kandydaci = kand?.Count > 0 ? kand : null };
        }
        string UtOf(string el) => nowe.TryGetValue(el, out var p) && p.Stan is ObrPrzypiecie.Pewne or ObrPrzypiecie.Potwierdzone ? p.Ut : null;
        HashSet<string> Uzyte() => nowe.Values.Where(p => p.Ut != null && p.Stan is ObrPrzypiecie.Pewne or ObrPrzypiecie.Potwierdzone).Select(p => p.Ut).ToHashSet();

        var turnouts = ut.Turnouts.Where(z => c.NaObrazie(z.Track)).ToList();
        var devices = ut.Devices.Where(d => c.NaObrazie(d.Track)).ToList();
        // kilka posterunków na obrazie (np. dwie stacje ze szlakiem wewnętrznym): te same numery zwrotnic i nazwy semaforów w każdej stacji –
        // rozstrzyga poprzednie pewne przypięcie (szkic, wcześniejsze F8), potem posterunek najbliższej jednoznacznej kotwicy rysunku
        var stare = o.Przypiecia.Where(p => p.Stan == ObrPrzypiecie.Pewne && p.Ut != null).GroupBy(p => p.El).ToDictionary(g => g.Key, g => g.First().Ut);
        var niejedn = new List<(string El, List<string> Kand)>();
        void Niejedn(string el, List<string> kand, string powod)
        {
            if (stare.TryGetValue(el, out var s0) && kand.Contains(s0)) { Set(el, ObrPrzypiecie.Pewne, s0); return; }
            Set(el, ObrPrzypiecie.DoWyjasnienia, powod: powod, kand: kand);
            niejedn.Add((el, kand));
        }

        // 1. rozjazdy po numerach zwrotnic
        var rozj = o.Wezly.Where(w => ObrGraf.RodzajWezla(o, w.Id) == ObrGraf.Rodzaj.Rozjazd).ToList();
        foreach (var w in rozj)
        {
            var nr = ObrSymbole.ZwrotnicaW(o, w.Id) is ObrElement zel ? Tekst(zel) : "";
            if (nr.Length == 0) continue;
            var k = turnouts.Where(z => Norm(z.Number) == Norm(nr)).ToList();
            if (k.Count == 1) Set(w.Id, ObrPrzypiecie.Pewne, k[0].Id);
            else if (k.Count > 1) Niejedn(w.Id, k.Select(z => z.Id).ToList(), $"{k.Count} rozjazdy o numerze {nr} w UT");
            else Set(w.Id, ObrPrzypiecie.DoWyjasnienia, powod: $"brak w UT rozjazdu {nr}", kand: turnouts.Select(z => z.Id).ToList());
        }
        // 2. sygnalizatory i wykolejnice po nazwach
        foreach (var el in o.Elementy.Where(e => ObrSymbole.Kierunkowy(e.Typ) || e.Typ == ObrSymbole.Wykolejnica))
        {
            var n = Tekst(el); if (n.Length == 0) continue;
            var k = devices.Where(d => Pasuje(el.Typ, d) && Norm(d.Name) == Norm(n)).ToList();
            if (k.Count == 1) Set(el.Id, ObrPrzypiecie.Pewne, k[0].Id);
            else if (k.Count > 1) Niejedn(el.Id, k.Select(d => d.Id).ToList(), $"{k.Count} obiekty „{n}” w UT");
            else Set(el.Id, ObrPrzypiecie.DoWyjasnienia, powod: $"brak w UT: {ObrSymbole.NazwaTypu(el.Typ)} {n}", kand: devices.Where(d => Pasuje(el.Typ, d)).Select(d => d.Id).ToList());
        }
        RozstrzygnijPosterunkiem(ut, o, niejedn, nowe, Set);
        // rozjazdy z ramionami odwrotnie niż w UT (na wprost na rysunku – odgałęzienie UT)
        OdwrocRamiona(c, Lancuchy(o), UtOf);

        // 3. łańcuchy torów: tor UT i km (dwa przebiegi – rozjazdy bez numeru rozstrzygnięte w pierwszym są kotwicami w drugim)
        var extra = new Dictionary<Lancuch, List<(double Poz, double Km, string Track)>>();
        // tor narysowany jedną linią przez styk torów UT, bez węzła w pobliżu styku – odcinek dzielony węzłem w miejscu styku (wygląd bez zmian)
        for (int it = 0; it < 100; it++)
        {
            var pd = new List<(ObrOdcinek E, int[] P)>();
            PodzielNaTory(c, Lancuchy(o), UtOf, extra, pd);
            if (pd.Count == 0) break;
            foreach (var (e, pt) in pd.GroupBy(x => x.E).Select(g => g.First()))
            {
                var w = ObrGraf.Podziel(o, e, pt);
                var e2 = ObrGraf.Przy(o, w.Id).FirstOrDefault(x => x != e);
                if (e2 != null && nowe.TryGetValue(e.Id, out var pe) && pe.Stan is ObrPrzypiecie.Potwierdzone or ObrPrzypiecie.Brak
                    && o.Przypiecia.FirstOrDefault(x => x.El == e2.Id) is ObrPrzypiecie p2) nowe[e2.Id] = p2;
            }
        }
        var lan0 = Lancuchy(o); var lan = lan0;
        var torLan = new Dictionary<Lancuch, string>();
        var kmW = new Dictionary<(Lancuch, int), double>();
        var kmF = new Dictionary<Lancuch, Func<double, double>>();
        for (int pass = 0; pass < 2; pass++)
        {
            torLan.Clear(); kmW.Clear(); kmF.Clear();
            lan = PodzielNaTory(c, lan0, UtOf, extra);
            foreach (var l in lan) LancuchKm(c, l, UtOf, nowe, Set, torLan, kmW, kmF, extra.GetValueOrDefault(l));
            // rozjazdy bez numeru: kandydaci – wolne rozjazdy UT na torze zasadniczym (tor łańcucha iglicy / na wprost), najbliższe km
            bool zmiana = false;
            foreach (var w in rozj.Where(w => UtOf(w.Id) == null && !(nowe.TryGetValue(w.Id, out var pp) && pp.Kandydaci != null && pp.Powod?.StartsWith("brak w UT") == true)))
            {
                var odg = ObrGraf.Odgalezienie(o, w.Id);
                var tory = new HashSet<string>(); var kmy = new List<double>();
                foreach (var l in lan)
                    for (int i = 0; i < l.Wezly.Count; i += Math.Max(1, l.Wezly.Count - 1))
                        if (l.Wezly[i] == w.Id && torLan.TryGetValue(l, out var tl) && tl != null)
                        {
                            var e = i == 0 ? l.Odc[0].E : l.Odc[^1].E;
                            if (e == odg) continue;
                            tory.Add(tl); if (kmW.TryGetValue((l, i), out var km)) kmy.Add(km);
                        }
                var uz = Uzyte();
                var k = turnouts.Where(z => tory.Contains(z.Track) && !uz.Contains(z.Id)).OrderBy(z => kmy.Count == 0 ? 0 : kmy.Min(q => Math.Abs(q - z.S))).ToList();
                if (k.Count == 1) { Set(w.Id, ObrPrzypiecie.Pewne, k[0].Id); zmiana = true; }
                else Set(w.Id, ObrPrzypiecie.DoWyjasnienia, powod: k.Count == 0 ? "rozjazd bez numeru – brak kandydata w UT" : $"rozjazd bez numeru – {k.Count} kandydaci", kand: (k.Count > 0 ? k : turnouts.Where(z => !uz.Contains(z.Id)).ToList()).Select(z => z.Id).ToList());
            }
            if (!zmiana) break;
        }

        // 4. elementy na torze bez nazwy (i przerwy, kozły, przejazdy, perony…): kandydaci na torze łańcucha według km
        var elKm = new Dictionary<string, (string Track, double S)>();
        foreach (var l in lan)
        {
            if (!torLan.TryGetValue(l, out var tr) || tr == null) continue;
            for (int i = 0; i < l.Odc.Count; i++)
            {
                var (e, fwd) = l.Odc[i];
                if (!kmW.TryGetValue((l, i), out var k0) || !kmW.TryGetValue((l, i + 1), out var k1)) continue;
                foreach (var el in o.Elementy.Where(x => x.Na == e.Id))
                {
                    // km między kotwicami łańcucha (nie liniowo między końcami odcinka – semafor w środku odcinka jest kotwicą)
                    double t = fwd ? el.T : 1 - el.T, poz = l.Poz[i] + (l.Poz[i + 1] - l.Poz[i]) * t;
                    elKm[el.Id] = (tr, kmF.TryGetValue(l, out var f) ? f(poz) : k0 + (k1 - k0) * t);
                }
            }
            // kozioł na końcu łańcucha
            foreach (var (wi, km) in new[] { (0, kmW.GetValueOrDefault((l, 0), double.NaN)), (l.Wezly.Count - 1, kmW.GetValueOrDefault((l, l.Wezly.Count - 1), double.NaN)) })
                foreach (var el in o.Elementy.Where(x => x.Typ is ObrSymbole.Koziol or ObrSymbole.OdstepySbl && x.Na == l.Wezly[wi]))
                    if (!double.IsNaN(km)) elKm[el.Id] = (tr, km);
            // odstępy SBL w środku toru (szlak wewnętrzny na jednym obrazie ze stacjami)
            for (int wi = 1; wi < l.Wezly.Count - 1; wi++)
                foreach (var el in o.Elementy.Where(x => x.Typ == ObrSymbole.OdstepySbl && x.Na == l.Wezly[wi]))
                    if (kmW.TryGetValue((l, wi), out var km)) elKm[el.Id] = (tr, km);
        }
        foreach (var el in o.Elementy)
        {
            if (nowe.TryGetValue(el.Id, out var p0) && p0.Stan is ObrPrzypiecie.Pewne or ObrPrzypiecie.Potwierdzone or ObrPrzypiecie.Brak)
            {
                // nazwany sygnalizator: zgodność toru i kierunku z UT
                if (p0.Stan == ObrPrzypiecie.Pewne && elKm.TryGetValue(el.Id, out var m0) && ut.Devices.FirstOrDefault(d => d.Id == p0.Ut) is UtDevice dd)
                {
                    // sygnalizator na styku dwóch torów UT – na rysunku może trafić na tor po drugiej stronie styku (kierunek km wg styku)
                    int? styk = dd.Track == m0.Track ? 1 : NaStyku(c, dd.Track, dd.S, m0.Track);
                    if (styk == null) Set(el.Id, ObrPrzypiecie.DoWyjasnienia, powod: $"na rysunku na torze {Opis(ut, m0.Track).Replace("tor ", "")}, w UT – {Opis(ut, dd.Track)}", kand: new() { dd.Id });
                    else if (ObrSymbole.Kierunkowy(el.Typ) && KierKm(el, nowe) is int kk && kk * styk != dd.Dir)
                        Set(el.Id, ObrPrzypiecie.DoWyjasnienia, powod: "kierunek różny niż w UT (spacja – obrót)", kand: new() { dd.Id });
                }
                continue;
            }
            switch (el.Typ)
            {
                case ObrSymbole.Napis: Set(el.Id, ObrPrzypiecie.Brak, powod: "napis – bez znaczenia ruchowego"); continue;
                case ObrSymbole.StrzalkaBlokady: case ObrSymbole.OdstepySbl:
                    {
                        // szlak (etap E6): szlak UT, którego droga przechodzi przez tor i km pod elementem (strzałka – za semaforem wjazdowym,
                        // odstępy – koniec toru na obrazie); bez km – szlaki na tym torze
                        var m = elKm.TryGetValue(el.Id, out var mm) ? mm : ((string)null, double.NaN);
                        var k = Linie(ut, m.Item1, m.Item2);
                        if (k.Count == 1) Set(el.Id, ObrPrzypiecie.Pewne, k[0].Id);
                        else Set(el.Id, ObrPrzypiecie.DoWyjasnienia, powod: k.Count == 0 ? "brak szlaku w UT przy tym torze" : $"{k.Count} szlaki", kand: (k.Count > 0 ? k : ut.Lines).Select(x => x.Id).ToList());
                        continue;
                    }
                case ObrSymbole.PolePip: continue;          // po szlakach – niżej
                case ObrSymbole.Zwrotnica: case ObrGraf.Skrzyzowanie: case ObrGraf.RozjazdKrzyzowy: continue;      // przypięcie ma węzeł rozjazdu
                case ObrSymbole.NumerToru:
                    {
                        var tr = elKm.TryGetValue(el.Id, out var m) ? m.Track : null;
                        var k = ut.Tracks.Where(t => c.NaObrazie(t.Id) && Norm(t.Number) == Norm(Tekst(el))).ToList();
                        if (tr != null && k.Any(t => t.Id == tr)) Set(el.Id, ObrPrzypiecie.Pewne, tr);
                        else Set(el.Id, ObrPrzypiecie.DoWyjasnienia, powod: tr == null ? "tor pod numerem nie jest przypięty" : $"numer {Tekst(el)}, a tor na rysunku to {Opis(ut, tr)}", kand: (tr != null ? new List<string> { tr } : k.Select(t => t.Id).ToList()));
                        continue;
                    }
                case ObrSymbole.OpisOdcinka:
                    {
                        var k = ut.Sections.Where(s => c.NaObrazie(s.Track) && Norm(s.Name) == Norm(Tekst(el))).ToList();
                        // ta sama nazwa odcinka w kilku posterunkach obrazu – posterunek toru pod opisem
                        if (k.Count > 1 && elKm.TryGetValue(el.Id, out var mo) && Posterunki.Of(ut, mo.Track)?.Id is string po
                            && k.Where(s => Posterunki.Of(ut, s.Track)?.Id == po).ToList() is { Count: 1 } kp) k = kp;
                        if (k.Count == 1) Set(el.Id, ObrPrzypiecie.Pewne, k[0].Id);
                        else Set(el.Id, ObrPrzypiecie.DoWyjasnienia, powod: k.Count == 0 ? $"brak w UT odcinka {Tekst(el)}" : $"{k.Count} odcinki {Tekst(el)}", kand: (k.Count > 0 ? k : ut.Sections.Where(s => c.NaObrazie(s.Track)).ToList()).Select(s => s.Id).ToList());
                        continue;
                    }
                case ObrSymbole.Peron:
                    {
                        var n = Norm(Tekst(el)).Replace("PERON", "").Trim();
                        var all = ut.Platforms.Where(p => c.NaObrazie(p.Track)).ToList();
                        var k = all.Where(p => Norm(p.Name).Replace("PERON", "").Trim() == n).ToList();
                        if (k.Count == 1) Set(el.Id, ObrPrzypiecie.Pewne, k[0].Id);
                        else if (all.Count == 0) Set(el.Id, ObrPrzypiecie.DoWyjasnienia, powod: "w UT nie ma peronów");
                        else Set(el.Id, ObrPrzypiecie.DoWyjasnienia, powod: k.Count == 0 ? $"brak w UT peronu {Tekst(el)}" : $"{k.Count} perony", kand: (k.Count > 0 ? k : all).Select(p => p.Id).ToList());
                        continue;
                    }
            }
            // symbol na torze / kozioł: najbliższy wolny obiekt tego rodzaju na torze łańcucha
            if (!elKm.TryGetValue(el.Id, out var mk)) { Set(el.Id, ObrPrzypiecie.DoWyjasnienia, powod: "tor pod symbolem nie jest przypięty"); continue; }
            var uzyte = Uzyte();
            var kand = el.Typ == ObrSymbole.Przejazd
                ? ut.LevelCrossings.Where(x => x.Track == mk.Track && !uzyte.Contains(x.Id)).Select(x => (x.Id, x.S)).ToList()
                : devices.Where(d => Pasuje(el.Typ, d) && d.Track == mk.Track && !uzyte.Contains(d.Id)).Select(d => (d.Id, d.S)).ToList();
            kand = kand.OrderBy(x => Math.Abs(x.S - mk.S)).ToList();
            double tol = el.Typ == ObrSymbole.Koziol ? 25 : 40;
            var bliskie = kand.Where(x => Math.Abs(x.S - mk.S) <= tol).ToList();
            if (bliskie.Count == 1) Set(el.Id, ObrPrzypiecie.Pewne, bliskie[0].Id);
            else Set(el.Id, ObrPrzypiecie.DoWyjasnienia,
                powod: kand.Count == 0 ? $"brak w UT: {ObrSymbole.NazwaTypu(el.Typ)} na {Opis(ut, mk.Track)} (km ok. {mk.S:0})" : bliskie.Count > 1 ? $"{bliskie.Count} kandydaci blisko km {mk.S:0}" : $"najbliższy w UT {Math.Abs(kand[0].S - mk.S):0} m od km {mk.S:0}",
                kand: kand.Take(6).Select(x => x.Id).ToList());
        }
        // okna numeru pociągu: szlak najbliższego przypiętego elementu szlaku (strzałka, odstępy) na obrazie
        foreach (var el in o.Elementy.Where(e => e.Typ == ObrSymbole.PolePip && e.P != null))
        {
            if (nowe.TryGetValue(el.Id, out var p0) && p0.Stan is ObrPrzypiecie.Potwierdzone or ObrPrzypiecie.Brak) continue;
            var r = ObrSymbole.Rozmiar(el); double cx = el.P[0] + r[0] / 2.0, cy = el.P[1] + r[1] / 2.0;
            var best = o.Elementy.Where(e => e.Typ is ObrSymbole.StrzalkaBlokady or ObrSymbole.OdstepySbl && UtOf(e.Id) != null)
                .Select(e => (e, d: ObrSymbole.Pozycja(o, e) is var (x, y, _, _) ? Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) : double.MaxValue)).OrderBy(z => z.d).FirstOrDefault();
            if (best.e != null) Set(el.Id, ObrPrzypiecie.Pewne, UtOf(best.e.Id));
            else Set(el.Id, ObrPrzypiecie.DoWyjasnienia, powod: "brak przypiętego szlaku w pobliżu (strzałki albo odstępów)", kand: ut.Lines.Select(x => x.Id).ToList());
        }
        o.Przypiecia = nowe.Values.OrderBy(p => p.Stan == ObrPrzypiecie.DoWyjasnienia ? 0 : 1).ToList();
        // puste opisy szlaku z UT: nazwa sąsiada, numery odstępów (E6)
        ObrSzlak.UzupelnijZUt(ut, o);
        return new Wynik(o.Przypiecia.Count(p => p.Stan == ObrPrzypiecie.Pewne), o.Przypiecia.Count(p => p.Stan == ObrPrzypiecie.Potwierdzone),
            o.Przypiecia.Count(p => p.Stan == ObrPrzypiecie.DoWyjasnienia), o.Przypiecia.Count(p => p.Stan == ObrPrzypiecie.Brak));
    }

    /// <summary>Szlaki UT, których droga przechodzi przez tor w km (do 1 m); NaN – szlaki z tym torem; null tor – wszystkie.</summary>
    public static List<UtLine> Linie(UtProject ut, string track, double km)
    {
        if (ut.Lines.Count == 0) return new();
        var drogi = Szlaki.Drogi(ut);
        return ut.Lines.Where(x => track == null || drogi.TryGetValue(x.Id, out var d) && d.Kawalki.Any(q => q.Track == track && (double.IsNaN(km) || km >= q.S0 - 1 && km <= q.S1 + 1))).ToList();
    }

    /// <summary>Kierunek symbolu w km (+1 – w stronę rosnącego km) z km odcinka, na którym stoi; null – nieznany.</summary>
    static int? KierKm(ObrElement el, Dictionary<string, ObrPrzypiecie> nowe)
    {
        if (el.Na == null || !nowe.TryGetValue(el.Na, out var p) || p.Tor == null || Math.Abs(p.Tor.S1 - p.Tor.S0) < 1e-6) return null;
        return Math.Sign(p.Tor.S1 - p.Tor.S0) * (el.Kier < 0 ? -1 : 1);
    }

    /// <summary>
    /// Tor UT i km węzłów łańcucha: kotwice – ramiona zakotwiczonych rozjazdów na końcach, nazwane sygnalizatory na łańcuchu (km, kierunek),
    /// numery torów, przypięcia potwierdzone odcinków; końce bez kotwicy – koniec toru UT w kierunku km. Wynik – przypięcia odcinków.
    /// </summary>
    /// <summary>Kotwice łańcucha z km: rozjazdy zakotwiczone na końcach i nazwane sygnalizatory na odcinkach (położenie wzdłuż łańcucha, km, tor UT).</summary>
    static List<(double Poz, double Km, string Track)> KotwiceLancucha(Ctx c, Lancuch l, Func<string, string> utOf)
    {
        var res = new List<(double, double, string)>(); var ut = c.Ut;
        foreach (var (wi, e) in new[] { (0, l.Odc[0].E), (l.Wezly.Count - 1, l.Odc[^1].E) })
            if (utOf(l.Wezly[wi]) is string zid && ut.Turnouts.FirstOrDefault(z => z.Id == zid) is UtTurnout z && Ramie(c, l.Wezly[wi], e, z) is var (tr, s, _))
                res.Add((l.Poz[wi], s, tr));
        for (int i = 0; i < l.Odc.Count; i++)
        {
            var (e, fwd) = l.Odc[i];
            foreach (var el in c.O.Elementy.Where(x => x.Na == e.Id))
                if (utOf(el.Id) is string did && ut.Devices.FirstOrDefault(d => d.Id == did) is UtDevice dev)
                    res.Add((l.Poz[i] + (l.Poz[i + 1] - l.Poz[i]) * (fwd ? el.T : 1 - el.T), dev.S, dev.Track));
        }
        return res;
    }

    /// <summary>
    /// Łańcuch przez kilka torów UT połączonych końcami (np. szlak wewnętrzny narysowany jedną linią od stacji do stacji: tor stacji – tor szlaku – tor
    /// drugiej stacji): kotwice po kolei grupami torów, granica w węźle łańcucha najbliższym styku torów (proporcjonalnie do km między kotwicami);
    /// łańcuch dzieli się na części z kotwicą w km styku na granicy (<paramref name="extra"/>). Bez węzła między kotwicami – łańcuch bez zmian.
    /// </summary>
    static List<Lancuch> PodzielNaTory(Ctx c, List<Lancuch> lan, Func<string, string> utOf, Dictionary<Lancuch, List<(double Poz, double Km, string Track)>> extra,
        List<(ObrOdcinek E, int[] P)> podzial = null)
    {
        extra.Clear();
        var res = new List<Lancuch>();
        foreach (var l in lan)
        {
            var kt = KotwiceLancucha(c, l, utOf).OrderBy(k => k.Poz).ToList();
            var grupy = new List<List<(double Poz, double Km, string Track)>>();
            foreach (var k in kt) if (grupy.Count > 0 && grupy[^1][0].Track == k.Track) grupy[^1].Add(k); else grupy.Add(new() { k });
            if (grupy.Count < 2 || grupy.Select(g => g[0].Track).Distinct().Count() != grupy.Count) { res.Add(l); continue; }
            var granice = new List<(int Wi, double KmA, string TA, double KmB, string TB)>();
            for (int i = 0; i + 1 < grupy.Count && granice != null; i++)
            {
                var a = grupy[i][^1]; var b = grupy[i + 1][0];
                // droga torów od a do b przez styki końców (także przez tory bez kotwic – np. tor szlaku między semaforami wjazdowymi dwóch stacji)
                var hops = Droga(c, a.Track, b.Track);
                if (hops == null) { granice = null; break; }
                // odległości w UT od kotwicy a do kolejnych styków i do kotwicy b – granice na rysunku w tym samym stosunku
                var d = new List<double>(); double sum = 0, km = a.Km;
                foreach (var h in hops) { sum += Math.Abs(h.SA - km); d.Add(sum); km = h.SB; }
                sum += Math.Abs(b.Km - km);
                double od = a.Poz;
                for (int h = 0; h < hops.Count; h++)
                {
                    double poz = a.Poz + (b.Poz - a.Poz) * d[h] / Math.Max(1e-9, sum);
                    // węzeł w kotwicy też może być granicą, gdy kotwica stoi na samym styku (do 0,5 m – semafor wjazdowy na końcu toru szlaku)
                    bool wA = h == 0 && d[h] < 0.5, wB = h == hops.Count - 1 && sum - d[h] < 0.5;
                    int wi = -1; double best = double.MaxValue;
                    for (int j = 1; j < l.Wezly.Count - 1; j++)
                        if ((wA ? l.Poz[j] > od - 1e-6 : l.Poz[j] > od + 1e-6) && (wB ? l.Poz[j] < b.Poz + 1e-6 : l.Poz[j] < b.Poz - 1e-6) && Math.Abs(l.Poz[j] - poz) < best) { best = Math.Abs(l.Poz[j] - poz); wi = j; }
                    // najbliższy węzeł daleko od styku (np. węzeł w środku szlaku, a styk przy semaforze) – tor UT trafiłby na zły kawałek rysunku
                    if (wi >= 0 && best > Math.Max(8, 0.05 * Math.Abs(b.Poz - a.Poz)) && podzial != null && PunktPodzialu(c.O, l, od, b.Poz, poz) is var (pe0, pp0))
                    {
                        podzial.Add((pe0, pp0)); granice = null; break;
                    }
                    if (wi < 0)
                    {
                        // brak węzła między kotwicami – podział odcinka rysunku w miejscu styku (Przypnij dzieli i liczy od nowa)
                        if (podzial != null && PunktPodzialu(c.O, l, od, b.Poz, poz) is var (pe, pp)) podzial.Add((pe, pp));
                        granice = null; break;
                    }
                    granice.Add((wi, hops[h].SA, hops[h].TA, hops[h].SB, hops[h].TB)); od = l.Poz[wi];
                }
            }
            if (granice == null) { res.Add(l); continue; }
            int start = 0;
            for (int g = 0; g <= granice.Count; g++)
            {
                int end = g < granice.Count ? granice[g].Wi : l.Wezly.Count - 1;
                var s = new Lancuch();
                for (int j = start; j <= end; j++) { s.Wezly.Add(l.Wezly[j]); s.Poz.Add(l.Poz[j] - l.Poz[start]); }
                for (int j = start; j < end; j++) s.Odc.Add(l.Odc[j]);
                var ex = new List<(double, double, string)>();
                if (g > 0) ex.Add((0, granice[g - 1].KmB, granice[g - 1].TB));
                if (g < granice.Count) ex.Add((s.Dl, granice[g].KmA, granice[g].TA));
                extra[s] = ex; res.Add(s); start = end;
            }
        }
        return res;
    }

    /// <summary>
    /// Punkt podziału odcinka łańcucha najbliżej położenia <paramref name="poz"/>, ściśle między <paramref name="od"/> a <paramref name="doP"/>:
    /// w całych jednostkach rysunku na odcinku poziomym, pionowym albo ukośnym 45°; null – nie da się (odcinek za krótki, inny skos).
    /// </summary>
    static (ObrOdcinek E, int[] P)? PunktPodzialu(ObrObraz o, Lancuch l, double od, double doP, double poz)
    {
        (ObrOdcinek E, int[] P)? best = null; double bd = double.MaxValue;
        for (int i = 0; i < l.Odc.Count; i++)
        {
            double p0 = l.Poz[i], p1 = l.Poz[i + 1];
            if (p1 <= od + 1e-6 || p0 >= doP - 1e-6) continue;
            var a = ObrGraf.Wezel(o, l.Wezly[i]).P; var b = ObrGraf.Wezel(o, l.Wezly[i + 1]).P;
            int dx = b[0] - a[0], dy = b[1] - a[1], n = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if (n < 2 || dx % n != 0 || dy % n != 0) continue;
            double krok = (p1 - p0) / n;
            int kMin = Math.Max(1, (int)Math.Floor((od - p0) / krok + 1e-9) + 1), kMax = Math.Min(n - 1, (int)Math.Ceiling((doP - p0) / krok - 1e-9) - 1);
            if (kMin > kMax) continue;
            int k = Math.Clamp((int)Math.Round((poz - p0) / krok), kMin, kMax);
            double d = Math.Abs(p0 + k * krok - poz);
            if (d < bd) { bd = d; best = (l.Odc[i].E, new[] { a[0] + dx / n * k, a[1] + dy / n * k }); }
        }
        return best;
    }

    /// <summary>Najkrótsza (w liczbie torów, do 6) droga po stykach końców torów UT od toru <paramref name="z"/> do <paramref name="na"/>: styki po kolei (tor, km styku – tor dalej, km).</summary>
    static List<(string TA, double SA, string TB, double SB)> Droga(Ctx c, string z, string na)
    {
        var prev = new Dictionary<string, (string From, double SA, double SB)> { [z] = (null, 0, 0) };
        var q = new Queue<(string T, int N)>(); q.Enqueue((z, 0));
        while (q.Count > 0)
        {
            var (t, n) = q.Dequeue();
            if (t == na) break;
            if (n >= 6) continue;
            foreach (var s in c.Styki.Where(s => s.OtherEnd && (s.Track == t || s.Other == t)))
            {
                string nt = s.Track == t ? s.Other : s.Track;
                if (prev.ContainsKey(nt) || !c.Len.ContainsKey(nt)) continue;
                double sa = s.Track == t ? (s.AtStart ? 0 : c.Len[t]) : s.OtherS, sb = s.Track == t ? s.OtherS : (s.AtStart ? 0 : c.Len[nt]);
                prev[nt] = (t, sa, sb); q.Enqueue((nt, n + 1));
            }
        }
        if (!prev.ContainsKey(na) || z == na) return null;
        var res = new List<(string, double, string, double)>();
        for (var t = na; prev[t].From != null; t = prev[t].From) res.Insert(0, (prev[t].From, prev[t].SA, t, prev[t].SB));
        return res;
    }

    static void LancuchKm(Ctx c, Lancuch l, Func<string, string> utOf, Dictionary<string, ObrPrzypiecie> nowe,
        Action<string, string, string, ZobUtTor, string, List<string>> set, Dictionary<Lancuch, string> torLan, Dictionary<(Lancuch, int), double> kmW, Dictionary<Lancuch, Func<double, double>> kmF,
        List<(double Poz, double Km, string Track)> extra = null)
    {
        var o = c.O; var ut = c.Ut;
        var tory = new Dictionary<string, int>();       // tor → liczba świadectw
        var kotwice = new List<(double Poz, double Km, string Track)>();
        // kierunek km wzdłuż łańcucha (od początku do końca): z ramion rozjazdów (pewny) i z kierunków semaforów (słabszy – zły semafor wychodzi w kroku 4)
        var kier = new List<(string Track, int Dir)>(); var kierS = new List<(string Track, int Dir)>();
        void Tor(string t) { if (t != null) tory[t] = tory.GetValueOrDefault(t) + 1; }
        var numery = new List<(ObrElement El, List<string> Kand)>();
        // końce: rozjazdy zakotwiczone
        foreach (var (wi, e, sgn) in new[] { (0, l.Odc[0].E, 1), (l.Wezly.Count - 1, l.Odc[^1].E, -1) })
        {
            var w = l.Wezly[wi];
            if (utOf(w) is string zid && ut.Turnouts.FirstOrDefault(z => z.Id == zid) is UtTurnout z && Ramie(c, w, e, z) is var (tr, s, d))
            {
                Tor(tr); kotwice.Add((l.Poz[wi], s, tr)); kier.Add((tr, d * sgn));
            }
        }
        // granica z sąsiednią częścią łańcucha (styk torów UT) – kotwica w km styku
        foreach (var x in extra ?? new()) { Tor(x.Track); Tor(x.Track); kotwice.Add(x); }
        // potwierdzone przypięcia odcinków (tor wybrany przez człowieka)
        foreach (var (e, _) in l.Odc)
            if (nowe.TryGetValue(e.Id, out var p) && p.Stan == ObrPrzypiecie.Potwierdzone && p.Tor?.Tor != null) { Tor(p.Tor.Tor); Tor(p.Tor.Tor); }
        // elementy na łańcuchu: nazwane sygnalizatory (km i kierunek), numery torów
        for (int i = 0; i < l.Odc.Count; i++)
        {
            var (e, fwd) = l.Odc[i];
            double p0 = l.Poz[i], p1 = l.Poz[i + 1];
            foreach (var el in o.Elementy.Where(x => x.Na == e.Id))
            {
                double poz = p0 + (p1 - p0) * (fwd ? el.T : 1 - el.T);
                if (utOf(el.Id) is string did && ut.Devices.FirstOrDefault(d => d.Id == did) is UtDevice dev)
                {
                    Tor(dev.Track); kotwice.Add((poz, dev.S, dev.Track));
                    if (ObrSymbole.Kierunkowy(el.Typ)) kierS.Add((dev.Track, dev.Dir * (fwd ? 1 : -1) * (el.Kier < 0 ? -1 : 1)));
                }
                if (el.Typ == ObrSymbole.NumerToru && Tekst(el).Length > 0)
                    numery.Add((el, ut.Tracks.Where(t => c.NaObrazie(t.Id) && Norm(t.Number) == Norm(Tekst(el))).Select(t => t.Id).ToList()));
            }
        }
        // numery torów: ten sam numer w kilku posterunkach obrazu – tor wskazany przez inne kotwice łańcucha, inaczej tor posterunku najbliższej kotwicy rysunku
        List<((double, double)? P, string Post)> kotw = null;
        foreach (var (el, kand) in numery)
        {
            var wybrane = kand.Count > 1 && kand.Where(tory.ContainsKey).ToList() is { Count: > 0 } wt ? wt : kand;
            if (wybrane.Count > 1 && ObrSymbole.Pozycja(o, el) is var (nx, ny, _, _))
            {
                kotw ??= Kotwice(ut, o, nowe);
                var posty = wybrane.Select(t => Posterunki.Of(ut, t)?.Id).ToHashSet();
                var naj = posty.Any(ps => !kotw.Any(z => z.Post == ps)) ? default : kotw.Where(z => posty.Contains(z.Post)).OrderBy(z => Math.Pow(z.P.Value.Item1 - nx, 2) + Math.Pow(z.P.Value.Item2 - ny, 2)).FirstOrDefault();
                if (naj.Post != null && wybrane.Where(t => Posterunki.Of(ut, t)?.Id == naj.Post).ToList() is { Count: 1 } jt) wybrane = jt;
            }
            foreach (var t in wybrane) Tor(t);
        }
        string Odc(string powod, List<string> kand = null)
        {
            foreach (var (e, _) in l.Odc) set(e.Id, ObrPrzypiecie.DoWyjasnienia, null, null, powod, kand);
            torLan[l] = null; return null;
        }
        if (tory.Count == 0) { Odc("brak kotwicy – wybierz tor UT poniżej (najazd pokazuje go w podglądzie UT) albo nadaj numer zwrotnicy, nazwę semafora lub numer toru (N)", c.Tory.ToList()); return; }
        if (tory.Count > 1)
        {
            var naj = tory.OrderByDescending(x => x.Value).ToList();
            if (naj[0].Value == naj[1].Value) { Odc($"sprzeczne kotwice: {string.Join(" i ", naj.Select(x => Opis(ut, x.Key)))}", naj.Select(x => x.Key).ToList()); return; }
        }
        var track = tory.OrderByDescending(x => x.Value).First().Key;
        torLan[l] = track;
        var kt = kotwice.Where(k => k.Track == track).OrderBy(k => k.Poz).ToList();
        var dirs = kier.Where(k => k.Track == track).Select(k => k.Dir).Distinct().ToList();
        int dir = dirs.Count == 1 ? dirs[0] : 0;
        if (dirs.Count > 1) { Odc($"kierunek km na {Opis(ut, track)} sprzeczny na końcach (sprawdź rozjazdy)", new() { track }); torLan[l] = track; return; }
        if (dir == 0 && kt.Count >= 2 && Math.Abs(kt[^1].Km - kt[0].Km) > 0.5) dir = kt[^1].Km > kt[0].Km ? 1 : -1;
        if (dir == 0 && kierS.Where(k => k.Track == track).Select(k => k.Dir).Distinct().ToList() is { Count: 1 } ds) dir = ds[0];
        if (dir == 0) { Odc($"{Opis(ut, track)}: nieznany kierunek km – potrzebne dwie kotwice albo rozjazd na końcu", new() { track }); torLan[l] = track; return; }
        double len = c.Len.GetValueOrDefault(track);
        // końce bez kotwicy – km z przypięcia potwierdzonego (np. obraz z wersji 1 z torem narysowanym w części), inaczej koniec toru UT w kierunku km;
        // km zapisane na końcu toru (przy długości z ostatniego F8) – nowy koniec toru (UT zmieniony), inne – w granicach toru
        double? KmKonca(bool start)
        {
            var (e, fwd) = start ? l.Odc[0] : l.Odc[^1];
            if (!nowe.TryGetValue(e.Id, out var p) || p.Stan != ObrPrzypiecie.Potwierdzone || p.Tor?.Tor != track || Math.Abs(p.Tor.S1 - p.Tor.S0) < 1e-9) return null;
            double km = start == fwd ? p.Tor.S0 : p.Tor.S1;
            if (p.DlToru is double dl && Math.Abs(dl - len) > 0.5 && Math.Abs(km - dl) < 0.5) return len;
            return Math.Clamp(km, 0, len);
        }
        if (kt.Count == 0 || kt[0].Poz > 1e-6) kt.Insert(0, (0, KmKonca(true) ?? (dir > 0 ? 0 : len), track));
        if (kt[^1].Poz < l.Dl - 1e-6) kt.Add((l.Dl, KmKonca(false) ?? (dir > 0 ? len : 0), track));
        // kolejność wzdłuż km zgodna z kierunkiem
        for (int i = 1; i < kt.Count; i++)
            if ((kt[i].Km - kt[i - 1].Km) * dir < -0.5) { Odc($"kolejność na {Opis(ut, track)} różna niż w UT (km {kt[i - 1].Km:0} przed {kt[i].Km:0})", new() { track }); torLan[l] = track; return; }
        double KmAt(double poz)
        {
            for (int i = 1; i < kt.Count; i++)
                if (poz <= kt[i].Poz + 1e-9)
                {
                    double a = kt[i - 1].Poz, b = kt[i].Poz;
                    return b - a < 1e-9 ? kt[i].Km : kt[i - 1].Km + (kt[i].Km - kt[i - 1].Km) * (poz - a) / (b - a);
                }
            return kt[^1].Km;
        }
        for (int i = 0; i < l.Wezly.Count; i++) kmW[(l, i)] = Math.Round(KmAt(l.Poz[i]), 2);
        kmF[l] = KmAt;
        for (int i = 0; i < l.Odc.Count; i++)
        {
            var (e, fwd) = l.Odc[i];
            double ka = kmW[(l, fwd ? i : i + 1)], kb = kmW[(l, fwd ? i + 1 : i)];
            // tor potwierdzony przez człowieka – km z kotwic, stan zostaje
            if (nowe.TryGetValue(e.Id, out var pp) && pp.Stan == ObrPrzypiecie.Potwierdzone && pp.Tor?.Tor == track) pp.Tor = new ZobUtTor { Tor = track, S0 = ka, S1 = kb };
            else set(e.Id, ObrPrzypiecie.Pewne, null, new ZobUtTor { Tor = track, S0 = ka, S1 = kb }, null, null);
            if (nowe.TryGetValue(e.Id, out var pn) && pn.Tor?.Tor == track) pn.DlToru = Math.Round(len, 2);
        }
    }

    /// <summary>Przypięcie odcinka z km nieaktualnym: km zapisane i km, które da F8 teraz (NaN – F8 nie wyznaczy km, odcinek do wyjaśnienia).</summary>
    public record KmNieaktualny(string El, string Tor, double S0, double S1, double N0, double N1);

    /// <summary>
    /// Odcinki przypięte (pewne, potwierdzone) z km innym niż da F8 przy obecnym UT i rysunku (więcej niż 1 m) – UT zmieniony (inna długość toru,
    /// przesunięty styk) albo rysunek zmieniony po ostatnim F8. Porównywane tylko km, które idą na pulpit (<see cref="ObrKompilacja"/>): końce
    /// łańcucha i zmiany stylu albo toru; km w rozjeździe kompilacja i tak bierze z UT, km w załamaniach nie idzie na pulpit. Tor inny po F8
    /// albo do wyjaśnienia – zawsze (km NaN). Liczone na kopii obrazu, obraz bez zmian.
    /// </summary>
    public static List<KmNieaktualny> KmNieaktualne(UtProject ut, ObrObraz o)
    {
        bool Ok(ObrPrzypiecie p) => p?.Tor?.Tor != null && p.Stan is ObrPrzypiecie.Pewne or ObrPrzypiecie.Potwierdzone;
        var stare = o.Przypiecia.Where(p => Ok(p) && ut.Tracks.Any(t => t.Id == p.Tor.Tor)).GroupBy(p => p.El).ToDictionary(g => g.Key, g => g.First());
        var res = new List<KmNieaktualny>();
        if (stare.Count == 0) return res;
        var kopia = System.Text.Json.JsonSerializer.Deserialize<ObrObraz>(System.Text.Json.JsonSerializer.Serialize(o, UtDocument.Json), UtDocument.Json);
        Przypnij(ut, kopia);
        var nowe = kopia.Przypiecia.GroupBy(p => p.El).ToDictionary(g => g.Key, g => g.First());
        var rozj = o.Przypiecia.Where(p => p.Ut != null && p.Stan is ObrPrzypiecie.Pewne or ObrPrzypiecie.Potwierdzone && ObrGraf.Wezel(o, p.El) != null).Select(p => p.El).ToHashSet();
        foreach (var l in Lancuchy(o))
            for (int i = 0; i < l.Odc.Count; i++)
            {
                var (e, fwd) = l.Odc[i];
                if (!stare.TryGetValue(e.Id, out var p)) continue;
                var n = nowe.GetValueOrDefault(e.Id);
                if (!Ok(n) || n.Tor.Tor != p.Tor.Tor) { res.Add(new(e.Id, p.Tor.Tor, p.Tor.S0, p.Tor.S1, double.NaN, double.NaN)); continue; }
                // koniec odcinka w węźle łańcucha j (sąsiedni odcinek k): liczy się na końcu łańcucha i przy zmianie stylu albo toru, nie w rozjeździe
                bool Liczy(int j, int k) => !rozj.Contains(l.Wezly[j])
                    && (k < 0 || k >= l.Odc.Count || l.Odc[k].E.Styl != e.Styl || stare.GetValueOrDefault(l.Odc[k].E.Id)?.Tor.Tor != p.Tor.Tor);
                bool zA = Liczy(fwd ? i : i + 1, fwd ? i - 1 : i + 1), zB = Liczy(fwd ? i + 1 : i, fwd ? i + 1 : i - 1);
                if (zA && Math.Abs(n.Tor.S0 - p.Tor.S0) > 1 || zB && Math.Abs(n.Tor.S1 - p.Tor.S1) > 1) res.Add(new(e.Id, p.Tor.Tor, p.Tor.S0, p.Tor.S1, n.Tor.S0, n.Tor.S1));
            }
        return res;
    }

    /// <summary>Potwierdzenie przez człowieka: element przypięty do obiektu UT (dla odcinka – tor UT całego łańcucha).</summary>
    public static void Potwierdz(ObrObraz o, string el, string utId, UtProject ut)
    {
        o.Przypiecia.RemoveAll(p => p.El == el);
        if (ObrGraf.Odcinek(o, el) != null && ut.Tracks.Any(t => t.Id == utId))
        {
            var l = Lancuchy(o).FirstOrDefault(x => x.Odc.Any(z => z.E.Id == el));
            foreach (var (e, _) in l?.Odc ?? new())
            {
                o.Przypiecia.RemoveAll(p => p.El == e.Id);
                o.Przypiecia.Add(new ObrPrzypiecie { El = e.Id, Tor = new ZobUtTor { Tor = utId }, Stan = ObrPrzypiecie.Potwierdzone });
            }
            return;
        }
        o.Przypiecia.Add(new ObrPrzypiecie { El = el, Ut = utId, Stan = ObrPrzypiecie.Potwierdzone });
    }

    /// <summary>Element bez znaczenia ruchowego / bez obiektu w UT – „bez przypięcia” (decyzja człowieka).</summary>
    public static void BezPrzypiecia(ObrObraz o, string el)
    {
        o.Przypiecia.RemoveAll(p => p.El == el);
        o.Przypiecia.Add(new ObrPrzypiecie { El = el, Stan = ObrPrzypiecie.Brak });
    }

    /// <summary>
    /// Obiekty UT posterunków obrazu, których nie ma na rysunku (plan § 6 pkt 5): tory (brakujące km), rozjazdy, sygnalizatory (bez odstępowych –
    /// szlak, etap E6), wykolejnice, kozły, liczniki osi i złącza, perony, przejazdy.
    /// </summary>
    public static List<Nieumieszczony> Nieumieszczone(UtProject ut, ObrObraz o)
    {
        var c = Kontekst(ut, o);
        var res = new List<Nieumieszczony>();
        var ok = o.Przypiecia.Where(p => p.Stan is ObrPrzypiecie.Pewne or ObrPrzypiecie.Potwierdzone).ToList();
        var uz = ok.Where(p => p.Ut != null).Select(p => p.Ut).ToHashSet();
        foreach (var t in ut.Tracks.Where(t => c.NaObrazie(t.Id)))
        {
            double len = c.Len[t.Id];
            var iv = ok.Where(p => p.Tor?.Tor == t.Id).Select(p => p.Tor).Concat(o.Wezly.Where(w => w.Poza != null).SelectMany(w => w.Poza).Where(z => z.Tor == t.Id))
                .Select(z => (A: Math.Min(z.S0, z.S1), B: Math.Max(z.S0, z.S1))).OrderBy(x => x.A).ToList();
            var luki = new List<(double, double)>(); double cur = 0;
            foreach (var (a, b) in iv) { if (a - cur > 1) luki.Add((cur, a)); cur = Math.Max(cur, b); }
            if (len - cur > 1) luki.Add((cur, len));
            if (luki.Count > 0) res.Add(new(ZobUt.GTory, t.Id, $"{Opis(ut, t.Id)}: " + (iv.Count == 0 ? "nie ma na rysunku" : "brak km " + string.Join(", ", luki.Select(x => $"{x.Item1:0}–{x.Item2:0}")))));
        }
        foreach (var z in ut.Turnouts.Where(z => c.NaObrazie(z.Track) && !uz.Contains(z.Id))) res.Add(new(ZobUt.GRozjazdy, z.Id, Opis(ut, z.Id)));
        foreach (var d in ut.Devices.Where(d => c.NaObrazie(d.Track) && !uz.Contains(d.Id)))
        {
            var g = d.Kind switch
            {
                UtDevice.Semafor when d.Funkcja != UtDevice.FOdstepowy => ZobUt.GSygnalizatory,
                UtDevice.TarczaMan or UtDevice.TarczaOstrz => ZobUt.GSygnalizatory,
                UtDevice.Wykolejnica => ZobUt.GWykolejnice,
                UtDevice.Koziol => ZobUt.GKozly,
                UtDevice.LicznikOsi or UtDevice.Zlacze => ZobUt.GPrzerwy,
                _ => null,
            };
            if (g != null) res.Add(new(g, d.Id, Opis(ut, d.Id)));
        }
        // semafory odstępowe – na obrazie przez odstępy SBL szlaku (E6)
        var szlakiOdst = o.Elementy.Where(e => e.Typ == ObrSymbole.OdstepySbl).Select(e => ok.FirstOrDefault(p => p.El == e.Id)?.Ut).Where(x => x != null).ToHashSet();
        var drogi = ut.Lines.Count > 0 ? Szlaki.Drogi(ut) : new();
        foreach (var d in ut.Devices.Where(d => c.NaObrazie(d.Track) && d.Kind == UtDevice.Semafor && d.Funkcja == UtDevice.FOdstepowy))
        {
            var ln = ut.Lines.FirstOrDefault(l => drogi.TryGetValue(l.Id, out var dr) && dr.Kawalki.Any(k => k.Track == d.Track && d.S >= k.S0 - 0.5 && d.S <= k.S1 + 0.5));
            if (ln == null || !szlakiOdst.Contains(ln.Id)) res.Add(new(ZobUt.GSygnalizatory, d.Id, Opis(ut, d.Id) + (ln != null ? $" – szlak {ln.Numer}: odstępy SBL" : "")));
        }
        foreach (var p in ut.Platforms.Where(p => c.NaObrazie(p.Track) && !uz.Contains(p.Id))) res.Add(new(ZobUt.GPerony, p.Id, Opis(ut, p.Id)));
        foreach (var x in ut.LevelCrossings.Where(x => c.NaObrazie(x.Track) && !uz.Contains(x.Id))) res.Add(new(ZobUt.GPrzejazdy, x.Id, Opis(ut, x.Id)));
        // obraz poglądowy (Ie-104 § 6 ust. 4): tory, rozjazdy, semafory, szlaki, przejazdy – tarcz, wykolejnic, kozłów, przerw i peronów się nie wymaga
        if (o.Rodzaj == ZobObraz.Pogladowy)
            res.RemoveAll(n => n.Grupa is ZobUt.GWykolejnice or ZobUt.GKozly or ZobUt.GPrzerwy or ZobUt.GPerony
                || n.Grupa == ZobUt.GSygnalizatory && ut.Devices.FirstOrDefault(d => d.Id == n.Id)?.Kind != UtDevice.Semafor);
        return res;
    }
}

namespace Lcs.Model;

/// <summary>Położenie rozjazdu na torze zasadniczym: na prostej albo na stałym promieniu (Ok), na krzywej przejściowej, na granicy prostej i łuku.</summary>
public enum NaLuku { Ok, Przejsciowa, Granica }

/// <summary>
/// Wariant montażu rozjazdu: nowy wielobok toru zasadniczego (<see cref="V"/> – zmiana R / L jednego łuku), przesunięcie rozjazdu
/// wzdłuż toru (<see cref="Shift"/> [m]) albo inny typ (<see cref="Type"/>). <see cref="Dev"/> – o ile najwyżej przesunie się oś toru [m];
/// <see cref="BezL"/> – łuk traci krzywą przejściową, <see cref="Zalom"/> – wierzchołek łuku przesunięty (oba tylko przyciskiem, nie samo).
/// </summary>
public record Wariant(string Label, List<UtVertex> V = null, double Shift = 0, string Type = null, double Dev = 0, int Vertex = -1, double R0 = 0, double R1 = 0, bool BezL = false, bool Zalom = false)
{
    public bool Luk => V != null;
}

/// <summary>Wynik „Napraw tory i łuki”: co zmieniono i czego nie dało się poprawić samemu.</summary>
public record NaprawaWynik(List<string> Zmiany, List<string> Zostalo);

/// <summary>
/// Montaż rozjazdów na łuku (PLAN_EDYTOR_UT.md § 5i) i „Napraw tory i łuki”. Rozjazd łukowy układa się na stałym promieniu – nie na
/// krzywej przejściowej ani na granicy prostej i łuku. Gdy rozjazd tam trafi, granica łuku przesuwa się poza rozjazd (zmiana R albo
/// krótsza przejściowa jednego łuku; wierzchołek i proste zostają) – sama, gdy oś toru przesuwa się najwyżej o <see cref="AutoDev"/>.
/// Po każdej zmianie wieloboku kilometraż obiektów toru liczy się z ich położenia (<see cref="Reanchor"/>), więc rozjazdy, urządzenia
/// i punkty km zostają tam, gdzie były w terenie, choć długość toru się zmienia.
/// </summary>
public static class Naprawa
{
    /// <summary>Zapas między granicą łuku a początkiem / końcem rozjazdu po montażu [m].</summary>
    public const double Zapas = 1;
    /// <summary>Największe przesunięcie osi toru, które montaż i naprawa robią same [m]; większe – tylko przyciskiem w panelu rozjazdu.</summary>
    public const double AutoDev = 0.30;
    /// <summary>Najkrótsza przejściowa po skróceniu [m]; krótsza – wariant bez przejściowej.</summary>
    public const double MinL = 10;
    /// <summary>Wariant łuku, który przesuwa oś bardziej niż tyle [m], nie jest proponowany – to już inny tor niż narysowany.</summary>
    public const double MaxDev = 3;
    /// <summary>Wierzchołek bez znaczenia (prawie na prostej) – usuwany, gdy oś przesuwa się mniej niż tyle [m].</summary>
    public const double ZbednyDev = 0.01;

    static UtTrack Tr(UtProject p, string id) => p.Tracks.FirstOrDefault(t => t.Id == id);
    static double Len(UtTurnout z) => Katalog.Find(z.Type) is RozjazdTyp ty ? ty.A + ty.B : 0;
    static List<UtVertex> Clone(IEnumerable<UtVertex> v) => v.Select(x => x.Clone()).ToList();

    /// <summary>Elementy toru pod odcinkiem km s1–s2 (rozszerzonym o <paramref name="margin"/>): przejściowa, różne elementy (granica) albo jeden.</summary>
    public static NaLuku Polozenie(TrasaResult r, double s1, double s2, double margin = 0)
    {
        double lo = Math.Min(s1, s2) - margin, hi = Math.Max(s1, s2) + margin, acc = 0;
        ElKind? first = null; bool mixed = false;
        foreach (var e in r.Elements)
        {
            double a = acc / r.K, b = (acc + e.Len) / r.K; acc += e.Len;
            if (e.Len <= 1e-9 || b <= lo + 0.01 || a >= hi - 0.01) continue;
            if (e.Kind == ElKind.Przejsciowa) return NaLuku.Przejsciowa;
            if (first == null) first = e.Kind; else if (first != e.Kind) mixed = true;
        }
        return mixed ? NaLuku.Granica : NaLuku.Ok;
    }

    public static NaLuku Polozenie(UtProject p, UtTurnout t) =>
        Tr(p, t.Track) is UtTrack m && Trasa.Compute(m) is var r && r.Elements.Count > 0 ? Polozenie(r, t.S, t.S + t.Dir * Len(t)) : NaLuku.Ok;

    // ------------------------------------------------------------------ kilometraż po zmianie toru

    /// <summary>Kilometraż najbliższego punktu osi [m w terenie], szukany w oknie ±<paramref name="win"/> wokół <paramref name="s0"/>.</summary>
    public static double Near(TrasaResult r, XY p, double s0, double win)
    {
        double len = r.Length, a = Math.Max(0, s0 - win), b = Math.Min(len, s0 + win);
        if (b <= a) { a = 0; b = len; }
        int n = Math.Max(4, (int)Math.Ceiling(b - a));
        double bs = a, bd = double.MaxValue;
        for (int i = 0; i <= n; i++) { double s = a + (b - a) * i / n, d = XY.Dist(r.At(s).P, p); if (d < bd) { bd = d; bs = s; } }
        double h = (b - a) / n, lo = Math.Max(0, bs - h), hi = Math.Min(len, bs + h);
        for (int i = 0; i < 50; i++)
        {
            double m1 = lo + (hi - lo) * 0.382, m2 = lo + (hi - lo) * 0.618;
            if (XY.Dist(r.At(m1).P, p) < XY.Dist(r.At(m2).P, p)) hi = m2; else lo = m1;
        }
        return (lo + hi) / 2;
    }

    /// <summary>Przeliczenie kilometrażu: km na starej osi → km punktu najbliższego na nowej (końce toru – na końce; poza torem – z tym samym wystawaniem).</summary>
    public static Func<double, double> Mapa(TrasaResult o, TrasaResult n)
    {
        if (o.Elements.Count == 0 || n.Elements.Count == 0) return s => s;
        double win = Math.Abs(n.Length - o.Length) + 30;
        return s =>
        {
            double c = Math.Clamp(s, 0, o.Length);
            double m = c <= 1e-6 ? 0 : c >= o.Length - 1e-6 ? n.Length : Near(n, o.At(c).P, c, win);
            return m + (s - c);
        };
    }

    /// <summary>Kilometraż obiektów toru <paramref name="tr"/> po zmianie jego geometrii (stara trasa <paramref name="old"/>): rozjazdy (PR),
    /// skrzyżowania, urządzenia, odcinki, perony, przejazdy, punkty km, obiekty inżynieryjne, niweleta i teren – zostają w tym samym miejscu w terenie.</summary>
    public static void Reanchor(UtProject p, UtTrack tr, TrasaResult old)
    {
        var nr = Trasa.Compute(tr);
        if (old.Elements.Count == 0 || nr.Elements.Count == 0 || (Math.Abs(nr.Length - old.Length) < 1e-9 && SameAxis(old, nr))) return;
        var M = Mapa(old, nr);
        foreach (var z in p.Turnouts.Where(z => z.Track == tr.Id)) z.S = Math.Round(M(z.S), 3);
        foreach (var c in p.Crossings) { if (c.TrackA == tr.Id) c.SA = M(c.SA); if (c.TrackB == tr.Id) c.SB = M(c.SB); }
        foreach (var d in p.Devices.Where(d => d.Track == tr.Id)) d.S = Math.Round(M(d.S), 2);
        foreach (var d in (p.Sections ?? new()).Where(d => d.Track == tr.Id)) d.S = Math.Round(M(d.S), 2);
        foreach (var d in (p.Platforms ?? new()).Where(d => d.Track == tr.Id)) { d.S0 = Math.Round(M(d.S0), 2); d.S1 = Math.Round(M(d.S1), 2); }
        foreach (var d in (p.LevelCrossings ?? new()).Where(d => d.Track == tr.Id)) d.S = Math.Round(M(d.S), 2);
        foreach (var d in (p.KmPoints ?? new()).Where(d => d.Track == tr.Id)) d.S = Math.Round(M(d.S), 2);
        Obiekty.MapKm(p, tr.Id, M);
        foreach (var q in (tr.Profile ?? new()).Concat(tr.Terrain ?? new())) q.S = Math.Round(M(q.S), 2);
    }

    static bool SameAxis(TrasaResult a, TrasaResult b)
    {
        for (double s = 0; s <= a.Length; s += Math.Max(1, a.Length / 50)) if (XY.Dist(a.At(s).P, b.At(s).P) > 1e-6) return false;
        return true;
    }

    /// <summary>Nowy wielobok toru z przeliczeniem kilometrażu jego obiektów.</summary>
    public static void SetVertices(UtProject p, UtTrack tr, List<UtVertex> nv)
    {
        var old = Trasa.Compute(tr);
        tr.Vertices = nv;
        Reanchor(p, tr, old);
    }

    // ------------------------------------------------------------------ montaż rozjazdu

    /// <summary>Największe przesunięcie osi <paramref name="nr"/> względem <paramref name="o"/> na km s1–s2 starej osi [m w terenie].</summary>
    static double Deviation(TrasaResult o, TrasaResult nr, double s1, double s2, Func<double, double> M)
    {
        double max = 0;
        s1 = Math.Max(0, s1); s2 = Math.Min(o.Length, s2);
        int n = Math.Max(2, (int)Math.Ceiling((s2 - s1) / 2));
        for (int i = 0; i <= n; i++)
        {
            double s = s1 + (s2 - s1) * i / n; var pt = o.At(s).P;
            double sn = Near(nr, pt, M(s), 15);
            max = Math.Max(max, XY.Dist(nr.At(sn).P, pt) / o.K);
        }
        return max;
    }

    /// <summary>Kilometraż początku i końca łuku (z przejściowymi) wierzchołka <paramref name="vi"/> [m w terenie]; null – brak łuku.</summary>
    static (double S, double E)? Curve(TrasaResult r, int vi)
    {
        double acc = 0, cs = -1, ce = -1;
        foreach (var e in r.Elements) { if (e.Vertex == vi && e.Kind != ElKind.Prosta && e.Len > 1e-9) { if (cs < 0) cs = acc / r.K; ce = (acc + e.Len) / r.K; } acc += e.Len; }
        return cs < 0 ? null : (cs, ce);
    }

    /// <summary>
    /// Warianty montażu rozjazdu, który leży na granicy prostej i łuku albo na krzywej przejściowej: (1) zmiana jednego łuku toru
    /// zasadniczego – R w górę albo w dół, krótsza przejściowa, przejściowa usunięta (z R dobranym) – tak, żeby rozjazd leżał w całości na
    /// prostej albo na stałym promieniu z zapasem <see cref="Zapas"/>; inne rozjazdy toru nie mogą przy tym trafić na granicę, a tor
    /// nie może mieć nowych błędów; (2) przesunięcie rozjazdu (łącznika) wzdłuż toru (do 160 m – za długą przejściową); (3) przy zwrotnym R &lt; 140 m – inny typ.
    /// Kolejność: łuki od najmniejszego przesunięcia osi (najwyżej <see cref="MaxDev"/>), potem przesunięcia, potem typy.
    /// </summary>
    public static List<Wariant> Warianty(UtProject p, UtTurnout t, bool zalom = true)
    {
        var res = new List<Wariant>();
        var main = Tr(p, t.Track); var typ = Katalog.Find(t.Type); if (main == null || typ == null) return res;
        var r0 = Trasa.Compute(main); if (r0.Elements.Count == 0) return res;
        double len = typ.A + typ.B;
        var pol = Polozenie(r0, t.S, t.S + t.Dir * len);
        if (pol != NaLuku.Ok)
        {
            if (main.Align == null) res.AddRange(ArcVariants(p, t, main, r0, len, zalom));
            res.AddRange(ShiftVariants(p, t, main, r0, len));
        }
        if (Rozjazdy.Geometry(main, t) is TurnoutGeo g && g.Kind != "Rz" && g.RDiv > 0 && g.RDiv < Rozjazdy.MinRDiv)
        {
            string rail = typ.Name.Split(' ').ElementAtOrDefault(1) ?? "";
            foreach (var ot in Katalog.Types.Where(x => x.Kind == RozjazdTyp.Rz && x.Name != typ.Name && x.Name.Contains(rail)).OrderBy(x => x.R))
            {
                var tt = new UtTurnout { Type = ot.Name, Track = t.Track, S = t.S, Dir = t.Dir, Side = t.Side };
                if (Rozjazdy.Geometry(main, tt) is TurnoutGeo go && (go.RDiv <= 0 || go.RDiv >= Rozjazdy.MinRDiv))
                { res.Add(new Wariant($"zmień typ na {ot.Name} (tor zwrotny {(go.RDiv > 0 ? $"R {go.RDiv:0} m" : "prosty")})", Type: ot.Name)); break; }
            }
        }
        return res;
    }

    static List<Wariant> ArcVariants(UtProject p, UtTurnout t, UtTrack main, TrasaResult r0, double len, bool zalom)
    {
        var res = new List<Wariant>();
        double k = r0.K;
        var v0 = main.Vertices;
        var others = p.Turnouts.Where(z => z.Track == main.Id && Katalog.Find(z.Type) != null).ToList();
        var before = others.ToDictionary(z => z.Id, z => Polozenie(r0, z.S, z.S + z.Dir * Len(z)));
        int err0 = r0.Notes.Count(n => n.Error);
        // rozjazdy, których odgałęzieniem jest ten tor: łuk za skosem nie może zacząć się przed KR
        var skew = p.Turnouts.Where(z => z.Branch == main.Id && z.Single).ToList();
        double s1 = Math.Min(t.S, t.S + t.Dir * len), s2 = Math.Max(t.S, t.S + t.Dir * len);
        for (int vi = 1; vi < v0.Count - 1; vi++)
        {
            var v = v0[vi];
            if (v.R <= 0 || v.LockR || Rozjazdy.Locked(p, main, vi)) continue;
            if (Curve(r0, vi) is not var (cs, ce) || ce <= s1 - Zapas || cs >= s2 + Zapas) continue;
            int vv = vi;
            bool Valid(List<UtVertex> nv, out TrasaResult nr, out Func<double, double> M)
            {
                nr = Trasa.Compute(nv, k); M = null;
                if (nr.Elements.Count == 0 || nr.Notes.Count(n => n.Error) > err0 || nr.Notes.Any(n => n.Error && (n.Vertex == vv || n.Vertex == vv + 1))) return false;
                M = Mapa(r0, nr);
                // zmieniony kawałek osi w km starej osi: przed łukiem km bez zmian, za nim przesunięte o różnicę długości
                double lo = cs, hi = ce;
                if (Curve(nr, vv) is var (ncs, nce)) { lo = Math.Min(lo, ncs); hi = Math.Max(hi, nce - (nr.Length - r0.Length)); }
                // najpierw sam rozjazd (zwykle to on odrzuca wariant), inne – tylko w zasięgu zmiany
                foreach (var z in others.OrderBy(z => z.Id == t.Id ? 0 : 1))
                {
                    if (z.Id != t.Id && !z.BranchAtEnd && Math.Max(z.S, z.S + z.Dir * Len(z)) < lo - 1 && Math.Min(z.S, z.S + z.Dir * Len(z)) < lo - 1
                        || z.Id != t.Id && !z.BranchAtEnd && Math.Min(z.S, z.S + z.Dir * Len(z)) > hi + 1) continue;
                    // drugi rozjazd łącznika nie trzyma km – staje na przecięciu skosu pierwszego z nową osią toru
                    var (zs, zd) = z.BranchAtEnd && z.Partner != null ? Rozjazdy.PairPos(p, z, nv) : (M(z.S), z.Dir);
                    var q = Polozenie(nr, zs, zs + zd * Len(z), z.Id == t.Id ? Zapas : 0);
                    if (z.Id == t.Id ? q != NaLuku.Ok : before[z.Id] == NaLuku.Ok && q != NaLuku.Ok) return false;
                }
                foreach (var z in skew.Where(_ => vv == 2))
                    if (nr.Tangent.TryGetValue(2, out var tv) && Rozjazdy.Geometry(p, z) is TurnoutGeo gz && z.BranchLen - tv / k < gz.B - 0.01
                        && !(r0.Tangent.TryGetValue(2, out var tv0) && z.BranchLen - tv0 / k < gz.B - 0.01)) return false;
                return true;
            }
            bool Ok(List<UtVertex> nv) => Valid(nv, out _, out _);
            List<UtVertex> With(List<UtVertex> b, Action<UtVertex> set) { var c = Clone(b); set(c[vv]); return c; }
            // pierwsza wartość (od obecnej) przy której wariant jest dobry: kroki geometryczne, potem połowienie i zaokrąglenie
            List<UtVertex> Search(List<UtVertex> b, Func<double, List<UtVertex>> make, double x0, double step, double min, double max, double round)
            {
                double prev = x0;
                for (int i = 1; i <= 160; i++)
                {
                    double x = x0 * Math.Pow(step, i);
                    if (x < min || x > max) return null;
                    if (Ok(make(x)))
                    {
                        double lo = prev, hi = x;
                        for (int j = 0; j < 30; j++) { double m = (lo + hi) / 2; if (Ok(make(m))) hi = m; else lo = m; }
                        foreach (var xr in new[] { step > 1 ? Math.Ceiling(hi / round) * round : Math.Floor(hi / round) * round, hi })
                            if (Ok(make(xr))) return make(xr);
                        return null;
                    }
                    prev = x;
                }
                return null;
            }
            var cands = new List<(List<UtVertex> V, string What)>();
            void R(List<UtVertex> b, string pre)
            {
                foreach (double st in new[] { 1.01, 1 / 1.01 })
                    if (Search(b, x => With(b, q => q.R = x), b[vv].R, st, Rozjazdy.MinRDiv, 100000, 1) is List<UtVertex> nv)
                        cands.Add((nv, pre + $"R {v.R:0} → {nv[vv].R:0} m"));
            }
            R(v0, "");
            // przejściowe pod rozjazdem: krótsza (R bez zmian) albo bez przejściowej (R dobrany)
            double acc = 0; bool inT = false, outT = false;
            foreach (var e in r0.Elements)
            {
                double a = acc / r0.K, b = (acc + e.Len) / r0.K; acc += e.Len;
                if (e.Vertex == vi && e.Kind == ElKind.Przejsciowa && b > s1 - Zapas && a < s2 + Zapas) { if (e.Out) outT = true; else inT = true; }
            }
            foreach (var (on, isOut) in new[] { (inT, false), (outT, true) })
            {
                if (!on) continue;
                double l0 = isOut ? v.LOut : v.L;
                List<UtVertex> L(double x) => With(v0, q => { var lo = q.LOut; if (isOut) q.L2 = x; else { q.L = x; q.L2 = lo; } });
                string nm = isOut ? "przejściowa na wyjściu" : "przejściowa na wejściu";
                // przejściowa krótsza niż MinL nie ma sensu – wtedy wariant „bez przejściowej”
                if (Search(v0, L, l0, 0.97, 0, l0, 1) is List<UtVertex> nv && (isOut ? nv[vi].LOut : nv[vi].L) >= MinL) cands.Add((nv, $"{nm} L {l0:0.#} → {(isOut ? nv[vi].LOut : nv[vi].L):0.#} m"));
                var z0 = L(0);
                if (Ok(z0)) cands.Add((z0, $"bez przejściowej (L {l0:0.#} → 0)"));
                else R(z0, $"bez przejściowej, ");
            }
            // załom przesunięty wzdłuż prostej przed nim albo za nim (sąsiednie wierzchołki zostają), R dobrany – gdy sam promień nie
            // wystarcza, np. krótki łuk pod rozjazdem, który w terenie leży na łuku (rozjazd łukowy na całym łuku albo łuk obok rozjazdu)
            int nz = cands.Count;
            var pv = v.P;
            // rozjazd w całości na łuku tego wierzchołka (rozjazd łukowy) – osobno od wariantów z łukiem obok rozjazdu
            bool CalyNaLuku(List<UtVertex> nv)
            {
                if (!Valid(nv, out var nr, out var M) || Curve(nr, vv) is not var (a, b)) return false;
                double ts = M(t.S), te = ts + t.Dir * len;
                return Math.Min(ts, te) > a && Math.Max(ts, te) < b;
            }
            foreach (var (od, nm) in zalom ? new[] { (v0[vi - 1].P, "przed"), (v0[vi + 1].P, "za") } : Array.Empty<(XY, string)>())
            {
                double dx = pv.X - od.X, dy = pv.Y - od.Y, dl = Math.Sqrt(dx * dx + dy * dy); if (dl < 1e-6) continue;
                dx /= dl; dy /= dl;
                foreach (int sg in new[] { -1, 1 })
                {
                    bool luk = false, obok = false;
                    for (double dm = 2; dm <= 40 && dm * k < dl - 1 && !(luk && obok); dm += 2)
                    {
                        var moved = With(v0, q => { q.X = pv.X + dx * sg * dm * k; q.Y = pv.Y + dy * sg * dm * k; });
                        string pre = $"załom W{vi + 1} o {dm:0} m wzdłuż prostej {nm} nim ({(sg > 0 ^ nm == "za" ? "w stronę rosnącego km" : "w stronę malejącego km")}), ";
                        var nowe = new List<(List<UtVertex> V, string What)>();
                        if (Ok(moved)) nowe.Add((moved, pre + $"R {v.R:0} m"));
                        else
                            // krok 3 % – z krótkiego łuku (R 300) do promienia łuku pod całym rozjazdem (kilka tysięcy m)
                            foreach (double st in new[] { 1.03, 1 / 1.03 })
                                if (Search(moved, x => With(moved, q => q.R = x), v.R, st, Rozjazdy.MinRDiv, 100000, 1) is List<UtVertex> mv)
                                    nowe.Add((mv, pre + $"R {v.R:0} → {mv[vv].R:0} m"));
                        foreach (var c in nowe)
                        {
                            bool nl = CalyNaLuku(c.V);
                            if (nl ? luk : obok) continue;
                            cands.Add(c); if (nl) luk = true; else obok = true;
                        }
                    }
                }
            }
            for (int ci = 0; ci < cands.Count; ci++)
            {
                var (nv, what) = cands[ci]; bool przesZalom = ci >= nz;
                Valid(nv, out var nr, out var M);
                double d1 = r0.Tangent.TryGetValue(vi, out var a1) && nr.Tangent.TryGetValue(vi, out var b1) ? Math.Max(0, b1 - a1) / k : 0;
                double d2 = r0.Tangent2.TryGetValue(vi, out var a2) && nr.Tangent2.TryGetValue(vi, out var b2) ? Math.Max(0, b2 - a2) / k : 0;
                // przesunięty załom obraca proste do sąsiednich wierzchołków – przesunięcie osi z całego toru
                double dev = przesZalom ? Deviation(r0, nr, 0, r0.Length, M) : Deviation(r0, nr, cs - d1 - 2, ce + d2 + 2, M);
                var (ts, td) = t.BranchAtEnd && t.Partner != null ? Rozjazdy.PairPos(p, t, nv) : (M(t.S), t.Dir);
                double te = ts + td * len, n1 = Math.Min(ts, te), n2 = Math.Max(ts, te);
                string where = Curve(nr, vi) is not var (ncs, nce) ? $"bez łuku W{vi + 1}"
                    : n2 <= ncs + 0.01 ? $"łuk W{vi + 1} zaczyna się za rozjazdem"
                    : n1 >= nce - 0.01 ? $"łuk W{vi + 1} kończy się przed rozjazdem"
                    : $"rozjazd w całości na łuku W{vi + 1}";
                res.Add(new Wariant($"{where} ({what}, oś najwyżej {dev * 100:0} cm)", nv, Dev: dev, Vertex: vi, R0: v.R, R1: nv[vi].R, BezL: v.L > 0 && nv[vi].L <= 0 || v.LOut > 0 && nv[vi].LOut <= 0, Zalom: przesZalom));
            }
        }
        // sprawdzenie na kopii (drugi rozjazd łącznika ustawia się sam na przecięciu): rozjazd na stałym promieniu, żaden inny nie trafia na granicę
        var bad0 = p.Turnouts.Where(z => Polozenie(p, z) != NaLuku.Ok).Select(z => z.Id).ToHashSet();
        string json = UtDocument.Serialize(p);
        return res.GroupBy(w => w.Label).Select(g => g.First()).Where(w => w.Dev <= MaxDev).OrderBy(w => w.Zalom).ThenBy(w => w.Dev).Where(w =>
        {
            var tmp = UtDocument.Deserialize(json);
            Zastosuj(tmp, tmp.Turnouts.First(x => x.Id == t.Id), w); Rozjazdy.SyncTurnouts(tmp, Dotkniete(tmp, main.Id));
            return tmp.Turnouts.All(z => z.Id != t.Id && bad0.Contains(z.Id) || Polozenie(tmp, z) == NaLuku.Ok)
                && tmp.Tracks.Where(x => x.Id == main.Id || p.Turnouts.Any(z => z.Track == main.Id && z.Branch == x.Id)).All(x => Trasa.Compute(x).Notes.Count(n => n.Error) <= Trasa.Compute(Tr(p, x.Id)).Notes.Count(n => n.Error));
        }).ToList();
    }

    /// <summary>
    /// Przesunięcie rozjazdu wzdłuż toru; w łączniku – całej pary (km pierwszego rozjazdu, drugi staje na przecięciu skosu), tak żeby
    /// oba rozjazdy leżały na prostej albo na stałym promieniu. Wstępnie: pierwszy z zapasem, drugi w przewidywanym miejscu (tory
    /// równoległe – przesuwa się o tyle samo); potem sprawdzenie na kopii po <see cref="Rozjazdy.Sync"/>.
    /// </summary>
    static List<Wariant> ShiftVariants(UtProject p, UtTurnout t, UtTrack main, TrasaResult r0, double len)
    {
        var res = new List<Wariant>();
        string json = null;
        var m = t.BranchAtEnd && t.Partner != null ? p.Turnouts.FirstOrDefault(x => x.Id == t.Partner) : t;
        if (m == null || Tr(p, m.Track) is not UtTrack mt) return res;
        var pair = m.Partner != null ? p.Turnouts.FirstOrDefault(x => x.Id == m.Partner) : null;
        var rm = m == t ? r0 : Trasa.Compute(mt); double lm = Len(m);
        TrasaResult rp = null; double rel = 0, lp = 0;
        if (pair != null && Tr(p, pair.Track) is UtTrack pt && Rozjazdy.Geometry(p, m) is TurnoutGeo gm)
        {
            rp = Trasa.Compute(pt); lp = Len(pair);
            if (rp.Elements.Count > 0) rel = Math.Sign(Math.Cos(rp.At(Math.Clamp(pair.S, 0, rp.Length)).Az - rm.At(Math.Clamp(m.S, 0, rm.Length)).Az));
        }
        List<(double, double)> Busy(UtTurnout x) => p.Turnouts.Where(z => z.Id != m.Id && z.Id != pair?.Id && z.Track == x.Track && Katalog.Find(z.Type) != null)
            .Select(z => (Math.Min(z.S, z.S + z.Dir * Len(z)), Math.Max(z.S, z.S + z.Dir * Len(z)))).ToList();
        var busyM = Busy(m); var busyP = pair != null ? Busy(pair) : new();
        bool Free(TrasaResult r, List<(double, double)> busy, double s, double e, double margin)
        {
            double lo = Math.Min(s, e), hi = Math.Max(s, e);
            return lo >= 0 && hi <= r.Length && !busy.Any(b => b.Item2 > lo + 0.01 && b.Item1 < hi - 0.01) && Polozenie(r, s, e, margin) == NaLuku.Ok;
        }
        foreach (int sg in new[] { -1, 1 })
        {
            int tries = 0;
            for (double d = 0.25; d <= 160 && tries < 6; d += 0.25)
            {
                double s = m.S + sg * d;
                if (!Free(rm, busyM, s, s + m.Dir * lm, Zapas)) continue;
                if (rp != null && rel != 0) { double ps = pair.S + rel * sg * d; if (!Free(rp, busyP, ps, ps + pair.Dir * lp, 0)) continue; }
                // sprawdzenie na kopii: oba rozjazdy na prostej / stałym promieniu, bez nowych uwag, odgałęzienie bez błędów
                tries++;
                json ??= UtDocument.Serialize(p);
                var tmp = UtDocument.Deserialize(json); var tm = tmp.Turnouts.First(x => x.Id == m.Id);
                var u0 = Uwagi(tmp, tm); tm.S = s; Rozjazdy.SyncTurnouts(tmp, tmp.Turnouts.Where(x => x.Id == m.Id || x.Id == m.Partner));
                tm = tmp.Turnouts.FirstOrDefault(x => x.Id == m.Id);
                if (tm == null || Polozenie(tmp, tm) != NaLuku.Ok) continue;
                if (pair != null && (tmp.Turnouts.FirstOrDefault(x => x.Id == pair.Id) is not UtTurnout tp || Polozenie(tmp, tp) != NaLuku.Ok)) continue;
                if (Uwagi(tmp, tm).Except(u0).Any()) continue;
                if (Tr(tmp, tm.Branch) is UtTrack br && !Trasa.Compute(br).Ok) continue;
                double km = s - m.S;
                string tn = mt.Number.Length > 0 ? $" toru {mt.Number}" : " toru";
                res.Add(new Wariant(pair != null ? $"przesuń łącznik {m.Number}/{pair.Number} o {Math.Abs(km):0.##} m w stronę {(km > 0 ? "rosnącego" : "malejącego")} km{tn}"
                    : $"przesuń rozjazd o {Math.Abs(km):0.##} m w stronę {(km > 0 ? "rosnącego" : "malejącego")} km{tn}", Shift: km, Dev: Math.Abs(km)));
                break;
            }
        }
        return res.OrderBy(w => Math.Abs(w.Shift)).ToList();
    }

    /// <summary>Rozjazdy, które trzeba ustawić po zmianie toru: na nim, z odgałęzieniem na nim, i ich pary w łącznikach.</summary>
    static List<UtTurnout> Dotkniete(UtProject p, string track)
    {
        var ids = p.Turnouts.Where(z => z.Track == track || z.Branch == track).SelectMany(z => new[] { z.Id, z.Partner }).Where(x => x != null).ToHashSet();
        return p.Turnouts.Where(z => ids.Contains(z.Id)).ToList();
    }

    /// <summary>Uwagi rozjazdu i jego pary w łączniku (bez numerów – do porównania przed i po).</summary>
    static List<string> Uwagi(UtProject p, UtTurnout t)
    {
        var u = Rozjazdy.Check(p, t).Select(x => x.Split(" – ")[0]).ToList();
        if (t.Partner != null && p.Turnouts.FirstOrDefault(x => x.Id == t.Partner) is UtTurnout o) u.AddRange(Rozjazdy.Check(p, o).Select(x => "2: " + x.Split(" – ")[0]));
        return u;
    }

    /// <summary>Zastosowanie wariantu (w zmianie dokumentu); wielobok – z przeliczeniem kilometrażu obiektów toru.</summary>
    public static void Zastosuj(UtProject p, UtTurnout t, Wariant w)
    {
        if (w.V != null && Tr(p, t.Track) is UtTrack tr) SetVertices(p, tr, Clone(w.V));
        else if (w.Type != null) foreach (var x in p.Turnouts.Where(x => x.Id == t.Id || x.Id == t.Partner)) x.Type = w.Type;
        else if (w.Shift != 0)
        {
            // drugi rozjazd łącznika stoi na przecięciu – przesuwa się pierwszy (cała para)
            var m = t.BranchAtEnd && t.Partner != null ? p.Turnouts.FirstOrDefault(x => x.Id == t.Partner) ?? t : t;
            m.S += w.Shift;
        }
    }

    /// <summary>Wariant, który montaż robi sam: rozjazd poza stałym promieniem, zmiana łuku przesuwa oś najwyżej o <paramref name="maxDev"/>,
    /// łuk zachowuje krzywe przejściowe (mogą się skrócić, nie niżej niż <see cref="MinL"/>).</summary>
    public static Wariant Auto(UtProject p, UtTurnout t, double maxDev = AutoDev) =>
        Polozenie(p, t) == NaLuku.Ok ? null : Warianty(p, t, zalom: false).FirstOrDefault(w => w.Luk && !w.BezL && w.Dev <= maxDev);

    /// <summary>
    /// Plan montażu samoczynnego rozjazdów <paramref name="ids"/>: w łączniku para razem – próba na kopii (pierwszy rozjazd, potem drugi),
    /// przyjęta tylko, gdy po niej oba leżą na prostej albo na stałym promieniu (inaczej poprawka pod jednym zmieniałaby łuk na próżno –
    /// zostaje przesunięcie łącznika z panelu). Zwraca kroki do <see cref="Zastosuj"/> po kolei i rozjazdy, których nie zamontowano.
    /// </summary>
    public static (List<(string Id, string Number, Wariant W)> Kroki, List<string> Nie) AutoPlan(UtProject p, IEnumerable<string> ids, double maxDev = AutoDev)
    {
        var kroki = new List<(string, string, Wariant)>(); var nie = new List<string>();
        var done = new HashSet<string>();
        string json = UtDocument.Serialize(p);
        var cur = UtDocument.Deserialize(json);
        foreach (var id in ids)
        {
            if (id == null || done.Contains(id) || cur.Turnouts.FirstOrDefault(x => x.Id == id) is not UtTurnout t0) continue;
            var grp = new[] { t0.BranchAtEnd ? t0.Partner : t0.Id, t0.BranchAtEnd ? t0.Id : t0.Partner }.Where(x => x != null).Distinct().ToList();
            foreach (var g in grp) done.Add(g);
            if (grp.All(g => cur.Turnouts.FirstOrDefault(x => x.Id == g) is not UtTurnout z || Polozenie(cur, z) == NaLuku.Ok)) continue;
            var tmp = UtDocument.Deserialize(UtDocument.Serialize(cur)); var plan = new List<(string, string, Wariant)>();
            foreach (var g in grp)
            {
                if (tmp.Turnouts.FirstOrDefault(x => x.Id == g) is not UtTurnout z || Polozenie(tmp, z) == NaLuku.Ok) continue;
                var w = Auto(tmp, z, maxDev); if (w == null) break;
                Zastosuj(tmp, z, w); Rozjazdy.SyncTurnouts(tmp, Dotkniete(tmp, z.Track).Concat(tmp.Turnouts.Where(x => x.Id == z.Id || x.Id == z.Partner)).Distinct()); plan.Add((g, z.Number, w));
            }
            if (grp.All(g => tmp.Turnouts.FirstOrDefault(x => x.Id == g) is not UtTurnout z || Polozenie(tmp, z) == NaLuku.Ok)) { kroki.AddRange(plan); cur = tmp; }
            else nie.AddRange(grp.Select(g => cur.Turnouts.FirstOrDefault(x => x.Id == g)).Where(z => z != null && Polozenie(cur, z) != NaLuku.Ok).Select(z => z.Number));
        }
        return (kroki, nie);
    }

    /// <summary>Wykonanie planu z <see cref="AutoPlan"/> (w zmianie dokumentu).</summary>
    public static void Wykonaj(UtProject p, IEnumerable<(string Id, string Number, Wariant W)> kroki)
    {
        foreach (var (id, _, w) in kroki)
            if (p.Turnouts.FirstOrDefault(x => x.Id == id) is UtTurnout z) { Zastosuj(p, z, w); Rozjazdy.Sync(p); }
    }

    // ------------------------------------------------------------------ napraw tory i łuki

    /// <summary>Wierzchołek, którego nie wolno usunąć: ustawia go rozjazd (PR, ŚR, skos) albo cały tor łączący łącznika.</summary>
    static bool Protected(UtProject p, UtTrack t, int i) =>
        Rozjazdy.Locked(p, t, i) || p.Turnouts.Any(z => z.Branch == t.Id && z.Single && i == 2) || t.Vertices[i].LockR;

    /// <summary>
    /// „Napraw tory i łuki” (cały projekt albo jeden tor): (1) zbędne wierzchołki – powtórzone punkty i załamania tak małe, że oś
    /// przesuwa się mniej niż 1 cm; (2) łuki, które się nie mieszczą – R zmniejszane jak w „Dopasuj promienie”; (3) rozjazdy na granicy
    /// prostej i łuku albo na przejściowej – montaż na łuku, gdy oś przesuwa się najwyżej o <paramref name="maxDev"/>. Kilometraż
    /// obiektów przeliczany po każdej zmianie. Czego nie da się poprawić samemu – w <see cref="NaprawaWynik.Zostalo"/>.
    /// </summary>
    public static NaprawaWynik Napraw(UtProject p, string onlyTrack = null, double maxDev = AutoDev)
    {
        var zm = new List<string>(); var zo = new List<string>();
        Rozjazdy.Sync(p);       // odgałęzienia rozjazdów z bieżącą geometrią (promień toru zwrotnego rozjazdu łukowego)
        string Nr(UtTrack t) => t.Number.Length > 0 ? $"tor {t.Number}" : $"tor (bez numeru, {t.Id})";
        var tracks = p.Tracks.Where(t => onlyTrack == null || t.Id == onlyTrack).ToList();
        foreach (var t in tracks)
        {
            if (t.Align != null) { if (!Trasa.Compute(t).Ok) zo.Add($"{Nr(t)}: trasa z elementów – popraw na wykresie krzywizny"); continue; }
            double k = Trasa.KFor(t);
            // (1) zbędne wierzchołki
            var v = Clone(t.Vertices); int removed = 0;
            for (int i = 1; i < v.Count - 1;)
            {
                if (Protected(p, t, i + removed)) { i++; continue; }
                double l1 = XY.Dist(v[i - 1].P, v[i].P), l2 = XY.Dist(v[i].P, v[i + 1].P);
                double ang = Math.Abs(Math.IEEERemainder(Trasa.Az(v[i].P, v[i + 1].P) - Trasa.Az(v[i - 1].P, v[i].P), 2 * Math.PI));
                double dev = l1 < 1e-3 || l2 < 1e-3 ? 0 : Math.Sin(ang) * l1 * l2 / (l1 + l2) / k;
                if (dev < ZbednyDev && ang < Math.PI / 2) { v.RemoveAt(i); removed++; } else i++;
            }
            if (removed > 0) { SetVertices(p, t, v); zm.Add($"{Nr(t)}: usunięte zbędne wierzchołki ({removed}) – załamania poniżej {ZbednyDev * 100:0} cm"); }
            // (2) łuki, które się nie mieszczą
            if (Trasa.Compute(t).Notes.Any(n => n.Error && n.Text.Contains("nie mieści")))
            {
                // R zmniejszane po 1 % – tylko gdy wszystkie łuki toru się zmieszczą (inaczej zostaje, z uwagą)
                var (nv, ch) = Budowa.FitRadii(t.Vertices, k, i => Rozjazdy.Locked(p, t, i), Rozjazdy.MinRDiv, 0.99);
                if (ch.Count > 0 && !Trasa.Compute(nv, k).Notes.Any(n => n.Error && n.Text.Contains("nie mieści")))
                { SetVertices(p, t, nv); zm.Add($"{Nr(t)}: łuki dopasowane – " + string.Join(", ", ch.Select(c => $"W{c.I + 1} R {c.From:0} → {c.To:0} m"))); }
            }
            foreach (var n in Trasa.Compute(t).Notes.Where(n => n.Error)) zo.Add($"{Nr(t)}, W{n.Vertex + 1}: {n.Text}");
        }
        // (3) rozjazdy na granicy łuku i na przejściowej
        var ids = p.Turnouts.Where(z => onlyTrack == null || z.Track == onlyTrack).OrderBy(z => z.BranchAtEnd ? 1 : 0).Select(z => z.Id).ToList();
        var (kroki, _) = AutoPlan(p, ids, maxDev);
        Wykonaj(p, kroki);
        foreach (var (_, num, w) in kroki) zm.Add($"rozjazd {num}: {w.Label}");
        foreach (var id in ids)
        {
            var z = p.Turnouts.FirstOrDefault(x => x.Id == id); if (z == null) continue;
            var pol = Polozenie(p, z); if (pol == NaLuku.Ok) continue;
            var ws = Warianty(p, z);
            string gdzie = pol == NaLuku.Przejsciowa ? "na krzywej przejściowej" : "na granicy prostej i łuku";
            zo.Add($"rozjazd {z.Number}: {gdzie} – " + (ws.Count > 0 ? $"poprawka przesuwa oś o więcej niż {maxDev * 100:0} cm, usuwa przejściową albo nie wystarcza dla obu rozjazdów łącznika – wybierz w panelu rozjazdu: {ws[0].Label}" : "brak poprawki samego łuku – przesuń rozjazd albo popraw łuk ręcznie"));
        }
        foreach (var z in p.Turnouts.Where(z => onlyTrack == null || z.Track == onlyTrack))
            foreach (var n in Rozjazdy.Check(p, z).Where(n => !n.Contains("granicy prostej") && !n.Contains("krzywej przejściowej")))
                zo.Add($"rozjazd {z.Number}: {n}");
        // wspólne konstrukcje (bramki, wysięgniki) poza jednym przekrojem – który sygnalizator stoi dobrze, wie projektant
        foreach (var (mast, m) in Urzadzenia.Konstrukcje(p).Where(k => k.M.Count >= 2 && (onlyTrack == null || k.M.Any(x => x.Track == onlyTrack))))
            if (Urzadzenia.Przekrzywienie(p, m[0]) > 0.5)
                zo.Add($"{(mast == UtDevice.MastWysiegnik ? "wysięgnik" : "bramka")} {string.Join("/", m.Select(x => x.Name))}: sygnalizatory nie w jednym przekroju (do {Urzadzenia.Przekrzywienie(p, m[0]):0.#} m) – zaznacz ten, który stoi dobrze, i „Ustaw bramkę w przekroju” w panelu");
        return new NaprawaWynik(zm, zo.Distinct().ToList());
    }
}

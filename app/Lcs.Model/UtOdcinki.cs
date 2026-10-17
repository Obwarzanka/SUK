namespace Lcs.Model;

/// <summary>
/// Odcinek kontroli niezajętości (odcinek torowy albo zwrotnicowy, Ig-10 § 4 ust. 1 pkt 9). Granice odcinków to urządzenia
/// projektanta: liczniki osi i złącza izolowane (<see cref="UtDevice.LicznikOsi"/>, <see cref="UtDevice.Zlacze"/>) oraz końce torów.
/// Zasięg odcinka program wyznacza z układu (<see cref="Odcinki.Compute"/>) – kawałek toru między granicami, przez rozjazdy
/// i skrzyżowania. Odcinek ma nazwę (wpisuje projektant) przypiętą do miejsca na torze (<see cref="Track"/>, <see cref="S"/>):
/// nazwa należy do kawałka, w którym leży to miejsce.
/// </summary>
public class UtSection
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public string Track { get; set; }
    public double S { get; set; }
    /// <summary>Rodzaj kontroli niezajętości: <c>lo</c> – liczniki osi, <c>ot</c> – obwód torowy.</summary>
    public string Kind { get; set; } = LicznikOsi;

    public const string LicznikOsi = "lo", ObwodTorowy = "ot";
    public static readonly (string Key, string Name)[] Kinds = { (LicznikOsi, "liczniki osi"), (ObwodTorowy, "obwód torowy") };
}

/// <summary>Kawałek toru należący do jednego odcinka: tor, km od–do [m w terenie od początku toru], numer składowej.</summary>
public record OdcinekKawalek(string Track, double S0, double S1, int Comp)
{
    public double Len => S1 - S0;
}

/// <summary>
/// Wynik podziału układu na odcinki: kawałki torów (scalone na każdym torze), składowe (zbiór kawałków bez granicy między nimi)
/// z nazwami odcinków projektanta i uwagami.
/// </summary>
public class OdcinkiResult
{
    public List<OdcinekKawalek> Pieces { get; } = new();
    /// <summary>Składowa → odcinki projektanta, których miejsce nazwy w niej leży (0 – kawałek bez odcinka, 2 i więcej – brak granicy).</summary>
    public Dictionary<int, List<UtSection>> Names { get; } = new();
    /// <summary>Odcinek → składowa (brak – miejsce nazwy poza torem).</summary>
    public Dictionary<string, int> CompOf { get; } = new();
    public int Count { get; set; }
    public IEnumerable<OdcinekKawalek> Of(int comp) => Pieces.Where(p => p.Comp == comp);
    public double Length(int comp) => Of(comp).Sum(p => p.Len);
    /// <summary>Składowa w miejscu toru (−1 – poza torem).</summary>
    public int At(string track, double s) =>
        Pieces.Where(p => p.Track == track && s >= p.S0 - 1e-3 && s <= p.S1 + 1e-3).Select(p => p.Comp).DefaultIfEmpty(-1).First();
}

/// <summary>
/// Styki torów: koniec toru leżący na innym torze (na jego końcu albo w środku – np. początek odgałęzienia w PR rozjazdu).
/// </summary>
public record Styk(string Track, bool AtStart, string Other, double OtherS, bool OtherEnd);

public static class Topologia
{
    /// <summary>Tolerancja styku końca toru z innym torem [m w układzie].</summary>
    public const double Tol = 0.05;

    public static Dictionary<string, TrasaResult> Routes(UtProject p) => p.Tracks.ToDictionary(t => t.Id, t => Trasa.Compute(t));

    /// <summary>Wszystkie styki: dla każdego końca każdego toru – tory, na których ten koniec leży.</summary>
    public static List<Styk> Styki(UtProject p, Dictionary<string, TrasaResult> r = null)
    {
        r ??= Routes(p);
        var res = new List<Styk>();
        foreach (var t in p.Tracks)
        {
            var rt = r[t.Id]; if (rt.Elements.Count == 0) continue;
            foreach (var atStart in new[] { true, false })
            {
                var pt = rt.At(atStart ? 0 : rt.Length).P;
                foreach (var u in p.Tracks)
                {
                    if (u.Id == t.Id) continue;
                    var ru = r[u.Id]; if (ru.Elements.Count == 0) continue;
                    // szybkie odrzucenie: daleko od wieloboku
                    if (!Near(u, pt, 2000)) continue;
                    var (su, dist) = ru.Nearest(pt, 0.25);
                    if (dist > Tol) continue;
                    // dokładniej (Nearest na łamanej co 0,25 m – na łuku błąd do kilku mm)
                    var q = ru.At(su).P; if (XY.Dist(q, pt) > Tol) continue;
                    bool end = su < Tol / ru.K || su > ru.Length - Tol / ru.K;
                    if (end) su = su < ru.Length / 2 ? 0 : ru.Length;
                    res.Add(new Styk(t.Id, atStart, u.Id, su, end));
                }
            }
        }
        return res;
    }

    static bool Near(UtTrack u, XY p, double d)
    {
        double minX = u.Vertices.Min(v => v.X) - d, maxX = u.Vertices.Max(v => v.X) + d, minY = u.Vertices.Min(v => v.Y) - d, maxY = u.Vertices.Max(v => v.Y) + d;
        return p.X >= minX && p.X <= maxX && p.Y >= minY && p.Y <= maxY;
    }

    /// <summary>Koniec toru połączony z innym torem (albo koniec innego toru leży na nim w tym miejscu).</summary>
    public static bool Connected(List<Styk> styki, string track, bool atStart, double len) =>
        styki.Any(s => s.Track == track && s.AtStart == atStart) ||
        styki.Any(s => s.Other == track && Math.Abs(s.OtherS - (atStart ? 0 : len)) < 0.06);
}

/// <summary>Podział układu na odcinki kontroli niezajętości.</summary>
public static class Odcinki
{
    /// <summary>Urządzenia będące granicą odcinka.</summary>
    public static bool IsBoundary(UtDevice d) => d.Kind is UtDevice.LicznikOsi or UtDevice.Zlacze;

    public static OdcinkiResult Compute(UtProject p, Dictionary<string, TrasaResult> r = null)
    {
        r ??= Topologia.Routes(p);
        var styki = Topologia.Styki(p, r);
        var res = new OdcinkiResult();
        const double E = 1e-3;
        // cięcia: końce, granice, miejsca styków (początek odgałęzienia w środku toru) i skrzyżowań
        var cuts = new Dictionary<string, List<double>>(); var bounds = new Dictionary<string, List<double>>();
        foreach (var t in p.Tracks)
        {
            var rt = r[t.Id]; if (rt.Elements.Count == 0) continue;
            double L = rt.Length;
            var b = p.Devices.Where(d => d.Track == t.Id && IsBoundary(d)).Select(d => Math.Clamp(d.S, 0, L)).ToList();
            var c = new List<double> { 0, L }; c.AddRange(b);
            c.AddRange(styki.Where(s => s.Other == t.Id).Select(s => s.OtherS));
            c.AddRange(p.Crossings.Where(x => x.TrackA == t.Id).Select(x => x.SA));
            c.AddRange(p.Crossings.Where(x => x.TrackB == t.Id).Select(x => x.SB));
            c.Sort();
            var cl = new List<double>();
            foreach (var x in c) if (cl.Count == 0 || x - cl[^1] > E) cl.Add(x); else if (x >= L - E) cl[^1] = L;
            if (cl.Count < 2) cl = new() { 0, L };
            cuts[t.Id] = cl; bounds[t.Id] = b;
        }
        // atomy: przedziały między cięciami
        var atoms = new List<(string T, double S0, double S1)>(); var first = new Dictionary<string, int>();
        foreach (var (t, cl) in cuts)
        {
            first[t] = atoms.Count;
            for (int i = 0; i + 1 < cl.Count; i++) atoms.Add((t, cl[i], cl[i + 1]));
        }
        var parent = Enumerable.Range(0, atoms.Count).ToArray();
        int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
        void Union(int a, int b) { if (a >= 0 && b >= 0) parent[Find(a)] = Find(b); }
        bool IsB(string t, double s) => bounds.TryGetValue(t, out var b) && b.Any(x => Math.Abs(x - s) <= E);
        int AtomAt(string t, double s, int side = 0)
        {
            if (!cuts.TryGetValue(t, out var cl)) return -1;
            int n = cl.Count - 1;
            for (int i = 0; i < n; i++)
            {
                bool inside = side > 0 ? s >= cl[i] - E && s < cl[i + 1] - E : side < 0 ? s > cl[i] + E && s <= cl[i + 1] + E : s >= cl[i] - E && s <= cl[i + 1] + E;
                if (inside) return first[t] + i;
            }
            return side > 0 ? first[t] + n - 1 : first[t];
        }
        // sąsiednie atomy toru – bez granicy między nimi
        foreach (var (t, cl) in cuts)
            for (int i = 1; i + 1 < cl.Count; i++)
                if (!IsB(t, cl[i])) Union(first[t] + i - 1, first[t] + i);
        // styki: koniec toru t z torem u
        foreach (var st in styki)
        {
            if (!cuts.ContainsKey(st.Track) || !cuts.ContainsKey(st.Other)) continue;
            var rt = r[st.Track]; double se = st.AtStart ? 0 : rt.Length;
            if (IsB(st.Track, se) && !IsB(st.Other, st.OtherS)) continue;      // granica na końcu toru
            int a = st.AtStart ? first[st.Track] : first[st.Track] + cuts[st.Track].Count - 2;
            if (IsB(st.Other, st.OtherS))
            {
                if (IsB(st.Track, se) || st.OtherEnd) continue;                 // granica dokładnie w styku – koniec toru t poza oboma
                // granica w miejscu odgałęzienia: tor t należy do strony, w którą biegnie
                var ru = r[st.Other]; var (_, azu) = ru.At(st.OtherS); var (_, azt) = rt.At(st.AtStart ? Math.Min(rt.Length, 1) : Math.Max(0, rt.Length - 1));
                var dt = st.AtStart ? azt : azt + Math.PI;
                Union(a, AtomAt(st.Other, st.OtherS, Math.Cos(dt - azu) >= 0 ? 1 : -1));
            }
            else Union(a, AtomAt(st.Other, st.OtherS));
        }
        // skrzyżowania i rozjazdy krzyżowe – jeden odcinek na przecięciu
        foreach (var x in p.Crossings)
            if (cuts.ContainsKey(x.TrackA) && cuts.ContainsKey(x.TrackB)) Union(AtomAt(x.TrackA, x.SA), AtomAt(x.TrackB, x.SB));
        // składowe – numeracja po kolei
        var comp = new Dictionary<int, int>();
        for (int i = 0; i < atoms.Count; i++) { int f = Find(i); if (!comp.ContainsKey(f)) comp[f] = comp.Count; }
        res.Count = comp.Count;
        // kawałki: sąsiednie atomy tej samej składowej na torze – scalone
        foreach (var (t, cl) in cuts)
        {
            OdcinekKawalek cur = null;
            for (int i = 0; i + 1 < cl.Count; i++)
            {
                int c = comp[Find(first[t] + i)];
                if (cur != null && cur.Comp == c && !IsB(t, cl[i])) cur = cur with { S1 = cl[i + 1] };
                else { if (cur != null) res.Pieces.Add(cur); cur = new OdcinekKawalek(t, cl[i], cl[i + 1], c); }
            }
            if (cur != null) res.Pieces.Add(cur);
        }
        for (int c = 0; c < res.Count; c++) res.Names[c] = new();
        foreach (var s in p.Sections)
        {
            int c = res.At(s.Track, s.S);
            if (c < 0) continue;
            res.CompOf[s.Id] = c; res.Names[c].Add(s);
        }
        return res;
    }

    /// <summary>Opis składowej do uwag: tory i km.</summary>
    public static string Where(UtProject p, OdcinkiResult o, int comp)
    {
        var parts = o.Of(comp).Take(3).Select(k =>
        {
            var t = p.Tracks.FirstOrDefault(x => x.Id == k.Track);
            return $"tor {(t?.Number.Length > 0 ? t.Number : "(bez numeru)")} km {k.S0:0.#}–{k.S1:0.#}";
        });
        return string.Join(", ", parts) + (o.Of(comp).Count() > 3 ? "…" : "");
    }

    /// <summary>Rozjazdy, których PR leży w odcinku (odcinek zwrotnicowy).</summary>
    public static List<UtTurnout> Turnouts(UtProject p, OdcinkiResult o, int comp) =>
        p.Turnouts.Where(t => o.At(t.Track, t.S + t.Dir * 0.01) == comp).ToList();

    /// <summary>Uwagi o odcinkach: kawałek bez odcinka, odcinki bez granicy między sobą, nazwa poza torem, nazwa powtórzona, granica w rozjeździe.</summary>
    public static List<(string Text, string Track, double S, string Section)> Check(UtProject p, OdcinkiResult o)
    {
        var res = new List<(string, string, double, string)>();
        if (p.Sections.Count == 0) return res;           // odcinki nie są jeszcze projektowane – bez uwag
        for (int c = 0; c < o.Count; c++)
        {
            var n = o.Names[c]; var k = o.Of(c).OrderByDescending(x => x.Len).FirstOrDefault(); if (k == null) continue;
            double mid = (k.S0 + k.S1) / 2;
            if (n.Count == 0) res.Add(($"kawałek toru bez odcinka kontroli niezajętości ({Where(p, o, c)}, {o.Length(c):0.#} m) – nadaj nazwę narzędziem Odcinek albo dodaj granicę", k.Track, mid, null));
            else if (n.Count > 1) res.Add(($"odcinki {string.Join(", ", n.Select(x => x.Name))} nie są rozdzielone – brak licznika osi albo złącza izolowanego między nimi", k.Track, mid, n[0].Id));
        }
        foreach (var s in p.Sections)
        {
            if (!o.CompOf.ContainsKey(s.Id)) res.Add(($"odcinek {s.Name}: miejsce nazwy poza torem", s.Track, s.S, s.Id));
            if (s.Name.Length == 0) res.Add(("odcinek bez nazwy", s.Track, s.S, s.Id));
            else if (p.Sections.Any(x => x.Id != s.Id && x.Name == s.Name) && p.Sections.First(x => x.Name == s.Name).Id == s.Id)
                res.Add(($"nazwa odcinka {s.Name} powtórzona", s.Track, s.S, s.Id));
        }
        // granica w obrębie rozjazdu (między PR a KR na torze zasadniczym albo na odgałęzieniu przed KR)
        foreach (var t in p.Turnouts)
        {
            var typ = Katalog.Find(t.Type); if (typ == null) continue;
            double a = Math.Min(t.S, t.S + t.Dir * (typ.A + typ.B)), b = Math.Max(t.S, t.S + t.Dir * (typ.A + typ.B));
            foreach (var d in p.Devices.Where(d => IsBoundary(d) && d.Track == t.Track && d.S > a + 0.05 && d.S < b - 0.05))
                res.Add(($"granica odcinka ({UtDevice.KindInfo(d.Kind).Name.ToLower()} {d.Name}) w obrębie rozjazdu {t.Number} – przesuń ją za koniec rozjazdu (za ukres)", d.Track, d.S, null));
            if (t.Branch != null && !t.BranchAtEnd)
                foreach (var d in p.Devices.Where(d => IsBoundary(d) && d.Track == t.Branch && d.S > 0.05 && d.S < typ.A + typ.B - 0.05))
                    res.Add(($"granica odcinka ({UtDevice.KindInfo(d.Kind).Name.ToLower()} {d.Name}) na odgałęzieniu rozjazdu {t.Number} przed jego końcem", d.Track, d.S, null));
        }
        return res;
    }

    /// <summary>
    /// Podpowiedź nazwy odcinka (projektant wpisuje swoją): w odcinku z rozjazdami – „z” + numery rozjazdów, na torze z numerem –
    /// numer toru; nazwa zajęta – z kolejną literą.
    /// </summary>
    public static string Suggest(UtProject p, OdcinkiResult o, int comp)
    {
        var rz = comp >= 0 ? Turnouts(p, o, comp).Select(t => t.Number).Where(n => n.Length > 0).ToList() : new();
        string baseName;
        if (rz.Count > 0) baseName = "z" + string.Join("/", rz.OrderBy(x => x.Length).ThenBy(x => x));
        else
        {
            var k = comp >= 0 ? o.Of(comp).OrderByDescending(x => x.Len).FirstOrDefault() : null;
            var t = k != null ? p.Tracks.FirstOrDefault(x => x.Id == k.Track) : null;
            baseName = t != null && t.Number.Length > 0 ? t.Number : "o1";
        }
        var used = p.Sections.Select(s => s.Name).ToHashSet();
        if (!used.Contains(baseName)) return baseName;
        foreach (var c in "abcdefghijk") if (!used.Contains(baseName + c)) return baseName + c;
        for (int i = 2; ; i++) if (!used.Contains(baseName + "_" + i)) return baseName + "_" + i;
    }
}

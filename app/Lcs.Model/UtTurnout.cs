using System.Text.Json;

namespace Lcs.Model;

/// <summary>
/// Typ rozjazdu z katalogu (Zrodla/KZN_KOLEJ_02.pdf, Zrodla/rozjazdy_1.pdf). Oznaczenie jak w katalogu, np. „Rz 60E1-190-1:9”.
/// A – od początku rozjazdu (PR) do środka (ŚR), B – od ŚR do końca (KR) na obu torach [m]. Katalog KZN nie podaje A i B –
/// w danych domyślnych są przybliżone (<see cref="Approx"/>), właściciel może wpisać katalogowe w pliku Dane/rozjazdy.json.
/// </summary>
public class RozjazdTyp
{
    /// <summary>Oznaczenie, np. „Rz 60E1-190-1:9”.</summary>
    public string Name { get; set; }
    /// <summary>Rz – zwyczajny, Rkp – krzyżowy pojedynczy, Rkpd – krzyżowy podwójny, St – skrzyżowanie torów.</summary>
    public string Kind { get; set; } = Rz;
    /// <summary>Promień toru zwrotnego [m] (St – 0).</summary>
    public double R { get; set; }
    /// <summary>Skos 1:N.</summary>
    public double N { get; set; } = 9;
    public double A { get; set; }
    public double B { get; set; }
    /// <summary>Prędkość dopuszczalna na zwrot [km/h] (rozjazdy_1.pdf: 190 / 300 → 40, 500 → 60, 760 → 80, 1200 → 100).</summary>
    public int Vbok { get; set; }
    /// <summary>A i B przybliżone (nie z katalogu).</summary>
    public bool Approx { get; set; }

    public const string Rz = "Rz", Rkp = "Rkp", Rkpd = "Rkpd", St = "St";
    /// <summary>Kąt rozjazdu (skos 1:N) [rad].</summary>
    public double Alpha => Math.Atan(1 / N);
    public bool IsCrossing => Kind is Rkp or Rkpd or St;
    public override string ToString() => Name;
}

/// <summary>Katalog typów rozjazdów: plik JSON (lista <see cref="RozjazdTyp"/>) albo dane domyślne.</summary>
public static class Katalog
{
    public static List<RozjazdTyp> Types { get; private set; } = Default();
    public static string Source { get; private set; } = "dane domyślne";

    public static RozjazdTyp Find(string name) => Types.FirstOrDefault(t => t.Name == name);

    public static void Load(string file)
    {
        if (!File.Exists(file)) return;
        var list = JsonSerializer.Deserialize<List<RozjazdTyp>>(File.ReadAllText(file), UtDocument.Json);
        if (list is { Count: > 0 }) { foreach (var t in list) Fill(t); Types = list; Source = file; }
    }

    /// <summary>Brakujące A, B: A = R·tg(α/2) (geometryczny środek rozjazdu), B = A; 1:9 – A = B = 16,615 m (Rz 190 i 300).</summary>
    static void Fill(RozjazdTyp t)
    {
        if (t.A > 0 && t.B > 0) return;
        t.Approx = true;
        double a = t.Kind == RozjazdTyp.St ? 0 : t.R * Math.Tan(t.Alpha / 2);
        if (Math.Abs(t.N - 9) < 1e-9) a = Math.Max(a, 16.615);
        if (t.Kind == RozjazdTyp.St) a = 4.75 * t.N / 2;     // skrzyżowanie: połowa długości przy rozstawie 4,75 m
        if (t.A <= 0) t.A = Math.Round(a, 3);
        if (t.B <= 0) t.B = t.A;
    }

    public static List<RozjazdTyp> Default()
    {
        RozjazdTyp T(string n, string k, double r, double s, int v) { var t = new RozjazdTyp { Name = n, Kind = k, R = r, N = s, Vbok = v }; Fill(t); return t; }
        return new()
        {
            T("Rz 60E1-190-1:9", "Rz", 190, 9, 40), T("Rz 60E1-300-1:9", "Rz", 300, 9, 40), T("Rz 60E1-500-1:12", "Rz", 500, 12, 60),
            T("Rz 60E1-760-1:14", "Rz", 760, 14, 80), T("Rz 60E1-1200-1:18,5", "Rz", 1200, 18.5, 100),
            T("Rz 49E1-190-1:9", "Rz", 190, 9, 40), T("Rz 49E1-300-1:9", "Rz", 300, 9, 40), T("Rz 49E1-500-1:12", "Rz", 500, 12, 60),
            T("Rz 49E1-190-1:7,5", "Rz", 190, 7.5, 40), T("Rz 49E1-140-1:7", "Rz", 140, 7, 30),
            T("Rkpd 60E1-190-1:9", "Rkpd", 190, 9, 40), T("Rkp 60E1-190-1:9", "Rkp", 190, 9, 40),
            T("Rkpd 49E1-190-1:9", "Rkpd", 190, 9, 40), T("Rkp 49E1-190-1:9", "Rkp", 190, 9, 40),
            T("St 60E1-1:9", "St", 0, 9, 0), T("St 49E1-1:9", "St", 0, 9, 0),
        };
    }
}

/// <summary>
/// Rozjazd zwyczajny na torze zasadniczym: początek rozjazdu (PR) w kilometrażu <see cref="S"/> toru <see cref="Track"/>,
/// <see cref="Dir"/> +1 – rozjazd „w stronę” rosnącego kilometrażu toru (od PR do KR), −1 – przeciwnie; <see cref="Side"/> +1 – odgałęzienie
/// w prawo (patrząc od PR do KR), −1 – w lewo. Odgałęzienie to tor <see cref="Branch"/>: jego dwa wierzchołki od strony rozjazdu
/// (PR i ŚR z promieniem R typu) ustawia rozjazd (<see cref="Rozjazdy.Sync"/>), dalsze rysuje projektant.
/// Łącznik: dwa rozjazdy ze wspólnym odgałęzieniem (<see cref="Partner"/>); drugi (<see cref="BranchAtEnd"/>) trzyma się przecięcia z torem.
/// </summary>
public class UtTurnout
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    /// <summary>Numer rozjazdu (zwrotnicy) – wpisuje projektant.</summary>
    public string Number { get; set; } = "";
    public string Type { get; set; }
    public string Track { get; set; }
    /// <summary>Kilometraż PR na torze zasadniczym [m w terenie od początku toru].</summary>
    public double S { get; set; }
    public int Dir { get; set; } = 1;
    public int Side { get; set; } = 1;
    /// <summary>Położenie zasadnicze: „+” – na wprost, „-” – na zwrot.</summary>
    public string Zas { get; set; } = "+";
    /// <summary>Zwrotnica nieuzależniona (ręczna, nie w zależnościach) – klin kreskowany (Ig-10).</summary>
    public bool Manual { get; set; }
    public string Branch { get; set; }
    /// <summary>Rozjazd ustawia koniec odgałęzienia (drugi rozjazd łącznika), nie początek.</summary>
    public bool BranchAtEnd { get; set; }
    /// <summary>Łącznik: drugi rozjazd.</summary>
    public string Partner { get; set; }
    /// <summary>
    /// Odgałęzienie pojedynczego rozjazdu: odległość trzeciego wierzchołka od ŚR wzdłuż skosu [m w terenie]. Wierzchołek leży zawsze
    /// na prostej skosu, więc kąt odgałęzienia jest kątem typu (1:N) – także po przesunięciu rozjazdu. 0 – do ustalenia (Sync).
    /// </summary>
    public double BranchLen { get; set; }
    /// <summary>Rozjazd pojedynczy (nie łącznik): ma wierzchołek na skosie.</summary>
    public bool Single => Partner == null && !BranchAtEnd;
}

/// <summary>
/// Skrzyżowanie torów (St) albo rozjazd krzyżowy (Rkp, Rkpd) na przecięciu torów <see cref="TrackA"/> i <see cref="TrackB"/>.
/// Rozjazd krzyżowy: łuki przejazdów (R typu, styczne do obu torów); Rkp – jeden (<see cref="Slip"/> ±1 wybiera stronę), Rkpd – dwa.
/// Końce (zwrotnice a, b, c, d) w odległości A od środka wzdłuż obu torów.
/// </summary>
public class UtCrossing
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Number { get; set; } = "";
    public string Type { get; set; }
    public string TrackA { get; set; }
    public string TrackB { get; set; }
    /// <summary>Kilometraż środka na obu torach [m w terenie].</summary>
    public double SA { get; set; }
    public double SB { get; set; }
    public int Slip { get; set; } = 1;
}

/// <summary>Położenie rozjazdu w układzie: PR, ŚR, KR na wprost i na zwrot, kierunki [rad].</summary>
public record TurnoutGeo(XY PR, XY SR, XY KRMain, XY KRDiv, double Az, double AzDiv, double K, RozjazdTyp Typ);

/// <summary>Geometria rozjazdu i skrzyżowania, synchronizacja odgałęzień z torem zasadniczym, sprawdzanie.</summary>
public static class Rozjazdy
{
    static UtTrack Tr(UtProject p, string id) => p.Tracks.FirstOrDefault(t => t.Id == id);

    public static TurnoutGeo Geometry(UtProject p, UtTurnout t) => Geometry(Tr(p, t.Track), t);

    public static TurnoutGeo Geometry(UtTrack main, UtTurnout t)
    {
        var typ = Katalog.Find(t.Type); if (main == null || typ == null) return null;
        var r = Trasa.Compute(main); if (r.Elements.Count == 0) return null;
        double k = r.K;
        var (pr, az0) = r.At(Math.Clamp(t.S, 0, r.Length));
        double az = t.Dir < 0 ? az0 + Math.PI : az0;
        var sr = pr + Element.Dir(az) * (typ.A * k);
        double azDiv = az + t.Side * typ.Alpha;
        return new TurnoutGeo(pr, sr, sr + Element.Dir(az) * (typ.B * k), sr + Element.Dir(azDiv) * (typ.B * k), az, azDiv, k, typ);
    }

    /// <summary>Uwagi o rozjeździe: poza torem, na łuku (rozjazdy łukowe – później), nachodzi na inny rozjazd.</summary>
    public static List<string> Check(UtProject p, UtTurnout t)
    {
        var res = new List<string>();
        var main = Tr(p, t.Track); var typ = Katalog.Find(t.Type);
        if (main == null) { res.Add("brak toru zasadniczego"); return res; }
        if (typ == null) { res.Add($"nieznany typ „{t.Type}” – brak w katalogu"); return res; }
        var r = Trasa.Compute(main);
        double s1 = t.S, s2 = t.S + t.Dir * (typ.A + typ.B);
        if (Math.Min(s1, s2) < -1e-6 || Math.Max(s1, s2) > r.Length + 1e-6) res.Add("rozjazd wychodzi poza koniec toru zasadniczego");
        double acc = 0;
        foreach (var e in r.Elements)
        {
            double a = acc / r.K, b = (acc + e.Len) / r.K; acc += e.Len;
            if (e.Kind != ElKind.Prosta && b > Math.Min(s1, s2) + 1e-6 && a < Math.Max(s1, s2) - 1e-6) { res.Add("rozjazd leży na łuku toru zasadniczego (rozjazdy łukowe – w kolejnym etapie)"); break; }
        }
        if (t.BranchAtEnd && p.Turnouts.FirstOrDefault(o => o.Id == t.Partner) is UtTurnout pm && Geometry(p, pm) is TurnoutGeo gpm && Geometry(main, t) is TurnoutGeo gt)
        {
            double dev = Math.Abs(Math.Abs(Math.IEEERemainder(gpm.AzDiv - gt.Az, Math.PI)) - typ.Alpha) * 180 / Math.PI;
            if (dev > 0.5) res.Add($"tory łącznika nie są równoległe – skos odchyla się o {dev:0.##}° od 1:{typ.N:0.#}");
        }
        if (t.Single && Tr(p, t.Branch) is UtTrack br && br.Vertices.Count > 3)
        {
            var rb = Trasa.Compute(br);
            if (rb.Tangent.TryGetValue(2, out var tv) && t.BranchLen - tv / rb.K < typ.B - 0.01)
                res.Add($"łuk za rozjazdem zaczyna się {typ.B - (t.BranchLen - tv / rb.K):0.##} m przed końcem rozjazdu (KR) – wydłuż skos albo zmniejsz R");
        }
        foreach (var o in p.Turnouts.Where(o => o.Id != t.Id && o.Track == t.Track && Katalog.Find(o.Type) is RozjazdTyp ot))
        {
            var ot = Katalog.Find(o.Type);
            double o1 = Math.Min(o.S, o.S + o.Dir * (ot.A + ot.B)), o2 = Math.Max(o.S, o.S + o.Dir * (ot.A + ot.B));
            if (o2 > Math.Min(s1, s2) + 0.01 && o1 < Math.Max(s1, s2) - 0.01) res.Add($"nachodzi na rozjazd {o.Number}");
        }
        return res;
    }

    /// <summary>
    /// Synchronizacja po każdej zmianie: rozjazdy bez toru – usunięte; drugi rozjazd łącznika – na przecięciu odgałęzienia z jego torem;
    /// wierzchołki odgałęzień od strony rozjazdu – PR i ŚR (promień R typu); skrzyżowania – na przecięciu torów.
    /// </summary>
    public static void Sync(UtProject p)
    {
        p.Turnouts.RemoveAll(t => Tr(p, t.Track) == null || Tr(p, t.Branch) == null);
        foreach (var t in p.Turnouts.Where(t => t.Partner != null && p.Turnouts.All(o => o.Id != t.Partner)).ToList()) { t.Partner = null; t.BranchAtEnd = false; }
        // najpierw rozjazdy ustawiające początek odgałęzienia, potem drugie rozjazdy łączników
        foreach (var t in p.Turnouts.OrderBy(t => t.BranchAtEnd ? 1 : 0))
        {
            if (t.BranchAtEnd && t.Partner != null) Relink(p, t);
            var g = Geometry(p, t); var br = Tr(p, t.Branch); if (g == null || br == null) continue;
            var v = br.Vertices;
            while (v.Count < 3) v.Add(new UtVertex { X = g.KRDiv.X, Y = g.KRDiv.Y });
            if (!t.BranchAtEnd) { Set(v[0], g.PR, 0); Set(v[1], g.SR, g.Typ.R); }
            else { Set(v[^1], g.PR, 0); Set(v[^2], g.SR, g.Typ.R); }
            if (t.Single)
            {
                // trzeci wierzchołek na prostej skosu: długość z modelu albo (stare pliki) rzut dotychczasowego punktu, nie mniej niż b
                if (t.BranchLen <= 0) t.BranchLen = Math.Max(g.Typ.B, Math.Round(((v[2].P - g.SR).X * Math.Cos(g.AzDiv) + (v[2].P - g.SR).Y * Math.Sin(g.AzDiv)) / g.K, 3));
                var q = g.SR + Element.Dir(g.AzDiv) * (t.BranchLen * g.K);
                v[2].X = q.X; v[2].Y = q.Y;
            }
        }
        p.Crossings.RemoveAll(c => Tr(p, c.TrackA) == null || Tr(p, c.TrackB) == null);
        p.Devices ??= new();
        p.Devices.RemoveAll(d => Tr(p, d.Track) == null);           // urządzenia usuniętego toru
        foreach (var c in p.Crossings)
        {
            var ra = Trasa.Compute(Tr(p, c.TrackA)); var rb = Trasa.Compute(Tr(p, c.TrackB));
            if (Intersect(ra, rb, ra.At(c.SA).P) is var (sa, sb, _)) { c.SA = sa; c.SB = sb; }
        }
    }

    static void Set(UtVertex v, XY p, double r) { v.X = p.X; v.Y = p.Y; v.R = r; v.L = 0; }

    /// <summary>Drugi rozjazd łącznika: ŚR na przecięciu promienia odgałęzienia pierwszego rozjazdu z osią jego toru.</summary>
    static void Relink(UtProject p, UtTurnout t)
    {
        var m = p.Turnouts.FirstOrDefault(o => o.Id == t.Partner); if (m == null) return;
        var gm = Geometry(p, m); var tr = Tr(p, t.Track); var typ = Katalog.Find(t.Type); if (gm == null || tr == null || typ == null) return;
        var r = Trasa.Compute(tr);
        if (RayHit(r, gm.SR, gm.AzDiv) is not var (sSr, _)) return;
        var (_, azB) = r.At(sSr);
        double azBack = gm.AzDiv + Math.PI;              // od ŚR2 w stronę ŚR1
        // kierunek rozjazdu 2 (od PR2 do KR2) przeciwny do kierunku rozjazdu 1 względem toru – po stronie zgodnej z odgałęzieniem
        double azT = azBack - t.Side * typ.Alpha;
        t.Dir = Math.Cos(azT - azB) >= 0 ? 1 : -1;
        t.S = sSr - t.Dir * typ.A;
    }

    /// <summary>Przecięcie półprostej z osią toru: kilometraż [m w terenie] i odległość od początku półprostej [m w układzie].</summary>
    public static (double S, double T)? RayHit(TrasaResult r, XY from, double az)
    {
        if (r.Elements.Count == 0) return null;
        var d = Element.Dir(az); double t = 0;
        var (s0, _) = r.Nearest(from, 1);
        // przecięcie z prostą styczną w najbliższym punkcie, dwa przybliżenia
        for (int i = 0; i < 4; i++)
        {
            var (q, azq) = r.At(s0); var e = Element.Dir(azq);
            double den = d.X * e.Y - d.Y * e.X; if (Math.Abs(den) < 1e-9) return null;
            var w = q - from; t = (w.X * e.Y - w.Y * e.X) / den;
            if (t < 0) return null;
            var hit = from + d * t; s0 = r.Nearest(hit, 0.2).S;
        }
        var (pp, _) = r.At(s0);
        return XY.Dist(pp, from + d * t) < 0.05 && s0 > 0 && s0 < r.Length ? (s0, t) : null;
    }

    /// <summary>Przecięcie osi dwóch torów najbliżej punktu: kilometraże i kąt [rad].</summary>
    public static (double SA, double SB, double Angle)? Intersect(TrasaResult a, TrasaResult b, XY near)
    {
        if (a.Elements.Count == 0 || b.Elements.Count == 0) return null;
        double sa = a.Nearest(near, 1).S, sb = b.Nearest(near, 1).S;
        for (int i = 0; i < 6; i++)
        {
            var (pa, aa) = a.At(sa); var (pb, ab) = b.At(sb);
            var da = Element.Dir(aa); var db = Element.Dir(ab);
            double den = da.X * db.Y - da.Y * db.X; if (Math.Abs(den) < 1e-6) return null;
            var w = pb - pa; double ta = (w.X * db.Y - w.Y * db.X) / den, tb = (w.X * da.Y - w.Y * da.X) / den;
            sa += ta / a.K; sb += tb / b.K;
        }
        if (sa < 0 || sa > a.Length || sb < 0 || sb > b.Length) return null;
        if (XY.Dist(a.At(sa).P, b.At(sb).P) > 0.01) return null;
        double ang = Math.Abs(Math.IEEERemainder(a.At(sa).Az - b.At(sb).Az, Math.PI));
        return (sa, sb, ang);
    }

    /// <summary>Największe odchylenie torów łącznika od równoległości (odrysowane ze zdjęcia nie są idealnie równoległe).</summary>
    public const double MaxSkewDev = 3 * Math.PI / 180;

    /// <summary>
    /// Dwa rozjazdy pojedyncze z samymi odgałęzieniami (do skosu) na różnych torach → łącznik: odgałęzienie rozjazdu
    /// <paramref name="aId"/> staje się torem łączącym, rozjazd <paramref name="bId"/> przesuwa się wzdłuż swojego toru tak,
    /// żeby jego ŚR leżał na skosie pierwszego, a jego odgałęzienie znika. Zwraca błąd (null – zrobione) i przesunięcie ŚR [m].
    /// </summary>
    public static string MakeCrossover(UtProject p, string aId, string bId, out double shift)
    {
        shift = 0;
        var a = p.Turnouts.FirstOrDefault(x => x.Id == aId); var b = p.Turnouts.FirstOrDefault(x => x.Id == bId);
        if (a == null || b == null || !a.Single || !b.Single) return "łącznik tworzą dwa rozjazdy pojedyncze";
        if (a.Track == b.Track) return "oba rozjazdy leżą na tym samym torze";
        if (a.Type != b.Type) return $"rozjazdy różnych typów ({a.Type}, {b.Type}) – ustaw ten sam typ";
        var ba = Tr(p, a.Branch); var bb = Tr(p, b.Branch);
        if (ba == null || bb == null || ba.Vertices.Count != 3 || bb.Vertices.Count != 3) return "odgałęzienie jest już przedłużone – łącznik powstaje tylko z odgałęzień do skosu";
        var ga = Geometry(p, a); var gb = Geometry(p, b); var rb = Trasa.Compute(Tr(p, b.Track));
        if (ga == null || gb == null) return "brak geometrii rozjazdu";
        if (RayHit(rb, ga.SR, ga.AzDiv) is not var (sSr, _)) return "skos rozjazdu nie trafia w tor drugiego rozjazdu";
        double dAz = Math.Abs(Math.IEEERemainder(rb.At(sSr).Az - ga.Az, Math.PI));
        if (dAz > MaxSkewDev) return $"tory nie są równoległe ({dAz * 180 / Math.PI:0.##}°)";
        var oldSr = gb.SR;
        b.Side = a.Side; b.Partner = a.Id; b.BranchAtEnd = true; a.Partner = b.Id;
        p.Tracks.RemoveAll(x => x.Id == b.Branch); b.Branch = a.Branch;
        var v = ba.Vertices;
        ba.Vertices = new() { v[0], v[1], new UtVertex { X = v[2].X, Y = v[2].Y }, new UtVertex { X = v[2].X, Y = v[2].Y } };
        Sync(p);
        if (Geometry(p, b) is TurnoutGeo nb) shift = XY.Dist(oldSr, nb.SR) / nb.K;
        return null;
    }

    /// <summary>Kolejny wolny numer (liczba większa od wszystkich numerów liczbowych).</summary>
    public static int NextNumber(UtProject p, int skip = 0)
    {
        int max = 0;
        foreach (var n in p.Turnouts.Select(t => t.Number).Concat(p.Crossings.Select(c => c.Number)))
            if (int.TryParse(new string(n.TakeWhile(char.IsDigit).ToArray()), out var v)) max = Math.Max(max, v);
        return max + 1 + skip;
    }

    public static bool NumberUsed(UtProject p, string n, string exceptId = null) =>
        n.Length > 0 && (p.Turnouts.Any(t => t.Number == n && t.Id != exceptId) || p.Crossings.Any(c => c.Number == n && c.Id != exceptId));

    /// <summary>
    /// Rozjazd zwyczajny z nowym odgałęzieniem; przed dodaniem do projektu. <paramref name="offset"/> = 0 – samo odgałęzienie PR – ŚR – KR;
    /// &gt; 0 – tor odgałęźny równoległy do zasadniczego w odstępie <paramref name="offset"/> [m]: skos rozjazdu, łuk zwrotny
    /// <paramref name="rz"/> i prosta do <paramref name="endAlong"/> [m od PR wzdłuż toru] (co najmniej 20 m za łukiem).
    /// </summary>
    public static (UtTurnout T, UtTrack Branch) NewSingle(UtProject p, string track, double s, int dir, int side, string type, string number, double offset = 0, double endAlong = 0, double rz = 300)
    {
        var t = new UtTurnout { Number = number, Type = type, Track = track, S = s, Dir = dir, Side = side };
        var g = Geometry(p, t);
        var br = new UtTrack { Vertices = new() { new UtVertex { X = g.PR.X, Y = g.PR.Y }, new UtVertex { X = g.SR.X, Y = g.SR.Y, R = g.Typ.R } } };
        if (offset <= 0) { t.BranchLen = g.Typ.B; br.Vertices.Add(new UtVertex { X = g.KRDiv.X, Y = g.KRDiv.Y }); }
        else
        {
            offset = Math.Max(offset, MinOffset(g.Typ, rz));
            double a = g.Typ.Alpha, len = offset / Math.Sin(a);
            var v = g.SR + Element.Dir(g.AzDiv) * (len * g.K);
            double tv = rz * Math.Tan(a / 2), alongV = g.Typ.A + len * Math.Cos(a);
            var e = v + Element.Dir(g.Az) * (Math.Max(endAlong - alongV, tv + 20) * g.K);
            t.BranchLen = len;
            br.Vertices.Add(new UtVertex { X = v.X, Y = v.Y, R = rz });
            br.Vertices.Add(new UtVertex { X = e.X, Y = e.Y });
        }
        t.Branch = br.Id;
        return (t, br);
    }

    /// <summary>Najmniejszy odstęp toru odgałęźnego [m]: prosta skosu mieści b (do KR) i styczną łuku zwrotnego R.</summary>
    public static double MinOffset(RozjazdTyp typ, double rz) => (typ.B + rz * Math.Tan(typ.Alpha / 2)) * Math.Sin(typ.Alpha);

    /// <summary>Odstęp toru odgałęźnego z odległości puszczenia myszy od toru [m]: poniżej 2 m – samo odgałęzienie (0), dalej co 0,25 m.</summary>
    public static double OffsetFor(double across, RozjazdTyp typ, double rz)
    {
        if (across < 2) return 0;
        double o = Math.Round(across * 4) / 4;
        return Math.Max(o, Math.Ceiling(MinOffset(typ, rz) * 4) / 4);
    }

    /// <summary>Rozjazd pojedynczy, którego odgałęzieniem jest tor (wierzchołek 2 leży na skosie), albo null.</summary>
    public static UtTurnout SkewOwner(UtProject p, string track) => p.Turnouts.FirstOrDefault(t => t.Branch == track && t.Single);

    /// <summary>Rzut punktu na prostą skosu rozjazdu: punkt i odległość od ŚR [m w terenie] (nie mniej niż b).</summary>
    public static (XY P, double Len)? OnSkew(UtProject p, UtTurnout t, XY w)
    {
        var g = Geometry(p, t); if (g == null) return null;
        var d = w - g.SR; double len = Math.Max(g.Typ.B, (d.X * Math.Cos(g.AzDiv) + d.Y * Math.Sin(g.AzDiv)) / g.K);
        return (g.SR + Element.Dir(g.AzDiv) * (len * g.K), len);
    }

    /// <summary>
    /// Łącznik z toru A na tor B (równoległe, do 0,5°): rozjazd 1 na A, odgałęzienie skosem rozjazdu do przecięcia z B, rozjazd 2 na B
    /// tego samego typu; tor łączący PR1 – ŚR1 – ŚR2 – PR2. Null – tory nie są równoległe albo skos nie trafia w tor B.
    /// </summary>
    public static (UtTurnout T1, UtTurnout T2, UtTrack Link, string Error) NewCrossover(UtProject p, string trackA, double s, int dir, int side, string trackB, string type, string n1, string n2)
    {
        var t1 = new UtTurnout { Number = n1, Type = type, Track = trackA, S = s, Dir = dir, Side = side };
        var g1 = Geometry(p, t1); var tb = Tr(p, trackB);
        if (g1 == null || tb == null) return (null, null, null, "brak toru");
        var rb = Trasa.Compute(tb);
        if (RayHit(rb, g1.SR, g1.AzDiv) is not var (sSr, _)) return (null, null, null, "skos rozjazdu nie trafia w drugi tor – przeciągnij w jego stronę");
        double dAz = Math.Abs(Math.IEEERemainder(rb.At(sSr).Az - g1.Az, Math.PI));
        if (dAz > MaxSkewDev) return (null, null, null, $"tory nie są równoległe ({dAz * 180 / Math.PI:0.##}°, dopuszczalne {MaxSkewDev * 180 / Math.PI:0}°) – połącz rozjazdem i torem");
        var link = new UtTrack();
        var t2 = new UtTurnout { Number = n2, Type = type, Track = trackB, Side = side, Branch = link.Id, BranchAtEnd = true, Partner = t1.Id };
        t1.Branch = link.Id; t1.Partner = t2.Id;
        link.Vertices = new() { new UtVertex { X = g1.PR.X, Y = g1.PR.Y }, new UtVertex { X = g1.SR.X, Y = g1.SR.Y, R = g1.Typ.R }, new UtVertex(), new UtVertex() };
        return (t1, t2, link, null);
    }

    /// <summary>
    /// Rozjazd krzyżowy / skrzyżowanie na jednym torze: tor przecinający pod kątem typu (1:N) przez punkt km <paramref name="s"/>,
    /// w stronę <paramref name="dir"/> toru i na stronę <paramref name="side"/>, z końcami w odległości a od środka (zwrotnice a–d);
    /// dalej tor przedłuża projektant. Przed dodaniem do projektu.
    /// </summary>
    public static (UtCrossing C, UtTrack Cross) NewCrossing(UtProject p, string track, double s, int dir, int side, string type, string number)
    {
        var tr = Tr(p, track); var typ = Katalog.Find(type); if (tr == null || typ == null) return (null, null);
        var r = Trasa.Compute(tr); if (r.Elements.Count == 0) return (null, null);
        var (pt, az0) = r.At(Math.Clamp(s, 0, r.Length));
        double az = (dir < 0 ? az0 + Math.PI : az0) + side * typ.Alpha, a = typ.A * r.K;
        var p1 = pt - Element.Dir(az) * a; var p2 = pt + Element.Dir(az) * a;
        var cross = new UtTrack { Vertices = new() { new UtVertex { X = p1.X, Y = p1.Y }, new UtVertex { X = p2.X, Y = p2.Y } } };
        var c = new UtCrossing { Number = number, Type = type, TrackA = track, TrackB = cross.Id, SA = s, SB = typ.A };
        return (c, cross);
    }

    /// <summary>Środek i końce skrzyżowania (a, b – na torze A, c, d – na torze B) oraz łuki przejazdów rozjazdu krzyżowego.</summary>
    public static (XY P, XY[] Ends, List<List<XY>> Slips, double Angle)? CrossGeo(UtProject p, UtCrossing c)
    {
        var ta = Tr(p, c.TrackA); var tb = Tr(p, c.TrackB); var typ = Katalog.Find(c.Type); if (ta == null || tb == null || typ == null) return null;
        var ra = Trasa.Compute(ta); var rb = Trasa.Compute(tb);
        var (pa, aa) = ra.At(c.SA); var (_, ab) = rb.At(c.SB);
        double k = ra.K;
        // kierunek toru B wybrany tak, żeby tworzył z A kąt ostry
        if (Math.Cos(ab - aa) < 0) ab += Math.PI;
        double A = typ.A * k;
        var ends = new[] { pa - Element.Dir(aa) * A, pa + Element.Dir(aa) * A, pa - Element.Dir(ab) * A, pa + Element.Dir(ab) * A };
        double ang = Math.Abs(Math.IEEERemainder(ab - aa, Math.PI));
        var slips = new List<List<XY>>();
        if (typ.Kind != RozjazdTyp.St && ang > 1e-4)
        {
            double R = typ.R * k, T = R * Math.Tan(ang / 2);
            // przejazd z A (przed środkiem) na B (za środkiem) i odwrotnie
            List<XY> Arc(XY from, double az, double turn)
            {
                var pts = new List<XY>(); var cen = from + Element.Right(az) * (Math.Sign(turn) * R);
                for (int i = 0; i <= 24; i++) { double a = az + turn * i / 24; pts.Add(cen - Element.Right(a) * (Math.Sign(turn) * R)); }
                return pts;
            }
            double turn = Math.IEEERemainder(ab - aa, 2 * Math.PI);
            if (typ.Kind == RozjazdTyp.Rkpd || c.Slip > 0) slips.Add(Arc(pa - Element.Dir(aa) * T, aa, turn));
            if (typ.Kind == RozjazdTyp.Rkpd || c.Slip < 0) slips.Add(Arc(pa - Element.Dir(ab) * T, ab, -turn));
        }
        return (pa, ends, slips, ang);
    }
}

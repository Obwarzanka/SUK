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

/// <summary>
/// Położenie rozjazdu w układzie: PR, ŚR, KR na wprost i na zwrot, kierunki [rad] (<see cref="AzDiv"/> – kierunek skosu za ŚR).
/// Rozjazd na łuku toru zasadniczego to rozjazd łukowy (wygięty rozjazd zwyczajny): <see cref="RMain"/> – promień toru zasadniczego
/// [m, 0 – prosty], <see cref="RDiv"/> – promień toru zwrotnego [m, 0 – prosty], <see cref="Kind"/> – Rz, Rłj (oba tory wygięte w tę samą
/// stronę) albo Rłd (w przeciwne). <see cref="Q"/> – skala długości stycznych względem rozjazdu prostego (A·Q, B·Q).
/// </summary>
public record TurnoutGeo(XY PR, XY SR, XY KRMain, XY KRDiv, double Az, double AzDiv, double K, RozjazdTyp Typ,
    double RMain = 0, double RDiv = 0, string Kind = "Rz", double Q = 1)
{
    /// <summary>Odległość ŚR – KR na zwrot [m w terenie].</summary>
    public double B => Typ.B * Q;
    /// <summary>Prędkość na zwrot ze wzoru v = 3,6·√(0,65·R) (Zrodla/rozjazdy_1.pdf), zaokrąglona w dół do 10 km/h.</summary>
    public int VDiv => RDiv <= 0 ? 0 : (int)(Math.Floor(3.6 * Math.Sqrt(0.65 * RDiv) / 10) * 10);
}

/// <summary>
/// Wstawka międzyrozjazdowa: odcinek między stykami (KR / PR) dwóch rozjazdów. <see cref="Len"/> [m] (ujemna – rozjazdy nachodzą
/// na siebie), <see cref="Min"/> – najmniejsza dopuszczalna [m] (0 – przepisy nie wymagają), <see cref="Psi"/> – przyrost przyspieszenia
/// niezrównoważonego [m/s³] (0 – nie liczony), <see cref="Other"/> – numer drugiego rozjazdu, <see cref="Crossover"/> – wstawka łącznika.
/// </summary>
public record Wstawka(string Other, double Len, double Min, double Psi, bool Crossover, double REq = 0, double DevDeg = 0)
{
    /// <summary>Dłuższa niż 30 m – osobny tor z numerem, nie wstawka (Kampczyk, Skoczylas, Przegląd Komunikacyjny 4/2017).</summary>
    public bool Tor => Len > 30;
}

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
        // tor zasadniczy: punkt KR i kierunek na końcu rozjazdu – krzywizna toru na długości rozjazdu (łuk → rozjazd łukowy)
        double lm = typ.A + typ.B;
        var (krm, azE0) = r.At(Math.Clamp(t.S + t.Dir * lm, 0, r.Length));
        double dMain = Math.IEEERemainder((t.Dir < 0 ? azE0 + Math.PI : azE0) - az, 2 * Math.PI);
        double kg = Math.Abs(dMain) < 1e-7 ? 0 : dMain / lm;            // krzywizna toru zasadniczego (+ w prawo) [1/m]
        if (kg == 0 || typ.R <= 0)
        {
            var sr0 = pr + Element.Dir(az) * (typ.A * k);
            double azD = az + t.Side * typ.Alpha;
            return new TurnoutGeo(pr, sr0, sr0 + Element.Dir(az) * (typ.B * k), sr0 + Element.Dir(azD) * (typ.B * k), az, azD, k, typ, 0, typ.R);
        }
        // łukowanie: krzywizna toru zwrotnego = krzywizna toru zasadniczego + 1/R typu (po stronie odgałęzienia); na długości łuku
        // zwrotnego R·α różnica kierunków torów jest kątem rozjazdu α (skos 1:N) – jak w rozjeździe prostym
        double k1 = kg + t.Side / typ.R, lb = typ.R * typ.Alpha, dDiv = k1 * lb;
        double rDiv = Math.Abs(k1) < 1e-9 ? 0 : 1 / Math.Abs(k1);
        double tan = rDiv == 0 ? lb / 2 : rDiv * Math.Tan(Math.Abs(dDiv) / 2);
        double q = tan / (typ.R * Math.Tan(typ.Alpha / 2));
        // ŚR nie bliżej PR niż styczna łuku zwrotnego (a = R·tg(α/2) z katalogu zaokrąglone do mm); promień bez zaokrąglenia – inaczej
        // styczna wierzchołka odgałęzienia wychodzi o kilka mm dłuższa od odcinka PR – ŚR i tor ma błąd „łuk nie mieści się”
        var sr = pr + Element.Dir(az) * (Math.Max(typ.A * q, tan) * k);
        double azDiv = az + dDiv;
        string kind = rDiv == 0 ? "Rłd" : Math.Sign(k1) == Math.Sign(kg) ? "Rłj" : "Rłd";
        return new TurnoutGeo(pr, sr, krm, sr + Element.Dir(azDiv) * (Math.Max(typ.B * q, tan) * k), az, azDiv, k, typ, 1 / Math.Abs(kg), rDiv, kind, q);
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
        // rozjazd łukowy leży na stałym promieniu: nie na krzywej przejściowej ani na granicy prostej i łuku
        var pol = Naprawa.Polozenie(r, s1, s2);
        if (pol == NaLuku.Przejsciowa) res.Add("rozjazd leży na krzywej przejściowej – rozjazd łukowy układa się na stałym promieniu, przesuń go albo skróć przejściową (panel rozjazdu)");
        else if (pol == NaLuku.Granica)
        {
            // granice elementów pod rozjazdem: przesunięcie w stronę malejącego km (koniec przed pierwszą granicą) i rosnącego
            double lo = Math.Min(s1, s2), hi = Math.Max(s1, s2), acc2 = 0, bMin = double.MaxValue, bMax = double.MinValue;
            foreach (var e in r.Elements) { acc2 += e.Len; double b = acc2 / r.K; if (b > lo + 0.01 && b < hi - 0.01) { bMin = Math.Min(bMin, b); bMax = Math.Max(bMax, b); } }
            res.Add(bMin < double.MaxValue
                ? $"rozjazd leży na granicy prostej i łuku – przesuń go o {hi - bMin:0.##} m w stronę malejącego km albo o {bMax - lo:0.##} m w stronę rosnącego (w całości na prostą albo na łuk)"
                : "rozjazd leży na granicy prostej i łuku – przesuń go w całości na prostą albo na łuk");
        }
        if (Geometry(main, t) is TurnoutGeo gl && gl.Kind != "Rz" && gl.RDiv > 0 && gl.RDiv < MinRDiv)
            res.Add($"promień toru zwrotnego rozjazdu łukowego {gl.RDiv:0} m mniejszy od najmniejszego {MinRDiv:0} m (warunki techniczne § 24)");
        if (t.BranchAtEnd && p.Turnouts.FirstOrDefault(o => o.Id == t.Partner) is UtTurnout pm && Geometry(p, pm) is TurnoutGeo gpm && Geometry(main, t) is TurnoutGeo gt
            && Equalizer(gpm, gt) is var (_, dev, rx))
        {
            if (dev > MaxSkewDev) res.Add($"skosy łącznika rozchodzą się o {dev * 180 / Math.PI:0.##}° (więcej niż {MaxSkewDev * 180 / Math.PI:0}°) – tory za mało równoległe na łącznik");
            else if (rx < MinRDiv) res.Add($"brak miejsca na łuk wyrównujący we wstawce (skosy rozchodzą się o {dev * 180 / Math.PI:0.##}°, mieści się R {rx:0} m < {MinRDiv:0} m) – rozsuń tory albo przesuń łącznik na prostszy odcinek");
        }
        if (t.Partner != null && !t.BranchAtEnd)
            foreach (var w in Wstawki(p, t).Where(w => w.Crossover))
            {
                if (w.Len < -0.01) res.Add($"rozjazdy łącznika {t.Number}/{w.Other} nachodzą na siebie o {-w.Len:0.##} m – brak wstawki; zwiększ rozstaw torów albo weź rozjazd o mniejszym skosie");
                else if (w.Len < w.Min - 0.01) res.Add($"wstawka międzyrozjazdowa {w.Len:0.##} m krótsza od najmniejszej {w.Min:0.#} m (v/10, nie mniej niż 6 m – warunki techniczne § 24)");
                if (w.Psi > 1 + 1e-9) res.Add($"przyrost przyspieszenia ψ = {w.Psi:0.##} m/s³ w łączniku większy od 1 m/s³ (§ 24, § 35)");
            }
        if (t.Single && Tr(p, t.Branch) is UtTrack br && br.Vertices.Count > 3)
        {
            var rb = Trasa.Compute(br);
            double bq = Geometry(main, t)?.B ?? typ.B;
            if (rb.Tangent.TryGetValue(2, out var tv) && t.BranchLen - tv / rb.K < bq - 0.01)
                res.Add($"łuk za rozjazdem zaczyna się {bq - (t.BranchLen - tv / rb.K):0.##} m przed końcem rozjazdu (KR) – wydłuż skos albo zmniejsz R");
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
    /// <summary>
    /// Wstawki międzyrozjazdowe rozjazdu: w łączniku – między KR obu rozjazdów na torze łączącym (krzyżownice łukowe końcami do siebie:
    /// najmniejsza l = v/10, nie mniej niż 6 m – warunki techniczne § 24 ust. 2 pkt 4; ψ = V·(a₁ + a₂) / (3,6·(b + w)), b = 20 m,
    /// a = V² / (12,96·R), dopuszczalne 1 m/s³ – § 35 ust. 7, § 24 ust. 2 pkt 5); na torze zasadniczym – do najbliższego rozjazdu
    /// przed i za (do 30 m, bez wymagań).
    /// </summary>
    public static List<Wstawka> Wstawki(UtProject p, UtTurnout t)
    {
        var res = new List<Wstawka>();
        var typ = Katalog.Find(t.Type); var g = Geometry(p, t); if (typ == null || g == null) return res;
        if (t.Partner != null && p.Turnouts.FirstOrDefault(o => o.Id == t.Partner) is UtTurnout o && Geometry(p, o) is TurnoutGeo go)
        {
            var eq = t.BranchAtEnd ? Equalizer(go, g) : Equalizer(g, go);
            double w = (eq is var (ex, _, _) ? XY.Dist(g.SR, ex) + XY.Dist(ex, go.SR) : XY.Dist(g.SR, go.SR)) / g.K - g.B - go.B;
            double v = Math.Min(typ.Vbok > 0 ? typ.Vbok : 40, Math.Min(g.VDiv > 0 ? g.VDiv : 999, go.VDiv > 0 ? go.VDiv : 999));
            double a1 = g.RDiv > 0 ? v * v / (12.96 * g.RDiv) : 0, a2 = go.RDiv > 0 ? v * v / (12.96 * go.RDiv) : 0;
            double psi = v * (a1 + a2) / (3.6 * (20 + Math.Max(0, w)));
            res.Add(new Wstawka(o.Number, w, Math.Max(6, v / 10), psi, true, eq?.R ?? 0, (eq?.Dev ?? 0) * 180 / Math.PI));
        }
        // na torze zasadniczym: najbliższy rozjazd przed PR i za KR
        double s1 = Math.Min(t.S, t.S + t.Dir * (typ.A + typ.B)), s2 = Math.Max(t.S, t.S + t.Dir * (typ.A + typ.B));
        Wstawka best1 = null, best2 = null;
        foreach (var n in p.Turnouts.Where(n => n.Id != t.Id && n.Track == t.Track))
        {
            var ot = Katalog.Find(n.Type); if (ot == null) continue;
            double o1 = Math.Min(n.S, n.S + n.Dir * (ot.A + ot.B)), o2 = Math.Max(n.S, n.S + n.Dir * (ot.A + ot.B));
            if (o1 >= s2 - 0.01 && o1 - s2 <= 30 && (best2 == null || o1 - s2 < best2.Len)) best2 = new Wstawka(n.Number, o1 - s2, 0, 0, false);
            if (o2 <= s1 + 0.01 && s1 - o2 <= 30 && (best1 == null || s1 - o2 < best1.Len)) best1 = new Wstawka(n.Number, s1 - o2, 0, 0, false);
        }
        if (best1 != null) res.Add(best1);
        if (best2 != null) res.Add(best2);
        return res;
    }

    /// <summary>Wierzchołek toru ustawiany przez rozjazd: PR i ŚR odgałęzienia (drugi rozjazd łącznika – ostatnie dwa), tor łączący łącznika – cały.</summary>
    public static bool Locked(UtProject p, UtTrack t, int i) =>
        p.Turnouts.Any(r => r.Branch == t.Id && (r.Partner != null || (!r.BranchAtEnd ? i <= 1 : i >= t.Vertices.Count - 2)));

    /// <summary>Najmniejszy promień toru zwrotnego [m] – rozporządzenie w sprawie warunków technicznych (Dz.U. 2014 poz. 867) § 24 ust. 2 pkt 2.</summary>
    public const double MinRDiv = 140;

    public static void Sync(UtProject p)
    {
        foreach (var tr in p.Tracks.Where(x => x.Align != null)) TrasaElem.SyncEnds(tr);
        p.Turnouts.RemoveAll(t => Tr(p, t.Track) == null || Tr(p, t.Branch) == null);
        foreach (var t in p.Turnouts.Where(t => t.Partner != null && p.Turnouts.All(o => o.Id != t.Partner)).ToList()) { t.Partner = null; t.BranchAtEnd = false; }
        SyncTurnouts(p, p.Turnouts);
        p.Crossings.RemoveAll(c => Tr(p, c.TrackA) == null || Tr(p, c.TrackB) == null);
        p.Devices ??= new();
        p.Devices.RemoveAll(d => Tr(p, d.Track) == null);           // urządzenia usuniętego toru
        p.Sections ??= new(); p.Platforms ??= new(); p.LevelCrossings ??= new(); p.KmPoints ??= new();
        p.Sections.RemoveAll(d => Tr(p, d.Track) == null);
        p.Platforms.RemoveAll(d => Tr(p, d.Track) == null);
        foreach (var pl in p.Platforms.Where(x => x.Track2 != null && Tr(p, x.Track2) == null)) pl.Track2 = null;
        p.LevelCrossings.RemoveAll(d => Tr(p, d.Track) == null);
        p.KmPoints.RemoveAll(d => Tr(p, d.Track) == null);
        foreach (var c in p.Crossings)
        {
            var ra = Trasa.Compute(Tr(p, c.TrackA)); var rb = Trasa.Compute(Tr(p, c.TrackB));
            if (Intersect(ra, rb, ra.At(c.SA).P) is var (sa, sb, _)) { c.SA = sa; c.SB = sb; }
        }
    }

    /// <summary>Część <see cref="Sync"/> dla wybranych rozjazdów (np. pary łącznika przy sprawdzaniu wariantu): drugi rozjazd łącznika
    /// na przecięciu, wierzchołki odgałęzień od strony rozjazdu, tor łączący.</summary>
    public static void SyncTurnouts(UtProject p, IEnumerable<UtTurnout> list)
    {
        var sel = list.ToList();
        // najpierw rozjazdy ustawiające początek odgałęzienia, potem drugie rozjazdy łączników
        foreach (var t in sel.OrderBy(t => t.BranchAtEnd ? 1 : 0))
        {
            if (t.BranchAtEnd && t.Partner != null) Relink(p, t);
            var g = Geometry(p, t); var br = Tr(p, t.Branch); if (g == null || br == null) continue;
            var v = br.Vertices;
            while (v.Count < 3) v.Add(new UtVertex { X = g.KRDiv.X, Y = g.KRDiv.Y });
            if (!t.BranchAtEnd) { Set(v[0], g.PR, 0); Set(v[1], g.SR, g.RDiv); }
            else { Set(v[^1], g.PR, 0); Set(v[^2], g.SR, g.RDiv); }
            if (t.Single)
            {
                // trzeci wierzchołek na prostej skosu: długość z modelu albo (stare pliki) rzut dotychczasowego punktu, nie mniej niż b
                if (t.BranchLen <= 0) t.BranchLen = Math.Max(g.B, Math.Round(((v[2].P - g.SR).X * Math.Cos(g.AzDiv) + (v[2].P - g.SR).Y * Math.Sin(g.AzDiv)) / g.K, 3));
                t.BranchLen = Math.Max(t.BranchLen, g.B);
                var q = g.SR + Element.Dir(g.AzDiv) * (t.BranchLen * g.K);
                v[2].X = q.X; v[2].Y = q.Y;
            }
        }
        // tor łączący łącznika: PR1 – ŚR1 – (X z łukiem wyrównującym) – ŚR2 – PR2
        foreach (var t in sel.Where(t => t.Partner != null && !t.BranchAtEnd))
        {
            var o = p.Turnouts.FirstOrDefault(x => x.Id == t.Partner); var link = Tr(p, t.Branch);
            if (o == null || link == null || o.Branch != t.Branch || Geometry(p, t) is not TurnoutGeo g1 || Geometry(p, o) is not TurnoutGeo g2) continue;
            var nv = new List<UtVertex> { V(g1.PR, 0), V(g1.SR, g1.RDiv) };
            if (Equalizer(g1, g2) is var (x, _, rx)) nv.Add(V(x, rx));
            nv.Add(V(g2.SR, g2.RDiv)); nv.Add(V(g2.PR, 0));
            link.Vertices = nv;
        }
    }

    static void Set(UtVertex v, XY p, double r) { v.X = p.X; v.Y = p.Y; v.R = r; v.L = 0; }
    static UtVertex V(XY p, double r) => new() { X = p.X, Y = p.Y, R = r };

    /// <summary>
    /// Drugi rozjazd łącznika. Tory proste i równoległe: ŚR2 na prostej skosu rozjazdu 1. Tory w łuku (albo nie całkiem równoległe)
    /// obracają się na długości łącznika, więc skosy obu rozjazdów rozchodzą się o kąt δ – wtedy we wstawce jest łuk wyrównujący
    /// w przecięciu skosów (Kampczyk, Skoczylas 2017: „łuk kołowy wyrównujący w torach nierównoległych”), a rozjazd 2 stoi tak,
    /// żeby łuk był w środku wstawki (cięciwa ŚR1 – ŚR2 w kierunku średnim obu skosów).
    /// </summary>
    /// <summary>Km PR i kierunek drugiego rozjazdu łącznika, gdyby jego tor miał wielobok <paramref name="vs"/> (projekt bez zmian) – jak w <see cref="Sync"/>.</summary>
    public static (double S, int Dir) PairPos(UtProject p, UtTurnout t, List<UtVertex> vs)
    {
        var tr = Tr(p, t.Track); double s = t.S; int d = t.Dir;
        if (tr == null || !t.BranchAtEnd || t.Partner == null) return (s, d);
        var old = tr.Vertices;
        try { tr.Vertices = vs; Relink(p, t); return (t.S, t.Dir); }
        finally { tr.Vertices = old; t.S = s; t.Dir = d; }
    }

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
        double s0 = sSr - t.Dir * typ.A;
        // f(S) – kąt między cięciwą ŚR1 → ŚR2 a średnim kierunkiem skosów; 0 – łuk wyrównujący w środku (tory równoległe: ŚR2 na skosie 1)
        double F(double s)
        {
            t.S = s; var g = Geometry(tr, t); if (g == null) return double.NaN;
            double back = Math.IEEERemainder(g.AzDiv + Math.PI - gm.AzDiv, 2 * Math.PI);
            return Math.IEEERemainder(Trasa.Az(gm.SR, g.SR) - (gm.AzDiv + back / 2), 2 * Math.PI);
        }
        double a = s0, fa = F(a), b = s0 + 0.5, fb = F(b);
        for (int i = 0; i < 30 && double.IsFinite(fa) && double.IsFinite(fb) && Math.Abs(fb) > 1e-10 && Math.Abs(fb - fa) > 1e-15; i++)
        {
            double c = b - fb * (b - a) / (fb - fa);
            if (!double.IsFinite(c) || Math.Abs(c - s0) > 60) break;
            a = b; fa = fb; b = c; fb = F(b);
        }
        t.S = double.IsFinite(fb) && Math.Abs(fb) < 1e-6 ? b : s0;
    }

    /// <summary>Łuk wyrównujący łącznika: wierzchołek X (przecięcie skosów), kąt δ [rad], promień [m] (największy, przy którym KR obu
    /// rozjazdów zostają na prostej, co 10 m, najwyżej 50 000); null – skosy na jednej prostej (δ ≈ 0).</summary>
    public static (XY X, double Dev, double R)? Equalizer(TurnoutGeo g1, TurnoutGeo g2)
    {
        double back = Math.IEEERemainder(g2.AzDiv + Math.PI - g1.AzDiv, 2 * Math.PI);
        if (Math.Abs(back) < 1e-6) return null;
        var d1 = Element.Dir(g1.AzDiv); var d2 = Element.Dir(g2.AzDiv);
        double den = d1.X * d2.Y - d1.Y * d2.X; if (Math.Abs(den) < 1e-12) return null;
        var w = g2.SR - g1.SR; double u = (w.X * d2.Y - w.Y * d2.X) / den;
        if (u <= 0) return null;
        var x = g1.SR + d1 * u;
        double room = Math.Min(u / g1.K - g1.B, XY.Dist(x, g2.SR) / g2.K - g2.B), tan = Math.Tan(Math.Abs(back) / 2);
        double rr = room <= 0 ? 0 : Math.Min(50000, Math.Floor(room / tan / 10) * 10);
        return (x, Math.Abs(back), rr);
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
    public static string MakeCrossover(UtProject p, string aId, string bId, out double shift) => MakeCrossover(p, aId, bId, out shift, false);

    /// <param name="joined">Odgałęzienia mogą być już przedłużone, jeśli ich końce się stykają (połączone ręcznie narzędziem Tor) –
    /// przedłużenia znikają, powstaje zwykły łącznik.</param>
    public static string MakeCrossover(UtProject p, string aId, string bId, out double shift, bool joined)
    {
        shift = 0;
        var a = p.Turnouts.FirstOrDefault(x => x.Id == aId); var b = p.Turnouts.FirstOrDefault(x => x.Id == bId);
        if (a == null || b == null || !a.Single || !b.Single) return "łącznik tworzą dwa rozjazdy pojedyncze";
        if (a.Track == b.Track) return "oba rozjazdy leżą na tym samym torze";
        if (a.Type != b.Type) return $"rozjazdy różnych typów ({a.Type}, {b.Type}) – ustaw ten sam typ";
        var ba = Tr(p, a.Branch); var bb = Tr(p, b.Branch);
        if (ba == null || bb == null) return "brak odgałęzienia";
        if (ba.Vertices.Count != 3 || bb.Vertices.Count != 3)
        {
            if (!joined) return "odgałęzienie jest już przedłużone – łącznik powstaje tylko z odgałęzień do skosu";
            // przedłużone odgałęzienia: tylko gdy łączą się ze sobą (końce się stykają) – inaczej zniknąłby np. tor bocznicy
            if (XY.Dist(ba.Vertices[^1].P, bb.Vertices[^1].P) > 1) return "odgałęzienia nie są ze sobą połączone (prowadzą dalej) – łącznik usunąłby ten tor";
            ba.Vertices = ba.Vertices.Take(3).Select(v => v.Clone()).ToList(); ba.Vertices[2].R = 0; ba.Vertices[2].L = 0;
            bb.Vertices = bb.Vertices.Take(3).Select(v => v.Clone()).ToList(); bb.Vertices[2].R = 0; bb.Vertices[2].L = 0;
        }
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

    /// <summary>
    /// Rozjazdy, z którymi rozjazd <paramref name="aId"/> może utworzyć łącznik (pojedyncze, ten sam typ, inny tor, skos trafia w ich tor,
    /// przesunięcie do 50 m): rozjazd i przesunięcie [m]. Sprawdzane na kopii projektu.
    /// </summary>
    public static List<(UtTurnout B, double Shift)> CrossoverCandidates(UtProject p, string aId)
    {
        var res = new List<(UtTurnout, double)>();
        var a = p.Turnouts.FirstOrDefault(x => x.Id == aId); if (a == null || !a.Single) return res;
        string json = UtDocument.Serialize(p);
        foreach (var b in p.Turnouts.Where(x => x.Id != aId && x.Single && x.Type == a.Type && x.Track != a.Track))
        {
            var tmp = UtDocument.Deserialize(json);
            if (MakeCrossover(tmp, aId, b.Id, out var sh, true) == null && sh <= 50) res.Add((b, sh));
        }
        return res.OrderBy(x => x.Item2).ToList();
    }

    /// <summary>Kolejny wolny numer (liczba większa od wszystkich numerów liczbowych).</summary>
    public static int NextNumber(UtProject p, int skip = 0)
    {
        int max = 0;
        foreach (var n in p.Turnouts.Select(t => t.Number).Concat(p.Crossings.Select(c => c.Number)))
            if (int.TryParse(new string(n.TakeWhile(char.IsDigit).ToArray()), out var v)) max = Math.Max(max, v);
        return max + 1 + skip;
    }

    /// <summary>
    /// Numer zajęty przez inny rozjazd tego samego posterunku (numery powtarzają się między posterunkami). Bez <paramref name="exceptId"/>
    /// (nowy rozjazd) – w posterunku toru <paramref name="track"/>, a bez toru – w całym obszarze.
    /// </summary>
    public static bool NumberUsed(UtProject p, string n, string exceptId = null, string track = null)
    {
        if (n.Length == 0) return false;
        track ??= p.Turnouts.FirstOrDefault(t => t.Id == exceptId)?.Track ?? p.Crossings.FirstOrDefault(c => c.Id == exceptId)?.TrackA;
        string g = track != null ? Posterunki.Grupa(p, track) : null;
        bool Same(string tr) => g == null || Posterunki.Grupa(p, tr) == g;
        return p.Turnouts.Any(t => t.Number == n && t.Id != exceptId && Same(t.Track)) || p.Crossings.Any(c => c.Number == n && c.Id != exceptId && Same(c.TrackA));
    }

    /// <summary>Plan zmiany numerów: nowe numery (id → numer, też zamienione), opis zamian dla projektanta, błąd (wtedy nic się nie zmienia).</summary>
    public record Przenumerowanie(List<(string Id, string Number)> Zmiany, List<string> Zamiany, string Blad);

    /// <summary>
    /// Nowe numery rozjazdów / skrzyżowań <paramref name="ids"/> (np. oba rozjazdy łącznika). Numer zajęty w posterunku przez inny
    /// rozjazd albo skrzyżowanie – zamiana: tamten dostaje numer zwolniony przez zmieniany (najpierw jego dawny), więc przenumerowanie
    /// stacji nie wymaga numerów tymczasowych. Numer nie może być pusty (poza skrzyżowaniem zwyczajnym) ani powtórzony w <paramref name="nums"/>.
    /// </summary>
    public static Przenumerowanie PlanNumbers(UtProject p, IList<string> ids, IList<string> nums)
    {
        Przenumerowanie Err(string e) => new(new(), new(), e);
        string Track(string id) => p.Turnouts.FirstOrDefault(t => t.Id == id)?.Track ?? p.Crossings.FirstOrDefault(c => c.Id == id)?.TrackA;
        string Old(string id) => p.Turnouts.FirstOrDefault(t => t.Id == id)?.Number ?? p.Crossings.FirstOrDefault(c => c.Id == id)?.Number ?? "";
        bool Needs(string id) => p.Crossings.FirstOrDefault(c => c.Id == id) is not UtCrossing c || Katalog.Find(c.Type)?.Kind != RozjazdTyp.St;
        string Opis(string id) => p.Crossings.FirstOrDefault(c => c.Id == id) is UtCrossing c
            ? (Katalog.Find(c.Type)?.Kind == RozjazdTyp.St ? "skrzyżowanie" : "rozjazd krzyżowy") + (c.Number.Length > 0 ? " " + c.Number : "")
            : $"rozjazd {Old(id)}";
        nums = nums.Select(n => (n ?? "").Trim()).ToList();
        for (int i = 0; i < ids.Count; i++)
            if (nums[i].Length == 0 && Needs(ids[i])) return Err(ids.Count > 1 ? "Numery nie mogą być puste" : "Numer nie może być pusty");
        var filled = nums.Where(n => n.Length > 0).ToList();
        if (filled.Distinct().Count() != filled.Count) return Err("Numery muszą być różne");
        var olds = ids.Select(Old).ToList();
        var zm = ids.Select((id, i) => (id, nums[i])).ToList();
        var zam = new List<string>();
        var freed = olds.Where(o => o.Length > 0 && !nums.Contains(o)).ToList();
        var mine = ids.ToHashSet();
        var others = p.Turnouts.Select(t => (t.Id, t.Number, Track: t.Track)).Concat(p.Crossings.Select(c => (c.Id, c.Number, Track: c.TrackA))).Where(x => !mine.Contains(x.Id)).ToList();
        for (int i = 0; i < ids.Count; i++)
        {
            if (nums[i].Length == 0 || nums[i] == olds[i]) continue;
            string g = Posterunki.Grupa(p, Track(ids[i]));
            foreach (var o in others.Where(x => x.Number == nums[i] && Posterunki.Grupa(p, x.Track) == g))
            {
                string give = freed.Contains(olds[i]) ? olds[i] : freed.FirstOrDefault();
                if (give == null)
                {
                    if (Needs(o.Id)) return Err($"Numer {nums[i]} ma {Opis(o.Id)}, a nie zwalnia się żaden numer do zamiany");
                    give = "";
                }
                else freed.Remove(give);
                if (give.Length > 0 && others.Any(x => x.Id != o.Id && x.Number == give && Posterunki.Grupa(p, x.Track) == Posterunki.Grupa(p, o.Track)))
                    return Err($"Numer {nums[i]} ma {Opis(o.Id)}, a numer {give} do zamiany ma jeszcze inny rozjazd");
                zm.Add((o.Id, give));
                zam.Add($"{Opis(o.Id)} → {(give.Length > 0 ? give : "bez numeru")}");
            }
        }
        return new(zm, zam, null);
    }

    /// <summary>Zastosowanie planu z <see cref="PlanNumbers"/> (w jednej zmianie dokumentu).</summary>
    public static void ApplyNumbers(UtProject p, Przenumerowanie plan)
    {
        foreach (var (id, n) in plan.Zmiany)
        {
            if (p.Turnouts.FirstOrDefault(t => t.Id == id) is UtTurnout t) t.Number = n;
            else if (p.Crossings.FirstOrDefault(c => c.Id == id) is UtCrossing c) c.Number = n;
        }
    }

    /// <summary>
    /// Rozjazd zwyczajny z nowym odgałęzieniem; przed dodaniem do projektu. <paramref name="offset"/> = 0 – samo odgałęzienie PR – ŚR – KR;
    /// &gt; 0 – tor odgałęźny równoległy do zasadniczego w odstępie <paramref name="offset"/> [m]: skos rozjazdu, łuk zwrotny
    /// <paramref name="rz"/> i prosta do <paramref name="endAlong"/> [m od PR wzdłuż toru] (co najmniej 20 m za łukiem).
    /// </summary>
    public static (UtTurnout T, UtTrack Branch) NewSingle(UtProject p, string track, double s, int dir, int side, string type, string number, double offset = 0, double endAlong = 0, double rz = 300)
    {
        var t = new UtTurnout { Number = number, Type = type, Track = track, S = s, Dir = dir, Side = side };
        var g = Geometry(p, t);
        var br = new UtTrack { Vertices = new() { new UtVertex { X = g.PR.X, Y = g.PR.Y }, new UtVertex { X = g.SR.X, Y = g.SR.Y, R = g.RDiv } } };
        if (offset <= 0) { t.BranchLen = g.B; br.Vertices.Add(new UtVertex { X = g.KRDiv.X, Y = g.KRDiv.Y }); }
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
        var d = w - g.SR; double len = Math.Max(g.B, (d.X * Math.Cos(g.AzDiv) + d.Y * Math.Sin(g.AzDiv)) / g.K);
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
        link.Vertices = new() { new UtVertex { X = g1.PR.X, Y = g1.PR.Y }, new UtVertex { X = g1.SR.X, Y = g1.SR.Y, R = g1.RDiv }, new UtVertex(), new UtVertex() };
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

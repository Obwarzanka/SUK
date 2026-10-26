namespace Lcs.Model;

/// <summary>
/// Operacje na grafie rysunku obrazu w wersji 2 (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 3 wariant A, § 5, § 8) – bez WPF, do testów bez okna.
/// Odcinek jest zawsze prosty (od węzła do węzła); załamanie to węzeł z dwoma odcinkami, rozjazd – węzeł z trzema (powstaje sam,
/// gdy tor zaczyna się albo kończy na innym torze, decyzja właściciela 2026-10-22), skrzyżowanie / rozjazd krzyżowy – węzeł z czterema
/// i elementem <see cref="Skrzyzowanie"/> / <see cref="RozjazdKrzyzowy"/>. Rysunek nie zależy od UT – nic nie jest odrzucane;
/// odstępstwa (odcinek nie co 45°, węzeł o nieznanym kształcie) to uwagi (<see cref="Uwagi"/>).
/// </summary>
public static class ObrGraf
{
    public const string Skrzyzowanie = "skrzyzowanie", RozjazdKrzyzowy = "rozjazdKrzyzowy";

    public static ObrWezel Wezel(ObrObraz o, string id) => o.Wezly.FirstOrDefault(w => w.Id == id);
    public static ObrOdcinek Odcinek(ObrObraz o, string id) => o.Odcinki.FirstOrDefault(e => e.Id == id);
    public static List<ObrOdcinek> Przy(ObrObraz o, string wezel) => o.Odcinki.Where(e => e.A == wezel || e.B == wezel).ToList();
    public static string Drugi(ObrOdcinek e, string wezel) => e.A == wezel ? e.B : e.A;
    public static (int[] A, int[] B) Konce(ObrObraz o, ObrOdcinek e) => (Wezel(o, e.A).P, Wezel(o, e.B).P);
    static bool Rowne(int[] a, int[] b) => a[0] == b[0] && a[1] == b[1];

    /// <summary>Kierunek odcinka od węzła <paramref name="z"/>: (dx, dy) sprowadzone do −1 / 0 / +1 (dla kierunków co 45°).</summary>
    public static (int X, int Y) Kier(ObrObraz o, ObrOdcinek e, string z)
    {
        var a = Wezel(o, z).P; var b = Wezel(o, Drugi(e, z)).P;
        return (Math.Sign(b[0] - a[0]), Math.Sign(b[1] - a[1]));
    }

    /// <summary>Odcinek co 45° (poziomy, pionowy albo ukośny 1:1).</summary>
    public static bool Osiowy(int[] a, int[] b)
    {
        int dx = Math.Abs(b[0] - a[0]), dy = Math.Abs(b[1] - a[1]);
        return dx == 0 || dy == 0 || dx == dy;
    }
    public static bool Poziomy(ObrObraz o, ObrOdcinek e) { var (a, b) = Konce(o, e); return a[1] == b[1]; }
    public static bool Pionowy(ObrObraz o, ObrOdcinek e) { var (a, b) = Konce(o, e); return a[0] == b[0]; }
    public static bool Ukosny(ObrObraz o, ObrOdcinek e) { var (a, b) = Konce(o, e); return a[0] != b[0] && Math.Abs(b[0] - a[0]) == Math.Abs(b[1] - a[1]); }

    /// <summary>
    /// Następny punkt pióra od <paramref name="prev"/> (plan § 5): kierunek co 45° najbliższy myszy, długość do siatki; koniec ukosu albo pionu
    /// przykleja się do prowadnicy rzędu bliższej niż <paramref name="tol"/> j. (rzędy torów § 2).
    /// </summary>
    public static int[] Kierunek(int[] prev, double x, double y, int g, IList<int> rzedy = null, double tol = 0)
    {
        double dx = x - prev[0], dy = y - prev[1];
        if (Math.Abs(dx) < 1e-9 && Math.Abs(dy) < 1e-9) return new[] { prev[0], prev[1] };
        double ang = Math.Round(Math.Atan2(dy, dx) / (Math.PI / 4));
        int ux = (int)Math.Round(Math.Cos(ang * Math.PI / 4)), uy = (int)Math.Round(Math.Sin(ang * Math.PI / 4));
        double len = ux != 0 && uy != 0 ? (dx * ux + dy * uy) / 2 : dx * ux + dy * uy;      // ukos: długość wzdłuż osi x (= wzdłuż y)
        int n = Math.Max(0, ZobTory.Grid(len, g));
        if (uy != 0 && rzedy != null)
        {
            // prowadnica najbliższa końcowi – koniec dokładnie na niej (gdy po drodze w tę stronę)
            int ey = prev[1] + uy * n;
            var r = rzedy.Where(ry => Math.Abs(ry - ey) <= tol && Math.Sign(ry - prev[1]) == uy).OrderBy(ry => Math.Abs(ry - ey)).Cast<int?>().FirstOrDefault();
            if (r is int ry2) n = Math.Abs(ry2 - prev[1]);
        }
        return new[] { prev[0] + ux * n, prev[1] + uy * n };
    }

    /// <summary>Rzut punktu na odcinek: odległość, część długości od A (0–1), punkt rzutu.</summary>
    public static (double Dist, double T, double X, double Y) Rzut(int[] a, int[] b, double x, double y)
    {
        double vx = b[0] - a[0], vy = b[1] - a[1], l2 = vx * vx + vy * vy;
        double t = l2 < 1e-9 ? 0 : Math.Clamp(((x - a[0]) * vx + (y - a[1]) * vy) / l2, 0, 1);
        double px = a[0] + t * vx, py = a[1] + t * vy;
        return (Math.Sqrt((x - px) * (x - px) + (y - py) * (y - py)), t, px, py);
    }

    /// <summary>Punkt siatki na odcinku najbliższy (x, y) – na ukosie w punktach, gdzie x i y są w siatce; null – brak takiego punktu wewnątrz.</summary>
    public static int[] NaOdcinku(int[] a, int[] b, double x, double y, int g)
    {
        int dx = Math.Sign(b[0] - a[0]), dy = Math.Sign(b[1] - a[1]);
        int len = Math.Max(Math.Abs(b[0] - a[0]), Math.Abs(b[1] - a[1]));
        if (!Osiowy(a, b) || len == 0)
        {
            var r = Rzut(a, b, x, y); return new[] { (int)Math.Round(r.X), (int)Math.Round(r.Y) };
        }
        var rr = Rzut(a, b, x, y);
        int k = ZobTory.Grid(rr.T * len, g);
        // na torze poziomym / pionowym – rzędna siatki; na ukosie od węzła w siatce też wychodzi siatka
        if (dy == 0) k = ZobTory.Grid(rr.X, g) - a[0] == 0 ? 0 : (ZobTory.Grid(rr.X, g) - a[0]) * dx;
        else if (dx == 0) k = (ZobTory.Grid(rr.Y, g) - a[1]) * dy;
        k = Math.Clamp(k, 0, len);
        return new[] { a[0] + dx * k, a[1] + dy * k };
    }

    /// <summary>Punkt leży na odcinku (z końcami) – dokładnie, w liczbach całkowitych.</summary>
    public static bool NaLinii(int[] a, int[] b, int[] p)
    {
        long cross = (long)(b[0] - a[0]) * (p[1] - a[1]) - (long)(b[1] - a[1]) * (p[0] - a[0]);
        if (cross != 0) return false;
        return p[0] >= Math.Min(a[0], b[0]) && p[0] <= Math.Max(a[0], b[0]) && p[1] >= Math.Min(a[1], b[1]) && p[1] <= Math.Max(a[1], b[1]);
    }

    static double Dlugosc(int[] a, int[] b) => Math.Sqrt(Math.Pow(b[0] - a[0], 2) + Math.Pow(b[1] - a[1], 2));

    /// <summary>Nowy węzeł w punkcie.</summary>
    public static ObrWezel NowyWezel(ObrObraz o, int[] p) { var w = new ObrWezel { P = new[] { p[0], p[1] } }; o.Wezly.Add(w); return w; }

    /// <summary>
    /// Podział odcinka w punkcie (węzeł wewnątrz): część od A zostaje pod tym samym id, część do B dostaje nowe id; elementy na odcinku
    /// przechodzą na właściwą część z przeliczonym <see cref="ObrElement.T"/>, przypięcie kopiuje się na obie części (E4 przelicza km).
    /// </summary>
    public static ObrWezel Podziel(ObrObraz o, ObrOdcinek e, int[] p)
    {
        var (a, b) = Konce(o, e);
        if (Rowne(a, p)) return Wezel(o, e.A);
        if (Rowne(b, p)) return Wezel(o, e.B);
        var w = NowyWezel(o, p);
        double r = Dlugosc(a, p) / Math.Max(1e-9, Dlugosc(a, b));
        var e2 = new ObrOdcinek { A = w.Id, B = e.B, Styl = e.Styl };
        e.B = w.Id;
        o.Odcinki.Insert(o.Odcinki.IndexOf(e) + 1, e2);
        foreach (var el in o.Elementy.Where(x => x.Na == e.Id))
            if (el.T > r) { el.Na = e2.Id; el.T = r >= 1 ? 0 : (el.T - r) / (1 - r); }
            else el.T = r <= 0 ? 0 : el.T / r;
        foreach (var pr in o.Przypiecia.Where(x => x.El == e.Id).ToList())
        {
            // km toru UT dzieli się w tym samym stosunku (A → węzeł → B)
            ZobUtTor t1 = pr.Tor, t2 = pr.Tor;
            if (pr.Tor != null)
            {
                double km = pr.Tor.S0 + (pr.Tor.S1 - pr.Tor.S0) * r;
                t1 = new ZobUtTor { Tor = pr.Tor.Tor, S0 = pr.Tor.S0, S1 = km }; t2 = new ZobUtTor { Tor = pr.Tor.Tor, S0 = km, S1 = pr.Tor.S1 };
                pr.Tor = t1;
            }
            o.Przypiecia.Add(new ObrPrzypiecie { El = e2.Id, Ut = pr.Ut, Tor = t2, Stan = pr.Stan, Kandydaci = pr.Kandydaci, Powod = pr.Powod });
        }
        return w;
    }

    /// <summary>Węzeł w punkcie: istniejący, podział odcinka, na którym punkt leży, albo nowy.</summary>
    public static ObrWezel WezelW(ObrObraz o, int[] p)
    {
        var w = o.Wezly.FirstOrDefault(x => Rowne(x.P, p));
        if (w != null) return w;
        var e = o.Odcinki.FirstOrDefault(x => { var (a, b) = Konce(o, x); return NaLinii(a, b, p); });
        return e != null ? Podziel(o, e, p) : NowyWezel(o, p);
    }

    /// <summary>Wynik pióra: nowe odcinki, węzły skrzyżowań (do wyboru: skrzyżowanie / rozjazd krzyżowy), rozjazdy powstałe na innych torach.</summary>
    public record Lamana(List<string> Odcinki, List<string> Skrzyzowania, List<string> Rozjazdy);

    /// <summary>
    /// Tor narysowany piórem (plan § 5 sposób 1): punkty łamanej → węzły i proste odcinki. Początek / koniec / załamanie na innym torze –
    /// rozjazd (podział toru); przejście przez węzeł – połączenie; przecięcie bez węzła – węzeł skrzyżowania (element <see cref="Skrzyzowanie"/>),
    /// gdy punkt przecięcia jest całkowity, inaczej tory się mijają bez połączenia.
    /// </summary>
    public static Lamana Rysuj(ObrObraz o, IList<int[]> pts)
    {
        var stare = o.Odcinki.Select(e => e.Id).ToHashSet();
        var stareW = o.Wezly.Select(w => w.Id).ToHashSet();
        var nowe = new List<ObrOdcinek>();
        var rozj = new List<string>();
        ObrWezel prev = null;
        foreach (var p in pts)
        {
            bool bylNaTorze = o.Wezly.All(x => !Rowne(x.P, p)) && o.Odcinki.Any(x => !nowe.Contains(x) && NaLinii(Konce(o, x).A, Konce(o, x).B, p));
            var w = WezelW(o, p);
            if (bylNaTorze) rozj.Add(w.Id);
            if (prev != null && prev.Id != w.Id) { var e = new ObrOdcinek { A = prev.Id, B = w.Id }; o.Odcinki.Add(e); nowe.Add(e); }
            prev = w;
        }
        // istniejące węzły leżące wewnątrz nowych odcinków – połączenie
        for (int i = 0; i < nowe.Count; i++)
        {
            var e = nowe[i]; var (a, b) = Konce(o, e);
            var w = o.Wezly.Where(x => x.Id != e.A && x.Id != e.B && NaLinii(a, b, x.P)).OrderBy(x => Dlugosc(a, x.P)).FirstOrDefault();
            if (w == null) continue;
            var e2 = new ObrOdcinek { A = w.Id, B = e.B }; e.B = w.Id;
            o.Odcinki.Add(e2); nowe.Insert(i + 1, e2);
        }
        // przecięcia nowych odcinków z dawnymi bez wspólnego węzła
        var skrz = new List<string>();
        for (int i = 0; i < nowe.Count; i++)
        {
            // najbliższe przecięcie od początku odcinka; dalsze leżą na reszcie odcinka (następny w kolejce)
            var e = nowe[i];
            var (a, b) = Konce(o, e);
            var hit = o.Odcinki.Where(x => !nowe.Contains(x) && x.A != e.A && x.A != e.B && x.B != e.A && x.B != e.B)
                .Select(f => (f, p: Przeciecie(a, b, Konce(o, f).A, Konce(o, f).B))).Where(z => z.p != null).OrderBy(z => Dlugosc(a, z.p)).FirstOrDefault();
            if (hit.f == null) continue;
            var w = Podziel(o, hit.f, hit.p);
            var e2 = new ObrOdcinek { A = w.Id, B = e.B }; e.B = w.Id;
            o.Odcinki.Add(e2); nowe.Insert(i + 1, e2);
            o.Elementy.Add(new ObrElement { Typ = Skrzyzowanie, Na = w.Id });
            skrz.Add(w.Id);
        }
        // punkty pióra na prostej (klik w połowie toru) – jeden odcinek
        Upros(o, o.Wezly.Where(w => !stareW.Contains(w.Id)).Select(w => w.Id).ToList());
        var ids = o.Odcinki.Select(x => x.Id).ToHashSet();
        return new Lamana(nowe.Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToList(), skrz.Where(s => Wezel(o, s) != null).ToList(), rozj.Where(s => Wezel(o, s) != null).ToList());
    }

    /// <summary>Punkt przecięcia wnętrz dwóch odcinków, gdy jest całkowity; null – brak (albo równoległe).</summary>
    public static int[] Przeciecie(int[] a, int[] b, int[] c, int[] d)
    {
        double rx = b[0] - a[0], ry = b[1] - a[1], sx = d[0] - c[0], sy = d[1] - c[1];
        double den = rx * sy - ry * sx;
        if (Math.Abs(den) < 1e-9) return null;
        double t = ((c[0] - a[0]) * sy - (c[1] - a[1]) * sx) / den, u = ((c[0] - a[0]) * ry - (c[1] - a[1]) * rx) / den;
        if (t <= 1e-9 || t >= 1 - 1e-9 || u <= 1e-9 || u >= 1 - 1e-9) return null;
        double x = a[0] + t * rx, y = a[1] + t * ry;
        if (Math.Abs(x - Math.Round(x)) > 1e-6 || Math.Abs(y - Math.Round(y)) > 1e-6) return null;
        return new[] { (int)Math.Round(x), (int)Math.Round(y) };
    }

    /// <summary>
    /// Porządek grafu po każdej zmianie: węzły w tym samym miejscu – jeden węzeł, odcinki z załamaniami – proste odcinki, odcinki zerowe
    /// i podwójne – usunięte, węzły bez odcinków i elementów – usunięte; elementy i przypięcia usuniętych odcinków / węzłów – usunięte.
    /// Węzły <paramref name="scal"/> (np. dawny rozjazd po usunięciu odgałęzienia), gdy są załamaniem na prostej (dwa odcinki w jednej linii,
    /// ten sam styl, bez elementów) – jeden odcinek. Innych węzłów na prostej nie scala – węzeł z „Podziel tor” zostaje do przeciągnięcia.
    /// </summary>
    /// <summary>
    /// Połączenie toru w węźle (odwrotność <see cref="Podziel"/>): dwa odcinki na jednej prostej w jeden; null – połączone, inaczej powód, dlaczego nie.
    /// </summary>
    public static string Polacz(ObrObraz o, string wezel)
    {
        var w = Wezel(o, wezel); if (w == null) return "brak węzła";
        var przy = Przy(o, wezel);
        if (przy.Count != 2) return przy.Count == 1 ? "to koniec toru – połącz go z innym końcem" : "w rozjeździe i skrzyżowaniu tory się nie łączą";
        if (o.Elementy.Any(x => x.Na == wezel)) return "w węźle stoi element (np. kozioł, odstępy SBL) – najpierw go zdejmij";
        var p1 = Wezel(o, Drugi(przy[0], wezel)).P; var p2 = Wezel(o, Drugi(przy[1], wezel)).P;
        var k1 = Kier(o, przy[0], wezel); var k2 = Kier(o, przy[1], wezel);
        if (!NaLinii(p1, p2, w.P) || k1.X != -k2.X || k1.Y != -k2.Y) return "tor się tu załamuje – po połączeniu nie biegłby co 45°";
        if (przy[0].Styl != przy[1].Styl) return "jeden kawałek jest z kontrolą niezajętości, drugi bez – ustaw ten sam styl";
        Polacz(o, w, przy[0], przy[1]);
        return null;
    }

    /// <summary>Połączenie dwóch końców torów odcinkiem (co 45°) i scalenie na prostej; null – połączone, inaczej powód.</summary>
    public static string PolaczKonce(ObrObraz o, string a, string b)
    {
        var wa = Wezel(o, a); var wb = Wezel(o, b);
        if (wa == null || wb == null || a == b) return "brak końców";
        if (Przy(o, a).Count != 1 || Przy(o, b).Count != 1) return "łączyć można tylko końce torów";
        if (!Osiowy(wa.P, wb.P)) return "końce nie leżą co 45° względem siebie – połącz je piórem (T) z załamaniem";
        var e = new ObrOdcinek { A = a, B = b, Styl = Przy(o, a)[0].Styl };
        o.Odcinki.Add(e);
        // na prostej – jeden odcinek (koniec bez elementów)
        Polacz(o, b); Polacz(o, a);
        return null;
    }

    public static void Upros(ObrObraz o, IEnumerable<string> scal = null)
    {
        // odcinki z załamaniami (np. z innego programu) → proste odcinki przez nowe węzły
        foreach (var e in o.Odcinki.Where(x => x.Pkt?.Count > 0).ToList())
        {
            var pts = e.Pkt.ToList(); e.Pkt = new();
            var bEnd = e.B; var cur = e;
            foreach (var p in pts)
            {
                var w = NowyWezel(o, p);
                var n = new ObrOdcinek { A = w.Id, B = bEnd, Styl = e.Styl };
                cur.B = w.Id; o.Odcinki.Add(n); cur = n;
            }
        }
        // jeden węzeł w jednym miejscu
        var first = new Dictionary<(int, int), string>(); var map = new Dictionary<string, string>();
        foreach (var w in o.Wezly)
            if (first.TryGetValue((w.P[0], w.P[1]), out var keep)) map[w.Id] = keep; else first[(w.P[0], w.P[1])] = w.Id;
        if (map.Count > 0)
        {
            foreach (var e in o.Odcinki) { if (map.TryGetValue(e.A, out var a)) e.A = a; if (map.TryGetValue(e.B, out var b)) e.B = b; }
            foreach (var el in o.Elementy) if (el.Na != null && map.TryGetValue(el.Na, out var n)) el.Na = n;
            o.Wezly.RemoveAll(w => map.ContainsKey(w.Id));
        }
        var ids = o.Wezly.Select(w => w.Id).ToHashSet();
        o.Odcinki.RemoveAll(e => e.A == e.B || !ids.Contains(e.A) || !ids.Contains(e.B));
        // podwójne odcinki
        var pary = new HashSet<(string, string)>();
        foreach (var e in o.Odcinki.ToList())
        {
            var k = string.CompareOrdinal(e.A, e.B) < 0 ? (e.A, e.B) : (e.B, e.A);
            if (!pary.Add(k)) Usun(o, e);
        }
        // załamania na prostej we wskazanych węzłach
        var sc = scal?.ToHashSet() ?? new HashSet<string>();
        bool zm = sc.Count > 0;
        while (zm)
        {
            zm = false;
            foreach (var w in o.Wezly.Where(x => sc.Contains(x.Id)).ToList())
            {
                var przy = Przy(o, w.Id);
                if (przy.Count != 2 || o.Elementy.Any(x => x.Na == w.Id)) continue;
                var (e1, e2) = (przy[0], przy[1]);
                if (e1.Styl != e2.Styl) continue;
                var k1 = Kier(o, e1, w.Id); var k2 = Kier(o, e2, w.Id);
                var p1 = Wezel(o, Drugi(e1, w.Id)).P; var p2 = Wezel(o, Drugi(e2, w.Id)).P;
                if (!NaLinii(p1, p2, w.P) || (k1.X != -k2.X || k1.Y != -k2.Y)) continue;
                Polacz(o, w, e1, e2);
                zm = true; break;
            }
        }
        o.Wezly.RemoveAll(w => !o.Odcinki.Any(e => e.A == w.Id || e.B == w.Id) && !o.Elementy.Any(x => x.Na == w.Id));
        var ws = o.Wezly.Select(w => w.Id).ToHashSet(); var es = o.Odcinki.Select(e => e.Id).ToHashSet();
        o.Elementy.RemoveAll(x => x.Na != null && !ws.Contains(x.Na) && !es.Contains(x.Na));
        // element skrzyżowania tylko w węźle z czterema odcinkami
        o.Elementy.RemoveAll(x => (x.Typ == Skrzyzowanie || x.Typ == RozjazdKrzyzowy) && Przy(o, x.Na).Count != 4);
        o.Przypiecia.RemoveAll(p => !ws.Contains(p.El) && !es.Contains(p.El) && !o.Elementy.Any(x => x.Id == p.El));
    }

    /// <summary>Dwa odcinki w jednej linii przez węzeł <paramref name="w"/> → jeden (elementy z przeliczonym położeniem).</summary>
    static void Polacz(ObrObraz o, ObrWezel w, ObrOdcinek e1, ObrOdcinek e2)
    {
        // e1 kierujemy do w (A1 → w), e2 od w (w → B2); wynik: A1 → B2 pod id e1
        // przypięcie: ten sam tor UT po obu stronach – km od końca A1 do końca B2; inny tor – bez przypięcia (F8 przypnie)
        var p1 = o.Przypiecia.FirstOrDefault(p => p.El == e1.Id); var p2 = o.Przypiecia.FirstOrDefault(p => p.El == e2.Id);
        ZobUtTor nowy = null;
        if (p1?.Tor != null && p2?.Tor != null && p1.Tor.Tor == p2.Tor.Tor)
            nowy = new ZobUtTor { Tor = p1.Tor.Tor, S0 = e1.A == w.Id ? p1.Tor.S1 : p1.Tor.S0, S1 = e2.A == w.Id ? p2.Tor.S1 : p2.Tor.S0 };
        if (p1 != null)
        {
            if (nowy != null) { p1.Tor = nowy; if (p1.Stan != p2.Stan) p1.Stan = ObrPrzypiecie.Pewne; }
            else o.Przypiecia.Remove(p1);
        }
        if (e1.A == w.Id) Odwroc(o, e1);
        if (e2.B == w.Id) Odwroc(o, e2);
        var a = Wezel(o, e1.A).P; var b = Wezel(o, e2.B).P;
        double l1 = Dlugosc(a, w.P), l = Math.Max(1e-9, l1 + Dlugosc(w.P, b)), r = l1 / l;
        foreach (var el in o.Elementy.Where(x => x.Na == e1.Id)) el.T *= r;
        foreach (var el in o.Elementy.Where(x => x.Na == e2.Id)) { el.Na = e1.Id; el.T = r + el.T * (1 - r); }
        e1.B = e2.B;
        o.Odcinki.Remove(e2);
        o.Przypiecia.RemoveAll(p => p.El == e2.Id);
        o.Wezly.Remove(w);
    }

    /// <summary>Zamiana końców odcinka (A ↔ B) – elementy zostają w tym samym miejscu, ich kierunek się odwraca.</summary>
    public static void Odwroc(ObrObraz o, ObrOdcinek e)
    {
        (e.A, e.B) = (e.B, e.A);
        foreach (var el in o.Elementy.Where(x => x.Na == e.Id)) { el.T = 1 - el.T; el.Kier = -el.Kier; }
    }

    static void Usun(ObrObraz o, ObrOdcinek e)
    {
        o.Odcinki.Remove(e);
        o.Elementy.RemoveAll(x => x.Na == e.Id);
        o.Przypiecia.RemoveAll(p => p.El == e.Id);
    }

    /// <summary>Usunięcie odcinków i węzłów (z odcinkami przy nich); potem <see cref="Upros"/> – rozjazd bez odgałęzienia znów jest prostym torem.</summary>
    public static void Usun(ObrObraz o, IEnumerable<string> wezly, IEnumerable<string> odcinki)
    {
        var ws = wezly.ToHashSet();
        var sasiad = new HashSet<string>();
        foreach (var e in o.Odcinki.Where(e => odcinki.Contains(e.Id) || ws.Contains(e.A) || ws.Contains(e.B)).ToList()) { sasiad.Add(e.A); sasiad.Add(e.B); Usun(o, e); }
        o.Elementy.RemoveAll(x => x.Na != null && ws.Contains(x.Na));
        o.Wezly.RemoveAll(w => ws.Contains(w.Id));
        sasiad.ExceptWith(ws);
        Upros(o, sasiad);
    }

    /// <summary>Węzły zaznaczenia: wskazane węzły i końce wskazanych odcinków.</summary>
    public static HashSet<string> WezlyZaznaczenia(ObrObraz o, IEnumerable<string> wezly, IEnumerable<string> odcinki)
    {
        var s = wezly.ToHashSet();
        foreach (var e in o.Odcinki.Where(e => odcinki.Contains(e.Id))) { s.Add(e.A); s.Add(e.B); }
        return s;
    }

    /// <summary>Przesunięcie węzłów o (dx, dy) – odcinki do innych węzłów się rozciągają (plan § 8); potem <see cref="Upros"/>.</summary>
    public static void Przesun(ObrObraz o, IEnumerable<string> wezly, int dx, int dy)
    {
        var s = wezly.ToHashSet();
        foreach (var w in o.Wezly.Where(w => s.Contains(w.Id))) w.P = new[] { w.P[0] + dx, w.P[1] + dy };
        Upros(o);
    }

    /// <summary>Rząd: odcinki poziome w tej samej rzędnej połączone z odcinkiem <paramref name="odcinek"/> (przez węzły).</summary>
    public static HashSet<string> Rzad(ObrObraz o, ObrOdcinek odcinek)
    {
        var wezly = new HashSet<string>();
        if (!Poziomy(o, odcinek)) return wezly;
        int y = Wezel(o, odcinek.A).P[1];
        var q = new Queue<string>(new[] { odcinek.A, odcinek.B });
        while (q.Count > 0)
        {
            var n = q.Dequeue();
            if (!wezly.Add(n)) continue;
            foreach (var e in Przy(o, n).Where(e => Poziomy(o, e) && Wezel(o, e.A).P[1] == y)) q.Enqueue(Drugi(e, n));
        }
        return wezly;
    }

    /// <summary>
    /// Przesunięcie rzędu (węzłów <paramref name="wezly"/>) w pionie o <paramref name="dy"/> z łącznikami (plan § 8): łącznik „[” (ukos – pion – ukos)
    /// – załamanie za ukosem jedzie z rzędem, pion się wydłuża / skraca; długi ukos do węzła, który zostaje – węzeł rzędu przesuwa się wzdłuż
    /// ukosu (tor rzędu się wydłuża / skraca), więc ukos zostaje co 45°. Inne odcinki się rozciągają.
    /// </summary>
    public static void PrzesunRzad(ObrObraz o, IEnumerable<string> wezly, int dy)
    {
        if (dy == 0) return;
        var s = wezly.ToHashSet();
        var ruch = s.ToDictionary(id => id, _ => (X: 0, Y: dy));
        foreach (var id in s)
        {
            var ukosy = Przy(o, id).Where(e => !s.Contains(Drugi(e, id)) && Ukosny(o, e)).ToList();
            foreach (var e in ukosy)
            {
                var m = Drugi(e, id);
                var przyM = Przy(o, m);
                // załamanie łącznika „[”: dwa odcinki, drugi pionowy – jedzie z rzędem
                if (przyM.Count == 2 && przyM.First(x => x != e) is var inny && Pionowy(o, inny) && !ruch.ContainsKey(m)) ruch[m] = (0, dy);
                else if (ukosy.Count == 1 && !ruch.ContainsKey(m))
                {
                    // ukos do węzła, który zostaje: węzeł rzędu ślizga się po ukosie
                    var k = Kier(o, e, id);
                    ruch[id] = (SlizgX(k, dy), dy);
                }
            }
        }
        foreach (var (id, (mx, my)) in ruch) { var w = Wezel(o, id); w.P = new[] { w.P[0] + mx, w.P[1] + my }; }
        Upros(o);
    }

    /// <summary>
    /// Przesunięcie x węzła na ukosie o kierunku <paramref name="k"/> (od węzła do węzła stałego), gdy węzeł idzie w pionie o <paramref name="dy"/>:
    /// zostaje na prostej przez węzeł stały.
    /// </summary>
    static int SlizgX((int X, int Y) k, int dy) => k.X * k.Y * dy;

    /// <summary>
    /// Rozsuń rzędy (Ctrl+G, plan § 8): rzędne <paramref name="ys"/> (co najmniej trzy) – skrajne zostają, środkowe w równych odstępach
    /// (do siatki); każdy rząd jedzie z łącznikami (<see cref="PrzesunRzad"/>), prowadnice w tych rzędnych – też. Zwraca nowe rzędne.
    /// </summary>
    public static List<int> RozsunRzedy(ObrObraz o, IEnumerable<string> wezly, int g)
    {
        var ws = wezly.ToHashSet();
        var ys = o.Wezly.Where(w => ws.Contains(w.Id) && Przy(o, w.Id).Any(e => Poziomy(o, e))).Select(w => w.P[1]).Distinct().OrderBy(y => y).ToList();
        if (ys.Count < 3) return null;
        int y0 = ys[0], y1 = ys[^1];
        var cel = ys.Select((y, i) => i == 0 || i == ys.Count - 1 ? y : ZobTory.Grid(y0 + (double)(y1 - y0) * i / (ys.Count - 1), g)).ToList();
        for (int i = 1; i < ys.Count - 1; i++)
        {
            int dy = cel[i] - ys[i];
            if (dy == 0) continue;
            var start = o.Odcinki.Where(e => Poziomy(o, e) && Wezel(o, e.A).P[1] == ys[i] && (ws.Contains(e.A) || ws.Contains(e.B))).ToList();
            var rz = new HashSet<string>();
            foreach (var e in start) rz.UnionWith(Rzad(o, e));
            PrzesunRzad(o, rz, dy);
            int idx = o.Rzedy.IndexOf(ys[i]);
            if (idx >= 0) o.Rzedy[idx] = cel[i];
        }
        o.Rzedy = o.Rzedy.Distinct().OrderBy(y => y).ToList();
        return cel;
    }

    /// <summary>Fragment rysunku do schowka (kopiuj / wklej / powiel – bez przypięć, plan § 8).</summary>
    public class Fragment
    {
        public List<ObrWezel> Wezly { get; set; } = new();
        public List<ObrOdcinek> Odcinki { get; set; } = new();
        public List<ObrElement> Elementy { get; set; } = new();
        /// <summary>Przypięcia fragmentu (szkic z UT – E7); kopiowanie i klocki – puste.</summary>
        public List<ObrPrzypiecie> Przypiecia { get; set; } = new();
    }

    /// <summary>Kopia zaznaczenia: węzły zaznaczenia, odcinki między nimi, elementy na nich.</summary>
    public static Fragment Kopiuj(ObrObraz o, IEnumerable<string> wezly, IEnumerable<string> odcinki)
    {
        var s = WezlyZaznaczenia(o, wezly, odcinki);
        var json = System.Text.Json.JsonSerializer.Serialize(new Fragment
        {
            Wezly = o.Wezly.Where(w => s.Contains(w.Id)).ToList(),
            Odcinki = o.Odcinki.Where(e => s.Contains(e.A) && s.Contains(e.B)).ToList(),
        }, UtDocument.Json);
        var f = System.Text.Json.JsonSerializer.Deserialize<Fragment>(json, UtDocument.Json);
        var es = f.Odcinki.Select(e => e.Id).ToHashSet();
        f.Elementy = o.Elementy.Where(x => x.Na != null && (s.Contains(x.Na) || es.Contains(x.Na)))
            .Select(x => System.Text.Json.JsonSerializer.Deserialize<ObrElement>(System.Text.Json.JsonSerializer.Serialize(x, UtDocument.Json), UtDocument.Json)).ToList();
        return f;
    }

    /// <summary>Wklejenie fragmentu przesuniętego o (dx, dy) – nowe id; węzły w miejscu istniejących łączą się z nimi. Zwraca id węzłów i odcinków.</summary>
    public static (HashSet<string> Wezly, HashSet<string> Odcinki) Wklej(ObrObraz o, Fragment f, int dx, int dy)
    {
        var map = new Dictionary<string, string>();
        foreach (var w in f.Wezly) { var n = NowyWezel(o, new[] { w.P[0] + dx, w.P[1] + dy }); map[w.Id] = n.Id; }
        foreach (var e in f.Odcinki) { var n = new ObrOdcinek { A = map[e.A], B = map[e.B], Styl = e.Styl }; o.Odcinki.Add(n); map[e.Id] = n.Id; }
        foreach (var x in f.Elementy)
            if (map.TryGetValue(x.Na, out var na))
                o.Elementy.Add(new ObrElement { Typ = x.Typ, Na = na, T = x.T, Kier = x.Kier, Tekst = x.Tekst, Styl = x.Styl, Pola = x.Pola, Czesci = x.Czesci?.ToList(), Opisy = x.Opisy.Select(d => new ObrOpis { Rola = d.Rola, Tekst = d.Tekst, D = d.D.ToArray() }).ToList() });
        var pts = map.Where(kv => f.Wezly.Any(w => w.Id == kv.Key)).Select(kv => kv.Value).ToList();
        var pos = pts.Select(id => Wezel(o, id).P).ToList();
        Upros(o);
        // po połączeniu z istniejącymi węzłami id mogły się zmienić – zaznaczenie po położeniu
        var wz = o.Wezly.Where(w => pos.Any(p => Rowne(p, w.P))).Select(w => w.Id).ToHashSet();
        return (wz, o.Odcinki.Where(e => wz.Contains(e.A) && wz.Contains(e.B)).Select(e => e.Id).ToHashSet());
    }

    /// <summary>Rodzaj węzła wg liczby odcinków i kierunków.</summary>
    public enum Rodzaj { Koniec, Zalamanie, Rozjazd, Skrzyzowanie, RozjazdKrzyzowy, Nieznany }

    public static Rodzaj RodzajWezla(ObrObraz o, string id)
    {
        var przy = Przy(o, id);
        if (przy.Count == 1) return Rodzaj.Koniec;
        if (przy.Count == 2) return Rodzaj.Zalamanie;
        if (przy.Count == 3) return Odgalezienie(o, id) != null ? Rodzaj.Rozjazd : Rodzaj.Nieznany;
        if (przy.Count == 4)
        {
            var el = o.Elementy.FirstOrDefault(x => x.Na == id && (x.Typ == Skrzyzowanie || x.Typ == RozjazdKrzyzowy));
            return el?.Typ == RozjazdKrzyzowy ? Rodzaj.RozjazdKrzyzowy : el != null ? Rodzaj.Skrzyzowanie : Rodzaj.Nieznany;
        }
        return Rodzaj.Nieznany;
    }

    /// <summary>
    /// Odgałęzienie rozjazdu: z trzech odcinków dwa są w jednej linii (tor zasadniczy), trzeci to odgałęzienie; null – brak pary w linii.
    /// </summary>
    public static ObrOdcinek Odgalezienie(ObrObraz o, string id)
    {
        var przy = Przy(o, id);
        if (przy.Count != 3) return null;
        for (int i = 0; i < 3; i++)
            for (int j = i + 1; j < 3; j++)
            {
                var a = Kier(o, przy[i], id); var b = Kier(o, przy[j], id);
                if (a.X == -b.X && a.Y == -b.Y) return przy[3 - i - j];
            }
        return null;
    }

    /// <summary>Uwagi do rysunku (plan § 9 – część bez UT): odcinek nie co 45°, węzeł o nieznanym kształcie. Nie blokują rysowania.</summary>
    public static List<(string El, string Tekst)> Uwagi(ObrObraz o)
    {
        var r = new List<(string, string)>();
        foreach (var e in o.Odcinki) { var (a, b) = Konce(o, e); if (!Osiowy(a, b)) r.Add((e.Id, "odcinek nie co 45°")); }
        foreach (var w in o.Wezly)
            if (RodzajWezla(o, w.Id) == Rodzaj.Nieznany)
                r.Add((w.Id, Przy(o, w.Id).Count == 3 ? "rozjazd bez toru na wprost" : $"węzeł z {Przy(o, w.Id).Count} torami – skrzyżowanie / rozjazd krzyżowy (PPM)"));
        return r;
    }
}

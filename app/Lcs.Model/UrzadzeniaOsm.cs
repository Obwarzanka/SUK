using System.Text.Json;

namespace Lcs.Model;

/// <summary>Obiekt z OpenStreetMap: sygnalizator, zwrotnica, wykolejnica, kozioł (<c>railway=…</c>), miejsce (PUWG 1992), tagi, kierunki dróg OSM w węźle.</summary>
public record OsmObiekt(long Id, string Railway, XY P, Dictionary<string, string> Tags, List<XY> Kierunki)
{
    public string Tag(string k) => Tags.TryGetValue(k, out var v) ? v : "";
}

/// <summary>Tor OSM z numerem (<c>railway:track_ref</c>): punkty osi (PUWG 1992).</summary>
public record OsmTor(long Id, string Nr, List<XY> Pts);

/// <summary>Dane OSM dla prostokąta: obiekty i tory z numerem.</summary>
public record OsmUrzadzenia(List<OsmObiekt> Obiekty, List<OsmTor> Tory);

/// <summary>Propozycja urządzenia z OSM: rodzaj (<see cref="UtDevice.Kind"/>), nazwa (pusta – z podpowiedzi), tor, miejsce, kierunek, strona, odległość od osi [m w terenie], opis i uwagi.</summary>
public record PropozycjaUrz(OsmObiekt Osm, string Kind, string Name, string Track, double S, int Dir, int Side, double Dist, string Opis, string Uwaga)
{
    public string Klucz => "u:" + Osm.Id;
}

/// <summary>Propozycja numeru z OSM: rozjazd (<c>Turnout</c>) albo tor (<c>Track</c>), numer w projekcie, numer z OSM, uwagi.</summary>
public record PropozycjaNr(bool Rozjazd, string Id, string Obecny, string Osm, double Dist, string Uwaga)
{
    public string Klucz => (Rozjazd ? "z:" : "t:") + Id;
}

/// <summary>
/// Podpowiedzi z OSM: miejsca sygnalizatorów (i wykolejnic, kozłów), numery zwrotnic i torów – tylko propozycje do zatwierdzenia przez
/// projektanta (decyzja właściciela 2026-10-27: „sugerować numery zwrotnic, torów i umiejscowienie sygnalizatorów”; tory rysuje człowiek,
/// bez generatora z OSM – 2026-10-16). Propozycje dotyczą tylko torów już narysowanych w projekcie.
/// </summary>
public static class UrzadzeniaOsm
{
    /// <summary>Najdalej od osi toru projektu, przy której obiekt OSM trafia na ten tor [m w terenie].</summary>
    public const double MaxDoToru = 10;
    /// <summary>Najdalej od PR rozjazdu projektu, przy której zwrotnica OSM daje mu numer [m w terenie].</summary>
    public const double MaxDoRozjazdu = 15;
    /// <summary>Najdalej od osi toru projektu, przy której tor OSM z numerem się liczy [m w terenie] (rozstaw torów ok. 4,5 m).</summary>
    public const double MaxDoToruNr = 2.5;
    /// <summary>Urządzenie tego rodzaju bliżej niż tyle na tym samym torze – już jest w projekcie [m w terenie].</summary>
    public const double JuzJest = 15;

    /// <summary>Odpowiedź Overpass: węzły <c>railway=signal|switch|derail|buffer_stop</c> i drogi kolejowe (<c>out geom</c>, z listą węzłów).</summary>
    public static OsmUrzadzenia Parse(byte[] json)
    {
        var ob = new List<(long Id, string R, XY P, Dictionary<string, string> T)>(); var tory = new List<OsmTor>();
        var drogi = new List<(List<long> N, List<XY> Pts)>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("elements", out var els))
            foreach (var el in els.EnumerateArray())
            {
                string type = el.TryGetProperty("type", out var t) ? t.GetString() : "";
                var tags = new Dictionary<string, string>();
                if (el.TryGetProperty("tags", out var tg)) foreach (var x in tg.EnumerateObject()) tags[x.Name] = x.Value.GetString() ?? "";
                string T(string k) => tags.TryGetValue(k, out var v) ? v : "";
                if (type == "node" && T("railway") is "signal" or "switch" or "derail" or "buffer_stop" && el.TryGetProperty("lat", out var la) && el.TryGetProperty("lon", out var lo))
                    ob.Add((el.GetProperty("id").GetInt64(), T("railway"), Geo.ToPuwg92(la.GetDouble(), lo.GetDouble()), tags));
                else if (type == "way" && el.TryGetProperty("geometry", out var g))
                {
                    var pts = g.EnumerateArray().Select(q => Geo.ToPuwg92(q.GetProperty("lat").GetDouble(), q.GetProperty("lon").GetDouble())).ToList();
                    var nodes = el.TryGetProperty("nodes", out var nn) ? nn.EnumerateArray().Select(q => q.GetInt64()).ToList() : new();
                    drogi.Add((nodes, pts));
                    if (T("railway:track_ref").Length > 0 && pts.Count >= 2) tory.Add(new OsmTor(el.GetProperty("id").GetInt64(), T("railway:track_ref").Split(';')[0].Trim(), pts));
                }
            }
        var res = new List<OsmObiekt>();
        foreach (var (id, r, p, tags) in ob)
        {
            // kierunek drogi OSM w węźle (od węzła do następnego) – „forward” sygnalizatora jest zgodny z kierunkiem drogi
            var kier = new List<XY>();
            foreach (var (n, pts) in drogi)
            {
                int i = n.IndexOf(id); if (i < 0 || pts.Count != n.Count || pts.Count < 2) continue;
                var v = i + 1 < pts.Count ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1]; double l = Math.Sqrt(v.X * v.X + v.Y * v.Y);
                if (l > 1e-6) kier.Add(v * (1 / l));
            }
            res.Add(new OsmObiekt(id, r, p, tags, kier));
        }
        return new OsmUrzadzenia(res, tory);
    }

    /// <summary>Rodzaj urządzenia UT z tagów sygnalizatora OSM (null – pominięty, np. sam radiostop) i opis rodzaju.</summary>
    public static (string Kind, string Opis, string Uwaga)? Rodzaj(OsmObiekt o)
    {
        if (o.Railway == "derail") return (UtDevice.Wykolejnica, "wykolejnica", "");
        if (o.Railway == "buffer_stop") return (UtDevice.Koziol, "kozioł oporowy", "");
        if (o.Railway != "signal") return null;
        var sig = o.Tags.Where(x => x.Key.StartsWith("railway:signal:") && x.Key.Count(c => c == ':') == 2).ToDictionary(x => x.Key[15..], x => x.Value.ToLowerInvariant());
        bool Ma(string k) => sig.ContainsKey(k) && sig[k] != "no";
        // wskaźniki W4 / W5 rozpoznane po wartości (np. PL-PKP:w4)
        if (sig.Values.Any(v => v.Contains("w5"))) return (UtDevice.W5, "wskaźnik W5", "");
        if (sig.Values.Any(v => v.Contains("w4"))) return (UtDevice.W4, "wskaźnik W4", "");
        if (Ma("main") || Ma("combined")) return (UtDevice.Semafor, "semafor", "");
        if (Ma("distant") || Ma("station_distant")) return (UtDevice.TarczaOstrz, "tarcza ostrzegawcza", "");
        if (Ma("minor") || Ma("shunting")) return (UtDevice.TarczaMan, "tarcza manewrowa", "");
        if (Ma("stop")) return (UtDevice.W4, "wskaźnik W4", "w OSM „stop” – sprawdź, czy to W4");
        if (sig.Keys.All(k => k is "direction" or "position" or "height" or "form")) return (UtDevice.Semafor, "sygnalizator", "rodzaj nieoznaczony w OSM");
        return null;
    }

    /// <summary>Propozycje urządzeń przy torach projektu. Pomija obiekty, przy których ten rodzaj urządzenia już stoi na tym torze.</summary>
    public static List<PropozycjaUrz> Urzadzenia(UtProject p, OsmUrzadzenia osm, Dictionary<string, TrasaResult> geom = null)
    {
        var res = new List<PropozycjaUrz>();
        var tory = Tory(p, geom);
        foreach (var o in osm.Obiekty)
        {
            if (Rodzaj(o) is not var (kind, opis, uw0)) continue;
            var kand = tory.Select(x => (x.t, x.r, n: x.r.Nearest(o.P, 0.5))).Select(x => (x.t, x.r, S: x.n.S, D: x.n.Dist / x.r.K))
                .Where(x => x.D <= MaxDoToru).OrderBy(x => x.D).ToList();
            if (kand.Count == 0) continue;
            var (t, r, s, d) = kand[0];
            var uw = new List<string>(); if (uw0.Length > 0) uw.Add(uw0);
            if (kand.Count > 1 && kand[1].D - d < 1) uw.Add($"tor do sprawdzenia (tor {Nr(kand[1].t)} prawie tak samo blisko)");
            int dir = 1, side = 1;
            if (kind == UtDevice.Koziol)
            {
                bool end = s > r.Length / 2; double se = end ? r.Length : 0;
                if (Math.Abs(s - se) <= 10) s = se; else uw.Add("daleko od końca toru");
                dir = end ? 1 : -1;
            }
            else if (UtDevice.KindInfo(kind).Directed)
            {
                string kier = o.Tag("railway:signal:direction");
                var u = Element.Dir(r.At(s).Az);
                if (kind == UtDevice.Wykolejnica || o.Kierunki.Count == 0 || kier is not ("forward" or "backward"))
                    uw.Add("kierunek do sprawdzenia");
                else
                {
                    var v = o.Kierunki[0]; double dot = v.X * u.X + v.Y * u.Y;
                    dir = (dot >= 0 ? 1 : -1) * (kier == "backward" ? -1 : 1);
                }
                string poz = o.Tag("railway:signal:position");
                side = poz == "left" ? -1 : 1;
            }
            if (s < -0.01 || s > r.Length + 0.01) continue;
            if (p.Devices.Any(x => x.Kind == kind && x.Track == t.Id && Math.Abs(x.S - s) < JuzJest && (!UtDevice.KindInfo(kind).Directed || x.Dir == dir))) continue;
            string name = Nazwa(o, kind);
            if (name.Length > 0 && Lcs.Model.Urzadzenia.NameTaken(p, name, t.Id)) { uw.Add($"nazwa {name} już jest w posterunku"); name = ""; }
            res.Add(new PropozycjaUrz(o, kind, name, t.Id, s, dir, side, d, opis, string.Join(", ", uw)));
        }
        return res.OrderBy(x => x.Track).ThenBy(x => x.S).ToList();
    }

    /// <summary>Nazwa z <c>ref</c> OSM; liczba przy tarczy, wykolejnicy – z przedrostkiem rodzaju (Tm12, Wk3).</summary>
    static string Nazwa(OsmObiekt o, string kind)
    {
        string rf = o.Tag("ref").Trim(); if (rf.Length == 0 || kind == UtDevice.Koziol) return "";
        string pre = UtDevice.KindInfo(kind).Prefix;
        return pre.Length > 0 && char.IsDigit(rf[0]) ? pre + rf : rf;
    }

    /// <summary>
    /// Numery zwrotnic: zwrotnica OSM z <c>ref</c> najbliżej PR rozjazdu (do <see cref="MaxDoRozjazdu"/> m), każda zwrotnica OSM dla
    /// jednego rozjazdu. Propozycja, gdy numer w projekcie jest inny albo pusty.
    /// </summary>
    public static List<PropozycjaNr> NumeryZwrotnic(UtProject p, OsmUrzadzenia osm, Dictionary<string, TrasaResult> geom = null)
    {
        var res = new List<PropozycjaNr>();
        var zw = osm.Obiekty.Where(o => o.Railway == "switch" && o.Tag("ref").Trim().Length > 0).ToList();
        var pary = new List<(UtTurnout T, OsmObiekt O, double D)>();
        foreach (var t in p.Turnouts)
        {
            if (p.Tracks.FirstOrDefault(x => x.Id == t.Track) is not UtTrack tr) continue;
            var r = geom != null && geom.TryGetValue(tr.Id, out var g) ? g : Trasa.Compute(tr); if (r.Elements.Count == 0) continue;
            var pr = r.At(Math.Clamp(t.S, 0, r.Length)).P;
            foreach (var o in zw) if (XY.Dist(o.P, pr) / r.K is var d && d <= MaxDoRozjazdu) pary.Add((t, o, d));
        }
        var uzyteT = new HashSet<string>(); var uzyteO = new HashSet<long>();
        foreach (var (t, o, d) in pary.OrderBy(x => x.D))
        {
            if (uzyteT.Contains(t.Id) || uzyteO.Contains(o.Id)) continue;
            uzyteT.Add(t.Id); uzyteO.Add(o.Id);
            string nr = o.Tag("ref").Trim();
            if (nr == t.Number) continue;
            var uw = new List<string>();
            if (t.Number.Length > 0) uw.Add($"w projekcie {t.Number}");
            if (Rozjazdy.NumberUsed(p, nr, t.Id, t.Track)) uw.Add($"numer {nr} już jest w posterunku");
            res.Add(new PropozycjaNr(true, t.Id, t.Number, nr, d, string.Join(", ", uw)));
        }
        return res.OrderBy(x => x.Osm, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Numery torów: punkty osi toru projektu co 10 m, w każdym najbliższy tor OSM z numerem (do <see cref="MaxDoToruNr"/> m);
    /// numer, który ma co najmniej połowa punktów. Propozycja, gdy numer w projekcie jest inny albo pusty.
    /// </summary>
    public static List<PropozycjaNr> NumeryTorow(UtProject p, OsmUrzadzenia osm, Dictionary<string, TrasaResult> geom = null)
    {
        var res = new List<PropozycjaNr>();
        if (osm.Tory.Count == 0) return res;
        foreach (var (t, r) in Tory(p, geom))
        {
            // łącznik (tor między dwoma rozjazdami łącznika) nie ma numeru – przy rozjazdach leży wzdłuż torów, które łączy
            if (p.Turnouts.Any(z => z.Branch == t.Id && z.Partner != null)) continue;
            double len = r.Length; if (len < 10) continue;
            int n = Math.Max(3, (int)(len / 10)); var glosy = new Dictionary<string, (int N, double D)>();
            for (int i = 0; i < n; i++)
            {
                var q = r.At(len * (i + 0.5) / n).P;
                string best = null; double bd = MaxDoToruNr;
                foreach (var ot in osm.Tory)
                    for (int j = 1; j < ot.Pts.Count; j++)
                        if (DoOdcinka(q, ot.Pts[j - 1], ot.Pts[j]) / r.K is var dd && dd < bd) { bd = dd; best = ot.Nr; }
                if (best != null) { var g = glosy.GetValueOrDefault(best); glosy[best] = (g.N + 1, g.D + bd); }
            }
            if (glosy.Count == 0) continue;
            var (nr, (k, sd)) = glosy.OrderByDescending(x => x.Value.N).Select(x => (x.Key, x.Value)).First();
            if (k * 2 < n || nr == t.Number) continue;
            var uw = new List<string>();
            if (t.Number.Length > 0) uw.Add($"w projekcie {t.Number}");
            if (k * 10 < n * 8) uw.Add($"tor OSM tylko na {100 * k / n}% długości");
            if (glosy.Count > 1) uw.Add("w OSM też " + string.Join(", ", glosy.Keys.Where(x => x != nr)));
            res.Add(new PropozycjaNr(false, t.Id, t.Number, nr, sd / k, string.Join(", ", uw)));
        }
        return res;
    }

    static List<(UtTrack t, TrasaResult r)> Tory(UtProject p, Dictionary<string, TrasaResult> geom) =>
        p.Tracks.Select(t => (t, r: geom != null && geom.TryGetValue(t.Id, out var g) ? g : Trasa.Compute(t))).Where(x => x.r.Elements.Count > 0).ToList();

    static string Nr(UtTrack t) => t.Number.Length > 0 ? t.Number : "bez numeru";

    static double DoOdcinka(XY p, XY a, XY b)
    {
        var d = b - a; double l2 = d.X * d.X + d.Y * d.Y;
        double t = l2 > 0 ? Math.Clamp(((p.X - a.X) * d.X + (p.Y - a.Y) * d.Y) / l2, 0, 1) : 0;
        return XY.Dist(p, a + d * t);
    }

    /// <summary>Urządzenie z zatwierdzonej propozycji (bez nazwy w OSM – kolejna wolna nazwa rodzaju w posterunku toru).</summary>
    public static UtDevice Urzadzenie(UtProject p, PropozycjaUrz x) => new()
    {
        Kind = x.Kind, Track = x.Track, S = x.S, Dir = x.Dir, Side = x.Side,
        Name = x.Name.Length > 0 ? x.Name : Lcs.Model.Urzadzenia.NextName(p, x.Kind, x.Track),
    };
}

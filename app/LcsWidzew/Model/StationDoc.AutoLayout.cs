using System.Text.Json.Nodes;

namespace LcsWidzew.Model;

/// <summary>
/// Autoukład planu schematycznego w skali (Ig-10 § 3) – PROPOZYCJA do ręcznych poprawek (zasada: plan układa właściciel,
/// automat tylko proponuje; całość cofa jedno Ctrl+Z). Zmienia wyłącznie rysunek układu torowego (plan): położenia węzłów
/// i przebieg torów oraz napisy / perony / przejazdy planu. Topologia, długości, odcinki i zobrazowanie (pulpit) bez zmian.
/// </summary>
public partial class StationDoc
{
    /// <summary>Jednostki planu na 1 m długości wg skali podłużnej planu (1:2000 → 1,25 j./m przy 2,5 j./mm rysunku Ig-10).</summary>
    public double PlanUnitsPerM => Ux;
    /// <summary>Rozstaw torów na planie: 4,75 m w skali poprzecznej (1:500 → 9,5 mm = 23,75 j.).</summary>
    public double PlanLane => 4.75 * Uy;
    /// <summary>Szlak rysowany w skróceniu (Ig-10 § 1 ust. 8) – najwyżej tyle metrów.</summary>
    public const double PlanSzlakMax = 250;

    /// <summary>
    /// X – najmniejsze kwadraty z równań x_b − x_a = ±len·skala (kierunek z obecnego planu, szlak skrócony);
    /// Y – tory poziome w pasach co rozstaw (kolejność pasów z obecnego planu); łączniki i odgałęzienia – proste skosy.
    /// Zwraca raport (odchyłki długości, odwrócone tory).
    /// </summary>
    public string AutoLayoutPlan()
    {
        var ids = Nodes.Select(k => k.Key).ToList();
        if (ids.Count == 0) return "brak węzłów";
        var old = ids.ToDictionary(k => k, Pos);
        var edges = EdgeObjs.ToList();
        var at = ids.ToDictionary(k => k, _ => new List<JsonObject>());
        foreach (var e in edges) { if (at.ContainsKey(Sv(e["a"]))) at[Sv(e["a"])].Add(e); if (at.ContainsKey(Sv(e["b"]))) at[Sv(e["b"])].Add(e); }
        bool Szlak(JsonObject e) => e["szlak"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;
        string Other(JsonObject e, string n) => Sv(e["a"]) == n ? Sv(e["b"]) : Sv(e["a"]);
        // węzeł „przelotowy” łańcucha: dwa tory, bez zwrotnicy
        var switchNodes = Switches.Select(s => Sv(s.Value["node"])).ToHashSet();
        bool ChainN(string n) => at[n].Count == 2 && !switchNodes.Contains(n);
        string Walk(string from, JsonObject via)
        {
            var cur = Other(via, from); var e = via; int guard = 0;
            while (ChainN(cur) && guard++ < 500) { var nx = at[cur].First(x => x != e); e = nx; cur = Other(nx, cur); }
            return cur;
        }

        // ---------------- X: długości, kolejność węzłów z obecnego planu nienaruszalna
        // (komputer zależności wyznacza kierunek jazdy z położenia końców toru – tor nie może się odwrócić)
        var dirE = new List<(string U, string V, double D)>();      // x_V ≥ x_U + D (D – długość w skali)
        var nb = ids.ToDictionary(k => k, _ => new List<(string O, double D, bool Out)>());
        foreach (var e in edges)
        {
            string a = Sv(e["a"]), b = Sv(e["b"]);
            if (!old.ContainsKey(a) || !old.ContainsKey(b) || a == b) continue;
            double dx = old[b].X - old[a].X;
            int s = Math.Abs(dx) >= 0.5 ? Math.Sign(dx) : (int)D(e["dir"]) != 0 ? Math.Sign(D(e["dir"])) : Math.Sign(old[Walk(a, e)].X - old[Walk(b, e)].X);
            double len = Math.Max(1, D(e["len"])); if (Szlak(e) || IsLink(e)) len = Math.Min(len, PlanSzlakMax);
            double d = len * PlanUnitsPerM;
            if (s == 0) { dirE.Add((a, b, 0)); dirE.Add((b, a, 0)); continue; }
            var (u, v) = s > 0 ? (a, b) : (b, a);
            dirE.Add((u, v, d)); nb[u].Add((v, d, true)); nb[v].Add((u, d, false));
        }
        // najdłuższa ścieżka (Bellman–Ford): każdy tor co najmniej swojej długości, kolejność zachowana
        var x = ids.ToDictionary(k => k, _ => 0.0);
        bool settled = false;
        for (int pass = 0; pass < ids.Count + 2 && !settled; pass++)
        {
            settled = true;
            foreach (var (u, v, d) in dirE) if (x[v] < x[u] + d - 1e-9) { x[v] = x[u] + d; settled = false; }
        }
        if (!settled) return "✗ sprzeczna kolejność torów na obecnym planie (cykl) – autoukład przerwany, plan bez zmian";
        // składowe spójne – kotwica: węzeł najbardziej na zachód zostaje na swoim X
        var comp = new Dictionary<string, int>();
        foreach (var n in ids)
        {
            if (comp.ContainsKey(n)) continue;
            int c = comp.Count; var q = new Queue<string>(); q.Enqueue(n); comp[n] = c; var members = new List<string>();
            while (q.Count > 0) { var m = q.Dequeue(); members.Add(m); foreach (var (o, _, _) in nb[m]) if (!comp.ContainsKey(o)) { comp[o] = c; q.Enqueue(o); } }
            var an = members.OrderBy(m => old[m].X).First(); double off = old[an].X - x[an];
            foreach (var m in members) x[m] += off;
        }
        // wygładzanie: długości jak najbliżej len, tor nie krótszy niż połowa długości i nigdy odwrócony
        for (int it = 0; it < 4000; it++)
        {
            double maxCh = 0;
            foreach (var n in ids)
            {
                if (nb[n].Count == 0) continue;
                double v = nb[n].Average(t => t.Out ? x[t.O] - t.D : x[t.O] + t.D);
                double lo = double.MinValue, hi = double.MaxValue;
                foreach (var (o, d, outE) in nb[n]) { if (outE) hi = Math.Min(hi, x[o] - Math.Max(1, 0.5 * d)); else lo = Math.Max(lo, x[o] + Math.Max(1, 0.5 * d)); }
                v = lo > hi ? (lo + hi) / 2 : Math.Clamp(v, lo, hi);
                maxCh = Math.Max(maxCh, Math.Abs(v - x[n])); x[n] = v;
            }
            if (maxCh < 0.005) break;
        }

        // ---------------- Y: pasy torów
        var parent = ids.ToDictionary(k => k, k => k);
        string Find(string k) { while (parent[k] != k) k = parent[k] = parent[parent[k]]; return k; }
        var horiz = new HashSet<string>();
        foreach (var e in edges)
        {
            if (IsLink(e)) continue;
            string a = Sv(e["a"]), b = Sv(e["b"]);
            if (!old.ContainsKey(a) || !old.ContainsKey(b) || Math.Abs(old[a].Y - old[b].Y) >= 1) continue;
            parent[Find(a)] = Find(b); horiz.Add(a); horiz.Add(b);
        }
        // pasy osobno dla każdego posterunku (inaczej tory jednej stacji wchodzą między tory drugiej i zostają puste pasy)
        string PostN(string n) => Sv(((JsonObject)Nodes[n])["post"]) ?? "";
        var strands = horiz.GroupBy(Find).ToDictionary(g => g.Key, g => (Y: g.Average(n => old[n].Y), Post: g.GroupBy(PostN).OrderByDescending(q => q.Count()).First().Key));
        var levels = strands.Values.GroupBy(v => v.Post).ToDictionary(g => g.Key, g =>
        {
            var l = new List<double>();
            foreach (var yv in g.Select(v => v.Y).OrderBy(v => v)) if (l.Count == 0 || yv - l[^1] > 4) l.Add(yv);
            return l;
        });
        int Rank(string post, double yv) { var l = levels[post]; int best = 0; for (int i = 1; i < l.Count; i++) if (Math.Abs(l[i] - yv) < Math.Abs(l[best] - yv)) best = i; return best; }
        double Rel(string n) { var st = strands[Find(n)]; return Rank(st.Post, st.Y) * PlanLane; }
        double globalMin = levels.Count > 0 ? levels.Values.Min(l => l[0]) : 0;
        // wyrównanie posterunków: tor łączący dwa posterunki (szlak) leży na jednym poziomie po obu stronach
        var offset = new Dictionary<string, double>();
        if (levels.Count > 0)
        {
            var root = levels.Keys.OrderByDescending(k => strands.Values.Count(v => v.Post == k)).First();
            offset[root] = globalMin;
            var links = edges.Select(e => (A: Sv(e["a"]), B: Sv(e["b"]))).Where(t => horiz.Contains(t.A) && horiz.Contains(t.B) && strands[Find(t.A)].Post != strands[Find(t.B)].Post).ToList();
            bool grew = true;
            while (grew)
            {
                grew = false;
                foreach (var (a2, b2) in links)
                {
                    string pa = strands[Find(a2)].Post, pb = strands[Find(b2)].Post;
                    if (offset.ContainsKey(pa) && !offset.ContainsKey(pb)) { offset[pb] = offset[pa] + Rel(a2) - Rel(b2); grew = true; }
                    else if (offset.ContainsKey(pb) && !offset.ContainsKey(pa)) { offset[pa] = offset[pb] + Rel(b2) - Rel(a2); grew = true; }
                }
            }
            foreach (var k in levels.Keys.Where(k => !offset.ContainsKey(k))) offset[k] = globalMin + (levels[k][0] - globalMin) * 0.25;
        }
        // odwzorowanie Y posterunku (monotoniczne, łamane) dla elementów spoza pasów
        double Fp(string post, double yv)
        {
            if (!levels.TryGetValue(post, out var l)) { if (levels.Count == 0) return yv; post = levels.Keys.OrderBy(k => Math.Abs(levels[k].Average() - yv)).First(); l = levels[post]; }
            double b0 = offset[post];
            if (l.Count < 2) return b0 + (yv - l[0]) * 0.25;
            if (yv <= l[0]) return b0 + (yv - l[0]) * PlanLane / Math.Max(1, l[1] - l[0]);
            for (int i = 1; i < l.Count; i++) if (yv <= l[i]) return b0 + (i - 1 + (yv - l[i - 1]) / (l[i] - l[i - 1])) * PlanLane;
            return b0 + (l.Count - 1) * PlanLane + (yv - l[^1]) * PlanLane / Math.Max(1, l[^1] - l[^2]);
        }
        var y = new Dictionary<string, double>();
        foreach (var n in horiz) y[n] = offset[strands[Find(n)].Post] + Rel(n);
        // węzły łączników (poza pasami): na prostym skosie między końcami łańcucha
        foreach (var n in ids.Where(k => !y.ContainsKey(k)))
        {
            if (ChainN(n))
            {
                var ends = at[n].Select(e => Walk(n, e)).ToList();
                if (ends.All(y.ContainsKey) && Math.Abs(x[ends[1]] - x[ends[0]]) > 1e-6)
                { y[n] = y[ends[0]] + (x[n] - x[ends[0]]) / (x[ends[1]] - x[ends[0]]) * (y[ends[1]] - y[ends[0]]); continue; }
            }
            y[n] = Fp(PostN(n), old[n].Y);
        }

        // ---------------- zapis: węzły, tory jako odcinki proste, elementy planu
        foreach (var n in ids) { var o = (JsonObject)Nodes[n]; o["x"] = Math.Round(x[n], 1); o["y"] = Math.Round(y[n], 1); }
        foreach (var e in edges)
        {
            string a = Sv(e["a"]), b = Sv(e["b"]); if (!x.ContainsKey(a) || !x.ContainsKey(b)) continue;
            e["pts"] = new JsonArray(new JsonArray(Math.Round(x[a], 1), Math.Round(y[a], 1)), new JsonArray(Math.Round(x[b], 1), Math.Round(y[b], 1)));
        }
        // przesunięcia napisów i peronów względem najbliższego węzła – w nowej skali poziomej
        double ox0 = old.Values.Min(p => p.X), ox1 = old.Values.Max(p => p.X), kx = ox1 - ox0 > 1 ? (x.Values.Max() - x.Values.Min()) / (ox1 - ox0) : 1;
        string Nearest(double px, double py) => ids.OrderBy(n => Math.Pow(old[n].X - px, 2) + Math.Pow(old[n].Y - py, 2)).First();
        double FXY(double px, double py) => Fp(PostN(Nearest(px, py)), py);
        double MapX(double px, double py) { var n = Nearest(px, py); return x[n] + (px - old[n].X) * kx; }
        foreach (var l in (JsonArray)Root["labels"]) { var o = (JsonObject)l; double lx = D(o["x"]), lyv = D(o["y"]); o["x"] = Math.Round(MapX(lx, lyv), 1); o["y"] = Math.Round(FXY(lx, lyv), 1); }
        foreach (var p in (JsonArray)Root["platforms"])
        {
            var o = (JsonObject)p; double px = D(o["x"]), py = D(o["y"]), pw = D(o["w"]), ph = D(o["h"]), yc = py + ph / 2;
            double x0 = MapX(px, yc), x1 = MapX(px + pw, yc); double y0 = FXY(px + pw / 2, py), y1 = FXY(px + pw / 2, py + ph);
            o["x"] = Math.Round(Math.Min(x0, x1), 1); o["w"] = Math.Round(Math.Max(10, Math.Abs(x1 - x0)), 1);
            o["y"] = Math.Round(y0, 1); o["h"] = Math.Round(Math.Max(6, y1 - y0), 1);
        }
        foreach (var c in (JsonArray)Root["crossings"]) { var o = (JsonObject)c; double cx = D(o["x"]), c0 = D(o["y0"]), c1 = D(o["y1"]); o["x"] = Math.Round(MapX(cx, (c0 + c1) / 2), 1); o["y0"] = Math.Round(FXY(cx, c0), 1); o["y1"] = Math.Round(FXY(cx, c1), 1); }
        if (Root["dashed"] is JsonArray da)
            foreach (var d in da) { var o = (JsonObject)d; double dy = D(o["y"]), d0 = D(o["x0"]), d1 = D(o["x1"]); o["x0"] = Math.Round(MapX(d0, dy), 1); o["x1"] = Math.Round(MapX(d1, dy), 1); o["y"] = Math.Round(FXY(d0, dy), 1); }

        // ---------------- raport
        double maxErr = 0; string maxE = ""; int flipped = 0;
        foreach (var e in edges)
        {
            string a = Sv(e["a"]), b = Sv(e["b"]); if (!x.ContainsKey(a) || !x.ContainsKey(b) || Szlak(e) || IsLink(e)) continue;
            double geo = Math.Abs(x[b] - x[a]) / PlanUnitsPerM, len = D(e["len"]);
            if (Math.Abs(geo - len) > maxErr) { maxErr = Math.Abs(geo - len); maxE = Sv(e["id"]); }
            double dxo = old[b].X - old[a].X; if (Math.Abs(dxo) >= 0.5 && Math.Sign(dxo) != Math.Sign(x[b] - x[a])) flipped++;
        }
        double w = ids.Count > 0 ? (x.Values.Max() - x.Values.Min()) / PlanUnitsPerM : 0;
        return $"pasy torów {levels.Values.Sum(l => l.Count)} (posterunki {levels.Count}), szerokość planu ok. {w:0} m (szlaki skrócone do {PlanSzlakMax:0} m); największa odchyłka długości {maxErr:0.#} m (tor {maxE}); torów z odwróconym kierunkiem {flipped}";
    }
}

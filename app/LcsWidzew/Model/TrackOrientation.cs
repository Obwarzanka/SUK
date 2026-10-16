using System.Text.Json.Nodes;

namespace LcsWidzew.Model;

/// <summary>
/// Orientacja torów układu: kierunek „+” = od końca A do końca B toru. Tory są zorientowane spójnie – przez zwykły węzeł
/// kierunek się nie zmienia, w zwrotnicy ostrze jest po przeciwnej stronie niż oba odgałęzienia (strony węzła wg geometrii:
/// odgałęzienia zwrotnicy to para torów o najbliższym kierunku). Dzięki temu kierunek jazdy (±1) w komputerze zależności,
/// blokadach i ruchu pociągów wynika z topologii, a nie z „wschodu / zachodu” rysunku – tory mogą biec skośnie, na północ,
/// zawracać łukiem. Sygnalizatory i końce szlaków zapisane jako ±1 znaczą „zgodnie z A→B toru / przeciwnie”.
/// <para>Stary plik (bez „orient”: „ab”) – jednorazowo: tory odwracane tak, by A→B = dawny kierunek „na wschód” (rosnące X
/// albo pole „dir” toru pionowego); zapisane ±1 zachowują znaczenie. Po każdej edycji – normalizacja: tory niezgodne
/// z sąsiednimi (np. narysowane od prawej) są odwracane, a sygnalizatory i szlaki przy nich – przeliczane.</para>
/// </summary>
public static class TrackOrientation
{
    public const string Flag = "ab";

    static string Sv(JsonNode n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
    static double D(JsonNode n) => n is JsonValue v ? (v.TryGetValue<double>(out var d) ? d : v.TryGetValue<int>(out var i) ? i : v.TryGetValue<long>(out var l) ? l : 0) : 0;

    /// <summary>Wynik: liczba odwróconych torów i węzły, w których orientacja jest sprzeczna (trójkąt torowy, pętla zawracająca).</summary>
    public record Result(int Flipped, List<string> Conflicts);

    /// <summary>Węzły z dwoma torami pod kątem ostrzejszym niż 90° (tor zawraca w węźle – zwykle błąd rysunku; przejazdu nie ma).</summary>
    public static List<string> Reversals(JsonObject root)
    {
        var nodes = root["nodes"] as JsonObject; var edges = (root["edges"] as JsonArray)?.OfType<JsonObject>().ToList(); var res = new List<string>();
        if (nodes == null || edges == null) return res;
        (double X, double Y) Pos(string n) => nodes[n] is JsonObject o ? (D(o["x"]), D(o["y"])) : (0, 0);
        (double X, double Y) T(JsonObject e, string n)
        {
            var p = (e["pts"] as JsonArray)?.OfType<JsonArray>().Select(q => (D(q[0]), D(q[1]))).ToList() ?? new() { Pos(Sv(e["a"])), Pos(Sv(e["b"])) };
            if (Sv(e["a"]) != n) p.Reverse();
            for (int i = 1; i < p.Count; i++) { double dx = p[i].Item1 - p[0].Item1, dy = p[i].Item2 - p[0].Item2, l = Math.Sqrt(dx * dx + dy * dy); if (l > 1e-6) return (dx / l, dy / l); }
            return (1, 0);
        }
        foreach (var g in edges.SelectMany(e => new[] { (N: Sv(e["a"]), E: e), (N: Sv(e["b"]), E: e) }).GroupBy(x => x.N))
        {
            var l = g.Select(x => x.E).Distinct().ToList(); if (l.Count != 2) continue;
            var (ax, ay) = T(l[0], g.Key); var (bx, by) = T(l[1], g.Key);
            if (ax * bx + ay * by > 0.1) res.Add(g.Key);
        }
        return res;
    }

    /// <summary>Orientuje tory w pliku (w miejscu). legacy – stary plik: znaczenie ±1 według X (wschód).</summary>
    public static Result Apply(JsonObject root, bool? legacyOverride = null)
    {
        bool legacy = legacyOverride ?? Sv(root["orient"]) != Flag;
        var nodes = root["nodes"] as JsonObject; var edges = (root["edges"] as JsonArray)?.OfType<JsonObject>().ToList();
        if (nodes == null || edges == null) return new(0, new());
        var byId = edges.Where(e => Sv(e["id"]) != null).GroupBy(e => Sv(e["id"])).ToDictionary(g => g.Key, g => g.First());
        var at = new Dictionary<string, List<JsonObject>>();
        foreach (var e in edges)
            foreach (var n in new[] { Sv(e["a"]), Sv(e["b"]) })
                if (n != null) { if (!at.TryGetValue(n, out var l)) at[n] = l = new(); l.Add(e); }
        (double X, double Y) Pos(string n) => nodes[n] is JsonObject o ? (D(o["x"]), D(o["y"])) : (0, 0);
        List<(double X, double Y)> Pts(JsonObject e) => (e["pts"] as JsonArray)?.OfType<JsonArray>().Select(p => (D(p[0]), D(p[1]))).ToList() ?? new() { Pos(Sv(e["a"])), Pos(Sv(e["b"])) };

        // kierunek „bieżący” (A→B) i „dawny” (X / pole dir) toru e od węzła n
        int Cur(JsonObject e, string n) => Sv(e["a"]) == n ? 1 : -1;
        int Old(JsonObject e, string n)
        {
            string a = Sv(e["a"]), b = Sv(e["b"]), o = a == n ? b : a;
            double dx = Pos(o).X - Pos(n).X;
            if (Math.Abs(dx) >= 0.5) return Math.Sign(dx);
            int d = Math.Sign((int)D(e["dir"]));
            return a == n ? d : -d;
        }
        Func<JsonObject, string, int> Sem = legacy ? Old : Cur;      // znaczenie zapisanych ±1

        // strony węzła: styczne torów od węzła; 2 tory – przeciwne strony; 3 – odgałęzienia (najbliższy kierunek) po jednej stronie;
        // więcej – oś z najbardziej przeciwnej pary
        var sideCache = new Dictionary<string, Dictionary<JsonObject, int>>();
        Dictionary<JsonObject, int> Sides(string n)
        {
            if (sideCache.TryGetValue(n, out var c)) return c;
            var l = at.GetValueOrDefault(n) ?? new(); c = new();
            (double X, double Y) T(JsonObject e)
            {
                var p = Pts(e); if (Sv(e["a"]) != n) p.Reverse();
                var o = p[0];
                for (int i = 1; i < p.Count; i++) { double dx = p[i].X - o.X, dy = p[i].Y - o.Y, len = Math.Sqrt(dx * dx + dy * dy); if (len > 1e-6) return (dx / len, dy / len); }
                return (1, 0);
            }
            var t = l.ToDictionary(e => e, T);
            double Dot(JsonObject a, JsonObject b) => t[a].X * t[b].X + t[a].Y * t[b].Y;
            // 2 tory: przejście przez węzeł; tory pod kątem ostrzejszym niż 90° – zawrócenie w węźle (błąd rysunku), bez przejazdu (jak dawniej)
            if (l.Count == 2) { c[l[0]] = 1; c[l[1]] = Dot(l[0], l[1]) > 0.1 ? 1 : -1; }
            else if (l.Count == 3)
            {
                var pairs = new[] { (0, 1, 2), (0, 2, 1), (1, 2, 0) };
                var (i, j, k) = pairs.OrderByDescending(q => Dot(l[q.Item1], l[q.Item2])).First();
                c[l[i]] = 1; c[l[j]] = 1; c[l[k]] = -1;
            }
            else if (l.Count > 3)
            {
                JsonObject ax = l[0]; double best = 2;
                for (int i = 0; i < l.Count; i++) for (int j = i + 1; j < l.Count; j++) if (Dot(l[i], l[j]) < best) { best = Dot(l[i], l[j]); ax = l[i]; }
                foreach (var e in l) c[e] = e == ax || Dot(e, ax) > 0 ? 1 : -1;
            }
            else foreach (var e in l) c[e] = 1;
            return sideCache[n] = c;
        }

        // o[e]: +1 – zostaje, −1 – odwrócić
        var o = new Dictionary<JsonObject, int>(); var conflicts = new List<string>();
        var q = new Queue<JsonObject>();
        void Propagate()
        {
            while (q.Count > 0)
            {
                var e = q.Dequeue();
                foreach (var n in new[] { Sv(e["a"]), Sv(e["b"]) })
                {
                    if (n == null || !at.ContainsKey(n)) continue;
                    var sd = Sides(n); int de = o[e] * Cur(e, n);       // kierunek „+” toru e od węzła n po orientacji
                    foreach (var f in at[n])
                    {
                        if (f == e) continue;
                        int want = sd[f] == sd[e] ? de : -de;            // ta sama strona – ten sam kierunek od węzła
                        int of = want * Cur(f, n);
                        if (!o.TryGetValue(f, out var have)) { o[f] = of; q.Enqueue(f); }
                        else if (have != of && !conflicts.Contains(n)) conflicts.Add(n);
                    }
                }
            }
        }
        if (legacy)
        {
            // znane z X / pola dir – dokładnie dawne znaczenie; nieznane (tory pionowe bez dir) – z sąsiadów
            foreach (var e in edges) { int d = Old(e, Sv(e["a"])); if (d != 0) o[e] = d; }
            var known = o.Keys.ToList(); var tmp = new Dictionary<JsonObject, int>(o);
            // propagacja tylko do nieznanych: sprzeczności między znanymi nie są poprawiane (dawny układ tak działał)
            foreach (var e in known) q.Enqueue(e);
            while (q.Count > 0)
            {
                var e = q.Dequeue();
                foreach (var n in new[] { Sv(e["a"]), Sv(e["b"]) })
                {
                    if (n == null || !at.ContainsKey(n)) continue;
                    var sd = Sides(n); int de = o[e] * Cur(e, n);
                    foreach (var f in at[n]) { if (f == e || o.ContainsKey(f)) continue; o[f] = (sd[f] == sd[e] ? de : -de) * Cur(f, n); q.Enqueue(f); }
                }
            }
            foreach (var e in edges) if (!o.ContainsKey(e)) o[e] = 1;
            // kontrola sprzeczności po migracji
            foreach (var (n, l) in at)
            {
                var sd = Sides(n);
                for (int i = 0; i < l.Count; i++) for (int j = i + 1; j < l.Count; j++)
                {
                    int di = o[l[i]] * Cur(l[i], n), dj = o[l[j]] * Cur(l[j], n);
                    if ((sd[l[i]] == sd[l[j]]) != (di == dj) && !conflicts.Contains(n)) conflicts.Add(n);
                }
            }
        }
        else
        {
            // od najstarszych torów (kolejność w pliku) – istniejąca orientacja zostaje, nowe tory dopasowują się do niej
            foreach (var e in edges) { if (o.ContainsKey(e)) continue; o[e] = 1; q.Enqueue(e); Propagate(); }
        }

        int flips = o.Count(x => x.Value < 0);
        if (flips == 0 && !legacy) return new(0, conflicts);

        // zapisane ±1 przed zmianą → tor „przed” sygnalizatorem / tor szlaku przy granicy
        var signals = root["signals"] as JsonObject; var blocks = root["blocks"] as JsonObject;
        var sigAhead = new List<(JsonObject S, JsonObject E, bool Back)>();
        if (signals != null)
            foreach (var (_, sv) in signals)
            {
                if (sv is not JsonObject s || Sv(s["node"]) is not string n || !at.ContainsKey(n)) continue;
                int d = Math.Sign((int)D(s["dir"])); if (d == 0) continue;
                var ah = at[n].FirstOrDefault(e => Sem(e, n) == d);
                if (ah != null) sigAhead.Add((s, ah, false));
                else if (at[n].FirstOrDefault(e => Sem(e, n) == -d) is { } bk) sigAhead.Add((s, bk, true));
            }
        var endEdge = new List<(JsonObject End, JsonObject E)>(); var defEnd = new List<(JsonObject B, int Idx)>();
        if (blocks != null)
            foreach (var (_, bv) in blocks)
            {
                if (bv is not JsonObject b || b["ends"] is not JsonArray ends) continue;
                int dd = Math.Sign((int)D(b["defDir"])), idx = -1, k = 0;
                foreach (var en in ends.OfType<JsonObject>())
                {
                    int ow = Math.Sign((int)D(en["outward"]));
                    if (dd != 0 && ow == dd && idx < 0) idx = k;
                    if (Sv(en["boundary"]) is string n && at.ContainsKey(n) && at[n].FirstOrDefault(e => Sem(e, n) == ow) is { } oe) endEdge.Add((en, oe));
                    k++;
                }
                defEnd.Add((b, idx));
            }

        // odwrócenie torów: końce, punkty, punkty zobrazowania, pole dir
        var dispEdges = (root["display"] as JsonObject)?["edges"] as JsonObject;
        foreach (var (e, v) in o)
        {
            if (v > 0) continue;
            var a = e["a"]?.DeepClone(); e["a"] = e["b"]?.DeepClone(); e["b"] = a;
            if (e["pts"] is JsonArray pa) e["pts"] = new JsonArray(pa.Reverse().Select(p => p?.DeepClone()).ToArray());
            if (dispEdges != null && Sv(e["id"]) is string id && dispEdges[id] is JsonArray da) dispEdges[id] = new JsonArray(da.Reverse().Select(p => p?.DeepClone()).ToArray());
            if (e["dir"] != null) { int d = (int)D(e["dir"]); if (d != 0) e["dir"] = -d; }
        }
        // ±1 po zmianie: względem A→B
        foreach (var (s, e, back) in sigAhead) { var n = Sv(s["node"]); int d = Cur(e, n); s["dir"] = back ? -d : d; }
        foreach (var (en, e) in endEdge) en["outward"] = Cur(e, Sv(en["boundary"]));
        foreach (var (b, idx) in defEnd)
            if (idx >= 0 && b["ends"] is JsonArray ends && ends[idx] is JsonObject en) b["defDir"] = (int)D(en["outward"]);
        root["orient"] = Flag;
        return new(flips, conflicts);
    }
}

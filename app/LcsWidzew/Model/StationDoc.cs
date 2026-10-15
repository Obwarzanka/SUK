using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LcsWidzew.Model;

/// <summary>
/// Edytowalny dokument obszaru LCS (Data/station.json) – operacje edytora układu torowego.
/// Każda zmiana działa bezpośrednio na drzewie JSON; Station powstaje z niego przez Station.Parse.
/// </summary>
public class StationDoc
{
    public JsonObject Root;
    readonly Stack<string> undo = new(), redo = new();

    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() };

    public StationDoc(JsonObject root) { Root = root; Ensure(); }
    public static StationDoc Load(string path) => new((JsonObject)JsonNode.Parse(File.ReadAllText(path)));
    public string Json() => Root.ToJsonString(Opt);
    /// <summary>Zapis z kopią poprzedniej wersji: plik .bak oraz kopie z datą w katalogu „kopie” (ostatnie 30).</summary>
    public void Save(string path)
    {
        if (File.Exists(path))
        {
            File.Copy(path, path + ".bak", true);
            var dir = Path.Combine(Path.GetDirectoryName(path) ?? ".", "kopie");
            Directory.CreateDirectory(dir);
            File.Copy(path, Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(path)}_{DateTime.Now:yyyyMMdd_HHmmss}.json"), true);
            foreach (var old in Directory.GetFiles(dir, "*.json").OrderByDescending(File.GetLastWriteTime).Skip(30)) File.Delete(old);
        }
        File.WriteAllText(path, Json());
    }

    public static string BackupDir(string path) => Path.Combine(Path.GetDirectoryName(path) ?? ".", "kopie");
    public Station Build() => Station.Parse(Json());

    /// <summary>Nowy, pusty obszar z jednym posterunkiem i jednym obrazem.</summary>
    public static StationDoc New(string post, string name)
    {
        var r = new JsonObject
        {
            ["name"] = name, ["code"] = post, ["width"] = 2000, ["height"] = 1125, ["source"] = "edytor",
            ["pages"] = new JsonArray(new JsonObject { ["id"] = post, ["name"] = name, ["x0"] = 0, ["x1"] = 2000, ["posts"] = new JsonArray(post) }),
            ["posts"] = new JsonObject { [post] = name }
        };
        return new StationDoc(r);
    }

    void Ensure()
    {
        foreach (var k in new[] { "nodes", "switches", "signals", "derailers", "blocks", "boundaries", "farEnds", "trackEnds", "posts" })
            if (Root[k] is not JsonObject) Root[k] = new JsonObject();
        foreach (var k in new[] { "edges", "platforms", "labels", "crossings", "dashed", "pages" })
            if (Root[k] is not JsonArray) Root[k] = new JsonArray();
    }

    public JsonObject Nodes => (JsonObject)Root["nodes"];
    public JsonArray Edges => (JsonArray)Root["edges"];
    public JsonObject Switches => (JsonObject)Root["switches"];
    public JsonObject Signals => (JsonObject)Root["signals"];
    public JsonObject Derailers => (JsonObject)Root["derailers"];
    public JsonObject Blocks => (JsonObject)Root["blocks"];
    public JsonObject Boundaries => (JsonObject)Root["boundaries"];
    public JsonObject FarEnds => (JsonObject)Root["farEnds"];
    public JsonObject TrackEnds => (JsonObject)Root["trackEnds"];
    public JsonObject Posts => (JsonObject)Root["posts"];
    public JsonArray Pages => (JsonArray)Root["pages"];
    public JsonArray Labels => (JsonArray)Root["labels"];
    public JsonArray Platforms => (JsonArray)Root["platforms"];

    // ------------------------------------------------------------------ cofanie
    public void Snapshot() { undo.Push(Root.ToJsonString()); redo.Clear(); if (undo.Count > 200) { var keep = undo.Take(150).Reverse().ToList(); undo.Clear(); foreach (var k in keep) undo.Push(k); } }
    public bool Undo() { if (undo.Count == 0) return false; redo.Push(Root.ToJsonString()); Root = (JsonObject)JsonNode.Parse(undo.Pop()); return true; }
    public bool Redo() { if (redo.Count == 0) return false; undo.Push(Root.ToJsonString()); Root = (JsonObject)JsonNode.Parse(redo.Pop()); return true; }

    // ------------------------------------------------------------------ pomocnicze
    public static double D(JsonNode n) => n == null ? 0 : double.Parse(n.ToJsonString(), System.Globalization.CultureInfo.InvariantCulture);
    public static string Sv(JsonNode n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
    public (double X, double Y) Pos(string nid) { var n = Nodes[nid]; return (D(n["x"]), D(n["y"])); }
    public IEnumerable<JsonObject> EdgeObjs => Edges.Select(e => (JsonObject)e);
    public JsonObject Edge(string id) => EdgeObjs.FirstOrDefault(e => Sv(e["id"]) == id);
    public List<JsonObject> EdgesAt(string nid) => EdgeObjs.Where(e => Sv(e["a"]) == nid || Sv(e["b"]) == nid).ToList();

    public string NewNodeId(string post)
    {
        int k = 1; string id;
        do id = $"{post}:n{k++}"; while (Nodes.ContainsKey(id));
        return id;
    }
    public string NewEdgeId()
    {
        var used = EdgeObjs.Select(e => Sv(e["id"])).ToHashSet();
        int k = EdgeObjs.Count(); string id;
        do id = "e" + k++; while (used.Contains(id));
        return id;
    }
    /// <summary>Nowy odcinek: „o…” – nadany automatycznie przy rysowaniu, „it…” – wydzielony celowo przez użytkownika.</summary>
    public string NewSecId(string post, string prefix = "o")
    {
        var used = EdgeObjs.Select(e => Sv(e["sec"])).ToHashSet();
        int k = 1; string id;
        do id = $"{post}:{prefix}{k++}"; while (used.Contains(id));
        return id;
    }

    static JsonArray Pt(double x, double y) => new(x, y);
    static List<(double X, double Y)> Pts(JsonObject e) => ((JsonArray)e["pts"]).Select(p => (D(p[0]), D(p[1]))).ToList();
    static void SetPts(JsonObject e, List<(double X, double Y)> pts)
    {
        var a = new JsonArray(); foreach (var p in pts) a.Add(Pt(Math.Round(p.X, 1), Math.Round(p.Y, 1))); e["pts"] = a;
    }
    static double GeoLen(List<(double X, double Y)> p) { double s = 0; for (int i = 0; i < p.Count - 1; i++) s += Math.Sqrt(Math.Pow(p[i + 1].X - p[i].X, 2) + Math.Pow(p[i + 1].Y - p[i].Y, 2)); return s; }
    static bool IsSzlak(JsonObject e) => e["szlak"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    // ------------------------------------------------------------------ węzły i krawędzie
    public string AddNode(string post, double x, double y)
    {
        var id = NewNodeId(post);
        Nodes[id] = new JsonObject { ["x"] = x, ["y"] = y, ["kind"] = "", ["ref"] = "", ["post"] = post };
        return id;
    }

    /// <summary>Kierunek jazdy przy przejściu toru od węzła from (1 – wschód, -1 – zachód, 0 – nieokreślony tor pionowy).</summary>
    public int EdgeDir(JsonObject e, string from)
    {
        string a = Sv(e["a"]), b = Sv(e["b"]), o = a == from ? b : a;
        var dx = Pos(o).X - Pos(from).X;
        if (Math.Abs(dx) >= 0.5) return Math.Sign(dx);
        int d = e["dir"] == null ? 0 : (int)D(e["dir"]);
        return a == from ? d : -d;
    }

    /// <summary>Tor, który staje się pionowy, zachowuje dotychczasowy kierunek jazdy (zapis „dir”).</summary>
    void KeepDir(JsonObject e, double oldAx, double oldBx)
    {
        var (ax, _) = Pos(Sv(e["a"])); var (bx, _) = Pos(Sv(e["b"]));
        if (Math.Abs(bx - ax) < 0.5 && e["dir"] == null && Math.Abs(oldBx - oldAx) >= 0.5) e["dir"] = Math.Sign(oldBx - oldAx);
    }

    public void MoveNode(string nid, double x, double y)
    {
        var n = Nodes[nid]; var (ox, oy) = (D(n["x"]), D(n["y"]));
        var oldX = EdgesAt(nid).ToDictionary(e => e, e => (A: Pos(Sv(e["a"])).X, B: Pos(Sv(e["b"])).X));
        n["x"] = x; n["y"] = y;
        foreach (var e in EdgesAt(nid))
        {
            var p = Pts(e); bool atA = Sv(e["a"]) == nid;
            int i = atA ? 0 : p.Count - 1, j = atA ? 1 : p.Count - 2;
            if (p.Count > 2)
            {
                // sąsiedni punkt załamania przesuwa się razem z węzłem po tej osi, po której był z nim wyrównany
                var q = p[j];
                if (Math.Abs(q.Y - oy) < 0.5) q.Y = y;
                if (Math.Abs(q.X - ox) < 0.5) q.X = x;
                p[j] = q;
            }
            p[i] = (x, y);
            SetPts(e, p);
            if (!IsSzlak(e) && !IsLink(e)) e["len"] = Math.Round(Math.Max(5, GeoLen(p)), 1);
        }
        foreach (var (e, o) in oldX) KeepDir(e, o.A, o.B);
    }

    /// <summary>Przesunięcie grupy węzłów: tory w całości wewnątrz grupy są przesuwane razem z załamaniami, tory na granicy – tylko końcem.</summary>
    public void MoveGroup(ICollection<string> group, double dx, double dy)
    {
        var set = group.Where(Nodes.ContainsKey).ToHashSet();
        var orig = set.ToDictionary(n => n, Pos);
        var oldX = EdgeObjs.Where(e => set.Contains(Sv(e["a"])) ^ set.Contains(Sv(e["b"]))).ToDictionary(e => e, e => (A: Pos(Sv(e["a"])).X, B: Pos(Sv(e["b"])).X));
        foreach (var n in set) { Nodes[n]["x"] = orig[n].X + dx; Nodes[n]["y"] = orig[n].Y + dy; }
        foreach (var e in EdgeObjs)
        {
            string a = Sv(e["a"]), b = Sv(e["b"]);
            bool ia = set.Contains(a), ib = set.Contains(b);
            if (!ia && !ib) continue;
            var p = Pts(e);
            if (ia && ib) { SetPts(e, p.Select(q => (q.X + dx, q.Y + dy)).ToList()); continue; }
            var nid = ia ? a : b; var (ox, oy) = orig[nid]; double x = ox + dx, y = oy + dy;
            int i = ia ? 0 : p.Count - 1, j = ia ? 1 : p.Count - 2;
            if (p.Count > 2)
            {
                // narożnik łącznika przy przesuwanym torze (ukos 45°) przesuwa się razem z nim – łącznik skraca / wydłuża się w części prostej
                for (int k = 1; k < p.Count - 1; k++)
                {
                    var q = p[k];
                    if (dx == 0 && Math.Abs(q.Y - oy) <= 8.5) q.Y += dy;
                    else if (dy == 0 && Math.Abs(q.X - ox) <= 8.5) q.X += dx;
                    else if (k == j) { if (Math.Abs(q.Y - oy) < 0.5) q.Y = y; if (Math.Abs(q.X - ox) < 0.5) q.X = x; }
                    p[k] = q;
                }
            }
            p[i] = (x, y); SetPts(e, p);
            if (!IsSzlak(e)) e["len"] = Math.Round(Math.Max(5, GeoLen(p)), 1);
        }
        foreach (var (e, o) in oldX) KeepDir(e, o.A, o.B);
    }

    /// <summary>Węzły toru poziomego, do którego należy krawędź (ciąg torów poziomych na tej samej wysokości, także przez zwrotnice).</summary>
    public HashSet<string> TrackLine(string eid)
    {
        bool Horizontal(JsonObject e) { var p = Pts(e); return p.All(q => Math.Abs(q.Y - p[0].Y) < 0.5); }
        var e0 = Edge(eid); var res = new HashSet<string>();
        if (e0 == null || !Horizontal(e0)) return res;
        var y = Pts(e0)[0].Y; var q2 = new Queue<string>(new[] { Sv(e0["a"]), Sv(e0["b"]) });
        while (q2.Count > 0)
        {
            var n = q2.Dequeue(); if (!res.Add(n)) continue;
            foreach (var e in EdgesAt(n))
                if (Horizontal(e) && Math.Abs(Pts(e)[0].Y - y) < 0.5) { var o = Sv(e["a"]) == n ? Sv(e["b"]) : Sv(e["a"]); if (!res.Contains(o)) q2.Enqueue(o); }
        }
        return res;
    }

    /// <summary>
    /// Rozstaw torów w pionie: poziomy torów w grupie dostają równy odstęp (górny tor zostaje na miejscu).
    /// Narożniki łączników (do 8,5 od toru) zachowują ukos 45°, części pionowe skracają się / wydłużają.
    /// </summary>
    public int RespaceY(ICollection<string> group, double spacing, List<JsonObject> labels = null, List<JsonObject> platforms = null)
    {
        if (spacing < 18) throw new InvalidOperationException("Odstęp torów co najmniej 18 (miejsce na ukosy łączników)");
        var set = group.Where(Nodes.ContainsKey).ToHashSet();
        bool Horizontal(JsonObject e) { var p = Pts(e); return p.All(q => Math.Abs(q.Y - p[0].Y) < 0.5); }
        var levels = EdgeObjs.Where(e => set.Contains(Sv(e["a"])) && set.Contains(Sv(e["b"])) && Horizontal(e)).Select(e => Math.Round(Pts(e)[0].Y * 2) / 2).Distinct().OrderBy(v => v).ToList();
        // poziomy bliższe niż 17 (krótkie odcinki poziome w łącznikach) należą do toru powyżej
        var merged = new List<double>(); foreach (var v in levels) if (merged.Count == 0 || v - merged[^1] >= 17) merged.Add(v);
        levels = merged;
        if (levels.Count < 2) throw new InvalidOperationException("Zaznacz co najmniej dwa tory poziome");
        var nl = levels.Select((v, i) => levels[0] + i * spacing).ToList();
        double F(double y)
        {
            for (int k = 0; k < levels.Count; k++) if (Math.Abs(y - levels[k]) <= 8.5) return nl[k] + (y - levels[k]);
            if (y < levels[0]) return y + nl[0] - levels[0];
            if (y > levels[^1]) return y + nl[^1] - levels[^1];
            int i = levels.FindLastIndex(v => v < y);
            double a0 = levels[i] + 8.5, a1 = levels[i + 1] - 8.5, b0 = nl[i] + 8.5, b1 = nl[i + 1] - 8.5;
            return a1 > a0 ? b0 + (y - a0) * (b1 - b0) / (a1 - a0) : b0;
        }
        var orig = set.ToDictionary(n => n, Pos);
        var oldX = EdgeObjs.Where(e => set.Contains(Sv(e["a"])) || set.Contains(Sv(e["b"]))).ToDictionary(e => e, e => (A: Pos(Sv(e["a"])).X, B: Pos(Sv(e["b"])).X));
        foreach (var n in set) Nodes[n]["y"] = Math.Round(F(orig[n].Y), 1);
        foreach (var e in EdgeObjs)
        {
            string a = Sv(e["a"]), b = Sv(e["b"]); bool ia = set.Contains(a), ib = set.Contains(b);
            if (!ia && !ib) continue;
            var p = Pts(e);
            if (ia && ib) p = p.Select(q => (q.X, F(q.Y))).ToList();
            else
            {
                var nid = ia ? a : b; var oy = orig[nid].Y; var dy = F(oy) - oy;
                for (int k = 0; k < p.Count; k++) if ((k == 0 && ia) || (k == p.Count - 1 && ib) || (k > 0 && k < p.Count - 1 && Math.Abs(p[k].Y - oy) <= 8.5)) p[k] = (p[k].X, p[k].Y + dy);
            }
            SetPts(e, p);
            if (!IsSzlak(e)) e["len"] = Math.Round(Math.Max(5, GeoLen(p)), 1);
        }
        foreach (var (e, o) in oldX) KeepDir(e, o.A, o.B);
        foreach (var l in labels ?? new()) l["y"] = Math.Round(F(D(l["y"])), 1);
        foreach (var pl in platforms ?? new()) { var cy = D(pl["y"]) + D(pl["h"]) / 2; pl["y"] = Math.Round(F(cy) - D(pl["h"]) / 2, 1); }
        return levels.Count;
    }

    /// <summary>Najkrótsza droga po torach między węzłami [m] (miarka edytora).</summary>
    public double? TrackDistance(string from, string to)
    {
        var dist = new Dictionary<string, double> { [from] = 0 }; var done = new HashSet<string>();
        var pq = new PriorityQueue<string, double>(); pq.Enqueue(from, 0);
        while (pq.TryDequeue(out var n, out var d))
        {
            if (!done.Add(n)) continue;
            if (n == to) return d;
            foreach (var e in EdgesAt(n))
            {
                var o = Sv(e["a"]) == n ? Sv(e["b"]) : Sv(e["a"]);
                var nd = d + D(e["len"]);
                if (!dist.TryGetValue(o, out var od) || nd < od) { dist[o] = nd; pq.Enqueue(o, nd); }
            }
        }
        return null;
    }

    public string AddEdge(string a, string b, string sec, string post)
    {
        var (ax, ay) = Pos(a); var (bx, by) = Pos(b);
        var e = new JsonObject
        {
            ["a"] = a, ["b"] = b, ["pts"] = new JsonArray(Pt(ax, ay), Pt(bx, by)), ["sec"] = sec ?? NewSecId(post),
            ["len"] = Math.Round(Math.Max(5, Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay))), 1), ["track"] = "", ["line"] = "", ["id"] = NewEdgeId()
        };
        Edges.Add(e);
        return Sv(e["id"]);
    }

    /// <summary>Podział krawędzi w punkcie (rzut na odcinek łamanej) – nowy węzeł dziedziczy odcinek izolowany, tor i linię.</summary>
    public string SplitEdge(string eid, double x, double y, string post)
    {
        var e = Edge(eid); var p = Pts(e);
        int best = 0; double bd = double.MaxValue; (double X, double Y) bp = p[0];
        for (int i = 0; i < p.Count - 1; i++)
        {
            double dx = p[i + 1].X - p[i].X, dy = p[i + 1].Y - p[i].Y, l2 = dx * dx + dy * dy;
            double t = l2 == 0 ? 0 : Math.Clamp(((x - p[i].X) * dx + (y - p[i].Y) * dy) / l2, 0.05, 0.95);
            var q = (X: p[i].X + t * dx, Y: p[i].Y + t * dy);
            var d = Math.Sqrt((q.X - x) * (q.X - x) + (q.Y - y) * (q.Y - y));
            if (d < bd) { bd = d; best = i; bp = q; }
        }
        bp = (Math.Round(bp.X, 1), Math.Round(bp.Y, 1));
        var nid = AddNode(post ?? Sv(Nodes[Sv(e["a"])]["post"]) ?? "", bp.X, bp.Y);
        var p1 = p.Take(best + 1).Append(bp).ToList();
        var p2 = new[] { bp }.Concat(p.Skip(best + 1)).ToList();
        double l1 = GeoLen(p1), l2g = GeoLen(p2), len = D(e["len"]);
        var e2 = (JsonObject)e.DeepClone();
        e2["id"] = NewEdgeId(); e2["a"] = nid; SetPts(e2, p2);
        e["b"] = nid; SetPts(e, p1);
        e["len"] = Math.Round(Math.Max(5, len * l1 / Math.Max(1, l1 + l2g)), 1);
        e2["len"] = Math.Round(Math.Max(5, len * l2g / Math.Max(1, l1 + l2g)), 1);
        Edges.Add(e2);
        return nid;
    }

    /// <summary>Czy węzeł jest zwykłym punktem na torze (2 tory, bez zwrotnicy, sygnalizatora, kozła, wykolejnicy, granicy szlaku).</summary>
    public bool IsPlainNode(string nid) =>
        Nodes.ContainsKey(nid) && EdgesAt(nid).Count == 2 && !Switches.Any(x => Sv(x.Value["node"]) == nid) && !Signals.Any(x => Sv(x.Value["node"]) == nid)
        && !Derailers.Any(x => Sv(x.Value["node"]) == nid) && !TrackEnds.ContainsKey(nid) && !Boundaries.ContainsKey(nid) && !FarEnds.ContainsKey(nid);

    /// <summary>Zamiana węzła na zwykły tor: dwa sąsiednie tory łączą się w jeden (długości się sumują, odcinek – z dłuższego).</summary>
    public void MergeNode(string nid)
    {
        if (!IsPlainNode(nid)) throw new InvalidOperationException("Tor można scalić tylko w zwykłym węźle z dwoma torami (bez zwrotnicy, sygnalizatora, kozła, wykolejnicy i granicy szlaku)");
        var ed = EdgesAt(nid); JsonObject e1 = ed[0], e2 = ed[1];
        string o1 = Sv(e1["a"]) == nid ? Sv(e1["b"]) : Sv(e1["a"]), o2 = Sv(e2["a"]) == nid ? Sv(e2["b"]) : Sv(e2["a"]);
        if (o1 == o2) throw new InvalidOperationException("Scalenie dałoby pętlę");
        int d1 = EdgeDir(e1, o1), d2 = EdgeDir(e2, nid);
        if (d1 != 0 && d2 != 0 && d1 != d2) throw new InvalidOperationException("Węzeł jest punktem zawrócenia toru (tor wraca w przeciwnym kierunku) – nie można go scalić");
        var p1 = Pts(e1); if (Sv(e1["a"]) != o1) p1.Reverse();
        var p2 = Pts(e2); if (Sv(e2["a"]) != nid) p2.Reverse();
        var pts = p1.Concat(p2.Skip(1)).ToList();
        // usunięcie zbędnych punktów leżących na prostej
        for (int i = pts.Count - 2; i >= 1; i--)
        {
            var (a, b, c) = (pts[i - 1], pts[i], pts[i + 1]);
            if (Math.Abs((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X)) < 0.5) pts.RemoveAt(i);
        }
        var keep = D(e1["len"]) >= D(e2["len"]) ? e1 : e2;
        var m = (JsonObject)keep.DeepClone();
        m["a"] = o1; m["b"] = o2; SetPts(m, pts); m["len"] = Math.Round(D(e1["len"]) + D(e2["len"]), 1); m.Remove("dir");
        int dir = d1 != 0 ? d1 : d2;
        if (Math.Abs(Pos(o1).X - Pos(o2).X) < 0.5 && dir != 0) m["dir"] = dir;
        if (string.IsNullOrEmpty(Sv(m["track"]))) m["track"] = Sv(e1["track"]) is { Length: > 0 } t1 ? t1 : Sv(e2["track"]) ?? "";
        Edges.Remove(e1); Edges.Remove(e2); Edges.Add(m);
        Nodes.Remove(nid);
    }

    /// <summary>
    /// Przypadkowe przerwy izolacyjne: w zwykłym węźle na prostym torze stykają się dwa odcinki, z których co najmniej jeden
    /// jest odcinkiem nadanym automatycznie przez edytor („…:o<n>”) – łączy je w jeden (odcinek nazwany ma pierwszeństwo).
    /// </summary>
    public int JoinStraySections()
    {
        static bool Auto(string sec) { var l = sec?.Split(':').Last() ?? ""; return l.Length > 1 && l[0] == 'o' && l.Skip(1).All(char.IsDigit); }
        int n = 0;
        for (bool again = true; again;)
        {
            again = false;
            foreach (var nid in Nodes.Select(x => x.Key).ToList())
            {
                if (!IsPlainNode(nid)) continue;
                var ed = EdgesAt(nid); string s1 = Sv(ed[0]["sec"]), s2 = Sv(ed[1]["sec"]);
                if (s1 == s2 || IsSzlak(ed[0]) || IsSzlak(ed[1]) || (!Auto(s1) && !Auto(s2))) continue;
                if (EdgeDir(ed[0], nid) == EdgeDir(ed[1], nid)) continue;   // nie na zawróceniu
                string keep = Auto(s1) ? s2 : s1, drop = keep == s1 ? s2 : s1;
                foreach (var e in EdgeObjs.Where(x => Sv(x["sec"]) == drop)) e["sec"] = keep;
                n++; again = true;
            }
        }
        return n;
    }

    /// <summary>Upraszczanie: scala wszystkie zwykłe węzły leżące na prostej między torami tego samego odcinka.</summary>
    public int Simplify(ICollection<string> only = null)
    {
        int n = 0;
        // węzły bez torów (pozostałości po usuwaniu)
        foreach (var k in Nodes.Select(x => x.Key).ToList())
            if (!EdgesAt(k).Any() && !Signals.Any(x => Sv(x.Value["node"]) == k) && !TrackEnds.ContainsKey(k)) { Nodes.Remove(k); n++; }
        foreach (var nid in (only ?? Nodes.Select(x => x.Key).ToList()).ToList())
        {
            if (!IsPlainNode(nid)) continue;
            var ed = EdgesAt(nid);
            if (Sv(ed[0]["sec"]) != Sv(ed[1]["sec"]) || IsSzlak(ed[0]) != IsSzlak(ed[1])) continue;
            string o1 = Sv(ed[0]["a"]) == nid ? Sv(ed[0]["b"]) : Sv(ed[0]["a"]), o2 = Sv(ed[1]["a"]) == nid ? Sv(ed[1]["b"]) : Sv(ed[1]["a"]);
            var a = Pos(o1); var b = Pos(nid); var c = Pos(o2);
            var p1 = Pts(ed[0]); var p2 = Pts(ed[1]);
            if (p1.Count != 2 || p2.Count != 2) continue;
            if (Math.Abs((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X)) > 0.5) continue;
            try { MergeNode(nid); n++; } catch (InvalidOperationException) { }
        }
        return n;
    }

    public static bool IsLink(JsonObject e) => e["link"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    /// <summary>
    /// Łącznik obrazów: logiczne połączenie dwóch końców torów (zwykle na różnych obrazach). Tor dziedziczy odcinek
    /// (np. odstęp szlaku), długość podaje się w metrach. Kierunek jazdy wynika z kolejności obrazów (wschód – w prawo).
    /// </summary>
    public string AddLink(string a, string b, double len)
    {
        if (a == b) throw new InvalidOperationException("Wskaż dwa różne końce torów");
        if (EdgesAt(a).Count != 1 || EdgesAt(b).Count != 1) throw new InvalidOperationException("Łącznik łączy końce torów (węzły z jednym torem)");
        if (TrackEnds.ContainsKey(a) || TrackEnds.ContainsKey(b)) throw new InvalidOperationException("Na końcu toru jest kozioł – usuń go przed połączeniem");
        if (Math.Abs(Pos(a).X - Pos(b).X) < 1) throw new InvalidOperationException("Końce muszą leżeć na różnych wysokościach osi X (obrazy obok siebie)");
        var ea = EdgesAt(a)[0]; var eb = EdgesAt(b)[0];
        var eid = AddEdge(a, b, Sv(ea["sec"]), Sv(Nodes[a]["post"]) ?? "");
        var e = Edge(eid); e["link"] = true; e["len"] = Math.Max(5, len);
        if (IsSzlak(ea) || IsSzlak(eb)) e["szlak"] = true;
        return eid;
    }

    /// <summary>
    /// Łącznik rozjazdowy w kształcie z pulpitu: krótki ukos 45° od toru, odcinek pionowy, krótki ukos 45° do drugiego toru.
    /// a – węzeł na pierwszym torze (zwrotnica), toEdge – tor docelowy (zwrotnica w odległości 2·k w poziomie),
    /// s – kierunek w poziomie (+1 – w prawo). Zwraca węzeł zwrotnicy na torze docelowym.
    /// </summary>
    public string AddCrossover(string a, string toEdge, int s, double k, string post)
    {
        var (ax, ay) = Pos(a);
        var tp = Pts(Edge(toEdge)); double by = tp[0].Y;
        if (Math.Abs(by - ay) < 2 * k + 2) throw new InvalidOperationException("Tory za blisko siebie na łącznik (min. " + (2 * k + 2) + ")");
        int v = Math.Sign(by - ay);
        var b = SplitEdge(toEdge, ax + s * 2 * k, by, post);
        var (bx, _) = Pos(b);
        double mx = ax + s * k;
        var j = AddNode(post, mx, (ay + by) / 2);
        var e1 = new JsonObject { ["a"] = a, ["b"] = j, ["sec"] = NewSecId(post), ["track"] = "", ["line"] = "", ["id"] = NewEdgeId() };
        SetPts(e1, new() { (ax, ay), (mx, ay + v * k), (mx, (ay + by) / 2) });
        Edges.Add(e1);
        var e2 = new JsonObject { ["a"] = j, ["b"] = b, ["sec"] = Sv(e1["sec"]), ["track"] = "", ["line"] = "", ["id"] = NewEdgeId() };
        SetPts(e2, new() { (mx, (ay + by) / 2), (mx, by - v * k), (bx, by) });
        Edges.Add(e2);
        foreach (var e in new[] { e1, e2 }) e["len"] = Math.Round(Math.Max(5, GeoLen(Pts(e))), 1);
        return b;
    }

    /// <summary>Odgałęzienie: z węzła na torze krótki ukos, pion do wysokości y, ukos i nowy tor poziomy do x końca.</summary>
    public string AddBranch(string a, double y, double xEnd, int s, double k, string post)
    {
        var (ax, ay) = Pos(a);
        if (Math.Abs(y - ay) < 2 * k + 2) throw new InvalidOperationException("Za mała odległość w pionie na odgałęzienie (min. " + (2 * k + 2) + ")");
        int v = Math.Sign(y - ay);
        double mx = ax + s * k, kx = ax + s * 2 * k;
        if (Math.Sign(xEnd - kx) != s || Math.Abs(xEnd - kx) < 4) xEnd = kx + s * 6 * k;
        var j = AddNode(post, kx, y);
        var e1 = new JsonObject { ["a"] = a, ["b"] = j, ["sec"] = NewSecId(post), ["track"] = "", ["line"] = "", ["id"] = NewEdgeId() };
        SetPts(e1, new() { (ax, ay), (mx, ay + v * k), (mx, y - v * k), (kx, y) });
        e1["len"] = Math.Round(Math.Max(5, GeoLen(Pts(e1))), 1);
        Edges.Add(e1);
        var end = AddNode(post, xEnd, y);
        AddEdge(j, end, null, post);
        return end;
    }

    /// <summary>
    /// Wyprostowanie istniejącego rozjazdu: odgałęzienie (−) zwrotnicy aż do drugiej zwrotnicy (łącznik) albo do toru poziomego
    /// (odgałęzienie) dostaje kształt z pulpitu: ukos 45° o długości k, pion, ukos 45°. Koniec łącznika przesuwa się po swoim torze
    /// na odległość 2·k od zwrotnicy. Odcinki izolowane zostają.
    /// </summary>
    public string NormalizeTurnout(string swKey, double k)
    {
        if (Switches[swKey] is not JsonObject sw) throw new InvalidOperationException("Brak zwrotnicy");
        var a = Sv(sw["node"]);
        var first = MinusLeg(a) ?? throw new InvalidOperationException("Nie można ustalić odgałęzienia zwrotnicy");
        bool Horizontal(JsonObject e) { var p = Pts(e); return p.All(q => Math.Abs(q.Y - p[0].Y) < 0.5); }
        if (Horizontal(first)) throw new InvalidOperationException("Odgałęzienie zwrotnicy jest poziome – nie ma czego prostować");
        // łańcuch odgałęzienia po zwykłych węzłach (ukosy / piony) do drugiej zwrotnicy albo do toru poziomego
        var chain = new List<JsonObject>(); var mids = new List<string>(); string cur = a, end = null; var e = first;
        for (int g = 0; g < 30; g++)
        {
            chain.Add(e);
            var o = Sv(e["a"]) == cur ? Sv(e["b"]) : Sv(e["a"]);
            if (!IsPlainNode(o)) { end = o; break; }
            var next = EdgesAt(o).First(x => x != e);
            if (Horizontal(next) || IsLink(next)) { end = o; break; }
            mids.Add(o); e = next; cur = o;
        }
        if (end == null) throw new InvalidOperationException("Zbyt długi łącznik");
        var (ax, ay) = Pos(a); var (ex, ey) = Pos(end);
        if (Math.Abs(ey - ay) < 2 * k + 2) throw new InvalidOperationException($"Tory za blisko siebie (min. {2 * k + 2:0})");
        int v = Math.Sign(ey - ay), sx = Math.Sign(ex - ax); if (sx == 0) sx = 1;
        bool endSwitch = Switches.Any(x => Sv(x.Value["node"]) == end);
        if (!endSwitch && EdgesAt(end).Count != 2) throw new InvalidOperationException("Koniec odgałęzienia nie leży na torze (sygnalizator / kozioł w łączniku?) – wyprostuj ręcznie");
        var sec = Sv(chain[0]["sec"]); var sec2 = Sv(chain[^1]["sec"]);
        foreach (var x in chain) Edges.Remove(x);
        foreach (var m in mids) Nodes.Remove(m);
        // koniec na swoim torze: x = zwrotnica ± 2k (tor poziomy przy końcu przesuwa się razem z nim)
        double nx = ax + sx * 2 * k;
        MoveNode(end, nx, ey);
        double mx = ax + sx * k;
        if (endSwitch)
        {
            var j = AddNode(Sv(Nodes[a]["post"]) ?? "", mx, (ay + ey) / 2);
            var e1 = new JsonObject { ["a"] = a, ["b"] = j, ["sec"] = sec, ["track"] = "", ["line"] = "", ["id"] = NewEdgeId() };
            SetPts(e1, new() { (ax, ay), (mx, ay + v * k), (mx, (ay + ey) / 2) }); Edges.Add(e1);
            var e2 = new JsonObject { ["a"] = j, ["b"] = end, ["sec"] = sec2, ["track"] = "", ["line"] = "", ["id"] = NewEdgeId() };
            SetPts(e2, new() { (mx, (ay + ey) / 2), (mx, ey - v * k), (nx, ey) }); Edges.Add(e2);
            foreach (var q in new[] { e1, e2 }) q["len"] = Math.Round(Math.Max(5, GeoLen(Pts(q))), 1);
        }
        else
        {
            var e1 = new JsonObject { ["a"] = a, ["b"] = end, ["sec"] = sec, ["track"] = "", ["line"] = "", ["id"] = NewEdgeId() };
            SetPts(e1, new() { (ax, ay), (mx, ay + v * k), (mx, ey - v * k), (nx, ey) });
            e1["len"] = Math.Round(Math.Max(5, GeoLen(Pts(e1))), 1); Edges.Add(e1);
        }
        return endSwitch ? Switches.First(x => Sv(x.Value["node"]) == end).Key : null;
    }

    public void DeleteEdge(string eid)
    {
        var e = Edge(eid); if (e == null) return;
        Edges.Remove(e);
        foreach (var n in new[] { Sv(e["a"]), Sv(e["b"]) }) CleanNode(n);
    }

    /// <summary>Usuwa obiekty przypięte do węzła, który przestał spełniać warunki (zwrotnica bez 3 krawędzi, węzeł bez krawędzi).</summary>
    void CleanNode(string nid)
    {
        if (!Nodes.ContainsKey(nid)) return;
        var cnt = EdgesAt(nid).Count;
        foreach (var (k, v) in Switches.ToList()) if (Sv(v["node"]) == nid && cnt != 3) Switches.Remove(k);
        if (cnt == 0) DeleteNode(nid);
    }

    public void DeleteNode(string nid)
    {
        var nb = EdgesAt(nid).Select(e => Sv(e["a"]) == nid ? Sv(e["b"]) : Sv(e["a"])).Distinct().ToList();
        foreach (var e in EdgesAt(nid)) Edges.Remove(e);
        foreach (var (k, v) in Switches.ToList()) if (Sv(v["node"]) == nid) Switches.Remove(k);
        foreach (var (k, v) in Signals.ToList()) if (Sv(v["node"]) == nid) Signals.Remove(k);
        foreach (var (k, v) in Derailers.ToList()) if (Sv(v["node"]) == nid) Derailers.Remove(k);
        TrackEnds.Remove(nid); Boundaries.Remove(nid); FarEnds.Remove(nid);
        foreach (var (k, v) in Blocks.ToList())
            if (Sv(v["far"]) == nid || ((JsonArray)v["ends"]).Any(en => Sv(en["boundary"]) == nid)) DeleteBlock(k);
        Nodes.Remove(nid);
        foreach (var n in nb) if (n != nid) CleanNode(n);
    }

    // ------------------------------------------------------------------ elementy
    public string AddSwitch(string nid, string label, string post, bool assignSec)
    {
        var key = $"{post}:{label}";
        if (Switches.ContainsKey(key)) throw new InvalidOperationException($"Zwrotnica {label} już istnieje");
        if (EdgesAt(nid).Count != 3) throw new InvalidOperationException("Zwrotnica wymaga węzła z dokładnie 3 krawędziami (iglica + 2 odgałęzienia)");
        var sec = $"{post}:z{label}";
        if (assignSec) foreach (var e in EdgesAt(nid)) e["sec"] = sec;
        Switches[key] = new JsonObject { ["num"] = key, ["node"] = nid, ["label"] = label, ["sec"] = assignSec ? sec : Sv(EdgesAt(nid)[0]["sec"]), ["post"] = post };
        Nodes[nid]["kind"] = "switch"; Nodes[nid]["ref"] = label;
        if (assignSec) CrossoverSecs(key);
        return key;
    }

    /// <summary>Odgałęzienie zwrotnicy (−): bardziej nachylona z dwóch krawędzi po stronie przeciwnej do iglicy.</summary>
    public JsonObject MinusLeg(string nid)
    {
        var ed = EdgesAt(nid); if (ed.Count != 3) return null;
        var side = ed.Select(e => EdgeDir(e, nid)).ToList();
        int tipSide = side.Count(x => x > 0) == 1 ? 1 : -1;
        var legs = ed.Where((e, i) => side[i] != tipSide).ToList(); if (legs.Count != 2) return null;
        var (x, y) = Pos(nid);
        double Slope(JsonObject e) { var p = Pts(e); var q = Sv(e["a"]) == nid ? p[1] : p[^2]; var dx = Math.Abs(q.X - x); return dx < 0.01 ? 99 : Math.Abs(q.Y - y) / dx; }
        return legs.OrderBy(Slope).Last();
    }

    /// <summary>
    /// Łącznik rozjazdowy (od odgałęzienia zwrotnicy po zwykłych węzłach do drugiej zwrotnicy) należy do odcinków zwrotnicowych:
    /// pierwsza połowa – tej zwrotnicy, druga – zwrotnicy po drugiej stronie; jedna przerwa izolacyjna w środku (jak na wzorze pulpitu).
    /// </summary>
    public int CrossoverSecs(string swKey)
    {
        if (Switches[swKey] is not JsonObject sw) return 0;
        var n = Sv(sw["node"]); var sec = Sv(sw["sec"]);
        var e = MinusLeg(n); if (e == null) return 0;
        // łącznik = tory ukośne / pionowe; tor poziomy (stacyjny) kończy łącznik i nie zmienia odcinka
        bool Skew(JsonObject q) { var p = Pts(q); return p.Any(t => Math.Abs(t.Y - p[0].Y) > 0.5); }
        if (!Skew(e)) return 0;
        var chain = new List<JsonObject>(); string cur = n, end;
        while (true)
        {
            if (!Skew(e)) { end = cur; break; }
            chain.Add(e);
            var o = Sv(e["a"]) == cur ? Sv(e["b"]) : Sv(e["a"]);
            if (!IsPlainNode(o) || chain.Count > 30) { end = o; break; }
            var next = EdgesAt(o).First(x => x != e);
            if (IsSzlak(next) || !Skew(next)) { end = o; break; }
            e = next; cur = o;
        }
        var other = Switches.Select(x => (JsonObject)x.Value).FirstOrDefault(x => Sv(x["node"]) == end);
        if (other == null) return 0;     // tylko łącznik między dwiema zwrotnicami; łuki do innych torów zostają bez zmian
        int half = (chain.Count + 1) / 2;
        for (int i = 0; i < chain.Count; i++) chain[i]["sec"] = i < half || other == null ? sec : Sv(other["sec"]);
        return chain.Count;
    }

    public string AddSignal(string nid, string label, string type, int dir, string post, string szlak = null, string repeats = null)
    {
        var key = $"{post}:{label}";
        if (Signals.ContainsKey(key)) throw new InvalidOperationException($"Sygnalizator {label} już istnieje");
        if (EdgesAt(nid).Count > 2) throw new InvalidOperationException("Sygnalizator nie może stać na węźle zwrotnicy – postaw go na torze obok");
        var o = new JsonObject { ["name"] = key, ["node"] = nid, ["dir"] = dir, ["type"] = type, ["label"] = label, ["post"] = post };
        if (!string.IsNullOrEmpty(szlak)) o["szlak"] = szlak;
        if (!string.IsNullOrEmpty(repeats)) o["repeats"] = repeats.Contains(':') || Blocks.ContainsKey(repeats) || Signals.ContainsKey(repeats) ? repeats : $"{post}:{repeats}";
        Signals[key] = o;
        if (type is "wjazdowy" or "wyjazdowy" or "tm" or "pk") SplitSectionAt(nid, post);
        return key;
    }

    /// <summary>
    /// Odcinek izolowany kończy się na sygnalizatorze: gdy oba tory w węźle należą do tego samego odcinka,
    /// tory po stronie wschodniej (do najbliższej granicy odcinka) dostają nowy odcinek.
    /// </summary>
    public bool SplitSectionAt(string nid, string post)
    {
        var ed = EdgesAt(nid);
        if (ed.Count != 2 || Sv(ed[0]["sec"]) != Sv(ed[1]["sec"]) || IsSzlak(ed[0])) return false;
        var sec = Sv(ed[0]["sec"]);
        var east = ed.FirstOrDefault(e => EdgeDir(e, nid) > 0) ?? ed[1];
        var newSec = NewSecId(post ?? Sv(Nodes[nid]["post"]) ?? "");
        var seen = new HashSet<JsonObject>(); var q = new Queue<(JsonObject E, string From)>(); q.Enqueue((east, nid));
        while (q.Count > 0)
        {
            var (e, from) = q.Dequeue(); if (!seen.Add(e)) continue;
            e["sec"] = newSec;
            var o = Sv(e["a"]) == from ? Sv(e["b"]) : Sv(e["a"]);
            if (o == nid) continue;
            foreach (var x in EdgesAt(o)) if (!seen.Contains(x) && Sv(x["sec"]) == sec) q.Enqueue((x, o));
        }
        // gdy odcinek zamykał się w pętli i wrócił do węzła – cofnięcie (nie da się podzielić)
        if (Sv(ed[0]["sec"]) == Sv(ed[1]["sec"])) { foreach (var e in seen) e["sec"] = sec; return false; }
        return true;
    }

    /// <summary>Podział odcinków na wszystkich semaforach i tarczach (dla układów narysowanych wcześniej).</summary>
    public int SplitSectionsAtSignals()
    {
        int n = 0;
        foreach (var sg in Signals.Select(x => (JsonObject)x.Value).Where(x => Sv(x["type"]) is "wjazdowy" or "wyjazdowy" or "tm").ToList())
            if (SplitSectionAt(Sv(sg["node"]), Sv(sg["post"]))) n++;
        return n;
    }

    public void AddTrackEnd(string nid, string disp, string post, string label)
    {
        if (EdgesAt(nid).Count != 1) throw new InvalidOperationException("Kozioł / koniec toru stawia się na końcu toru (węzeł z jedną krawędzią)");
        TrackEnds[nid] = new JsonObject { ["id"] = $"{post}:{disp}", ["label"] = label ?? "", ["disp"] = disp, ["marker"] = true };
    }

    public void AddDerailer(string nid, string label, string post)
    {
        var ed = EdgesAt(nid); if (ed.Count == 0) throw new InvalidOperationException("Wykolejnica musi leżeć na torze");
        Derailers[$"{post}:{label}"] = new JsonObject { ["name"] = $"{post}:{label}", ["node"] = nid, ["label"] = label, ["post"] = post, ["sec"] = Sv(ed[0]["sec"]) };
    }

    public void RenameKey(JsonObject dict, string oldKey, string newKey)
    {
        if (oldKey == newKey || !dict.ContainsKey(oldKey)) return;
        if (dict.ContainsKey(newKey)) throw new InvalidOperationException(newKey + " już istnieje");
        var v = dict[oldKey]; dict.Remove(oldKey); dict[newKey] = v;
        if (dict == Signals)
        {
            v["name"] = newKey;
            foreach (var (_, s) in Signals) if (Sv(s["repeats"]) == oldKey) s["repeats"] = newKey;
            foreach (var (_, b) in Blocks) foreach (var en in (JsonArray)b["ends"]) if (Sv(en["entrySig"]) == oldKey) en["entrySig"] = newKey;
        }
        if (dict == Switches) v["num"] = newKey;
        if (dict == Derailers) v["name"] = newKey;
    }

    // ------------------------------------------------------------------ szlaki
    /// <summary>Krawędzie łańcucha między dwoma węzłami (bez przechodzenia przez zwrotnice), uporządkowane od węzła a.</summary>
    public List<(JsonObject E, string From, string To)> Chain(string a, string b)
    {
        var prev = new Dictionary<string, (string N, JsonObject E)>(); var q = new Queue<string>(); q.Enqueue(a); prev[a] = (null, null);
        while (q.Count > 0)
        {
            var n = q.Dequeue(); if (n == b) break;
            if (n != a && EdgesAt(n).Count > 2) continue;
            foreach (var e in EdgesAt(n))
            {
                var o = Sv(e["a"]) == n ? Sv(e["b"]) : Sv(e["a"]);
                if (prev.ContainsKey(o)) continue;
                prev[o] = (n, e); q.Enqueue(o);
            }
        }
        if (!prev.ContainsKey(b)) return null;
        var res = new List<(JsonObject, string, string)>();
        for (var n = b; prev[n].N != null; n = prev[n].N) res.Add((prev[n].E, prev[n].N, n));
        res.Reverse();
        return res;
    }

    /// <summary>
    /// Nowy szlak: granica przy semaforze wjazdowym (lub końcu obszaru posterunku) i drugi koniec – koniec obszaru LCS (szlak zewnętrzny)
    /// albo granica drugiego posterunku LCS (szlak wewnętrzny).
    /// </summary>
    public void AddBlock(string id, string boundary, string other, bool internalBlock, string nbr, string line, string tor, int defDir, bool sbl) =>
        AddBlock(id, boundary, other, internalBlock, nbr, line, tor, defDir, "sbl", sbl ? 3 : 1);

    public void AddBlock(string id, string boundary, string other, bool internalBlock, string nbr, string line, string tor, int defDir, string type, int odstepy = 1)
    {
        if (Blocks.ContainsKey(id)) throw new InvalidOperationException($"Szlak {id} już istnieje");
        var ch = Chain(boundary, other) ?? throw new InvalidOperationException("Brak toru łączącego wskazane węzły (szlak nie może przechodzić przez zwrotnice)");
        foreach (var (e, _, _) in ch) { e["szlak"] = true; e["sec"] = id + "-1"; e["track"] = ""; e["len"] = Math.Max(D(e["len"]), 300); }
        var (bx, _) = Pos(boundary); var (ox, _) = Pos(other);
        int outward = Math.Sign(ox - bx);
        if (outward == 0) throw new InvalidOperationException("Granica i koniec szlaku nie mogą leżeć w jednej pionie");
        string Entry(string n, int outw) => Signals.Where(s => Sv(s.Value["node"]) == n && (int)D(s.Value["dir"]) == -outw).Select(s => s.Key).FirstOrDefault() ?? "";
        var ends = new JsonArray(new JsonObject { ["boundary"] = boundary, ["outward"] = outward, ["entrySig"] = Entry(boundary, outward), ["post"] = Sv(Nodes[boundary]["post"]) });
        if (internalBlock)
            ends.Add(new JsonObject { ["boundary"] = other, ["outward"] = -outward, ["entrySig"] = Entry(other, -outward), ["post"] = Sv(Nodes[other]["post"]) });
        var west = outward > 0 ? boundary : other; var east = outward > 0 ? other : boundary;
        var blk = new JsonObject
        {
            ["id"] = id, ["type"] = type, ["nbr"] = nbr, ["internal"] = internalBlock, ["ends"] = ends, ["far"] = internalBlock ? null : other,
            ["secsWE"] = new JsonArray(id + "-1"), ["defDir"] = defDir, ["tor"] = tor, ["line"] = line, ["label"] = "it" + id
        };
        if (internalBlock) { blk["nbrW"] = Sv(Posts[Sv(Nodes[west]["post"]) ?? ""]) ?? ""; blk["nbrE"] = Sv(Posts[Sv(Nodes[east]["post"]) ?? ""]) ?? ""; }
        Blocks[id] = blk;
        Boundaries[boundary] = new JsonObject { ["szlak"] = id };
        if (internalBlock) Boundaries[other] = new JsonObject { ["szlak"] = id };
        else FarEnds[other] = new JsonObject { ["szlak"] = id, ["nbr"] = nbr };
        foreach (var en in ends) { var s = Sv(en["entrySig"]); if (s != "" && Signals[s] is JsonObject so) so["szlak"] = id; }
        if (type is "sbl1" or "pol1" && defDir == 0) blk["defDir"] = outward;
        if (odstepy >= 2 && type != "tel") SetOdstepy(id, odstepy);
    }

    /// <summary>
    /// Szlak od semafora wjazdowego: idzie torem na zewnątrz stacji (także przez łączniki obrazów) do semafora wjazdowego
    /// innego posterunku (szlak wewnętrzny) albo do końca toru (szlak zewnętrzny). Zwraca węzeł drugiego końca i rodzaj.
    /// </summary>
    public (string Other, bool Internal, string Sig)? TraceSzlak(string nid)
    {
        var sig = Signals.Select(x => (JsonObject)x.Value).FirstOrDefault(x => Sv(x["node"]) == nid && Sv(x["type"]) == "wjazdowy");
        if (sig == null) return null;
        int outward = -(int)D(sig["dir"]);
        string cur = nid, from = null;
        for (int k = 0; k < 500; k++)
        {
            var next = EdgesAt(cur).FirstOrDefault(e => e != (from == null ? null : Edge(from)) && EdgeDir(e, cur) == outward);
            if (next == null) return EdgesAt(cur).Count == 1 && cur != nid ? (cur, false, null) : null;
            var o = Sv(next["a"]) == cur ? Sv(next["b"]) : Sv(next["a"]);
            var osig = Signals.Select(x => (JsonObject)x.Value).FirstOrDefault(x => Sv(x["node"]) == o && Sv(x["type"]) == "wjazdowy" && (int)D(x["dir"]) == outward);
            if (osig != null) return (o, true, Sv(osig["label"]));
            if (EdgesAt(o).Count == 1) return (o, false, null);
            if (EdgesAt(o).Count > 2) return null;                       // zwrotnica – to nie jest tor szlakowy
            from = Sv(next["id"]); cur = o;
        }
        return null;
    }

    // ---------------------------------------------------------------- złącza szlaku wewnętrznego
    public JsonObject Ports { get { if (Root["ports"] is not JsonObject p) Root["ports"] = p = new JsonObject(); return p; } }

    /// <summary>Złącze szlaku wewnętrznego (kwadrat „SZLAK WEWNĘTRZNY”) na końcu toru szlakowego stacji – czeka na połączenie z innym złączem.</summary>
    public void AddPort(string endNode, string entryNode, string[] ans)
    {
        if (EdgesAt(endNode).Count != 1) throw new InvalidOperationException("Złącze stawia się na końcu toru szlakowego");
        var arr = new JsonArray(); foreach (var a in ans) arr.Add(a);
        Ports[endNode] = new JsonObject { ["id"] = ans[0], ["entry"] = entryNode, ["post"] = Sv(Nodes[entryNode]["post"]), ["ans"] = arr };
    }

    /// <summary>Połączenie dwóch złączy: tory łączą się (długość w metrach), powstaje jeden szlak wewnętrzny z semaforami na obu końcach.</summary>
    public string PairPorts(string a, string b, double len)
    {
        if (Ports[a] is not JsonObject pa || Ports[b] is not JsonObject pb) throw new InvalidOperationException("Brak złącza");
        if (a == b) throw new InvalidOperationException("Wybierz inne złącze");
        var ans = ((JsonArray)pa["ans"]).Select(x => Sv(x)).ToArray();
        string ea = Sv(pa["entry"]), eb = Sv(pb["entry"]);
        Ports.Remove(a); Ports.Remove(b);
        AddLink(a, b, len);
        var (fx, _) = Pos(ea); var (ox, _) = Pos(eb); int outward = Math.Sign(ox - fx);
        int defDir = ans.Length > 6 && ans[6].StartsWith("WYJAZD") ? outward : ans.Length > 6 && ans[6].StartsWith("PRZYJAZD") ? -outward : 0;
        var type = LcsWidzew.Model.BlockDef.Types.FirstOrDefault(t => t.Name == (ans.Length > 5 ? ans[5] : "")).Code ?? "sbl";
        int nod = ans.Length > 7 && int.TryParse(ans[7].Trim(), out var no) ? Math.Clamp(no, 1, 8) : 1;
        var id = ans[0].Trim(); if (Blocks.ContainsKey(id)) id += "W";
        AddBlock(id, ea, eb, true, "", ans.Length > 2 ? ans[2] : "", ans.Length > 3 ? ans[3] : "", defDir, type, type == "tel" ? 1 : nod);
        // każda stacja ma własne oznaczenie szlaku (oznaczenie jej złącza)
        var ends = (JsonArray)Blocks[id]["ends"];
        ends[0]["label"] = ans[0].Trim(); ends[1]["label"] = Sv(pb["id"]);
        return id;
    }

    /// <summary>Zmiana oznaczenia szlaku: odstępy, sygnalizatory odstępowe, semafory wjazdowe, granice i reguły przebiegów.</summary>
    public void RenameBlock(string old, string nw)
    {
        nw = (nw ?? "").Trim();
        if (nw == "" || nw.Contains('/') || nw.Contains(' ')) throw new InvalidOperationException("Oznaczenie szlaku: bez spacji i ukośnika (np. 1A, 2W)");
        if (nw == old) return;
        if (Blocks.ContainsKey(nw)) throw new InvalidOperationException($"Szlak {nw} już istnieje");
        if (Blocks[old] is not JsonObject b) throw new InvalidOperationException("Brak szlaku " + old);
        Blocks.Remove(old); b["id"] = nw; Blocks[nw] = b;
        var secMap = ((JsonArray)b["secsWE"]).Select(Sv).ToDictionary(s => s, s => nw + s[old.Length..]);
        b["secsWE"] = new JsonArray(secMap.Values.Select(x => (JsonNode)x).ToArray());
        foreach (var e in EdgeObjs) if (secMap.TryGetValue(Sv(e["sec"]) ?? "", out var ns)) e["sec"] = ns;
        if (Sv(b["label"]) == "it" + old) b["label"] = "it" + nw;
        foreach (var en in ((JsonArray)b["ends"]).OfType<JsonObject>()) if (Sv(en["label"]) == old) en["label"] = nw;
        foreach (var (k, v) in Signals.ToList())
        {
            if (Sv(v["szlak"]) != old) continue;
            v["szlak"] = nw;
            if (Sv(v["type"]) == "odstepowy" && k.StartsWith(old + "/"))
            {
                var nk = nw + k[old.Length..]; Signals.Remove(k); v["name"] = nk;
                if ((Sv(v["label"]) ?? "").StartsWith(old)) v["label"] = nw + Sv(v["label"])[old.Length..];
                Signals[nk] = v;
            }
        }
        foreach (var (_, v) in Signals) if (Sv(v["repeats"]) == old) v["repeats"] = nw;
        foreach (var (_, v) in Boundaries) if (Sv(v["szlak"]) == old) v["szlak"] = nw;
        foreach (var (_, v) in FarEnds) if (Sv(v["szlak"]) == old) v["szlak"] = nw;
        foreach (var r in RouteRules.OfType<JsonObject>()) if (Sv(r["end"]) == old) r["end"] = nw;
    }

    /// <summary>Prędkość dopuszczalna na torze szlakowym [km/h] (null – domyślna).</summary>
    public void SetSzlakVmax(string id, double? v)
    {
        var secs = ((JsonArray)Blocks[id]["secsWE"]).Select(Sv).ToHashSet();
        foreach (var e in EdgeObjs.Where(e => secs.Contains(Sv(e["sec"]) ?? ""))) { if (v is double d) e["vmax"] = d; else e.Remove("vmax"); }
    }

    /// <summary>Zmiana semafora wjazdowego przy końcu szlaku.</summary>
    public void SetEntrySig(string id, int ei, string sig)
    {
        var en = (JsonObject)((JsonArray)Blocks[id]["ends"])[ei];
        var old = Sv(en["entrySig"]);
        if (!string.IsNullOrEmpty(old) && Signals[old] is JsonObject so && Sv(so["szlak"]) == id) so.Remove("szlak");
        en["entrySig"] = sig ?? "";
        if (!string.IsNullOrEmpty(sig) && Signals[sig] is JsonObject sn) sn["szlak"] = id;
    }

    public void DeleteBlock(string id)
    {
        if (Blocks[id] is not JsonObject b) return;
        var secs = ((JsonArray)b["secsWE"]).Select(Sv).ToHashSet();
        foreach (var e in EdgeObjs.Where(e => secs.Contains(Sv(e["sec"])))) { e.Remove("szlak"); e["sec"] = id.Replace("/", "") + "_tor"; }
        foreach (var (k, v) in Signals.ToList()) { if (Sv(v["szlak"]) == id && Sv(v["type"]) == "odstepowy") Signals.Remove(k); else if (Sv(v["szlak"]) == id) v.AsObject().Remove("szlak"); }
        foreach (var (k, v) in Boundaries.ToList()) if (Sv(v["szlak"]) == id) Boundaries.Remove(k);
        foreach (var (k, v) in FarEnds.ToList()) if (Sv(v["szlak"]) == id) FarEnds.Remove(k);
        Blocks.Remove(id);
    }

    /// <summary>Podział szlaku na odstępy blokady samoczynnej (n ≥ 2) albo jeden odstęp – blokada liniowa bez sygnalizatorów odstępowych (n = 1).</summary>
    public void SetOdstepy(string id, int n)
    {
        var b = (JsonObject)Blocks[id];
        var ends = (JsonArray)b["ends"];
        string a = Sv(ends[0]["boundary"]);
        string z = b["far"] != null ? Sv(b["far"]) : Sv(ends[1]["boundary"]);
        if (Pos(a).X > Pos(z).X) (a, z) = (z, a);
        var oldLabels = Signals.Where(x => Sv(x.Value["szlak"]) == id && Sv(x.Value["type"]) == "odstepowy").ToDictionary(x => x.Key, x => Sv(x.Value["label"]));
        foreach (var (k, v) in Signals.ToList()) if (Sv(v["szlak"]) == id && Sv(v["type"]) == "odstepowy") Signals.Remove(k);
        var ch = Chain(a, z) ?? throw new InvalidOperationException("Szlak " + id + " nie jest ciągłym torem");
        double total = ch.Sum(c => D(c.E["len"]));
        var bnodes = new List<string>();
        if (n >= 2)
        {
            for (int j = 1; j < n; j++)
            {
                // węzeł podziału w j/n długości szlaku – istniejący węzeł pośredni albo nowy (podział krawędzi)
                ch = Chain(a, z); double target = total * j / n, acc = 0;
                foreach (var (e, from, to) in ch)
                {
                    double l = D(e["len"]);
                    if (acc + l >= target - 1)
                    {
                        string node;
                        if (Math.Abs(acc + l - target) < l * 0.15 && to != z) node = to;
                        else if (IsLink(e)) node = to != z ? to : from;
                        else
                        {
                            var p = Pts(e); if (Sv(e["a"]) != from) p.Reverse();
                            double geo = GeoLen(p), f = Math.Clamp((target - acc) / l, 0.1, 0.9), want = geo * f, s = 0;
                            (double X, double Y) q = p[0];
                            for (int i = 0; i < p.Count - 1; i++)
                            {
                                double d = Math.Sqrt(Math.Pow(p[i + 1].X - p[i].X, 2) + Math.Pow(p[i + 1].Y - p[i].Y, 2));
                                if (s + d >= want) { var u = d > 0 ? (want - s) / d : 0; q = (p[i].X + (p[i + 1].X - p[i].X) * u, p[i].Y + (p[i + 1].Y - p[i].Y) * u); break; }
                                s += d;
                            }
                            node = SplitEdge(Sv(e["id"]), q.X, q.Y, Sv(Nodes[from]["post"]));
                        }
                        if (!bnodes.Contains(node)) bnodes.Add(node);
                        break;
                    }
                    acc += l;
                }
            }
        }
        // przypisanie odcinków (odstępów) od zachodu
        ch = Chain(a, z); int k2 = 1; var secs = new JsonArray(id + "-1");
        foreach (var (e, from, to) in ch)
        {
            e["sec"] = $"{id}-{k2}"; e["szlak"] = true;
            if (bnodes.Contains(to) && to != z) { k2++; secs.Add($"{id}-{k2}"); }
        }
        b["secsWE"] = secs;
        // liczba odstępów a rodzaj: samoczynna ma ≥ 2 odstępy, pozostałe – jeden; kierunkowość zostaje
        // rodzaj blokady (SBL / PBL, kierunkowość) nie zależy od liczby odstępów
        if (Sv(b["type"]) is null or "blok") b["type"] = "sbl";
        for (int j = 0; j < bnodes.Count; j++)
        {
            var post = Sv(Nodes[bnodes[j]]["post"]) ?? "";
            Signals[$"{id}/{j + 1}+"] = new JsonObject { ["name"] = $"{id}/{j + 1}+", ["node"] = bnodes[j], ["dir"] = 1, ["type"] = "odstepowy", ["szlak"] = id, ["label"] = oldLabels.GetValueOrDefault($"{id}/{j + 1}+") ?? $"{id}{2 * j + 1}", ["post"] = post };
            Signals[$"{id}/{j + 1}-"] = new JsonObject { ["name"] = $"{id}/{j + 1}-", ["node"] = bnodes[j], ["dir"] = -1, ["type"] = "odstepowy", ["szlak"] = id, ["label"] = oldLabels.GetValueOrDefault($"{id}/{j + 1}-") ?? $"{id}{2 * j + 2}", ["post"] = post };
        }
    }

    // ------------------------------------------------------------------ długości odcinków
    /// <summary>Długość odcinka izolowanego / odstępu [m] – suma długości jego torów.</summary>
    public double SecLen(string sec) => EdgeObjs.Where(e => Sv(e["sec"]) == sec).Sum(e => D(e["len"]));

    /// <summary>Ustawienie długości odcinka: tory odcinka przeskalowane proporcjonalnie (przy zerowej sumie – po równo).</summary>
    public void SetSecLen(string sec, double len)
    {
        if (len < 1) throw new InvalidOperationException("Długość odcinka musi być dodatnia");
        var es = EdgeObjs.Where(e => Sv(e["sec"]) == sec).ToList();
        if (es.Count == 0) throw new InvalidOperationException("Odcinek " + sec + " nie ma torów");
        double sum = es.Sum(e => D(e["len"]));
        foreach (var e in es) e["len"] = Math.Round(Math.Max(1, sum > 0 ? D(e["len"]) * len / sum : len / es.Count), 1);
    }

    // ------------------------------------------------------------------ reguły przebiegów
    public JsonArray RouteRules { get { if (Root["routeRules"] is not JsonArray a) Root["routeRules"] = a = new JsonArray(); return a; } }

    JsonObject Rule(string start, string end, string type, string sw) =>
        RouteRules.OfType<JsonObject>().FirstOrDefault(r => Sv(r["start"]) == start && Sv(r["end"]) == end && Sv(r["type"]) == type && (Sv(r["sw"]) ?? "") == sw);

    static bool Flag(JsonNode n) => n is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    /// <summary>Wyłączenie / włączenie przebiegu albo ustanowienie przebiegu zasadniczego (jeden na parę początek–koniec).</summary>
    public void SetRule(string start, string end, string type, string sw, bool? off, bool? basic, double? vmax = null)
    {
        if (basic == true)
            foreach (var r in RouteRules.OfType<JsonObject>().Where(r => Sv(r["start"]) == start && Sv(r["end"]) == end && Sv(r["type"]) == type).ToList()) r["basic"] = false;
        var o = Rule(start, end, type, sw);
        if (o == null) { o = new JsonObject { ["start"] = start, ["end"] = end, ["type"] = type, ["sw"] = sw }; RouteRules.Add(o); }
        if (off != null) o["off"] = off.Value;
        if (basic != null) o["basic"] = basic.Value;
        if (vmax != null) { if (vmax > 0) o["vmax"] = vmax.Value; else o.Remove("vmax"); }
        foreach (var r in RouteRules.OfType<JsonObject>().ToList())
            if (!Flag(r["off"]) && !Flag(r["basic"]) && r["vmax"] == null) RouteRules.Remove(r);
    }

    // ------------------------------------------------------------------ kontrola poprawności
    public List<string> Validate()
    {
        var w = new List<string>();
        var deg = Nodes.ToDictionary(n => n.Key, n => 0);
        foreach (var e in EdgeObjs)
        {
            var a = Sv(e["a"]); var b = Sv(e["b"]);
            if (!Nodes.ContainsKey(a) || !Nodes.ContainsKey(b)) { w.Add($"Krawędź {Sv(e["id"])}: nieistniejący węzeł"); continue; }
            deg[a]++; deg[b]++;
            if (Math.Abs(Pos(a).X - Pos(b).X) < 0.5 && (e["dir"] == null || (int)D(e["dir"]) == 0)) w.Add($"Tor pionowy {Sv(e["id"])} ({a}–{b}) bez kierunku jazdy – ustaw „dir” we właściwościach (1 – jazda od {a} do {b} na wschód, -1 – na zachód)");
            if (string.IsNullOrEmpty(Sv(e["sec"]))) w.Add($"Krawędź {Sv(e["id"])}: brak odcinka izolowanego");
        }
        foreach (var (k, v) in Switches)
        {
            var n = Sv(v["node"]);
            if (!deg.TryGetValue(n, out var d) || d != 3) { w.Add($"Zwrotnica {Sv(v["label"])}: węzeł ma {d} krawędzi zamiast 3"); continue; }
            var (x, _) = Pos(n);
            var sides = EdgesAt(n).Select(e => EdgeDir(e, n)).ToList();
            if (sides.Count(s => s > 0) != 1 && sides.Count(s => s < 0) != 1) w.Add($"Zwrotnica {Sv(v["label"])}: nie da się ustalić iglicy (po jednej stronie musi być 1 krawędź, po drugiej 2)");
        }
        foreach (var (k, v) in Signals)
        {
            var n = Sv(v["node"]);
            if (!Nodes.ContainsKey(n)) { w.Add($"Sygnalizator {k}: brak węzła"); continue; }
            if (deg[n] > 2) w.Add($"Sygnalizator {Sv(v["label"])} stoi na węźle zwrotnicy");
            if (Sv(v["type"]) is "to" or "sp" && (Sv(v["repeats"]) is not string r || (!Signals.ContainsKey(r) && !Blocks.ContainsKey(r)))) w.Add($"Tarcza/powtarzacz {Sv(v["label"])}: nie wskazano powtarzanego semafora (albo szlaku – informacja z sąsiedniej stacji)");
        }
        foreach (var (k, v) in Signals)
        {
            if (Sv(v["type"]) is not ("wjazdowy" or "wyjazdowy" or "tm")) continue;
            var ed = EdgesAt(Sv(v["node"]));
            if (ed.Count == 2 && Sv(ed[0]["sec"]) == Sv(ed[1]["sec"]) && !IsSzlak(ed[0])) w.Add($"Odcinek {Sv(ed[0]["sec"])} przechodzi przez sygnalizator {Sv(v["label"])} – przebiegi będą się blokować (przycisk „Odcinki na sygnalizatorach”)");
        }
        foreach (var (k, v) in TrackEnds) if (!deg.TryGetValue(k, out var d) || d != 1) w.Add($"Kozioł {Sv(v["disp"])}: węzeł nie jest końcem toru");
        // rozgałęzienie toru bez zwrotnicy
        var swNodes = Switches.Select(x => Sv(x.Value["node"])).ToHashSet();
        foreach (var (n, d) in deg) if (d >= 3 && !swNodes.Contains(n)) w.Add($"Węzeł {n}: rozgałęzienie ({d} tory) bez zwrotnicy – postaw zwrotnicę (narzędzie Zwrotnica)");
        // odcinek izolowany z niepołączonych części (np. kawałki na dwóch różnych torach)
        foreach (var g in EdgeObjs.Where(e => !IsSzlak(e) && !IsLink(e)).GroupBy(e => Sv(e["sec"]) ?? ""))
        {
            var es = g.ToList(); if (es.Count < 2 || g.Key == "") continue;
            var seen = new HashSet<string> { Sv(es[0]["a"]) }; bool grow = true;
            var done = new HashSet<JsonObject>();
            while (grow)
            {
                grow = false;
                foreach (var e in es) if (!done.Contains(e) && (seen.Contains(Sv(e["a"])) || seen.Contains(Sv(e["b"])))) { seen.Add(Sv(e["a"])); seen.Add(Sv(e["b"])); done.Add(e); grow = true; }
            }
            if (done.Count < es.Count) w.Add($"Odcinek {g.Key} składa się z niepołączonych części ({string.Join(", ", es.Where(e => !done.Contains(e)).Select(e => Sv(e["id"])))} osobno) – przypisz kawałki do właściwych odcinków (narzędzie Odcinek)");
        }
        foreach (var (k, v) in Blocks)
        {
            foreach (var en in (JsonArray)v["ends"]) if (!Nodes.ContainsKey(Sv(en["boundary"]) ?? "")) w.Add($"Szlak {k}: brak węzła granicy");
            if (v["internal"] is JsonValue iv && iv.TryGetValue<bool>(out var ib) && ib)
                foreach (var en in (JsonArray)v["ends"])
                    if (string.IsNullOrEmpty(Sv(en["entrySig"])) || !Signals.ContainsKey(Sv(en["entrySig"])))
                        w.Add($"Szlak wewnętrzny {k}: koniec {Sv(en["boundary"])} bez semafora wjazdowego – szlak nie łączy dwóch stacji. Usuń go i utwórz ponownie: klik na semafor wjazdowy, „wewnętrzny – do innej stacji LCS”, klik na semafor drugiej stacji.");
            var secs = ((JsonArray)v["secsWE"]).Select(Sv).ToList();
            foreach (var s in secs) if (!EdgeObjs.Any(e => Sv(e["sec"]) == s)) w.Add($"Szlak {k}: odstęp {s} bez torów");
        }
        foreach (var (k, d) in deg) if (d == 0) w.Add($"Węzeł {k} bez torów (zostanie pominięty – usuń go)");
        foreach (var (k, v) in Ports) w.Add($"Złącze szlaku wewnętrznego {Sv(v["id"])} ({k}) niepołączone – zaznacz je i wybierz złącze drugiej stacji");
        if (Pages.Count == 0) w.Add("Brak obrazów (stron)");
        return w;
    }
}

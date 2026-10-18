using System.Text.Json.Nodes;

namespace Lcs.Model;

/// <summary>
/// Przekształcenie UT + zobrazowanie → model symulatora (<c>station.json</c>, etap 9 – PLAN_EDYTOR_UT.md § 5).
/// Topologia (węzły, tory, zwrotnice, odcinki, sygnalizatory, szlaki) powstaje z UT: każdy tor UT dzieli się na kawałki między
/// rozjazdami, urządzeniami i granicami odcinków; długości – km UT. Geometria pulpitu („display”) – z obrazów zobrazowania: punkt
/// na torze obrazu wynika z km UT, interpolowanego między symbolami narysowanymi przez człowieka (kolejność sprawdza „Sprawdź”).
/// Plan schematyczny (korzeń pliku) – z geometrii UT wzdłuż osi schematu. Przebiegi wyznacza symulator z topologii, jak dla
/// istniejących układów; tablicę zależności (routeRules) projektuje człowiek w symulatorze.
/// Nazwy obiektów: kod posterunku z UT + „:” + nazwa (np. „MT:A”, „MT:1”), odcinki szlaku – sama nazwa (jak w dotychczasowych plikach).
/// </summary>
public static class DoSymulatora
{
    /// <summary>Wynik: plik symulatora i uwagi (czego nie przeniesiono albo co uzupełniono).</summary>
    public record Wynik(JsonObject Json, List<string> Uwagi);

    const double Tol = 0.5;           // [m] – punkty toru bliżej niż Tol to ten sam węzeł

    public static Wynik Buduj(UtProject ut, ZobProject zob, string source = null)
    {
        var uw = new List<string>();
        var r = Topologia.Routes(ut);
        var styki = Topologia.Styki(ut, r);
        var odc = Odcinki.Compute(ut, r);
        var drogi = Szlaki.Drogi(ut, r, styki);
        var tracks = ut.Tracks.Where(t => r[t.Id].Elements.Count > 0).ToList();
        string Kod(string track) => Posterunki.Of(ut, track)?.Kod is string k && k.Length > 0 ? k : "UT";
        string Nr(string track) => ut.Tracks.First(t => t.Id == track).Number;

        // ---------- węzły na torach (km UT) ----------
        var pts = tracks.ToDictionary(t => t.Id, _ => new List<(double S, string N)>());
        var nodePost = new Dictionary<string, string>();
        var nodeKind = new Dictionary<string, (string Kind, string Ref)>();
        int nn = 0;
        string NodeOn(string track, double s, string want = null)
        {
            var l = pts[track];
            foreach (var x in l) if (Math.Abs(x.S - s) < Tol) return x.N;
            var id = want ?? $"{Kod(track)}:n{++nn}";
            l.Add((s, id)); nodePost[id] = Kod(track);
            return id;
        }
        void Put(string track, double s, string id)
        {
            var l = pts[track];
            if (l.Any(x => x.N == id)) return;
            if (l.FirstOrDefault(x => Math.Abs(x.S - s) < Tol) is var o && o.N != null && o.N != id) uw.Add($"tor {Nr(track)} km {s:0.#}: dwa węzły w jednym miejscu ({o.N}, {id})");
            l.Add((s, id));
        }

        // rozjazdy: węzeł zwrotnicy w PR na torze zasadniczym, odgałęzienie zaczyna się (kończy) w tym samym węźle
        var swNode = new Dictionary<string, string>();
        foreach (var z in ut.Turnouts.Where(z => pts.ContainsKey(z.Track) && pts.ContainsKey(z.Branch)))
        {
            var id = NodeOn(z.Track, z.S, $"{Kod(z.Track)}:sw{z.Number}");
            swNode[z.Id] = id; nodeKind[id] = ("switch", z.Number);
            Put(z.Branch, z.BranchAtEnd ? r[z.Branch].Length : 0, id);
        }
        if (ut.Crossings.Count > 0) uw.Add($"rozjazdy krzyżowe i skrzyżowania ({ut.Crossings.Count}) – jeszcze nie przenoszone do symulatora");

        // styki końców torów (połączenia bez rozjazdu)
        foreach (var st in styki.Where(s => pts.ContainsKey(s.Track) && pts.ContainsKey(s.Other)))
        {
            double se = st.AtStart ? 0 : r[st.Track].Length;
            if (pts[st.Track].Any(x => Math.Abs(x.S - se) < Tol)) continue;
            var id = NodeOn(st.Other, st.OtherS);
            Put(st.Track, se, id);
            if (!st.OtherEnd) uw.Add($"koniec toru {Nr(st.Track)} leży w środku toru {Nr(st.Other)} bez rozjazdu – w symulatorze połączenie bez zwrotnicy");
        }

        // urządzenia
        var devNode = new Dictionary<string, string>();
        foreach (var d in ut.Devices.Where(d => pts.ContainsKey(d.Track)))
        {
            if (d.Kind is UtDevice.Ukres or UtDevice.W4 or UtDevice.W5) continue;
            double s = Math.Clamp(d.S, 0, r[d.Track].Length);
            var id = NodeOn(d.Track, s);
            devNode[d.Id] = id;
            if (nodeKind.TryGetValue(id, out var nk) && nk.Kind == "switch" && d.Kind != UtDevice.Koziol)
                uw.Add($"{ZobSymbole.Nazwa(ut, d.Id)} stoi w miejscu zwrotnicy {nk.Ref} – przesuń w UT (w symulatorze węzeł zwrotnicy nie ma urządzeń)");
            if (d.Kind == UtDevice.Wykolejnica) nodeKind[id] = ("derail", d.Name);
        }
        // końce torów
        foreach (var t in tracks)
        {
            NodeOn(t.Id, 0); NodeOn(t.Id, r[t.Id].Length);
        }

        // ---------- plan schematyczny (korzeń): oś schematu ----------
        double az = ut.SchematAz();
        var dir = Element.Dir(az); var right = Element.Right(az);
        var all = tracks.SelectMany(t => pts[t.Id].Select(x => r[t.Id].At(x.S).P)).ToList();
        double Along(XY p) => p.X * dir.X + p.Y * dir.Y;
        double Across(XY p) => p.X * right.X + p.Y * right.Y;
        double a0 = all.Min(Along), a1 = all.Max(Along), c0 = all.Min(Across), c1 = all.Max(Across);
        int W = zob?.Obrazy.FirstOrDefault()?.Szer ?? 1920, H = zob?.Obrazy.FirstOrDefault()?.Wys ?? 1080;
        double sx = (W - 80) / Math.Max(1, a1 - a0), sy = Math.Max(sx * 4, 4);
        double py0 = H / 2.0 - (c1 - c0) * sy / 2;
        (double X, double Y) Plan(XY p) => (Math.Round(40 + (Along(p) - a0) * sx, 2), Math.Round(py0 + (Across(p) - c0) * sy, 2));
        (double X, double Y) PlanAt(string track, double s) => Plan(r[track].At(Math.Clamp(s, 0, r[track].Length)).P);

        // ---------- odcinki ----------
        UtLine LineAt(string track, double s) => Szlaki.At(ut, drogi, track, s);
        var unnamed = new HashSet<int>();
        string SecName(string track, double s, out bool szlak, out UtLine line)
        {
            line = LineAt(track, s); szlak = line != null;
            int comp = odc.At(track, s);
            string name;
            if (comp < 0 || !odc.Names.TryGetValue(comp, out var ns) || ns.Count == 0)
            {
                name = $"{(szlak ? line.Numer : "T" + Nr(track))}-{comp}";
                if (unnamed.Add(comp)) uw.Add($"odcinek bez nazwy w UT (tor {Nr(track)} km {s:0}) – w symulatorze „{name}”; postaw liczniki osi i nazwy odcinków w edytorze UT");
            }
            else
            {
                name = ns[0].Name;
                if (ns.Count > 1) uw.Add($"odcinki {string.Join(", ", ns.Select(x => x.Name))} bez granicy między nimi – w symulatorze jeden odcinek {name}");
            }
            return szlak ? name : $"{Kod(track)}:{name}";
        }

        // ---------- tory symulatora ----------
        var edges = new JsonArray();
        var edgeInfo = new List<(string Id, string Track, double S0, double S1, string A, string B)>();
        var secKind = new Dictionary<string, string>();
        var blockSecs = new Dictionary<string, List<(string Sec, double X)>>();
        int ne = 0;
        foreach (var t in tracks)
        {
            var l = pts[t.Id].OrderBy(x => x.S).ToList();
            for (int i = 1; i < l.Count; i++)
            {
                double s0 = l[i - 1].S, s1 = l[i].S; if (s1 - s0 < 0.01) continue;
                double mid = (s0 + s1) / 2;
                var sec = SecName(t.Id, mid, out bool szlak, out var line);
                var id = $"e{ne++}";
                var pl = new JsonArray();
                int n = t.Vertices.Count > 2 || t.Align != null ? 6 : 1;
                for (int k = 0; k <= n; k++) { var q = PlanAt(t.Id, s0 + (s1 - s0) * k / n); pl.Add(new JsonArray(q.X, q.Y)); }
                var e = new JsonObject
                {
                    ["id"] = id, ["a"] = l[i - 1].N, ["b"] = l[i].N, ["pts"] = pl, ["sec"] = sec, ["len"] = Math.Round(s1 - s0, 1),
                    ["track"] = t.Number, ["line"] = szlak ? line.Linia : "",
                    ["cat"] = t.Cat is UtTrack.MainZ or UtTrack.MainD ? t.Cat : "b",
                };
                if (t.Elec) e["elec"] = true;
                if (t.Vmax > 0) e["vmax"] = t.Vmax;
                if (szlak)
                {
                    e["szlak"] = true;
                    if (!blockSecs.TryGetValue(line.Id, out var bl)) blockSecs[line.Id] = bl = new();
                    if (!bl.Any(x => x.Sec == sec)) bl.Add((sec, PlanAt(t.Id, mid).X));
                }
                var us = ut.Sections.FirstOrDefault(x => odc.CompOf.TryGetValue(x.Id, out var c) && c == odc.At(t.Id, mid));
                if (us != null) secKind[sec] = us.Kind;
                edges.Add(e);
                edgeInfo.Add((id, t.Id, s0, s1, l[i - 1].N, l[i].N));
            }
        }

        // ---------- węzły ----------
        var nodes = new JsonObject();
        var nodePlan = new Dictionary<string, (double X, double Y)>();
        foreach (var t in tracks) foreach (var x in pts[t.Id]) if (!nodePlan.ContainsKey(x.N)) nodePlan[x.N] = PlanAt(t.Id, x.S);
        foreach (var (id, p) in nodePlan)
        {
            var (k, rf) = nodeKind.TryGetValue(id, out var v) ? v : ("", "");
            nodes[id] = new JsonObject { ["x"] = p.X, ["y"] = p.Y, ["kind"] = k, ["ref"] = rf, ["post"] = nodePost.GetValueOrDefault(id, "") };
        }

        // ---------- zwrotnice ----------
        var switches = new JsonObject();
        foreach (var z in ut.Turnouts.Where(z => swNode.ContainsKey(z.Id)))
        {
            var key = $"{Kod(z.Track)}:{z.Number}";
            if (z.Number.Length == 0 || switches.ContainsKey(key)) { uw.Add($"rozjazd bez numeru albo z powtórzonym numerem ({key}) – pominięty"); continue; }
            var typ = Katalog.Find(z.Type);
            var o = new JsonObject
            {
                ["num"] = key, ["node"] = swNode[z.Id], ["label"] = z.Number, ["sec"] = SecName(z.Track, z.S + z.Dir * 0.2, out _, out _), ["post"] = Kod(z.Track),
                ["kind"] = z.Type,
            };
            if (typ != null) { o["radius"] = typ.R; o["ratio"] = $"1:{typ.N:0.##}".Replace('.', ','); if (typ.Vbok > 0) o["vbok"] = typ.Vbok; }
            if (z.Zas == "-") o["zas"] = "-";
            if (z.Manual) o["manual"] = true;
            switches[key] = o;
        }

        // ---------- szlaki ----------
        var blocks = new JsonObject(); var boundaries = new JsonObject(); var farEnds = new JsonObject();
        var lineOfEntry = new Dictionary<string, string>();
        foreach (var ln in ut.Lines)
        {
            if (!drogi.TryGetValue(ln.Id, out var dr) || dr.Err != null) { uw.Add($"szlak {ln.Numer}: {dr?.Err ?? "brak drogi"} – pominięty"); continue; }
            var ends = new JsonArray(); string far = null, nbr = "", post = ""; int defDir = 0;
            foreach (var en in ln.Konce)
            {
                if (en.Semafor != null && ut.Devices.FirstOrDefault(d => d.Id == en.Semafor) is UtDevice sg && devNode.TryGetValue(sg.Id, out var bn))
                {
                    int outward = -sg.Dir;
                    post = Kod(sg.Track);
                    ends.Add(new JsonObject { ["boundary"] = bn, ["outward"] = outward, ["entrySig"] = $"{post}:{sg.Name}", ["post"] = post, ["label"] = en.Oznaczenie.Length > 0 ? en.Oznaczenie : ln.Numer });
                    boundaries[bn] = new JsonObject { ["szlak"] = ln.Numer };
                    lineOfEntry[sg.Id] = ln.Numer;
                    if (defDir == 0) defDir = outward;
                }
                else if (en.Track != null && pts.ContainsKey(en.Track))
                {
                    far = NodeOn(en.Track, en.AtStart ? 0 : r[en.Track].Length);
                    nbr = en.Sasiad;
                }
            }
            if (ends.Count == 0 || (ends.Count == 1 && far == null)) { uw.Add($"szlak {ln.Numer}: brak semafora wjazdowego albo końca na granicy obszaru – pominięty"); continue; }
            var secs = blockSecs.GetValueOrDefault(ln.Id) ?? new();
            var b = new JsonObject
            {
                ["id"] = ln.Numer, ["type"] = ln.Typ, ["nbr"] = nbr, ["internal"] = ends.Count == 2, ["ends"] = ends,
                ["secsWE"] = new JsonArray(secs.OrderBy(x => x.X).Select(x => (JsonNode)x.Sec).ToArray()),
                ["defDir"] = defDir, ["tor"] = ln.Tor, ["line"] = ln.Linia, ["label"] = ln.Numer, ["aspects"] = ln.Stawnosc,
            };
            if (far != null) { b["far"] = far; farEnds[far] = new JsonObject { ["szlak"] = ln.Numer, ["nbr"] = nbr }; }
            blocks[ln.Numer] = b;
        }

        // ---------- sygnalizatory, wykolejnice, kozły ----------
        var signals = new JsonObject(); var derailers = new JsonObject(); var bumpers = new JsonObject();
        foreach (var d in ut.Devices.Where(d => devNode.ContainsKey(d.Id)))
        {
            var node = devNode[d.Id]; var post = Kod(d.Track);
            if (d.Kind == UtDevice.Koziol) { bumpers[node] = new JsonObject(); continue; }
            if (d.Kind == UtDevice.Wykolejnica)
            {
                var wk = $"{post}:{d.Name}";
                derailers[wk] = new JsonObject { ["name"] = wk, ["node"] = node, ["label"] = d.Name, ["post"] = post, ["sec"] = SecName(d.Track, d.S, out _, out _) };
                continue;
            }
            string type = d.Kind switch
            {
                UtDevice.TarczaMan => "tm", UtDevice.TarczaOstrz => "to",
                UtDevice.Semafor => d.Funkcja switch
                {
                    UtDevice.FWjazdowy => "wjazdowy", UtDevice.FWyjazdowy => "wyjazdowy", UtDevice.FOdstepowy => "odstepowy",
                    UtDevice.FPowtarzacz => "sp", UtDevice.FPk => "pk", UtDevice.FDrogowskazowy => "wyjazdowy", _ => null,
                },
                _ => null,
            };
            if (d.Kind is UtDevice.LicznikOsi or UtDevice.Zlacze) continue;
            if (type == null) { uw.Add($"{ZobSymbole.Nazwa(ut, d.Id)}: brak funkcji semafora – pominięty (ustaw funkcję w UT)"); continue; }
            if (d.Funkcja == UtDevice.FDrogowskazowy) uw.Add($"semafor drogowskazowy {d.Name} – w symulatorze jako wyjazdowy");
            var key = $"{post}:{d.Name}";
            if (d.Name.Length == 0 || signals.ContainsKey(key)) { uw.Add($"sygnalizator bez nazwy albo z powtórzoną nazwą ({key}) – pominięty"); continue; }
            var o = new JsonObject { ["name"] = key, ["node"] = node, ["dir"] = d.Dir, ["type"] = type, ["label"] = d.Name, ["post"] = post, ["track"] = Nr(d.Track) };
            if (lineOfEntry.TryGetValue(d.Id, out var szl)) o["szlak"] = szl;
            else if (type == "odstepowy")
            {
                var l = LineAt(d.Track, d.S + 0.01) ?? LineAt(d.Track, d.S - 0.01);
                if (l == null || !blocks.ContainsKey(l.Numer)) { uw.Add($"semafor odstępowy {d.Name} poza szlakiem – pominięty"); continue; }
                o["szlak"] = l.Numer;
            }
            if (type == "wjazdowy" && !lineOfEntry.ContainsKey(d.Id)) uw.Add($"semafor wjazdowy {d.Name} nie jest końcem żadnego szlaku w UT");
            var lights = UtDevice.ParseLights(d.LightsOrDefault);
            if (d.Kind == UtDevice.Semafor) o["lamps"] = lights.Count;
            if (type == "wyjazdowy" && !lights.Any(x => x.C == 'B')) o["shunt"] = false;
            if (d.Side < 0) o["side"] = "L";
            if (d.MastOrDefault is UtDevice.MastWysiegnik or UtDevice.MastBramka or UtDevice.MastKarzel) o["mount"] = d.MastOrDefault;
            signals[key] = o;
        }

        // ---------- plan: perony, przejazdy ----------
        var platforms = new JsonArray(); var crossings = new JsonArray();
        foreach (var pl in ut.Platforms.Where(x => pts.ContainsKey(x.Track)))
        {
            var p0 = PlanAt(pl.Track, Math.Min(pl.S0, pl.S1)); var p1 = PlanAt(pl.Track, Math.Max(pl.S0, pl.S1));
            double h = Math.Max(4, pl.Width * sy), off = pl.Offset * sy;
            double y = pl.Side > 0 ? p0.Y + off : p0.Y - off - h;
            platforms.Add(new JsonObject { ["name"] = pl.Name, ["x"] = Math.Min(p0.X, p1.X), ["y"] = Math.Round(y, 2), ["w"] = Math.Round(Math.Abs(p1.X - p0.X), 2), ["h"] = Math.Round(h, 2) });
        }
        foreach (var lc in ut.LevelCrossings.Where(x => pts.ContainsKey(x.Track)))
        {
            var p = PlanAt(lc.Track, lc.S);
            crossings.Add(new JsonObject { ["name"] = lc.Name.Length > 0 ? lc.Name : UtLevelCrossing.CatName(lc.Cat), ["x"] = p.X, ["y0"] = p.Y - 20, ["y1"] = p.Y + 20 });
        }

        // ---------- zobrazowanie ----------
        var pages = new JsonArray();
        JsonObject display = null;
        var posts = new JsonObject();
        foreach (var p in ut.Posts) posts[p.Kod] = p.Nazwa;
        if (zob != null && zob.Obrazy.Count > 0)
        {
            var dn = new JsonObject(); var de = new JsonObject();
            int x0 = 0;
            foreach (var ob in zob.Obrazy)
            {
                var codes = ob.Posterunki.Select(id => ut.Posts.FirstOrDefault(p => p.Id == id)?.Kod).Where(k => k != null).ToList();
                pages.Add(new JsonObject { ["id"] = ob.Id, ["name"] = ob.Nazwa, ["x0"] = x0, ["x1"] = x0 + ob.Szer, ["posts"] = new JsonArray(codes.Select(c => (JsonNode)c).ToArray()) });
                foreach (var ei in edgeInfo)
                {
                    if (de.ContainsKey(ei.Id)) continue;
                    double mid = (ei.S0 + ei.S1) / 2;
                    var zt = ZobSymbole.Kawalki(ob, ei.Track, mid).FirstOrDefault();
                    if (zt == null) continue;
                    var map = Mapa(ut, ob, zt);
                    var line = Fragment(zt, map(ei.S0), map(ei.S1));
                    de[ei.Id] = new JsonArray(line.Select(q => (JsonNode)new JsonArray(q.X + x0, q.Y)).ToArray());
                    if (!dn.ContainsKey(ei.A)) dn[ei.A] = new JsonArray(line[0].X + x0, line[0].Y);
                    if (!dn.ContainsKey(ei.B)) dn[ei.B] = new JsonArray(line[^1].X + x0, line[^1].Y);
                }
                x0 += ob.Szer;
            }
            int missing = edgeInfo.Count(e => !de.ContainsKey(e.Id));
            if (missing > 0) uw.Add($"{missing} kawałków torów UT nie ma na żadnym obrazie – na pulpicie ich nie widać");
            display = new JsonObject { ["nodes"] = dn, ["edges"] = de, ["labels"] = new JsonArray(), ["platforms"] = new JsonArray(), ["crossings"] = new JsonArray() };
        }
        else uw.Add("brak zobrazowania – pulpit pokaże plan z UT");

        var sections = new JsonObject();
        foreach (var (k, v) in secKind) if (v == UtSection.ObwodTorowy) sections[k] = new JsonObject { ["detection"] = "ot" };
        var code = ut.Posts.FirstOrDefault()?.Kod ?? "UT";
        var root = new JsonObject
        {
            ["name"] = ut.Name, ["code"] = code, ["width"] = W, ["height"] = H, ["orient"] = "ab", ["source"] = source ?? "UT + zobrazowanie",
            ["detection"] = "lo", ["posts"] = posts, ["pages"] = pages,
            ["nodes"] = nodes, ["edges"] = edges, ["switches"] = switches, ["signals"] = signals, ["derailers"] = derailers,
            ["blocks"] = blocks, ["boundaries"] = boundaries, ["farEnds"] = farEnds, ["trackEnds"] = new JsonObject(), ["bumpers"] = bumpers,
            ["sections"] = sections, ["platforms"] = platforms, ["labels"] = new JsonArray(), ["crossings"] = crossings, ["dashed"] = new JsonArray(),
            ["routeRules"] = new JsonArray(),
        };
        if (display != null) root["display"] = display;
        return new Wynik(root, uw);
    }

    /// <summary>
    /// Km toru UT → ułamek długości łamanej toru obrazu: liniowo między punktami zaczepienia – końcami kawałka i symbolami obiektów UT
    /// tego toru narysowanymi na łamanej (człowiek stawia je w dowolnych miejscach, więc węzeł symulatora trafia dokładnie w symbol).
    /// </summary>
    static Func<double, double> Mapa(UtProject ut, ZobObraz ob, ZobTor zt)
    {
        var anchors = new List<(double Km, double F)> { (zt.Ut.S0, 0), (zt.Ut.S1, 1) };
        foreach (ZobRodzaj rd in Enum.GetValues<ZobRodzaj>())
            foreach (var s in ZobSymbole.Lista(ob, rd))
                if (ZobSymbole.Miejsce(ut, s.Ut) is var (tr, km) && tr == zt.Ut.Tor && ZobSymbole.NaTorze(zt, s.P))
                    anchors.Add((km, ZobTory.Rzut(zt.Pkt, s.P[0], s.P[1]).Frac));
        anchors = anchors.OrderBy(a => a.Km).ToList();
        return km =>
        {
            if (km <= anchors[0].Km) return anchors[0].F;
            for (int i = 1; i < anchors.Count; i++)
                if (km <= anchors[i].Km)
                {
                    double d = anchors[i].Km - anchors[i - 1].Km;
                    return d < 1e-9 ? anchors[i].F : anchors[i - 1].F + (anchors[i].F - anchors[i - 1].F) * (km - anchors[i - 1].Km) / d;
                }
            return anchors[^1].F;
        };
    }

    /// <summary>Fragment łamanej między ułamkami długości f0 i f1 (także odwrotnie): punkt początkowy, wierzchołki po drodze, punkt końcowy.</summary>
    static List<(double X, double Y)> Fragment(ZobTor zt, double f0, double f1)
    {
        var p = zt.Pkt;
        var cum = new List<double> { 0 };
        for (int i = 1; i < p.Count; i++) cum.Add(cum[^1] + Math.Sqrt(Math.Pow(p[i][0] - p[i - 1][0], 2) + Math.Pow(p[i][1] - p[i - 1][1], 2)));
        double total = cum[^1];
        (double X, double Y) At(double f)
        {
            double L = Math.Clamp(f, 0, 1) * total;
            for (int i = 1; i < p.Count; i++)
                if (L <= cum[i] + 1e-9)
                {
                    double seg = cum[i] - cum[i - 1], t = seg < 1e-9 ? 0 : (L - cum[i - 1]) / seg;
                    return (Math.Round(p[i - 1][0] + (p[i][0] - p[i - 1][0]) * t, 2), Math.Round(p[i - 1][1] + (p[i][1] - p[i - 1][1]) * t, 2));
                }
            return (p[^1][0], p[^1][1]);
        }
        bool rev = f1 < f0; double lo = Math.Min(f0, f1), hi = Math.Max(f0, f1);
        var res = new List<(double X, double Y)> { At(lo) };
        for (int i = 1; i < p.Count - 1; i++)
        {
            double f = total > 0 ? cum[i] / total : 0;
            if (f > lo + 1e-9 && f < hi - 1e-9) res.Add((p[i][0], p[i][1]));
        }
        res.Add(At(hi));
        if (rev) res.Reverse();
        return res;
    }
}

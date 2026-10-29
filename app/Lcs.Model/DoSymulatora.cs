using System.Text.Json.Nodes;

namespace Lcs.Model;

/// <summary>
/// Przekształcenie UT + zobrazowanie → model symulatora (<c>station.json</c>, etap 9 – PLAN_EDYTOR_UT.md § 5).
/// Topologia (węzły, tory, zwrotnice, odcinki, sygnalizatory, szlaki) powstaje z UT: każdy tor UT dzieli się na kawałki między
/// rozjazdami, urządzeniami i granicami odcinków; długości – km UT. Geometria pulpitu („display”) – z obrazów zobrazowania: punkt
/// na torze obrazu wynika z km UT, interpolowanego między symbolami narysowanymi przez człowieka (kolejność sprawdza „Sprawdź”).
/// Plan schematyczny (korzeń pliku) – z geometrii UT wzdłuż osi schematu. Tablicę przebiegów projektuje człowiek w edytorze
/// zobrazowania (<see cref="ObrProject.Przebiegi"/> → pole „przebiegi”, symulator bierze tylko je); bez projektu symulator wyznacza
/// przebiegi z topologii jak dla istniejących układów. Zależności specjalne – plik &lt;układ&gt;.zaleznosci.json w symulatorze.
/// Nazwy obiektów: kod posterunku z UT + „:” + nazwa (np. „MT:A”, „MT:1”), odcinki szlaku – sama nazwa (jak w dotychczasowych plikach).
/// </summary>
public static class DoSymulatora
{
    /// <summary>Wynik: plik symulatora i uwagi (czego nie przeniesiono albo co uzupełniono).</summary>
    public record Wynik(JsonObject Json, List<string> Uwagi)
    {
        /// <summary>Id urządzenia UT → nazwa sygnalizatora w symulatorze („MT:A”).</summary>
        public Dictionary<string, string> Sygnaly { get; init; } = new();
        /// <summary>Tor symulatora (id krawędzi) → tor UT i km na nim (podświetlenie drogi przebiegu na obrazie).</summary>
        public Dictionary<string, (string Tor, double S0, double S1)> Krawedzie { get; init; } = new();
    }

    const double Tol = 0.5;           // [m] – punkty toru bliżej niż Tol to ten sam węzeł

    public static Wynik Buduj(UtProject ut, ZobProject zob, string source = null, List<ObrPrzebieg> przebiegi = null)
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
        var kraw = new Dictionary<string, (string Tor, double S0, double S1)>();
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

        // rozjazdy krzyżowe i skrzyżowania (decyzje właściciela 2026-10-19, UtKrzyzowe.cs): St – węzeł krzyża na obu torach; Rkpd i Rkp –
        // zwrotnice „Nab” / „Ncd” na końcach zwrócone ostrzami (węzeł na obu torach), wnętrze toru B pominięte; Rkp – zakaz jazdy przez brakujący przejazd
        var krSw = new List<(UtCrossing C, string K, string Node, string Track, double S)>();
        var krRkp = new List<(UtCrossing C, string Ab, string Cd)>();
        var skip = new List<(string Track, double A, double B)>();
        foreach (var c in ut.Crossings.Where(c => pts.ContainsKey(c.TrackA) && pts.ContainsKey(c.TrackB)))
        {
            var kind = Krzyzowe.Rodzaj(c);
            var nr = c.Number.Length > 0 ? c.Number : "X";
            if (c.Number.Length == 0) uw.Add($"{(kind == RozjazdTyp.St ? "skrzyżowanie" : "rozjazd krzyżowy")} bez numeru (tor {Nr(c.TrackA)} km {c.SA:0}) – w symulatorze „{nr}”; wpisz numer w UT");
            if (kind == RozjazdTyp.St)
            {
                var x = NodeOn(c.TrackA, c.SA, $"{Kod(c.TrackA)}:kr{nr}");
                Put(c.TrackB, c.SB, x); nodeKind[x] = ("crossing", nr);
            }
            if (kind == RozjazdTyp.St) continue;
            var kn = new Dictionary<string, string>();
            foreach (var k in Krzyzowe.Konce)
            {
                var w = Krzyzowe.Wezly(ut, c, k);
                double Cl(string tr, double s)
                {
                    double len = r[tr].Length;
                    if (s < -Tol || s > len + Tol) uw.Add($"rozjazd krzyżowy {nr}{k}: koniec poza torem {Nr(tr)} (km {s:0.#}, długość {len:0.#}) – przesunięty na koniec toru");
                    return Math.Clamp(s, 0, len);
                }
                var id = NodeOn(w[0].Track, Cl(w[0].Track, w[0].S), $"{Kod(w[0].Track)}:sw{nr}{k}");
                foreach (var (tr, s) in w.Skip(1)) Put(tr, Cl(tr, s), id);
                nodeKind[id] = ("switch", nr + k);
                krSw.Add((c, k, id, w[0].Track, w[0].S));
                kn[k] = id;
            }
            double a = Krzyzowe.A(c); skip.Add((c.TrackB, c.SB - a, c.SB + a));
            if (kind == RozjazdTyp.Rkp) krRkp.Add((c, kn[Krzyzowe.Ab], kn[Krzyzowe.Cd]));
        }

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
        // końce szlaków w środku toru (granica obszaru w dowolnym miejscu)
        foreach (var e in ut.Lines.SelectMany(l => l.Konce).Where(e => e.Semafor == null && e.S != null && e.Track != null && pts.ContainsKey(e.Track)))
            NodeOn(e.Track, Math.Clamp(e.S.Value, 0, r[e.Track].Length));

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

        // ---------- perony: km na torze (krawędź przy Track; przy Track2 – rzut końców peronu) ----------
        IEnumerable<(double A, double B)> PeronyToru(string track)
        {
            foreach (var pl in ut.Platforms)
            {
                if (pl.Track == track) yield return (Math.Min(pl.S0, pl.S1), Math.Max(pl.S0, pl.S1));
                else if (pl.Track2 == track && r.ContainsKey(pl.Track) && r.ContainsKey(track))
                {
                    double a = r[track].Nearest(r[pl.Track].At(Math.Clamp(pl.S0, 0, r[pl.Track].Length)).P).S, b = r[track].Nearest(r[pl.Track].At(Math.Clamp(pl.S1, 0, r[pl.Track].Length)).P).S;
                    yield return (Math.Min(a, b), Math.Max(a, b));
                }
            }
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
                // wnętrze rozjazdu krzyżowego podwójnego na torze B – w symulatorze łącznik ab–cd to kawałek toru A
                if (skip.Any(k => k.Track == t.Id && mid > k.A && mid < k.B))
                {
                    if (i > 1 && l[i - 1].S > skip.First(k => k.Track == t.Id && mid > k.A && mid < k.B).A + Tol) uw.Add($"tor {Nr(t.Id)} km {l[i - 1].S:0}: węzeł wewnątrz rozjazdu krzyżowego – pominięty");
                    continue;
                }
                var sec = SecName(t.Id, mid, out bool szlak, out var line);
                var id = $"e{ne++}";
                kraw[id] = (t.Id, s0, s1);
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
                // tor przy peronie UT (postój pociągu, okno numeru) – kawałek zachodzi na peron na tym torze albo na drugiej krawędzi peronu wyspowego
                if (PeronyToru(t.Id).Any(x => Math.Min(s1, x.B) - Math.Max(s0, x.A) > 1)) e["peron"] = true;
                // pochylenie [‰] od A do B (rosnący km) – średnie z niwelety UT na kawałku; bez niwelety – brak pola
                if (t.Profile is { Count: >= 2 } prof)
                {
                    double z0 = Profil.Z(prof, s0), z1 = Profil.Z(prof, s1);
                    if (!double.IsNaN(z0) && !double.IsNaN(z1)) e["poch"] = Math.Round((z1 - z0) / (s1 - s0) * 1000, 1);
                }
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

        // Rkp: pary torów bez przejazdu (w obie strony) – ramię zwrotnicy ab i ramię zwrotnicy cd po stronach brakującego łuku
        var noPass = new JsonArray();
        foreach (var (c, nab, ncd) in krRkp)
        {
            if (Krzyzowe.BrakPrzejazdu(c) is not var (tAb, tCd)) continue;
            string Arm(string node, string tr, string other) =>
                edgeInfo.FirstOrDefault(e => e.Track == tr && (e.A == node || e.B == node) && e.A != other && e.B != other).Id;
            var x = Arm(nab, tAb, ncd); var y = Arm(ncd, tCd, nab);
            if (x != null && y != null) noPass.Add(new JsonArray(x, y));
            else uw.Add($"rozjazd krzyżowy {c.Number}: nie znaleziono ramion brakującego przejazdu – w symulatorze jak Rkpd (wszystkie jazdy)");
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

        // zwrotnice rozjazdów krzyżowych: koniec ab / cd (Ie-104 § 8 pkt 9), w odcinku rozjazdu
        foreach (var (c, k, node, tr, s) in krSw)
        {
            var label = Krzyzowe.Numer(c, k);
            var key = $"{Kod(tr)}:{label}";
            if (c.Number.Length == 0 || switches.ContainsKey(key)) { uw.Add($"rozjazd krzyżowy bez numeru albo z powtórzonym numerem ({key}) – zwrotnica pominięta"); continue; }
            var typ = Katalog.Find(c.Type);
            var o = new JsonObject { ["num"] = key, ["node"] = node, ["label"] = label, ["sec"] = SecName(c.TrackA, c.SA, out _, out _), ["post"] = Kod(tr), ["kind"] = c.Type };
            if (typ != null) { o["radius"] = typ.R; o["ratio"] = $"1:{typ.N:0.##}".Replace('.', ','); if (typ.Vbok > 0) o["vbok"] = typ.Vbok; }
            switches[key] = o;
        }

        // ---------- szlaki ----------
        var blocks = new JsonObject(); var boundaries = new JsonObject(); var farEnds = new JsonObject();
        var lineOfEntry = new Dictionary<string, string>();
        foreach (var ln in ut.Lines)
        {
            if (!drogi.TryGetValue(ln.Id, out var dr) || dr.Err != null) { uw.Add($"szlak {ln.Numer}: {dr?.Err ?? "brak drogi"} – pominięty"); continue; }
            var ends = new JsonArray(); string far = null, nbr = "", post = ""; int defDir = 0, evenDir = 0;
            foreach (var en in ln.Konce)
            {
                int ki = ln.Konce.IndexOf(en);
                if (en.Semafor != null && ut.Devices.FirstOrDefault(d => d.Id == en.Semafor) is UtDevice sg && devNode.TryGetValue(sg.Id, out var bn))
                {
                    int outward = -sg.Dir;
                    post = Kod(sg.Track);
                    var eo = new JsonObject { ["boundary"] = bn, ["outward"] = outward, ["entrySig"] = $"{post}:{sg.Name}", ["post"] = post, ["label"] = en.Oznaczenie.Length > 0 ? en.Oznaczenie : ln.Numer };
                    // tor w stronę szlaku (za semaforem, na jego torze UT) – jednoznaczny także w styku torów o przeciwnym km
                    if (edgeInfo.FirstOrDefault(e => e.Track == sg.Track && (outward > 0 ? e.A == bn : e.B == bn)).Id is string oe) eo["outEdge"] = oe;
                    // semafor na końcu swojego toru – szlak po drugiej stronie styku: jedyny inny tor w węźle
                    else if (edgeInfo.FirstOrDefault(e => e.Track == sg.Track && (outward > 0 ? e.B == bn : e.A == bn)).Id is string ie &&
                             edgeInfo.Where(e => e.Id != ie && (e.A == bn || e.B == bn)).ToList() is { Count: 1 } inne) eo["outEdge"] = inne[0].Id;
                    ends.Add(eo);
                    boundaries[bn] = new JsonObject { ["szlak"] = ln.Numer };
                    lineOfEntry[sg.Id] = ln.Numer;
                    if (defDir == 0) defDir = outward;
                    // pociągi parzyste jadą od końca „od” do końca „do”: przy „od” wyjeżdżają z posterunku (na zewnątrz), przy „do” wjeżdżają
                    if (ln.Parzysty is 1 or 2 && evenDir == 0) { int od = ln.Parzysty - 1; evenDir = ki == od ? outward : -outward; }
                }
                else if (en.Track != null && pts.ContainsKey(en.Track))
                {
                    far = NodeOn(en.Track, en.S is double es ? Math.Clamp(es, 0, r[en.Track].Length) : en.AtStart ? 0 : r[en.Track].Length);
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
                ["name"] = ln.Nazwa, ["nameShort"] = ln.NazwaSkr,
            };
            if (evenDir != 0) b["evenDir"] = evenDir;
            if (far != null) { b["far"] = far; farEnds[far] = new JsonObject { ["szlak"] = ln.Numer, ["nbr"] = nbr }; }
            blocks[ln.Numer] = b;
        }

        // ---------- sygnalizatory, wykolejnice, kozły ----------
        var signals = new JsonObject(); var derailers = new JsonObject(); var bumpers = new JsonObject(); var w5 = new JsonObject();
        var sigKey = new Dictionary<string, string>();      // id urządzenia UT → nazwa sygnalizatora w symulatorze (do „repeats”)
        foreach (var d in ut.Devices.Where(d => devNode.ContainsKey(d.Id)))
        {
            var node = devNode[d.Id]; var post = Kod(d.Track);
            if (d.Kind == UtDevice.Koziol) { bumpers[node] = new JsonObject(); continue; }
            if (d.Kind is UtDevice.W4 or UtDevice.W8 or UtDevice.W9 or UtDevice.Ukres) continue;   // niżej: wskaźniki
            // W5 – granica przetaczania: koniec manewru na szlak (bez W5 – semafor wjazdowy)
            if (d.Kind == UtDevice.W5) { if (d.Dir != 0) w5[node] = new JsonObject { ["dir"] = d.Dir, ["label"] = d.Name, ["post"] = post }; continue; }
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
            // tor przed sygnalizatorem (kawałek jego toru UT w kierunku Dir; na końcu toru – tor za nim, „behind”): w styku dwóch torów UT
            // o przeciwnym km symulator po ujednoliceniu kierunku torów nie odgadnie z samego ±1, w którą stronę patrzy sygnalizator
            var ahead = edgeInfo.FirstOrDefault(e => e.Track == d.Track && (d.Dir > 0 ? e.A == node : e.B == node)).Id;
            var behind = edgeInfo.FirstOrDefault(e => e.Track == d.Track && (d.Dir > 0 ? e.B == node : e.A == node)).Id;
            if (ahead != null) o["ahead"] = ahead; else if (behind != null) o["behind"] = behind;
            if (lineOfEntry.TryGetValue(d.Id, out var szl)) o["szlak"] = szl;
            else if (type == "odstepowy")
            {
                var l = LineAt(d.Track, d.S + 0.01) ?? LineAt(d.Track, d.S - 0.01);
                if (l == null || !blocks.ContainsKey(l.Numer)) { uw.Add($"semafor odstępowy {d.Name} poza szlakiem – pominięty"); continue; }
                o["szlak"] = l.Numer;
            }
            if (type == "wjazdowy" && !lineOfEntry.ContainsKey(d.Id)) uw.Add($"semafor wjazdowy {d.Name} nie jest końcem żadnego szlaku w UT");
            var lights = UtDevice.Czynne(d.LightsOrDefault);     // komory nieczynne nie świecą – bez nich
            if (d.Kind == UtDevice.Semafor)
            {
                o["lamps"] = lights.Count;
                // pasy świetlne i W21 – dobór sygnału do prędkości przebiegu (Ie-1); „S” – W21 świetlny, cyfra – tablica
                o["stripes"] = (d.StripeP ? "P" : "") + (d.StripeZ ? "Z" : "");
                if (d.W21Swietlny) o["w21"] = "S"; else if (!string.IsNullOrEmpty(d.W21)) o["w21"] = d.W21;
            }
            if (type == "wyjazdowy" && !lights.Any(x => x.C == 'B')) o["shunt"] = false;
            if (d.Side < 0) o["side"] = "L";
            if (d.MastOrDefault is UtDevice.MastWysiegnik or UtDevice.MastBramka or UtDevice.MastKarzel) o["mount"] = d.MastOrDefault;
            signals[key] = o; sigKey[d.Id] = key;
        }
        // tarcze ostrzegawcze i powtarzacze: semafor powtarzany (albo szlak – informacja z sąsiedniej stacji)
        foreach (var d in ut.Devices.Where(d => d.Powtarzajacy && sigKey.ContainsKey(d.Id)))
        {
            var o = (JsonObject)signals[sigKey[d.Id]];
            if (string.IsNullOrEmpty(d.Powtarza)) { uw.Add($"{ZobSymbole.Nazwa(ut, d.Id)}: nie wskazano semafora powtarzanego – na pulpicie bez wskazania (ustaw „Powtarza” w UT)"); continue; }
            if (sigKey.TryGetValue(d.Powtarza, out var mk)) o["repeats"] = mk;
            else if (ut.Lines.FirstOrDefault(l => l.Id == d.Powtarza) is UtLine pl && blocks.ContainsKey(pl.Numer)) o["repeats"] = pl.Numer;
            else uw.Add($"{ZobSymbole.Nazwa(ut, d.Id)}: semafor powtarzany (albo szlak) nie trafia do symulatora – pominięty");
        }

        // ---------- wskaźniki W4, W8, W9, W17 (Ie-1) ----------
        var w4 = new JsonObject(); var ukres = new JsonArray();
        var edgeJson = edges.OfType<JsonObject>().ToDictionary(x => (string)x["id"]);
        var edgesAt = edgeInfo.SelectMany(e => new[] { (N: e.A, E: e), (N: e.B, E: e) }).ToLookup(x => x.N, x => x.E);
        var turnAt = ut.Turnouts.Where(z => swNode.ContainsKey(z.Id)).GroupBy(z => swNode[z.Id]).ToDictionary(g => g.Key, g => g.First());
        // dalsza jazda z węzła: zwrotnica – z ostrza na wprost albo na bok, z toru na wprost / zwrotnego tylko na ostrze; inne węzły – pozostałe tory
        List<(string Id, string Track, double S0, double S1, string A, string B)> Dalej(string node, string from)
        {
            var all = edgesAt[node].Where(e => e.Id != from).ToList();
            if (from == null || !turnAt.TryGetValue(node, out var z)) return all;
            bool Ostrze((string Id, string Track, double S0, double S1, string A, string B) e) => e.Track == z.Track && (z.Dir > 0 ? e.B == node : e.A == node);
            return Ostrze(edgeInfo.First(e => e.Id == from)) ? all : all.Where(Ostrze).ToList();
        }
        // kierunek jazdy względem km toru UT urządzenia w jego węźle: przyjazd krawędzią „from”
        int KierunekWzgledem(UtDevice d, string node, string from)
        {
            var fe = edgeInfo.First(e => e.Id == from);
            if (fe.Track == d.Track) return fe.B == node ? 1 : -1;
            var dalej = Dalej(node, from).FirstOrDefault(e => e.Track == d.Track);
            return dalej.Id == null ? 0 : dalej.A == node ? 1 : -1;
        }
        var w9 = ut.Devices.Where(d => d.Kind == UtDevice.W9 && devNode.ContainsKey(d.Id)).ToList();
        var nodeW9 = w9.ToLookup(d => devNode[d.Id]);
        // przejście w kierunku dir (km toru urządzenia) od węzła urządzenia: odwiedza krawędzie z węzłem wejścia, aż stop(węzeł, krawędź przyjazdu) – do maxLen m
        void Przejdz(UtDevice d, double maxLen, Func<string, string, bool> stop, Action<string, string> visit, out bool urwane)
        {
            urwane = false; var node = devNode[d.Id];
            var start = edgeInfo.FirstOrDefault(e => e.Track == d.Track && (d.Dir > 0 ? e.A == node : e.B == node));
            if (start.Id == null) { urwane = true; return; }
            var q = new Stack<(string Node, (string Id, string Track, double S0, double S1, string A, string B) E, double Len)>();
            q.Push((node, start, 0)); var seen = new HashSet<string>();
            bool cut = false;
            while (q.Count > 0)
            {
                var (n, e, len) = q.Pop();
                if (!seen.Add(e.Id + n)) continue;
                visit(e.Id, n);
                var nn = e.A == n ? e.B : e.A; double l2 = len + (e.S1 - e.S0);
                if (stop(nn, e.Id)) continue;
                if (l2 > maxLen || farEnds.ContainsKey(nn)) { cut = true; continue; }
                foreach (var x in Dalej(nn, e.Id)) q.Push((nn, x, l2));
            }
            urwane = cut;
        }
        void Ogranicz(string eid, string from, int v, bool c)
        {
            var o = edgeJson[eid];
            var lim = o["vlim"] as JsonObject ?? (JsonObject)(o["vlim"] = new JsonObject());
            int cur = lim[from] is JsonNode old ? (int)old : int.MaxValue;
            if (cur < v) return;
            lim[from] = v;
            // niższe ograniczenie bez C zastępuje wcześniejsze z C; to samo z C (drugi przebieg) – dokłada znacznik
            if (c) ((JsonObject)(o["vlimC"] ??= new JsonObject()))[from] = true;
            else if (cur > v) (o["vlimC"] as JsonObject)?.Remove(from);
        }
        foreach (var d in ut.Devices.Where(d => devNode.ContainsKey(d.Id)))
        {
            var node = devNode[d.Id];
            if (d.Kind == UtDevice.W4) { if (d.Dir != 0) w4[node] = new JsonObject { ["dir"] = d.Dir, ["track"] = Nr(d.Track), ["label"] = d.Name, ["ahead"] = edgeInfo.FirstOrDefault(e => e.Track == d.Track && (d.Dir > 0 ? e.A == node : e.B == node)).Id }; continue; }
            if (d.Kind == UtDevice.Ukres) { ukres.Add(node); continue; }
            if (d.Kind == UtDevice.W9 && d.Vw is int v9)
            {
                // od W9 w jego kierunku do W9 zwróconego przeciwnie (strona końca widziana z tej jazdy); litera C – na tablicy końca
                bool c = false;
                Przejdz(d, 5000, (n, from) =>
                {
                    var end = nodeW9[n].FirstOrDefault(x => x.Id != d.Id && KierunekWzgledem(x, n, from) == -x.Dir);
                    if (end != null) c |= end.W9C;
                    return end != null;
                }, (eid, from) => Ogranicz(eid, from, v9, false), out bool urw);
                if (c) Przejdz(d, 5000, (n, from) => nodeW9[n].Any(x => x.Id != d.Id && KierunekWzgledem(x, n, from) == -x.Dir), (eid, from) => Ogranicz(eid, from, v9, true), out _);
                if (urw) uw.Add($"W9 {d.Name} ({v9} km/h, tor {Nr(d.Track)} km {d.S:0}): brak W9 końca ograniczenia w ciągu 5 km albo do granicy obszaru – ograniczenie do tego miejsca");
            }
            if (d.Kind == UtDevice.W8 && d.Vw is int v8)
            {
                // W8 lit. e: bez W9 początku ograniczenia w tym kierunku (do 3 km) – ograniczenie na stacji do jej minięcia (od pierwszego toru stacyjnego)
                bool maW9 = false;
                Przejdz(d, 3000, (n, from) => { if (nodeW9[n].Any(x => x.Vw != null && KierunekWzgledem(x, n, from) == x.Dir)) maW9 = true; return maW9; }, (_, _) => { }, out _);
                if (maW9) continue;
                bool naStacji = false;
                Przejdz(d, 6000, (n, from) =>
                {
                    bool st = edgeJson[from]["szlak"] == null; naStacji |= st;
                    // za stacją tor szlakowy – koniec ograniczenia
                    return naStacji && Dalej(n, from).All(x => edgeJson[x.Id]["szlak"] != null) && Dalej(n, from).Count > 0;
                }, (eid, from) => { if (edgeJson[eid]["szlak"] == null) Ogranicz(eid, from, v8, false); }, out _);
                uw.Add($"W8 {d.Name} ({v8} km/h): bez W9 za nim – w symulatorze ograniczenie do minięcia całej stacji (Ie-1, W8 lit. e)");
            }
        }
        // W24 na semaforze (Ie-1 § W24): zapala się z sygnałem zezwalającym na wyjazd na tor szlaku w kierunku przeciwnym do zasadniczego
        foreach (var d in ut.Devices.Where(d => d.Kind == UtDevice.Semafor && d.W24 && sigKey.ContainsKey(d.Id)))
            ((JsonObject)signals[sigKey[d.Id]])["w24"] = true;

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
        // obraz poglądowy – tylko w edytorze; na pulpicie zakładka poglądowa jak dotąd (decyzja właściciela 2026-10-19, plan § 10 pkt 4)
        var detal = zob?.Obrazy.Where(o => o.Rodzaj != ZobObraz.Pogladowy).ToList() ?? new();
        if (zob != null && zob.Obrazy.Count > detal.Count) uw.Add($"obraz poglądowy ({string.Join(", ", zob.Obrazy.Where(o => o.Rodzaj == ZobObraz.Pogladowy).Select(o => o.Nazwa))}) – tylko w edytorze; na pulpicie zakładka „Obraz poglądowy” pokazuje pomniejszone obrazy szczegółowe");
        if (detal.Count > 0)
        {
            // geometria główna: każdy tor i węzeł z pierwszego obrazu, na którym jest; geometria każdej strony (pageGeo) – wszystko z tej strony,
            // więc obiekt narysowany na dwóch obrazach pulpit pokazuje na obu (decyzja właściciela 2026-10-19, plan § 10 pkt 5)
            var main = PustaGeometria();
            var szlakNodes = edges.OfType<JsonObject>().Where(e => e["szlak"] != null).SelectMany(e => new[] { (string)e["a"], (string)e["b"] }).ToHashSet();
            var pageGeo = new JsonObject();
            string secMode = null;
            int x0 = 0;
            foreach (var ob in detal)
            {
                var codes = ob.Posterunki.Select(id => ut.Posts.FirstOrDefault(p => p.Id == id)?.Kod).Where(k => k != null).ToList();
                pages.Add(new JsonObject { ["id"] = ob.Id, ["name"] = ob.Nazwa, ["x0"] = x0, ["x1"] = x0 + ob.Szer, ["posts"] = new JsonArray(codes.Select(c => (JsonNode)c).ToArray()) });
                var pg = PustaGeometria();
                var dn = pg["nodes"].AsObject(); var de = pg["edges"].AsObject();
                foreach (var ei in edgeInfo)
                {
                    double mid = (ei.S0 + ei.S1) / 2;
                    var zt = ZobSymbole.Kawalki(ob, ei.Track, mid).FirstOrDefault();
                    if (zt == null) continue;
                    var map = Mapa(ut, ob, zt);
                    var line = Fragment(zt, map(ei.S0), map(ei.S1));
                    de[ei.Id] = new JsonArray(line.Select(q => (JsonNode)new JsonArray(q.X + x0, q.Y)).ToArray());
                    if (!dn.ContainsKey(ei.A)) dn[ei.A] = new JsonArray(line[0].X + x0, line[0].Y);
                    if (!dn.ContainsKey(ei.B)) dn[ei.B] = new JsonArray(line[^1].X + x0, line[^1].Y);
                    if (zt.BezKontroli) pg["dashedEdges"].AsArray().Add(ei.Id);
                }
                // przerwy na szlaku: węzły przerw i sygnalizatorów postawionych przez człowieka na obrazie; inne granice odcinków
                // i semafory odstępowe bez symbolu pulpit rysuje w sposób ciągły (odstępy jak na obrazie)
                var gaps = new JsonArray();
                foreach (var s in ob.Przerwy.Concat(ob.Sygnalizatory).Where(s => s.P != null && s.Ut != null))
                    if (devNode.TryGetValue(s.Ut, out var gn) && szlakNodes.Contains(gn) && dn.ContainsKey(gn) && !gaps.Any(x => (string)x == gn)) gaps.Add(gn);
                pg["szlakGaps"] = gaps;
                foreach (var n in ob.Napisy.Where(n => n.P != null && n.Tekst.Length > 0))
                    pg["labels"].AsArray().Add(new JsonObject { ["text"] = n.Tekst, ["x"] = n.P[0] + x0, ["y"] = n.P[1], ["anchor"] = n.Rodzaj == ZobNapis.Nastawnia ? "nastawnia" : "middle" });
                foreach (var pr in ob.Perony.Where(p => p.Prost is { Length: 4 }))
                {
                    var pl = ut.Platforms.FirstOrDefault(x => x.Id == pr.Ut); if (pl == null) continue;
                    var name = pl.Name.StartsWith("Peron", StringComparison.OrdinalIgnoreCase) ? pl.Name : $"Peron {pl.Name}".Trim();
                    pg["platforms"].AsArray().Add(new JsonObject { ["name"] = name, ["x"] = pr.Prost[0] + x0, ["y"] = pr.Prost[1], ["w"] = pr.Prost[2], ["h"] = pr.Prost[3] });
                }
                foreach (var s in ob.Przejazdy.Where(s => s.P != null))
                {
                    var lc = ut.LevelCrossings.FirstOrDefault(x => x.Id == s.Ut); if (lc == null) continue;
                    int up = s.Zasieg?[0] ?? 28, down = s.Zasieg?[1] ?? 28;
                    pg["crossings"].AsArray().Add(new JsonObject { ["name"] = lc.Name.Length > 0 ? lc.Name : UtLevelCrossing.CatName(lc.Cat), ["kind"] = "Y", ["x"] = s.P[0] + x0, ["y0"] = s.P[1] - up, ["y1"] = s.P[1] + down });
                }
                foreach (var s in ob.NumeryTorow.Where(s => s.P != null))
                    if (ut.Tracks.FirstOrDefault(t => t.Id == s.Ut) is UtTrack t && t.Number.Length > 0)
                        pg["trackLabels"].AsArray().Add(new JsonObject { ["text"] = t.Number, ["x"] = s.P[0] + x0, ["y"] = s.P[1] });
                foreach (var s in ob.Odcinki.Where(s => s.P != null))
                    if (ut.Sections.FirstOrDefault(x => x.Id == s.Ut) is UtSection sc && pts.ContainsKey(sc.Track))
                        pg["secLabels"].AsArray().Add(new JsonObject { ["sec"] = SecName(sc.Track, sc.S, out _, out _), ["x"] = s.P[0] + x0, ["y"] = s.P[1] });
                secMode ??= ob.OpisyOdcinkow;
                pg["secLabelsMode"] = ob.OpisyOdcinkow;
                foreach (var s in ob.Szlaki.Where(s => s.P != null))
                {
                    // indeks końca w „ends” symulatora: kolejne końce szlaku z semaforem wjazdowym (koniec na granicy obszaru to „far”)
                    var ln = ut.Lines.FirstOrDefault(x => x.Id == s.Ut); if (ln == null || !blocks.ContainsKey(ln.Numer)) continue;
                    int k = s.Koniec ?? 0, idx = 0;
                    for (int i = 0; i < k && i < ln.Konce.Count; i++) if (ln.Konce[i].Semafor != null) idx++;
                    if (k >= ln.Konce.Count || ln.Konce[k].Semafor == null) continue;
                    pg["blockMarks"].AsArray().Add(new JsonObject { ["block"] = ln.Numer, ["end"] = idx, ["x"] = s.P[0] + x0, ["y"] = s.P[1] });
                }
                // semafory wjazdowe bez trójkąta końca przebiegu (nowy edytor) – klucz tylko, gdy są
                foreach (var s in ob.Sygnalizatory.Where(s => s.BezKonca == true && s.Ut != null && sigKey.ContainsKey(s.Ut)))
                    (pg["noEndMark"] as JsonArray ?? (JsonArray)(pg["noEndMark"] = new JsonArray())).Add(sigKey[s.Ut]);
                if (pg["noEndMark"] is JsonArray nea)
                    foreach (var v in nea) (main["noEndMark"] as JsonArray ?? (JsonArray)(main["noEndMark"] = new JsonArray())).Add(v.DeepClone());
                // okna numeru pociągu dla szlaku (nowy edytor, E6) – klucz tylko, gdy są
                foreach (var s in ob.PolaPip?.Where(s => s.P != null) ?? Enumerable.Empty<ZobSymbol>())
                {
                    var ln = ut.Lines.FirstOrDefault(x => x.Id == s.Ut); if (ln == null || !blocks.ContainsKey(ln.Numer)) continue;
                    var tf = pg["trainFields"] as JsonArray ?? (JsonArray)(pg["trainFields"] = new JsonArray());
                    tf.Add(new JsonObject { ["block"] = ln.Numer, ["x"] = s.P[0] + x0, ["y"] = s.P[1], ["w"] = s.Zasieg?[0] ?? 88, ["h"] = s.Zasieg?[1] ?? 68 });
                }
                if (pg["trainFields"] is JsonArray tfa)
                    foreach (var v in tfa) (main["trainFields"] as JsonArray ?? (JsonArray)(main["trainFields"] = new JsonArray())).Add(v.DeepClone());
                // do geometrii głównej: tory i węzły jeszcze niezajęte, elementy Z4 strony
                foreach (var (id, v) in de) if (!main["edges"].AsObject().ContainsKey(id)) main["edges"][id] = v.DeepClone();
                foreach (var (id, v) in dn) if (!main["nodes"].AsObject().ContainsKey(id)) main["nodes"][id] = v.DeepClone();
                foreach (var key in new[] { "labels", "platforms", "crossings", "trackLabels", "secLabels", "blockMarks" })
                    foreach (var v in pg[key].AsArray()) main[key].AsArray().Add(v.DeepClone());
                var mg = main["szlakGaps"] as JsonArray ?? (JsonArray)(main["szlakGaps"] = new JsonArray());
                foreach (var v in gaps) if (!mg.Any(x => (string)x == (string)v)) mg.Add(v.DeepClone());
                foreach (var v in pg["dashedEdges"].AsArray()) if (!main["dashedEdges"].AsArray().Any(x => (string)x == (string)v)) main["dashedEdges"].AsArray().Add(v.DeepClone());
                pageGeo[ob.Id] = pg;
                x0 += ob.Szer;
            }
            int missing = edgeInfo.Count(e => !main["edges"].AsObject().ContainsKey(e.Id));
            if (missing > 0) uw.Add($"{missing} kawałków torów UT nie ma na żadnym obrazie – na pulpicie ich nie widać");
            int shared = edgeInfo.Count(e => pageGeo.Count(pp => pp.Value["edges"].AsObject().ContainsKey(e.Id)) > 1);
            if (shared > 0) uw.Add($"{shared} kawałków torów jest na kilku obrazach – na pulpicie na każdym z nich");
            main["secLabelsMode"] = secMode ?? ZobObraz.OpisNaZadanie;
            display = main;
            if (detal.Count > 1) display["pageGeo"] = pageGeo;
        }
        else uw.Add("brak zobrazowania – pulpit pokaże plan z UT");

        var sections = new JsonObject();
        foreach (var (k, v) in secKind) if (v == UtSection.ObwodTorowy) sections[k] = new JsonObject { ["detection"] = "ot" };
        // tor stacyjny (okno numeru pociągu, numer toru na pulpicie – jak odcinki „:JT” dawnych układów): odcinek poza szlakiem, na torach
        // z numerem, bez zwrotnic i krzyży
        foreach (var g in edges.OfType<JsonObject>().GroupBy(e => (string)e["sec"]))
            if (g.All(e => e["szlak"] == null && ((string)e["track"]).Length > 0 && !new[] { (string)e["a"], (string)e["b"] }.Any(n => nodeKind.TryGetValue(n, out var nk) && nk.Kind is "switch" or "crossing")))
                (sections[g.Key] ??= new JsonObject())["tor"] = true;
        var code = ut.Posts.FirstOrDefault()?.Kod ?? "UT";
        var root = new JsonObject
        {
            ["name"] = ut.Name, ["code"] = code, ["width"] = W, ["height"] = H, ["orient"] = "ab", ["source"] = source ?? "UT + zobrazowanie",
            ["detection"] = "lo", ["posts"] = posts, ["pages"] = pages,
            ["nodes"] = nodes, ["edges"] = edges, ["switches"] = switches, ["signals"] = signals, ["derailers"] = derailers,
            ["blocks"] = blocks, ["boundaries"] = boundaries, ["farEnds"] = farEnds, ["trackEnds"] = new JsonObject(), ["bumpers"] = bumpers, ["w5"] = w5, ["w4"] = w4, ["ukres"] = ukres,
            ["sections"] = sections, ["platforms"] = platforms, ["labels"] = new JsonArray(), ["crossings"] = crossings, ["dashed"] = new JsonArray(),
            ["routeRules"] = new JsonArray(),
        };
        if (noPass.Count > 0) root["noPass"] = noPass;
        if (display != null) root["display"] = display;
        if (przebiegi != null) root["przebiegi"] = PrzebiegiJson(przebiegi);
        return new Wynik(root, uw) { Sygnaly = sigKey, Krawedzie = kraw };
    }

    /// <summary>Pusta geometria pulpitu (główna albo jednej strony).</summary>
    static JsonObject PustaGeometria() => new()
    {
        ["nodes"] = new JsonObject(), ["edges"] = new JsonObject(), ["labels"] = new JsonArray(), ["platforms"] = new JsonArray(), ["crossings"] = new JsonArray(),
        ["trackLabels"] = new JsonArray(), ["secLabels"] = new JsonArray(), ["blockMarks"] = new JsonArray(), ["dashedEdges"] = new JsonArray(),
    };

    /// <summary>
    /// Km toru UT → ułamek długości łamanej toru obrazu: liniowo między punktami zaczepienia – końcami kawałka i symbolami obiektów UT
    /// tego toru narysowanymi na łamanej (człowiek stawia je w dowolnych miejscach, więc węzeł symulatora trafia dokładnie w symbol).
    /// </summary>
    static Func<double, double> Mapa(UtProject ut, ZobObraz ob, ZobTor zt)
    {
        var anchors = new List<(double Km, double F)> { (zt.Ut.S0, 0), (zt.Ut.S1, 1) };
        foreach (ZobRodzaj rd in Enum.GetValues<ZobRodzaj>().Where(ZobSymbole.Kotwica))
            foreach (var s in ZobSymbole.Lista(ob, rd))
                if (s.P != null && ZobSymbole.Miejsce(ut, s) is var (tr, km) && tr == zt.Ut.Tor && ZobSymbole.NaTorze(zt, s.P))
                    anchors.Add((km, ZobTory.Rzut(zt.Pkt, s.P[0], s.P[1]).Frac));
        // środek skrzyżowania (St) – w punkcie, w którym tory obrazu się krzyżują
        foreach (var c in ut.Crossings.Where(c => Krzyzowe.Rodzaj(c) == RozjazdTyp.St && (c.TrackA == zt.Ut.Tor || c.TrackB == zt.Ut.Tor)))
        {
            bool onA = c.TrackA == zt.Ut.Tor; double km = onA ? c.SA : c.SB, kmO = onA ? c.SB : c.SA;
            if (km < Math.Min(zt.Ut.S0, zt.Ut.S1) || km > Math.Max(zt.Ut.S0, zt.Ut.S1)) continue;
            foreach (var o2 in ZobSymbole.Kawalki(ob, onA ? c.TrackB : c.TrackA, kmO))
                if (ZobTory.Przeciecie(zt.Pkt, o2.Pkt) is var (ix, iy)) { anchors.Add((km, ZobTory.Rzut(zt.Pkt, ix, iy).Frac)); break; }
        }
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

    /// <summary>Tablica przebiegów z edytora zobrazowania → pole „przebiegi” pliku symulatora (start, koniec, rodzaj, zwrotnice, zasadniczy).</summary>
    public static JsonArray PrzebiegiJson(IEnumerable<ObrPrzebieg> przebiegi)
    {
        var a = new JsonArray();
        foreach (var p in przebiegi)
        {
            var o = new JsonObject { ["type"] = p.Typ, ["start"] = p.Od, ["end"] = p.Do };
            if (!string.IsNullOrEmpty(p.Zwr)) o["sw"] = p.Zwr;
            if (p.Zasadniczy) o["basic"] = true;
            if (p.V > 0) o["v"] = p.V;
            a.Add(o);
        }
        return a;
    }
}

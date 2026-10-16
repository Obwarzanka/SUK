using System.Text.RegularExpressions;
using LcsWidzew.Model;

namespace LcsWidzew.Sim;

/// <summary>
/// Rdzeń symulacji: stan urządzeń, warstwa zależnościowa (uproszczona), rejestracja zdarzeń.
/// Zasady wg Ie-20 (obsługa) i Ie-104 (zobrazowanie, polecenia, alarmy).
/// </summary>
public partial class Simulator
{
    public static class Cfg
    {
        public const double SwTime = 5, SwTimeout = 12, SzTime = 90, SpecialConfirm = 60, SpecialMinWait = 5, TimeRelease = 120,
            BlockRelease = 3, AzkTime = 5, NbrDelay = 6, ReqAlarmAfter = 180, ParamTimeout = 60;
    }

    public Station L { get; }
    public double T;
    public double StartClock = 6 * 3600;
    public string Page;                       // aktualnie wyświetlany obraz (kontekst adresów poleceń, Ie-104 § 11 ust. 6)
    public Dictionary<string, SecState> Secs = new();
    public Dictionary<string, SwState> Sws = new();
    public Dictionary<string, SigState> Sigs = new();
    public Dictionary<string, DerState> Ders = new();
    public Dictionary<string, BlockState> Blocks = new();
    public Dictionary<string, Route> Routes = new();
    public Dictionary<string, List<RouteDef>> RouteTable = new();
    public PowerState Power = new();
    public Pending Pending;
    public List<BookEntry> Book = new();
    public List<Pending> BookDrafts = new();
    public User User;
    public Stats Stats = new();
    int seq = 1;

    /// <summary>Rejestrator zdarzeń i alarmów – usługa wspólna modułów (Sim/Rejestrator.cs).</summary>
    public readonly Rejestrator Rej;
    public List<EventRecord> Events => Rej.Events;
    public List<EventRecord> Alarms => Rej.Alarms;
    public List<(string Time, string Text)> Remarks => Rej.Remarks;
    public event Action<EventRecord> EventAdded { add => Rej.EventAdded += value; remove => Rej.EventAdded -= value; }
    public event Action<EventRecord> AlarmAdded { add => Rej.AlarmAdded += value; remove => Rej.AlarmAdded -= value; }
    public event Action Beep { add => Rej.Beep += value; remove => Rej.Beep -= value; }
    public event Action<string> SpecialExecuted;
    public event Action PendingExpired;

    public Simulator(Station st, bool routes = true)
    {
        L = st;
        Rej = new Rejestrator(L, () => T, () => Clock(), () => User);
        Instruktor = new Instruktor(this);
        Page = L.Pages[^1].Id;
        foreach (var s in L.Sections.Keys) Secs[s] = new SecState { Id = s };
        foreach (var s in L.Switches.Keys) Sws[s] = new SwState { Num = s };
        foreach (var s in L.Signals.Keys) Sigs[s] = new SigState { Name = s };
        foreach (var s in L.Derailers.Keys) Ders[s] = new DerState { Name = s };
        foreach (var b in L.Blocks.Values) Blocks[b.Id] = new BlockState { Id = b.Id, Dir = b.DefDir };
        if (routes) BuildRouteTable();
    }

    // ------------------------------------------------------------------ czas, dziennik
    public string Clock(double? t = null)
    {
        var s = (long)Math.Floor(StartClock + (t ?? T));
        return $"{s / 3600 % 24:00}:{s / 60 % 60:00}:{s % 60:00}";
    }

    public string Clean(string s) => Rej.Clean(s);
    /// <summary>Skrót posterunku: inicjały nazwy (Łódź Widzew → ŁW, Koluszki → Ko).</summary>
    public static string PostShort(Station L, string post) => Rejestrator.PostShort(L, post);

    public EventRecord Log(string kategoria, string text, string typ = "", string obiekt = "", string zrodlo = "System") => Rej.Log(kategoria, text, typ, obiekt, zrodlo);
    /// <summary>Alarm (Ie-104 § 7): lista z potwierdzeniem i znacznikiem trwania stanu alarmowego.</summary>
    public void Alarm(string text, string typ = "", string obiekt = "", string key = null, Func<bool> stillActive = null) => Rej.Alarm(text, typ, obiekt, key, stillActive);
    public void Remark(string text) => Rej.Remark(text);

    // ------------------------------------------------------------------ topologia
    public record Fwd(string Edge, string Num, string Pos);

    public List<Fwd> Forward(string nid, int dir, string fromEdge)
    {
        var n = L.Nodes[nid];
        var res = new List<Fwd>();
        if (n.Switch != null)
        {
            var sw = L.Switches[n.Switch];
            if (sw.TipDir == dir)
            {
                if (fromEdge == sw.Plus) res.Add(new Fwd(sw.Tip, sw.Num, "+"));
                else if (fromEdge == sw.Minus) res.Add(new Fwd(sw.Tip, sw.Num, "-"));
                else if (fromEdge == null) res.Add(new Fwd(sw.Tip, null, null));
            }
            else
            {
                if (!L.Edges[sw.Plus].Dismantled) res.Add(new Fwd(sw.Plus, sw.Num, "+"));
                if (!L.Edges[sw.Minus].Dismantled) res.Add(new Fwd(sw.Minus, sw.Num, "-"));
            }
            return res;
        }
        foreach (var e in n.Edges)
            if (e != fromEdge && !L.Edges[e].Dismantled && L.EdgeDir(e, nid) == dir) res.Add(new Fwd(e, null, null));
        return res;
    }

    public Fwd NextByState(string nid, int dir, string fromEdge)
    {
        var n = L.Nodes[nid];
        if (n.Switch != null && L.Switches[n.Switch].TipDir != dir)
        {
            var sw = L.Switches[n.Switch]; var pos = Sws[n.Switch].Pos; var e = pos == "+" ? sw.Plus : sw.Minus;
            return L.Edges[e].Dismantled ? null : new Fwd(e, sw.Num, pos);
        }
        var o = Forward(nid, dir, fromEdge);
        return o.Count == 0 ? null : o[0];
    }

    // ------------------------------------------------------------------ tablica przebiegów (z wariantami)
    void BuildRouteTable()
    {
        foreach (var s in L.Signals.Values)
        {
            if (s.Type is not ("wjazdowy" or "wyjazdowy" or "tm")) continue;
            RouteTable[s.Name] = ApplyRules(RawRoutes(s.Name)).Where(r => !r.Off).ToList();
        }
    }

    /// <summary>
    /// Reguły z edytora: przebieg wyłączony (off) albo wskazany jako zasadniczy (basic) – staje się wariantem 0,
    /// pozostałe są wariantami 1, 2, … (Ie-104: przebieg zasadniczy i przebiegi wariantowe).
    /// </summary>
    public List<RouteDef> ApplyRules(List<RouteDef> raw)
    {
        var res = new List<RouteDef>();
        foreach (var grp in raw.GroupBy(r => (r.Type, r.End)))
        {
            var list = grp.OrderBy(r => r.Variant).ToList();
            foreach (var r in list)
            {
                r.Off = L.RouteRules.Any(q => q.Off && q.Start == r.Start && q.End == r.End && q.Type == r.Type && q.Sw == r.SwKey);
                r.VSet = L.RouteRules.FirstOrDefault(q => q.VMax > 0 && q.Start == r.Start && q.End == r.End && q.Type == r.Type && q.Sw == r.SwKey)?.VMax ?? 0;
                r.VCalc = RouteSpeed(r);
            }
            var basic = list.FirstOrDefault(r => !r.Off && L.RouteRules.Any(q => q.Basic && q.Start == r.Start && q.End == r.End && q.Type == r.Type && q.Sw == r.SwKey));
            if (basic != null) { list.Remove(basic); list.Insert(0, basic); }
            int v = 0;
            foreach (var r in list)
            {
                if (r.Off) { r.Variant = 99; continue; }
                r.Variant = v++;
                r.Id = Regex.Replace(r.Id, @" \(wariant \d+\)$", "") + (r.Variant > 0 ? $" (wariant {r.Variant})" : "");
            }
            res.AddRange(list);
        }
        return res.OrderBy(r => r.Type).ThenBy(r => r.End).ThenBy(r => r.Variant).ToList();
    }

    /// <summary>Prędkość przebiegu z ograniczeń torów (vmax, tor stacyjny 60) i zwrotnic (jazda na bok, na / z ostrza); manewrowy – 25.</summary>
    public double RouteSpeed(RouteDef r)
    {
        if (r.Type == "M") return 25;
        double v = 1e4;
        foreach (var eid in r.Edges)
        {
            var e = L.Edges[eid]; if (e.Szlak) continue;
            v = Math.Min(v, e.VMax > 0 ? e.VMax : 60);
            if (e.SwLimit > 0) v = Math.Min(v, e.SwLimit);
        }
        return v >= 1e4 ? 60 : v;
    }

    /// <summary>Wszystkie przebiegi od sygnalizatora wyznaczone z topologii (do 6 wariantów na koniec), bez reguł edytora.</summary>
    public List<RouteDef> RawRoutes(string sigName)
    {
        var s = L.Signals[sigName];
        var all = new List<RouteDef>();
        {
            foreach (var type in new[] { "P", "M" })
            {
                if (type == "P" && !s.IsTrain) continue;
                if (type == "M" && !s.ForShunts) continue;
                if (type == "M" && s.Type == "wjazdowy" && s.Szlak == null) continue;
                var cands = new List<(string End, string Kind, List<string> Edges, List<(string, string)> Reqs, string EndNode)>();
                void Dfs(string nid, string from, List<string> edges, List<(string, string)> reqs, int depth)
                {
                    if (depth > 80 || cands.Count > 800) return;
                    foreach (var o in Forward(nid, s.Dir, from))
                    {
                        var e2 = new List<string>(edges) { o.Edge };
                        var r2 = o.Num != null ? new List<(string, string)>(reqs) { (o.Num, o.Pos) } : reqs;
                        var nn = L.Other(o.Edge, nid);
                        var node = L.Nodes[nn];
                        if (L.Boundaries.TryGetValue(nn, out var sz))
                        {
                            if (type == "P" && s.Type == "wyjazdowy") cands.Add((sz, "szlak", e2, r2, nn));
                            else if (type == "M")
                            {
                                // przebieg manewrowy w kierunku szlaku: do semafora wjazdowego (granica przetaczania)
                                // albo wyjazd manewrowy na szlak – do końca pierwszego odstępu blokady
                                var be = L.Blocks[sz].Ends.FirstOrDefault(x => x.Boundary == nn);
                                if (be != null && be.Outward == s.Dir && s.Type != "wjazdowy")
                                {
                                    if (L.Signals.ContainsKey(be.EntrySig)) cands.Add((be.EntrySig, "gr", e2, r2, nn));
                                    cands.Add((sz, "szlakM", e2, r2, nn));
                                }
                            }
                            continue;
                        }
                        var endSig = node.Signals.Select(x => L.Signals[x]).FirstOrDefault(x => x.Dir == s.Dir && (type == "P" ? x.IsTrain || x.PkTrain : x.StopsShunts || x.PkShunt));
                        if (endSig != null)
                        {
                            // koniec przebiegu: semafor wyjazdowy / tarcza / PK – punkt końcowy przebiegu (bez blokady, np. przy bliskich posterunkach)
                            if (type == "P" && (endSig.Type == "wyjazdowy" || endSig.PkTrain)) cands.Add((endSig.Name, "sig", e2, r2, nn));
                            else if (type == "M" && endSig.Type != "wjazdowy") cands.Add((endSig.Name, "sig", e2, r2, nn));
                            continue;
                        }
                        if (L.TrackEnds.TryGetValue(nn, out var te)) { if (type == "M") cands.Add((te.Id, "kz", e2, r2, nn)); continue; }
                        if (L.FarEnds.ContainsKey(nn)) continue;
                        Dfs(nn, o.Edge, e2, r2, depth + 1);
                    }
                }
                Dfs(s.Node, null, new(), new(), 0);
                foreach (var grp in cands.GroupBy(c => c.End))
                {
                    int v = 0;
                    foreach (var c in grp.OrderBy(c => c.Reqs.Count(r => r.Item2 == "-")).ThenBy(c => c.Edges.Count))
                    {
                        var rd = new RouteDef { Start = s.Name, End = c.End, EndKind = c.Kind, Type = type, EndNode = c.EndNode, Minus = c.Reqs.Count(r => r.Item2 == "-"), Variant = v++ };
                        rd.Id = (type == "P" ? "P " : "M ") + Station.Lbl(s.Name) + "→" + Station.Lbl(c.End) + (type == "M" && s.Type == "wjazdowy" ? " (powrót ze szlaku)" : "") + c.Kind switch { "gr" => " (do semafora wjazdowego)", "szlakM" => " (na szlak)", _ => "" } + (rd.Variant > 0 ? $" (wariant {rd.Variant})" : "");
                        rd.Edges.AddRange(c.Edges);
                        foreach (var eid in c.Edges) { var sc = L.Edges[eid].Sec; if (rd.Secs.Count == 0 || rd.Secs[^1] != sc) rd.Secs.Add(sc); }
                        if (c.Kind == "szlakM") { var fs = L.Blocks[c.End].FirstSec(s.Dir); rd.Secs.Add(fs); rd.Edges.AddRange(L.Sections[fs].Edges); }
                        rd.Sw.AddRange(c.Reqs);
                        rd.Ders.AddRange(L.Derailers.Values.Where(d => rd.Secs.Contains(d.Sec)).Select(d => d.Name));
                        rd.Flank.AddRange(FlankFor(rd));
                        if (type == "P" && c.Kind == "sig")
                        {
                            var nx = Forward(c.EndNode, s.Dir, c.Edges[^1]);
                            if (nx.Count > 0) rd.Overlap = L.Edges[nx[0].Edge].Sec;
                        }
                        all.Add(rd);
                        if (v >= 6) break;
                    }
                }
            }
        }
        return all;
    }

    /// <summary>
    /// Ochrona boczna przebiegu (poradnik UTK 1.5.8, Tabela 22: zwrotnice „utwierdzone w ochronie bocznej”, wykolejnice,
    /// sygnalizatory „utwierdzone w ochronie przebiegu”). Od każdej zwrotnicy przebiegu wzdłuż nieużywanej gałęzi do pierwszego
    /// elementu, który może zatrzymać lub odwrócić jazdę w stronę przebiegu: wykolejnica (założona), sygnalizator zwrócony
    /// w stronę przebiegu („Stój”), zwrotnica osiągnięta od strony gałęzi (położenie kierujące w drugą gałąź). Zwrotnica
    /// osiągnięta od ostrza nie chroni (obie gałęzie prowadzą w stronę przebiegu) – wtedy ochrony brak.
    /// </summary>
    List<(string Kind, string Id, string Pos, string For)> FlankFor(RouteDef rd)
    {
        var res = new List<(string, string, string, string)>();
        var inRoute = rd.Sw.Select(q => q.Num).ToHashSet();
        foreach (var (num, pos) in rd.Sw)
        {
            var sw = L.Switches[num];
            string edge = pos == "+" ? sw.Minus : sw.Plus, nid = sw.Node;
            for (int k = 0; k < 10 && edge != null && L.Edges.ContainsKey(edge) && !L.Edges[edge].Dismantled; k++)
            {
                var nn = L.Other(edge, nid); var node = L.Nodes[nn];
                if (rd.Edges.Contains(edge)) break;
                var der = L.Derailers.Values.FirstOrDefault(d => d.Node == nn || (d.Sec == L.Edges[edge].Sec && d.Node == nid));
                if (der != null && !rd.Ders.Contains(der.Name)) { res.Add(("der", der.Name, "on", num)); break; }
                int toward = L.EdgeDir(edge, nn);      // kierunek jazdy od nn tym torem w stronę przebiegu
                var sig = node.Signals.Select(x => L.Signals[x]).FirstOrDefault(x => x.Dir == toward && x.Name != rd.Start && (x.IsTrain || x.ForShunts));
                if (sig != null && toward != 0) { res.Add(("sig", sig.Name, "stop", num)); break; }
                if (node.Switch != null)
                {
                    var t = L.Switches[node.Switch];
                    if (edge == t.Tip || inRoute.Contains(t.Num)) break;
                    res.Add(("sw", t.Num, edge == t.Plus ? "-" : "+", num));
                    break;
                }
                if (node.Edges.Count != 2 || L.Boundaries.ContainsKey(nn) || L.TrackEnds.ContainsKey(nn)) break;
                nid = nn; edge = node.Edges.First(x => x != edge);
            }
        }
        // ten sam element dla kilku zwrotnic – raz; sprzeczne położenia tej samej zwrotnicy – bez ochrony
        return res.GroupBy(f => (f.Item1, f.Item2)).Where(g => g.Select(x => x.Item3).Distinct().Count() == 1).Select(g => g.First()).ToList();
    }

    /// <summary>Wybór przebiegu: zasadniczy albo wariantowy przechodzący przez punkty pośrednie (zwrotnica, odcinek, sygnalizator).</summary>
    public RouteDef FindRoute(string start, string end, string type, IList<string> via = null)
    {
        if (!RouteTable.TryGetValue(start, out var l)) return null;
        var c = l.Where(r => r.End == end && (type == null || r.Type == type));
        if (via != null && via.Count > 0) c = c.Where(r => via.All(v => Passes(r, v)));
        return c.OrderBy(r => r.Variant).FirstOrDefault();
    }

    public bool Passes(RouteDef r, string v) => Passes(L, r, v);
    // ------------------------------------------------------------------ predykaty
    public double SzLeft(string id) => Sigs[id].SzUntil - T;
    public bool SecOcc(string id) { var s = Secs[id]; return s.TrainOcc || s.Fault != null; }
    public bool SwControlled(string n) { var w = Sws[n]; return w.Moving == null && !w.NoCtrl && !w.Rozpr && !w.NoData && !w.Local; }

    // ------------------------------------------------------------------ pętla
    public Trains Trains;
    /// <summary>Instruktor: usterki i scenariusze usterek (Sim/Instruktor.cs).</summary>
    public readonly Instruktor Instruktor;

    public void Tick(double dt)
    {
        T += dt;
        if (Pending != null && T > Pending.Until) { Log("Odrzucone", "Upłynął czas potwierdzenia polecenia " + Pending.Line, "Polecenie"); Pending = null; PendingExpired?.Invoke(); }
        UpdatePower(); UpdateSwitches(); UpdateBlocks();
        Trains?.Tick(dt);
        UpdateSignals(); UpdateRoutes();
        Rej.UpdateAlarms();
    }

    void UpdatePower()
    {
        var p = Power;
        if (!p.Mains && !p.Gen && p.GenAuto && !p.GenFail && p.OutageAt is double o && T - o > 15) { p.Gen = true; Log("Zdarzenie", "Agregat prądotwórczy uruchomiony samoczynnie", "Zasilanie", "AGR"); }
    }

    void UpdateSwitches()
    {
        foreach (var (n, w) in Sws)
        {
            if (w.Moving == null) continue;
            if (!Power.NN && !w.Frozen) continue;
            if (w.Frozen)
            {
                if (T - w.Moving.Started > Cfg.SwTimeout && !w.Moving.Alarmed) { w.Moving.Alarmed = true; Alarm($"Zwrotnica {n}: przestawianie trwa zbyt długo – przestaw do położenia pierwotnego (§24 ust. 6)", "Zwrotnica", n, stillActive: () => w.Moving != null); }
                if (w.Moving.Target == w.Moving.From && T >= w.Moving.Until) { w.Pos = w.Moving.Target; w.Moving = null; }
                continue;
            }
            if (T >= w.Moving.Until) { w.Pos = w.Moving.Target; w.Moving = null; Log("Zdarzenie", $"Kontrola położenia „{w.Pos}”", "Zwrotnica", n); }
        }
        foreach (var d in Ders.Values) if (d.MovingTo is bool m && T >= d.MoveUntil) { d.On = m; d.MovingTo = null; }
    }

    void UpdateSignals()
    {
        foreach (var (n, s) in Sigs)
        {
            var d = L.Signals[n];
            if (s.Aspect == Aspect.Sz && T >= s.SzUntil) { s.Aspect = Aspect.Stop; Log("Zdarzenie", "Sygnał zastępczy wygasł samoczynnie", "Semafor", n); }
            if (d.Type == "odstepowy")
            {
                var bs = Blocks[d.Szlak];
                var ahead = Forward(d.Node, d.Dir, null).FirstOrDefault();
                bool free = ahead != null && !SecOcc(L.Edges[ahead.Edge].Sec);
                s.Dark = bs.Dir != d.Dir || bs.AzkUntil != null;
                s.Aspect = !s.Dark && free && bs.ReleaseUntil == null ? Aspect.Train : Aspect.Stop;
            }
        }
        // sygnały wg stawności (S1 / S5 / S4 / S2) – od końca szlaku w stronę początku
        foreach (var (n, s) in Sigs)
        {
            var d = L.Signals[n]; if (d.Type != "odstepowy") continue;
            s.Sig = null;
        }
        foreach (var (n, s) in Sigs)
        {
            var d = L.Signals[n]; if (d.Type != "odstepowy" || s.Dark) continue;
            s.Sig = OdstepSig(n, 0);
        }
    }

    readonly Dictionary<string, string> nextSigCache = new();
    /// <summary>Następny sygnalizator w kierunku jazdy za semaforem odstępowym (odstępowy albo wjazdowy; null – koniec obszaru).</summary>
    string NextSignal(string sig)
    {
        if (nextSigCache.TryGetValue(sig, out var c)) return c;
        var d = L.Signals[sig]; string node = d.Node, from = null, found = null;
        for (int k = 0; k < 200 && found == null; k++)
        {
            var nx = Forward(node, d.Dir, from).FirstOrDefault(); if (nx == null) break;
            node = L.Other(nx.Edge, node); from = nx.Edge;
            found = L.Nodes[node].Signals.FirstOrDefault(x => L.Signals[x].Dir == d.Dir && L.Signals[x].Type is "odstepowy" or "wjazdowy");
        }
        return nextSigCache[sig] = found;
    }

    /// <summary>Sygnał semafora odstępowego wg stawności blokady: S1 – „Stój”; trzystawna S5 / S2; czterostawna S5 / S4 / S2.</summary>
    string OdstepSig(string n, int depth)
    {
        var s = Sigs[n]; var d = L.Signals[n];
        if (s.Aspect != Aspect.Train) return "S1";
        int st = L.Blocks.TryGetValue(d.Szlak ?? "", out var b) ? b.Aspects : 3;
        if (st <= 2) return "S2";
        var nx = NextSignal(n);
        string next = nx == null ? "S1" : L.Signals[nx].Type == "odstepowy" ? (depth < 6 ? OdstepSig(nx, depth + 1) : "S2") : (Sigs[nx].Aspect is Aspect.Train ? "S2" : "S1");
        if (next == "S1") return "S5";
        if (st == 4 && next == "S5") return "S4";
        return "S2";
    }
}

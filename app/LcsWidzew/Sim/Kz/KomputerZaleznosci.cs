using System.Text.RegularExpressions;
using LcsWidzew.Model;
using Cfg = LcsWidzew.Sim.Simulator.Cfg;
using Fwd = LcsWidzew.Sim.Simulator.Fwd;

namespace LcsWidzew.Sim;

/// <summary>
/// Komputer zależności (EBILock, PODZIAL_MODULOW.md krok 10): stan zależności (odcinki, zwrotnice, sygnalizatory, wykolejnice,
/// blokady, przebiegi), tablica przebiegów, nastawianie i zwalnianie, ochrona boczna, blokada liniowa, sygnały wg Ie-1,
/// statusy kart sterowników i rozkazy dla nich. Polecenia przychodzą od systemu nadrzędnego jako <see cref="PolecenieSystemowe"/>.
/// Nie prowadzi pociągów – o terenie wie tylko ze statusów kart.
/// </summary>
public partial class KomputerZaleznosci
{
    readonly Simulator S;
    Station L => S.L;
    double T => S.T;
    Urzadzenia Urzadzenia => S.Urzadzenia;
    Otoczenie Otoczenie => S.Otoczenie;
    Trains Trains => S.Trains;
    Stats Stats => S.Stats;
    EventRecord Log(string kategoria, string text, string typ = "", string obiekt = "", string zrodlo = "System") => S.Log(kategoria, text, typ, obiekt, zrodlo);
    void Alarm(string text, string typ = "", string obiekt = "", string key = null, Func<bool> stillActive = null) => S.Alarm(text, typ, obiekt, key, stillActive);
    void Remark(string text) => S.Remark(text);
    string Clock(double? t = null) => S.Clock(t);
    List<Fwd> Forward(string nid, int dir, string fromEdge) => S.Forward(nid, dir, fromEdge);

    public Dictionary<string, SecState> Secs = new();
    public Dictionary<string, SwState> Sws = new();
    public Dictionary<string, SigState> Sigs = new();
    public Dictionary<string, DerState> Ders = new();
    public Dictionary<string, BlockState> Blocks = new();
    public Dictionary<string, Route> Routes = new();
    public Dictionary<string, List<RouteDef>> RouteTable = new();
    public PowerState Power = new();
    int seq = 1;

    public KomputerZaleznosci(Simulator sim)
    {
        S = sim;
        foreach (var s in L.Sections.Keys) Secs[s] = new SecState { Id = s };
        foreach (var s in L.Switches.Keys) Sws[s] = new SwState { Num = s };
        foreach (var s in L.Signals.Keys) Sigs[s] = new SigState { Name = s };
        foreach (var s in L.Derailers.Keys) Ders[s] = new DerState { Name = s };
        foreach (var b in L.Blocks.Values) Blocks[b.Id] = new BlockState { Id = b.Id, Dir = b.DefDir };
    }

    /// <summary>Następny tor wg położenia zwrotnic znanego komputerowi zależności.</summary>
    public Fwd NextByState(string nid, int dir, string fromEdge) => S.Next(nid, dir, fromEdge, n => Sws[n].Pos);
    // ------------------------------------------------------------------ tablica przebiegów (z wariantami)
    internal void BuildRouteTable()
    {
        foreach (var s in L.Signals.Values)
        {
            if (s.Type is not ("wjazdowy" or "wyjazdowy" or "tm")) continue;
            RouteTable[s.Name] = ApplyRules(RawRoutes(s.Name)).Where(r => !r.Off).ToList();
        }
        // ten sam opis przebiegu na kilku posterunkach obszaru (np. „P A→C1” na obu stacjach LCS) – z kodem posterunku,
        // żeby dziennik, tablica przebiegów i podgląd KZ je rozróżniały; opisy niepowtarzalne bez zmian
        foreach (var g in RouteTable.Values.SelectMany(x => x).GroupBy(r => r.Id).Where(g => g.Select(r => L.Signals[r.Start].Post).Distinct().Count() > 1))
            foreach (var r in g) r.Id += $" ({L.Signals[r.Start].Post})";
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
                ApplySpecial(r);
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
                        // rozjazd krzyżowy pojedynczy: bez jazdy przez brakujący przejazd
                        if (L.NoPass.Count > 0 && L.NoPass.Any(p => p.A == o.Edge && edges.Contains(p.B) || p.B == o.Edge && edges.Contains(p.A))) continue;
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

    /// <summary>
    /// Zależności specjalne z pliku &lt;układ&gt;.zaleznosci.json (projektuje człowiek): warianty drogi ochronnej, przebieg bez drogi
    /// ochronnej, przebiegi sprzeczne i dopuszczone, elementy ochrony bocznej dodane / usunięte, dodatkowe warunki.
    /// </summary>
    void ApplySpecial(RouteDef r)
    {
        foreach (var z in L.Zal.Dla(r.Type, r.Start, r.End, r.SwKey))
        {
            if (z.BezOchronnej) r.NoOverlap = true;
            r.OverlapAlts.AddRange(z.DrogaOchronna);
            r.Excl.AddRange(z.Sprzeczne); r.Allow.AddRange(z.Dopuszczone); r.Conds.AddRange(z.Warunki);
            foreach (var u in z.OchronaUsun) r.Flank.RemoveAll(f => f.Id == u || f.Id + f.Pos == u);
            foreach (var d in z.OchronaDodaj)
            {
                (string, string, string, string) f;
                if (d.Length > 1 && d[^1] is '+' or '-' && L.Switches.ContainsKey(d[..^1])) f = ("sw", d[..^1], d[^1..], null);
                else if (L.Derailers.ContainsKey(d)) f = ("der", d, "on", null);
                else if (L.Signals.ContainsKey(d)) f = ("sig", d, "stop", null);
                else continue;
                r.Flank.RemoveAll(x => x.Id == f.Item2);
                r.Flank.Add(f);
            }
        }
    }

    /// <summary>Przebiegi dopuszczone do jednoczesnego nastawienia zależnością specjalną (wpis przy którymkolwiek z nich).</summary>
    public static bool Dopuszczone(RouteDef a, RouteDef b) => a.Allow.Contains(b.Key) || b.Allow.Contains(a.Key);
    /// <summary>Przebiegi sprzeczne z zależności specjalnej (wpis przy którymkolwiek z nich).</summary>
    public static bool Sprzeczne(RouteDef a, RouteDef b) => a.Excl.Contains(b.Key) || b.Excl.Contains(a.Key);

    /// <summary>Wybór przebiegu: zasadniczy albo wariantowy przechodzący przez punkty pośrednie (zwrotnica, odcinek, sygnalizator).</summary>
    public RouteDef FindRoute(string start, string end, string type, IList<string> via = null)
    {
        if (!RouteTable.TryGetValue(start, out var l)) return null;
        var c = l.Where(r => r.End == end && (type == null || r.Type == type));
        if (via != null && via.Count > 0) c = c.Where(r => via.All(v => Passes(r, v)));
        return c.OrderBy(r => r.Variant).FirstOrDefault();
    }

    public bool Passes(RouteDef r, string v) => Simulator.Passes(L, r, v);
    // ------------------------------------------------------------------ predykaty
    public double SzLeft(string id) => Sigs[id].SzUntil - T;
    public bool SecOcc(string id) { var s = Secs[id]; return s.TrainOcc || s.Fault != null; }
    public bool SwControlled(string n) { var w = Sws[n]; return w.Moving == null && !w.NoCtrl && !w.Rozpr && !w.NoData && !w.Local; }

    /// <summary>
    /// Status zasilania (karta <see cref="Zasilanie"/>): sieć, UPS, agregat, napięcie nastawcze. KZ trzyma kopię w <see cref="Power"/>,
    /// przy zaniku sieci – alarm (Ie-20 § 23), przy powrocie – zdarzenie.
    /// </summary>
    public void StatusZasilania(bool siec, bool ups, bool agregat, bool nn, bool agregatNieRuszyl)
    {
        Urzadzenia.LogStatus("zasilanie", Zasilanie.Id, (siec ? "sieć" : "BRAK SIECI") + (ups ? ", UPS" : ", UPS niesprawny") + (agregat ? ", agregat pracuje" : agregatNieRuszyl ? ", agregat nie ruszył" : "") + (nn ? ", NN załączone" : ", NN odłączone"));
        var p = Power;
        if (p.Mains && !siec)
            Alarm(agregatNieRuszyl ? "Zanik zasilania – agregat nie uruchomił się (AGR / automatyk, §23 ust. 2)" : "Zanik zasilania sieciowego – praca z UPS (Ie-20 §23)", "Zasilanie", "SIEĆ", stillActive: () => !Power.Mains);
        else if (!p.Mains && siec) Log("Zdarzenie", "Przywrócono zasilanie sieciowe", "Zasilanie", "SIEĆ");
        p.Mains = siec; p.Ups = ups; p.Gen = agregat; p.NN = nn;
    }

    /// <summary>
    /// Status karty zwrotnicy: położenie z kontrolą (null – brak kontroli), przekładanie, przekroczony czas przekładania.
    /// Brak kontroli bez przekładania: przy zajętym odcinku zwrotnicowym – rozprucie, przy wolnym – brak kontroli
    /// (decyzja właściciela 2026-10-16, PODZIAL_MODULOW.md § 7 pkt 1).
    /// </summary>
    public void StatusZwrotnicy(string num, string pol, bool przekladanie, bool przekroczonyCzas)
    {
        Urzadzenia.LogStatus("zwrotnica", num, pol != null ? $"kontrola „{pol}”" : przekladanie ? "przekładanie" + (przekroczonyCzas ? ", przekroczony czas" : "") : "brak kontroli");
        var w = Sws[num];
        w.Frozen = przekroczonyCzas;
        if (przekroczonyCzas && w.Moving != null && !w.Moving.Alarmed) { w.Moving.Alarmed = true; Alarm($"Zwrotnica {num}: przestawianie trwa zbyt długo – przestaw do położenia pierwotnego (§24 ust. 6)", "Zwrotnica", num, stillActive: () => w.Moving != null); }
        if (pol != null)
        {
            w.Pos = pol; w.KsrDone = false;
            if (w.NoCtrl) { w.NoCtrl = false; Log("Zdarzenie", $"Przywrócona kontrola położenia „{pol}”", "Zwrotnica", num); }
            if (w.Moving != null && !przekladanie) { if (pol == w.Moving.Target) Log("Zdarzenie", $"Kontrola położenia „{pol}”", "Zwrotnica", num); w.Moving = null; }
            return;
        }
        if (przekladanie) return;
        w.Moving = null;
        if (w.Rozpr) return;
        if (!w.KsrDone && SecOcc(L.Switches[num].Sec))
        {
            w.Rozpr = true; w.NoCtrl = false;
            Alarm($"ROZPRUCIE zwrotnicy {num} – zamknij rozjazd dla ruchu (§22 ust. 2)", "Zwrotnica", num, stillActive: () => w.Rozpr);
        }
        else if (!w.NoCtrl)
        {
            w.NoCtrl = true;
            Alarm(w.KsrDone ? $"Zwrotnica {num}: brak kontroli po skasowaniu rozprucia – postępuj wg §22 ust. 2" : $"Zwrotnica {num}: niespodziewany brak kontroli położenia", "Zwrotnica", num, stillActive: () => w.NoCtrl);
        }
    }

    /// <summary>Status karty wykolejnicy: założona / zdjęta (null – przekładanie).</summary>
    public void StatusWykolejnicy(string name, bool? zalozona)
    {
        Urzadzenia.LogStatus("wykolejnica", name, zalozona is bool zz ? (zz ? "założona" : "zdjęta") : "przekładanie");
        if (zalozona is not bool z) return;
        var d = Ders[name]; d.On = z;
        if (d.MovingTo == z) d.MovingTo = null;
    }

    /// <summary>Rozkaz dla karty wykolejnicy (stan „w ruchu” do meldunku karty).</summary>
    void DerCmd(string name, bool zaloz)
    {
        var d = Ders[name];
        if (d.MovingTo == null && d.On == zaloz) return;
        d.MovingTo = zaloz; d.MoveUntil = T + Cfg.SwTime;
        Urzadzenia.Wykolejnica(name, zaloz);
    }

    internal void UpdateSignals()
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
    }

    readonly Dictionary<string, string> nextSigCache = new();
    /// <summary>Następny sygnalizator w kierunku jazdy za semaforem odstępowym (odstępowy albo wjazdowy; null – koniec obszaru).</summary>
    string NextSignal(string sig)
    {
        if (nextSigCache.TryGetValue(sig, out var c)) return c;
        var d = L.Signals[sig];
        return nextSigCache[sig] = NextSignalFrom(d.Node, d.Dir);
    }

    /// <summary>Pierwszy semafor odstępowy albo wjazdowy w kierunku dir od węzła (po torach szlaku; null – koniec obszaru).</summary>
    string NextSignalFrom(string node, int dir)
    {
        string from = null;
        for (int k = 0; k < 200; k++)
        {
            var nx = Forward(node, dir, from).FirstOrDefault(); if (nx == null) break;
            node = L.Other(nx.Edge, node); from = nx.Edge;
            var f = L.Nodes[node].Signals.FirstOrDefault(x => L.Signals[x].Dir == dir && L.Signals[x].Type is "odstepowy" or "wjazdowy");
            if (f != null) return f;
        }
        return null;
    }

    /// <summary>
    /// Prędkość sygnalizowana przebiegu [km/h] (Ie-1 § 4 ust. 13): ograniczenia ze zwrotnic w drodze przebiegu i torów z wpisaną prędkością,
    /// albo prędkość ustalona ręcznie (VSet); 0 – bez ograniczenia (największa dozwolona). Domyślne 60 km/h torów stacyjnych symulatora
    /// sygnału nie obniża.
    /// </summary>
    public double SignalSpeed(RouteDef r)
    {
        if (r.VSet > 0) return r.VSet;
        double v = 1e4;
        foreach (var eid in r.Edges)
        {
            var e = L.Edges[eid]; if (e.Szlak) continue;
            if (e.VMax > 0) v = Math.Min(v, e.VMax);
            if (e.SwLimit > 0) v = Math.Min(v, e.SwLimit);
        }
        return v >= 1e4 ? 0 : v;
    }

    /// <summary>Sygnał następnego semafora za przebiegiem (koniec przebiegu, pierwszy semafor na szlaku); null – brak uzależnienia.</summary>
    string NextAfterRoute(Route r) => r.EndKind switch
    {
        "sig" => r.End,
        "szlak" => NextSignalFrom(r.EndNode, L.Signals[r.Start].Dir),
        _ => null,
    };

    /// <summary>
    /// Sygnały zlecane kartom (Ie-1 § 4 ust. 13, zał. 1): „Stój” / Ms1, Sz, Ms2, a przy przebiegu pociągowym – sygnał z siatki
    /// prędkość przy semaforze × co wskazuje następny semafor. Uzależnienie: zmiana na następnym semaforze zmienia sygnał tego semafora
    /// w tym samym cyklu (np. A: S5 przy B „Stój”, S2 po otwarciu B). SBL czterostawna: S3 – dwa odstępy wolne (następny wskazuje S5).
    /// </summary>
    internal void SignalCodes()
    {
        var memo = new Dictionary<string, string>();
        foreach (var (n, d) in L.Signals)
        {
            if (!Urzadzenia.MaKarte(d)) continue;
            var kod = Kod(n, memo, 0);
            var s = Sigs[n];
            if (kod != s.Sig) { s.Sig = kod; Urzadzenia.Sygnal(n, kod); }
        }
    }

    string Kod(string n, Dictionary<string, string> memo, int depth)
    {
        if (memo.TryGetValue(n, out var m)) return m;
        var d = L.Signals[n]; var s = Sigs[n];
        if (!Urzadzenia.MaKarte(d)) return null;
        string res;
        if (d.Type == "odstepowy" && s.Dark) res = "";
        else switch (s.Aspect)
        {
            case Aspect.Sz: res = "Sz"; break;
            case Aspect.Shunt: res = "Ms2"; break;
            case Aspect.Train:
            {
                int klasa = 0; string next; int stawnosc = 3;
                if (d.Type == "odstepowy")
                {
                    next = NextSignal(n);
                    if (L.Blocks.TryGetValue(d.Szlak ?? "", out var b)) stawnosc = b.Aspects;
                }
                else
                {
                    var r = s.Route != null && Routes.TryGetValue(s.Route, out var rr) ? rr : null;
                    klasa = r != null ? Sygnaly.Klasa(SignalSpeed(r)) : 0;
                    next = r != null ? NextAfterRoute(r) : null;
                    if (r?.EndKind == "szlak" && L.Blocks.TryGetValue(r.End, out var b)) stawnosc = b.Aspects; else if (r?.EndKind != "szlak") stawnosc = 3;
                }
                string kolumna, nk = null;
                if (stawnosc <= 2 && d.Type == "odstepowy") kolumna = "Vmax";     // dwustawna – bez zapowiadania
                else if (next == null) kolumna = d.Type == "odstepowy" ? "Stoj" : "Vmax";   // koniec obszaru / brak uzależnienia (o następnym informuje tarcza ostrzegawcza)
                else { memo[n] = "S1"; nk = depth < 12 ? Kod(next, memo, depth + 1) : "S1"; kolumna = Sygnaly.Kolumna(nk); }
                res = Sygnaly.Dobierz(klasa, kolumna);
                if (stawnosc == 4 && klasa == 0 && kolumna == "Vmax" && nk == "S5") res = "S3";
                break;
            }
            default: res = d.Type == "tm" ? "Ms1" : "S1"; break;
        }
        return memo[n] = res;
    }

    /// <summary>Status karty sygnalizatora: sygnał wyświetlany, uszkodzenie światła zezwalającego, brak łączności.</summary>
    public void StatusSygnalizatora(string sig, string wyswietlany, bool uszkZezwalajacego, bool brakDanych)
    {
        Urzadzenia.LogStatus("sygnalizator", sig, "wyświetla " + (wyswietlany == "" ? "– (ciemny)" : wyswietlany) + (uszkZezwalajacego ? ", uszkodzone światło zezwalające" : "") + (brakDanych ? ", brak łączności" : ""));
        var s = Sigs[sig];
        s.Wyswietlany = wyswietlany;
        if (s.LampFault && !uszkZezwalajacego) s.FailAlarm = false;
        s.LampFault = uszkZezwalajacego;
        if (brakDanych && !s.NoData) Alarm($"Sygnalizator {sig}: brak komunikacji z obiektem", "Semafor", sig, stillActive: () => s.NoData);
        s.NoData = brakDanych;
        // sygnał zezwalający nie zapalił się / zgasł w terenie – „Stój” i przebieg wraca do nastawiania (§26 ust. 1)
        if (uszkZezwalajacego && s.Aspect == Aspect.Train && Sygnaly.AspectOf(wyswietlany) == Aspect.Stop && s.Sig != null && Sygnaly.Def(s.Sig)?.V != null)
        {
            s.Aspect = Aspect.Stop;
            if (s.Route != null && Routes.TryGetValue(s.Route, out var r) && !r.Passed)
            {
                r.State = "setting";
                if (!s.FailAlarm) { s.FailAlarm = true; Alarm($"Semafor {sig}: nie wyświetlił sygnału zezwalającego (§26 ust. 1)", "Semafor", sig, stillActive: () => Routes.ContainsKey(r.Uid) && !r.Passed && s.Aspect == Aspect.Stop); }
            }
        }
    }
}

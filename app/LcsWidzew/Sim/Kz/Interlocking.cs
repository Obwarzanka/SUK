using LcsWidzew.Model;
using Cfg = LcsWidzew.Sim.Simulator.Cfg;

namespace LcsWidzew.Sim;

public partial class KomputerZaleznosci
{
    List<string> RouteObstacles(Route rt, bool forSetting)
    {
        var why = new List<string>();
        var sig = Sigs[rt.Start];
        if (sig.Closed) why.Add($"sygnalizator {rt.Start} zamknięty indywidualnie");
        if (forSetting && sig.Route != null) why.Add($"sygnalizator {rt.Start} jest już początkiem przebiegu");
        if (sig.Flank.Any(u => u != rt.Uid)) why.Add($"sygnalizator {rt.Start} utwierdzony w ochronie bocznej przebiegu {FlankOwner(sig.Flank, rt.Uid)}");
        for (int i = 0; i < rt.Secs.Count; i++)
        {
            var sc = rt.Secs[i]; var s = Secs[sc]; bool last = i == rt.Secs.Count - 1;
            if (s.Closed) why.Add($"odcinek {sc} zamknięty");
            // droga ochronna przebiegu kończącego się na semaforze początkowym może zostać zajęta przez przebieg dalszy (jazda „na wprost” przez stację)
            bool overlapOfPrev = s.LockType == "overlap" && Routes.TryGetValue(s.Lock ?? "", out var prv) && prv.End == rt.Start && prv.OvSecs.Contains(sc);
            if (s.Lock != null && s.Lock != rt.Uid && !overlapOfPrev && !SharedOk(rt, s)) why.Add($"odcinek {sc} utwierdzony w innym przebiegu");
            if (SecOcc(sc) && !(rt.Type == "M" && last)) why.Add($"odcinek {sc} zajęty");
        }
        foreach (var (num, pos) in rt.Sw)
        {
            var w = Sws[num];
            if (w.NoData) why.Add($"zwr. {num} – brak danych");
            if (w.Rozpr) why.Add($"zwr. {num} – rozprucie");
            if (w.Local) why.Add($"zwr. {num} – przekazana do nastawiania lokalnego");
            var cur = w.Moving?.Target ?? w.Pos;
            if (cur != pos)
            {
                if (w.Lock != null && w.Lock != rt.Uid) why.Add($"zwr. {num} utwierdzona w innym przebiegu");
                if (w.Flank.Any(u => u != rt.Uid)) why.Add($"zwr. {num} utwierdzona w ochronie bocznej przebiegu {FlankOwner(w.Flank, rt.Uid)}");
                if (w.Ov.Any(u => u != rt.Uid)) why.Add($"zwr. {num} utwierdzona w drodze ochronnej przebiegu {FlankOwner(w.Ov, rt.Uid)}");
                if (w.Stopped) why.Add($"zwr. {num} zastopowana");
                if (!Power.NN) why.Add("wyłączone napięcie nastawcze");
                if (forSetting && SecOcc(L.Switches[num].Sec)) why.Add($"zwr. {num} – odcinek zajęty");
            }
        }
        foreach (var d in rt.Ders) if (Ders[d].Flank.Any(u => u != rt.Uid)) why.Add($"wykolejnica {d} utwierdzona w ochronie bocznej przebiegu {FlankOwner(Ders[d].Flank, rt.Uid)}");
        // ochrona boczna
        foreach (var (kind, id, fpos, _) in rt.Flank)
        {
            if (kind == "sw")
            {
                var w = Sws[id]; var cur = w.Moving?.Target ?? w.Pos;
                if (cur == fpos) continue;
                if (w.Lock != null && w.Lock != rt.Uid) why.Add($"ochrona boczna: zwr. {id} utwierdzona w innym przebiegu");
                if (w.Flank.Any(u => u != rt.Uid) || w.Ov.Any(u => u != rt.Uid)) why.Add($"ochrona boczna: zwr. {id} utwierdzona w ochronie innego przebiegu w położeniu {cur}");
                if (w.Stopped) why.Add($"ochrona boczna: zwr. {id} zastopowana");
                if (w.Rozpr || w.NoData || w.Local) why.Add($"ochrona boczna: zwr. {id} bez kontroli / rozprucie / nastawianie lokalne");
                if (!Power.NN) why.Add("ochrona boczna: wyłączone napięcie nastawcze");
                if (forSetting && SecOcc(L.Switches[id].Sec)) why.Add($"ochrona boczna: zwr. {id} – odcinek zajęty");
            }
            else if (kind == "der")
            {
                var d = Ders[id]; bool on = d.MovingTo ?? d.On;
                if (on) continue;
                if (d.Lock != null && d.Lock != rt.Uid) why.Add($"ochrona boczna: wykolejnica {id} zdjęta w innym przebiegu");
                if (forSetting && SecOcc(L.Derailers[id].Sec)) why.Add($"ochrona boczna: wykolejnica {id} – odcinek zajęty");
            }
            else
            {
                var s = Sigs[id];
                if (s.Aspect != Aspect.Stop || (forSetting && s.Route != null)) why.Add($"ochrona boczna: sygnalizator {id} – sygnał zezwalający / początek przebiegu");
            }
        }
        foreach (var oc in rt.OvSecs)
        {
            var o = Secs[oc];
            if (SecOcc(oc)) why.Add($"droga ochronna {oc} zajęta");
            if (o.Lock != null && o.Lock != rt.Uid && o.LockType != "overlap" && !SharedOk(rt, o)) why.Add($"droga ochronna {oc} utwierdzona w innym przebiegu");
        }
        // zwrotnice drogi ochronnej (zależność specjalna) – przestawiane i utwierdzane jak w ochronie bocznej
        foreach (var (num, pos) in rt.OvSw)
        {
            var w = Sws[num]; var cur = w.Moving?.Target ?? w.Pos;
            if (cur == pos) continue;
            if (w.Lock != null && w.Lock != rt.Uid) why.Add($"droga ochronna: zwr. {num} utwierdzona w innym przebiegu");
            if (w.Flank.Any(u => u != rt.Uid) || w.Ov.Any(u => u != rt.Uid)) why.Add($"droga ochronna: zwr. {num} utwierdzona w ochronie innego przebiegu w położeniu {cur}");
            if (w.Stopped) why.Add($"droga ochronna: zwr. {num} zastopowana");
            if (w.Rozpr || w.NoData || w.Local) why.Add($"droga ochronna: zwr. {num} bez kontroli / rozprucie / nastawianie lokalne");
            if (!Power.NN) why.Add("droga ochronna: wyłączone napięcie nastawcze");
            if (forSetting && SecOcc(L.Switches[num].Sec)) why.Add($"droga ochronna: zwr. {num} – odcinek zajęty");
        }
        // przebiegi sprzeczne i dodatkowe warunki z zależności specjalnych
        foreach (var o in Routes.Values) if (o.Uid != rt.Uid && Sprzeczne(rt, o)) why.Add($"przebieg sprzeczny {o.Id} (zależność specjalna)");
        foreach (var c in rt.Conds)
        {
            var (obj, st) = Zaleznosci.Warunek(c);
            bool ok = st switch
            {
                "+" or "-" => Sws.TryGetValue(obj, out var w) && w.Pos == st && SwControlled(obj),
                "wolny" => Secs.ContainsKey(obj) && !SecOcc(obj),
                "założona" => Ders.TryGetValue(obj, out var d) && d.On && d.MovingTo == null,
                "zdjęta" => Ders.TryGetValue(obj, out var d2) && !d2.On && d2.MovingTo == null,
                "stój" => Sigs.TryGetValue(obj, out var g) && g.Aspect == Aspect.Stop && g.Route == null,
                _ => false
            };
            if (!ok) why.Add($"warunek specjalny „{Station.Lbl(obj)} {st}” niespełniony");
        }
        if (rt.EndKind is "szlak" or "szlakM" && !BlockAllowsExit(rt.End, L.Signals[rt.Start].Dir)) why.Add($"blokada liniowa {rt.End}: brak kierunku WYJAZD, szlak zamknięty lub pierwszy odstęp zajęty");
        if (forSetting && rt.EndKind == "szlak" && PwlBlocks(rt.End, L.Signals[rt.Start].Dir)) why.Add($"blokada półsamoczynna {rt.End}: przeciwwtórność liniowa (Pwl) – sygnał zezwalający już był podany, najpierw blok początkowy ({rt.End} Po)");
        if (rt.Type == "M" && L.Signals[rt.Start].Type == "wjazdowy" && !rt.Passed)
        {
            // powrót taboru manewrowego ze szlaku – na sygnał zastępczy, tylko gdy tabor stoi za semaforem wjazdowym: do W5 (granica
            // przetaczania), a bez W5 – w odcinku tuż za semaforem
            var sd = L.Signals[rt.Start];
            var zaW = (DoW5(sd.Node, -sd.Dir) ?? Forward(sd.Node, -sd.Dir, null).Take(1).Select(f => f.Edge).ToList()).Select(e => L.Edges[e].Sec).Distinct().ToList();
            if (!zaW.Any(SecOcc)) why.Add($"przebieg manewrowy od semafora wjazdowego {Station.Lbl(rt.Start)} tylko dla powrotu taboru ze szlaku – {string.Join(", ", zaW.Select(Station.Lbl))} wolny");
        }
        return why;
    }

    /// <summary>Przeszkody w nastawieniu przebiegu (diagnostyka testów).</summary>
    public List<string> WhyNot(RouteDef rd)
    {
        return Obstacles(NewRoute(rd, "probe"), rd, true);
    }

    Route NewRoute(RouteDef rd, string uid)
    {
        var r = new Route
        {
            Variant = rd.Variant, VCalc = rd.VCalc, VSet = rd.VSet, Id = rd.Id, Start = rd.Start, End = rd.End, EndKind = rd.EndKind, Type = rd.Type, EndNode = rd.EndNode, Overlap = rd.Overlap,
            NoOverlap = rd.NoOverlap, Uid = uid, T0 = T
        };
        r.Edges.AddRange(rd.Edges); r.Secs.AddRange(rd.Secs); r.Sw.AddRange(rd.Sw); r.Ders.AddRange(rd.Ders); r.Flank.AddRange(rd.Flank);
        r.OverlapAlts.AddRange(rd.OverlapAlts); r.Excl.AddRange(rd.Excl); r.Allow.AddRange(rd.Allow); r.Conds.AddRange(rd.Conds);
        return r;
    }

    /// <summary>Droga ochronna przebiegu: wariant z zależności specjalnej albo (null) z układu – pierwszy odcinek za końcem przebiegu.</summary>
    static void ChooseOverlap(Route r, ZalOchronna alt, string fromLayout)
    {
        r.OvSecs.Clear(); r.OvSw.Clear();
        if (r.NoOverlap) { }
        else if (alt != null) { r.OvSecs.AddRange(alt.Odcinki); r.OvSw.AddRange(alt.Zwrotnice.Where(z => z.Length > 1).Select(z => (z[..^1], z[^1..]))); }
        else if (fromLayout != null) r.OvSecs.Add(fromLayout);
        r.Overlap = r.OvSecs.FirstOrDefault();
    }

    /// <summary>Przeszkody z wyborem drogi ochronnej: pierwszy wariant bez przeszkód; gdy żadnego – przeszkody wariantu pierwszego.</summary>
    List<string> Obstacles(Route r, RouteDef rd, bool forSetting)
    {
        var alts = rd.OverlapAlts.Count > 0 && !rd.NoOverlap ? rd.OverlapAlts : new List<ZalOchronna> { null };
        List<string> first = null;
        foreach (var alt in alts)
        {
            ChooseOverlap(r, alt, rd.Overlap);
            var why = RouteObstacles(r, forSetting);
            if (why.Count == 0) return why;
            first ??= why;
        }
        ChooseOverlap(r, alts[0], rd.Overlap);
        return first;
    }

    /// <summary>Odcinek utwierdzony w innych przebiegach – tylko dopuszczonych zależnością specjalną (albo już w tym przebiegu).</summary>
    bool SharedOk(Route rt, SecState s) => s.Shared.Contains(rt.Uid) || new[] { s.Lock }.Concat(s.Shared).Where(u => u != null && u != rt.Uid).All(u => Routes.TryGetValue(u, out var o) && Dopuszczone(rt, o));

    void LockSec(string sc, string uid, string type)
    {
        var s = Secs[sc];
        if (s.Lock == null || s.Lock == uid) { s.Lock = uid; s.LockType = type; }
        else s.Shared.Add(uid);
    }

    /// <summary>Zwolnienie odcinka przez przebieg; przy wspólnym utwierdzeniu odcinek przechodzi na kolejny przebieg.</summary>
    void UnlockSec(string sc, string uid, bool timeRel)
    {
        var s = Secs[sc];
        if (s.Lock != uid) { s.Shared.Remove(uid); return; }
        s.Lock = null; s.LockType = null; if (timeRel) s.TimeRel = null;
        s.Shared.RemoveWhere(u => u == uid || !Routes.ContainsKey(u));
        var nx = s.Shared.FirstOrDefault();
        if (nx == null) return;
        s.Shared.Remove(nx); var o = Routes[nx];
        s.Lock = nx; s.LockType = o.Secs.Skip(o.Rel).Contains(sc) ? (o.Type == "P" ? "train" : "shunt") : "overlap";
    }

    /// <summary>Droga ochronna przejęta przez przebieg dalszy (jazda „na wprost”): odcinek, a gdy to ostatni – także zwrotnice.</summary>
    void DropOverlap(Route prv, string sc)
    {
        prv.OvSecs.Remove(sc); UnlockSec(sc, prv.Uid, false);
        if (prv.OvSecs.Count == 0) { foreach (var (num, _) in prv.OvSw) Sws[num].Ov.Remove(prv.Uid); prv.OvSw.Clear(); }
        prv.Overlap = prv.OvSecs.FirstOrDefault();
    }

    (bool Ok, List<string> Why, Route R) SetRoute(RouteDef rd)
    {
        var r = NewRoute(rd, "R" + seq++);
        var why = Obstacles(r, rd, true);
        if (why.Count > 0) return (false, why, null);
        foreach (var sc in r.Secs)
        {
            var s = Secs[sc];
            if (s.LockType == "overlap" && s.Lock != null && Routes.TryGetValue(s.Lock, out var prv) && prv.OvSecs.Contains(sc) && prv.End == r.Start) DropOverlap(prv, sc);   // przejęta droga ochronna
            LockSec(sc, r.Uid, r.Type == "P" ? "train" : "shunt");
        }
        foreach (var oc in r.OvSecs) if (!r.Secs.Contains(oc)) LockSec(oc, r.Uid, "overlap");
        foreach (var (num, pos) in r.OvSw) { var w = Sws[num]; w.Ov.Add(r.Uid); if ((w.Moving?.Target ?? w.Pos) != pos) MoveSwitch(num, pos, true); }
        foreach (var (num, pos) in r.Sw)
        {
            var w = Sws[num]; w.Lock = r.Uid;
            if (w.Pos != pos || (w.Moving != null && w.Moving.Target != pos)) MoveSwitch(num, pos, true);
        }
        foreach (var d in r.Ders) { Ders[d].Lock = r.Uid; DerCmd(d, false); }
        foreach (var (kind, id, fpos, _) in r.Flank)
        {
            if (kind == "sw") { var w = Sws[id]; w.Flank.Add(r.Uid); if ((w.Moving?.Target ?? w.Pos) != fpos) MoveSwitch(id, fpos, true); }
            else if (kind == "der") { var d = Ders[id]; d.Flank.Add(r.Uid); if (!(d.MovingTo ?? d.On)) DerCmd(id, true); }
            else Sigs[id].Flank.Add(r.Uid);
        }
        Routes[r.Uid] = r;
        Sigs[r.Start].Route = r.Uid;
        if (r.EndKind == "sig") Sigs[r.End].RouteEnd = r.Uid;
        return (true, null, r);
    }

    /// <summary>Warunki sygnału zezwalającego nastawionego przebiegu – przeszkody (puste – można otworzyć semafor).</summary>
    public List<string> SignalConditions(Route r)
    {
        var why = RouteObstacles(r, false);
        foreach (var (num, pos) in r.Sw) if (Sws[num].Pos != pos || !SwControlled(num)) why.Add($"zwr. {num} bez kontroli w położeniu {pos}");
        foreach (var d in r.Ders) if (Ders[d].On || Ders[d].MovingTo != null) why.Add($"wykolejnica {d} założona");
        foreach (var (kind, id, fpos, forSw) in r.Flank)
        {
            if (kind == "sw" && (Sws[id].Pos != fpos || !SwControlled(id)) && (forSw == null || Sws[forSw].Lock == r.Uid)) why.Add($"ochrona boczna: zwr. {id} bez kontroli w położeniu {fpos}");
            if (kind == "der" && (!Ders[id].On || Ders[id].MovingTo != null) && (forSw == null || Sws[forSw].Lock == r.Uid)) why.Add($"ochrona boczna: wykolejnica {id} nie założona");
        }
        foreach (var (num, pos) in r.OvSw) if (Sws[num].Pos != pos || !SwControlled(num)) why.Add($"droga ochronna: zwr. {num} bez kontroli w położeniu {pos}");
        return why;
    }

    public void ReleaseRoute(string uid, string reason)
    {
        if (!Routes.TryGetValue(uid, out var r)) return;
        foreach (var sc in r.Secs) UnlockSec(sc, uid, true);
        foreach (var oc in r.OvSecs) UnlockSec(oc, uid, false);
        foreach (var (num, _) in r.OvSw) Sws[num].Ov.Remove(uid);
        foreach (var (num, _) in r.Sw) if (Sws[num].Lock == uid) Sws[num].Lock = null;
        foreach (var d in r.Ders) if (Ders[d].Lock == uid) { Ders[d].Lock = null; DerCmd(d, true); }
        foreach (var f in r.Flank) ReleaseFlank(f, uid);
        var sg = Sigs[r.Start];
        if (sg.Route == uid) { sg.Route = null; if (sg.Aspect != Aspect.Sz) sg.Aspect = Aspect.Stop; }
        if (r.EndKind == "sig" && Sigs[r.End].RouteEnd == uid) Sigs[r.End].RouteEnd = null;
        Routes.Remove(uid);
        if (reason != null) Log("Zdarzenie", $"Zwolniono przebieg {r.Id} ({reason})", "Przebieg", r.Id);
    }

    internal void UpdateRoutes()
    {
        foreach (var uid in Routes.Keys.ToList())
        {
            if (!Routes.TryGetValue(uid, out var r)) continue;
            var sg = Sigs[r.Start];
            if (r.TimeRelUntil is double tr) { if (T >= tr) ReleaseRoute(uid, "zwolnienie czasowe"); continue; }

            // najechanie pierwszej sekcji – samoczynne „Stój” (§17 ust. 2 pkt 3)
            if (!r.Passed && Secs[r.Secs[0]].TrainOcc && !(r.Type == "M" && r.Secs.Count == 1))
            {
                if (sg.Aspect is Aspect.Train or Aspect.Shunt || (sg.Aspect == Aspect.Sz && r.Type == "M")) sg.Aspect = Aspect.Stop;
                r.Passed = true; r.State = "passed";
            }
            if (!r.Passed)
            {
                var why = SignalConditions(r);
                if (r.State == "setting" && why.Count == 0)
                {
                    if (sg.LampFault && r.Type == "P")
                    {
                        if (!sg.FailAlarm) { var rr = r; Alarm($"Semafor {r.Start}: nie wyświetlił sygnału zezwalającego (§26 ust. 1)", "Semafor", r.Start, stillActive: () => Routes.ContainsKey(rr.Uid) && !rr.Passed && Sigs[rr.Start].Aspect == Aspect.Stop); sg.FailAlarm = true; }
                    }
                    else if (!sg.NoData)
                    {
                        r.State = "set"; sg.Aspect = r.Type == "P" ? Aspect.Train : Aspect.Shunt;
                        if (r.Type == "M" && L.Signals[r.Start].Type == "wjazdowy") { sg.Aspect = Aspect.Sz; sg.SzUntil = T + Cfg.SzTime; }
                        Log("Zdarzenie", "Sygnał zezwalający – " + r.Id, "Semafor", r.Start);
                    }
                }
                else if (r.State == "set" && sg.Aspect == Aspect.Stop && r.Type == "M" && L.Signals[r.Start].Type == "wjazdowy") { }
                else if (r.State == "set" && why.Count > 0 && sg.Aspect != Aspect.Stop)
                {
                    sg.Aspect = Aspect.Stop; r.State = "setting";
                    Alarm($"Semafor {r.Start} zamknięty samoczynnie: {why[0]}", "Semafor", r.Start);
                }
                else if (r.State == "setting" && T - r.T0 > 20 && !r.SlowAlarm)
                {
                    r.SlowAlarm = true; var rr = r; Alarm($"Przebieg {r.Id} nie może zostać nastawiony: {(why.Count > 0 ? why[0] : "brak kontroli")}", "Przebieg", r.Id, stillActive: () => Routes.ContainsKey(rr.Uid) && rr.State == "setting");
                }
            }
            if (r.Passed)
            {
                while (r.Rel < r.Secs.Count)
                {
                    var sc = r.Secs[r.Rel]; var s = Secs[sc]; bool last = r.Rel == r.Secs.Count - 1;
                    if (s.TrainOcc) r.WasOcc.Add(sc);
                    // ostatni odcinek przebiegu na szlak zwalnia się dopiero po jego opuszczeniu (pociąg na szlaku trzyma kierunek blokady);
                    // przebieg do sygnalizatora – już po zajęciu ostatniego odcinka (pociąg zatrzymuje się na nim)
                    if (r.WasOcc.Contains(sc) && (!s.TrainOcc || (last && r.EndKind != "szlak")))
                    {
                        UnlockSec(sc, uid, false);
                        foreach (var (num, _) in r.Sw) if (L.Switches[num].Sec == sc && Sws[num].Lock == uid) Sws[num].Lock = null;
                        foreach (var d in r.Ders) if (L.Derailers[d].Sec == sc && Ders[d].Lock == uid) { Ders[d].Lock = null; DerCmd(d, true); }
                        foreach (var f in r.Flank) if (f.For != null && L.Switches[f.For].Sec == sc) ReleaseFlank(f, uid);
                        r.Rel++;
                    }
                    else break;
                }
                for (int i = r.Rel; i < r.Secs.Count; i++) if (Secs[r.Secs[i]].TrainOcc) r.WasOcc.Add(r.Secs[i]);
                if (r.Rel >= r.Secs.Count)
                {
                    r.DoneAt ??= T;
                    if (T - r.DoneAt > 8) ReleaseRoute(uid, null);
                }
            }
        }
    }

    void ReleaseFlank((string Kind, string Id, string Pos, string For) f, string uid)
    {
        if (f.Kind == "sw") Sws[f.Id].Flank.Remove(uid);
        else if (f.Kind == "der") Ders[f.Id].Flank.Remove(uid);
        else Sigs[f.Id].Flank.Remove(uid);
    }

    string FlankOwner(HashSet<string> set, string except) => string.Join(", ", set.Where(u => u != except).Select(u => Routes.TryGetValue(u, out var r) ? r.Id : u));

    void MoveSwitch(string num, string pos, bool byRoute = false)
    {
        var w = Sws[num];
        double time = L.Switches[num].Manual ? Cfg.SwTime * 4 : Cfg.SwTime;
        w.Moving = new MoveInfo { Target = pos, From = w.Pos, Until = T + time, Started = T };
        Urzadzenia.Przestaw(num, pos);
        Log("Zdarzenie", $"Przestawianie do „{pos}”" + (byRoute ? " (przebiegowo)" : ""), "Zwrotnica", num);
    }

    /// <summary>Kontrola praktyki przy sygnale zastępczym (§18 ust. 2, §27 ust. 1).</summary>
    void CheckSzPractice(string sigName)
    {
        var sig = L.Signals[sigName];
        string nid = sig.Node, from = null; var bad = new List<string>();
        for (int g = 0; g < 60; g++)
        {
            var nx = NextByState(nid, sig.Dir, from); if (nx == null) break;
            var node = L.Nodes[nid];
            if (node.Switch != null) { var w = Sws[node.Switch]; if (!w.Stopped && w.Lock == null && Power.NN) bad.Add(node.Switch); }
            var nn = L.Other(nx.Edge, nid);
            bool stop = L.Nodes[nn].Signals.Any(x => L.Signals[x].Dir == sig.Dir && L.Signals[x].Type != "odstepowy") || L.Boundaries.ContainsKey(nn);
            nid = nn; from = nx.Edge;
            if (stop) break;
        }
        if (bad.Count > 0) Remark($"Sygnał zastępczy na {sigName}: zwrotnice {string.Join(", ", bad)} nie zostały zabezpieczone (zastopowanie / przebieg manewrowy / NN0) – §18 ust. 2, §27 ust. 1 pkt 1");
        else Log("Ocena", $"Sygnał zastępczy na {sigName}: zwrotnice w drodze przebiegu zabezpieczone – zgodnie z §27", "Semafor", sigName);
    }
}

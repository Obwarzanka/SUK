using LcsWidzew.Model;
namespace LcsWidzew.Sim;

public partial class Simulator
{
    List<string> RouteObstacles(Route rt, bool forSetting)
    {
        var why = new List<string>();
        var sig = Sigs[rt.Start];
        if (sig.Closed) why.Add($"sygnalizator {rt.Start} zamknięty indywidualnie");
        if (forSetting && sig.Route != null) why.Add($"sygnalizator {rt.Start} jest już początkiem przebiegu");
        for (int i = 0; i < rt.Secs.Count; i++)
        {
            var sc = rt.Secs[i]; var s = Secs[sc]; bool last = i == rt.Secs.Count - 1;
            if (s.Closed) why.Add($"odcinek {sc} zamknięty");
            // droga ochronna przebiegu kończącego się na semaforze początkowym może zostać zajęta przez przebieg dalszy (jazda „na wprost” przez stację)
            bool overlapOfPrev = s.LockType == "overlap" && Routes.TryGetValue(s.Lock ?? "", out var prv) && prv.End == rt.Start && prv.Overlap == sc;
            if (s.Lock != null && s.Lock != rt.Uid && !overlapOfPrev) why.Add($"odcinek {sc} utwierdzony w innym przebiegu");
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
                if (w.Stopped) why.Add($"zwr. {num} zastopowana");
                if (!Power.NN) why.Add("wyłączone napięcie nastawcze");
                if (forSetting && SecOcc(L.Switches[num].Sec)) why.Add($"zwr. {num} – odcinek zajęty");
            }
        }
        if (rt.Overlap != null)
        {
            var o = Secs[rt.Overlap];
            if (SecOcc(rt.Overlap)) why.Add($"droga ochronna {rt.Overlap} zajęta");
            if (o.Lock != null && o.Lock != rt.Uid && o.LockType != "overlap") why.Add($"droga ochronna {rt.Overlap} utwierdzona w innym przebiegu");
        }
        if (rt.EndKind is "szlak" or "szlakM" && !BlockAllowsExit(rt.End, L.Signals[rt.Start].Dir)) why.Add($"blokada liniowa {rt.End}: brak kierunku WYJAZD, szlak zamknięty lub pierwszy odstęp zajęty");
        if (forSetting && rt.EndKind == "szlak" && PwlBlocks(rt.End, L.Signals[rt.Start].Dir)) why.Add($"blokada półsamoczynna {rt.End}: przeciwwtórność liniowa (Pwl) – sygnał zezwalający już był podany, najpierw blok początkowy ({rt.End} Po)");
        if (rt.Type == "M" && L.Signals[rt.Start].Type == "wjazdowy" && !rt.Passed)
        {
            // powrót taboru manewrowego ze szlaku – na sygnał zastępczy, tylko gdy tabor stoi w pierwszym odstępie za semaforem wjazdowym
            var sd = L.Signals[rt.Start]; var bl = L.Blocks[sd.Szlak];
            var fs = bl.FirstSec(-sd.Dir);
            if (!SecOcc(fs)) why.Add($"przebieg manewrowy od semafora wjazdowego {Station.Lbl(rt.Start)} tylko dla powrotu taboru ze szlaku – odstęp {Station.Lbl(fs)} wolny");
        }
        return why;
    }

    /// <summary>Przeszkody w nastawieniu przebiegu (diagnostyka testów).</summary>
    public List<string> WhyNot(RouteDef rd)
    {
        var r = new Route { Id = rd.Id, Start = rd.Start, End = rd.End, EndKind = rd.EndKind, Type = rd.Type, EndNode = rd.EndNode, Overlap = rd.Overlap, Uid = "probe" };
        r.Edges.AddRange(rd.Edges); r.Secs.AddRange(rd.Secs); r.Sw.AddRange(rd.Sw); r.Ders.AddRange(rd.Ders);
        return RouteObstacles(r, true);
    }

    (bool Ok, List<string> Why, Route R) SetRoute(RouteDef rd)
    {
        var r = new Route
        {
            Variant = rd.Variant, VCalc = rd.VCalc, VSet = rd.VSet, Id = rd.Id, Start = rd.Start, End = rd.End, EndKind = rd.EndKind, Type = rd.Type, EndNode = rd.EndNode, Overlap = rd.Overlap,
            Uid = "R" + seq++, T0 = T
        };
        r.Edges.AddRange(rd.Edges); r.Secs.AddRange(rd.Secs); r.Sw.AddRange(rd.Sw); r.Ders.AddRange(rd.Ders);
        var why = RouteObstacles(r, true);
        if (why.Count > 0) return (false, why, null);
        foreach (var sc in r.Secs)
        {
            var s = Secs[sc];
            if (s.LockType == "overlap" && s.Lock != null && Routes.TryGetValue(s.Lock, out var prv) && prv.Overlap == sc) prv.Overlap = null;   // przejęta droga ochronna
            s.Lock = r.Uid; s.LockType = r.Type == "P" ? "train" : "shunt";
        }
        if (r.Overlap != null && Secs[r.Overlap].Lock == null) { Secs[r.Overlap].Lock = r.Uid; Secs[r.Overlap].LockType = "overlap"; }
        foreach (var (num, pos) in r.Sw)
        {
            var w = Sws[num]; w.Lock = r.Uid;
            if (w.Pos != pos || (w.Moving != null && w.Moving.Target != pos)) MoveSwitch(num, pos, true);
        }
        foreach (var d in r.Ders) { Ders[d].Lock = r.Uid; Ders[d].On = false; }
        Routes[r.Uid] = r;
        Sigs[r.Start].Route = r.Uid;
        if (r.EndKind == "sig") Sigs[r.End].RouteEnd = r.Uid;
        return (true, null, r);
    }

    List<string> SignalConditions(Route r)
    {
        var why = RouteObstacles(r, false);
        foreach (var (num, pos) in r.Sw) if (Sws[num].Pos != pos || !SwControlled(num)) why.Add($"zwr. {num} bez kontroli w położeniu {pos}");
        foreach (var d in r.Ders) if (Ders[d].On || Ders[d].MovingTo != null) why.Add($"wykolejnica {d} założona");
        return why;
    }

    public void ReleaseRoute(string uid, string reason)
    {
        if (!Routes.TryGetValue(uid, out var r)) return;
        foreach (var sc in r.Secs) { var s = Secs[sc]; if (s.Lock == uid) { s.Lock = null; s.LockType = null; s.TimeRel = null; } }
        if (r.Overlap != null && Secs[r.Overlap].Lock == uid) { Secs[r.Overlap].Lock = null; Secs[r.Overlap].LockType = null; }
        foreach (var (num, _) in r.Sw) if (Sws[num].Lock == uid) Sws[num].Lock = null;
        foreach (var d in r.Ders) if (Ders[d].Lock == uid) { Ders[d].Lock = null; Ders[d].On = true; }
        var sg = Sigs[r.Start];
        if (sg.Route == uid) { sg.Route = null; if (sg.Aspect != Aspect.Sz) sg.Aspect = Aspect.Stop; }
        if (r.EndKind == "sig" && Sigs[r.End].RouteEnd == uid) Sigs[r.End].RouteEnd = null;
        Routes.Remove(uid);
        if (reason != null) Log("Zdarzenie", $"Zwolniono przebieg {r.Id} ({reason})", "Przebieg", r.Id);
    }

    void UpdateRoutes()
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
                        if (s.Lock == uid) { s.Lock = null; s.LockType = null; }
                        foreach (var (num, _) in r.Sw) if (L.Switches[num].Sec == sc && Sws[num].Lock == uid) Sws[num].Lock = null;
                        foreach (var d in r.Ders) if (L.Derailers[d].Sec == sc && Ders[d].Lock == uid) { Ders[d].Lock = null; Ders[d].On = true; }
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

    void MoveSwitch(string num, string pos, bool byRoute = false)
    {
        var w = Sws[num];
        double time = L.Switches[num].Manual ? Cfg.SwTime * 4 : Cfg.SwTime;
        w.Moving = new MoveInfo { Target = pos, From = w.Pos, Until = T + time, Started = T };
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

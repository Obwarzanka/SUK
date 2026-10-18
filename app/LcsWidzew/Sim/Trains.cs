using LcsWidzew.Model;

namespace LcsWidzew.Sim;

public class Seg { public string E, From, To; public double Len; public int Dir; public bool Sink; public string Szlak; }

public class Train
{
    public string Id, Num, Cat, To, From, Track, WaitingAt, Window, DwellSecDone;
    public double Len, VMax, V, Head, DwellSec, DwellUntil, Waited, Planned, StoppedSince;
    public double? DepAt, ReadySince, ParkedAt, Entered;
    public List<Seg> Segs = new();
    public bool Stop, DwellDone, Shunt, Loco, SzLimit, Gone, Derailed;
    public string State = "run";
    public TrainTerm Term;
    public RgbColor Color;
    public HashSet<string> StopAt;                       // posterunki z postojem (null – każdy peron)
    public Dictionary<string, double> DepBy = new();     // odjazd wg rozkładu na posterunku
    public UnitDuty Duty;                                // jednostka z zaplecza: po wjeździe na tor peronowy staje się pociągiem
    public bool LoggedStuck;
    public double WaitingSince(double now) => StoppedSince > 0 ? now - StoppedSince : 0;
}

/// <summary>Zmiana czoła / zakończenie biegu na posterunku; Then – kolejna zmiana (łańcuch dla pociągów z rozkładu).</summary>
public class TrainTerm
{
    public bool Reverse, Odstaw; public string NewNum, To, AtPost, NewCat; public double? NewDep;
    public HashSet<string> NewStopAt; public Dictionary<string, double> NewDepBy; public TrainTerm Then;
    public string ShuntNum; public UnitDuty NewDuty;   // odstawienie: numer manewrowy jednostki i jej następny bieg
}

/// <summary>Jednostka ŁKA w zaplecz­u: manewr na tor peronowy, potem odjazd jako pociąg.</summary>
public class UnitDuty
{
    public string Num, To, Unit, Track; public double Dep; public bool Armed; public int Dir;
    public TrainTerm Term; public HashSet<string> StopAt; public Dictionary<string, double> DepBy;   // dalszy bieg pociągu z rozkładu
}

public class TimetableEntry
{
    public double T; public string Num, Cat, From, To, Track; public bool Stop; public double? Dep; public TrainTerm Term; public bool Done;
    public string StartSec;   // pociąg rozpoczynający bieg na torze stacyjnym (np. Łódź Fabryczna)
    public int StartDir = 1;
    public bool Loco;         // lokomotywa manewrowa
    public string Name, Veh;  // nazwa pociągu, pojazd (rozkład rzeczywisty)
    public double? Len;
    public HashSet<string> StopAt;
    public Dictionary<string, double> DepBy;
    public UnitDuty Duty;
}

public class CatDef { public double Len, VMax, Dwell; public RgbColor Color; }

/// <summary>Ruch pociągów i manewrów, posterunki sąsiednie, scenariusze, usterki.</summary>
public partial class Trains
{
    readonly Simulator S;
    public List<Train> List = new();
    int seq = 1;
    const double Acc = 0.5, Brake = 0.8, VStation = 60, VSz = 20, VShunt = 25;

    public static readonly Dictionary<string, CatDef> Cats = new()
    {
        ["ŁKA"] = new() { Len = 90, VMax = 100, Dwell = 40, Color = RgbColor.FromRgb(0x7f, 0xd4, 0xff) },
        ["PR"] = new() { Len = 130, VMax = 100, Dwell = 45, Color = RgbColor.FromRgb(0x9c, 0xf0, 0x9c) },
        ["IC"] = new() { Len = 260, VMax = 120, Dwell = 60, Color = RgbColor.FromRgb(0xff, 0xd2, 0x7f) },
        ["TOW"] = new() { Len = 480, VMax = 70, Dwell = 0, Color = RgbColor.FromRgb(0xd0, 0xb0, 0xff) },
        ["MAN"] = new() { Len = 18, VMax = 25, Dwell = 0, Color = RgbColor.FromRgb(0xff, 0xff, 0xff) },
        ["TLK"] = new() { Len = 260, VMax = 120, Dwell = 60, Color = RgbColor.FromRgb(0xff, 0xd2, 0x7f) },
        ["EIC"] = new() { Len = 260, VMax = 120, Dwell = 60, Color = RgbColor.FromRgb(0xff, 0xd2, 0x7f) },
        ["PRÓŻ"] = new() { Len = 190, VMax = 80, Dwell = 0, Color = RgbColor.FromRgb(0xc0, 0xc0, 0xc0) },
        ["LUZ"] = new() { Len = 20, VMax = 80, Dwell = 0, Color = RgbColor.FromRgb(0xc0, 0xc0, 0xc0) }
    };

    public Trains(Simulator s) { S = s; s.Trains = this; }

    static double Total(Train t) => t.Segs.Sum(s => s.Len);
    bool StationEdge(string e) => e != null && S.L.Sections[S.L.Edges[e].Sec].Kind != "szlak";

    bool Permits(string name, Train t)
    {
        // maszynista widzi sygnał wyświetlany przez kartę sygnalizatora (nie stan w komputerze zależności)
        var w = S.Urzadzenia.SygnalWidziany(name);
        var a = w != null ? Sygnaly.AspectOf(w) : S.Sigs[name].NoData ? Aspect.Stop : S.Sigs[name].Aspect;
        return a switch { Aspect.Train => true, Aspect.Sz => true, Aspect.Shunt => t.Shunt, _ => false };
    }
    IEnumerable<string> SigsAt(string nid, int dir) => S.L.Nodes[nid].Signals.Where(n => S.L.Signals[n].Dir == dir);
    IEnumerable<string> SigsFor(string nid, int dir, Train t) => SigsAt(nid, dir).Where(n => t.Shunt ? S.L.Signals[n].StopsShunts : S.L.Signals[n].ForTrains);

    // ------------------------------------------------------------------ pojawienie się pociągu
    Train NewTrain(TimetableEntry q)
    {
        var c = Cats.GetValueOrDefault(q.Cat) ?? Cats["ŁKA"];
        var t = new Train
        {
            Id = "T" + seq++, Num = q.Num, Cat = q.Cat, Len = c.Len, VMax = c.VMax / 3.6, Stop = q.Stop, DwellSec = c.Dwell, DepAt = q.Dep, Term = q.Term,
            To = q.To, From = q.From, Track = q.Track, Planned = q.T, Color = c.Color, Loco = q.Loco, Shunt = q.Loco,
            StopAt = q.StopAt, Duty = q.Duty
        };
        if (q.Len is double ln) t.Len = ln;
        if (q.DepBy != null) t.DepBy = new(q.DepBy);
        List.Add(t);
        return t;
    }

    /// <summary>Pociąg wyprawiony przez posterunek sąsiedni (Otoczenie) – pojawia się na szlaku przed granicą obszaru.</summary>
    public void SpawnFromSzlak(TimetableEntry q)
    {
        var b = S.L.Blocks[q.From]; int inward = -b.Ends[0].Outward;
        var t = NewTrain(q); t.V = t.VMax * 0.8;
        t.Segs.Add(new Seg { E = null, From = null, To = b.Far, Len = t.Len + 5, Dir = inward });
        t.Head = t.Len + 5;
        S.Log("Ruch", $"Pociąg {t.Num} ({t.Cat}) wyprawiony z posterunku {b.Nbr} na szlak {q.From}", "Pociąg", t.Num);
        // zapowiedź posterunku sąsiedniego: numer pociągu do PIP na pierwszym odstępie od granicy
        S.Nadrzedny.Pip.Nadaj(t.Num, S.L.Nodes[b.Far].Edges.Select(e => S.L.Edges[e].Sec).FirstOrDefault());
    }

    /// <summary>Pociąg / lokomotywa stojąca na torze – czoło przy końcu toru w kierunku jazdy.</summary>
    public bool SpawnOnTrack(TimetableEntry q)
    {
        var sec = q.StartSec;
        if (!S.L.Sections.ContainsKey(sec) || S.SecOcc(sec)) return false;
        var edges = S.L.Sections[sec].Edges.Select(e => S.L.Edges[e]).OrderBy(e => Math.Min(S.L.Nodes[e.A].X, S.L.Nodes[e.B].X)).ToList();
        if (q.StartDir < 0) edges.Reverse();
        var t = NewTrain(q);
        foreach (var e in edges)
        {
            bool fwd = S.L.EdgeDir(e.Id, e.A) == q.StartDir;
            t.Segs.Add(new Seg { E = e.Id, From = fwd ? e.A : e.B, To = fwd ? e.B : e.A, Len = e.Len, Dir = q.StartDir });
        }
        double tot = Total(t);
        t.Head = Math.Max(Math.Min(tot - 25, tot), t.Len + 1);
        if (t.Head > tot) t.Head = tot;
        t.DwellDone = true; t.Stop = false; t.State = "run"; t.ReadySince = S.T;
        if (!q.Loco) { t.State = "dwell"; t.DwellUntil = Math.Max(S.T + 20, q.Dep ?? 0); t.DwellDone = false; t.Stop = q.Stop; t.DwellSecDone = sec; }
        S.Nadrzedny.Pip.Nadaj(t.Num, sec);   // numer z rozkładu na torze postoju
        S.Log("Ruch", q.Loco ? $"Lokomotywa manewrowa {t.Num} na torze {Station.Lbl(sec)} – oczekuje na przebiegi manewrowe" : $"Pociąg {t.Num} ({t.Cat}) podstawiony na tor {Station.Lbl(sec)}, odjazd {S.Clock(t.DwellUntil)} – do {q.To}", "Pociąg", t.Num);
        return true;
    }

    // ------------------------------------------------------------------ ruch
    (double D, string Sig, bool Dwell, bool End) Lookahead(Train t)
    {
        var last = t.Segs[^1];
        if (last.Sink) return (double.PositiveInfinity, null, false, false);
        double dist = Total(t) - t.Head; string node = last.To, from = last.E, prevE = last.E; int dir = last.Dir;
        for (int k = 0; k < 60; k++)
        {
            var sg = SigsFor(node, dir, t).ToList();
            if (t.Stop && !t.DwellDone && prevE != null && S.L.Edges[prevE].Platform && sg.Count > 0 && S.L.Edges[prevE].Sec != t.DwellSecDone
                && (t.StopAt == null || t.StopAt.Contains(Station.PostOf(S.L.Edges[prevE].Sec)))) return (dist - 15, null, true, false);
            foreach (var s in sg) if (!Permits(s, t)) return (dist - 8, s, false, false);
            if (t.Shunt && S.L.TrackEnds.ContainsKey(node)) return (dist - 10, null, false, true);
            // PK – punkt końcowy przebiegu: dalej tylko w nastawionym przebiegu pociągowym (np. od semafora sąsiedniego posterunku)
            if (!t.Shunt && SigsAt(node, dir).Any(x => S.L.Signals[x].PkTrain) && S.NextInTerrain(node, dir, from) is { } pkn && S.Secs[S.L.Edges[pkn.Edge].Sec].LockType != "train")
                return (dist - 8, null, false, true);
            if (t.Shunt && k > 0 && !InShuntRoute(prevE) && prevE != null) return (dist - S.L.Edges[prevE].Len - 6, null, false, true);
            if (S.L.FarEnds.TryGetValue(node, out var fe) && dir == S.L.Blocks[fe.Szlak].Ends[0].Outward) return (double.PositiveInfinity, null, false, false);
            var nx = S.NextInTerrain(node, dir, from);
            if (nx == null) return (dist - 5, null, false, true);
            dist += S.L.Edges[nx.Edge].Len; from = nx.Edge; prevE = nx.Edge; node = S.L.Other(nx.Edge, node);
            if (dist > 2500) break;
        }
        return (double.PositiveInfinity, null, false, false);
    }

    /// <summary>Manewry poruszają się wyłącznie w obrębie utwierdzonych przebiegów manewrowych (lub toru, na którym stoją).</summary>
    bool InShuntRoute(string eid)
    {
        if (eid == null) return true;
        var sc = S.Secs[S.L.Edges[eid].Sec];
        return sc.LockType is "shunt" || S.Urzadzenia.Pojazd(sc.Id);
    }

    bool Extend(Train t)
    {
        var last = t.Segs[^1];
        if (last.Sink) return false;
        string node = last.To; int dir = last.Dir;
        foreach (var n in SigsFor(node, dir, t).ToList())
        {
            if (S.L.Signals[n].Type == "odstepowy") continue;
            if (!Permits(n, t)) { S.Alarm($"{(t.Shunt ? "Skład" : "Pociąg")} {t.Num} minął sygnalizator {n} wskazujący „Stój”!", "Semafor", n, "spad" + t.Id + n); S.Stats.Spad++; }
            t.SzLimit = (S.Urzadzenia.SygnalWidziany(n) ?? (S.Sigs[n].Aspect == Aspect.Sz ? "Sz" : null)) == "Sz";
            if (S.L.Signals[n].Type == "wjazdowy" && t.Entered == null) { t.Entered = S.T; S.Stats.TrainsIn++; }
        }
        if (S.L.FarEnds.TryGetValue(node, out var fe) && dir == S.L.Blocks[fe.Szlak].Ends[0].Outward)
        {
            t.Segs.Add(new Seg { E = null, From = node, To = null, Len = 1e6, Dir = dir, Sink = true, Szlak = fe.Szlak });
            return true;
        }
        var nx = S.NextInTerrain(node, dir, last.E);
        if (nx == null) return false;
        if (nx.Num != null)
        {
            // najechanie od gałęzi: przy iglicach w innym położeniu karta melduje brak kontroli, KZ rozstrzyga o rozpruciu
            if (S.L.Switches[nx.Num].TipDir == dir) S.Urzadzenia.Najazd(nx.Num, nx.Pos);
        }
        var der = S.L.Derailers.Values.FirstOrDefault(d => d.Node == node);
        if (der != null && S.Urzadzenia.WykolejnicaNaTorze(der.Name)) { S.Alarm($"WYKOLEJENIE {t.Num} na wykolejnicy {der.Name}!", "Wykolejnica", der.Name); t.Derailed = true; }
        t.Segs.Add(new Seg { E = nx.Edge, From = node, To = S.L.Other(nx.Edge, node), Len = S.L.Edges[nx.Edge].Len, Dir = dir });
        return true;
    }

    void Reverse(Train t)
    {
        double tail = t.Head - t.Len; var kept = new List<Seg>();
        foreach (var s in t.Segs) { if (s.E != null) kept.Add(s); else if (kept.Count == 0) tail -= s.Len; }
        double tot = kept.Sum(s => s.Len);
        kept.Reverse();
        t.Segs = kept.Select(s => new Seg { E = s.E, From = s.To, To = s.From, Len = s.Len, Dir = -s.Dir }).ToList();
        t.Head = tot - Math.Max(0, tail); t.V = 0; t.SzLimit = false;
    }

    string HeadSec(Train t) { for (int i = t.Segs.Count - 1; i >= 0; i--) if (t.Segs[i].E != null) return S.L.Edges[t.Segs[i].E].Sec; return null; }

    string PostOfTrain(Train t)
    {
        for (int i = t.Segs.Count - 1; i >= 0; i--) { var e = t.Segs[i].E; if (e != null) return Station.PostOf(S.L.Edges[e].Sec); }
        return "";
    }

    string TrackOf(Train t)
    {
        for (int i = t.Segs.Count - 1; i >= 0; i--) { var e = t.Segs[i].E; if (e != null && S.L.Edges[e].Track != "") return S.L.Edges[e].Track; }
        return "?";
    }

    /// <summary>Sygnalizatory za składem (przy jego końcu), od których można podać przebieg w kierunku przeciwnym.</summary>
    public List<string> SignalsBehind(Train t)
    {
        int dir = t.Segs[^1].Dir; double tail = t.Head - t.Len, pos = 0;
        var nodes = new List<string>();
        foreach (var s in t.Segs) { if (s.E != null && pos + s.Len >= tail - 60) { nodes.Add(s.From); nodes.Add(s.To); } pos += s.Len; }
        return nodes.Distinct().SelectMany(n => SigsAt(n, -dir)).Where(x => S.L.Signals[x].ForShunts).Distinct().ToList();
    }

    /// <summary>Lokomotywa manewrowa zmienia kierunek, gdy za nią (po przeciwnej stronie) podano sygnał manewrowy.</summary>
    void ShuntReverseCheck(Train t)
    {
        if (!t.Shunt || t.V > 0.05 || S.T - t.StoppedSince < 4) return;
        int dir = t.Segs[^1].Dir;
        double tail = t.Head - t.Len, pos = 0;
        var nodes = new List<string>();
        foreach (var s in t.Segs) { if (s.E != null && pos + s.Len >= tail - 60) { nodes.Add(s.From); nodes.Add(s.To); } pos += s.Len; }
        foreach (var n in nodes.Distinct())
            foreach (var sg in SigsAt(n, -dir))
                if (S.L.Signals[sg].ForShunts && S.Sigs[sg].Aspect is Aspect.Shunt or Aspect.Train or Aspect.Sz && S.Routes.Values.Any(r => r.Start == sg && r.Type == "M"))
                {
                    Reverse(t); S.Log("Ruch", $"Skład {t.Num} – zmiana kierunku jazdy (sygnał manewrowy na {Station.Lbl(sg)})", "Pociąg", t.Num);
                    return;
                }
    }

    void Move(Train t, double dt)
    {
        if (t.Derailed) { t.V = 0; return; }
        if (t.State == "dwell")
        {
            t.V = 0;
            if (S.T >= t.DwellUntil)
            {
                t.DwellDone = false; t.State = "run";
                var term = t.Term != null && (t.Term.AtPost == null || t.Term.AtPost == PostOfTrain(t)) ? t.Term : null;
                if (term == null && t.Term != null) { S.Log("Ruch", $"Pociąg {t.Num} gotowy do odjazdu z toru {TrackOf(t)}", "Pociąg", t.Num); t.ReadySince = S.T; t.DwellDone = false; return; }
                if (t.Term?.Reverse == true)
                {
                    var tm = t.Term;
                    Reverse(t); var old = t.Num; t.Num = tm.NewNum ?? t.Num; t.To = tm.To; t.DepAt = S.T; S.Nadrzedny.Pip.ZmienNumer(old, t.Num);
                    t.Stop = tm.NewStopAt?.Count > 0; t.StopAt = tm.NewStopAt; t.DepBy = tm.NewDepBy ?? new(); t.DwellSecDone = null;
                    if (tm.NewCat != null) { t.Cat = tm.NewCat; t.Color = (Cats.GetValueOrDefault(tm.NewCat) ?? Cats["IC"]).Color; }
                    t.Term = tm.Then;
                    var what = old == t.Num ? $"Pociąg {old} – zmiana czoła" : $"Pociąg {old} zakończył bieg – zmiana czoła, dalej jako {t.Num}";
                    if (tm.NewDep is double nd && nd > S.T) { t.State = "dwell"; t.DwellUntil = nd; S.Log("Ruch", $"{what}, odjazd o {S.Clock(nd)}{(t.To != null ? " na szlak " + t.To : "")}", "Pociąg", t.Num); return; }
                    S.Log("Ruch", $"{what}{(t.To != null ? ", odjazd na szlak " + t.To : "")}", "Pociąg", t.Num);
                }
                else if (term != null && !term.Reverse && !term.Odstaw)
                {
                    // zmiana numeru bez zmiany czoła (pociąg jedzie dalej w tym samym kierunku jako inny pociąg)
                    var old = t.Num; t.Num = term.NewNum ?? t.Num; S.Nadrzedny.Pip.ZmienNumer(old, t.Num); t.To = term.To; t.Stop = term.NewStopAt?.Count > 0; t.StopAt = term.NewStopAt; t.DepBy = term.NewDepBy ?? new(); t.Term = term.Then;
                    if (term.NewDep is double nd2 && nd2 > S.T) { t.State = "dwell"; t.DwellUntil = nd2; }
                    S.Log("Ruch", $"Pociąg {old} zakończył bieg – dalej jako {t.Num}{(t.To != null ? " na szlak " + t.To : "")}", "Pociąg", t.Num);
                    if (t.State == "dwell") return;
                }
                else if (t.Term?.Odstaw == true)
                {
                    var tm = t.Term; var old = t.Num;
                    Reverse(t); t.Shunt = true; t.Stop = false; t.StopAt = null; t.Num = tm.ShuntNum ?? t.Num + "M"; S.Nadrzedny.Pip.ZmienNumer(old, t.Num); t.Duty = tm.NewDuty; t.Term = tm.Then;
                    S.Log("Ruch", tm.NewDuty != null
                        ? $"Pociąg {old} zakończył bieg – zmiana numeru na jednostkę {t.Num}, do odstawienia w bazie ŁKA (manewr); następny bieg: pociąg {tm.NewDuty.Num} o {S.Clock(tm.NewDuty.Dep)}"
                        : $"Pociąg {old} zakończył bieg – skład {t.Num} do odstawienia (przebieg manewrowy na tor postojowy)", "Pociąg", t.Num);
                }
                else S.Log("Ruch", $"Pociąg {t.Num} gotowy do odjazdu z toru {TrackOf(t)} (kierunek: {t.To})", "Pociąg", t.Num);
                t.ReadySince = S.T;
            }
            return;
        }
        var la = Lookahead(t);
        double vmax = t.VMax;
        if (t.SzLimit) vmax = Math.Min(vmax, VSz / 3.6);
        if (t.Shunt) vmax = Math.Min(vmax, VShunt / 3.6);
        double vt = double.IsFinite(la.D) ? Math.Sqrt(2 * Brake * Math.Max(0, la.D)) : vmax;
        vt = Math.Min(vt, vmax);
        vt = Math.Min(vt, SpeedLimit(t));
        if (la.D < 0.5) vt = 0;
        t.V = Math.Min(t.V + Acc * dt, vt);
        if (t.V < 0.05 && la.D < 1)
        {
            if (t.V > 0 || t.StoppedSince <= 0) t.StoppedSince = S.T;
            t.V = 0;
            if (la.Dwell) { t.State = "dwell"; t.DwellUntil = Math.Max(S.T + t.DwellSec, t.DepBy.TryGetValue(PostOfTrain(t), out var dp) ? dp : t.DepAt ?? 0); t.DwellSecDone = HeadSec(t); S.Log("Ruch", $"Pociąg {t.Num} – postój na torze {TrackOf(t)}", "Pociąg", t.Num); return; }
            if (la.Sig != null && !t.Shunt) { t.Waited += dt; S.Stats.DelaySum += dt; t.WaitingAt = la.Sig; }
            if (la.Sig != null && t.Shunt) t.WaitingAt = la.Sig;
            if (t.Duty != null && !t.Duty.Armed && t.Shunt && (HeadSec(t)?.EndsWith("JT102") == true || HeadSec(t)?.EndsWith("JT104") == true))
            {
                t.Duty.Armed = true;
                if (t.Duty.Num == null) { t.Gone = true; S.Log("Ruch", $"Skład {t.Num} odstawiony w bazie ŁKA", "Pociąg", t.Num); }
                else S.Log("Ruch", $"Jednostka {t.Num} odstawiona w bazie ŁKA – następny bieg: pociąg {t.Duty.Num} o {S.Clock(t.Duty.Dep)}", "Pociąg", t.Num);
            }
            // skład odstawiony w bazie ŁKA (bez dalszego biegu) – schodzi z planszy
            if (t.Shunt && !t.Loco && t.Duty == null && (HeadSec(t)?.EndsWith("JT102") == true || HeadSec(t)?.EndsWith("JT104") == true))
            { t.ParkedAt ??= S.T; if (S.T - t.ParkedAt > 30) { t.Gone = true; S.Log("Ruch", $"Skład {t.Num} odstawiony w bazie ŁKA", "Pociąg", t.Num); } }
            if (la.End && t.Shunt && !t.Loco && t.Duty == null && S.L.TrackEnds.Keys.Any(k => t.Segs.Any(s => s.To == k || s.From == k)))
            { t.ParkedAt ??= S.T; if (S.T - t.ParkedAt > 20) { t.Gone = true; S.Log("Ruch", $"Skład {t.Num} odstawiony", "Pociąg", t.Num); } }
            if (t.Duty != null && t.Duty.Armed && t.Shunt && (HeadSec(t)?.Contains(":JT") ?? false) && !(HeadSec(t)?.EndsWith("JT102") ?? false) && !(HeadSec(t)?.EndsWith("JT104") ?? false) && S.T - t.StoppedSince > 5) DutyToTrain(t);
            ShuntReverseCheck(t);
            return;
        }
        t.StoppedSince = 0;
        t.WaitingAt = null;
        double mv = t.V * dt;
        if (double.IsFinite(la.D)) mv = Math.Min(mv, Math.Max(0, la.D));
        t.Head += mv;
        for (int g = 0; g < 10 && t.Head > Total(t); g++)
            if (!Extend(t)) { t.Head = Total(t); t.V = 0; break; }
        double tail = t.Head - t.Len;
        while (t.Segs.Count > 1 && t.Segs[0].Len < tail) { tail -= t.Segs[0].Len; t.Head -= t.Segs[0].Len; t.Segs.RemoveAt(0); }
        var lastSeg = t.Segs[^1];
        if (lastSeg.Sink && t.Head - t.Len > Total(t) - lastSeg.Len) { t.Gone = true; Leave(t, lastSeg.Szlak); }
    }

    /// <summary>Jednostka z zaplecza stanęła na torze peronowym – staje się pociągiem (zmiana czoła, jeśli trzeba).</summary>
    void DutyToTrain(Train t)
    {
        var d = t.Duty; t.Duty = null;
        var b = S.L.Blocks.GetValueOrDefault(d.To);
        int outDir = d.Dir != 0 ? d.Dir : b == null ? t.Segs[^1].Dir : b.Ends.FirstOrDefault(x => x.Post == PostOfTrain(t))?.Outward ?? b.Ends[0].Outward;
        if (outDir != t.Segs[^1].Dir) Reverse(t);
        var unit = t.Num;
        t.Shunt = false; t.Loco = false; t.Num = d.Num; S.Nadrzedny.Pip.ZmienNumer(unit, t.Num); t.To = d.To; t.Stop = true; t.StopAt = d.StopAt ?? new() { PostOfTrain(t) };
        if (d.Term != null) t.Term = d.Term;
        if (d.DepBy != null) t.DepBy = new(d.DepBy);
        t.State = "dwell"; t.DwellUntil = Math.Max(S.T + 30, d.Dep); t.DwellDone = false; t.DwellSecDone = HeadSec(t);
        S.Log("Ruch", $"Jednostka {unit} z zaplecza na torze {TrackOf(t)} – zmiana numeru na pociąg {t.Num}, odjazd {S.Clock(t.DwellUntil)} na szlak {d.To}", "Pociąg", t.Num);
    }

    /// <summary>Prędkość dopuszczalna toru dla pociągu [m/s]: tor (vmax) i zwrotnice (jazda na bok, na / z ostrza).</summary>
    double EdgeLimit(string eid)
    {
        var e = S.L.Edges[eid];
        double v = e.VMax > 0 ? e.VMax : StationEdge(eid) ? VStation : 1e4;
        if (e.SwLimit > 0) v = Math.Min(v, e.SwLimit);
        // przebieg pociągowy ustalony z prędkością (edytor – reguła przebiegu)
        foreach (var r in S.Routes.Values) if (r.Type == "P" && r.VSet > 0 && r.Edges.Contains(eid)) v = Math.Min(v, r.VSet);
        return v / 3.6;
    }

    /// <summary>
    /// Profil prędkości: najniższe ograniczenie pod całym składem (pociąg przyspiesza dopiero, gdy koniec minie ograniczenie)
    /// i ograniczenia przed czołem z uwzględnieniem drogi hamowania.
    /// </summary>
    public double SpeedLimit(Train t)
    {
        double lim = 1e4, pos = 0, tail = t.Head - t.Len;
        foreach (var sg in t.Segs) { double a = pos, b = pos + sg.Len; pos = b; if (sg.E != null && b > tail && a < t.Head) lim = Math.Min(lim, EdgeLimit(sg.E)); }
        var last = t.Segs[^1]; if (last.Sink || last.E == null) return lim;
        double dist = Total(t) - t.Head; string node = last.To, from = last.E; int dir = last.Dir;
        for (int k = 0; k < 40 && dist < 2500; k++)
        {
            var nx = S.NextInTerrain(node, dir, from); if (nx == null) break;
            var l = EdgeLimit(nx.Edge);
            if (l < 1e3) lim = Math.Min(lim, Math.Sqrt(l * l + 2 * Brake * Math.Max(0, dist)));
            dist += S.L.Edges[nx.Edge].Len; from = nx.Edge; node = S.L.Other(nx.Edge, node);
        }
        return lim;
    }

    void Leave(Train t, string sz)
    {
        S.Stats.TrainsOut++;
        S.Log("Ruch", $"Pociąg {t.Num} opuścił obszar LCS – szlak {sz} ({S.L.Blocks[sz].Nbr})", "Pociąg", t.Num);
        if (t.To != null && t.To != sz) { S.Stats.WrongDir++; S.Remark($"Pociąg {t.Num} wyprawiony na szlak {sz} zamiast {t.To}"); }
    }

    /// <summary>Osie pojazdów na odcinkach – zgłoszenie do urządzeń przytorowych (karty odcinków liczą zajętość dla KZ).</summary>
    void Occupancy()
    {
        var occ = new HashSet<string>();
        foreach (var t in List)
        {
            double pos = 0, tail = t.Head - t.Len;
            string headSec = null;
            foreach (var s in t.Segs)
            {
                double a = pos, b = pos + s.Len; pos = b;
                if (s.E == null) continue;
                if (Math.Min(b, t.Head) - Math.Max(a, tail) > 0) { var sc = S.L.Edges[s.E].Sec; occ.Add(sc); if (t.Head >= a) headSec = sc; }
            }
            // wyświetlacz numeru pociągu (Ie-104 § 8 pkt 26): tory stacyjne i odstępy szlakowe
            if (headSec != null && (headSec.Contains(":JT") || S.L.Sections[headSec].Kind == "szlak")) t.Window = headSec;
        }
        S.Urzadzenia.Osie(occ);
    }

    /// <summary>Nowy scenariusz: rozkład jazdy (Rozklad) i usterki (Instruktor).</summary>
    public void Load(string key)
    {
        List.Clear(); seq = 1; S.Nadrzedny.Pip.Numery.Clear();
        S.Rozklad.Load(key);
        S.Instruktor.Load(key);
    }

    public bool AddLoco(string sec, int dir, string num)
    {
        var q = new TimetableEntry { T = S.T, Num = string.IsNullOrWhiteSpace(num) ? "SM" + (40 + seq++) : num, Cat = "MAN", StartSec = sec, StartDir = dir, Loco = true };
        if (!SpawnOnTrack(q)) return false;
        q.Done = true; S.Rozklad.Queue.Add(q);
        return true;
    }

    public void Tick(double dt)
    {
        foreach (var t in List.ToList()) Move(t, dt);
        List.RemoveAll(t => t.Gone);
        Occupancy();
    }

    public (double X, double Y, int Dir)? HeadPos(Train t)
    {
        double pos = 0;
        for (int i = 0; i < t.Segs.Count; i++)
        {
            var s = t.Segs[i];
            if (t.Head <= pos + s.Len || i == t.Segs.Count - 1)
            {
                if (s.E == null) { var n = S.L.Nodes[s.To ?? s.From]; return (n.X, n.Y, s.Dir); }
                var f = Math.Clamp((t.Head - pos) / s.Len, 0, 1);
                var p = PointAlong(s.E, s.From, f);
                return (p.X, p.Y, s.Dir);
            }
            pos += s.Len;
        }
        return null;
    }

    public (double X, double Y) PointAlong(string eid, string from, double f)
    {
        var e = S.L.Edges[eid];
        var pts = e.A == from ? e.Pts : Enumerable.Reverse(e.Pts).ToList();
        double total = 0; for (int i = 0; i < pts.Count - 1; i++) total += Dist(pts[i], pts[i + 1]);
        double target = total * f, acc = 0;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            var d = Dist(pts[i], pts[i + 1]);
            if (acc + d >= target) { var u = d > 0 ? (target - acc) / d : 0; return (pts[i].X + (pts[i + 1].X - pts[i].X) * u, pts[i].Y + (pts[i + 1].Y - pts[i].Y) * u); }
            acc += d;
        }
        return pts[^1];
    }
    static double Dist((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}

using LcsWidzew.Model;

namespace LcsWidzew.Sim;

// ---------------------------------------------------------------------------------------------------------------------------
// Meldunki komputera zależności dla stanowiska dyżurnego ruchu.
// Jak w rzeczywistym LCS: komputer zależności cyklicznie wysyła obraz stanu obiektów, a zobrazowanie (pulpit) rysuje
// wyłącznie z otrzymanej kopii – nie zna wnętrza komputera zależności (przebiegów, utwierdzeń, liczników czasu).
// Utrata łączności: brak meldunków – wszystkie obiekty w stanie „brak danych” (biały), polecenia nie są przyjmowane.
// ---------------------------------------------------------------------------------------------------------------------------

public class SecInd { public bool TrainOcc, Closed, NoData; public string Fault, LockType, Lock; public double? TimeRel; public List<string> Shared; }
public class MoveInd { public string Target; public double Started; }
public class SwInd { public string Pos = "+", Lock; public MoveInd Moving; public bool Stopped, Rozpr, NoCtrl, Frozen, NoData, Local; /** <summary>Zamknięta w ochronie bocznej nastawionego przebiegu (ISDR: żółta kreska na torze przy zwrotnicy).</summary> */ public bool Flank; }
public class SigInd { public string Sig; public Aspect Aspect; public bool Closed, LampFault, FailAlarm, NoData, Dark; public string Route, RouteEnd; public double SzLeft; /** <summary>Przebieg od tego sygnalizatora w trakcie nastawiania (zwrotnice się przekładają).</summary> */ public bool Setting; /** <summary>Sygnał zastępczy z W24 (NSZ).</summary> */ public bool W24; /** <summary>Zastopowany w ochronie bocznej nastawionego przebiegu (ISDR: czerwony).</summary> */ public bool Flank; }
public class DerInd { public bool On; public bool? MovingTo; public string Lock; public bool Flank; }
public class ReqInd { public int EndIdx; }
public class BlockInd
{
    public int Dir, AzkDir; public ReqInd Req; public double? ReleaseUntil, AzkUntil; public int? AwaitEnd;
    public bool[] StopEnd = new bool[2]; public bool NbrStop, TrainOn, NoData;
    /// <summary>Półsamoczynna: lampki Pwl (czerwona), Ko możliwy (biała) dla każdego końca; kTor – szczelina kontrolna SBL (czerwona).</summary>
    public bool[] Pwl = new bool[2], KoReady = new bool[2]; public bool KTor;
}
/// <summary>Przebieg na pulpicie; Edges – droga jazdy, OverlapEdges – droga ochronna wg położenia zwrotnic (Ie-104 § 8 pkt 8 ust. 2 lit. c: barwa tylko na drodze).</summary>
public class RouteInd { public string Start, End, EndKind, EndNode, Type, State; public bool TimeRel; public List<string> Edges = new(), OverlapEdges = new(); }
public class PendingInd { public string Code, Arg, Line, Desc; public List<string> Targets = new(), Notes = new(); public double NotBefore, Until; }
public class PowerInd { public bool Mains, Ups, Gen, NN; }
public class DeskInd { public string Id, Name, User; public bool Host; }

/// <summary>Obraz stanu (telegram) komputera zależności dla jednego stanowiska.</summary>
public class Indication
{
    public long Seq; public double T; public bool NoLink;
    public string Clock;
    public User User;
    public Dictionary<string, SecInd> Secs = new();
    public Dictionary<string, SwInd> Sws = new();
    public Dictionary<string, SigInd> Sigs = new();
    public Dictionary<string, DerInd> Ders = new();
    public Dictionary<string, BlockInd> Blocks = new();
    public Dictionary<string, RouteInd> Routes = new();
    /// <summary>Polecenie specjalne zainicjowane na tym stanowisku (czeka na potwierdzenie).</summary>
    public PendingInd Pending;
    public PowerInd Power = new();
    /// <summary>Wyświetlacze numerów pociągów: odcinek (tor stacyjny / odstęp) → numery pociągów.</summary>
    public Dictionary<string, List<string>> Windows = new();
    /// <summary>Obszary sterowania: posterunek → stanowisko.</summary>
    public Dictionary<string, string> Control = new();
    public List<DeskInd> Desks = new();
    /// <summary>Żądania przekazania sterowania skierowane do tego stanowiska.</summary>
    public List<ControlRequest> ControlReqs = new();
    public int BookDrafts;
    /// <summary>Posterunki z zastopowanymi wszystkimi sygnalizatorami (SSS) i wszystkimi zwrotnicami (ZSS).</summary>
    public List<string> Sss = new(), Zss = new();
    public bool SecOcc(string id) => Secs.TryGetValue(id, out var s) && (s.TrainOcc || s.Fault != null);
}

/// <summary>Źródło danych dla zobrazowania: układ torowy i meldunki.</summary>
public interface IPulpitSource
{
    Station L { get; }
    Indication Receive();
}

/// <summary>Łącze stanowisko ↔ komputer zależności.</summary>
public class KzLink
{
    readonly Func<Indication> source;
    readonly Func<bool> down;
    readonly Station layout;
    Indication noLink;
    public KzLink(Func<Indication> source, Func<bool> down, Station layout) { this.source = source; this.down = down; this.layout = layout; }
    public Indication Last { get; private set; }
    /// <summary>Odbiór aktualnego telegramu (cykliczna transmisja stanu).</summary>
    public Indication Receive()
    {
        if (down()) return noLink ??= NoData(layout, Last);
        noLink = null;
        return Last = source();
    }

    public static Indication NoData(Station L, Indication last) => new()
    {
        NoLink = true, T = last?.T ?? 0, Clock = last?.Clock, User = last?.User, Power = last?.Power ?? new(), Control = last?.Control ?? new(), Desks = last?.Desks ?? new(),
        Secs = L.Sections.Keys.ToDictionary(k => k, _ => new SecInd { NoData = true }),
        Sws = L.Switches.Keys.ToDictionary(k => k, k => new SwInd { NoData = true, Pos = last?.Sws.GetValueOrDefault(k)?.Pos ?? "+" }),
        Sigs = L.Signals.Keys.ToDictionary(k => k, _ => new SigInd { NoData = true }),
        Ders = L.Derailers.Keys.ToDictionary(k => k, _ => new DerInd { On = true }),
        Blocks = L.Blocks.Keys.ToDictionary(k => k, _ => new BlockInd { NoData = true }),
    };
}

public partial class Simulator : IPulpitSource
{
    long indSeq;
    KzLink link;
    /// <summary>Uszkodzenie łącza stanowisk (instruktor): meldunki nie docierają, polecenia nie są przyjmowane.</summary>
    public bool LinkDown;
    public KzLink Link => link ??= new KzLink(() => Publish(null), () => LinkDown, L);
    Indication IPulpitSource.Receive() => Link.Receive();
    /// <summary>Pulpit rysuje zobrazowanie (obrazy EbiScreen), komputer zależności liczy z układu torowego.</summary>
    Station IPulpitSource.L => L.View;

    /// <summary>Telegram stanu dla stanowiska – kopia stanu obiektów, bez wewnętrznych struktur komputera zależności.</summary>
    public Indication Publish(Desk d)
    {
        var I = new Indication { Seq = ++indSeq, T = T, Clock = Clock(), User = d != null ? d.User : User };
        // przebieg w trakcie nastawiania (zwrotnice się przekładają) – droga przebiegu wyświetlana dopiero po przełożeniu
        // i utwierdzeniu: do tego czasu odcinki, zwrotnice i sygnalizator końcowy jak bez przebiegu
        bool Shown(string uid) => uid != null && (!Routes.TryGetValue(uid, out var rr) || rr.State != "setting");
        foreach (var (k, s) in Secs) I.Secs[k] = new SecInd { TrainOcc = s.TrainOcc, Closed = s.Closed, NoData = s.NoData, Fault = s.Fault, LockType = Shown(s.Lock) ? s.LockType : null, Lock = Shown(s.Lock) ? s.Lock : null, TimeRel = s.TimeRel, Shared = s.Shared.Count == 0 ? null : s.Shared.Where(Shown).ToList() };
        foreach (var (k, w) in Sws) I.Sws[k] = new SwInd { Pos = w.Pos, Lock = Shown(w.Lock) ? w.Lock : null, Moving = w.Moving == null ? null : new MoveInd { Target = w.Moving.Target, Started = w.Moving.Started }, Stopped = w.Stopped, Rozpr = w.Rozpr, NoCtrl = w.NoCtrl, Frozen = w.Frozen, NoData = w.NoData, Local = w.Local, Flank = w.Flank.Any(Shown) };
        foreach (var (k, s) in Sigs) I.Sigs[k] = new SigInd { Aspect = s.Aspect, Closed = s.Closed, LampFault = s.LampFault, FailAlarm = s.FailAlarm, NoData = s.NoData, Dark = s.Dark, Sig = s.Sig, Route = Shown(s.Route) ? s.Route : null, RouteEnd = Shown(s.RouteEnd) ? s.RouteEnd : null, SzLeft = s.SzUntil - T, Setting = s.Route != null && !Shown(s.Route), W24 = s.W24, Flank = s.Flank.Any(Shown) };
        foreach (var (k, x) in Ders) I.Ders[k] = new DerInd { On = x.On, MovingTo = x.MovingTo, Lock = Shown(x.Lock) ? x.Lock : null, Flank = x.Flank.Any(Shown) };
        foreach (var (k, b) in Blocks)
            I.Blocks[k] = new BlockInd
            {
                Dir = b.Dir, AzkDir = b.AzkDir, Req = b.Req == null ? null : new ReqInd { EndIdx = b.Req.EndIdx }, ReleaseUntil = b.ReleaseUntil, AzkUntil = b.AzkUntil,
                AwaitEnd = b.AwaitEnd, StopEnd = (bool[])b.StopEnd.Clone(), NbrStop = b.NbrStop, TrainOn = Trains != null && TrainOnBlock(k),
                Pwl = (bool[])b.Pwl.Clone(), KoReady = new[] { KoReady(L.Blocks[k], 0), KoReady(L.Blocks[k], 1) }, KTor = KTor(L.Blocks[k])
            };
        foreach (var (k, r) in Routes) I.Routes[k] = new RouteInd { Start = r.Start, End = r.End, EndKind = r.EndKind, EndNode = r.EndNode, Type = r.Type, State = r.State, TimeRel = r.TimeRelUntil != null, Edges = new(r.Edges), OverlapEdges = OverlapPath(r) };
        if (Pending != null && (d == null || Pending.Desk == null || Pending.Desk == d.Id))
            I.Pending = new PendingInd { Code = Pending.Code, Arg = Pending.Arg, Line = Pending.Line, Desc = Pending.Desc, Targets = new(Pending.Targets), Notes = new(Pending.Notes), NotBefore = Pending.NotBefore, Until = Pending.Until };
        I.Power = new PowerInd { Mains = Power.Mains, Ups = Power.Ups, Gen = Power.Gen, NN = Power.NN };
        I.Windows = Nadrzedny.Pip.Okna();
        I.Sss = Kz.SssPosts.ToList(); I.Zss = Kz.ZssPosts.ToList();
        I.Control = new(Control);
        I.Desks = Desks.Values.Select(x => new DeskInd { Id = x.Id, Name = x.Name, User = x.User?.Name, Host = x.Host }).ToList();
        if (d != null) I.ControlReqs = ControlReqs.Where(r => r.To == d.Id).ToList();
        I.BookDrafts = BookDrafts.Count(x => x.Desk == null || d == null || x.Desk == d.Id);
        return I;
    }

    /// <summary>Tory drogi ochronnej: od końca przebiegu w głąb odcinków drogi ochronnej, przez zwrotnice w ich obecnym położeniu.</summary>
    List<string> OverlapPath(Route r)
    {
        var res = new List<string>();
        if (r.OvSecs.Count == 0 || r.Edges.Count == 0 || r.EndNode == null || !L.Signals.TryGetValue(r.Start, out var sg)) return res;
        string nid = r.EndNode, from = r.Edges[^1];
        for (int k = 0; k < 50; k++)
        {
            var f = Next(nid, sg.Dir, from, n => Sws.TryGetValue(n, out var w) ? w.Pos : "+");
            if (f == null || !r.OvSecs.Contains(L.Edges[f.Edge].Sec) || res.Contains(f.Edge)) break;
            res.Add(f.Edge); nid = L.Other(f.Edge, nid); from = f.Edge;
        }
        return res;
    }
}

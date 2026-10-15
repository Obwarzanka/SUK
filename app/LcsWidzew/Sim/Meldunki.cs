using LcsWidzew.Model;

namespace LcsWidzew.Sim;

// ---------------------------------------------------------------------------------------------------------------------------
// Meldunki komputera zależności dla stanowiska dyżurnego ruchu.
// Jak w rzeczywistym LCS: komputer zależności cyklicznie wysyła obraz stanu obiektów, a zobrazowanie (pulpit) rysuje
// wyłącznie z otrzymanej kopii – nie zna wnętrza komputera zależności (przebiegów, utwierdzeń, liczników czasu).
// Utrata łączności: brak meldunków – wszystkie obiekty w stanie „brak danych” (biały), polecenia nie są przyjmowane.
// ---------------------------------------------------------------------------------------------------------------------------

public class SecInd { public bool TrainOcc, Closed, NoData; public string Fault, LockType; public double? TimeRel; }
public class MoveInd { public string Target; public double Started; }
public class SwInd { public string Pos = "+", Lock; public MoveInd Moving; public bool Stopped, Rozpr, NoCtrl, Frozen, NoData, Local; }
public class SigInd { public Aspect Aspect; public bool Closed, LampFault, FailAlarm, NoData, Dark; public string Route, RouteEnd; public double SzLeft; }
public class DerInd { public bool On; public bool? MovingTo; public string Lock; }
public class ReqInd { public int EndIdx; }
public class BlockInd
{
    public int Dir, AzkDir; public ReqInd Req; public double? ReleaseUntil, AzkUntil; public int? AwaitEnd;
    public bool[] StopEnd = new bool[2]; public bool NbrStop, TrainOn, NoData;
}
public class RouteInd { public string Start, End, EndKind, EndNode, Type; }
public class PendingInd { public string Code; public List<string> Targets = new(); }

/// <summary>Obraz stanu (telegram) komputera zależności.</summary>
public class Indication
{
    public long Seq; public double T; public bool NoLink;
    public User User;
    public Dictionary<string, SecInd> Secs = new();
    public Dictionary<string, SwInd> Sws = new();
    public Dictionary<string, SigInd> Sigs = new();
    public Dictionary<string, DerInd> Ders = new();
    public Dictionary<string, BlockInd> Blocks = new();
    public Dictionary<string, RouteInd> Routes = new();
    public PendingInd Pending;
    /// <summary>Wyświetlacze numerów pociągów: odcinek (tor stacyjny / odstęp) → numery pociągów.</summary>
    public Dictionary<string, List<string>> Windows = new();
    public bool SecOcc(string id) => Secs.TryGetValue(id, out var s) && (s.TrainOcc || s.Fault != null);
}

/// <summary>Łącze stanowisko ↔ komputer zależności.</summary>
public class KzLink
{
    readonly Func<Indication> source;
    readonly Station layout;
    Indication noLink;
    public KzLink(Func<Indication> source, Station layout) { this.source = source; this.layout = layout; }
    /// <summary>Uszkodzenie łącza (instruktor): meldunki nie docierają, polecenia nie są przyjmowane.</summary>
    public bool Down;
    public Indication Last { get; private set; }
    /// <summary>Odbiór aktualnego telegramu (cykliczna transmisja stanu).</summary>
    public Indication Receive()
    {
        if (Down) return noLink ??= NoData(layout, Last);
        noLink = null;
        return Last = source();
    }

    static Indication NoData(Station L, Indication last) => new()
    {
        NoLink = true, T = last?.T ?? 0, User = last?.User,
        Secs = L.Sections.Keys.ToDictionary(k => k, _ => new SecInd { NoData = true }),
        Sws = L.Switches.Keys.ToDictionary(k => k, k => new SwInd { NoData = true, Pos = last?.Sws.GetValueOrDefault(k)?.Pos ?? "+" }),
        Sigs = L.Signals.Keys.ToDictionary(k => k, _ => new SigInd { NoData = true }),
        Ders = L.Derailers.Keys.ToDictionary(k => k, _ => new DerInd { On = true }),
        Blocks = L.Blocks.Keys.ToDictionary(k => k, _ => new BlockInd { NoData = true }),
    };
}

public partial class Simulator
{
    long indSeq;
    KzLink link;
    public KzLink Link => link ??= new KzLink(Publish, L);

    /// <summary>Telegram stanu dla stanowiska dyżurnego – kopia stanu obiektów, bez wewnętrznych struktur komputera zależności.</summary>
    public Indication Publish()
    {
        var I = new Indication { Seq = ++indSeq, T = T, User = User };
        foreach (var (k, s) in Secs) I.Secs[k] = new SecInd { TrainOcc = s.TrainOcc, Closed = s.Closed, NoData = s.NoData, Fault = s.Fault, LockType = s.LockType, TimeRel = s.TimeRel };
        foreach (var (k, w) in Sws) I.Sws[k] = new SwInd { Pos = w.Pos, Lock = w.Lock, Moving = w.Moving == null ? null : new MoveInd { Target = w.Moving.Target, Started = w.Moving.Started }, Stopped = w.Stopped, Rozpr = w.Rozpr, NoCtrl = w.NoCtrl, Frozen = w.Frozen, NoData = w.NoData, Local = w.Local };
        foreach (var (k, s) in Sigs) I.Sigs[k] = new SigInd { Aspect = s.Aspect, Closed = s.Closed, LampFault = s.LampFault, FailAlarm = s.FailAlarm, NoData = s.NoData, Dark = s.Dark, Route = s.Route, RouteEnd = s.RouteEnd, SzLeft = s.SzUntil - T };
        foreach (var (k, d) in Ders) I.Ders[k] = new DerInd { On = d.On, MovingTo = d.MovingTo, Lock = d.Lock };
        foreach (var (k, b) in Blocks)
            I.Blocks[k] = new BlockInd
            {
                Dir = b.Dir, AzkDir = b.AzkDir, Req = b.Req == null ? null : new ReqInd { EndIdx = b.Req.EndIdx }, ReleaseUntil = b.ReleaseUntil, AzkUntil = b.AzkUntil,
                AwaitEnd = b.AwaitEnd, StopEnd = (bool[])b.StopEnd.Clone(), NbrStop = b.NbrStop, TrainOn = Trains != null && Trains.OnBlock(k)
            };
        foreach (var (k, r) in Routes) I.Routes[k] = new RouteInd { Start = r.Start, End = r.End, EndKind = r.EndKind, EndNode = r.EndNode, Type = r.Type };
        if (Pending != null) I.Pending = new PendingInd { Code = Pending.Code, Targets = new List<string>(Pending.Targets) };
        if (Trains != null)
            foreach (var g in Trains.List.Where(t => t.Window != null).GroupBy(t => t.Window)) I.Windows[g.Key] = g.Select(t => t.Num).ToList();
        return I;
    }
}

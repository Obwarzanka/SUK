using LcsWidzew.Model;

namespace LcsWidzew.Sim;

/// <summary>
/// Sesja stanowiska dyżurnego: wszystko, czego okno pulpitu potrzebuje od komputera zależności – meldunki, polecenia,
/// rejestrator zdarzeń, alarmy, książka E-1758, plan ruchu, obszary sterowania. Lokalnie (gra jednoosobowa / serwer)
/// albo przez sieć (stanowisko przyłączone do serwera).
/// </summary>
public interface ILcsSession : IPulpitSource
{
    /// <summary>Komputer zależności działa w tym programie (czas, instruktor, edytor układu).</summary>
    bool IsHost { get; }
    /// <summary>Komputer zależności – tylko na serwerze / w grze jednoosobowej.</summary>
    Simulator Sim { get; }
    string DeskId { get; }
    string DeskName { get; }
    Dictionary<string, List<RouteDef>> RouteTable { get; }
    Indication Last { get; }
    List<EventRecord> Events { get; }
    List<EventRecord> Alarms { get; }

    event Action<EventRecord> EventAdded, AlarmAdded;
    event Action AlarmsChanged, Beep, SpecialExecuted, PendingExpired;
    event Action<string> Disconnected;
    /// <summary>Nowy układ / nowy scenariusz na serwerze.</summary>
    event Action Reset;

    CmdResult Exec(string line);
    CmdResult Confirm(string typed);
    void Cancel();
    void AckAlarm(long id);
    void Login(User u);
    void SetPage(string page);
    string RequestControl(string post);
    void ReleaseControl(string post);
    void AnswerControl(string post, bool yes);
    void Phone(string toDesk, string text);
    List<PlanRow> GetPlan();
    ScoreData GetScore();
    BookData GetBook();
    void AddBook(string text, string auto);
    /// <summary>EDR – dzienniki ruchu stanowiska (PLAN_EDR.md).</summary>
    EdrData GetEdr();
    CmdResult EdrCmd(EdrPolecenie p);
}

/// <summary>Stanowisko w tym samym programie co komputer zależności.</summary>
public class LocalSession : ILcsSession
{
    public Simulator Sim { get; }
    public Desk Desk { get; }
    readonly KzLink link;
    public LocalSession(Simulator sim, Desk desk)
    {
        Sim = sim; Desk = desk;
        link = new KzLink(() => sim.Publish(desk), () => sim.LinkDown, sim.L);
        sim.EventAdded += e => EventAdded?.Invoke(e);
        sim.AlarmAdded += e => AlarmAdded?.Invoke(e);
        sim.Beep += () => Beep?.Invoke();
        sim.SpecialExecuted += _ => { if (sim.CurDesk == null || sim.CurDesk == desk) SpecialExecuted?.Invoke(); };
        sim.PendingExpired += () => PendingExpired?.Invoke();
    }
    public bool IsHost => true;
    public string DeskId => Desk.Id;
    public string DeskName => Desk.Name;
    public Station L => Sim.L.View;
    public Dictionary<string, List<RouteDef>> RouteTable => Sim.RouteTable;
    public Indication Last => link.Last;
    public Indication Receive() => link.Receive();
    public List<EventRecord> Events => Sim.Events;
    public List<EventRecord> Alarms => Sim.Alarms;

    public event Action<EventRecord> EventAdded, AlarmAdded;
    public event Action AlarmsChanged, Beep, SpecialExecuted, PendingExpired;
    public event Action<string> Disconnected { add { } remove { } }
    public event Action Reset { add { } remove { } }

    public CmdResult Exec(string line) => Sim.ExecAs(Desk, line);
    public CmdResult Confirm(string typed) => Sim.ConfirmAs(Desk, typed);
    public void Cancel() => Sim.CancelAs(Desk);
    public void AckAlarm(long id) { var a = Sim.Alarms.FirstOrDefault(x => x.Id == id); if (a != null) { a.Ack = true; AlarmsChanged?.Invoke(); } }
    public void Login(User u) => Sim.Login(Desk, u);
    public void SetPage(string page) { Desk.Page = page; if (Desk.Host) Sim.Page = page; }
    public string RequestControl(string post) => Sim.RequestControl(Desk, post);
    public void ReleaseControl(string post) => Sim.ReleaseControl(Desk, post);
    public void AnswerControl(string post, bool yes) => Sim.AnswerControl(Desk, post, yes);
    public void Phone(string toDesk, string text) => Sim.Phone(Desk, toDesk, text);
    public List<PlanRow> GetPlan() => Sim.PlanRows();
    public ScoreData GetScore() => Sim.Score();
    public BookData GetBook() => Sim.BookFor(Desk);
    public void AddBook(string text, string auto) => Sim.AddBook(Desk, text, auto);
    public EdrData GetEdr() => Sim.Edr.DlaStanowiska(Desk);
    public CmdResult EdrCmd(EdrPolecenie p) => Sim.Edr.Wykonaj(Desk, p);
}

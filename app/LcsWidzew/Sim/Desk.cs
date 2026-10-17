using LcsWidzew.Model;

namespace LcsWidzew.Sim;

/// <summary>Stanowisko dyżurnego ruchu (pulpit) przyłączone do komputera zależności – lokalne albo przez sieć.</summary>
public class Desk
{
    public string Id, Name;
    public User User;
    /// <summary>Wyświetlany obraz – kontekst adresów poleceń (Ie-104 § 11 ust. 6).</summary>
    public string Page;
    /// <summary>Stanowisko główne (serwer): przejmuje obszary sterowania stanowisk, które się rozłączyły.</summary>
    public bool Host;
}

/// <summary>Żądanie przekazania obszaru sterowania (posterunku) między stanowiskami.</summary>
public class ControlRequest { public string Post, From, To; public double At; }

public class PlanRow { public string Nr { get; set; } public string Kat { get; set; } public string Z { get; set; } public string Do { get; set; } public string Wypr { get; set; } public string Odj { get; set; } public string Tor { get; set; } public string Stan { get; set; } }
public class ScoreData { public Stats Stats; public int Book, Drafts; public List<string> Remarks = new(); }
public class BookData { public List<BookEntry> Entries = new(); public string Draft; public int Drafts; }

/// <summary>
/// Fasada: polecenia, stanowiska, obszary sterowania i książka E-1758 należą do systemu nadrzędnego
/// (<see cref="SystemNadrzedny"/>, Sim/Pulpit/); tu tylko przekierowania dla okien, sieci i testów.
/// </summary>
public partial class Simulator
{
    /// <summary>System nadrzędny stanowisk (parser poleceń, dwuetapowość, stanowiska, PIP).</summary>
    public readonly SystemNadrzedny Nadrzedny;

    public string Page { get => Nadrzedny.Page; set => Nadrzedny.Page = value; }
    public User User { get => Nadrzedny.User; set => Nadrzedny.User = value; }
    public Pending Pending { get => Nadrzedny.Pending; set => Nadrzedny.Pending = value; }
    public List<BookEntry> Book => Nadrzedny.Book;
    public List<Pending> BookDrafts => Nadrzedny.BookDrafts;
    public Dictionary<string, Desk> Desks => Nadrzedny.Desks;
    public Dictionary<string, string> Control => Nadrzedny.Control;
    public List<ControlRequest> ControlReqs => Nadrzedny.ControlReqs;
    public Desk CurDesk => Nadrzedny.CurDesk;
    public List<string> LinkTrace => Nadrzedny.LinkTrace;
    public static Dictionary<string, SystemNadrzedny.CodeInfo> Codes => SystemNadrzedny.Codes;
    public static string Fold(string s) => SystemNadrzedny.Fold(s);

    public CmdResult Exec(string line, bool confirm = false) => Nadrzedny.Exec(line, confirm);
    public CmdResult ConfirmPending(string typed) => Nadrzedny.ConfirmPending(typed);
    public void CancelPending() => Nadrzedny.CancelPending();
    public string Resolve(string tok, params string[] kinds) => Nadrzedny.Resolve(tok, kinds);
    public Desk AddDesk(string id, string name, bool host) => Nadrzedny.AddDesk(id, name, host);
    public void RemoveDesk(string id) => Nadrzedny.RemoveDesk(id);
    public string PostName(string p) => Nadrzedny.PostName(p);
    public CmdResult ExecAs(Desk d, string line, bool confirm = false) => Nadrzedny.ExecAs(d, line, confirm);
    public CmdResult ConfirmAs(Desk d, string typed) => Nadrzedny.ConfirmAs(d, typed);
    public void CancelAs(Desk d) => Nadrzedny.CancelAs(d);
    public void Trace(string s) => Nadrzedny.Trace(s);
    public long IndCount => indSeq;
    public string RequestControl(Desk d, string post) => Nadrzedny.RequestControl(d, post);
    public void AnswerControl(Desk d, string post, bool yes) => Nadrzedny.AnswerControl(d, post, yes);
    public void ReleaseControl(Desk d, string post) => Nadrzedny.ReleaseControl(d, post);
    public void Phone(Desk from, string toDesk, string text) => Nadrzedny.Phone(from, toDesk, text);
    public BookData BookFor(Desk d) => Nadrzedny.BookFor(d);
    public void AddBook(Desk d, string text, string auto) => Nadrzedny.AddBook(d, text, auto);
    public void Login(Desk d, User u) => Nadrzedny.Login(d, u);

    /// <summary>Zdarzenia stanowisk zgłaszane przez system nadrzędny.</summary>
    internal void ZglosWykonanieSpecjalnego(string line) => SpecialExecuted?.Invoke(line);
    internal void ZglosWygasniecie() => PendingExpired?.Invoke();

    // ------------------------------------------------------------------ dane dla okien stanowiska
    public List<PlanRow> PlanRows()
    {
        string Nb(string sz) => sz == null ? "–" : L.Blocks.TryGetValue(sz, out var b) ? $"{sz} ({(b.Internal ? "tunel / LCS" : b.Nbr)})" : sz;
        if (Trains == null) return new();
        return Rozklad.Queue.OrderBy(q => q.T).Select(q =>
        {
            var t = Trains.List.FirstOrDefault(x => x.Num == q.Num || x.Num == q.Num + "M" || (q.Term?.NewNum != null && x.Num == q.Term.NewNum));
            string st = !q.Done ? (q.T <= T ? "oczekuje na szlak" : "planowy") : t == null ? "obsłużony" : t.State == "dwell" ? "postój" : t.ReadySince != null && t.V < 0.1 ? "gotowy do odjazdu" + (t.WaitingAt != null ? $" ({t.WaitingAt})" : "") : t.WaitingAt != null ? $"stoi przed {t.WaitingAt}" : "w ruchu";
            var dest = q.Term?.Reverse == true ? $"kończy bieg → {q.Term.NewNum} do {Nb(q.Term.To)}" : q.Term?.Odstaw == true ? "kończy bieg, odstawienie" : Nb(q.To);
            return new PlanRow { Nr = q.Num, Kat = q.Cat, Z = q.StartSec != null ? (q.Loco ? "manewry: " : "tor ") + Station.Lbl(q.StartSec) + " (" + Station.PostOf(q.StartSec) + ")" : Nb(q.From), Do = q.Loco ? "–" : dest, Wypr = Clock(q.T), Odj = q.Dep is double dd ? Clock(dd) : (q.Stop ? "" : "bez post."), Tor = q.Track, Stan = st };
        }).ToList();
    }

    public ScoreData Score() => new() { Stats = Stats, Book = Book.Count, Drafts = BookDrafts.Count, Remarks = Remarks.Select(r => $"{r.Time} – {r.Text}").ToList() };

    public static bool Passes(Station L, RouteDef r, string v)
    {
        if (L.Switches.ContainsKey(v)) return r.Sw.Any(q => q.Num == v);
        if (v.EndsWith("+") || v.EndsWith("-")) { var n = v[..^1]; return r.Sw.Any(q => q.Num == n && q.Pos == v[^1..]); }
        if (L.Sections.ContainsKey(v)) return r.Secs.Contains(v);
        if (L.Signals.TryGetValue(v, out var s)) return r.Edges.Any(e => L.Edges[e].A == s.Node || L.Edges[e].B == s.Node);
        return false;
    }
}

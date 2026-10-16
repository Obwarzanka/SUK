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

public partial class Simulator
{
    public Dictionary<string, Desk> Desks = new();
    /// <summary>Obszary sterowania: posterunek → stanowisko, które nim steruje (null – nieobsadzony).</summary>
    public Dictionary<string, string> Control = new();
    public List<ControlRequest> ControlReqs = new();
    /// <summary>Stanowisko, od którego pochodzi wykonywane polecenie (null – wewnętrzne / test).</summary>
    public Desk CurDesk;

    public Desk AddDesk(string id, string name, bool host)
    {
        var d = new Desk { Id = id, Name = name, Host = host, Page = Page };
        Desks[id] = d;
        foreach (var p in L.Posts.Keys) if (!Control.TryGetValue(p, out var o) || o == null || (host && !Desks.ContainsKey(o))) Control[p] = host ? id : Control.GetValueOrDefault(p);
        Log("Zdarzenie", $"Przyłączono stanowisko {name}", "Stanowisko", "");
        return d;
    }

    public void RemoveDesk(string id)
    {
        if (!Desks.Remove(id, out var d)) return;
        var host = Desks.Values.FirstOrDefault(x => x.Host);
        foreach (var p in Control.Where(x => x.Value == id).Select(x => x.Key).ToList())
        {
            Control[p] = host?.Id;
            Log("Zdarzenie", $"Stanowisko {d.Name} rozłączone – sterowanie posterunkiem {PostName(p)} przechodzi na {host?.Name ?? "brak obsługi"}", "Stanowisko", "");
        }
        ControlReqs.RemoveAll(r => r.From == id || r.To == id);
        if (Pending?.Desk == id) Pending = null;
    }

    public string PostName(string p) => L.Posts.GetValueOrDefault(p ?? "") ?? p;
    string DeskName(string id) => id != null && Desks.TryGetValue(id, out var d) ? d.Name : "brak obsługi";

    bool Controls(string post) => CurDesk == null || string.IsNullOrEmpty(post) || !L.Posts.ContainsKey(post) || Control.GetValueOrDefault(post) == CurDesk.Id;

    /// <summary>Czy stanowisko może wydać polecenie dla wskazanych obiektów (obszar sterowania).</summary>
    string CheckControl(string code, List<string> args)
    {
        if (CurDesk == null || code is "KW" or "NN" or "ONN" or "AGR") return null;
        foreach (var a in args)
        {
            var k = Resolve(a); if (k == null) continue;
            if (L.Blocks.TryGetValue(k, out var b))
            {
                if (b.Ends.Any(e => Controls(e.Post))) continue;
                return $"Szlak {k} nie należy do obszaru sterowania tego stanowiska ({string.Join(", ", b.Ends.Select(e => PostName(e.Post) + ": " + DeskName(Control.GetValueOrDefault(e.Post ?? ""))))})";
            }
            var post = Station.PostOf(k);
            if (!Controls(post)) return $"Obiekt {Station.Lbl(k)} należy do obszaru sterowania posterunku {PostName(post)} – steruje nim {DeskName(Control.GetValueOrDefault(post))}";
        }
        return null;
    }

    /// <summary>Polecenie z określonego stanowiska: kontekst użytkownika, obrazu i obszaru sterowania.</summary>
    public CmdResult ExecAs(Desk d, string line, bool confirm = false)
    {
        Trace($"→ {d.Name}: polecenie „{line}”");
        var r = As(d, () => Exec(line, confirm));
        Trace($"← {d.Name}: {(r.Ok ? "przyjęte" : "odrzucone")}{(r.Msg != "" ? " – " + r.Msg : "")}");
        return r;
    }

    /// <summary>Zapis wymiany ze stanowiskami (podgląd komputera zależności).</summary>
    public readonly List<string> LinkTrace = new();
    public void Trace(string s) { LinkTrace.Add($"{Clock()}  {s}"); if (LinkTrace.Count > 400) LinkTrace.RemoveRange(0, 100); }
    public long IndCount => indSeq;
    public CmdResult ConfirmAs(Desk d, string typed) => As(d, () => Pending != null && Pending.Desk != null && Pending.Desk != d.Id ? new CmdResult(false, "Polecenie specjalne zainicjowało inne stanowisko") : ConfirmPending(typed));
    public void CancelAs(Desk d) => As(d, () => { if (Pending != null && (Pending.Desk == null || Pending.Desk == d.Id)) CancelPending(); return new CmdResult(true, ""); });

    T As<T>(Desk d, Func<T> f)
    {
        var (u, p, c) = (User, Page, CurDesk);
        User = d.User; Page = d.Page ?? Page; CurDesk = d;
        try { return f(); }
        finally { User = u; Page = p; CurDesk = c; }
    }

    // ------------------------------------------------------------------ przekazanie obszaru sterowania
    public string RequestControl(Desk d, string post)
    {
        if (!L.Posts.ContainsKey(post)) return "Nieznany posterunek";
        var owner = Control.GetValueOrDefault(post);
        if (owner == d.Id) return "Stanowisko już steruje tym posterunkiem";
        if (owner == null || !Desks.ContainsKey(owner))
        {
            Control[post] = d.Id;
            As(d, () => Log("Zdarzenie", $"Przejęto sterowanie posterunkiem {PostName(post)} (nieobsadzony) – stanowisko {d.Name}", "Stanowisko", ""));
            return null;
        }
        if (ControlReqs.Any(r => r.Post == post && r.From == d.Id)) return "Żądanie już wysłane – czeka na zgodę";
        ControlReqs.Add(new ControlRequest { Post = post, From = d.Id, To = owner, At = T });
        As(d, () => Log("Zdarzenie", $"Żądanie przekazania sterowania posterunkiem {PostName(post)}: {d.Name} → od {DeskName(owner)}", "Stanowisko", ""));
        return null;
    }

    public void AnswerControl(Desk d, string post, bool yes)
    {
        var r = ControlReqs.FirstOrDefault(x => x.Post == post && x.To == d.Id); if (r == null) return;
        ControlReqs.Remove(r);
        if (yes && Control.GetValueOrDefault(post) == d.Id)
        {
            Control[post] = r.From;
            As(d, () => Log("Zdarzenie", $"Przekazano sterowanie posterunkiem {PostName(post)}: {d.Name} → {DeskName(r.From)}", "Stanowisko", ""));
        }
        else As(d, () => Log("Zdarzenie", $"Odmowa przekazania sterowania posterunkiem {PostName(post)} dla {DeskName(r.From)}", "Stanowisko", ""));
    }

    public void ReleaseControl(Desk d, string post)
    {
        if (Control.GetValueOrDefault(post) != d.Id) return;
        var host = Desks.Values.FirstOrDefault(x => x.Host && x.Id != d.Id);
        Control[post] = host?.Id;
        As(d, () => Log("Zdarzenie", $"Oddano sterowanie posterunkiem {PostName(post)} – przejmuje {host?.Name ?? "brak obsługi"}", "Stanowisko", ""));
    }

    // ------------------------------------------------------------------ łączność między posterunkami
    public void Phone(Desk from, string toDesk, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var to = toDesk == null || toDesk == "*" ? "wszystkie stanowiska" : DeskName(toDesk);
        As(from, () => Log("Telefon", $"{from.Name} → {to}: {text.Trim()}", "Łączność", ""));
    }

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

    public BookData BookFor(Desk d)
    {
        var dr = BookDrafts.FirstOrDefault(x => x.Desk == null || x.Desk == d?.Id);
        return new BookData
        {
            Entries = Book.AsEnumerable().Reverse().ToList(), Drafts = BookDrafts.Count(x => x.Desk == null || x.Desk == d?.Id),
            Draft = dr == null ? null : $"{Clock(dr.Until)} – użyto polecenia specjalnego: {dr.Desc} ({dr.Line}).\r\nOkoliczności / przyczyna: \r\nWprowadzone obostrzenia: "
        };
    }

    public void AddBook(Desk d, string text, string auto)
    {
        As(d, () =>
        {
            var dr = BookDrafts.FirstOrDefault(x => x.Desk == null || x.Desk == d?.Id); if (dr != null) BookDrafts.Remove(dr);
            Book.Add(new BookEntry { Czas = Clock(), Uzytkownik = User?.Name ?? "-", Tresc = text.Trim(), Automatyk = auto.Trim(), ForCode = dr?.Code });
            Log("E-1758", text.Trim().Split('\n')[0], "Książka", "E-1758");
            if (dr != null && auto.Trim() == "") Remark($"Zapis po poleceniu {dr.Line} bez informacji o powiadomieniu automatyka (§14 ust. 4)");
            return 0;
        });
    }

    public void Login(Desk d, User u)
    {
        d.User = u;
        As(d, () => Log("Zdarzenie", u != null ? $"Zalogowano: {u.Name} (stanowisko {d.Name})" : $"Wylogowano (stanowisko {d.Name})", "Użytkownik", u?.Name ?? ""));
        if (d.Host) User = u;
    }

    public static bool Passes(Station L, RouteDef r, string v)
    {
        if (L.Switches.ContainsKey(v)) return r.Sw.Any(q => q.Num == v);
        if (v.EndsWith("+") || v.EndsWith("-")) { var n = v[..^1]; return r.Sw.Any(q => q.Num == n && q.Pos == v[^1..]); }
        if (L.Sections.ContainsKey(v)) return r.Secs.Contains(v);
        if (L.Signals.TryGetValue(v, out var s)) return r.Edges.Any(e => L.Edges[e].A == s.Node || L.Edges[e].B == s.Node);
        return false;
    }
}

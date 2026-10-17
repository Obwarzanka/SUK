using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Threading;
using LcsWidzew.Model;
using LcsWidzew.Sim;

namespace LcsWidzew.Net;

// ---------------------------------------------------------------------------------------------------------------------------
// Sieć stanowisk LCS: serwer (komputer zależności + teren) i stanowiska dyżurnych łączące się po TCP (np. przez Radmin VPN).
// Protokół: jedna wiadomość JSON w linii, pole "t" – rodzaj. Stanowisko → serwer: hello, cmd, confirm, cancel, ack, login,
// page, reqctl, relctl, ansctl, phone, get (plan / score / book), book. Serwer → stanowisko: welcome / reset (układ, tablica
// przebiegów, rejestrator), ind (meldunki), ev, al, alupd, res, special, pendexp, beep.
// Program serwisowy Komputerownia: serwis (zamiast hello – bez stanowiska), karty (→ res: KomputerowniaStan), usterka (key, target, clear).
// ---------------------------------------------------------------------------------------------------------------------------

public static class Wire
{
    public const int DefaultPort = 47120;
    public static readonly JsonSerializerOptions Opt = new() { IncludeFields = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    public static JsonNode Node(object o) => JsonSerializer.SerializeToNode(o, Opt);
    public static T Get<T>(JsonNode n) => n == null ? default : n.Deserialize<T>(Opt);
    public static string Msg(string t, params (string K, object V)[] f)
    {
        var o = new JsonObject { ["t"] = t };
        foreach (var (k, v) in f) o[k] = v is JsonNode jn ? jn : v == null ? null : Node(v);
        return o.ToJsonString(Opt);
    }
}

/// <summary>Serwer stanowisk: przyjmuje połączenia, przekazuje polecenia do komputera zależności, rozsyła meldunki.</summary>
public class LcsServer : IDisposable
{
    class Client
    {
        public TcpClient Tcp; public StreamWriter W; public Desk Desk; public readonly object Lk = new();
        public Dictionary<long, (bool Ack, bool Active)> AlSent = new(); public bool Alive = true;
        public long Bytes;
        /// <summary>Program serwisowy (Komputerownia): bez stanowiska, tylko podgląd kart i usterki.</summary>
        public bool Serwis;
        public void Send(string line) { if (!Alive) return; try { lock (Lk) { W.WriteLine(line); W.Flush(); } Bytes += line.Length + 1; } catch { Alive = false; } }
    }

    readonly Dispatcher ui;
    readonly Func<string> stationJson;
    Simulator sim;
    TcpListener listener;
    readonly List<Client> clients = new();
    int deskSeq;
    public int Port { get; private set; }
    /// <summary>Bajty wysłane do stanowisk (podgląd łącza).</summary>
    public long BytesOut;
    public int Count { get { lock (clients) return clients.Count(c => c.Alive && c.Desk != null); } }

    public LcsServer(Dispatcher ui, Simulator sim, Func<string> stationJson) { this.ui = ui; this.stationJson = stationJson; Attach(sim); }

    public void Start(int port, bool loopbackOnly = false)
    {
        Port = port;
        listener = new TcpListener(loopbackOnly ? IPAddress.Loopback : IPAddress.Any, port);
        listener.Start();
        new Thread(AcceptLoop) { IsBackground = true, Name = "LCS accept" }.Start();
    }

    /// <summary>Nowy komputer zależności (restart scenariusza, nowy układ) – stanowiska przyłączają się ponownie.</summary>
    public void Attach(Simulator s)
    {
        sim = s;
        sim.EventAdded += e => Broadcast(Wire.Msg("ev", ("d", e)));
        sim.AlarmAdded += e => Broadcast(Wire.Msg("al", ("d", e)));
        sim.Beep += () => Broadcast(Wire.Msg("beep"));
        sim.PendingExpired += () => Broadcast(Wire.Msg("pendexp"));
        sim.SpecialExecuted += _ => { var c = ClientOf(sim.CurDesk); c?.Send(Wire.Msg("special")); };
        List<Client> list; lock (clients) list = clients.Where(c => c.Alive && c.Desk != null).ToList();
        foreach (var c in list)
        {
            var old = c.Desk;
            c.Desk = sim.AddDesk(old.Id, old.Name, false); c.Desk.User = old.User; c.Desk.Page = old.Page;
            c.AlSent.Clear();
            c.Send(Welcome("reset", c.Desk));
        }
    }

    Client ClientOf(Desk d) { if (d == null) return null; lock (clients) return clients.FirstOrDefault(c => c.Desk == d); }

    void AcceptLoop()
    {
        while (true)
        {
            TcpClient tcp;
            try { tcp = listener.AcceptTcpClient(); } catch { return; }
            tcp.NoDelay = true;
            var c = new Client { Tcp = tcp, W = new StreamWriter(tcp.GetStream(), new UTF8Encoding(false)) { NewLine = "\n" } };
            lock (clients) clients.Add(c);
            new Thread(() => ReadLoop(c)) { IsBackground = true, Name = "LCS client" }.Start();
        }
    }

    void ReadLoop(Client c)
    {
        try
        {
            using var r = new StreamReader(c.Tcp.GetStream(), Encoding.UTF8);
            string line;
            while ((line = r.ReadLine()) != null)
            {
                JsonNode m; try { m = JsonNode.Parse(line); } catch { continue; }
                ui.Invoke(() => { try { Handle(c, m); } catch (Exception ex) { App.LogError(ex, "serwer"); } });
            }
        }
        catch { }
        c.Alive = false;
        ui.BeginInvoke(() => { if (c.Desk != null) sim.RemoveDesk(c.Desk.Id); lock (clients) clients.Remove(c); });
        try { c.Tcp.Close(); } catch { }
    }

    string Welcome(string t, Desk d) => Wire.Msg(t, ("desk", d.Id), ("name", d.Name), ("station", stationJson()), ("routes", sim.RouteTable),
        ("events", sim.Events.Skip(Math.Max(0, sim.Events.Count - 500)).ToList()), ("alarms", sim.Alarms));

    void Handle(Client c, JsonNode m)
    {
        var t = (string)m["t"]; var id = m["id"]?.GetValue<long>() ?? 0;
        void Res(object v) => c.Send(Wire.Msg("res", ("id", id), ("d", v)));
        if (t == "hello")
        {
            var name = ((string)m["name"] ?? "").Trim(); if (name == "") name = "Stanowisko " + (deskSeq + 2);
            c.Desk = sim.AddDesk("S" + (++deskSeq), name, false);
            c.Send(Welcome("welcome", c.Desk));
            return;
        }
        if (t == "serwis")
        {
            c.Serwis = true;
            c.Send(Wire.Msg("serwis", ("stacja", sim.L.Name), ("wersja", 1)));
            return;
        }
        if (c.Serwis)
        {
            switch (t)
            {
                case "karty": Res(sim.Urzadzenia.Stan()); break;
                case "usterka":
                {
                    var key = (string)m["key"]; var target = (string)m["target"]; var clear = m["clear"]?.GetValue<bool>() ?? false;
                    if (!Instruktor.Faults.Any(f => f.Key == key)) { Res("nieznana usterka"); break; }
                    sim.Instruktor.ApplyFault(key, target, clear); Res("OK"); break;
                }
            }
            return;
        }
        var d = c.Desk; if (d == null) return;
        switch (t)
        {
            case "cmd": Res(sim.ExecAs(d, (string)m["line"])); break;
            case "confirm": Res(sim.ConfirmAs(d, (string)m["text"])); break;
            case "cancel": sim.CancelAs(d); break;
            case "ack": { var a = sim.Alarms.FirstOrDefault(x => x.Id == m["ev"].GetValue<long>()); if (a != null) a.Ack = true; break; }
            case "login": sim.Login(d, Wire.Get<User>(m["user"])); break;
            case "page": d.Page = (string)m["page"]; break;
            case "reqctl": Res(sim.RequestControl(d, (string)m["post"])); break;
            case "relctl": sim.ReleaseControl(d, (string)m["post"]); break;
            case "ansctl": sim.AnswerControl(d, (string)m["post"], m["yes"].GetValue<bool>()); break;
            case "phone": sim.Phone(d, (string)m["to"], (string)m["text"]); break;
            case "book": sim.AddBook(d, (string)m["text"], (string)m["auto"]); break;
            case "get":
                switch ((string)m["what"])
                {
                    case "plan": Res(sim.PlanRows()); break;
                    case "score": Res(sim.Score()); break;
                    case "book": Res(sim.BookFor(d)); break;
                }
                break;
        }
    }

    void Broadcast(string line) { List<Client> l; lock (clients) l = clients.Where(c => c.Alive && c.Desk != null).ToList(); foreach (var c in l) c.Send(line); }

    /// <summary>Cykliczna transmisja stanu do stanowisk (wywoływana z pętli czasu serwera).</summary>
    public void Push()
    {
        List<Client> l; lock (clients) l = clients.Where(c => c.Alive && c.Desk != null).ToList();
        foreach (var c in l)
        {
            var ind = sim.LinkDown ? KzLink.NoData(sim.L, null) : sim.Publish(c.Desk);
            var msg = Wire.Msg("ind", ("d", ind)); c.Send(msg); BytesOut += msg.Length + 1; LastSize = msg.Length;
            var upd = new JsonArray();
            foreach (var a in sim.Alarms)
                if (!c.AlSent.TryGetValue(a.Id, out var st) || st.Ack != a.Ack || st.Active != a.Active) { c.AlSent[a.Id] = (a.Ack, a.Active); upd.Add(new JsonArray(a.Id, a.Ack, a.Active)); }
            if (upd.Count > 0) c.Send(Wire.Msg("alupd", ("d", upd)));
        }
    }

    public int LastSize;
    public List<(string Name, long Bytes)> Stations() { lock (clients) return clients.Where(c => c.Alive && c.Desk != null).Select(c => (c.Desk.Name, c.Bytes)).ToList(); }

    public void Dispose()
    {
        try { listener?.Stop(); } catch { }
        lock (clients) foreach (var c in clients) { c.Alive = false; try { c.Tcp.Close(); } catch { } }
    }
}

/// <summary>Stanowisko przyłączone do serwera przez sieć.</summary>
public class RemoteSession : ILcsSession, IDisposable
{
    readonly Dispatcher ui;
    TcpClient tcp; StreamWriter w; readonly object lk = new();
    long reqSeq;
    readonly Dictionary<long, TaskCompletionSource<JsonNode>> waits = new();
    readonly TaskCompletionSource<bool> welcomed = new();
    bool closed;

    public Simulator Sim => null;
    public bool IsHost => false;
    public string DeskId { get; private set; }
    public string DeskName { get; private set; }
    public Station L { get; private set; }
    public Dictionary<string, List<RouteDef>> RouteTable { get; private set; } = new();
    public Indication Last { get; private set; }
    public List<EventRecord> Events { get; } = new();
    public List<EventRecord> Alarms { get; } = new();
    public string Server { get; private set; }

    public event Action<EventRecord> EventAdded, AlarmAdded;
    public event Action AlarmsChanged, Beep, SpecialExecuted, PendingExpired;
    public event Action<string> Disconnected;
    /// <summary>Serwer wymienił układ lub uruchomił scenariusz od nowa.</summary>
    public event Action Reset;

    RemoteSession(Dispatcher ui) { this.ui = ui; }

    public static RemoteSession Connect(string host, int port, string name, Dispatcher ui)
    {
        var s = new RemoteSession(ui) { Server = $"{host}:{port}" };
        s.tcp = new TcpClient { NoDelay = true };
        if (!s.tcp.ConnectAsync(host, port).Wait(TimeSpan.FromSeconds(8))) throw new IOException($"Brak odpowiedzi serwera {host}:{port}");
        s.w = new StreamWriter(s.tcp.GetStream(), new UTF8Encoding(false)) { NewLine = "\n" };
        new Thread(s.ReadLoop) { IsBackground = true, Name = "LCS remote" }.Start();
        s.Send(Wire.Msg("hello", ("name", name)));
        if (!s.welcomed.Task.Wait(TimeSpan.FromSeconds(15))) { s.Dispose(); throw new IOException("Serwer nie przesłał układu stacji"); }
        return s;
    }

    void Send(string line) { if (closed) return; try { lock (lk) { w.WriteLine(line); w.Flush(); } } catch { Lost("Błąd wysyłania"); } }

    JsonNode Call(string t, params (string K, object V)[] f)
    {
        if (closed) return null;
        var id = Interlocked.Increment(ref reqSeq); var tcs = new TaskCompletionSource<JsonNode>();
        lock (waits) waits[id] = tcs;
        Send(Wire.Msg(t, f.Append(("id", (object)id)).ToArray()));
        return tcs.Task.Wait(TimeSpan.FromSeconds(6)) ? tcs.Task.Result : null;
    }

    void ReadLoop()
    {
        try
        {
            using var r = new StreamReader(tcp.GetStream(), Encoding.UTF8);
            string line;
            while ((line = r.ReadLine()) != null)
            {
                JsonNode m; try { m = JsonNode.Parse(line); } catch { continue; }
                try { Handle(m); } catch (Exception ex) { App.LogError(ex, "stanowisko"); }
            }
        }
        catch { }
        Lost("Połączenie z serwerem zostało przerwane");
    }

    void Handle(JsonNode m)
    {
        switch ((string)m["t"])
        {
            case "welcome":
            case "reset":
            {
                var st = Station.Parse((string)m["station"]);
                var routes = Wire.Get<Dictionary<string, List<RouteDef>>>(m["routes"]);
                var evs = Wire.Get<List<EventRecord>>(m["events"]); var als = Wire.Get<List<EventRecord>>(m["alarms"]);
                void Apply()
                {
                    L = st.View; RouteTable = routes; DeskId = (string)m["desk"]; DeskName = (string)m["name"]; Last = null;
                    Events.Clear(); Events.AddRange(evs); Alarms.Clear(); Alarms.AddRange(als);
                }
                if ((string)m["t"] == "welcome") { Apply(); welcomed.TrySetResult(true); }
                else ui.BeginInvoke(() => { Apply(); Reset?.Invoke(); });
                break;
            }
            case "ind": Last = Wire.Get<Indication>(m["d"]); break;
            case "ev": { var e = Wire.Get<EventRecord>(m["d"]); ui.BeginInvoke(() => { Events.Add(e); EventAdded?.Invoke(e); }); break; }
            case "al": { var e = Wire.Get<EventRecord>(m["d"]); ui.BeginInvoke(() => { Alarms.Add(e); AlarmAdded?.Invoke(e); }); break; }
            case "alupd":
            {
                var arr = m["d"].AsArray().Select(x => (Id: x[0].GetValue<long>(), Ack: x[1].GetValue<bool>(), Active: x[2].GetValue<bool>())).ToList();
                ui.BeginInvoke(() =>
                {
                    foreach (var (id, ack, act) in arr) { var a = Alarms.FirstOrDefault(x => x.Id == id); if (a != null) { a.Ack = ack; a.Active = act; } }
                    AlarmsChanged?.Invoke();
                });
                break;
            }
            case "beep": ui.BeginInvoke(() => Beep?.Invoke()); break;
            case "special": ui.BeginInvoke(() => SpecialExecuted?.Invoke()); break;
            case "pendexp": ui.BeginInvoke(() => PendingExpired?.Invoke()); break;
            case "res":
            {
                TaskCompletionSource<JsonNode> tcs;
                lock (waits) { waits.Remove(m["id"].GetValue<long>(), out tcs); }
                tcs?.TrySetResult(m["d"]);
                break;
            }
        }
    }

    void Lost(string why)
    {
        if (closed) return;
        closed = true;
        welcomed.TrySetResult(false);
        lock (waits) { foreach (var x in waits.Values) x.TrySetResult(null); waits.Clear(); }
        ui.BeginInvoke(() => Disconnected?.Invoke(why));
    }

    public Indication Receive() => closed || Last == null ? (L == null ? null : KzLink.NoData(L, Last)) : Last;

    static CmdResult Res(JsonNode n) => n == null ? new CmdResult(false, "Brak odpowiedzi serwera") : Wire.Get<CmdResult>(n);
    public CmdResult Exec(string line) => Res(Call("cmd", ("line", line)));
    public CmdResult Confirm(string typed) => Res(Call("confirm", ("text", typed)));
    public void Cancel() => Send(Wire.Msg("cancel"));
    public void AckAlarm(long id) => Send(Wire.Msg("ack", ("ev", id)));
    public void Login(User u) => Send(Wire.Msg("login", ("user", u)));
    public void SetPage(string page) => Send(Wire.Msg("page", ("page", page)));
    public string RequestControl(string post) { var n = Call("reqctl", ("post", post)); return n == null ? null : (string)n; }
    public void ReleaseControl(string post) => Send(Wire.Msg("relctl", ("post", post)));
    public void AnswerControl(string post, bool yes) => Send(Wire.Msg("ansctl", ("post", post), ("yes", yes)));
    public void Phone(string toDesk, string text) => Send(Wire.Msg("phone", ("to", toDesk), ("text", text)));
    public List<PlanRow> GetPlan() => Wire.Get<List<PlanRow>>(Call("get", ("what", "plan"))) ?? new();
    public ScoreData GetScore() => Wire.Get<ScoreData>(Call("get", ("what", "score"))) ?? new() { Stats = new() };
    public BookData GetBook() => Wire.Get<BookData>(Call("get", ("what", "book"))) ?? new();
    public void AddBook(string text, string auto) => Send(Wire.Msg("book", ("text", text), ("auto", auto)));

    public void Dispose() { closed = true; try { tcp?.Close(); } catch { } }
}

using System.Windows.Threading;
using LcsWidzew.Model;
using LcsWidzew.Sim;

namespace LcsWidzew.Net;

/// <summary>
/// Pulpit zdalny SimRail (etap 3 integracji): stanowisko SUK bez własnego komputera zależności. Zależności liczy serwer gry,
/// SUK jest tylko pulpitem – jak stanowisko sieciowe (<see cref="RemoteSession"/>), ale źródłem meldunków jest gra:
/// <list type="bullet">
/// <item>meldunki (<see cref="Indication"/>) budowane ze stanów moda SimRailMost (UDP 47130) przez mapę nazw
///       <c>&lt;układ&gt;.simrail.json</c> (nazwa SimRail → odcinek / zwrotnica / sygnalizator SUK);</item>
/// <item>polecenia z pulpitu (Exec) tłumaczone na polecenia gry i wysyłane do moda (UDP 47131) – na razie pojedyncze:
///       Plus, Minus, Stop, oStop, Stój; przebiegi dopiero po podsłuchu prawdziwego polecenia pulpitu gry.</item>
/// </list>
/// </summary>
public sealed class SimRailSession : ILcsSession, IDisposable
{
    /// <summary>Pulpit zdalny działa (okno „SimRail” nie zatrzymuje wtedy wspólnego odbioru).</summary>
    public static bool Aktywna { get; private set; }

    readonly Dispatcher ui;
    readonly SimRailOdbiornik odb = SimRailOdbiornik.Wspolny;
    readonly Station uklad;
    readonly Dictionary<string, List<string>> doSimRail = new(StringComparer.Ordinal);   // id SUK → nazwy SimRail
    readonly Dictionary<string, string> doSuk = new(StringComparer.OrdinalIgnoreCase);     // nazwa SimRail → id SUK
    readonly Dictionary<string, bool> poprzZaj = new(StringComparer.OrdinalIgnoreCase);
    long seq; long nrZdarzenia;
    User user;

    public Simulator Sim => null;
    public bool IsHost => false;
    public string DeskId => "simrail";
    public string DeskName => "Pulpit zdalny SimRail";
    public Station L { get; }
    public Dictionary<string, List<RouteDef>> RouteTable { get; }
    public Indication Last { get; private set; }
    public List<EventRecord> Events { get; } = new();
    public List<EventRecord> Alarms { get; } = new();

    public event Action<EventRecord> EventAdded, AlarmAdded;
    public event Action AlarmsChanged, Beep, SpecialExecuted, PendingExpired;
    public event Action<string> Disconnected;
    public event Action Reset;

    public SimRailSession(Station uklad, Dispatcher ui, int port = SimRailOdbiornik.DomyslnyPort)
    {
        this.uklad = uklad; this.ui = ui; L = uklad.View;
        // tablica przebiegów układu – do wyboru początku i końca na pulpicie (wysyłanie przebiegów do gry jeszcze nieobsługiwane)
        try { RouteTable = new Simulator(uklad).RouteTable; } catch { RouteTable = new(); }
        var mapa = SimRailMapa.Wczytaj(SimRailMapa.PlikDla(uklad.SourcePath));
        foreach (var (sr, suk) in mapa.Mapa)
        {
            doSuk[sr] = suk;
            if (!doSimRail.TryGetValue(suk, out var l)) doSimRail[suk] = l = new();
            l.Add(sr);
        }
        if (mapa.Mapa.Count == 0) throw new InvalidOperationException($"Brak mapy nazw SimRail → SUK ({SimRailMapa.PlikDla(uklad.SourcePath)}). Wypełnij ją w oknie „SimRail”.");
        if (!odb.Dziala) odb.Start(port);
        Aktywna = true;
        Zdarz("Zdarzenie", "", "Pulpit zdalny SimRail: stany i polecenia przez mod SimRailMost (zależności liczy gra)");
    }

    public void Dispose() { Aktywna = false; }

    // ------------------------------------------------------------------ meldunki z gry

    public Indication Receive()
    {
        var wiek = odb.OstatniPakiet == default ? double.MaxValue : (DateTime.Now - odb.OstatniPakiet).TotalSeconds;
        if (wiek > 10) return Last = KzLink.NoData(L, Last);
        var I = new Indication { Seq = ++seq, User = user, Clock = Zegar(odb.CzasGry) };
        I.T = TimeSpan.TryParse(I.Clock, out var ts) ? ts.TotalSeconds : 0;
        foreach (var k in L.Sections.Keys) I.Secs[k] = new SecInd();
        foreach (var k in L.Switches.Keys) I.Sws[k] = new SwInd { Pos = Last?.Sws.GetValueOrDefault(k)?.Pos ?? "+" };
        foreach (var k in L.Signals.Keys) I.Sigs[k] = new SigInd { Aspect = Aspect.Stop };
        foreach (var k in L.Derailers.Keys) I.Ders[k] = new DerInd { On = true };
        foreach (var k in L.Blocks.Keys) I.Blocks[k] = new BlockInd();
        foreach (var p in L.Posts.Keys) I.Control[p] = DeskId;
        I.Desks.Add(new DeskInd { Id = DeskId, Name = DeskName, User = user?.Name });

        foreach (var o in odb.Obiekty())
        {
            if (!doSuk.TryGetValue(o.Nazwa, out var id)) continue;
            bool pociagowy = o.Przebieg.Contains("TrainRoute"), manewrowy = o.Przebieg.Contains("ShuntingRoute");
            if (I.Secs.TryGetValue(id, out var s))
            {
                s.TrainOcc |= o.Zajety;
                if (pociagowy) { s.LockType = "train"; s.Lock = "simrail"; }
                else if (manewrowy && s.LockType == null) { s.LockType = "shunt"; s.Lock = "simrail"; }
                if (o.Przebieg.Contains("TimeRelease")) s.TimeRel = 0;
                // numery pociągów z gry; gdzie je pokazać (tory stacyjne, odstępy) – decyduje pulpit (Station.Pip)
                if (o.Pociagi.Length > 0) I.Windows[id] = o.Pociagi.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            }
            if (I.Sws.TryGetValue(id, out var w) && L.Switches.TryGetValue(id, out var sw))
            {
                // położenie: „Wprost/Straight” = tor prosty; „+” to położenie zasadnicze (prosto, chyba że zasadniczo na „−”)
                bool prosto = o.Zwrotnica is "Wprost" or "Straight";
                bool bok = o.Zwrotnica is "Bok" or "Side";
                if (prosto || bok) w.Pos = prosto ^ sw.BasicMinus ? "+" : "-";
                w.Rozpr = o.Zwrotnica is "Rozprucie" or "Broken";
                w.Stopped = o.Blokada.Contains("Stopped") || o.Blokada.Contains("Zastopowanie");
                if (pociagowy || manewrowy) w.Lock = "simrail";
                w.NoCtrl = o.Basic.Contains("NoControl") || o.Basic.Contains("BrakKontroli");
            }
            if (I.Sigs.TryGetValue(id, out var g))
            {
                if (o.Przebieg.Contains("SubsidiarySignal")) g.Aspect = Aspect.Sz;
                else if (o.Przebieg.Contains("Signal")) g.Aspect = manewrowy && !pociagowy ? Aspect.Shunt : Aspect.Train;
                g.Closed = o.Blokada.Contains("Stopped") || o.Blokada.Contains("Zastopowanie");
            }
        }
        foreach (var o in odb.Obiekty())
        {
            if (poprzZaj.TryGetValue(o.Nazwa, out var b) && b != o.Zajety && doSuk.TryGetValue(o.Nazwa, out var id) && L.Sections.ContainsKey(id))
                Zdarz("Zdarzenie", Station.Lbl(id), (o.Zajety ? "zajęcie odcinka" : "zwolnienie odcinka") + (o.Pociagi.Length > 0 ? $" – pociąg {o.Pociagi}" : ""));
            poprzZaj[o.Nazwa] = o.Zajety;
        }
        return Last = I;
    }

    static string Zegar(string czasGry) => czasGry != null && czasGry.Length >= 19 ? czasGry[11..19] : "--:--:--";

    // ------------------------------------------------------------------ polecenia do gry

    static readonly Dictionary<string, string> NaGre = new()
    {
        ["PLUS"] = "Plus", ["MINUS"] = "Minus", ["STOP"] = "Stop", ["OSTOP"] = "RevokeStop", ["STOJ"] = "StopSignal",
    };

    public CmdResult Exec(string line)
    {
        var t = (line ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var kod = t.Select(SystemNadrzedny.KodPolecenia).LastOrDefault(x => x != null);
        if (kod == null) return new CmdResult(false, "Nieznane polecenie");
        if (!NaGre.TryGetValue(kod, out var order))
            return new CmdResult(false, kod is "POCIAG" or "MANEWR"
                ? "Pulpit zdalny SimRail: wysyłanie przebiegów do gry jeszcze nieobsługiwane (czeka na podsłuch polecenia przebiegu z pulpitu gry)."
                : $"Pulpit zdalny SimRail: polecenie {kod} jeszcze nieobsługiwane (na razie Plus, Minus, Stop, oStop, Stój).");
        var obiekty = t.Where(x => SystemNadrzedny.KodPolecenia(x) == null).ToList();
        if (obiekty.Count == 0) return new CmdResult(false, "Brak obiektu polecenia");
        var wyniki = new List<string>(); bool wszystkieOk = true;
        foreach (var ob in obiekty)
        {
            var id = Znajdz(ob, kod);
            if (id == null) { wszystkieOk = false; wyniki.Add($"{ob}: nie ma takiego obiektu na pulpicie"); continue; }
            if (!doSimRail.TryGetValue(id, out var nazwy) || nazwy.Count == 0) { wszystkieOk = false; wyniki.Add($"{ob}: brak w mapie SimRail"); continue; }
            var (ok, msg) = odb.WyslijPolecenie(nazwy[0], order);
            wszystkieOk &= ok; wyniki.Add($"{ob}: {msg}");
            Zdarz("Polecenie", ob, $"{kod} → gra: {(ok ? "wysłano" : "odrzucono")} ({msg})");
        }
        return new CmdResult(wszystkieOk, string.Join("; ", wyniki));
    }

    /// <summary>Obiekt po oznaczeniu z pulpitu: zwrotnica / wykolejnica dla Plus/Minus, sygnalizator dla Stój, oba dla Stop.</summary>
    string Znajdz(string ozn, string kod)
    {
        var f = SystemNadrzedny.Fold(ozn);
        bool Rowne(string label, string name) => SystemNadrzedny.Fold(label ?? "") == f || SystemNadrzedny.Fold(name ?? "") == f;
        if (kod != "STOJ")
        {
            var sw = L.Switches.Values.FirstOrDefault(x => Rowne(x.Label, x.Num)); if (sw != null) return sw.Num;
            var de = L.Derailers.Values.FirstOrDefault(x => Rowne(x.Label, x.Name)); if (de != null) return de.Name;
        }
        if (kod is "STOJ" or "STOP" or "OSTOP")
        {
            var sg = L.Signals.Values.FirstOrDefault(x => Rowne(x.Label, x.Name)); if (sg != null) return sg.Name;
        }
        return null;
    }

    void Zdarz(string kat, string obiekt, string tekst)
    {
        var e = new EventRecord { Id = ++nrZdarzenia, Czas = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), Obszar = L.Name ?? "SimRail", Stacja = "SimRail",
                                  TypObiektu = "SimRail", Obiekt = obiekt, Kategoria = kat, Zdarzenie = tekst, Operator = user?.Name ?? "", Zrodlo = "SimRail" };
        void Dodaj() { Events.Add(e); EventAdded?.Invoke(e); }
        if (ui.CheckAccess()) Dodaj(); else ui.BeginInvoke(Dodaj);
    }

    // ------------------------------------------------------------------ reszta stanowiska – bez komputera zależności
    public CmdResult Confirm(string typed) => new(false, "Pulpit zdalny SimRail: polecenia dwuetapowe jeszcze nieobsługiwane");
    public void Cancel() { }
    public void AckAlarm(long id) { }
    public void Login(User u) => user = u;
    public void SetPage(string page) { }
    public string RequestControl(string post) => null;
    public void ReleaseControl(string post) { }
    public void AnswerControl(string post, bool yes) { }
    public void Phone(string toDesk, string text) { }
    public List<PlanRow> GetPlan() => new();
    public ScoreData GetScore() => new() { Stats = new() };
    public BookData GetBook() => new();
    public void AddBook(string text, string auto) { }
    public EdrData GetEdr() => null;
    public CmdResult EdrCmd(EdrPolecenie p) => new(false, "EDR niedostępny w trybie pulpitu zdalnego SimRail");

    void Nieuzywane() { AlarmAdded?.Invoke(null); AlarmsChanged?.Invoke(); Beep?.Invoke(); SpecialExecuted?.Invoke(); PendingExpired?.Invoke(); Disconnected?.Invoke(null); Reset?.Invoke(); }
}

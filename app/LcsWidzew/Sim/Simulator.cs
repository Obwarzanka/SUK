using System.Text.RegularExpressions;
using System.IO;
using LcsWidzew.Model;

namespace LcsWidzew.Sim;

/// <summary>
/// Fasada symulacji (PODZIAL_MODULOW.md krok 10): czas, rejestrator, topologia układu, moduły i cykl <see cref="Tick"/>.
/// Komputer zależności – <see cref="KomputerZaleznosci"/> (Sim/Kz/), system nadrzędny – <see cref="SystemNadrzedny"/> (Sim/Pulpit/),
/// urządzenia – <see cref="Urzadzenia"/>; dawne pola i metody przekierowane do modułów (Sim/Kz/Fasada.cs, Sim/Desk.cs).
/// </summary>
public partial class Simulator
{
    public static class Cfg
    {
        public const double SwTime = 5, SwTimeout = 12, SzTime = 90, SpecialConfirm = 60, SpecialMinWait = 5, TimeRelease = 120, ZwlokaZd = 90,
            BlockRelease = 3, AzkTime = 5, NbrDelay = 6, ReqAlarmAfter = 180, ParamTimeout = 60;
    }

    public Station L { get; }
    /// <summary>Katalog plików rozkładu nadany przez hosta lub wynikający ze źródła układu.</summary>
    public string DataDirectory { get; }
    public double T;
    public double StartClock = 6 * 3600;
    public Stats Stats = new();

    /// <summary>Rejestrator zdarzeń i alarmów – usługa wspólna modułów (Sim/Rejestrator.cs).</summary>
    public readonly Rejestrator Rej;
    public List<EventRecord> Events => Rej.Events;
    public List<EventRecord> Alarms => Rej.Alarms;
    public List<(string Time, string Text)> Remarks => Rej.Remarks;
    public event Action<EventRecord> EventAdded { add => Rej.EventAdded += value; remove => Rej.EventAdded -= value; }
    public event Action<EventRecord> AlarmAdded { add => Rej.AlarmAdded += value; remove => Rej.AlarmAdded -= value; }
    public event Action Beep { add => Rej.Beep += value; remove => Rej.Beep -= value; }
    public event Action<string> SpecialExecuted;
    public event Action PendingExpired;

    public Simulator(Station st, bool routes = true, SimulatorOptions options = null)
    {
        L = st;
        DataDirectory = Path.GetFullPath(options?.DataDirectory ?? (st.SourcePath == null ? "." : Path.GetDirectoryName(st.SourcePath)));
        Nadrzedny = new SystemNadrzedny(this);
        Rej = new Rejestrator(L, () => T, () => Clock(), () => User, options?.RejestratorLogger);
        mapaLog = options?.MapaWlasnosciLogger;
        Instruktor = new Instruktor(this);
        Rozklad = new Rozklad(this);
        Otoczenie = new Otoczenie(this);
        Kz = new KomputerZaleznosci(this);
        Urzadzenia = new Urzadzenia(this);
        Edr = new Edr(this);
        if (routes) Kz.BuildRouteTable();
    }

    // ------------------------------------------------------------------ czas, dziennik
    public string Clock(double? t = null)
    {
        var s = (long)Math.Floor(StartClock + (t ?? T));
        return $"{s / 3600 % 24:00}:{s / 60 % 60:00}:{s % 60:00}";
    }

    public string Clean(string s) => Rej.Clean(s);
    /// <summary>Skrót posterunku: inicjały nazwy (Łódź Widzew → ŁW, Koluszki → Ko).</summary>
    public static string PostShort(Station L, string post) => Rejestrator.PostShort(L, post);

    public EventRecord Log(string kategoria, string text, string typ = "", string obiekt = "", string zrodlo = "System") => Rej.Log(kategoria, text, typ, obiekt, zrodlo);
    /// <summary>Alarm (Ie-104 § 7): lista z potwierdzeniem i znacznikiem trwania stanu alarmowego.</summary>
    public void Alarm(string text, string typ = "", string obiekt = "", string key = null, Func<bool> stillActive = null) => Rej.Alarm(text, typ, obiekt, key, stillActive);
    public void Remark(string text) => Rej.Remark(text);

    // ------------------------------------------------------------------ topologia
    public record Fwd(string Edge, string Num, string Pos);

    public List<Fwd> Forward(string nid, int dir, string fromEdge)
    {
        var n = L.Nodes[nid];
        var res = new List<Fwd>();
        if (n.Switch != null)
        {
            var sw = L.Switches[n.Switch];
            if (sw.TipDir == dir)
            {
                if (fromEdge == sw.Plus) res.Add(new Fwd(sw.Tip, sw.Num, "+"));
                else if (fromEdge == sw.Minus) res.Add(new Fwd(sw.Tip, sw.Num, "-"));
                else if (fromEdge == null) res.Add(new Fwd(sw.Tip, null, null));
            }
            else
            {
                if (!L.Edges[sw.Plus].Dismantled) res.Add(new Fwd(sw.Plus, sw.Num, "+"));
                if (!L.Edges[sw.Minus].Dismantled) res.Add(new Fwd(sw.Minus, sw.Num, "-"));
            }
            return res;
        }
        foreach (var e in n.Edges)
            if (e != fromEdge && !L.Edges[e].Dismantled && L.EdgeDir(e, nid) == dir) res.Add(new Fwd(e, null, null));
        // skrzyżowanie torów: przez krzyż tylko na wprost
        if (res.Count > 1 && fromEdge != null && n.Edges.Count > 3 && L.Straight(nid, fromEdge, res.Select(f => f.Edge)) is string st)
            res.RemoveAll(f => f.Edge != st);
        return res;
    }

    /// <summary>Następny tor wg położenia iglic w terenie (ruch pociągów).</summary>
    public Fwd NextInTerrain(string nid, int dir, string fromEdge) => Next(nid, dir, fromEdge, Urzadzenia.Iglice);

    public Fwd Next(string nid, int dir, string fromEdge, Func<string, string> posOf)
    {
        var n = L.Nodes[nid];
        if (n.Switch != null && L.Switches[n.Switch].TipDir != dir)
        {
            var sw = L.Switches[n.Switch]; var pos = posOf(n.Switch); var e = pos == "+" ? sw.Plus : sw.Minus;
            return L.Edges[e].Dismantled ? null : new Fwd(e, sw.Num, pos);
        }
        var o = Forward(nid, dir, fromEdge);
        return o.Count == 0 ? null : o[0];
    }

    // ------------------------------------------------------------------ pętla
    public Trains Trains;
    /// <summary>Instruktor: usterki i scenariusze usterek (Sim/Instruktor.cs).</summary>
    public readonly Instruktor Instruktor;
    /// <summary>Rozkład jazdy (Sim/Rozklad.cs).</summary>
    public readonly Rozklad Rozklad;
    /// <summary>Posterunki sąsiednie spoza LCS (Sim/Otoczenie.cs).</summary>
    public readonly Otoczenie Otoczenie;
    /// <summary>Urządzenia przytorowe i karty sterowników (Sim/Urzadzenia/).</summary>
    public readonly Urzadzenia Urzadzenia;
    /// <summary>Elektroniczny dziennik ruchu (Sim/Edr/, PLAN_EDR.md).</summary>
    public readonly Edr Edr;

    public void Tick(double dt)
    {
        T += dt;
        Nadrzedny.Tick();
        Urzadzenia.Tick(); Kz.UpdateBlocks();
        if (Trains != null) { Rozklad.Tick(); Instruktor.Tick(); }
        Trains?.Tick(dt);
        Kz.UpdateSignals(); Kz.UpdateRoutes(); Kz.SignalCodes();
        Nadrzedny.Pip.Tick();
        Rej.UpdateAlarms();
        Edr.Tick();
    }

}

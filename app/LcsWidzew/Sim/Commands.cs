using LcsWidzew.Model;

namespace LcsWidzew.Sim;

/// <summary>
/// Komputer zależności – wykonanie poleceń systemowych (adresy rozwiązał system nadrzędny, Sim/Pulpit/SystemNadrzedny.cs):
/// warunki zależności i skutki poleceń Ie-104 § 12.
/// </summary>
public partial class Simulator
{
    /// <summary>Polecenie specjalne – warunki wstępne przed potwierdzeniem (pierwszy etap): błąd albo adres i obiekty do wyróżnienia.</summary>
    public (string Err, string Target, List<string> Targets) SprawdzSpecjalne(string code, string key)
    {
        switch (code)
        {
            case "SZ":
            {
                var s = key;
                if (!L.Signals[s].IsTrain) return ("Sygnał zastępczy możliwy tylko na semaforze", null, null);
                if (Sigs[s].Aspect == Aspect.Train) return ("Semafor wyświetla sygnał zezwalający", null, null);
                if (Sigs[s].Flank.Count > 0) return ($"Semafor utwierdzony w ochronie bocznej przebiegu {FlankOwner(Sigs[s].Flank, null)}", null, null);
                return (null, s, new() { s });
            }
            case "ZDP":
            {
                var s = key;
                var r = Routes.Values.FirstOrDefault(x => x.Start == s && x.Type == "P");
                return r == null ? ("Brak przebiegu pociągowego od " + Station.Lbl(s), null, null) : (null, s, new List<string> { s }.Concat(r.Secs).ToList());
            }
            case "PLUSBZ":
            case "MINBZ":
            {
                var w = key;
                if (Sws[w].Lock != null) return ("Zwrotnica utwierdzona w przebiegu", null, null);
                if (Sws[w].Flank.Count > 0) return ($"Zwrotnica utwierdzona w ochronie bocznej przebiegu {FlankOwner(Sws[w].Flank, null)}", null, null);
                if (Sws[w].Stopped) return ("Zwrotnica zastopowana", null, null);
                return (null, w, new() { w, L.Switches[w].Sec });
            }
            case "KSR":
                return Sws[key].Rozpr ? (null, key, new() { key }) : ($"Zwrotnica {Station.Lbl(key)} nie sygnalizuje rozprucia", null, null);
            case "ZEROLO":
            {
                var sc = key;
                if (!L.Sections[sc].AxleCounter) return ($"Odcinek {Station.Lbl(sc)} jest kontrolowany obwodem torowym – zerowanie licznika osi go nie dotyczy (zajętość ustępuje samoczynnie po usunięciu przyczyny)", null, null);
                return Secs[sc].Fault is "reset" or "occ" ? (null, sc, L.Switches.Values.Where(x => x.Sec == sc).Select(x => x.Num).Append(sc).ToList()) : ($"Odcinek {Station.Lbl(sc)} nie wymaga zerowania", null, null);
            }
            case "AZK":
                return (null, key, new() { key });
            case "NN":
                return Power.NN ? ("Napięcie nastawcze jest załączone", null, null) : (null, "NN", new());
        }
        return (null, key, new());
    }

    /// <summary>Polecenie „wszystkie semafory Stój” (STW).</summary>
    public CmdResult WszystkieStoj()
    {
        foreach (var (n, x) in Sigs)
            if (L.Signals[n].Type != "odstepowy" && x.Aspect is Aspect.Train or Aspect.Shunt) { x.Aspect = Aspect.Stop; if (x.Route != null && Routes.TryGetValue(x.Route, out var q)) q.State = "stopped"; }
        return new(true, "wszystkie semafory – „Stój”");
    }

    /// <summary>Wykonanie polecenia systemowego (adresy już rozwiązane).</summary>
    public CmdResult Wykonaj(PolecenieSystemowe p)
    {
        string code = p.Kod, a0 = p.Arg0, k0 = p.Obiekty.FirstOrDefault();
        switch (code)
        {
            case "POCIAG":
            case "MANEWR":
                return NastawPrzebieg(code == "POCIAG" ? "P" : "M", p.Obiekty[0], p.Obiekty[^1], p.Obiekty.Skip(1).Take(p.Obiekty.Count - 2).ToList());
            case "STOJ":
            {
                var s = k0;
                var st = Sigs[s];
                if (st.Aspect == Aspect.Stop) return new(false, "Sygnalizator wskazuje „Stój”");
                if (st.Aspect == Aspect.Sz) { st.Aspect = Aspect.Stop; st.SzUntil = 0; return new(true, "wyłączono sygnał zastępczy"); }
                st.Aspect = Aspect.Stop;
                if (st.Route != null && Routes.TryGetValue(st.Route, out var ru)) { ru.State = "stopped"; ru.ManualStop = true; }
                return new(true, "„Stój” (przebieg pozostaje utwierdzony)");
            }
            case "STOP":
            case "OSTOP":
            {
                bool on = code == "STOP";
                var k = k0;
                if (L.Signals.ContainsKey(k))
                {
                    Sigs[k].Closed = on;
                    if (on && Sigs[k].Aspect is Aspect.Train or Aspect.Shunt) { Sigs[k].Aspect = Aspect.Stop; if (Sigs[k].Route != null && Routes.TryGetValue(Sigs[k].Route, out var q)) q.State = "stopped"; }
                    return new(true, on ? "sygnalizator zastopowany" : "odwołano zastopowanie sygnalizatora");
                }
                if (L.Switches.ContainsKey(k)) { Sws[k].Stopped = on; return new(true, on ? "zwrotnica zastopowana" : "odwołano zastopowanie zwrotnicy"); }
                return BlockCmd(code, k, p.Koniec);
            }
            case "OSZ":
            {
                var s = k0;
                if (Sigs[s].Aspect != Aspect.Sz) return new(false, $"Na {a0} nie jest wyświetlany sygnał zastępczy");
                Sigs[s].Aspect = Aspect.Stop; Sigs[s].SzUntil = 0; return new(true, "");
            }
            case "ZDM":
            {
                var s = k0;
                var r = Routes.Values.FirstOrDefault(x => x.Start == s && x.Type == "M");
                if (r == null) return new(false, "Brak przebiegu manewrowego od " + Station.Lbl(s));
                ReleaseRoute(r.Uid, "ZDM"); return new(true, "przebieg manewrowy zwolniony");
            }
            case "ZCZ":
            {
                var s = k0;
                var r = Routes.Values.FirstOrDefault(x => x.Start == s);
                if (r == null) return new(false, "Brak przebiegu od " + Station.Lbl(s));
                if (r.Type == "M") { ReleaseRoute(r.Uid, "zwolnienie"); return new(true, "przebieg manewrowy zwolniony"); }
                var appr = L.Signals[s].ApproachSec;
                bool opened = r.State is "set" or "stopped" || r.Passed;
                if (opened && (r.Passed || (appr != null && SecOcc(appr))))
                {
                    Sigs[s].Aspect = Sigs[s].Aspect == Aspect.Sz ? Aspect.Sz : Aspect.Stop;
                    r.TimeRelUntil = T + Cfg.TimeRelease;
                    foreach (var sc in r.Secs) if (Secs[sc].Lock == r.Uid) Secs[sc].TimeRel = r.TimeRelUntil;
                    return new(true, $"zwolnienie ze zwłoką czasową {Cfg.TimeRelease:0} s");
                }
                ReleaseRoute(r.Uid, "odwołanie"); return new(true, "przebieg odwołany");
            }
            case "OZCZ":
            {
                var s = k0;
                var r = Routes.Values.FirstOrDefault(x => x.Start == s && x.TimeRelUntil != null);
                if (r == null) return new(false, "Brak odliczania czasu zwolnienia od " + Station.Lbl(s));
                r.TimeRelUntil = null; foreach (var sc in r.Secs) if (Secs[sc].Lock == r.Uid) Secs[sc].TimeRel = null;
                return new(true, "przerwano odliczanie – przebieg utwierdzony");
            }
            case "PLUS":
            case "MINUS":
            {
                var k = k0;
                if (L.Derailers.ContainsKey(k))
                {
                    var d = Ders[k]; if (d.Lock != null) return new(false, "Wykolejnica utwierdzona w przebiegu");
                    if (d.Flank.Count > 0) return new(false, $"Wykolejnica utwierdzona w ochronie bocznej przebiegu {FlankOwner(d.Flank, null)}");
                    if (SecOcc(L.Derailers[k].Sec)) return new(false, "Odcinek zajęty");
                    DerCmd(k, code == "PLUS"); return new(true, code == "PLUS" ? "zakładanie wykolejnicy" : "zdejmowanie wykolejnicy");
                }
                var w = Sws[k]; var pos = code == "PLUS" ? "+" : "-";
                if (w.NoData) return new(false, "Brak danych o zwrotnicy");
                if (w.Lock != null) return new(false, "Zwrotnica utwierdzona w przebiegu");
                if (w.Flank.Count > 0) return new(false, $"Zwrotnica utwierdzona w ochronie bocznej przebiegu {FlankOwner(w.Flank, null)}");
                if (w.Stopped) return new(false, "Zwrotnica zastopowana (Ie-20 §15 ust. 6)");
                if (w.Local) return new(false, "Zwrotnica przekazana do nastawiania lokalnego");
                if (w.Rozpr) return new(false, "Sygnalizowane rozprucie – najpierw KSR po sprawdzeniu w terenie");
                if (!Power.NN) return new(false, "Napięcie nastawcze wyłączone");
                if (SecOcc(L.Switches[k].Sec)) return new(false, "Odcinek zwrotnicowy zajęty – tylko PlusBZ/MinBZ (Ie-20 §15 ust. 3)");
                if (w.Moving == null && w.Pos == pos) return new(false, $"Zwrotnica już w położeniu „{pos}”");
                MoveSwitch(k, pos); return new(true, "");
            }
            case "ZMK":
            case "OZMK":
            {
                var k = k0;
                if (code == "ZMK" && Secs[k].Lock != null) return new(false, "Odcinek utwierdzony w przebiegu");
                Secs[k].Closed = code == "ZMK"; return new(true, code == "ZMK" ? "zamknięcie ruchowe" : "odwołano zamknięcie ruchowe");
            }
            case "WBL":
            case "BKON":
            case "PO":
            case "POZ":
            case "DPO":
            case "DKO":
            case "OWBL":
            case "PZK":
            case "ZWBL":
            case "AZK":
            {
                return BlockCmd(code, k0, p.Koniec);
            }
            case "ONN": Urzadzenia.NapiecieNastawcze(false); return new(true, "napięcie nastawcze odłączone");
            case "NN": Urzadzenia.NapiecieNastawcze(true); return new(true, "napięcie nastawcze załączone");
            case "AGR":
                if (Power.Mains) return new(false, "Zasilanie sieciowe obecne");
                if (Power.Gen) return new(false, "Agregat pracuje");
                Urzadzenia.UruchomAgregat(); return new(true, "agregat uruchomiony");
            // ----- specjalne (po potwierdzeniu)
            case "SZ":
            {
                var s = k0;
                var st = Sigs[s]; st.Aspect = Aspect.Sz; st.SzUntil = T + Cfg.SzTime; st.FailAlarm = false;
                CheckSzPractice(s);
                return new(true, $"sygnał zastępczy ({Cfg.SzTime:0} s)");
            }
            case "ZDP":
            {
                var s = k0;
                var r = Routes.Values.First(x => x.Start == s && x.Type == "P");
                if (Sigs[s].Aspect != Aspect.Sz) Sigs[s].Aspect = Aspect.Stop;
                ReleaseRoute(r.Uid, "ZDP"); return new(true, "przebieg pociągowy zwolniony natychmiast");
            }
            case "PLUSBZ":
            case "MINBZ":
            {
                var w = k0;
                if (!Power.NN) return new(false, "Napięcie nastawcze wyłączone");
                MoveSwitch(w, code == "PLUSBZ" ? "+" : "-"); return new(true, "przestawienie z bocznikowaniem");
            }
            case "KSR":
            {
                // brak kontroli trwa dalej (iglice uszkodzone) – KZ zgłosi go w następnym cyklu jako brak kontroli, nie rozprucie
                var k = k0; var w = Sws[k]; w.Rozpr = false; w.KsrDone = true;
                return new(true, "skasowano sygnalizację rozprucia");
            }
            case "ZEROLO":
            {
                var sc = k0;
                Urzadzenia.Zeruj(sc); return new(true, "odcinek oczekuje na pierwszy przejazd");
            }
        }
        return new(false, $"Polecenie „{code}” nieobsługiwane");
    }

    /// <summary>
    /// Nastawienie przebiegu: początek, punkty pośrednie, koniec. Punkt pośredni (zwrotnica, np. 13 lub 13-, odcinek, sygnalizator)
    /// wybiera przebieg wariantowy. Przebieg manewrowy może być złożony z kolejnych przebiegów elementarnych (przez tarcze).
    /// </summary>
    CmdResult NastawPrzebieg(string type, string start, string end, List<string> via)
    {
        var rd = FindRoute(start, end, type, via);
        if (rd != null)
        {
            var res = SetRoute(rd);
            if (!res.Ok) return new(false, res.Why[0] + (res.Why.Count > 1 ? $" (+{res.Why.Count - 1})" : ""));
            return new(true, $"przebieg {rd.Id} w realizacji{(rd.Variant > 0 ? " – przebieg wariantowy" : "")}, V = {rd.VMax:0} km/h");
        }
        if (type == "M" && via.Count == 0)
        {
            var chainR = ShuntChain(start, end);
            if (chainR != null)
            {
                var set = new List<Route>();
                foreach (var part in chainR)
                {
                    var res = SetRoute(part);
                    if (!res.Ok) { foreach (var x in set) ReleaseRoute(x.Uid, null); return new(false, $"{part.Id}: {res.Why[0]}"); }
                    set.Add(res.R);
                }
                return new(true, $"przebieg manewrowy złożony: {string.Join(" + ", chainR.Select(c => Station.Lbl(c.Start) + "→" + Station.Lbl(c.End)))}");
            }
        }
        return new(false, $"Brak przebiegu {(type == "P" ? "pociągowego" : "manewrowego")} {Station.Lbl(start)} → {Station.Lbl(end)}{(via.Count > 0 ? " przez " + string.Join(", ", via.Select(Station.Lbl)) : "")} w tablicy przebiegów");
    }

    List<RouteDef> ShuntChain(string start, string end)
    {
        var q = new Queue<List<RouteDef>>();
        foreach (var r in RouteTable.GetValueOrDefault(start, new()).Where(r => r.Type == "M" && r.Variant == 0)) q.Enqueue(new() { r });
        var seen = new HashSet<string> { start };
        while (q.Count > 0)
        {
            var path = q.Dequeue();
            var last = path[^1];
            if (last.End == end) return path;
            if (path.Count >= 6 || last.EndKind != "sig" || !seen.Add(last.End)) continue;
            foreach (var r in RouteTable.GetValueOrDefault(last.End, new()).Where(r => r.Type == "M" && r.Variant == 0))
                q.Enqueue(new List<RouteDef>(path) { r });
        }
        return null;
    }
}

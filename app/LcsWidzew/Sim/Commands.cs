using System.Globalization;
using System.Text;
using LcsWidzew.Model;

namespace LcsWidzew.Sim;

public record CmdResult(bool Ok, string Msg);

/// <summary>
/// Interpreter poleceń. Kody wg Ie-104 § 12; składnia „adres(y) kod” (Ie-104 § 11 ust. 7) lub „kod adres(y)” (jak w EbiScreen).
/// Adresy rozwiązywane w kontekście wyświetlanego obrazu (Ie-104 § 11 ust. 6).
/// </summary>
public partial class Simulator
{
    public record CodeInfo(string Code, string Desc, bool Special);

    public static readonly Dictionary<string, CodeInfo> Codes = new()
    {
        ["POCIAG"] = new("Pociąg", "nastawianie przebiegu pociągowego", false),
        ["MANEWR"] = new("Manewr", "nastawianie przebiegu manewrowego", false),
        ["STOJ"] = new("Stój", "sygnał zabraniający na sygnalizatorze", false),
        ["STOP"] = new("Stop", "zastopowanie sygnalizatora / zwrotnicy, zamknięcie wyjazdu na szlak", false),
        ["OSTOP"] = new("oStop", "odwołanie zastopowania / zamknięcia wyjazdu", false),
        ["SZ"] = new("SZ", "sygnał zastępczy", true),
        ["OSZ"] = new("oSZ", "odwołanie sygnału zastępczego", false),
        ["ZDP"] = new("ZDP", "zwolnienie doraźne natychmiastowe przebiegu pociągowego", true),
        ["ZDM"] = new("ZDM", "zwolnienie doraźne natychmiastowe przebiegu manewrowego", false),
        ["ZCZ"] = new("ZCZ", "zwolnienie doraźne przebiegu pociągowego ze zwłoką czasową", false),
        ["OZCZ"] = new("oZCZ", "przerwanie odliczania czasu zwolnienia przebiegu", false),
        ["PLUS"] = new("Plus", "przestawienie zwrotnicy do „+” / założenie wykolejnicy", false),
        ["MINUS"] = new("Minus", "przestawienie zwrotnicy do „−” / zdjęcie wykolejnicy", false),
        ["PLUSBZ"] = new("PlusBZ", "przestawienie do „+” z bocznikowaniem zajętości", true),
        ["MINBZ"] = new("MinBZ", "przestawienie do „−” z bocznikowaniem zajętości", true),
        ["KSR"] = new("KSR", "kasowanie sygnalizacji rozprucia", true),
        ["ZMK"] = new("Zmk", "zamknięcie ruchowe toru / zwrotnicy", false),
        ["OZMK"] = new("oZmk", "odwołanie zamknięcia ruchowego", false),
        ["ZEROLO"] = new("ZeroLO", "zerowanie licznika osi sekcji kontroli niezajętości", true),
        ["WBL"] = new("Wbl", "włączenie (żądanie) kierunku blokady WYJAZD", false),
        ["BKON"] = new("Ko", "blok końcowy – blokada półsamoczynna (po przyjeździe całego pociągu)", false),
        ["PO"] = new("Po", "blok początkowy – blokada półsamoczynna (po wyprawieniu pociągu i wygaszeniu sygnału)", false),
        ["POZ"] = new("Poz", "blok pozwolenia – oddanie pozwolenia (kierunku) stacji sąsiedniej", false),
        ["DPO"] = new("dPo", "doraźny blok początkowy – wyjazd na sygnał zastępczy / rozkaz pisemny (licznik)", true),
        ["DKO"] = new("dKo", "doraźny blok końcowy – wjazd na sygnał zastępczy / rozkaz pisemny (licznik)", true),
        ["OWBL"] = new("oWbl", "odwołanie włączania blokady", false),
        ["PZK"] = new("Pzk", "pozwolenie na ustawienie kierunku (PRZYJAZD)", false),
        ["ZWBL"] = new("Zwbl", "zwolnienie ustawionego kierunku", false),
        ["AZK"] = new("AZK", "awaryjna zmiana kierunku blokady", true),
        ["NN"] = new("NN", "załączenie napięcia nastawczego", true),
        ["ONN"] = new("oNN", "odłączenie napięcia nastawczego", false),
        ["AGR"] = new("AGR", "ręczne uruchomienie agregatu (polecenie dodatkowe)", false),
        ["KW"] = new("KW", "potwierdzenie wszystkich alarmów (polecenie dodatkowe)", false),
    };

    static readonly Dictionary<string, string> Aliases = new()
    {
        ["POC"] = "POCIAG", ["PP"] = "POCIAG", ["MAN"] = "MANEWR", ["PM"] = "MANEWR", ["ST"] = "STOJ", ["SSS"] = "STOJ",
        ["Z+"] = "PLUS", ["Z-"] = "MINUS", ["ZS"] = "STOP", ["ZO"] = "OSTOP", ["SZM"] = "STOP", ["SOM"] = "OSTOP", ["OZ"] = "OSZ",
        ["ZP"] = "ZCZ", ["DZ"] = "ZDP", ["RK"] = "KSR", ["ZER"] = "ZEROLO", ["ZLO"] = "ZEROLO", ["ZT"] = "ZMK", ["OT"] = "OZMK",
        ["WZ"] = "PLUS", ["WD"] = "MINUS", ["MINUSBZ"] = "MINBZ", ["BKA"] = "AZK", ["NN1"] = "NN", ["NN0"] = "ONN", ["NSZ"] = "SZ",
        ["POCIAG"] = "POCIAG", ["MANEWR"] = "MANEWR", ["KO"] = "BKON", ["BKON"] = "BKON"
    };

    public static string Fold(string s)
    {
        var n = s.ToUpperInvariant().Replace("Ł", "L").Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in n) if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString();
    }
    static string CodeOf(string tok)
    {
        var f = Fold(tok);
        if (Codes.ContainsKey(f)) return f;
        return Aliases.TryGetValue(f, out var c) ? c : null;
    }

    /// <summary>
    /// Koniec szlaku, którego dotyczy polecenie: drugi adres może wskazać kierunek wyjazdu na obrazie pulpitu (E / → – w prawo, W / Z / ← – w lewo)
    /// albo posterunek (np. LF, LN); bez niego – koniec po stronie wyświetlanego obrazu.
    /// </summary>
    int EndFor(BlockDef b, List<string> args)
    {
        // oznaczenie końca szlaku wewnętrznego (np. 1G) wskazuje posterunek
        if (args.Count > 0) for (int i = 0; i < b.Ends.Count; i++) if (!string.IsNullOrEmpty(b.Ends[i].Label) && Fold(b.Ends[i].Label) == Fold(args[0]) && Fold(b.Id) != Fold(args[0])) return i;
        foreach (var t in args.Skip(1))
        {
            var f = Fold(t);
            int dir = f is "E" or "WSCH" or "WSCHOD" or "→" or ">" or "->" ? 1 : f is "W" or "Z" or "ZACH" or "ZACHOD" or "←" or "<" or "<-" ? -1 : 0;
            if (dir != 0)
            {
                // w prawo / w lewo na obrazie pulpitu (zwrot końca szlaku na rysunku, nie kierunek toru A→B)
                var vb = L.View.Blocks.GetValueOrDefault(b.Id) ?? b;
                for (int i = 0; i < b.Ends.Count && i < vb.Ends.Count; i++) if (vb.Ends[i].VOut == dir) return i;
            }
            for (int i = 0; i < b.Ends.Count; i++) if (Fold(b.Ends[i].Post ?? "") == f) return i;
        }
        // stanowisko steruje tylko jednym końcem – ten koniec
        if (CurDesk != null) { var mine = Enumerable.Range(0, b.Ends.Count).Where(i => Controls(b.Ends[i].Post)).ToList(); if (mine.Count == 1 && b.Ends.Count > 1) return mine[0]; }
        // szlak wewnętrzny z obu końcami na obrazie – strona musi być wskazana
        var pg = L.Pages.FirstOrDefault(p => p.Id == Page);
        if (b.Internal && b.Ends.Count == 2 && pg != null && b.Ends.All(x => pg.Posts.Contains(x.Post))) return -1;
        return EndIdx(b, Page);
    }

    // ------------------------------------------------------------------ adresy
    IEnumerable<string> PagePosts() => L.Pages.FirstOrDefault(p => p.Id == Page)?.Posts ?? new List<string>();

    /// <summary>Rozwiązanie adresu w kontekście obrazu: klucz pełny, nazwa obiektu na wyświetlanym obrazie albo opis (np. numer tarczy).</summary>
    public string Resolve(string tok, params string[] kinds)
    {
        if (string.IsNullOrEmpty(tok)) return null;
        var f = Fold(tok);
        var posts = PagePosts().ToList();
        foreach (var k in kinds.Length > 0 ? kinds : new[] { "sig", "sw", "sec", "der", "blk", "end" })
        {
            IEnumerable<(string Key, string Post, string Label)> c = k switch
            {
                "sig" => L.Signals.Values.Where(s => s.Type != "odstepowy").Select(s => (s.Name, s.Post, s.Label)),
                "sw" => L.Switches.Values.Select(s => (s.Num, s.Post, s.Label)),
                "sec" => L.Sections.Keys.Select(s => (s, Station.PostOf(s), Station.Lbl(s))),
                "der" => L.Derailers.Values.Select(d => (d.Name, d.Post, d.Label)),
                "blk" => L.Blocks.Values.SelectMany(b => new[] { (b.Id, "", b.Id) }.Concat(b.Ends.Where(x => !string.IsNullOrEmpty(x.Label)).Select(x => (b.Id, x.Post ?? "", x.Label)))),
                "end" => L.TrackEnds.Values.Select(t => (t.Id, Station.PostOf(t.Id), t.Disp)),
                _ => Enumerable.Empty<(string, string, string)>()
            };
            var list = c.ToList();
            var exact = list.FirstOrDefault(x => Fold(x.Key) == f);
            if (exact.Key != null) return exact.Key;
            var onPage = list.Where(x => x.Post == "" || posts.Contains(x.Post)).ToList();
            var byName = onPage.FirstOrDefault(x => Fold(Station.Lbl(x.Key)) == f);
            if (byName.Key != null) return byName.Key;
            if (k != "sw")
            {
                var byLabel = onPage.FirstOrDefault(x => Fold(x.Label ?? "") == f);
                if (byLabel.Key != null) return byLabel.Key;
            }
            if (k == "blk") { var anyEnd = list.FirstOrDefault(x => Fold(x.Label ?? "") == f); if (anyEnd.Key != null) return anyEnd.Key; }
        }
        return null;
    }

    /// <summary>Nazwy obiektów ze spacją (np. szlak „W 101”, odcinek „W 101-1”) – po złożeniu wielkimi literami.</summary>
    HashSet<string> MultiWordNames()
    {
        var all = L.Blocks.Values.SelectMany(b => new[] { b.Id }.Concat(b.Ends.Select(x => x.Label)))
            .Concat(L.Signals.Values.SelectMany(s => new[] { s.Name, Station.Lbl(s.Name), s.Label }))
            .Concat(L.Switches.Values.SelectMany(s => new[] { s.Num, Station.Lbl(s.Num), s.Label }))
            .Concat(L.Sections.Keys.SelectMany(s => new[] { s, Station.Lbl(s) }))
            .Concat(L.Derailers.Values.SelectMany(d => new[] { d.Name, Station.Lbl(d.Name), d.Label }))
            .Concat(L.TrackEnds.Values.SelectMany(t => new[] { t.Id, t.Disp }));
        return all.Where(x => !string.IsNullOrEmpty(x) && x.Contains(' ')).Select(x => Fold(string.Join(' ', x.Split(' ', StringSplitOptions.RemoveEmptyEntries)))).ToHashSet();
    }

    /// <summary>Sklejenie sąsiednich słów polecenia w jeden adres, gdy tworzą nazwę obiektu ze spacją („W 101 Wbl”).</summary>
    List<string> JoinNames(List<string> toks)
    {
        if (toks.Count < 2) return toks;
        var names = MultiWordNames(); if (names.Count == 0) return toks;
        var res = new List<string>();
        for (int i = 0; i < toks.Count;)
        {
            int take = 1;
            for (int k = Math.Min(4, toks.Count - i); k >= 2; k--)
                if (names.Contains(Fold(string.Join(' ', toks.Skip(i).Take(k))))) { take = k; break; }
            res.Add(string.Join(' ', toks.Skip(i).Take(take))); i += take;
        }
        return res;
    }

    // ------------------------------------------------------------------ wykonanie
    public CmdResult Exec(string line, bool confirm = false)
    {
        line = string.Join(' ', (line ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (line == "") return new(false, "");
        if (LinkDown) return new(false, "Brak łączności z komputerem zależności – polecenie nie zostało przyjęte");
        var toks = line.Split(' ').ToList();
        int ci = toks.FindIndex(t => CodeOf(t) != null);
        if (ci < 0) return Reject(line, "Brak kodu polecenia (Ie-104 § 12) – np. „E S1 Pociąg”, „13 Minus”, „S1 SZ”; POMOC – lista");
        var code = CodeOf(toks[ci]);
        var orig = Fold(toks[ci]);
        var args = JoinNames(toks.Take(ci).ToList()).Concat(JoinNames(toks.Skip(ci + 1).ToList())).ToList();
        if (code == "KW") { foreach (var x in Alarms) x.Ack = true; return new(true, "Alarmy potwierdzone"); }
        if (User == null) return Reject(line, "Brak zalogowanego użytkownika – polecenia zablokowane (Ie-20 §7 ust. 2)");
        if (Pending != null && !confirm && Pending.Desk != null && CurDesk != null && Pending.Desk != CurDesk.Id && Codes[code].Special) return Reject(line, "Inne stanowisko ma zainicjowane polecenie specjalne – spróbuj za chwilę");
        if (Pending != null && !confirm && (Pending.Desk == null || CurDesk == null || Pending.Desk == CurDesk.Id)) return Reject(line, $"Zainicjowano polecenie specjalne {Pending.Code} {Station.Lbl(Pending.Arg)} – potwierdź lub wycofaj (Ie-104 § 11 ust. 16)");
        // dawne skróty z parametrem (ZB n +, BK szlak)
        if (orig == "ZB" && args.Count > 1) { code = args[1] == "-" ? "MINBZ" : "PLUSBZ"; args = args.Take(1).ToList(); }
        if (orig == "BK") code = "WBL";
        if (orig == "STW") return RunAllStop(line);

        var info = Codes[code];
        if (CheckControl(code, args) is string ctlErr) return Reject(line, ctlErr);
        if (info.Special && !confirm)
        {
            var (err, target, targets) = SpecialPrecheck(code, args);
            if (err != null) return Reject(line, err);
            Pending = new Pending { Code = code, Arg = target, Line = line, Until = T + Cfg.SpecialConfirm, NotBefore = T + Cfg.SpecialMinWait, Notes = SpecialNotes(code), Desc = info.Desc, Targets = targets, Desk = CurDesk?.Id };
            Log("Polecenie", "Zainicjowano polecenie specjalne: " + line, "Polecenie", target, "PC");
            return new(true, "Polecenie specjalne zainicjowane – sprawdź adres i kod, potwierdź (nie wcześniej niż po 5 s)");
        }
        Stats.Cmds++;
        var r = Run(code, args);
        if (r.Ok)
        {
            Log("Polecenie", line + (r.Msg != "" ? " – " + r.Msg : ""), "Polecenie", args.FirstOrDefault() ?? "", "PC");
            if (info.Special) { Stats.Special++; BookDrafts.Add(new Pending { Code = code, Arg = Pending?.Arg ?? args.FirstOrDefault(), Line = line, Until = T, Desc = info.Desc, Desk = CurDesk?.Id }); SpecialExecuted?.Invoke(line); }
        }
        else { Stats.Rejected++; Log("Odrzucone", line + " – " + r.Msg, "Polecenie", "", "PC"); }
        return r;
    }

    CmdResult Reject(string line, string msg) { Stats.Rejected++; Log("Odrzucone", line + " – " + msg, "Polecenie", "", "PC"); return new(false, msg); }

    public CmdResult ConfirmPending(string typed)
    {
        var p = Pending; if (p == null) return new(false, "Brak polecenia do potwierdzenia");
        if (T > p.Until) { Pending = null; return Reject(p.Line, "Upłynął czas na potwierdzenie"); }
        if (T < p.NotBefore) return new(false, $"Potwierdzenie możliwe po upływie 5 s od zainicjowania (Ie-104 § 11 ust. 13) – jeszcze {p.NotBefore - T:0} s");
        var lbl = Station.Lbl(p.Arg);
        var tf = Fold((typed ?? "").Trim());
        if (tf != Fold(lbl) && tf != Fold(p.Arg ?? "") && tf != Fold(L.Signals.TryGetValue(p.Arg ?? "", out var sg) ? sg.Label : "#"))
            return new(false, $"Wpisany adres nie zgadza się z adresem polecenia ({lbl})");
        Pending = null;
        return Exec(p.Line, true);
    }

    public void CancelPending() { if (Pending != null) { Log("Polecenie", "Wycofano polecenie specjalne: " + Pending.Line, "Polecenie", Pending.Arg, "PC"); Pending = null; } }

    (string Err, string Target, List<string> Targets) SpecialPrecheck(string code, List<string> args)
    {
        string a0 = args.FirstOrDefault();
        switch (code)
        {
            case "SZ":
            {
                var s = Resolve(a0, "sig");
                if (s == null) return ($"Nieznany sygnalizator „{a0}”", null, null);
                if (!L.Signals[s].IsTrain) return ("Sygnał zastępczy możliwy tylko na semaforze", null, null);
                if (Sigs[s].Aspect == Aspect.Train) return ("Semafor wyświetla sygnał zezwalający", null, null);
                if (Sigs[s].Flank.Count > 0) return ($"Semafor utwierdzony w ochronie bocznej przebiegu {FlankOwner(Sigs[s].Flank, null)}", null, null);
                return (null, s, new() { s });
            }
            case "ZDP":
            {
                var s = Resolve(a0, "sig");
                if (s == null) return ($"Nieznany sygnalizator „{a0}”", null, null);
                var r = Routes.Values.FirstOrDefault(x => x.Start == s && x.Type == "P");
                return r == null ? ("Brak przebiegu pociągowego od " + Station.Lbl(s), null, null) : (null, s, new List<string> { s }.Concat(r.Secs).ToList());
            }
            case "PLUSBZ":
            case "MINBZ":
            {
                var w = Resolve(a0, "sw");
                if (w == null) return ($"Nieznana zwrotnica „{a0}”", null, null);
                if (Sws[w].Lock != null) return ("Zwrotnica utwierdzona w przebiegu", null, null);
                if (Sws[w].Flank.Count > 0) return ($"Zwrotnica utwierdzona w ochronie bocznej przebiegu {FlankOwner(Sws[w].Flank, null)}", null, null);
                if (Sws[w].Stopped) return ("Zwrotnica zastopowana", null, null);
                return (null, w, new() { w, L.Switches[w].Sec });
            }
            case "KSR":
            {
                var w = Resolve(a0, "sw");
                if (w == null) return ($"Nieznana zwrotnica „{a0}”", null, null);
                return Sws[w].Rozpr ? (null, w, new() { w }) : ($"Zwrotnica {Station.Lbl(w)} nie sygnalizuje rozprucia", null, null);
            }
            case "ZEROLO":
            {
                var sc = Resolve(a0, "sec", "sw");
                if (sc != null && L.Switches.ContainsKey(sc)) sc = L.Switches[sc].Sec;
                if (sc == null) return ($"Nieznany odcinek „{a0}”", null, null);
                if (!L.Sections[sc].AxleCounter) return ($"Odcinek {Station.Lbl(sc)} jest kontrolowany obwodem torowym – zerowanie licznika osi go nie dotyczy (zajętość ustępuje samoczynnie po usunięciu przyczyny)", null, null);
                return Secs[sc].Fault is "reset" or "occ" ? (null, sc, L.Switches.Values.Where(x => x.Sec == sc).Select(x => x.Num).Append(sc).ToList()) : ($"Odcinek {Station.Lbl(sc)} nie wymaga zerowania", null, null);
            }
            case "AZK":
            {
                var b = Resolve(a0, "blk");
                return b == null ? ($"Nieznana blokada „{a0}”", null, null) : (null, b, new() { b });
            }
            case "NN":
                return Power.NN ? ("Napięcie nastawcze jest załączone", null, null) : (null, "NN", new());
        }
        return (null, a0, new());
    }

    static List<string> SpecialNotes(string code) => code switch
    {
        "SZ" => new() {
            "Sprawdź na monitorze położenie zwrotnic i wykolejnic w drodze przebiegu (Ie-20 §27 ust. 1 pkt 3).",
            "Zwrotnice w drodze przebiegu zabezpiecz: Stop (zastopowanie), przebieg manewrowy w kierunku przeciwnym lub oNN (§18 ust. 2, §27 ust. 1).",
            "Przy wyjeździe: kierunek blokady WYJAZD i wolny pierwszy odstęp (§27 ust. 1 pkt 4).",
            "Użycie odnotuj w książce przebiegów i książce kontroli urządzeń (§17 ust. 7 pkt 5)." },
        "ZDP" => new() { "Upewnij się, że pociąg nie zbliża się do semafora albo z sygnałami końca minął miejsce przebiegowe (§18 ust. 4, §25 ust. 2).", "Odnotuj w książce kontroli urządzeń." },
        "PLUSBZ" or "MINBZ" => new() { "Przekonaj się, że zwrotnica nie jest zajęta i nic się do niej nie zbliża (§15 ust. 3).", "Wymagany zapis w książce kontroli urządzeń i powiadomienie automatyka." },
        "KSR" => new() { "Sprawdź w terenie iglice, zamknięcia nastawcze, pręt nastawczy i pręty kontrolne (§22 ust. 3 pkt 1).", "Zamknij iglicę odlegającą zamkiem zapasowym; jazdy pociągowe max 40 km/h (§22 ust. 3 pkt 3, 7)." },
        "ZEROLO" => new() { "Stwierdź, że na odcinku nie ma pojazdu kolejowego.", "Po zerowaniu odcinek oczekuje na pierwszy przejazd – jazda na sygnał zastępczy lub rozkaz pisemny." },
        "NN" => new() { "Upewnij się, że pracownicy utrzymania nie pracują przy napędach zwrotnicowych (§13 ust. 10)." },
        "AZK" => new() { "Normalna zmiana kierunku jest niemożliwa (§20 ust. 5 pkt 1).", "Szlak pomiędzy posterunkami jest wolny od pojazdów (§20 ust. 5 pkt 2).", "AZK wydaje posterunek, który będzie przyjmował pociągi (kierunek PRZYJAZD)." },
        _ => new()
    };

    CmdResult RunAllStop(string line)
    {
        foreach (var (n, x) in Sigs)
            if (L.Signals[n].Type != "odstepowy" && x.Aspect is Aspect.Train or Aspect.Shunt) { x.Aspect = Aspect.Stop; if (x.Route != null && Routes.TryGetValue(x.Route, out var q)) q.State = "stopped"; }
        Log("Polecenie", line + " – wszystkie semafory „Stój”", "Polecenie", "", "PC");
        return new(true, "wszystkie semafory – „Stój”");
    }

    CmdResult Run(string code, List<string> args)
    {
        string a0 = args.FirstOrDefault();
        switch (code)
        {
            case "POCIAG":
            case "MANEWR":
                return SetRouteCmd(code == "POCIAG" ? "P" : "M", args);
            case "STOJ":
            {
                var s = Resolve(a0, "sig"); if (s == null) return new(false, $"Nieznany sygnalizator „{a0}”");
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
                var k = Resolve(a0, "sig", "sw", "blk");
                if (k == null) return new(false, $"Nieznany obiekt „{a0}”");
                if (L.Signals.ContainsKey(k))
                {
                    Sigs[k].Closed = on;
                    if (on && Sigs[k].Aspect is Aspect.Train or Aspect.Shunt) { Sigs[k].Aspect = Aspect.Stop; if (Sigs[k].Route != null && Routes.TryGetValue(Sigs[k].Route, out var q)) q.State = "stopped"; }
                    return new(true, on ? "sygnalizator zastopowany" : "odwołano zastopowanie sygnalizatora");
                }
                if (L.Switches.ContainsKey(k)) { Sws[k].Stopped = on; return new(true, on ? "zwrotnica zastopowana" : "odwołano zastopowanie zwrotnicy"); }
                return BlockCmd(code, k, EndFor(L.Blocks[k], args));
            }
            case "OSZ":
            {
                var s = Resolve(a0, "sig");
                if (s == null || Sigs[s].Aspect != Aspect.Sz) return new(false, $"Na {a0} nie jest wyświetlany sygnał zastępczy");
                Sigs[s].Aspect = Aspect.Stop; Sigs[s].SzUntil = 0; return new(true, "");
            }
            case "ZDM":
            {
                var s = Resolve(a0, "sig"); if (s == null) return new(false, $"Nieznany sygnalizator „{a0}”");
                var r = Routes.Values.FirstOrDefault(x => x.Start == s && x.Type == "M");
                if (r == null) return new(false, "Brak przebiegu manewrowego od " + Station.Lbl(s));
                ReleaseRoute(r.Uid, "ZDM"); return new(true, "przebieg manewrowy zwolniony");
            }
            case "ZCZ":
            {
                var s = Resolve(a0, "sig"); if (s == null) return new(false, $"Nieznany sygnalizator „{a0}”");
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
                var s = Resolve(a0, "sig"); if (s == null) return new(false, $"Nieznany sygnalizator „{a0}”");
                var r = Routes.Values.FirstOrDefault(x => x.Start == s && x.TimeRelUntil != null);
                if (r == null) return new(false, "Brak odliczania czasu zwolnienia od " + Station.Lbl(s));
                r.TimeRelUntil = null; foreach (var sc in r.Secs) if (Secs[sc].Lock == r.Uid) Secs[sc].TimeRel = null;
                return new(true, "przerwano odliczanie – przebieg utwierdzony");
            }
            case "PLUS":
            case "MINUS":
            {
                var k = Resolve(a0, "sw", "der");
                if (k == null) return new(false, $"Nieznana zwrotnica / wykolejnica „{a0}”");
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
                var k = Resolve(a0, "sec", "sw");
                if (k == null) return new(false, $"Nieznany tor / zwrotnica „{a0}”");
                if (L.Switches.ContainsKey(k)) k = L.Switches[k].Sec;
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
                var b = Resolve(a0, "blk");
                if (b == null) return new(false, $"Nieznana blokada liniowa „{a0}” (np. 2A, 1F, 2T)");
                return BlockCmd(code, b, EndFor(L.Blocks[b], args));
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
                var s = Resolve(a0, "sig");
                var st = Sigs[s]; st.Aspect = Aspect.Sz; st.SzUntil = T + Cfg.SzTime; st.FailAlarm = false;
                CheckSzPractice(s);
                return new(true, $"sygnał zastępczy ({Cfg.SzTime:0} s)");
            }
            case "ZDP":
            {
                var s = Resolve(a0, "sig");
                var r = Routes.Values.First(x => x.Start == s && x.Type == "P");
                if (Sigs[s].Aspect != Aspect.Sz) Sigs[s].Aspect = Aspect.Stop;
                ReleaseRoute(r.Uid, "ZDP"); return new(true, "przebieg pociągowy zwolniony natychmiast");
            }
            case "PLUSBZ":
            case "MINBZ":
            {
                var w = Resolve(a0, "sw");
                if (!Power.NN) return new(false, "Napięcie nastawcze wyłączone");
                MoveSwitch(w, code == "PLUSBZ" ? "+" : "-"); return new(true, "przestawienie z bocznikowaniem");
            }
            case "KSR":
            {
                // brak kontroli trwa dalej (iglice uszkodzone) – KZ zgłosi go w następnym cyklu jako brak kontroli, nie rozprucie
                var k = Resolve(a0, "sw"); var w = Sws[k]; w.Rozpr = false; w.KsrDone = true;
                return new(true, "skasowano sygnalizację rozprucia");
            }
            case "ZEROLO":
            {
                var sc = Resolve(a0, "sec", "sw"); if (L.Switches.ContainsKey(sc)) sc = L.Switches[sc].Sec;
                Urzadzenia.Zeruj(sc); return new(true, "odcinek oczekuje na pierwszy przejazd");
            }
        }
        return new(false, $"Polecenie „{code}” nieobsługiwane");
    }

    /// <summary>
    /// Nastawienie przebiegu: adresy „początek [punkty pośrednie] koniec”. Punkt pośredni (zwrotnica, np. 13 lub 13-, odcinek, sygnalizator)
    /// wybiera przebieg wariantowy. Przebieg manewrowy może być złożony z kolejnych przebiegów elementarnych (przez tarcze).
    /// </summary>
    CmdResult SetRouteCmd(string type, List<string> args)
    {
        if (args.Count < 2) return new(false, "Podaj sygnalizator początkowy i koniec przebiegu");
        var start = Resolve(args[0], "sig");
        if (start == null) return new(false, $"Nieznany sygnalizator początkowy „{args[0]}”");
        var endTok = args[^1];
        var end = Resolve(endTok, "sig", "blk", "end");
        if (end == null) return new(false, $"Nieznany koniec przebiegu „{endTok}”");
        var via = new List<string>();
        foreach (var t in args.Skip(1).Take(args.Count - 2))
        {
            var pos = t.EndsWith("+") || t.EndsWith("-") ? t[^1..] : "";
            var core = pos == "" ? t : t[..^1];
            var k = Resolve(core, "sw", "sig", "sec");
            if (k == null) return new(false, $"Nieznany punkt pośredni „{t}”");
            via.Add(L.Switches.ContainsKey(k) && pos != "" ? k + pos : k);
        }
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

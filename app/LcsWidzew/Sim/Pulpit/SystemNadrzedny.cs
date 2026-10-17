using System.Globalization;
using System.Text;
using LcsWidzew.Model;

namespace LcsWidzew.Sim;

public record CmdResult(bool Ok, string Msg);

/// <summary>
/// Polecenie systemowe (PODZIAL_MODULOW.md 3.1): kod Ie-104 i adresy rozwiązane przez system nadrzędny w kontekście obrazu stanowiska.
/// Obiekty – klucze obiektów (przebieg: początek, punkty pośrednie, koniec); Koniec – koniec szlaku wskazany w poleceniu blokady;
/// Arg0 – pierwszy adres tak, jak go wpisano (do komunikatów).
/// </summary>
public record PolecenieSystemowe(string Kod, IReadOnlyList<string> Obiekty, int Koniec, string Arg0);

/// <summary>
/// System nadrzędny stanowisk (EbiScreen, PODZIAL_MODULOW.md krok 9): interpreter poleceń (kody wg Ie-104 § 12; składnia
/// „adres(y) kod” – Ie-104 § 11 ust. 7 – lub „kod adres(y)” jak w EbiScreen), adresy w kontekście wyświetlanego obrazu
/// (Ie-104 § 11 ust. 6), polecenia dwuetapowe (Ie-104 § 11 ust. 13–16), stanowiska i obszary sterowania, książka E-1758.
/// Komputer zależności dostaje gotowe <see cref="PolecenieSystemowe"/> i sam sprawdza warunki zależności.
/// </summary>
public class SystemNadrzedny
{
    readonly Simulator S;
    Station L => S.L;
    double T => S.T;

    /// <summary>Aktualnie wyświetlany obraz (kontekst adresów poleceń, Ie-104 § 11 ust. 6).</summary>
    public string Page;
    /// <summary>Zalogowany użytkownik (stanowisko główne / wykonywane polecenie).</summary>
    public User User;
    /// <summary>Polecenie specjalne zainicjowane, czeka na potwierdzenie.</summary>
    public Pending Pending;
    public List<BookEntry> Book = new();
    public List<Pending> BookDrafts = new();
    public Dictionary<string, Desk> Desks = new();
    /// <summary>Obszary sterowania: posterunek → stanowisko, które nim steruje (null – nieobsadzony).</summary>
    public Dictionary<string, string> Control = new();
    public List<ControlRequest> ControlReqs = new();
    /// <summary>Stanowisko, od którego pochodzi wykonywane polecenie (null – wewnętrzne / test).</summary>
    public Desk CurDesk;

    /// <summary>PIP – numery pociągów na pulpicie.</summary>
    public readonly Pip Pip;

    public SystemNadrzedny(Simulator s) { S = s; Page = s.L.Pages[^1].Id; Pip = new Pip(s); }

    /// <summary>Cykl: wygaśnięcie niepotwierdzonego polecenia specjalnego.</summary>
    public void Tick()
    {
        if (Pending != null && T > Pending.Until) { S.Log("Odrzucone", "Upłynął czas potwierdzenia polecenia " + Pending.Line, "Polecenie"); Pending = null; S.ZglosWygasniecie(); }
    }

    // ------------------------------------------------------------------ kody poleceń
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

    static readonly HashSet<string> BlockCodes = new() { "WBL", "BKON", "PO", "POZ", "DPO", "DKO", "OWBL", "PZK", "ZWBL", "AZK" };

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
        return S.Kz.EndIdx(b, Page);
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

    /// <summary>
    /// Adresy polecenia → <see cref="PolecenieSystemowe"/> (klucze obiektów, koniec szlaku). Błąd – adres nieznany na obrazie.
    /// </summary>
    (PolecenieSystemowe P, string Err) Adresy(string code, List<string> args)
    {
        string a0 = args.FirstOrDefault();
        PolecenieSystemowe P(params string[] k) => new(code, k, 0, a0);
        switch (code)
        {
            case "POCIAG":
            case "MANEWR":
            {
                if (args.Count < 2) return (null, "Podaj sygnalizator początkowy i koniec przebiegu");
                var start = Resolve(args[0], "sig");
                if (start == null) return (null, $"Nieznany sygnalizator początkowy „{args[0]}”");
                var endTok = args[^1];
                var end = Resolve(endTok, "sig", "blk", "end");
                if (end == null) return (null, $"Nieznany koniec przebiegu „{endTok}”");
                var obj = new List<string> { start };
                foreach (var t in args.Skip(1).Take(args.Count - 2))
                {
                    var pos = t.EndsWith("+") || t.EndsWith("-") ? t[^1..] : "";
                    var core = pos == "" ? t : t[..^1];
                    var k = Resolve(core, "sw", "sig", "sec");
                    if (k == null) return (null, $"Nieznany punkt pośredni „{t}”");
                    obj.Add(L.Switches.ContainsKey(k) && pos != "" ? k + pos : k);
                }
                obj.Add(end);
                return (P(obj.ToArray()), null);
            }
            case "STOJ": case "ZDM": case "ZCZ": case "OZCZ": case "SZ": case "ZDP":
            {
                var s = Resolve(a0, "sig");
                return s == null ? (null, $"Nieznany sygnalizator „{a0}”") : (P(s), null);
            }
            case "OSZ":
            {
                var s = Resolve(a0, "sig");
                return s == null ? (null, $"Na {a0} nie jest wyświetlany sygnał zastępczy") : (P(s), null);
            }
            case "STOP":
            case "OSTOP":
            {
                var k = Resolve(a0, "sig", "sw", "blk");
                if (k == null) return (null, $"Nieznany obiekt „{a0}”");
                return (L.Blocks.TryGetValue(k, out var b) ? new(code, new[] { k }, EndFor(b, args), a0) : P(k), null);
            }
            case "PLUS":
            case "MINUS":
            {
                var k = Resolve(a0, "sw", "der");
                return k == null ? (null, $"Nieznana zwrotnica / wykolejnica „{a0}”") : (P(k), null);
            }
            case "PLUSBZ": case "MINBZ": case "KSR":
            {
                var w = Resolve(a0, "sw");
                return w == null ? (null, $"Nieznana zwrotnica „{a0}”") : (P(w), null);
            }
            case "ZMK":
            case "OZMK":
            {
                var k = Resolve(a0, "sec", "sw");
                if (k == null) return (null, $"Nieznany tor / zwrotnica „{a0}”");
                return (P(L.Switches.ContainsKey(k) ? L.Switches[k].Sec : k), null);
            }
            case "ZEROLO":
            {
                var sc = Resolve(a0, "sec", "sw");
                if (sc != null && L.Switches.ContainsKey(sc)) sc = L.Switches[sc].Sec;
                return sc == null ? (null, $"Nieznany odcinek „{a0}”") : (P(sc), null);
            }
        }
        if (BlockCodes.Contains(code))
        {
            var b = Resolve(a0, "blk");
            if (b == null) return (null, $"Nieznana blokada liniowa „{a0}” (np. 2A, 1F, 2T)");
            return (new(code, new[] { b }, EndFor(L.Blocks[b], args), a0), null);
        }
        return (P(), null);
    }

    // ------------------------------------------------------------------ wykonanie
    public CmdResult Exec(string line, bool confirm = false)
    {
        line = string.Join(' ', (line ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (line == "") return new(false, "");
        if (S.LinkDown) return new(false, "Brak łączności z komputerem zależności – polecenie nie zostało przyjęte");
        var toks = line.Split(' ').ToList();
        int ci = toks.FindIndex(t => CodeOf(t) != null);
        if (ci < 0) return Reject(line, "Brak kodu polecenia (Ie-104 § 12) – np. „E S1 Pociąg”, „13 Minus”, „S1 SZ”; POMOC – lista");
        var code = CodeOf(toks[ci]);
        var orig = Fold(toks[ci]);
        var args = JoinNames(toks.Take(ci).ToList()).Concat(JoinNames(toks.Skip(ci + 1).ToList())).ToList();
        if (code == "KW") { foreach (var x in S.Alarms) x.Ack = true; return new(true, "Alarmy potwierdzone"); }
        if (User == null) return Reject(line, "Brak zalogowanego użytkownika – polecenia zablokowane (Ie-20 §7 ust. 2)");
        if (Pending != null && !confirm && Pending.Desk != null && CurDesk != null && Pending.Desk != CurDesk.Id && Codes[code].Special) return Reject(line, "Inne stanowisko ma zainicjowane polecenie specjalne – spróbuj za chwilę");
        if (Pending != null && !confirm && (Pending.Desk == null || CurDesk == null || Pending.Desk == CurDesk.Id)) return Reject(line, $"Zainicjowano polecenie specjalne {Pending.Code} {Station.Lbl(Pending.Arg)} – potwierdź lub wycofaj (Ie-104 § 11 ust. 16)");
        // dawne skróty z parametrem (ZB n +, BK szlak)
        if (orig == "ZB" && args.Count > 1) { code = args[1] == "-" ? "MINBZ" : "PLUSBZ"; args = args.Take(1).ToList(); }
        if (orig == "BK") code = "WBL";
        if (orig == "STW")
        {
            var all = S.Kz.WszystkieStoj();
            S.Log("Polecenie", line + " – wszystkie semafory „Stój”", "Polecenie", "", "PC");
            return all;
        }

        var info = Codes[code];
        if (CheckControl(code, args) is string ctlErr) return Reject(line, ctlErr);
        if (info.Special && !confirm)
        {
            var (err, target, targets) = SpecialPrecheck(code, args);
            if (err != null) return Reject(line, err);
            Pending = new Pending { Code = code, Arg = target, Line = line, Until = T + Simulator.Cfg.SpecialConfirm, NotBefore = T + Simulator.Cfg.SpecialMinWait, Notes = SpecialNotes(code), Desc = info.Desc, Targets = targets, Desk = CurDesk?.Id };
            S.Log("Polecenie", "Zainicjowano polecenie specjalne: " + line, "Polecenie", target, "PC");
            return new(true, "Polecenie specjalne zainicjowane – sprawdź adres i kod, potwierdź (nie wcześniej niż po 5 s)");
        }
        S.Stats.Cmds++;
        var (p, adrErr) = Adresy(code, args);
        var r = adrErr != null ? new CmdResult(false, adrErr) : S.Kz.Wykonaj(p);
        if (r.Ok)
        {
            S.Log("Polecenie", line + (r.Msg != "" ? " – " + r.Msg : ""), "Polecenie", args.FirstOrDefault() ?? "", "PC");
            if (info.Special) { S.Stats.Special++; BookDrafts.Add(new Pending { Code = code, Arg = Pending?.Arg ?? args.FirstOrDefault(), Line = line, Until = T, Desc = info.Desc, Desk = CurDesk?.Id }); S.ZglosWykonanieSpecjalnego(line); }
        }
        else { S.Stats.Rejected++; S.Log("Odrzucone", line + " – " + r.Msg, "Polecenie", "", "PC"); }
        return r;
    }

    CmdResult Reject(string line, string msg) { S.Stats.Rejected++; S.Log("Odrzucone", line + " – " + msg, "Polecenie", "", "PC"); return new(false, msg); }

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

    public void CancelPending() { if (Pending != null) { S.Log("Polecenie", "Wycofano polecenie specjalne: " + Pending.Line, "Polecenie", Pending.Arg, "PC"); Pending = null; } }

    /// <summary>Polecenie specjalne – pierwszy etap: adres (system nadrzędny) i warunki wstępne (komputer zależności).</summary>
    (string Err, string Target, List<string> Targets) SpecialPrecheck(string code, List<string> args)
    {
        string a0 = args.FirstOrDefault();
        string key;
        switch (code)
        {
            case "SZ":
            case "ZDP":
                key = Resolve(a0, "sig"); if (key == null) return ($"Nieznany sygnalizator „{a0}”", null, null); break;
            case "PLUSBZ":
            case "MINBZ":
            case "KSR":
                key = Resolve(a0, "sw"); if (key == null) return ($"Nieznana zwrotnica „{a0}”", null, null); break;
            case "ZEROLO":
                key = Resolve(a0, "sec", "sw");
                if (key != null && L.Switches.ContainsKey(key)) key = L.Switches[key].Sec;
                if (key == null) return ($"Nieznany odcinek „{a0}”", null, null);
                break;
            case "AZK":
                key = Resolve(a0, "blk"); if (key == null) return ($"Nieznana blokada „{a0}”", null, null); break;
            case "NN":
                key = null; break;
            default:
                return (null, a0, new());
        }
        return S.Kz.SprawdzSpecjalne(code, key);
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

    // ------------------------------------------------------------------ stanowiska i obszary sterowania
    public Desk AddDesk(string id, string name, bool host)
    {
        var d = new Desk { Id = id, Name = name, Host = host, Page = Page };
        Desks[id] = d;
        foreach (var p in L.Posts.Keys) if (!Control.TryGetValue(p, out var o) || o == null || (host && !Desks.ContainsKey(o))) Control[p] = host ? id : Control.GetValueOrDefault(p);
        S.Log("Zdarzenie", $"Przyłączono stanowisko {name}", "Stanowisko", "");
        return d;
    }

    public void RemoveDesk(string id)
    {
        if (!Desks.Remove(id, out var d)) return;
        var host = Desks.Values.FirstOrDefault(x => x.Host);
        foreach (var p in Control.Where(x => x.Value == id).Select(x => x.Key).ToList())
        {
            Control[p] = host?.Id;
            S.Log("Zdarzenie", $"Stanowisko {d.Name} rozłączone – sterowanie posterunkiem {PostName(p)} przechodzi na {host?.Name ?? "brak obsługi"}", "Stanowisko", "");
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
    public void Trace(string s) { LinkTrace.Add($"{S.Clock()}  {s}"); if (LinkTrace.Count > 400) LinkTrace.RemoveRange(0, 100); }
    public CmdResult ConfirmAs(Desk d, string typed) => As(d, () => Pending != null && Pending.Desk != null && Pending.Desk != d.Id ? new CmdResult(false, "Polecenie specjalne zainicjowało inne stanowisko") : ConfirmPending(typed));
    public void CancelAs(Desk d) => As(d, () => { if (Pending != null && (Pending.Desk == null || Pending.Desk == d.Id)) CancelPending(); return new CmdResult(true, ""); });

    T As<T>(Desk d, Func<T> f)
    {
        var (u, p, c) = (User, Page, CurDesk);
        User = d.User; Page = d.Page ?? Page; CurDesk = d;
        try { return f(); }
        finally { User = u; Page = p; CurDesk = c; }
    }

    public string RequestControl(Desk d, string post)
    {
        if (!L.Posts.ContainsKey(post)) return "Nieznany posterunek";
        var owner = Control.GetValueOrDefault(post);
        if (owner == d.Id) return "Stanowisko już steruje tym posterunkiem";
        if (owner == null || !Desks.ContainsKey(owner))
        {
            Control[post] = d.Id;
            As(d, () => S.Log("Zdarzenie", $"Przejęto sterowanie posterunkiem {PostName(post)} (nieobsadzony) – stanowisko {d.Name}", "Stanowisko", ""));
            return null;
        }
        if (ControlReqs.Any(r => r.Post == post && r.From == d.Id)) return "Żądanie już wysłane – czeka na zgodę";
        ControlReqs.Add(new ControlRequest { Post = post, From = d.Id, To = owner, At = T });
        As(d, () => S.Log("Zdarzenie", $"Żądanie przekazania sterowania posterunkiem {PostName(post)}: {d.Name} → od {DeskName(owner)}", "Stanowisko", ""));
        return null;
    }

    public void AnswerControl(Desk d, string post, bool yes)
    {
        var r = ControlReqs.FirstOrDefault(x => x.Post == post && x.To == d.Id); if (r == null) return;
        ControlReqs.Remove(r);
        if (yes && Control.GetValueOrDefault(post) == d.Id)
        {
            Control[post] = r.From;
            As(d, () => S.Log("Zdarzenie", $"Przekazano sterowanie posterunkiem {PostName(post)}: {d.Name} → {DeskName(r.From)}", "Stanowisko", ""));
        }
        else As(d, () => S.Log("Zdarzenie", $"Odmowa przekazania sterowania posterunkiem {PostName(post)} dla {DeskName(r.From)}", "Stanowisko", ""));
    }

    public void ReleaseControl(Desk d, string post)
    {
        if (Control.GetValueOrDefault(post) != d.Id) return;
        var host = Desks.Values.FirstOrDefault(x => x.Host && x.Id != d.Id);
        Control[post] = host?.Id;
        As(d, () => S.Log("Zdarzenie", $"Oddano sterowanie posterunkiem {PostName(post)} – przejmuje {host?.Name ?? "brak obsługi"}", "Stanowisko", ""));
    }

    /// <summary>Łączność między stanowiskami (telefon) – zapis w rejestratorze.</summary>
    public void Phone(Desk from, string toDesk, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var to = toDesk == null || toDesk == "*" ? "wszystkie stanowiska" : DeskName(toDesk);
        As(from, () => S.Log("Telefon", $"{from.Name} → {to}: {text.Trim()}", "Łączność", ""));
    }

    public void Login(Desk d, User u)
    {
        d.User = u;
        As(d, () => S.Log("Zdarzenie", u != null ? $"Zalogowano: {u.Name} (stanowisko {d.Name})" : $"Wylogowano (stanowisko {d.Name})", "Użytkownik", u?.Name ?? ""));
        if (d.Host) User = u;
    }

    // ------------------------------------------------------------------ książka kontroli urządzeń E-1758
    public BookData BookFor(Desk d)
    {
        var dr = BookDrafts.FirstOrDefault(x => x.Desk == null || x.Desk == d?.Id);
        return new BookData
        {
            Entries = Book.AsEnumerable().Reverse().ToList(), Drafts = BookDrafts.Count(x => x.Desk == null || x.Desk == d?.Id),
            Draft = dr == null ? null : $"{S.Clock(dr.Until)} – użyto polecenia specjalnego: {dr.Desc} ({dr.Line}).\r\nOkoliczności / przyczyna: \r\nWprowadzone obostrzenia: "
        };
    }

    public void AddBook(Desk d, string text, string auto)
    {
        As(d, () =>
        {
            var dr = BookDrafts.FirstOrDefault(x => x.Desk == null || x.Desk == d?.Id); if (dr != null) BookDrafts.Remove(dr);
            Book.Add(new BookEntry { Czas = S.Clock(), Uzytkownik = User?.Name ?? "-", Tresc = text.Trim(), Automatyk = auto.Trim(), ForCode = dr?.Code });
            S.Log("E-1758", text.Trim().Split('\n')[0], "Książka", "E-1758");
            if (dr != null && auto.Trim() == "") S.Remark($"Zapis po poleceniu {dr.Line} bez informacji o powiadomieniu automatyka (§14 ust. 4)");
            return 0;
        });
    }
}

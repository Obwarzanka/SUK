using System.IO;
using System.Text;
using LcsWidzew.Model;

namespace LcsWidzew.Sim;

/// <summary>Test bez interfejsu: LcsWidzew.exe --selftest [plik_wyjściowy]</summary>
public static class SelfTest
{
    public static void Run(string stationPath, string outPath, SimulatorOptions options = null)
    {
        var sb = new StringBuilder();
        var sw0 = System.Diagnostics.Stopwatch.StartNew();
        void Prog(string m) => File.AppendAllText(outPath + ".postep", $"{sw0.Elapsed.TotalSeconds:0.0}s {m}{(char)10}");
        Prog("start");
        var st = Station.Load(stationPath); Prog("wczytano");
        var sim = new Simulator(st, options: options); Prog("tablica przebiegów");
        var tr = new Trains(sim);
        sb.AppendLine($"Węzły {st.Nodes.Count}, krawędzie {st.Edges.Count}, zwrotnice {st.Switches.Count}, semafory {st.Signals.Count}, odcinki {st.Sections.Count}");
        foreach (var e in st.Edges.Values) if (Math.Abs(st.Nodes[e.A].X - st.Nodes[e.B].X) < 0.5) sb.AppendLine("UWAGA krawędź pionowa: " + e.Id + " " + e.Sec);
        int n = 0;
        foreach (var (k, v) in sim.RouteTable.OrderBy(x => x.Key))
        {
            n += v.Count;
            sb.AppendLine($"{k,-10} " + string.Join(", ", v.Select(r => r.Type + "→" + r.End + "[" + string.Join(" ", r.Sw.Select(q => q.Num + q.Pos)) + "]")));
        }
        sb.AppendLine("Przebiegów: " + n);

        sim.User = new User { Name = "test", Role = "dyżurny ruchu" };
        tr.Load("podstawowy");
        // Scenariusz z tablicy przebiegów wczytanego układu (bez nazw konkretnej stacji):
        // dla każdego szlaku jeden przebieg wyjazdowy (z Wbl / Pzk), dla każdego semafora wjazdowego jeden wjazd, jeden manewr.
        string PageOf(string key) { var p = Station.PostOf(key); return st.Pages.FirstOrDefault(x => x.Posts.Contains(p))?.Id ?? sim.Page; }
        CmdResult Ex(string page, string c, bool log = true)
        {
            sim.Page = page; var r = sim.Exec(c);
            if (log) sb.AppendLine($"{sim.Clock()} [{page}] > {c}: {(r.Ok ? "OK" : "ODRZ")} {r.Msg}");
            return r;
        }
        string Cmd(RouteDef r) => $"{r.Start} {r.End} {(r.Type == "M" ? "Manewr" : "Pociąg")}";
        var basic = sim.RouteTable.Values.SelectMany(v => v).Where(r => r.Variant == 0).ToList();
        var todo = new List<RouteDef>();
        foreach (var g in basic.Where(r => r.Type == "P" && r.EndKind == "szlak").GroupBy(r => r.End).OrderBy(g => g.Key)) todo.Add(g.OrderBy(r => r.Start).First());
        foreach (var g in basic.Where(r => r.Type == "P" && r.EndKind != "szlak" && st.Signals.TryGetValue(r.Start, out var s) && s.Type == "wjazdowy").GroupBy(r => r.Start).OrderBy(g => g.Key)) todo.Add(g.OrderBy(r => r.End).First());
        var man = basic.Where(r => r.Type == "M" && st.Signals.TryGetValue(r.Start, out var s) && s.Type != "wjazdowy").OrderBy(r => r.Id).FirstOrDefault(); if (man != null) todo.Add(man);
        sb.AppendLine($"Scenariusz: {todo.Count} przebiegów do nastawienia (szlaki {todo.Count(r => r.EndKind == "szlak")}, wjazdy {todo.Count(r => r.Type == "P" && r.EndKind != "szlak")}, manewry {(man != null ? 1 : 0)})");
        var done = new HashSet<RouteDef>(); var ok = new HashSet<string>(); var failed = new Dictionary<RouteDef, string>(); var lastWbl = new Dictionary<string, double>();
        bool conflictChecked = false; int conflictOk = 0, conflictTotal = 0;
        var naruszenia = new Dictionary<string, string>(); int kontroli = 0;
        int pipOk = 0, pipTot = 0, pipObce = 0; var pipBledy = new List<string>();
        for (int i = 0; i < 4 * 900; i++)
        {
            sim.Tick(0.25);
            if (i % 8 == 0)
            {
                // kontrola zależności na żywo (niezmienniki KZ) – co 2 s scenariusza
                kontroli++; foreach (var k in KzPodglad.Kontrola(sim, out _).Where(k => k.Poziom == "naruszenie")) naruszenia.TryAdd(k.Tekst, sim.Clock());
                // PIP (system nadrzędny, po zajętościach) a położenie pociągów w terenie
                var okna = sim.Nadrzedny.Pip.Okna();
                foreach (var t in tr.List.Where(t => t.Window != null))
                {
                    pipTot++;
                    if (okna.GetValueOrDefault(t.Window)?.Contains(t.Num) == true) pipOk++;
                    else if (pipBledy.Count < 12) pipBledy.Add($"{sim.Clock()} {t.Num}: pociąg na {t.Window}, PIP {string.Join(", ", sim.Nadrzedny.Pip.Numery.Where(x => x.Nr == t.Num).Select(x => $"czoło {x.Czolo}, okno {x.Okno ?? "–"}").DefaultIfEmpty("brak numeru"))}");
                }
                pipObce += okna.Values.SelectMany(x => x).Count(n => !tr.List.Any(t => t.Num == n && t.Window != null));
            }
            if (i % 20 != 0) continue;
            // posterunki sąsiednie i drugi koniec szlaku wewnętrznego dają pozwolenie
            foreach (var (id, bs) in sim.Blocks)
            {
                if (bs.Req == null) continue;
                var b = st.Blocks[id];
                if (bs.Req.EndIdx == -1) Ex(PageOf(b.Ends[0].Post + ":"), id + " Pzk");
                else if (b.Internal) Ex(PageOf(b.Ends[1 - bs.Req.EndIdx].Post + ":"), id + " Pzk");
            }
            // przebieg nastawiony (sygnał zezwalający) – sprawdzony, zwolnij go, żeby zrobić miejsce kolejnym
            foreach (var ru in sim.Routes.Values.Where(x => x.State == "set" && todo.Any(r => r.Id == x.Id) && !ok.Contains(x.Id)).ToList())
            {
                ok.Add(ru.Id);
                sb.AppendLine($"{sim.Clock()} nastawiony {ru.Id}, sygnał {sim.Sigs[ru.Start].Aspect}");
                sim.Page = PageOf(ru.Start);
                var rr = sim.Exec($"{ru.Start} {(ru.Type == "M" ? "ZDM" : "ZDP")}", true);
                if (!rr.Ok) sb.AppendLine($"   zwolnienie {ru.Id}: ODRZ {rr.Msg}");
            }
            foreach (var r in todo.Where(r => !done.Contains(r)).ToList())
            {
                if (failed.ContainsKey(r) || sim.Sigs[r.Start].Route != null) continue;
                var page = PageOf(r.Start);
                if (r.EndKind == "szlak" && !sim.BlockAllowsExit(r.End, st.Signals[r.Start].Dir))
                {
                    if (sim.T - lastWbl.GetValueOrDefault(r.End, -1e9) > 60)
                    {
                        lastWbl[r.End] = sim.T;
                        var w = Ex(page, $"{r.End} Wbl {(st.Signals[r.Start].Dir > 0 ? "E" : "W")}");
                        if (!w.Ok && (w.Msg.StartsWith("Nieznan") || st.Blocks[r.End].Ends.Count == 1 && st.Blocks[r.End].Type is "sbl1" or "pol1")) failed[r] = "Wbl: " + w.Msg;
                    }
                    continue;
                }
                if (sim.WhyNot(r).Count > 0) continue;   // poczekaj, aż droga się zwolni
                var res = Ex(page, Cmd(r));
                if (res.Ok) done.Add(r); else if (res.Msg.StartsWith("Nieznan")) failed[r] = res.Msg;
            }
            // kontrola zależności: drugi przebieg z sygnalizatora będącego już początkiem przebiegu musi zostać odrzucony
            if (!conflictChecked && done.Count > 0)
            {
                conflictChecked = true;
                foreach (var d in done.ToList())
                {
                    var other = basic.FirstOrDefault(x => x.Start == d.Start && x.Id != d.Id && x.Type == d.Type);
                    if (other == null) continue;
                    conflictTotal++;
                    var r = Ex(PageOf(other.Start), Cmd(other), false);
                    sb.AppendLine($"{sim.Clock()} kontrola: {Cmd(other)} przy nastawionym {d.Id}: {(r.Ok ? "PRZYJĘTE – BŁĄD" : "odrzucone (dobrze)")} {r.Msg}");
                    if (!r.Ok) conflictOk++;
                }
            }
            if (i % 200 == 0) Prog($"krok {i} T={sim.T:0} pociągi {tr.List.Count} nastawione {done.Count}/{todo.Count}");
        }
        Prog("koniec pętli");
        sb.AppendLine($"WYNIK: nastawione {ok.Count} z {todo.Count} przebiegów; kontrola zależności {conflictOk} z {conflictTotal}");
        sb.AppendLine($"KONTROLA KZ NA ŻYWO: {kontroli} kontroli w trakcie scenariusza, naruszeń: {naruszenia.Count}");
        foreach (var (t, c) in naruszenia) sb.AppendLine($"   naruszenie {c}: {t}");
        sb.AppendLine($"PIP: numer w oknie pociągu {pipOk} z {pipTot} kontroli, numery bez pociągu w oknie: {pipObce}");
        foreach (var x in pipBledy) sb.AppendLine("   PIP " + x);
        {
            // kontrola musi wykrywać naruszenia: celowo zepsuty stan KZ (osobny symulator)
            var ns = new Simulator(st, options: options) { User = sim.User }; int wyk = 0;
            var sg = st.Signals.Values.First(x => x.Type == "wyjazdowy").Name;
            ns.Sigs[sg].Aspect = Aspect.Train;
            if (KzPodglad.Kontrola(ns, out _).Any(k => k.Poziom == "naruszenie" && k.Tekst.Contains("bez nastawionego przebiegu"))) wyk++;
            ns.Sigs[sg].Aspect = Aspect.Stop;
            var pr = ns.RouteTable.Values.SelectMany(v => v).FirstOrDefault(r => r.Type == "P" && r.Sw.Count > 0 && ns.WhyNot(r).Count == 0);
            if (pr != null)
            {
                ns.Page = PageOf(pr.Start); ns.Exec(Cmd(pr), false);
                for (int k = 0; k < 160 && !ns.Routes.Values.Any(x => x.Id == pr.Id && x.State == "set"); k++) ns.Tick(0.25);
                var (num, pos) = pr.Sw[0]; ns.Sws[num].Pos = pos == "+" ? "-" : "+";     // „zgubione” położenie zwrotnicy w KZ
                if (KzPodglad.Kontrola(ns, out _).Any(k => k.Poziom == "naruszenie" && k.Tekst.Contains(Station.Lbl(num)))) wyk++;
            }
            sb.AppendLine($"KONTROLA KZ – wykrywanie celowych naruszeń: {wyk} z 2");
        }
        // ochrona boczna (poradnik UTK 1.5.8, Tabela 22) – osobny symulator bez pociągów
        {
            var fs = new Simulator(st, options: options) { User = sim.User };
            var all = fs.RouteTable.Values.SelectMany(v => v).ToList();
            int withFlank = all.Count(r => r.Flank.Count > 0), fOk = 0, fTot = 0;
            void F(bool c, string m) { fTot++; if (c) fOk++; sb.AppendLine($"ochrona boczna: {(c ? "OK  " : "BŁĄD")} {m}"); }
            var fr = all.Where(r => r.Variant == 0 && r.Type == "P" && r.EndKind == "sig" && r.Flank.Any(f => f.Kind == "sw")).FirstOrDefault(r => fs.WhyNot(r).Count == 0);
            if (fr != null)
            {
                var f = fr.Flank.First(x => x.Kind == "sw");
                fs.Page = PageOf(fr.Start); var res = fs.Exec(Cmd(fr));
                for (int k = 0; k < 160 && !fs.Routes.Values.Any(x => x.Id == fr.Id && x.State == "set"); k++) fs.Tick(0.25);
                sb.AppendLine($"ochrona boczna: przebieg {fr.Id}: {(res.Ok ? "OK" : "ODRZ " + res.Msg)}, ochrona: {fr.FlankText}");
                F(fs.Sws[f.Id].Pos == f.Pos && fs.Sws[f.Id].Flank.Count == 1 && fs.Routes.Values.Any(x => x.Id == fr.Id && x.State == "set"), $"zwr. {Station.Lbl(f.Id)} przestawiona do „{f.Pos}” i utwierdzona w ochronie, sygnał zezwalający");
                fs.Page = PageOf(f.Id); var mv = fs.Exec($"{f.Id} {(f.Pos == "+" ? "Minus" : "Plus")}");
                F(!mv.Ok, $"przestawienie zwrotnicy ochronnej odrzucone: {mv.Msg}");
                var r2 = all.FirstOrDefault(r => r.Sw.Any(q => q.Num == f.Id && q.Pos != f.Pos));
                if (r2 != null) F(fs.WhyNot(r2).Any(x => x.Contains("ochronie bocznej")), $"przebieg {r2.Id} (zwr. {Station.Lbl(f.Id)} w drugim położeniu) zablokowany ochroną boczną");
                fs.Page = PageOf(fr.Start); fs.Exec($"{fr.Start} ZDP", true); fs.Tick(0.25);
                F(fs.Sws[f.Id].Flank.Count == 0, "po zwolnieniu przebiegu zwrotnica ochronna wolna");
            }
            sb.AppendLine($"OCHRONA BOCZNA: przebiegi z ochroną {withFlank} z {all.Count}; kontrola {fOk} z {fTot}");
        }
        // zależności specjalne (<układ>.zaleznosci.json) – wpisy testowe w pamięci, dobrane z tablicy przebiegów (bez nazw stacji)
        {
            var zalOrig = st.Zal;
            int zOk = 0, zTot = 0;
            void Z(bool c, string m) { zTot++; if (c) zOk++; sb.AppendLine($"zależności specjalne: {(c ? "OK  " : "BŁĄD")} {m}"); }
            try
            {
                st.Zal = new Zaleznosci();
                var ps = new Simulator(st, options: options) { User = sim.User };
                var all = ps.RouteTable.Values.SelectMany(v => v).Where(r => r.Variant == 0 && r.Type == "P").ToList();
                // przebieg z drogą ochronną, w której odcinku jest zwrotnica spoza przebiegu i jego ochrony bocznej
                RouteDef zr = null; string ovNum = null, ovPos = null;
                foreach (var r in all.Where(r => r.EndKind == "sig" && r.Overlap != null && ps.WhyNot(r).Count == 0))
                {
                    // zwrotnica w ochronie bocznej – położenie ochronne; inna – przestawienie do drugiego położenia
                    // najlepiej zwrotnica w odcinku drogi ochronnej; gdy takiej nie ma (kopia testowa) – zwrotnica tego posterunku spoza przebiegu
                    var w = st.Switches.Values.FirstOrDefault(x => x.Sec == r.Overlap && !r.Sw.Any(q => q.Num == x.Num))
                         ?? st.Switches.Values.FirstOrDefault(x => Station.PostOf(x.Num) == Station.PostOf(r.Start) && !r.Sw.Any(q => q.Num == x.Num) && !r.Secs.Contains(x.Sec) && x.Sec != r.Overlap && !x.Manual);
                    if (w == null) continue;
                    var fl = r.Flank.FirstOrDefault(f => f.Kind == "sw" && f.Id == w.Num);
                    zr = r; ovNum = w.Num; ovPos = fl.Id != null ? fl.Pos : ps.Sws[w.Num].Pos == "+" ? "-" : "+"; break;
                }
                if (zr == null) sb.AppendLine("zależności specjalne: brak przebiegu z drogą ochronną – bez kontroli");
                else
                {
                    var used = zr.Secs.Append(zr.Overlap).ToHashSet();
                    var free = all.Where(r => r.Start != zr.Start && r.End != zr.End && !r.Secs.Any(used.Contains) && r.Overlap != zr.Overlap && ps.WhyNot(r).Count == 0).ToList();
                    var r2 = free.FirstOrDefault();
                    var r3 = free.Skip(1).FirstOrDefault();
                    var cw = r3 == null ? null : st.Switches.Values.FirstOrDefault(x => !r3.Sw.Any(q => q.Num == x.Num) && !r3.Flank.Any(f => f.Id == x.Num) && x.Num != ovNum);
                    var z = new ZalPrzebieg { Start = zr.Start, End = zr.End, Type = "P", DrogaOchronna = { new ZalOchronna { Odcinki = { zr.Overlap }, Zwrotnice = { ovNum + ovPos } } } };
                    st.Zal.Przebiegi.Add(z);
                    if (r2 != null) st.Zal.Przebiegi.Add(new ZalPrzebieg { Start = r2.Start, End = r2.End, Type = "P", Sprzeczne = { zr.Key } });
                    if (cw != null) st.Zal.Przebiegi.Add(new ZalPrzebieg { Start = r3.Start, End = r3.End, Type = "P", Warunki = { cw.Num + (ps.Sws[cw.Num].Pos == "+" ? "-" : "+") } });
                    var uw = st.Zal.Sprawdz(st, ps.RouteTable.Values.SelectMany(v => v).Select(r => r.Key));
                    Z(uw.Count == 0, "kontrola danych bez uwag" + (uw.Count > 0 ? ": " + string.Join("; ", uw) : ""));
                    var zs = new Simulator(st, options: options) { User = sim.User };
                    var zd = zs.FindRoute(zr.Start, zr.End, "P");
                    Z(zd != null && zd.OverlapAlts.Count == 1, $"{zr.Id}: droga ochronna z zależności – {zr.Overlap}, zwr. {Station.Lbl(ovNum)}{ovPos}");
                    zs.Page = PageOf(zr.Start); var res = zs.Exec(Cmd(zr));
                    for (int k = 0; k < 160 && !zs.Routes.Values.Any(x => x.Id == zr.Id && x.State == "set"); k++) zs.Tick(0.25);
                    var act = zs.Routes.Values.FirstOrDefault(x => x.Id == zr.Id);
                    Z(res.Ok && act != null && act.State == "set" && zs.Sws[ovNum].Pos == ovPos && zs.Sws[ovNum].Ov.Contains(act.Uid), $"zwr. {Station.Lbl(ovNum)} przestawiona do „{ovPos}” i utwierdzona w drodze ochronnej, sygnał zezwalający");
                    zs.Page = PageOf(ovNum); var mv = zs.Exec($"{ovNum} {(ovPos == "+" ? "Minus" : "Plus")}");
                    Z(!mv.Ok, $"przestawienie zwrotnicy drogi ochronnej odrzucone: {mv.Msg}");
                    if (r2 != null) Z(zs.WhyNot(zs.FindRoute(r2.Start, r2.End, "P")).Any(x => x.Contains("przebieg sprzeczny")), $"{r2.Id} sprzeczny z {zr.Id} (wpis przy {r2.Id}) – odrzucony");
                    if (cw != null) Z(zs.WhyNot(zs.FindRoute(r3.Start, r3.End, "P")).Any(x => x.Contains("warunek specjalny")), $"{r3.Id}: warunek specjalny niespełniony – odrzucony");
                    // przebieg odrzucany tylko z powodu odcinka drogi ochronnej – po wpisie „dopuszczone” możliwy jednocześnie
                    var r4 = all.FirstOrDefault(r => r.Id != zr.Id && zs.WhyNot(r) is var w && w.Count > 0 && w.All(x => x.Contains(zr.Overlap) && x.Contains("utwierdzon")));
                    zs.Page = PageOf(zr.Start); zs.Exec($"{zr.Start} ZDP", true); zs.Tick(0.25);
                    Z(zs.Sws[ovNum].Ov.Count == 0 && zs.Secs[zr.Overlap].Lock == null, "po zwolnieniu przebiegu zwrotnica i odcinek drogi ochronnej wolne");
                    if (r4 != null)
                    {
                        z.Dopuszczone.Add(r4.Key);
                        var ds = new Simulator(st, options: options) { User = sim.User };
                        ds.Page = PageOf(zr.Start); ds.Exec(Cmd(zr));
                        for (int k = 0; k < 160 && !ds.Routes.Values.Any(x => x.Id == zr.Id && x.State == "set"); k++) ds.Tick(0.25);
                        var w4 = ds.WhyNot(r4);
                        Z(w4.Count == 0, $"{r4.Id} dopuszczony jednocześnie z {zr.Id} mimo wspólnego odcinka {Station.Lbl(zr.Overlap)}" + (w4.Count > 0 ? ": " + w4[0] : ""));
                    }
                    else sb.AppendLine("zależności specjalne: brak przebiegu blokowanego tylko drogą ochronną – bez kontroli „dopuszczone”");
                }
            }
            finally { st.Zal = zalOrig; }
            sb.AppendLine($"ZALEŻNOŚCI SPECJALNE: kontrola {zOk} z {zTot}");
        }
        // karty zwrotnic (PODZIAL_MODULOW krok 5): brak kontroli, rozprucie przy zajętości, KSR, nadzór czasu – osobny symulator bez pociągów
        {
            var ks = new Simulator(st, options: options) { User = sim.User };
            int kOk = 0, kTot = 0;
            void K(bool c, string m) { kTot++; if (c) kOk++; sb.AppendLine($"karty: {(c ? "OK  " : "BŁĄD")} {m}"); }
            void Run(double s) { for (double t = 0; t < s; t += 0.25) ks.Tick(0.25); }
            var sws = st.Switches.Values.Where(x => !x.Manual).Take(2).Select(x => x.Num).ToList();
            // trzecia (zamarznięcie) – w innym odcinku niż druga (jej odcinek test zajmuje)
            var swc = st.Switches.Values.FirstOrDefault(x => !x.Manual && sws.Count == 2 && !sws.Contains(x.Num) && x.Sec != st.Switches[sws[1]].Sec);
            if (swc != null) sws.Add(swc.Num);
            if (sws.Count == 3)
            {
                string a = sws[0], b = sws[1], c = sws[2]; var wa = ks.Sws[a]; var wb = ks.Sws[b]; var wc = ks.Sws[c];
                // brak kontroli przy wolnym odcinku
                ks.Instruktor.ApplyFault("swRozpr", a, false); Run(0.5);
                K(wa.NoCtrl && !wa.Rozpr, $"zwr. {Station.Lbl(a)}: uszkodzenie iglic przy wolnym odcinku – brak kontroli, bez rozprucia");
                ks.Instruktor.ApplyFault("swRozpr", a, true); Run(0.5);
                K(!wa.NoCtrl && !wa.Rozpr, $"zwr. {Station.Lbl(a)}: po naprawie kontrola położenia „{wa.Pos}”");
                // najechanie od gałęzi przy iglicach w drugim położeniu i zajętym odcinku – rozprucie, KSR, brak kontroli
                var sec = st.Switches[b].Sec; var other = wb.Pos == "+" ? "-" : "+";
                ks.Instruktor.ApplyFault("secOcc", sec, false); ks.Urzadzenia.Najazd(b, other); Run(0.5);
                K(wb.Rozpr && !wb.NoCtrl, $"zwr. {Station.Lbl(b)}: najechanie od gałęzi „{other}” przy zajętym odcinku – rozprucie");
                ks.Page = PageOf(b); var ksr = ks.Exec($"{b} KSR", true); Run(0.5);
                K(ksr.Ok && !wb.Rozpr && wb.NoCtrl, $"zwr. {Station.Lbl(b)}: po KSR – brak kontroli (iglice uszkodzone), nie rozprucie");
                ks.Instruktor.ApplyFault("swRozpr", b, true); Run(0.5);
                K(!wb.NoCtrl && wb.Pos == other && ks.Urzadzenia.Iglice(b) == other, $"zwr. {Station.Lbl(b)}: po naprawie kontrola w położeniu „{other}”, w którym zostały iglice");
                // zamarznięcie: przekroczony czas, powrót do położenia wyjściowego
                var p0 = wc.Pos; var p1 = p0 == "+" ? "-" : "+";
                ks.Instruktor.ApplyFault("swFrozen", c, false); ks.Page = PageOf(c);
                var mv = ks.Exec($"{c} {(p1 == "+" ? "Plus" : "Minus")}"); Run(Simulator.Cfg.SwTimeout + 2);
                K(mv.Ok && wc.Moving != null && wc.Frozen && ks.Alarms.Any(x => x.Obiekt == Station.Lbl(c) && x.Zdarzenie.Contains("zbyt długo")), $"zwr. {Station.Lbl(c)}: zamarznięta – przekroczony czas przekładania, alarm{(mv.Ok ? "" : " (" + mv.Msg + ")")}");
                ks.Exec($"{c} {(p0 == "+" ? "Plus" : "Minus")}"); Run(Simulator.Cfg.SwTime + 1);
                K(wc.Moving == null && wc.Pos == p0 && !wc.NoCtrl, $"zwr. {Station.Lbl(c)}: powrót do położenia wyjściowego „{p0}” z kontrolą");
                // zwykłe przestawienie: kontrola dopiero po czasie przekładania (meldunek karty)
                ks.Instruktor.ApplyFault("swFrozen", c, true); ks.Exec($"{c} {(p1 == "+" ? "Plus" : "Minus")}"); Run(Simulator.Cfg.SwTime - 1);
                bool during = wc.Moving != null && wc.Pos == p0; Run(2);
                K(during && wc.Moving == null && wc.Pos == p1, $"zwr. {Station.Lbl(c)}: przestawienie do „{p1}” – kontrola po czasie przekładania");
            }
            sb.AppendLine($"KARTY ZWROTNIC: kontrola {kOk} z {kTot}");
        }
        // zasilanie jako urządzenie (krok 7): NN0 zatrzymuje napęd, zanik sieci – alarm i samoczynny agregat, agregat uszkodzony – AGR
        {
            var zs = new Simulator(st, options: options) { User = sim.User };
            int zOk = 0, zTot = 0;
            void Z(bool c, string m) { zTot++; if (c) zOk++; sb.AppendLine($"zasilanie: {(c ? "OK  " : "BŁĄD")} {m}"); }
            void Run(double s) { for (double t = 0; t < s; t += 0.25) zs.Tick(0.25); }
            var z = zs.Urzadzenia.Zasilanie;
            var sw = st.Switches.Values.FirstOrDefault(x => !x.Manual)?.Num;
            if (sw != null)
            {
                var w = zs.Sws[sw]; var p0 = w.Pos; var p1 = p0 == "+" ? "-" : "+"; var ruch = p1 == "+" ? "Plus" : "Minus";
                zs.Page = PageOf(sw);
                var mv = zs.Exec($"{sw} {ruch}"); Run(0.5);
                var onn = zs.Exec("ONN", true); Run(Simulator.Cfg.SwTime + 1);
                Z(mv.Ok && onn.Ok && !z.NN && !zs.Power.NN && w.Moving != null && zs.Urzadzenia.Iglice(sw) == p0, $"NN0 w czasie przekładania {Station.Lbl(sw)} – napęd stoi, iglice „{p0}”{(onn.Ok ? "" : " (" + onn.Msg + ")")}");
                var odrz = zs.Exec($"{sw} {(p0 == "+" ? "Plus" : "Minus")}");
                Z(!odrz.Ok && odrz.Msg.Contains("nastawcze"), $"polecenie przestawienia przy NN0 odrzucone: {odrz.Msg}");
                var nn = zs.Exec("NN", true); Run(Simulator.Cfg.SwTime + 1);
                Z(nn.Ok && z.NN && zs.Power.NN && w.Moving == null && w.Pos == p1, $"NN – napęd kończy przekładanie, kontrola „{p1}”{(nn.Ok ? "" : " (" + nn.Msg + ")")}");
            }
            zs.Instruktor.ApplyFault("mains", null, false); Run(1);
            Z(!zs.Power.Mains && zs.Alarms.Any(a => a.Zdarzenie.Contains("praca z UPS")) && !z.Agregat, "zanik sieci – alarm, praca z UPS");
            Run(Zasilanie.CzasRozruchu + 1);
            Z(z.Agregat && zs.Power.Gen, "agregat uruchomiony samoczynnie");
            zs.Instruktor.ApplyFault("mains", null, true); Run(1);
            Z(zs.Power.Mains && !zs.Power.Gen, "powrót sieci – agregat wyłączony");
            zs.Instruktor.ApplyFault("mainsGenFail", null, false); Run(Zasilanie.CzasRozruchu + 2);
            Z(!z.Agregat && zs.Alarms.Any(a => a.Zdarzenie.Contains("nie uruchomił")), "agregat uszkodzony – nie rusza samoczynnie, alarm");
            var agr = zs.Exec("AGR", true); Run(0.5);
            Z(agr.Ok && z.Agregat && zs.Power.Gen, $"AGR – agregat uruchomiony poleceniem{(agr.Ok ? "" : " (" + agr.Msg + ")")}");
            sb.AppendLine($"ZASILANIE: kontrola {zOk} z {zTot}");
        }
        // sygnały wg Ie-1 (krok 6): uzależnienie od następnego semafora, siatka zał. 1, karta sygnalizatora
        {
            var gs = new Simulator(st, options: options) { User = sim.User };
            int sOk = 0, sTot = 0;
            void G(bool c, string m) { sTot++; if (c) sOk++; sb.AppendLine($"sygnały: {(c ? "OK  " : "BŁĄD")} {m}"); }
            void Run(double s) { for (double t = 0; t < s; t += 0.25) gs.Tick(0.25); }
            bool Set(RouteDef r) { for (int k = 0; k < 160 && !gs.Routes.Values.Any(x => x.Id == r.Id && x.State == "set"); k++) gs.Tick(0.25); Run(0.5); return gs.Routes.Values.Any(x => x.Id == r.Id && x.State == "set"); }
            string Widz(string sg) => gs.Urzadzenia.SygnalWidziany(sg);
            var all = gs.RouteTable.Values.SelectMany(v => v).Where(r => r.Variant == 0 && r.Type == "P").ToList();
            // A → B (B semafor wyjazdowy z własnym przebiegiem pociągowym, który nie koliduje z A)
            (RouteDef A, RouteDef B) para = default;
            foreach (var a in all.Where(r => r.EndKind == "sig" && st.Signals[r.End].Type == "wyjazdowy" && gs.WhyNot(r).Count == 0))
            {
                var b = all.FirstOrDefault(r => r.Start == a.End && gs.WhyNot(r).Count == 0 && !r.Secs.Intersect(a.Secs).Any() && !r.Sw.Any(q => a.Sw.Any(p => p.Num == q.Num && p.Pos != q.Pos)));
                if (b != null) { para = (a, b); break; }
            }
            if (para.A != null)
            {
                var (A, B) = para;
                int kA = Sygnaly.Klasa(gs.SignalSpeed(A)), kB = Sygnaly.Klasa(gs.SignalSpeed(B));
                gs.Page = PageOf(A.Start); var ra = gs.Exec(Cmd(A)); bool setA = ra.Ok && Set(A);
                var a1 = Widz(A.Start);
                G(setA && a1 == Sygnaly.Dobierz(kA, "Stoj") && gs.Sigs[A.Start].Sig == a1,
                    $"{A.Id} (v {gs.SignalSpeed(A):0}), B={Station.Lbl(B.Start)} „Stój” → A wyświetla {a1} ({Sygnaly.Def(a1)?.OpisSwiatel()}), oczekiwane {Sygnaly.Dobierz(kA, "Stoj")}");
                gs.Page = PageOf(B.Start); var rb = gs.Exec(Cmd(B)); bool setB = rb.Ok && Set(B);
                var b1 = Widz(B.Start); var a2 = Widz(A.Start);
                G(setB && Sygnaly.Def(b1)?.V == kB && a2 == Sygnaly.Dobierz(kA, Sygnaly.Kolumna(b1)),
                    $"po otwarciu {B.Id}: B wyświetla {b1}, A zmienia się samoczynnie na {a2} (oczekiwane {Sygnaly.Dobierz(kA, Sygnaly.Kolumna(b1))}){(rb.Ok ? "" : " – " + rb.Msg)}");
                gs.Page = PageOf(B.Start); gs.Exec($"{B.Start} ZDP", true); Run(0.75);
                G(Widz(B.Start) == "S1" && Widz(A.Start) == a1, $"po zwolnieniu {B.Id}: B {Widz(B.Start)}, A wraca do {Widz(A.Start)}");
                gs.Page = PageOf(A.Start); gs.Exec($"{A.Start} ZDP", true); Run(0.75);
                // uszkodzenie światła zezwalającego: karta nie zapala sygnału, KZ zgłasza §26
                gs.Instruktor.ApplyFault("sigLamp", A.Start, false); Run(0.5);
                gs.Page = PageOf(A.Start); gs.Exec(Cmd(A)); for (int k = 0; k < 80; k++) gs.Tick(0.25);
                G(Widz(A.Start) == "S1" && gs.Alarms.Any(x => x.Obiekt == Station.Lbl(A.Start) && x.Zdarzenie.Contains("nie wyświetlił")),
                    $"{Station.Lbl(A.Start)} z uszkodzonym światłem zezwalającym: wyświetla {Widz(A.Start)}, alarm §26");
                gs.Instruktor.ApplyFault("sigLamp", A.Start, true); Run(1);
                G(Sygnaly.Def(Widz(A.Start))?.V != null, $"po naprawie {Station.Lbl(A.Start)} wyświetla {Widz(A.Start)}");
            }
            else sb.AppendLine("sygnały: brak pary przebiegów A → B w układzie");
            // siatka zał. 1: każda para (prędkość, następny) ma dokładnie jeden sygnał
            int siatka = 0; foreach (var k in new[] { 0, 100, 60, 40 }) foreach (var c in new[] { "Vmax", "100", "40/60", "Stoj" }) if (Sygnaly.Tab.Values.Count(d => d.V == k && d.Nast == c) == 1) siatka++;
            G(siatka == 16, $"tabela Ie-1 zał. 1: {siatka} z 16 pól siatki (S2–S13a)");
            // SBL 3- i 4-stawna: ciąg odstępowych w kierunku zasadniczym szlaku (Ie-1 § 4 ust. 13 pkt 3 lit. b – S3: dwa odstępy wolne)
            var blk = st.Blocks.Values.Where(b => b.Type == "sbl").Select(b => (B: b, Sigs: st.Signals.Values.Where(s => s.Szlak == b.Id && s.Type == "odstepowy" && s.Dir == b.DefDir).ToList()))
                .Where(x => x.Sigs.Count >= 2).OrderByDescending(x => x.Sigs.Count).FirstOrDefault();
            if (blk.B != null)
            {
                int asp0 = blk.B.Aspects;
                foreach (var stawn in new[] { 3, 4 })
                {
                    blk.B.Aspects = stawn;
                    var ss = new Simulator(st, options: options) { User = sim.User };
                    for (int k = 0; k < 4; k++) ss.Tick(0.25);
                    // kolejność od początku szlaku: odstępowy, za którym jest drugi, stoi wcześniej
                    var ciag = blk.Sigs.Select(s => s.Name).ToList();
                    var nastepny = ciag.ToDictionary(n => n, n => ciag.FirstOrDefault(m => m != n && ss.Urzadzenia.SygnalWidziany(m) != null && IsNext(ss, n, m)));
                    ciag = ciag.OrderByDescending(n => Depth(n, nastepny)).ToList();
                    var w1 = string.Join(" → ", ciag.Select(n => $"{Station.Lbl(n)} {ss.Urzadzenia.SygnalWidziany(n)}"));
                    var oczek = stawn == 4 ? ciag.Select((n, i) => i == ciag.Count - 1 ? "S5" : i == ciag.Count - 2 ? "S3" : "S2").ToList() : ciag.Select((n, i) => i == ciag.Count - 1 ? "S5" : "S2").ToList();
                    G(ciag.Select(n => ss.Urzadzenia.SygnalWidziany(n)).SequenceEqual(oczek), $"szlak {blk.B.Id}, SBL {stawn}-stawna, odstępy wolne: {w1} (oczekiwane {string.Join(" ", oczek)})");
                    if (stawn == 4)
                    {
                        // zajęty odstęp za ostatnim odstępowym: ostatni S1, przedostatni S5, wcześniejszy S3
                        var ost = st.Signals[ciag[^1]]; var nx = ss.Forward(ost.Node, ost.Dir, null).FirstOrDefault();
                        if (nx != null)
                        {
                            var sec = st.Edges[nx.Edge].Sec;
                            ss.Instruktor.ApplyFault("secOcc", sec, false); for (int k = 0; k < 4; k++) ss.Tick(0.25);
                            var w2 = string.Join(" → ", ciag.Select(n => $"{Station.Lbl(n)} {ss.Urzadzenia.SygnalWidziany(n)}"));
                            var oc2 = ciag.Select((n, i) => i == ciag.Count - 1 ? "S1" : i == ciag.Count - 2 ? "S5" : i == ciag.Count - 3 ? "S3" : "S2").ToList();
                            G(ciag.Select(n => ss.Urzadzenia.SygnalWidziany(n)).SequenceEqual(oc2), $"szlak {blk.B.Id}, SBL 4-stawna, odstęp {Station.Lbl(sec)} zajęty: {w2} (oczekiwane {string.Join(" ", oc2)})");
                        }
                    }
                }
                blk.B.Aspects = asp0;
            }
            static bool IsNext(Simulator s, string a, string b)
            {
                var d = s.L.Signals[a]; string node = d.Node, from = null;
                for (int k = 0; k < 200; k++)
                {
                    var nx = s.Forward(node, d.Dir, from).FirstOrDefault(); if (nx == null) return false;
                    node = s.L.Other(nx.Edge, node); from = nx.Edge;
                    var f = s.L.Nodes[node].Signals.FirstOrDefault(x => s.L.Signals[x].Dir == d.Dir && s.L.Signals[x].Type is "odstepowy" or "wjazdowy");
                    if (f != null) return f == b;
                }
                return false;
            }
            static int Depth(string n, Dictionary<string, string> nx) { int k = 0; while (nx.TryGetValue(n, out var m) && m != null && k < 50) { n = m; k++; } return k; }
            sb.AppendLine($"SYGNAŁY: kontrola {sOk} z {sTot}");
        }
        foreach (var r in todo.Where(r => !ok.Contains(r.Id))) sb.AppendLine($"NIE NASTAWIONO {r.Id} ({Cmd(r)}): " + (failed.TryGetValue(r, out var why) ? why : done.Contains(r) ? "przyjęty, ale nie doszedł do sygnału zezwalającego" : string.Join(", ", sim.WhyNot(r).Take(3))));
        foreach (var e in sim.Events.Where(e => e.Kategoria is "Ruch" or "Alarm" or "Uwaga")) sb.AppendLine($"{e.Czas} {e.Kategoria}: {e.Zdarzenie}");
        sb.AppendLine("Pociągi: " + string.Join("; ", tr.List.Select(t => $"{t.Num} {t.State} wait={t.WaitingAt}")));
        File.WriteAllText(outPath, sb.ToString());
        // pełny dziennik zdarzeń – porównanie „przed / po” przy przenoszeniu kodu między modułami
        File.WriteAllLines(outPath + ".zdarzenia", sim.Events.Select(e => $"{e.Czas}|{e.Kategoria}|{e.TypObiektu}|{e.Obiekt}|{e.Stacja}|{e.Zrodlo}|{e.Zdarzenie}"));
    }

    /// <summary>Przebieg scenariusza z automatycznym dyżurnym (test rozkładu): LcsWidzew.exe --selftest wynik.txt ic_05</summary>
    public static string RunScenario(string stationPath, string outPath, string key, string onlyNum = null, SimulatorOptions options = null)
    {
        var sb = new StringBuilder();
        var st = Station.Load(stationPath);
        var sim = new Simulator(st, options: options);
        var tr = new Trains(sim);
        sim.User = new User { Name = "auto", Role = "dyżurny ruchu" };
        tr.Load(key);
        if (onlyNum != null) sim.Rozklad.Queue = sim.Rozklad.Queue.Where(q => q.Num == onlyNum || q.Num == onlyNum + "M" || q.Duty?.Num == onlyNum).ToList();
        sb.AppendLine($"Scenariusz {key}: start {sim.Clock()}, w kolejce {sim.Rozklad.Queue.Count}");
        foreach (var q in sim.Rozklad.Queue) sb.AppendLine($"  {sim.Clock(q.T)} {q.Num,-8} {q.Cat,-5} {(q.From ?? q.StartSec),-9} → {q.To ?? "–",-4} {q.Track}");
        string PageOf(string key2) { var p = Station.PostOf(key2); return st.Pages.FirstOrDefault(x => x.Posts.Contains(p))?.Id ?? sim.Page; }
        CmdResult Ex(string page, string c)
        {
            sim.Page = page; var r = sim.Exec(c);
            if (r.Ok) sb.AppendLine($"{sim.Clock()} [{page}] {c}: {r.Msg}");
            var bid = st.Blocks.Keys.Where(k => c.StartsWith(k + " ")).OrderByDescending(k => k.Length).FirstOrDefault();   // nazwa szlaku może mieć spację
            if (r.Ok && c.Contains("Wbl") && bid != null)
            {
                var bd = st.Blocks[bid];
                sb.AppendLine($"     dbg {bid}: odstępy {string.Join(",", bd.SecsWE.Select(x => x + (sim.SecOcc(x) ? "*" : "")))}; pociągi: {string.Join(" ", tr.List.Where(t => t.Segs.Any(q => q.E != null && bd.SecsWE.Contains(st.Edges[q.E].Sec))).Select(t => t.Num))}");
            }
            return r;
        }
        int okRoutes = 0; var lastWbl = new Dictionary<string, double>(); double maxBok = 0, maxSt = 0, maxAll = 0;
        int pipOk = 0, pipTot = 0, pipObce = 0; var pipBledy = new List<string>();
        for (int i = 0; i < 3 * 3600 * 4; i++)
        {
            sim.Tick(0.25);
            foreach (var t in tr.List)
            {
                var hs = t.Segs.LastOrDefault(x => x.E != null); if (hs == null || t.Shunt) continue;
                var ed = st.Edges[hs.E]; var v = t.V * 3.6;
                maxAll = Math.Max(maxAll, v);
                if (ed.SwLimit > 0 && t.Segs.Where(x => x.E != null).All(x => st.Edges[x.E].SwLimit > 0 || true)) maxBok = Math.Max(maxBok, ed.SwLimit > 0 ? v : 0);
                if (!ed.Szlak && ed.SwLimit <= 0 && ed.VMax <= 0) maxSt = Math.Max(maxSt, v);
            }
            if (i % 8 == 0)
            {
                // PIP (po zajętościach) a położenie pociągów w terenie
                var okna = sim.Nadrzedny.Pip.Okna();
                foreach (var t in tr.List.Where(t => t.Window != null))
                {
                    pipTot++;
                    if (okna.GetValueOrDefault(t.Window)?.Contains(t.Num) == true) pipOk++;
                    else if (pipBledy.Count < 12) pipBledy.Add($"{sim.Clock()} {t.Num}: pociąg na {t.Window}, PIP {string.Join(", ", sim.Nadrzedny.Pip.Numery.Where(x => x.Nr == t.Num).Select(x => $"czoło {x.Czolo}, okno {x.Okno ?? "–"}").DefaultIfEmpty("brak numeru"))}");
                }
                pipObce += okna.Values.SelectMany(x => x).Count(n => !tr.List.Any(t => t.Num == n && t.Window != null));
            }
            if (i % 20 != 0) continue;
            // pozwolenia dla posterunków sąsiednich i drugiego końca szlaków wewnętrznych
            foreach (var (id, bs) in sim.Blocks)
            {
                if (bs.Req == null) continue;
                var b = st.Blocks[id];
                if (bs.Req.EndIdx == -1) Ex(PageOf(b.Ends[0].Post + ":"), id + " Pzk");
                else if (b.Internal) Ex(PageOf(b.Ends[1 - bs.Req.EndIdx].Post + ":"), id + " Pzk");
            }
            foreach (var t in tr.List.ToList())
            {
                if (t.Shunt && t.Duty != null && t.V < 0.05 && t.WaitingAt == null && t.WaitingSince(sim.T) > 10 && !sim.Routes.Values.Any(r => r.Type == "M" && r.Secs.Contains(st.Edges[t.Segs[^1].E ?? t.Segs[0].E].Sec)))
                {
                    // koniec przebiegu manewrowego (granica przy semaforze wjazdowym) – zmiana kierunku i jazda na tor peronowy
                    foreach (var bsig in tr.SignalsBehind(t))
                    {
                        var post = Station.PostOf(bsig);
                        var ends = t.Duty.Armed
                            ? st.Signals.Values.Where(x => x.Post == post && x.Type == "wyjazdowy" && x.Dir == st.Signals[bsig].Dir && x.ApproachSec != null && x.ApproachSec.Contains(":JT") && !x.ApproachSec.EndsWith("JT102") && !x.ApproachSec.EndsWith("JT104"))
                                .OrderBy(x => t.Duty.Track != null && x.ApproachSec == post + ":JT" + t.Duty.Track ? 0 : 1)
                            : new[] { "LW:P102", "LW:P104", "LW:N102", "LW:N104" }.Where(st.Signals.ContainsKey).Select(x => st.Signals[x]).Where(x => x.Dir == st.Signals[bsig].Dir);
                        bool done = false;
                        foreach (var en in ends) if (Ex(PageOf(bsig), $"{Station.Lbl(bsig)} {Station.Lbl(en.Name)} Manewr").Ok) { okRoutes++; done = true; break; }
                        if (done) break;
                    }
                    continue;
                }
                if (t.WaitingAt == null || t.V > 0.05 || t.State == "dwell" || sim.Sigs[t.WaitingAt].Route != null) continue;
                if (t.Shunt && t.Duty == null && !t.Loco) t.Duty = new UnitDuty { Num = null, Armed = false };   // test: skład po zakończeniu biegu – do bazy
                if (t.Shunt && t.Duty == null) continue;
                if (t.Shunt && t.Duty?.Armed == true && t.Segs.Any(x => x.E != null && st.Edges[x.E].Sec.Contains(":JT") && !st.Edges[x.E].Sec.EndsWith("JT102") && !st.Edges[x.E].Sec.EndsWith("JT104")) && t.Segs[^1].E != null && st.Edges[t.Segs[^1].E].Sec.Contains(":JT")) continue;
                var sig = t.WaitingAt; var page = PageOf(sig);
                if (t.Shunt && t.Duty != null && !t.Duty.Armed)
                {
                    bool okB = false;
                    foreach (var en in new[] { "LW:P102", "LW:P104", "LW:N102", "LW:N104" }.Where(x => st.Signals.ContainsKey(x) && st.Signals[x].Dir == st.Signals[sig].Dir))
                        if (Ex(page, $"{Station.Lbl(sig)} {Station.Lbl(en)} Manewr").Ok) { okRoutes++; okB = true; break; }
                    if (!okB)
                        foreach (var en in st.Signals.Values.Where(x => x.Post == Station.PostOf(sig) && x.Type == "wjazdowy" && x.Dir == -st.Signals[sig].Dir))
                            if (Ex(page, $"{Station.Lbl(sig)} {Station.Lbl(en.Name)} Manewr").Ok) { okRoutes++; break; }
                    continue;
                }
                var routes = sim.RouteTable.GetValueOrDefault(sig, new()).Where(r => r.Type == (t.Shunt ? "M" : "P") && r.Variant == 0).ToList();
                bool Baza(RouteDef r) => r.Secs.Any(x => x.EndsWith("JT102") || x.EndsWith("JT104"));
                if (t.Shunt)
                {
                    // manewr z bazy: przebieg (także złożony) do semafora wyjazdowego toru peronowego ŁW
                    var post = Station.PostOf(sig);
                    var ends = st.Signals.Values.Where(x => x.Post == post && x.Type == "wyjazdowy" && x.Dir == st.Signals[sig].Dir && x.ApproachSec != null && x.ApproachSec.Contains(":JT") && !x.ApproachSec.EndsWith("JT102") && !x.ApproachSec.EndsWith("JT104"))
                        .OrderBy(x => t.Duty?.Track != null && x.ApproachSec == post + ":JT" + t.Duty.Track ? 0 : 1);
                    bool ok = false;
                    foreach (var en in ends) if (Ex(page, $"{Station.Lbl(sig)} {Station.Lbl(en.Name)} Manewr").Ok) { okRoutes++; ok = true; break; }
                    if (!ok)
                        foreach (var en in st.Signals.Values.Where(x => x.Post == post && x.Type == "wjazdowy" && x.Dir == -st.Signals[sig].Dir).OrderBy(x => x.Dir > 0 ? 0 : 1))
                            if (Ex(page, $"{Station.Lbl(sig)} {Station.Lbl(en.Name)} Manewr").Ok) { okRoutes++; break; }
                    continue;
                }
                // szlak wewnętrzny – gdy szlak docelowy nie zaczyna się na tym posterunku albo pociąg jedzie do ŁN / ŁF
                bool toLcs = t.StopAt != null && (t.StopAt.Contains("LF") || t.StopAt.Contains("LN")) || (t.To == null && t.Term != null)
                    || (t.To != null && st.Blocks.TryGetValue(t.To, out var tob) && !tob.Ends.Any(x => x.Post == Station.PostOf(sig)));
                var cand = routes.Where(r => !Baza(r) && (r.EndKind != "szlak" || r.End == t.To || (st.Blocks[r.End].Internal && (toLcs || t.To == null)))).OrderBy(r => r.End == t.To ? 0 : r.EndKind == "szlak" ? 2 : 1).ToList();
                if (t.WaitingSince(sim.T) > 300 && !t.LoggedStuck) { t.LoggedStuck = true; sb.AppendLine($"{sim.Clock()} UTKNĄŁ {t.Num} przed {sig}: " + string.Join(" | ", cand.Select(r => r.Id + ": " + string.Join(",", sim.WhyNot(r).Take(2))))); }
                foreach (var r in cand)
                {
                    if (r.EndKind == "szlak" && !sim.BlockAllowsExit(r.End, st.Signals[sig].Dir))
                    {
                        // bez przełączania kierunku tam i z powrotem: kolejna zmiana najwcześniej po 2 min
                        if (sim.T - lastWbl.GetValueOrDefault(r.End, -1e9) > 120 && Ex(page, $"{r.End} Wbl {(st.Signals[sig].Dir > 0 ? "E" : "W")}").Ok) lastWbl[r.End] = sim.T;
                        continue;
                    }
                    var res = Ex(page, $"{Station.Lbl(sig)} {Station.Lbl(r.End)} {(t.Shunt ? "Manewr" : "Pociąg")}");
                    if (res.Ok) { okRoutes++; break; }
                }
            }
        }
        sb.AppendLine($"Prędkość czoła: na rozjazdach na bok max {maxBok:0} km/h (limit 40), na torach stacyjnych max {maxSt:0} km/h (limit 60), ogółem max {maxAll:0} km/h");
        sb.AppendLine($"Nastawione przebiegi: {okRoutes}, wjazdy {sim.Stats.TrainsIn}, wyjazdy {sim.Stats.TrainsOut}");
        foreach (var e in sim.Events.Where(e => e.Kategoria is "Ruch" or "Alarm" or "Uwaga")) sb.AppendLine($"{e.Czas} {e.Kategoria}: {e.Zdarzenie}");
        sb.AppendLine("Pozostałe: " + string.Join("; ", tr.List.Select(t => $"{t.Num} {t.Cat} {t.State} wait={t.WaitingAt}")));
        sb.AppendLine("Niewykonane z kolejki: " + string.Join(", ", sim.Rozklad.Queue.Where(q => !q.Done).Select(q => q.Num)));
        sb.AppendLine($"PIP: numer w oknie pociągu {pipOk} z {pipTot} kontroli, numery bez pociągu w oknie: {pipObce}");
        foreach (var x in pipBledy) sb.AppendLine("   PIP " + x);
        if (outPath != null)
        {
            File.WriteAllText(outPath, sb.ToString());
            // Ten sam pełny dziennik co dla Run; obejmuje również odrzucenia poleceń, nie tylko podsumowanie scenariusza.
            File.WriteAllLines(outPath + ".zdarzenia", sim.Events.Select(e => $"{e.Czas}|{e.Kategoria}|{e.TypObiektu}|{e.Obiekt}|{e.Stacja}|{e.Zrodlo}|{e.Zdarzenie}"));
        }
        if (onlyNum == null) return sb.ToString();
        var evs = sim.Events.Where(e => e.Kategoria == "Ruch").Select(e => e.Zdarzenie).ToList();
        var left = evs.LastOrDefault(e => e.Contains("opuścił obszar")); var parked = evs.LastOrDefault(e => e.Contains("odstawiony"));
        var rem = tr.List.Where(t => !t.Loco).Select(t => $"{t.Num} {t.State} czeka={Station.Lbl(t.WaitingAt)} na {Station.Lbl(t.Segs.LastOrDefault(x => x.E != null)?.E is string ee ? st.Edges[ee].Sec : "")}").ToList();
        return left != null ? "OK – " + left : parked != null && rem.Count == 0 ? "OK – " + parked : rem.Count == 0 ? "OK – bieg zakończony" : "STOI: " + string.Join("; ", rem) + " | ostatnie: " + string.Join(" / ", evs.TakeLast(2));
    }

    /// <summary>Każdy rodzaj biegu z rozkładu (wjazd, wyjazd, postoje, zmiany czoła, baza) osobno – jeden pociąg na pustym układzie.</summary>
    public static void RunPatterns(string stationPath, string outPath, string key, SimulatorOptions options = null)
    {
        var sb = new StringBuilder();
        var st = Station.Load(stationPath); var sim = new Simulator(st, options: options); var tr = new Trains(sim); tr.Load(key);
        string Pat(TimetableEntry q) => $"{q.Cat}|{q.From ?? q.StartSec?.Split(':')[0] + (q.Duty != null ? "-baza" : "")}|{q.To}|{string.Join(">", (q.Track ?? "").Split('→').Select(x => x.Trim().Split(' ').FirstOrDefault(y => y is "ŁW" or "ŁF" or "ŁN")))}|{(q.Term != null ? (q.Term.Reverse ? "rev" : "") + (q.Term.Odstaw ? "odst" : "") : "")}";
        var groups = sim.Rozklad.Queue.Where(q => !q.Loco || q.Duty != null).GroupBy(Pat).ToList();
        sb.AppendLine($"Rodzajów biegu: {groups.Count} (pociągów {sim.Rozklad.Queue.Count})");
        int ok = 0;
        foreach (var g in groups)
        {
            var q = g.First(); var num = q.Duty?.Num ?? q.Num;
            var res = RunScenario(stationPath, null, key, num, options);
            if (res.StartsWith("OK")) ok++;
            sb.AppendLine($"{(res.StartsWith("OK") ? "✓" : "✗")} [{g.Count(),2}×] {g.Key,-60} np. {num}: {res}");
        }
        sb.AppendLine($"Przechodzi {ok} z {groups.Count} rodzajów biegu");
        File.WriteAllText(outPath, sb.ToString());
    }
}

using System.Text;
using System.Text.Json;
using LcsWidzew.Model;

namespace LcsWidzew.Sim;

/// <summary>
/// Test EDR bez okna (<c>--edrtest wynik.txt</c>, PLAN_EDR.md § 5): dzienniki z szlaków układu, objęcie dyżuru, przyjęcie
/// i wyprawienie, pola wymagane, wersje wierszy, zmiana doby, obszary sterowania stanowisk, przesyłanie przez sieć.
/// Osobno od selftestu – nie zmienia jego raportu.
/// </summary>
public static class EdrTest
{
    public static void Run(string stationPath, string outPath, SimulatorOptions options = null)
    {
        var sb = new StringBuilder(); int ok = 0, tot = 0;
        void K(bool c, string m) { tot++; if (c) ok++; sb.AppendLine($"{(c ? "OK  " : "BŁĄD")} {m}"); }
        // kopia układu w katalogu tymczasowym: bez pliku ustawień EDR obok układu (<układ>.edr.json), test go też nie zapisuje
        var kat = Path.Combine(Path.GetTempPath(), $"edrtest_{Environment.ProcessId}");
        Directory.CreateDirectory(kat);
        var kopia = Path.Combine(kat, Path.GetFileName(stationPath));
        File.Copy(stationPath, kopia, true);
        var st = Station.Load(kopia);
        // kierunek parzysty (z edytora UT) na pierwszym szlaku dwutorowym: wyprawiane z jego posterunku – parzyste
        var szlP = st.Blocks.Values.Where(b => !b.Internal && b.Ends.Count == 1).GroupBy(b => (b.Ends[0].Post, b.Nbr, b.Line)).FirstOrDefault(g => g.Count() > 1)?.ToList() ?? new();
        foreach (var b in szlP) b.EvenDir = b.Ends[0].Outward;
        var sim = new Simulator(st, options: options);
        var user = new User { Name = "Jan Kowalski", Role = "dyżurny" };
        var desk = sim.AddDesk("PC", "Stanowisko 1", true); desk.User = user; sim.User = user;
        var edr = sim.Edr;

        // 1. dzienniki z szlaków: każdy koniec szlaku z posterunkiem w dokładnie jednym dzienniku, tory szlakowe razem
        sb.AppendLine($"Dzienniki ruchu: {edr.Dzienniki.Count}");
        foreach (var d in edr.Dzienniki) sb.AppendLine($"   {d.Post}: {d.Nazwa} (typ {d.Typ}, tory {string.Join(",", d.Tory)}, szlaki {string.Join(",", d.Szlaki)})");
        int konce = st.Blocks.Values.Sum(b => b.Ends.Count(e => !string.IsNullOrEmpty(e.Post)));
        int przypisane = st.Blocks.Values.Sum(b => b.Ends.Count(e => !string.IsNullOrEmpty(e.Post) && edr.Dzienniki.Count(d => d.Post == e.Post && d.Szlaki.Contains(b.Id)) == 1));
        K(edr.Dzienniki.Count > 0 && przypisane == konce, $"każdy koniec szlaku w jednym dzienniku posterunku ({przypisane} z {konce})");
        K(edr.Dzienniki.Select(d => d.Nazwa + d.Post).Distinct().Count() == edr.Dzienniki.Count, "nazwy dzienników posterunku są różne");
        var dwutorowy = edr.Dzienniki.FirstOrDefault(d => d.Tory.Count > 1);
        K(dwutorowy != null, $"szlak wielotorowy w jednym dzienniku: {dwutorowy?.Nazwa} – tory {string.Join(",", dwutorowy?.Tory ?? new())}");
        K(edr.ToryStacyjne.Values.Any(l => l.Count > 0), $"tory stacyjne posterunków: {string.Join("; ", edr.ToryStacyjne.Select(x => x.Key + " " + x.Value.Count))}");

        // 2. objęcie dyżuru – wpis automatyczny w każdym dzienniku stanowiska
        sim.Tick(0.25);
        var dane = edr.DlaStanowiska(desk);
        K(dane.Dzienniki.Count == edr.Dzienniki.Count && dane.Dzienniki.All(d => d.Strony.Count == 1 && d.Strony[0].Wiersze.Count(w => w.Rodzaj == "tekst" && w.Tekst.StartsWith("Dyżur przyjął Jan Kowalski")) == 1 && d.Strony[0].Wiersze[0].Tekst.StartsWith("Północ dnia")),
            "„Północ dnia …” na początku strony i „Dyżur przyjął …” raz w każdym dzienniku");
        sim.Tick(0.25);
        K(edr.Dzienniki.All(d => d.Strony[0].Wiersze.Count == 2), "kolejny cykl nie powtarza wpisu o dyżurze");

        // 3. przyjęcie: pola wymagane wg rodzaju blokady, wiersz niekompletny, potem kompletny
        var dz = edr.Dzienniki[0];
        var r1 = edr.Wykonaj(desk, new EdrPolecenie { Dziennik = dz.Id, Rodzaj = "przyj", Dane = new EdrWersja { Nr = "12345", Tor = "2", Odj = edr.Teraz() } });
        var w1 = dz.Strony[^1].Wiersze[^1];
        K(r1.Ok && w1.Rodzaj == "przyj" && w1.W.Nr == "12345" && Edr.KolumnaNumeru(w1.W.Nr) == 1, $"przyjęcie 12345 zapisane, kolumna 1 (nieparzysty): {r1.Msg}");
        K(w1.Wymagane.SequenceEqual(Edr.WymaganeDla(dz.Typ)) && !w1.Kompletny, $"pola wymagane ({dz.Typ}): {string.Join(",", w1.Wymagane)}; wiersz niekompletny");
        foreach (var p in w1.Wymagane.Where(p => Edr.Wartosc(w1.W, p) == "")) edr.Wykonaj(desk, new EdrPolecenie { Dziennik = dz.Id, Rodzaj = "pole", Wiersz = w1.Id, Pole = p, Wartosc = "8:05" });
        K(w1.Kompletny && w1.W.Prz == "08:05", "po wpisaniu brakujących pól wiersz kompletny, godzina „8:05” → „08:05”");
        int wersji = w1.Wersje.Count;
        K(wersji == 1 + w1.Wymagane.Count(p => p is "Dw" or "Prz"), $"każde nowe pole – nowa wersja ({wersji})");

        // 4. kolejne zmiany tego samego pola w ciągu 10 s – jedna wersja; po 10 s – nowa
        edr.Wykonaj(desk, new EdrPolecenie { Dziennik = dz.Id, Rodzaj = "pole", Wiersz = w1.Id, Pole = "Prz", Wartosc = "08:06" });
        edr.Wykonaj(desk, new EdrPolecenie { Dziennik = dz.Id, Rodzaj = "pole", Wiersz = w1.Id, Pole = "Prz", Wartosc = "08:07" });
        K(w1.Wersje.Count == wersji && w1.W.Prz == "08:07", "zmiany kółkiem w ciągu 10 s – jedna wersja");
        for (int i = 0; i < 48; i++) sim.Tick(0.25);
        edr.Wykonaj(desk, new EdrPolecenie { Dziennik = dz.Id, Rodzaj = "pole", Wiersz = w1.Id, Pole = "Prz", Wartosc = "08:08" });
        K(w1.Wersje.Count == wersji + 1 && w1.Wersje[^2].Prz == "08:07", "zmiana po 12 s – nowa wersja, poprzednia zachowana");

        // 5. wyprawienie, parzysty, „nie dotyczy”, błędy
        var dz2 = edr.Dzienniki[^1];
        var r2 = edr.Wykonaj(desk, new EdrPolecenie { Dziennik = dz2.Id, Rodzaj = "wypr", Dane = new EdrWersja { Nr = "23456", Tor = "3", Dw = "---", Odj = "0812", Lewy = true } });
        var w2 = dz2.Strony[^1].Wiersze[^1];
        K(r2.Ok && w2.Rodzaj == "wypr" && Edr.KolumnaNumeru("23456") == 2 && w2.W.Dw == "---" && w2.W.Odj == "08:12" && w2.W.Lewy, "wyprawienie 23456: kolumna 2, droga wolna „---”, „0812” → „08:12”, po torze lewym");
        K(!edr.Wykonaj(desk, new EdrPolecenie { Dziennik = dz2.Id, Rodzaj = "wypr", Dane = new EdrWersja { Nr = "", Odj = "08:00" } }).Ok, "bez numeru pociągu – odrzucone");
        K(!edr.Wykonaj(desk, new EdrPolecenie { Dziennik = dz2.Id, Rodzaj = "pole", Wiersz = w2.Id, Pole = "Odj", Wartosc = "25:70" }).Ok && w2.W.Odj == "08:12", "błędna godzina – odrzucone, wartość bez zmian");
        K(!edr.Wykonaj(desk, new EdrPolecenie { Dziennik = dz2.Id, Rodzaj = "pole", Wiersz = dz2.Strony[^1].Wiersze[0].Id, Pole = "Uwagi", Wartosc = "x" }).Ok, "wpisu o dyżurze nie da się zmienić");

        // 6. zmiana doby – nowa strona z wpisem
        sim.StartClock = 86400 - sim.T - 1;
        for (int i = 0; i < 12; i++) sim.Tick(0.25);
        K(edr.Doba == 1 && dz.Strony.Count == 2 && dz.Strony[1].Wiersze[0].Tekst.StartsWith("Północ dnia " + dz.Strony[1].Data) && dz.Strony[0].Wiersze.Contains(w1), $"po północy nowa strona {dz.Strony[^1].Data}, poprzednia doba zachowana");

        // 7. obszary sterowania: posterunek drugiego stanowiska
        var desk2 = sim.AddDesk("B", "Stanowisko 2", false); desk2.User = new User { Name = "Adam Nowak" };
        var post = dz2.Post; sim.Control[post] = "B";
        K(!edr.Wykonaj(desk, new EdrPolecenie { Dziennik = dz2.Id, Rodzaj = "przyj", Dane = new EdrWersja { Nr = "1" } }).Ok, $"dziennik posterunku {post} sterowanego z innego stanowiska – odrzucone");
        var dane2 = edr.DlaStanowiska(desk2);
        K(dane2.Dzienniki.All(d => d.Post == post) && dane2.Dzienniki.Any() && dz2.Prowadzi == "Adam Nowak" && dz2.Strony[^1].Wiersze.Any(w => w.Tekst?.StartsWith("Dyżur przyjął Adam Nowak") == true),
            "drugie stanowisko widzi tylko swoje dzienniki, wpis o objęciu dyżuru przez Adama Nowaka");

        // 8. przesyłanie przez sieć (te same opcje co Wire)
        var opt = new JsonSerializerOptions { IncludeFields = true };
        var js = JsonSerializer.Serialize(edr.DlaStanowiska(desk), opt);
        var back = JsonSerializer.Deserialize<EdrData>(js, opt);
        var bw = back.Dzienniki.First(d => d.Id == dz.Id).Strony[0].Wiersze.First(w => w.Id == w1.Id);
        K(bw.W.Prz == "08:08" && bw.Wersje.Count == w1.Wersje.Count && bw.Kompletny, $"dane EDR po serializacji zgodne ({js.Length} B)");

        // ---------------- E2: parzystość wg kierunku szlaku, wykaz pociągów, wykonanie rozkładu
        sb.AppendLine("--- E2");
        var sp = new Simulator(st, options: options) { User = user };
        var kp = sp.AddDesk("PC", "Stanowisko 1", true); kp.User = user;
        var ep = sp.Edr;
        var dp = ep.Dzienniki.FirstOrDefault(d => d.Parzysty != 0);
        K(dp != null && dp.Parzysty == 1 && szlP.All(b => dp.Szlaki.Contains(b.Id)), $"kierunek parzysty szlaku {string.Join(",", szlP.Select(b => b.Id))} → dziennik {dp?.Nazwa}: wyprawiane parzyste");
        if (dp != null)
        {
            var rw = ep.Wykonaj(kp, new EdrPolecenie { Dziennik = dp.Id, Rodzaj = "wypr", Dane = new EdrWersja { Nr = "12345", Tor = "1", Odj = "06:00" } });
            K(dp.Strony[^1].Wiersze[^1].W.Nr == "12344" && rw.Msg.Contains("12345 → 12344"), $"wyprawienie 12345 → 12344 (parzysty kierunek): {rw.Msg}");
            ep.Wykonaj(kp, new EdrPolecenie { Dziennik = dp.Id, Rodzaj = "przyj", Dane = new EdrWersja { Nr = "12344", Tor = "1", Odj = "06:00" } });
            var wp = dp.Strony[^1].Wiersze[^1];
            K(wp.W.Nr == "12345", "przyjęcie 12344 → 12345 (nieparzysty kierunek)");
            ep.Wykonaj(kp, new EdrPolecenie { Dziennik = dp.Id, Rodzaj = "pole", Wiersz = wp.Id, Pole = "Nr", Wartosc = "22222" });
            K(wp.W.Nr == "22223", "zmiana numeru w wierszu przyjęcia też dostosowana (22222 → 22223)");
        }
        K(Edr.TenSam("64410", "64411") && !Edr.TenSam("64410", "64412") && Edr.Wymus("Rob. 1", true) == "Rob. 0" && Edr.Wymus("ABC", true) == "ABC", "ten sam pociąg mimo zmiany parzystości, numer bez cyfry na końcu bez zmian");

        var tr = new Trains(sp); tr.Load("podstawowy");
        var dane3 = ep.DlaStanowiska(kp);
        foreach (var (pk, l) in dane3.Wykaz) sb.AppendLine($"   wykaz {pk}: {l.Count} pociągów – {string.Join(", ", l.Take(6).Select(x => $"{x.Nr} {x.ZKierunku}→{x.WKierunku} {x.OdjPlan}"))}");
        var post0 = st.Blocks.Values.First(b => !b.Internal && b.Ends.Count == 1).Ends[0].Post;
        var wyk = dane3.Wykaz.GetValueOrDefault(post0) ?? new();
        var zSzlaku = sp.Rozklad.Queue.Where(q => q.From != null && st.Blocks.TryGetValue(q.From, out var b) && b.Ends.Any(e => e.Post == post0)).ToList();
        K(zSzlaku.Count > 0 && zSzlaku.All(q => wyk.Any(x => x.Nr == q.Num && x.ZSzlak == q.From && x.ZKierunku != "")), $"wykaz {post0}: pociągi z szlaków posterunku z kierunkiem przyjazdu ({zSzlaku.Count})");
        K(wyk.Select(x => x.Sort).SequenceEqual(wyk.Select(x => x.Sort).OrderBy(x => x)), "wykaz uporządkowany wg czasu");
        // posterunek, do którego pociąg przyjeżdża szlakiem wewnętrznym od innego posterunku LCS (np. Fabryczna od Widzewa)
        var wewn = st.Blocks.Values.FirstOrDefault(b => b.Ends.Count == 2 && b.Ends[0].Post != b.Ends[1].Post);
        if (wewn != null)
        {
            var pB = wewn.Ends.Select(e => e.Post).First(x => x != post0);
            var wykB = dane3.Wykaz.GetValueOrDefault(pB) ?? new();
            K(wykB.Count > 0 && wykB.All(x => x.ZSzlak != null && ep.Dzienniki.Any(d => d.Post == pB && d.Szlaki.Contains(x.ZSzlak))),
                $"wykaz {pB}: kierunek przyjazdu ze szlaku wewnętrznego od {post0} ({string.Join(", ", wykB.Select(x => $"{x.Nr} z {x.ZKierunku} w {x.WKierunku}"))})");
        }

        // wykonanie: pociąg ze szlaku wjeżdża na stację i odjeżdża na inny szlak
        var q0 = zSzlaku.FirstOrDefault(q => q.To != null && q.To != q.From);
        if (q0 != null)
        {
            int w0 = (int)ep.Wersja;
            ep.Zarejestruj("t1", q0.Num, null, q0.From, null);
            for (int i = 0; i < 40; i++) sp.Tick(0.25);
            ep.Zarejestruj("t1", q0.Num, post0, null, "5");
            for (int i = 0; i < 240; i++) sp.Tick(0.25);
            ep.Zarejestruj("t1", q0.Num, null, q0.To, null);
            var r = ep.Wykaz(post0).First(x => x.Nr == q0.Num);
            K(r.PrzRzecz != "" && r.OdjRzecz != "" && r.Stan == "odjechał" && r.TorRzecz == "5" && r.ZSzlak == q0.From && r.WSzlak == q0.To && ep.Wersja > w0,
                $"pociąg {q0.Num}: przyjazd {r.PrzRzecz}, odjazd {r.OdjRzecz}, tor 5, z {r.ZKierunku} na {r.WKierunku} – {r.Stan}");
            // E8c: trasa pociągu – kolumny jak w EDR.exe; posterunek z przejazdem ma „✓” i rzeczywiste godziny z ruchu (rozkład bez planu na posterunkach – trasa pusta)
            var tr0 = r.Trasa.FirstOrDefault(x => x.Z == "✓");
            K(r.Trasa.Count == 0 || tr0 != null && tr0.PrzRzeczGodz == r.PrzRzecz && tr0.OdjRzeczGodz == r.OdjRzecz && tr0.Lp != "" && tr0.Nr != "",
                r.Trasa.Count == 0 ? "trasa: rozkład bez planu na posterunkach – bez wierszy" : $"trasa {q0.Num}: {r.Trasa.Count} wierszy, ✓ {tr0?.Posterunek} {tr0?.PrzRzeczData} {tr0?.PrzRzeczGodz} – {tr0?.OdjRzeczGodz}, Lp. {tr0?.Lp}");
            var dz0 = ep.Dzienniki.First(d => d.Post == post0 && d.Szlaki.Contains(q0.From));
            ep.Wykonaj(kp, new EdrPolecenie { Dziennik = dz0.Id, Rodzaj = "przyj", Dane = new EdrWersja { Nr = q0.Num, Tor = "5", Odj = "06:00" } });
            var rp2 = ep.Wykaz(post0).First(x => x.Nr == q0.Num);
            K(rp2.Przyjety && !rp2.Wyprawiony, $"po wpisie przyjęcia w dzienniku {dz0.Nazwa} – w wykazie „P” (numer w dzienniku: {dz0.Strony[^1].Wiersze[^1].W.Nr})");
        }
        // ruch pociągów: wjazdy z szlaków posterunku (przebiegi jak w selftescie) – rejestracja przyjazdu w cyklu symulacji
        var wjazdy = new HashSet<string>();
        for (int i = 0; i < 2400; i++)
        {
            sp.Tick(0.25);
            if (i % 40 != 0) continue;
            foreach (var b in st.Blocks.Values.Where(b => !b.Internal && b.Ends.Count == 1 && b.Ends[0].Post == post0))
            {
                var sg = b.Ends[0].EntrySig;
                if (sg == null || wjazdy.Contains(sg) || !sp.RouteTable.TryGetValue(sg, out var rt)) continue;
                var r = rt.Where(x => x.Type == "P" && x.Variant == 0 && x.EndKind != "szlak").FirstOrDefault(x => sp.WhyNot(x).Count == 0);
                if (r == null) continue;
                sp.Page = st.Pages.FirstOrDefault(x => x.Posts.Contains(post0))?.Id ?? sp.Page;
                if (sp.Exec($"{r.Start} {r.End} Pociąg", false).Ok) { wjazdy.Add(sg); sb.AppendLine($"   {sp.Clock()} przebieg {r.Start} {r.End} Pociąg"); }
            }
        }
        foreach (var x in ep.Ruch.Where(x => !x.Key.StartsWith("t1|"))) sb.AppendLine($"   ruch {x.Key}: {x.Value.Nr} przyjazd {(x.Value.Prz is double a ? sp.Clock(a) : "–")} z {x.Value.ZSzlak}, tor {x.Value.Tor}, odjazd {(x.Value.Odj is double o ? sp.Clock(o) : "–")}");
        K(ep.Ruch.Any(x => !x.Key.StartsWith("t1|") && x.Value.Prz != null && x.Value.ZSzlak != null && x.Value.Tor != null), $"wjazd pociągu z szlaku zarejestrowany w cyklu symulacji (przebiegi wjazdowe: {wjazdy.Count})");
        K(ep.Ruch.Count > 0, $"po 10 min ruchu: zarejestrowane przyjazdy / odjazdy {ep.Ruch.Count}, pociągów {tr.List.Count}");
        var js3 = JsonSerializer.Serialize(ep.DlaStanowiska(kp), opt);
        var back3 = JsonSerializer.Deserialize<EdrData>(js3, opt);
        K(back3.Wykaz.Sum(x => x.Value.Count) == ep.DlaStanowiska(kp).Wykaz.Sum(x => x.Value.Count) && back3.Posterunki.Count > 0, $"wykaz po serializacji zgodny ({js3.Length} B)");

        // ---------------- E3: stan torów szlakowych i stacyjnych z wpisów w dzienniku (nie z urządzeń)
        sb.AppendLine("--- E3");
        var s3 = new Simulator(st, options: options) { User = user };
        var k3 = s3.AddDesk("PC", "Stanowisko 1", true); k3.User = user;
        var e3 = s3.Edr; s3.Tick(0.25);
        var dwa = e3.Dzienniki.First(d => d.Parzysty == 0 && d.Szlaki.Count == 2 && d.Tory.Count == 2 && st.Blocks[d.Szlaki[0]].Ends.Count == 1);
        var jeden = e3.Dzienniki.FirstOrDefault(d => d.Szlaki.Count == 1 && !st.Blocks[d.Szlaki[0]].OneWay);
        e3.StanSzlaku(dwa); if (jeden != null) e3.StanSzlaku(jeden);
        // ustawienia zasadnicze: bez pliku kierunek zasadniczy nieustalony (ustala go człowiek w ustawieniach dziennika), tor lewy / prawy wg numerów
        K(dwa.ToryStan.Count == 2 && dwa.ToryStan.All(t => t.Kierunek == 0) && dwa.ToryStan[0].Strona == "lewy" && dwa.ToryStan[1].Strona == "prawy" && dwa.ToryStan.All(t => !t.Zajety),
            $"{dwa.Nazwa}: bez ustawień kierunek zasadniczy nieustalony, tor lewy {dwa.ToryStan[0].Tor}, prawy {dwa.ToryStan[1].Tor}, oba wolne");
        K(jeden == null || jeden.ToryStan.Count == 1 && jeden.ToryStan[0].Kierunek == 0, $"szlak jednotorowy dwukierunkowy {jeden?.Nazwa}: kierunek zasadniczy nieustalony (↕)");
        e3.PlikUstawien = Path.Combine(Path.GetTempPath(), $"edrtest_{Environment.ProcessId}.edr.json");
        var us = dwa.Ustawienia.Kopia();
        us.Tory.First(t => t.Strona == "lewy").Zasadniczy = -1; us.Tory.First(t => t.Strona == "prawy").Zasadniczy = 1;
        var bad = us.Kopia(); bad.Tory[0].Strona = bad.Tory[1].Strona = "lewy";
        K(!e3.Wykonaj(k3, new EdrPolecenie { Dziennik = dwa.Id, Rodzaj = "ustawienia", Ustawienia = bad }).Ok, "ustawienia: dwa tory lewe – odrzucone");
        var bad2 = us.Kopia(); bad2.Tory[0].Rodzaj = "Jednokierunkowy"; bad2.Tory[0].Zasadniczy = 0;
        K(!e3.Wykonaj(k3, new EdrPolecenie { Dziennik = dwa.Id, Rodzaj = "ustawienia", Ustawienia = bad2 }).Ok, "ustawienia: tor jednokierunkowy bez kierunku zasadniczego – odrzucone");
        var ru = e3.Wykonaj(k3, new EdrPolecenie { Dziennik = dwa.Id, Rodzaj = "ustawienia", Ustawienia = us });
        e3.StanSzlaku(dwa);
        K(ru.Ok && dwa.ToryStan[0].Kierunek == -1 && dwa.ToryStan[1].Kierunek == 1 && File.Exists(e3.PlikUstawien),
            $"ustawienia zasadnicze zapisane ({ru.Msg}): tor lewy – Z posterunku sąsiedniego, prawy – DO posterunku sąsiedniego, plik {Path.GetFileName(e3.PlikUstawien)}");
        var s4 = new Simulator(st, options: options) { User = user };
        s4.Edr.PlikUstawien = e3.PlikUstawien; s4.Edr.WczytajUstawienia();
        var dwa4 = s4.Edr.Dzienniki.First(d => d.Id == dwa.Id); s4.Edr.StanSzlaku(dwa4);
        K(dwa4.ToryStan[0].Kierunek == -1 && dwa4.ToryStan[1].Kierunek == 1, "ustawienia wczytane z pliku w nowym symulatorze");
        try { File.Delete(e3.PlikUstawien); } catch { }
        EdrWiersz Nowy(EdrDziennik d, string rodzaj, EdrWersja w) { e3.Wykonaj(k3, new EdrPolecenie { Dziennik = d.Id, Rodzaj = rodzaj, Dane = w }); return d.Strony[^1].Wiersze[^1]; }
        void Pole(EdrDziennik d, EdrWiersz w, string pole, string v) => e3.Wykonaj(k3, new EdrPolecenie { Dziennik = d.Id, Rodzaj = "pole", Wiersz = w.Id, Pole = pole, Wartosc = v });
        var post3 = dwa.Post;
        var a3 = Nowy(dwa, "przyj", new EdrWersja { Nr = "46021", Tor = "", Odj = "" });
        e3.StanSzlaku(dwa);
        K(dwa.ToryStan.All(t => !t.Zajety), "przyjęcie bez godziny odjazdu – tor szlakowy wolny");
        Pole(dwa, a3, "Odj", "06:00"); e3.StanSzlaku(dwa);
        K(dwa.ToryStan[0].Zajety && dwa.ToryStan[0].Ruch == -1 && dwa.ToryStan[0].Pociagi.SequenceEqual(new[] { "46021" }) && !dwa.ToryStan[1].Zajety, "„odjechał” wpisany – tor lewy zajęty, jazda do nas, pociąg 46021");
        K(e3.ZajetoscTorowStacyjnych(post3).All(t => !t.Zajety), "przed przyjazdem tory stacyjne wolne");
        Pole(dwa, a3, "Tor", "3"); Pole(dwa, a3, "Prz", "06:05"); e3.StanSzlaku(dwa);
        var z3 = e3.ZajetoscTorowStacyjnych(post3);
        K(dwa.ToryStan.All(t => !t.Zajety) && z3.Single(t => t.Zajety).Tor == "3" && z3.First(t => t.Tor == "3").Pociag == "46021", "„przyjechał” – tor szlakowy wolny, tor stacyjny 3 zajęty przez 46021");
        var inny = e3.Dzienniki.First(d => d.Post == post3 && d != dwa && d.Szlaki.Count == 2 && st.Blocks[d.Szlaki[0]].Ends.Count == 1);
        var b3 = Nowy(inny, "wypr", new EdrWersja { Nr = "46021", Tor = "3", Odj = "06:10" });
        e3.StanSzlaku(inny);
        K(e3.ZajetoscTorowStacyjnych(post3).All(t => !t.Zajety) && inny.ToryStan.Single(t => t.Zajety).Strona == "prawy" && inny.ToryStan.Single(t => t.Zajety).Ruch == 1,
            $"wyprawienie {b3.W.Nr} z toru 3 na {inny.Nazwa} (kierunek zasadniczy nieustalony): tor 3 wolny, zajęty tor prawy, jazda od nas");
        Pole(inny, b3, "Prz", "06:20"); e3.StanSzlaku(inny);
        K(inny.ToryStan.All(t => !t.Zajety), "„przyjechał” u sąsiada – tor szlakowy wolny");
        var c3 = Nowy(inny, "wypr", new EdrWersja { Nr = "99999", Tor = "1", Odj = "06:30", Lewy = true }); e3.StanSzlaku(inny);
        K(inny.ToryStan.Single(t => t.Zajety).Strona == "lewy" && inny.ToryStan.Single(t => t.Zajety).Ruch == 1, "wyprawienie po torze lewym – zajęty tor lewy, jazda od nas");
        Pole(inny, c3, "Prz", "---"); e3.StanSzlaku(inny);
        K(inny.ToryStan.All(t => !t.Zajety), "„przyjechał” = „---” – wpis zamknięty, tor wolny");
        var uw = inny.Ustawienia.Kopia(); uw.WymTor = false; uw.WymDw = true;
        e3.PlikUstawien = null;
        e3.Wykonaj(k3, new EdrPolecenie { Dziennik = inny.Id, Rodzaj = "ustawienia", Ustawienia = uw });
        var c4 = Nowy(inny, "przyj", new EdrWersja { Nr = "11111" });
        K(c4.Wymagane.SequenceEqual(new[] { "Nr", "Dw", "Odj", "Prz" }), $"pola wymagane z opcji wypełniania w ustawieniach: {string.Join(",", c4.Wymagane)}");
        var d3 = e3.DlaStanowiska(k3);
        var b3j = JsonSerializer.Deserialize<EdrData>(JsonSerializer.Serialize(d3, opt), opt);
        K(b3j.ZajetoscTorow.ContainsKey(post3) && b3j.Dzienniki.First(d => d.Id == dwa.Id).ToryStan.Count == 2, "stan torów w danych stanowiska (także po serializacji)");

        // ---------------- E4: telefonogramy i dziennik telefoniczny, stan bieżący szlaku
        sb.AppendLine("--- E4");
        var s5 = new Simulator(st, options: options) { User = user };
        var k5 = s5.AddDesk("PC", "Stanowisko 1", true); k5.User = user;
        var e5 = s5.Edr; e5.PlikUstawien = null; e5.Dzien0 = new DateTime(2026, 10, 10); s5.Tick(0.25);
        var d5 = e5.Dzienniki.First(d => d.Szlaki.Count == 2 && st.Blocks[d.Szlaki[0]].Ends.Count == 1);
        var post5 = d5.Post; var naz5 = st.Posts[post5];
        CmdResult Tel(EdrTelefonogram t, string dz = null, string post = null) => e5.Wykonaj(k5, new EdrPolecenie { Rodzaj = "telefonogram", Dziennik = dz, Wartosc = post, Tel = t });
        K(!Tel(new EdrTelefonogram { Tresc = "Proba", Odebral = "" }, d5.Id).Ok, "telefonogram nadany bez „Odebrał” – odrzucony");
        K(!Tel(new EdrTelefonogram { Tresc = "  ", Odebral = "Adam Nowak" }, d5.Id).Ok, "telefonogram bez treści – odrzucony");
        var lewy = d5.Biezace.Tory.First(t => t.Strona == "lewy");
        var r5 = Tel(new EdrTelefonogram
        {
            Tresc = $"Tor numer {lewy.Tor} od {naz5} do {d5.Sasiad} zamknięty od godz. 5 min 55 z powodu pęknięcia szyny.", Odebral = "Adam Nowak", Godz = "05:55", Notatka = "Zawiadomiono odpowiednie służby.",
            Zmiany = { new EdrZmianaToru { Szlak = lewy.Szlak, Status = "Zamknięty" } }
        }, d5.Id);
        var w5 = d5.Strony[^1].Wiersze[^1];
        K(r5.Ok && w5.Rodzaj == "telefonogram" && w5.Tel.Nr == 1 && w5.Kompletny && w5.Tel.Godz == "05:55" && Edr.Podpis(w5.Tel) == $"Nadał Jan Kowalski {naz5}, odebrał Adam Nowak {d5.Sasiad} godz. {e5.Teraz()}",
            $"telefonogram nr 1 w dzienniku ruchu na całą szerokość, nadany 05:55, w podpisie godzina wprowadzenia: „{Edr.Podpis(w5.Tel)}” ({r5.Msg})");
        e5.StanSzlaku(d5);
        K(d5.ToryStan.First(t => t.Szlak == lewy.Szlak).Status == "Zamknięty" && d5.Ustawienia.Tory.First(t => t.Szlak == lewy.Szlak).Status == "Otwarty",
            "zamknięcie toru: stan bieżący „Zamknięty”, ustawienia zasadnicze bez zmian");
        K((e5.Telefoniczny.GetValueOrDefault(post5)?.Count ?? 0) == 0, "telefonogram z dziennika ruchu – tylko w dzienniku ruchu, nie w telefonicznym");
        K(!e5.Wykonaj(k5, new EdrPolecenie { Dziennik = d5.Id, Rodzaj = "pole", Wiersz = w5.Id, Pole = "Uwagi", Wartosc = "x" }).Ok, "telefonogramu w dzienniku nie da się zmienić");
        var ro = Tel(new EdrTelefonogram { Nadaje = false, Tresc = "Droga wolna dla pociągu 12345", Nadal = "" }, d5.Id);
        var ro2 = Tel(new EdrTelefonogram { Nadaje = false, Tresc = "Droga wolna dla pociągu 12345", Nadal = "Adam Nowak", NrOdbiorcy = "17",
            Zmiany = { new EdrZmianaToru { Szlak = d5.Szlaki[1], Podstawa = Edr.Podstawy[3], Rodzaj = "Dwukierunkowy" } } }, d5.Id);
        var w6 = d5.Strony[^1].Wiersze[^1];
        K(!ro.Ok && ro2.Ok && w6.Tel.Nr == 2 && Edr.Podpis(w6.Tel).StartsWith($"Nadał Adam Nowak {d5.Sasiad}, odebrał Jan Kowalski {naz5}"), $"telefonogram odebrany: bez „Nadał” odrzucony, potem nr 2 – „{Edr.Podpis(w6.Tel)}”");
        var wDw = e5.Wykonaj(k5, new EdrPolecenie { Dziennik = d5.Id, Rodzaj = "przyj", Dane = new EdrWersja { Nr = "12345" } });
        K(d5.Strony[^1].Wiersze[^1].Wymagane.Contains("Dw"), "zapowiadanie telefoniczne wprowadzone telefonogramem – „Droga wolna” wymagana w nowym wierszu");
        var ok5 = Tel(new EdrTelefonogram { Rodzaj = "okolicznosc", Tresc = "Sprawdzono oświetlenie semaforów." }, null, post5);
        K(ok5.Ok && e5.Telefoniczny[post5].Count == 1 && e5.Telefoniczny[post5][0].Nr == 3 && e5.Telefoniczny[post5][0].Dziennik == null && d5.Strony[^1].Wiersze.Count(w => w.Rodzaj == "telefonogram") == 2,
            "inne okoliczności – tylko w dzienniku telefonicznym, numer 3 (numeracja wspólna z telefonogramami w dziennikach ruchu)");
        // zmiana ustawień zasadniczych nie znosi zamknięcia toru z telefonogramu
        var uz = d5.Ustawienia.Kopia(); uz.Tory.First(t => t.Strona == "prawy").Zasadniczy = 1;
        e5.Wykonaj(k5, new EdrPolecenie { Dziennik = d5.Id, Rodzaj = "ustawienia", Ustawienia = uz });
        K(d5.Biezace.Tory.First(t => t.Szlak == lewy.Szlak).Status == "Zamknięty" && d5.Biezace.Tory.First(t => t.Strona == "prawy").Zasadniczy == 1,
            "po zmianie ustawień zasadniczych zamknięcie toru z telefonogramu zostaje w stanie bieżącym");
        var otw = Tel(new EdrTelefonogram { Tresc = $"Tor numer {lewy.Tor} otwarty.", Odebral = "Adam Nowak", Zmiany = { new EdrZmianaToru { Szlak = lewy.Szlak, Status = "Otwarty" } } }, d5.Id);
        e5.StanSzlaku(d5);
        K(otw.Ok && d5.ToryStan.First(t => t.Szlak == lewy.Szlak).Status == "Otwarty", "telefonogram otwierający tor – stan bieżący „Otwarty”");
        K(!Tel(new EdrTelefonogram { Tresc = "x", Odebral = "y" }, null, "XX").Ok, "telefonogram do nieistniejącego posterunku – odrzucony");
        var j5 = JsonSerializer.Deserialize<EdrData>(JsonSerializer.Serialize(e5.DlaStanowiska(k5), opt), opt);
        K(j5.Telefoniczny[post5].Count == 1 && j5.Dzienniki.First(d => d.Id == d5.Id).Strony[^1].Wiersze.Count(w => w.Tel != null) == 3 && j5.Dzienniki.First(d => d.Id == d5.Id).Biezace.Tory.Count == 2,
            "dziennik telefoniczny, telefonogramy i stan bieżący po serializacji");
        // numeracja: dobowa (domyślnie) od 1 w nowej dobie; miesięczna i roczna – dalej
        int nrPrzed = e5.NastepnyNumer(post5);
        s5.StartClock = 86400 - s5.T - 1; for (int i = 0; i < 12; i++) s5.Tick(0.25);
        int nrDoba = e5.NastepnyNumer(post5);
        var rn = e5.Wykonaj(k5, new EdrPolecenie { Rodzaj = "numeracja", Wartosc = post5, Pole = "miesieczna" });
        int nrMies = e5.NastepnyNumer(post5);
        e5.Wykonaj(k5, new EdrPolecenie { Rodzaj = "numeracja", Wartosc = post5, Pole = "roczna" });
        int nrRok = e5.NastepnyNumer(post5);
        K(nrPrzed == 5 && nrDoba == 1 && rn.Ok && nrMies == 5 && nrRok == 5 && e5.DlaStanowiska(k5).NastepnyNr[post5] == 5 && !e5.Wykonaj(k5, new EdrPolecenie { Rodzaj = "numeracja", Wartosc = post5, Pole = "tygodniowa" }).Ok,
            $"numeracja: przed północą następny {nrPrzed}, nowa doba – dobowa {nrDoba}, miesięczna {nrMies}, roczna {nrRok}; nieznana – odrzucona");

        // ---------------- E5: dróżnicy, jazda do km i z powrotem, wpis dowolny, sprawdzenia cykliczne, godzina wprowadzenia
        sb.AppendLine("--- E5");
        K(Edr.Godzina("8:00 / 8:12") == "08:00 / 08:12" && Edr.Godzina("08:00 /") == "08:00" && Edr.Godzina("25:00 / 08:00") == null && Edr.Godzina(" / 08:00") == null,
            "druga godzina w komórce (pole wyboru w edytorze): „8:00 / 8:12” → „08:00 / 08:12”, błędna – odrzucona");
        var s6 = new Simulator(st, options: options) { User = user };
        var k6 = s6.AddDesk("PC", "Stanowisko 1", true); k6.User = user;
        var e6 = s6.Edr; e6.PlikUstawien = null; s6.Tick(0.25);
        var d6 = e6.Dzienniki.First(d => d.Szlaki.Count == 2 && st.Blocks[d.Szlaki[0]].Ends.Count == 1 && d.Parzysty == 0);
        CmdResult W6(EdrPolecenie p) { p.Dziennik ??= d6.Id; return e6.Wykonaj(k6, p); }
        var u6 = d6.Ustawienia.Kopia();
        u6.Droznicy.Add(new EdrDroznik { Km = "62,267", Nazwa = "63", Oznaczenie = "63", WgSzlaku = "Jednotorowych" });
        u6.Droznicy.Add(new EdrDroznik { Km = "61,966", Nazwa = "skp1", Oznaczenie = "skp1", WgSzlaku = "Dwu i więcej torowych", DlaPociagow = "DO posterunku sąsiedniego" });
        var bez = u6.Kopia(); bez.Droznicy[0].Oznaczenie = "";
        var dup = u6.Kopia(); dup.Droznicy[1].Oznaczenie = "63";
        K(!W6(new EdrPolecenie { Rodzaj = "ustawienia", Ustawienia = bez }).Ok && !W6(new EdrPolecenie { Rodzaj = "ustawienia", Ustawienia = dup }).Ok && W6(new EdrPolecenie { Rodzaj = "ustawienia", Ustawienia = u6 }).Ok && d6.Biezace.Droznicy.Count == 2,
            "posterunki dróżników w ustawieniach dziennika: bez oznaczenia i powtórzone – odrzucone, dwa poprawne – przyjęte");
        var id63 = d6.Biezace.Droznicy[0].Id; var idSkp = d6.Biezace.Droznicy[1].Id;
        W6(new EdrPolecenie { Rodzaj = "przyj", Dane = new EdrWersja { Nr = "11111", Tor = "1", Odj = "06:00" } });
        var a6 = d6.Strony[^1].Wiersze[^1];
        K(a6.Wymagane.Contains("Dr:" + id63) && !a6.Wymagane.Contains("Dr:" + idSkp) && a6.W.Wpis.GetValueOrDefault("Odj") == e6.Teraz(),
            "przyjęcie: wymagane powiadomienie dróżnika 63 (w obu kierunkach), skp1 nie (tylko do sąsiada); godzina wprowadzenia odjazdu zapisana");
        W6(new EdrPolecenie { Rodzaj = "pole", Wiersz = a6.Id, Pole = "Dr:" + id63, Wartosc = "6:02" });
        K(Edr.Wartosc(a6.W, "Dr:" + id63) == "06:02" && a6.W.Wpis.ContainsKey("Dr:" + id63), "dróżnik „jak na linii jednotorowej” – godzina 06:02 (z godziną wprowadzenia)");
        W6(new EdrPolecenie { Rodzaj = "wypr", Dane = new EdrWersja { Nr = "22222", Tor = "1", Odj = "06:05" } });
        var b6 = d6.Strony[^1].Wiersze[^1];
        K(!W6(new EdrPolecenie { Rodzaj = "pole", Wiersz = b6.Id, Pole = "Dr:" + idSkp, Wartosc = "06:05" }).Ok && W6(new EdrPolecenie { Rodzaj = "pole", Wiersz = b6.Id, Pole = "Dr:" + idSkp, Wartosc = "\\" }).Ok
            && b6.Wymagane.Contains("Dr:" + idSkp) && Edr.Wartosc(b6.W, "Dr:" + idSkp) == "\\", "dróżnik „dwu i więcej torowy” – tylko ukośnik, godzina odrzucona");
        // jazda do km i z powrotem
        K(!W6(new EdrPolecenie { Rodzaj = "wypr", Pole = "powrot", Wartosc = "", Dane = new EdrWersja { Nr = "Rob. 1", Tor = "3", Odj = "06:10" } }).Ok, "jazda do km bez kilometra – odrzucona");
        int przed = d6.Strony[^1].Wiersze.Count;
        var rk = W6(new EdrPolecenie { Rodzaj = "wypr", Pole = "powrot", Wartosc = "57,987", Dane = new EdrWersja { Nr = "Rob. 1", Tor = "3", Dw = "06:09", Odj = "06:10" } });
        var k1 = d6.Strony[^1].Wiersze[^2]; var k2 = d6.Strony[^1].Wiersze[^1];
        K(rk.Ok && d6.Strony[^1].Wiersze.Count == przed + 2 && k1.Para == k2.Id && k2.Para == k1.Id && k2.Rodzaj == "powrot" && k1.W.Uwagi.StartsWith("do km 57,987 i z powrotem") && k1.W.Prz == "---" && k2.W.Odj == "---" && k2.Wymagane.SequenceEqual(new[] { "Prz" }) && !k2.Kompletny,
            "jazda do km 57,987 i z powrotem: dwa wiersze w klamrze, w powrocie wymagane „przyjechał”");
        e6.StanSzlaku(d6);
        bool zaj = d6.ToryStan.Any(t => t.Pociagi.Contains("Rob. 0") || t.Pociagi.Contains("Rob. 1"));
        W6(new EdrPolecenie { Rodzaj = "pole", Wiersz = k2.Id, Pole = "Prz", Wartosc = "06:40" }); e6.StanSzlaku(d6);
        K(zaj && !d6.ToryStan.Any(t => t.Pociagi.Any(n => n.StartsWith("Rob."))) && k2.Kompletny, "pociąg na szlaku od wyjazdu do wpisu powrotu; po powrocie tor wolny, wiersz kompletny");
        // wpis o dowolnej treści
        var rwp = W6(new EdrPolecenie { Rodzaj = "wpis", Wartosc = "Przekazano kluczyk do skrzynki." });
        var w7 = d6.Strony[^1].Wiersze[^1];
        K(!W6(new EdrPolecenie { Rodzaj = "wpis", Wartosc = " " }).Ok && rwp.Ok && w7.Rodzaj == "wpis" && w7.Tekst == "Przekazano kluczyk do skrzynki." && w7.Kompletny && !W6(new EdrPolecenie { Rodzaj = "pole", Wiersz = w7.Id, Pole = "Uwagi", Wartosc = "x" }).Ok,
            "wpis o dowolnej treści: pusty odrzucony, zapisany na całą szerokość, bez zmian");
        // sprawdzenia cykliczne
        var post6 = d6.Post;
        var piec = Enumerable.Range(0, 5).Select(i => new EdrSprawdzenie { Nazwa = "S" + i, Okres = 60 }).ToList();
        K(!e6.Wykonaj(k6, new EdrPolecenie { Rodzaj = "sprawdzenia", Wartosc = post6, Sprawdzenia = piec }).Ok, "więcej niż cztery sprawdzenia – odrzucone");
        var spr = new EdrSprawdzenie { Nazwa = "Sprawdzenie łączności z posterunkami dróżników przejazdowych", Okres = 30 };
        K(e6.Wykonaj(k6, new EdrPolecenie { Rodzaj = "sprawdzenia", Wartosc = post6, Sprawdzenia = new() { spr } }).Ok && e6.StanSprawdzen(k6).Single().Stan == "ok", "sprawdzenie co 30 min – na początku w terminie");
        for (int i = 0; i < 16 * 240; i++) s6.Tick(0.25);
        var st16 = e6.StanSprawdzen(k6).Single().Stan;
        for (int i = 0; i < 15 * 240; i++) s6.Tick(0.25);
        var st31 = e6.StanSprawdzen(k6).Single().Stan;
        int tel = e6.Telefoniczny.GetValueOrDefault(post6)?.Count ?? 0;
        var rs = e6.Wykonaj(k6, new EdrPolecenie { Rodzaj = "sprawdzenie", Wartosc = post6, Pole = spr.Id, Dane = new EdrWersja { Uwagi = "Łączność sprawna." } });
        var sn = e6.StanSprawdzen(k6).Single();
        K(st16 == "wkrotce" && st31 == "po_terminie" && rs.Ok && sn.Stan == "ok" && sn.Ostatnie == e6.Teraz() && e6.Telefoniczny[post6].Count == tel + 1 && e6.Telefoniczny[post6][^1].Tresc.StartsWith("Sprawdzenie: Sprawdzenie łączności"),
            $"po 16 min – {st16}, po 31 min – {st31}; wykonanie: znów w terminie, ostatnio {sn.Ostatnie}, zapis w dzienniku telefonicznym");
        var j6 = JsonSerializer.Deserialize<EdrData>(JsonSerializer.Serialize(e6.DlaStanowiska(k6), opt), opt);
        K(j6.Dzienniki.First(d => d.Id == d6.Id).Biezace.Droznicy.Count == 2 && j6.Sprawdzenia.Count == 1 && j6.SprawdzeniaUst[post6].Count == 1 && j6.Dzienniki.First(d => d.Id == d6.Id).Strony[^1].Wiersze.Any(w => w.Para != 0),
            "dróżnicy, klamra i sprawdzenia po serializacji");

        // ---------------- E6: wyciągi (dziennik ruchu bez historii i z historią, dziennik telefoniczny, zajętość torów)
        sb.AppendLine("--- E6");
        {
            var s7 = new Simulator(st, options: options) { User = user };
            var k7 = s7.AddDesk("PC", "Stanowisko 1", true); k7.User = user;
            var e7 = s7.Edr; e7.Dzien0 = new DateTime(2026, 10, 10); s7.Tick(0.25);
            var d7 = e7.Dzienniki.First(d => d.Szlaki.Count == 2 && st.Blocks[d.Szlaki[0]].Ends.Count == 1 && d.Parzysty == 0);
            CmdResult W7(EdrPolecenie p) { p.Dziennik ??= d7.Id; return e7.Wykonaj(k7, p); }
            W7(new EdrPolecenie { Rodzaj = "przyj", Dane = new EdrWersja { Nr = "11111", Tor = "3", Odj = "06:00" } });
            var a7 = d7.Strony[^1].Wiersze[^1];
            for (int i = 0; i < 60; i++) s7.Tick(0.25);
            W7(new EdrPolecenie { Rodzaj = "pole", Wiersz = a7.Id, Pole = "Prz", Wartosc = "06:05" });
            for (int i = 0; i < 60; i++) s7.Tick(0.25);
            W7(new EdrPolecenie { Rodzaj = "pole", Wiersz = a7.Id, Pole = "Uwagi", Wartosc = "Opóźnienie – oczekiwanie na skomunikowanie" });
            e7.Wykonaj(k7, new EdrPolecenie { Rodzaj = "telefonogram", Dziennik = d7.Id, Tel = new EdrTelefonogram { Tresc = "Tor numer 1 zamknięty.", Odebral = "Adam Nowak" } });
            var inny7 = e7.Dzienniki.First(d => d.Post == d7.Post && d != d7);
            e7.Wykonaj(k7, new EdrPolecenie { Dziennik = inny7.Id, Rodzaj = "wypr", Dane = new EdrWersja { Nr = "11111", Tor = "3", Odj = "06:20" } });
            e7.Wykonaj(k7, new EdrPolecenie { Rodzaj = "telefonogram", Wartosc = d7.Post, Tel = new EdrTelefonogram { Rodzaj = "okolicznosc", Tresc = "Sprawdzono łączność." } });
            var od = new DateTime(2026, 10, 10, 0, 0, 0); var doo = new DateTime(2026, 10, 10, 23, 59, 0);
            var wBez = EdrWyciag.Dziennik(d7, "Testowo", od, doo, false);
            var wHist = EdrWyciag.Dziennik(d7, "Testowo", od, doo, true);
            var wp = wBez.Wiersze.Where(x => x.Rodzaj == "poc").ToList();
            K(wBez.Wiersze.Any(x => x.Rodzaj == "tekst" && x.Tekst.StartsWith("Dyżur przyjął")) && wp.Count == 1 && wp[0].Np == "11111" && wp[0].Prz == "06:05" && wp[0].Uwagi.StartsWith("Opóźnienie") && wBez.Wiersze.Any(x => x.Rodzaj == "tel" && x.Podpis.StartsWith("Nadał")) && wBez.Wiersze.All(x => !x.Historyczny && x.Zmiana == ""),
                $"wyciąg bez historii: wpis o dyżurze, pociąg w ostatecznej postaci, telefonogram z podpisem ({wBez.Wiersze.Count} wierszy)");
            var hp = wHist.Wiersze.Where(x => x.Rodzaj == "poc").ToList();
            K(hp.Count == 3 && !hp[0].Historyczny && hp.Skip(1).All(x => x.Historyczny) && hp[0].Uwagi.StartsWith("Opóźnienie") && hp[1].Uwagi == "" && hp[1].Prz == "06:05" && hp[2].Prz == "" && hp.All(x => x.Zmiana.Contains("/26 ")),
                $"wyciąg z historią: bieżąca postać, pod nią dwie wcześniejsze (najniżej najstarsza), kolumna 9 – data i godzina zmiany ({hp[0].Zmiana}, {hp[2].Zmiana})");
            K(EdrWyciag.Dziennik(d7, "Testowo", new DateTime(2026, 10, 11), new DateTime(2026, 10, 11, 23, 0, 0), false).Wiersze.Count == 0, "okres bez zmian – wyciąg pusty");
            var tel7 = EdrWyciag.Telefoniczny(e7.Telefoniczny.GetValueOrDefault(d7.Post), od, doo);
            K(tel7.Count == 1 && tel7[0].Nr == "2" && tel7[0].Tresc.Contains("Sprawdzono łączność") && tel7[0].Data == "2026-10-10", $"wyciąg z dziennika telefonicznego: tylko jego wpisy (nr {tel7.FirstOrDefault()?.Nr})");
            var tor7 = EdrWyciag.ZajetoscTorow(e7.Dzienniki.Where(d => d.Post == d7.Post), od, doo);
            K(tor7.Count == 1 && tor7[0].Tor == "3" && tor7[0].Pociag == "11111" && tor7[0].Od == "2026-10-10 06:05" && tor7[0].Do == "2026-10-10 06:20", $"wyciąg z zajętości torów: tor 3, 11111, {tor7.FirstOrDefault()?.Od} – {tor7.FirstOrDefault()?.Do}");
        }

        // ---------------- E7: konfiguracja posterunków i dzienników (administrator), K / NK, potwierdzenie czasu, uwagi, czas bez wypadku
        sb.AppendLine("--- E7");
        {
            var s8 = new Simulator(st, options: options) { User = user };
            var k8 = s8.AddDesk("PC", "Stanowisko 1", true); k8.User = user;
            var e8 = s8.Edr; e8.Dzien0 = new DateTime(2026, 10, 10); new Trains(s8).Load("podstawowy"); s8.Tick(0.25);
            var dysp = new User { Name = "Anna Dyspozytor", Role = "administrator" };
            var post8 = e8.Posterunki.First(p => e8.Dzienniki.Count(d => d.Post == p.Kod) >= 2).Kod;
            var dz8 = e8.Dzienniki.First(d => d.Post == post8);
            List<EdrPosterunekUst> Konf() => e8.Posterunki.Select(x => x.Kopia()).ToList();
            CmdResult Posterunki(List<EdrPosterunekUst> l) => e8.Wykonaj(k8, new EdrPolecenie { Rodzaj = "posterunki", Posterunki = l });
            K(!Posterunki(Konf()).Ok, "konfiguracja posterunków przez dyżurnego – odrzucona (tylko administrator)");
            e8.Wykonaj(k8, new EdrPolecenie { Dziennik = dz8.Id, Rodzaj = "przyj", Dane = new EdrWersja { Nr = "22222", Tor = "1", Odj = "06:00" } });
            k8.User = dysp;
            var konf = Konf(); var p0 = konf.First(x => x.Kod == post8);
            var j0 = p0.Dzienniki.First(x => x.Id == dz8.Id); j0.Nazwa = "Szlak testowy"; j0.Zakladka = 99;
            p0.Dzienniki.Add(new EdrDziennikUst { Nazwa = "Bocznica Zakład", Sasiad = "Zakład", Typ = "tel", Tory = { "1" }, Zakladka = 99 });
            konf.Add(new EdrPosterunekUst { Kod = "PX", Nazwa = "Posterunek X", ToryStacyjne = { "2", "1" }, Dzienniki = { new EdrDziennikUst { Nazwa = "Do Y", Sasiad = "Y", Tory = { "1", "2" }, Zakladka = 1 } } });
            var r8 = Posterunki(konf);
            var dzN = e8.Dzienniki.FirstOrDefault(d => d.Id == dz8.Id);
            K(r8.Ok && dzN != null && dzN.Nazwa == "Szlak testowy" && dzN.Zakladka == 99 && dzN.Strony.SelectMany(x => x.Wiersze).Any(w => w.Rodzaj == "przyj" && Edr.TenSam(w.W.Nr, "22222")),
                $"konfiguracja przyjęta ({r8.Msg}): dziennik o tym samym id zachowuje wpisy, nowa nazwa, zakładka 99");
            var bocz = e8.Dzienniki.FirstOrDefault(d => d.Nazwa == "Bocznica Zakład");
            K(bocz != null && bocz.Post == post8 && bocz.Tory.SequenceEqual(new[] { "1" }) && bocz.Zakladka == 99 && Edr.Wymagane(bocz.Biezace).Contains("Dw") && bocz.Strony.Count == 1,
                "nowy dziennik z torem wpisanym ręcznie (bez szlaku w układzie), telefoniczne zapowiadanie – droga wolna wymagana, w zakładce razem z dziennikiem „Szlak testowy”");
            var dane8 = e8.DlaStanowiska(k8);
            K(dane8.Posterunki.GetValueOrDefault("PX") == "Posterunek X" && dane8.ToryStacyjne["PX"].SequenceEqual(new[] { "1", "2" }) && dane8.Dzienniki.Any(d => d.Post == "PX" && d.Tory.Count == 2) && !dane8.ZUkladu && dane8.Admin,
                "nowy posterunek ruchu z torami stacyjnymi i dziennikiem w danych stanowiska");
            var zle = Konf(); zle.Add(new EdrPosterunekUst { Kod = "PX", Nazwa = "Drugi" });
            K(!Posterunki(zle).Ok, "powtórzony kod posterunku – odrzucone");
            var zle2 = Konf(); foreach (var x in zle2.First(x => x.Kod == post8).Dzienniki) x.Zakladka = 5;
            K(!Posterunki(zle2).Ok, "trzy dzienniki w jednej zakładce – odrzucone");
            var zle3 = Konf(); zle3.First(x => x.Kod == post8).Dzienniki[0].Szlaki.Add("NIE_MA");
            K(!Posterunki(zle3).Ok, "szlak spoza układu – odrzucone");
            var s9 = new Simulator(st, options: options);
            K(s9.Edr.Dzienniki.Any(d => d.Nazwa == "Bocznica Zakład" && d.Zakladka == 99) && s9.Edr.Posterunki.Any(p => p.Kod == "PX"), "konfiguracja wczytana z pliku w nowym symulatorze");
            var rz = e8.Wykonaj(k8, new EdrPolecenie { Rodzaj = "posterunkizukladu" });
            K(rz.Ok && e8.DlaStanowiska(k8).ZUkladu && !e8.Dzienniki.Any(d => d.Nazwa == "Bocznica Zakład") && e8.Dzienniki.Any(d => d.Id == dz8.Id && d.Nazwa != "Szlak testowy" && d.Strony.SelectMany(x => x.Wiersze).Any(w => w.Rodzaj == "przyj" && Edr.TenSam(w.W.Nr, "22222"))),
                $"przywrócenie z układu ({rz.Msg}): dziennik szlaku z wpisami zostaje");

            // wykaz: K / NK i potwierdzenie przez dyspozytora, uwagi, potwierdzenie przez człowieka (wpis w dzienniku)
            var (pw, wk) = e8.Posterunki.Select(p => (p.Kod, e8.Wykaz(p.Kod))).FirstOrDefault(x => x.Item2.Count > 0);
            if (pw != null)
            {
                var poc = wk[0];
                k8.User = user;
                K(!e8.Wykonaj(k8, new EdrPolecenie { Rodzaj = "kursuje", Klucz = poc.Klucz, Wartosc = "NK" }).Ok && !e8.Wykonaj(k8, new EdrPolecenie { Rodzaj = "uwagi", Pole = "ekspl", Klucz = poc.Klucz, Wartosc = "x" }).Ok,
                    "dyżurny nie zmienia K / NK ani uwag eksploatacyjnych");
                K(e8.Wykonaj(k8, new EdrPolecenie { Rodzaj = "uwagi", Pole = "wlasne", Post = pw, Klucz = poc.Klucz, Wartosc = "Sprawdzić skład" }).Ok, "dyżurny wpisuje uwagi własne");
                k8.User = dysp;
                e8.Wykonaj(k8, new EdrPolecenie { Rodzaj = "kursuje", Klucz = poc.Klucz, Wartosc = "NK" });
                e8.Wykonaj(k8, new EdrPolecenie { Rodzaj = "potwierdz", Klucz = poc.Klucz, Wartosc = "tak" });
                e8.Wykonaj(k8, new EdrPolecenie { Rodzaj = "uwagi", Pole = "przew", Klucz = poc.Klucz, Wartosc = "Opóźnienie przewoźnika" });
                var po = e8.Wykaz(pw).First(x => x.Klucz == poc.Klucz);
                K(!po.Kursuje && po.NieKursuje && po.Potw == "dyspozytor" && po.UwagiPrzew == "Opóźnienie przewoźnika" && po.UwagiWlasne == "Sprawdzić skład",
                    $"pociąg {po.Nr}: NK, czas potwierdzony przez dyspozytora (szary), uwagi przewoźnika i własne");
                var dzp = e8.Dzienniki.First(d => d.Post == pw);
                e8.Wykonaj(k8, new EdrPolecenie { Dziennik = dzp.Id, Rodzaj = "przyj", Dane = new EdrWersja { Nr = po.Nr, Tor = "1", Odj = "06:00" } });
                var po2 = e8.Wykaz(pw).First(x => x.Klucz == poc.Klucz);
                K(po2.Potw == "czlowiek" && !po2.Kursuje, $"wpis dyżurnego w dzienniku – czas potwierdzony przez człowieka (zielony): {po2.Nr}");
                K(po2.PrzPlanD == "" || po2.PrzPlanD.EndsWith(" " + po2.PrzPlan) && po2.PrzPlanD.Length == 11, $"godziny z datą (Prezentuj daty): {po2.PrzPlanD} / {po2.OdjPlanD}");
            }
            else K(false, "wykaz pociągów pusty – brak pociągu do sprawdzenia K / NK");

            // czas bez wypadku: 1 dzień 2 godz. 5 min. przed bieżącą chwilą
            var teraz = e8.Dzien0.AddSeconds(s8.StartClock + s8.T); teraz = new DateTime(teraz.Year, teraz.Month, teraz.Day, teraz.Hour, teraz.Minute, 0);
            var od8 = teraz.AddMinutes(-(24 * 60 + 125)).ToString("dd-MM-yyyy HH:mm");
            k8.User = user;
            K(!e8.Wykonaj(k8, new EdrPolecenie { Rodzaj = "bezwypadku", Wartosc = od8 }).Ok, "czas bez wypadku – dyżurny: odrzucone");
            k8.User = dysp;
            var rb = e8.Wykonaj(k8, new EdrPolecenie { Rodzaj = "bezwypadku", Wartosc = od8 });
            K(rb.Ok && e8.CzasBezWypadku() == "1 dzień 2 godz. 5 min." && !e8.Wykonaj(k8, new EdrPolecenie { Rodzaj = "bezwypadku", Wartosc = "wczoraj" }).Ok, $"czas bez wypadku od {od8}: {e8.CzasBezWypadku()}; błędna data – odrzucona");
            var js8 = JsonSerializer.Deserialize<EdrData>(JsonSerializer.Serialize(e8.DlaStanowiska(k8), new JsonSerializerOptions { IncludeFields = true }), new JsonSerializerOptions { IncludeFields = true });
            K(js8.PosterunkiUst.Count == e8.Posterunki.Count && js8.BezWypadkuCzas == "1 dzień 2 godz. 5 min." && js8.SzlakiUkladu.Count == st.Blocks.Count && js8.Admin, "nowe dane EDR przechodzą przez sieć (serializacja)");
        }

        // E8a: kolumna „P T” – peron cyframi rzymskimi, tor arabskimi (uwaga właściciela 2026-10-28)
        {
            var pt = new[] { Edr.PeronTor("2", "5"), Edr.PeronTor("IV", "12"), Edr.PeronTor(null, "3"), Edr.PeronTor("1", "") };
            K(pt[0] == "II 5" && pt[1] == "IV 12" && pt[2] == "3" && pt[3] == "I", "P T: " + string.Join(", ", pt));
        }

        // ---------------- E8d: godziny i północ, droga wolna, tor szlakowy z uwag, wykreślenie, zapis dzienników do pliku
        sb.AppendLine("--- E8d");
        {
            K(Edr.Godzina("812") == "08:12" && Edr.Godzina("0812") == "08:12" && Edr.Godzina("2359") == "23:59" && Edr.Godzina("960") == null && Edr.Godzina("12") == null,
                $"godzina bez dwukropka: 812 → {Edr.Godzina("812")}, 0812 → {Edr.Godzina("0812")}, 960 – błędna");
            var s9 = new Simulator(st, options: options) { User = user };
            var k9 = s9.AddDesk("PC", "Stanowisko 1", true); k9.User = user;
            var e9 = s9.Edr; e9.PlikUstawien = null; s9.Tick(0.25);
            CmdResult W9(EdrPolecenie p) => e9.Wykonaj(k9, p);
            var dwa9 = e9.Dzienniki.First(d => d.Szlaki.Count == 2 && st.Blocks[d.Szlaki[0]].Ends.Count == 1);
            EdrWiersz Ost(EdrDziennik d) => d.Strony[^1].Wiersze[^1];

            // północ: odjazd 23:58, przyjazd 00:03 – następna doba; przyjazd przed odjazdem w dzień – odrzucony
            var rp = W9(new EdrPolecenie { Dziennik = dwa9.Id, Rodzaj = "przyj", Dane = new EdrWersja { Nr = "19001", Tor = "1", Odj = "23:58" } });
            var wp = Ost(dwa9);
            var rp2 = W9(new EdrPolecenie { Dziennik = dwa9.Id, Rodzaj = "pole", Wiersz = wp.Id, Pole = "Prz", Wartosc = "0003" });
            var rz = W9(new EdrPolecenie { Dziennik = dwa9.Id, Rodzaj = "przyj", Dane = new EdrWersja { Nr = "19003", Tor = "1", Odj = "10:00", Prz = "09:50" } });
            K(rp.Ok && rp2.Ok && wp.W.Prz == "00:03" && !rz.Ok, $"północ: odjechał 23:58, przyjechał 00:03 – przyjęte; przyjechał 09:50 przed odjazdem 10:00 – odrzucone ({rz.Msg})");

            // droga wolna: szlak jednotorowy – zawsze; dwutorowy – przy zamkniętym torze i przy ruchu dwukierunkowym na torze zasadniczo jednokierunkowym
            var jeden9 = e9.Dzienniki.FirstOrDefault(d => d.Biezace.Tory.Count == 1);
            var u9 = dwa9.Biezace.Kopia(); bool dwBez = Edr.Wymagane(u9, dwa9.Ustawienia).Contains("Dw");
            var zam = u9.Kopia(); zam.Tory[0].Status = "Zamknięty";
            var zas = u9.Kopia(); foreach (var t in zas.Tory) t.Rodzaj = "Jednokierunkowy";
            K((jeden9 == null || Edr.Wymagane(jeden9.Biezace, jeden9.Ustawienia).Contains("Dw")) && Edr.Wymagane(zam, dwa9.Ustawienia).Contains("Dw") && Edr.Wymagane(u9, zas).Contains("Dw")
                && (u9.WymDw || u9.Tory.Any(t => t.Podstawa == Edr.Podstawy[3]) || !dwBez),
                $"droga wolna: jednotorowy {(jeden9 == null ? "(brak w układzie)" : string.Join(",", Edr.Wymagane(jeden9.Biezace, jeden9.Ustawienia)))}; dwutorowy z zamkniętym torem i z ruchem jednotorowym – wymagana, bez zmian – {(dwBez ? "wymagana z ustawień" : "nie")}");

            // tor szlakowy z uwag: „po torze nr N” zajmuje tor N, błędny numer, dwa numery, sprzeczność z lewym / prawym – odrzucone
            var tLewy = dwa9.Biezace.Tory.First(t => t.Strona == "lewy"); var tPrawy = dwa9.Biezace.Tory.First(t => t.Strona == "prawy");
            // kierunki zasadnicze (ustawienia dziennika): tor lewy – do nas (przyjazdy), prawy – od nas (wyprawienia)
            var uz9 = dwa9.Ustawienia.Kopia(); uz9.Tory.First(t => t.Strona == "lewy").Zasadniczy = -1; uz9.Tory.First(t => t.Strona == "prawy").Zasadniczy = 1;
            var ru9 = W9(new EdrPolecenie { Dziennik = dwa9.Id, Rodzaj = "ustawienia", Ustawienia = uz9 });
            var zasW = dwa9.Biezace.Tory.FirstOrDefault(t => t.Zasadniczy == 1);
            K(ru9.Ok && zasW?.Tor == tPrawy.Tor, $"ustawienia: kierunek zasadniczy – tor {tLewy.Tor} do nas, tor {tPrawy.Tor} od nas ({ru9.Msg})");
            var rw = W9(new EdrPolecenie { Dziennik = dwa9.Id, Rodzaj = "wypr", Dane = new EdrWersja { Nr = "19100", Tor = "2", Odj = "11:00", Uwagi = $"Po torze nr {tLewy.Tor}" } });
            var ww = Ost(dwa9); e9.StanSzlaku(dwa9);
            var zaj9 = dwa9.ToryStan.FirstOrDefault(t => t.Pociagi.Contains(ww.W.Nr));
            var bz1 = W9(new EdrPolecenie { Dziennik = dwa9.Id, Rodzaj = "wypr", Dane = new EdrWersja { Nr = "19102", Odj = "11:05", Uwagi = "Po torze nr 77" } });
            var bz2 = W9(new EdrPolecenie { Dziennik = dwa9.Id, Rodzaj = "wypr", Dane = new EdrWersja { Nr = "19104", Odj = "11:06", Uwagi = $"Po torze nr {tLewy.Tor}, po torze nr {tPrawy.Tor}" } });
            var bz3 = zasW == null ? new CmdResult(false, "") : W9(new EdrPolecenie { Dziennik = dwa9.Id, Rodzaj = "wypr", Dane = new EdrWersja { Nr = "19106", Odj = "11:07", Lewy = true, Uwagi = $"Po torze nr {zasW.Tor}" } });
            var bz4 = W9(new EdrPolecenie { Dziennik = dwa9.Id, Rodzaj = "pole", Wiersz = ww.Id, Pole = "Uwagi", Wartosc = "po torze lewym, po torze prawym" });
            K(rw.Ok && zaj9?.Tor == tLewy.Tor && !bz1.Ok && !bz2.Ok && !bz3.Ok && !bz4.Ok,
                $"tor z uwag: „Po torze nr {tLewy.Tor}” – zajęty tor {zaj9?.Tor}; nr 77 – {bz1.Msg}; dwa numery – {bz2.Msg}; lewy + tor zasadniczy {zasW?.Tor} – {bz3.Msg}; lewy i prawy – {bz4.Msg}");

            // wykreślenie: przyczyna obowiązkowa, wpis zostaje z nową wersją, nie zajmuje toru, nie da się go zmienić ani wykreślić drugi raz
            int wersjiPrzed = ww.Wersje.Count;
            var wk0 = W9(new EdrPolecenie { Dziennik = dwa9.Id, Rodzaj = "wykresl", Wiersz = ww.Id, Wartosc = "  " });
            var wk1 = W9(new EdrPolecenie { Dziennik = dwa9.Id, Rodzaj = "wykresl", Wiersz = ww.Id, Wartosc = "pomyłka w numerze" });
            var wk2 = W9(new EdrPolecenie { Dziennik = dwa9.Id, Rodzaj = "pole", Wiersz = ww.Id, Pole = "Prz", Wartosc = "11:10" });
            var wk3 = W9(new EdrPolecenie { Dziennik = dwa9.Id, Rodzaj = "wykresl", Wiersz = ww.Id, Wartosc = "drugi raz" });
            var tekst9 = dwa9.Strony[^1].Wiersze.First(w => w.Rodzaj == "tekst");
            var wk4 = W9(new EdrPolecenie { Dziennik = dwa9.Id, Rodzaj = "wykresl", Wiersz = tekst9.Id, Wartosc = "próba" });
            e9.StanSzlaku(dwa9);
            K(!wk0.Ok && wk1.Ok && ww.Wykreslony && ww.Kompletny && ww.Wersje.Count == wersjiPrzed + 1 && ww.W.Wykreslono == "pomyłka w numerze" && !wk2.Ok && !wk3.Ok && !wk4.Ok
                && !dwa9.ToryStan.Any(t => t.Pociagi.Contains(ww.W.Nr)),
                $"wykreślenie: bez przyczyny – {wk0.Msg}; „{wk1.Msg}”, wersji {ww.Wersje.Count}, tor zwolniony; zmiana – {wk2.Msg}; ponownie – {wk3.Msg}; wpis o dyżurze – {wk4.Msg}");
            var wy9 = EdrWyciag.Dziennik(dwa9, "", DateTime.MinValue, DateTime.MaxValue, false);
            K(wy9.Wiersze.Any(x => x.Wykreslony && x.Uwagi.Contains("Wykreślono: pomyłka w numerze")), "wyciąg: wpis wykreślony z przyczyną w uwagach");
        }
        {
            // zapis dzienników do pliku obok układu (kopia w katalogu tymczasowym): tmp + .bak, odczyt przy następnym uruchomieniu – ciągła historia
            var zapis0 = Edr.ZapisDziennikow; Edr.ZapisDziennikow = true;
            try
            {
                var st10 = Station.Load(kopia);
                var s10 = new Simulator(st10, options: options) { User = user };
                var k10 = s10.AddDesk("PC", "Stanowisko 1", true); k10.User = user;
                var e10 = s10.Edr; e10.PlikUstawien = null; s10.Tick(0.25);
                var plik = e10.PlikDziennikow;
                if (File.Exists(plik)) File.Delete(plik);
                var d10 = e10.Dzienniki.First(d => d.Szlaki.Count == 2 && st10.Blocks[d.Szlaki[0]].Ends.Count == 1);
                var r10 = e10.Wykonaj(k10, new EdrPolecenie { Dziennik = d10.Id, Rodzaj = "wypr", Dane = new EdrWersja { Nr = "19200", Tor = "1", Odj = "12:00" } });
                bool jest1 = File.Exists(plik);
                var r10b = e10.Wykonaj(k10, new EdrPolecenie { Dziennik = d10.Id, Rodzaj = "wpis", Wartosc = "Wpis do sprawdzenia zapisu" });
                bool bak = File.Exists(plik + ".bak") && !File.Exists(plik + ".tmp");
                int maxId = e10.Dzienniki.SelectMany(d => d.Strony.SelectMany(s => s.Wiersze)).Max(w => w.Id);
                K(r10.Ok && r10b.Ok && jest1 && bak && plik.EndsWith(".edr_dzienniki.json"), $"zapis dzienników: {Path.GetFileName(plik)} po wpisie, kopia .bak po kolejnym, bez pliku .tmp");

                // następne uruchomienie: te same strony (historia), wiersze z pliku nie zajmują torów, kolejne numery wierszy dalej
                var s11 = new Simulator(Station.Load(kopia), options: options) { User = user };
                var k11 = s11.AddDesk("PC", "Stanowisko 1", true); k11.User = user;
                var e11 = s11.Edr; e11.PlikUstawien = null; s11.Tick(0.25);
                var d11 = e11.Dzienniki.First(d => d.Id == d10.Id);
                var w11 = d11.Strony.SelectMany(s => s.Wiersze).FirstOrDefault(w => w.W?.Nr == "19200");
                e11.StanSzlaku(d11);
                var r11 = e11.Wykonaj(k11, new EdrPolecenie { Dziennik = d11.Id, Rodzaj = "przyj", Dane = new EdrWersja { Nr = "19201", Tor = "1" } });
                int nowyId = Ost11(d11).Id;
                EdrWiersz Ost11(EdrDziennik d) => d.Strony[^1].Wiersze[^1];
                K(w11 != null && w11.Wczytany && d11.Strony.SelectMany(s => s.Wiersze).Any(w => w.Tekst == "Wpis do sprawdzenia zapisu") && !d11.ToryStan.Any(t => t.Pociagi.Contains("19200"))
                    && r11.Ok && nowyId > maxId && e11.BladDziennikow == null,
                    $"odczyt przy następnym uruchomieniu: wpisy z pliku jako historia (bez zajętości toru), nowy wiersz nr {nowyId} > {maxId}");

                // uszkodzony plik – nie jest zastępowany, zapis wyłączony, komunikat
                File.WriteAllText(plik, "{ to nie jest plik dzienników");
                var s12 = new Simulator(Station.Load(kopia), options: options) { User = user };
                var k12 = s12.AddDesk("PC", "Stanowisko 1", true); k12.User = user;
                var e12 = s12.Edr; e12.PlikUstawien = null; s12.Tick(0.25);
                var d12 = e12.Dzienniki.First(d => d.Id == d10.Id);
                e12.Wykonaj(k12, new EdrPolecenie { Dziennik = d12.Id, Rodzaj = "wpis", Wartosc = "po błędzie" }); s12.Tick(0.25);
                K(e12.BladDziennikow != null && File.ReadAllText(plik) == "{ to nie jest plik dzienników" && e12.DlaStanowiska(k12).BladDziennikow != null,
                    $"uszkodzony plik dzienników zostaje bez zmian: {e12.BladDziennikow}");
                foreach (var f in new[] { plik, plik + ".bak" }) if (File.Exists(f)) File.Delete(f);
            }
            finally { Edr.ZapisDziennikow = zapis0; }
        }

        // ---------------- E8e: wzory telefonogramów (Data/telefonogramy.json) – treść z pól, skutek dla stanu szlaku
        sb.AppendLine("--- E8e");
        {
            var nry = new[] { "9", "10", "11", "12", "16", "16a", "17", "17a", "21", "21a", "022", "22", "23", "24", "25", "26", "27", "28", "29", "30", "31", "32" };
            K(EdrWzory.Blad == null && EdrWzory.Lista.Select(w => w.Nr).SequenceEqual(nry) && EdrWzory.Sprawdz() == null, $"wzory telefonogramów: {EdrWzory.Lista.Count} ({EdrWzory.Blad ?? "plik poprawny"})");
            var w21 = EdrWzory.Wzor("21");
            var p21 = new Dictionary<string, string> { ["tor"] = "1", ["tryb"] = "", ["godz"] = "1015", ["powod_slowo"] = "z powodu", ["powod"] = "robót torowych" };
            var t21 = EdrWzory.Tresc(w21, p21, "Łódź Widzew", "Łódź Chojny", new[] { "1", "2" }).Tresc;
            p21["tryb"] = "planowo"; var t21p = EdrWzory.Tresc(w21, p21, "Łódź Widzew", "Łódź Chojny", new[] { "1", "2" }).Tresc;
            K(t21 == "Tor numer 1 od Łódź Widzew do Łódź Chojny zamknięty od 10:15 z powodu robót torowych." && t21p.Contains("zamknięty planowo od 10:15"), $"treść ze wzoru 21: „{t21}”; z trybem: „{t21p}”");

            var s13 = new Simulator(st, options: options) { User = user };
            var k13 = s13.AddDesk("PC", "Stanowisko 1", true); k13.User = user;
            var e13 = s13.Edr; e13.PlikUstawien = null; s13.Tick(0.25);
            var d13 = e13.Dzienniki.First(d => d.Szlaki.Count == 2 && st.Blocks[d.Szlaki[0]].Ends.Count == 1);
            var tor13 = d13.Biezace.Tory[0];
            CmdResult T13(string nr, Dictionary<string, string> pola, bool nadaje = true, string dz = "") => e13.Wykonaj(k13, new EdrPolecenie { Rodzaj = "telefonogram", Dziennik = dz == "" ? d13.Id : dz, Wartosc = d13.Post,
                Tel = new EdrTelefonogram { Wzor = nr, Pola = pola, Nadaje = nadaje, Odebral = "Adam Nowak", Nadal = "Adam Nowak", NadalPost = "Sąsiad", OdebralPost = "Sąsiad" } });
            string Stat() { e13.StanSzlaku(d13); return d13.Biezace.Tory.First(t => t.Szlak == tor13.Szlak).Status; }
            EdrTelefonogram Ost13() => d13.Strony[^1].Wiersze[^1].Tel;

            var r21 = T13("21", new() { ["tor"] = tor13.Tor, ["godz"] = "10:15", ["powod_slowo"] = "celem", ["powod"] = "naprawy" });
            var tel21 = Ost13(); var st21 = Stat();
            var r22 = T13("22", new() { ["tor"] = tor13.Tor, ["godz"] = "11:00" }); var st22 = Stat();
            K(r21.Ok && tel21.WzorNr == "21" && tel21.Tresc.StartsWith($"Tor numer {tor13.Tor} od ") && tel21.Tresc.Contains($" do {d13.Sasiad} zamknięty od 10:15 celem naprawy") && st21 == "Zamknięty" && r22.Ok && st22 == "Otwarty",
                $"wzór 21 z dziennika ruchu: „{tel21?.Tresc}” – tor {tor13.Tor} {st21}; wzór 22 – {st22}");

            var r21a = T13("21a", new() { ["tor"] = tor13.Tor, ["pociag"] = "19501", ["powod_slowo"] = "z powodu", ["powod"] = "usterki" }); var przed21a = Stat();
            e13.Wykonaj(k13, new EdrPolecenie { Dziennik = d13.Id, Rodzaj = "przyj", Dane = new EdrWersja { Nr = "19501", Tor = "1", Odj = "12:00", Prz = "12:06" } });
            var po = Stat();
            T13("22", new() { ["tor"] = tor13.Tor, ["godz"] = "12:30" });
            K(r21a.Ok && przed21a == "Otwarty" && po == "Zamknięty", $"wzór 21a: przed przyjazdem 19501 tor {przed21a}, po przyjeździe {po}");

            var r16 = T13("16", new() { ["tor"] = tor13.Tor, ["godz"] = "13:00", ["powod"] = "usterki blokady" }); Stat();
            bool dw16 = Edr.Wymagane(d13.Biezace, d13.Ustawienia).Contains("Dw"); var pod16 = d13.Biezace.Tory.First(t => t.Szlak == tor13.Szlak).Podstawa;
            var r17 = T13("17", new() { ["tor"] = tor13.Tor, ["godz"] = "14:00" }); Stat();
            var pod17 = d13.Biezace.Tory.First(t => t.Szlak == tor13.Szlak).Podstawa;
            K(r16.Ok && pod16 == Edr.Podstawy[3] && dw16 && r17.Ok && pod17 == d13.Ustawienia.Tory.First(t => t.Szlak == tor13.Szlak).Podstawa,
                $"wzór 16: prowadzenie ruchu „{pod16}”, droga wolna wymagana; wzór 17: z powrotem zasadnicze („{pod17 ?? "z urządzenia"}”)");

            // zasadniczo tory jednokierunkowe (lewy – do nas, prawy – od nas): 23 wprowadza ruch dwukierunkowy, 25 przywraca zasadniczy
            var u13 = d13.Ustawienia.Kopia(); foreach (var t in u13.Tory) { t.Rodzaj = "Jednokierunkowy"; t.Zasadniczy = t.Strona == "lewy" ? -1 : 1; }
            e13.Wykonaj(k13, new EdrPolecenie { Dziennik = d13.Id, Rodzaj = "ustawienia", Ustawienia = u13 });
            var zas13 = d13.Ustawienia.Tory.Select(t => t.Rodzaj).ToList();
            var r23 = T13("23", new() { ["tor"] = tor13.Tor, ["pociag"] = "19503", ["odjechal"] = "14:10" }); Stat();
            var rod23 = d13.Biezace.Tory.First(t => t.Szlak == tor13.Szlak).Rodzaj;
            var r25 = T13("25", new() { ["godz"] = "15:00" }); Stat();
            K(r23.Ok && rod23 == "Dwukierunkowy" && zas13.All(x => x == "Jednokierunkowy") && r25.Ok && d13.Biezace.Tory.Select(t => t.Rodzaj).SequenceEqual(zas13), $"wzór 23: ruch {rod23.ToLowerInvariant()}; wzór 25: rodzaj ruchu jak w ustawieniach ({string.Join(", ", zas13)})");

            var bw1 = T13("21", new() { ["tor"] = tor13.Tor, ["godz"] = "10:15", ["powod_slowo"] = "celem" });
            var bw2 = T13("22", new() { ["tor"] = "77", ["godz"] = "11:00" });
            var bw3 = T13("99", new());
            var rOdb = T13("10", new() { ["godz"] = "16:00" }, nadaje: false);
            var rTel = T13("9", new() { ["godz"] = "16:05" }, dz: null);
            var telF = e13.Telefoniczny.GetValueOrDefault(d13.Post)?.LastOrDefault();
            var r28 = T13("28", new() { ["godz"] = "16:10" }, nadaje: false); var t28 = Ost13();
            K(!bw1.Ok && !bw2.Ok && !bw3.Ok && rOdb.Ok && rTel.Ok && telF?.Tresc == "Nie wyprawiać pociągów od 16:05 aż do odwołania." && r28.Ok && t28.Tresc.StartsWith($"Posterunek {d13.Sasiad} otwarty o godzinie 16 minut 10"),
                $"wzory: bez powodu – {bw1.Msg}; tor 77 – {bw2.Msg}; nieznany wzór – {bw3.Msg}; z dziennika telefonicznego: „{telF?.Tresc}”; 28 odebrany: „{t28?.Tresc}”");
        }

        // ---------------- kontrolka torów stacyjnych (jak w EDR.exe): rezerwacja, pociąg postawiony ręcznie, przesunięcie, usunięcie
        sb.AppendLine("--- Tory stacyjne");
        {
            var s14 = new Simulator(st, options: options) { User = user };
            var k14 = s14.AddDesk("PC", "Stanowisko 1", true); k14.User = user;
            var e14 = s14.Edr; e14.PlikUstawien = null; s14.Tick(0.25);
            var d14 = e14.Dzienniki.First(d => e14.ToryStacyjne.GetValueOrDefault(d.Post)?.Count >= 3);
            var post14 = d14.Post; var ts = e14.ToryStacyjne[post14];
            string tA = ts[0], tB = ts[1], tC = ts[2];
            CmdResult W14(EdrPolecenie p) => e14.Wykonaj(k14, p);
            CmdResult Tor14(string pole, string tor = null, string klucz = null, string nr = null) => W14(new EdrPolecenie { Rodzaj = "torst", Post = post14, Pole = pole, Wartosc = tor, Klucz = klucz, Dane = nr == null ? null : new EdrWersja { Nr = nr } });
            EdrTorStacyjny T(string tor) => e14.ZajetoscTorowStacyjnych(post14).First(x => x.Tor == tor);

            W14(new EdrPolecenie { Dziennik = d14.Id, Rodzaj = "przyj", Dane = new EdrWersja { Nr = "19601", Tor = tA, Odj = "08:00" } });
            var wr = d14.Strony[^1].Wiersze[^1];
            bool rez = T(tA).Zarezerwowany && !T(tA).Zajety;
            W14(new EdrPolecenie { Dziennik = d14.Id, Rodzaj = "pole", Wiersz = wr.Id, Pole = "Prz", Wartosc = "08:05" });
            K(rez && T(tA).Zajety && T(tA).Pociag == "19601" && T(tA).Od == "08:05", $"tor {tA}: przyjęcie z torem – rezerwacja, po „przyjechał” 08:05 – zajęty przez 19601");

            var rd = Tor14("dodaj", tB, nr: "70001");
            var bd1 = Tor14("dodaj", tB, nr: "70001"); var bd2 = Tor14("dodaj", "ZZ9", nr: "70003"); var bd3 = Tor14("dodaj", tC, nr: " ");
            var na = T(tB).Pociagi.FirstOrDefault();
            K(rd.Ok && na?.Recznie == true && na.Nr == "70001" && T(tB).Zajety && !bd1.Ok && !bd2.Ok && !bd3.Ok,
                $"postawienie 70001 na torze {tB}: {rd.Msg}; ponownie – {bd1.Msg}; tor spoza posterunku – {bd2.Msg}; bez numeru – {bd3.Msg}");

            var rp = Tor14("przesun", tC, na?.Klucz);
            var rpw = Tor14("przesun", tB, "w:" + wr.Id);
            K(rp.Ok && T(tC).Pociagi.Any(x => x.Nr == "70001") && rpw.Ok && T(tB).Pociag == "19601" && !T(tA).Zajety, $"przesunięcie: {rp.Msg}; pociąg z wpisu – {rpw.Msg}");

            // odjazd zwalnia tor: wyprawienie wpisane po postawieniu; wiersz wyprawienia sprzed postawienia z „odjechał” wpisanym później
            W14(new EdrPolecenie { Dziennik = d14.Id, Rodzaj = "wypr", Dane = new EdrWersja { Nr = "70001", Tor = tC, Odj = "08:20" } });
            bool wolnyC = !T(tC).Pociagi.Any(x => x.Nr == "70001");
            W14(new EdrPolecenie { Dziennik = d14.Id, Rodzaj = "wypr", Dane = new EdrWersja { Nr = "70005", Tor = tA } });
            var ww = d14.Strony[^1].Wiersze[^1];
            Tor14("dodaj", tA, nr: "70005"); bool stoi = T(tA).Pociagi.Any(x => x.Nr == "70005");
            W14(new EdrPolecenie { Dziennik = d14.Id, Rodzaj = "pole", Wiersz = ww.Id, Pole = "Odj", Wartosc = "08:30" });
            K(wolnyC && stoi && !T(tA).Pociagi.Any(x => x.Nr == "70005"), "odjazd zwalnia tor pociągu postawionego ręcznie (także „odjechał” wpisane później w istniejącym wierszu)");

            var rus = Tor14("usun", klucz: "w:" + wr.Id); var rus2 = Tor14("usun", klucz: "w:" + wr.Id);
            K(rus.Ok && !T(tB).Zajety && !rus2.Ok, $"usunięcie z toru: {rus.Msg}; drugi raz – {rus2.Msg}");
        }

        try { Directory.Delete(kat, true); } catch { }
        sb.Insert(0, $"EDR: kontrola {ok} z {tot}{Environment.NewLine}");
        File.WriteAllText(outPath, sb.ToString());
    }
}

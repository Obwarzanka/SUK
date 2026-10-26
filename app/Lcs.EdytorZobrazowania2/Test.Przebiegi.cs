using System.IO;
using System.Text.Json.Nodes;
using System.Windows.Controls;
using Lcs.Model;
using LcsWidzew.Sim;
using Stacja = LcsWidzew.Model.Station;

namespace Lcs.EdytorZobrazowania2;

/// <summary>
/// Etap 3 – tablica przebiegów w edytorze zobrazowania (ROZWOJ.md, decyzja właściciela 2026-10-26): propozycje z układu, przyjęcie,
/// usunięcie, zasadniczy, cofanie, Sprawdź, test każdego przebiegu, zapis w pliku zobrazowania i eksport – symulator bierze tylko tablicę.
/// </summary>
public static partial class Test
{
    static async Task RunPrzebiegi(MainWindow w, string shot)
    {
        var dir = Path.Combine(Path.GetTempPath(), "edytor_zob2_test_przebiegi"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        var ut = Path.Combine(dir, "mijanka_testowa.ut.json"); var v1 = Path.Combine(dir, "mijanka_testowa.zob.json");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Testy", "mijanka_testowa.ut.json"), ut);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Testy", "mijanka_testowa.zob.json"), v1);
        Ok(w.OpenPath(v1) && w.SaveDoc(false), "mijanka_testowa – wersja 1 otwarta i zapisana jako wersja 2");
        await Settle(w);
        Ok(w.Doc.P.Przebiegi == null, "nowe zobrazowanie: tablica przebiegów nie zaprojektowana");

        // --- bez projektu: eksport bez pola „przebiegi”, symulator wyznacza przebiegi jak dotąd
        var sim0 = Path.Combine(dir, "bez_projektu.json");
        var uw0 = w.DoSymulatora(sim0);
        var j0 = JsonNode.Parse(File.ReadAllText(sim0));
        int n0 = new Simulator(Stacja.Load(sim0)).RouteTable.Values.Sum(v => v.Count);
        Ok(j0["przebiegi"] == null && n0 > 0 && uw0.Any(u => u.Contains("tablica przebiegów nie jest zaprojektowana")),
            $"bez projektu: plik bez „przebiegi”, symulator wyznacza {n0} przebiegów sam, uwaga przy eksporcie");

        // --- okno Przebiegi: propozycje z układu
        var pw = w.Przebiegi();
        Ok(pw != null && pw.Uklad.Count >= n0 && pw.Wiersze.All(x => !x.WTablicy) && pw.Status.Text.Contains("nie zaprojektowana"),
            $"Przebiegi (F6): propozycji z układu {pw?.Uklad.Count}, wszystkie szare (propozycje) – {pw?.Status.Text}");
        var sigA = pw.Uklad.First(r => r.Type == "P").Start;
        pw.SigBox.SelectedItem = Stacja.Lbl(sigA);
        Ok(pw.Wiersze.Count > 0 && pw.Wiersze.All(x => x.Od == Stacja.Lbl(sigA)), $"filtr „Od {Stacja.Lbl(sigA)}”: {pw.Wiersze.Count} wierszy tylko od tego sygnalizatora");
        foreach (var x in pw.Wiersze.Where(x => x.R.Type == "P")) pw.Grid.SelectedItems.Add(x);
        int zazn = pw.Grid.SelectedItems.Count;
        pw.Przyjmij();
        Ok(w.Doc.P.Przebiegi?.Count == zazn && w.Doc.P.Przebiegi.All(p => p.Od == sigA && p.Typ == "P"), $"Przyjmij: {zazn} przebiegów pociągowych od {Stacja.Lbl(sigA)} w tablicy");
        Ok(w.Doc.P.Przebiegi.GroupBy(p => p.Do).All(g => g.Count(p => p.Zasadniczy) == 1), "propozycja zasadniczego: dla każdego końca jeden (wariant 0 z układu)");
        w.Doc.Undo();
        Ok(w.Doc.P.Przebiegi == null, "Ctrl+Z: tablica znowu nie zaprojektowana");
        w.Doc.Redo();

        // --- wszystkie, potem człowiek usuwa niepotrzebne i wybiera zasadniczy
        pw.SigBox.SelectedItem = "wszystkie";
        pw.PrzyjmijWidoczne();
        Ok(w.Doc.P.Przebiegi.Count == pw.Uklad.Count, $"Przyjmij wszystkie widoczne: {w.Doc.P.Przebiegi.Count} w tablicy");
        var grp = w.Doc.P.Przebiegi.GroupBy(p => (p.Typ, p.Od, p.Do)).FirstOrDefault(g => g.Count() > 1);
        var usun = w.Doc.P.Przebiegi.First(p => p.Typ == "M" && (grp == null || p.Od != grp.Key.Od || p.Do != grp.Key.Do));
        pw.Grid.SelectedItems.Clear(); pw.Grid.SelectedItems.Add(pw.Wiersze.First(x => x.Klucz == usun.Klucz));
        pw.Usun();
        Ok(!w.Doc.P.Przebiegi.Any(p => p.Klucz == usun.Klucz) && pw.Wiersze.First(x => x.Klucz == usun.Klucz).Tablica == "propozycja",
            $"Usuń z tablicy: {TablicaPrzebiegow.Opis(usun)} – z powrotem propozycja");
        string zasKlucz = null;
        if (grp != null)
        {
            var war = grp.First(p => !p.Zasadniczy);
            zasKlucz = war.Klucz;
            pw.Grid.SelectedItems.Clear(); pw.Grid.SelectedItems.Add(pw.Wiersze.First(x => x.Klucz == zasKlucz));
            pw.Zasadniczy();
            var g2 = w.Doc.P.Przebiegi.Where(p => (p.Typ, p.Od, p.Do) == grp.Key).ToList();
            Ok(g2.Count(p => p.Zasadniczy) == 1 && g2.First(p => p.Zasadniczy).Klucz == zasKlucz, $"Zasadniczy: {TablicaPrzebiegow.Opis(war)} (pozostałe {g2.Count - 1} – wariantowe)");
        }
        else Ok(true, "Zasadniczy: w układzie nie ma pary z kilkoma wariantami – pominięte");
        var spr = pw.Sprawdz();
        Ok(spr.Count(u => u.Blad) == 0, $"Sprawdź: błędów 0 ({pw.UwagiBox.Text.Split('\n')[0]})");

        // --- eksport: symulator bierze tylko tablicę
        var sim1 = Path.Combine(dir, "z_projektem.json");
        w.DoSymulatora(sim1);
        var j1 = JsonNode.Parse(File.ReadAllText(sim1));
        var s1 = new Simulator(Stacja.Load(sim1));
        var tab = s1.RouteTable.Values.SelectMany(v => v).ToList();
        Ok(j1["przebiegi"] is JsonArray ja && ja.Count == w.Doc.P.Przebiegi.Count, $"Do symulatora: pole „przebiegi” – {w.Doc.P.Przebiegi.Count} wpisów");
        Ok(tab.Count == w.Doc.P.Przebiegi.Count && !tab.Any(r => TablicaPrzebiegow.Pasuje(usun, r)) && s1.Kz.BrakujacePrzebiegi.Count == 0,
            $"symulator: w tablicy {tab.Count} przebiegów = tablica z edytora, usuniętego nie ma");
        if (zasKlucz != null)
            Ok(tab.FirstOrDefault(r => TablicaPrzebiegow.Z(r).Klucz == zasKlucz)?.Variant == 0, "symulator: przebieg wskazany jako zasadniczy ma wariant 0 (bez punktów pośrednich)");

        // --- przebieg, którego nie ma w układzie (np. po zmianie UT)
        var zly = new ObrPrzebieg { Typ = "P", Od = sigA, Do = usun.Do, Zwr = "XX:99+" };
        w.Doc.Change("test: przebieg spoza układu", q => q.Przebiegi.Add(zly));
        spr = pw.Sprawdz();
        Ok(spr.Any(u => u.Blad && u.Tekst.Contains("nie ma takiego przebiegu w układzie")) && pw.Wiersze.Any(x => x.Brak), "Sprawdź: przebieg spoza układu – błąd, wiersz czerwony „brak w układzie”");
        var sim2 = Path.Combine(dir, "z_bledem.json");
        var uw2 = w.DoSymulatora(sim2);
        var s2 = new Simulator(Stacja.Load(sim2));
        Ok(uw2.Any(u => u.StartsWith("przebiegi:")) && s2.Kz.BrakujacePrzebiegi.Count == 1 && s2.RouteTable.Values.Sum(v => v.Count) == tab.Count,
            $"eksport z błędem: uwaga, symulator pomija go z uwagą („{s2.Kz.BrakujacePrzebiegi.FirstOrDefault()}”), reszta tablicy bez zmian");
        w.Doc.Undo();

        // --- test każdego przebiegu
        var t = pw.Testuj();
        int sy = t.Count(x => x.Wynik == TablicaPrzebiegow.Test.Sygnal);
        Ok(t.Count == w.Doc.P.Przebiegi.Count && sy > 0 && pw.Wiersze.Where(x => x.WTablicy).All(x => x.Test.Length > 0),
            $"Testuj: {pw.UwagiBox.Text.Split('\n')[0]}");
        foreach (var x in t.Where(x => x.Wynik != TablicaPrzebiegow.Test.Sygnal)) log.AppendLine($"      {TablicaPrzebiegow.Opis(x.P)}: {x.Wynik} – {x.Tekst}");

        // --- zapis i odczyt
        Ok(w.SaveDoc(false), "zapis zobrazowania");
        var zap = ObrDocument.Load(w.Doc.Path).P.Przebiegi;
        Ok(zap?.Count == w.Doc.P.Przebiegi.Count && zap.Count(p => p.Zasadniczy) == w.Doc.P.Przebiegi.Count(p => p.Zasadniczy), $"odczyt pliku: tablica {zap?.Count} przebiegów");

        // --- podgląd pulpitu z tablicy
        var pg = w.Podglad();
        Ok(pg != null && pg.Sim.RouteTable.Values.Sum(v => v.Count) == w.Doc.P.Przebiegi.Count, $"podgląd pulpitu: tablica z edytora ({w.LastMessage})");
        pg?.Close();

        // --- panel „Przebiegi od …” jak w starym edytorze: klik na semafor na obrazie
        pw.Close();
        var semEl = w.Obraz.Elementy.First(e => e.Typ == ObrSymbole.Semafor && w.SygnalElementu(e) == sigA);
        w.Side.SelectedItem = w.TabWlasciwosci;
        w.View.Select(null, null, new[] { semEl.Id });
        await Settle(w);
        var panel = w.PrzebiegiPanel;
        var cbs = panel == null ? new() : All<CheckBox>(panel).ToList();
        int odA = w.ModelUkladu().Routes.Count(r => r.Start == sigA);
        int wTabA = w.Doc.P.Przebiegi.Count(p => p.Od == sigA);
        Ok(panel != null && cbs.Count == odA && cbs.Count(c => c.IsChecked == true) == wTabA, $"klik na semafor {Stacja.Lbl(sigA)}: panel „Przebiegi od {Stacja.Lbl(sigA)}” – {cbs.Count} przebiegów, ✓ przy {wTabA} z tablicy");
        int przed = w.Doc.P.Przebiegi.Count;
        cbs[0].IsChecked = false; Click(cbs[0]); await Settle(w);
        Ok(w.Doc.P.Przebiegi.Count == przed - 1, "odznaczenie ✓ – przebieg poza tablicą");
        cbs = All<CheckBox>(w.PrzebiegiPanel).ToList();
        cbs[0].IsChecked = true; Click(cbs[0]); await Settle(w);
        Ok(w.Doc.P.Przebiegi.Count == przed, "ponowne ✓ – przebieg w tablicy");
        var vBox = All<ComboBox>(w.PrzebiegiPanel).First();
        vBox.SelectedIndex = Array.IndexOf(LcsWidzew.Model.Zaleznosci.Predkosci, 60) + 1; await Settle(w);
        var pV = w.Doc.P.Przebiegi.FirstOrDefault(p => p.Od == sigA && p.Typ == "P" && p.V == 60);
        Ok(pV != null, $"V w panelu: {(pV == null ? "–" : TablicaPrzebiegow.Opis(pV))} – 60 km/h");
        var sim3 = Path.Combine(dir, "z_predkoscia.json");
        w.DoSymulatora(sim3);
        var s3 = new Simulator(Stacja.Load(sim3));
        var r3 = s3.RouteTable.Values.SelectMany(v => v).FirstOrDefault(r => pV != null && TablicaPrzebiegow.Pasuje(pV, r));
        Ok(r3?.VSet == 60, $"symulator: prędkość z edytora – {r3?.Id} VSet {r3?.VSet:0} km/h");
        var droga = w.ModelUkladu().Routes.First(r => r.Start == sigA && r.Type == "P" && r.Sw.Count > 0);
        int lam = w.PokazDroge(droga);
        Ok(lam > 0 && w.View.Droga?.Sum(l => l.Count) >= 2, $"klik na przebiegu {droga.Id}: droga na obrazie – {lam} łamanych ({w.LastMessage})");
        var testA = w.TestujOd(sigA);
        Ok(testA.Count == w.Doc.P.Przebiegi.Count(p => p.Od == sigA) && All<TextBlock>(w.PrzebiegiPanel).Any(t => t.Text.Contains("test:")), $"Testuj w panelu: {w.LastMessage}");
        if (shot != null) { w.View.Fit(); await Settle(w); Save(w, Path.ChangeExtension(shot, null) + "_panel.png"); }
        w.View.Select(null, null, null); await Settle(w);
        Ok(w.View.Droga == null, "zmiana zaznaczenia – droga znika z obrazu");
        pw = w.Przebiegi();

        if (shot != null)
        {
            pw.SigBox.SelectedItem = "wszystkie";
            OffScreen(pw); pw.Show(); await Settle(pw);
            Save(pw, shot);
        }
        pw.Close();
        await ZasadniczyLcs(w);
    }

    /// <summary>Wybór przebiegu zasadniczego na układzie z wariantami (lcs_testowe, dwie stacje): w symulatorze wariant 0, polecenie bez punktów pośrednich.</summary>
    static async Task ZasadniczyLcs(MainWindow w)
    {
        string src = null;
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null && src == null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "projekty_ut", "lcs_testowe", "lcs_testowe.zob.json"))) src = Path.Combine(d.FullName, "projekty_ut", "lcs_testowe");
        if (src == null) { Ok(true, "lcs_testowe – brak w repozytorium, zasadniczy pominięty"); return; }
        var dir = Path.Combine(Path.GetTempPath(), "edytor_zob2_test_przebiegi_lcs"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        foreach (var f in new[] { "lcs_testowe.ut.json", "lcs_testowe.zob.json" }) File.Copy(Path.Combine(src, f), Path.Combine(dir, f));
        Ok(w.OpenPath(Path.Combine(dir, "lcs_testowe.zob.json")), "lcs_testowe otwarte");
        await Settle(w);
        var pw = w.Przebiegi();
        pw.PrzyjmijWidoczne();
        var grp = w.Doc.P.Przebiegi.GroupBy(p => (p.Typ, p.Od, p.Do)).Where(g => g.Count() > 1).OrderBy(g => g.Key.Typ).FirstOrDefault();
        if (grp == null) { Ok(false, "lcs_testowe: brak pary z kilkoma wariantami"); pw.Close(); return; }
        var war = grp.First(p => !p.Zasadniczy);
        pw.Grid.SelectedItems.Clear(); pw.Grid.SelectedItems.Add(pw.Wiersze.First(x => x.Klucz == war.Klucz));
        pw.Zasadniczy();
        var g2 = w.Doc.P.Przebiegi.Where(p => (p.Typ, p.Od, p.Do) == grp.Key).ToList();
        Ok(g2.Count(p => p.Zasadniczy) == 1 && g2.First(p => p.Zasadniczy).Klucz == war.Klucz, $"lcs_testowe – Zasadniczy: {TablicaPrzebiegow.Opis(war)} (wariantów {g2.Count})");
        var sim = Path.Combine(dir, "lcs.json");
        w.DoSymulatora(sim);
        var s = new Simulator(Stacja.Load(sim));
        var rd = s.RouteTable.Values.SelectMany(v => v).FirstOrDefault(r => TablicaPrzebiegow.Pasuje(war, r));
        Ok(rd?.Variant == 0 && s.FindRoute(war.Od, war.Do, war.Typ) == rd, $"symulator: {rd?.Id} – wariant 0, polecenie bez punktów pośrednich wybiera ten przebieg");
        pw.Close();
        await WlasnyLcs(w, dir);
    }

    /// <summary>
    /// Własny przebieg (decyzja właściciela 2026-10-27): w panelu semafora koniec i położenie zwrotnicy – program pokazuje tylko drogi
    /// przez tę zwrotnicę w tym położeniu (także spoza propozycji); „Dodaj” – do tablicy, Sprawdź bez błędu, symulator ma ten przebieg.
    /// </summary>
    static async Task WlasnyLcs(MainWindow w, string dir)
    {
        var m = w.ModelUkladu();
        // para początek – koniec z największą liczbą dróg w układzie (bez limitu propozycji)
        var para = m.Routes.Where(r => r.Type == "P").Select(r => (r.Start, r.End)).Distinct()
            .Select(k => (k.Start, k.End, Drogi: TablicaPrzebiegow.Drogi(m, k.Start, "P", k.End))).OrderByDescending(x => x.Drogi.Count).First();
        int prop = m.Routes.Count(r => r.Type == "P" && r.Start == para.Start && r.End == para.End);
        Ok(para.Drogi.Count >= prop && para.Drogi.Count > 1, $"własny: {Stacja.Lbl(para.Start)} → {Stacja.Lbl(para.End)} – dróg w układzie {para.Drogi.Count}, propozycji {prop}");
        // zwrotnica, której położenie rozróżnia drogi; wybór położenia rzadszego
        var zw = para.Drogi.SelectMany(r => r.Sw).GroupBy(q => q).Where(g => g.Count() < para.Drogi.Count).OrderBy(g => g.Count()).First().Key;
        ObrElement semEl = null;
        foreach (var o in w.Doc.P.Obrazy)
        {
            w.ShowImage(o.Id); await Settle(w);
            semEl = w.Obraz.Elementy.FirstOrDefault(e => e.Typ == ObrSymbole.Semafor && w.SygnalElementu(e) == para.Start);
            if (semEl != null) break;
        }
        if (semEl != null)
        {
            w.Side.SelectedItem = w.TabWlasciwosci;
            w.View.Select(null, null, new[] { semEl.Id }); await Settle(w);
        }
        var wyn = w.WlasnyPrzebieg(para.Start, "P", para.End, new Dictionary<string, string> { [zw.Num] = zw.Pos });
        await Settle(w);
        Save(w, Path.Combine(dir, "wlasny_panel.png"));
        Ok(wyn.Count > 0 && wyn.Count < para.Drogi.Count && wyn.All(r => r.Sw.Contains(zw)),
            $"własny: zwrotnica {Stacja.Lbl(zw.Num)}{zw.Pos} – {wyn.Count} z {para.Drogi.Count} dróg, wszystkie przez nią w tym położeniu");
        var spoza = wyn.FirstOrDefault(r => !m.Routes.Any(x => TablicaPrzebiegow.Z(x).Klucz == TablicaPrzebiegow.Z(r).Klucz)) ?? wyn[0];
        if (w.PrzebiegiPanel != null && semEl != null)
        {
            var dodaj = All<Button>(w.PrzebiegiPanel).Where(b => b.Content as string == "Dodaj").ToList();
            Ok(dodaj.Count == wyn.Count(r => !w.Doc.P.Przebiegi.Any(p => TablicaPrzebiegow.Pasuje(p, r))), $"panel: „Własny przebieg od {Stacja.Lbl(para.Start)}” – przycisków „Dodaj” {dodaj.Count}");
        }
        w.Doc.Change("test: bez wariantów pary", q => q.Przebiegi.RemoveAll(p => p.Od == para.Start && p.Do == para.End));
        int przed = w.Doc.P.Przebiegi.Count;
        w.PrzyjmijPrzebiegi(new[] { spoza });
        Ok(w.Doc.P.Przebiegi.Count == przed + 1, $"Dodaj: {spoza.Id} [{spoza.SwText}] w tablicy{(m.Routes.Any(x => TablicaPrzebiegow.Pasuje(TablicaPrzebiegow.Z(spoza), x)) ? "" : " (spoza propozycji)")}");
        var pw = w.Przebiegi();
        var spr = pw.Sprawdz();
        Ok(!spr.Any(u => u.Blad), $"Sprawdź: bez błędów – przebieg własny jest w układzie ({pw.UwagiBox.Text.Split('\n')[0]})");
        Ok(pw.Wiersze.Any(x => x.Klucz == TablicaPrzebiegow.Z(spoza).Klucz && !x.Brak), "okno Przebiegi: własny przebieg w tablicy, nie „brak w układzie”");
        pw.Close();
        var sim = Path.Combine(dir, "lcs_wlasny.json");
        w.DoSymulatora(sim);
        var s = new Simulator(Stacja.Load(sim));
        var rd = s.RouteTable.GetValueOrDefault(para.Start)?.FirstOrDefault(r => r.Type == "P" && r.End == para.End && r.SwKey == spoza.SwKey);
        Ok(rd != null && s.Kz.BrakujacePrzebiegi.Count == 0, $"symulator: przebieg własny w tablicy – {rd?.Id} [{rd?.SwText}]");
        var dalej = w.WlasnyPrzebieg(para.Start, "P", para.End, new Dictionary<string, string> { [zw.Num] = zw.Pos == "+" ? "-" : "+" }, "XX:brak");
        Ok(dalej.Count == 0, "warunek „przez” odcinek spoza drogi – nie ma takiej drogi");
    }
}

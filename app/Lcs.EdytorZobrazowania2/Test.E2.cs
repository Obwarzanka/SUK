using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Lcs.Model;
using K = System.Windows.Input.Key;

namespace Lcs.EdytorZobrazowania2;

/// <summary>
/// Test etapu E2 (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 10): mijanka narysowana klikami bez UT – tor 1, tor 2 z łącznikami (rozjazdy powstają same),
/// tor 3 z łącznikiem „[”, skrzyżowanie / rozjazd krzyżowy, zaznaczanie prostokątem, przesunięcie głowicy jednym przeciągnięciem, rząd z łącznikami,
/// rozsuń rzędy, usuwanie, strzałki, kopiuj / wklej, styl linii, podział toru, zapis i odczyt.
/// </summary>
public static partial class Test
{
    static Point S(MainWindow w, double x, double y) => w.View.ToScreen(x, y);
    static ObrWezel W(MainWindow w, int x, int y) => w.Obraz.Wezly.FirstOrDefault(n => n.P[0] == x && n.P[1] == y);
    static bool Jest(MainWindow w, int x0, int y0, int x1, int y1) =>
        W(w, x0, y0) is ObrWezel a && W(w, x1, y1) is ObrWezel b && w.Obraz.Odcinki.Any(e => (e.A == a.Id && e.B == b.Id) || (e.A == b.Id && e.B == a.Id));
    static int Rozjazdy(MainWindow w) => w.Obraz.Wezly.Count(n => ObrGraf.RodzajWezla(w.Obraz, n.Id) == ObrGraf.Rodzaj.Rozjazd);
    static string Graf(MainWindow w) => $"{w.Obraz.Wezly.Count} węzłów, {w.Obraz.Odcinki.Count} odcinków";
    static void Ctrl(MainWindow w, K k) => w.RysunekKey(k, true, false);
    static void MenuClick(MainWindow w, string header)
    {
        var mi = w.LastMenu?.Items.OfType<MenuItem>().FirstOrDefault(m => (m.Header as string)?.StartsWith(header) == true);
        if (mi == null) { Ok(false, $"menu: brak pozycji „{header}”"); return; }
        mi.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
    }

    static async Task RunE2(MainWindow w, string shot)
    {
        var dir = Path.Combine(Path.GetTempPath(), "edytor_zob2_test_e2"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        var ut = Path.Combine(dir, "mijanka.ut.json");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Testy", "mijanka.ut.json"), ut);
        Ok(w.NewFromUt(ut) && w.Obraz.Wezly.Count == 0, "nowe zobrazowanie – pusty rysunek (bez UT w rysowaniu)");
        await Settle(w); w.View.Fit();
        Type(w.RzedyText, "448 544 640");

        // --- tor 1: dwa kliki, Enter (kliki obok siatki – przyciąganie do siatki i prowadnicy)
        Key(w, K.T);
        Ok(w.View.Tool == ObrTool.Tor && w.ToolBtns[ObrTool.Tor].IsChecked == true, "klawisz T – pióro toru");
        w.View.SimClick(S(w, 98, 453));
        w.View.SimClick(S(w, 1822, 444));
        Key(w, K.Enter);
        Ok(w.Obraz.Odcinki.Count == 1 && Jest(w, 96, 448, 1824, 448), $"tor 1: (96, 448) – (1824, 448) poziomo, na prowadnicy ({Graf(w)})");

        // --- tor 2: start na torze 1, ukos, poziomo, ukos, klik na torze 1 kończy – dwa rozjazdy same
        w.View.SimClick(S(w, 466, 449));
        w.View.SimClick(S(w, 557, 538));
        w.View.SimClick(S(w, 1362, 547));
        w.View.SimClick(S(w, 1453, 452));
        Ok(w.View.Draft.Count == 0 && Jest(w, 464, 448, 560, 544) && Jest(w, 560, 544, 1360, 544) && Jest(w, 1360, 544, 1456, 448), $"tor 2: ukos 45° – poziomo na prowadnicy 544 – ukos 45° ({Graf(w)})");
        Ok(Rozjazdy(w) == 2 && ObrGraf.RodzajWezla(w.Obraz, W(w, 464, 448).Id) == ObrGraf.Rodzaj.Rozjazd && ObrGraf.RodzajWezla(w.Obraz, W(w, 1456, 448).Id) == ObrGraf.Rodzaj.Rozjazd,
            "rozjazdy w (464, 448) i (1456, 448) powstały same (tor 1 podzielony)");
        var odg = ObrGraf.Odgalezienie(w.Obraz, W(w, 464, 448).Id);
        Ok(odg != null && ObrGraf.Ukosny(w.Obraz, odg), "odgałęzienie rozjazdu – ukos (tor zasadniczy na wprost)");
        Ok(w.LastMessage.Contains("rozjazdy: 2"), $"komunikat ({w.LastMessage})");

        // --- tor 3 z łącznikiem „[” (ukos – pion – ukos) od toru 2
        w.View.SimClick(S(w, 721, 546));
        w.View.SimClick(S(w, 746, 570));
        w.View.SimClick(S(w, 745, 618));
        w.View.SimClick(S(w, 770, 642));
        w.View.SimClick(S(w, 1199, 638));
        Key(w, K.Enter);
        Ok(Jest(w, 720, 544, 744, 568) && Jest(w, 744, 568, 744, 616) && Jest(w, 744, 616, 768, 640) && Jest(w, 768, 640, 1200, 640), $"tor 3: łącznik „[” i tor na prowadnicy 640 ({Graf(w)})");
        Ok(Rozjazdy(w) == 3 && ObrGraf.Uwagi(w.Obraz).Count == 0, "trzeci rozjazd; uwag brak (wszystko co 45°)");

        // --- Backspace i Esc w piórze
        w.View.SimClick(S(w, 200, 800)); w.View.SimClick(S(w, 400, 800)); Key(w, K.Back);
        Ok(w.View.Draft.Count == 1, "Backspace – cofnięty punkt pióra");
        Key(w, K.Escape);
        Ok(w.View.Draft.Count == 0 && w.View.Tool == ObrTool.Tor, "Esc – rysowanie przerwane, pióro zostaje");
        int nOdc = w.Obraz.Odcinki.Count, nW = w.Obraz.Wezly.Count;

        // --- skrzyżowanie: ukos przez tor 1 bez węzła
        w.View.SimClick(S(w, 1000, 400));
        w.View.SimClick(S(w, 1097, 497));
        Key(w, K.Enter);
        var kr = W(w, 1048, 448);
        Ok(kr != null && ObrGraf.RodzajWezla(w.Obraz, kr.Id) == ObrGraf.Rodzaj.Skrzyzowanie && w.LastMessage.Contains("skrzyżowania: 1"), $"skrzyżowanie w (1048, 448) – węzeł z czterema torami ({w.LastMessage})");
        Key(w, K.Escape);
        Ok(w.View.Tool == ObrTool.Wskaz, "Esc – Wskaż");
        w.View.SimClick(S(w, 1048, 448), MouseButton.Right);
        MenuClick(w, "Rozjazd krzyżowy");
        Ok(ObrGraf.RodzajWezla(w.Obraz, W(w, 1048, 448).Id) == ObrGraf.Rodzaj.RozjazdKrzyzowy, "PPM na węźle → Rozjazd krzyżowy");
        w.DoUndo(); w.DoUndo();
        Ok(w.Obraz.Odcinki.Count == nOdc && w.Obraz.Wezly.Count == nW && W(w, 1048, 448) == null, "Ctrl+Z ×2 – bez skrzyżowania, tor 1 znów cały");

        // --- zaznaczenie prostokątem i przesunięcie głowicy jednym przeciągnięciem
        w.View.SimDrag(S(w, 400, 416), S(w, 600, 580));
        Ok(w.View.SelW.SetEquals(new[] { W(w, 464, 448).Id, W(w, 560, 544).Id }) && w.View.SelE.Count == 1, $"prostokąt: głowica – rozjazd, załamanie i łącznik ({w.View.SelW.Count} węzły)");
        w.View.SimDrag(S(w, 512, 496), S(w, 593, 497));
        Ok(Jest(w, 544, 448, 640, 544) && Jest(w, 96, 448, 544, 448) && Jest(w, 640, 544, 720, 544) && ObrGraf.Uwagi(w.Obraz).Count == 0 && w.Doc.UndoName.StartsWith("Przesunięcie"),
            "przeciągnięcie zaznaczenia: głowica o 80 j. w prawo, tory się rozciągnęły, łącznik dalej 45°");
        w.DoUndo();
        Ok(Jest(w, 464, 448, 560, 544), "Ctrl+Z – głowica wróciła");

        // --- przeciągnięcie toru poziomego: cały rząd w pionie z łącznikiem „[”
        w.View.ClearSelection();
        w.View.SimDrag(S(w, 1000, 640), S(w, 1000, 673));
        Ok(Jest(w, 768, 672, 1200, 672) && Jest(w, 744, 648, 768, 672) && Jest(w, 744, 568, 744, 648) && Jest(w, 720, 544, 744, 568) && ObrGraf.Uwagi(w.Obraz).Count == 0,
            "tor 3 przeciągnięty o 32 j. w dół: załamanie łącznika jedzie z rzędem, pion się wydłużył, ukosy 45°");

        // --- rozsuń rzędy (Ctrl+A, Ctrl+G): 448 / 544 / 672 → 448 / 560 / 672
        Ctrl(w, K.A);
        Ok(w.View.SelW.Count == w.Obraz.Wezly.Count, "Ctrl+A – wszystko zaznaczone");
        Ctrl(w, K.G);
        Ok(Jest(w, 464, 448, 576, 560) && Jest(w, 576, 560, 720, 560) && Jest(w, 1344, 560, 1456, 448) && Jest(w, 720, 560, 744, 584) && Jest(w, 744, 584, 744, 648) && ObrGraf.Uwagi(w.Obraz).Count == 0,
            "Ctrl+G: tor 2 w rzędzie 560 (równe odstępy), końce ślizgają się po ukosach łączników, „[” się skrócił, wszystko 45°");
        Ok(string.Join(" ", w.Obraz.Rzedy) == "448 560 640", $"prowadnica 544 przesunięta z rzędem ({string.Join(" ", w.Obraz.Rzedy)})");
        w.DoUndo();
        Ok(Jest(w, 560, 544, 720, 544) && Jest(w, 720, 544, 1360, 544) && string.Join(" ", w.Obraz.Rzedy) == "448 544 640", "Ctrl+Z – rzędy jak przed");

        // --- usunięcie toru 3: rozjazd na torze 2 znika, tor 2 jednym odcinkiem
        w.View.ClearSelection(); w.View.SimDrag(S(w, 1230, 700), S(w, 735, 560));
        Ok(w.View.SelW.Count == 4, $"prostokąt: tor 3 z łącznikiem ({w.View.SelW.Count} węzły)");
        Key(w, K.Delete);
        Ok(Jest(w, 560, 544, 1360, 544) && W(w, 720, 544) == null && Rozjazdy(w) == 2 && w.LastMessage.StartsWith("Usunięto"), "Del – tor 3 usunięty, rozjazd 720 zniknął, tor 2 jednym odcinkiem");
        w.DoUndo();
        Ok(Rozjazdy(w) == 3 && Jest(w, 768, 672, 1200, 672), "Ctrl+Z – tor 3 wrócił");

        // --- Shift + klik, strzałki
        w.View.SimClick(S(w, 1824, 448));
        w.View.SimClick(S(w, 96, 448), MouseButton.Left, ModifierKeys.Shift);
        Ok(w.View.SelW.Count == 2 && w.View.SelE.Count == 0, "klik i Shift + klik – dwa końce toru 1");
        w.View.SimClick(S(w, 96, 448), MouseButton.Left, ModifierKeys.Shift);
        Ok(w.View.SelW.Count == 1, "Shift + klik drugi raz – zdjęty z zaznaczenia");
        Key(w, K.Right);
        Ok(W(w, 1832, 448) != null && Jest(w, 1456, 448, 1832, 448), "strzałka w prawo – koniec toru o 8 j.");
        w.DoUndo();

        // --- kopiuj / wklej w miejscu kursora, powiel
        w.View.ClearSelection(); w.View.SimDrag(S(w, 1230, 700), S(w, 735, 560));
        Ctrl(w, K.C);
        w.View.Move(S(w, 800, 800));
        int przed = w.Obraz.Wezly.Count, przedE = w.Obraz.Odcinki.Count;
        Ctrl(w, K.V);
        Ok(w.Obraz.Wezly.Count == przed + 4 && w.Obraz.Odcinki.Count == przedE + 3 && W(w, 800, 800) != null && Jest(w, 824, 904, 1256, 904) && w.View.SelW.Count == 4,
            $"Ctrl+C, Ctrl+V – kopia łącznika z torem w miejscu kursora, zaznaczona ({Graf(w)})");
        Ctrl(w, K.D);
        Ok(w.Obraz.Wezly.Count == przed + 8 && W(w, 840, 840) != null, "Ctrl+D – powielenie 40 j. obok");
        w.DoUndo(); w.DoUndo();
        Ok(w.Obraz.Wezly.Count == przed, "Ctrl+Z ×2 – bez kopii");

        // --- PPM na torze: styl linii, podział toru, węzeł do przeciągnięcia
        w.View.SimClick(S(w, 1700, 448), MouseButton.Right);
        MenuClick(w, "Tor bez kontroli");
        var e1 = w.Obraz.Odcinki.First(e => ObrGraf.Konce(w.Obraz, e).B[0] == 1824 || ObrGraf.Konce(w.Obraz, e).A[0] == 1824);
        Ok(e1.Styl == ObrOdcinek.BezKontroli, "PPM → tor bez kontroli niezajętości (przerywany)");
        w.DoUndo();
        w.View.SimClick(S(w, 1600, 448), MouseButton.Right);
        MenuClick(w, "Podziel tor");
        Ok(W(w, 1600, 448) != null, "PPM → Podziel tor tutaj – węzeł w (1600, 448)");
        w.View.SimDrag(S(w, 1600, 448), S(w, 1600, 400));
        Ok(Jest(w, 1456, 448, 1600, 400) && ObrGraf.Uwagi(w.Obraz).Count == 2 && ObrGraf.Uwagi(w.Obraz).All(u => w.Obraz.Odcinki.Any(e => e.Id == u.El)),
            "przeciągnięcie węzła – tory się rozciągają; dwa odcinki nie co 45° – uwagi (rysunek nie jest blokowany)");
        w.Side.SelectedItem = w.TabWlasciwosci; await Settle(w);
        Ok(All<Button>(w.PropPanel).Any(b => (b.Content as string)?.Contains("nie co 45°") == true), "zakładka Właściwości: uwaga z przyciskiem");
        w.DoUndo(); w.DoUndo();
        Ok(W(w, 1600, 448) == null || ObrGraf.Uwagi(w.Obraz).Count == 0, "Ctrl+Z – tor prosto");
        // łączenie – odwrotność podziału (uwaga właściciela 2026-10-22)
        if (W(w, 1600, 448) == null) { w.View.SimClick(S(w, 1600, 448), MouseButton.Right); MenuClick(w, "Podziel tor"); }
        int odc = w.Obraz.Odcinki.Count;
        w.View.SimClick(S(w, 1600, 448), MouseButton.Right);
        MenuClick(w, "Połącz tor tutaj");
        Ok(W(w, 1600, 448) == null && w.Obraz.Odcinki.Count == odc - 1 && ObrGraf.Uwagi(w.Obraz).Count == 0, $"PPM na węźle → Połącz tor tutaj – węzeł zniknął, jeden odcinek ({w.LastMessage})");
        // koniec toru 1 (x 1824) odcięty i połączony z powrotem z końcem obok
        var k1824 = W(w, 1824, 448);
        w.Doc.Change("test: przerwa w torze", p =>
        {
            var o = p.Obrazy.First(x => x.Id == w.Obraz.Id);
            ObrOdcinek Na(int x) => o.Odcinki.First(e => { var (a, b) = ObrGraf.Konce(o, e); return a[1] == 448 && b[1] == 448 && Math.Min(a[0], b[0]) < x && Math.Max(a[0], b[0]) > x; });
            var n1 = ObrGraf.Podziel(o, Na(1700), new[] { 1700, 448 }); var n2 = ObrGraf.Podziel(o, Na(1720), new[] { 1720, 448 });
            o.Odcinki.RemoveAll(e => (e.A == n1.Id && e.B == n2.Id) || (e.A == n2.Id && e.B == n1.Id));
        });
        Ok(W(w, 1700, 448) != null && W(w, 1720, 448) != null && ObrGraf.Przy(w.Obraz, W(w, 1700, 448).Id).Count == 1, "przerwa w torze 1 między 1700 a 1720 – dwa końce");
        w.View.SimClick(S(w, 1700, 448), MouseButton.Right);
        MenuClick(w, "Połącz z końcem toru");
        Ok(W(w, 1700, 448) == null && W(w, 1720, 448) == null && ObrGraf.Przy(w.Obraz, W(w, 1824, 448).Id).Count == 1 && ObrGraf.Uwagi(w.Obraz).Count == 0,
            $"PPM na końcu → Połącz z końcem toru – tor 1 znów ciągły ({w.LastMessage})");
        w.DoUndo(); w.DoUndo(); w.DoUndo(); w.DoUndo();

        // --- zapis i odczyt grafu
        Ok(w.SaveDoc(false), "Zapisz");
        var l = ObrDocument.Load(w.Doc.Path);
        Ok(ObrDocument.Serialize(l.P) == ObrDocument.Serialize(w.Doc.P) && l.P.Obrazy[0].Odcinki.Count == w.Obraz.Odcinki.Count, $"odczyt: graf identyczny ({Graf(w)})");

        if (shot != null)
        {
            w.View.ClearSelection();
            w.View.SimClick(S(w, 900, 544));
            w.Side.SelectedItem = w.TabWlasciwosci;
            w.SetTool(ObrTool.Wskaz); w.View.Fit(); await Settle(w); Save(w, shot);
            // głowica 1:1 – przerwa przy odgałęzieniu rozjazdu, łącznik „[”
            w.View.ClearSelection(); w.View.Actual(); w.View.CenterOn(640, 560); await Settle(w);
            Save(w, Path.ChangeExtension(shot, null) + "_glowica.png");
        }
    }
}

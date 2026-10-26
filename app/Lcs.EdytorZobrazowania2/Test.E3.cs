using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Lcs.Model;
using K = System.Windows.Input.Key;

namespace Lcs.EdytorZobrazowania2;

/// <summary>
/// Test etapu E3 (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 10): mijanka klikami – tory, potem z palety semafory wjazdowe i wyjazdowe, tarcza, przerwy,
/// numery zwrotnic z „+”, numery torów, peron, przejazd, kozioł, wykolejnica; opisy wpisywane na płótnie, obrót spacją, przeciąganie opisu
/// i symbolu, symbole jadą z torem, menu PPM, uwagi ⚠, kopiuj / wklej z symbolami, zapis i odczyt. Zrzut do porównania z pulpitem GrM.
/// </summary>
public static partial class Test
{
    static void Wpisz(MainWindow w, string text)
    {
        Ok(w.OpisEdit.Visibility == Visibility.Visible, $"pole opisu na płótnie otwarte (do wpisania „{text}”)");
        w.OpisEdit.Text = text;
        var src = PresentationSource.FromVisual(w);
        w.OpisEdit.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, src, 0, K.Enter) { RoutedEvent = UIElement.KeyDownEvent });
    }
    static ObrElement El(MainWindow w, string typ, string tekst) => w.Obraz.Elementy.FirstOrDefault(e => e.Typ == typ && e.Opisy.FirstOrDefault()?.Tekst == tekst);
    static (double X, double Y, double Ux, double Uy) Poz(MainWindow w, ObrElement el) => ObrSymbole.Pozycja(w.Obraz, el).Value;
    static void Wstaw(MainWindow w, double x, double y, string opis)
    {
        w.View.SimClick(S(w, x, y));
        if (opis != null) Wpisz(w, opis);
    }

    static async Task RunE3(MainWindow w, string shot)
    {
        var dir = Path.Combine(Path.GetTempPath(), "edytor_zob2_test_e3"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        var ut = Path.Combine(dir, "mijanka.ut.json");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Testy", "mijanka.ut.json"), ut);
        Ok(w.NewFromUt(ut), "nowe zobrazowanie");
        await Settle(w); w.View.Fit();
        Type(w.RzedyText, "448 544 640");

        // --- tory jak w E2: tor 1, tor 2 z łącznikami, bocznica z łącznikiem „[”
        Key(w, K.T);
        w.View.SimClick(S(w, 96, 448)); w.View.SimClick(S(w, 1824, 448)); Key(w, K.Enter);
        w.View.SimClick(S(w, 464, 448)); w.View.SimClick(S(w, 560, 544)); w.View.SimClick(S(w, 1360, 544)); w.View.SimClick(S(w, 1456, 448));
        w.View.SimClick(S(w, 720, 544)); w.View.SimClick(S(w, 744, 568)); w.View.SimClick(S(w, 744, 616)); w.View.SimClick(S(w, 768, 640)); w.View.SimClick(S(w, 1040, 640)); Key(w, K.Enter);
        Ok(Rozjazdy(w) == 3 && ObrGraf.Uwagi(w.Obraz).Count == 0, $"tory mijanki z bocznicą ({Graf(w)})");
        Ok(ObrSymbole.Uwagi(w.Obraz).Count(u => u.Tekst.StartsWith("rozjazd bez numeru")) == 3, "uwagi: trzy rozjazdy bez numeru");

        // --- semafory wjazdowe (klawisz 1): ostrze w stronę środka obrazu, nazwa wpisana na płótnie
        Key(w, K.D1);
        Ok(w.View.Tool == ObrTool.Symbol && w.View.SymbolTyp == ObrSymbole.Semafor && w.SymBtns[ObrSymbole.Semafor].IsChecked == true, "klawisz 1 – semafor z palety");
        Wstaw(w, 202, 451, "A");
        var a = El(w, ObrSymbole.Semafor, "A");
        Ok(a != null && Poz(w, a) is var pa && pa.X == 200 && pa.Y == 448 && pa.Ux == 1, "semafor A na torze 1 w (200, 448), ostrze w prawo (do stacji)");
        Ok(a.Opisy[0].D.SequenceEqual(new[] { 0, 17 }), $"nazwa A pod torem – z prawej patrząc w kierunku ostrza ({a.Opisy[0].D[0]}, {a.Opisy[0].D[1]})");
        Wstaw(w, 1720, 446, "B");
        var b = El(w, ObrSymbole.Semafor, "B");
        Ok(b != null && Poz(w, b).Ux == -1 && b.Opisy[0].D.SequenceEqual(new[] { 0, -16 }), "semafor B ostrzem w lewo, nazwa nad torem");
        // spacja zaraz po wstawieniu – obraca wstawiony symbol (uwaga właściciela: „obracanie nie działa”)
        Wstaw(w, 1100, 448, "X1");
        var x1 = El(w, ObrSymbole.Semafor, "X1"); int k0 = Poz(w, x1).Ux > 0 ? 1 : -1;
        Key(w, K.Space);
        Ok(Poz(w, El(w, ObrSymbole.Semafor, "X1")).Ux == -k0 && w.View.Tool == ObrTool.Symbol, "spacja zaraz po wstawieniu – wstawiony semafor obrócony, narzędzie zostaje");
        Key(w, K.Space);
        Ok(Poz(w, El(w, ObrSymbole.Semafor, "X1")).Ux == k0, "druga spacja – z powrotem");
        w.DoUndo(); w.DoUndo(); w.DoUndo(); w.DoUndo();
        Ok(El(w, ObrSymbole.Semafor, "X1") == null, "Ctrl+Z – bez X1");
        // wyjazdowe: od strony szlaku – spacja odwraca kierunek następnych symboli
        Key(w, K.D1); Key(w, K.Space);
        Ok(w.View.SymbolOdwroc, "spacja w narzędziu – następne symbole ostrzem w drugą stronę");
        Wstaw(w, 1392, 448, "C1"); Wstaw(w, 1328, 544, "C2");
        Wstaw(w, 528, 448, "D1"); Wstaw(w, 592, 544, "D2");
        Ok(Poz(w, El(w, ObrSymbole.Semafor, "C1")).Ux == 1 && Poz(w, El(w, ObrSymbole.Semafor, "C2")).Ux == 1 && Poz(w, El(w, ObrSymbole.Semafor, "D1")).Ux == -1 && Poz(w, El(w, ObrSymbole.Semafor, "D2")).Ux == -1,
            "wyjazdowe C1, C2 w prawo, D1, D2 w lewo");
        Key(w, K.Escape);
        // A i B – odmiana „wjazdowy” z menu PPM
        foreach (var n in new[] { "A", "B" })
        {
            var el = El(w, ObrSymbole.Semafor, n);
            var p = Poz(w, el);
            w.View.SimClick(S(w, p.X, p.Y), MouseButton.Right);
            MenuClick(w, "semafor wjazdowy");
            Ok(El(w, ObrSymbole.Semafor, n).Styl == ObrSymbole.SemWjazdowy, $"PPM na semaforze {n} → semafor wjazdowy");
        }

        // --- tarcza manewrowa, wykolejnica, kozioł na bocznicy
        Key(w, K.D2); Key(w, K.Space);
        Wstaw(w, 880, 640, "Tm1");
        Ok(El(w, ObrSymbole.Tarcza, "Tm1") is ObrElement tm && Poz(w, tm).Ux == -1, "tarcza Tm1 na bocznicy ostrzem w lewo");
        Key(w, K.D6); Wstaw(w, 960, 640, "Wk1");
        Key(w, K.D7); w.View.SimClick(S(w, 1036, 642));
        var koz = w.Obraz.Elementy.FirstOrDefault(e => e.Typ == ObrSymbole.Koziol);
        Ok(koz != null && Poz(w, koz) is var pk && pk.X == 1040 && pk.Ux == 1, "kozioł na końcu bocznicy, otwarty w stronę toru");
        w.View.SimClick(S(w, 1036, 642));
        Ok(w.Obraz.Elementy.Count(e => e.Typ == ObrSymbole.Koziol) == 1 && w.LastMessage.Contains("już stoi"), "drugi kozioł w tym samym miejscu – nie (komunikat)");

        // --- przerwy izolacyjne (5), przejazd (8), peron (9), numery torów (N)
        Key(w, K.D5); w.View.SimClick(S(w, 320, 448)); w.View.SimClick(S(w, 1600, 448)); w.View.SimClick(S(w, 1200, 544));
        Ok(w.Obraz.Elementy.Count(e => e.Typ == ObrSymbole.Przerwa) == 3 && w.OpisEdit.Visibility != Visibility.Visible, "trzy przerwy izolacyjne (bez opisu)");
        Key(w, K.D8); Wstaw(w, 1664, 448, "km 12,345");
        Key(w, K.D9); Wstaw(w, 960, 496, "Peron 1");
        var peron = El(w, ObrSymbole.Peron, "Peron 1");
        Ok(peron != null && peron.P.SequenceEqual(new[] { 880, 484 }) && peron.Rozm.SequenceEqual(new[] { 160, 24 }), "peron 160 × 24 j. między torami 1 i 2");
        // przeciąganie (uwaga właściciela 2026-10-26): w pionie zostaje na środku między torami, krawędź przyciąga się do węzła toru 2 (560)
        Key(w, K.V);
        w.View.SimDrag(S(w, 960, 496), S(w, 997, 517));
        Ok(El(w, ObrSymbole.Peron, "Peron 1").P.SequenceEqual(new[] { 920, 484 }), $"peron przeciągnięty obok – dalej w środku między torami ({string.Join(", ", El(w, ObrSymbole.Peron, "Peron 1").P)})");
        w.View.SimDrag(S(w, 1000, 496), S(w, 646, 506));
        Ok(El(w, ObrSymbole.Peron, "Peron 1").P.SequenceEqual(new[] { 560, 484 }), $"lewa krawędź peronu przyciągnięta do węzła toru 2 ({string.Join(", ", El(w, ObrSymbole.Peron, "Peron 1").P)})");
        w.DoUndo(); w.DoUndo();
        Ok(El(w, ObrSymbole.Peron, "Peron 1").P.SequenceEqual(new[] { 880, 484 }), "Ctrl+Z – peron z powrotem");
        Key(w, K.N); Wstaw(w, 960, 448, "1"); Wstaw(w, 1040, 544, "2");
        Ok(El(w, ObrSymbole.NumerToru, "1") != null && El(w, ObrSymbole.NumerToru, "2") != null, "numery torów 1 i 2 w osi toru");

        // --- numery zwrotnic: Wskaż, klik w rozjazd, E, numer
        Key(w, K.V);
        foreach (var (x, y, n) in new[] { (464, 448, "1"), (1456, 448, "2"), (720, 544, "3") })
        {
            w.View.SimClick(S(w, x, y)); Key(w, K.E); Wpisz(w, n);
        }
        var z1 = El(w, ObrSymbole.Zwrotnica, "1");
        Ok(z1 != null && z1.Opisy[0].D.SequenceEqual(new[] { 8, -12 }), $"zwrotnica 1: numer nad torem (odgałęzienie w dół), od strony odgałęzienia ({z1?.Opisy[0].D[0]}, {z1?.Opisy[0].D[1]})");
        Ok(El(w, ObrSymbole.Zwrotnica, "2").Opisy[0].D.SequenceEqual(new[] { -8, -12 }), "zwrotnica 2: numer z drugiej strony (iglica w prawo)");
        Ok(ObrSymbole.Uwagi(w.Obraz).Count == 0 && ObrGraf.Uwagi(w.Obraz).Count == 0, $"uwag brak – rozjazdy z numerami, sygnalizatory z nazwami ({string.Join("; ", ObrSymbole.Uwagi(w.Obraz).Select(u => u.Tekst))})");
        // „+” na bok – PPM na rozjeździe
        w.View.SimClick(S(w, 720, 544), MouseButton.Right);
        MenuClick(w, "„+” na bok");
        Ok(El(w, ObrSymbole.Zwrotnica, "3").Styl == ObrSymbole.PlusBok, "PPM na rozjeździe 3 → „+” na bok (bocznica zasadniczo)");

        // --- spacja na zaznaczonym: obrót, opis przechodzi na drugą stronę
        var c1 = El(w, ObrSymbole.Semafor, "C1");
        w.View.SimClick(S(w, Poz(w, c1).X, Poz(w, c1).Y));
        Ok(w.View.SelEl.SetEquals(new[] { c1.Id }), "klik na symbolu – zaznaczony");
        w.Side.SelectedItem = w.TabWlasciwosci; await Settle(w);
        Ok(All<TextBox>(w.PropPanel).Any(t => t.Text == "C1") && All<ComboBox>(w.PropPanel).Any(), "zakładka Właściwości: nazwa i odmiana semafora");
        Key(w, K.Space);
        c1 = El(w, ObrSymbole.Semafor, "C1");
        Ok(Poz(w, c1).Ux == -1 && c1.Opisy[0].D.SequenceEqual(new[] { 0, -16 }), "spacja – C1 ostrzem w lewo, nazwa nad torem");
        w.DoUndo();
        Ok(Poz(w, El(w, ObrSymbole.Semafor, "C1")).Ux == 1, "Ctrl+Z – C1 jak przed");

        // --- przeciąganie opisu i symbolu
        var ap = Poz(w, El(w, ObrSymbole.Semafor, "A"));
        w.View.SimDrag(S(w, ap.X, ap.Y + 17), S(w, ap.X - 10, ap.Y + 23));
        Ok(El(w, ObrSymbole.Semafor, "A").Opisy[0].D.SequenceEqual(new[] { -10, 23 }) && w.Doc.UndoName == "Przesunięcie opisu", $"przeciągnięcie nazwy A ({string.Join(",", El(w, ObrSymbole.Semafor, "A").Opisy[0].D)})");
        w.DoUndo();
        var cp = Poz(w, El(w, ObrSymbole.Semafor, "C1"));
        w.View.SimDrag(S(w, cp.X, cp.Y), S(w, cp.X - 31, cp.Y + 5));
        Ok(Poz(w, El(w, ObrSymbole.Semafor, "C1")) is var c2 && c2.X == 1360 && c2.Y == 448, $"przeciągnięcie semafora C1 wzdłuż toru: (1392 → {Poz(w, El(w, ObrSymbole.Semafor, "C1")).X}, 448)");
        w.DoUndo();

        // --- symbole jadą z torem: rząd toru 2 o 16 j. w dół
        w.View.ClearSelection();
        w.View.SimDrag(S(w, 1100, 544), S(w, 1100, 560));
        Ok(Poz(w, El(w, ObrSymbole.Semafor, "C2")).Y == 560 && Poz(w, El(w, ObrSymbole.NumerToru, "2")).Y == 560, "przesunięcie rzędu toru 2 – semafor C2 i numer toru jadą z torem");
        w.DoUndo();

        // --- usuwanie i PPM na torze: wstaw przerwę
        var d2 = El(w, ObrSymbole.Semafor, "D2"); var dp = Poz(w, d2);
        w.View.SimClick(S(w, dp.X, dp.Y)); Key(w, K.Delete);
        Ok(El(w, ObrSymbole.Semafor, "D2") == null, "Del – semafor D2 usunięty");
        w.DoUndo();
        Ok(El(w, ObrSymbole.Semafor, "D2") != null, "Ctrl+Z – D2 wrócił");
        int prz = w.Obraz.Elementy.Count(e => e.Typ == ObrSymbole.Przerwa);
        w.View.SimClick(S(w, 1100, 448), MouseButton.Right);
        MenuClick(w, "Wstaw przerwę");
        Ok(w.Obraz.Elementy.Count(e => e.Typ == ObrSymbole.Przerwa) == prz + 1, "PPM na torze → wstaw przerwę izolacyjną");
        w.DoUndo();

        // --- kopiuj / wklej z symbolami (bocznica z tarczą, wykolejnicą i kozłem)
        w.View.ClearSelection(); w.View.SimDrag(S(w, 1060, 700), S(w, 735, 560));
        Ctrl(w, K.C); w.View.Move(S(w, 1200, 800)); int nEl = w.Obraz.Elementy.Count;
        Ctrl(w, K.V);
        Ok(w.Obraz.Elementy.Count == nEl + 3, $"kopia bocznicy z tarczą, wykolejnicą i kozłem ({w.Obraz.Elementy.Count - nEl} symbole)");
        w.DoUndo();

        // --- poruszanie się po płótnie (uwaga właściciela: „nie mogę poruszać się swobodnie”): PPM przeciągnięcie, strzałki bez zaznaczenia
        w.SetTool(ObrTool.Wskaz); w.View.ClearSelection();
        var off0 = w.View.Off;
        w.View.SimRight(new Point(700, 300), new Point(760, 340));
        Ok(Math.Abs(w.View.Off.X - off0.X - 60) < 1e-6 && Math.Abs(w.View.Off.Y - off0.Y - 40) < 1e-6 && w.LastMenu?.IsOpen != true, "PPM przeciągnięcie – widok przesunięty o 60 / 40 px, bez menu");
        var bp0 = Poz(w, El(w, ObrSymbole.Semafor, "B"));
        w.View.SimRight(S(w, bp0.X, bp0.Y), S(w, bp0.X, bp0.Y));
        Ok(w.LastMenu?.Items.OfType<MenuItem>().Any(m => (m.Header as string)?.StartsWith("Obróć") == true) == true, "PPM klik bez ruchu – menu symbolu jak dotąd");
        w.View.ClearSelection(); off0 = w.View.Off;
        Key(w, K.Right);
        Ok(w.View.Off.X < off0.X && w.Obraz.Elementy.Count > 0, "strzałka bez zaznaczenia – widok przesunięty (rysunek bez zmian)");
        w.View.Fit();
        // --- semafor wjazdowy bez trójkąta końca przebiegu (decyzja właściciela: do wyboru przy semaforze)
        var bb2 = El(w, ObrSymbole.Semafor, "B");
        w.UstawStylEl(bb2.Id, ObrSymbole.SemWjazdowyBez);
        Ok(El(w, ObrSymbole.Semafor, "B").Styl == ObrSymbole.SemWjazdowyBez && ObrSymbole.Wjazdowy(El(w, ObrSymbole.Semafor, "B").Styl), "semafor B – odmiana „wjazdowy bez końca przebiegu”");
        // --- zapis i odczyt
        Ok(w.SaveDoc(false), "Zapisz");
        var l = ObrDocument.Load(w.Doc.Path);
        Ok(ObrDocument.Serialize(l.P) == ObrDocument.Serialize(w.Doc.P) && l.P.Obrazy[0].Elementy.Count == w.Obraz.Elementy.Count, $"odczyt: symbole i opisy identyczne ({w.Obraz.Elementy.Count} elementów)");

        if (shot != null)
        {
            w.View.ClearSelection(); w.SetTool(ObrTool.Wskaz);
            w.View.Fit(); await Settle(w); Save(w, shot);
            w.View.Actual(); w.View.CenterOn(760, 520); await Settle(w);
            Save(w, Path.ChangeExtension(shot, null) + "_glowica.png");
            w.View.CenterOn(1500, 500); await Settle(w);
            Save(w, Path.ChangeExtension(shot, null) + "_wschod.png");
            // jak na pulpicie – bez siatki, prowadnic, węzłów (do porównania ze wzorem GrM)
            w.View.Czysty = true; w.View.Actual(); w.View.CenterOn(960, 520); await Settle(w);
            Save(w, Path.ChangeExtension(shot, null) + "_pulpit.png");
            w.View.Czysty = false;
        }
    }
}

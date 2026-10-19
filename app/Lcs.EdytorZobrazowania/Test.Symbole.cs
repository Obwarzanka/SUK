using System.IO;
using System.Windows;
using System.Windows.Input;
using Lcs.Model;

namespace Lcs.EdytorZobrazowania;

/// <summary>
/// Test etapu Z3 (PLAN_EDYTOR_ZOBRAZOWANIA.md § 5) jak praca użytkownika, na obrazie mijanki z Z2: semafory A, B (wjazdowe), F (wyjazdowy),
/// S60 (odstępowy), zwrotnice 1–4 (przyciągnięte do torów odgałęźnych), kozły na końcach toru 2 – narzędziami S / Z / K; kierunek symboli
/// z UT i z kierunku km toru obrazu, opis domyślny i przeciągnięty, błędy (zły tor, kolejność), przesunięcie toru z symbolami,
/// „Sprawdź” – brak nieumieszczonych, zapis i odczyt.
/// </summary>
public static partial class Test
{
    static async Task RunZ3(MainWindow w, string shot)
    {
        var d = w.Doc; var ut = d.Ut;
        await Settle(w); w.View.Fit();
        Point S(int x, double y) => w.View.ToScreen(x, y);
        string Dev(string name) => ut.Devices.First(x => x.Name == name).Id;
        string Rz(string nr) => ut.Turnouts.First(x => x.Number == nr).Id;
        ZobSymbol Sym(ZobRodzaj r, string id) => ZobSymbole.Lista(w.Obraz, r).FirstOrDefault(x => x.Ut == id);
        string P(ZobSymbol s) => s == null ? "-" : $"({s.P[0]},{s.P[1]})";
        string A = Dev("A"), B = Dev("B"), F = Dev("F"), S60 = Dev("S60");
        var kozly = ut.Devices.Where(x => x.Kind == UtDevice.Koziol).OrderBy(x => x.S).Select(x => x.Id).ToList();
        // „Sprawdź” bez elementów etapu Z4 (numery torów, strzałki szlaków – sprawdza je test Z4)
        List<ZobUwaga> Spr() => w.Sprawdz().Where(x => !x.Text.Contains("numer toru") && !x.Text.Contains("szlak ")).ToList();

        // --- „Sprawdź” przed symbolami
        var u0 = Spr();
        Ok(u0.Count(x => x.Text.StartsWith("nie ma na obrazie")) == 10 && !u0.Any(x => x.Error), $"Sprawdź: nieumieszczone 4 zwrotnice, 4 sygnalizatory, 2 kozły ({u0.Count})");
        Ok(w.CheckPanel.Children.Count == 15, "panel Sprawdź: nagłówek i 14 uwag (10 z Z3, 2 numery torów, 2 szlaki)");

        // --- sygnalizatory
        w.ToolKey(Key.S);
        Ok(w.View.Tool == ZobTool.Sygnalizator && w.SymCurrent != null && w.LastMessage.StartsWith("Sygnalizator:"), $"S – narzędzie Sygnalizator, następny nieumieszczony ({w.LastMessage})");
        Leaf(w, "A – semafor").IsSelected = true;
        Ok(w.SymCurrent == A, "A wybrany w panelu UT");
        w.View.SimClick(S(312, 616));
        Ok(Sym(ZobRodzaj.Sygnalizator, A) == null && w.LastMessage.Contains("stoi na torze 1"), $"A kliknięty na torze 2 – odmowa ({w.LastMessage})");
        w.View.SimClick(S(312, 521));
        var sa = Sym(ZobRodzaj.Sygnalizator, A);
        Ok(sa != null && P(sa) == "(312,520)", $"A na torze 1: {P(sa)}");
        var ka = ZobSymbole.Kierunek(w.Obraz.Tory.First(t => t.Ut.Tor == ut.Devices.First(x => x.Id == A).Track), sa.P, 1);
        Ok(ka == (1, 0) && w.View.OpisAt(ZobRodzaj.Sygnalizator, sa) is var oa && oa[0] == 312 && oa[1] == 537, "A: ostrze w prawo (kierunek km toru 1), opis pod torem jak na pulpicie");
        Ok(w.SymCurrent != null && w.SymCurrent != A, "następny sygnalizator do postawienia");
        foreach (var (id, x, y) in new[] { (S60, 184, 520), (B, 1608, 520), (F, 1360, 616) })
        {
            Leaf(w, ut.Devices.First(q => q.Id == id).Name + " – ").IsSelected = true;
            w.View.SimClick(S(x, y));
        }
        var sb = Sym(ZobRodzaj.Sygnalizator, B); var sf = Sym(ZobRodzaj.Sygnalizator, F);
        Ok(sb != null && sf != null && Sym(ZobRodzaj.Sygnalizator, S60) != null && w.SymCurrent == null, $"S60, B, F na obrazie; wszystkie sygnalizatory postawione ({w.LastMessage})");
        Ok(w.View.OpisAt(ZobRodzaj.Sygnalizator, sb) is var ob && ob[0] == 1608 && ob[1] == 503, "B (działa w stronę malejącego km): ostrze w lewo, opis nad torem");

        // --- zwrotnice: przyciąganie do torów odgałęźnych
        w.ToolKey(Key.Z);
        Ok(w.View.Tool == ZobTool.Zwrotnica && w.SymCurrent == Rz("1"), $"Z – narzędzie Zwrotnica, pierwsza: 1 ({w.LastMessage})");
        w.View.SimClick(S(611, 521));
        var z1 = Sym(ZobRodzaj.Zwrotnica, Rz("1"));
        Ok(P(z1) == "(600,520)", $"zwrotnica 1 przyciągnięta do łącznika: {P(z1)}");
        Ok(w.View.OpisAt(ZobRodzaj.Zwrotnica, z1) is var oz && oz[1] == 508, "numer zwrotnicy 1 po stronie przeciwnej niż odgałęzienie – łącznik w dół, numer nad torem (jak w SimRail)");
        foreach (var (nr, x, y) in new[] { ("2", 624, 616), ("3", 1244, 520), ("4", 1226, 616) })
        {
            Leaf(w, "rozjazd " + nr).IsSelected = true;
            w.View.SimClick(S(x, y));
        }
        Ok(P(Sym(ZobRodzaj.Zwrotnica, Rz("2"))) == "(616,616)" && P(Sym(ZobRodzaj.Zwrotnica, Rz("3"))) == "(1248,520)" && P(Sym(ZobRodzaj.Zwrotnica, Rz("4"))) == "(1232,616)",
            "zwrotnice 2, 3, 4 przyciągnięte do końców łączników");

        // --- kozły
        w.ToolKey(Key.K);
        w.View.SimClick(S(176, 616));
        w.View.SimClick(S(1740, 616));
        Ok(P(Sym(ZobRodzaj.Kozioł, kozly[0])) == "(160,616)" && P(Sym(ZobRodzaj.Kozioł, kozly[1])) == "(1760,616)", "kozły przyciągnięte do końców toru 2 (wg km)");
        w.ToolKey(Key.W);
        Ok(w.LastMessage.Contains("nie ma takich obiektów"), "W – w mijance nie ma wykolejnic");

        // --- „Sprawdź” – komplet
        var u1 = Spr();
        Ok(u1.Count == 0, $"Sprawdź: brak nieumieszczonych, bez uwag ({string.Join("; ", u1.Select(x => x.Text))})");
        Ok(w.UtInfo.Text.StartsWith("Na obrazie 14 z 18"), $"panel UT: {w.UtInfo.Text.Split('.')[0]} (bez numerów torów i szlaków – Z4)");

        // --- błąd kolejności
        w.ToolKey(Key.S); Leaf(w, "B – ").IsSelected = true;
        w.View.SimClick(S(232, 520));
        var u2 = Spr();
        Ok(u2.Any(x => x.Error && x.Text.StartsWith("kolejność na torze 1")), $"B przestawiony przed A – błąd kolejności ({u2.FirstOrDefault()?.Text})");
        w.DoUndo();
        Ok(Spr().Count == 0, "Ctrl+Z – kolejność dobra");

        // --- Wskaż: zaznaczenie symbolu, opis, zdjęcie
        w.ToolKey(Key.V);
        w.View.SimClick(S(306, 520));
        Ok(w.View.SelectedSym == (ZobRodzaj.Sygnalizator, A) && Btn(w.ImgPanel, "Zdejmij z obrazu") != null, "klik na semaforze A – zaznaczony, panel symbolu");
        w.View.Actual(); w.View.CenterOn(312, 540);
        w.View.SimDrag(S(312, 537), S(331, 546));
        sa = Sym(ZobRodzaj.Sygnalizator, A);
        Ok(sa.Opis != null && sa.Opis[0] == 328 && sa.Opis[1] == 544 && d.UndoName == "Przesunięcie opisu", $"opis A przeciągnięty (do siatki): ({sa.Opis?[0]},{sa.Opis?[1]})");
        Click(Btn(w.ImgPanel, "Opis w miejscu domyślnym"));
        Ok(Sym(ZobRodzaj.Sygnalizator, A).Opis == null, "Opis w miejscu domyślnym");
        w.View.Fit();
        w.ToolKey(Key.Delete);
        Ok(Sym(ZobRodzaj.Sygnalizator, A) == null && Spr().Count == 1, "Del – A zdjęty z obrazu, Sprawdź: nie ma na obrazie A");
        w.DoUndo();
        Ok(Sym(ZobRodzaj.Sygnalizator, A) != null, "Ctrl+Z – A wrócił");

        // --- kierunek km toru obrazu odwrócony – symbol obraca się sam
        w.View.SimClick(S(900, 520));
        Click(Btn(w.ImgPanel, "Odwróć kierunek km"));
        var t1 = w.Obraz.Tory.First(t => t.Id == w.SelectedTor.Id);
        Ok(ZobSymbole.Kierunek(t1, Sym(ZobRodzaj.Sygnalizator, A).P, 1) == (-1, 0), "tor 1 z odwróconym km – A zwrócony w lewo (kierunek z UT)");
        Ok(w.Sprawdz().Any(x => x.Text.StartsWith("kolejność na torze 1")), "… i Sprawdź: kolejność wzdłuż km odwrotna");
        w.DoUndo();

        // --- przesunięcie toru z symbolami
        w.View.SimClick(S(1000, 616));
        w.ToolKey(Key.Down);
        Ok(P(Sym(ZobRodzaj.Sygnalizator, F)) == "(1360,624)" && P(Sym(ZobRodzaj.Kozioł, kozly[0])) == "(160,624)" && P(Sym(ZobRodzaj.Zwrotnica, Rz("2"))) == "(616,624)",
            "tor 2 strzałką w dół – F, zwrotnice i kozły jadą razem z nim");
        w.DoUndo();
        Ok(P(Sym(ZobRodzaj.Sygnalizator, F)) == "(1360,616)" && Spr().Count == 0, "Ctrl+Z – jak było, Sprawdź bez uwag");
        w.ToolKey(Key.Delete);
        Ok(w.Sprawdz().Count(x => x.Error && x.Text.Contains("nie leży na torze")) == 5, "tor 2 zdjęty – 5 symboli bez toru (błędy, czerwone)");
        w.DoUndo();

        // --- zapis i odczyt
        Ok(w.SaveDoc(false), "zapis z symbolami");
        var l = ZobDocument.Load(d.Path);
        var lo = l.P.Obrazy[0];
        Ok(lo.Sygnalizatory.Count == 4 && lo.Zwrotnice.Count == 4 && lo.Kozly.Count == 2 && ZobDocument.Serialize(l.P) == ZobDocument.Serialize(w.Doc.P), "odczyt: 4 sygnalizatory, 4 zwrotnice, 2 kozły, identycznie");

        w.View.Select(null); w.Sprawdz(); await Settle(w);
        Ok(w.View.SelectedSym == null && w.SelectedTor == null, "po odznaczeniu nic nie wraca do zaznaczenia (panel UT nie zaznacza sam)");
        if (shot != null)
        {
            string Shot(string suf) => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(shot))!, Path.GetFileNameWithoutExtension(shot) + suf);
            w.Sprawdz();
            w.View.Fit(); await Settle(w); Save(w, Shot("_symbole.png"));
            w.View.Actual(); w.View.ZoomAt(new Point(w.View.ActualWidth / 2, w.View.ActualHeight / 2), 2); w.View.CenterOn(440, 568); await Settle(w); Save(w, Shot("_symbole_2x.png"));
        }
    }
}

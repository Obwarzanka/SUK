using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Lcs.Model;
using K = System.Windows.Input.Key;

namespace Lcs.EdytorZobrazowania2;

/// <summary>
/// Test etapu E7 (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 10): głowice mijanki złożone z klocków (łącznik „[”, łącznik ukośny odbity spacją, koniec toru,
/// rozjazd krzyżowy), F8 i Sprawdź bez błędów; własny klocek z zaznaczenia wstawiony w innym obrazie (bez przypięć); szkic na życzenie z UT –
/// prostokąt, szara warstwa, przesunięcie, Odrzuć / Ctrl+Z, „Przyjmij i edytuj”, poprawka rysunku, F8 i Sprawdź bez błędów.
/// </summary>
public static partial class Test
{
    static async Task RunE7(MainWindow w, string shot)
    {
        var dir = Path.Combine(Path.GetTempPath(), "edytor_zob2_test_e7"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        var ut = Path.Combine(dir, "mijanka.ut.json");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Testy", "mijanka.ut.json"), ut);
        MainWindow.KlockiPath = Path.Combine(dir, "klocki.json");
        Ok(w.NewFromUt(ut), "nowe zobrazowanie do mijanki");
        w.WczytajKlocki();
        await Settle(w); w.View.Fit();
        Type(w.RzedyText, "448 544");

        // --- tory jako dwa proste rzędy, głowice z klocków
        Key(w, K.T);
        w.View.SimClick(S(w, 96, 448)); w.View.SimClick(S(w, 1824, 448)); Key(w, K.Enter);
        w.View.SimClick(S(w, 256, 544)); w.View.SimClick(S(w, 1664, 544)); Key(w, K.Enter);
        Key(w, K.Escape);
        Ok(w.KlocekBtns.ContainsKey(ObrKlocki.Lacznik) && w.KlocekBtns.ContainsKey(ObrKlocki.Krzyzowy), "paleta: klocki łącznik „[”, łącznik ukośny, rozjazd krzyżowy, koniec toru");
        Click(w.KlocekBtns[ObrKlocki.Lacznik]);
        Ok(w.View.Tool == ObrTool.Klocek && w.KlocekBtns[ObrKlocki.Lacznik].IsChecked == true, "klocek łącznik „[” wybrany");
        w.View.SimClick(S(w, 466, 450));
        Ok(Jest(w, 464, 448, 488, 472) && Jest(w, 488, 472, 488, 520) && Jest(w, 488, 520, 512, 544) && Rozjazdy(w) == 2,
            $"łącznik „[” od toru 1 (464) do toru 2 (do następnej prowadnicy – 96 j.), dwa rozjazdy powstały same ({Graf(w)})");
        Click(w.KlocekBtns[ObrKlocki.Ukos]); Key(w, K.Space);
        Ok(w.KlocekWariant == 1, "spacja – klocek odbity w poziomie");
        w.View.SimClick(S(w, 1455, 449));
        Ok(Jest(w, 1456, 448, 1360, 544) && Rozjazdy(w) == 4, "łącznik ukośny w lewo od toru 1 (1456) do toru 2 (1360) – cztery rozjazdy");
        Click(w.KlocekBtns[ObrKlocki.KoniecToru]); w.View.SimClick(S(w, 1664, 544));
        Ok(Jest(w, 1664, 544, 1712, 544) == false && Jest(w, 1360, 544, 1712, 544) || Jest(w, 1664, 544, 1712, 544), "koniec toru: tor 2 przedłużony do 1712");
        Ok(w.Obraz.Elementy.Any(e => e.Typ == ObrSymbole.Koziol && ObrGraf.Wezel(w.Obraz, e.Na)?.P.SequenceEqual(new[] { 1712, 544 }) == true), "kozioł na końcu klocka");
        Click(w.KlocekBtns[ObrKlocki.Krzyzowy]); w.View.SimClick(S(w, 960, 448));
        Ok(W(w, 960, 448) is ObrWezel kr && ObrGraf.RodzajWezla(w.Obraz, kr.Id) == ObrGraf.Rodzaj.RozjazdKrzyzowy, "klocek rozjazd krzyżowy na torze 1 – węzeł rozjazdu krzyżowego");
        w.DoUndo();
        Ok(W(w, 960, 448) == null && ObrGraf.Uwagi(w.Obraz).Count == 0, "Ctrl+Z – bez rozjazdu krzyżowego; rysunek co 45°");
        Key(w, K.Escape);
        foreach (var (x, y, n) in new[] { (464, 448, "1"), (512, 544, "2"), (1456, 448, "3"), (1360, 544, "4") }) { w.View.SimClick(S(w, x, y)); Key(w, K.E); Wpisz(w, n); }
        Key(w, K.D7); w.View.SimClick(S(w, 256, 544)); Key(w, K.Escape);
        Key(w, K.F8);
        var spr = w.Sprawdz();
        Ok(Wyj(w) == 0 && spr.Count(x => x.Blad) == 0, $"głowice z klocków: F8 – lista pusta, Sprawdź bez błędów ({string.Join("; ", spr.Where(x => x.Blad).Select(x => x.Tekst))})");
        if (shot != null) { w.View.ClearSelection(); w.View.Fit(); await Settle(w); Save(w, Path.ChangeExtension(shot, null) + "_klocki.png"); }

        // --- własny klocek z zaznaczenia, wstawiony w innym obrazie
        w.View.ClearSelection(); w.View.SimDrag(S(w, 440, 430), S(w, 530, 560));
        int zazn = w.View.SelW.Count;
        var kl = w.ZapiszKlocek("Głowica lewa");
        Ok(kl != null && w.Wlasne.Count == 1 && File.Exists(MainWindow.KlockiPath) && w.KlocekBtns.ContainsKey("w:0") && kl.Fragment.Przypiecia.Count == 0,
            $"„Zapisz zaznaczenie” – własny klocek „Głowica lewa” ({zazn} węzłów, bez przypięć), plik biblioteki");
        var pierwszy = w.Current;
        Click(Btn(w.Tabs, "+ obraz szczegółowy"));
        Click(w.KlocekBtns["w:0"]); w.View.SimClick(S(w, 200, 296));
        Ok(w.Obraz.Odcinki.Count == kl.Fragment.Odcinki.Count && w.Obraz.Przypiecia.Count == 0 && W(w, 200, 296) != null, $"własny klocek w drugim obrazie: {w.Obraz.Odcinki.Count} odcinków, bez przypięć");
        w.DoUndo(); w.DoUndo();
        w.ShowImage(pierwszy);

        // --- szkic na życzenie: nowe zobrazowanie, prostokąt, warstwa szkicu
        var dir2 = Path.Combine(dir, "szkic"); Directory.CreateDirectory(dir2);
        var ut2 = Path.Combine(dir2, "mijanka.ut.json"); File.Copy(ut, ut2);
        Ok(w.NewFromUt(ut2) && w.Obraz.Wezly.Count == 0, "nowe, puste zobrazowanie do szkicu");
        await Settle(w); w.View.Fit();
        w.Side.SelectedItem = w.TabUt; await Settle(w);
        var bs = All<Button>(w.UtPanel).FirstOrDefault(b => b.Content as string == "Zaproponuj szkic…");
        Ok(bs != null && bs.IsEnabled && All<CheckBox>(w.UtPanel).Count() == 4, "zakładka UT: 4 tory nienarysowane do wyboru, „Zaproponuj szkic…”");
        Click(bs);
        Ok(w.View.Tool == ObrTool.Szkic, "narzędzie szkicu – prostokąt");
        w.View.SimDrag(S(w, 96, 400), S(w, 1824, 640));
        var sz = w.Obraz.Szkic;
        var so = sz == null ? null : ObrSzkic.JakoObraz(sz, w.Obraz);
        Ok(sz != null && w.Obraz.Wezly.Count == 0 && so.Wezly.Count(n => ObrGraf.RodzajWezla(so, n.Id) == ObrGraf.Rodzaj.Rozjazd) == 4 && ObrGraf.Uwagi(so).Count == 0,
            $"szkic: osobna warstwa (obraz pusty), 4 rozjazdy, wszystko co 45° ({sz?.Odcinki.Count} odcinków)");
        Ok(sz.Elementy.Where(e => e.Typ == ObrSymbole.Zwrotnica).Select(e => e.Opisy[0].Tekst).OrderBy(x => x).SequenceEqual(new[] { "1", "2", "3", "4" })
            && sz.Elementy.Any(e => e.Typ == ObrSymbole.Semafor && e.Opisy[0].Tekst == "A") && sz.Elementy.Count(e => e.Typ == ObrSymbole.Koziol) == 2 && sz.Przypiecia.Count > 0,
            "szkic: numery zwrotnic, semafory, kozły z UT, od razu przypięty");
        Ok(w.SzkicBar.Visibility == Visibility.Visible, "pasek szkicu: Przyjmij / Przyjmij i edytuj / Odrzuć");
        if (shot != null) { w.View.Fit(); await Settle(w); Save(w, Path.ChangeExtension(shot, null) + "_szkic.png"); }
        // przesunięcie szkicu jako całości
        var e0 = sz.Odcinki.First(e => { var a = sz.Wezly.First(n => n.Id == e.A).P; var b = sz.Wezly.First(n => n.Id == e.B).P; return a[1] == b[1] && Math.Abs(a[0] - b[0]) > 80; });
        var pa = sz.Wezly.First(n => n.Id == e0.A).P; var pb = sz.Wezly.First(n => n.Id == e0.B).P;
        int ys = pa[1]; double xs = (pa[0] + pb[0]) / 2.0;
        w.View.SimDrag(S(w, xs, ys), S(w, xs, ys + 16));
        Ok(w.Obraz.Szkic.Wezly.Any(n => n.P[1] == ys + 16) && !w.Obraz.Szkic.Wezly.Any(n => n.P[1] == ys) && w.Obraz.Wezly.Count == 0, "przeciągnięcie szkicu – cały szkic o 16 j. niżej, obraz dalej pusty");
        // odrzuć i cofnij
        Click(All<Button>(w.SzkicBar).First(b => b.Content as string == "Odrzuć"));
        Ok(w.Obraz.Szkic == null && w.SzkicBar.Visibility != Visibility.Visible, "Odrzuć – bez szkicu");
        w.DoUndo();
        Ok(w.Obraz.Szkic != null, "Ctrl+Z – szkic wrócił");
        Click(All<Button>(w.SzkicBar).First(b => b.Content as string == "Przyjmij i edytuj"));
        Ok(w.Obraz.Szkic == null && Rozjazdy(w) == 4 && w.View.SelE.Count > 0 && w.Obraz.Przypiecia.Count > 0, $"Przyjmij i edytuj – szkic w obrazie z przypięciami, zaznaczony ({Graf(w)})");
        Key(w, K.F8);
        spr = w.Sprawdz();
        Ok(Wyj(w) == 0 && spr.Count(x => x.Blad) == 0, $"szkic przyjęty: F8 – lista pusta, Sprawdź bez błędów ({string.Join("; ", spr.Where(x => x.Blad).Select(x => x.Tekst))})");
        // poprawka: drugi rząd o 32 j. niżej (rząd z łącznikami)
        var ry = w.Obraz.Wezly.Select(n => n.P[1]).Distinct().OrderBy(y => y).ToList();
        var r2 = w.Obraz.Odcinki.Where(e => ObrGraf.Poziomy(w.Obraz, e)).Select(e => ObrGraf.Konce(w.Obraz, e)).Where(k => k.A[1] > ry[0]).OrderBy(k => k.A[1]).ThenByDescending(k => Math.Abs(k.B[0] - k.A[0])).First();
        w.View.ClearSelection();
        w.View.SimDrag(S(w, (r2.A[0] + r2.B[0]) / 2.0, r2.A[1]), S(w, (r2.A[0] + r2.B[0]) / 2.0, r2.A[1] + 32));
        Key(w, K.F8);
        spr = w.Sprawdz();
        Ok(w.Obraz.Wezly.Any(n => n.P[1] == r2.A[1] + 32) && ObrGraf.Uwagi(w.Obraz).Count == 0 && Wyj(w) == 0 && spr.Count(x => x.Blad) == 0,
            "poprawka po przyjęciu: rząd toru 2 o 32 j. niżej (łączniki dalej 45°), F8 i Sprawdź bez błędów");
        Ok(w.SaveDoc(false) && ObrDocument.Load(w.Doc.Path).P.Obrazy[0].Szkic == null, "zapis i odczyt – bez szkicu");
        if (shot != null) { w.View.ClearSelection(); w.View.Fit(); await Settle(w); Save(w, shot); }
    }
}

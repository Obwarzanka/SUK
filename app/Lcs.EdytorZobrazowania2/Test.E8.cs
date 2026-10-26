using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lcs.Model;
using K = System.Windows.Input.Key;

namespace Lcs.EdytorZobrazowania2;

/// <summary>
/// Etap E8 (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 10): wiele obrazów (kopia obrazu, kolejność zakładek), obraz poglądowy (rodzaj, cały obszar, Sprawdź
/// wg Ie-104 § 6 ust. 4, nieumieszczone tylko z § 6, eksport bez zmian – poglądowy tylko w edytorze), PNG i wydruk bez znaczników edytora.
/// </summary>
public static partial class Test
{
    static async Task RunE8(MainWindow w, string shot)
    {
        var dir = Path.Combine(Path.GetTempPath(), "edytor_zob2_test_e8"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        var ut = Path.Combine(dir, "mijanka_testowa.ut.json"); var v1 = Path.Combine(dir, "mijanka_testowa.zob.json");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Testy", "mijanka_testowa.ut.json"), ut);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Testy", "mijanka_testowa.zob.json"), v1);
        Ok(w.OpenPath(v1) && w.SaveDoc(false), "mijanka_testowa – wersja 1 otwarta i zapisana jako wersja 2");
        await Settle(w);
        var jBase = Lcs.Model.DoSymulatora.Buduj(w.Doc.Ut, w.Kompiluj().Zob, "test").Json;
        var src = w.Obraz;

        // --- kopia obrazu
        Click(Btn(w.ImgPanel, "Kopiuj obraz"));
        var kop = w.Obraz; int nEl = kop.Elementy.Count;
        Ok(w.Doc.P.Obrazy.Count == 2 && w.Doc.P.Obrazy[1].Id == kop.Id && kop.Id != src.Id && kop.Nazwa == src.Nazwa + " (kopia)"
            && kop.Odcinki.Count == src.Odcinki.Count && kop.Elementy.Count == src.Elementy.Count && kop.Przypiecia.Count == src.Przypiecia.Count,
            $"Kopiuj obraz: zakładka zaraz za obrazem, „{kop.Nazwa}”, ten sam rysunek, symbole i przypięcia ({kop.Odcinki.Count} odcinków, {kop.Elementy.Count} elementów)");

        // --- rodzaj: poglądowy – cały obszar
        w.KindBox.SelectedIndex = Array.FindIndex(ZobObraz.Rodzaje, r => r.Key == ZobObraz.Pogladowy);
        await Settle(w);
        Ok(w.Obraz.Rodzaj == ZobObraz.Pogladowy && w.Obraz.Posterunki.Count == w.Doc.Ut.Posts.Count, "rodzaj: obraz poglądowy – wszystkie posterunki UT (Ie-104 § 6)");
        // tarcza manewrowa na torze – na obrazie poglądowym jej nie ma (Ie-104 § 6 ust. 4)
        var poz = w.Obraz.Odcinki.Where(e => ObrGraf.Poziomy(w.Obraz, e)).Select(e => ObrGraf.Konce(w.Obraz, e)).OrderByDescending(k => Math.Abs(k.B[0] - k.A[0])).First();
        Key(w, K.D2); w.View.SimClick(S(w, (poz.A[0] + poz.B[0]) / 2.0 + 20, poz.A[1])); Key(w, K.Escape); Key(w, K.Escape);
        var spr = w.Sprawdz();
        var pogl = spr.Where(x => x.Tekst.StartsWith("obraz poglądowy:")).ToList();
        Ok(pogl.Count > 0, $"Sprawdź: symbole spoza Ie-104 § 6 ust. 4 – ostrzeżenia ({pogl.Count}: {string.Join("; ", pogl.Take(2).Select(x => x.Tekst))})");
        // uproszczenie: zdjęte tarcze, wykolejnice, PK, opisy odcinków, perony i kozły
        var zbedne = w.Obraz.Elementy.Where(e => e.Typ is ObrSymbole.Tarcza or ObrSymbole.Wykolejnica or ObrSymbole.Pk or ObrSymbole.OpisOdcinka or ObrSymbole.Peron or ObrSymbole.Koziol).Select(e => e.Id).ToList();
        w.View.Select(null, null, zbedne); Key(w, K.Delete);
        int poUpr = w.Obraz.Elementy.Count;
        spr = w.Sprawdz();
        Ok(!spr.Any(x => x.Tekst.StartsWith("obraz poglądowy:")) && !spr.Any(x => x.Tekst.StartsWith("nie ma na obrazie") && (x.Tekst.Contains("kozioł", StringComparison.OrdinalIgnoreCase) || x.Tekst.Contains("tarcza", StringComparison.OrdinalIgnoreCase))) && spr.Count(x => x.Blad) == 0,
            $"uproszczony obraz poglądowy ({zbedne.Count} elementów zdjętych): bez ostrzeżeń § 6, kozłów i tarcz nie wymaga, bez błędów ({string.Join("; ", spr.Take(3).Select(x => x.Tekst))})");
        var roz = Roznice(jBase, Lcs.Model.DoSymulatora.Buduj(w.Doc.Ut, w.Kompiluj().Zob, "test").Json);
        Ok(roz.Count == 0, $"eksport do symulatora bez zmian – obraz poglądowy tylko w edytorze ({(roz.Count == 0 ? "bez różnic" : string.Join("; ", roz.Take(3)))})");
        var pw = w.Podglad();
        Ok(pw != null && pw.Station != null, $"podgląd pulpitu przy obrazie poglądowym: {w.LastMessage}");
        pw?.Close();

        // --- PNG: jak na pulpicie, bez siatki, stref i znaczników
        var png = Path.Combine(dir, "poglad.png");
        Ok(w.SavePng(png) && File.Exists(png), $"PNG: {w.LastMessage}");
        var bmp = new FormatConvertedBitmap(BitmapFrame.Create(new Uri(png), BitmapCreateOptions.None, BitmapCacheOption.OnLoad), PixelFormats.Bgra32, null, 0);
        var px = new byte[bmp.PixelWidth * bmp.PixelHeight * 4]; bmp.CopyPixels(px, bmp.PixelWidth * 4, 0);
        byte[] P(int x, int y) => px.Skip((y * bmp.PixelWidth + x) * 4).Take(3).ToArray();
        var strefa = w.Obraz.Strefy[0].Prost;
        bool czarne(byte[] c) => c.All(b => b < 8);
        int jasne = 0; for (int i = 0; i < px.Length; i += 4 * 7) if (px[i] > 60 || px[i + 1] > 60 || px[i + 2] > 60) jasne++;
        Ok(bmp.PixelWidth == w.Obraz.Szer && bmp.PixelHeight == w.Obraz.Wys && czarne(P(strefa[0] + 10, strefa[1] + 10)) && czarne(P(w.Doc.P.Siatka * 3, w.Doc.P.Siatka * 3)) && jasne > 100,
            $"PNG {bmp.PixelWidth} × {bmp.PixelHeight} px: tło pulpitu bez siatki i strefy, tory i symbole widać ({jasne} jasnych próbek)");
        var dv = w.Wydruk(w.Obraz, 1100, 780);
        Ok(!dv.ContentBounds.IsEmpty && dv.ContentBounds.Width > 1000 && dv.ContentBounds.Height <= 780, $"wydruk: obraz na jednej stronie poziomo ({dv.ContentBounds.Width:0} × {dv.ContentBounds.Height:0})");

        // --- kolejność zakładek, zapis i odczyt
        Click(Btn(w.ImgPanel, "◀"));
        Ok(w.Doc.P.Obrazy[0].Id == kop.Id, "◀ – obraz poglądowy pierwszą zakładką");
        w.DoUndo();
        Ok(w.Doc.P.Obrazy[1].Id == kop.Id, "Ctrl+Z – kolejność jak przed");
        Ok(w.SaveDoc(false), "Zapisz");
        var d2 = ObrDocument.Load(w.Doc.Path);
        Ok(d2.P.Obrazy.Count == 2 && d2.P.Obrazy[1].Rodzaj == ZobObraz.Pogladowy && d2.P.Obrazy[1].Elementy.Count == poUpr && poUpr == nEl + 1 - zbedne.Count,
            "odczyt: dwa obrazy, drugi poglądowy, uproszczony");
        // numer toru cyframi, gdy fokus ucieka z pola opisu na płótno (uwaga właściciela 2026-10-22: „numeru toru dalej nie mogę dodać”)
        w.ShowImage(src.Id); await Settle(w);
        var tor = w.Obraz.Odcinki.Where(e => ObrGraf.Poziomy(w.Obraz, e)).Select(e => ObrGraf.Konce(w.Obraz, e)).OrderByDescending(k => Math.Abs(k.B[0] - k.A[0])).First();
        int nNum = w.Obraz.Elementy.Count(e => e.Typ == ObrSymbole.NumerToru);
        Key(w, K.N); w.View.SimClick(S(w, (tor.A[0] + tor.B[0]) / 2.0 - 40, tor.A[1]));
        w.View.Focus(); await Settle(w);
        Ok(w.OpisOtwarty, "numer toru: fokus zabrany przez płótno zaraz po kliknięciu – pole opisu zostaje otwarte (nie zatwierdza się puste)");
        Key(w, K.D1);
        Ok(w.View.Tool == ObrTool.Symbol && w.View.SymbolTyp == ObrSymbole.NumerToru && w.OpisOtwarty, "cyfra 1 przy otwartym polu – do pola, nie skrót palety (semafor)");
        Type(w.OpisEdit, "1"); Key(w, K.Enter);
        var num = w.Obraz.Elementy.Where(e => e.Typ == ObrSymbole.NumerToru).ToList();
        Ok(num.Count == nNum + 1 && num.Any(e => e.Opisy[0].Tekst == "1") && !w.OpisOtwarty, $"Enter – numer toru „1” na torze ({w.LastMessage})");
        Key(w, K.Escape);
        // numer toru niewidoczny – tylko do przypięcia (pytanie właściciela 2026-10-24)
        Key(w, K.F8);
        var n1 = w.Obraz.Elementy.Last(e => e.Typ == ObrSymbole.NumerToru && e.Opisy[0].Tekst == "1");
        int przed = w.Kompiluj().Zob.Obrazy.First(x => x.Id == w.Obraz.Id).NumeryTorow.Count;
        w.View.Select(null, null, new[] { n1.Id }); w.Side.SelectedItem = w.TabWlasciwosci; await Settle(w);
        var odm = All<System.Windows.Controls.ComboBox>(w.PropPanel).FirstOrDefault(c => c.Items.Cast<object>().Any(i => (i as string)?.StartsWith("numer toru niewidoczny") == true));
        Ok(odm != null, "Właściwości numeru toru: odmiana „widoczny / niewidoczny – tylko do przypięcia”");
        if (odm != null) { odm.SelectedIndex = odm.Items.Cast<object>().ToList().FindIndex(i => (i as string)?.StartsWith("numer toru niewidoczny") == true); await Settle(w); }
        n1 = w.Obraz.Elementy.First(e => e.Id == n1.Id);
        int po = w.Kompiluj().Zob.Obrazy.First(x => x.Id == w.Obraz.Id).NumeryTorow.Count;
        Key(w, K.F8);
        Ok(ObrSymbole.Ukryty(n1) && po == przed - 1 && Pr(w, n1.Id)?.Stan is ObrPrzypiecie.Pewne or ObrPrzypiecie.Potwierdzone or ObrPrzypiecie.DoWyjasnienia,
            $"numer toru niewidoczny: nie trafia na pulpit (numerów torów {przed} → {po}), dalej przypinany przy F8 ({Pr(w, n1.Id)?.Stan})");
        // Właściwości toru rysunku: przypięcie do toru UT z id, odcinki kontroli niezajętości, wybór toru UT (uwaga właściciela 2026-10-22)
        var ed = w.Obraz.Odcinki.Where(e => ObrGraf.Poziomy(w.Obraz, e) && Pr(w, e.Id)?.Tor != null).OrderByDescending(e => { var (a1, b1) = ObrGraf.Konce(w.Obraz, e); return Math.Abs(b1[0] - a1[0]); }).First();
        w.View.Select(null, new[] { ed.Id }); w.Side.SelectedItem = w.TabWlasciwosci; await Settle(w);
        var teksty = All<System.Windows.Controls.TextBlock>(w.PropPanel).Select(t => t.Text).ToList();
        var trId = Pr(w, ed.Id).Tor.Tor;
        Ok(teksty.Any(t => t == "Przypięcie do UT") && teksty.Any(t => t.Contains("id " + trId) && t.Contains("km")) && teksty.Any(t => t.StartsWith("Odcinki kontroli niezajętości")),
            $"Właściwości toru: przypięcie z id toru UT i km, odcinki kontroli z UT ({teksty.FirstOrDefault(t => t.Contains("id " + trId))}; {teksty.SkipWhile(t => !t.StartsWith("Odcinki kontroli")).Skip(1).FirstOrDefault()})");
        if (shot != null) Save(w, Path.ChangeExtension(shot, null) + "_wlasciwosci.png");
        var cb = All<System.Windows.Controls.ComboBox>(w.PropPanel).First(c => c.Items.Count > 1 && (c.Items[0] as string) == "(wybierz tor UT…)");
        int idx = Enumerable.Range(1, cb.Items.Count - 1).First(i => (cb.Items[i] as string).Contains("id " + trId));
        cb.SelectedIndex = idx; await Settle(w);
        Ok(Pr(w, ed.Id)?.Stan == ObrPrzypiecie.Potwierdzone && Pr(w, ed.Id).Tor?.Tor == trId, $"wybór toru UT z listy we Właściwościach – przypięcie potwierdzone ({w.LastMessage})");
        w.DoUndo();
        if (shot != null)
        {
            File.Copy(png, Path.ChangeExtension(shot, null) + "_png.png", true);
            w.View.Fit(); await Settle(w); Save(w, shot);
        }
    }
}

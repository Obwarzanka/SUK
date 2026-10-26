using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Lcs.Model;
using K = System.Windows.Input.Key;

namespace Lcs.EdytorZobrazowania2;

/// <summary>
/// Test etapu E4 (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 10): mijanka z UT testu (tor 1 z szlakami, tor 2 z kozłami, łączniki 1/2 i 3/4, semafory A, B, F)
/// narysowana klikami bez UT, potem „Przypnij do UT” (F8): kotwice po numerach i nazwach, tory i km, rozjazd bez numeru z sąsiedztwa,
/// lista „do wyjaśnienia” – wybór kandydata i „bez przypięcia”, „Weź z UT”, nieumieszczone, przypięcia przy zmianie rysunku, „Sprawdź” bez błędów.
/// </summary>
public static partial class Test
{
    static ObrPrzypiecie Pr(MainWindow w, string el) => w.Obraz.Przypiecia.FirstOrDefault(p => p.El == el);
    static string UtNr(MainWindow w, string el) => Pr(w, el)?.Ut is string u ? ObrPrzypinanie.NazwaUt(w.Doc.Ut, u) : null;
    static int Wyj(MainWindow w) => w.Obraz.Przypiecia.Count(p => p.Stan == ObrPrzypiecie.DoWyjasnienia);

    static async Task RunE4(MainWindow w, string shot)
    {
        var dir = Path.Combine(Path.GetTempPath(), "edytor_zob2_test_e4"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        var ut = Path.Combine(dir, "mijanka.ut.json");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Testy", "mijanka.ut.json"), ut);
        Ok(w.NewFromUt(ut), "nowe zobrazowanie do mijanki z testu UT");
        var u = w.Doc.Ut;
        await Settle(w); w.View.Fit();
        Type(w.RzedyText, "448 544");

        // --- rysunek bez UT: tor 1, tor 2, łączniki 1/2 i 3/4
        Key(w, K.T);
        w.View.SimClick(S(w, 96, 448)); w.View.SimClick(S(w, 1824, 448)); Key(w, K.Enter);
        w.View.SimClick(S(w, 256, 544)); w.View.SimClick(S(w, 1664, 544)); Key(w, K.Enter);
        w.View.SimClick(S(w, 464, 448)); w.View.SimClick(S(w, 560, 544));
        w.View.SimClick(S(w, 1360, 544)); w.View.SimClick(S(w, 1456, 448));
        Ok(Rozjazdy(w) == 4 && ObrGraf.Uwagi(w.Obraz).Count == 0, $"mijanka: 4 rozjazdy ({Graf(w)})");
        // numery zwrotnic 1, 2, 3 (rozjazd 1360 bez numeru – ma się znaleźć z sąsiedztwa)
        Key(w, K.V);
        foreach (var (x, y, n) in new[] { (464, 448, "1"), (560, 544, "2"), (1456, 448, "3") }) { w.View.SimClick(S(w, x, y)); Key(w, K.E); Wpisz(w, n); }
        // kozły na końcach toru 2, semafory A, B (wjazdowe), semafor bez nazwy na torze 2 daleko od F, przerwa, numery torów
        Key(w, K.D7); w.View.SimClick(S(w, 256, 544)); w.View.SimClick(S(w, 1664, 544));
        Key(w, K.D1); Wstaw(w, 200, 448, "A"); Wstaw(w, 1720, 448, "B");
        Key(w, K.D1); Key(w, K.Space); Wstaw(w, 1600, 544, "");
        Key(w, K.D5); w.View.SimClick(S(w, 320, 448));
        Key(w, K.N); Wstaw(w, 960, 448, "1"); Wstaw(w, 960, 544, "2");
        Key(w, K.Escape);

        // --- F8
        Ok(w.Obraz.Przypiecia.Count == 0 && w.PrzypnijBtn.IsEnabled, "przed F8: bez przypięć, przycisk „Przypnij do UT” czynny");
        Key(w, K.F8);
        Ok(w.Side.SelectedItem == w.TabWyjasnienia && w.LastMessage.StartsWith("Przypięte"), $"F8 – zakładka „Do wyjaśnienia” ({w.LastMessage})");
        string Nr(int x, int y) => UtNr(w, W(w, x, y).Id);
        Ok(Nr(464, 448) == "1" && Nr(560, 544) == "2" && Nr(1456, 448) == "3" && Pr(w, W(w, 464, 448).Id).Stan == ObrPrzypiecie.Pewne, "rozjazdy 1, 2, 3 przypięte po numerach (pewne)");
        Ok(Nr(1360, 544) == "4" && Pr(w, W(w, 1360, 544).Id).Stan == ObrPrzypiecie.Pewne, "rozjazd bez numeru → rozjazd 4 (jedyny wolny na torze 2)");
        var a = El(w, ObrSymbole.Semafor, "A"); var b = El(w, ObrSymbole.Semafor, "B");
        Ok(UtNr(w, a.Id) == "A" && UtNr(w, b.Id) == "B", "semafory A i B po nazwach");
        // tory i km
        var t1 = u.Tracks.First(t => t.Number == "1").Id; var t2 = u.Tracks.First(t => t.Number == "2").Id;
        // km odcinka od punktu (x0, y0) do (x1, y1)
        ZobUtTor Km(int x0, int y0, int x1, int y1)
        {
            var wa = W(w, x0, y0); var wb = W(w, x1, y1); if (wa == null || wb == null) return null;
            var e = w.Obraz.Odcinki.FirstOrDefault(x => (x.A == wa.Id && x.B == wb.Id) || (x.A == wb.Id && x.B == wa.Id));
            var t = e == null ? null : Pr(w, e.Id)?.Tor; if (t == null) return null;
            return e.A == wa.Id ? t : new ZobUtTor { Tor = t.Tor, S0 = t.S1, S1 = t.S0 };
        }
        var k1 = Km(464, 448, 1456, 448);
        Ok(k1?.Tor == t1 && Math.Abs(k1.S0 - 400) < 0.5 && Math.Abs(k1.S1 - 800) < 0.5, $"tor 1 między rozjazdami 1 i 3: km 400 → 800 ({k1?.S0:0} → {k1?.S1:0})");
        var k0 = w.Obraz.Odcinki.Where(e => ObrGraf.Konce(w.Obraz, e) is var (pa, pb) && (pa[0] == 96 || pb[0] == 96)).Select(e => Pr(w, e.Id)?.Tor).FirstOrDefault();
        Ok(k0?.Tor == t1 && Math.Min(k0.S0, k0.S1) < 0.5, $"koniec toru 1 (x 96) – km 0 (koniec toru UT)");
        var kz = Km(256, 544, 560, 544);
        Ok(kz?.Tor == t2 && Math.Abs(kz.S0) < 0.5 && Math.Abs(kz.S1 - 478) < 0.5, $"tor 2 od kozła do rozjazdu 2: km 0 → 478 ({kz?.S0:0} → {kz?.S1:0})");
        var kl = Km(464, 448, 560, 544);
        var l12 = u.Turnouts.First(z => z.Number == "1").Branch;
        Ok(kl?.Tor == l12 && Math.Abs(kl.S0) < 0.5 && Math.Abs(kl.S1 - Trasa.Compute(u.Tracks.First(t => t.Id == l12)).Length) < 0.5, "łącznik 1/2 – tor łączący od km 0 do końca");
        var koz = w.Obraz.Elementy.Where(e => e.Typ == ObrSymbole.Koziol).ToList();
        Ok(koz.All(e => Pr(w, e.Id)?.Stan == ObrPrzypiecie.Pewne), "kozły przypięte do kozłów UT na końcach toru 2");
        Ok(Pr(w, El(w, ObrSymbole.NumerToru, "1").Id)?.Ut == t1 && Pr(w, El(w, ObrSymbole.NumerToru, "2").Id)?.Ut == t2, "numery torów 1 i 2 zgodne z torami");

        // --- do wyjaśnienia: semafor bez nazwy (F daleko) i przerwa bez licznika w UT
        var sem = w.Obraz.Elementy.First(e => e.Typ == ObrSymbole.Semafor && e.Opisy[0].Tekst == "");
        var prz = w.Obraz.Elementy.First(e => e.Typ == ObrSymbole.Przerwa);
        var fId = u.Devices.First(d => d.Name == "F").Id;
        Ok(Wyj(w) == 2 && Pr(w, sem.Id).Stan == ObrPrzypiecie.DoWyjasnienia && Pr(w, sem.Id).Kandydaci?.Contains(fId) == true && Pr(w, prz.Id).Powod.StartsWith("brak w UT"),
            $"do wyjaśnienia: semafor bez nazwy (kandydat F – {Pr(w, sem.Id).Powod}), przerwa ({Pr(w, prz.Id).Powod})");
        await Settle(w);
        var poz = All<Border>(w.WyjPanel).Where(x => x.Tag is string).ToList();
        Ok(poz.Count == 2 && All<Button>(w.WyjPanel).Any(x => x.Tag as string == fId), "zakładka: dwie pozycje, przycisk kandydata F");
        // najazd – podświetlenie na obrazie i w podglądzie UT
        poz.First(x => (string)x.Tag == sem.Id).RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseEnterEvent });
        Ok(w.View.SelEl.Contains(sem.Id) && w.PodgladUt.HiPunkty.Count > 0, "najazd na pozycję: semafor zaznaczony, punkt F w podglądzie UT");
        Click(All<Button>(w.WyjPanel).First(x => x.Tag as string == fId));
        Ok(Pr(w, sem.Id).Stan == ObrPrzypiecie.Potwierdzone && Pr(w, sem.Id).Ut == fId && Wyj(w) == 1, "klik kandydata F – przypięcie potwierdzone");
        var bp = All<Border>(w.WyjPanel).First(x => x.Tag as string == prz.Id);
        Click(All<Button>(bp).First(x => x.Content as string == "bez przypięcia"));
        Ok(Pr(w, prz.Id).Stan == ObrPrzypiecie.Brak && Wyj(w) == 0, "przerwa – „bez przypięcia”; lista pusta");
        await Settle(w);
        Ok(All<TextBlock>(w.WyjPanel).Any(t => t.Text.StartsWith("Lista pusta")), "zakładka: „Lista pusta – wszystko przypięte”");

        // --- Sprawdź: opis semafora pusty (w UT „F”) – Weź z UT; potem bez błędów
        Key(w, K.F7);
        Ok(w.Uwagi.Any(x => x.Akcja == ObrSprawdz.WezZUt && x.El == sem.Id), "Sprawdź: opis pusty, w UT „F” – „Weź z UT”");
        await Settle(w);
        Click(WezZUtDla(w, "w UT „F”"));
        Ok(El(w, ObrSymbole.Semafor, "F") != null, "Weź z UT – semafor ma nazwę F");
        var spr = w.Sprawdz();
        Ok(spr.Count(x => x.Blad) == 0, $"Sprawdź bez błędów ({string.Join("; ", spr.Where(x => x.Blad).Select(x => x.Tekst))})");
        // semafor odstępowy S60 – na obrazie dopiero z odstępami SBL szlaku (E6); ta mijanka szlaków nie rysuje
        bool Szlak(ObrUwaga x) => x.Tekst.Contains("odstępy SBL");
        Ok(!spr.Any(x => x.Tekst.StartsWith("nie ma na obrazie") && !Szlak(x)), $"nieumieszczonych brak poza S60 (szlak – E6) ({string.Join("; ", ObrPrzypinanie.Nieumieszczone(u, w.Obraz).Select(n => n.Opis))})");
        spr = spr.Where(x => !Szlak(x)).ToList();
        Ok(spr.Count == 1 && spr[0].El == W(w, 1360, 544).Id && spr[0].Akcja == ObrSprawdz.WezZUt, $"jedno ostrzeżenie: rozjazd bez numeru – w UT 4, „Weź z UT” ({string.Join("; ", spr.Select(x => x.Tekst))})");
        await Settle(w);
        Click(WezZUtDla(w, "w UT rozjazd 4"));
        Ok(El(w, ObrSymbole.Zwrotnica, "4")?.Na == W(w, 1360, 544).Id && w.Sprawdz().Count(x => !Szlak(x)) == 0, "Weź z UT – numer zwrotnicy 4 na obrazie; Sprawdź bez uwag (poza S60 – szlak)");

        // --- przypięcia idą za rysunkiem; kolejny F8 nie zmienia potwierdzonych
        int nPr = w.Obraz.Przypiecia.Count;
        w.View.ClearSelection(); w.View.SimDrag(S(w, 1100, 544), S(w, 1100, 576));
        Ok(w.Obraz.Przypiecia.Count == nPr && Pr(w, sem.Id)?.Stan == ObrPrzypiecie.Potwierdzone, "przesunięcie toru 2 – przypięcia zostają");
        Key(w, K.F8);
        Ok(Wyj(w) == 0 && Pr(w, sem.Id).Stan == ObrPrzypiecie.Potwierdzone && Pr(w, prz.Id).Stan == ObrPrzypiecie.Brak && Nr(1328, 576) == "4", "F8 po zmianie (rozjazd 4 przesunięty po ukosie do x 1328): lista pusta, potwierdzone i „bez przypięcia” zostają");
        w.DoUndo(); w.DoUndo();

        // --- opis różny od UT po zmianie tekstu; zła nazwa – do wyjaśnienia
        w.View.SimClick(S(w, 200, 448)); Key(w, K.E); Wpisz(w, "A1");
        Ok(w.Sprawdz().Any(x => x.Akcja == ObrSprawdz.WezZUt && x.Tekst.Contains("„A1”") && x.Tekst.Contains("„A”")), "opis A1, w UT A – uwaga z „Weź z UT” (opis może być inny)");
        Key(w, K.F8);
        Ok(Pr(w, a.Id).Stan == ObrPrzypiecie.Pewne && UtNr(w, a.Id) == "A" && w.Sprawdz().Any(x => x.Akcja == ObrSprawdz.WezZUt && x.El == a.Id),
            "F8: nazwy A1 nie ma w UT, ale w tym miejscu toru jest A – przypięty do A (opis może być inny, decyzja § 11 pkt 4), uwaga zostaje");
        // semafor z nazwą spoza UT daleko od semaforów UT – do wyjaśnienia
        Key(w, K.D1); Wstaw(w, 1000, 448, "X9"); Key(w, K.Escape); Key(w, K.F8);
        var x9 = El(w, ObrSymbole.Semafor, "X9");
        Ok(Pr(w, x9.Id).Stan == ObrPrzypiecie.DoWyjasnienia && Wyj(w) == 1, $"semafor X9 (nie ma w UT, w pobliżu brak semafora) – do wyjaśnienia ({Pr(w, x9.Id).Powod})");
        for (int i = 0; i < 5; i++) w.DoUndo();
        Ok(Wyj(w) == 0 && El(w, ObrSymbole.Semafor, "A") != null && El(w, ObrSymbole.Semafor, "X9") == null, "Ctrl+Z – A, bez X9, lista pusta");

        // --- zmiana UT po F8: tor 1 przedłużony w edytorze UT o 100 m (1200 → 1300); koniec toru na obrazie (x 1824) bez kotwicy, tor potwierdzony
        // przez człowieka – Sprawdź: km nieaktualne, eksport – uwaga, F8 – koniec nowego toru (nie stary km z przypięcia potwierdzonego)
        var eK = w.Obraz.Odcinki.First(e => ObrGraf.Konce(w.Obraz, e) is var (pa, pb) && (pa[0] == 1824 || pb[0] == 1824));
        w.PotwierdzPrzypiecie(eK.Id, t1);
        double KmKonca() { var q = Pr(w, eK.Id).Tor; var (pa, _) = ObrGraf.Konce(w.Obraz, eK); return pa[0] == 1824 ? q.S0 : q.S1; }
        Ok(Pr(w, eK.Id).Stan == ObrPrzypiecie.Potwierdzone && Math.Abs(KmKonca() - 1200) < 0.5 && Math.Abs(Pr(w, eK.Id).DlToru.Value - 1200) < 0.5
            && !w.Sprawdz().Any(x => x.Tekst.Contains("nieaktualne")), $"tor przy x 1824 potwierdzony: km końca 1200, w przypięciu długość toru 1200, km aktualne ({KmKonca():0})");
        var utTxt = File.ReadAllText(ut);
        var utJ = System.Text.Json.Nodes.JsonNode.Parse(utTxt);
        var vs = utJ["tracks"].AsArray().First(t => (string)t["number"] == "1")["vertices"].AsArray();
        double vx0 = (double)vs[0]["x"], vy0 = (double)vs[0]["y"], vx1 = (double)vs[^1]["x"], vy1 = (double)vs[^1]["y"];
        vs[^1]["x"] = vx0 + (vx1 - vx0) * 13 / 12; vs[^1]["y"] = vy0 + (vy1 - vy0) * 13 / 12; vs[^1].AsObject().Remove("p");
        File.WriteAllText(ut, utJ.ToJsonString()); w.ReloadUt();
        Ok(Math.Abs(Trasa.Compute(w.Doc.Ut.Tracks.First(t => t.Id == t1)).Length - 1300) < 0.5, "UT wczytany od nowa: tor 1 ma 1300 m");
        var nk = w.Sprawdz().Where(x => x.Tekst.Contains("nieaktualne")).ToList();
        Ok(nk.Count == 1 && nk[0].Blad && nk[0].Tekst.Contains("km 800–1200") && nk[0].Tekst.Contains("wg UT km 800–1300"), $"Sprawdź po zmianie UT: tor 1 km 800–1200 nieaktualne, wg UT 800–1300 – jedna uwaga ({string.Join("; ", nk.Select(x => x.Tekst))})");
        Ok(ObrKompilacja.Kompiluj(w.Doc.Ut, w.Doc.P).Uwagi.Any(x => x.Contains("km nieaktualnym")), "eksport: uwaga o km nieaktualnym");
        if (shot != null) { w.View.ClearSelection(); w.View.Fit(); await Settle(w); Save(w, Path.ChangeExtension(shot, null) + "_km_nieaktualne.png"); }
        Key(w, K.F8);
        Ok(Pr(w, eK.Id).Stan == ObrPrzypiecie.Potwierdzone && Math.Abs(KmKonca() - 1300) < 0.5 && !w.Sprawdz().Any(x => x.Tekst.Contains("nieaktualne")),
            $"F8: koniec toru 1 – km 1300 (nowy koniec toru), przypięcie nadal potwierdzone, Sprawdź bez uwagi ({KmKonca():0})");
        File.WriteAllText(ut, utTxt); w.ReloadUt(); Key(w, K.F8);
        Ok(Math.Abs(KmKonca() - 1200) < 0.5 && Wyj(w) == 0 && !w.Sprawdz().Any(x => x.Tekst.Contains("nieaktualne")), "UT z powrotem, F8 – km końca 1200, lista pusta");

        // --- tor 2 rozcięty w UT na dwa tory „2” w miejscu semafora F (km 900, F na początku nowego toru): na obrazie jedna linia bez węzła przy
        // styku – F8 dzieli odcinek węzłem w miejscu styku (wygląd bez zmian), oba kawałki przypięte, F na styku i kozioł na nowym torze bez uwag
        var utP = UtDocument.Deserialize(utTxt);
        var cw = Ciecie.Split(utP, t2, 900);
        var t2b = cw.NewTrack; utP.Tracks.First(t => t.Id == t2b).Number = "2";
        File.WriteAllText(ut, UtDocument.Serialize(utP)); w.ReloadUt();
        int nW = w.Obraz.Wezly.Count;
        Key(w, K.F8);
        var kStyk = w.Obraz.Odcinki.Select(e => Pr(w, e.Id)?.Tor).Where(t => t != null).ToList();
        Ok(cw.Error == null && w.Obraz.Wezly.Count == nW + 1 && kStyk.Any(t => t.Tor == t2 && Math.Abs(Math.Max(t.S0, t.S1) - 900) < 0.5) && kStyk.Any(t => t.Tor == t2b && Math.Min(t.S0, t.S1) < 0.5),
            $"tor 2 rozcięty w UT (km 900): F8 – węzeł w miejscu styku, tor 2 do km 900 i nowy tor 2 od km 0 ({cw.Error}, węzłów {nW} → {w.Obraz.Wezly.Count})");
        Ok(Wyj(w) == 0 && !w.Sprawdz().Any(x => x.Blad) && koz.All(e => Pr(w, e.Id)?.Stan == ObrPrzypiecie.Pewne),
            $"po podziale: lista pusta, Sprawdź bez błędów, kozły przypięte ({string.Join("; ", w.Obraz.Przypiecia.Where(p => p.Stan == ObrPrzypiecie.DoWyjasnienia).Select(p => p.Powod))})");
        // F na rysunku na kawałku toru 2, w UT na początku nowego toru – nie „na rysunku na torze 2, w UT – tor 2”
        var fEl = El(w, ObrSymbole.Semafor, "F");
        Ok(ObrSprawdz.Sprawdz(w.Doc.Ut, w.Obraz).All(x => x.El != fEl.Id), "semafor F na styku torów – bez uwagi");
        w.DoUndo();
        Ok(w.Obraz.Wezly.Count == nW, "Ctrl+Z – węzeł styku znika");
        File.WriteAllText(ut, utTxt); w.ReloadUt(); Key(w, K.F8);
        Ok(Wyj(w) == 0 && w.Obraz.Wezly.Count == nW, "UT z powrotem, F8 – bez nowego węzła, lista pusta");

        // --- styk w UT z dala od semafora (km 600): granica torów UT w miejscu styku, nie w odległym węźle rysunku; żaden kawałek
        // rysunku nie dostaje toru o zerowej długości (wcześniej szlak ŁF ściśnięty, a krótki tor rozciągnięty na resztę linii)
        utP = UtDocument.Deserialize(utTxt);
        cw = Ciecie.Split(utP, t2, 600);
        t2b = cw.NewTrack; utP.Tracks.First(t => t.Id == t2b).Number = "2";
        File.WriteAllText(ut, UtDocument.Serialize(utP)); w.ReloadUt();
        Key(w, K.F8);
        var zero = w.Obraz.Odcinki.Where(e => Pr(w, e.Id)?.Tor is ZobUtTor t && Math.Abs(t.S1 - t.S0) < 0.5).ToList();
        kStyk = w.Obraz.Odcinki.Select(e => Pr(w, e.Id)?.Tor).Where(t => t != null).ToList();
        Ok(cw.Error == null && zero.Count == 0 && kStyk.Any(t => t.Tor == t2 && Math.Abs(Math.Max(t.S0, t.S1) - 600) < 0.5) && kStyk.Any(t => t.Tor == t2b && Math.Min(t.S0, t.S1) < 0.5) && Wyj(w) == 0,
            $"tor 2 rozcięty w UT (km 600): granica w miejscu styku, bez kawałków o zerowej długości ({zero.Count}), lista pusta");
        w.DoUndo();
        File.WriteAllText(ut, utTxt); w.ReloadUt(); Key(w, K.F8);
        Ok(Wyj(w) == 0 && w.Obraz.Wezly.Count == nW, "UT z powrotem, F8 – lista pusta");

        // --- zakładka UT, zapis i odczyt
        w.Side.SelectedItem = w.TabUt; await Settle(w);
        Ok(All<TextBlock>(w.UtPanel).Any(t => t.Text == "Nieumieszczone na obrazie (1)") && w.PodgladUt.Przypiete.Count == 4, $"zakładka UT: nieumieszczony tylko S60 (szlak), w podglądzie 4 tory przypięte ({w.PodgladUt.Przypiete.Count})");
        Ok(w.SaveDoc(false), "Zapisz");
        var l = ObrDocument.Load(w.Doc.Path);
        Ok(ObrDocument.Serialize(l.P) == ObrDocument.Serialize(w.Doc.P) && l.P.Obrazy[0].Przypiecia.Count == w.Obraz.Przypiecia.Count, $"odczyt: przypięcia identyczne ({w.Obraz.Przypiecia.Count})");

        if (shot != null)
        {
            w.View.ClearSelection(); w.SetTool(ObrTool.Wskaz);
            Pokaz(w, a.Id);
            w.Side.SelectedItem = w.TabUt; w.View.Fit(); await Settle(w); Save(w, shot);
            w.Sprawdz(); await Settle(w);
            Save(w, Path.ChangeExtension(shot, null) + "_sprawdz.png");
        }
    }

    static void Pokaz(MainWindow w, string el) => w.Pokaz(el);
    /// <summary>Przycisk „Weź z UT” w wierszu uwagi zawierającym tekst.</summary>
    static Button WezZUtDla(MainWindow w, string tekst) =>
        All<DockPanel>(w.WyjPanel).Where(d => d.Children.OfType<TextBlock>().Any(t => t.Text.Contains(tekst))).SelectMany(d => d.Children.OfType<Button>()).First();
}

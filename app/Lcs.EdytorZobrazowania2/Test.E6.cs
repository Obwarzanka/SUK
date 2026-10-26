using System.IO;
using System.Text.Json.Nodes;
using System.Windows.Controls;
using Lcs.Model;
using K = System.Windows.Input.Key;

namespace Lcs.EdytorZobrazowania2;

/// <summary>
/// Test etapu E6 (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 10): mijanka z UT testu klikami, na obu końcach toru 1 szlaki jak na GrM – odstępy SBL (4 pola
/// w rzędzie, 3 w kolumnie), klocek „koniec szlaku” (strzałka blokady, semafor wjazdowy, nazwa sąsiada), okno numeru pociągu; F8 – szlaki 1A / 2A,
/// numery odstępów i nazwa sąsiada z UT, kompilacja: kreski odstępów z km odstępów UT, semafor odstępowy na końcu pola, okno PIP w pliku symulatora.
/// Plik symulatora zapisany do <c>%TEMP%/edytor_zob2_test_e6/mijanka_szlaki.json</c> – zrzut pulpitu robi skrypt testu (LcsWidzew --screenshot).
/// </summary>
public static partial class Test
{
    static async Task RunE6(MainWindow w, string shot)
    {
        var dir = Path.Combine(Path.GetTempPath(), "edytor_zob2_test_e6"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        var ut = Path.Combine(dir, "mijanka.ut.json");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Testy", "mijanka.ut.json"), ut);
        Ok(w.NewFromUt(ut), "nowe zobrazowanie do mijanki z testu UT");
        var u = w.Doc.Ut;
        await Settle(w); w.View.Fit();
        Type(w.RzedyText, "448 544");

        // --- tory (jak w E4)
        Key(w, K.T);
        w.View.SimClick(S(w, 96, 448)); w.View.SimClick(S(w, 1824, 448)); Key(w, K.Enter);
        w.View.SimClick(S(w, 256, 544)); w.View.SimClick(S(w, 1664, 544)); Key(w, K.Enter);
        w.View.SimClick(S(w, 464, 448)); w.View.SimClick(S(w, 560, 544));
        w.View.SimClick(S(w, 1360, 544)); w.View.SimClick(S(w, 1456, 448));
        Key(w, K.V);
        foreach (var (x, y, n) in new[] { (464, 448, "1"), (560, 544, "2"), (1456, 448, "3"), (1360, 544, "4") }) { w.View.SimClick(S(w, x, y)); Key(w, K.E); Wpisz(w, n); }
        Key(w, K.D7); w.View.SimClick(S(w, 256, 544)); w.View.SimClick(S(w, 1664, 544));
        Key(w, K.D1); Key(w, K.Space); Wstaw(w, 1480, 544, "F");
        Key(w, K.Escape);

        // --- odstępy SBL przy obu końcach toru 1 (rząd 4 pola, kolumna 3 – po lewej w górę, po prawej w dół)
        Ok(w.SymBtns.ContainsKey(ObrSymbole.OdstepySbl) && w.SymBtns.ContainsKey(ObrSymbole.KoniecSzlaku) && w.SymBtns.ContainsKey(ObrSymbole.PolePip), "paleta: klocki koniec szlaku, odstępy SBL, okno numeru pociągu");
        Click(w.SymBtns[ObrSymbole.OdstepySbl]);
        w.View.SimClick(S(w, 100, 450));
        var odL = w.Obraz.Elementy.LastOrDefault(e => e.Typ == ObrSymbole.OdstepySbl);
        Ok(odL != null && ObrGraf.Wezel(w.Obraz, odL.Na).P.SequenceEqual(new[] { 96, 448 }) && ObrSymbole.PolaSbl(w.Obraz, odL).Count == 7, "odstępy SBL przy końcu toru 1 (x 96): 4 pola w rzędzie i 3 w kolumnie");
        Key(w, K.Escape);
        w.Side.SelectedItem = w.TabWlasciwosci;
        w.View.SimClick(S(w, 1820, 450));
        Click(w.SymBtns[ObrSymbole.OdstepySbl]); w.View.SimClick(S(w, 1820, 450)); Key(w, K.Escape);
        var odP = w.Obraz.Elementy.LastOrDefault(e => e.Typ == ObrSymbole.OdstepySbl && e != odL);
        w.UstawPolaSbl(odP.Id, 4, 2, 1);
        var polaP = ObrSymbole.PolaSbl(w.Obraz, odP);
        Ok(polaP.Count == 6 && polaP[^1].Kolumna && polaP[^1].Ay > 448 && odP.Opisy.Count == 6, "odstępy przy końcu x 1824: 4 + 2 pola, kolumna w dół");

        // --- klocek „koniec szlaku” na obu końcach: strzałka za odstępami, semafor wjazdowy, nazwa sąsiada
        Click(w.SymBtns[ObrSymbole.KoniecSzlaku]); w.View.SimClick(S(w, 100, 450));
        Ok(w.OpisEdit.Visibility == System.Windows.Visibility.Visible, "koniec szlaku: pole nazwy semafora wjazdowego na płótnie");
        Wpisz(w, "A");
        var strL = w.Obraz.Elementy.Single(e => e.Typ == ObrSymbole.StrzalkaBlokady);
        var a = El(w, ObrSymbole.Semafor, "A");
        Ok(a != null && a.Styl == ObrSymbole.SemWjazdowy && Poz(w, a).Ux == 1 && Poz(w, strL).X > 96 + 4 * 40 && Poz(w, a).X > Poz(w, strL).X,
            $"klocek: strzałka za odstępami (x {Poz(w, strL).X:0}), semafor wjazdowy A ostrzem do stacji (x {Poz(w, a).X:0})");
        var napL = w.Obraz.Elementy.Single(e => e.Typ == ObrSymbole.Napis && e.Styl == ZobNapis.Sasiad);
        Ok(napL.Opisy[0].Tekst == "", "nazwa sąsiada – na razie pusta (z UT po F8)");
        Click(w.SymBtns[ObrSymbole.KoniecSzlaku]); w.View.SimClick(S(w, 1820, 450)); Wpisz(w, "B");
        Ok(El(w, ObrSymbole.Semafor, "B") is ObrElement bb && Poz(w, bb).Ux == -1, "koniec szlaku po prawej: semafor B ostrzem w lewo");
        // okna numeru pociągu
        Click(w.SymBtns[ObrSymbole.PolePip]); w.View.SimClick(S(w, 200, 340)); w.View.SimClick(S(w, 1700, 600)); Key(w, K.Escape);
        Ok(w.Obraz.Elementy.Count(e => e.Typ == ObrSymbole.PolePip) == 2, "dwa okna numeru pociągu");

        // --- F8: szlaki 1A / 2A, numery odstępów i nazwy sąsiadów z UT
        Key(w, K.F8);
        var l1 = u.Lines.First(l => l.Numer == "1A").Id; var l2 = u.Lines.First(l => l.Numer == "2A").Id;
        Ok(Pr(w, odL.Id)?.Ut == l1 && Pr(w, odP.Id)?.Ut == l2 && Pr(w, strL.Id)?.Ut == l1, "F8: odstępy i strzałka po lewej – szlak 1A, odstępy po prawej – 2A");
        var pipy = w.Obraz.Elementy.Where(e => e.Typ == ObrSymbole.PolePip).ToList();
        Ok(Pr(w, pipy[0].Id)?.Ut == l1 && Pr(w, pipy[1].Id)?.Ut == l2, "okna numeru pociągu – szlaki najbliższych strzałek / odstępów");
        Ok(UtNr(w, a.Id) == "A" && Wyj(w) == 0, $"semafor A, lista do wyjaśnienia pusta ({string.Join("; ", w.Obraz.Przypiecia.Where(p => p.Stan == ObrPrzypiecie.DoWyjasnienia).Select(p => p.Powod))})");
        var sas = ObrSzlak.NazwaSasiada(u, l1, w.Obraz);
        Ok(napL.Opisy[0].Tekst == sas && sas.Length > 0, $"nazwa sąsiada z UT: „{napL.Opisy[0].Tekst}”");
        Ok(odL.Opisy[0].Tekst == "S60" && odL.Opisy.Skip(1).All(d => d.Tekst == ""), $"numery odstępów z UT: pole przy stacji „S60” ({string.Join(" ", odL.Opisy.Select(d => d.Tekst))})");
        var spr = w.Sprawdz();
        Ok(spr.Count(x => x.Blad) == 0 && spr.Any(x => x.Tekst.StartsWith("odstępy SBL") && x.Tekst.Contains("pól bez semafora")), $"Sprawdź bez błędów; ostrzeżenie: pola bez semafora odstępowego w UT ({string.Join("; ", spr.Where(x => x.Blad).Select(x => x.Tekst))})");
        Ok(!ObrPrzypinanie.Nieumieszczone(u, w.Obraz).Any(n => n.Opis.Contains("S60")), "S60 – na obrazie przez odstępy SBL (nie ma go w nieumieszczonych)");
        // opis zmieniony przez człowieka zostaje, „Weź z UT” wraca do UT
        Type(All<TextBox>(w.PropPanel).FirstOrDefault() ?? new TextBox(), "");
        w.View.Select(null, null, new[] { napL.Id }); await Settle(w);
        var nb = All<TextBox>(w.PropPanel).First(); Type(nb, "Andrzejów Wsch.");
        Ok(w.Sprawdz().Any(x => x.Akcja == ObrSprawdz.WezZUt && x.El == napL.Id), "nazwa sąsiada inna niż w UT – uwaga z „Weź z UT”");
        Key(w, K.F8);
        Ok(napL.Opisy[0].Tekst == "Andrzejów Wsch.", "F8 nie nadpisuje nazwy wpisanej przez człowieka");
        w.WezZUt(napL.Id);
        Ok(w.Obraz.Elementy.First(e => e.Id == napL.Id).Opisy[0].Tekst == sas, "Weź z UT – nazwa z UT");

        // semafor B bez trójkąta końca przebiegu wyjazdowego (odmiana) – na pulpicie też
        w.UstawStylEl(El(w, ObrSymbole.Semafor, "B").Id, ObrSymbole.SemWjazdowyBez);
        // --- kompilacja i plik symulatora
        var k = w.Kompiluj();
        var zo = k.Zob.Obrazy[0];
        var t1 = u.Tracks.First(t => t.Number == "1").Id;
        var kreski = zo.Tory.Where(t => t.Ut.Tor == t1 && t.Pkt.Count == 2 && t.Pkt[0][1] == t.Pkt[1][1] && t.Pkt.Max(q => q[0]) < 340 && Math.Max(t.Ut.S0, t.Ut.S1) <= 150.5).ToList();
        Ok(kreski.Any(t => Math.Abs(t.Ut.S0 - 150) < 0.5 && Math.Abs(t.Ut.S1 - 60) < 0.5) && kreski.Any(t => Math.Abs(t.Ut.S0 - 60) < 0.5 && Math.Abs(t.Ut.S1) < 0.5),
            $"kreski odstępów szlaku 1A: km 150 → 60 i 60 → 0 ({kreski.Count} kresek toru 1)");
        var aP = zo.Sygnalizatory.First(s => s.Ut == u.Devices.First(d => d.Name == "A").Id).P;
        Ok(!zo.Tory.Any(t => t.Ut.Tor == t1 && !kreski.Contains(t) && Math.Min(t.Ut.S0, t.Ut.S1) < 149.5) && kreski.Any(t => t.Pkt[0].SequenceEqual(aP) && Math.Abs(t.Ut.S0 - 150) < 0.5),
            $"tor szlaku za semaforem A zastąpiony kreskami odstępów; pierwsze pole od punktu semafora A ({aP[0]}, {aP[1]})");
        var s60 = u.Devices.First(d => d.Name == "S60").Id;
        Ok(zo.Sygnalizatory.Any(s => s.Ut == s60) && zo.PolaPip?.Count == 2, "semafor odstępowy S60 na końcu pola; dwa okna PIP");
        var sim = Path.Combine(dir, "mijanka_szlaki.json");
        Ok(w.DoSymulatora(sim) != null && File.Exists(sim), $"Do symulatora: {Path.GetFileName(sim)}");
        var js = JsonNode.Parse(File.ReadAllText(sim));
        Ok(js["display"]?["trainFields"]?.AsArray().Count == 2 && js["display"]["trainFields"][0]["block"]?.GetValue<string>() == "1A", "plik symulatora: display.trainFields – 2 okna (1A, 2A)");
        Ok(js["display"]?["noEndMark"]?.AsArray().Count == 1, $"plik symulatora: display.noEndMark – semafor B bez trójkąta ({js["display"]?["noEndMark"]?.ToJsonString()})");
        var pw = w.Podglad();
        Ok(pw != null && pw.Station.View.Signals.Values.Count(x => x.NoEndMark) == 1 && pw.Station.View.TrainFields.Count == 2, $"podgląd pulpitu: okna PIP w modelu pulpitu ({w.LastMessage})");
        if (shot != null && pw != null) { OffScreen(pw); pw.Show(); await Settle(pw); Save(pw, Path.ChangeExtension(shot, null) + "_podglad.png"); }
        pw?.Close();

        if (shot != null)
        {
            w.View.ClearSelection(); w.SetTool(ObrTool.Wskaz); w.View.Fit(); await Settle(w); Save(w, shot);
            w.View.Actual(); w.View.CenterOn(330, 430); await Settle(w); Save(w, Path.ChangeExtension(shot, null) + "_szlak.png");
        }
    }
}

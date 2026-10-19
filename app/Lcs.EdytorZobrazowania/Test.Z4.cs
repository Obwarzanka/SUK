using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Lcs.Model;

namespace Lcs.EdytorZobrazowania;

/// <summary>
/// Test etapu Z4 (PLAN_EDYTOR_ZOBRAZOWANIA.md § 5, decyzje § 10) jak praca użytkownika, na mijance testowej (Testy/mijanka_testowa.*: tory,
/// zwrotnice, sygnalizatory i kozły już na obrazie): przerwy izolacyjne (8 liczników osi), opisy odcinków, numery torów, perony, przejazd,
/// strzałki blokady szlaków 1Z / 1W, napisy, tor bez kontroli niezajętości, opisy odcinków na pulpicie, „Sprawdź” – bez uwag, zapis i odczyt.
/// </summary>
public static partial class Test
{
    static async Task RunZ4(MainWindow w, string shot)
    {
        var dir = Path.Combine(Path.GetTempPath(), "edytor_zob_test", "z4"); Directory.CreateDirectory(dir);
        foreach (var f in new[] { "mijanka_testowa.ut.json", "mijanka_testowa.zob.json" }) File.Copy(Path.Combine(AppContext.BaseDirectory, "Testy", f), Path.Combine(dir, f), true);
        Ok(w.OpenPath(Path.Combine(dir, "mijanka_testowa.zob.json")), "otwarta mijanka testowa (tory, zwrotnice, sygnalizatory, kozły na obrazie)");
        await Settle(w); w.View.Fit();
        var ut = w.Doc.Ut;
        Point S(double x, double y) => w.View.ToScreen(x, y);
        ZobTor Piece(string trNr, double km) => ZobSymbole.Kawalki(w.Obraz, ut.Tracks.First(t => t.Number == trNr).Id, km).First();
        // x na torze obrazu dla km UT – liniowo między symbolami zaczepionymi na torze (jak przy przenoszeniu na pulpit)
        int[] At(string trNr, double km)
        {
            var t = Piece(trNr, km);
            var anchors = new List<(double Km, int X)> { (t.Ut.S0, t.Pkt[0][0]), (t.Ut.S1, t.Pkt[^1][0]) };
            foreach (ZobRodzaj r in Enum.GetValues<ZobRodzaj>().Where(ZobSymbole.Kotwica))
                foreach (var s in ZobSymbole.Lista(w.Obraz, r))
                    if (s.P != null && ZobSymbole.Miejsce(ut, s) is var (tr, k) && tr == t.Ut.Tor && ZobSymbole.NaTorze(t, s.P)) anchors.Add((k, s.P[0]));
            anchors = anchors.OrderBy(a => a.Km).ToList();
            for (int i = 1; i < anchors.Count; i++)
                if (km <= anchors[i].Km + 1e-6)
                {
                    var (k0, x0) = anchors[i - 1]; var (k1, x1) = anchors[i];
                    double x = k1 - k0 < 1e-6 ? x1 : x0 + (x1 - x0) * (km - k0) / (k1 - k0);
                    return new[] { (int)Math.Round(x / 8) * 8, t.Pkt[0][1] };
                }
            return new[] { anchors[^1].X, t.Pkt[0][1] };
        }
        List<ZobUwaga> Sp(string zawiera) => w.Sprawdz().Where(x => x.Text.Contains(zawiera)).ToList();

        var u0 = w.Sprawdz();
        Ok(u0.Count(x => x.Text.StartsWith("nie ma na obrazie")) == 8 + 8 + 2 + 2 + 1 + 2 && !u0.Any(x => x.Error),
            $"Sprawdź przed Z4: nieumieszczone 8 przerw, 8 opisów odcinków, 2 numery torów, 2 perony, przejazd, 2 szlaki ({u0.Count})");

        // --- przerwy izolacyjne: liczniki osi w miejscu z km UT (przy semaforach – w punkcie semafora)
        w.ToolKey(Key.P);
        Ok(w.View.Tool == ZobTool.Przerwa && w.SymCurrent != null && w.LastMessage.StartsWith("Przerwa:"), $"P – narzędzie Przerwa ({w.LastMessage})");
        foreach (var d in ut.Devices.Where(x => x.Kind == UtDevice.LicznikOsi).OrderBy(x => x.Name))
        {
            var tr = ut.Tracks.First(t => t.Id == d.Track).Number;
            Leaf(w, $"{d.Name} – ").IsSelected = true;
            var p = At(tr, d.S);
            w.View.SimClick(S(p[0], p[1]));
        }
        Ok(w.Obraz.Przerwy.Count == 8 && Sp("przerwa").Count == 0, $"8 przerw izolacyjnych na obrazie, bez uwag ({w.LastMessage})");
        var lo1 = ut.Devices.First(x => x.Name == "lo1");
        var sygn103 = w.Obraz.Sygnalizatory.First(s => ut.Devices.First(d => d.Id == s.Ut).Track == lo1.Track && Math.Abs(ut.Devices.First(d => d.Id == s.Ut).S - lo1.S) < 1);
        Ok(w.Obraz.Przerwy.First(x => x.Ut == lo1.Id).P.SequenceEqual(sygn103.P), "lo1 w miejscu semafora odstępowego (ten sam km)");
        Leaf(w, "lo2 – ").IsSelected = true;
        w.View.SimClick(S(At("1", 300)[0], 520));
        var m2 = w.LastMessage;
        Ok(Sp("kolejność").Count > 0, $"lo2 przestawiony przed lo1 – błąd kolejności ({m2})");
        w.DoUndo();
        Ok(Sp("kolejność").Count == 0, "Ctrl+Z – kolejność dobra");

        // --- opisy odcinków: nad torem
        w.ToolKey(Key.O);
        foreach (var sc in ut.Sections)
        {
            var tr = ut.Tracks.First(t => t.Id == sc.Track).Number;
            Leaves(w).First(l => l.Tag is ZobUtObj { Grupa: ZobUt.GOdcinki } x && x.Id == sc.Id).IsSelected = true;
            var p = At(tr, sc.S);
            w.View.SimClick(S(p[0], p[1] - 16));
        }
        Ok(w.Obraz.Odcinki.Count == 8 && Sp("opis odcinka").Count == 0, $"8 opisów odcinków nad torami ({w.LastMessage})");
        w.ToolKey(Key.V);
        var t1 = ut.Sections.First(x => x.Name == "T1"); var pt1 = w.Obraz.Odcinki.First(x => x.Ut == t1.Id).P;
        w.View.Actual(); w.View.CenterOn(pt1[0], pt1[1]);
        w.View.SimDrag(S(pt1[0], pt1[1]), S(pt1[0] + 17, pt1[1] - 9));
        var pt1b = w.Obraz.Odcinki.First(x => x.Ut == t1.Id).P;
        Ok(pt1b[0] == pt1[0] + 16 && pt1b[1] == pt1[1] - 8, $"opis T1 przeciągnięty (do siatki): ({pt1b[0]},{pt1b[1]})");
        w.DoUndo(); w.View.Fit();
        w.ToolKey(Key.O); Leaves(w).First(l => l.Tag is ZobUtObj { Grupa: ZobUt.GOdcinki } x && x.Id == t1.Id).IsSelected = true;
        w.View.SimClick(S(pt1[0], pt1[1] - 120));
        Ok(w.LastMessage.Contains("kliknij przy torze") && w.Obraz.Odcinki.First(x => x.Ut == t1.Id).P.SequenceEqual(pt1), "opis T1 daleko od toru – odmowa");

        // --- numery torów
        w.ToolKey(Key.N);
        foreach (var (nr, km) in new[] { ("1", 1500.0), ("2", 577.0) }) { Leaf(w, "numer toru " + nr).IsSelected = true; var p = At(nr, km); w.View.SimClick(S(p[0], p[1])); }
        Ok(w.Obraz.NumeryTorow.Count == 2 && Sp("numer toru").Count == 0, "numery torów 1 i 2 w liniach torów");

        // --- perony: przeciągnięcie prostokąta, po zewnętrznej stronie torów
        w.SetTool(ZobTool.Peron);
        Ok(w.PeronCurrent != null && w.LastMessage.StartsWith("Peron:"), $"narzędzie Peron ({w.LastMessage})");
        int[] a1 = At("1", 1400), b1 = At("1", 1600), a2 = At("2", 477), b2 = At("2", 677);
        Leaf(w, "peron Peron 1").IsSelected = true;
        w.View.SimPeron(S(a1[0], 464), S(b1[0], 488));
        Leaf(w, "peron Peron 2").IsSelected = true;
        w.View.SimPeron(S(a2[0], 632), S(b2[0], 656));
        Ok(w.Obraz.Perony.Count == 2 && w.Obraz.Perony.All(p => p.Prost[3] == 24), $"2 perony narysowane ({string.Join("; ", w.Obraz.Perony.Select(p => string.Join(",", p.Prost)))})");
        w.View.SimPeron(S(100, 100), S(104, 102));
        Ok(w.LastMessage.StartsWith("Peron: przeciągnij prostokąt co najmniej"), "za mały prostokąt – odmowa");

        // --- przejazd kat. C na torze 1
        w.SetTool(ZobTool.Przejazd);
        var pp = At("1", 550); w.View.SimClick(S(pp[0], 520));
        var prz = w.Obraz.Przejazdy.SingleOrDefault();
        Ok(prz != null && prz.P[1] == 520 && Sp("przejazd").Count == 0, $"przejazd na torze 1 w km 550 ({prz?.P[0]},{prz?.P[1]})");
        w.ToolKey(Key.V); w.View.SimClick(S(prz.P[0], 520));
        Ok(w.View.SelectedSym == (ZobRodzaj.Przejazd, prz.Ut) && w.PrzejazdBoxes != null, "przejazd zaznaczony – panel z zasięgiem linii");
        Type(w.PrzejazdBoxes.Value.Down, "120");
        Ok(w.Obraz.Przejazdy[0].Zasieg?[1] == 120, "zasięg w dół 120 j. (przez tor 2)");

        // --- strzałki blokady: za semaforem wjazdowym od strony szlaku
        w.ToolKey(Key.B);
        Ok(w.View.Tool == ZobTool.Szlak && w.SymCurrent != null, $"B – narzędzie Szlak ({w.LastMessage})");
        var semA = ut.Devices.First(x => x.Name == "A"); var symA = w.Obraz.Sygnalizatory.First(x => x.Ut == semA.Id);
        var semB = ut.Devices.First(x => x.Name == "B"); var symB = w.Obraz.Sygnalizatory.First(x => x.Ut == semB.Id);
        var l1Z = ut.Lines.First(x => x.Numer == "1Z"); var l1W = ut.Lines.First(x => x.Numer == "1W");
        Leaf(w, "szlak 1Z").IsSelected = true;
        w.View.SimClick(S(symA.P[0] + 40 * semA.Dir, 520));
        Ok(w.Obraz.Szlaki.Count == 0 && w.LastMessage.Contains("od strony szlaku"), "strzałka 1Z po stronie stacji – odmowa");
        w.View.SimClick(S(symA.P[0] - 40 * semA.Dir, 520));
        Leaf(w, "szlak 1W").IsSelected = true;
        w.View.SimClick(S(symB.P[0] - 40 * semB.Dir, 520));
        var s1z = w.Obraz.Szlaki.FirstOrDefault(x => x.Ut == l1Z.Id); var s1w = w.Obraz.Szlaki.FirstOrDefault(x => x.Ut == l1W.Id);
        Ok(s1z != null && s1w != null && s1z.Koniec == l1Z.Konce.FindIndex(e => e.Semafor == semA.Id) && s1w.Koniec == l1W.Konce.FindIndex(e => e.Semafor == semB.Id),
            "strzałki 1Z i 1W przy końcach z semaforami A i B");
        Ok(ZobSymbole.NaZewnatrz(ut, w.Obraz, s1z) == (-semA.Dir * 1, 0) || ZobSymbole.NaZewnatrz(ut, w.Obraz, s1z).Dx == -Math.Sign(semA.Dir), "strzałka 1Z skierowana na zewnątrz – w stronę szlaku");
        Ok(w.View.OpisAt(ZobRodzaj.Szlak, s1z) is var os && os[1] == 506, "oznaczenie szlaku nad strzałką");

        // --- napisy
        w.ToolKey(Key.X);
        w.NextNapis = ("Zachodnia", ZobNapis.Sasiad); w.View.SimClick(S(96, 480));
        w.NextNapis = ("Wschodnia", ZobNapis.Sasiad); w.View.SimClick(S(1824, 480));
        w.NextNapis = ("MT", ZobNapis.Nastawnia); w.View.SimClick(S(960, 760));
        Ok(w.Obraz.Napisy.Count == 3 && w.Obraz.Napisy[2].Rodzaj == ZobNapis.Nastawnia && w.NapisBox?.Text == "MT", "3 napisy: posterunki sąsiednie i nastawnia „MT”");
        Type(w.NapisBox, "MTe");
        Ok(w.Obraz.Napisy[2].Tekst == "MTe", "tekst napisu z panelu");
        w.DoUndo();
        w.ToolKey(Key.V);
        w.View.SimDrag(S(96, 480), S(104, 489));
        Ok(w.Obraz.Napisy[0].P[0] == 104 && w.Obraz.Napisy[0].P[1] == 488, $"napis przeciągnięty ({w.Obraz.Napisy[0].P[0]},{w.Obraz.Napisy[0].P[1]})");
        w.ToolKey(Key.Delete);
        Ok(w.Obraz.Napisy.Count == 2, "Del – napis usunięty");
        w.DoUndo(); w.DoUndo();
        Ok(w.Obraz.Napisy.Count == 3 && w.Obraz.Napisy[0].P[0] == 96, "Ctrl+Z ×2 – napis wrócił na miejsce");

        // --- peron: przesunięcie myszą, zaznaczenie z panelu UT
        var per1 = w.Obraz.Perony.First(); var r0 = per1.Prost.ToArray();
        w.View.SimDrag(S(r0[0] + 16, r0[1] + 8), S(r0[0] + 24, r0[1]));
        Ok(w.Obraz.Perony.First().Prost[0] == r0[0] + 8 && w.Obraz.Perony.First().Prost[1] == r0[1] - 8, "peron przesunięty myszą o krok siatki");
        w.DoUndo();

        // --- tor bez kontroli niezajętości, opisy odcinków na pulpicie
        w.View.SimClick(S(At("2", 900)[0], 616));
        Ok(w.SelectedTor != null && w.BezKontroliBox != null, "tor 2 zaznaczony – pole „bez kontroli niezajętości”");
        w.BezKontroliBox.IsChecked = true; Click(w.BezKontroliBox);
        Ok(w.SelectedTor.BezKontroli, "tor 2 bez kontroli niezajętości (linia przerywana)");
        w.DoUndo();
        Ok(!w.Obraz.Tory.First(t => t.Ut.Tor == ut.Tracks.First(x => x.Number == "2").Id).BezKontroli, "Ctrl+Z – tor 2 z kontrolą");
        w.View.Select(null);
        Ok(w.OpisyBox != null && w.Obraz.OpisyOdcinkow == ZobObraz.OpisNaZadanie, "opisy odcinków na pulpicie: domyślnie na żądanie");
        w.OpisyBox.SelectedIndex = 1;
        Ok(w.Obraz.OpisyOdcinkow == ZobObraz.OpisZawsze, "opisy odcinków na pulpicie: zawsze");

        // --- „Sprawdź” – komplet
        var u1 = w.Sprawdz();
        Ok(u1.Count == 0 && w.LastMessage == "Sprawdź: bez uwag", $"Sprawdź: obraz mijanki kompletny, bez uwag ({string.Join("; ", u1.Select(x => x.Text))})");
        Ok(ZobUt.Obiekty(ut, w.Obraz).All(x => x.NaObrazie), "panel UT: wszystkie obiekty na obrazie");

        // --- zapis i odczyt
        Ok(w.SaveDoc(false), "zapis z elementami Z4");
        var lz = ZobDocument.Load(w.Doc.Path).P.Obrazy[0];
        Ok(lz.Przerwy.Count == 8 && lz.Odcinki.Count == 8 && lz.NumeryTorow.Count == 2 && lz.Perony.Count == 2 && lz.Przejazdy.Count == 1 && lz.Szlaki.Count == 2 && lz.Napisy.Count == 3
            && lz.OpisyOdcinkow == ZobObraz.OpisZawsze && ZobDocument.Serialize(ZobDocument.Load(w.Doc.Path).P) == ZobDocument.Serialize(w.Doc.P), "odczyt: wszystkie elementy Z4, identycznie");

        // --- przeniesienie na pulpit (Do symulatora): display z elementami Z4
        w.Obraz.Tory.First(t => t.Ut.Tor == ut.Tracks.First(x => x.Number == "2").Id).BezKontroli = true;     // na chwilę, bez cofania – tylko do eksportu
        // jak symulator: z tekstu pliku (wartości z pamięci mają typ int, plik – liczby)
        var ds = System.Text.Json.Nodes.JsonNode.Parse(DoSymulatora.Buduj(ut, w.Doc.P).Json.ToJsonString())["display"].AsObject();
        w.Obraz.Tory.First(t => t.Ut.Tor == ut.Tracks.First(x => x.Number == "2").Id).BezKontroli = false;
        int Cnt(string k) => ds[k]?.AsArray().Count ?? -1;
        Ok(Cnt("labels") == 3 && Cnt("platforms") == 2 && Cnt("crossings") == 1 && Cnt("trackLabels") == 2 && Cnt("secLabels") == 8 && Cnt("blockMarks") == 2,
            $"Do symulatora: napisy 3, perony 2, przejazd 1, numery torów 2, opisy odcinków 8, strzałki 2 ({string.Join(", ", new[] { "labels", "platforms", "crossings", "trackLabels", "secLabels", "blockMarks" }.Select(k => $"{k} {Cnt(k)}"))})");
        var bm = ds["blockMarks"].AsArray().Select(x => ((string)x["block"], (int)x["end"])).OrderBy(x => x.Item1).ToList();
        Ok(bm.SequenceEqual(new[] { ("1W", 0), ("1Z", 0) }) && (string)ds["secLabelsMode"] == ZobObraz.OpisZawsze, $"strzałki 1W / 1Z przy końcach z semaforem (ends[0]), opisy odcinków „zawsze” ({string.Join(" ", bm)})");
        Ok(Cnt("dashedEdges") > 0 && ds["secLabels"].AsArray().Any(x => (string)x["sec"] == "MT:T1"), $"tor 2 bez kontroli – kawałki przerywane ({Cnt("dashedEdges")}); opis odcinka MT:T1");
        Ok(ds["labels"].AsArray().Any(x => (string)x["anchor"] == "nastawnia" && (string)x["text"] == "MT") && ds["crossings"][0]["y1"].GetValue<double>() - ds["crossings"][0]["y0"].GetValue<double>() == 28 + 120,
            "nastawnia „MT” jako napis nastawni; przejazd – zasięg 28 + 120 j.");
        File.WriteAllText(Path.Combine(dir, "mijanka_testowa_z4.json"), DoSymulatora.Buduj(ut, w.Doc.P).Json.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));

        if (shot != null)
        {
            string Shot(string suf) => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(shot))!, Path.GetFileNameWithoutExtension(shot) + suf);
            w.View.Select(null); w.Sprawdz();
            w.View.Fit(); await Settle(w); Save(w, Shot("_z4.png"));
            w.View.Actual(); w.View.ZoomAt(new Point(w.View.ActualWidth / 2, w.View.ActualHeight / 2), 2); w.View.CenterOn(symA.P[0] + 40, 560); await Settle(w); Save(w, Shot("_z4_2x.png"));
            w.View.Fit();
        }
    }
}

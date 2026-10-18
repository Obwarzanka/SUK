using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Lcs.Model;

namespace Lcs.EdytorZobrazowania;

/// <summary>
/// Test etapu Z5 (PLAN_EDYTOR_ZOBRAZOWANIA.md § 5, decyzje § 10 pkt 4–5) jak praca użytkownika: mijanka po teście Z4 z drugim posterunkiem
/// MW (tor 2 i łączniki); obraz 1 – posterunek MT (tor 2 i łączniki zostają na nim jako obiekty innego posterunku – wspólne), obraz 2 – MW
/// rysowany klikami, obraz poglądowy (Ie-104 § 6) z kontrolą dozwolonych symboli i odstępu torów; „Sprawdź” – bez uwag na wszystkich;
/// „Do symulatora”: dwie strony (bez poglądowego), geometria każdej strony z torem 2 na obu; PNG obrazu bez znaczników edytora.
/// </summary>
public static partial class Test
{
    static async Task RunZ5(MainWindow w, string shot)
    {
        var z4 = Path.Combine(Path.GetTempPath(), "edytor_zob_test", "z4");
        var dir = Path.Combine(Path.GetTempPath(), "edytor_zob_test", "z5"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        File.Copy(Path.Combine(z4, "mijanka_testowa.zob.json"), Path.Combine(dir, "mijanka_testowa.zob.json"));
        // drugi posterunek w UT (jak w edytorze UT: nowy posterunek, tory 2 i łączniki do niego)
        var utFile = Path.Combine(dir, "mijanka_testowa.ut.json");
        var utDoc = UtDocument.Load(Path.Combine(z4, "mijanka_testowa.ut.json"));
        var mw = new UtPost { Kod = "MW", Nazwa = "Mijanka Wschodnia" };
        utDoc.P.Posts.Add(mw);
        foreach (var t in utDoc.P.Tracks.Where(t => t.Number != "1")) t.Post = mw.Id;
        utDoc.Save(utFile);
        Ok(w.OpenPath(Path.Combine(dir, "mijanka_testowa.zob.json")), "mijanka po Z4, UT z drugim posterunkiem MW (tor 2, łączniki)");
        await Settle(w); w.View.Fit();
        var ut = w.Doc.Ut; var o1 = w.Obraz;
        Point S(double x, double y) => w.View.ToScreen(x, y);
        Ok(o1.Posterunki.Count == 1 && w.Sprawdz().Count == 0, $"obraz 1 (MT): Sprawdź bez uwag – tor 2 i łączniki to teraz obiekty innego posterunku ({w.LastMessage})");
        Ok(!ZobUt.Obiekty(ut, o1).Any(x => x.Opis.StartsWith("tor 2")) && ZobUt.Obiekty(ut, o1, true).Any(x => x.Opis.StartsWith("tor 2") && x.Obcy && x.NaObrazie), "tor 2 – inny posterunek, na obrazie 1 (wspólny)");
        w.InneBox.IsChecked = true; Click(w.InneBox);
        Ok(Leaf(w, "tor 2") is TreeViewItem l2 && ((string)l2.Header).Contains("(inny posterunek)"), "panel UT: obiekty innych posterunków na szaro");
        w.InneBox.IsChecked = false; Click(w.InneBox);

        // --- obraz 2: posterunek MW, rysowany klikami (położenia jak na obrazie 1)
        Click(Btn(w.Tabs, "+ obraz szczegółowy"));
        var o2 = w.Obraz;
        var cbMw = All<CheckBox>(w.PostsPanel).First(c => ((string)c.Content).StartsWith("MW")); cbMw.IsChecked = true; Click(cbMw);
        Type(w.NameBox, "Mijanka – tor 2");
        Ok(w.Obraz.Posterunki.SequenceEqual(new[] { mw.Id }) && w.UtHeader.Text == "Obiekty UT – MW", "obraz 2 z posterunkiem MW");
        w.View.Actual(); w.View.ZoomAt(new Point(w.View.ActualWidth / 2, w.View.ActualHeight / 2), 2);
        var tory1 = o1.Tory.Where(t => ut.Tracks.First(x => x.Id == t.Ut.Tor).Post == mw.Id).OrderBy(t => t.Pkt.Count).ToList();   // tor 2 (2 punkty), potem łączniki
        foreach (var t in tory1)
        {
            w.SetTool(ZobTool.Tor);
            Leaves(w).First(l => ((ZobUtObj)l.Tag).Grupa == ZobUt.GTory && ((ZobUtObj)l.Tag).Id == t.Ut.Tor).IsSelected = true;
            // łącznik od strony toru 2 (ostatni punkt łamanej leży na torze 2) – w drugą stronę niż na obrazie 1
            var pts = t.Pkt.Count > 2 && t.Pkt[^1][1] == 616 ? Enumerable.Reverse(t.Pkt).ToList() : t.Pkt;
            w.View.CenterOn(pts[0][0], pts[0][1]);
            w.View.SimClick(S(pts[0][0], pts[0][1]));
            foreach (var q in pts.Skip(1)) { w.View.CenterOn(q[0], q[1]); w.View.SimClick(S(q[0], q[1]), MouseButton.Left, ModifierKeys.Control); }
            if (w.View.Draft.Count > 0) w.ToolKey(Key.Enter);
        }
        Ok(o2.Tory.Count == 3 && o2.Tory.All(t => t.Pkt.Count >= 2), $"obraz 2: tor 2 i dwa łączniki ({string.Join("; ", o2.Tory.Select(Pts))})");
        // symbole obiektów MW – w tych samych miejscach co na obrazie 1
        foreach (var x in ZobUt.Obiekty(ut, o2).Where(x => x.Grupa != ZobUt.GTory))
        {
            if (x.Grupa == ZobUt.GPerony)
            {
                var pr = o1.Perony.First(p => p.Ut == x.Id).Prost;
                w.SetTool(ZobTool.Peron); Leaves(w).First(l => ((ZobUtObj)l.Tag).Id == x.Id).IsSelected = true;
                w.View.CenterOn(pr[0], pr[1]); w.View.SimPeron(S(pr[0], pr[1]), S(pr[0] + pr[2], pr[1] + pr[3]));
                continue;
            }
            var r = ZobSymbole.RodzajUt(ut, x.Id).Value;
            var s1 = ZobSymbole.Lista(o1, r).First(s => s.Ut == x.Id);
            w.SetTool(r switch
            {
                ZobRodzaj.Zwrotnica => ZobTool.Zwrotnica, ZobRodzaj.Sygnalizator => ZobTool.Sygnalizator, ZobRodzaj.Kozioł => ZobTool.Koziol, ZobRodzaj.Przerwa => ZobTool.Przerwa,
                ZobRodzaj.NumerToru => ZobTool.NumerToru, ZobRodzaj.OpisOdcinka => ZobTool.OpisOdcinka, ZobRodzaj.Przejazd => ZobTool.Przejazd, _ => ZobTool.Szlak,
            });
            Leaves(w).First(l => ((ZobUtObj)l.Tag).Id == x.Id && ((ZobUtObj)l.Tag).Grupa == x.Grupa).IsSelected = true;
            w.View.CenterOn(s1.P[0], s1.P[1]);
            w.View.SimClick(S(s1.P[0], s1.P[1]));
        }
        w.SetTool(ZobTool.Wskaz); w.View.Fit();
        var u2 = w.Sprawdz();
        Ok(u2.Count == 0, $"obraz 2 (MW): Sprawdź bez uwag ({string.Join("; ", u2.Select(x => x.Text))})");

        // --- obraz poglądowy: tory 40 j. od siebie, bez opisów odcinków i peronów (Ie-104 § 6 ust. 4)
        Click(Btn(w.Tabs, "+ obraz poglądowy"));
        var op = w.Obraz;
        Ok(op.Rodzaj == ZobObraz.Pogladowy && op.Posterunki.Count == 2, "obraz poglądowy: oba posterunki");
        int X(int x) => 100 + (int)Math.Round((x - 64) * 1700.0 / 1792 / 8) * 8;      // skala obrazu szczegółowego → poglądowy (na siatce)
        int Y(int y) => y == 520 ? 304 : 344;
        var t1 = o1.Tory.First(t => ut.Tracks.First(x => x.Id == t.Ut.Tor).Number == "1"); var t2 = o1.Tory.First(t => ut.Tracks.First(x => x.Id == t.Ut.Tor).Number == "2");
        w.View.Fit();
        foreach (var (nr, src) in new[] { ("tor 1", t1), ("tor 2", t2) })
        {
            w.SetTool(ZobTool.Tor); Leaf(w, nr).IsSelected = true;
            w.View.SimClick(S(X(src.Pkt[0][0]), Y(src.Pkt[0][1]))); w.View.SimClick(S(X(src.Pkt[^1][0]), Y(src.Pkt[^1][1])), MouseButton.Left, ModifierKeys.Shift); w.ToolKey(Key.Enter);
        }
        w.SetTool(ZobTool.Lacznik);
        foreach (var lz in o1.Tory.Where(t => t.Pkt.Count == 4))
        {
            var a = lz.Pkt[0][1] == 520 ? lz.Pkt[0] : lz.Pkt[^1];   // koniec na torze 1
            int dx = Math.Sign((lz.Pkt[0][1] == 520 ? lz.Pkt[^1] : lz.Pkt[0])[0] - a[0]);
            w.View.SimClick(S(X(a[0]), 304)); w.View.SimClick(S(X(a[0]) + dx * 16, 344));
        }
        Ok(op.Tory.Count == 4 && ZobUt.Obiekty(ut, op).Where(x => x.Grupa == ZobUt.GTory).All(x => x.NaObrazie), $"poglądowy: tory 1, 2 i łączniki ({w.LastMessage})");
        // zwrotnice (przyciągnięte do łączników), sygnalizatory pociągowe, strzałki, przejazd, numery torów
        foreach (var (r, tool) in new[] { (ZobRodzaj.Zwrotnica, ZobTool.Zwrotnica), (ZobRodzaj.Sygnalizator, ZobTool.Sygnalizator), (ZobRodzaj.Szlak, ZobTool.Szlak), (ZobRodzaj.Przejazd, ZobTool.Przejazd), (ZobRodzaj.NumerToru, ZobTool.NumerToru) })
            foreach (var s1 in ZobSymbole.Lista(o1, r).OrderBy(s => s.P[0]))
            {
                w.SetTool(tool);
                Leaves(w).First(l => ((ZobUtObj)l.Tag).Id == s1.Ut && ((ZobUtObj)l.Tag).Grupa != ZobUt.GTory).IsSelected = true;
                int x = X(s1.P[0]);
                if (r == ZobRodzaj.Szlak) x = X(ZobSymbole.Lista(o1, ZobRodzaj.Sygnalizator).First(q => q.Ut == ut.Lines.First(l => l.Id == s1.Ut).Konce[s1.Koniec ?? 0].Semafor).P[0]) + Math.Sign(s1.P[0] - ZobSymbole.Lista(o1, ZobRodzaj.Sygnalizator).First(q => q.Ut == ut.Lines.First(l => l.Id == s1.Ut).Konce[s1.Koniec ?? 0].Semafor).P[0]) * 40;
                w.View.SimClick(S(x, Y(s1.P[1])));
            }
        w.SetTool(ZobTool.Wskaz);
        var up = w.Sprawdz();
        Ok(up.Count == 0, $"poglądowy: Sprawdź bez uwag – bez kozłów, przerw, opisów i peronów ({string.Join("; ", up.Select(x => x.Text))})");
        // peron na poglądowym – uwaga; tor 2 za blisko toru 1 – uwaga
        w.SetTool(ZobTool.Peron); Leaf(w, "peron Peron 1").IsSelected = true; w.View.SimPeron(S(800, 256), S(928, 280));
        Ok(w.Sprawdz().Any(x => x.Text.StartsWith("obraz poglądowy: peron")), "peron na obrazie poglądowym – uwaga (Ie-104 § 6 ust. 4)");
        w.DoUndo();
        w.SetTool(ZobTool.Wskaz); w.View.SimClick(S(800, 344));
        for (int i = 0; i < 4; i++) w.ToolKey(Key.Up);
        Ok(w.Sprawdz().Any(x => x.Text.Contains("za blisko") && x.Text.Contains("co najmniej 10 j.")), "tor 2 8 j. od toru 1 – za blisko (poglądowy: 1 × wysokość opisu)");
        for (int i = 0; i < 4; i++) w.DoUndo();
        Ok(w.Sprawdz().Count == 0, "Ctrl+Z – odstęp 40 j., bez uwag");

        // --- Do symulatora: dwie strony, geometria każdej strony
        var dj = JsonNode.Parse(DoSymulatora.Buduj(ut, w.Doc.P).Json.ToJsonString());
        var pages = dj["pages"].AsArray(); var pg = dj["display"]["pageGeo"]?.AsObject();
        Ok(pages.Count == 2 && pg != null && pg.Count == 2, $"Do symulatora: 2 strony (bez poglądowego), geometria każdej strony ({pages.Count}, {pg?.Count})");
        var e2 = pg[o1.Id]["edges"].AsObject().Select(x => x.Key).Intersect(pg[o2.Id]["edges"].AsObject().Select(x => x.Key)).ToList();
        Ok(e2.Count > 0 && dj["display"]["edges"].AsObject().Count == pg[o1.Id]["edges"].AsObject().Select(x => x.Key).Union(pg[o2.Id]["edges"].AsObject().Select(x => x.Key)).Count(),
            $"tor 2 i łączniki na obu stronach ({e2.Count} kawałków), geometria główna – każdy kawałek raz");
        Ok(pg[o2.Id]["edges"].AsObject().All(x => x.Value[0][0].GetValue<double>() >= 1920), "strona 2 – x od 1920 (obrazy obok siebie)");
        Ok(DoSymulatora.Buduj(ut, w.Doc.P).Uwagi.Any(x => x.StartsWith("obraz poglądowy")), "uwaga: obraz poglądowy tylko w edytorze");
        File.WriteAllText(Path.Combine(dir, "mijanka_z5.json"), DoSymulatora.Buduj(ut, w.Doc.P).Json.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));

        // --- PNG obrazu (bez siatki i znaczników edytora)
        w.ShowImage(o1.Id);
        var png = Path.Combine(dir, "obraz1.png");
        Ok(w.SavePng(png), "PNG obrazu 1");
        var bmp = new PngBitmapDecoder(new Uri(png), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        var conv = new FormatConvertedBitmap(bmp, System.Windows.Media.PixelFormats.Bgra32, null, 0);
        byte[] Px(int x, int y) { var b = new byte[4]; conv.CopyPixels(new Int32Rect(x, y, 1, 1), b, 4, 0); return b; }
        var onTrack = Px(1000, 520); var empty = Px(400, 200); var node = Px(64, 520);
        Ok(bmp.PixelWidth == 1920 && bmp.PixelHeight == 1080 && onTrack[0] == 0x9a && onTrack[1] == 0x9a && empty[0] == 0 && empty[1] == 0 && empty[2] == 0,
            $"PNG 1920 × 1080: tor szary, tło czarne bez siatki ({onTrack[2]:x2}{onTrack[1]:x2}{onTrack[0]:x2}, {empty[2]:x2}{empty[1]:x2}{empty[0]:x2})");
        Ok(!(node[0] == 0x60 && node[1] == 0x60), "PNG bez znaczników węzłów edytora");

        Ok(w.SaveDoc(false), "zapis: trzy obrazy");
        Ok(o1.Nazwa == "Mijanka Testowa" && o2.Nazwa == "Mijanka – tor 2", $"nazwy obrazów bez zmian po przełączaniu zakładek przy fokusie w polu nazwy ({o1.Nazwa}; {o2.Nazwa})");
        if (shot != null)
        {
            string Shot(string suf) => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(shot))!, Path.GetFileNameWithoutExtension(shot) + suf);
            File.Copy(png, Shot("_z5_png.png"), true);
            w.ShowImage(op.Id); w.Sprawdz(); await Settle(w); Save(w, Shot("_z5_pogladowy.png"));
            w.ShowImage(o2.Id); await Settle(w); Save(w, Shot("_z5_obraz2.png"));
        }
    }
}

using System.IO;
using System.Windows;
using System.Windows.Controls;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Test montażu rozjazdów na łuku i „Napraw tory i łuki” (PLAN_EDYTOR_UT.md § 5i) jak użytkownik: rozjazd przeciągnięty na początek
/// łuku R 1500 (kąt 4°) – montuje się sam (łuk wydłużony, oś najwyżej kilka cm), semafor za łukiem zostaje w terenie, Ctrl+Z cofa samą
/// poprawkę; rozjazd przesunięty na koniec łuku – to samo; rozjazd w połowie długiej przejściowej – bez samej poprawki, warianty w panelu,
/// „Przesuń”; Rz 500 1:12 na łuku R 1200 – odgałęzienie bez błędów (zaokrąglony promień toru zwrotnego dawał „brakuje 0 m prostej”);
/// „Napraw” na torze ze zbędnym wierzchołkiem, łukami, które się nie mieszczą, i rozjazdem na granicy łuku – jedna zmiana, drugi raz bez zmian.
/// </summary>
public static partial class Test
{
    static async Task RunMontaz(MainWindow w)
    {
        const double lineAz = 20;
        var dir = Path.Combine(Path.GetTempPath(), "edytor_ut_test", "montaz"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        w.SetSchemat(false);
        w.NewDoc(); w.SaveDoc(false, Path.Combine(dir, "stacja.ut.json"));
        var d = w.Doc;
        var o = Geo.ToPuwg92(51.7631, 19.5438); double k = Geo.ScaleFactor(o);
        XY Go(XY from, double azDeg, double len) => from + Element.Dir(azDeg * Math.PI / 180) * (len * k);
        Point S(XY p) => w.View.ToScreen(p);
        UtTrack T0() => d.P.Tracks.First(x => x.Number == "1");
        (double S, double E) Arc(UtTrack t) { var r = w.View.Geometry(t); double acc = 0, cs = -1, ce = -1; foreach (var e in r.Elements) { if (e.Kind != ElKind.Prosta) { if (cs < 0) cs = acc / r.K; ce = (acc + e.Len) / r.K; } acc += e.Len; } return (cs, ce); }
        XY DevAt(UtDevice dv) => w.View.Geometry(d.P.Tracks.First(x => x.Id == dv.Track)).At(dv.S).P;

        // tor 1: prosta 500 m – łuk R 1500 (kąt 4°) – prosta 500 m, semafor A 100 m za łukiem
        var a = Go(o, lineAz + 180, 500); var c = Go(o, lineAz + 4, 500);
        var t1 = new UtTrack { Number = "1", Vertices = new() { new() { X = a.X, Y = a.Y }, new() { X = o.X, Y = o.Y, R = 1500 }, new() { X = c.X, Y = c.Y } } };
        d.Change("Tor", p => { p.Tracks.Clear(); p.Turnouts.Clear(); p.Devices.Clear(); p.Tracks.Add(t1); });
        var (cs, ce) = Arc(T0());
        d.Change("Semafor", p => p.Devices.Add(new UtDevice { Kind = UtDevice.Semafor, Name = "A", Track = t1.Id, S = ce + 100, Dir = 1 }));
        var semXY = DevAt(d.P.Devices[0]);

        // --- rozjazd przeciągnięciem: PR 10 m przed początkiem łuku, odgałęzienie w lewo (na zewnątrz)
        w.SetTool(UtTool.Rozjazd); w.RzTypeBox.SelectedItem = "Rz 60E1-500-1:12";
        var r1 = w.View.Geometry(T0()); var (pr, az) = r1.At(cs - 10); w.View.SetView(pr, 0.3);
        w.View.SimDrag(S(pr), S(pr + Element.Dir(az) * (30 * k) - Element.Right(az) * (1 * k)));
        var z = d.P.Turnouts.FirstOrDefault();
        var ra = w.View.Geometry(T0());
        Ok(z != null && Naprawa.Polozenie(d.P, z) == NaLuku.Ok && d.UndoName == "Montaż rozjazdu na łuku" && w.LastMessage.Contains("zamontowany")
           && XY.Dist(T0().Vertices[1].P, o) < 1e-9 && ra.Ok && !Rozjazdy.Check(d.P, z).Any(x => x.Contains("granicy") || x.Contains("przejściowej")),
            $"rozjazd na początku łuku montuje się sam: R {1500} → {T0().Vertices[1].R:0} m, uwaga znika ({w.LastMessage})");
        var semMove = XY.Dist(DevAt(d.P.Devices[0]), semXY) / k;
        Ok(semMove < 0.01, $"semafor A za łukiem zostaje w terenie (przesunięcie {semMove * 100:0.##} cm, km {ce + 100:0.000} → {d.P.Devices[0].S:0.000} – długość toru się zmieniła)");
        var prXY = Rozjazdy.Geometry(d.P, z).PR;
        Ok(XY.Dist(prXY, pr) / k < 0.3, $"PR rozjazdu w miejscu przeciągnięcia ({XY.Dist(prXY, pr) / k * 100:0.#} cm – oś przesunięta montażem)");
        w.DoUndo();
        z = d.P.Turnouts.FirstOrDefault();
        Ok(z != null && Naprawa.Polozenie(d.P, z) == NaLuku.Granica && T0().Vertices[1].R == 1500, "Ctrl+Z cofa samą poprawkę: rozjazd zostaje, na granicy prostej i łuku");
        w.DoRedo();

        // --- przesunięcie rozjazdu na koniec łuku – znów montaż
        z = d.P.Turnouts.First(); (cs, ce) = Arc(T0());
        w.SetTool(UtTool.Wskaz); var g = Rozjazdy.Geometry(d.P, z); r1 = w.View.Geometry(T0());
        w.View.SetView(g.PR, 0.3); var to = r1.At(ce - 40).P;
        w.View.SetView((g.PR + to) * 0.5, Math.Max(0.3, XY.Dist(g.PR, to) / 900));
        w.View.SimDrag(S(g.PR), S(to));
        z = d.P.Turnouts.First();
        Ok(Math.Abs(z.S - (ce - 40)) < 15 && Naprawa.Polozenie(d.P, z) == NaLuku.Ok && d.UndoName == "Montaż rozjazdu na łuku",
            $"rozjazd przesunięty na koniec łuku – montaż sam: km PR {z.S:0.0}, łuk {Arc(T0()).S:0.0}–{Arc(T0()).E:0.0} ({w.LastMessage})");
        Ok(XY.Dist(DevAt(d.P.Devices[0]), semXY) / k < 0.01, "semafor A dalej w tym samym miejscu");
        if (shotRails != null) { w.View.SetView(Rozjazdy.Geometry(d.P, z).SR, 0.15); await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_montaz.png")); }

        // --- długa przejściowa: rozjazd w jej połowie – bez samej poprawki, warianty w panelu, „Przesuń”
        var t2 = new UtTrack { Number = "1", Vertices = new() { new() { X = a.X, Y = a.Y }, new() { X = o.X, Y = o.Y, R = 600, L = 150 }, new() { X = Go(o, lineAz + 30, 600).X, Y = Go(o, lineAz + 30, 600).Y } } };
        d.Change("Tor", p => { p.Tracks.Clear(); p.Turnouts.Clear(); p.Devices.Clear(); p.Tracks.Add(t2); });
        (cs, ce) = Arc(T0());
        w.SetTool(UtTool.Rozjazd); w.RzTypeBox.SelectedItem = "Rz 60E1-190-1:9";
        r1 = w.View.Geometry(T0()); (pr, az) = r1.At(cs + 60); w.View.SetView(pr, 0.3);
        w.View.SimDrag(S(pr), S(pr + Element.Dir(az) * (25 * k) - Element.Right(az) * (1 * k)));
        z = d.P.Turnouts.FirstOrDefault();
        Ok(z != null && Naprawa.Polozenie(d.P, z) == NaLuku.Przejsciowa && d.UndoName == "Rozjazd" && w.LastMessage.Contains("warianty montażu"),
            $"rozjazd w połowie przejściowej L 150: bez samej poprawki ({w.LastMessage})");
        w.SetTool(UtTool.Wskaz); w.View.SelectTurnout(z.Id);
        var btns = w.TrackPanel.Children.OfType<Button>().Select(b => b.Content as string).Where(x => x != null && (x.StartsWith("Popraw łuk:") || x.StartsWith("Przesuń:"))).ToList();
        Ok(btns.Any(x => x.StartsWith("Przesuń:")), $"panel rozjazdu – warianty montażu: {string.Join(" | ", btns)}");
        var mv = w.TrackPanel.Children.OfType<Button>().FirstOrDefault(b => (b.Content as string)?.StartsWith("Przesuń:") == true);
        if (mv != null)
        {
            Click(mv); z = d.P.Turnouts.First();
            Ok(Naprawa.Polozenie(d.P, z) == NaLuku.Ok && d.UndoName == "Przesunięcie rozjazdu" && w.View.Geometry(d.P.Tracks.First(x => x.Id == z.Branch)).Ok,
                $"„{mv.Content}” – rozjazd poza przejściową, odgałęzienie bez błędów");
        }

        // --- Rz 500 1:12 w łuku R 1200: rozjazd łukowy, odgałęzienie bez błędu „łuk nie mieści się”
        var t3 = new UtTrack { Number = "1", Vertices = new() { new() { X = a.X, Y = a.Y }, new() { X = o.X, Y = o.Y, R = 1200 }, new() { X = Go(o, lineAz + 20, 600).X, Y = Go(o, lineAz + 20, 600).Y } } };
        d.Change("Tor", p => { p.Tracks.Clear(); p.Turnouts.Clear(); p.Tracks.Add(t3); });
        (cs, ce) = Arc(T0());
        w.SetTool(UtTool.Rozjazd); w.RzTypeBox.SelectedItem = "Rz 60E1-500-1:12";
        int bad = 0; string kinds = "";
        foreach (var (side, sm) in new[] { (1, 0.3), (-1, 0.55) })
        {
            r1 = w.View.Geometry(T0()); (pr, az) = r1.At(cs + (ce - cs) * sm); w.View.SetView(pr, 0.3);
            w.View.SimDrag(S(pr), S(pr + Element.Dir(az) * (30 * k) + Element.Right(az) * (side * 1 * k)));
            var zz = d.P.Turnouts.Last(); var gz = Rozjazdy.Geometry(d.P, zz); kinds += $" {gz.Kind} R {gz.RDiv:0.#}";
            bad += w.View.Geometry(d.P.Tracks.First(x => x.Id == zz.Branch)).Notes.Count(n => n.Error);
        }
        Ok(d.P.Turnouts.Count == 2 && bad == 0, $"Rz 500 1:12 w łuku R 1200 – rozjazdy łukowe{kinds}, odgałęzienia bez błędów ({bad})");

        // --- para rozjazdów (łącznik) na torach równoległych w łuku R 1500 / 4°: przeciągnięcie z toru 1 na tor 2 – drugi rozjazd (na
        //     torze 2, ustawiany na przecięciu skosu) trafia na granicę łuku; montaż liczy go na nowym przecięciu, para razem
        foreach (var (off, side, auto) in new[] { (30.0, -1, true), (30.0, 1, false) })
        {
            var ta = new UtTrack { Number = "1", Vertices = new() { new() { X = a.X, Y = a.Y }, new() { X = o.X, Y = o.Y, R = 1500 }, new() { X = c.X, Y = c.Y } } };
            var tb = new UtTrack { Number = "2", Vertices = Trasa.Offset(ta, side * 4.75) };
            d.Change("Tory", p => { p.Tracks.Clear(); p.Turnouts.Clear(); p.Devices.Clear(); p.Tracks.Add(ta); p.Tracks.Add(tb); });
            (cs, ce) = Arc(T0());
            w.SetTool(UtTool.Rozjazd); w.RzTypeBox.SelectedItem = "Rz 60E1-500-1:12";
            var rA = w.View.Geometry(T0()); var rB = w.View.Geometry(d.P.Tracks.First(x => x.Number == "2"));
            var p1 = rA.At(cs + off).P; var p2 = rB.At(rB.Nearest(rA.At(cs + off + 45).P).S).P;
            w.View.SetView((p1 + p2) * 0.5, 0.3); w.View.SimDrag(S(p1), S(p2));
            var zz1 = d.P.Turnouts.FirstOrDefault(x => !x.BranchAtEnd); var zz2 = d.P.Turnouts.FirstOrDefault(x => x.BranchAtEnd);
            string tag = $"łącznik {(side < 0 ? "po lewej" : "po prawej")}";
            if (zz1 == null || zz2 == null) { Ok(false, $"{tag}: łącznik nie powstał ({w.LastMessage})"); continue; }
            if (auto)
                Ok(Naprawa.Polozenie(d.P, zz1) == NaLuku.Ok && Naprawa.Polozenie(d.P, zz2) == NaLuku.Ok && d.UndoName == "Montaż rozjazdu na łuku"
                   && w.LastMessage.Contains($"rozjazd {zz2.Number} zamontowany") && w.View.Geometry(d.P.Tracks.First(x => x.Id == zz1.Branch)).Ok,
                    $"{tag}: drugi rozjazd na granicy łuku toru 2 – montaż sam, oba na stałym promieniu ({w.LastMessage})");
            else
            {
                Ok(Naprawa.Polozenie(d.P, zz2) != NaLuku.Ok && d.UndoName == "Łącznik" && w.LastMessage.Contains("warianty montażu"),
                    $"{tag}: drugi rozjazd – poprawka za duża, bez samej zmiany ({w.LastMessage})");
                w.SetTool(UtTool.Wskaz); w.View.SelectTurnout(zz2.Id);
                var pb = w.TrackPanel.Children.OfType<Button>().FirstOrDefault(b => (b.Content as string)?.StartsWith("Przesuń: przesuń łącznik") == true);
                Ok(pb != null, $"{tag}: panel drugiego rozjazdu – „{pb?.Content}”");
                if (pb != null)
                {
                    Click(pb); zz1 = d.P.Turnouts.First(x => !x.BranchAtEnd); zz2 = d.P.Turnouts.First(x => x.BranchAtEnd);
                    Ok(Naprawa.Polozenie(d.P, zz1) == NaLuku.Ok && Naprawa.Polozenie(d.P, zz2) == NaLuku.Ok && d.UndoName == "Przesunięcie rozjazdu"
                       && w.View.Geometry(d.P.Tracks.First(x => x.Id == zz1.Branch)).Ok, $"{tag}: łącznik przesunięty – oba rozjazdy poza granicą łuku");
                }
            }
        }

        // --- „Napraw tory i łuki”: zbędny wierzchołek na prostej, łuki R 2000, które się nie mieszczą, rozjazd na granicy łuku (wstawiony bez montażu)
        var m1 = Go(a, lineAz, 250); var m2 = Go(o, lineAz + 4, 60); var m3 = Go(m2, lineAz + 10, 440);
        var t4 = new UtTrack { Number = "1", Vertices = new() { new() { X = a.X, Y = a.Y }, new() { X = m1.X, Y = m1.Y }, new() { X = o.X, Y = o.Y, R = 1500 }, new() { X = m2.X, Y = m2.Y, R = 2000 }, new() { X = m3.X, Y = m3.Y } } };
        d.Change("Tor", p => { p.Tracks.Clear(); p.Turnouts.Clear(); p.Devices.Clear(); p.Tracks.Add(t4); });
        var r4 = w.View.Geometry(T0());
        double acc = 0, c4 = 0; foreach (var e in r4.Elements) { if (e.Kind != ElKind.Prosta) { c4 = acc / r4.K; break; } acc += e.Len; }
        var (zn, brn) = Rozjazdy.NewSingle(d.P, t4.Id, c4 - 8, 1, -1, "Rz 60E1-500-1:12", "7");
        d.Change("Rozjazd i semafor", p => { p.Tracks.Add(brn); p.Turnouts.Add(zn); p.Devices.Add(new UtDevice { Kind = UtDevice.Semafor, Name = "B", Track = t4.Id, S = r4.Length - 50, Dir = 1 }); });
        var semB = DevAt(d.P.Devices[0]); int nv0 = T0().Vertices.Count;
        bool errs0 = !w.View.Geometry(T0()).Ok; var pol0 = Naprawa.Polozenie(d.P, d.P.Turnouts[0]);
        var fix = Find<Button>((DependencyObject)w.Content, b => b.Content as string == "🔧 Napraw");
        Ok(fix != null && errs0 && pol0 == NaLuku.Granica, $"przed naprawą: łuki się nie mieszczą, rozjazd 7 na granicy łuku, przycisk „🔧 Napraw” na pasku");
        if (fix != null) Click(fix);
        var res = w.LastRepair; var t4n = T0();
        Ok(res != null && t4n.Vertices.Count == nv0 - 1 && w.View.Geometry(t4n).Ok && Naprawa.Polozenie(d.P, d.P.Turnouts[0]) == NaLuku.Ok
           && d.UndoName == "Napraw tory i łuki" && w.RepairExpander.IsVisible && w.RepairList.Items.Count >= 3,
            $"„Napraw”: {string.Join(" | ", res?.Zmiany ?? new())}" + (res?.Zostalo.Count > 0 ? " / do decyzji: " + string.Join(" | ", res.Zostalo) : ""));
        Ok(XY.Dist(DevAt(d.P.Devices[0]), semB) / k < 0.01, $"semafor B zostaje w terenie ({XY.Dist(DevAt(d.P.Devices[0]), semB) / k * 100:0.##} cm)");
        var again = w.RepairAll();
        Ok(again.Zmiany.Count == 0, $"„Napraw” drugi raz – bez zmian ({string.Join(" | ", again.Zmiany)})");
        w.View.Select(t4n.Id);
        Ok(BtnIn(w.TrackPanel, "Napraw tor i łuki") != null, "panel toru – przycisk „Napraw tor i łuki”");
        w.DoUndo();
        Ok(T0().Vertices.Count == nv0 && !w.View.Geometry(T0()).Ok && Naprawa.Polozenie(d.P, d.P.Turnouts[0]) == NaLuku.Granica, "Ctrl+Z – cała naprawa cofnięta jednym krokiem");
        w.SetTool(UtTool.Wskaz);
        await Task.CompletedTask;
    }
}

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Test łuku o zmiennym promieniu (łuk koszowy), jak użytkownik: „na zdjęciu” łuk R 600 (20°) i zaraz za nim R 900 (20°), narysowany
/// jednym łukiem R 700; PPM „Podziel łuk tutaj” (kształt bez zmian), „Łuk z punktów” (J) na podzielonym łuku – oba promienie i wierzchołki naraz.
/// </summary>
public static partial class Test
{
    static async Task RunLukKoszowy(MainWindow w)
    {
        const double lineAz = 30;
        var dir = Path.Combine(Path.GetTempPath(), "edytor_ut_test", "lukkoszowy"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        w.SetSchemat(false);
        w.NewDoc(); w.SaveDoc(false, Path.Combine(dir, "stacja.ut.json"));
        var d = w.Doc;
        var o = Geo.ToPuwg92(51.7631, 19.5438); double k = Geo.ScaleFactor(o);
        Point S(XY p) => w.View.ToScreen(p);
        XY D(double azDeg) => Element.Dir(azDeg * Math.PI / 180);

        // „zdjęcie”: prosta 250 m, łuk R 600 o 20°, łuk R 900 o 20° (w prawo), prosta 250 m
        double R1 = 600, R2 = 900, t1 = 600 * Math.Tan(10 * Math.PI / 180), t2 = 900 * Math.Tan(10 * Math.PI / 180);
        var A = o; var ts1 = A + D(lineAz) * (250 * k);
        var v1 = ts1 + D(lineAz) * (t1 * k); var j = v1 + D(lineAz + 20) * (t1 * k);
        var v2 = j + D(lineAz + 20) * (t2 * k); var st2 = v2 + D(lineAz + 40) * (t2 * k); var E = st2 + D(lineAz + 40) * (250 * k);
        var truth = Trasa.Compute(new List<UtVertex> { new() { X = A.X, Y = A.Y }, new() { X = v1.X, Y = v1.Y, R = R1 }, new() { X = v2.X, Y = v2.Y, R = R2 }, new() { X = E.X, Y = E.Y } }, k);
        Ok(truth.Ok, "wzorzec: łuk R 600 + R 900 bez prostej między nimi");
        // narysowany zgrubnie: jeden wierzchołek w przecięciu prostych, R 700
        var d0 = D(lineAz); var d2 = D(lineAz + 40); var wv = E - A; double den = d0.X * d2.Y - d0.Y * d2.X, tt = (wv.X * d2.Y - wv.Y * d2.X) / den;
        var vd = A + d0 * tt;
        var tr = new UtTrack { Number = "3", Vertices = new() { new() { X = A.X, Y = A.Y }, new() { X = vd.X, Y = vd.Y, R = 700 }, new() { X = E.X, Y = E.Y } } };
        d.Change("Tor", p => p.Tracks.Add(tr));
        var r0 = w.View.Geometry(tr); var arc = Ciecie.Kawalki(r0).First(x => x.Luk);
        double sMid = (arc.S0 + arc.S1) / 2;

        // PPM „Podziel łuk W2 tutaj”
        w.SetTool(UtTool.Wskaz);
        var cm = w.BuildTrackMenu(tr.Id, sMid);
        var mi = cm.Items.OfType<MenuItem>().FirstOrDefault(x => (x.Header as string)?.StartsWith("Podziel łuk W2 tutaj") == true);
        Ok(mi != null, $"menu PPM na łuku: „{mi?.Header}”");
        mi?.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        tr = d.P.Tracks.First(x => x.Id == tr.Id); var r1 = w.View.Geometry(tr);
        double shape = r0.Sample(2).Max(q => r1.Nearest(q.P, 0.25).Dist) / k;
        Ok(tr.Vertices.Count == 4 && tr.Vertices[1].R == 700 && tr.Vertices[2].R == 700 && r1.Ok && shape < 0.002 && d.UndoName == "Podział łuku",
            $"łuk podzielony: W2 i W3, oba R 700, kształt bez zmian (różnica {shape * 1000:0.0} mm)");
        Ok(Budowa.ArcGroup(tr.Vertices, 1, k).SequenceEqual(new[] { 1, 2 }), "W2 i W3 to jeden łuk złożony (bez prostej między nimi)");

        // „Łuk z punktów” na podzielonym łuku: punkty co ~12 m od 50 m przed łukiem do 50 m za nim, rozrzut ±3 cm
        w.SetTool(UtTool.LukPunkty);
        if (!w.View.FitFreeVertex) KeyPress(w, Key.W);
        if (w.View.FitWithL) KeyPress(w, Key.Tab);
        w.View.SetView(r1.At(arc.S0 + 20).P, 0.4); w.View.SimClick(S(r1.At(arc.S0 + 20).P));
        Ok(w.View.FitGroup.Count == 2, $"klik na łuku – łuk złożony z {w.View.FitGroup.Count} części");
        double s0 = 250 - 50, s1 = 250 + R1 * 20 * Math.PI / 180 + R2 * 20 * Math.PI / 180 + 50;
        var rnd = new Random(5); var pts = new List<XY>();
        for (double s = s0; s <= s1; s += 12) { var (q, az) = truth.At(s); pts.Add(q + Element.Right(az) * ((rnd.NextDouble() - 0.5) * 0.06 * k)); }
        // punkty jak klikane – wszystkie poza ostatnim od razu, ostatni kliknięciem (jedno dopasowanie zamiast kilkudziesięciu)
        w.View.FitPts.AddRange(pts.Take(pts.Count - 1));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        w.View.SetView(pts[^1], 0.1); w.View.SimClick(S(pts[^1]));
        var f = w.View.FitResult;
        var pr = f?.Parts;
        Ok(f != null && f.Error == null && pr != null && pr.Count == 2 && Math.Abs(pr[0].R - R1) <= 20 && Math.Abs(pr[1].R - R2) <= 30 && f.Max < 0.06,
            $"{pts.Count} punktów: R {pr?[0].R:0} / {pr?[1].R:0} m (zdjęcie 600 / 900, było 700 / 700), odchyłki średnio {f?.Rms * 100:0.#} cm, najwyżej {f?.Max * 100:0.#} cm, w {sw.ElapsedMilliseconds} ms ({f?.Error})");
        if (shotRails != null) { w.View.FitPoints(pts); await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_luk_koszowy.png")); }
        KeyPress(w, Key.Enter);
        tr = d.P.Tracks.First(x => x.Id == tr.Id);
        Ok(pr != null && tr.Vertices[1].R == pr[0].R && tr.Vertices[2].R == pr[1].R && w.View.Geometry(tr).Ok && d.UndoName == "Łuk z punktów",
            $"Enter – oba łuki zastosowane: W2 R {tr.Vertices[1].R}, W3 R {tr.Vertices[2].R} ({w.LastMessage})");
        w.DoUndo();
        Ok(d.P.Tracks.First(x => x.Id == tr.Id).Vertices[1].R == 700, "Ctrl+Z – znowu 700 / 700");
        KeyPress(w, Key.Escape);
        w.SetTool(UtTool.Wskaz);

        // „Podziel łuk na części” – 4 części: więcej wierzchołków do dostosowania, kształt bez zmian
        d.Change("Jeden łuk", p => p.Tracks.First(x => x.Id == tr.Id).Vertices = new() { new() { X = A.X, Y = A.Y }, new() { X = vd.X, Y = vd.Y, R = 700 }, new() { X = E.X, Y = E.Y } });
        var rb = w.View.Geometry(d.P.Tracks.First(x => x.Id == tr.Id));
        cm = w.BuildTrackMenu(tr.Id, sMid);
        var sub = cm.Items.OfType<MenuItem>().FirstOrDefault(x => (x.Header as string)?.StartsWith("Podziel łuk W2 na części") == true);
        sub?.Items.OfType<MenuItem>().First(x => x.Header as string == "4 części").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        tr = d.P.Tracks.First(x => x.Id == tr.Id); var ra = w.View.Geometry(tr);
        double sh4 = rb.Sample(2).Max(q => ra.Nearest(q.P, 0.25).Dist) / k;
        var arcs4 = Ciecie.Kawalki(ra).Where(x => x.Luk).ToList();
        Ok(sub != null && tr.Vertices.Count == 6 && tr.Vertices.Skip(1).Take(4).All(x => x.R == 700) && ra.Ok && sh4 < 0.002 && arcs4.Max(x => x.Len) - arcs4.Min(x => x.Len) < 0.01,
            $"„Podziel łuk na części → 4 części”: W2–W5, każda R 700, równe ({arcs4.FirstOrDefault()?.Len:0.00} m), kształt bez zmian ({sh4 * 1000:0.0} mm)");
        Ok(Budowa.ArcGroup(tr.Vertices, 2, k).Count == 4, "4 części to jeden łuk złożony – „Łuk z punktów” dopasuje je razem");
        await Task.CompletedTask;
    }
}

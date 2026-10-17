using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Test „Wygładź łuk” i „Przejściowe z prędkości”, jak użytkownik: łuk ze „zdjęcia” R 900 z L 60 odrysowany po swojemu – podzielony
/// na 5 części i dopasowany do punktów (promienie się rozjeżdżają) → PPM „Wygładź łuk → jeden łuk z przejściowymi”; łuk R 600, Vmax 80
/// z panelu → „Przejściowe z prędkości” (h i L ze wzorów kalkulatora, środek łuku na miejscu).
/// </summary>
public static partial class Test
{
    static async Task RunWygladzanie(MainWindow w)
    {
        const double lineAz = 110;
        var dir = Path.Combine(Path.GetTempPath(), "edytor_ut_test", "wygladzanie"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        w.SetSchemat(false);
        w.NewDoc(); w.SaveDoc(false, Path.Combine(dir, "stacja.ut.json"));
        var d = w.Doc;
        var o = Geo.ToPuwg92(51.7631, 19.5438); double k = Geo.ScaleFactor(o);
        XY Go(XY from, double azDeg, double len) => from + Element.Dir(azDeg * Math.PI / 180) * (len * k);

        // „zdjęcie”: prosta – łuk R 900, L 60, zwrot 30° – prosta
        var a = Go(o, lineAz + 180, 500); var c = Go(o, lineAz + 30, 500);
        var truth = Trasa.Compute(new List<UtVertex> { new() { X = a.X, Y = a.Y }, new() { X = o.X, Y = o.Y, R = 900, L = 60 }, new() { X = c.X, Y = c.Y } }, k);
        // odrysowane: ten sam wielobok, R 900 bez L, podzielone na 5 części i dopasowane do punktów zdjęcia (każda część z innym R)
        var v0 = new List<UtVertex> { new() { X = a.X, Y = a.Y }, new() { X = o.X, Y = o.Y, R = 900 }, new() { X = c.X, Y = c.Y } };
        var (v5, _) = Budowa.SplitArcN(v0, 1, k, 5);
        var pts = new List<XY>(); for (double s = 380; s <= 380 + 60 + 900 * Math.PI / 6 + 60 + 40; s += 5) pts.Add(truth.At(s).P);
        var fg = Budowa.FitArcGroup(v5, new[] { 1, 2, 3, 4, 5 }, pts, k, false, true);
        foreach (var (pi, pp, pr, pl) in fg.Parts) { v5[pi].X = pp.X; v5[pi].Y = pp.Y; v5[pi].R = pr; }
        var tr = new UtTrack { Number = "5", Vertices = v5 };
        d.Change("Tor", p => p.Tracks.Add(tr));
        var rs = string.Join(" / ", v5.Skip(1).Take(5).Select(x => x.R.ToString("0")));
        Ok(w.View.Geometry(tr).Ok && v5.Skip(1).Take(5).Select(x => x.R).Distinct().Count() > 2, $"łuk odrysowany jak u właściciela: 5 części R {rs}");

        // PPM „Wygładź łuk W2–W6 → jeden łuk z krzywymi przejściowymi”
        w.SetTool(UtTool.Wskaz);
        var r0 = w.View.Geometry(tr); var mid = Ciecie.Kawalki(r0).Where(x => x.Luk).ElementAt(2);
        var cm = w.BuildTrackMenu(tr.Id, (mid.S0 + mid.S1) / 2);
        var sm = cm.Items.OfType<MenuItem>().FirstOrDefault(x => (x.Header as string)?.StartsWith("Wygładź łuk W2–W6") == true);
        Ok(sm != null && sm.Items.Count == 3, $"menu PPM na łuku złożonym: „{sm?.Header}” (3 warianty)");
        sm?.Items.OfType<MenuItem>().First(x => (x.Header as string).Contains("z krzywymi przejściowymi")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        tr = d.P.Tracks.First(x => x.Id == tr.Id); var r1 = w.View.Geometry(tr);
        bool InMid(XY q, TrasaResult g) { var sq = g.Nearest(q).S; return sq > 300 && sq < g.Length - 300; }
        double toTruth = truth.Sample(3).Where(q => InMid(q.P, r1)).Max(q => Budowa.Dist(r1, q.P)) / k;
        double drawnToTruth = truth.Sample(3).Where(q => InMid(q.P, r0)).Max(q => Budowa.Dist(r0, q.P)) / k;
        double toDrawn = r0.Sample(3).Where(q => InMid(q.P, r0)).Max(q => Budowa.Dist(r1, q.P)) / k;
        Ok(tr.Vertices.Count == 3 && Math.Abs(tr.Vertices[1].R - 900) <= 18 && tr.Vertices[1].L > 0 && r1.Ok && toDrawn < 0.06 && toTruth <= drawnToTruth + 0.02 && d.UndoName == "Wygładzenie łuku",
            $"wygładzony: jeden łuk R {tr.Vertices[1].R} m, L {tr.Vertices[1].L} m; od narysowanego najwyżej {toDrawn * 100:0.#} cm, od zdjęcia {toTruth * 100:0.#} cm (narysowany był {drawnToTruth * 100:0.#} cm) – {w.LastMessage}");
        w.DoUndo();
        cm = w.BuildTrackMenu(tr.Id, (mid.S0 + mid.S1) / 2);
        cm.Items.OfType<MenuItem>().First(x => (x.Header as string)?.StartsWith("Wygładź łuk") == true).Items.OfType<MenuItem>().First(x => (x.Header as string) == "→ dwa łuki").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        tr = d.P.Tracks.First(x => x.Id == tr.Id);
        Ok(tr.Vertices.Count == 4 && w.View.Geometry(tr).Ok && w.LastMessage.Contains("dwa łuki"), $"→ dwa łuki: {w.LastMessage}");
        w.DoUndo();
        if (shotRails != null)
        {
            w.DoRedo(); w.DoUndo();
            cm = w.BuildTrackMenu(tr.Id, (mid.S0 + mid.S1) / 2);
            cm.Items.OfType<MenuItem>().First(x => (x.Header as string)?.StartsWith("Wygładź łuk") == true).Items.OfType<MenuItem>().First(x => (x.Header as string).Contains("z krzywymi")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            w.View.Select(tr.Id, 1); w.View.FitPoints(new[] { truth.At(380).P, truth.At(800).P }); await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_wygladzanie.png"));
            w.DoUndo();
        }

        // --- „Przejściowe z prędkości”: tor z łukiem R 600, Vmax 80 km/h z panelu
        var b0 = Go(o, lineAz - 90, 300); var b1 = Go(b0, lineAz + 180, 400); var b2 = Go(b0, lineAz - 25, 400);
        var t6 = new UtTrack { Number = "6", Vertices = new() { new() { X = b1.X, Y = b1.Y }, new() { X = b0.X, Y = b0.Y, R = 600 }, new() { X = b2.X, Y = b2.Y } } };
        d.Change("Tor 6", p => p.Tracks.Add(t6));
        var g0 = w.View.Geometry(t6); var arc0 = Ciecie.Kawalki(g0).First(x => x.Luk); var mid0 = g0.At((arc0.S0 + arc0.S1) / 2).P;
        w.View.Select(t6.Id);
        Click(BtnIn(w.TrackPanel, "Przejściowe z prędkości"));
        Ok(w.LastMessage.Contains("wpisz najpierw Vmax"), "bez Vmax – prośba o prędkość");
        var vbox = Find<TextBox>(w.TrackPanel, x => (x.ToolTip as string) == "Prędkość największa [km/h]");
        Type(vbox, "80");
        Click(BtnIn(w.TrackPanel, "Przejściowe z prędkości"));
        var want = Obliczenia.Licz(80, 600, null, new Dopuszczalne());
        t6 = d.P.Tracks.First(x => x.Id == t6.Id); var g1 = w.View.Geometry(t6);
        double midMove = Budowa.Dist(g1, mid0) / k;
        Ok(t6.Vmax == 80 && t6.Vertices[1].L == want.L && t6.Vertices[1].H == want.H && g1.Ok && midMove < 0.02 && d.UndoName == "Przejściowe z prędkości",
            $"80 km/h, R 600: h {t6.Vertices[1].H} mm, L {t6.Vertices[1].L} m (wzór: {want.H} / {want.L}), środek łuku przesunięty o {midMove * 100:0.#} cm – {w.LastMessage}");
        w.DoUndo();
        // za mały promień przy dużej prędkości – uwaga z kalkulatora
        d.Change("Vmax 160", p => p.Tracks.First(x => x.Id == t6.Id).Vmax = 160);
        w.View.Select(null); w.View.Select(t6.Id);
        Click(BtnIn(w.TrackPanel, "Przejściowe z prędkości"));
        Ok(w.LastMessage.Contains("promień za mały"), $"160 km/h na R 600 – uwaga „promień za mały” ({w.LastMessage})");
        await Task.CompletedTask;
    }
}

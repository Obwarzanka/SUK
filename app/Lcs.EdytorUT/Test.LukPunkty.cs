using System.IO;
using System.Windows;
using System.Windows.Input;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Test „Łuk z punktów” jak użytkownik: tor narysowany z łukiem R 800 (zgrubnie), a „na zdjęciu” łuk R 1000 z przejściowymi 60 m;
/// klik łuku, 17 punktów na prawdziwej osi z rozrzutem ±3 cm (ręczne klikanie) → R 1000 bez L, potem Tab → R i L; Backspace, Enter, Ctrl+Z.
/// </summary>
public static partial class Test
{
    static async Task RunLukPunkty(MainWindow w)
    {
        const double lineAz = 40;
        var dir = Path.Combine(Path.GetTempPath(), "edytor_ut_test", "lukpunkty"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        w.SetSchemat(false);
        w.NewDoc(); w.SaveDoc(false, Path.Combine(dir, "stacja.ut.json"));
        var d = w.Doc;
        var o = Geo.ToPuwg92(51.7631, 19.5438); double k = Geo.ScaleFactor(o);
        XY Go(XY from, double azDeg, double len) => from + Element.Dir(azDeg * Math.PI / 180) * (len * k);
        Point S(XY p) => w.View.ToScreen(p);

        // tor: prosta – załamanie 25° z łukiem R 800 (narysowany zgrubnie) – prosta
        var a = Go(o, lineAz + 180, 400); var c = Go(o, lineAz + 25, 400);
        var t = new UtTrack { Vertices = new() { new UtVertex { X = a.X, Y = a.Y }, new UtVertex { X = o.X, Y = o.Y, R = 800 }, new UtVertex { X = c.X, Y = c.Y } } };
        d.Change("Tor", p => p.Tracks.Add(t));
        // „zdjęcie”: ten sam wielobok, łuk R 1000 z przejściowymi 60 m; punkty na jego osi z rozrzutem ±3 cm
        var truth = Trasa.Compute(new List<UtVertex> { t.Vertices[0].Clone(), new UtVertex { X = o.X, Y = o.Y, R = 1000, L = 60 }, t.Vertices[2].Clone() }, k);
        double s0 = 0, len = 0, acc = 0;
        foreach (var e in truth.Elements) { if (e.Kind != ElKind.Prosta && s0 == 0) s0 = acc; if (e.Kind != ElKind.Prosta) len += e.Len; acc += e.Len; }
        var pts = new List<XY>(); var rnd = new Random(7);
        for (int i = 0; i <= 16; i++)
        {
            var (q, az) = truth.At((s0 + len * i / 16) / truth.K);
            pts.Add(q + Element.Right(az) * ((rnd.NextDouble() - 0.5) * 0.06 * k));
        }
        w.SetTool(UtTool.LukPunkty);
        Ok(w.View.Tool == UtTool.LukPunkty && w.View.FitFreeVertex, "narzędzie „Łuk z punktów” (J), domyślnie wierzchołek też dopasowywany");
        KeyPress(w, Key.W);
        Ok(!w.View.FitFreeVertex, "W – proste stałe (tylko R / L), jak tor narysowany dobrze, a źle tylko R");
        w.View.SetView(o, 0.4);
        var r0 = w.View.Geometry(t);
        w.View.SimClick(S(r0.At(r0.Nearest(o).S).P));
        Ok(w.View.SelectedTrack == t.Id && w.View.SelectedVertex == 1, "klik na łuku – wybrany łuk W2");
        foreach (var q in pts) { w.View.SetView(q, 0.1); w.View.SimClick(S(q)); }
        var f1 = w.View.FitResult;
        Ok(f1 != null && f1.Error == null && Math.Abs(f1.R - 1000) > 5 && f1.Max > 0.05,
            $"17 punktów, samo R: R {f1?.R:0} m, odchyłki średnio {f1?.Rms * 100:0.#} cm, najwyżej {f1?.Max * 100:0.#} cm (łuk ze zdjęcia ma przejściowe – samo R ich nie odda)");
        KeyPress(w, Key.Tab);
        var f2 = w.View.FitResult;
        Ok(w.View.FitWithL && f2 != null && Math.Abs(f2.R - 1000) <= 10 && Math.Abs(f2.L - 60) <= 8 && f2.Max < 0.04,
            $"Tab – R i L: R {f2?.R:0} m (zdjęcie 1000), L {f2?.L:0} m (60), odchyłki średnio {f2?.Rms * 100:0.#} cm, najwyżej {f2?.Max * 100:0.#} cm");
        KeyPress(w, Key.Back);
        Ok(w.View.FitPts.Count == 16, "Backspace – ostatni punkt usunięty");
        double rB = w.View.FitResult.R, lB = w.View.FitResult.L;
        KeyPress(w, Key.Enter);
        t = d.P.Tracks.First(x => x.Id == t.Id);
        Ok(t.Vertices[1].R == rB && t.Vertices[1].L == lB && d.UndoName == "Łuk z punktów" && w.View.Geometry(t).Ok && w.LastMessage.Contains("odchyłka"),
            $"Enter – łuk zastosowany: R {t.Vertices[1].R} m, L {t.Vertices[1].L} m, z cofaniem ({w.LastMessage})");
        w.DoUndo();
        Ok(d.P.Tracks.First(x => x.Id == t.Id).Vertices[1].R == 800, "Ctrl+Z – R 800 wrócił");
        // jeden punkt – łuk dokładnie przez niego (samo R)
        KeyPress(w, Key.Tab);
        w.View.SetView(o, 0.4); r0 = w.View.Geometry(d.P.Tracks.First(x => x.Id == t.Id));
        w.View.SimClick(S(r0.At(r0.Nearest(o).S).P));
        var mid = truth.At((s0 + len / 2) / truth.K).P; w.View.SetView(mid, 0.1); w.View.SimClick(S(mid));
        var f3 = w.View.FitResult;
        Ok(!w.View.FitWithL && f3 != null && f3.Max < 0.015, $"jeden punkt w środku łuku: R {f3?.R:0} m przechodzi przez niego ({f3?.Max * 100:0.#} cm – R zaokrąglony do 1 m)");
        KeyPress(w, Key.Escape);

        // --- jak na zrzucie właściciela: wierzchołek narysowany 8 m obok, R 700; prawdziwy łuk R 1000 (bez L) – punkty na łuku i na prostych
        KeyPress(w, Key.W);
        Ok(w.View.FitFreeVertex, "W – z powrotem: wierzchołek swobodny");
        var tv = d.P.Tracks.First(x => x.Id == t.Id);
        var trueV = tv.Vertices[1].P;
        var off = trueV + Element.Right(lineAz * Math.PI / 180) * (8 * k);
        d.Change("Zły wierzchołek", p => { var x = p.Tracks.First(q => q.Id == t.Id).Vertices[1]; x.X = off.X; x.Y = off.Y; x.R = 700; x.L = 0; });
        var truth2 = Trasa.Compute(new List<UtVertex> { tv.Vertices[0].Clone(), new UtVertex { X = trueV.X, Y = trueV.Y, R = 1000 }, tv.Vertices[2].Clone() }, k);
        double c0 = 0, cl = 0, ac = 0;
        foreach (var e in truth2.Elements) { if (e.Kind != ElKind.Prosta) { c0 = ac; cl = e.Len; } ac += e.Len; }
        var pts2 = new List<XY>(); var rnd2 = new Random(11);
        for (int i = -4; i <= 16; i++)       // 4 punkty na prostej przed łukiem (co 15 m), 13 na łuku, 4 na prostej za nim
        {
            double s = i < 0 ? c0 + i * 15 : i <= 12 ? c0 + cl * i / 12 : c0 + cl + (i - 12) * 15;
            var (q, az) = truth2.At(s / truth2.K); pts2.Add(q + Element.Right(az) * ((rnd2.NextDouble() - 0.5) * 0.06 * k));
        }
        var tr2 = w.View.Geometry(d.P.Tracks.First(x => x.Id == t.Id));
        w.View.SetView(off, 0.4); w.View.SimClick(S(tr2.At(tr2.Nearest(off).S).P));
        foreach (var q in pts2) { w.View.SetView(q, 0.1); w.View.SimClick(S(q)); }
        var f4 = w.View.FitResult;
        double vErr = f4?.Vertex is XY fv4 ? XY.Dist(fv4, trueV) / k : 99;
        Ok(f4 != null && f4.Error == null && vErr < 0.5 && Math.Abs(f4.R - 1000) <= 15 && f4.Max < 0.05,
            $"21 punktów (łuk i proste): wierzchołek {vErr:0.00} m od prawdziwego (był 8 m), R {f4?.R:0} m (zdjęcie 1000, był 700), odchyłki średnio {f4?.Rms * 100:0.#} cm, najwyżej {f4?.Max * 100:0.#} cm");
        if (shotRails != null) { w.View.FitPoints(pts2); await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_luk_z_punktow.png")); }
        KeyPress(w, Key.Enter);
        var applied = d.P.Tracks.First(x => x.Id == t.Id).Vertices[1];
        Ok(XY.Dist(applied.P, trueV) / k < 0.5 && Math.Abs(applied.R - 1000) <= 15 && w.View.Geometry(d.P.Tracks.First(x => x.Id == t.Id)).Ok,
            $"Enter – wierzchołek przesunięty i R {applied.R} m zastosowane jedną zmianą ({w.LastMessage})");
        w.DoUndo();
        Ok(XY.Dist(d.P.Tracks.First(x => x.Id == t.Id).Vertices[1].P, off) < 1e-9, "Ctrl+Z – wierzchołek wrócił na złe miejsce");
        w.SetTool(UtTool.Wskaz);
        await Task.CompletedTask;
    }
}

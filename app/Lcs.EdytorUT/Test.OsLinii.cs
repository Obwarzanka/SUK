using System.IO;
using System.Windows;
using System.Windows.Controls;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Test osi schematu po linii (PLAN_EDYTOR_UT.md § 5b, poprawka 2026-11-02): linia w łuku R 500 (60°) i dalej drugim torem prosto,
/// tor równoległy 5 m z panelu. „Oś wg toru” na torze 1 – oś idzie torem 1 i torem, który go przedłuża; w Schemacie ×4 oba tory
/// leżą poziomo także w łuku, międzytorze stałe. Wcześniej oś była prostą (cięciwa), więc tory w łuku i daleko od cięciwy uciekały
/// skosem w górę i w dół (×4 wzmacnia każdy kąt).
/// </summary>
public static partial class Test
{
    static async Task RunOsLinii(MainWindow w)
    {
        var dir = Path.Combine(Path.GetTempPath(), "edytor_ut_test", "oslinii"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        w.SetSchemat(false);
        w.NewDoc(); w.SaveDoc(false, Path.Combine(dir, "linia.ut.json"));
        var d = w.Doc;
        var o = Geo.ToPuwg92(51.7631, 19.5438); double k = Geo.ScaleFactor(o);
        Point S(XY p) => w.View.ToScreen(p);
        XY D(double azDeg) => Element.Dir(azDeg * Math.PI / 180);
        const double az0 = 75;

        // tor 1: prosta – łuk R 500 o 60° w prawo – prosta; tor 1a przedłuża go dalej prosto (koniec do końca)
        double T = 500 * Math.Tan(30 * Math.PI / 180);
        var A = o; var V = A + D(az0) * ((300 + T) * k); var E = V + D(az0 + 60) * ((T + 300) * k); var F = E + D(az0 + 60) * (400 * k);
        var t1 = new UtTrack { Number = "1", Cat = UtTrack.MainZ, Vertices = new() { new() { X = A.X, Y = A.Y }, new() { X = V.X, Y = V.Y, R = 500 }, new() { X = E.X, Y = E.Y } } };
        var t1a = new UtTrack { Number = "1a", Vertices = new() { new() { X = E.X, Y = E.Y }, new() { X = F.X, Y = F.Y } } };
        d.Change("Tory", p => { p.Tracks.Add(t1); p.Tracks.Add(t1a); });
        var r1 = w.View.Geometry(t1); var r1a = w.View.Geometry(t1a);
        Ok(r1.Ok && Math.Abs(r1.Length - (600 + 500 * Math.PI / 3)) < 0.05, $"tor 1: prosta, łuk R 500 (60°), prosta – {r1.Length:0.00} m");
        // tor 2 równoległy 5 m po lewej – z panelu, jak użytkownik
        w.SetTool(UtTool.Wskaz); w.View.Select(t1.Id);
        Type(Find<TextBox>(w.TrackPanel, x => x.Text == "4,75"), "5");
        Click(BtnIn(w.TrackPanel, "← po lewej"));
        var t2 = d.P.Tracks.First(x => x.Id != t1.Id && x.Id != t1a.Id); var r2 = w.View.Geometry(t2);
        Ok(Math.Abs(r2.Nearest(r1.At(r1.Length / 2).P).Dist / k - 5) < 0.001, "tor 2 równoległy 5 m po lewej toru 1 (także w łuku)");

        // --- automatycznie (bez wybranej osi): najdłuższy ciąg torów – tor 1 i 1a
        var auto = OsSchematu.Dla(d.P);
        Ok(auto.Tory.SequenceEqual(new[] { t1.Id, t1a.Id }) && Math.Abs(auto.Dlugosc / k - (r1.Length + r1a.Length)) < 0.5,
            $"oś automatyczna: najdłuższy ciąg torów 1 – 1a ({auto.Dlugosc / k:0.0} m)");

        // --- „Oś wg toru” na torze 1
        w.View.Select(t1.Id);
        Click(Find<Button>(Toolbar(w), x => x.Content as string == "Oś wg toru"));
        Ok(w.View.Schemat && d.P.AxisTrack == t1.Id && w.View.Os.Tory.SequenceEqual(new[] { t1.Id, t1a.Id }) && w.LastMessage.Contains("1a"),
            $"„Oś wg toru”: Schemat, oś po torze 1 i dalej 1a – „{w.LastMessage}”");
        w.View.FitPoints(r1.Sample(5).Select(q => q.P).Concat(r1a.Sample(5).Select(q => q.P)).ToList());
        double dyMax = 0, gapErr = 0; double? y0 = null; bool rising = true; double lastX = double.MinValue;
        double gapWant = 5 * k * w.View.Stretch / w.View.Mpp;
        foreach (var (rr, len) in new[] { (r1, r1.Length), (r1a, r1a.Length) })
            for (double s = 0; s <= len; s += 10)
            {
                var p = rr.At(s).P; var sp = S(p);
                y0 ??= sp.Y; dyMax = Math.Max(dyMax, Math.Abs(sp.Y - y0.Value));
                if (sp.X < lastX - 1e-6) rising = false; lastX = sp.X;
                if (rr == r1) { var q = r2.At(r2.Nearest(p, 0.2).S).P; gapErr = Math.Max(gapErr, Math.Abs((sp.Y - S(q).Y) - gapWant)); }
            }
        Ok(dyMax < 0.05 && rising, $"oś po linii: tor 1 i 1a na ekranie poziomo także w łuku (odchyłka {dyMax:0.0000} px), kilometraż od lewej do prawej");
        Ok(gapErr < 0.05, $"międzytorze 5 m ×4 stałe na całej długości, także w łuku ({gapWant:0.00} px, odchyłka {gapErr:0.0000} px), tor 2 (lewy) nad torem 1");
        // dla porównania: ta sama linia na osi prostej (cięciwa toru 1)
        var prosta = OsSchematu.ProstaAz(Trasa.Az(A, E)); var (_, vA) = prosta.UV(r1.At(r1.Length / 2).P); var (_, vB) = prosta.UV(A);
        Ok(Math.Abs(vA - vB) * w.View.Stretch / w.View.Mpp > 50, $"na osi prostej (cięciwa) środek łuku odchodzi od początku toru o {Math.Abs(vA - vB) * w.View.Stretch / w.View.Mpp:0} px – stąd „rozjeżdżanie się” schematu");

        // ekran ↔ świat, kółko, przeciąganie przy osi po linii
        double worst = 0;
        foreach (var s in new[] { 0.0, 150, 300, 420, 560, 700, 880 })
            foreach (var off in new[] { -30.0, -5, 0, 7, 25 })
            {
                var (p, az) = r1.At(Math.Min(s, r1.Length)); var q = p + Element.Right(az) * off;
                worst = Math.Max(worst, XY.Dist(w.View.ToWorld(S(q)), q));
            }
        Ok(worst < 1e-6, $"oś po linii: ekran ↔ świat tam i z powrotem (do 30 m od osi, w łuku; błąd {worst:0.0e0} m)");
        var sp0 = new Point(320, 210); var before = w.View.ToWorld(sp0);
        w.View.ZoomAt(sp0, 1 / 1.2); w.View.ZoomAt(sp0, 1.5);
        Ok(XY.Dist(before, w.View.ToWorld(sp0)) < 1e-6, "oś po linii: kółko myszy – punkt pod kursorem zostaje");
        var c0 = w.View.ToWorld(new Point(500, 300)); w.View.PanBy(new Vector(60, -25));
        Ok(XY.Dist(w.View.ToWorld(new Point(560, 275)), c0) < 1e-6, "oś po linii: przeciąganie – punkt jedzie za myszą");
        // znak przy torze w łuku: kierunek toru na ekranie poziomo
        var (pm, azm) = r1.At(r1.Length / 2); var dm = w.View.ScreenDir(pm, azm);
        Ok(Math.Abs(dm.Y) < 1e-3 && dm.X > 0.999, $"kierunek toru w środku łuku na ekranie: ({dm.X:0.0000}; {dm.Y:0.0000}) – znaki stają wzdłuż toru");
        // klik na torze w łuku zaznacza tor 2 (trafienie liczone z ekranu)
        w.View.FitPoints(r1.Sample(5).Select(q => q.P).ToList());
        w.View.Select(null); w.View.SimClick(S(r2.At(r2.Length / 2).P));
        Ok(w.View.SelectedTrack == t2.Id, "klik na torze 2 w środku łuku zaznacza tor 2");

        // ↻ 180°: kilometraż osi w lewo, Ctrl+Z wraca
        w.View.Select(null);
        Click(Find<Button>(Toolbar(w), x => x.Content as string == "↻ 180°"));
        var a0 = S(r1.At(0).P); var a1 = S(r1.At(r1.Length).P);
        Ok(d.P.AxisRev && a1.X < a0.X && Math.Abs(a1.Y - a0.Y) < 0.05, "↻ 180°: oś odwrócona – tor 1 od prawej do lewej, nadal poziomo");
        w.DoUndo();
        a0 = S(r1.At(0).P); a1 = S(r1.At(r1.Length).P);
        Ok(!d.P.AxisRev && d.P.AxisTrack == t1.Id && a1.X > a0.X, "Ctrl+Z – oś znowu od lewej do prawej");

        // wydruk planu schematycznego tą samą osią
        var str = Eksport.Render(d, new Eksport.Opcje(true, 2000, 4, 96, "dopasowany (jeden arkusz)", true, w.View.Center, 0, 0, Array.Empty<string>(), w.View.Os));
        var page = str[0]; var pa = page.ToPx(r1.At(0).P); var pb = page.ToPx(r1.At(r1.Length / 2).P); var pc = page.ToPx(r1a.At(r1a.Length).P);
        Ok(Math.Abs(pa.Y - pb.Y) < 0.05 && Math.Abs(pa.Y - pc.Y) < 0.05 && pc.X > pb.X && pb.X > pa.X, $"wydruk planu schematycznego: linia w łuku poziomo ({str.Count} ark.)");

        // zapis i odczyt: oś zapamiętana
        Ok(w.SaveDoc(false), "zapis");
        var re = UtDocument.Load(d.Path);
        Ok(re.P.AxisTrack == t1.Id && !re.P.AxisRev && UtDocument.Serialize(re.P) == UtDocument.Serialize(d.P), "odczyt: oś po torze 1 zapamiętana w projekcie");
        if (shotRails != null)
        {
            w.View.FitPoints(r1.Sample(5).Select(q => q.P).Concat(r1a.Sample(5).Select(q => q.P)).ToList());
            await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_os_linii.png"));
        }
        await Task.CompletedTask;
    }
}

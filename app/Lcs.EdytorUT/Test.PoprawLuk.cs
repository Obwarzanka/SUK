using System.IO;
using System.Windows;
using System.Windows.Controls;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Test „Popraw łuk” jak użytkownik: rozjazd postawiony przeciągnięciem tak, że łuk toru zaczyna się w jego połowie (uwaga „na granicy
/// prostej i łuku”) → przyciski w panelu rozjazdu, klik – łuk zaczyna się za rozjazdem, uwaga znika, komunikat o zmianie R; tor
/// z przejściowymi (rozjazd na krzywej przejściowej) – to samo; menu PPM rozjazdu.
/// </summary>
public static partial class Test
{
    static async Task RunPoprawLuk(MainWindow w)
    {
        const double lineAz = 20;
        var dir = Path.Combine(Path.GetTempPath(), "edytor_ut_test", "poprawluk"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        w.SetSchemat(false);
        w.NewDoc(); w.SaveDoc(false, Path.Combine(dir, "stacja.ut.json"));
        var d = w.Doc;
        var o = Geo.ToPuwg92(51.7631, 19.5438); double k = Geo.ScaleFactor(o);
        XY Go(XY from, double azDeg, double len) => from + Element.Dir(azDeg * Math.PI / 180) * (len * k);
        Point S(XY p) => w.View.ToScreen(p);

        foreach (var (L, tag) in new[] { (0.0, "łuk bez przejściowych"), (40.0, "łuk z przejściowymi L 40") })
        {
            var a = Go(o, lineAz + 180, 400); var c = Go(o, lineAz + 25, 400);
            var t = new UtTrack { Number = "1", Vertices = new() { new() { X = a.X, Y = a.Y }, new() { X = o.X, Y = o.Y, R = 800, L = L }, new() { X = c.X, Y = c.Y } } };
            d.Change("Tor", p => { p.Tracks.Clear(); p.Turnouts.Clear(); p.Tracks.Add(t); });
            var r = w.View.Geometry(t); double cs = Ciecie.Kawalki(r).First(x => !x.Luk ? false : true).S0;
            if (L > 0) { double acc = 0; foreach (var e in r.Elements) { if (e.Kind != ElKind.Prosta) { cs = acc / r.K; break; } acc += e.Len; } }
            // rozjazd przeciągnięciem: PR 15 m przed początkiem łuku, odgałęzienie w lewo (na zewnątrz łuku)
            w.SetTool(UtTool.Rozjazd); w.RzTypeBox.SelectedItem = "Rz 60E1-190-1:9";
            var (pr, az) = r.At(cs - 15); w.View.SetView(pr, 0.3);
            w.View.SimDrag(S(pr), S(pr + Element.Dir(az) * (25 * k) - Element.Right(az) * (1 * k)));
            var z = d.P.Turnouts.FirstOrDefault();
            var uw = z != null ? Rozjazdy.Check(d.P, z) : new();
            Ok(z != null && uw.Any(x => x.Contains("granicy prostej i łuku") || x.Contains("krzywej przejściowej")), $"{tag}: rozjazd na początku łuku – uwaga „{uw.FirstOrDefault()}”");
            w.SetTool(UtTool.Wskaz); w.View.SelectTurnout(z.Id);
            var btns = w.TrackPanel.Children.OfType<Button>().Where(b => (b.Content as string)?.StartsWith("Popraw łuk") == true).ToList();
            var menu = w.BuildTurnoutMenu(z.Id).Items.OfType<MenuItem>().Count(x => (x.Header as string)?.StartsWith("Popraw łuk") == true);
            Ok(btns.Count >= 1 && menu == btns.Count, $"{tag}: panel rozjazdu – {btns.Count} warianty „Popraw łuk” (też w menu PPM): {string.Join(" | ", btns.Select(b => b.Content))}");
            if (btns.Count == 0) continue;
            double r0 = d.P.Tracks[0].Vertices[1].R; var vtx = d.P.Tracks[0].Vertices[1].P;
            Click(btns[0]);
            var t2 = d.P.Tracks[0]; var z2 = d.P.Turnouts[0];
            Ok(!Rozjazdy.Check(d.P, z2).Any(x => x.Contains("granicy") || x.Contains("przejściowej")) && w.View.Geometry(t2).Ok && XY.Dist(t2.Vertices[1].P, vtx) < 1e-9
               && t2.Vertices[1].R != r0 && w.LastMessage.Contains("zmiana R") && d.UndoName == "Popraw łuk",
                $"{tag}: klik – uwaga znika, wierzchołek ten sam, R {r0} → {t2.Vertices[1].R} m ({w.LastMessage})");
            if (shotRails != null && L > 0) { w.View.SetView(w.View.Geometry(t2).At(cs).P, 0.25); await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_popraw_luk.png")); }
            w.DoUndo();
            Ok(Rozjazdy.Check(d.P, d.P.Turnouts[0]).Any(x => x.Contains("granicy") || x.Contains("przejściowej")), $"{tag}: Ctrl+Z – łuk jak był");
        }
        w.SetTool(UtTool.Wskaz);
    }
}

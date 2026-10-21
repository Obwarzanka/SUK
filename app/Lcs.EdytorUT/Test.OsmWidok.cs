using System.IO;
using System.Windows;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Test pobierania sieci OSM dla widoku Schemat (zgłoszenie właściciela 2026-10-21: „czemu tu nie mam osi” – w Schemacie linie OSM
/// urywały się): tor ukośny (azymut 55°), Schemat z osią wzdłuż toru i rozciągnięciem ×4, narzędzie Ślad. Zamiast Overpass –
/// zapamiętane prostokąty: obszar obejmuje wszystkie cztery narożniki ekranu; po przesunięciu mapy brakujący obszar dobiera się sam.
/// </summary>
public static partial class Test
{
    static async Task RunOsmWidok(MainWindow w)
    {
        var dir = Path.Combine(Path.GetTempPath(), "edytor_ut_test", "osmwidok"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        w.SetSchemat(false);
        w.NewDoc(); w.SaveDoc(false, Path.Combine(dir, "stacja.ut.json"));
        var d = w.Doc;
        var o = Geo.ToPuwg92(51.7631, 19.5438); double k = Geo.ScaleFactor(o);
        var a = o - Element.Dir(55 * Math.PI / 180) * (1500 * k); var b = o + Element.Dir(55 * Math.PI / 180) * (1500 * k);
        var t = new UtTrack { Number = "1", Vertices = new() { new() { X = a.X, Y = a.Y }, new() { X = b.X, Y = b.Y } } };
        d.Change("Tor", p => { p.Tracks.Clear(); p.Turnouts.Clear(); p.Tracks.Add(t); });
        w.SetSchemat(true); w.View.Select(t.Id); w.AxisFromTrack();
        var asked = new List<(XY A, XY B)>();
        w.View.OsmFetchForTest = (p, q) => { asked.Add((p, q)); var s = new OsmSiec(); s.Nodes[asked.Count] = (p + q) * 0.5; return s; };
        w.View.OsmAutoTest = true;
        w.View.Osm = null;
        w.View.SetView(o, 0.5);
        w.SetTool(UtTool.Slad);
        await w.View.OsmPobierz();
        double W = w.View.ActualWidth, H = w.View.ActualHeight;
        bool In((XY A, XY B) r, XY p) => p.X >= r.A.X && p.X <= r.B.X && p.Y >= r.A.Y && p.Y <= r.B.Y;
        XY[] Corners() => new[] { w.View.ToWorld(new Point(0, 0)), w.View.ToWorld(new Point(W, 0)), w.View.ToWorld(new Point(0, H)), w.View.ToWorld(new Point(W, H)) };
        var c0 = Corners();
        // dawny prostokąt z dwóch narożników – w obróconym Schemacie nie obejmuje pozostałych dwóch
        var old = (new XY(Math.Min(c0[0].X, c0[3].X), Math.Min(c0[0].Y, c0[3].Y)), new XY(Math.Max(c0[0].X, c0[3].X), Math.Max(c0[0].Y, c0[3].Y)));
        Ok(asked.Count == 1 && c0.All(p => In(asked[0], p)) && !(In(old, c0[1]) && In(old, c0[2])),
            $"Schemat obrócony (oś {w.View.AxisAz * 180 / Math.PI:0}°): sieć OSM pobrana dla całego ekranu – wszystkie 4 narożniki w prostokącie (z dwóch narożników – nie)");
        // przesunięcie mapy w Śladzie – brakujący obszar dobiera się sam
        w.View.PanBy(new Vector(W * 0.9, 0));
        await WaitFor(() => asked.Count >= 2, 3000);
        var c1 = Corners();
        Ok(asked.Count == 2 && c1.All(p => w.View.Osm.Covers(p, p)), $"po przesunięciu mapy w Śladzie sieć dobrała się sama ({asked.Count} pobrania), widok pokryty");
        w.View.PanBy(new Vector(-W * 0.05, 0)); await Task.Delay(1000);
        Ok(asked.Count == 2, "małe przesunięcie w pobranym obszarze – bez nowego pobrania");
        w.View.OsmAutoTest = false; w.View.OsmFetchForTest = null; w.View.Osm = null;
        w.SetTool(UtTool.Wskaz); w.SetSchemat(false);
    }
}

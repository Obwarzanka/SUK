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
        // pierwsze pobranie nieudane (błąd Overpass) – sieć pusta; po ruchu mapy pobiera się jeszcze raz (dawniej nigdy: „oś nie zawsze się pokazuje”)
        w.SetTool(UtTool.Wskaz); w.View.Osm = null; w.View.OsmShown = true;
        bool fail = true;
        w.View.OsmFetchForTest = (p, q) => { if (fail) throw new System.Net.Http.HttpRequestException("test: Overpass niedostępny"); asked.Add((p, q)); var s = new OsmSiec(); s.Nodes[asked.Count] = (p + q) * 0.5; return s; };
        await w.View.OsmPobierz();
        Ok(w.View.Osm == null, "nieudane pobranie – brak sieci");
        fail = false; int n0 = asked.Count;
        w.View.PanBy(new Vector(W * 0.3, 0));
        await WaitFor(() => asked.Count > n0, 3000);
        Ok(w.View.Osm != null && w.View.OsmCovers(), "sieć OSM włączona, pierwsze pobranie nieudane – po przesunięciu mapy dobrała się sama");
        // błąd Overpass bez ruchu mapy – ponowienie samo (w programie po 15 s)
        w.View.OsmRetrySec = 0.5; fail = true;
        string info = ""; Action<string> onInfo = m => info = m; w.View.DraftInfo += onInfo;
        w.View.PanBy(new Vector(W * 0.9, 0));
        await WaitFor(() => !w.View.OsmCovers() && info.Contains("ponowię"), 3000);
        w.View.DraftInfo -= onInfo;
        int n1 = asked.Count; fail = false;
        await WaitFor(() => asked.Count > n1 && w.View.OsmCovers(), 4000);
        Ok(asked.Count > n1 && w.View.OsmCovers(), "pobranie nieudane (serwer przeciążony), mapa stoi – ponowienie samo, widok pokryty");
        w.View.OsmRetrySec = 15;
        // zapis na dysku – raz pobrana sieć bez Overpass
        var net = new OsmSiec(); net.Nodes[12345678901] = o; net.Nodes[2] = o + new XY(10, 5); net.Ways.Add(new long[] { 12345678901, 2 }); net.Areas.Add((o - new XY(50, 50), o + new XY(50, 50)));
        var f = Path.Combine(dir, "osm", "siec.json"); net.Save(f);
        var back = OsmSiec.Load(f);
        Ok(back != null && back.Ways.Count == 1 && back.Nodes.ContainsKey(12345678901) && XY.Dist(back.Nodes[2], net.Nodes[2]) < 1e-6 && back.Covers(o, o) && !back.Covers(o + new XY(80, 0), o + new XY(80, 0)),
            "sieć OSM zapisana na dysku i odczytana – drogi, węzły (id OSM > 2³²), pobrane obszary");
        File.WriteAllText(f, "{zepsuty"); Ok(OsmSiec.Load(f) == null, "uszkodzony plik sieci – pominięty");
        // odpowiedź API OpenStreetMap (XML): tylko drogi kolejowe, węzły w PUWG
        var api = OsmPunkty.ParseApi(System.Text.Encoding.UTF8.GetBytes(
            "<osm version=\"0.6\"><node id=\"1\" lat=\"51.7631\" lon=\"19.5438\"/><node id=\"2\" lat=\"51.7640\" lon=\"19.5450\"/><node id=\"3\" lat=\"51.7650\" lon=\"19.5460\"/>" +
            "<way id=\"10\"><nd ref=\"1\"/><nd ref=\"2\"/><tag k=\"railway\" v=\"rail\"/></way><way id=\"11\"><nd ref=\"2\"/><nd ref=\"3\"/><tag k=\"highway\" v=\"residential\"/></way>" +
            "<way id=\"12\"><nd ref=\"1\"/><nd ref=\"99\"/><tag k=\"railway\" v=\"rail\"/></way><way id=\"13\"><nd ref=\"1\"/><nd ref=\"3\"/><tag k=\"railway\" v=\"platform\"/></way></osm>"));
        Ok(api.Ways.Count == 1 && api.Nodes.Count == 2 && XY.Dist(api.Nodes[1], Geo.ToPuwg92(51.7631, 19.5438)) < 1e-6,
            "API OpenStreetMap: z odpowiedzi tylko tor (railway=rail), bez ulicy, peronu i drogi z brakującym węzłem");
        // kolor linii OSM: nie pomarańczowy jak pas OpenRailwayMap pod nią (wtedy znikała), fioletowy z białą obwódką
        w.SetSchemat(false); w.View.OsmAutoTest = false;
        var line = new OsmSiec(); var oc = o + new XY(300, 0); var la = oc - new XY(0, 400); var lb = oc + new XY(0, 400);   // 300 m od toru 1 (inaczej piksel toru)
        line.Nodes[1] = la; line.Nodes[2] = lb; line.Ways.Add(new long[] { 1, 2 }); line.Areas.Add((o - new XY(5000, 5000), o + new XY(5000, 5000)));
        w.View.Osm = line; w.View.OsmShown = true; w.View.SetView(oc, 0.5); w.View.UpdateLayout();
        var dv = new System.Windows.Media.DrawingVisual();
        using (var dc = dv.RenderOpen()) dc.DrawRectangle(new System.Windows.Media.VisualBrush(w.View), null, new Rect(0, 0, w.View.ActualWidth, w.View.ActualHeight));
        var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap((int)w.View.ActualWidth, (int)w.View.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); bmp.Render(dv);
        byte[] Px(Point p) { var b = new byte[4]; bmp.CopyPixels(new Int32Rect((int)Math.Round(p.X), (int)Math.Round(p.Y), 1, 1), b, 4, 0); return b; }   // B, G, R, A
        var mid = w.View.ToScreen(oc);
        // linia ma 1,4 px – najciemniejszy piksel w pasku ±4 px w poprzek; obwódka 3 px dalej od niego
        var best = Enumerable.Range(-4, 9).Select(dy => mid + new Vector(0, dy)).OrderBy(q => Px(q)[1]).First(); var on = Px(best);
        // ekran: X (północ) w górę, więc linia wzdłuż Y (wschód) jest pozioma – obwódka obok w pionie
        var side = Px(best + new Vector(0, -1.4)); if (side[1] < 200) side = Px(best + new Vector(0, 1.4));
        Ok(on[0] > on[2] && on[2] > 60 && on[1] < 80, $"linia OSM fioletowa (B {on[0]}, G {on[1]}, R {on[2]}) – nie ginie w pomarańczowym pasie OpenRailwayMap");
        Ok(side[0] > 200 && side[1] > 200 && side[2] > 200, "linia OSM z białą obwódką");
        w.View.Osm = null;
        w.View.OsmShown = false;
        w.View.OsmAutoTest = false; w.View.OsmFetchForTest = null; w.View.Osm = null;
        w.SetTool(UtTool.Wskaz); w.SetSchemat(false);
    }
}

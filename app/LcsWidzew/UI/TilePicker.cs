using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LcsWidzew.Model;

namespace LcsWidzew.UI;

/// <summary>
/// Kafelki mapy w odwzorowaniu Web Mercator (jak OSM): kafelek ortofotomapy = kafelek z15 (ok. 750 m przy 52° N).
/// Ortofotomapa z Geoportalu pobierana w EPSG:3857 dokładnie w granicach kafelka – kafelki składają się bez szwów.
/// </summary>
public static class WebTiles
{
    public const int PickZ = 15;
    const double R = 20037508.342789244;            // pół obwodu w metrach Web Mercator
    public static (double X, double Y) LatLonToPx(double lat, double lon, double z)
    {
        double n = Math.Pow(2, z) * 256, phi = lat * Math.PI / 180;
        return ((lon + 180) / 360 * n, (1 - Math.Log(Math.Tan(phi) + 1 / Math.Cos(phi)) / Math.PI) / 2 * n);
    }
    public static (double Lat, double Lon) PxToLatLon(double x, double y, double z)
    {
        double n = Math.Pow(2, z) * 256;
        return (Math.Atan(Math.Sinh(Math.PI * (1 - 2 * y / n))) * 180 / Math.PI, x / n * 360 - 180);
    }
    /// <summary>Granice kafelka w metrach EPSG:3857 (minX, minY, maxX, maxY).</summary>
    public static (double, double, double, double) Bbox3857(int x, int y, int z)
    {
        double s = 2 * R / Math.Pow(2, z), minX = -R + x * s, maxY = R - y * s;
        return (minX, maxY - s, minX + s, maxY);
    }
    /// <summary>Metry w terenie na piksel przy kafelku z po n pikseli (szerokość geograficzna środka kafelka).</summary>
    public static double GroundMpp(int y, int z, int n)
    {
        var (lat, _) = PxToLatLon(0, (y + 0.5) * 256, z);
        return 2 * R * Math.Cos(lat * Math.PI / 180) / Math.Pow(2, z) / n;
    }
}

/// <summary>Okno wyboru kafelków ortofotomapy na mapie OSM z liniami kolejowymi (OpenRailwayMap).</summary>
public class TilePickerWindow : Window
{
    public readonly HashSet<(int X, int Y)> Selected = new();
    public HashSet<(int X, int Y)> Existing = new();
    public int Source => srcBox.SelectedIndex;              // 0 – ortofotomapa (Geoportal), 1 – zdjęcia satelitarne (Esri)
    public int N => resBox.SelectedIndex == 0 ? 1024 : 2048;
    readonly MapCanvas map;
    readonly ComboBox srcBox = new() { Width = 260 }, resBox = new() { Width = 220 };
    readonly TextBlock info = new() { Foreground = Brushes.LightGray, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };

    public TilePickerWindow(double lat, double lon, int zoom)
    {
        Title = "Wybór kafelków ortofotomapy"; Width = 1300; Height = 860; Background = new SolidColorBrush(Color.FromRgb(0x1a, 0x1a, 0x1a));
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        map = new MapCanvas(this);
        var (cx, cy) = WebTiles.LatLonToPx(lat, lon, zoom); map.Z = zoom; map.CX = cx; map.CY = cy;
        var root = new DockPanel();
        var bar = new WrapPanel { Margin = new Thickness(6) };
        TextBlock L(string s) => new() { Text = s, Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0) };
        Button B(string s, RoutedEventHandler h) { var b = new Button { Content = s, Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(3), Background = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26)), Foreground = Brushes.White }; b.Click += h; return b; }
        var go = new TextBox { Width = 170, Background = Brushes.Black, Foreground = Brushes.White, CaretBrush = Brushes.White, VerticalAlignment = VerticalAlignment.Center, ToolTip = "szerokość, długość geograficzna – np. z Google Maps" };
        bar.Children.Add(L("Idź do (szer., dł.):")); bar.Children.Add(go);
        bar.Children.Add(B("Idź", (a, e) => { if (GeoPodklad.TryLatLon(go.Text, out var la, out var lo)) map.GoTo(la, lo, 14); }));
        go.KeyDown += (a, e) => { if (e.Key == Key.Enter && GeoPodklad.TryLatLon(go.Text, out var la, out var lo)) map.GoTo(la, lo, 14); };
        srcBox.ItemsSource = new[] { "Ortofotomapa – Geoportal (GUGiK)", "Zdjęcia satelitarne – Esri World Imagery" }; srcBox.SelectedIndex = 0;
        resBox.ItemsSource = new[] { "1024 px na kafelek (ok. 0,7 m/px)", "2048 px na kafelek (ok. 0,4 m/px)" }; resBox.SelectedIndex = 0;
        bar.Children.Add(L("Źródło:")); bar.Children.Add(srcBox); bar.Children.Add(L("Rozdzielczość:")); bar.Children.Add(resBox);
        var orm = new CheckBox { Content = "linie kolejowe (OpenRailwayMap)", IsChecked = true, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        orm.Click += (a, e) => { map.Rail = orm.IsChecked == true; map.InvalidateVisual(); };
        bar.Children.Add(orm);
        bar.Children.Add(B("Wyczyść zaznaczenie", (a, e) => { Selected.Clear(); Changed(); }));
        var ok = B("Pobierz zaznaczone i wstaw na plan", (a, e) => { if (Selected.Count > 0) { DialogResult = true; Close(); } });
        ok.Background = new SolidColorBrush(Color.FromRgb(0x1d, 0x5a, 0x2a));
        bar.Children.Add(ok);
        bar.Children.Add(B("Anuluj", (a, e) => { DialogResult = false; Close(); }));
        bar.Children.Add(info);
        DockPanel.SetDock(bar, Dock.Top); root.Children.Add(bar);
        var help = new TextBlock { Text = "LPM – zaznacz / odznacz kafelek; przeciągnij z LPM, żeby „pomalować” kafelki wzdłuż linii.  PPM albo środkowy – przesuń mapę.  Kółko – przybliż.  Kafelki do wyboru widać od przybliżenia 11.  Zielone – już na planie.   Mapa © współtwórcy OpenStreetMap, linie © OpenRailwayMap.", Foreground = Brushes.Gray, Margin = new Thickness(8, 2, 8, 4), TextWrapping = TextWrapping.Wrap };
        DockPanel.SetDock(help, Dock.Bottom); root.Children.Add(help);
        root.Children.Add(map);
        Content = root;
        srcBox.SelectionChanged += (a, e) => Changed(); resBox.SelectionChanged += (a, e) => Changed();
        Changed();
    }

    public void Changed()
    {
        double km = Selected.Count > 0 ? Selected.Average(t => WebTiles.GroundMpp(t.Y, WebTiles.PickZ, 1)) / 1000 : 0;
        info.Text = Selected.Count == 0 ? "Zaznacz kafelki na mapie" : $"Zaznaczone: {Selected.Count} kaf. (ok. {km:0.00} × {km:0.00} km każdy), ok. {Selected.Count * (N == 1024 ? 0.3 : 1.1):0.#} MB";
        map.InvalidateVisual();
    }

    /// <summary>Mapa: kafelki OSM + OpenRailwayMap, siatka kafelków z15, zaznaczanie myszą.</summary>
    class MapCanvas : FrameworkElement
    {
        readonly TilePickerWindow w;
        public int Z = 6; public double CX, CY; public bool Rail = true;
        readonly Dictionary<string, BitmapSource> cache = new(); readonly HashSet<string> loading = new();
        static readonly SemaphoreSlim gate = new(6);
        Point? pan; bool paint, paintAdd;
        public MapCanvas(TilePickerWindow w) { this.w = w; ClipToBounds = true; Focusable = true; }

        public void GoTo(double lat, double lon, int z) { Z = z; (CX, CY) = WebTiles.LatLonToPx(lat, lon, z); InvalidateVisual(); }
        Point ToScreen(double gx, double gy) => new(gx - CX + ActualWidth / 2, gy - CY + ActualHeight / 2);
        (double X, double Y) ToGlobal(Point p) => (p.X - ActualWidth / 2 + CX, p.Y - ActualHeight / 2 + CY);

        BitmapSource Tile(string kind, int z, int x, int y)
        {
            int n = 1 << z; if (y < 0 || y >= n) return null; x = ((x % n) + n) % n;
            var key = $"{kind}/{z}/{x}/{y}";
            if (cache.TryGetValue(key, out var b)) return b;
            if (loading.Add(key))
            {
                var url = kind == "osm" ? $"https://tile.openstreetmap.org/{z}/{x}/{y}.png" : $"https://tiles.openrailwaymap.org/standard/{z}/{x}/{y}.png";
                _ = Task.Run(async () =>
                {
                    await gate.WaitAsync();
                    try
                    {
                        var data = await GeoPodklad.Http.GetByteArrayAsync(url);
                        var bi = new BitmapImage(); bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.StreamSource = new MemoryStream(data); bi.EndInit(); bi.Freeze();
                        await Dispatcher.BeginInvoke(() => { if (cache.Count > 800) cache.Clear(); cache[key] = bi; loading.Remove(key); InvalidateVisual(); });
                    }
                    catch { await Dispatcher.BeginInvoke(() => { cache[key] = null; loading.Remove(key); }); }
                    finally { gate.Release(); }
                });
            }
            return null;
        }

        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xdd, 0xdd, 0xd8)), null, new Rect(0, 0, ActualWidth, ActualHeight));
            var (gx0, gy0) = ToGlobal(new Point(0, 0)); var (gx1, gy1) = ToGlobal(new Point(ActualWidth, ActualHeight));
            int tx0 = (int)Math.Floor(gx0 / 256), ty0 = (int)Math.Floor(gy0 / 256), tx1 = (int)Math.Floor(gx1 / 256), ty1 = (int)Math.Floor(gy1 / 256);
            foreach (var kind in Rail ? new[] { "osm", "orm" } : new[] { "osm" })
                for (int ty = ty0; ty <= ty1; ty++)
                    for (int tx = tx0; tx <= tx1; tx++)
                        if (Tile(kind, Z, tx, ty) is { } img) dc.DrawImage(img, new Rect(ToScreen(tx * 256, ty * 256), new Size(256, 256)));
            if (Z < 11) { Label(dc, "Przybliż mapę (kółko), żeby wybierać kafelki"); return; }
            // siatka kafelków ortofotomapy (z15) w bieżącym przybliżeniu
            double k = Math.Pow(2, Z - WebTiles.PickZ) * 256;          // piksele ekranu na kafelek z15
            int sx0 = (int)Math.Floor(gx0 / k), sy0 = (int)Math.Floor(gy0 / k), sx1 = (int)Math.Floor(gx1 / k), sy1 = (int)Math.Floor(gy1 / k);
            var grid = new Pen(new SolidColorBrush(Color.FromArgb(0x70, 0x30, 0x30, 0x30)), 1);
            var sel = new SolidColorBrush(Color.FromArgb(0x70, 0xff, 0x8c, 0x00)); var ex = new SolidColorBrush(Color.FromArgb(0x60, 0x20, 0xc0, 0x40));
            var selPen = new Pen(new SolidColorBrush(Color.FromRgb(0xff, 0x6a, 0x00)), 2);
            for (int y = sy0; y <= sy1; y++)
                for (int x = sx0; x <= sx1; x++)
                {
                    var r = new Rect(ToScreen(x * k, y * k), new Size(k, k));
                    bool s = w.Selected.Contains((x, y)), e = w.Existing.Contains((x, y));
                    dc.DrawRectangle(s ? sel : e ? ex : null, s ? selPen : grid, r);
                }
        }

        void Label(DrawingContext dc, string s)
        {
            var ft = new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 15, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0xc0, 0, 0, 0)), null, new Rect(8, 8, ft.Width + 16, ft.Height + 8)); dc.DrawText(ft, new Point(16, 12));
        }

        (int X, int Y)? TileAt(Point p)
        {
            if (Z < 11) return null;
            var (gx, gy) = ToGlobal(p); double k = Math.Pow(2, Z - WebTiles.PickZ) * 256;
            return ((int)Math.Floor(gx / k), (int)Math.Floor(gy / k));
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            Focus(); var p = e.GetPosition(this);
            if (e.ChangedButton == MouseButton.Left && TileAt(p) is { } t)
            {
                paint = true; paintAdd = !w.Selected.Contains(t);
                if (paintAdd) w.Selected.Add(t); else w.Selected.Remove(t);
                CaptureMouse(); w.Changed();
            }
            else if (e.ChangedButton is MouseButton.Right or MouseButton.Middle || e.ChangedButton == MouseButton.Left) { pan = p; CaptureMouse(); }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            var p = e.GetPosition(this);
            if (paint && TileAt(p) is { } t) { if (paintAdd ? w.Selected.Add(t) : w.Selected.Remove(t)) w.Changed(); }
            else if (pan is Point q) { CX -= p.X - q.X; CY -= p.Y - q.Y; pan = p; InvalidateVisual(); }
        }
        protected override void OnMouseUp(MouseButtonEventArgs e) { paint = false; pan = null; ReleaseMouseCapture(); }
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            int nz = Math.Clamp(Z + (e.Delta > 0 ? 1 : -1), 3, 18); if (nz == Z) return;
            var p = e.GetPosition(this); var (gx, gy) = ToGlobal(p); double f = Math.Pow(2, nz - Z);
            // punkt pod kursorem zostaje na miejscu
            CX = gx * f - (p.X - ActualWidth / 2); CY = gy * f - (p.Y - ActualHeight / 2); Z = nz; InvalidateVisual();
        }
    }
}

public partial class EditorWindow
{
    /// <summary>Okno mapki: wybór kafelków ortofotomapy, pobranie i wstawienie mozaiki na plan w skali.</summary>
    async void OpenTilePicker()
    {
        if (FuzzMode || NoPrompt || bgBusy) return;
        var (lat, lon, z) = PickerStart();
        var win = new TilePickerWindow(lat, lon, z) { Owner = this };
        foreach (var o in (Bgs ?? new JsonArray()).OfType<JsonObject>().Where(IsTiles)) foreach (var t in Tiles(o)) win.Existing.Add(t);
        if (win.ShowDialog() != true || win.Selected.Count == 0) return;
        string src = win.Source == 1 ? "esri" : "orto"; int n = win.N; var tiles = win.Selected.ToList();
        await RunBg(() => { var dir = GeoPodklad.Mosaic(src, n, tiles, BgDir, Progress); return () => AddTiles(src, n, tiles, dir); });
    }

    /// <summary>Początek mapki: środek istniejącej mozaiki, ostatni podkład geograficzny albo cała Polska.</summary>
    (double Lat, double Lon, int Z) PickerStart()
    {
        var mz = (Bgs ?? new JsonArray()).OfType<JsonObject>().FirstOrDefault(IsTiles);
        if (mz != null && Tiles(mz) is { Count: > 0 } ts)
        {
            var (la, lo) = WebTiles.PxToLatLon((ts.Average(t => t.X) + 0.5) * 256, (ts.Average(t => t.Y) + 0.5) * 256, WebTiles.PickZ);
            return (la, lo, 14);
        }
        if (GeoPodklad.TryLatLon(LastLatLon(), out var a, out var b)) return (a, b, 14);
        return (52.0, 19.4, 6);
    }

    /// <summary>
    /// Wstawienie kafelków: do istniejącej mozaiki tego samego źródła (we właściwym miejscu), obok innej mozaiki – wyrównane
    /// geograficznie do niej (ten sam obrót i skala), a pierwsza mozaika – w skali planu na środku widoku.
    /// </summary>
    void AddTiles(string src, int n, List<(int X, int Y)> add, string dir, bool fit = true)
    {
        const int z = WebTiles.PickZ;
        var all = doc.PlanBgs;
        var same = all.OfType<JsonObject>().FirstOrDefault(o => IsTiles(o) && StationDoc.Sv(o["src"]) == src && (int)Num(o["n"], 0) == n && (int)Num(o["z"], 0) == z);
        doc.Snapshot();
        string note = "";
        if (doc.Nodes.Count == 0 && !doc.RealScale && all.Count == 0) { doc.SetPlanScale(doc.ScaleAlong, doc.ScaleAlong, true); note = $" Plan pusty – skala rzeczywista 1:{doc.ScaleAlong:0}."; }
        if (same != null)
        {
            // dołożenie do mozaiki: nowy lewy górny kafelek przesuwa początek układu pikseli
            var t = BgXf(same, BgPx(same)); var (x0, y0, _, _) = TileSpan(same);
            var tl = Tiles(same).Union(add).Distinct().ToList();
            int nx0 = Math.Min(x0, tl.Min(q => q.X)), ny0 = Math.Min(y0, tl.Min(q => q.Y));
            t = t with { O = BgToWorld(t, new Point((nx0 - x0) * n, (ny0 - y0) * n)) };
            same["tiles"] = new JsonArray(tl.OrderBy(q => q.Y).ThenBy(q => q.X).Select(q => (JsonNode)new JsonArray(q.X, q.Y)).ToArray());
            same["tx0"] = nx0; same["ty0"] = ny0;
            BgStore(same, BgPx(same), t);
            bgActive = all.IndexOf(same);
            Changed($"Dołożono {add.Count} kafelków do mozaiki (razem {tl.Count}) – we właściwym miejscu, w skali planu.{note}");
            return;
        }
        int ax = add.Min(q => q.X), ay = add.Min(q => q.Y);
        var bg = new JsonObject
        {
            ["type"] = "tiles", ["src"] = src, ["z"] = z, ["n"] = n, ["dir"] = BgRel(dir), ["tx0"] = ax, ["ty0"] = ay,
            ["tiles"] = new JsonArray(add.OrderBy(q => q.Y).ThenBy(q => q.X).Select(q => (JsonNode)new JsonArray(q.X, q.Y)).ToArray()),
            ["opacity"] = 0.85, ["rot"] = 0,
            ["attribution"] = src == "esri" ? "Zdjęcia satelitarne: Esri World Imagery (Esri, Maxar, Earthstar Geographics)" : "Ortofotomapa: GUGiK, geoportal.gov.pl",
        };
        var (cLat, cLon) = WebTiles.PxToLatLon((add.Average(q => q.X) + 0.5) * 256, (add.Average(q => q.Y) + 0.5) * 256, z);
        bg["geo"] = new JsonObject { ["src"] = src, ["lat"] = Math.Round(cLat, 6), ["lon"] = Math.Round(cLon, 6), ["mpp"] = Math.Round(WebTiles.GroundMpp((int)add.Average(q => q.Y), z, n), 4) };
        var size = BgPx(bg);
        var other = all.OfType<JsonObject>().FirstOrDefault(IsTiles);
        BgT nt;
        if (other != null)
        {
            // ta sama siatka Web Mercator: piksel mozaiki B → piksel mozaiki A = k·p + c; obrót i rozciągnięcie jak w A
            var ta = BgXf(other, BgPx(other)); var (ox0, oy0, _, _) = TileSpan(other); int on = (int)Num(other["n"], 1024), oz = (int)Num(other["z"], z);
            double f = Math.Pow(2, oz - z), k = (double)on / n * f;
            var c = new Point((ax * f - ox0) * on, (ay * f - oy0) * on);
            nt = new BgT(BgToWorld(ta, c), ta.Rot, ta.S * k, ta.Sy);
            note += " Wyrównane do istniejącej mozaiki.";
        }
        else
        {
            // pierwsza mozaika: skala planu (wzdłuż – podłużna, w poprzek – poprzeczna), środek widoku
            double s = WebTiles.GroundMpp((int)add.Average(q => q.Y), z, n) * doc.Ux, sy = doc.Uy / doc.Ux;
            var v = panel.ViewWorld;
            Point mid = st != null && st.Nodes.Count > 0 && !v.IsEmpty ? new Point(v.X + v.Width / 2, v.Y + v.Height / 2) : new Point(0, 0);
            nt = new BgT(mid - new Vector(size.PixelWidth * s / 2, size.PixelHeight * s * sy / 2), 0, s, sy);
        }
        BgStore(bg, size, nt);
        all.Add(bg); bgActive = all.Count - 1;
        Changed($"Wstawiono mozaikę {add.Count} kafelków ({(src == "esri" ? "zdjęcia satelitarne" : "ortofotomapa")}, 1 px ≈ {WebTiles.GroundMpp(ay, z, n):0.##} m) w skali planu 1:{doc.ScaleAlong:0} / 1:{doc.ScaleAcross:0}.{note} Obróć: „1. Wyrównaj tor do poziomu” – kolejne kafelki dołożą się już obrócone.");
        if (fit && !FuzzMode) panel.FitAll();
    }

    /// <summary>
    /// Test (--editshot … kafelki orto|esri "lat, lon" out.json): mozaika 3 kafelków, obrót, dołożenie 2 kafelków (te same miejsca
    /// geograficzne zostają w miejscu), druga mozaika z innego źródła wyrównana do pierwszej, okno mapki (zrzut obok).
    /// </summary>
    public string TilesForTest(string src, string latlon, string outJson, string mapShot)
    {
        var log = new System.Text.StringBuilder(); FuzzMode = true; int bad = 0;
        void Ok(bool c, string m) { log.AppendLine((c ? "OK  " : "BŁĄD") + " " + m); if (!c) bad++; }
        PlanForTest();
        GeoPodklad.TryLatLon(latlon, out var lat, out var lon);
        var (gx, gy) = WebTiles.LatLonToPx(lat, lon, WebTiles.PickZ); int cx = (int)(gx / 256), cy = (int)(gy / 256);
        var first = new List<(int, int)> { (cx - 1, cy), (cx, cy), (cx + 1, cy) };
        var dir = Task.Run(() => GeoPodklad.Mosaic(src, 1024, first, BgDir, null)).GetAwaiter().GetResult();
        Ok(first.All(t => File.Exists(Path.Combine(dir, $"{t.Item1}_{t.Item2}.jpg"))), $"pobrano 3 kafelki → {dir}");
        AddTiles(src, 1024, first, dir, false);
        var a = Bg; Ok(IsTiles(a) && Tiles(a).Count == 3, "mozaika na planie");
        // punkt geograficzny (środek stacji) – położenie na planie przez mozaikę
        Point Geo(JsonObject bg, double la, double lo)
        {
            var (px, py) = WebTiles.LatLonToPx(la, lo, Num(bg["z"], 15)); var (x0, y0, _, _) = TileSpan(bg); double nn = Num(bg["n"], 1024) / 256;
            return BgToWorld(BgXf(bg, BgPx(bg)), new Point((px - x0 * 256) * nn, (py - y0 * 256) * nn));
        }
        double want = WebTiles.GroundMpp(cy, 15, 1024) * doc.Ux, got = BgXf(a, BgPx(a)).S;
        Ok(Math.Abs(got / want - 1) < 1e-4 && Math.Abs(BgXf(a, BgPx(a)).Sy - doc.Uy / doc.Ux) < 1e-9, $"skala: 1 px = {WebTiles.GroundMpp(cy, 15, 1024):0.###} m → {got:0.####} j. planu (1:{doc.ScaleAlong:0} / 1:{doc.ScaleAcross:0})");
        doc.Snapshot(); a["rot"] = -12.5; Changed();
        var p0 = Geo(a, lat, lon);
        // dołożenie kafelków na lewo i do góry – istniejące miejsca bez ruchu
        var more = new List<(int, int)> { (cx - 2, cy), (cx - 1, cy - 1) };
        Task.Run(() => GeoPodklad.Mosaic(src, 1024, more, BgDir, null)).GetAwaiter().GetResult();
        AddTiles(src, 1024, more, dir, false);
        Ok(Tiles(Bg).Count == 5 && Bgs.Count == 1, "dołożono 2 kafelki do tej samej mozaiki");
        Ok((Geo(Bg, lat, lon) - p0).Length < 0.01, $"po dołożeniu punkt stacji w tym samym miejscu (Δ {(Geo(Bg, lat, lon) - p0).Length:0.####})");
        // druga mozaika z innego źródła i rozdzielczości – wyrównana geograficznie
        string src2 = src == "orto" ? "esri" : "orto";
        var two = new List<(int, int)> { (cx, cy) };
        var dir2 = Task.Run(() => GeoPodklad.Mosaic(src2, 2048, two, BgDir, null)).GetAwaiter().GetResult();
        AddTiles(src2, 2048, two, dir2, false);
        var b = Bg;
        Ok(Bgs.Count == 2 && (Geo(b, lat, lon) - p0).Length < 0.05, $"druga mozaika ({src2}, 2048 px) wyrównana do pierwszej (Δ {(Geo(b, lat, lon) - p0).Length:0.###}), obrót {Num(b["rot"], 0):0.##}°");
        doc.Save(outJson);
        Ok((StationDoc.Load(outJson).Root["planBgs"] as JsonArray)?.Count == 2, "zapis dwóch podkładów");
        bgActive = 0; ShowProps();
        ZoomForTest(p0.X, p0.Y, Math.Max(0.05, panel.ActualWidth / (1600 * doc.Ux)));
        // okno mapki z zaznaczeniem (zrzut po wczytaniu kafelków mapy)
        if (mapShot != null)
        {
            var win = new TilePickerWindow(lat, lon, 14) { ShowInTaskbar = false };
            foreach (var t in Tiles(a)) win.Existing.Add(t);
            foreach (var t in new[] { (cx + 2, cy), (cx + 3, cy), (cx + 3, cy + 1) }) win.Selected.Add(t);
            win.Changed(); win.Show();
            var until = DateTime.Now.AddSeconds(8);
            while (DateTime.Now < until) { var fr = new System.Windows.Threading.DispatcherFrame(); Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () => fr.Continue = false); System.Windows.Threading.Dispatcher.PushFrame(fr); Thread.Sleep(50); }
            win.UpdateLayout();
            var rtb = new RenderTargetBitmap((int)win.ActualWidth, (int)win.ActualHeight, 96, 96, PixelFormats.Pbgra32); rtb.Render(win);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb)); using (var fs = File.Create(mapShot)) enc.Save(fs);
            win.Close();
        }
        log.AppendLine($"błędów testu: {bad}, błędów edytora: {Errors.Count}");
        return log.ToString();
    }
}

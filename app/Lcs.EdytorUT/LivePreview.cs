using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Podgląd na żywo pod podkładami projektu (nie zapisuje się w projekcie): ortofotomapa Geoportalu dla bieżącego widoku
/// (jedno zapytanie WMS w rozdzielczości ekranu, gdy widok przestanie się ruszać) albo kafelki Esri / OSM.
/// Poprzedni obraz zostaje na swoim miejscu, dopóki nie przyjdzie nowy – przy przesuwaniu nie ma pustego ekranu.
/// </summary>
public class LivePreview
{
    public const string None = "brak", Orto = "orto", Esri = "esri", Osm = "osm";
    public static readonly (string Key, string Name)[] Sources = { (Orto, "Ortofotomapa (Geoportal)"), (Esri, "Zdjęcia Esri"), (Osm, "Mapa OpenStreetMap"), (None, "Brak") };

    string source = Orto;
    public string Source { get => source; set { source = value; ortoImg = null; Changed?.Invoke(); Schedule(); } }
    public event Action Changed;
    public string Status { get; private set; } = "";
    public const string Orm = "orm", OrmAttribution = "Linie kolejowe © OpenRailwayMap, OpenStreetMap";
    /// <summary>Nakładka nad podglądem: <see cref="Orm"/> (linie kolejowe, przezroczyste kafelki) albo null.</summary>
    public string Overlay { get => overlay; set { overlay = value; Changed?.Invoke(); } }
    string overlay;
    public string Attribution => source switch { Orto => Pobieranie.OrtoAttribution, Esri => "Zdjęcia: Esri World Imagery", Osm => "Mapa © współtwórcy OpenStreetMap", _ => "" };

    // ortofotomapa: ostatni obraz widoku
    public (BitmapSource Bmp, Affine Geo)? ortoImg;
    public bool OrtoBusy => ortoReq > ortoDone;
    int ortoReq, ortoDone;
    readonly DispatcherTimer settle = new() { Interval = TimeSpan.FromMilliseconds(350) };
    Func<(XY TopLeft, double Mpp, int W, int H)> viewNow;
    CancellationTokenSource cts;

    // kafelki Web Mercator: pamięć (najwyżej MaxMem), dysk (Kafelki.Dir), kolejka od środka widoku
    const int MaxMem = 800, Workers = 8;
    readonly Dictionary<(string, int, int, int), (BitmapSource Bmp, Affine Geo)> tiles = new();
    readonly LinkedList<(string, int, int, int)> tileOrder = new();
    readonly List<(string, int, int, int)> queue = new();
    readonly HashSet<(string, int, int, int)> inFlight = new(), wanted = new();
    readonly Dictionary<(string, int, int, int), DateTime> tileFailed = new();
    readonly Dictionary<string, (double X, double Y)> center = new();
    int running;
    /// <summary>Kafelki w kolejce i w trakcie pobierania.</summary>
    public int TilePending { get { lock (tiles) return queue.Count + inFlight.Count; } }
    /// <summary>Kafelki wzięte z dysku / z sieci (do testu i opisu).</summary>
    public int FromDisk, FromNet;
    /// <summary>Kafelki, których nie udało się pobrać (ponawiane po 20 s); ostatni błąd.</summary>
    public int FailedCount { get { lock (tiles) return tileFailed.Count; } }
    public string LastError { get; private set; }
    readonly DispatcherTimer redraw = new() { Interval = TimeSpan.FromMilliseconds(40) };

    public LivePreview(Func<(XY TopLeft, double Mpp, int W, int H)> view)
    {
        viewNow = view;
        settle.Tick += (s, e) => { settle.Stop(); RequestOrto(); };
        redraw.Tick += (s, e) => { redraw.Stop(); Changed?.Invoke(); };
    }

    /// <summary>Widok się zmienił – zapytanie o ortofotomapę po chwili bezruchu.</summary>
    public void Schedule() { if (source == Orto) { settle.Stop(); settle.Start(); } }

    async void RequestOrto()
    {
        var (tl, mpp, w, h) = viewNow();
        if (w < 16 || h < 16 || source != Orto) return;
        // obraz w rozdzielczości ekranu, najwyżej 4096 px
        double k = Math.Max(1, Math.Max(w, h) / (double)Pobieranie.WmsMax);
        int W = (int)(w / k), H = (int)(h / k); double m = mpp * k;
        double maxX = tl.X, minY = tl.Y, minX = maxX - H * m, maxY = minY + W * m;
        if (!Geo.InPoland(new XY((minX + maxX) / 2, (minY + maxY) / 2))) { Status = "ortofotomapa tylko dla Polski"; Changed?.Invoke(); return; }
        if (m > 40) { ortoImg = null; Status = "przybliż widok, aby zobaczyć ortofotomapę"; Changed?.Invoke(); return; }
        int id = ++ortoReq;
        cts?.Cancel(); cts = new CancellationTokenSource(); var ct = cts.Token;
        Status = "pobieranie ortofotomapy…"; Changed?.Invoke();
        try
        {
            var data = await Pobieranie.Orto(minX, minY, maxX, maxY, W, H, ct, std: m > 2);
            var bmp = await Task.Run(() => Pobieranie.Decode(data), ct);
            if (id < ortoReq) return;          // w międzyczasie nowsze zapytanie
            ortoImg = (bmp, Affine.NorthUp(maxX, minY, m));
            Status = "";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
        catch (Exception ex) { if (id == ortoReq) Status = "ortofotomapa: " + Pobieranie.Reason(ex) + " – spróbuj Esri"; }
        finally { if (id == ortoReq) ortoDone = id; }
        Changed?.Invoke();
    }

    /// <summary>Poziom kafelków dla rozdzielczości widoku (piksel kafelka ≈ piksel ekranu).</summary>
    public static int ZoomFor(double mpp, double lat) => Math.Clamp((int)Math.Round(Math.Log2(156543.034 * Math.Cos(lat * Math.PI / 180) / mpp)), 1, 19);

    /// <summary>
    /// Kafelki do narysowania (w kolejności rysowania); brakujące są zamawiane – najpierw te przy środku widoku. Zamiast brakującego
    /// rysuje się kafelek z poziomu wyżej (przycięty kawałek większego) albo cztery z poziomu niżej, jeśli są w pamięci – przy
    /// przybliżaniu i oddalaniu nie ma pustego ekranu.
    /// </summary>
    public List<(BitmapSource Bmp, Affine Geo)> VisibleTiles(XY tl, XY br) => VisibleTiles(tl, br, source);

    public List<(BitmapSource Bmp, Affine Geo)> VisibleTiles(XY tl, XY br, string source)
    {
        var res = new List<(BitmapSource, Affine)>();
        if (source != Esri && source != Osm && source != Orm) return res;
        var (_, mpp, _, _) = viewNow();
        var (lat1, lon1) = Geo.ToWgs84(tl); var (lat2, lon2) = Geo.ToWgs84(br);
        var (lat3, lon3) = Geo.ToWgs84(new XY(tl.X, br.Y)); var (lat4, lon4) = Geo.ToWgs84(new XY(br.X, tl.Y));
        int z = ZoomFor(mpp, (lat1 + lat2) / 2);
        // OpenRailwayMap daje kafelki 512 px – poziom niżej pokrywa ekran piksel w piksel, a kafelków jest 4 razy mniej
        if (source == Orm) z = Math.Clamp(z - 1, 1, 19);
        var p = new[] { Pobieranie.TileOf(lat1, lon1, z), Pobieranie.TileOf(lat2, lon2, z), Pobieranie.TileOf(lat3, lon3, z), Pobieranie.TileOf(lat4, lon4, z) };
        int x0 = (int)Math.Floor(p.Min(q => q.X)), x1 = (int)Math.Floor(p.Max(q => q.X)), y0 = (int)Math.Floor(p.Min(q => q.Y)), y1 = (int)Math.Floor(p.Max(q => q.Y));
        if ((x1 - x0 + 1) * (y1 - y0 + 1) > 120) return res;
        var coarse = new List<(int D, BitmapSource Bmp, Affine Geo)>(); var fine = new List<(BitmapSource, Affine)>();
        bool pump = false;
        lock (tiles)
        {
            wanted.RemoveWhere(k => k.Item1 == source);
            center[source] = ((x0 + x1 + 1) / 2.0, (y0 + y1 + 1) / 2.0);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var key = (source, z, x, y);
                    if (tiles.TryGetValue(key, out var t)) { res.Add(t); Touch(key); continue; }
                    wanted.Add(key);
                    if (!inFlight.Contains(key) && !queue.Contains(key) && !(tileFailed.TryGetValue(key, out var ft) && DateTime.UtcNow - ft < TimeSpan.FromSeconds(20)))
                    { queue.Add(key); pump = true; }
                    // zastępstwo: przodek (poziom wyżej) albo czwórka dzieci (poziom niżej)
                    bool found = false;
                    for (int d = 1; d <= 6 && z - d >= 1 && !found; d++)
                        if (tiles.TryGetValue((source, z - d, x >> d, y >> d), out var an) && Crop(key, d, an.Bmp) is BitmapSource cb)
                        {
                            // przycięty kawałek przodka dokładnie w miejscu brakującego kafelka (ORM jest przezroczysty – cały przodek
                            // prześwitywałby pod sąsiednimi kafelkami podwójnymi liniami)
                            var g = Pobieranie.TileGeo(z, x, y); double f = 256.0 / cb.PixelWidth;
                            coarse.Add((d, cb, g with { A = g.A * f, B = g.B * f, D = g.D * f, E = g.E * f })); found = true;
                        }
                    if (!found && z < 19)
                    {
                        var ch = new[] { (2 * x, 2 * y), (2 * x + 1, 2 * y), (2 * x, 2 * y + 1), (2 * x + 1, 2 * y + 1) };
                        foreach (var (cx, cy) in ch) if (tiles.TryGetValue((source, z + 1, cx, cy), out var c)) fine.Add(c);
                    }
                }
            // przodkowie od najgrubszego – drobniejsze zastępstwa na wierzchu, właściwe kafelki na końcu
            var fall = coarse.OrderByDescending(c => c.D).Select(c => (c.Bmp, c.Geo)).ToList();
            fall.AddRange(fine);
            res.InsertRange(0, fall);
        }
        if (pump) Pump();
        return res;
    }

    readonly Dictionary<((string, int, int, int), int), BitmapSource> crops = new();

    /// <summary>Kawałek kafelka przodka (o <paramref name="d"/> poziomów wyżej) odpowiadający kafelkowi <paramref name="key"/>.</summary>
    BitmapSource Crop((string, int Z, int X, int Y) key, int d, BitmapSource an)
    {
        if (crops.TryGetValue((key, d), out var c)) return c;
        int n = 1 << d, sz = an.PixelWidth / n; if (sz < 1) return null;
        int cx = key.X & (n - 1), cy = key.Y & (n - 1);
        try { c = new CroppedBitmap(an, new Int32Rect(cx * sz, cy * sz, sz, sz)); c.Freeze(); } catch { return null; }
        if (crops.Count > 2000) crops.Clear();
        return crops[(key, d)] = c;
    }

    /// <summary>Położenie kafelka dla jego rzeczywistego rozmiaru (256 albo 512 px – OpenRailwayMap) – piksel obrazu, nie piksel 256.</summary>
    static Affine Geo256(Affine g, BitmapSource bmp)
    {
        double f = 256.0 / Math.Max(1, bmp.PixelWidth);
        return f == 1 ? g : g with { A = g.A * f, B = g.B * f, D = g.D * f, E = g.E * f };
    }

    void Touch((string, int, int, int) key) { tileOrder.Remove(key); tileOrder.AddLast(key); }

    /// <summary>Uruchamia pobieranie z kolejki (najwyżej <see cref="Workers"/> naraz).</summary>
    void Pump()
    {
        while (true)
        {
            (string, int, int, int) key;
            lock (tiles)
            {
                // kafelki, których już nie widać, wypadają z kolejki
                queue.RemoveAll(k => !wanted.Contains(k));
                if (running >= Workers || queue.Count == 0) return;
                // najbliższy środka widoku
                int bi = 0; double bd = double.MaxValue;
                for (int i = 0; i < queue.Count; i++)
                {
                    var k = queue[i]; var c = center.TryGetValue(k.Item1, out var cc) ? cc : (0, 0);
                    double d = Math.Pow(k.Item3 + 0.5 - c.Item1, 2) + Math.Pow(k.Item4 + 0.5 - c.Item2, 2);
                    if (d < bd) { bd = d; bi = i; }
                }
                key = queue[bi]; queue.RemoveAt(bi); inFlight.Add(key); running++;
            }
            _ = LoadTile(key);
        }
    }

    async Task LoadTile((string Src, int Z, int X, int Y) key)
    {
        bool ok = false;
        try
        {
            var (bmp, disk) = await Task.Run(() => Kafelki.Load(key.Src, key.Z, key.X, key.Y));
            if (bmp != null) Interlocked.Increment(ref FromDisk);
            else
            {
                var data = await Pobieranie.Retry(() => Pobieranie.Get(Pobieranie.TileUrl(key.Src, key.Z, key.X, key.Y), CancellationToken.None), CancellationToken.None, 3);
                bmp = await Task.Run(() => { var b = Pobieranie.Decode(data); Kafelki.Save(key.Src, key.Z, key.X, key.Y, data); return b; });
                Interlocked.Increment(ref FromNet);
            }
            var geo = Geo256(Pobieranie.TileGeo(key.Z, key.X, key.Y), bmp);
            lock (tiles)
            {
                tiles[key] = (bmp, geo); tileOrder.AddLast(key); tileFailed.Remove(key);
                while (tileOrder.Count > MaxMem) { tiles.Remove(tileOrder.First.Value); tileOrder.RemoveFirst(); }
            }
            ok = true;
        }
        catch (Exception ex)
        {
            LastError = Pobieranie.Reason(ex);
            // stary kafelek z dysku lepszy niż dziura
            try { var (old, _) = await Task.Run(() => Kafelki.Load(key.Src, key.Z, key.X, key.Y, anyAge: true)); if (old != null) lock (tiles) { tiles[key] = (old, Geo256(Pobieranie.TileGeo(key.Z, key.X, key.Y), old)); tileOrder.AddLast(key); ok = true; } } catch { }
            if (!ok) lock (tiles) tileFailed[key] = DateTime.UtcNow;
        }
        finally { lock (tiles) { inFlight.Remove(key); running--; } }
        Application.Current?.Dispatcher.BeginInvoke(() => { if (!redraw.IsEnabled) redraw.Start(); Pump(); });
    }
}

/// <summary>
/// Kafelki podglądu na dysku: %LOCALAPPDATA%\LcsEdytorUT\kafelki\źródło\z\x\y.png. Ważne 30 dni (potem pobierane od nowa;
/// gdy serwer nie odpowie – użyty stary). Nie należą do projektu.
/// </summary>
public static class Kafelki
{
    public static string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LcsEdytorUT", "kafelki");
    static string FileOf(string src, int z, int x, int y) => Path.Combine(Dir, src, z.ToString(), x.ToString(), y + ".png");

    public static (BitmapSource Bmp, bool Disk) Load(string src, int z, int x, int y, bool anyAge = false)
    {
        var f = FileOf(src, z, x, y);
        if (!File.Exists(f)) return (null, false);
        if (!anyAge && DateTime.UtcNow - File.GetLastWriteTimeUtc(f) > TimeSpan.FromDays(30)) return (null, false);
        try { return (Pobieranie.Decode(File.ReadAllBytes(f)), true); } catch { return (null, false); }
    }

    public static void Save(string src, int z, int x, int y, byte[] data)
    {
        try { var f = FileOf(src, z, x, y); Directory.CreateDirectory(Path.GetDirectoryName(f)); File.WriteAllBytes(f, data); } catch { }
    }
}

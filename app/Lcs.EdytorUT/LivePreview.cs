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
    public string Attribution => source switch { Orto => Pobieranie.OrtoAttribution, Esri => "Zdjęcia: Esri World Imagery", Osm => "Mapa © współtwórcy OpenStreetMap", _ => "" };

    // ortofotomapa: ostatni obraz widoku
    public (BitmapSource Bmp, Affine Geo)? ortoImg;
    public bool OrtoBusy => ortoReq > ortoDone;
    int ortoReq, ortoDone;
    readonly DispatcherTimer settle = new() { Interval = TimeSpan.FromMilliseconds(350) };
    Func<(XY TopLeft, double Mpp, int W, int H)> viewNow;
    CancellationTokenSource cts;

    // kafelki Web Mercator
    readonly Dictionary<(string, int, int, int), (BitmapSource Bmp, Affine Geo)> tiles = new();
    readonly HashSet<(string, int, int, int)> tilePending = new(), tileFailed = new();
    readonly LinkedList<(string, int, int, int)> tileOrder = new();
    readonly SemaphoreSlim tileSlots = new(6);
    public int TilePending { get { lock (tiles) return tilePending.Count; } }

    public LivePreview(Func<(XY TopLeft, double Mpp, int W, int H)> view)
    {
        viewNow = view;
        settle.Tick += (s, e) => { settle.Stop(); RequestOrto(); };
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
        catch (OperationCanceledException) { return; }
        catch (Exception ex) { if (id == ortoReq) Status = "ortofotomapa: " + Pobieranie.Reason(ex) + " – spróbuj Esri"; }
        finally { if (id == ortoReq) ortoDone = id; }
        Changed?.Invoke();
    }

    /// <summary>Poziom kafelków dla rozdzielczości widoku (piksel kafelka ≈ piksel ekranu).</summary>
    public static int ZoomFor(double mpp, double lat) => Math.Clamp((int)Math.Round(Math.Log2(156543.034 * Math.Cos(lat * Math.PI / 180) / mpp)), 1, 19);

    /// <summary>Widoczne kafelki (te, które są w pamięci); brakujące są zamawiane.</summary>
    public List<(BitmapSource Bmp, Affine Geo)> VisibleTiles(XY tl, XY br)
    {
        var res = new List<(BitmapSource, Affine)>();
        if (source != Esri && source != Osm) return res;
        var (_, mpp, _, _) = viewNow();
        var (lat1, lon1) = Geo.ToWgs84(tl); var (lat2, lon2) = Geo.ToWgs84(br);
        var (lat3, lon3) = Geo.ToWgs84(new XY(tl.X, br.Y)); var (lat4, lon4) = Geo.ToWgs84(new XY(br.X, tl.Y));
        int z = ZoomFor(mpp, (lat1 + lat2) / 2);
        var p = new[] { Pobieranie.TileOf(lat1, lon1, z), Pobieranie.TileOf(lat2, lon2, z), Pobieranie.TileOf(lat3, lon3, z), Pobieranie.TileOf(lat4, lon4, z) };
        int x0 = (int)Math.Floor(p.Min(q => q.X)), x1 = (int)Math.Floor(p.Max(q => q.X)), y0 = (int)Math.Floor(p.Min(q => q.Y)), y1 = (int)Math.Floor(p.Max(q => q.Y));
        if ((x1 - x0 + 1) * (y1 - y0 + 1) > 120) return res;
        lock (tiles)
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var key = (source, z, x, y);
                    if (tiles.TryGetValue(key, out var t)) res.Add(t);
                    else if (!tileFailed.Contains(key) && tilePending.Add(key)) _ = LoadTile(key);
                }
        return res;
    }

    async Task LoadTile((string Src, int Z, int X, int Y) key)
    {
        await tileSlots.WaitAsync();
        try
        {
            var data = await Pobieranie.Retry(() => Pobieranie.GetFresh(Pobieranie.TileUrl(key.Src, key.Z, key.X, key.Y), CancellationToken.None), CancellationToken.None, 3);
            var bmp = await Task.Run(() => Pobieranie.Decode(data));
            var geo = Pobieranie.TileGeo(key.Z, key.X, key.Y);
            lock (tiles)
            {
                tiles[key] = (bmp, geo); tileOrder.AddLast(key);
                while (tileOrder.Count > 600) { tiles.Remove(tileOrder.First.Value); tileOrder.RemoveFirst(); }
            }
        }
        catch { lock (tiles) tileFailed.Add(key); }
        finally { lock (tiles) tilePending.Remove(key); tileSlots.Release(); }
        Application.Current?.Dispatcher.BeginInvoke(() => Changed?.Invoke());
    }
}

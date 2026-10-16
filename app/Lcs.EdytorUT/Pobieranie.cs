using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Pobieranie podkładów z internetu.
/// Ortofotomapa: Geoportal GUGiK, WMS wprost w PUWG 1992 (EPSG:2180, WMS 1.3.0 – kolejność osi w BBOX: X (północ), Y (wschód)),
/// najpierw pełna rozdzielczość (HighResolution, ok. 5–25 cm), gdzie jej brak – StandardResolution.
/// Zapasowo zdjęcia Esri World Imagery i mapa OpenStreetMap (kafelki Web Mercator).
/// Geoportal losowo zwraca 404 z wadliwego serwera za load balancerem – każda próba nowym połączeniem, bez ciasteczek, do 6 prób.
/// </summary>
public static class Pobieranie
{
    public const string OrtoHigh = "https://mapy.geoportal.gov.pl/wss/service/PZGIK/ORTO/WMS/HighResolution";
    public const string OrtoStd = "https://mapy.geoportal.gov.pl/wss/service/PZGIK/ORTO/WMS/StandardResolution";
    public const string EsriTiles = "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{0}/{2}/{1}";
    public const string OsmTiles = "https://tile.openstreetmap.org/{0}/{1}/{2}.png";
    public const string OrmTiles = "https://tiles.openrailwaymap.org/standard/{0}/{1}/{2}.png";
    public const string OrtoAttribution = "Ortofotomapa © GUGiK, geoportal.gov.pl";
    public const int WmsMax = 4096, TileSize = 2048, MaxTiles = 600;

    static readonly HttpClient http = Create();
    static HttpClient Create()
    {
        var h = new HttpClient(new SocketsHttpHandler { UseCookies = false, PooledConnectionLifetime = TimeSpan.FromSeconds(30) }) { Timeout = TimeSpan.FromSeconds(60) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd("LcsEdytorUT/0.2 (edytor ukladu torowego, symulator szkoleniowy LCS)");
        return h;
    }

    /// <summary>Zapytanie nowym połączeniem (Connection: close) – ponowna próba trafia na inny serwer.</summary>
    public static async Task<byte[]> GetFresh(string url, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url); req.Headers.ConnectionClose = true;
        using var resp = await http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsByteArrayAsync(ct);
    }

    public static async Task<byte[]> Retry(Func<Task<byte[]>> get, CancellationToken ct, int n = 6)
    {
        for (int i = 1; ; i++)
        {
            ct.ThrowIfCancellationRequested();
            try { return await get(); }
            catch (Exception ex) when (i < n && ex is not OperationCanceledException) { await Task.Delay(400 * i, ct); }
        }
    }

    /// <summary>Obraz z bajtów (wątek dowolny, wynik zamrożony); wyjątek – to nie obraz (np. błąd WMS w XML).</summary>
    public static BitmapSource Decode(byte[] data, int decodeWidth = 0)
    {
        var bi = new BitmapImage();
        bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.StreamSource = new MemoryStream(data);
        if (decodeWidth > 0) bi.DecodePixelWidth = decodeWidth;
        bi.EndInit(); bi.Freeze();
        return bi;
    }

    /// <summary>
    /// Obraz bez treści – jednolity (Geoportal czasem zwraca „200 OK” z pustym białym obrazem 115 315 B zamiast zdjęcia,
    /// ok. 1 na 6 zapytań z wadliwego serwera; sprawdzone 2026-10-16). Miniatura 64 px: rozpiętość jasności &lt; 6.
    /// </summary>
    public static bool IsBlank(BitmapSource img)
    {
        var f = new FormatConvertedBitmap(img, PixelFormats.Gray8, null, 0);
        int w = f.PixelWidth, h = f.PixelHeight; var px = new byte[w * h];
        f.CopyPixels(px, w, 0);
        byte mn = 255, mx = 0; foreach (var b in px) { if (b < mn) mn = b; if (b > mx) mx = b; }
        return mx - mn < 6;
    }
    public static bool IsBlank(byte[] data) => IsBlank(Decode(data, 64));
    public static bool IsBlankFile(string file) { try { return IsBlank(File.ReadAllBytes(file)); } catch { return true; } }

    public static string WmsUrl(string svc, double minX, double minY, double maxX, double maxY, int w, int h) =>
        FormattableString.Invariant($"{svc}?SERVICE=WMS&REQUEST=GetMap&VERSION=1.3.0&LAYERS=Raster&STYLES=&CRS=EPSG:2180&BBOX={minX:0.###},{minY:0.###},{maxX:0.###},{maxY:0.###}&WIDTH={w}&HEIGHT={h}&FORMAT=image/jpeg");

    /// <summary>
    /// Ortofotomapa prostokąta PUWG 1992 jako JPEG w × h px – sprawdzony (obraz, wymiar). Pełna rozdzielczość, a gdy jej nie ma – standardowa.
    /// </summary>
    public static async Task<byte[]> Orto(double minX, double minY, double maxX, double maxY, int w, int h, CancellationToken ct, bool std = false)
    {
        Exception last = null;
        for (int attempt = 1; attempt <= 6; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var svc in std ? new[] { OrtoStd } : new[] { OrtoHigh, OrtoStd })
            {
                try
                {
                    var data = await GetFresh(WmsUrl(svc, minX, minY, maxX, maxY, w, h), ct);
                    BitmapSource img;
                    try { img = Decode(data); }
                    catch { last = new InvalidOperationException("Geoportal nie zwrócił obrazu (poza zasięgiem albo przeciążony)"); continue; }
                    if (img.PixelWidth != w || img.PixelHeight != h) { last = new InvalidOperationException($"Geoportal zwrócił {img.PixelWidth}×{img.PixelHeight} px zamiast {w}×{h}"); continue; }
                    // pusty obraz z wadliwego serwera – nowa próba nowym połączeniem
                    if (IsBlank(data)) { last = new InvalidOperationException("Geoportal zwrócił pusty obraz"); break; }
                    return data;
                }
                catch (Exception ex) when (ex is not OperationCanceledException) { last = ex; break; }   // błąd połączenia – nowa próba od początku
            }
            await Task.Delay(400 * attempt, ct);
        }
        throw last ?? new InvalidOperationException("Nie udało się pobrać ortofotomapy");
    }

    public record GridResult(List<UtTile> Ok, List<string> Failed, bool Cancelled = false);

    /// <summary>
    /// Kafelki siatki do katalogu (pliki r{wiersz}_c{kolumna}.jpg; już pobrane – pomijane). Po 2 naraz.
    /// Nieudany kafelek nie przerywa reszty. Pliki zapisywane przez .tmp.
    /// </summary>
    public static async Task<GridResult> DownloadGrid(UtGrid g, IReadOnlyList<(int Row, int Col)> cells, string dir, IProgress<string> progress, CancellationToken ct)
    {
        Directory.CreateDirectory(dir);
        var ok = new System.Collections.Concurrent.ConcurrentBag<UtTile>(); var failed = new System.Collections.Concurrent.ConcurrentBag<string>();
        int done = 0; bool cancelled = false;
        try
        {
        await Parallel.ForEachAsync(cells, new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = ct }, async (cell, token) =>
        {
            var (r, c) = cell; var file = Path.Combine(dir, $"r{r}_c{c}.jpg");
            try
            {
                if (File.Exists(file) && IsBlankFile(file)) File.Delete(file);   // pusty kafelek z wcześniejszego pobrania
                if (!File.Exists(file))
                {
                    var (minX, minY, maxX, maxY) = g.TileBox(r, c);
                    var data = await Orto(minX, minY, maxX, maxY, g.TileSize, g.TileSize, token, std: g.Mpp >= 0.5);
                    var tmp = file + ".tmp"; await File.WriteAllBytesAsync(tmp, data, token); File.Move(tmp, file, true);
                }
                ok.Add(new UtTile { File = file, W = g.TileSize, H = g.TileSize, Row = r, Col = c, Geo = g.TileGeo(r, c) });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { failed.Add($"r{r}_c{c}: {Reason(ex)}"); }
            progress?.Report($"Ortofotomapa: {Interlocked.Increment(ref done)}/{cells.Count} kafelków" + (failed.IsEmpty ? "" : $", nieudane {failed.Count}"));
        });
        }
        catch (OperationCanceledException) { cancelled = true; }   // pobrane do tej pory zostają w wyniku
        return new GridResult(ok.OrderBy(t => t.Row).ThenBy(t => t.Col).ToList(), failed.OrderBy(f => f).ToList(), cancelled);
    }

    public static string Reason(Exception ex) => ex switch
    {
        HttpRequestException h => "brak połączenia / błąd serwera" + (h.StatusCode != null ? $" ({(int)h.StatusCode})" : ""),
        TaskCanceledException => "serwer nie odpowiedział w 60 s",
        _ => ex.Message
    };

    // ------------------------------------------------------------------ kafelki Web Mercator (podgląd zapasowy)
    public static string TileUrl(string src, int z, int x, int y) => string.Format(CultureInfo.InvariantCulture, src == "osm" ? OsmTiles : src == "orm" ? OrmTiles : EsriTiles, z, x, y);

    /// <summary>Narożnik kafelka (x, y – piksel kafelka × 256) → szerokość, długość geograficzna.</summary>
    public static (double Lat, double Lon) TileCorner(double x, double y, int z)
    {
        double n = 1 << z, lon = x / n * 360 - 180, lat = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * y / n))) * 180 / Math.PI;
        return (lat, lon);
    }
    public static (double X, double Y) TileOf(double lat, double lon, int z)
    {
        double n = 1 << z, r = lat * Math.PI / 180;
        return ((lon + 180) / 360 * n, (1 - Math.Log(Math.Tan(r) + 1 / Math.Cos(r)) / Math.PI) / 2 * n);
    }
    /// <summary>Położenie kafelka 256 px w PUWG 1992 – przekształcenie afiniczne z trzech narożników (w obrębie kafelka błąd ≪ 1 piksel).</summary>
    public static Affine TileGeo(int z, int x, int y)
    {
        XY P(double tx, double ty) { var (la, lo) = TileCorner(tx, ty, z); return Geo.ToPuwg92(la, lo); }
        var o = P(x, y); var u = (P(x + 1, y) - o) * (1.0 / 256); var v = (P(x, y + 1) - o) * (1.0 / 256);
        return new Affine(u.Y, v.Y, o.Y, u.X, v.X, o.X);
    }
}

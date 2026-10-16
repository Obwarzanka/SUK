using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LcsWidzew.Model;

namespace LcsWidzew.UI;

/// <summary>
/// Pobieranie podkładu planu: ortofotomapa (zdjęcia lotnicze, Geoportal GUGiK – WMS w układzie PUWG 1992, EPSG:2180),
/// mapa OpenStreetMap (kafelki) albo dowolny obraz z adresu URL (np. plan schematyczny z Semaforka). Zdjęcia i mapa mają
/// znaną skalę (metry na piksel), więc trafiają na plan od razu w skali Ig-10 (wzdłuż i w poprzek).
/// </summary>
public static class GeoPodklad
{
    static readonly HttpClient http = CreateHttp();
    static HttpClient CreateHttp()
    {
        // bez ciasteczek – Geoportal przypina sesję do jednego serwera za load balancerem (bywa wadliwy i zwraca 404)
        var h = new HttpClient(new SocketsHttpHandler { UseCookies = false, PooledConnectionLifetime = TimeSpan.FromSeconds(30) }) { Timeout = TimeSpan.FromSeconds(60) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd("LcsSymulator/1.0 (symulator szkoleniowy LCS; edytor planu schematycznego)");
        return h;
    }

    public const string OrtoWms = "https://mapy.geoportal.gov.pl/wss/service/PZGIK/ORTO/WMS/StandardResolution";
    /// <summary>Ortofotomapa w pełnej rozdzielczości (ok. 0,1–0,25 m) – znacznie ostrzejsza niż StandardResolution (widać szyny).</summary>
    public const string OrtoWmsHigh = "https://mapy.geoportal.gov.pl/wss/service/PZGIK/ORTO/WMS/HighResolution";
    public const string OsmTiles = "https://tile.openstreetmap.org/{0}/{1}/{2}.png";
    public const string EsriTiles = "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{0}/{2}/{1}";

    /// <summary>Współrzędne geograficzne (WGS84/ETRS89) → PUWG 1992 (EPSG:2180): odwzorowanie Gaussa–Krügera, GRS80, południk 19°, k0 = 0,9993.</summary>
    public static (double E, double N) Puwg92(double lat, double lon)
    {
        double a = 6378137.0, f = 1 / 298.257222101, k0 = 0.9993, lon0 = 19 * Math.PI / 180, fe = 500000, fn = -5300000;
        double n = f / (2 - f), e = Math.Sqrt(f * (2 - f));
        double A = a / (1 + n) * (1 + n * n / 4 + Math.Pow(n, 4) / 64);
        double[] al = { n / 2 - 2 * n * n / 3 + 5 * n * n * n / 16, 13 * n * n / 48 - 3 * n * n * n / 5, 61 * n * n * n / 240 };
        double phi = lat * Math.PI / 180, dl = lon * Math.PI / 180 - lon0;
        double t = Math.Sinh(Math.Atanh(Math.Sin(phi)) - e * Math.Atanh(e * Math.Sin(phi)));
        double xi = Math.Atan2(t, Math.Cos(dl)), eta = Math.Atanh(Math.Sin(dl) / Math.Sqrt(1 + t * t));
        double X = xi, Y = eta;
        for (int j = 0; j < 3; j++) { X += al[j] * Math.Sin(2 * (j + 1) * xi) * Math.Cosh(2 * (j + 1) * eta); Y += al[j] * Math.Cos(2 * (j + 1) * xi) * Math.Sinh(2 * (j + 1) * eta); }
        return (fe + k0 * A * Y, fn + k0 * A * X);
    }

    /// <summary>„51.7631, 19.5438” (Google Maps), „51,7631 19,5438”, „51.7631N 19.5438E”.</summary>
    public static bool TryLatLon(string s, out double lat, out double lon)
    {
        lat = lon = 0;
        var m = Regex.Matches(s ?? "", @"[-+]?\d+(?:[.,]\d+)?");
        if (m.Count < 2) return false;
        bool P(string v, out double d) => double.TryParse(v.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out d);
        return P(m[0].Value, out lat) && P(m[1].Value, out lon) && Math.Abs(lat) <= 85 && Math.Abs(lon) <= 180;
    }

    public record Result(string File, int W, int H, double Mpp, string Attribution, JsonObject Geo);

    static byte[] Get(string url) => http.GetByteArrayAsync(url).GetAwaiter().GetResult();
    /// <summary>Kilka prób (serwery map bywają przeciążone, Geoportal losowo zwraca 404).</summary>
    static byte[] Retry(Func<byte[]> get, int n = 6)
    {
        for (int i = 1; ; i++)
        {
            try { return get(); }
            catch (Exception) when (i < n) { Thread.Sleep(400 * i); }
        }
    }
    /// <summary>Zapytanie nowym połączeniem (Connection: close) – ponowna próba trafia na inny serwer za load balancerem.</summary>
    static byte[] GetFresh(string url)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url); req.Headers.ConnectionClose = true;
        using var resp = http.Send(req, HttpCompletionOption.ResponseContentRead);
        resp.EnsureSuccessStatusCode();
        using var ms = new MemoryStream(); resp.Content.ReadAsStream().CopyTo(ms); return ms.ToArray();
    }
    public static HttpClient Http => http;

    public const string NmtService = "https://services.gugik.gov.pl/nmt/";
    /// <summary>
    /// Wysokości terenu [m n.p.m.] z numerycznego modelu terenu GUGiK (NMT z lotniczego skaningu laserowego, siatka ok. 1 m)
    /// dla punktów PUWG 1992 (E, N). Paczki po 200 punktów (dłuższy adres – błąd 414), każda do 6 prób; NaN – brak danych.
    /// </summary>
    public static double[] Nmt(IReadOnlyList<(double E, double N)> pts, Action<string> progress)
    {
        var res = new double[pts.Count]; const int B = 200;
        for (int i0 = 0; i0 < pts.Count; i0 += B)
        {
            int n = Math.Min(B, pts.Count - i0);
            progress?.Invoke($"Pobieranie wysokości z NMT GUGiK: {i0 + n}/{pts.Count} punktów…");
            var list = string.Join(",", Enumerable.Range(i0, n).Select(i => FormattableString.Invariant($"{pts[i].N:0.##} {pts[i].E:0.##}")));
            var url = NmtService + "?request=GetHByPointList&list=" + Uri.EscapeDataString(list);
            var txt = System.Text.Encoding.UTF8.GetString(Retry(() => GetFresh(url))).Trim();
            var items = txt.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (items.Length != n) throw new InvalidOperationException("Usługa NMT GUGiK zwróciła nieoczekiwaną odpowiedź: " + (txt.Length > 120 ? txt[..120] + "…" : txt));
            for (int k = 0; k < n; k++)
            {
                var f = items[k].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                res[i0 + k] = f.Length >= 3 && double.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var h) && h > -100 && h < 3000 ? h : double.NaN;
            }
        }
        return res;
    }

    public record MosaicResult(string Dir, List<(int X, int Y)> Ok, List<string> Failed);

    /// <summary>
    /// Kafelki mozaiki (Web Mercator z15) do katalogu podklady/&lt;źródło&gt;_z15_&lt;n&gt;: ortofotomapa – WMS Geoportalu w EPSG:3857
    /// dokładnie w granicach kafelka (n × n px), zdjęcia Esri – sklejone z kafelków o 1–2 poziomy dokładniejszych. Już pobrane – pomijane.
    /// Geoportal – po 2 kafelki naraz, Esri – po 4; każdy kafelek do 6 prób (Geoportal losowo zwraca 404 z wadliwego serwera –
    /// każda próba nowym połączeniem); nieudany kafelek nie przerywa reszty – wynik: pobrane i nieudane (z przyczyną).
    /// </summary>
    public static MosaicResult Mosaic(string src, int n, List<(int X, int Y)> tiles, string baseDir, Action<string> progress)
    {
        const int z = WebTiles.PickZ;
        var dir = Path.Combine(baseDir, $"{src}_z{z}_{n}"); Directory.CreateDirectory(dir);
        var ok = new System.Collections.Concurrent.ConcurrentBag<(int X, int Y)>(); var failed = new System.Collections.Concurrent.ConcurrentBag<string>();
        int done = 0;
        Parallel.ForEach(tiles, new ParallelOptions { MaxDegreeOfParallelism = src == "orto" ? 2 : 4 }, t =>
        {
            var (x, y) = t; var file = Path.Combine(dir, $"{x}_{y}.jpg");
            Exception last = null;
            for (int attempt = 1; attempt <= 6 && !File.Exists(file); attempt++)
            {
                try { Tile(src, n, x, y, file); last = null; }
                catch (Exception ex) { last = ex; if (ex is InvalidOperationException { Message: "kafelek poza mapą" }) break; if (attempt < 6) Thread.Sleep(400 * attempt); }
            }
            if (File.Exists(file)) ok.Add(t); else failed.Add($"{x}_{y}: {Reason(last)}");
            progress?.Invoke($"Pobieranie kafelków {Interlocked.Increment(ref done)}/{tiles.Count}…" + (failed.IsEmpty ? "" : $" (nieudane: {failed.Count})"));
        });
        return new MosaicResult(dir, ok.OrderBy(q => q.Y).ThenBy(q => q.X).ToList(), failed.OrderBy(f => f).ToList());
    }

    static string Reason(Exception ex) => ex switch
    {
        null => "?",
        HttpRequestException h => "brak połączenia / błąd serwera" + (h.StatusCode != null ? $" ({(int)h.StatusCode})" : "") + " – " + h.Message,
        TaskCanceledException => "serwer nie odpowiedział w 60 s",
        _ => ex.Message
    };

    /// <summary>Jeden kafelek mozaiki – zapis przez plik tymczasowy (przerwane pobieranie nie zostawia uszkodzonego pliku).</summary>
    static void Tile(string src, int n, int x, int y, string file)
    {
        const int z = WebTiles.PickZ;
        if (x < 0 || y < 0 || x >= 1 << z || y >= 1 << z) throw new InvalidOperationException("kafelek poza mapą");
        if (src == "orto")
        {
            var (a, b, c, d) = WebTiles.Bbox3857(x, y, z);
            string Url(string svc) => FormattableString.Invariant($"{svc}?SERVICE=WMS&REQUEST=GetMap&VERSION=1.3.0&LAYERS=Raster&STYLES=&CRS=EPSG:3857&BBOX={a:0.###},{b:0.###},{c:0.###},{d:0.###}&WIDTH={n}&HEIGHT={n}&FORMAT=image/jpeg");
            var data = GetFresh(Url(OrtoWmsHigh));
            BitmapSource img;
            try { img = Decode(data); }
            catch
            {
                data = GetFresh(Url(OrtoWms));    // brak pełnej rozdzielczości w tym miejscu – wersja standardowa
                try { img = Decode(data); } catch { throw new InvalidOperationException("Geoportal nie zwrócił obrazu (przeciążony albo poza zasięgiem ortofotomapy)"); }
            }
            if (img.PixelWidth != n || img.PixelHeight != n) throw new InvalidOperationException($"Geoportal zwrócił obraz {img.PixelWidth}×{img.PixelHeight} zamiast {n}×{n}");
            var tmp = file + ".tmp"; File.WriteAllBytes(tmp, data); File.Move(tmp, file, true);
        }
        else
        {
            int sub = n / 256, zz = z + (int)Math.Round(Math.Log2(sub));
            var buf = new byte[n * n * 4];
            for (int j = 0; j < sub; j++)
                for (int i = 0; i < sub; i++)
                {
                    var img = Decode(Get(string.Format(CultureInfo.InvariantCulture, EsriTiles, zz, x * sub + i, y * sub + j)));
                    img.CopyPixels(new Int32Rect(0, 0, 256, 256), buf, n * 4, (j * 256 * n + i * 256) * 4);
                }
            Save(buf, n, n, file);
        }
    }

    static BitmapSource Decode(byte[] data)
    {
        var dec = BitmapDecoder.Create(new MemoryStream(data), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var f = new FormatConvertedBitmap(dec.Frames[0], PixelFormats.Bgra32, null, 0); f.Freeze();
        return f;
    }

    static void Save(byte[] buf, int w, int h, string file)
    {
        var bs = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, buf, w * 4);
        var enc = new JpegBitmapEncoder { QualityLevel = 90 }; enc.Frames.Add(BitmapFrame.Create(bs));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var tmp = file + ".tmp";
        using (var fs = File.Create(tmp)) enc.Save(fs);
        File.Move(tmp, file, true);
    }

    static void CheckSize(int w, int h)
    {
        if (w < 16 || h < 16) throw new InvalidOperationException("Za mały obszar albo za mała rozdzielczość");
        if (w > 20000 || h > 20000 || (long)w * h > 60_000_000) throw new InvalidOperationException($"Obraz byłby za duży ({w}×{h} px) – zmniejsz obszar albo zwiększ liczbę metrów na piksel");
    }

    /// <summary>Ortofotomapa GUGiK (WMS, EPSG:2180, osie N, E) – prostokąt w metrach wokół środka, sklejany z kawałków ≤ 2000 px.</summary>
    public static Result Orto(double lat, double lon, double wM, double hM, double mpp, string file, Action<string> progress)
    {
        var (e0, n0) = Puwg92(lat, lon);
        if (e0 < 140000 || e0 > 900000 || n0 < 100000 || n0 > 800000) throw new InvalidOperationException("Ortofotomapa Geoportalu obejmuje tylko Polskę – sprawdź współrzędne");
        int W = (int)Math.Round(wM / mpp), H = (int)Math.Round(hM / mpp); CheckSize(W, H);
        double left = e0 - W * mpp / 2, top = n0 + H * mpp / 2;
        var buf = new byte[(long)W * H * 4];
        const int T = 2000; int nx = (W + T - 1) / T, ny = (H + T - 1) / T, k = 0;
        for (int ty = 0; ty < ny; ty++)
            for (int tx = 0; tx < nx; tx++)
            {
                int px = tx * T, py = ty * T, tw = Math.Min(T, W - px), th = Math.Min(T, H - py);
                double minE = left + px * mpp, maxE = minE + tw * mpp, maxN = top - py * mpp, minN = maxN - th * mpp;
                progress?.Invoke($"Pobieranie ortofotomapy {++k}/{nx * ny}…");
                var url = FormattableString.Invariant($"{OrtoWmsHigh}?SERVICE=WMS&REQUEST=GetMap&VERSION=1.3.0&LAYERS=Raster&STYLES=&CRS=EPSG:2180&BBOX={minN:0.###},{minE:0.###},{maxN:0.###},{maxE:0.###}&WIDTH={tw}&HEIGHT={th}&FORMAT=image/jpeg");
                var img = Decode(Retry(() => GetFresh(url)));
                if (img.PixelWidth != tw || img.PixelHeight != th) throw new InvalidOperationException($"Serwer zwrócił obraz {img.PixelWidth}×{img.PixelHeight} zamiast {tw}×{th}");
                img.CopyPixels(new Int32Rect(0, 0, tw, th), buf, W * 4, (py * W + px) * 4);
            }
        progress?.Invoke("Zapisywanie podkładu…");
        Save(buf, W, H, file);
        var geo = new JsonObject { ["src"] = "orto", ["crs"] = "EPSG:2180", ["lat"] = lat, ["lon"] = lon, ["mpp"] = mpp, ["e0"] = Math.Round(left, 2), ["n0"] = Math.Round(top, 2) };
        return new Result(file, W, H, mpp, "Ortofotomapa: GUGiK, geoportal.gov.pl", geo);
    }

    public static Result Osm(double lat, double lon, double wM, double hM, double mpp, string file, Action<string> progress) =>
        Tiles(OsmTiles, "osm", "mapy", "Mapa: © współtwórcy OpenStreetMap (ODbL)", lat, lon, wM, hM, mpp, file, progress);
    public static Result Esri(double lat, double lon, double wM, double hM, double mpp, string file, Action<string> progress) =>
        Tiles(EsriTiles, "esri", "zdjęć", "Zdjęcia satelitarne: Esri World Imagery (Esri, Maxar, Earthstar Geographics)", lat, lon, wM, hM, mpp, file, progress);

    /// <summary>Podkład z kafelków (Web Mercator, {0}=z, {1}=x, {2}=y) – poziom przybliżenia dobrany do rozdzielczości, najwyżej 400 kafelków.</summary>
    static Result Tiles(string template, string src, string what, string attribution, double lat, double lon, double wM, double hM, double mpp, string file, Action<string> progress)
    {
        double cosl = Math.Cos(lat * Math.PI / 180);
        int z = (int)Math.Clamp(Math.Round(Math.Log2(156543.03392 * cosl / mpp)), 10, 19);
        double n = Math.Pow(2, z), res = 156543.03392 * cosl / n;
        double cx = (lon + 180) / 360 * n * 256, phi = lat * Math.PI / 180, cy = (1 - Math.Log(Math.Tan(phi) + 1 / Math.Cos(phi)) / Math.PI) / 2 * n * 256;
        int W = (int)Math.Round(wM / res), H = (int)Math.Round(hM / res); CheckSize(W, H);
        long x0 = (long)Math.Floor(cx - W / 2.0), y0 = (long)Math.Floor(cy - H / 2.0);
        long tx0 = x0 / 256, ty0 = y0 / 256, tx1 = (x0 + W - 1) / 256, ty1 = (y0 + H - 1) / 256;
        long count = (tx1 - tx0 + 1) * (ty1 - ty0 + 1);
        if (count > 400) throw new InvalidOperationException($"Za dużo kafelków ({count}) – zmniejsz obszar albo zwiększ liczbę metrów na piksel");
        var buf = new byte[(long)W * H * 4]; int k = 0;
        for (long ty = ty0; ty <= ty1; ty++)
            for (long tx = tx0; tx <= tx1; tx++)
            {
                progress?.Invoke($"Pobieranie {what} {++k}/{count}…");
                var img = Decode(Retry(() => Get(string.Format(CultureInfo.InvariantCulture, template, z, tx, ty))));
                // część kafelka w prostokącie wyniku
                long gx0 = Math.Max(tx * 256, x0), gy0 = Math.Max(ty * 256, y0), gx1 = Math.Min(tx * 256 + 256, x0 + W), gy1 = Math.Min(ty * 256 + 256, y0 + H);
                if (gx1 <= gx0 || gy1 <= gy0) continue;
                img.CopyPixels(new Int32Rect((int)(gx0 - tx * 256), (int)(gy0 - ty * 256), (int)(gx1 - gx0), (int)(gy1 - gy0)), buf, W * 4, (int)(((gy0 - y0) * W + (gx0 - x0)) * 4));
            }
        progress?.Invoke("Zapisywanie podkładu…");
        Save(buf, W, H, file);
        var geo = new JsonObject { ["src"] = src, ["zoom"] = z, ["lat"] = lat, ["lon"] = lon, ["mpp"] = Math.Round(res, 5) };
        return new Result(file, W, H, res, attribution, geo);
    }

    /// <summary>Obraz z adresu URL (np. plan schematyczny z Semaforka) – zapis do katalogu podkładów; skalę ustawia się w edytorze.</summary>
    public static string Url(string url, string dir, Action<string> progress)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var u) || (u.Scheme != "http" && u.Scheme != "https")) throw new InvalidOperationException("Podaj pełny adres obrazu (https://…)");
        progress?.Invoke("Pobieranie obrazu…");
        var data = Retry(() => Get(u.AbsoluteUri), 3);
        BitmapSource img;
        try { img = Decode(data); }
        catch { throw new InvalidOperationException("Pod tym adresem nie ma obrazu (to strona?) – na obrazie PPM → „Kopiuj adres obrazu” i wklej ten adres"); }
        var name = Path.GetFileName(u.LocalPath);
        name = string.Concat((string.IsNullOrWhiteSpace(name) ? "obraz" : name).Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        if (!Regex.IsMatch(name, @"\.(png|jpe?g|gif|bmp|tiff?|webp)$", RegexOptions.IgnoreCase)) name += ".png";
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, name);
        File.WriteAllBytes(file, data);
        progress?.Invoke($"Pobrano {name} ({img.PixelWidth}×{img.PixelHeight})");
        return file;
    }
}

public partial class EditorWindow
{
    bool bgBusy;
    string BgDir => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".", "podklady");
    static readonly string[] BgSources =
    {
        "Ortofotomapa – zdjęcia lotnicze (Geoportal, GUGiK)",
        "Mapa OpenStreetMap",
        "Zdjęcia satelitarne (Esri World Imagery)",
        "Plan z Semaforka albo inny obraz z adresu internetowego (URL)",
        "Plik z dysku (plan Semaforka, skan, zdjęcie)",
    };

    /// <summary>Nowy podkład: wybór źródła, pobranie, ustawienie na planie (zdjęcia i mapa – od razu w skali planu).</summary>
    async void BgNewDialog()
    {
        if (FuzzMode || NoPrompt || bgBusy) return;   // testy bez okna – bez sieci i okien
        var r = Ask("Podkład planu", ("Źródło", string.Join("|", BgSources)));
        if (r == null) return;
        int src = Array.IndexOf(BgSources, r[0]);
        if (src == 4)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Obrazy|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.webp|Wszystkie|*.*" };
            if (dlg.ShowDialog(this) == true) Safe(() => SetBackground(dlg.FileName));
            return;
        }
        if (src == 3)
        {
            var u = Ask("Obraz z internetu", ("Adres obrazu (np. plan stacji z Semaforka: PPM na obrazie → „Kopiuj adres obrazu”)", "https://"));
            if (u == null) return;
            await RunBg(() => { var f = GeoPodklad.Url(u[0], BgDir, Progress); return () => SetBackground(f); });
            return;
        }
        var g = Ask(src == 0 ? "Ortofotomapa (Geoportal)" : src == 1 ? "Mapa OpenStreetMap" : "Zdjęcia satelitarne (Esri)",
            ("Środek obszaru: szerokość, długość geograficzna (np. z Google Maps: PPM na mapie → współrzędne, „52.2297, 21.0122”)", LastLatLon()),
            ("Szerokość obszaru – wschód–zachód [m]", "3000"), ("Wysokość obszaru – północ–południe [m]", "1000"),
            ("Rozdzielczość [m na piksel] (ortofotomapa 0,25–1; mapa i zdjęcia satelitarne ok. 0,3–0,6)", src == 0 ? "0.5" : src == 1 ? "0.6" : "0.3"));
        if (g == null) return;
        if (!GeoPodklad.TryLatLon(g[0], out var lat, out var lon)) { status.Text = "✗ Nie rozpoznano współrzędnych – wpisz np. 51.7631, 19.5438"; return; }
        double Num2(string s, double d) => double.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : d;
        double wM = Num2(g[1], 3000), hM = Num2(g[2], 1000), mpp = Math.Clamp(Num2(g[3], 0.5), 0.1, 20);
        var file = Path.Combine(BgDir, FormattableString.Invariant($"{(src == 0 ? "orto" : src == 1 ? "osm" : "esri")}_{lat:0.0000}_{lon:0.0000}_{wM:0}x{hM:0}.jpg"));
        await RunBg(() =>
        {
            var res = src == 0 ? GeoPodklad.Orto(lat, lon, wM, hM, mpp, file, Progress) : src == 1 ? GeoPodklad.Osm(lat, lon, wM, hM, mpp, file, Progress) : GeoPodklad.Esri(lat, lon, wM, hM, mpp, file, Progress);
            return () => ApplyGeoBackground(res);
        });
    }

    /// <summary>Środek poprzedniego podkładu geograficznego (żeby nie wpisywać drugi raz).</summary>
    string LastLatLon() => Bg?["geo"] is JsonObject g && g["lat"] != null && g["lon"] != null ? FormattableString.Invariant($"{StationDoc.D(g["lat"]):0.######}, {StationDoc.D(g["lon"]):0.######}") : "";

    void Progress(string msg) => Dispatcher.BeginInvoke(() => status.Text = msg);

    /// <summary>Pobieranie w tle (okno nie zamarza); wynik – na wątku okna.</summary>
    async Task RunBg(Func<Action> work)
    {
        bgBusy = true; Cursor = System.Windows.Input.Cursors.Wait;
        try { var done = await Task.Run(work); Safe(done); }
        catch (Exception ex) { status.Text = "✗ Podkład: " + (ex is HttpRequestException or TaskCanceledException ? "brak połączenia z serwerem (" + ex.Message + ") – spróbuj później albo wybierz inne źródło (np. zdjęcia satelitarne Esri)" : ex.Message); }
        finally { bgBusy = false; Cursor = null; }
    }

    /// <summary>Podkład o znanej skali (zdjęcie lotnicze, mapa): szerokość wg skali podłużnej planu, rozciągnięcie pionowe wg poprzecznej.</summary>
    void ApplyGeoBackground(GeoPodklad.Result res)
    {
        bgCache.Remove(Path.GetFullPath(res.File));
        var img = BgImage(Path.GetFullPath(res.File)); if (img == null) { status.Text = "✗ Nie można wczytać pobranego obrazu"; return; }
        // pusty plan w skali Ig-10 (inna poprzeczna) – od razu skala rzeczywista, żeby zdjęcie nie było zniekształcone
        string note = "";
        doc.Snapshot();
        if (doc.Nodes.Count == 0 && !doc.RealScale) { doc.SetPlanScale(doc.ScaleAlong, doc.ScaleAlong, true); note = $" Plan pusty – ustawiono skalę rzeczywistą 1:{doc.ScaleAlong:0} w obu kierunkach."; }
        // podkład dokładnie w skali planu: wzdłuż wg skali podłużnej, w poprzek wg poprzecznej (w skali rzeczywistej – bez zniekształceń)
        double w = res.W * res.Mpp * doc.Ux, h = res.H * res.Mpp * doc.Uy;
        var v = panel.ViewWorld;
        Point c = st != null && st.Nodes.Count > 0 && !v.IsEmpty ? new Point(v.X + v.Width / 2, v.Y + v.Height / 2) : new Point(0, 0);
        var bg = new JsonObject
        {
            ["file"] = BgRel(res.File), ["x"] = Math.Round(c.X - w / 2, 2), ["y"] = Math.Round(c.Y - h / 2, 2), ["w"] = Math.Round(w, 2),
            ["sy"] = Math.Round(doc.Uy / doc.Ux, 5), ["rot"] = 0, ["opacity"] = 0.85, ["attribution"] = res.Attribution, ["geo"] = res.Geo,
        };
        doc.PlanBgs.Add(bg); bgActive = doc.PlanBgs.Count - 1;
        if (Math.Abs(doc.Uy / doc.Ux - 1) < 1e-6) bg.Remove("sy");
        Changed($"Podkład w skali planu 1:{doc.ScaleAlong:0} / 1:{doc.ScaleAcross:0}: {res.W}×{res.H} px, 1 px = {res.Mpp:0.###} m ({res.W * res.Mpp:0} × {res.H * res.Mpp:0} m).{note}" +
            (doc.RealScale ? " Bez zniekształceń – rysuj tory po zdjęciu, długości wyjdą rzeczywiste." : $" Plan ma skalę poprzeczną 1:{doc.ScaleAcross:0}, więc zdjęcie jest rozciągnięte ×{doc.Uy / doc.Ux:0.#} w pionie – dla zdjęcia bez zniekształceń wybierz skalę rzeczywistą.") + $" Obróć: „1. Wyrównaj tor do poziomu”. {res.Attribution}");
        if (!FuzzMode) panel.FitAll();
    }

    string BgRel(string file)
    {
        var dir = Path.GetFullPath(Path.GetDirectoryName(path) ?? ".");
        var full = Path.GetFullPath(file);
        return full.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? Path.GetRelativePath(dir, full) : full;
    }

    /// <summary>Test (--editshot … podklad orto|osm "lat, lon" szer wys out.json): pobranie, skala na planie, zapis.</summary>
    public string BgGeoForTest(string src, string latlon, double wM, double hM, string outJson)
    {
        var log = new System.Text.StringBuilder(); FuzzMode = true; int bad = 0;
        void Ok(bool c, string m) { log.AppendLine((c ? "OK  " : "BŁĄD") + " " + m); if (!c) bad++; }
        PlanForTest();
        Ok(GeoPodklad.TryLatLon(latlon, out var lat, out var lon), $"współrzędne {lat}, {lon}");
        var (e, n) = GeoPodklad.Puwg92(52.2297, 21.0122);
        log.AppendLine($"kontrola PUWG 1992 (Warszawa 52.2297, 21.0122): E = {e:0.0}, N = {n:0.0}");
        var file = Path.Combine(BgDir, $"test_{src}.jpg");
        var res = Task.Run(() => src == "osm" ? GeoPodklad.Osm(lat, lon, wM, hM, 0.6, file, null) : src == "esri" ? GeoPodklad.Esri(lat, lon, wM, hM, 0.3, file, null) : GeoPodklad.Orto(lat, lon, wM, hM, 0.5, file, null)).GetAwaiter().GetResult();
        Ok(File.Exists(res.File), $"pobrano {res.W}×{res.H} px, {res.Mpp:0.###} m/px → {res.File}");
        ApplyGeoBackground(res);
        var bi = BgPx(Bg); var t = BgXf(Bg, bi);
        double mW = (BgToWorld(t, new Point(bi.PixelWidth, 0)).X - BgToWorld(t, new Point(0, 0)).X) / doc.Ux;
        double mH = (BgToWorld(t, new Point(0, bi.PixelHeight)).Y - BgToWorld(t, new Point(0, 0)).Y) / doc.Uy;
        Ok(Math.Abs(mW - res.W * res.Mpp) < 0.5 && Math.Abs(mH - res.H * res.Mpp) < 0.5, $"na planie: {mW:0.#} m × {mH:0.#} m w skali 1:{doc.ScaleAlong:0} / 1:{doc.ScaleAcross:0} (rozciągnięcie pionowe {t.Sy})");
        doc.Save(outJson);
        Ok((StationDoc.Load(outJson).Root["planBgs"] as JsonArray)?.LastOrDefault()?["geo"]?["mpp"] != null, "zapis podkładu z georeferencją");
        // zrzut: środek podkładu, ok. 1200 m szerokości widoku
        var mid = BgToWorld(t, new Point(bi.PixelWidth / 2.0, bi.PixelHeight / 2.0));
        ZoomForTest(mid.X, mid.Y, Math.Max(0.05, panel.ActualWidth / (1200 * doc.Ux)));
        log.AppendLine($"błędów testu: {bad}, błędów edytora: {Errors.Count}");
        return log.ToString();
    }
}

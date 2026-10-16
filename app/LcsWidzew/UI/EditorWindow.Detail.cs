using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LcsWidzew.Model;

namespace LcsWidzew.UI;

/// <summary>
/// Ostrzejszy podkład przy przybliżeniu: gdy kafelek mozaiki jest na ekranie większy niż jego rozdzielczość, edytor w tle
/// dociąga ten kafelek w wyższej rozdzielczości (ortofotomapa do 4096 px ≈ 0,18 m/px, Esri do 2048 px) do katalogu
/// &lt;źródło&gt;_z15_&lt;n&gt; obok mozaiki i rysuje najlepszy dostępny. Pobierane są tylko kafelki oglądane z bliska.
/// Mozaika na planie (położenie, skala, obrót) się nie zmienia – ten sam kafelek, tylko więcej pikseli.
/// </summary>
public partial class EditorWindow
{
    static bool autoDetail = true;
    /// <summary>Test: dociąganie także w trybie testowym (inaczej wyłączone w testach).</summary>
    bool detailTest;
    static readonly Dictionary<string, bool> fileSeen = new();
    record DetailJob(string Src, int N, int X, int Y, string BaseDir);
    static readonly List<DetailJob> detailQ = new();
    static readonly HashSet<string> detailKnown = new();
    static bool detailRunning;
    static int detailOk, detailBad;

    static int MaxDetail(string src) => src == "orto" ? 4096 : src == "esri" ? 2048 : 0;
    bool Have(string f) { lock (fileSeen) { if (!fileSeen.TryGetValue(f, out var b)) fileSeen[f] = b = File.Exists(f); return b; } }

    /// <summary>Katalog tej samej mozaiki w innej rozdzielczości (…_z15_1024 → …_z15_4096); null – nazwa nietypowa.</summary>
    string TileDirN(JsonObject bg, int n)
    {
        var d = StationDoc.Sv(bg["dir"]) ?? "";
        var full = Path.IsPathRooted(d) ? d : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path) ?? ".", d));
        full = full.TrimEnd('\\', '/');
        return Regex.IsMatch(full, @"_z\d+_\d+$") ? Regex.Replace(full, @"_\d+$", "_" + n) : null;
    }

    /// <summary>Najlepszy dostępny obraz kafelka dla żądanej szerokości na ekranie; brak lepszego – zlecenie pobrania w tle.</summary>
    BitmapSource TileBest(JsonObject bg, int x, int y, int want, int npx)
    {
        string src = StationDoc.Sv(bg["src"]) ?? ""; int max = MaxDetail(src);
        string file = TileFile(bg, x, y); int have = npx;
        foreach (var r in new[] { 2048, 4096 })
        {
            if (r <= npx || r > max || TileDirN(bg, r) is not { } dn) continue;
            var f = Path.Combine(dn, $"{x}_{y}.jpg");
            if (Have(f)) { file = f; have = r; if (r >= want) break; }
        }
        if (want > have && have < max && autoDetail && (detailTest || !(FuzzMode || NoPrompt)) && TileDirN(bg, max) is { } top)
            QueueDetail(new DetailJob(src, Math.Min(max, Math.Max(2048, want)), x, y, Path.GetDirectoryName(top)));
        return TileImg(file, Math.Min(want, have));
    }

    void QueueDetail(DetailJob j)
    {
        lock (detailQ)
        {
            if (!detailKnown.Add($"{j.Src}|{j.N}|{j.X}|{j.Y}|{j.BaseDir}")) return;
            detailQ.Add(j);
            // najwyżej 40 w kolejce – najstarsze (już niewidoczne) odpadają i mogą wrócić później
            while (detailQ.Count > 40) { var o = detailQ[0]; detailQ.RemoveAt(0); detailKnown.Remove($"{o.Src}|{o.N}|{o.X}|{o.Y}|{o.BaseDir}"); }
            if (detailRunning || detailTest) return;
            detailRunning = true;
        }
        Task.Run(() => { while (DetailStep()) { } }).ContinueWith(_ => { lock (detailQ) detailRunning = false; });
    }

    /// <summary>Jedna paczka (do 2 kafelków, najnowsze zlecenia – to, co teraz widać). False – kolejka pusta.</summary>
    bool DetailStep()
    {
        List<DetailJob> batch;
        lock (detailQ)
        {
            if (detailQ.Count == 0) return false;
            var last = detailQ[^1];
            batch = detailQ.Where(q => q.Src == last.Src && q.N == last.N && q.BaseDir == last.BaseDir).TakeLast(2).ToList();
            foreach (var b in batch) detailQ.Remove(b);
        }
        var j0 = batch[0];
        try
        {
            var res = GeoPodklad.Mosaic(j0.Src, j0.N, batch.Select(b => (b.X, b.Y)).ToList(), j0.BaseDir, null);
            lock (fileSeen) foreach (var (x, y) in res.Ok) fileSeen[Path.Combine(res.Dir, $"{x}_{y}.jpg")] = true;
            Interlocked.Add(ref detailOk, res.Ok.Count); Interlocked.Add(ref detailBad, res.Failed.Count);
            foreach (var f in res.Failed) App.LogError(new Exception(f), "podkład – ostrzejszy kafelek");
        }
        catch (Exception ex) { Interlocked.Add(ref detailBad, batch.Count); App.LogError(ex, "podkład – ostrzejszy kafelek"); }
        int left; lock (detailQ) left = detailQ.Count;
        if (!detailTest)
            Dispatcher.BeginInvoke(() =>
            {
                panel.InvalidateVisual();
                status.Text = $"Podkład: ostrzejsze kafelki ({j0.N} px) – pobrane {detailOk}{(detailBad > 0 ? $", nieudane {detailBad}" : "")}{(left > 0 ? $", w kolejce {left}" : "")}";
            });
        return true;
    }

    /// <summary>Panel planu: przełącznik dociągania ostrzejszych kafelków.</summary>
    void DetailPanel()
    {
        if (Bgs?.OfType<JsonObject>().Any(o => IsTiles(o) && MaxDetail(StationDoc.Sv(o["src"]) ?? "") > (int)Num(o["n"], 1024)) != true) return;
        var cb = new CheckBox { Content = "Ostrzejszy podkład przy przybliżeniu", IsChecked = autoDetail, Foreground = Brushes.LightGray, Margin = new Thickness(0, 4, 0, 2),
            ToolTip = "Przy przybliżeniu edytor sam dociąga oglądane kafelki w wyższej rozdzielczości (ortofotomapa do 4096 px ≈ 0,18 m/px, zdjęcia Esri do 2048 px) – tylko te, które widać z bliska. Pliki obok mozaiki (katalog …_z15_2048 / _4096). Położenie i skala podkładu bez zmian." };
        cb.Click += (a, e) => { autoDetail = cb.IsChecked == true; panel.InvalidateVisual(); };
        props.Children.Add(cb);
    }

    /// <summary>Test (--editshot … ostrosc "lat, lon" out.json): mozaika 1024 px, przybliżenie – dociągnięcie kafelka 4096 px, skala bez zmian.</summary>
    public string DetailForTest(string latlon, string outJson)
    {
        var log = new System.Text.StringBuilder(); FuzzMode = true; detailTest = true; int bad = 0;
        void Ok(bool c, string m) { log.AppendLine((c ? "OK  " : "BŁĄD") + " " + m); if (!c) bad++; }
        doc = StationDoc.New("TD", "Ostrość (test)"); path = Path.Combine(Path.GetTempPath(), "lcs_ostrosc", "ostrosc.json"); Directory.CreateDirectory(Path.GetDirectoryName(path)); dispMode = false; Rebuild(true);
        PlanForTest();
        GeoPodklad.TryLatLon(latlon, out var lat, out var lon);
        var (gx, gy) = WebTiles.LatLonToPx(lat, lon, WebTiles.PickZ); int cx = (int)(gx / 256), cy = (int)(gy / 256);
        var m = Task.Run(() => GeoPodklad.Mosaic("orto", 1024, new List<(int, int)> { (cx, cy), (cx + 1, cy) }, BgDir, null)).GetAwaiter().GetResult();
        Ok(m.Ok.Count == 2, $"mozaika 1024 px: {m.Ok.Count} kafelki" + (m.Failed.Count > 0 ? " – " + m.Failed[0] : ""));
        AddTiles("orto", 1024, m.Ok, m.Dir, false);
        var bg = Bg; var t0 = BgXf(bg, BgPx(bg)); var scale0 = (doc.ScaleAlong, doc.ScaleAcross); var bg0 = bg.ToJsonString();
        var img1 = TileBest(bg, cx, cy, 4096, 1024);
        Ok(img1 != null && img1.PixelWidth == 1024, $"przed dociągnięciem: {img1?.PixelWidth} px (najlepszy dostępny)");
        int queued; lock (detailQ) queued = detailQ.Count; Ok(queued == 1, $"zlecone dociągnięcie: {queued} kafelek");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Task.Run(() => { while (DetailStep()) { } }).GetAwaiter().GetResult();
        var img2 = TileBest(bg, cx, cy, 4096, 1024);
        Ok(img2 != null && img2.PixelWidth == 4096, $"po dociągnięciu: {img2?.PixelWidth} px w {sw.Elapsed.TotalSeconds:0.#} s (ok. {WebTiles.GroundMpp(cy, 15, 4096):0.##} m/px)");
        Ok(TileBest(bg, cx, cy, 512, 1024)?.PixelWidth == 512, "oddalenie – mniejszy poziom szczegółowości");
        Ok(bg.ToJsonString() == bg0 && (doc.ScaleAlong, doc.ScaleAcross) == scale0 && BgXf(bg, BgPx(bg)) == t0, "podkład i skala planu bez zmian");
        lock (detailQ) queued = detailQ.Count; Ok(queued == 0, "kolejka pusta");
        var (px, py) = WebTiles.LatLonToPx(lat, lon, 15); var (x0, y0, _, _) = TileSpan(bg);
        var c = BgToWorld(t0, new Point((px - x0 * 256) * 4, (py - y0 * 256) * 4));
        ZoomForTest(c.X, c.Y, panel.ActualWidth / (120 * doc.Ux));
        doc.Save(outJson);
        log.AppendLine($"błędów testu: {bad}, błędów edytora: {Errors.Count}");
        return log.ToString();
    }
}

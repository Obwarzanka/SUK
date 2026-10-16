using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LcsWidzew.Model;

namespace LcsWidzew.UI;

/// <summary>
/// Profil wysokościowy (podłużny) toru albo trasy zmierzonej miarką: punkty co kilka metrów → współrzędne geograficzne
/// z podkładu (mozaika kafelków / obszar ortofotomapy / mapa) → wysokości terenu z NMT GUGiK → odcinki o stałym pochyleniu [‰].
/// Pochylenia można zapisać w torach ciągu (pola `poch`, `hA`, `hB`, `prof` – dane pomocnicze planu).
/// </summary>
public partial class EditorWindow
{
    /// <summary>Punkt planu → piksel podkładu (odwrotność BgToWorld).</summary>
    static Point WorldToBgPx(BgT t, Point w)
    {
        var v = w - t.O; v.Y /= t.Sy;
        double r = -t.Rot * Math.PI / 180, x = v.X * Math.Cos(r) - v.Y * Math.Sin(r), y = v.X * Math.Sin(r) + v.Y * Math.Cos(r);
        return new Point(x / t.S, y / t.S);
    }

    /// <summary>Punkt planu → PUWG 1992 (E, N) przez podkład z położeniem geograficznym (od wierzchu); null – poza podkładami.</summary>
    (double E, double N)? WorldToPuwg(Point w)
    {
        if (Bgs is not { } all) return null;
        foreach (var bg in all.OfType<JsonObject>().Reverse())
        {
            if (BgPx(bg) is not { } sz) continue;
            var px = WorldToBgPx(BgXf(bg, sz), w);
            if (px.X < 0 || px.Y < 0 || px.X > sz.PixelWidth || px.Y > sz.PixelHeight) continue;
            if (IsTiles(bg))
            {
                var (x0, y0, _, _) = TileSpan(bg); double nn = Num(bg["n"], 1024) / 256;
                int tx = x0 + (int)(px.X / Num(bg["n"], 1024)), ty = y0 + (int)(px.Y / Num(bg["n"], 1024));
                if (!Tiles(bg).Contains((tx, ty))) continue;
                var (lat, lon) = WebTiles.PxToLatLon(px.X / nn + x0 * 256, px.Y / nn + y0 * 256, Num(bg["z"], 15));
                return GeoPodklad.Puwg92(lat, lon);
            }
            if (bg["geo"] is not JsonObject geo) continue;
            var src = StationDoc.Sv(geo["src"]);
            if (src == "orto" && geo["e0"] != null) return (Num(geo["e0"], 0) + px.X * Num(geo["mpp"], 1), Num(geo["n0"], 0) - px.Y * Num(geo["mpp"], 1));
            if (src is "osm" or "esri" && geo["zoom"] != null)
            {
                double z = Num(geo["zoom"], 17); var (cx, cy) = WebTiles.LatLonToPx(Num(geo["lat"], 0), Num(geo["lon"], 0), z);
                double gx0 = Math.Floor(cx - sz.PixelWidth / 2.0), gy0 = Math.Floor(cy - sz.PixelHeight / 2.0);
                var (lat, lon) = WebTiles.PxToLatLon(gx0 + px.X, gy0 + px.Y, z);
                return GeoPodklad.Puwg92(lat, lon);
            }
        }
        return null;
    }

    /// <summary>Ciąg torów przez węzły bez zwrotnic (jak tor równoległy), w kolejności; Rev – tor przechodzony od B do A.</summary>
    List<(string Id, bool Rev)> RunEdges(string eid)
    {
        var e0 = st.Edges[eid]; var res = new List<(string Id, bool Rev)> { (eid, false) };
        bool Pass(string n) => st.Nodes[n].Edges.Count == 2 && st.Nodes[n].Switch == null;
        foreach (bool fwd in new[] { true, false })
        {
            string node = fwd ? e0.B : e0.A, edge = eid;
            for (int g = 0; g < 500 && Pass(node); g++)
            {
                var nx = st.Nodes[node].Edges.First(x => x != edge); if (nx == eid) break; var ne = st.Edges[nx]; if (ne.Link) break;
                if (fwd) res.Add((nx, ne.B == node)); else res.Insert(0, (nx, ne.A == node));
                node = ne.A == node ? ne.B : ne.A; edge = nx;
            }
        }
        return res;
    }

    List<(double X, double Y)> EdgesPts(List<(string Id, bool Rev)> run, List<(string Id, bool Rev, double S0, double S1)> ranges)
    {
        var pts = new List<(double X, double Y)>(); double s = 0;
        foreach (var (id, rev) in run)
        {
            var p = st.Edges[id].Pts.ToList(); if (rev) p.Reverse();
            double l = doc.PlanLen(p);
            ranges?.Add((id, rev, s, s + l)); s += l;
            pts.AddRange(pts.Count > 0 ? p.Skip(1) : p);
        }
        return pts;
    }

    /// <summary>Profil terenu wzdłuż łamanej planu: (s [m], h [m n.p.m.]); punkty bez danych pominięte. Synchronicznie (sieć).</summary>
    List<(double S, double H)> ProfileCompute(List<(double X, double Y)> pts, Action<string> progress, out string err)
    {
        err = null;
        double L = doc.PlanLen(pts);
        if (L < 1) { err = "Trasa za krótka"; return null; }
        double step = Math.Max(2, Math.Round(L / 1500, 1));      // NMT ma siatkę ok. 1 m; najwyżej ok. 1500 punktów
        var smp = Profil.Sample(pts, (a, b) => doc.PlanLen(a.X, a.Y, b.X, b.Y), step);
        var geo = new List<(double E, double N)>();
        foreach (var q in smp)
        {
            if (WorldToPuwg(new Point(q.X, q.Y)) is not { } g) { err = $"Punkt trasy w {q.S:0} m leży poza podkładem z położeniem geograficznym (mozaika kafelków z mapki, obszar ortofotomapy albo mapy). Profil liczy się tylko na podkładzie."; return null; }
            if (g.E < 140000 || g.E > 900000 || g.N < 100000 || g.N > 800000) { err = "Trasa poza Polską – NMT GUGiK obejmuje tylko Polskę"; return null; }
            geo.Add(g);
        }
        var h = GeoPodklad.Nmt(geo, progress);
        var res = new List<(double S, double H)>();
        for (int i = 0; i < smp.Count; i++) if (!double.IsNaN(h[i])) res.Add((smp[i].S, h[i]));
        if (res.Count < 2) { err = "Usługa NMT nie zwróciła wysokości dla tej trasy"; return null; }
        return res;
    }

    /// <summary>Profil: trasa (punkty planu) i opcjonalnie tory ciągu (do zapisu pochyleń). Pobieranie w tle, potem okno profilu.</summary>
    void ProfileStart(List<(double X, double Y)> pts, List<(string Id, bool Rev, double S0, double S1)> ranges, string what)
    {
        if (!panel.PlanView || dispMode) { status.Text = "Profil wysokościowy – w trybie planu schematycznego"; return; }
        if (WorldToPuwg(new Point(pts[0].X, pts[0].Y)) == null) { status.Text = "✗ Profil: trasa musi leżeć na podkładzie z położeniem geograficznym (kafelki ortofotomapy z mapki, obszar ortofotomapy / mapy)"; return; }
        status.Text = "Profil wysokościowy: pobieranie…";
        Task.Run(() => { var r = ProfileCompute(pts, m => Dispatcher.BeginInvoke(() => status.Text = m), out var err); return (r, err); })
            .ContinueWith(t => Dispatcher.BeginInvoke(() => Safe(() =>
            {
                if (t.Exception != null) { status.Text = "✗ Profil: " + (t.Exception.InnerException?.Message ?? t.Exception.Message); App.LogError(t.Exception, "profil wysokościowy"); return; }
                var (raw, err) = t.Result;
                if (raw == null) { status.Text = "✗ Profil: " + err; return; }
                status.Text = $"Profil wysokościowy: {raw.Count} punktów, {raw[^1].S:0} m";
                var w = new ProfileWindow(what, raw, ranges != null ? v => ProfileSave(ranges, v) : null) { Owner = this };
                w.Show();
            })));
    }

    /// <summary>Zapis pochyleń w torach ciągu: `poch` [‰ od A do B], `hA`, `hB` [m n.p.m.], `prof` – załomy wewnątrz toru [[m od A, h]].</summary>
    string ProfileSave(List<(string Id, bool Rev, double S0, double S1)> ranges, List<(double S, double H)> v)
    {
        doc.Snapshot(); int n = 0;
        foreach (var (id, rev, s0, s1) in ranges)
        {
            if (doc.Edge(id) is not { } e) continue;
            double lg = s1 - s0; if (lg < 0.01) continue;
            double ha = Profil.At(v, rev ? s1 : s0), hb = Profil.At(v, rev ? s0 : s1);
            e["hA"] = Math.Round(ha, 2); e["hB"] = Math.Round(hb, 2); e["poch"] = Math.Round((hb - ha) / lg * 1000, 1);
            var inner = v.Where(q => q.S > s0 + 0.5 && q.S < s1 - 0.5).Select(q => (D: rev ? s1 - q.S : q.S - s0, q.H)).OrderBy(q => q.D).ToList();
            if (inner.Count > 0)
                e["prof"] = new JsonArray(new[] { (D: 0.0, H: ha) }.Concat(inner).Append((D: lg, H: hb)).Select(q => (JsonNode)new JsonArray(Math.Round(q.D, 1), Math.Round(q.H, 2))).ToArray());
            else e.Remove("prof");
            n++;
        }
        var msg = $"Zapisano pochylenia w {n} torach (warstwa planu „pochylenia ‰”, podpowiedź toru)";
        Changed(msg);
        return msg;
    }

    /// <summary>Przycisk w panelu toru.</summary>
    void ProfileButton(string eid)
    {
        if (!panel.PlanView || Bgs is not { Count: > 0 }) return;
        props.Children.Add(Btn("Profil wysokościowy toru (NMT GUGiK)…", (a, ev) => Safe(() =>
        {
            var ranges = new List<(string Id, bool Rev, double S0, double S1)>();
            var pts = EdgesPts(RunEdges(eid), ranges);
            ProfileStart(pts, ranges, $"ciąg toru {eid} ({ranges.Count} {(ranges.Count == 1 ? "tor" : "torów")}, do zwrotnic / końców)");
        })));
        var e = doc.Edge(eid);
        if (e?["poch"] != null) props.Children.Add(T($"Pochylenie: {Num(e["poch"], 0):+0.#;-0.#;0}‰ (od A do B), wysokość {Num(e["hA"], 0):0.##} → {Num(e["hB"], 0):0.##} m n.p.m.", 10.5, Brushes.LightGray));
    }

    /// <summary>Test (--editshot … profil "lat, lon" out.json): tor po mozaice, wysokości z NMT, uproszczenie, zapis pochyleń, skala bez zmian.</summary>
    public string ProfileForTest(string latlon, string outJson)
    {
        var log = new System.Text.StringBuilder(); FuzzMode = true; int bad = 0;
        void Ok(bool c, string m) { log.AppendLine((c ? "OK  " : "BŁĄD") + " " + m); if (!c) bad++; }
        doc = StationDoc.New("TP", "Profil (test)"); path = Path.Combine(Path.GetTempPath(), "lcs_profil", "profil.json"); Directory.CreateDirectory(Path.GetDirectoryName(path)); dispMode = false; Rebuild(true);
        PlanForTest();
        GeoPodklad.TryLatLon(latlon, out var lat, out var lon);
        var (gx, gy) = WebTiles.LatLonToPx(lat, lon, WebTiles.PickZ); int cx = (int)(gx / 256), cy = (int)(gy / 256);
        var m = Task.Run(() => GeoPodklad.Mosaic("orto", 1024, new List<(int, int)> { (cx - 1, cy), (cx, cy), (cx + 1, cy) }, BgDir, null)).GetAwaiter().GetResult();
        Ok(m.Ok.Count == 3, $"mozaika: {m.Ok.Count} kafelki");
        AddTiles("orto", 1024, m.Ok, m.Dir, false);
        var bg = Bg; var t = BgXf(bg, BgPx(bg)); var scale0 = (doc.ScaleAlong, doc.ScaleAcross);
        // punkt geograficzny → plan → PUWG: zgodność z bezpośrednim przeliczeniem
        var (px, py) = WebTiles.LatLonToPx(lat, lon, 15); var (x0, y0, _, _) = TileSpan(bg);
        var c = BgToWorld(t, new Point((px - x0 * 256) * 4, (py - y0 * 256) * 4));
        var direct = GeoPodklad.Puwg92(lat, lon); var via = WorldToPuwg(c);
        Ok(via is { } vv && Math.Abs(vv.E - direct.E) < 0.05 && Math.Abs(vv.N - direct.N) < 0.05, $"plan → PUWG 1992: Δ {(via is { } v2 ? Math.Sqrt(Math.Pow(v2.E - direct.E, 2) + Math.Pow(v2.N - direct.N, 2)) : -1):0.###} m");
        doc.Snapshot(); bg["rot"] = 7.5; Changed(); t = BgXf(bg, BgPx(bg));
        c = BgToWorld(t, new Point((px - x0 * 256) * 4, (py - y0 * 256) * 4)); via = WorldToPuwg(c);
        Ok(via is { } v3 && Math.Abs(v3.E - direct.E) < 0.05 && Math.Abs(v3.N - direct.N) < 0.05, "po obrocie podkładu – ten sam punkt terenu");
        Ok(WorldToPuwg(c + new Vector(1e6, 0)) == null, "punkt poza podkładem – brak położenia");
        // tor 1200 m przez środek (dwa tory w ciągu), profil
        double ux = doc.Ux;
        var a0 = doc.AddNode("TP", c.X - 600 * ux, c.Y); var a1 = doc.AddNode("TP", c.X + 100 * ux, c.Y); var a2 = doc.AddNode("TP", c.X + 600 * ux, c.Y);
        var t1 = doc.AddEdge(a0, a1, null, "TP"); var t2 = doc.AddEdge(a2, a1, null, "TP"); Rebuild();   // drugi tor odwrócony (B w środku)
        var run = RunEdges(t1); Ok(run.Count == 2 && run[1] == (t2, true), $"ciąg toru: {string.Join(", ", run.Select(r => r.Id + (r.Rev ? "↔" : "")))}");
        var ranges = new List<(string Id, bool Rev, double S0, double S1)>(); var pts = EdgesPts(run, ranges);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var raw = ProfileCompute(pts, null, out var err);
        Ok(raw != null && raw.Count > 300 && Math.Abs(raw[^1].S - 1200) < 0.5, $"NMT: {raw?.Count} punktów w {sw.Elapsed.TotalSeconds:0.#} s, {raw?[^1].S:0} m" + (err != null ? " – " + err : ""));
        if (raw != null)
        {
            Ok(raw.All(q => q.H > 50 && q.H < 1000), $"wysokości {raw.Min(q => q.H):0.#}–{raw.Max(q => q.H):0.#} m n.p.m.");
            var fine = Profil.Simplify(raw, 0.3);
            Ok(raw.Max(q => Math.Abs(Profil.At(fine, q.S) - q.H)) <= 0.3 + 1e-9, $"odchyłka uproszczenia 0,3 m ≤ 0,3 m ({fine.Count} załomów)");
            var simp = Profil.Simplify(raw, 0.3, 50); var sec = Profil.Sections(simp);
            Ok(simp.Count >= 2 && sec.All(q => q.Len >= 50 - 1e-6), $"z najkrótszym odcinkiem 50 m: {simp.Count} załomów, odcinki: {string.Join("; ", sec.Take(6).Select(q => $"{q.Len:0} m {q.Promil:+0.#;-0.#;0}‰"))}{(sec.Count > 6 ? "…" : "")}");
            var msg = ProfileSave(ranges, simp);
            double hA1 = Num(doc.Edge(t1)["hA"], 0), hB1 = Num(doc.Edge(t1)["hB"], 0), hA2 = Num(doc.Edge(t2)["hA"], 0), hB2 = Num(doc.Edge(t2)["hB"], 0);
            Ok(Math.Abs(hA1 - simp[0].H) < 0.01 && Math.Abs(hA2 - simp[^1].H) < 0.01 && Math.Abs(hB1 - hB2) < 0.01, $"{msg}: tor 1 {hA1:0.##}→{hB1:0.##}, tor 2 (odwrócony) {hA2:0.##}→{hB2:0.##} m");
            double l1 = ranges[0].S1 - ranges[0].S0;
            Ok(Math.Abs(Num(doc.Edge(t1)["poch"], 0) - Math.Round((hB1 - hA1) / l1 * 1000, 1)) < 0.11, $"pochylenie toru 1: {Num(doc.Edge(t1)["poch"], 0):+0.#;-0.#;0}‰");
            doc.Save(outJson); var re = Station.Load(outJson);
            Ok(!double.IsNaN(re.Edges[t1].Grad) && Math.Abs(re.Edges[t2].Grad - Num(doc.Edge(t2)["poch"], 0)) < 1e-9, "zapis i wczytanie pochyleń (model)");
            Ok((doc.ScaleAlong, doc.ScaleAcross) == scale0, $"skala planu bez zmian (1:{doc.ScaleAlong:0} / 1:{doc.ScaleAcross:0})");
            var win = new ProfileWindow($"ciąg toru {t1} (test)", raw, v => ProfileSave(ranges, v)) { Owner = this, Width = 1300, Height = 760 };
            win.Show(); win.UpdateLayout();
            var shot = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outJson)) ?? ".", "profil_okno.png");
            win.Shot(shot); win.Close();
            Ok(File.Exists(shot), "zrzut okna profilu: " + shot);
        }
        PlanLayers.Current.Radius = true;
        panel.FitAll();
        log.AppendLine($"błędów testu: {bad}, błędów edytora: {Errors.Count}");
        return log.ToString();
    }
}

/// <summary>Okno profilu: wykres (teren i uproszczony profil), pasek pochyleń, tabela odcinków, zapis w torach.</summary>
public class ProfileWindow : DarkWindow
{
    readonly List<(double S, double H)> raw;
    List<(double S, double H)> simp;
    readonly Func<List<(double S, double H)>, string> save;
    readonly ProfileChart chart = new();
    readonly DataGrid grid = Grid();
    readonly TextBlock info = new() { Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 6, 8, 2) };
    readonly TextBox tolBox = Box("0.3"), minBox = Box("50");
    record Row(string Od, string Do, string Dl, string H0, string H1, string Poch);

    public ProfileWindow(string what, List<(double S, double H)> raw, Func<List<(double S, double H)>, string> save) : base("Profil wysokościowy – " + what, 1300, 760)
    {
        this.raw = raw; this.save = save; ShowInTaskbar = true;
        Col(grid, "od [m]", "Od", 80); Col(grid, "do [m]", "Do", 80); Col(grid, "długość [m]", "Dl", 90); Col(grid, "h od", "H0", 80); Col(grid, "h do", "H1", 80); Col(grid, "pochylenie [‰]", "Poch", 130);
        var hs = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        hs.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x28)))); hs.Setters.Add(new Setter(ForegroundProperty, Brushes.LightGray)); hs.Setters.Add(new Setter(PaddingProperty, new Thickness(4, 2, 4, 2)));
        grid.ColumnHeaderStyle = hs;
        var root = new DockPanel();
        DockPanel.SetDock(info, Dock.Top); root.Children.Add(info);
        var bar = new WrapPanel { Margin = new Thickness(8, 2, 8, 6) };
        bar.Children.Add(new TextBlock { Text = "Tolerancja załomów [m]: ", Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center });
        tolBox.Width = 50; tolBox.ToolTip = "Największa odchyłka uproszczonego profilu od terenu. Mniej – więcej krótkich odcinków; więcej – długie odcinki o stałym pochyleniu jak w profilu linii.";
        tolBox.KeyDown += (a, e) => { if (e.Key == Key.Enter) Recompute(); };
        bar.Children.Add(tolBox);
        bar.Children.Add(new TextBlock { Text = "   najkrótszy odcinek [m]: ", Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center });
        minBox.Width = 50; minBox.ToolTip = "Odcinki o stałym pochyleniu krótsze niż ta długość są scalane z sąsiednim (0 – bez scalania).";
        minBox.KeyDown += (a, e) => { if (e.Key == Key.Enter) Recompute(); };
        bar.Children.Add(minBox);
        bar.Children.Add(Btn("Przelicz", (a, e) => Recompute()));
        if (save != null) bar.Children.Add(Btn("Zapisz pochylenia w torach", (a, e) => { var m = save(simp); info.Text = InfoText() + "\n✓ " + m; }, new SolidColorBrush(Color.FromRgb(0x1f, 0x3d, 0x5a))));
        bar.Children.Add(Btn("Kopiuj tabelę (Excel)", (a, e) => Clipboard.SetText(Csv())));
        bar.Children.Add(Btn("Zamknij", (a, e) => Close()));
        DockPanel.SetDock(bar, Dock.Bottom); root.Children.Add(bar);
        grid.Height = 170; DockPanel.SetDock(grid, Dock.Bottom); root.Children.Add(grid);
        root.Children.Add(chart);
        Content = root;
        Recompute();
    }

    double MinLen => double.TryParse(minBox.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var t) ? Math.Clamp(t, 0, 5000) : 50;
    double Tol => double.TryParse(tolBox.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var t) ? Math.Clamp(t, 0.02, 20) : 0.3;

    void Recompute()
    {
        simp = Profil.Simplify(raw, Tol, MinLen);
        var sec = Profil.Sections(simp);
        grid.ItemsSource = sec.Select(q => new Row($"{q.S0:0}", $"{q.S1:0}", $"{q.Len:0}", $"{q.H0:0.00}", $"{q.H1:0.00}", Math.Abs(q.Promil) < 0.05 ? "0 (poziom)" : $"{q.Promil:+0.0;-0.0}")).ToList();
        chart.Set(raw, simp);
        info.Text = InfoText();
    }

    string InfoText()
    {
        var sec = Profil.Sections(simp);
        return $"Długość {raw[^1].S:0} m, wysokość terenu {raw.Min(q => q.H):0.0}–{raw.Max(q => q.H):0.0} m n.p.m. (różnica końców {raw[^1].H - raw[0].H:+0.0;-0.0} m). " +
               $"Odcinki o stałym pochyleniu: {sec.Count}, największe pochylenie {sec.Select(q => Math.Abs(q.Promil)).DefaultIfEmpty(0).Max():0.0}‰. " +
               "Wysokości: NMT GUGiK (geoportal.gov.pl) – teren z lotniczego skaningu laserowego, nie główka szyny; na wiaduktach i mostach NMT pokazuje dolinę pod obiektem.";
    }

    string Csv() => "od [m]\tdo [m]\tdługość [m]\th od [m]\th do [m]\tpochylenie [‰]\n" +
        string.Join("\n", Profil.Sections(simp).Select(q => FormattableString.Invariant($"{q.S0:0}\t{q.S1:0}\t{q.Len:0}\t{q.H0:0.00}\t{q.H1:0.00}\t{q.Promil:0.0}").Replace('.', ',')));

    public void Shot(string file)
    {
        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
        rtb.Render((Visual)Content);
        var enc = new System.Windows.Media.Imaging.PngBitmapEncoder(); enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
        using var f = File.Create(file); enc.Save(f);
    }
}

/// <summary>Wykres profilu: oś s [m], oś h [m n.p.m.] (przewyższenie automatyczne), teren szary, profil czerwony, pasek pochyleń.</summary>
public class ProfileChart : FrameworkElement
{
    List<(double S, double H)> raw = new(), simp = new();
    Point? hover;
    public ProfileChart() { MouseMove += (a, e) => { hover = e.GetPosition(this); InvalidateVisual(); }; MouseLeave += (a, e) => { hover = null; InvalidateVisual(); }; ClipToBounds = true; }
    public void Set(List<(double S, double H)> r, List<(double S, double H)> s) { raw = r; simp = s; InvalidateVisual(); }

    static double Nice(double v) { double p = Math.Pow(10, Math.Floor(Math.Log10(Math.Max(1e-9, v)))); foreach (var k in new[] { 1, 2, 5, 10 }) if (k * p >= v) return k * p; return 10 * p; }
    FormattedText Ft(string t, double size, Brush b) => new(t, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Consolas"), size, b, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x0c, 0x0c, 0x10)), null, new Rect(0, 0, ActualWidth, ActualHeight));
        if (raw.Count < 2 || ActualWidth < 200 || ActualHeight < 150) return;
        const double ml = 64, mr = 20, mt = 16, band = 54, mb = 30 + band;
        double W = ActualWidth - ml - mr, H = ActualHeight - mt - mb, L = raw[^1].S;
        double h0 = raw.Min(q => q.H), h1 = raw.Max(q => q.H); double pad = Math.Max(1, (h1 - h0) * 0.12); h0 -= pad; h1 += pad;
        double X(double s) => ml + s / L * W; double Y(double h) => mt + (h1 - h) / (h1 - h0) * H;
        var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(0x2a, 0x2a, 0x32)), 1); var tb = new SolidColorBrush(Color.FromRgb(0x90, 0xa0, 0xb0));
        double sx = Nice(L / 12), sh = Nice((h1 - h0) / 8);
        for (double s = 0; s <= L + 1e-6; s += sx) { dc.DrawLine(gridPen, new Point(X(s), mt), new Point(X(s), mt + H)); var ft = Ft(L >= 3000 ? $"{s / 1000:0.#} km" : $"{s:0} m", 10, tb); dc.DrawText(ft, new Point(X(s) - ft.Width / 2, mt + H + 2)); }
        for (double h = Math.Ceiling(h0 / sh) * sh; h <= h1; h += sh) { dc.DrawLine(gridPen, new Point(ml, Y(h)), new Point(ml + W, Y(h))); var ft = Ft($"{h:0.#}", 10, tb); dc.DrawText(ft, new Point(ml - ft.Width - 4, Y(h) - ft.Height / 2)); }
        dc.DrawText(Ft("m n.p.m.", 10, tb), new Point(4, mt - 2));
        // przewyższenie
        double vex = (W / L) / (H / (h1 - h0));
        dc.DrawText(Ft($"przewyższenie ×{1 / vex:0}", 10, tb), new Point(ml + W - 130, mt + 2));
        // teren
        var g = new StreamGeometry(); using (var c = g.Open()) { c.BeginFigure(new Point(X(raw[0].S), Y(raw[0].H)), false, false); foreach (var q in raw.Skip(1)) c.LineTo(new Point(X(q.S), Y(q.H)), true, false); }
        g.Freeze(); dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)), 1), g);
        // profil uproszczony
        var red = new SolidColorBrush(Color.FromRgb(0xff, 0x50, 0x40)); var rp = new Pen(red, 2.2);
        for (int i = 0; i < simp.Count - 1; i++) dc.DrawLine(rp, new Point(X(simp[i].S), Y(simp[i].H)), new Point(X(simp[i + 1].S), Y(simp[i + 1].H)));
        foreach (var q in simp) dc.DrawEllipse(red, null, new Point(X(q.S), Y(q.H)), 2.5, 2.5);
        // pasek pochyleń (jak na profilu linii): kreska w górę / w dół / pozioma, ‰ i długość
        double by = ActualHeight - band - 4; var bp = new Pen(Brushes.Gainsboro, 1);
        dc.DrawRectangle(null, bp, new Rect(ml, by, W, band - 8));
        foreach (var o in Profil.Sections(simp))
        {
            double xa = X(o.S0), xb = X(o.S1), top = by + 4, bot = by + band - 12;
            dc.DrawLine(bp, new Point(xb, by), new Point(xb, by + band - 8));
            var p = o.Promil;
            if (Math.Abs(p) < 0.05) dc.DrawLine(bp, new Point(xa, (top + bot) / 2), new Point(xb, (top + bot) / 2));
            else dc.DrawLine(bp, new Point(xa, p > 0 ? bot : top), new Point(xb, p > 0 ? top : bot));
            if (xb - xa < 34) continue;
            var f1 = Ft(Math.Abs(p) < 0.05 ? "0" : $"{Math.Abs(p):0.0}", 10.5, Brushes.White); var f2 = Ft($"{o.Len:0}", 9.5, tb);
            // ‰ nad kreską, długość pod nią (po stronie, gdzie kreska jest niżej / wyżej)
            dc.DrawText(f1, new Point(p > 0 ? xa + 4 : xb - f1.Width - 4, top - 2));
            dc.DrawText(f2, new Point(p > 0 ? xb - f2.Width - 4 : xa + 4, bot - f2.Height + 3));
        }
        dc.DrawText(Ft("‰ / m", 9.5, tb), new Point(8, by + band / 2 - 10));
        // kursor
        if (hover is Point hp && hp.X >= ml && hp.X <= ml + W)
        {
            double s = (hp.X - ml) / W * L, ht = Profil.At(raw, s), hs = Profil.At(simp, s);
            dc.DrawLine(new Pen(Brushes.DeepSkyBlue, 1), new Point(hp.X, mt), new Point(hp.X, mt + H));
            var o = Profil.Sections(simp).FirstOrDefault(q => s >= q.S0 && s <= q.S1);
            var ft = Ft($"{s:0} m: teren {ht:0.00}, profil {hs:0.00} m n.p.m.{(o != null ? $", {o.Promil:+0.0;-0.0;0}‰" : "")}", 11, Brushes.DeepSkyBlue);
            double tx = Math.Min(hp.X + 8, ActualWidth - ft.Width - 4);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0xd0, 0, 0, 0)), null, new Rect(tx - 2, mt + 2, ft.Width + 4, ft.Height));
            dc.DrawText(ft, new Point(tx, mt + 2));
        }
    }
}

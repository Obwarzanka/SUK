using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>Test etapu 2 – podkłady, miarka (część testu --test; potrzebny internet: Geoportal, Esri).</summary>
public static partial class Test
{
    static async Task<bool> WaitFor(Func<bool> cond, int ms)
    {
        var sw = Stopwatch.StartNew();
        while (!cond()) { if (sw.ElapsedMilliseconds > ms) return false; await Task.Delay(100); }
        return true;
    }

    /// <summary>Do zrzutu: podgląd na żywo i obrazy podkładów wczytane.</summary>
    static async Task Settle(MainWindow w)
    {
        w.View.Live.Schedule();
        await Task.Delay(500);
        await WaitFor(() => !w.View.Live.OrtoBusy && w.View.Live.TilePending == 0 && w.View.Cache.PendingCount == 0, 30000);
        w.View.InvalidateVisual();
        await w.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        await WaitFor(() => w.View.Cache.PendingCount == 0, 20000);
        w.View.InvalidateVisual();
        await w.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
    }

    static void KeyPress(MainWindow w, Key k)
    {
        w.View.Focus(); Keyboard.Focus(w.View);
        var src = PresentationSource.FromVisual(w);
        w.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, src, 0, k) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
    }
    static void Enter(TextBox t)
    {
        var src = PresentationSource.FromVisual(t);
        t.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, src, 0, System.Windows.Input.Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent });
    }
    static T Find<T>(DependencyObject root, Func<T, bool> pred) where T : DependencyObject
    {
        if (root is T t && pred(t)) return t;
        if (root is FrameworkElement fe) fe.ApplyTemplate();
        foreach (var c in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            var r = Find(c, pred); if (r != null) return r;
        }
        return null;
    }
    static Button BtnIn(Panel p, string text) => Find<Button>(p, b => b.Content as string == text);
    static ToggleButton ToggleIn(Panel p, string text) => Find<ToggleButton>(p, b => b.Content as string == text && b is not CheckBox);

    /// <summary>Obraz testowy z punktami charakterystycznymi (krzyże w znanych pikselach).</summary>
    static void MakeImage(string file, int w, int h)
    {
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, new Pen(Brushes.Black, 4), new Rect(0, 0, w, h));
            var pen = new Pen(Brushes.Red, 3);
            foreach (var (x, y) in new[] { (50, 40), (350, 60), (200, 260), (370, 280) })
            {
                dc.DrawLine(pen, new Point(x - 12, y), new Point(x + 12, y)); dc.DrawLine(pen, new Point(x, y - 12), new Point(x, y + 12));
            }
            dc.DrawText(new FormattedText("PLAN TESTOWY", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 28, Brushes.Navy, 1), new Point(110, 130));
        }
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32); rtb.Render(dv);
        var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
        using var f = File.Create(file); enc.Save(f);
    }

    static async Task RunBg(MainWindow w)
    {
        var inv = CultureInfo.InvariantCulture;
        // --- geometria (model)
        var wf = Affine.FromWorldFile(new[] { "0.1", "0", "0", "-0.1", "537500.05", "433199.95" });
        Ok(XY.Dist(wf.World(0, 0), new XY(433200, 537500)) < 1e-9 && Math.Abs(wf.Mpp - 0.1) < 1e-12, "plik world: środek piksela → narożnik obrazu");
        var truth = Affine.NorthUp(433300, 537400, 0.137).Then(new XY(433250, 537450), 7, 1.3, new XY(12, -8));
        var pix = new[] { (10.0, 20.0), (380.0, 35.0), (200.0, 290.0), (390.0, 270.0) };
        var cps = pix.Select(p => { var q = truth.World(p.Item1, p.Item2); return new UtCalPoint { Col = p.Item1, Row = p.Item2, X = q.X, Y = q.Y }; }).ToList();
        var fs = Affine.FitSimilarity(cps.Take(2).ToList());
        double fsErr = pix.Max(p => XY.Dist(fs.World(p.Item1, p.Item2), truth.World(p.Item1, p.Item2)));
        Ok(fsErr < 1e-6 && Math.Abs(fs.RotationDeg - 7) < 1e-7, $"dopasowanie podobieństwem z 2 punktów – obrót 7°, skala, przesunięcie dokładnie (błąd {fsErr:0.######} m, obrót {fs.RotationDeg:0.######}° / wzorzec {truth.RotationDeg:0.######}°, piksel {fs.Mpp:0.######} / {truth.Mpp:0.######})");
        var skew = new Affine(0.11, 0.02, 537000, -0.015, -0.09, 433000);
        var ca = pix.Select(p => { var q = skew.World(p.Item1, p.Item2); return new UtCalPoint { Col = p.Item1, Row = p.Item2, X = q.X, Y = q.Y }; }).ToList();
        var fa = Affine.FitAffine(ca.Take(3).ToList());
        Ok(XY.Dist(fa.World(390, 270), skew.World(390, 270)) < 1e-6, "dopasowanie afiniczne z 3 punktów (dwie skale, skos)");
        var noisy = cps.Select((p, i) => new UtCalPoint { Col = p.Col, Row = p.Row, X = p.X + (i % 2 == 0 ? 0.03 : -0.03), Y = p.Y }).ToList();
        var fn = Affine.FitSimilarity(noisy);
        Ok(noisy.Max(p => XY.Dist(fn.World(p.Col, p.Row), p.Target)) < 0.04, "4 punkty z błędem ±3 cm – odchyłki ≤ 4 cm");
        var (pc, pr) = truth.Pixel(truth.World(123.4, 56.7));
        Ok(Math.Abs(pc - 123.4) < 1e-6 && Math.Abs(pr - 56.7) < 1e-6, "piksel ↔ świat (odwrotność)");
        Ok(Math.Abs(Geo.ScaleFactor(new XY(450000, 500000)) - 0.9993) < 1e-9, "skala odwzorowania na południku 19° = 0,9993");
        foreach (var (la, lo, la2, lo2, what) in new[] { (51.7631, 19.5438, 51.7712, 19.5701, "Łódź (≈2 km)"), (50.30, 23.60, 50.31, 23.64, "wschód Polski (≈3 km)") })
        {
            var a = Geo.ToPuwg92(la, lo); var b = Geo.ToPuwg92(la2, lo2);
            double g = Geo.GroundDistance(a, b), v = Geo.GeodesicDistance(la, lo, la2, lo2);
            Ok(Math.Abs(g - v) < 0.002, $"długość w terenie z PUWG = geodezyjna, {what}: {g.ToString("0.000", inv)} / {v.ToString("0.000", inv)} m (w układzie {XY.Dist(a, b).ToString("0.000", inv)})");
        }

        // --- projekt w katalogu testowym (podkłady obok projektu)
        var dir = Path.Combine(Path.GetTempPath(), "edytor_ut_test", "etap2"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        w.NewDoc();
        Ok(w.SaveDoc(false, Path.Combine(dir, "podklady_test.ut.json")), "projekt zapisany w katalogu testowym");
        var d = w.Doc;

        // --- podgląd na żywo: ortofotomapa
        w.GoTo("51.7631, 19.5438"); w.View.SetView(w.View.Center, 0.5);
        Ok(await WaitFor(() => w.View.Live.ortoImg != null, 40000), "podgląd na żywo: ortofotomapa widoku pobrana" + (w.View.Live.Status.Length > 0 ? " – " + w.View.Live.Status : ""));
        if (w.View.Live.ortoImg is var (lb, lg)) Ok(Math.Abs(lg.Mpp - 0.5) < 1e-9 && XY.Dist(lg.World(lb.PixelWidth / 2.0, lb.PixelHeight / 2.0), w.View.Center) < 1, "podgląd: obraz w rozdzielczości ekranu, środek w środku widoku");

        // --- pobranie ortofotomapy: Zaznacz obszar → przeciągnięcie
        w.ResBox.SelectedIndex = 1;   // 10 cm
        Click(w.AreaBtn);
        Ok(w.View.Tool == UtTool.Obszar, "przycisk „Zaznacz obszar” – narzędzie Obszar");
        var c = new Point(w.View.ActualWidth / 2, w.View.ActualHeight / 2);
        var A = c + new Vector(-300, -180); var B = c + new Vector(300, 180);   // 300 × 180 m
        var wa = w.View.ToWorld(A); var wb = w.View.ToWorld(B);
        w.View.SimDrag(A, B);
        Ok(!w.Downloading.IsCompleted || d.P.Backgrounds.Count > 0, "przeciągnięcie – pobieranie ruszyło");
        await w.Downloading;
        var orto = d.P.Backgrounds.FirstOrDefault(b => b.Kind == UtBackground.Orto);
        Ok(orto != null, "ortofotomapa dodana do projektu");
        if (orto != null)
        {
            var g = orto.Grid;
            Ok(g.Mpp == 0.1 && g.TileSize == 2048 && orto.Tiles.Count == g.Rows * g.Cols, $"siatka {g.Cols} × {g.Rows} kafelków 2048 px po 10 cm, pobrane wszystkie ({orto.Tiles.Count})");
            Ok(g.Y0 <= Math.Min(wa.Y, wb.Y) && g.X0 >= Math.Max(wa.X, wb.X) && g.Y0 + g.Cols * 204.8 >= Math.Max(wa.Y, wb.Y) && g.X0 - g.Rows * 204.8 <= Math.Min(wa.X, wb.X), "siatka pokrywa zaznaczony prostokąt");
            Ok(orto.Tiles.All(t => File.Exists(d.Resolve(t.File)) && !Path.IsPathRooted(t.File) && t.File.StartsWith("podklady/")), "pliki kafelków obok projektu, ścieżki względne (podklady/…)");
            Ok(orto.Tiles.All(t => { var bi = BitmapDecoder.Create(new Uri(d.Resolve(t.File)), BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0]; return bi.PixelWidth == 2048 && bi.PixelHeight == 2048; }), "kafelki mają 2048 × 2048 px");
            var t00 = orto.Tiles.First(t => t.Row == 0 && t.Col == 0);
            var t01 = orto.Tiles.FirstOrDefault(t => t.Row == 0 && t.Col == 1);
            Ok(t01 == null || XY.Dist(t00.Geo.World(2048, 0), t01.Geo.World(0, 0)) < 1e-6, "sąsiednie kafelki stykają się bez szczeliny i zakładki");
            Ok(XY.Dist(t00.Geo.World(0, 0), new XY(g.X0, g.Y0)) < 1e-9 && Math.Abs(g.X0 / 0.1 - Math.Round(g.X0 / 0.1)) < 1e-6, "narożnik siatki w okrągłych współrzędnych (wielokrotność 10 cm)");
            Ok(w.BgList.Items.Count == 1 && (w.BgList.SelectedItem as ListBoxItem)?.Tag as string == orto.Id, "warstwa na liście, zaznaczona");
            await Settle(w);
            Ok(w.View.DrawnTiles == orto.Tiles.Count, $"kafelki narysowane na płótnie ({w.View.DrawnTiles})");
            w.DoUndo(); Ok(d.P.Backgrounds.Count == 0 && w.BgList.Items.Count == 0, "Ctrl+Z: ortofotomapa zdjęta z projektu");
            w.DoRedo(); Ok(d.P.Backgrounds.Count == 1, "Ctrl+Y: wróciła");
            orto = d.P.Backgrounds[0];
        }

        // --- miarka
        w.View.SetView(w.View.Center, 0.5);
        Find<ToggleButton>(w, b => (b.Content as string)?.Contains("Miarka") == true).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Ok(w.View.Tool == UtTool.Miarka, "przycisk Miarka");
        var m1 = c + new Vector(-250, 120); var m2 = c + new Vector(230, -90); var m3 = c + new Vector(260, 150);
        w.View.SimClick(m1); w.View.SimClick(m2);
        var q1 = w.View.ToWorld(m1); var q2 = w.View.ToWorld(m2);
        var (la1, lo1) = Geo.ToWgs84(q1); var (la2_, lo2_) = Geo.ToWgs84(q2);
        double meas = UtView.Length(w.View.MeasurePts), geod = Geo.GeodesicDistance(la1, lo1, la2_, lo2_);
        Ok(w.View.MeasurePts.Count == 2 && Math.Abs(meas - geod) < 0.002, $"miarka: zmierzona długość = rzeczywista (geodezyjna) – {meas.ToString("0.000", inv)} / {geod.ToString("0.000", inv)} m");
        w.View.SimDrag(c, c + new Vector(40, 0));
        Ok(w.View.MeasurePts.Count == 2, "przeciągnięcie w miarce przesuwa widok, nie dodaje punktu");
        w.View.PanBy(new Vector(-40, 0));
        w.View.SimClick(m3); Ok(w.View.MeasurePts.Count == 3, "trzeci punkt");
        KeyPress(w, System.Windows.Input.Key.Back); Ok(w.View.MeasurePts.Count == 2, "Backspace – cofnięty punkt");
        w.View.SimClick(m3);
        double total = UtView.Length(w.View.MeasurePts);

        // --- grafika z plikiem world
        var src = Path.Combine(Path.GetTempPath(), "edytor_ut_test", "zrodla"); Directory.CreateDirectory(src);
        var img = Path.Combine(src, "plan_world.png"); MakeImage(img, 400, 300);
        var center = w.View.Center;
        double gm = 0.4;   // 0,4 m/px – obraz 160 × 120 m
        var geoTrue = Affine.NorthUp(center.X + 60, center.Y - 80, gm);
        var cw = geoTrue.World(0.5, 0.5);
        File.WriteAllLines(Path.ChangeExtension(img, ".pgw"), new[] { gm, 0, 0, -gm, cw.Y, cw.X }.Select(v => v.ToString("R", inv)));
        w.PickImageForTest = () => img;
        Click(w.ImageBtn);
        var gW = d.P.Backgrounds.LastOrDefault();
        Ok(gW?.Kind == UtBackground.Image && XY.Dist(gW.Tiles[0].Geo.World(0, 0), geoTrue.World(0, 0)) < 1e-6 && XY.Dist(gW.Tiles[0].Geo.World(400, 300), geoTrue.World(400, 300)) < 1e-6, "grafika z plikiem .pgw – od razu na miejscu");
        Ok(gW != null && File.Exists(d.Resolve(gW.Tiles[0].File)) && gW.Tiles[0].File.StartsWith("podklady/") && File.Exists(Path.Combine(dir, "podklady", "plan_world.pgw")), "grafika i plik world skopiowane do podklady/ projektu");

        // --- grafika bez pliku world → kalibracja klikami
        var img2 = Path.Combine(src, "plan_bez.png"); File.Copy(img, img2, true);
        w.PickImageForTest = () => img2;
        Click(w.ImageBtn);
        var gC = d.P.Backgrounds.Last();
        Ok(gC.Name == "plan_bez" && gC.Calibration.Count == 0 && XY.Dist(gC.Tiles[0].Geo.World(200, 150), geoTrue.World(200, 150)) > 1, "grafika bez pliku world – na środku widoku, do kalibracji");
        Ok((w.BgList.SelectedItem as ListBoxItem)?.Tag as string == gC.Id && BtnIn(w.BgDetails, "Pokaż") != null, "nowa grafika zaznaczona, panel warstwy");
        var calBtn = ToggleIn(w.BgDetails, "Kalibruj punktami");
        calBtn.IsChecked = true; calBtn.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Ok(w.View.Tool == UtTool.Kalibracja, "„Kalibruj punktami” – narzędzie Kalibracja");
        w.View.SimClick(new Point(5, w.View.ActualHeight - 5));
        Ok(w.View.CalPending == null, "klik poza grafiką – nic");
        async Task CalPoint(double col, double row)
        {
            var g0 = d.P.Backgrounds.First(b => b.Id == gC.Id).Tiles[0].Geo;
            w.View.SimClick(w.View.ToScreen(g0.World(col, row)));
            w.View.SimClick(w.View.ToScreen(geoTrue.World(col, row)));
            await Task.Yield();
        }
        await CalPoint(50, 40);
        var g1 = d.P.Backgrounds.First(b => b.Id == gC.Id);
        Ok(g1.Calibration.Count == 1 && XY.Dist(g1.Tiles[0].Geo.World(50, 40), geoTrue.World(50, 40)) < 1e-6, "punkt 1: grafika przesunięta na miejsce punktu");
        await CalPoint(350, 60);
        g1 = d.P.Backgrounds.First(b => b.Id == gC.Id);
        double err2 = new[] { (0.0, 0.0), (400.0, 300.0), (200.0, 260.0) }.Max(p => XY.Dist(g1.Tiles[0].Geo.World(p.Item1, p.Item2), geoTrue.World(p.Item1, p.Item2)));
        Ok(g1.Calibration.Count == 2 && err2 < 1e-4, $"punkt 2: skala i obrót – grafika dokładnie na miejscu (błąd {err2 * 1000:0.000} mm)");
        w.View.SimClick(w.View.ToScreen(g1.Tiles[0].Geo.World(200, 260)));
        Ok(w.View.CalPending != null, "trzeci punkt wskazany na grafice");
        w.View.SimClick(new Point(300, 300), MouseButton.Right);
        Ok(w.View.CalPending == null && d.P.Backgrounds.First(b => b.Id == gC.Id).Calibration.Count == 2, "PPM – rezygnacja z punktu");
        await CalPoint(200, 260); await CalPoint(370, 280);
        g1 = d.P.Backgrounds.First(b => b.Id == gC.Id);
        Ok(g1.Calibration.Count == 4 && g1.Calibration.All(p => MainWindow.Residual(g1, p) < 1e-4), "4 punkty – odchyłki 0");
        w.DoUndo(); Ok(d.P.Backgrounds.First(b => b.Id == gC.Id).Calibration.Count == 3, "Ctrl+Z – cofnięty ostatni punkt kalibracji");
        KeyPress(w, System.Windows.Input.Key.Escape); Ok(w.View.Tool == UtTool.Wskaz, "Esc – koniec kalibracji");

        // --- przesuwanie, obrót, skala grafiki
        w.SelectBg(gC.Id);
        var mvBtn = ToggleIn(w.BgDetails, "Przesuń"); mvBtn.IsChecked = true; mvBtn.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        var before = d.P.Backgrounds.First(b => b.Id == gC.Id).Tiles[0].Geo;
        var p0 = w.View.ToScreen(before.World(200, 150));
        w.View.SimDrag(p0, p0 + new Vector(100, 50));
        var after = d.P.Backgrounds.First(b => b.Id == gC.Id).Tiles[0].Geo;
        var sh = after.World(0, 0) - before.World(0, 0);
        Ok(Math.Abs(sh.Y - 100 * w.View.Mpp) < 1e-6 && Math.Abs(sh.X + 50 * w.View.Mpp) < 1e-6 && Math.Abs(after.A - before.A) < 1e-12, "Przesuń: grafika przeciągnięta o 100 × 50 px (na wschód i na południe)");
        w.DoUndo();
        KeyPress(w, System.Windows.Input.Key.Escape);
        w.SelectBg(gC.Id);
        var boxes = new List<TextBox>(); void Collect(DependencyObject o) { if (o is TextBox tb) boxes.Add(tb); foreach (var ch in LogicalTreeHelper.GetChildren(o).OfType<DependencyObject>()) Collect(ch); }
        Collect(w.BgDetails);
        var rot = boxes.First(b => (b.ToolTip as string)?.StartsWith("Obrót") == true);
        var g2 = d.P.Backgrounds.First(b => b.Id == gC.Id).Tiles[0].Geo; var piv = g2.World(200, 150);
        rot.Text = "10"; Enter(rot);
        var g3 = d.P.Backgrounds.First(b => b.Id == gC.Id).Tiles[0].Geo;
        Ok(Math.Abs(g3.RotationDeg - 10) < 1e-9 && Math.Abs(g3.Mpp - g2.Mpp) < 1e-12 * g2.Mpp * 100 && XY.Dist(g3.World(200, 150), piv) < 1e-6, $"obrót 10° wokół środka grafiki, skala bez zmian (obrót {g3.RotationDeg:0.######}°, piksel {g2.Mpp:0.########} → {g3.Mpp:0.########}, środek {XY.Dist(g3.World(200, 150), piv):0.######} m, pole „{rot.Text}”, pól {boxes.Count})");
        w.DoUndo();

        // --- warstwy: widoczność, krycie, kolejność, usuwanie
        w.SelectBg(gW.Id);
        int depth0 = UndoDepth(w);
        w.SelectBg(gW.Id);
        var slider = Find<Slider>(w.BgDetails, _ => true);
        slider.Value = 0.6; slider.Value = 0.4;
        Ok(Find<Slider>(w.BgDetails, _ => true) == slider, "panel nie przebudowany w trakcie przesuwania suwaka");
        Ok(Math.Abs(d.P.Backgrounds.First(b => b.Id == gW.Id).Opacity - 0.4) < 1e-9 && UndoDepth(w) == depth0 + 1, "krycie suwakiem – dwie zmiany, jedno cofnięcie");
        var item = w.BgList.Items.OfType<ListBoxItem>().First(i => i.Tag as string == gW.Id);
        var vis = Find<CheckBox>(item, _ => true); vis.IsChecked = false; vis.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Ok(!d.P.Backgrounds.First(b => b.Id == gW.Id).Visible, "warstwa ukryta (pole na liście)");
        w.DoUndo(); Ok(d.P.Backgrounds.First(b => b.Id == gW.Id).Visible, "Ctrl+Z – widoczna");
        int iw = d.P.Backgrounds.FindIndex(b => b.Id == gW.Id);
        w.SelectBg(gW.Id); Click(BtnIn(w.BgDetails, "↑ Wyżej"));
        Ok(d.P.Backgrounds.FindIndex(b => b.Id == gW.Id) == iw + 1 && ((ListBoxItem)w.BgList.Items[0]).Tag as string == gW.Id, "↑ Wyżej – warstwa na wierzchu, pierwsza na liście");
        Click(BtnIn(w.BgDetails, "↓ Niżej")); Ok(d.P.Backgrounds.FindIndex(b => b.Id == gW.Id) == iw, "↓ Niżej");
        int n0 = d.P.Backgrounds.Count;
        w.SelectBg(gC.Id); Click(BtnIn(w.BgDetails, "Usuń"));
        Ok(d.P.Backgrounds.Count == n0 - 1, "Usuń warstwę"); w.DoUndo(); Ok(d.P.Backgrounds.Count == n0, "Ctrl+Z – przywrócona");

        // --- zapis, odczyt, zapis w innym katalogu
        Ok(w.SaveDoc(false), "zapis projektu z podkładami");
        var re = UtDocument.Load(d.Path);
        Ok(re.P.Backgrounds.Count == n0 && re.P.Backgrounds.SelectMany(b => b.Tiles).All(t => File.Exists(re.Resolve(t.File))), "odczyt: wszystkie pliki podkładów odnalezione");
        var dir2 = Path.Combine(Path.GetTempPath(), "edytor_ut_test", "etap2_inny"); if (Directory.Exists(dir2)) Directory.Delete(dir2, true); Directory.CreateDirectory(dir2);
        Ok(w.SaveDoc(false, Path.Combine(dir2, "kopia.ut.json")), "Zapisz jako… w innym katalogu");
        var re2 = UtDocument.Load(Path.Combine(dir2, "kopia.ut.json"));
        Ok(re2.P.Backgrounds.SelectMany(b => b.Tiles).All(t => Path.IsPathRooted(t.File) && File.Exists(re2.Resolve(t.File))), "kopia w innym katalogu: ścieżki bezwzględne do plików, wszystkie istnieją");
        Ok(w.SaveDoc(false, Path.Combine(dir, "podklady_test.ut.json")) && UtDocument.Load(Path.Combine(dir, "podklady_test.ut.json")).P.Backgrounds.SelectMany(b => b.Tiles).All(t => !Path.IsPathRooted(t.File)), "z powrotem w katalogu projektu – ścieżki znów względne");

        // --- podgląd zapasowy: Esri
        w.LiveBox.SelectedIndex = 1;
        Ok(w.View.Live.Source == LivePreview.Esri, "podgląd: zdjęcia Esri");
        w.View.InvalidateVisual();
        await w.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        var tl = w.View.ToWorld(new Point(0, 0)); var br = w.View.ToWorld(new Point(w.View.ActualWidth, w.View.ActualHeight));
        w.View.Live.VisibleTiles(tl, br);
        Ok(await WaitFor(() => w.View.Live.TilePending == 0 && w.View.Live.VisibleTiles(tl, br).Count > 0, 30000), $"kafelki Esri pobrane ({w.View.Live.VisibleTiles(tl, br).Count})");
        var et = w.View.Live.VisibleTiles(tl, br).FirstOrDefault();
        Ok(et.Bmp != null && Math.Abs(et.Geo.Mpp - w.View.Mpp) / w.View.Mpp < 0.75, "kafelek Esri w skali widoku (afinicznie do PUWG 1992)");
        w.LiveBox.SelectedIndex = 0;

        // --- do zrzutu: ortofotomapa, grafika półprzezroczysta, miarka
        w.SelectBg(gC.Id);
        w.SetTool(UtTool.Miarka);
        w.View.MeasurePts.Clear(); w.View.MeasurePts.AddRange(new[] { q1, q2, w.View.ToWorld(m3) });
        Ok(Math.Abs(total - UtView.Length(w.View.MeasurePts)) < 1e-6, $"miarka: łamana {UtView.Len3(total)}");
    }
}

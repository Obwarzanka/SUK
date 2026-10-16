using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Mapka kafelków ortofotomapy: mapa (OSM / zdjęcia Esri / ortofotomapa) z liniami kolejowymi OpenRailwayMap i stałą siatką
/// kafelków wybranej rozdzielczości. LPM – zaznaczanie kafelków (przeciąganie „maluje” wzdłuż linii; zaczęcie na zaznaczonym – odznacza),
/// PPM / środkowy – przesuwanie, kółko – przybliżenie. Zielone – już w projekcie, niebieskie – do pobrania, czerwona ramka – zamówione, niepobrane.
/// </summary>
public class TileMapWindow : Window
{
    readonly MainWindow main;
    public readonly UtView Map = new() { ShowBackgrounds = false };
    public readonly ComboBox ResBox = new() { Width = 200, Margin = new Thickness(4, 0, 8, 0) };
    public readonly ComboBox BaseBox = new() { Width = 190, Margin = new Thickness(4, 0, 8, 0) };
    public readonly CheckBox RailBox = new() { Content = "Linie kolejowe", IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
    public readonly TextBox GoToBox = new() { Width = 200, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Współrzędne z Google Maps albo X Y w PUWG 1992, Enter – idź" };
    public readonly Button DownloadBtn = new() { Content = "Pobierz zaznaczone", Padding = new Thickness(10, 3, 10, 3), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 6, 0) };
    public readonly Button ClearBtn = new() { Content = "Wyczyść zaznaczenie", Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 0, 6, 0) };
    public readonly Button ShowBtn = new() { Content = "Pokaż w edytorze", Padding = new Thickness(8, 3, 8, 3) };
    readonly TextBlock info = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
    readonly TextBlock status = new() { Foreground = Brushes.DimGray, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };

    /// <summary>Zaznaczone do pobrania – osobno dla każdej rozdzielczości.</summary>
    readonly Dictionary<double, HashSet<UtCell>> selected = new();
    bool erasing;
    public double Res => MainWindow.Resolutions[Math.Max(0, ResBox.SelectedIndex)];
    public HashSet<UtCell> Selected => selected.TryGetValue(Res, out var s) ? s : selected[Res] = new();
    UtGrid Grid => UtGrid.Global(Res);

    static readonly Brush HaveFill = Frozen(Color.FromArgb(0x55, 0x10, 0xb0, 0x30)), SelFill = Frozen(Color.FromArgb(0x60, 0x20, 0x70, 0xe0)),
        HoverFill = Frozen(Color.FromArgb(0x30, 0xff, 0xff, 0xff));
    static readonly Pen GridPen = FrozenPen(Color.FromArgb(0x90, 0x30, 0x30, 0x30), 0.8), HavePen = FrozenPen(Color.FromRgb(0x10, 0x90, 0x20), 1.2),
        SelPen = FrozenPen(Color.FromRgb(0x10, 0x50, 0xc0), 1.4), MissPen = FrozenPen(Color.FromRgb(0xd0, 0x18, 0x18), 1.6), HoverPen = FrozenPen(Color.FromRgb(0x20, 0x20, 0x20), 1.6);

    public TileMapWindow(MainWindow owner)
    {
        main = owner; Owner = owner;
        Title = "Mapka kafelków ortofotomapy";
        Width = 1250; Height = 820; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
        Background = new SolidColorBrush(Color.FromRgb(0xf3, 0xf3, 0xf0));

        foreach (var r in MainWindow.Resolutions)
        {
            var g = UtGrid.Global(r);
            ResBox.Items.Add($"{r * 100:0} cm/px – kafelek {UtView.Len(g.Side)}");
        }
        ResBox.SelectedIndex = Math.Max(0, owner.ResBox.SelectedIndex);
        ResBox.SelectionChanged += (s, e) => { Refresh(); };
        foreach (var (k, n) in new[] { (LivePreview.Osm, "Mapa OpenStreetMap"), (LivePreview.Esri, "Zdjęcia Esri"), (LivePreview.Orto, "Ortofotomapa (Geoportal)") }) BaseBox.Items.Add(n);
        BaseBox.SelectedIndex = 0;
        BaseBox.SelectionChanged += (s, e) => Map.Live.Source = new[] { LivePreview.Osm, LivePreview.Esri, LivePreview.Orto }[BaseBox.SelectedIndex];
        RailBox.Click += (s, e) => Map.Live.Overlay = RailBox.IsChecked == true ? LivePreview.Orm : null;
        GoToBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) { GoTo(GoToBox.Text); e.Handled = true; } };
        DownloadBtn.Click += (s, e) => _ = Download();
        ClearBtn.Click += (s, e) => { Selected.Clear(); Refresh(); };
        ShowBtn.Click += (s, e) => { main.View.SetView(Map.Center, Math.Min(Map.Mpp, 0.5)); main.Activate(); };

        var top = new WrapPanel { Margin = new Thickness(8, 6, 8, 6) };
        top.Children.Add(Lbl("Punkt:")); top.Children.Add(GoToBox);
        var go = new Button { Content = "Idź do", Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(2, 0, 12, 0) }; go.Click += (s, e) => GoTo(GoToBox.Text); top.Children.Add(go);
        top.Children.Add(Lbl("Rozdzielczość:")); top.Children.Add(ResBox);
        top.Children.Add(Lbl("Mapa:")); top.Children.Add(BaseBox);
        top.Children.Add(RailBox);
        var bottom = new DockPanel { Margin = new Thickness(8, 6, 8, 6), LastChildFill = true };
        var btns = new StackPanel { Orientation = Orientation.Horizontal };
        btns.Children.Add(DownloadBtn); btns.Children.Add(ClearBtn); btns.Children.Add(ShowBtn);
        DockPanel.SetDock(btns, Dock.Right); bottom.Children.Add(btns);
        DockPanel.SetDock(info, Dock.Left); bottom.Children.Add(info);
        bottom.Children.Add(status);
        var hint = new TextBlock { Text = "LPM – zaznacz kafelki (przeciągnij wzdłuż linii; zaczęcie na zaznaczonym odznacza) · PPM / środkowy – przesuń · kółko – przybliż · zielone – już w projekcie", Margin = new Thickness(8, 0, 8, 4), Foreground = Brushes.DimGray, FontSize = 12 };
        var root = new DockPanel();
        DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
        DockPanel.SetDock(hint, Dock.Bottom); root.Children.Add(hint);
        root.Children.Add(Map);
        Content = root;

        Map.Live.Source = LivePreview.Osm; Map.Live.Overlay = LivePreview.Orm;
        Map.Tool = UtTool.Kafelki;
        Map.Paint += OnPaint;
        Map.PaintEnd += Refresh;
        Map.Overlay = DrawGrid;
        Map.CursorMoved += _ => Map.InvalidateVisual();
        main.TilesChanged += Refresh;
        main.MessageShown += ShowStatus;
        Closed += (s, e) => { main.TilesChanged -= Refresh; main.MessageShown -= ShowStatus; };
        Loaded += (s, e) => { Map.SetView(main.View.Center, Math.Max(main.View.Mpp, 3)); Refresh(); };
    }

    void ShowStatus(string m) { status.Text = m; UpdateInfo(); }

    static TextBlock Lbl(string t) => new() { Text = t, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) };

    public bool GoTo(string text)
    {
        if (!Geo.TryParsePoint(text, out var p)) { status.Text = "Nie rozpoznano punktu – np. „51.7631, 19.5438” albo „384120 537450”"; return false; }
        Map.SetView(p, Math.Min(Map.Mpp, 3)); return true;
    }

    HashSet<UtCell> Have()
    {
        var b = main.Mosaic(Res);
        return b?.Tiles.Select(t => new UtCell(t.Row, t.Col)).ToHashSet() ?? new();
    }

    XY lastPaint;
    /// <summary>Malowanie: kafelki wzdłuż drogi kursora od poprzedniego położenia (szybki ruch myszy nie zostawia dziur).</summary>
    void OnPaint(XY w, bool start)
    {
        var g = Grid; var have = Have();
        if (start) { erasing = Selected.Contains(g.CellAt(w)); lastPaint = w; }
        double d = XY.Dist(lastPaint, w); int n = Math.Max(1, (int)Math.Ceiling(d / (g.Side / 3)));
        for (int i = 0; i <= n; i++)
        {
            var c = g.CellAt(lastPaint + (w - lastPaint) * (i / (double)n));
            if (erasing) Selected.Remove(c);
            else if (!have.Contains(c)) Selected.Add(c);
        }
        lastPaint = w;
        UpdateInfo();
    }

    void Refresh()
    {
        var have = Have();
        Selected.RemoveWhere(have.Contains);     // pobrane – już nie do pobrania
        UpdateInfo(); Map.InvalidateVisual();
    }

    void UpdateInfo()
    {
        var g = Grid; int n = Selected.Count, h = Have().Count;
        info.Text = $"Zaznaczono {n} kafelków (ok. {n * g.Side * g.Side / 1e6:0.##} km², {n * 0.7:0} MB) · w projekcie {h}";
        DownloadBtn.IsEnabled = n > 0 && main.Downloading.IsCompleted;
    }

    public async Task Download()
    {
        if (Selected.Count == 0) return;
        DownloadBtn.IsEnabled = false;
        await main.DownloadCells(Res, Selected.ToList());
        Refresh();
    }

    // ------------------------------------------------------------------ nakładka: siatka i kafelki
    void DrawGrid(DrawingContext dc)
    {
        var g = Grid; double px = g.Side / Map.Mpp;   // bok kafelka na ekranie
        var tl = Map.ToWorld(new Point(0, 0)); var br = Map.ToWorld(new Point(Map.ActualWidth, Map.ActualHeight));
        var c1 = g.CellAt(tl); var c2 = g.CellAt(br);
        long count = (long)(c2.Row - c1.Row + 1) * (c2.Col - c1.Col + 1);
        Rect R(UtCell c) { var (x0, y0, x1, y1) = g.TileBox(c.Row, c.Col); return new Rect(Map.ToScreen(new XY(x1, y0)), Map.ToScreen(new XY(x0, y1))); }
        bool In(UtCell c) => c.Row >= c1.Row && c.Row <= c2.Row && c.Col >= c1.Col && c.Col <= c2.Col;
        if (px >= 6 && count < 40000)
        {
            for (int r = c1.Row; r <= c2.Row + 1; r++) { double y = Map.ToScreen(new XY(-r * g.Side, 0)).Y; dc.DrawLine(GridPen, new Point(0, y), new Point(Map.ActualWidth, y)); }
            for (int c = c1.Col; c <= c2.Col + 1; c++) { double x = Map.ToScreen(new XY(0, c * g.Side)).X; dc.DrawLine(GridPen, new Point(x, 0), new Point(x, Map.ActualHeight)); }
        }
        var have = Have();
        foreach (var c in have.Where(In)) dc.DrawRectangle(HaveFill, px >= 6 ? HavePen : null, R(c));
        var b = main.Mosaic(Res);
        foreach (var c in MainWindow.Missing(b).Where(In)) dc.DrawRectangle(null, MissPen, Inset(R(c)));
        foreach (var c in Selected.Where(In)) dc.DrawRectangle(SelFill, px >= 6 ? SelPen : null, R(c));
        if (Map.Hover is XY hv && px >= 4) dc.DrawRectangle(HoverFill, HoverPen, R(g.CellAt(hv)));
    }

    static Rect Inset(Rect r) => r.Width > 6 ? new Rect(r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4) : r;

    static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
    static Pen FrozenPen(Color c, double t) { var p = new Pen(Frozen(c), t); p.Freeze(); return p; }
}

using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lcs.Model;
using Microsoft.Win32;

namespace Lcs.EdytorUT;

/// <summary>
/// Podkłady: podgląd na żywo, pobieranie ortofotomapy (obszar / widok), grafika z pliku (plik world albo kalibracja punktami),
/// lista warstw (widoczność, krycie, kolejność), przesuwanie i obrót grafiki; miarka.
/// </summary>
public partial class MainWindow
{
    public readonly ComboBox LiveBox = new() { Margin = new Thickness(0, 2, 0, 8) };
    public readonly ComboBox ResBox = new() { Width = 90, Margin = new Thickness(0, 0, 6, 0) };
    public readonly Button MapBtn = new() { Content = "🗺 Mapka kafelków…", Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 2, 0, 6), HorizontalAlignment = HorizontalAlignment.Left, FontWeight = FontWeights.SemiBold };
    public TileMapWindow MapWin { get; private set; }
    public readonly Button AreaBtn = new() { Content = "Zaznacz obszar…", Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 0, 4, 0) };
    public readonly Button ViewAreaBtn = new() { Content = "Cały widok", Padding = new Thickness(6, 2, 6, 2) };
    public readonly Button ImageBtn = new() { Content = "Grafika z pliku…", Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 6, 0, 8), HorizontalAlignment = HorizontalAlignment.Left };
    public readonly ListBox BgList = new() { MinHeight = 60, MaxHeight = 200, Margin = new Thickness(0, 2, 0, 6) };
    public readonly StackPanel BgDetails = new();
    public readonly Button CancelBtn = new() { Content = "Przerwij", Visibility = Visibility.Collapsed, Padding = new Thickness(6, 0, 6, 0) };
    public static readonly double[] Resolutions = { 0.05, 0.1, 0.25, 0.5 };
    /// <summary>Test: zamiast okna wyboru pliku.</summary>
    public Func<string> PickImageForTest;
    CancellationTokenSource dl;
    string selBg;
    bool bgRefreshing;
    /// <summary>Zmiana z kontrolki panelu (suwak) – bez przebudowy panelu, żeby nie przerwać przeciągania.</summary>
    bool keepDetails;
    public Task Downloading { get; private set; } = Task.CompletedTask;

    void InitBg()
    {
        View.AreaSelected += (a, b) => { SetTool(UtTool.Wskaz); _ = StartDownload(a, b); };
        View.CalClick += OnCalClick;
        View.CalCancel += () => { View.CalPending = null; Message("Kalibracja: punkt pominięty"); };
        View.BgDragged += (id, d) =>
        {
            Doc.Change("Przesunięcie grafiki", p => { var b = p.Backgrounds.FirstOrDefault(x => x.Id == id); if (b != null) foreach (var t in b.Tiles) t.Geo = t.Geo with { C = t.Geo.C + d.Y, F = t.Geo.F + d.X }; });
            Message($"Grafika przesunięta o {UtView.Len(d.Length)}");
        };
        CancelBtn.Click += (s, e) => dl?.Cancel();
    }

    void BuildBgPanel(StackPanel sp)
    {
        sp.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 10) });
        sp.Children.Add(Header("Podkłady"));
        sp.Children.Add(Label("Podgląd na żywo (nie zapisuje się w projekcie)"));
        foreach (var (_, name) in LivePreview.Sources) LiveBox.Items.Add(name);
        LiveBox.SelectedIndex = 0;
        LiveBox.SelectionChanged += (s, e) => { if (LiveBox.SelectedIndex >= 0) View.Live.Source = LivePreview.Sources[LiveBox.SelectedIndex].Key; };
        sp.Children.Add(LiveBox);

        sp.Children.Add(Label("Pobierz ortofotomapę do projektu (Geoportal)"));
        MapBtn.Click += (s, e) => OpenTileMap();
        MapBtn.ToolTip = "Mapa z liniami kolejowymi i siatką kafelków – zaznacz kafelki wzdłuż linii i pobierz";
        sp.Children.Add(MapBtn);
        foreach (var r in Resolutions) ResBox.Items.Add(r < 0.1 ? $"{r * 100:0} cm/px" : $"{r * 100:0} cm/px");
        ResBox.SelectedIndex = 1;
        ResBox.ToolTip = "Rozdzielczość pobieranych zdjęć: 5 cm – widać podkłady i złącza (pełna rozdzielczość Geoportalu w miastach), 10 cm – szyny, 25 cm – tory";
        AreaBtn.Click += (s, e) => SetTool(UtTool.Obszar);
        AreaBtn.ToolTip = "Przeciągnij prostokąt na płótnie – kafelki 2048 px zostaną pobrane do katalogu podklady obok projektu";
        ViewAreaBtn.Click += (s, e) => { var w = View.ActualWidth; var h = View.ActualHeight; _ = StartDownload(View.ToWorld(new Point(UtView.RulerLeft, UtView.RulerTop)), View.ToWorld(new Point(w, h))); };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 4), ToolTip = "Albo wprost na płótnie: prostokąt albo cały widok" };
        row.Children.Add(ResBox); row.Children.Add(AreaBtn); row.Children.Add(ViewAreaBtn);
        sp.Children.Add(row);
        ImageBtn.Click += (s, e) => { var f = PickImage(); if (f != null) ImportImage(f); };
        ImageBtn.ToolTip = "Skan planu, mapa, zrzut – z plikiem world (.pgw / .jgw / .tfw) od razu na miejscu, bez niego – kalibracja punktami";
        sp.Children.Add(ImageBtn);

        sp.Children.Add(Label("Warstwy (u góry – na wierzchu)"));
        BgList.SelectionChanged += (s, e) => { if (bgRefreshing) return; selBg = (BgList.SelectedItem as ListBoxItem)?.Tag as string; if (View.Tool is UtTool.Kalibracja or UtTool.Przesun) SetTool(UtTool.Wskaz); View.SelectedBg = selBg; RefreshBgDetails(); View.InvalidateVisual(); };
        sp.Children.Add(BgList);
        sp.Children.Add(BgDetails);
    }

    public TileMapWindow OpenTileMap()
    {
        if (MapWin == null) { MapWin = new TileMapWindow(this) { ShowActivated = !Test.Running }; MapWin.Closed += (s, e) => MapWin = null; MapWin.Show(); }
        else MapWin.Activate();
        return MapWin;
    }

    UtBackground SelBg => Doc.P.Backgrounds.FirstOrDefault(b => b.Id == selBg);

    void RefreshBg()
    {
        if (BgList == null) return;
        bgRefreshing = true;
        try
        {
            BgList.Items.Clear();
            if (selBg != null && SelBg == null) selBg = null;
            foreach (var b in Enumerable.Reverse(Doc.P.Backgrounds))
            {
                var cb = new CheckBox { IsChecked = b.Visible, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0), ToolTip = "Widoczny" };
                var id = b.Id;
                cb.Click += (s, e) => Doc.Change(cb.IsChecked == true ? "Pokaż podkład" : "Ukryj podkład", p => { var x = p.Backgrounds.First(q => q.Id == id); x.Visible = cb.IsChecked == true; });
                var st = new StackPanel { Orientation = Orientation.Horizontal };
                st.Children.Add(cb);
                st.Children.Add(new TextBlock { Text = (b.Kind == UtBackground.Orto ? "🛰 " : "🖼 ") + b.Name, VerticalAlignment = VerticalAlignment.Center });
                var it = new ListBoxItem { Content = st, Tag = id };
                BgList.Items.Add(it);
                if (id == selBg) it.IsSelected = true;
            }
        }
        finally { bgRefreshing = false; }
        View.SelectedBg = selBg;
        if (!keepDetails) RefreshBgDetails();
    }

    void RefreshBgDetails()
    {
        BgDetails.Children.Clear();
        var b = SelBg; if (b == null) return;
        var id = b.Id;
        void Ch(string what, Action<UtBackground> act, string merge = null) => Doc.Change(what, p => act(p.Backgrounds.First(q => q.Id == id)), merge);

        var name = new TextBox { Text = b.Name, Margin = new Thickness(0, 2, 0, 6) };
        name.LostFocus += (s, e) => { var n = name.Text.Trim(); if (n.Length > 0 && n != SelBg?.Name) Ch("Nazwa podkładu", x => x.Name = n); };
        BgDetails.Children.Add(Label("Nazwa")); BgDetails.Children.Add(name);

        var op = new Slider { Minimum = 0.05, Maximum = 1, Value = b.Opacity, Margin = new Thickness(0, 2, 0, 6), SmallChange = 0.05, LargeChange = 0.1 };
        var opT = new TextBlock { Text = $"Krycie {b.Opacity * 100:0} %" };
        op.ValueChanged += (s, e) => { opT.Text = $"Krycie {op.Value * 100:0} %"; if (bgRefreshing) return; keepDetails = true; try { Ch("Krycie podkładu", x => x.Opacity = Math.Round(op.Value, 2), "op:" + id); } finally { keepDetails = false; } };
        BgDetails.Children.Add(opT); BgDetails.Children.Add(op);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        row.Children.Add(SmallBtn("↑ Wyżej", () => Move(id, +1), "Przenieś warstwę wyżej (na wierzch)"));
        row.Children.Add(SmallBtn("↓ Niżej", () => Move(id, -1), "Przenieś warstwę niżej"));
        row.Children.Add(SmallBtn("Pokaż", () => FitBg(SelBg), "Cały podkład w oknie"));
        row.Children.Add(SmallBtn("Usuń", () => { Doc.Change("Usunięcie podkładu", p => p.Backgrounds.RemoveAll(q => q.Id == id)); Message("Podkład usunięty z projektu (pliki zostają na dysku; Ctrl+Z przywraca)"); }, "Usuń z projektu (pliki zostają na dysku)"));
        BgDetails.Children.Add(row);

        var info = new List<string>();
        if (b.Kind == UtBackground.Orto && b.Grid is UtGrid g)
        {
            int miss = Missing(b).Count;
            info.Add(g.IsGlobal ? $"Ortofotomapa {g.Mpp * 100:0.#} cm/px: {b.Tiles.Count} kafelków {UtView.Len(g.Side)} × {UtView.Len(g.Side)} (ok. {b.Tiles.Count * g.Side * g.Side / 1e6:0.##} km²)"
                : $"Ortofotomapa {g.Mpp * 100:0.#} cm/px, {g.Cols} × {g.Rows} kafelków ({UtView.Len(g.Cols * g.TileSize * g.Mpp)} × {UtView.Len(g.Rows * g.TileSize * g.Mpp)})");
            if (miss > 0)
            {
                info.Add($"Brakuje {miss} kafelków");
                BgDetails.Children.Add(SmallBtn("Dopobierz brakujące", () => _ = Refill(id), "Pobierz kafelki, których nie udało się pobrać"));
            }
            BgDetails.Children.Add(SmallBtn("Napraw puste kafelki", () => _ = RepairBlank(id), "Znajdź kafelki bez zdjęcia (Geoportal czasem zwraca pusty biały obraz) i pobierz je jeszcze raz"));
        }
        else if (b.Tiles.Count > 0)
        {
            var t = b.Tiles[0];
            info.Add($"{t.W} × {t.H} px, {t.Geo.Mpp * 100:0.##} cm/px, obrót {(Math.Abs(t.Geo.RotationDeg) < 0.005 ? 0 : t.Geo.RotationDeg):0.##}°");
            info.Add("Plik: " + t.File);
        }
        if (b.Source.Length > 0) info.Add(b.Source);
        BgDetails.Children.Add(new TextBlock { Text = string.Join("\n", info), TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, FontSize = 12, Margin = new Thickness(0, 0, 0, 8) });

        if (b.Kind == UtBackground.Image) BuildImageTools(b, Ch);
    }

    void BuildImageTools(UtBackground b, Action<string, Action<UtBackground>, string> ch)
    {
        var id = b.Id; var t = b.Tiles[0];
        BgDetails.Children.Add(Label("Położenie grafiki"));
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 6) };
        var cal = new ToggleButton { Content = "Kalibruj punktami", Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 0, 4, 0), IsChecked = View.Tool == UtTool.Kalibracja };
        cal.Click += (s, e) => SetTool(cal.IsChecked == true ? UtTool.Kalibracja : UtTool.Wskaz);
        var mv = new ToggleButton { Content = "Przesuń", Padding = new Thickness(6, 2, 6, 2), IsChecked = View.Tool == UtTool.Przesun };
        mv.Click += (s, e) => SetTool(mv.IsChecked == true ? UtTool.Przesun : UtTool.Wskaz);
        tools.Children.Add(cal); tools.Children.Add(mv);
        BgDetails.Children.Add(tools);

        var mode = new ComboBox { Margin = new Thickness(0, 2, 0, 6) };
        mode.Items.Add("podobieństwo – przesunięcie, obrót, skala (od 2 punktów)");
        mode.Items.Add("afiniczne – dwie skale i skos (od 3 punktów)");
        mode.SelectedIndex = b.CalMode == UtBackground.AffineMode ? 1 : 0;
        mode.SelectionChanged += (s, e) => ch("Sposób kalibracji", x => { x.CalMode = mode.SelectedIndex == 1 ? UtBackground.AffineMode : UtBackground.Similarity; Refit(x); }, null);
        BgDetails.Children.Add(mode);

        var cp = b.Calibration ?? new();
        if (cp.Count > 0)
        {
            var lines = cp.Select((p, i) => $"{i + 1}: odchyłka {Residual(b, p) * 100:0.#} cm").ToList();
            BgDetails.Children.Add(new TextBlock { Text = string.Join("\n", lines), FontFamily = new FontFamily("Consolas"), FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
            var r = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            r.Children.Add(SmallBtn("Usuń ostatni punkt", () => ch("Usunięcie punktu kalibracji", x => { x.Calibration.RemoveAt(x.Calibration.Count - 1); Refit(x); }, null), null));
            r.Children.Add(SmallBtn("Usuń punkty", () => ch("Usunięcie punktów kalibracji", x => x.Calibration.Clear(), null), "Położenie grafiki zostaje"));
            BgDetails.Children.Add(r);
        }

        var g = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var rot = new TextBox { Text = t.Geo.RotationDeg.ToString("0.###", CultureInfo.InvariantCulture), Margin = new Thickness(0, 0, 4, 0), ToolTip = "Obrót [°] w prawo względem północy, Enter – zastosuj (wokół środka grafiki)" };
        var scl = new TextBox { Text = (t.Geo.Mpp * 100).ToString("0.####", CultureInfo.InvariantCulture), ToolTip = "Rozmiar piksela [cm], Enter – zastosuj (wokół środka grafiki)" };
        string rot0 = rot.Text, scl0 = scl.Text;
        void Apply()
        {
            if (!double.TryParse(rot.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var r) || !double.TryParse(scl.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var c) || c <= 0) { Message("Wpisz liczby: obrót w stopniach, piksel w cm"); return; }
            ch("Obrót / skala grafiki", x =>
            {
                var tt = x.Tiles[0]; var piv = tt.Geo.World(tt.W / 2.0, tt.H / 2.0);
                // zmienia się tylko pole, które wpisano (wartości w polach są zaokrąglone)
                tt.Geo = tt.Geo.Then(piv, rot.Text == rot0 ? 0 : r - tt.Geo.RotationDeg, scl.Text == scl0 ? 1 : c / 100 / tt.Geo.Mpp, default);
            }, null);
        }
        rot.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) Apply(); };
        scl.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) Apply(); };
        var lr = new StackPanel(); lr.Children.Add(new TextBlock { Text = "Obrót [°]", FontSize = 12 }); lr.Children.Add(rot);
        var ls = new StackPanel(); ls.Children.Add(new TextBlock { Text = "Piksel [cm]", FontSize = 12 }); ls.Children.Add(scl);
        Grid.SetColumn(ls, 1); g.Children.Add(lr); g.Children.Add(ls);
        BgDetails.Children.Add(g);
    }

    static Button SmallBtn(string text, Action a, string tip)
    {
        var b = new Button { Content = text, Padding = new Thickness(5, 1, 5, 1), Margin = new Thickness(0, 0, 4, 0), ToolTip = tip };
        b.Click += (s, e) => a(); return b;
    }

    void Move(string id, int dir)
    {
        Doc.Change("Kolejność podkładów", p =>
        {
            int i = p.Backgrounds.FindIndex(q => q.Id == id), j = i + dir;
            if (i < 0 || j < 0 || j >= p.Backgrounds.Count) return;
            (p.Backgrounds[i], p.Backgrounds[j]) = (p.Backgrounds[j], p.Backgrounds[i]);
        });
    }

    public void SelectBg(string id) { selBg = id; RefreshBg(); View.InvalidateVisual(); }

    void FitBg(UtBackground b)
    {
        if (b == null || b.Tiles.Count == 0) return;
        var pts = b.Tiles.SelectMany(t => new[] { t.Geo.World(0, 0), t.Geo.World(t.W, t.H), t.Geo.World(t.W, 0), t.Geo.World(0, t.H) }).ToList();
        View.FitWorld(new XY(pts.Min(p => p.X), pts.Min(p => p.Y)), new XY(pts.Max(p => p.X), pts.Max(p => p.Y)));
    }

    // ------------------------------------------------------------------ ortofotomapa
    string BaseDir() => Doc.Dir ?? ProjectsDir();
    /// <summary>Ścieżka do zapisu w projekcie: wewnątrz katalogu projektu – względna.</summary>
    string Rel(string full)
    {
        if (Doc.Dir == null) return full;
        var r = Path.GetRelativePath(Doc.Dir, full);
        return r.StartsWith("..") || Path.IsPathRooted(r) ? full : r.Replace('\\', '/');
    }

    double SelectedRes => Resolutions[Math.Max(0, ResBox.SelectedIndex)];

    public Task StartDownload(XY a, XY b, double? res = null)
    {
        double mpp = res ?? SelectedRes;
        var cells = UtGrid.Global(mpp).CellsIn(a, b).Take(Pobieranie.MaxTiles + 1).ToList();
        return DownloadCells(mpp, cells);
    }

    /// <summary>Mozaika ortofotomapy danej rozdzielczości w stałej siatce (null – jeszcze nie ma).</summary>
    public UtBackground Mosaic(double mpp) => Doc.P.Backgrounds.FirstOrDefault(b => b.Kind == UtBackground.Orto && b.Grid is UtGrid g && g.IsGlobal && Math.Abs(g.Mpp - mpp) < 1e-9);

    /// <summary>Zmiana kafelków w projekcie (dla okna mapki).</summary>
    public event Action TilesChanged;
    /// <summary>Komunikat w pasku stanu (dla okna mapki).</summary>
    public event Action<string> MessageShown;

    /// <summary>
    /// Pobranie kafelków stałej siatki do mozaiki danej rozdzielczości (jedna warstwa „Ortofotomapa N cm”, pliki w podklady/orto_Ncm).
    /// Kafelki już pobrane są pomijane; zamówione, ale nieudane zostają w <see cref="UtBackground.Wanted"/> do dopobrania.
    /// </summary>
    public Task DownloadCells(double mpp, IReadOnlyCollection<UtCell> cells)
    {
        if (!Downloading.IsCompleted) { Message("Trwa pobieranie – poczekaj albo przerwij"); return Downloading; }
        return Downloading = DownloadCellsTask(mpp, cells.Distinct().ToList());
    }

    async Task DownloadCellsTask(double mpp, List<UtCell> cells)
    {
        var g = UtGrid.Global(mpp);
        if (cells.Count == 0) { Message("Nie zaznaczono kafelków"); return; }
        var (mx0, my0, mx1, my1) = g.TileBox(cells[0].Row, cells[0].Col);
        if (!Geo.InPoland(new XY((mx0 + mx1) / 2, (my0 + my1) / 2))) { Message("Ortofotomapa Geoportalu jest tylko dla Polski"); return; }
        var bg = Mosaic(mpp);
        var have = bg?.Tiles.Select(t => new UtCell(t.Row, t.Col)).ToHashSet() ?? new();
        var todo = cells.Where(c => !have.Contains(c)).ToList();
        if (todo.Count == 0) { Message("Te kafelki są już w projekcie"); return; }
        if (todo.Count > Pobieranie.MaxTiles) { Message($"Za dużo kafelków: {todo.Count} (najwyżej {Pobieranie.MaxTiles} naraz) – zmniejsz obszar albo rozdzielczość"); return; }
        string side = UtView.Len(g.Side);
        if (!NoPrompt && MessageBox.Show(this, $"Ortofotomapa {mpp * 100:0} cm/px: {todo.Count} kafelków {side} × {side} ({g.TileSize} px), ok. {todo.Count * 0.7:0} MB.\n\nPobrać do katalogu podklady obok projektu?", "Pobieranie ortofotomapy", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        var dir = bg?.Tiles.Count > 0 ? Path.GetDirectoryName(Doc.Resolve(bg.Tiles[0].File)) : Path.Combine(BaseDir(), "podklady", $"orto_{mpp * 100:0}cm");
        var res = await RunDownload(g, todo.Select(c => (c.Row, c.Col)).ToList(), dir);
        if (res == null) return;
        foreach (var t in res.Ok) { t.File = Rel(t.File); View.Cache.Forget(Doc.Resolve(t.File)); }
        string id = bg?.Id;
        var nb = id == null ? new UtBackground { Kind = UtBackground.Orto, Name = $"Ortofotomapa {mpp * 100:0} cm", Source = Pobieranie.OrtoAttribution, Grid = g, Wanted = new() } : null;
        if (res.Ok.Count > 0 || id != null)
            Doc.Change(id == null ? "Pobranie ortofotomapy" : "Dołożenie kafelków ortofotomapy", p =>
            {
                var x = id == null ? nb : p.Backgrounds.First(q => q.Id == id);
                if (id == null) p.Backgrounds.Add(x);
                x.Wanted ??= new();
                var w = x.Wanted.ToHashSet();
                // zamówione: pobrane i nieudane (przerwane – tylko pobrane)
                foreach (var c in res.Cancelled ? res.Ok.Select(t => new UtCell(t.Row, t.Col)) : cells) if (w.Add(c)) x.Wanted.Add(c);
                x.Tiles.AddRange(res.Ok);
                x.Tiles.Sort((u, v) => u.Row != v.Row ? u.Row.CompareTo(v.Row) : u.Col.CompareTo(v.Col));
            });
        if (id == null && res.Ok.Count > 0) SelectBg(nb.Id);
        TilesChanged?.Invoke();
        Message((res.Cancelled ? "Pobieranie przerwane – " : "Ortofotomapa: ") + $"pobrano {res.Ok.Count} z {todo.Count} kafelków {side} × {side}" + Failed(res));
    }

    static string Failed(Pobieranie.GridResult r) => r.Failed.Count == 0 ? "" : $"; nieudane {r.Failed.Count} ({string.Join("; ", r.Failed.Take(2))}{(r.Failed.Count > 2 ? "…" : "")}) – „Dopobierz brakujące” w panelu";

    async Task<Pobieranie.GridResult> RunDownload(UtGrid g, List<(int, int)> cells, string dir)
    {
        dl = new CancellationTokenSource(); CancelBtn.Visibility = Visibility.Visible;
        try
        {
            var prog = new Progress<string>(Message);
            return await Pobieranie.DownloadGrid(g, cells, dir, prog, dl.Token);
        }
        catch (Exception ex) { Message("Błąd pobierania: " + Pobieranie.Reason(ex)); return null; }
        finally { CancelBtn.Visibility = Visibility.Collapsed; dl = null; }
    }

    /// <summary>Brakujące kafelki warstwy: stała siatka – zamówione, a niepobrane; siatka lokalna (dawne pobrania) – cały prostokąt.</summary>
    public static List<UtCell> Missing(UtBackground b)
    {
        if (b?.Grid is not UtGrid g) return new();
        var have = b.Tiles.Select(t => new UtCell(t.Row, t.Col)).ToHashSet();
        var all = g.IsGlobal ? (IEnumerable<UtCell>)(b.Wanted ?? new()) : Enumerable.Range(0, g.Rows).SelectMany(r => Enumerable.Range(0, g.Cols).Select(c => new UtCell(r, c)));
        return all.Where(c => !have.Contains(c)).ToList();
    }

    public Task Refill(string id)
    {
        var b = Doc.P.Backgrounds.FirstOrDefault(x => x.Id == id);
        if (b?.Grid is UtGrid g && g.IsGlobal) return DownloadCells(g.Mpp, Missing(b));
        return Downloading.IsCompleted ? Downloading = RefillLocal(id) : Downloading;
    }

    /// <summary>Kafelki bez treści (pusty obraz z Geoportalu): pliki usunięte, kafelki zdjęte z warstwy (zostają zamówione) i pobrane ponownie.</summary>
    public async Task<int> RepairBlank(string id)
    {
        var b = Doc.P.Backgrounds.FirstOrDefault(x => x.Id == id); if (b == null) return 0;
        if (!Downloading.IsCompleted) { Message("Trwa pobieranie – poczekaj albo przerwij"); return 0; }
        Message($"Sprawdzanie {b.Tiles.Count} kafelków…");
        var files = b.Tiles.Select(t => (t.Row, t.Col, Full: Doc.Resolve(t.File))).ToList();
        var blank = await Task.Run(() => files.Where(f => Pobieranie.IsBlankFile(f.Full)).ToList());
        if (blank.Count == 0) { Message($"Wszystkie {files.Count} kafelki mają zdjęcie"); return 0; }
        foreach (var f in blank) { try { File.Delete(f.Full); } catch { } View.Cache.Forget(f.Full); }
        var set = blank.Select(f => (f.Row, f.Col)).ToHashSet();
        Doc.Change("Usunięcie pustych kafelków", p =>
        {
            var x = p.Backgrounds.First(q => q.Id == id);
            if (x.Grid?.IsGlobal == true) { x.Wanted ??= new(); foreach (var (r, c) in set) if (!x.Wanted.Contains(new UtCell(r, c))) x.Wanted.Add(new UtCell(r, c)); }
            x.Tiles.RemoveAll(t => set.Contains((t.Row, t.Col)));
        });
        TilesChanged?.Invoke();
        Message($"Pustych kafelków: {blank.Count} – pobieranie od nowa…");
        await Refill(id);
        return blank.Count;
    }

    async Task RefillLocal(string id)
    {
        var b = Doc.P.Backgrounds.FirstOrDefault(x => x.Id == id); if (b?.Grid is not UtGrid g) return;
        var cells = Missing(b).Select(c => (c.Row, c.Col)).ToList();
        if (cells.Count == 0) return;
        var dir = b.Tiles.Count > 0 ? Path.GetDirectoryName(Doc.Resolve(b.Tiles[0].File)) : Path.Combine(BaseDir(), "podklady", $"orto_{DateTime.Now:yyyyMMdd_HHmmss}");
        var res = await RunDownload(g, cells, dir);
        if (res == null || res.Ok.Count == 0) { if (res != null) Message("Nie udało się dopobrać kafelków" + Failed(res)); return; }
        foreach (var t in res.Ok) { t.File = Rel(t.File); View.Cache.Forget(Doc.Resolve(t.File)); }
        Doc.Change("Dopobranie kafelków", p => { var x = p.Backgrounds.First(q => q.Id == id); x.Tiles.AddRange(res.Ok); x.Tiles.Sort((u, v) => u.Row != v.Row ? u.Row.CompareTo(v.Row) : u.Col.CompareTo(v.Col)); });
        TilesChanged?.Invoke();
        Message($"Dopobrano {res.Ok.Count} z {cells.Count} kafelków" + Failed(res));
    }

    // ------------------------------------------------------------------ grafika
    string PickImage()
    {
        if (PickImageForTest != null) return PickImageForTest();
        if (NoPrompt) return null;
        var dlg = new OpenFileDialog { Filter = "Obrazy (*.png;*.jpg;*.jpeg;*.tif;*.tiff;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.tif;*.tiff;*.bmp;*.gif|Wszystkie pliki|*.*" };
        return dlg.ShowDialog(this) == true ? dlg.FileName : null;
    }

    static readonly string[] WorldExt = { ".pgw", ".jgw", ".tfw", ".wld", ".gfw", ".bpw" };
    /// <summary>Plik world obok obrazu: plan.pgw, plan.png.w, plan.pngw albo plan.wld.</summary>
    static string FindWorldFile(string img)
    {
        var bas = Path.Combine(Path.GetDirectoryName(img)!, Path.GetFileNameWithoutExtension(img));
        var ext = Path.GetExtension(img);
        var cands = WorldExt.Select(e => bas + e).Append(img + "w").Append(bas + ext + "w").Append(bas + ".wld");
        return cands.FirstOrDefault(File.Exists);
    }

    /// <summary>Grafika z pliku: kopia do katalogu podklady (z plikiem world), położenie z pliku world albo na środku widoku do kalibracji.</summary>
    public UtBackground ImportImage(string file)
    {
        try
        {
            int w, h;
            using (var fs = File.OpenRead(file))
            {
                var dec = BitmapDecoder.Create(fs, BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                w = dec.Frames[0].PixelWidth; h = dec.Frames[0].PixelHeight;
            }
            var world = FindWorldFile(file);
            var dir = Path.Combine(BaseDir(), "podklady");
            Directory.CreateDirectory(dir);
            var dest = Path.GetFullPath(file);
            if (!dest.StartsWith(Path.GetFullPath(dir), StringComparison.OrdinalIgnoreCase))
            {
                dest = Path.Combine(dir, Path.GetFileName(file));
                for (int i = 2; File.Exists(dest); i++) dest = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(file)}_{i}{Path.GetExtension(file)}");
                File.Copy(file, dest);
                if (world != null) File.Copy(world, Path.ChangeExtension(dest, Path.GetExtension(world)), true);
            }
            Affine geo; string how;
            if (world != null) { geo = Affine.FromWorldFile(File.ReadAllLines(world)); how = $"położenie z pliku {Path.GetFileName(world)} (PUWG 1992)"; }
            else
            {
                double vw = (View.ActualWidth - UtView.RulerLeft) * View.Mpp * 0.6, m = vw / w;
                var c = View.ToWorld(new Point((View.ActualWidth + UtView.RulerLeft) / 2, (View.ActualHeight + UtView.RulerTop) / 2));
                geo = Affine.NorthUp(c.X + h * m / 2, c.Y - w * m / 2, m);
                how = "bez pliku world – ustaw ją: „Kalibruj punktami” (najlepiej 3–4 punkty daleko od siebie)";
            }
            var bg = new UtBackground
            {
                Kind = UtBackground.Image, Name = Path.GetFileNameWithoutExtension(file), Opacity = world != null ? 1 : 0.7,
                Tiles = new() { new UtTile { File = Rel(dest), W = w, H = h, Geo = geo } },
                Calibration = new(), CalMode = UtBackground.Similarity,
            };
            Doc.Change("Grafika", p => p.Backgrounds.Add(bg));
            SelectBg(bg.Id);
            Message($"Grafika {Path.GetFileName(file)} {w} × {h} px – {how}");
            return bg;
        }
        catch (Exception ex) { Message("Nie można wczytać grafiki: " + ex.Message); return null; }
    }

    public static double Residual(UtBackground b, UtCalPoint p) => XY.Dist(b.Tiles[0].Geo.World(p.Col, p.Row), p.Target);

    /// <summary>Położenie grafiki z punktów kalibracji: 1 punkt – przesunięcie, 2+ – podobieństwo, 3+ – afiniczne (wg trybu).</summary>
    static void Refit(UtBackground b)
    {
        var cp = b.Calibration; var t = b.Tiles[0];
        if (cp == null || cp.Count == 0) return;
        var fit = Affine.Fit(cp, b.CalMode);
        if (fit is Affine f && f.Valid) { t.Geo = f; return; }
        // za mało punktów dla trybu: przesunięcie (średnio) – obraz od razu w okolicy
        var d = new XY(cp.Average(p => p.X - t.Geo.World(p.Col, p.Row).X), cp.Average(p => p.Y - t.Geo.World(p.Col, p.Row).Y));
        t.Geo = t.Geo with { C = t.Geo.C + d.Y, F = t.Geo.F + d.X };
    }

    void OnCalClick(XY w)
    {
        var b = SelBg;
        if (b == null || b.Kind != UtBackground.Image) { Message("Zaznacz grafikę na liście warstw"); return; }
        var t = b.Tiles[0];
        if (View.CalPending == null)
        {
            var (c, r) = t.Geo.Pixel(w);
            if (c < 0 || r < 0 || c > t.W || r > t.H) { Message("Kliknij punkt na grafice"); return; }
            View.CalPending = (c, r);
            Message($"Punkt {(b.Calibration?.Count ?? 0) + 1} na grafice wskazany – teraz kliknij jego prawdziwe miejsce (np. na ortofotomapie)");
            return;
        }
        var (pc, pr) = View.CalPending.Value; View.CalPending = null;
        var id = b.Id;
        Doc.Change("Punkt kalibracji", p =>
        {
            var x = p.Backgrounds.First(q => q.Id == id); x.Calibration ??= new();
            x.Calibration.Add(new UtCalPoint { Col = pc, Row = pr, X = w.X, Y = w.Y });
            Refit(x);
        });
        var nb = SelBg; int n = nb.Calibration.Count;
        Message(n == 1 ? "Punkt 1: grafika przesunięta. Wskaż drugi punkt daleko od pierwszego." :
            $"Punkty: {n}, największa odchyłka {nb.Calibration.Max(q => Residual(nb, q)) * 100:0.#} cm" + (nb.CalMode == UtBackground.AffineMode && n < 3 ? " – tryb afiniczny potrzebuje 3 punktów" : ""));
    }

    // ------------------------------------------------------------------ miarka
    void ShowMeasure()
    {
        var m = View.MeasurePts;
        if (m.Count < 2) { Message(m.Count == 1 ? "Miarka: punkt 1 – klikaj kolejne" : "Miarka wyczyszczona"); return; }
        double ground = UtView.Length(m), grid = 0; for (int i = 1; i < m.Count; i++) grid += XY.Dist(m[i - 1], m[i]);
        var k = Geo.ScaleFactor(m[0]);
        Message($"Miarka: {m.Count} pkt, długość w terenie {UtView.Len3(ground)} (w układzie {UtView.Len3(grid)}, skala odwzorowania {k.ToString("0.000000", CultureInfo.InvariantCulture)}), ostatni odcinek {UtView.Len3(Geo.GroundDistance(m[^2], m[^1]))}, azymut {Geo.Azimuth(m[^2], m[^1]):0.0}°");
    }
}

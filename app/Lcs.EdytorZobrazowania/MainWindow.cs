using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;
using Microsoft.Win32;

namespace Lcs.EdytorZobrazowania;

/// <summary>
/// Okno edytora zobrazowania (PLAN_EDYTOR_ZOBRAZOWANIA.md § 4): pasek, zakładki obrazów, panel obiektów UT (z lewej), płótno,
/// panel obrazu (z prawej), pasek stanu. UT tylko do odczytu – zmiany w UT robi się w edytorze UT.
/// </summary>
public partial class MainWindow : Window
{
    public ZobDocument Doc { get; private set; }
    public readonly ZobView View = new();
    /// <summary>Bez okien dialogowych (testy).</summary>
    public bool NoPrompt;
    /// <summary>Obraz pokazany na płótnie (id).</summary>
    public string Current { get; private set; }
    public ZobObraz Obraz => Doc?.P.Obrazy.FirstOrDefault(o => o.Id == Current);

    public readonly Button UndoBtn = new() { Content = "↶ Cofnij" }, RedoBtn = new() { Content = "↷ Ponów" }, SaveBtn = new() { Content = "Zapisz" },
        SaveAsBtn = new() { Content = "Zapisz jako…" }, ReloadUtBtn = new() { Content = "Wczytaj UT ponownie" };
    public readonly CheckBox GridBox = new() { Content = "Siatka", IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), ToolTip = "Siatka przyciągania (tylko w edytorze, nie na pulpicie)" };
    /// <summary>Zakładki obrazów (przyciski) i przyciski nowego obrazu.</summary>
    public readonly WrapPanel Tabs = new() { Margin = new Thickness(4, 2, 4, 2) };
    public readonly TreeView UtTree = new() { BorderThickness = new Thickness(0), Background = Brushes.Transparent };
    public readonly TextBlock UtHeader = new() { FontWeight = FontWeights.SemiBold, FontSize = 15, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) };
    public readonly TextBlock UtInfo = new() { Foreground = Brushes.DimGray, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };
    /// <summary>Panel obrazu (nazwa, rodzaj, posterunki, wymiary).</summary>
    public readonly StackPanel ImgPanel = new() { Margin = new Thickness(10) };
    public readonly TextBox NameBox = new() { Margin = new Thickness(0, 2, 0, 8) };
    public readonly ComboBox KindBox = new() { Margin = new Thickness(0, 2, 0, 8) };
    public readonly TextBox WidthBox = new() { Width = 70, Margin = new Thickness(0, 2, 6, 8) }, HeightBox = new() { Width = 70, Margin = new Thickness(0, 2, 0, 8) };
    public readonly StackPanel PostsPanel = new() { Margin = new Thickness(0, 2, 0, 8) };
    readonly TextBlock stCursor = new() { FontFamily = new FontFamily("Consolas"), MinWidth = 170 }, stZoom = new() { MinWidth = 120 }, stMsg = new();
    public readonly TextBlock StUt = new() { MinWidth = 200 };
    bool refreshing;

    static readonly Brush ToolbarBg = new SolidColorBrush(Color.FromRgb(0xe6, 0xe6, 0xe2)), Border = new SolidColorBrush(Color.FromRgb(0xc8, 0xc8, 0xc2)),
        TabOn = new SolidColorBrush(Color.FromRgb(0xb8, 0xd4, 0xf0)), PartFg = new SolidColorBrush(Color.FromRgb(0xb0, 0x70, 0x00)), OkFg = new SolidColorBrush(Color.FromRgb(0x10, 0x80, 0x10)), MissFg = new SolidColorBrush(Color.FromRgb(0xb0, 0x30, 0x20));

    public MainWindow(string path)
    {
        Width = 1500; Height = 900; WindowState = WindowState.Maximized;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
        Background = new SolidColorBrush(Color.FromRgb(0xf3, 0xf3, 0xf0));

        var root = new DockPanel();
        root.Children.Add(Dock(BuildToolbar(), System.Windows.Controls.Dock.Top));
        root.Children.Add(Dock(new Border { Child = Tabs, Background = ToolbarBg, BorderBrush = Border, BorderThickness = new Thickness(0, 0, 0, 1) }, System.Windows.Controls.Dock.Top));
        root.Children.Add(Dock(BuildStatus(), System.Windows.Controls.Dock.Bottom));
        root.Children.Add(Dock(BuildUtPanel(), System.Windows.Controls.Dock.Left));
        root.Children.Add(Dock(BuildImgPanel(), System.Windows.Controls.Dock.Right));
        root.Children.Add(View);
        Content = root;

        View.CursorMoved += ShowCursor;
        View.ViewChanged += ShowZoom;
        View.EmptyText = "Nowy… – zobrazowanie do projektu UT (*.ut.json)\nOtwórz… – istniejące zobrazowanie (*.zob.json)";
        GridBox.Click += (s, e) => { View.ShowGrid = GridBox.IsChecked == true; View.InvalidateVisual(); };
        PreviewKeyDown += OnKey;
        Closing += OnClosing;
        InitTory();
        InitSymbole();
        Loaded += (s, e) =>
        {
            if (path != null) OpenPath(path);
            else Refresh();
        };
    }

    static UIElement Dock(UIElement e, System.Windows.Controls.Dock d) { DockPanel.SetDock(e, d); return e; }
    static Button Btn(string text, RoutedEventHandler click, string tip) { var b = new Button { Content = text, ToolTip = tip, Padding = new Thickness(6, 2, 6, 2) }; b.Click += click; return b; }
    static TextBlock Header(string t) => new() { Text = t, FontWeight = FontWeights.SemiBold, FontSize = 15, Margin = new Thickness(0, 0, 0, 8) };
    static TextBlock Label(string t) => new() { Text = t, Foreground = Brushes.Black, Margin = new Thickness(0, 2, 0, 0) };

    UIElement BuildToolbar()
    {
        var tb = new ToolBar { Background = ToolbarBg };
        tb.Items.Add(Btn("Nowy…", (s, e) => NewDoc(), "Nowe zobrazowanie do projektu UT (*.ut.json) (Ctrl+N)"));
        tb.Items.Add(Btn("Otwórz…", (s, e) => OpenDoc(), "Otwórz zobrazowanie (*.zob.json) (Ctrl+O)"));
        SaveBtn.Click += (s, e) => SaveDoc(false); SaveBtn.ToolTip = "Zapisz zobrazowanie; poprzednia wersja zostaje w .bak (Ctrl+S)"; tb.Items.Add(SaveBtn);
        SaveAsBtn.Click += (s, e) => SaveDoc(true); SaveAsBtn.ToolTip = "Zapisz jako inny plik (Ctrl+Shift+S)"; tb.Items.Add(SaveAsBtn);
        tb.Items.Add(new Separator());
        UndoBtn.Click += (s, e) => DoUndo(); RedoBtn.Click += (s, e) => DoRedo();
        tb.Items.Add(UndoBtn); tb.Items.Add(RedoBtn);
        tb.Items.Add(new Separator());
        tb.Items.Add(GridBox);
        tb.Items.Add(Btn("Cały obraz", (s, e) => View.Fit(), "Cały obraz w oknie (Home)"));
        tb.Items.Add(Btn("1:1", (s, e) => View.Actual(), "Powiększenie 1 – jednostka obrazu = piksel monitora"));
        tb.Items.Add(new Separator());
        ReloadUtBtn.Click += (s, e) => ReloadUt(); ReloadUtBtn.ToolTip = "Wczytaj projekt UT od nowa – po zmianach w edytorze UT (obraz zostaje)"; tb.Items.Add(ReloadUtBtn);
        var tools = new ToolBar { Background = ToolbarBg };
        tools.Items.Add(ToolBtn(ZobTool.Wskaz, "Wskaż", "Wskaż (V): klik na torze – zaznaczenie i panel; przeciągnięcie wierzchołka zaznaczonego toru; strzałki – przesunięcie o krok siatki; Del – zdjęcie z obrazu"));
        tools.Items.Add(ToolBtn(ZobTool.Tor, "Tor", "Tor (T): wybierz tor w panelu UT (albo kliknij węzeł obrazu – lista torów UT), potem punkty łamanej; Shift – poziomo, Ctrl – co 45°; koniec: klik na węźle, PPM, Enter, dwuklik"));
        tools.Items.Add(ToolBtn(ZobTool.Miarka, "Miarka", "Miarka (M): klikaj punkty – długość w jednostkach obrazu i w kratkach siatki; PPM – wyczyść"));
        tools.Items.Add(ToolBtn(ZobTool.Zwrotnica, "Zwrotnica", "Zwrotnica (Z): rozjazd UT z panelu UT albo następny nieumieszczony – klik na torze zasadniczym w miejscu, gdzie odchodzi tor odgałęźny"));
        tools.Items.Add(ToolBtn(ZobTool.Sygnalizator, "Sygnalizator", "Sygnalizator (S): semafor / tarcza z panelu UT albo następny nieumieszczony – klik na linii jego toru; kierunek z UT"));
        tools.Items.Add(ToolBtn(ZobTool.Wykolejnica, "Wykolejnica", "Wykolejnica (W): klik na linii jej toru"));
        tools.Items.Add(ToolBtn(ZobTool.Koziol, "Kozioł", "Kozioł (K): klik przy końcu jego toru obrazu"));
        var z4 = new ToolBar { Background = ToolbarBg, Band = 1 };      // drugi wiersz – pierwszy jest pełny
        z4.Items.Add(ToolBtn(ZobTool.Przerwa, "Przerwa", "Przerwa izolacyjna (P): licznik osi / złącze UT – klik na linii jego toru; przerwa w linii jak na pulpicie"));
        z4.Items.Add(ToolBtn(ZobTool.OpisOdcinka, "Opis odcinka", "Opis odcinka (O): odcinek UT – klik w miejscu opisu, przy torze odcinka (Ie-104 § 8 ust. 1 – nad torem)"));
        z4.Items.Add(ToolBtn(ZobTool.NumerToru, "Numer toru", "Numer toru (N): klik na linii toru – numer w przerwie linii"));
        z4.Items.Add(ToolBtn(ZobTool.Peron, "Peron", "Peron: peron UT z panelu albo następny – przeciągnij prostokąt"));
        z4.Items.Add(ToolBtn(ZobTool.Przejazd, "Przejazd", "Przejazd: przejazd UT – klik na linii jego toru; zasięg linii przejazdu w panelu"));
        z4.Items.Add(ToolBtn(ZobTool.Szlak, "Szlak", "Strzałka blokady (B): szlak UT – klik na torze za semaforem wjazdowym, od strony szlaku"));
        z4.Items.Add(ToolBtn(ZobTool.Napis, "Napis", "Napis (X): klik w miejscu napisu – nazwa posterunku sąsiedniego, nastawnia albo dowolny tekst"));
        tools.Items.Add(new Separator());
        tools.Items.Add(Btn("✔ Sprawdź", (s, e) => Sprawdz(), "Sprawdź obraz (F7): nieumieszczone obiekty UT, przypięcia, symbole poza torem, kolejność wzdłuż km"));
        tools.Items.Add(Btn("Do symulatora…", (s, e) => DoSymulatora(), "Zapisz UT i obrazy jako układ symulatora (LcsWidzew/Data/projekty) – do wyboru przy starcie symulatora; Data/station.json zostaje bez zmian"));
        tools.Items.Add(new Separator());
        tools.Items.Add(ToolBtn(ZobTool.Lacznik, "Łącznik", "Łącznik (R): klik na torze obrazu w miejscu pierwszej zwrotnicy, potem na drugim torze – ukos 45° – pion – ukos 45°"));
        return new ToolBarTray { Background = ToolbarBg, ToolBars = { tb, tools, z4 } };
    }

    UIElement BuildUtPanel()
    {
        var sp = new StackPanel { Margin = new Thickness(10) };
        sp.Children.Add(UtHeader);
        sp.Children.Add(UtInfo);
        var dock = new DockPanel();
        dock.Children.Add(Dock(sp, System.Windows.Controls.Dock.Top));
        var prev = new StackPanel { Margin = new Thickness(10, 4, 10, 10) };
        prev.Children.Add(new TextBlock { Text = "Podgląd UT", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
        prev.Children.Add(Preview);
        dock.Children.Add(Dock(prev, System.Windows.Controls.Dock.Bottom));
        UtTree.SelectedItemChanged += (s, e) => { if (!refreshing && UtTree.SelectedItem is TreeViewItem { Tag: ZobUtObj o }) UtClicked(o); };
        dock.Children.Add(new ScrollViewer { Content = UtTree, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        return new Border { Child = dock, Width = 300, BorderBrush = Border, BorderThickness = new Thickness(0, 0, 1, 0) };
    }

    UIElement BuildImgPanel()
    {
        foreach (var r in ZobObraz.Rodzaje) KindBox.Items.Add(r.Name);
        NameBox.LostFocus += (s, e) => CommitName();
        NameBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) CommitName(); };
        KindBox.SelectionChanged += (s, e) =>
        {
            if (refreshing || Obraz == null || KindBox.SelectedIndex < 0) return;
            var k = ZobObraz.Rodzaje[KindBox.SelectedIndex].Key;
            Ch("Rodzaj obrazu", o => o.Rodzaj = k);
        };
        foreach (var box in new[] { WidthBox, HeightBox })
        {
            box.LostFocus += (s, e) => CommitSize();
            box.KeyDown += (s, e) => { if (e.Key == Key.Enter) CommitSize(); };
        }
        return new Border
        {
            Width = 280, BorderBrush = Border, BorderThickness = new Thickness(1, 0, 0, 0),
            Child = new ScrollViewer { Content = ImgPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
        };
    }

    void BuildImgPanelContent()
    {
        ImgPanel.Children.Clear();
        if (WidthBox.Parent is Panel wp) wp.Children.Clear();
        var o = Obraz;
        if (o == null) return;
        BuildTrackPanel();
        BuildSymPanel();
        BuildZ4Panel();
        if (Uwagi != null) { ImgPanel.Children.Add(Header("Sprawdź")); if (CheckPanel.Parent is Panel cp) cp.Children.Remove(CheckPanel); ImgPanel.Children.Add(CheckPanel); ImgPanel.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 10) }); }
        ImgPanel.Children.Add(Header("Obraz"));
        ImgPanel.Children.Add(Label("Nazwa")); ImgPanel.Children.Add(NameBox);
        ImgPanel.Children.Add(Label("Rodzaj")); KindBox.ToolTip = "Ie-104 § 5 – obraz szczegółowy posterunku, § 6 – obraz poglądowy obszaru"; ImgPanel.Children.Add(KindBox);
        ImgPanel.Children.Add(Label("Posterunki na obrazie"));
        ImgPanel.Children.Add(PostsPanel);
        OpisyCombo(o);
        ImgPanel.Children.Add(Label("Wymiary [j.] – szerokość × wysokość"));
        var sizes = new StackPanel { Orientation = Orientation.Horizontal };
        sizes.Children.Add(WidthBox); sizes.Children.Add(new TextBlock { Text = "×", Margin = new Thickness(0, 4, 6, 0) }); sizes.Children.Add(HeightBox);
        ImgPanel.Children.Add(sizes);
        ImgPanel.Children.Add(new TextBlock { Text = $"1 j. = 1 piksel monitora przy powiększeniu 1; siatka co {Doc.P.Siatka} j.", Foreground = Brushes.DimGray, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        row.Children.Add(Btn("◀", (s, e) => MoveImage(-1), "Przesuń zakładkę w lewo"));
        row.Children.Add(Btn("▶", (s, e) => MoveImage(1), "Przesuń zakładkę w prawo"));
        var del = Btn("Usuń obraz", (s, e) => DeleteImage(), "Usuń ten obraz ze zobrazowania (Ctrl+Z przywraca); obiekty UT zostają");
        del.Margin = new Thickness(8, 0, 0, 0); del.IsEnabled = Doc.P.Obrazy.Count > 1;
        row.Children.Add(del);
        ImgPanel.Children.Add(row);
    }

    UIElement BuildStatus()
    {
        var sb = new StatusBar { Background = ToolbarBg };
        sb.Items.Add(new StatusBarItem { Content = stCursor });
        sb.Items.Add(new Separator());
        sb.Items.Add(new StatusBarItem { Content = stZoom });
        sb.Items.Add(new Separator());
        sb.Items.Add(new StatusBarItem { Content = StUt });
        sb.Items.Add(new Separator());
        sb.Items.Add(new StatusBarItem { Content = stMsg });
        return sb;
    }

    // ------------------------------------------------------------------ odświeżanie
    void SetDoc(ZobDocument d)
    {
        if (Doc != null) Doc.Changed -= Refresh;
        Doc = d; Doc.Changed += Refresh;
        Current = d.P.Obrazy.FirstOrDefault()?.Id;
        ResetTools();
        Refresh();
        Dispatcher.BeginInvoke(() => View.Fit(), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    void Refresh()
    {
        refreshing = true;
        try
        {
            bool has = Doc != null;
            if (has && Obraz == null) Current = Doc.P.Obrazy.FirstOrDefault()?.Id;
            Title = !has ? "Edytor zobrazowania" : $"{(Dirty ? "* " : "")}{Path.GetFileName(Doc.DefaultPath)} – {Doc.Ut.Name} – Edytor zobrazowania";
            SaveBtn.IsEnabled = SaveAsBtn.IsEnabled = ReloadUtBtn.IsEnabled = has;
            UndoBtn.IsEnabled = has && Doc.UndoName != null; UndoBtn.ToolTip = has && Doc.UndoName != null ? $"Cofnij: {Doc.UndoName} (Ctrl+Z)" : "Nic do cofnięcia";
            RedoBtn.IsEnabled = has && Doc.RedoName != null; RedoBtn.ToolTip = has && Doc.RedoName != null ? $"Ponów: {Doc.RedoName} (Ctrl+Y)" : "Nic do ponowienia";
            View.Obraz = Obraz; View.Siatka = has ? Doc.P.Siatka : 8; View.Ut = Doc?.Ut;
            if (View.SelectedSym is var (sr, su) && (Obraz == null || !ZobSymbole.Lista(Obraz, sr).Any(x => x.Ut == su))) View.SelectSym(null, null);
            if (View.Selected != null && Obraz?.Tory.Any(t => t.Id == View.Selected) != true) View.Select(null);
            RefreshPreview();
            RefreshTabs();
            RefreshUt();
            RefreshImgPanel();
            RefreshCheck();
            StUt.Text = !has ? "" : Doc.UtChanged ? $"⚠ UT {Path.GetFileName(Doc.UtPath)} zmieniony po zapisie obrazu" : $"UT: {Path.GetFileName(Doc.UtPath)} (tylko do odczytu)";
            StUt.Foreground = has && Doc.UtChanged ? MissFg : Brushes.Black;
            ShowZoom();
            View.InvalidateVisual();
        }
        finally { refreshing = false; }
    }

    /// <summary>Zmiany do zapisania – także nowe, jeszcze niezapisane zobrazowanie.</summary>
    public bool Dirty => Doc != null && (Doc.Dirty || Doc.Path == null);

    void RefreshTabs()
    {
        Tabs.Children.Clear();
        if (Doc == null) return;
        foreach (var o in Doc.P.Obrazy)
        {
            var id = o.Id;
            var b = new ToggleButton
            {
                Content = (o.Nazwa.Length > 0 ? o.Nazwa : "(bez nazwy)") + (o.Rodzaj == ZobObraz.Pogladowy && !o.Nazwa.Contains("poglądowy", StringComparison.OrdinalIgnoreCase) ? " (poglądowy)" : ""),
                IsChecked = id == Current, Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 4, 0),
                Background = id == Current ? TabOn : Brushes.White, ToolTip = ZobObraz.RodzajName(o.Rodzaj),
            };
            b.Click += (s, e) => ShowImage(id);
            Tabs.Children.Add(b);
        }
        Tabs.Children.Add(Btn("+ obraz szczegółowy", (s, e) => AddImage(ZobObraz.Szczegolowy), "Nowy obraz szczegółowy (Ie-104 § 5) – posterunki wybierasz w panelu obrazu"));
        Tabs.Children.Add(new Border { Width = 4 });
        Tabs.Children.Add(Btn("+ obraz poglądowy", (s, e) => AddImage(ZobObraz.Pogladowy), "Nowy obraz poglądowy całego obszaru (Ie-104 § 6) – rysowany osobno"));
    }

    void RefreshImgPanel()
    {
        BuildImgPanelContent();
        var o = Obraz; if (o == null) return;
        if (!NameBox.IsKeyboardFocused) NameBox.Text = o.Nazwa;
        KindBox.SelectedIndex = Array.FindIndex(ZobObraz.Rodzaje, r => r.Key == o.Rodzaj);
        WidthBox.Text = o.Szer.ToString(CultureInfo.InvariantCulture); HeightBox.Text = o.Wys.ToString(CultureInfo.InvariantCulture);
        PostsPanel.Children.Clear();
        if (Doc.Ut.Posts.Count == 0)
            PostsPanel.Children.Add(new TextBlock { Text = "W UT nie ma posterunków – obraz obejmuje cały obszar. Posterunki dodaje się w edytorze UT.", Foreground = Brushes.DimGray, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        foreach (var x in Doc.Ut.Posts)
        {
            var pid = x.Id;
            var cb = new CheckBox { Content = $"{x.Kod} {x.Nazwa}".Trim(), IsChecked = o.Posterunki.Contains(pid), Margin = new Thickness(0, 2, 0, 2) };
            cb.Click += (s, e) =>
            {
                bool on = cb.IsChecked == true;
                Ch(on ? "Posterunek na obrazie" : "Posterunek zdjęty z obrazu", q => { q.Posterunki.Remove(pid); if (on) q.Posterunki.Add(pid); q.Posterunki.Sort((a, b) => Doc.Ut.Posts.FindIndex(p => p.Id == a).CompareTo(Doc.Ut.Posts.FindIndex(p => p.Id == b))); });
            };
            PostsPanel.Children.Add(cb);
        }
    }

    /// <summary>Panel UT: obiekty posterunków obrazu po grupach, „na obrazie” / „brak”.</summary>
    void RefreshUt()
    {
        UtTree.Items.Clear();
        var o = Obraz;
        if (Doc == null || o == null) { UtHeader.Text = "Obiekty UT"; UtInfo.Text = ""; return; }
        var all = ZobUt.Obiekty(Doc.Ut, o);
        var posts = o.Posterunki.Select(id => Doc.Ut.Posts.FirstOrDefault(p => p.Id == id)).Where(p => p != null).Select(p => p.Kod).ToList();
        UtHeader.Text = "Obiekty UT – " + (posts.Count > 0 ? string.Join(", ", posts) : "cały obszar");
        UtInfo.Text = $"Na obrazie {all.Count(x => x.NaObrazie)} z {all.Count}. Zielone – na obrazie, brązowe – tor w części, czerwone – brak (do narysowania). Dane z UT tylko do odczytu.";
        foreach (var g in ZobUt.Grupy)
        {
            var items = all.Where(x => x.Grupa == g).ToList();
            if (items.Count == 0) continue;
            var gi = new TreeViewItem { Header = $"{g} ({items.Count(x => x.NaObrazie)}/{items.Count})", IsExpanded = true, FontWeight = FontWeights.SemiBold };
            foreach (var x in items)
            {
                var it = new TreeViewItem
                {
                    // obiekt wybrany ostatnio – pogrubiony (bez zaznaczania w drzewie: TreeView zgłosiłby to później jak klik)
                    Header = $"{x.Opis} – {(x.NaObrazie ? "na obrazie" : x.Czesc ?? "brak")}", Tag = x, FontWeight = x.Id == utSelected ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = x.NaObrazie ? OkFg : x.Czesc != null ? PartFg : MissFg,
                };
                gi.Items.Add(it);
            }
            UtTree.Items.Add(gi);
        }
    }

    /// <summary>Obiekt UT wybrany w panelu (zostaje wybrany po odświeżeniu drzewa).</summary>
    string utSelected;

    void ShowCursor() => stCursor.Text = View.CursorPt is var (x, y) ? $"x = {x,5} j.  y = {y,5} j." : "";
    void ShowZoom() => stZoom.Text = Doc == null ? "" : $"powiększenie {View.Zoom * 100:0} %";
    public void Message(string m) => stMsg.Text = m;
    public string LastMessage => stMsg.Text;

    // ------------------------------------------------------------------ zmiany obrazu
    void Ch(string what, Action<ZobObraz> act)
    {
        var id = Current;
        Doc.Change(what, p => { var o = p.Obrazy.FirstOrDefault(x => x.Id == id); if (o != null) act(o); });
    }

    void CommitName()
    {
        if (refreshing || Obraz == null) return;
        var n = NameBox.Text.Trim();
        if (n != Obraz.Nazwa) Ch("Nazwa obrazu", o => o.Nazwa = n);
    }

    void CommitSize()
    {
        if (refreshing || Obraz == null) return;
        if (!int.TryParse(WidthBox.Text.Trim(), out int w) || !int.TryParse(HeightBox.Text.Trim(), out int h) || w < 200 || h < 200 || w > 20000 || h > 20000)
        {
            Message("Wymiary obrazu: liczby całkowite od 200 do 20000 j.");
            WidthBox.Text = Obraz.Szer.ToString(CultureInfo.InvariantCulture); HeightBox.Text = Obraz.Wys.ToString(CultureInfo.InvariantCulture);
            return;
        }
        int g = Doc.P.Siatka; w = (int)Math.Round((double)w / g) * g; h = (int)Math.Round((double)h / g) * g;
        if (w != Obraz.Szer || h != Obraz.Wys) Ch("Wymiary obrazu", o => { o.Szer = w; o.Wys = h; });
        else { WidthBox.Text = w.ToString(CultureInfo.InvariantCulture); HeightBox.Text = h.ToString(CultureInfo.InvariantCulture); }
    }

    public void ShowImage(string id)
    {
        if (Doc == null || Doc.P.Obrazy.All(o => o.Id != id)) return;
        if (id != Current) ResetTools();
        Current = id; Refresh(); View.Fit();
        Message($"Obraz „{Obraz.Nazwa}”");
    }

    public ZobObraz AddImage(string rodzaj)
    {
        if (Doc == null) return null;
        int n = Doc.P.Obrazy.Count + 1;
        var o = new ZobObraz
        {
            Rodzaj = rodzaj,
            Nazwa = rodzaj == ZobObraz.Pogladowy ? "Obraz poglądowy" : $"Obraz {n}",
            // poglądowy – cały obszar (wszystkie posterunki); szczegółowy – posterunki wybiera człowiek
            Posterunki = rodzaj == ZobObraz.Pogladowy ? Doc.Ut.Posts.Select(x => x.Id).ToList() : new(),
        };
        Doc.Change(rodzaj == ZobObraz.Pogladowy ? "Nowy obraz poglądowy" : "Nowy obraz szczegółowy", p => p.Obrazy.Add(o));
        ShowImage(o.Id);
        Message($"Nowy obraz „{o.Nazwa}” – " + (rodzaj == ZobObraz.Pogladowy ? "cały obszar" : "wybierz posterunki w panelu obrazu"));
        return o;
    }

    public void DeleteImage()
    {
        if (Doc == null || Obraz == null || Doc.P.Obrazy.Count < 2) return;
        var id = Current; var name = Obraz.Nazwa;
        int i = Doc.P.Obrazy.FindIndex(o => o.Id == id);
        Doc.Change("Usunięcie obrazu", p => p.Obrazy.RemoveAll(o => o.Id == id));
        ShowImage(Doc.P.Obrazy[Math.Max(0, i - 1)].Id);
        Message($"Usunięto obraz „{name}” (Ctrl+Z przywraca)");
    }

    void MoveImage(int d)
    {
        var id = Current; int i = Doc.P.Obrazy.FindIndex(o => o.Id == id), j = i + d;
        if (i < 0 || j < 0 || j >= Doc.P.Obrazy.Count) return;
        Doc.Change("Kolejność obrazów", p => { var o = p.Obrazy[i]; p.Obrazy.RemoveAt(i); p.Obrazy.Insert(j, o); });
    }

    public void DoUndo()
    {
        if (Doc == null) return;
        var n = Doc.UndoName; if (Doc.Undo()) Message("Cofnięto: " + n);
    }
    public void DoRedo()
    {
        if (Doc == null) return;
        var n = Doc.RedoName; if (Doc.Redo()) Message("Ponowiono: " + n);
    }

    // ------------------------------------------------------------------ pliki
    bool ConfirmDiscard()
    {
        if (!Dirty || NoPrompt) return true;
        var r = MessageBox.Show(this, $"Zapisać zobrazowanie „{Path.GetFileName(Doc.DefaultPath)}”?", "Edytor zobrazowania", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (r == MessageBoxResult.Cancel) return false;
        return r == MessageBoxResult.No || SaveDoc(false);
    }

    void NewDoc()
    {
        if (!ConfirmDiscard()) return;
        var dlg = new OpenFileDialog { Title = "Projekt UT dla nowego zobrazowania", Filter = "Układ torowy (*.ut.json)|*.ut.json|Wszystkie pliki|*.*", InitialDirectory = ProjectsDir() };
        if (dlg.ShowDialog(this) != true) return;
        NewFromUt(dlg.FileName);
    }

    /// <summary>Nowe zobrazowanie do projektu UT (gdy obok UT jest już zobrazowanie – pytanie, czy je otworzyć).</summary>
    public bool NewFromUt(string utPath)
    {
        var existing = ZobDocument.ZobPathFor(utPath);
        if (File.Exists(existing) && !NoPrompt &&
            MessageBox.Show(this, $"Obok projektu UT jest już zobrazowanie {Path.GetFileName(existing)}. Otworzyć je?", "Nowe zobrazowanie", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            return OpenPath(existing);
        try
        {
            SetDoc(ZobDocument.New(utPath));
            Message($"Nowe zobrazowanie do {Path.GetFileName(utPath)}: obraz „{Obraz.Nazwa}”" + (Doc.Ut.Posts.Count == 0 ? " (w UT nie ma posterunków – obraz obejmuje cały obszar)" : ""));
            return true;
        }
        catch (Exception ex) { Message("Nie można wczytać UT: " + ex.Message); if (!NoPrompt) MessageBox.Show(this, ex.Message, "Projekt UT"); return false; }
    }

    void OpenDoc()
    {
        if (!ConfirmDiscard()) return;
        var dlg = new OpenFileDialog { Filter = "Zobrazowanie (*.zob.json)|*.zob.json|Układ torowy – nowe zobrazowanie (*.ut.json)|*.ut.json|Wszystkie pliki|*.*", InitialDirectory = ProjectsDir() };
        if (dlg.ShowDialog(this) != true) return;
        OpenPath(dlg.FileName);
    }

    /// <summary>Otwarcie <c>*.zob.json</c> (albo <c>*.ut.json</c> – nowe zobrazowanie do niego).</summary>
    public bool OpenPath(string path)
    {
        if (path.EndsWith(".ut.json", StringComparison.OrdinalIgnoreCase)) return NewFromUt(path);
        try
        {
            SetDoc(ZobDocument.Load(path));
            Message("Otwarto " + Path.GetFileName(path));
            if (Doc.UtChanged)
            {
                var m = $"Projekt UT {Path.GetFileName(Doc.UtPath)} był zapisany po ostatnim zapisie zobrazowania – sprawdź przypięcia obrazu do UT.";
                Message(m);
                if (!NoPrompt) MessageBox.Show(this, m, "Zmiana UT", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            return true;
        }
        catch (Exception ex) { Message("Nie można otworzyć: " + ex.Message); if (!NoPrompt) MessageBox.Show(this, ex.Message, "Otwieranie zobrazowania"); return false; }
    }

    public bool SaveDoc(bool askName, string path = null)
    {
        if (Doc == null) return false;
        path ??= Doc.Path;
        if (path == null || askName)
        {
            if (NoPrompt && path == null && !askName) path = Doc.DefaultPath;
            else if (NoPrompt) return false;
            else
            {
                var dlg = new SaveFileDialog { Filter = "Zobrazowanie (*.zob.json)|*.zob.json", InitialDirectory = Path.GetDirectoryName(Doc.DefaultPath), FileName = Path.GetFileName(Doc.DefaultPath) };
                if (dlg.ShowDialog(this) != true) return false;
                path = dlg.FileName;
            }
        }
        CommitName();
        try { Doc.Save(path); Message("Zapisano " + Path.GetFileName(path)); return true; }
        catch (Exception ex) { Message("Błąd zapisu: " + ex.Message); if (!NoPrompt) MessageBox.Show(this, ex.Message, "Zapis zobrazowania"); return false; }
    }

    public void ReloadUt()
    {
        if (Doc == null) return;
        try { Doc.ReloadUt(); Message($"Wczytano UT {Path.GetFileName(Doc.UtPath)} od nowa"); }
        catch (Exception ex) { Message("Nie można wczytać UT: " + ex.Message); if (!NoPrompt) MessageBox.Show(this, ex.Message, "Projekt UT"); }
    }

    /// <summary>Katalog projektów: <c>projekty_ut</c> w katalogu repozytorium (szukany w górę od programu), inaczej Dokumenty.</summary>
    public static string ProjectsDir()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "PLAN_EDYTOR_ZOBRAZOWANIA.md")))
            {
                var p = Path.Combine(d.FullName, "projekty_ut"); Directory.CreateDirectory(p); return p;
            }
        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    void OnClosing(object s, CancelEventArgs e) { if (!ConfirmDiscard()) e.Cancel = true; }

    void OnKey(object s, KeyEventArgs e)
    {
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control), shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        bool inText = Keyboard.FocusedElement is TextBox;
        if (ctrl && e.Key == Key.Z && !inText) { if (shift) DoRedo(); else DoUndo(); e.Handled = true; }
        else if (ctrl && e.Key == Key.Y && !inText) { DoRedo(); e.Handled = true; }
        else if (ctrl && e.Key == Key.S) { SaveDoc(shift); e.Handled = true; }
        else if (ctrl && e.Key == Key.O) { OpenDoc(); e.Handled = true; }
        else if (ctrl && e.Key == Key.N) { NewDoc(); e.Handled = true; }
        else if (!inText && e.Key == Key.Home) { View.Fit(); e.Handled = true; }
        else if (e.Key == Key.F7) { Sprawdz(); e.Handled = true; }
        else if (!inText && !ctrl && ToolKey(e.Key, shift)) e.Handled = true;
    }
}

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

namespace Lcs.EdytorZobrazowania2;

/// <summary>
/// Okno nowego edytora zobrazowania (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 8): pasek, zakładki obrazów, paleta (z lewej), płótno na większości
/// ekranu, prawy panel w zakładkach (Właściwości, Do wyjaśnienia, UT, Obraz), pasek stanu. Rysuje się bez UT; UT tylko do odczytu.
/// Etap E1: szkielet – plik w wersji 2, płótno z siatką, prowadnice rzędów, strefy zarezerwowane, cofnij / ponów;
/// etap E2: pióro toru, rozjazdy i skrzyżowania, zaznaczanie, przesuwanie, kopiuj / wklej, rozsuń rzędy (MainWindow.Rysunek.cs).
/// </summary>
public partial class MainWindow : Window
{
    public ObrDocument Doc { get; private set; }
    public readonly ObrView View = new();
    /// <summary>Bez okien dialogowych (testy).</summary>
    public bool NoPrompt;
    /// <summary>Obraz pokazany na płótnie (id).</summary>
    public string Current { get; private set; }
    public ObrObraz Obraz => Doc?.P.Obrazy.FirstOrDefault(o => o.Id == Current);

    public readonly Button UndoBtn = new() { Content = "↶ Cofnij" }, RedoBtn = new() { Content = "↷ Ponów" }, SaveBtn = new() { Content = "Zapisz" },
        SaveAsBtn = new() { Content = "Zapisz jako…" }, ReloadUtBtn = new() { Content = "Wczytaj UT ponownie" };
    public readonly CheckBox GridBox = new() { Content = "Siatka", IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), ToolTip = "Siatka przyciągania co 8 j. (tylko w edytorze, nie na pulpicie)" },
        RzedyBox = new() { Content = "Prowadnice", IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), ToolTip = "Prowadnice rzędów torów (tylko w edytorze, nie na pulpicie)" };
    /// <summary>Zakładki obrazów (przyciski) i przyciski nowego obrazu.</summary>
    public readonly WrapPanel Tabs = new() { Margin = new Thickness(4, 2, 4, 2) };
    /// <summary>Prawy panel: Właściwości, Do wyjaśnienia, UT, Obraz.</summary>
    public readonly TabControl Side = new() { BorderThickness = new Thickness(0) };
    public readonly TabItem TabWlasciwosci = new() { Header = "Właściwości" }, TabWyjasnienia = new() { Header = "Do wyjaśnienia" }, TabUt = new() { Header = "UT" }, TabObraz = new() { Header = "Obraz" };
    /// <summary>Zakładka Obraz.</summary>
    public readonly StackPanel ImgPanel = new() { Margin = new Thickness(10) };
    public readonly StackPanel UtPanel = new() { Margin = new Thickness(10) };
    public readonly TextBox NameBox = new() { Margin = new Thickness(0, 2, 0, 8) };
    public readonly ComboBox KindBox = new() { Margin = new Thickness(0, 2, 0, 8) }, OpisyBox = new() { Margin = new Thickness(0, 2, 0, 8) };
    public readonly TextBox WidthBox = new() { Width = 70, Margin = new Thickness(0, 2, 6, 8) }, HeightBox = new() { Width = 70, Margin = new Thickness(0, 2, 0, 8) };
    public readonly TextBox RzedyText = new() { Margin = new Thickness(0, 2, 0, 2), ToolTip = "Rzędne y prowadnic [j.], oddzielone spacją lub przecinkiem; zaokrąglane do siatki" };
    public readonly StackPanel PostsPanel = new() { Margin = new Thickness(0, 2, 0, 8) }, StrefyPanel = new() { Margin = new Thickness(0, 2, 0, 4) };
    /// <summary>Przyciski narzędzi i elementów palety (tag – narzędzie albo opis etapu).</summary>
    public readonly Dictionary<ObrTool, ToggleButton> ToolBtns = new();
    readonly TextBlock stCursor = new() { FontFamily = new FontFamily("Consolas"), MinWidth = 170 }, stZoom = new() { MinWidth = 120 }, stMsg = new();
    public readonly TextBlock StUt = new() { MinWidth = 200 };
    bool refreshing;

    static readonly Brush ToolbarBg = new SolidColorBrush(Color.FromRgb(0xe6, 0xe6, 0xe2)), Border = new SolidColorBrush(Color.FromRgb(0xc8, 0xc8, 0xc2)),
        TabOn = new SolidColorBrush(Color.FromRgb(0xb8, 0xd4, 0xf0)), MissFg = new SolidColorBrush(Color.FromRgb(0xb0, 0x30, 0x20)), PaletteBg = new SolidColorBrush(Color.FromRgb(0xee, 0xee, 0xea));

    public MainWindow(string path)
    {
        Width = 1500; Height = 900; WindowState = WindowState.Maximized;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
        Background = new SolidColorBrush(Color.FromRgb(0xf3, 0xf3, 0xf0));

        var root = new DockPanel();
        root.Children.Add(Dock(BuildToolbar(), System.Windows.Controls.Dock.Top));
        root.Children.Add(Dock(new Border { Child = Tabs, Background = ToolbarBg, BorderBrush = Border, BorderThickness = new Thickness(0, 0, 0, 1) }, System.Windows.Controls.Dock.Top));
        root.Children.Add(Dock(BuildStatus(), System.Windows.Controls.Dock.Bottom));
        root.Children.Add(Dock(BuildPalette(), System.Windows.Controls.Dock.Left));
        root.Children.Add(Dock(BuildSide(), System.Windows.Controls.Dock.Right));
        root.Children.Add(BuildCanvas());
        Content = root;

        View.CursorMoved += ShowCursor;
        View.ViewChanged += ShowZoom;
        View.ToolChanged += RefreshTools;
        View.RzadAdded += y => Ch($"Prowadnica rzędu {y}", o => { if (!o.Rzedy.Contains(y)) { o.Rzedy.Add(y); o.Rzedy.Sort(); } });
        View.RzadMoved += (a, b) => Ch($"Prowadnica {a} → {b}", o => { o.Rzedy.Remove(a); if (!o.Rzedy.Contains(b)) o.Rzedy.Add(b); o.Rzedy.Sort(); });
        View.RzadRemoved += y => { Ch($"Usunięcie prowadnicy {y}", o => o.Rzedy.Remove(y)); Message($"Usunięto prowadnicę {y} (Ctrl+Z przywraca)"); };
        InitRysunek();
        InitSymbole();
        InitKlocki();
        View.EmptyText = "Nowy… – zobrazowanie do projektu UT (*.ut.json)\nOtwórz… – istniejące zobrazowanie (*.zob.json)";
        GridBox.Click += (s, e) => { View.ShowGrid = GridBox.IsChecked == true; View.InvalidateVisual(); };
        RzedyBox.Click += (s, e) => { View.ShowRzedy = RzedyBox.IsChecked == true; View.InvalidateVisual(); };
        PreviewKeyDown += OnKey;
        Closing += OnClosing;
        Loaded += (s, e) =>
        {
            if (path != null) OpenPath(path);
            else Refresh();
        };
    }

    static UIElement Dock(UIElement e, System.Windows.Controls.Dock d) { DockPanel.SetDock(e, d); return e; }
    static Button Btn(string text, RoutedEventHandler click, string tip) { var b = new Button { Content = text, ToolTip = tip, Padding = new Thickness(6, 2, 6, 2) }; b.Click += click; return b; }
    static TextBlock Header(string t) => new() { Text = t, FontWeight = FontWeights.SemiBold, FontSize = 15, Margin = new Thickness(0, 0, 0, 8) };
    static TextBlock Label(string t) => new() { Text = t, Foreground = Brushes.Black, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
    static TextBlock Note(string t) => new() { Text = t, Foreground = Brushes.DimGray, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };

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
        tb.Items.Add(RzedyBox);
        tb.Items.Add(Btn("Cały obraz", (s, e) => View.Fit(), "Cały obraz w oknie (Home)"));
        tb.Items.Add(Btn("1:1", (s, e) => View.Actual(), "Powiększenie 1 – jednostka obrazu = piksel monitora"));
        tb.Items.Add(new Separator());
        // zapowiedź kolejnych etapów (plan § 10) – przyciski widać, działają od swojego etapu
        SprawdzBtn.Click += (s, e) => Sprawdz(); tb.Items.Add(SprawdzBtn);
        PrzypnijBtn.Click += (s, e) => Przypnij(); tb.Items.Add(PrzypnijBtn);
        PrzebiegiBtn.Click += (s, e) => Przebiegi(); tb.Items.Add(PrzebiegiBtn);
        PodgladBtn.Click += (s, e) => Podglad(); tb.Items.Add(PodgladBtn);
        SymBtn.Click += (s, e) => DoSymulatora(); tb.Items.Add(SymBtn);
        tb.Items.Add(Btn("PNG…", (s, e) => ExportPng(), "Obraz jak na pulpicie do pliku PNG (1 j. = 1 piksel, bez siatki i znaczników edytora)"));
        tb.Items.Add(Btn("Drukuj…", (s, e) => PrintObraz(), "Wydruk obrazu na jednej kartce, poziomo"));
        tb.Items.Add(new Separator());
        ReloadUtBtn.Click += (s, e) => ReloadUt(); ReloadUtBtn.ToolTip = "Wczytaj projekt UT od nowa – po zmianach w edytorze UT (obraz zostaje)"; tb.Items.Add(ReloadUtBtn);
        return new ToolBarTray { Background = ToolbarBg, ToolBars = { tb } };
    }

    static Button Later(string text, string tip) => new() { Content = text, ToolTip = tip, IsEnabled = false, Padding = new Thickness(6, 2, 6, 2) };

    /// <summary>
    /// Paleta (plan § 5 „oba naraz”, § 8): narzędzia, symbole z klawiszami 1…9 (kolejność wg Ie-104 – decyzja 2026-10-22), klocki.
    /// Działają narzędzia, symbole i opisy; klocki są zapowiedzią z numerem etapu.
    /// </summary>
    UIElement BuildPalette()
    {
        var sp = new StackPanel { Margin = new Thickness(6) };
        sp.Children.Add(new TextBlock { Text = "Narzędzia", FontWeight = FontWeights.SemiBold, Margin = new Thickness(2, 0, 0, 4) });
        sp.Children.Add(ToolBtn(ObrTool.Wskaz, "Wskaż", "V", "Wskaż (V / Esc): klik – zaznaczenie, Shift + klik – dodaj / zdejmij, prostokąt na pustym miejscu; przeciąganie zaznaczenia albo węzła; tor poziomy – cały rząd w pionie z łącznikami; strzałki – o 8 j. (Shift – o 40 j.); Del – usuń; PPM – menu; środkowy przycisk – przesuwanie widoku"));
        sp.Children.Add(ToolBtn(ObrTool.Tor, "Pióro toru", "T", "Pióro toru (T): klik – klik, kierunek co 45° najbliższy myszy, przyklejanie do siatki, prowadnic i torów; tor zaczęty albo skończony na innym torze – rozjazd powstaje sam; koniec – klik na torze, Enter, PPM, dwuklik; Backspace – cofnij punkt; Esc – przerwij"));
        sp.Children.Add(ToolBtn(ObrTool.Prowadnica, "Prowadnica rzędu", "R", "Prowadnica rzędu (R): klik – nowa prowadnica w tej rzędnej; przeciągnięcie – przesunięcie (poza obraz – usunięcie); PPM – usunięcie"));
        BuildSymbolePalette(sp);
        sp.Children.Add(new TextBlock { Text = "Klocki", FontWeight = FontWeights.SemiBold, Margin = new Thickness(2, 10, 0, 4) });
        BuildKlockiSzlaku(sp);
        BuildKlockiTorow(sp);
        return new Border
        {
            Width = 170, Background = PaletteBg, BorderBrush = Border, BorderThickness = new Thickness(0, 0, 1, 0),
            Child = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
        };
    }

    static Grid PaletteRow(string text, string key)
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.Children.Add(new TextBlock { Text = text });
        var k = new TextBlock { Text = key, Foreground = Brushes.Gray, Margin = new Thickness(6, 0, 0, 0) };
        Grid.SetColumn(k, 1); g.Children.Add(k);
        return g;
    }

    ToggleButton ToolBtn(ObrTool t, string text, string key, string tip)
    {
        var b = new ToggleButton { Content = PaletteRow(text, key), ToolTip = tip, Focusable = false, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(6, 3, 6, 3), Margin = new Thickness(0, 1, 0, 1) };
        b.Click += (s, e) => SetTool(t);
        ToolBtns[t] = b;
        return b;
    }

    static Button PaletteLater(string text, string key, string tip)
    {
        var b = new Button { Content = PaletteRow(text, key), HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(6, 3, 6, 3), Margin = new Thickness(0, 1, 0, 1), IsEnabled = false, ToolTip = tip };
        ToolTipService.SetShowOnDisabled(b, true);
        return b;
    }

    UIElement BuildSide()
    {
        TabWlasciwosci.Content = new ScrollViewer { Content = PropPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        TabWyjasnienia.Content = new ScrollViewer { Content = WyjPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        TabUt.Content = new ScrollViewer { Content = UtPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        TabObraz.Content = new ScrollViewer { Content = ImgPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Side.Items.Add(TabWlasciwosci); Side.Items.Add(TabWyjasnienia); Side.Items.Add(TabUt); Side.Items.Add(TabObraz);
        Side.SelectedItem = TabObraz;
        Side.SelectionChanged += (s, e) => { if (e.Source == Side) RefreshWidocznyPanel(); };

        foreach (var r in ZobObraz.Rodzaje) KindBox.Items.Add(r.Name);
        foreach (var r in ZobObraz.OpisyRodzaje) OpisyBox.Items.Add(r.Name);
        NameBox.LostFocus += (s, e) => CommitName();
        NameBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) CommitName(); };
        KindBox.SelectionChanged += (s, e) =>
        {
            if (refreshing || Obraz == null || KindBox.SelectedIndex < 0) return;
            var k = ZobObraz.Rodzaje[KindBox.SelectedIndex].Key;
            // obraz poglądowy – cały obszar (Ie-104 § 6): wszystkie posterunki UT
            var wszystkie = Doc.Ut.Posts.Select(x => x.Id).ToList();
            Ch("Rodzaj obrazu", o => { o.Rodzaj = k; if (k == ZobObraz.Pogladowy) o.Posterunki = wszystkie; });
        };
        OpisyBox.SelectionChanged += (s, e) =>
        {
            if (refreshing || Obraz == null || OpisyBox.SelectedIndex < 0) return;
            var k = ZobObraz.OpisyRodzaje[OpisyBox.SelectedIndex].Key;
            Ch("Opisy odcinków", o => o.OpisyOdcinkow = k);
        };
        foreach (var box in new[] { WidthBox, HeightBox })
        {
            box.LostFocus += (s, e) => CommitSize();
            box.KeyDown += (s, e) => { if (e.Key == Key.Enter) CommitSize(); };
        }
        RzedyText.LostFocus += (s, e) => CommitRzedy();
        RzedyText.KeyDown += (s, e) => { if (e.Key == Key.Enter) CommitRzedy(); };
        return new Border { Width = 300, BorderBrush = Border, BorderThickness = new Thickness(1, 0, 0, 0), Child = Side };
    }

    void BuildImgPanelContent()
    {
        ImgPanel.Children.Clear();
        if (WidthBox.Parent is Panel wp) wp.Children.Clear();
        var o = Obraz;
        if (o == null) { ImgPanel.Children.Add(Note("Brak otwartego zobrazowania.")); return; }
        ImgPanel.Children.Add(Header("Obraz"));
        ImgPanel.Children.Add(Label("Nazwa")); ImgPanel.Children.Add(NameBox);
        ImgPanel.Children.Add(Label("Rodzaj")); KindBox.ToolTip = "Ie-104 § 5 – obraz szczegółowy posterunku, § 6 – obraz poglądowy obszaru"; ImgPanel.Children.Add(KindBox);
        ImgPanel.Children.Add(Label("Posterunki na obrazie"));
        ImgPanel.Children.Add(PostsPanel);
        ImgPanel.Children.Add(Label("Opisy odcinków na pulpicie")); ImgPanel.Children.Add(OpisyBox);
        ImgPanel.Children.Add(Label("Wymiary [j.] – szerokość × wysokość"));
        var sizes = new StackPanel { Orientation = Orientation.Horizontal };
        sizes.Children.Add(WidthBox); sizes.Children.Add(new TextBlock { Text = "×", Margin = new Thickness(0, 4, 6, 0) }); sizes.Children.Add(HeightBox);
        ImgPanel.Children.Add(sizes);
        ImgPanel.Children.Add(Note($"1 j. = 1 piksel monitora przy powiększeniu 1; siatka co {Doc.P.Siatka} j."));

        ImgPanel.Children.Add(Label("Prowadnice rzędów – y [j.]"));
        ImgPanel.Children.Add(RzedyText);
        ImgPanel.Children.Add(Note("Poziome linie, do których przykleja się tor (tylko w edytorze). Narzędzie Prowadnica (R) – klik na płótnie."));

        ImgPanel.Children.Add(Label("Strefy zarezerwowane przez pulpit – x, y, szer., wys. [j.]"));
        ImgPanel.Children.Add(StrefyPanel);
        var addS = Btn("+ strefa", (s, e) => AddStrefa(), "Nowy prostokąt zarezerwowany (np. zegar) – tory nie powinny na niego wchodzić");
        addS.HorizontalAlignment = HorizontalAlignment.Left;
        ImgPanel.Children.Add(addS);
        ImgPanel.Children.Add(Note("Ramy ekranu (panel poleceń, zegar, serwery) rysuje pulpit, nie obraz."));

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        row.Children.Add(Btn("◀", (s, e) => MoveImage(-1), "Przesuń zakładkę w lewo"));
        row.Children.Add(Btn("▶", (s, e) => MoveImage(1), "Przesuń zakładkę w prawo"));
        var kop = Btn("Kopiuj obraz", (s, e) => CopyImage(), "Nowy obraz – kopia tego obrazu z rysunkiem i przypięciami, zakładka obok (np. obraz poglądowy z obrazu szczegółowego)");
        kop.Margin = new Thickness(8, 0, 0, 0);
        row.Children.Add(kop);
        var del = Btn("Usuń obraz", (s, e) => DeleteImage(), "Usuń ten obraz ze zobrazowania (Ctrl+Z przywraca)");
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

    // ------------------------------------------------------------------ narzędzia
    public void SetTool(ObrTool t)
    {
        View.Tool = t;
        Message(t switch
        {
            ObrTool.Prowadnica => "Prowadnica: klik – nowa prowadnica; przeciągnij – przesuń (poza obraz – usuń); PPM – usuń; Esc – Wskaż",
            ObrTool.Symbol => $"{ObrSymbole.NazwaTypu(View.SymbolTyp)}: klik na torze",
            ObrTool.Tor => "Pióro: klik – pierwszy punkt (na torze – rozjazd), dalej punkty co 45°; koniec – klik na torze, Enter, PPM, dwuklik",
            _ => "Wskaż: klik, Shift + klik, prostokąt; przeciągnij zaznaczenie; PPM – menu; środkowy przycisk – przesuwanie widoku",
        });
    }

    void RefreshTools()
    {
        foreach (var (t, b) in ToolBtns) b.IsChecked = View.Tool == t;
        foreach (var (t, b) in SymBtns) b.IsChecked = View.Tool == ObrTool.Symbol && View.SymbolTyp == t;
        foreach (var (t, b) in KlocekBtns) b.IsChecked = View.Tool == ObrTool.Klocek && KlocekTyp == t;
    }

    // ------------------------------------------------------------------ odświeżanie
    void SetDoc(ObrDocument d)
    {
        if (Doc != null) Doc.Changed -= Refresh;
        przebiegiWin?.Close();
        Doc = d; Doc.Changed += Refresh;
        Current = d.P.Obrazy.FirstOrDefault()?.Id;
        View.Tool = ObrTool.Wskaz;
        Refresh();
        Dispatcher.BeginInvoke(() => View.Fit(), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    bool panelyNieaktualne = true;
    object panelUt, panelWyj;

    /// <summary>Odświeżenie zakładki UT albo Do wyjaśnienia, jeśli jest widoczna (każda pamięta, czy jest aktualna).</summary>
    void RefreshWidocznyPanel()
    {
        if (panelyNieaktualne) { panelUt = panelWyj = null; panelyNieaktualne = false; }
        if (Side.SelectedItem == TabUt && panelUt == null) { RefreshUt(); panelUt = Obraz; }
        if (Side.SelectedItem == TabWyjasnienia && panelWyj == null) { RefreshWyjasnienia(); panelWyj = Obraz; }
    }
    void Refresh()
    {
        refreshing = true;
        try
        {
            bool has = Doc != null;
            if (has && Obraz == null) Current = Doc.P.Obrazy.FirstOrDefault()?.Id;
            Title = !has ? "Edytor zobrazowania 2" : $"{(Dirty ? "* " : "")}{Path.GetFileName(Doc.DefaultPath)} – {Doc.Ut.Name} – Edytor zobrazowania 2";
            SaveBtn.IsEnabled = SaveAsBtn.IsEnabled = ReloadUtBtn.IsEnabled = SprawdzBtn.IsEnabled = PrzypnijBtn.IsEnabled = PodgladBtn.IsEnabled = SymBtn.IsEnabled = PrzebiegiBtn.IsEnabled = has;
            UndoBtn.IsEnabled = has && Doc.UndoName != null; UndoBtn.ToolTip = has && Doc.UndoName != null ? $"Cofnij: {Doc.UndoName} (Ctrl+Z)" : "Nic do cofnięcia";
            RedoBtn.IsEnabled = has && Doc.RedoName != null; RedoBtn.ToolTip = has && Doc.RedoName != null ? $"Ponów: {Doc.RedoName} (Ctrl+Y)" : "Nic do ponowienia";
            foreach (var b in ToolBtns.Values.Concat(SymBtns.Values).Concat(KlocekBtns.Values)) b.IsEnabled = has;
            SzkicBar.Visibility = Obraz?.Szkic != null ? Visibility.Visible : Visibility.Collapsed;
            if (!has || opisFor is var (oid, _) && Obraz?.Elementy.Any(x => x.Id == oid) != true) { opisFor = null; OpisEdit.Visibility = Visibility.Collapsed; }
            View.Obraz = Obraz; View.Siatka = has ? Doc.P.Siatka : 8;
            View.PruneSelection();
            RefreshWlasciwosci();
            RefreshTools();
            RefreshTabs();
            // zakładki UT i Do wyjaśnienia liczą dużo – odświeżane, gdy widać je na ekranie (reszta – przy przejściu na zakładkę)
            panelyNieaktualne = true;
            RefreshWidocznyPanel();
            RefreshImgPanel();
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
        Tabs.Children.Add(Btn("+ obraz szczegółowy", (s, e) => AddImage(ZobObraz.Szczegolowy), "Nowy obraz szczegółowy (Ie-104 § 5) – posterunki wybierasz w zakładce Obraz"));
        Tabs.Children.Add(new Border { Width = 4 });
        Tabs.Children.Add(Btn("+ obraz poglądowy", (s, e) => AddImage(ZobObraz.Pogladowy), "Nowy obraz poglądowy całego obszaru (Ie-104 § 6) – rysowany osobno"));
    }

    void RefreshImgPanel()
    {
        BuildImgPanelContent();
        var o = Obraz; if (o == null) return;
        // pole nazwy należy do jednego obrazu – po zmianie obrazu zawsze jego nazwa (inaczej zapis przeniósłby nazwę poprzedniego obrazu)
        if (!NameBox.IsKeyboardFocused || nameFor != o.Id) NameBox.Text = o.Nazwa;
        nameFor = o.Id;
        KindBox.SelectedIndex = Array.FindIndex(ZobObraz.Rodzaje, r => r.Key == o.Rodzaj);
        OpisyBox.SelectedIndex = Array.FindIndex(ZobObraz.OpisyRodzaje, r => r.Key == o.OpisyOdcinkow);
        WidthBox.Text = o.Szer.ToString(CultureInfo.InvariantCulture); HeightBox.Text = o.Wys.ToString(CultureInfo.InvariantCulture);
        RzedyText.Text = string.Join(" ", o.Rzedy.Select(y => y.ToString(CultureInfo.InvariantCulture)));
        PostsPanel.Children.Clear();
        if (Doc.Ut.Posts.Count == 0)
            PostsPanel.Children.Add(Note("W UT nie ma posterunków – obraz obejmuje cały obszar. Posterunki dodaje się w edytorze UT."));
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
        RefreshStrefy();
    }

    /// <summary>Strefy zarezerwowane: rodzaj, prostokąt, usunięcie.</summary>
    void RefreshStrefy()
    {
        StrefyPanel.Children.Clear();
        var o = Obraz;
        for (int i = 0; i < o.Strefy.Count; i++)
        {
            int idx = i; var s = o.Strefy[i];
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            var del = Btn("✕", (_, _) => Ch("Usunięcie strefy", q => { if (idx < q.Strefy.Count) q.Strefy.RemoveAt(idx); }), "Usuń strefę");
            DockPanel.SetDock(del, System.Windows.Controls.Dock.Right); row.Children.Add(del);
            var kind = new ComboBox { Width = 110, Margin = new Thickness(0, 0, 4, 0) };
            foreach (var r in ObrStrefa.Rodzaje) kind.Items.Add(r.Name);
            kind.SelectedIndex = Array.FindIndex(ObrStrefa.Rodzaje, r => r.Key == s.Rodzaj);
            kind.SelectionChanged += (_, _) => { if (!refreshing && kind.SelectedIndex >= 0) { var k = ObrStrefa.Rodzaje[kind.SelectedIndex].Key; Ch("Rodzaj strefy", q => q.Strefy[idx].Rodzaj = k); } };
            DockPanel.SetDock(kind, System.Windows.Controls.Dock.Left); row.Children.Add(kind);
            var box = new TextBox { Text = string.Join(", ", s.Prost ?? new int[4]), Margin = new Thickness(0, 0, 4, 0), Tag = idx };
            box.LostFocus += (_, _) => CommitStrefa(idx, box);
            box.KeyDown += (_, e) => { if (e.Key == Key.Enter) CommitStrefa(idx, box); };
            row.Children.Add(box);
            StrefyPanel.Children.Add(row);
        }
    }

    /// <summary>Pola prostokątów stref (test).</summary>
    public List<TextBox> StrefyBoxes => StrefyPanel.Children.OfType<DockPanel>().SelectMany(d => d.Children.OfType<TextBox>()).ToList();

    void RefreshUt()
    {
        UtPanel.Children.Clear();
        UtPanel.Children.Add(Header("Układ torowy"));
        if (Doc == null) { UtPanel.Children.Add(Note("Brak otwartego zobrazowania.")); return; }
        var u = Doc.Ut;
        UtPanel.Children.Add(Note($"{Path.GetFileName(Doc.UtPath)} – „{u.Name}”, tylko do odczytu; zmiany w UT robi się w edytorze UT."));
        UtPanel.Children.Add(Label($"Posterunki: {(u.Posts.Count == 0 ? "brak" : string.Join(", ", u.Posts.Select(p => $"{p.Kod} {p.Nazwa}".Trim())))}"));
        UtPanel.Children.Add(Label($"Tory: {u.Tracks.Count}, rozjazdy: {u.Turnouts.Count}, urządzenia: {u.Devices.Count}"));
        UtPanel.Children.Add(Label($"Szlaki: {(u.Lines.Count == 0 ? "brak" : string.Join(", ", u.Lines.Select(l => l.Numer)))}"));
        UtPanelPrzypiecia(u);
        UtPanelSzkic(u);
    }

    void ShowCursor() => stCursor.Text = View.CursorPt is var (x, y) ? $"x = {x,5} j.  y = {y,5} j." : "";
    void ShowZoom() => stZoom.Text = Doc == null ? "" : $"powiększenie {View.Zoom * 100:0} %";
    public void Message(string m) => stMsg.Text = m;
    public string LastMessage => stMsg.Text;

    // ------------------------------------------------------------------ zmiany obrazu
    void Ch(string what, Action<ObrObraz> act)
    {
        var id = Current;
        Doc.Change(what, p => { var o = p.Obrazy.FirstOrDefault(x => x.Id == id); if (o != null) act(o); });
    }

    /// <summary>Obraz, którego nazwę pokazuje pole nazwy.</summary>
    string nameFor;

    void CommitName()
    {
        if (refreshing || Obraz == null || nameFor != Current) return;
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

    /// <summary>Liczby całkowite oddzielone spacją, przecinkiem albo średnikiem; null – błąd.</summary>
    static List<int> Liczby(string s)
    {
        var r = new List<int>();
        foreach (var x in s.Split(new[] { ' ', ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) return null;
            r.Add(v);
        }
        return r;
    }

    void CommitRzedy()
    {
        if (refreshing || Obraz == null) return;
        var l = Liczby(RzedyText.Text);
        int g = Doc.P.Siatka;
        if (l == null || l.Any(y => y <= 0 || y >= Obraz.Wys))
        {
            Message($"Prowadnice: liczby całkowite od 1 do {Obraz.Wys - 1} j., oddzielone spacją lub przecinkiem");
            RzedyText.Text = string.Join(" ", Obraz.Rzedy);
            return;
        }
        var r = l.Select(y => ZobTory.Grid(y, g)).Where(y => y > 0 && y < Obraz.Wys).Distinct().OrderBy(y => y).ToList();
        if (!r.SequenceEqual(Obraz.Rzedy)) Ch("Prowadnice rzędów", o => o.Rzedy = r);
        else RzedyText.Text = string.Join(" ", r);
    }

    void CommitStrefa(int idx, TextBox box)
    {
        if (refreshing || Obraz == null || idx >= Obraz.Strefy.Count) return;
        var l = Liczby(box.Text);
        int g = Doc.P.Siatka;
        if (l == null || l.Count != 4 || l[2] < g || l[3] < g || l[0] < 0 || l[1] < 0 || l[0] + l[2] > Obraz.Szer || l[1] + l[3] > Obraz.Wys)
        {
            Message("Strefa: cztery liczby x, y, szerokość, wysokość [j.] – prostokąt w obrębie obrazu");
            box.Text = string.Join(", ", Obraz.Strefy[idx].Prost);
            return;
        }
        var p = l.Select(v => ZobTory.Grid(v, g)).ToArray();
        if (!p.SequenceEqual(Obraz.Strefy[idx].Prost)) Ch("Strefa zarezerwowana", o => o.Strefy[idx].Prost = p);
        else box.Text = string.Join(", ", p);
    }

    public void AddStrefa() => Ch("Nowa strefa", o => o.Strefy.Add(new ObrStrefa { Rodzaj = ObrStrefa.Inna, Prost = new[] { 0, 0, 160, 80 } }));

    public void ShowImage(string id)
    {
        if (Doc == null || Doc.P.Obrazy.All(o => o.Id != id)) return;
        if (id != Current) View.ClearSelection();
        Current = id; Refresh(); View.Fit();
        Message($"Obraz „{Obraz.Nazwa}”");
    }

    public ObrObraz AddImage(string rodzaj)
    {
        if (Doc == null) return null;
        int n = Doc.P.Obrazy.Count + 1;
        var o = new ObrObraz
        {
            Rodzaj = rodzaj,
            Nazwa = rodzaj == ZobObraz.Pogladowy ? "Obraz poglądowy" : $"Obraz {n}",
            // poglądowy – cały obszar (wszystkie posterunki); szczegółowy – posterunki wybiera człowiek
            Posterunki = rodzaj == ZobObraz.Pogladowy ? Doc.Ut.Posts.Select(x => x.Id).ToList() : new(),
        };
        o.Strefy = ObrObraz.StrefyDomyslne(o.Szer);
        Doc.Change(rodzaj == ZobObraz.Pogladowy ? "Nowy obraz poglądowy" : "Nowy obraz szczegółowy", p => p.Obrazy.Add(o));
        ShowImage(o.Id);
        Message($"Nowy obraz „{o.Nazwa}” – " + (rodzaj == ZobObraz.Pogladowy ? "cały obszar" : "wybierz posterunki w zakładce Obraz"));
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
        var r = MessageBox.Show(this, $"Zapisać zobrazowanie „{Path.GetFileName(Doc.DefaultPath)}”?", "Edytor zobrazowania 2", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
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
            SetDoc(ObrDocument.New(utPath));
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

    /// <summary>Otwarcie <c>*.zob.json</c> w wersji 2 (albo <c>*.ut.json</c> – nowe zobrazowanie do niego).</summary>
    public bool OpenPath(string path)
    {
        if (path.EndsWith(".ut.json", StringComparison.OrdinalIgnoreCase)) return NewFromUt(path);
        try
        {
            SetDoc(ObrDocument.Load(path));
            Message("Otwarto " + Path.GetFileName(path));
            if (Doc.ZWersji1 != null)
            {
                var m = $"Plik starego edytora (wersja 1) przekształcony do wersji 2 – przypięcia potwierdzone. Zapisz jako {Path.GetFileName(Doc.DefaultPath)} (plik starego edytora zostaje)."
                    + (Doc.UwagiKonwersji.Count > 0 ? $" Pominięto: {Doc.UwagiKonwersji.Count} (szczegóły w oknie)." : "");
                Message(m);
                if (!NoPrompt) MessageBox.Show(this, m + (Doc.UwagiKonwersji.Count > 0 ? "\n\n• " + string.Join("\n• ", Doc.UwagiKonwersji.Take(15)) : ""), "Obraz z wersji 1", MessageBoxButton.OK, MessageBoxImage.Information);
            }
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
        // plik starego edytora obok UT nie jest nadpisywany bez pytania (stary edytor działa dalej – plan § 4)
        if (File.Exists(path) && path != Doc.Path && ObrDocument.WersjaPliku(File.ReadAllText(path)) == 1)
        {
            if (NoPrompt) { Message($"{Path.GetFileName(path)} to plik starego edytora – zapisz pod inną nazwą"); return false; }
            if (MessageBox.Show(this, $"{Path.GetFileName(path)} to plik starego edytora zobrazowania (wersja 1). Nadpisać go plikiem wersji 2? Stary edytor go potem nie otworzy (kopia zostanie w .bak).",
                "Zapis zobrazowania", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return false;
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
            if (File.Exists(Path.Combine(d.FullName, "PLAN_EDYTOR_ZOBRAZOWANIA_V2.md")))
            {
                var p = Path.Combine(d.FullName, "projekty_ut"); Directory.CreateDirectory(p); return p;
            }
        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    void OnClosing(object s, CancelEventArgs e) { if (!ConfirmDiscard()) e.Cancel = true; }

    void OnKey(object s, KeyEventArgs e)
    {
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control), shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        // pole opisu otwarte, a fokus gdzie indziej: Enter / Esc – zatwierdź / zamknij, inny klawisz – do pola (nie skrót palety)
        if (OpisOtwarty && !OpisEdit.IsKeyboardFocusWithin)
        {
            if (e.Key == Key.Enter) { CommitOpis(); e.Handled = true; return; }
            if (e.Key == Key.Escape) { CloseOpis(); e.Handled = true; return; }
            if (!ctrl) { opisOd = DateTime.UtcNow; OpisEdit.Focus(); Keyboard.Focus(OpisEdit); return; }
        }
        bool inText = Keyboard.FocusedElement is TextBox;
        if (ctrl && e.Key == Key.Z && !inText) { if (shift) DoRedo(); else DoUndo(); e.Handled = true; }
        else if (ctrl && e.Key == Key.Y && !inText) { DoRedo(); e.Handled = true; }
        else if (ctrl && e.Key == Key.S) { SaveDoc(shift); e.Handled = true; }
        else if (ctrl && e.Key == Key.O) { OpenDoc(); e.Handled = true; }
        else if (ctrl && e.Key == Key.N) { NewDoc(); e.Handled = true; }
        else if (!inText && Doc != null && !ctrl && SymbolKey(e.Key)) e.Handled = true;
        else if (!inText && Doc != null && RysunekKey(e.Key, ctrl, shift)) e.Handled = true;
        else if (inText || ctrl || Doc == null) return;
        else if (e.Key == Key.Home) { View.Fit(); e.Handled = true; }
        else if (e.Key == Key.F7) { Sprawdz(); e.Handled = true; }
        else if (e.Key == Key.F8) { Przypnij(); e.Handled = true; }
        else if (e.Key == Key.F9) { Podglad(); e.Handled = true; }
        else if (e.Key == Key.F6) { Przebiegi(); e.Handled = true; }
        else if (e.Key == Key.Escape && Obraz?.Szkic != null && View.Tool == ObrTool.Wskaz) { OdrzucSzkic(); e.Handled = true; }
        else if (e.Key is Key.V or Key.Escape) { if (View.Tool == ObrTool.Tor && View.Draft.Count > 0) View.CancelDraft(); else SetTool(ObrTool.Wskaz); e.Handled = true; }
        else if (e.Key == Key.T) { SetTool(ObrTool.Tor); e.Handled = true; }
        else if (e.Key == Key.R) { SetTool(ObrTool.Prowadnica); e.Handled = true; }
    }
}

using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;
using Microsoft.Win32;

namespace Lcs.EdytorUT;

/// <summary>
/// Okno edytora układu torowego: pasek poleceń i narzędzi, płótno w PUWG 1992, panel (obszar, podkłady), pasek stanu.
/// </summary>
public partial class MainWindow : Window
{
    public UtDocument Doc { get; private set; }
    public readonly UtView View = new();
    /// <summary>Bez okien dialogowych (testy).</summary>
    public bool NoPrompt;

    public readonly TextBox GoToBox = new() { Width = 170, Margin = new Thickness(4, 0, 2, 0), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Współrzędne z Google Maps (szerokość, długość – np. 51.7631, 19.5438) albo X Y w PUWG 1992 (najpierw X – północ). Enter – idź." };
    public readonly Button GoToBtn = new() { Content = "Idź do" };
    public readonly TextBox NameBox = new() { Margin = new Thickness(0, 2, 0, 8) };
    public readonly TextBox NotesBox = new() { Margin = new Thickness(0, 2, 0, 8), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 90, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    public readonly ComboBox ScaleBox = new() { Margin = new Thickness(0, 2, 0, 8) };
    public readonly Button UndoBtn = new() { Content = "↶ Cofnij" }, RedoBtn = new() { Content = "↷ Ponów" };
    public readonly CheckBox gridBox = new() { Content = "Siatka", IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
    public readonly ToggleButton SchematBtn = new() { Content = "Schemat", Padding = new Thickness(6, 2, 6, 2), ToolTip = "Widok schematu: oś stacji poziomo, w poprzek rozciągnięte, białe tło – jak plan schematyczny (S)" };
    public readonly ToggleButton TerenBtn = new() { Content = "Teren", Padding = new Thickness(6, 2, 6, 2), ToolTip = "Widok terenu: północ u góry, 1:1, ortofotomapa – do odrysowania (S)" };
    public readonly ComboBox StretchBox = new() { Width = 52, ToolTip = "Rozciągnięcie w poprzek osi w widoku Schemat" };
    public readonly CheckBox BgBox = new() { Content = "Podkład", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0), ToolTip = "Podkład (ortofotomapa, grafiki) także w widoku Schemat" };
    static readonly double[] Stretches = { 1, 2, 4, 6 };
    readonly TextBlock stCursor = new() { FontFamily = new FontFamily("Consolas"), MinWidth = 520 }, stView = new() { MinWidth = 230 }, stMsg = new();
    bool refreshing;

    public MainWindow(string path)
    {
        Width = 1500; Height = 900; WindowState = WindowState.Maximized;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
        Background = new SolidColorBrush(Color.FromRgb(0xf3, 0xf3, 0xf0));

        var root = new DockPanel();
        root.Children.Add(Dock(BuildToolbar(), System.Windows.Controls.Dock.Top));
        root.Children.Add(Dock(BuildStatus(), System.Windows.Controls.Dock.Bottom));
        root.Children.Add(Dock(BuildPanel(), System.Windows.Controls.Dock.Right));
        View.ToolChanged += ToolShown;
        View.MeasureChanged += ShowMeasure;
        root.Children.Add(View);
        Content = root;

        View.CursorMoved += ShowCursor;
        View.ViewChanged += ShowView;
        gridBox.Click += (s, e) => { if (View.Schemat) View.ShowGridSchemat = gridBox.IsChecked == true; else View.ShowGrid = gridBox.IsChecked == true; View.InvalidateVisual(); };
        PreviewKeyDown += OnKey;
        InitBg();
        InitTory();
        InitRozjazdy();
        InitUrzadzenia();
        InitWyposazenie();
        InitKontrola();
        Closing += OnClosing;

        UtDocument doc = null;
        if (path != null)
        {
            try { doc = UtDocument.Load(path); }
            catch (Exception ex) { Message($"Nie można otworzyć {Path.GetFileName(path)}: {ex.Message}"); }
        }
        SetDoc(doc ?? new UtDocument());
    }

    static UIElement Dock(UIElement e, System.Windows.Controls.Dock d) { DockPanel.SetDock(e, d); return e; }

    // ------------------------------------------------------------------ budowa okna
    UIElement BuildToolbar()
    {
        var tb = new ToolBar { Background = new SolidColorBrush(Color.FromRgb(0xe6, 0xe6, 0xe2)) };
        ToolBarTray.SetIsLocked(tb, true);
        tb.Items.Add(Btn("Nowy", (s, e) => NewDoc(), "Nowy projekt układu torowego (Ctrl+N)"));
        tb.Items.Add(Btn("Otwórz…", (s, e) => OpenDoc(), "Otwórz projekt *.ut.json (Ctrl+O)"));
        tb.Items.Add(Btn("Zapisz", (s, e) => SaveDoc(false), "Zapisz (Ctrl+S)"));
        tb.Items.Add(Btn("Zapisz jako…", (s, e) => SaveDoc(true), "Zapisz pod nową nazwą (Ctrl+Shift+S)"));
        tb.Items.Add(new Separator());
        UndoBtn.Click += (s, e) => DoUndo(); RedoBtn.Click += (s, e) => DoRedo();
        tb.Items.Add(UndoBtn); tb.Items.Add(RedoBtn);
        tb.Items.Add(new Separator());
        tb.Items.Add(new TextBlock { Text = "Punkt:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) });
        GoToBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) { GoTo(GoToBox.Text); e.Handled = true; } };
        GoToBtn.Click += (s, e) => GoTo(GoToBox.Text);
        tb.Items.Add(GoToBox); tb.Items.Add(GoToBtn);
        tb.Items.Add(new Separator());
        SchematBtn.Click += (s, e) => SetSchemat(true); TerenBtn.Click += (s, e) => SetSchemat(false);
        tb.Items.Add(SchematBtn); tb.Items.Add(TerenBtn);
        foreach (var k in Stretches) StretchBox.Items.Add($"×{k:0}");
        StretchBox.SelectionChanged += (s, e) => { if (!refreshing && StretchBox.SelectedIndex >= 0) { Doc.P.View.Stretch = Stretches[StretchBox.SelectedIndex]; ApplyMode(false); } };
        tb.Items.Add(StretchBox);
        tb.Items.Add(Btn("Oś wg toru", (s, e) => AxisFromTrack(), "Oś schematu (poziomo) wzdłuż zaznaczonego toru; bez zaznaczenia – wzdłuż najdłuższego toru"));
        tb.Items.Add(Btn("↻ 180°", (s, e) => Doc.Change("Oś schematu", p => p.AxisAz = (View.AxisAz * 180 / Math.PI + 180) % 360), "Obróć schemat o 180°"));
        BgBox.Click += (s, e) => { Doc.P.View.ShowBg = BgBox.IsChecked == true; ApplyMode(false); };
        tb.Items.Add(BgBox);
        tb.Items.Add(gridBox);
        tb.Items.Add(new Separator());
        tb.Items.Add(Btn("✔ Sprawdź", (s, e) => RunCheck(true), "Kontrola układu (Ig-10, etap 7): otwarte końce, rozjazdy bez numeru, urządzenia poza torem, nakładające się tory, odcinki, kilometraż, profil (F7)"));
        tb.Items.Add(Btn("Profil…", (s, e) => OpenProfile(), "Profil podłużny zaznaczonego toru: niweleta, pochylenia, teren z NMT GUGiK, nasyp / przekop"));
        tb.Items.Add(Btn("Eksport…", (s, e) => OpenExport(), "Wydruk w skali do PNG / PDF (1:500 … 1:5000, schemat Ig-10 1:2000 / 1:500)"));
        // drugi rząd: narzędzia rysowania
        var bg = new SolidColorBrush(Color.FromRgb(0xe6, 0xe6, 0xe2));
        var tray = new ToolBarTray { Background = bg, IsLocked = true };
        tray.ToolBars.Add(tb);
        tb = new ToolBar { Background = bg, Band = 1 };
        tray.ToolBars.Add(tb);
        tb.Items.Add(ToolBtn(UtTool.Wskaz, "✋ Wskaż", "Przesuwanie widoku (V, Esc)"));
        tb.Items.Add(ToolBtn(UtTool.Tor, "🛤 Tor", "Rysowanie toru: klik – kolejne wierzchołki wieloboku stycznych, w wierzchołkach łuk R z przejściowymi L (T)"));
        tb.Items.Add(ProstaBtn); tb.Items.Add(LukBtn);
        tb.Items.Add(ToolBtn(UtTool.LukOdDo, "◠ Łuk od–do", "Łuk na torze: klik w miejscu początku łuku, klik w miejscu końca – promień dobiera się sam (K)"));
        tb.Items.Add(ToolBtn(UtTool.LukPunkty, "◠ Łuk z punktów", "Dokładny łuk: klik łuku toru, potem kilka punktów na osi łuku na zdjęciu – R (Tab: i L) dobiera się do punktów (J)"));
        tb.Items.Add(ToolBtn(UtTool.Rownolegly, "⫽ Równoległy", "Tor równoległy z podkładu: klik na torze wzorcowym, klik na osi nowego toru (początek), klik na końcu albo Enter (P)"));
        tb.Items.Add(new TextBlock { Text = " R", VerticalAlignment = VerticalAlignment.Center }); tb.Items.Add(NewRBox);
        tb.Items.Add(new TextBlock { Text = " L", VerticalAlignment = VerticalAlignment.Center }); tb.Items.Add(NewLBox);
        tb.Items.Add(new Separator());
        tb.Items.Add(ToolBtn(UtTool.Rozjazd, "⑂ Rozjazd", "Rozjazd z katalogu: wciśnij na torze w miejscu początku rozjazdu (PR) i przeciągnij w stronę odgałęzienia; puszczenie na torze sąsiednim – łącznik; krzyżowy / skrzyżowanie – klik na przecięciu torów (R)"));
        tb.Items.Add(RzTypeBox);
        tb.Items.Add(new Separator());
        tb.Items.Add(ToolBtn(UtTool.Urzadzenie, "⊢○ Urządzenie", "Urządzenie na torze (Ig-10): wciśnij na torze w miejscu urządzenia i przeciągnij w stronę jazdy, po stronie, po której stoi; kozioł, licznik, złącze, ukres – klik (U)"));
        tb.Items.Add(DevKindBox);
        tb.Items.Add(ToolBtn(UtTool.Odcinek, "▭ Odcinek", "Odcinek kontroli niezajętości: klik na torze w kawałku między granicami (licznikami osi, złączami izolowanymi) – nazwa odcinka (O)"));
        tb.Items.Add(ToolBtn(UtTool.Peron, "▤ Peron", "Peron (Ig-10 s. 52): wciśnij na torze w miejscu początku krawędzi i przeciągnij wzdłuż toru do jej końca, po stronie peronu"));
        tb.Items.Add(ToolBtn(UtTool.Przejazd, "╪ Przejazd", "Przejazd kolejowo-drogowy: klik na torze – droga prostopadle; przeciągnięcie – wzdłuż osi drogi (kąt i długość)"));
        tb.Items.Add(ToolBtn(UtTool.Km, "km", "Kilometraż linii: wciśnij na torze w znanym miejscu i przeciągnij w stronę rosnącego km linii; numer linii i km z okna"));
        tb.Items.Add(new Separator());
        tb.Items.Add(Btn("Symbole Ig-10…", (s, e) => OpenSymbols(), "Katalog znaków Ig-10 (zał. 8) z pliku Dane/symbole_ig10.json – podgląd, ponowne wczytanie po poprawkach"));
        tb.Items.Add(new Separator());
        tb.Items.Add(ToolBtn(UtTool.Miarka, "📏 Miarka", "Pomiar długości w terenie: klik – kolejne punkty, Backspace – cofnij punkt, PPM – wyczyść (M)"));
        return tray;
    }

    readonly Dictionary<UtTool, ToggleButton> toolBtns = new();
    ToggleButton ToolBtn(UtTool t, string text, string tip)
    {
        var b = new ToggleButton { Content = text, ToolTip = tip, Padding = new Thickness(6, 2, 6, 2), IsChecked = t == UtTool.Wskaz };
        b.Click += (s, e) => SetTool(t);
        toolBtns[t] = b; return b;
    }
    public void SetTool(UtTool t)
    {
        if (t != UtTool.Kalibracja) View.CalPending = null;
        // narzędzia podkładów działają na zdjęciu północą do góry – przełączenie na Teren
        if (t is UtTool.Obszar or UtTool.Kalibracja or UtTool.Przesun && View.Schemat) SetSchemat(false);
        View.Tool = t;
    }
    void ToolShown()
    {
        foreach (var (t, b) in toolBtns) b.IsChecked = View.Tool == t;
        Message(View.Tool switch
        {
            UtTool.Tor => View.Mode == UtView.TorMode.Luk
                ? "Tor – łuk: promień R z pola (R 0 – promień za kursorem: łuk przez punkt pod kursorem), strona i kąt za kursorem, długość łuku z klawiatury + Enter; Shift – koniec równoległy do osi schematu; Tab – prosta; klik na końcu innego toru – połączenie; Enter / PPM – koniec; Backspace – cofnij odcinek"
                : "Tor – prosta: klik – pierwszy punkt (albo koniec toru – przedłużenie), potem odcinki na przedłużeniu, długość z klawiatury + Enter; Alt – wolny kierunek; Tab – łuk; klik na końcu innego toru – połączenie łukiem / łukami odwrotnymi; Enter / PPM – koniec; Backspace – cofnij odcinek; Esc – rezygnacja",
            UtTool.Rozjazd => Katalog.Find(View.RzType)?.IsCrossing == true
                ? $"{View.RzType}: wciśnij na torze w miejscu środka i przeciągnij w stronę toru przecinającego (powstanie pod kątem typu) albo kliknij na przecięciu dwóch torów"
                : $"{View.RzType}: wciśnij na torze w miejscu początku rozjazdu (PR) i przeciągnij w stronę odgałęzienia; puść na torze sąsiednim – łącznik",
            UtTool.Rownolegly => "Równoległy: 1) klik na torze wzorcowym, 2) klik na podkładzie na osi nowego toru w miejscu początku (odstęp co 1 cm, Shift – co 0,25 m), 3) klik w miejscu końca albo Enter – do końca toru; PPM / Backspace – krok wstecz, Esc – od nowa",
            UtTool.Urzadzenie => $"{UtDevice.KindInfo(View.DevKind).Name}: " + (UtDevice.KindInfo(View.DevKind).Directed && View.DevKind != UtDevice.Koziol
                ? "wciśnij na torze w miejscu urządzenia i przeciągnij w stronę jazdy, której dotyczy, po stronie, po której stoi"
                : View.DevKind == UtDevice.Koziol ? "kliknij przy końcu toru (przyciąga się do końca)" : "kliknij na torze w miejscu urządzenia"),
            UtTool.LukPunkty => "Łuk z punktów: kliknij łuk toru (albo przy załamaniu), potem kilka punktów na osi prawdziwego łuku na zdjęciu (najlepiej od początku do końca łuku) – promień dobiera się do wszystkich punktów, przy każdym widać odchyłkę; Tab – także krzywe przejściowe L; Backspace – usuń punkt; Enter / PPM – zastosuj; Esc – od nowa",
            UtTool.LukOdDo => "Łuk od–do: kliknij na torze w miejscu początku łuku (np. gdzie na zdjęciu kończy się prosta), potem w miejscu końca łuku – wierzchołek i promień dobiorą się same; wierzchołki pośrednie znikną; PPM / Esc – od nowa",
            UtTool.Odcinek => "Odcinek: kliknij na torze – nazwa odcinka kontroli niezajętości dla całego kawałka między granicami (liczniki osi, złącza izolowane, końce torów); kolor = odcinek, czerwone kreski – kawałek bez odcinka",
            UtTool.Peron => "Peron: wciśnij na torze w miejscu początku krawędzi peronowej i przeciągnij wzdłuż toru do jej końca, po stronie peronu",
            UtTool.Przejazd => "Przejazd: kliknij na torze w osi drogi (droga prostopadle) albo przeciągnij wzdłuż osi drogi – kąt i długość drogi na planie",
            UtTool.Km => "Kilometraż: wciśnij na torze w miejscu o znanym km linii i przeciągnij w stronę rosnącego km; w oknie numer linii i km (np. 17 25.452). Drugi punkt na tym samym torze – kilometr skrócony / wydłużony",
            UtTool.Miarka => "Miarka: klikaj kolejne punkty (przeciągnięcie przesuwa widok); Backspace – cofnij punkt, PPM – wyczyść, Esc – koniec",
            UtTool.Obszar => "Zaznacz prostokąt do pobrania ortofotomapy (przeciągnij); PPM / Esc – rezygnacja",
            UtTool.Kalibracja => "Kalibracja: klik na grafice (punkt charakterystyczny), potem klik w miejscu, gdzie ten punkt jest naprawdę (np. na ortofotomapie); PPM – rezygnacja z punktu, Esc – koniec",
            UtTool.Przesun => "Przesuń grafikę: przeciągnij ją myszą; Esc – koniec",
            _ => "",
        });
    }

    static Button Btn(string text, RoutedEventHandler click, string tip)
    {
        var b = new Button { Content = text, ToolTip = tip, Padding = new Thickness(6, 2, 6, 2) };
        b.Click += click; return b;
    }

    UIElement BuildPanel()
    {
        var root = new StackPanel { Margin = new Thickness(10) };
        root.Children.Add(TrackPanel);
        root.Children.Add(CheckExpander);
        root.Children.Add(LayerExpander);
        var sp = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        root.Children.Add(new Expander { Header = Header("Obszar"), Content = sp, IsExpanded = false, Margin = new Thickness(0, 0, 0, 6) });
        sp.Children.Add(Label("Nazwa"));
        NameBox.LostFocus += (s, e) => CommitName();
        NameBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) { CommitName(); e.Handled = true; } };
        sp.Children.Add(NameBox);
        sp.Children.Add(Label("Układ współrzędnych"));
        sp.Children.Add(new TextBlock { Text = "PUWG 1992 (EPSG:2180)\nX – na północ, Y – na wschód, Z – wysokość [m]", Margin = new Thickness(0, 2, 0, 8), Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(Label("Skala arkusza (Ig-10 § 3) – wielkość znaków, wydruk"));
        foreach (var k in UtProject.DrawingScales) ScaleBox.Items.Add($"1:{k} wzdłuż, 1:{k / 4} w poprzek");
        ScaleBox.SelectionChanged += (s, e) =>
        {
            if (refreshing || ScaleBox.SelectedIndex < 0) return;
            int k = UtProject.DrawingScales[ScaleBox.SelectedIndex];
            Doc.Change("Skala rysunku", p => p.DrawingScale = k);
        };
        sp.Children.Add(ScaleBox);
        sp.Children.Add(new TextBlock { Text = "Geometria zawsze w metrach terenu – skala rysunku jej nie zmienia.", Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, -4, 0, 8) });
        sp.Children.Add(Label("Uwagi"));
        NotesBox.LostFocus += (s, e) => { if (!refreshing && NotesBox.Text != Doc.P.Notes) Doc.Change("Uwagi", p => p.Notes = NotesBox.Text); };
        sp.Children.Add(NotesBox);
        var bgp = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        BuildBgPanel(bgp);
        bgp.Children.RemoveAt(0); bgp.Children.RemoveAt(0);    // separator i nagłówek – nagłówkiem jest rozwijany pasek
        BgExpander = new Expander { Header = Header("Podkłady"), Content = bgp, IsExpanded = false };
        root.Children.Add(BgExpander);
        return new Border
        {
            Width = 330, Child = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xc8, 0xc8, 0xc2)), BorderThickness = new Thickness(1, 0, 0, 0),
        };
    }

    public Expander BgExpander { get; private set; }

    // ------------------------------------------------------------------ widok Schemat / Teren
    /// <summary>Przełączenie widoku; przy wejściu w Schemat oś liczona na nowo (wpisana albo wg najdłuższego toru).</summary>
    public void SetSchemat(bool on) { Doc.P.View.Schemat = on; ApplyMode(true); }

    double appliedAxis = double.NaN; double? appliedExplicit;
    /// <summary>Widok wg ustawień projektu. Oś automatyczna liczy się tylko przy przełączeniu (rysowanie toru nie obraca widoku).</summary>
    void ApplyMode(bool recomputeAxis)
    {
        var v = Doc.P.View;
        if (recomputeAxis || double.IsNaN(appliedAxis) || Doc.P.AxisAz != appliedExplicit) { appliedAxis = Doc.P.SchematAz(); appliedExplicit = Doc.P.AxisAz; }
        View.SchematBg = v.ShowBg;
        View.SetMode(v.Schemat, appliedAxis, v.Stretch);
        refreshing = true;
        try
        {
            SchematBtn.IsChecked = v.Schemat; TerenBtn.IsChecked = !v.Schemat;
            StretchBox.SelectedIndex = Math.Max(0, Array.IndexOf(Stretches, v.Stretch)); StretchBox.IsEnabled = v.Schemat;
            BgBox.IsChecked = v.ShowBg; BgBox.IsEnabled = v.Schemat;
            gridBox.IsChecked = v.Schemat ? View.ShowGridSchemat : View.ShowGrid;      // osobno dla Schematu (domyślnie bez siatki) i Terenu
        }
        finally { refreshing = false; }
        ShowView();
    }

    /// <summary>Oś schematu wzdłuż zaznaczonego toru (cięciwa, zwrócona na wschód) albo najdłuższego.</summary>
    public void AxisFromTrack()
    {
        var t = Doc.P.Tracks.FirstOrDefault(x => x.Id == View.SelectedTrack);
        // Ig-10 § 3 ust. 3: kilometraż rośnie od lewej do prawej – oś w kierunku toru
        double? az = t != null && t.Vertices.Count >= 2 ? (Trasa.Az(t.Vertices[0].P, t.Vertices[^1].P) * 180 / Math.PI + 360) % 360 : null;
        Doc.Change("Oś schematu", p => p.AxisAz = az);
        if (!View.Schemat) SetSchemat(true); else ApplyMode(true);
        Message(t != null ? $"Oś schematu wzdłuż toru {t.Number} (azymut {az:0.###}°)" : "Oś schematu wzdłuż najdłuższego toru");
    }

    static TextBlock Header(string t) => new() { Text = t, FontWeight = FontWeights.SemiBold, FontSize = 15, Margin = new Thickness(0, 0, 0, 8) };
    static TextBlock Label(string t) => new() { Text = t, Foreground = Brushes.Black, Margin = new Thickness(0, 2, 0, 0) };

    UIElement BuildStatus()
    {
        var sb = new StatusBar { Background = new SolidColorBrush(Color.FromRgb(0xe6, 0xe6, 0xe2)) };
        sb.Items.Add(new StatusBarItem { Content = stCursor });
        sb.Items.Add(new Separator());
        sb.Items.Add(new StatusBarItem { Content = stView });
        sb.Items.Add(new Separator());
        sb.Items.Add(new StatusBarItem { Content = CancelBtn });
        sb.Items.Add(new StatusBarItem { Content = stMsg });
        return sb;
    }

    // ------------------------------------------------------------------ dokument
    void SetDoc(UtDocument d)
    {
        if (Doc != null) Doc.Changed -= Refresh;
        if (profWin != null && !profWin.WasClosed) profWin.Close();
        Doc = d; View.Doc = d; View.MeasurePts.Clear(); SetTool(UtTool.Wskaz); selBg = null; View.Select(null);
        d.Changed += Refresh;
        var v = d.P.View;
        appliedAxis = double.NaN;
        ApplyLayers();
        ApplyMode(true);
        View.SetView(new XY(v.X, v.Y), v.Mpp);
        Refresh();
    }

    /// <summary>Panel, tytuł i przyciski zgodne z dokumentem (po każdej zmianie, cofnięciu, zapisie).</summary>
    void Refresh()
    {
        refreshing = true;
        try
        {
            // pola zatwierdzane przy wyjściu z pola – Changed nie przychodzi w trakcie pisania
            NameBox.Text = Doc.P.Name;
            NotesBox.Text = Doc.P.Notes;
            ScaleBox.SelectedIndex = Array.IndexOf(UtProject.DrawingScales, Doc.P.DrawingScale);
        }
        finally { refreshing = false; }
        Title = $"{(Doc.Dirty ? "* " : "")}{Doc.P.Name} – {(Doc.Path != null ? Path.GetFileName(Doc.Path) : "(nie zapisano)")} – Edytor układu torowego";
        UndoBtn.IsEnabled = Doc.UndoName != null; UndoBtn.ToolTip = Doc.UndoName != null ? $"Cofnij: {Doc.UndoName} (Ctrl+Z)" : "Nic do cofnięcia";
        RedoBtn.IsEnabled = Doc.RedoName != null; RedoBtn.ToolTip = Doc.RedoName != null ? $"Ponów: {Doc.RedoName} (Ctrl+Y)" : "Nic do ponowienia";
        RefreshBg();
        RefreshTrackPanel();
        if (Doc.P.AxisAz != appliedExplicit) ApplyMode(false);
        View.InvalidateVisual();
    }

    void CommitName()
    {
        if (refreshing) return;
        var n = NameBox.Text.Trim();
        if (n.Length == 0) { NameBox.Text = Doc.P.Name; return; }
        Doc.Change("Nazwa obszaru", p => p.Name = n);
    }

    public void DoUndo() { var n = Doc.UndoName; if (Doc.Undo()) Message("Cofnięto: " + n); }
    public void DoRedo() { var n = Doc.RedoName; if (Doc.Redo()) Message("Ponowiono: " + n); }

    /// <summary>Widok zapisywany w pliku (bez cofania i bez oznaczania zmian).</summary>
    void StoreView() { var v = Doc.P.View; v.X = View.Center.X; v.Y = View.Center.Y; v.Mpp = View.Mpp; }

    bool ConfirmDiscard()
    {
        if (!Doc.Dirty || NoPrompt) return true;
        var r = MessageBox.Show(this, $"Zapisać zmiany w „{Doc.P.Name}”?", "Edytor układu torowego", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (r == MessageBoxResult.Cancel) return false;
        return r == MessageBoxResult.No || SaveDoc(false);
    }

    public void NewDoc()
    {
        if (!ConfirmDiscard()) return;
        var d = new UtDocument();
        // nowy projekt w miejscu bieżącego widoku – zwykle obok poprzedniego obszaru
        var ov = Doc.P.View;
        d.P.View = new UtViewState { X = View.Center.X, Y = View.Center.Y, Mpp = View.Mpp, Schemat = ov.Schemat, Stretch = ov.Stretch, ShowBg = ov.ShowBg };
        SetDoc(d); Message("Nowy projekt");
    }

    void OpenDoc()
    {
        if (!ConfirmDiscard()) return;
        var dlg = new OpenFileDialog { Filter = "Układ torowy (*.ut.json)|*.ut.json|Wszystkie pliki|*.*", InitialDirectory = ProjectsDir() };
        if (dlg.ShowDialog(this) != true) return;
        OpenPath(dlg.FileName);
    }

    public bool OpenPath(string path)
    {
        try { SetDoc(UtDocument.Load(path)); Message("Otwarto " + Path.GetFileName(path)); return true; }
        catch (Exception ex) { Message("Nie można otworzyć: " + ex.Message); if (!NoPrompt) MessageBox.Show(this, ex.Message, "Otwieranie projektu"); return false; }
    }

    public bool SaveDoc(bool askName, string path = null)
    {
        path ??= Doc.Path;
        if (path == null || askName)
        {
            if (NoPrompt) return false;
            var dlg = new SaveFileDialog { Filter = "Układ torowy (*.ut.json)|*.ut.json", InitialDirectory = ProjectsDir(), FileName = SafeName(Doc.P.Name) + ".ut.json" };
            if (dlg.ShowDialog(this) != true) return false;
            path = dlg.FileName;
        }
        StoreView();
        try { Doc.Save(path); Message("Zapisano " + Path.GetFileName(path)); return true; }
        catch (Exception ex) { Message("Błąd zapisu: " + ex.Message); if (!NoPrompt) MessageBox.Show(this, ex.Message, "Zapis projektu"); return false; }
    }

    static string SafeName(string n) => string.Concat(n.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    /// <summary>Katalog projektów: <c>projekty_ut</c> w katalogu repozytorium (szukany w górę od programu), inaczej Dokumenty.</summary>
    public static string ProjectsDir()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "PLAN_EDYTOR_UT.md")))
            {
                var p = Path.Combine(d.FullName, "projekty_ut"); Directory.CreateDirectory(p); return p;
            }
        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    void OnClosing(object s, CancelEventArgs e) { if (!ConfirmDiscard()) e.Cancel = true; }

    // ------------------------------------------------------------------ widok
    /// <summary>Idź do punktu (WGS84 albo PUWG 1992); przybliżenie 0,5 m/px, chyba że widok jest już bliżej.</summary>
    public bool GoTo(string text)
    {
        if (!Geo.TryParsePoint(text, out var p)) { Message("Nie rozpoznano punktu – wpisz np. „51.7631, 19.5438” albo „384120 537450” (X Y w PUWG 1992)"); return false; }
        View.SetView(p, Math.Min(View.Mpp, 0.5));
        Message($"Punkt X = {Geo.M(p.X)} m, Y = {Geo.M(p.Y)} m");
        return true;
    }

    void ShowCursor(XY? p)
    {
        if (p is not XY w) { stCursor.Text = ""; return; }
        var (lat, lon) = Geo.ToWgs84(w);
        var txt = $"X = {Geo.M(w.X)} m   Y = {Geo.M(w.Y)} m   ({lat.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture)}, {lon.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture)})";
        // na torze: Z niwelety i km linii
        if (View.NearTrack(w) is var (tr, s) && Doc.P.Tracks.FirstOrDefault(x => x.Id == tr) is UtTrack t)
        {
            double z = Profil.Z(t.Profile, s);
            if (!double.IsNaN(z)) txt += $"   Z = {z.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)} m";
            var km = Kilometraz.Text(Doc.P, tr, s);
            txt += $"   tor {(t.Number.Length > 0 ? t.Number : "?")} {s.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} m" + (km.Length > 0 ? "   " + km : "");
        }
        else if (Doc.P.KmPoints.Count > 0 && Kilometraz.At(Doc.P, w) is { Count: > 0 } a) txt += $"   km {Kilometraz.Fmt(a[0].Km)} (rzut)";
        stCursor.Text = txt;
    }

    void ShowView()
    {
        // 96 px na cal – skala obrazu na typowym monitorze
        double screen = View.Mpp / (0.0254 / 96);
        stView.Text = View.Schemat && View.Stretch > 1
            ? $"schemat: wzdłuż ok. 1:{Geo.M(UtView.Nice(screen), 0)}, w poprzek ok. 1:{Geo.M(UtView.Nice(screen / View.Stretch), 0)}"
            : $"1 px = {UtView.Len(View.Mpp)}   ·   ekran ok. 1:{Geo.M(UtView.Nice(screen), 0)}";
    }

    public void Message(string m) { stMsg.Text = m; MessageShown?.Invoke(m); }
    public string LastMessage => stMsg.Text;

    void OnKey(object s, KeyEventArgs e)
    {
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control), shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        bool inText = Keyboard.FocusedElement is TextBox;
        if (!inText && !ctrl && View.TorKey(e.Key)) { e.Handled = true; return; }
        if (!inText && !ctrl && View.ParKey(e.Key)) { e.Handled = true; return; }
        if (!inText && !ctrl && View.ArcKey(e.Key)) { e.Handled = true; return; }
        if (!inText && !ctrl && View.FitKey(e.Key)) { e.Handled = true; return; }
        if (!inText && View.MultiKey(e.Key)) { e.Handled = true; return; }
        if (ctrl && e.Key == Key.S) { SaveDoc(shift); e.Handled = true; }
        else if (ctrl && e.Key == Key.O) { OpenDoc(); e.Handled = true; }
        else if (ctrl && e.Key == Key.N) { NewDoc(); e.Handled = true; }
        else if (!inText && ctrl && (e.Key == Key.Y || (e.Key == Key.Z && shift))) { DoRedo(); e.Handled = true; }
        else if (!inText && ctrl && e.Key == Key.Z) { DoUndo(); e.Handled = true; }
        else if (!inText && !ctrl && e.Key == Key.Escape)
        {
            if (View.Tool == UtTool.Kalibracja && View.CalPending != null) { View.CalPending = null; View.InvalidateVisual(); }
            else SetTool(UtTool.Wskaz);
            e.Handled = true;
        }
        else if (!inText && !ctrl && e.Key == Key.M) { SetTool(UtTool.Miarka); e.Handled = true; }
        else if (!inText && !ctrl && e.Key == Key.T) { SetTool(UtTool.Tor); e.Handled = true; }
        else if (!inText && !ctrl && e.Key == Key.R) { SetTool(UtTool.Rozjazd); e.Handled = true; }
        else if (!inText && !ctrl && e.Key == Key.P) { SetTool(UtTool.Rownolegly); e.Handled = true; }
        else if (!inText && !ctrl && e.Key == Key.U) { SetTool(UtTool.Urzadzenie); e.Handled = true; }
        else if (!inText && !ctrl && e.Key == Key.K) { SetTool(UtTool.LukOdDo); e.Handled = true; }
        else if (!inText && !ctrl && e.Key == Key.J) { SetTool(UtTool.LukPunkty); e.Handled = true; }
        else if (!inText && !ctrl && e.Key == Key.O) { SetTool(UtTool.Odcinek); e.Handled = true; }
        else if (!inText && e.Key == Key.F7) { RunCheck(true); e.Handled = true; }
        else if (!inText && e.Key == Key.Delete && !View.Busy && DeleteSelection()) e.Handled = true;
        else if (!inText && !ctrl && e.Key is Key.Left or Key.Right or Key.Up or Key.Down && View.NudgeKey(e.Key, shift)) e.Handled = true;
        else if (!inText && !ctrl && e.Key == Key.V) { SetTool(UtTool.Wskaz); e.Handled = true; }
        else if (!inText && !ctrl && e.Key == Key.S) { SetSchemat(!View.Schemat); e.Handled = true; }
        else if (!inText && e.Key == Key.Back && View.Tool == UtTool.Miarka && View.MeasurePts.Count > 0) { View.MeasurePts.RemoveAt(View.MeasurePts.Count - 1); ShowMeasure(); View.InvalidateVisual(); e.Handled = true; }
    }
}

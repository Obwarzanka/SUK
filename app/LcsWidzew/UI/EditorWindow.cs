using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using LcsWidzew.Model;
using LcsWidzew.Sim;

namespace LcsWidzew.UI;

/// <summary>
/// Edytor układu obszaru LCS: tory, zwrotnice, sygnalizatory, kozły, wykolejnice, szlaki (blokada samoczynna / liniowa),
/// perony, napisy, obrazy i posterunki. Zapisuje Data/station.json, który wczytuje symulator.
/// </summary>
public partial class EditorWindow : DarkWindow, IPanelEditor
{
    enum Tool { Select, Track, Section, Switch, Signal, TrackEnd, Derailer, Label, Platform, Szlak, Measure, Link, Turnout, Crossing }
    Point? crossFrom;                 // narzędzie Przejazd
    string turnFrom;
    (string From, string End, string[] Ans)? szlakPend;   // szlak wewnętrzny czekający na wskazanie drugiej stacji                  // narzędzie Rozjazd – węzeł zwrotnicy na pierwszym torze
    string linkFrom;                  // narzędzie Łącznik obrazów
    static CheckBox Chk(string t, bool on, string tip) => new() { Content = t, IsChecked = on, Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), ToolTip = tip };
    readonly CheckBox chkGrid = Chk("siatka", true, "Siatka w tle (oczko = krok przyciągania, co 5 – linia główna)");
    readonly CheckBox chkNodes = Chk("węzły", true, "Punkty węzłów");
    readonly CheckBox chkSecs = Chk("odcinki", false, "Nazwy odcinków izolowanych na torach");
    readonly CheckBox chkLens = Chk("długości", false, "Długości torów [m]");
    readonly CheckBox chkSpeed = Chk("prędkości", false, "Prędkości dopuszczalne torów i ograniczenia zwrotnicowe [km/h]");
    readonly CheckBox chkCross = Chk("celownik", true, "Krzyż celowniczy ze współrzędnymi przy kursorze");
    readonly TextBox findBox = new() { Width = 110, Background = Brushes.Black, Foreground = Brushes.White, CaretBrush = Brushes.White, VerticalAlignment = VerticalAlignment.Center, ToolTip = "Szukaj: semafor, zwrotnica, odcinek, szlak, tor (np. S1, 13, JT4, 2A). Enter – następny wynik." };
    // zaznaczenie wielu obiektów (Shift+przeciągnięcie / Shift+klik)
    readonly HashSet<string> multi = new(); readonly HashSet<int> multiLabels = new(), multiPlats = new();
    Point? bandFrom; bool groupDrag;
    /// <summary>Zaznaczanie obszarem: przeciąganie pustego miejsca rysuje prostokąt zaznaczenia (bez Shift) zamiast przesuwać widok.</summary>
    bool bandMode;
    ToggleButton bandBtn; (double X, double Y) groupApplied;
    Point? measureFrom; string measureText; (Point A, Point B)? measureLine;
    int findIdx;
    public bool NoPrompt;
    readonly TextBox secBox = new() { Width = 90, Background = Brushes.Black, Foreground = Brushes.White, CaretBrush = Brushes.White, VerticalAlignment = VerticalAlignment.Center, ToolTip = "Odcinek izolowany przypisywany narzędziem Odcinek (Ctrl+klik na torze – pobierz odcinek)" };
    List<string> routeEdges;          // podświetlony przebieg w panelu przebiegów

    StationDoc doc;
    string path;
    Station st;
    Simulator sim;
    readonly PanelView panel = new();
    readonly Action<string> apply;
    /// <summary>Po zapisie: odświeżenie układu w otwartym symulatorze (bez przełączania okna).</summary>
    public Action<string> Saved;
    Tool tool = Tool.Select;
    readonly Dictionary<Tool, ToggleButton> toolBtns = new();
    readonly ComboBox postBox = new() { Width = 70 }, gridBox = new() { Width = 55 }, pageBox = new() { Width = 230 };
    readonly CheckBox chkSameSec = new() { Content = "jeden odcinek w ciągu", IsChecked = true, Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
    readonly CheckBox chkSwSec = new() { Content = "odcinek zwrotnicowy", IsChecked = true, Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
    readonly StackPanel props = new() { Margin = new Thickness(8) };
    readonly TextBlock status = new() { Foreground = Brushes.Gray, Margin = new Thickness(8, 3, 8, 3), TextTrimming = TextTrimming.CharacterEllipsis };
    readonly ComboBox blockBox = new() { Margin = new Thickness(0, 2, 0, 4) };
    bool dirty;

    // zaznaczenie i stan narzędzi
    string selKind, selId;            // node, edge, sig, sw, te, der, label, platform, block
    int selIdx = -1;                  // indeks napisu / peronu
    bool dragging, dragSnap; Point dragStart; (double X, double Y) dragOrig;
    string chainFrom, chainSec;       // narzędzie Tor
    string szlakFrom;                 // narzędzie Szlak
    Point? rectFrom;                  // narzędzie Peron
    Point mouse;

    public EditorWindow(string path, Action<string> apply) : base("Edytor układu – LCS", 1500, 900)
    {
        this.path = path; this.apply = apply;
        WindowState = WindowState.Maximized; ShowInTaskbar = true;
        doc = StationDoc.Load(path);

        var root = new DockPanel();
        // --- pasek narzędzi
        var bar = new WrapPanel { Background = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x18)), Margin = new Thickness(0) };
        modeBtn = new Button { Content = "Tryb: PLAN SCHEMATYCZNY (układ torowy)  ⇄", Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(2, 3, 8, 3), Background = new SolidColorBrush(Color.FromRgb(0x1f, 0x3d, 0x5a)), Foreground = Brushes.White, FontWeight = FontWeights.SemiBold,
            ToolTip = "Przełączenie: UKŁAD TOROWY (topologia, odcinki, sygnalizatory, szlaki – dla komputera zależności) ⇄ ZOBRAZOWANIE (obraz pulpitu – położenie elementów niezależne od połączeń)" };
        modeBtn.Click += (a, e) => Safe(ToggleDisplayMode);
        bar.Children.Add(modeBtn);
        planBtn = new Button { Content = "Rozjazdy jako skosy", Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(2, 3, 6, 3), Background = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26)), Foreground = Brushes.White,
            ToolTip = "Plan schematyczny: łączniki i odgałęzienia zwrotnic jako proste skosy jak w terenie (zamiast pulpitowego ukos–pion–ukos). Zaznaczona grupa albo cały plan. Obraz pulpitu się nie zmienia." };
        planBtn.Click += (a, e) => Safe(() =>
        {
            var r = Ask("Rozjazdy jako skosy (plan schematyczny)", ("Pochylenie skosu: długość / wysokość (np. 3 – łagodny, 1 – 45°)", "3"), ("Zakres", multi.Count > 0 ? "zaznaczona grupa|cały plan" : "cały plan"));
            if (r == null) return;
            double ratio = double.TryParse(r[0].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var rv) ? Math.Clamp(rv, 0.5, 12) : 3;
            doc.Snapshot(); doc.EnsureDisplay();
            var k = doc.PlanSkewAll(ratio, r[1].StartsWith("zazn") ? multi : null);
            Changed($"Plan: {k} rozjazdów jako skosy (obraz pulpitu bez zmian)");
        });
        bar.Children.Add(planBtn);
        autoBtn = new Button { Content = "Autoukład w skali", Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(2, 3, 6, 3), Background = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26)), Foreground = Brushes.White,
            ToolTip = "Propozycja planu w skali Ig-10: długości z torów (1:2000), tory w pasach co rozstaw (1:500), łączniki jako skosy, szlaki skrócone. Potem popraw ręcznie (np. na podkładzie). Ctrl+Z cofa całość. Obraz pulpitu się nie zmienia." };
        autoBtn.Click += (a, e) => Safe(() =>
        {
            if (!NoPrompt && !FuzzMode && MessageBox.Show(this, $"Ułożyć plan schematyczny od nowa w skali (propozycja)?{(char)10}{(char)10}Długości z torów, tory w pasach, łączniki jako skosy. Obraz pulpitu się nie zmienia. Ctrl+Z cofa całość.", "Autoukład planu", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            status.Text = "Autoukład planu: " + AutoLayout();
        });
        bar.Children.Add(autoBtn);
        foreach (var (op, txt, tip) in new[] { ("split", "✂ Podziel tor", "Klik na torze – podział na dwa tory w tym miejscu (nowy węzeł, np. pod sygnalizator albo inny odcinek). Działa na planie i na zobrazowaniu. Shift – kolejny bez wychodzenia."),
                                              ("merge", "⊕ Scal tory", "Klik na węźle z dwoma torami (bez zwrotnicy, sygnalizatora, kozła, granicy szlaku) – dwa tory łączą się w jeden (długości się sumują). Działa na planie i na zobrazowaniu.") })
        {
            var b = new Button { Content = txt, ToolTip = tip, Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(2, 3, 2, 3), Background = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26)), Foreground = Brushes.White };
            b.Click += (a, e) => { trackOp = trackOp == op ? null : op; status.Text = trackOp == null ? "Zakończono" : op == "split" ? "PODZIEL TOR: kliknij tor w miejscu podziału (Shift – kolejne, PPM/Esc – koniec)" : "SCAL TORY: kliknij węzeł między dwoma torami (Shift – kolejne, PPM/Esc – koniec)"; panel.Cursor = trackOp == null ? null : Cursors.Cross; };
            bar.Children.Add(b);
        }
        void ToolBtn(Tool t, string text, string tip, Key key)
        {
            var b = new ToggleButton { Content = text, ToolTip = tip + $"  [{key}]", Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(2, 3, 0, 3), Background = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26)), Foreground = Brushes.White };
            b.Click += (a, e) => SetTool(t);
            toolBtns[t] = b; bar.Children.Add(b); toolButtons.Add(b);
        }
        bandBtn = new ToggleButton { Content = "⬚ Zaznacz obszarem", ToolTip = "Zbiorcze zaznaczanie: przeciągnij prostokąt po pustym miejscu – zaznacza węzły (z torami, sygnalizatorami, zwrotnicami), napisy i perony; Shift – dodaje do zaznaczenia. Potem przeciągnij element grupy, strzałki, Del. Ctrl+A – wszystko na obrazie. Wyłączone: przeciąganie przesuwa widok (prostokąt – Shift+przeciąganie).  [Q]",
            Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(2, 3, 0, 3), Background = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26)), Foreground = Brushes.White };
        bandBtn.Click += (a, e) => SetBandMode(bandBtn.IsChecked == true);
        bar.Children.Add(bandBtn);
        ToolBtn(Tool.Select, "↖ Wskaż / przesuń", "Zaznaczanie obiektów i przeciąganie węzłów, napisów i peronów. Przeciąganie pustego miejsca – przesuwanie obrazu, kółko – powiększenie. Del – usuń zaznaczony obiekt.", Key.V);
        ToolBtn(Tool.Track, "━ Tor", "Rysowanie toru: klikaj kolejne punkty (węzeł, tor – podział, puste miejsce – nowy węzeł). Shift – poziomo. PPM / Esc – koniec ciągu.", Key.T);
        ToolBtn(Tool.Section, "┅ Odcinek", "Odcinki izolowane: klik na torze przypisuje mu odcinek z pola obok; Ctrl+klik pobiera odcinek klikniętego toru. Tory odcinka są podświetlone.", Key.I);
        ToolBtn(Tool.Turnout, "⌐ Rozjazd", "Klik na torze (miejsce zwrotnicy), potem: klik na drugim torze – łącznik rozjazdowy z dwiema zwrotnicami (ukos – pion – ukos), albo klik w pustym miejscu – odgałęzienie z nowym torem do tego punktu. Kierunek ukosu: w stronę drugiego kliknięcia.", Key.R);
        ToolBtn(Tool.Switch, "⑂ Zwrotnica", "Klik na węźle z 3 torami – nowa zwrotnica (iglica po stronie pojedynczego toru).", Key.Z);
        ToolBtn(Tool.Signal, "▶ Semafor / tarcza", "Klik na torze lub węźle – nowy semafor wjazdowy/wyjazdowy, tarcza manewrowa, ostrzegawcza, powtarzacz.", Key.S);
        ToolBtn(Tool.TrackEnd, "⊣ Kozioł", "Klik na końcu toru – kozioł oporowy / znacznik końca przebiegu manewrowego.", Key.K);
        ToolBtn(Tool.Derailer, "▮ Wykolejnica", "Klik na torze – wykolejnica.", Key.W);
        ToolBtn(Tool.Link, "⇢ Łącznik obrazów", "Połączenie logiczne końców torów na dwóch obrazach: klik na końcu toru, potem na końcu toru na drugim obrazie (edytor sam przełączy obraz). Podaj długość w metrach.", Key.L);
        ToolBtn(Tool.Szlak, "⇄ Szlak", "Klik na semaforze wjazdowym – edytor idzie torem na zewnątrz stacji (także przez łącznik obrazów) do semafora wjazdowego innej stacji LCS (szlak wewnętrzny) albo do końca toru (szlak zewnętrzny). Bez semafora: klik granica, klik koniec.", Key.B);
        ToolBtn(Tool.Platform, "▭ Peron", "Przeciągnij prostokąt peronu.", Key.P);
        ToolBtn(Tool.Crossing, "Y Przejazd", "Przeciągnij w pionie przez tory – przejazd kolejowo-drogowy (oznaczenie Y albo T, kilometr).", Key.X);
        ToolBtn(Tool.Label, "A Napis", "Klik – nowy napis (np. kierunek szlaku, nazwa grupy torów).", Key.N);
        ToolBtn(Tool.Measure, "↔ Miarka", "Przeciągnij od punktu do punktu: odległość na obrazie i długość drogi po torach [m] między węzłami.", Key.M);
        bar.Children.Add(Lbl("Odcinek:")); bar.Children.Add(secBox);
        secBox.TextChanged += (a, e) => panel.InvalidateVisual();
        bar.Children.Add(chkSameSec); bar.Children.Add(chkSwSec);
        bar.Children.Add(new Border { Width = 12 });
        bar.Children.Add(Lbl("Posterunek:")); bar.Children.Add(postBox);
        bar.Children.Add(Lbl("Siatka:")); gridBox.ItemsSource = new[] { "1", "4", "8", "16", "25", "Ig-10" }; gridBox.ToolTip = "Krok przyciągania. Ig-10 – 10 m wzdłuż × 4,75 m (rozstaw torów) w skali planu."; gridBox.SelectedIndex = 2; bar.Children.Add(gridBox);
        bar.Children.Add(Lbl("Obraz:")); bar.Children.Add(pageBox);
        pageBox.SelectionChanged += (a, e) =>
        {
            if (pageBox.SelectedIndex < 0 || st == null) return;
            if (pageBox.SelectedIndex < st.Pages.Count && st.Pages[pageBox.SelectedIndex].Posts.FirstOrDefault() is string pp && postBox.Items.Contains(pp)) postBox.SelectedItem = pp;
            ApplyPage(); panel.FitPage();
        };
        var bar2 = new WrapPanel { Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x14)) };
        foreach (var c in new[] { chkGrid, chkNodes, chkSecs, chkLens, chkSpeed, chkCross }) { c.Click += (a, e) => panel.InvalidateVisual(); bar2.Children.Add(c); }
        bar2.Children.Add(Lbl("Szukaj:")); bar2.Children.Add(findBox);
        findBox.KeyDown += (a, e) => { if (e.Key == Key.Enter) Safe(() => Find(findBox.Text)); };
        bar2.Children.Add(new Border { Width = 10 });
        bar2.Children.Add(Btn("Cofnij (Ctrl+Z)", (a, e) => DoUndo()));
        bar2.Children.Add(Btn("Ponów (Ctrl+Y)", (a, e) => DoRedo()));
        bar2.Children.Add(Btn("Sprawdź", (a, e) => ShowValidation()));
        bar2.Children.Add(Btn("Nadaj identyfikatory odcinków", (a, e) => Safe(() => { doc.Snapshot(); var k = doc.AutoNameSections(); Changed($"Nadano identyfikatory {k} odcinkom (posterunek:nazwa – z…, JT…, it…)"); })));
        bar2.Children.Add(Btn("Kontrola niezajętości…", (a, e) => Safe(() =>
        {
            var r = Ask("Kontrola niezajętości w obszarze", ("Domyślny rodzaj dla odcinków bez własnego ustawienia", doc.DefaultDetection == "ot" ? "obwody torowe|liczniki osi" : "liczniki osi|obwody torowe"),
                ("Ustaw dla wszystkich odcinków posterunku (puste – bez zmian)", "|" + string.Join("|", doc.Posts.Select(p => p.Key))));
            if (r == null) return;
            doc.Snapshot(); var det = r[0].StartsWith("obw") ? "ot" : "lo"; doc.DefaultDetection = det;
            int n = 0;
            if (r[1] != "") foreach (var s in doc.EdgeObjs.Select(x => StationDoc.Sv(x["sec"])).Distinct().Where(s => StationDoc.SecPost(s) == r[1]).ToList()) { doc.SetSecDetection(s, det); n++; }
            foreach (var k in doc.SecMeta.Where(x => x.Value is JsonObject m && StationDoc.Sv(m["detection"]) == det).Select(x => x.Key).ToList()) doc.SecMeta.Remove(k);
            Changed($"Kontrola niezajętości: domyślnie {(det == "ot" ? "obwody torowe" : "liczniki osi")}{(n > 0 ? $", posterunek {r[1]}: {n} odcinków" : "")}");
        })));
        bar2.Children.Add(Btn("Odcinki na sygnalizatorach", (a, e) => Safe(() => { doc.Snapshot(); var k = doc.SplitSectionsAtSignals(); Changed($"Podzielono odcinki na {k} sygnalizatorach"); })));
        bar2.Children.Add(Btn("Napraw łączniki", (a, e) => Safe(() =>
        {
            doc.Snapshot(); int k = 0;
            foreach (var sw in doc.Switches.Select(x => x.Key).ToList()) k += doc.CrossoverSecs(sw);
            Changed($"Łączniki zwrotnic: {k} torów w odcinkach zwrotnicowych (jedna przerwa izolacyjna na łącznik)");
        })) );
        bar2.Children.Add(Btn("Uprość tory", (a, e) => Safe(() => { doc.Snapshot(); var j = doc.JoinStraySections(); var k = doc.Simplify(); Changed($"Usunięto {j} przypadkowych przerw izolacyjnych, scalono {k} zbędnych węzłów", false); })));
        bar2.Children.Add(Btn("Zapisz (Ctrl+S)", (a, e) => { if (SaveTo(path)) Saved?.Invoke(path); }));
        bar2.Children.Add(Btn("Zapisz jako…", (a, e) => SaveAs()));
        bar2.Children.Add(Btn("Otwórz…", (a, e) => OpenFile()));
        bar2.Children.Add(Btn("Przywróć kopię…", (a, e) => RestoreBackup()));
        bar2.Children.Add(Btn("Nowy obszar…", (a, e) => NewArea()));
        bar2.Children.Add(Btn("Nowy posterunek…", (a, e) => NewPost()));
        bar2.Children.Add(Btn("Nowy obraz…", (a, e) => NewPage()));
        bar2.Children.Add(Btn("Zapisz i uruchom w symulatorze", (a, e) => { if (SaveTo(path)) apply?.Invoke(path); }, new SolidColorBrush(Color.FromRgb(0x1f, 0x5d, 0x2a))));
        bar2.Children.Add(Btn("Pomoc", (a, e) => MessageBox.Show(this, HelpText, "Edytor układu – pomoc")));
        var top = new StackPanel(); top.Children.Add(bar); top.Children.Add(bar2);
        DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status);

        // --- panel właściwości
        var side = new DockPanel { Width = 330, Background = Panel2 };
        var blk = new StackPanel { Margin = new Thickness(8) };
        blk.Children.Add(T("Szlaki (blokady liniowe)", 13, Brushes.White, true));
        blk.Children.Add(blockBox);
        blockBox.SelectionChanged += (a, e) => { if (blockBox.SelectedItem is string b) Select("block", b); };
        DockPanel.SetDock(blk, Dock.Top); side.Children.Add(blk);
        side.Children.Add(new ScrollViewer { Content = props, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        DockPanel.SetDock(side, Dock.Right); root.Children.Add(side);
        root.Children.Add(panel);
        Content = root;

        panel.Editor = this; panel.EditView = true;
        panel.Hover += t => { if (!string.IsNullOrEmpty(t)) status.Text = t; };
        PreviewKeyDown += OnKey;
        Closing += (a, e) =>
        {
            if (!dirty || NoPrompt) return;
            var r = MessageBox.Show(this, "Zapisać zmiany w układzie?", "Edytor układu", MessageBoxButton.YesNoCancel);
            if (r == MessageBoxResult.Cancel) e.Cancel = true; else if (r == MessageBoxResult.Yes && SaveTo(path)) Saved?.Invoke(path);
        };
        Loaded += (a, e) =>
        {
            Rebuild(true); SetTool(Tool.Select); ShowProps();
            if (st.Pages.FirstOrDefault()?.Posts.FirstOrDefault() is string pp && postBox.Items.Contains(pp)) postBox.SelectedItem = pp;
            // edytor otwiera się na zobrazowaniu (obrazy pulpitu); plan schematyczny – przyciskiem trybu
            if (!NoPrompt && st.Nodes.Count > 0) Safe(ToggleDisplayMode);
        };
    }

    /// <summary>Test skryptowy (--editshot … script): rysowanie małej stacji narzędziami edytora, jak myszą.</summary>
    /// <summary>Test narzędzia Rozjazd (--editshot plik rozjazd).</summary>
    public string ScriptRozjazd()
    {
        var log = new System.Text.StringBuilder();
        doc = StationDoc.New("BE", "Bełchów (test)"); path = Path.Combine(Path.GetTempPath(), "lcs_rozjazd.json"); Rebuild(true);
        void Click(Tool t, double x, double y) { if (tool != t) SetTool(t); Down(new Point(x, y), null); Up(new Point(x, y), null); }
        foreach (var x in new[] { 100.0, 1900 }) Click(Tool.Track, x, 448); Right(new Point());
        foreach (var x in new[] { 100.0, 1900 }) Click(Tool.Track, x, 552); Right(new Point());
        // łączniki jak w Bełchowie: 2↔1 w prawo i w lewo, odgałęzienia do torów 4 (w górę) i 3 (w dół)
        Click(Tool.Turnout, 568, 448); Click(Tool.Turnout, 600, 552);
        Click(Tool.Turnout, 704, 552); Click(Tool.Turnout, 680, 448);
        Click(Tool.Turnout, 784, 448); Click(Tool.Turnout, 1300, 344);
        Click(Tool.Turnout, 784, 552); Click(Tool.Turnout, 1300, 648);
        log.AppendLine("Walidacja: " + string.Join(" | ", doc.Validate()));
        var stt = doc.Build();
        log.AppendLine("Zwrotnice: " + string.Join(" ", stt.Switches.Values.Select(x => $"{x.Label}(iglica {(x.TipDir > 0 ? "→" : "←")})")));
        doc.Save(path);
        Rebuild(true); panel.SetZoom(1.6); panel.CenterOn(760, 500); ShowProps();
        return log.ToString();
    }

    /// <summary>Test: dwie stacje LCS na dwóch obrazach połączone szlakiem wewnętrznym (--editshot plik szlaki2).</summary>
    public string ScriptSzlaki2()
    {
        var log = new System.Text.StringBuilder();
        doc = StationDoc.New("TS", "Stacja Zachód"); path = Path.Combine(Path.GetTempPath(), "lcs_szlaki2.json"); Rebuild(true);
        AutoAnswers.Enqueue(new[] { "TN", "Stacja Wschód", "TN" }); NewPage();          // drugi obraz z posterunkiem TN (x 2000–4000)
        void Click(Tool t, double x, double y) { if (tool != t) SetTool(t); Down(new Point(x, y), null); Up(new Point(x, y), null); }
        void Page(int i) { pageBox.SelectedIndex = i; }
        void Sig(int pg, string name, string type, int dir, double x) { Page(pg); AutoAnswers.Enqueue(new[] { name, type, dir > 0 ? "→ na wschód (w prawo)" : "← na zachód (w lewo)", "" }); Click(Tool.Signal, x, 500); }
        // tory rysowane osobno na każdym obrazie, połączone łącznikiem obrazów
        Page(0); foreach (var x in new[] { 40.0, 250, 900, 1960 }) Click(Tool.Track, x, 500); Right(new Point());
        Page(1); foreach (var x in new[] { 2040.0, 2300, 2850, 2990 }) Click(Tool.Track, x, 500); Right(new Point());
        Page(0); AutoAnswers.Enqueue(new[] { "1200" }); Click(Tool.Link, 1960, 500);
        log.AppendLine("Łącznik – po pierwszym kliku obraz: " + pageBox.SelectedIndex);
        Click(Tool.Link, 2040, 500);
        log.AppendLine("Łącznik: " + status.Text);
        Sig(0, "A", "wjazdowy", 1, 250); Sig(0, "F1", "wyjazdowy", 1, 700); Sig(0, "B", "wjazdowy", -1, 900);
        Sig(1, "C", "wjazdowy", 1, 2300); Sig(1, "G1", "wyjazdowy", 1, 2650); Sig(1, "D", "wjazdowy", -1, 2850);
        // szlaki jednym kliknięciem na semaforze wjazdowym
        Page(0); AutoAnswers.Enqueue(new[] { "1Z", "Zachód", "1", "1", "zewnętrzny", BlockDef.Types[0].Name, "PRZYJAZD do tej stacji", "2" }); Click(Tool.Szlak, 250, 500);
        log.AppendLine("Szlak od A: " + status.Text);
        Page(0); AutoAnswers.Enqueue(new[] { "1S", "", "1", "1", "wewnętrzny (oba końce w LCS)", BlockDef.Types[0].Name, "neutralny", "4" }); Click(Tool.Szlak, 900, 500);
        log.AppendLine("Szlak od B: " + status.Text);
        Page(1); AutoAnswers.Enqueue(new[] { "1E", "Wschód", "1", "1", "zewnętrzny", BlockDef.Types[0].Name, "WYJAZD z tej stacji", "1" }); Click(Tool.Szlak, 2850, 500);
        log.AppendLine("Szlak od D: " + status.Text);
        log.AppendLine("Walidacja: " + string.Join(" | ", doc.Validate()));
        try
        {
            var stt = doc.Build();
            foreach (var b in stt.Blocks.Values) log.AppendLine($"Szlak {b.Id}: {b.TypeShort}, końce {string.Join(" / ", b.Ends.Select(e => $"{Station.Lbl(e.EntrySig)} ({e.Post})"))}, wewn. {b.Internal}");
            log.AppendLine("Obrazy: " + string.Join("; ", stt.Pages.Select(p => $"{p.Name} x {p.X0}–{p.X1}, posterunki {string.Join(",", p.Posts)}")));
            var sm = new Simulator(stt); var tr = new Trains(sm); sm.User = new User { Name = "T", Role = "d" };
            tr.Queue.Add(new TimetableEntry { T = 5, Num = "12345", Cat = "IC", From = "1Z", To = "1E", Stop = false });
            string Ex(string pg, string c) { sm.Page = pg; var r = sm.Exec(c); return $"{sm.Clock()} [{pg}] {c}: {(r.Ok ? "OK" : "ODRZ")} {r.Msg}"; }
            log.AppendLine(Ex("TS", "A F1 Pociąg")); log.AppendLine(Ex("TS", "1S Wbl")); log.AppendLine(Ex("TS", "F1 1S Pociąg"));
            log.AppendLine(Ex("TN", "C G1 Pociąg")); log.AppendLine(Ex("TN", "G1 1E Pociąg"));
            for (int i = 0; i < 4 * 1200; i++) sm.Tick(0.25);
            foreach (var e in sm.Events.Where(e => e.Kategoria is "Ruch" or "Alarm")) log.AppendLine($"{e.Czas} {e.Kategoria}: {e.Zdarzenie}");
            log.AppendLine("Kierunek 1S teraz: " + sm.Blocks["1S"].Dir + "; " + Ex("TN", "1S Wbl") + "; " + Ex("TS", "1S Wbl"));
            doc.Save(path);
        }
        catch (Exception ex) { log.AppendLine("BŁĄD: " + ex); }
        Rebuild(true); ShowProps();
        return log.ToString();
    }

    /// <summary>Test szlaków (--editshot plik szlaki): dwie stacje, szlak zewnętrzny SBL, wewnętrzny między posterunkami, zewnętrzny bez odstępów; jazda pociągu.</summary>
    public string ScriptSzlaki()
    {
        var log = new System.Text.StringBuilder();
        doc = StationDoc.New("TS", "Stacja Zachód"); path = Path.Combine(Path.GetTempPath(), "lcs_szlaki.json"); Rebuild(true);
        AutoAnswers.Enqueue(new[] { "TN", "Stacja Wschód" }); NewPost();
        void Click(Tool t, double x, double y) { if (tool != t) SetTool(t); Down(new Point(x, y), null); Up(new Point(x, y), null); }
        void Sig(string post, string name, string type, int dir, double x) { postBox.SelectedItem = post; AutoAnswers.Enqueue(new[] { name, type, dir > 0 ? "→ na wschód (w prawo)" : "← na zachód (w lewo)", "" }); Click(Tool.Signal, x, 500); }
        // jeden tor przez obie stacje i szlaki; stacje: TS 200–900, TN 1300–1900
        postBox.SelectedItem = "TS";
        foreach (var x in new[] { 40.0, 250, 900 }) Click(Tool.Track, x, 500);
        postBox.SelectedItem = "TN";
        foreach (var x in new[] { 1300.0, 1850, 1990 }) Click(Tool.Track, x, 500);
        Right(new Point());
        Sig("TS", "A", "wjazdowy", 1, 250); Sig("TS", "F1", "wyjazdowy", 1, 700); Sig("TS", "B", "wjazdowy", -1, 900);
        Sig("TN", "C", "wjazdowy", 1, 1300); Sig("TN", "G1", "wyjazdowy", 1, 1650); Sig("TN", "D", "wjazdowy", -1, 1850);
        log.AppendLine("Po rysowaniu: " + string.Join(" | ", doc.Validate()) + $"; sygnalizatory {doc.Signals.Count}");
        // szlaki – tak jak w edytorze: klik granica, klik koniec
        postBox.SelectedItem = "TS";
        AutoAnswers.Enqueue(new[] { "1Z", "Posterunek Zachód", "1", "1", "zewnętrzny", "samoczynna (SBL)", "PRZYJAZD do tej stacji" }); Click(Tool.Szlak, 250, 500); Click(Tool.Szlak, 40, 500);
        AutoAnswers.Enqueue(new[] { "1S", "", "1", "1", "wewnętrzny (oba końce w LCS)", "samoczynna (SBL)", "WYJAZD z tej stacji" }); Click(Tool.Szlak, 900, 500); Click(Tool.Szlak, 1300, 500);
        postBox.SelectedItem = "TN";
        AutoAnswers.Enqueue(new[] { "1E", "Posterunek Wschód", "1", "1", "zewnętrzny", "liniowa bez odstępów", "WYJAZD z tej stacji" }); Click(Tool.Szlak, 1850, 500); Click(Tool.Szlak, 1990, 500);
        log.AppendLine("Status po ostatnim szlaku: " + status.Text);
        log.AppendLine("Walidacja: " + string.Join(" | ", doc.Validate()));
        try
        {
            var stt = doc.Build();
            foreach (var b in stt.Blocks.Values) log.AppendLine($"Szlak {b.Id}: {b.Type}, odstępy {string.Join(",", b.SecsWE)}, końce {string.Join(" / ", b.Ends.Select(e => $"{Station.Lbl(e.Boundary)} wyjazd {e.Outward:+0;-0} sem.wjazd. {Station.Lbl(e.EntrySig)} ({e.Post})"))}, far {Station.Lbl(b.Far)}, wewn. {b.Internal}, kier. zasadn. {b.DefDir}");
            log.AppendLine("Sygnalizatory odstępowe: " + string.Join(" ", stt.Signals.Values.Where(x => x.Type == "odstepowy").Select(x => x.Label)));
            var sm = new Simulator(stt); var tr = new Trains(sm);
            foreach (var (k, l) in sm.RouteTable) if (l.Count > 0) log.AppendLine($"  {Station.Lbl(k),-3} {string.Join(", ", l.Select(r => r.Id))}");
            sm.User = new User { Name = "T", Role = "dyżurny ruchu" }; sm.Page = stt.Pages[0].Id;
            tr.Queue.Add(new TimetableEntry { T = 5, Num = "12345", Cat = "IC", From = "1Z", To = "1E", Stop = false });
            string Ex(string c) { var r = sm.Exec(c); return $"{sm.Clock()} {c}: {(r.Ok ? "OK" : "ODRZ")} {r.Msg}"; }
            var plan = new (double T, string C)[] { (2, "1Z Pzk"), (20, "A F1 Pociąg"), (20, "1S Wbl"), (21, "1S Pzk"), (22, "F1 1S Pociąg"), (22, "C G1 Pociąg"), (23, "G1 1E Pociąg") };
            int pi = 0; bool seenOd = false;
            for (int i = 0; i < 4 * 1800; i++)
            {
                while (pi < plan.Length && plan[pi].T <= sm.T) log.AppendLine(Ex(plan[pi++].C));
                sm.Tick(0.25);
                if (!seenOd && stt.Signals.Values.Any(x => x.Type == "odstepowy" && sm.Sigs[x.Name].Aspect == Aspect.Stop && !sm.Sigs[x.Name].Dark && x.Szlak == "1S")) { seenOd = true; log.AppendLine($"{sm.Clock()} SBL 1S: sygnalizator odstępowy na „Stój” za pociągiem"); }
                if (pi == plan.Length && i % 240 == 0 && sm.Routes.Count == 0 && tr.List.All(t => t.Shunt)) { }
            }
            foreach (var e in sm.Events.Where(e => e.Kategoria is "Ruch" or "Alarm")) log.AppendLine($"{e.Czas} {e.Kategoria}: {e.Zdarzenie}");
            log.AppendLine("Pociągi na końcu: " + string.Join("; ", tr.List.Select(t => $"{t.Num} {t.State} czeka={t.WaitingAt}")));
        }
        catch (Exception ex) { log.AppendLine("BŁĄD: " + ex); }
        Rebuild(true); ShowProps();
        return log.ToString();
    }

    public string Script()
    {
        var log = new System.Text.StringBuilder();
        doc = StationDoc.New("TS", "Stacja testowa"); path = Path.Combine(Path.GetTempPath(), "lcs_script.json"); Rebuild(true);
        void Click(Tool t, double x, double y) { if (tool != t) SetTool(t); Down(new Point(x, y), null); Up(new Point(x, y), null); }
        // tor 1 (zasadniczy) i tor 2 z rozjazdami
        Click(Tool.Track, 100, 500); Click(Tool.Track, 400, 500); Click(Tool.Track, 600, 500); Click(Tool.Track, 1400, 500); Click(Tool.Track, 1600, 500); Click(Tool.Track, 1900, 500); Right(new Point());
        chkSameSec.IsChecked = false;
        Click(Tool.Track, 600, 500); Click(Tool.Track, 664, 436); Click(Tool.Track, 1336, 436); Click(Tool.Track, 1400, 500); Right(new Point());
        // ponowne ułożenie: węzły na torze 1 w punktach 600 i 1400 już istnieją (klik w węzeł)
        AutoAnswers.Enqueue(new[] { "1" }); Click(Tool.Switch, 600, 500);
        AutoAnswers.Enqueue(new[] { "2" }); Click(Tool.Switch, 1400, 500);
        AutoAnswers.Enqueue(new[] { "A", "wjazdowy", "→ na wschód (w prawo)", "" }); Click(Tool.Signal, 400, 500);
        AutoAnswers.Enqueue(new[] { "B", "wjazdowy", "← na zachód (w lewo)", "" }); Click(Tool.Signal, 1600, 500);
        AutoAnswers.Enqueue(new[] { "D1", "wyjazdowy", "← na zachód (w lewo)", "" }); Click(Tool.Signal, 700, 500);
        AutoAnswers.Enqueue(new[] { "F1", "wyjazdowy", "→ na wschód (w prawo)", "" }); Click(Tool.Signal, 1300, 500);
        AutoAnswers.Enqueue(new[] { "D2", "wyjazdowy", "← na zachód (w lewo)", "" }); Click(Tool.Signal, 720, 436);
        AutoAnswers.Enqueue(new[] { "F2", "wyjazdowy", "→ na wschód (w prawo)", "" }); Click(Tool.Signal, 1280, 436);
        // szlaki: zachodni SBL, wschodni bez odstępów
        AutoAnswers.Enqueue(new[] { "1Z", "Sąsiad Zachód", "1", "1", "zewnętrzny", "samoczynna (SBL)", "WYJAZD z tej stacji" }); Click(Tool.Szlak, 400, 500); Click(Tool.Szlak, 100, 500);
        AutoAnswers.Enqueue(new[] { "1W", "Sąsiad Wschód", "1", "1", "zewnętrzny", "liniowa bez odstępów", "PRZYJAZD do tej stacji" }); Click(Tool.Szlak, 1600, 500); Click(Tool.Szlak, 1900, 500);
        // tor stacyjny 2 i peron, napis, przesunięcie węzła
        AutoAnswers.Enqueue(new[] { "Peron 1" }); SetTool(Tool.Platform); Down(new Point(800, 456), null); Move(new Point(1200, 480), null); Up(new Point(1200, 480), null);
        AutoAnswers.Enqueue(new[] { "Sąsiad Zachód", "start" }); Click(Tool.Label, 100, 470);
        log.AppendLine("Walidacja: " + string.Join(" | ", doc.Validate()));
        try
        {
            var stt = doc.Build(); var sm = new Simulator(stt); new Trains(sm);
            log.AppendLine($"Zwrotnice: {string.Join(" ", stt.Switches.Values.Select(x => x.Label))}; semafory: {string.Join(" ", stt.Signals.Values.Select(x => x.Label))}; szlaki: {string.Join(" ", stt.Blocks.Values.Select(b => b.Id + "(" + b.Type + "," + b.SecsWE.Count + ")"))}");
            foreach (var (k, l) in sm.RouteTable) if (l.Count > 0) log.AppendLine($"  {Station.Lbl(k),-4} {string.Join(", ", l.Select(r => r.Id))}");
            sm.User = new User { Name = "T", Role = "dyżurny ruchu" };
            log.AppendLine("A F2 Pociąg: " + sm.Exec("A F2 Pociąg").Msg);
            log.AppendLine("D1 A Manewr: " + sm.Exec("D1 A Manewr").Msg);
        }
        catch (Exception ex) { log.AppendLine("BŁĄD: " + ex.Message); }
        selKind = null; Rebuild(true); ShowProps();
        return log.ToString();
    }

    public string RespaceForTest(Rect r, double sp)
    {
        pageBox.SelectedIndex = 0; SelectRect(r);
        var before = multi.Count;
        Safe(() => Respace(sp));
        var stt = status.Text;
        SelectRect(r with { Height = r.Height });   // ponowne zaznaczenie do pokazania wymiarów
        return $"zaznaczono {before}, status: {stt}";
    }

    public string NormalizeForTest(double x, double y, double z)
    {
        pageBox.SelectedIndex = 0;
        var log = new System.Text.StringBuilder(); int ok = 0; var done = new HashSet<string>();
        doc.Snapshot();
        foreach (var sw in doc.Switches.Where(q => doc.Pos(StationDoc.Sv(q.Value["node"])).X < 0).Select(q => q.Key).ToList())
        {
            if (!doc.Switches.ContainsKey(sw) || done.Contains(sw)) continue;
            try { var o = doc.NormalizeTurnout(sw, Math.Max(4, Grid)); ok++; done.Add(sw); if (o != null) done.Add(o); }
            catch (InvalidOperationException ex) { log.AppendLine("  pominięto " + Station.Lbl(sw) + ": " + ex.Message); }
        }
        log.AppendLine($"wyprostowano {ok}; walidacja: " + string.Join(" | ", doc.Validate().Take(8)));
        try { var stt = doc.Build(); var sm = new Simulator(stt); log.AppendLine($"przebiegów: {sm.RouteTable.Values.Sum(l => l.Count)}, zwrotnic: {stt.Switches.Count}"); } catch (Exception ex) { log.AppendLine("BŁĄD modelu: " + ex.Message); }
        Rebuild(); ZoomForTest(x, y, z);
        return log.ToString();
    }

    /// <summary>Test na pliku użytkownika: szlaki wewnętrzne Niciarniana (C1/C2) – Widzew (E/D) przez dwa obrazy.</summary>
    public string AutoNameForTest() { var before = doc.EdgeObjs.ToDictionary(e => StationDoc.Sv(e["id"]), e => StationDoc.Sv(e["sec"])); doc.AutoNameSections(); Rebuild(); var ch = doc.EdgeObjs.Where(e => before[StationDoc.Sv(e["id"])] != StationDoc.Sv(e["sec"])).Select(e => before[StationDoc.Sv(e["id"])] + " → " + StationDoc.Sv(e["sec"])).Distinct().ToList(); var e0 = doc.EdgeObjs.First(e => StationDoc.Sv(e["sec"]).Contains(":JT")); Select("edge", StationDoc.Sv(e0["id"])); return string.Join(System.Environment.NewLine, ch); }
    public string ProblemsForTest(int i) { ShowValidation(); GoToProblem(i); return string.Join(System.Environment.NewLine, problems.Select((p, k) => $"{k + 1}. [{string.Join(",", p.Refs)}] {p.Msg}")); }

    public string ValidateText() => string.Join(Environment.NewLine, doc.Validate());

    public string SzlakDiag()
    {
        var log = new System.Text.StringBuilder();
        foreach (var (k, v) in doc.Signals.Where(x => StationDoc.Sv(x.Value["type"]) == "wjazdowy").ToList())
        {
            var nid = StationDoc.Sv(v["node"]);
            var tr = doc.TraceSzlak(nid);
            log.AppendLine($"{k} node={nid} dir={v["dir"]} szlak={StationDoc.Sv(v["szlak"])} krawędzie={doc.EdgesAt(nid).Count} trace={(tr is { } t ? $"{t.Other} wewn={t.Internal} {t.Sig} krawędzie końca={doc.EdgesAt(t.Other).Count}" : "BRAK")} granica={doc.Boundaries.ContainsKey(nid)}");
        }
        log.AppendLine("Szlaki: " + string.Join(", ", doc.Blocks.Select(b => b.Key)));
        // próba: narzędzie Szlak na każdym wolnym semaforze wjazdowym – złącze
        foreach (var (k, v) in doc.Signals.Where(x => StationDoc.Sv(x.Value["type"]) == "wjazdowy" && (x.Value["szlak"] == null || StationDoc.Sv(x.Value["post"]) == "LF")).ToList())
        {
            var n = st.Nodes[StationDoc.Sv(v["node"])];
            if (v["szlak"] != null) AutoAnswers.Enqueue(new[] { "usuń szlak" });
            AutoAnswers.Enqueue(new[] { "T" + Station.Lbl(k), "", "17", "1", "wewnętrzny – złącze SZLAK WEWNĘTRZNY (połączysz je ze złączem drugiej stacji)", BlockDef.Types[0].Name, "neutralny", "1" });
            AutoAnswers.Enqueue(new[] { "później", "2000" });
            SetTool(Tool.Szlak);
            pageBox.SelectedIndex = st.Pages.FindIndex(p => n.X >= p.X0 && n.X < p.X1);
            Down(new Point(n.X, n.Y), null); Up(new Point(n.X, n.Y), null);
            log.AppendLine($"klik {k}: {status.Text} | złącza={doc.Ports.Count} pozostałe odp.={AutoAnswers.Count}");
            AutoAnswers.Clear();
        }
        var pk = doc.Ports.ToDictionary(x => StationDoc.Sv(x.Value["id"]), x => x.Key);
        foreach (var (x, y) in new[] { ("TE", "TC1"), ("TD", "TC2") })
            try { var bid = doc.PairPorts(pk[x], pk[y], 1500); var bs = doc.Build(); log.AppendLine($"połącz {x}+{y}: {bid} końce {string.Join(" / ", bs.Blocks[bid].Ends.Select(e => Station.Lbl(e.EntrySig) + "(" + e.Post + ")"))}"); }
            catch (Exception ex) { log.AppendLine($"połącz {x}+{y}: BŁĄD {ex.Message}"); }
        log.AppendLine("Walidacja: " + string.Join(" | ", doc.Validate()));
        return log.ToString();
    }

    public string UserSzlakTest()
    {
        var log = new System.Text.StringBuilder();
        foreach (var b in new[] { "1W", "2W" }) if (doc.Blocks.ContainsKey(b)) doc.DeleteBlock(b);
        Rebuild();
        void ClickAt(string sigKey)
        {
            var n = st.Nodes[st.Signals[sigKey].Node];
            pageBox.SelectedIndex = st.Pages.FindIndex(p => n.X >= p.X0 && n.X < p.X1);
            Down(new Point(n.X, n.Y), null); Up(new Point(n.X, n.Y), null);
            log.AppendLine($"klik {sigKey}: {status.Text}");
        }
        SetTool(Tool.Szlak);
        foreach (var (a, b, id) in new[] { ("LF:C1", "LW:E", "1F"), ("LF:C2", "LW:D", "2F") })
        {
            AutoAnswers.Enqueue(new[] { id, "", "17", id[..1], BlockDef.Types[0].Name.Length > 0 ? "wewnętrzny – do innej stacji LCS (po OK wskaż jej semafor wjazdowy, także na innym obrazie)" : "", BlockDef.Types[0].Name, "neutralny", "2" });
            AutoAnswers.Enqueue(new[] { "1500" });
            SetTool(Tool.Szlak); ClickAt(a); ClickAt(b);
        }
        log.AppendLine("Walidacja: " + string.Join(" | ", doc.Validate().Where(x => x.Contains("Szlak") || x.Contains("szlak"))));
        var stt = doc.Build();
        foreach (var id in new[] { "1F", "2F" }) if (stt.Blocks.TryGetValue(id, out var bd)) log.AppendLine($"{id}: {bd.TypeShort}, końce {string.Join(" / ", bd.Ends.Select(e => Station.Lbl(e.EntrySig) + " (" + e.Post + ")"))}, wewn. {bd.Internal}");
        var sm = new Simulator(stt);
        log.AppendLine("Przebiegi: C1→" + string.Join(",", sm.RouteTable.GetValueOrDefault("LF:C1", new()).Where(r => r.Type == "P").Select(r => Station.Lbl(r.End))) + "; L1→" + string.Join(",", sm.RouteTable.GetValueOrDefault("LW:L1", new()).Where(r => r.Type == "P").Select(r => Station.Lbl(r.End))));
        doc.Save(Path.Combine(Path.GetTempPath(), "lcs_user_szlak.json"));
        return log.ToString();
    }

    public void ZoomForTest(double x, double y, double z) { panel.SetZoom(z); panel.CenterOn(x, y); panel.UpdateLayout(); }

    public void DemoTools()
    {
        pageBox.SelectedIndex = st.Pages.FindIndex(p => p.Id == "LW"); panel.FitPage();
        chkSecs.IsChecked = true; chkLens.IsChecked = false; gridBox.SelectedIndex = 2;
        SelectRect(new Rect(1330, 640, 330, 300));
        SetTool(Tool.Measure);
        Down(new Point(st.Nodes["LW:sgS1"].X, st.Nodes["LW:sgS1"].Y), null); Move(new Point(1760, 783), null); Up(new Point(st.Nodes["LW:sgV"].X, st.Nodes["LW:sgV"].Y), null);
        mouse = new Point(1000, 950);
    }

    public string BlockPanelTest(string id)
    {
        var log = new System.Text.StringBuilder();
        Select("block", id);
        void Flush() => Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => { }));
        int n = 0;
        while (true)
        {
            var boxes = props.Children.OfType<TextBox>().ToList();
            boxes.AddRange(props.Children.OfType<StackPanel>().SelectMany(x => x.Children.OfType<TextBox>()));
            if (n >= boxes.Count) break;
            var tb = boxes[n]; var before = doc.Root.ToJsonString(); var old = tb.Text;
            var nv = double.TryParse(old, out var dv) ? (dv + 1).ToString() : old + "X";
            if (old == "") nv = "77";
            tb.Text = nv; tb.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent)); Flush();
            bool changed = doc.Root.ToJsonString() != before;
            log.AppendLine($"pole {n}: '{old}' -> '{nv}': {(changed ? "zapisane" : "NIE ZAPISANE")} | {status.Text}");
            if (selKind != "block") Select("block", doc.Blocks.ContainsKey(id) ? id : doc.Blocks.Select(x => x.Key).First(k => k.StartsWith(id)));
            id = selId; n++;
        }
        var combos = props.Children.OfType<ComboBox>().ToList();
        for (int i = 0; i < combos.Count; i++)
        {
            combos = props.Children.OfType<ComboBox>().ToList(); if (i >= combos.Count) break;
            var cb = combos[i]; var before = doc.Root.ToJsonString(); var oi = cb.SelectedIndex;
            var cnt = ((System.Collections.IList)cb.ItemsSource).Count; if (cnt < 2) { log.AppendLine($"lista {i}: tylko {cnt} opcja"); continue; }
            cb.SelectedIndex = (oi + 1) % cnt; Flush();
            log.AppendLine($"lista {i}: {oi} -> {cb.SelectedIndex}: {(doc.Root.ToJsonString() != before ? "zapisane" : "NIE ZAPISANE")} | {status.Text}");
            if (selKind != "block") Select("block", id);
        }
        return log.ToString();
    }

    public string BlockForTest(string id, string rename)
    {
        Select("block", id);
        if (rename != null) { doc.Snapshot(); doc.RenameBlock(id, rename); Changed("test"); Select("block", rename); }
        var stt = doc.Build(); var sm = new Simulator(stt);
        var b = stt.Blocks[rename ?? id];
        return $"{b.Id}: odstępy {string.Join(",", b.SecsWE)}; końce {string.Join(" / ", b.Ends.Select(e => Station.Lbl(e.EntrySig) + " " + e.Label))}; odstępowe {string.Join(",", stt.Signals.Values.Where(x => x.Szlak == b.Id).Select(x => x.Name + "=" + x.Label))}; przebiegi na szlak {sm.RouteTable.Values.SelectMany(l => l).Count(r => r.End == b.Id)}; walidacja: {string.Join(" | ", doc.Validate().Where(x => x.Contains(b.Id) || x.Contains(id)))}";
    }

    public void SelectForTest(string sig) { pageBox.SelectedIndex = st.Pages.FindIndex(p => p.Posts.Contains(Station.PostOf(sig))); Select("sig", sig); var r = sim.RawRoutes(sig).FirstOrDefault(x => x.Sw.Count > 2); routeEdges = r?.Edges; panel.FitPage(); }

    static TextBlock Lbl(string s) => new() { Text = s, Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0) };

    const string HelpText =
        "Edytor zapisuje plik Data/station.json – ten sam, który wczytuje symulator.\n\n" +
        "Kierunek: oś X rośnie na wschód (w prawo). Tor musi mieć składową poziomą – pionowych krawędzi nie wolno rysować (połączenia rysuj ukośnie).\n\n" +
        "Tory pionowe są dozwolone: kierunek jazdy edytor ustala z toru, z którego wjeżdża się na odcinek pionowy (albo pyta); można go zmienić polem „dir” toru.\n\n" +
        "1. Tor (T): klikaj kolejne punkty. Klik na istniejącym torze dzieli go i tworzy węzeł. Shift – rysowanie poziome. PPM lub Esc kończy ciąg. „Jeden odcinek w ciągu” – cały ciąg dostaje jeden odcinek izolowany.\n" +
        "2. Zwrotnica (Z): klik na węźle, z którego wychodzą 3 tory – po jednej stronie iglica, po drugiej dwa odgałęzienia. Kierunek zasadniczy (+) to odgałęzienie bardziej poziome.\n" +
        "3. Semafor / tarcza (S): klik na torze. Typ: wjazdowy, wyjazdowy, tm (tarcza manewrowa), to (ostrzegawcza), sp (powtarzacz). Kierunek → – dla jazdy na wschód.\n" +
        "4. Kozioł (K): na końcu toru – koniec przebiegu manewrowego.\n" +
        "5. Szlak (B): klik na węźle semafora wjazdowego (granica stacji), potem na końcu toru szlakowego przy krawędzi obrazu. Dla szlaku między dwoma posterunkami LCS – drugi klik na granicy drugiego posterunku. Po utworzeniu ustaw liczbę odstępów: 1 – blokada liniowa bez sygnalizatorów odstępowych, 2 i więcej – samoczynna blokada liniowa (SBL).\n" +
        "6. Narzędzia: siatka (G), celownik ze współrzędnymi, warstwy odcinków i długości, szukaj (Ctrl+F), miarka (M) – odległość i droga po torach w metrach, Ctrl przy rysowaniu toru – kąt 45°, Shift – poziomo.\n" +
        "Tory: dwuklik na torze zaznacza cały tor (Shift+dwuklik – kolejny); przesuwanie myszą (tylko w jednej osi, Ctrl – dowolnie) lub strzałkami; „Rozstaw tory w pionie” – równy odstęp torów. Linijka pokazuje długości w kratkach siatki.\n" +
        "Rozjazd (R): klik na torze, potem na drugim torze – łącznik z dwiema zwrotnicami w kształcie z pulpitu (ukos 45° – pion – ukos 45°, wielkość ukosu = krok siatki); klik w puste miejsce – odgałęzienie z nowym torem.\n" +
        "Łącznik obrazów (L): tor kończący się przy krawędzi jednego obrazu łączy się logicznie z torem na drugim obrazie (klik koniec – klik koniec, długość w metrach). Na pulpicie w tym miejscu jest napis z nazwą obrazu – klik przełącza obraz. Szlak (B): klik na semaforze wjazdowym wystarczy – drugi koniec edytor znajdzie sam.\n" +
        "7. Grupa: Shift+przeciągnięcie – zaznaczenie prostokątem, Shift+klik – dodaj/usuń węzeł, Ctrl+A – cały obraz; przeciąganie / strzałki przesuwają grupę, wyrównanie w poziomie, równe odstępy, przypisanie odcinka, usuwanie.\n" +
        "8. Właściwości (panel z prawej): odcinek izolowany, numer toru, długość, nazwy, typy. Enter zatwierdza pole.\n\n" +
        "Przebiegi pociągowe i manewrowe są wyznaczane samoczynnie z układu: od semafora/tarczy do kolejnego sygnalizatora w tym samym kierunku, kozła albo szlaku.";

    // ------------------------------------------------------------------ budowa widoku
    void Rebuild(bool fit = false)
    {
        try { RebuildCore(fit); }
        catch (Exception ex) { status.Text = "Błąd modelu: " + ex.Message; }
    }

    /// <summary>Budowa modelu z dokumentu; błąd przerywa operację (Safe wycofa zmianę).</summary>
    void RebuildCore(bool fit = false)
    {
        {
            if (dispMode && doc.Disp == null) doc.EnsureDisplay();
            panel.PlanView = !dispMode;
            st = doc.Build();
            sim = new Simulator(st, false); new Trains(sim);
            panel.Sim = sim;
            panel.Geometry = dispMode && st.Display != null ? st.Display : st;   // tryb „Układ torowy” – rysunek układu, „Zobrazowanie” – obrazy pulpitu
            RefreshLists();
            ApplyPage();
            if (fit) panel.FitPage();
            panel.InvalidateVisual();
        }
    }

    void RefreshLists()
    {
        var posts = st.Posts.Keys.ToList(); if (posts.Count == 0) posts.Add("ST");
        var cur = postBox.SelectedItem as string;
        postBox.ItemsSource = posts; postBox.SelectedItem = cur != null && posts.Contains(cur) ? cur : posts[0];
        var pages = st.Pages.Select(p => p.Name).Append("Cały obszar").ToList();
        int pi = pageBox.SelectedIndex;
        pageBox.ItemsSource = pages; pageBox.SelectedIndex = pi >= 0 && pi < pages.Count ? pi : 0;
        var cb = blockBox.SelectedItem as string;
        blockBox.ItemsSource = st.Blocks.Keys.OrderBy(k => k).ToList(); blockBox.SelectedItem = cb;
    }

    void ApplyPage()
    {
        if (st == null) return;
        int i = pageBox.SelectedIndex;
        if (i >= 0 && i < st.Pages.Count) panel.Page = st.Pages[i];
        else
        {
            double x0 = st.Nodes.Count > 0 ? st.Nodes.Values.Min(n => n.X) - 100 : 0, x1 = st.Nodes.Count > 0 ? st.Nodes.Values.Max(n => n.X) + 100 : 2000;
            panel.Page = new PageDef { Id = st.Pages.FirstOrDefault()?.Id ?? "ALL", Name = "Cały obszar", X0 = x0, X1 = x1 };
        }
        sim.Page = panel.Page.Id;
    }

    void Changed(string msg = null, bool keepSel = true)
    {
        dirty = true;
        doc.CleanDisplay();
        try { RebuildCore(); }
        catch (Exception ex) { throw new InvalidOperationException("zmiana daje niepoprawny model (" + ex.GetType().Name + ": " + ex.Message + ")", ex); }
        if (!keepSel || !Exists()) { selKind = null; selId = null; selIdx = -1; }
        ShowProps();
        RefreshProblems();
        if (msg != null) status.Text = msg;
    }

    bool Exists() => selKind switch
    {
        "node" => doc.Nodes.ContainsKey(selId), "edge" => doc.Edge(selId) != null, "sig" => doc.Signals.ContainsKey(selId), "sw" => doc.Switches.ContainsKey(selId),
        "te" => doc.TrackEnds.ContainsKey(selId), "der" => doc.Derailers.ContainsKey(selId), "block" => doc.Blocks.ContainsKey(selId),
        "multi" => multi.Count + multiLabels.Count + multiPlats.Count > 0,
        "crossing" => selIdx >= 0 && selIdx < doc.Crossings.Count,
        "port" => doc.Ports.ContainsKey(selId),
        "label" => selIdx >= 0 && selIdx < doc.Labels.Count, "platform" => selIdx >= 0 && selIdx < doc.Platforms.Count, _ => false
    };

    string Post => postBox.SelectedItem as string ?? "ST";
    bool GridIg => (string)gridBox.SelectedItem == "Ig-10" && doc != null;
    /// <summary>Krok przyciągania w poziomie; GridY – w pionie (różny przy siatce Ig-10).</summary>
    double Grid => GridIg ? 10 * doc.Ux : double.Parse((string)gridBox.SelectedItem ?? "8", CultureInfo.InvariantCulture);
    double GridY => GridIg ? 4.75 * doc.Uy : Grid;
    /// <summary>Przyciąganie do kierunku 45° (połączenia rozjazdowe) – Ctrl przy rysowaniu toru.</summary>
    static Point Snap45((double X, double Y) a, Point p)
    {
        double dx = p.X - a.X, dy = p.Y - a.Y;
        if (Math.Abs(dy) < Math.Abs(dx) * 0.41) return new Point(p.X, a.Y);
        var d = Math.Max(Math.Abs(dx), Math.Abs(dy));
        return new Point(a.X + Math.Sign(dx == 0 ? 1 : dx) * d, a.Y + Math.Sign(dy) * d);
    }
    Point Snap(Point p) { double g = Grid, gy = GridY; return new Point(Math.Round(p.X / g) * g, Math.Round(p.Y / gy) * gy); }
    double R => Math.Max(6, 9 / panel.Scale);

    void SetBandMode(bool on)
    {
        bandMode = on; if (bandBtn != null) { bandBtn.IsChecked = on; bandBtn.Background = new SolidColorBrush(on ? Color.FromRgb(0x0a, 0x4a, 0x66) : Color.FromRgb(0x26, 0x26, 0x26)); }
        if (on && tool != Tool.Select) SetTool(Tool.Select);
        status.Text = on ? "ZAZNACZ OBSZAREM: przeciągnij prostokąt po pustym miejscu (Shift – dodaj do zaznaczenia), potem przeciągnij element grupy / strzałki / Del. Q – wyłącz" : "Zaznaczanie obszarem wyłączone – przeciąganie pustego miejsca przesuwa widok";
    }

    void SetTool(Tool t)
    {
        tool = t; chainFrom = null; chainSec = null; szlakFrom = null; rectFrom = null; szlakPend = null;
        foreach (var (k, b) in toolBtns) { b.IsChecked = k == t; b.Background = k == t ? new SolidColorBrush(Color.FromRgb(0x0a, 0x4a, 0x66)) : new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26)); }
        status.Text = ((string)toolBtns[t].ToolTip).Split("  [")[0] + (t != Tool.Select ? "   ·  Esc / PPM / V – powrót do „Wskaż”" : "");
        panel.InvalidateVisual();
    }

    // ------------------------------------------------------------------ wskazywanie
    string PickNode(Point w)
    {
        string best = null; double bd = R;
        foreach (var n in st.Nodes.Values)
        {
            if (!OnPage(n.X)) continue;
            var d = Math.Sqrt((n.X - w.X) * (n.X - w.X) + (n.Y - w.Y) * (n.Y - w.Y));
            if (d < bd) { bd = d; best = n.Id; }
        }
        return best;
    }
    string PickEdge(Point w)
    {
        string best = null; double bd = R * 0.8;
        foreach (var e in st.Edges.Values.Where(x => !x.Link))
            for (int i = 0; i < e.Pts.Count - 1; i++)
            {
                var d = DistSeg(w, e.Pts[i], e.Pts[i + 1]);
                if (d < bd) { bd = d; best = e.Id; }
            }
        return best;
    }
    int PickLabel(Point w)
    {
        for (int i = 0; i < V.Labels.Count; i++)
        {
            var l = V.Labels[i]; double wd = l.Text.Length * 6.2, x0 = l.Anchor == "middle" ? l.X - wd / 2 : l.Anchor == "end" ? l.X - wd : l.X;
            if (w.X >= x0 - 3 && w.X <= x0 + wd + 3 && Math.Abs(w.Y - l.Y) < 8) return i;
        }
        return -1;
    }
    int PickPlatform(Point w)
    {
        for (int i = 0; i < V.Platforms.Count; i++) { var p = V.Platforms[i]; if (new Rect(p.X, p.Y, p.W, p.H).Contains(w)) return i; }
        return -1;
    }
    bool OnPage(double x) => panel.Page == null || (x >= panel.Page.X0 - 1 && x <= panel.Page.X1 + 1);
    static double DistSeg(Point p, (double X, double Y) a, (double X, double Y) b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
        double t = l2 == 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2, 0, 1);
        return Math.Sqrt(Math.Pow(p.X - a.X - t * dx, 2) + Math.Pow(p.Y - a.Y - t * dy, 2));
    }

    /// <summary>Węzeł pod kursorem; na torze – podział toru; w pustym miejscu – nowy węzeł (gdy allowNew).</summary>
    string NodeAt(Point w, bool allowNew, bool shiftY = false)
    {
        var n = PickNode(w); if (n != null) return n;
        var e = PickEdge(w);
        if (e != null) return doc.SplitEdge(e, w.X, w.Y, Post);
        if (!allowNew) return null;
        var p = Snap(w);
        if (shiftY && chainFrom != null) p.Y = st.Nodes.TryGetValue(chainFrom, out var cf) ? cf.Y : doc.Pos(chainFrom).Y;
        else if (chainFrom != null && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) p = Snap45(doc.Pos(chainFrom), p);
        return doc.AddNode(Post, p.X, p.Y);
    }

    void Select(string kind, string id, int idx = -1)
    {
        selKind = kind; selId = id; selIdx = idx; routeEdges = null;
        if (kind == "block" && blockBox.SelectedItem as string != id) blockBox.SelectedItem = id;
        ShowProps(); panel.InvalidateVisual();
    }

    // ------------------------------------------------------------------ IPanelEditor
    Button modeBtn, planBtn, autoBtn;
    readonly List<UIElement> toolButtons = new();

    /// <summary>Operacja na torach w obu trybach: „split” – podział toru, „merge” – scalenie torów w węźle.</summary>
    string trackOp;

    bool TrackOpDown(Point w)
    {
        bool keep = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        if (trackOp == "split")
        {
            string eid; double f;
            if (dispMode) { if (DPickSeg(w) is not { } sg) { status.Text = "Kliknij tor (linia) w miejscu podziału"; return true; } eid = sg.Edge; f = StationDoc.FractionAt(DS.Edges[eid].Pts, w.X, w.Y); }
            else { eid = PickEdge(w); if (eid == null) { status.Text = "Kliknij tor (linia) w miejscu podziału"; return true; } f = StationDoc.FractionAt(st.Edges[eid].Pts, w.X, w.Y); }
            doc.Snapshot();
            var nn = doc.SplitEdgeAtFraction(eid, f);
            Changed($"Podzielono tor {eid} – nowy węzeł {nn} (odcinek bez zmian; nowy odcinek – narzędzie Odcinek albo „Nowy odcinek tylko dla tego toru”)");
        }
        else
        {
            var nid = dispMode ? DPickNode(w) : PickNode(w);
            if (nid == null) { status.Text = "Kliknij węzeł między dwoma torami"; return true; }
            if (!doc.IsPlainNode(nid)) { status.Text = $"Węzeł {nid}: scalić można tylko zwykły punkt z dwoma torami (bez zwrotnicy, sygnalizatora, kozła, wykolejnicy, granicy szlaku)"; return true; }
            doc.Snapshot();
            try { doc.MergeNode(nid); dSel.Remove(nid); Changed($"Scalono tory w węźle {nid}"); }
            catch (InvalidOperationException ex) { doc.Undo(); Rebuild(); status.Text = "✗ " + ex.Message; }
        }
        if (!keep) { trackOp = null; panel.Cursor = null; }
        return true;
    }

    public bool Down(Point w, MouseButtonEventArgs e)
    {
        if (trackOp != null && e?.ChangedButton != MouseButton.Right) { bool r1 = true; Safe(() => r1 = TrackOpDown(w)); return r1; }
        if (bgCal > 0 && !dispMode && e?.ChangedButton != MouseButton.Right) { Safe(() => BgCalClick(w)); return true; }
        if (bgOp != null && !dispMode && e?.ChangedButton != MouseButton.Right) { Safe(() => BgOpClick(w)); return true; }
        if (dispMode && !DecoTool(tool)) { bool r0 = true; Safe(() => r0 = DDown(w, e)); return r0; }
        bool res = true;
        var t0 = tool; bool keep = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        Safe(() => res = DownCore(w, e));
        // narzędzia wstawiania jednego obiektu – po wstawieniu (lub anulowaniu) powrót do „Wskaż”; Shift – zostaje do wstawienia kolejnego
        bool oneShot = t0 is Tool.Switch or Tool.Signal or Tool.TrackEnd or Tool.Derailer or Tool.Label || (t0 == Tool.Szlak && szlakFrom == null && szlakPend == null) || (t0 == Tool.Link && linkFrom == null) || (t0 == Tool.Turnout && turnFrom == null);
        if (oneShot && !keep && !FuzzMode && tool == t0) { var msg = status.Text; SetTool(Tool.Select); status.Text = msg + "   (Shift+klik – kolejny bez wychodzenia z narzędzia)"; }
        return res;
    }

    bool DownCore(Point w, MouseButtonEventArgs e)
    {
        mouse = w;
        bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        try
        {
            switch (tool)
            {
                case Tool.Select:
                {
                    bool sh = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
                    if (sh && e?.ClickCount == 2 && PickEdge(w) is string te2) { foreach (var n2 in doc.TrackLine(te2)) multi.Add(n2); selKind = multi.Count > 0 ? "multi" : null; ShowProps(); return true; }
                    var hn = PickNode(w);
                    if (sh && hn != null) { if (!multi.Remove(hn)) multi.Add(hn); selKind = multi.Count > 0 ? "multi" : null; ShowProps(); return true; }
                    if (sh) { bandFrom = w; return true; }
                    if (hn != null && multi.Contains(hn) && multi.Count + multiLabels.Count + multiPlats.Count > 1) { StartGroupDrag(w); return true; }
                    ClearMulti();
                    var (k, id) = panel.HitTest(w);
                    if (k == "sig") { Select("sig", id); StartDrag(st.Signals[id].Node, w); return true; }
                    if (k == "sw") { Select("sw", id); StartDrag(st.Switches[id].Node, w); return true; }
                    if (k == "der") { Select("der", id); StartDrag(st.Derailers[id].Node, w); return true; }
                    if (k == "te") { var nid = st.TrackEnds.First(t => t.Value.Id == id).Key; Select("te", nid); StartDrag(nid, w); return true; }
                    var n = PickNode(w);
                    if (n != null) { Select("node", n); StartDrag(n, w); return true; }
                    if (k == "blk") { Select("block", id); return true; }
                    if (k == "link") { pageBox.SelectedIndex = st.Pages.FindIndex(p => p.Id == id); return true; }
                    foreach (var (pn, _) in doc.Ports) if (st.Nodes.TryGetValue(pn, out var pnd) && PortRect(pnd).Contains(w)) { Select("port", pn); return true; }
                    for (int ci = 0; ci < V.Crossings.Count; ci++) { var c = V.Crossings[ci]; if (Math.Abs(w.X - c.X) < 6 && w.Y > c.Y0 - 16 && w.Y < c.Y1 + 16) { Select("crossing", null, ci); return true; } }
                    int li = PickLabel(w); if (li >= 0) { Select("label", null, li); StartDragObj(w, V.Labels[li].X, V.Labels[li].Y); return true; }
                    var ed = PickEdge(w);
                    if (ed != null && e?.ClickCount == 2) { SelectTrack(ed); return true; }
                    if (ed != null) { Select("edge", ed); return true; }
                    int pi = PickPlatform(w); if (pi >= 0) { Select("platform", null, pi); StartDragObj(w, V.Platforms[pi].X, V.Platforms[pi].Y); return true; }
                    selKind = null; ShowProps();
                    if (bandMode) { bandFrom = w; return true; }
                    return false;
                }
                case Tool.Track:
                {
                    doc.Snapshot();
                    var nid = NodeAt(w, true, shift);
                    if (chainFrom == null) { chainFrom = nid; chainSec = null; Rebuild(); status.Text = "Początek toru – wskaż kolejny punkt (PPM – koniec)"; return true; }
                    if (nid == chainFrom) return true;
                    var (ax, _) = doc.Pos(chainFrom); var (bx, _) = doc.Pos(nid);
                    int vdir = 0;
                    if (Math.Abs(ax - bx) < 0.5)
                    {
                        vdir = VerticalDir(chainFrom, nid);
                        if (vdir == 0) { doc.Undo(); Rebuild(); return true; }
                    }
                    // pierwszy tor ciągu: przedłużenie toru (koniec toru) albo odgałęzienie zwrotnicy dziedziczy jego odcinek
                    if (chainSec == null && chkSameSec.IsChecked == true)
                    {
                        var swAt = doc.Switches.Select(x => (JsonObject)x.Value).FirstOrDefault(x => StationDoc.Sv(x["node"]) == chainFrom);
                        var at = doc.EdgesAt(chainFrom);
                        if (swAt != null) chainSec = StationDoc.Sv(swAt["sec"]);
                        else if (at.Count == 1 && !doc.Signals.Any(x => StationDoc.Sv(x.Value["node"]) == chainFrom)) chainSec = StationDoc.Sv(at[0]["sec"]);
                    }
                    var endEdges = doc.EdgesAt(nid);
                    var eid = doc.AddEdge(chainFrom, nid, chkSameSec.IsChecked == true ? chainSec : null, Post);
                    // ciąg dochodzący do końca istniejącego toru (lub wychodzący z niego) – jeden odcinek, bez przypadkowej przerwy izolacyjnej
                    if (chkSameSec.IsChecked == true && endEdges.Count == 1 && !doc.Signals.Any(x => StationDoc.Sv(x.Value["node"]) == nid) && !doc.Switches.Any(x => StationDoc.Sv(x.Value["node"]) == nid))
                    {
                        var target = StationDoc.Sv(endEdges[0]["sec"]); var mine = StationDoc.Sv(doc.Edge(eid)["sec"]);
                        if (target != mine && !st.Blocks.Values.Any(b => b.SecsWE.Contains(target)))
                            foreach (var x in doc.EdgeObjs.Where(x => StationDoc.Sv(x["sec"]) == mine)) x["sec"] = target;
                    }
                    if (vdir != 0) doc.Edge(eid)["dir"] = vdir;
                    chainSec = StationDoc.Sv(doc.Edge(eid)["sec"]);
                    chainFrom = nid;
                    Changed($"Dodano tor (odcinek {Station.Lbl(chainSec)}) – wskaż kolejny punkt, PPM kończy");
                    Select("edge", eid);
                    return true;
                }
                case Tool.Section:
                {
                    var ed = PickEdge(w);
                    if (ed == null) { status.Text = "Wskaż tor"; return true; }
                    var eo = doc.Edge(ed);
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { secBox.Text = StationDoc.Sv(eo["sec"]); status.Text = "Pobrano odcinek " + secBox.Text; return true; }
                    if (string.IsNullOrWhiteSpace(secBox.Text)) secBox.Text = doc.NewSecId(Post, "it");
                    var sec = secBox.Text.Trim(); if (!sec.Contains(':') && !st.Blocks.Values.Any(b => b.SecsWE.Contains(sec))) sec = Post + ":" + sec;
                    if (StationDoc.Sv(eo["sec"]) == sec) return true;
                    doc.Snapshot(); eo["sec"] = sec;
                    Changed($"Tor {ed} → odcinek {sec}"); Select("edge", ed);
                    return true;
                }
                case Tool.Switch:
                {
                    var nid = PickNode(w);
                    if (nid == null || doc.EdgesAt(nid).Count != 3) { status.Text = "Wskaż węzeł, z którego wychodzą dokładnie 3 tory"; return true; }
                    if (doc.Switches.Any(s => StationDoc.Sv(s.Value["node"]) == nid)) { status.Text = "Tu już jest zwrotnica"; return true; }
                    var r = Ask("Nowa zwrotnica", ("Numer zwrotnicy", NextSwitchNum()));
                    if (r == null) return true;
                    doc.Snapshot(); var key = doc.AddSwitch(nid, r[0].Trim(), Post, chkSwSec.IsChecked == true);
                    Changed($"Zwrotnica {r[0]} dodana"); Select("sw", key);
                    return true;
                }
                case Tool.Signal:
                {
                    var r = Ask("Nowy sygnalizator", ("Nazwa (np. S1, Tm12, A; dla PK – np. PK1)", ""), ("Typ", "wyjazdowy|wyjazdowy bez funkcji manewrowej|wjazdowy|tm|to|sp|pk – punkt końcowy przebiegu"), ("Kierunek jazdy (dla PK – kierunek przebiegów, które w nim się kończą)", "→ na wschód (w prawo)|← na zachód (w lewo)"), ("Powtarzany semafor / sygnalizator SBL / szlak (dla to / sp)", ""), ("Przebiegi kończące się w PK (dla pk)", "pociągowe i manewrowe|pociągowe|manewrowe"));
                    if (r == null || r[0].Trim() == "") return true;
                    doc.Snapshot();
                    var nid = NodeAt(w, false);
                    if (nid == null) { doc.Undo(); status.Text = "Sygnalizator stawia się na torze"; return true; }
                    bool noMs = r[1] == "wyjazdowy bez funkcji manewrowej"; bool pk = r[1].StartsWith("pk");
                    var key = doc.AddSignal(nid, r[0].Trim(), noMs ? "wyjazdowy" : pk ? "pk" : r[1], r[2].StartsWith("→") ? 1 : -1, Post, null, pk ? null : r[3].Trim());
                    if (noMs) doc.Signals[key]["shunt"] = false;
                    if (pk) doc.Signals[key]["routes"] = r.Length > 4 ? (r[4].StartsWith("pociągowe i") ? "PM" : r[4].StartsWith("poc") ? "P" : "M") : "PM";
                    Changed($"Dodano {r[1]} {r[0]}"); Select("sig", key);
                    return true;
                }
                case Tool.TrackEnd:
                {
                    var nid = PickNode(w);
                    if (nid == null || doc.EdgesAt(nid).Count != 1) { status.Text = "Wskaż koniec toru (węzeł z jednym torem)"; return true; }
                    var r = Ask("Kozioł / koniec toru", ("Rodzaj", "kozioł oporowy – tylko symbol|znacznik końca przebiegu manewrowego (np. 12k)"), ("Oznaczenie znacznika (np. 12k)", ""), ("Opis", ""));
                    if (r == null) return true;
                    if (r[0].StartsWith("kozioł"))
                    {
                        doc.Snapshot(); doc.Bumpers[nid] = new JsonObject();
                        Changed("Dodano kozioł oporowy (symbol)"); Select("node", nid);
                        return true;
                    }
                    r = new[] { r[1], r[2] };
                    if (r[0].Trim() == "") { status.Text = "Podaj oznaczenie znacznika końca (np. 12k)"; return true; }
                    doc.Snapshot(); doc.AddTrackEnd(nid, r[0].Trim(), Post, r[1]);
                    Changed("Dodano kozioł " + r[0]); Select("te", nid);
                    return true;
                }
                case Tool.Derailer:
                {
                    var r = Ask("Wykolejnica", ("Oznaczenie (np. Wk5)", ""));
                    if (r == null || r[0].Trim() == "") return true;
                    doc.Snapshot();
                    var nid = NodeAt(w, false);
                    if (nid == null) { doc.Undo(); status.Text = "Wykolejnica leży na torze"; return true; }
                    doc.AddDerailer(nid, r[0].Trim(), Post);
                    Changed("Dodano wykolejnicę " + r[0]);
                    return true;
                }
                case Tool.Label:
                {
                    var r = Ask("Napis", ("Tekst (dla nastawni – skrót posterunku)", ""), ("Rodzaj", "start|middle|end|nastawnia"));
                    if (r == null || r[0] == "") return true;
                    var p = Snap(w);
                    doc.Snapshot(); doc.Labels.Add(new JsonObject { ["x"] = p.X, ["y"] = p.Y, ["text"] = r[0], ["anchor"] = r[1] });
                    Changed("Dodano napis");
                    return true;
                }
                case Tool.Platform:
                    rectFrom = Snap(w); return true;
                case Tool.Crossing:
                    crossFrom = Snap(w); return true;
                case Tool.Measure:
                {
                    var mn = PickNode(w);
                    measureFrom = mn != null ? new Point(st.Nodes[mn].X, st.Nodes[mn].Y) : Snap(w);
                    measureLine = null; measureText = null;
                    return true;
                }
                case Tool.Turnout:
                {
                    if (turnFrom == null)
                    {
                        doc.Snapshot();
                        var n0 = PickNode(w);
                        if (n0 == null || doc.EdgesAt(n0).Count != 2) { var e0 = PickEdge(w); if (e0 == null) { doc.Undo(); status.Text = "Wskaż tor, na którym ma być zwrotnica"; return true; } n0 = doc.SplitEdge(e0, Snap(w).X, w.Y, Post); }
                        turnFrom = n0; Rebuild();
                        status.Text = "Zwrotnica na " + n0 + " – kliknij drugi tor (łącznik) albo puste miejsce (odgałęzienie); kierunek ukosu w stronę kliknięcia. PPM – rezygnacja";
                        return true;
                    }
                    var a0 = turnFrom; turnFrom = null;
                    var (ax0, ay0) = doc.Pos(a0);
                    int sx = w.X >= ax0 ? 1 : -1; double kk = Math.Max(4, Grid);
                    var te = PickEdge(w);
                    doc.Snapshot();
                    if (te != null && Math.Abs(st.Edges[te].Pts[0].Y - st.Edges[te].Pts[^1].Y) < 0.5 && Math.Abs(st.Edges[te].Pts[0].Y - ay0) > 1)
                    {
                        var b0 = doc.AddCrossover(a0, te, sx, kk, Post);
                        var n1 = NextSwitchNum(); doc.AddSwitch(a0, n1, Post, true);
                        var n2 = NextSwitchNum(); doc.AddSwitch(b0, n2, Post, true);
                        doc.CrossoverSecs($"{Post}:{n2}");
                        if (panel.PlanView) doc.PlanSkew($"{Post}:{n1}", 3);
                        Changed($"Łącznik rozjazdowy: zwrotnice {n1} i {n2}");
                    }
                    else
                    {
                        var p = Snap(w);
                        var end = doc.AddBranch(a0, p.Y, p.X, sx, kk, Post);
                        var n1 = NextSwitchNum(); doc.AddSwitch(a0, n1, Post, true);
                        if (panel.PlanView) doc.PlanSkew($"{Post}:{n1}", 3);
                        Changed($"Odgałęzienie: zwrotnica {n1}, nowy tor do {end}");
                    }
                    return true;
                }
                case Tool.Link:
                {
                    var nid = PickNode(w);
                    if (nid == null || doc.EdgesAt(nid).Count != 1) { status.Text = "Wskaż koniec toru (węzeł z jednym torem) przy krawędzi obrazu"; return true; }
                    if (linkFrom == null)
                    {
                        linkFrom = nid;
                        // drugi koniec zwykle na sąsiednim obrazie – przełączenie (kierunek: w stronę, w którą biegnie tor)
                        var (lx, _) = doc.Pos(nid); var oth = doc.EdgesAt(nid)[0]; var on = StationDoc.Sv(oth["a"]) == nid ? StationDoc.Sv(oth["b"]) : StationDoc.Sv(oth["a"]);
                        int d = Math.Sign(lx - doc.Pos(on).X);
                        int cur = pageBox.SelectedIndex, tgt = cur + (d >= 0 ? 1 : -1);
                        if (cur < st.Pages.Count && tgt >= 0 && tgt < st.Pages.Count) pageBox.SelectedIndex = tgt;
                        status.Text = "Łącznik od " + nid + " – wskaż koniec toru na drugim obrazie (PPM – rezygnacja)";
                        return true;
                    }
                    var from2 = linkFrom; linkFrom = null;
                    var r2 = Ask("Łącznik obrazów", ("Długość toru między końcami [m]", "500"));
                    if (r2 == null) return true;
                    double ll = double.TryParse(r2[0].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var lv) ? lv : 500;
                    doc.Snapshot(); doc.AddLink(from2, nid, ll);
                    Changed($"Połączono tory {from2} ⇢ {nid} ({ll:0} m)");
                    return true;
                }
                case Tool.Szlak:
                {
                    var nid = PickNode(w);
                    if (nid == null) { status.Text = "Wskaż węzeł semafora wjazdowego (albo granicę / koniec toru szlakowego)"; return true; }
                    bool IsEntry(string n) => doc.Signals.Any(x => StationDoc.Sv(x.Value["node"]) == n && StationDoc.Sv(x.Value["type"]) == "wjazdowy");
                    // druga stacja szlaku wewnętrznego – semafor wjazdowy (także na innym obrazie); końce torów łączą się same
                    if (szlakPend is { } pend)
                    {
                        if (!IsEntry(nid)) { status.Text = "Wskaż semafor wjazdowy drugiej stacji LCS (przełącz obraz, jeśli trzeba; PPM – rezygnacja)"; return true; }
                        var tr2 = doc.TraceSzlak(nid);
                        string end2;
                        if (tr2 is { } t2 && t2.Internal && t2.Other == pend.From) end2 = null;          // tor już łączy obie stacje
                        else if (tr2 is { } t3 && !t3.Internal) end2 = t3.Other;
                        else { status.Text = "Od tego semafora tor szlakowy nie prowadzi do końca toru (zwrotnica na szlaku albo brak toru) – dorysuj tor od semafora na zewnątrz stacji"; return true; }
                        doc.Snapshot();
                        if (end2 != null && end2 != pend.End)
                        {
                            var rl = Ask("Długość szlaku", ("Długość toru szlakowego między stacjami [m] – końce torów zostaną połączone (łącznik obrazów)", "2000"));
                            if (rl == null) { doc.Undo(); szlakPend = null; return true; }
                            double ll = double.TryParse(rl[0].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var lv) ? lv : 2000;
                            doc.AddLink(pend.End, end2, ll);
                        }
                        szlakPend = null;
                        CreateBlock(pend.Ans, pend.From, nid, true);
                        return true;
                    }
                    // semafor ma już szlak (np. błędnie utworzony) – zastąpienie nowym
                    if (szlakFrom == null && (doc.Boundaries[nid] is JsonObject bo ? StationDoc.Sv(bo["szlak"]) : null) is string old && doc.Blocks.ContainsKey(old))
                    {
                        var rq = Ask("Szlak już istnieje", ($"Ten semafor należy do szlaku {old}. Co zrobić?", $"usuń szlak {old} i utwórz nowy|zostaw (pokaż szlak {old})"));
                        if (rq == null || !rq[0].StartsWith("usuń")) { Select("block", old); return true; }
                        doc.Snapshot(); doc.DeleteBlock(old);
                        foreach (var (pk, pv) in doc.Ports.ToList()) if (StationDoc.Sv(pv["entry"]) == nid) doc.Ports.Remove(pk);
                        Rebuild();
                    }
                    string other, from;
                    if (szlakFrom == null && doc.TraceSzlak(nid) is { } trc)
                    {
                        var (tOther, tInt, tSig) = trc;
                        // drugi koniec znaleziony po torze
                        from = nid; other = tOther;
                        status.Text = tInt ? $"Szlak do semafora wjazdowego {tSig} (szlak wewnętrzny LCS)" : "Szlak do końca toru – zewnętrzny albo wewnętrzny do stacji na innym obrazie";
                    }
                    else
                    {
                        if (szlakFrom == null) { szlakFrom = nid; status.Text = "Granica szlaku: " + nid + " – wskaż koniec toru szlakowego albo granicę drugiego posterunku LCS"; return true; }
                        other = nid; from = szlakFrom; szlakFrom = null;
                    }
                    bool otherIsEntry = IsEntry(other);
                    bool canLink = !otherIsEntry && doc.EdgesAt(other).Count == 1 && IsEntry(from);
                    var r = Ask("Nowy szlak", ("Oznaczenie (np. 1A, 2O)", ""), ("Posterunek sąsiedni (dla zewnętrznego)", otherIsEntry ? "" : "Łódź …"), ("Linia (nr)", ""), ("Tor szlakowy", "1"),
                        ("Rodzaj", otherIsEntry ? "wewnętrzny (oba końce w LCS)|zewnętrzny"
                                 : canLink ? "zewnętrzny (do posterunku spoza LCS)|wewnętrzny – złącze SZLAK WEWNĘTRZNY (połączysz je ze złączem drugiej stacji)|wewnętrzny – do innej stacji LCS (po OK wskaż jej semafor wjazdowy, także na innym obrazie)"
                                           : "zewnętrzny|wewnętrzny (oba końce w LCS)"),
                        ("Blokada", string.Join("|", BlockDef.Types.Select(t => t.Name))), ("Kierunek zasadniczy (dla jednokierunkowej – kierunek stały)", "WYJAZD z tej stacji|PRZYJAZD do tej stacji|neutralny"), ("Liczba odstępów (1 – jednoodstępowa)", "3"));
                    if (r == null || r[0].Trim() == "") return true;
                    if (r[4].Contains("złącze") && canLink)
                    {
                        doc.Snapshot(); doc.AddPort(other, from, r);
                        var free = doc.Ports.Where(x => x.Key != other).Select(x => (Node: x.Key, Id: StationDoc.Sv(x.Value["id"]), Post: StationDoc.Sv(x.Value["post"]))).ToList();
                        if (free.Count > 0)
                        {
                            var rp = Ask("Połączenie szlaku wewnętrznego", ("Połącz ze złączem", string.Join("|", free.Select(x => $"{x.Id} ({st.Posts.GetValueOrDefault(x.Post ?? "") ?? x.Post})")) + "|później"), ("Długość szlaku [m]", "2000"));
                            if (rp != null && rp[0] != "później")
                            {
                                var f = free[Array.IndexOf(free.Select(x => $"{x.Id} ({st.Posts.GetValueOrDefault(x.Post ?? "") ?? x.Post})").ToArray(), rp[0])];
                                double ll = double.TryParse(rp[1].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var lv) ? lv : 2000;
                                var bid = doc.PairPorts(f.Node, other, ll);
                                Changed($"Połączono złącza – szlak wewnętrzny {bid}"); Select("block", bid);
                                return true;
                            }
                        }
                        Changed($"Złącze SZLAK WEWNĘTRZNY {r[0]} – utwórz złącze na drugiej stacji i wybierz połączenie (albo zaznacz złącze i wybierz z listy)");
                        return true;
                    }
                    if (r[4].StartsWith("wewn") && canLink)
                    {
                        szlakPend = (from, other, r);
                        status.Text = $"Szlak {r[0]}: wskaż semafor wjazdowy drugiej stacji LCS – przełącz obraz, jeśli jest na innym (PPM – rezygnacja)";
                        return true;
                    }
                    CreateBlock(r, from, other, r[4].StartsWith("wewn"));
                    return true;
                }
            }
        }
        catch (InvalidOperationException) { throw; }
        return true;
    }

    /// <summary>
    /// Kierunek jazdy po torze pionowym od a do b: z toru, z którego się wjeżdża (jedyny tor poziomy w a – jazda przechodzi „na wprost”),
    /// albo z toru, na który się wyjeżdża (jedyny tor poziomy w b); gdy to niejednoznaczne – pytanie.
    /// </summary>
    int VerticalDir(string a, string b)
    {
        int Single(string n)
        {
            var hz = doc.EdgesAt(n).Select(e => doc.EdgeDir(e, n)).Where(d => d != 0).ToList();
            return hz.Count == 1 ? hz[0] : 0;
        }
        var da = Single(a); if (da != 0) return -da;
        var db = Single(b); if (db != 0) return db;
        var r = Ask("Tor pionowy – kierunek jazdy", ("Jazda po tym torze od punktu, w którym zaczęto rysować, prowadzi", "→ na wschód (w prawo)|← na zachód (w lewo)"));
        if (r == null) { status.Text = "Anulowano tor pionowy"; return 0; }
        return r[0].StartsWith("→") ? 1 : -1;
    }

    /// <summary>Kwadrat złącza szlaku wewnętrznego przy końcu toru (od strony zewnętrznej).</summary>
    Rect PortRect(Node n)
    {
        var e = n.Edges.Count > 0 ? st.Edges[n.Edges[0]] : null;
        int d = e == null ? 1 : -st.EdgeDir(e.Id, n.Id); if (d == 0) d = 1;
        return new Rect(d > 0 ? n.X + 4 : n.X - 20, n.Y - 8, 16, 16);
    }

    /// <summary>Utworzenie szlaku z odpowiedzi okna „Nowy szlak”.</summary>
    void CreateBlock(string[] r, string from, string other, bool internalBlock)
    {
        var (fx, _) = doc.Pos(from); var (ox, _) = doc.Pos(other); int outward = Math.Sign(ox - fx);
        int defDir = r[6].StartsWith("WYJAZD") ? outward : r[6].StartsWith("PRZYJAZD") ? -outward : 0;
        doc.Snapshot();
        var btype = BlockDef.Types.FirstOrDefault(t => t.Name == r[5]).Code ?? "sbl";
        int nod = r.Length > 7 && int.TryParse(r[7].Trim(), out var no) ? Math.Clamp(no, 1, 8) : (r[5].StartsWith("samo") ? 3 : 1);
        if (r.Length <= 7 && r[5].StartsWith("liniowa")) nod = 1;
        doc.AddBlock(r[0].Trim(), from, other, internalBlock, r[1], r[2], r[3], defDir, btype, btype == "tel" ? 1 : nod);
        Changed("Utworzono szlak " + r[0] + (internalBlock ? " (wewnętrzny LCS)" : "")); Select("block", r[0].Trim());
    }

    void StartGroupDrag(Point w) { dragging = true; dragSnap = false; dragStart = w; groupDrag = true; groupApplied = (0, 0); dragNode = null; }

    void ClearMulti() { multi.Clear(); multiLabels.Clear(); multiPlats.Clear(); if (selKind == "multi") selKind = null; }

    /// <summary>Zaznaczenie prostokątem: węzły, napisy i perony w obszarze.</summary>
    public void SelectRect(Rect r, bool add = false)
    {
        if (!add) ClearMulti();
        foreach (var n in st.Nodes.Values) if (r.Contains(new Point(n.X, n.Y)) && OnPage(n.X)) multi.Add(n.Id);
        for (int i = 0; i < V.Labels.Count; i++) if (r.Contains(new Point(V.Labels[i].X, V.Labels[i].Y))) multiLabels.Add(i);
        for (int i = 0; i < V.Platforms.Count; i++) { var p = V.Platforms[i]; if (r.Contains(new Rect(p.X, p.Y, p.W, p.H))) multiPlats.Add(i); }
        selKind = multi.Count + multiLabels.Count + multiPlats.Count > 0 ? "multi" : null; selId = null;
        ShowProps();
        status.Text = $"Zaznaczono: węzły {multi.Count}, napisy {multiLabels.Count}, perony {multiPlats.Count} – przeciągnij węzeł grupy, Del usuwa, panel z prawej: wyrównanie";
    }

    /// <summary>Przesunięcie zaznaczonej grupy (z torami, napisami i peronami).</summary>
    public void GroupMove(double dx, double dy)
    {
        if (dx == 0 && dy == 0) return;
        doc.MoveGroup(multi, dx, dy);
        foreach (var i in multiLabels) if (i < doc.Labels.Count) { doc.Labels[i]["x"] = StationDoc.D(doc.Labels[i]["x"]) + dx; doc.Labels[i]["y"] = StationDoc.D(doc.Labels[i]["y"]) + dy; }
        foreach (var i in multiPlats) if (i < doc.Platforms.Count) { doc.Platforms[i]["x"] = StationDoc.D(doc.Platforms[i]["x"]) + dx; doc.Platforms[i]["y"] = StationDoc.D(doc.Platforms[i]["y"]) + dy; }
    }

    /// <summary>Zaznaczenie całego toru poziomego (dwuklik) – do przesuwania w pionie strzałkami lub myszą.</summary>
    public void SelectTrack(string eid)
    {
        ClearMulti();
        foreach (var n in doc.TrackLine(eid)) multi.Add(n);
        selKind = multi.Count > 0 ? "multi" : null; selId = null; ShowProps();
        status.Text = $"Zaznaczono tor ({multi.Count} węzłów) – przeciągnij albo ↑↓ (krok siatki); Shift+dwuklik dodaje kolejny tor; „Rozstaw tory” w panelu";
    }

    /// <summary>Rozstaw zaznaczonych torów w pionie co „spacing”.</summary>
    public void Respace(double spacing)
    {
        doc.Snapshot();
        var labs = multiLabels.Where(i => i < doc.Labels.Count).Select(i => (JsonObject)doc.Labels[i]).ToList();
        var pls = multiPlats.Where(i => i < doc.Platforms.Count).Select(i => (JsonObject)doc.Platforms[i]).ToList();
        var k = doc.RespaceY(multi.ToList(), spacing, labs, pls);
        Changed($"Rozstawiono {k} torów co {spacing:0} ({spacing / Grid:0.#} kratek)"); selKind = "multi"; ShowProps();
    }

    /// <summary>Wyrównanie zaznaczonych węzłów do wspólnej wysokości (prostowanie toru).</summary>
    public void AlignY()
    {
        var ns = multi.Where(doc.Nodes.ContainsKey).ToList(); if (ns.Count < 2) return;
        var y = ns.Select(n => doc.Pos(n).Y).GroupBy(v => Math.Round(v)).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First().Key;
        doc.Snapshot();
        foreach (var n in ns) { var (x, _) = doc.Pos(n); doc.MoveNode(n, x, y); }
        Changed($"Wyrównano {ns.Count} węzłów do y={y:0}");
    }

    /// <summary>Rozmieszczenie zaznaczonych węzłów w równych odstępach w poziomie (między skrajnymi).</summary>
    public void DistributeX()
    {
        var ns = multi.Where(doc.Nodes.ContainsKey).OrderBy(n => doc.Pos(n).X).ToList(); if (ns.Count < 3) return;
        double x0 = doc.Pos(ns[0]).X, x1 = doc.Pos(ns[^1]).X;
        doc.Snapshot();
        for (int i = 1; i < ns.Count - 1; i++) { var (_, y) = doc.Pos(ns[i]); doc.MoveNode(ns[i], Math.Round(x0 + (x1 - x0) * i / (ns.Count - 1)), y); }
        Changed($"Rozmieszczono {ns.Count} węzłów równomiernie");
    }

    /// <summary>Szukanie obiektu po nazwie – zaznaczenie i wyśrodkowanie obrazu.</summary>
    void Find(string q)
    {
        q = (q ?? "").Trim(); if (q == "") return;
        bool M(string s2) => s2 != null && (string.Equals(Station.Lbl(s2), q, StringComparison.OrdinalIgnoreCase) || string.Equals(s2, q, StringComparison.OrdinalIgnoreCase));
        var hits = new List<(string Kind, string Id, double X, double Y)>();
        foreach (var x in st.Signals.Values) if (M(x.Name) || M(x.Label)) hits.Add(("sig", x.Name, st.Nodes[x.Node].X, st.Nodes[x.Node].Y));
        foreach (var x in st.Switches.Values) if (M(x.Num) || M(x.Label)) hits.Add(("sw", x.Num, st.Nodes[x.Node].X, st.Nodes[x.Node].Y));
        foreach (var x in st.Sections.Values) if (M(x.Id)) { var e = st.Edges[x.Edges[0]]; hits.Add(("edge", e.Id, (e.Pts[0].X + e.Pts[^1].X) / 2, (e.Pts[0].Y + e.Pts[^1].Y) / 2)); }
        foreach (var x in st.Blocks.Values) if (M(x.Id)) { var n = st.Nodes[x.Ends[0].Boundary]; hits.Add(("block", x.Id, n.X, n.Y)); }
        foreach (var x in st.Edges.Values) if (x.Track != "" && string.Equals("tor " + x.Track, q, StringComparison.OrdinalIgnoreCase)) hits.Add(("edge", x.Id, (x.Pts[0].X + x.Pts[^1].X) / 2, x.Pts[0].Y));
        foreach (var x in st.Nodes.Values) if (M(x.Id)) hits.Add(("node", x.Id, x.X, x.Y));
        if (hits.Count == 0) { status.Text = $"Nie znaleziono „{q}”"; return; }
        var h = hits[findIdx++ % hits.Count];
        var pi = st.Pages.FindIndex(p => h.X >= p.X0 && h.X < p.X1);
        if (pi >= 0 && pi != pageBox.SelectedIndex && pageBox.SelectedIndex < st.Pages.Count) pageBox.SelectedIndex = pi;
        ClearMulti(); Select(h.Kind, h.Id);
        panel.CenterOn(h.X, h.Y);
        status.Text = $"Znaleziono {hits.Count}: {h.Kind} {Station.Lbl(h.Id)} (Enter – następny)";
    }

    void StartDrag(string nid, Point w) { dragging = true; dragSnap = false; dragStart = w; dragOrig = doc.Pos(nid); dragNode = nid; }
    void StartDragObj(Point w, double x, double y) { dragging = true; dragSnap = false; dragStart = w; dragOrig = (x, y); dragNode = null; }
    string dragNode;

    public void Move(Point w, MouseEventArgs e) { mouse = w; Safe(() => { if (dispMode && !DecoTool(tool)) DMove(w, e); else MoveCore(w, e); }); if (dispMode && (rectFrom != null || crossFrom != null)) panel.InvalidateVisual(); }
    void MoveCore(Point w, MouseEventArgs e)
    {
        if (measureFrom != null && tool == Tool.Measure && (e == null || e.LeftButton == MouseButtonState.Pressed)) { measureLine = (measureFrom.Value, Snap(w)); return; }
        if (!dragging || (e != null && e.LeftButton != MouseButtonState.Pressed)) return;
        if (!dragSnap && (w - dragStart).Length * panel.Scale < 4) return;
        if (!dragSnap) { doc.Snapshot(); dragSnap = true; }
        if (groupDrag)
        {
            double g = Grid, gy = GridY, dx = Math.Round((w.X - dragStart.X) / g) * g, dy = Math.Round((w.Y - dragStart.Y) / gy) * gy;
            // przesuwanie tylko w jednej osi (Ctrl – dowolnie)
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { if (Math.Abs(dx) >= Math.Abs(dy)) dy = 0; else dx = 0; }
            GroupMove(dx - groupApplied.X, 0); GroupMove(0, dy - groupApplied.Y); groupApplied = (dx, dy);
            dirty = true; Rebuild(); return;
        }
        var p = Snap(new Point(dragOrig.X + w.X - dragStart.X, dragOrig.Y + w.Y - dragStart.Y));
        if (dragNode != null) doc.MoveNode(dragNode, p.X, p.Y);
        else if (selKind == "label") { doc.Labels[selIdx]["x"] = p.X; doc.Labels[selIdx]["y"] = p.Y; }
        else if (selKind == "platform") { doc.Platforms[selIdx]["x"] = p.X; doc.Platforms[selIdx]["y"] = p.Y; }
        dirty = true; Rebuild();
    }

    public void Up(Point w, MouseButtonEventArgs e) => Safe(() => { if (dispMode && !DecoTool(tool)) DUp(w, e); else UpCore(w, e); });
    void UpCore(Point w, MouseButtonEventArgs e)
    {
        if (bandFrom is Point bf) { bandFrom = null; SelectRect(new Rect(bf, w), true); return; }
        if (tool == Tool.Measure && measureFrom is Point mf)
        {
            var na = PickNode(mf); var nb = PickNode(w);
            var b2 = nb != null ? new Point(st.Nodes[nb].X, st.Nodes[nb].Y) : Snap(w);
            measureLine = (mf, b2); measureFrom = null;
            double dx = b2.X - mf.X, dy = b2.Y - mf.Y;
            var path = na != null && nb != null && na != nb ? doc.TrackDistance(na, nb) : null;
            measureText = (panel.PlanView ? $"na planie {doc.PlanLen(mf.X, mf.Y, b2.X, b2.Y):0.#} m (wzdłuż {Math.Abs(dx) / doc.Ux:0.#} m, w poprzek {Math.Abs(dy) / doc.Uy:0.##} m)" : $"Δx={dx:0} Δy={dy:0} na obrazie: {Math.Sqrt(dx * dx + dy * dy):0}") + (path != null ? $"; droga po torach: {path:0} m" : na != null && nb != null ? "; brak połączenia torami" : " (zacznij i skończ na węźle, by zmierzyć drogę po torach)");
            status.Text = "Miarka: " + measureText;
            return;
        }
        if (dragging)
        {
            dragging = false; bool g = groupDrag; groupDrag = false; dragNode = null;
            if (dragSnap) { Changed(g ? $"Przesunięto grupę o ({groupApplied.X:0}, {groupApplied.Y:0})" : "Przesunięto"); if (g) { selKind = "multi"; ShowProps(); } }
            return;
        }
        if (tool == Tool.Crossing && crossFrom is Point cf)
        {
            var b2 = Snap(w); crossFrom = null;
            if (Math.Abs(b2.Y - cf.Y) < 20) { status.Text = "Przeciągnij w pionie przez tory"; return; }
            var r = Ask("Przejazd kolejowo-drogowy", ("Kilometr / opis (np. 10.173)", ""), ("Oznaczenie końców", "Y|T"));
            if (r == null) return;
            doc.Snapshot();
            doc.Crossings.Add(new JsonObject { ["name"] = r[0], ["kind"] = r[1], ["x"] = cf.X, ["y0"] = Math.Min(cf.Y, b2.Y), ["y1"] = Math.Max(cf.Y, b2.Y) });
            Changed("Dodano przejazd " + r[0]);
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && !FuzzMode) SetTool(Tool.Select);
            return;
        }
        if (tool == Tool.Platform && rectFrom is Point a)
        {
            var b = Snap(w); rectFrom = null;
            var rc = new Rect(a, b);
            if (rc.Width < 10 || rc.Height < 10) return;
            var r = Ask("Peron", ("Nazwa", "Peron "));
            if (r == null) return;
            doc.Snapshot(); doc.Platforms.Add(new JsonObject { ["name"] = r[0], ["x"] = rc.X, ["y"] = rc.Y, ["w"] = rc.Width, ["h"] = rc.Height });
            Changed("Dodano peron");
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && !FuzzMode) SetTool(Tool.Select);
        }
    }

    public void Right(Point w)
    {
        if (trackOp != null) { trackOp = null; panel.Cursor = null; status.Text = "Zakończono"; return; }
        if (bgCal > 0 || bgOp != null) { bgCal = 0; bgOp = null; bgPts.Clear(); status.Text = "Operacja na podkładzie przerwana"; panel.InvalidateVisual(); return; }
        if (dispMode && DecoTool(tool)) { rectFrom = null; crossFrom = null; SetTool(Tool.Select); panel.InvalidateVisual(); return; }
        if (dispMode) { Safe(() => DRight(w)); return; }
        if (chainFrom != null || szlakFrom != null || rectFrom != null || linkFrom != null || turnFrom != null || crossFrom != null || szlakPend != null) { szlakPend = null; chainFrom = null; chainSec = null; szlakFrom = null; rectFrom = null; linkFrom = null; turnFrom = null; crossFrom = null; status.Text = "Zakończono"; return; }
        if (tool != Tool.Select) SetTool(Tool.Select);
    }

    public string Hover(Point w)
    {
        if (st == null) return "";
        try { return dispMode ? DHover(w) : HoverCore(w); } catch { return ""; }
    }
    string HoverCore(Point w)
    {
        var n = PickNode(w);
        var g = Snap(w);
        string pos = $"x={g.X:0} y={g.Y:0} ({g.X / doc.Ux:0} m, {g.Y / doc.Uy:0.#} m w poprzek)";
        if (n != null) return $"{pos}   węzeł {n} ({st.Nodes[n].Edges.Count} tory){(st.Nodes[n].Switch != null ? ", zwrotnica " + st.Switches[st.Nodes[n].Switch].Label : "")}";
        var e = PickEdge(w);
        if (e != null) { var ed = st.Edges[e]; return $"{pos}   tor {e}: odcinek {ed.Sec} – dł. odcinka {st.Sections[ed.Sec].Edges.Sum(x => st.Edges[x].Len):0} m (ten fragment {ed.Len:0} m){(ed.Track != "" ? ", tor nr " + ed.Track : "")}"; }
        return pos;
    }

    /// <summary>Tło edytora: siatka (oczko = krok przyciągania; gęstsza siatka rozrzedza się przy oddaleniu), granice obrazów.</summary>
    public void DrawBack(DrawingContext dc, double scale)
    {
        if (panel.PlanView) DrawPlanBackground(dc, scale);
        if (st == null || chkGrid.IsChecked != true) return;
        var v = panel.ViewWorld; double g = Grid, gy = GridY;
        while (g * scale < 7) g *= 5;
        while (gy * scale < 7) gy *= 5;
        double major = g * 5, majorY = gy * 5;
        var minor = new Pen(new SolidColorBrush(panel.PlanView ? Color.FromRgb(0xee, 0xee, 0xf2) : Color.FromRgb(0x16, 0x16, 0x1c)), 1 / scale);
        var maj = new Pen(new SolidColorBrush(panel.PlanView ? Color.FromRgb(0xdc, 0xdc, 0xe4) : Color.FromRgb(0x26, 0x26, 0x30)), 1 / scale);
        var geoMin = new StreamGeometry(); var geoMaj = new StreamGeometry();
        using (var cmin = geoMin.Open())
        using (var cmaj = geoMaj.Open())
        {
            for (double x = Math.Floor(v.Left / g) * g; x <= v.Right; x += g)
            {
                var c = Math.Abs(x / major - Math.Round(x / major)) < 1e-6 ? cmaj : cmin;
                c.BeginFigure(new Point(x, v.Top), false, false); c.LineTo(new Point(x, v.Bottom), true, false);
            }
            for (double y = Math.Floor(v.Top / gy) * gy; y <= v.Bottom; y += gy)
            {
                var c = Math.Abs(y / majorY - Math.Round(y / majorY)) < 1e-6 ? cmaj : cmin;
                c.BeginFigure(new Point(v.Left, y), false, false); c.LineTo(new Point(v.Right, y), true, false);
            }
        }
        geoMin.Freeze(); geoMaj.Freeze();
        dc.DrawGeometry(null, minor, geoMin); dc.DrawGeometry(null, maj, geoMaj);
        var pagePen = new Pen(new SolidColorBrush(Color.FromRgb(0x40, 0x30, 0x10)), 2 / scale) { DashStyle = DashStyles.Dash };
        if (!panel.PlanView) foreach (var p in st.Pages) { dc.DrawLine(pagePen, new Point(p.X0, v.Top), new Point(p.X0, v.Bottom)); dc.DrawLine(pagePen, new Point(p.X1, v.Top), new Point(p.X1, v.Bottom)); }
    }

    /// <summary>Linijka: miara w kratkach przy rysowaniu toru, przesuwaniu i na brzegach widoku.</summary>
    void DrawRulers(DrawingContext dc, double scale, double fs)
    {
        double g = Grid; var v = panel.ViewWorld;
        var rb = new SolidColorBrush(Color.FromArgb(0xd0, 0x10, 0x14, 0x1c)); var tb = new SolidColorBrush(Color.FromRgb(0x90, 0xa8, 0xc0));
        var yb = new SolidColorBrush(Color.FromRgb(0xff, 0xe0, 0x40));
        void Tag(string t, Point at, Brush b)
        {
            var ft = Ft(t, fs * 1.05, b); var r = new Rect(at.X, at.Y, ft.Width + 6 / scale, ft.Height + 2 / scale);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0xe0, 0, 0, 0)), new Pen(b, 1 / scale), r); dc.DrawText(ft, new Point(at.X + 3 / scale, at.Y + 1 / scale));
        }
        // brzegi widoku: numer kratki co linię główną; na planie – metry w skali planu (wzdłuż / w poprzek)
        bool pm = panel.PlanView;
        static double Nice(double m) { double p = Math.Pow(10, Math.Floor(Math.Log10(Math.Max(1e-9, m)))); foreach (var k in new[] { 1, 2, 5, 10 }) if (k * p >= m) return k * p; return 10 * p; }
        double stepX = g, stepY = GridY; while (stepX * scale < 7) stepX *= 5; while (stepY * scale < 7) stepY *= 5;
        if (pm) { stepX = Nice(7 / scale / doc.Ux) * doc.Ux; stepY = Nice(7 / scale / doc.Uy) * doc.Uy; }
        double majorX = stepX * (pm ? 10 : 5), majorY = stepY * (pm ? 10 : 5);
        string LX(double x) => pm ? $"{x / doc.Ux:0} m" : $"{x / g:0}";
        string LY(double y) => pm ? $"{y / doc.Uy:0.#}" : $"{y / GridY:0}";
        double bh = 16 / scale;
        dc.DrawRectangle(rb, null, new Rect(v.Left, v.Top, v.Width, bh)); dc.DrawRectangle(rb, null, new Rect(v.Left, v.Top, bh * 2.2, v.Height));
        var tp = new Pen(tb, 1 / scale);
        for (double x = Math.Floor(v.Left / stepX) * stepX; x <= v.Right; x += stepX)
        {
            bool mj = Math.Abs(x / majorX - Math.Round(x / majorX)) < 1e-6;
            dc.DrawLine(tp, new Point(x, v.Top + bh), new Point(x, v.Top + bh - (mj ? 7 : 3) / scale));
            if (mj) dc.DrawText(Ft(LX(x), fs * 0.85, tb), new Point(x + 2 / scale, v.Top));
        }
        for (double y = Math.Floor(v.Top / stepY) * stepY; y <= v.Bottom; y += stepY)
        {
            bool mj = Math.Abs(y / majorY - Math.Round(y / majorY)) < 1e-6;
            dc.DrawLine(tp, new Point(v.Left + bh * 2.2, y), new Point(v.Left + bh * 2.2 - (mj ? 7 : 3) / scale, y));
            if (mj && y > v.Top + bh) dc.DrawText(Ft(LY(y), fs * 0.85, tb), new Point(v.Left + 1 / scale, y - fs * 0.6));
        }
        // rysowany tor: Δx × Δy w kratkach i długość
        if (tool == Tool.Track && chainFrom != null && doc.Nodes.ContainsKey(chainFrom))
        {
            var a = doc.Pos(chainFrom); var m = Snap(mouse);
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) m.Y = a.Y; else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) m = Snap45(a, m);
            double dx = m.X - a.X, dy = m.Y - a.Y;
            Tag(panel.PlanView ? $"{doc.PlanLen(a.X, a.Y, m.X, m.Y):0} m  (wzdłuż {Math.Abs(dx) / doc.Ux:0} m, w poprzek {Math.Abs(dy) / doc.Uy:0.##} m; 1:{doc.ScaleAlong:0} / 1:{doc.ScaleAcross:0})"
                : $"{Math.Abs(dx) / g:0.#} × {Math.Abs(dy) / g:0.#} kr.  ({Math.Sqrt(dx * dx + dy * dy) / g:0.#} kr. = {Math.Sqrt(dx * dx + dy * dy):0} px)", new Point(m.X + 10 / scale, m.Y - 22 / scale), yb);
        }
        // przesuwanie: o ile kratek
        if (dragging && dragSnap)
        {
            (double X, double Y) d = groupDrag ? groupApplied : dragNode != null && doc.Nodes.ContainsKey(dragNode) ? (doc.Pos(dragNode).X - dragOrig.X, doc.Pos(dragNode).Y - dragOrig.Y) : (0, 0);
            Tag($"Δ {d.X / g:+0.#;-0.#;0} kr. poziomo, {d.Y / g:+0.#;-0.#;0} kr. pionowo", new Point(mouse.X + 12 / scale, mouse.Y + 8 / scale), yb);
        }
        // rozstaw zaznaczonych torów
        if (selKind == "multi" && !dragging)
        {
            var ys = multi.Where(st.Nodes.ContainsKey).Select(n => st.Nodes[n]).Where(n => n.Edges.Any(x => { var ed = st.Edges[x]; return ed.Pts.All(p => Math.Abs(p.Y - n.Y) < 0.5); })).Select(n => Math.Round(n.Y)).Distinct().OrderBy(y => y).ToList();
            { var mg = new List<double>(); foreach (var y in ys) if (mg.Count == 0 || y - mg[^1] >= 17) mg.Add(y); ys = mg; }
            if (ys.Count >= 2)
            {
                double lx = Math.Max(v.Left + 60 / scale, Math.Min(multi.Where(st.Nodes.ContainsKey).Max(n => st.Nodes[n].X) + 30 / scale, v.Right - 70 / scale));
                var dp = new Pen(yb, 1.2 / scale);
                for (int i = 0; i < ys.Count - 1; i++)
                {
                    dc.DrawLine(dp, new Point(lx, ys[i]), new Point(lx, ys[i + 1]));
                    dc.DrawLine(dp, new Point(lx - 4 / scale, ys[i]), new Point(lx + 4 / scale, ys[i])); dc.DrawLine(dp, new Point(lx - 4 / scale, ys[i + 1]), new Point(lx + 4 / scale, ys[i + 1]));
                    var ft = Ft($"{(ys[i + 1] - ys[i]) / g:0.#} kr.", fs, yb); dc.DrawText(ft, new Point(lx + 6 / scale, (ys[i] + ys[i + 1]) / 2 - ft.Height / 2));
                }
            }
        }
    }

    FormattedText Ft(string t, double size, Brush b) => new(t, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Consolas"), size, b, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    public void Draw(DrawingContext dc, double scale)
    {
        if (st == null) return;
        try { if (dispMode) DDraw(dc, scale); else { DrawOverlay(dc, scale); DrawProblems(dc, scale); } } catch (Exception ex) { App.LogError(ex, "edytor – rysowanie"); }
    }

    void DrawOverlay(DrawingContext dc, double scale)
    {
        if (selKind != null && !Exists()) { selKind = null; selId = null; }
        var az = new SolidColorBrush(Color.FromRgb(0x00, 0xa0, 0xff));
        var pen = new Pen(az, 2 / scale);
        double r = 4 / scale;
        double fs = Math.Clamp(9 / scale, 5, 30);
        if (chkSecs.IsChecked == true)
        {
            var ob = new SolidColorBrush(Color.FromRgb(0xff, 0x9c, 0x30));
            foreach (var sd in st.Sections.Values)
            {
                var e = sd.Edges.Select(x => st.Edges[x]).OrderByDescending(x => x.Len).First();
                var m = e.Pts[e.Pts.Count / 2 - (e.Pts.Count % 2 == 0 ? 1 : 0)]; var m2 = e.Pts[e.Pts.Count / 2];
                double mx = (m.X + m2.X) / 2, my = (m.Y + m2.Y) / 2; if (!OnPage(mx)) continue;
                var ft = Ft(Station.Lbl(sd.Id), fs, ob); dc.DrawText(ft, new Point(mx - ft.Width / 2, my - ft.Height - 2 / scale));
            }
        }
        if (chkLens.IsChecked == true)
        {
            var cb = new SolidColorBrush(Color.FromRgb(0x60, 0xc8, 0xff));
            foreach (var e in st.Edges.Values)
            {
                double mx = (e.Pts[0].X + e.Pts[^1].X) / 2, my = (e.Pts[0].Y + e.Pts[^1].Y) / 2; if (!OnPage(mx)) continue;
                var ft = Ft($"{e.Len:0}", fs * 0.9, cb); dc.DrawText(ft, new Point(mx - ft.Width / 2, my + 3 / scale));
            }
        }
        if (chkSpeed.IsChecked == true)
        {
            var gb = new SolidColorBrush(Color.FromRgb(0x7c, 0xe0, 0x7c)); var ob = new SolidColorBrush(Color.FromRgb(0xff, 0x70, 0x70));
            foreach (var e in st.Edges.Values)
            {
                if (e.VMax <= 0 && e.SwLimit <= 0) continue;
                double mx = (e.Pts[0].X + e.Pts[^1].X) / 2, my = (e.Pts[0].Y + e.Pts[^1].Y) / 2; if (!OnPage(mx)) continue;
                var txt = e.SwLimit > 0 && (e.VMax <= 0 || e.SwLimit < e.VMax) ? $"⑂{e.SwLimit:0}" : $"{e.VMax:0}";
                var ft = Ft(txt, fs, e.SwLimit > 0 && (e.VMax <= 0 || e.SwLimit < e.VMax) ? ob : gb); dc.DrawText(ft, new Point(mx - ft.Width / 2, my - ft.Height - 2 / scale));
            }
        }
        if (multi.Count + multiLabels.Count + multiPlats.Count > 0)
        {
            var mp = new Pen(new SolidColorBrush(Color.FromRgb(0xff, 0xd0, 0x00)), 2 / scale);
            foreach (var n in multi) if (st.Nodes.TryGetValue(n, out var nd)) dc.DrawRectangle(null, mp, new Rect(nd.X - 6, nd.Y - 6, 12, 12));
            foreach (var i in multiLabels) if (i < V.Labels.Count) dc.DrawRectangle(null, mp, new Rect(V.Labels[i].X - 4, V.Labels[i].Y - 8, 16, 16));
            foreach (var i in multiPlats) if (i < V.Platforms.Count) { var p = V.Platforms[i]; dc.DrawRectangle(null, mp, new Rect(p.X - 3, p.Y - 3, p.W + 6, p.H + 6)); }
        }
        if (bandFrom is Point bfr) dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x30, 0xff, 0xd0, 0x00)), new Pen(Brushes.Gold, 1 / scale) { DashStyle = DashStyles.Dash }, new Rect(bfr, mouse));
        if (measureLine is (Point ma, Point mb))
        {
            var mpn = new Pen(Brushes.Magenta, 2 / scale) { DashStyle = DashStyles.Dash };
            dc.DrawLine(mpn, ma, mb); dc.DrawEllipse(null, mpn, ma, 4 / scale, 4 / scale); dc.DrawEllipse(null, mpn, mb, 4 / scale, 4 / scale);
            if (measureText != null) { var ft = Ft(measureText, fs, Brushes.Magenta); dc.DrawText(ft, new Point((ma.X + mb.X) / 2 - ft.Width / 2, (ma.Y + mb.Y) / 2 - ft.Height - 4 / scale)); }
        }
        DrawRulers(dc, scale, fs);
        if (chkCross.IsChecked == true && tool != Tool.Select)
        {
            var c = Snap(mouse); var v = panel.ViewWorld;
            var cp = new Pen(new SolidColorBrush(Color.FromArgb(0x60, 0x00, 0xa0, 0xff)), 1 / scale);
            dc.DrawLine(cp, new Point(v.Left, c.Y), new Point(v.Right, c.Y)); dc.DrawLine(cp, new Point(c.X, v.Top), new Point(c.X, v.Bottom));
            var ft = Ft($"{c.X:0}, {c.Y:0}", fs, new SolidColorBrush(Color.FromRgb(0x00, 0xa0, 0xff))); dc.DrawText(ft, new Point(c.X + 6 / scale, c.Y + 4 / scale));
        }
        // węzły – drobne punkty (ułatwiają trafienie)
        if (chkNodes.IsChecked == true)
            foreach (var n in st.Nodes.Values) if (OnPage(n.X)) dc.DrawEllipse(panel.PlanView ? new SolidColorBrush(Color.FromArgb(0x70, 0x30, 0x60, 0xc0)) : new SolidColorBrush(Color.FromArgb(0x90, 0x60, 0x60, 0x60)), null, new Point(n.X, n.Y), panel.PlanView ? Math.Min(r * 0.4, 1.8) : r * 0.7, panel.PlanView ? Math.Min(r * 0.4, 1.8) : r * 0.7);
        switch (selKind)
        {
            case "node": case "te": if (doc.Nodes.ContainsKey(selId)) { var p = doc.Pos(selId); dc.DrawEllipse(null, pen, new Point(p.X, p.Y), 7, 7); break; } break;
            case "sig": if (st.Signals.TryGetValue(selId, out var sgd)) { var n = st.Nodes[sgd.Node]; dc.DrawRectangle(null, pen, new Rect(n.X - 16, n.Y - 10, 32, 20)); break; } break;
            case "sw": if (st.Switches.TryGetValue(selId, out var swd)) { var n = st.Nodes[swd.Node]; dc.DrawEllipse(null, pen, new Point(n.X, n.Y), 10, 10); break; } break;
            case "der": if (st.Derailers.TryGetValue(selId, out var dd)) { var n = st.Nodes[dd.Node]; dc.DrawRectangle(null, pen, new Rect(n.X - 6, n.Y - 12, 12, 24)); break; } break;
            case "edge": if (st.Edges.TryGetValue(selId, out var e)) for (int i = 0; i < e.Pts.Count - 1; i++) dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(0x90, 0x00, 0xa0, 0xff)), 10), new Point(e.Pts[i].X, e.Pts[i].Y), new Point(e.Pts[i + 1].X, e.Pts[i + 1].Y)); break;
            case "label": if (selIdx < V.Labels.Count) { var l = V.Labels[selIdx]; double wd = l.Text.Length * 6.2, x0 = l.Anchor == "middle" ? l.X - wd / 2 : l.Anchor == "end" ? l.X - wd : l.X; dc.DrawRectangle(null, pen, new Rect(x0 - 3, l.Y - 8, wd + 6, 16)); } break;
            case "platform": if (selIdx < V.Platforms.Count) { var p = V.Platforms[selIdx]; dc.DrawRectangle(null, pen, new Rect(p.X - 3, p.Y - 3, p.W + 6, p.H + 6)); } break;
            case "block":
                if (st.Blocks.TryGetValue(selId, out var b))
                    foreach (var ed in st.Edges.Values.Where(x => b.SecsWE.Contains(x.Sec)))
                        for (int i = 0; i < ed.Pts.Count - 1; i++) dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(0x70, 0xff, 0xf0, 0x00)), 12), new Point(ed.Pts[i].X, ed.Pts[i].Y), new Point(ed.Pts[i + 1].X, ed.Pts[i + 1].Y));
                break;
        }
        if (tool == Tool.Section && !string.IsNullOrWhiteSpace(secBox.Text))
        {
            var sc = secBox.Text.Trim(); var full = sc.Contains(':') ? sc : Post + ":" + sc;
            foreach (var ed in st.Edges.Values.Where(x => x.Sec == sc || x.Sec == full))
                for (int i = 0; i < ed.Pts.Count - 1; i++) dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(0x80, 0xff, 0x8c, 0x00)), 12), new Point(ed.Pts[i].X, ed.Pts[i].Y), new Point(ed.Pts[i + 1].X, ed.Pts[i + 1].Y));
        }
        if (routeEdges != null && selKind == "sig")
            foreach (var eid in routeEdges) if (st.Edges.TryGetValue(eid, out var ed))
                for (int i = 0; i < ed.Pts.Count - 1; i++) dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(0xa0, 0x00, 0xe6, 0x00)), 9), new Point(ed.Pts[i].X, ed.Pts[i].Y), new Point(ed.Pts[i + 1].X, ed.Pts[i + 1].Y));
        if (tool == Tool.Track && chainFrom != null && doc.Nodes.ContainsKey(chainFrom))
        {
            var a = doc.Pos(chainFrom); var m = Snap(mouse);
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) m.Y = a.Y;
            else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) m = Snap45(a, m);
            dc.DrawLine(new Pen(az, 3) { DashStyle = DashStyles.Dash }, new Point(a.X, a.Y), m);
        }
        foreach (var le in st.Edges.Values.Where(x => x.Link))
        {
            var a = st.Nodes[le.A]; var b = st.Nodes[le.B];
            if (OnPage(a.X) && OnPage(b.X)) dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(0x00, 0xa0, 0xff)), 1.5 / scale) { DashStyle = DashStyles.Dash }, new Point(a.X, a.Y), new Point(b.X, b.Y));
        }
        foreach (var (pn, pv) in doc.Ports)
        {
            if (!st.Nodes.TryGetValue(pn, out var nd) || !OnPage(nd.X)) continue;
            var pr = PortRect(nd); var pcol = new SolidColorBrush(Color.FromRgb(0xe8, 0xc0, 0x00));
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x60, 0xe8, 0xc0, 0x00)), new Pen(pcol, 1.6 / scale), pr);
            var ft = Ft($"SZLAK WEWNĘTRZNY {StationDoc.Sv(pv["id"])} – niepołączony", fs * 0.9, pcol);
            dc.DrawText(ft, new Point(pr.X + pr.Width / 2 - ft.Width / 2, pr.Y - ft.Height - 2 / scale));
            if (selKind == "port" && selId == pn) dc.DrawRectangle(null, new Pen(Brushes.DeepSkyBlue, 2 / scale), new Rect(pr.X - 3, pr.Y - 3, pr.Width + 6, pr.Height + 6));
        }
        if (tool == Tool.Turnout && turnFrom != null && doc.Nodes.ContainsKey(turnFrom))
        {
            var a = doc.Pos(turnFrom); var m = Snap(mouse); int sx = m.X >= a.X ? 1 : -1; double k = Math.Max(4, Grid); int v = Math.Sign(m.Y - a.Y);
            var pp = new Pen(new SolidColorBrush(Color.FromArgb(0xc0, 0x00, 0xa0, 0xff)), 4.6) { LineJoin = PenLineJoin.Round };
            if (v != 0)
            {
                var g = new StreamGeometry();
                using (var c = g.Open()) { c.BeginFigure(new Point(a.X, a.Y), false, false); c.PolyLineTo(new[] { new Point(a.X + sx * k, a.Y + v * k), new Point(a.X + sx * k, m.Y - v * k), new Point(a.X + sx * 2 * k, m.Y) }, true, true); }
                dc.DrawGeometry(null, pp, g);
            }
        }
        if (tool == Tool.Link && linkFrom != null && doc.Nodes.ContainsKey(linkFrom)) { var a = doc.Pos(linkFrom); if (OnPage(a.X)) dc.DrawEllipse(null, new Pen(Brushes.DeepSkyBlue, 2 / scale), new Point(a.X, a.Y), 9, 9); }
        if (tool == Tool.Szlak && szlakFrom != null) { var a = doc.Pos(szlakFrom); dc.DrawLine(new Pen(Brushes.Yellow, 2) { DashStyle = DashStyles.Dash }, new Point(a.X, a.Y), mouse); }
        if (tool == Tool.Platform && rectFrom is Point rf) dc.DrawRectangle(null, pen, new Rect(rf, Snap(mouse)));
        if (tool == Tool.Crossing && crossFrom is Point cfr) dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(0xf0, 0x80, 0xf0)), 3), cfr, new Point(cfr.X, Snap(mouse).Y));
    }

    // ------------------------------------------------------------------ klawiatura
    void OnKey(object s, KeyEventArgs e) => Safe(() => OnKeyCore(s, e));
    void OnKeyCore(object s, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox) { if (e.Key == Key.Escape) panel.Focus(); return; }
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (ctrl && e.Key == Key.Z) { DoUndo(); e.Handled = true; return; }
        if (ctrl && e.Key == Key.Y) { DoRedo(); e.Handled = true; return; }
        if (ctrl && e.Key == Key.S) { if (SaveTo(path)) Saved?.Invoke(path); e.Handled = true; return; }
        if (dispMode)
        {
            var dt = e.Key switch { Key.V => Tool.Select, Key.N => Tool.Label, Key.P => Tool.Platform, Key.X => Tool.Crossing, _ => (Tool?)null };
            if (dt != null) { SetTool(dt.Value); e.Handled = true; return; }
            if (e.Key == Key.Escape) { Right(mouse); panel.InvalidateVisual(); e.Handled = true; return; }
            if (ctrl && e.Key == Key.A) { DSelectAll(); e.Handled = true; return; }
            if (!ctrl && e.Key == Key.Q) { SetBandMode(!bandMode); e.Handled = true; return; }
            if (DKey(e.Key)) e.Handled = true;
            return;
        }
        switch (e.Key)
        {
            case Key.Escape: Right(mouse); panel.InvalidateVisual(); break;
            case Key.Delete: DeleteSel(); break;
            case Key.Q when !ctrl: SetBandMode(!bandMode); break;
            case Key.V: SetTool(Tool.Select); break;
            case Key.T: SetTool(Tool.Track); break;
            case Key.I: SetTool(Tool.Section); break;
            case Key.Z: SetTool(Tool.Switch); break;
            case Key.S: SetTool(Tool.Signal); break;
            case Key.K: SetTool(Tool.TrackEnd); break;
            case Key.W: SetTool(Tool.Derailer); break;
            case Key.B: SetTool(Tool.Szlak); break;
            case Key.P: SetTool(Tool.Platform); break;
            case Key.N: SetTool(Tool.Label); break;
            case Key.M: SetTool(Tool.Measure); break;
            case Key.L: SetTool(Tool.Link); break;
            case Key.R: SetTool(Tool.Turnout); break;
            case Key.X: SetTool(Tool.Crossing); break;
            case Key.G: chkGrid.IsChecked = chkGrid.IsChecked != true; panel.InvalidateVisual(); break;
            case Key.F when ctrl: findBox.Focus(); findBox.SelectAll(); break;
            case Key.A when ctrl: SelectRect(panel.Page != null ? new Rect(panel.Page.X0, -1e5, panel.Page.X1 - panel.Page.X0, 2e5) : new Rect(-1e6, -1e6, 2e6, 2e6)); break;
            case Key.Left or Key.Right or Key.Up or Key.Down when selKind == "multi":
            {
                double g = Grid; doc.Snapshot();
                GroupMove(e.Key == Key.Left ? -g : e.Key == Key.Right ? g : 0, e.Key == Key.Up ? -g : e.Key == Key.Down ? g : 0);
                Changed("Przesunięto grupę"); selKind = "multi"; ShowProps(); break;
            }
            default: return;
        }
        e.Handled = true;
    }

    void DoUndo() { ClearMulti(); if (doc.Undo()) { chainFrom = null; szlakFrom = null; dragging = false; Changed("Cofnięto"); } }
    void DoRedo() { ClearMulti(); if (doc.Redo()) { chainFrom = null; szlakFrom = null; Changed("Ponowiono"); } }

    void DeleteSel()
    {
        if (selKind == "multi")
        {
            if (!FuzzMode && multi.Count > 15 && MessageBox.Show(this, $"Usunąć {multi.Count} węzłów razem z torami, sygnalizatorami i zwrotnicami?{(char)10}(Ctrl+Z cofa)", "Usuwanie", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            doc.Snapshot();
            foreach (var n in multi.ToList()) if (doc.Nodes.ContainsKey(n)) doc.DeleteNode(n);
            foreach (var i in multiLabels.OrderByDescending(x => x)) if (i < doc.Labels.Count) doc.Labels.RemoveAt(i);
            foreach (var i in multiPlats.OrderByDescending(x => x)) if (i < doc.Platforms.Count) doc.Platforms.RemoveAt(i);
            ClearMulti(); Changed("Usunięto zaznaczone", false); return;
        }
        if (selKind == null) return;
        doc.Snapshot();
        switch (selKind)
        {
            case "node":
                if (doc.IsPlainNode(selId) && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) { doc.MergeNode(selId); Changed("Węzeł zamieniony na tor (tory scalone)", false); return; }
                doc.DeleteNode(selId); break;
            case "edge": doc.DeleteEdge(selId); break;
            case "sig": doc.Signals.Remove(selId); break;
            case "sw": { var n = StationDoc.Sv(doc.Switches[selId]["node"]); doc.Switches.Remove(selId); doc.Nodes[n]["kind"] = ""; break; }
            case "te": doc.TrackEnds.Remove(selId); break;
            case "der": doc.Derailers.Remove(selId); break;
            case "label": doc.Labels.RemoveAt(selIdx); break;
            case "platform": doc.Platforms.RemoveAt(selIdx); break;
            case "crossing": doc.Crossings.RemoveAt(selIdx); break;
            case "block": doc.DeleteBlock(selId); break;
        }
        Changed("Usunięto", false);
    }

    string NextSwitchNum()
    {
        var nums = doc.Switches.Select(s => StationDoc.Sv(s.Value["label"])).Select(l => int.TryParse(new string((l ?? "").TakeWhile(char.IsDigit).ToArray()), out var v) ? v : 0);
        return ((nums.DefaultIfEmpty(0).Max()) + 1).ToString();
    }

    // ------------------------------------------------------------------ właściwości
    void ShowProps()
    {
        props.Children.Clear();
        if (dispMode && !(selKind is "label" or "platform" or "crossing" && Exists())) { DShowProps(); return; }
        if (selKind == "port" && doc.Ports[selId] is JsonObject po)
        {
            props.Children.Add(T($"Złącze SZLAK WEWNĘTRZNY {StationDoc.Sv(po["id"])}", 14, Brushes.White, true));
            props.Children.Add(T($"Stacja: {st.Posts.GetValueOrDefault(StationDoc.Sv(po["post"]) ?? "") ?? StationDoc.Sv(po["post"])}, semafor wjazdowy przy {StationDoc.Sv(po["entry"])}. Wybierz złącze drugiej stacji – tory połączą się, powstanie jeden szlak wewnętrzny z semaforami na obu końcach.", 10.5, Brushes.Gray));
            var others = doc.Ports.Where(x => x.Key != selId).Select(x => (Node: x.Key, Text: $"{StationDoc.Sv(x.Value["id"])} ({st.Posts.GetValueOrDefault(StationDoc.Sv(x.Value["post"]) ?? "") ?? StationDoc.Sv(x.Value["post"])})")).ToList();
            if (others.Count == 0) props.Children.Add(T("Brak innych złączy – utwórz złącze na drugiej stacji (narzędzie Szlak, rodzaj „wewnętrzny – złącze”).", 11, Brushes.Orange));
            else
            {
                props.Children.Add(T("Łączy się z", 11, Brushes.Gray));
                var cb = new ComboBox { ItemsSource = others.Select(x => x.Text).ToList(), SelectedIndex = 0, Margin = new Thickness(0, 2, 0, 4) };
                props.Children.Add(cb);
                props.Children.Add(T("Długość szlaku [m]", 11, Brushes.Gray));
                var lb = Box("2000"); props.Children.Add(lb);
                var me = selId;
                props.Children.Add(Btn("Połącz", (a, e) => Safe(() =>
                {
                    double ll = double.TryParse(lb.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var lv) ? lv : 2000;
                    doc.Snapshot(); var bid = doc.PairPorts(me, others[cb.SelectedIndex].Node, ll);
                    Changed($"Połączono złącza – szlak wewnętrzny {bid}"); Select("block", bid);
                })));
            }
            props.Children.Add(Btn("Usuń złącze", (a, e) => Safe(() => { doc.Snapshot(); doc.Ports.Remove(selId); Changed("Usunięto złącze", false); })));
            return;
        }
        if (selKind == "multi")
        {
            multi.RemoveWhere(n => !doc.Nodes.ContainsKey(n));
            props.Children.Add(T($"Zaznaczono: węzły {multi.Count}, napisy {multiLabels.Count}, perony {multiPlats.Count}", 14, Brushes.White, true));
            props.Children.Add(T("Przeciągnij dowolny zaznaczony węzeł albo użyj strzałek, by przesunąć całą grupę (o krok siatki). Shift+klik dodaje / usuwa węzeł, Shift+przeciągnięcie – prostokąt.", 10.5, Brushes.Gray));
            // kategoria i elektryfikacja – dla wszystkich torów między zaznaczonymi węzłami (np. cały tor: dwuklik z Shift)
            var selEdges = doc.EdgeObjs.Where(x => !StationDoc.IsLink(x) && multi.Contains(StationDoc.Sv(x["a"])) && multi.Contains(StationDoc.Sv(x["b"]))).ToList();
            if (selEdges.Count > 0)
            {
                props.Children.Add(T($"Tory między zaznaczonymi węzłami: {selEdges.Count} (plan schematyczny wg Ig-10)", 11.5, Brushes.LightGray));
                var rowC = new WrapPanel();
                foreach (var (v, t) in TrackCats)
                    rowC.Children.Add(Btn(t, (a, e) => Safe(() => { doc.Snapshot(); foreach (var x in selEdges) { if (v == "b") x.Remove("cat"); else x["cat"] = v; } Changed($"Kategoria toru: {t} ({selEdges.Count} torów)"); selKind = "multi"; ShowProps(); })));
                rowC.Children.Add(Btn("zelektryfikowane", (a, e) => Safe(() => { doc.Snapshot(); foreach (var x in selEdges) x["elec"] = true; Changed($"Tory zelektryfikowane ({selEdges.Count})"); selKind = "multi"; ShowProps(); })));
                rowC.Children.Add(Btn("bez sieci trakcyjnej", (a, e) => Safe(() => { doc.Snapshot(); foreach (var x in selEdges) x.Remove("elec"); Changed($"Tory bez sieci trakcyjnej ({selEdges.Count})"); selKind = "multi"; ShowProps(); })));
                props.Children.Add(rowC);
            }
            var rowS = new StackPanel { Orientation = Orientation.Horizontal };
            var spB = Box((Grid * 5).ToString(CultureInfo.InvariantCulture)); spB.Width = 50; rowS.Children.Add(spB);
            rowS.Children.Add(new TextBlock { Text = " px ", Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center });
            rowS.Children.Add(Btn("Rozstaw tory w pionie", (a, e) => Safe(() =>
            {
                if (!double.TryParse(spB.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var sp)) { status.Text = "✗ odstęp: liczba"; return; }
                Respace(sp);
            })));
            props.Children.Add(T($"Rozstaw torów – odstęp między torami (siatka: {Grid:0} px = 1 kratka; obecnie np. 5 kratek = {Grid * 5:0} px). Łączniki zachowują ukosy, skraca się ich pion.", 10.5, Brushes.Gray));
            props.Children.Add(rowS);
            props.Children.Add(Btn("Wyrównaj w poziomie (wspólne Y)", (a, e) => Safe(AlignY)));
            props.Children.Add(Btn("Rozmieść równomiernie w poziomie", (a, e) => Safe(DistributeX)));
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var sb2 = Box(secBox.Text); sb2.Width = 110; row.Children.Add(sb2);
            row.Children.Add(Btn("Przypisz odcinek torom grupy", (a, e) => Safe(() =>
            {
                var sec = sb2.Text.Trim(); if (sec == "") { status.Text = "Podaj odcinek"; return; }
                if (!sec.Contains(':') && !st.Blocks.Values.Any(b => b.SecsWE.Contains(sec))) sec = Post + ":" + sec;
                doc.Snapshot(); int k = 0;
                foreach (var ed in doc.EdgeObjs.Where(x => multi.Contains(StationDoc.Sv(x["a"])) && multi.Contains(StationDoc.Sv(x["b"])))) { ed["sec"] = sec; k++; }
                Changed($"Odcinek {sec} → {k} torów"); selKind = "multi"; ShowProps();
            })));
            props.Children.Add(row);
            var rowV = new StackPanel { Orientation = Orientation.Horizontal };
            var vb2 = Box(""); vb2.Width = 60; rowV.Children.Add(vb2);
            rowV.Children.Add(Btn("Prędkość [km/h] torom grupy (puste – domyślna)", (a, e) => Safe(() =>
            {
                var t = vb2.Text.Trim(); double v = 0;
                if (t != "" && (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out v) || v <= 0)) { status.Text = "✗ prędkość: liczba km/h"; return; }
                doc.Snapshot(); int k = 0;
                foreach (var ed in doc.EdgeObjs.Where(x => multi.Contains(StationDoc.Sv(x["a"])) && multi.Contains(StationDoc.Sv(x["b"])))) { if (t == "") ed.Remove("vmax"); else ed["vmax"] = v; k++; }
                Changed($"Prędkość {(t == "" ? "domyślna" : t + " km/h")} → {k} torów"); selKind = "multi"; ShowProps();
            })));
            props.Children.Add(rowV);
            props.Children.Add(Btn("Wyprostuj rozjazdy w grupie (ukos – pion – ukos)", (a, e) => Safe(() =>
            {
                doc.Snapshot(); int ok = 0; var errs = new List<string>(); var done = new HashSet<string>();
                foreach (var sw in doc.Switches.Where(x => multi.Contains(StationDoc.Sv(x.Value["node"]))).Select(x => x.Key).ToList())
                {
                    if (!doc.Switches.ContainsKey(sw) || done.Contains(sw)) continue;
                    try
                    {
                        var other = doc.NormalizeTurnout(sw, Math.Max(4, Grid)); ok++; done.Add(sw);
                        if (other != null) done.Add(other);      // zwrotnica po drugiej stronie łącznika – wyprostowana razem z tą
                    }
                    catch (InvalidOperationException ex) { errs.Add(Station.Lbl(sw) + ": " + ex.Message); }
                }
                Changed($"Wyprostowano {ok} rozjazdów" + (errs.Count > 0 ? $"; pominięto {errs.Count} ({string.Join("; ", errs.Take(3))})" : "")); selKind = "multi"; ShowProps();
            })));
            props.Children.Add(Btn("Łączniki zwrotnic w grupie – odcinki zwrotnicowe", (a, e) => Safe(() =>
            {
                doc.Snapshot(); int k = 0;
                foreach (var sw in doc.Switches.Where(x => multi.Contains(StationDoc.Sv(x.Value["node"]))).Select(x => x.Key).ToList()) k += doc.CrossoverSecs(sw);
                Changed($"Łączniki: {k} torów przypisanych do odcinków zwrotnicowych"); selKind = "multi"; ShowProps();
            })));
            props.Children.Add(Btn("Zamień zbędne węzły na tor (uprość)", (a, e) => Safe(() => { doc.Snapshot(); var k = doc.Simplify(multi.ToList()); ClearMulti(); Changed($"Uproszczono: scalono {k} węzłów", false); })));
            props.Children.Add(Btn("Usuń zaznaczone (Del)", (a, e) => Safe(DeleteSel)));
            props.Children.Add(Btn("Odznacz", (a, e) => { ClearMulti(); ShowProps(); panel.InvalidateVisual(); }));
            return;
        }
        if (selKind == null || !Exists())
        {
            props.Children.Add(T("Nic nie zaznaczono", 13, Brushes.White, true));
            props.Children.Add(T($"Węzły {doc.Nodes.Count}, tory {doc.Edges.Count}, zwrotnice {doc.Switches.Count}, sygnalizatory {doc.Signals.Count}, szlaki {doc.Blocks.Count}.", 11, Brushes.Gray));
            props.Children.Add(T("Plik: " + path, 10, Brushes.DimGray));
            if (panel.PlanView) PlanSideProps();
            return;
        }
        JsonObject o = selKind switch
        {
            "node" => (JsonObject)doc.Nodes[selId], "edge" => doc.Edge(selId), "sig" => (JsonObject)doc.Signals[selId], "sw" => (JsonObject)doc.Switches[selId],
            "te" => (JsonObject)doc.TrackEnds[selId], "der" => (JsonObject)doc.Derailers[selId], "block" => (JsonObject)doc.Blocks[selId],
            "label" => (JsonObject)doc.Labels[selIdx], "platform" => (JsonObject)doc.Platforms[selIdx], "crossing" => (JsonObject)doc.Crossings[selIdx], _ => null
        };
        string title = selKind switch
        {
            "node" => "Węzeł " + selId, "edge" => "Tor (krawędź) " + selId, "sig" => "Sygnalizator " + Station.Lbl(selId), "sw" => "Zwrotnica " + Station.Lbl(selId),
            "te" => "Kozioł / koniec toru", "der" => "Wykolejnica " + Station.Lbl(selId), "block" => "Szlak " + selId, "label" => "Napis", "platform" => "Peron", "crossing" => "Przejazd kolejowo-drogowy", _ => ""
        };
        props.Children.Add(T(title, 14, Brushes.White, true));
        if (dispMode) props.Children.Add(T("Na obrazie pulpitu (zobrazowanie). Plan schematyczny ma własną kopię – zmiana tutaj go nie rusza. Del – usuń z pulpitu.", 10.5, Brushes.Gray));
        if (selKind is "sig" or "sw" or "der") KeyField(selKind == "sig" ? doc.Signals : selKind == "sw" ? doc.Switches : doc.Derailers);
        string[] fields = selKind switch
        {
            "node" => new[] { "x", "y", "post" },
            "edge" => Math.Abs(st.Nodes[st.Edges[selId].A].X - st.Nodes[st.Edges[selId].B].X) < 0.5 ? new[] { "line", "vmax", "radius", "cant", "dir" } : new[] { "line", "vmax", "radius", "cant" },
            "sig" => new[] { "label", "type", "dir", "szlak", "repeats", "track", "lamps" },
            "sw" => new[] { "label", "sec", "vbok", "vprosto", "radius", "ratio", "manual" },
            "te" => new[] { "disp", "id", "label", "marker" },
            "der" => new[] { "label", "sec" },
            "block" => Array.Empty<string>(),
            "label" => new[] { "text", "x", "y", "anchor" },
            "platform" => new[] { "name", "x", "y", "w", "h" },
            "crossing" => new[] { "name", "x", "y0", "y1" },
            _ => Array.Empty<string>()
        };
        if (selKind == "block") BlockPanel(o);
        if (selKind == "crossing") ComboField(o, "kind", "Oznaczenie przejazdu", new[] { ("Y", "Y"), ("T", "T") });
        if (selKind == "edge") ComboField(o, "dismantled", "Tor rozebrany", new[] { ("false", "nie"), ("true", "tak – linia przerywana, wyłączony z jazd") });
        if (selKind == "edge") ComboField(o, "cat", "Kategoria toru (Ig-10 – grubość linii na planie; cały tor: Shift+dwuklik i panel grupy)", TrackCats);
        if (selKind == "edge") ComboField(o, "elec", "Sieć trakcyjna (Ig-10 – tor zelektryfikowany niebieski)", new[] { ("false", "nie"), ("true", "tak – tor zelektryfikowany") });
        foreach (var f in fields)
        {
            if (selKind == "sig" && f == "type") { ComboField(o, "type", "Typ sygnalizatora", new[] { ("wjazdowy", "semafor wjazdowy"), ("wyjazdowy", "semafor wyjazdowy / drogowy z funkcją tarczy manewrowej (Ms2)"), ("wyjazdowy_bez", "semafor wyjazdowy / drogowy BEZ funkcji manewrowej"), ("tm", "tarcza manewrowa"), ("to", "tarcza ostrzegawcza"), ("sp", "powtarzacz"), ("pk", "PK – punkt końcowy przebiegu"), ("odstepowy", "sygnalizator odstępowy (SBL)") }); continue; }
            if (selKind == "sig" && f == "dir" && StationDoc.Sv(o["type"]) == "wyjazdowy")
                ComboField(o, "shunt", "Funkcja tarczy manewrowej (Ms2)", new[] { ("true", "tak – podaje Ms2 (symbol: trójkąt + grot)"), ("false", "nie – tylko sygnały pociągowe (symbol: trójkąt)") });
            if (selKind == "sig" && f == "dir" && StationDoc.Sv(o["type"]) == "pk")
                ComboField(o, "routes", "Przebiegi kończące się w PK", new[] { ("PM", "pociągowe i manewrowe"), ("P", "pociągowe"), ("M", "manewrowe") });
            if (selKind == "sig" && f == "dir") { ComboField(o, "dir", "Kierunek jazdy", new[] { ("1", "→ na wschód (w prawo)"), ("-1", "← na zachód (w lewo)") }); continue; }
            if (selKind == "sw" && f == "radius")
                ComboField(o, "kind", "Typ rozjazdu (ustawia skos, promień i domyślną prędkość na bok)", new[] { ("", "– nie określono –") }.Concat(SwitchDef.Types.Select(t => (t.Kind, $"{t.Kind}  → na bok {t.V:0} km/h"))).ToArray());
            Field(o, f, selKind == "edge" ? f switch { "line" => "Linia kolejowa (nr)", "vmax" => "Prędkość dopuszczalna na torze [km/h]", "dir" => "Kierunek jazdy toru pionowego (1 – na wschód, -1 – na zachód)", "radius" => "Promień łuku [m] (puste – tor prosty)", "cant" => "Przechyłka w łuku [mm]", _ => null }
                : selKind == "sw" ? f switch { "label" => "Numer zwrotnicy", "sec" => "Odcinek zwrotnicowy", "vbok" => "Prędkość na bok [km/h] (puste – z typu rozjazdu)", "vprosto" => "Ograniczenie na wprost [km/h]", "radius" => "Promień toru zwrotnego [m]", "ratio" => "Skos (np. 1:9)", "manual" => "Zwrotnica ręczna (true/false)", _ => null }
                : selKind == "sig" && f == "lamps" ? $"Liczba komór – plan schematyczny Ig-10 (puste – domyślnie {st.Signals.GetValueOrDefault(selId)?.LampCount})" : null);
        }
        var hint = selKind switch
        {
            "edge" => "Prędkość: puste – domyślna (tor stacyjny 60 km/h, szlak – prędkość pociągu). Odcinek kontroli niezajętości – niżej: tory z tym samym identyfikatorem (posterunek:nazwa) tworzą jeden odcinek; tor stacyjny JT wyświetla numer toru i okno numeru pociągu.",
            "sig" => "dir: 1 – jazda na wschód, -1 – na zachód. szlak – dla semafora wjazdowego oznaczenie szlaku. repeats (powtarzacz sp / tarcza ostrzegawcza to) – nazwa powtarzanego semafora, także sygnalizatora SBL (np. 2A/1+), albo oznaczenie szlaku (np. 2A) – informacja z sąsiedniej stacji: zielony, gdy jedzie pociąg w kierunku powtarzacza.",
            "sw" => "Plus (+) – odgałęzienie bardziej poziome. vbok – prędkość przy jeździe na bok, na ostrze i z ostrza [km/h] (domyślnie 40; obejmuje też łącznik do następnej zwrotnicy), vprosto – ograniczenie na wprost (puste – brak). manual: true – zwrotnica ręczna.",
            "block" => null,
            _ => null
        };
        if (hint != null) props.Children.Add(T(hint, 10.5, Brushes.Gray));
        if (selKind == "edge")
        {
            var e = doc.Edge(selId); var sec = StationDoc.Sv(e["sec"]);
            var frag = doc.EdgeObjs.Count(x => StationDoc.Sv(x["sec"]) == sec);
            SectionPanel(sec);
            LenField($"Długość odcinka {Station.Lbl(sec)} [m] ({frag} {(frag == 1 ? "tor" : "fragmenty torów")})", sec);
            EdgeScaleInfo(e);
            props.Children.Add(Btn("Zaznacz cały tor (dwuklik) – do przesunięcia", (a, b) => SelectTrack(selId)));
            props.Children.Add(Btn("Nowy odcinek tylko dla tego toru", (a, b) => { doc.Snapshot(); e["sec"] = doc.NewSecId(Post, "it"); Changed("Wydzielono odcinek"); }));
            props.Children.Add(Btn("Zaznacz tor stacyjny (JT)…", (a, b) =>
            {
                var r = Ask("Tor stacyjny", ("Numer toru", StationDoc.Sv(e["track"]) ?? ""));
                if (r == null) return;
                doc.Snapshot(); var ns = $"{Post}:JT{r[0].Trim()}";
                foreach (var x in doc.EdgeObjs.Where(x => StationDoc.Sv(x["sec"]) == sec)) { x["sec"] = ns; x["track"] = r[0].Trim(); }
                Changed($"Odcinek {sec} → tor stacyjny {r[0]}");
            }));
        }
        if (selKind == "block" && !st.Blocks.ContainsKey(selId)) props.Children.Add(T("⚠ Szlak nieczynny w modelu (brak torów odstępów lub granicy) – usuń go albo popraw tory.", 11, Brushes.OrangeRed));
        if (selKind == "block" && st.Blocks.ContainsKey(selId))
        {
            var b = st.Blocks[selId];
            props.Children.Add(T($"{b.TypeShort}; {(b.Internal ? "szlak wewnętrzny LCS" : "szlak do posterunku spoza LCS")}. Odstępy: {b.SecsWE.Count}.{(b.OneWay ? " Kierunek stały: defDir (1 – na wschód, -1 – na zachód)." : "")}", 11, Brushes.LightGray));
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var nb = Box(b.SecsWE.Count.ToString()); nb.Width = 50;
            row.Children.Add(new TextBlock { Text = "Liczba odstępów: ", Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(nb);
            bool odDone = false; var odBlk = selId;
            void ApplyOd()
            {
                if (odDone || selId != odBlk || !doc.Blocks.ContainsKey(odBlk)) return;
                if (!int.TryParse(nb.Text, out var n) || n < 1 || n > 8) { status.Text = "Liczba odstępów 1–8"; return; }
                if (n == b.SecsWE.Count) return;
                odDone = true;
                try { doc.Snapshot(); doc.SetOdstepy(odBlk, n); Changed(n == 1 ? $"Szlak {odBlk}: jeden odstęp (bez sygnalizatorów odstępowych)" : $"Szlak {odBlk}: {n} odstępy"); }
                catch (Exception ex) { doc.Undo(); status.Text = "✗ " + ex.Message; }
            }
            nb.KeyDown += (a, ev) => { if (ev.Key == Key.Enter) Safe(ApplyOd); };
            nb.LostFocus += (a, ev) => Dispatcher.BeginInvoke(() => Safe(ApplyOd));
            row.Children.Add(Btn("Zastosuj", (a, ev) => Safe(ApplyOd)));
            props.Children.Add(row);
            props.Children.Add(T("Długości odstępów [m] (od zachodu):", 11.5, Brushes.White, true));
            foreach (var od in b.SecsWE) LenField($"odstęp {od}", od);
            props.Children.Add(T($"Długość szlaku w obszarze: {b.SecsWE.Sum(doc.SecLen):0} m", 11, Brushes.Gray));
            props.Children.Add(Btn("Usuń szlak (tory zostają)", (a, ev) => DeleteSel()));
        }
        if (selKind == "node")
        {
            bool plain = doc.IsPlainNode(selId);
            props.Children.Add(T(plain ? "Zwykły punkt na torze. Del – zamiana na tor (scalenie), Shift+Del – usunięcie razem z torami." : $"Węzeł: {doc.EdgesAt(selId).Count} tory{(doc.Switches.Any(x => StationDoc.Sv(x.Value["node"]) == selId) ? ", zwrotnica" : "")}{(doc.Signals.Any(x => StationDoc.Sv(x.Value["node"]) == selId) ? ", sygnalizator" : "")}. Del – usunięcie razem z torami.", 10.5, Brushes.Gray));
            if (doc.Bumpers.ContainsKey(selId)) props.Children.Add(Btn("Usuń kozioł oporowy (symbol)", (a, e) => Safe(() => { doc.Snapshot(); doc.Bumpers.Remove(selId); Changed("Usunięto kozioł oporowy", false); })));
            else if (doc.EdgesAt(selId).Count == 1 && !doc.TrackEnds.ContainsKey(selId)) props.Children.Add(Btn("Dodaj kozioł oporowy (symbol)", (a, e) => Safe(() => { doc.Snapshot(); doc.Bumpers[selId] = new JsonObject(); Changed("Dodano kozioł oporowy (symbol)", false); })));
            if (plain) props.Children.Add(Btn("Zamień na tor (scal tory)", (a, e) => Safe(() => { doc.Snapshot(); doc.MergeNode(selId); Changed("Węzeł zamieniony na tor", false); })));
        }
        if (selKind == "sw" && doc.Switches.ContainsKey(selId))
            props.Children.Add(Btn("Wyprostuj rozjazd (ukos – pion – ukos)", (a, e) => Safe(() => { doc.Snapshot(); doc.NormalizeTurnout(selId, Math.Max(4, Grid)); Changed("Wyprostowano rozjazd " + Station.Lbl(selId)); })));
            props.Children.Add(Btn("Odcinek zwrotnicowy na łącznik (jedna przerwa w środku)", (a, e) => Safe(() => { doc.Snapshot(); var k = doc.CrossoverSecs(selId); Changed($"Łącznik: {k} torów w odcinkach zwrotnicowych"); })));
        if (selKind == "sw" && !st.Switches.ContainsKey(selId)) props.Children.Add(T("⚠ Zwrotnica nieczynna w modelu: z węzła muszą wychodzić 3 tory – po jednej stronie iglica, po drugiej dwa odgałęzienia.", 11, Brushes.OrangeRed));
        if (selKind == "edge") secBox.Text = StationDoc.Sv(doc.Edge(selId)["sec"]);
        if (selKind == "sig" && doc.Signals[selId] is JsonObject so)
            props.Children.Add(Btn("Odwróć kierunek sygnalizatora", (a, e) => Safe(() => { doc.Snapshot(); so["dir"] = -(int)StationDoc.D(so["dir"]); Changed("Odwrócono kierunek"); })));
        if (selKind == "sig" && !st.Signals.ContainsKey(selId)) props.Children.Add(T("⚠ Sygnalizator nieczynny w modelu (np. odstępowy szlaku, który usunięto) – usuń go.", 11, Brushes.OrangeRed));
        if (selKind == "sig" && st.Signals.TryGetValue(selId, out var sgx) && sgx.Type is "wjazdowy" or "wyjazdowy" or "tm") RoutesPanel(selId);
        props.Children.Add(Btn("Usuń (Del)", (a, b) => DeleteSel()));
    }

    /// <summary>
    /// Przebiegi od sygnalizatora: wyznaczone z układu torowego; dla każdej pary początek–koniec można wskazać przebieg zasadniczy
    /// (pozostałe stają się wariantowymi) i wyłączyć przebiegi niedopuszczalne. Reguły zapisują się w station.json (routeRules).
    /// </summary>
    void RoutesPanel(string sig)
    {
        List<RouteDef> raw;
        try { raw = sim.ApplyRules(sim.RawRoutes(sig)); }
        catch (Exception ex) { props.Children.Add(T("Przebiegi: błąd – " + ex.Message, 11, Brushes.OrangeRed)); return; }
        props.Children.Add(T($"Przebiegi od {Station.Lbl(sig)} ({raw.Count(r => !r.Off)} czynnych, {raw.Count(r => r.Off)} wyłączonych)", 13, Brushes.White, true));
        props.Children.Add(T("✓ – przebieg czynny, ★ – zasadniczy (pozostałe do tego samego końca to wariantowe), pole – prędkość przebiegu w granicach stacji [km/h] (puste: wyliczona z torów i zwrotnic, * – ustalona). Klik na opisie – podgląd na obrazie.", 10.5, Brushes.Gray));
        foreach (var grp in raw.GroupBy(r => (r.Type, r.End)))
        {
            props.Children.Add(T($"{(grp.Key.Type == "P" ? "Pociągowy" : "Manewrowy")} → {Station.Lbl(grp.Key.End)}", 11.5, grp.Key.Type == "P" ? Brushes.LightGreen : Brushes.Khaki, true));
            foreach (var r in grp)
            {
                var row = new DockPanel { Margin = new Thickness(6, 0, 0, 1) };
                var on = new CheckBox { IsChecked = !r.Off, VerticalAlignment = VerticalAlignment.Center, ToolTip = "przebieg czynny" };
                var rr = r;
                on.Click += (a, e) => { doc.Snapshot(); doc.SetRule(rr.Start, rr.End, rr.Type, rr.SwKey, on.IsChecked != true, null); Changed((on.IsChecked == true ? "Włączono " : "Wyłączono ") + rr.Id); };
                var star = new Button { Content = r.Variant == 0 && !r.Off ? "★" : "☆", Padding = new Thickness(4, 0, 4, 0), Margin = new Thickness(4, 0, 4, 0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.Gold, ToolTip = "ustaw jako przebieg zasadniczy", IsEnabled = !r.Off };
                star.Click += (a, e) => { doc.Snapshot(); doc.SetRule(rr.Start, rr.End, rr.Type, rr.SwKey, null, true); Changed("Przebieg zasadniczy: " + rr.Id); };
                var vbx = new TextBox { Text = r.VSet > 0 ? r.VSet.ToString("0") : "", Width = 34, Margin = new Thickness(0, 0, 4, 0), Background = Brushes.Black, Foreground = Brushes.Gold, CaretBrush = Brushes.White, ToolTip = $"Prędkość przebiegu [km/h] – puste: wyliczona z torów i zwrotnic ({r.VCalc:0} km/h)", VerticalAlignment = VerticalAlignment.Center };
                bool vdone = false;
                void VCommit()
                {
                    if (vdone || !ReferenceEquals(doc.Root, doc.Root)) return;
                    var tx = vbx.Text.Trim(); double vv = 0;
                    if (tx != "" && (!double.TryParse(tx, NumberStyles.Float, CultureInfo.InvariantCulture, out vv) || vv <= 0)) { status.Text = "✗ prędkość przebiegu: liczba km/h"; return; }
                    if (Math.Abs(vv - rr.VSet) < 0.01) return;
                    vdone = true; doc.Snapshot(); doc.SetRule(rr.Start, rr.End, rr.Type, rr.SwKey, null, null, vv);
                    Changed($"{rr.Id}: prędkość {(vv > 0 ? vv.ToString("0") + " km/h" : $"wyliczona ({rr.VCalc:0} km/h)")}");
                }
                vbx.KeyDown += (a, e) => { if (e.Key == Key.Enter) Safe(VCommit); };
                vbx.LostFocus += (a, e) => Dispatcher.BeginInvoke(() => Safe(VCommit));
                var txt = new TextBlock { Text = (r.Off ? "wyłączony" : r.Variant == 0 ? "zasadniczy" : $"wariant {r.Variant}") + $"  V {r.VMax:0}{(r.VSet > 0 ? "*" : "")}  [{(r.SwText == "" ? "bez zwrotnic" : r.SwText)}]", Foreground = r.Off ? Brushes.DimGray : Brushes.LightGray, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Cursor = Cursors.Hand };
                txt.MouseLeftButtonUp += (a, e) => { routeEdges = rr.Edges.ToList(); panel.InvalidateVisual(); status.Text = $"{rr.Id}: odcinki {string.Join(" ", rr.Secs.Select(Station.Lbl))}{(rr.Overlap != null ? ", droga ochronna " + Station.Lbl(rr.Overlap) : "")}"; };
                DockPanel.SetDock(on, Dock.Left); DockPanel.SetDock(star, Dock.Left); DockPanel.SetDock(vbx, Dock.Left);
                row.Children.Add(on); row.Children.Add(star); row.Children.Add(vbx); row.Children.Add(txt);
                props.Children.Add(row);
            }
        }
    }

    /// <summary>Pole długości odcinka izolowanego / odstępu (rzeczywista długość w metrach – decyduje o czasie jazdy i zajętości).</summary>
    void LenField(string label, string sec)
    {
        props.Children.Add(T(label, 11, Brushes.Gray));
        var tb = Box(doc.SecLen(sec).ToString("0.#", CultureInfo.InvariantCulture));
        var root = doc.Root; bool done = false;
        void Commit()
        {
            if (done || !ReferenceEquals(root, doc.Root)) return;
            if (!double.TryParse(tb.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) || d < 1) { status.Text = "✗ długość: wpisz liczbę metrów"; return; }
            if (Math.Abs(d - doc.SecLen(sec)) < 0.05) return;
            done = true; doc.Snapshot(); doc.SetSecLen(sec, d); Changed($"Odcinek {Station.Lbl(sec)}: {d:0.#} m");
        }
        tb.KeyDown += (a, e) => { if (e.Key == Key.Enter) Safe(Commit); };
        tb.LostFocus += (a, e) => Dispatcher.BeginInvoke(() => Safe(Commit));
        props.Children.Add(tb);
    }

    /// <summary>Pole wyboru z listy (typ i kierunek sygnalizatora).</summary>
    static readonly (string Val, string Text)[] TrackCats = { ("gz", "główny zasadniczy"), ("gd", "główny dodatkowy"), ("b", "boczny / inny") };

    void ComboField(JsonObject o, string key, string label, (string Val, string Text)[] opts)
    {
        props.Children.Add(T(label, 11, Brushes.Gray));
        var cur = key == "aspects" && o[key] == null ? "3" : key == "routes" && o[key] == null ? "PM" : key == "type" && StationDoc.Sv(o["type"]) == "wyjazdowy" && o["shunt"] is JsonValue shv && shv.TryGetValue<bool>(out var shb) && !shb ? "wyjazdowy_bez" :
            o[key] == null ? (key == "shunt" ? "true" : key is "dismantled" or "elec" ? "false" : key == "cat" ? "b" : key == "kind" ? "Y" : "") : o[key] is JsonValue v && v.TryGetValue<string>(out var sv) ? sv : o[key].ToJsonString();
        var cb = new ComboBox { ItemsSource = opts.Select(x => x.Text).ToList(), SelectedIndex = Array.FindIndex(opts, x => x.Val == cur), Margin = new Thickness(0, 2, 0, 4) };
        string myId = selId; var root = doc.Root;
        cb.SelectionChanged += (a, e) => Safe(() =>
        {
            if (cb.SelectedIndex < 0 || !ReferenceEquals(root, doc.Root)) return;
            var val = opts[cb.SelectedIndex].Val; if (val == cur) return;
            doc.Snapshot();
            if (key == "type" && val is "wyjazdowy" or "wyjazdowy_bez") { o["type"] = "wyjazdowy"; if (val == "wyjazdowy_bez") o["shunt"] = false; else o.Remove("shunt"); doc.SplitSectionAt(StationDoc.Sv(o["node"]), StationDoc.Sv(o["post"])); Changed($"{label}: {opts[cb.SelectedIndex].Text}"); return; }
            if (key == "kind" && selKind == "sw")
            {
                var t = SwitchDef.Types.FirstOrDefault(x => x.Kind == val);
                if (val == "") { o.Remove("kind"); }
                else { o["kind"] = val; if (t.R > 0) o["radius"] = t.R; else o.Remove("radius"); if (t.Ratio != "") o["ratio"] = t.Ratio; o.Remove("vbok"); }
                Changed($"{label}: {opts[cb.SelectedIndex].Text}"); return;
            }
            if (key == "dismantled") { if (val == "true") o["dismantled"] = true; else o.Remove("dismantled"); }
            else if (key == "elec") { if (val == "true") o["elec"] = true; else o.Remove("elec"); }
            else if (key == "cat") { if (val == "b") o.Remove("cat"); else o["cat"] = val; }
            else if (key is "dir" or "defDir" or "aspects") o[key] = int.Parse(val); else if (key == "shunt") { if (val == "true") o.Remove("shunt"); else o["shunt"] = false; } else o[key] = val;
            if (key == "type" && val is "wjazdowy" or "wyjazdowy" or "tm") doc.SplitSectionAt(StationDoc.Sv(o["node"]), StationDoc.Sv(o["post"]));
            if (key == "type" && val is not ("to" or "sp")) o.Remove("repeats");
            Changed($"{label}: {opts[cb.SelectedIndex].Text}");
        });
        props.Children.Add(cb);
    }

    /// <summary>
    /// Odcinek kontroli niezajętości: identyfikator (posterunek + nazwa), rodzaj kontroli (licznik osi / obwód torowy), numer toru.
    /// </summary>
    void SectionPanel(string sec)
    {
        bool szl = doc.EdgeObjs.Any(x => StationDoc.Sv(x["sec"]) == sec && x["szlak"] is JsonValue sv && sv.TryGetValue<bool>(out var sb) && sb);
        props.Children.Add(T("Odcinek kontroli niezajętości", 12.5, Brushes.White, true));
        if (szl) { props.Children.Add(T($"{sec} – odstęp szlaku (ustawienia w panelu szlaku).", 10.5, Brushes.Gray)); }
        else
        {
            var post = StationDoc.SecPost(sec); var local = sec.Contains(':') ? sec[(sec.IndexOf(':') + 1)..] : sec;
            // posterunek
            var posts = doc.Posts.Select(p => (p.Key, StationDoc.Sv(p.Value))).ToList();
            props.Children.Add(T("Posterunek", 11, Brushes.Gray));
            var cbp = new ComboBox { ItemsSource = posts.Select(p => $"{p.Item2} ({p.Key})").ToList(), SelectedIndex = posts.FindIndex(p => p.Key == post), Margin = new Thickness(0, 2, 0, 4) };
            cbp.SelectionChanged += (a, ev) => Safe(() =>
            {
                if (cbp.SelectedIndex < 0 || posts[cbp.SelectedIndex].Key == post) return;
                doc.Snapshot(); doc.RenameSection(sec, posts[cbp.SelectedIndex].Key + ":" + local); Changed($"Odcinek {local}: posterunek {posts[cbp.SelectedIndex].Item2}");
            });
            props.Children.Add(cbp);
            // nazwa (identyfikator w posterunku)
            props.Children.Add(T("Nazwa odcinka (JT2 – tor stacyjny 2, z15 – zwrotnicowy, it12 – torowy)", 11, Brushes.Gray));
            var nb = Box(local); props.Children.Add(nb);
            bool done = false;
            void CommitN()
            {
                if (done || doc.EdgeObjs.All(x => StationDoc.Sv(x["sec"]) != sec)) return;
                var v = nb.Text.Trim(); if (v == local || v == "") return;
                if (v.Contains(':') || v.Contains(' ')) { status.Text = "✗ nazwa bez spacji i dwukropka (posterunek wybierasz wyżej)"; return; }
                done = true; doc.Snapshot();
                try { doc.RenameSection(sec, (post == "" ? Post : post) + ":" + v); Changed($"Odcinek {local} → {v}"); }
                catch (InvalidOperationException ex) { doc.Undo(); Rebuild(); status.Text = "✗ " + ex.Message; }
            }
            nb.KeyDown += (a, ev) => { if (ev.Key == Key.Enter) Safe(CommitN); };
            nb.LostFocus += (a, ev) => Dispatcher.BeginInvoke(() => Safe(CommitN));
            if (local.StartsWith("JT")) props.Children.Add(T("Uwaga: rozkład jazdy przypisuje pociągi do torów stacyjnych po nazwie JT + numer toru.", 10, Brushes.Gray));
        }
        // rodzaj kontroli niezajętości
        props.Children.Add(T("Kontrola niezajętości", 11, Brushes.Gray));
        var dets = new[] { ("lo", "licznik osi – punkty liczące; po zakłóceniu ZeroLO i przejazd sprawdzający"), ("ot", "obwód torowy – odcinek izolowany; zajętość ustępuje samoczynnie") };
        var cur = doc.SecDetection(sec);
        var cbd = new ComboBox { ItemsSource = dets.Select(d => d.Item2 + (d.Item1 == doc.DefaultDetection ? "  (domyślny w obszarze)" : "")).ToList(), SelectedIndex = Array.FindIndex(dets, d => d.Item1 == cur), Margin = new Thickness(0, 2, 0, 4) };
        cbd.SelectionChanged += (a, ev) => Safe(() =>
        {
            if (cbd.SelectedIndex < 0 || dets[cbd.SelectedIndex].Item1 == cur) return;
            doc.Snapshot(); doc.SetSecDetection(sec, dets[cbd.SelectedIndex].Item1); Changed($"Odcinek {Station.Lbl(sec)}: {dets[cbd.SelectedIndex].Item2.Split(' ')[0]} {dets[cbd.SelectedIndex].Item2.Split(' ')[1]}", true);
        });
        props.Children.Add(cbd);
        if (!szl)
        {
            // numer toru – na wszystkich fragmentach odcinka
            props.Children.Add(T("Numer toru (puste – brak, np. łącznik zwrotnicowy)", 11, Brushes.Gray));
            var tr = doc.EdgeObjs.Where(x => StationDoc.Sv(x["sec"]) == sec).Select(x => StationDoc.Sv(x["track"])).FirstOrDefault(t => !string.IsNullOrEmpty(t)) ?? "";
            var tb = Box(tr); props.Children.Add(tb);
            bool tdone = false;
            void CommitT()
            {
                if (tdone) return; var v = tb.Text.Trim(); if (v == tr) return;
                tdone = true; doc.Snapshot();
                foreach (var x in doc.EdgeObjs.Where(x => StationDoc.Sv(x["sec"]) == sec)) x["track"] = v;
                Changed($"Odcinek {Station.Lbl(sec)}: tor {(v == "" ? "–" : v)}");
            }
            tb.KeyDown += (a, ev) => { if (ev.Key == Key.Enter) Safe(CommitT); };
            tb.LostFocus += (a, ev) => Dispatcher.BeginInvoke(() => Safe(CommitT));
        }
        props.Children.Add(new Separator { Margin = new Thickness(0, 6, 0, 6) });
    }

    /// <summary>Wszystkie dane blokady liniowej (szlaku) w jednym panelu.</summary>
    void BlockPanel(JsonObject o)
    {
        string id = selId; bool internalB = o["internal"] is JsonValue iv && iv.TryGetValue<bool>(out var ib) && ib;
        var ends = (JsonArray)o["ends"];
        string PostName(string p) => st.Posts.GetValueOrDefault(p ?? "") ?? p ?? "";
        void Head(string t) => props.Children.Add(T(t, 12, Brushes.White, true));
        void Hint(string t) => props.Children.Add(T(t, 10, Brushes.Gray));
        // oznaczenie (nazwa) szlaku
        Head("Oznaczenie szlaku");
        var idBox = Box(id); props.Children.Add(idBox);
        bool idDone = false;
        void CommitId()
        {
            if (idDone || !doc.Blocks.ContainsKey(id)) return;
            var nw = idBox.Text.Trim(); if (nw == id) return;
            idDone = true; doc.Snapshot();
            try { doc.RenameBlock(id, nw); selId = nw; Changed($"Szlak {id} → {nw}"); }
            catch (InvalidOperationException ex) { doc.Undo(); Rebuild(); status.Text = "✗ " + ex.Message; }
        }
        idBox.KeyDown += (a, e) => { if (e.Key == Key.Enter) Safe(CommitId); };
        idBox.LostFocus += (a, e) => Dispatcher.BeginInvoke(() => Safe(CommitId));
        Hint("Wewnętrzna nazwa szlaku (polecenia, rozkład jazdy – rozkład odwołuje się do szlaków zewnętrznych po tej nazwie, np. 1A, 2O). Napis nad strzałką ustawiasz niżej, osobno dla każdej stacji.");
        Head("Urządzenia");
        ComboField(o, "type", "Rodzaj blokady", BlockDef.Types.Select(t => (t.Code, t.Name)).ToArray());
        if (StationDoc.Sv(o["type"]) is "sbl" or "sbl1")
            ComboField(o, "aspects", "Stawność SBL (sygnały na semaforach odstępowych)", new[] { ("3", "trzystawna – S1, S5, S2 (domyślna)"), ("2", "dwustawna – S1, S2"), ("4", "czterostawna – S1, S5, S4, S2") });
        ComboField(o, "defDir", "Kierunek zasadniczy (po uruchomieniu; jednokierunkowa – kierunek stały)", new[] { ("0", "neutralny – brak ustawionego kierunku"), ("1", "→ na wschód (w prawo)"), ("-1", "← na zachód (w lewo)") });
        Head("Opis szlaku");
        Field(o, "line", "Linia kolejowa (nr)");
        Field(o, "tor", "Tor szlakowy (nr)");
        Field(o, "label", "Oznaczenie szlaku na obrazie, za strzałką (np. it1Z, 1ŁF; szlak zewnętrzny)");
        if (internalB) { Field(o, "nbrW", "Posterunek po stronie zachodniej (nazwa)"); Field(o, "nbrE", "Posterunek po stronie wschodniej (nazwa)"); }
        else Field(o, "nbr", "Posterunek sąsiedni (spoza LCS)");
        // końce szlaku: nazwa nad strzałką i semafor wjazdowy
        for (int i = 0; i < ends.Count; i++)
        {
            if (ends[i] is not JsonObject en) continue;
            var pst = StationDoc.Sv(en["post"]); int outw = (int)StationDoc.D(en["outward"]); int ei = i;
            Head($"Koniec przy stacji {PostName(pst)} ({(outw > 0 ? "wyjazd na wschód" : "wyjazd na zachód")})");
            props.Children.Add(T("Napis nad strzałką (oznaczenie szlaku na obrazie tej stacji)", 11, Brushes.Gray));
            var lb = Box(StationDoc.Sv(en["label"]) ?? id); props.Children.Add(lb);
            bool lDone = false;
            void CommitL()
            {
                if (lDone || !ReferenceEquals(en.Root, doc.Root)) return;
                var v = lb.Text.Trim(); if (v == (StationDoc.Sv(en["label"]) ?? id)) return;
                lDone = true; doc.Snapshot(); if (v == "" || v == id) en.Remove("label"); else en["label"] = v; Changed($"Napis nad strzałką ({PostName(pst)}): {(v == "" ? id : v)}", false);
            }
            lb.KeyDown += (a, e) => { if (e.Key == Key.Enter) Safe(CommitL); };
            lb.LostFocus += (a, e) => Dispatcher.BeginInvoke(() => Safe(CommitL));
            var bnode = StationDoc.Sv(en["boundary"]);
            var cands = doc.Signals.Where(x => StationDoc.Sv(x.Value["type"]) == "wjazdowy" && StationDoc.Sv(x.Value["post"]) == pst && (int)StationDoc.D(x.Value["dir"]) == -outw)
                .Select(x => x.Key).OrderBy(k => StationDoc.Sv(doc.Signals[k]["node"]) == bnode ? 0 : 1).ToList();
            var curSig = StationDoc.Sv(en["entrySig"]) ?? "";
            if (curSig != "" && !cands.Contains(curSig)) cands.Insert(0, curSig);
            props.Children.Add(T("Semafor wjazdowy od tego szlaku", 11, Brushes.Gray));
            var opts = new List<string> { "(brak)" }; opts.AddRange(cands.Select(Station.Lbl));
            var cb = new ComboBox { ItemsSource = opts, SelectedIndex = curSig == "" ? 0 : cands.IndexOf(curSig) + 1, Margin = new Thickness(0, 2, 0, 4) };
            cb.SelectionChanged += (a, e) => Safe(() =>
            {
                var nv = cb.SelectedIndex <= 0 ? "" : cands[cb.SelectedIndex - 1]; if (nv == curSig) return;
                doc.Snapshot(); doc.SetEntrySig(id, ei, nv); Changed($"Szlak {id}: semafor wjazdowy {(nv == "" ? "brak" : Station.Lbl(nv))}");
            });
            props.Children.Add(cb);
        }
        // prędkość na szlaku
        Head("Prędkość");
        var secs = ((JsonArray)o["secsWE"]).Select(StationDoc.Sv).ToHashSet();
        var vs = doc.EdgeObjs.Where(e => secs.Contains(StationDoc.Sv(e["sec"]) ?? "")).Select(e => e["vmax"] == null ? "" : StationDoc.D(e["vmax"]).ToString("0", CultureInfo.InvariantCulture)).Distinct().ToList();
        props.Children.Add(T("Prędkość dopuszczalna na szlaku [km/h] (puste – prędkość pociągu)", 11, Brushes.Gray));
        var vb = Box(vs.Count == 1 ? vs[0] : ""); props.Children.Add(vb);
        bool vDone = false;
        void CommitV()
        {
            if (vDone || !doc.Blocks.ContainsKey(id)) return;
            var t = vb.Text.Trim(); var cur = vs.Count == 1 ? vs[0] : null; if (t == cur) return;
            double? v = null;
            if (t != "") { if (!double.TryParse(t.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) || d < 5 || d > 250) { status.Text = "✗ prędkość 5–250 km/h"; return; } v = d; }
            vDone = true; doc.Snapshot(); doc.SetSzlakVmax(id, v); Changed($"Szlak {id}: V {(v == null ? "domyślna" : v + " km/h")}", false);
        }
        vb.KeyDown += (a, e) => { if (e.Key == Key.Enter) Safe(CommitV); };
        vb.LostFocus += (a, e) => Dispatcher.BeginInvoke(() => Safe(CommitV));
        // sygnalizatory odstępowe – nazwy (numery) wyświetlane na szlaku
        var ods = doc.Signals.Where(x => StationDoc.Sv(x.Value["szlak"]) == id && StationDoc.Sv(x.Value["type"]) == "odstepowy").OrderBy(x => doc.Pos(StationDoc.Sv(x.Value["node"])).X).ThenBy(x => -StationDoc.D(x.Value["dir"])).ToList();
        if (ods.Count > 0)
        {
            Head("Sygnalizatory odstępowe SBL (od zachodu)");
            foreach (var (k, v) in ods)
            {
                var so = (JsonObject)v;
                var r2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
                r2.Children.Add(new TextBlock { Text = (StationDoc.D(so["dir"]) > 0 ? "→ " : "← ") + k, Foreground = Brushes.Gray, Width = 110, VerticalAlignment = VerticalAlignment.Center });
                var sb = Box(StationDoc.Sv(so["label"]) ?? ""); sb.Width = 90; r2.Children.Add(sb);
                bool sDone = false;
                void CommitS()
                {
                    if (sDone || !ReferenceEquals(so.Root, doc.Root)) return;
                    var t = sb.Text.Trim(); if (t == "" || t == StationDoc.Sv(so["label"])) return;
                    sDone = true; doc.Snapshot(); so["label"] = t; Changed($"Sygnalizator odstępowy {k}: {t}", false);
                }
                sb.KeyDown += (a, e) => { if (e.Key == Key.Enter) Safe(CommitS); };
                sb.LostFocus += (a, e) => Dispatcher.BeginInvoke(() => Safe(CommitS));
                props.Children.Add(r2);
            }
        }
    }

    void KeyField(JsonObject dict)
    {
        props.Children.Add(T("Klucz (posterunek:nazwa)", 11, Brushes.Gray));
        var tb = Box(selId);
        string myId = selId; bool done = false;
        void Commit()
        {
            if (done || !ReferenceEquals(dict.Root, doc.Root)) return;
            var nk = tb.Text.Trim(); if (nk == myId || nk == "" || !dict.ContainsKey(myId)) return;
            done = true;
            try { doc.Snapshot(); doc.RenameKey(dict, myId, nk); if (selId == myId) selId = nk; Changed("Zmieniono nazwę"); }
            catch (Exception ex) { doc.Undo(); Rebuild(); status.Text = "✗ " + ex.Message; }
        }
        tb.KeyDown += (a, e) => { if (e.Key == Key.Enter) Safe(Commit); };
        tb.LostFocus += (a, e) => Dispatcher.BeginInvoke(() => Safe(Commit));
        props.Children.Add(tb);
    }

    void Field(JsonObject o, string key, string label = null)
    {
        props.Children.Add(T(label ?? key, 11, Brushes.Gray));
        var cur = o[key];
        var tb = Box(cur == null ? "" : cur is JsonValue v && v.TryGetValue<string>(out var s) ? s : cur.ToJsonString());
        string myKind = selKind, myId = selId;
        bool done = false;
        void Commit()
        {
            // pole mogło zostać odłączone (cofnięcie, wczytanie pliku) albo zaznaczenie się zmieniło
            if (done || !ReferenceEquals(o.Root, doc.Root)) return;
            var txt = tb.Text.Trim();
            var old = o[key];
            string oldTxt = old == null ? "" : old is JsonValue ov && ov.TryGetValue<string>(out var os) ? os : old.ToJsonString();
            if (txt == oldTxt) return;
            doc.Snapshot();
            bool numeric = key is "x" or "y" or "w" or "h" or "rot" or "sy" or "len" or "dir" or "defDir" or "vmax" or "vbok" or "vprosto" or "y0" or "y1" or "radius" or "cant" or "lamps";
            if (key is "vmax" or "vbok" or "vprosto" or "radius" or "cant" && txt == "") { if (o[key] != null) { doc.Snapshot(); o.Remove(key); done = true; Changed($"{key}: domyślna"); } return; }
            if (numeric && !double.TryParse(txt.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out _)) { status.Text = $"✗ {label ?? key}: wpisz liczbę"; tb.Text = oldTxt; return; }
            if (key is "dir" && txt is not ("1" or "-1")) { status.Text = "✗ dir: 1 (na wschód) albo -1 (na zachód)"; tb.Text = oldTxt; return; }
            if (key is "len" && double.Parse(txt.Replace(',', '.'), CultureInfo.InvariantCulture) < 1) { status.Text = "✗ długość musi być dodatnia"; tb.Text = oldTxt; return; }
            if (txt == "" && key is ("sec" or "label" or "text" or "name" or "id" or "disp")) { status.Text = $"✗ {label ?? key}: pole nie może być puste"; tb.Text = oldTxt; return; }
            if (txt == "" && key is not ("sec" or "label" or "text" or "name")) o.Remove(key);
            else if (double.TryParse(txt.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && key is "x" or "y" or "w" or "h" or "rot" or "sy" or "len" or "dir" or "defDir" or "vmax" or "vbok" or "vprosto" or "y0" or "y1" or "radius" or "cant")
            {
                if (key is "dir" or "defDir") o[key] = (int)d; else o[key] = d;
                if (myKind == "node" && key is "x" or "y") { var p = doc.Pos(myId); o[key] = key == "x" ? p.X : p.Y; doc.MoveNode(myId, key == "x" ? d : p.X, key == "y" ? d : p.Y); }
            }
            else if (txt is "true" or "false" || key is "manual" or "marker") o[key] = txt is "true" or "tak" or "1";
            else o[key] = txt;
            done = true;
            Changed($"{label ?? key} = {txt}");
        }
        tb.KeyDown += (a, e) => { if (e.Key == Key.Enter) Safe(Commit); };
        tb.LostFocus += (a, e) => Dispatcher.BeginInvoke(() => Safe(Commit));
        props.Children.Add(tb);
    }

    /// <summary>Operacja edytora z zabezpieczeniem: błąd cofa zmianę dokumentu zamiast zamykać program.</summary>
    void Safe(Action a)
    {
        var before = doc.Root.ToJsonString();
        try { a(); }
        catch (Exception ex)
        {
            if (ex is InvalidOperationException && ex.InnerException != null) Errors.Add("ODRZUCONA ZMIANA: " + ex.Message + " @ " + (ex.InnerException.StackTrace ?? "").Split((char)10).FirstOrDefault(l => l.Contains("LcsWidzew"))?.Trim());
            if (ex is not InvalidOperationException) { App.LogError(ex, "edytor"); Errors.Add(ex.GetType().Name + ": " + ex.Message + " @ " + (ex.StackTrace ?? "").Split((char)10).FirstOrDefault(l => l.Contains("LcsWidzew"))?.Trim()); }
            try { doc.Root = (System.Text.Json.Nodes.JsonObject)JsonNode.Parse(before); Rebuild(); ShowProps(); } catch { }
            status.Text = "✗ Operacja nie powiodła się: " + ex.Message + " (zmiana wycofana)";
        }
    }

    // ------------------------------------------------------------------ pliki, obrazy, posterunki
    bool SaveTo(string p)
    {
        var w = doc.Validate();
        var hard = w.Where(x => x.Contains("nieistniejący") || x.Contains("brak węzła")).ToList();
        if (hard.Count > 0) { MessageBox.Show(this, "Nie można zapisać – błędy modelu:\n\n" + string.Join("\n", hard.Take(20)), "Edytor"); return false; }
        try { doc.Build(); }
        catch (Exception ex) { MessageBox.Show(this, "Nie można zapisać – model nie wczytuje się: " + ex.Message, "Edytor"); return false; }
        // ochrona przed przypadkowym usunięciem dużej części układu
        if (!FuzzMode && File.Exists(p))
        {
            try
            {
                var old = StationDoc.Load(p);
                int dn = old.Nodes.Count - doc.Nodes.Count, ds = old.Signals.Count - doc.Signals.Count, db = old.Blocks.Count - doc.Blocks.Count;
                if ((dn > 30 || ds > 10 || db > 1) && MessageBox.Show(this, $"Zapis usunie z pliku {Math.Max(0, dn)} węzłów, {Math.Max(0, ds)} sygnalizatorów i {Math.Max(0, db)} szlaków.{(char)10}Czy na pewno zapisać?{(char)10}{(char)10}(Poprzednia wersja i tak trafi do kopii – przycisk „Przywróć kopię…”)", "Zapis układu", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return false;
            }
            catch { }
        }
        doc.Save(p); path = p; dirty = false;
        status.Text = $"Zapisano {p}{(w.Count > 0 ? $" – uwagi: {w.Count} (przycisk Sprawdź)" : "")}";
        return true;
    }

    void SaveAs()
    {
        var d = new Microsoft.Win32.SaveFileDialog { Filter = "Układ LCS (*.json)|*.json", FileName = Path.GetFileName(path), InitialDirectory = Path.GetDirectoryName(path) };
        if (d.ShowDialog(this) == true) SaveTo(d.FileName);
    }

    void OpenFile()
    {
        var d = new Microsoft.Win32.OpenFileDialog { Filter = "Układ LCS (*.json)|*.json", InitialDirectory = Path.GetDirectoryName(path) };
        if (d.ShowDialog(this) != true) return;
        try { var nd = StationDoc.Load(d.FileName); nd.Build(); doc = nd; path = d.FileName; dirty = false; selKind = null; Rebuild(true); ShowProps(); }
        catch (Exception ex) { MessageBox.Show(this, "Nie można wczytać pliku: " + ex.Message); }
    }

    /// <summary>Wczytanie jednej z automatycznych kopii (zapis dopiero po Ctrl+S).</summary>
    void RestoreBackup()
    {
        var dir = StationDoc.BackupDir(path);
        var files = (Directory.Exists(dir) ? Directory.GetFiles(dir, "*.json") : Array.Empty<string>()).Concat(File.Exists(path + ".bak") ? new[] { path + ".bak" } : Array.Empty<string>())
            .OrderByDescending(File.GetLastWriteTime).ToList();
        if (files.Count == 0) { MessageBox.Show(this, "Brak kopii zapasowych.", "Kopie"); return; }
        string Desc(string f) { try { var d = StationDoc.Load(f); return $"{File.GetLastWriteTime(f):yyyy-MM-dd HH:mm:ss}  –  węzły {d.Nodes.Count}, sygnalizatory {d.Signals.Count}, szlaki {d.Blocks.Count}  ({Path.GetFileName(f)})"; } catch { return Path.GetFileName(f) + " (uszkodzony)"; } }
        var r = Ask("Przywróć kopię układu", ("Kopia (wczytana do edytora – zapis Ctrl+S)", string.Join("|", files.Select(Desc))));
        if (r == null) return;
        var f2 = files[files.Select(Desc).ToList().IndexOf(r[0])];
        doc.Snapshot();
        var nd = StationDoc.Load(f2); nd.Build();
        doc.Root = nd.Root; dirty = true; ClearMulti(); selKind = null;
        Rebuild(true); ShowProps();
        status.Text = "Wczytano kopię " + Path.GetFileName(f2) + " – zapisz Ctrl+S, aby ją przywrócić (Ctrl+Z cofa)";
    }

    void NewArea()
    {
        var r = Ask("Nowy obszar", ("Skrót posterunku (np. KO)", "ST"), ("Nazwa posterunku", "Nowa stacja"), ("Plik", Path.Combine(Path.GetDirectoryName(path), "nowy_uklad.json")));
        if (r == null) return;
        doc = StationDoc.New(r[0].Trim().ToUpperInvariant(), r[1].Trim());
        path = r[2].Trim(); dirty = true; selKind = null;
        Rebuild(true); ShowProps();
        status.Text = "Nowy obszar – narysuj tory (T), wstaw zwrotnice (Z), semafory (S), kozły (K) i szlaki (B). Zapis: Ctrl+S.";
    }

    void NewPost()
    {
        var r = Ask("Nowy posterunek", ("Skrót (np. LN)", ""), ("Nazwa", ""));
        if (r == null || r[0].Trim() == "") return;
        doc.Snapshot();
        var code = r[0].Trim().ToUpperInvariant();
        doc.Posts[code] = r[1].Trim();
        int i = Math.Clamp(pageBox.SelectedIndex, 0, Math.Max(0, doc.Pages.Count - 1));
        if (doc.Pages.Count > 0) ((JsonArray)doc.Pages[i]["posts"]).Add(code);
        Changed("Dodano posterunek " + code);
        postBox.SelectedItem = code;
    }

    void NewPage()
    {
        var r = Ask("Nowy obraz (strona pulpitu)", ("Skrót", ""), ("Nazwa", ""), ("Posterunki na obrazie (oddzielone przecinkiem)", Post));
        if (r == null || r[0].Trim() == "") return;
        double x0 = doc.Pages.Count == 0 ? 0 : doc.Pages.Max(p => StationDoc.D(p["x1"]));
        var posts = new JsonArray(); foreach (var p in r[2].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) posts.Add(p.ToUpperInvariant());
        doc.Snapshot();
        doc.Pages.Add(new JsonObject { ["id"] = r[0].Trim().ToUpperInvariant(), ["name"] = r[1].Trim(), ["x0"] = x0, ["x1"] = x0 + 2000, ["posts"] = posts });
        foreach (var p in posts) if (!doc.Posts.ContainsKey(StationDoc.Sv(p))) doc.Posts[StationDoc.Sv(p)] = r[1].Trim();
        Changed("Dodano obraz " + r[1]);
        pageBox.SelectedIndex = doc.Pages.Count - 1; panel.FitPage();
    }

    // ------------------------------------------------------------------ kontrola układu: lista problemów i znaczniki na rysunku
    List<StationDoc.Problem> problems = new();
    int probSel = -1;
    Window probWin; ListBox probList; TextBlock probHead;

    void ShowValidation()
    {
        problems = doc.Problems(); probSel = -1;
        if (problems.Count == 0) { probWin?.Close(); panel.InvalidateVisual(); MessageBox.Show(this, "Nie znaleziono błędów.", "Kontrola układu"); return; }
        if (probWin == null)
        {
            probHead = new TextBlock { Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, Margin = new Thickness(8, 6, 8, 4), TextWrapping = TextWrapping.Wrap };
            probList = new ListBox { Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x14)), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            ScrollViewer.SetHorizontalScrollBarVisibility(probList, ScrollBarVisibility.Disabled);
            probList.SelectionChanged += (a, e) => { if (probList.SelectedIndex >= 0) GoToProblem(probList.SelectedIndex); };
            var dock = new DockPanel(); DockPanel.SetDock(probHead, Dock.Top); dock.Children.Add(probHead); dock.Children.Add(probList);
            probWin = new Window { Title = "Kontrola układu", Width = 460, Height = 420, Owner = this, Content = dock, Background = new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x12)), ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
            probWin.Left = Left + Width - 480; probWin.Top = Top + 120;
            probWin.Closed += (a, e) => { probWin = null; problems.Clear(); probSel = -1; panel.InvalidateVisual(); };
            probWin.Show();
        }
        FillProblems();
        panel.InvalidateVisual();
    }

    void FillProblems()
    {
        if (probWin == null) return;
        probHead.Text = problems.Count == 0 ? "Brak problemów – wszystko poprawione." : $"Problemy: {problems.Count}. Klik – przejście do miejsca (czerwone kółko z numerem na rysunku). Lista odświeża się po każdej zmianie.";
        probList.Items.Clear();
        for (int i = 0; i < problems.Count; i++)
            probList.Items.Add(new TextBlock { Text = $"{i + 1}. {problems[i].Msg}{(problems[i].Refs.Count == 0 ? "  (bez miejsca na rysunku)" : "")}", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 3, 2, 3), Foreground = problems[i].Refs.Count == 0 ? Brushes.Gray : Brushes.White });
    }

    /// <summary>Po zmianie w dokumencie – odświeżenie listy problemów (poprawione znikają).</summary>
    void RefreshProblems()
    {
        if (probWin == null) return;
        var sel = probSel >= 0 && probSel < problems.Count ? problems[probSel].Msg : null;
        problems = doc.Problems();
        probSel = sel == null ? -1 : problems.FindIndex(p => p.Msg == sel);
        FillProblems();
    }

    void GoToProblem(int i)
    {
        if (i < 0 || i >= problems.Count) return;
        probSel = i;
        var pos = problems[i].Refs.Select(doc.RefPos).FirstOrDefault(p => p != null);
        if (pos is { } p)
        {
            int pi = st.Pages.FindIndex(pg => p.X >= pg.X0 && p.X < pg.X1);
            if (pi >= 0 && pageBox.SelectedIndex != pi) pageBox.SelectedIndex = pi;
            Dispatcher.BeginInvoke(() => { if (panel.Scale < 1.2) panel.SetZoom(1.6); panel.CenterOn(p.X, p.Y); }, System.Windows.Threading.DispatcherPriority.Render);
            status.Text = $"Problem {i + 1}: {problems[i].Msg}";
        }
        else status.Text = $"Problem {i + 1} nie ma miejsca na rysunku: {problems[i].Msg}";
        panel.InvalidateVisual();
    }

    void DrawProblems(DrawingContext dc, double scale)
    {
        if (problems.Count == 0) return;
        var red = new SolidColorBrush(Color.FromRgb(0xff, 0x30, 0x30));
        for (int i = 0; i < problems.Count; i++)
            foreach (var r in problems[i].Refs)
            {
                if (doc.RefPos(r) is not { } p || !OnPage(p.X)) continue;
                bool sel = i == probSel;
                double rad = (sel ? 22 : 15) / Math.Max(0.4, scale) * 0.7;
                dc.DrawEllipse(null, new Pen(sel ? Brushes.Orange : red, (sel ? 4 : 2.5) / scale), new Point(p.X, p.Y), rad, rad);
                var ft = Ft((i + 1).ToString(), Math.Clamp(11 / scale, 6, 30), Brushes.White);
                var bp = new Point(p.X + rad * 0.7, p.Y - rad * 0.7 - ft.Height);
                dc.DrawRoundedRectangle(sel ? Brushes.Orange : red, null, new Rect(bp.X - 2 / scale, bp.Y, ft.Width + 4 / scale, ft.Height), 2 / scale, 2 / scale);
                dc.DrawText(ft, bp);
            }
    }

    // ------------------------------------------------------------------ okno pytań
    /// <summary>Proste okno z polami; wartość domyślna zawierająca „|” tworzy listę wyboru.</summary>
    public Queue<string[]> AutoAnswers = new();
    public List<string> Errors = new();
    public bool FuzzMode;

    /// <summary>Test losowy (--editshot plik fuzz N): losowe operacje narzędziami, edycja pól, cofanie, rysowanie.</summary>
    public string Fuzz(int n, int seed)
    {
        var rnd = new Random(seed); var log = new System.Text.StringBuilder(); FuzzMode = true;
        var tools = Enum.GetValues<Tool>();
        var types = new[] { "wyjazdowy", "wjazdowy", "tm", "to", "sp" };
        Point P() { var pg = panel.Page; return new Point(pg.X0 + rnd.NextDouble() * (pg.X1 - pg.X0), 150 + rnd.NextDouble() * 1000); }
        Point Near() { var ns = st.Nodes.Values.Where(x => OnPage(x.X)).ToList(); if (ns.Count == 0 || rnd.Next(4) == 0) return P(); var q = ns[rnd.Next(ns.Count)]; return new Point(q.X + rnd.Next(-3, 4), q.Y + rnd.Next(-3, 4)); }
        for (int i = 0; i < n; i++)
        {
            int op = rnd.Next(23);
            chkSecs.IsChecked = rnd.Next(2) == 0; chkLens.IsChecked = rnd.Next(2) == 0; chkGrid.IsChecked = rnd.Next(4) != 0;
            try
            {
                switch (op)
                {
                    case 0: case 1: case 2:
                    {
                        var t = tools[rnd.Next(tools.Length)]; SetTool(t);
                        AutoAnswers.Clear();
                        AutoAnswers.Enqueue(new[] { "X" + rnd.Next(99), types[rnd.Next(types.Length)], rnd.Next(2) == 0 ? "→ na wschód (w prawo)" : "← na zachód (w lewo)", rnd.Next(2) == 0 ? "" : "A", rnd.Next(2) == 0 ? "zewnętrzny" : "wewnętrzny (oba końce w LCS)", rnd.Next(2) == 0 ? "samoczynna (SBL)" : "liniowa bez odstępów", "neutralny" });
                        var a = Near(); Down(a, null); Move(new Point(a.X + rnd.Next(-60, 60), a.Y + rnd.Next(-40, 40)), null); Up(Near(), null);
                        if (rnd.Next(3) == 0) { var b = Near(); Down(b, null); Up(b, null); }
                        if (rnd.Next(3) == 0) Right(P());
                        break;
                    }
                    case 3: SetTool(Tool.Select); { var a = Near(); Down(a, null); var b = new Point(a.X + rnd.Next(-80, 80), a.Y + rnd.Next(-50, 50)); dragging = dragging; Move(b, null); Up(b, null); } break;
                    case 4: Safe(DeleteSel); break;
                    case 5: Safe(DoUndo); break;
                    case 6: Safe(DoRedo); break;
                    case 7:
                    {
                        var boxes = props.Children.OfType<TextBox>().ToList();
                        if (boxes.Count == 0) break;
                        var tb = boxes[rnd.Next(boxes.Count)];
                        tb.Text = rnd.Next(5) switch { 0 => "", 1 => rnd.Next(-50, 2000).ToString(), 2 => "LW:Q" + rnd.Next(9), 3 => "abc", _ => (rnd.NextDouble() * 100).ToString("0.0", CultureInfo.InvariantCulture) };
                        tb.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this) ?? PresentationSource.CurrentSources.Cast<PresentationSource>().First(), 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent });
                        break;
                    }
                    case 8:
                    {
                        var btns = props.Children.OfType<Button>().Where(b => !(b.Content as string ?? "").StartsWith("Usuń")).ToList();
                        if (btns.Count > 0) { AutoAnswers.Enqueue(new[] { rnd.Next(1, 9).ToString() }); btns[rnd.Next(btns.Count)].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); }
                        break;
                    }
                    case 9: if (pageBox.Items.Count > 0) pageBox.SelectedIndex = rnd.Next(pageBox.Items.Count); break;
                    case 10: if (blockBox.Items.Count > 0) blockBox.SelectedIndex = rnd.Next(blockBox.Items.Count); break;
                    case 11:
                    {
                        var rows = props.Children.OfType<DockPanel>().ToList();
                        if (rows.Count > 0) { var cb = rows[rnd.Next(rows.Count)].Children.OfType<CheckBox>().FirstOrDefault(); cb?.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); }
                        break;
                    }
                    case 12: { var sigs = st.Signals.Keys.ToList(); if (sigs.Count > 0) Select("sig", sigs[rnd.Next(sigs.Count)]); break; }
                    case 13: { var eds = st.Edges.Keys.ToList(); if (eds.Count > 0) Select("edge", eds[rnd.Next(eds.Count)]); break; }
                    case 14: { var a = Near(); SelectRect(new Rect(a, new Point(a.X + rnd.Next(20, 300), a.Y + rnd.Next(20, 200)))); break; }
                    case 15: if (selKind == "multi") Safe(() => { doc.Snapshot(); GroupMove(rnd.Next(-3, 4) * Grid, rnd.Next(-3, 4) * Grid); Changed("grupa"); selKind = "multi"; }); break;
                    case 16: if (selKind == "multi") Safe(rnd.Next(2) == 0 ? AlignY : DistributeX); break;
                    case 17: if (selKind == "multi" && rnd.Next(3) == 0) Safe(DeleteSel); break;
                    case 22: { SetTool(Tool.Turnout); var a = Near(); Down(a, null); Up(a, null); var b = rnd.Next(2) == 0 ? Near() : P(); Down(b, null); Up(b, null); break; }
                    case 20: { var eds = st.Edges.Keys.ToList(); if (eds.Count > 0) { SelectTrack(eds[rnd.Next(eds.Count)]); if (rnd.Next(2) == 0) Safe(() => { doc.Snapshot(); GroupMove(0, rnd.Next(-3, 4) * Grid); Changed("tor"); selKind = "multi"; }); } break; }
                    case 21: if (selKind == "multi") Safe(() => Respace(rnd.Next(2, 12) * Grid)); break;
                    case 19: { var pn = doc.Nodes.Select(x => x.Key).Where(doc.IsPlainNode).ToList(); if (pn.Count > 0) { Select("node", pn[rnd.Next(pn.Count)]); Safe(DeleteSel); } break; }
                    case 18: Safe(() => Find(new[] { "S1", "13", "2A", "JT4", "tor 2", "XX" }[rnd.Next(6)])); break;
                }
            }
            catch (Exception ex) { Errors.Add("NIEPRZECHWYCONY " + ex.GetType().Name + ": " + ex.Message + " @ " + (ex.StackTrace ?? "").Split((char)10).FirstOrDefault(l => l.Contains("LcsWidzew"))?.Trim()); }
            if (i % 5 == 0)
            {
                panel.InvalidateVisual(); panel.UpdateLayout();
                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(400, 300, 96, 96, PixelFormats.Pbgra32); rtb.Render(panel);
                if (panel.RenderError != null) Errors.Add("RYSOWANIE: " + panel.RenderError);
                try { var x = doc.Build(); new Simulator(x).ToString(); } catch (Exception ex) { Errors.Add("MODEL: " + ex.Message); }
            }
        }
        foreach (var g in Errors.GroupBy(x => x)) log.AppendLine($"{g.Count(),4} × {g.Key}");
        log.AppendLine($"Koniec: węzły {doc.Nodes.Count}, tory {doc.Edges.Count}, błędów {Errors.Count}");
        return log.ToString();
    }
    string[] Ask(string title, params (string Label, string Def)[] fields)
    {
        if (AutoAnswers.Count > 0) return AutoAnswers.Dequeue();
        if (FuzzMode) return null;
        var w = new DarkWindow2(title, 420, 140 + fields.Length * 52) { Owner = this };
        var sp = new StackPanel { Margin = new Thickness(14) };
        var getters = new List<Func<string>>();
        foreach (var (l, d) in fields)
        {
            sp.Children.Add(T(l, 11, Brushes.Gray));
            if (d.Contains('|')) { var cb = new ComboBox { ItemsSource = d.Split('|'), SelectedIndex = 0, Margin = new Thickness(0, 2, 0, 4) }; sp.Children.Add(cb); getters.Add(() => (string)cb.SelectedItem); }
            else { var tb = Box(d); sp.Children.Add(tb); getters.Add(() => tb.Text); }
        }
        bool ok = false;
        var bp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var bOk = Btn("OK", (a, e) => { ok = true; w.Close(); }); bOk.IsDefault = true;
        var bCancel = Btn("Anuluj", (a, e) => w.Close()); bCancel.IsCancel = true;
        bp.Children.Add(bOk); bp.Children.Add(bCancel); sp.Children.Add(bp);
        w.Content = sp;
        w.Loaded += (a, e) => { var first = sp.Children.OfType<Control>().FirstOrDefault(c => c is TextBox or ComboBox); first?.Focus(); if (first is TextBox t) t.SelectAll(); };
        w.ShowDialog();
        return ok ? getters.Select(g => g()).ToArray() : null;
    }

    class DarkWindow2 : DarkWindow { public DarkWindow2(string t, double w, double h) : base(t, w, h) { ResizeMode = ResizeMode.NoResize; } }
}

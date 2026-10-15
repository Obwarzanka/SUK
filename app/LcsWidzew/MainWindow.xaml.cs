using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LcsWidzew.Model;
using LcsWidzew.Sim;
using LcsWidzew.UI;

namespace LcsWidzew;

public partial class MainWindow : Window
{
    Station station;
    Simulator sim;
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    DateTime last = DateTime.Now;
    double speed = 2, acc;
    bool paused = true;
    string scenario = "podstawowy";
    readonly ObservableCollection<EventRecord> events = new();
    readonly ObservableCollection<EventRecord> alarms = new();
    readonly List<string> history = new(); int histIdx;
    SpecialConfirmWindow confirmWin;
    double lastBeep;

    public MainWindow()
    {
        InitializeComponent();
        station = Station.Load(App.StationPath);
        GridEvents.ItemsSource = events;
        GridAlarms.ItemsSource = alarms;
        EvFilter.ItemsSource = new[] { "wszystkie", "Polecenie", "Odrzucone", "Zdarzenie", "Alarm", "Ruch", "Uwaga", "Ocena", "Instruktor", "E-1758" };
        EvFilter.SelectedIndex = 0;
        Panel.SignalClicked += OnSignal; Panel.SwitchClicked += OnSwitch; Panel.SectionClicked += OnSection;
        Panel.BlockClicked += OnBlock; Panel.TrackEndClicked += OnTrackEnd; Panel.DerailerClicked += OnDerailer;
        Panel.LinkClicked += (pg, e) => ShowPage(station.Pages.FindIndex(p => p.Id == pg));
        Panel.Hover += t => Hint.Text = t;
        Panel.OverviewClicked += pid => ShowPage(station.Pages.FindIndex(p => p.Id == pid));
        KeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape) { ClearSel(); }
            if (e.Key == Key.F1) Help_Click(null, null);
            if (e.Key == Key.F2) ShowPage(0);
            if (e.Key == Key.F3) ShowPage(1);
        };
        BuildTabs();
        Loaded += (s, e) => { Start(scenario, null); Dispatcher.BeginInvoke(() => { ShowPage(station.Pages.Count - 1); if (ShotPath == null) ShowLogin(); else Demo(); }, DispatcherPriority.Loaded); };
        SizeChanged += (s, e) => Panel.FitPage();
        timer.Tick += OnTimer;
        timer.Start();
    }

    void BuildTabs()
    {
        PageTabs.Children.Clear();
        var ov = new Button { Content = "0  Obraz poglądowy", Tag = "OV", Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 6, 0) };
        ov.Click += (s, e) => ShowOverview();
        PageTabs.Children.Add(ov);
        int i = 1;
        foreach (var pg in station.Pages)
        {
            var b = new Button { Content = $"{i++}  {pg.Name}", Tag = pg.Id, Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 2, 0) };
            b.Click += (s, e) => ShowPage(station.Pages.FindIndex(p => p.Id == (string)((Button)s).Tag));
            PageTabs.Children.Add(b);
        }
    }

    EditorWindow editor;
    void Editor_Click(object sender, RoutedEventArgs e)
    {
        if (editor != null) { editor.Activate(); return; }
        paused = true;
        editor = new EditorWindow(App.StationPath, ReloadStation) { Saved = p => ReloadStation(p, true) };
        editor.Closed += (a, b) => editor = null;
        editor.Show();
    }

    /// <summary>Wczytanie zmienionego układu (z edytora) i ponowny start symulacji.</summary>
    public void ReloadStation(string path) => ReloadStation(path, false);

    public void ReloadStation(string path, bool quiet)
    {
        try
        {
            var st = Station.Load(path);
            var keep = Panel.Page?.Id;
            station = st; App.StationPath = path;
            BuildTabs();
            Start(scenario, sim?.User);
            int pi = quiet ? station.Pages.FindIndex(p => p.Id == keep) : -1;
            ShowPage(pi >= 0 ? pi : station.Pages.Count - 1);
            sim.Log("Zdarzenie", "Wczytano układ z edytora: " + Path.GetFileName(path), "System", "");
            if (!quiet) Activate();
        }
        catch (Exception ex) { MessageBox.Show(this, "Nie można wczytać układu: " + ex.Message, "Edytor układu"); }
    }

    /// <summary>Obraz poglądowy całego obszaru LCS – wszystkie obrazy obok siebie, klik przenosi do obrazu szczegółowego.</summary>
    void ShowOverview()
    {
        if (station.Pages.Count == 0) return;
        Panel.Overview = true;
        Panel.Page = new PageDef { Id = "OV", Name = "Obraz poglądowy LCS", X0 = station.Pages.Min(p => p.X0), X1 = station.Pages.Max(p => p.X1) };
        ClearSel();
        foreach (Button b in PageTabs.Children)
        {
            bool on = (string)b.Tag == "OV";
            b.Background = on ? new SolidColorBrush(Color.FromRgb(0x0a, 0x4a, 0x66)) : new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26));
            b.BorderBrush = on ? new SolidColorBrush(Color.FromRgb(0x00, 0xa0, 0xff)) : new SolidColorBrush(Color.FromRgb(0x4a, 0x4a, 0x4a));
        }
        Hint.Text = "Obraz poglądowy – klik na obszarze stacji otwiera jej obraz szczegółowy";
        Dispatcher.BeginInvoke(() => Panel.FitPage(), DispatcherPriority.Render);
    }

    void ShowPage(int idx)
    {
        if (idx < 0 || idx >= station.Pages.Count) return;
        var pg = station.Pages[idx];
        Panel.Overview = false;
        Panel.Page = pg; if (sim != null) sim.Page = pg.Id;
        ClearSel();
        foreach (Button b in PageTabs.Children)
        {
            bool on = (string)b.Tag == pg.Id;
            b.Background = on ? new SolidColorBrush(Color.FromRgb(0x0a, 0x4a, 0x66)) : new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26));
            b.BorderBrush = on ? new SolidColorBrush(Color.FromRgb(0x00, 0xa0, 0xff)) : new SolidColorBrush(Color.FromRgb(0x4a, 0x4a, 0x4a));
        }
        Dispatcher.BeginInvoke(() => Panel.FitPage(), DispatcherPriority.Render);
    }

    void ClearSel() { Panel.SelStart = null; Panel.SelSwitch = null; Panel.SelVia.Clear(); Panel.InvalidateVisual(); }

    // ------------------------------------------------------------------ tryb zrzutu (testy wyglądu)
    public string ShotPath; public string[] ShotCmds = Array.Empty<string>(); public double ShotSeconds = 60;
    void Demo()
    {
        if (ShotCmds.FirstOrDefault()?.StartsWith("#scen=") == true) { scenario = ShotCmds[0][6..]; ShotCmds = ShotCmds.Skip(1).ToArray(); }
        Start(scenario, new User { Name = "Test", Role = "dyżurny ruchu" });
        paused = true;
        foreach (var c in ShotCmds)
        {
            if (c.StartsWith("@")) { var until = double.Parse(c[1..], System.Globalization.CultureInfo.InvariantCulture); while (sim.T < until) sim.Tick(0.1); continue; }
            if (c == "#overview") { ShowOverview(); continue; }
            if (c.StartsWith("#fault=")) { var f = c[7..].Split(':'); sim.Trains.ApplyFault(f[0], f.Length > 1 ? f[1] : "", false); continue; }
            if (c.StartsWith("#page=")) { ShowPage(int.Parse(c[6..])); continue; }
            Run(c);
        }
        while (sim.T < ShotSeconds) sim.Tick(0.1);
        UpdateTop(); Panel.InvalidateVisual();
        Dispatcher.BeginInvoke(() =>
        {
            Panel.FitPage(); UpdateLayout();
            Dispatcher.BeginInvoke(() =>
            {
                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(this);
                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder(); enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                using (var f = File.Create(ShotPath)) enc.Save(f);
                Application.Current.Shutdown();
            }, DispatcherPriority.ApplicationIdle);
        }, DispatcherPriority.ApplicationIdle);
    }

    // ------------------------------------------------------------------ start / logowanie
    void Start(string sc, User user)
    {
        scenario = sc;
        sim = new Simulator(station);
        if (Panel.Page != null) sim.Page = Panel.Page.Id;
        var tr = new Trains(sim);
        tr.Load(sc);
        sim.User = user;
        events.Clear(); alarms.Clear();
        sim.EventAdded += e => { if (Matches(e)) events.Insert(0, e); if (events.Count > 3000) events.RemoveAt(events.Count - 1); };
        sim.AlarmAdded += a => { alarms.Insert(0, a); SortAlarms(); };
        sim.Beep += () => { if (ChkMute.IsChecked != true) System.Media.SystemSounds.Asterisk.Play(); };
        sim.SpecialExecuted += line => Dispatcher.BeginInvoke(() => new BookWindow(sim) { Owner = this }.Show());
        sim.PendingExpired += () => confirmWin?.Close();
        sim.Log("Zdarzenie", "Start symulacji – scenariusz: " + Trains.Scenarios.First(s => s.Key == sc).Name + (user != null ? ", zalogowany: " + user.Name : ""), "System", "");
        Panel.Sim = sim; ClearSel();
        UpdateTop();
    }

    /// <summary>Kolejność na liście alarmów: niepotwierdzone, trwające potwierdzone, nietrwające potwierdzone (Ie-104 § 7 ust. 5).</summary>
    void SortAlarms()
    {
        var sorted = alarms.OrderBy(a => a.Ack ? (a.Active ? 1 : 2) : 0).ThenByDescending(a => a.T).ToList();
        for (int i = 0; i < sorted.Count; i++) { int j = alarms.IndexOf(sorted[i]); if (j != i) alarms.Move(j, i); }
    }

    void ShowLogin()
    {
        var w = new LoginWindow(sim.User != null) { Owner = this };
        if (w.ShowDialog() == true)
        {
            if (w.Restart || sim.User == null && events.Count < 3) Start(w.Scenario, new User { Name = w.UserName, Role = w.Role });
            else { sim.User = new User { Name = w.UserName, Role = w.Role }; sim.Log("Zdarzenie", "Zalogowano: " + w.UserName, "Użytkownik", w.UserName); }
            paused = false; Cmd.Focus();
        }
        UpdateTop();
    }

    void BtnLogin_Click(object sender, RoutedEventArgs e)
    {
        if (sim.User != null && MessageBox.Show(this, "Wylogować użytkownika " + sim.User.Name + "?\n(Ie-20 §7 ust. 2 – wylogowanie przy oddaleniu się od stanowiska)", "Logowanie", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            sim.Log("Zdarzenie", "Wylogowano: " + sim.User.Name, "Użytkownik", sim.User.Name); sim.User = null; UpdateTop(); return;
        }
        ShowLogin();
    }

    // ------------------------------------------------------------------ pętla czasu
    void OnTimer(object s, EventArgs e)
    {
        var now = DateTime.Now; var real = Math.Min(0.25, (now - last).TotalSeconds); last = now;
        if (!paused && speed > 0)
        {
            acc += real * speed;
            int steps = 0;
            while (acc >= 0.1 && steps < 120) { sim.Tick(0.1); acc -= 0.1; steps++; }
        }
        Panel.InvalidateVisual();
        UpdateTop();
        // dźwięk co kilka sekund, dopóki istnieje niepotwierdzony alarm; głośniejszy po minucie (Ie-104 § 7 ust. 6)
        var un = sim.Alarms.Where(a => !a.Ack).ToList();
        if (un.Count > 0 && ChkMute.IsChecked != true && (now - DateTime.MinValue).TotalSeconds - lastBeep > 4)
        {
            lastBeep = (now - DateTime.MinValue).TotalSeconds;
            if (un.Any(a => sim.T - a.T > 60)) System.Media.SystemSounds.Hand.Play(); else System.Media.SystemSounds.Asterisk.Play();
        }
    }

    void UpdateTop()
    {
        if (sim == null) return;
        TxtClock.Text = sim.Clock();
        Life.Fill = (Environment.TickCount64 / 500) % 2 == 0 ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
        void Lamp(Border b, bool on, bool warn) { b.Background = on ? (warn ? new SolidColorBrush(Color.FromRgb(0xff, 0xc0, 0)) : new SolidColorBrush(Color.FromRgb(0x3d, 0xdc, 0x84))) : Brushes.Transparent; ((TextBlock)b.Child).Foreground = on ? Brushes.Black : Brushes.Gray; }
        Lamp(LampMains, sim.Power.Mains, false); Lamp(LampUps, !sim.Power.Mains && sim.Power.Ups, true); Lamp(LampGen, sim.Power.Gen, true); Lamp(LampNN, sim.Power.NN, false);
        TxtLogged.Text = sim.User == null ? "Zalogowany: – (brak możliwości wydawania poleceń)" : $"Zalogowany: {sim.User.Name}";
        TxtRole.Text = sim.User == null ? "Brak obsługi" : char.ToUpper(sim.User.Role[0]) + sim.User.Role[1..];
        ChipRole.Background = sim.User == null ? new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)) : new SolidColorBrush(Color.FromRgb(0x2f, 0x7d, 0x32));
        BtnLogin.Content = sim.User == null ? "Logowanie" : "Wyloguj";
        TxtPaused.Visibility = paused || speed == 0 ? Visibility.Visible : Visibility.Collapsed;
        var n = sim.Alarms.Count(a => !a.Ack);
        AlCount.Text = n > 0 ? $"Niepotwierdzone: {n}" : "";
        EvCount.Text = $"{sim.Events.Count} zdarzeń";
        if (sim.Pending != null && confirmWin == null)
        {
            confirmWin = new SpecialConfirmWindow(sim) { Owner = this };
            confirmWin.Closed += (a, b) => { confirmWin = null; Panel.InvalidateVisual(); };
            confirmWin.Show();
        }
    }

    // ------------------------------------------------------------------ polecenia
    public CmdResult Run(string line)
    {
        var r = sim.Exec(line);
        if (r.Msg != "")
        {
            CmdRes.Text = (r.Ok ? "✓ " : "✗ ") + line + (r.Msg != "" ? " – " + r.Msg : "");
            CmdRes.Foreground = r.Ok ? new SolidColorBrush(Color.FromRgb(0x3d, 0xdc, 0x84)) : new SolidColorBrush(Color.FromRgb(0xff, 0x5a, 0x5a));
        }
        Panel.InvalidateVisual();
        return r;
    }

    void Cmd_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { var v = Cmd.Text; if (v.Trim() != "") { history.Add(v); histIdx = history.Count; Run(v); } Cmd.Text = ""; }
        else if (e.Key == Key.Up && histIdx > 0) { histIdx--; Cmd.Text = history[histIdx]; Cmd.CaretIndex = Cmd.Text.Length; }
        else if (e.Key == Key.Down && histIdx < history.Count) { histIdx++; Cmd.Text = histIdx < history.Count ? history[histIdx] : ""; }
    }

    static string L(string key) => Station.Lbl(key);
    bool Ctrl => Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

    // ------------------------------------------------------------------ mysz na pulpicie
    void OnSignal(string name, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Right) { SignalMenu(name); return; }
        if (Panel.SelStart != null && Ctrl) { ToggleVia(name); return; }
        if (Panel.SelStart == null)
        {
            if (!sim.RouteTable.TryGetValue(name, out var l) || l.Count == 0) { Hint.Text = "Brak przebiegów od " + L(name); return; }
            Panel.SelStart = name; Panel.SelVia.Clear();
            Hint.Text = $"Początek: {L(name)} – wskaż koniec przebiegu (sygnalizator, strzałkę blokady szlaku lub znacznik końca). Ctrl+klik – punkt pośredni (przebieg wariantowy). Esc – rezygnacja.";
        }
        else if (Panel.SelStart == name) ClearSel();
        else Dest(name);
        Panel.InvalidateVisual();
    }

    void ToggleVia(string key)
    {
        if (Panel.SelVia.Contains(key)) Panel.SelVia.Remove(key); else Panel.SelVia.Add(key);
        Hint.Text = $"Początek: {L(Panel.SelStart)}, punkty pośrednie: {string.Join(", ", Panel.SelVia.Select(L))} – wskaż koniec przebiegu.";
        Panel.InvalidateVisual();
    }

    /// <summary>Koniec przebiegu – menu z przebiegiem zasadniczym i wariantowymi (Ie-104 § 5 ust. 4).</summary>
    void Dest(string end)
    {
        var start = Panel.SelStart;
        var all = sim.RouteTable[start].Where(r => r.End == end).ToList();
        var list = all.Where(r => Panel.SelVia.All(v => sim.Passes(r, v))).ToList();
        string Cmdl(RouteDef r) => $"{L(start)} {string.Join(" ", ViaFor(r, all).Select(L))}{(ViaFor(r, all).Count > 0 ? " " : "")}{L(end)} {(r.Type == "P" ? "Pociąg" : "Manewr")}";
        var m = NewMenu($"Przebieg {L(start)} → {L(end)}");
        if (list.Count == 0)
        {
            if (all.Count == 0 && sim.RouteTable[start].Any(r => r.Type == "M"))
                Add(m, $"Przebieg manewrowy złożony {L(start)} → {L(end)}", $"{L(start)} {L(end)} Manewr");
            else { Hint.Text = $"Brak przebiegu {L(start)} → {L(end)}{(Panel.SelVia.Count > 0 ? " przez wskazane punkty" : "")}"; return; }
        }
        foreach (var r in list)
        {
            var kind = r.Type == "P" ? "pociągowy" : "manewrowy";
            var var_ = r.Variant == 0 ? "zasadniczy" : $"wariantowy {r.Variant}";
            Add(m, $"Przebieg {kind} {var_}   [{r.SwText}]", Cmdl(r));
        }
        m.Closed += (a, b) => ClearSel();
        m.IsOpen = true;
    }

    /// <summary>Punkty pośrednie jednoznacznie wskazujące wariant (zwrotnica w położeniu odróżniającym od zasadniczego).</summary>
    List<string> ViaFor(RouteDef r, List<RouteDef> all)
    {
        if (r.Variant == 0) return new();
        var basic = all.FirstOrDefault(x => x.Variant == 0 && x.Type == r.Type);
        var diff = r.Sw.Where(q => basic == null || !basic.Sw.Contains(q)).ToList();
        foreach (var q in diff)
            if (all.Where(x => x.Type == r.Type && sim.Passes(x, q.Num + q.Pos)).OrderBy(x => x.Variant).First() == r) return new() { q.Num + q.Pos };
        return diff.Select(q => q.Num + q.Pos).ToList();
    }

    void SignalMenu(string name)
    {
        var s = sim.Sigs[name]; var d = station.Signals[name];
        var m = NewMenu((d.Type == "tm" ? "Tarcza " : "Semafor ") + L(name));
        Add(m, "Wybierz jako początek przebiegu", null, () => { Panel.SelStart = name; Panel.SelVia.Clear(); Panel.InvalidateVisual(); });
        if (s.Aspect is Aspect.Train or Aspect.Shunt) Add(m, "Sygnał „Stój”", $"{L(name)} Stój");
        var r = sim.Routes.Values.FirstOrDefault(x => x.Start == name);
        if (r != null && r.Type == "P") { Add(m, "Zwolnienie przebiegu ze zwłoką (ZCZ)", $"{L(name)} ZCZ"); if (r.TimeRelUntil != null) Add(m, "Przerwanie odliczania (oZCZ)", $"{L(name)} oZCZ"); }
        if (r != null && r.Type == "M") Add(m, "Zwolnienie przebiegu manewrowego (ZDM)", $"{L(name)} ZDM");
        Add(m, s.Closed ? "Odwołanie zastopowania (oStop)" : "Zastopowanie sygnalizatora (Stop)", $"{L(name)} {(s.Closed ? "oStop" : "Stop")}");
        if (s.Aspect == Aspect.Sz) Add(m, "Odwołanie sygnału zastępczego (oSZ)", $"{L(name)} oSZ");
        if (d.IsTrain)
        {
            Sep(m, "Polecenia specjalne");
            Add(m, "Sygnał zastępczy (SZ)", $"{L(name)} SZ", special: true);
            Add(m, "Zwolnienie doraźne natychmiastowe (ZDP)", $"{L(name)} ZDP", special: true, enabled: r != null && r.Type == "P");
        }
        m.IsOpen = true;
    }

    void OnSwitch(string num, MouseButtonEventArgs e)
    {
        if (Panel.SelStart != null && Ctrl) { var w0 = sim.Sws[num]; ToggleVia(num); return; }
        var w = sim.Sws[num]; Panel.SelSwitch = num; Panel.InvalidateVisual();
        var lb = station.Switches[num].Label;
        var m = NewMenu($"Zwrotnica {lb} (położenie {w.Pos})");
        Add(m, "Przestawienie do „+” (Plus)", $"{lb} Plus", enabled: w.Pos != "+" || w.Moving != null);
        Add(m, "Przestawienie do „−” (Minus)", $"{lb} Minus", enabled: w.Pos != "-" || w.Moving != null);
        Add(m, w.Stopped ? "Odwołanie zastopowania (oStop)" : "Zastopowanie (Stop)", $"{lb} {(w.Stopped ? "oStop" : "Stop")}");
        var sec = station.Switches[num].Sec;
        Add(m, sim.Secs[sec].Closed ? "Odwołanie zamknięcia ruchowego (oZmk)" : "Zamknięcie ruchowe (Zmk)", $"{lb} {(sim.Secs[sec].Closed ? "oZmk" : "Zmk")}");
        Sep(m, "Polecenia specjalne");
        Add(m, "Przestawienie „+” z bocznikowaniem (PlusBZ)", $"{lb} PlusBZ", special: true);
        Add(m, "Przestawienie „−” z bocznikowaniem (MinBZ)", $"{lb} MinBZ", special: true);
        Add(m, "Kasowanie sygnalizacji rozprucia (KSR)", $"{lb} KSR", special: true, enabled: w.Rozpr);
        Add(m, "Zerowanie licznika osi (ZeroLO)", $"{lb} ZeroLO", special: true, enabled: sim.Secs[sec].Fault != null);
        m.Closed += (a, b) => { Panel.SelSwitch = null; };
        m.IsOpen = true;
    }

    void OnSection(string sec, MouseButtonEventArgs e)
    {
        if (Panel.SelStart != null && Ctrl) { ToggleVia(sec); return; }
        if (e.ChangedButton != MouseButton.Right) return;
        var s = sim.Secs[sec]; var m = NewMenu("Odcinek " + L(sec));
        Add(m, s.Closed ? "Odwołanie zamknięcia ruchowego (oZmk)" : "Zamknięcie ruchowe toru (Zmk)", $"{L(sec)} {(s.Closed ? "oZmk" : "Zmk")}");
        Sep(m, "Polecenia specjalne");
        Add(m, "Zerowanie licznika osi (ZeroLO)", $"{L(sec)} ZeroLO", special: true, enabled: s.Fault is "reset" or "occ");
        m.IsOpen = true;
    }

    void OnBlock(string id, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && Panel.SelStart != null) { Dest(id); return; }
        var b = station.Blocks[id]; var bs = sim.Blocks[id]; int ei = sim.EndIdx(b, sim.Page);
        if (!b.Permission)
        {
            // jednokierunkowa / telefoniczne zapowiadanie – bez bloku pozwolenia
            var mj = NewMenu($"Szlak {id} – {b.TypeShort}{(b.OneWay ? (b.DefDir > 0 ? ", kierunek → na wschód" : ", kierunek ← na zachód") : "")}");
            if (b.SemiAuto) foreach (var (en, i) in b.Ends.Select((x, i) => (x, i))) Add(mj, $"Blok końcowy (Bkon){(b.Ends.Count > 1 ? " – " + (station.Posts.GetValueOrDefault(en.Post) ?? en.Post) : "")}", $"{id} Bkon {(en.Outward > 0 ? "E" : "W")}", enabled: bs.AwaitEnd == i);
            foreach (var (en, i) in b.Ends.Select((x, i) => (x, i)))
                Add(mj, (bs.StopEnd[i] ? "Odwołanie zamknięcia wyjazdu" : "Zamknięcie wyjazdu na szlak") + (b.Ends.Count > 1 ? " z " + (station.Posts.GetValueOrDefault(en.Post) ?? en.Post) : ""), $"{id} {(bs.StopEnd[i] ? "oStop" : "Stop")} {(en.Outward > 0 ? "E" : "W")}");
            mj.IsOpen = true;
            return;
        }
        if (b.Internal)
        {
            // szlak wewnętrzny LCS – oba końce u dyżurnego: kierunek wybierany wprost
            var mi = NewMenu($"Szlak {id} (wewnętrzny LCS) – {(bs.Dir > 0 ? "kierunek → na wschód" : bs.Dir < 0 ? "kierunek ← na zachód" : "stan neutralny")}");
            foreach (var en in b.Ends)
                Add(mi, $"Kierunek {(en.Outward > 0 ? "→ na wschód" : "← na zachód")} – wyjazd z {station.Posts.GetValueOrDefault(en.Post) ?? en.Post}", $"{id} Wbl {(en.Outward > 0 ? "E" : "W")}", enabled: bs.Dir != en.Outward);
            Add(mi, "Zwolnienie kierunku (Zwbl)", $"{id} Zwbl", enabled: bs.Dir != 0);
            if (b.SemiAuto) foreach (var (en, i) in b.Ends.Select((x, i) => (x, i))) Add(mi, $"Blok końcowy – {station.Posts.GetValueOrDefault(en.Post) ?? en.Post}", $"{id} Bkon {(en.Outward > 0 ? "E" : "W")}", enabled: bs.AwaitEnd == i);
            foreach (var (en, i) in b.Ends.Select((x, i) => (x, i)))
                Add(mi, (bs.StopEnd[i] ? "Odwołanie zamknięcia wyjazdu z " : "Zamknięcie wyjazdu na szlak z ") + (station.Posts.GetValueOrDefault(en.Post) ?? en.Post), $"{id} {(bs.StopEnd[i] ? "oStop" : "Stop")} {(en.Outward > 0 ? "E" : "W")}");
            Sep(mi, "Polecenia specjalne");
            foreach (var en in b.Ends) Add(mi, $"Awaryjna zmiana kierunku na {(en.Outward > 0 ? "zachód (←)" : "wschód (→)")}", $"{id} AZK {(en.Outward > 0 ? "E" : "W")}", special: true);
            mi.IsOpen = true;
            return;
        }
        var m = NewMenu($"Blokada {id} – {sim.DirText(b, bs.Dir, ei)}");
        bool reqFromOther = bs.Req != null && bs.Req.EndIdx != ei, reqMine = bs.Req != null && bs.Req.EndIdx == ei;
        Add(m, "Włączenie kierunku WYJAZD (Wbl)", $"{id} Wbl", enabled: !reqMine && bs.Dir != b.Ends[ei].Outward);
        if (reqMine) Add(m, "Odwołanie żądania (oWbl)", $"{id} oWbl");
        Add(m, "Pozwolenie – kierunek PRZYJAZD (Pzk)", $"{id} Pzk", enabled: reqFromOther);
        Add(m, "Zwolnienie kierunku (Zwbl)", $"{id} Zwbl", enabled: bs.Dir != 0);
        if (b.SemiAuto) Add(m, "Blok końcowy (Bkon)", $"{id} Bkon", enabled: bs.AwaitEnd == ei);
        Add(m, bs.StopEnd[ei] ? "Odwołanie zamknięcia wyjazdu (oStop)" : "Zamknięcie wyjazdu na szlak (Stop)", $"{id} {(bs.StopEnd[ei] ? "oStop" : "Stop")}");
        Sep(m, "Polecenia specjalne");
        Add(m, "Awaryjna zmiana kierunku (AZK)", $"{id} AZK", special: true);
        m.IsOpen = true;
    }
    void OnTrackEnd(string id, MouseButtonEventArgs e) { if (Panel.SelStart != null) Dest(id); }
    void OnDerailer(string name, MouseButtonEventArgs e)
    {
        var d = sim.Ders[name]; var lb = station.Derailers[name].Label; var m = NewMenu("Wykolejnica " + lb);
        Add(m, "Założenie na tor (Plus)", $"{lb} Plus", enabled: !d.On); Add(m, "Zdjęcie z toru (Minus)", $"{lb} Minus", enabled: d.On);
        m.IsOpen = true;
    }

    ContextMenu NewMenu(string title)
    {
        var m = new ContextMenu { PlacementTarget = Panel, Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint };
        m.Items.Add(new MenuItem { Header = title, IsEnabled = false, FontWeight = FontWeights.Bold });
        m.Items.Add(new Separator());
        return m;
    }
    void Sep(ContextMenu m, string t) { m.Items.Add(new Separator()); m.Items.Add(new MenuItem { Header = t, IsEnabled = false, Foreground = Brushes.DarkRed, FontSize = 11 }); }
    void Add(ContextMenu m, string label, string cmd, Action fn = null, bool special = false, bool enabled = true)
    {
        var mi = new MenuItem { Header = label, IsEnabled = enabled, InputGestureText = cmd ?? "" };
        if (special) mi.Foreground = Brushes.DarkRed;
        mi.Click += (a, b) => { if (fn != null) fn(); else { ClearSel(); Run(cmd); } };
        m.Items.Add(mi);
    }

    // ------------------------------------------------------------------ przyciski
    void Speed_Click(object sender, RoutedEventArgs e)
    {
        var v = double.Parse((string)((Button)sender).Tag);
        if (v == 0) { paused = !paused; } else { speed = v; paused = false; }
    }
    void FitStation_Click(object s, RoutedEventArgs e) => Panel.FitPage();
    void FitAll_Click(object s, RoutedEventArgs e) => Panel.FitPage();
    void Plan_Click(object s, RoutedEventArgs e) => new PlanWindow(sim) { Owner = this }.Show();
    void Routes_Click(object s, RoutedEventArgs e) => new RoutesWindow(sim) { Owner = this }.Show();
    void Book_Click(object s, RoutedEventArgs e) => new BookWindow(sim) { Owner = this }.Show();
    void Instr_Click(object s, RoutedEventArgs e) => new InstructorWindow(sim, sc => { var u = sim.User; Start(sc, u); paused = false; }) { Owner = this }.Show();
    void Score_Click(object s, RoutedEventArgs e) => new ScoreWindow(sim) { Owner = this }.Show();
    void Help_Click(object s, RoutedEventArgs e) => new HelpWindow() { Owner = this }.Show();
    void Ack_Click(object s, RoutedEventArgs e) { Run("KW"); SortAlarms(); }
    void GridAlarms_DoubleClick(object s, MouseButtonEventArgs e) { if (GridAlarms.SelectedItem is EventRecord a) { a.Ack = true; SortAlarms(); } }

    bool Matches(EventRecord e) => EvFilter.SelectedIndex <= 0 || e.Kategoria == (string)EvFilter.SelectedItem;
    void EvFilter_Changed(object s, SelectionChangedEventArgs e)
    {
        if (sim == null) return;
        events.Clear();
        foreach (var x in sim.Events.Where(Matches).Reverse().Take(3000)) events.Add(x);
    }

    void Export_Click(object s, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog { FileName = $"rejestr_LCS_Widzew_{sim.Clock().Replace(":", "")}.csv", Filter = "CSV|*.csv" };
        if (dlg.ShowDialog() != true) return;
        var sb = new StringBuilder("Czas;Obszar;Stacja;Typ obiektu;Obiekt;Kategoria;Zdarzenie;Operator;Notatka;Źródło\r\n");
        foreach (var x in sim.Events) sb.Append(string.Join(";", new[] { x.Czas, x.Obszar, x.Stacja, x.TypObiektu, x.Obiekt, x.Kategoria, x.Zdarzenie, x.Operator, x.Notatka, x.Zrodlo }.Select(c => "\"" + (c ?? "").Replace("\"", "\"\"") + "\""))).Append("\r\n");
        File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
    }
}

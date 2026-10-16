using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LcsWidzew.Model;
using LcsWidzew.Net;
using LcsWidzew.Sim;
using LcsWidzew.UI;

namespace LcsWidzew;

/// <summary>
/// Stanowisko dyżurnego ruchu (zobrazowanie). Z komputerem zależności rozmawia wyłącznie przez sesję <see cref="ILcsSession"/>:
/// lokalnie (gra jednoosobowa, serwer) albo przez sieć (stanowisko przyłączone do serwera).
/// </summary>
public partial class MainWindow : Window
{
    Station station;
    ILcsSession ses;
    /// <summary>Komputer zależności – tylko gdy działa w tym programie.</summary>
    Simulator sim => ses?.Sim;
    LcsServer server;
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    DateTime last = DateTime.Now;
    double speed = 2, acc;
    bool paused = true;
    int pushTick;
    string scenario = "podstawowy";
    readonly ObservableCollection<EventRecord> events = new();
    readonly ObservableCollection<EventRecord> alarms = new();
    readonly List<string> history = new(); int histIdx;
    SpecialConfirmWindow confirmWin;
    NetWindow netWin;
    readonly HashSet<string> ctlPrompted = new();
    double lastBeep;

    public MainWindow()
    {
        InitializeComponent();
        station = Station.Load(App.StationPath);
        GridEvents.ItemsSource = events;
        GridAlarms.ItemsSource = alarms;
        EvFilter.ItemsSource = new[] { "wszystkie", "Polecenie", "Odrzucone", "Zdarzenie", "Alarm", "Ruch", "Telefon", "Uwaga", "Ocena", "Instruktor", "E-1758" };
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
        Closed += (s, e) => { server?.Dispose(); (ses as RemoteSession)?.Dispose(); };
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

    /// <summary>Wczytanie układu z pliku i ponowny start symulacji.</summary>
    public void ReloadStation(string path) => ReloadStation(path, false);

    public void ReloadStation(string path, bool quiet)
    {
        if (!ses.IsHost) return;
        try
        {
            var st = Station.Load(path);
            var keep = Panel.Page?.Id;
            station = st; App.StationPath = path;
            BuildTabs();
            Start(scenario, ses.Last?.User ?? sim?.User);
            int pi = quiet ? station.Pages.FindIndex(p => p.Id == keep) : -1;
            ShowPage(pi >= 0 ? pi : station.Pages.Count - 1);
            sim.Log("Zdarzenie", "Wczytano układ: " + Path.GetFileName(path), "System", "");
            if (!quiet) Activate();
        }
        catch (Exception ex) { MessageBox.Show(this, "Nie można wczytać układu: " + ex.Message, "Układ"); }
    }

    /// <summary>Obraz poglądowy całego obszaru LCS – wszystkie obrazy obok siebie, klik przenosi do obrazu szczegółowego.</summary>
    void ShowOverview()
    {
        if (station.Pages.Count == 0) return;
        Panel.Overview = true;
        Panel.Page = new PageDef { Id = "OV", Name = "Obraz poglądowy LCS", X0 = station.Pages.Min(p => p.X0), X1 = station.Pages.Max(p => p.X1) };
        ClearSel();
        MarkTab("OV");
        Hint.Text = "Obraz poglądowy – klik na obszarze stacji otwiera jej obraz szczegółowy";
        Dispatcher.BeginInvoke(() => Panel.FitPage(), DispatcherPriority.Render);
    }

    void MarkTab(string id)
    {
        foreach (Button b in PageTabs.Children)
        {
            bool on = (string)b.Tag == id;
            b.Background = on ? new SolidColorBrush(Color.FromRgb(0x0a, 0x4a, 0x66)) : new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26));
            b.BorderBrush = on ? new SolidColorBrush(Color.FromRgb(0x00, 0xa0, 0xff)) : new SolidColorBrush(Color.FromRgb(0x4a, 0x4a, 0x4a));
        }
    }

    void ShowPage(int idx)
    {
        if (idx < 0 || idx >= station.Pages.Count) return;
        var pg = station.Pages[idx];
        Panel.Overview = false;
        Panel.Page = pg; ses?.SetPage(pg.Id);
        ClearSel();
        MarkTab(pg.Id);
        Dispatcher.BeginInvoke(() => Panel.FitPage(), DispatcherPriority.Render);
    }

    void ClearSel() { Panel.SelStart = null; Panel.SelSwitch = null; Panel.SelVia.Clear(); Panel.Targets.Clear(); Panel.InvalidateVisual(); }

    /// <summary>Początek przebiegu: możliwe końce (z punktami pośrednimi) oznaczone na obrazie.</summary>
    void SetStart(string name)
    {
        Panel.SelStart = name; Panel.SelVia.Clear();
        MarkTargets();
    }

    void MarkTargets()
    {
        Panel.Targets.Clear();
        if (Panel.SelStart == null || !ses.RouteTable.TryGetValue(Panel.SelStart, out var l)) return;
        foreach (var r in l.Where(r => Panel.SelVia.All(v => Passes(r, v))))
            Panel.Targets.Add(r.EndKind is "szlak" or "szlakM" ? "blk:" + r.EndNode : r.End);
        Panel.InvalidateVisual();
    }

    // ------------------------------------------------------------------ tryb zrzutu (testy wyglądu)
    public string ShotPath; public string[] ShotCmds = Array.Empty<string>(); public double ShotSeconds = 60;
    void Demo()
    {
        if (ShotCmds.FirstOrDefault()?.StartsWith("#scen=") == true) { scenario = ShotCmds[0][6..]; ShotCmds = ShotCmds.Skip(1).ToArray(); }
        if (ShotCmds.FirstOrDefault()?.StartsWith("#join=") == true)
        {
            var hp = ShotCmds[0][6..].Split(':'); ShotCmds = ShotCmds.Skip(1).ToArray();
            Join(hp[0], hp.Length > 1 ? int.Parse(hp[1]) : Wire.DefaultPort, "Test sieci");
            ses.Login(new User { Name = "Test2", Role = "dyżurny ruchu" });
        }
        else Start(scenario, new User { Name = "Test", Role = "dyżurny ruchu" });
        paused = true;
        foreach (var c in ShotCmds)
        {
            if (c.StartsWith("@")) { var until = double.Parse(c[1..], System.Globalization.CultureInfo.InvariantCulture); if (sim != null) while (sim.T < until) { sim.Tick(0.1); server?.Push(); } else Pump(until); continue; }
            if (c == "#overview") { ShowOverview(); continue; }
            if (c.StartsWith("#fault=") && sim != null) { var f = c[7..].Split(':'); sim.Instruktor.ApplyFault(f[0], f.Length > 1 ? f[1] : "", false); continue; }
            if (c.StartsWith("#page=")) { ShowPage(int.Parse(c[6..])); continue; }
            if (c.StartsWith("#serve=")) { var er = Serve(int.Parse(c[7..]), true); File.AppendAllText(ShotPath + ".txt", $"serwer: {er ?? "OK"}{(char)10}"); continue; }
            if (c.StartsWith("#hold=")) { ShotHold = double.Parse(c[6..], System.Globalization.CultureInfo.InvariantCulture); continue; }
            if (c.StartsWith("#sleep=")) { var end = DateTime.Now.AddSeconds(double.Parse(c[7..], System.Globalization.CultureInfo.InvariantCulture)); while (DateTime.Now < end) { Dispatcher.Invoke(() => { }, DispatcherPriority.Background); Thread.Sleep(50); } continue; }
            if (c.StartsWith("#phone=")) { ses.Phone("*", c[7..]); continue; }
            if (c.StartsWith("#kz=")) { kzShots.Add(int.Parse(c[4..])); continue; }
            if (c.StartsWith("#plan")) { planShot = c.Length > 6 ? c[6..] : ""; continue; }
            if (c.StartsWith("#sw=")) { Panel.SelSwitch = station.Switches.Values.First(x => x.Label == c[4..] && Panel.Page.Posts.Contains(x.Post)).Num; continue; }
            if (c.StartsWith("#via=")) { ToggleVia(station.Switches.Values.First(x => x.Label == c[5..] && Panel.Page.Posts.Contains(x.Post)).Num); continue; }
            if (c.StartsWith("#start=")) { SetStart(station.Signals.Values.First(x => x.Label == c[7..] && Panel.Page.Posts.Contains(x.Post)).Name); continue; }
            if (c.StartsWith("#ctl=")) { var r = ses.RequestControl(c[5..]); File.AppendAllText(ShotPath + ".txt", $"przejęcie {c[5..]}: {r ?? "OK"}\n"); continue; }
            var res = Run(c);
            File.AppendAllText(ShotPath + ".txt", $"{c}: {(res.Ok ? "OK" : "ODRZUCONE")} {res.Msg}\n");
        }
        if (sim != null) { while (sim.T < ShotSeconds) sim.Tick(0.1); server?.Push(); }
        else Pump(-1);
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
                if (planShot != null)
                {
                    var pw = new PlanStationWindow(() => sim) { Owner = this, Width = 1500, Height = 820 }; pw.Show(); pw.UpdateLayout();
                    var pv = (PlanLiveView)((DockPanel)pw.Content).Children[1];
                    if (planShot.StartsWith("post:")) pv.FitPost(planShot[5..]); else if (planShot.StartsWith("train:")) pv.Follow = planShot[6..]; else pv.FitAll();
                    pv.UpdateLayout(); Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    var rp = new System.Windows.Media.Imaging.RenderTargetBitmap((int)pw.ActualWidth, (int)pw.ActualHeight, 96, 96, PixelFormats.Pbgra32); rp.Render((Visual)pw.Content);
                    var ep = new System.Windows.Media.Imaging.PngBitmapEncoder(); ep.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rp));
                    using (var f = File.Create(ShotPath + ".plan.png")) ep.Save(f);
                    pw.Close();
                }
                foreach (var tab in kzShots)
                {
                    var kw = new KzWindow(() => sim, () => server) { Owner = this, Width = 1100, Height = 520 }; kw.Show(); kw.ShowTab(tab); kw.UpdateLayout();
                    Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    var rk = new System.Windows.Media.Imaging.RenderTargetBitmap((int)kw.ActualWidth, (int)kw.ActualHeight, 96, 96, PixelFormats.Pbgra32); rk.Render((Visual)kw.Content);
                    var ek = new System.Windows.Media.Imaging.PngBitmapEncoder(); ek.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rk));
                    using (var f = File.Create(ShotPath + $".kz{tab}.png")) ek.Save(f);
                    kw.Close();
                }
                if (ShotHold > 0) { var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(ShotHold) }; t.Tick += (a, b) => Application.Current.Shutdown(); t.Start(); }
                else Application.Current.Shutdown();
            }, DispatcherPriority.ApplicationIdle);
        }, DispatcherPriority.ApplicationIdle);
    }
    /// <summary>Tryb zrzutu: ile sekund program działa po zrzucie (serwer dla testu sieci).</summary>
    public double ShotHold;
    readonly List<int> kzShots = new();
    string planShot;

    /// <summary>Stanowisko sieciowe w trybie testu: odczekanie na meldunki z serwera.</summary>
    void Pump(double until)
    {
        var end = DateTime.Now.AddSeconds(until < 0 ? 1.5 : 3);
        while (DateTime.Now < end) { Dispatcher.Invoke(() => { }, DispatcherPriority.Background); Thread.Sleep(50); if (until >= 0 && ses.Last?.T >= until) break; }
    }

    // ------------------------------------------------------------------ start / sesja
    /// <summary>Nowy komputer zależności w tym programie (gra jednoosobowa albo serwer).</summary>
    void Start(string sc, User user)
    {
        scenario = sc;
        if (ses is RemoteSession rs) { rs.Dispose(); }
        var s = new Simulator(station);
        if (Panel.Page != null && Panel.Page.Id != "OV") s.Page = Panel.Page.Id;
        var tr = new Trains(s);
        tr.Load(sc);
        var desk = s.AddDesk("PC", server != null ? "Stanowisko główne (serwer)" : "Stanowisko", true);
        desk.Page = s.Page; desk.User = user; s.User = user;
        Bind(new LocalSession(s, desk));
        server?.Attach(s);
        s.Log("Zdarzenie", "Start symulacji – scenariusz: " + Rozklad.Scenarios.First(x => x.Key == sc).Name + (user != null ? ", zalogowany: " + user.Name : ""), "System", "");
    }

    void Bind(ILcsSession x)
    {
        ses = x;
        station = x.L;
        var area = station.Name ?? "LCS";
        TxtArea.Text = area; Title = $"Symulator {area} – komputerowe urządzenia srk (Ie-20)";
        events.Clear(); alarms.Clear(); ctlPrompted.Clear();
        foreach (var e in x.Events.Where(Matches).Reverse().Take(3000)) events.Add(e);
        foreach (var a in x.Alarms) alarms.Add(a);
        SortAlarms();
        x.EventAdded += e => { if (ses != x) return; if (Matches(e)) events.Insert(0, e); if (events.Count > 3000) events.RemoveAt(events.Count - 1); };
        x.AlarmAdded += a => { if (ses != x) return; alarms.Insert(0, a); SortAlarms(); };
        x.AlarmsChanged += () => { if (ses == x) SortAlarms(); };
        x.Beep += () => { if (ses == x && ChkMute.IsChecked != true) System.Media.SystemSounds.Asterisk.Play(); };
        x.SpecialExecuted += () => { if (ses == x) Dispatcher.BeginInvoke(() => new BookWindow(ses) { Owner = this }.Show()); };
        x.PendingExpired += () => { if (ses == x) confirmWin?.Close(); };
        x.Disconnected += why => { if (ses != x) return; MessageBox.Show(this, why + ".\nStanowisko przechodzi w stan „brak danych”. Połącz ponownie w oknie „Stanowiska / sieć” albo uruchom grę jednoosobową.", "Sieć"); };
        x.Reset += () => { if (ses != x) return; var keep = Panel.Page?.Id; Bind(x); BuildTabs(); var pi = station.Pages.FindIndex(p => p.Id == keep); ShowPage(pi >= 0 ? pi : station.Pages.Count - 1); };
        Panel.Sim = x; ClearSel();
        BtnNet.Background = x.IsHost ? (server != null ? new SolidColorBrush(Color.FromRgb(0x2f, 0x5a, 0x2a)) : new SolidColorBrush(Color.FromRgb(0x3a, 0x2a, 0x5a))) : new SolidColorBrush(Color.FromRgb(0x1f, 0x4a, 0x7a));
        UpdateTop();
    }

    /// <summary>Uruchomienie serwera stanowisk na tym komputerze.</summary>
    public string Serve(int port, bool loopbackOnly = false)
    {
        if (!ses.IsHost) return "Najpierw rozłącz się z serwerem";
        if (server != null) return null;
        try
        {
            server = new LcsServer(Dispatcher, sim, () => File.ReadAllText(App.StationPath));
            server.Start(port, loopbackOnly);
            if (ses is LocalSession ls) ls.Desk.Name = "Stanowisko główne (serwer)";
            sim.Log("Zdarzenie", $"Uruchomiono serwer stanowisk – port {port}", "Sieć", "");
            Bind(ses);
            return null;
        }
        catch (Exception ex) { server = null; return "Nie można uruchomić serwera: " + ex.Message; }
    }

    public void StopServer()
    {
        if (server == null) return;
        server.Dispose(); server = null;
        foreach (var d in sim.Desks.Values.Where(d => !d.Host).ToList()) sim.RemoveDesk(d.Id);
        sim.Log("Zdarzenie", "Zatrzymano serwer stanowisk", "Sieć", "");
        Bind(ses);
    }

    /// <summary>Przyłączenie do serwera jako stanowisko sieciowe.</summary>
    public string Join(string host, int port, string name)
    {
        try
        {
            var rs = RemoteSession.Connect(host, port, name, Dispatcher);
            server?.Dispose(); server = null;
            Bind(rs);
            BuildTabs(); ShowPage(station.Pages.Count - 1);
            return null;
        }
        catch (Exception ex) { return "Nie można połączyć: " + (ex.InnerException?.Message ?? ex.Message); }
    }

    /// <summary>Powrót do gry jednoosobowej (rozłączenie stanowiska sieciowego).</summary>
    public void Leave()
    {
        if (ses is not RemoteSession rs) return;
        rs.Dispose();
        station = Station.Load(App.StationPath); BuildTabs();
        Start(scenario, rs.Last?.User);
        ShowPage(station.Pages.Count - 1);
    }

    public ILcsSession Session => ses;
    public LcsServer Server => server;

    /// <summary>Kolejność na liście alarmów: niepotwierdzone, trwające potwierdzone, nietrwające potwierdzone (Ie-104 § 7 ust. 5).</summary>
    void SortAlarms()
    {
        var sorted = alarms.OrderBy(a => a.Ack ? (a.Active ? 1 : 2) : 0).ThenByDescending(a => a.T).ToList();
        for (int i = 0; i < sorted.Count; i++) { int j = alarms.IndexOf(sorted[i]); if (j != i) alarms.Move(j, i); }
    }

    void ShowLogin()
    {
        var I = ses.Receive();
        var w = new LoginWindow(I?.User != null) { Owner = this };
        if (w.ShowDialog() == true)
        {
            var u = new User { Name = w.UserName, Role = w.Role };
            if (ses.IsHost && (w.Restart || I?.User == null && ses.Events.Count < 3)) Start(w.Scenario, u);
            else ses.Login(u);
            paused = false; Cmd.Focus();
        }
        UpdateTop();
    }

    void BtnLogin_Click(object sender, RoutedEventArgs e)
    {
        var u = ses.Receive()?.User;
        if (u != null && MessageBox.Show(this, "Wylogować użytkownika " + u.Name + "?\n(Ie-20 §7 ust. 2 – wylogowanie przy oddaleniu się od stanowiska)", "Logowanie", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            ses.Login(null); UpdateTop(); return;
        }
        ShowLogin();
    }

    // ------------------------------------------------------------------ pętla czasu
    void OnTimer(object s, EventArgs e)
    {
        var now = DateTime.Now; var real = Math.Min(0.25, (now - last).TotalSeconds); last = now;
        if (sim != null && ShotPath == null)
        {
            if (!paused && speed > 0)
            {
                acc += real * speed;
                int steps = 0;
                while (acc >= 0.1 && steps < 120) { sim.Tick(0.1); acc -= 0.1; steps++; }
            }
        }
        if (server != null && ++pushTick % 4 == 0) server.Push();        // meldunki do stanowisk sieciowych co 0,2 s
        Panel.InvalidateVisual();
        UpdateTop();
        // dźwięk co kilka sekund, dopóki istnieje niepotwierdzony alarm; głośniejszy po minucie (Ie-104 § 7 ust. 6)
        var un = ses.Alarms.Where(a => !a.Ack).ToList();
        var T = ses.Last?.T ?? 0;
        if (un.Count > 0 && ChkMute.IsChecked != true && (now - DateTime.MinValue).TotalSeconds - lastBeep > 4)
        {
            lastBeep = (now - DateTime.MinValue).TotalSeconds;
            if (un.Any(a => T - a.T > 60)) System.Media.SystemSounds.Hand.Play(); else System.Media.SystemSounds.Asterisk.Play();
        }
    }

    void UpdateTop()
    {
        if (ses == null) return;
        var I = ses.Receive(); if (I == null) return;
        TxtClock.Text = I.Clock ?? "--:--:--";
        Life.Fill = !I.NoLink && (Environment.TickCount64 / 500) % 2 == 0 ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
        void Lamp(Border b, bool on, bool warn) { b.Background = on ? (warn ? new SolidColorBrush(Color.FromRgb(0xff, 0xc0, 0)) : new SolidColorBrush(Color.FromRgb(0x3d, 0xdc, 0x84))) : Brushes.Transparent; ((TextBlock)b.Child).Foreground = on ? Brushes.Black : Brushes.Gray; }
        Lamp(LampMains, I.Power.Mains, false); Lamp(LampUps, !I.Power.Mains && I.Power.Ups, true); Lamp(LampGen, I.Power.Gen, true); Lamp(LampNN, I.Power.NN, false);
        var user = I.User;
        TxtLogged.Text = user == null ? "Zalogowany: – (brak możliwości wydawania poleceń)" : $"Zalogowany: {user.Name}";
        TxtRole.Text = user == null ? "Brak obsługi" : char.ToUpper(user.Role[0]) + user.Role[1..];
        ChipRole.Background = user == null ? new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)) : new SolidColorBrush(Color.FromRgb(0x2f, 0x7d, 0x32));
        BtnLogin.Content = user == null ? "Logowanie" : "Wyloguj";
        TxtPaused.Visibility = ses.IsHost && (paused || speed == 0) ? Visibility.Visible : Visibility.Collapsed;
        var mine = I.Control.Where(c => c.Value == ses.DeskId).Select(c => station.Posts.GetValueOrDefault(c.Key) ?? c.Key).ToList();
        TxtNet.Text = (ses is RemoteSession rs ? $"Stanowisko sieciowe „{ses.DeskName}” – serwer {rs.Server}" : server != null ? $"Serwer – port {server.Port}, stanowisk: {server.Count}" : $"{station.Name} ({string.Join(", ", station.Posts.Keys.Select(p => Simulator.PostShort(station, p)))})")
            + (I.Desks.Count > 1 || ses is RemoteSession ? " · steruje: " + (mine.Count > 0 ? string.Join(", ", mine) : "–") : "");
        var n = ses.Alarms.Count(a => !a.Ack);
        AlCount.Text = n > 0 ? $"Niepotwierdzone: {n}" : "";
        EvCount.Text = $"{ses.Events.Count} zdarzeń";
        if (I.Pending != null && confirmWin == null)
        {
            confirmWin = new SpecialConfirmWindow(ses) { Owner = this };
            confirmWin.Closed += (a, b) => { confirmWin = null; Panel.InvalidateVisual(); };
            confirmWin.Show();
        }
        // żądania przekazania obszaru sterowania od innych stanowisk
        foreach (var r in I.ControlReqs)
        {
            var key = r.Post + "|" + r.From + "|" + r.At;
            if (!ctlPrompted.Add(key)) continue;
            var from = I.Desks.FirstOrDefault(d => d.Id == r.From);
            var rr = r;
            if (ShotPath != null) { ses.AnswerControl(rr.Post, true); continue; }   // tryb testu – zgoda bez pytania
            Dispatcher.BeginInvoke(() =>
            {
                var yes = MessageBox.Show(this, $"Stanowisko {from?.Name ?? r.From} ({from?.User ?? "bez obsługi"}) żąda przekazania sterowania posterunkiem {station.Posts.GetValueOrDefault(rr.Post) ?? rr.Post}.\n\nPrzekazać?", "Przekazanie obszaru sterowania", MessageBoxButton.YesNo) == MessageBoxResult.Yes;
                ses.AnswerControl(rr.Post, yes);
            });
        }
    }

    // ------------------------------------------------------------------ polecenia
    public CmdResult Run(string line)
    {
        var r = ses.Exec(line);
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
    Indication Ind => ses.Receive() ?? new Indication();

    // ------------------------------------------------------------------ mysz na pulpicie
    void OnSignal(string name, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Right) { SignalMenu(name); return; }
        if (Panel.SelStart != null && Ctrl) { ToggleVia(name); return; }
        if (Panel.SelStart == null)
        {
            if (!ses.RouteTable.TryGetValue(name, out var l) || l.Count == 0) { Hint.Text = "Brak przebiegów od " + L(name); return; }
            SetStart(name);
            Hint.Text = $"Początek: {L(name)} – wskaż koniec przebiegu (oznaczone turkusowo). Przebieg wariantowy: najpierw kliknij zwrotnice, przez które ma prowadzić. Esc – rezygnacja.";
        }
        else if (Panel.SelStart == name) ClearSel();
        else Dest(name);
        Panel.InvalidateVisual();
    }

    void ToggleVia(string key)
    {
        if (Panel.SelVia.Contains(key)) Panel.SelVia.Remove(key); else Panel.SelVia.Add(key);
        MarkTargets();
        Hint.Text = Panel.SelVia.Count == 0 ? $"Początek: {L(Panel.SelStart)} – wskaż koniec przebiegu" : $"Początek: {L(Panel.SelStart)}, przez: {string.Join(", ", Panel.SelVia.Select(L))} – wskaż koniec przebiegu (oznaczone końce osiągalne tą drogą)";
        Panel.InvalidateVisual();
    }

    bool Passes(RouteDef r, string v) => Simulator.Passes(station, r, v);

    /// <summary>Koniec przebiegu – menu z przebiegiem zasadniczym i wariantowymi (Ie-104 § 5 ust. 4).</summary>
    void Dest(string end)
    {
        var start = Panel.SelStart;
        var ends = new List<string> { end };
        // semafor wjazdowy: także przebiegi na szlak – kończą się przy nim (mały trójkąt końca przebiegu wyjazdowego)
        if (station.Signals.TryGetValue(end, out var sd))
            ends.AddRange(ses.RouteTable[start].Where(r => r.EndNode == sd.Node && r.EndKind is "szlak" or "szlakM").Select(r => r.End).Distinct());
        var all = ses.RouteTable[start].Where(r => ends.Contains(r.End)).ToList();
        var list = all.Where(r => Panel.SelVia.All(v => Passes(r, v))).ToList();
        string Cmdl(RouteDef r)
        {
            var via = ViaFor(r, all.Where(x => x.End == r.End).ToList());
            return $"{L(start)} {string.Join(" ", via.Select(L))}{(via.Count > 0 ? " " : "")}{L(r.End)} {(r.Type == "P" ? "Pociąg" : "Manewr")}";
        }
        var m = NewMenu($"Przebieg {L(start)} → {L(end)}");
        if (list.Count == 0)
        {
            if (all.Count == 0 && ses.RouteTable[start].Any(r => r.Type == "M"))
                Add(m, $"Przebieg manewrowy złożony {L(start)} → {L(end)}", $"{L(start)} {L(end)} Manewr");
            else { Hint.Text = $"Brak przebiegu {L(start)} → {L(end)}{(Panel.SelVia.Count > 0 ? " przez wskazane punkty" : "")}"; return; }
        }
        foreach (var r in list)
        {
            var kind = r.Type == "P" ? "pociągowy" : "manewrowy";
            var var_ = r.Variant == 0 ? "zasadniczy" : $"wariantowy {r.Variant}";
            Add(m, $"Przebieg {kind} {var_}{(r.EndKind is "szlak" or "szlakM" ? " na szlak " + r.End : "")}   [{r.SwText}]", Cmdl(r));
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
            if (all.Where(x => x.Type == r.Type && Passes(x, q.Num + q.Pos)).OrderBy(x => x.Variant).First() == r) return new() { q.Num + q.Pos };
        return diff.Select(q => q.Num + q.Pos).ToList();
    }

    void SignalMenu(string name)
    {
        var I = Ind; if (!I.Sigs.TryGetValue(name, out var s)) return; var d = station.Signals[name];
        var m = NewMenu((d.Type == "tm" ? "Tarcza " : "Semafor ") + L(name));
        Add(m, "Wybierz jako początek przebiegu", null, () => { SetStart(name); Panel.InvalidateVisual(); });
        if (s.Aspect is Aspect.Train or Aspect.Shunt) Add(m, "Sygnał „Stój”", $"{L(name)} Stój");
        var r = I.Routes.Values.FirstOrDefault(x => x.Start == name);
        if (r != null && r.Type == "P") { Add(m, "Zwolnienie przebiegu ze zwłoką (ZCZ)", $"{L(name)} ZCZ"); if (r.TimeRel) Add(m, "Przerwanie odliczania (oZCZ)", $"{L(name)} oZCZ"); }
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
        // wybrany początek przebiegu: klik na zwrotnicy wskazuje wariant (przebieg ma przez nią prowadzić)
        if (Panel.SelStart != null && e.ChangedButton == MouseButton.Left) { ToggleVia(num); return; }
        var I = Ind; if (!I.Sws.TryGetValue(num, out var w)) return;
        Panel.SelSwitch = num; Panel.InvalidateVisual();
        var lb = station.Switches[num].Label;
        var m = NewMenu($"Zwrotnica {lb} (położenie {w.Pos})");
        Add(m, "Przestawienie do „+” (Plus)", $"{lb} Plus", enabled: w.Pos != "+" || w.Moving != null);
        Add(m, "Przestawienie do „−” (Minus)", $"{lb} Minus", enabled: w.Pos != "-" || w.Moving != null);
        Add(m, w.Stopped ? "Odwołanie zastopowania (oStop)" : "Zastopowanie (Stop)", $"{lb} {(w.Stopped ? "oStop" : "Stop")}");
        var sec = station.Switches[num].Sec; var sc = I.Secs.GetValueOrDefault(sec) ?? new SecInd();
        Add(m, sc.Closed ? "Odwołanie zamknięcia ruchowego (oZmk)" : "Zamknięcie ruchowe (Zmk)", $"{lb} {(sc.Closed ? "oZmk" : "Zmk")}");
        Sep(m, "Polecenia specjalne");
        Add(m, "Przestawienie „+” z bocznikowaniem (PlusBZ)", $"{lb} PlusBZ", special: true);
        Add(m, "Przestawienie „−” z bocznikowaniem (MinBZ)", $"{lb} MinBZ", special: true);
        Add(m, "Kasowanie sygnalizacji rozprucia (KSR)", $"{lb} KSR", special: true, enabled: w.Rozpr);
        Add(m, "Zerowanie licznika osi (ZeroLO)", $"{lb} ZeroLO", special: true, enabled: sc.Fault != null);
        m.Closed += (a, b) => { Panel.SelSwitch = null; };
        m.IsOpen = true;
    }

    void OnSection(string sec, MouseButtonEventArgs e)
    {
        if (Panel.SelStart != null && Ctrl) { ToggleVia(sec); return; }
        if (e.ChangedButton != MouseButton.Right) return;
        var s = Ind.Secs.GetValueOrDefault(sec) ?? new SecInd(); var m = NewMenu("Odcinek " + L(sec));
        Add(m, s.Closed ? "Odwołanie zamknięcia ruchowego (oZmk)" : "Zamknięcie ruchowe toru (Zmk)", $"{L(sec)} {(s.Closed ? "oZmk" : "Zmk")}");
        Sep(m, "Polecenia specjalne");
        Add(m, "Zerowanie licznika osi (ZeroLO)", $"{L(sec)} ZeroLO", special: true, enabled: s.Fault is "reset" or "occ");
        m.IsOpen = true;
    }

    /// <summary>Koniec szlaku po stronie posterunku, którym steruje stanowisko (albo wyświetlanego obrazu).</summary>
    int EndIdxHere(BlockDef b)
    {
        var I = Ind;
        var mine = Enumerable.Range(0, b.Ends.Count).Where(i => I.Control.GetValueOrDefault(b.Ends[i].Post ?? "") == ses.DeskId).ToList();
        if (mine.Count == 1) return mine[0];
        for (int i = 0; i < b.Ends.Count; i++) if (Panel.Page != null && Panel.Page.Posts.Contains(b.Ends[i].Post)) return i;
        return 0;
    }

    void OnBlock(string id, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && Panel.SelStart != null) { Dest(id); return; }
        var b = station.Blocks[id]; if (!Ind.Blocks.TryGetValue(id, out var bs)) return; int ei = EndIdxHere(b);
        string PN(string p) => station.Posts.GetValueOrDefault(p ?? "") ?? p;
        if (!b.Permission)
        {
            // jednokierunkowa / telefoniczne zapowiadanie – bez bloku pozwolenia
            var mj = NewMenu($"Szlak {id} – {b.TypeShort}{(b.OneWay ? (b.DefDir > 0 ? ", kierunek → na wschód" : ", kierunek ← na zachód") : "")}");
            if (b.SemiAuto) foreach (var (en, i) in b.Ends.Select((x, i) => (x, i))) SemiItems(mj, id, b, bs, i, $" {(en.Outward > 0 ? "E" : "W")}", b.Ends.Count > 1 ? " – " + PN(en.Post) : "");
            foreach (var (en, i) in b.Ends.Select((x, i) => (x, i)))
                Add(mj, (bs.StopEnd[i] ? "Odwołanie zamknięcia wyjazdu" : "Zamknięcie wyjazdu na szlak") + (b.Ends.Count > 1 ? " z " + PN(en.Post) : ""), $"{id} {(bs.StopEnd[i] ? "oStop" : "Stop")} {(en.Outward > 0 ? "E" : "W")}");
            mj.IsOpen = true;
            return;
        }
        var I = Ind;
        bool bothMine = b.Internal && b.Ends.All(en => I.Control.GetValueOrDefault(en.Post ?? "") == ses.DeskId);
        if (b.Internal && bothMine)
        {
            // szlak wewnętrzny LCS – oba końce u tego dyżurnego: kierunek wybierany wprost
            var mi = NewMenu($"Szlak {id} (wewnętrzny LCS) – {(bs.Dir > 0 ? "kierunek → na wschód" : bs.Dir < 0 ? "kierunek ← na zachód" : "stan neutralny")}");
            foreach (var en in b.Ends)
                Add(mi, $"Kierunek {(en.Outward > 0 ? "→ na wschód" : "← na zachód")} – wyjazd z {PN(en.Post)}", $"{id} Wbl {(en.Outward > 0 ? "E" : "W")}", enabled: bs.Dir != en.Outward);
            Add(mi, "Zwolnienie kierunku (Zwbl)", $"{id} Zwbl", enabled: bs.Dir != 0);
            if (b.SemiAuto) foreach (var (en, i) in b.Ends.Select((x, i) => (x, i))) SemiItems(mi, id, b, bs, i, $" {(en.Outward > 0 ? "E" : "W")}", " – " + PN(en.Post));
            foreach (var (en, i) in b.Ends.Select((x, i) => (x, i)))
                Add(mi, (bs.StopEnd[i] ? "Odwołanie zamknięcia wyjazdu z " : "Zamknięcie wyjazdu na szlak z ") + PN(en.Post), $"{id} {(bs.StopEnd[i] ? "oStop" : "Stop")} {(en.Outward > 0 ? "E" : "W")}");
            Sep(mi, "Polecenia specjalne");
            foreach (var en in b.Ends) Add(mi, $"Awaryjna zmiana kierunku na {(en.Outward > 0 ? "zachód (←)" : "wschód (→)")}", $"{id} AZK {(en.Outward > 0 ? "E" : "W")}", special: true);
            mi.IsOpen = true;
            return;
        }
        // blokada z blokiem pozwolenia: szlak zewnętrzny albo wewnętrzny, którego drugi koniec obsługuje inne stanowisko
        var dirText = bs.Dir == 0 ? "stan neutralny" : bs.Dir == b.Ends[ei].Outward ? "kierunek WYJAZD" : "kierunek PRZYJAZD";
        var m = NewMenu($"Blokada {id}{(b.Internal ? " (" + PN(b.Ends[ei].Post) + ")" : "")} – {dirText}");
        bool reqFromOther = bs.Req != null && bs.Req.EndIdx != ei, reqMine = bs.Req != null && bs.Req.EndIdx == ei;
        string side = b.Internal ? $" {(b.Ends[ei].Outward > 0 ? "E" : "W")}" : "";
        Add(m, "Włączenie kierunku WYJAZD (Wbl)", $"{id} Wbl{side}", enabled: !reqMine && bs.Dir != b.Ends[ei].Outward);
        if (reqMine) Add(m, "Odwołanie żądania (oWbl)", $"{id} oWbl{side}");
        Add(m, "Pozwolenie – kierunek PRZYJAZD (Pzk)", $"{id} Pzk{side}", enabled: reqFromOther);
        Add(m, "Zwolnienie kierunku (Zwbl)", $"{id} Zwbl{side}", enabled: bs.Dir != 0);
        if (b.SemiAuto) SemiItems(m, id, b, bs, ei, side, "");
        Add(m, bs.StopEnd[ei] ? "Odwołanie zamknięcia wyjazdu (oStop)" : "Zamknięcie wyjazdu na szlak (Stop)", $"{id} {(bs.StopEnd[ei] ? "oStop" : "Stop")}{side}");
        Sep(m, "Polecenia specjalne");
        Add(m, "Awaryjna zmiana kierunku (AZK)", $"{id} AZK{side}", special: true);
        m.IsOpen = true;
    }
    /// <summary>Przyciski blokady półsamoczynnej (KOMPUTERY.pdf 1.6.2): Po, Ko, Poz oraz doraźne dPo, dKo (z licznikami).</summary>
    void SemiItems(ContextMenu m, string id, BlockDef b, BlockInd bs, int i, string side, string where)
    {
        int outw = b.Ends[i].Outward;
        Sep(m, "Blokada półsamoczynna" + where + (bs.Pwl.Length > i && bs.Pwl[i] ? " – Pwl" : ""));
        Add(m, "Blok początkowy (Po)", $"{id} Po{side}", enabled: bs.AwaitEnd == null && bs.Dir == outw);
        Add(m, "Blok końcowy (Ko)" + (bs.KoReady.Length > i && bs.KoReady[i] ? " – wjazd stwierdzony" : ""), $"{id} Ko{side}", enabled: bs.AwaitEnd == i);
        Add(m, "Blok pozwolenia – oddanie pozwolenia (Poz)", $"{id} Poz{side}", enabled: bs.AwaitEnd == null && b.Permission);
        Add(m, "Doraźny blok początkowy (dPo) – wyjazd na Sz / rozkaz", $"{id} dPo{side}", special: true, enabled: bs.AwaitEnd == null);
        Add(m, "Doraźny blok końcowy (dKo) – wjazd na Sz / rozkaz", $"{id} dKo{side}", special: true, enabled: bs.AwaitEnd == i);
    }
    void OnTrackEnd(string id, MouseButtonEventArgs e) { if (Panel.SelStart != null) Dest(id); }
    void OnDerailer(string name, MouseButtonEventArgs e)
    {
        if (!Ind.Ders.TryGetValue(name, out var d)) return; var lb = station.Derailers[name].Label; var m = NewMenu("Wykolejnica " + lb);
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
        if (!ses.IsHost) { Hint.Text = "Tempo symulacji ustawia serwer"; return; }
        var v = double.Parse((string)((Button)sender).Tag);
        if (v == 0) { paused = !paused; } else { speed = v; paused = false; }
    }
    void FitStation_Click(object s, RoutedEventArgs e) => Panel.FitPage();
    void FitAll_Click(object s, RoutedEventArgs e) => Panel.FitPage();
    void Plan_Click(object s, RoutedEventArgs e) => new PlanWindow(ses) { Owner = this }.Show();
    void Routes_Click(object s, RoutedEventArgs e) => new RoutesWindow(ses.RouteTable) { Owner = this }.Show();
    void Book_Click(object s, RoutedEventArgs e) => new BookWindow(ses) { Owner = this }.Show();
    void Instr_Click(object s, RoutedEventArgs e)
    {
        if (sim == null) { MessageBox.Show(this, "Panel instruktora działa na serwerze.", "Instruktor"); return; }
        new InstructorWindow(sim, sc => { var u = ses.Last?.User; Start(sc, u); paused = false; }) { Owner = this }.Show();
    }
    void PlanSt_Click(object s, RoutedEventArgs e)
    {
        if (sim == null) { MessageBox.Show(this, "Plan stacji (podgląd terenu) działa tam, gdzie pracuje komputer zależności – na serwerze.", "Plan stacji"); return; }
        new PlanStationWindow(() => sim) { Owner = this }.Show();
    }
    void Kz_Click(object s, RoutedEventArgs e)
    {
        if (sim == null) { MessageBox.Show(this, "Komputer zależności działa na serwerze – podgląd jest dostępny tam.", "Komputer zależności"); return; }
        new KzWindow(() => sim, () => server) { Owner = this }.Show();
    }
    void Score_Click(object s, RoutedEventArgs e) => new ScoreWindow(ses) { Owner = this }.Show();
    void Help_Click(object s, RoutedEventArgs e) => new HelpWindow() { Owner = this }.Show();
    void Net_Click(object s, RoutedEventArgs e)
    {
        if (netWin != null) { netWin.Activate(); return; }
        netWin = new NetWindow(this) { Owner = this };
        netWin.Closed += (a, b) => netWin = null;
        netWin.Show();
    }
    void Ack_Click(object s, RoutedEventArgs e) { Run("KW"); SortAlarms(); }
    void GridAlarms_DoubleClick(object s, MouseButtonEventArgs e) { if (GridAlarms.SelectedItem is EventRecord a) { a.Ack = true; ses.AckAlarm(a.Id); SortAlarms(); } }

    bool Matches(EventRecord e) => EvFilter.SelectedIndex <= 0 || e.Kategoria == (string)EvFilter.SelectedItem;
    void EvFilter_Changed(object s, SelectionChangedEventArgs e)
    {
        if (ses == null) return;
        events.Clear();
        foreach (var x in ses.Events.Where(Matches).Reverse().Take(3000)) events.Add(x);
    }

    void Export_Click(object s, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog { FileName = $"rejestr_{string.Concat((station.Name ?? "LCS").Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'))}_{(ses.Last?.Clock ?? "").Replace(":", "")}.csv", Filter = "CSV|*.csv" };
        if (dlg.ShowDialog() != true) return;
        var sb = new StringBuilder("Czas;Obszar;Stacja;Typ obiektu;Obiekt;Kategoria;Zdarzenie;Operator;Notatka;Źródło\r\n");
        foreach (var x in ses.Events) sb.Append(string.Join(";", new[] { x.Czas, x.Obszar, x.Stacja, x.TypObiektu, x.Obiekt, x.Kategoria, x.Zdarzenie, x.Operator, x.Notatka, x.Zrodlo }.Select(c => "\"" + (c ?? "").Replace("\"", "\"\"") + "\""))).Append("\r\n");
        File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
    }
}

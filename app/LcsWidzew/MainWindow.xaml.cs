using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Lcs.WorldHost;
using Lcs.Logi;
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
    /// <summary>Port serwisowy tylko na tym komputerze (127.0.0.1) – program Komputerownia (podgląd kart).</summary>
    LcsServer serwis;
    public const int SerwisPort = 47121;
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    SingleplayerHost localWorld;
    double speed = 2;
    bool paused = true;
    int pushTick;
    string scenario = "podstawowy";
    /// <summary>Układ torowy z pliku – dla komputera zależności (station po Bind to zobrazowanie pulpitu).</summary>
    Station layout;
    /// <summary>Dzienniki ZDARZENIA i ALARMY wg ISDR (zakres dat, filtry kolumn, strony, Online).</summary>
    Dziennik zdarzenia, alarmy;
    CheckBox ChkMute; TextBlock AlCount;
    readonly List<string> history = new(); int histIdx;
    SpecialConfirmWindow confirmWin;
    NetWindow netWin;
    readonly HashSet<string> ctlPrompted = new();
    double lastBeep;

    public MainWindow()
    {
        InitializeComponent();
        station = layout = Station.Load(App.StationPath);
        BudujDzienniki();
        Panel.SignalClicked += OnSignal; Panel.SwitchClicked += OnSwitch; Panel.SectionClicked += OnSection;
        Panel.BlockClicked += OnBlock; Panel.TrackEndClicked += OnTrackEnd; Panel.DerailerClicked += OnDerailer;
        Panel.PostClicked += OnPost; Panel.EmptyClicked += e => ClearSel();
        Pol.Przygotowano += p => { Panel.Prepared = true; Panel.Targets.Clear(); Panel.InvalidateVisual(); };
        Pol.Wyslano += Send;
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
        Closed += (s, e) => { timer.Stop(); server?.Dispose(); serwis?.Dispose(); (ses as RemoteSession)?.Dispose(); localWorld?.Dispose(); };
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
            station = layout = st; App.StationPath = path;
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

    void ClearSel()
    {
        Panel.SelStart = null; Panel.SelSwitch = null; Panel.SelVia.Clear(); Panel.Targets.Clear();
        Panel.SelEnd = null; Panel.SelObj = null; Panel.Prepared = false; Panel.HoverTarget = null;
        selKind = null; selId = null; selAll = new(); selRoutes = new(); selEndKey = null;
        Pol?.Wyczysc(); Panel.InvalidateVisual();
    }

    /// <summary>Początek przebiegu: możliwe końce (z punktami pośrednimi) oznaczone na obrazie.</summary>
    void SetStart(string name)
    {
        Panel.SelStart = name; Panel.SelVia.Clear();
        MarkTargets();
        Select("sig", name);
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
        else Start(scenario, new User { Name = "Test", Role = ShotCmds.Contains("#edradmin") ? "administrator" : "dyżurny ruchu" });
        paused = true;
        foreach (var c in ShotCmds)
        {
            TrackHistory();
            if (c.StartsWith("@")) { var until = double.Parse(c[1..], System.Globalization.CultureInfo.InvariantCulture); if (localWorld != null) localWorld.AdvanceTo(until, () => server?.Push()); else Pump(until); continue; }
            if (c == "#overview") { ShowOverview(); continue; }
            if (c == "#opisy") { SetOpisy(true); continue; }
            // „#stan” – do pliku .txt sygnały zezwalające (kody Ie-1) i pociągi z prędkością [km/h]
            if (c == "#stan" && sim != null)
            {
                var sg = sim.Sigs.Where(kv => kv.Value.Sig is not (null or "" or "S1")).Select(kv => $"{Station.Lbl(kv.Key)} {kv.Value.Sig}" + (kv.Value.W21 > 0 ? $"+W21 {kv.Value.W21}" : ""));
                var tr = sim.Trains.List.Where(t => !t.Gone).Select(t => $"{t.Num} {t.V * 3.6:0} km/h ({string.Join(" ", t.Segs.Where(s => s.E != null).Select(s => Station.Lbl(station.Edges[s.E].Sec)).Distinct())})"
                    // jazda wg fizyki (PLAN_EDYTOR_TABORU.md B2): skład, masa, pochylenie pod składem, prędkość docelowa maszynisty, siła i hamowanie
                    + (t.Fiz != null ? $" [{t.Fiz.Nazwa}, {t.Fiz.Masa:0} t, {t.IPod:+0.#;-0.#;0} ‰, cel {t.VCel * 3.6:0} km/h, F {t.Jaz.F:0} kN, ham {t.Jaz.H * 100:0} %]" : ""));
                File.AppendAllText(ShotPath + ".txt", $"@{sim.T:0} sygnały: {string.Join(", ", sg)}; pociągi: {string.Join(", ", tr)}\n");
                continue;
            }
            if (c.StartsWith("#fault=") && sim != null) { var f = c[7..].Split(':', 2); sim.Instruktor.ApplyFault(f[0], f.Length > 1 ? f[1] : "", false); continue; }
            if (c.StartsWith("#page=")) { ShowPage(int.Parse(c[6..])); continue; }
            if (c.StartsWith("#serwis=")) { var er = StartSerwis(int.Parse(c[8..]), sim); File.AppendAllText(ShotPath + ".txt", $"serwis: {er ?? "OK"}{(char)10}"); continue; }
            if (c.StartsWith("#serve=")) { var er = Serve(int.Parse(c[7..]), true); File.AppendAllText(ShotPath + ".txt", $"serwer: {er ?? "OK"}{(char)10}"); continue; }
            if (c.StartsWith("#hold=")) { ShotHold = double.Parse(c[6..], System.Globalization.CultureInfo.InvariantCulture); continue; }
            if (c.StartsWith("#localtest=")) { TestLocalAdapter(c[11..]); continue; }
            if (c == "#leave") { Leave(); continue; }
            if (c.StartsWith("#sleep=")) { var end = DateTime.Now.AddSeconds(double.Parse(c[7..], System.Globalization.CultureInfo.InvariantCulture)); while (DateTime.Now < end) { Dispatcher.Invoke(() => { }, DispatcherPriority.Background); Thread.Sleep(50); } continue; }
            if (c.StartsWith("#phone=")) { ses.Phone("*", c[7..]); continue; }
            if (c.StartsWith("#kz=")) { kzShots.Add(int.Parse(c[4..])); continue; }
            if (c.StartsWith("#tablica=")) { routesShot = c[9..]; continue; }
            if (c.StartsWith("#plan")) { planShot = c.Length > 6 ? c[6..] : ""; continue; }
            if (c.StartsWith("#edrprzyj=") || c.StartsWith("#edrwypr=") || c.StartsWith("#edrpole=")) { EdrShotCmd(c); continue; }
            if (c.StartsWith("#edrklik=")) { edrKlik.Add(c[9..]); continue; }
            if (c == "#edrszlaki") { edrSzlaki = true; continue; }
            if (c.StartsWith("#edrust=")) { edrUst = int.Parse(c[8..]); continue; }
            if (c.StartsWith("#edrtel=")) { EdrTelShot(c[8..]); continue; }
            if (c.StartsWith("#edrdr=") || c.StartsWith("#edrpowrot=") || c.StartsWith("#edrwpis=") || c.StartsWith("#edrspr=") || c.StartsWith("#edrwykresl=")) { EdrE5Shot(c); continue; }
            if (c.StartsWith("#edrdrokno=")) { edrDrOkno = int.Parse(c[11..]); continue; }
            if (c.StartsWith("#edrsprust=")) { edrSprUst = c[11..]; continue; }
            if (c.StartsWith("#edrwyciag=")) { edrWyciag.Add(c[11..]); continue; }
            if (c.StartsWith("#edrpost=")) { edrPost = c[9..]; continue; }
            // E7: „#edradmin” – zalogowany administrator, „#edrkonf=KOD” – okno posterunków i dzienników, „#edrbezwyp” – okno czasu bez wypadku, „#edropis=nr” – opis i trasa pociągu
            if (c == "#edradmin") continue;
            if (c.StartsWith("#edrkonf=")) { edrKonf = c[9..]; continue; }
            if (c == "#edrbezwyp") { edrBezWyp = true; continue; }
            if (c.StartsWith("#edropis=")) { edrOpis = c[9..]; continue; }
            // „#edrwykaz=KOD” – wykaz pociągów posterunku do pliku .txt (kierunki, szlaki, bieg z rozkładu)
            if (c.StartsWith("#edrwykaz=") && sim != null)
            {
                foreach (var p in sim.Edr.Wykaz(c[10..]))
                {
                    var q = sim.Rozklad.Queue.FirstOrDefault(x => $"{x.Num}@{x.T:0}" == p.Klucz);
                    File.AppendAllText(ShotPath + ".txt", $"{p.Nr}/{p.NrWyj} {p.Rodz}|{p.Kod} z={p.ZSzlak}:{p.ZKierunku} w={p.WSzlak}:{p.WKierunku} prz={p.PrzPlan} odj={p.OdjPlan} | From={q?.From} To={q?.To} Start={q?.StartSec} plan={string.Join(",", (q?.PlanPrz ?? new()).OrderBy(x => x.Value).Select(x => x.Key))} term={string.Join(",", Enumerable.Repeat(0, 1).SelectMany(_ => { var l = new List<string>(); for (var t = q?.Duty?.Term ?? q?.Term; t != null; t = t.Then) l.Add($"{t.AtPost}>{t.To}"); return l; }))}{(char)10}");
                }
                continue;
            }
            if (c.StartsWith("#edrtelokno=")) { var a = c[12..].Split('|'); edrTelOkno = int.Parse(a[0]); edrTelWzor = a.Length > 1 ? a[1] : null; continue; }
            if (c == "#edrtelefoniczny") { edrTelefoniczny = true; continue; }
            if (c.StartsWith("#edr")) { edrShot = c.Length > 5 ? int.Parse(c[5..]) : 0; continue; }
            if (c.StartsWith("#sw=")) { Panel.SelSwitch = station.Switches.Values.First(x => x.Label == c[4..] && Panel.Page.Posts.Contains(x.Post)).Num; continue; }
            if (c.StartsWith("#via=")) { ToggleVia(station.Switches.Values.First(x => x.Label == c[5..] && Panel.Page.Posts.Contains(x.Post)).Num); continue; }
            if (c.StartsWith("#koniec="))
            {
                var nm = c[8..];
                var sg = station.Signals.Values.FirstOrDefault(x => x.Label == nm && Panel.Page.Posts.Contains(x.Post));
                Dest(sg?.Name ?? (station.Blocks.ContainsKey(nm) ? nm : station.TrackEnds.Values.FirstOrDefault(x => x.Disp == nm)?.Id ?? nm));
                continue;
            }
            if (c.StartsWith("#hover=")) { var sg = station.Signals.Values.First(x => x.Label == c[7..] && Panel.Page.Posts.Contains(x.Post)); Panel.HoverTarget = Panel.TargetKey("sig", sg.Name); Panel.HoverFixed = true; continue; }
            if (c.StartsWith("#pol=")) { RefreshPanel(); var bt = Pol.Przyciski.FirstOrDefault(x => x.Label == c[5..]); File.AppendAllText(ShotPath + ".txt", $"{c}: {(bt == null ? "BRAK PRZYCISKU" : bt.Enabled ? "dostępny – " + bt.Line : "NIEDOSTĘPNY")}{(char)10}"); if (bt != null) Pol.Wybierz(bt); continue; }
            if (c == "#przyciski") { UpdateTop(); RefreshPanel(); File.AppendAllText(ShotPath + ".txt", $"panel „{Pol.Obiekty}”: {string.Join(" ", Pol.Przyciski.Select(x => x.Label + (x.Enabled ? "" : "(ciemny)")))}; T={ses.Receive()?.T:0.0}; zainicjowane: {ses.Receive()?.Pending?.Line ?? "–"}; okno potwierdzenia: {(confirmWin != null ? "tak" : "nie")}{(char)10}"); continue; }
            if (c == "#wyslij") { var w = Pol.Wybrane; Send(w); File.AppendAllText(ShotPath + ".txt", $"wysłano: {w?.Line} – {CmdRes.Text}{(char)10}"); continue; }
            if (c == "#historia") { Pol.Pokaz(true); continue; }
            // dzienniki: „#filtr=Kategoria:Polecenie|Odrzucone”, „#strona=2”, „#zakres=HH:mm-HH:mm”, „#online”
            if (c.StartsWith("#filtr=")) { var f = c[7..].Split(':', 2); zdarzenia.UstawFiltr(f[0], f[1].Split('|').ToHashSet()); zdarzenia.Tick(); continue; }
            if (c.StartsWith("#zakres=")) { var z = c[8..].Split('-'); zdarzenia.UstawZakres(DateTime.Today + TimeSpan.Parse(z[0]), DateTime.Today + TimeSpan.Parse(z[1])); continue; }
            if (c == "#online") { zdarzenia.UstawOnline(); continue; }
            if (c == "#dziennik") { UpdateLayout(); zdarzenia.Przelicz(); zdarzenia.Tick(); File.AppendAllText(ShotPath + ".txt", $"ZDARZENIA: wpisów {zdarzenia.LiczbaWpisow}, strona {zdarzenia.Strona + 1} / {zdarzenia.Stron}, online: {(zdarzenia.Online ? "tak" : "nie")}{(char)10}"); continue; }
            if (c.StartsWith("#nastawnia=")) { OnPost(c[11..], null); continue; }
            if (c.StartsWith("#obj=")) { var o = c[5..].Split(':', 2); var key = o[0] == "sw" ? station.Switches.Values.First(x => x.Label == o[1] && Panel.Page.Posts.Contains(x.Post)).Num : o[1]; ClearSel(); if (o[0] == "sw") Panel.SelSwitch = key; else Panel.SelObj = key; Select(o[0], key); continue; }
            if (c.StartsWith("#start=")) { SetStart(station.Signals.Values.First(x => x.Label == c[7..] && Panel.Page.Posts.Contains(x.Post)).Name); continue; }
            if (c.StartsWith("#ctl=")) { var r = ses.RequestControl(c[5..]); File.AppendAllText(ShotPath + ".txt", $"przejęcie {c[5..]}: {r ?? "OK"}\n"); continue; }
            var res = Run(c);
            File.AppendAllText(ShotPath + ".txt", $"{c}: {(res.Ok ? "OK" : "ODRZUCONE")} {res.Msg}\n");
        }
        if (localWorld != null) { localWorld.AdvanceTo(ShotSeconds); server?.Push(); }
        else Pump(-1);
        File.WriteAllText(ShotPath + ".world.json", System.Text.Json.JsonSerializer.Serialize(new
        { local = localWorld != null, status = localWorld?.Status, remote = ses is RemoteSession }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        UpdateTop(); RefreshPanel(); TrackHistory(); zdarzenia.Tick(); alarmy.Tick(); Panel.InvalidateVisual();
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
                if (routesShot != null)
                {
                    var rw = new RoutesWindow(ses.RouteTable, sim) { Owner = this }; rw.Show();
                    // „#tablica=filtr;v=60” – dodatkowo wpis prędkości przebiegu (v=0 – usunięcie) i zapis zależności
                    var rs = routesShot.Split(';');
                    if (rs.Length > 1 && rs[1].StartsWith("v=")) rw.UstawV(rs[0], int.Parse(rs[1][2..])); else rw.Pokaz(routesShot);
                    rw.UpdateLayout();
                    Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    var rr = new System.Windows.Media.Imaging.RenderTargetBitmap((int)rw.ActualWidth, (int)rw.ActualHeight, 96, 96, PixelFormats.Pbgra32); rr.Render((Visual)rw.Content);
                    var er = new System.Windows.Media.Imaging.PngBitmapEncoder(); er.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rr));
                    using (var f = File.Create(ShotPath + ".tablica.png")) er.Save(f);
                    rw.Close();
                }
                if (edrShot >= 0)
                {
                    var ew = new EdrWindow(ses) { Owner = this, Width = 1500, Height = 900 }; ew.Show(); ew.Odswiez(true);
                    if (edrPost != null) { ew.WykazPanel.Wybierz(edrPost, null); ew.Odswiez(true); }
                    else if (edrKlik.Count == 0) ew.PokazDziennik(edrShot);
                    foreach (var nr in edrKlik)
                    {
                        var p = ew.WykazPanel.Pociag(nr);
                        if (p != null) ew.DodajZWykazu(ew.WykazPanel.Posterunek, p);
                        File.AppendAllText(ShotPath + ".txt", $"#edrklik={nr}: {(p == null ? "BRAK W WYKAZIE" : $"przyjazd {p.PrzRzecz}, tor {p.TorRzecz}, z {p.ZKierunku} – {ew.Komunikat}")}{(char)10}");
                    }
                    if (edrKlik.Count > 0) ew.WykazPanel.Wybierz(null, edrKlik[^1]);
                    ew.UpdateLayout();
                    Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    var re = new System.Windows.Media.Imaging.RenderTargetBitmap((int)ew.ActualWidth, (int)ew.ActualHeight, 96, 96, PixelFormats.Pbgra32); re.Render((Visual)ew.Content);
                    var ee = new System.Windows.Media.Imaging.PngBitmapEncoder(); ee.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(re));
                    using (var f = File.Create(ShotPath + ".edr.png")) ee.Save(f);
                    if (ew.Dane?.Dzienniki.Count > 0)
                    {
                        // okno przyjęcia pociągu (puste – jak po kliknięciu przycisku nad dziennikiem)
                        var ow = ew.OknoPociagu(ew.Dane.Dzienniki[Math.Min(edrShot, ew.Dane.Dzienniki.Count - 1)], "przyj"); ow.Show(); ow.UpdateLayout();
                        Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                        var ro = new System.Windows.Media.Imaging.RenderTargetBitmap((int)ow.ActualWidth, (int)ow.ActualHeight, 96, 96, PixelFormats.Pbgra32); ro.Render((Visual)ow.Content);
                        var eo = new System.Windows.Media.Imaging.PngBitmapEncoder(); eo.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(ro));
                        using (var f = File.Create(ShotPath + ".edr_przyjecie.png")) eo.Save(f);
                        ow.Close();
                    }
                    void Okno(Window w, string plik)
                    {
                        w.Show(); w.UpdateLayout(); Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                        var rw = new System.Windows.Media.Imaging.RenderTargetBitmap((int)w.ActualWidth, (int)w.ActualHeight, 96, 96, PixelFormats.Pbgra32); rw.Render((Visual)w.Content);
                        var ewn = new System.Windows.Media.Imaging.PngBitmapEncoder(); ewn.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rw));
                        using (var f = File.Create(ShotPath + plik)) ewn.Save(f);
                    }
                    for (int wi = 0; wi < edrWyciag.Count; wi++)
                    {
                        var a = edrWyciag[wi].Split('|');
                        var wy = ew.WyciagOkno(a[0]); wy.Width = 1000; wy.Height = 1180; wy.Show(); wy.Ustaw(a.Length > 1 ? int.Parse(a[1]) : 0, a.Length > 2 && a[2] == "h");
                        Okno(wy, $".edr_wyciag_{wi + 1}.png"); wy.Close();
                    }
                    if (edrKonf != null) { var kw = (OknoPosterunkow)ew.PosterunkiOkno(); kw.Show(); kw.Wybierz(edrKonf); Okno(kw, ".edr_posterunki.png"); kw.Close(); }
                    if (edrBezWyp) { var bw = ew.BezWypadkuOkno(); Okno(bw, ".edr_bezwypadku.png"); bw.Close(); }
                    if (edrOpis != null)
                    {
                        ew.WykazPanel.Wybierz(null, edrOpis);
                        foreach (var (zi, pl) in new[] { (1, ".edr_opis.png"), (2, ".edr_trasa.png") }) { ew.PokazZakladkePociagu(zi); Okno(ew, pl); }
                        ew.PokazZakladkePociagu(0);
                    }
                    if (edrDrOkno >= 0 && ew.Dane?.Dzienniki.Count > edrDrOkno) { var dw = ew.DroznicyOkno(ew.Dane.Dzienniki[edrDrOkno]); Okno(dw, ".edr_droznicy.png"); dw.Close(); }
                    if (edrSprUst != null) { var sw2 = ew.SprawdzeniaUstOkno(edrSprUst); Okno(sw2, ".edr_sprawdzenia.png"); sw2.Close(); }
                    if (edrTelOkno >= 0 && ew.Dane?.Dzienniki.Count > edrTelOkno)
                    {
                        var dz = ew.Dane.Dzienniki[edrTelOkno]; var tw = ew.TelefonogramOkno(dz.Post, dz, false);
                        if (edrTelWzor != null && tw is UI.OknoTelefonogramu ot) ot.WybierzWzor(edrTelWzor);
                        Okno(tw, ".edr_telefonogram.png"); tw.Close();
                    }
                    if (edrTelefoniczny)
                    {
                        ew.PokazTelefoniczny(); ew.Odswiez(true); ew.UpdateLayout(); Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                        var rt = new System.Windows.Media.Imaging.RenderTargetBitmap((int)ew.ActualWidth, (int)ew.ActualHeight, 96, 96, PixelFormats.Pbgra32); rt.Render((Visual)ew.Content);
                        var et = new System.Windows.Media.Imaging.PngBitmapEncoder(); et.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rt));
                        using (var f = File.Create(ShotPath + ".edr_telefoniczny.png")) et.Save(f);
                    }
                    if (edrUst >= 0 && ew.Dane?.Dzienniki.Count > edrUst)
                    {
                        var uw = ew.UstawieniaOkno(ew.Dane.Dzienniki[edrUst], false); uw.Show(); uw.UpdateLayout();
                        Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                        var ru = new System.Windows.Media.Imaging.RenderTargetBitmap((int)uw.ActualWidth, (int)uw.ActualHeight, 96, 96, PixelFormats.Pbgra32); ru.Render((Visual)uw.Content);
                        var eu = new System.Windows.Media.Imaging.PngBitmapEncoder(); eu.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(ru));
                        using (var f = File.Create(ShotPath + ".edr_ustawienia.png")) eu.Save(f);
                        uw.Close();
                    }
                    if (edrSzlaki && ew.Dane != null)
                    {
                        var sw = new SzlakiPosterunku(ew.Dane.Dzienniki.Where(x => x.Post == ew.WykazPanel.Posterunek).ToList()) { Owner = ew }; sw.Show(); sw.UpdateLayout();
                        Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                        var rs = new System.Windows.Media.Imaging.RenderTargetBitmap((int)sw.ActualWidth, (int)sw.ActualHeight, 96, 96, PixelFormats.Pbgra32); rs.Render((Visual)sw.Content);
                        var es = new System.Windows.Media.Imaging.PngBitmapEncoder(); es.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rs));
                        using (var f = File.Create(ShotPath + ".edr_szlaki.png")) es.Save(f);
                        sw.Close();
                    }
                    ew.Close();
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
    string edrKonf, edrOpis; bool edrBezWyp;
    readonly List<int> kzShots = new();
    string planShot, routesShot;
    /// <summary>Tryb zrzutu: „#edr” / „#edr=i” – zrzut okna EDR z zakładką dziennika i (−1 – bez zrzutu).</summary>
    int edrShot = -1;
    /// <summary>Tryb zrzutu: „#edrklik=nr” – dwuklik na pociągu w wykazie (po upływie czasu zrzutu), „#edrszlaki” – zrzut okna „Szlaki posterunku”.</summary>
    readonly List<string> edrKlik = new();
    bool edrSzlaki;
    /// <summary>Tryb zrzutu: „#edrust=i” – zrzut okna ustawień zasadniczych dziennika i (−1 – bez).</summary>
    int edrUst = -1;
    /// <summary>Tryb zrzutu: „#edrtelokno=i” (albo „i|nr wzoru”) – okno telefonogramu dziennika i, „#edrtelefoniczny” – zakładka dziennika telefonicznego.</summary>
    int edrTelOkno = -1;
    /// <summary>„#edrtelokno=i|nr” – wzór telefonogramu wybrany w oknie (E8e).</summary>
    string edrTelWzor;
    /// <summary>Tryb zrzutu E5: „#edrdrokno=i” – okno powiadomienia dróżników dziennika i, „#edrsprust=POST” – konfiguracja sprawdzeń posterunku.</summary>
    int edrDrOkno = -1;
    string edrSprUst;
    /// <summary>Tryb zrzutu E6: „#edrwyciag=rodzaj|i|h” – okno wyciągu (ruchu / telefoniczny / tory), dziennik i, h – z historią; zrzut do `.edr_wyciag_n.png`.</summary>
    readonly List<string> edrWyciag = new();
    /// <summary>Tryb zrzutu: „#edrpost=KOD” – posterunek wybrany u góry okna EDR (wykaz, dzienniki, dziennik telefoniczny, kontrolka).</summary>
    string edrPost;

    /// <summary>
    /// Tryb zrzutu E5: „#edrdr=i|ozn:km:j|ozn:km:d” – dróżnicy w ustawieniach dziennika i (j – godzina, d – ukośnik), „#edrpowrot=i:nr:tor:km” – jazda do km i z powrotem,
    /// „#edrwpis=i|treść” – wpis dowolny, „#edrspr=POST|nazwa|minuty” – wymagane sprawdzenie posterunku; E8d: „#edrwykresl=i|przyczyna” – wykreślenie ostatniego wiersza pociągu dziennika i.
    /// </summary>
    void EdrE5Shot(string c)
    {
        var dane = ses.GetEdr(); CmdResult r;
        var arg = c[(c.IndexOf('=') + 1)..];
        if (c.StartsWith("#edrdr="))
        {
            var a = arg.Split('|'); var d = dane.Dzienniki[int.Parse(a[0])]; var u = d.Ustawienia.Kopia();
            foreach (var x in a.Skip(1)) { var f = x.Split(':'); u.Droznicy.Add(new EdrDroznik { Oznaczenie = f[0], Nazwa = f[0], Km = f[1], WgSzlaku = f[2] == "j" ? "Jednotorowych" : "Dwu i więcej torowych" }); }
            r = ses.EdrCmd(new EdrPolecenie { Dziennik = d.Id, Rodzaj = "ustawienia", Ustawienia = u });
        }
        else if (c.StartsWith("#edrpowrot="))
        {
            var a = arg.Split(':'); var d = dane.Dzienniki[int.Parse(a[0])];
            r = ses.EdrCmd(new EdrPolecenie { Dziennik = d.Id, Rodzaj = "wypr", Pole = "powrot", Wartosc = a[3], Dane = new EdrWersja { Nr = a[1], Tor = a[2], Dw = dane.Zegar[..5], Odj = dane.Zegar[..5] } });
        }
        else if (c.StartsWith("#edrwykresl="))
        {
            var a = arg.Split('|', 2); var d = dane.Dzienniki[int.Parse(a[0])];
            var w = d.Strony[^1].Wiersze.LastOrDefault(x => x.Rodzaj is "przyj" or "wypr" or "wpis");
            r = w == null ? new CmdResult(false, "brak wiersza do wykreślenia") : ses.EdrCmd(new EdrPolecenie { Dziennik = d.Id, Rodzaj = "wykresl", Wiersz = w.Id, Wartosc = a[1] });
        }
        else if (c.StartsWith("#edrwpis="))
        {
            var a = arg.Split('|', 2); r = ses.EdrCmd(new EdrPolecenie { Dziennik = dane.Dzienniki[int.Parse(a[0])].Id, Rodzaj = "wpis", Wartosc = a[1] });
        }
        else
        {
            var a = arg.Split('|'); var l = (dane.SprawdzeniaUst.GetValueOrDefault(a[0]) ?? new()).ToList(); l.Add(new EdrSprawdzenie { Nazwa = a[1], Okres = int.Parse(a[2]) });
            r = ses.EdrCmd(new EdrPolecenie { Rodzaj = "sprawdzenia", Wartosc = a[0], Sprawdzenia = l });
        }
        File.AppendAllText(ShotPath + ".txt", $"{c}: {(r.Ok ? "OK" : "ODRZUCONE")} {r.Msg}{(char)10}");
    }
    bool edrTelefoniczny;

    /// <summary>Tryb zrzutu: „#edrtel=i|treść|odebrał|lewy” – telefonogram nadany z dziennika i (ostatnie – zamknięcie toru lewego); i = -1 – inne okoliczności posterunku.</summary>
    void EdrTelShot(string arg)
    {
        var a = arg.Split('|'); var dane = ses.GetEdr(); int i = int.Parse(a[0]);
        var d = i >= 0 ? dane.Dzienniki[i] : null;
        var t = new EdrTelefonogram { Rodzaj = d == null ? "okolicznosc" : "telefonogram", Tresc = a.Length > 1 ? a[1] : "", Odebral = a.Length > 2 ? a[2] : "" };
        if (d != null && a.Length > 3 && d.Biezace.Tory.FirstOrDefault(x => x.Strona == a[3]) is EdrTorUstawienia tz) t.Zmiany.Add(new EdrZmianaToru { Szlak = tz.Szlak, Status = "Zamknięty" });
        var r = ses.EdrCmd(new EdrPolecenie { Rodzaj = "telefonogram", Dziennik = d?.Id, Wartosc = d?.Post ?? dane.Posterunki.Keys.First(), Tel = t });
        File.AppendAllText(ShotPath + ".txt", $"#edrtel={arg}: {(r.Ok ? "OK" : "ODRZUCONE")} {r.Msg}{(char)10}");
    }

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
        // Przygotowanie na tym samym właścicielu: błąd inicjalizacji nie usuwa poprzedniej gry.
        var next = new SingleplayerHost(layout ?? station, sc, user, Panel.Page?.Id,
            server != null ? "Stanowisko główne (serwer)" : "Stanowisko", App.CreateSimulatorOptions(),
            logger: Diagnostyka.Modul(SingleplayerLog.Module, WorldLog.Version),
            worldLogger: Diagnostyka.Modul(WorldLog.Module, WorldLog.Version));
        scenario = sc;
        if (ses is RemoteSession rs) { rs.Dispose(); }
        CloseWorldWindows(); localWorld?.Dispose(); localWorld = next;
        var s = next.Session.Sim;
        Bind(next.Session);
        server?.Attach(s);
        if (serwis != null) serwis.Attach(s);
        else if (ShotPath == null) StartSerwis(SerwisPort, s);
        s.Log("Zdarzenie", "Start symulacji – scenariusz: " + Rozklad.Scenarios.First(x => x.Key == sc).Name + (user != null ? ", zalogowany: " + user.Name : ""), "System", "");
    }

    void CloseWorldWindows()
    {
        // Okna ze starą sesją lub mutable Simulator nie mogą wprowadzać zmian po resecie epoki.
        foreach (Window window in OwnedWindows.Cast<Window>().Where(w => w is not NetWindow).ToArray()) window.Close();
        confirmWin = null;
    }

    void Bind(ILcsSession x)
    {
        ses = x;
        station = x.L;
        var area = station.Name ?? "LCS";
        TxtArea.Text = area; Title = $"Symulator {area} – komputerowe urządzenia srk (Ie-20)";
        ctlPrompted.Clear();
        zdarzenia.Zrodlo = () => x.Events; alarmy.Zrodlo = () => x.Alarms; zdarzenia.Reset(); alarmy.Reset();
        x.EventAdded += e => { if (ses == x) zdarzenia.NowyWpis(e); };
        x.AlarmAdded += a => { if (ses == x) alarmy.NowyWpis(a); };
        x.AlarmsChanged += () => { if (ses == x) SortAlarms(); };
        x.Beep += () => { if (ses == x && ChkMute.IsChecked != true) System.Media.SystemSounds.Asterisk.Play(); };
        x.SpecialExecuted += () => { if (ses == x) Dispatcher.BeginInvoke(() => new BookWindow(ses) { Owner = this }.Show()); };
        x.PendingExpired += () => { if (ses == x) confirmWin?.Close(); };
        x.Disconnected += why => { if (ses != x) return; MessageBox.Show(this, why + ".\nStanowisko przechodzi w stan „brak danych”. Połącz ponownie w oknie „Stanowiska / sieć” albo uruchom grę jednoosobową.", "Sieć"); };
        x.Reset += () => { if (ses != x) return; var keep = Panel.Page?.Id; Bind(x); BuildTabs(); var pi = station.Pages.FindIndex(p => p.Id == keep); ShowPage(pi >= 0 ? pi : station.Pages.Count - 1); };
        Panel.Sim = x; ClearSel();
        BtnNet.Background = x.IsHost ? (server != null ? new SolidColorBrush(Color.FromRgb(0x2f, 0x5a, 0x2a)) : new SolidColorBrush(Color.FromRgb(0x3a, 0x2a, 0x5a))) : new SolidColorBrush(Color.FromRgb(0x1f, 0x4a, 0x7a));
        UpdateTop(); netWin?.RefreshSession();
    }

    /// <summary>Port serwisowy dla programu Komputerownia (tylko 127.0.0.1); zajęty (drugi symulator) – bez portu.</summary>
    string StartSerwis(int port, Simulator s)
    {
        if (s == null) return "brak komputera zależności";
        try { serwis = new LcsServer(Dispatcher, s, () => ""); serwis.Start(port, true); return null; }
        catch (Exception ex) { serwis = null; return ex.Message; }
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
            if (ses.IsHost && sim.Desks.TryGetValue(ses.DeskId, out var desk)) desk.Name = "Stanowisko główne (serwer)";
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
            serwis?.Dispose(); serwis = null;
            CloseWorldWindows(); localWorld?.Dispose(); localWorld = null;
            (ses as RemoteSession)?.Dispose();
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
        station = layout = Station.Load(App.StationPath); BuildTabs();
        Start(scenario, rs.Last?.User);
        ShowPage(station.Pages.Count - 1);
    }

    public ILcsSession Session => ses;
    public LcsServer Server => server;

    /// <summary>Kolejność na liście alarmów: niepotwierdzone, trwające potwierdzone, nietrwające potwierdzone (Ie-104 § 7 ust. 5).</summary>
    void SortAlarms() => alarmy.Przelicz();

    /// <summary>ZDARZENIA i ALARMY (kolumny jak dotąd; Czas – data i godzina jak w ISDR).</summary>
    void BudujDzienniki()
    {
        zdarzenia = new Dziennik("ZDARZENIA", new SolidColorBrush(Color.FromRgb(0x1f, 0x3d, 0x8a)), "zdarzeń", new (string, string, double)[]
        {
            ("Czas", "Czas", 128), ("Obszar", "Obszar", 100), ("Stacja", "Stacja", 72), ("Typ obiektu", "TypObiektu", 120), ("Obiekt", "Obiekt", 92), ("Kategoria", "Kategoria", 104),
            ("Zdarzenie", "Zdarzenie", 0), ("Operator", "Operator", 100), ("Notatka", "Notatka", 80), ("Źródło", "Zrodlo", 60)
        });
        var exp = new Button { Content = "Eksport CSV", Margin = new Thickness(10, 0, 0, 0) }; exp.Click += Export_Click;
        zdarzenia.Dodatki.Children.Add(exp);
        HostZdarzenia.Content = zdarzenia;
        alarmy = new Dziennik("ALARMY", new SolidColorBrush(Color.FromRgb(0x9a, 0x10, 0x10)), "alarmów", new (string, string, double)[]
        {
            ("Czas", "Czas", 128), ("Obszar", "Obszar", 100), ("Stacja", "Stacja", 72), ("Typ obiektu", "TypObiektu", 120), ("Obiekt", "Obiekt", 92), ("Kategoria", "Kategoria", 104),
            ("Zdarzenie", "Zdarzenie", 0), ("Operator", "Operator", 100), ("Stan alarmu", "Stan", 130)
        });
        // kolejność: niepotwierdzone, trwające potwierdzone, nietrwające potwierdzone (Ie-104 § 7 ust. 5)
        alarmy.Porzadek = x => x.OrderBy(a => a.Ack ? (a.Active ? 1 : 2) : 0).ThenByDescending(a => a.T);
        var rs = new Style(typeof(DataGridRow));
        rs.Setters.Add(new Setter(ForegroundProperty, new SolidColorBrush(Color.FromRgb(0xff, 0x80, 0x80))));
        var tr = new DataTrigger { Binding = new System.Windows.Data.Binding("Ack"), Value = true }; tr.Setters.Add(new Setter(ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77))));
        rs.Triggers.Add(tr);
        alarmy.Grid.RowStyle = rs;
        alarmy.Grid.MouseDoubleClick += GridAlarms_DoubleClick;
        var kw = new Button { Content = "Kwituj wszystkie (KW)", Margin = new Thickness(10, 0, 4, 0) }; kw.Click += Ack_Click;
        ChkMute = new CheckBox { Content = "Wycisz", Foreground = new SolidColorBrush(Color.FromRgb(0xaa, 0xaa, 0xaa)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
        AlCount = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0), Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0x70, 0x70)) };
        alarmy.Dodatki.Children.Add(kw); alarmy.Dodatki.Children.Add(ChkMute); alarmy.Dodatki.Children.Add(AlCount);
        HostAlarmy.Content = alarmy;
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
        var now = Environment.TickCount64 / 1000.0;
        if (localWorld != null && ShotPath == null) localWorld.Frame(paused, speed);
        if (server != null && ++pushTick % 4 == 0) server.Push();        // meldunki do stanowisk sieciowych co 0,2 s
        Panel.InvalidateVisual();
        UpdateTop();
        RefreshPanel(); TrackHistory(); zdarzenia.Tick(); alarmy.Tick();
        // dźwięk co kilka sekund, dopóki istnieje niepotwierdzony alarm; głośniejszy po minucie (Ie-104 § 7 ust. 6)
        var un = ses.Alarms.Where(a => !a.Ack).ToList();
        var T = ses.Last?.T ?? 0;
        if (un.Count > 0 && ChkMute.IsChecked != true && now - lastBeep > 4)
        {
            lastBeep = now;
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
        if (I.Pending == null) panelInit = null;
        // polecenie specjalne zainicjowane w panelu (SZI, ZDP) wykonuje się przyciskiem w panelu (SZ / NSZ, ZDPP) – bez okna potwierdzenia
        if (I.Pending != null && confirmWin == null && I.Pending.Line != panelInit)
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
    public CmdResult Run(string line) => Run(line, null, null, null);

    /// <summary>Polecenie do systemu nadrzędnego i wpis w zakładce „Historia” (Czas, Polecenie, Przyjęcie, Realizacja).</summary>
    CmdResult Run(string line, string hdr, string start, string typ)
    {
        var r = ses.Exec(line);
        if (!string.IsNullOrWhiteSpace(line))
        {
            if (start == null)
            {
                // polecenie wpisane: przebieg – początek z pierwszego adresu na wyświetlanym obrazie
                var t = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var k = t.Select(SystemNadrzedny.KodPolecenia).FirstOrDefault(x => x != null);
                if (k is "POCIAG" or "MANEWR")
                {
                    typ = k == "POCIAG" ? "P" : "M";
                    var f = SystemNadrzedny.Fold(t[0]);
                    start = station.Signals.Values.FirstOrDefault(x => (SystemNadrzedny.Fold(x.Label ?? "") == f || SystemNadrzedny.Fold(x.Name) == f) && (Panel.Page == null || Panel.Page.Posts.Contains(x.Post)))?.Name;
                }
            }
            var I = ses.Receive();
            Pol.Historia.Insert(0, new HistoriaWpis
            {
                Czas = I?.Clock ?? "", Polecenie = hdr ?? line.Trim(), Przyjecie = r.Ok ? "przyjęte" : "odrzucone",
                Realizacja = !r.Ok ? "–" : start != null ? "w realizacji" : r.Msg.Contains("zainicjowane") ? "zainicjowane" : "wykonane",
                Start = r.Ok ? start : null, Typ = typ, Wyslano = I?.T ?? 0
            });
            while (Pol.Historia.Count > 300) Pol.Historia.RemoveAt(Pol.Historia.Count - 1);
        }
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

    // ------------------------------------------------------------------ mysz na pulpicie i panel „Polecenia” (wg ISDR, ROZWOJ.md § 2a)
    // Panel zastępuje menu PPM: klik (LPM albo PPM) wybiera obiekt, panel pokazuje jego polecenia, „Wyślij” przekazuje polecenie.
    /// <summary>Wybór do panelu: „sig”, „route” (para początek–koniec), „sw”, „sec”, „blk”, „der”, „post” – i obiekt.</summary>
    string selKind, selId;
    /// <summary>Wybrana para początek–koniec: przebiegi do wskazanego końca (wszystkie i zgodne z punktami pośrednimi), klucz końca w poleceniu.</summary>
    List<RouteDef> selAll = new(), selRoutes = new(); string selEndKey;
    /// <summary>Wiersz polecenia specjalnego zainicjowanego z panelu (SZI, ZDP) – wykonanie przyciskiem w panelu, bez okna potwierdzenia.</summary>
    string panelInit;

    void OnSignal(string name, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Right) { ClearSel(); Panel.SelObj = name; Select("sig", name); return; }
        if (Panel.SelStart != null && Ctrl) { ToggleVia(name); return; }
        // klik na możliwym końcu przebiegu (semafor albo trójkąt semafora wjazdowego) – para początek–koniec w panelu
        if (Panel.SelStart != null && Panel.SelStart != name && Panel.TargetKey("sig", name) != null) { Dest(name); return; }
        if (Panel.SelStart == name && Panel.SelEnd == null && !Panel.Prepared) { ClearSel(); return; }
        ClearSel();
        if (ses.RouteTable.TryGetValue(name, out var l) && l.Count > 0)
        {
            SetStart(name);
            Hint.Text = $"Początek: {L(name)} – wskaż koniec przebiegu (oznaczone turkusowo). Przebieg wariantowy: najpierw kliknij zwrotnice, przez które ma prowadzić. Esc – rezygnacja.";
        }
        else { Panel.SelObj = name; Select("sig", name); Hint.Text = "Brak przebiegów od " + L(name); }
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

    /// <summary>
    /// Koniec przebiegu (ISDR): koniec w turkusowej ramce, kółka znikają, panel pokazuje polecenia przebiegu (POCIĄG, MANEWR, ZD, ZDP, OPS, ZDPP).
    /// Klik na końcu nie nastawia przebiegu – dopiero polecenie i „Wyślij”.
    /// </summary>
    void Dest(string end)
    {
        var start = Panel.SelStart;
        if (start == null || !ses.RouteTable.TryGetValue(start, out var od)) return;
        var ends = new List<string> { end };
        string frame = end;
        // semafor wjazdowy: także przebiegi na szlak – kończą się przy nim (mały trójkąt końca przebiegu wyjazdowego)
        if (station.Signals.TryGetValue(end, out var sd))
        {
            ends.AddRange(od.Where(r => r.EndNode == sd.Node && r.EndKind is "szlak" or "szlakM").Select(r => r.End).Distinct());
            if (Panel.Targets.Contains("blk:" + sd.Node)) frame = "blk:" + sd.Node;     // koniec na szlak – ramka na trójkącie końca przebiegu wyjazdowego
        }
        else if (station.Blocks.ContainsKey(end))
        {
            var r0 = od.FirstOrDefault(r => r.End == end && r.EndKind is "szlak" or "szlakM");
            if (r0 != null) frame = "blk:" + r0.EndNode;
        }
        var all = od.Where(r => ends.Contains(r.End)).ToList();
        var list = all.Where(r => Panel.SelVia.All(v => Passes(r, v))).ToList();
        bool chain = all.Count == 0 && station.Signals.ContainsKey(end) && od.Any(r => r.Type == "M");
        if (list.Count == 0 && !chain) { Hint.Text = $"Brak przebiegu {L(start)} → {L(end)}{(Panel.SelVia.Count > 0 ? " przez wskazane punkty" : "")}"; return; }
        selAll = all; selRoutes = list; selEndKey = list.FirstOrDefault()?.End ?? end;
        Panel.SelEnd = frame; Panel.Targets.Clear(); Panel.HoverTarget = null;
        selKind = "route"; selId = start;
        RefreshPanel();
        Hint.Text = $"Przebieg {L(start)} → {L(selEndKey)}: wybierz polecenie w panelu i „Wyślij”. Esc – rezygnacja.";
        Panel.InvalidateVisual();
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

    void OnSwitch(string num, MouseButtonEventArgs e)
    {
        // wybrany początek przebiegu: klik na zwrotnicy wskazuje wariant (przebieg ma przez nią prowadzić)
        if (Panel.SelStart != null && Panel.SelEnd == null && e.ChangedButton == MouseButton.Left) { ToggleVia(num); return; }
        ClearSel(); Panel.SelSwitch = num; Select("sw", num);
    }

    void OnSection(string sec, MouseButtonEventArgs e)
    {
        if (Panel.SelStart != null && Ctrl) { ToggleVia(sec); return; }
        if (Panel.SelStart != null && e.ChangedButton == MouseButton.Left) return;      // wybór przebiegu trwa – klik obok symbolu go nie przerywa
        ClearSel(); Panel.SelObj = sec; Select("sec", sec);
    }

    void OnBlock(string id, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && Panel.SelStart != null && Panel.SelEnd == null) { Dest(id); return; }
        ClearSel(); Panel.SelObj = id; Select("blk", id);
    }
    void OnTrackEnd(string id, MouseButtonEventArgs e) { if (Panel.SelStart != null) Dest(id); }
    void OnDerailer(string name, MouseButtonEventArgs e) { ClearSel(); Panel.SelObj = name; Select("der", name); }
    void OnPost(string label, MouseButtonEventArgs e) { ClearSel(); Panel.SelObj = "post:" + label; Select("post", label); }

    void Select(string kind, string id) { selKind = kind; selId = id; RefreshPanel(); Panel.InvalidateVisual(); }

    /// <summary>Panel: przyciski dla wybranego obiektu w bieżącym stanie (odświeżane cyklicznie – np. SZ dostępne 5 s po SZI).</summary>
    void RefreshPanel()
    {
        if (selKind == null || ses == null) { if (Pol.Obiekty != null) Pol.Wyczysc(); return; }
        var (hdr, list, opis) = selKind switch
        {
            "sig" => SigCmds(selId), "route" => RouteCmds(), "sw" => SwitchCmds(selId), "sec" => SecCmds(selId),
            "blk" => BlockCmds(selId), "der" => DerCmds(selId), "post" => PostCmds(selId), _ => (null, null, null)
        };
        if (list == null) { ClearSel(); return; }
        Pol.Ustaw(hdr, list, opis);
    }

    /// <summary>Semafor (zdjęcia ISDR): STOP OSTOP SZ SZI NSZ; SZ i NSZ dostępne dopiero 5 s po SZI (Ie-104 § 11 ust. 13).</summary>
    (string, List<PolecenieBtn>, string) SigCmds(string name)
    {
        var I = Ind; if (!I.Sigs.TryGetValue(name, out var s) || !station.Signals.TryGetValue(name, out var d)) return (null, null, null);
        var p = I.Pending; bool szi = p?.Code == "SZI" && p.Arg == name, po5 = szi && I.T >= p.NotBefore;
        var n = L(name);
        var list = new List<PolecenieBtn>
        {
            new("STOP", $"{n} Stop", !s.Closed, false, "Zastopowanie sygnalizatora (Stop)"),
            new("OSTOP", $"{n} oStop", s.Closed, false, "Odwołanie zastopowania (oStop)"),
            new("SZ", $"{n} SZ", d.IsTrain && po5, false, "Wykonanie: sygnał zastępczy (po SZI, nie wcześniej niż po 5 s)"),
            new("SZI", $"{n} SZI", d.IsTrain && p == null && s.Aspect != Aspect.Train, true, "Inicjalizacja polecenia specjalnego – sygnał zastępczy (1. etap)"),
            new("NSZ", $"{n} NSZ", d.IsTrain && po5 && PodPrad(name), false, "Wykonanie: sygnał zastępczy z W24 – wyjazd na tor szlaku w kierunku przeciwnym do zasadniczego (po SZI)"),
        };
        // dodatkowo (decyzja właściciela 2026-10-20, poza zdjęciami ISDR): Stój, oSZ, ZCZ, oZCZ, ZDM
        var r = I.Routes.Values.FirstOrDefault(x => x.Start == name);
        list.Add(new("STÓJ", $"{n} Stój", s.Aspect is Aspect.Train or Aspect.Shunt or Aspect.Sz, false, "Sygnał „Stój” (przebieg pozostaje utwierdzony)"));
        list.Add(new("OSZ", $"{n} oSZ", s.Aspect == Aspect.Sz, false, "Odwołanie sygnału zastępczego (oSZ)"));
        list.Add(new("ZCZ", $"{n} ZCZ", r != null && r.Type == "P" && !r.TimeRel, false, "Zwolnienie przebiegu pociągowego ze zwłoką czasową (ZCZ)"));
        list.Add(new("OZCZ", $"{n} oZCZ", r != null && r.TimeRel, false, "Przerwanie odliczania czasu zwolnienia (oZCZ)"));
        list.Add(new("ZDM", $"{n} ZDM", r != null && r.Type == "M", false, "Zwolnienie doraźne przebiegu manewrowego (ZDM)"));
        var asp = s.Aspect switch { Aspect.Train => "zezwalający", Aspect.Shunt => "manewrowy", Aspect.Sz => "zastępczy", _ => "„Stój”" };
        return (n, list, $"{(d.Type == "tm" ? "Tarcza" : "Semafor")} {n}: {asp}{(s.Closed ? ", zastopowany" : "")}{(szi ? ", zainicjowano SZI" : "")}");
    }

    /// <summary>Wyjazd z semafora na tor szlaku w kierunku przeciwnym do zasadniczego (warunek NSZ).</summary>
    bool PodPrad(string s) => ses.RouteTable.GetValueOrDefault(s, new()).Any(r => r.Type == "P" && r.EndKind == "szlak" && station.Blocks.TryGetValue(r.End, out var b) && !b.OneWay && b.DefDir != 0 && station.Signals[s].Dir == -b.DefDir);

    /// <summary>Para początek–koniec (zdjęcia ISDR): POCIĄG MANEWR ZD ZDP OPS ZDPP.</summary>
    (string, List<PolecenieBtn>, string) RouteCmds()
    {
        var start = Panel.SelStart; if (start == null) return (null, null, null);
        var I = Ind; var p = I.Pending;
        var rp = selRoutes.Where(r => r.Type == "P").OrderBy(r => r.Variant).FirstOrDefault();
        var rm = selRoutes.Where(r => r.Type == "M").OrderBy(r => r.Variant).FirstOrDefault();
        string Line(RouteDef r, string kod)
        {
            var via = ViaFor(r, selAll.Where(x => x.End == r.End).ToList());
            return $"{L(start)} {string.Join(" ", via.Select(L))}{(via.Count > 0 ? " " : "")}{L(r.End)} {kod}";
        }
        var ends = selAll.Select(r => r.End).Append(selEndKey).ToHashSet();
        var cur = I.Routes.Values.FirstOrDefault(r => r.Start == start && ends.Contains(r.End));
        bool zdp = p?.Code == "ZDP" && p.Arg == start;
        var e = L(selEndKey);
        var list = new List<PolecenieBtn>
        {
            new("POCIĄG", rp != null ? Line(rp, "Pociąg") : "", rp != null && cur == null, false, rp != null ? $"Przebieg pociągowy {(rp.Variant == 0 ? "zasadniczy" : "wariantowy " + rp.Variant)} [{rp.SwText}]" : null),
            new("MANEWR", rm != null ? Line(rm, "Manewr") : $"{L(start)} {e} Manewr", cur == null && (rm != null || (selAll.Count == 0 && station.Signals.ContainsKey(selEndKey))), false, rm != null ? $"Przebieg manewrowy {(rm.Variant == 0 ? "zasadniczy" : "wariantowy " + rm.Variant)} [{rm.SwText}]" : "Przebieg manewrowy złożony"),
            new("ZD", $"{L(start)} {e} ZD", cur != null && !cur.TimeRel, false, "Zwolnienie doraźne – przy pociągu w strefie zbliżania ze zwłoką 90 s"),
            new("ZDP", $"{L(start)} ZDP", cur?.Type == "P" && !cur.TimeRel && p == null, false, "Inicjalizacja zwolnienia doraźnego przebiegu pociągowego (1. etap), wykonanie – ZDPP po 5 s (zwolnienie natychmiastowe)"),
            new("OPS", $"{L(start)} {e} OPS", p != null, false, "Odwołanie zainicjowanego polecenia specjalnego"),
            new("ZDPP", $"{L(start)} ZDPP", zdp && I.T >= p.NotBefore, false, "Wykonanie zwolnienia doraźnego po ZDP – przebieg zwolniony natychmiast"),
        };
        return ($"{L(start)} {e}", list, $"Przebieg {L(start)} → {e}{(cur != null ? " – nastawiony" : "")}");
    }

    /// <summary>Kod polecenia z wiersza (napis przycisku): kod Ie-104 wielkimi literami, strona szlaku jako strzałka.</summary>
    static string CodeLabel(string line)
    {
        var t = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int i = Array.FindIndex(t, x => SystemNadrzedny.KodPolecenia(x) != null); if (i < 0) return line;
        var k = SystemNadrzedny.Codes[SystemNadrzedny.KodPolecenia(t[i])].Code.ToUpperInvariant();
        var side = t.Skip(i + 1).Select(x => x == "E" ? "→" : x == "W" ? "←" : x);
        return string.Join(" ", side.Prepend(k));
    }

    // przyciski dla obiektów, których nie ma na zdjęciach ISDR – kody Ie-104 z dawnego menu (decyzja właściciela 2026-10-20)
    List<PolecenieBtn> NewMenu(string title) { menuTitle = title; return new(); }
    string menuTitle;
    static void Sep(List<PolecenieBtn> m, string t) { }
    static void Add(List<PolecenieBtn> m, string label, string cmd, bool special = false, bool enabled = true) =>
        m.Add(new(CodeLabel(cmd), cmd, enabled, special, label + (special ? " – polecenie specjalne (dwuetapowe)" : "")));     // specjalne – czerwone jak SZI (decyzja właściciela 2026-10-20)

    (string, List<PolecenieBtn>, string) SwitchCmds(string num)
    {
        var I = Ind; if (!I.Sws.TryGetValue(num, out var w)) return (null, null, null);
        var lb = station.Switches[num].Label;
        var m = NewMenu($"Zwrotnica {lb} (położenie {w.Pos})");
        Add(m, "Przestawienie do „+” (Plus)", $"{lb} Plus", enabled: w.Pos != "+" || w.Moving != null);
        Add(m, "Przestawienie do „−” (Minus)", $"{lb} Minus", enabled: w.Pos != "-" || w.Moving != null);
        // kolejność jak na filmie ISDR z 2026-10-27: PLUS MINUS ZMK OZM… / MINUSBZ KSR KSRP …; STOP, OSTOP, ZEROLO – z dawnego menu (spoza filmu)
        var sec = station.Switches[num].Sec; var sc = I.Secs.GetValueOrDefault(sec) ?? new SecInd();
        Add(m, "Zamknięcie ruchowe (Zmk)", $"{lb} Zmk", enabled: !sc.Closed);
        Add(m, "Odwołanie zamknięcia ruchowego (oZmk)", $"{lb} oZmk", enabled: sc.Closed);
        Add(m, "Przestawienie „+” z bocznikowaniem (PlusBZ)", $"{lb} PlusBZ", special: true);
        Add(m, "Przestawienie „−” z bocznikowaniem (MinBZ)", $"{lb} MinBZ", special: true);
        m[^1] = m[^1] with { Label = "MINUSBZ" };                 // napis jak na filmie ISDR
        Add(m, "Kasowanie sygnalizacji rozprucia (KSR)", $"{lb} KSR", special: true, enabled: w.Rozpr);
        Add(m, "Zastopowanie (Stop)", $"{lb} Stop", enabled: !w.Stopped);
        Add(m, "Odwołanie zastopowania (oStop)", $"{lb} oStop", enabled: w.Stopped);
        Add(m, "Zerowanie licznika osi (ZeroLO)", $"{lb} ZeroLO", special: true, enabled: sc.Fault != null);
        return (lb, m, $"{menuTitle}{(w.Flank ? ", zamknięta w ochronie bocznej" : "")}");
    }

    (string, List<PolecenieBtn>, string) SecCmds(string sec)
    {
        var s = Ind.Secs.GetValueOrDefault(sec) ?? new SecInd(); var m = NewMenu("Odcinek " + L(sec));
        Add(m, "Zamknięcie ruchowe toru (Zmk)", $"{L(sec)} Zmk", enabled: !s.Closed);
        Add(m, "Odwołanie zamknięcia ruchowego (oZmk)", $"{L(sec)} oZmk", enabled: s.Closed);
        Add(m, "Zerowanie licznika osi (ZeroLO)", $"{L(sec)} ZeroLO", special: true, enabled: s.Fault is "reset" or "occ");
        return (L(sec), m, menuTitle);
    }

    (string, List<PolecenieBtn>, string) DerCmds(string name)
    {
        if (!Ind.Ders.TryGetValue(name, out var d)) return (null, null, null); var lb = station.Derailers[name].Label; var m = NewMenu("Wykolejnica " + lb);
        Add(m, "Założenie na tor (Plus)", $"{lb} Plus", enabled: !d.On); Add(m, "Zdjęcie z toru (Minus)", $"{lb} Minus", enabled: d.On);
        return (lb, m, menuTitle);
    }

    /// <summary>Posterunek z ikony nastawni: skrót, kod albo nazwa; jeden posterunek na obrazie – ten.</summary>
    string PostOfLabel(string label)
    {
        var f = SystemNadrzedny.Fold(label ?? "");
        foreach (var (k, n) in station.Posts) if (SystemNadrzedny.Fold(k) == f || SystemNadrzedny.Fold(n ?? "") == f || SystemNadrzedny.Fold(Simulator.PostShort(station, k)) == f) return k;
        return Panel.Page?.Posts.Count == 1 ? Panel.Page.Posts[0] : null;
    }

    /// <summary>Ikona nastawni (zdjęcia ISDR): SSS OSSS ZSS OZSS OSZ.</summary>
    (string, List<PolecenieBtn>, string) PostCmds(string label)
    {
        var post = PostOfLabel(label); if (post == null) { Hint.Text = $"Nastawnia „{label}” – nie rozpoznano posterunku"; return (null, null, null); }
        var I = Ind; bool sss = I.Sss.Contains(post), zss = I.Zss.Contains(post);
        var list = new List<PolecenieBtn>
        {
            new("SSS", $"{post} SSS", !sss, false, "Zastopowanie wszystkich sygnalizatorów stacji"),
            new("OSSS", $"{post} OSSS", sss, false, "Odwołanie zastopowania wszystkich sygnalizatorów stacji"),
            new("ZSS", $"{post} ZSS", !zss, false, "Zastopowanie wszystkich zwrotnic stacji"),
            new("OZSS", $"{post} OZSS", zss, false, "Odwołanie zastopowania wszystkich zwrotnic stacji"),
            new("OSZ", $"{post} OSZ", true, false, "Odświeżenie stanu komputera zobrazowania"),
        };
        return (label, list, "Nastawnia " + (station.Posts.GetValueOrDefault(post) ?? post));
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

    (string, List<PolecenieBtn>, string) BlockCmds(string id)
    {
        var b = station.Blocks[id]; if (!Ind.Blocks.TryGetValue(id, out var bs)) return (null, null, null); int ei = EndIdxHere(b);
        string PN(string p) => station.Posts.GetValueOrDefault(p ?? "") ?? p;
        if (!b.Permission)
        {
            // jednokierunkowa / telefoniczne zapowiadanie – bez bloku pozwolenia
            var mj = NewMenu($"Szlak {id} – {b.TypeShort}{(b.OneWay ? (b.DefDir > 0 ? ", kierunek → na wschód" : ", kierunek ← na zachód") : "")}");
            if (b.SemiAuto) foreach (var (en, i) in b.Ends.Select((x, i) => (x, i))) SemiItems(mj, id, b, bs, i, $" {(en.Outward > 0 ? "E" : "W")}", b.Ends.Count > 1 ? " – " + PN(en.Post) : "");
            foreach (var (en, i) in b.Ends.Select((x, i) => (x, i)))
                Add(mj, (bs.StopEnd[i] ? "Odwołanie zamknięcia wyjazdu" : "Zamknięcie wyjazdu na szlak") + (b.Ends.Count > 1 ? " z " + PN(en.Post) : ""), $"{id} {(bs.StopEnd[i] ? "oStop" : "Stop")} {(en.Outward > 0 ? "E" : "W")}");
            return (id, mj, menuTitle);
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
            foreach (var en in b.Ends) Add(mi, $"Awaryjna zmiana kierunku na {(en.Outward > 0 ? "zachód (←)" : "wschód (→)")}", $"{id} AZK {(en.Outward > 0 ? "E" : "W")}", special: true);
            return (id, mi, menuTitle);
        }
        // blokada z blokiem pozwolenia: szlak zewnętrzny albo wewnętrzny, którego drugi koniec obsługuje inne stanowisko
        var dirText = bs.Dir == 0 ? "stan neutralny" : bs.Dir == b.Ends[ei].Outward ? "kierunek WYJAZD" : "kierunek PRZYJAZD";
        var m = NewMenu($"Blokada {id}{(b.Internal ? " (" + PN(b.Ends[ei].Post) + ")" : "")} – {dirText}");
        bool reqFromOther = bs.Req != null && bs.Req.EndIdx != ei, reqMine = bs.Req != null && bs.Req.EndIdx == ei;
        string side = b.Internal ? $" {(b.Ends[ei].Outward > 0 ? "E" : "W")}" : "";
        Add(m, "Włączenie kierunku WYJAZD (Wbl)", $"{id} Wbl{side}", enabled: !reqMine && bs.Dir != b.Ends[ei].Outward);
        Add(m, "Odwołanie żądania (oWbl)", $"{id} oWbl{side}", enabled: reqMine);
        Add(m, "Pozwolenie – kierunek PRZYJAZD (Pzk)", $"{id} Pzk{side}", enabled: reqFromOther);
        Add(m, "Zwolnienie kierunku (Zwbl)", $"{id} Zwbl{side}", enabled: bs.Dir != 0);
        if (b.SemiAuto) SemiItems(m, id, b, bs, ei, side, "");
        Add(m, "Zamknięcie wyjazdu na szlak (Stop)", $"{id} Stop{side}", enabled: !bs.StopEnd[ei]);
        Add(m, "Odwołanie zamknięcia wyjazdu (oStop)", $"{id} oStop{side}", enabled: bs.StopEnd[ei]);
        Add(m, "Awaryjna zmiana kierunku (AZK)", $"{id} AZK{side}", special: true);
        return (id, m, menuTitle);
    }
    /// <summary>Przyciski blokady półsamoczynnej (KOMPUTERY.pdf 1.6.2): Po, Ko, Poz oraz doraźne dPo, dKo (z licznikami).</summary>
    void SemiItems(List<PolecenieBtn> m, string id, BlockDef b, BlockInd bs, int i, string side, string where)
    {
        int outw = b.Ends[i].Outward;
        Add(m, "Blok początkowy (Po)" + where, $"{id} Po{side}", enabled: bs.AwaitEnd == null && bs.Dir == outw);
        Add(m, "Blok końcowy (Ko)" + where + (bs.KoReady.Length > i && bs.KoReady[i] ? " – wjazd stwierdzony" : ""), $"{id} Ko{side}", enabled: bs.AwaitEnd == i);
        Add(m, "Blok pozwolenia – oddanie pozwolenia (Poz)" + where, $"{id} Poz{side}", enabled: bs.AwaitEnd == null && b.Permission);
        Add(m, "Doraźny blok początkowy (dPo) – wyjazd na Sz / rozkaz" + where, $"{id} dPo{side}", special: true, enabled: bs.AwaitEnd == null);
        Add(m, "Doraźny blok końcowy (dKo) – wjazd na Sz / rozkaz" + where, $"{id} dKo{side}", special: true, enabled: bs.AwaitEnd == i);
    }

    /// <summary>„Wyślij” w panelu: polecenie do systemu nadrzędnego, wpis w Historii; ramki znikają.</summary>
    void Send(PolecenieBtn p)
    {
        if (p == null || string.IsNullOrEmpty(p.Line)) return;
        var kod = SystemNadrzedny.KodPolecenia(p.Line.Split(' ').FirstOrDefault(t => SystemNadrzedny.KodPolecenia(t) != null) ?? "");
        bool init = kod is "SZI" or "ZDP";
        if (init) panelInit = p.Line;
        var hdr = $"{p.Label} {Pol.Obiekty}";
        string start = kod is "POCIAG" or "MANEWR" ? Panel.SelStart : null;
        var r = Run(p.Line, hdr, start, kod == "POCIAG" ? "P" : kod == "MANEWR" ? "M" : null);
        if (!r.Ok && init) panelInit = null;
        ClearSel();
        if (r.Ok && kod == "OSZ") Odswiez();
        Pol.Potwierdz(hdr, r.Ok, r.Msg);
    }

    /// <summary>OSZ na nastawni: odświeżenie stanu komputera zobrazowania – nowy telegram stanu, dzienniki i obraz od nowa.</summary>
    void Odswiez()
    {
        Panel.Sim = ses; ClearSel();
        zdarzenia.Przelicz(); alarmy.Przelicz();
        Panel.InvalidateVisual(); UpdateTop();
    }

    /// <summary>Realizacja w Historii: przebieg nastawiony (sygnał / droga utwierdzona) albo niezrealizowany.</summary>
    void TrackHistory()
    {
        var I = ses?.Receive(); if (I == null) return;
        foreach (var h in Pol.Historia)
        {
            if (h.Start == null || h.Realizacja != "w realizacji") continue;
            var r = I.Routes.Values.FirstOrDefault(x => x.Start == h.Start && x.Type == h.Typ);
            if (r != null) { h.Widziany = true; if (r.State != "setting") h.Realizacja = "zrealizowane " + I.Clock; }
            else if (h.Widziany || I.T - h.Wyslano > 30) h.Realizacja = "niezrealizowane";
        }
    }

    // ------------------------------------------------------------------ przyciski
    void Speed_Click(object sender, RoutedEventArgs e)
    {
        if (!ses.IsHost) { Hint.Text = "Tempo symulacji ustawia serwer"; return; }
        var v = double.Parse((string)((Button)sender).Tag);
        if (v == 0) { paused = !paused; } else { speed = v; paused = false; }
    }
    void FitStation_Click(object s, RoutedEventArgs e) => Panel.FitPage();
    void Opisy_Click(object s, RoutedEventArgs e) => SetOpisy(!Panel.ShowOpisy);
    // opisy odcinków na żądanie (Ie-104 § 5 ust. 2) – przycisk podświetlony, gdy są włączone
    void SetOpisy(bool on)
    {
        Panel.ShowOpisy = on;
        BtnOpisy.Background = on ? new SolidColorBrush(Color.FromRgb(0x5a, 0x5a, 0x5a)) : new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26));
        BtnOpisy.BorderBrush = on ? Brushes.Gray : new SolidColorBrush(Color.FromRgb(0x4a, 0x4a, 0x4a));
        Panel.InvalidateVisual();
    }
    void FitAll_Click(object s, RoutedEventArgs e) => Panel.FitPage();
    void Plan_Click(object s, RoutedEventArgs e) => new PlanWindow(ses) { Owner = this }.Show();
    void Routes_Click(object s, RoutedEventArgs e) => new RoutesWindow(ses.RouteTable, ses.IsHost ? sim : null) { Owner = this }.Show();
    void Book_Click(object s, RoutedEventArgs e) => new BookWindow(ses) { Owner = this }.Show();
    EdrWindow edrWin;
    void Edr_Click(object s, RoutedEventArgs e)
    {
        if (edrWin == null || !edrWin.IsLoaded) { edrWin = new EdrWindow(ses) { Owner = this }; edrWin.Closed += (a, b) => edrWin = null; edrWin.Show(); }
        else edrWin.Activate();
    }

    /// <summary>Tryb zrzutu: „#edrprzyj=i:nr:tor”, „#edrwypr=i:nr:tor”, „#edrpole=i:Pole:wartość” (ostatni wiersz pociągu dziennika i; „teraz”, „+N” – minuty).</summary>
    void EdrShotCmd(string c)
    {
        var a = c[(c.IndexOf('=') + 1)..].Split(':');
        var dane = ses.GetEdr(); var d = dane.Dzienniki[int.Parse(a[0])];
        CmdResult r;
        if (c.StartsWith("#edrpole="))
        {
            var w = d.Strony[^1].Wiersze.Last(x => x.Rodzaj != "tekst");
            var v = a[2] == "teraz" ? dane.Zegar[..5] : a[2].StartsWith("+") ? EdrWindow.Przesun(dane.Zegar[..5], 0, int.Parse(a[2][1..]), dane.Zegar[..5]) : string.Join(":", a.Skip(2));
            r = ses.EdrCmd(new EdrPolecenie { Dziennik = d.Id, Rodzaj = "pole", Wiersz = w.Id, Pole = a[1], Wartosc = v });
        }
        else r = ses.EdrCmd(new EdrPolecenie { Dziennik = d.Id, Rodzaj = c.StartsWith("#edrprzyj=") ? "przyj" : "wypr", Dane = new EdrWersja { Nr = a[1], Tor = a.Length > 2 ? a[2] : "", Odj = dane.Zegar[..5] } });
        File.AppendAllText(ShotPath + ".txt", $"{c}: {(r.Ok ? "OK" : "ODRZUCONE")} {r.Msg}{(char)10}");
    }
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
    void GridAlarms_DoubleClick(object s, MouseButtonEventArgs e) { if (alarmy.Grid.SelectedItem is EventRecord a) { a.Ack = true; ses.AckAlarm(a.Id); SortAlarms(); } }

    void Export_Click(object s, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog { FileName = $"rejestr_{string.Concat((station.Name ?? "LCS").Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'))}_{(ses.Last?.Clock ?? "").Replace(":", "")}.csv", Filter = "CSV|*.csv" };
        if (dlg.ShowDialog() != true) return;
        var sb = new StringBuilder("Czas;Obszar;Stacja;Typ obiektu;Obiekt;Kategoria;Zdarzenie;Operator;Notatka;Źródło\r\n");
        foreach (var x in ses.Events) sb.Append(string.Join(";", new[] { x.Czas, x.Obszar, x.Stacja, x.TypObiektu, x.Obiekt, x.Kategoria, x.Zdarzenie, x.Operator, x.Notatka, x.Zrodlo }.Select(c => "\"" + (c ?? "").Replace("\"", "\"\"") + "\""))).Append("\r\n");
        File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
    }
}

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Lcs.Model;
using Pulpit = LcsWidzew.UI.PanelView;
using Stacja = LcsWidzew.Model.Station;
using LcsWidzew.Sim;

namespace Lcs.EdytorZobrazowania;

/// <summary>
/// Podgląd „jak na pulpicie” (etap Z6, PLAN_EDYTOR_ZOBRAZOWANIA.md § 4–5): UT + zobrazowanie → model symulatora (<see cref="DoSymulatora"/>),
/// komputer zależności z rdzenia symulatora (<c>Lcs.Core</c>) i obraz rysowany tym samym kodem pulpitu co w symulatorze
/// (<c>LcsWidzew/UI/PanelView.cs</c>, dołączony jako link). Stan zasadniczy, przykładowy przebieg z tablicy przebiegów, czas płynie.
/// Nic nie zapisuje: ani UT, ani zobrazowania, ani plików symulatora.
/// </summary>
public class PodgladWindow : Window
{
    public readonly Pulpit Panel = new();
    public Stacja Station { get; private set; }
    public Simulator Sim { get; private set; }
    public LocalSession Ses { get; private set; }
    public List<string> Uwagi { get; private set; } = new();
    public readonly ComboBox PageBox = new() { Width = 200, Margin = new Thickness(4, 0, 8, 0) };
    public readonly ComboBox RouteBox = new() { Width = 260, Margin = new Thickness(4, 0, 4, 0) };
    public readonly CheckBox OpisyBox = new() { Content = "Opisy odcinków", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), ToolTip = "Opisy odcinków na żądanie dyżurnego (Ie-104 § 5 ust. 2)" };
    public readonly TextBlock Status = new() { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    public List<RouteDef> Routes { get; private set; } = new();
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    readonly UtProject ut; readonly ZobProject zob;

    public PodgladWindow(UtProject ut, ZobProject zob, string page = null)
    {
        this.ut = ut; this.zob = zob;
        Title = $"Podgląd pulpitu – {ut.Name}"; Width = 1500; Height = 900;
        Background = Brushes.Black; FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
        var bar = new ToolBar { Background = new SolidColorBrush(Color.FromRgb(0xe6, 0xe6, 0xe2)) };
        bar.Items.Add(new TextBlock { Text = "Obraz", VerticalAlignment = VerticalAlignment.Center }); bar.Items.Add(PageBox);
        bar.Items.Add(new TextBlock { Text = "Przebieg", VerticalAlignment = VerticalAlignment.Center }); bar.Items.Add(RouteBox);
        bar.Items.Add(Btn("Nastaw", (s, e) => Nastaw(RouteBox.SelectedIndex), "Nastaw wybrany przebieg (jak polecenie dyżurnego na pulpicie)"));
        bar.Items.Add(Btn("Stan zasadniczy", (s, e) => Build(), "Komputer zależności od nowa – wszystko w stanie zasadniczym"));
        bar.Items.Add(OpisyBox); bar.Items.Add(Status);
        var dock = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top); dock.Children.Add(bar);
        dock.Children.Add(Panel);
        Content = dock;
        PageBox.SelectionChanged += (s, e) => { if (PageBox.SelectedIndex >= 0 && Station != null) ShowPage(PageBox.SelectedIndex); };
        OpisyBox.Click += (s, e) => { Panel.ShowOpisy = OpisyBox.IsChecked == true; Panel.InvalidateVisual(); };
        timer.Tick += (s, e) => { if (Sim == null) return; Sim.Tick(0.1); Panel.InvalidateVisual(); };
        Closed += (s, e) => timer.Stop();
        Build(page);
        Loaded += (s, e) => { Panel.FitPage(); timer.Start(); };
    }

    static Button Btn(string t, RoutedEventHandler h, string tip) { var b = new Button { Content = t, ToolTip = tip, Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(2, 0, 2, 0) }; b.Click += h; return b; }

    /// <summary>Model symulatora z UT i obrazów, nowy komputer zależności w stanie zasadniczym.</summary>
    public void Build(string page = null)
    {
        var keep = page ?? (PageBox.SelectedIndex >= 0 && Station != null ? Station.View.Pages[PageBox.SelectedIndex].Id : null);
        var w = DoSymulatora.Buduj(ut, zob, "edytor zobrazowania – podgląd");
        Uwagi = w.Uwagi;
        Station = Stacja.Parse(w.Json.ToJsonString());
        Sim = new Simulator(Station);
        var desk = Sim.AddDesk("PC", "Podgląd w edytorze zobrazowania", true);
        var user = new User { Name = "Edytor", Role = "dyżurny ruchu" };
        desk.User = user; Sim.User = user;
        Ses = new LocalSession(Sim, desk);
        Panel.Sim = Ses;
        PageBox.Items.Clear();
        foreach (var p in Station.View.Pages) PageBox.Items.Add(p.Name);
        Routes = Sim.RouteTable.Values.SelectMany(v => v).Where(r => r.Variant == 0).OrderBy(r => r.Type).ThenBy(r => Stacja.Lbl(r.Start)).ThenBy(r => Stacja.Lbl(r.End)).ToList();
        RouteBox.Items.Clear();
        foreach (var r in Routes) RouteBox.Items.Add($"{(r.Type == "M" ? "M" : "P")} {Stacja.Lbl(r.Start)} → {Stacja.Lbl(r.End)}");
        if (RouteBox.Items.Count > 0) RouteBox.SelectedIndex = 0;
        int pi = Math.Max(0, Station.View.Pages.FindIndex(p => p.Id == keep));
        PageBox.SelectedIndex = Station.View.Pages.Count > 0 ? pi : -1;
        if (Station.View.Pages.Count > 0) ShowPage(pi);
        Status.Text = $"stan zasadniczy; przebiegów w tablicy {Routes.Count}" + (Uwagi.Count > 0 ? $"; uwagi przekształcenia: {Uwagi.Count}" : "");
        Status.ToolTip = Uwagi.Count > 0 ? string.Join("\n", Uwagi) : null;
    }

    public void ShowPage(int i)
    {
        Panel.Page = Station.View.Pages[i];
        Sim.Page = Panel.Page.Id;
        if (PageBox.SelectedIndex != i) PageBox.SelectedIndex = i;
        Panel.FitPage(); Panel.InvalidateVisual();
    }

    /// <summary>Nastawienie przebiegu z tablicy (polecenie jak z klawiatury pulpitu).</summary>
    public CmdResult Nastaw(int i)
    {
        if (i < 0 || i >= Routes.Count) return null;
        var r = Routes[i];
        var res = Ses.Exec($"{r.Start} {r.End} {(r.Type == "M" ? "Manewr" : "Pociąg")}");
        Status.Text = (res.Ok ? "✓ " : "✗ ") + res.Msg;
        Panel.InvalidateVisual();
        return res;
    }

    /// <summary>Czas symulacji naprzód (testy; w oknie płynie sam).</summary>
    public void Czas(double s) { for (double t = 0; t < s; t += 0.1) Sim.Tick(0.1); Panel.InvalidateVisual(); }

    /// <summary>Zrzut pulpitu (testy, porównanie z edytorem).</summary>
    public void Save(string path)
    {
        UpdateLayout();
        var rtb = new RenderTargetBitmap((int)Math.Max(1, Panel.ActualWidth), (int)Math.Max(1, Panel.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        rtb.Render(Panel);
        var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
        using var f = File.Create(path); enc.Save(f);
    }
}

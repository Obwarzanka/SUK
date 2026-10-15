using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LcsWidzew.Model;
using LcsWidzew.Net;
using LcsWidzew.Sim;

namespace LcsWidzew.UI;

/// <summary>
/// Podgląd komputera zależności (jak terminal serwisowy): stan wewnętrzny niewidoczny na pulpicie – przebiegi i ich
/// zwalnianie, utwierdzenia zwrotnic i odcinków, liczniki czasu, blokady liniowe oraz wymiana telegramów ze stanowiskami.
/// </summary>
public class KzWindow : DarkWindow
{
    public class RouteRow { public string Przebieg { get; set; } public string Stan { get; set; } public string Zwolniono { get; set; } public string Odcinki { get; set; } public string Ochronna { get; set; } public string Boczna { get; set; } public string Czas { get; set; } public string ZCZ { get; set; } public string Stanowisko { get; set; } }
    public class SwRow { public string Zwrotnica { get; set; } public string Polozenie { get; set; } public string Utwierdzona { get; set; } public string Przestawianie { get; set; } public string Stan { get; set; } }
    public class SecRow { public string Odcinek { get; set; } public string Zajetosc { get; set; } public string Utwierdzenie { get; set; } public string Zwalnianie { get; set; } public string Uwagi { get; set; } }
    public class SigRow { public string Sygnalizator { get; set; } public string Sygnal { get; set; } public string Przebieg { get; set; } public string Uwagi { get; set; } }
    public class BlkRow { public string Szlak { get; set; } public string Rodzaj { get; set; } public string Kierunek { get; set; } public string Zadanie { get; set; } public string Odstepy { get; set; } public string Uwagi { get; set; } }

    readonly Func<Simulator> getSim;
    Simulator sim => getSim();
    readonly Func<LcsServer> server;
    readonly DataGrid gRoutes = Grid(), gSw = Grid(), gSec = Grid(), gSig = Grid(), gBlk = Grid();
    readonly TextBlock linkInfo = T("", 12);
    readonly TextBox trace = Box();
    readonly TabControl tabs = new() { Background = Brushes.Black };
    readonly CheckBox onlyActive = new() { Content = "tylko obiekty w stanie innym niż zasadniczy", IsChecked = true, Foreground = Brushes.LightGray, Margin = new Thickness(0, 0, 0, 6) };
    long lastInd; DateTime lastAt = DateTime.Now; double rate;

    public KzWindow(Func<Simulator> sim, Func<LcsServer> server) : base("Podgląd komputera zależności", 1050, 640)
    {
        getSim = sim; this.server = server; ShowInTaskbar = true;
        foreach (var (h, p, w) in new[] { ("Przebieg", "Przebieg", 190.0), ("Stan", "Stan", 200), ("Zwolniono", "Zwolniono", 75), ("Odcinki (utwierdzone)", "Odcinki", double.NaN), ("Dr. ochronna", "Ochronna", 80), ("Ochrona boczna", "Boczna", 130), ("Od nastawienia", "Czas", 90), ("ZCZ", "ZCZ", 60), ("Stanowisko", "Stanowisko", 110) }) Col(gRoutes, h, p, w);
        foreach (var (h, p, w) in new[] { ("Zwrotnica", "Zwrotnica", 80.0), ("Położenie", "Polozenie", 70), ("Utwierdzona w przebiegu", "Utwierdzona", 220), ("Przestawianie", "Przestawianie", 160), ("Stan", "Stan", double.NaN) }) Col(gSw, h, p, w);
        foreach (var (h, p, w) in new[] { ("Odcinek", "Odcinek", 110.0), ("Zajętość", "Zajetosc", 110), ("Utwierdzenie", "Utwierdzenie", 300), ("Zwalnianie czasowe", "Zwalnianie", 120), ("Uwagi", "Uwagi", double.NaN) }) Col(gSec, h, p, w);
        foreach (var (h, p, w) in new[] { ("Sygnalizator", "Sygnalizator", 110.0), ("Sygnał", "Sygnal", 160), ("Przebieg", "Przebieg", 240), ("Uwagi", "Uwagi", double.NaN) }) Col(gSig, h, p, w);
        foreach (var (h, p, w) in new[] { ("Szlak", "Szlak", 60.0), ("Rodzaj", "Rodzaj", 170), ("Kierunek", "Kierunek", 150), ("Żądanie / pozwolenie", "Zadanie", 170), ("Odstępy", "Odstepy", 160), ("Uwagi", "Uwagi", double.NaN) }) Col(gBlk, h, p, w);
        TabItem Tab(string h, UIElement c) => new() { Header = h, Content = c, Foreground = Brushes.Black };
        tabs.Items.Add(Tab("Przebiegi", gRoutes)); tabs.Items.Add(Tab("Zwrotnice", gSw)); tabs.Items.Add(Tab("Odcinki", gSec));
        tabs.Items.Add(Tab("Sygnalizatory", gSig)); tabs.Items.Add(Tab("Blokady liniowe", gBlk));
        trace.IsReadOnly = true; trace.FontFamily = new FontFamily("Consolas"); trace.FontSize = 11.5; trace.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; trace.TextWrapping = TextWrapping.NoWrap;
        var link = new DockPanel { Margin = new Thickness(4) }; DockPanel.SetDock(linkInfo, Dock.Top); link.Children.Add(linkInfo); link.Children.Add(trace);
        tabs.Items.Add(Tab("Łącze ze stanowiskami", link));
        var dock = new DockPanel { Margin = new Thickness(8) };
        var head = new StackPanel(); DockPanel.SetDock(head, Dock.Top);
        head.Children.Add(T("Stan wewnętrzny komputera zależności – stanowisko dyżurnego widzi z tego tylko meldunki (kolory, położenia). Odświeżanie co 0,5 s.", 11, Brushes.Gray));
        head.Children.Add(onlyActive);
        dock.Children.Add(head); dock.Children.Add(tabs); Content = dock;
        var tm = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        tm.Tick += (a, b) => Refresh(); tm.Start(); Closed += (a, b) => tm.Stop();
        Refresh();
    }

    string Secs(double? until) => until is double u ? $"{Math.Max(0, u - sim.T):0} s" : "";
    static string L(string k) => Station.Lbl(k ?? "");
    string RouteName(string uid) => uid != null && sim.Routes.TryGetValue(uid, out var r) ? r.Id : uid ?? "";

    public void ShowTab(int i) { tabs.SelectedIndex = i; Refresh(); }

    void Refresh()
    {
        if (sim == null) return;
        bool only = onlyActive.IsChecked == true;
        switch (tabs.SelectedIndex)
        {
            case 0:
                gRoutes.ItemsSource = sim.Routes.Values.Select(r => new RouteRow
                {
                    Przebieg = r.Id, Stan = r.State switch { "setting" => "nastawianie (zwrotnice / warunki)", "set" => "utwierdzony, sygnał zezwalający", "passed" => "przejechany – zwalnianie odcinkami", "stopped" => "sygnał „Stój” – przebieg utwierdzony", _ => r.State } + (r.ManualStop ? ", zatrzymany ręcznie" : ""),
                    Zwolniono = $"{r.Rel}/{r.Secs.Count}", Odcinki = string.Join(" ", r.Secs.Skip(r.Rel).Select(L)), Ochronna = L(r.Overlap), Boczna = r.FlankText, Czas = $"{sim.T - r.T0:0} s", ZCZ = Secs(r.TimeRelUntil),
                    Stanowisko = sim.Desks.Values.FirstOrDefault(d => sim.Control.GetValueOrDefault(Station.PostOf(r.Start)) == d.Id)?.Name ?? ""
                }).ToList();
                break;
            case 1:
                gSw.ItemsSource = sim.Sws.Values.Where(w => !only || w.Lock != null || w.Flank.Count > 0 || w.Moving != null || w.Stopped || w.Rozpr || w.NoCtrl || w.Frozen || w.Local || w.NoData).OrderBy(w => w.Num).Select(w => new SwRow
                {
                    Zwrotnica = L(w.Num), Polozenie = w.Pos, Utwierdzona = RouteName(w.Lock) + (w.Flank.Count > 0 ? (w.Lock != null ? "; " : "") + "ochrona boczna: " + string.Join(", ", w.Flank.Select(RouteName)) : ""),
                    Przestawianie = w.Moving == null ? "" : $"→ „{w.Moving.Target}”, {Math.Max(0, w.Moving.Until - sim.T):0.0} s" + (w.Frozen ? " (nie przestawia się!)" : ""),
                    Stan = string.Join(", ", new[] { w.Stopped ? "zastopowana" : null, w.Rozpr ? "ROZPRUCIE" : null, w.NoCtrl ? "brak kontroli" : null, w.Frozen ? "zamarznięta" : null, w.Local ? "sterowanie miejscowe" : null, w.NoData ? "brak danych" : null }.Where(x => x != null))
                }).ToList();
                break;
            case 2:
                gSec.ItemsSource = sim.Secs.Values.Where(s => !only || s.TrainOcc || s.Lock != null || s.Fault != null || s.Closed || s.TimeRel != null || s.NoData).OrderBy(s => s.Id).Select(s => new SecRow
                {
                    Odcinek = L(s.Id), Zajetosc = s.TrainOcc ? "zajęty" : s.Fault == "occ" ? "zajęty (usterka)" : "wolny",
                    Utwierdzenie = s.LockType == null ? "" : (s.LockType switch { "train" => "pociągowy", "shunt" => "manewrowy", "overlap" => "droga ochronna", _ => s.LockType }) + " – " + RouteName(s.Lock),
                    Zwalnianie = Secs(s.TimeRel),
                    Uwagi = string.Join(", ", new[] { s.Closed ? "zamknięty ruchowo" : null, s.Fault == "reset" ? "wymaga ZeroLO" : null, s.Fault == "await" ? "czeka na 1. przejazd" : null, s.NoData ? "brak danych" : null }.Where(x => x != null))
                }).ToList();
                break;
            case 3:
                gSig.ItemsSource = sim.Sigs.Values.Where(s => sim.L.Signals[s.Name].Type is not ("odstepowy" or "to" or "sp" or "pk") && (!only || s.Aspect != Aspect.Stop || s.Route != null || s.RouteEnd != null || s.Closed || s.LampFault || s.NoData)).OrderBy(s => s.Name).Select(s => new SigRow
                {
                    Sygnalizator = L(s.Name), Sygnal = s.Aspect switch { Aspect.Train => "zezwalający pociągowy", Aspect.Shunt => "zezwalający manewrowy", Aspect.Sz => $"zastępczy ({Math.Max(0, s.SzUntil - sim.T):0} s)", _ => "„Stój”" },
                    Przebieg = s.Route != null ? "początek: " + RouteName(s.Route) : s.RouteEnd != null ? "koniec: " + RouteName(s.RouteEnd) : "",
                    Uwagi = string.Join(", ", new[] { s.Closed ? "zastopowany" : null, s.LampFault ? "uszkodzenie lampy" : null, s.NoData ? "brak komunikacji" : null, s.Dark ? "wygaszony" : null }.Where(x => x != null))
                }).ToList();
                break;
            case 4:
                gBlk.ItemsSource = sim.L.Blocks.Values.Select(b => (b, s: sim.Blocks[b.Id])).Where(x => !only || x.s.Dir != x.b.DefDir || x.s.Req != null || x.s.Busy || x.s.AwaitEnd != null || x.b.SecsWE.Any(sim.SecOcc) || x.s.StopEnd.Any(v => v)).Select(x => new BlkRow
                {
                    Szlak = x.b.Id, Rodzaj = x.b.TypeShort, Kierunek = x.s.Dir == 0 ? "neutralny" : x.s.Dir > 0 ? "→ na wschód" : "← na zachód",
                    Zadanie = x.s.Req == null ? "" : $"żądanie od {(x.s.Req.EndIdx >= 0 && x.s.Req.EndIdx < x.b.Ends.Count ? sim.PostName(x.b.Ends[x.s.Req.EndIdx].Post) : "posterunku sąsiedniego")}, {sim.T - x.s.Req.At:0} s",
                    Odstepy = string.Join(" ", x.b.SecsWE.Select(sc => sim.SecOcc(sc) ? "■" : "□")),
                    Uwagi = string.Join(", ", new[] { x.s.ReleaseUntil != null ? $"zwalnianie {Secs(x.s.ReleaseUntil)}" : null, x.s.AzkUntil != null ? $"AZK {Secs(x.s.AzkUntil)}" : null, x.s.AwaitEnd != null ? "czeka na blok końcowy (Ko)" : null, x.s.Pwl.Any(v => v) ? "Pwl" : null, x.s.DPoCount + x.s.DKoCount > 0 ? $"liczniki dPo {x.s.DPoCount} / dKo {x.s.DKoCount}" : null, x.b.SemiAuto ? null : sim.KTor(x.b) ? "kTor" : null, x.s.StopEnd.Any(v => v) ? "wyjazd zamknięty" : null, x.s.NbrSilent ? "sąsiad nie odpowiada" : null }.Where(v => v != null))
                }).ToList();
                break;
            case 5:
            {
                var now = DateTime.Now; var dt = (now - lastAt).TotalSeconds;
                if (dt > 0.2) { rate = (sim.IndCount - lastInd) / dt; lastInd = sim.IndCount; lastAt = now; }
                var srv = server();
                var desks = string.Join("\n", sim.Desks.Values.Select(d => $"   {d.Name}{(d.Host ? " (serwer)" : "")} – {d.User?.Name ?? "nikt nie zalogowany"}; steruje: {string.Join(", ", sim.Control.Where(c => c.Value == d.Id).Select(c => sim.PostName(c.Key)))}"));
                linkInfo.Text = $"Łącze: {(sim.LinkDown ? "USZKODZONE (usterka instruktora)" : "sprawne")}. Telegramy stanu: {rate:0.0}/s" +
                    (srv != null ? $"; serwer port {srv.Port}, ostatni telegram {srv.LastSize / 1024.0:0.0} kB, wysłano łącznie {srv.BytesOut / 1048576.0:0.0} MB" + string.Concat(srv.Stations().Select(s => $"\n   → {s.Name}: {s.Bytes / 1048576.0:0.0} MB")) : "; gra jednoosobowa (stanowisko w tym programie)") +
                    $"\nStanowiska:\n{desks}\nWymiana poleceń (→ do komputera zależności, ← odpowiedź):";
                var txt = string.Join("\n", sim.LinkTrace.TakeLast(200));
                if (trace.Text != txt) { trace.Text = txt; trace.ScrollToEnd(); }
                break;
            }
        }
    }
}

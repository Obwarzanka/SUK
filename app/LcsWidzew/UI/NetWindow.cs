using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LcsWidzew.Net;
using LcsWidzew.Sim;

namespace LcsWidzew.UI;

/// <summary>
/// Stanowiska i sieć: serwer (komputer zależności na tym komputerze) albo przyłączenie do serwera (np. przez Radmin VPN),
/// obszary sterowania posterunków (przejęcie / oddanie) i telefon między stanowiskami.
/// </summary>
public class NetWindow : DarkWindow
{
    readonly MainWindow main;
    readonly StackPanel conn = new(), ctl = new(), phone = new();
    readonly DispatcherTimer tm = new() { Interval = TimeSpan.FromSeconds(1) };
    string lastCtlKey;
    ILcsSession lastCtlSession;

    public NetWindow(MainWindow main) : base("Stanowiska i sieć", 560, 700)
    {
        this.main = main;
        var sp = new StackPanel { Margin = new Thickness(14) };
        sp.Children.Add(T("Połączenie", 14, Brushes.White, true)); sp.Children.Add(conn);
        sp.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 8) });
        sp.Children.Add(T("Obszary sterowania (posterunki)", 14, Brushes.White, true));
        sp.Children.Add(T("Stanowisko wydaje polecenia tylko dla posterunków, którymi steruje. Przejęcie posterunku obsadzonego przez inne stanowisko wymaga jego zgody; nieobsadzony – od razu. Po rozłączeniu stanowiska jego posterunki przejmuje serwer.", 10.5, Brushes.Gray));
        sp.Children.Add(ctl);
        sp.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 8) });
        sp.Children.Add(T("Telefon między stanowiskami", 14, Brushes.White, true));
        sp.Children.Add(T("Zapowiadanie pociągów, uzgodnienia – rozmowy trafiają do rejestratora (kategoria „Telefon”).", 10.5, Brushes.Gray));
        sp.Children.Add(phone);
        Content = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        BuildConn(); BuildPhone(); RefreshCtl();
        tm.Tick += (a, b) => { RefreshCtl(); RefreshPhone(); };
        tm.Start(); Closed += (a, b) => tm.Stop();
    }

    static IEnumerable<string> LocalIps()
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback))
            foreach (var a in ni.GetIPProperties().UnicastAddresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork))
                yield return $"{a.Address}  ({ni.Name})";
    }

    void BuildConn()
    {
        conn.Children.Clear();
        var ses = main.Session;
        if (ses is RemoteSession rs)
        {
            conn.Children.Add(T($"Stanowisko sieciowe „{rs.DeskName}” – połączone z serwerem {rs.Server}.", 12, Brushes.LightGreen));
            conn.Children.Add(Btn("Rozłącz (wróć do gry jednoosobowej)", (a, b) => { main.Leave(); BuildConn(); BuildPhone(); }));
            return;
        }
        if (main.Server != null)
        {
            conn.Children.Add(T($"Serwer działa – port {main.Server.Port}. Pozostali uruchamiają symulator, „Stanowiska / sieć” → Połącz, wpisując jeden z adresów:", 12, Brushes.LightGreen));
            foreach (var ip in LocalIps()) conn.Children.Add(T("   " + ip + (ip.StartsWith("26.") ? "   ← Radmin VPN" : ""), 12, Brushes.White));
            conn.Children.Add(T("Zapora Windows przy pierwszym uruchomieniu zapyta o dostęp do sieci – zezwól (sieci prywatne).", 10.5, Brushes.Gray));
            conn.Children.Add(Btn("Zatrzymaj serwer", (a, b) => { main.StopServer(); BuildConn(); }));
            return;
        }
        // gra jednoosobowa: uruchom serwer albo przyłącz się
        conn.Children.Add(T("Gra wieloosobowa: jeden komputer jest serwerem (komputer zależności, pociągi, rozkład), pozostali przyłączają się jako stanowiska dyżurnych. Przez Radmin VPN wpisz adres serwera z Radmina (26.x.x.x).", 11, Brushes.Gray));
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var port = Box(Wire.DefaultPort.ToString()); port.Width = 70;
        row.Children.Add(new TextBlock { Text = "Port: ", Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center }); row.Children.Add(port);
        row.Children.Add(Btn("Uruchom serwer na tym komputerze", (a, b) =>
        {
            var err = main.Serve(int.TryParse(port.Text, out var p) ? p : Wire.DefaultPort);
            if (err != null) MessageBox.Show(this, err, "Serwer"); BuildConn();
        }, new SolidColorBrush(Color.FromRgb(0x2f, 0x5a, 0x2a))));
        conn.Children.Add(row);
        conn.Children.Add(T("Przyłączenie do serwera:", 12, Brushes.White, true));
        var g = new System.Windows.Controls.Grid(); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) }); g.ColumnDefinitions.Add(new ColumnDefinition());
        TextBox Field(string label, string val, int r)
        {
            g.RowDefinitions.Add(new RowDefinition());
            var l = new TextBlock { Text = label, Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center }; System.Windows.Controls.Grid.SetRow(l, r); g.Children.Add(l);
            var b = Box(val); System.Windows.Controls.Grid.SetRow(b, r); System.Windows.Controls.Grid.SetColumn(b, 1); g.Children.Add(b); return b;
        }
        var host = Field("Adres serwera (IP)", Properties.LastHost, 0);
        var jport = Field("Port", Wire.DefaultPort.ToString(), 1);
        var name = Field("Nazwa stanowiska", Properties.LastName, 2);
        conn.Children.Add(g);
        conn.Children.Add(Btn("Połącz", (a, b) =>
        {
            Properties.LastHost = host.Text.Trim(); Properties.LastName = name.Text.Trim();
            Cursor = System.Windows.Input.Cursors.Wait;
            var err = main.Join(host.Text.Trim(), int.TryParse(jport.Text, out var p) ? p : Wire.DefaultPort, name.Text.Trim());
            Cursor = null;
            if (err != null) MessageBox.Show(this, err + "\n\nSprawdź adres, czy serwer działa i czy zapora na serwerze przepuszcza port " + jport.Text + ".", "Połączenie");
            BuildConn(); BuildPhone(); RefreshCtl();
        }, new SolidColorBrush(Color.FromRgb(0x1f, 0x4a, 0x7a))));
    }

    void RefreshCtl()
    {
        var ses = main.Session; var I = ses?.Receive(); if (I == null) return;
        var key = string.Join(";", I.Control.Select(c => c.Key + "=" + c.Value)) + "|" + string.Join(";", I.Desks.Select(d => d.Id + d.User));
        if (key == lastCtlKey && ReferenceEquals(ses, lastCtlSession)) return;
        lastCtlKey = key; lastCtlSession = ses;
        ctl.Children.Clear();
        string DeskText(string id) { var d = I.Desks.FirstOrDefault(x => x.Id == id); return d == null ? "nieobsadzony" : $"{d.Name}{(d.User != null ? " – " + d.User : "")}"; }
        foreach (var (post, pname) in ses.L.Posts)
        {
            var owner = I.Control.GetValueOrDefault(post); bool mine = owner == ses.DeskId;
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            var btn = mine
                ? Btn("Oddaj", (a, b) => { ses.ReleaseControl(post); lastCtlKey = null; })
                : Btn("Przejmij", (a, b) => { var r = ses.RequestControl(post); if (r != null) MessageBox.Show(this, r, "Przejęcie sterowania"); lastCtlKey = null; });
            if (mine && I.Desks.Count < 2) btn.IsEnabled = false;
            DockPanel.SetDock(btn, Dock.Right); row.Children.Add(btn);
            row.Children.Add(new TextBlock { Text = $"{pname} ({post}) – steruje: {(mine ? "to stanowisko" : DeskText(owner))}", Foreground = mine ? Brushes.LightGreen : Brushes.White, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
            ctl.Children.Add(row);
        }
        if (I.Desks.Count > 0) ctl.Children.Add(T("Stanowiska: " + string.Join(", ", I.Desks.Select(d => d.Name + (d.User != null ? $" ({d.User})" : "") + (d.Id == ses.DeskId ? " – to" : ""))), 10.5, Brushes.Gray));
    }

    internal void RefreshSession()
    {
        BuildConn(); RefreshCtl(); RefreshPhone();
    }

    ComboBox phoneTo; TextBlock phoneLog;
    void BuildPhone()
    {
        phone.Children.Clear();
        phoneTo = new ComboBox { Margin = new Thickness(0, 2, 0, 4) };
        var text = Box();
        var row = new DockPanel();
        var send = Btn("Wyślij", (a, b) =>
        {
            if (text.Text.Trim() == "") return;
            var to = phoneTo.SelectedItem is ComboBoxItem ci ? (string)ci.Tag : "*";
            main.Session.Phone(to, text.Text.Trim()); text.Text = "";
        });
        text.KeyDown += (a, e) => { if (e.Key == System.Windows.Input.Key.Enter) send.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); };
        DockPanel.SetDock(send, Dock.Right); row.Children.Add(send); row.Children.Add(text);
        phone.Children.Add(phoneTo); phone.Children.Add(row);
        phoneLog = T("", 11.5, Brushes.LightGray); phone.Children.Add(phoneLog);
        RefreshPhone();
    }

    void RefreshPhone()
    {
        var ses = main.Session; var I = ses?.Receive(); if (I == null || phoneTo == null) return;
        var want = new[] { ("*", "do wszystkich stanowisk") }.Concat(I.Desks.Where(d => d.Id != ses.DeskId).Select(d => (d.Id, d.Name + (d.User != null ? $" ({d.User})" : "")))).ToList();
        var have = phoneTo.Items.OfType<ComboBoxItem>().Select(x => (string)x.Tag).ToList();
        if (!have.SequenceEqual(want.Select(w => w.Item1)))
        {
            var sel = phoneTo.SelectedItem is ComboBoxItem c ? (string)c.Tag : "*";
            phoneTo.Items.Clear();
            foreach (var (id, n) in want) phoneTo.Items.Add(new ComboBoxItem { Content = n, Tag = id });
            phoneTo.SelectedIndex = Math.Max(0, want.FindIndex(w => w.Item1 == sel));
        }
        phoneLog.Text = string.Join("\n", ses.Events.Where(e => e.Kategoria == "Telefon").TakeLast(12).Select(e => $"{e.Czas}  {e.Zdarzenie}"));
    }

    /// <summary>Ostatnio użyty adres i nazwa (na czas działania programu).</summary>
    static class Properties { public static string LastHost = "26.", LastName = Environment.UserName; }
}

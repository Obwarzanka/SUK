using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Urządzenia w oknie: rodzaj na pasku, nazwa z okna (podpowiedź kolejnej wolnej), panel urządzenia (nazwa, km, kierunek, strona,
/// komory), menu PPM, usuwanie.
/// </summary>
public partial class MainWindow
{
    public readonly ComboBox DevKindBox = new() { Width = 150, ToolTip = "Rodzaj urządzenia (Ig-10 zał. 8)" };
    /// <summary>Test: nazwa zamiast okna (null – podpowiedź).</summary>
    public Func<string, string> AskNameForTest;

    void InitUrzadzenia()
    {
        foreach (var k in UtDevice.Kinds) DevKindBox.Items.Add(k.Name);
        DevKindBox.SelectedIndex = 0;
        DevKindBox.SelectionChanged += (s, e) =>
        {
            if (DevKindBox.SelectedIndex < 0) return;
            View.DevKind = UtDevice.Kinds[DevKindBox.SelectedIndex].Key;
            if (View.Tool == UtTool.Urzadzenie) ToolShown(); else SetTool(UtTool.Urzadzenie);
        };
        View.DevicePlace += OnDevicePlace;
        View.DeviceMoved += (id, s) => ChDevice(id, "Przesunięcie urządzenia", d => d.S = s);
        View.DeviceMenu += id => { var cm = BuildDeviceMenu(id); cm.PlacementTarget = View; cm.IsOpen = true; };
    }

    void ChDevice(string id, string what, Action<UtDevice> act) => Doc.Change(what, p => { var d = p.Devices.FirstOrDefault(x => x.Id == id); if (d != null) act(d); });

    string AskName(string title, string label, string def)
    {
        if (AskNameForTest != null) return AskNameForTest(def) ?? def;
        if (NoPrompt) return def;
        var dlg = new Window { Title = title, Owner = this, SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, FontFamily = FontFamily, FontSize = 13 };
        var sp = new StackPanel { Margin = new Thickness(14) };
        sp.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 2) });
        var tb = new TextBox { Text = def, Width = 160 }; sp.Children.Add(tb);
        var ok = new Button { Content = "OK", IsDefault = true, Width = 80, Margin = new Thickness(0, 12, 6, 0) };
        var cancel = new Button { Content = "Anuluj", IsCancel = true, Width = 80, Margin = new Thickness(0, 12, 0, 0) };
        ok.Click += (s, e) => dlg.DialogResult = true;
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; row.Children.Add(ok); row.Children.Add(cancel);
        sp.Children.Add(row); dlg.Content = sp;
        dlg.Loaded += (s, e) => { tb.Focus(); tb.SelectAll(); };
        return dlg.ShowDialog() == true ? tb.Text.Trim() : null;
    }

    void OnDevicePlace(string track, double s, int dir, int side)
    {
        var kind = View.DevKind; var info = UtDevice.KindInfo(kind);
        string name = "";
        if (!string.IsNullOrEmpty(info.Prefix) || kind == UtDevice.Semafor)
        {
            name = AskName(info.Name, $"Nazwa ({info.Name.ToLower()})", Urzadzenia.NextName(Doc.P, kind));
            if (name == null) return;
            if (name.Length > 0 && Doc.P.Devices.Any(x => x.Name == name)) { Message($"Nazwa {name} jest już zajęta – urządzenie nie zostało wstawione"); return; }
        }
        var d = new UtDevice { Kind = kind, Name = name, Track = track, S = Math.Round(s, 2), Dir = dir, Side = side };
        Doc.Change(info.Name, p => p.Devices.Add(d));
        View.SelectDevice(d.Id);
        var tr = Doc.P.Tracks.FirstOrDefault(x => x.Id == track);
        Message($"{info.Name} {name} na torze {tr?.Number} km {F(d.S)}" + (info.Directed && kind != UtDevice.Koziol ? $", dla jazdy w stronę {(dir > 0 ? "rosnącego" : "malejącego")} km, po {(side > 0 ? "prawej" : "lewej")}" : ""));
    }

    /// <summary>Usunięcie zaznaczonego urządzenia (Del).</summary>
    bool DeleteDevice()
    {
        if (View.SelectedDevice is not string id || Doc.P.Devices.FirstOrDefault(x => x.Id == id) is not UtDevice d) return false;
        Doc.Change("Usunięcie urządzenia", p => p.Devices.RemoveAll(x => x.Id == id));
        View.Select(null); Message($"{UtDevice.KindInfo(d.Kind).Name} {d.Name} usunięty (Ctrl+Z przywraca)");
        return true;
    }

    public ContextMenu BuildDeviceMenu(string id)
    {
        var cm = new ContextMenu();
        var d = Doc.P.Devices.FirstOrDefault(x => x.Id == id); if (d == null) return cm;
        MenuItem Item(string h, Action a, bool en = true) { var mi = new MenuItem { Header = h, IsEnabled = en }; mi.Click += (s, e) => a(); cm.Items.Add(mi); return mi; }
        cm.Items.Add(new MenuItem { Header = $"{UtDevice.KindInfo(d.Kind).Name} {d.Name}", IsEnabled = false, FontWeight = FontWeights.SemiBold });
        Item("Zmień stronę", () => ChDevice(id, "Strona urządzenia", x => x.Side = -x.Side));
        Item("Odwróć kierunek", () => ChDevice(id, "Kierunek urządzenia", x => x.Dir = -x.Dir), d.Directed);
        cm.Items.Add(new Separator());
        Item("Usuń", () => { View.SelectDevice(id); DeleteDevice(); });
        return cm;
    }

    void BuildDevicePanel(UtDevice d)
    {
        var P = TrackPanel; var info = UtDevice.KindInfo(d.Kind);
        P.Children.Add(Header($"{info.Name} {d.Name}"));
        P.Children.Add(new TextBlock { Text = $"Ig-10 {info.Ig10}", Foreground = Brushes.DimGray, FontSize = 12, Margin = new Thickness(0, -4, 0, 6) });
        var name = new TextBox { Text = d.Name, Margin = new Thickness(0, 2, 0, 6) };
        void CommitName()
        {
            var n = name.Text.Trim(); if (n == d.Name) return;
            if (n.Length > 0 && Doc.P.Devices.Any(x => x.Id != d.Id && x.Name == n)) { Message($"Nazwa {n} jest już zajęta"); name.Text = d.Name; return; }
            ChDevice(d.Id, "Nazwa urządzenia", x => x.Name = n);
        }
        name.LostFocus += (s, e) => CommitName(); name.KeyDown += (s, e) => { if (e.Key == Key.Enter) CommitName(); };
        P.Children.Add(Label("Nazwa")); P.Children.Add(name);
        var tr = Doc.P.Tracks.FirstOrDefault(x => x.Id == d.Track);
        P.Children.Add(Label($"Tor {(tr?.Number.Length > 0 ? tr.Number : "(bez numeru)")} – km [m od początku toru]"));
        var km = new TextBox { Text = d.S.ToString("0.00", CultureInfo.InvariantCulture), Margin = new Thickness(0, 2, 0, 6) };
        km.KeyDown += (s, e) => { if (e.Key == Key.Enter && Num(km.Text) is double v) ChDevice(d.Id, "Kilometraż urządzenia", x => x.S = v); };
        P.Children.Add(km);
        if (d.Directed)
            P.Children.Add(new TextBlock { Text = $"dla jazdy w stronę {(d.Dir > 0 ? "rosnącego" : "malejącego")} km toru, po {(d.Side > 0 ? "prawej" : "lewej")}", Margin = new Thickness(0, 0, 0, 4) });
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        row.Children.Add(SmallBtn("Zmień stronę", () => ChDevice(d.Id, "Strona urządzenia", x => x.Side = -x.Side), "Urządzenie po drugiej stronie toru"));
        if (d.Directed) row.Children.Add(SmallBtn("Odwróć kierunek", () => ChDevice(d.Id, "Kierunek urządzenia", x => x.Dir = -x.Dir), "Dla jazdy w drugą stronę"));
        P.Children.Add(row);
        if (d.Kind == UtDevice.Semafor)
        {
            P.Children.Add(Label("Liczba komór"));
            var hb = new ComboBox { Margin = new Thickness(0, 2, 0, 6) };
            for (int i = 2; i <= 6; i++) hb.Items.Add(i.ToString());
            hb.SelectedItem = d.Heads.ToString();
            hb.SelectionChanged += (s, e) => { if (hb.SelectedItem is string h) ChDevice(d.Id, "Komory semafora", x => x.Heads = int.Parse(h)); };
            P.Children.Add(hb);
        }
        if (d.Kind == UtDevice.TarczaMan)
        {
            var dw = new CheckBox { Content = "karzełkowa (bez masztu)", IsChecked = d.Dwarf, Margin = new Thickness(0, 0, 0, 6) };
            dw.Click += (s, e) => ChDevice(d.Id, "Tarcza karzełkowa", x => x.Dwarf = dw.IsChecked == true);
            P.Children.Add(dw);
        }
        foreach (var n in Urzadzenia.Check(Doc.P, d))
            P.Children.Add(new TextBlock { Text = n, Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        P.Children.Add(SmallBtn("Usuń", () => { View.SelectDevice(d.Id); DeleteDevice(); }, "Usuń urządzenie (Del)"));
        P.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 10) });
    }
}

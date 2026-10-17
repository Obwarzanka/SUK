using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace Lcs.Komputerownia;

/// <summary>Wiersz tabeli: jedna karta sterownika obiektowego (dane z symulatora, odświeżane w miejscu).</summary>
public sealed class Karta : INotifyPropertyChanged
{
    public string Id { get; init; }
    public string Rodzaj { get; init; }
    public string Post { get; init; }
    public string Obiekt { get; init; }
    public string Typ { get; set; }
    public string Rozkaz { get; set; }
    public string RozkazKiedy { get; set; }
    public string Status { get; set; }
    public string Teren { get; set; }
    public string Usterki { get; set; }
    public List<string> UsterkiKlucze { get; set; } = new();
    public bool Nietypowa { get; set; }
    public bool BrakKontroli => Status?.Contains("BRAK KONTROLI") == true || Status?.Contains("NIE ZAPALIŁ") == true || Status?.Contains("BRAK ŁĄCZNOŚCI") == true;
    public event PropertyChangedEventHandler PropertyChanged;

    public void Ustaw(JsonNode n, double t)
    {
        Typ = (string)n["Typ"]; Rozkaz = (string)n["Rozkaz"] ?? "–";
        var rt = n["RozkazT"]?.GetValue<double?>();
        RozkazKiedy = rt is double x ? $"{Math.Max(0, t - x):0.0} s temu" : "";
        Status = (string)n["Status"]; Teren = (string)n["Teren"]; Usterki = (string)n["UsterkiOpis"];
        UsterkiKlucze = n["Usterki"]?.AsArray().Select(v => (string)v).ToList() ?? new();
        Nietypowa = n["Nietypowa"]?.GetValue<bool>() ?? false;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}

/// <summary>
/// Komputerownia – podgląd kart sterowników obiektowych w działającym symulatorze (PODZIAL_MODULOW.md krok 6a):
/// dla każdej karty rozkaz od komputera zależności, status meldowany do KZ, stan terenu i usterki; wprowadzanie usterek kart.
/// Łączy się przez sieć: domyślnie port serwisowy symulatora na tym komputerze (127.0.0.1:47121), może też z serwerem stanowisk (47120).
/// </summary>
public class MainWindow : Window
{
    public readonly TextBox Host = new() { Text = "127.0.0.1", Width = 130 }, Port = new() { Text = Polaczenie.PortSerwisowy.ToString(), Width = 60 };
    public readonly Button BtnPolacz = new() { Content = "Połącz", Padding = new Thickness(12, 2, 12, 2), Margin = new Thickness(8, 0, 0, 0) };
    public readonly TextBlock Info = new() { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    public readonly ListBox Rodzaje = new() { Margin = new Thickness(0, 4, 0, 10) };
    public readonly ComboBox Posterunki = new() { Margin = new Thickness(0, 4, 0, 10) };
    public readonly TextBox Szukaj = new() { Margin = new Thickness(0, 4, 0, 10), ToolTip = "Fragment nazwy obiektu, np. 57 albo z53" };
    public readonly CheckBox TylkoNietypowe = new() { Content = "tylko stany nietypowe", Margin = new Thickness(0, 0, 0, 10) };
    public readonly DataGrid Tabela = new()
    {
        AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, SelectionMode = DataGridSelectionMode.Single, HeadersVisibility = DataGridHeadersVisibility.Column,
        GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(0xe2, 0xe2, 0xdc)), RowHeight = 24,
    };
    public readonly StackPanel Szczegoly = new() { Margin = new Thickness(14, 0, 0, 0) };
    public readonly ObservableCollection<Karta> Karty = new();
    readonly Dictionary<string, Karta> byId = new();
    ICollectionView widok;
    public Polaczenie Pol;
    public JsonArray UsterkiDef = new();
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    bool wToku;
    public int Odswiezenia;

    static readonly (string Key, string Name)[] RodzajeKart =
    {
        ("", "wszystkie"), ("odcinek", "odcinki (liczniki osi, obwody torowe)"), ("zwrotnica", "zwrotnice"), ("wykolejnica", "wykolejnice"),
        ("sygnalizator", "sygnalizatory (sygnały wg Ie-1)"),
    };

    public MainWindow()
    {
        Title = "Komputerownia LCS – karty sterowników obiektowych"; Width = 1560; Height = 780;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 13; Background = new SolidColorBrush(Color.FromRgb(0xf6, 0xf6, 0xf3));
        var root = new DockPanel { Margin = new Thickness(12) };

        var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        top.Children.Add(new TextBlock { Text = "Symulator:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        top.Children.Add(Host);
        top.Children.Add(new TextBlock { Text = "port", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 6, 0) });
        top.Children.Add(Port);
        top.Children.Add(BtnPolacz);
        top.Children.Add(Info);
        DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);

        var left = new StackPanel { Width = 230, Margin = new Thickness(0, 0, 12, 0) };
        left.Children.Add(Naglowek("Rodzaj karty"));
        foreach (var (k, n) in RodzajeKart) Rodzaje.Items.Add(new ListBoxItem { Content = n, Tag = k });
        Rodzaje.SelectedIndex = 0;
        left.Children.Add(Rodzaje);
        left.Children.Add(Naglowek("Posterunek"));
        left.Children.Add(Posterunki);
        left.Children.Add(Naglowek("Obiekt"));
        left.Children.Add(Szukaj);
        left.Children.Add(TylkoNietypowe);
        left.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, FontSize = 12, Margin = new Thickness(0, 10, 0, 0),
            Text = "Karta przyjmuje rozkaz od komputera zależności, wykonuje go w terenie (napęd, licznik osi) i melduje status. " +
                   "Kolumny: rozkaz od KZ – ostatni rozkaz i kiedy przyszedł; status do KZ – to, co widzi komputer zależności; teren – stan rzeczywisty przy torze. " +
                   "Żółte – stan nietypowy (rozkaz w toku, usterka), czerwone – brak kontroli, sygnał niezapalony, brak łączności. Sygnały semaforów – kody i światła wg Ie-1 (S1…S13a, Sz, Ms1, Ms2).",
        });
        DockPanel.SetDock(left, Dock.Left); root.Children.Add(left);

        Szczegoly.Width = 300;
        var right = new ScrollViewer { Content = Szczegoly, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        DockPanel.SetDock(right, Dock.Right); root.Children.Add(right);

        Kolumna("Post.", nameof(Karta.Post), 48); Kolumna("Obiekt", nameof(Karta.Obiekt), 70); Kolumna("Karta", nameof(Karta.Rodzaj), 92); Kolumna("Typ", nameof(Karta.Typ), 95);
        Kolumna("Rozkaz od KZ", nameof(Karta.Rozkaz), 0.9, true); Kolumna("kiedy", nameof(Karta.RozkazKiedy), 78); Kolumna("Status do KZ", nameof(Karta.Status), 1.2, true);
        Kolumna("Teren", nameof(Karta.Teren), 1, true); Kolumna("Usterki", nameof(Karta.Usterki), 0.9, true);
        var rs = new Style(typeof(DataGridRow));
        var t1 = new DataTrigger { Binding = new Binding(nameof(Karta.Nietypowa)), Value = true }; t1.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(Color.FromRgb(0xff, 0xf4, 0xc8)))); rs.Triggers.Add(t1);
        var t2 = new DataTrigger { Binding = new Binding(nameof(Karta.BrakKontroli)), Value = true }; t2.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(Color.FromRgb(0xff, 0xd6, 0xd2)))); rs.Triggers.Add(t2);
        Tabela.RowStyle = rs;
        widok = CollectionViewSource.GetDefaultView(Karty); widok.Filter = Filtr;
        Tabela.ItemsSource = widok;
        root.Children.Add(Tabela);
        Content = root;

        BtnPolacz.Click += (s, e) => { if (Pol == null) Polacz(); else Rozlacz("rozłączono"); };
        Rodzaje.SelectionChanged += (s, e) => widok.Refresh();
        Posterunki.SelectionChanged += (s, e) => widok.Refresh();
        Szukaj.TextChanged += (s, e) => widok.Refresh();
        TylkoNietypowe.Click += (s, e) => widok.Refresh();
        Tabela.SelectionChanged += (s, e) => PokazSzczegoly();
        timer.Tick += async (s, e) => await Odswiez();
        Closed += (s, e) => Pol?.Dispose();
        PokazSzczegoly();
        Info.Text = "Niepołączony. Uruchom symulator (gra jednoosobowa albo serwer) i kliknij „Połącz”.";
    }

    static TextBlock Naglowek(string t) => new() { Text = t, FontWeight = FontWeights.SemiBold };

    void Kolumna(string h, string p, double w, bool star = false) =>
        Tabela.Columns.Add(new DataGridTextColumn { Header = h, Binding = new Binding(p), Width = star ? new DataGridLength(w, DataGridLengthUnitType.Star) : new DataGridLength(w) });

    bool Filtr(object o)
    {
        var k = (Karta)o;
        var r = (string)((ListBoxItem)Rodzaje.SelectedItem)?.Tag ?? "";
        if (r != "" && k.Rodzaj != r) return false;
        if (Posterunki.SelectedItem is ComboBoxItem pi && (string)pi.Tag != "" && k.Post != (string)pi.Tag) return false;
        var q = Szukaj.Text.Trim();
        if (q != "" && !k.Obiekt.Contains(q, StringComparison.OrdinalIgnoreCase)) return false;
        if (TylkoNietypowe.IsChecked == true && !k.Nietypowa) return false;
        return true;
    }

    public void Polacz()
    {
        if (!int.TryParse(Port.Text, out var port)) { Info.Text = "Zły numer portu"; return; }
        try { Pol = Polaczenie.Polacz(Host.Text.Trim(), port); }
        catch (Exception ex) { Pol = null; Info.Text = "Nie można połączyć: " + (ex.InnerException?.Message ?? ex.Message); return; }
        Pol.Rozlaczono += why => Dispatcher.BeginInvoke(() => Rozlacz(why));
        Karty.Clear(); byId.Clear();
        BtnPolacz.Content = "Rozłącz"; Host.IsEnabled = Port.IsEnabled = false;
        Info.Text = $"Połączono z „{Pol.Stacja}” ({Pol.Adres})";
        timer.Start();
    }

    void Rozlacz(string why)
    {
        timer.Stop();
        Pol?.Dispose(); Pol = null;
        BtnPolacz.Content = "Połącz"; Host.IsEnabled = Port.IsEnabled = true;
        Info.Text = "Rozłączono: " + why;
    }

    /// <summary>Pobranie stanu kart i aktualizacja tabeli w miejscu (zaznaczenie i przewinięcie zostają).</summary>
    public async Task Odswiez()
    {
        if (Pol == null || wToku) return;
        wToku = true;
        try
        {
            var d = await Pol.Zadanie("karty");
            if (d == null || Pol == null) return;
            double t = d["T"]?.GetValue<double>() ?? 0;
            UsterkiDef = d["Usterki"]?.AsArray() ?? new JsonArray();
            if (Posterunki.Items.Count == 0)
            {
                Posterunki.Items.Add(new ComboBoxItem { Content = "wszystkie", Tag = "" });
                foreach (var (k, v) in d["Posterunki"]?.AsObject() ?? new JsonObject()) Posterunki.Items.Add(new ComboBoxItem { Content = $"{k} – {(string)v}", Tag = k });
                Posterunki.SelectedIndex = 0;
            }
            bool nowe = false;
            foreach (var n in d["Karty"]!.AsArray())
            {
                var id = (string)n["Rodzaj"] + "|" + (string)n["Id"];
                if (!byId.TryGetValue(id, out var k))
                {
                    k = new Karta { Id = (string)n["Id"], Rodzaj = (string)n["Rodzaj"], Post = (string)n["Post"], Obiekt = (string)n["Obiekt"] };
                    byId[id] = k; Karty.Add(k); nowe = true;
                }
                k.Ustaw(n, t);
            }
            if (nowe || TylkoNietypowe.IsChecked == true) widok.Refresh();
            int nk = Karty.Count(k => k.Nietypowa), bk = Karty.Count(k => k.BrakKontroli);
            Info.Text = $"Połączono z „{Pol.Stacja}” ({Pol.Adres}) · czas symulacji {(string)d["Zegar"]} · kart: {Karty.Count}, nietypowych: {nk}, bez kontroli: {bk}";
            Odswiezenia++;
            PokazSzczegoly();
        }
        finally { wToku = false; }
    }

    Karta wybrana; string wybranaStan;

    void PokazSzczegoly()
    {
        var k = Tabela.SelectedItem as Karta;
        var stan = k == null ? null : $"{k.Rozkaz}|{k.RozkazKiedy}|{k.Status}|{k.Teren}|{string.Join(",", k.UsterkiKlucze)}";
        if (k == wybrana && stan == wybranaStan && Szczegoly.Children.Count > 0) return;
        wybrana = k; wybranaStan = stan;
        Szczegoly.Children.Clear();
        if (k == null)
        {
            Szczegoly.Children.Add(new TextBlock { Text = "Wybierz kartę w tabeli – tu pojawi się jej opis i usterki do wprowadzenia (jak instruktor).", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray });
            return;
        }
        Szczegoly.Children.Add(new TextBlock { Text = $"Karta: {k.Rodzaj} {k.Obiekt}", FontSize = 17, FontWeight = FontWeights.SemiBold });
        Szczegoly.Children.Add(new TextBlock { Text = $"posterunek {k.Post} · {k.Typ}", Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 0, 10) });
        void Pole(string n, string v) { Szczegoly.Children.Add(Naglowek(n)); Szczegoly.Children.Add(new TextBlock { Text = string.IsNullOrEmpty(v) ? "–" : v, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) }); }
        Pole("KZ → karta (rozkaz)", k.Rozkaz + (k.RozkazKiedy != "" ? $" ({k.RozkazKiedy})" : ""));
        Pole("karta → KZ (status)", k.Status);
        Pole("teren", k.Teren);
        Pole("usterki", k.Usterki);
        var defs = UsterkiDef.Where(u => (string)u["Kind"] == k.Rodzaj).ToList();
        if (defs.Count == 0) { Szczegoly.Children.Add(new TextBlock { Text = "Brak usterek do wprowadzenia dla tej karty.", Foreground = Brushes.DimGray }); return; }
        Szczegoly.Children.Add(Naglowek("Usterki karty (instruktor)"));
        foreach (var u in defs)
        {
            var key = (string)u["Key"]; bool jest = k.UsterkiKlucze.Contains(key);
            var b = new Button { Content = new TextBlock { Text = (jest ? "Usuń: " : "Wprowadź: ") + (string)u["Name"], TextWrapping = TextWrapping.Wrap }, Tag = jest, Margin = new Thickness(0, 4, 0, 0), Padding = new Thickness(6, 3, 6, 3), HorizontalContentAlignment = HorizontalAlignment.Left };
            var kk = k;
            b.Click += async (s, e) => await Usterka(key, kk.Id, jest);
            Szczegoly.Children.Add(new TextBlock { Text = "", Height = 0 });
            Szczegoly.Children.Add(b);
        }
    }

    /// <summary>Wprowadzenie (clear = false) albo usunięcie usterki karty w symulatorze.</summary>
    public async Task<string> Usterka(string key, string target, bool clear)
    {
        if (Pol == null) return "niepołączony";
        var r = await Pol.Zadanie("usterka", ("key", key), ("target", target), ("clear", clear));
        var msg = r == null ? "brak odpowiedzi" : (string)r;
        if (msg != "OK") Info.Text = "Usterka: " + msg;
        await Odswiez();
        return msg;
    }

    public void Zaznacz(string rodzaj, string id)
    {
        var k = Karty.FirstOrDefault(x => x.Rodzaj == rodzaj && x.Id == id);
        if (k != null) { Tabela.SelectedItem = k; Tabela.ScrollIntoView(k); }
        PokazSzczegoly();
    }
}

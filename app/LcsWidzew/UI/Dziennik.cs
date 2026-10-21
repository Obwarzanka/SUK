using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using LcsWidzew.Sim;

namespace LcsWidzew.UI;

/// <summary>
/// Dziennik ZDARZENIA / ALARMY wg ISDR (Zrodla/ISDR_zdjecia.md § 4, ROZWOJ.md § 2a): zakres dat od – do, lejek filtra przy kolumnach
/// Obszar, Stacja, Typ obiektu, Obiekt, Kategoria, „Zostaw na wierzchu” (nowy wpis przewija na pierwszą stronę), liczba wpisów,
/// strony z przewijaniem i przycisk „Online” (powrót do bieżących wpisów; przeglądanie stron albo zmiana dat wyłącza tryb Online).
/// </summary>
public class Dziennik : DockPanel
{
    static Brush B(byte r, byte g, byte b) { var x = new SolidColorBrush(Color.FromRgb(r, g, b)); x.Freeze(); return x; }
    static readonly Brush Szary = B(0x88, 0x88, 0x88), Lejek = B(0x9a, 0x9a, 0x9a), LejekWl = B(0x00, 0xc0, 0xff), OnlineWl = B(0x2f, 0x7d, 0x32);
    /// <summary>Kolumny z filtrem (lejkiem) – jak na zdjęciach ISDR.</summary>
    static readonly string[] FiltrowaneKolumny = { "Obszar", "Stacja", "TypObiektu", "Obiekt", "Kategoria" };

    public readonly DataGrid Grid = new();
    public readonly CheckBox NaWierzchu = new() { Content = "Zostaw na wierzchu", IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Foreground = B(0xaa, 0xaa, 0xaa), Margin = new Thickness(10, 0, 0, 0) };
    /// <summary>Miejsce na dodatkowe przyciski (eksport, kwitowanie, „Wycisz”) – przed paskiem stron.</summary>
    public readonly StackPanel Dodatki = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    readonly ObservableCollection<EventRecord> widok = new();
    readonly DatePicker dataOd = new() { Width = 104, Margin = new Thickness(4, 0, 2, 0) }, dataDo = new() { Width = 104, Margin = new Thickness(4, 0, 2, 0) };
    readonly TextBox godzOd = new() { Width = 42, Text = "00:00" }, godzDo = new() { Width = 42, Text = "23:59" };
    readonly TextBlock liczba = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 8, 0), Foreground = Szary };
    readonly TextBlock strona = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 8, 0), Foreground = Szary };
    readonly Button online = new() { Content = "Online", Padding = new Thickness(14, 1, 14, 1) };
    readonly Dictionary<string, HashSet<string>> filtry = new();
    readonly Dictionary<string, Path> lejki = new();
    readonly string nazwaWpisow;

    /// <summary>Wszystkie wpisy (najstarsze pierwsze) i porządek wyświetlania (domyślnie najnowsze u góry).</summary>
    public Func<IEnumerable<EventRecord>> Zrodlo = () => Enumerable.Empty<EventRecord>();
    public Func<IEnumerable<EventRecord>, IEnumerable<EventRecord>> Porzadek = x => x.Reverse();
    /// <summary>Dzień rozpoczęcia symulacji – data wpisów (wpis ma tylko godzinę i czas symulacji).</summary>
    public DateTime Dzien0 = DateTime.Today;
    double? start;           // godzina startu symulacji [s], z pierwszego wpisu: Czas − T
    bool tryOnline = true, brudny = true;
    int nr;                  // strona od 0
    List<EventRecord> wynik = new();

    public bool Online => tryOnline;
    public int Strona => nr;
    public int Stron => Math.Max(1, (wynik.Count + NaStrone - 1) / NaStrone);
    public int LiczbaWpisow => wynik.Count;

    public Dziennik(string tytul, Brush kolor, string nazwaWpisow, (string Naglowek, string Pole, double Szer)[] kolumny)
    {
        this.nazwaWpisow = nazwaWpisow;
        var pasek = new Border { Background = B(0x1b, 0x1b, 0x1b), BorderBrush = B(0x33, 0x33, 0x33), BorderThickness = new Thickness(0, 1, 0, 1) };
        SetDock(pasek, Dock.Top); Children.Add(pasek);
        var dp = new DockPanel { Margin = new Thickness(4, 2, 4, 2), LastChildFill = false };
        pasek.Child = dp;
        var lewo = new StackPanel { Orientation = Orientation.Horizontal };
        lewo.Children.Add(new Border { Background = kolor, Padding = new Thickness(8, 1, 8, 1), Child = new TextBlock { Text = tytul, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold }, VerticalAlignment = VerticalAlignment.Center });
        lewo.Children.Add(Etykieta("  od:")); lewo.Children.Add(dataOd); lewo.Children.Add(godzOd);
        lewo.Children.Add(Etykieta("  do:")); lewo.Children.Add(dataDo); lewo.Children.Add(godzDo);
        lewo.Children.Add(Dodatki);
        dp.Children.Add(lewo);
        var prawo = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(prawo, Dock.Right);
        prawo.Children.Add(NaWierzchu); prawo.Children.Add(liczba); prawo.Children.Add(strona); prawo.Children.Add(online);
        foreach (var (t, f) in new (string, Action)[] { ("⏮", () => Idz(0)), ("◀", () => Idz(nr - 1)), ("▶", () => Idz(nr + 1)), ("⏭", () => Idz(int.MaxValue)) })
        {
            var b = new Button { Content = t, Padding = new Thickness(6, 0, 6, 0), Margin = new Thickness(1, 0, 0, 0), ToolTip = t switch { "⏮" => "Pierwsza strona (najnowsze)", "◀" => "Poprzednia strona", "▶" => "Następna strona", _ => "Ostatnia strona (najstarsze)" } };
            b.Click += (s, e) => f(); prawo.Children.Add(b);
        }
        dp.Children.Add(prawo);
        online.Click += (s, e) => UstawOnline();

        Grid.ItemsSource = widok;
        Grid.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled; Grid.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        foreach (var (h, p, w) in kolumny)
        {
            var bind = p == "Czas" ? new Binding { Converter = new CzasKonw(this) } : new Binding(p);
            Grid.Columns.Add(new DataGridTextColumn { Header = FiltrowaneKolumny.Contains(p) ? NaglowekZLejkiem(h, p) : h, Binding = bind, Width = w > 0 ? new DataGridLength(w) : new DataGridLength(1, DataGridLengthUnitType.Star) });
        }
        Children.Add(Grid);
        Grid.SizeChanged += (s, e) => { brudny = true; Tick(); };

        dataOd.SelectedDate = dataDo.SelectedDate = Dzien0;
        dataOd.SelectedDateChanged += (s, e) => ZmianaZakresu(); dataDo.SelectedDateChanged += (s, e) => ZmianaZakresu();
        godzOd.LostFocus += (s, e) => ZmianaZakresu(); godzDo.LostFocus += (s, e) => ZmianaZakresu();
        godzOd.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) ZmianaZakresu(); };
        godzDo.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) ZmianaZakresu(); };
        Pokaz();
    }

    static TextBlock Etykieta(string t) => new() { Text = t, VerticalAlignment = VerticalAlignment.Center, Foreground = Szary };

    /// <summary>Data i godzina wpisu: dzień startu + godzina startu symulacji + czas symulacji.</summary>
    public DateTime Data(EventRecord e)
    {
        if (start == null && TimeSpan.TryParse(e.Czas, CultureInfo.InvariantCulture, out var c)) start = ((c.TotalSeconds - Math.Floor(e.T)) % 86400 + 86400) % 86400;
        return Dzien0.AddSeconds((start ?? 0) + Math.Floor(e.T));
    }

    class CzasKonw : IValueConverter
    {
        readonly Dziennik d; public CzasKonw(Dziennik d) => this.d = d;
        public object Convert(object v, Type t, object p, CultureInfo c) => v is EventRecord e ? d.Data(e).ToString("yyyy-MM-dd HH:mm:ss") : "";
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
    }

    // ------------------------------------------------------------------ filtry kolumn (lejek)
    object NaglowekZLejkiem(string h, string pole)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(new TextBlock { Text = h, VerticalAlignment = VerticalAlignment.Center });
        var lj = new Path { Data = Geometry.Parse("M0,0 L9,0 L5.5,4.5 L5.5,9 L3.5,8 L3.5,4.5 Z"), Fill = Lejek, Margin = new Thickness(6, 1, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var bt = new Button { Content = lj, Padding = new Thickness(2, 0, 2, 0), Margin = new Thickness(4, 0, 0, 0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), ToolTip = "Filtr kolumny " + h, Cursor = System.Windows.Input.Cursors.Hand };
        bt.Click += (s, e) => { OtworzFiltr(bt, pole, h); e.Handled = true; };
        sp.Children.Add(bt);
        lejki[pole] = lj;
        return sp;
    }

    static string Pole(EventRecord e, string p) => p switch { "Obszar" => e.Obszar, "Stacja" => e.Stacja, "TypObiektu" => e.TypObiektu, "Obiekt" => e.Obiekt, "Kategoria" => e.Kategoria, _ => "" } ?? "";

    void OtworzFiltr(UIElement przy, string pole, string h)
    {
        var wartosci = WZakresie().Select(e => Pole(e, pole)).Distinct().OrderBy(x => x, StringComparer.CurrentCulture).ToList();
        var zazn = filtry.GetValueOrDefault(pole);
        var lista = new StackPanel();
        var boxy = new List<CheckBox>();
        foreach (var w in wartosci)
        {
            var cb = new CheckBox { Content = w == "" ? "(puste)" : w, Tag = w, IsChecked = zazn == null || zazn.Contains(w), Foreground = Brushes.White, Margin = new Thickness(2) };
            boxy.Add(cb); lista.Children.Add(cb);
        }
        var wszystkie = new CheckBox { Content = "(wszystkie)", IsChecked = zazn == null, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, Margin = new Thickness(2, 2, 2, 4) };
        wszystkie.Click += (s, e) => { foreach (var b in boxy) b.IsChecked = wszystkie.IsChecked == true; };
        var pop = new Popup { PlacementTarget = przy, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = false };
        var ok = new Button { Content = "OK", Padding = new Thickness(14, 1, 14, 1), Margin = new Thickness(0, 6, 4, 0) };
        var czysc = new Button { Content = "Wyczyść filtr", Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(0, 6, 0, 0) };
        ok.Click += (s, e) =>
        {
            var sel = boxy.Where(b => b.IsChecked == true).Select(b => (string)b.Tag).ToHashSet();
            UstawFiltr(pole, sel.Count == boxy.Count ? null : sel); pop.IsOpen = false;
        };
        czysc.Click += (s, e) => { UstawFiltr(pole, null); pop.IsOpen = false; };
        var dol = new StackPanel { Orientation = Orientation.Horizontal }; dol.Children.Add(ok); dol.Children.Add(czysc);
        var sp = new StackPanel { Margin = new Thickness(6) };
        sp.Children.Add(new TextBlock { Text = "Filtr: " + h, Foreground = Szary, Margin = new Thickness(2, 0, 0, 4) });
        sp.Children.Add(wszystkie);
        sp.Children.Add(new ScrollViewer { Content = lista, MaxHeight = 260, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        sp.Children.Add(dol);
        pop.Child = new Border { Background = B(0x20, 0x20, 0x20), BorderBrush = B(0x55, 0x55, 0x55), BorderThickness = new Thickness(1), Child = sp, MinWidth = 180 };
        pop.IsOpen = true;
    }

    /// <summary>Filtr kolumny: dozwolone wartości (null – wszystkie).</summary>
    public void UstawFiltr(string pole, HashSet<string> dozwolone)
    {
        if (dozwolone == null) filtry.Remove(pole); else filtry[pole] = dozwolone;
        if (lejki.TryGetValue(pole, out var lj)) lj.Fill = dozwolone == null ? Lejek : LejekWl;
        nr = 0; brudny = true; Pokaz();
    }

    // ------------------------------------------------------------------ zakres dat, strony, Online
    DateTime Od => (dataOd.SelectedDate ?? Dzien0).Date + Godz(godzOd.Text, TimeSpan.Zero);
    DateTime Do => (dataDo.SelectedDate ?? Dzien0).Date + Godz(godzDo.Text, new TimeSpan(23, 59, 0)).Add(TimeSpan.FromSeconds(59));
    static TimeSpan Godz(string t, TimeSpan dom) => TimeSpan.TryParseExact((t ?? "").Trim(), new[] { @"hh\:mm", @"h\:mm", @"hh\:mm\:ss" }, CultureInfo.InvariantCulture, out var v) ? v : dom;

    bool zmiana;
    void ZmianaZakresu() { if (zmiana) return; tryOnline = false; nr = 0; brudny = true; Pokaz(); }
    /// <summary>Zakres dat od – do (przeglądanie – wyłącza tryb Online).</summary>
    public void UstawZakres(DateTime od, DateTime @do)
    {
        zmiana = true;
        dataOd.SelectedDate = od.Date; godzOd.Text = od.ToString("HH:mm"); dataDo.SelectedDate = @do.Date; godzDo.Text = @do.ToString("HH:mm");
        zmiana = false; ZmianaZakresu();
    }

    void Idz(int n) { tryOnline = false; nr = Math.Clamp(n, 0, Stron - 1); brudny = true; Pokaz(); }

    /// <summary>Online: bieżące wpisy – zakres do końca bieżącego dnia symulacji, pierwsza strona, nowe wpisy od razu.</summary>
    public void UstawOnline()
    {
        tryOnline = true; nr = 0;
        var ost = Zrodlo().LastOrDefault();
        var dzis = ost != null ? Data(ost).Date : Dzien0;
        zmiana = true;
        dataOd.SelectedDate = dzis; godzOd.Text = "00:00";
        dataDo.SelectedDate = dzis; godzDo.Text = "23:59";
        zmiana = false; brudny = true; Pokaz();
    }

    /// <summary>Nowy wpis: „Zostaw na wierzchu” – przewinięcie na pierwszą stronę (najnowszy wpis); w trybie Online widok odświeżany.</summary>
    public void NowyWpis(EventRecord e)
    {
        if (NaWierzchu.IsChecked == true) { if (!tryOnline) UstawOnline(); nr = 0; }
        if (tryOnline) { if (Data(e) > Do) UstawOnline(); brudny = true; }
    }

    /// <summary>Pełne przeliczenie (nowe źródło, zmiana porządku – np. kwitowanie alarmów).</summary>
    public void Przelicz() { brudny = true; }
    public void Reset() { start = null; filtry.Clear(); foreach (var l in lejki.Values) l.Fill = Lejek; nr = 0; tryOnline = true; zmiana = true; dataOd.SelectedDate = dataDo.SelectedDate = Dzien0; godzOd.Text = "00:00"; godzDo.Text = "23:59"; zmiana = false; brudny = true; }

    /// <summary>Cykliczne odświeżenie (pulpit, co 50 ms) – przeliczenie tylko po zmianie.</summary>
    public void Tick() { if (brudny) Pokaz(); }

    IEnumerable<EventRecord> WZakresie() { var (a, b) = (Od, Do); return Zrodlo().Where(e => { var d = Data(e); return d >= a && d <= b; }); }

    int NaStrone => Math.Max(3, (int)((Grid.ActualHeight - 22) / Math.Max(10, Grid.RowHeight > 0 ? Grid.RowHeight : 18)));

    void Pokaz()
    {
        brudny = false;
        wynik = Porzadek(WZakresie().Where(e => filtry.All(f => f.Value.Contains(Pole(e, f.Key))))).ToList();
        nr = Math.Clamp(nr, 0, Stron - 1);
        var str = wynik.Skip(nr * NaStrone).Take(NaStrone).ToList();
        if (!widok.SequenceEqual(str)) { widok.Clear(); foreach (var e in str) widok.Add(e); }
        liczba.Text = $"Liczba {nazwaWpisow}: {wynik.Count}";
        strona.Text = $"Strona: {(wynik.Count == 0 ? 0 : nr + 1)} / {(wynik.Count == 0 ? 0 : Stron)}";
        online.Background = tryOnline ? OnlineWl : B(0x26, 0x26, 0x26);
        online.ToolTip = tryOnline ? "Tryb Online – bieżące wpisy" : "Przeglądanie – kliknij, aby wrócić do bieżących wpisów";
    }
}

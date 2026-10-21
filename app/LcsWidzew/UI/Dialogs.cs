using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LcsWidzew.Sim;

namespace LcsWidzew.UI;

/// <summary>Wspólny wygląd okien pomocniczych (ciemny motyw).</summary>
public class DarkWindow : Window
{
    protected static readonly Brush Fg = new SolidColorBrush(Color.FromRgb(0xd8, 0xd8, 0xd8));
    protected static readonly Brush Panel2 = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x18));
    public DarkWindow(string title, double w, double h)
    {
        Title = title; Width = w; Height = h; Background = new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x12)); Foreground = Fg;
        FontFamily = new FontFamily("Segoe UI"); WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
    }
    protected static TextBlock T(string s, double size = 12, Brush b = null, bool bold = false) =>
        new() { Text = s, FontSize = size, Foreground = b ?? Fg, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) };
    protected static Button Btn(string s, RoutedEventHandler h, Brush bg = null)
    {
        var b = new Button { Content = s, Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 4, 6, 4), Background = bg ?? new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26)), Foreground = Brushes.White };
        b.Click += h; return b;
    }
    protected static TextBox Box(string v = "") => new() { Text = v, Background = Brushes.Black, Foreground = Brushes.White, CaretBrush = Brushes.White, BorderBrush = Brushes.DimGray, Padding = new Thickness(3), Margin = new Thickness(0, 2, 0, 4) };
    protected static DataGrid Grid()
    {
        return new DataGrid
        {
            AutoGenerateColumns = false, IsReadOnly = true, HeadersVisibility = DataGridHeadersVisibility.Column, Background = Brushes.Black,
            RowBackground = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x14)), AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(0x1a, 0x1a, 0x1a)),
            Foreground = Fg, FontFamily = new FontFamily("Consolas"), FontSize = 11.5, GridLinesVisibility = DataGridGridLinesVisibility.None, CanUserAddRows = false
        };
    }
    protected static void Col(DataGrid g, string h, string path, double w = double.NaN)
        => g.Columns.Add(new DataGridTextColumn { Header = h, Binding = new Binding(path), Width = double.IsNaN(w) ? new DataGridLength(1, DataGridLengthUnitType.Star) : new DataGridLength(w) });
}

// ---------------------------------------------------------------------- logowanie
public class LoginWindow : DarkWindow
{
    public string UserName, Role, Scenario; public bool Restart;
    public LoginWindow(bool loggedBefore) : base("Logowanie – LCS Łódź Widzew", 520, 400)
    {
        ResizeMode = ResizeMode.NoResize;
        var sp = new StackPanel { Margin = new Thickness(18) };
        sp.Children.Add(T("Symulator komputerowych urządzeń srk – LCS Łódź Widzew", 15, Brushes.White, true));
        sp.Children.Add(T("Szkolenie wg Ie-20 (zobrazowanie wg Ie-104). Topologia stacji z OpenStreetMap, semafory i zależności – założenia do weryfikacji z regulaminem technicznym. Logowanie symboliczne (bez hasła).", 11, Brushes.Gray));
        sp.Children.Add(T("Użytkownik:"));
        var name = Box("Dyżurny ruchu"); sp.Children.Add(name);
        sp.Children.Add(T("Rola:"));
        var role = new ComboBox { ItemsSource = new[] { "dyżurny ruchu", "nastawniczy", "instruktor" }, SelectedIndex = 0 }; sp.Children.Add(role);
        sp.Children.Add(T("Scenariusz:"));
        var sc = new ComboBox { ItemsSource = Rozklad.Scenarios.Select(s => s.Name).ToList(), SelectedIndex = 0 }; sp.Children.Add(sc);
        var desc = T(Rozklad.Scenarios[0].Desc, 11, Brushes.Gray); sp.Children.Add(desc);
        sc.SelectionChanged += (a, b) => desc.Text = Rozklad.Scenarios[sc.SelectedIndex].Desc;
        var bp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        void Go(bool restart) { UserName = name.Text.Trim() == "" ? "Dyżurny" : name.Text.Trim(); Role = (string)role.SelectedItem; Scenario = Rozklad.Scenarios[sc.SelectedIndex].Key; Restart = restart; DialogResult = true; }
        bp.Children.Add(Btn(loggedBefore ? "Zaloguj i rozpocznij od nowa" : "Zaloguj i rozpocznij", (a, b) => Go(true), new SolidColorBrush(Color.FromRgb(0x0a, 0x4a, 0x66))));
        if (loggedBefore) bp.Children.Add(Btn("Zaloguj (kontynuuj)", (a, b) => Go(false)));
        bp.Children.Add(Btn("Anuluj", (a, b) => DialogResult = false));
        sp.Children.Add(bp);
        Content = sp;
        Loaded += (a, b) => { name.Focus(); name.SelectAll(); };
    }
}

// ---------------------------------------------------------------------- polecenie specjalne
public class SpecialConfirmWindow : DarkWindow
{
    public SpecialConfirmWindow(ILcsSession ses) : base("Polecenie nastawcze specjalne – potwierdzenie", 580, 430)
    {
        var p = ses.Receive()?.Pending ?? new PendingInd();
        BorderBrush = Brushes.Red; BorderThickness = new Thickness(2); Topmost = true;
        var sp = new StackPanel { Margin = new Thickness(16) };
        sp.Children.Add(T("POLECENIE SPECJALNE", 15, Brushes.OrangeRed, true));
        sp.Children.Add(T($"Kod: {(p.Code != null && Simulator.Codes.TryGetValue(p.Code, out var ci) ? ci.Code : p.Code)} – {p.Desc}", 13, Brushes.White, true));
        sp.Children.Add(T($"Adres: {Model.Station.Lbl(p.Arg)}   (obiekty zamarkowane na pomarańczowo)", 13, Brushes.White, true));
        var left = T("", 12, Brushes.Gold); sp.Children.Add(left);
        sp.Children.Add(T("Przed potwierdzeniem obowiązkowo sprawdź poprawność adresu i kodu polecenia (Ie-20 §13 ust. 7). Uwagi:", 11, Brushes.Gray));
        foreach (var n in p.Notes) sp.Children.Add(T("• " + n, 12));
        sp.Children.Add(T("Wpisz ponownie adres obiektu:"));
        var box = Box(); box.CharacterCasing = CharacterCasing.Upper; sp.Children.Add(box);
        var err = T("", 12, Brushes.Tomato); sp.Children.Add(err);
        var bp = new StackPanel { Orientation = Orientation.Horizontal };
        void Ok()
        {
            var r = ses.Confirm(box.Text);
            if (!r.Ok && ses.Receive()?.Pending != null) { err.Text = r.Msg; return; }
            Close();
        }
        bp.Children.Add(Btn("Potwierdź", (a, b) => Ok(), new SolidColorBrush(Color.FromRgb(0x6a, 0, 0))));
        bp.Children.Add(Btn("Wycofaj (Esc)", (a, b) => { ses.Cancel(); Close(); }));
        sp.Children.Add(bp);
        Content = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        box.KeyDown += (a, e) => { if (e.Key == Key.Enter) Ok(); if (e.Key == Key.Escape) { ses.Cancel(); Close(); } };
        var tm = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        tm.Tick += (a, b) =>
        {
            var I = ses.Receive(); var q = I?.Pending;
            if (q == null) { tm.Stop(); Close(); return; }
            left.Text = I.T < q.NotBefore ? $"Potwierdzenie możliwe za {Math.Ceiling(q.NotBefore - I.T)} s (Ie-104 § 11 ust. 13)" : $"Pozostało {Math.Max(0, Math.Ceiling(q.Until - I.T))} s na potwierdzenie";
        };
        tm.Start(); Closed += (a, b) => tm.Stop();
        Loaded += (a, b) => box.Focus();
    }
}

// ---------------------------------------------------------------------- książka E-1758
public class BookWindow : DarkWindow
{
    public BookWindow(ILcsSession ses) : base("Książka kontroli urządzeń (E-1758)", 700, 620)
    {
        var dock = new DockPanel { Margin = new Thickness(12) };
        var form = new StackPanel(); DockPanel.SetDock(form, Dock.Top);
        var bd = ses.GetBook();
        form.Children.Add(T(bd.Draft != null ? $"Wymagany zapis po poleceniu specjalnym (§14 ust. 4){(bd.Drafts > 1 ? $" – oczekujących: {bd.Drafts}" : "")}" : "Nowy zapis", 13, Brushes.Gold, true));
        var text = Box(bd.Draft ?? "");
        text.AcceptsReturn = true; text.Height = 110; text.TextWrapping = TextWrapping.Wrap; form.Children.Add(text);
        form.Children.Add(T("Powiadomiono automatyka (imię i nazwisko, godz.):"));
        var auto = Box(); form.Children.Add(auto);
        var list = Grid();
        Col(list, "Czas", "Czas", 70); Col(list, "Użytkownik", "Uzytkownik", 110); Col(list, "Treść", "Tresc"); Col(list, "Powiadomiono", "Automatyk", 140);
        list.ItemsSource = bd.Entries;
        form.Children.Add(Btn("Zapisz w książce", (a, b) =>
        {
            if (text.Text.Trim() == "") return;
            ses.AddBook(text.Text, auto.Text);
            Close();
            if (bd.Drafts > 1) Dispatcher.BeginInvoke(() => new BookWindow(ses) { Owner = Owner }.Show(), DispatcherPriority.Background);
        }, new SolidColorBrush(Color.FromRgb(0x0a, 0x4a, 0x66))));
        form.Children.Add(T("Zapisy:", 12, Brushes.Gray));
        dock.Children.Add(form); dock.Children.Add(list);
        Content = dock;
    }
}

// ---------------------------------------------------------------------- plan ruchu
public class PlanWindow : DarkWindow
{
    public PlanWindow(ILcsSession ses) : base("Plan ruchu – obszar LCS", 900, 420)
    {
        var g = Grid();
        foreach (var (h, p, w) in new[] { ("Nr", "Nr", 60.0), ("Kat.", "Kat", 45), ("Z (szlak)", "Z", 170), ("Do (szlak)", "Do", 210), ("Wyprawienie", "Wypr", 80), ("Odjazd", "Odj", 65), ("Tor", "Tor", 45), ("Stan", "Stan", double.NaN) }) Col(g, h, p, w);
        var dock = new DockPanel { Margin = new Thickness(8) };
        var info = T("Wyprawienie = godzina wyprawienia z posterunku sąsiedniego. Tor = zalecany tor przyjęcia.", 11, Brushes.Gray); DockPanel.SetDock(info, Dock.Top);
        dock.Children.Add(info); dock.Children.Add(g); Content = dock;
        void Refresh() => g.ItemsSource = ses.GetPlan();
        Refresh();
        var tm = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) }; tm.Tick += (a, b) => Refresh(); tm.Start(); Closed += (a, b) => tm.Stop();
    }
}

// ---------------------------------------------------------------------- instruktor
public class InstructorWindow : DarkWindow
{
    public InstructorWindow(Simulator sim, Action<string> restart) : base("Panel instruktora", 560, 520)
    {
        var sp = new StackPanel { Margin = new Thickness(12) };
        sp.Children.Add(T("Usterki", 14, Brushes.White, true));
        var ft = new ComboBox { ItemsSource = Instruktor.Faults.Select(f => f.Name).ToList(), SelectedIndex = 0 };
        var tg = new ComboBox();
        void Fill()
        {
            var t = Instruktor.Faults[ft.SelectedIndex].Target;
            tg.ItemsSource = t switch
            {
                "sec" => sim.L.Sections.Keys.OrderBy(x => x).ToList(),
                "sw" => sim.L.Switches.Keys.OrderBy(x => x.Length).ThenBy(x => x).ToList(),
                "sig" => sim.L.Signals.Values.Where(s => s.Type != "odstepowy").Select(s => s.Name).OrderBy(x => x).ToList(),
                "block" => sim.L.Blocks.Keys.ToList(),
                _ => new List<string> { "(cała nastawnia)" }
            };
            tg.SelectedIndex = 0;
        }
        ft.SelectionChanged += (a, b) => Fill(); Fill();
        sp.Children.Add(ft); sp.Children.Add(tg);
        var bp = new StackPanel { Orientation = Orientation.Horizontal };
        string Target() => Instruktor.Faults[ft.SelectedIndex].Target == "" ? null : (string)tg.SelectedItem;
        bp.Children.Add(Btn("Wprowadź", (a, b) => sim.Instruktor.ApplyFault(Instruktor.Faults[ft.SelectedIndex].Key, Target(), false)));
        bp.Children.Add(Btn("Usuń", (a, b) => sim.Instruktor.ApplyFault(Instruktor.Faults[ft.SelectedIndex].Key, Target(), true)));
        sp.Children.Add(bp);

        sp.Children.Add(T("Pociąg dodatkowy", 14, Brushes.White, true));
        var from = new ComboBox { ItemsSource = sim.L.Blocks.Keys.ToList(), SelectedIndex = 0 };
        var cat = new ComboBox { ItemsSource = Trains.Cats.Keys.ToList(), SelectedIndex = 0 };
        var to = new ComboBox { ItemsSource = new[] { "" }.Concat(sim.L.Blocks.Keys).ToList(), SelectedIndex = 0 };
        var num = Box(); var stop = new CheckBox { Content = "postój na ŁW", IsChecked = true, Foreground = Fg };
        sp.Children.Add(T("Ze szlaku / kategoria / numer / do szlaku:", 11, Brushes.Gray));
        sp.Children.Add(from); sp.Children.Add(cat); sp.Children.Add(num); sp.Children.Add(to); sp.Children.Add(stop);
        sp.Children.Add(Btn("Dodaj do kolejki", (a, b) => sim.Rozklad.Enqueue((string)from.SelectedItem, (string)cat.SelectedItem, num.Text.Trim(), (string)to.SelectedItem, stop.IsChecked == true)));

        sp.Children.Add(T("Lokomotywa manewrowa", 14, Brushes.White, true));
        var locoSec = new ComboBox { ItemsSource = sim.L.Sections.Keys.Where(k => k.Contains(":it") || k.Contains(":JT")).OrderBy(k => k).ToList(), SelectedIndex = 0 };
        var locoDir = new ComboBox { ItemsSource = new[] { "czołem na wschód (w prawo)", "czołem na zachód (w lewo)" }, SelectedIndex = 0 };
        sp.Children.Add(locoSec); sp.Children.Add(locoDir);
        sp.Children.Add(Btn("Postaw lokomotywę", (a, b) => { if (!sim.Trains.AddLoco((string)locoSec.SelectedItem, locoDir.SelectedIndex == 0 ? 1 : -1, null)) MessageBox.Show("Odcinek zajęty lub nieznany."); }));
        sp.Children.Add(T("Scenariusz", 14, Brushes.White, true));
        var sc = new ComboBox { ItemsSource = Rozklad.Scenarios.Select(s => s.Name).ToList(), SelectedIndex = 0 };
        sp.Children.Add(sc);
        sp.Children.Add(Btn("Uruchom od nowa", (a, b) => { restart(Rozklad.Scenarios[sc.SelectedIndex].Key); Close(); }));
        Content = new ScrollViewer { Content = sp };
    }
}

// ---------------------------------------------------------------------- ocena
public class ScoreWindow : DarkWindow
{
    public ScoreWindow(ILcsSession ses) : base("Ocena przebiegu ćwiczenia", 620, 520)
    {
        var sd = ses.GetScore(); var s = sd.Stats ?? new Stats();
        var sp = new StackPanel { Margin = new Thickness(14) };
        void Line(string a, object b) { var p = new DockPanel(); var tb = T(b.ToString(), 12, Brushes.White, true); DockPanel.SetDock(tb, Dock.Right); p.Children.Add(tb); p.Children.Add(T(a)); sp.Children.Add(p); }
        Line("Polecenia wykonane", s.Cmds); Line("Polecenia odrzucone", s.Rejected); Line("Polecenia specjalne", s.Special);
        Line("Zapisy w książce E-1758", sd.Book); Line("Brakujące zapisy po poleceniach specjalnych", sd.Drafts);
        Line("Pociągi przyjęte / wyprawione", $"{s.TrainsIn} / {s.TrainsOut}"); Line("Wyprawienia w niewłaściwym kierunku", s.WrongDir);
        Line("Łączny czas postoju pociągów przed semaforami", $"{s.DelaySum:0} s"); Line("Minięcia sygnału „Stój”", s.Spad);
        sp.Children.Add(T("Uwagi (zgodność z Ie-20):", 13, Brushes.Gold, true));
        if (sd.Remarks.Count == 0) sp.Children.Add(T("Brak uwag.", 12, Brushes.Gray));
        foreach (var x in sd.Remarks) sp.Children.Add(T(x));
        Content = new ScrollViewer { Content = sp };
    }
}

// ---------------------------------------------------------------------- pomoc
public class HelpWindow : DarkWindow
{
    public HelpWindow() : base("Pomoc", 760, 680)
    {
        var sp = new StackPanel { Margin = new Thickness(14) };
        sp.Children.Add(T("Obsługa myszą", 14, Brushes.White, true));
        sp.Children.Add(T("LPM na semaforze – początek przebiegu (turkusowe kółka na możliwych końcach), LPM na końcu – para początek–koniec. Polecenia w panelu „Polecenia” w prawym górnym rogu obrazu: klik na obiekcie (semafor, zwrotnica, odcinek, szlak, wykolejnica, ikona nastawni) pokazuje jego przyciski (ciemne – niedostępne), klik na przycisku przygotowuje polecenie, „Wyślij” je przekazuje. SZI → po 5 s SZ / NSZ, ZDP → po 5 s ZDPP (zwolnienie natychmiastowe), ZD – zwłoka 90 s przy pociągu w strefie zbliżania, OPS – odwołanie. Kółko – powiększenie, przeciąganie LPM – przesuwanie obrazu. Esc albo klik w puste miejsce – rezygnacja."));
        sp.Children.Add(T("Polecenia specjalne są dwuetapowe: zainicjowanie, ponowne wpisanie adresu i potwierdzenie w ciągu 30 s (§13 ust. 7–9). Po wykonaniu otwiera się książka kontroli urządzeń.", 12));
        sp.Children.Add(T("Barwy zobrazowania (Ie-104)", 14, Brushes.White, true));
        foreach (var (b, t) in new[] { (PanelView.Grey, "stan podstawowy"), (PanelView.Red, "odcinek zajęty; semafor „Stój”"), (PanelView.Green, "przebieg pociągowy / sygnał zezwalający"), (PanelView.Yellow, "przebieg manewrowy, droga ochronna"), (PanelView.Magenta, "zwalnianie czasowe, zastopowanie, zamknięcie"), (PanelView.White, "brak kontroli (miganie), sygnał zastępczy / manewrowy"), (PanelView.DarkRed, "oczekiwanie na pierwszy przejazd (po zerowaniu)") })
        {
            var p = new StackPanel { Orientation = Orientation.Horizontal };
            p.Children.Add(new Border { Width = 30, Height = 5, Background = b, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
            p.Children.Add(T(t)); sp.Children.Add(p);
        }
        sp.Children.Add(T("Polecenia", 14, Brushes.White, true));
        sp.Children.Add(T("Składnia: adres(y) i kod polecenia (Ie-104 § 11 ust. 7), np. „E S1 Pociąg”, „13 Minus”, „S1 SZ”, „2A Wbl”. Dopuszczalny też zapis w stylu EbiScreen: „POC E S1”. Wielkość liter nie ma znaczenia; adresy dotyczą wyświetlanego obrazu.", 12, Brushes.Gray));
        sp.Children.Add(T("Przebieg wariantowy: podaj punkt pośredni – zwrotnicę z położeniem lub odcinek – np. „E 13+ S3 Pociąg”, albo Ctrl+klik na pulpicie przed wskazaniem końca. Przebieg manewrowy przez kilka tarcz: „Tm10 R6 Manewr” – system złoży go z przebiegów elementarnych.", 12, Brushes.Gray));
        foreach (var (k, c) in Simulator.Codes)
        {
            var p = new DockPanel();
            var a = T(c.Code, 12, c.Special ? Brushes.OrangeRed : Brushes.LightSkyBlue); a.Width = 90; a.FontFamily = new FontFamily("Consolas");
            p.Children.Add(a); p.Children.Add(T((c.Special ? "[specjalne] " : "") + c.Desc)); sp.Children.Add(p);
        }
        sp.Children.Add(T("Blokady (elektroniczna samoczynna, Ie-104 § 8 pkt 17): 1M – Marysin (LK16, jednotorowa), 1C/2C – Chojny (LK540), 2A/1A – Andrzejów (LK17), 2O/1O – Olechów (LK541, blokada liniowa bez odstępów); wewnętrzne LCS: 2F/1F – Niciarniana↔Widzew, 2T/1T/102T/101T – tunel Fabryczna↔Niciarniana. Strzałka: szara dwustronna – stan neutralny, żółta – kierunek ustawiony, migający grot – żądanie pozwolenia, migający trzon – zwalnianie, czerwona – AZK, różowa – szlak zamknięty. Lampka TOR – możliwa zmiana kierunku.", 11, Brushes.Gray));
        Content = new ScrollViewer { Content = sp };
    }
}

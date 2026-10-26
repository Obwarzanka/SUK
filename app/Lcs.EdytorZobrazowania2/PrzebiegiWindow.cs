using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;
using LcsWidzew.Sim;
using Stacja = LcsWidzew.Model.Station;

namespace Lcs.EdytorZobrazowania2;

/// <summary>
/// Okno „Przebiegi” (etap 3): wszystkie przebiegi możliwe w układzie (propozycje – szare) i przebiegi przyjęte do tablicy (czarne),
/// przebiegi z tablicy, których nie ma w układzie – czerwone. Przyjęcie, usunięcie, zasadniczy, kontrola i test każdego przebiegu.
/// Zmiany idą do zobrazowania z cofaniem (Ctrl+Z w oknie edytora i tutaj); okno nic nie zapisuje samo.
/// </summary>
public class PrzebiegiWindow : Window
{
    /// <summary>Wiersz tablicy: przebieg z układu (<see cref="R"/>) i / albo z tablicy (<see cref="P"/>).</summary>
    public class Wiersz
    {
        public RouteDef R { get; init; }
        public ObrPrzebieg P { get; init; }
        public string Klucz => P?.Klucz ?? TablicaPrzebiegow.Z(R).Klucz;
        public bool WTablicy => P != null;
        public bool Brak => R == null;
        public string Tablica => P == null ? "propozycja" : R == null ? "brak w układzie" : "w tablicy";
        public string Zasadniczy => P?.Zasadniczy == true ? "zasadniczy" : P != null ? "wariantowy" : "";
        public string Rodzaj => (P?.Typ ?? R.Type) == "M" ? "manewrowy" : "pociągowy";
        public string Od => Stacja.Lbl(P?.Od ?? R.Start);
        public string Do => Stacja.Lbl(P?.Do ?? R.End) + (R?.EndKind switch { "szlak" => " (szlak)", "szlakM" => " (na szlak)", "gr" => " (do wjazdowego)", "kz" => " (koniec toru)", _ => "" });
        public string Zwrotnice => string.Join(" ", (P?.Zwr ?? R.SwKey).Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Stacja.Lbl));
        public string Odcinki => R == null ? "" : string.Join(" ", R.Secs.Select(Stacja.Lbl));
        public string V => R == null ? "" : R.Type == "M" ? "25" : $"{R.VCalc:0}";
        public string Ochrona => R?.FlankText ?? "";
        public string Test { get; set; } = "";
    }

    readonly MainWindow main;
    public Stacja St { get; private set; }
    /// <summary>Model układu (propozycje i wyszukiwanie dróg własnych przebiegów).</summary>
    public TablicaPrzebiegow.Model M { get; private set; }
    /// <summary>Przebiegi wyznaczone z układu (propozycje).</summary>
    public List<RouteDef> Uklad { get; private set; } = new();
    public List<Wiersz> Wiersze { get; private set; } = new();
    public readonly DataGrid Grid = new() { AutoGenerateColumns = false, IsReadOnly = true, SelectionMode = DataGridSelectionMode.Extended, HeadersVisibility = DataGridHeadersVisibility.Column, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, CanUserAddRows = false };
    public readonly ComboBox SigBox = new() { Width = 120, Margin = new Thickness(4, 0, 8, 0) };
    public readonly CheckBox TylkoBox = new() { Content = "tylko tablica", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 8, 0), ToolTip = "Pokaż tylko przebiegi przyjęte do tablicy" };
    public readonly TextBox UwagiBox = new() { IsReadOnly = true, Height = 130, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas"), FontSize = 12 };
    public readonly TextBlock Status = new() { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    public readonly Dictionary<string, TablicaPrzebiegow.Test> Testy = new();
    static readonly Brush Szary = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)), Czerwony = new SolidColorBrush(Color.FromRgb(0xb0, 0x30, 0x20));

    public PrzebiegiWindow(MainWindow main)
    {
        this.main = main;
        Title = $"Przebiegi – {main.Doc.Ut.Name}"; Width = 1250; Height = 760;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
        var bar = new ToolBar { Background = new SolidColorBrush(Color.FromRgb(0xe6, 0xe6, 0xe2)) };
        bar.Items.Add(new TextBlock { Text = "Od", VerticalAlignment = VerticalAlignment.Center }); bar.Items.Add(SigBox);
        bar.Items.Add(TylkoBox);
        bar.Items.Add(Btn("Przyjmij", (s, e) => Przyjmij(), "Zaznaczone propozycje do tablicy (Enter; dwuklik – przyjmij / usuń)"));
        bar.Items.Add(Btn("Usuń z tablicy", (s, e) => Usun(), "Zaznaczone przebiegi z tablicy (Del)"));
        bar.Items.Add(Btn("Zasadniczy", (s, e) => Zasadniczy(), "Zaznaczony przebieg zasadniczy dla swojej pary początek – koniec; pozostałe – wariantowe"));
        bar.Items.Add(new Separator());
        bar.Items.Add(Btn("Przyjmij wszystkie widoczne", (s, e) => PrzyjmijWidoczne(), "Wszystkie widoczne propozycje do tablicy – potem usuń niepotrzebne"));
        bar.Items.Add(new Separator());
        bar.Items.Add(Btn("Sprawdź", (s, e) => Sprawdz(), "Kontrola tablicy wobec układu"));
        bar.Items.Add(Btn("Testuj", (s, e) => Testuj(), "Każdy przebieg z tablicy osobno: polecenie dyżurnego w stanie zasadniczym, do 30 s – czy dochodzi do sygnału zezwalającego"));
        bar.Items.Add(Btn("Przelicz z układu", (s, e) => Przelicz(), "Propozycje od nowa – po „Wczytaj UT ponownie”"));
        bar.Items.Add(Status);

        void Col(string h, string p, double w = double.NaN) => Grid.Columns.Add(new DataGridTextColumn { Header = h, Binding = new Binding(p), Width = double.IsNaN(w) ? DataGridLength.Auto : new DataGridLength(w) });
        Col("Tablica", nameof(Wiersz.Tablica)); Col("", nameof(Wiersz.Zasadniczy)); Col("Rodzaj", nameof(Wiersz.Rodzaj)); Col("Od", nameof(Wiersz.Od)); Col("Do", nameof(Wiersz.Do));
        Col("Zwrotnice", nameof(Wiersz.Zwrotnice), 150); Col("Odcinki", nameof(Wiersz.Odcinki), 230); Col("V z układu", nameof(Wiersz.V)); Col("Ochrona boczna", nameof(Wiersz.Ochrona), 150);
        Grid.Columns.Add(new DataGridTextColumn { Header = "Test", Binding = new Binding(nameof(Wiersz.Test)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        Grid.LoadingRow += (s, e) => { if (e.Row.Item is Wiersz w) e.Row.Foreground = w.Brak ? Czerwony : w.WTablicy ? Brushes.Black : Szary; };
        Grid.MouseDoubleClick += (s, e) => { if (Grid.SelectedItem is Wiersz w) { if (w.WTablicy) main.UsunPrzebiegi(new[] { w.Klucz }); else if (w.R != null) main.PrzyjmijPrzebiegi(new[] { w.R }); } };
        Grid.PreviewKeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter) { Przyjmij(); e.Handled = true; }
            else if (e.Key == Key.Delete) { Usun(); e.Handled = true; }
        };
        PreviewKeyDown += (s, e) =>
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z) { main.Doc.Undo(); e.Handled = true; }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y) { main.Doc.Redo(); e.Handled = true; }
        };
        SigBox.SelectionChanged += (s, e) => Pokaz();
        TylkoBox.Click += (s, e) => Pokaz();

        var help = new TextBlock
        {
            Text = "Tablicę przebiegów projektuje człowiek: program proponuje przebiegi możliwe w układzie (szare), do symulatora idą tylko przyjęte (czarne). " +
                   "Droga, odcinki i ochrona boczna wynikają z układu; zależności specjalne (droga ochronna, sprzeczne, prędkość) – w symulatorze, „Tablica przebiegów”.",
            TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, FontSize = 12, Margin = new Thickness(6, 4, 6, 4),
        };
        var dock = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top); dock.Children.Add(bar);
        DockPanel.SetDock(help, Dock.Top); dock.Children.Add(help);
        DockPanel.SetDock(UwagiBox, Dock.Bottom); dock.Children.Add(UwagiBox);
        dock.Children.Add(Grid);
        Content = dock;

        main.Doc.Changed += Pokaz;
        Closed += (s, e) => main.Doc.Changed -= Pokaz;
        Przelicz();
    }

    static Button Btn(string t, RoutedEventHandler h, string tip) { var b = new Button { Content = t, ToolTip = tip, Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(2, 0, 2, 0) }; b.Click += h; return b; }

    /// <summary>Propozycje z układu od nowa (UT + obrazy z edytora).</summary>
    public void Przelicz()
    {
        var k = main.Kompiluj();
        M = TablicaPrzebiegow.ZUkladu(main.Doc.Ut, k.Zob);
        (St, Uklad, _) = M;
        var keep = SigBox.SelectedItem as string;
        SigBox.Items.Clear(); SigBox.Items.Add("wszystkie");
        foreach (var s in Uklad.Select(r => Stacja.Lbl(r.Start)).Distinct().OrderBy(x => x, StringComparer.Ordinal)) SigBox.Items.Add(s);
        SigBox.SelectedItem = keep != null && SigBox.Items.Contains(keep) ? keep : "wszystkie";
        Pokaz();
    }

    /// <summary>Wiersze wg filtra: propozycje z układu połączone z tablicą (wpisy bez przebiegu w układzie – na końcu).</summary>
    public void Pokaz()
    {
        if (St == null) return;
        var proj = main.Doc.P.Przebiegi ?? new();
        var all = Uklad.Select(r => new Wiersz { R = r, P = proj.FirstOrDefault(p => TablicaPrzebiegow.Pasuje(p, r)) })
            .Concat(proj.Where(p => !Uklad.Any(r => TablicaPrzebiegow.Pasuje(p, r))).Select(p => new Wiersz { P = p, R = TablicaPrzebiegow.Droga(M, p) })).ToList();
        var sig = SigBox.SelectedItem as string;
        Wiersze = all.Where(w => (sig == null || sig == "wszystkie" || w.Od == sig) && (TylkoBox.IsChecked != true || w.WTablicy)).ToList();
        foreach (var w in Wiersze) w.Test = Testy.TryGetValue(w.Klucz, out var t) ? $"{t.Wynik}: {t.Tekst}" : "";
        var sel = Grid.SelectedItems.OfType<Wiersz>().Select(w => w.Klucz).ToHashSet();
        Grid.ItemsSource = Wiersze;
        foreach (var w in Wiersze.Where(w => sel.Contains(w.Klucz))) Grid.SelectedItems.Add(w);
        Status.Text = main.Doc.P.Przebiegi == null
            ? $"tablica nie zaprojektowana – symulator wyznacza przebiegi sam; propozycji: {Uklad.Count}"
            : $"w tablicy {proj.Count} z {Uklad.Count} możliwych" + (all.Count(w => w.Brak) is int b && b > 0 ? $", brak w układzie: {b}" : "");
    }

    List<Wiersz> Zaznaczone => Grid.SelectedItems.OfType<Wiersz>().ToList();

    public void Przyjmij() => main.PrzyjmijPrzebiegi(Zaznaczone.Where(w => !w.WTablicy && w.R != null).Select(w => w.R));
    public void Usun() => main.UsunPrzebiegi(Zaznaczone.Where(w => w.WTablicy).Select(w => w.Klucz));
    public void Zasadniczy() { if (Zaznaczone.FirstOrDefault(w => w.WTablicy) is Wiersz w) main.UstawZasadniczy(w.Klucz); }
    public void PrzyjmijWidoczne()
    {
        var l = Wiersze.Where(w => !w.WTablicy && w.R != null).Select(w => w.R).ToList();
        if (l.Count == 0) return;
        if (!main.NoPrompt && MessageBox.Show(this, $"Przyjąć do tablicy {l.Count} widocznych propozycji?\nNiepotrzebne usuniesz potem z tablicy.", "Przebiegi", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        main.PrzyjmijPrzebiegi(l);
    }

    /// <summary>Kontrola tablicy – wynik w polu uwag; zwraca uwagi.</summary>
    public List<TablicaPrzebiegow.Uwaga> Sprawdz()
    {
        var uw = TablicaPrzebiegow.Sprawdz(main.Doc.P.Przebiegi, St, Uklad, M);
        UwagiBox.Text = uw.Count == 0 ? "Sprawdź: tablica bez uwag." : $"Sprawdź: błędów {uw.Count(u => u.Blad)}, uwag {uw.Count(u => !u.Blad)}\n" + string.Join("\n", uw.Select(u => (u.Blad ? "✗ " : "• ") + u.Tekst));
        return uw;
    }

    /// <summary>Test każdego przebiegu z tablicy – wynik w kolumnie „Test” i w polu uwag.</summary>
    public List<TablicaPrzebiegow.Test> Testuj()
    {
        var proj = main.Doc.P.Przebiegi;
        if (proj == null || proj.Count == 0) { UwagiBox.Text = "Testuj: tablica pusta – najpierw przyjmij przebiegi."; return new(); }
        var old = Cursor; Cursor = Cursors.Wait;
        try
        {
            var res = TablicaPrzebiegow.Testuj(main.Doc.Ut, main.Kompiluj().Zob, proj);
            Testy.Clear(); foreach (var t in res) Testy[t.P.Klucz] = t;
            int s = res.Count(t => t.Wynik == TablicaPrzebiegow.Test.Sygnal), b = res.Count(t => t.Wynik == TablicaPrzebiegow.Test.BezSygnalu), o = res.Count(t => t.Wynik == TablicaPrzebiegow.Test.Odrzucony);
            UwagiBox.Text = $"Testuj: {res.Count} przebiegów – sygnał zezwalający {s}, nastawione bez sygnału {b}, odrzucone {o}\n" +
                string.Join("\n", res.Where(t => t.Wynik != TablicaPrzebiegow.Test.Sygnal).Select(t => $"{(t.Wynik == TablicaPrzebiegow.Test.Odrzucony ? "✗" : "•")} {TablicaPrzebiegow.Opis(t.P)}: {t.Wynik} – {t.Tekst}"));
            Pokaz();
            return res;
        }
        finally { Cursor = old; }
    }
}

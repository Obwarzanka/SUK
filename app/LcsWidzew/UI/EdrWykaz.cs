using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using LcsWidzew.Sim;

namespace LcsWidzew.UI;

/// <summary>
/// Wykaz pociągów posterunku w oknie EDR (podręcznik EDR rozdz. 7.1, wygląd i kolory wg filmu ze stanowiska ISDR i legendy „Opis oznaczeń”).
/// PPM – Przyjmij / Wypraw pociąg (także z innego / na inny szlak), dwuklik w numer pociągu – wiersz w dzienniku bez okna,
/// przeciągnięcie numeru na dziennik – wiersz w tym dzienniku.
/// </summary>
public class WykazPociagow : DockPanel
{
    public const string Format = "edr-pociag";
    // kolory wg filmu i zrzutu EDR: tło oliwkowe, ciemne nagłówki, kolumny numerów i kierunków w kolorach
    internal static readonly Brush Tlo = EdrWindow.Pedzel(0xEE, 0xEE, 0xD2), Linie = EdrWindow.Pedzel(0xCF, 0xCF, 0xB4),
        NagCiemny = EdrWindow.Pedzel(0x55, 0x5F, 0x6E), NagBordo = EdrWindow.Pedzel(0x7E, 0x3B, 0x3B), NagNr1 = EdrWindow.Pedzel(0x2A, 0x9C, 0xA8), NagZ = EdrWindow.Pedzel(0x4E, 0x96, 0xC8),
        NagNr2 = EdrWindow.Pedzel(0x3C, 0x96, 0x46), NagW = EdrWindow.Pedzel(0x2E, 0x7E, 0x6A), NagTor = EdrWindow.Pedzel(0x8A, 0x8A, 0x8A), NagNrL = EdrWindow.Pedzel(0xD9, 0x8B, 0x2B),
        NagPost = EdrWindow.Pedzel(0xB0, 0x8A, 0x5A), NagPT = EdrWindow.Pedzel(0x3D, 0x7E, 0xBF), NagStacja = EdrWindow.Pedzel(0x3A, 0x3F, 0x8C),
        KolZ = EdrWindow.Pedzel(0xCF, 0xE0, 0xEE), KolW = EdrWindow.Pedzel(0xF3, 0xE3, 0xDB),
        KolTor = EdrWindow.Pedzel(0xA9, 0xA9, 0xA9), KolNrL = EdrWindow.Pedzel(0xF4, 0xE1, 0xC6), KolPT = EdrWindow.Pedzel(0x9D, 0xC2, 0xE6), KolStacja = EdrWindow.Pedzel(0xF7, 0xF7, 0xE6),
        // numer pociągu: turkusowy – nieparzysty, zielony – parzysty (uwaga właściciela 2026-10-28)
        NrNieparzysty = EdrWindow.Pedzel(0x9E, 0xE4, 0xEC), NrParzysty = EdrWindow.Pedzel(0x93, 0xC9, 0x8F),
        // legenda: wstęga i potwierdzenie czasu („Rodz.”): zielony – człowiek, czerwony – automatycznie (urządzenie), szary – dyspozytor
        WstPrzyjazd = EdrWindow.Pedzel(0xE6, 0x5C, 0xE6), WstOdjazd = EdrWindow.Pedzel(0x44, 0xE0, 0x44), WstPrzejazd = EdrWindow.Pedzel(0x44, 0xE0, 0xE0),
        RodzUrzadzenie = EdrWindow.Pedzel(0xC0, 0x28, 0x28), RodzCzlowiek = EdrWindow.Pedzel(0x2E, 0x8B, 0x3E), RodzDyspozytor = EdrWindow.Pedzel(0x8A, 0x8A, 0x8A),
        Opoznienie = EdrWindow.Pedzel(0x7E, 0x3B, 0x3B), Zaznaczony = EdrWindow.Pedzel(0x33, 0x99, 0xFF), Link = EdrWindow.Pedzel(0x1A, 0x4F, 0xC4);
    readonly EdrWindow okno;
    readonly ComboBox posterunek = new() { Width = 380, Margin = new Thickness(0, 0, 12, 4), FontWeight = FontWeights.Bold, FontSize = 14 };
    readonly TextBox filtr = new() { Width = 90, Margin = new Thickness(4, 0, 0, 0), Background = Brushes.White }, czestotliwosc = new() { Width = 34, Text = "3", Background = Brushes.White, Margin = new Thickness(4, 0, 12, 0) },
        zakres = new() { Width = 34, Text = "6", Background = Brushes.White, Margin = new Thickness(4, 0, 12, 0) },
        uwEksp = Uwagi(), uwWlasne = Uwagi(), uwPrzew = Uwagi();
    readonly CheckBox biezace = new() { Content = "Dane\nbieżące", IsChecked = true, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) },
        daty = new() { Content = "Prezentuj daty", IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
    readonly TextBlock czas = new() { Background = Brushes.White, Padding = new Thickness(4, 1, 4, 1), MinWidth = 120 }, infoZ = new() { Foreground = Link }, infoDo = new() { Foreground = Link }, infoP = new() { Foreground = Link },
        infoN = new() { Foreground = Link }, info = new() { Foreground = Brushes.DimGray, Margin = new Thickness(0, 4, 0, 0), FontSize = 11, TextWrapping = TextWrapping.Wrap };
    readonly Hyperlink potwLink = new(), pocLink = new();
    static readonly string[] Potwierdzenia = { "wszystkie", "potwierdzone", "niepotwierdzone" }, Pociagi = { "wszystkie", "kursujące", "niekursujące" };
    int potwFiltr, pocFiltr;
    readonly DataGrid grid;
    readonly List<DataGridTextColumn> godziny = new();
    List<EdrPociag> lista = new(), wszystkie = new();
    string podpis, post, aktualny;
    /// <summary>Czas pobrania danych (minuta symulacji) – przy „Dane bieżące” dane pobierają się co „Częstotliwość odświeżania” minut.</summary>
    double? pobrano;
    Point? start;
    bool admin;
    /// <summary>Zaznaczony pociąg zmienił się (zakładki „Opis pociągu”, „Trasa pociągu”, „Przewoźnik”).</summary>
    public event Action<EdrPociag> Wybrano;
    Grid gora;
    /// <summary>Zegar okna stoi w prawym górnym rogu – pasek nad wykazem kończy się przed nim.</summary>
    public void MiejsceNaZegar(double szer) => gora.Margin = new Thickness(4, 3, Math.Max(4, szer), 3);

    static TextBox Uwagi() => new() { Width = 250, Height = 34, Background = Brushes.White, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 0, 12, 0), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

    public WykazPociagow(EdrWindow okno)
    {
        this.okno = okno;
        Background = Tlo;
        // pasek nad wykazem (jak na zrzucie EDR): posterunek, odświeżanie, czas pobrania; numer pociągu, filtry, daty; uwagi; informacje dodatkowe
        gora = new Grid { Margin = new Thickness(4, 3, 4, 3) };
        gora.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); gora.ColumnDefinitions.Add(new ColumnDefinition());
        var lewa = new StackPanel();
        static TextBlock E(string t) => new() { Text = t, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
        var r1 = new StackPanel { Orientation = Orientation.Horizontal };
        r1.Children.Add(posterunek);
        r1.Children.Add(E("Częstotliwość\nodświeżania (w min.):")); r1.Children.Add(czestotliwosc);
        r1.Children.Add(E("-/+\ngodz.")); r1.Children.Add(zakres);
        r1.Children.Add(biezace);
        r1.Children.Add(E("Czas pobrania\ndanych z bazy:")); r1.Children.Add(new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Child = czas, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) });
        lewa.Children.Add(r1);
        var r2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 4) };
        r2.Children.Add(E("Nr pociągu:")); r2.Children.Add(filtr);
        var szukaj = new Button { Content = new TextBlock { Text = "", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 11 }, Width = 24, Margin = new Thickness(2, 0, 16, 0), ToolTip = "Szukaj pociągu o numerze" };
        szukaj.Click += (a, e) => { var p = lista.FirstOrDefault(x => x.Nr.Contains(filtr.Text.Trim()) || x.NrWyj.Contains(filtr.Text.Trim())); if (p != null) { grid.SelectedItem = p; grid.ScrollIntoView(p); } };
        r2.Children.Add(szukaj);
        Link2(r2, potwLink, () => { potwFiltr = (potwFiltr + 1) % 3; potwLink.Inlines.Clear(); potwLink.Inlines.Add("Potwierdzenie: " + Potwierdzenia[potwFiltr]); }, "Potwierdzenie: wszystkie", "Klik – filtr wg potwierdzenia czasu przejazdu: wszystkie / potwierdzone / niepotwierdzone");
        Link2(r2, pocLink, () => { pocFiltr = (pocFiltr + 1) % 3; pocLink.Inlines.Clear(); pocLink.Inlines.Add("Pociągi: " + Pociagi[pocFiltr]); }, "Pociągi: wszystkie", "Klik – filtr: wszystkie / kursujące (K) / niekursujące (NK)");
        r2.Children.Add(daty);
        var opis = new Button { Content = "Opis oznaczeń", Padding = new Thickness(10, 1, 10, 1), Margin = new Thickness(20, 0, 0, 0) };
        opis.Click += (a, e) => new LegendaEdr { Owner = okno }.Show();
        r2.Children.Add(opis);
        lewa.Children.Add(r2);
        var r3 = new StackPanel { Orientation = Orientation.Horizontal };
        r3.Children.Add(E("Uwagi\neksp.:")); r3.Children.Add(uwEksp); r3.Children.Add(E("Uwagi\nwłasne:")); r3.Children.Add(uwWlasne); r3.Children.Add(E("Uwagi\nPrzew.:")); r3.Children.Add(uwPrzew);
        uwEksp.ToolTip = "Uwagi eksploatacyjne – wpisuje dyspozytor"; uwPrzew.ToolTip = "Uwagi przewoźnika – wpisuje dyspozytor"; uwWlasne.ToolTip = "Uwagi własne dyżurnego do pociągu (Enter albo wyjście z pola – zapis)";
        foreach (var (tb, pole) in new[] { (uwEksp, "ekspl"), (uwWlasne, "wlasne"), (uwPrzew, "przew") })
        {
            var (t, pl) = (tb, pole);
            void Zapisz() { if (Wybrany is EdrPociag p && t.Text.Trim() != (pl == "ekspl" ? p.UwagiEksp : pl == "przew" ? p.UwagiPrzew : p.UwagiWlasne)) okno.Wyslij(new EdrPolecenie { Rodzaj = "uwagi", Pole = pl, Wartosc = t.Text, Klucz = p.Klucz, Post = post }); }
            t.LostFocus += (a, e) => Zapisz();
            t.KeyDown += (a, e) => { if (e.Key == Key.Enter) { Zapisz(); e.Handled = true; } };
        }
        lewa.Children.Add(r3);
        gora.Children.Add(lewa);
        var c4 = new Grid { Margin = new Thickness(20, 0, 0, 0) };
        c4.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); c4.ColumnDefinitions.Add(new ColumnDefinition());
        for (int i = 0; i < 6; i++) c4.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var tyt = new TextBlock { Text = "Informacje dodatkowe:", FontSize = 11, Margin = new Thickness(0, 0, 0, 4) }; Grid.SetColumnSpan(tyt, 2); c4.Children.Add(tyt);
        void Info(int r, string l, TextBlock v)
        {
            var t = new TextBlock { Text = l, FontWeight = FontWeights.Bold, FontSize = 11.5, Margin = new Thickness(0, 0, 6, 0) }; Grid.SetRow(t, r); c4.Children.Add(t);
            v.FontSize = 11.5; Grid.SetRow(v, r); Grid.SetColumn(v, 1); c4.Children.Add(v);
        }
        Info(1, "Z:", infoZ); Info(2, "Do:", infoDo); Info(3, "P:", infoP); Info(4, "N:", infoN);
        Grid.SetRow(info, 5); Grid.SetColumnSpan(info, 2); c4.Children.Add(info);
        Grid.SetColumn(c4, 1); gora.Children.Add(c4);
        posterunek.SelectionChanged += (a, e) => { if (posterunek.SelectedValue is string p && p != post) { post = p; podpis = null; pobrano = null; okno.Odswiez(true); } };
        filtr.TextChanged += (a, e) => { podpis = null; pobrano = null; okno.Odswiez(true); };
        zakres.TextChanged += (a, e) => { podpis = null; pobrano = null; okno.Odswiez(true); };
        biezace.Click += (a, e) => { pobrano = null; okno.Odswiez(true); };
        daty.Click += (a, e) => Godziny();
        SetDock(gora, Dock.Top); Children.Add(gora);

        grid = new DataGrid
        {
            AutoGenerateColumns = false, IsReadOnly = true, SelectionMode = DataGridSelectionMode.Single, SelectionUnit = DataGridSelectionUnit.FullRow,
            HeadersVisibility = DataGridHeadersVisibility.Column, CanUserAddRows = false, CanUserSortColumns = false, CanUserResizeRows = false,
            GridLinesVisibility = DataGridGridLinesVisibility.All, FontSize = 13, RowHeight = 25, BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray,
            HorizontalGridLinesBrush = Linie, VerticalGridLinesBrush = Linie, Background = Tlo, RowBackground = Brushes.White, AlternatingRowBackground = EdrWindow.Pedzel(0xFB, 0xFB, 0xEC)
        };
        Kolumna("K", nameof(EdrPociag.Kursuje), NagCiemny, null, "K – kursuje (zaznacza dyspozytor)", znak: true);
        Kolumna("NK", nameof(EdrPociag.NieKursuje), NagCiemny, null, "NK – nie kursuje (zaznacza dyspozytor)", znak: true);
        godziny.Add(Kolumna("Przyjazd plan.", nameof(EdrPociag.PrzPlanD), NagBordo, null));
        Kolumna("+/-", nameof(EdrPociag.PrzOdch), NagBordo, null, "Odchylenie przyjazdu od rozkładu [min] – bordowe tło: opóźnienie", opozn: true, wysrodkuj: true);
        godziny.Add(Kolumna("Przyjazd rzecz.", nameof(EdrPociag.PrzRzeczD), NagBordo, null));
        Kolumna("Rodz.", nameof(EdrPociag.Rodz), NagCiemny, null, "Kategoria pociągu wg Ir-11; tło – czas przejazdu potwierdzony: zielone – przez człowieka, czerwone – automatycznie, szare – przez dyspozytora", rodz: true, wysrodkuj: true);
        Kolumna("Nr poc.", nameof(EdrPociag.NrNieparzysty), NagNr1, NrNieparzysty, "Numer pociągu nieparzysty; dwuklik – wpis w dzienniku, przeciągnięcie na dziennik – wpis w tym dzienniku", gruby: true, wysrodkuj: true);
        Kolumna("Z kierunku post.", nameof(EdrPociag.ZKierunku), NagZ, KolZ, wysrodkuj: true);
        Kolumna("Nr poc.", nameof(EdrPociag.NrParzysty), NagNr2, NrParzysty, "Numer pociągu parzysty; dwuklik – wpis w dzienniku, przeciągnięcie na dziennik – wpis w tym dzienniku", gruby: true, wysrodkuj: true);
        Kolumna("W kierunku post.", nameof(EdrPociag.WKierunku), NagW, KolW, wysrodkuj: true);
        Kolumna("Tor", nameof(EdrPociag.TorRzecz), NagTor, KolTor, "Tor, na który pociąg wjechał", wysrodkuj: true);
        Kolumna("Nr L.", nameof(EdrPociag.Linia), NagNrL, KolNrL, "Numer linii kolejowej", wysrodkuj: true);
        Kolumna("P. zam.", nameof(EdrPociag.PZam), NagPost, KolNrL, "Postój zamówiony wg rozkładu [min]", wysrodkuj: true);
        Kolumna("P. obl.", nameof(EdrPociag.PObl), NagPost, KolNrL, "Postój obliczony z wykonania [min]", wysrodkuj: true);
        Kolumna("Typ p.", nameof(EdrPociag.Postoj), NagCiemny, null, "Typ postoju: ph – postój handlowy", gruby: true, wysrodkuj: true);
        Kolumna("P T", nameof(EdrPociag.Tor), NagPT, KolPT, "Tor stacyjny wg rozkładu", wysrodkuj: true);
        godziny.Add(Kolumna("Odjazd plan.", nameof(EdrPociag.OdjPlanD), NagBordo, null));
        Kolumna("+/-", nameof(EdrPociag.OdjOdch), NagBordo, null, "Odchylenie odjazdu od rozkładu [min] – bordowe tło: opóźnienie", opozn: true, wysrodkuj: true);
        godziny.Add(Kolumna("Odjazd rzecz.", nameof(EdrPociag.OdjRzeczD), NagBordo, null));
        Kolumna("Stacja początkowa", nameof(EdrPociag.StacjaPocz), NagStacja, KolStacja, wysrodkuj: true);
        Kolumna("Stacja końcowa", nameof(EdrPociag.StacjaKonc), NagStacja, KolStacja, wysrodkuj: true);
        Kolumna("Przewoźnik", nameof(EdrPociag.Przewoznik), NagStacja, KolStacja, wysrodkuj: true, szer: 1);
        grid.LoadingRow += (a, e) => { if (e.Row.Item is EdrPociag p) e.Row.ToolTip = $"{p.Nr}{(p.NrWyj != p.Nr ? " / " + p.NrWyj : "")}{(p.Nazwa != "" ? " " + p.Nazwa : "")} – {p.Stan}"; };

        var menu = new ContextMenu();
        MenuItem M(string t, string co)
        {
            var m = new MenuItem { Header = t };
            if (co != null) m.Click += (a, e) => { if (Wybrany is EdrPociag p) okno.PolecenieWykazu(post, p, co); };
            else { m.IsEnabled = false; m.ToolTip = "W kolejnych etapach"; ToolTipService.SetShowOnDisabled(m, true); }
            menu.Items.Add(m); return m;
        }
        var mp = M("Przyjmij pociąg", "przyj"); var mw = M("Wypraw pociąg", "wypr");
        M("Przyjmij z innego szlaku", "przyjinny"); M("Wypraw na inny szlak", "wyprinny");
        menu.Items.Add(new Separator());
        M("Pociąg STÓJ", null); M("Zgłoszenie gotowości", null); M("Analiza pociągu", null);
        // dyspozytor (w symulatorze: administrator / instruktor): potwierdzenie czasu przejazdu
        var sepD = new Separator(); menu.Items.Add(sepD);
        var md = new MenuItem { Header = "Potwierdź czas przejazdu (dyspozytor)" };
        md.Click += (a, e) => { if (Wybrany is EdrPociag p) okno.Wyslij(new EdrPolecenie { Rodzaj = "potwierdz", Klucz = p.Klucz, Wartosc = p.Potw == "dyspozytor" ? "nie" : "tak" }); };
        menu.Items.Add(md);
        menu.Opened += (a, e) =>
        {
            var p = Wybrany;
            mp.IsEnabled = p != null && okno.DziennikSzlaku(post, p.ZSzlak) != null;
            mw.IsEnabled = p != null && okno.DziennikSzlaku(post, p.WSzlak) != null;
            sepD.Visibility = md.Visibility = admin ? Visibility.Visible : Visibility.Collapsed;
            md.Header = p?.Potw == "dyspozytor" ? "Cofnij potwierdzenie dyspozytora" : "Potwierdź czas przejazdu (dyspozytor)";
        };
        grid.ContextMenu = menu;
        grid.PreviewMouseRightButtonDown += (a, e) => { if (Wiersz(e.OriginalSource) is DataGridRow r) grid.SelectedItem = r.Item; };
        grid.SelectionChanged += (a, e) => Informacje();

        // do dziennika przepisuje się pociąg tylko dwuklikiem w numer pociągu (uwaga właściciela 2026-10-28)
        grid.MouseDoubleClick += (a, e) => { if (Komorka(e.OriginalSource)?.Column?.Header as string == "Nr poc." && Wiersz(e.OriginalSource)?.Item is EdrPociag p) okno.DodajZWykazu(post, p); };
        // K / NK – klik dyspozytora (administrator / instruktor) zmienia „kursuje”
        grid.MouseLeftButtonUp += (a, e) =>
        {
            if (!admin || Komorka(e.OriginalSource)?.Column?.Header is not ("K" or "NK") || Wiersz(e.OriginalSource)?.Item is not EdrPociag p) return;
            var k = Komorka(e.OriginalSource).Column.Header as string;
            if (k == "K" != p.Kursuje) okno.Wyslij(new EdrPolecenie { Rodzaj = "kursuje", Klucz = p.Klucz, Wartosc = k });
        };
        grid.PreviewMouseLeftButtonDown += (a, e) => start = Wiersz(e.OriginalSource) != null ? e.GetPosition(grid) : null;
        grid.PreviewMouseMove += (a, e) =>
        {
            if (start is not Point s0 || e.LeftButton != MouseButtonState.Pressed) return;
            var d = e.GetPosition(grid) - s0;
            if (Math.Abs(d.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(d.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            start = null;
            if (Wiersz(e.OriginalSource)?.Item is EdrPociag p) { grid.SelectedItem = p; DragDrop.DoDragDrop(grid, new DataObject(Format, p.Klucz), DragDropEffects.Copy); }
        };
        Children.Add(grid);
    }

    void Link2(Panel p, Hyperlink h, Action klik, string tekst, string opis)
    {
        h.Inlines.Add(tekst); h.Foreground = Link; h.Click += (a, e) => { klik(); podpis = null; pobrano = null; okno.Odswiez(true); };
        p.Children.Add(new TextBlock { Inlines = { h }, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0), ToolTip = opis });
    }

    /// <summary>„Prezentuj daty”: godziny z datą („dd-MM HH:mm”) albo same godziny.</summary>
    void Godziny()
    {
        bool d = daty.IsChecked == true;
        string[] pola = { nameof(EdrPociag.PrzPlan), nameof(EdrPociag.PrzRzecz), nameof(EdrPociag.OdjPlan), nameof(EdrPociag.OdjRzecz) };
        for (int i = 0; i < godziny.Count; i++) godziny[i].Binding = new Binding(pola[i] + (d ? "D" : ""));
    }

    public EdrPociag Wybrany => grid.SelectedItem as EdrPociag;

    void Informacje()
    {
        var p = Wybrany;
        infoZ.Text = p?.StacjaPocz ?? ""; infoDo.Text = p?.StacjaKonc ?? ""; infoP.Text = p?.Przewoznik ?? ""; infoN.Text = p?.Nazwa ?? "";
        if (!uwEksp.IsKeyboardFocused) uwEksp.Text = p?.UwagiEksp ?? "";
        if (!uwPrzew.IsKeyboardFocused) uwPrzew.Text = p?.UwagiPrzew ?? "";
        if (!uwWlasne.IsKeyboardFocused) uwWlasne.Text = p?.UwagiWlasne ?? "";
        uwEksp.IsReadOnly = uwPrzew.IsReadOnly = !admin || p == null; uwWlasne.IsReadOnly = p == null;
        uwEksp.Background = uwPrzew.Background = uwEksp.IsReadOnly ? EdrWindow.Pedzel(0xF4, 0xF4, 0xF4) : Brushes.White;
        Wybrano?.Invoke(p);
    }

    static DataGridRow Wiersz(object src)
    {
        for (var o = src as DependencyObject; o != null; o = o is Visual || o is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(o) : LogicalTreeHelper.GetParent(o))
            if (o is DataGridRow r) return r;
        return null;
    }

    static DataGridCell Komorka(object src)
    {
        for (var o = src as DependencyObject; o != null; o = o is Visual || o is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(o) : LogicalTreeHelper.GetParent(o))
            if (o is DataGridCell c) return c;
        return null;
    }

    /// <summary>
    /// Kolumna wykazu: kolorowy nagłówek z białym napisem, tło komórek kolumny; wstęga (przyjazd / odjazd / przejazd wg legendy)
    /// przykrywa tło całego wiersza, zaznaczenie – niebieskie.
    /// </summary>
    DataGridTextColumn Kolumna(string naglowek, string pole, Brush nag, Brush tlo, string opis = null, bool znak = false, bool gruby = false, bool wysrodkuj = false, bool opozn = false, bool rodz = false, bool nr = false, double szer = 0)
    {
        var hs = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        hs.Setters.Add(new Setter(Control.BackgroundProperty, new LinearGradientBrush(Rozjasnij(nag), ((SolidColorBrush)nag).Color, 90)));
        hs.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
        hs.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.DimGray));
        hs.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 0)));
        hs.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4, 3, 4, 3)));
        hs.Setters.Add(new Setter(Control.FontSizeProperty, 12.0));
        hs.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        hs.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
        if (opis != null) hs.Setters.Add(new Setter(ToolTipService.ToolTipProperty, opis));

        var cs = new Style(typeof(DataGridCell));
        cs.Setters.Add(new Setter(Control.BackgroundProperty, tlo ?? Brushes.Transparent));
        cs.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.Black));
        cs.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        if (opozn) cs.Triggers.Add(new DataTrigger { Binding = new Binding(pole) { Converter = new Opozniony() }, Value = true, Setters = { new Setter(Control.BackgroundProperty, Opoznienie), new Setter(Control.ForegroundProperty, Brushes.White) } });
        if (rodz)
            foreach (var (w, b) in new[] { ("czlowiek", RodzCzlowiek), ("auto", RodzUrzadzenie), ("dyspozytor", RodzDyspozytor) })
                cs.Triggers.Add(new DataTrigger { Binding = new Binding(nameof(EdrPociag.Potw)), Value = w, Setters = { new Setter(Control.BackgroundProperty, b), new Setter(Control.ForegroundProperty, Brushes.White) } });
        if (nr)
        {
            cs.Triggers.Add(new DataTrigger { Binding = new Binding(pole) { Converter = new Parzystosc() }, Value = 1, Setters = { new Setter(Control.BackgroundProperty, NrNieparzysty) } });
            cs.Triggers.Add(new DataTrigger { Binding = new Binding(pole) { Converter = new Parzystosc() }, Value = 2, Setters = { new Setter(Control.BackgroundProperty, NrParzysty) } });
        }
        // wstęga = pasek zaznaczenia: tylko zaznaczony pociąg (kliknięty albo aktualny – zaznaczenie przeskakuje samo), kolor wg stanu pociągu
        cs.Triggers.Add(new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true, Setters = { new Setter(Control.BackgroundProperty, Zaznaczony), new Setter(Control.ForegroundProperty, Brushes.White) } });
        foreach (var (w, b) in new[] { ("przyjazd", WstPrzyjazd), ("odjazd", WstOdjazd), ("przejazd", WstPrzejazd) })
        {
            var mt = new MultiDataTrigger();
            mt.Conditions.Add(new Condition(new Binding(nameof(DataGridCell.IsSelected)) { RelativeSource = RelativeSource.Self }, true));
            mt.Conditions.Add(new Condition(new Binding(nameof(EdrPociag.Wstega)), w));
            mt.Setters.Add(new Setter(Control.BackgroundProperty, b)); mt.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.Black));
            cs.Triggers.Add(mt);
        }

        var es = new Style(typeof(TextBlock));
        es.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(4, 0, 4, 0)));
        es.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        if (wysrodkuj || znak) es.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
        if (gruby) es.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));
        if (znak) es.Setters.Add(new Setter(TextBlock.FontSizeProperty, 15.0));
        var b2 = new Binding(pole);
        if (znak) b2.Converter = new Znak();
        var k = new DataGridTextColumn
        {
            Header = naglowek, Binding = b2, HeaderStyle = hs, CellStyle = cs, ElementStyle = es,
            Width = szer > 0 ? new DataGridLength(szer, DataGridLengthUnitType.Star) : DataGridLength.Auto, MinWidth = znak ? 26 : 44
        };
        grid.Columns.Add(k);
        return k;
    }

    static Color Rozjasnij(Brush b) { var c = ((SolidColorBrush)b).Color; return Color.FromRgb((byte)(c.R + (255 - c.R) / 4), (byte)(c.G + (255 - c.G) / 4), (byte)(c.B + (255 - c.B) / 4)); }

    class Znak : IValueConverter
    {
        public object Convert(object v, Type t, object p, System.Globalization.CultureInfo c) => v is true ? "☑" : "☐";
        public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c) => null;
    }

    class Opozniony : IValueConverter
    {
        public object Convert(object v, Type t, object p, System.Globalization.CultureInfo c) => v is string s && s.StartsWith("+");
        public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c) => null;
    }

    /// <summary>1 – numer nieparzysty, 2 – parzysty, 0 – brak numeru.</summary>
    class Parzystosc : IValueConverter
    {
        public object Convert(object v, Type t, object p, System.Globalization.CultureInfo c) => v is string s && s.Trim().Any(char.IsDigit) ? Edr.KolumnaNumeru(s) : 0;
        public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c) => null;
    }

    /// <summary>Wiersz wykazu po kluczu (przeciąganie): posterunek i pociąg.</summary>
    public (string, EdrPociag)? Znajdz(string klucz) => wszystkie.FirstOrDefault(x => x.Klucz == klucz) is EdrPociag p ? (post, p) : null;

    static int Liczba(TextBox t, int dom) => int.TryParse(t.Text.Trim(), out var v) && v > 0 ? v : dom;

    /// <summary>
    /// Nowe dane: lista posterunków stanowiska i wykaz wybranego. Przy „Dane bieżące” wykaz pobiera się co „Częstotliwość odświeżania” minut
    /// (i od razu po własnym poleceniu – <paramref name="sila"/>); bez – tylko po zmianie posterunku albo filtra. Zakres: −/+ godz. od bieżącej chwili.
    /// </summary>
    public void Pokaz(EdrData d, bool sila = true)
    {
        admin = d.Admin;
        var posty = d.Posterunki.Where(x => d.Wykaz.ContainsKey(x.Key)).ToList();
        if (posterunek.Items.Count != posty.Count || posty.Any(x => !posterunek.Items.Cast<KeyValuePair<string, string>>().Any(y => y.Key == x.Key && y.Value == x.Value)))
        {
            posterunek.ItemsSource = posty; posterunek.DisplayMemberPath = "Value"; posterunek.SelectedValuePath = "Key";
            // domyślnie posterunek, który ma najwięcej pociągów
            if (post == null || !d.Wykaz.ContainsKey(post)) post = d.Wykaz.OrderByDescending(x => x.Value.Count).FirstOrDefault().Key;
            posterunek.SelectedValue = post;
        }
        double minuta = d.Doba * 1440 + (d.Zegar.Length >= 5 && int.TryParse(d.Zegar[..2], out var hh) && int.TryParse(d.Zegar[3..5], out var mm) ? hh * 60 + mm : 0);
        bool pobierz = pobrano == null || (biezace.IsChecked == true && (sila || minuta - pobrano >= Liczba(czestotliwosc, 3)));
        if (!pobierz) return;
        pobrano = minuta;
        czas.Text = $"{d.Data} {d.Zegar[..5]}";
        wszystkie = post != null && d.Wykaz.TryGetValue(post, out var w) ? w : new();
        double h = Liczba(zakres, 6) * 3600;
        var f = filtr.Text.Trim();
        var l = wszystkie.Where(x => Math.Abs(x.Sort - d.T) <= h || x.Stan == "na stacji")
            .Where(x => f == "" || x.Nr.Contains(f) || x.NrWyj.Contains(f))
            .Where(x => potwFiltr == 0 || x.Potwierdzony == (potwFiltr == 1))
            .Where(x => pocFiltr == 0 || x.Kursuje == (pocFiltr == 1)).ToList();
        var nowy = string.Join("\n", l.Select(p => string.Join("|", p.Klucz, p.Nr, p.NrWyj, p.ZKierunku, p.WKierunku, p.Tor, p.TorRzecz, p.PrzPlan, p.PrzRzecz, p.OdjPlan, p.OdjRzecz, p.Stan, p.Wstega, p.Potw, p.Kursuje, p.UwagiEksp, p.UwagiPrzew, p.UwagiWlasne)));
        info.Text = $"Pociągów: {l.Count}, na stacji: {l.Count(x => x.NaStacji)}, odjechało: {l.Count(x => x.Stan == "odjechał")}. Dwuklik w numer pociągu – wpis w dzienniku, przeciągnięcie na dziennik – wpis w tym dzienniku, PPM – polecenia.";
        if (nowy == podpis) return;
        podpis = nowy;
        var wybrany = Wybrany?.Klucz;
        var sv = Przewijanie(grid); double off = sv?.VerticalOffset ?? 0;
        lista = l; grid.ItemsSource = l;
        // aktualny pociąg: najwcześniejszy ze wstęgą (stoi na stacji albo zbliża się z szlaku); gdy się zmieni – zaznaczenie przeskakuje na niego
        var akt = l.Where(x => x.Wstega != "").OrderBy(x => x.Stan == "na stacji" ? 0 : 1).ThenBy(x => x.Sort).FirstOrDefault();
        if (akt != null && akt.Klucz != aktualny) { aktualny = akt.Klucz; wybrany = akt.Klucz; Dispatcher.BeginInvoke(() => { if (grid.SelectedItem != null) grid.ScrollIntoView(grid.SelectedItem); }, System.Windows.Threading.DispatcherPriority.Loaded); }
        if (wybrany != null) grid.SelectedItem = l.FirstOrDefault(x => x.Klucz == wybrany);
        Informacje();
        if (sv != null) Dispatcher.BeginInvoke(() => sv.ScrollToVerticalOffset(off), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    static ScrollViewer Przewijanie(DependencyObject o)
    {
        if (o is ScrollViewer s) return s;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++) if (Przewijanie(VisualTreeHelper.GetChild(o, i)) is ScrollViewer r) return r;
        return null;
    }

    /// <summary>Zrzut i testy: wybór posterunku i pociągu.</summary>
    public void Wybierz(string postKod, string nr)
    {
        if (postKod != null) posterunek.SelectedValue = postKod;
        grid.SelectedItem = lista.FirstOrDefault(x => x.Nr == nr || x.NrWyj == nr);
        if (grid.SelectedItem != null) grid.ScrollIntoView(grid.SelectedItem);
    }
    public string Posterunek => post;
    public EdrPociag Pociag(string nr) => wszystkie.FirstOrDefault(x => x.Nr == nr || x.NrWyj == nr);
}

/// <summary>Okno „Legenda” (przycisk „Opis oznaczeń”): znaczenie kolorów wykazu pociągów, wstęga, typy postoju – jak w EDR na stanowisku ISDR.</summary>
public class LegendaEdr : Window
{
    public LegendaEdr()
    {
        Title = "Legenda"; Width = 760; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EdrWindow.Tlo; FontFamily = new FontFamily("Segoe UI"); FontSize = 11; ShowInTaskbar = false;
        var sp = new StackPanel { Margin = new Thickness(10) };
        sp.Children.Add(new TextBlock { Text = "Znaczenie kolorów", Margin = new Thickness(0, 0, 0, 6) });
        var g = new Grid();
        for (int i = 0; i < 3; i++) g.ColumnDefinitions.Add(new ColumnDefinition());
        var kol = new[] { new StackPanel(), new StackPanel(), new StackPanel() };
        for (int i = 0; i < 3; i++) { Grid.SetColumn(kol[i], i); g.Children.Add(kol[i]); }
        void Poz(int k, string napis, Brush tlo, string opis, bool ramka = false, bool ciemny = false)
        {
            var r = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            r.Children.Add(new Border
            {
                Width = 76, Height = 20, Background = tlo ?? Brushes.Transparent, BorderBrush = ramka ? Brushes.Red : null, BorderThickness = new Thickness(ramka ? 2 : 0),
                Child = new TextBlock { Text = napis, Foreground = ciemny || tlo == null ? Brushes.Black : Brushes.White, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            });
            r.Children.Add(new TextBlock { Text = opis, Width = 150, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            kol[k].Children.Add(r);
        }
        Brush P(byte r, byte g2, byte b) => EdrWindow.Pedzel(r, g2, b);
        Poz(0, "K", P(0x7B, 0x4A, 0x8C), "Kursuje"); Poz(0, "K", P(0xE8, 0x5F, 0x5A), "Pociąg posiada uwagi eksploatacyjne"); Poz(0, "NK", P(0x6A, 0x3A, 0x7C), "Nie kursuje");
        Poz(0, "NK", P(0x3A, 0x5A, 0xE8), "Rezygnacja z trasy przez przewoźnika (nie uruchamiać)"); Poz(0, "NK", P(0x2E, 0x7A, 0x6E), "Odwołanie pociągu w zamknięciu (nie uruchamiać)");
        Poz(0, "+/-", WykazPociagow.Opoznienie, "Opóźnienie"); Poz(0, "+/-", P(0xA8, 0xD8, 0xA0), "Pociąg objęty rozliczeniem z tytułu JAKOŚCI", ciemny: true);
        Poz(1, "Przyjazd rzecz.", P(0x8A, 0x6A, 0xE0), "Pociąg kursuje według uzupełniającego rozkładu jazdy"); Poz(1, "Rodz.", WykazPociagow.RodzDyspozytor, "Czas przejazdu potwierdzony przez dyspozytora");
        Poz(1, "Rodz.", WykazPociagow.RodzUrzadzenie, "Czas przejazdu potwierdzony automatycznie (urządzenie)"); Poz(1, "Rodz.", WykazPociagow.RodzCzlowiek, "Czas przejazdu potwierdzony przez człowieka (wpis w dzienniku)");
        Poz(1, "Nr pociągu", null, "Pociągi intermodalne z prędkościami pasażerskimi do 120 km/h", ramka: true); Poz(1, "Nr pociągu*", null, "Pociągi zdające i/lub oczekujące na skomunikowanie");
        Poz(1, "Nr pociągu", P(0xE8, 0xD0, 0x40), "Przesyłki nadzwyczajne", ciemny: true);
        Poz(2, "Z kierunku post.", P(0xD8, 0x8A, 0x9A), "Pociąg aktualnie przewozi Towary Niebezpieczne"); Poz(2, "W kierunku post.", P(0xF0, 0x70, 0x40), "Pociąg przewozi Towary Wysokiego Ryzyka (TWR)");
        Poz(2, "Typ post.", null, "Postój handlowy na danej stacji"); Poz(2, "Odjazd rzecz.", P(0xF0, 0xB0, 0xB0), "Postój pociągu (POCIĄG STÓJ)", ciemny: true);
        Poz(2, "Odjazd rzecz.", P(0xE8, 0x70, 0x50), "Postój pociągu z wprowadzoną prognozą odjazdu (POCIĄG STÓJ z prognozą)"); Poz(2, "Przewoźnik", P(0xF0, 0xD0, 0x40), "Poc. pasażerski posiadający uwagi przewoźnika", ciemny: true);
        sp.Children.Add(g);
        sp.Children.Add(new TextBlock { Text = "Kolor paska zaznaczenia – tzw. „wstęga”", Margin = new Thickness(0, 6, 0, 4) });
        var w = new Grid();
        for (int i = 0; i < 3; i++) w.ColumnDefinitions.Add(new ColumnDefinition());
        int k2 = 0;
        foreach (var (b, t) in new[] { (WykazPociagow.WstPrzyjazd, "Przyjazd poc."), (WykazPociagow.WstOdjazd, "Odjazd poc."), (WykazPociagow.WstPrzejazd, "Przejazd poc. bez zatrzymania") })
        {
            var r = new StackPanel { Orientation = Orientation.Horizontal };
            r.Children.Add(new Border { Width = 80, Height = 14, Background = b });
            r.Children.Add(new TextBlock { Text = t, Margin = new Thickness(8, 0, 0, 0) });
            Grid.SetColumn(r, k2++); w.Children.Add(r);
        }
        sp.Children.Add(w);
        sp.Children.Add(new TextBlock { Text = "Oznaczenie typu postoju", Margin = new Thickness(0, 10, 0, 4) });
        var typy = new WrapPanel();
        foreach (var (k, t) in new[] { ("ph", "postój handlowy"), ("pm", "postój dla prac manewrowych"), ("zp", "zmiana pracownicza"), ("zc", "zmiana czoła pociągu"), ("pn", "postój niehandlowy"), ("zd", "zmiana drużyny"), ("pt", "postój techniczny") })
            typy.Children.Add(new TextBlock { Text = $"{k}   {t}", Width = 180, Margin = new Thickness(0, 0, 0, 2) });
        sp.Children.Add(typy);
        sp.Children.Add(new TextBlock { Text = "W symulatorze: wstęga, K / NK, „Rodz.” (potwierdzenie czasu przejazdu), numer pociągu (turkusowy – nieparzysty, zielony – parzysty), „+/-” (opóźnienie) i „Typ p.” (ph). Pozostałe oznaczenia wymagają danych, których rozkład nie ma.", Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
        var ok = new Button { Content = "OK", Width = 80, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0), IsDefault = true, IsCancel = true };
        ok.Click += (a, e) => Close();
        sp.Children.Add(ok);
        Content = sp;
    }
}

/// <summary>Okno „Szlaki posterunku” (podręcznik EDR, Rys. 35): wybór szlaku, z którego przyjeżdża / na który odjeżdża pociąg.</summary>
public class SzlakiPosterunku : Window
{
    public EdrDziennik Wybrany;
    public SzlakiPosterunku(List<EdrDziennik> dzienniki)
    {
        Title = "Szlaki posterunku"; Width = 620; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = EdrWindow.Tlo; FontFamily = new FontFamily("Segoe UI"); ShowInTaskbar = false;
        var sp = new StackPanel { Margin = new Thickness(10) };
        sp.Children.Add(new Border { Background = EdrWindow.Brak, Padding = new Thickness(4), Child = new TextBlock { Text = "Szlaki posterunku", FontWeight = FontWeights.Bold, FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center } });
        var lv = new ListView { MinHeight = 120, MaxHeight = 400, Margin = new Thickness(0, 4, 0, 4) };
        var gv = new GridView();
        gv.Columns.Add(new GridViewColumn { Header = "Do / Z posterunku", DisplayMemberBinding = new Binding("Sasiad"), Width = 200 });
        gv.Columns.Add(new GridViewColumn { Header = "Linia kolejowa", DisplayMemberBinding = new Binding("Linia"), Width = 110 });
        gv.Columns.Add(new GridViewColumn { Header = "Nazwa dziennika ruchu", DisplayMemberBinding = new Binding("Nazwa"), Width = 260 });
        lv.View = gv;
        lv.ItemsSource = dzienniki.Select(d => new Wiersz(d)).ToList();
        lv.MouseDoubleClick += (a, e) => { if (lv.SelectedItem is Wiersz w) { Wybrany = w.D; DialogResult = true; } };
        sp.Children.Add(lv);
        var bp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = new Button { Content = "Wybierz", Width = 90, Margin = new Thickness(4), IsDefault = true };
        ok.Click += (a, e) => { if (lv.SelectedItem is Wiersz w) { Wybrany = w.D; DialogResult = true; } };
        bp.Children.Add(ok);
        bp.Children.Add(new Button { Content = "Anuluj", Width = 90, Margin = new Thickness(4), IsCancel = true });
        sp.Children.Add(bp);
        Content = sp;
    }

    record Wiersz(EdrDziennik D)
    {
        public string Sasiad => D.Sasiad;
        public string Linia => D.Linia;
        public string Nazwa => D.Nazwa;
    }
}

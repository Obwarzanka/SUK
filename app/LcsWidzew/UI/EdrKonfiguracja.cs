using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using LcsWidzew.Sim;

namespace LcsWidzew.UI;

/// <summary>
/// Konfiguracja → „Posterunki ruchu i dzienniki…” (administrator, uwaga właściciela 2026-10-28): dodawanie i ustawianie posterunków ruchu,
/// ich dzienników ruchu (nazwa, posterunek sąsiedni, linia, rodzaj urządzeń, szlaki układu albo tory wpisane ręcznie) i zakładek –
/// dzienniki z tym samym numerem zakładki stoją obok siebie (najwyżej dwa). Bez konfiguracji – wszystko ze szlaków układu.
/// </summary>
public class OknoPosterunkow : Window
{
    readonly EdrWindow okno;
    readonly ObservableCollection<Post> posty = new();
    readonly ListBox lista = new() { Width = 260, Margin = new Thickness(0, 0, 10, 0), DisplayMemberPath = nameof(Post.Opis) };
    readonly TextBox kod = Pole(90), nazwa = Pole(320), tory = Pole(320);
    readonly DataGrid dzienniki = new()
    {
        AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, CanUserSortColumns = false, HeadersVisibility = DataGridHeadersVisibility.Column,
        FontSize = 12.5, RowHeight = 26, Background = Brushes.White, SelectionMode = DataGridSelectionMode.Single, MinHeight = 220
    };
    readonly TextBlock szlakiInfo = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, FontSize = 11.5, Margin = new Thickness(0, 6, 0, 0) },
        blad = new() { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) },
        zrodlo = new() { FontSize = 11.5, Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap };
    readonly StackPanel prawa = new();
    Post wybrany;
    bool wypelnianie;

    static TextBox Pole(double w) => new() { Width = w, Background = Brushes.White, Margin = new Thickness(0, 0, 12, 0), Padding = new Thickness(2, 1, 2, 1) };

    /// <summary>Posterunek w edycji; dzienniki jako wiersze tabeli (szlaki i tory – po przecinku).</summary>
    class Post
    {
        public string Kod = "", Nazwa = "", Tory = "";
        public ObservableCollection<Dz> Dzienniki = new();
        public string Opis => $"{Kod} – {Nazwa}";
    }

    public class Dz
    {
        public string Id { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public string Sasiad { get; set; } = "";
        public string Linia { get; set; } = "";
        public string Typ { get; set; } = "sbl";
        public string Szlaki { get; set; } = "";
        public string Tory { get; set; } = "";
        public int Zakladka { get; set; }
        public bool MaWpisy;
    }

    static List<string> Lista(string s) => (s ?? "").Split(',', ';').Select(x => x.Trim()).Where(x => x != "").Distinct().ToList();

    public OknoPosterunkow(EdrWindow okno)
    {
        this.okno = okno;
        Title = "Posterunki ruchu i dzienniki ruchu"; Width = 1180; Height = 680; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EdrWindow.Pedzel(0xE9, 0xE9, 0xE9); FontFamily = new FontFamily("Segoe UI"); FontSize = 12; ShowInTaskbar = false;
        var d = okno.Dane;
        var zWpisami = d?.Dzienniki.Where(x => x.Strony.Any(s => s.Wiersze.Any(w => w.Rodzaj != "tekst"))).Select(x => x.Id).ToHashSet() ?? new();
        foreach (var p in d?.PosterunkiUst ?? new())
        {
            var pp = new Post { Kod = p.Kod, Nazwa = p.Nazwa, Tory = string.Join(", ", p.ToryStacyjne ?? new()) };
            foreach (var x in p.Dzienniki)
                pp.Dzienniki.Add(new Dz { Id = x.Id, Nazwa = x.Nazwa, Sasiad = x.Sasiad, Linia = x.Linia, Typ = x.Typ, Szlaki = string.Join(", ", x.Szlaki), Tory = string.Join(", ", x.Tory), Zakladka = x.Zakladka, MaWpisy = zWpisami.Contains(x.Id) });
            posty.Add(pp);
        }

        var dock = new DockPanel { Margin = new Thickness(12) };
        var tyt = new Border { Background = EdrWindow.Brak, Padding = new Thickness(4), Margin = new Thickness(0, 0, 0, 8),
            Child = new TextBlock { Text = "Posterunki ruchu i dzienniki ruchu", FontWeight = FontWeights.Bold, FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center } };
        DockPanel.SetDock(tyt, Dock.Top); dock.Children.Add(tyt);
        zrodlo.Text = (d?.ZUkladu == true ? "Teraz: posterunki i dzienniki ze szlaków układu (bez konfiguracji). " : "Teraz: konfiguracja administratora. ") +
            (d?.BladKonfiguracji != null ? $"Uwaga: {d.BladKonfiguracji}. " : "") +
            (d?.Admin == true ? "" : "Zmiany zapisuje tylko administrator (rola „administrator” albo „instruktor”) – podgląd.");
        zrodlo.Foreground = d?.Admin == true && d.BladKonfiguracji == null ? Brushes.Black : Brushes.Firebrick;
        DockPanel.SetDock(zrodlo, Dock.Top); dock.Children.Add(zrodlo);

        // dół: przywróć z układu, zapisz, anuluj
        var dol = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        var zUkladu = new Button { Content = "Przywróć z układu", Padding = new Thickness(10, 2, 10, 2), IsEnabled = d?.Admin == true, ToolTip = "Posterunki i dzienniki ze szlaków układu (konfiguracja administratora zostanie usunięta)" };
        zUkladu.Click += (a, e) =>
        {
            if (MessageBox.Show(this, "Usunąć konfigurację i wrócić do posterunków i dzienników ze szlaków układu?\nDzienniki, których nie ma w układzie, znikną razem z wpisami.", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            var r = okno.Wyslij(new EdrPolecenie { Rodzaj = "posterunkizukladu" });
            if (r.Ok) Close(); else blad.Text = r.Msg;
        };
        DockPanel.SetDock(zUkladu, Dock.Left); dol.Children.Add(zUkladu);
        var bp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var zapisz = new Button { Content = "Zapisz", MinWidth = 100, Margin = new Thickness(4, 0, 4, 0), IsEnabled = d?.Admin == true };
        zapisz.Click += (a, e) => Zapisz();
        bp.Children.Add(zapisz);
        bp.Children.Add(new Button { Content = "Anuluj", Width = 100, Margin = new Thickness(4, 0, 0, 0), IsCancel = true });
        dol.Children.Add(bp);
        DockPanel.SetDock(dol, Dock.Bottom); dock.Children.Add(dol);
        DockPanel.SetDock(blad, Dock.Bottom); dock.Children.Add(blad);

        // lewa: lista posterunków
        var lewa = new DockPanel();
        var lp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 10, 0) };
        var dodajP = new Button { Content = "Dodaj posterunek", Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(0, 0, 4, 0) };
        dodajP.Click += (a, e) => { var p = new Post { Kod = "P" + (posty.Count + 1), Nazwa = "Nowy posterunek" }; posty.Add(p); lista.SelectedItem = p; };
        var usunP = new Button { Content = "Usuń", Padding = new Thickness(8, 1, 8, 1) };
        usunP.Click += (a, e) =>
        {
            if (wybrany == null) return;
            if (wybrany.Dzienniki.Any(x => x.MaWpisy) && MessageBox.Show(this, $"Posterunek {wybrany.Nazwa} ma dzienniki z wpisami – usunąć razem z nimi?", Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            posty.Remove(wybrany); lista.SelectedIndex = posty.Count > 0 ? 0 : -1;
        };
        lp.Children.Add(dodajP); lp.Children.Add(usunP);
        DockPanel.SetDock(lp, Dock.Bottom); lewa.Children.Add(lp);
        var lt = new TextBlock { Text = "Posterunki ruchu:", Margin = new Thickness(0, 0, 0, 2) }; DockPanel.SetDock(lt, Dock.Top); lewa.Children.Add(lt);
        lista.ItemsSource = posty;
        lewa.Children.Add(lista);
        DockPanel.SetDock(lewa, Dock.Left); dock.Children.Add(lewa);

        // prawa: dane posterunku i jego dzienniki
        var r1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        r1.Children.Add(new TextBlock { Text = "Kod:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) }); r1.Children.Add(kod);
        r1.Children.Add(new TextBlock { Text = "Nazwa:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) }); r1.Children.Add(nazwa);
        prawa.Children.Add(r1);
        var r2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        r2.Children.Add(new TextBlock { Text = "Tory stacyjne (po przecinku, puste – z układu):", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) }); r2.Children.Add(tory);
        prawa.Children.Add(r2);
        kod.TextChanged += (a, e) => { if (!wypelnianie && wybrany != null) { wybrany.Kod = kod.Text.Trim(); Odswiez(); } };
        nazwa.TextChanged += (a, e) => { if (!wypelnianie && wybrany != null) { wybrany.Nazwa = nazwa.Text.Trim(); Odswiez(); } };
        tory.TextChanged += (a, e) => { if (!wypelnianie && wybrany != null) wybrany.Tory = tory.Text; };

        prawa.Children.Add(new TextBlock { Text = "Dzienniki ruchu posterunku (zakładka – dzienniki z tym samym numerem obok siebie, najwyżej dwa; 0 – osobna zakładka):", Margin = new Thickness(0, 2, 0, 2) });
        void K(string h, string pole, double w, string opis = null)
        {
            var hs = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
            hs.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            if (opis != null) hs.Setters.Add(new Setter(ToolTipService.ToolTipProperty, opis));
            dzienniki.Columns.Add(new DataGridTextColumn { Header = h, Binding = new Binding(pole) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, Width = new DataGridLength(w, w < 5 ? DataGridLengthUnitType.Star : DataGridLengthUnitType.Pixel), HeaderStyle = hs });
        }
        K("Nazwa dziennika", nameof(Dz.Nazwa), 2);
        K("Posterunek sąsiedni", nameof(Dz.Sasiad), 1.5, "Nazwa na zakładce i w kolumnach „Z / W kierunku post.” wykazu");
        K("Linia", nameof(Dz.Linia), 60);
        dzienniki.Columns.Add(new DataGridComboBoxColumn
        {
            Header = "Urządzenia", SelectedItemBinding = new Binding(nameof(Dz.Typ)), ItemsSource = Edr.Typy, Width = 80
        });
        K("Szlaki układu", nameof(Dz.Szlaki), 1.3, "Id szlaków z układu po przecinku (lista niżej) – tory szlakowe i stan szlaku z układu");
        K("Tory (ręcznie)", nameof(Dz.Tory), 1, "Tory szlakowe dziennika bez szlaku w układzie, po przecinku");
        K("Zakładka", nameof(Dz.Zakladka), 70);
        prawa.Children.Add(dzienniki);
        var dp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        var dodajD = new Button { Content = "Dodaj dziennik", Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(0, 0, 4, 0) };
        dodajD.Click += (a, e) => { if (wybrany == null) return; var z = wybrany.Dzienniki.Count == 0 ? 1 : wybrany.Dzienniki.Max(x => x.Zakladka) + 1; var nd = new Dz { Nazwa = "Nowy dziennik", Zakladka = z }; wybrany.Dzienniki.Add(nd); dzienniki.SelectedItem = nd; };
        var usunD = new Button { Content = "Usuń dziennik", Padding = new Thickness(8, 1, 8, 1) };
        usunD.Click += (a, e) =>
        {
            if (wybrany == null || dzienniki.SelectedItem is not Dz x) return;
            if (x.MaWpisy && MessageBox.Show(this, $"Dziennik {x.Nazwa} ma wpisy – usunąć razem z nimi?", Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            wybrany.Dzienniki.Remove(x);
        };
        dp.Children.Add(dodajD); dp.Children.Add(usunD);
        prawa.Children.Add(dp);
        prawa.Children.Add(szlakiInfo);
        dock.Children.Add(new ScrollViewer { Content = prawa, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = dock;

        lista.SelectionChanged += (a, e) => Wybierz(lista.SelectedItem as Post);
        if (posty.Count > 0) lista.SelectedIndex = Math.Max(0, posty.ToList().FindIndex(x => x.Kod == okno.WykazPanel.Posterunek));
    }

    void Odswiez() { var s = lista.SelectedItem; lista.Items.Refresh(); lista.SelectedItem = s; Szlaki(); }

    void Wybierz(Post p)
    {
        dzienniki.CommitEdit(DataGridEditingUnit.Row, true);
        wybrany = p;
        wypelnianie = true;
        kod.Text = p?.Kod ?? ""; nazwa.Text = p?.Nazwa ?? ""; tory.Text = p?.Tory ?? "";
        wypelnianie = false;
        dzienniki.ItemsSource = p?.Dzienniki;
        prawa.IsEnabled = p != null;
        Szlaki();
    }

    /// <summary>Szlaki układu dochodzące do posterunku – do wpisania w kolumnie „Szlaki układu”.</summary>
    void Szlaki()
    {
        var sz = okno.Dane?.SzlakiUkladu.Where(x => wybrany != null && x.Posterunki.Contains(wybrany.Kod)).ToList() ?? new();
        szlakiInfo.Text = wybrany == null ? "" : sz.Count == 0 ? $"Do posterunku {wybrany.Kod} nie dochodzi żaden szlak układu – dzienniki z torami wpisanymi ręcznie."
            : $"Szlaki układu przy posterunku {wybrany.Kod}: " + string.Join("; ", sz.Select(x => $"{x.Id} (tor {(x.Tor != "" ? x.Tor : "–")}{(x.Linia != "" ? ", L. " + x.Linia : "")}, {string.Join("–", x.Posterunki)}{(x.Sasiad != "" ? ", " + x.Sasiad : "")})"));
    }

    void Zapisz()
    {
        dzienniki.CommitEdit(DataGridEditingUnit.Row, true);
        var l = posty.Select(p => new EdrPosterunekUst
        {
            Kod = p.Kod, Nazwa = p.Nazwa, ToryStacyjne = Lista(p.Tory),
            Dzienniki = p.Dzienniki.Select(x => new EdrDziennikUst { Id = x.Id, Nazwa = x.Nazwa, Sasiad = x.Sasiad, Linia = x.Linia, Typ = x.Typ ?? "sbl", Szlaki = Lista(x.Szlaki), Tory = Lista(x.Tory), Zakladka = x.Zakladka }).ToList()
        }).ToList();
        var r = okno.Wyslij(new EdrPolecenie { Rodzaj = "posterunki", Posterunki = l });
        if (r.Ok) Close(); else blad.Text = r.Msg;
    }

    /// <summary>Zrzut: wybór posterunku na liście.</summary>
    public void Wybierz(string kodPost) { var p = posty.FirstOrDefault(x => x.Kod == kodPost); if (p != null) lista.SelectedItem = p; }
}

/// <summary>Konfiguracja → „Czas bez wypadku…” (administrator): napis i początek liczenia czasu bez wypadku (pole nad zegarem).</summary>
public class OknoBezWypadku : OknoEdr
{
    public OknoBezWypadku(EdrWindow okno) : base("Czas bez wypadku", 520)
    {
        var d = okno.Dane;
        Sp.Children.Add(new TextBlock { Text = "Czas bez wypadku", FontWeight = FontWeights.Bold, FontSize = 14, Margin = new Thickness(0, 0, 0, 6) });
        Sp.Children.Add(Et("Napis nad zegarem:"));
        var tekst = new TextBox { Text = d?.BezWypadku?.Tekst ?? "", Background = Brushes.White };
        Sp.Children.Add(tekst);
        Sp.Children.Add(Et("Ostatni wypadek – początek liczenia (dd-MM-rrrr gg:mm; puste – początek symulacji):"));
        var r = new StackPanel { Orientation = Orientation.Horizontal };
        var od = new TextBox { Text = d?.BezWypadku?.Od ?? "", Width = 160, Background = Brushes.White };
        var teraz = new Button { Content = "Teraz", Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(8, 0, 8, 0), ToolTip = "Wypadek teraz – licznik od zera" };
        teraz.Click += (a, e) => od.Text = $"{okno.Dane?.Data} {okno.Teraz}";
        r.Children.Add(od); r.Children.Add(teraz);
        Sp.Children.Add(r);
        Sp.Children.Add(Et($"Teraz: {d?.BezWypadkuCzas}"));
        if (d?.Admin != true) Sp.Children.Add(new TextBlock { Text = "Zmiany zapisuje tylko administrator (rola „administrator” albo „instruktor”).", Foreground = Brushes.Firebrick, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap });
        Przyciski("Zapisz", () => okno.Wyslij(new EdrPolecenie { Rodzaj = "bezwypadku", Pole = tekst.Text, Wartosc = od.Text }));
    }
}

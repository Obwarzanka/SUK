using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LcsWidzew.Sim;

namespace LcsWidzew.UI;

/// <summary>
/// Okno telefonogramu (film ISDR: „Telefonogram do Dziennika Telefonicznego”, podręcznik EDR Rys. 38 – „Rejestracja telegramu w Dzienniku ruchu”)
/// i innych okoliczności. Z dziennika ruchu – także zmiana stanu bieżącego torów szlakowych (zamknięcie toru, ruch dwukierunkowy,
/// zapowiadanie telefoniczne): ze wzoru telefonogramu (E8e – Data/telefonogramy.json) albo, „[bez szablonu]”, ręcznie.
/// </summary>
public class OknoTelefonogramu : Window
{
    static readonly string[] Kierunki = { "bez zmian", "Nieustalony", "Z posterunku sąsiedniego", "DO posterunku sąsiedniego" };
    ComboBox wzorWybor;
    /// <summary>Wybór wzoru telefonogramu po numerze (zrzut „#edrtelokno=i|nr”).</summary>
    public void WybierzWzor(string nr) { foreach (ComboBoxItem x in wzorWybor.Items) if (x.Tag as string == nr) wzorWybor.SelectedItem = x; }

    public OknoTelefonogramu(EdrWindow okno, string post, EdrDziennik d, bool okolicznosc)
    {
        var dane = okno.Dane;
        string user = dane?.Uzytkownik ?? "", nazwaPost = dane?.Posterunki.GetValueOrDefault(post, post) ?? post;
        int nr = dane?.NastepnyNr.GetValueOrDefault(post, 1) ?? 1;
        var numeracja = Edr.NazwaNumeracji(dane?.Numeracja.GetValueOrDefault(post) ?? "dobowa");
        Title = okolicznosc ? "Inne okoliczności do Dziennika Telefonicznego" : d != null ? "Rejestracja telefonogramu w Dzienniku ruchu" : "Telefonogram do Dziennika Telefonicznego";
        Width = d != null && !okolicznosc ? 820 : 600; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EdrWindow.Pedzel(0xE9, 0xE9, 0xE9); FontFamily = new FontFamily("Segoe UI"); FontSize = 12; ShowInTaskbar = false;
        var sp = new StackPanel { Margin = new Thickness(12) };
        TextBlock Et(string t) => new() { Text = t, Foreground = Brushes.DimGray, FontSize = 11, Margin = new Thickness(0, 6, 0, 1) };
        TextBox Pole(string v = "", bool tylkoOdczyt = false) => new() { Text = v, IsReadOnly = tylkoOdczyt, Background = tylkoOdczyt ? EdrWindow.Pedzel(0xF2, 0xF2, 0xF2) : Brushes.White, Foreground = tylkoOdczyt ? Brushes.DimGray : Brushes.Black };
        TextBox Godz(string v)
        {
            var t = Pole(v); t.ToolTip = "Dwuklik – bieżąca godzina; kółko myszy – ± minuta (prawa połowa) / ± godzina (lewa połowa)";
            t.MouseDoubleClick += (a, e) => t.Text = okno.Teraz;
            t.MouseWheel += (a, e) => { e.Handled = true; bool h = e.GetPosition(t).X < t.ActualWidth / 2; int k = e.Delta > 0 ? 1 : -1; t.Text = EdrWindow.Przesun(t.Text, h ? k : 0, h ? 0 : k, okno.Teraz); };
            return t;
        }

        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) }); g.ColumnDefinitions.Add(new ColumnDefinition());
        var l = new StackPanel(); var p = new StackPanel(); Grid.SetColumn(p, 2); g.Children.Add(l); g.Children.Add(p);
        l.Children.Add(new TextBlock { Text = d?.Nazwa ?? "Dziennik telefoniczny", FontWeight = FontWeights.Bold, FontSize = 13 });
        l.Children.Add(Et($"Numer własny (numeracja {numeracja}):")); l.Children.Add(Pole(nr.ToString(), true));
        l.Children.Add(Et("Nadał:")); var nadal = Pole(user, true); l.Children.Add(nadal);
        l.Children.Add(Et("Data:")); var data = Pole(dane?.Data ?? ""); l.Children.Add(data);
        l.Children.Add(Et("Godzina nadania:")); var godz = Godz(okno.Teraz); l.Children.Add(godz);
        var rb = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
        var nadaje = new RadioButton { Content = "Nadaję", IsChecked = true, Margin = new Thickness(0, 0, 16, 0) }; var odbiera = new RadioButton { Content = "Odbieram" };
        rb.Children.Add(nadaje); rb.Children.Add(odbiera);
        if (!okolicznosc) p.Children.Add(rb); else p.Children.Add(new TextBlock { Text = " " });
        p.Children.Add(Et("Numer odbiorcy:")); var nrOdb = Pole(); p.Children.Add(nrOdb);
        p.Children.Add(Et("Odebrał:")); var odebral = Pole(); p.Children.Add(odebral);
        p.Children.Add(Et("Posterunek drugiej strony:")); var drugi = Pole(d?.Sasiad ?? ""); p.Children.Add(drugi);
        p.Children.Add(Et("Czas zakończenia:")); var zak = Godz(okno.Teraz); p.Children.Add(zak);
        if (okolicznosc) { nrOdb.IsEnabled = odebral.IsEnabled = drugi.IsEnabled = false; }
        void Kierunek()
        {
            bool n = nadaje.IsChecked == true;
            nadal.IsReadOnly = n; nadal.Background = n ? EdrWindow.Pedzel(0xF2, 0xF2, 0xF2) : Brushes.White; nadal.Foreground = n ? Brushes.DimGray : Brushes.Black;
            odebral.IsReadOnly = !n; odebral.Background = !n ? EdrWindow.Pedzel(0xF2, 0xF2, 0xF2) : Brushes.White; odebral.Foreground = !n ? Brushes.DimGray : Brushes.Black;
            if (n) { nadal.Text = user; if (odebral.Text == user) odebral.Text = ""; } else { odebral.Text = user; if (nadal.Text == user) nadal.Text = ""; }
        }
        nadaje.Checked += (a, e) => Kierunek(); odbiera.Checked += (a, e) => Kierunek();
        sp.Children.Add(g);

        sp.Children.Add(Et("Wzór telefonogramu:"));
        // wzory wg Ir-1 (E8e): treść powstaje z pól wzoru, zmiana stanu szlaku – ze skutku wzoru
        var wzor = new ComboBox { ToolTip = EdrWzory.Blad ?? "Wzór telefonogramu wg Ir-1 – treść z pól wzoru; „[bez szablonu]” – treść i zmiana stanu szlaku ręcznie" };
        wzor.Items.Add(new ComboBoxItem { Content = "[bez szablonu]", Tag = "" });
        foreach (var w in EdrWzory.Lista) wzor.Items.Add(new ComboBoxItem { Content = w.Tytul, Tag = w.Nr });
        wzor.SelectedIndex = 0; wzorWybor = wzor;
        var polaWz = new Grid { Margin = new Thickness(0, 2, 0, 0) };
        polaWz.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) }); polaWz.ColumnDefinitions.Add(new ColumnDefinition());
        var wartosciWz = new Dictionary<string, Func<string>>();
        if (!okolicznosc) { sp.Children.Add(wzor); sp.Children.Add(polaWz); }
        sp.Children.Add(Et(okolicznosc ? "Treść (inne okoliczności):" : "Adres i treść rozmowy lub telefonogramu:"));
        var tresc = new TextBox { Height = 90, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Background = EdrWindow.Pedzel(0xFB, 0xF4, 0xC8), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        sp.Children.Add(tresc);

        // zmiana stanu bieżącego szlaku – tylko telefonogram z dziennika ruchu
        var zmiany = new List<(string Szlak, ComboBox Status, ComboBox Rodzaj, ComboBox Kier, ComboBox Pod)>();
        GroupBox grupaZmian = null;
        var skutekWz = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = EdrWindow.Zegar, Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
        if (d != null && !okolicznosc)
        {
            var gz = new Grid { Margin = new Thickness(4) };
            foreach (var w in new[] { 120.0, 110, 125, 175, 0 }) gz.ColumnDefinitions.Add(new ColumnDefinition { Width = w > 0 ? new GridLength(w) : new GridLength(1, GridUnitType.Star) });
            int r = 0;
            void Kom(int c, UIElement e) { Grid.SetRow(e, r); Grid.SetColumn(e, c); gz.Children.Add(e); }
            gz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            foreach (var (c, t) in new[] { (0, "Tor szlakowy"), (1, "Status toru"), (2, "Rodzaj ruchu"), (3, "Zasadniczy kierunek"), (4, "Prowadzenie ruchu jak dla") }) Kom(c, new TextBlock { Text = t, FontSize = 10.5, Foreground = Brushes.DimGray });
            ComboBox Lista(IEnumerable<string> el) { var cb = new ComboBox { Margin = new Thickness(0, 1, 4, 1), FontSize = 11 }; cb.Items.Add("bez zmian"); foreach (var x in el) cb.Items.Add(x); cb.SelectedIndex = 0; return cb; }
            foreach (var t in d.Biezace.Tory)
            {
                r++; gz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Kom(0, new TextBlock { Text = $"{(t.Strona != "" ? t.Strona + " " : "")}{t.Tor} ({t.Status.ToLowerInvariant()})", VerticalAlignment = VerticalAlignment.Center, FontSize = 11 });
                var st = Lista(new[] { "Otwarty", "Zamknięty" }); var ro = Lista(new[] { "Jednokierunkowy", "Dwukierunkowy" });
                var ki = new ComboBox { Margin = new Thickness(0, 1, 4, 1), FontSize = 11 }; foreach (var x in Kierunki) ki.Items.Add(x); ki.SelectedIndex = 0;
                var po = Lista(Edr.Podstawy);
                Kom(1, st); Kom(2, ro); Kom(3, ki); Kom(4, po);
                zmiany.Add((t.Szlak, st, ro, ki, po));
            }
            sp.Children.Add(grupaZmian = new GroupBox { Header = "Zmiana stanu bieżącego szlaku (telefonogram bez szablonu – ręcznie; wzór – sam)", Content = gz, Margin = new Thickness(0, 8, 0, 0) });
        }
        if (!okolicznosc) sp.Children.Add(skutekWz);

        // pola wybranego wzoru i podgląd treści (od – posterunek nadający, do – odbierający, jak w rdzeniu)
        EdrWzor Wybrany() => EdrWzory.Wzor((wzor.SelectedItem as ComboBoxItem)?.Tag as string);
        Dictionary<string, string> Wartosci() => wartosciWz.ToDictionary(x => x.Key, x => x.Value());
        void Podglad()
        {
            var w = Wybrany();
            if (w == null) return;
            string tu = nazwaPost, tam = d?.Sasiad ?? drugi.Text;
            bool n = nadaje.IsChecked == true;
            tresc.Text = EdrWzory.Tresc(w, Wartosci(), n ? tu : tam, n ? tam : tu, null, podglad: true).Tresc;
        }
        void Buduj()
        {
            polaWz.Children.Clear(); polaWz.RowDefinitions.Clear(); wartosciWz.Clear();
            var w = Wybrany();
            tresc.IsReadOnly = w != null; tresc.Background = w != null ? EdrWindow.Pedzel(0xF2, 0xEE, 0xD8) : EdrWindow.Pedzel(0xFB, 0xF4, 0xC8);
            if (grupaZmian != null) grupaZmian.IsEnabled = w == null;
            skutekWz.Visibility = w != null ? Visibility.Visible : Visibility.Collapsed;
            skutekWz.Text = w == null ? "" : EdrWzory.OpisSkutku(w) + (d == null && w.Skutek != null ? " (tylko z dziennika ruchu – z dziennika telefonicznego stan szlaku się nie zmienia)" : "");
            if (w == null) { tresc.Text = ""; return; }
            int r = 0;
            foreach (var id in w.Pola)
            {
                var p = EdrWzory.Pola[id];
                polaWz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var et = new TextBlock { Text = p.Etykieta + ":", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 6, 2) };
                Grid.SetRow(et, r); polaWz.Children.Add(et);
                FrameworkElement el;
                if (p.Rodzaj == "wybor" || p.Rodzaj == "tor" && d != null)
                {
                    var cb = new ComboBox { Margin = new Thickness(0, 2, 0, 2) };
                    foreach (var o in p.Rodzaj == "tor" ? d.Biezace.Tory.Select(t => t.Tor) : p.Opcje) cb.Items.Add(o);
                    cb.SelectedIndex = cb.Items.Count > 0 ? 0 : -1;
                    cb.SelectionChanged += (a, e) => Podglad();
                    wartosciWz[id] = () => cb.SelectedItem as string ?? ""; el = cb;
                }
                else
                {
                    var tb = p.Rodzaj == "godzina" ? Godz(id == "godz" ? okno.Teraz : "") : Pole();
                    tb.Margin = new Thickness(0, 2, 0, 2);
                    tb.TextChanged += (a, e) => Podglad();
                    wartosciWz[id] = () => tb.Text; el = tb;
                }
                Grid.SetRow(el, r); Grid.SetColumn(el, 1); polaWz.Children.Add(el);
                r++;
            }
            Podglad();
        }
        wzor.SelectionChanged += (a, e) => Buduj();
        nadaje.Checked += (a, e) => Podglad(); odbiera.Checked += (a, e) => Podglad(); drugi.TextChanged += (a, e) => Podglad();

        sp.Children.Add(Et("Dodatkowa notatka:"));
        var notatka = new TextBox { Height = 50, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
        sp.Children.Add(notatka);
        var blad = new TextBlock { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
        sp.Children.Add(blad);
        var bp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        var zapisz = new Button { Content = "Zapisz", Width = 100, Margin = new Thickness(4) };
        zapisz.Click += (a, e) =>
        {
            bool n = nadaje.IsChecked == true;
            var tel = new EdrTelefonogram
            {
                Rodzaj = okolicznosc ? "okolicznosc" : "telefonogram", Nadaje = n, NrOdbiorcy = nrOdb.Text, Data = data.Text, Godz = godz.Text, CzasZak = zak.Text,
                Nadal = nadal.Text, Odebral = odebral.Text, NadalPost = n ? "" : drugi.Text, OdebralPost = n ? drugi.Text : "", Tresc = tresc.Text, Notatka = notatka.Text,
                Wzor = Wybrany()?.Nr ?? "[bez szablonu]", Pola = Wybrany() != null ? Wartosci() : new(),
                Zmiany = zmiany.Select(z => new EdrZmianaToru
                {
                    Szlak = z.Szlak, Status = z.Status.SelectedIndex > 0 ? z.Status.SelectedItem as string : null, Rodzaj = z.Rodzaj.SelectedIndex > 0 ? z.Rodzaj.SelectedItem as string : null,
                    Podstawa = z.Pod.SelectedIndex > 0 ? z.Pod.SelectedItem as string : null, Zasadniczy = z.Kier.SelectedIndex switch { 1 => 0, 2 => -1, 3 => 1, _ => null }
                }).Where(z => z.Status != null || z.Rodzaj != null || z.Podstawa != null || z.Zasadniczy != null).ToList()
            };
            var r = okno.Wyslij(new EdrPolecenie { Rodzaj = "telefonogram", Dziennik = d?.Id, Wartosc = post, Tel = tel });
            if (r.Ok) Close(); else blad.Text = r.Msg;
        };
        bp.Children.Add(zapisz);
        bp.Children.Add(new Button { Content = "Anuluj", Width = 100, Margin = new Thickness(4), IsCancel = true });
        sp.Children.Add(bp);
        Content = sp;
        Loaded += (a, e) => (okolicznosc ? (Control)tresc : odebral).Focus();
    }
}

/// <summary>Zakładka „Dziennik telefoniczny” (film ISDR): Nr kol., Data, Adres i treść, Czas zakończenia, Nadał / odebrał; strony = doby.</summary>
public class TelefonicznyWidok : DockPanel
{
    static readonly double[] Szer = { 60, 90, 0, 110, 300 };
    readonly EdrWindow okno;
    readonly ComboBox posterunek = new() { Width = 200, Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }, daty = new() { Width = 100, Margin = new Thickness(2, 0, 2, 0) };
    readonly StackPanel wiersze = new();
    readonly ScrollViewer sv;
    string post, podpis;
    int doba = -1;

    public TelefonicznyWidok(EdrWindow okno)
    {
        this.okno = okno;
        Background = EdrWindow.Tlo;
        var nag = new DockPanel { Margin = new Thickness(4, 4, 4, 4) };
        var przyc = new StackPanel { Orientation = Orientation.Horizontal };
        Button B(string t, Action a) { var b = new Button { Content = t, Padding = new Thickness(14, 3, 14, 3), Margin = new Thickness(4, 0, 0, 0), Background = new LinearGradientBrush(Color.FromRgb(0xF4, 0xF4, 0xF4), Color.FromRgb(0xD2, 0xD2, 0xD2), 90) }; b.Click += (s, e) => a(); return b; }
        przyc.Children.Add(B("Rejestruj telefonogram", () => { if (post != null) okno.Telefonogram(post, null, false); }));
        przyc.Children.Add(B("Rejestruj inne okoliczności", () => { if (post != null) okno.Telefonogram(post, null, true); }));
        DockPanel.SetDock(przyc, Dock.Right); nag.Children.Add(przyc);
        nag.Children.Add(new TextBlock { Text = "Dziennik telefoniczny", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = EdrWindow.Pedzel(0x33, 0x33, 0x33), Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        nag.Children.Add(posterunek);
        posterunek.SelectionChanged += (a, e) => { if (posterunek.SelectedValue is string p && p != post) { post = p; doba = -1; podpis = null; okno.Odswiez(true); } };
        SetDock(nag, Dock.Top); Children.Add(nag);

        var kol = new Grid { Background = new LinearGradientBrush(Color.FromRgb(0x2C, 0x2C, 0x2C), Color.FromRgb(0x5A, 0x5A, 0x5A), 90) }; Kolumny(kol);
        int c = 0;
        foreach (var t in new[] { "Nr kol.", "Data", "Adres i treść", "Czas zakończenia", "Nadał / odebrał" })
        {
            var b = new Border { BorderBrush = EdrWindow.Pedzel(0x80, 0x80, 0x80), BorderThickness = new Thickness(0, 0, 1, 0), Child = new TextBlock { Text = t, Foreground = Brushes.White, FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4, 3, 4, 3) } };
            Grid.SetColumn(b, c++); kol.Children.Add(b);
        }
        SetDock(kol, Dock.Top); Children.Add(kol);

        var stopka = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
        Button N(string t, Func<int> d) { var b = new Button { Content = t, MinWidth = 26, Margin = new Thickness(1, 0, 1, 0) }; b.Click += (s, e) => { doba = d(); podpis = null; okno.Odswiez(true); }; return b; }
        stopka.Children.Add(N("|<", () => Doby.FirstOrDefault()));
        stopka.Children.Add(N("<", () => Doby.LastOrDefault(x => x < Biezaca, Biezaca)));
        stopka.Children.Add(daty);
        stopka.Children.Add(N(">", () => Doby.FirstOrDefault(x => x > Biezaca, Biezaca)));
        stopka.Children.Add(N(">|", () => -1));
        daty.SelectionChanged += (a, e) => { if (daty.SelectedItem is KeyValuePair<int, string> kv && kv.Key != Biezaca) { doba = kv.Key; podpis = null; okno.Odswiez(true); } };
        SetDock(stopka, Dock.Bottom); Children.Add(stopka);
        sv = new ScrollViewer { Content = wiersze, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = EdrWindow.PoleDziennika };
        Children.Add(sv);
    }

    List<int> doby = new();
    int dzis;
    List<int> Doby => doby;
    int Biezaca => doba < 0 ? dzis : doba;

    static void Kolumny(Grid g)
    {
        foreach (var w in Szer) g.ColumnDefinitions.Add(new ColumnDefinition { Width = w > 0 ? new GridLength(w) : new GridLength(1, GridUnitType.Star) });
    }

    /// <summary>Dziennik telefoniczny posterunku wybranego u góry okna EDR.</summary>
    public void Pokaz(EdrData d, string wybrany)
    {
        if (wybrany != null && wybrany != post) { post = wybrany; doba = -1; podpis = null; }
        posterunek.Visibility = Visibility.Collapsed;
        var l = post != null && d.Telefoniczny.TryGetValue(post, out var t) ? t : new();
        dzis = d.Doba;
        doby = l.Select(x => x.Doba).Append(d.Doba).Distinct().OrderBy(x => x).ToList();
        var nowy = $"{post}|{Biezaca}|{l.Count}";
        if (nowy == podpis) return;
        podpis = nowy;
        daty.ItemsSource = doby.Select(x => new KeyValuePair<int, string>(x, DataDoby(d, x))).ToList(); daty.DisplayMemberPath = "Value";
        daty.SelectedIndex = doby.IndexOf(Biezaca);
        wiersze.Children.Clear();
        foreach (var x in l.Where(x => x.Doba == Biezaca)) wiersze.Children.Add(Wiersz(x));
        sv.ScrollToBottom();
    }

    static string DataDoby(EdrData d, int doba) => DateTime.TryParseExact(d.Data, "dd-MM-yyyy", null, System.Globalization.DateTimeStyles.None, out var dz) ? dz.AddDays(doba - d.Doba).ToString("dd-MM-yyyy") : doba.ToString();

    static UIElement Wiersz(EdrTelefonogram t)
    {
        var g = new Grid { Background = Brushes.White }; Kolumny(g);
        void K(int c, UIElement e) { var b = new Border { BorderBrush = EdrWindow.Linia, BorderThickness = new Thickness(0, 0, 1, 1), Child = e, Padding = new Thickness(4, 2, 4, 2) }; Grid.SetColumn(b, c); g.Children.Add(b); }
        K(0, new TextBlock { Text = t.Nr.ToString(), HorizontalAlignment = HorizontalAlignment.Center });
        K(1, new TextBlock { Text = t.Data });
        var tr = new StackPanel();
        if (t.Rodzaj == "okolicznosc") tr.Children.Add(new TextBlock { Text = "Inne okoliczności:", FontStyle = FontStyles.Italic, Foreground = Brushes.DimGray, FontSize = 11 });
        else tr.Children.Add(new TextBlock { Text = $"{(t.Nadaje ? "Nadany" : "Odebrany")} {t.Godz}{(t.NrOdbiorcy != "" ? $", nr odbiorcy {t.NrOdbiorcy}" : "")}", Foreground = Brushes.DimGray, FontSize = 11 });
        tr.Children.Add(new TextBlock { Text = t.Tresc, TextWrapping = TextWrapping.Wrap, FontWeight = t.Rodzaj == "telefonogram" ? FontWeights.SemiBold : FontWeights.Normal });
        if (t.Notatka != "") tr.Children.Add(new TextBlock { Text = t.Notatka, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, FontSize = 11 });
        K(2, tr);
        K(3, new TextBlock { Text = t.CzasZak, HorizontalAlignment = HorizontalAlignment.Center });
        K(4, new TextBlock { Text = Edr.Podpis(t), TextWrapping = TextWrapping.Wrap });
        return g;
    }
}

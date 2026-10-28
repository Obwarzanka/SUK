using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LcsWidzew.Sim;

namespace LcsWidzew.UI;

/// <summary>
/// Okno EDR – elektroniczny dziennik ruchu (PLAN_EDR.md, wygląd wg Zrodla/EDR_notatki.md i ISDR_zdjecia.md § 5).
/// U góry wykaz pociągów (etap E2), na dole zakładki z dziennikami ruchu szlaków (po dwa obok siebie).
/// Godziny: dwuklik – bieżąca godzina, kółko myszy – ± minuta (prawa połowa pola) albo ± godzina (lewa połowa).
/// </summary>
public class EdrWindow : Window
{
    // kolory wg filmu ze stanowiska ISDR: szare tło okna, ciemne nagłówki dziennika, pomarańczowy wiersz niekompletny, granatowy zegar
    internal static readonly Brush Tlo = Pedzel(0xE6, 0xE6, 0xE6), Naglowek = Pedzel(0x3A, 0x3A, 0x3A), Linia = Pedzel(0xC4, 0xC4, 0xC4),
        Niekompletny = Pedzel(0xF6, 0xC5, 0x90), Brak = Pedzel(151, 186, 205), Kompletny = Brushes.White, Tekstowy = Pedzel(0xF1, 0xF1, 0xF1),
        Wykaz = Pedzel(0xEE, 0xEE, 0xD2), Zegar = Pedzel(0x22, 0x41, 0x8C), PoleDziennika = Pedzel(0xEB, 0xEB, 0xEB);
    internal static Brush Pedzel(byte r, byte g, byte b) { var x = new SolidColorBrush(Color.FromRgb(r, g, b)); x.Freeze(); return x; }

    readonly ILcsSession ses;
    readonly TabControl zakladki = new() { Background = Tlo, Padding = new Thickness(2), FontSize = 11 };
    readonly TextBlock zegar = new() { FontSize = 46, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(0, -8, 0, -6), HorizontalAlignment = HorizontalAlignment.Center },
        data = new() { FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Pedzel(0xF2, 0xE2, 0x4A) },
        bezWypTekst = new() { FontSize = 13, FontStyle = FontStyles.Italic, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center },
        bezWypCzas = new() { FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center },
        uzytkownik = new() { Margin = new Thickness(6, 0, 24, 0), FontSize = 11, VerticalAlignment = VerticalAlignment.Center }, komunikat = new() { Foreground = Brushes.Firebrick, Margin = new Thickness(12, 0, 0, 0), FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
    readonly MenuItem konfiguracja = new() { Header = "Konfiguracja" };
    readonly List<DziennikWidok> widoki = new();
    readonly WykazPociagow wykazPanel;
    /// <summary>Zakładki nad wykazem: „Opis pociągu”, „Trasa pociągu”, „Przewoźnik” – zaznaczony pociąg (szkielet, uwaga właściciela 2026-10-28).</summary>
    readonly StackPanel przewoznik = new() { Margin = new Thickness(12) };
    /// <summary>„Opis pociągu” w układzie EDR.exe (E8b): pola po lewej, uwagi po prawej, pola wyboru TN / TWR / nadzwyczajne.</summary>
    readonly Grid opisPociagu = new() { Margin = new Thickness(8, 5, 8, 5), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    readonly DataGrid trasaPociagu = new() { AutoGenerateColumns = false, IsReadOnly = true, HeadersVisibility = DataGridHeadersVisibility.Column, FontSize = 13, RowHeight = 24, Margin = new Thickness(6), Background = Pedzel(0xEE, 0xEE, 0xD2) };
    TabControl zakladkiPociagu;
    TelefonicznyWidok telefoniczny;
    /// <summary>Przyciski sprawdzeń cyklicznych (do czterech) obok zegara: zielony – w terminie, żółty – 15 min przed terminem, czerwony – po terminie.</summary>
    readonly StackPanel sprawdzenia = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 6, 0) };
    string sprawdzeniaPodpis;
    /// <summary>Kontrolka zajętości torów stacyjnych pod dziennikami (z wpisów w dziennikach).</summary>
    readonly StackPanel kontrolka = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 1) };
    string kontrolkaPodpis;
    internal static readonly Brush TorWolny = Pedzel(0x31, 0xB4, 0x4B), TorZajety = Pedzel(0xE5, 0x37, 0x2F);
    internal WykazPociagow WykazPanel => wykazPanel;
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    long wersja = -1;
    string uklad;
    internal EdrData Dane;
    /// <summary>Ostatni komunikat na stopce (zrzuty i testy).</summary>
    public string Komunikat => komunikat.Text;

    public EdrWindow(ILcsSession ses)
    {
        this.ses = ses;
        Title = "EDR – Elektroniczny Dziennik Ruchu"; Width = 1500; Height = 900; Background = Tlo;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 12; WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var g = new Grid { Background = Tlo };
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.25, GridUnitType.Star) });
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // pasek: menu i zegar (granatowe pole z białą godziną, data nad nią)
        var gora = new DockPanel { Margin = new Thickness(4, 2, 4, 2) };
        var bw = new Border { Background = Pedzel(0x1E, 0x7E, 0x22), Padding = new Thickness(10, 2, 10, 2), Child = new StackPanel { Children = { bezWypTekst, bezWypCzas } },
            ToolTip = "Czas bez wypadku – początek liczenia ustawia administrator (Konfiguracja)" };
        var zg = new Border { Background = Zegar, Padding = new Thickness(6, 1, 10, 3), Child = new StackPanel { Children = { data, zegar } } };
        var zegarPole = new StackPanel { MinWidth = 330, Children = { bw, zg } };
        // zegar i sprawdzenia w prawym górnym rogu, obok paska nad wykazem (jak na zrzucie EDR) – pasek wykazu zostawia im miejsce
        var rog = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 4, 0), Children = { sprawdzenia, zegarPole } };
        Grid.SetRowSpan(rog, 2); Panel.SetZIndex(rog, 1);
        rog.SizeChanged += (a, e) => wykazPanel.MiejsceNaZegar(rog.ActualWidth + 8);
        var menu = new Menu { Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Top, FontSize = 12 };
        foreach (var m in new[] { "Plik" }) menu.Items.Add(new MenuItem { Header = m, IsEnabled = false });
        // Raporty (podręcznik EDR rozdz. 8): wyciągi
        var raporty = new MenuItem { Header = "Raporty" };
        foreach (var (t, r) in new[] { ("Wyciąg z dziennika ruchu…", "ruchu"), ("Wyciąg z dziennika telefonicznego…", "telefoniczny"), ("Wyciąg z zajętości torów stacyjnych…", "tory") })
        { var mi = new MenuItem { Header = t }; var rr = r; mi.Click += (a, e) => WyciagOkno(rr).Show(); raporty.Items.Add(mi); }
        menu.Items.Add(raporty);
        menu.Items.Add(new MenuItem { Header = "Dyżury", IsEnabled = false });
        konfiguracja.SubmenuOpened += (a, e) => MenuKonfiguracji();
        konfiguracja.Items.Add(new MenuItem { Header = "(brak dzienników)", IsEnabled = false });
        menu.Items.Add(konfiguracja);
        foreach (var m in new[] { "Widok", "Okna", "Pomoc" }) menu.Items.Add(new MenuItem { Header = m, IsEnabled = false });
        gora.Children.Add(menu);
        g.Children.Add(gora);

        // wykaz pociągów posterunku (etap E2)
        var wykaz = new TabControl { Background = Tlo, FontSize = 11 };
        wykaz.Items.Add(new TabItem { Header = "Wykaz pociągów", Content = wykazPanel = new WykazPociagow(this) });
        wykaz.Items.Add(new TabItem { Header = "Opis pociągu", Content = new ScrollViewer { Content = opisPociagu, Background = Brushes.White, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
        wykaz.Items.Add(new TabItem { Header = "Trasa pociągu", Content = trasaPociagu });
        wykaz.Items.Add(new TabItem { Header = "Przewoźnik", Content = przewoznik });
        zakladkiPociagu = wykaz;
        KolumnyTrasy();
        wykazPanel.Wybrano += OpisPociagu;
        Grid.SetRow(wykaz, 1); g.Children.Add(wykaz);
        var spl = new GridSplitter { HorizontalAlignment = HorizontalAlignment.Stretch, Background = Linia }; Grid.SetRow(spl, 2); g.Children.Add(spl);
        Grid.SetRow(zakladki, 3); g.Children.Add(zakladki);
        g.Children.Add(rog);

        // pasek dolny jak na filmie: użytkownik z lewej, kontrolka torów stacyjnych na środku, tryb z prawej
        var stopka = new Grid { Margin = new Thickness(4, 3, 8, 3) };
        stopka.ColumnDefinitions.Add(new ColumnDefinition()); stopka.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); stopka.ColumnDefinitions.Add(new ColumnDefinition());
        // komunikat pod danymi użytkownika, przycinany, żeby nie zachodził na kontrolkę torów
        uzytkownik.TextTrimming = komunikat.TextTrimming = TextTrimming.CharacterEllipsis; komunikat.Margin = new Thickness(6, 1, 8, 0);
        stopka.Children.Add(new StackPanel { Children = { uzytkownik, komunikat } });
        Grid.SetColumn(kontrolka, 1); stopka.Children.Add(kontrolka);
        var tryb = new TextBlock { Text = "TRYB OPERATORA", FontWeight = FontWeights.Bold, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
        Grid.SetColumn(tryb, 2); stopka.Children.Add(tryb);
        Grid.SetRow(stopka, 5); g.Children.Add(stopka);
        Content = g;

        timer.Tick += (a, b) => Odswiez(false);
        Loaded += (a, b) => { Odswiez(true); timer.Start(); };
        Closed += (a, b) => timer.Stop();
    }

    /// <summary>Pobiera stan EDR i przebudowuje dzienniki, gdy coś się zmieniło (albo <paramref name="sila"/>).</summary>
    public void Odswiez(bool sila)
    {
        var d = ses.GetEdr();
        if (d == null) { komunikat.Text = "Brak danych EDR z serwera"; return; }
        if (d.BladDziennikow != null && Dane?.BladDziennikow == null) { komunikat.Text = "EDR: " + d.BladDziennikow; komunikat.Foreground = Brushes.Firebrick; }
        Dane = d;
        zegar.Text = d.Zegar; data.Text = d.Data;
        bezWypTekst.Text = d.BezWypadku?.Tekst ?? ""; bezWypCzas.Text = d.BezWypadkuCzas ?? "";
        uzytkownik.Text = $"Użytkownik: {d.Uzytkownik ?? "–"}" + (d.ZalogowanyOd != null ? $"      Zalogowany od: {d.ZalogowanyOd}" : "");
        // posterunek wybrany u góry (lista nad wykazem) wyznacza wszystko: wykaz, dzienniki ruchu, dziennik telefoniczny, kontrolkę torów
        wykazPanel.Pokaz(d, sila);
        var post = wykazPanel.Posterunek;
        Title = "EDR – Elektroniczny Dziennik Ruchu" + (post != null ? " – " + d.Posterunki.GetValueOrDefault(post, post) : "");
        var u = post + "#" + string.Join("|", d.Dzienniki.Where(x => post == null || x.Post == post).Select(x => $"{x.Id}:{x.Zakladka}:{x.Nazwa}:{x.Sasiad}"));
        if (u != uklad) { uklad = u; Uklad(); sila = true; }
        telefoniczny?.Pokaz(d, post);
        Sprawdzenia(d);
        Kontrolka(d);
        if (!sila && d.Wersja == wersja) return;
        if (widoki.Any(w => w.Edycja)) return;
        wersja = d.Wersja;
        foreach (var w in widoki) w.Pokaz(d.Dzienniki.First(x => x.Id == w.Id));
    }

    /// <summary>Kwadraty z numerami torów stacyjnych: zielony – wolny, czerwony – zajęty (wg wpisów w dziennikach).</summary>
    void Kontrolka(EdrData d)
    {
        var podpis = wykazPanel.Posterunek + ";" + string.Join(";", d.ZajetoscTorow.Select(x => x.Key + ":" + string.Join(",", x.Value.Select(t => t.Tor + "=" + t.Pociag))));
        if (podpis == kontrolkaPodpis) return;
        kontrolkaPodpis = podpis;
        kontrolka.Children.Clear();
        var posty = d.ZajetoscTorow.Where(x => x.Value.Count > 0 && (wykazPanel.Posterunek == null || x.Key == wykazPanel.Posterunek)).ToList();
        foreach (var (post, tory) in posty)
        {
            if (posty.Count > 1) kontrolka.Children.Add(new TextBlock { Text = d.Posterunki.GetValueOrDefault(post, post) + ":", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 4, 0), Foreground = Brushes.DimGray });
            foreach (var t in tory)
                kontrolka.Children.Add(new Border
                {
                    Background = t.Zajety ? TorZajety : TorWolny, BorderBrush = Pedzel(0x55, 0x55, 0x55), BorderThickness = new Thickness(0.5), MinWidth = 30, Height = 22,
                    Child = new TextBlock { Text = t.Tor, Foreground = Brushes.White, FontSize = 10.5, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 3, 0) },
                    ToolTip = t.Zajety ? $"Tor {t.Tor} zajęty – pociąg {t.Pociag}, przyjechał {t.Od} (wg dziennika ruchu)" : $"Tor {t.Tor} wolny (wg dziennika ruchu)"
                });
        }
    }

    void Sprawdzenia(EdrData d)
    {
        var podpis = string.Join("|", d.Sprawdzenia.Select(x => $"{x.Id}{x.Stan}{x.Ostatnie}{x.Nastepne}"));
        if (podpis == sprawdzeniaPodpis) return;
        sprawdzeniaPodpis = podpis;
        sprawdzenia.Children.Clear();
        foreach (var x in d.Sprawdzenia.Take(4))
        {
            var kolor = x.Stan == "po_terminie" ? TorZajety : x.Stan == "wkrotce" ? Pedzel(0xF2, 0xD2, 0x1B) : TorWolny;
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock { Text = "\uE9D5", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 20, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            sp.Children.Add(new TextBlock { Text = (x.Ostatnie != "" ? x.Ostatnie : "–") + "\n(" + x.Nastepne + ")", FontSize = 10.5, VerticalAlignment = VerticalAlignment.Center });
            var b = new Button
            {
                Content = sp, Background = kolor, Foreground = x.Stan == "wkrotce" ? Brushes.Black : Brushes.White, Margin = new Thickness(2, 0, 2, 0), Padding = new Thickness(8, 2, 8, 2), MinHeight = 46,
                ToolTip = $"{x.Nazwa} (posterunek {d.Posterunki.GetValueOrDefault(x.Post, x.Post)}) – co {OknoSprawdzenia.Czas(x.Okres)}\nostatnio: {(x.Ostatnie != "" ? $"{x.Ostatnie} ({x.Kto})" : "nie wykonano")}, termin: {x.Nastepne}" + (x.Stan == "po_terminie" ? " – PO TERMINIE" : "") + "\nKlik – wykonanie sprawdzenia"
            };
            var xx = x; b.Click += (a, e) => new OknoSprawdzenia(this, xx) { Owner = this }.ShowDialog();
            sprawdzenia.Children.Add(b);
        }
    }

    /// <summary>Okno wyciągu: „ruchu”, „telefoniczny” albo „tory”.</summary>
    public WyciagWindow WyciagOkno(string rodzaj) => new(this, rodzaj) { Owner = this };

    /// <summary>Powiadomienie dróżników przejazdowych (przycisk nad dziennikiem) i wpis o dowolnej treści.</summary>
    internal void PowiadomDroznikow(EdrDziennik d) => new OknoDroznikow(this, d) { Owner = this }.ShowDialog();
    internal void WpisDowolny(EdrDziennik d) => new OknoWpisu(this, d) { Owner = this }.ShowDialog();
    /// <summary>Zrzuty: okna E5.</summary>
    public Window DroznicyOkno(EdrDziennik d) => new OknoDroznikow(this, d) { Owner = this };
    public Window SprawdzeniaUstOkno(string post) => new OknoSprawdzenUst(this, post, Dane?.Posterunki.GetValueOrDefault(post, post) ?? post, Dane?.SprawdzeniaUst.GetValueOrDefault(post) ?? new()) { Owner = this };

    /// <summary>Okno telefonogramu (z dziennika ruchu <paramref name="d"/> albo z dziennika telefonicznego posterunku) albo innych okoliczności.</summary>
    internal void Telefonogram(string post, EdrDziennik d, bool okolicznosc) => TelefonogramOkno(post, d, okolicznosc).ShowDialog();
    public Window TelefonogramOkno(string post, EdrDziennik d, bool okolicznosc) => new OknoTelefonogramu(this, post, d, okolicznosc) { Owner = this };
    /// <summary>Zrzut i testy: zakładka dziennika telefonicznego.</summary>
    public void PokazTelefoniczny() => zakladki.SelectedIndex = zakladki.Items.Count - 1;

    /// <summary>Menu Konfiguracja: ustawienia zasadnicze każdego dziennika stanowiska.</summary>
    void MenuKonfiguracji()
    {
        konfiguracja.Items.Clear();
        // administrator: posterunki ruchu i ich dzienniki (zakładki), czas bez wypadku
        var mp = new MenuItem { Header = "Posterunki ruchu i dzienniki…", IsEnabled = Dane != null };
        mp.Click += (a, e) => PosterunkiOkno().ShowDialog();
        var mb = new MenuItem { Header = "Czas bez wypadku…", IsEnabled = Dane != null };
        mb.Click += (a, e) => BezWypadkuOkno().ShowDialog();
        if (Dane?.Admin != true) foreach (var m in new[] { mp, mb }) { m.Header += " (administrator)"; m.ToolTip = "Zmiany zapisuje tylko administrator (rola „administrator” albo „instruktor”)"; }
        konfiguracja.Items.Add(mp); konfiguracja.Items.Add(mb); konfiguracja.Items.Add(new Separator());
        foreach (var d in Dane?.Dzienniki ?? new())
        {
            var m = new MenuItem { Header = $"Ustawienia zasadnicze dziennika: {d.Nazwa}" + (Dane.Posterunki.Count > 1 ? $" ({Dane.Posterunki.GetValueOrDefault(d.Post, d.Post)})" : "") };
            var dd = d; m.Click += (a, e) => Ustawienia(dd, false);
            konfiguracja.Items.Add(m);
        }
        if (konfiguracja.Items.Count == 0) konfiguracja.Items.Add(new MenuItem { Header = "(brak dzienników)", IsEnabled = false });
        // numeracja telefonogramów posterunku: dobowa, miesięczna albo roczna
        if (Dane?.Numeracja.Count > 0) konfiguracja.Items.Add(new Separator());
        foreach (var (post, num) in Dane?.Numeracja ?? new())
        {
            var m = new MenuItem { Header = $"Numeracja telefonogramów – {Dane.Posterunki.GetValueOrDefault(post, post)}" };
            foreach (var n in Edr.Numeracje)
            {
                var pn = new MenuItem { Header = Edr.NazwaNumeracji(n), IsCheckable = true, IsChecked = n == num };
                var (pp, nn) = (post, n); pn.Click += (a, e) => Wyslij(new EdrPolecenie { Rodzaj = "numeracja", Wartosc = pp, Pole = nn });
                m.Items.Add(pn);
            }
            konfiguracja.Items.Add(m);
        }
        // wymagane sprawdzenia cykliczne posterunków
        foreach (var post in Dane?.SprawdzeniaUst.Keys ?? Enumerable.Empty<string>())
        {
            var m = new MenuItem { Header = $"Wymagane sprawdzenia – {Dane.Posterunki.GetValueOrDefault(post, post)}…" };
            var pp = post; m.Click += (a, e) => SprawdzeniaUstOkno(pp).ShowDialog();
            konfiguracja.Items.Add(m);
        }
    }

    /// <summary>Okna konfiguracji administratora: posterunki ruchu i dzienniki, czas bez wypadku.</summary>
    public Window PosterunkiOkno() => new OknoPosterunkow(this) { Owner = this };
    public Window BezWypadkuOkno() => new OknoBezWypadku(this) { Owner = this };

    /// <summary>Zakładki „Opis pociągu”, „Trasa pociągu”, „Przewoźnik” – zaznaczony pociąg.</summary>
    void OpisPociagu(EdrPociag p)
    {
        opisPociagu.Children.Clear(); opisPociagu.RowDefinitions.Clear(); opisPociagu.ColumnDefinitions.Clear(); przewoznik.Children.Clear();
        void W(Panel pn, string l, string v)
        {
            var r = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            r.Children.Add(new TextBlock { Text = l, Width = 200, FontWeight = FontWeights.SemiBold });
            r.Children.Add(new TextBlock { Text = string.IsNullOrEmpty(v) ? "–" : v, TextWrapping = TextWrapping.Wrap, MaxWidth = 900 });
            pn.Children.Add(r);
        }
        if (p == null) { opisPociagu.Children.Add(new TextBlock { Text = "Zaznacz pociąg w wykazie.", Foreground = Brushes.DimGray }); trasaPociagu.ItemsSource = null; return; }
        OpisJakEdr(p);
        trasaPociagu.ItemsSource = p.Trasa;
        W(przewoznik, "Przewoźnik:", p.Przewoznik);
        W(przewoznik, "Uwagi przewoźnika:", p.UwagiPrzew);
        przewoznik.Children.Add(new TextBlock { Text = "Dane przewoźnika (kontakt, skład pociągu) – w kolejnych etapach.", Foreground = Brushes.DimGray, Margin = new Thickness(0, 8, 0, 0) });
    }

    // kolory i czcionka opisu wg EDR.exe: granatowe pogrubione etykiety, kremowe pola tylko do odczytu, Tahoma 9 pt
    static readonly Brush OpisEtykieta = Pedzel(0, 0, 128), OpisPole = Pedzel(255, 255, 225), OpisRezygnacja = Pedzel(242, 212, 181), OpisRamka = Pedzel(0x7A, 0x7A, 0x7A);
    static readonly FontFamily OpisCzcionka = new("Tahoma");

    /// <summary>
    /// „Opis pociągu” w układzie EDR.exe (E8b): po lewej pola opisu (numer, nazwa, rodzaj, przewoźnik, typ, stacje, rozkład, wniosek, rezygnacja),
    /// po prawej pola wielowierszowe (informacje dodatkowe, ładunek, uwagi) i pola wyboru. Pola, których rozkład nie ma, zostają puste. Tylko do odczytu.
    /// </summary>
    void OpisJakEdr(EdrPociag p)
    {
        var g = opisPociagu;
        foreach (var w in new[] { 174.0, 250, 24, 520 }) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });
        TextBlock Etykieta(string t) => new() { Text = t, FontFamily = OpisCzcionka, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = OpisEtykieta, VerticalAlignment = VerticalAlignment.Center };
        TextBox Pole(string v, double h, double szer = double.NaN, Brush tlo = null) => new()
        {
            Text = v ?? "", IsReadOnly = true, Height = h, Width = szer, HorizontalAlignment = double.IsNaN(szer) ? HorizontalAlignment.Stretch : HorizontalAlignment.Left,
            FontFamily = OpisCzcionka, FontSize = 12, Background = tlo ?? OpisPole, BorderBrush = OpisRamka, BorderThickness = new Thickness(1), Padding = new Thickness(1, 0, 1, 0),
            TextWrapping = h > 22 ? TextWrapping.Wrap : TextWrapping.NoWrap, VerticalContentAlignment = h > 22 ? VerticalAlignment.Top : VerticalAlignment.Center
        };
        void Wstaw(FrameworkElement e, int r, int c)
        {
            while (g.RowDefinitions.Count <= r) g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            e.Margin = new Thickness(0, 1, 4, 2);
            Grid.SetRow(e, r); Grid.SetColumn(e, c); g.Children.Add(e);
        }

        // lewa strona: pola jednowierszowe (krótkie – numery i rodzaj, jak w EDR.exe)
        var lewe = new (string Etykieta, string Wartosc, bool Krotkie)[]
        {
            ("Numer pociągu:", p.Nr + (p.NrWyj != p.Nr ? " / " + p.NrWyj : ""), true), ("Numer międzynarodowy:", "", true), ("Nazwa pociągu:", p.Nazwa, false),
            ("Numer zamówienia:", "", true), ("Rodzaj pociągu:", p.Rodz, true), ("Przewoźnik:", p.Przewoznik, false), ("Typ pociągu:", p.Kod == p.Przewoznik ? "" : p.Kod, false),
            ("Stacja początkowa:", p.StacjaPocz, false), ("Stacja końcowa:", p.StacjaKonc, false), ("Rodzaj rozkładu jazdy:", "", false),
            ("Nr wniosku:", "", false), ("Data wniosku:", "", false),
        };
        for (int i = 0; i < lewe.Length; i++)
        {
            Wstaw(Etykieta(lewe[i].Etykieta), i, 0);
            Wstaw(Pole(lewe[i].Wartosc, 20, lewe[i].Krotkie ? 160 : 246), i, 1);
        }

        // prawa strona: pola wielowierszowe (etykieta nad polem) i pola wyboru
        var info = string.Join("; ", new[] { p.Pojazd, p.Dlugosc, p.Kursuje ? "" : "nie kursuje (NK)", string.IsNullOrEmpty(p.Stan) ? "" : "stan: " + p.Stan }.Where(x => !string.IsNullOrEmpty(x)));
        var prawe = new StackPanel();
        foreach (var (e, v) in new[] { ("Informacje dodatkowe:", info), ("Ładunek:", ""), ("Uwagi przewoźnika:", p.UwagiPrzew), ("Uwagi eksploatacyjne:", p.UwagiEksp), ("Uwagi własne:", p.UwagiWlasne) })
        {
            prawe.Children.Add(Etykieta(e));
            var pole = Pole(v, 32); pole.Margin = new Thickness(0, 0, 0, 3); prawe.Children.Add(pole);
        }
        // pola wyboru jak w EDR.exe – rozkład ich nie podaje, więc są puste (tylko do odczytu)
        var flagi = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        foreach (var w in new[] { 300.0, 133, 87 }) flagi.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });
        foreach (var (t, r, c) in new[] { ("Przekroczona skrajnia", 0, 0), ("Przekroczony nacisk", 1, 0), ("Inne nadzwyczajne", 2, 0), ("TWR", 0, 1), ("TN", 0, 2), ("Zamknięciowy", 3, 0), ("Śledzony", 3, 1) })
        {
            while (flagi.RowDefinitions.Count <= r) flagi.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var cb = new CheckBox { Content = t, IsChecked = false, IsHitTestVisible = false, Focusable = false, FontFamily = OpisCzcionka, FontSize = 12, Foreground = OpisEtykieta, Margin = new Thickness(0, r == 3 ? 8 : 1, 0, 1) };
            Grid.SetRow(cb, r); Grid.SetColumn(cb, c); flagi.Children.Add(cb);
        }
        prawe.Children.Add(flagi);
        Wstaw(prawe, 0, 3); Grid.SetRowSpan(prawe, lewe.Length + 2);

        // pod spodem: wnioskodawca, rezygnacja i data rezygnacji (łososiowe tło jak w EDR.exe)
        int rz = lewe.Length;
        Wstaw(Etykieta("Wnioskodawca:"), rz, 0); Wstaw(Pole("", 20, 246), rz, 1);
        var rez = new StackPanel { Orientation = Orientation.Horizontal };
        rez.Children.Add(Pole("", 20, 40, OpisRezygnacja));
        var dr = Etykieta("Data rezygnacji:"); dr.Margin = new Thickness(18, 0, 6, 0); rez.Children.Add(dr);
        rez.Children.Add(Pole("", 20, 108, OpisRezygnacja));
        Wstaw(Etykieta("Rezygnacja:"), rz + 1, 0); Wstaw(rez, rz + 1, 1);
    }

    /// <summary>
    /// Kolumny „Trasy pociągu” jak w EDR.exe (E8c): 29 kolumn, nagłówek – gradient od białego do koloru grupy (godziny – oliwkowy, postój – szary,
    /// gotowość – zielony, skład rzeczywisty – morski, pozostałe – granatowy), kremowe komórki, zaznaczenie niebieskie.
    /// </summary>
    void KolumnyTrasy()
    {
        var kol = new (string Naglowek, string Pole, double Szer)[]
        {
            ("Przyjazd plan.", nameof(EdrTrasa.PrzPlanData), 51), ("Godz.", nameof(EdrTrasa.PrzPlanGodz), 35), ("Przyjazd rzecz.", nameof(EdrTrasa.PrzRzeczData), 51), ("Godz.", nameof(EdrTrasa.PrzRzeczGodz), 35),
            ("+/-", nameof(EdrTrasa.PrzOdch), 29), ("Z", nameof(EdrTrasa.Z), 23), ("Lp.", nameof(EdrTrasa.Lp), 25), ("Stacja", nameof(EdrTrasa.Posterunek), 138),
            ("Post. zam.", nameof(EdrTrasa.PZam), 39), ("P. obl.", nameof(EdrTrasa.PObl), 39), ("Typ postoju", nameof(EdrTrasa.Postoj), 55), ("*", nameof(EdrTrasa.Gwiazdka), 18),
            ("P T", nameof(EdrTrasa.PT), 30), ("Odjazd plan.", nameof(EdrTrasa.OdjPlanData), 51), ("Godz.", nameof(EdrTrasa.OdjPlanGodz), 35), ("Odjazd rzecz.", nameof(EdrTrasa.OdjRzeczData), 51),
            ("Godz.", nameof(EdrTrasa.OdjRzeczGodz), 35), ("+/-", nameof(EdrTrasa.OdjOdch), 29), ("Rodz.", nameof(EdrTrasa.Rodz), 36), ("Nr poc.", nameof(EdrTrasa.Nr), 48),
            ("Got. do odj.", nameof(EdrTrasa.Gotowosc), 68), ("Czas wpisu", nameof(EdrTrasa.CzasWpisu), 68), ("Maszynista", nameof(EdrTrasa.Maszynista), 90),
            ("Seria pl.", nameof(EdrTrasa.SeriaPl), 65), ("Dł. pl.", nameof(EdrTrasa.DlPl), 45), ("Brutto pl.", nameof(EdrTrasa.BruttoPl), 52),
            ("Seria rz.", nameof(EdrTrasa.SeriaRz), 82), ("Dł. rz.", nameof(EdrTrasa.DlRz), 45), ("Brutto rz.", nameof(EdrTrasa.BruttoRz), 52),
        };
        // kolor nagłówka i napisu wg numeru kolumny (EDR.exe, RouteAppearance)
        Color Kolor(int n) => n is >= 20 and <= 22 ? Color.FromRgb(0, 130, 0) : n is 1 or 3 or 14 or 16 or 23 or 24 or 25 ? Color.FromRgb(105, 105, 45)
            : n is >= 8 and <= 11 ? Color.FromRgb(128, 128, 128) : n is >= 26 and <= 28 ? Color.FromRgb(0, 80, 85) : Color.FromRgb(0, 0, 75);
        bool Czarny(int n) => n is >= 20 and <= 22 or 3 or 14 or 16 or 8 or 9 or 10;
        trasaPociagu.Background = Brushes.White;
        trasaPociagu.FontFamily = new FontFamily("Tahoma"); trasaPociagu.FontSize = 12; trasaPociagu.RowHeight = 22;
        trasaPociagu.RowBackground = TrasaKrem; trasaPociagu.AlternatingRowBackground = TrasaKrem;
        trasaPociagu.HorizontalGridLinesBrush = TrasaLinia; trasaPociagu.VerticalGridLinesBrush = TrasaLinia;
        trasaPociagu.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        for (int n = 0; n < kol.Length; n++)
        {
            var hs = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
            hs.Setters.Add(new Setter(Control.BackgroundProperty, new LinearGradientBrush(Colors.White, Kolor(n), 90)));
            hs.Setters.Add(new Setter(Control.ForegroundProperty, Czarny(n) ? Brushes.Black : Brushes.White));
            hs.Setters.Add(new Setter(Control.BorderBrushProperty, TrasaRamka));
            hs.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
            hs.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0, 2, 0, 2)));
            hs.Setters.Add(new Setter(Control.FontSizeProperty, 11.0));
            hs.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
            hs.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
            hs.Setters.Add(new Setter(ToolTipService.ToolTipProperty, kol[n].Naglowek + (n is 0 or 2 or 13 or 15 ? " – data" : "")));
            var cs = new Style(typeof(DataGridCell));
            cs.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            cs.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.Black));
            cs.Triggers.Add(new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true, Setters = { new Setter(Control.BackgroundProperty, WykazPociagow.Zaznaczony), new Setter(Control.ForegroundProperty, Brushes.White) } });
            var es = new Style(typeof(TextBlock));
            es.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(3, 0, 3, 0)));
            es.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            es.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, n == 7 ? HorizontalAlignment.Left : HorizontalAlignment.Center));
            trasaPociagu.Columns.Add(new DataGridTextColumn
            {
                Header = new TextBlock { Text = kol[n].Naglowek, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center }, Binding = new System.Windows.Data.Binding(kol[n].Pole), HeaderStyle = hs, CellStyle = cs, ElementStyle = es,
                // jak w EDR.exe (FitRouteColumns): kolumny w proporcjach EDR.exe dopasowane do szerokości okna
                Width = new DataGridLength(kol[n].Szer, DataGridLengthUnitType.Star), MinWidth = 18
            });
        }
    }
    static readonly Brush TrasaKrem = Pedzel(255, 255, 225), TrasaLinia = Pedzel(215, 215, 215), TrasaRamka = Pedzel(190, 190, 190);

    /// <summary>Zrzut: zakładka nad wykazem (0 – wykaz, 1 – opis, 2 – trasa, 3 – przewoźnik).</summary>
    public void PokazZakladkePociagu(int i) => zakladkiPociagu.SelectedIndex = i;

    /// <summary>Okno ustawień dziennika: zasadnicze (zmiana i zapis) albo bieżące (podgląd).</summary>
    internal void Ustawienia(EdrDziennik d, bool biezace) => UstawieniaOkno(d, biezace).ShowDialog();
    public Window UstawieniaOkno(EdrDziennik d, bool biezace) => new UstawieniaDziennika(this, d, Dane?.Posterunki.GetValueOrDefault(d.Post, d.Post) ?? d.Post, biezace) { Owner = this };

    /// <summary>Zakładki: dzienniki posterunków po dwa obok siebie, w kolejności z układu.</summary>
    void Uklad()
    {
        int sel = Math.Max(0, zakladki.SelectedIndex);
        zakladki.Items.Clear(); widoki.Clear();
        var post = wykazPanel.Posterunek;
        var l = Dane.Dzienniki.Where(x => post == null || x.Post == post).ToList();
        widoczne = l; zakladkaDziennika.Clear();
        var grupy = new List<List<EdrDziennik>>();
        foreach (var x in l)
        {
            var gr = x.Zakladka > 0 ? grupy.FirstOrDefault(g => g[0].Zakladka == x.Zakladka && g[0].Post == x.Post) : null;
            if (gr != null && gr.Count < 2) gr.Add(x); else grupy.Add(new() { x });
        }
        foreach (var para in grupy)
        {
            foreach (var x in para) zakladkaDziennika[x.Id] = zakladki.Items.Count;
            var g = new Grid();
            for (int k = 0; k < 2; k++) g.ColumnDefinitions.Add(new ColumnDefinition());
            for (int k = 0; k < para.Count; k++)
            {
                var w = new DziennikWidok(this, para[k]); widoki.Add(w);
                Grid.SetColumn(w.Ramka, k); g.Children.Add(w.Ramka);
            }
            var ti = new TabItem { Header = string.Join(" / ", para.Select(x => x.Sasiad != "" ? x.Sasiad : x.Nazwa)), Content = g, ToolTip = string.Join(" / ", para.Select(x => x.Nazwa)), AllowDrop = true };
            // przeciąganie pociągu z wykazu: najechanie na zakładkę pokazuje jej dzienniki
            ti.DragEnter += (a, e) => { if (e.Data.GetDataPresent(WykazPociagow.Format)) zakladki.SelectedItem = ti; };
            zakladki.Items.Add(ti);
        }
        zakladki.Items.Add(new TabItem { Header = "Dziennik telefoniczny", Content = telefoniczny = new TelefonicznyWidok(this) });
        zakladki.SelectedIndex = Math.Min(sel, Math.Max(0, zakladki.Items.Count - 2));
    }

    /// <summary>Komunikat informacyjny na stopce okna.</summary>
    internal void Info(string t) { komunikat.Text = t; komunikat.Foreground = Brushes.DarkGreen; }

    /// <summary>Wysyła polecenie EDR; błąd pokazuje na stopce.</summary>
    internal CmdResult Wyslij(EdrPolecenie p)
    {
        var r = ses.EdrCmd(p);
        komunikat.Text = r.Msg ?? "";
        komunikat.Foreground = r.Ok ? Brushes.DarkGreen : Brushes.Firebrick;
        Odswiez(true);
        return r;
    }

    /// <summary>Godzina przesunięta o godziny / minuty (puste i „---” – od bieżącej).</summary>
    internal static string Przesun(string v, int dh, int dm, string teraz)
    {
        var g = Edr.Godzina(v);
        if (string.IsNullOrEmpty(g) || g == "---") g = teraz;
        int t = ((int.Parse(g[..2]) + dh) * 60 + int.Parse(g[3..]) + dm) % 1440; if (t < 0) t += 1440;
        return $"{t / 60:00}:{t % 60:00}";
    }

    internal string Teraz => (Dane?.Zegar ?? "00:00")[..5];

    /// <summary>Okno przyjęcia („przyj”) albo wyprawienia („wypr”) pociągu dla dziennika; <paramref name="wstepne"/> – pola wypełnione z wykazu.</summary>
    public Window OknoPociagu(EdrDziennik d, string rodzaj, EdrWersja wstepne = null) =>
        new OknoPociagu(this, d, rodzaj, Dane?.ToryStacyjne.GetValueOrDefault(d.Post) ?? new(), wstepne) { Owner = this };

    /// <summary>Dziennik szlaku <paramref name="szlak"/> na posterunku (null – nie ma).</summary>
    internal EdrDziennik DziennikSzlaku(string post, string szlak) => szlak == null ? null : Dane?.Dzienniki.FirstOrDefault(d => d.Post == post && d.Szlaki.Contains(szlak));

    /// <summary>Pokazuje zakładkę z dziennikiem.</summary>
    /// <summary>Pokazuje zakładkę z dziennikiem (dziennik innego posterunku – najpierw przełącza posterunek u góry).</summary>
    internal void PokazDziennik(EdrDziennik d)
    {
        if (d.Post != wykazPanel.Posterunek) { wykazPanel.Wybierz(d.Post, null); Odswiez(true); }
        if (zakladkaDziennika.TryGetValue(d.Id, out var i)) zakladki.SelectedIndex = i;
    }
    List<EdrDziennik> widoczne = new();
    readonly Dictionary<string, int> zakladkaDziennika = new();

    /// <summary>Wartości wiersza z wykazu: numer przy przyjeździe / odjeździe, tor (rzeczywisty albo z rozkładu), bieżąca godzina odjazdu.</summary>
    internal EdrWersja Wstepne(EdrPociag p, string rodzaj) => new()
    {
        Nr = rodzaj == "wypr" ? p.NrWyj : p.Nr, Tor = p.TorRzecz != "" ? p.TorRzecz : p.Tor, Odj = Teraz
    };

    /// <summary>
    /// Wiersz z wykazu bez okna (dwuklik, przeciągnięcie): przyjęcie z szlaku przyjazdu albo wyprawienie na szlak odjazdu;
    /// <paramref name="d"/> – dziennik, na który upuszczono pociąg (null – EDR wybiera sam).
    /// </summary>
    internal void DodajZWykazu(string post, EdrPociag p, EdrDziennik d = null)
    {
        EdrDziennik dz = DziennikSzlaku(post, p.ZSzlak), dw = DziennikSzlaku(post, p.WSzlak);
        string rodzaj;
        if (d != null) rodzaj = d.Id == dw?.Id && d.Id != dz?.Id ? "wypr" : d.Id == dz?.Id ? "przyj" : p.Przyjety && !p.Wyprawiony ? "wypr" : "przyj";
        else if (dz != null && !p.Przyjety) (d, rodzaj) = (dz, "przyj");
        else if (dw != null && !p.Wyprawiony) (d, rodzaj) = (dw, "wypr");
        else if (dz != null || dw != null) { Info($"Pociąg {p.Nr} jest już wpisany w dzienniku ruchu{(p.Przyjety && p.Wyprawiony ? " (przyjęcie i wyprawienie)" : "")} – kolejny wpis tylko przez PPM"); return; }
        else { komunikat.Text = $"Pociąg {p.Nr}: nie wiadomo, po którym szlaku jedzie – użyj „Przyjmij z innego szlaku” albo „Wypraw na inny szlak”"; komunikat.Foreground = Brushes.Firebrick; return; }
        // upuszczenie na dziennik: ten sam wpis drugi raz – nie
        if (rodzaj == "przyj" && p.Przyjety || rodzaj == "wypr" && p.Wyprawiony) { Info($"{(rodzaj == "przyj" ? "Przyjęcie" : "Wyprawienie")} pociągu {p.Nr} jest już wpisane – kolejny wpis tylko przez PPM"); return; }
        Wyslij(new EdrPolecenie { Dziennik = d.Id, Rodzaj = rodzaj, Dane = Wstepne(p, rodzaj) });
        PokazDziennik(d);
    }

    /// <summary>Polecenia z menu wykazu: „przyj” / „wypr” – okno dla dziennika szlaku z rozkładu; „przyjinny” / „wyprinny” – najpierw wybór szlaku.</summary>
    internal void PolecenieWykazu(string post, EdrPociag p, string co)
    {
        bool przyj = co.StartsWith("przyj");
        var d = co.EndsWith("inny") ? WybierzSzlak(post) : DziennikSzlaku(post, przyj ? p.ZSzlak : p.WSzlak);
        if (d == null) return;
        PokazDziennik(d);
        OknoPociagu(d, przyj ? "przyj" : "wypr", Wstepne(p, przyj ? "przyj" : "wypr")).ShowDialog();
    }

    /// <summary>Okno „Szlaki posterunku” (Rys. 35): wybór dziennika, w którym ma być wpis.</summary>
    internal EdrDziennik WybierzSzlak(string post)
    {
        var l = Dane?.Dzienniki.Where(x => x.Post == post).ToList() ?? new();
        var w = new SzlakiPosterunku(l) { Owner = this };
        return w.ShowDialog() == true ? w.Wybrany : null;
    }

    /// <summary>Zrzut i testy: wybór zakładki z dziennikiem.</summary>
    public void PokazDziennik(int i) { if (Dane != null && i >= 0 && i < Dane.Dzienniki.Count) PokazDziennik(Dane.Dzienniki[i]); }
}

/// <summary>Jeden dziennik ruchu: nagłówek z nazwą i przyciskami, tabela kolumn 1–7, nawigacja po dobach.</summary>
class DziennikWidok
{
    static readonly double[] Szer = { 72, 72, 44, 80, 100, 104 };
    static readonly string[] PoleKolumny = { "Nr", "Nr", "Tor", "Dw", "Odj", "Prz", "Uwagi" };
    readonly EdrWindow okno;
    public readonly string Id;
    public readonly Border Ramka;
    readonly StackPanel wiersze = new();
    readonly ScrollViewer sv;
    readonly TextBlock nazwa = new() { FontSize = 17, FontWeight = FontWeights.Bold, Foreground = Brushes.Black, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Background = Brushes.Transparent, TextTrimming = TextTrimming.CharacterEllipsis },
        prowadzi = new() { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, FontSize = 11 };
    readonly ComboBox daty = new() { Width = 100, Margin = new Thickness(2, 0, 2, 0) };
    readonly StackPanel ikony = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    EdrDziennik d;
    int strona = -1;
    bool wypelnianie;
    public bool Edycja;

    public DziennikWidok(EdrWindow okno, EdrDziennik d)
    {
        this.okno = okno; this.d = d; Id = d.Id;
        var dock = new DockPanel();

        var nag = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
        var przyc = new StackPanel { Orientation = Orientation.Horizontal };
        przyc.Children.Add(Ikona("↓", "", "Przyjęcie pociągu – puste okno przyjęcia", () => Nowy("przyj")));
        przyc.Children.Add(Ikona("↑", "", "Wyprawienie pociągu – puste okno wyprawienia", () => Nowy("wypr")));
        przyc.Children.Add(Ikona("", "\uE9D5", "Powiadomienie dróżników przejazdowych o jeździe pociągu (kolumna 8)", () => okno.PowiadomDroznikow(this.d)));
        przyc.Children.Add(Ikona("", "\uE717", "Telefonogram – rejestracja w dzienniku ruchu (i w dzienniku telefonicznym)", () => okno.Telefonogram(this.d.Post, this.d, false)));
        przyc.Children.Add(Ikona("", "\uE946", "Wpis o dowolnej treści do dziennika ruchu", () => okno.WpisDowolny(this.d)));
        przyc.Children.Add(Ikona("", "\uE713", "Ustawienia bieżące dziennika ruchu (stan szlaku)", () => okno.Ustawienia(this.d, true)));
        DockPanel.SetDock(przyc, Dock.Right); nag.Children.Add(przyc);
        DockPanel.SetDock(ikony, Dock.Left); nag.Children.Add(ikony);
        nag.Children.Add(nazwa);
        var mn = new ContextMenu(); var mu = new MenuItem { Header = "Ustawienia zasadnicze dziennika ruchu…" }; mu.Click += (a, e) => okno.Ustawienia(this.d, false); mn.Items.Add(mu);
        nazwa.ContextMenu = mn; nazwa.ToolTip = "PPM – ustawienia zasadnicze dziennika ruchu (kierunek zasadniczy, kierunek parzysty, pola wymagane)";
        DockPanel.SetDock(nag, Dock.Top); dock.Children.Add(nag);

        // nagłówek kolumn jak na filmie: dwa ciemne pasy (nazwy, numery) i jaśniejszy pas „Nieparzysty / Parzysty”
        kol = new Grid { Background = new LinearGradientBrush(Color.FromRgb(0x2C, 0x2C, 0x2C), Color.FromRgb(0x5A, 0x5A, 0x5A), 90) };
        DockPanel.SetDock(kol, Dock.Top); dock.Children.Add(kol);
        Naglowek();

        var stopka = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
        var nav = new StackPanel { Orientation = Orientation.Horizontal };
        nav.Children.Add(Nav("|<", "Pierwsza strona dziennika", () => Idz(0)));
        nav.Children.Add(Nav("<", "Poprzednia strona (poprzednia doba)", () => Idz(Biezaca - 1)));
        nav.Children.Add(daty);
        nav.Children.Add(Nav(">", "Następna strona (następna doba)", () => Idz(Biezaca + 1)));
        nav.Children.Add(Nav(">|", "Ostatnia strona", () => Idz(-1)));
        nav.Children.Add(Nav("^", "Pierwszy wpis na stronie", () => sv.ScrollToTop()));
        nav.Children.Add(Nav("v", "Ostatni wpis na stronie", () => sv.ScrollToBottom()));
        daty.SelectionChanged += (a, b) => { if (!wypelnianie && daty.SelectedIndex >= 0) Idz(daty.SelectedIndex); };
        DockPanel.SetDock(nav, Dock.Left); stopka.Children.Add(nav);
        // „Zapisz” jak na filmie: zapis do bazy odbywa się sam, przycisk tylko to potwierdza (podręcznik EDR, rozdz. 7.2)
        var zapisz = new Button { Content = "Zapisz", Width = 170, Margin = new Thickness(4, 0, 2, 0), Background = new LinearGradientBrush(Color.FromRgb(0xF4, 0xF4, 0xF4), Color.FromRgb(0xD2, 0xD2, 0xD2), 90), ToolTip = "Zapisy dziennika ruchu są rejestrowane w bazie automatycznie" };
        zapisz.Click += (a, e) => okno.Info($"Zapisy dziennika {this.d.Nazwa} zarejestrowane w bazie danych.");
        DockPanel.SetDock(zapisz, Dock.Right); stopka.Children.Add(zapisz);
        stopka.Children.Add(prowadzi);
        DockPanel.SetDock(stopka, Dock.Bottom); dock.Children.Add(stopka);

        sv = new ScrollViewer { Content = wiersze, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = EdrWindow.PoleDziennika };
        dock.Children.Add(sv);
        Ramka = new Border { Child = dock, BorderBrush = EdrWindow.Linia, BorderThickness = new Thickness(1), Margin = new Thickness(2), Background = EdrWindow.Tlo, AllowDrop = true };
        // pociąg przeciągnięty z wykazu – nowy wiersz na końcu tego dziennika
        Ramka.DragOver += (a, e) => { e.Effects = e.Data.GetDataPresent(WykazPociagow.Format) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
        Ramka.Drop += (a, e) =>
        {
            if (e.Data.GetData(WykazPociagow.Format) is not string klucz || okno.WykazPanel.Znajdz(klucz) is not (string post, EdrPociag p)) return;
            if (post != this.d.Post) return;
            okno.DodajZWykazu(post, p, this.d);
        };
    }

    int Biezaca => strona < 0 ? d.Strony.Count - 1 : strona;
    void Idz(int s) { strona = s < 0 || s >= d.Strony.Count - 1 ? -1 : s; Pokaz(d); }

    static Button Ikona(string strzalka, string glif, string opis, Action klik)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        if (strzalka != "") sp.Children.Add(new TextBlock { Text = strzalka, FontWeight = FontWeights.Bold, FontSize = 14, VerticalAlignment = VerticalAlignment.Center });
        sp.Children.Add(new TextBlock { Text = glif, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 23, VerticalAlignment = VerticalAlignment.Center, Foreground = EdrWindow.Pedzel(0x22, 0x22, 0x22) });
        var b = new Button
        {
            Content = sp, ToolTip = opis, Width = 50, Height = 40, Margin = new Thickness(3, 0, 3, 0), IsEnabled = klik != null, BorderThickness = new Thickness(1.5),
            Background = new LinearGradientBrush(Color.FromRgb(0xFA, 0xFA, 0xFA), Color.FromRgb(0xD4, 0xD4, 0xD4), 90), BorderBrush = EdrWindow.Pedzel(0x9A, 0x9A, 0x9A)
        };
        ToolTipService.SetShowOnDisabled(b, true);
        if (klik != null) b.Click += (a, e) => klik();
        return b;
    }

    static readonly Brush Szyna = EdrWindow.Pedzel(0x40, 0x40, 0x40), Zasadniczy = EdrWindow.Pedzel(0x90, 0x90, 0x90), Jazda = EdrWindow.Pedzel(0x1E, 0x5A, 0xD8);

    /// <summary>
    /// Ikona toru szlakowego (podręcznik EDR, Rys. 37): szara strzałka – zasadniczy kierunek (w dół – do naszego posterunku),
    /// tor czerwony – zajęty wg dziennika, niebieska strzałka na torze – kierunek jazdy pociągu.
    /// </summary>
    static FrameworkElement IkonaToru(EdrTorSzlakowy t)
    {
        var tor = new Canvas { Width = 14, Height = 26 };
        var kolor = t.Zajety ? EdrWindow.TorZajety : Szyna;
        void Prost(double x, double y, double w, double h) { var r = new System.Windows.Shapes.Rectangle { Width = w, Height = h, Fill = kolor }; Canvas.SetLeft(r, x); Canvas.SetTop(r, y); tor.Children.Add(r); }
        Prost(2, 0, 2, 26); Prost(10, 0, 2, 26);
        for (int i = 0; i < 5; i++) Prost(0, 1 + i * 5.5, 14, 2);
        var g = new Grid { Width = 14, Height = 26 };
        g.Children.Add(tor);
        // tor zamknięty (stan bieżący, z telefonogramu): czerwony krzyż na ikonie
        if (t.Status == "Zamknięty") g.Children.Add(new TextBlock { Text = "✕", Foreground = EdrWindow.TorZajety, FontWeight = FontWeights.Bold, FontSize = 20, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, -4, 0, 0) });
        if (t.Zajety) g.Children.Add(new TextBlock { Text = t.Ruch < 0 ? "↓" : "↑", Foreground = Jazda, FontWeight = FontWeights.Bold, FontSize = 17, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, -3, 0, 0) });
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 6, 0), Background = Brushes.Transparent };
        sp.Children.Add(new TextBlock { Text = t.Kierunek < 0 ? "↓" : t.Kierunek > 0 ? "↑" : "↕", Foreground = Zasadniczy, FontWeight = FontWeights.Bold, FontSize = 15, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 1, 2) });
        var nr = new Border { BorderBrush = EdrWindow.Pedzel(0x55, 0x55, 0x55), BorderThickness = new Thickness(0.8), Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(2, 0, 2, 0),
            Child = new TextBlock { Text = t.Tor == "" ? "–" : t.Tor, FontSize = 8.5, Foreground = Brushes.Black } };
        sp.Children.Add(new StackPanel { Children = { nr, g } });
        var kier = t.Kierunek < 0 ? "Z posterunku sąsiedniego" : t.Kierunek > 0 ? "DO posterunku sąsiedniego" : "nieustalony (ustawienia dziennika – PPM na nazwie)";
        sp.ToolTip = $"Tor szlakowy {(t.Strona != "" ? t.Strona + " " : "")}{t.Tor} (szlak {t.Szlak}), zasadniczy kierunek ruchu: {kier}" +
            (t.Status == "Zamknięty" ? $"{(char)10}Tor ZAMKNIĘTY (stan bieżący – telefonogram)" : "") +
            (t.Zajety ? $"{(char)10}Zajęty wg dziennika: {string.Join(", ", t.Pociagi)} – jazda {(t.Ruch < 0 ? "do naszego posterunku" : "od naszego posterunku")}" : $"{(char)10}Wolny wg dziennika");
        return sp;
    }

    static Button Nav(string t, string opis, Action klik)
    {
        var b = new Button { Content = t, ToolTip = opis, MinWidth = 26, Margin = new Thickness(1, 0, 1, 0), Padding = new Thickness(4, 0, 4, 0) };
        b.Click += (a, e) => klik();
        return b;
    }

    readonly Grid kol;
    /// <summary>Dróżnicy przejazdowi w kolumnie 8 (z ustawień bieżących, „W dzienniku ruchu”).</summary>
    List<EdrDroznik> dr = new();
    string drPodpis = "";
    int Kol => 7 + dr.Count;

    void Kolumny(Grid g)
    {
        foreach (var w in Szer) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 120 });
        foreach (var x in dr) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(x.Godzina ? 78 : 52) });
    }

    /// <summary>Nagłówek: Nr pociągu (1 nieparzysty, 2 parzysty), 3–7, kolumna 8 „O jeździe pociągu zawiadomiono dróżników przejazdowych” – po kolumnie na dróżnika (km, oznaczenie).</summary>
    void Naglowek()
    {
        kol.Children.Clear(); kol.ColumnDefinitions.Clear(); kol.RowDefinitions.Clear();
        Kolumny(kol);
        void H(int c, string t, int span = 1, int row = 0, double rozm = 0)
        {
            var b = new Border
            {
                BorderBrush = EdrWindow.Pedzel(0x80, 0x80, 0x80), BorderThickness = new Thickness(0, 0, 1, row < 2 ? 1 : 0),
                Background = row == 2 ? new LinearGradientBrush(Color.FromRgb(0x9A, 0x9A, 0x9A), Color.FromRgb(0x6A, 0x6A, 0x6A), 90) : null,
                Child = new TextBlock { Text = t, Foreground = Brushes.White, FontSize = rozm > 0 ? rozm : row == 2 ? 10 : 11, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(2, 1, 2, 1), TextTrimming = TextTrimming.CharacterEllipsis }
            };
            Grid.SetColumn(b, c); Grid.SetColumnSpan(b, span); Grid.SetRow(b, row); kol.Children.Add(b);
        }
        for (int r = 0; r < 3; r++) kol.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        H(0, "Nr pociągu", 2); H(2, "Tor"); H(3, "Droga wolna"); H(4, "Pociąg odjechał"); H(5, "Pociąg przyjechał"); H(6, "Uwagi");
        for (int c = 0; c < 7; c++) H(c, (c + 1).ToString(), 1, 1);
        H(0, "Nieparzysty", 1, 2); H(1, "Parzysty", 1, 2); for (int c = 2; c < 7; c++) H(c, "", 1, 2);
        if (dr.Count > 0)
        {
            H(7, "O jeździe poc. zawiad. dróżników", dr.Count, 0, 9.5);
            H(7, "8", dr.Count, 1);
            for (int i = 0; i < dr.Count; i++) H(7 + i, dr[i].Km != "" ? dr[i].Km : dr[i].Oznaczenie, 1, 2);
            ((FrameworkElement)kol.Children[^(dr.Count + 2)]).ToolTip = "O jeździe pociągu zawiadomiono dróżników przejazdowych: " + string.Join(", ", dr.Select(x => $"{x.Oznaczenie} (km {x.Km}, {(x.Godzina ? "godzina" : "ukośnik")})"));
        }
    }

    // ------------------------------------------------------------------ wyświetlanie
    public void Pokaz(EdrDziennik nowy)
    {
        d = nowy;
        dr = d.Biezace.Droznicy.Where(x => x.WDzienniku).ToList();
        var dp = string.Join("|", dr.Select(x => $"{x.Id}:{x.Km}:{x.Oznaczenie}:{x.WgSzlaku}"));
        if (dp != drPodpis) { drPodpis = dp; Naglowek(); }
        nazwa.Text = d.Nazwa;
        ikony.Children.Clear();
        foreach (var t in d.ToryStan) ikony.Children.Add(IkonaToru(t));
        prowadzi.Inlines.Clear(); prowadzi.Inlines.Add("Prowadzi: "); prowadzi.Inlines.Add(new System.Windows.Documents.Run(d.Prowadzi ?? "–") { FontWeight = FontWeights.Bold });
        wypelnianie = true;
        daty.ItemsSource = d.Strony.Select(s => s.Data).ToList();
        daty.SelectedIndex = Biezaca;
        wypelnianie = false;
        bool naDole = sv.VerticalOffset >= sv.ScrollableHeight - 2;
        wiersze.Children.Clear();
        if (d.Strony.Count == 0) return;
        foreach (var w in d.Strony[Biezaca].Wiersze) wiersze.Children.Add(Wiersz(w));
        if (naDole) sv.ScrollToBottom();
    }

    UIElement Wiersz(EdrWiersz w)
    {
        var g = new Grid(); Kolumny(g);
        if (w.Rodzaj == "telefonogram" && w.Tel is EdrTelefonogram tel)
        {
            // telefonogram na całą szerokość strony (podręcznik EDR, Rys. 39): treść pogrubiona, pod nią „Nadał …, odebrał … godz. …”
            var spt = new StackPanel { Margin = new Thickness(60, 3, 8, 3) };
            spt.Children.Add(new TextBlock { Text = tel.Tresc, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap });
            if (tel.Notatka != "") spt.Children.Add(new TextBlock { Text = tel.Notatka, TextWrapping = TextWrapping.Wrap });
            spt.Children.Add(new TextBlock { Text = Edr.Podpis(tel), Foreground = EdrWindow.Pedzel(0x55, 0x55, 0x55), FontSize = 11 });
            var bt = new Border { Background = Telefonogram, BorderBrush = EdrWindow.Linia, BorderThickness = new Thickness(0, 0, 0, 1), Child = spt,
                ToolTip = $"Telefonogram nr {tel.Nr} ({(tel.Nadaje ? "nadany" : "odebrany")} {tel.Data} o godz. {tel.Godz}, wprowadzony o {tel.Wprowadzono}), wzór {tel.Wzor}" + (tel.Zmiany.Count > 0 ? $"{(char)10}Zmiana stanu szlaku: " + string.Join("; ", tel.Zmiany.Select(OpisZmiany)) : "") };
            Grid.SetColumnSpan(bt, Kol); g.Children.Add(bt);
            return g;
        }
        if (w.Rodzaj == "tekst")
        {
            var t = new Border { Background = EdrWindow.Tekstowy, BorderBrush = EdrWindow.Linia, BorderThickness = new Thickness(0, 0, 0, 1),
                Child = new TextBlock { Text = w.Tekst, FontWeight = FontWeights.Bold, FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(4, 6, 4, 6) } };
            Grid.SetColumnSpan(t, Kol); g.Children.Add(t);
            return g;
        }
        if (w.Rodzaj == "wpis")
        {
            // wpis o dowolnej treści: tekst od lewej, kto i kiedy
            var sw = new StackPanel { Margin = new Thickness(60, 3, 8, 3) };
            sw.Children.Add(new TextBlock { Text = w.Tekst, TextWrapping = TextWrapping.Wrap, TextDecorations = w.Wykreslony ? TextDecorations.Strikethrough : null, Foreground = w.Wykreslony ? Wykreslony : Brushes.Black });
            sw.Children.Add(new TextBlock { Text = $"Wpisał {w.W.Kto} godz. {w.W.Uwagi}", Foreground = EdrWindow.Pedzel(0x55, 0x55, 0x55), FontSize = 11 });
            if (w.Wykreslony) sw.Children.Add(PodpisWykreslenia(w));
            var bw = new Border { Background = Brushes.White, BorderBrush = EdrWindow.Linia, BorderThickness = new Thickness(0, 0, 0, 1), Child = sw, ToolTip = "Wpis o dowolnej treści" + (w.Wykreslony ? "" : "; PPM – wykreślenie") };
            if (!w.Wykreslony) { var mw = new ContextMenu(); mw.Items.Add(Pozycja("Wykreśl wpis…", () => Wykresl(w))); bw.ContextMenu = mw; }
            Grid.SetColumnSpan(bw, Kol); g.Children.Add(bw);
            return g;
        }
        var v = w.W; int kn = Edr.KolumnaNumeru(v.Nr) - 1;
        var tlo = w.Kompletny ? EdrWindow.Kompletny : EdrWindow.Niekompletny;
        for (int c = 0; c < 7; c++)
        {
            var pole = PoleKolumny[c];
            string tekst = c is 0 or 1 ? (c == kn ? v.Nr : "") : Edr.Wartosc(v, pole);
            if (c == 6 && v.Lewy) tekst = "Po torze lewym" + (tekst != "" ? "; " + tekst : "");
            if (c == 6 && w.Wykreslony) tekst += (tekst != "" ? "; " : "") + "Wykreślono: " + v.Wykreslono;
            bool brak = !w.Wykreslony && (c is 0 or 1 ? c == kn : true) && w.Wymagane.Contains(pole) && Edr.Wartosc(v, pole) == "";
            g.Children.Add(Komorka(w, c, pole, tekst, brak ? EdrWindow.Brak : tlo));
        }
        // kolumna 8: dróżnicy przejazdowi – godzina (jak na linii jednotorowej) albo ukośnik (dwu- i więcej torowej); szare – nie dotyczy kierunku
        for (int i = 0; i < dr.Count; i++)
        {
            var x = dr[i]; var pole = "Dr:" + x.Id; var wart = Edr.Wartosc(v, pole);
            bool dotyczy = w.Rodzaj != "powrot" && x.Dotyczy(w.Rodzaj);
            var tl = !dotyczy ? NieDotyczy : !w.Wykreslony && w.Wymagane.Contains(pole) && wart == "" ? EdrWindow.Brak : tlo;
            if (!dotyczy) { var bn = new Border { Background = tl, BorderBrush = EdrWindow.Linia, BorderThickness = new Thickness(0, 0, 1, 1), ToolTip = $"Dróżnik {x.Oznaczenie}: nie dotyczy pociągów w tym kierunku" }; Grid.SetColumn(bn, 7 + i); g.Children.Add(bn); continue; }
            if (x.Godzina) { var kb = Komorka(w, 7 + i, pole, wart, tl, true); kb.ToolTip = $"Dróżnik {x.Oznaczenie} (km {x.Km}) – godzina powiadomienia. " + kb.ToolTip; g.Children.Add(kb); continue; }
            var bu = new Border { Background = tl, BorderBrush = EdrWindow.Linia, BorderThickness = new Thickness(0, 0, 1, 1), ToolTip = $"Dróżnik {x.Oznaczenie} (km {x.Km}) – klik: ukośnik „\\” (powiadomiono)",
                Child = new TextBlock { Text = wart, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(2, 3, 2, 3), FontWeight = FontWeights.Bold } };
            bu.MouseLeftButtonDown += (a, e) => { e.Handled = true; if (!w.Wykreslony) Ustaw(w, pole, wart == "" ? "\\" : ""); };
            Grid.SetColumn(bu, 7 + i); g.Children.Add(bu);
        }
        // jazda do km i z powrotem: klamra spina wiersz wyjazdu i wiersz powrotu (podręcznik EDR, Rys. 48)
        if (w.Para != 0)
        {
            var kl = new TextBlock { Text = w.Pierwszy ? "⎧" : "⎩", FontSize = 18, Foreground = EdrWindow.Pedzel(0x33, 0x33, 0x33), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = w.Pierwszy ? VerticalAlignment.Bottom : VerticalAlignment.Top,
                Margin = new Thickness(1, w.Pierwszy ? 0 : -6, 0, w.Pierwszy ? -6 : 0), IsHitTestVisible = false };
            Grid.SetColumn(kl, 0); g.Children.Add(kl);
        }
        if (w.Wersje.Count > 1)
            g.ToolTip = "Historia zmian:\n" + string.Join("\n", w.Wersje.Select(x => $"{x.Zmiana}  {x.Kto} – " + (x.Pole == null ? $"zapis: {x.Nr}" : x.Pole == "Wykreślenie" ? $"wykreślono: {x.Wykreslono}"
                : $"{Edr.NazwaPola(x.Pole)}: {(Edr.Wartosc(x, x.Pole) == "" ? "(puste)" : Edr.Wartosc(x, x.Pole))}")));
        if (w.Wczytany) g.ToolTip = (g.ToolTip is string tt ? tt + "\n" : "") + "Wpis z wcześniejszego uruchomienia (plik dzienników) – nie liczy się do stanu torów i wykazu.";
        return g;
    }

    static readonly Brush Wykreslony = EdrWindow.Pedzel(0x80, 0x80, 0x80);

    TextBlock PodpisWykreslenia(EdrWiersz w) => new() { Text = $"Wykreślono: {w.W.Wykreslono} ({w.W.Kto}, {w.W.Zmiana})", Foreground = Brushes.Firebrick, FontSize = 11, TextWrapping = TextWrapping.Wrap };

    /// <summary>Wykreślenie wpisu (E8d): okno z przyczyną; wpis zostaje w dzienniku przekreślony.</summary>
    void Wykresl(EdrWiersz w) => new OknoWykreslenia(okno, d, w) { Owner = okno }.ShowDialog();

    Border Komorka(EdrWiersz w, int c, string pole, string tekst, Brush tlo, bool? godzina = null)
    {
        bool godz = godzina ?? Edr.Godzinowe.Contains(pole);
        var tb = new TextBlock { Text = tekst, Margin = new Thickness(4, 3, 4, 3), HorizontalAlignment = c < 6 || godzina != null ? HorizontalAlignment.Center : HorizontalAlignment.Left, TextTrimming = TextTrimming.CharacterEllipsis };
        var b = new Border { Background = tlo, BorderBrush = EdrWindow.Linia, BorderThickness = new Thickness(0, 0, 1, 1), Child = tb };
        Grid.SetColumn(b, c);
        if (w.Wykreslony)
        {
            // wpis wykreślony: przekreślony, szary, bez edycji; przyczyna w uwagach i w podpowiedzi
            tb.TextDecorations = TextDecorations.Strikethrough; tb.Foreground = c == 6 ? Brushes.Firebrick : Wykreslony;
            b.ToolTip = $"Wpis wykreślony: {w.W.Wykreslono} ({w.W.Kto}, {w.W.Zmiana})";
            return b;
        }
        b.MouseLeftButtonDown += (a, e) =>
        {
            if (godz) { if (!Edycja) { e.Handled = true; EdytujGodzine(b, w, pole); } return; }
            if (e.ClickCount != 2) return;
            e.Handled = true;
            Edytuj(b, w, pole);
        };
        if (godz)
        {
            b.ToolTip = "Klik – wpis godziny (kółko myszy – ± minuta w prawej połowie pola, ± godzina w lewej); ☐ – druga godzina (np. 08:00 / 08:12), gdy potrzebna; Enter albo wyjście z pola – zapis, ⊗ albo Esc – rezygnacja.";
            var menu = new ContextMenu();
            menu.Items.Add(Pozycja("Bieżąca godzina", () => Ustaw(w, pole, okno.Teraz)));
            menu.Items.Add(Pozycja("Nie dotyczy (---)", () => Ustaw(w, pole, "---")));
            menu.Items.Add(Pozycja("Wyczyść", () => Ustaw(w, pole, "")));
            menu.Items.Add(new Separator());
            menu.Items.Add(Pozycja("Wykreśl wpis…", () => Wykresl(w)));
            b.ContextMenu = menu;
        }
        else
        {
            b.ToolTip = "Dwuklik – zmiana wpisu; PPM – wykreślenie wpisu";
            var menu = new ContextMenu(); menu.Items.Add(Pozycja("Wykreśl wpis…", () => Wykresl(w))); b.ContextMenu = menu;
        }
        return b;
    }

    static readonly Brush Telefonogram = EdrWindow.Pedzel(0xF2, 0xCF, 0x96), NieDotyczy = EdrWindow.Pedzel(0xD8, 0xD8, 0xD8);

    static string OpisZmiany(EdrZmianaToru z) => $"szlak {z.Szlak}: " + string.Join(", ", new[]
    {
        z.Status != null ? "tor " + z.Status.ToLowerInvariant() + (string.IsNullOrEmpty(z.PoPociagu) ? "" : $" po przyjeździe pociągu {z.PoPociagu}") : null,
        z.Rodzaj != null ? (z.Rodzaj == "*" ? "ruch zasadniczy" : "ruch " + z.Rodzaj.ToLowerInvariant()) : null,
        z.Zasadniczy is int k ? "kierunek zasadniczy " + (k < 0 ? "Z posterunku sąsiedniego" : k > 0 ? "DO posterunku sąsiedniego" : "nieustalony") : null,
        z.Podstawa != null ? (z.Podstawa == "*" ? "prowadzenie ruchu zasadnicze" : "jak dla: " + z.Podstawa) : null
    }.Where(x => x != null));

    static MenuItem Pozycja(string t, Action a) { var m = new MenuItem { Header = t }; m.Click += (s, e) => a(); return m; }

    void Ustaw(EdrWiersz w, string pole, string v) => okno.Wyslij(new EdrPolecenie { Dziennik = d.Id, Rodzaj = "pole", Wiersz = w.Id, Pole = pole, Wartosc = v });

    /// <summary>
    /// Edytor godziny w komórce (film ISDR): pole z godziną (pusta komórka – bieżąca), pole wyboru – druga godzina (nie zawsze potrzebna),
    /// czerwony krzyżyk – rezygnacja. Enter albo wyjście z edytora – zapis. Kółko myszy zmienia minuty (prawa połowa pola) albo godziny (lewa).
    /// </summary>
    void EdytujGodzine(Border b, EdrWiersz w, string pole)
    {
        Edycja = true;
        var stara = Edr.Godzina(Edr.Wartosc(w.W, pole)) ?? "";
        var cz = stara.Split(" / ");
        TextBox Pole(string v)
        {
            var t = new TextBox { Text = v, Width = 42, Padding = new Thickness(1, 0, 1, 0), VerticalContentAlignment = VerticalAlignment.Center, FontSize = 11.5 };
            t.MouseWheel += (a, e) => { e.Handled = true; bool h = e.GetPosition(t).X < t.ActualWidth / 2; int k = e.Delta > 0 ? 1 : -1; t.Text = EdrWindow.Przesun(t.Text, h ? k : 0, h ? 0 : k, okno.Teraz); t.SelectAll(); };
            t.GotKeyboardFocus += (a, e) => t.SelectAll();
            return t;
        }
        var tb = Pole(cz[0] != "" && cz[0] != "---" ? cz[0] : okno.Teraz);
        var tb2 = Pole(cz.Length > 1 ? cz[1] : okno.Teraz); tb2.Visibility = cz.Length > 1 ? Visibility.Visible : Visibility.Collapsed; tb2.Margin = new Thickness(2, 0, 0, 0);
        var ok = new CheckBox { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 2, 0), ToolTip = "Druga godzina", IsChecked = cz.Length > 1 };
        var x = new Button { Width = 18, Height = 18, Padding = new Thickness(0), Margin = new Thickness(2, 0, 0, 0), ToolTip = "Rezygnacja", Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Content = new Border { Width = 15, Height = 15, CornerRadius = new CornerRadius(7.5), Background = EdrWindow.Pedzel(0xD9, 0x2B, 0x2B), Child = new TextBlock { Text = "✕", Foreground = Brushes.White, FontSize = 9, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } } };
        var sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(1) };
        sp.Children.Add(tb); sp.Children.Add(ok); sp.Children.Add(tb2); sp.Children.Add(x);
        b.Child = sp;
        bool koniec = false;
        void Zakoncz(bool zapis)
        {
            if (koniec) return; koniec = true; Edycja = false;
            if (zapis) Ustaw(w, pole, ok.IsChecked == true ? $"{tb.Text} / {tb2.Text}" : tb.Text); else okno.Odswiez(true);
        }
        ok.Checked += (a, e) => { tb2.Visibility = Visibility.Visible; tb2.Focus(); };
        ok.Unchecked += (a, e) => { tb2.Visibility = Visibility.Collapsed; tb.Focus(); };
        x.Click += (a, e) => Zakoncz(false);
        foreach (var t in new[] { tb, tb2 }) t.KeyDown += (a, e) => { if (e.Key == Key.Enter) Zakoncz(true); else if (e.Key == Key.Escape) Zakoncz(false); };
        // wyjście z edytora (klik gdzie indziej) – zapis
        sp.IsKeyboardFocusWithinChanged += (a, e) => { if (!(bool)e.NewValue) sp.Dispatcher.BeginInvoke(() => { if (!sp.IsKeyboardFocusWithin && !x.IsMouseOver) Zakoncz(true); }, System.Windows.Threading.DispatcherPriority.Input); };
        tb.Loaded += (a, e) => { tb.Focus(); tb.SelectAll(); };
    }

    /// <summary>Edycja tekstu w komórce: Enter / wyjście z pola – zapis, Esc – rezygnacja.</summary>
    void Edytuj(Border b, EdrWiersz w, string pole)
    {
        Edycja = true;
        var tb = new TextBox { Text = Edr.Wartosc(w.W, pole), BorderThickness = new Thickness(0), Padding = new Thickness(2, 2, 2, 2) };
        b.Child = tb; tb.Focus(); tb.SelectAll();
        bool koniec = false;
        void Zakoncz(bool zapis)
        {
            if (koniec) return; koniec = true; Edycja = false;
            if (zapis) Ustaw(w, pole, tb.Text); else okno.Odswiez(true);
        }
        tb.KeyDown += (a, e) => { if (e.Key == Key.Enter) Zakoncz(true); else if (e.Key == Key.Escape) Zakoncz(false); };
        tb.LostKeyboardFocus += (a, e) => Zakoncz(true);
    }

    void Nowy(string rodzaj) => okno.OknoPociagu(d, rodzaj).ShowDialog();
}

/// <summary>Okno przyjęcia / wyprawienia pociągu (podręcznik EDR, Rys. 34 i 36). Pola wymagane – niebieskie, dopóki puste.</summary>
class OknoPociagu : Window
{
    public OknoPociagu(EdrWindow okno, EdrDziennik d, string rodzaj, List<string> tory, EdrWersja wstepne = null)
    {
        bool przyj = rodzaj == "przyj";
        Title = przyj ? "Przyjęcie pociągu" : "Wyprawienie pociągu";
        Width = 380; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EdrWindow.Tlo; FontFamily = new FontFamily("Segoe UI"); ShowInTaskbar = false;
        var wym = Edr.Wymagane(d.Biezace, d.Ustawienia);
        var sp = new StackPanel { Margin = new Thickness(14) };
        sp.Children.Add(new TextBlock { Text = $"Pociąg {(przyj ? "Z" : "DO")}  {d.Sasiad.ToUpperInvariant()}", FontWeight = FontWeights.Bold, FontSize = 13, Foreground = EdrWindow.Zegar, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) });
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        int row = 0;
        void Wiersz(string etykieta, FrameworkElement pole, FrameworkElement dod = null)
        {
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = new TextBlock { Text = etykieta, VerticalAlignment = VerticalAlignment.Center }; Grid.SetRow(l, row); g.Children.Add(l);
            pole.Margin = new Thickness(0, 3, 4, 3); Grid.SetRow(pole, row); Grid.SetColumn(pole, 1); g.Children.Add(pole);
            if (dod != null) { Grid.SetRow(dod, row); Grid.SetColumn(dod, 2); g.Children.Add(dod); }
            row++;
        }
        var dz = new TextBlock { Text = d.Nazwa, FontWeight = FontWeights.Bold }; Wiersz("Dziennik ruchu:", dz);
        var nr = new TextBox(); Wiersz("Nr pociągu", nr);
        var tor = new ComboBox { IsEditable = true, ItemsSource = tory }; Wiersz("Tor stacyjny", tor);
        TextBox Godz(string pole, string etykieta, string wartosc)
        {
            var t = new TextBox { Text = wartosc, ToolTip = "Dwuklik – bieżąca godzina; kółko myszy – ± minuta (prawa połowa) / ± godzina (lewa połowa)" };
            var nd = new CheckBox { ToolTip = "Nie dotyczy (---)", VerticalAlignment = VerticalAlignment.Center };
            nd.Checked += (a, e) => { t.Text = "---"; t.IsEnabled = false; };
            nd.Unchecked += (a, e) => { t.Text = ""; t.IsEnabled = true; };
            t.MouseDoubleClick += (a, e) => t.Text = okno.Teraz;
            t.MouseWheel += (a, e) => { e.Handled = true; bool h = e.GetPosition(t).X < t.ActualWidth / 2; int k = e.Delta > 0 ? 1 : -1; t.Text = EdrWindow.Przesun(t.Text, h ? k : 0, h ? 0 : k, okno.Teraz); };
            Wiersz(etykieta, t, nd);
            return t;
        }
        var dw = Godz("Dw", "Droga wolna", "");
        var odj = Godz("Odj", "Pociąg odjechał", wstepne?.Odj ?? okno.Teraz);
        if (wstepne != null) { nr.Text = wstepne.Nr; tor.Text = wstepne.Tor; }
        var prz = Godz("Prz", "Pociąg przyjechał", "");
        var lewy = new CheckBox { Content = "Po torze lewym", Margin = new Thickness(0, 4, 0, 0) };
        if (d.Tory.Count > 1) Wiersz("", lewy);
        // jazda do km i z powrotem (podręcznik EDR rozdz. 7.5): dwa wiersze w klamrze
        var powrot = new CheckBox { Content = przyj ? "Powrót ze szlaku" : "Wprowadź powrót ze szlaku", VerticalAlignment = VerticalAlignment.Center };
        var km = new TextBox { Width = 70, IsEnabled = false, Margin = new Thickness(6, 0, 0, 0), ToolTip = "Kilometr, do którego jedzie pociąg (np. 57,987)" };
        powrot.Checked += (a, e) => { km.IsEnabled = true; km.Focus(); }; powrot.Unchecked += (a, e) => km.IsEnabled = false;
        var pp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        pp.Children.Add(powrot); pp.Children.Add(new TextBlock { Text = "do km", Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }); pp.Children.Add(km);
        Wiersz("", pp);
        sp.Children.Add(g);
        sp.Children.Add(new TextBlock { Text = "Uwagi", Margin = new Thickness(0, 6, 0, 2) });
        var uw = new TextBox { Height = 70, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
        sp.Children.Add(uw);
        var blad = new TextBlock { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
        sp.Children.Add(blad);

        // pola wymagane – niebieskie, dopóki puste
        var wymagane = new List<(Control Pole, Func<string> Wartosc)>();
        if (wym.Contains("Nr")) wymagane.Add((nr, () => nr.Text));
        if (wym.Contains("Tor")) wymagane.Add((tor, () => tor.Text));
        if (wym.Contains("Dw")) wymagane.Add((dw, () => dw.Text));
        if (wym.Contains("Odj")) wymagane.Add((odj, () => odj.Text));
        if (wym.Contains("Prz")) wymagane.Add((prz, () => prz.Text));
        void Koloruj() { foreach (var (p, v) in wymagane) p.Background = v().Trim() == "" ? EdrWindow.Brak : Brushes.White; }
        foreach (var t in new[] { nr, dw, odj, prz }) t.TextChanged += (a, e) => Koloruj();
        tor.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, new TextChangedEventHandler((a, e) => Koloruj()));
        tor.SelectionChanged += (a, e) => Dispatcher.BeginInvoke(Koloruj);
        Loaded += (a, e) => { Koloruj(); nr.Focus(); };

        var bp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };
        var zapisz = new Button { Content = "Zapisz", Width = 100, Margin = new Thickness(4), IsDefault = true };
        var anuluj = new Button { Content = "Anuluj", Width = 100, Margin = new Thickness(4), IsCancel = true };
        zapisz.Click += (a, e) =>
        {
            var r = okno.Wyslij(new EdrPolecenie
            {
                Dziennik = d.Id, Rodzaj = rodzaj, Pole = powrot.IsChecked == true ? "powrot" : null, Wartosc = km.Text,
                Dane = new EdrWersja { Nr = nr.Text, Tor = tor.Text, Dw = dw.Text, Odj = odj.Text, Prz = prz.Text, Uwagi = uw.Text, Lewy = lewy.IsChecked == true }
            });
            if (r.Ok) Close(); else blad.Text = r.Msg;
        };
        bp.Children.Add(zapisz); bp.Children.Add(anuluj);
        sp.Children.Add(bp);
        Content = sp;
    }
}

using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Lcs.Lacznosc;

/// <summary>
/// Terminal stacji stycznej z EZP (styl EbiScreen / MOR): u góry pulpit – nasza stacja (sześciokąt) i szlaki do posterunków
/// sąsiednich z okienkami numerów pociągów w kolorze stanu EZP; na dole zakładki Alarmy / Zdarzenia / Plan ruchu / EZP.
/// Rozmowa w osobnym oknie. Automaty (awizacja, odjazd, przyjazd) – w Opcjach, domyślnie wyłączone.
/// </summary>
public sealed class MainWindow : Window
{
    // ------------------------------------------------------------------ dane
    readonly MostKlient _most;
    readonly ObservableCollection<Sasiad> _sasiedzi = new();
    readonly ObservableCollection<Wpis> _wpisy = new();
    readonly ObservableCollection<Rodzaj> _rodzaje = new();
    readonly ObservableCollection<PozycjaPlanu> _plan = new();
    readonly ObservableCollection<string> _zdarzenia = new(), _alarmy = new();
    readonly Dictionary<long, Wpis> _wyslane = new();
    readonly Dictionary<string, List<string>> _wStacji = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> _zgody = new(StringComparer.OrdinalIgnoreCase);
    readonly StanSzlakow _szlaki = new();
    List<RozkladSimRail.Pociag> _rozklad = new();
    string _posterunek = "", _stacjaGry = "", _czasGry = "";
    DateTime _rozkladPobrany = DateTime.MinValue;
    bool _pobieranie;
    readonly bool _demo;
    readonly Ustawienia _ust;

    // automaty
    readonly Dictionary<string, int> _gdzie = new();          // pociąg → 0 brak, 1 szlak, 2 stacja (z poprzedniego stanu)
    bool _pierwszyStan = true;
    readonly HashSet<string> _awizowane = new();
    DateTime _ostatnieApi = DateTime.MinValue, _ostatniaAwizacjaMin = DateTime.MinValue;
    bool _api;

    // ------------------------------------------------------------------ kontrolki
    readonly SchematSzlakow _schemat = new();
    readonly TextBlock _tytul = new(), _zegar = new(), _status = new(), _planStatus = new();
    readonly ComboBox _cbPosterunek = new(), _cbSerwer = new(), _cbPowod = new();
    readonly ListBox _lbAlarmy = new(), _lbZdarzenia = new();
    readonly DataGrid _dgPlan = new();
    readonly Dictionary<string, (Border Naglowek, UIElement Tresc)> _zakladki = new();

    // EZP
    string _wybrany;                       // posterunek w panelu EZP
    string _klasa = "TrainRequestMessage"; // wybrany rodzaj komunikatu („STOJ” – stój jako tekst)
    Wpis _pytanie;
    bool _torRecznie;
    readonly TextBlock _ezpTytul = new(), _ostatni = new(), _podglad = new(), _wynik = new();
    readonly Border _ostatniRamka = new();
    readonly StackPanel _odpPrzyciski = new();
    readonly WrapPanel _pocPrzyciski = new();
    readonly UniformGrid _rodzPrzyciski = new();
    readonly TextBox _tbPociag = new(), _tbCzas = new(), _tbTekst = new();
    readonly WyborToru _tor = new();
    readonly Para _wierszPowod, _wierszCzas;
    readonly Button _btnWyslij;
    OknoRozmowy _oknoRozmowy;

    /// <summary>Etykieta i pole jednego wiersza formularza – chowane razem.</summary>
    sealed record Para(FrameworkElement Etykieta, FrameworkElement Pole)
    {
        public void Pokaz(bool tak) { var v = tak ? Visibility.Visible : Visibility.Collapsed; Etykieta.Visibility = v; Pole.Visibility = v; }
    }

    public MainWindow(bool demo)
    {
        _demo = demo;
        _ust = demo ? new Ustawienia() : Ustawienia.Wczytaj();
        Title = "Terminal Stacji Stycznej – EZP (SimRail)";
        try { Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/app.ico")); }
        catch (Exception ex) { try { File.AppendAllText(Program.ErrorLog, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} ikona okna: {ex.Message}{Environment.NewLine}"); } catch { } }
        Width = 1400; Height = 900; MinWidth = 1000; MinHeight = 680;
        Background = Brushes.Black; Foreground = Wyglad.Tekst; FontFamily = Wyglad.Mono; FontSize = 12;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var glowna = new Grid();
        glowna.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        glowna.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        glowna.RowDefinitions.Add(new RowDefinition { Height = new GridLength(340) });
        Content = glowna;

        // ---- belka górna
        var belka = new DockPanel { Background = Wyglad.BelkaGorna, Height = 30, LastChildFill = true };
        var tahoma = new FontFamily("Tahoma");
        belka.Children.Add(new Border
        {
            Background = Wyglad.P("#B0B0B0"), Padding = new Thickness(10, 0, 10, 0), BorderBrush = Wyglad.P("#777777"), BorderThickness = new Thickness(0, 0, 1, 0),
            Child = new TextBlock { Text = "Posterunek", Foreground = Brushes.Black, FontFamily = tahoma, VerticalAlignment = VerticalAlignment.Center },
        });
        _cbPosterunek.IsEditable = true; _cbPosterunek.Width = 210; _cbPosterunek.Margin = new Thickness(6, 4, 6, 4); _cbPosterunek.FontFamily = tahoma;
        _cbPosterunek.ToolTip = "Posterunek, dla którego działa terminal. „(z gry)” – ten, który obsługujesz w grze. Inny – podgląd (plan, szlaki z rozkładu); wysyłanie tylko z posterunku w grze.";
        _cbPosterunek.Text = string.IsNullOrEmpty(_ust.Stacja) ? ZGry : _ust.Stacja;
        _cbPosterunek.SelectionChanged += (_, _) => Dispatcher.BeginInvoke(ZmianaPosterunku);
        _cbPosterunek.LostKeyboardFocus += (_, _) => ZmianaPosterunku();
        _cbPosterunek.KeyDown += (_, e) => { if (e.Key == Key.Enter) ZmianaPosterunku(); };
        belka.Children.Add(_cbPosterunek);
        var zegarBox = new Border { Background = Brushes.Black, Padding = new Thickness(10, 0, 10, 0), Child = _zegar };
        _zegar.Foreground = Brushes.White; _zegar.FontSize = 16; _zegar.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(zegarBox, Dock.Right); belka.Children.Add(zegarBox);
        void Prawy(FrameworkElement e) { e.Margin = new Thickness(0, 4, 6, 4); DockPanel.SetDock(e, Dock.Right); belka.Children.Add(e); }
        Prawy(Wyglad.Przycisk("Opcje…", Opcje, "Awizacja automatyczna, zgłoszenia z gry, posterunki objęte automatami"));
        Prawy(Wyglad.Przycisk("Rozmowa", () => PokazRozmowe(null), "Osobne okno rozmowy z posterunkami (godziny, pełne komunikaty gry)"));
        Prawy(Wyglad.Przycisk("Pobierz rozkład", async () => await PobierzRozklad()));
        _cbSerwer.IsEditable = true; _cbSerwer.Width = 80; _cbSerwer.Text = _ust.Serwer ?? ""; _cbSerwer.FontFamily = tahoma;
        _cbSerwer.ToolTip = "Kod serwera SimRail (np. pl1) – rozkład i pozycje pociągów z publicznego API SimRail";
        Prawy(_cbSerwer);
        Prawy(new TextBlock { Text = "Serwer", Foreground = Brushes.Black, FontFamily = tahoma, VerticalAlignment = VerticalAlignment.Center });
        _status.FontFamily = tahoma; _status.VerticalAlignment = VerticalAlignment.Center;
        Prawy(_status);
        _tytul.FontFamily = tahoma; _tytul.FontSize = 13; _tytul.Foreground = Brushes.Black; _tytul.HorizontalAlignment = HorizontalAlignment.Center; _tytul.VerticalAlignment = VerticalAlignment.Center;
        belka.Children.Add(new Border { Background = Wyglad.P("#F2F2F2"), BorderBrush = Wyglad.P("#999999"), BorderThickness = new Thickness(1), Margin = new Thickness(4, 4, 10, 4), Child = _tytul });
        glowna.Children.Add(belka);

        // ---- pulpit
        _schemat.WybranoSasiada += s => WybierzSasiada(s, zakladkaEzp: true);
        _schemat.UstawStrone += (s, lewa) => { _ust.Strony[Klucz(s)] = lewa; ZapiszUst(); OdswiezSchemat(); };
        _schemat.UstawTory += UstawToryRecznie;
        _schemat.WybranoPociag += (s, nr) => { WybierzSasiada(s, zakladkaEzp: true); _tbPociag.Text = nr; };
        _schemat.AnulujPociag += AnulujPociag;
        _schemat.Licznik = s => (Pytania(s), _sasiedzi.FirstOrDefault(x => x.Nazwa == s)?.Oczekujace ?? 0,
                                 (_ust.Awizacja || _ust.AutoOdjazd || _ust.AutoPrzyjazd) && _ust.Automat(s));
        Grid.SetRow(_schemat, 1); glowna.Children.Add(_schemat);
        _szlaki.NowyTor += (s, t) =>
        {
            var k = Klucz(s);
            if (!_ust.Tory.TryGetValue(k, out var l)) _ust.Tory[k] = l = new();
            if (!l.Contains(t)) { l.Add(t); l.Sort(); ZapiszUst(); }
        };

        // ---- zakładki
        var dol = new DockPanel { Background = Brushes.Black };
        var naglowki = new StackPanel { Orientation = Orientation.Horizontal, Background = Wyglad.P("#10141C") };
        var tresci = new Grid { Margin = new Thickness(8, 6, 8, 6) };
        DockPanel.SetDock(naglowki, Dock.Top); dol.Children.Add(naglowki); dol.Children.Add(tresci);
        var dolRamka = new Border { BorderBrush = Wyglad.Cyjan, BorderThickness = new Thickness(0, 2, 0, 0), Child = dol };
        Grid.SetRow(dolRamka, 2); glowna.Children.Add(dolRamka);

        Wyglad.Lista(_lbAlarmy); _lbAlarmy.ItemsSource = _alarmy; _lbAlarmy.Foreground = Wyglad.Czerwony;
        Wyglad.Lista(_lbZdarzenia); _lbZdarzenia.ItemsSource = _zdarzenia;
        ZbudujTabele();
        var planDok = new DockPanel();
        _planStatus.Foreground = Wyglad.Szary; _planStatus.Margin = new Thickness(0, 0, 0, 4);
        DockPanel.SetDock(_planStatus, Dock.Top); planDok.Children.Add(_planStatus); planDok.Children.Add(_dgPlan);

        // EZP: trzy ramki obok siebie – Nadawanie (formularz) | Odebrane (ostatni komunikat, odpowiedzi) | Do gry (podgląd, wysyłka).
        // Wszystkie przyciski i pola mają jedną wysokość (Wyglad.H), odstępy co 6 px.
        var ezp = new Grid();
        ezp.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(9, GridUnitType.Star) });
        ezp.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5, GridUnitType.Star) });
        ezp.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5, GridUnitType.Star) });
        void Ramka(int kol, UIElement naglowek, UIElement tresc)
        {
            var d = new DockPanel();
            var n = new Border { Background = Wyglad.NaglowekRamki, Padding = new Thickness(8, 3, 8, 3), Child = naglowek };
            DockPanel.SetDock(n, Dock.Top); d.Children.Add(n);
            d.Children.Add(new Border { Padding = new Thickness(10, 8, 10, 8), Child = tresc });
            var r = new Border { BorderBrush = Wyglad.RamkaPanelu, BorderThickness = new Thickness(1), Margin = new Thickness(kol == 0 ? 0 : 6, 0, 0, 0), Child = d };
            Grid.SetColumn(r, kol); ezp.Children.Add(r);
        }
        TextBlock Naglowek(TextBlock t, string tekst = null)
        {
            if (tekst != null) t.Text = tekst;
            t.Foreground = Wyglad.Cyjan; t.FontWeight = FontWeights.Bold; t.TextTrimming = TextTrimming.CharacterEllipsis;
            return t;
        }

        // -- Nadawanie
        var form = new Grid();
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
        form.ColumnDefinitions.Add(new ColumnDefinition());
        Para Wiersz(string et, FrameworkElement pole)
        {
            int w = form.RowDefinitions.Count;
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = Wyglad.Etykieta(et); l.Height = Wyglad.H; l.VerticalAlignment = VerticalAlignment.Top; l.Padding = new Thickness(0, 5, 0, 0);
            Grid.SetRow(l, w); form.Children.Add(l);
            pole.Margin = new Thickness(0, 0, 0, 6); pole.HorizontalAlignment = HorizontalAlignment.Left;
            Grid.SetRow(pole, w); Grid.SetColumn(pole, 1); form.Children.Add(pole);
            return new Para(l, pole);
        }

        Wyglad.Pole(_tbPociag); _tbPociag.Width = 90; _tbPociag.FontSize = 15; _tbPociag.Height = Wyglad.H; _tbPociag.VerticalAlignment = VerticalAlignment.Top;
        _tbPociag.ToolTip = "Numer pociągu – dowolny, także spoza rozkładu; przyciski obok – pociągi tego szlaku z rozkładu i ze szlaku";
        _tbPociag.TextChanged += (_, _) => { TorZNumeru(); Podglad(); };
        var poc = new DockPanel(); DockPanel.SetDock(_tbPociag, Dock.Left); poc.Children.Add(_tbPociag);
        _pocPrzyciski.Margin = new Thickness(6, 0, 0, -6); poc.Children.Add(_pocPrzyciski);
        Wiersz("Pociąg", poc);
        _tor.Zmieniono += () => { _torRecznie = true; Podglad(); };
        _tor.ToolTip = "Tor szlakowy w komunikacie (tory tego szlaku z komunikatów gry); Alt+0 – bez, Alt+1… – kolejne";
        Wiersz("Tor", _tor);
        _rodzPrzyciski.Columns = 5; _rodzPrzyciski.Margin = new Thickness(0, 0, 0, 0);
        var rodz = new Border { Child = _rodzPrzyciski };
        var wr = Wiersz("Komunikat", rodz); rodz.Margin = new Thickness(0, 0, -4, 2); rodz.HorizontalAlignment = HorizontalAlignment.Stretch; rodz.MaxWidth = 5 * 130;
        _cbPowod.ItemsSource = Teksty.Powody.Select(x => x.Opis).ToList(); _cbPowod.SelectedIndex = 0; _cbPowod.Width = 320; _cbPowod.Height = Wyglad.H;
        _cbPowod.VerticalContentAlignment = VerticalAlignment.Center;
        _cbPowod.SelectionChanged += (_, _) => Podglad();
        _wierszPowod = Wiersz("Powód", _cbPowod);
        Wyglad.Pole(_tbCzas); _tbCzas.Width = 70; _tbCzas.Height = Wyglad.H;
        _tbCzas.TextChanged += (_, _) => Podglad();
        _wierszCzas = Wiersz("Godzina", _tbCzas);
        var tekstWiersz = new DockPanel { MaxWidth = 5 * 130 - 4 };
        var wyslijTekst = Wyglad.Przycisk("Wyślij tekst", WyslijTekst, "Tekst własny do tego posterunku (Enter)");
        wyslijTekst.Margin = new Thickness(6, 0, 0, 0); wyslijTekst.Width = 120;
        DockPanel.SetDock(wyslijTekst, Dock.Right); tekstWiersz.Children.Add(wyslijTekst);
        Wyglad.Pole(_tbTekst); _tbTekst.Foreground = Brushes.White; _tbTekst.FontFamily = tahoma; _tbTekst.Height = Wyglad.H;
        _tbTekst.KeyDown += (_, e) => { if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None) { WyslijTekst(); e.Handled = true; } };
        tekstWiersz.Children.Add(_tbTekst);
        Wiersz("Tekst", tekstWiersz); tekstWiersz.HorizontalAlignment = HorizontalAlignment.Stretch;
        Ramka(0, Naglowek(_ezpTytul), form);

        // -- Odebrane
        var odebrane = new StackPanel();
        _ostatni.TextWrapping = TextWrapping.Wrap; _ostatni.FontSize = 13;
        _ostatniRamka.Background = Wyglad.P("#080C12");
        _ostatniRamka.BorderBrush = Wyglad.P("#404040"); _ostatniRamka.BorderThickness = new Thickness(1); _ostatniRamka.Padding = new Thickness(8, 5, 8, 5);
        _ostatniRamka.Margin = new Thickness(0, 0, 0, 8); _ostatniRamka.MinHeight = 48; _ostatniRamka.Child = _ostatni;
        odebrane.Children.Add(_ostatniRamka);
        odebrane.Children.Add(Wyglad.Etykieta("Odpowiedz:"));
        _odpPrzyciski.Margin = new Thickness(0, 4, 0, 0);
        odebrane.Children.Add(_odpPrzyciski);
        Ramka(1, Naglowek(new TextBlock(), "ODEBRANE – ostatni komunikat"), odebrane);

        // -- Do gry
        var doGry = new DockPanel();
        _btnWyslij = Wyglad.Przycisk("WYŚLIJ   Ctrl+Enter", Wyslij);
        _btnWyslij.Height = 34; _btnWyslij.FontSize = 14; _btnWyslij.FontWeight = FontWeights.Bold; _btnWyslij.Margin = new Thickness(0, 0, 0, 6);
        _btnWyslij.Background = Wyglad.P("#1F5A2A");
        var anuluj = Wyglad.Przycisk("Anuluj pociąg (bez komunikatu)", () => AnulujPociag(_wybrany, _tbPociag.Text.Trim()),
            "Zdejmuje pociąg z okienka tego szlaku bez wysyłania komunikatu (np. pomyłka, pociąg usunięty z gry)");
        anuluj.Margin = new Thickness(0);
        var wysylka = new StackPanel(); wysylka.Children.Add(_btnWyslij); wysylka.Children.Add(anuluj);
        DockPanel.SetDock(wysylka, Dock.Bottom); doGry.Children.Add(wysylka);
        var tresc = new StackPanel();
        var podRamka = new Border
        {
            Background = Wyglad.P("#080C12"), BorderBrush = Wyglad.P("#404040"), BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 5, 8, 5), MinHeight = 48, Margin = new Thickness(0, 0, 0, 8), Child = _podglad,
        };
        _podglad.Foreground = Wyglad.Zolty; _podglad.TextWrapping = TextWrapping.Wrap; _podglad.FontSize = 13;
        tresc.Children.Add(podRamka);
        _wynik.TextWrapping = TextWrapping.Wrap; _wynik.Foreground = Wyglad.Szary;
        tresc.Children.Add(_wynik);
        doGry.Children.Add(tresc);
        Ramka(2, Naglowek(new TextBlock(), "DO GRY – podgląd komunikatu"), doGry);

        Zakladka("Alarmy", _lbAlarmy);
        Zakladka("Zdarzenia", _lbZdarzenia);
        Zakladka("Plan ruchu", planDok);
        Zakladka("EZP", ezp);
        PokazZakladke("EZP");

        void Zakladka(string nazwa, UIElement tresc)
        {
            var n = new Border
            {
                Padding = new Thickness(16, 3, 16, 3), Cursor = Cursors.Hand, BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 0, 1, 0),
                Child = new TextBlock { Text = nazwa, FontFamily = tahoma },
            };
            n.MouseLeftButtonUp += (_, _) => PokazZakladke(nazwa);
            naglowki.Children.Add(n);
            tresci.Children.Add(tresc);
            _zakladki[nazwa] = (n, tresc);
        }

        // ---- zdarzenia
        foreach (var k in Teksty.Klasy) _rodzaje.Add(new Rodzaj { Klasa = k, Arg = Teksty.DomyslneArg(k) });
        ZbudujRodzaje();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { Wyslij(); e.Handled = true; return; }
            if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.System && e.SystemKey is >= Key.D0 and <= Key.D9)
            { _tor.WybierzNr(e.SystemKey - Key.D0, reka: true); e.Handled = true; }
        };
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        t.Tick += (_, _) => Takt();
        t.Start();
        Closed += (_, _) =>
        {
            if (!_demo) { _ust.Serwer = _cbSerwer.Text; _ust.Zapisz(); }
            if (_oknoRozmowy != null) { _oknoRozmowy.Zamykanie = true; _oknoRozmowy.Close(); }
            _most?.Dispose();
        };

        if (demo) Demo.Wypelnij(this);
        else
        {
            _most = new MostKlient();
            _most.Pakiet += p => Dispatcher.BeginInvoke(() => Przyjmij(p));
            Loaded += async (_, _) => await WczytajSerwery();
        }
        Takt();
        OdswiezSchemat();
        OdswiezEzp();
    }

    void PokazZakladke(string nazwa)
    {
        foreach (var (n, (nag, tr)) in _zakladki)
        {
            bool on = n == nazwa;
            nag.Background = on ? Wyglad.Cyjan : Wyglad.P("#1E2430");
            ((TextBlock)nag.Child).Foreground = on ? Brushes.White : Wyglad.P("#9AA4B0");
            tr.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // ------------------------------------------------------------------ posterunek (wybór stacji)

    const string ZGry = "(z gry)";

    /// <summary>Posterunek terminala: wybrany w belce albo obsługiwany w grze.</summary>
    string Stacja => !string.IsNullOrEmpty(_ust.Stacja) ? _ust.Stacja : !string.IsNullOrEmpty(_posterunek) ? _posterunek : _stacjaGry;

    /// <summary>Wybrany posterunek to ten z gry (wtedy sąsiedzi z gry i można wysyłać).</summary>
    bool StacjaZGry => string.IsNullOrEmpty(_ust.Stacja) || string.IsNullOrEmpty(_posterunek) || RozkladSimRail.Pasuje(_ust.Stacja, _posterunek);

    void ZmianaPosterunku()
    {
        var t = (_cbPosterunek.SelectedItem as string ?? _cbPosterunek.Text ?? "").Trim();
        var nowa = t == ZGry ? "" : t;
        if (nowa == (_ust.Stacja ?? "")) return;
        _ust.Stacja = nowa; ZapiszUst();
        _szlaki.Wyczysc(); _wybrany = null;
        ZbudujPlan(); OdswiezSchemat(); OdswiezEzp();
    }

    /// <summary>Posterunki sąsiednie: z gry (posterunek z gry), inaczej z rozkładu – punkty tuż przed i za stacją.</summary>
    List<string> Sasiedzi()
    {
        if (StacjaZGry && _sasiedzi.Count > 0) return _sasiedzi.Select(s => s.Nazwa).ToList();
        var st = Stacja;
        if (string.IsNullOrEmpty(st)) return new();
        var licz = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in _rozklad)
        {
            int i = t.Punkty.FindIndex(p => RozkladSimRail.Pasuje(p.Nazwa, st));
            if (i < 0) continue;
            int k = i; while (k + 1 < t.Punkty.Count && RozkladSimRail.Pasuje(t.Punkty[k + 1].Nazwa, st)) k++;
            if (i > 0) licz[t.Punkty[i - 1].Nazwa] = licz.GetValueOrDefault(t.Punkty[i - 1].Nazwa) + 1;
            if (k + 1 < t.Punkty.Count) licz[t.Punkty[k + 1].Nazwa] = licz.GetValueOrDefault(t.Punkty[k + 1].Nazwa) + 1;
        }
        return licz.Where(x => x.Value >= 2).OrderByDescending(x => x.Value).Take(8).Select(x => x.Key).ToList();
    }

    string Klucz(string sasiad) => $"{Stacja}|{sasiad}";

    // ------------------------------------------------------------------ pakiety z moda

    internal void Przyjmij(JsonElement p)
    {
        switch (MostKlient.S(p, "typ"))
        {
            case "lacznosc_stan": Stan(p); break;
            case "czat": Czat(p); break;
            case "odp": Odpowiedz(p); break;
        }
    }

    void Stan(JsonElement p)
    {
        var post = MostKlient.S(p, "posterunek") ?? "";
        bool zmiana = post != _posterunek;
        _posterunek = post;
        var st = MostKlient.S(p, "stacja") ?? "";
        bool zmianaStacji = st != _stacjaGry; _stacjaGry = st;
        _czasGry = MostKlient.S(p, "czasGry") ?? "";
        bool nowiSasiedzi = false;
        if (p.TryGetProperty("sasiedzi", out var ss))
            foreach (var s in ss.EnumerateArray())
            {
                var n = MostKlient.S(s, "n"); if (string.IsNullOrEmpty(n)) continue;
                var gracz = s.TryGetProperty("gracz", out var g) && g.GetBoolean();
                var juz = _sasiedzi.FirstOrDefault(x => x.Nazwa == n);
                if (juz == null) { _sasiedzi.Add(new Sasiad { Nazwa = n, Gracz = gracz }); nowiSasiedzi = true; } else juz.Gracz = gracz;
            }
        if (p.TryGetProperty("szablony", out var sz) && sz.GetArrayLength() > 0)
        {
            var nowe = sz.EnumerateArray().Select(x => new Rodzaj
            {
                Klasa = MostKlient.S(x, "klasa"), Szablon = MostKlient.S(x, "tekst"), ZGry = true,
                Arg = x.TryGetProperty("arg", out var a) ? a.EnumerateArray().Select(v => v.GetString()).ToArray() : Array.Empty<string>(),
            }).ToList();
            if (!_rodzaje.Select(r => r.Klasa + r.Szablon + string.Join(",", r.Arg)).SequenceEqual(nowe.Select(r => r.Klasa + r.Szablon + string.Join(",", r.Arg))))
            {
                _rodzaje.Clear(); foreach (var r in nowe) _rodzaje.Add(r);
                ZbudujRodzaje();
            }
        }
        _wStacji.Clear();
        if (p.TryGetProperty("pociagi", out var pp))
            foreach (var x in pp.EnumerateArray())
            {
                var nr = MostKlient.S(x, "nr"); if (string.IsNullOrEmpty(nr)) continue;
                _wStacji[nr] = x.TryGetProperty("gdzie", out var gd) ? gd.EnumerateArray().Select(v => v.GetString()).ToList() : new();
            }
        foreach (var r in _plan) r.WStacji = WStacji(r.Pociag);
        if (zmiana || zmianaStacji || nowiSasiedzi) { ZbudujPlan(); OdswiezSchemat(); OdswiezEzp(); }
        bool zGry = ZajetosciZGry();   // przed automatami – odjazd bierze kierunek i tor z szlaku, na który wjechał pociąg
        Automaty();
        zGry |= ZjechalyZeSzlaku();
        if (zGry) { OdswiezSchemat(); OdswiezEzp(); }
    }

    // ------------------------------------------------------------------ zajętości z gry: pociągi w stacji i na szlakach

    List<string> _pociagiStacji = new();
    string _podpisZGry = "";

    /// <summary>Szlak z nazwy odcinka: „l1_lb_lc_it4” (skróty obu stacji, tor 4) albo „l12_it_sk_pm_1”. Zwraca skróty i numer.</summary>
    static (string A, string B, int Nr)? SzlakZeSkrotami(string n)
    {
        var m = Regex.Match(n ?? "", @"^\d*l\d+_([a-z]+)_([a-z]+)_it(\d+)$", RegexOptions.IgnoreCase);
        if (!m.Success) m = Regex.Match(n ?? "", @"^\d*l\d+_it_([a-z]+)_([a-z]+)_(\d+)$", RegexOptions.IgnoreCase);
        return m.Success && int.TryParse(m.Groups[3].Value, out var nr) ? (m.Groups[1].Value.ToLowerInvariant(), m.Groups[2].Value.ToLowerInvariant(), nr) : null;
    }

    static string BezOgonkow(string s)
    {
        var d = (s ?? "").Replace('Ł', 'L').Replace('ł', 'l').Normalize(System.Text.NormalizationForm.FormD);
        return new string(d.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant();
    }

    static bool Podciag(string skrot, string w)
    {
        if (string.IsNullOrEmpty(w) || w[0] != skrot[0]) return false;
        int j = 0; foreach (var c in w) if (j < skrot.Length && c == skrot[j]) j++;
        return j == skrot.Length;
    }

    /// <summary>Sąsiad o skrócie z nazwy odcinka: „lc” → „Łazy Łc”, „pm” → „Puszcza Mariańska”, „zw” → „Zawiercie”. Tylko jednoznacznie.</summary>
    string SasiadZeSkrotu(string skrot)
    {
        bool Pasuje(string nazwa)
        {
            var slowa = BezOgonkow(nazwa).Split(new[] { ' ', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (slowa.Length == 0) return false;
            if (string.Concat(slowa.Select(w => w[0])) == skrot) return true;
            return Podciag(skrot, slowa[^1]) || (slowa.Length == 1 && Podciag(skrot, slowa[0]));
        }
        var k = Sasiedzi().Where(Pasuje).ToList();
        return k.Count == 1 ? k[0] : null;
    }

    /// <summary>
    /// Pociągi w stacji (na odcinkach stacyjnych) i na szlakach (odcinki ze skrótami obu stacji) wg ostatniego stanu z gry.
    /// Tor z nazwy odcinka bywa nazwą toru (Łazy–Łazy Łc 1–4, Łazy Ła 123), ale nie zawsze („l1_la_lb_it324” to tor 224 w czacie) –
    /// schemat bierze go tylko, gdy jest wśród torów szlaku. Zwraca true, gdy coś się zmieniło.
    /// </summary>
    bool ZajetosciZGry()
    {
        var stacji = new List<string>();
        var szlaki = new Dictionary<string, List<(string, int?, bool)>>(StringComparer.OrdinalIgnoreCase);
        if (StacjaZGry)
        {
            // własny skrót: najczęstszy w odcinkach stacyjnych („l1_lb_iz…” → „lb”)
            var wlasny = _wStacji.Values.SelectMany(g => g).Where(ObiektStacji)
                .Select(o => Regex.Match(o, @"^(?:l\d+|\d+)_([a-z]+)_", RegexOptions.IgnoreCase)).Where(m => m.Success)
                .GroupBy(m => m.Groups[1].Value.ToLowerInvariant()).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
            if (!_ust.OdcinkiSasiadow.TryGetValue(Stacja ?? "", out var poznane)) poznane = null;
            bool zapisz = false;
            void Ucz(string o, string sasiad, int? tor)
            {
                if (poznane == null) _ust.OdcinkiSasiadow[Stacja] = poznane = new(StringComparer.OrdinalIgnoreCase);
                var nowy = $"{sasiad}|{tor}";
                if (poznane.TryGetValue(o, out var stary) && stary == nowy) return;
                poznane[o] = nowy; zapisz = true;
                Zdarzenie($"{Teraz():HH:mm}  odcinek {o} → szlak do {sasiad}{(tor is int t ? $", tor {t}" : "")} (poznany)");
            }
            (string Sasiad, int? Tor)? Poznany(string o)
            {
                if (poznane == null || !poznane.TryGetValue(o, out var wpis)) return null;
                var cz = wpis.Split('|');
                return (cz[0], cz.Length > 1 && int.TryParse(cz[1], out var t) ? t : null);
            }
            foreach (var (nr, g) in _wStacji.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                bool wSt = g.Any(ObiektStacji);
                if (wSt) stacji.Add(nr);
                // kierunek na odcinku: 3 – odjechał (od nas); 2 – w stacji: od nas, chyba że to ogon z wjazdu; 1 – dojeżdża do nas;
                // 0 – nieznany (start programu): od nas, gdy już stoi w stacji
                int gd = _gdzie.GetValueOrDefault(nr);
                var ogonWj = _ogonPrzyjazdu.GetValueOrDefault(nr);
                var nieznane = new List<(string O, bool OdNas)>();
                foreach (var o in g)
                {
                    bool odNas = gd == 3 || (gd == 2 && !(ogonWj?.Contains(o) ?? false)) || (gd == 0 && wSt);
                    string sasiad = null; int? tor = null;
                    if (SzlakZeSkrotami(o) is { } sz)
                    {
                        var obcy = sz.A == wlasny ? sz.B : sz.B == wlasny ? sz.A : null;
                        sasiad = obcy != null ? SasiadZeSkrotu(obcy) : SasiadZeSkrotu(sz.A) ?? SasiadZeSkrotu(sz.B);
                        tor = sz.Nr;
                        if (sasiad != null)
                        {
                            // numer w nazwie nie jest torem szlaku („l1_la_lb_it324” = tor 224): poznany albo z czatu dla tego pociągu
                            var tory = _szlaki.Daj(sasiad).Tory;
                            if (tory.Count > 0 && !tory.Contains(sz.Nr))
                            {
                                tor = Poznany(o)?.Tor ?? (OdcinekTorZnany.TryGetValue(o, out var tz) ? tz : null);
                                if (tor == null && TorZCzatu(sasiad, nr) is int tc && tory.Contains(tc)) { Ucz(o, sasiad, tc); tor = tc; }
                            }
                        }
                    }
                    else if (ObiektSzlaku(o))
                    {
                        // odcinek bez skrótów („l1_it2790”, „t…”): zapamiętany sąsiad albo poznany teraz z komunikatu / rozkładu
                        var zKom = _szlaki.Wszystkie.Where(s => s.Pociagi.Any(p => p.Nr == nr && p.OdNas == odNas && p.Stan != StanOkna.Odmowa)).ToList();
                        var zPlanu = _plan.FirstOrDefault(r => r.Pociag == nr) is { } r ? (odNas ? r.SasiadDokad : r.SasiadSkad) : null;
                        string nowy = zKom.Count == 1 ? zKom[0].Sasiad : zKom.Count == 0 && zPlanu != null && Sasiedzi().Contains(zPlanu) ? zPlanu : null;
                        int? torKom = nowy != null ? TorZCzatu(nowy, nr) : null;
                        if (Poznany(o) is { } pz)
                        {
                            (sasiad, tor) = pz;
                            if (tor == null && torKom != null && nowy == sasiad) { Ucz(o, sasiad, torKom); tor = torKom; }
                        }
                        else if (nowy != null) { Ucz(o, nowy, torKom); sasiad = nowy; tor = torKom; }
                        else if (poznane != null && Regex.Match(o, @"^(\d*l\d+_it)(\d+)$", RegexOptions.IgnoreCase) is { Success: true } mo)
                        {
                            // sąsiedni odcinek tej samej linii (kilometraż w nazwie, ±40): „l1_it2790” obok poznanego „l1_it2799” – ten sam szlak
                            int km = int.Parse(mo.Groups[2].Value);
                            var blisko = poznane.Select(kv => (kv, m: Regex.Match(kv.Key, @"^(\d*l\d+_it)(\d+)$", RegexOptions.IgnoreCase)))
                                .Where(x => x.m.Success && string.Equals(x.m.Groups[1].Value, mo.Groups[1].Value, StringComparison.OrdinalIgnoreCase)
                                            && Math.Abs(int.Parse(x.m.Groups[2].Value) - km) <= 40)
                                .Select(x => x.kv.Value.Split('|')[0]).Distinct().ToList();
                            if (blisko.Count == 1) sasiad = blisko[0];
                        }
                        if (sasiad == null) nieznane.Add((o, odNas));
                        else tor ??= TorOdcinka(o, sasiad);
                    }
                    if (sasiad == null) continue;
                    if (odNas) _dokad[nr] = sasiad; else _skad[nr] = sasiad;
                    if (!szlaki.TryGetValue(sasiad, out var l)) szlaki[sasiad] = l = new();
                    int i = l.FindIndex(x => x.Item1 == nr);
                    if (i < 0) l.Add((nr, tor, odNas));
                    else if (l[i].Item2 == null && tor != null) l[i] = (nr, tor, odNas);
                }

                // eliminacja: odcinek bez skrótów po drugiej stronie niż znany szlak tego pociągu należy do jedynego pozostałego sąsiada
                // (wjechał od Łc, wyjeżdża „l1_it2790” → Ła; wjechał „l1_it2799”, wyjechał do Łc → „l1_it2799” to Ła)
                string Inny(string znany) { var k = Sasiedzi().Where(x => !string.Equals(x, znany, StringComparison.OrdinalIgnoreCase)).ToList(); return k.Count == 1 ? k[0] : null; }
                foreach (var (o, odNas) in nieznane)
                {
                    var znany = odNas ? _skad.GetValueOrDefault(nr) : _dokad.GetValueOrDefault(nr);
                    if (znany != null && Inny(znany) is { } y) Ucz(o, y, null);
                    else if (!odNas) { if (!_wjazdNieznany.TryGetValue(nr, out var w)) _wjazdNieznany[nr] = w = new(); w.Add(o); }
                }
                if (_dokad.TryGetValue(nr, out var dok) && _wjazdNieznany.TryGetValue(nr, out var wj) && Inny(dok) is { } y2)
                {
                    _wjazdNieznany.Remove(nr);
                    foreach (var o in wj) Ucz(o, y2, null);
                }
            }
            if (zapisz) ZapiszUst();
        }
        var podpis = string.Join(",", stacji) + "|" + string.Join(";", szlaki.OrderBy(k => k.Key).Select(k => k.Key + ":" + string.Join(",", k.Value)));
        if (podpis == _podpisZGry) return false;
        _podpisZGry = podpis;
        _pociagiStacji = stacji;
        foreach (var s in _szlaki.Wszystkie) s.ZGry.Clear();
        foreach (var (sasiad, l) in szlaki)
        {
            var s = _szlaki.Daj(sasiad);
            s.ZGry.AddRange(l);
            // sąsiad wyprawił do nas pociąg: „droga wolna” → „na szlaku”, gdy gra pokazuje go na szlaku od tego sąsiada
            foreach (var p in s.Pociagi.Where(p => !p.OdNas && p.Stan == StanOkna.Zgoda && l.Any(z => z.Item1 == p.Nr && !z.Item3)))
            {
                p.Stan = StanOkna.NaSzlaku; p.Zmiana = DateTime.Now;
                Zdarzenie($"{Teraz():HH:mm}  {p.Nr} odjechał od {sasiad} – jest na szlaku (z gry)");
            }
            foreach (var z in l.Where(z => !z.Item3 && !_odSasiadaZGry.Contains(z.Item1 + "|" + sasiad)))
            {
                _odSasiadaZGry.Add(z.Item1 + "|" + sasiad);
                if (!s.Pociagi.Any(p => p.Nr == z.Item1)) Zdarzenie($"{Teraz():HH:mm}  {z.Item1} jedzie od {sasiad}{(z.Item2 is int t ? $" po torze {t}" : "")} (z gry)");
            }
        }
        return true;
    }

    readonly HashSet<string> _odSasiadaZGry = new();
    readonly Dictionary<string, string> _skad = new(StringComparer.OrdinalIgnoreCase);    // pociąg → sąsiad, od którego przyjechał (z gry)
    readonly Dictionary<string, string> _dokad = new(StringComparer.OrdinalIgnoreCase);   // pociąg → sąsiad, do którego odjechał (z gry)
    readonly Dictionary<string, HashSet<string>> _wjazdNieznany = new(StringComparer.OrdinalIgnoreCase);   // odcinki wjazdu bez sąsiada

    /// <summary>Odcinki, których numer w nazwie nie jest nazwą toru – z zapisów czatu (2026-11-27: 484745 „po torze 224” na „l1_la_lb_it324”).</summary>
    /// Pulpit Łazów (2026-11-27): tor 1ŁA – 2795, 2781, 2779; tor 2ŁA – 2794, 2790, 2778 (40682 na 2790 po torze 2).
    static readonly Dictionary<string, int> OdcinekTorZnany = new(StringComparer.OrdinalIgnoreCase)
    {
        ["l1_la_lb_it324"] = 224,
        ["l1_it2799"] = 1, ["l1_it2781"] = 1, ["l1_it2779"] = 1, ["l1_2795"] = 1,
        ["l1_it2790"] = 2, ["l1_it2778"] = 2, ["lt_it2794"] = 2,
    };

    /// <summary>
    /// Tor odcinka szlaku bez skrótów: znany z pulpitu / poznany; dalej szlak dwutorowy linii („l1_it2781”) – kilometr nieparzysty
    /// tor 1, parzysty tor 2 (tak na pulpicie Łazów i w czacie: 14021 po torze 1 przez 2799/2781), o ile szlak ma tory 1 i 2.
    /// </summary>
    int? TorOdcinka(string o, string sasiad)
    {
        if (OdcinekTorZnany.TryGetValue(o, out var t)) return t;
        var tory = _szlaki.Daj(sasiad).Tory;
        var m = Regex.Match(o, @"^\d*l\d+_it(\d+)$", RegexOptions.IgnoreCase);
        if (m.Success && tory.Contains(1) && tory.Contains(2)) return int.Parse(m.Groups[1].Value) % 2 == 1 ? 1 : 2;
        return null;
    }

    /// <summary>Tor pociągu do / od sąsiada z czatu: okienko szlaku albo ostatni komunikat z torem (bez wysłanych automatycznie).</summary>
    int? TorZCzatu(string sasiad, string nr) =>
        _szlaki.Znajdz(sasiad, nr)?.Tor
        ?? _wpisy.LastOrDefault(w => w.Pociag == nr && w.Sasiad == sasiad && w.Tor.HasValue && !(w.Status ?? "").Contains("(auto)"))?.Tor;
    readonly Dictionary<string, DateTime> _widzianyWGrze = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Pociąg wyprawiony od nas („na szlaku” ►) znika ze szlaku, gdy gra od minuty nie pokazuje go na żadnym naszym odcinku –
    /// zjechał na odcinki sąsiada. Pociągu, którego gra nigdy nie pokazała, nie rusza (zostaje do przyjęcia albo 60 min).
    /// </summary>
    bool ZjechalyZeSzlaku()
    {
        if (!StacjaZGry) return false;
        var teraz = DateTime.Now;
        foreach (var nr in _wStacji.Keys) _widzianyWGrze[nr] = teraz;
        bool zm = false;
        foreach (var s in _szlaki.Wszystkie)
            foreach (var p in s.Pociagi.Where(p => p.OdNas && p.Stan == StanOkna.NaSzlaku
                                                   && _widzianyWGrze.TryGetValue(p.Nr, out var t) && (teraz - t).TotalSeconds > 60).ToList())
            {
                s.Pociagi.Remove(p); zm = true;
                Zdarzenie($"{Teraz():HH:mm}  {p.Nr} zjechał ze szlaku do {s.Sasiad} (z gry)");
            }
        foreach (var k in _widzianyWGrze.Where(kv => (teraz - kv.Value).TotalMinutes > 30).Select(kv => kv.Key).ToList()) _widzianyWGrze.Remove(k);
        return zm;
    }

    /// <summary>Sąsiad (i tor), na którego szlaku gra pokazuje teraz pociąg.</summary>
    (string Sasiad, int? Tor) SzlakZGry(string nr, bool? odNas = null)
    {
        foreach (var s in _szlaki.Wszystkie)
            foreach (var z in s.ZGry.Where(z => z.Nr == nr && (odNas == null || z.OdNas == odNas)))
                return (s.Sasiad, z.Tor.HasValue && (s.Tory.Count == 0 || s.Tory.Contains(z.Tor.Value)) ? z.Tor : null);
        return (null, null);
    }

    void Czat(JsonElement p)
    {
        var wych = MostKlient.S(p, "kier") == "wych";
        var od = MostKlient.S(p, "od") ?? ""; var doK = MostKlient.S(p, "do") ?? "";
        var klasa = MostKlient.S(p, "klasa") ?? ""; var tekst = MostKlient.S(p, "tekst") ?? ""; var pociag = MostKlient.S(p, "pociag") ?? "";
        var czas = Godzina(MostKlient.S(p, "czasGry")) ?? Godzina(MostKlient.S(p, "czasPc")) ?? DateTime.Now.ToString("HH:mm");
        int? torModa = p.TryGetProperty("tor", out var tz) && tz.TryGetInt32(out var tzz) ? tzz : null;
        var tor = Formulki.TorZTekstu(tekst, torModa);

        if (wych)
        {
            // własny komunikat z programu wraca z gry – prawdziwy tekst gry zamiast formułki
            var moj = _wpisy.LastOrDefault(w => w.Wychodzacy && w.Id > 0 && w.Do == doK && w.Klasa == klasa && (DateTime.Now - w.Utworzony).TotalSeconds < 20 && !(w.Status ?? "").StartsWith("w czacie gry"));
            if (moj != null) { moj.Tekst = tekst; moj.Status = "w czacie gry" + ((moj.Status ?? "").EndsWith("(auto)") ? "  (auto)" : ""); return; }
            OznaczOdpowiedziane(doK, pociag);
        }
        var sasiad = wych ? doK : od;
        var s = _sasiedzi.FirstOrDefault(x => x.Nazwa == sasiad);
        if (s == null && sasiad.Length > 0 && StacjaZGry) _sasiedzi.Add(s = new Sasiad { Nazwa = sasiad, Gracz = !(p.TryGetProperty("ai", out var ai0) && ai0.GetBoolean()) });
        var w = new Wpis
        {
            Czas = czas, Wychodzacy = wych, Od = od, Do = doK, Ai = p.TryGetProperty("ai", out var ai) && ai.GetBoolean(),
            Klasa = klasa, Tekst = tekst, Pociag = pociag, Status = wych ? "z okna gry" : "", Tor = tor,
        };
        _wpisy.Add(w);
        // po starcie mod dosyła 60 ostatnich komunikatów, nawet sprzed godzin – stare nie stawiają pociągów na szlakach
        bool stary = DateTime.TryParse(MostKlient.S(p, "czasPc"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var kiedy)
                     && (DateTime.Now - kiedy).TotalMinutes > 20;
        if (stary) w.Odpowiedziano = true;
        else _szlaki.Zastosuj(w);
        Zdarzenie($"{czas}  {(wych ? $"→ {doK}" : $"← {od}")}: {tekst}");
        if (s != null && !stary)
        {
            if (wych) s.Oczekujace++;
            else
            {
                s.Oczekujace = 0;
                if (_wybrany != s.Nazwa || !IsActive) s.Nieprzeczytane++;
            }
        }
        if (!wych && klasa == "TrainPermissionMessage" && pociag.Length > 0)
        {
            _zgody[pociag] = $"{Skrot(sasiad)} {czas}";
            foreach (var r in _plan.Where(r => r.Pociag == pociag)) r.Zgoda = _zgody[pociag];
        }
        if (!wych && _wybrany == null && sasiad.Length > 0) _wybrany = sasiad;
        OdswiezSchemat();
        if (sasiad == _wybrany) OdswiezEzp();
    }

    void Odpowiedz(JsonElement p)
    {
        long id = p.TryGetProperty("id", out var i) && i.TryGetInt64(out var ii) ? ii : 0;
        var ok = p.TryGetProperty("ok", out var o) && o.GetBoolean();
        var msg = MostKlient.S(p, "msg") ?? "";
        if (_wyslane.TryGetValue(id, out var w) && !(w.Status ?? "").StartsWith("w czacie gry"))
        {
            w.Status = (ok ? "przekazano do gry" : "ODRZUCONO: " + msg) + ((w.Status ?? "").EndsWith("(auto)") ? "  (auto)" : "");
            if (!ok) Zdarzenie($"{Teraz():HH:mm}  gra odrzuciła komunikat do {w.Do}: {msg}");
        }
        _wynik.Text = ok ? "Gra przyjęła komunikat." : "Gra odrzuciła: " + msg;
        _wynik.Foreground = ok ? Wyglad.Zielony : Wyglad.Czerwony;
    }

    // ------------------------------------------------------------------ pulpit

    /// <summary>Usuwa pociąg z okienka szlaku bez komunikatu do gry (pomyłka, pociąg zniknął itp.).</summary>
    void AnulujPociag(string sasiad, string nr)
    {
        if (string.IsNullOrEmpty(sasiad) || string.IsNullOrEmpty(nr)) return;
        if (_szlaki.Daj(sasiad).Pociagi.RemoveAll(p => p.Nr == nr) == 0)
        {
            Zdarzenie(_szlaki.Daj(sasiad).ZGry.Any(z => z.Nr == nr)
                ? $"{Teraz():HH:mm}  {nr} – gra pokazuje go na szlaku do {sasiad}; okienko zniknie samo, gdy zjedzie z naszych odcinków"
                : $"{Teraz():HH:mm}  {nr} – brak na szlaku do {sasiad}");
            return;
        }
        _awizowane.Remove($"{nr}|{sasiad}");
        foreach (var w in _wpisy.Where(w => w.Sasiad == sasiad && w.Pociag == nr && !w.Wychodzacy)) w.Odpowiedziano = true;
        Zdarzenie($"{Teraz():HH:mm}  {nr} anulowany na szlaku {sasiad} (bez wiadomości)");
        OdswiezSchemat();
        if (sasiad == _wybrany) OdswiezEzp();
    }

    /// <summary>Prawy klik na szlaku – ręczne tory (np. „1 2”); puste – z komunikatów gry, poznane od nowa.</summary>
    void UstawToryRecznie(string sasiad)
    {
        var k = Klucz(sasiad);
        var s = _szlaki.Daj(sasiad);
        var pole = new TextBox { Text = string.Join(" ", s.Tory), MinWidth = 220, Margin = new Thickness(0, 6, 0, 10) };
        var okno = new Window
        {
            Title = $"Tory szlaku {Stacja} – {sasiad}", SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, Background = Wyglad.P("#D4D0C8"), FontFamily = new FontFamily("Tahoma"),
        };
        bool? wynik = null;
        var ok = Wyglad.Przycisk("OK", () => { wynik = true; okno.Close(); }); ok.IsDefault = true; ok.MinWidth = 70;
        var auto = Wyglad.Przycisk("Z gry (automatycznie)", () => { wynik = false; okno.Close(); }, "Usuwa ręczne ustawienie – tory poznawane z komunikatów od nowa");
        var anuluj = Wyglad.Przycisk("Anuluj", () => okno.Close()); anuluj.IsCancel = true; anuluj.MinWidth = 70;
        var odwroc = Wyglad.Przycisk("Odwróć kolejność", () =>
        {
            var t = pole.Text.Split(new[] { ' ', ',', ';', '/' }, StringSplitOptions.RemoveEmptyEntries);
            pole.Text = string.Join(" ", t.Reverse());
        }, "Np. „1 2” → „2 1”");
        foreach (var b in new[] { ok, auto, anuluj, odwroc }) b.Margin = new Thickness(0, 0, 6, 0);
        var przyciski = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        przyciski.Children.Add(odwroc); przyciski.Children.Add(ok); przyciski.Children.Add(auto); przyciski.Children.Add(anuluj);
        var p = new StackPanel { Margin = new Thickness(12) };
        p.Children.Add(new TextBlock { Text = "Numery torów szlakowych (jak w komunikatach gry, np. „1 2”),\noddzielone spacją lub przecinkiem. Kolejność = od góry na schemacie\n(„2 1” – tor 2 na górze):" });
        p.Children.Add(pole); p.Children.Add(przyciski);
        okno.Content = p;
        okno.Loaded += (_, _) => { pole.Focus(); pole.SelectAll(); };
        okno.ShowDialog();
        if (wynik == null) return;
        if (wynik == true)
        {
            var tory = pole.Text.Split(new[] { ' ', ',', ';', '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => int.TryParse(x, out var n) ? n : 0).Where(n => n > 0).Distinct().ToList();   // kolejność jak wpisana (od góry)
            if (tory.Count == 0) { _ust.ToryReczne.Remove(k); _ust.Tory.Remove(k); }
            else _ust.ToryReczne[k] = tory;
        }
        else { _ust.ToryReczne.Remove(k); _ust.Tory.Remove(k); s.Tory.Clear(); s.Reczne = false; }
        ZapiszUst();
        OdswiezSchemat();
        if (sasiad == _wybrany) OdswiezEzp();
    }

    void OdswiezSchemat()
    {
        var nazwy = Sasiedzi();
        var lista = new List<Szlak>();
        for (int i = 0; i < nazwy.Count; i++)
        {
            var s = _szlaki.Daj(nazwy[i]);
            var k = Klucz(nazwy[i]);
            if (_ust.ToryReczne.TryGetValue(k, out var r) && r.Count > 0)
            {
                s.Reczne = true; s.Tory.Clear(); s.Tory.AddRange(r.Distinct());   // ręczne – w kolejności wpisanej przez użytkownika
            }
            else
            {
                if (s.Reczne) { s.Reczne = false; s.Tory.Clear(); }
                if (_ust.Tory.TryGetValue(k, out var t)) foreach (var x in t.ToList()) _szlaki.PoznajTor(nazwy[i], x);
                if (Ustawienia.ToryZnane.TryGetValue(k, out var z)) foreach (var x in z) _szlaki.PoznajTor(nazwy[i], x);
            }
            s.Lewa = _ust.Strony.TryGetValue(k, out var l) ? l : i < (nazwy.Count + 1) / 2;
            lista.Add(s);
        }
        if (_wybrany == null || !nazwy.Contains(_wybrany)) _wybrany = nazwy.FirstOrDefault();
        _schemat.Szlaki = lista;
        _schemat.Stacja = Stacja;
        _schemat.PociagiStacji = _pociagiStacji;
        _schemat.Wybrany = _wybrany;
        _schemat.InvalidateVisual();
    }

    /// <summary>Pytania posterunku czekające na odpowiedź (zapytanie, „co odjechało?”, blokada; z ostatnich 30 min) – licznik „O:”.</summary>
    int Pytania(string sasiad) => _wpisy.Count(x => !x.Wychodzacy && !x.Odpowiedziano && x.Od == sasiad && x.Klasa is "TrainRequestMessage" or "TrainQuestionMessage" or "LockLineblockMessage" && (DateTime.Now - x.Utworzony).TotalMinutes < 30);

    void WybierzSasiada(string s, bool zakladkaEzp)
    {
        if (_wybrany != s) { _wybrany = s; _pytanie = null; _torRecznie = false; _tbPociag.Text = ""; }
        var sa = _sasiedzi.FirstOrDefault(x => x.Nazwa == s);
        if (sa != null) sa.Nieprzeczytane = 0;
        if (zakladkaEzp) PokazZakladke("EZP");
        OdswiezSchemat();
        OdswiezEzp();
    }

    // ------------------------------------------------------------------ EZP

    void ZbudujRodzaje()
    {
        _rodzPrzyciski.Children.Clear();
        var klasy = new[] { "TrainRequestMessage", "TrainPermissionMessage", "TrainDepartedMessage", "TrainArrivedMessage", "TrainDenialMessage", "TrainCancelledMessage", "TrainQuestionMessage", "LockLineblockMessage", "STOJ" };
        foreach (var k in klasy)
        {
            if (k != "STOJ" && !_rodzaje.Any(r => r.Klasa == k)) continue;
            var b = Wyglad.Klasyczny(new ToggleButton
            {
                Content = k switch
                {
                    "TrainRequestMessage" => "Zapytanie", "TrainPermissionMessage" => "Droga wolna", "TrainDepartedMessage" => "Odjechał",
                    "TrainArrivedMessage" => "Przyjechał", "TrainDenialMessage" => "Odmowa", "TrainCancelledMessage" => "Odwołany",
                    "TrainQuestionMessage" => "Co odjechało?", "LockLineblockMessage" => "Blokada", _ => "Stój",
                },
                Tag = k, Margin = new Thickness(0, 0, 4, 4), Height = Wyglad.H, Focusable = false,
                ToolTip = k == "STOJ" ? "Stój – zatrzymać pociąg (w grze nie ma takiego komunikatu: idzie jako tekst, okienko na szlaku miga na czerwono)" : Teksty.Etykieta(k),
            });
            b.Click += (_, _) => WybierzRodzaj(k);
            _rodzPrzyciski.Children.Add(b);
        }
        WybierzRodzaj(_klasa);
    }

    Rodzaj RodzajGry(string klasa) => _rodzaje.FirstOrDefault(r => r.Klasa == klasa);

    string[] Argumenty(string klasa) => klasa == "STOJ" ? new[] { "Train", "TrackNumber" } : RodzajGry(klasa)?.Arg ?? Teksty.DomyslneArg(klasa);

    void WybierzRodzaj(string klasa)
    {
        _klasa = klasa;
        foreach (ToggleButton b in _rodzPrzyciski.Children) b.IsChecked = (string)b.Tag == klasa;
        var arg = Argumenty(klasa);
        _wierszPowod?.Pokaz(arg.Contains("DenialReason"));
        _wierszCzas?.Pokaz(arg.Contains("Time"));
        if (arg.Contains("Time")) _tbCzas.Text = Teraz().ToString("HH:mm");
        _tor.IsEnabled = arg.Contains("TrackNumber");
        Podglad();
    }

    void OdswiezEzp()
    {
        var s = _wybrany;
        var sa = s == null ? null : _sasiedzi.FirstOrDefault(x => x.Nazwa == s);
        _ezpTytul.Text = s == null ? "NADAWANIE – brak posterunków sąsiednich (połącz z grą albo pobierz rozkład)"
            : $"NADAWANIE DO: {s}{(sa != null ? $"  ({sa.Obsada})" : "")}" + (StacjaZGry ? "" : "   – tylko podgląd, wysyłanie z posterunku w grze");
        _tor.UstawTory(s != null && _szlaki.Daj(s).Tory.Count > 0 ? _szlaki.Daj(s).Tory : new List<int> { 1, 2 });

        // pociągi tego szlaku: na szlaku, z planu w tę stronę (−30 … +90 min)
        _pocPrzyciski.Children.Clear();
        if (s != null)
        {
            var teraz = Teraz();
            var nr = _szlaki.Daj(s).ZGry.Select(z => z.Nr)
                .Concat(_szlaki.Daj(s).Pociagi.Select(p => p.Nr))
                .Concat(_pociagiStacji)
                .Concat(_plan.Where(r => (r.SasiadSkad == s || r.SasiadDokad == s) && (r.Czas - teraz).TotalMinutes is > -30 and < 90)
                             .OrderBy(r => Math.Abs((r.Czas - teraz).TotalMinutes)).Select(r => r.Pociag))
                .Distinct().Take(10).ToList();
            foreach (var n in nr)
            {
                var r = _plan.FirstOrDefault(x => x.Pociag == n);
                bool wSt = _pociagiStacji.Contains(n);
                var b = Wyglad.Przycisk(n, () => { _torRecznie = false; _tbPociag.Text = n; _tbPociag.Focus(); _tbPociag.CaretIndex = n.Length; },
                    r != null ? $"{r.Czas:HH:mm} {r.Skad} → {r.Dokad}  {r.Kategoria}" : wSt ? "w stacji (z gry)" : "na szlaku");
                b.Height = Wyglad.H; b.MinWidth = 64; b.Margin = new Thickness(0, 0, 4, 6);
                if (r?.WStacji == true || wSt) b.Foreground = Wyglad.Pomaranczowy;
                _pocPrzyciski.Children.Add(b);
            }
        }
        PokazOdpowiedzi();
        Podglad();
    }

    /// <summary>Ostatni komunikat posterunku (albo kliknięty w rozmowie) i odpowiedzi na niego.</summary>
    void PokazOdpowiedzi()
    {
        var s = _wybrany;
        _odpPrzyciski.Children.Clear();
        if (s == null) { _ostatni.Text = ""; return; }
        var w = _pytanie != null && _pytanie.Od == s ? _pytanie
              : _wpisy.LastOrDefault(x => !x.Wychodzacy && !x.Odpowiedziano && x.Od == s && MaOdpowiedzi(x.Klasa) && (DateTime.Now - x.Utworzony).TotalMinutes < 30);
        var ost = w ?? _wpisy.LastOrDefault(x => x.Sasiad == s);
        bool pytanie = w != null && !w.Odpowiedziano;
        _ostatni.Text = ost == null ? "–" : $"{ost.Czas} {(ost.Wychodzacy ? "→" : "←")} {ost.Tekst}{(ost.Wychodzacy ? $"   [{ost.Status}]" : "")}";
        _ostatni.Foreground = pytanie ? Wyglad.Zolty : ost?.Wychodzacy == true ? Wyglad.Pomaranczowy : Wyglad.Zielony;
        _ostatniRamka.BorderBrush = pytanie ? Wyglad.Zolty : Wyglad.P("#404040");
        if (w == null) return;
        var nr = w.Pociag ?? "";
        void B(string etykieta, string klasa, string powod = null, bool zTorem = true)
        {
            var b = Wyglad.Przycisk(etykieta, () =>
            {
                int? tor = zTorem ? w.Tor : null;
                bool komplet = nr.Length > 0 && powod != "";
                if (_ust.OdRazu && komplet && StacjaZGry) Nadaj(s, klasa, nr, tor, Teraz().ToString("HH:mm"), powod, null);
                else Przygotuj(klasa, nr, tor, powod);
            });
            b.Height = Wyglad.H; b.HorizontalContentAlignment = HorizontalAlignment.Left; b.Padding = new Thickness(10, 2, 10, 2);
            if (klasa == "TrainPermissionMessage") b.Background = Wyglad.P("#1F5A2A");
            else if (klasa == "TrainDenialMessage") b.Background = Wyglad.P("#5A2323");
            _odpPrzyciski.Children.Add(b);
        }
        switch (w.Klasa)
        {
            case "TrainRequestMessage":
                B($"✔ Droga wolna dla {nr}", "TrainPermissionMessage");
                B("✖ tor zajęty", "TrainDenialMessage", "TrackOccupied");
                B("✖ brak wolnych torów", "TrainDenialMessage", "NoTracksAvailable");
                B("✖ mam pozwolenie w drugą stronę", "TrainDenialMessage", "IHavePermission");
                B("✖ inny powód…", "TrainDenialMessage", "");
                break;
            case "TrainPermissionMessage":
                B($"Poc. {nr} odjechał", "TrainDepartedMessage");
                B($"Poc. {nr} odwołany", "TrainCancelledMessage", zTorem: false);
                break;
            case "TrainDepartedMessage":
                B($"Poc. {nr} przyjechał", "TrainArrivedMessage", zTorem: false);
                break;
            case "TrainQuestionMessage":
                nr = "";
                B("Pociąg … odjechał (wybierz pociąg)", "TrainDepartedMessage", zTorem: false);
                break;
            case "TrainDenialMessage":
                B($"Zapytaj ponownie o poc. {nr}", "TrainRequestMessage");
                break;
            case "LockLineblockMessage":
                B("Odpowiedz tekstem", "CustomTextMessage", zTorem: false);
                break;
        }
    }

    static bool MaOdpowiedzi(string klasa) => klasa is "TrainRequestMessage" or "TrainPermissionMessage" or "TrainDepartedMessage"
        or "TrainQuestionMessage" or "TrainDenialMessage" or "LockLineblockMessage";

    void OznaczOdpowiedziane(string sasiad, string pociag)
    {
        foreach (var x in _wpisy.Where(x => !x.Wychodzacy && !x.Odpowiedziano && x.Od == sasiad && (string.IsNullOrEmpty(x.Pociag) || x.Pociag == pociag)))
        {
            x.Odpowiedziano = true;
            if (string.IsNullOrEmpty(x.Status)) x.Status = "✓ odpowiedziano";
        }
    }

    void Przygotuj(string klasa, string pociag, int? tor, string powod)
    {
        PokazZakladke("EZP");
        if (klasa == "CustomTextMessage") { _tbTekst.Focus(); return; }
        WybierzRodzaj(klasa);
        _torRecznie = false;
        _tbPociag.Text = pociag ?? "";
        _tor.Wartosc = tor;
        _torRecznie = tor.HasValue;
        if (powod != null)
        {
            var i = Array.FindIndex(Teksty.Powody, x => x.Kod == powod);
            if (i >= 0) _cbPowod.SelectedIndex = i;
        }
        _wynik.Text = powod == "" ? "Wybierz powód i kliknij Wyślij." : string.IsNullOrEmpty(pociag) ? "Wpisz albo wybierz pociąg i kliknij Wyślij." : "Sprawdź podgląd i kliknij Wyślij (Ctrl+Enter).";
        _wynik.Foreground = Wyglad.Cyjan;
        (powod == "" ? (Control)_cbPowod : string.IsNullOrEmpty(pociag) ? _tbPociag : _btnWyslij).Focus();
        Podglad();
    }

    /// <summary>Tor z rozmowy / szlaku dla wpisanego pociągu i wybranego posterunku. Ręcznego wyboru nie rusza.</summary>
    void TorZNumeru()
    {
        if (_torRecznie) return;
        var nr = _tbPociag.Text?.Trim();
        int? tor = null;
        if (!string.IsNullOrEmpty(nr) && _wybrany != null)
            tor = _wpisy.LastOrDefault(x => x.Pociag == nr && x.Tor.HasValue && x.Sasiad == _wybrany)?.Tor ?? _szlaki.Znajdz(_wybrany, nr)?.Tor;
        if (_tor.Wartosc != tor) _tor.Wartosc = tor;
    }

    (string Pociag, int? Tor, string Czas, string Powod) Pola()
    {
        var arg = Argumenty(_klasa);
        string pociag = arg.Contains("Train") ? _tbPociag.Text?.Trim() : null;
        int? tor = arg.Contains("TrackNumber") ? _tor.Wartosc : null;
        string czas = arg.Contains("Time") ? _tbCzas.Text?.Trim() : null;
        string powod = arg.Contains("DenialReason") && _cbPowod.SelectedIndex >= 0 ? Teksty.Powody[_cbPowod.SelectedIndex].Kod : null;
        return (pociag, tor, czas, powod);
    }

    void Podglad()
    {
        if (_podglad == null) return;
        var (pociag, tor, czas, powod) = Pola();
        _podglad.Text = _klasa == "STOJ" ? TekstStoj(pociag, tor)
            : Formulki.Tekst(_klasa, string.IsNullOrEmpty(pociag) ? "…" : pociag, tor, czas, powod, null, RodzajGry(_klasa));
    }

    static string TekstStoj(string nr, int? tor) => $"Pociąg {(string.IsNullOrEmpty(nr) ? "…" : nr)}{(tor.HasValue ? $" po torze {tor}" : "")} – stój, zatrzymać.";

    void Wyslij()
    {
        if (_wybrany == null) { Blad("Wybierz posterunek – klik w pole EZP przy szlaku."); return; }
        var (pociag, tor, czas, powod) = Pola();
        if (Argumenty(_klasa).Contains("Train") && string.IsNullOrEmpty(pociag)) { Blad("Podaj numer pociągu."); return; }
        if (!string.IsNullOrEmpty(czas) && !TimeSpan.TryParse(czas, out _)) { Blad("Godzina w postaci GG:MM."); return; }
        if (_klasa == "STOJ")
        {
            if (!Nadaj(_wybrany, "CustomTextMessage", null, null, null, null, TekstStoj(pociag, tor))) return;
            var p = _szlaki.Znajdz(_wybrany, pociag);
            _szlaki.Ustaw(_szlaki.Daj(_wybrany), pociag, p?.OdNas ?? false, StanOkna.Stoj, tor ?? p?.Tor);
            OdswiezSchemat();
        }
        else if (!Nadaj(_wybrany, _klasa, pociag, tor, czas, powod, null)) return;
        _torRecznie = false;
    }

    void WyslijTekst()
    {
        if (_wybrany == null) { Blad("Wybierz posterunek – klik w pole EZP przy szlaku."); return; }
        if (string.IsNullOrWhiteSpace(_tbTekst.Text)) { Blad("Wpisz tekst."); return; }
        if (Nadaj(_wybrany, "CustomTextMessage", null, null, null, null, _tbTekst.Text.Trim())) _tbTekst.Text = "";
    }

    /// <summary>Wysyła komunikat do posterunku (ręcznie albo automat) i dopisuje go do rozmowy i szlaku.</summary>
    bool Nadaj(string sasiad, string klasa, string pociag, int? tor, string czas, string powod, string tekst, bool auto = false)
    {
        if (!StacjaZGry) { Blad($"Wysyłanie tylko z posterunku obsługiwanego w grze ({_posterunek}). Wybierz „{ZGry}”."); return false; }
        var opis = Formulki.Tekst(klasa, pociag, tor, czas, powod, tekst, RodzajGry(klasa));
        long id = _demo ? -1 : _most.WyslijKomunikat(sasiad, klasa, pociag, tor, czas, powod, tekst);
        var w = new Wpis
        {
            Czas = Teraz().ToString("HH:mm"), Wychodzacy = true, Od = _posterunek, Do = sasiad, Klasa = klasa, Pociag = pociag, Tor = tor, Id = id, Tekst = opis,
            Status = (_demo ? "tryb pokazowy – nie wysłano" : _most.Polaczony ? "wysyłanie…" : "brak połączenia z modem – nie dotarło do gry") + (auto ? "  (auto)" : ""),
        };
        _wyslane[id] = w;
        _wpisy.Add(w);
        _szlaki.Zastosuj(w);
        var s = _sasiedzi.FirstOrDefault(x => x.Nazwa == sasiad);
        if (s != null) s.Oczekujace++;
        OznaczOdpowiedziane(sasiad, pociag);
        Zdarzenie($"{w.Czas}  → {sasiad}: {opis}{(auto ? "  (auto)" : "")}");
        if (!auto) { _wynik.Text = $"Wysłano do {sasiad}."; _wynik.Foreground = Wyglad.Szary; _pytanie = null; }
        OdswiezSchemat();
        if (sasiad == _wybrany) OdswiezEzp();
        return true;
    }

    void Blad(string t) { _wynik.Text = t; _wynik.Foreground = Wyglad.Czerwony; }

    void Zdarzenie(string t)
    {
        _zdarzenia.Insert(0, t);
        while (_zdarzenia.Count > 500) _zdarzenia.RemoveAt(_zdarzenia.Count - 1);
    }

    // ------------------------------------------------------------------ automaty (awizacja, odjazd, przyjazd)

    /// <summary>
    /// Obiekt na pewno stacyjny: zwrotnica („z2055b”, „z1950ab”), odcinek ze skrótem stacji („l1_kol_it5”), wykolejnica („…_wk…”).
    /// Odcinki „t39532” bywają i na szlaku (np. od Żakowic, Rokicin), i w stacji – same nie przesądzają.
    /// Skrót stacji bywa po numerze linii („l1_kol_”, „l1_rg_”) albo po numerze posterunku („3877_sk_it104a”, „3877_sk_iz1”).
    /// Zwrotnice same nie niosą pociągów (pulpit podaje je tylko z EBI300TrackRenderer) – zajęcie widać na odcinkach izolowanych „…_iz…”.
    /// Szlakowe: „l1_it766”, „l11_it23”, „69l11_it94”, „l12_it_sk_pm_…”, a z dwoma skrótami stacji (Łazy):
    /// „l1_lb_lc_it3” (Łazy–Łazy Łc, tor 3), „l1_la_lb_it123” (Łazy Ła–Łazy, tor 123) – nie są stacyjne mimo „l1_lb_”.
    /// </summary>
    static bool ObiektStacji(string n) => !string.IsNullOrEmpty(n) && !SzlakZNazwy(n)
        && (Regex.IsMatch(n, @"^z(?:\d|_)") || Regex.IsMatch(n, @"^(?:l\d+|\d+)_(?!it)[a-z]+_", RegexOptions.IgnoreCase) || n.Contains("_wk", StringComparison.OrdinalIgnoreCase));

    static bool SzlakZNazwy(string n) => Regex.IsMatch(n, @"^\d*l\d+_(?:it|[a-z]+_[a-z]+_it)", RegexOptions.IgnoreCase);

    /// <summary>
    /// Odcinek szlaku przy stacji: z nazwy („l1_it988”, „l17_it219”) albo poznany – zajmowany przez pociąg wjeżdżający do nas
    /// ze szlaku, zanim wjechał na zwrotnice (odcinki „t…” od Żakowic, Rokicin). Poznane – w ustawieniach, osobno dla stacji.
    /// </summary>
    bool ObiektSzlaku(string n) => !string.IsNullOrEmpty(n) && !ObiektStacji(n) && (SzlakZNazwy(n) || Szlakowe().Contains(n));

    HashSet<string> _szlakowe; string _szlakoweDla;
    HashSet<string> Szlakowe()
    {
        if (_szlakowe == null || _szlakoweDla != Stacja)
        {
            _szlakoweDla = Stacja;
            if (_ust.ObiektySzlaku.TryGetValue(Stacja ?? "", out var l) && l.RemoveAll(o => ObiektStacji(o) || SzlakZNazwy(o)) > 0)
                ZapiszUst();   // wyrzuć odcinki stacyjne poznane przez pomyłkę (np. „3877_sk_…” przed poprawką wzorca)
            _szlakowe = new(l ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        }
        return _szlakowe;
    }

    bool PoznajSzlakowe(IEnumerable<string> obiekty)
    {
        bool nowe = false;
        foreach (var o in obiekty.Where(o => !string.IsNullOrEmpty(o) && !ObiektStacji(o) && !SzlakZNazwy(o)))
            if (Szlakowe().Add(o))
            {
                if (!_ust.ObiektySzlaku.TryGetValue(Stacja, out var l)) _ust.ObiektySzlaku[Stacja] = l = new();
                l.Add(o); nowe = true;
            }
        return nowe;
    }

    /// <summary>Pociąg w stacji (do planu ruchu): ze stanu automatów, a bez gry – po obiektach stacyjnych.</summary>
    bool WStacji(string nr) => _gdzie.TryGetValue(nr, out var s) ? s == 2 : _wStacji.TryGetValue(nr, out var g) && g.Any(ObiektStacji);

    /// <summary>Pociąg jedzie do nas ze szlaku: zgoda / zapytanie od sąsiada albo w rozkładzie poprzedni posterunek to sąsiad.</summary>
    bool JedzieDoNas(string nr) =>
        _szlaki.Wszystkie.Any(s => s.Pociagi.Any(p => p.Nr == nr && !p.OdNas && p.Stan != StanOkna.Odmowa))
        || _plan.FirstOrDefault(x => x.Pociag == nr)?.SasiadSkad != null;

    readonly Dictionary<string, HashSet<string>> _ogonPrzyjazdu = new();   // odcinki szlaku zajęte przy wjeździe do stacji
    readonly Dictionary<string, int> _znikniety = new(StringComparer.OrdinalIgnoreCase);   // odczyty z rzędu bez żadnego odcinka
    readonly HashSet<string> _zjechalZeSzlaku = new();                    // w stacji, już bez żadnego odcinka szlaku
    readonly Dictionary<string, List<string>> _poprzednio = new();

    /// <summary>
    /// Stan pociągu: 0 – poza obszarem, 1 – na szlaku dojazdowym do nas, 2 – w stacji, 3 – odjechał (pierwsza oś na odcinku szlaku).
    /// Wjazd do stacji = zajęcie obiektu stacyjnego (zwrotnicy). Odjazd = zajęcie odcinka szlaku innego niż te, na których stał
    /// ogon przy wjeździe (albo po zjechaniu całym składem ze szlaku). Bez poznanych odcinków – odjazd, gdy zniknie z obszaru.
    /// </summary>
    void Automaty()
    {
        if (!StacjaZGry) return;
        var teraz = new Dictionary<string, int>();
        bool zm = false, nowe = false;
        foreach (var nr in _wStacji.Keys.Union(_gdzie.Keys).ToList())
        {
            var g = _wStacji.GetValueOrDefault(nr);
            int przed = _gdzie.GetValueOrDefault(nr), po;
            // długi skład potrafi na chwilę zniknąć z danych moda – z „w stacji” znika dopiero po 5 odczytach z rzędu (~10 s)
            if ((g == null || g.Count == 0) && przed == 2 && (_znikniety[nr] = _znikniety.GetValueOrDefault(nr) + 1) < 5) { teraz[nr] = 2; continue; }
            if (g != null && g.Count > 0) _znikniety.Remove(nr);
            if (g == null || g.Count == 0) { po = 0; _znikniety.Remove(nr); }
            else
            {
                bool st = g.Any(ObiektStacji), sz = g.Any(ObiektSzlaku);
                switch (przed)
                {
                    case 0: case 1:
                        po = st ? 2 : 1;
                        if (po == 1 && !_pierwszyStan && JedzieDoNas(nr)) nowe |= PoznajSzlakowe(g);
                        if (po == 2)
                        {
                            var ogon = new HashSet<string>(g.Where(ObiektSzlaku), StringComparer.OrdinalIgnoreCase);
                            if (przed == 1 && _poprzednio.TryGetValue(nr, out var pp)) ogon.UnionWith(pp.Where(o => !ObiektStacji(o)));
                            _ogonPrzyjazdu[nr] = ogon;
                        }
                        break;
                    case 2:
                        if (!sz) { _zjechalZeSzlaku.Add(nr); po = 2; }
                        else
                        {
                            var ogon = _ogonPrzyjazdu.GetValueOrDefault(nr);
                            po = _zjechalZeSzlaku.Contains(nr) || g.Where(ObiektSzlaku).Any(o => ogon == null || !ogon.Contains(o)) ? 3 : 2;
                        }
                        break;
                    default:
                        po = st && !sz ? 2 : 3;   // wrócił całym składem do stacji (manewry)
                        break;
                }
            }
            if (po == 0) { _ogonPrzyjazdu.Remove(nr); _zjechalZeSzlaku.Remove(nr); _poprzednio.Remove(nr); _skad.Remove(nr); _dokad.Remove(nr); _wjazdNieznany.Remove(nr); }
            else { _poprzednio[nr] = g; teraz[nr] = po; if (po == 3) _zjechalZeSzlaku.Remove(nr); }
            if (_pierwszyStan || przed == po) continue;

            if (przed == 0 && po == 1)
            {
                // pociąg do nas wjechał na szlak dojazdowy: zgoda → „na szlaku” (czerwone)
                foreach (var s in _szlaki.Wszystkie)
                    foreach (var p in s.Pociagi.Where(p => p.Nr == nr && !p.OdNas && p.Stan == StanOkna.Zgoda))
                    { p.Stan = StanOkna.NaSzlaku; p.Zmiana = DateTime.Now; zm = true; Zdarzenie($"{Teraz():HH:mm}  {nr} wjechał na szlak od {s.Sasiad} (z gry)"); }
                if (_ust.Awizacja && _ust.AwizacjaTryb != "min") Awizuj(nr);
            }
            else if (przed < 2 && po == 2) { Przyjazd(nr); zm = true; }
            else if (przed == 2 && (po == 3 || po == 0)) { Odjazd(nr); zm = true; }
        }
        _pierwszyStan = false;
        _gdzie.Clear(); foreach (var kv in teraz) _gdzie[kv.Key] = kv.Value;
        if (nowe) ZapiszUst();
        if (zm) OdswiezSchemat();
    }

    int? TorDla(string sasiad, string nr)
    {
        var t = _szlaki.Znajdz(sasiad, nr)?.Tor ?? _wpisy.LastOrDefault(x => x.Pociag == nr && x.Tor.HasValue && x.Sasiad == sasiad)?.Tor;
        if (t == null && _ust.TorDomyslny) { var tory = _szlaki.Daj(sasiad).Tory; if (tory.Count > 0) t = tory[0]; }
        return t;
    }

    /// <summary>Awizacja: zapytanie o drogę wolną do następnego posterunku wg rozkładu (raz na pociąg i posterunek).</summary>
    void Awizuj(string nr)
    {
        var r = _plan.FirstOrDefault(x => x.Pociag == nr);
        var y = r?.SasiadDokad;
        if (y == null || string.IsNullOrEmpty(r.Odjazd) || !Sasiedzi().Contains(y) || !_ust.Automat(y)) return;
        if (_szlaki.Znajdz(y, nr) != null || !_awizowane.Add($"{nr}|{y}")) return;
        Nadaj(y, "TrainRequestMessage", nr, TorDla(y, nr), null, null, null, auto: true);
    }

    void Odjazd(string nr)
    {
        // kierunek: szlak, na który pociąg wjechał w grze; dalej zgoda / zapytanie z czatu; na końcu rozkład
        var (yGry, torGry) = SzlakZGry(nr, odNas: true);
        var zgoda = _szlaki.Wszystkie.SelectMany(s => s.Pociagi.Where(p => p.Nr == nr && p.OdNas && p.Stan is StanOkna.Zgoda or StanOkna.Zapytanie).Select(p => (s.Sasiad, p))).FirstOrDefault();
        var y = yGry ?? zgoda.Sasiad ?? _plan.FirstOrDefault(x => x.Pociag == nr)?.SasiadDokad;
        if (y == null || !Sasiedzi().Contains(y))
        {
            Zdarzenie($"{Teraz():HH:mm}  {nr} opuścił stację – nie wiem dokąd (brak zgody, rozkładu i poznanego odcinka szlaku), „odjechał” nie wysłany"
                      + $"  [odcinki: {string.Join(" ", _wStacji.GetValueOrDefault(nr) ?? new())}]");
            return;
        }
        if (_szlaki.Znajdz(y, nr) is { OdNas: true, Stan: StanOkna.NaSzlaku }) return;
        // ten sam „odjechał” tego pociągu do tego sąsiada najwyżej raz na 10 min (ręcznie albo automatem)
        if (_wpisy.Any(w => w.Wychodzacy && w.Klasa == "TrainDepartedMessage" && w.Pociag == nr && w.Do == y && (DateTime.Now - w.Utworzony).TotalMinutes < 10))
            return;
        // tor: z gry, z czatu; domyślny tor szlaku tylko gdy kierunek nie pochodzi z gry (inaczej lepiej bez toru niż zły)
        var tor = torGry ?? (yGry != null ? TorZCzatu(y, nr) : TorDla(y, nr));
        Zdarzenie($"{Teraz():HH:mm}  {nr} opuścił stację w stronę {y} (z gry)");
        if (_ust.AutoOdjazd && _ust.Automat(y)) Nadaj(y, "TrainDepartedMessage", nr, tor, Teraz().ToString("HH:mm"), null, null, auto: true);
        else _szlaki.Ustaw(_szlaki.Daj(y), nr, true, StanOkna.NaSzlaku, tor);
    }

    void Przyjazd(string nr)
    {
        foreach (var (s, _) in _szlaki.Przyjechal(nr, odNas: false))
        {
            Zdarzenie($"{Teraz():HH:mm}  {nr} przyjechał od {s} (z gry)");
            if (_ust.AutoPrzyjazd && _ust.Automat(s)) Nadaj(s, "TrainArrivedMessage", nr, null, Teraz().ToString("HH:mm"), null, null, auto: true);
        }
    }

    void AwizacjaWgRozkladu()
    {
        if (!_ust.Awizacja || _ust.AwizacjaTryb != "min" || !StacjaZGry || (DateTime.Now - _ostatniaAwizacjaMin).TotalSeconds < 10) return;
        _ostatniaAwizacjaMin = DateTime.Now;
        var teraz = Teraz();
        foreach (var r in _plan.Where(r => !string.IsNullOrEmpty(r.Odjazd) && _wStacji.ContainsKey(r.Pociag)).ToList())
        {
            var min = (r.Czas - teraz).TotalMinutes;
            if (min >= 0 && min <= _ust.AwizacjaMin) Awizuj(r.Pociag);
        }
    }

    /// <summary>Pociąg wysłany do sąsiada dojechał: indeks następnego punktu rozkładu (API) jest już za posterunkiem sąsiednim.</summary>
    async Task PrzyjazdyZApi()
    {
        var serwer = _cbSerwer.Text?.Trim();
        var wyslane = _szlaki.Wszystkie.SelectMany(s => s.Pociagi.Where(p => p.OdNas && p.Stan == StanOkna.NaSzlaku).Select(p => (s.Sasiad, p))).ToList();
        if (_demo || _api || wyslane.Count == 0 || string.IsNullOrEmpty(serwer) || (DateTime.Now - _ostatnieApi).TotalSeconds < 20) return;
        _api = true; _ostatnieApi = DateTime.Now;
        try
        {
            var poz = await RozkladSimRail.PozycjeAsync(serwer, CancellationToken.None);
            var st = Stacja; bool zm = false;
            foreach (var (s, p) in wyslane)
            {
                var t = _rozklad.FirstOrDefault(x => x.Nr == p.Nr);
                bool dojechal;
                if (!poz.TryGetValue(p.Nr, out var idx)) dojechal = (DateTime.Now - p.Zmiana).TotalMinutes > 10;   // zniknął z serwera
                else
                {
                    int i = t?.Punkty.FindIndex(x => RozkladSimRail.Pasuje(x.Nazwa, st)) ?? -1;
                    int j = i < 0 ? -1 : t.Punkty.FindIndex(i + 1, x => RozkladSimRail.Pasuje(x.Nazwa, s));
                    while (j >= 0 && j + 1 < t.Punkty.Count && RozkladSimRail.Pasuje(t.Punkty[j + 1].Nazwa, s)) j++;   // „Rogów”, „Rogów PZS …”
                    // VDDelayedTimetableIndex = indeks NASTĘPNEGO punktu (jeszcze nieosiągniętego) – minął sąsiada, gdy idx > j
                    dojechal = j >= 0 && idx > j;
                }
                if (!dojechal) continue;
                _szlaki.Daj(s).Pociagi.Remove(p); zm = true;
                Zdarzenie($"{Teraz():HH:mm}  {p.Nr} dojechał do {s} (pozycja z API SimRail)");
            }
            if (zm) OdswiezSchemat();
        }
        catch (Exception ex) { _planStatus.Text = "Pozycje pociągów niedostępne: " + ex.Message; }
        finally { _api = false; }
    }

    // ------------------------------------------------------------------ rozkład / plan ruchu

    async Task WczytajSerwery()
    {
        try
        {
            var lista = await RozkladSimRail.SerweryAsync(CancellationToken.None);
            var tekst = _cbSerwer.Text;
            _cbSerwer.ItemsSource = lista.Select(x => x.Kod).ToList();
            _cbSerwer.Text = tekst;
            _planStatus.Text = $"serwerów: {lista.Count}" + (string.IsNullOrEmpty(tekst) ? " – wybierz serwer, na którym grasz" : "");
            if (!string.IsNullOrEmpty(tekst)) await PobierzRozklad();
        }
        catch (Exception ex) { _planStatus.Text = "Lista serwerów niedostępna: " + ex.Message; }
    }

    async Task PobierzRozklad()
    {
        var serwer = _cbSerwer.Text?.Trim();
        if (string.IsNullOrEmpty(serwer)) { _planStatus.Text = "Podaj kod serwera."; return; }
        if (_pobieranie || _demo) return;
        _pobieranie = true; _planStatus.Text = "pobieranie rozkładu…";
        try
        {
            _rozklad = await RozkladSimRail.PobierzAsync(serwer, CancellationToken.None);
            _rozkladPobrany = DateTime.Now; _ust.Serwer = serwer; ZapiszUst();
            ListaPosterunkow();
            ZbudujPlan(); OdswiezSchemat(); OdswiezEzp();
        }
        catch (Exception ex) { _planStatus.Text = "Rozkład niedostępny: " + ex.Message; }
        finally { _pobieranie = false; }
    }

    void ListaPosterunkow()
    {
        var tekst = _cbPosterunek.Text;
        var nazwy = _rozklad.SelectMany(t => t.Punkty).Where(p => p.Postoj || !string.IsNullOrEmpty(p.Tor)).Select(p => p.Nazwa)
            .Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.Create(new CultureInfo("pl-PL"), true));
        _cbPosterunek.ItemsSource = new[] { ZGry }.Concat(nazwy).ToList();
        _cbPosterunek.Text = tekst;
    }

    internal void UstawRozklad(List<RozkladSimRail.Pociag> r) { _rozklad = r; _rozkladPobrany = DateTime.Now; ListaPosterunkow(); ZbudujPlan(); OdswiezSchemat(); OdswiezEzp(); }

    void ZbudujPlan()
    {
        var stacja = Stacja;
        _plan.Clear();
        if (string.IsNullOrEmpty(stacja) || _rozklad.Count == 0)
        {
            _planStatus.Text = _rozklad.Count == 0 ? "brak rozkładu – wybierz serwer i „Pobierz rozkład”" : "nieznany posterunek – wybierz go w belce";
            return;
        }
        var sasiedzi = Sasiedzi();
        string Sasiad(RozkladSimRail.Pociag t, int od, int krok)
        {
            for (int j = od, n = 0; j >= 0 && j < t.Punkty.Count && n < 10; j += krok, n++)
            {
                var m = sasiedzi.FirstOrDefault(s => RozkladSimRail.Pasuje(t.Punkty[j].Nazwa, s));
                if (m != null) return m;
            }
            return null;
        }
        var teraz = Teraz();
        var wiersze = new List<PozycjaPlanu>();
        foreach (var t in _rozklad)
        {
            int i = t.Punkty.FindIndex(p => RozkladSimRail.Pasuje(p.Nazwa, stacja));
            if (i < 0) continue;
            int k = i; while (k + 1 < t.Punkty.Count && RozkladSimRail.Pasuje(t.Punkty[k + 1].Nazwa, stacja)) k++;
            var p0 = t.Punkty[i]; var p1 = t.Punkty[k];
            var czas = p1.Odjazd ?? p0.Przyjazd ?? p0.Odjazd;
            if (czas == null) continue;
            var min = (czas.Value - teraz).TotalMinutes;
            if (min < -60 || min > 240) continue;
            string skad = i > 0 ? t.Punkty[i - 1].Nazwa : t.Od, dokad = k + 1 < t.Punkty.Count ? t.Punkty[k + 1].Nazwa : t.Do;
            bool postoj = t.Punkty.Skip(i).Take(k - i + 1).Any(p => p.Postoj) || i == 0 || k == t.Punkty.Count - 1;
            string sSkad = i > 0 ? Sasiad(t, i - 1, -1) : null, sDokad = k + 1 < t.Punkty.Count ? Sasiad(t, k + 1, 1) : null;
            wiersze.Add(new PozycjaPlanu
            {
                Pociag = t.Nr, Kategoria = string.IsNullOrEmpty(p0.Rodzaj) ? t.Kategoria : p0.Rodzaj, Czas = czas.Value,
                Przyjazd = i == 0 ? "" : p0.Przyjazd?.ToString("HH:mm") ?? "", Odjazd = k == t.Punkty.Count - 1 ? "" : p1.Odjazd?.ToString("HH:mm") ?? "",
                PO = postoj ? "p" : "o", Tor = p0.Tor ?? "", Peron = p0.Peron ?? "",
                Skad = sSkad ?? skad, Dokad = sDokad ?? dokad, SasiadSkad = sSkad, SasiadDokad = sDokad, Relacja = $"{t.Od} – {t.Do}",
                Lokomotywa = t.Lok, Dlugosc = t.Dlugosc, Brutto = t.Brutto,
                Zgoda = _zgody.GetValueOrDefault(t.Nr), WStacji = WStacji(t.Nr),
            });
        }
        foreach (var w in wiersze.OrderBy(x => x.Czas)) _plan.Add(w);
        _planStatus.Text = $"{stacja}: {_plan.Count} poc. (−1 h … +4 h), rozkład z {_rozkladPobrany:HH:mm}";
    }

    // ------------------------------------------------------------------ takt 1 s

    void Takt()
    {
        var t = Teraz();
        _zegar.Text = $"{t:HH:mm:ss}";
        bool pol = _demo || (_most?.Polaczony ?? false);
        _status.Text = _demo ? "tryb pokazowy" : pol ? $"gra: {(_posterunek.Length > 0 ? _posterunek : "–")}" : "brak połączenia z grą";
        _status.Foreground = pol ? Brushes.DarkGreen : Brushes.DarkRed;
        _tytul.Text = $"Terminal Stacji Stycznej – {(Stacja.Length > 0 ? Stacja : "?")}" + (StacjaZGry ? "" : "  (podgląd)");
        if (!_demo && _rozkladPobrany != DateTime.MinValue && (DateTime.Now - _rozkladPobrany).TotalMinutes > 5) _ = PobierzRozklad();
        if (_szlaki.Sprzataj()) OdswiezSchemat();
        AwizacjaWgRozkladu();
        _ = PrzyjazdyZApi();
        Alarmy(pol);
    }

    void Alarmy(bool pol)
    {
        var a = new List<string>();
        if (!pol) a.Add("Brak połączenia z grą (mod SimRailMost, port 47131).");
        foreach (var w in _wpisy.Where(x => !x.Wychodzacy && !x.Odpowiedziano && x.Klasa == "TrainRequestMessage" && (DateTime.Now - x.Utworzony).TotalMinutes is > 2 and < 30))
            a.Add($"{w.Czas}  {w.Od} czeka na odpowiedź: {w.Tekst}");
        foreach (var s in _szlaki.Wszystkie) foreach (var p in s.Pociagi.Where(p => p.Stan == StanOkna.Odmowa && p.OdNas))
            a.Add($"Odmowa od {s.Sasiad} dla pociągu {p.Nr}.");
        foreach (var s in _szlaki.Wszystkie) foreach (var p in s.Pociagi.Where(p => p.Stan == StanOkna.Stoj))
            a.Add($"STÓJ – pociąg {p.Nr} (szlak {s.Sasiad}).");
        if (a.SequenceEqual(_alarmy)) return;
        _alarmy.Clear(); foreach (var x in a) _alarmy.Add(x);
        ((TextBlock)_zakladki["Alarmy"].Naglowek.Child).Text = a.Count > 0 ? $"Alarmy ({a.Count})" : "Alarmy";
    }

    DateTime Teraz() => DateTime.TryParse(_czasGry, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : DateTime.Now;

    static string Godzina(string s) => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.ToString("HH:mm") : null;

    static string Skrot(string s) => string.IsNullOrEmpty(s) ? "" : s.Length <= 3 ? s : s[..3];

    void ZapiszUst() { if (!_demo) _ust.Zapisz(); }

    /// <summary>
    /// Odtworzenie bez gry (tryb pokazowy, nic nie idzie do gry): zajętości moda (zajetosci_serwer_*.csv) i czat (polecenia_*.jsonl)
    /// od – do (czas komputera) przez Stan / Czat jak na żywo. Własne „odjechał” z zapisu są pomijane – automat ma je wysłać sam.
    /// Wynik: zdarzenia (od najstarszego) do pliku.
    /// </summary>
    internal void Odtworz(string zajetosci, string polecenia, string posterunek, string[] sasiedzi, string od, string doCzasu, string wynik)
    {
        _ust.ToryReczne = Ustawienia.Wczytaj().ToryReczne;
        _ust.AutoOdjazd = true;
        _zdarzenia.Clear(); _wpisy.Clear(); _szlaki.Wyczysc(); _sasiedzi.Clear();
        var zdarz = new List<(string Pc, int Rodzaj, string[] Kol, string Json)>();
        foreach (var l in File.ReadLines(zajetosci).Skip(1))
        {
            var k = l.TrimStart('﻿').Split(';');
            if (k.Length >= 7 && string.CompareOrdinal(k[0], od) >= 0 && string.CompareOrdinal(k[0], doCzasu) <= 0) zdarz.Add((k[0], 0, k, null));
        }
        foreach (var l in File.ReadLines(polecenia))
        {
            try
            {
                using var d = JsonDocument.Parse(l.TrimStart('﻿'));
                var r = d.RootElement;
                if (MostKlient.S(r, "metoda") != "Czat") continue;
                var pc = MostKlient.S(r, "czasPc") ?? "";
                if (string.CompareOrdinal(pc, od) < 0 || string.CompareOrdinal(pc, doCzasu) > 0) continue;
                var js = r.GetProperty("arg").GetProperty("json").GetString();
                using var c = JsonDocument.Parse(js);
                if (MostKlient.S(c.RootElement, "kier") == "wych" && MostKlient.S(c.RootElement, "klasa") == "TrainDepartedMessage") continue;
                zdarz.Add((pc, 1, null, js));
            }
            catch { }
        }
        var zaj = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        string czasGry = "";
        void Migawka()
        {
            using var ms = new MemoryStream();
            using (var w = new Utf8JsonWriter(ms))
            {
                w.WriteStartObject(); w.WriteString("typ", "lacznosc_stan"); w.WriteString("posterunek", posterunek); w.WriteString("stacja", posterunek);
                w.WriteString("czasGry", czasGry);
                w.WriteStartArray("sasiedzi"); foreach (var s in sasiedzi) { w.WriteStartObject(); w.WriteString("n", s); w.WriteBoolean("gracz", true); w.WriteEndObject(); } w.WriteEndArray();
                w.WriteStartArray("pociagi");
                foreach (var g in zaj.SelectMany(kv => kv.Value.Select(nr => (nr, o: kv.Key))).GroupBy(x => x.nr))
                {
                    w.WriteStartObject(); w.WriteString("nr", g.Key);
                    w.WriteStartArray("gdzie"); foreach (var x in g.Take(8)) w.WriteStringValue(x.o); w.WriteEndArray();
                    w.WriteEndObject();
                }
                w.WriteEndArray(); w.WriteEndObject();
            }
            using var doc = JsonDocument.Parse(ms.ToArray());
            Stan(doc.RootElement);
        }
        string sekunda = null;
        foreach (var e in zdarz.OrderBy(e => e.Pc, StringComparer.Ordinal))
        {
            var s = e.Pc.Length >= 19 ? e.Pc[..19] : e.Pc;
            if (sekunda != null && s != sekunda) Migawka();
            sekunda = s;
            if (e.Rodzaj == 0)
            {
                var k = e.Kol; czasGry = k[1];
                if (k[5] == "WOLNY") zaj.Remove(k[3]);
                else if (k[5] == "ZAJETY" && k[6].Length > 0) zaj[k[3]] = k[6].Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            }
            else
            {
                // czas odbioru = teraz, żeby nie był uznany za stary komunikat z historii
                var js = Regex.Replace(e.Json, "\"czasPc\":\"[^\"]*\"", $"\"czasPc\":\"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\"");
                using var c = JsonDocument.Parse(js);
                Czat(c.RootElement);
            }
        }
        Migawka();
        File.WriteAllLines(wynik, _zdarzenia.Reverse());
    }

    // ------------------------------------------------------------------ okna

    void PokazRozmowe(string sasiad)
    {
        if (_oknoRozmowy == null)
        {
            _oknoRozmowy = new OknoRozmowy(_wpisy, _sasiedzi) { Owner = this };
            _oknoRozmowy.KliknietoWpis += w =>
            {
                if (string.IsNullOrEmpty(w.Sasiad)) return;
                WybierzSasiada(w.Sasiad, zakladkaEzp: true);
                if (!w.Wychodzacy) { _pytanie = w; PokazOdpowiedzi(); }
                if (!string.IsNullOrEmpty(w.Pociag)) { _torRecznie = false; _tbPociag.Text = w.Pociag; }
            };
            _oknoRozmowy.Wyczysc += () =>
            {
                _wpisy.Clear(); _wyslane.Clear(); _pytanie = null;
                foreach (var s in _sasiedzi) { s.Nieprzeczytane = 0; s.Oczekujace = 0; }
                OdswiezSchemat(); OdswiezEzp();
            };
        }
        _oknoRozmowy.Pokaz(sasiad);
    }

    internal void PokazZbiorczy() => PokazRozmowe(null);

    void Opcje()
    {
        var strony = Sasiedzi().Distinct().ToDictionary(s => s, s => _szlaki.Daj(s).Lewa);
        var o = new OknoOpcji(_ust, strony.Keys, strony) { Owner = this };
        if (o.ShowDialog() != true) return;
        foreach (var (s, lewa) in strony) _ust.Strony[Klucz(s)] = lewa;
        ZapiszUst(); OdswiezSchemat();
    }

    // ------------------------------------------------------------------ tryb pokazowy

    internal void UstawStanPokazowy(string posterunek, string stacja, string czasGry, IEnumerable<Sasiad> sasiedzi, IEnumerable<(string nr, string gdzie)> pociagi)
    {
        _posterunek = posterunek; _stacjaGry = stacja; _czasGry = czasGry;
        foreach (var s in sasiedzi) _sasiedzi.Add(s);
        foreach (var (nr, gdzie) in pociagi) _wStacji[nr] = new() { gdzie };
    }
    internal void DodajWpis(Wpis w) { _wpisy.Add(w); _szlaki.Zastosuj(w); }
    internal void Wybierz(string sasiad, string klasa, string pociag)
    {
        OdswiezSchemat();
        WybierzSasiada(sasiad, zakladkaEzp: true);
        WybierzRodzaj(klasa);
        _tbPociag.Text = pociag;
    }

    // ------------------------------------------------------------------ tabela planu

    void ZbudujTabele()
    {
        _dgPlan.ItemsSource = _plan;
        _dgPlan.AutoGenerateColumns = false; _dgPlan.IsReadOnly = true; _dgPlan.HeadersVisibility = DataGridHeadersVisibility.Column;
        _dgPlan.Background = Brushes.Black; _dgPlan.RowBackground = Brushes.Black; _dgPlan.AlternatingRowBackground = Wyglad.P("#0A0F18");
        _dgPlan.Foreground = Wyglad.Zielony; _dgPlan.BorderBrush = Wyglad.P("#303030"); _dgPlan.GridLinesVisibility = DataGridGridLinesVisibility.Vertical;
        _dgPlan.VerticalGridLinesBrush = Wyglad.P("#1E3A5A"); _dgPlan.FontFamily = Wyglad.Mono; _dgPlan.FontSize = 13;
        var naglowekStyl = new Style(typeof(DataGridColumnHeader));
        naglowekStyl.Setters.Add(new Setter(BackgroundProperty, Brushes.Black));
        naglowekStyl.Setters.Add(new Setter(ForegroundProperty, Wyglad.Cyjan));
        naglowekStyl.Setters.Add(new Setter(PaddingProperty, new Thickness(6, 2, 6, 2)));
        naglowekStyl.Setters.Add(new Setter(BorderBrushProperty, Wyglad.Cyjan));
        naglowekStyl.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
        _dgPlan.ColumnHeaderStyle = naglowekStyl;
        var wierszStyl = new Style(typeof(DataGridRow));
        var wStacji = new DataTrigger { Binding = new Binding(nameof(PozycjaPlanu.WStacji)), Value = true };
        wStacji.Setters.Add(new Setter(BackgroundProperty, Wyglad.P("#3A1E12")));
        wStacji.Setters.Add(new Setter(ForegroundProperty, Wyglad.Pomaranczowy));
        wierszStyl.Triggers.Add(wStacji);
        _dgPlan.RowStyle = wierszStyl;
        void K(string n, string p, double w = double.NaN) => _dgPlan.Columns.Add(new DataGridTextColumn { Header = n, Binding = new Binding(p), Width = double.IsNaN(w) ? DataGridLength.Auto : new DataGridLength(w) });
        K("Pociąg", nameof(PozycjaPlanu.Pociag), 80);
        K("p/o", nameof(PozycjaPlanu.PO), 40);
        K("Przyj.", nameof(PozycjaPlanu.Przyjazd), 62);
        K("Odj.", nameof(PozycjaPlanu.Odjazd), 62);
        K("Tor", nameof(PozycjaPlanu.Tor), 46);
        K("Peron", nameof(PozycjaPlanu.Peron), 56);
        K("Skąd", nameof(PozycjaPlanu.Skad), 170);
        K("Dokąd", nameof(PozycjaPlanu.Dokad), 170);
        K("Zgoda", nameof(PozycjaPlanu.Zgoda), 100);
        K("Rodz", nameof(PozycjaPlanu.Kategoria), 60);
        K("Relacja", nameof(PozycjaPlanu.Relacja), 240);
        K("Dług", nameof(PozycjaPlanu.Dlugosc), 56);
        K("Brutto", nameof(PozycjaPlanu.Brutto), 64);
        K("Lokomotywa", nameof(PozycjaPlanu.Lokomotywa), 110);
        _dgPlan.MouseDoubleClick += (_, _) =>
        {
            if (_dgPlan.SelectedItem is not PozycjaPlanu r) return;
            var s = r.SasiadDokad ?? r.SasiadSkad;
            if (s != null) WybierzSasiada(s, zakladkaEzp: true); else PokazZakladke("EZP");
            _torRecznie = false;
            _tbPociag.Text = r.Pociag;
        };
        _dgPlan.ToolTip = "Dwuklik – pociąg do EZP (posterunek wg kierunku). Pomarańczowe – pociąg w stacji (z gry).";
    }
}

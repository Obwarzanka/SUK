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
    readonly WrapPanel _odpPrzyciski = new(), _pocPrzyciski = new(), _rodzPrzyciski = new();
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

        // EZP: formularz po lewej, ostatni komunikat / odpowiedzi / podgląd po prawej
        var ezp = new Grid();
        ezp.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        ezp.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        var form = new Grid();
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(86) });
        form.ColumnDefinitions.Add(new ColumnDefinition());
        Para Wiersz(string et, FrameworkElement pole)
        {
            int w = form.RowDefinitions.Count;
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = Wyglad.Etykieta(et); l.Margin = new Thickness(0, 6, 6, 0); l.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetRow(l, w); form.Children.Add(l);
            pole.Margin = new Thickness(0, 3, 0, 3);
            Grid.SetRow(pole, w); Grid.SetColumn(pole, 1); form.Children.Add(pole);
            return new Para(l, pole);
        }
        _ezpTytul.Foreground = Wyglad.Cyjan; _ezpTytul.FontSize = 14; _ezpTytul.Margin = new Thickness(0, 0, 0, 4);
        form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetColumnSpan(_ezpTytul, 2); form.Children.Add(_ezpTytul);

        Wyglad.Pole(_tbPociag); _tbPociag.Width = 90; _tbPociag.FontSize = 15; _tbPociag.Height = 24;
        _tbPociag.ToolTip = "Numer pociągu – dowolny, także spoza rozkładu; przyciski obok – pociągi tego szlaku z rozkładu i ze szlaku";
        _tbPociag.TextChanged += (_, _) => { TorZNumeru(); Podglad(); };
        var poc = new DockPanel(); DockPanel.SetDock(_tbPociag, Dock.Left); poc.Children.Add(_tbPociag);
        _pocPrzyciski.Margin = new Thickness(8, 0, 0, 0); poc.Children.Add(_pocPrzyciski);
        Wiersz("Pociąg", poc);
        _tor.Zmieniono += () => { _torRecznie = true; Podglad(); };
        _tor.ToolTip = "Tor szlakowy w komunikacie (tory tego szlaku z komunikatów gry); Alt+0 – bez, Alt+1… – kolejne";
        Wiersz("Tor", _tor);
        Wiersz("Komunikat", _rodzPrzyciski);
        _cbPowod.ItemsSource = Teksty.Powody.Select(x => x.Opis).ToList(); _cbPowod.SelectedIndex = 0; _cbPowod.Width = 320; _cbPowod.HorizontalAlignment = HorizontalAlignment.Left;
        _cbPowod.SelectionChanged += (_, _) => Podglad();
        _wierszPowod = Wiersz("Powód", _cbPowod);
        Wyglad.Pole(_tbCzas); _tbCzas.Width = 70; _tbCzas.HorizontalAlignment = HorizontalAlignment.Left;
        _tbCzas.TextChanged += (_, _) => Podglad();
        _wierszCzas = Wiersz("Godzina", _tbCzas);
        var tekstWiersz = new DockPanel();
        var wyslijTekst = Wyglad.Przycisk("Wyślij tekst", WyslijTekst, "Tekst własny do tego posterunku (Enter)");
        wyslijTekst.Margin = new Thickness(6, 0, 0, 0);
        DockPanel.SetDock(wyslijTekst, Dock.Right); tekstWiersz.Children.Add(wyslijTekst);
        Wyglad.Pole(_tbTekst); _tbTekst.Foreground = Brushes.White; _tbTekst.FontFamily = tahoma; _tbTekst.Height = 24;
        _tbTekst.KeyDown += (_, e) => { if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None) { WyslijTekst(); e.Handled = true; } };
        tekstWiersz.Children.Add(_tbTekst);
        Wiersz("Tekst", tekstWiersz);
        _btnWyslij = Wyglad.Przycisk("Wyślij  (Ctrl+Enter)", Wyslij);
        _btnWyslij.HorizontalAlignment = HorizontalAlignment.Left; _btnWyslij.Padding = new Thickness(18, 4, 18, 4); _btnWyslij.FontSize = 13;
        var anuluj = Wyglad.Przycisk("Anuluj pociąg", () => AnulujPociag(_wybrany, _tbPociag.Text.Trim()),
            "Zdejmuje pociąg z okienka tego szlaku bez wysyłania komunikatu (np. pomyłka, pociąg usunięty z gry)");
        anuluj.Margin = new Thickness(10, 0, 0, 0); anuluj.Padding = new Thickness(10, 4, 10, 4);
        var wysylka = new StackPanel { Orientation = Orientation.Horizontal };
        wysylka.Children.Add(_btnWyslij); wysylka.Children.Add(anuluj);
        Wiersz("", wysylka);
        ezp.Children.Add(form);

        var prawa = new StackPanel { Margin = new Thickness(14, 0, 0, 0) };
        prawa.Children.Add(Wyglad.Etykieta("Ostatni komunikat posterunku"));
        _ostatni.TextWrapping = TextWrapping.Wrap; _ostatni.FontSize = 13;
        _ostatniRamka.BorderBrush = Wyglad.P("#404040"); _ostatniRamka.BorderThickness = new Thickness(1); _ostatniRamka.Padding = new Thickness(6, 4, 6, 4);
        _ostatniRamka.Margin = new Thickness(0, 2, 0, 6); _ostatniRamka.MinHeight = 44; _ostatniRamka.Child = _ostatni;
        prawa.Children.Add(_ostatniRamka);
        prawa.Children.Add(_odpPrzyciski);
        prawa.Children.Add(Wyglad.Etykieta("Podgląd – tak pójdzie do gry"));
        _podglad.Foreground = Wyglad.Zolty; _podglad.TextWrapping = TextWrapping.Wrap; _podglad.FontSize = 13; _podglad.Margin = new Thickness(0, 2, 0, 8);
        prawa.Children.Add(_podglad);
        _wynik.TextWrapping = TextWrapping.Wrap; _wynik.Foreground = Wyglad.Szary;
        prawa.Children.Add(_wynik);
        Grid.SetColumn(prawa, 1); ezp.Children.Add(prawa);

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
        Automaty();
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
        _szlaki.Zastosuj(w);
        Zdarzenie($"{czas}  {(wych ? $"→ {doK}" : $"← {od}")}: {tekst}");
        if (s != null)
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
        if (_szlaki.Daj(sasiad).Pociagi.RemoveAll(p => p.Nr == nr) == 0) { Zdarzenie($"{Teraz():HH:mm}  {nr} – brak na szlaku do {sasiad}"); return; }
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
        foreach (var b in new[] { ok, auto, anuluj }) b.Margin = new Thickness(0, 0, 6, 0);
        var przyciski = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        przyciski.Children.Add(ok); przyciski.Children.Add(auto); przyciski.Children.Add(anuluj);
        var p = new StackPanel { Margin = new Thickness(12) };
        p.Children.Add(new TextBlock { Text = "Numery torów szlakowych (jak w komunikatach gry, np. „1 2”),\noddzielone spacją lub przecinkiem:" });
        p.Children.Add(pole); p.Children.Add(przyciski);
        okno.Content = p;
        okno.Loaded += (_, _) => { pole.Focus(); pole.SelectAll(); };
        okno.ShowDialog();
        if (wynik == null) return;
        if (wynik == true)
        {
            var tory = pole.Text.Split(new[] { ' ', ',', ';', '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => int.TryParse(x, out var n) ? n : 0).Where(n => n > 0).Distinct().OrderBy(n => n).ToList();
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
                s.Reczne = true; s.Tory.Clear(); s.Tory.AddRange(r.Distinct().OrderBy(x => x));
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
                Tag = k, Margin = new Thickness(0, 0, 4, 4), Focusable = false,
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
        _ezpTytul.Text = s == null ? "EZP – brak posterunków sąsiednich (połącz z grą albo wybierz posterunek i pobierz rozkład)"
            : $"EZP – {s}{(sa != null ? $"  ({sa.Obsada})" : "")}" + (StacjaZGry ? "" : "   [podgląd – wysyłanie tylko z posterunku w grze]");
        _tor.UstawTory(s != null && _szlaki.Daj(s).Tory.Count > 0 ? _szlaki.Daj(s).Tory : new List<int> { 1, 2 });

        // pociągi tego szlaku: na szlaku, z planu w tę stronę (−30 … +90 min)
        _pocPrzyciski.Children.Clear();
        if (s != null)
        {
            var teraz = Teraz();
            var nr = _szlaki.Daj(s).Pociagi.Select(p => p.Nr)
                .Concat(_plan.Where(r => (r.SasiadSkad == s || r.SasiadDokad == s) && (r.Czas - teraz).TotalMinutes is > -30 and < 90)
                             .OrderBy(r => Math.Abs((r.Czas - teraz).TotalMinutes)).Select(r => r.Pociag))
                .Distinct().Take(10).ToList();
            foreach (var n in nr)
            {
                var r = _plan.FirstOrDefault(x => x.Pociag == n);
                var b = Wyglad.Przycisk(n, () => { _torRecznie = false; _tbPociag.Text = n; _tbPociag.Focus(); _tbPociag.CaretIndex = n.Length; },
                    r == null ? "na szlaku" : $"{r.Czas:HH:mm} {r.Skad} → {r.Dokad}  {r.Kategoria}");
                if (r?.WStacji == true) b.Foreground = Wyglad.Pomaranczowy;
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
            _odpPrzyciski.Children.Add(Wyglad.Przycisk(etykieta, () =>
            {
                int? tor = zTorem ? w.Tor : null;
                bool komplet = nr.Length > 0 && powod != "";
                if (_ust.OdRazu && komplet && StacjaZGry) Nadaj(s, klasa, nr, tor, Teraz().ToString("HH:mm"), powod, null);
                else Przygotuj(klasa, nr, tor, powod);
            }));
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
    /// </summary>
    static bool ObiektStacji(string n) => !string.IsNullOrEmpty(n)
        && (Regex.IsMatch(n, @"^z\d") || Regex.IsMatch(n, @"^l\d+_[a-z]+_", RegexOptions.IgnoreCase) || n.Contains("_wk", StringComparison.OrdinalIgnoreCase));

    static bool SzlakZNazwy(string n) => Regex.IsMatch(n, @"^l\d+_it", RegexOptions.IgnoreCase);

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
            _szlakowe = new(_ust.ObiektySzlaku.TryGetValue(Stacja ?? "", out var l) ? l : new List<string>(), StringComparer.OrdinalIgnoreCase);
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
    readonly HashSet<string> _zjechalZeSzlaku = new();                      // w stacji, już bez żadnego odcinka szlaku
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
            if (g == null || g.Count == 0) po = 0;
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
            if (po == 0) { _ogonPrzyjazdu.Remove(nr); _zjechalZeSzlaku.Remove(nr); _poprzednio.Remove(nr); }
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
        var zgoda = _szlaki.Wszystkie.SelectMany(s => s.Pociagi.Where(p => p.Nr == nr && p.OdNas && p.Stan is StanOkna.Zgoda or StanOkna.Zapytanie).Select(p => (s.Sasiad, p))).FirstOrDefault();
        var y = zgoda.Sasiad ?? _plan.FirstOrDefault(x => x.Pociag == nr)?.SasiadDokad;
        if (y == null || !Sasiedzi().Contains(y)) return;
        if (_szlaki.Znajdz(y, nr) is { OdNas: true, Stan: StanOkna.NaSzlaku }) return;
        var tor = TorDla(y, nr);
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

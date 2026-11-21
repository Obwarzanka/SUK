using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Lcs.Lacznosc;

/// <summary>
/// Okno łączności: posterunki sąsiednie, rozmowa, EZP (opcje tekstowe – komunikaty gry z podpowiedzią numeru pociągu),
/// plan ruchu posterunku z rozkładu SimRail. Wygląd – ciemny terminal jak terminal stacji stycznej.
/// </summary>
public sealed class MainWindow : Window
{
    // ------------------------------------------------------------------ barwy
    static readonly Brush Tlo = Pedzel("#0B1020"), TloPanelu = Pedzel("#121A2E"), TloPola = Pedzel("#0E1528"), Ramka = Pedzel("#2A3552");
    static readonly Brush Tekst = Pedzel("#D8DEE9"), Szary = Pedzel("#8A94A8"), Cyjan = Pedzel("#4FD3E6"), Pomaranczowy = Pedzel("#FF8A3D");
    static readonly Brush Zielony = Pedzel("#6BFF6B"), Czerwony = Pedzel("#FF5A5A"), Naglowek = Pedzel("#E9ECF2"), Belka = Pedzel("#29B6D6");
    static readonly FontFamily Mono = new("Consolas");

    static Brush Pedzel(string hex) { var b = (Brush)new BrushConverter().ConvertFromString(hex); b.Freeze(); return b; }

    // ------------------------------------------------------------------ dane
    readonly MostKlient _most;
    readonly ObservableCollection<Sasiad> _sasiedzi = new();
    readonly ObservableCollection<Wpis> _wpisy = new();
    readonly ObservableCollection<Rodzaj> _rodzaje = new();
    readonly ObservableCollection<Podpowiedz> _podpowiedzi = new();
    readonly ObservableCollection<PozycjaPlanu> _plan = new();
    readonly ICollectionView _widokWpisow;
    readonly Dictionary<long, Wpis> _wyslane = new();
    readonly Dictionary<string, List<string>> _wStacji = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> _zgody = new(StringComparer.OrdinalIgnoreCase);
    List<RozkladSimRail.Pociag> _rozklad = new();
    string _posterunek = "", _stacjaGry = "", _czasGry = "";
    DateTime _rozkladPobrany = DateTime.MinValue;
    bool _pobieranie;
    readonly bool _demo;
    readonly Ustawienia _ust;

    // ------------------------------------------------------------------ kontrolki
    readonly TextBlock _tytul = new(), _zegar = new(), _status = new(), _ezpLicznik = new(), _rozmowaTytul = new(), _ezpOdbiorca = new();
    readonly TextBlock _wzor = new(), _planStatus = new(), _wynik = new();
    readonly ListBox _lbSasiedzi = new(), _lbWpisy = new(), _lbRodzaje = new(), _lbPodpowiedzi = new();
    readonly ComboBox _cbPociag = new(), _cbPowod = new(), _cbSerwer = new();
    readonly TextBox _tbTor = new(), _tbCzas = new(), _tbTekst = new(), _tbStacja = new();
    readonly FrameworkElement _wierszPociag, _wierszTor, _wierszCzas, _wierszPowod, _wierszTekst;
    readonly Border _panelEzp;
    readonly DataGrid _dgPlan = new();
    readonly TextBlock _odpTytul = new();
    readonly WrapPanel _odpPrzyciski = new();
    readonly Border _panelOdpowiedzi;
    readonly Button _btnWyslij;
    Wpis _pytanie;

    public MainWindow(bool demo)
    {
        _demo = demo;
        _ust = Ustawienia.Wczytaj();
        Title = "Łączność z posterunkami – SimRail";
        try { Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/app.ico")); }
        catch (Exception ex) { try { File.AppendAllText(Program.ErrorLog, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} ikona okna: {ex.Message}{Environment.NewLine}"); } catch { } }
        Width = 1400; Height = 900; MinWidth = 1000; MinHeight = 650;
        Background = Tlo; Foreground = Tekst; FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _widokWpisow = CollectionViewSource.GetDefaultView(_wpisy);
        _widokWpisow.Filter = o => _lbSasiedzi.SelectedItem is not Sasiad s || ((Wpis)o).Sasiad == s.Nazwa;

        var glowna = new Grid();
        glowna.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        glowna.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        glowna.RowDefinitions.Add(new RowDefinition { Height = new GridLength(290) });
        Content = glowna;

        // ---- nagłówek
        var naglowek = new DockPanel { Background = Naglowek, LastChildFill = false };
        _tytul.Text = "Łączność z posterunkami"; _tytul.FontSize = 18; _tytul.Foreground = Brushes.Black; _tytul.Margin = new Thickness(14, 6, 14, 6);
        _tytul.FontFamily = Mono; _tytul.VerticalAlignment = VerticalAlignment.Center;
        naglowek.Children.Add(_tytul);
        _status.Margin = new Thickness(10, 0, 0, 0); _status.VerticalAlignment = VerticalAlignment.Center; _status.Foreground = Brushes.DimGray;
        naglowek.Children.Add(_status);
        var zegarBox = new Border { Background = Brushes.Black, Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(6), Child = _zegar };
        _zegar.Foreground = Brushes.White; _zegar.FontFamily = Mono; _zegar.FontSize = 18; _zegar.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(zegarBox, Dock.Right); naglowek.Children.Add(zegarBox);
        // EZP – licznik O (odebrane nieprzeczytane) / W (wysłane bez odpowiedzi); klik – pokaż / ukryj opcje tekstowe
        var ezpTekst = new StackPanel();
        ezpTekst.Children.Add(new TextBlock { Text = "EZP", FontSize = 11, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Foreground = Brushes.White });
        ezpTekst.Children.Add(_ezpLicznik);
        _ezpLicznik.FontFamily = Mono; _ezpLicznik.FontSize = 16; _ezpLicznik.Foreground = Brushes.White; _ezpLicznik.Text = "O: 0   W: 0";
        var ezp = new Border { Background = Pedzel("#5B6478"), BorderBrush = Brushes.Silver, BorderThickness = new Thickness(1), Padding = new Thickness(10, 2, 10, 2),
            Margin = new Thickness(6), Child = ezpTekst, Cursor = Cursors.Hand, ToolTip = "EZP – opcje tekstowe (O: odebrane nieprzeczytane, W: wysłane bez odpowiedzi)" };
        ezp.MouseLeftButtonUp += (_, _) => _panelEzp.Visibility = _panelEzp.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        DockPanel.SetDock(ezp, Dock.Right); naglowek.Children.Add(ezp);
        glowna.Children.Add(naglowek);

        // ---- środek: sąsiedzi | rozmowa | EZP
        var srodek = new Grid { Margin = new Thickness(8) };
        srodek.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
        srodek.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        srodek.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetRow(srodek, 1); glowna.Children.Add(srodek);

        // sąsiedzi
        _lbSasiedzi.ItemsSource = _sasiedzi;
        _lbSasiedzi.ItemTemplate = SzablonSasiada();
        StylListy(_lbSasiedzi);
        _lbSasiedzi.SelectionChanged += (_, _) => WybranoSasiada();
        var lewyDok = new DockPanel();
        var lewyPrzyciski = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        lewyPrzyciski.Children.Add(Przycisk("☰ Widok zbiorczy (wszystkie)", WidokZbiorczy, "Rozmowa ze wszystkimi posterunkami naraz; odpowiedź idzie do posterunku klikniętego komunikatu"));
        lewyPrzyciski.Children.Add(Przycisk("Wyczyść posterunki", WyczyscPosterunki, "Usuwa listę; sąsiedzi obecnego posterunku wrócą z gry po kilku sekundach"));
        DockPanel.SetDock(lewyPrzyciski, Dock.Bottom); lewyDok.Children.Add(lewyPrzyciski); lewyDok.Children.Add(_lbSasiedzi);
        var lewy = Panel("Posterunki sąsiednie", lewyDok);
        srodek.Children.Add(lewy);

        // rozmowa
        _lbWpisy.ItemsSource = _widokWpisow;
        _lbWpisy.ItemTemplate = SzablonWpisu();
        StylListy(_lbWpisy);
        ScrollViewer.SetHorizontalScrollBarVisibility(_lbWpisy, ScrollBarVisibility.Disabled);
        _lbWpisy.SelectionChanged += (_, _) => PokazOdpowiedzi();   // klik w komunikat posterunku – odpowiedź na niego
        _rozmowaTytul.Text = "Rozmowa – wszystkie posterunki";
        var srodDok = new DockPanel();
        var czysc = Przycisk("Wyczyść czat", WyczyscCzat, "Czyści rozmowę w programie (czat w grze zostaje)");
        czysc.HorizontalAlignment = HorizontalAlignment.Right; czysc.Margin = new Thickness(0, 6, 0, 0);
        DockPanel.SetDock(czysc, Dock.Bottom); srodDok.Children.Add(czysc); srodDok.Children.Add(_lbWpisy);
        var srod = Panel(_rozmowaTytul, srodDok);
        srod.Margin = new Thickness(8, 0, 8, 0);
        Grid.SetColumn(srod, 1); srodek.Children.Add(srod);

        // EZP – opcje tekstowe
        var ezpPanel = new StackPanel { Width = 380 };
        _ezpOdbiorca.Foreground = Cyjan; _ezpOdbiorca.FontWeight = FontWeights.SemiBold; _ezpOdbiorca.Margin = new Thickness(0, 0, 0, 6);
        ezpPanel.Children.Add(_ezpOdbiorca);
        // podpowiedź odpowiedzi na ostatnie pytanie posterunku (np. „czy droga wolna?” → droga wolna / nie można przyjąć)
        _odpTytul.Foreground = Pomaranczowy; _odpTytul.TextWrapping = TextWrapping.Wrap; _odpTytul.Margin = new Thickness(0, 0, 0, 4);
        var odpStos = new StackPanel();
        odpStos.Children.Add(_odpTytul); odpStos.Children.Add(_odpPrzyciski);
        _panelOdpowiedzi = new Border { Child = odpStos, BorderBrush = Pomaranczowy, BorderThickness = new Thickness(1), Padding = new Thickness(6),
            Margin = new Thickness(0, 0, 0, 8), Background = Pedzel("#1E1A1A"), Visibility = Visibility.Collapsed };
        ezpPanel.Children.Add(_panelOdpowiedzi);
        _lbRodzaje.ItemsSource = _rodzaje;
        _lbRodzaje.DisplayMemberPath = nameof(Rodzaj.Etykieta);
        StylListy(_lbRodzaje);
        // rodzaje w dwóch kolumnach – pola pociągu i toru zostają widoczne bez przewijania
        _lbRodzaje.ItemsPanel = (ItemsPanelTemplate)System.Windows.Markup.XamlReader.Parse(
            "<ItemsPanelTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><UniformGrid Columns='2'/></ItemsPanelTemplate>");
        ScrollViewer.SetHorizontalScrollBarVisibility(_lbRodzaje, ScrollBarVisibility.Disabled);
        _lbRodzaje.FontSize = 12;
        _lbRodzaje.SelectionChanged += (_, _) => WybranoRodzaj();
        ezpPanel.Children.Add(_lbRodzaje);
        _wzor.Foreground = Szary; _wzor.TextWrapping = TextWrapping.Wrap; _wzor.Margin = new Thickness(0, 4, 0, 6); _wzor.FontStyle = FontStyles.Italic;
        ezpPanel.Children.Add(_wzor);

        _cbPociag.IsEditable = true; _cbPociag.ItemsSource = _podpowiedzi; _cbPociag.FontFamily = Mono; _cbPociag.FontSize = 15;
        TextSearch.SetTextPath(_cbPociag, nameof(Podpowiedz.Nr));
        _cbPociag.ItemTemplate = SzablonPodpowiedzi();
        _cbPociag.SelectionChanged += (_, _) => { if (_cbPociag.SelectedItem is Podpowiedz p) UzyjPodpowiedzi(p); };
        _cbPociag.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) => { OdswiezPodpowiedzi(); TorZNumeru(); }));
        _wierszPociag = Wiersz("Pociąg", _cbPociag);
        ezpPanel.Children.Add(_wierszPociag);
        // tor i godzina zaraz pod numerem pociągu (przed listą podpowiedzi – widoczne bez przewijania)
        StylPola(_tbTor); _tbTor.FontSize = 15; _wierszTor = Wiersz("Tor", _tbTor); ezpPanel.Children.Add(_wierszTor);
        _tbTor.ToolTip = "Tor – dla zapytania o drogę wolną, dania drogi wolnej i dania odjazdu (podpowiedź / plan ruchu wpisują go same)";
        StylPola(_tbCzas); _wierszCzas = Wiersz("Godzina", _tbCzas); ezpPanel.Children.Add(_wierszCzas);
        _lbPodpowiedzi.ItemsSource = _podpowiedzi;
        _lbPodpowiedzi.ItemTemplate = SzablonPodpowiedzi();
        StylListy(_lbPodpowiedzi);
        _lbPodpowiedzi.Height = 170;
        _lbPodpowiedzi.MouseLeftButtonUp += (_, _) => { if (_lbPodpowiedzi.SelectedItem is Podpowiedz p) { _cbPociag.Text = p.Nr; UzyjPodpowiedzi(p); } };
        var podpTytul = new TextBlock { Text = "Podpowiedzi: w stacji / z rozkładu wg godziny", Foreground = Szary, FontSize = 11, Margin = new Thickness(0, 2, 0, 2) };
        ezpPanel.Children.Add(podpTytul);
        ezpPanel.Children.Add(_lbPodpowiedzi);
        _wierszPociag.Tag = new FrameworkElement[] { podpTytul, _lbPodpowiedzi };

        _cbPowod.ItemsSource = Teksty.Powody; _cbPowod.DisplayMemberPath = "Opis"; _cbPowod.SelectedIndex = 0;
        _wierszPowod = Wiersz("Powód", _cbPowod); ezpPanel.Children.Add(_wierszPowod);
        StylPola(_tbTekst); _tbTekst.AcceptsReturn = true; _tbTekst.TextWrapping = TextWrapping.Wrap; _tbTekst.Height = 70;
        _wierszTekst = Wiersz("Tekst", _tbTekst); ezpPanel.Children.Add(_wierszTekst);

        var wyslij = _btnWyslij = new Button { Content = "Wyślij  (Ctrl+Enter)", Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 8, 0, 0),
            Background = Belka, Foreground = Brushes.Black, FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0) };
        wyslij.Click += (_, _) => Wyslij();
        ezpPanel.Children.Add(wyslij);
        _wynik.TextWrapping = TextWrapping.Wrap; _wynik.Margin = new Thickness(0, 6, 0, 0); _wynik.Foreground = Szary;
        ezpPanel.Children.Add(_wynik);
        _panelEzp = Panel("EZP – opcje tekstowe", new ScrollViewer { Content = ezpPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Grid.SetColumn(_panelEzp, 2); srodek.Children.Add(_panelEzp);

        // ---- plan ruchu
        var planPasek = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
        planPasek.Children.Add(new TextBlock { Text = "Plan ruchu", Foreground = Brushes.Black, Background = Belka, Padding = new Thickness(14, 3, 14, 3), FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 12, 0) });
        planPasek.Children.Add(Etykieta("Serwer"));
        _cbSerwer.IsEditable = true; _cbSerwer.Width = 110; _cbSerwer.Text = _ust.Serwer ?? "";
        _cbSerwer.ToolTip = "Kod serwera SimRail (np. pl1) – rozkład z publicznego API SimRail";
        planPasek.Children.Add(_cbSerwer);
        planPasek.Children.Add(Etykieta("Posterunek w rozkładzie"));
        StylPola(_tbStacja); _tbStacja.Width = 160; _tbStacja.Text = demo ? "" : _ust.Stacja ?? "";
        _tbStacja.ToolTip = "Puste – nazwa stacji z gry. Wpisz, gdy nazwa w rozkładzie jest inna.";
        _tbStacja.TextChanged += (_, _) => { if (!_demo) _ust.Stacja = _tbStacja.Text; ZbudujPlan(); };
        planPasek.Children.Add(_tbStacja);
        var odswiez = new Button { Content = "Pobierz rozkład", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 8, 0) };
        odswiez.Click += async (_, _) => await PobierzRozklad();
        planPasek.Children.Add(odswiez);
        _planStatus.Foreground = Szary; _planStatus.VerticalAlignment = VerticalAlignment.Center;
        planPasek.Children.Add(_planStatus);

        ZbudujTabele();
        var dol = new DockPanel { Margin = new Thickness(8, 0, 8, 8) };
        DockPanel.SetDock(planPasek, Dock.Top); dol.Children.Add(planPasek); dol.Children.Add(_dgPlan);
        Grid.SetRow(dol, 2); glowna.Children.Add(dol);

        // ---- zdarzenia
        foreach (var k in Teksty.Klasy) _rodzaje.Add(new Rodzaj { Klasa = k, Arg = Teksty.DomyslneArg(k) });
        _lbRodzaje.SelectedIndex = 0;
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { Wyslij(); e.Handled = true; } };
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        t.Tick += (_, _) => Takt();
        t.Start();
        Closed += (_, _) => { if (!_demo) { _ust.Serwer = _cbSerwer.Text; _ust.Zapisz(); } _most?.Dispose(); };

        if (demo) Demo.Wypelnij(this);
        else
        {
            _most = new MostKlient();
            _most.Pakiet += p => Dispatcher.BeginInvoke(() => Przyjmij(p));
            Loaded += async (_, _) => await WczytajSerwery();
        }
        Takt();
        WybranoSasiada();
    }

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
        _posterunek = MostKlient.S(p, "posterunek") ?? "";
        var st = MostKlient.S(p, "stacja") ?? "";
        bool zmianaStacji = st != _stacjaGry; _stacjaGry = st;
        _czasGry = MostKlient.S(p, "czasGry") ?? "";
        if (p.TryGetProperty("sasiedzi", out var ss))
            foreach (var s in ss.EnumerateArray())
            {
                var n = MostKlient.S(s, "n"); if (string.IsNullOrEmpty(n)) continue;
                var gracz = s.TryGetProperty("gracz", out var g) && g.GetBoolean();
                var juz = _sasiedzi.FirstOrDefault(x => x.Nazwa == n);
                if (juz == null) _sasiedzi.Add(new Sasiad { Nazwa = n, Gracz = gracz }); else juz.Gracz = gracz;
            }
        if (p.TryGetProperty("szablony", out var sz) && sz.GetArrayLength() > 0)
        {
            var wybrany = (_lbRodzaje.SelectedItem as Rodzaj)?.Klasa;
            var nowe = sz.EnumerateArray().Select(x => new Rodzaj
            {
                Klasa = MostKlient.S(x, "klasa"), Szablon = MostKlient.S(x, "tekst"), ZGry = true,
                Arg = x.TryGetProperty("arg", out var a) ? a.EnumerateArray().Select(v => v.GetString()).ToArray() : Array.Empty<string>(),
            }).ToList();
            if (!_rodzaje.Select(r => r.Klasa + r.Szablon + string.Join(",", r.Arg)).SequenceEqual(nowe.Select(r => r.Klasa + r.Szablon + string.Join(",", r.Arg))))
            {
                _rodzaje.Clear(); foreach (var r in nowe) _rodzaje.Add(r);
                _lbRodzaje.SelectedItem = _rodzaje.FirstOrDefault(r => r.Klasa == wybrany) ?? _rodzaje.FirstOrDefault();
            }
        }
        _wStacji.Clear();
        if (p.TryGetProperty("pociagi", out var pp))
            foreach (var x in pp.EnumerateArray())
            {
                var nr = MostKlient.S(x, "nr"); if (string.IsNullOrEmpty(nr)) continue;
                _wStacji[nr] = x.TryGetProperty("gdzie", out var gd) ? gd.EnumerateArray().Select(v => v.GetString()).ToList() : new();
            }
        foreach (var r in _plan) r.WStacji = _wStacji.ContainsKey(r.Pociag);
        if (zmianaStacji) ZbudujPlan();
        OdswiezPodpowiedzi();
    }

    void Czat(JsonElement p)
    {
        var wych = MostKlient.S(p, "kier") == "wych";
        var od = MostKlient.S(p, "od") ?? ""; var doK = MostKlient.S(p, "do") ?? "";
        var klasa = MostKlient.S(p, "klasa") ?? ""; var tekst = MostKlient.S(p, "tekst") ?? ""; var pociag = MostKlient.S(p, "pociag") ?? "";
        var czas = Godzina(MostKlient.S(p, "czasGry")) ?? Godzina(MostKlient.S(p, "czasPc")) ?? DateTime.Now.ToString("HH:mm");

        if (wych)
        {
            // własny komunikat z programu wraca z gry – uzupełnij wpis zamiast dublować
            var moj = _wpisy.LastOrDefault(w => w.Wychodzacy && w.Id > 0 && w.Do == doK && w.Klasa == klasa && (DateTime.Now - w.Utworzony).TotalSeconds < 20 && w.Status != "w czacie gry");
            if (moj != null) { moj.Tekst = tekst; moj.Status = "w czacie gry"; return; }
        }
        var sasiad = wych ? doK : od;
        var s = _sasiedzi.FirstOrDefault(x => x.Nazwa == sasiad);
        if (s == null && sasiad.Length > 0) _sasiedzi.Add(s = new Sasiad { Nazwa = sasiad, Gracz = !(p.TryGetProperty("ai", out var ai0) && ai0.GetBoolean()) });
        _wpisy.Add(new Wpis
        {
            Czas = czas, Wychodzacy = wych, Od = od, Do = doK, Ai = p.TryGetProperty("ai", out var ai) && ai.GetBoolean(),
            Klasa = klasa, Tekst = tekst, Pociag = pociag, Status = wych ? "z okna gry" : "",
            Tor = p.TryGetProperty("tor", out var tz) && tz.TryGetInt32(out var tzz) ? tzz : null,
        });
        if (s != null)
        {
            if (wych) s.Oczekujace++;
            else
            {
                s.Oczekujace = 0;
                if (_lbSasiedzi.SelectedItem != s || !IsActive) s.Nieprzeczytane++;
            }
        }
        if (!wych && klasa == "TrainPermissionMessage" && pociag.Length > 0)
        {
            _zgody[pociag] = $"{sasiad} {czas}";
            foreach (var r in _plan.Where(r => r.Pociag == pociag)) r.Zgoda = _zgody[pociag];
        }
        PokazOdpowiedzi();
        Przewin();
        Takt();
    }

    void Odpowiedz(JsonElement p)
    {
        long id = p.TryGetProperty("id", out var i) && i.TryGetInt64(out var ii) ? ii : 0;
        var ok = p.TryGetProperty("ok", out var o) && o.GetBoolean();
        var msg = MostKlient.S(p, "msg") ?? "";
        if (_wyslane.TryGetValue(id, out var w) && w.Status != "w czacie gry") w.Status = ok ? "przekazano do gry" : "ODRZUCONO: " + msg;
        _wynik.Text = ok ? "Gra przyjęła: " + msg : "Gra odrzuciła: " + msg;
        _wynik.Foreground = ok ? Zielony : Czerwony;
    }

    // ------------------------------------------------------------------ EZP

    void WybranoSasiada()
    {
        var s = _lbSasiedzi.SelectedItem as Sasiad;
        if (s != null) s.Nieprzeczytane = 0;
        _rozmowaTytul.Text = s == null ? "Widok zbiorczy – wszystkie posterunki" : $"Rozmowa z: {s.Nazwa} ({s.Obsada})";
        _widokWpisow.Refresh();
        Przewin();
        PokazOdpowiedzi();
        OdswiezPodpowiedzi();
        Takt();
    }

    void WybranoRodzaj()
    {
        var r = _lbRodzaje.SelectedItem as Rodzaj;
        var arg = r?.Arg ?? Array.Empty<string>();
        bool custom = r?.Klasa == "CustomTextMessage";
        Pokaz(_wierszPociag, arg.Contains("Train"));
        Pokaz(_wierszTor, arg.Contains("TrackNumber") || ZTorem(r?.Klasa));
        TorZNumeru();
        Pokaz(_wierszCzas, arg.Contains("Time"));
        Pokaz(_wierszPowod, arg.Contains("DenialReason"));
        Pokaz(_wierszTekst, custom);
        if (arg.Contains("Time") && string.IsNullOrWhiteSpace(_tbCzas.Text)) _tbCzas.Text = Teraz().ToString("HH:mm");
        _wzor.Text = r == null ? "" : !string.IsNullOrEmpty(r.Szablon) ? $"Wzór gry: {r.Szablon}" : r.ZGry ? "" : "Bez połączenia z grą – pola wg szablonów odczytanych wcześniej z gry.";
    }

    static void Pokaz(FrameworkElement w, bool tak)
    {
        w.Visibility = tak ? Visibility.Visible : Visibility.Collapsed;
        if (w.Tag is FrameworkElement[] dodatkowe) foreach (var d in dodatkowe) d.Visibility = w.Visibility;
    }

    // ------------------------------------------------------------------ widok zbiorczy, czyszczenie

    /// <summary>Wybrany posterunek, a w widoku zbiorczym – posterunek komunikatu, na który odpowiadasz.</summary>
    Sasiad Odbiorca() => _lbSasiedzi.SelectedItem as Sasiad
        ?? (_pytanie == null ? null : _sasiedzi.FirstOrDefault(x => x.Nazwa == _pytanie.Od) ?? new Sasiad { Nazwa = _pytanie.Od, Gracz = !_pytanie.Ai });

    void UstawOdbiorce()
    {
        var o = Odbiorca();
        _ezpOdbiorca.Text = o == null ? "Wybierz posterunek po lewej albo kliknij komunikat w rozmowie"
            : _lbSasiedzi.SelectedItem == null ? $"Do: {o.Nazwa}  (widok zbiorczy – odpowiedź na komunikat)" : $"Do: {o.Nazwa}";
    }

    internal void PokazZbiorczy() => WidokZbiorczy();

    void WidokZbiorczy()
    {
        _lbWpisy.SelectedItem = null;
        if (_lbSasiedzi.SelectedItem == null) WybranoSasiada(); else _lbSasiedzi.SelectedItem = null;
    }

    void WyczyscCzat()
    {
        _wpisy.Clear(); _wyslane.Clear(); _pytanie = null;
        foreach (var s in _sasiedzi) { s.Nieprzeczytane = 0; s.Oczekujace = 0; }
        PokazOdpowiedzi(); Takt();
        _wynik.Text = "Rozmowa wyczyszczona (czat w grze zostaje)."; _wynik.Foreground = Szary;
    }

    void WyczyscPosterunki()
    {
        _lbSasiedzi.SelectedItem = null;
        _sasiedzi.Clear();
        WybranoSasiada();
        _wynik.Text = "Lista posterunków wyczyszczona – sąsiedzi obecnego posterunku wrócą z gry po kilku sekundach."; _wynik.Foreground = Szary;
    }

    static Button Przycisk(string tekst, Action akcja, string podpowiedz)
    {
        var b = new Button { Content = tekst, Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 0, 0, 4), Background = TloPola,
            Foreground = Tekst, BorderBrush = Ramka, HorizontalContentAlignment = HorizontalAlignment.Left, ToolTip = podpowiedz };
        b.Click += (_, _) => akcja();
        return b;
    }

    // ------------------------------------------------------------------ podpowiedź odpowiedzi na pytanie posterunku

    /// <summary>
    /// Ostatni komunikat od wybranego posterunku (albo kliknięty w rozmowie) i pasujące odpowiedzi – klik wypełnia EZP
    /// (rodzaj, pociąg, tor, powód), wysyła dopiero „Wyślij”.
    /// </summary>
    void PokazOdpowiedzi()
    {
        var s = _lbSasiedzi.SelectedItem as Sasiad;
        // widok zbiorczy: ostatni komunikat od dowolnego posterunku (albo kliknięty)
        var w = _lbWpisy.SelectedItem is Wpis kl && !kl.Wychodzacy ? kl
              : s == null ? _wpisy.LastOrDefault() : _wpisy.LastOrDefault(x => x.Sasiad == s.Nazwa);
        if (w == null || w.Wychodzacy || (s != null && w.Od != s.Nazwa)) { _panelOdpowiedzi.Visibility = Visibility.Collapsed; _pytanie = null; UstawOdbiorce(); return; }
        _pytanie = w;
        UstawOdbiorce();
        _odpPrzyciski.Children.Clear();
        var nr = w.Pociag ?? "";
        void B(string etykieta, string klasa, string powod = null, bool zTorem = true)
        {
            var b = new Button { Content = etykieta, Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(6, 2, 6, 2),
                Background = TloPola, Foreground = Tekst, BorderBrush = Ramka };
            b.Click += (_, _) => Przygotuj(klasa, nr, zTorem ? w.Tor : null, powod);
            _odpPrzyciski.Children.Add(b);
        }
        switch (w.Klasa)
        {
            case "TrainRequestMessage":
                B($"Dla poc. {nr} droga wolna", "TrainPermissionMessage");
                B("Nie można przyjąć – tor zajęty", "TrainDenialMessage", "TrackOccupied");
                B("– brak wolnych torów", "TrainDenialMessage", "NoTracksAvailable");
                B("– mam pozwolenie w drugą stronę", "TrainDenialMessage", "IHavePermission");
                B("– muszę zapytać dalej", "TrainDenialMessage", "IHaveToAskFurther");
                B("– inny powód…", "TrainDenialMessage", "");
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
                B("Pociąg … odjechał (wybierz z podpowiedzi)", "TrainDepartedMessage", zTorem: false);
                break;
            case "TrainDenialMessage":
                B($"Zapytaj ponownie o poc. {nr}", "TrainRequestMessage");
                break;
            case "LockLineblockMessage":
                B("Odpowiedz tekstem", "CustomTextMessage", zTorem: false);
                break;
            default:
                _panelOdpowiedzi.Visibility = Visibility.Collapsed; _pytanie = null; return;
        }
        _odpTytul.Text = $"{w.Czas} {w.Od}: {w.Tekst}";
        _panelOdpowiedzi.Visibility = Visibility.Visible;
    }

    void Przygotuj(string klasa, string pociag, int? tor, string powod)
    {
        _lbRodzaje.SelectedItem = _rodzaje.FirstOrDefault(r => r.Klasa == klasa) ?? _lbRodzaje.SelectedItem;
        _cbPociag.Text = pociag ?? "";
        _tbTor.Text = tor?.ToString() ?? "";
        if (tor == null) TorZNumeru();
        _tbCzas.Text = Teraz().ToString("HH:mm");
        if (powod != null)
        {
            var i = Array.FindIndex(Teksty.Powody, x => x.Kod == powod);
            if (i >= 0) _cbPowod.SelectedIndex = i;
        }
        _wynik.Text = powod == "" ? "Wybierz powód i kliknij Wyślij." : string.IsNullOrEmpty(pociag) && klasa != "CustomTextMessage" ? "Wybierz pociąg z podpowiedzi i kliknij Wyślij." : "Sprawdź i kliknij Wyślij (Ctrl+Enter).";
        _wynik.Foreground = Cyjan;
        (powod == "" ? (Control)_cbPowod : string.IsNullOrEmpty(pociag) ? _cbPociag : klasa == "CustomTextMessage" ? _tbTekst : _btnWyslij).Focus();
    }

    /// <summary>Zapytanie o drogę wolną, danie drogi wolnej, danie odjazdu – pole toru zawsze widoczne (niewymagane).</summary>
    static bool ZTorem(string klasa) => klasa is "TrainRequestMessage" or "TrainPermissionMessage" or "TrainDepartedMessage";

    void UzyjPodpowiedzi(Podpowiedz p)
    {
        if (p.Tor.HasValue && _wierszTor.Visibility == Visibility.Visible) _tbTor.Text = p.Tor.Value.ToString();
    }

    /// <summary>Wpisany numer pociągu znany z podpowiedzi / planu ruchu, a tor pusty – wpisz tor z rozkładu.</summary>
    void TorZNumeru()
    {
        if (_wierszTor.Visibility != Visibility.Visible || !string.IsNullOrWhiteSpace(_tbTor.Text)) return;
        var nr = _cbPociag.Text?.Trim();
        if (string.IsNullOrEmpty(nr)) return;
        var tor = _podpowiedzi.FirstOrDefault(x => x.Nr == nr && x.Tor.HasValue)?.Tor ?? Tor(_plan.FirstOrDefault(x => x.Pociag == nr)?.Tor);
        if (tor.HasValue) _tbTor.Text = tor.Value.ToString();
    }

    void Wyslij()
    {
        var s = Odbiorca();
        var r = _lbRodzaje.SelectedItem as Rodzaj;
        if (s == null) { Blad("Wybierz posterunek po lewej albo kliknij komunikat posterunku w widoku zbiorczym."); return; }
        if (r == null) { Blad("Wybierz rodzaj komunikatu."); return; }
        string pociag = _wierszPociag.Visibility == Visibility.Visible ? _cbPociag.Text?.Trim() : null;
        if (_wierszPociag.Visibility == Visibility.Visible && string.IsNullOrEmpty(pociag)) { Blad("Podaj numer pociągu."); return; }
        int? tor = null;
        if (_wierszTor.Visibility == Visibility.Visible && !string.IsNullOrWhiteSpace(_tbTor.Text))
        {
            if (!int.TryParse(_tbTor.Text.Trim(), out var t)) { Blad("Tor – podaj liczbę."); return; }
            tor = t;
        }
        string czas = _wierszCzas.Visibility == Visibility.Visible ? _tbCzas.Text?.Trim() : null;
        if (!string.IsNullOrEmpty(czas) && !TimeSpan.TryParse(czas, out _)) { Blad("Godzina w postaci GG:MM."); return; }
        string powod = _wierszPowod.Visibility == Visibility.Visible ? (((string Kod, string Opis))_cbPowod.SelectedItem).Kod : null;
        string tekst = r.Klasa == "CustomTextMessage" ? _tbTekst.Text : null;
        if (r.Klasa == "CustomTextMessage" && string.IsNullOrWhiteSpace(tekst)) { Blad("Wpisz tekst."); return; }

        var opis = Opis(r, pociag, tor, czas, powod, tekst);
        long id = _demo ? -1 : _most.WyslijKomunikat(s.Nazwa, r.Klasa, pociag, tor, czas, powod, tekst);
        var w = new Wpis
        {
            Czas = Teraz().ToString("HH:mm"), Wychodzacy = true, Od = _posterunek, Do = s.Nazwa, Klasa = r.Klasa, Pociag = pociag,
            Tekst = opis, Status = _demo ? "tryb pokazowy – nie wysłano" : _most.Polaczony ? "wysyłanie…" : "brak połączenia z modem – nie dotarło do gry", Id = id,
        };
        _wyslane[id] = w;
        _wpisy.Add(w);
        s.Oczekujace++;
        _wynik.Text = ""; _tbTekst.Text = "";
        Przewin(); Takt();
    }

    static string Opis(Rodzaj r, string pociag, int? tor, string czas, string powod, string tekst)
    {
        if (r.Klasa == "CustomTextMessage") return tekst;
        var cz = new List<string> { r.Etykieta };
        if (!string.IsNullOrEmpty(pociag)) cz.Add($"poc. {pociag}");
        if (tor.HasValue) cz.Add($"tor {tor}");
        if (!string.IsNullOrEmpty(czas)) cz.Add($"godz. {czas}");
        if (!string.IsNullOrEmpty(powod)) cz.Add(Teksty.Powody.FirstOrDefault(x => x.Kod == powod).Opis);
        return string.Join(", ", cz);
    }

    void Blad(string t) { _wynik.Text = t; _wynik.Foreground = Czerwony; }

    // ------------------------------------------------------------------ podpowiedzi numeru pociągu

    void OdswiezPodpowiedzi()
    {
        var wpisano = _cbPociag.Text?.Trim() ?? "";
        if (_cbPociag.SelectedItem is Podpowiedz wyb && wyb.Nr == wpisano) wpisano = "";   // wybrana z listy – nie zawężaj
        var sasiad = (_lbSasiedzi.SelectedItem as Sasiad)?.Nazwa;
        var teraz = Teraz();
        var lista = new List<Podpowiedz>();
        foreach (var kv in _wStacji)
        {
            var w = _plan.FirstOrDefault(r => r.Pociag == kv.Key);
            lista.Add(new Podpowiedz { Nr = kv.Key, Waga = 0, Tor = Tor(w?.Tor), Opis = $"w stacji: {string.Join(", ", kv.Value.Take(3))}{(w != null ? $"  {w.Skad} → {w.Dokad}" : "")}" });
        }
        foreach (var r in _plan)
        {
            var min = (r.Czas - teraz).TotalMinutes;
            if (min < -30 || min > 90) continue;
            bool kierunek = sasiad != null && (RozkladSimRail.Pasuje(r.Skad, sasiad) || RozkladSimRail.Pasuje(r.Dokad, sasiad));
            lista.Add(new Podpowiedz
            {
                Nr = r.Pociag, Tor = Tor(r.Tor), Czas = r.Czas, Waga = (kierunek ? 1000 : 5000) + (int)Math.Abs(min),
                Opis = $"{r.Czas:HH:mm} {r.PO} tor {(string.IsNullOrEmpty(r.Tor) ? "–" : r.Tor)}  {r.Skad} → {r.Dokad}  {r.Kategoria}",
            });
        }
        var wynik = lista.Where(p => wpisano.Length == 0 || p.Nr.StartsWith(wpisano, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Waga).GroupBy(p => p.Nr).Select(g => g.First()).Take(40).ToList();
        if (wynik.Select(p => p.Nr + p.Opis).SequenceEqual(_podpowiedzi.Select(p => p.Nr + p.Opis))) return;
        var tekst = _cbPociag.Text;
        _podpowiedzi.Clear();
        foreach (var p in wynik) _podpowiedzi.Add(p);
        if (_cbPociag.Text != tekst) _cbPociag.Text = tekst;
    }

    static int? Tor(string t) => int.TryParse(t, out var v) ? v : null;

    DateTime Teraz() => DateTime.TryParse(_czasGry, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : DateTime.Now;

    static string Godzina(string s) => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.ToString("HH:mm") : null;

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
            _rozkladPobrany = DateTime.Now; _ust.Serwer = serwer; _ust.Zapisz();
            ZbudujPlan();
        }
        catch (Exception ex) { _planStatus.Text = "Rozkład niedostępny: " + ex.Message; }
        finally { _pobieranie = false; }
    }

    internal void UstawRozklad(List<RozkladSimRail.Pociag> r) { _rozklad = r; _rozkladPobrany = DateTime.Now; ZbudujPlan(); }

    string StacjaPlanu => !string.IsNullOrWhiteSpace(_tbStacja.Text) ? _tbStacja.Text.Trim() : !string.IsNullOrEmpty(_stacjaGry) ? _stacjaGry : _posterunek;

    void ZbudujPlan()
    {
        var stacja = StacjaPlanu;
        _plan.Clear();
        if (string.IsNullOrEmpty(stacja) || _rozklad.Count == 0)
        {
            _planStatus.Text = _rozklad.Count == 0 ? "brak rozkładu" : "nieznana stacja – wpisz nazwę posterunku z rozkładu";
            OdswiezPodpowiedzi(); return;
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
            wiersze.Add(new PozycjaPlanu
            {
                Pociag = t.Nr, Kategoria = string.IsNullOrEmpty(p0.Rodzaj) ? t.Kategoria : p0.Rodzaj, Czas = czas.Value,
                Przyjazd = i == 0 ? "" : p0.Przyjazd?.ToString("HH:mm") ?? "", Odjazd = k == t.Punkty.Count - 1 ? "" : p1.Odjazd?.ToString("HH:mm") ?? "",
                PO = postoj ? "p" : "o", Tor = p0.Tor ?? "", Peron = p0.Peron ?? "", Skad = skad, Dokad = dokad, Relacja = $"{t.Od} – {t.Do}",
                Lokomotywa = t.Lok, Dlugosc = t.Dlugosc, Brutto = t.Brutto,
                Zgoda = _zgody.GetValueOrDefault(t.Nr), WStacji = _wStacji.ContainsKey(t.Nr),
            });
        }
        foreach (var w in wiersze.OrderBy(x => x.Czas)) _plan.Add(w);
        _planStatus.Text = $"{stacja}: {_plan.Count} poc. (−1 h … +4 h), rozkład z {_rozkladPobrany:HH:mm}";
        OdswiezPodpowiedzi();
    }

    // ------------------------------------------------------------------ takt 1 s

    void Takt()
    {
        var t = Teraz();
        _zegar.Text = $"{t:HH:mm:ss}  {t:yyyy-MM-dd}";
        int o = _sasiedzi.Sum(s => s.Nieprzeczytane), w = _sasiedzi.Sum(s => s.Oczekujace);
        _ezpLicznik.Text = $"O: {o}   W: {w}";
        bool pol = _demo || (_most?.Polaczony ?? false);
        _status.Text = _demo ? "tryb pokazowy (dane przykładowe)"
            : pol ? $"gra: posterunek {(_posterunek.Length > 0 ? _posterunek : "–")}{(_stacjaGry.Length > 0 ? $" ({_stacjaGry})" : "")}"
            : "brak połączenia z grą (mod SimRailMost 0.10+, port 47131)";
        _status.Foreground = pol ? Brushes.DarkGreen : Brushes.DarkRed;
        _tytul.Text = _posterunek.Length > 0 ? $"Łączność – {_posterunek}" : "Łączność z posterunkami";
        if (!_demo && _rozkladPobrany != DateTime.MinValue && (DateTime.Now - _rozkladPobrany).TotalMinutes > 5) _ = PobierzRozklad();
    }

    void Przewin()
    {
        if (_lbWpisy.Items.Count > 0) _lbWpisy.ScrollIntoView(_lbWpisy.Items[^1]);
    }

    // ------------------------------------------------------------------ dostęp dla trybu pokazowego

    internal void UstawStanPokazowy(string posterunek, string stacja, string czasGry, IEnumerable<Sasiad> sasiedzi, IEnumerable<(string nr, string gdzie)> pociagi)
    {
        _posterunek = posterunek; _stacjaGry = stacja; _czasGry = czasGry;
        foreach (var s in sasiedzi) _sasiedzi.Add(s);
        foreach (var (nr, gdzie) in pociagi) _wStacji[nr] = new() { gdzie };
    }
    internal void DodajWpis(Wpis w) => _wpisy.Add(w);
    internal void Wybierz(int sasiad, string klasa, string pociag)
    {
        _lbSasiedzi.SelectedIndex = sasiad;
        _lbRodzaje.SelectedItem = _rodzaje.FirstOrDefault(r => r.Klasa == klasa);
        _cbPociag.Text = pociag;
        OdswiezPodpowiedzi();
    }

    // ------------------------------------------------------------------ wygląd

    static Border Panel(object tytul, UIElement tresc)
    {
        var dp = new DockPanel();
        var t = tytul as TextBlock ?? new TextBlock { Text = tytul.ToString() };
        t.Foreground = Cyjan; t.FontWeight = FontWeights.SemiBold; t.Margin = new Thickness(0, 0, 0, 6);
        DockPanel.SetDock(t, Dock.Top); dp.Children.Add(t); dp.Children.Add(tresc);
        return new Border { Background = TloPanelu, BorderBrush = Ramka, BorderThickness = new Thickness(1), Padding = new Thickness(8), Child = dp };
    }

    static FrameworkElement Wiersz(string etykieta, FrameworkElement pole)
    {
        var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.Children.Add(new TextBlock { Text = etykieta, Foreground = Szary, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(pole, 1); g.Children.Add(pole);
        return g;
    }

    static TextBlock Etykieta(string t) => new() { Text = t, Foreground = Szary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0) };

    static void StylPola(TextBox t)
    {
        t.Background = TloPola; t.Foreground = Tekst; t.BorderBrush = Ramka; t.CaretBrush = Tekst; t.Padding = new Thickness(3); t.FontFamily = Mono; t.FontSize = 14;
    }

    static void StylListy(ListBox l)
    {
        l.Background = TloPola; l.Foreground = Tekst; l.BorderBrush = Ramka;
        l.HorizontalContentAlignment = HorizontalAlignment.Stretch;
    }

    static DataTemplate SzablonSasiada() => Szablon(@"
<StackPanel Orientation='Vertical' Margin='2,3'>
  <DockPanel>
    <TextBlock DockPanel.Dock='Right' Text='{Binding Licznik}' Foreground='#FF8A3D' FontFamily='Consolas' FontWeight='Bold'/>
    <TextBlock Text='{Binding Nazwa}' FontWeight='SemiBold' FontSize='14'/>
  </DockPanel>
  <TextBlock Text='{Binding Obsada}' Foreground='#8A94A8' FontSize='11'/>
</StackPanel>");

    static DataTemplate SzablonWpisu() => Szablon(@"
<Border Margin='2,3' Padding='6,4' BorderThickness='3,0,0,0'>
  <Border.Style>
    <Style TargetType='Border'>
      <Setter Property='BorderBrush' Value='#4FD3E6'/>
      <Style.Triggers><DataTrigger Binding='{Binding Wychodzacy}' Value='True'><Setter Property='BorderBrush' Value='#FF8A3D'/></DataTrigger></Style.Triggers>
    </Style>
  </Border.Style>
  <StackPanel>
    <TextBlock Text='{Binding Naglowek}' FontFamily='Consolas' FontSize='12'>
      <TextBlock.Style>
        <Style TargetType='TextBlock'>
          <Setter Property='Foreground' Value='#4FD3E6'/>
          <Style.Triggers><DataTrigger Binding='{Binding Wychodzacy}' Value='True'><Setter Property='Foreground' Value='#FF8A3D'/></DataTrigger></Style.Triggers>
        </Style>
      </TextBlock.Style>
    </TextBlock>
    <TextBlock Text='{Binding Tekst}' TextWrapping='Wrap' FontSize='14' Foreground='#E8ECF4'/>
    <TextBlock Text='{Binding Status}' Foreground='#8A94A8' FontSize='11'/>
  </StackPanel>
</Border>");

    static DataTemplate SzablonPodpowiedzi() => Szablon(@"
<DockPanel>
  <TextBlock Text='{Binding Nr}' Width='70' FontFamily='Consolas' FontSize='14' Foreground='#FF8A3D' FontWeight='Bold'/>
  <TextBlock Text='{Binding Opis}' FontSize='11' Foreground='#B8C0D0' VerticalAlignment='Center' TextTrimming='CharacterEllipsis'/>
</DockPanel>");

    static DataTemplate Szablon(string xaml)
    {
        var pelny = "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>" + xaml + "</DataTemplate>";
        return (DataTemplate)System.Windows.Markup.XamlReader.Parse(pelny);
    }

    void ZbudujTabele()
    {
        _dgPlan.ItemsSource = _plan;
        _dgPlan.AutoGenerateColumns = false; _dgPlan.IsReadOnly = true; _dgPlan.HeadersVisibility = DataGridHeadersVisibility.Column;
        _dgPlan.Background = TloPola; _dgPlan.RowBackground = TloPola; _dgPlan.AlternatingRowBackground = Pedzel("#111A30");
        _dgPlan.Foreground = Zielony; _dgPlan.BorderBrush = Ramka; _dgPlan.GridLinesVisibility = DataGridGridLinesVisibility.All;
        _dgPlan.HorizontalGridLinesBrush = Ramka; _dgPlan.VerticalGridLinesBrush = Ramka; _dgPlan.FontFamily = Mono; _dgPlan.FontSize = 14;
        var naglowekStyl = new Style(typeof(DataGridColumnHeader));
        naglowekStyl.Setters.Add(new Setter(BackgroundProperty, Belka));
        naglowekStyl.Setters.Add(new Setter(ForegroundProperty, Brushes.White));
        naglowekStyl.Setters.Add(new Setter(FontWeightProperty, FontWeights.Bold));
        naglowekStyl.Setters.Add(new Setter(PaddingProperty, new Thickness(6, 2, 6, 2)));
        naglowekStyl.Setters.Add(new Setter(BorderBrushProperty, Ramka));
        naglowekStyl.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(0, 0, 1, 0)));
        _dgPlan.ColumnHeaderStyle = naglowekStyl;
        var wierszStyl = new Style(typeof(DataGridRow));
        var wStacji = new DataTrigger { Binding = new Binding(nameof(PozycjaPlanu.WStacji)), Value = true };
        wStacji.Setters.Add(new Setter(BackgroundProperty, Pedzel("#3A1E12")));
        wStacji.Setters.Add(new Setter(ForegroundProperty, Pomaranczowy));
        wierszStyl.Triggers.Add(wStacji);
        _dgPlan.RowStyle = wierszStyl;
        void K(string n, string p, double w = double.NaN) => _dgPlan.Columns.Add(new DataGridTextColumn { Header = n, Binding = new Binding(p), Width = double.IsNaN(w) ? DataGridLength.Auto : new DataGridLength(w) });
        K("Pociąg", nameof(PozycjaPlanu.Pociag), 80);
        K("p/o", nameof(PozycjaPlanu.PO), 40);
        K("Przyj.", nameof(PozycjaPlanu.Przyjazd), 62);
        K("Odj.", nameof(PozycjaPlanu.Odjazd), 62);
        K("Tor", nameof(PozycjaPlanu.Tor), 46);
        K("Peron", nameof(PozycjaPlanu.Peron), 56);
        K("Skąd", nameof(PozycjaPlanu.Skad), 150);
        K("Dokąd", nameof(PozycjaPlanu.Dokad), 150);
        K("Zgoda", nameof(PozycjaPlanu.Zgoda), 130);
        K("Rodz", nameof(PozycjaPlanu.Kategoria), 60);
        K("Relacja", nameof(PozycjaPlanu.Relacja), 230);
        K("Dług", nameof(PozycjaPlanu.Dlugosc), 56);
        K("Brutto", nameof(PozycjaPlanu.Brutto), 64);
        K("Lokomotywa", nameof(PozycjaPlanu.Lokomotywa), 110);
        _dgPlan.MouseDoubleClick += (_, _) =>
        {
            if (_dgPlan.SelectedItem is PozycjaPlanu r) { _cbPociag.Text = r.Pociag; if (Tor(r.Tor) is int t) _tbTor.Text = t.ToString(); }
        };
        _dgPlan.ToolTip = "Dwuklik – numer pociągu do komunikatu EZP. Pomarańczowe – pociąg w stacji (z gry).";
    }
}

/// <summary>Ustawienia w %LocalAppData%\SUK\Lacznosc\ustawienia.json.</summary>
public sealed class Ustawienia
{
    public string Serwer { get; set; }
    public string Stacja { get; set; }

    static string Plik => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SUK", "Lacznosc", "ustawienia.json");

    public static Ustawienia Wczytaj()
    {
        try { return JsonSerializer.Deserialize<Ustawienia>(File.ReadAllText(Plik)) ?? new(); } catch { return new(); }
    }

    public void Zapisz()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(Plik)); File.WriteAllText(Plik, JsonSerializer.Serialize(this)); } catch { }
    }
}

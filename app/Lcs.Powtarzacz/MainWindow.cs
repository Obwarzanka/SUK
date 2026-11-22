using System.Collections.ObjectModel;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Lcs.Powtarzacz;

/// <summary>
/// Okno powtarzacza: pola wybranych przejazdów (PrzejazdView), dziennik zdarzeń i usterek, kwitowanie awarii (alarm dźwiękowy do
/// skwitowania), wybór przejazdów. Stan z gry – <see cref="Odbiornik"/>; tryb --demo – dane przykładowe.
/// </summary>
public sealed class MainWindow : Window
{
    static readonly Brush TloOkna = Pedzel("#000000"), TloPanelu = Pedzel("#101418"), Tekst = Pedzel("#D0D0D0"), Szary = Pedzel("#8A94A8");
    static Brush Pedzel(string hex) { var b = (Brush)new BrushConverter().ConvertFromString(hex); b.Freeze(); return b; }
    static readonly FontFamily Mono = new("Consolas");

    readonly Odbiornik _odb;
    readonly bool _demo;
    readonly DateTime _start;
    readonly Ustawienia _ust = Ustawienia.Wczytaj();
    readonly WrapPanel _pola = new();
    readonly Dictionary<string, PrzejazdView> _widoki = new(StringComparer.OrdinalIgnoreCase);
    readonly ObservableCollection<string> _dziennik = new();
    readonly ListBox _lbDziennik = new();
    readonly TextBlock _status = new(), _alarm = new();
    readonly Border _panelWyboru = new();
    readonly StackPanel _listaWyboru = new();
    readonly TextBox _filtr = new();
    readonly Dictionary<string, (string Stan, bool Awaria, bool Ostrz)> _poprz = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _niekwitowane = new(StringComparer.OrdinalIgnoreCase);
    List<Przejazd> _wszystkie = new();
    DateTime _ostatniDzwiek = DateTime.MinValue;
    string _zaznaczony;
    readonly RaspPanel _rasp = new();
    readonly ScrollViewer _widokIe = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    Button _bWidok;
    DockPanel _dol;

    public MainWindow(bool demo, int port, double demoOdSekundy = 0, string widok = null, string ekran = null)
    {
        if (widok != null) _ust.Widok = widok;
        _demo = demo; _start = DateTime.Now.AddSeconds(-demoOdSekundy);
        if (!demo) _odb = new Odbiornik(port);
        Title = "Powtarzacz zdalnej kontroli przejazdów (UZK)";
        Width = 1120; Height = 760; Background = TloOkna; Foreground = Tekst; FontFamily = Mono;

        var gora = new DockPanel { Background = TloPanelu, LastChildFill = true };
        var bKwit = Przycisk("Kwituj awarię (F5)", (_, _) => Kwituj(null));
        var bWybor = Przycisk("Wybór przejazdów", (_, _) => { _panelWyboru.Visibility = _panelWyboru.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; OdswiezWybor(); });
        _bWidok = Przycisk("", (_, _) => { _ust.Widok = _ust.Widok == "rasp" ? "ie104" : "rasp"; UstawWidok(); if (!_demo) _ust.Zapisz(); });
        DockPanel.SetDock(bKwit, Dock.Right); DockPanel.SetDock(bWybor, Dock.Right); DockPanel.SetDock(_bWidok, Dock.Right);
        gora.Children.Add(bKwit); gora.Children.Add(bWybor); gora.Children.Add(_bWidok);
        var tytul = new StackPanel { Margin = new Thickness(10, 6, 10, 6) };
        tytul.Children.Add(new TextBlock { Text = "POWTARZACZ ZDALNEJ KONTROLI PRZEJAZDÓW", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Pedzel("#E9ECF2") });
        _status.FontSize = 11; _status.Foreground = Szary; tytul.Children.Add(_status);
        _alarm.FontSize = 13; _alarm.FontWeight = FontWeights.Bold; _alarm.Foreground = Pedzel("#FF3030"); tytul.Children.Add(_alarm);
        gora.Children.Add(tytul);

        _lbDziennik.ItemsSource = _dziennik; _lbDziennik.Height = 160; _lbDziennik.Background = TloPanelu; _lbDziennik.Foreground = Tekst;
        _lbDziennik.BorderThickness = new Thickness(0); _lbDziennik.FontSize = 11;
        var dol = _dol = new DockPanel { Background = TloPanelu };
        var nd = new TextBlock { Text = "Dziennik zdarzeń i usterek (Ie-20 § 21 ust. 8)", Margin = new Thickness(8, 4, 8, 2), Foreground = Szary, FontSize = 11 };
        DockPanel.SetDock(nd, Dock.Top); dol.Children.Add(nd); dol.Children.Add(_lbDziennik);

        _filtr.Margin = new Thickness(6); _filtr.Background = TloOkna; _filtr.Foreground = Tekst; _filtr.CaretBrush = Tekst;
        _filtr.ToolTip = "Filtr: linia lub km, np. „L1_1” albo „102”";
        _filtr.TextChanged += (_, _) => OdswiezWybor();
        var pw = new DockPanel { Width = 290 };
        var nw = new TextBlock { Text = "Przejazdy na powtarzaczu", Margin = new Thickness(6, 6, 6, 0), Foreground = Szary };
        DockPanel.SetDock(nw, Dock.Top); DockPanel.SetDock(_filtr, Dock.Top);
        pw.Children.Add(nw); pw.Children.Add(_filtr);
        pw.Children.Add(new ScrollViewer { Content = _listaWyboru, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        _panelWyboru.Child = pw; _panelWyboru.Background = TloPanelu; _panelWyboru.Visibility = Visibility.Collapsed;

        var root = new DockPanel { Background = TloOkna };
        DockPanel.SetDock(gora, Dock.Top); DockPanel.SetDock(dol, Dock.Bottom); DockPanel.SetDock(_panelWyboru, Dock.Right);
        root.Children.Add(gora); root.Children.Add(dol); root.Children.Add(_panelWyboru);
        _pola.Margin = new Thickness(6);
        _widokIe.Content = _pola;
        var srodek = new Grid();
        srodek.Children.Add(_widokIe); srodek.Children.Add(_rasp);
        root.Children.Add(srodek);
        Content = root;
        UstawWidok();
        if (ekran != null) Loaded += (_, _) => Dispatcher.BeginInvoke(() => _rasp.PokazEkran(ekran, _ust.Wybrane?.FirstOrDefault()), DispatcherPriority.ContextIdle);

        // F5 – kwitowanie (widok Ie-104); Ctrl+Enter – „Zaakceptowano !” jak w RASP-UZK (DTR RASP-4F tabela 7)
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.F5 || e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { Kwituj(null); e.Handled = true; }
        };
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        t.Tick += (_, _) => Odswiez();
        t.Start();
        Closed += (_, _) => { _odb?.Dispose(); if (!_demo) _ust.Zapisz(); };
        if (_odb?.Blad != null) Wpis("", _odb.Blad, true);
    }

    void UstawWidok()
    {
        bool rasp = _ust.Widok == "rasp";
        _rasp.Visibility = rasp ? Visibility.Visible : Visibility.Collapsed;
        _widokIe.Visibility = rasp ? Visibility.Collapsed : Visibility.Visible;
        _dol.Visibility = rasp ? Visibility.Collapsed : Visibility.Visible;   // RASP-UZK ma własne listy zdarzeń (ekran II i III)
        _bWidok.Content = rasp ? "Widok: RASP-UZK (Kombud)" : "Widok: Ie-104 § 13 / § 14";
        _bWidok.ToolTip = "Przełącz widok: pola wg Ie-104 albo ekrany RASP-UZK (Z.A. Kombud, DTR RASP-4F)";
    }

    Button Przycisk(string tekst, RoutedEventHandler h)
    {
        var b = new Button { Content = tekst, Margin = new Thickness(6, 10, 6, 10), Padding = new Thickness(10, 2, 10, 2), Background = Pedzel("#1E2630"), Foreground = Tekst, BorderBrush = Pedzel("#40505F") };
        b.Click += h; return b;
    }

    // ------------------------------------------------------------------ odświeżanie

    void Odswiez()
    {
        var teraz = DateTime.Now;
        _wszystkie = _demo ? Demo.Stan(teraz - _start) : _odb.Migawka();
        bool miga = teraz.Millisecond >= 500;
        if (!_demo && _ust.Wybrane == null) WyborDomyslny();
        var wybrane = _demo ? _wszystkie.Select(p => p.Nazwa).ToList() : _ust.Wybrane ?? new List<string>();
        var poNazwie = _wszystkie.ToDictionary(p => p.Nazwa, StringComparer.OrdinalIgnoreCase);

        // pola – w kolejności linia, km
        var kolej = wybrane.Select(n => poNazwie.GetValueOrDefault(n) ?? new Przejazd { Nazwa = n }).OrderBy(p => int.TryParse(p.Linia, out var l) ? l : 9999).ThenBy(p => p.Km).ToList();
        if (_pola.Children.Count != kolej.Count || kolej.Where((p, i) => !ReferenceEquals((_pola.Children[i] as PrzejazdView)?.Tag, p.Nazwa)).Any())
        {
            _pola.Children.Clear();
            foreach (var p in kolej)
            {
                if (!_widoki.TryGetValue(p.Nazwa, out var v))
                {
                    v = new PrzejazdView { Tag = p.Nazwa, Cursor = Cursors.Hand };
                    var nazwa = p.Nazwa;
                    v.MouseLeftButtonUp += (_, _) => { _zaznaczony = nazwa; Kwituj(nazwa); };
                    _widoki[p.Nazwa] = v;
                }
                _pola.Children.Add(v);
            }
        }
        foreach (var p in kolej)
        {
            var v = _widoki[p.Nazwa];
            bool brak = p.Odebrano == DateTime.MinValue || (teraz - p.Odebrano).TotalSeconds > 5;
            Zdarzenia(p, brak);
            v.P = p; v.Miga = miga; v.BrakDanych = brak; v.AlarmNiekwitowany = _niekwitowane.Contains(p.Nazwa); v.Wybrany = _zaznaczony == p.Nazwa;
            v.ToolTip = $"{p.Nazwa} – {p.Opis}{(p.NaPulpicie ? ", na pulpicie gry" : "")}. LPM – kwitowanie awarii tego przejazdu";
            v.InvalidateVisual();
        }

        _rasp.Odswiez(kolej, p => p.Odebrano == DateTime.MinValue || (teraz - p.Odebrano).TotalSeconds > 5, miga, _niekwitowane);

        // alarm dźwiękowy do skwitowania
        if (_niekwitowane.Count > 0)
        {
            _alarm.Text = $"AWARIA SSP – do skwitowania: {string.Join(", ", _niekwitowane.Select(n => poNazwie.GetValueOrDefault(n)?.KmTekst ?? n))}";
            _alarm.Visibility = miga ? Visibility.Visible : Visibility.Hidden;
            if ((teraz - _ostatniDzwiek).TotalSeconds >= 3) { _ostatniDzwiek = teraz; SystemSounds.Hand.Play(); }
        }
        else _alarm.Text = "";

        _status.Text = _demo ? "DANE PRZYKŁADOWE (--demo) – bez gry"
            : _odb.Blad ?? (_odb.Ostatni == DateTime.MinValue ? $"czekam na stan z gry (mod SimRailMost ≥ 0.12, UDP 127.0.0.1:{_odb.Port})…"
            : $"gra: {(_odb.Posterunek.Length > 0 ? _odb.Posterunek : "?")}  ·  ostatni stan {(teraz - _odb.Ostatni).TotalSeconds:0} s temu  ·  przejazdów w grze: {_wszystkie.Count}, na powtarzaczu: {kolej.Count}");
        if (_panelWyboru.Visibility == Visibility.Visible && _listaWyboru.Children.Count == 0) OdswiezWybor();
    }

    void Zdarzenia(Przejazd p, bool brak)
    {
        var stan = brak ? null : p.Stan;
        bool aw = !brak && p.Awaria, os = !brak && p.Ostrzeganie;
        if (_poprz.TryGetValue(p.Nazwa, out var b))
        {
            if (aw && !b.Awaria) { _niekwitowane.Add(p.Nazwa); Wpis(p.KmTekst, "AWARIA SSP (brak gotowości)", true); }
            else if (!aw && b.Awaria) Wpis(p.KmTekst, "ustąpienie awarii SSP", true);
            if (stan != b.Stan && !aw) Wpis(p.KmTekst, stan == null ? "brak danych z gry" : Przejazd.Slownie(stan) + (stan == "zamkniety" && p.Potwierdzony ? " (utwierdzony)" : ""), false);
            if (os != b.Ostrz && !brak) Wpis(p.KmTekst, os ? "sygnalizacja ostrzegawcza załączona" : "sygnalizacja ostrzegawcza wyłączona", false);
        }
        else if (aw) { _niekwitowane.Add(p.Nazwa); Wpis(p.KmTekst, "AWARIA SSP (brak gotowości)", true); }
        _poprz[p.Nazwa] = (stan, aw, os);
    }

    void Kwituj(string nazwa)
    {
        var lista = nazwa == null ? _niekwitowane.ToList() : _niekwitowane.Where(n => string.Equals(n, nazwa, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var n in lista)
        {
            _niekwitowane.Remove(n);
            var km = _wszystkie.FirstOrDefault(p => string.Equals(p.Nazwa, n, StringComparison.OrdinalIgnoreCase))?.KmTekst ?? n;
            Wpis(km, "awaria skwitowana przez dyżurnego", true);
            _rasp.Komunikat(km, "Zaakceptowano !");
        }
    }

    void Wpis(string km, string tekst, bool usterka)
    {
        var w = $"{DateTime.Now:HH:mm:ss}  {(km.Length > 0 ? $"km {km,-8}" : "")}  {(usterka ? "!! " : "")}{tekst}";
        _dziennik.Add(w);
        while (_dziennik.Count > 500) _dziennik.RemoveAt(0);
        _lbDziennik.ScrollIntoView(w);
        if (_demo) return;
        try
        {
            var kat = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SUK", "Powtarzacz");
            Directory.CreateDirectory(kat);
            File.AppendAllText(Path.Combine(kat, $"dziennik_{DateTime.Now:yyyyMMdd}.log"), $"{DateTime.Now:yyyy-MM-dd} {w}{Environment.NewLine}");
        }
        catch { }
    }

    // ------------------------------------------------------------------ wybór przejazdów

    /// <summary>
    /// Wybór domyślny (pierwsze uruchomienie): przejazdy pulpitu gry bez poleceń (UZK) i przejazdy SSP (kat. B / C) do 15 km od
    /// przejazdów pulpitu – np. LK1 Koluszki → Rokiciny. Czeka, aż mod poda przejazdy pulpitu z położeniem.
    /// </summary>
    void WyborDomyslny()
    {
        var pul = _wszystkie.Where(p => p.NaPulpicie && !double.IsNaN(p.X)).ToList();
        if (pul.Count == 0) return;
        var w = _wszystkie.Where(p => p.NaPulpicie && p.Uzk
                                   || p.Ssp && !double.IsNaN(p.X) && pul.Any(q => Math.Sqrt(Math.Pow(p.X - q.X, 2) + Math.Pow(p.Z - q.Z, 2)) <= 15000))
                          .Select(p => p.Nazwa).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        _ust.Wybrane = w; _ust.Zapisz();
        Wpis("", $"wybór domyślny: {w.Count} przejazdów (UZK z pulpitu gry i SSP do 15 km) – zmiana: „Wybór przejazdów”", false);
    }

    void OdswiezWybor()
    {
        _listaWyboru.Children.Clear();
        var f = _filtr.Text.Trim();
        var wyb = new HashSet<string>(_ust.Wybrane ?? new(), StringComparer.OrdinalIgnoreCase);
        foreach (var p in _wszystkie.Where(p => f.Length == 0 || p.Nazwa.Contains(f, StringComparison.OrdinalIgnoreCase) || p.KmTekst.Contains(f.Replace('.', ',')))
                                    .OrderByDescending(p => wyb.Contains(p.Nazwa)).ThenBy(p => int.TryParse(p.Linia, out var l) ? l : 9999).ThenBy(p => p.Km).Take(300))
        {
            var cb = new CheckBox
            {
                Content = $"{p.Opis}{(p.NaPulpicie ? (p.Uzk ? "  [pulpit, UZK]" : "  [pulpit]") : "")}{(p.Ssp ? "  SSP" : "")}",
                IsChecked = wyb.Contains(p.Nazwa), Foreground = Tekst, Margin = new Thickness(6, 1, 6, 1), ToolTip = p.Nazwa,
            };
            var nazwa = p.Nazwa;
            cb.Checked += (_, _) => { (_ust.Wybrane ??= new()).Add(nazwa); _ust.Zapisz(); };
            cb.Unchecked += (_, _) => { _ust.Wybrane?.RemoveAll(x => string.Equals(x, nazwa, StringComparison.OrdinalIgnoreCase)); _ust.Zapisz(); };
            _listaWyboru.Children.Add(cb);
        }
        if (_listaWyboru.Children.Count == 0) _listaWyboru.Children.Add(new TextBlock { Text = "brak przejazdów (gra nie wysłała jeszcze stanu)", Foreground = Szary, Margin = new Thickness(6) });
    }
}

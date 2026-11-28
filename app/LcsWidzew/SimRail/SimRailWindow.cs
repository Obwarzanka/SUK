using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using LcsWidzew.Model;
using LcsWidzew.Net;
using LcsWidzew.UI;

namespace LcsWidzew.SimRail;

/// <summary>
/// Podgląd stanów z gry SimRail (etap 1 integracji): obiekty posterunku odebrane od moda „SimRailMost” po UDP,
/// zdarzenia zajęć / zwolnień z godziną gry i numerami pociągów oraz mapa nazw SimRail → odcinki SUK
/// (<c>&lt;układ&gt;.simrail.json</c>, wypełnia właściciel). Tylko odczyt – nic nie trafia do komputera zależności.
/// </summary>
public class SimRailWindow : DarkWindow
{
    public sealed class ObiektRow : INotifyPropertyChanged
    {
        string _stan = "", _pociagi = "", _przebieg = "", _blokada = "", _zwrotnica = "", _suk = "", _uwagi = "", _rodzaj = "", _posterunek = "";
        public string Obiekt { get; init; }
        /// <summary>Posterunek gry, do którego należy obiekt (mod ≥ 0.10.2).</summary>
        public string Posterunek { get => _posterunek; set => Ustaw(ref _posterunek, value, nameof(Posterunek)); }
        public string Rodzaj { get => _rodzaj; set => Ustaw(ref _rodzaj, value, nameof(Rodzaj)); }
        public string Stan { get => _stan; set => Ustaw(ref _stan, value, nameof(Stan)); }
        public string Pociagi { get => _pociagi; set => Ustaw(ref _pociagi, value, nameof(Pociagi)); }
        public string Przebieg { get => _przebieg; set => Ustaw(ref _przebieg, value, nameof(Przebieg)); }
        public string Blokada { get => _blokada; set => Ustaw(ref _blokada, value, nameof(Blokada)); }
        public string Zwrotnica { get => _zwrotnica; set => Ustaw(ref _zwrotnica, value, nameof(Zwrotnica)); }
        /// <summary>Odcinek / obiekt SUK z mapy (edytowalny).</summary>
        public string Suk { get => _suk; set => Ustaw(ref _suk, value ?? "", nameof(Suk)); }
        public string Uwagi { get => _uwagi; set => Ustaw(ref _uwagi, value, nameof(Uwagi)); }
        public bool Zajety;
        public event PropertyChangedEventHandler PropertyChanged;
        void Ustaw(ref string pole, string v, string nazwa)
        {
            if (pole == v) return;
            pole = v; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nazwa));
        }
    }

    public sealed class ZdarzenieRow
    {
        public string CzasGry { get; init; } public string Odebrano { get; init; } public string Obiekt { get; init; }
        public string Rodzaj { get; init; } public string Stan { get; init; } public string Pociagi { get; init; } public string Suk { get; init; }
    }

    readonly SimRailOdbiornik odb = SimRailOdbiornik.Wspolny;   // wspólny z pulpitem zdalnym
    readonly Station station;
    readonly HashSet<string> znaneSuk;
    readonly string plikMapy;
    SimRailMapa mapa;
    bool mapaZmieniona;

    readonly Dictionary<string, ObiektRow> wiersze = new(StringComparer.Ordinal);
    readonly ObservableCollection<ObiektRow> obiekty = new();
    readonly ObservableCollection<ZdarzenieRow> zdarzenia = new();
    readonly ICollectionView widok;
    readonly DataGrid gObiekty = Grid(), gZdarzenia = Grid();
    readonly TextBox port = Box(SimRailOdbiornik.DomyslnyPort.ToString()), filtr = Box();
    readonly TextBlock status = T("", 12), infoMapy = T("", 11, Brushes.Gray), posterunki = T("", 12, Brushes.White), ostrzezenie = T("", 12, Brushes.Orange);
    readonly CheckBox tylkoZajete = new() { Content = "tylko zajęte", Foreground = Brushes.LightGray, Margin = new Thickness(10, 6, 10, 0) };
    readonly CheckBox tylkoOdcinki = new() { Content = "tylko odcinki", Foreground = Brushes.LightGray, Margin = new Thickness(0, 6, 10, 0) };
    readonly CheckBox tylkoBezMapy = new() { Content = "tylko bez przypisania w SUK", Foreground = Brushes.LightGray, Margin = new Thickness(0, 6, 10, 0) };
    readonly Button btnStart;

    public SimRailWindow(Station station) : base("SimRail – podgląd stanów (etap 1)", 1150, 680)
    {
        this.station = station; ShowInTaskbar = true;
        znaneSuk = ZnaneObiektySuk(station);
        plikMapy = SimRailMapa.PlikDla(station?.SourcePath) ?? Path.Combine(AppContext.BaseDirectory, "simrail.json");
        WczytajMape();

        // --- tabela obiektów: wszystkie kolumny tylko do odczytu poza „Odcinek SUK”
        gObiekty.IsReadOnly = false; gObiekty.CanUserAddRows = false; gObiekty.CanUserDeleteRows = false;
        foreach (var (h, p, w) in new[] { ("Obiekt SimRail", "Obiekt", 170.0), ("Posterunek", "Posterunek", 110), ("Rodzaj", "Rodzaj", 95), ("Stan", "Stan", 140), ("Pociągi", "Pociagi", 90),
                                          ("Przebieg", "Przebieg", 170), ("Zablokowanie", "Blokada", 120), ("Zwrotnica", "Zwrotnica", 75) })
            KolumnaOdczyt(gObiekty, h, p, w);
        var edycja = new Style(typeof(TextBox));
        edycja.Setters.Add(new Setter(BackgroundProperty, Brushes.Black));
        edycja.Setters.Add(new Setter(ForegroundProperty, Brushes.White));
        edycja.Setters.Add(new Setter(TextBox.CaretBrushProperty, Brushes.White));
        gObiekty.Columns.Add(new DataGridTextColumn
        {
            Header = "Odcinek SUK (edytuj)", Width = new DataGridLength(150),
            Binding = new Binding(nameof(ObiektRow.Suk)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
            EditingElementStyle = edycja,
        });
        KolumnaOdczyt(gObiekty, "Uwagi", nameof(ObiektRow.Uwagi), double.NaN);
        gObiekty.CellEditEnding += (a, e) => Dispatcher.BeginInvoke(() => PoEdycji(e.Row.Item as ObiektRow), DispatcherPriority.Background);
        gObiekty.LoadingRow += (a, e) => KolorujWiersz(e.Row);
        widok = CollectionViewSource.GetDefaultView(obiekty);
        widok.Filter = Filtr;
        widok.SortDescriptions.Add(new SortDescription(nameof(ObiektRow.Obiekt), ListSortDirection.Ascending));
        gObiekty.ItemsSource = widok;

        foreach (var (h, p, w) in new[] { ("Czas gry", "CzasGry", 140.0), ("Odebrano", "Odebrano", 90), ("Obiekt SimRail", "Obiekt", 170),
                                          ("Rodzaj", "Rodzaj", 95), ("Stan", "Stan", 80), ("Pociągi", "Pociagi", 90), ("Odcinek SUK", "Suk", double.NaN) })
            Col(gZdarzenia, h, p, w);
        gZdarzenia.ItemsSource = zdarzenia;

        // --- nagłówek
        btnStart = Btn("Nasłuchuj", (a, b) => PrzelaczOdbior());
        port.Width = 70; port.Margin = new Thickness(4, 4, 8, 4);
        filtr.Width = 180; filtr.Margin = new Thickness(4, 4, 0, 4);
        filtr.TextChanged += (a, b) => widok.Refresh();
        foreach (var c in new[] { tylkoZajete, tylkoOdcinki, tylkoBezMapy }) { c.Checked += (a, b) => widok.Refresh(); c.Unchecked += (a, b) => widok.Refresh(); }

        var pasek1 = new WrapPanel();
        TextBlock Etykieta(string s) { var t = T(s, 12); t.VerticalAlignment = VerticalAlignment.Center; return t; }
        pasek1.Children.Add(Etykieta("Port UDP:")); pasek1.Children.Add(port); pasek1.Children.Add(btnStart);
        pasek1.Children.Add(Btn("Wyczyść", (a, b) => { odb.Wyczysc(); wiersze.Clear(); obiekty.Clear(); zdarzenia.Clear(); }));
        pasek1.Children.Add(new System.Windows.Shapes.Rectangle { Width = 12 });
        pasek1.Children.Add(Btn("Zapisz mapę", (a, b) => ZapiszMape()));
        pasek1.Children.Add(Btn("Pulpit zdalny SimRail", (a, b) => { if (mapaZmieniona) ZapiszMape(); var w = (Owner as MainWindow)?.JoinSimRail(); if (w != null) MessageBox.Show(this, w, "Pulpit zdalny"); },
            new SolidColorBrush(Color.FromRgb(0x1f, 0x4a, 0x7a))));
        pasek1.Children.Add(Btn("Wczytaj mapę ponownie", (a, b) => { WczytajMape(); OdswiezMapeWierszy(); }));
        // posterunki SimRail tego układu (każdy posterunek gry – własny układ i własna mapa)
        var pasek3 = new WrapPanel();
        pasek3.Children.Add(Etykieta("Posterunki SimRail tego układu:"));
        posterunki.Margin = new Thickness(6, 0, 8, 0); posterunki.VerticalAlignment = VerticalAlignment.Center;
        pasek3.Children.Add(posterunki);
        pasek3.Children.Add(Btn("Przypisz posterunek z gry", (a, b) => PrzypiszPosterunek()));
        pasek3.Children.Add(Btn("Odepnij posterunek…", (a, b) => OdepnijPosterunek()));
        var pasek2 = new WrapPanel();
        pasek2.Children.Add(Etykieta("Filtr:")); pasek2.Children.Add(filtr);
        pasek2.Children.Add(tylkoZajete); pasek2.Children.Add(tylkoOdcinki); pasek2.Children.Add(tylkoBezMapy);

        var head = new StackPanel(); DockPanel.SetDock(head, Dock.Top);
        head.Children.Add(T("Stany posterunku z gry SimRail (mod SimRailMost, UDP na localhost). Tylko podgląd – dane nie trafiają do komputera zależności. " +
                            "Kolumnę „Odcinek SUK” wypełnia właściciel (przypisanie projektowe), np. POST:nazwa.", 11, Brushes.Gray));
        head.Children.Add(pasek1); head.Children.Add(pasek3); head.Children.Add(pasek2); head.Children.Add(status); head.Children.Add(ostrzezenie); head.Children.Add(infoMapy);

        var tabs = new TabControl { Background = Brushes.Black };
        tabs.Items.Add(new TabItem { Header = "Obiekty", Content = gObiekty, Foreground = Brushes.Black });
        tabs.Items.Add(new TabItem { Header = "Zajęcia i zwolnienia", Content = gZdarzenia, Foreground = Brushes.Black });

        var dock = new DockPanel { Margin = new Thickness(8) };
        dock.Children.Add(head); dock.Children.Add(tabs); Content = dock;

        var tm = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        tm.Tick += (a, b) => Odswiez(); tm.Start();
        Closing += (a, e) =>
        {
            if (mapaZmieniona && MessageBox.Show(this, "Mapa SimRail → SUK ma niezapisane zmiany. Zapisać?", "SimRail",
                                                 MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) ZapiszMape();
        };
        Closed += (a, b) => { tm.Stop(); if (!SimRailSession.Aktywna) odb.Stop(); };
        if (!odb.Dziala) PrzelaczOdbior(); else { port.Text = odb.Port.ToString(); btnStart.Content = "Zatrzymaj"; }
        Odswiez();
    }

    static void KolumnaOdczyt(DataGrid g, string h, string path, double w) =>
        g.Columns.Add(new DataGridTextColumn
        {
            Header = h, Binding = new Binding(path) { Mode = BindingMode.OneWay }, IsReadOnly = true,
            Width = double.IsNaN(w) ? new DataGridLength(1, DataGridLengthUnitType.Star) : new DataGridLength(w),
        });

    /// <summary>Identyfikatory z układu, którym można przypisać obiekt SimRail: odcinki, zwrotnice, sygnalizatory, wykolejnice.</summary>
    static HashSet<string> ZnaneObiektySuk(Station st)
    {
        var s = new HashSet<string>(StringComparer.Ordinal);
        if (st == null) return s;
        foreach (var e in st.Edges.Values) if (!string.IsNullOrEmpty(e.Sec)) s.Add(e.Sec);
        foreach (var k in st.Sections.Keys) s.Add(k);
        foreach (var k in st.Switches.Keys) s.Add(k);
        foreach (var k in st.Signals.Keys) s.Add(k);
        foreach (var k in st.Derailers.Keys) s.Add(k);
        return s;
    }

    void PrzelaczOdbior()
    {
        if (odb.Dziala) { if (SimRailSession.Aktywna) { status.Text = "Odbiór jest używany przez pulpit zdalny SimRail."; return; } odb.Stop(); btnStart.Content = "Nasłuchuj"; return; }
        if (!int.TryParse(port.Text.Trim(), out var p) || p < 1 || p > 65535) { status.Text = "Nieprawidłowy port."; return; }
        try { odb.Start(p); btnStart.Content = "Zatrzymaj"; }
        catch (Exception ex) { status.Text = $"Nie można nasłuchiwać na porcie {p}: {ex.Message}"; btnStart.Content = "Nasłuchuj"; }
    }

    bool Filtr(object o)
    {
        if (o is not ObiektRow r) return false;
        if (tylkoZajete.IsChecked == true && !r.Zajety) return false;
        if (tylkoOdcinki.IsChecked == true && r.Rodzaj != "odcinek") return false;
        if (tylkoBezMapy.IsChecked == true && r.Suk.Length > 0) return false;
        var f = filtr.Text.Trim();
        return f.Length == 0 || r.Obiekt.Contains(f, StringComparison.OrdinalIgnoreCase) || r.Suk.Contains(f, StringComparison.OrdinalIgnoreCase)
            || r.Pociagi.Contains(f, StringComparison.OrdinalIgnoreCase) || r.Posterunek.Contains(f, StringComparison.OrdinalIgnoreCase);
    }

    void Odswiez()
    {
        bool nowe = false, zmianaZaj = false;
        foreach (var o in odb.Obiekty())
        {
            if (!wiersze.TryGetValue(o.Nazwa, out var r))
            {
                r = new ObiektRow { Obiekt = o.Nazwa };
                r.Suk = mapa.Mapa.TryGetValue(o.Nazwa, out var s) ? s : "";
                wiersze[o.Nazwa] = r; obiekty.Add(r); nowe = true;
                UstawUwagi(r);
            }
            if (r.Zajety != o.Zajety) zmianaZaj = true;
            r.Zajety = o.Zajety;
            r.Rodzaj = o.Rodzaj;
            r.Posterunek = o.Stacja;
            r.Stan = o.Zajety ? "ZAJĘTY" : string.IsNullOrEmpty(o.Basic) ? "wolny" : o.Basic == "StanPodstawowy" ? "wolny" : o.Basic;
            r.Pociagi = o.Pociagi;
            r.Przebieg = o.Przebieg == "Basic" ? "" : o.Przebieg;
            r.Blokada = o.Blokada == "Basic" || o.Blokada == "StanPodstawowy" ? "" : o.Blokada;
            r.Zwrotnica = o.Zwrotnica;
        }
        foreach (var z in odb.PobierzZdarzenia())
            zdarzenia.Insert(0, new ZdarzenieRow
            {
                CzasGry = z.CzasGry, Odebrano = z.Odebrano.ToString("HH:mm:ss"), Obiekt = z.Nazwa, Rodzaj = z.Rodzaj,
                Stan = z.Zajety ? "ZAJĘTY" : "wolny", Pociagi = z.Pociagi, Suk = mapa.Mapa.TryGetValue(z.Nazwa, out var s) ? s : "",
            });
        while (zdarzenia.Count > 2000) zdarzenia.RemoveAt(zdarzenia.Count - 1);
        if (nowe || (zmianaZaj && tylkoZajete.IsChecked == true)) widok.Refresh();
        foreach (var item in gObiekty.Items) if (gObiekty.ItemContainerGenerator.ContainerFromItem(item) is DataGridRow row) KolorujWiersz(row);

        var wiek = odb.OstatniPakiet == default ? "brak danych" : $"ostatni pakiet {(DateTime.Now - odb.OstatniPakiet).TotalSeconds:0} s temu";
        int zajete = wiersze.Values.Count(r => r.Zajety);
        status.Text = (odb.Dziala ? $"Nasłuch UDP 127.0.0.1:{odb.Port}" : "Nasłuch zatrzymany") +
                      $" · {wiek} · posterunek: {(odb.PosterunkiGry() is { Count: > 0 } pg ? string.Join(", ", pg) : "–")} · źródło: {(odb.Zrodlo.Length > 0 ? odb.Zrodlo : "–")}" +
                      $" · czas gry: {(odb.CzasGry.Length > 0 ? odb.CzasGry : "–")} · obiektów: {wiersze.Count}, zajętych: {zajete}" +
                      $" · pakietów: {odb.Pakiety}" + (odb.Bledy > 0 ? $", błędnych: {odb.Bledy} ({odb.OstatniBlad})" : "");
        posterunki.Text = mapa.Posterunki.Count > 0 ? string.Join(", ", mapa.Posterunki) : "(brak – układ nieprzypisany)";
        ostrzezenie.Text = OstrzezeniePosterunku(mapa, odb.PosterunkiGry());
        ostrzezenie.Visibility = ostrzezenie.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        int przypisane = wiersze.Values.Count(r => r.Suk.Length > 0);
        infoMapy.Text = $"Mapa: {plikMapy}{(mapaZmieniona ? " (niezapisane zmiany)" : "")} · przypisanych: {przypisane} z {wiersze.Count}" +
                        (station == null ? " · brak wczytanego układu – nie sprawdzam identyfikatorów SUK" : "");
    }

    static readonly Brush TloZajety = new SolidColorBrush(Color.FromRgb(0x4a, 0x12, 0x12));
    static void KolorujWiersz(DataGridRow row)
    {
        if (row.Item is not ObiektRow r) return;
        var tlo = r.Zajety ? TloZajety : null;
        if (!ReferenceEquals(row.Background, tlo)) { if (tlo == null) row.ClearValue(BackgroundProperty); else row.Background = tlo; }
    }

    void UstawUwagi(ObiektRow r) =>
        r.Uwagi = r.Suk.Length == 0 || station == null ? "" : znaneSuk.Contains(r.Suk) ? "" : "brak takiego obiektu w układzie SUK";

    void PoEdycji(ObiektRow r)
    {
        if (r == null) return;
        var v = r.Suk.Trim();
        if (v != r.Suk) r.Suk = v;
        mapa.Mapa.TryGetValue(r.Obiekt, out var stara);
        if ((stara ?? "") == v) return;
        if (v.Length == 0) mapa.Mapa.Remove(r.Obiekt); else mapa.Mapa[r.Obiekt] = v;
        mapaZmieniona = true;
        UstawUwagi(r);
    }

    void WczytajMape()
    {
        try { mapa = SimRailMapa.Wczytaj(plikMapy); mapaZmieniona = false; }
        catch (Exception ex)
        {
            mapa = new SimRailMapa();
            MessageBox.Show(this, $"Nie można wczytać mapy {plikMapy}:{Environment.NewLine}{ex.Message}", "SimRail", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void OdswiezMapeWierszy()
    {
        foreach (var r in wiersze.Values) { r.Suk = mapa.Mapa.TryGetValue(r.Obiekt, out var s) ? s : ""; UstawUwagi(r); }
        widok.Refresh();
    }

    /// <summary>Ostrzeżenie, gdy gra nadaje posterunek, którego układ nie obsługuje (pusty tekst – wszystko w porządku).</summary>
    public static string OstrzezeniePosterunku(SimRailMapa m, IReadOnlyCollection<string> stacjeGry)
    {
        if (stacjeGry.Count == 0) return "";
        var gra = string.Join(", ", stacjeGry);
        if (m.Posterunki.Count == 0)
            return $"Ten układ nie jest przypisany do posterunku SimRail. W grze: „{gra}” – jeśli to ten posterunek, kliknij „Przypisz posterunek z gry”.";
        var brak = stacjeGry.Where(s => !m.Obejmuje(s)).ToList();
        if (brak.Count == 0) return "";
        if (brak.Count < stacjeGry.Count)   // LCS: część posterunków gry poza układem – ich obiekty pulpit pomija
            return $"Posterunki gry spoza układu: {string.Join(", ", brak)} – ich obiekty pulpit zdalny pomija (przypisz je, jeśli należą do tego LCS).";
        return $"W grze jest posterunek „{gra}”, a ten układ obsługuje: {string.Join(", ", m.Posterunki)}. Uruchom układ tego posterunku albo przypisz go tutaj.";
    }

    void PrzypiszPosterunek()
    {
        var nowe = odb.PosterunkiGry().Where(s => !mapa.Obejmuje(s)).ToList();
        if (odb.PosterunkiGry().Count == 0) { MessageBox.Show(this, "Gra nie nadała jeszcze nazwy posterunku (mod SimRailMost, nasłuch włączony?).", "SimRail"); return; }
        if (nowe.Count == 0) return;
        var inne = nowe.SelectMany(s => SimRailMapa.UkladyDlaPosterunku(InneUklady(), s)).Distinct().ToList();
        var pyt = $"Przypisać posterunek SimRail „{string.Join(", ", nowe)}” do układu {Path.GetFileName(station?.SourcePath ?? plikMapy)}?" +
                  (inne.Count > 0 ? $"{Environment.NewLine}{Environment.NewLine}Już przypisany do: {string.Join(", ", inne.Select(Path.GetFileName))}." : "");
        if (MessageBox.Show(this, pyt, "SimRail", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        mapa.Posterunki.AddRange(nowe.Select(s => s.Trim()));
        ZapiszMape();
    }

    void OdepnijPosterunek()
    {
        if (mapa.Posterunki.Count == 0) return;
        var p = mapa.Posterunki[^1];
        var wGrze = mapa.Posterunki.FirstOrDefault(x => odb.PosterunkiGry().Any(s => SimRailMapa.Klucz(s) == SimRailMapa.Klucz(x)));
        if (mapa.Posterunki.Count > 1 && wGrze != null) p = wGrze;
        if (MessageBox.Show(this, $"Odpiąć posterunek SimRail „{p}” od tego układu? Mapa obiektów zostaje.", "SimRail", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        mapa.Posterunki.Remove(p);
        ZapiszMape();
    }

    /// <summary>Pozostałe układy programu (Data/station.json i Data/projekty/*.json) – do sprawdzenia przypisań posterunku.</summary>
    IEnumerable<string> InneUklady()
    {
        var src = station?.SourcePath;
        if (src == null) yield break;
        var dir = Path.GetDirectoryName(src) ?? ".";
        // układ z Data/projekty → katalog danych wyżej
        var data = string.Equals(Path.GetFileName(dir), "projekty", StringComparison.OrdinalIgnoreCase) ? Path.GetDirectoryName(dir) ?? dir : dir;
        var lista = new List<string>();
        if (File.Exists(Path.Combine(data, "station.json"))) lista.Add(Path.Combine(data, "station.json"));
        var proj = Path.Combine(data, "projekty");
        if (Directory.Exists(proj)) lista.AddRange(Directory.GetFiles(proj, "*.json").Where(f => !f.EndsWith(".simrail.json", StringComparison.OrdinalIgnoreCase)));
        foreach (var f in lista) if (!string.Equals(Path.GetFullPath(f), Path.GetFullPath(src), StringComparison.OrdinalIgnoreCase)) yield return f;
    }

    void ZapiszMape()
    {
        gObiekty.CommitEdit(DataGridEditingUnit.Row, true);
        try { mapa.Zapisz(plikMapy); mapaZmieniona = false; }
        catch (Exception ex) { MessageBox.Show(this, $"Nie można zapisać mapy {plikMapy}:{Environment.NewLine}{ex.Message}", "SimRail", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}

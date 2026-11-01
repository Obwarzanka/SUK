using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using LcsWidzew.Sim;

namespace LcsWidzew.UI;

/// <summary>
/// E9 (PLAN_EDR.md): menu Plik / Dyżury / Widok / Okna / Pomoc, zakładki szczegółów pociągu i „Usługi”, układ dzienników (na dole, z prawej,
/// w osobnym oknie), powiększenie, drugie okno – jak w EDR.exe.
/// </summary>
public partial class EdrWindow
{
    internal string PostWybrany => wykazPanel.Posterunek;
    internal EdrPociagLokalny Lok(EdrPociag p) => p == null ? new() : Dane?.Lokalne.GetValueOrDefault(PostWybrany + "|" + p.Klucz) ?? new();
    internal string NazwaPost(string p) => Dane?.Posterunki.GetValueOrDefault(p ?? "", p) ?? p;
    internal EdrPosterunekUst PostUst(string p) => Dane?.PosterunkiUst.FirstOrDefault(x => x.Kod == p);

    static MenuItem Poz(string t, Action a, bool czynne = true) { var m = new MenuItem { Header = t, IsEnabled = czynne }; m.Click += (s, e) => a(); return m; }

    // ------------------------------------------------------------------ menu
    /// <summary>Plik: sesja z dziennikami, kopia danych, nowa sesja ćwiczeniowa, rozkład (dane lokalne, CSV), konfiguracja posterunku, zakończ.</summary>
    MenuItem MenuPlik()
    {
        var m = new MenuItem { Header = "Plik" };
        m.SubmenuOpened += (a, e) =>
        {
            if (e.OriginalSource != m) return;
            m.Items.Clear();
            m.Items.Add(Poz("Zapisz sesję z dziennikami…", ZapiszSesje));
            m.Items.Add(Poz("Wczytaj sesję z dziennikami…", WczytajSesje, Dane?.Obserwator == false));
            m.Items.Add(Poz("Kopia wszystkich danych…", KopiaDanych));
            m.Items.Add(Poz("Nowa sesja ćwiczeniowa", NowaSesja, Dane?.Obserwator == false));
            m.Items.Add(new Separator());
            m.Items.Add(Poz("Dodaj dane lokalne…", () => new OknoPociaguLok(this, PostWybrany, null) { Owner = this }.ShowDialog(), PostWybrany != null));
            m.Items.Add(Poz("Importuj rozkład z CSV…", ImportCsv, PostWybrany != null));
            m.Items.Add(Poz("Eksportuj widoczny rozkład…", EksportCsv, PostWybrany != null));
            m.Items.Add(new Separator());
            m.Items.Add(Poz("Zapisz konfigurację posterunku…", ZapiszKonfiguracje, PostWybrany != null));
            m.Items.Add(Poz("Wczytaj konfigurację posterunku…", WczytajKonfiguracje, Dane?.Admin == true));
            m.Items.Add(new Separator());
            m.Items.Add(Poz("Zakończ", Close));
        };
        m.Items.Add(new MenuItem { Header = "…" });
        return m;
    }

    /// <summary>Dyżury: przyjęcie / zdanie dyżuru, logowanie użytkownika lokalnego, sytuacja na szlakach, historia zmian.</summary>
    MenuItem MenuDyzury()
    {
        var m = new MenuItem { Header = "Dyżury" };
        m.SubmenuOpened += (a, e) =>
        {
            if (e.OriginalSource != m) return;
            m.Items.Clear();
            m.Items.Add(Poz("Przyjęcie / zdanie dyżuru…", () => new OknoDyzuru(this, PostWybrany) { Owner = this }.ShowDialog(), PostWybrany != null));
            m.Items.Add(Poz("Bieżąca sytuacja ruchowa na szlakach", () => new OknoSytuacji(this, PostWybrany, false) { Owner = this }.ShowDialog(), PostWybrany != null));
            m.Items.Add(new Separator());
            m.Items.Add(Poz("Zaloguj użytkownika lokalnego…", () => new OknoLogowania(this) { Owner = this }.ShowDialog(), Dane?.Uzytkownicy.Count > 0));
            m.Items.Add(Poz("Wyloguj użytkownika lokalnego", () => Wyslij(new EdrPolecenie { Rodzaj = "wyloguj" }), Dane?.Operator != null));
            m.Items.Add(new Separator());
            m.Items.Add(Poz("Historia zmian", () => new OknoHistorii(this, null) { Owner = this }.Show()));
        };
        m.Items.Add(new MenuItem { Header = "…" });
        return m;
    }

    /// <summary>Widok: układ dzienników, powiększenie, legenda.</summary>
    MenuItem MenuWidok()
    {
        var m = new MenuItem { Header = "Widok" };
        m.SubmenuOpened += (a, e) =>
        {
            if (e.OriginalSource != m) return;
            m.Items.Clear();
            foreach (var (t, u) in new[] { ("Dzienniki na dole", "dol"), ("Dzienniki z prawej", "prawo"), ("Dzienniki w osobnym oknie", "okno") })
            { var mi = Poz(t, () => UstawUklad(u)); mi.IsCheckable = true; mi.IsChecked = uklad9 == u; m.Items.Add(mi); }
            m.Items.Add(new Separator());
            var pw = new MenuItem { Header = "Powiększenie" };
            foreach (var z in new[] { 0.8, 0.9, 1.0, 1.1, 1.25, 1.5 })
            { var zz = z; var mi = Poz($"{z * 100:0}%", () => Powieksz(zz)); mi.IsCheckable = true; mi.IsChecked = Math.Abs(zoom - z) < 0.01; pw.Items.Add(mi); }
            m.Items.Add(pw);
            m.Items.Add(new Separator());
            m.Items.Add(Poz("Legenda", () => new LegendaEdr { Owner = this }.Show()));
        };
        m.Items.Add(new MenuItem { Header = "…" });
        return m;
    }

    MenuItem MenuOkna()
    {
        var m = new MenuItem { Header = "Okna" };
        m.Items.Add(Poz("Otwórz drugie okno", () => { var w = new EdrWindow(ses) { Owner = Owner }; w.Show(); }));
        m.Items.Add(Poz("Dzienniki w osobnym oknie", () => UstawUklad("okno")));
        return m;
    }

    MenuItem MenuPomoc()
    {
        var m = new MenuItem { Header = "Pomoc" };
        m.Items.Add(Poz("Legenda", () => new LegendaEdr { Owner = this }.Show()));
        m.Items.Add(Poz("Komunikaty", () => MessageBox.Show(this, "EDR – elektroniczny dziennik ruchu symulatora LCS.\n\nRozkład jazdy i czasy rzeczywiste pochodzą z symulacji; dane lokalne pociągów, dzienniki i telefonogramy są w EDR.\n" +
            "EDR niczego nie nastawia w komputerze zależności i nie zmienia ruchu.\n\nDzienniki zapisują się w pliku obok układu (ciągła historia); przy zamknięciu programu – pytanie „Zachować dane sesji EDR?”.",
            "Komunikaty", MessageBoxButton.OK, MessageBoxImage.Information)));
        return m;
    }

    // ------------------------------------------------------------------ pliki
    static string Wybierz(bool zapis, string filtr, string nazwa)
    {
        Microsoft.Win32.FileDialog d = zapis ? new Microsoft.Win32.SaveFileDialog { FileName = nazwa } : new Microsoft.Win32.OpenFileDialog();
        d.Filter = filtr;
        return d.ShowDialog() == true ? d.FileName : null;
    }

    void ZapiszSesje()
    {
        var r = ses.EdrCmd(new EdrPolecenie { Rodzaj = "eksportsesji" });
        if (!r.Ok) { Info(r.Msg); return; }
        var f = Wybierz(true, "Sesja EDR (*.json)|*.json", $"sesja_edr_{DateTime.Now:yyyyMMdd_HHmm}.json");
        if (f == null) return;
        File.WriteAllText(f, r.Msg); Info($"Sesja z dziennikami zapisana: {f}");
    }

    void WczytajSesje()
    {
        var f = Wybierz(false, "Sesja EDR (*.json)|*.json", null);
        if (f == null) return;
        if (MessageBox.Show(this, "Wczytać sesję? Dzienniki z pliku zastąpią bieżące strony tych dzienników (jako historia).", "Wczytaj sesję z dziennikami", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        Wyslij(new EdrPolecenie { Rodzaj = "wczytajsesje", Plik = File.ReadAllText(f) });
    }

    void KopiaDanych()
    {
        var r = ses.EdrCmd(new EdrPolecenie { Rodzaj = "eksportsesji" });
        if (!r.Ok) { Info(r.Msg); return; }
        var f = Wybierz(true, "Archiwum ZIP (*.zip)|*.zip", $"edr_kopia_{DateTime.Now:yyyyMMdd_HHmm}.zip");
        if (f == null) return;
        if (File.Exists(f)) File.Delete(f);
        using (var z = System.IO.Compression.ZipFile.Open(f, System.IO.Compression.ZipArchiveMode.Create))
        {
            void Dodaj(string n, string t) { using var s = new StreamWriter(z.CreateEntry(n).Open()); s.Write(t); }
            Dodaj("dzienniki.json", r.Msg);
            foreach (var p in Dane?.Posterunki.Keys ?? Enumerable.Empty<string>())
                if (ses.EdrCmd(new EdrPolecenie { Rodzaj = "eksportkonf", Post = p }) is { Ok: true } k) Dodaj($"konfiguracja_{p}.json", k.Msg);
        }
        Info($"Kopia wszystkich danych EDR: {f}");
    }

    void NowaSesja()
    {
        if (MessageBox.Show(this, "Rozpocząć nową sesję ćwiczeniową? Dzienniki ruchu i telefoniczny posterunków stanowiska zaczną się od nowa.\nPrzy zamknięciu programu „Nie zapisuj” przywraca dzienniki sprzed sesji.",
            "Nowa sesja ćwiczeniowa", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) Wyslij(new EdrPolecenie { Rodzaj = "nowasesja" });
    }

    void ZapiszKonfiguracje()
    {
        var r = ses.EdrCmd(new EdrPolecenie { Rodzaj = "eksportkonf", Post = PostWybrany });
        if (!r.Ok) { Info(r.Msg); return; }
        var f = Wybierz(true, "Konfiguracja posterunku EDR (*.json)|*.json", $"konfiguracja_{PostWybrany}.json");
        if (f == null) return;
        File.WriteAllText(f, r.Msg); Info($"Konfiguracja posterunku zapisana: {f}");
    }

    void WczytajKonfiguracje()
    {
        var f = Wybierz(false, "Konfiguracja posterunku EDR (*.json)|*.json", null);
        if (f != null) Wyslij(new EdrPolecenie { Rodzaj = "konfpost", Plik = File.ReadAllText(f) });
    }

    static readonly string[] KolumnyCsv = { "Numer", "Rodzaj", "Przewoźnik", "Z kierunku", "W kierunku", "Tor", "Przyjazd", "Odjazd", "Uwagi" };

    static string Csv(IEnumerable<string> pola) => string.Join(";", pola.Select(x => (x ?? "").Contains(';') || (x ?? "").Contains('"') || (x ?? "").Contains('\n') ? "\"" + x.Replace("\"", "\"\"") + "\"" : x ?? ""));

    /// <summary>Wiersze CSV (separator „;” albo „,”, cudzysłowy) – null przy niezamkniętym cudzysłowie.</summary>
    internal static List<List<string>> CzytajCsv(string tekst)
    {
        var res = new List<List<string>>(); var w = new List<string>(); var sb = new System.Text.StringBuilder(); bool cudz = false;
        char sep = tekst.Split('\n')[0].Count(c => c == ';') >= tekst.Split('\n')[0].Count(c => c == ',') ? ';' : ',';
        for (int i = 0; i < tekst.Length; i++)
        {
            char c = tekst[i];
            if (cudz) { if (c == '"') { if (i + 1 < tekst.Length && tekst[i + 1] == '"') { sb.Append('"'); i++; } else cudz = false; } else sb.Append(c); continue; }
            if (c == '"') cudz = true;
            else if (c == sep) { w.Add(sb.ToString()); sb.Clear(); }
            else if (c == '\n') { w.Add(sb.ToString().TrimEnd('\r')); sb.Clear(); if (w.Any(x => x.Trim() != "")) res.Add(w); w = new(); }
            else sb.Append(c);
        }
        if (cudz) return null;
        w.Add(sb.ToString().TrimEnd('\r')); if (w.Any(x => x.Trim() != "")) res.Add(w);
        return res;
    }

    /// <summary>Import rozkładu z CSV (jak w EDR.exe): pociągi lokalne posterunku; nagłówek: Numer;Rodzaj;Przewoźnik;Z kierunku;W kierunku;Tor;Przyjazd;Odjazd;Uwagi.</summary>
    void ImportCsv()
    {
        var f = Wybierz(false, "Rozkład CSV (*.csv)|*.csv", null);
        if (f == null) return;
        var w = CzytajCsv(File.ReadAllText(f));
        if (w == null) { Info("Niezamknięty cudzysłów w pliku CSV."); return; }
        if (w.Count < 2) { Info("CSV nie zawiera danych."); return; }
        if (!w[0].Select(x => x.Trim()).Take(KolumnyCsv.Length).SequenceEqual(KolumnyCsv)) { Info("Nieprawidłowy nagłówek. Wzór: " + string.Join(";", KolumnyCsv)); return; }
        var l = new List<EdrPociagLok>();
        for (int i = 1; i < w.Count; i++)
        {
            var x = w[i];
            if (x.Count < KolumnyCsv.Length) { Info($"Nieprawidłowa liczba kolumn w wierszu {i + 1}."); return; }
            l.Add(new EdrPociagLok { Nr = x[0], Rodzaj = x[1], Przewoznik = x[2], ZKierunku = x[3], WKierunku = x[4], Tor = x[5], Przyjazd = x[6], Odjazd = x[7], Uwagi = x[8] });
        }
        Wyslij(new EdrPolecenie { Rodzaj = "importcsv", Post = PostWybrany, PociagiLok = l });
    }

    /// <summary>Eksport widocznego wykazu do CSV (ten sam układ kolumn co import).</summary>
    void EksportCsv()
    {
        var f = Wybierz(true, "Rozkład CSV (*.csv)|*.csv", $"rozklad_{PostWybrany}_{Dane?.Data}.csv");
        if (f == null) return;
        var sb = new System.Text.StringBuilder(Csv(KolumnyCsv) + "\r\n");
        foreach (var p in wykazPanel.Widoczne) sb.Append(Csv(new[] { p.Nr == p.NrWyj ? p.Nr : p.Nr + "/" + p.NrWyj, p.Rodz, p.Przewoznik, p.ZKierunku, p.WKierunku, p.TorRzecz != "" ? p.TorRzecz : p.Tor, p.PrzPlan, p.OdjPlan, p.UwagiWlasne })).Append("\r\n");
        File.WriteAllText(f, sb.ToString(), new System.Text.UTF8Encoding(true));
        Info($"Wykaz zapisany: {f}");
    }

    // ------------------------------------------------------------------ układ okna i powiększenie
    string uklad9 = "dol";
    /// <summary>Pole zegara i sprawdzeń w prawym górnym rogu (układ z prawej – dzienniki pod nim).</summary>
    FrameworkElement rog9;
    double zoom = 1;
    Window oknoDziennikow;

    internal void Powieksz(double z) { zoom = z; srodek.LayoutTransform = Math.Abs(z - 1) < 0.01 ? Transform.Identity : new ScaleTransform(z, z); }

    /// <summary>Dzienniki na dole (jak dotąd), z prawej strony wykazu albo w osobnym oknie (drugi monitor) – jak „Layout” w EDR.exe.</summary>
    internal void UstawUklad(string u)
    {
        if (oknoDziennikow != null && u != "okno") { var o = oknoDziennikow; oknoDziennikow = null; o.Content = null; o.Close(); }
        uklad9 = u;
        srodek.Children.Clear(); srodek.RowDefinitions.Clear(); srodek.ColumnDefinitions.Clear();
        var spl = new GridSplitter { Background = Linia, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        // pasek wykazu zostawia miejsce na zegar tylko wtedy, gdy zegar stoi nad nim; z prawej – zegar nad dziennikami
        zakladki.Margin = u == "prawo" ? new Thickness(0, rog9?.ActualHeight ?? 110, 0, 0) : new Thickness(0);
        wykazPanel.MiejsceNaZegar(u == "prawo" ? 4 : (rog9?.ActualWidth ?? 340) + 8);
        if (u == "prawo")
        {
            srodek.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
            srodek.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            srodek.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            srodek.Children.Add(zakladkiPociagu); Grid.SetColumn(zakladkiPociagu, 0); Grid.SetRow(zakladkiPociagu, 0);
            srodek.Children.Add(spl); Grid.SetColumn(spl, 1);
            srodek.Children.Add(zakladki); Grid.SetColumn(zakladki, 2); Grid.SetRow(zakladki, 0);
            return;
        }
        srodek.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.25, GridUnitType.Star) });
        if (u == "okno")
        {
            srodek.Children.Add(zakladkiPociagu); Grid.SetRow(zakladkiPociagu, 0); Grid.SetColumn(zakladkiPociagu, 0);
            if (oknoDziennikow == null)
            {
                oknoDziennikow = new Window { Title = Title + " – dzienniki ruchu", Width = 1200, Height = 650, Owner = this, Background = Tlo, Content = zakladki, ShowInTaskbar = true };
                oknoDziennikow.Closed += (a, e) => { if (oknoDziennikow != null) { oknoDziennikow.Content = null; oknoDziennikow = null; UstawUklad("dol"); } };
                oknoDziennikow.Show();
            }
            return;
        }
        srodek.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
        srodek.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        srodek.Children.Add(zakladkiPociagu); Grid.SetRow(zakladkiPociagu, 0); Grid.SetColumn(zakladkiPociagu, 0);
        srodek.Children.Add(spl); Grid.SetRow(spl, 1);
        srodek.Children.Add(zakladki); Grid.SetRow(zakladki, 2); Grid.SetColumn(zakladki, 0);
    }

    // ------------------------------------------------------------------ zakładki szczegółów pociągu i usługi
    readonly Dictionary<string, DataGrid> tabeleLok = new();
    readonly DataGrid uslugiGrid = new() { AutoGenerateColumns = false, IsReadOnly = true, HeadersVisibility = DataGridHeadersVisibility.Column, FontSize = 12, Margin = new Thickness(4), SelectionMode = DataGridSelectionMode.Single, Background = Brushes.White };
    EdrPociag wybrany9;
    string lokPodpis;

    static readonly System.Text.Json.JsonSerializerOptions JsonPola = new() { IncludeFields = true };
    static readonly Brush Pasek = Pedzel(255, 225, 199), PasekJasny = Pedzel(255, 241, 201);

    static TextBlock Band(string t, Brush tlo = null) => new() { Text = t, Background = tlo ?? Pasek, FontWeight = FontWeights.Bold, Padding = new Thickness(6, 3, 6, 3), Foreground = Pedzel(25, 65, 90) };

    /// <summary>Tabela danych lokalnych (kolumny wg <see cref="Edr.Tabele"/>) z przyciskami Dodaj / Edytuj / Usuń.</summary>
    FrameworkElement Tabela(string id, bool reczna = true)
    {
        var def = Edr.Tabele.First(x => x.Id == id);
        var g = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, HeadersVisibility = DataGridHeadersVisibility.Column, FontSize = 12, SelectionMode = DataGridSelectionMode.Single, Background = Brushes.White, MinHeight = 60, RowBackground = Pedzel(255, 255, 225) };
        for (int i = 0; i < def.Kolumny.Length; i++)
            g.Columns.Add(new DataGridTextColumn { Header = def.Kolumny[i], Binding = new Binding($"Komorki[{i}]"), Width = new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 50 });
        if (id == "zarzadzenia") g.Columns.Add(new DataGridTextColumn { Header = "Telegram PDF", Binding = new Binding(nameof(EdrWierszLok.Zalacznik)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        tabeleLok[id] = g;
        var dock = new DockPanel();
        var b = Band(def.Tytul, id.StartsWith("skom") || id.StartsWith("sklad") ? PasekJasny : null); DockPanel.SetDock(b, Dock.Top); dock.Children.Add(b);
        if (reczna)
        {
            var bp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            Button Pr(string t, Action a) { var x = new Button { Content = t, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(2, 0, 2, 0) }; x.Click += (s, e) => a(); bp.Children.Add(x); return x; }
            Pr("Dodaj…", () => { if (wybrany9 != null) new OknoWierszaLok(this, PostWybrany, wybrany9, id, null) { Owner = this }.ShowDialog(); });
            Pr("Edytuj…", () => { if (wybrany9 != null && g.SelectedItem is EdrWierszLok w) new OknoWierszaLok(this, PostWybrany, wybrany9, id, w) { Owner = this }.ShowDialog(); });
            Pr("Usuń", () => { if (wybrany9 != null && g.SelectedItem is EdrWierszLok w) Wyslij(new EdrPolecenie { Rodzaj = "pociag", Pole = "usunwiersz", Pole2 = w.Id, Post = PostWybrany, Klucz = wybrany9.Klucz, Wartosc = wybrany9.Nr }); });
            if (id == "zarzadzenia") Pr("Otwórz telegram PDF", () => { if (g.SelectedItem is EdrWierszLok w && File.Exists(w.Zalacznik)) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(w.Zalacznik) { UseShellExecute = true }); else Info("Do tego zarządzenia nie dołączono pliku telegramu PDF."); });
            g.MouseDoubleClick += (s, e) => { if (wybrany9 != null && g.SelectedItem is EdrWierszLok w) new OknoWierszaLok(this, PostWybrany, wybrany9, id, w) { Owner = this }.ShowDialog(); };
            DockPanel.SetDock(bp, Dock.Bottom); dock.Children.Add(bp);
        }
        dock.Children.Add(g);
        return dock;
    }

    static Grid Dwie(FrameworkElement a, FrameworkElement b)
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition());
        g.Children.Add(a); Grid.SetColumn(b, 1); g.Children.Add(b); a.Margin = b.Margin = new Thickness(1);
        return g;
    }

    static Grid Wiersze(params (FrameworkElement E, double H)[] el)
    {
        var g = new Grid();
        for (int i = 0; i < el.Length; i++)
        {
            g.RowDefinitions.Add(new RowDefinition { Height = el[i].H > 0 ? new GridLength(el[i].H, GridUnitType.Star) : GridLength.Auto });
            Grid.SetRow(el[i].E, i); g.Children.Add(el[i].E);
        }
        return g;
    }

    readonly TextBlock skomInfo = new() { Margin = new Thickness(6, 2, 6, 2), FontSize = 11.5 }, nadzwInfo = new() { Background = Pedzel(255, 225, 199), Padding = new Thickness(6, 2, 6, 2), FontSize = 11.5 };

    /// <summary>Zakładki szczegółów pociągu jak w EDR.exe (`BuildDetails`, `BuildConnections`) i „Usługi” posterunku.</summary>
    void ZakladkiE9()
    {
        var t = zakladkiPociagu;
        int pierwsza = t.Items.Count;
        t.Items.Add(new TabItem { Header = "Kody opóźnień", Content = Tabela("opoznienia") });
        t.Items.Add(new TabItem { Header = "Przesyłki nadzwyczajne i TWR", Content = Wiersze((Tabela("nadzwyczajne"), 62), (nadzwInfo, 0), (Dwie(Tabela("telegramy"), Tabela("telegramyobce")), 38)) });
        t.Items.Add(new TabItem { Header = "Zarządzenie RJ", Content = Tabela("zarzadzenia") });
        t.Items.Add(new TabItem { Header = "Skomunikowania / Przejścia składu", Content = Wiersze((skomInfo, 0), (Band("SKOMUNIKOWANIA"), 0), (Dwie(Tabela("skomZ"), Tabela("skomNa")), 1), (Band("Plan przejścia składu pociągu / grup wagonów"), 0), (Dwie(Tabela("skladZ"), Tabela("skladNa")), 1)) });
        var tn = Tabela("tn", false);
        var bTn = new Button { Content = "Towary niebezpieczne / TWR…", HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(2) };
        bTn.Click += (a, e) => { if (wybrany9 != null) new OknoLadunku(this, PostWybrany, wybrany9) { Owner = this }.ShowDialog(); };
        t.Items.Add(new TabItem { Header = "Towary niebezpieczne", Content = Wiersze((tn, 1), (bTn, 0)) });
        // usługi posterunku (EDR.exe – zakładka „Usługi”): rejestracja usługi, brak zgody
        foreach (var (h, p, w) in new[] { ("Rodzaj usługi", nameof(EdrUsluga.Rodzaj), 110.0), ("Numer pociągu", nameof(EdrUsluga.Pociag), 100), ("Przewoźnik", nameof(EdrUsluga.Przewoznik), 120), ("Tor", nameof(EdrUsluga.Tor), 60),
            ("Rozpoczęcie", nameof(EdrUsluga.Od), 90), ("Zakończenie", nameof(EdrUsluga.Do), 90), ("Status", nameof(EdrUsluga.Status), 110), ("Uwagi", nameof(EdrUsluga.Uwagi), 300) })
            uslugiGrid.Columns.Add(new DataGridTextColumn { Header = h, Binding = new Binding(p), Width = new DataGridLength(w) });
        var ud = new DockPanel();
        var ub = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 2, 4, 2) };
        Button U(string tx, Action a) { var x = new Button { Content = tx, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(2, 0, 2, 0) }; x.Click += (s, e) => a(); ub.Children.Add(x); return x; }
        U("Rejestruj usługę…", () => new OknoUslugi(this, PostWybrany, null, wybrany9?.Nr) { Owner = this }.ShowDialog());
        U("Edytuj…", () => { if (uslugiGrid.SelectedItem is EdrUsluga s) new OknoUslugi(this, PostWybrany, s, null) { Owner = this }.ShowDialog(); });
        U("Brak zgody na usługę…", () => { if (uslugiGrid.SelectedItem is EdrUsluga s) new OknoTekstu(this, "Brak zgody na usługę", "Powód:", "", v => Wyslij(new EdrPolecenie { Rodzaj = "brakzgody", Post = PostWybrany, Klucz = s.Id, Wartosc = v })) { Owner = this }.ShowDialog(); else Info("Wybierz usługę."); });
        DockPanel.SetDock(ub, Dock.Top); ud.Children.Add(ub); ud.Children.Add(uslugiGrid);
        t.Items.Add(new TabItem { Header = "Usługi", Content = ud });
        // zegar w prawym górnym rogu zachodzi na górę zakładek – ich zawartość zaczyna się pod nim
        foreach (var ti in t.Items.Cast<TabItem>().Skip(pierwsza)) if (ti.Content is FrameworkElement fe) fe.Margin = new Thickness(0, 72, 0, 0);
    }

    /// <summary>Zakładki szczegółów: dane lokalne wybranego pociągu (po zmianie pociągu albo danych).</summary>
    void PokazE9(EdrPociag p)
    {
        wybrany9 = p;
        var l = Lok(p);
        foreach (var (id, g) in tabeleLok) g.ItemsSource = p == null ? null : l.Wiersze.Where(x => x.Rodzaj == id).ToList();
        nadzwInfo.Text = p == null ? "" : $"Relacja: {p.StacjaPocz} – {p.StacjaKonc}   Przesyłka nadzwyczajna: {(l.PN ? "TAK" : "nie")}   TWR: {(l.TWR ? "TAK" : "nie")}   TN: {(l.TN ? "TAK" : "nie")}" + (l.Ladunek != "" ? $"   Ładunek: {l.Ladunek}" : "");
        skomInfo.Text = p == null ? "" : $"Max. czas oczekiwania bez uzgodnienia z IDDE: {l.Opis.GetValueOrDefault("MaxCzekania", "")}        Prognoza opóźnienia poc. skomunikowanego: {l.Opis.GetValueOrDefault("PrognozaSkom", "")}";
        uslugiGrid.ItemsSource = Dane?.Uslugi.Where(x => x.Post == PostWybrany).OrderBy(x => x.Od).ToList();
    }

    /// <summary>Odświeżenie zakładek po zmianie danych lokalnych (polecenie z innego okna, stanowisko przez sieć).</summary>
    void OdswiezE9(EdrData d)
    {
        var podp = string.Join("|", d.Lokalne.Select(x => x.Key + System.Text.Json.JsonSerializer.Serialize(x.Value, JsonPola))) + "#" + string.Join("|", d.Uslugi.Select(x => x.Id + x.Status + x.Do + x.Uwagi));
        if (podp == lokPodpis) return;
        lokPodpis = podp;
        PokazE9(wykazPanel.Wybrany);
        OpisPociagu(wykazPanel.Wybrany);
    }
}

/// <summary>Okno z jednym polem tekstowym (powód, uwagi własne).</summary>
public class OknoTekstu : OknoEdr
{
    public OknoTekstu(EdrWindow okno, string tytul, string etykieta, string wartosc, Func<string, CmdResult> akcja) : base(tytul, 480)
    {
        Sp.Children.Add(Et(etykieta));
        var t = new TextBox { Text = wartosc ?? "", Height = 80, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
        Sp.Children.Add(t);
        Loaded += (a, e) => t.Focus();
        Przyciski("Zapisz", () => akcja(t.Text));
    }
}

/// <summary>Nagłówek okien pociągu (jak `TrainHeading` w EDR.exe): numer, relacja, plan.</summary>
public static class NaglowekPociagu
{
    public static void Dodaj(StackPanel sp, EdrPociag p) => sp.Children.Add(new TextBlock
    {
        Text = $"Pociąg {p.Nr}{(p.NrWyj != p.Nr ? " / " + p.NrWyj : "")}{(p.StacjaPocz != "" || p.StacjaKonc != "" ? $" / Relacja: {p.StacjaPocz} – {p.StacjaKonc}" : "")}",
        FontWeight = FontWeights.Bold, FontSize = 14, Foreground = Brushes.Navy, Margin = new Thickness(0, 0, 0, 6), TextWrapping = TextWrapping.Wrap
    });

    /// <summary>Pole godziny: kółko myszy ± minuta / godzina, dwuklik – bieżąca.</summary>
    public static TextBox Godz(EdrWindow okno, string v, double szer = 70)
    {
        var t = new TextBox { Text = v ?? "", Width = szer, HorizontalAlignment = HorizontalAlignment.Left, ToolTip = "GG:MM; dwuklik – bieżąca godzina; kółko myszy – ± minuta (prawa połowa) / ± godzina (lewa)" };
        t.MouseDoubleClick += (a, e) => t.Text = okno.Teraz;
        t.MouseWheel += (a, e) => { e.Handled = true; bool h = e.GetPosition(t).X < t.ActualWidth / 2; int k = e.Delta > 0 ? 1 : -1; t.Text = EdrWindow.Przesun(t.Text, h ? k : 0, h ? 0 : k, okno.Teraz); };
        return t;
    }
}

/// <summary>Postój (pociąg STÓJ) i prognoza przyjazdu / odjazdu (jak `StopForecastForm` w EDR.exe; E9d).</summary>
public class OknoStoj : OknoEdr
{
    public OknoStoj(EdrWindow okno, string post, EdrPociag p) : base("Postój (pociąg STÓJ) i prognoza przyjazdu / odjazdu", 520)
    {
        var l = okno.Lok(p);
        NaglowekPociagu.Dodaj(Sp, p);
        Sp.Children.Add(Et("UWAGA! W tym oknie możliwa jest tylko obsługa postoju (pociąg STÓJ) i PROGNOZY Przyjazdu / Odjazdu."));
        Sp.Children.Add(new TextBlock { Text = l.Stoj ? $"Pociąg STÓJ od {l.StojOd}" : "Pociąg bez postoju", FontWeight = FontWeights.Bold, Foreground = l.Stoj ? Brushes.Firebrick : Brushes.DarkGreen, Margin = new Thickness(0, 4, 0, 4) });
        var g = new Grid(); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) }); g.ColumnDefinitions.Add(new ColumnDefinition());
        var a = NaglowekPociagu.Godz(okno, l.PrognozaPrz != "" ? l.PrognozaPrz : p.PrzRzecz); var o = NaglowekPociagu.Godz(okno, l.PrognozaOdj != "" ? l.PrognozaOdj : p.OdjRzecz);
        foreach (var (et, t, r) in new[] { ("Przyjazd:", a, 0), ("Odjazd:", o, 1) })
        {
            g.RowDefinitions.Add(new RowDefinition());
            var e = new TextBlock { Text = et, VerticalAlignment = VerticalAlignment.Center }; Grid.SetRow(e, r); g.Children.Add(e);
            t.Margin = new Thickness(0, 2, 0, 2); Grid.SetRow(t, r); Grid.SetColumn(t, 1); g.Children.Add(t);
        }
        Sp.Children.Add(g);
        var przep = new Button { Content = "Przepisz czas", HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(90, 4, 0, 4), ToolTip = "Odjazd = przyjazd" };
        przep.Click += (s, e) => o.Text = a.Text; Sp.Children.Add(przep);
        var bp = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        CmdResult W(string pole, EdrPociagLokalny lok = null) => okno.Wyslij(new EdrPolecenie { Rodzaj = "pociag", Pole = pole, Post = post, Klucz = p.Klucz, Wartosc = p.Nr, Lok = lok });
        void B(string t, Func<CmdResult> f) { var b = new Button { Content = t, Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 6, 4) }; b.Click += (s, e) => { var r = f(); if (r.Ok) Close(); else Blad.Text = r.Msg; }; bp.Children.Add(b); }
        B(l.Stoj ? "Odwołaj postój (pociąg STÓJ)" : "Wprowadź postój (pociąg STÓJ)", () => W(l.Stoj ? "odwolajstoj" : "stoj"));
        B("Zapisz PROGNOZĘ", () => W("prognoza", new EdrPociagLokalny { PrognozaPrz = a.Text, PrognozaOdj = o.Text }));
        Sp.Children.Add(bp);
        Sp.Children.Add(Blad);
        Sp.Children.Add(new Button { Content = "Anuluj", Width = 100, HorizontalAlignment = HorizontalAlignment.Right, IsCancel = true });
    }
}

/// <summary>Rzeczywisty czas przyjazdu i odjazdu (jak `TrainTimes` / `OperationTimePanel` w EDR.exe): godziny, „Dodaj do czasu” z przyczyną, „Przepisz czas”.</summary>
public class OknoGodzin : OknoEdr
{
    static readonly (string T, int Min)[] Teksty = { ("Defekt lokomotywy", 70), ("Uszkodzenie Sieci", 70), ("Wypadek na przejeździe", 120), ("Potrącenie", 120) };

    public OknoGodzin(EdrWindow okno, string post, EdrPociag p) : base("Rzeczywisty czas przyjazdu i odjazdu pociągu", 600)
    {
        var l = okno.Lok(p);
        NaglowekPociagu.Dodaj(Sp, p);
        TextBox Panel(string tyt, string plan, string v, out ComboBox dod)
        {
            var gb = new GroupBox { Header = tyt, Margin = new Thickness(0, 4, 0, 4) };
            var sp = new StackPanel { Margin = new Thickness(6) };
            sp.Children.Add(new TextBlock { Text = $"Planowo: {(plan != "" ? plan : "–")}", Foreground = Brushes.DimGray });
            var t = NaglowekPociagu.Godz(okno, v); sp.Children.Add(t);
            var r = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            r.Children.Add(new TextBlock { Text = "Dodaj do czasu: ", VerticalAlignment = VerticalAlignment.Center });
            var c = new ComboBox { Width = 260, IsEditable = true, ToolTip = "Dodaj do czasu – teksty (minuty w nawiasie); można wpisać liczbę minut" };
            foreach (var (x, m) in Teksty) c.Items.Add($"{x} [{m} min.]");
            r.Children.Add(c);
            var bd = new Button { Content = "Dodaj", Padding = new Thickness(8, 0, 8, 0), Margin = new Thickness(4, 0, 0, 0) };
            bd.Click += (s, e) =>
            {
                var m = System.Text.RegularExpressions.Regex.Match(c.Text ?? "", @"(\d+)\s*min|^\s*(\d+)\s*$");
                if (!m.Success) { Blad.Text = "Wybierz tekst albo wpisz liczbę minut."; return; }
                int min = int.Parse(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value);
                t.Text = EdrWindow.Przesun(plan != "" ? plan : t.Text, 0, min, okno.Teraz);
            };
            r.Children.Add(bd); sp.Children.Add(r);
            gb.Content = sp; Sp.Children.Add(gb);
            dod = c;
            return t;
        }
        var a = Panel("Przyjazd:", p.PrzPlan, l.CzasPrz != "" ? l.CzasPrz : p.PrzRzecz, out var ca);
        var przep = new Button { Content = "Przepisz czas", HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(8, 1, 8, 1) };
        Sp.Children.Add(przep);
        var o = Panel("Odjazd:", p.OdjPlan, l.CzasOdj != "" ? l.CzasOdj : p.OdjRzecz, out var co);
        przep.Click += (s, e) => o.Text = a.Text;
        var stoj = new Button { Content = l.Stoj ? "Odwołaj postój (pociąg STÓJ)" : "Wprowadź postój (pociąg STÓJ)", HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 4, 0, 0) };
        stoj.Click += (s, e) => { Close(); new OknoStoj(okno, post, p) { Owner = okno }.ShowDialog(); };
        Sp.Children.Add(stoj);
        Przyciski("Zapisz", () => okno.Wyslij(new EdrPolecenie { Rodzaj = "pociag", Pole = "czasy", Post = post, Klucz = p.Klucz, Wartosc = p.Nr,
            Lok = new EdrPociagLokalny { CzasPrz = a.Text, CzasOdj = o.Text, Przyczyna = string.Join("; ", new[] { ca.Text, co.Text }.Where(x => !string.IsNullOrWhiteSpace(x))) } }));
    }
}

/// <summary>Wprowadzanie gotowości pociągu do odjazdu (jak `TrainReady` w EDR.exe): godzina zgłoszenia, maszynista, analiza, TN.</summary>
public class OknoGotowosci : OknoEdr
{
    public OknoGotowosci(EdrWindow okno, string post, EdrPociag p) : base("Wprowadzanie gotowości pociągu do odjazdu", 480)
    {
        var l = okno.Lok(p);
        NaglowekPociagu.Dodaj(Sp, p);
        Sp.Children.Add(new TextBlock { Text = "Gotowość pociągu do odjazdu zgłoszono:", FontWeight = FontWeights.Bold, FontSize = 14, Foreground = Brushes.Navy });
        var g = NaglowekPociagu.Godz(okno, l.GotowoscZgl != "" ? l.GotowoscZgl : okno.Teraz, 90); g.FontSize = 14; Sp.Children.Add(g);
        Sp.Children.Add(new TextBlock { Text = "Nazwisko maszynisty:", FontWeight = FontWeights.Bold, FontSize = 14, Foreground = Brushes.Navy, Margin = new Thickness(0, 10, 0, 0) });
        var m = new TextBox { Text = l.Maszynista, FontSize = 14 }; Sp.Children.Add(m);
        var an = new Button { Content = "Wprowadź analizę pociągu", FontWeight = FontWeights.Bold, FontSize = 13, Padding = new Thickness(6), Margin = new Thickness(0, 12, 0, 0) };
        an.Click += (s, e) => new OknoAnalizy(okno, post, p) { Owner = this }.ShowDialog();
        Sp.Children.Add(an);
        Sp.Children.Add(new TextBlock { Text = l.TN ? "Pociąg przewozi towary niebezpieczne." : "Pociąg aktualnie nie przewozi towarów niebezp.", Foreground = Brushes.Green, Margin = new Thickness(0, 8, 0, 0) });
        var tn = new Button { Content = "TN", FontWeight = FontWeights.Bold, Width = 120, HorizontalAlignment = HorizontalAlignment.Left, Background = l.TN ? Brushes.OrangeRed : Brushes.Lime, Margin = new Thickness(0, 4, 0, 0) };
        tn.Click += (s, e) =>
        {
            if (MessageBox.Show(this, l.TN ? "Usunąć oznaczenie przewozu towarów niebezpiecznych?" : "Wprowadzić oznaczenie przewozu towarów niebezpiecznych?", "Towary niebezpieczne", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            var n = l.Kopia(); n.TN = !l.TN;
            var r = okno.Wyslij(new EdrPolecenie { Rodzaj = "pociag", Pole = "ladunek", Post = post, Klucz = p.Klucz, Wartosc = p.Nr, Lok = n });
            if (r.Ok) { l = okno.Lok(p); tn.Background = l.TN ? Brushes.OrangeRed : Brushes.Lime; } else Blad.Text = r.Msg;
        };
        Sp.Children.Add(tn);
        if (l.GotowoscWpis != "") Sp.Children.Add(Et($"Wpisano w EDR o {l.GotowoscWpis}."));
        Loaded += (a, e) => m.Focus();
        Przyciski("Zapisz", () => okno.Wyslij(new EdrPolecenie { Rodzaj = "pociag", Pole = "gotowosc", Post = post, Klucz = p.Klucz, Wartosc = p.Nr, Lok = new EdrPociagLokalny { GotowoscZgl = g.Text, Maszynista = m.Text } }));
    }
}

/// <summary>Wprowadzanie Analizy Pociągu (jak `TrainAnalysis` w EDR.exe): rzeczywiste brutto i długość, lokomotywy (seria, numer).</summary>
public class OknoAnalizy : OknoEdr
{
    public OknoAnalizy(EdrWindow okno, string post, EdrPociag p) : base("Wprowadzanie Analizy Pociągu", 560)
    {
        var l = okno.Lok(p);
        NaglowekPociagu.Dodaj(Sp, p);
        Sp.Children.Add(new TextBlock { Text = "Wprowadzanie danych o pociągu:", FontWeight = FontWeights.Bold, Foreground = Brushes.Navy });
        var gb = new GroupBox { Header = "Rzeczywiste brutto składu i długość pociągu", Margin = new Thickness(0, 6, 0, 6) };
        var g = new Grid { Margin = new Thickness(6) };
        foreach (var w in new[] { 140.0, 120, 200 }) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });
        TextBox Wiersz(int r, string et, string v, string plan)
        {
            g.RowDefinitions.Add(new RowDefinition());
            var e = new TextBlock { Text = et, VerticalAlignment = VerticalAlignment.Center }; Grid.SetRow(e, r); g.Children.Add(e);
            var t = new TextBox { Text = v, Margin = new Thickness(0, 2, 6, 2) }; Grid.SetRow(t, r); Grid.SetColumn(t, 1); g.Children.Add(t);
            var pl = new TextBlock { Text = "planowo: " + (plan != "" ? plan : "–"), Foreground = Brushes.DimGray, VerticalAlignment = VerticalAlignment.Center }; Grid.SetRow(pl, r); Grid.SetColumn(pl, 2); g.Children.Add(pl);
            return t;
        }
        var br = Wiersz(0, "Brutto składu [t]:", l.Brutto, ""); var dl = Wiersz(1, "Długość pociągu [m]:", l.Dlugosc, p.Dlugosc);
        gb.Content = g; Sp.Children.Add(gb);
        var gl = new GroupBox { Header = "Dane lokomotyw" };
        var g2 = new Grid { Margin = new Thickness(6) };
        foreach (var w in new[] { 120.0, 220, 140 }) g2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });
        g2.RowDefinitions.Add(new RowDefinition());
        foreach (var (t, c) in new[] { ("Seria", 1), ("Numer", 2) }) { var e = new TextBlock { Text = t, FontWeight = FontWeights.Bold, Foreground = Brushes.Purple }; Grid.SetColumn(e, c); g2.Children.Add(e); }
        var serie = new List<ComboBox>(); var numery = new List<TextBox>();
        for (int i = 0; i < 3; i++)
        {
            g2.RowDefinitions.Add(new RowDefinition());
            var e = new TextBlock { Text = $"Lokomotywa {i + 1}:", Foreground = Brushes.Maroon, VerticalAlignment = VerticalAlignment.Center }; Grid.SetRow(e, i + 1); g2.Children.Add(e);
            var s = new ComboBox { IsEditable = true, Text = i < l.Serie.Count ? l.Serie[i] : "", Margin = new Thickness(0, 2, 6, 2) };
            if (!string.IsNullOrEmpty(p.Pojazd)) s.Items.Add(p.Pojazd);
            Grid.SetRow(s, i + 1); Grid.SetColumn(s, 1); g2.Children.Add(s); serie.Add(s);
            var n = new TextBox { Text = i < l.NumeryLok.Count ? l.NumeryLok[i] : "", Margin = new Thickness(0, 2, 0, 2) }; Grid.SetRow(n, i + 1); Grid.SetColumn(n, 2); g2.Children.Add(n); numery.Add(n);
        }
        gl.Content = g2; Sp.Children.Add(gl);
        Przyciski("Zapisz", () => okno.Wyslij(new EdrPolecenie { Rodzaj = "pociag", Pole = "analiza", Post = post, Klucz = p.Klucz, Wartosc = p.Nr,
            Lok = new EdrPociagLokalny { Brutto = br.Text, Dlugosc = dl.Text, Serie = serie.Select(x => x.Text).ToList(), NumeryLok = numery.Select(x => x.Text).ToList() } }));
    }
}

/// <summary>Towary niebezpieczne i przesyłki (jak `TrainCargo` w EDR.exe): TN, TWR, przesyłka nadzwyczajna, opis / wagony / UN / RID.</summary>
public class OknoLadunku : OknoEdr
{
    public OknoLadunku(EdrWindow okno, string post, EdrPociag p) : base($"Towary niebezpieczne i przesyłki – {p.Nr}", 520)
    {
        var l = okno.Lok(p);
        var tn = new CheckBox { Content = "TN – Towary niebezpieczne", IsChecked = l.TN, Margin = new Thickness(0, 2, 0, 2) };
        var twr = new CheckBox { Content = "TWR – Towary wysokiego ryzyka", IsChecked = l.TWR, Margin = new Thickness(0, 2, 0, 2) };
        var pn = new CheckBox { Content = "Przesyłka nadzwyczajna", IsChecked = l.PN, Margin = new Thickness(0, 2, 0, 2) };
        Sp.Children.Add(tn); Sp.Children.Add(twr); Sp.Children.Add(pn);
        Sp.Children.Add(Et("Opis / wagony / UN / RID:"));
        var o = new TextBox { Text = l.Ladunek, Height = 120, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap }; Sp.Children.Add(o);
        Przyciski("Zapisz", () => { var n = l.Kopia(); n.TN = tn.IsChecked == true; n.TWR = twr.IsChecked == true; n.PN = pn.IsChecked == true; n.Ladunek = o.Text;
            return okno.Wyslij(new EdrPolecenie { Rodzaj = "pociag", Pole = "ladunek", Post = post, Klucz = p.Klucz, Wartosc = p.Nr, Lok = n }); });
    }
}

/// <summary>Lokalny opis pociągu (jak `EditProfile` w EDR.exe): pola opisu i pola wyboru.</summary>
public class OknoOpisuLok : OknoEdr
{
    public OknoOpisuLok(EdrWindow okno, string post, EdrPociag p) : base($"Lokalny opis pociągu {p.Nr}", 620)
    {
        var l = okno.Lok(p);
        var g = new Grid(); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) }); g.ColumnDefinitions.Add(new ColumnDefinition());
        var pola = new Dictionary<string, TextBox>();
        int r = 0;
        foreach (var (id, et) in Edr.PolaOpisu)
        {
            g.RowDefinitions.Add(new RowDefinition());
            var e = new TextBlock { Text = et + ":", VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold, Foreground = Brushes.Navy }; Grid.SetRow(e, r); g.Children.Add(e);
            var t = new TextBox { Text = l.Opis.GetValueOrDefault(id, ""), Margin = new Thickness(0, 1, 0, 1) }; Grid.SetRow(t, r); Grid.SetColumn(t, 1); g.Children.Add(t);
            pola[id] = t; r++;
        }
        Sp.Children.Add(g);
        var cb = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        CheckBox C(string t, bool v) { var c = new CheckBox { Content = t, IsChecked = v, Margin = new Thickness(0, 0, 16, 0) }; cb.Children.Add(c); return c; }
        var sk = C("Przekroczona skrajnia", l.Skrajnia); var na = C("Przekroczony nacisk", l.Nacisk); var za = C("Zamknięciowy", l.Zamknieciowy); var sl = C("Śledzony", l.Sledzony);
        Sp.Children.Add(cb);
        Przyciski("Zapisz", () => okno.Wyslij(new EdrPolecenie { Rodzaj = "pociag", Pole = "opis", Post = post, Klucz = p.Klucz, Wartosc = p.Nr,
            Lok = new EdrPociagLokalny { Opis = pola.ToDictionary(x => x.Key, x => x.Value.Text), Skrajnia = sk.IsChecked == true, Nacisk = na.IsChecked == true, Zamknieciowy = za.IsChecked == true, Sledzony = sl.IsChecked == true } }));
    }
}

/// <summary>Dane lokalne – wiersz tabeli zakładki (jak `EditDetailRow` w EDR.exe); zarządzenia RJ – także plik telegramu PDF.</summary>
public class OknoWierszaLok : OknoEdr
{
    public OknoWierszaLok(EdrWindow okno, string post, EdrPociag p, string tabela, EdrWierszLok w) : base("Dane lokalne – " + Edr.Tabele.First(x => x.Id == tabela).Tytul, 620)
    {
        var def = Edr.Tabele.First(x => x.Id == tabela);
        var n = w?.Kopia() ?? new EdrWierszLok { Rodzaj = tabela };
        Sp.Children.Add(new TextBlock { Text = $"Pociąg {p.Nr}", FontWeight = FontWeights.Bold });
        var g = new Grid(); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) }); g.ColumnDefinitions.Add(new ColumnDefinition());
        var pola = new List<TextBox>();
        for (int i = 0; i < def.Kolumny.Length; i++)
        {
            g.RowDefinitions.Add(new RowDefinition());
            var e = new TextBlock { Text = def.Kolumny[i], VerticalAlignment = VerticalAlignment.Center }; Grid.SetRow(e, i); g.Children.Add(e);
            var t = new TextBox { Text = i < n.Komorki.Count ? n.Komorki[i] : "", Margin = new Thickness(0, 1, 0, 1) }; Grid.SetRow(t, i); Grid.SetColumn(t, 1); g.Children.Add(t);
            pola.Add(t);
        }
        Sp.Children.Add(g);
        TextBox zal = null;
        if (tabela == "zarzadzenia")
        {
            Sp.Children.Add(Et("Plik telegramu PDF (ścieżka):"));
            var r = new DockPanel();
            var wyb = new Button { Content = "…", Width = 30 }; DockPanel.SetDock(wyb, Dock.Right); r.Children.Add(wyb);
            zal = new TextBox { Text = n.Zalacznik }; r.Children.Add(zal);
            wyb.Click += (s, e) => { var d = new Microsoft.Win32.OpenFileDialog { Filter = "Telegram PDF (*.pdf)|*.pdf" }; if (d.ShowDialog() == true) zal.Text = d.FileName; };
            Sp.Children.Add(r);
        }
        Przyciski("Zapisz", () =>
        {
            n.Komorki = pola.Select(x => x.Text).ToList(); n.Zalacznik = zal?.Text ?? "";
            if (n.Zalacznik != "" && !File.Exists(n.Zalacznik)) return new CmdResult(false, "Wskaż istniejący plik PDF telegramu.");
            return okno.Wyslij(new EdrPolecenie { Rodzaj = "pociag", Pole = "wiersz", Post = post, Klucz = p.Klucz, Wartosc = p.Nr, WierszLok = n });
        });
    }
}

/// <summary>Rejestracja usługi (jak `ServiceEdit` w EDR.exe).</summary>
public class OknoUslugi : OknoEdr
{
    public OknoUslugi(EdrWindow okno, string post, EdrUsluga u, string nr) : base("Rejestracja usługi", 520)
    {
        var n = u?.Kopia() ?? new EdrUsluga { Pociag = nr ?? "", Od = okno.Teraz };
        var g = new Grid(); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) }); g.ColumnDefinitions.Add(new ColumnDefinition());
        int r = 0;
        T Wiersz<T>(string et, T e) where T : FrameworkElement
        {
            g.RowDefinitions.Add(new RowDefinition());
            var l = new TextBlock { Text = et, VerticalAlignment = VerticalAlignment.Center }; Grid.SetRow(l, r); g.Children.Add(l);
            e.Margin = new Thickness(0, 2, 0, 2); Grid.SetRow(e, r); Grid.SetColumn(e, 1); g.Children.Add(e); r++;
            return e;
        }
        var rod = Wiersz("Rodzaj usługi", new ComboBox { ItemsSource = EdrUsluga.Rodzaje, SelectedItem = n.Rodzaj });
        var poc = Wiersz("Numer pociągu", new TextBox { Text = n.Pociag });
        var prz = Wiersz("Przewoźnik", new TextBox { Text = n.Przewoznik });
        var tor = Wiersz("Tor", new ComboBox { IsEditable = true, ItemsSource = okno.Dane?.ToryStacyjne.GetValueOrDefault(post) ?? new(), Text = n.Tor });
        var od = Wiersz("Rozpoczęcie", NaglowekPociagu.Godz(okno, n.Od));
        var dd = Wiersz("Zakończenie", NaglowekPociagu.Godz(okno, n.Do));
        Sp.Children.Add(g);
        Sp.Children.Add(Et("Uwagi:"));
        var uw = new TextBox { Text = n.Uwagi, Height = 60, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap }; Sp.Children.Add(uw);
        Przyciski("Zapisz", () =>
        {
            n.Rodzaj = rod.SelectedItem as string ?? "Manewry"; n.Pociag = poc.Text; n.Przewoznik = prz.Text; n.Tor = tor.Text; n.Od = od.Text; n.Do = dd.Text; n.Uwagi = uw.Text;
            return okno.Wyslij(new EdrPolecenie { Rodzaj = "usluga", Post = post, Usluga = n });
        });
    }
}

/// <summary>Pociąg lokalny w wykazie (jak „Dodaj / Edytuj dane lokalne…” w EDR.exe).</summary>
public class OknoPociaguLok : OknoEdr
{
    public OknoPociaguLok(EdrWindow okno, string post, EdrPociagLok x) : base(x == null ? "Dodaj dane lokalne" : "Edytuj dane lokalne", 480)
    {
        var n = x?.Kopia() ?? new EdrPociagLok();
        Sp.Children.Add(Et("Pociąg lokalny – tylko w wykazie EDR (nie jeździ w symulacji)."));
        var g = new Grid(); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) }); g.ColumnDefinitions.Add(new ColumnDefinition());
        var pola = new List<(string F, TextBox T)>();
        int r = 0;
        foreach (var (f, et) in new[] { ("Nr", "Numer"), ("Rodzaj", "Rodzaj"), ("Przewoznik", "Przewoźnik"), ("ZKierunku", "Z kierunku"), ("WKierunku", "W kierunku"), ("Tor", "Tor"), ("Przyjazd", "Przyjazd"), ("Odjazd", "Odjazd"), ("Uwagi", "Uwagi") })
        {
            g.RowDefinitions.Add(new RowDefinition());
            var l = new TextBlock { Text = et, VerticalAlignment = VerticalAlignment.Center }; Grid.SetRow(l, r); g.Children.Add(l);
            var v = (string)typeof(EdrPociagLok).GetField(f).GetValue(n);
            var t = f is "Przyjazd" or "Odjazd" ? NaglowekPociagu.Godz(okno, v, 80) : new TextBox { Text = v };
            t.Margin = new Thickness(0, 1, 0, 1); Grid.SetRow(t, r); Grid.SetColumn(t, 1); g.Children.Add(t); pola.Add((f, t)); r++;
        }
        Sp.Children.Add(g);
        Przyciski("Zapisz", () =>
        {
            foreach (var (f, t) in pola) typeof(EdrPociagLok).GetField(f).SetValue(n, t.Text);
            return okno.Wyslij(new EdrPolecenie { Rodzaj = "lokalny", Post = post, PociagLok = n });
        });
    }
}

/// <summary>Przyjęcie / zdanie dyżuru (jak „Przyjęcie / zdanie dyżuru…” w EDR.exe; E9b). Przed zdaniem – sytuacja bieżąca na szlakach do potwierdzenia.</summary>
public class OknoDyzuru : OknoEdr
{
    public OknoDyzuru(EdrWindow okno, string post) : base("Przyjęcie / zdanie dyżuru", 460)
    {
        var d = okno.Dane;
        var prow = d?.Dzienniki.Where(x => x.Post == post).Select(x => x.Prowadzi).FirstOrDefault(x => x != null);
        Sp.Children.Add(new TextBlock { Text = $"Posterunek: {okno.NazwaPost(post)}", FontWeight = FontWeights.Bold, FontSize = 13 });
        Sp.Children.Add(Et($"Prowadzi: {prow ?? "– (dyżur zdany)"}" + (d?.DyzurOd.GetValueOrDefault(post) is string od ? $", od {od}" : "")));
        Sp.Children.Add(Et($"Pracuje w EDR: {d?.Uzytkownik}{(d?.Operator != null ? $" (użytkownik lokalny, {d.OperatorRola})" : "")}"));
        Sp.Children.Add(Et("Godzina (nie późniejsza niż bieżąca):"));
        var g = NaglowekPociagu.Godz(okno, okno.Teraz); Sp.Children.Add(g);
        var bp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        var prz = new Button { Content = "Przyjmij dyżur", Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 8, 0), IsEnabled = d?.Obserwator == false };
        var zda = new Button { Content = "Zdaj dyżur…", Padding = new Thickness(10, 3, 10, 3), IsEnabled = prow != null && d?.Obserwator == false };
        prz.Click += (s, e) => { var r = okno.Wyslij(new EdrPolecenie { Rodzaj = "dyzur", Pole = "przyjmij", Post = post, Wartosc = g.Text }); if (r.Ok) Close(); else Blad.Text = r.Msg; };
        zda.Click += (s, e) =>
        {
            if (new OknoSytuacji(okno, post, true) { Owner = this }.ShowDialog() != true) return;
            var r = okno.Wyslij(new EdrPolecenie { Rodzaj = "dyzur", Pole = "zdaj", Post = post, Wartosc = g.Text }); if (r.Ok) Close(); else Blad.Text = r.Msg;
        };
        bp.Children.Add(prz); bp.Children.Add(zda);
        Sp.Children.Add(bp);
        if (d?.Obserwator == true) Sp.Children.Add(Et("TRYB OBSERWATORA – obserwator nie przyjmuje dyżuru."));
        Sp.Children.Add(Blad);
        Sp.Children.Add(new Button { Content = "Zamknij", Width = 100, HorizontalAlignment = HorizontalAlignment.Right, IsCancel = true, Margin = new Thickness(0, 6, 0, 0) });
    }
}

/// <summary>„Posterunek ruchu – sytuacja bieżąca” (jak `SituationForm` w EDR.exe): stan torów szlaków przyległych; przy zdaniu dyżuru – potwierdzenie.</summary>
public class OknoSytuacji : OknoEdr
{
    public OknoSytuacji(EdrWindow okno, string post, bool zdanie) : base("Posterunek ruchu - sytuacja bieżąca", 760)
    {
        Sp.Children.Add(new TextBlock { Text = $"Posterunek:  {okno.NazwaPost(post).ToUpperInvariant()}", FontWeight = FontWeights.Bold, FontSize = 14 });
        Sp.Children.Add(new TextBlock { Text = "Bieżąca sytuacja ruchowa na szlakach przyległych:", Margin = new Thickness(0, 6, 0, 4) });
        foreach (var d in okno.Dane?.Dzienniki.Where(x => x.Post == post) ?? Enumerable.Empty<EdrDziennik>())
        {
            var gb = new GroupBox { Header = $"Szlak do/z: {d.Sasiad}" + (d.Linia != "" ? $"     Linia: {d.Linia}" : "") + $"     ({d.Nazwa})", Margin = new Thickness(0, 2, 0, 4) };
            var g = new Grid { Margin = new Thickness(4) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) }); g.ColumnDefinitions.Add(new ColumnDefinition());
            int r = 0;
            foreach (var t in d.ToryStan)
            {
                var u = d.Biezace.Tory.FirstOrDefault(x => x.Szlak == t.Szlak);
                var stan = t.Status == "Zamknięty" ? "ZAMKNIĘTY" : t.Zajety ? "ZAJĘTY: " + string.Join(", ", t.Pociagi) : "WOLNY";
                g.RowDefinitions.Add(new RowDefinition());
                var a = new TextBlock { Text = $"Tor szlakowy {t.Tor}{(t.Strona != "" ? $" ({t.Strona})" : "")}", FontWeight = FontWeights.SemiBold }; Grid.SetRow(a, r); g.Children.Add(a);
                var b = new TextBlock { Text = $"status:  {stan}   ·   ruch {u?.Rodzaj?.ToLowerInvariant()}   ·   jak dla: {u?.Podstawa ?? "–"}" + (!string.IsNullOrEmpty(u?.Trakcja) ? $"   ·   trakcja {u.Trakcja.ToLowerInvariant()}" : ""),
                    Foreground = t.Status == "Zamknięty" || t.Zajety ? Brushes.Firebrick : Brushes.DarkGreen, TextWrapping = TextWrapping.Wrap };
                Grid.SetRow(b, r); Grid.SetColumn(b, 1); g.Children.Add(b); r++;
            }
            gb.Content = g; Sp.Children.Add(gb);
        }
        if (zdanie)
        {
            Sp.Children.Add(new TextBlock { Text = "UWAGA! Przed przekazaniem dyżuru należy upewnić się, że powyższa informacja jest zgodna ze stanem faktycznym. Jeżeli tak nie jest, należy kliknąć przycisk „Anuluj” i uzupełnić zapisy w dzienniku.",
                TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Firebrick, Margin = new Thickness(0, 6, 0, 0) });
            var bp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
            var ok = new Button { Content = "OK", Width = 100, Margin = new Thickness(4), IsDefault = true }; ok.Click += (s, e) => DialogResult = true;
            bp.Children.Add(ok); bp.Children.Add(new Button { Content = "Anuluj", Width = 100, Margin = new Thickness(4), IsCancel = true });
            Sp.Children.Add(bp);
        }
        else Sp.Children.Add(new Button { Content = "Zamknij", Width = 100, HorizontalAlignment = HorizontalAlignment.Right, IsCancel = true, Margin = new Thickness(0, 8, 0, 0) });
    }
}

/// <summary>Logowanie użytkownika lokalnego EDR (identyfikator i hasło lokalne).</summary>
public class OknoLogowania : OknoEdr
{
    public OknoLogowania(EdrWindow okno) : base("Logowanie do EDR – użytkownik lokalny", 380)
    {
        Sp.Children.Add(Et("Identyfikator:"));
        var id = new ComboBox { IsEditable = true, ItemsSource = okno.Dane?.Uzytkownicy.Select(x => x.Id).ToList() }; Sp.Children.Add(id);
        Sp.Children.Add(Et("Hasło lokalne:"));
        var h = new PasswordBox(); Sp.Children.Add(h);
        Loaded += (a, e) => id.Focus();
        Przyciski("Zaloguj", () => okno.Wyslij(new EdrPolecenie { Rodzaj = "zaloguj", Pole = id.Text, Wartosc = h.Password }));
    }
}

/// <summary>Użytkownicy lokalni EDR (Konfiguracja – koordynator): identyfikator, imię i nazwisko, rola, nowe hasło lokalne.</summary>
public class OknoUzytkownikow : OknoEdr
{
    public OknoUzytkownikow(EdrWindow okno) : base("Użytkownicy lokalni", 720)
    {
        var l = (okno.Dane?.Uzytkownicy ?? new()).Select(x => x.Kopia()).ToList();
        Sp.Children.Add(Et("Role: Dyżurny ruchu – prowadzi dzienniki; Obserwator – tylko odczyt (TRYB OBSERWATORA); Koordynator – także konfiguracja. Puste „Nowe hasło lokalne” – hasło bez zmian."));
        var tab = new StackPanel();
        Sp.Children.Add(tab);
        var pola = new List<(EdrUzytkownik U, TextBox Id, TextBox Naz, ComboBox Rola, PasswordBox H)>();
        void Zbierz() { foreach (var (u, i, n, r, h) in pola) { u.Id = i.Text; u.Nazwa = n.Text; u.Rola = r.SelectedItem as string; u.Haslo = h.Password; } }
        void Pokaz()
        {
            tab.Children.Clear(); pola.Clear();
            var nag = new Grid();
            foreach (var w in new[] { 120.0, 220, 150, 150, 60 }) nag.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });
            int c = 0; foreach (var t in new[] { "Identyfikator", "Imię i nazwisko", "Rola", "Nowe hasło lokalne", "" }) { var e = new TextBlock { Text = t, FontSize = 10.5, Foreground = Brushes.DimGray }; Grid.SetColumn(e, c++); nag.Children.Add(e); }
            tab.Children.Add(nag);
            foreach (var u in l)
            {
                var g = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                foreach (var w in new[] { 120.0, 220, 150, 150, 60 }) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });
                var i = new TextBox { Text = u.Id, Margin = new Thickness(0, 0, 4, 0) }; var n = new TextBox { Text = u.Nazwa, Margin = new Thickness(0, 0, 4, 0) };
                var r = new ComboBox { ItemsSource = EdrUzytkownik.Role, SelectedItem = u.Rola, Margin = new Thickness(0, 0, 4, 0) };
                var h = new PasswordBox { Margin = new Thickness(0, 0, 4, 0), ToolTip = u.MaHaslo ? "Hasło ustawione – puste: bez zmian" : "Podaj hasło" };
                var us = new Button { Content = "Usuń" }; var uu = u; us.Click += (s, e) => { Zbierz(); l.Remove(uu); Pokaz(); };
                foreach (var (e, k) in new (FrameworkElement, int)[] { (i, 0), (n, 1), (r, 2), (h, 3), (us, 4) }) { Grid.SetColumn(e, k); g.Children.Add(e); }
                tab.Children.Add(g); pola.Add((u, i, n, r, h));
            }
            if (l.Count == 0) tab.Children.Add(new TextBlock { Text = "(brak użytkowników lokalnych – w EDR pracuje użytkownik zalogowany do symulatora)", Foreground = Brushes.DimGray });
        }
        Pokaz();
        var dod = new Button { Content = "Dodaj użytkownika", HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(0, 6, 0, 0) };
        dod.Click += (s, e) => { Zbierz(); l.Add(new EdrUzytkownik()); Pokaz(); };
        Sp.Children.Add(dod);
        Przyciski("Zapisz", () => { Zbierz(); return okno.Wyslij(new EdrPolecenie { Rodzaj = "uzytkownicy", Uzytkownicy = l }); });
    }
}

/// <summary>„Historia zmian” (jak w EDR.exe): zmiany w EDR – data i godzina, użytkownik, czynność, czego dotyczy, opis; filtr – wpis.</summary>
public class OknoHistorii : Window
{
    public OknoHistorii(EdrWindow okno, string filtr)
    {
        Title = "Historia zmian lokalnych"; Width = 1050; Height = 620; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = EdrWindow.Tlo;
        var g = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, HeadersVisibility = DataGridHeadersVisibility.Column, FontSize = 12, Background = Brushes.White, SelectionMode = DataGridSelectionMode.Single };
        foreach (var (h, p, w) in new[] { ("Data i godzina", nameof(EdrZdarzenie.Czas), 160.0), ("Użytkownik", nameof(EdrZdarzenie.Kto), 140), ("Czynność", nameof(EdrZdarzenie.Czynnosc), 240), ("Identyfikator wpisu", nameof(EdrZdarzenie.Obiekt), 220), ("Opis", nameof(EdrZdarzenie.Opis), 400) })
            g.Columns.Add(new DataGridTextColumn { Header = h, Binding = new Binding(p), Width = new DataGridLength(w) });
        g.ItemsSource = (okno.Dane?.Historia ?? new()).Where(x => filtr == null || x.Obiekt.Contains(filtr) || x.Opis.Contains(filtr)).Reverse().ToList();
        var t = new TextBox { IsReadOnly = true, FontFamily = new FontFamily("Consolas"), TextWrapping = TextWrapping.Wrap, Height = 120 };
        g.SelectionChanged += (a, e) => t.Text = g.SelectedItem is EdrZdarzenie z ? $"{z.Czas}  {z.Kto}\n{z.Czynnosc} – {z.Obiekt}\n{z.Opis}" : "";
        var d = new DockPanel(); DockPanel.SetDock(t, Dock.Bottom); d.Children.Add(t); d.Children.Add(g);
        Content = d;
    }
}

/// <summary>„Historia wpisu” (E9b): wszystkie wersje wiersza dziennika – kiedy, kto, które pole, wartości kolumn.</summary>
public class OknoHistoriiWpisu : Window
{
    public OknoHistoriiWpisu(EdrDziennik d, EdrWiersz w)
    {
        Title = $"Historia wpisu – {d.Nazwa}"; Width = 1100; Height = 360; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = EdrWindow.Tlo;
        var g = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, HeadersVisibility = DataGridHeadersVisibility.Column, FontSize = 12, Background = Brushes.White };
        var dr = d.Biezace.Droznicy.ToDictionary(x => x.Id, x => x.Oznaczenie);
        var l = w.Wersje.Select((v, i) => new
        {
            Lp = i + 1, v.Zmiana, v.Kto, Pole = v.Pole == null ? "zapis" : Edr.NazwaPola(v.Pole), v.Nr, v.Tor, v.Dw, v.Odj, v.Prz, v.Uwagi, v.Zad, Data = v.DataWpisu, Lewy = v.Lewy ? "tak" : "",
            Droznicy = string.Join(", ", (v.Dr ?? new()).Select(x => $"{dr.GetValueOrDefault(x.Key, x.Key)}: {x.Value}")), Wykreslono = v.Wykreslono ?? ""
        }).ToList();
        foreach (var (h, p) in new[] { ("Lp.", "Lp"), ("Data i godzina zmiany", "Zmiana"), ("Kto", "Kto"), ("Pole", "Pole"), ("Nr pociągu", "Nr"), ("Tor", "Tor"), ("Droga wolna", "Dw"), ("Odjechał", "Odj"), ("Przyjechał", "Prz"),
            ("Uwagi", "Uwagi"), ("Żądanie pozwolenia", "Zad"), ("Data wpisu", "Data"), ("Po torze lewym", "Lewy"), ("Dróżnicy", "Droznicy"), ("Wykreślono", "Wykreslono") })
            g.Columns.Add(new DataGridTextColumn { Header = h, Binding = new Binding(p) });
        g.ItemsSource = l;
        Content = g;
    }
}

/// <summary>Dodatkowe pola wpisu (jak w EDR.exe – PPM na numerze pociągu): żądanie pozwolenia i data wpisu.</summary>
public class OknoPolDodatkowych : OknoEdr
{
    public OknoPolDodatkowych(EdrWindow okno, EdrDziennik d, EdrWiersz w) : base("Dodatkowe pola wpisu", 400)
    {
        Sp.Children.Add(new TextBlock { Text = $"Pociąg {w.W.Nr} – {d.Nazwa}", FontWeight = FontWeights.Bold });
        Sp.Children.Add(Et("Żądanie pozwolenia (godzina):"));
        var z = NaglowekPociagu.Godz(okno, w.W.Zad); Sp.Children.Add(z);
        Sp.Children.Add(Et("Data wpisu (dd-MM-rrrr, puste – data strony):"));
        var dt = new TextBox { Text = w.W.DataWpisu, Width = 110, HorizontalAlignment = HorizontalAlignment.Left }; Sp.Children.Add(dt);
        Przyciski("Zapisz", () =>
        {
            foreach (var (pole, v) in new[] { ("Zad", z.Text), ("Data", dt.Text) })
            {
                if (Edr.Wartosc(w.W, pole) == v.Trim()) continue;
                var r = okno.Wyslij(new EdrPolecenie { Dziennik = d.Id, Rodzaj = "pole", Wiersz = w.Id, Pole = pole, Wartosc = v });
                if (!r.Ok) return r;
            }
            return new CmdResult(true, "");
        });
    }
}

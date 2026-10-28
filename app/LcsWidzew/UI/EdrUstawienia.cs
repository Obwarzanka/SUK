using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using LcsWidzew.Sim;

namespace LcsWidzew.UI;

/// <summary>
/// Okno ustawień dziennika ruchu (klatka filmu ISDR f1_01 „Ustawienia bieżące dziennika ruchu”, podręcznik EDR rozdz. 5.2.1):
/// szlak, rodzaj linii, kierunek parzysty / nieparzysty, opcje wypełniania (pola wymagane), tory szlakowe z zasadniczym kierunkiem ruchu.
/// Zasadnicze (menu Konfiguracja) – do zmiany i zapisu; bieżące (przycisk z kołem zębatym) – tylko do odczytu.
/// </summary>
public class UstawieniaDziennika : Window
{
    static readonly string[] Kierunki = { "Nieustalony", "Z posterunku sąsiedniego", "DO posterunku sąsiedniego" };
    static int Indeks(int k) => k < 0 ? 1 : k > 0 ? 2 : 0;
    static int Kierunek(int i) => i == 1 ? -1 : i == 2 ? 1 : 0;

    public UstawieniaDziennika(EdrWindow okno, EdrDziennik d, string nazwaPosterunku, bool biezace)
    {
        var u = (biezace ? d.Biezace : d.Ustawienia).Kopia();
        Title = $"{nazwaPosterunku.ToUpperInvariant()} – Ustawienia {(biezace ? "bieżące" : "zasadnicze")} dziennika ruchu";
        Width = 940; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EdrWindow.Pedzel(0xE9, 0xE9, 0xE9); FontFamily = new FontFamily("Segoe UI"); FontSize = 12; ShowInTaskbar = false;
        var sp = new StackPanel { Margin = new Thickness(14) };
        TextBlock Et(string t) => new() { Text = t, Foreground = Brushes.DimGray, FontSize = 11, Margin = new Thickness(0, 6, 0, 0) };
        TextBlock War(string t) => new() { Text = t, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 1, 0, 0) };
        ComboBox Lista(IEnumerable<string> el, int wyb)
        {
            var c = new ComboBox { Width = 230, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 1, 0, 0), IsEnabled = !biezace };
            foreach (var x in el) c.Items.Add(x);
            c.SelectedIndex = Math.Max(0, wyb); return c;
        }

        var gora = new Grid();
        gora.ColumnDefinitions.Add(new ColumnDefinition()); gora.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
        var l = new StackPanel(); var p = new StackPanel(); Grid.SetColumn(p, 1); gora.Children.Add(l); gora.Children.Add(p);
        var dz = new StackPanel { Orientation = Orientation.Horizontal };
        dz.Children.Add(new TextBlock { Text = "Dziennik ruchu:  ", Foreground = Brushes.DimGray }); dz.Children.Add(War(d.Nazwa));
        l.Children.Add(dz);
        l.Children.Add(Et("Szlak (ustawienia)"));
        l.Children.Add(Et("Linia kolejowa (szlakowa)")); l.Children.Add(War($"{(d.Linia != "" ? d.Linia + ": " : "")}{nazwaPosterunku.ToUpperInvariant()} – {d.Sasiad.ToUpperInvariant()}"));
        l.Children.Add(Et("Początek szlaku (posterunek początku szlaku)")); l.Children.Add(War(nazwaPosterunku.ToUpperInvariant()));
        l.Children.Add(Et("Koniec szlaku (posterunek końca szlaku)")); l.Children.Add(War(d.Sasiad.ToUpperInvariant()));
        p.Children.Add(Et("Rodzaj linii na szlaku")); p.Children.Add(War(u.Tory.Count switch { 1 => "Jednotorowa", 2 => "Dwutorowa", _ => "Wielotorowa" }));
        var niep = War("");
        p.Children.Add(Et("Kierunek nieparzysty")); p.Children.Add(niep);
        p.Children.Add(Et("Kierunek parzysty"));
        var parz = Lista(Kierunki, Indeks(u.Parzysty)); p.Children.Add(parz);
        void Niep() => niep.Text = Kierunki[Indeks(-Kierunek(parz.SelectedIndex))];
        parz.SelectionChanged += (a, e) => Niep(); Niep();
        sp.Children.Add(gora);

        sp.Children.Add(new TextBlock { Text = "Opcje wypełniania dziennika ruchu", Foreground = Brushes.DimGray, FontSize = 11, Margin = new Thickness(0, 14, 0, 2) });
        var opcje = new WrapPanel();
        CheckBox Op(string t, bool v) { var c = new CheckBox { Content = t, IsChecked = v, Margin = new Thickness(0, 0, 40, 0), IsEnabled = !biezace }; opcje.Children.Add(c); return c; }
        var wTor = Op("„Tor stacyjny” wymagany", u.WymTor); var wDw = Op("„Droga wolna” wymagana", u.WymDw);
        var wOdj = Op("„Pociąg odjechał” wymagane", u.WymOdj); var wPrz = Op("„Pociąg przyjechał” wymagane", u.WymPrz);
        sp.Children.Add(opcje);

        var tory = new UniformGrid { Columns = Math.Max(1, u.Tory.Count), Margin = new Thickness(0, 12, 0, 0) };
        var pola = new List<(EdrTorUstawienia T, ComboBox Strona, ComboBox Status, ComboBox Rodzaj, ComboBox Zas, ComboBox Pod)>();
        foreach (var t in u.Tory)
        {
            var g = new StackPanel { Margin = new Thickness(4) };
            var naz = new StackPanel { Orientation = Orientation.Horizontal };
            naz.Children.Add(new TextBlock { Text = "Numer toru szlakowego:  ", Foreground = Brushes.DimGray }); naz.Children.Add(War(t.Tor == "" ? "–" : t.Tor));
            naz.Children.Add(new TextBlock { Text = $"   (szlak {t.Szlak})", Foreground = Brushes.DimGray, FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
            g.Children.Add(naz);
            ComboBox str = null;
            if (u.Tory.Count > 1) { g.Children.Add(Et("Tor względem posterunku")); str = Lista(new[] { "", "lewy", "prawy" }, Array.IndexOf(new[] { "", "lewy", "prawy" }, t.Strona ?? "")); g.Children.Add(str); }
            g.Children.Add(Et(biezace ? "Bieżący status toru" : "Status toru")); var sta = Lista(new[] { "Otwarty", "Zamknięty" }, t.Status == "Zamknięty" ? 1 : 0); g.Children.Add(sta);
            g.Children.Add(Et(biezace ? "Bieżący rodzaj ruchu" : "Zasadniczy rodzaj ruchu")); var rod = Lista(new[] { "Jednokierunkowy", "Dwukierunkowy" }, t.Rodzaj == "Jednokierunkowy" ? 0 : 1); g.Children.Add(rod);
            g.Children.Add(Et("Zasadniczy kierunek ruchu")); var zas = Lista(Kierunki, Indeks(t.Zasadniczy)); g.Children.Add(zas);
            g.Children.Add(Et("Rodzaj urządzenia SRK")); g.Children.Add(new TextBlock { Text = t.Urzadzenie ?? "", FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, MaxWidth = 380, HorizontalAlignment = HorizontalAlignment.Left });
            g.Children.Add(Et("Prowadzenie dziennika ruchu jak dla")); var pod = Lista(Edr.Podstawy, Array.IndexOf(Edr.Podstawy, t.Podstawa)); g.Children.Add(pod);
            pola.Add((t, str, sta, rod, zas, pod));
            tory.Children.Add(new GroupBox { Header = $"Tor szlakowy{(t.Strona != "" ? " " + t.Strona : "")}", Content = g, Margin = new Thickness(0, 0, 8, 0) });
        }
        sp.Children.Add(tory);

        // posterunki dróżników przejazdowych (podręcznik EDR, Rys. 24) – kolumna 8 dziennika
        var drg = new Grid { Margin = new Thickness(4) };
        var drPola = new List<(EdrDroznik X, TextBox Km, TextBox Nr, TextBox Naz, TextBox Ozn, ComboBox Wg, ComboBox Dla, CheckBox Wd)>();
        string[] wgSz = { "Jednotorowych", "Dwu i więcej torowych" }, dla = { "W obu kierunkach", "Z posterunku sąsiedniego", "DO posterunku sąsiedniego" };
        void Dr()
        {
            drg.Children.Clear(); drg.RowDefinitions.Clear(); drg.ColumnDefinitions.Clear(); drPola.Clear();
            foreach (var w in new[] { 70.0, 60, 120, 90, 150, 170, 60, 60 }) drg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });
            drg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            int c = 0;
            foreach (var h in new[] { "Km na linii", "Numer na linii", "Nazwa", "Oznaczenie w dzienniku ruchu", "Powiadamianie – według szlaku", "Dla pociągów", "W dzienniku ruchu", "" })
            { var t = new TextBlock { Text = h, FontSize = 10.5, Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap }; Grid.SetColumn(t, c++); drg.Children.Add(t); }
            int r = 0;
            foreach (var x in u.Droznicy)
            {
                r++; drg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                void K(int col, FrameworkElement e) { e.Margin = new Thickness(0, 1, 4, 1); e.IsEnabled = !biezace; Grid.SetRow(e, r); Grid.SetColumn(e, col); drg.Children.Add(e); }
                var km = new TextBox { Text = x.Km }; var nr = new TextBox { Text = x.Numer }; var naz = new TextBox { Text = x.Nazwa }; var ozn = new TextBox { Text = x.Oznaczenie };
                var wg = new ComboBox { FontSize = 11 }; foreach (var y in wgSz) wg.Items.Add(y); wg.SelectedItem = x.WgSzlaku;
                var dl = new ComboBox { FontSize = 11 }; foreach (var y in dla) dl.Items.Add(y); dl.SelectedItem = x.DlaPociagow;
                var wd = new CheckBox { IsChecked = x.WDzienniku, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                K(0, km); K(1, nr); K(2, naz); K(3, ozn); K(4, wg); K(5, dl); K(6, wd);
                var us = new Button { Content = "Usuń", FontSize = 11 }; var xx = x; us.Click += (a, e) => { ZbierzDr(); u.Droznicy.Remove(xx); Dr(); }; K(7, us);
                drPola.Add((x, km, nr, naz, ozn, wg, dl, wd));
            }
        }
        void ZbierzDr()
        {
            foreach (var (x, km, nr, naz, ozn, wg, dl, wd) in drPola)
            { x.Km = km.Text.Trim(); x.Numer = nr.Text.Trim(); x.Nazwa = naz.Text.Trim(); x.Oznaczenie = ozn.Text.Trim(); x.WgSzlaku = wg.SelectedItem as string ?? wgSz[0]; x.DlaPociagow = dl.SelectedItem as string ?? dla[0]; x.WDzienniku = wd.IsChecked == true; }
        }
        Dr();
        var drp = new StackPanel(); drp.Children.Add(drg);
        if (!biezace)
        {
            var nowy = new Button { Content = "Utwórz nowy", HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(4, 2, 0, 2) };
            nowy.Click += (a, e) => { ZbierzDr(); u.Droznicy.Add(new EdrDroznik { WgSzlaku = u.Tory.Count > 1 ? "Dwu i więcej torowych" : "Jednotorowych" }); Dr(); };
            drp.Children.Add(nowy);
        }
        sp.Children.Add(new GroupBox { Header = "Posterunki dróżników przejazdowych (kolumna 8 dziennika – o jeździe pociągu zawiadomiono dróżników)", Content = drp, Margin = new Thickness(0, 8, 0, 0) });

        var blad = new TextBlock { Foreground = Brushes.Firebrick, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };
        sp.Children.Add(blad);
        if (biezace) sp.Children.Add(new TextBlock { Text = "Stan bieżący: ustawienia zasadnicze zmienione telefonogramami (przycisk telefonogramu nad dziennikiem). Ustawienia zasadnicze: menu Konfiguracja albo PPM na nazwie dziennika.", Foreground = Brushes.DimGray, FontSize = 11, TextWrapping = TextWrapping.Wrap });
        var bp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        if (!biezace)
        {
            var zap = new Button { Content = "Zapisz", Width = 100, Margin = new Thickness(4), IsDefault = true };
            zap.Click += (a, e) =>
            {
                u.Parzysty = Kierunek(parz.SelectedIndex);
                ZbierzDr();
                u.WymTor = wTor.IsChecked == true; u.WymDw = wDw.IsChecked == true; u.WymOdj = wOdj.IsChecked == true; u.WymPrz = wPrz.IsChecked == true;
                foreach (var (t, str, sta, rod, zas, pod) in pola)
                {
                    if (str != null) t.Strona = str.SelectedItem as string ?? "";
                    t.Status = sta.SelectedItem as string; t.Rodzaj = rod.SelectedItem as string; t.Zasadniczy = Kierunek(zas.SelectedIndex); t.Podstawa = pod.SelectedItem as string;
                }
                var r = okno.Wyslij(new EdrPolecenie { Dziennik = d.Id, Rodzaj = "ustawienia", Ustawienia = u });
                if (r.Ok) Close(); else blad.Text = r.Msg;
            };
            bp.Children.Add(zap);
        }
        bp.Children.Add(new Button { Content = biezace ? "Zamknij" : "Anuluj", Width = 100, Margin = new Thickness(4), IsCancel = true });
        sp.Children.Add(bp);
        Content = sp;
    }
}

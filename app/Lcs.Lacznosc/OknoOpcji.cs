using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Lcs.Lacznosc;

/// <summary>Opcje EZP (klasyczne okno dialogowe): awizacja automatyczna, zgłoszenia z gry, posterunki objęte automatami.</summary>
public sealed class OknoOpcji : Window
{
    /// <param name="strony">sąsiad → true: po lewej stronie schematu (stan obecny); po OK – wybór użytkownika.</param>
    public OknoOpcji(Ustawienia u, IEnumerable<string> sasiedzi, Dictionary<string, bool> strony = null)
    {
        Title = "Opcje EZP"; SizeToContent = SizeToContent.WidthAndHeight; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Wyglad.P("#D4D0C8"); FontFamily = new FontFamily("Tahoma"); FontSize = 12;

        var st = new StackPanel { Margin = new Thickness(12), Width = 520 };
        CheckBox Ch(string t, bool v) => new() { Content = new TextBlock { Text = t, TextWrapping = TextWrapping.Wrap }, IsChecked = v, Margin = new Thickness(0, 3, 0, 3) };
        GroupBox Grupa(string t, UIElement c) => new() { Header = t, Content = c, Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(0, 0, 0, 8) };

        // ---- awizacja
        var aw = new StackPanel();
        var cbAw = Ch("Automatyczna awizacja – program sam pyta następny posterunek o drogę wolną", u.Awizacja);
        aw.Children.Add(cbAw);
        var rSzl = new RadioButton { Content = "zaraz po zajęciu szlaku – pociąg wjechał na szlak dojazdowy do naszej stacji (z gry)", IsChecked = u.AwizacjaTryb != "min", Margin = new Thickness(20, 4, 0, 2) };
        var rMin = new RadioButton { IsChecked = u.AwizacjaTryb == "min", Margin = new Thickness(20, 2, 0, 2) };
        var min = new TextBox { Text = u.AwizacjaMin.ToString(), Width = 36, Margin = new Thickness(4, 0, 4, 0), TextAlignment = TextAlignment.Center };
        min.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
        var rMinTr = new StackPanel { Orientation = Orientation.Horizontal };
        rMinTr.Children.Add(new TextBlock { Text = "na" }); rMinTr.Children.Add(min); rMinTr.Children.Add(new TextBlock { Text = "min przed odjazdem wg rozkładu (pociąg musi być już w obszarze stacji)" });
        rMin.Content = rMinTr;
        aw.Children.Add(rSzl); aw.Children.Add(rMin);
        var cbTor = Ch("bez toru z rozmowy – pierwszy tor szlaku (zasadniczy)", u.TorDomyslny);
        cbTor.Margin = new Thickness(20, 6, 0, 2);
        aw.Children.Add(cbTor);
        st.Children.Add(Grupa("Awizacja", aw));

        // ---- zgłoszenia z gry
        var zg = new StackPanel();
        var cbOdj = Ch("Sam zgłasza „odjechał”, gdy pociąg opuści stację na szlak", u.AutoOdjazd);
        var cbPrz = Ch("Sam zgłasza „przyjechał” posterunkowi, z którego przyjechał pociąg (okienko i tak gaśnie samo)", u.AutoPrzyjazd);
        var cbOdRazu = Ch("Przyciski odpowiedzi w EZP wysyłają od razu (bez „Wyślij”)", u.OdRazu);
        zg.Children.Add(cbOdj); zg.Children.Add(cbPrz); zg.Children.Add(cbOdRazu);
        st.Children.Add(Grupa("Zgłoszenia", zg));

        // ---- posterunki
        var lista = new StackPanel();
        var pola = new List<CheckBox>();
        foreach (var s in sasiedzi.Distinct())
        {
            var c = new CheckBox { Content = s, IsChecked = u.Automat(s), Margin = new Thickness(2) };
            pola.Add(c); lista.Children.Add(c);
        }
        if (pola.Count == 0) lista.Children.Add(new TextBlock { Text = "brak posterunków sąsiednich (połącz z grą albo wybierz posterunek)", Foreground = Brushes.DimGray });
        var przyciski = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        Button B(string t, Action a) { var b = new Button { Content = t, Padding = new Thickness(10, 1, 10, 1), Margin = new Thickness(0, 0, 6, 0) }; b.Click += (_, _) => a(); return b; }
        przyciski.Children.Add(B("Wszystkie", () => pola.ForEach(p => p.IsChecked = true)));
        przyciski.Children.Add(B("Żaden", () => pola.ForEach(p => p.IsChecked = false)));
        var pp = new StackPanel(); pp.Children.Add(new Border { Child = lista, Background = Brushes.White, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Padding = new Thickness(4) }); pp.Children.Add(przyciski);
        st.Children.Add(Grupa("Automaty działają dla posterunków", pp));

        // ---- strona na schemacie
        var combo = new Dictionary<string, ComboBox>();
        if (strony != null && strony.Count > 0)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            int r = 0;
            foreach (var (s, lewa) in strony)
            {
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var t = new TextBlock { Text = s, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 2, 10, 2) };
                var c = new ComboBox { ItemsSource = new[] { "◄ po lewej", "po prawej ►" }, SelectedIndex = lewa ? 0 : 1, Width = 120, Margin = new Thickness(2) };
                Grid.SetRow(t, r); Grid.SetRow(c, r); Grid.SetColumn(c, 1);
                g.Children.Add(t); g.Children.Add(c);
                combo[s] = c; r++;
            }
            var sp = new StackPanel();
            sp.Children.Add(new Border { Child = g, Background = Brushes.White, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Padding = new Thickness(4) });
            sp.Children.Add(new TextBlock { Text = "Szybciej: prawy przycisk na posterunku na schemacie.", Foreground = Brushes.DimGray, Margin = new Thickness(0, 4, 0, 0) });
            st.Children.Add(Grupa("Po której stronie stacji wyświetlać posterunek", sp));
        }

        var dol = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = B("OK", () =>
        {
            u.Awizacja = cbAw.IsChecked == true; u.AwizacjaTryb = rMin.IsChecked == true ? "min" : "szlak";
            u.AwizacjaMin = int.TryParse(min.Text, out var m) ? Math.Clamp(m, 1, 60) : 5;
            u.TorDomyslny = cbTor.IsChecked == true; u.AutoOdjazd = cbOdj.IsChecked == true; u.AutoPrzyjazd = cbPrz.IsChecked == true; u.OdRazu = cbOdRazu.IsChecked == true;
            foreach (var c in pola)
            {
                var n = (string)c.Content;
                u.BezAutomatow.RemoveAll(x => string.Equals(x, n, StringComparison.OrdinalIgnoreCase));
                if (c.IsChecked != true) u.BezAutomatow.Add(n);
            }
            foreach (var (s, c) in combo) strony[s] = c.SelectedIndex == 0;
            DialogResult = true;
        });
        ok.IsDefault = true;
        var anuluj = B("Anuluj", () => DialogResult = false); anuluj.IsCancel = true;
        dol.Children.Add(ok); dol.Children.Add(anuluj);
        st.Children.Add(dol);
        Content = st;
    }
}

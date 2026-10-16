using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Lcs.Przechylka;

/// <summary>
/// Okno kalkulatora: dane (V, R, h – puste: propozycja), wartości dopuszczalne (przykładowe, do zmiany), wyniki na żywo
/// (h_r, niedobór, a_n, trzy warunki na L z zaznaczonym decydującym, L, V największa dla R i h), uwagi i wzory.
/// </summary>
public class MainWindow : Window
{
    public readonly TextBox VBox = Box("100", "Prędkość V [km/h]"), RBox = Box("600", "Promień łuku R [m]"), HBox = Box("", "Przechyłka zastosowana h [mm] – puste: propozycja programu");
    readonly TextBox hMax = Box("150", "Największa przechyłka [mm]"), hnMax = Box("110", "Największy niedobór przechyłki [mm]"),
        psi = Box("0,5", "Szybkość przyrostu przyspieszenia niezrównoważonego ψ [m/s³]"), f = Box("35", "Szybkość zmiany przechyłki f [mm/s]"),
        nk = Box("10", "Pochylenie rampy przechyłkowej 1:n, n = k·V – współczynnik k"), rnd = Box("5", "Zaokrąglenie długości w górę [m]");
    public readonly TextBlock HrText = Res(), HText = Res(), HnText = Res(), AnText = Res(), L1Text = Res(), L2Text = Res(), L3Text = Res(), LText = Res(), VmaxText = Res();
    readonly TextBlock uwagi = new() { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    static readonly CultureInfo Pl = CultureInfo.GetCultureInfo("pl-PL");

    static TextBox Box(string t, string tip) => new() { Text = t, Width = 80, ToolTip = tip, Margin = new Thickness(0, 2, 0, 2), HorizontalContentAlignment = HorizontalAlignment.Right };
    static TextBlock Res() => new() { Margin = new Thickness(0, 3, 0, 3), FontWeight = FontWeights.SemiBold };

    public MainWindow()
    {
        Title = "Kalkulator przechyłki i krzywej przejściowej"; Width = 900; Height = 640;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 14; Background = new SolidColorBrush(Color.FromRgb(0xf6, 0xf6, 0xf3));
        var root = new StackPanel { Margin = new Thickness(18) };
        root.Children.Add(new TextBlock { Text = "Przechyłka i krzywa przejściowa", FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) });

        var cols = new Grid();
        cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = new StackPanel();
        left.Children.Add(Group("Dane", ("Prędkość V [km/h]", VBox), ("Promień R [m]", RBox), ("Przechyłka h [mm] (puste – propozycja)", HBox)));
        left.Children.Add(Group("Wartości dopuszczalne (przykładowe – sprawdź w przepisach)",
            ("największa przechyłka [mm]", hMax), ("największy niedobór [mm]", hnMax), ("ψ [m/s³]", psi), ("f [mm/s]", f), ("rampa 1:n, n = k·V, k =", nk), ("zaokrąglenie L [m]", rnd)));
        Grid.SetColumn(left, 0); cols.Children.Add(left);

        var right = new StackPanel();
        right.Children.Add(Group("Wynik", ("przechyłka równoważąca h_r", HrText), ("przechyłka h", HText), ("niedobór h_n = h_r − h", HnText), ("przyspieszenie niezrównoważone a_n", AnText),
            ("L z rampy przechyłkowej", L1Text), ("L z przyrostu przyspieszenia ψ", L2Text), ("L ze zmiany przechyłki f", L3Text), ("długość krzywej przejściowej L", LText), ("V największa dla tego R i h", VmaxText)));
        right.Children.Add(uwagi);
        Grid.SetColumn(right, 2); cols.Children.Add(right);
        root.Children.Add(cols);

        root.Children.Add(new TextBlock
        {
            Text = "Wzory (rozstaw osi szyn 1500 mm): h_r = 11,8·V²/R;  h_n = h_r − h;  a_n = 9,81·h_n / 1500;  L = max(n·h / 1000 przy n = k·V;  V·|a_n| / (3,6·ψ);  V·h / (3,6·f)).  " +
                   "Bez h program proponuje najmniejszą przechyłkę (co 5 mm), przy której niedobór nie przekracza dopuszczalnego. Wartości dopuszczalne są przykładowe – wpisz obowiązujące z przepisów (warunki techniczne, Id-1).",
            TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, FontSize = 12.5, Margin = new Thickness(0, 14, 0, 0),
        });
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        foreach (var b in new[] { VBox, RBox, HBox, hMax, hnMax, psi, f, nk, rnd }) b.TextChanged += (s, e) => Licz();
        Licz();
    }

    static GroupBox Group(string title, params (string Label, FrameworkElement El)[] rows)
    {
        var g = new Grid { Margin = new Thickness(6) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        for (int i = 0; i < rows.Length; i++)
        {
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = new TextBlock { Text = rows[i].Label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0), TextWrapping = TextWrapping.Wrap };
            Grid.SetRow(l, i); g.Children.Add(l);
            rows[i].El.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetRow(rows[i].El, i); Grid.SetColumn(rows[i].El, 1); g.Children.Add(rows[i].El);
        }
        return new GroupBox { Header = new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap }, Content = g, Margin = new Thickness(0, 0, 0, 10) };
    }

    static double? Num(TextBox b) => double.TryParse(b.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    static string F(double v, string fmt = "0.#") => v.ToString(fmt, Pl);

    void Licz()
    {
        var all = new[] { HrText, HText, HnText, AnText, L1Text, L2Text, L3Text, LText, VmaxText };
        if (Num(VBox) is not double v || Num(RBox) is not double r || v <= 0 || r <= 0)
        {
            foreach (var t in all) t.Text = "–";
            uwagi.Text = "Wpisz prędkość V > 0 i promień R > 0.";
            return;
        }
        double? h = HBox.Text.Trim().Length == 0 ? null : Num(HBox);
        if (HBox.Text.Trim().Length > 0 && h == null) { foreach (var t in all) t.Text = "–"; uwagi.Text = "Przechyłka h: liczba w mm albo puste pole."; return; }
        var d = new Dopuszczalne
        {
            HMax = Num(hMax) ?? 150, HnMax = Num(hnMax) ?? 110, Psi = Num(psi) ?? 0.5, F = Num(f) ?? 35, NFactor = Num(nk) ?? 10, Round = Num(rnd) ?? 5,
        };
        var w = Obliczenia.Licz(v, r, h, d);
        HrText.Text = $"{F(w.Hr)} mm";
        HText.Text = $"{F(w.H)} mm" + (w.HProponowana ? " (propozycja)" : "");
        HnText.Text = w.Hn >= 0 ? $"{F(w.Hn)} mm" : $"nadmiar {F(-w.Hn)} mm";
        AnText.Text = $"{F(w.An, "0.00")} m/s²";
        string Mark(double l, string name) => $"{F(l)} m" + (w.Decyduje == name ? "  ← decyduje" : "");
        L1Text.Text = Mark(w.LRampa, "rampa przechyłkowa") + $"  (1:{F(d.NFactor * v, "0")})";
        L2Text.Text = Mark(w.LPsi, "przyrost przyspieszenia ψ");
        L3Text.Text = Mark(w.LF, "zmiana przechyłki f");
        LText.Text = $"{F(w.L, "0.#")} m";
        LText.Foreground = Brushes.DarkGreen; LText.FontSize = 16;
        VmaxText.Text = $"{F(w.VMax, "0")} km/h";
        uwagi.Text = string.Join("\n", w.Uwagi);
    }
}

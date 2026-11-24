using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Lcs.Lacznosc;

/// <summary>
/// Wybór toru szlakowego w komunikacie EZP: „bez” i tory szlaku do danego posterunku (nazwy z komunikatów gry,
/// np. Koluszki–Żakowice Płd.: 2, 3; Łazy–Łazy Ła: 224) przyciskami, inny numer w polu.
/// Wartosc ustawiona z kodu nie zgłasza Zmieniono – zdarzenie tylko z ręki dyżurnego.
/// </summary>
public sealed class WyborToru : StackPanel
{
    readonly List<ToggleButton> _przyciski = new();
    readonly StackPanel _rzad = new() { Orientation = Orientation.Horizontal };
    readonly TextBox _inny = new();
    int? _wartosc;
    bool _zKodu;

    public event Action Zmieniono;

    public WyborToru()
    {
        Orientation = Orientation.Horizontal;
        Children.Add(_rzad);
        Children.Add(new TextBlock { Text = "inny", Foreground = Wyglad.Szary, FontFamily = Wyglad.Mono, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 6, 0) });
        _inny.Width = 52; _inny.Height = Wyglad.H; _inny.MaxLength = 4;
        Wyglad.Pole(_inny);
        _inny.ToolTip = "Inny numer toru (cyfry)";
        _inny.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
        _inny.TextChanged += (_, _) =>
        {
            if (_zKodu) return;
            _wartosc = int.TryParse(_inny.Text.Trim(), out var t) ? t : null;
            Zaznacz();
            Zmieniono?.Invoke();
        };
        Children.Add(_inny);
        UstawTory(new[] { 1, 2 });
    }

    /// <summary>Przyciski: „bez” + podane tory (Alt+0 – bez, Alt+1… – kolejne przyciski).</summary>
    public void UstawTory(IEnumerable<int> tory)
    {
        var lista = tory.Distinct().OrderBy(x => x).ToList();
        if (_przyciski.Skip(1).Select(b => (int)b.Tag).SequenceEqual(lista) && _przyciski.Count > 0) return;
        _rzad.Children.Clear(); _przyciski.Clear();
        Dodaj("bez", null, "Bez numeru toru (Alt+0)");
        int i = 1;
        foreach (var t in lista) Dodaj(t.ToString(), t, $"Tor {t} (Alt+{i++})");
        Ustaw(_wartosc, reka: false);

        void Dodaj(string et, int? v, string tip)
        {
            var b = new ToggleButton { Content = et, Tag = v, MinWidth = 48, Height = Wyglad.H, Margin = new Thickness(0, 0, 4, 0), Focusable = false, ToolTip = tip };
            Wyglad.Klasyczny(b);
            b.Click += (_, _) => Ustaw(v, reka: true);
            _przyciski.Add(b); _rzad.Children.Add(b);
        }
    }

    /// <summary>Wybór przycisku o numerze (0 – bez, 1… – kolejne tory), np. z Alt+cyfra.</summary>
    public void WybierzNr(int i, bool reka)
    {
        if (i >= 0 && i < _przyciski.Count) Ustaw(_przyciski[i].Tag as int?, reka);
    }

    public int? Wartosc { get => _wartosc; set => Ustaw(value, reka: false); }

    public void Ustaw(int? v, bool reka)
    {
        _wartosc = v;
        _zKodu = true;
        _inny.Text = v is int t && !_przyciski.Any(b => b.Tag is int bt && bt == t) ? t.ToString() : "";
        _zKodu = false;
        Zaznacz();
        if (reka) Zmieniono?.Invoke();
    }

    void Zaznacz()
    {
        foreach (var b in _przyciski) b.IsChecked = Equals(b.Tag as int?, _wartosc) && (_wartosc != null || _inny.Text.Trim().Length == 0);
    }
}

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LcsWidzew.UI;

/// <summary>Przycisk polecenia w panelu: napis (kod), wiersz polecenia dla systemu nadrzędnego, dostępność, inicjalizacja polecenia specjalnego (czerwony).</summary>
public record PolecenieBtn(string Label, string Line, bool Enabled = true, bool Init = false, string Tip = null);

/// <summary>Wpis zakładki „Historia”: czas wysłania, polecenie, przyjęcie przez system nadrzędny, realizacja w komputerze zależności.</summary>
public class HistoriaWpis : System.ComponentModel.INotifyPropertyChanged
{
    public string Czas { get; set; }
    public string Polecenie { get; set; }
    public string Przyjecie { get; set; }
    string realizacja;
    public string Realizacja { get => realizacja; set { realizacja = value; PropertyChanged?.Invoke(this, new(nameof(Realizacja))); } }
    /// <summary>Przebieg do śledzenia realizacji: początek i rodzaj (P / M); null – realizacja znana od razu.</summary>
    public string Start, Typ;
    public double Wyslano;
    public bool Widziany;
    public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
}

/// <summary>
/// Panel „Polecenia / Historia” w prawym górnym rogu obrazu stacji – wg zdjęć stanowiska ISDR (Zrodla/ISDR_zdjecia.md § 2, ROZWOJ.md § 2a).
/// Zastępuje menu PPM: nagłówek z nazwą wybranych obiektów i przyciski poleceń (ciemne – niedostępne w danym stanie),
/// po wybraniu polecenia nagłówek „KOD obiekty” i przycisk „Wyślij” – dopiero on przekazuje polecenie.
/// </summary>
public class PanelPolecen : Grid
{
    static Brush B(byte r, byte g, byte b) { var x = new SolidColorBrush(Color.FromRgb(r, g, b)); x.Freeze(); return x; }
    static Brush Grad(Color a, Color b) { var x = new LinearGradientBrush(a, b, 90); x.Freeze(); return x; }
    static readonly Brush Tlo = Grad(Color.FromRgb(0x2a, 0x58, 0x77), Color.FromRgb(0x3c, 0x70, 0x8f)), Ramka = B(0xc8, 0xcc, 0xd0),
        ZakladkaWyb = Grad(Color.FromRgb(0x3a, 0x6c, 0x8c), Color.FromRgb(0x2a, 0x58, 0x77)), Zakladka = Grad(Color.FromRgb(0x6a, 0x6a, 0x6a), Color.FromRgb(0x48, 0x48, 0x48)),
        BtnJasny = Grad(Color.FromRgb(0xf6, 0xf6, 0xf6), Color.FromRgb(0xc8, 0xc8, 0xc8)), BtnCiemny = Grad(Color.FromRgb(0x9c, 0xa8, 0xae), Color.FromRgb(0x78, 0x86, 0x8e)),
        BtnCzerw = Grad(Color.FromRgb(0xf2, 0x6a, 0x6a), Color.FromRgb(0xd8, 0x40, 0x40)), BtnCzerwCiemny = Grad(Color.FromRgb(0x9a, 0x62, 0x62), Color.FromRgb(0x80, 0x4c, 0x4c)),
        NaglHist = B(0x1f, 0x46, 0x66);

    readonly TextBlock naglowek = new() { Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 15, Margin = new Thickness(2, 0, 0, 2) };
    readonly WrapPanel przyciski = new() { Orientation = Orientation.Horizontal };
    readonly Border zakPol, zakHist;
    readonly Grid stronaPol = new(), stronaHist = new();
    public readonly ObservableCollection<HistoriaWpis> Historia = new();
    readonly DataGrid gridHist;
    string podpis;

    /// <summary>Nazwa wybranych obiektów (np. „R”, „R 1B”, „An”).</summary>
    public string Obiekty { get; private set; }
    /// <summary>Wybrane (przygotowane) polecenie – czeka na „Wyślij”.</summary>
    public PolecenieBtn Wybrane { get; private set; }
    public bool HistoriaWidoczna => stronaHist.Visibility == Visibility.Visible;
    public IReadOnlyList<PolecenieBtn> Przyciski { get; private set; } = Array.Empty<PolecenieBtn>();

    /// <summary>Kliknięto przycisk polecenia (przygotowanie), „Wyślij” (wysłanie).</summary>
    public event Action<PolecenieBtn> Przygotowano, Wyslano;

    public PanelPolecen()
    {
        Width = 460; Height = 150;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var zak = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 0, -1.5) };
        zakPol = Zakladka_("Polecenia", () => Pokaz(false)); zakHist = Zakladka_("Historia", () => Pokaz(true));
        zak.Children.Add(zakPol); zak.Children.Add(zakHist);
        Children.Add(zak);

        var tresc = new Border { Background = Tlo, BorderBrush = Ramka, BorderThickness = new Thickness(1.5), Padding = new Thickness(3, 2, 3, 3) };
        SetRow(tresc, 1); Children.Add(tresc);
        var g = new Grid(); tresc.Child = g;
        var sp = new StackPanel(); sp.Children.Add(naglowek); sp.Children.Add(przyciski);
        stronaPol.Children.Add(sp);
        g.Children.Add(stronaPol); g.Children.Add(stronaHist);

        gridHist = new DataGrid
        {
            Background = Brushes.Transparent, RowBackground = Brushes.Transparent, AlternatingRowBackground = Brushes.Transparent, Foreground = Brushes.White,
            BorderThickness = new Thickness(0), GridLinesVisibility = DataGridGridLinesVisibility.Vertical, VerticalGridLinesBrush = new SolidColorBrush(Color.FromArgb(0x60, 0xff, 0xff, 0xff)),
            HeadersVisibility = DataGridHeadersVisibility.Column, IsReadOnly = true, AutoGenerateColumns = false, CanUserAddRows = false, FontFamily = new FontFamily("Segoe UI"), FontSize = 11,
            RowHeight = 17, ItemsSource = Historia, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var hs = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        hs.Setters.Add(new Setter(Control.BackgroundProperty, NaglHist)); hs.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
        hs.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold)); hs.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
        hs.Setters.Add(new Setter(Control.BorderBrushProperty, Ramka)); hs.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 1))); hs.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4, 1, 4, 1)));
        gridHist.ColumnHeaderStyle = hs;
        var cs = new Style(typeof(DataGridCell)); cs.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0))); cs.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        cs.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
        gridHist.CellStyle = cs;
        void Kol(string h, string p, double w) => gridHist.Columns.Add(new DataGridTextColumn { Header = h, Binding = new System.Windows.Data.Binding(p), Width = w > 0 ? new DataGridLength(w) : new DataGridLength(1, DataGridLengthUnitType.Star) });
        Kol("Czas", nameof(HistoriaWpis.Czas), 62); Kol("Polecenie", nameof(HistoriaWpis.Polecenie), 0); Kol("Przyjęcie", nameof(HistoriaWpis.Przyjecie), 74); Kol("Realizacja", nameof(HistoriaWpis.Realizacja), 132);
        stronaHist.Children.Add(gridHist);
        Pokaz(false);
    }

    Border Zakladka_(string t, Action klik)
    {
        var b = new Border
        {
            Child = new TextBlock { Text = t, Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 12.5 }, Padding = new Thickness(7, 2, 7, 2),
            BorderBrush = Ramka, BorderThickness = new Thickness(1.5, 1.5, 1.5, 0), CornerRadius = new CornerRadius(5, 5, 0, 0), Margin = new Thickness(0, 0, 2, 0), Cursor = Cursors.Hand
        };
        b.MouseLeftButtonDown += (s, e) => { klik(); e.Handled = true; };
        return b;
    }

    public void Pokaz(bool historia)
    {
        stronaHist.Visibility = historia ? Visibility.Visible : Visibility.Collapsed;
        stronaPol.Visibility = historia ? Visibility.Collapsed : Visibility.Visible;
        zakPol.Background = historia ? Zakladka : ZakladkaWyb; zakHist.Background = historia ? ZakladkaWyb : Zakladka;
        Panel.SetZIndex(zakPol, historia ? 0 : 1); Panel.SetZIndex(zakHist, historia ? 1 : 0);
    }

    /// <summary>Wybrane obiekty i ich polecenia; przyciski przebudowywane tylko przy zmianie (stan odświeżany cyklicznie).</summary>
    public void Ustaw(string obiekty, IReadOnlyList<PolecenieBtn> lista, string opis = null)
    {
        if (Wybrane != null && obiekty == Obiekty) return;
        var sig = obiekty + "|" + string.Join(";", lista.Select(x => x.Label + x.Line + x.Enabled + x.Init));
        if (sig == podpis) return;
        podpis = sig; Obiekty = obiekty; Wybrane = null; Przyciski = lista;
        naglowek.Text = obiekty ?? ""; naglowek.ToolTip = opis;
        przyciski.Children.Clear();
        foreach (var p in lista) przyciski.Children.Add(Przycisk(p.Label, p.Enabled, p.Init, p.Tip, () => Wybierz(p)));
    }

    /// <summary>Polecenie przygotowane: nagłówek „KOD obiekty” i jeden przycisk „Wyślij”.</summary>
    public void Wybierz(PolecenieBtn p)
    {
        if (!p.Enabled) return;
        Wybrane = p; podpis = null;
        naglowek.Text = $"{p.Label} {Obiekty}";
        przyciski.Children.Clear();
        przyciski.Children.Add(Przycisk("Wyślij", true, false, p.Tip ?? p.Line, () => { var w = Wybrane; if (w != null) Wyslano?.Invoke(w); }, 70));
        Przygotowano?.Invoke(p);
    }

    public void Wyczysc()
    {
        podpis = null; Obiekty = null; Wybrane = null; Przyciski = Array.Empty<PolecenieBtn>();
        naglowek.Text = ""; naglowek.ToolTip = null; przyciski.Children.Clear();
    }

    static Border Przycisk(string t, bool on, bool init, string tip, Action klik, double minW = 0)
    {
        var b = new Border
        {
            Background = init ? (on ? BtnCzerw : BtnCzerwCiemny) : on ? BtnJasny : BtnCiemny, BorderBrush = on ? B(0x6a, 0x6a, 0x6a) : B(0xb8, 0xc0, 0xc4),
            BorderThickness = new Thickness(1.2), CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 1, 3, 1), Padding = new Thickness(6, 1, 6, 1), MinWidth = minW,
            Child = new TextBlock
            {
                Text = t, FontFamily = new FontFamily("Segoe UI"), FontSize = 12.5, HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = init ? (on ? Brushes.White : B(0xd8, 0xc0, 0xc0)) : on ? B(0x18, 0x18, 0x18) : B(0xe8, 0xee, 0xf0)
            },
            ToolTip = tip, Cursor = on ? Cursors.Hand : Cursors.Arrow, Opacity = on ? 1 : 0.85
        };
        if (on) b.MouseLeftButtonUp += (s, e) => { klik(); e.Handled = true; };
        b.MouseLeftButtonDown += (s, e) => e.Handled = true;
        return b;
    }
}

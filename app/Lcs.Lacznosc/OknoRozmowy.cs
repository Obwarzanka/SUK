using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Lcs.Lacznosc;

/// <summary>
/// Osobne okno rozmowy: wszystkie komunikaty z godziną gry, kierunkiem, „poc. · tor”, pełnym zdaniem z gry i statusem.
/// Filtr posterunku; klik we wpis – panel EZP przechodzi na tego sąsiada (odpowiedź na ten komunikat).
/// Zamknięcie tylko chowa okno.
/// </summary>
public sealed class OknoRozmowy : Window
{
    readonly ListBox _lista = new();
    readonly ComboBox _filtr = new();
    readonly ICollectionView _widok;
    readonly ObservableCollection<Sasiad> _sasiedzi;
    public event Action<Wpis> KliknietoWpis;
    public event Action Wyczysc;
    public bool Zamykanie { get; set; }

    public OknoRozmowy(ObservableCollection<Wpis> wpisy, ObservableCollection<Sasiad> sasiedzi)
    {
        _sasiedzi = sasiedzi;
        Title = "Rozmowa EZP"; Width = 560; Height = 700; Background = Brushes.Black;
        FontFamily = Wyglad.Mono; Foreground = Wyglad.Tekst;
        try { Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/app.ico")); } catch { }

        _widok = new CollectionViewSource { Source = wpisy }.View;
        _widok.Filter = o => _filtr.SelectedItem is not string s || s == Wszystkie || ((Wpis)o).Sasiad == s;
        _lista.IsSynchronizedWithCurrentItem = false;   // bez samoczynnego zaznaczenia pierwszego wpisu (przestawiałoby panel EZP)
        _lista.ItemsSource = _widok;
        _lista.ItemTemplate = SzablonWpisu();
        Wyglad.Lista(_lista);
        ScrollViewer.SetHorizontalScrollBarVisibility(_lista, ScrollBarVisibility.Disabled);
        _lista.SelectionChanged += (_, _) => { if (_lista.SelectedItem is Wpis w) KliknietoWpis?.Invoke(w); };
        wpisy.CollectionChanged += (_, _) => { if (_lista.Items.Count > 0) _lista.ScrollIntoView(_lista.Items[^1]); };

        OdswiezFiltr();
        sasiedzi.CollectionChanged += (_, _) => OdswiezFiltr();
        _filtr.SelectionChanged += (_, _) => _widok.Refresh();
        _filtr.MinWidth = 220;

        var gora = new DockPanel { Background = Wyglad.BelkaGorna, LastChildFill = false };
        gora.Children.Add(new TextBlock { Text = "Posterunek:", Foreground = Brushes.Black, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 6, 0), FontFamily = new FontFamily("Tahoma") });
        gora.Children.Add(_filtr);
        var czysc = Wyglad.Przycisk("Wyczyść czat", () => Wyczysc?.Invoke(), "Czyści rozmowę w programie (czat w grze zostaje)");
        czysc.Margin = new Thickness(4);
        DockPanel.SetDock(czysc, Dock.Right); gora.Children.Add(czysc);

        var dok = new DockPanel();
        DockPanel.SetDock(gora, Dock.Top); dok.Children.Add(gora); dok.Children.Add(_lista);
        Content = dok;
        Closing += (_, e) => { if (!Zamykanie) { e.Cancel = true; Hide(); } };
    }

    const string Wszystkie = "(wszystkie)";

    void OdswiezFiltr()
    {
        var wyb = _filtr.SelectedItem as string;
        _filtr.ItemsSource = new[] { Wszystkie }.Concat(_sasiedzi.Select(s => s.Nazwa)).ToList();
        _filtr.SelectedItem = wyb != null && ((List<string>)_filtr.ItemsSource).Contains(wyb) ? wyb : Wszystkie;
    }

    public void Pokaz(string sasiad)
    {
        if (sasiad != null) _filtr.SelectedItem = sasiad;
        Show(); Activate();
    }

    static DataTemplate SzablonWpisu() => (DataTemplate)System.Windows.Markup.XamlReader.Parse(@"
<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
<Border Margin='2,3' Padding='6,4' BorderThickness='3,0,0,0'>
  <Border.Style>
    <Style TargetType='Border'>
      <Setter Property='BorderBrush' Value='#29B6D6'/>
      <Style.Triggers><DataTrigger Binding='{Binding Wychodzacy}' Value='True'><Setter Property='BorderBrush' Value='#FF8A3D'/></DataTrigger></Style.Triggers>
    </Style>
  </Border.Style>
  <StackPanel>
    <TextBlock Text='{Binding Naglowek}' FontSize='12'>
      <TextBlock.Style>
        <Style TargetType='TextBlock'>
          <Setter Property='Foreground' Value='#29B6D6'/>
          <Style.Triggers><DataTrigger Binding='{Binding Wychodzacy}' Value='True'><Setter Property='Foreground' Value='#FF8A3D'/></DataTrigger></Style.Triggers>
        </Style>
      </TextBlock.Style>
    </TextBlock>
    <TextBlock Text='{Binding Szczegoly}' FontSize='15' FontWeight='Bold' Foreground='#FFE000'>
      <TextBlock.Style>
        <Style TargetType='TextBlock'>
          <Style.Triggers><DataTrigger Binding='{Binding Szczegoly}' Value=''><Setter Property='Visibility' Value='Collapsed'/></DataTrigger></Style.Triggers>
        </Style>
      </TextBlock.Style>
    </TextBlock>
    <TextBlock Text='{Binding Tekst}' TextWrapping='Wrap' FontSize='14' Foreground='#00E000'/>
    <TextBlock Text='{Binding Status}' Foreground='#808080' FontSize='11'/>
  </StackPanel>
</Border>
</DataTemplate>");
}

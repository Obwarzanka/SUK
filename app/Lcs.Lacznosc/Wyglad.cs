using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;

namespace Lcs.Lacznosc;

/// <summary>Wygląd w stylu pulpitu EbiScreen / MOR: czarne pola, szare wypukłe przyciski, Consolas, belki cyjanowe.</summary>
public static class Wyglad
{
    public static readonly Brush Tlo = P("#000000"), TloPola = P("#000000"), Ramka = P("#808080"), Tekst = P("#C8C8C8");
    public static readonly Brush Zielony = P("#00E000"), Zolty = P("#FFE000"), Cyjan = P("#29B6D6"), Czerwony = P("#FF4040");
    public static readonly Brush Szary = P("#808080"), BelkaGorna = P("#DADADA"), Pomaranczowy = P("#FF8A3D");
    public static readonly Brush NaglowekRamki = P("#12243A"), RamkaPanelu = P("#2E4A66");
    public static readonly FontFamily Mono = new("Consolas");
    /// <summary>Jedna wysokość przycisków i pól w panelach.</summary>
    public const double H = 26;

    public static Brush P(string hex) { var b = (Brush)new BrushConverter().ConvertFromString(hex); b.Freeze(); return b; }

    const string Szablon = @"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{0}'>
  <Border x:Name='g' BorderThickness='1,1,0,0' BorderBrush='#9A9A9A' Background='{{TemplateBinding Background}}'>
    <Border x:Name='d' BorderThickness='0,0,1,1' BorderBrush='#1A1A1A' Padding='{{TemplateBinding Padding}}'>
      <ContentPresenter HorizontalAlignment='{{TemplateBinding HorizontalContentAlignment}}' VerticalAlignment='Center'/>
    </Border>
  </Border>
  <ControlTemplate.Triggers>
    <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='g' Property='BorderBrush' Value='#29B6D6'/></Trigger>
    {1}
    <Trigger Property='IsEnabled' Value='False'><Setter Property='Foreground' Value='#707070'/></Trigger>
  </ControlTemplate.Triggers>
</ControlTemplate>";
    const string Wcisniety = "<Setter TargetName='g' Property='BorderBrush' Value='#1A1A1A'/><Setter TargetName='d' Property='BorderBrush' Value='#9A9A9A'/><Setter TargetName='g' Property='Background' Value='#C8C8C8'/><Setter Property='Foreground' Value='Black'/>";

    static readonly ControlTemplate SzablonPrzycisku = (ControlTemplate)XamlReader.Parse(string.Format(Szablon, "Button",
        $"<Trigger Property='IsPressed' Value='True'>{Wcisniety}</Trigger>"));
    static readonly ControlTemplate SzablonPrzelacznika = (ControlTemplate)XamlReader.Parse(string.Format(Szablon, "ToggleButton",
        $"<Trigger Property='IsChecked' Value='True'>{Wcisniety}</Trigger><Trigger Property='IsPressed' Value='True'>{Wcisniety}</Trigger>"));

    /// <summary>Szary wypukły przycisk (wciśnięty / zaznaczony – jasny, napis czarny).</summary>
    public static T Klasyczny<T>(T b) where T : ButtonBase
    {
        b.Template = b is ToggleButton ? SzablonPrzelacznika : SzablonPrzycisku;
        b.Background = P("#4A4A4A"); b.Foreground = Brushes.White;
        b.Padding = new Thickness(8, 2, 8, 2); b.FontFamily = Mono; b.FontSize = 12;
        b.Cursor = System.Windows.Input.Cursors.Hand;
        return b;
    }

    public static Button Przycisk(string tekst, Action akcja, string podpowiedz = null)
    {
        var b = Klasyczny(new Button { Content = tekst, ToolTip = podpowiedz, Margin = new Thickness(0, 0, 4, 4) });
        b.Click += (_, _) => akcja();
        return b;
    }

    public static void Pole(TextBox t)
    {
        t.Background = Brushes.Black; t.Foreground = Zolty; t.BorderBrush = Ramka; t.CaretBrush = Brushes.White;
        t.FontFamily = Mono; t.FontSize = 13; t.Padding = new Thickness(3, 1, 3, 1); t.VerticalContentAlignment = VerticalAlignment.Center;
    }

    public static void Lista(ItemsControl l)
    {
        l.Background = Brushes.Black; l.Foreground = Zielony; l.BorderBrush = P("#303030");
        l.FontFamily = Mono; l.FontSize = 12;
        if (l is ListBox lb) lb.HorizontalContentAlignment = HorizontalAlignment.Stretch;
    }

    public static TextBlock Etykieta(string t, double szer = 0) => new()
    {
        Text = t, Foreground = Szary, FontFamily = Mono, FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
        Width = szer > 0 ? szer : double.NaN, Margin = new Thickness(0, 0, 6, 0),
    };
}

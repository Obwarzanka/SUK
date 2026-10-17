using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Lcs.Komputerownia;

public sealed class PrzebiegRow { public string Id { get; set; } public string Typ { get; set; } public string Stan { get; set; } public string Sygnal { get; set; } public string Zwrotnice { get; set; } public string Odcinki { get; set; } public string Zwolnione { get; set; } public string Ochronna { get; set; } public string Boczna { get; set; } public string Czas { get; set; } public string V { get; set; } }
public sealed class UtwRow { public string Rodzaj { get; set; } public string Obiekt { get; set; } public string Utwierdzenie { get; set; } public string Stan { get; set; } }
public sealed class KomRow { public string Czas { get; set; } public string Kierunek { get; set; } public string Rodzaj { get; set; } public string Obiekt { get; set; } public string Tresc { get; set; } }
public sealed class PolRow { public string Czas { get; set; } public string Kategoria { get; set; } public string Tresc { get; set; } public string Operator { get; set; } }

/// <summary>
/// Zakładka „Komputer zależności”: kontrola zależności na żywo (naruszenia niezmienników KZ), przebiegi i ich stan, utwierdzenia,
/// komunikacja KZ ↔ karty (rozkazy i zmiany statusów), polecenia z pulpitu, „Sprawdź przebieg” (dlaczego można / nie można nastawić).
/// </summary>
public class KzPanel : DockPanel
{
    public readonly TextBlock KontrolaInfo = new() { FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
    public readonly ListBox KontrolaLista = new() { MaxHeight = 120, FontSize = 12.5 };
    public readonly DataGrid Przebiegi = Siatka(), Utwierdzenia = Siatka(), Komunikacja = Siatka(), Polecenia = Siatka();
    public readonly TextBox SprawdzTekst = new() { Width = 220, ToolTip = "Fragment nazwy przebiegu, np. „A P102” albo „P102 1A”" };
    public readonly Button SprawdzBtn = new() { Content = "Sprawdź przebieg", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(6, 0, 0, 0) };
    public readonly TextBox SprawdzWynik = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily("Consolas"), FontSize = 12 };
    public readonly ObservableCollection<KomRow> Kom = new();
    public readonly ObservableCollection<PolRow> Pol = new();
    long odKom, odPol;
    public int Naruszenia = -1, Sprawdzono;
    Polaczenie pol;

    static DataGrid Siatka() => new()
    {
        AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, HeadersVisibility = DataGridHeadersVisibility.Column, RowHeight = 22,
        GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(0xe2, 0xe2, 0xdc)),
    };
    static void Kol(DataGrid g, string h, string p, double w, bool star = false) =>
        g.Columns.Add(new DataGridTextColumn { Header = h, Binding = new Binding(p), Width = star ? new DataGridLength(w, DataGridLengthUnitType.Star) : new DataGridLength(w) });
    static UIElement Ramka(string tytul, UIElement c)
    {
        var d = new DockPanel { Margin = new Thickness(0, 0, 8, 8) };
        var t = new TextBlock { Text = tytul, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 3) };
        DockPanel.SetDock(t, Dock.Top); d.Children.Add(t); d.Children.Add(c);
        return d;
    }

    public KzPanel()
    {
        Kol(Przebiegi, "Przebieg", nameof(PrzebiegRow.Id), 1.3, true); Kol(Przebiegi, "Stan", nameof(PrzebiegRow.Stan), 1.3, true); Kol(Przebiegi, "Sygnał", nameof(PrzebiegRow.Sygnal), 55);
        Kol(Przebiegi, "V", nameof(PrzebiegRow.V), 95); Kol(Przebiegi, "Zwrotnice", nameof(PrzebiegRow.Zwrotnice), 1, true); Kol(Przebiegi, "Odcinki (utwierdzone)", nameof(PrzebiegRow.Odcinki), 1, true);
        Kol(Przebiegi, "Zwoln.", nameof(PrzebiegRow.Zwolnione), 50); Kol(Przebiegi, "Ochr. boczna", nameof(PrzebiegRow.Boczna), 0.8, true); Kol(Przebiegi, "Czas", nameof(PrzebiegRow.Czas), 50);
        Kol(Utwierdzenia, "Rodzaj", nameof(UtwRow.Rodzaj), 90); Kol(Utwierdzenia, "Obiekt", nameof(UtwRow.Obiekt), 70); Kol(Utwierdzenia, "Utwierdzenie", nameof(UtwRow.Utwierdzenie), 2, true); Kol(Utwierdzenia, "Stan", nameof(UtwRow.Stan), 1, true);
        Kol(Komunikacja, "Czas", nameof(KomRow.Czas), 62); Kol(Komunikacja, "Kierunek", nameof(KomRow.Kierunek), 82); Kol(Komunikacja, "Karta", nameof(KomRow.Rodzaj), 85); Kol(Komunikacja, "Obiekt", nameof(KomRow.Obiekt), 75); Kol(Komunikacja, "Treść", nameof(KomRow.Tresc), 1, true);
        Kol(Polecenia, "Czas", nameof(PolRow.Czas), 62); Kol(Polecenia, "", nameof(PolRow.Kategoria), 80); Kol(Polecenia, "Polecenie / odpowiedź KZ", nameof(PolRow.Tresc), 1, true); Kol(Polecenia, "Operator", nameof(PolRow.Operator), 80);
        Komunikacja.ItemsSource = Kom; Polecenia.ItemsSource = Pol;
        var ks = new Style(typeof(DataGridRow));
        var kt = new DataTrigger { Binding = new Binding(nameof(KomRow.Kierunek)), Value = "KZ → karta" }; kt.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(Color.FromRgb(0xe6, 0xf0, 0xff)))); ks.Triggers.Add(kt);
        Komunikacja.RowStyle = ks;
        var ps = new Style(typeof(DataGridRow));
        var pt = new DataTrigger { Binding = new Binding(nameof(PolRow.Kategoria)), Value = "Odrzucone" }; pt.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(Color.FromRgb(0xff, 0xe4, 0xe0)))); ps.Triggers.Add(pt);
        Polecenia.RowStyle = ps;

        var gora = new StackPanel { Margin = new Thickness(0, 0, 8, 8) };
        gora.Children.Add(KontrolaInfo);
        gora.Children.Add(new TextBlock
        {
            Text = "Kontrola zależności na żywo: przy każdym sygnale zezwalającym – zwrotnice z kontrolą w położeniu i utwierdzone, odcinki utwierdzone i (pociągowy) wolne, wykolejnice zdjęte, ochrona boczna; sygnał zezwalający tylko przy nastawionym przebiegu; odcinek w jednym przebiegu; utwierdzenia tylko istniejących przebiegów; sygnał zgodny z Ie-1 (prędkość × następny semafor); karta wyświetla sygnał zlecony przez KZ.",
            TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, FontSize = 12, Margin = new Thickness(0, 0, 0, 4),
        });
        gora.Children.Add(KontrolaLista);
        DockPanel.SetDock(gora, Dock.Top); Children.Add(gora);

        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        void Put(UIElement e, int r, int c) { Grid.SetRow(e, r); Grid.SetColumn(e, c); g.Children.Add(e); }
        Put(Ramka("Przebiegi w komputerze zależności", Przebiegi), 0, 0);
        Put(Ramka("Utwierdzenia (odcinki, zwrotnice, wykolejnice, sygnalizatory w ochronie)", Utwierdzenia), 1, 0);
        var spr = new DockPanel();
        var sprBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        sprBar.Children.Add(SprawdzTekst); sprBar.Children.Add(SprawdzBtn);
        DockPanel.SetDock(sprBar, Dock.Top); spr.Children.Add(sprBar); spr.Children.Add(SprawdzWynik);
        Put(Ramka("Sprawdź przebieg z tablicy (przeszkody, prędkość, sygnał)", spr), 0, 1);
        var logi = new TabControl();
        logi.Items.Add(new TabItem { Header = "Komunikacja KZ ↔ karty", Content = Komunikacja });
        logi.Items.Add(new TabItem { Header = "Polecenia z pulpitu", Content = Polecenia });
        Put(Ramka("Dziennik", logi), 1, 1);
        Children.Add(g);

        SprawdzBtn.Click += async (s, e) => await Sprawdz();
        SprawdzTekst.KeyDown += async (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) await Sprawdz(); };
        KontrolaInfo.Text = "Kontrola zależności: brak danych (niepołączony)";
    }

    /// <summary>Nowe połączenie – dzienniki od początku.</summary>
    public void Ustaw(Polaczenie p) { pol = p; odKom = odPol = 0; Kom.Clear(); Pol.Clear(); Naruszenia = -1; }

    public async Task Odswiez()
    {
        if (pol == null) return;
        var d = await pol.Zadanie("kz", ("odKom", odKom), ("odPol", odPol));
        if (d == null) return;
        Przebiegi.ItemsSource = d["Przebiegi"]!.AsArray().Select(n => new PrzebiegRow
        {
            Id = (string)n["Id"], Typ = (string)n["Typ"], Stan = (string)n["Stan"], Sygnal = (string)n["Sygnal"], Zwrotnice = (string)n["Zwrotnice"], Odcinki = (string)n["Odcinki"],
            Zwolnione = (string)n["Zwolnione"], Ochronna = (string)n["Ochronna"], Boczna = (string)n["Boczna"], Czas = (string)n["Czas"], V = (string)n["V"],
        }).ToList();
        Utwierdzenia.ItemsSource = d["Utwierdzenia"]!.AsArray().Select(n => new UtwRow { Rodzaj = (string)n["Rodzaj"], Obiekt = (string)n["Obiekt"], Utwierdzenie = (string)n["Utwierdzenie"], Stan = (string)n["Stan"] }).ToList();
        var k = d["Kontrola"]!.AsArray();
        Sprawdzono = d["Sprawdzono"]?.GetValue<int>() ?? 0;
        Naruszenia = k.Count(x => (string)x["Poziom"] == "naruszenie");
        int uwagi = k.Count - Naruszenia;
        KontrolaInfo.Text = Naruszenia == 0
            ? $"Kontrola zależności ({(string)d["Zegar"]}): brak naruszeń – sprawdzono {Sprawdzono} warunków{(uwagi > 0 ? $", uwag: {uwagi}" : "")}"
            : $"Kontrola zależności ({(string)d["Zegar"]}): NARUSZENIA: {Naruszenia} (sprawdzono {Sprawdzono} warunków)";
        KontrolaInfo.Foreground = Naruszenia == 0 ? new SolidColorBrush(Color.FromRgb(0x1d, 0x7a, 0x2e)) : Brushes.Firebrick;
        KontrolaLista.ItemsSource = k.Select(x => new TextBlock
        {
            Text = ((string)x["Poziom"] == "naruszenie" ? "✗ " : "• ") + (string)x["Tekst"], TextWrapping = TextWrapping.Wrap,
            Foreground = (string)x["Poziom"] == "naruszenie" ? Brushes.Firebrick : Brushes.DarkGoldenrod,
        }).ToList();
        KontrolaLista.Visibility = k.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        bool nk = false, np = false;
        foreach (var n in d["Komunikacja"]!.AsArray())
        {
            odKom = Math.Max(odKom, n["Id"]!.GetValue<long>()); nk = true;
            Kom.Add(new KomRow { Czas = (string)n["Czas"], Kierunek = (string)n["Kierunek"], Rodzaj = (string)n["Rodzaj"], Obiekt = Lbl((string)n["Obiekt"]), Tresc = (string)n["Tresc"] });
        }
        foreach (var n in d["Polecenia"]!.AsArray())
        {
            odPol = Math.Max(odPol, n["Id"]!.GetValue<long>()); np = true;
            Pol.Add(new PolRow { Czas = (string)n["Czas"], Kategoria = (string)n["Kategoria"], Tresc = (string)n["Tresc"], Operator = (string)n["Operator"] });
        }
        while (Kom.Count > 1500) Kom.RemoveAt(0);
        while (Pol.Count > 800) Pol.RemoveAt(0);
        if (nk && Kom.Count > 0) Komunikacja.ScrollIntoView(Kom[^1]);
        if (np && Pol.Count > 0) Polecenia.ScrollIntoView(Pol[^1]);
    }

    static string Lbl(string k) { var i = k?.IndexOf(':') ?? -1; return i >= 0 ? k[(i + 1)..] : k; }

    public async Task Sprawdz()
    {
        if (pol == null) { SprawdzWynik.Text = "niepołączony"; return; }
        var d = await pol.Zadanie("kzprzebieg", ("tekst", SprawdzTekst.Text));
        if (d == null) { SprawdzWynik.Text = "brak odpowiedzi"; return; }
        var a = d.AsArray();
        if (a.Count == 0) { SprawdzWynik.Text = "Brak przebiegu w tablicy zawierającego: " + SprawdzTekst.Text; return; }
        var sb = new System.Text.StringBuilder();
        foreach (var n in a)
        {
            sb.AppendLine($"{(string)n["Id"]} ({(string)n["Typ"]})");
            sb.AppendLine($"  zwrotnice: {(string)n["Zwrotnice"]}");
            sb.AppendLine($"  odcinki: {(string)n["Odcinki"]}");
            if (!string.IsNullOrEmpty((string)n["Boczna"])) sb.AppendLine($"  ochrona boczna: {(string)n["Boczna"]}");
            sb.AppendLine($"  V: {(string)n["V"]}; sygnał: {(string)n["Sygnal"]}");
            var st = (string)n["Stan"]; var why = n["Przeszkody"]!.AsArray().Select(x => (string)x).ToList();
            sb.AppendLine(st != "" ? $"  {st}" : why.Count == 0 ? "  ✓ można nastawić" : "  ✗ nie można nastawić: " + string.Join("; ", why));
            sb.AppendLine();
        }
        SprawdzWynik.Text = sb.ToString();
    }
}

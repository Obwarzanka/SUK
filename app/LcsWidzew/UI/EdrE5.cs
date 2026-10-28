using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LcsWidzew.Sim;

namespace LcsWidzew.UI;

/// <summary>Wspólny wygląd małych okien EDR (szare tło, Segoe UI).</summary>
public class OknoEdr : Window
{
    protected readonly StackPanel Sp = new() { Margin = new Thickness(12) };
    protected readonly TextBlock Blad = new() { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
    public OknoEdr(string tytul, double szer)
    {
        Title = tytul; Width = szer; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EdrWindow.Pedzel(0xE9, 0xE9, 0xE9); FontFamily = new FontFamily("Segoe UI"); FontSize = 12; ShowInTaskbar = false;
        Content = Sp;
    }
    protected static TextBlock Et(string t) => new() { Text = t, Foreground = Brushes.DimGray, FontSize = 11, Margin = new Thickness(0, 6, 0, 1), TextWrapping = TextWrapping.Wrap };
    /// <summary>Przyciski na dole: Zapisz (akcja zwraca wynik polecenia) i Anuluj.</summary>
    protected void Przyciski(string zapisz, Func<CmdResult> akcja)
    {
        Sp.Children.Add(Blad);
        var bp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        var z = new Button { Content = zapisz, MinWidth = 100, Margin = new Thickness(4), Padding = new Thickness(8, 1, 8, 1) };
        z.Click += (a, e) => { var r = akcja(); if (r == null) return; if (r.Ok) Close(); else Blad.Text = r.Msg; };
        bp.Children.Add(z);
        bp.Children.Add(new Button { Content = "Anuluj", Width = 100, Margin = new Thickness(4), IsCancel = true });
        Sp.Children.Add(bp);
    }
}

/// <summary>
/// Powiadomienie dróżników przejazdowych o jeździe pociągu (przycisk nad dziennikiem; podręcznik EDR rozdz. 7.4): wybór wiersza pociągu,
/// dla dróżnika „jak na linii jednotorowej” – godzina, „dwu- i więcej torowej” – ukośnik. Można też klikać komórki kolumny 8.
/// </summary>
public class OknoDroznikow : OknoEdr
{
    public OknoDroznikow(EdrWindow okno, EdrDziennik d) : base("Powiadomienie dróżników przejazdowych", 520)
    {
        var dr = d.Biezace.Droznicy.Where(x => x.WDzienniku).ToList();
        Sp.Children.Add(new TextBlock { Text = d.Nazwa, FontWeight = FontWeights.Bold, FontSize = 13 });
        if (dr.Count == 0)
        {
            Sp.Children.Add(Et("Na tym szlaku nie ma posterunków dróżników przejazdowych w dzienniku ruchu. Dodaje się je w ustawieniach zasadniczych dziennika (PPM na nazwie dziennika albo menu Konfiguracja)."));
            Przyciski("Zamknij", () => new CmdResult(true, "")); return;
        }
        var wiersze = d.Strony.LastOrDefault()?.Wiersze.Where(w => w.Rodzaj is "przyj" or "wypr").ToList() ?? new();
        Sp.Children.Add(Et("Pociąg (wiersz dziennika na bieżącej stronie):"));
        var poc = new ComboBox();
        foreach (var w in wiersze) poc.Items.Add($"{w.W.Nr} – {(w.Rodzaj == "przyj" ? "przyjęcie" : "wyprawienie")}{(w.Wymagane.Any(p => p.StartsWith("Dr:") && Edr.Wartosc(w.W, p) == "") ? " (do powiadomienia)" : "")}");
        // domyślnie ostatni pociąg z niepowiadomionym dróżnikiem
        poc.SelectedIndex = Math.Max(wiersze.FindLastIndex(w => w.Wymagane.Any(p => p.StartsWith("Dr:") && Edr.Wartosc(w.W, p) == "")), wiersze.Count - 1);
        Sp.Children.Add(poc);
        var lista = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        Sp.Children.Add(lista);
        var pola = new List<(EdrDroznik X, CheckBox Cb, TextBox Godz)>();
        void Lista()
        {
            lista.Children.Clear(); pola.Clear();
            if (poc.SelectedIndex < 0) return;
            var w = wiersze[poc.SelectedIndex];
            foreach (var x in dr)
            {
                var r = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                var wart = Edr.Wartosc(w.W, "Dr:" + x.Id);
                var cb = new CheckBox { Content = $"{x.Oznaczenie} – km {x.Km}{(x.Nazwa != "" ? " (" + x.Nazwa + ")" : "")}", Width = 300, IsChecked = wart == "" && x.Dotyczy(w.Rodzaj), IsEnabled = x.Dotyczy(w.Rodzaj), VerticalAlignment = VerticalAlignment.Center };
                r.Children.Add(cb);
                TextBox g = null;
                if (x.Godzina)
                {
                    g = new TextBox { Text = Edr.Godzina(wart) is string gw && gw != "" ? gw : okno.Teraz, Width = 60, ToolTip = "Godzina powiadomienia – kółko myszy: ± minuta (prawa połowa) / ± godzina (lewa)" };
                    var gg = g; g.MouseWheel += (a, e) => { e.Handled = true; bool h = e.GetPosition(gg).X < gg.ActualWidth / 2; int k = e.Delta > 0 ? 1 : -1; gg.Text = EdrWindow.Przesun(gg.Text, h ? k : 0, h ? 0 : k, okno.Teraz); };
                    r.Children.Add(g);
                }
                else r.Children.Add(new TextBlock { Text = "ukośnik „\\”", Foreground = Brushes.DimGray, VerticalAlignment = VerticalAlignment.Center });
                if (!x.Dotyczy(w.Rodzaj)) r.Children.Add(new TextBlock { Text = "  nie dotyczy tego kierunku", Foreground = Brushes.DimGray, FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
                else if (wart != "") r.Children.Add(new TextBlock { Text = $"  powiadomiono: {wart}", Foreground = Brushes.DarkGreen, FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
                lista.Children.Add(r); pola.Add((x, cb, g));
            }
        }
        poc.SelectionChanged += (a, e) => Lista(); Lista();
        Przyciski("Powiadom", () =>
        {
            if (poc.SelectedIndex < 0) return new CmdResult(false, "Brak pociągu");
            var w = wiersze[poc.SelectedIndex];
            foreach (var (x, cb, g) in pola.Where(p => p.Cb.IsChecked == true))
            {
                var r = okno.Wyslij(new EdrPolecenie { Dziennik = d.Id, Rodzaj = "pole", Wiersz = w.Id, Pole = "Dr:" + x.Id, Wartosc = x.Godzina ? g.Text : "\\" });
                if (!r.Ok) return r;
            }
            return new CmdResult(true, "");
        });
    }
}

/// <summary>Wpis o dowolnej treści do dziennika ruchu (przycisk „i” nad dziennikiem).</summary>
public class OknoWpisu : OknoEdr
{
    public OknoWpisu(EdrWindow okno, EdrDziennik d) : base("Wpis do dziennika ruchu", 520)
    {
        Sp.Children.Add(new TextBlock { Text = d.Nazwa, FontWeight = FontWeights.Bold, FontSize = 13 });
        Sp.Children.Add(Et("Treść wpisu:"));
        var t = new TextBox { Height = 110, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Sp.Children.Add(t);
        Loaded += (a, e) => t.Focus();
        Przyciski("Zapisz", () => okno.Wyslij(new EdrPolecenie { Dziennik = d.Id, Rodzaj = "wpis", Wartosc = t.Text }));
    }
}

/// <summary>Wykonanie sprawdzenia cyklicznego (przycisk w prawym górnym rogu): potwierdzenie z uwagami – zapis w dzienniku telefonicznym.</summary>
public class OknoSprawdzenia : OknoEdr
{
    public OknoSprawdzenia(EdrWindow okno, EdrSprawdzenieStan s) : base("Sprawdzenie cykliczne", 460)
    {
        Sp.Children.Add(new TextBlock { Text = s.Nazwa, FontWeight = FontWeights.Bold, FontSize = 13, TextWrapping = TextWrapping.Wrap });
        Sp.Children.Add(Et($"Co {Czas(s.Okres)}; ostatnio: {(s.Ostatnie != "" ? $"{s.Ostatnie} ({s.Kto})" : "nie wykonano")}; termin: {s.Nastepne}{(s.Stan == "po_terminie" ? " – PO TERMINIE" : "")}"));
        Sp.Children.Add(Et("Uwagi (np. wynik sprawdzenia):"));
        var u = new TextBox { Height = 60, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
        Sp.Children.Add(u);
        Sp.Children.Add(Et("Zapis „Sprawdzenie: … – wykonane” trafi do dziennika telefonicznego jako inne okoliczności."));
        Przyciski("Wykonano", () => okno.Wyslij(new EdrPolecenie { Rodzaj = "sprawdzenie", Wartosc = s.Post, Pole = s.Id, Dane = new EdrWersja { Uwagi = u.Text } }));
    }
    internal static string Czas(int min) => min % 60 == 0 ? $"{min / 60} h" : min > 60 ? $"{min / 60} h {min % 60} min" : $"{min} min";
}

/// <summary>Konfiguracja wymaganych sprawdzeń cyklicznych posterunku (najwyżej cztery przyciski) – menu Konfiguracja.</summary>
public class OknoSprawdzenUst : OknoEdr
{
    static readonly string[] Przyklady = { "Sprawdzenie łączności z posterunkami dróżników przejazdowych", "Sprawdzenie oświetlenia semaforów i wskaźników" };

    public OknoSprawdzenUst(EdrWindow okno, string post, string nazwaPost, List<EdrSprawdzenie> lista) : base($"{nazwaPost} – wymagane sprawdzenia", 640)
    {
        var l = lista.Select(x => x.Kopia()).ToList();
        Sp.Children.Add(Et("Sprawdzenia cykliczne posterunku – przyciski w prawym górnym rogu okna EDR (najwyżej cztery). Okres w minutach; przycisk żółknie 15 min przed terminem, po terminie jest czerwony."));
        var tab = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        Sp.Children.Add(tab);
        var pola = new List<(EdrSprawdzenie S, TextBox Nazwa, TextBox Okres)>();
        void Pokaz()
        {
            tab.Children.Clear(); pola.Clear();
            foreach (var s in l)
            {
                var r = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
                var us = new Button { Content = "Usuń", Margin = new Thickness(4, 0, 0, 0), Padding = new Thickness(6, 0, 6, 0) };
                var ss = s; us.Click += (a, e) => { Zbierz(); l.Remove(ss); Pokaz(); };
                DockPanel.SetDock(us, Dock.Right); r.Children.Add(us);
                var ok = new TextBox { Text = s.Okres.ToString(), Width = 60, Margin = new Thickness(4, 0, 0, 0), ToolTip = "Co ile minut" };
                DockPanel.SetDock(ok, Dock.Right); r.Children.Add(ok);
                var lab = new TextBlock { Text = "co [min]:", Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }; DockPanel.SetDock(lab, Dock.Right); r.Children.Add(lab);
                var na = new TextBox { Text = s.Nazwa }; r.Children.Add(na);
                tab.Children.Add(r); pola.Add((s, na, ok));
            }
            if (l.Count == 0) tab.Children.Add(new TextBlock { Text = "(brak – przyciski nie są wyświetlane)", Foreground = Brushes.DimGray });
        }
        void Zbierz() { foreach (var (s, na, ok) in pola) { s.Nazwa = na.Text.Trim(); s.Okres = int.TryParse(ok.Text, out var m) ? m : 0; } }
        var dod = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        var nowy = new Button { Content = "Utwórz nowy", Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(0, 0, 6, 4) };
        nowy.Click += (a, e) => { Zbierz(); if (l.Count < 4) { l.Add(new EdrSprawdzenie { Nazwa = "", Okres = 240 }); Pokaz(); } };
        dod.Children.Add(nowy);
        foreach (var p in Przyklady)
        {
            var b = new Button { Content = "+ " + p, Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(0, 0, 6, 4), FontSize = 11 };
            var pp = p; b.Click += (a, e) => { Zbierz(); if (l.Count < 4 && !l.Any(x => x.Nazwa == pp)) { l.Add(new EdrSprawdzenie { Nazwa = pp, Okres = 240 }); Pokaz(); } };
            dod.Children.Add(b);
        }
        Sp.Children.Add(dod);
        Pokaz();
        Przyciski("Zapisz", () => { Zbierz(); return okno.Wyslij(new EdrPolecenie { Rodzaj = "sprawdzenia", Wartosc = post, Sprawdzenia = l }); });
    }
}

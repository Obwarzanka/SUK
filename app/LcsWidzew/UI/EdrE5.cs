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
/// Powiadomienie dróżników przejazdowych o jeździe pociągu (przycisk nad dziennikiem; podręcznik EDR rozdz. 7.4, okno jak „Powiadomienia
/// dróżników przejazdowych” w EDR.exe – E9a): tylko wyprawienia, dróżnicy wymagający powiadomienia dla tego pociągu, „Powiadom wszystkich”,
/// godzina albo ukośnik wg ustawień dróżnika.
/// </summary>
public class OknoDroznikow : OknoEdr
{
    public OknoDroznikow(EdrWindow okno, EdrDziennik d) : base("Powiadomienia dróżników przejazdowych", 560)
    {
        var dr = d.Biezace.Droznicy.Where(x => x.WDzienniku).ToList();
        Sp.Children.Add(new TextBlock { Text = $"Szlak do posterunku: {d.Sasiad}   ({d.Nazwa})", FontWeight = FontWeights.Bold, FontSize = 13 });
        if (dr.Count == 0)
        {
            Sp.Children.Add(Et("Na tym szlaku nie ma posterunków dróżników przejazdowych w dzienniku ruchu. Dodaje się je w ustawieniach zasadniczych dziennika (PPM na nazwie dziennika albo menu Konfiguracja)."));
            Przyciski("Zamknij", () => new CmdResult(true, "")); return;
        }
        var wiersze = d.Strony.LastOrDefault()?.Wiersze.Where(w => w.Rodzaj == "wypr" && !w.Wykreslony).ToList() ?? new();
        Sp.Children.Add(Et("Nr pociągu (wyprawienie na bieżącej stronie dziennika):"));
        var poc = new ComboBox();
        bool Brak(EdrWiersz w) => w.Wymagane.Any(p => p.StartsWith("Dr:") && Edr.Wartosc(w.W, p) == "");
        foreach (var w in wiersze) poc.Items.Add($"{w.W.Nr}{(Brak(w) ? " (do powiadomienia)" : "")}");
        poc.SelectedIndex = Math.Max(wiersze.FindLastIndex(Brak), wiersze.Count - 1);
        Sp.Children.Add(poc);
        var wszyscy = new Button { Content = "Powiadom wszystkich", HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 8, 0, 2) };
        Sp.Children.Add(wszyscy);
        var lista = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
        Sp.Children.Add(lista);
        var pola = new List<(EdrDroznik X, CheckBox Cb, TextBox Godz)>();
        void Lista()
        {
            lista.Children.Clear(); pola.Clear();
            if (poc.SelectedIndex < 0) { lista.Children.Add(Et("Brak wyprawień na bieżącej stronie.")); return; }
            var w = wiersze[poc.SelectedIndex];
            foreach (var x in dr.Where(x => x.Dotyczy(w.Rodzaj, w.W.Nr)))
            {
                var r = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                var wart = Edr.Wartosc(w.W, "Dr:" + x.Id);
                r.Children.Add(new TextBlock { Text = $"{(x.Oznaczenie != "" ? x.Oznaczenie : x.Km)}  {x.Nazwa}", Width = 220, VerticalAlignment = VerticalAlignment.Center });
                var cb = new CheckBox { Content = "Powiadomiono", IsChecked = wart != "", Width = 120, VerticalAlignment = VerticalAlignment.Center };
                r.Children.Add(cb);
                TextBox g = null;
                if (x.Godzina(d.Tory.Count))
                {
                    g = new TextBox { Text = Edr.Godzina(wart) is string gw && gw != "" ? gw : "", Width = 60, ToolTip = "Godzina powiadomienia – kółko myszy: ± minuta (prawa połowa) / ± godzina (lewa)" };
                    var gg = g; g.MouseWheel += (a, e) => { e.Handled = true; bool h = e.GetPosition(gg).X < gg.ActualWidth / 2; int k = e.Delta > 0 ? 1 : -1; gg.Text = EdrWindow.Przesun(gg.Text, h ? k : 0, h ? 0 : k, okno.Teraz); };
                    cb.Checked += (a, e) => { if (gg.Text == "") gg.Text = okno.Teraz; };
                    g.TextChanged += (a, e) => cb.IsChecked = gg.Text.Trim() != "";
                    r.Children.Add(g);
                }
                else r.Children.Add(new TextBlock { Text = "ukośnik „\\”", Foreground = Brushes.DimGray, VerticalAlignment = VerticalAlignment.Center });
                lista.Children.Add(r); pola.Add((x, cb, g));
            }
            if (pola.Count == 0) lista.Children.Add(Et("Brak dróżników wymagających powiadomienia dla tego pociągu."));
        }
        wszyscy.Click += (a, e) => { foreach (var (x, cb, g) in pola) { cb.IsChecked = true; if (g != null && g.Text.Trim() == "") g.Text = okno.Teraz; } };
        poc.SelectionChanged += (a, e) => Lista(); Lista();
        Przyciski("OK", () =>
        {
            if (poc.SelectedIndex < 0) return new CmdResult(false, "Brak pociągu");
            var w = wiersze[poc.SelectedIndex];
            foreach (var (x, cb, g) in pola)
            {
                var v = cb.IsChecked == true ? (g != null ? g.Text : "\\") : "";
                if (cb.IsChecked == true && g != null && v.Trim() == "") return new CmdResult(false, $"Wpisz godzinę powiadomienia dróżnika {x.Oznaczenie}.");
                if (v == Edr.Wartosc(w.W, "Dr:" + x.Id)) continue;
                var r = okno.Wyslij(new EdrPolecenie { Dziennik = d.Id, Rodzaj = "pole", Wiersz = w.Id, Pole = "Dr:" + x.Id, Wartosc = v });
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

/// <summary>Postawienie pociągu na torze stacyjnym z kontrolki (jak w EDR.exe): tor zajęty do odjazdu tego pociągu wpisanego w dzienniku.</summary>
public class OknoNaTor : OknoEdr
{
    public OknoNaTor(EdrWindow okno, string post, string tor) : base($"Tor stacyjny {tor}", 360)
    {
        Sp.Children.Add(new TextBlock { Text = $"{okno.Dane?.Posterunki.GetValueOrDefault(post, post) ?? post} – tor {tor}", FontWeight = FontWeights.Bold, FontSize = 13 });
        Sp.Children.Add(Et("Numer pociągu:"));
        var nr = new TextBox();
        Sp.Children.Add(nr);
        Sp.Children.Add(Et("Tor będzie zajęty do wpisania „odjechał” w wyprawieniu tego pociągu (albo do usunięcia z kontrolki)."));
        Loaded += (a, e) => nr.Focus();
        Przyciski("Postaw", () => okno.Wyslij(new EdrPolecenie { Rodzaj = "torst", Post = post, Pole = "dodaj", Wartosc = tor, Dane = new EdrWersja { Nr = nr.Text } }));
    }
}

/// <summary>Wykreślenie wpisu z dziennika ruchu (E8d, jak w EDR.exe): przyczyna obowiązkowa, wpis zostaje przekreślony z przyczyną.</summary>
public class OknoWykreslenia : OknoEdr
{
    public OknoWykreslenia(EdrWindow okno, EdrDziennik d, EdrWiersz w) : base("Wykreślenie wpisu", 460)
    {
        Sp.Children.Add(new TextBlock { Text = d.Nazwa, FontWeight = FontWeights.Bold, FontSize = 13 });
        var opis = w.Rodzaj == "wpis" ? $"Wpis: {w.Tekst}" : $"{(w.Rodzaj == "przyj" ? "Przyjęcie" : w.Rodzaj == "wypr" ? "Wyprawienie" : "Powrót ze szlaku")} pociągu {w.W.Nr}"
            + (w.Para != 0 ? " (jazda do km i z powrotem – wykreśla się oba wiersze)" : "");
        Sp.Children.Add(new TextBlock { Text = opis, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
        Sp.Children.Add(Et("Przyczyna wykreślenia:"));
        var t = new TextBox { Height = 60, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
        Sp.Children.Add(t);
        Sp.Children.Add(Et("Wpis zostaje w dzienniku przekreślony, z przyczyną, kto i kiedy wykreślił. Wykreślonego wpisu nie można zmieniać."));
        Loaded += (a, e) => t.Focus();
        Przyciski("Wykreśl", () => okno.Wyslij(new EdrPolecenie { Dziennik = d.Id, Rodzaj = "wykresl", Wiersz = w.Id, Wartosc = t.Text }));
    }
}

/// <summary>Wykonanie sprawdzenia cyklicznego (przycisk w prawym górnym rogu): potwierdzenie z uwagami – zapis w dzienniku telefonicznym.</summary>
public class OknoSprawdzenia : OknoEdr
{
    public OknoSprawdzenia(EdrWindow okno, EdrSprawdzenieStan s) : base("Sprawdzenie cykliczne", 460)
    {
        Sp.Children.Add(new TextBlock { Text = s.Nazwa, FontWeight = FontWeights.Bold, FontSize = 13, TextWrapping = TextWrapping.Wrap });
        Sp.Children.Add(Et($"{s.Opis}; ostatnio: {(s.Ostatnie != "" ? $"{s.Ostatnie} ({s.Kto})" : "nie wykonano")}; termin: {s.Nastepne}{(s.Stan == "po_terminie" ? " – PO TERMINIE (bez wykonania)" : "")}"));
        Sp.Children.Add(Et("Uwagi (np. wynik sprawdzenia):"));
        var u = new TextBox { Height = 60, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
        Sp.Children.Add(u);
        Sp.Children.Add(Et("Zapis „Sprawdzenie: … – wykonane” trafi do dziennika telefonicznego jako inne okoliczności."));
        Przyciski("Wykonano", () => okno.Wyslij(new EdrPolecenie { Rodzaj = "sprawdzenie", Wartosc = s.Post, Pole = s.Id, Dane = new EdrWersja { Uwagi = u.Text } }));
    }
    internal static string Czas(int min) => min % 60 == 0 ? $"{min / 60} h" : min > 60 ? $"{min / 60} h {min % 60} min" : $"{min} min";
}

/// <summary>
/// Konfiguracja wymaganych sprawdzeń posterunku (menu Konfiguracja; E9a jak „Wymagane sprawdzenia” w EDR.exe): rodzaj sprawdzenia, aktywne,
/// godziny sprawdzeń w dobie (liczba sprawdzeń na dobę), sprawdzenie N minut po przejęciu dyżuru. Najwyżej cztery przyciski.
/// </summary>
public class OknoSprawdzenUst : OknoEdr
{
    static readonly string[] Przyklady = { "Sprawdzenie łączności z posterunkami dróżników przejazdowych", "Sprawdzenie oświetlenia semaforów i wskaźników" };

    public OknoSprawdzenUst(EdrWindow okno, string post, string nazwaPost, List<EdrSprawdzenie> lista) : base($"{nazwaPost} – wymagane sprawdzenia", 820)
    {
        var l = lista.Select(x => x.Kopia()).ToList();
        Sp.Children.Add(Et("Sprawdzenia posterunku – przyciski w prawym górnym rogu okna EDR (najwyżej cztery). Termin: godziny sprawdzeń w dobie (GG:MM; GG:MM) i / albo sprawdzenie po przejęciu dyżuru. Przycisk żółknie 15 min przed terminem, po terminie bez wykonania jest czerwony."));
        var tab = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        Sp.Children.Add(tab);
        var pola = new List<(EdrSprawdzenie S, TextBox Nazwa, CheckBox Akt, TextBox Godz, CheckBox Po, TextBox Min)>();
        void Pokaz()
        {
            tab.Children.Clear(); pola.Clear();
            var nag = new Grid();
            foreach (var w in new[] { 300.0, 60, 200, 150, 60, 60 }) nag.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });
            int c0 = 0; foreach (var h in new[] { "Rodzaj sprawdzenia", "Aktywne", "Godziny HH:mm; HH:mm", "Po przejęciu dyżuru (min)", "", "" }) { var t = new TextBlock { Text = h, FontSize = 10.5, Foreground = Brushes.DimGray }; Grid.SetColumn(t, c0++); nag.Children.Add(t); }
            tab.Children.Add(nag);
            foreach (var s in l)
            {
                var r = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                foreach (var w in new[] { 300.0, 60, 200, 30, 120, 60 }) r.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });
                void K(int c, FrameworkElement e) { e.Margin = new Thickness(0, 0, 4, 0); e.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(e, c); r.Children.Add(e); }
                var na = new TextBox { Text = s.Nazwa }; var akt = new CheckBox { IsChecked = s.Aktywne, HorizontalAlignment = HorizontalAlignment.Center };
                var go = new TextBox { Text = s.Godziny != "" || s.Okres > 0 || s.PoDyzurze != null ? s.Godziny : "05:00; 17:00", ToolTip = "Godziny sprawdzeń w dobie, np. 05:00; 17:00 (najwyżej 12)" };
                var po = new CheckBox { IsChecked = s.PoDyzurze != null, HorizontalAlignment = HorizontalAlignment.Center, ToolTip = "Sprawdzenie po przejęciu dyżuru" };
                var mi = new TextBox { Text = (s.PoDyzurze ?? 5).ToString(), ToolTip = "Ilość minut, po których nastąpi sprawdzenie po przejęciu dyżuru" };
                var us = new Button { Content = "Usuń", Padding = new Thickness(6, 0, 6, 0) };
                var ss = s; us.Click += (a, e) => { Zbierz(); l.Remove(ss); Pokaz(); };
                K(0, na); K(1, akt); K(2, go); K(3, po); K(4, mi); K(5, us);
                tab.Children.Add(r); pola.Add((s, na, akt, go, po, mi));
            }
            if (l.Count == 0) tab.Children.Add(new TextBlock { Text = "(brak – przyciski nie są wyświetlane)", Foreground = Brushes.DimGray });
        }
        void Zbierz()
        {
            foreach (var (s, na, akt, go, po, mi) in pola)
            {
                s.Nazwa = na.Text.Trim(); s.Aktywne = akt.IsChecked == true; s.Godziny = go.Text.Trim(); s.Okres = 0;
                s.PoDyzurze = po.IsChecked == true ? int.TryParse(mi.Text, out var m) ? m : -1 : null;
            }
        }
        var dod = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        var nowy = new Button { Content = "Utwórz nowy", Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(0, 0, 6, 4) };
        nowy.Click += (a, e) => { Zbierz(); if (l.Count < 4) { l.Add(new EdrSprawdzenie { Nazwa = "", Godziny = "05:00; 17:00" }); Pokaz(); } };
        dod.Children.Add(nowy);
        foreach (var p in Przyklady)
        {
            var b = new Button { Content = "+ " + p, Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(0, 0, 6, 4), FontSize = 11 };
            var pp = p; b.Click += (a, e) => { Zbierz(); if (l.Count < 4 && !l.Any(x => x.Nazwa == pp)) { l.Add(new EdrSprawdzenie { Nazwa = pp, Godziny = "05:00; 17:00" }); Pokaz(); } };
            dod.Children.Add(b);
        }
        Sp.Children.Add(dod);
        Pokaz();
        Przyciski("Zapisz", () => { Zbierz(); return okno.Wyslij(new EdrPolecenie { Rodzaj = "sprawdzenia", Wartosc = post, Sprawdzenia = l }); });
    }
}

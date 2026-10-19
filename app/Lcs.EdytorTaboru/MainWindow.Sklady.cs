using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Lcs.Tabor;

namespace Lcs.EdytorTaboru;

/// <summary>
/// Zakładka „Składy”: lista składów, pojazdy po kolei (seria, liczba, załadowanie), nastawienie hamulca, sumy i krótki podgląd jazdy
/// (ten sam kod fizyki co symulator – <see cref="Jazda"/>).
/// </summary>
public partial class MainWindow
{
    public int SkladIdx = -1;
    /// <summary>Numer przebudowy wierszy pojazdów: pole ze starego (odłączonego) wiersza nie zapisuje już nic przy utracie fokusu.</summary>
    int pozycjeGen;
    public readonly ListBox SkladList = new() { Width = 300, Margin = new Thickness(0, 0, 8, 0) };
    public Sklad Sklad => SkladIdx >= 0 && SkladIdx < Doc.P.Sklady.Count ? Doc.P.Sklady[SkladIdx] : null;
    readonly StackPanel skladForm = new() { Margin = new Thickness(8) };
    public TextBox SkladIdBox, SkladNazwaBox, SkladUwagiBox;
    public readonly ComboBox NastawienieBox = new() { Width = 260, Margin = new Thickness(0, 2, 8, 2) };
    /// <summary>Wiersze pojazdów składu (przebudowywane przy odświeżeniu).</summary>
    public readonly StackPanel PozycjePanel = new() { Margin = new Thickness(0, 2, 0, 6) };
    public readonly TextBlock Sumy = new() { FontFamily = new FontFamily("Consolas"), Margin = new Thickness(0, 4, 0, 4), TextWrapping = TextWrapping.Wrap };
    static readonly (string Key, string Name)[] Nastawienia = { ("G", "G – towarowy (wolne napełnianie)"), ("P", "P – osobowy"), ("R", "R – pospieszny (duża masa hamująca)") };

    UIElement BuildSklady()
    {
        var left = new DockPanel();
        var btns = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        btns.Children.Add(Btn("+ skład", (s, e) => DodajSklad(), "Nowy skład – pojazdy dodajesz w formularzu"));
        btns.Children.Add(Btn("Kopiuj", (s, e) => KopiujSklad(), "Kopia zaznaczonego składu"));
        btns.Children.Add(Btn("Usuń", (s, e) => UsunSklad(), "Usuń skład (Ctrl+Z przywraca); rozkład z tym składem pojedzie w ruchu uproszczonym"));
        left.Children.Add(Dock(btns, System.Windows.Controls.Dock.Top));
        left.Children.Add(SkladList);
        SkladList.SelectionChanged += (s, e) =>
        {
            if (refreshing || SkladList.SelectedIndex < 0) return;
            CommitPending();
            SkladIdx = SkladList.SelectedIndex; FillSklad();
        };

        var g = skladForm;
        g.Children.Add(Header("Skład"));
        SkladIdBox = Pole(t => ZmienIdSkladu(t), 200, "Identyfikator składu – pole „sklad” w rozkładzie (<układ>.rozklad.json)");
        SkladNazwaBox = Pole(t => ChS("Nazwa składu", s => s.Nazwa = t.Trim()), 360);
        Wiersz(g, "Id (w rozkładzie)", SkladIdBox, "Nazwa", SkladNazwaBox);
        foreach (var n in Nastawienia) NastawienieBox.Items.Add(n.Name);
        NastawienieBox.SelectionChanged += (s, e) => { if (!refreshing && NastawienieBox.SelectedIndex >= 0) { var k = Nastawienia[NastawienieBox.SelectedIndex].Key; ChS("Nastawienie hamulca", x => x.Nastawienie = k); } };
        Wiersz(g, "Nastawienie hamulca", NastawienieBox);
        SkladUwagiBox = Pole(t => ChS("Uwagi składu", s => s.Uwagi = t.Trim()), 560);
        Wiersz(g, "Uwagi", SkladUwagiBox);
        g.Children.Add(Header("Pojazdy od czoła"));
        g.Children.Add(new TextBlock { Text = "Seria z katalogu, liczba (np. 2 × EN57 w trakcji wielokrotnej), załadowanie w % (wagony towarowe: 100 – ładowny; pasażerskie – zapełnienie).", Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap, MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left });
        g.Children.Add(PozycjePanel);
        var dodaj = Btn("+ pojazd do składu", (s, e) => DodajPozycje(), "Dopisz pojazd na końcu składu"); dodaj.HorizontalAlignment = HorizontalAlignment.Left;
        g.Children.Add(dodaj);
        g.Children.Add(Header("Sumy i podgląd jazdy"));
        g.Children.Add(Sumy);
        BuildPodglad(g);

        var dock = new DockPanel { Margin = new Thickness(8) };
        dock.Children.Add(Dock(left, System.Windows.Controls.Dock.Left));
        dock.Children.Add(new ScrollViewer { Content = skladForm, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        return dock;
    }

    void ChS(string what, Action<Sklad> act)
    {
        if (Sklad == null) return;
        int i = SkladIdx; var id = Sklad.Id;
        Doc.Change($"{what} ({id})", p => act(p.Sklady[i]));
    }

    void ZmienIdSkladu(string t)
    {
        if (Sklad == null) return;
        var nowe = t.Trim(); var stare = Sklad.Id;
        if (nowe == stare) return;
        if (nowe.Length == 0 || Doc.P.Sklady.Any(s => s.Id == nowe)) { Message(nowe.Length == 0 ? "Id składu nie może być puste" : $"Skład {nowe} już jest"); SkladIdBox.Text = stare; return; }
        int i = SkladIdx;
        Doc.Change($"Id składu {stare} → {nowe}", p =>
        {
            p.Sklady[i].Id = nowe;
            foreach (var k in p.Domyslne.Where(kv => kv.Value == stare).Select(kv => kv.Key).ToList()) p.Domyslne[k] = nowe;
        });
        Message($"Skład: {nowe} – popraw pole „sklad” w rozkładach, które używały „{stare}”");
    }

    void RefreshSklady()
    {
        SkladList.Items.Clear();
        foreach (var s in Doc.P.Sklady) SkladList.Items.Add(new TextBlock { Text = $"{s.Id}{(s.Nazwa.Length > 0 && s.Nazwa != s.Id ? " – " + s.Nazwa : "")}" });
        SkladList.SelectedIndex = SkladIdx;
        FillSklad();
    }

    void FillSklad()
    {
        bool was = refreshing; refreshing = true;
        try
        {
            var s = Sklad;
            skladForm.IsEnabled = s != null;
            PozycjePanel.Children.Clear(); pozycjeGen++;
            if (s == null) { Sumy.Text = ""; return; }
            Set(SkladIdBox, s.Id); Set(SkladNazwaBox, s.Nazwa); Set(SkladUwagiBox, s.Uwagi);
            NastawienieBox.SelectedIndex = Array.FindIndex(Nastawienia, n => n.Key == s.Nastawienie);
            for (int i = 0; i < s.Pojazdy.Count; i++) PozycjePanel.Children.Add(Pozycja(i, s.Pojazdy[i]));
            Sumy.Text = SumyTekst(s);
            WykresySkladu(s);
        }
        finally { refreshing = was; }
    }

    UIElement Pozycja(int i, PozycjaSkladu x)
    {
        int gen = pozycjeGen; var skId = Sklad.Id;
        // zmiana wiersza: tylko z bieżącego wiersza tego samego składu
        void ChW(string what, Action<Sklad> act) { if (gen == pozycjeGen && Sklad?.Id == skId && i < Sklad.Pojazdy.Count) ChS(what, act); }
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
        row.Children.Add(new TextBlock { Text = $"{i + 1}.", Width = 24, VerticalAlignment = VerticalAlignment.Center });
        var cb = new ComboBox { Width = 160, Margin = new Thickness(0, 0, 8, 0) };
        foreach (var p in Doc.P.Pojazdy) cb.Items.Add(p.Seria);
        if (Doc.P.Pojazd(x.Seria) == null) cb.Items.Add(x.Seria);   // seria spoza katalogu – widoczna, „Sprawdź” ją wskaże
        cb.SelectedItem = x.Seria;
        cb.SelectionChanged += (s, e) => { if (!refreshing && cb.SelectedItem is string v) ChW("Seria w składzie", q => q.Pojazdy[i].Seria = v); };
        row.Children.Add(cb);
        row.Children.Add(Label("×"));
        TextBox lb = null, zb = null;
        lb = Pole(t => { if (!int.TryParse(t.Trim(), out var n) || n < 1 || n > 200) { Message("Liczba pojazdów: od 1 do 200"); lb.Text = x.Liczba.ToString(); return; } ChW("Liczba pojazdów", q => q.Pojazdy[i].Liczba = n); }, 50);
        lb.Text = x.Liczba.ToString();
        row.Children.Add(lb);
        row.Children.Add(Label("załadowanie [%]"));
        zb = Pole(t => { if (!TryNum(t, out var v) || v < 0 || v > 100) { Message("Załadowanie: od 0 do 100 %"); zb.Text = F(x.Zaladowanie * 100); return; } ChW("Załadowanie", q => q.Pojazdy[i].Zaladowanie = Math.Round(v / 100, 4)); }, 50);
        zb.Text = F(x.Zaladowanie * 100);
        row.Children.Add(zb);
        row.Children.Add(Btn("↑", (s, e) => Przesun(i, -1), "W stronę czoła"));
        row.Children.Add(Btn("↓", (s, e) => Przesun(i, 1), "W stronę końca"));
        row.Children.Add(Btn("✕", (s, e) => { CommitPending(); ChW("Usunięcie pojazdu ze składu", q => q.Pojazdy.RemoveAt(i)); }, "Usuń z składu"));
        return row;
    }

    void Przesun(int i, int d)
    {
        CommitPending();
        int j = i + d; if (Sklad == null || j < 0 || j >= Sklad.Pojazdy.Count) return;
        ChS("Kolejność pojazdów", q => { var x = q.Pojazdy[i]; q.Pojazdy.RemoveAt(i); q.Pojazdy.Insert(j, x); });
    }

    public void DodajPozycje()
    {
        if (Sklad == null || Doc.P.Pojazdy.Count == 0) { Message("Najpierw dodaj pojazdy do katalogu"); return; }
        CommitPending();
        var seria = Sklad.Pojazdy.LastOrDefault()?.Seria ?? Doc.P.Pojazdy[0].Seria;
        ChS("Pojazd w składzie", q => q.Pojazdy.Add(new PozycjaSkladu { Seria = seria }));
    }

    public void DodajSklad()
    {
        CommitPending();
        var id = "Nowy skład"; for (int n = 2; Doc.P.Sklad(id) != null; n++) id = $"Nowy skład {n}";
        int at = SkladIdx + 1;
        Doc.Change("Nowy skład", p => p.Sklady.Insert(at, new Sklad { Id = id }));
        SkladIdx = at; Refresh();
        Message($"Nowy skład „{id}” – wpisz id i dodaj pojazdy");
    }

    public void KopiujSklad()
    {
        if (Sklad == null) return;
        CommitPending();
        var k = TaborPlik.Deserialize(TaborPlik.Serialize(new TaborPlik { Sklady = { Sklad } })).Sklady[0];
        var id = Sklad.Id + " (kopia)"; for (int n = 2; Doc.P.Sklad(id) != null; n++) id = $"{Sklad.Id} (kopia {n})";
        k.Id = id; int at = SkladIdx + 1;
        Doc.Change("Kopia składu", p => p.Sklady.Insert(at, k));
        SkladIdx = at; Refresh();
    }

    public void UsunSklad()
    {
        if (Sklad == null) return;
        CommitPending();
        int i = SkladIdx; var id = Sklad.Id;
        Doc.Change($"Usunięcie składu {id}", p => p.Sklady.RemoveAt(i));
        Message($"Usunięto skład {id} (Ctrl+Z przywraca)");
    }

    /// <summary>Sumy składu i podgląd jazdy: rozpędzanie, droga hamowania, ruszanie na wzniesieniu – kod fizyki symulatora.</summary>
    public string SumyTekst(Sklad s)
    {
        if (s.Pojazdy.Count == 0) return "Skład pusty – dodaj pojazdy.";
        var brak = s.Pojazdy.Where(x => Doc.P.Pojazd(x.Seria) == null).Select(x => x.Seria).Distinct().ToList();
        if (brak.Count > 0) return "Nie ma w katalogu: " + string.Join(", ", brak);
        var p = new Pociag(s, Doc.P);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Długość {F(Math.Round(p.Dlugosc, 1))} m   masa {F(Math.Round(p.Masa, 1))} t   masa hamująca {F(Math.Round(p.MasaHamujaca, 1))} t   λ {p.Lambda:0} % ({p.Nastawienie})   Vmax {F(p.VMax)} km/h");
        bool siec = p.Elementy.Any(e => e.P.Napedny && Pociag.ZrodloDla(e.P, true) != null);
        double f0 = p.SilaMax(0, siec);
        if (f0 <= 0) { sb.AppendLine("Brak siły pociągowej – skład bez pojazdu napędnego albo bez charakterystyki."); return sb.ToString(); }
        sb.AppendLine($"Siła przy ruszaniu {f0:0} kN, przyspieszenie przy ruszaniu na poziomie {(f0 - p.Opor(0)) / p.MasaZastepcza:0.00} m/s²");
        double vr = Math.Min(p.VMax, 100);
        string R(double i) { var r = Jazda.Rozpedzanie(p, vr, i, siec); return r.T is double t ? $"{t:0} s / {r.S:0} m" : r.VOsiagnieta > 0 ? $"nie osiąga ({r.VOsiagnieta:0} km/h)" : "nie rusza"; }
        sb.AppendLine($"Rozpędzanie 0 → {vr:0} km/h: poziom {R(0)}, wzniesienie 10 ‰ {R(10)}");
        string H(double i) { var d = Jazda.DrogaHamowania(p, vr, 0, i, 1, siec); return double.IsInfinity(d) ? "nie zatrzyma" : $"{d:0} m"; }
        sb.AppendLine($"Hamowanie nagłe {vr:0} → 0: poziom {H(0)}, spadek 10 ‰ {H(-10)}");
        sb.AppendLine($"Rusza na wzniesieniu: 5 ‰ {(p.MozeRuszyc(5, siec) ? "tak" : "nie")}, 10 ‰ {(p.MozeRuszyc(10, siec) ? "tak" : "nie")}, 20 ‰ {(p.MozeRuszyc(20, siec) ? "tak" : "nie")}");
        if (p.Elementy.Any(e => e.P.Rodzaj == Rodzaj.Bzt))
        {
            var d = Jazda.Rozpedzanie(p, Math.Min(vr, 80), 0, false);
            sb.AppendLine($"BZT bez sieci (spalinowy): 0 → {Math.Min(vr, 80):0} km/h {(d.T is double t ? $"{t:0} s" : "nie osiąga")}");
        }
        return sb.ToString();
    }
}

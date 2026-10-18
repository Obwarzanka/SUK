using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorZobrazowania;

/// <summary>
/// Etap Z4 w oknie (PLAN_EDYTOR_ZOBRAZOWANIA.md § 4, decyzje § 10): strzałki blokady przy końcach szlaków, opisy odcinków, perony
/// (prostokąt przypięty do peronu UT), napisy, zasięg linii przejazdu, ustawienie opisów odcinków na pulpicie, tor bez kontroli niezajętości.
/// </summary>
public partial class MainWindow
{
    /// <summary>Peron UT do narysowania narzędziem Peron.</summary>
    public string PeronCurrent { get; private set; }
    /// <summary>Napis do wstawienia bez okna (testy): tekst i rodzaj.</summary>
    public (string Tekst, string Rodzaj)? NextNapis;
    public CheckBox BezKontroliBox { get; private set; }
    public ComboBox OpisyBox { get; private set; }
    public TextBox NapisBox { get; private set; }

    void InitZ4()
    {
        View.PeronDrawn += r => { if (PeronCurrent == null) PickPeron(null); if (PeronCurrent != null) PlacePeron(PeronCurrent, r); };
        View.NapisClick += OnNapisClick;
        View.ItemMoved += (k, id, dx, dy) =>
        {
            if (k == "peron") Ch("Przesunięcie peronu", o => { var p = o.Perony.FirstOrDefault(x => x.Ut == id); if (p != null) p.Prost = new[] { p.Prost[0] + dx, p.Prost[1] + dy, p.Prost[2], p.Prost[3] }; });
            else Ch("Przesunięcie napisu", o => { var n = o.Napisy.FirstOrDefault(x => x.Id == id); if (n != null) n.P = new[] { n.P[0] + dx, n.P[1] + dy }; });
        };
    }

    static string Klawisz(ZobRodzaj r) => r switch
    {
        ZobRodzaj.Zwrotnica => "Z", ZobRodzaj.Sygnalizator => "S", ZobRodzaj.Wykolejnica => "W", ZobRodzaj.Kozioł => "K", ZobRodzaj.Przerwa => "P",
        ZobRodzaj.OpisOdcinka => "O", ZobRodzaj.NumerToru => "N", ZobRodzaj.Szlak => "B", _ => null,
    };

    /// <summary>Koniec szlaku, przy którym stawiana jest strzałka: koniec przy posterunku obrazu, jeszcze bez strzałki, najbliższy kliknięciu.</summary>
    int? KoniecSzlaku(string ut, int[] p)
    {
        var l = Doc.Ut.Lines.FirstOrDefault(x => x.Id == ut); if (l == null) return null;
        var konce = ZobUt.KonceNaObrazie(Doc.Ut, Obraz, l);
        if (konce.Count == 0) { Message($"Szlak {l.Numer}: żaden koniec nie jest przy semaforze wjazdowym posterunku tego obrazu – strzałka stoi przy posterunku"); return null; }
        double Dist(int k)
        {
            var sem = Obraz.Sygnalizatory.FirstOrDefault(x => x.Ut == l.Konce[k].Semafor);
            return sem == null ? 1e9 : Math.Abs(sem.P[0] - p[0]) + Math.Abs(sem.P[1] - p[1]);
        }
        var wolne = konce.Where(k => !Obraz.Szlaki.Any(x => x.Ut == ut && (x.Koniec ?? 0) == k)).ToList();
        return (wolne.Count > 0 ? wolne : konce).OrderBy(Dist).First();
    }

    /// <summary>Opis odcinka: w klikniętym punkcie siatki, przy torze obrazu toru odcinka (do 48 j.).</summary>
    bool PlaceOpis(string ut, int[] p)
    {
        var sc = Doc.Ut.Sections.FirstOrDefault(x => x.Id == ut); if (sc == null) { Message("Odcinka nie ma w UT"); return false; }
        string trNr = Doc.Ut.Tracks.FirstOrDefault(t => t.Id == sc.Track)?.Number ?? "?";
        var kaw = ZobSymbole.Kawalki(Obraz, sc.Track, double.NaN).ToList();
        if (kaw.Count == 0) { Message($"Opis odcinka {sc.Name}: najpierw narysuj tor {trNr} (narzędzie Tor)"); return false; }
        if (!kaw.Any(t => ZobTory.Rzut(t.Pkt, p[0], p[1]).Dist <= 48)) { Message($"Opis odcinka {sc.Name}: kliknij przy torze {trNr} (do 48 j. od linii, zwykle nad torem – Ie-104 § 8 ust. 1)"); return false; }
        var sym = new ZobSymbol { Ut = ut, P = p };
        bool was = Obraz.Odcinki.Any(x => x.Ut == ut);
        Ch(was ? "Przestawienie opisu odcinka" : "Opis odcinka na obrazie", o => { o.Odcinki.RemoveAll(x => x.Ut == ut); o.Odcinki.Add(sym); });
        PickSym(ZobRodzaj.OpisOdcinka);
        Message($"Opis odcinka {sc.Name} na obrazie ({p[0]}, {p[1]})" + (SymCurrent != null ? $". Następny: {SymOpis(SymCurrent)}" : ". Wszystkie opisy odcinków są na obrazie"));
        return true;
    }

    /// <summary>Zasięg linii przejazdu w górę i w dół od toru [j.].</summary>
    void PrzejazdZasieg(StackPanel P, ZobSymbol s)
    {
        P.Children.Add(Label("Linia przejazdu – zasięg od toru w górę / w dół [j.]"));
        var up = new TextBox { Width = 60, Margin = new Thickness(0, 2, 6, 6), Text = (s.Zasieg?[0] ?? 28).ToString(CultureInfo.InvariantCulture) };
        var dn = new TextBox { Width = 60, Margin = new Thickness(0, 2, 0, 6), Text = (s.Zasieg?[1] ?? 28).ToString(CultureInfo.InvariantCulture) };
        void Commit()
        {
            if (!int.TryParse(up.Text.Trim(), out int a) || !int.TryParse(dn.Text.Trim(), out int b) || a < 16 || b < 16 || a > 2000 || b > 2000)
            { Message("Zasięg przejazdu: liczby całkowite od 16 do 2000 j."); return; }
            if (a != (s.Zasieg?[0] ?? 28) || b != (s.Zasieg?[1] ?? 28)) ChSym(ZobRodzaj.Przejazd, s.Ut, "Zasięg przejazdu", x => x.Zasieg = new[] { a, b });
        }
        foreach (var box in new[] { up, dn }) { box.LostFocus += (a, e) => Commit(); box.KeyDown += (a, e) => { if (e.Key == Key.Enter) Commit(); }; }
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(up); row.Children.Add(new TextBlock { Text = "/", Margin = new Thickness(0, 4, 6, 0) }); row.Children.Add(dn);
        P.Children.Add(row);
        PrzejazdBoxes = (up, dn);
    }
    public (TextBox Up, TextBox Down)? PrzejazdBoxes { get; private set; }

    // ------------------------------------------------------------------ perony
    void PickPeron(string prefer)
    {
        var all = ZobUt.Obiekty(Doc.Ut, Obraz, true).Where(x => x.Grupa == ZobUt.GPerony).ToList();
        PeronCurrent = all.FirstOrDefault(x => x.Id == prefer)?.Id ?? all.FirstOrDefault(x => !x.NaObrazie && !x.Obcy)?.Id;
        Message(PeronCurrent == null ? (all.Count == 0 ? "Peron: w UT obrazu nie ma peronów" : "Peron: wszystkie są już na obrazie (wybierz w panelu UT, żeby narysować ponownie)")
            : $"Peron: {all.First(x => x.Id == PeronCurrent).Opis} – przeciągnij prostokąt peronu (do siatki)");
    }

    public bool PlacePeron(string ut, int[] rect)
    {
        var pl = Doc.Ut.Platforms.FirstOrDefault(x => x.Id == ut); if (pl == null) { Message("Peronu nie ma w UT"); return false; }
        var pr = new ZobPeron { Ut = ut, Prost = rect };
        bool was = Obraz.Perony.Any(x => x.Ut == ut);
        Ch(was ? "Peron narysowany ponownie" : "Peron na obrazie", o => { o.Perony.RemoveAll(x => x.Ut == ut); o.Perony.Add(pr); });
        var name = pl.Name;
        PickPeron(null);
        Message($"Peron {name} na obrazie ({rect[0]}, {rect[1]}, {rect[2]} × {rect[3]})" + (PeronCurrent != null ? ". Następny: peron " + Doc.Ut.Platforms.First(x => x.Id == PeronCurrent).Name : ""));
        return true;
    }

    // ------------------------------------------------------------------ napisy
    void OnNapisClick(int[] p)
    {
        (string, string)? v = NextNapis;
        NextNapis = null;
        if (v == null && !NoPrompt) v = AskNapis();
        if (v is not var (tekst, rodzaj) || string.IsNullOrWhiteSpace(tekst)) { Message("Napis: bez tekstu – nic nie wstawiono"); return; }
        AddNapis(tekst.Trim(), rodzaj, p);
    }

    public ZobNapis AddNapis(string tekst, string rodzaj, int[] p)
    {
        var n = new ZobNapis { Tekst = tekst, Rodzaj = rodzaj, P = p };
        Ch("Napis", o => o.Napisy.Add(n));
        View.SelectItem("napis", n.Id);
        Message($"Napis „{tekst}” w ({p[0]}, {p[1]})");
        return n;
    }

    /// <summary>Okno tekstu napisu: tekst (podpowiedzi – posterunki sąsiednie ze szlaków UT) i rodzaj.</summary>
    (string, string)? AskNapis()
    {
        var w = new Window { Title = "Napis", Width = 420, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, ResizeMode = ResizeMode.NoResize };
        var sp = new StackPanel { Margin = new Thickness(12) };
        sp.Children.Add(new TextBlock { Text = "Tekst" });
        var tb = new ComboBox { IsEditable = true, Margin = new Thickness(0, 2, 0, 8) };
        foreach (var s in Doc.Ut.Lines.SelectMany(l => l.Konce).Select(e => e.Sasiad).Where(s => s.Length > 0).Distinct()) tb.Items.Add(s);
        foreach (var x in Doc.Ut.Posts) tb.Items.Add(x.Kod);
        sp.Children.Add(tb);
        sp.Children.Add(new TextBlock { Text = "Rodzaj" });
        var cb = new ComboBox { Margin = new Thickness(0, 2, 0, 8) };
        foreach (var r in ZobNapis.Rodzaje) cb.Items.Add(r.Name);
        cb.SelectedIndex = 0;
        sp.Children.Add(cb);
        var ok = new Button { Content = "OK", IsDefault = true, Width = 80, HorizontalAlignment = HorizontalAlignment.Right };
        ok.Click += (s, e) => w.DialogResult = true;
        sp.Children.Add(ok);
        w.Content = sp;
        tb.Focus();
        return w.ShowDialog() == true ? (tb.Text, ZobNapis.Rodzaje[Math.Max(0, cb.SelectedIndex)].Key) : null;
    }

    // ------------------------------------------------------------------ zaznaczone: peron, napis
    public void DeleteItem()
    {
        if (View.SelectedPeron is string pid)
        {
            Ch("Zdjęcie peronu z obrazu", o => o.Perony.RemoveAll(x => x.Ut == pid));
            Message("Peron zdjęty z obrazu (w UT zostaje; Ctrl+Z przywraca)");
        }
        else if (View.SelectedNapis is string nid)
        {
            Ch("Usunięcie napisu", o => o.Napisy.RemoveAll(x => x.Id == nid));
            Message("Napis usunięty (Ctrl+Z przywraca)");
        }
        View.Select(null);
    }

    void BuildZ4Panel()
    {
        NapisBox = null;
        var P = ImgPanel;
        if (View.SelectedPeron is string pid && Obraz.Perony.FirstOrDefault(x => x.Ut == pid) is ZobPeron pr)
        {
            var pl = Doc.Ut.Platforms.FirstOrDefault(x => x.Id == pid);
            P.Children.Add(Header("Peron na obrazie"));
            P.Children.Add(new TextBlock { Text = pl == null ? "peronu nie ma w UT" : $"peron {pl.Name} – tor {Doc.Ut.Tracks.FirstOrDefault(t => t.Id == pl.Track)?.Number ?? "?"}, km {Math.Min(pl.S0, pl.S1):0}–{Math.Max(pl.S0, pl.S1):0}, krawędź {pl.Len:0} m", TextWrapping = TextWrapping.Wrap });
            P.Children.Add(new TextBlock { Text = $"Prostokąt ({pr.Prost[0]}, {pr.Prost[1]}), {pr.Prost[2]} × {pr.Prost[3]} j. – przesuwasz myszą (Wskaż), nowy rozmiar: narzędzie Peron.", Foreground = Brushes.DimGray, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 6) });
            P.Children.Add(Btn("Zdejmij z obrazu", (s, e) => DeleteItem(), "Zdejmij peron z obrazu (Del); w UT zostaje"));
            P.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 10) });
        }
        if (View.SelectedNapis is string nid && Obraz.Napisy.FirstOrDefault(x => x.Id == nid) is ZobNapis n)
        {
            P.Children.Add(Header("Napis"));
            P.Children.Add(Label("Tekst"));
            var tb = new TextBox { Text = n.Tekst, Margin = new Thickness(0, 2, 0, 8) };
            void Commit() { var t = tb.Text.Trim(); if (t.Length > 0 && t != n.Tekst) Ch("Tekst napisu", o => o.Napisy.First(x => x.Id == nid).Tekst = t); }
            tb.LostFocus += (s, e) => Commit(); tb.KeyDown += (s, e) => { if (e.Key == Key.Enter) Commit(); };
            P.Children.Add(tb); NapisBox = tb;
            P.Children.Add(Label("Rodzaj"));
            var cb = new ComboBox { Margin = new Thickness(0, 2, 0, 8) };
            foreach (var r in ZobNapis.Rodzaje) cb.Items.Add(r.Name);
            cb.SelectedIndex = Array.FindIndex(ZobNapis.Rodzaje, r => r.Key == n.Rodzaj);
            cb.SelectionChanged += (s, e) => { if (!refreshing && cb.SelectedIndex >= 0) { var k = ZobNapis.Rodzaje[cb.SelectedIndex].Key; Ch("Rodzaj napisu", o => o.Napisy.First(x => x.Id == nid).Rodzaj = k); } };
            P.Children.Add(cb);
            P.Children.Add(Btn("Usuń napis", (s, e) => DeleteItem(), "Usuń napis (Del)"));
            P.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 10) });
        }
    }

    /// <summary>Panel obrazu: opisy odcinków na pulpicie (decyzja § 10 pkt 2).</summary>
    void OpisyCombo(ZobObraz o)
    {
        ImgPanel.Children.Add(Label("Opisy odcinków na pulpicie"));
        var cb = new ComboBox { Margin = new Thickness(0, 2, 0, 8), ToolTip = "Ie-104 § 5 ust. 2: oznaczenia odcinków wyświetla się na żądanie dyżurnego ruchu" };
        foreach (var r in ZobObraz.OpisyRodzaje) cb.Items.Add(r.Name);
        cb.SelectedIndex = Math.Max(0, Array.FindIndex(ZobObraz.OpisyRodzaje, r => r.Key == o.OpisyOdcinkow));
        cb.SelectionChanged += (s, e) => { if (!refreshing && cb.SelectedIndex >= 0) { var k = ZobObraz.OpisyRodzaje[cb.SelectedIndex].Key; Ch("Opisy odcinków na pulpicie", x => x.OpisyOdcinkow = k); } };
        ImgPanel.Children.Add(cb);
        OpisyBox = cb;
    }
}

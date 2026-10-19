using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Lcs.Tabor;
using Microsoft.Win32;
using Point = System.Windows.Point;

namespace Lcs.EdytorTaboru;

/// <summary>
/// Etap B4 (PLAN_EDYTOR_TABORU.md § 8): wykresy składu – siła pociągowa i opory na pochyleniach (przecięcie = prędkość równowagi), jazda v(s) / v(t)
/// (rozpędzanie i hamowanie), przejazd próbny po profilu z tekstu albo z układu symulatora – ten sam maszynista i fizyka co w symulatorze.
/// </summary>
public partial class MainWindow
{
    public readonly Wykres WykresSily = new() { Width = 900, HorizontalAlignment = HorizontalAlignment.Left, Tytul = "Siła pociągowa i opory składu", OsX = "v [km/h]", OsY = "F, W [kN]" };
    public readonly Wykres WykresJazdy = new() { Width = 900, HorizontalAlignment = HorizontalAlignment.Left, Tytul = "Jazda", OsY = "v [km/h]" };
    public readonly Wykres WykresPrzejazdu = new() { Width = 900, HorizontalAlignment = HorizontalAlignment.Left, Tytul = "Przejazd próbny", OsX = "droga [m]", OsY = "v [km/h]" };
    public readonly Wykres WykresProfilu = new() { Width = 900, Height = 140, HorizontalAlignment = HorizontalAlignment.Left, Tytul = "Profil (pochylenie)", OsX = "droga [m]", OsY = "‰" };
    public readonly ComboBox OsJazdy = new() { Width = 160, Margin = new Thickness(0, 2, 8, 2) };
    public readonly TextBox ProfilBox = new()
    {
        AcceptsReturn = true, Height = 130, Width = 300, FontFamily = new FontFamily("Consolas"), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalAlignment = HorizontalAlignment.Left,
        Text = "# długość [m]  pochylenie [‰]  Vmax [km/h]  sieć (1/0)\n1000 0 100 1\n3000 8 100 1\n2000 -6 100 1\n500 0 60 1",
    };
    public readonly TextBlock PrzejazdWynik = new() { FontFamily = new FontFamily("Consolas"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4) };
    /// <summary>Ostatni przejazd próbny (test).</summary>
    public Przejazd.Wynik OstatniPrzejazd;

    void BuildPodglad(StackPanel g)
    {
        g.Children.Add(WykresSily);
        OsJazdy.Items.Add("v od drogi – v(s)"); OsJazdy.Items.Add("v od czasu – v(t)"); OsJazdy.SelectedIndex = 0;
        OsJazdy.SelectionChanged += (s, e) => { if (Sklad != null) WykresySkladu(Sklad); };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 2) };
        row.Children.Add(Label("Wykres jazdy:")); row.Children.Add(OsJazdy);
        row.Children.Add(new TextBlock { Text = "rozpędzanie na poziomie i 10 ‰, hamowanie nagłe na poziomie i na spadku 10 ‰", Foreground = Brushes.DimGray, VerticalAlignment = VerticalAlignment.Center });
        g.Children.Add(row);
        g.Children.Add(WykresJazdy);

        g.Children.Add(Header("Przejazd próbny"));
        g.Children.Add(new TextBlock { Text = "Pociąg rusza na początku profilu i staje na końcu; prowadzi automatyczny maszynista symulatora (zapas 3 km/h, hamowanie służbowe, pochylenie na drodze hamowania). Profil z układu: tory i pochylenia z pliku symulatora, bez ograniczeń na zwrotnicach i bez sygnałów.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left });
        var pr = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
        pr.Children.Add(ProfilBox);
        var bp = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
        bp.Children.Add(Btn("Przejedź", (s, e) => Przejedz(), "Przejazd próbny wybranego składu po profilu"));
        bp.Children.Add(new Border { Height = 6 });
        bp.Children.Add(Btn("Profil z układu…", (s, e) => ProfilZUkladuOkno(), "Tor z pliku układu symulatora (LcsWidzew/Data/projekty/*.json) – pochylenia, prędkości, sieć"));
        pr.Children.Add(bp);
        g.Children.Add(pr);
        g.Children.Add(PrzejazdWynik);
        g.Children.Add(WykresPrzejazdu);
        g.Children.Add(WykresProfilu);
    }

    static readonly Color[] KoloryPoch = { Color.FromRgb(0x50, 0x90, 0x50), Color.FromRgb(0xa0, 0x90, 0x20), Color.FromRgb(0xc0, 0x60, 0x20), Color.FromRgb(0xb0, 0x20, 0x20) };

    /// <summary>Wykres siły i oporów oraz wykres jazdy dla składu (puste, gdy skład nie daje się policzyć).</summary>
    void WykresySkladu(Sklad s)
    {
        WykresSily.Serie.Clear(); WykresJazdy.Serie.Clear();
        Pociag p = null;
        try { if (s.Pojazdy.Count > 0 && s.Pojazdy.All(x => Doc.P.Pojazd(x.Seria) != null)) p = new Pociag(s, Doc.P); } catch { }
        if (p == null) { WykresSily.Odswiez(); WykresJazdy.Odswiez(); return; }
        double vmax = Math.Max(10, p.VMax);
        bool siec = p.Elementy.Any(e => e.P.Napedny && Pociag.ZrodloDla(e.P, true) != null);
        var f = new Wykres.Seria { Nazwa = siec ? "siła pociągowa (sieć)" : "siła pociągowa", Kolor = KolorSiec, Grubosc = 2.2 };
        for (double v = 0; v <= vmax + 1e-9; v += 1) f.Punkty.Add(new Point(v, p.SilaMax(v, siec)));
        WykresSily.Serie.Add(f);
        if (p.Elementy.Any(e => e.P.Rodzaj == Rodzaj.Bzt))
        {
            var fs = new Wykres.Seria { Nazwa = "siła pociągowa (bez sieci)", Kolor = KolorSpal, Grubosc = 2 };
            for (double v = 0; v <= vmax + 1e-9; v += 1) fs.Punkty.Add(new Point(v, p.SilaMax(v, false)));
            WykresSily.Serie.Add(fs);
        }
        foreach (var (i, k) in new[] { 0.0, 5, 10, 15 }.Select((x, k) => (x, k)))
        {
            var w = new Wykres.Seria { Nazwa = $"opory na {i:0} ‰", Kolor = KoloryPoch[k], Przerywana = true, Grubosc = 1.3 };
            for (double v = 0; v <= vmax + 1e-9; v += 2) w.Punkty.Add(new Point(v, p.Opor(v) + p.OporProfilu(i)));
            WykresSily.Serie.Add(w);
        }
        WykresSily.XMax = vmax; WykresSily.Odswiez();

        bool czas = OsJazdy.SelectedIndex == 1;
        WykresJazdy.OsX = czas ? "czas [s]" : "droga [m]";
        double vr = Math.Min(p.VMax, 100);
        void Dodaj(string nazwa, Color kol, bool przer, List<(double T, double S, double V)> l) =>
            WykresJazdy.Serie.Add(new Wykres.Seria { Nazwa = nazwa, Kolor = kol, Przerywana = przer, Grubosc = 2, Punkty = l.Select(x => new Point(czas ? x.T : x.S, x.V)).ToList() });
        Dodaj("rozpędzanie, poziom", KoloryPoch[0], false, Jazda.Slad(p, 0, new Sterowanie { Nastawa = 1, Siec = siec, Warunki = 1 }, st => st.V * 3.6 >= vr));
        Dodaj("rozpędzanie, 10 ‰", KoloryPoch[3], false, Jazda.Slad(p, 0, new Sterowanie { Nastawa = 1, Siec = siec, Warunki = 1, Pochylenie = 10 }, st => st.V * 3.6 >= vr));
        Dodaj($"hamowanie z {vr:0}, poziom", KoloryPoch[0], true, Jazda.Slad(p, vr, new Sterowanie { Hamowanie = 1, Siec = siec, Warunki = 1 }, st => st.V <= 0));
        Dodaj($"hamowanie z {vr:0}, spadek 10 ‰", KoloryPoch[3], true, Jazda.Slad(p, vr, new Sterowanie { Hamowanie = 1, Siec = siec, Warunki = 1, Pochylenie = -10 }, st => st.V <= 0));
        WykresJazdy.XMax = 0; WykresJazdy.YMax = vr * 1.05; WykresJazdy.Odswiez();
    }

    /// <summary>Przejazd próbny zaznaczonego składu po profilu z pola tekstowego; wynik w tekście i na wykresach.</summary>
    public Przejazd.Wynik Przejedz()
    {
        CommitPending();
        if (Sklad == null) { Message("Przejazd próbny: wybierz skład"); return null; }
        var prof = Przejazd.Parsuj(ProfilBox.Text, out var err);
        if (prof == null) { Message("Profil: " + err); return null; }
        Pociag p;
        try { p = new Pociag(Sklad, Doc.P); }
        catch (Exception ex) { Message("Przejazd próbny: " + ex.Message); return null; }
        var w = Przejazd.Przejedz(p, prof);
        OstatniPrzejazd = w;
        double vsr = w.Czas > 0 ? w.Droga / w.Czas * 3.6 : 0;
        PrzejazdWynik.Text = (w.Dojechal ? $"Dojechał: {w.Droga:0} m w {Math.Floor(w.Czas / 60):0} min {w.Czas % 60:00} s, średnio {vsr:0} km/h" : $"NIE DOJECHAŁ: {w.Uwaga} – po {w.Droga:0} m, {w.Czas:0} s")
            + $"\nNajwyższa prędkość {(w.Probki.Count > 0 ? w.Probki.Max(q => q.V) : 0):0} km/h; skład {p.Nazwa}, {p.Masa:0} t, profil {prof.Sum(x => x.Dlugosc):0} m";
        PrzejazdWynik.Foreground = w.Dojechal ? Brushes.Black : MissFg;
        WykresPrzejazdu.Serie.Clear();
        WykresPrzejazdu.Serie.Add(new Wykres.Seria { Nazwa = "prędkość", Kolor = KolorSiec, Grubosc = 2, Punkty = w.Probki.Select(q => new Point(q.S, q.V)).ToList() });
        var doz = new Wykres.Seria { Nazwa = "prędkość dozwolona", Kolor = Color.FromRgb(0xb0, 0x30, 0x20), Przerywana = true, Schodki = true };
        double s0 = 0; foreach (var x in prof) { doz.Punkty.Add(new Point(s0, Math.Min(x.VMax, p.VMax))); s0 += x.Dlugosc; } doz.Punkty.Add(new Point(s0, doz.Punkty[^1].Y));
        WykresPrzejazdu.Serie.Add(doz);
        WykresPrzejazdu.XMax = s0; WykresPrzejazdu.YMax = Math.Min(p.VMax, prof.Max(x => x.VMax)) * 1.1; WykresPrzejazdu.Odswiez();
        WykresProfilu.Serie.Clear();
        var pp = new Wykres.Seria { Nazwa = "pochylenie (+ wzniesienie)", Kolor = Color.FromRgb(0x60, 0x60, 0x60), Schodki = true };
        s0 = 0; foreach (var x in prof) { pp.Punkty.Add(new Point(s0, x.Pochylenie)); s0 += x.Dlugosc; } pp.Punkty.Add(new Point(s0, pp.Punkty[^1].Y));
        WykresProfilu.Serie.Add(pp);
        if (prof.Any(x => !x.Siec))
        {
            var bs = new Wykres.Seria { Nazwa = "bez sieci", Kolor = KolorSpal, Grubosc = 4 };
            s0 = 0; foreach (var x in prof) { if (!x.Siec) { bs.Punkty.Add(new Point(s0, 0)); bs.Punkty.Add(new Point(s0 + x.Dlugosc, 0)); } s0 += x.Dlugosc; }
            WykresProfilu.Serie.Add(bs);
        }
        WykresProfilu.XMax = s0; WykresProfilu.Odswiez();
        Message(w.Dojechal ? $"Przejazd próbny: {w.Czas:0} s" : "Przejazd próbny: " + w.Uwaga);
        return w;
    }

    /// <summary>Profil z toru układu symulatora do pola profilu (bez okien – test i okno wyboru).</summary>
    public bool ProfilZUkladu(string path, string tor, bool odwrotnie)
    {
        try
        {
            var l = ProfilUkladu.Profil(path, tor, odwrotnie);
            if (l.Count == 0) { Message($"Tor {tor}: brak kawałków w układzie"); return false; }
            ProfilBox.Text = $"# {Path.GetFileName(path)}, tor {tor}{(odwrotnie ? " (B → A)" : "")}" + (l.All(x => x.Pochylenie == 0) ? " – w układzie brak pochyleń (poch)" : "") + "\n" + Przejazd.Tekst(l);
            Message($"Profil z układu: tor {tor}, {l.Count} kawałków, {l.Sum(x => x.Dlugosc):0} m");
            return true;
        }
        catch (Exception ex) { Message("Profil z układu: " + ex.Message); return false; }
    }

    void ProfilZUkladuOkno()
    {
        var dlg = new OpenFileDialog { Title = "Układ symulatora", Filter = "Układ symulatora (*.json)|*.json" };
        var dir = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(TaborPlik.SciezkaWspolna()))) ?? "", "LcsWidzew", "Data", "projekty");
        if (Directory.Exists(dir)) dlg.InitialDirectory = dir;
        if (dlg.ShowDialog(this) != true) return;
        List<ProfilUkladu.Tor> tory;
        try { tory = ProfilUkladu.Tory(dlg.FileName); } catch (Exception ex) { Message("Układ: " + ex.Message); return; }
        if (tory.Count == 0) { Message("Układ bez numerów torów"); return; }
        var cb = new ComboBox { Margin = new Thickness(0, 4, 0, 8) };
        foreach (var t in tory) cb.Items.Add($"tor {t.Numer} – {t.Dlugosc:0} m ({t.Kawalki} kawałków)");
        cb.SelectedIndex = 0;
        var odw = new CheckBox { Content = "jazda od B do A (w przeciwną stronę)", Margin = new Thickness(0, 0, 0, 8) };
        var ok = new Button { Content = "Wczytaj profil", IsDefault = true, Padding = new Thickness(10, 2, 10, 2), HorizontalAlignment = HorizontalAlignment.Right };
        var win = new Window { Title = "Profil z układu", Owner = this, SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize };
        ok.Click += (s, e) => win.DialogResult = true;
        win.Content = new StackPanel { Margin = new Thickness(12), MinWidth = 320, Children = { new TextBlock { Text = Path.GetFileName(dlg.FileName) }, cb, odw, ok } };
        if (win.ShowDialog() == true) ProfilZUkladu(dlg.FileName, tory[cb.SelectedIndex].Numer, odw.IsChecked == true);
    }
}

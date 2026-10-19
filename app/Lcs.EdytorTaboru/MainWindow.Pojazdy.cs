using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Point = System.Windows.Point;
using Lcs.Tabor;

namespace Lcs.EdytorTaboru;

/// <summary>Zakładka „Pojazdy”: lista serii i formularz pojazdu (§ 2) z charakterystykami F(v) – sieć i spalinowy (BZT – obie).</summary>
public partial class MainWindow
{
    public int PojazdIdx = -1;
    public readonly ListBox PojazdList = new() { Width = 300, Margin = new Thickness(0, 0, 8, 0) };
    public Pojazd Pojazd => PojazdIdx >= 0 && PojazdIdx < Doc.P.Pojazdy.Count ? Doc.P.Pojazdy[PojazdIdx] : null;
    readonly StackPanel pojazdForm = new() { Margin = new Thickness(8) };
    readonly List<(TextBox Box, Func<Pojazd, string> Get)> pojazdPola = new();
    /// <summary>Pola formularza pojazdu po nazwie (jak w komunikatach, np. „Masa”, „Masa hamująca próżny P”) – dla testu.</summary>
    public readonly Dictionary<string, TextBox> Pola = new();
    public TextBox SeriaBox, NazwaBox;
    public readonly ComboBox RodzajBox = new() { Width = 300, Margin = new Thickness(0, 2, 8, 2) };
    public readonly CheckBox DoPotwBox = new() { Content = "dane do potwierdzenia (przykład, nie z karty pojazdu)", Margin = new Thickness(0, 6, 0, 2) };
    /// <summary>Sekcje charakterystyk: [0] – sieć, [1] – spalinowy.</summary>
    public readonly CharSekcja[] Chary = new CharSekcja[2];
    /// <summary>Wykres F(v) zaznaczonego pojazdu z uchwytami punktów (etap B4).</summary>
    public readonly Wykres WykresFv = new() { Width = 900, HorizontalAlignment = HorizontalAlignment.Left, Tytul = "Charakterystyka trakcyjna", OsX = "v [km/h]", OsY = "F [kN]" };

    public class CharSekcja
    {
        public string Zasilanie;
        public CheckBox Jest;
        public TextBox Moc, VMax, F0, Punkty;
        public Button ZMocy;
        public StackPanel Panel;
    }

    UIElement BuildPojazdy()
    {
        var left = new DockPanel();
        var btns = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        btns.Children.Add(Btn("+ pojazd", (s, e) => DodajPojazd(), "Nowa seria w katalogu (dane wpisujesz w formularzu)"));
        btns.Children.Add(Btn("Kopiuj", (s, e) => KopiujPojazd(), "Kopia zaznaczonej serii – np. odmiana z inną mocą"));
        btns.Children.Add(Btn("Usuń", (s, e) => UsunPojazd(), "Usuń serię z katalogu (tylko gdy nie ma jej w żadnym składzie; Ctrl+Z przywraca)"));
        left.Children.Add(Dock(btns, System.Windows.Controls.Dock.Top));
        left.Children.Add(PojazdList);
        PojazdList.SelectionChanged += (s, e) =>
        {
            if (refreshing || PojazdList.SelectedIndex < 0) return;
            CommitPending();
            PojazdIdx = PojazdList.SelectedIndex; FillPojazd();
        };

        var g = pojazdForm;
        g.Children.Add(Header("Pojazd"));
        SeriaBox = Pole(t => { if (Pojazd == null) return; var err = Doc.ZmienSerie(PojazdIdx, t); if (err != null) { Message(err); SeriaBox.Text = Pojazd.Seria; } else Message($"Seria: {t.Trim()} (odwołania w składach poprawione)"); }, 160,
            "Oznaczenie serii – identyfikator w składach; zmiana poprawia składy");
        NazwaBox = Pole(t => ChP("Nazwa", p => p.Nazwa = t.Trim()), 420);
        Wiersz(g, "Seria", SeriaBox, "Nazwa", NazwaBox);
        foreach (var r in Rodzaj.Wszystkie) RodzajBox.Items.Add(r.Name);
        RodzajBox.SelectionChanged += (s, e) => { if (!refreshing && RodzajBox.SelectedIndex >= 0) { var k = Rodzaj.Wszystkie[RodzajBox.SelectedIndex].Key; ChP("Rodzaj", p => p.Rodzaj = k); } };
        Wiersz(g, "Rodzaj", RodzajBox);

        g.Children.Add(Header("Wymiary i masy"));
        Wiersz(g, "Długość [m]", Num("Długość", p => p.Dlugosc, (p, v) => p.Dlugosc = v, 0.1),
            "Osie", Num("Liczba osi", p => p.Osie, (p, v) => p.Osie = (int)Math.Round(v), 1),
            "Vmax [km/h]", Num("Vmax", p => p.VMax, (p, v) => p.VMax = v, 1));
        Wiersz(g, "Masa własna [t]", Num("Masa", p => p.Masa, (p, v) => p.Masa = v, 0.1),
            "Ładunek / pasażerowie [t]", Num("Ładunek", p => p.Ladunek, (p, v) => p.Ladunek = v),
            "Masa napędna [t]", Num("Masa napędna", p => p.MasaNapedna, (p, v) => p.MasaNapedna = v, 0, "Masa na osiach napędnych – przyczepność; 0 – pojazd bez napędu"));
        Wiersz(g, "Masy wirujące ρ", Num("Masy wirujące", p => p.MasyWirujace, (p, v) => p.MasyWirujace = v, 0, "Masa zastępcza = m·(1+ρ); wagon ok. 0,04–0,08, lokomotywa 0,15–0,25"));

        g.Children.Add(Header("Opory ruchu w = a + b·v + c·v² [N/kN], v w km/h"));
        Wiersz(g, "a", Num("Opór a", p => p.Opory.A, (p, v) => p.Opory.A = v), "b", Num("Opór b", p => p.Opory.B, (p, v) => p.Opory.B = v),
            "c", Num("Opór c", p => p.Opory.C, (p, v) => p.Opory.C = v));

        g.Children.Add(Header("Hamulec – masa hamująca [t] (puste – brak nastawienia)"));
        var mh = new List<UIElement>();
        foreach (var n in new[] { "G", "P", "R" }) { var k = n; mh.Add(new TextBlock { Text = $"próżny {k}" }); mh.Add(Mh(k, false)); }
        Wiersz(g, mh.ToArray());
        var mhl = new List<UIElement>();
        foreach (var n in new[] { "G", "P", "R" }) { var k = n; mhl.Add(new TextBlock { Text = $"ładowny {k}" }); mhl.Add(Mh(k, true)); }
        Wiersz(g, mhl.ToArray());
        Wiersz(g, "Hamulec ED [kN]", Num("Hamulec ED", p => p.HamulecED, (p, v) => p.HamulecED = v, 0, "Hamulec elektrodynamiczny (tylko pod siecią); 0 – brak"));

        g.Children.Add(Header("Charakterystyka trakcyjna F(v)"));
        g.Children.Add(new TextBlock { Text = "Punkty: w każdym wierszu prędkość [km/h] i siła [kN], np. „30 69,6”; za ostatnim punktem F = moc / v. „Z mocy” – krzywa F = min(F0, P/v) co 10 km/h jako podpowiedź do poprawy.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left });
        var chRow = new StackPanel { Orientation = Orientation.Horizontal };
        for (int i = 0; i < 2; i++) { Chary[i] = BuildChar(i == 0 ? Zasilanie.Siec : Zasilanie.Spalinowy); chRow.Children.Add(Chary[i].Panel); }
        g.Children.Add(chRow);
        WykresFv.PunktPrzesuniety += (s, i, pt) => { var z = (string)s.Tag; ChP("Punkt F(v)", p => p.Char(z).Punkty[i] = new[] { pt.X, pt.Y }); Message($"Punkt {i + 1} ({z}): {F(pt.X)} km/h, {F(pt.Y)} kN"); };
        WykresFv.PunktDodany += (s, pt) =>
        {
            var z = (string)s.Tag;
            if (Pojazd.Char(z).Punkty.Any(q => Math.Abs(q[0] - pt.X) < 0.5)) { Message($"Punkt przy {F(pt.X)} km/h już jest"); return; }
            ChP("Nowy punkt F(v)", p => { var l = p.Char(z).Punkty; l.Add(new[] { pt.X, pt.Y }); l.Sort((a, b) => a[0].CompareTo(b[0])); });
            Message($"Nowy punkt ({z}): {F(pt.X)} km/h, {F(pt.Y)} kN");
        };
        WykresFv.PunktUsuniety += (s, i) =>
        {
            var z = (string)s.Tag;
            if (Pojazd.Char(z).Punkty.Count <= 1) { Message("Charakterystyka musi mieć co najmniej jeden punkt"); return; }
            ChP("Usunięcie punktu F(v)", p => p.Char(z).Punkty.RemoveAt(i));
            Message($"Usunięto punkt {i + 1} ({z})");
        };
        g.Children.Add(new TextBlock { Text = "Wykres: przeciągnij punkt (kwadrat), dwuklik – nowy punkt na najbliższej charakterystyce, prawy przycisk na punkcie – usunięcie. Linie przerywane: przyczepność (sucho) i opory samego pojazdu.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 2) });
        g.Children.Add(WykresFv);

        g.Children.Add(Header("Źródło danych"));
        Wiersz(g, "Źródło", Txt("Źródło", p => p.Zrodlo, (p, v) => p.Zrodlo = v, 560));
        Wiersz(g, "Uwagi", Txt("Uwagi", p => p.Uwagi, (p, v) => p.Uwagi = v, 560));
        DoPotwBox.Click += (s, e) => { var v = DoPotwBox.IsChecked == true; ChP("Do potwierdzenia", p => p.DoPotwierdzenia = v); };
        g.Children.Add(DoPotwBox);

        var dock = new DockPanel { Margin = new Thickness(8) };
        dock.Children.Add(Dock(left, System.Windows.Controls.Dock.Left));
        dock.Children.Add(new ScrollViewer { Content = pojazdForm, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        return dock;
    }

    static void Wiersz(Panel g, params object[] items)
    {
        var row = new WrapPanel { Margin = new Thickness(0, 1, 0, 1) };
        foreach (var x in items) row.Children.Add(x is string s ? Label(s) : (UIElement)x);
        g.Children.Add(row);
    }

    /// <summary>Zmiana zaznaczonego pojazdu z cofaniem.</summary>
    void ChP(string what, Action<Pojazd> act)
    {
        if (Pojazd == null) return;
        int i = PojazdIdx; var seria = Pojazd.Seria;
        Doc.Change($"{what} ({seria})", p => act(p.Pojazdy[i]));
    }

    TextBox Num(string what, Func<Pojazd, double> get, Action<Pojazd, double> set, double min = 0, string tip = null)
    {
        TextBox tb = null;
        tb = Pole(t =>
        {
            if (Pojazd == null) return;
            if (!TryNum(t, out var v) || v < min) { Message($"{what}: wpisz liczbę ≥ {F(min)}"); tb.Text = F(get(Pojazd)); return; }
            ChP(what, p => set(p, v));
        }, 80, tip);
        pojazdPola.Add((tb, p => F(get(p)))); Pola[what] = tb;
        return tb;
    }

    TextBox Txt(string what, Func<Pojazd, string> get, Action<Pojazd, string> set, double width)
    {
        var tb = Pole(t => ChP(what, p => set(p, t.Trim())), width);
        pojazdPola.Add((tb, get)); Pola[what] = tb;
        return tb;
    }

    TextBox Mh(string nast, bool ladowny)
    {
        string what = $"Masa hamująca {(ladowny ? "ładowny" : "próżny")} {nast}";
        TextBox tb = null;
        string Get(Pojazd p) { var d = ladowny ? p.MasaHamujacaLadowny : p.MasaHamujaca; return d != null && d.TryGetValue(nast, out var v) ? F(v) : ""; }
        tb = Pole(t =>
        {
            if (Pojazd == null) return;
            if (t.Trim().Length == 0) { ChP(what, p => { var d = ladowny ? p.MasaHamujacaLadowny : p.MasaHamujaca; d?.Remove(nast); if (ladowny && d?.Count == 0) p.MasaHamujacaLadowny = null; }); return; }
            if (!TryNum(t, out var v) || v < 0) { Message($"{what}: wpisz liczbę ≥ 0 albo zostaw puste"); tb.Text = Get(Pojazd); return; }
            ChP(what, p => { if (ladowny) (p.MasaHamujacaLadowny ??= new())[nast] = v; else p.MasaHamujaca[nast] = v; });
        }, 60);
        pojazdPola.Add((tb, Get)); Pola[what] = tb;
        return tb;
    }

    CharSekcja BuildChar(string zas)
    {
        var c = new CharSekcja { Zasilanie = zas, Panel = new StackPanel { Width = 360, Margin = new Thickness(0, 4, 16, 0) } };
        string nazwa = zas == Zasilanie.Siec ? "sieć (elektryczna)" : "spalinowa";
        c.Jest = new CheckBox { Content = $"charakterystyka – {nazwa}", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
        c.Jest.Click += (s, e) =>
        {
            bool on = c.Jest.IsChecked == true;
            ChP(on ? $"Charakterystyka {nazwa}" : $"Usunięcie charakterystyki {nazwa}", p =>
            {
                p.Trakcja.RemoveAll(x => x.Zasilanie == zas);
                if (on) { p.Trakcja.Add(new Charakterystyka { Zasilanie = zas }); p.Trakcja.Sort((a, b) => (a.Zasilanie == Zasilanie.Siec ? 0 : 1).CompareTo(b.Zasilanie == Zasilanie.Siec ? 0 : 1)); }
            });
        };
        c.Panel.Children.Add(c.Jest);
        c.Moc = Pole(t => ChChar(c, "Moc", t, (x, v) => x.Moc = v), 70, "Moc na obwodzie kół [kW]");
        c.VMax = Pole(t => ChChar(c, "Vmax charakterystyki", t, (x, v) => x.VMax = v), 60, "Vmax przy tym zasilaniu; 0 – jak pojazd");
        Wiersz(c.Panel, "Moc [kW]", c.Moc, "Vmax [km/h]", c.VMax);
        c.F0 = new TextBox { Width = 70, Margin = new Thickness(0, 2, 8, 2), ToolTip = "Siła rozruchowa F0 [kN] – tylko do przycisku „Z mocy”" };
        c.ZMocy = Btn("Z mocy", (s, e) => ZMocy(c), "Punkty F = min(F0, P/v) co 10 km/h do Vmax (zastępuje obecne punkty; Ctrl+Z cofa)");
        Wiersz(c.Panel, "F0 [kN]", c.F0, c.ZMocy);
        c.Panel.Children.Add(new TextBlock { Text = "Punkty F(v): v [km/h]  F [kN]", Margin = new Thickness(0, 4, 0, 2) });
        c.Punkty = new TextBox { AcceptsReturn = true, Height = 170, Width = 220, HorizontalAlignment = HorizontalAlignment.Left, FontFamily = new FontFamily("Consolas"), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Action commit = () => { if (!refreshing) { pokazane[c.Punkty] = c.Punkty.Text; ChPunkty(c); } };
        c.Punkty.Tag = commit;
        c.Punkty.LostFocus += (s, e) => commit();
        c.Panel.Children.Add(c.Punkty);
        return c;
    }

    void ChChar(CharSekcja c, string what, string t, Action<Charakterystyka, double> set)
    {
        var ch = Pojazd?.Char(c.Zasilanie); if (ch == null) return;
        if (!TryNum(t, out var v) || v < 0) { Message($"{what}: wpisz liczbę ≥ 0"); FillPojazd(); return; }
        ChP($"{what} ({c.Zasilanie})", p => set(p.Char(c.Zasilanie), v));
    }

    /// <summary>Punkty z pola tekstowego: wiersze „v F”; v rosnąco.</summary>
    public static List<double[]> ParsujPunkty(string text, out string blad)
    {
        blad = null; var l = new List<double[]>();
        foreach (var (line, nr) in text.Split('\n').Select((x, i) => (x.Trim(), i + 1)))
        {
            if (line.Length == 0) continue;
            var parts = line.Split(new[] { ' ', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !TryNum(parts[0], out var v) || !TryNum(parts[1], out var f) || v < 0 || f < 0) { blad = $"wiersz {nr}: „{line}” – potrzebne dwie liczby: v F"; return null; }
            if (l.Count > 0 && v <= l[^1][0]) { blad = $"wiersz {nr}: prędkość {F(v)} nie większa niż w poprzednim wierszu"; return null; }
            l.Add(new[] { v, f });
        }
        return l;
    }

    static string PunktyTekst(Charakterystyka c) => string.Join("\n", c.Punkty.Select(q => $"{F(q[0])} {F(q[1])}"));

    void ChPunkty(CharSekcja c)
    {
        var ch = Pojazd?.Char(c.Zasilanie); if (ch == null) return;
        var l = ParsujPunkty(c.Punkty.Text, out var err);
        if (l == null) { Message("Punkty F(v): " + err); return; }
        ChP($"Punkty F(v) ({c.Zasilanie})", p => p.Char(c.Zasilanie).Punkty = l);
    }

    void ZMocy(CharSekcja c)
    {
        var ch = Pojazd?.Char(c.Zasilanie);
        if (ch == null) { Message("Najpierw zaznacz charakterystykę"); return; }
        if (!TryNum(c.F0.Text, out var f0) || f0 <= 0) { Message("Z mocy: wpisz siłę rozruchową F0 [kN]"); return; }
        if (ch.Moc <= 0) { Message("Z mocy: wpisz moc [kW]"); return; }
        double vmax = ch.VMax > 0 ? ch.VMax : Pojazd.VMax;
        var pts = Charakterystyka.ZMocy(f0, ch.Moc, vmax);
        ChP($"Krzywa z mocy ({c.Zasilanie})", p => p.Char(c.Zasilanie).Punkty = pts);
        Message($"Krzywa F(v): F0 {F(f0)} kN, moc {F(ch.Moc)} kW, {pts.Count} punktów do {F(vmax)} km/h – popraw wg karty pojazdu");
    }

    static readonly Color KolorSiec = Color.FromRgb(0x1f, 0x5f, 0xc0), KolorSpal = Color.FromRgb(0xd0, 0x70, 0x10);

    /// <summary>Serie wykresu F(v): charakterystyki z uchwytami, przedłużenie P/v za ostatnim punktem, przyczepność, opory pojazdu na 0 i 10 ‰.</summary>
    void WykresPojazdu(Pojazd p)
    {
        var w = WykresFv; w.Serie.Clear();
        double vmax = Math.Max(10, p.VMax);
        foreach (var c in p.Trakcja)
        {
            var kol = c.Zasilanie == Zasilanie.Siec ? KolorSiec : KolorSpal; var nazwa = c.Zasilanie == Zasilanie.Siec ? "sieć" : "spalinowa";
            w.Serie.Add(new Wykres.Seria { Nazwa = "F(v) " + nazwa, Kolor = kol, Uchwyty = true, Tag = c.Zasilanie, Grubosc = 2, Punkty = c.Punkty.Select(q => new Point(q[0], q[1])).ToList() });
            double cv = c.VMax > 0 ? c.VMax : p.VMax;
            if (c.Moc > 0 && c.Punkty.Count > 0 && c.Punkty[^1][0] < cv)
            {
                var ext = new Wykres.Seria { Nazwa = "", Kolor = kol, Przerywana = true };
                for (double v = c.Punkty[^1][0]; v <= cv + 1e-9; v += 2) ext.Punkty.Add(new Point(v, c.F(v)));
                w.Serie.Add(ext);
            }
        }
        if (p.MasaNapedna > 0)
        {
            var ad = new Wykres.Seria { Nazwa = "przyczepność (sucho)", Kolor = Colors.Gray, Przerywana = true, Grubosc = 1.2 };
            for (double v = 0; v <= vmax + 1e-9; v += 2) ad.Punkty.Add(new Point(v, Pociag.Mi(v, 1) * p.MasaNapedna * Pociag.G));
            w.Serie.Add(ad);
        }
        try
        {
            var poc = new Pociag(new Sklad { Id = "_", Pojazdy = { new PozycjaSkladu { Seria = p.Seria } } }, Doc.P);
            foreach (var (i, kol) in new[] { (0.0, Color.FromRgb(0x50, 0x90, 0x50)), (10.0, Color.FromRgb(0xb0, 0x30, 0x20)) })
            {
                var r = new Wykres.Seria { Nazwa = $"opór pojazdu {i:0} ‰", Kolor = kol, Przerywana = true, Grubosc = 1.2 };
                for (double v = 0; v <= vmax + 1e-9; v += 2) r.Punkty.Add(new Point(v, poc.Opor(v) + poc.OporProfilu(i)));
                w.Serie.Add(r);
            }
        }
        catch { }
        w.XMax = vmax; w.YMax = 0;
        w.Odswiez();
    }

    // ------------------------------------------------------------------ lista i operacje
    void RefreshPojazdy()
    {
        PojazdList.Items.Clear();
        foreach (var p in Doc.P.Pojazdy)
            PojazdList.Items.Add(new TextBlock { Text = $"{p.Seria} – {Rodzaj.Nazwa(p.Rodzaj)}{(p.DoPotwierdzenia ? "  (do potwierdzenia)" : "")}", ToolTip = p.Nazwa });
        PojazdList.SelectedIndex = PojazdIdx;
        FillPojazd();
    }

    void FillPojazd()
    {
        bool was = refreshing; refreshing = true;
        try
        {
            var p = Pojazd;
            pojazdForm.IsEnabled = p != null;
            if (p == null) return;
            Set(SeriaBox, p.Seria); Set(NazwaBox, p.Nazwa);
            RodzajBox.SelectedIndex = Array.FindIndex(Rodzaj.Wszystkie, r => r.Key == p.Rodzaj);
            foreach (var (box, get) in pojazdPola) Set(box, get(p));
            DoPotwBox.IsChecked = p.DoPotwierdzenia;
            foreach (var c in Chary)
            {
                var ch = p.Char(c.Zasilanie);
                c.Jest.IsChecked = ch != null;
                foreach (var x in new UIElement[] { c.Moc, c.VMax, c.F0, c.ZMocy, c.Punkty }) x.IsEnabled = ch != null;
                Set(c.Moc, ch != null ? F(ch.Moc) : ""); Set(c.VMax, ch != null ? F(ch.VMax) : "");
                if (ch != null && c.F0.Text.Length == 0 && ch.Punkty.Count > 0) c.F0.Text = F(ch.Punkty[0][1]);
                if (ch == null) c.F0.Text = "";
                Set(c.Punkty, ch != null ? PunktyTekst(ch) : "");
            }
            WykresPojazdu(p);
        }
        finally { refreshing = was; }
    }

    public void DodajPojazd()
    {
        CommitPending();
        var s = "Nowy"; for (int n = 2; Doc.P.Pojazd(s) != null; n++) s = $"Nowy {n}";
        int at = PojazdIdx + 1;
        Doc.Change("Nowy pojazd", p => p.Pojazdy.Insert(at, new Pojazd { Seria = s, Nazwa = "", Rodzaj = Rodzaj.WagonPas, Dlugosc = 24.5, Masa = 40, Osie = 4, VMax = 120, MasaHamujaca = { ["P"] = 40 } }));
        PojazdIdx = at; Refresh();
        Message($"Nowy pojazd „{s}” – wpisz serię i dane z karty pojazdu");
    }

    public void KopiujPojazd()
    {
        if (Pojazd == null) return;
        CommitPending();
        var k = TaborPlik.Deserialize(TaborPlik.Serialize(new TaborPlik { Pojazdy = { Pojazd } })).Pojazdy[0];
        var s = Pojazd.Seria + " (kopia)"; for (int n = 2; Doc.P.Pojazd(s) != null; n++) s = $"{Pojazd.Seria} (kopia {n})";
        k.Seria = s; int at = PojazdIdx + 1;
        Doc.Change("Kopia pojazdu", p => p.Pojazdy.Insert(at, k));
        PojazdIdx = at; Refresh();
        Message($"Kopia: {s}");
    }

    public void UsunPojazd()
    {
        if (Pojazd == null) return;
        CommitPending();
        var seria = Pojazd.Seria;
        var w = Doc.P.Sklady.Where(s => s.Pojazdy.Any(x => x.Seria == seria)).Select(s => s.Id).ToList();
        if (w.Count > 0) { Message($"Nie usunięto {seria} – jest w składach: {string.Join(", ", w)}"); return; }
        int i = PojazdIdx;
        Doc.Change($"Usunięcie pojazdu {seria}", p => p.Pojazdy.RemoveAt(i));
        Message($"Usunięto {seria} (Ctrl+Z przywraca)");
    }
}

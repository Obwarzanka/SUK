using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Lcs.Powtarzacz;

/// <summary>
/// Widok „RASP-UZK” – odwzorowanie urządzenia zdalnej kontroli Z.A. Kombud dla SSP RASP-4F (DTR-2002/RASP-4F wersja 2.0, rozdz. 2.2
/// i 6.1, tabele 4–8; układ pól jak RASP-UZK 4.4Ft). Program ma trzy ekrany:
/// <list type="bullet">
/// <item>EKRAN I – stany ogólne wszystkich nadzorowanych ssp: pole przejazdu z km i elementami STAN, STEROWANIE, ZEROWANIE,
/// USTERKA I, USTERKA II, TRANSMISJA, ZASILANIE, TOR I, TOR II;</item>
/// <item>EKRAN II – wybrany ssp: schemat przejazdu (napędy N1–N4, sygnalizatory, strefa), przyciski poleceń
/// („Zezwolenie na ster. LOKALNE”, „Sterowanie AUTOMATYCZNE”, „T1 ZAŁ.”, „T1 WYŁ.”, „T2 ZAŁ.”, „T2 WYŁ.”, „ZEROWANIE” – w grze
/// niedostępne, wyszarzone) i lista bieżących zdarzeń;</item>
/// <item>EKRAN III – lista zarejestrowanych zdarzeń z filtrem.</item>
/// </list>
/// Komunikaty słowne jak w DTR (tabela 5–8): „OSTRZEGANIE”, „OCZEKIWANIE”, „Napęd N1 opuszczony”, „Załączony sygnalizator S12”,
/// „USTERKA kat. I”, „Zanik usterki kat. I”, „BRAK TRANSMISJI”, „TRANSMISJA OK.”, „TOR 1: czujniki wyłączone”, „Zaakceptowano !”
/// (Ctrl+Enter). Gra nie daje stref czujników, zasilania, napędów pojedynczo ani poleceń UZK – takie pola zostają w stanie zasadniczym.
/// </summary>
public sealed class RaspPanel : DockPanel
{
    static Brush B(string hex) { var b = (Brush)new BrushConverter().ConvertFromString(hex); b.Freeze(); return b; }
    // wygląd: aplikacja na komputerze przemysłowym (klasyczny szary interfejs), pola wypełniane barwą
    internal static readonly Brush Tlo = B("#C0C0C0"), TloPola = B("#D4D0C8"), Ramka = B("#404040"), Czarny = B("#000000"),
        Szary = B("#A0A0A0"), Bialy = B("#FFFFFF"), Fiolet = B("#B040C0"), Zielony = B("#20B020"), Pomar = B("#FF9020"),
        Czerwony = B("#E00000"), Zolty = B("#FFE000"), CiemnoCzerw = B("#800000"), Niebieski = B("#2060FF"), Granat = B("#000080");

    public sealed record Kom(DateTime Czas, string Km, string Tekst, int Kat);   // Kat: 0 – informacja, 1 / 2 – usterka kat. I / II

    readonly List<Kom> _rejestr = new();
    readonly Dictionary<string, Migawka> _poprz = new(StringComparer.OrdinalIgnoreCase);
    readonly WrapPanel _ekran1 = new() { Margin = new Thickness(6) };
    readonly Grid _ekran2 = new();
    readonly DockPanel _ekran3 = new();
    readonly ListBox _lbBiezace = new(), _lbRejestr = new();
    readonly StackPanel _belka = new() { Orientation = Orientation.Horizontal };
    readonly TextBox _filtr3 = new() { Width = 160, Margin = new Thickness(6, 3, 6, 3) };
    readonly SzczegolyRasp _schemat = new();
    readonly StackPanel _przyciski = new() { Width = 190, Margin = new Thickness(6) };
    readonly Dictionary<string, PoleRasp> _pola = new(StringComparer.OrdinalIgnoreCase);
    readonly ContentControl _srodek = new();
    string _ekran = "I", _wybrany;
    List<Przejazd> _ostatnie = new();

    record Migawka(bool Brak, bool Ostrz, string RogWj, string RogWy, bool Awaria, int Tory);

    public RaspPanel()
    {
        Background = Tlo;
        TextElement.SetFontFamily(this, new FontFamily("Arial"));
        TextElement.SetForeground(this, Czarny);

        // dolna belka – pasek sygnalizacyjny i wyboru ekranu (DTR rys. 6)
        var dol = new Border { Background = TloPola, BorderBrush = Ramka, BorderThickness = new Thickness(0, 1, 0, 0), Child = _belka, Padding = new Thickness(4) };
        DockPanel.SetDock(dol, Dock.Bottom);
        Children.Add(dol);

        // ekran II: przyciski | schemat / lista bieżących zdarzeń
        _przyciski.Children.Add(new TextBlock { Text = "POLECENIA", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 4) });
        foreach (var (et, tip) in new[]
                 {
                     ("Zezwolenie na ster. LOKALNE", "umożliwienie sterowania miejscowego z szafki bez wejścia do kontenera"),
                     ("Sterowanie AUTOMATYCZNE", "przełączenie w tryb pracy samoczynnej"),
                     ("T1 ZAŁ.", "załączenie z UZK czujników torowych w torze nr 1"), ("T1 WYŁ.", "wyłączenie z UZK czujników torowych w torze nr 1"),
                     ("T2 ZAŁ.", "załączenie z UZK czujników torowych w torze nr 2"), ("T2 WYŁ.", "wyłączenie z UZK czujników torowych w torze nr 2"),
                     ("ZEROWANIE", "zdalne doprowadzenie ssp do stanu oczekiwania, gdy po przejechaniu pociągu sygnalizacja pozostaje w stanie ostrzegania"),
                 })
            _przyciski.Children.Add(new Button
            {
                Content = et, IsEnabled = false, Height = 34, Margin = new Thickness(0, 2, 0, 2), Background = Szary,
                ToolTip = $"{tip} – brak w grze (SimRail nie przyjmuje poleceń UZK)", HorizontalContentAlignment = HorizontalAlignment.Center,
            });
        ToolTipService.SetShowOnDisabled(_przyciski, true);
        foreach (UIElement b in _przyciski.Children) ToolTipService.SetShowOnDisabled(b, true);
        _przyciski.Children.Add(new TextBlock { Text = "Polecenia UZK: gra ich\nnie przyjmuje (wyszarzone).", FontSize = 10, Margin = new Thickness(0, 6, 0, 0), Foreground = Ramka });

        _ekran2.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _ekran2.ColumnDefinitions.Add(new ColumnDefinition());
        _ekran2.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star) });
        _ekran2.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) });
        Grid.SetRowSpan(_przyciski, 2); _ekran2.Children.Add(_przyciski);
        var ramkaSchematu = new Border { Child = _schemat, Background = Czarny, BorderBrush = Ramka, BorderThickness = new Thickness(1), Margin = new Thickness(6) };
        Grid.SetColumn(ramkaSchematu, 1); _ekran2.Children.Add(ramkaSchematu);
        Styl(_lbBiezace); Grid.SetColumn(_lbBiezace, 1); Grid.SetRow(_lbBiezace, 1); _ekran2.Children.Add(_lbBiezace);

        // ekran III: rejestr z filtrem
        var g3 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6) };
        g3.Children.Add(new TextBlock { Text = "EKRAN III – zarejestrowane zdarzenia.  Filtr:", VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Bold });
        g3.Children.Add(_filtr3);
        _filtr3.TextChanged += (_, _) => Ekran3();
        DockPanel.SetDock(g3, Dock.Top); _ekran3.Children.Add(g3);
        Styl(_lbRejestr); _ekran3.Children.Add(_lbRejestr);

        Children.Add(_srodek);
        _srodek.Content = new ScrollViewer { Content = _ekran1, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    /// <summary>
    /// Uproszczony schemat przejazdu (pole STAN): krawędzie jezdni w barwie stanu, dwa tory w poprzek, półrogatki
    /// (wjazdowe – lewy dół / prawa góra, wyjazdowe – prawy dół / lewa góra): podniesione – pionowo, opuszczone – w poprzek
    /// pasa ruchu, w ruchu – ukośnie, migające. Bez wypełnienia jezdni – sam obrys.
    /// </summary>
    internal static void Schemat(DrawingContext dc, Rect r, Brush stan, string rogWj, string rogWy, bool miga)
    {
        double cx = r.Left + r.Width / 2, pol = r.Width * 0.17, gora = r.Top + r.Height * 0.10, dol = r.Bottom - r.Height * 0.10;
        double ty1 = r.Top + r.Height * 0.40, ty2 = r.Top + r.Height * 0.60;
        double grub = Math.Max(2, r.Width / 30);
        // jezdnia: dwie krawędzie i przerywana oś
        var kraw = new Pen(stan, grub);
        dc.DrawLine(kraw, new Point(cx - pol, gora), new Point(cx - pol, dol));
        dc.DrawLine(kraw, new Point(cx + pol, gora), new Point(cx + pol, dol));
        dc.DrawLine(new Pen(stan, grub / 2) { DashStyle = new DashStyle(new double[] { 2, 2 }, 0) }, new Point(cx, gora), new Point(cx, dol));
        // tory: po dwie szyny, przechodzą przez jezdnię
        var szyna = new Pen(Szary, Math.Max(1.2, grub / 2));
        foreach (var ty in new[] { ty1, ty2 })
        {
            double d = r.Height * 0.03;
            dc.DrawLine(szyna, new Point(r.Left + 3, ty - d), new Point(r.Right - 3, ty - d));
            dc.DrawLine(szyna, new Point(r.Left + 3, ty + d), new Point(r.Right - 3, ty + d));
        }
        // półrogatki
        double dl = pol * 1.05, yG = r.Top + r.Height * 0.22, yD = r.Bottom - r.Height * 0.22;
        void Rog(double x, double y, int strona, string st)   // strona: +1 – ramię w prawo, −1 – w lewo
        {
            Brush b = st == null ? Bialy : st == "zamknięte" ? Czerwony : st == "w ruchu" ? (miga ? Zolty : Czarny) : Bialy;
            var koniec = st == "zamknięte" ? new Point(x + strona * dl, y) : st == "w ruchu" ? new Point(x + strona * dl * 0.7, y - dl * 0.7) : new Point(x, y - dl);
            dc.DrawLine(new Pen(b, grub * 1.2), new Point(x, y), koniec);
            dc.DrawEllipse(Szary, null, new Point(x, y), grub, grub);
        }
        double o = pol + grub * 2;
        Rog(cx - o, yD, +1, rogWj); Rog(cx + o, yG, -1, rogWj);   // wjazdowe (ruch prawostronny)
        Rog(cx + o, yD, -1, rogWy); Rog(cx - o, yG, +1, rogWy);   // wyjazdowe
    }

    static void Styl(ListBox lb)
    {
        lb.Background = Bialy; lb.Foreground = Czarny; lb.FontFamily = new FontFamily("Consolas"); lb.FontSize = 12;
        lb.Margin = new Thickness(6); lb.BorderBrush = Ramka;
    }

    public void PokazEkran(string e, string przejazd = null)
    {
        _ekran = e; if (przejazd != null) _wybrany = przejazd;
        _srodek.Content = e switch
        {
            "II" => _ekran2,
            "III" => _ekran3,
            _ => new ScrollViewer { Content = _ekran1, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
        };
        if (e == "III") Ekran3();
        Belka();
    }

    void Belka()
    {
        _belka.Children.Clear();
        Button P(string t, bool akt, RoutedEventHandler h)
        {
            var b = new Button { Content = t, MinWidth = 70, Height = 30, Margin = new Thickness(2, 0, 2, 0), Padding = new Thickness(6, 0, 6, 0),
                                 Background = akt ? Granat : TloPola, Foreground = akt ? Bialy : Czarny, FontWeight = akt ? FontWeights.Bold : FontWeights.Normal };
            b.Click += h; _belka.Children.Add(b); return b;
        }
        P("EKRAN I", _ekran == "I", (_, _) => PokazEkran("I"));
        foreach (var p in _ostatnie)
        {
            var n = p.Nazwa;
            P(p.KmTekst, _ekran == "II" && _wybrany == n, (_, _) => PokazEkran("II", n));
        }
        P("EKRAN III", _ekran == "III", (_, _) => PokazEkran("III"));
        _belka.Children.Add(new TextBlock { Text = "   Ctrl+Enter – „Zaakceptowano !” (potwierdzenie usterki)", VerticalAlignment = VerticalAlignment.Center, FontSize = 11 });
    }

    /// <summary>Wpis do rejestru (DTR 6.1: komunikaty słowne z datą i godziną).</summary>
    public void Komunikat(string km, string tekst, int kat = 0)
    {
        var k = new Kom(DateTime.Now, km, tekst, kat);
        _rejestr.Add(k);
        if (_rejestr.Count > 5000) _rejestr.RemoveAt(0);
        if (_ekran == "III") Ekran3();
    }

    static string Linia(Kom k) => $"{k.Czas:yyyy-MM-dd HH:mm:ss}  {(k.Km.Length > 0 ? $"km {k.Km,-8}" : new string(' ', 11))}  {k.Tekst}";

    void Ekran3()
    {
        var f = _filtr3.Text.Trim();
        _lbRejestr.ItemsSource = _rejestr.Where(k => f.Length == 0 || Linia(k).Contains(f, StringComparison.OrdinalIgnoreCase)).Reverse().Take(1000).Select(Linia).ToList();
    }

    /// <summary>Co 250 ms z okna: stany przejazdów, miganie, nieskwitowane usterki.</summary>
    public void Odswiez(List<Przejazd> kolej, Func<Przejazd, bool> brak, bool miga, ISet<string> niekwitowane)
    {
        bool zmianaListy = kolej.Count != _ostatnie.Count || kolej.Where((p, i) => p.Nazwa != _ostatnie[i].Nazwa).Any();
        _ostatnie = kolej;
        foreach (var p in kolej) Komunikaty(p, brak(p));

        if (zmianaListy)
        {
            _ekran1.Children.Clear();
            foreach (var p in kolej)
            {
                if (!_pola.TryGetValue(p.Nazwa, out var v))
                {
                    v = new PoleRasp { Cursor = Cursors.Hand };
                    var n = p.Nazwa;
                    v.MouseLeftButtonUp += (_, _) => PokazEkran("II", n);
                    _pola[n] = v;
                }
                _ekran1.Children.Add(v);
            }
            if (_wybrany == null || kolej.All(p => p.Nazwa != _wybrany)) _wybrany = kolej.FirstOrDefault()?.Nazwa;
            Belka();
        }
        foreach (var p in kolej)
        {
            var v = _pola[p.Nazwa];
            v.P = p; v.Brak = brak(p); v.Miga = miga; v.Niekwitowana = niekwitowane.Contains(p.Nazwa);
            v.InvalidateVisual();
        }
        if (_ekran == "II")
        {
            var w = kolej.FirstOrDefault(p => p.Nazwa == _wybrany);
            _schemat.P = w; _schemat.Brak = w == null || brak(w); _schemat.Miga = miga; _schemat.InvalidateVisual();
            var km = w?.KmTekst ?? "";
            var biez = _rejestr.Where(k => k.Km == km || k.Km.Length == 0).Reverse().Take(200).Select(Linia).ToList();
            if (_lbBiezace.Items.Count != biez.Count || (biez.Count > 0 && (string)_lbBiezace.Items[0] != biez[0])) _lbBiezace.ItemsSource = biez;
        }
    }

    /// <summary>Komunikaty słowne przy zmianie stanu (DTR tabela 5–8).</summary>
    void Komunikaty(Przejazd p, bool brak)
    {
        var m = new Migawka(brak, !brak && p.Ostrzeganie, brak ? null : Przejazd.Rog(p.Wj), brak ? null : Przejazd.Rog(p.Wy), !brak && p.Awaria, p.ToryWyl.Count);
        var km = p.KmTekst;
        if (!_poprz.TryGetValue(p.Nazwa, out var b))
        {
            _poprz[p.Nazwa] = m;
            if (brak) Komunikat(km, "BRAK TRANSMISJI", 2);
            else
            {
                Komunikat(km, m.Ostrz ? "OSTRZEGANIE" : "OCZEKIWANIE");
                if (m.Awaria) { Komunikat(km, "USTERKA kat. I", 1); Komunikat(km, "\"AWARIA SYSTEMU\" – usterka kat. I", 1); }
            }
            return;
        }
        _poprz[p.Nazwa] = m;
        if (m.Brak != b.Brak) Komunikat(km, m.Brak ? "BRAK TRANSMISJI" : "TRANSMISJA OK.", m.Brak ? 2 : 0);
        if (m.Brak) return;
        if (m.Awaria != b.Awaria)
        {
            if (m.Awaria) { Komunikat(km, "USTERKA kat. I", 1); Komunikat(km, "\"AWARIA SYSTEMU\" – usterka kat. I", 1); }
            else { Komunikat(km, "Zanik usterki kat. I"); Komunikat(km, "\"Zanik awarii systemu\""); }
        }
        if (m.Ostrz != b.Ostrz)
        {
            Komunikat(km, m.Ostrz ? "OSTRZEGANIE" : "OCZEKIWANIE");
            Komunikat(km, m.Ostrz ? "Załączony sygnalizator S12" : "Wyłączony sygnalizator S12");
        }
        static string Naped(string r) => r switch { "zamknięte" => "opuszczony", "otwarte" => "podniesiony", "w ruchu" => "położenie pośrednie", _ => null };
        if (m.RogWj != b.RogWj && Naped(m.RogWj) is string nj) Komunikat(km, $"Napęd N1 {nj}");
        if (m.RogWy != b.RogWy && Naped(m.RogWy) is string ny) Komunikat(km, $"Napęd N3 {ny}");
        if (m.Tory != b.Tory)
        {
            if ((m.Tory >= 1) != (b.Tory >= 1)) Komunikat(km, m.Tory >= 1 ? "TOR 1: czujniki wyłączone" : "TOR 1: czujniki załączone");
            if ((m.Tory >= 2) != (b.Tory >= 2)) Komunikat(km, m.Tory >= 2 ? "TOR 2: czujniki wyłączone" : "TOR 2: czujniki załączone");
        }
    }
}

/// <summary>EKRAN I – pole jednego przejazdu (RASP-UZK 4.4Ft): nazwa / km, uproszczony schemat i elementy stanu.</summary>
public sealed class PoleRasp : FrameworkElement
{
    public Przejazd P; public bool Brak, Miga, Niekwitowana;
    static readonly Typeface Arial = new(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    static readonly Typeface ArialB = new(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    public const double Szer = 250, Wys = 214;
    public PoleRasp() { Width = Szer; Height = Wys; Margin = new Thickness(5); }

    protected override void OnRender(DrawingContext dc)
    {
        var p = P; if (p == null) return;
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        dc.DrawRectangle(RaspPanel.TloPola, new Pen(Niekwitowana && Miga ? RaspPanel.Czerwony : RaspPanel.Ramka, Niekwitowana ? 3 : 1.5), new Rect(0, 0, Szer, Wys));
        void T(string s, double x, double y, double size, Brush b, bool bold = false, double maxW = 0, bool srodek = false)
        {
            var ft = new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, bold ? ArialB : Arial, size, b, dpi);
            if (maxW > 0) { ft.MaxTextWidth = maxW; ft.Trimming = TextTrimming.CharacterEllipsis; ft.MaxLineCount = 1; }
            dc.DrawText(ft, new Point(srodek && maxW > 0 ? x + (maxW - Math.Min(ft.Width, maxW)) / 2 : x, y));
        }
        // nagłówek: nazwa (lokalizacja) i km
        dc.DrawRectangle(RaspPanel.Granat, null, new Rect(1, 1, Szer - 2, 22));
        T($"km {p.KmTekst}   LK{p.Linia}" + (p.Kat.Length > 0 ? $"  kat. {p.Kat}" : ""), 6, 4, 13, RaspPanel.Bialy, true);

        // STAN – uproszczony schemat przejazdu: biały – brak transmisji, fioletowy – oczekiwanie, szary – ostrzeganie,
        // czerwony / biały migający – usterka
        Brush stan = Brak ? RaspPanel.Bialy : p.Awaria ? (Miga ? RaspPanel.Czerwony : RaspPanel.Bialy) : p.Ostrzeganie || p.Stan is "zamkniety" or "zamykanie" ? RaspPanel.Szary : RaspPanel.Fiolet;
        T("STAN", 8, 28, 10, RaspPanel.Czarny, true);
        var sch = new Rect(8, 42, 82, 92);
        dc.DrawRectangle(RaspPanel.Czarny, null, sch);
        RaspPanel.Schemat(dc, sch, stan, Brak ? null : Przejazd.Rog(p.Wj), Brak ? null : Przejazd.Rog(p.Wy), Miga);

        // elementy stanu: (nazwa, barwa, napis)
        bool bt = Brak;
        var el = new (string N, Brush B, string Napis)[]
        {
            ("STEROWANIE", bt ? RaspPanel.Bialy : RaspPanel.Zielony, "OK auto"),
            ("ZEROWANIE", bt ? RaspPanel.Bialy : RaspPanel.Szary, "OK zer."),
            ("USTERKA I", bt ? RaspPanel.Bialy : p.Awaria ? (Miga ? RaspPanel.Czerwony : RaspPanel.Szary) : RaspPanel.Szary, p.Awaria && !bt ? "Ust. kat.1" : "OK ust.1"),
            ("USTERKA II", bt ? RaspPanel.Bialy : RaspPanel.Szary, "OK ust.2"),
            ("TRANSMISJA", bt ? RaspPanel.Czerwony : RaspPanel.Szary, bt ? "BRAK transm." : "OK transm."),
            ("ZASILANIE", bt ? RaspPanel.Bialy : RaspPanel.Szary, "OK 230V"),
            ("TOR I", bt ? RaspPanel.Bialy : p.ToryWyl.Count >= 1 ? RaspPanel.Czerwony : RaspPanel.Szary, p.ToryWyl.Count >= 1 && !bt ? "Tor 1 WYŁ." : "Tor 1 ZAŁ."),
            ("TOR II", bt ? RaspPanel.Bialy : p.ToryWyl.Count >= 2 ? RaspPanel.Czerwony : RaspPanel.Szary, p.ToryWyl.Count >= 2 && !bt ? "Tor 2 WYŁ." : "Tor 2 ZAŁ."),
        };
        double y = 28;
        foreach (var (n, b, napis) in el)
        {
            T(n, 98, y + 4, 9, RaspPanel.Czarny, true);
            dc.DrawRectangle(b, new Pen(RaspPanel.Ramka, 1), new Rect(170, y, 74, 19));
            T(napis, 170, y + 3, 10, b == RaspPanel.Czerwony || b == RaspPanel.Zielony ? RaspPanel.Bialy : RaspPanel.Czarny, false, 74, true);
            y += 22;
        }
        // stan słownie (DTR tabela 5)
        string s = Brak ? "BRAK TRANSMISJI" : p.Awaria ? "USTERKA I" : p.Ostrzeganie || p.Stan is "zamkniety" or "zamykanie" ? "OSTRZEGANIE" : "OCZEKIWANIE";
        T(s, 8, 140, 10.5, p.Awaria || Brak ? RaspPanel.Czerwony : RaspPanel.Czarny, true, 92);
        T("STER.AUTOMAT", 8, 158, 10, RaspPanel.Czarny, false, 90);
        if (p.NaPulpicie) T(p.Uzk ? "na pulpicie gry" : "pulpit gry", 8, 174, 9, RaspPanel.Ramka, false, 90);
        if (Niekwitowana) T("Ctrl+Enter – zaakceptuj", 8, Wys - 18, 10, RaspPanel.Czerwony, true, Szer - 16);
    }
}

/// <summary>EKRAN II – schemat wybranego przejazdu: tory, droga, napędy N1–N4 (wjazdowe / wyjazdowe), sygnalizatory drogowe.</summary>
public sealed class SzczegolyRasp : FrameworkElement
{
    public Przejazd P; public bool Brak, Miga;
    static readonly Typeface Arial = new(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight; if (w < 50 || h < 50) return;
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        dc.DrawRectangle(RaspPanel.Czarny, null, new Rect(0, 0, w, h));
        void T(string s, double x, double y, double size, Brush b) => dc.DrawText(new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Arial, size, b, dpi), new Point(x, y));
        var p = P;
        if (p == null) { T("Wybierz przejazd na dolnej belce", 20, 20, 14, RaspPanel.Bialy); return; }
        T($"EKRAN II – przejazd km {p.KmTekst}  LK{p.Linia}{(p.Kat.Length > 0 ? $"  kat. {p.Kat}" : "")}   {(Brak ? "BRAK TRANSMISJI" : p.Awaria ? "USTERKA I" : p.Ostrzeganie ? "OSTRZEGANIE" : "OCZEKIWANIE")}", 12, 8, 14,
          Brak || p.Awaria ? RaspPanel.Czerwony : RaspPanel.Bialy);

        double cx = w / 2, cy = h / 2 + 10;
        // tory (poziomo) i strefa przejazdu – zajęta (pociąg na przejeździe / ostrzeganie) – wypełniona
        // jezdnia – sam obrys (krawędzie i oś) w barwie stanu, tory po dwie szyny przechodzące przez jezdnię
        Brush droga = Brak ? RaspPanel.Bialy : p.Awaria ? (Miga ? RaspPanel.Czerwony : RaspPanel.Bialy) : p.Ostrzeganie ? RaspPanel.Szary : RaspPanel.Fiolet;
        var kraw = new Pen(droga, 4);
        dc.DrawLine(kraw, new Point(cx - 30, cy - 100), new Point(cx - 30, cy + 100));
        dc.DrawLine(kraw, new Point(cx + 30, cy - 100), new Point(cx + 30, cy + 100));
        dc.DrawLine(new Pen(droga, 2) { DashStyle = new DashStyle(new double[] { 3, 3 }, 0) }, new Point(cx, cy - 100), new Point(cx, cy + 100));
        var szyna = new Pen(RaspPanel.Szary, 2);
        foreach (var ty in new[] { cy - 22, cy + 22 })
        {
            dc.DrawLine(szyna, new Point(30, ty - 4), new Point(w - 30, ty - 4));
            dc.DrawLine(szyna, new Point(30, ty + 4), new Point(w - 30, ty + 4));
        }
        T("TOR 1", 34, cy - 44, 11, p.ToryWyl.Count >= 1 ? RaspPanel.Czerwony : RaspPanel.Szary);
        T("TOR 2", 34, cy + 30, 11, p.ToryWyl.Count >= 2 ? RaspPanel.Czerwony : RaspPanel.Szary);
        // napędy: N1 / N2 wjazdowe (lewy dół, prawa góra), N3 / N4 wyjazdowe
        void Naped(string n, double x, double y, int s, bool lewo)
        {
            var r = Przejazd.Rog(s);
            Brush b = Brak ? RaspPanel.Bialy : r == "zamknięte" ? RaspPanel.Czerwony : r == "w ruchu" ? (Miga ? RaspPanel.Zolty : RaspPanel.Czarny) : RaspPanel.Zielony;
            double dl = 46;
            var koniec = r == "zamknięte" ? new Point(x + (lewo ? dl : -dl), y) : r == "w ruchu" ? new Point(x + (lewo ? dl * 0.7 : -dl * 0.7), y - (y < cy ? -dl * 0.7 : dl * 0.7)) : new Point(x, y + (y < cy ? -dl : dl));
            dc.DrawLine(new Pen(b, 5), new Point(x, y), koniec);
            dc.DrawEllipse(RaspPanel.Szary, null, new Point(x, y), 5, 5);
            T(n, x + (lewo ? -24 : 8), y - 6, 11, RaspPanel.Bialy);
        }
        Naped("N1", cx - 50, cy + 80, p.Wj, true); Naped("N2", cx + 50, cy - 80, p.Wj, false);
        Naped("N3", cx + 50, cy + 80, p.Wy, false); Naped("N4", cx - 50, cy - 80, p.Wy, true);
        // sygnalizatory drogowe S1 / S2: czerwone migające przy ostrzeganiu
        void Syg(string n, double x, double y)
        {
            bool on = !Brak && p.Ostrzeganie;
            dc.DrawEllipse(on && Miga ? RaspPanel.Czerwony : RaspPanel.CiemnoCzerw, new Pen(RaspPanel.Szary, 1), new Point(x, y), 7, 7);
            dc.DrawEllipse(on && !Miga ? RaspPanel.Czerwony : RaspPanel.CiemnoCzerw, new Pen(RaspPanel.Szary, 1), new Point(x + 16, y), 7, 7);
            T(n, x - 4, y + 10, 10, RaspPanel.Bialy);
        }
        Syg("S1", cx - 120, cy + 70); Syg("S2", cx + 104, cy - 82);
        T("Strefy czujników (C1C2 … C13C14), zasilanie i napędy pojedynczo – gra ich nie podaje.", 12, h - 22, 11, RaspPanel.Szary);
    }
}

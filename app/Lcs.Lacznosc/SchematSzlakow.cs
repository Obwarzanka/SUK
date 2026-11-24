using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Lcs.Lacznosc;

/// <summary>
/// Pulpit terminala stacji stycznej (styl EbiScreen / MOR): nasza stacja – sześciokąt w środku, szlaki do posterunków
/// sąsiednich po obu stronach (tory z nazwami z komunikatów gry), okienka numerów pociągów w kolorze stanu EZP,
/// posterunki sąsiednie – małe sześciokąty z polem „EZP O: W:”. Klik w pole / sąsiada – wybór posterunku w panelu EZP,
/// PPM – przeniesienie szlaku na drugą stronę.
/// </summary>
public sealed class SchematSzlakow : FrameworkElement
{
    public string Stacja { get; set; } = "";
    public IList<Szlak> Szlaki { get; set; } = new List<Szlak>();
    public string Wybrany { get; set; }
    /// <summary>Sąsiad → (O: nieodczytane, W: wysłane bez odpowiedzi, awizacja auto włączona).</summary>
    public Func<string, (int O, int W, bool Auto)> Licznik { get; set; } = _ => (0, 0, false);

    public event Action<string> WybranoSasiada;
    /// <summary>sąsiad, true – po lewej stronie stacji.</summary>
    public event Action<string, bool> UstawStrone;
    public event Action<string> UstawTory;
    /// <summary>Klik w okienko pociągu: sąsiad, numer.</summary>
    public event Action<string, string> WybranoPociag;
    /// <summary>Usunięcie pociągu ze szlaku bez wysyłania komunikatu: sąsiad, numer.</summary>
    public event Action<string, string> AnulujPociag;

    static readonly Brush Tor = Zamroz(new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)));
    static readonly Brush TorZaj = Zamroz(new SolidColorBrush(Color.FromRgb(0xE0, 0x00, 0x00)));
    static readonly Brush Sasiedni = Zamroz(new SolidColorBrush(Color.FromRgb(0x7A, 0x7A, 0x7A)));
    static readonly Brush Szary = Zamroz(new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x70)));
    static readonly Brush Napis = Zamroz(new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8)));
    static readonly Brush Pusty = Zamroz(new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)));
    static readonly Brush EzpTlo = Zamroz(new SolidColorBrush(Color.FromRgb(0x5B, 0x64, 0x78)));
    static readonly Brush EzpWyb = Zamroz(new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xC8)));
    static readonly Brush Zolty = Zamroz(new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0x00)));
    static readonly Brush Cyjan = Zamroz(new SolidColorBrush(Color.FromRgb(0x00, 0xE0, 0xFF)));
    static readonly Brush Belka = Zamroz(new SolidColorBrush(Color.FromRgb(0x29, 0xB6, 0xD6)));
    static readonly Brush Odm = Zamroz(new SolidColorBrush(Color.FromRgb(0x50, 0x50, 0x50)));
    static readonly Typeface Mono = new("Consolas");
    static Brush Zamroz(Brush b) { b.Freeze(); return b; }

    readonly List<(Rect R, string Sasiad)> _cele = new();
    readonly List<(Rect R, string Sasiad, string Nr)> _okna = new();
    bool _miga;

    public SchematSzlakow()
    {
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        t.Tick += (_, _) => { _miga = !_miga; if (Szlaki.Any(s => s.Pociagi.Any(p => p.Stan is StanOkna.Zapytanie or StanOkna.Stoj)) || Szlaki.Any(s => Licznik(s.Sasiad).O > 0)) InvalidateVisual(); };
        t.Start();
        Cursor = Cursors.Arrow;
    }

    protected override Size MeasureOverride(Size a) => new(double.IsInfinity(a.Width) ? 900 : a.Width, double.IsInfinity(a.Height) ? 300 : a.Height);

    protected override void OnRender(DrawingContext dc)
    {
        double W = ActualWidth, H = ActualHeight;
        dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, W, H));
        _cele.Clear(); _okna.Clear();
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        double CX = W / 2, CY = H / 2, HW = Math.Min(260, W * 0.22);
        const double Wej = 16, Sciecie = 18;   // odstęp wejść torów w bok stacji, ścięcie narożników stacji

        var lewe = Szlaki.Where(s => s.Lewa).ToList();
        var prawe = Szlaki.Where(s => !s.Lewa).ToList();
        // wysokość wiązki wejść po jednej stronie: tory co Wej, między szlakami dodatkowy odstęp
        double Wiazka(List<Szlak> l) => l.Count == 0 ? 0 : (l.Sum(s => s.Tory.Count > 0 ? s.Tory.Count : 2) - 1) * Wej + (l.Count - 1) * Wej / 2;
        double maxWiazka = Math.Max(Wiazka(lewe), Wiazka(prawe));
        // stacja: ośmiokąt z płaskimi bokami – każdy tor wchodzi w bok osobno, poziomo
        double HH = Math.Max(60, maxWiazka + 2 * Sciecie + 16);
        Strona(dc, lewe, -1); Strona(dc, prawe, 1);

        Stacja8(dc, CX, CY, HW, HH, Sciecie, new Pen(Tor, 4), Brushes.Black);
        Tekst(dc, Stacja, CX, CY, 16, Brushes.White, dpi, srodek: true);

        void Strona(DrawingContext dc, List<Szlak> lista, int sx)
        {
            int k = lista.Count; if (k == 0) return;
            double xKrawedz = CX + sx * HW / 2;                  // bok stacji
            double yWej = CY - Wiazka(lista) / 2;              // kolejne wejścia od góry
            for (int i = 0; i < k; i++)
            {
                var s = lista[i];
                double dy = k == 1 ? 0 : (-1 + 2.0 * i / (k - 1)) * Math.Min(H / 2 - 70, 40 + 45 * (k - 1));
                double yS = CY + dy;
                var tory = s.Tory.Count > 0 ? s.Tory.Select(t => (int?)t).ToList() : new List<int?> { null, null };
                int n = tory.Count;
                double xN = sx < 0 ? 52 : W - 52;                // środek sześciokąta sąsiada
                double hN = Math.Max(26, (n - 1) * 30 + 22), wN = Math.Max(64, hN + 20);
                var okna = Przydziel(s, tory);
                // koniec łuku szlaku – wspólny dla wszystkich torów szlaku, więc tory biegną równolegle i się nie krzyżują
                double xLuk = xKrawedz + sx * 30;
                for (int t = 0; t < n; t++)
                {
                    double y = yS + (t - (n - 1) / 2.0) * 30;
                    double xStart = xN - sx * (wN / 2 - Math.Abs(y - yS));
                    double yE = yWej; yWej += Wej;                                // wejście toru w bok stacji
                    double xK = xLuk + sx * Math.Abs(y - yE);                     // początek skosu 45°
                    var naTorze = okna[t];
                    bool zaj = naTorze.Any(p => p.Stan is StanOkna.NaSzlaku or StanOkna.Stoj);
                    var pen = new Pen(zaj ? TorZaj : Tor, 4) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
                    var g = new StreamGeometry();
                    using (var c = g.Open())
                    {
                        c.BeginFigure(new Point(xStart, y), false, false);
                        c.LineTo(new Point(xK, y), true, true); c.LineTo(new Point(xLuk, yE), true, true); c.LineTo(new Point(xKrawedz, yE), true, true);
                    }
                    g.Freeze(); dc.DrawGeometry(null, pen, g);
                    var etk = tory[t]?.ToString() ?? "?";
                    Tekst(dc, etk, xStart - sx * 8, y - 15, 10, Szary, dpi, wyrownaj: sx < 0 ? 1 : -1);
                    // kilka pociągów na jednym torze (np. jeden jedzie, na następny już zgoda) – okienka obok siebie
                    double lo = Math.Min(xStart, xK) + 44, hi = Math.Max(xStart, xK) - 44, mid = (xStart + xK) / 2;
                    if (naTorze.Count == 0) Okno(dc, mid, y, null, sx, dpi);
                    double krok = naTorze.Count > 1 ? Math.Min(88, Math.Max(0, hi - lo) / (naTorze.Count - 1)) : 0;
                    for (int q = 0; q < naTorze.Count; q++)
                    {
                        double x = mid + sx * ((naTorze.Count - 1) / 2.0 - q) * krok;
                        Okno(dc, x, y, naTorze[q], sx, dpi);
                        _okna.Add((new Rect(x - 42, y - 10, 84, 20), s.Sasiad, naTorze[q].Nr));
                    }
                }
                yWej += Wej / 2;                                  // odstęp przed wejściami następnego szlaku
                Szesciokat(dc, xN, yS, wN, hN, new Pen(Sasiedni, 3), Brushes.Black);

                // nazwa i pole EZP nad (albo pod) szlakiem
                double yNap = dy <= 0 ? yS - hN / 2 - 30 : yS + hN / 2 + 8;
                var nap = Tekst(dc, s.Sasiad, sx < 0 ? 8 : W - 8, yNap + 4, 13, Napis, dpi, wyrownaj: sx < 0 ? 1 : -1);
                double xE = sx < 0 ? 8 + nap + 12 : W - 8 - nap - 12 - 86;
                var (o, w, auto) = Licznik(s.Sasiad);
                bool alarm = o > 0;
                var rE = new Rect(xE, yNap, 86, 26);
                if (!(alarm && _miga))
                {
                    bool wyb = string.Equals(Wybrany, s.Sasiad, StringComparison.OrdinalIgnoreCase);
                    dc.DrawRectangle(alarm ? Zolty : wyb ? EzpWyb : EzpTlo, new Pen(wyb ? Belka : Brushes.Silver, wyb ? 2 : 1), rE);
                    var kol = alarm || wyb ? Brushes.Black : Brushes.White;
                    Tekst(dc, "EZP" + (auto ? "  A" : ""), rE.X + 43, rE.Y + 6, 9, kol, dpi, srodek: true);
                    Tekst(dc, $"O:{o}  W:{w}", rE.X + 43, rE.Y + 18, 11, kol, dpi, srodek: true);
                }
                _cele.Add((rE, s.Sasiad));
                _cele.Add((new Rect(xN - wN / 2, yS - hN / 2 - 32, wN + 12, hN + 34), s.Sasiad));
            }
        }
    }

    /// <summary>Okienko na każdym torze: pociąg z tym torem (najnowszy), pociągi bez toru – na wolne tory.</summary>
    /// <summary>Pociągi na tory szlaku: wg toru z komunikatu; bez toru (lub z nieznanym) – na tor z najmniejszą liczbą okienek. Nikt nie ginie.</summary>
    static List<PociagNaSzlaku>[] Przydziel(Szlak s, List<int?> tory)
    {
        var wynik = Enumerable.Range(0, Math.Max(1, tory.Count)).Select(_ => new List<PociagNaSzlaku>()).ToArray();
        foreach (var p in s.Pociagi.OrderBy(x => x.Zmiana))
        {
            int i = p.Tor.HasValue ? tory.IndexOf(p.Tor) : -1;
            if (i < 0) i = Array.IndexOf(wynik, wynik.OrderBy(l => l.Count).First());
            wynik[i].Add(p);
        }
        // kolejność od strony sąsiada: do sąsiada – najstarszy najdalej; do nas – najstarszy najbliżej stacji
        for (int i = 0; i < wynik.Length; i++) wynik[i] = wynik[i].OrderBy(p => p.OdNas ? p.Zmiana.Ticks : -p.Zmiana.Ticks).ToList();
        return wynik;
    }

    void Okno(DrawingContext dc, double x, double y, PociagNaSzlaku p, int sx, double dpi)
    {
        var r = new Rect(x - 40, y - 8, 80, 16);
        if (p == null) { dc.DrawRectangle(Brushes.Black, new Pen(Pusty, 1), r); return; }
        if (p.Stan is StanOkna.Zapytanie or StanOkna.Stoj && _miga) { dc.DrawRectangle(Brushes.Black, new Pen(Pusty, 1), r); return; }
        var (tlo, ramka, tekst) = p.Stan switch
        {
            StanOkna.Zapytanie => (Brushes.Black, Cyjan, Cyjan),
            StanOkna.Zgoda => (Zolty, Zolty, (Brush)Brushes.Black),
            StanOkna.NaSzlaku => (TorZaj, TorZaj, Brushes.White),
            StanOkna.Stoj => (TorZaj, Brushes.White, Brushes.White),
            _ => (Odm, Szary, Brushes.Silver),
        };
        dc.DrawRectangle(tlo, new Pen(ramka, p.Stan == StanOkna.Stoj ? 2 : 1), r);
        bool wPrawo = p.OdNas == (sx > 0);
        var t = (wPrawo ? "" : "◄") + p.Nr + (wPrawo ? "►" : "");
        var ft = new FormattedText(t, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Mono, 12, tekst, dpi);
        if (p.Stan == StanOkna.Odmowa) ft.SetTextDecorations(TextDecorations.Strikethrough);
        dc.DrawText(ft, new Point(x - ft.Width / 2, y - ft.Height / 2));
    }

    /// <summary>Stacja: prostokąt ze ściętymi narożnikami (płaskie boki na wejścia torów).</summary>
    static void Stacja8(DrawingContext dc, double cx, double cy, double w, double h, double c, Pen pen, Brush wyp)
    {
        double l = cx - w / 2, p = cx + w / 2, g = cy - h / 2, d = cy + h / 2;
        var geo = new StreamGeometry();
        using (var k = geo.Open())
        {
            k.BeginFigure(new Point(l + c, g), true, true);
            foreach (var pt in new[] { new Point(p - c, g), new Point(p, g + c), new Point(p, d - c), new Point(p - c, d), new Point(l + c, d), new Point(l, d - c), new Point(l, g + c) })
                k.LineTo(pt, true, true);
        }
        geo.Freeze();
        pen.LineJoin = PenLineJoin.Round;
        dc.DrawGeometry(wyp, pen, geo);
    }

    static void Szesciokat(DrawingContext dc, double cx, double cy, double w, double h, Pen pen, Brush wyp)
    {
        double a = h / 2;
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(cx - w / 2, cy), true, true);
            c.LineTo(new Point(cx - w / 2 + a, cy - a), true, true); c.LineTo(new Point(cx + w / 2 - a, cy - a), true, true);
            c.LineTo(new Point(cx + w / 2, cy), true, true); c.LineTo(new Point(cx + w / 2 - a, cy + a), true, true);
            c.LineTo(new Point(cx - w / 2 + a, cy + a), true, true);
        }
        g.Freeze();
        pen.LineJoin = PenLineJoin.Round;
        dc.DrawGeometry(wyp, pen, g);
    }

    /// <summary>Napis; wyrownaj 1 – od x w prawo, -1 – do x; srodek – wyśrodkowany w (x, y). Zwraca szerokość.</summary>
    static double Tekst(DrawingContext dc, string t, double x, double y, double rozm, Brush b, double dpi, int wyrownaj = 1, bool srodek = false)
    {
        var ft = new FormattedText(t ?? "", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Mono, rozm, b, dpi);
        var p = srodek ? new Point(x - ft.Width / 2, y - ft.Height / 2) : new Point(wyrownaj > 0 ? x : x - ft.Width, y);
        dc.DrawText(ft, p);
        return ft.Width;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        var o = TrafioneOkno(e.GetPosition(this));
        if (o.Nr != null) { WybranoPociag?.Invoke(o.Sasiad, o.Nr); e.Handled = true; return; }
        var s = Trafiony(e.GetPosition(this));
        if (s != null) { WybranoSasiada?.Invoke(s); e.Handled = true; }
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        var o = TrafioneOkno(e.GetPosition(this));
        if (o.Nr != null)
        {
            var mo = new System.Windows.Controls.ContextMenu();
            var w = new System.Windows.Controls.MenuItem { Header = $"Pociąg {o.Nr} – EZP z posterunkiem {o.Sasiad}" };
            w.Click += (_, _) => WybranoPociag?.Invoke(o.Sasiad, o.Nr);
            var a = new System.Windows.Controls.MenuItem { Header = $"Anuluj pociąg {o.Nr} (bez wiadomości)" };
            a.Click += (_, _) => AnulujPociag?.Invoke(o.Sasiad, o.Nr);
            mo.Items.Add(w); mo.Items.Add(a);
            mo.IsOpen = true; e.Handled = true;
            return;
        }
        var s = Trafiony(e.GetPosition(this));
        if (s == null) return;
        var m = new System.Windows.Controls.ContextMenu();
        bool lewa = Szlaki?.FirstOrDefault(x => x.Sasiad == s)?.Lewa ?? true;
        var l = new System.Windows.Controls.MenuItem { Header = $"„{s}” po lewej stronie", IsCheckable = true, IsChecked = lewa };
        l.Click += (_, _) => UstawStrone?.Invoke(s, true);
        var p = new System.Windows.Controls.MenuItem { Header = $"„{s}” po prawej stronie", IsCheckable = true, IsChecked = !lewa };
        p.Click += (_, _) => UstawStrone?.Invoke(s, false);
        m.Items.Add(l); m.Items.Add(p);
        m.Items.Add(new System.Windows.Controls.Separator());
        var t = new System.Windows.Controls.MenuItem { Header = $"Tory szlaku „{s}”…" };
        t.Click += (_, _) => UstawTory?.Invoke(s);
        m.Items.Add(t);
        m.IsOpen = true;
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e) =>
        Cursor = Trafiony(e.GetPosition(this)) != null || TrafioneOkno(e.GetPosition(this)).Nr != null ? Cursors.Hand : Cursors.Arrow;

    (Rect R, string Sasiad, string Nr) TrafioneOkno(Point p) => _okna.FirstOrDefault(c => c.R.Contains(p));

    string Trafiony(Point p) => _cele.FirstOrDefault(c => c.R.Contains(p)).Sasiad;
}

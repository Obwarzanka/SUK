using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Lcs.EdytorTaboru;

/// <summary>
/// Prosty wykres liniowy (etap B4): osie z podziałką, serie (linia ciągła / przerywana, schodkowa), legenda. Seria z uchwytami pozwala
/// przeciągać punkty (v między sąsiednimi punktami, F ≥ 0), dodawać punkt dwuklikiem i usuwać prawym przyciskiem – zmiana idzie
/// do zdarzeń, a zapis (z cofaniem) robi okno.
/// </summary>
public class Wykres : FrameworkElement
{
    public class Seria
    {
        public string Nazwa = "";
        public Color Kolor = Colors.Black;
        public bool Przerywana, Schodki, Uchwyty;
        public double Grubosc = 1.6;
        public List<Point> Punkty = new();
        /// <summary>Dowolny znacznik (np. zasilanie charakterystyki).</summary>
        public object Tag;
    }

    public string Tytul = "", OsX = "", OsY = "";
    public List<Seria> Serie = new();
    /// <summary>Zakres osi; 0 – z danych.</summary>
    public double XMax, YMax, YMin;
    public event Action<Seria, int, Point> PunktPrzesuniety;
    public event Action<Seria, Point> PunktDodany;
    public event Action<Seria, int> PunktUsuniety;

    const double L = 56, R = 16, T = 26, B = 40;
    (Seria S, int I)? drag; Point dragPt;
    static readonly Typeface Font = new("Segoe UI");

    public Wykres() { Height = 300; Focusable = true; }

    public void Odswiez() => InvalidateVisual();

    (double X0, double X1, double Y0, double Y1) Zakres()
    {
        var all = Serie.SelectMany(s => s.Punkty).ToList();
        double x1 = XMax > 0 ? XMax : all.Count > 0 ? all.Max(p => p.X) : 1;
        double y1 = YMax > 0 ? YMax : all.Count > 0 ? all.Max(p => p.Y) * 1.1 : 1;
        double y0 = YMin != 0 ? YMin : Math.Min(0, all.Count > 0 ? all.Min(p => p.Y) * 1.1 : 0);
        return (0, Nice(Math.Max(x1, 1e-6)), y0 < 0 ? -Nice(-y0) : 0, Nice(Math.Max(y1, 1e-6)));
    }

    static double Nice(double v) { double e = Math.Pow(10, Math.Floor(Math.Log10(v))); foreach (var m in new[] { 1, 1.2, 1.5, 2, 2.5, 3, 4, 5, 6, 8, 10 }) if (m * e >= v - 1e-9) return m * e; return 10 * e; }
    static double Krok(double span) { double raw = span / 6, e = Math.Pow(10, Math.Floor(Math.Log10(raw))); foreach (var m in new[] { 1, 2, 2.5, 5, 10 }) if (m * e >= raw) return m * e; return 10 * e; }

    public Point Ekran(double x, double y)
    {
        var (x0, x1, y0, y1) = Zakres();
        double w = Math.Max(10, ActualWidth - L - R), h = Math.Max(10, ActualHeight - T - B);
        return new Point(L + (x - x0) / (x1 - x0) * w, T + h - (y - y0) / (y1 - y0) * h);
    }

    public Point Dane(Point p)
    {
        var (x0, x1, y0, y1) = Zakres();
        double w = Math.Max(10, ActualWidth - L - R), h = Math.Max(10, ActualHeight - T - B);
        return new Point(x0 + (p.X - L) / w * (x1 - x0), y0 + (T + h - p.Y) / h * (y1 - y0));
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.White, new Pen(new SolidColorBrush(Color.FromRgb(0xc8, 0xc8, 0xc2)), 1), new Rect(0, 0, ActualWidth, ActualHeight));
        var (x0, x1, y0, y1) = Zakres();
        var grid = new Pen(new SolidColorBrush(Color.FromRgb(0xe4, 0xe4, 0xe0)), 1); var axis = new Pen(Brushes.Gray, 1);
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        void Txt(string s, Point p, double align, double size = 11, Brush b = null)
        {
            var ft = new FormattedText(s, CultureInfo.GetCultureInfo("pl-PL"), FlowDirection.LeftToRight, Font, size, b ?? Brushes.DimGray, dpi);
            dc.DrawText(ft, new Point(p.X - ft.Width * align, p.Y - ft.Height / 2));
        }
        double kx = Krok(x1 - x0), ky = Krok(y1 - y0);
        for (double x = x0; x <= x1 + 1e-9; x += kx) { var a = Ekran(x, y0); var b = Ekran(x, y1); dc.DrawLine(grid, a, b); Txt(x.ToString("0.##", CultureInfo.GetCultureInfo("pl-PL")), new Point(a.X, a.Y + 10), 0.5); }
        for (double y = y0; y <= y1 + 1e-9; y += ky) { var a = Ekran(x0, y); var b = Ekran(x1, y); dc.DrawLine(Math.Abs(y) < 1e-9 ? axis : grid, a, b); Txt(y.ToString("0.##", CultureInfo.GetCultureInfo("pl-PL")), new Point(a.X - 6, a.Y), 1); }
        dc.DrawLine(axis, Ekran(x0, y0), Ekran(x0, y1));
        Txt(Tytul, new Point(L, 12), 0, 13, Brushes.Black);
        Txt(OsX, new Point(ActualWidth - R, ActualHeight - 10), 1);
        Txt(OsY, new Point(L + 4, T + 8), 0);
        dc.PushClip(new RectangleGeometry(new Rect(L - 1, T - 1, Math.Max(0, ActualWidth - L - R + 2), Math.Max(0, ActualHeight - T - B + 2))));
        foreach (var s in Serie)
        {
            if (s.Punkty.Count == 0) continue;
            var pen = new Pen(new SolidColorBrush(s.Kolor), s.Grubosc);
            if (s.Przerywana) pen.DashStyle = new DashStyle(new double[] { 4, 3 }, 0);
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(Ekran(s.Punkty[0].X, s.Punkty[0].Y), false, false);
                for (int i = 1; i < s.Punkty.Count; i++)
                {
                    if (s.Schodki) c.LineTo(Ekran(s.Punkty[i].X, s.Punkty[i - 1].Y), true, false);
                    c.LineTo(Ekran(s.Punkty[i].X, s.Punkty[i].Y), true, false);
                }
            }
            dc.DrawGeometry(null, pen, g);
            if (s.Uchwyty)
                for (int i = 0; i < s.Punkty.Count; i++)
                {
                    bool d = drag is var (ds, di) && ds == s && di == i;
                    dc.DrawRectangle(d ? new SolidColorBrush(s.Kolor) : Brushes.White, new Pen(new SolidColorBrush(s.Kolor), 1.4), new Rect(Ekran(s.Punkty[i].X, s.Punkty[i].Y) - new Vector(4, 4), new Size(8, 8)));
                }
        }
        dc.Pop();
        // legenda na białym tle (nie ginie na liniach wykresu)
        double ly = T + 6;
        int nLeg = Serie.Count(s => s.Nazwa.Length > 0);
        if (nLeg > 0) dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0xe8, 0xff, 0xff, 0xff)), new Pen(new SolidColorBrush(Color.FromRgb(0xd8, 0xd8, 0xd4)), 1), new Rect(ActualWidth - R - 216, T - 2, 210, nLeg * 15 + 6));
        foreach (var s in Serie.Where(s => s.Nazwa.Length > 0))
        {
            var pen = new Pen(new SolidColorBrush(s.Kolor), 2); if (s.Przerywana) pen.DashStyle = new DashStyle(new double[] { 3, 2 }, 0);
            double lx = ActualWidth - R - 210;
            dc.DrawLine(pen, new Point(lx, ly), new Point(lx + 22, ly));
            Txt(s.Nazwa, new Point(lx + 28, ly), 0, 11, Brushes.Black);
            ly += 15;
        }
        if (drag != null) Txt($"v {dragPt.X:0.#} km/h, F {dragPt.Y:0.#} kN", new Point(L + 6, ActualHeight - B - 10), 0, 11, Brushes.Black);
    }

    (Seria, int)? Trafiony(Point p)
    {
        foreach (var s in Serie.Where(s => s.Uchwyty))
            for (int i = 0; i < s.Punkty.Count; i++)
                if ((Ekran(s.Punkty[i].X, s.Punkty[i].Y) - p).Length <= 7) return (s, i);
        return null;
    }

    /// <summary>Nowe położenie przeciąganego punktu: v między sąsiadami (o 0,5 km/h), F ≥ 0, zaokrąglenie do 0,1.</summary>
    Point Ogranicz(Seria s, int i, Point d)
    {
        double lo = i > 0 ? s.Punkty[i - 1].X + 0.5 : 0, hi = i < s.Punkty.Count - 1 ? s.Punkty[i + 1].X - 0.5 : double.MaxValue;
        return new Point(Math.Round(Math.Clamp(d.X, lo, Math.Max(lo, hi)), 1), Math.Round(Math.Max(0, d.Y), 1));
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var p = e.GetPosition(this);
        if (e.ClickCount == 2) { DodajW(p); e.Handled = true; return; }
        if (Trafiony(p) is var (s, i)) { drag = (s, i); dragPt = s.Punkty[i]; CaptureMouse(); e.Handled = true; InvalidateVisual(); }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (drag is not var (s, i)) { Cursor = Trafiony(e.GetPosition(this)) != null ? Cursors.Hand : null; return; }
        dragPt = Ogranicz(s, i, Dane(e.GetPosition(this)));
        s.Punkty[i] = dragPt; InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (drag is not var (s, i)) return;
        drag = null; ReleaseMouseCapture(); InvalidateVisual();
        PunktPrzesuniety?.Invoke(s, i, dragPt);
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        if (Trafiony(e.GetPosition(this)) is var (s, i)) { PunktUsuniety?.Invoke(s, i); e.Handled = true; }
    }

    void DodajW(Point p)
    {
        var d = Dane(p);
        // seria z uchwytami najbliższa w pionie przy tej prędkości
        var cel = Serie.Where(s => s.Uchwyty && s.Punkty.Count > 0).OrderBy(s => Math.Abs(Interp(s, d.X) - d.Y)).FirstOrDefault();
        if (cel != null && d.X > 0) PunktDodany?.Invoke(cel, new Point(Math.Round(d.X, 1), Math.Round(Math.Max(0, d.Y), 1)));
    }

    static double Interp(Seria s, double x)
    {
        var p = s.Punkty;
        if (x <= p[0].X) return p[0].Y;
        for (int i = 1; i < p.Count; i++) if (x <= p[i].X) return p[i - 1].Y + (p[i].Y - p[i - 1].Y) * (x - p[i - 1].X) / Math.Max(1e-9, p[i].X - p[i - 1].X);
        return p[^1].Y;
    }

    // ------------------------------------------------------------------ dla testu – te same ścieżki co mysz
    public void SimPrzeciagnij(Seria s, int i, Point dane)
    {
        drag = (s, i); dragPt = Ogranicz(s, i, dane); s.Punkty[i] = dragPt;
        drag = null; PunktPrzesuniety?.Invoke(s, i, dragPt);
    }
    public void SimDwuklik(Point dane) => DodajW(Ekran(dane.X, dane.Y));
    public void SimPrawy(Seria s, int i) => PunktUsuniety?.Invoke(s, i);
}

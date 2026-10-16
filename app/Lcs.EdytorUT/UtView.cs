using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Płótno edytora UT w metrach PUWG 1992. Północ (X) do góry, wschód (Y) w prawo.
/// Rysowanie w pikselach ekranu – przeliczenie świat → ekran w double (współrzędne rzędu 10⁵ m nie tracą dokładności w WPF).
/// Mysz: kółko – przybliżenie wokół kursora, przeciąganie LPM / środkowym przyciskiem / Spacja + LPM – przesuwanie.
/// </summary>
public class UtView : FrameworkElement
{
    public UtDocument Doc;
    public const double MinMpp = 0.002, MaxMpp = 2000;
    public const double RulerTop = 22, RulerLeft = 70;

    XY center = new(470000, 500000);
    double mpp = 1000;
    public XY Center => center;
    /// <summary>Metry na piksel ekranu.</summary>
    public double Mpp => mpp;
    public bool ShowGrid = true;
    /// <summary>Kursor nad płótnem (null – poza).</summary>
    public event Action<XY?> CursorMoved;
    public event Action ViewChanged;

    static readonly Typeface UiFont = new("Segoe UI"), MonoFont = new("Consolas");
    static readonly Brush Paper = Frozen(Color.FromRgb(0xfb, 0xfb, 0xf8)), RulerBg = Frozen(Color.FromRgb(0xee, 0xee, 0xea)),
        Ink = Frozen(Color.FromRgb(0x30, 0x30, 0x30)), Hint = Frozen(Color.FromRgb(0x80, 0x80, 0x80));
    static readonly Pen MinorPen = FrozenPen(Color.FromRgb(0xea, 0xea, 0xe6), 1), MajorPen = FrozenPen(Color.FromRgb(0xcc, 0xcc, 0xc6), 1),
        TickPen = FrozenPen(Color.FromRgb(0x70, 0x70, 0x70), 1), EdgePen = FrozenPen(Color.FromRgb(0xb0, 0xb0, 0xa8), 1),
        BarPen = FrozenPen(Color.FromRgb(0x30, 0x30, 0x30), 2);

    public UtView()
    {
        ClipToBounds = true; Focusable = true;
        MouseWheel += (s, e) => { ZoomAt(e.GetPosition(this), e.Delta > 0 ? 1 / 1.2 : 1.2); e.Handled = true; };
        MouseDown += OnDown; MouseMove += OnMove; MouseUp += OnUp;
        MouseLeave += (s, e) => CursorMoved?.Invoke(null);
        SizeChanged += (s, e) => InvalidateVisual();
    }

    // ------------------------------------------------------------------ przeliczenia
    double Cx => ActualWidth / 2;
    double Cy => ActualHeight / 2;
    public Point ToScreen(XY p) => new(Cx + (p.Y - center.Y) / mpp, Cy - (p.X - center.X) / mpp);
    public XY ToWorld(Point s) => new(center.X - (s.Y - Cy) * mpp, center.Y + (s.X - Cx) * mpp);

    public void SetView(XY c, double m)
    {
        center = c; mpp = Math.Clamp(m, MinMpp, MaxMpp);
        InvalidateVisual(); ViewChanged?.Invoke();
    }
    /// <summary>Przybliżenie (k &lt; 1) albo oddalenie (k &gt; 1) z punktem pod kursorem w miejscu.</summary>
    public void ZoomAt(Point s, double k)
    {
        var w = ToWorld(s);
        mpp = Math.Clamp(mpp * k, MinMpp, MaxMpp);
        // ten sam punkt świata zostaje pod kursorem
        center = new XY(w.X + (s.Y - Cy) * mpp, w.Y - (s.X - Cx) * mpp);
        InvalidateVisual(); ViewChanged?.Invoke();
    }
    /// <summary>Przesunięcie obrazu o wektor w pikselach (jak przeciągnięcie myszą).</summary>
    public void PanBy(Vector d)
    {
        center = new XY(center.X + d.Y * mpp, center.Y - d.X * mpp);
        InvalidateVisual(); ViewChanged?.Invoke();
    }

    // ------------------------------------------------------------------ mysz
    Point? panFrom;
    void OnDown(object s, MouseButtonEventArgs e)
    {
        Focus();
        if (e.ChangedButton == MouseButton.Middle || e.ChangedButton == MouseButton.Left)
        {
            panFrom = e.GetPosition(this); CaptureMouse(); Cursor = Cursors.SizeAll; e.Handled = true;
        }
    }
    void OnMove(object s, MouseEventArgs e)
    {
        var p = e.GetPosition(this);
        if (panFrom is Point f)
        {
            if (e.LeftButton == MouseButtonState.Pressed || e.MiddleButton == MouseButtonState.Pressed) { PanBy(p - f); panFrom = p; }
            else EndPan();
        }
        CursorMoved?.Invoke(ToWorld(p));
    }
    void OnUp(object s, MouseButtonEventArgs e) { if (panFrom != null) { EndPan(); e.Handled = true; } }
    void EndPan() { panFrom = null; ReleaseMouseCapture(); Cursor = null; }

    // ------------------------------------------------------------------ siatka
    /// <summary>Najmniejsza „okrągła” wartość 1 / 2 / 5 · 10ᵏ nie mniejsza niż v.</summary>
    public static double Nice(double v)
    {
        double p = Math.Pow(10, Math.Floor(Math.Log10(v)));
        foreach (var m in new[] { 1.0, 2, 5, 10 }) if (m * p >= v * (1 - 1e-9)) return m * p;
        return 10 * p;
    }
    /// <summary>Krok siatki drobnej (co najmniej 14 px) i głównej (podpisy na linijkach) [m].</summary>
    public (double Minor, double Major) GridSteps()
    {
        double minor = Nice(14 * mpp);
        return (minor, Nice(minor * 4.5));
    }

    // ------------------------------------------------------------------ rysowanie
    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight, dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        dc.DrawRectangle(Paper, null, new Rect(0, 0, w, h));
        if (w < 10 || h < 10) return;
        var (minor, major) = GridSteps();
        var tl = ToWorld(new Point(0, 0)); var br = ToWorld(new Point(w, h));
        if (ShowGrid)
        {
            foreach (var step in new[] { minor, major })
            {
                var pen = step == major ? MajorPen : MinorPen;
                for (double y = Math.Ceiling(tl.Y / step) * step; y <= br.Y; y += step)
                {
                    if (step == minor && IsMul(y, major)) continue;
                    double sx = Crisp(ToScreen(new XY(0, y)).X); dc.DrawLine(pen, new Point(sx, 0), new Point(sx, h));
                }
                for (double x = Math.Ceiling(br.X / step) * step; x <= tl.X; x += step)
                {
                    if (step == minor && IsMul(x, major)) continue;
                    double sy = Crisp(ToScreen(new XY(x, 0)).Y); dc.DrawLine(pen, new Point(0, sy), new Point(w, sy));
                }
            }
        }

        DrawHint(dc, w, dpi);
        DrawScaleBar(dc, h, dpi);
        DrawNorth(dc, w, dpi);
        DrawRulers(dc, w, h, minor, major, tl, br, dpi);
    }

    static bool IsMul(double v, double step) { double r = Math.Abs(v / step - Math.Round(v / step)); return r < 1e-6; }
    static double Crisp(double v) => Math.Round(v) + 0.5;

    void DrawRulers(DrawingContext dc, double w, double h, double minor, double major, XY tl, XY br, double dpi)
    {
        dc.DrawRectangle(RulerBg, null, new Rect(0, 0, w, RulerTop));
        dc.DrawRectangle(RulerBg, null, new Rect(0, 0, RulerLeft, h));
        int dec = Math.Max(0, (int)Math.Ceiling(-Math.Log10(major) - 1e-9));
        // linijka górna: Y (wschód)
        double lastLabel = double.NegativeInfinity;
        for (double y = Math.Ceiling(tl.Y / minor) * minor; y <= br.Y; y += minor)
        {
            double sx = Crisp(ToScreen(new XY(0, y)).X); if (sx < RulerLeft) continue;
            bool big = IsMul(y, major);
            dc.DrawLine(TickPen, new Point(sx, RulerTop - (big ? 9 : 4)), new Point(sx, RulerTop));
            if (!big) continue;
            var t = Text(Geo.M(y, dec), 10.5, Ink, dpi, MonoFont);
            if (sx - t.Width / 2 < lastLabel + 8 || sx + t.Width / 2 > w - 4) continue;
            dc.DrawText(t, new Point(sx - t.Width / 2, 1)); lastLabel = sx + t.Width / 2;
        }
        // linijka lewa: X (północ)
        lastLabel = double.NegativeInfinity;
        for (double x = Math.Ceiling(br.X / minor) * minor; x <= tl.X; x += minor)
        {
            double sy = Crisp(ToScreen(new XY(x, 0)).Y); if (sy < RulerTop) continue;
            bool big = IsMul(x, major);
            dc.DrawLine(TickPen, new Point(RulerLeft - (big ? 9 : 4), sy), new Point(RulerLeft, sy));
            if (!big) continue;
            var t = Text(Geo.M(x, dec), 10.5, Ink, dpi, MonoFont);
            if (sy - t.Height / 2 < RulerTop || sy + t.Height / 2 > h - 2) continue;
            // od dołu do góry – kolejne podpisy w odstępie co najmniej wysokości tekstu
            if (Math.Abs(sy - lastLabel) < t.Height + 4) continue;
            dc.DrawText(t, new Point(RulerLeft - 11 - t.Width, sy - t.Height / 2)); lastLabel = sy;
        }
        dc.DrawLine(EdgePen, new Point(RulerLeft, RulerTop - 0.5), new Point(w, RulerTop - 0.5));
        dc.DrawLine(EdgePen, new Point(RulerLeft - 0.5, RulerTop), new Point(RulerLeft - 0.5, h));
        dc.DrawRectangle(RulerBg, null, new Rect(0, 0, RulerLeft, RulerTop));
        var c = Text("Y →", 10, Hint, dpi, UiFont); dc.DrawText(c, new Point(RulerLeft - c.Width - 4, 4));
        var c2 = Text("X ↑", 10, Hint, dpi, UiFont); dc.DrawText(c2, new Point(4, 4));
    }

    void DrawScaleBar(DrawingContext dc, double h, double dpi)
    {
        double len = Nice(110 * mpp); double px = len / mpp;
        double x0 = RulerLeft + 16, y0 = h - 18;
        dc.DrawLine(BarPen, new Point(x0, y0), new Point(x0 + px, y0));
        dc.DrawLine(BarPen, new Point(x0, y0 - 5), new Point(x0, y0 + 1)); dc.DrawLine(BarPen, new Point(x0 + px, y0 - 5), new Point(x0 + px, y0 + 1));
        var t = Text(Len(len), 11, Ink, dpi, UiFont);
        dc.DrawText(t, new Point(x0 + px + 6, y0 - t.Height / 2 - 2));
    }

    void DrawNorth(DrawingContext dc, double w, double dpi)
    {
        double x = w - 26, y = RulerTop + 14;
        var g = new StreamGeometry();
        using (var c = g.Open()) { c.BeginFigure(new Point(x, y), true, true); c.LineTo(new Point(x + 6, y + 18), true, false); c.LineTo(new Point(x, y + 14), true, false); c.LineTo(new Point(x - 6, y + 18), true, false); }
        dc.DrawGeometry(Ink, null, g);
        var t = Text("N", 11, Ink, dpi, UiFont); dc.DrawText(t, new Point(x - t.Width / 2, y + 19));
    }

    void DrawHint(DrawingContext dc, double w, double dpi)
    {
        if (Doc == null || !IsEmpty(Doc.P)) return;
        var t = Text("Pusty projekt. W polu „Idź do” wpisz współrzędne obszaru: z Google Maps (np. 51.7631, 19.5438) albo X Y w PUWG 1992.", 12.5, Hint, dpi, UiFont);
        dc.DrawText(t, new Point(Math.Max(RulerLeft + 10, (w - t.Width) / 2), RulerTop + 12));
    }

    /// <summary>Projekt bez elementów rysunku (na razie zawsze – tory i podkłady dochodzą w kolejnych etapach).</summary>
    static bool IsEmpty(UtProject p) => true;

    public static string Len(double m) => m >= 1000 ? (m / 1000).ToString("0.###", Pl) + " km" : m >= 1 ? m.ToString("0.##", Pl) + " m" : (m * 100).ToString("0.#", Pl) + " cm";

    static readonly CultureInfo Pl = CultureInfo.GetCultureInfo("pl-PL");
    static FormattedText Text(string s, double size, Brush b, double dpi, Typeface f) => new(s, Pl, FlowDirection.LeftToRight, f, size, b, dpi);
    static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
    static Pen FrozenPen(Color c, double t) { var p = new Pen(Frozen(c), t); p.Freeze(); return p; }
}

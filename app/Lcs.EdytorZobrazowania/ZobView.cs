using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorZobrazowania;

/// <summary>
/// Płótno obrazu w jednostkach obrazu „j.” (PLAN_EDYTOR_ZOBRAZOWANIA.md § 4): czarne tło jak na pulpicie (Ie-104 § 4 ust. 13),
/// siatka co krok przyciągania (co 5 – grubsza), przybliżanie kółkiem wokół kursora, przesuwanie lewym albo środkowym przyciskiem.
/// Siatka, ramka obrazu i znacznik kursora są tylko w edytorze – nie należą do obrazu.
/// </summary>
public class ZobView : FrameworkElement
{
    public ZobObraz Obraz { get; set; }
    public int Siatka { get; set; } = 8;
    public bool ShowGrid { get; set; } = true;
    /// <summary>Tekst na pustym płótnie (brak otwartego zobrazowania).</summary>
    public string EmptyText { get; set; }

    /// <summary>Powiększenie: pikseli ekranu na 1 j.</summary>
    public double Zoom { get; private set; } = 0.5;
    /// <summary>Położenie punktu (0, 0) obrazu na ekranie.</summary>
    public Point Off { get; private set; } = new(20, 20);
    public const double MinZoom = 0.1, MaxZoom = 8;

    /// <summary>Punkt siatki pod kursorem [j.]; null – kursor poza płótnem.</summary>
    public (int X, int Y)? CursorPt { get; private set; }
    public event Action CursorMoved;
    public event Action ViewChanged;

    static Brush B(byte r, byte g, byte b) { var x = new SolidColorBrush(Color.FromRgb(r, g, b)); x.Freeze(); return x; }
    static Pen P(Brush b, double w) { var p = new Pen(b, w); p.Freeze(); return p; }
    public static readonly Brush Bg = B(0, 0, 0), Outside = B(0x2a, 0x2a, 0x2a), Hint = B(0x90, 0x90, 0x90);
    static readonly Pen GridMinor = P(B(0x16, 0x16, 0x16), 1), GridMajor = P(B(0x2c, 0x2c, 0x2c), 1), Frame = P(B(0x60, 0x60, 0x60), 1),
        CursorPen = P(B(0x00, 0x90, 0x90), 1);
    static readonly Typeface Ui = new("Segoe UI");

    Point? panFrom; Point panOff;

    public ZobView()
    {
        ClipToBounds = true; Focusable = true;
        MouseWheel += (s, e) => { ZoomAt(e.GetPosition(this), e.Delta > 0 ? 1.2 : 1 / 1.2); e.Handled = true; };
        MouseDown += (s, e) =>
        {
            if (e.ChangedButton is MouseButton.Left or MouseButton.Middle) { Focus(); panFrom = e.GetPosition(this); panOff = Off; CaptureMouse(); e.Handled = true; }
        };
        MouseUp += (s, e) => { if (panFrom != null) { panFrom = null; ReleaseMouseCapture(); Cursor = null; e.Handled = true; } };
        MouseMove += (s, e) =>
        {
            var p = e.GetPosition(this);
            if (panFrom is Point f && (e.LeftButton == MouseButtonState.Pressed || e.MiddleButton == MouseButtonState.Pressed))
            {
                Cursor = Cursors.SizeAll;
                PanTo(new Point(panOff.X + p.X - f.X, panOff.Y + p.Y - f.Y));
            }
            SetCursor(p);
        };
        MouseLeave += (s, e) => { CursorPt = null; CursorMoved?.Invoke(); InvalidateVisual(); };
        SizeChanged += (s, e) => InvalidateVisual();
    }

    void SetCursor(Point p)
    {
        var c = Snap(p);
        if (CursorPt == c) return;
        CursorPt = c; CursorMoved?.Invoke(); InvalidateVisual();
    }

    // ------------------------------------------------------------------ przeliczenia
    public Point ToScreen(double x, double y) => new(Off.X + x * Zoom, Off.Y + y * Zoom);
    public (double X, double Y) ToImage(Point p) => ((p.X - Off.X) / Zoom, (p.Y - Off.Y) / Zoom);
    /// <summary>Punkt siatki najbliższy punktowi ekranu [j.].</summary>
    public (int X, int Y) Snap(Point p)
    {
        var (x, y) = ToImage(p); int g = Math.Max(1, Siatka);
        return ((int)Math.Round(x / g) * g, (int)Math.Round(y / g) * g);
    }

    public void ZoomAt(Point s, double factor)
    {
        double z = Math.Clamp(Zoom * factor, MinZoom, MaxZoom);
        var (x, y) = ToImage(s);
        Zoom = z; Off = new Point(s.X - x * z, s.Y - y * z);
        ViewChanged?.Invoke(); InvalidateVisual();
    }

    public void PanTo(Point off) { Off = off; ViewChanged?.Invoke(); InvalidateVisual(); }

    /// <summary>Cały obraz w oknie (z marginesem).</summary>
    public void Fit()
    {
        if (Obraz == null || ActualWidth < 10 || ActualHeight < 10) return;
        double z = Math.Clamp(Math.Min((ActualWidth - 40) / Obraz.Szer, (ActualHeight - 40) / Obraz.Wys), MinZoom, MaxZoom);
        Zoom = z; Off = new Point((ActualWidth - Obraz.Szer * z) / 2, (ActualHeight - Obraz.Wys * z) / 2);
        ViewChanged?.Invoke(); InvalidateVisual();
    }

    /// <summary>Powiększenie 1 (1 j. = 1 piksel) wokół środka okna.</summary>
    public void Actual() => ZoomAt(new Point(ActualWidth / 2, ActualHeight / 2), 1 / Zoom);

    // ------------------------------------------------------------------ rysowanie
    protected override void OnRender(DrawingContext dc)
    {
        var all = new Rect(0, 0, ActualWidth, ActualHeight);
        dc.DrawRectangle(Outside, null, all);
        if (Obraz == null)
        {
            if (!string.IsNullOrEmpty(EmptyText))
            {
                var ft = new FormattedText(EmptyText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Ui, 15, Hint, 1.0) { MaxTextWidth = Math.Max(50, ActualWidth - 80), TextAlignment = TextAlignment.Center };
                dc.DrawText(ft, new Point((ActualWidth - ft.MaxTextWidth) / 2, ActualHeight / 2 - ft.Height / 2));
            }
            return;
        }
        var img = new Rect(ToScreen(0, 0), ToScreen(Obraz.Szer, Obraz.Wys));
        dc.DrawRectangle(Bg, null, img);
        var vis = Rect.Intersect(img, all);
        if (ShowGrid && !vis.IsEmpty) DrawGrid(dc, vis);
        dc.DrawRectangle(null, Frame, new Rect(Math.Round(img.X) - 0.5, Math.Round(img.Y) - 0.5, Math.Round(img.Width) + 1, Math.Round(img.Height) + 1));
        if (CursorPt is var (cx, cy) && cx >= 0 && cy >= 0 && cx <= Obraz.Szer && cy <= Obraz.Wys)
        {
            var c = ToScreen(cx, cy); double r = Math.Max(4, Siatka * Zoom * 0.6);
            dc.DrawLine(CursorPen, new Point(c.X - r, c.Y), new Point(c.X + r, c.Y));
            dc.DrawLine(CursorPen, new Point(c.X, c.Y - r), new Point(c.X, c.Y + r));
        }
    }

    void DrawGrid(DrawingContext dc, Rect vis)
    {
        int g = Math.Max(1, Siatka);
        double px = g * Zoom;
        // drobna siatka tylko, gdy krok ma co najmniej 5 px; gruba (co 5 kroków) – gdy co najmniej 12 px
        foreach (var (step, pen) in new[] { (g, GridMinor), (g * 5, GridMajor) })
        {
            if (step * Zoom < (step == g ? 5 : 12)) continue;
            var (x0, y0) = ToImage(vis.TopLeft); var (x1, y1) = ToImage(vis.BottomRight);
            for (int x = (int)Math.Ceiling(Math.Max(0, x0) / step) * step; x <= Math.Min(Obraz.Szer, x1); x += step)
            {
                if (pen == GridMinor && x % (g * 5) == 0) continue;
                double sx = Math.Round(ToScreen(x, 0).X) + 0.5;
                dc.DrawLine(pen, new Point(sx, vis.Top), new Point(sx, vis.Bottom));
            }
            for (int y = (int)Math.Ceiling(Math.Max(0, y0) / step) * step; y <= Math.Min(Obraz.Wys, y1); y += step)
            {
                if (pen == GridMinor && y % (g * 5) == 0) continue;
                double sy = Math.Round(ToScreen(0, y).Y) + 0.5;
                dc.DrawLine(pen, new Point(vis.Left, sy), new Point(vis.Right, sy));
            }
        }
    }
}

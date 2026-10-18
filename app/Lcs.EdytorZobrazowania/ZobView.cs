using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorZobrazowania;

public enum ZobTool { Wskaz, Tor, Lacznik, Miarka }

/// <summary>
/// Płótno obrazu w jednostkach obrazu „j.” (PLAN_EDYTOR_ZOBRAZOWANIA.md § 4): czarne tło jak na pulpicie (Ie-104 § 4 ust. 13),
/// siatka co krok przyciągania (co 5 – grubsza), przybliżanie kółkiem wokół kursora, przesuwanie środkowym przyciskiem
/// (we Wskaż także lewym na pustym miejscu). Narzędzia: Wskaż (zaznaczenie toru, przeciąganie wierzchołka), Tor (łamana co 45°),
/// Łącznik (ukos – pion – ukos między dwoma torami). Siatka, ramka, węzły i zaznaczenie są tylko w edytorze – nie należą do obrazu.
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

    /// <summary>Punkt pod kursorem [j.] (z przyciąganiem); null – kursor poza płótnem.</summary>
    public (int X, int Y)? CursorPt { get; private set; }
    public event Action CursorMoved;
    public event Action ViewChanged;

    // ------------------------------------------------------------------ narzędzia
    ZobTool tool;
    public ZobTool Tool { get => tool; set { tool = value; Draft.Clear(); LacznikFrom = null; drag = null; Miara.Clear(); ToolChanged?.Invoke(); InvalidateVisual(); } }
    public event Action ToolChanged;
    /// <summary>Zaznaczony tor obrazu (id <see cref="ZobTor"/>).</summary>
    public string Selected { get; private set; }
    /// <summary>Tor UT podświetlony (wybrany w panelu UT).</summary>
    public string Highlight { get; set; }
    /// <summary>Tor UT rysowany narzędziem Tor.</summary>
    public string DraftUt { get; set; }
    /// <summary>Punkty rysowanej łamanej [j.].</summary>
    public List<int[]> Draft { get; } = new();
    /// <summary>Tor obrazu, na którego węźle zaczęto / skończono rysowanie (do kierunku km); null – wolne miejsce.</summary>
    public string DraftStartTor { get; private set; }
    public string DraftEndTor { get; private set; }
    /// <summary>Punkty miarki [j.].</summary>
    public List<int[]> Miara { get; } = new();
    /// <summary>Łącznik: pierwszy punkt na torze obrazu.</summary>
    public (int[] P, string Tor)? LacznikFrom { get; private set; }

    /// <summary>Łamana gotowa (narzędzie Tor).</summary>
    public event Action<List<int[]>> TorDone;
    /// <summary>Klik narzędziem Tor bez wybranego toru UT – okno pokazuje tory do wyboru (punkt, tor obrazu pod kursorem albo null).</summary>
    public event Action<int[], string> TorNeedsUt;
    /// <summary>Łącznik: tor A, punkt na A, tor B, punkt kliknięty na B.</summary>
    public event Action<string, int[], string, int[]> LacznikDone;
    public event Action SelectionChanged;
    /// <summary>Wierzchołek toru przeciągnięty: tor, nr punktu, nowe miejsce.</summary>
    public event Action<string, int, int[]> VertexMoved;
    public event Action<string> Info;

    static Brush B(byte r, byte g, byte b, byte a = 255) { var x = new SolidColorBrush(Color.FromArgb(a, r, g, b)); x.Freeze(); return x; }
    static Pen P(Brush b, double w, bool dash = false)
    {
        var p = new Pen(b, w) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        if (dash) p.DashStyle = new DashStyle(new double[] { 2, 2 }, 0);
        p.Freeze(); return p;
    }
    // barwy pulpitu wg Ie-104 § 4 ust. 13 (jak PanelView): tło czarne, tor w stanie zasadniczym szary
    public static readonly Brush Bg = B(0, 0, 0), Grey = B(0x9a, 0x9a, 0x9a), Outside = B(0x2a, 0x2a, 0x2a), Hint = B(0x90, 0x90, 0x90);
    static readonly Pen GridMinor = P(B(0x16, 0x16, 0x16), 1), GridMajor = P(B(0x2c, 0x2c, 0x2c), 1), Frame = P(B(0x60, 0x60, 0x60), 1),
        CursorPen = P(B(0x00, 0x90, 0x90), 1), SelPen = P(B(0x00, 0xa0, 0xff, 0x90), 12), HiPen = P(B(0x20, 0xe0, 0xc0, 0x70), 12),
        DraftPen = P(B(0xff, 0xff, 0xff), 2, true), NodePen = P(B(0x00, 0xb0, 0xb0), 1.2), OddPen = P(B(0xff, 0x40, 0x40), 2, true), MiaraPen = P(B(0xff, 0xc0, 0x40), 1.5, true);
    static readonly Brush NodeFill = B(0x00, 0x60, 0x60), VertexFill = B(0x00, 0xa0, 0xff);
    static readonly Typeface Ui = new("Segoe UI");

    Point? panFrom; Point panOff;
    (string Tor, int Index, int[] Pt)? drag;
    Point? hoverScreen;

    public ZobView()
    {
        ClipToBounds = true; Focusable = true;
        MouseWheel += (s, e) => { ZoomAt(e.GetPosition(this), e.Delta > 0 ? 1.2 : 1 / 1.2); e.Handled = true; };
        MouseDown += (s, e) =>
        {
            Focus();
            var p = e.GetPosition(this);
            if (e.ChangedButton == MouseButton.Middle) StartPan(p);
            else if (e.ChangedButton == MouseButton.Left) LeftDown(p, Keyboard.Modifiers, e.ClickCount);
            else if (e.ChangedButton == MouseButton.Right) RightClick(p);
            if (panFrom != null || drag != null) CaptureMouse();
            e.Handled = true;
        };
        MouseUp += (s, e) => { LeftUp(e.GetPosition(this)); e.Handled = true; };
        MouseMove += (s, e) => Move(e.GetPosition(this), Keyboard.Modifiers);
        MouseLeave += (s, e) => { CursorPt = null; hoverScreen = null; CursorMoved?.Invoke(); InvalidateVisual(); };
        SizeChanged += (s, e) => InvalidateVisual();
    }

    // ------------------------------------------------------------------ przeliczenia
    public Point ToScreen(double x, double y) => new(Off.X + x * Zoom, Off.Y + y * Zoom);
    public Point ToScreen(int[] p) => ToScreen(p[0], p[1]);
    public (double X, double Y) ToImage(Point p) => ((p.X - Off.X) / Zoom, (p.Y - Off.Y) / Zoom);
    /// <summary>Punkt siatki najbliższy punktowi ekranu [j.].</summary>
    public (int X, int Y) Snap(Point p)
    {
        var (x, y) = ToImage(p); int g = Math.Max(1, Siatka);
        return (ZobTory.Grid(x, g), ZobTory.Grid(y, g));
    }

    /// <summary>
    /// Przyciąganie: węzeł obrazu (punkt łamanej toru, do 10 px), linia toru (do 8 px – punkt siatki na linii), inaczej siatka.
    /// </summary>
    /// <param name="noLine">Tor obrazu, do którego linii nie przyciągać (pierwszy odcinek rysowany od tego toru).</param>
    public (int[] P, string Tor, bool Node) SnapAt(Point s, string exceptTor = null, string noLine = null)
    {
        if (Obraz != null)
        {
            var (x, y) = ToImage(s);
            foreach (var t in Obraz.Tory.Where(t => t.Id != exceptTor))
                foreach (var q in t.Pkt)
                    if ((ToScreen(q) - s).Length <= 10) return (q, t.Id, true);
            string bt = null; double bd = double.MaxValue; int bseg = -1;
            foreach (var t in Obraz.Tory.Where(t => t.Id != exceptTor && t.Id != noLine && t.Pkt.Count >= 2))
            {
                var r = ZobTory.Rzut(t.Pkt, x, y);
                if (r.Dist * Zoom <= 8 && r.Dist < bd) { bd = r.Dist; bt = t.Id; bseg = r.Seg; }
            }
            if (bt != null)
            {
                var pts = Obraz.Tory.First(t => t.Id == bt).Pkt;
                return (ZobTory.NaOdcinku(pts[bseg], pts[bseg + 1], x, y, Math.Max(1, Siatka)), bt, true);
            }
        }
        var (gx, gy) = Snap(s);
        return (new[] { gx, gy }, null, false);
    }

    /// <summary>Tor obrazu pod punktem ekranu (do 6 px od linii).</summary>
    public ZobTor HitTrack(Point s)
    {
        if (Obraz == null) return null;
        var (x, y) = ToImage(s);
        return Obraz.Tory.Where(t => t.Pkt.Count >= 2).Select(t => (t, ZobTory.Rzut(t.Pkt, x, y).Dist))
            .Where(z => z.Dist * Zoom <= 6).OrderBy(z => z.Dist).Select(z => z.t).FirstOrDefault();
    }

    public void Select(string id) { Selected = id; SelectionChanged?.Invoke(); InvalidateVisual(); }

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

    /// <summary>Punkt obrazu [j.] na środku okna.</summary>
    public void CenterOn(double x, double y) => PanTo(new Point(ActualWidth / 2 - x * Zoom, ActualHeight / 2 - y * Zoom));

    /// <summary>Powiększenie 1 (1 j. = 1 piksel) wokół środka okna.</summary>
    public void Actual() => ZoomAt(new Point(ActualWidth / 2, ActualHeight / 2), 1 / Zoom);

    // ------------------------------------------------------------------ mysz (także z testów)
    void StartPan(Point p) { panFrom = p; panOff = Off; }

    public void LeftDown(Point p, ModifierKeys mods, int clicks = 1)
    {
        if (Obraz == null) { StartPan(p); return; }
        switch (Tool)
        {
            case ZobTool.Tor: TorClick(p, mods, clicks); break;
            case ZobTool.Lacznik: LacznikClick(p); break;
            case ZobTool.Miarka: Miara.Add(SnapAt(p).P); Info?.Invoke(MiaraText()); InvalidateVisual(); break;
            default:
                // wierzchołek zaznaczonego toru – przeciąganie; tor – zaznaczenie; puste miejsce – przesuwanie widoku
                if (Selected != null && Obraz.Tory.FirstOrDefault(t => t.Id == Selected) is ZobTor st)
                    for (int i = 0; i < st.Pkt.Count; i++)
                        if ((ToScreen(st.Pkt[i]) - p).Length <= 7) { drag = (st.Id, i, st.Pkt[i]); return; }
                var hit = HitTrack(p);
                if (hit != null) { Select(hit.Id); return; }
                if (Selected != null) Select(null);
                StartPan(p);
                break;
        }
    }

    public void Move(Point p, ModifierKeys mods)
    {
        hoverScreen = p;
        if (panFrom is Point f) { Cursor = Cursors.SizeAll; PanTo(new Point(panOff.X + p.X - f.X, panOff.Y + p.Y - f.Y)); }
        if (drag is var (tor, i, _)) { var (gx, gy) = Snap(p); drag = (tor, i, new[] { gx, gy }); }
        var c = HoverPoint(p, mods);
        CursorPt = (c[0], c[1]); CursorMoved?.Invoke(); InvalidateVisual();
    }

    public void LeftUp(Point p)
    {
        if (panFrom != null) { panFrom = null; ReleaseMouseCapture(); Cursor = null; }
        if (drag is var (tor, i, pt))
        {
            drag = null; ReleaseMouseCapture();
            var t = Obraz?.Tory.FirstOrDefault(x => x.Id == tor);
            if (t != null && (t.Pkt[i][0] != pt[0] || t.Pkt[i][1] != pt[1])) VertexMoved?.Invoke(tor, i, pt);
            InvalidateVisual();
        }
    }

    public void RightClick(Point p)
    {
        if (Tool == ZobTool.Tor) { if (Draft.Count >= 2) FinishDraft(); else CancelDraft(); }
        else if (Tool == ZobTool.Lacznik) { LacznikFrom = null; Info?.Invoke("Łącznik: kliknij tor obrazu w miejscu pierwszej zwrotnicy"); InvalidateVisual(); }
        else if (Tool == ZobTool.Miarka) { Miara.Clear(); Info?.Invoke("Miarka: klikaj punkty"); InvalidateVisual(); }
    }

    /// <summary>Klik (test): naciśnięcie i puszczenie w tym samym miejscu.</summary>
    public void SimClick(Point p, MouseButton b = MouseButton.Left, ModifierKeys mods = ModifierKeys.None, int clicks = 1)
    {
        Move(p, mods);
        if (b == MouseButton.Right) RightClick(p);
        else { LeftDown(p, mods, clicks); LeftUp(p); }
    }

    /// <summary>Przeciągnięcie (test).</summary>
    public void SimDrag(Point a, Point b)
    {
        Move(a, ModifierKeys.None); LeftDown(a, ModifierKeys.None);
        Move(new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2), ModifierKeys.None); Move(b, ModifierKeys.None);
        LeftUp(b);
    }

    /// <summary>Punkt pod kursorem z przyciąganiem narzędzia (podgląd i pasek stanu).</summary>
    int[] HoverPoint(Point p, ModifierKeys mods)
    {
        if (drag is var (_, _, dp)) return dp;
        var s = TorSnap(p);
        if (Tool == ZobTool.Tor && Draft.Count > 0 && !s.Node)
        {
            var (x, y) = ToImage(p);
            return ZobTory.Kierunek(Draft[^1], x, y, Math.Max(1, Siatka), mods.HasFlag(ModifierKeys.Shift), mods.HasFlag(ModifierKeys.Control));
        }
        return s.P;
    }

    /// <summary>Przyciąganie narzędzia Tor: pierwszy odcinek nie wraca na linię toru, od którego zaczęto.</summary>
    (int[] P, string Tor, bool Node) TorSnap(Point p) => SnapAt(p, null, Tool == ZobTool.Tor && Draft.Count == 1 ? DraftStartTor : null);

    void TorClick(Point p, ModifierKeys mods, int clicks)
    {
        var s = TorSnap(p);
        if (DraftUt == null) { TorNeedsUt?.Invoke(s.P, s.Tor); return; }
        if (clicks >= 2) { FinishDraft(); return; }
        if (Draft.Count == 0) { Draft.Add(s.P); DraftStartTor = s.Tor; DraftEndTor = null; InvalidateVisual(); return; }
        var pt = HoverPoint(p, mods);
        if (pt[0] == Draft[^1][0] && pt[1] == Draft[^1][1]) return;
        Draft.Add(pt);
        // klik na węźle obrazu (koniec, załamanie albo linia innego toru) kończy tor
        if (s.Node && pt[0] == s.P[0] && pt[1] == s.P[1]) { DraftEndTor = s.Tor; FinishDraft(); }
        InvalidateVisual();
    }

    public void FinishDraft()
    {
        if (Draft.Count < 2) { Info?.Invoke("Tor: potrzebne co najmniej dwa punkty"); return; }
        var pts = Draft.Select(x => new[] { x[0], x[1] }).ToList();
        Draft.Clear(); InvalidateVisual();
        TorDone?.Invoke(pts);
    }

    public void CancelDraft() { Draft.Clear(); InvalidateVisual(); }
    /// <summary>Pierwszy punkt rysowania na węźle toru obrazu (wybór toru UT z listy przy węźle).</summary>
    public void StartDraft(int[] p, string tor) { Draft.Clear(); Draft.Add(p); DraftStartTor = tor; DraftEndTor = null; InvalidateVisual(); }

    /// <summary>Długość miarki [j.] i w krokach siatki.</summary>
    public string MiaraText()
    {
        double l = 0; for (int i = 1; i < Miara.Count; i++) l += Math.Sqrt(Math.Pow(Miara[i][0] - Miara[i - 1][0], 2) + Math.Pow(Miara[i][1] - Miara[i - 1][1], 2));
        if (Miara.Count < 2) return "Miarka: kliknij następny punkt; PPM – wyczyść, Esc – koniec";
        var (dx, dy) = (Miara[^1][0] - Miara[^2][0], Miara[^1][1] - Miara[^2][1]);
        return $"Miarka: {l:0.#} j. ({l / Math.Max(1, Siatka):0.#} kratek); ostatni odcinek Δx {dx}, Δy {dy}; PPM – wyczyść, Esc – koniec";
    }
    public void UndoPoint() { if (Draft.Count > 0) { Draft.RemoveAt(Draft.Count - 1); InvalidateVisual(); } }

    void LacznikClick(Point p)
    {
        var s = SnapAt(p);
        if (s.Tor == null) { Info?.Invoke("Łącznik: kliknij na linii toru obrazu"); return; }
        if (LacznikFrom is not var (fp, ft)) { LacznikFrom = (s.P, s.Tor); Info?.Invoke("Łącznik: teraz kliknij drugi tor w miejscu drugiej zwrotnicy; PPM / Esc – od nowa"); InvalidateVisual(); return; }
        if (s.Tor == ft) { Info?.Invoke("Łącznik: drugi punkt musi być na innym torze obrazu"); return; }
        LacznikFrom = null; InvalidateVisual();
        LacznikDone?.Invoke(ft, fp, s.Tor, s.P);
    }

    public void CancelLacznik() { LacznikFrom = null; InvalidateVisual(); }

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
        DrawTracks(dc);
        DrawTool(dc);
        if (CursorPt is var (cx, cy) && cx >= 0 && cy >= 0 && cx <= Obraz.Szer && cy <= Obraz.Wys)
        {
            var c = ToScreen(cx, cy); double r = Math.Max(4, Siatka * Zoom * 0.6);
            dc.DrawLine(CursorPen, new Point(c.X - r, c.Y), new Point(c.X + r, c.Y));
            dc.DrawLine(CursorPen, new Point(c.X, c.Y - r), new Point(c.X, c.Y + r));
        }
    }

    StreamGeometry Poly(IList<int[]> pts)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(ToScreen(pts[0]), false, false);
            for (int i = 1; i < pts.Count; i++) c.LineTo(ToScreen(pts[i]), true, true);
        }
        g.Freeze(); return g;
    }

    void DrawTracks(DrawingContext dc)
    {
        // tor w stanie zasadniczym: szary, grubość jak na pulpicie (4,6 px przy powiększeniu 1)
        var pen = new Pen(Grey, Math.Max(1, 4.6 * Zoom)) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat, LineJoin = PenLineJoin.Round };
        foreach (var t in Obraz.Tory.Where(t => t.Pkt.Count >= 2))
        {
            var pts = drag is var (dt, di, dp) && dt == t.Id ? t.Pkt.Select((q, i) => i == di ? dp : q).ToList() : t.Pkt;
            var g = Poly(pts);
            if (t.Ut?.Tor == Highlight && Highlight != null) dc.DrawGeometry(null, HiPen, g);
            if (t.Id == Selected) dc.DrawGeometry(null, SelPen, g);
            dc.DrawGeometry(null, pen, g);
            // odcinek nie co 45° – znacznik edytora
            for (int i = 1; i < pts.Count; i++)
                if (!ZobTory.Osiowy(pts[i - 1], pts[i])) dc.DrawLine(OddPen, ToScreen(pts[i - 1]), ToScreen(pts[i]));
        }
        // węzły obrazu (końce torów) i wierzchołki zaznaczonego toru – tylko w edytorze
        foreach (var t in Obraz.Tory.Where(t => t.Pkt.Count >= 2))
            foreach (var q in new[] { t.Pkt[0], t.Pkt[^1] })
                dc.DrawEllipse(NodeFill, NodePen, ToScreen(q), 3, 3);
        if (Obraz.Tory.FirstOrDefault(t => t.Id == Selected) is ZobTor st)
            for (int i = 0; i < st.Pkt.Count; i++)
            {
                var q = drag is var (dt, di, dp) && dt == st.Id && di == i ? dp : st.Pkt[i];
                var c = ToScreen(q);
                dc.DrawRectangle(VertexFill, null, new Rect(c.X - 3.5, c.Y - 3.5, 7, 7));
            }
    }

    void DrawTool(DrawingContext dc)
    {
        if (Tool == ZobTool.Tor && Draft.Count > 0)
        {
            var pts = new List<int[]>(Draft);
            if (CursorPt is var (x, y)) pts.Add(new[] { x, y });
            if (pts.Count >= 2) dc.DrawGeometry(null, DraftPen, Poly(pts));
            dc.DrawEllipse(null, DraftPen, ToScreen(Draft[0]), 4, 4);
        }
        if (Tool == ZobTool.Miarka && Miara.Count > 0)
        {
            var mp = new List<int[]>(Miara);
            if (CursorPt is var (mx, my)) mp.Add(new[] { mx, my });
            if (mp.Count >= 2) dc.DrawGeometry(null, MiaraPen, Poly(mp));
            foreach (var q in Miara) dc.DrawEllipse(null, MiaraPen, ToScreen(q), 3, 3);
        }
        if (Tool == ZobTool.Lacznik && LacznikFrom is var (fp, _))
        {
            dc.DrawEllipse(null, DraftPen, ToScreen(fp), 5, 5);
            if (CursorPt is var (x, y) && ZobTory.Lacznik(fp, x, y, Math.Max(1, Siatka)) is List<int[]> l)
                dc.DrawGeometry(null, DraftPen, Poly(l));
        }
    }

    void DrawGrid(DrawingContext dc, Rect vis)
    {
        int g = Math.Max(1, Siatka);
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

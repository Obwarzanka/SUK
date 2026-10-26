using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorZobrazowania2;

/// <summary>Narzędzia edytora (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 8); Symbol – element z palety (<see cref="ObrView.SymbolTyp"/>, etap E3).</summary>
public enum ObrTool { Wskaz, Tor, Prowadnica, Symbol, Klocek, Szkic }

/// <summary>
/// Płótno obrazu w jednostkach „j.” (plan § 8): czarne tło jak na pulpicie (Ie-104 § 4 ust. 13), siatka co krok przyciągania
/// (co 5 – grubsza), prowadnice rzędów, strefy zarezerwowane; przybliżanie kółkiem wokół kursora, przesuwanie środkowym przyciskiem.
/// Narzędzia: Wskaż (klik, Shift + klik, prostokąt, przeciąganie zaznaczenia, węzła, rzędu), Pióro toru (klik – klik co 45°),
/// Prowadnica. Siatka, prowadnice, strefy, węzły, zaznaczenie i uwagi są tylko w edytorze (<see cref="Czysty"/> – bez nich).
/// </summary>
public partial class ObrView : FrameworkElement
{
    public ObrObraz Obraz { get; set; }
    public int Siatka { get; set; } = 8;
    public bool ShowGrid { get; set; } = true;
    public bool ShowRzedy { get; set; } = true;
    /// <summary>Bez znaczników edytora – PNG i wydruk, wygląd jak na pulpicie.</summary>
    public bool Czysty { get; set; }
    /// <summary>Tekst na pustym płótnie (brak otwartego zobrazowania).</summary>
    public string EmptyText { get; set; }

    /// <summary>Powiększenie: pikseli ekranu na 1 j.</summary>
    public double Zoom { get; private set; } = 0.5;
    /// <summary>Położenie punktu (0, 0) obrazu na ekranie.</summary>
    public Point Off { get; private set; } = new(20, 20);
    public const double MinZoom = 0.1, MaxZoom = 8;

    /// <summary>Punkt pod kursorem [j.] (z przyciąganiem narzędzia); null – kursor poza płótnem.</summary>
    public (int X, int Y)? CursorPt { get; private set; }
    public event Action CursorMoved;
    public event Action ViewChanged;

    ObrTool tool;
    public ObrTool Tool { get => tool; set { tool = value; rzadDrag = null; Draft.Clear(); drag = null; rect = null; elDrag = null; opisDrag = null; szkicDrag = null; preview = null; ToolChanged?.Invoke(); InvalidateVisual(); } }
    public event Action ToolChanged;

    /// <summary>Zaznaczenie: węzły i odcinki (id).</summary>
    public readonly HashSet<string> SelW = new(), SelE = new();
    public event Action SelectionChanged;
    /// <summary>Punkty rysowanej łamanej pióra [j.].</summary>
    public List<int[]> Draft { get; } = new();

    /// <summary>Prowadnica: nowa (y), przesunięta (stare y, nowe y), usunięta (y).</summary>
    public event Action<int> RzadAdded;
    public event Action<int, int> RzadMoved;
    public event Action<int> RzadRemoved;
    /// <summary>Łamana pióra gotowa.</summary>
    public event Action<List<int[]>> TorDone;
    /// <summary>Przeciągnięcie zaznaczenia (węzły, dx, dy) albo rzędu (węzły rzędu, dy).</summary>
    public event Action<HashSet<string>, int, int> SelMoved;
    public event Action<HashSet<string>, int> RzadDragged;
    /// <summary>PPM na płótnie (narzędzie Wskaż): punkt [j.], węzeł, odcinek pod kursorem (null – brak).</summary>
    public event Action<int[], string, string> MenuAt;
    public event Action<string> Info;

    static Brush B(byte r, byte g, byte b, byte a = 255) { var x = new SolidColorBrush(Color.FromArgb(a, r, g, b)); x.Freeze(); return x; }
    static Pen P(Brush b, double w, double[] dash = null)
    {
        var p = new Pen(b, w) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        if (dash != null) p.DashStyle = new DashStyle(dash, 0);
        p.Freeze(); return p;
    }
    // barwy pulpitu wg Ie-104 § 4 ust. 13 (jak PanelView): tło czarne, tor w stanie zasadniczym szary
    public static readonly Brush Bg = B(0, 0, 0), Grey = B(0x9a, 0x9a, 0x9a), Outside = B(0x2a, 0x2a, 0x2a), Hint = B(0x90, 0x90, 0x90);
    static readonly Pen GridMinor = P(B(0x16, 0x16, 0x16), 1), GridMajor = P(B(0x2c, 0x2c, 0x2c), 1), Frame = P(B(0x60, 0x60, 0x60), 1),
        CursorPen = P(B(0x00, 0x90, 0x90), 1), RzadPen = P(B(0x30, 0x80, 0xc0, 0xb0), 1, new double[] { 6, 4 }), RzadHotPen = P(B(0x50, 0xb0, 0xff), 1.5, new double[] { 6, 3 }),
        StrefaPen = P(B(0xc0, 0x90, 0x30, 0xc0), 1, new double[] { 4, 3 }), NodePen = P(B(0x00, 0xb0, 0xb0), 1.2), SelPen = P(B(0x00, 0xa0, 0xff, 0x90), 12),
        DraftPen = P(B(0xff, 0xff, 0xff), 2, new double[] { 2, 2 }), OddPen = P(B(0xff, 0x40, 0x40), 2, new double[] { 2, 2 }), RectPen = P(B(0x50, 0xb0, 0xff), 1, new double[] { 4, 3 });
    static readonly Brush StrefaFill = B(0xc0, 0x90, 0x30, 0x28), StrefaText = B(0xc0, 0x98, 0x50), RzadText = B(0x50, 0x90, 0xc8), NodeFill = B(0x00, 0x60, 0x60),
        SelFill = B(0x00, 0xa0, 0xff), RectFill = B(0x50, 0xb0, 0xff, 0x20), Warn = B(0xff, 0xb0, 0x20), JoinFill = B(0xe0, 0xe0, 0x60);
    static readonly Typeface Ui = new("Segoe UI");

    Point? panFrom; Point panOff;
    /// <summary>Ostatnie położenie kursora na ekranie (cień symbolu).</summary>
    Point? hoverScreen;
    /// <summary>Przeciągana prowadnica: y na początku i bieżące.</summary>
    (int From, int Now)? rzadDrag;
    /// <summary>Przeciąganie we Wskaż: punkt startu [j.], węzły do przesunięcia, rząd (tylko w pionie), bieżące przesunięcie.</summary>
    (int[] Start, HashSet<string> Nodes, bool Rzad, int Dx, int Dy)? drag;
    /// <summary>Prostokąt zaznaczenia: punkt startu na ekranie, bieżący, Shift.</summary>
    (Point A, Point B, bool Add)? rect;
    /// <summary>Podgląd przeciągania – kopia obrazu z przesunięciem (rysowana zamiast obrazu).</summary>
    ObrObraz preview;

    public ObrView()
    {
        ClipToBounds = true; Focusable = true;
        // kółko – przybliżenie; Shift + kółko – w poziomie, Ctrl + kółko – w pionie (uwaga właściciela 2026-10-22: „nie mogę poruszać się swobodnie”)
        MouseWheel += (s, e) =>
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) PanBy(e.Delta > 0 ? 80 : -80, 0);
            else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) PanBy(0, e.Delta > 0 ? 80 : -80);
            else ZoomAt(e.GetPosition(this), e.Delta > 0 ? 1.2 : 1 / 1.2);
            e.Handled = true;
        };
        MouseDown += (s, e) =>
        {
            Focus();
            var p = e.GetPosition(this);
            if (e.ChangedButton == MouseButton.Middle) StartPan(p);
            else if (e.ChangedButton == MouseButton.Left) LeftDown(p, Keyboard.Modifiers, e.ClickCount);
            // PPM: przeciągnięcie – przesuwanie widoku, klik bez ruchu – menu / koniec pióra (przy puszczeniu)
            else if (e.ChangedButton == MouseButton.Right) { rightFrom = p; CaptureMouse(); }
            if (panFrom != null || rzadDrag != null || drag != null || rect != null || elDrag != null || opisDrag != null || szkicDrag != null) CaptureMouse();
            e.Handled = true;
        };
        MouseUp += (s, e) =>
        {
            var p = e.GetPosition(this);
            if (e.ChangedButton == MouseButton.Right && rightFrom != null)
            {
                bool panned = panFrom != null;
                rightFrom = null; panFrom = null; ReleaseMouseCapture(); Cursor = null;
                if (!panned) RightClick(p);
            }
            else LeftUp(p);
            e.Handled = true;
        };
        MouseMove += (s, e) => Move(e.GetPosition(this));
        MouseLeave += (s, e) => { CursorPt = null; hoverScreen = null; CursorMoved?.Invoke(); InvalidateVisual(); };
        SizeChanged += (s, e) => InvalidateVisual();
    }

    // ------------------------------------------------------------------ przeliczenia
    public Point ToScreen(double x, double y) => new(Off.X + x * Zoom, Off.Y + y * Zoom);
    public Point ToScreen(int[] p) => ToScreen(p[0], p[1]);
    public (double X, double Y) ToImage(Point p) => ((p.X - Off.X) / Zoom, (p.Y - Off.Y) / Zoom);
    int G => Math.Max(1, Siatka);
    /// <summary>Punkt siatki najbliższy punktowi ekranu [j.].</summary>
    public (int X, int Y) Snap(Point p)
    {
        var (x, y) = ToImage(p);
        return (ZobTory.Grid(x, G), ZobTory.Grid(y, G));
    }

    /// <summary>Prowadnica pod punktem ekranu (do 5 px); null – brak.</summary>
    public int? HitRzad(Point s)
    {
        if (Obraz == null || !ShowRzedy) return null;
        var hit = Obraz.Rzedy.Select(y => (y, d: Math.Abs(ToScreen(0, y).Y - s.Y))).Where(z => z.d <= 5).OrderBy(z => z.d).ToList();
        return hit.Count > 0 ? hit[0].y : null;
    }

    /// <summary>Węzeł pod punktem ekranu (do 7 px).</summary>
    public ObrWezel HitWezel(Point s, double px = 7) =>
        Obraz?.Wezly.Select(w => (w, d: (ToScreen(w.P) - s).Length)).Where(z => z.d <= px).OrderBy(z => z.d).Select(z => z.w).FirstOrDefault();

    /// <summary>Odcinek pod punktem ekranu (do 6 px od linii).</summary>
    public ObrOdcinek HitOdcinek(Point s, double px = 6)
    {
        if (Obraz == null) return null;
        var (x, y) = ToImage(s);
        return Obraz.Odcinki.Select(e => { var (a, b) = ObrGraf.Konce(Obraz, e); return (e, d: ObrGraf.Rzut(a, b, x, y).Dist); })
            .Where(z => z.d * Zoom <= px).OrderBy(z => z.d).Select(z => z.e).FirstOrDefault();
    }

    /// <summary>
    /// Punkt pióra pod kursorem (plan § 5): węzeł (do 10 px), tor (do 8 px – punkt na torze, najlepiej w kierunku co 45° od poprzedniego punktu),
    /// inaczej kierunek co 45° od poprzedniego punktu z przyklejaniem do prowadnic; pierwszy punkt – siatka i prowadnica.
    /// </summary>
    public (int[] P, bool NaTorze) PenPoint(Point s)
    {
        var (x, y) = ToImage(s);
        double tol = Math.Max(6, 10 / Zoom);
        int[] dir = Draft.Count > 0 ? ObrGraf.Kierunek(Draft[^1], x, y, G, ShowRzedy ? Obraz.Rzedy : null, tol) : null;
        if (HitWezel(s, 10) is ObrWezel w) return (w.P, true);
        if (HitOdcinek(s, 8) is ObrOdcinek e)
        {
            var (a, b) = ObrGraf.Konce(Obraz, e);
            if (dir != null && ObrGraf.NaLinii(a, b, dir) && (dir[0] != Draft[^1][0] || dir[1] != Draft[^1][1])) return (dir, true);
            if (dir != null)
            {
                // przecięcie promienia pióra z torem – koniec dokładnie co 45°
                var far = new[] { Draft[^1][0] + (dir[0] - Draft[^1][0]) * 50, Draft[^1][1] + (dir[1] - Draft[^1][1]) * 50 };
                if (dir[0] != Draft[^1][0] || dir[1] != Draft[^1][1])
                    if (ObrGraf.Przeciecie(Draft[^1], far, a, b) is int[] q && Math.Sqrt(Math.Pow(q[0] - x, 2) + Math.Pow(q[1] - y, 2)) * Zoom <= 16) return (q, true);
            }
            return (ObrGraf.NaOdcinku(a, b, x, y, G), true);
        }
        if (dir != null) return (dir, false);
        int gx = ZobTory.Grid(x, G), gy = ZobTory.Grid(y, G);
        if (ShowRzedy && Obraz.Rzedy.Where(r => Math.Abs(r - y) <= tol).OrderBy(r => Math.Abs(r - y)).Cast<int?>().FirstOrDefault() is int ry) gy = ry;
        return (new[] { gx, gy }, false);
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

    /// <summary>Punkt obrazu [j.] na środku okna.</summary>
    public void CenterOn(double x, double y) => PanTo(new Point(ActualWidth / 2 - x * Zoom, ActualHeight / 2 - y * Zoom));
    /// <summary>Powiększenie i położenie punktu (0, 0) obrazu wprost (PNG).</summary>
    public void SetView(double zoom, Point off) { Zoom = Math.Clamp(zoom, MinZoom, MaxZoom); Off = off; InvalidateVisual(); }
    /// <summary>Powiększenie 1 (1 j. = 1 piksel) wokół środka okna.</summary>
    public void Actual() => ZoomAt(new Point(ActualWidth / 2, ActualHeight / 2), 1 / Zoom);

    // ------------------------------------------------------------------ zaznaczenie
    public void Select(IEnumerable<string> wezly, IEnumerable<string> odcinki)
    {
        SelW.Clear(); SelE.Clear(); SelEl.Clear();
        if (wezly != null) SelW.UnionWith(wezly);
        if (odcinki != null) SelE.UnionWith(odcinki);
        SelectionChanged?.Invoke(); InvalidateVisual();
    }
    public void ClearSelection() => Select(null, null);
    /// <summary>Zaznaczenie bez elementów, których już nie ma (po cofnięciu).</summary>
    public void PruneSelection()
    {
        if (Obraz == null) { SelW.Clear(); SelE.Clear(); SelEl.Clear(); return; }
        int n = SelW.Count + SelE.Count + SelEl.Count;
        SelW.IntersectWith(Obraz.Wezly.Select(w => w.Id)); SelE.IntersectWith(Obraz.Odcinki.Select(e => e.Id)); SelEl.IntersectWith(Obraz.Elementy.Select(e => e.Id));
        if (SelW.Count + SelE.Count + SelEl.Count != n) SelectionChanged?.Invoke();
    }

    // ------------------------------------------------------------------ mysz (także z testów)
    void StartPan(Point p) { panFrom = p; panOff = Off; }

    public void LeftDown(Point p, ModifierKeys mods = ModifierKeys.None, int clicks = 1)
    {
        if (Obraz == null) { StartPan(p); return; }
        switch (Tool)
        {
            case ObrTool.Prowadnica:
                // prowadnica pod kursorem – przeciąganie; wolne miejsce na obrazie – nowa prowadnica w rzędnej siatki
                if (HitRzad(p) is int y) { rzadDrag = (y, y); return; }
                var (_, gy) = Snap(p);
                if (gy > 0 && gy < Obraz.Wys) RzadAdded?.Invoke(gy);
                return;
            case ObrTool.Tor: PenClick(p, clicks); return;
            case ObrTool.Symbol: SymbolClick(p); return;
            case ObrTool.Klocek: KlocekClick(p); return;
            case ObrTool.Szkic: rect = (p, p, false); return;
        }
        bool shift = mods.HasFlag(ModifierKeys.Shift);
        var start = Snap(p); var sp = new[] { start.X, start.Y };
        // opis i symbol mają pierwszeństwo przed węzłem i torem (stoją na nich)
        if (SzkicDown(p)) return;
        if (ElementDown(p, shift)) return;
        if (HitWezel(p) is ObrWezel w)
        {
            if (shift) { if (!SelW.Remove(w.Id)) SelW.Add(w.Id); SelectionChanged?.Invoke(); InvalidateVisual(); return; }
            if (!SelW.Contains(w.Id)) Select(new[] { w.Id }, null);
            drag = (w.P, ObrGraf.WezlyZaznaczenia(Obraz, SelW, SelE), false, 0, 0);
            return;
        }
        if (HitOdcinek(p) is ObrOdcinek e)
        {
            if (shift) { if (!SelE.Remove(e.Id)) SelE.Add(e.Id); SelectionChanged?.Invoke(); InvalidateVisual(); return; }
            if (SelE.Contains(e.Id)) { drag = (sp, ObrGraf.WezlyZaznaczenia(Obraz, SelW, SelE), false, 0, 0); return; }
            Select(null, new[] { e.Id });
            // tor poziomy niezaznaczony – przeciąganie przesuwa cały rząd z łącznikami w pionie (plan § 8)
            bool rz = ObrGraf.Poziomy(Obraz, e);
            drag = (sp, rz ? ObrGraf.Rzad(Obraz, e) : new HashSet<string> { e.A, e.B }, rz, 0, 0);
            return;
        }
        rect = (p, p, shift);
    }

    /// <summary>Przesunięcie widoku o (dx, dy) pikseli ekranu.</summary>
    public void PanBy(double dx, double dy) => PanTo(new Point(Off.X + dx, Off.Y + dy));

    /// <summary>PPM (test): naciśnięcie w <paramref name="a"/>, puszczenie w <paramref name="b"/> – przeciągnięcie przesuwa widok, bez ruchu – menu.</summary>
    public void SimRight(Point a, Point b)
    {
        Move(a); rightFrom = a;
        Move(new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2)); Move(b);
        bool panned = panFrom != null;
        rightFrom = null; panFrom = null; Cursor = null;
        if (!panned) RightClick(b);
    }

    /// <summary>Naciśnięty PPM (przesuwanie widoku po przeciągnięciu o więcej niż 4 px).</summary>
    Point? rightFrom;

    public void Move(Point p)
    {
        if (rightFrom is Point rf && panFrom == null && (p - rf).Length > 4) StartPan(rf);
        hoverScreen = p;
        if (panFrom is Point f) { Cursor = Cursors.SizeAll; PanTo(new Point(panOff.X + p.X - f.X, panOff.Y + p.Y - f.Y)); }
        if (rzadDrag is var (from, _)) rzadDrag = (from, Snap(p).Y);
        if (drag is var (st, nodes, rz, _, _))
        {
            var (gx, gy) = Snap(p);
            int dx = rz ? 0 : gx - st[0], dy = gy - st[1];
            drag = (st, nodes, rz, dx, dy);
            preview = Clone(Obraz);
            if (rz) ObrGraf.PrzesunRzad(preview, nodes, dy); else ObrGraf.Przesun(preview, nodes, dx, dy);
        }
        if (rect is var (ra, _, add)) rect = (ra, p, add);
        ElementMove(p);
        SzkicMove(p);
        if (Tool == ObrTool.Prowadnica) Cursor = rzadDrag != null || HitRzad(p) != null ? Cursors.SizeNS : Cursors.Cross;
        else if (Tool == ObrTool.Tor) Cursor = Cursors.Pen;
        else if (Tool is ObrTool.Symbol or ObrTool.Klocek or ObrTool.Szkic) Cursor = Cursors.Cross;
        else if (panFrom == null) Cursor = drag is var (_, _, r2, _, _) && r2 ? Cursors.SizeNS : null;
        if (Obraz != null && Tool == ObrTool.Tor) { var q = PenPoint(p).P; CursorPt = (q[0], q[1]); }
        else { var (gx, gy) = Snap(p); CursorPt = (gx, gy); }
        CursorMoved?.Invoke(); InvalidateVisual();
    }

    public void LeftUp(Point p)
    {
        if (panFrom != null) { panFrom = null; ReleaseMouseCapture(); Cursor = null; }
        if (rzadDrag is var (from, now))
        {
            rzadDrag = null; ReleaseMouseCapture();
            // przeciągnięcie poza obraz usuwa prowadnicę
            if (now <= 0 || now >= Obraz.Wys) RzadRemoved?.Invoke(from);
            else if (now != from) RzadMoved?.Invoke(from, now);
        }
        ElementUp();
        SzkicUp();
        if (drag is var (_, nodes, rz, dx, dy))
        {
            drag = null; preview = null; ReleaseMouseCapture();
            if (rz && dy != 0) RzadDragged?.Invoke(nodes, dy);
            else if (!rz && (dx != 0 || dy != 0)) SelMoved?.Invoke(nodes, dx, dy);
        }
        if (rect is var (sa, sb, _) && Tool == ObrTool.Szkic)
        {
            // prostokąt obszaru szkicu na życzenie
            rect = null; ReleaseMouseCapture();
            var (x0, y0) = ToImage(sa); var (x1, y1) = ToImage(sb);
            int gx = ZobTory.Grid(Math.Min(x0, x1), G), gy = ZobTory.Grid(Math.Min(y0, y1), G);
            SzkicRect?.Invoke(new[] { gx, gy, ZobTory.Grid(Math.Abs(x1 - x0), G), ZobTory.Grid(Math.Abs(y1 - y0), G) });
        }
        if (rect is var (a, b, add))
        {
            rect = null; ReleaseMouseCapture();
            var (x0, y0) = ToImage(a); var (x1, y1) = ToImage(b);
            var r = new Rect(new Point(x0, y0), new Point(x1, y1));
            var ws = Obraz.Wezly.Where(w => r.Contains(w.P[0], w.P[1])).Select(w => w.Id).ToHashSet();
            var es = Obraz.Odcinki.Where(e => ws.Contains(e.A) && ws.Contains(e.B)).Select(e => e.Id).ToList();
            if (!add) Select(ws, es);
            else { SelW.UnionWith(ws); SelE.UnionWith(es); SelectionChanged?.Invoke(); }
        }
        InvalidateVisual();
    }

    public void RightClick(Point p)
    {
        if (Tool == ObrTool.Prowadnica && HitRzad(p) is int y) RzadRemoved?.Invoke(y);
        else if (Tool == ObrTool.Tor) { if (Draft.Count >= 2) FinishDraft(); else CancelDraft(); }
        else if (Tool is ObrTool.Symbol or ObrTool.Klocek or ObrTool.Szkic) Tool = ObrTool.Wskaz;
        else if (Tool == ObrTool.Wskaz && Obraz != null)
        {
            var el = HitElement(p);
            var w = el == null ? HitWezel(p) : null; var e = el == null && w == null ? HitOdcinek(p) : null;
            var (gx, gy) = Snap(p);
            if (el != null) { if (!SelEl.Contains(el.Id)) Select(null, null, new[] { el.Id }); ElementMenuAt?.Invoke(new[] { gx, gy }, el.Id); return; }
            MenuAt?.Invoke(new[] { gx, gy }, w?.Id, e?.Id);
        }
    }

    // ------------------------------------------------------------------ pióro
    void PenClick(Point p, int clicks)
    {
        if (clicks >= 2) { if (Draft.Count >= 2) FinishDraft(); return; }
        var (q, naTorze) = PenPoint(p);
        if (Draft.Count > 0 && q[0] == Draft[^1][0] && q[1] == Draft[^1][1]) return;
        Draft.Add(q);
        Info?.Invoke(Draft.Count == 1 ? "Pióro: klikaj kolejne punkty (co 45°); koniec – klik na torze, Enter, PPM albo dwuklik; Backspace – cofnij punkt; Esc – przerwij"
            : $"Pióro: {Draft.Count} punkty");
        // klik na torze albo węźle (nie pierwszy) kończy tor – rozjazd albo połączenie
        if (Draft.Count >= 2 && naTorze) FinishDraft();
        InvalidateVisual();
    }

    public void FinishDraft()
    {
        if (Draft.Count < 2) { Info?.Invoke("Pióro: potrzebne co najmniej dwa punkty"); return; }
        var pts = Draft.Select(x => new[] { x[0], x[1] }).ToList();
        Draft.Clear(); InvalidateVisual();
        TorDone?.Invoke(pts);
    }
    public void CancelDraft() { Draft.Clear(); InvalidateVisual(); }
    public void UndoPoint() { if (Draft.Count > 0) { Draft.RemoveAt(Draft.Count - 1); InvalidateVisual(); } }

    /// <summary>Klik (test): naciśnięcie i puszczenie w tym samym miejscu.</summary>
    public void SimClick(Point p, MouseButton b = MouseButton.Left, ModifierKeys mods = ModifierKeys.None, int clicks = 1)
    {
        Move(p);
        if (b == MouseButton.Right) RightClick(p);
        else { LeftDown(p, mods, clicks); LeftUp(p); }
    }

    /// <summary>Przeciągnięcie (test) lewym albo środkowym przyciskiem.</summary>
    public void SimDrag(Point a, Point b, MouseButton btn = MouseButton.Left, ModifierKeys mods = ModifierKeys.None)
    {
        Move(a);
        if (btn == MouseButton.Middle) StartPan(a); else LeftDown(a, mods);
        Move(new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2)); Move(b);
        LeftUp(b);
    }

    static ObrObraz Clone(ObrObraz o) => System.Text.Json.JsonSerializer.Deserialize<ObrObraz>(System.Text.Json.JsonSerializer.Serialize(o, UtDocument.Json), UtDocument.Json);

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
        if (!Czysty)
        {
            if (ShowGrid && !vis.IsEmpty) DrawGrid(dc, vis);
            DrawStrefy(dc);
            if (ShowRzedy) DrawRzedy(dc, img);
            dc.DrawRectangle(null, Frame, new Rect(Math.Round(img.X) - 0.5, Math.Round(img.Y) - 0.5, Math.Round(img.Width) + 1, Math.Round(img.Height) + 1));
        }
        DrawTracks(dc, preview ?? Obraz);
        DrawDroga(dc);
        DrawSymbole(dc, preview ?? Obraz);
        if (!Czysty) DrawKlocki(dc);
        DrawTool(dc);
        if (!Czysty && CursorPt is var (cx, cy) && cx >= 0 && cy >= 0 && cx <= Obraz.Szer && cy <= Obraz.Wys)
        {
            var c = ToScreen(cx, cy); double r = Math.Max(4, Siatka * Zoom * 0.6);
            dc.DrawLine(CursorPen, new Point(c.X - r, c.Y), new Point(c.X + r, c.Y));
            dc.DrawLine(CursorPen, new Point(c.X, c.Y - r), new Point(c.X, c.Y + r));
        }
    }

    /// <summary>Droga przebiegu wskazanego w panelu „Przebiegi od …” [j.] – łamane po torach obrazu; null – brak.</summary>
    public List<List<Point>> Droga;
    static readonly Pen DrogaPen = new(B(0x20, 0xe0, 0x50, 0xa0), 1) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };

    void DrawDroga(DrawingContext dc)
    {
        if (Droga == null) return;
        var pen = DrogaPen.Clone(); pen.Thickness = Math.Max(5, 7 * Zoom); pen.Freeze();
        foreach (var l in Droga.Where(l => l.Count >= 2))
        {
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(ToScreen(l[0].X, l[0].Y), false, false);
                foreach (var p in l.Skip(1)) c.LineTo(ToScreen(p.X, p.Y), true, true);
            }
            dc.DrawGeometry(null, pen, g);
        }
    }

    void DrawRzedy(DrawingContext dc, Rect img)
    {
        var hot = Tool == ObrTool.Prowadnica && CursorPt is var (_, cy) ? Obraz.Rzedy.Cast<int?>().FirstOrDefault(y => Math.Abs(ToScreen(0, y.Value).Y - ToScreen(0, cy).Y) <= 5) : null;
        var ys = Obraz.Rzedy.Where(y => rzadDrag is not var (from, _) || y != from).ToList();
        if (rzadDrag is var (_, now)) ys.Add(now);
        foreach (var y in ys)
        {
            double sy = Math.Round(ToScreen(0, y).Y) + 0.5;
            bool on = y == hot || rzadDrag is var (_, n) && n == y;
            dc.DrawLine(on ? RzadHotPen : RzadPen, new Point(img.Left, sy), new Point(img.Right, sy));
            var ft = new FormattedText(y.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Ui, 10, RzadText, 1.0);
            dc.DrawText(ft, new Point(img.Left - ft.Width - 4, sy - ft.Height / 2));
        }
    }

    void DrawStrefy(DrawingContext dc)
    {
        foreach (var s in Obraz.Strefy.Where(s => s.Prost?.Length == 4))
        {
            var r = new Rect(ToScreen(s.Prost[0], s.Prost[1]), ToScreen(s.Prost[0] + s.Prost[2], s.Prost[1] + s.Prost[3]));
            dc.DrawRectangle(StrefaFill, StrefaPen, r);
            var ft = new FormattedText(ObrStrefa.RodzajName(s.Rodzaj), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Ui, 11, StrefaText, 1.0) { MaxTextWidth = Math.Max(10, r.Width - 8), MaxLineCount = 2, Trimming = TextTrimming.CharacterEllipsis };
            if (r.Height > ft.Height + 6) dc.DrawText(ft, new Point(r.Left + 4, r.Top + 4));
        }
    }

    /// <summary>Przerwa przy węźle rozjazdu na nieczynnym ramieniu [j.] – jak na pulpicie (PanelView: 9 j., ROZWOJ.md § 2a).</summary>
    public const int PrzerwaRozjazdu = 9;

    /// <summary>
    /// Tory jak na pulpicie w stanie zasadniczym: szare, grubość 4,6 px przy powiększeniu 1; odgałęzienie rozjazdu z przerwą przy węźle
    /// (zasadnicze – na wprost; strona „+” – etap E3). Znaczniki edytora: końce i rozjazdy, zaznaczenie, odcinki nie co 45°, uwagi ⚠.
    /// </summary>
    void DrawTracks(DrawingContext dc, ObrObraz o)
    {
        var pen = new Pen(Grey, Math.Max(1, 4.6 * Zoom)) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
        var dash = new Pen(Grey, Math.Max(1, 4.5 * Zoom)) { DashStyle = new DashStyle(new double[] { 2.2, 1.6 }, 0) };
        var w = o.Wezly.ToDictionary(x => x.Id);
        // nieczynne ramię rozjazdu w stanie zasadniczym: odgałęzienie, a przy „+” na bok – przedłużenie na wprost
        var odg = o.Wezly.Select(x => (x.Id, e: ObrSymbole.ZwrotnicaW(o, x.Id)?.Styl == ObrSymbole.PlusBok ? ObrSymbole.OdcinekPlus(o, x.Id, false) : ObrGraf.Odgalezienie(o, x.Id)))
            .Where(z => z.e != null).ToList();
        // przerwa wzdłuż toru jak na pulpicie (PanelView skraca całą łamaną): krótki skos przy rozjeździe – przerwa przechodzi za załamanie
        // (uwaga właściciela 2026-10-22: przerwa przy rozjeździe ledwo widoczna, gdy skos krótszy niż przerwa)
        var ciecia = new Dictionary<(string E, string W), double>();
        foreach (var (wid, oe) in odg)
        {
            double reszta = PrzerwaRozjazdu; var cur = oe; var wz = wid;
            for (int i = 0; i < 8 && cur != null && reszta > 0; i++)
            {
                var (qa, qb) = ObrGraf.Konce(o, cur);
                double l = Math.Sqrt(Math.Pow(qb[0] - qa[0], 2) + Math.Pow(qb[1] - qa[1], 2));
                double c = Math.Min(reszta, l);
                ciecia[(cur.Id, wz)] = Math.Max(ciecia.GetValueOrDefault((cur.Id, wz)), c);
                reszta -= l; wz = ObrGraf.Drugi(cur, wz);
                var przy = ObrGraf.Przy(o, wz);
                cur = przy.Count == 2 ? przy.First(x => x != cur) : null;
            }
        }
        foreach (var e in o.Odcinki)
        {
            if (!w.TryGetValue(e.A ?? "", out var a) || !w.TryGetValue(e.B ?? "", out var b)) continue;
            Point pa = ToScreen(a.P), pb = ToScreen(b.P);
            // odgałęzienie: przerwa przy węźle rozjazdu, także przy kilku rozjazdach na jednym odcinku (dwa cięcia – najwyżej po połowie)
            double len = (pb - pa).Length;
            bool cutA = false, cutB = false;
            double ka = ciecia.GetValueOrDefault((e.Id, e.A)), kb = ciecia.GetValueOrDefault((e.Id, e.B));
            if (ka > 0 && kb > 0) { ka = Math.Min(ka, len / Zoom / 2 - 1); kb = Math.Min(kb, len / Zoom / 2 - 1); }
            var v = pb - pa; if (len > 1e-9) v /= len;
            if (ka > 0) { pa += v * ka * Zoom; cutA = true; }
            if (kb > 0) { pb -= v * kb * Zoom; cutB = true; }
            if ((ka > 0 || kb > 0) && (ka + kb) * Zoom >= len - 1e-6) continue;      // cały krótki odcinek w przerwie
            if (SelE.Contains(e.Id) && preview == null) dc.DrawLine(SelPen, ToScreen(a.P), ToScreen(b.P));
            // bez przerwy tor przechodzi w sąsiedni w sposób ciągły – zaokrąglony koniec wypełnia załamanie (jak PanelView)
            var capA = !cutA && ObrGraf.Przy(o, a.Id).Count >= 2 ? PenLineCap.Round : PenLineCap.Flat;
            var capB = !cutB && ObrGraf.Przy(o, b.Id).Count >= 2 ? PenLineCap.Round : PenLineCap.Flat;
            var tp = e.Styl == ObrOdcinek.BezKontroli ? dash : capA == PenLineCap.Flat && capB == PenLineCap.Flat ? pen : new Pen(Grey, pen.Thickness) { StartLineCap = capA, EndLineCap = capB };
            dc.DrawLine(tp, pa, pb);
            if (!Czysty && !ObrGraf.Osiowy(a.P, b.P)) dc.DrawLine(OddPen, ToScreen(a.P), ToScreen(b.P));
        }
        if (Czysty) return;
        foreach (var n in o.Wezly)
        {
            var c = ToScreen(n.P);
            var rodz = ObrGraf.RodzajWezla(o, n.Id);
            if (SelW.Contains(n.Id) && preview == null) dc.DrawRectangle(SelFill, null, new Rect(c.X - 4, c.Y - 4, 8, 8));
            else if (rodz == ObrGraf.Rodzaj.Koniec) dc.DrawEllipse(NodeFill, NodePen, c, 3, 3);
            else if (rodz is ObrGraf.Rodzaj.Rozjazd or ObrGraf.Rodzaj.Skrzyzowanie or ObrGraf.Rodzaj.RozjazdKrzyzowy) dc.DrawEllipse(null, NodePen, c, 3.5, 3.5);
            if (rodz == ObrGraf.Rodzaj.RozjazdKrzyzowy) dc.DrawEllipse(JoinFill, null, c, 2, 2);
        }
        foreach (var (el, _) in ObrGraf.Uwagi(o).Concat(ObrSymbole.Uwagi(o)).Concat(o.Przypiecia.Where(p => p.Stan == ObrPrzypiecie.DoWyjasnienia).Select(p => (p.El, p.Powod))))
        {
            if (o.Elementy.FirstOrDefault(x => x.Id == el) is ObrElement oe2 && ObrSymbole.Pozycja(o, oe2) is var (ex, ey, _, _))
            {
                var fe = new FormattedText("⚠", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Ui, 13, Warn, 1.0);
                var ce = ToScreen(ex, ey); dc.DrawText(fe, new Point(ce.X + 6, ce.Y - fe.Height - 6)); continue;
            }
            int[] p = w.TryGetValue(el, out var n) ? n.P : o.Odcinki.FirstOrDefault(x => x.Id == el) is ObrOdcinek e && w.ContainsKey(e.A) && w.ContainsKey(e.B)
                ? new[] { (w[e.A].P[0] + w[e.B].P[0]) / 2, (w[e.A].P[1] + w[e.B].P[1]) / 2 } : null;
            if (p == null) continue;
            var ft = new FormattedText("⚠", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Ui, 13, Warn, 1.0);
            var c = ToScreen(p);
            dc.DrawText(ft, new Point(c.X + 4, c.Y - ft.Height - 2));
        }
    }

    void DrawTool(DrawingContext dc)
    {
        if (Tool == ObrTool.Tor && Draft.Count > 0)
        {
            var pts = new List<int[]>(Draft);
            if (CursorPt is var (x, y)) pts.Add(new[] { x, y });
            for (int i = 1; i < pts.Count; i++) dc.DrawLine(DraftPen, ToScreen(pts[i - 1]), ToScreen(pts[i]));
            dc.DrawEllipse(null, DraftPen, ToScreen(Draft[0]), 4, 4);
        }
        if (rect is var (a, b, _)) dc.DrawRectangle(RectFill, RectPen, new Rect(a, b));
    }

    void DrawGrid(DrawingContext dc, Rect vis)
    {
        int g = G;
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

using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LcsWidzew.Model;
using LcsWidzew.Sim;

namespace LcsWidzew.UI;

/// <summary>
/// Obraz szczegółowy posterunku wg Ie-104 (Rozdział 2, § 4–8). Rysowany w OnRender.
/// Kółko myszy – powiększenie, przeciąganie LPM w pustym miejscu – przesuwanie.
/// </summary>
/// <summary>Tryb edycji układu – PanelView przekazuje mysz edytorowi (współrzędne świata) i rysuje jego nakładkę.</summary>
public interface IPanelEditor
{
    bool Down(Point w, MouseButtonEventArgs e);
    void Move(Point w, MouseEventArgs e);
    void Up(Point w, MouseButtonEventArgs e);
    void Right(Point w);
    void Draw(DrawingContext dc, double scale);
    void DrawBack(DrawingContext dc, double scale);
    string Hover(Point w);
}

public class PanelView : FrameworkElement
{
    public IPanelEditor Editor;
    /// <summary>Widok edytora: tory ciągłe (bez przerw położenia zwrotnic i przerw izolacyjnych – granice odcinków jako cienkie kreski).</summary>
    public bool EditView;
    /// <summary>Plan schematyczny węzła (układ torowy): białe tło, cienkie czarne tory, symbole planu – bez stanów pulpitu.</summary>
    public bool PlanView;
    /// <summary>Obraz poglądowy całego obszaru LCS (Ie-104 § 7 ust. 9): tory, sygnały, blokady i numery pociągów bez opisów; klik – obraz szczegółowy.</summary>
    public bool Overview;
    public event Action<string> OverviewClicked;
    bool editDrag;
    public double Scale => scale;
    IPulpitSource sim;
    /// <summary>Źródło meldunków (komputer zależności lokalnie albo przez sieć). Zobrazowanie korzysta tylko z układu torowego (Lay) i meldunków (I).</summary>
    public IPulpitSource Sim { get => sim; set { sim = value; I = null; } }
    /// <summary>Geometria rysowana zamiast źródła meldunków (edytor: układ torowy albo zobrazowanie).</summary>
    public Station Geometry;
    Station Lay => Geometry ?? sim?.L;
    /// <summary>Ostatni odebrany telegram stanu.</summary>
    Indication I;
    Indication Ind => I ??= sim?.Receive();
    public PageDef Page;
    public string SelStart, SelSwitch;
    public List<string> SelVia = new();
    /// <summary>Możliwe końce przebiegu od wybranego początku: klucze sygnalizatorów, znaczników końca, „blk:węzeł granicy” szlaku.</summary>
    public HashSet<string> Targets = new();

    /// <summary>Możliwy koniec przebiegu – turkusowe kółko i wypełniony kwadrat pod symbolem (jak na pulpicie).</summary>
    void TargetMark(DrawingContext dc, Rect r)
    {
        var c = new Point(r.X + r.Width / 2, r.Y + r.Height / 2);
        dc.DrawEllipse(null, new Pen(SetFrame, 1.4), c, Math.Max(r.Width, r.Height) / 2 + 5, Math.Max(r.Width, r.Height) / 2 + 5);
        dc.DrawRoundedRectangle(SetFrame, null, r, 3, 3);
    }
    public event Action<string, MouseButtonEventArgs> SignalClicked, SwitchClicked, SectionClicked, BlockClicked, TrackEndClicked, DerailerClicked, LinkClicked;
    public event Action<string> Hover;

    double scale = 0.7, offX = 20, offY = 10;
    Point? dragFrom; Point dragOff; bool dragged;

    static Brush B(byte r, byte g, byte b) { var x = new SolidColorBrush(Color.FromRgb(r, g, b)); x.Freeze(); return x; }
    // barwy wg Ie-104 § 4 ust. 13
    public static readonly Brush Bg = B(0, 0, 0), BgSz = B(0x10, 0x18, 0x40), Grey = B(0x9a, 0x9a, 0x9a), Red = B(0xff, 0x10, 0x10), Green = B(0x00, 0xe6, 0x00),
        Yellow = B(0xff, 0xf0, 0x00), Magenta = B(0xff, 0x40, 0xff), White = B(0xff, 0xff, 0xff), DarkRed = B(0x90, 0x00, 0x00), Cyan = B(0x00, 0xd0, 0xd0),
        Azure = B(0x00, 0xa0, 0xff), Orange = B(0xff, 0x8c, 0x00), Dim = B(0x40, 0x40, 0x40), Label = B(0xd0, 0xd0, 0xd0), SwLabel = B(0xb4, 0xb4, 0xb4),
        PlatformLine = B(0xb0, 0xb0, 0xb0), SigName = B(0xe8, 0xc0, 0x00), TmName = B(0x18, 0xc0, 0xc0), CrossPink = B(0xf0, 0x80, 0xf0), Dim2 = B(0x80, 0x80, 0x80);
    static readonly Brush SetFrame = B(0x20, 0xe0, 0xc0);
    static readonly Brush TargetSym = B(0x3a, 0x3a, 0x3a);
    static readonly Typeface Mono = new("Consolas"), MonoB = new(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    public PanelView()
    {
        ClipToBounds = true; Focusable = true;
        MouseWheel += OnWheel; MouseLeftButtonDown += OnLeftDown; MouseLeftButtonUp += OnLeftUp; MouseMove += OnMove;
        // przesuwanie widoku w każdym narzędziu edytora: środkowy przycisk myszy (albo Spacja + LPM)
        MouseDown += (s, e) => { if (e.ChangedButton == MouseButton.Middle) { StartPan(e.GetPosition(this)); e.Handled = true; } };
        MouseUp += (s, e) => { if (e.ChangedButton == MouseButton.Middle && panFrom != null) { panFrom = null; ReleaseMouseCapture(); Cursor = null; e.Handled = true; } };
        MouseRightButtonUp += (s, e) => { if (Editor != null) { Editor.Right(S2W(e.GetPosition(this))); InvalidateVisual(); } else HitAndRaise(e); };
    }

    bool OnPage(double x) => Page == null || (x >= Page.X0 - 1 && x <= Page.X1 + 1);
    bool OnPageNode(string nid) => OnPage(Lay.Nodes[nid].X);

    public void FitPage()
    {
        if (sim == null || Page == null || ActualWidth < 10) return;
        var ys = Lay.Nodes.Values.Where(n => n.X >= Page.X0 && n.X < Page.X1).Select(n => n.Y).ToList();
        if (ys.Count == 0) ys = new() { 300, 900 };
        double x0 = Page.X0, x1 = Page.X1, y0 = Math.Min(70, ys.Min() - 60), y1 = ys.Max() + 70;
        scale = Math.Min(ActualWidth / (x1 - x0), ActualHeight / (y1 - y0));
        offX = (ActualWidth - (x1 - x0) * scale) / 2 - x0 * scale; offY = (ActualHeight - (y1 - y0) * scale) / 2 - y0 * scale;
        InvalidateVisual();
    }

    /// <summary>Cały plan (wszystkie węzły) w oknie – po autoukładzie planu.</summary>
    public void FitAll()
    {
        if (Lay == null || Lay.Nodes.Count == 0 || ActualWidth < 10) return;
        var ns = Lay.Nodes.Values.Where(n => n.X > Station.Hidden + 1).ToList(); if (ns.Count == 0) return;
        double x0 = ns.Min(n => n.X) - 40, x1 = ns.Max(n => n.X) + 40, y0 = ns.Min(n => n.Y) - 60, y1 = ns.Max(n => n.Y) + 60;
        scale = Math.Min(ActualWidth / (x1 - x0), ActualHeight / (y1 - y0));
        offX = (ActualWidth - (x1 - x0) * scale) / 2 - x0 * scale; offY = (ActualHeight - (y1 - y0) * scale) / 2 - y0 * scale;
        InvalidateVisual();
    }

    Point S2W(Point p) => new((p.X - offX) / scale, (p.Y - offY) / scale);
    /// <summary>Widoczny fragment obrazu we współrzędnych układu.</summary>
    public Rect ViewWorld => new(S2W(new Point(0, 0)), S2W(new Point(ActualWidth, ActualHeight)));
    public void SetZoom(double s) { scale = s; }
    public void CenterOn(double x, double y) { offX = ActualWidth / 2 - x * scale; offY = ActualHeight / 2 - y * scale; InvalidateVisual(); }

    // ------------------------------------------------------------------ mysz
    Point? panFrom; Point panOff;
    void StartPan(Point p) { Focus(); panFrom = p; panOff = new Point(offX, offY); CaptureMouse(); Cursor = Cursors.SizeAll; }
    void OnWheel(object s, MouseWheelEventArgs e)
    {
        var p = e.GetPosition(this); var w = S2W(p);
        // szeroki zakres: cała linia w skali rzeczywistej (oddalenie) i szczegóły zdjęcia (przybliżenie)
        scale = Math.Clamp(scale * (e.Delta > 0 ? 1.15 : 1 / 1.15), Editor != null ? 0.004 : 0.2, Editor != null ? 60 : 6);
        offX = p.X - w.X * scale; offY = p.Y - w.Y * scale;
        InvalidateVisual();
    }
    void OnLeftDown(object s, MouseButtonEventArgs e)
    {
        Focus();
        if (Keyboard.IsKeyDown(Key.Space)) { StartPan(e.GetPosition(this)); e.Handled = true; return; }
        if (Editor != null && Editor.Down(S2W(e.GetPosition(this)), e)) { editDrag = true; CaptureMouse(); InvalidateVisual(); return; }
        dragFrom = e.GetPosition(this); dragOff = new Point(offX, offY); dragged = false; CaptureMouse();
    }
    void OnLeftUp(object s, MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();
        if (panFrom != null) { panFrom = null; Cursor = null; return; }
        if (editDrag) { editDrag = false; Editor?.Up(S2W(e.GetPosition(this)), e); InvalidateVisual(); return; }
        var was = dragged; dragFrom = null;
        if (!was && Editor == null && Overview) { var w = S2W(e.GetPosition(this)); var pg = Lay?.PageOfX(w.X); if (pg != null) OverviewClicked?.Invoke(pg.Id); return; }
        if (!was && Editor == null) HitAndRaise(e);
    }
    void OnMove(object s, MouseEventArgs e)
    {
        var p = e.GetPosition(this);
        if (panFrom is Point pf)
        {
            if (e.MiddleButton == MouseButtonState.Pressed || e.LeftButton == MouseButtonState.Pressed) { offX = panOff.X + (p.X - pf.X); offY = panOff.Y + (p.Y - pf.Y); InvalidateVisual(); return; }
            panFrom = null; ReleaseMouseCapture(); Cursor = null;
        }
        if (Editor != null && (editDrag || dragFrom == null))
        {
            Editor.Move(S2W(p), e);
            Hover?.Invoke(Editor.Hover(S2W(p)));
            InvalidateVisual();
            return;
        }
        if (dragFrom is Point f && e.LeftButton == MouseButtonState.Pressed)
        {
            if ((p - f).Length > 4) dragged = true;
            if (dragged) { offX = dragOff.X + (p.X - f.X); offY = dragOff.Y + (p.Y - f.Y); InvalidateVisual(); }
            return;
        }
        var hit = HitTest(S2W(p));
        Hover?.Invoke(hit.kind == null ? "" : Describe(hit.kind, hit.id));
        Cursor = hit.kind == null ? Cursors.Arrow : Cursors.Hand;
    }

    void HitAndRaise(MouseButtonEventArgs e)
    {
        var (kind, id) = HitTest(S2W(e.GetPosition(this)));
        switch (kind)
        {
            case "sig": SignalClicked?.Invoke(id, e); break;
            case "sw": SwitchClicked?.Invoke(id, e); break;
            case "sec": SectionClicked?.Invoke(id, e); break;
            case "blk": BlockClicked?.Invoke(id, e); break;
            case "te": TrackEndClicked?.Invoke(id, e); break;
            case "der": DerailerClicked?.Invoke(id, e); break;
            case "link": LinkClicked?.Invoke(id, e); break;
        }
    }

    public (string kind, string id) HitTest(Point w)
    {
        if (sim == null || !OnPage(w.X)) return (null, null);
        var L = Lay; double r = 9;
        foreach (var s in L.Signals.Values)
        {
            if (s.Type is "odstepowy" or "to" or "sp") continue;
            var n = L.Nodes[s.Node];
            if (Math.Abs(w.X - (n.X - s.VDir * 4)) < 13 && Math.Abs(w.Y - n.Y) < 9) return ("sig", s.Name);
            if (s.Type == "wjazdowy" && Math.Abs(w.X - (n.X + s.VDir * 16)) < 8 && Math.Abs(w.Y - n.Y) < 9) return ("sig", s.Name);   // trójkąt końca przebiegu wyjazdowego
        }
        foreach (var sw in L.Switches.Values) { var n = L.Nodes[sw.Node]; if (Math.Abs(w.X - n.X) < r && Math.Abs(w.Y - n.Y) < r + 4) return ("sw", sw.Num); }
        foreach (var d in L.Derailers.Values) { var n = L.Nodes[d.Node]; if (Math.Abs(w.X - n.X) < r && Math.Abs(w.Y - n.Y) < r + 4) return ("der", d.Name); }
        foreach (var b in L.Blocks.Values)
        {
            if (MidBlock(b)) { if (MidRect(b).Contains(w)) return ("blk", b.Id); continue; }
            for (int i = 0; i < b.Ends.Count; i++) if (OnPageNode(b.Ends[i].Boundary) && BlockRect(b, i).Contains(w)) return ("blk", b.Id);
        }
        foreach (var (nid, te) in L.TrackEnds) { var n = L.Nodes[nid]; if (Math.Abs(w.X - n.X) < 14 && Math.Abs(w.Y - n.Y) < 16) return ("te", te.Id); }
        foreach (var e in L.Edges.Values)
        {
            if (e.Link) continue;
            for (int i = 0; i < e.Pts.Count - 1; i++)
                if (DistSeg(w, e.Pts[i], e.Pts[i + 1]) < 5) return ("sec", e.Sec);
        }
        return (null, null);
    }
    static double DistSeg(Point p, (double X, double Y) a, (double X, double Y) b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
        double t = l2 == 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2, 0, 1);
        return Math.Sqrt(Math.Pow(p.X - a.X - t * dx, 2) + Math.Pow(p.Y - a.Y - t * dy, 2));
    }

    string Describe(string kind, string id)
    {
        var S = Ind; var L = Lay; if (S == null) return ""; var lbl = Station.Lbl(id);
        switch (kind)
        {
            case "sig":
            {
                var s = S.Sigs[id]; var d = L.Signals[id];
                var asp = s.Aspect switch { Aspect.Train => "zezwalający dla pociągu", Aspect.Shunt => "zezwalający dla manewru", Aspect.Sz => $"zastępczy ({Math.Max(0, s.SzLeft):0} s)", _ => "„Stój”" };
                var t = d.Type == "tm" ? "Tarcza manewrowa" : d.Type == "wjazdowy" ? "Semafor wjazdowy" : "Semafor";
                return $"{t} {lbl}: {asp}{(s.Closed ? ", zastopowany" : "")}   ·  LPM: początek / koniec przebiegu, Ctrl+LPM: punkt pośredni, PPM: polecenia";
            }
            case "sw":
            {
                var w = S.Sws[id];
                return $"Zwrotnica {L.Switches[id].Label} (na bok {L.Switches[id].VBok:0} km/h{(L.Switches[id].VProsto > 0 ? $", na wprost {L.Switches[id].VProsto:0} km/h" : "")}): „{w.Pos}”{(w.Moving != null ? ", przestawianie" : "")}{(w.Lock != null ? ", utwierdzona" : "")}{(w.Stopped ? ", ZASTOPOWANA" : "")}{(w.Rozpr ? ", ROZPRUCIE" : "")}{(w.NoCtrl ? ", BRAK KONTROLI" : "")}   ·  LPM: polecenia, Ctrl+LPM: punkt pośredni";
            }
            case "sec":
            {
                var s = S.Secs[id];
                var st = s.Fault switch { "reset" => "wymagane potwierdzenie zerowania", "await" => "oczekiwanie na pierwszy przejazd", "occ" => "zajęty (usterka?)", _ => s.TrainOcc ? "zajęty" : "wolny" };
                var vs = L.Sections[id].Edges.Select(x => L.Edges[x]).Select(x => x.VMax > 0 ? x.VMax : 0).Where(x => x > 0).Distinct().ToList();
                return $"Odcinek {lbl} – {L.Sections[id].DetectionName}, posterunek {L.Posts.GetValueOrDefault(Model.StationDoc.SecPost(id)) ?? "?"} ({L.Sections[id].Edges.Sum(x => L.Edges[x].Len):0} m{(vs.Count > 0 ? ", V " + string.Join("/", vs) + " km/h" : "")}): {st}{(s.LockType != null ? ", utwierdzony" : "")}{(s.Closed ? ", ZAMKNIĘTY" : "")}   ·  PPM: polecenia";
            }
            case "blk":
            {
                var b = L.Blocks[id]; var bs = S.Blocks[id]; int e = EndIdxOnPage(b);
                var req = bs.Req == null ? "" : bs.Req.EndIdx == e ? ", wysłano żądanie pozwolenia" : ", ŻĄDANIE POZWOLENIA od strony przeciwnej";
                return $"Szlak {id} – {b.TypeName} (LK{b.Line}, tor {b.Tor}, {b.Nbr}): {(b.NoBlock ? "bez blokady" : bs.Dir == 0 ? "stan neutralny" : bs.Dir == b.Ends[e].Outward ? "kierunek WYJAZD" : "kierunek PRZYJAZD")}{req}{(bs.AwaitEnd != null ? ", ZABLOKOWANY – brak bloku końcowego" : "")}{(bs.StopEnd[e] ? ", wyjazd zamknięty" : "")}   ·  LPM: koniec przebiegu, PPM: polecenia";
            }
            case "te": return $"Znacznik końca przebiegu manewrowego {Station.Lbl(id)}";
            case "der": return $"Wykolejnica {L.Derailers[id].Label}: {(S.Ders[id].On ? "założona" : "zdjęta")}";
        }
        return "";
    }

    /// <summary>Koniec szlaku po stronie posterunku wyświetlanego obrazu.</summary>
    int EndIdxOnPage(BlockDef b)
    {
        for (int i = 0; i < b.Ends.Count; i++) if (Page != null && Page.Posts.Contains(b.Ends[i].Post)) return i;
        return 0;
    }

    /// <summary>Kierunek toru szlakowego od granicy (semafora wjazdowego) na zewnątrz stacji – wektor jednostkowy.</summary>
    Vector BlockAxis(BlockDef b, int endIdx)
    {
        var en = b.Ends[endIdx]; var L = Lay; var n = L.Nodes[en.Boundary];
        var e = n.Edges.Select(x => L.Edges[x]).FirstOrDefault(x => b.SecsWE.Contains(x.Sec)) ?? n.Edges.Select(x => L.Edges[x]).FirstOrDefault(x => L.GeoX(x.Id, n.Id) == en.VOut);
        return e != null ? Tangent(e, n.Id) : new Vector(en.VOut, 0);
    }

    /// <summary>Szlak wewnętrzny z obu końcami na wyświetlanym obrazie – jeden symbol blokady w połowie szlaku.</summary>
    // szlak wewnętrzny jak w SimRail / EbiScreen: strzałka przy każdym końcu (własne oznaczenie), obie wskazują ten sam kierunek
    public bool MidBlock(BlockDef b) => false;

    /// <summary>Środek szlaku (połowa długości torów szlakowych) i kierunek „na wschód” w tym miejscu.</summary>
    (Point P, Vector U) BlockMid(BlockDef b)
    {
        // łamana szlaku od granicy jednego posterunku do drugiego, punkt w połowie jej długości (na odcinku poziomym, jeśli to możliwe)
        var L = Lay; var pts = new List<Point>();
        string node = b.Ends[0].Boundary, from = null;
        for (int k = 0; k < 200; k++)
        {
            var next = L.Nodes[node].Edges.FirstOrDefault(x => x != from && b.SecsWE.Contains(L.Edges[x].Sec));
            if (next == null) break;
            var e = L.Edges[next]; var ep = e.Pts.Select(q => new Point(q.X, q.Y)).ToList(); if (e.A != node) ep.Reverse();
            if (pts.Count > 0) ep.RemoveAt(0);
            pts.AddRange(ep); from = next; node = L.Other(next, node);
        }
        if (pts.Count < 2) { var n0 = L.Nodes[b.Ends[0].Boundary]; return (new Point(n0.X, n0.Y), new Vector(1, 0)); }
        double total = 0; for (int i = 0; i < pts.Count - 1; i++) total += (pts[i + 1] - pts[i]).Length;
        // najdłuższy odcinek poziomy najbliżej połowy łamanej
        double acc = 0, best = double.MaxValue; Point bp = pts[0]; Vector bu = new(1, 0);
        for (int i = 0; i < pts.Count - 1; i++)
        {
            var v = pts[i + 1] - pts[i]; var len = v.Length;
            if (len > 40 && Math.Abs(v.Y) < 0.5)
            {
                // punkt możliwie blisko połowy szlaku, z dala od styków (granica odstępów – opis sygnalizatora SBL)
                double m = Math.Min(45, len / 2), t = Math.Clamp(total / 2 - acc, m, len - m), dist = Math.Abs(acc + t - total / 2);
                if (dist < best - 0.5) { best = dist; bp = pts[i] + v * (t / len); bu = v / len; }
            }
            acc += len;
        }
        if (best == double.MaxValue) { bp = pts[pts.Count / 2]; bu = new Vector(1, 0); }
        if (bu.X < 0) bu = -bu;
        return (bp, bu);
    }

    Rect MidRect(BlockDef b) { var (p, u) = BlockMid(b); var r = new Rect(p - u * 18, p + u * 18); r.Inflate(6, 8); r.Union(new Point(r.X, p.Y - 22)); return r; }

    /// <summary>Symbol blokady szlaku wewnętrznego: jedna strzałka w osi toru w połowie szlaku, grot w kierunku jazdy.</summary>
    void DrawBlockMid(DrawingContext dc, BlockDef b, double dpi)
    {
        var S = Ind; var bs = S.Blocks[b.Id]; var (mp, u) = BlockMid(b);
        Brush sa = Grey, sb = Grey; int arrowDir;
        bool closed = bs.StopEnd[0] || bs.StopEnd[1];
        if (bs.AzkUntil != null) { arrowDir = bs.AzkDir; sa = sb = BlinkOn ? Red : Dim; }
        else if (bs.Req != null) { arrowDir = b.Ends[bs.Req.EndIdx >= 0 ? bs.Req.EndIdx : 0].Outward; sa = BlinkOn ? Yellow : Dim; sb = Yellow; }
        else if (bs.Dir == 0) arrowDir = 0;
        else { arrowDir = bs.Dir; sa = sb = Yellow; if (bs.ReleaseUntil != null) sb = BlinkOn ? Yellow : Dim; if (closed) sa = sb = Magenta; }
        if (bs.NoData) { arrowDir = 0; sa = sb = White; }
        else if (b.NoBlock) { arrowDir = 0; sa = sb = Dim; }
        else if (b.OneWay && bs.AzkUntil == null) { arrowDir = b.DefDir; if (sa == Grey) sa = sb = White; }
        if (bs.AwaitEnd != null) { sb = Red; if (!b.SecsWE.Any(S.SecOcc) && !BlinkOn) sb = Dim; if (sa == Grey || sa == White) sa = Red; }
        // kierunek blokady (wg toru A→B) → zwrot strzałki na rysunku (oś u w prawo)
        if (b.Ends.Count > 0) arrowDir *= b.Ends[0].VOut * b.Ends[0].Outward;
        Point P(double d) => mp + u * d;
        dc.DrawLine(new Pen(Bg, 9), P(-18), P(18));
        void Head(double at, int toward, Brush c)
        {
            var tip = P(at); var back = P(at - toward * 10); var nrm = new Vector(-u.Y, u.X) * 6.5;
            var g = new StreamGeometry();
            using (var ctx = g.Open()) { ctx.BeginFigure(tip, true, true); ctx.LineTo(back + nrm, true, false); ctx.LineTo(back - nrm, true, false); }
            g.Freeze(); dc.DrawGeometry(c, null, g);
        }
        var shaft = new Pen(sb, 6) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
        if (arrowDir == 0) { var nc = bs.NoData ? White : Grey; dc.DrawLine(new Pen(nc, 6), P(-7), P(7)); Head(16, 1, nc); Head(-16, -1, nc); }
        else if (arrowDir > 0) { dc.DrawLine(shaft, P(-16), P(7)); Head(16, 1, sa); }
        else { dc.DrawLine(shaft, P(-7), P(16)); Head(-16, -1, sa); }
        Text(dc, b.Id, mp.X, mp.Y - 14, 10, SigName, true, dpi, Mono);
        if (FreeBar(b)) dc.DrawRectangle(Green, null, new Rect(mp.X - 6, mp.Y - 25, 12, 3));
        if (closed && BlinkOn) { dc.DrawRectangle(Red, null, new Rect(mp.X + 16, mp.Y + 8, 24, 9)); Text(dc, "STOP", mp.X + 28, mp.Y + 12.5, 6.5, White, true, dpi, MonoB); }
        var rc = MidRect(b);
        if (S.Pending?.Targets.Contains(b.Id) == true) dc.DrawRectangle(null, new Pen(Orange, 2), rc);
    }

    /// <summary>Obszar symbolu blokady przy danym końcu (strzałka w osi toru za semaforem wjazdowym i oznaczenie szlaku nad nią).</summary>
    Rect BlockRect(BlockDef b, int endIdx)
    {
        var bn = Lay.Nodes[b.Ends[endIdx].Boundary]; var u = BlockAxis(b, endIdx);
        var p0 = new Point(bn.X + u.X * 14, bn.Y + u.Y * 14); var p1 = new Point(bn.X + u.X * 46, bn.Y + u.Y * 46);
        var r = new Rect(p0, p1); r.Inflate(4, 6); r.Union(new Point(r.X, bn.Y - 22));
        return r;
    }

    /// <summary>Napisy łączników obrazów widoczne na bieżącym obrazie: prostokąt przy końcu toru i identyfikator obrazu docelowego.</summary>
    List<(Rect R, string Page)> LinkTags() => LinkTagsDir().Select(x => (x.R, x.Page)).ToList();
    List<(Rect R, string Page, int D)> LinkTagsDir()
    {
        var res = new List<(Rect, string, int)>();
        if (sim == null || Page == null) return res;
        var L = Lay;
        foreach (var e in L.Edges.Values.Where(x => x.Link))
            foreach (var (here, there) in new[] { (e.A, e.B), (e.B, e.A) })
            {
                if (!OnPageNode(here) || OnPageNode(there)) continue;
                var n = L.Nodes[here]; var tp = L.PageOfNode(there);
                int d = Math.Sign(L.Nodes[there].X - n.X); if (d == 0) d = 1;
                double w = Math.Max(60, (tp.Name.Length + 2) * 5.6);
                // nad końcem toru, od strony obrazu (żeby nie wychodził poza jego krawędź)
                res.Add((new Rect(d > 0 ? n.X - w : n.X, n.Y - 24, w, 14), tp.Id, d));
            }
        return res;
    }

    /// <summary>Zielona kreska nad strzałką: szlak wolny i nie nastawiono przebiegu na ten szlak.</summary>
    bool FreeBar(BlockDef b) => !b.NoBlock && !Ind.NoLink && !b.SecsWE.Any(Ind.SecOcc) && !Ind.Routes.Values.Any(r => r.End == b.Id) && Ind.Blocks[b.Id].AwaitEnd == null;

    bool BlinkOn => (Environment.TickCount64 / 500) % 2 == 0;

    // ------------------------------------------------------------------ rysowanie
    protected override void OnRender(DrawingContext dc)
    {
        I = sim?.Receive();                                    // telegram stanu z komputera zależności
        var bgBrush = PlanView ? Brushes.White : I?.Pending?.Code == "SZ" ? BgSz : Bg;   // Ie-104 § 4 ust. 18 – przekolorowanie tła przed sygnałem zastępczym
        dc.DrawRectangle(bgBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));
        if (PlanView && Lay != null && Page != null)
        {
            dc.PushTransform(new MatrixTransform(scale, 0, 0, scale, offX, offY));
            try { Editor?.DrawBack(dc, scale); DrawPlan(dc, Lay, VisualTreeHelper.GetDpi(this).PixelsPerDip); Editor?.Draw(dc, scale); RenderError = null; }
            catch (Exception ex) { RenderError = ex.Message; }
            finally { dc.Pop(); }
            return;
        }
        if (sim == null || Page == null || I == null || Lay == null) return;
        var S = I; var L = Lay;
        dc.PushTransform(new MatrixTransform(scale, 0, 0, scale, offX, offY));
        dc.PushClip(new RectangleGeometry(new Rect(Page.X0, -100, Page.X1 - Page.X0, 1400)));
        fontK = Math.Clamp(0.95 / scale, 0.9, 1.7);
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        double cxPage = (Page.X0 + Page.X1) / 2;
        try
        {
        Editor?.DrawBack(dc, scale);

        Text(dc, $"Dyżurny ruchu: {S.User?.Name ?? "–"}", cxPage, 88, 10, Dim2, true, dpi, Mono);
        Text(dc, Page.Name.ToUpperInvariant(), cxPage, 118, 20, SwLabel, true, dpi, Mono);
        DrawControlSymbol(dc, Page.X1 - 120, 80);

        var penPlat = new Pen(PlatformLine, 1.4);
        foreach (var p in L.Platforms.Where(p => OnPage(p.X)))
        {
            dc.DrawRectangle(null, penPlat, new Rect(p.X, p.Y, p.W, p.H));
            dc.DrawRectangle(null, new Pen(PlatformLine, 0.8), new Rect(p.X + 3, p.Y + 3, p.W - 6, p.H - 6));
            Text(dc, p.Name, p.X + p.W / 2, p.Y + p.H / 2, 10, Label, true, dpi, Mono);
        }
        foreach (var l in L.Labels.Where(l => OnPage(l.X)))
        {
            if (l.Anchor == "nastawnia")
            {
                // nastawnia: ikona i skrót posterunku w cudzysłowie
                var ip = new Pen(Dim2, 1.4);
                dc.DrawRectangle(null, ip, new Rect(l.X - 11, l.Y - 22, 22, 15));
                dc.DrawRectangle(null, ip, new Rect(l.X - 7, l.Y - 19, 14, 6));
                dc.DrawEllipse(Dim2, null, new Point(l.X, l.Y - 10), 1.6, 1.6);
                Text(dc, "„" + l.Text + "”", l.X, l.Y + 3, 10, Dim2, true, dpi, Mono);
                continue;
            }
            Text(dc, l.Text, l.X, l.Y, 10, Grey, l.Anchor == "middle", dpi, Mono, l.Anchor == "end");
        }
        var dash = new Pen(Grey, 4.5) { DashStyle = new DashStyle(new double[] { 2.2, 1.6 }, 0) };
        foreach (var d in L.Dashed.Where(d => OnPage(d.X0))) dc.DrawLine(dash, new Point(d.X0, d.Y), new Point(d.X1, d.Y));
        foreach (var c in L.Crossings.Where(c => OnPage(c.X)))
        {
            dc.DrawLine(new Pen(CrossPink, 3), new Point(c.X, c.Y0 + 14), new Point(c.X, c.Y1 - 14));
            DrawCrossMark(dc, c.X, c.Y0, -1, c.Kind); DrawCrossMark(dc, c.X, c.Y1, 1, c.Kind);
            Text(dc, c.Name, c.X + 8, c.Y0 - 4, 8, CrossPink, false, dpi, Mono);
        }

        // tory
        var secCache = new Dictionary<string, Brush>();
        var pend = S.Pending?.Targets ?? new List<string>();
        // nieczynne odgałęzienie zwrotnicy – szare na całej długości łącznika (ukos – pion – ukos), do następnej zwrotnicy
        var deadLeg = new HashSet<string>();
        if (!EditView)
            foreach (var (sw, def) in L.Switches)
            {
                var w = S.Sws[sw]; if (w.Moving != null || w.NoCtrl) continue;
                var leg = w.Pos == "+" ? def.Minus : def.Plus; if (leg == null) continue;
                string cur = def.Node, eid = leg;
                for (int g = 0; g < 40 && eid != null; g++)
                {
                    if (!L.Edges.TryGetValue(eid, out var ce) || ce.Link) break;
                    if (g > 0) deadLeg.Add(eid);
                    var o = L.Other(eid, cur); var on = L.Nodes[o];
                    if (on.Switch != null || on.Edges.Count != 2 || on.Signals.Count > 0) break;
                    eid = on.Edges.FirstOrDefault(x => x != eid); cur = o;
                }
            }
        foreach (var e in L.Edges.Values)
        {
            if (e.Link) continue;                                   // łącznik obrazów – tylko logiczny
            if (!OnPageNode(e.A) && !OnPageNode(e.B)) continue;
            if (!secCache.TryGetValue(e.Sec, out var col)) secCache[e.Sec] = col = SecBrush(e.Sec);
            var pts = e.Pts.Select(p => new Point(p.X, p.Y)).ToList();
            double gapA = EndGap(e, e.A), gapB = EndGap(e, e.B); Brush c = deadLeg.Contains(e.Id) ? Grey : col;
            foreach (var n in new[] { e.A, e.B })
            {
                if (EditView) break;
                var sw = L.Nodes[n].Switch; if (sw == null) continue;
                var def = L.Switches[sw]; var w = S.Sws[sw];
                bool moving = w.Moving != null || w.NoCtrl;
                if (def.Plus != e.Id && def.Minus != e.Id)
                {
                    // iglica: w trakcie przekładania (albo bez kontroli położenia) odcięta od obu odgałęzień
                    if (moving && def.Tip == e.Id) { if (n == e.A) gapA = Math.Max(gapA, 4); else gapB = Math.Max(gapB, 4); }
                    continue;
                }
                bool active = !moving && w.Pos == (def.Plus == e.Id ? "+" : "-");
                // nieczynne odgałęzienie: wyraźna przerwa przy zwrotnicy (szersza niż zaokrąglony koniec toru)
                if (!active) { c = w.NoData ? White : Grey; if (n == e.A) gapA = 9; else gapB = 9; }
            }
            Shorten(pts, gapA, gapB);
            if (pend.Contains(e.Sec)) DrawPoly(dc, new Pen(Orange, 11), pts);
            bool closed = S.Secs[e.Sec].Closed;
            // bez przerwy izolacyjnej tor przechodzi w sąsiedni w sposób ciągły (zaokrąglony koniec wypełnia załamanie)
            var capA = gapA == 0 && L.Nodes[e.A].Edges.Count >= 2 ? PenLineCap.Round : PenLineCap.Flat;
            var capB = gapB == 0 && L.Nodes[e.B].Edges.Count >= 2 ? PenLineCap.Round : PenLineCap.Flat;
            var pen = new Pen(c, closed ? 7 : 4.6) { StartLineCap = capA, EndLineCap = capB, LineJoin = PenLineJoin.Round };
            if (e.Dismantled) pen = new Pen(Dim2, 4.6) { DashStyle = new DashStyle(new double[] { 2.2, 1.4 }, 0), LineJoin = PenLineJoin.Round };
            // odstęp blokowy wolny przy nieustawionym kierunku – szary migający (Ie-104 § 8 pkt 17 ust. 2 lit. d)
            if (e.Szlak && !S.NoLink && S.Blocks.TryGetValue(e.Sec.Split('-')[0], out var bsz) && bsz.Dir == 0 && !S.SecOcc(e.Sec) && !BlinkOn) pen = new Pen(Dim, 4.6);
            DrawPoly(dc, pen, pts);
            if (closed) DrawPoly(dc, new Pen(bgBrush, 2.4), pts);
        }

        if (EditView)
        {
            var jp = new Pen(B(0x10, 0x10, 0x10), 1.4);
            foreach (var n in L.Nodes.Values)
            {
                if (n.Edges.Count < 2 || !OnPage(n.X)) continue;
                var secs = n.Edges.Select(x => L.Edges[x].Sec).Distinct().ToList();
                if (secs.Count < 2) continue;
                // kreska poprzeczna na każdym torze, który zaczyna w węźle inny odcinek niż pozostałe
                foreach (var x in n.Edges)
                {
                    var ex = L.Edges[x]; if (n.Edges.Count(y => L.Edges[y].Sec == ex.Sec) > 1) continue;
                    var t = Tangent(ex, n.Id); var pnt = new Point(n.X + t.X * 2.5, n.Y + t.Y * 2.5); var nrm = new Vector(-t.Y, t.X) * 3.2;
                    dc.DrawLine(jp, pnt - nrm, pnt + nrm);
                }
            }
        }

        // oznaczenia torów stacyjnych i wyświetlacze numerów pociągów (Ie-104 § 8 pkt 26)
        var winTrains = S.Windows;
        foreach (var (sec, sd) in L.Sections)
        {
            bool track = sec.Contains(":JT"), szl = sd.Kind == "szlak";
            if (!track && !szl) continue;
            var e = sd.Edges.Select(x => L.Edges[x]).Where(x => !x.Link).OrderByDescending(x => x.Len).FirstOrDefault();
            if (e == null) continue;
            var a = e.Pts[0]; var b = e.Pts[^1];
            var mx = (a.X + b.X) / 2; var my = e.Pts.Count > 2 ? e.Pts[e.Pts.Count / 2].Y : (a.Y + b.Y) / 2;
            if (!OnPage(mx)) continue;
            if (winTrains.TryGetValue(sec, out var trs))
            {
                int ti = trs.Count == 1 ? 0 : (int)(Environment.TickCount64 / 1000 % trs.Count);
                var txt = trs.Count > 1 ? (ti == 0 ? trs[ti] + "*" : "*" + trs[ti]) : trs[0];
                bool occ = S.Secs[sec].TrainOcc;
                var ft = new FormattedText(txt, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, MonoB, 10 * fontK, occ ? White : SecBrush(sec), dpi);
                var rr = new Rect(mx - ft.Width / 2 - 3, my - ft.Height / 2, ft.Width + 6, ft.Height);
                dc.DrawRectangle(occ ? Red : Bg, null, rr);
                dc.DrawText(ft, new Point(rr.X + 3, rr.Y));
            }
            else if (track)
            {
                dc.DrawRectangle(bgBrush, null, new Rect(mx - 10, my - 6, 20, 12));
                Text(dc, e.Track, mx, my, 9.5, SwLabel, true, dpi, Mono);
            }
        }

        // zwrotnice
        foreach (var sw in L.Switches.Values)
        {
            var n = L.Nodes[sw.Node]; if (!OnPage(n.X)) continue;
            var w = S.Sws[sw.Num];
            Brush mark = null;
            if (w.NoData) mark = White;
            else if (w.Rozpr) mark = BlinkOn ? Red : null;
            else if (w.NoCtrl || (w.Moving != null && w.Frozen && S.T - w.Moving.Started > Simulator.Cfg.SwTimeout)) mark = BlinkOn ? White : Red;
            else if (w.Stopped) mark = Magenta;
            else if (w.Local) mark = Cyan;
            bool minusDown = L.Edges[sw.Minus].Pts.Any(p => p.Y > n.Y + 1);
            double ly = minusDown ? n.Y - 12 : n.Y + 13, lx = n.X + (sw.TipDir < 0 ? 8 : -8);
            if (pend.Contains(sw.Num)) dc.DrawRectangle(Orange, null, new Rect(lx - 14, ly - 7, 28, 14));
            if (mark != null) dc.DrawRectangle(mark, null, new Rect(n.X - 4, n.Y - 4, 8, 8));
            Text(dc, sw.Label, lx, ly, 9.5, w.Lock != null ? Green : White, true, dpi, Mono);
            // „+” – położenie zasadnicze, od strony odgałęzienia zasadniczego
            int pd = L.EdgeDir(sw.Plus, sw.Node); if (pd == 0) pd = -sw.TipDir;
            Text(dc, "+", lx + pd * (5 + sw.Label.Length * 3.2), ly - 2, 8, White, true, dpi, Mono);
            if (SelVia.Contains(sw.Num) || SelVia.Contains(sw.Num + "+") || SelVia.Contains(sw.Num + "-"))
            {
                // wariant przebiegu przez tę zwrotnicę – turkusowy prostokąt na torze
                dc.DrawRoundedRectangle(SetFrame, null, new Rect(n.X - 11, n.Y - 10, 22, 20), 2.5, 2.5);
                dc.DrawLine(new Pen(TargetSym, 3), new Point(n.X - 8, n.Y), new Point(n.X + 8, n.Y));
            }
            else if (SelSwitch == sw.Num)
            {
                // zaznaczona zwrotnica – ramka wokół jej zobrazowania na torze (iglica i początki obu odgałęzień)
                double fx = n.X - sw.TipDir * 6;
                dc.DrawRoundedRectangle(null, new Pen(SetFrame, 2), new Rect(fx - 15, n.Y - 9 + (minusDown ? 3 : -3), 30, 18), 3, 3);
            }
        }
        foreach (var d in L.Derailers.Values)
        {
            var n = L.Nodes[d.Node]; if (!OnPage(n.X)) continue;
            var st = S.Ders[d.Name];
            var c = st.MovingTo != null ? (BlinkOn ? White : Grey) : st.Lock != null ? Yellow : st.On ? Grey : Dim;
            dc.DrawRectangle(c, null, new Rect(n.X - 2.5, n.Y - 9, 5, 18));
            Text(dc, d.Label, n.X, n.Y + 17, 9, SwLabel, true, dpi, Mono);
        }
        // kozły oporowe – sam symbol: nawias zamykający koniec toru, otwarty w stronę toru
        foreach (var nid in L.Bumpers)
        {
            if (!L.Nodes.TryGetValue(nid, out var n) || !OnPage(n.X) || n.Edges.Count != 1) continue;
            int od = -L.EdgeDir(n.Edges[0], nid); if (od == 0) od = 1;                 // na zewnątrz toru
            double bx = n.X + od * 1, t = -od * 6;
            var pen = new Pen(Grey, 2.4) { StartLineCap = PenLineCap.Square, EndLineCap = PenLineCap.Square };
            dc.DrawLine(pen, new Point(bx, n.Y - 8), new Point(bx, n.Y + 8));
            dc.DrawLine(pen, new Point(bx, n.Y - 8), new Point(bx + t, n.Y - 8));
            dc.DrawLine(pen, new Point(bx, n.Y + 8), new Point(bx + t, n.Y + 8));
        }
        // znaczniki końca przebiegu manewrowego (półkole, Ie-104 § 8 pkt 6) i kozły
        foreach (var (nid, te) in L.TrackEnds)
        {
            var n = L.Nodes[nid]; if (!OnPage(n.X)) continue;
            if (Targets.Contains(te.Id)) TargetMark(dc, new Rect(n.X - 10, n.Y - 19, 20, 14));
            dc.DrawLine(new Pen(Grey, 3), new Point(n.X, n.Y - 7), new Point(n.X, n.Y + 7));
            if (!te.Marker) continue;
            bool end = S.Routes.Values.Any(r => r.End == te.Id && r.State != "setting");
            var g = new StreamGeometry();
            using (var ctx = g.Open()) { ctx.BeginFigure(new Point(n.X - 6, n.Y - 9), false, false); ctx.ArcTo(new Point(n.X + 6, n.Y - 9), new Size(6, 6), 0, false, SweepDirection.Clockwise, true, false); }
            dc.DrawGeometry(null, new Pen(end ? Red : Grey, 1.6), g);
            Text(dc, te.Disp, n.X, n.Y + 15, 9, TmName, true, dpi, Mono);
        }

        foreach (var s in L.Signals.Values) if (OnPageNode(s.Node)) DrawSignal(dc, s, dpi);
        foreach (var b in L.Blocks.Values)
        {
            if (MidBlock(b)) { DrawBlockMid(dc, b, dpi); continue; }
            for (int i = 0; i < b.Ends.Count; i++) if (OnPageNode(b.Ends[i].Boundary)) DrawBlock(dc, b, i, dpi);
        }

        if (Overview)
        {
            // nazwy posterunków nad ich układem torowym, ramka obrazu szczegółowego
            foreach (var (pid, pname) in L.Posts)
            {
                var ns = L.Nodes.Values.Where(n => n.Post == pid && n.Edges.Count > 1).ToList(); if (ns.Count == 0) continue;
                double x0 = ns.Min(n => n.X), x1 = ns.Max(n => n.X), y0 = ns.Min(n => n.Y);
                Text(dc, pname.ToUpperInvariant(), (x0 + x1) / 2, y0 - 40, 26, SwLabel, true, dpi, Mono);
            }
            foreach (var p in L.Pages) dc.DrawRectangle(null, new Pen(Dim, 2) { DashStyle = DashStyles.Dash }, new Rect(p.X0 + 4, 20, p.X1 - p.X0 - 8, 1240));
        }
        Editor?.Draw(dc, scale);
        RenderError = null;
        }
        catch (Exception ex) { RenderError = ex.Message; }
        finally { dc.Pop(); dc.Pop(); }
        if (RenderError != null)
            dc.DrawText(new FormattedText("Błąd rysowania: " + RenderError, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Mono, 13, Red, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(10, 10));
    }
    public string RenderError;

    /// <summary>
    /// Plan schematyczny węzła wg Ig-10 [D-27] (znaki umowne – załącznik nr 8): cały obszar na jednym arkuszu, osie torów
    /// (główne grubsze, zelektryfikowane niebieskie), rozjazdy z klinem, sygnalizatory świetlne z komorami, kozły, wykolejnice,
    /// liczniki osi / złącza izolowane, perony, przejazdy. Do edycji topologii – nie trafia na pulpit.
    /// </summary>
    void DrawPlan(DrawingContext dc, Station L, double dpi)
    {
        var g = new Ig10(dc, 2.5 * L.SymbolScale, dpi, 0.9 / Math.Max(0.004, scale)); var ly = PlanLayers.Current;
        var grey = B(0x70, 0x70, 0x70);
        if (ly.Platforms) foreach (var p in L.Platforms) g.Platform(p);
        foreach (var e in L.Edges.Values)
        {
            if (e.Link) continue;
            g.Track(e, e.Pts.Select(q => new Point(q.X, q.Y)).ToList());
        }
        // łuki: promień (i przechyłka) przy torze
        if (ly.Radius) foreach (var e in L.Edges.Values.Where(x => (x.Radius > 0 || x.Embank != 0 || !double.IsNaN(x.Grad)) && !x.Link))
        {
            var m = e.Pts[e.Pts.Count / 2]; var m0 = e.Pts.Count == 2 ? ((e.Pts[0].X + e.Pts[1].X) / 2, (e.Pts[0].Y + e.Pts[1].Y) / 2) : m;
            var txt = string.Join(" ", new[] { e.Radius > 0 ? $"R={e.Radius:0}" : null, e.Cant > 0 ? $"h={e.Cant:0}" : null, e.Embank > 0 ? $"nasyp {e.Embank:0.#} m" : e.Embank < 0 ? $"przekop {-e.Embank:0.#} m" : null, double.IsNaN(e.Grad) ? null : Math.Abs(e.Grad) < 0.05 ? "poziom" : $"i={e.Grad:0.#}‰" }.Where(x => x != null));
            Text(dc, txt, m0.Item1, m0.Item2 + 8, 6.5, grey, true, dpi, Mono);
        }
        // kontrola niezajętości: liczniki osi / złącza izolowane na granicach odcinków
        if (ly.Detection) foreach (var (p, v, det) in Ig10.SectionBoundaries(L)) { if (det == "ot") g.InsulatedJoint(p, v); else g.AxleCounter(p, v); }
        // numery torów w przerwie osi toru
        if (ly.Numbers) foreach (var (sec, sd) in L.Sections)
        {
            var e = sd.Edges.Select(x => L.Edges[x]).Where(x => !x.Link && x.Track != "").OrderByDescending(x => x.Len).FirstOrDefault();
            if (e == null) continue;
            g.TrackNumber(e.Track, new Point(e.Pts[0].X, e.Pts[0].Y), new Point(e.Pts[^1].X, e.Pts[^1].Y), e.Elec ? Ig10.Elec : null);
        }
        foreach (var sw in L.Switches.Values) g.Switch(L, sw, null, ly.Numbers);
        if (ly.Signals) foreach (var gn in L.Gantries.Values) g.Gantry(gn);
        if (ly.Signals) foreach (var s in L.Signals.Values) g.Signal(L, s);
        if (ly.Ends) foreach (var d in L.Derailers.Values)
        {
            var n = L.Nodes[d.Node]; var e = n.Edges.Select(x => L.Edges[x]).FirstOrDefault(x => !x.Link);
            var v = e == null ? new Vector(1, 0) : new Vector(e.Pts[^1].X - e.Pts[0].X, e.Pts[^1].Y - e.Pts[0].Y);
            g.Derailer(new Point(n.X, n.Y), v, d.Label);
        }
        // kozły oporowe i końce torów: „]” zwrócony w stronę toru
        if (ly.Ends) foreach (var nid in L.TrackEnds.Keys.Concat(L.Bumpers).Distinct())
        {
            if (!L.Nodes.TryGetValue(nid, out var n)) continue;
            var e = n.Edges.Select(x => L.Edges[x]).FirstOrDefault(x => !x.Link); if (e == null) continue;
            var pts = e.Pts.Select(q => new Point(q.X, q.Y)).ToList(); if (e.A != nid) pts.Reverse();
            g.BufferStop(pts[0], pts[1] - pts[0]);
            if (ly.Texts && L.TrackEnds.TryGetValue(nid, out var te) && !string.IsNullOrEmpty(te.Disp)) g.Text(te.Disp, n.X, n.Y + 10, 1.8, grey);
        }
        if (ly.Texts) foreach (var b in L.Blocks.Values)
            foreach (var en in b.Ends) { var n = L.Nodes[en.Boundary]; g.Text((en.Label ?? b.Id) + (b.Internal ? "" : " → " + b.Nbr), n.X + en.VOut * 40, n.Y - 12, 1.8, grey); }
        if (ly.Texts) foreach (var l in L.Labels) g.Text(l.Text, l.X, l.Y, 1.8, grey, l.Anchor == "middle" ? 0 : l.Anchor == "end" ? 1 : -1);
        if (ly.Crossings) foreach (var c in L.Crossings) g.Crossing(c);
        // nazwy posterunków (Ig-10 zał. 1: pełne nazwy)
        if (ly.Texts) foreach (var (pid, pname) in L.Posts)
        {
            var ns = L.Nodes.Values.Where(n => n.Post == pid && n.Edges.Count > 1).ToList(); if (ns.Count == 0) continue;
            g.Text(pname, (ns.Min(n => n.X) + ns.Max(n => n.X)) / 2, ns.Min(n => n.Y) - 40, 4);
        }
    }

    /// <summary>Symbol kontrolny (Ie-104 § 4 ust. 16): wzorzec barw, biały na szarym, symbol migania.</summary>
    void DrawControlSymbol(DrawingContext dc, double x, double y)
    {
        dc.DrawRectangle(B(0xff, 0, 0), null, new Rect(x, y, 12, 12)); dc.DrawRectangle(B(0, 0xff, 0), null, new Rect(x + 14, y, 12, 12));
        dc.DrawRectangle(B(0, 0, 0xff), null, new Rect(x + 28, y, 12, 12));
        dc.DrawRectangle(Grey, null, new Rect(x + 44, y, 14, 12)); dc.DrawRectangle(White, null, new Rect(x + 48, y + 3, 6, 6));
        dc.DrawEllipse(BlinkOn ? White : Dim, null, new Point(x + 68, y + 6), 4, 4);
    }

    /// <summary>
    /// Blokada liniowa – wzór EbiScreen / Ie-104 § 8 pkt 17: strzałka kierunku w osi toru szlakowego tuż za semaforem wjazdowym
    /// (żółta – kierunek ustawiony, grot wskazuje kierunek jazdy; szara dwustronna – stan neutralny; migający grot – żądanie pozwolenia;
    /// migający trzon – zwalnianie kierunku; czerwona – AZK; różowa – wyjazd zamknięty), oznaczenie szlaku nad strzałką,
    /// lampki TOR / STOP (małe, tylko w stanie czynnym).
    /// </summary>
    void DrawBlock(DrawingContext dc, BlockDef b, int ei, double dpi)
    {
        var S = Ind; var bs = S.Blocks[b.Id]; var en = b.Ends[ei];
        var bn = Lay.Nodes[en.Boundary]; var u = BlockAxis(b, ei);
        int outward = en.Outward;
        Brush sa = Grey, sb = Grey; int arrowDir;
        bool closedHere = bs.StopEnd[ei], closedThere = b.Internal ? bs.StopEnd[1 - ei] : bs.NbrStop;
        if (bs.AzkUntil != null) { arrowDir = bs.AzkDir; sa = sb = BlinkOn ? Red : Dim; }
        else if (bs.Req != null) { arrowDir = bs.Req.EndIdx == ei ? outward : -outward; sa = BlinkOn ? Yellow : Dim; sb = Yellow; }
        else if (bs.Dir == 0) arrowDir = 0;
        else
        {
            arrowDir = bs.Dir; sa = sb = Yellow;
            if (bs.ReleaseUntil != null) sb = BlinkOn ? Yellow : Dim;
            if (closedHere || closedThere) { sa = sb = Magenta; }
        }
        if (bs.NoData) { arrowDir = 0; sa = sb = White; }
        else if (b.NoBlock) { arrowDir = 0; sa = sb = Dim; }
        else if (b.OneWay && bs.AzkUntil == null) { arrowDir = b.DefDir; if (sa == Grey) sa = sb = White; }
        if (bs.AwaitEnd != null) { sb = Red; if (!b.SecsWE.Any(S.SecOcc) && !BlinkOn) sb = Dim; if (sa == Grey || sa == White) sa = Red; }
        Point P(double d) => new(bn.X + u.X * d, bn.Y + u.Y * d);
        double d0 = 14, d1 = 46;                                  // od granicy na zewnątrz
        var bg = new Pen(Bg, 9) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
        dc.DrawLine(bg, P(d0 - 2), P(d1 + 2));                     // wycięcie toru pod strzałką
        void Head(double at, double toward, Brush c)
        {
            var tip = P(at); var back = P(at - toward * 10); var nrm = new Vector(-u.Y, u.X) * 6.5;
            var g = new StreamGeometry();
            using (var ctx = g.Open()) { ctx.BeginFigure(tip, true, true); ctx.LineTo(back + nrm, true, false); ctx.LineTo(back - nrm, true, false); }
            g.Freeze(); dc.DrawGeometry(c, null, g);
        }
        var shaft = new Pen(sb, 6) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
        if (arrowDir == 0)
        {
            var nc = bs.NoData ? White : Grey;
            dc.DrawLine(new Pen(nc, 6), P(d0 + 9), P(d1 - 9));
            Head(d1, 1, nc); Head(d0, -1, nc);
        }
        else if (arrowDir == outward) { dc.DrawLine(shaft, P(d0), P(d1 - 9)); Head(d1, 1, sa); }
        else { dc.DrawLine(shaft, P(d0 + 9), P(d1)); Head(d0, -1, sa); }
        // oznaczenie szlaku (jedno, gdy oba końce szlaku wewnętrznego są tuż obok siebie)
        var lp = P((d0 + d1) / 2);
        var name = string.IsNullOrEmpty(en.Label) ? b.Id : en.Label;
        bool dup = ei == 1 && b.Ends.Count == 2 && (b.Ends[0].Label ?? b.Id) == name && (new Point(Lay.Nodes[b.Ends[0].Boundary].X, Lay.Nodes[b.Ends[0].Boundary].Y) - new Point(bn.X, bn.Y)).Length < 110;
        if (!dup) Text(dc, name, lp.X, lp.Y - 14, 10, SigName, true, dpi, Mono);
        // lampki TOR / STOP – małe, obok oznaczenia
        double lx = lp.X + (en.VOut > 0 ? 18 : -42);
        if (FreeBar(b)) dc.DrawRectangle(Green, null, new Rect(lp.X - 6, lp.Y - 25, 12, 3));
        // oznaczenie szlaku (np. it1Z, 1ŁF) – nad torem szlakowym, za strzałką (szlak zewnętrzny, jak w SimRail)
        if (!b.Internal && b.SecsWE.Count == 1 && !string.IsNullOrEmpty(b.Label) && b.Label != name) { var ip = P(d1 + 26); Text(dc, b.Label, ip.X, ip.Y - 13, 8.5, SwLabel, true, dpi, Mono); }
        if (closedHere || (closedThere && BlinkOn)) { dc.DrawRectangle(Red, null, new Rect(lx, lp.Y + 8, 24, 9)); Text(dc, "STOP", lx + 12, lp.Y + 12.5, 6.5, White, true, dpi, MonoB); }
        // półsamoczynna: Pwl (czerwona – przeciwwtórność liniowa), Ko (biała – wjazd stwierdzony, można dać blok końcowy); SBL: kTor (czerwona)
        double ly2 = lp.Y + 19;
        if (b.SemiAuto)
        {
            if (bs.Pwl.Length > ei && bs.Pwl[ei]) { dc.DrawRectangle(Red, null, new Rect(lx, ly2, 20, 9)); Text(dc, "Pwl", lx + 10, ly2 + 4.5, 6.5, White, true, dpi, MonoB); }
            if (bs.KoReady.Length > ei && bs.KoReady[ei]) { dc.DrawRectangle(White, null, new Rect(lx + 22, ly2, 16, 9)); Text(dc, "Ko", lx + 30, ly2 + 4.5, 6.5, Bg, true, dpi, MonoB); }
        }
        else if (bs.KTor) { dc.DrawRectangle(Red, null, new Rect(lx, ly2, 24, 9)); Text(dc, "kTor", lx + 12, ly2 + 4.5, 6.5, White, true, dpi, MonoB); }
        var rc = BlockRect(b, ei);
        if (S.Pending?.Targets.Contains(b.Id) == true) dc.DrawRectangle(null, new Pen(Orange, 2), rc);

    }

    static void DrawArrowHead(DrawingContext dc, double tipX, double cy, int d, Brush c)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open()) { ctx.BeginFigure(new Point(tipX, cy), true, true); ctx.LineTo(new Point(tipX - d * 10, cy - 7.5), true, false); ctx.LineTo(new Point(tipX - d * 10, cy + 7.5), true, false); }
        dc.DrawGeometry(c, null, g);
    }

    /// <summary>Oznaczenie końca linii przejazdu: „Y” – rozwidlenie, „T” – poprzeczka.</summary>
    void DrawCrossMark(DrawingContext dc, double x, double y, int side, string kind = "Y")
    {
        var p = new Pen(CrossPink, 2.6);
        dc.DrawLine(p, new Point(x, y), new Point(x, y + side * 14));
        if (kind == "T") { dc.DrawLine(p, new Point(x - 6, y), new Point(x + 6, y)); return; }
        dc.DrawLine(p, new Point(x, y + side * 10), new Point(x - 6, y + side * 16));
        dc.DrawLine(p, new Point(x, y + side * 10), new Point(x + 6, y + side * 16));
    }

    double EndGap(Edge e, string node)
    {
        var n = Lay.Nodes[node];
        if (n.Signals.Any(x => Lay.Signals[x].Type != "odstepowy")) return 9;
        if (n.Signals.Count > 0) return 3;
        if (n.Switch != null || n.Edges.Count < 2) return 0;
        if (EditView) return 0;
        // przerwa izolacyjna tylko na prostym przejściu toru; na załamaniu łącznika (ukos → pion) tor rysowany jest w sposób ciągły
        var other = n.Edges.Where(x => x != e.Id && Lay.Edges[x].Sec != e.Sec).ToList();
        if (other.Count == 0 || EditView) return 0;
        var t1 = Tangent(e, node);
        return other.Any(x => { var t2 = Tangent(Lay.Edges[x], node); return t1.X * t2.X + t1.Y * t2.Y < -0.96; }) ? 1.8 : 0;
    }

    /// <summary>Kierunek toru wychodzącego z węzła (wektor jednostkowy od węzła).</summary>
    static Vector Tangent(Edge e, string node)
    {
        var p = e.A == node ? (e.Pts[0], e.Pts[1]) : (e.Pts[^1], e.Pts[^2]);
        var v = new Vector(p.Item2.X - p.Item1.X, p.Item2.Y - p.Item1.Y);
        return v.Length > 0 ? v / v.Length : v;
    }

    static void Shorten(List<Point> pts, double a, double b)
    {
        if (pts.Count < 2) return;
        if (a > 0) { var v = pts[1] - pts[0]; if (v.Length > a + 1) pts[0] = pts[0] + v / v.Length * a; }
        if (b > 0) { var v = pts[^2] - pts[^1]; if (v.Length > b + 1) pts[^1] = pts[^1] + v / v.Length * b; }
    }
    static void DrawPoly(DrawingContext dc, Pen pen, List<Point> pts)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open()) { ctx.BeginFigure(pts[0], false, false); ctx.PolyLineTo(pts.Skip(1).ToList(), true, true); }
        g.Freeze();
        dc.DrawGeometry(null, pen, g);
    }

    Brush SecBrush(string sec)
    {
        var s = Ind.Secs[sec];
        if (s.NoData) return White;
        if (s.Fault == "reset") return BlinkOn ? Red : Grey;
        if (s.Fault == "await") return DarkRed;
        if (s.TrainOcc || s.Fault == "occ") return Red;
        if (s.TimeRel != null) return Magenta;
        if (s.LockType == "train") return Green;
        if (s.LockType is "shunt" or "overlap") return Yellow;
        return Grey;
    }

    /// <summary>
    /// Sygnalizatory w osi toru, ostrzem w kierunku, dla którego podają sygnały (Ie-104 § 8 pkt 3–5).
    /// Opis z prawej strony patrząc w kierunku wskazanym (w prawo – pod torem, w lewo – nad torem); pociągowe żółte, tarcze turkusowe (sam numer).
    /// </summary>
    void DrawSignal(DrawingContext dc, SignalDef s, double dpi)
    {
        var S = Ind; var st = S.Sigs[s.Name]; var n = Lay.Nodes[s.Node];
        double d = s.VDir, cx = n.X, cy = n.Y, h = 6.2;     // zwrot na rysunku pulpitu (kierunek obowiązywania – s.Dir)
        if (s.Type == "odstepowy")
        {
            // szlak jak na pulpicie: odstępy rozdzielone przerwami, nad torem numer sygnalizatora odstępowego dla kierunku
            // ustawionego (w stanie neutralnym – zasadniczego); bez symboli sygnalizatorów
            var bs = S.Blocks.GetValueOrDefault(s.Szlak); var bd = Lay.Blocks.GetValueOrDefault(s.Szlak);
            int show = bs != null && bs.Dir != 0 ? bs.Dir : bd != null && bd.DefDir != 0 ? bd.DefDir : 1;
            if (s.Dir != show || string.IsNullOrEmpty(s.Label)) return;
            Text(dc, s.Label, cx + d * 14, cy - 11, 8, SwLabel, true, dpi, Mono);
            return;
        }
        if (s.Type == "pk")
        {
            // PK – punkt końcowy przebiegu: mały trójkąt w osi toru; czerwony, gdy kończy się w nim nastawiony przebieg
            bool endHere = S.Routes.Values.Any(r => r.End == s.Name && r.State != "setting");
            var pc = endHere ? Red : SwLabel;
            dc.DrawGeometry(null, new Pen(pc, 1.6), Tri(cx + d * 5, cy, d, 4.5, false));
            Text(dc, string.IsNullOrEmpty(s.Label) ? "PK" : s.Label.StartsWith("PK") ? s.Label : "PK " + s.Label, cx, cy + (d > 0 ? 13 : -12), 8, pc, true, dpi, Mono);
            return;
        }
        if (s.Type is "to" or "sp")
        {
            // powtarzacz: semafora (także SBL) albo informacji z sąsiedniej stacji – szlak: zielony, gdy jedzie pociąg w kierunku powtarzacza
            var main = s.Repeats != null && S.Sigs.TryGetValue(s.Repeats, out var m) ? m : null;
            Brush rc = main == null ? White : main.Aspect == Aspect.Train ? Green : Grey;
            if (main == null && s.Repeats != null && S.Blocks.TryGetValue(s.Repeats, out var rb))
                rc = rb.Dir == s.Dir && rb.TrainOn ? Green : Grey;
            if (s.Type == "sp") dc.DrawGeometry(null, new Pen(rc, 1.6), Tri(cx + d * 6, cy, d, h * 0.8, false));
            else { dc.DrawGeometry(null, new Pen(rc, 1.8), Chevron(cx + d * 6, cy, d, h * 0.8, 9)); dc.DrawEllipse(rc, null, new Point(cx - d * 1, cy), 1.8, 1.8); }
            Text(dc, s.Label, cx, cy + (d > 0 ? 15 : -14), 8, SigName, true, dpi, Mono);
            return;
        }
        Brush c;
        if (st.NoData) c = White;
        else if (st.Aspect == Aspect.Sz) c = BlinkOn ? White : Dim;
        else if (st.Aspect == Aspect.Train) c = Green;
        else if (st.Aspect == Aspect.Shunt) c = Yellow;
        else if (st.LampFault && st.FailAlarm) c = BlinkOn ? Red : Dim;
        else if (st.Route != null || st.RouteEnd != null) c = Red;
        else if (st.Closed) c = Magenta;
        else c = Grey;
        if (c == Grey && Targets.Contains(s.Name) && !Targets.Contains("blk:" + s.Node)) c = TargetSym;      // symbol na turkusowym polu – ciemny
        double tipX = cx + d * 8;
        double x0 = Math.Min(tipX, tipX - d * 22);
        if (S.Pending?.Targets.Contains(s.Name) == true) dc.DrawRectangle(Orange, null, new Rect(x0 - 3, cy - h - 4, 28, 2 * h + 8));
        else if (Targets.Contains(s.Name) && !Targets.Contains("blk:" + s.Node)) TargetMark(dc, new Rect(x0 - 3, cy - h - 4, 28, 2 * h + 8));
        if (s.Type == "tm")
            dc.DrawGeometry(null, new Pen(c, 2.4) { LineJoin = PenLineJoin.Miter }, Chevron(tipX, cy, d, h, 10));
        else if (s.Type == "wjazdowy")
        {
            dc.DrawGeometry(c, null, Tri(tipX, cy, d, h + 1, true));
            // funkcja końca przebiegu wyjazdowego (Ie-104 § 8 pkt 3 lit. f) – mały trójkąt skierowany przeciwnie
            bool endR = S.Routes.Values.Any(r => r.EndNode == s.Node && r.State != "setting" && r.EndKind is "szlak" or "szlakM" or "gr");
            // po stronie stacji, za semaforem, skierowany w stronę szlaku (jak na pulpicie)
            dc.DrawRectangle(Bg, null, new Rect(cx + d * 15 - 6, cy - 4, 12, 8));      // przerwa w torze pod trójkątem
            dc.DrawGeometry(endR ? Red : Grey, null, Tri(cx + d * 18, cy, -d, 5, true));
            if (Targets.Contains("blk:" + s.Node)) dc.DrawRoundedRectangle(null, new Pen(SetFrame, 2), new Rect(cx + d * 15 - 7, cy - 9, 14, 18), 3, 3);
        }
        else
        {
            // semafor z funkcją tarczy manewrowej (sygnały Ms2): pełny trójkąt na czele i otwarty grot za nim (Ie-104, symbol 52.6)
            dc.DrawGeometry(c, null, Tri(tipX, cy, d, h + 1, true));
            if (!s.NoShunt) dc.DrawGeometry(null, new Pen(c, 2.4) { LineJoin = PenLineJoin.Miter }, Chevron(tipX - d * 11.5, cy, d, h + 1, 9));
        }
        if (SelStart == s.Name) dc.DrawRoundedRectangle(null, new Pen(SetFrame, 2), new Rect(x0 - 4, cy - h - 5, 30, 2 * h + 10), 3, 3);
        else if (SelVia.Contains(s.Name)) dc.DrawRectangle(null, new Pen(Azure, 1.6), new Rect(x0 - 3, cy - h - 4, 28, 2 * h + 8));
        // przebieg od tego sygnalizatora w trakcie nastawiania – turkusowa ramka (jak na pulpicie), droga jeszcze nie wyświetlona
        if (st.Setting) dc.DrawRoundedRectangle(null, new Pen(SetFrame, 1.8), new Rect(x0 - 4, cy - h - 5, 30, 2 * h + 10), 3, 3);
        Text(dc, s.Label, cx, cy + (d > 0 ? 17 : -16), 10, s.Type == "tm" ? TmName : SigName, true, dpi, Mono);
    }

    static Geometry Tri(double tipX, double cy, double d, double h, bool filled)
    {
        var g = new StreamGeometry();
        using (var c = g.Open()) { c.BeginFigure(new Point(tipX, cy), filled, true); c.LineTo(new Point(tipX - d * 1.5 * h, cy - h), true, false); c.LineTo(new Point(tipX - d * 1.5 * h, cy + h), true, false); }
        g.Freeze(); return g;
    }
    static Geometry Chevron(double tipX, double cy, double d, double h, double len)
    {
        var g = new StreamGeometry();
        using (var c = g.Open()) { c.BeginFigure(new Point(tipX - d * len, cy - h), false, false); c.LineTo(new Point(tipX, cy), true, false); c.LineTo(new Point(tipX - d * len, cy + h), true, false); }
        g.Freeze(); return g;
    }

    double fontK = 1;
    void Text(DrawingContext dc, string s, double x, double y, double size, Brush b, bool center, double dpi, Typeface tf, bool alignRight = false)
    {
        if (string.IsNullOrEmpty(s)) return;
        if (Overview && size < 14) return;                         // obraz poglądowy – bez drobnych opisów
        var ft = new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, Math.Max(1, size * fontK), b, dpi);
        double dx = center ? -ft.Width / 2 : alignRight ? -ft.Width : 0;
        dc.DrawText(ft, new Point(x + dx, y - ft.Height / 2));
    }
}

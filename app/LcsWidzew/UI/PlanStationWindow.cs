using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LcsWidzew.Model;
using LcsWidzew.Sim;

namespace LcsWidzew.UI;

/// <summary>
/// „Plan stacji” na żywo: plan schematyczny węzła (układ torowy) ze stanem terenu – pociągi z numerem i rodzajem,
/// semafory jako głowice z rzeczywistymi światłami, zwrotnice w aktualnym położeniu. Śledzenie składu i wybór posterunku.
/// Podgląd dla instruktora i do nauki – stanowisko dyżurnego widzi tylko pulpit.
/// </summary>
public class PlanStationWindow : Window
{
    readonly Func<Simulator> getSim;
    readonly PlanLiveView view;
    readonly ComboBox follow = new() { Width = 200, Margin = new Thickness(4, 2, 12, 2) };
    readonly ComboBox posts = new() { Width = 220, Margin = new Thickness(4, 2, 4, 2) };
    readonly DispatcherTimer tm = new() { Interval = TimeSpan.FromMilliseconds(100) };
    string lastTrains = "";

    public PlanStationWindow(Func<Simulator> sim)
    {
        getSim = sim;
        Title = "Plan stacji – podgląd terenu"; Width = 1400; Height = 800; Background = Brushes.White;
        view = new PlanLiveView(sim);
        var bottom = new DockPanel { Background = new SolidColorBrush(Color.FromRgb(0xf0, 0xf0, 0xf0)) };
        var right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var chk = new CheckBox { Content = "Śledzenie składu:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        right.Children.Add(chk); right.Children.Add(follow);
        right.Children.Add(new TextBlock { Text = "Posterunek:", VerticalAlignment = VerticalAlignment.Center });
        right.Children.Add(posts);
        var fit = new Button { Content = "Cały węzeł", Margin = new Thickness(4, 2, 8, 2), Padding = new Thickness(8, 1, 8, 1) };
        fit.Click += (a, e) => { chk.IsChecked = false; view.FitAll(); };
        right.Children.Add(fit);
        // warstwy planu (Ig-10 § 4 ust. 4) – te same co w edytorze
        var layers = new Button { Content = "Warstwy ▾", Margin = new Thickness(4, 2, 8, 2), Padding = new Thickness(8, 1, 8, 1) };
        var menu = new ContextMenu();
        foreach (var (name, get, set) in PlanLayers.All)
        {
            var mi = new MenuItem { Header = name, IsCheckable = true, IsChecked = get(PlanLayers.Current), StaysOpenOnClick = true };
            mi.Click += (a, e) => { set(PlanLayers.Current, mi.IsChecked); view.InvalidateVisual(); };
            menu.Items.Add(mi);
        }
        layers.Click += (a, e) => { menu.PlacementTarget = layers; menu.IsOpen = true; };
        right.Children.Add(layers);
        DockPanel.SetDock(right, Dock.Right); bottom.Children.Add(right);
        bottom.Children.Add(new TextBlock { Text = "Kółko – powiększenie, przeciąganie – przesuwanie. Podgląd terenu (nie jest pulpitem dyżurnego).", Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });
        var dock = new DockPanel(); DockPanel.SetDock(bottom, Dock.Bottom); dock.Children.Add(bottom); dock.Children.Add(view);
        Content = dock;
        var s = sim();
        posts.ItemsSource = s.L.Posts.Select(p => $"{p.Value} ({p.Key})").ToList();
        posts.SelectionChanged += (a, e) => { if (posts.SelectedIndex >= 0) { chk.IsChecked = false; view.FitPost(getSim().L.Posts.Keys.ElementAt(posts.SelectedIndex)); } };
        follow.SelectionChanged += (a, e) => { if (follow.SelectedItem != null) chk.IsChecked = true; };
        tm.Tick += (a, e) =>
        {
            var sm = getSim(); if (sm == null) return;
            view.Follow = chk.IsChecked == true ? follow.SelectedItem as string : null;
            var nums = sm.Trains?.List.Where(t => !t.Gone).Select(t => t.Num).OrderBy(x => x).ToList() ?? new();
            var key = string.Join(",", nums);
            if (key != lastTrains) { var cur = follow.SelectedItem as string; follow.ItemsSource = nums; follow.SelectedItem = cur; lastTrains = key; }
            view.InvalidateVisual();
        };
        tm.Start(); Closed += (a, e) => tm.Stop();
        Loaded += (a, e) => view.FitAll();
    }
}

public class PlanLiveView : FrameworkElement
{
    readonly Func<Simulator> getSim;
    double scale = 1, offX, offY;
    Point? drag; Point dragOff;
    public string Follow;
    static readonly Typeface Font = new("Segoe UI");

    public PlanLiveView(Func<Simulator> sim)
    {
        getSim = sim; ClipToBounds = true;
        MouseWheel += (s, e) =>
        {
            var p = e.GetPosition(this); var w = new Point((p.X - offX) / scale, (p.Y - offY) / scale);
            scale = Math.Clamp(scale * (e.Delta > 0 ? 1.2 : 1 / 1.2), 0.05, 20);
            offX = p.X - w.X * scale; offY = p.Y - w.Y * scale; InvalidateVisual();
        };
        MouseLeftButtonDown += (s, e) => { drag = e.GetPosition(this); dragOff = new Point(offX, offY); CaptureMouse(); };
        MouseLeftButtonUp += (s, e) => { drag = null; ReleaseMouseCapture(); };
        MouseMove += (s, e) => { if (drag is Point d && e.LeftButton == MouseButtonState.Pressed) { var p = e.GetPosition(this); offX = dragOff.X + p.X - d.X; offY = dragOff.Y + p.Y - d.Y; Follow = null; InvalidateVisual(); } };
    }

    void Fit(IEnumerable<Node> ns)
    {
        var l = ns.ToList(); if (l.Count == 0 || ActualWidth < 10) return;
        double x0 = l.Min(n => n.X) - 40, x1 = l.Max(n => n.X) + 40, y0 = l.Min(n => n.Y) - 60, y1 = l.Max(n => n.Y) + 60;
        scale = Math.Min(ActualWidth / (x1 - x0), ActualHeight / (y1 - y0));
        offX = ActualWidth / 2 - (x0 + x1) / 2 * scale; offY = ActualHeight / 2 - (y0 + y1) / 2 * scale;
        InvalidateVisual();
    }
    public void FitAll() { var s = getSim(); if (s != null) Fit(s.L.Nodes.Values); }
    public void FitPost(string post) { var s = getSim(); if (s != null) Fit(s.L.Nodes.Values.Where(n => n.Post == post)); }

    static Brush B(byte r, byte g, byte b) { var x = new SolidColorBrush(Color.FromRgb(r, g, b)); x.Freeze(); return x; }
    static readonly Brush LRed = B(0xe0, 0x10, 0x10), LGreen = B(0x10, 0xc0, 0x30), LOrange = B(0xff, 0x9a, 0x00), LWhite = B(0xff, 0xff, 0xff),
        LBlue = B(0x20, 0x60, 0xff), LOff = B(0x55, 0x55, 0x55), Head = B(0x20, 0x20, 0x20), TrainFill = B(0xc8, 0xc8, 0xc8), Grey = B(0x9a, 0x9a, 0x9a);

    void Text(DrawingContext dc, string s, double x, double y, double size, Brush b, bool center = true, bool right = false)
    {
        if (string.IsNullOrEmpty(s)) return;
        var ft = new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Font, size, b, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(ft, new Point(right ? x - ft.Width : center ? x - ft.Width / 2 : x, y - ft.Height / 2));
    }

    /// <summary>Punkt na torze w odległości „d” metrów od węzła „from” (długość w metrach odwzorowana na łamaną planu).</summary>
    static (Point P, Vector U) AlongEdge(Station L, string eid, string from, double d)
    {
        var e = L.Edges[eid]; var pts = e.Pts.Select(q => new Point(q.X, q.Y)).ToList(); if (e.A != from) pts.Reverse();
        double geo = 0; for (int i = 0; i < pts.Count - 1; i++) geo += (pts[i + 1] - pts[i]).Length;
        double want = Math.Clamp(d / Math.Max(1, e.Len), 0, 1) * geo, acc = 0;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            var v = pts[i + 1] - pts[i]; var l = v.Length;
            if (acc + l >= want || i == pts.Count - 2) { var u = l > 0 ? v / l : new Vector(1, 0); return (pts[i] + u * Math.Clamp(want - acc, 0, l), u); }
            acc += l;
        }
        return (pts[0], new Vector(1, 0));
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, ActualWidth, ActualHeight));
        var S = getSim(); if (S == null) return;
        var L = S.L;
        // śledzenie składu – widok za czołem pociągu
        var ft = Follow != null ? S.Trains?.List.FirstOrDefault(t => t.Num == Follow && !t.Gone) : null;
        if (ft != null && HeadPoint(S, ft) is Point hp) { offX = ActualWidth / 2 - hp.X * scale; offY = ActualHeight / 2 - hp.Y * scale; }
        dc.PushTransform(new MatrixTransform(scale, 0, 0, scale, offX, offY));
        double px = 1 / Math.Max(0.05, scale);   // 1 piksel ekranu w jednostkach planu
        var ink = Brushes.Black;
        // znaki umowne Ig-10 (zał. 8); u – mm arkusza w jednostkach planu: przy powiększeniu stała wielkość na ekranie
        var g = new Ig10(dc, Math.Min(2.5 * L.SymbolScale, 3 * px), VisualTreeHelper.GetDpi(this).PixelsPerDip); var ly = PlanLayers.Current;
        if (ly.Platforms) foreach (var p in L.Platforms) g.Platform(p);
        // tory: nieczynne odgałęzienie zwrotnicy – szare, z przerwą przy zwrotnicy
        var dead = new HashSet<string>();
        foreach (var (sw, def) in L.Switches) { var w = S.Sws[sw]; if (w.Moving == null) dead.Add(w.Pos == "+" ? def.Minus : def.Plus); else { dead.Add(def.Plus); dead.Add(def.Minus); } }
        foreach (var e in L.Edges.Values)
        {
            if (e.Link) continue;
            var pts = e.Pts.Select(q => new Point(q.X, q.Y)).ToList();
            bool d = dead.Contains(e.Id);
            if (d)
            {
                // przerwa przy iglicy – widać, że odgałęzienie nie prowadzi
                var sw = L.Switches.Values.FirstOrDefault(x => x.Plus == e.Id || x.Minus == e.Id);
                if (sw != null) { if (e.A == sw.Node) pts[0] = pts[0] + (pts[1] - pts[0]) * Math.Min(0.35, 6 * px / Math.Max(0.1, (pts[1] - pts[0]).Length)); else pts[^1] = pts[^1] + (pts[^2] - pts[^1]) * Math.Min(0.35, 6 * px / Math.Max(0.1, (pts[^2] - pts[^1]).Length)); }
            }
            g.Track(e, pts, d ? Grey : null, d ? 0.25 : null);
        }
        if (ly.Detection) foreach (var (p, v, det) in Ig10.SectionBoundaries(L)) { if (det == "ot") g.InsulatedJoint(p, v); else g.AxleCounter(p, v); }
        if (ly.Numbers) foreach (var (sec, sd) in L.Sections)
        {
            var e = sd.Edges.Select(x => L.Edges[x]).Where(x => !x.Link && x.Track != "").OrderByDescending(x => x.Len).FirstOrDefault();
            if (e != null) g.TrackNumber(e.Track, new Point(e.Pts[0].X, e.Pts[0].Y), new Point(e.Pts[^1].X, e.Pts[^1].Y), e.Elec ? Ig10.Elec : null);
        }
        // rozjazdy: znak Ig-10; w trakcie przekładania – pomarańczowy, przy numerze „…”
        foreach (var sw in L.Switches.Values) g.Switch(L, sw, S.Sws[sw.Num].Moving != null ? LOrange : null, ly.Numbers);
        if (ly.Ends) foreach (var d in L.Derailers.Values)
        {
            var n = L.Nodes[d.Node]; var e = n.Edges.Select(x => L.Edges[x]).FirstOrDefault(x => !x.Link);
            g.Derailer(new Point(n.X, n.Y), e == null ? new Vector(1, 0) : new Vector(e.Pts[^1].X - e.Pts[0].X, e.Pts[^1].Y - e.Pts[0].Y), d.Label);
        }
        if (ly.Ends) foreach (var nid in L.Bumpers.Concat(L.TrackEnds.Keys).Distinct())
        {
            if (!L.Nodes.TryGetValue(nid, out var n)) continue;
            var e = n.Edges.Select(x => L.Edges[x]).FirstOrDefault(x => !x.Link); if (e == null) continue;
            var pts = e.Pts.Select(q => new Point(q.X, q.Y)).ToList(); if (e.A != nid) pts.Reverse();
            g.BufferStop(pts[0], pts[1] - pts[0]);
        }
        if (ly.Crossings) foreach (var c in L.Crossings) g.Crossing(c);
        // pociągi
        if (S.Trains != null) foreach (var t in S.Trains.List.Where(t => !t.Gone)) DrawTrain(dc, S, t, px, t.Num == Follow);
        // sygnalizatory
        bool blink = (Environment.TickCount64 / 500) % 2 == 0;
        if (ly.Signals) foreach (var gn in L.Gantries.Values) g.Gantry(gn);
        if (ly.Signals) foreach (var s in L.Signals.Values) DrawSignal(g, L, s, S.Sigs[s.Name], S, blink);
        // nazwy posterunków
        foreach (var (pid, pname) in L.Posts)
        {
            var ns = L.Nodes.Values.Where(n => n.Post == pid && n.Edges.Count > 1).ToList(); if (ns.Count == 0) continue;
            Text(dc, pname, (ns.Min(n => n.X) + ns.Max(n => n.X)) / 2, ns.Min(n => n.Y) - 30 * px, Math.Max(10, 18 * px), ink);
        }
        dc.Pop();
        Text(dc, S.Clock(), 60, 18, 16, ink);
    }

    /// <summary>Odcinek toru zajęty przez pociąg: od ogona do czoła, wzdłuż jego drogi.</summary>
    List<List<Point>> TrainPolys(Simulator S, Train t)
    {
        var res = new List<List<Point>>();
        double tail = t.Head - t.Len, pos = 0;
        foreach (var sg in t.Segs)
        {
            double a = pos, b = pos + sg.Len; pos = b;
            if (sg.E == null || !S.L.Edges.ContainsKey(sg.E)) continue;
            double lo = Math.Max(a, tail), hi = Math.Min(b, t.Head); if (hi <= lo) continue;
            var pl = new List<Point>();
            int steps = 6;
            for (int k = 0; k <= steps; k++) pl.Add(AlongEdge(S.L, sg.E, sg.From, lo - a + (hi - lo) * k / steps).P);
            res.Add(pl);
        }
        return res;
    }

    static Point? HeadPoint(Simulator S, Train t)
    {
        double pos = 0;
        foreach (var sg in t.Segs) { if (sg.E != null && S.L.Edges.ContainsKey(sg.E) && t.Head >= pos && t.Head <= pos + sg.Len) return AlongEdge(S.L, sg.E, sg.From, t.Head - pos).P; pos += sg.Len; }
        return null;
    }

    void DrawTrain(DrawingContext dc, Simulator S, Train t, double px, bool followed)
    {
        var polys = TrainPolys(S, t); if (polys.Count == 0) return;
        var outline = new Pen(followed ? LBlue : Brushes.Black, (followed ? 2 : 1) * px);
        foreach (var pl in polys)
        {
            var g = new StreamGeometry(); using (var c = g.Open()) { c.BeginFigure(pl[0], false, false); c.PolyLineTo(pl.Skip(1).ToList(), true, true); }
            dc.DrawGeometry(null, new Pen(outline.Brush, 9 * px) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat }, g);
            dc.DrawGeometry(null, new Pen(TrainFill, 7 * px) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat }, g);
        }
        // czoło – czerwony znacznik, opis: numer i rodzaj
        if (HeadPoint(S, t) is Point hp) dc.DrawEllipse(LRed, null, hp, 2.2 * px, 2.2 * px);
        var all = polys.SelectMany(p => p).ToList(); var mid = all[all.Count / 2];
        var kind = t.Shunt ? (t.Loco ? "lok." : "manewr") : t.Duty?.Unit ?? t.Cat;
        Text(dc, t.Num, mid.X, mid.Y - 11 * px, Math.Max(7, 10 * px), Brushes.Black);
        Text(dc, $"{kind} {t.V * 3.6:0} km/h", mid.X, mid.Y + 11 * px, Math.Max(6, 8.5 * px), B(0x50, 0x50, 0x50));
    }

    /// <summary>Sygnalizator wg Ig-10 (słupek, podstawa, komory) z komorami zapalonymi wg rzeczywistego sygnału.</summary>
    void DrawSignal(Ig10 g, Station L, SignalDef s, SigState st, Simulator S, bool blink)
    {
        if (s.Type == "pk") return;
        var a = st.Aspect; bool dark = st.Dark || st.NoData;
        var cols = Ig10.LampColors(s, s.LampCount);
        bool Lit(char c, int i) => s.Type switch
        {
            "tm" => c == 'W' ? a == Aspect.Shunt : c == 'B' && a != Aspect.Shunt,
            "to" or "sp" => (s.Repeats != null && S.Sigs.TryGetValue(s.Repeats, out var m) ? m.Aspect : Aspect.Stop) is var rep && (c == 'G' ? rep == Aspect.Train : c == 'O' && rep != Aspect.Train),
            "odstepowy" => st.Sig switch { "S2" => c == 'G', "S4" => c == 'G' && blink, "S5" => c == 'O', _ => c == 'R' },
            _ => c switch { 'G' => a == Aspect.Train, 'R' => a is Aspect.Stop or Aspect.Sz, 'W' => (a == Aspect.Sz && blink) || a == Aspect.Shunt, _ => false }
        };
        Brush Col(char c) => c switch { 'G' => LGreen, 'O' => LOrange, 'R' => LRed, 'W' => LWhite, _ => LBlue };
        var lit = cols.Select((c, i) => !dark && Lit(c, i) ? Col(c) : LOff).ToArray();
        // tarcza z jedną komorą – ta komora świeci barwą bieżącego sygnału
        if (cols.Length == 1 && !dark && s.Type == "tm") lit[0] = a == Aspect.Shunt ? LWhite : LBlue;
        if (cols.Length == 1 && !dark && s.Type is "to" or "sp") lit[0] = s.Repeats != null && S.Sigs.TryGetValue(s.Repeats, out var rp) && rp.Aspect == Aspect.Train ? LGreen : LOrange;
        g.Signal(L, s, lit);
    }
}

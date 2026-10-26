using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Profil podłużny toru (etap 6): wykres – oś pozioma km toru, pionowa wysokość (przewyższenie); teren z NMT GUGiK (szary), niweleta
/// (czarna, załomy z łukami pionowymi), pochylenia ‰ i długości odcinków, nasyp (pomarańczowy) / przekop (niebieski). Załom przeciąga
/// się myszą w pionie (Z co 1 cm). Tabela załomów: km, Z, Rv. Przyciski: teren z NMT, niweleta z terenu (uproszczenie), nowy załom.
/// </summary>
public class ProfilWindow : Window
{
    readonly MainWindow main;
    public string TrackId { get; private set; }
    public bool WasClosed;
    public readonly ProfilView Plot;
    public readonly StackPanel Table = new() { Margin = new Thickness(8) };
    public readonly TextBox TolBox = new() { Text = "0,3", Width = 40, ToolTip = "Tolerancja uproszczenia [m]: niweleta odchodzi od terenu najwyżej o tyle (przed scaleniem krótkich odcinków)" };
    public readonly TextBox MinLenBox = new() { Text = "100", Width = 40, ToolTip = "Najkrótszy odcinek stałego pochylenia [m]" };
    public readonly TextBox StepBox = new() { Text = "10", Width = 34, ToolTip = "Krok próbkowania terenu wzdłuż osi toru [m]" };
    readonly TextBlock status = new() { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
    public string Status => status.Text;

    public ProfilWindow(MainWindow w)
    {
        main = w; Plot = new ProfilView(this);
        Width = 1100; Height = 640; FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
        Background = new SolidColorBrush(Color.FromRgb(0xf3, 0xf3, 0xf0));
        var root = new DockPanel();
        var bar = new WrapPanel { Margin = new Thickness(6) };
        Button B(string t, Action a, string tip) { var b = new Button { Content = t, Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 0, 6, 0), ToolTip = tip }; b.Click += (s, e) => a(); bar.Children.Add(b); return b; }
        B("Teren z NMT GUGiK", async () => await LoadTerrain(), "Wysokości terenu wzdłuż osi toru z numerycznego modelu terenu (NMT, ok. 1 m) – co krok");
        bar.Children.Add(new TextBlock { Text = "krok", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) }); bar.Children.Add(StepBox);
        bar.Children.Add(new TextBlock { Text = " m   ", VerticalAlignment = VerticalAlignment.Center });
        B("Niweleta z terenu", () => FromTerrain(), "Odcinki stałego pochylenia dopasowane do terenu (Douglas–Peucker); potem popraw ręcznie – NMT to teren, nie główka szyny (wiadukty!)");
        bar.Children.Add(new TextBlock { Text = "tolerancja", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) }); bar.Children.Add(TolBox);
        bar.Children.Add(new TextBlock { Text = " m, odcinek ≥", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) }); bar.Children.Add(MinLenBox);
        bar.Children.Add(new TextBlock { Text = " m   ", VerticalAlignment = VerticalAlignment.Center });
        B("+ załom", () => AddPoint(null), "Nowy załom w środku najdłuższego odcinka (albo pierwszy – na początku toru)");
        B("Wyczyść niweletę", () => Change("Niweleta usunięta", t => t.Profile.Clear()), "Usuń wszystkie załomy");
        bar.Children.Add(status);
        DockPanel.SetDock(bar, Dock.Top); root.Children.Add(bar);
        var sv = new ScrollViewer { Content = Table, Height = 190, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        DockPanel.SetDock(sv, Dock.Bottom); root.Children.Add(sv);
        root.Children.Add(Plot);
        Content = root;
        main.Doc.Changed += Refresh;
        Closed += (s, e) => main.Doc.Changed -= Refresh;
    }

    public UtTrack Track => main.Doc.P.Tracks.FirstOrDefault(t => t.Id == TrackId);
    public TrasaResult Route => Track is UtTrack t ? main.View.Geometry(t) : null;
    /// <summary>Obiekty inżynieryjne na torze profilu (km od–do na tym torze).</summary>
    public List<(UtObiekt O, double S0, double S1)> ObiektyToru => Obiekty.NaTorze(main.Doc.P, TrackId).ToList();

    public void SetTrack(string id) { TrackId = id; Refresh(); }

    void Change(string what, Action<UtTrack> a) { var id = TrackId; main.Doc.Change(what, p => { var t = p.Tracks.FirstOrDefault(x => x.Id == id); if (t != null) a(t); }); }

    public void Refresh()
    {
        var t = Track;
        Title = t != null ? $"Profil podłużny – tor {(t.Number.Length > 0 ? t.Number : "(bez numeru)")} – {UtView.Len3(Route.Length)}" : "Profil podłużny";
        BuildTable();
        Plot.InvalidateVisual();
    }

    static string F(double v, string f = "0.##") => v.ToString(f, CultureInfo.GetCultureInfo("pl-PL"));
    static double? Num(string s) => double.TryParse((s ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    void BuildTable()
    {
        Table.Children.Clear();
        var t = Track; if (t == null) return;
        var v = t.Profile; var secs = Profil.Sections(v);
        var g = new Grid();
        foreach (var w in new[] { 40.0, 90, 90, 80, 300, 30 }) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });
        void Put(UIElement e, int r, int c) { Grid.SetRow(e, r); Grid.SetColumn(e, c); g.Children.Add(e); }
        g.RowDefinitions.Add(new RowDefinition());
        foreach (var (h, c) in new[] { ("załom", 0), ("km toru [m]", 1), ("Z [m n.p.m.]", 2), ("Rv [m]", 3), ("dalej: pochylenie, długość, teren / nasyp", 4) })
            Put(new TextBlock { Text = h, Foreground = Brushes.DimGray, FontSize = 12 }, 0, c);
        for (int i = 0; i < v.Count; i++)
        {
            int ii = i; g.RowDefinitions.Add(new RowDefinition()); int r = i + 1;
            Put(new TextBlock { Text = $"P{i + 1}", VerticalAlignment = VerticalAlignment.Center }, r, 0);
            TextBox Box(double val, int col, string fmt, Action<UtProfilePoint, double> set)
            {
                var tb = new TextBox { Text = F(val, fmt), Margin = new Thickness(0, 1, 6, 1) };
                string last = tb.Text;
                void C() { if (tb.Text != last && Num(tb.Text) is double x) { last = tb.Text; Change("Załom niwelety", q => { set(q.Profile[ii], x); q.Profile.Sort((a, b) => a.S.CompareTo(b.S)); }); } }
                tb.LostFocus += (s, e) => C(); tb.KeyDown += (s, e) => { if (e.Key == Key.Enter) C(); };
                Put(tb, r, col); return tb;
            }
            Box(v[i].S, 1, "0.##", (q, x) => q.S = x);
            Box(v[i].Z, 2, "0.00", (q, x) => q.Z = x);
            if (i > 0 && i < v.Count - 1) Box(v[i].Rv, 3, "0", (q, x) => q.Rv = Math.Max(0, x));
            string info = "";
            if (i < secs.Count) info = $"{F(secs[i].Promil, "0.0")} ‰ na {F(secs[i].Len, "0")} m";
            double ground = Profil.Ground(t.Terrain, v[i].S);
            if (!double.IsNaN(ground)) info += $"   teren {F(ground, "0.00")}, {(v[i].Z >= ground ? "nasyp" : "przekop")} {F(Math.Abs(v[i].Z - ground), "0.00")} m";
            Put(new TextBlock { Text = info, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 }, r, 4);
            var del = new Button { Content = "✕", ToolTip = "Usuń załom", Margin = new Thickness(0, 1, 0, 1) };
            del.Click += (s, e) => Change("Usunięcie załomu", q => q.Profile.RemoveAt(ii));
            Put(del, r, 5);
        }
        Table.Children.Add(g);
        if (v.Count == 0) Table.Children.Add(new TextBlock { Text = "Brak niwelety. „Teren z NMT GUGiK”, potem „Niweleta z terenu” – albo „+ załom” i wysokości z ręki.", Foreground = Brushes.DimGray, Margin = new Thickness(0, 6, 0, 0) });
        foreach (var n in Profil.Check(main.Doc.P).Where(x => x.Track == TrackId))
            Table.Children.Add(new TextBlock { Text = n.Text, Foreground = Brushes.Firebrick, FontSize = 12 });
    }

    /// <summary>Teren z NMT (pobranie w tle); wynik w torze (zapisywany w projekcie).</summary>
    public async Task<bool> LoadTerrain()
    {
        var t = Track; var r = Route; if (t == null || r == null || r.Elements.Count == 0) return false;
        double step = Math.Clamp(Num(StepBox.Text) ?? 10, 1, 100);
        var pts = Profil.Sample(r, step);
        try
        {
            status.Text = "Pobieranie wysokości z NMT GUGiK…";
            var h = await Pobieranie.Nmt(pts.Select(q => q.P).ToList(), CancellationToken.None, m => Dispatcher.Invoke(() => status.Text = m));
            var terr = pts.Select((q, i) => new UtProfilePoint { S = Math.Round(q.S, 2), Z = h[i] }).Where(q => !double.IsNaN(q.Z)).ToList();
            Change("Teren z NMT", x => x.Terrain = terr);
            int miss = h.Count(double.IsNaN);
            status.Text = $"Teren: {terr.Count} punktów co {F(step)} m, {F(terr.Min(q => q.Z), "0.0")}–{F(terr.Max(q => q.Z), "0.0")} m n.p.m." + (miss > 0 ? $", {miss} bez danych" : "");
            main.Message(status.Text);
            return true;
        }
        catch (Exception ex) { status.Text = "NMT: " + ex.Message; main.Message(status.Text); return false; }
    }

    /// <summary>Niweleta z terenu (uproszczenie do odcinków stałego pochylenia).</summary>
    public void FromTerrain()
    {
        var t = Track; if (t == null) return;
        if (t.Terrain.Count < 2) { status.Text = "Najpierw „Teren z NMT GUGiK”"; return; }
        // most, wiadukt, tunel: NMT pokazuje dolinę albo wzgórze – niweleta przechodzi przez obiekt prosto (§ 5m)
        var bez = Obiekty.BezNmt(main.Doc.P, t.Id);
        var v = Profil.FromTerrain(t.Terrain, Num(TolBox.Text) ?? 0.3, Num(MinLenBox.Text) ?? 100, bez.Select(b => (b.S0, b.S1)).ToList());
        Change("Niweleta z terenu", x => x.Profile = v);
        var secs = Profil.Sections(v);
        status.Text = $"Niweleta: {v.Count} załomów, pochylenia {string.Join(" / ", secs.Select(s => F(s.Promil, "0.0")))} ‰" +
            (bez.Count > 0 ? $"; bez NMT na obiektach: {string.Join(", ", bez.Select(b => Obiekty.Opis(b.O)).Distinct())}" : "") + " – popraw ręcznie (NMT to teren, nie główka szyny)";
        main.Message(status.Text);
    }

    public void AddPoint(double? at)
    {
        var t = Track; var r = Route; if (t == null || r == null) return;
        var v = t.Profile;
        double s; double z;
        if (at is double a) s = a;
        else if (v.Count == 0) s = 0;
        else if (v.Count == 1) s = v[0].S < r.Length / 2 ? r.Length : 0;
        else { var secs = Profil.Sections(v); var best = secs.OrderByDescending(x => x.Len).First(); s = (best.S0 + best.S1) / 2; }
        z = Profil.Z(v, s); if (double.IsNaN(z)) z = Profil.Ground(t.Terrain, s); if (double.IsNaN(z)) z = 200;
        Change("Nowy załom", x => { x.Profile.Add(new UtProfilePoint { S = Math.Round(s, 2), Z = Math.Round(z, 2) }); x.Profile.Sort((p, q) => p.S.CompareTo(q.S)); });
    }

    public void MovePoint(int i, double z) => Change("Wysokość załomu", x => { if (i < x.Profile.Count) x.Profile[i].Z = Math.Round(z, 2); });
}

/// <summary>Wykres profilu podłużnego.</summary>
public class ProfilView : FrameworkElement
{
    readonly ProfilWindow w;
    public ProfilView(ProfilWindow w)
    {
        this.w = w; ClipToBounds = true;
        MouseMove += (s, e) => { hover = e.GetPosition(this); if (drag >= 0 && e.LeftButton == MouseButtonState.Pressed) dragZ = ZAt(hover.Value.Y); InvalidateVisual(); };
        MouseLeave += (s, e) => { hover = null; InvalidateVisual(); };
        MouseLeftButtonDown += (s, e) => { var p = e.GetPosition(this); drag = Hit(p); if (drag >= 0) { dragZ = ZAt(p.Y); CaptureMouse(); } };
        MouseLeftButtonUp += (s, e) => { if (drag >= 0) { int i = drag; drag = -1; ReleaseMouseCapture(); w.MovePoint(i, dragZ); } };
    }
    Point? hover; int drag = -1; double dragZ;
    const double L = 60, R = 20, T = 20, B = 36;
    double s0, s1, z0, z1;

    void Range()
    {
        var t = w.Track; var r = w.Route;
        s0 = 0; s1 = Math.Max(1, r?.Length ?? 1);
        var zs = new List<double>();
        if (t != null) { zs.AddRange(t.Terrain.Select(q => q.Z)); zs.AddRange(t.Profile.Select(q => q.Z)); }
        if (zs.Count == 0) { z0 = 0; z1 = 10; return; }
        z0 = zs.Min(); z1 = zs.Max(); double pad = Math.Max(1, (z1 - z0) * 0.15); z0 -= pad; z1 += pad;
    }
    double X(double s) => L + (s - s0) / (s1 - s0) * (ActualWidth - L - R);
    double Y(double z) => T + (z1 - z) / (z1 - z0) * (ActualHeight - T - B);
    double ZAt(double y) => Math.Round((z1 - (y - T) / (ActualHeight - T - B) * (z1 - z0)) * 100) / 100;
    double SAt(double x) => s0 + (x - L) / (ActualWidth - L - R) * (s1 - s0);

    int Hit(Point p)
    {
        var t = w.Track; if (t == null) return -1;
        for (int i = 0; i < t.Profile.Count; i++) if ((new Point(X(t.Profile[i].S), Y(t.Profile[i].Z)) - p).Length < 7) return i;
        return -1;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double W = ActualWidth, H = ActualHeight; var pl = CultureInfo.GetCultureInfo("pl-PL");
        dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, W, H));
        var t = w.Track; if (t == null || W < 100 || H < 80) return;
        Range();
        FormattedText Tx(string s, double sz, Brush b) => new(s, pl, FlowDirection.LeftToRight, new Typeface("Segoe UI"), sz, b, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var grid = new Pen(new SolidColorBrush(Color.FromRgb(0xe4, 0xe4, 0xe0)), 1);
        // siatka: km co okrągłą wartość, Z co okrągły metr
        double stepS = UtView.Nice((s1 - s0) / 12), stepZ = UtView.Nice((z1 - z0) / 8);
        for (double s = Math.Ceiling(s0 / stepS) * stepS; s <= s1; s += stepS) { dc.DrawLine(grid, new Point(X(s), T), new Point(X(s), H - B)); var f = Tx(s.ToString("0", pl), 10.5, Brushes.DimGray); dc.DrawText(f, new Point(X(s) - f.Width / 2, H - B + 2)); }
        for (double z = Math.Ceiling(z0 / stepZ) * stepZ; z <= z1; z += stepZ) { dc.DrawLine(grid, new Point(L, Y(z)), new Point(W - R, Y(z))); var f = Tx(z.ToString("0.#", pl), 10.5, Brushes.DimGray); dc.DrawText(f, new Point(L - f.Width - 4, Y(z) - f.Height / 2)); }
        var cap = Tx("km toru [m]  ·  Z [m n.p.m.]  ·  przewyższenie ×" + ((H - T - B) / (z1 - z0) / ((W - L - R) / (s1 - s0))).ToString("0", pl) + "  ·  szary – teren (NMT), czarny – niweleta, pomarańczowy – nasyp, niebieski – przekop, brązowy – obiekty", 11, Brushes.DimGray);
        dc.DrawText(cap, new Point(L, H - cap.Height - 2));
        var v = t.Profile.Select(q => new UtProfilePoint { S = q.S, Z = q.Z, Rv = q.Rv }).ToList();
        if (drag >= 0 && drag < v.Count) v[drag].Z = dragZ;
        // obiekty inżynieryjne: pas u góry z nazwą (wzdłuż toru), kreska w km (poprzeczne); na moście / wiadukcie / w tunelu bez nasypu i przekopu
        var obs = w.ObiektyToru;
        bool OnObj(double s) => obs.Any(x => Obiekty.Info(x.O.Kind).BezNmt && s > x.S0 && s < x.S1);
        foreach (var (o, a, b) in obs)
        {
            var oi = Obiekty.Info(o.Kind); var f = Tx((o.Name.Length > 0 ? o.Name : oi.Name) + (o.Nad.Length > 0 ? " – " + o.Nad : ""), 10.5, Brushes.SaddleBrown);
            if (oi.Pol == Obiekty.Pol.Poprzeczny)
            {
                dc.DrawLine(new Pen(Brushes.SaddleBrown, 1) { DashStyle = DashStyles.Dash }, new Point(X(a), T), new Point(X(a), H - B));
                dc.DrawText(f, new Point(X(a) + 3, T + 16));
                continue;
            }
            var rr = new Rect(new Point(X(a), T), new Point(Math.Max(X(a) + 1, X(b)), H - B));
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(oi.BezNmt ? (byte)0x22 : (byte)0x10, 0x8b, 0x45, 0x13)), null, rr);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x90, 0x8b, 0x45, 0x13)), null, new Rect(rr.X, T, rr.Width, 3));
            if (rr.Width > f.Width + 4) dc.DrawText(f, new Point(rr.X + (rr.Width - f.Width) / 2, T + 4));
        }
        // nasyp / przekop
        if (t.Terrain.Count > 1 && v.Count > 0)
            for (int i = 0; i + 1 < t.Terrain.Count; i++)
            {
                double sa = t.Terrain[i].S, sb = t.Terrain[i + 1].S, za = Profil.Z(v, sa), zb = Profil.Z(v, sb);
                if (OnObj((sa + sb) / 2)) continue;
                var g = new StreamGeometry();
                using (var c = g.Open()) { c.BeginFigure(new Point(X(sa), Y(za)), true, true); c.LineTo(new Point(X(sb), Y(zb)), false, false); c.LineTo(new Point(X(sb), Y(t.Terrain[i + 1].Z)), false, false); c.LineTo(new Point(X(sa), Y(t.Terrain[i].Z)), false, false); }
                bool fill = (za + zb) / 2 >= (t.Terrain[i].Z + t.Terrain[i + 1].Z) / 2;
                dc.DrawGeometry(new SolidColorBrush(fill ? Color.FromArgb(0x50, 0xf0, 0x90, 0x20) : Color.FromArgb(0x50, 0x20, 0x80, 0xf0)), null, g);
            }
        // teren
        if (t.Terrain.Count > 1)
        {
            var g = new StreamGeometry();
            using (var c = g.Open()) { c.BeginFigure(new Point(X(t.Terrain[0].S), Y(t.Terrain[0].Z)), false, false); foreach (var q in t.Terrain.Skip(1)) c.LineTo(new Point(X(q.S), Y(q.Z)), true, true); }
            dc.DrawGeometry(null, new Pen(Brushes.Gray, 1.2), g);
            // pod mostem / wiaduktem / nad tunelem teren cienką linią przerywaną
            for (int i = 0; i + 1 < t.Terrain.Count; i++)
                if (OnObj((t.Terrain[i].S + t.Terrain[i + 1].S) / 2))
                {
                    var a0 = new Point(X(t.Terrain[i].S), Y(t.Terrain[i].Z)); var b0 = new Point(X(t.Terrain[i + 1].S), Y(t.Terrain[i + 1].Z));
                    dc.DrawLine(new Pen(Brushes.White, 2.4), a0, b0); dc.DrawLine(new Pen(Brushes.Gray, 0.8) { DashStyle = DashStyles.Dash }, a0, b0);
                }
        }
        // niweleta (z łukami pionowymi – próbki co 1 m)
        if (v.Count > 0)
        {
            var g = new StreamGeometry();
            double a = Math.Min(v[0].S, s0), b = Math.Max(v[^1].S, s1);
            using (var c = g.Open())
            {
                c.BeginFigure(new Point(X(a), Y(Profil.Z(v, a))), false, false);
                for (double s = a; s <= b; s += Math.Max(0.5, (s1 - s0) / 600)) c.LineTo(new Point(X(s), Y(Profil.Z(v, s))), true, true);
                c.LineTo(new Point(X(b), Y(Profil.Z(v, b))), true, true);
            }
            dc.DrawGeometry(null, new Pen(Brushes.Black, 2), g);
            var secs = Profil.Sections(v);
            foreach (var sc in secs)
            {
                var f = Tx($"{sc.Promil.ToString("0.0", pl)} ‰ / {sc.Len.ToString("0", pl)} m", 11.5, Brushes.Black);
                double xm = (X(sc.S0) + X(sc.S1)) / 2, ym = (Y(sc.Z0) + Y(sc.Z1)) / 2;
                if (X(sc.S1) - X(sc.S0) > f.Width + 6) dc.DrawText(f, new Point(xm - f.Width / 2, ym - f.Height - 6));
            }
            for (int i = 0; i < v.Count; i++)
            {
                var p = new Point(X(v[i].S), Y(v[i].Z));
                dc.DrawEllipse(i == drag ? Brushes.OrangeRed : Brushes.White, new Pen(Brushes.Black, 1.4), p, 5, 5);
                var f = Tx($"P{i + 1}: {v[i].Z.ToString("0.00", pl)}", 10.5, Brushes.Black);
                dc.DrawText(f, new Point(p.X + 6, p.Y + 4));
            }
        }
        if (hover is Point h && h.X > L && h.X < W - R)
        {
            double s = SAt(h.X), zn = Profil.Z(v, s), zt = Profil.Ground(t.Terrain, s);
            dc.DrawLine(new Pen(Brushes.SteelBlue, 1) { DashStyle = DashStyles.Dash }, new Point(h.X, T), new Point(h.X, H - B));
            var txt = $"km {s.ToString("0.0", pl)} m" + (double.IsNaN(zn) ? "" : $"   niweleta {zn.ToString("0.00", pl)}") + (double.IsNaN(zt) ? "" : $"   teren {zt.ToString("0.00", pl)}") +
                (obs.FirstOrDefault(x => s >= x.S0 - 0.5 && s <= x.S1 + 0.5) is var (oh, _, _) && oh != null ? "   " + Obiekty.Opis(oh)
                : !double.IsNaN(zn) && !double.IsNaN(zt) ? $"   {(zn >= zt ? "nasyp" : "przekop")} {Math.Abs(zn - zt).ToString("0.00", pl)} m" : "");
            var f = Tx(txt, 11.5, Brushes.SteelBlue);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0xe0, 0xff, 0xff, 0xff)), null, new Rect(h.X + 6, T, f.Width + 6, f.Height + 2));
            dc.DrawText(f, new Point(h.X + 9, T + 1));
        }
    }
}

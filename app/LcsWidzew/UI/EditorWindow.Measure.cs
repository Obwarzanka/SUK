using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LcsWidzew.Model;

namespace LcsWidzew.UI;

/// <summary>
/// Miarka (łamana: długości odcinków, kąt, suma, droga po torze, promień przez 3 punkty) i prowadnice przeciągane z linijek.
/// Wszystko tylko w edytorze; prowadnice zapisują się w station.json („guides”), miarka nie.
/// </summary>
public partial class EditorWindow
{
    /// <summary>Pomiar miarką: punkty i to, w co trafiły (węzeł / tor / siatka) – potrzebne do drogi po torze.</summary>
    sealed class Meas
    {
        public readonly List<Point> P = new();
        public readonly List<(string Kind, string Id)> H = new();
        public bool Done;
    }
    readonly List<Meas> measures = new();
    Meas MeasCur => measures.Count > 0 && !measures[^1].Done ? measures[^1] : null;
    Point measDownAt;

    // ------------------------------------------------------------------ miarka
    void MeasureDown(Point w, MouseButtonEventArgs e)
    {
        var (p, k, id) = dispMode ? (Snap(w), "grid", null) : SnapTarget(w);
        if (k == "grid") p = Snap(w);
        var m = MeasCur;
        if (e?.ClickCount == 2 && m != null) { MeasureFinish(); return; }   // dwuklik – koniec (punkt dodał już pierwszy klik)
        if (m == null) { m = new Meas(); measures.Add(m); while (measures.Count > 8) measures.RemoveAt(0); }
        if (m.P.Count > 0 && (m.P[^1] - p).Length * panel.Scale < 2) return;
        m.P.Add(p); m.H.Add((k, id)); measDownAt = w;
        status.Text = "Miarka: " + MeasureText(m, null) + "   ·  klik – kolejny punkt, dwuklik / Enter / PPM – koniec, Backspace – cofnij punkt, H – profil wysokościowy, Esc – wyczyść";
    }

    /// <summary>Puszczenie przycisku: przeciągnięcie od punktu do punktu (jak dawna miarka) – od razu pomiar dwóch punktów.</summary>
    void MeasureUp(Point w)
    {
        var m = MeasCur;
        if (m == null || m.P.Count != 1 || (w - measDownAt).Length * panel.Scale < 8) return;
        var (p, k, id) = dispMode ? (Snap(w), "grid", null) : SnapTarget(w); if (k == "grid") p = Snap(w);
        m.P.Add(p); m.H.Add((k, id)); MeasureFinish();
    }

    void MeasureFinish()
    {
        var m = MeasCur; if (m == null) return;
        if (m.P.Count < 2) { measures.Remove(m); status.Text = "Miarka: kliknij co najmniej dwa punkty"; return; }
        m.Done = true;
        status.Text = "Miarka: " + MeasureText(m, null) + "   ·  H – profil wysokościowy wzdłuż pomiaru, Esc – wyczyść pomiary";
        panel.InvalidateVisual();
    }

    /// <summary>PPM / Esc w miarce: koniec bieżącego pomiaru, potem czyszczenie, potem wyjście z narzędzia. True – obsłużone.</summary>
    bool MeasureRight()
    {
        if (MeasCur is { } m) { if (m.P.Count >= 2) MeasureFinish(); else measures.Remove(m); return true; }
        if (measures.Count > 0) { measures.Clear(); status.Text = "Miarka: pomiary wyczyszczone"; return true; }
        return false;
    }

    bool MeasureKey(Key k)
    {
        if (tool != Tool.Measure) return false;
        switch (k)
        {
            case Key.Enter: MeasureFinish(); return true;
            case Key.Back when MeasCur is { } m: m.P.RemoveAt(m.P.Count - 1); m.H.RemoveAt(m.H.Count - 1); if (m.P.Count == 0) measures.Remove(m); return true;
            case Key.H:
                var last = MeasCur ?? (measures.Count > 0 ? measures[^1] : null);
                if (last == null || last.P.Count < 2) { status.Text = "Profil: najpierw zmierz miarką trasę (co najmniej 2 punkty)"; return true; }
                ProfileStart(last.P.Select(q => (q.X, q.Y)).ToList(), null, "pomiar miarką");
                return true;
        }
        return false;
    }

    double MLen(Point a, Point b) => panel.PlanView ? doc.PlanLen(a.X, a.Y, b.X, b.Y) : (b - a).Length;
    string MUnit => panel.PlanView ? "m" : "j.";
    /// <summary>Kąt odcinka względem poziomu w terenie (metry w obu kierunkach, w górę – dodatni) i pochylenie 1:n.</summary>
    string MAngle(Point a, Point b)
    {
        double dx = (b.X - a.X) / (panel.PlanView ? doc.Ux : 1), dy = -(b.Y - a.Y) / (panel.PlanView ? doc.Uy : 1);
        if (Math.Abs(dx) < 1e-9 && Math.Abs(dy) < 1e-9) return "";
        double ang = Math.Atan2(dy, dx) * 180 / Math.PI; if (Math.Abs(ang) < 0.005) ang = 0; double sk = Math.Abs(dx) > 1e-9 ? Math.Abs(dy / dx) : double.PositiveInfinity;
        return sk > 1e-4 && sk < 0.5 ? $"{ang:0.##}° (1:{1 / sk:0.#})" : $"{ang:0.##}°";
    }
    /// <summary>Promień okręgu przez 3 punkty [m] (null – punkty prawie na prostej).</summary>
    (double R, Point C)? MRadius(Point a, Point b, Point c)
    {
        double kx = panel.PlanView ? doc.Ux : 1, ky = panel.PlanView ? doc.Uy : 1;
        double ax = a.X / kx, ay = a.Y / ky, bx = b.X / kx, by = b.Y / ky, cx = c.X / kx, cy = c.Y / ky;
        double d = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
        if (Math.Abs(d) < 1e-9) return null;
        double ux = ((ax * ax + ay * ay) * (by - cy) + (bx * bx + by * by) * (cy - ay) + (cx * cx + cy * cy) * (ay - by)) / d;
        double uy = ((ax * ax + ay * ay) * (cx - bx) + (bx * bx + by * by) * (ax - cx) + (cx * cx + cy * cy) * (bx - ax)) / d;
        double r = Math.Sqrt((ax - ux) * (ax - ux) + (ay - uy) * (ay - uy));
        return r > 1e6 ? null : (r, new Point(ux * kx, uy * ky));
    }

    /// <summary>Położenie punktu na torze: krawędź i odległość od jej końca A [m] (wg długości toru `len`).</summary>
    (string E, double S)? TrackPos(Point p, (string Kind, string Id) h)
    {
        if (h.Kind == "node" && st.Nodes.TryGetValue(h.Id, out var nd) && nd.Edges.FirstOrDefault(x => !st.Edges[x].Link) is { } ne)
            return (ne, st.Edges[ne].A == h.Id ? 0 : st.Edges[ne].Len);
        if (h.Kind != "edge" || !st.Edges.TryGetValue(h.Id, out var ed)) return null;
        double tot = 0, at = 0, bd = double.MaxValue;
        for (int i = 0; i < ed.Pts.Count - 1; i++)
        {
            var a = ed.Pts[i]; var b = ed.Pts[i + 1]; double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy, seg = MLen(new Point(a.X, a.Y), new Point(b.X, b.Y));
            double t = l2 == 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2, 0, 1);
            var q = new Point(a.X + t * dx, a.Y + t * dy); var d = (q - p).Length;
            if (d < bd) { bd = d; at = tot + t * seg; }
            tot += seg;
        }
        return (h.Id, tot > 1e-9 ? at / tot * ed.Len : 0);
    }

    /// <summary>Najkrótsza droga po torach [m] między dwoma punktami na torach (także w środku toru).</summary>
    double? AlongTrack((string E, double S) a, (string E, double S) b)
    {
        var ea = st.Edges[a.E]; var eb = st.Edges[b.E];
        double best = a.E == b.E ? Math.Abs(a.S - b.S) : double.MaxValue;
        var dist = new Dictionary<string, double>(); var pq = new PriorityQueue<string, double>();
        void Push(string n, double d) { if (!dist.TryGetValue(n, out var od) || d < od) { dist[n] = d; pq.Enqueue(n, d); } }
        Push(ea.A, a.S); Push(ea.B, ea.Len - a.S);
        var done = new HashSet<string>();
        while (pq.TryDequeue(out var n, out var d))
        {
            if (d >= best || !done.Add(n)) continue;
            if (n == eb.A) best = Math.Min(best, d + b.S);
            if (n == eb.B) best = Math.Min(best, d + eb.Len - b.S);
            foreach (var x in st.Nodes[n].Edges) { var e = st.Edges[x]; Push(e.A == n ? e.B : e.A, d + e.Len); }
        }
        return best < double.MaxValue ? best : null;
    }

    /// <summary>Opis pomiaru: suma, ostatni odcinek z kątem, droga po torach, promień przez 3 ostatnie punkty.</summary>
    string MeasureText(Meas m, Point? extra)
    {
        var p = m.P.ToList(); var h = m.H.ToList();
        if (extra is Point ex) { var (q, k, id) = SnapTarget(ex); if (k == "grid") q = Snap(ex); p.Add(q); h.Add((k, id)); }
        if (p.Count < 2) return "kliknij następny punkt";
        double sum = 0; double? track = 0;
        for (int i = 0; i < p.Count - 1; i++)
        {
            sum += MLen(p[i], p[i + 1]);
            if (track != null && TrackPos(p[i], h[i]) is { } ta && TrackPos(p[i + 1], h[i + 1]) is { } tb && AlongTrack(ta, tb) is double d) track += d; else track = null;
        }
        var sb = new System.Text.StringBuilder();
        sb.Append(p.Count > 2 ? $"Σ {sum:0.##} {MUnit} ({p.Count - 1} odc.); ostatni {MLen(p[^2], p[^1]):0.##} {MUnit}" : $"{sum:0.##} {MUnit}");
        if (panel.PlanView && !doc.RealScale && Math.Abs(p[^1].Y - p[^2].Y) > 0.01) sb.Append($" (wzdłuż {Math.Abs(p[^1].X - p[^2].X) / doc.Ux:0.#} m, w poprzek {Math.Abs(p[^1].Y - p[^2].Y) / doc.Uy:0.##} m)");
        if (MAngle(p[^2], p[^1]) is { Length: > 0 } an) sb.Append($", kąt {an}");
        if (track is double t) sb.Append($"; po torach {t:0.#} m");
        else if (h.Count >= 2 && h[^1].Kind != "grid" && h[^2].Kind != "grid" && TrackPos(p[^2], h[^2]) is { } a2 && TrackPos(p[^1], h[^1]) is { } b2 && AlongTrack(a2, b2) is null) sb.Append("; brak połączenia torami");
        if (p.Count >= 3 && MRadius(p[^3], p[^2], p[^1]) is { } rc) sb.Append($"; R = {rc.R:0} m (3 ostatnie punkty)");
        return sb.ToString();
    }

    void DrawMeasures(DrawingContext dc, double scale, double fs)
    {
        if (measures.Count == 0) return;
        var mb = Brushes.Magenta; var mpn = new Pen(mb, 2 / scale) { DashStyle = DashStyles.Dash }; var thin = new Pen(new SolidColorBrush(Color.FromArgb(0x90, 0xff, 0x00, 0xff)), 1 / scale) { DashStyle = DashStyles.Dot };
        void Tag(string t, Point at)
        {
            var ft = Ft(t, fs, mb); var v = panel.ViewWorld;
            at = new Point(Math.Max(v.Left + 40 / scale, Math.Min(at.X, v.Right - ft.Width - 12 / scale)), Math.Max(v.Top + 20 / scale, Math.Min(at.Y, v.Bottom - ft.Height - 6 / scale)));
            var r = new Rect(at.X, at.Y, ft.Width + 6 / scale, ft.Height + 2 / scale);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0xd8, 0, 0, 0)), null, r); dc.DrawText(ft, new Point(at.X + 3 / scale, at.Y + 1 / scale));
        }
        foreach (var m in measures)
        {
            bool live = !m.Done && tool == Tool.Measure;
            var p = m.P.ToList();
            if (live) { var (q, k, _) = SnapTarget(mouse); p.Add(k == "grid" ? Snap(mouse) : q); }
            for (int i = 0; i < p.Count - 1; i++)
            {
                dc.DrawLine(mpn, p[i], p[i + 1]);
                if (p.Count > 2 || !m.Done)
                {
                    var mid = new Point((p[i].X + p[i + 1].X) / 2, (p[i].Y + p[i + 1].Y) / 2);
                    var ft = Ft($"{MLen(p[i], p[i + 1]):0.#} {MUnit}  {MAngle(p[i], p[i + 1])}", fs * 0.9, mb); dc.DrawText(ft, new Point(mid.X - ft.Width / 2, mid.Y + 3 / scale));
                }
            }
            foreach (var q in m.P) dc.DrawEllipse(null, mpn, q, 4 / scale, 4 / scale);
            if (p.Count >= 3 && MRadius(p[^3], p[^2], p[^1]) is { } rc && rc.R < 20000)
                dc.DrawEllipse(null, thin, rc.C, rc.R * (panel.PlanView ? doc.Ux : 1), rc.R * (panel.PlanView ? doc.Uy : 1));
            if (p.Count >= 2) Tag(MeasureText(m, live ? mouse : null), new Point(p[^1].X + 10 / scale, p[^1].Y + 10 / scale));
        }
    }

    // ------------------------------------------------------------------ prowadnice z linijek
    /// <summary>Prowadnice planu: h – poziome (y), v – pionowe (x), w jednostkach planu. Zapis w station.json („guides”).</summary>
    JsonObject Guides => doc.Root["guides"] as JsonObject ?? (JsonObject)(doc.Root["guides"] = new JsonObject { ["h"] = new JsonArray(), ["v"] = new JsonArray() });
    List<double> GuideList(string k) => (Guides[k] as JsonArray ?? new JsonArray()).Select(x => StationDoc.D(x)).ToList();
    void GuideSet(string k, List<double> v) { Guides[k] = new JsonArray(v.Select(x => (JsonNode)Math.Round(x, 2)).ToArray()); dirty = true; }
    bool HasGuides => doc.Root["guides"] is JsonObject g && (g["h"] as JsonArray)?.Count + (g["v"] as JsonArray)?.Count > 0;
    /// <summary>Przeciągana prowadnica: rodzaj (h / v) i indeks (-1 – nowa z linijki).</summary>
    (string K, int I)? guideDrag; double guidePos;
    double RulerH => 16 / panel.Scale;
    bool InTopRuler(Point w) { var v = panel.ViewWorld; return w.Y < v.Top + RulerH && w.X > v.Left + RulerH * 2.2; }
    bool InLeftRuler(Point w) { var v = panel.ViewWorld; return w.X < v.Left + RulerH * 2.2 && w.Y > v.Top + RulerH; }

    (string K, int I)? GuideAt(Point w)
    {
        double tol = 4 / Math.Max(0.004, panel.Scale);
        var h = GuideList("h"); for (int i = 0; i < h.Count; i++) if (Math.Abs(w.Y - h[i]) < tol) return ("h", i);
        var v = GuideList("v"); for (int i = 0; i < v.Count; i++) if (Math.Abs(w.X - v[i]) < tol) return ("v", i);
        return null;
    }

    /// <summary>Wciśnięcie: z linijki – nowa prowadnica; w „Wskaż” na prowadnicy (nie na węźle / torze) – przesuwanie.</summary>
    bool GuideDown(Point w, MouseButtonEventArgs e)
    {
        if (dispMode || !panel.PlanView || e?.ChangedButton == MouseButton.Right) return false;
        if (InTopRuler(w)) { guideDrag = ("h", -1); guidePos = w.Y; status.Text = "Prowadnica pozioma: przeciągnij na plan (z powrotem na linijkę – rezygnacja)"; return true; }
        if (InLeftRuler(w)) { guideDrag = ("v", -1); guidePos = w.X; status.Text = "Prowadnica pionowa: przeciągnij na plan (z powrotem na linijkę – rezygnacja)"; return true; }
        if (tool == Tool.Select && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && PickNode(w) == null && PickEdge(w) == null && GuideAt(w) is { } g)
        {
            guideDrag = g; guidePos = g.K == "h" ? GuideList("h")[g.I] : GuideList("v")[g.I];
            status.Text = "Przesuwanie prowadnicy (upuść na linijce – usuń)"; return true;
        }
        return false;
    }

    /// <summary>Położenie prowadnicy pod kursorem: na wysokości (pozioma) / w osi (pionowa) widocznego węzła w zasięgu ok. 10 px, siatka (gdy przyciąganie) albo dokładnie.</summary>
    double GuideSnap(string k, Point w)
    {
        var v = panel.ViewWorld; double best = R; double? at = null;
        foreach (var n in st.Nodes.Values)
        {
            if (!v.Contains(new Point(n.X, n.Y))) continue;
            double d = k == "h" ? Math.Abs(n.Y - w.Y) : Math.Abs(n.X - w.X);
            if (d < best) { best = d; at = k == "h" ? n.Y : n.X; }
        }
        if (at is double a) return a;
        var s = Snap(w); return k == "h" ? s.Y : s.X;
    }

    void GuideMove(Point w) { if (guideDrag is { } g) guidePos = GuideSnap(g.K, w); }

    void GuideUp(Point w)
    {
        if (guideDrag is not { } g) return;
        guideDrag = null;
        bool remove = g.K == "h" ? InTopRuler(w) || w.Y < panel.ViewWorld.Top + RulerH : InLeftRuler(w) || w.X < panel.ViewWorld.Left + RulerH * 2.2;
        var l = GuideList(g.K);
        if (g.I >= 0 && g.I < l.Count) l.RemoveAt(g.I);
        if (!remove) l.Add(guidePos);
        GuideSet(g.K, l);
        status.Text = remove ? (g.I >= 0 ? "Usunięto prowadnicę" : "Bez prowadnicy") : $"Prowadnica {(g.K == "h" ? "pozioma" : "pionowa")} na {GuideLabel(g.K, guidePos)} – przyciąga punkty (Alt – bez przyciągania); PPM na prowadnicy – usuń";
        if (selKind == null) ShowProps();
    }

    bool GuideRight(Point w)
    {
        if (dispMode || !panel.PlanView || tool != Tool.Select || PickNode(w) != null || GuideAt(w) is not { } g) return false;
        var l = GuideList(g.K); l.RemoveAt(g.I); GuideSet(g.K, l); status.Text = "Usunięto prowadnicę";
        if (selKind == null) ShowProps();
        return true;
    }

    string GuideLabel(string k, double v) => k == "h" ? $"{v / doc.Uy:0.##} m" : $"{v / doc.Ux:0.##} m";

    /// <summary>Przyciąganie punktu do prowadnic (zasięg ok. 10 px; Alt – bez).</summary>
    Point SnapGuides(Point p)
    {
        if (st == null || dispMode || !panel.PlanView || guideDrag != null || doc.Root["guides"] is not JsonObject || Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) return p;
        double best = R;
        foreach (var y in GuideList("h")) if (Math.Abs(p.Y - y) < best) { best = Math.Abs(p.Y - y); p.Y = y; }
        best = R;
        foreach (var x in GuideList("v")) if (Math.Abs(p.X - x) < best) { best = Math.Abs(p.X - x); p.X = x; }
        return p;
    }

    void DrawGuides(DrawingContext dc, double scale, double fs)
    {
        if (dispMode || !panel.PlanView) return;
        var v = panel.ViewWorld; var gb = new SolidColorBrush(Color.FromRgb(0x00, 0xb8, 0xd0));
        var gp = new Pen(gb, 1 / scale); var gd = new Pen(gb, 1.5 / scale) { DashStyle = DashStyles.Dash };
        var hs = GuideList("h"); var vs = GuideList("v");
        if (guideDrag is { } g) { var l = g.K == "h" ? hs : vs; if (g.I >= 0 && g.I < l.Count) l.RemoveAt(g.I); }
        foreach (var y in hs) dc.DrawLine(gp, new Point(v.Left, y), new Point(v.Right, y));
        foreach (var x in vs) dc.DrawLine(gp, new Point(x, v.Top), new Point(x, v.Bottom));
        // odstępy między sąsiednimi prowadnicami [m]
        void Gaps(List<double> l, bool horiz)
        {
            l = l.OrderBy(q => q).ToList();
            for (int i = 0; i < l.Count - 1; i++)
            {
                double a = l[i], b = l[i + 1], d = horiz ? (b - a) / doc.Uy : (b - a) / doc.Ux;
                var ft = Ft($"{d:0.##} m", fs, gb);
                if (horiz) { double x = v.Left + RulerH * 2.6; if ((b - a) * scale < ft.Height) continue; dc.DrawLine(gp, new Point(x, a), new Point(x, b)); dc.DrawText(ft, new Point(x + 3 / scale, (a + b) / 2 - ft.Height / 2)); }
                else { double y = v.Top + RulerH * 1.4; if ((b - a) * scale < ft.Width + 6) continue; dc.DrawLine(gp, new Point(a, y), new Point(b, y)); dc.DrawText(ft, new Point((a + b) / 2 - ft.Width / 2, y + 2 / scale)); }
            }
        }
        Gaps(hs, true); Gaps(vs, false);
        if (guideDrag is { } gg)
        {
            if (gg.K == "h") dc.DrawLine(gd, new Point(v.Left, guidePos), new Point(v.Right, guidePos)); else dc.DrawLine(gd, new Point(guidePos, v.Top), new Point(guidePos, v.Bottom));
            var others = gg.K == "h" ? hs : vs; var near = others.Count > 0 ? others.MinBy(q => Math.Abs(q - guidePos)) : (double?)null;
            var txt = GuideLabel(gg.K, guidePos) + (near is double nq ? $"  · do sąsiedniej {Math.Abs(nq - guidePos) / (gg.K == "h" ? doc.Uy : doc.Ux):0.##} m" : "");
            var ft = Ft(txt, fs, gb); var at = new Point(mouse.X + 12 / scale, mouse.Y + 8 / scale);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0xe0, 0, 0, 0)), null, new Rect(at.X, at.Y, ft.Width + 6 / scale, ft.Height + 2 / scale)); dc.DrawText(ft, new Point(at.X + 3 / scale, at.Y + 1 / scale));
        }
    }

    /// <summary>Panel planu: liczba prowadnic i usuwanie.</summary>
    void GuidesPanel()
    {
        if (!HasGuides) { props.Children.Add(T("Prowadnice: przeciągnij z linijki u góry (pozioma) albo z lewej (pionowa). Przyciągają punkty; odstępy w metrach przy linijce.", 10.5, Brushes.Gray)); return; }
        props.Children.Add(T($"Prowadnice: {GuideList("h").Count} poziome, {GuideList("v").Count} pionowe (przeciągnij – przesuń, na linijkę albo PPM – usuń)", 10.5, Brushes.Gray));
        props.Children.Add(Btn("Usuń wszystkie prowadnice", (a, e) => { doc.Root.Remove("guides"); dirty = true; ShowProps(); panel.InvalidateVisual(); status.Text = "Usunięto prowadnice"; }));
    }

    /// <summary>Test (--editshot … pomiar out.json): miarka łamana, kąt, promień, droga po torze, prowadnice i przyciąganie do nich.</summary>
    public string MeasureForTest(string outJson)
    {
        var log = new System.Text.StringBuilder(); FuzzMode = true; int bad = 0;
        void Ok(bool c, string m) { log.AppendLine((c ? "OK  " : "BŁĄD") + " " + m); if (!c) bad++; }
        doc = StationDoc.New("TM", "Miarka (test)"); path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lcs_miarka.json"); dispMode = false; Rebuild(true);
        PlanForTest(); chkSnap.IsChecked = false;
        double ux = doc.Ux, uy = doc.Uy; var scale0 = (doc.ScaleAlong, doc.ScaleAcross);
        // tor 1000 m z odgałęzieniem pod kątem (bez zwrotnicy – miarka liczy po grafie torów)
        var n0 = doc.AddNode("TM", 0, 0); var n1 = doc.AddNode("TM", 600 * ux, 0); var n2 = doc.AddNode("TM", 1000 * ux, 0); var n3 = doc.AddNode("TM", 900 * ux, -50 * uy);
        var e1 = doc.AddEdge(n0, n1, null, "TM"); var e2 = doc.AddEdge(n1, n2, null, "TM"); var e3 = doc.AddEdge(n1, n3, null, "TM"); Rebuild();
        panel.FitAll(); SetTool(Tool.Measure);
        // łamana 3 punkty: (100,0) na torze → (400,0) na torze → (400, 300 w górę) pusto
        MeasureDown(new Point(100 * ux, 0), null); MeasureDown(new Point(400 * ux, 0), null); MeasureDown(new Point(400 * ux, -300 * uy), null); MeasureFinish();
        var m = measures[^1]; var txt = MeasureText(m, null);
        Ok(m.Done && m.P.Count == 3 && txt.Contains("Σ 600 m"), $"łamana 3 punkty: {txt}");
        Ok(txt.Contains("kąt 90°"), "kąt ostatniego odcinka 90°");
        // kąt 1:9 (rozjazd)
        measures.Clear(); MeasureDown(new Point(0, 500 * uy), null); MeasureDown(new Point(90 * ux, 490 * uy), null); MeasureFinish();
        txt = MeasureText(measures[^1], null); Ok(txt.Contains("(1:9)"), $"pochylenie 1:9 – {txt}");
        // promień: 3 punkty na okręgu R = 500 m
        measures.Clear(); double Rr = 500; Point C(double deg) => new(Rr * Math.Sin(deg * Math.PI / 180) * ux, (2000 - Rr + Rr * Math.Cos(deg * Math.PI / 180)) * uy);
        MeasureDown(C(-20), null); MeasureDown(C(0), null); MeasureDown(C(25), null); MeasureFinish();
        txt = MeasureText(measures[^1], null); Ok(MRadius(C(-20), C(0), C(25)) is { } rr && Math.Abs(rr.R - Rr) < 0.5 && txt.Contains("R = 500 m"), $"promień przez 3 punkty: {txt}");
        // droga po torze: od środka toru e1 (300 m) do końca odgałęzienia n3 – 300 + dł. e3
        measures.Clear(); MeasureDown(new Point(300 * ux, 0.2), null); MeasureDown(new Point(900 * ux, -50 * uy), null); MeasureFinish();
        double l3 = StationDoc.D(doc.Edge(e3)["len"]);
        txt = MeasureText(measures[^1], null); Ok(txt.Contains($"po torach {300 + l3:0.#} m"), $"droga po torach od środka toru do końca odgałęzienia ({300 + l3:0.#} m): {txt}");
        measures.Clear(); MeasureDown(new Point(200 * ux, 0), null); MeasureDown(new Point(700 * ux, 0), null); MeasureFinish();
        txt = MeasureText(measures[^1], null); Ok(txt.Contains("po torach 500 m"), $"dwa punkty w środku torów przez węzeł: {txt}");
        // przeciągnięcie (jak dawna miarka)
        measures.Clear(); MeasureDown(new Point(0, 100 * uy), null); MeasureUp(new Point(250 * ux, 100 * uy));
        Ok(measures.Count == 1 && measures[0].Done && MeasureText(measures[0], null).StartsWith("250 m"), "przeciągnięcie od punktu do punktu – 250 m");
        Ok(MeasureRight() && measures.Count == 0, "Esc / PPM czyści pomiary");
        Ok(!MeasureRight(), "kolejne Esc – wyjście z narzędzia");
        // prowadnice: z linijki u góry (pozioma) na y = 50 m, z lewej (pionowa) na węzeł n1
        SetTool(Tool.Select);
        var v = panel.ViewWorld;
        GuideDown(new Point(v.Left + v.Width / 2, v.Top + RulerH / 2), null); GuideMove(new Point(v.Left + v.Width / 2, 50 * uy)); GuideUp(new Point(v.Left + v.Width / 2, 50 * uy));
        GuideDown(new Point(v.Left + RulerH, v.Top + v.Height / 2), null); GuideMove(new Point(600 * ux + 0.3 * R, 30 * uy)); GuideUp(new Point(600 * ux + 0.3 * R, 30 * uy));
        Ok(GuideList("h").Count == 1 && Math.Abs(GuideList("h")[0] - 50 * uy) < 0.01, $"prowadnica pozioma na {GuideLabel("h", GuideList("h").FirstOrDefault())}");
        Ok(GuideList("v").Count == 1 && Math.Abs(GuideList("v")[0] - 600 * ux) < 0.01, "prowadnica pionowa przyciągnięta do węzła");
        var sp = Snap(new Point(300 * ux, 50 * uy + 0.4 * R)); Ok(Math.Abs(sp.Y - 50 * uy) < 1e-9, "punkt przyciągnięty do prowadnicy (bez siatki)");
        // przesunięcie prowadnicy i usunięcie na linijkę
        GuideDown(new Point(300 * ux, 50 * uy), null); GuideMove(new Point(300 * ux, 54.75 * uy)); GuideUp(new Point(300 * ux, 54.75 * uy));
        Ok(GuideList("h").Count == 1 && Math.Abs(GuideList("h")[0] - 54.75 * uy) < 0.01, "przesunięcie prowadnicy o 4,75 m");
        doc.Snapshot(); doc.AddNode("TM", 5, 5); var gBefore = doc.Root["guides"]?.ToJsonString(); DoUndo();
        Ok(doc.Root["guides"]?.ToJsonString() == gBefore, "Ctrl+Z nie zmienia prowadnic");
        doc.Save(outJson); var re = StationDoc.Load(outJson);
        Ok((re.Root["guides"]?["h"] as JsonArray)?.Count == 1 && (re.Root["guides"]?["v"] as JsonArray)?.Count == 1, "zapis i wczytanie prowadnic");
        GuideDown(new Point(300 * ux, 54.75 * uy), null); GuideUp(new Point(300 * ux, panel.ViewWorld.Top + RulerH / 3));
        Ok(GuideList("h").Count == 0, "prowadnica upuszczona na linijkę – usunięta");
        Ok((doc.ScaleAlong, doc.ScaleAcross) == scale0, $"skala planu bez zmian (1:{doc.ScaleAlong:0} / 1:{doc.ScaleAcross:0})");
        // zrzut: pomiary i prowadnice
        GuideSet("h", new() { 0, 4.75 * uy, 9.5 * uy }); GuideSet("v", new() { 600 * ux });
        SetTool(Tool.Measure);
        MeasureDown(new Point(100 * ux, 0), null); MeasureDown(new Point(600 * ux, 0), null); MeasureDown(new Point(900 * ux, -50 * uy), null); MeasureFinish();
        MeasureDown(C(-20), null); MeasureDown(C(0), null); MeasureDown(C(25), null); MeasureFinish();
        ZoomForTest(500 * ux, 0, 0.9);
        log.AppendLine($"błędów testu: {bad}, błędów edytora: {Errors.Count}");
        return log.ToString();
    }
}

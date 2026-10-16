using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Tory na płótnie: rysowanie osi (jedna linia jak w ISDR: tory główne 2 px, boczne 1,2 px, numer toru nad osią),
/// narzędzie Tor (wielobok styczny klikami, długość z klawiatury, przedłużenie kierunku, przyciąganie), zaznaczanie,
/// przeciąganie wierzchołków, wstawianie wierzchołka dwuklikiem.
/// </summary>
public partial class UtView
{
    /// <summary>Zaznaczony tor i wierzchołek (−1 – cały tor).</summary>
    public string SelectedTrack { get; private set; }
    public int SelectedVertex { get; private set; } = -1;
    public event Action SelectionChanged;
    /// <summary>Narzędzie Tor: tor narysowany (wierzchołki, tor przedłużany albo null, przedłużany od początku).</summary>
    public event Action<List<UtVertex>, string, bool> TrackDrawn;
    public event Action<string, int, XY> VertexMoved;
    public event Action<string, int, XY> VertexInserted;
    /// <summary>Zmiana stanu rysowania (opis do paska stanu).</summary>
    public event Action<string> DraftInfo;
    /// <summary>Promień i przejściowa w nowych wierzchołkach [m].</summary>
    public double NewR = 300, NewL = 0;

    public const double Gauge = 1.435, RailHead = 0.072;
    const double SnapPx = 10, HandlePx = 7, HitPx = 6;

    // ---- geometria torów (pamięć podręczna wg wierzchołków)
    readonly Dictionary<string, (string Sig, TrasaResult Res, List<(XY P, double Az)> Pts)> geo = new();
    static string Sig(UtTrack t) => string.Join(";", t.Vertices.Select(v => FormattableString.Invariant($"{v.X:R},{v.Y:R},{v.R:R},{v.L:R}")));

    public TrasaResult Geometry(UtTrack t) => Cached(t).Res;
    (string Sig, TrasaResult Res, List<(XY P, double Az)> Pts) Cached(UtTrack t)
    {
        var sig = Sig(t);
        if (geo.TryGetValue(t.Id, out var c) && c.Sig == sig) return c;
        var r = Trasa.Compute(t);
        c = (sig, r, r.Sample(0.5));
        geo[t.Id] = c; return c;
    }

    UtTrack Track(string id) => Doc?.P.Tracks.FirstOrDefault(t => t.Id == id);

    public void Select(string track, int vertex = -1)
    {
        if (SelectedTrack == track && SelectedVertex == vertex && SelectedTurnout == null && SelectedCrossing == null) return;
        SelectedTrack = track; SelectedVertex = track == null ? -1 : vertex; SelectedTurnout = null; SelectedCrossing = null;
        InvalidateVisual(); SelectionChanged?.Invoke();
    }

    // ------------------------------------------------------------------ przyciąganie
    public enum SnapKind { Brak, Koniec, Wierzcholek, Tor }
    public record SnapResult(XY P, SnapKind Kind, string Track, int Vertex);

    /// <summary>Przyciąganie (Ctrl – wyłączone): koniec toru, wierzchołek, punkt na osi toru – w promieniu 10 px.</summary>
    public SnapResult Snap(Point s, string skipTrack = null, int skipVertex = -1)
    {
        var w = ToWorld(s);
        if (Doc == null || Mods.HasFlag(ModifierKeys.Control)) return new(w, SnapKind.Brak, null, -1);
        SnapResult best = null; double bd = SnapPx;
        foreach (var t in Doc.P.Tracks)
            for (int i = 0; i < t.Vertices.Count; i++)
            {
                if (t.Id == skipTrack && i == skipVertex) continue;
                double d = (ToScreen(t.Vertices[i].P) - s).Length;
                bool end = i == 0 || i == t.Vertices.Count - 1;
                if (d < bd + (end ? 3 : 0)) { bd = d; best = new(t.Vertices[i].P, end ? SnapKind.Koniec : SnapKind.Wierzcholek, t.Id, i); }
            }
        if (best != null) return best;
        foreach (var t in Doc.P.Tracks)
        {
            if (t.Id == skipTrack) continue;
            var r = Geometry(t); if (r.Elements.Count == 0) continue;
            var (sKm, dist) = r.Nearest(w);
            var q = r.At(sKm).P; double sd = (ToScreen(q) - s).Length;
            if (sd < bd) { bd = sd; best = new(q, SnapKind.Tor, t.Id, -1); }
        }
        return best ?? new(w, SnapKind.Brak, null, -1);
    }

    // ------------------------------------------------------------------ narzędzie Tor
    readonly List<XY> draft = new();
    string extendId; bool extendAtStart;
    /// <summary>Długość wpisywana z klawiatury [m w terenie].</summary>
    public string LenBuf { get; private set; } = "";
    public IReadOnlyList<XY> Draft => draft;

    double KAt(XY p) => Geo.InPoland(p) ? Geo.ScaleFactor(p) : 1;

    /// <summary>Kolejny punkt rysowanego toru dla kursora: przyciągnięty; Shift – na przedłużeniu ostatniego odcinka.</summary>
    XY DraftPoint(Point s)
    {
        var sn = Snap(s);
        var w = sn.P;
        if (Mods.HasFlag(ModifierKeys.Shift) && DirLock() is XY d)
        {
            var a = draft[^1]; double t = (w.X - a.X) * d.X + (w.Y - a.Y) * d.Y;
            w = a + d * Math.Max(0, t);
        }
        return w;
    }

    /// <summary>Kierunek ostatniego odcinka (rysunek albo przedłużany tor).</summary>
    XY? DirLock()
    {
        if (draft.Count >= 2) { var d = draft[^1] - draft[^2]; return d * (1 / d.Length); }
        if (draft.Count == 1 && extendId != null && Track(extendId) is UtTrack t && t.Vertices.Count >= 2)
        {
            var v = t.Vertices; var d = extendAtStart ? v[0].P - v[1].P : v[^1].P - v[^2].P; return d * (1 / d.Length);
        }
        return null;
    }

    void DraftClick(Point s)
    {
        if (draft.Count == 0)
        {
            var sn = Snap(s);
            extendId = null;
            // start na końcu istniejącego toru – przedłużenie tego toru
            if (sn.Kind == SnapKind.Koniec && Track(sn.Track) is UtTrack t && !Locked(t.Id, sn.Vertex)) { extendId = t.Id; extendAtStart = sn.Vertex == 0; }
            draft.Add(sn.P);
        }
        else
        {
            var p = DraftPoint(s);
            if (XY.Dist(p, draft[^1]) < 1e-6) return;
            draft.Add(p);
            // klik na końcu innego toru – połączenie i koniec rysowania
            var sn = Snap(s);
            if (sn.Kind == SnapKind.Koniec && !Mods.HasFlag(ModifierKeys.Shift) && draft.Count >= 2) { FinishDraft(); return; }
        }
        LenBuf = ""; DraftChanged();
    }

    /// <summary>Długość wpisana z klawiatury: kolejny wierzchołek w tej odległości [m w terenie] w stronę kursora (Shift – na przedłużeniu).</summary>
    public bool DraftLength(double len, Point? cursor = null)
    {
        if (draft.Count == 0 || len <= 0) return false;
        XY dir;
        if ((Mods.HasFlag(ModifierKeys.Shift) || cursor == null) && DirLock() is XY d) dir = d;
        else
        {
            var c = cursor != null ? ToWorld(cursor.Value) : hover ?? draft[^1];
            var v = c - draft[^1]; if (v.Length < 1e-9) { if (DirLock() is XY d2) dir = d2; else return false; } else dir = v * (1 / v.Length);
        }
        var a = draft[^1];
        draft.Add(a + dir * (len * KAt(a)));
        LenBuf = ""; DraftChanged();
        return true;
    }

    /// <summary>Klawisz w narzędziu Tor: cyfry i przecinek – długość, Enter – wierzchołek / koniec, Backspace – cofnij, Esc – rezygnacja.</summary>
    public bool TorKey(Key k)
    {
        if (tool != UtTool.Tor) return false;
        string ch = k switch
        {
            >= Key.D0 and <= Key.D9 => ((char)('0' + (k - Key.D0))).ToString(),
            >= Key.NumPad0 and <= Key.NumPad9 => ((char)('0' + (k - Key.NumPad0))).ToString(),
            Key.OemComma or Key.OemPeriod or Key.Decimal => ",",
            _ => null,
        };
        if (ch != null && draft.Count > 0) { if (!(ch == "," && LenBuf.Contains(','))) LenBuf += ch; DraftChanged(); return true; }
        if (k == Key.Enter)
        {
            if (LenBuf.Length > 0 && double.TryParse(LenBuf.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var len)) DraftLength(len, hover != null ? ToScreen(hover.Value) : null);
            else FinishDraft();
            return true;
        }
        if (k == Key.Back)
        {
            if (LenBuf.Length > 0) LenBuf = LenBuf[..^1];
            else if (draft.Count > 0) draft.RemoveAt(draft.Count - 1);
            DraftChanged(); return true;
        }
        if (k == Key.Escape && draft.Count > 0) { CancelDraft(); return true; }
        return false;
    }

    public void FinishDraft()
    {
        if (draft.Count >= 2)
        {
            var vs = draft.Select(p => new UtVertex { X = p.X, Y = p.Y, R = NewR, L = NewL }).ToList();
            var ext = extendId; bool atStart = extendAtStart;
            CancelDraft();
            TrackDrawn?.Invoke(vs, ext, atStart);
        }
        else CancelDraft();
    }

    public void CancelDraft() { draft.Clear(); extendId = null; LenBuf = ""; DraftChanged(); }

    void DraftChanged()
    {
        InvalidateVisual();
        DraftInfo?.Invoke(draft.Count == 0 ? "" : $"Tor: {draft.Count} pkt" + (extendId != null ? " (przedłużenie toru)" : "") + (LenBuf.Length > 0 ? $" · długość: {LenBuf} m – Enter" : ""));
    }

    bool DoubleClick(Point p)
    {
        if (tool == UtTool.Tor) { FinishDraft(); return true; }
        if (tool == UtTool.Wskaz && Track(SelectedTrack) is UtTrack t)
        {
            // wstawienie wierzchołka na boku wieloboku stycznych najbliżej kursora
            var w = ToWorld(p); int bi = -1; double bd = HitPx * 2; XY bq = default;
            for (int i = 1; i < t.Vertices.Count; i++)
            {
                var a = t.Vertices[i - 1].P; var b = t.Vertices[i].P; var d = b - a; double l2 = d.X * d.X + d.Y * d.Y;
                double u = l2 > 0 ? Math.Clamp(((w.X - a.X) * d.X + (w.Y - a.Y) * d.Y) / l2, 0.02, 0.98) : 0;
                var q = a + d * u; double dist = (ToScreen(q) - p).Length;
                if (dist < bd) { bd = dist; bi = i; bq = q; }
            }
            if (bi < 0)
            {
                // dwuklik na osi toru (na łuku) – wierzchołek na boku, którego łuk dotyczy
                var r = Geometry(t); var (sKm, dist) = r.Nearest(w);
                if ((ToScreen(r.At(sKm).P) - p).Length > HitPx) return false;
                double acc = 0; Element el = r.Elements[0];
                foreach (var e in r.Elements) { if (acc + e.Len >= sKm * r.K) { el = e; break; } acc += e.Len; }
                bi = Math.Clamp(el.Vertex, 1, t.Vertices.Count - 1);
                var a = t.Vertices[bi - 1].P; var b = t.Vertices[bi].P; var dd = b - a; double l2 = dd.X * dd.X + dd.Y * dd.Y;
                bq = a + dd * Math.Clamp(((w.X - a.X) * dd.X + (w.Y - a.Y) * dd.Y) / l2, 0.02, 0.98);
            }
            if (Locked(t.Id, bi) && Locked(t.Id, bi - 1)) { DraftInfo?.Invoke("Ten odcinek odgałęzienia ustawia rozjazd – wierzchołka nie można tu dodać"); return true; }
            VertexInserted?.Invoke(t.Id, bi, bq);
            return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ zaznaczanie i wierzchołki
    (string Track, int Index)? vDrag; XY vDragTo; bool vDragMoved;

    public string HitTrack(Point s)
    {
        if (Doc == null) return null;
        var w = ToWorld(s); string best = null; double bd = HitPx;
        foreach (var t in Doc.P.Tracks)
        {
            var r = Geometry(t); if (r.Elements.Count == 0) continue;
            double d = (ToScreen(r.At(r.Nearest(w).S).P) - s).Length;
            if (d < bd) { bd = d; best = t.Id; }
        }
        return best;
    }

    void SelectAt(Point s)
    {
        if (HitTurnout(s) is string rz) { SelectTurnout(rz); return; }
        if (HitCrossing(s) is string cr) { SelectCrossing(cr); return; }
        if (Track(SelectedTrack) is UtTrack t)
            for (int i = 0; i < t.Vertices.Count; i++)
                if ((ToScreen(t.Vertices[i].P) - s).Length <= HandlePx) { Select(t.Id, i); return; }
        Select(HitTrack(s));
    }

    bool PressVertex(Point s)
    {
        if (Track(SelectedTrack) is not UtTrack t) return false;
        for (int i = 0; i < t.Vertices.Count; i++)
            if ((ToScreen(t.Vertices[i].P) - s).Length <= HandlePx)
            {
                if (Locked(t.Id, i)) { Select(t.Id, i); DraftInfo?.Invoke("Ten wierzchołek ustawia rozjazd – przesuń rozjazd (zaznacz go i przeciągnij wzdłuż toru)"); return true; }
                vDrag = (t.Id, i); vDragTo = t.Vertices[i].P; vDragMoved = false;
                Select(t.Id, i);
                return true;
            }
        return false;
    }

    void DragVertex(Point s)
    {
        if (downAt is Point d && (s - d).Length > ClickTol) vDragMoved = true;
        if (!vDragMoved) return;
        vDragTo = Snap(s, vDrag.Value.Track, vDrag.Value.Index).P;
        // wierzchołek odgałęzienia za rozjazdem – tylko wzdłuż skosu rozjazdu
        if (SkewTurnout(vDrag.Value.Track, vDrag.Value.Index) is UtTurnout ot && Rozjazdy.OnSkew(Doc.P, ot, vDragTo) is var (q, _)) vDragTo = q;
        InvalidateVisual();
    }

    void EndVertexDrag(Point s)
    {
        var (id, i) = vDrag.Value; vDrag = null;
        if (vDragMoved) VertexMoved?.Invoke(id, i, vDragTo);
        InvalidateVisual();
    }

    // ------------------------------------------------------------------ rysowanie torów
    static readonly Brush TrackInk = Frozen(Color.FromRgb(0x18, 0x18, 0x18)), TrackElec = Frozen(Color.FromRgb(0x10, 0x48, 0xc8)),
        SelGlow = Frozen(Color.FromArgb(0x90, 0xff, 0xa0, 0x10)), HandleFill = Frozen(Colors.White), HandleSel = Frozen(Color.FromRgb(0xff, 0x90, 0x00)),
        DraftInk = Frozen(Color.FromRgb(0xe0, 0x30, 0x10)), SnapInk = Frozen(Color.FromRgb(0x10, 0xa0, 0x30));
    static readonly Pen PolyPen = Dashed(Color.FromRgb(0x60, 0x60, 0x60), 1), HandlePen = FrozenPen(Color.FromRgb(0x20, 0x20, 0x20), 1.2),
        TickPenT = FrozenPen(Color.FromRgb(0x20, 0x20, 0x20), 1.2), ErrPen = FrozenPen(Color.FromRgb(0xd0, 0x18, 0x18), 2.2),
        DraftPoly = Dashed(Color.FromRgb(0xe0, 0x30, 0x10), 1.2), SnapPen = FrozenPen(Color.FromRgb(0x10, 0xa0, 0x30), 2);

    /// <summary>Szerokość osi [px], stała przy każdym przybliżeniu: główny zasadniczy 2, główny dodatkowy 1,6, boczny 1,2.</summary>
    static double AxisPx(UtTrack t) => t.Cat == UtTrack.MainZ ? 2 : t.Cat == UtTrack.MainD ? 1.6 : 1.2;

    void DrawTracks(DrawingContext dc, double dpi)
    {
        if (Doc == null) return;
        foreach (var t in Doc.P.Tracks)
        {
            var src = t;
            if (vDrag is var (vid, vi) && vid == t.Id && vDragMoved)
            {
                src = new UtTrack { Id = t.Id + "~", Cat = t.Cat, Elec = t.Elec, Number = t.Number, Vertices = t.Vertices.Select(v => v.Clone()).ToList() };
                src.Vertices[vi].X = vDragTo.X; src.Vertices[vi].Y = vDragTo.Y;
            }
            var (_, res, pts) = Cached(src);
            bool sel = t.Id == SelectedTrack;
            DrawAxis(dc, src, res, pts, sel, dpi);
        }
        if (Track(SelectedTrack) is UtTrack st) DrawTrackEdit(dc, st, dpi);
        if (tool == UtTool.Tor) DrawDraft(dc, dpi);
    }

    void DrawAxis(DrawingContext dc, UtTrack t, TrasaResult res, List<(XY P, double Az)> pts, bool sel, double dpi)
    {
        if (pts.Count < 2) return;
        // poza ekranem – pomiń
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var (p, _) in pts) { var s = ToScreen(p); minX = Math.Min(minX, s.X); maxX = Math.Max(maxX, s.X); minY = Math.Min(minY, s.Y); maxY = Math.Max(maxY, s.Y); }
        if (maxX < -20 || minX > ActualWidth + 20 || maxY < -20 || minY > ActualHeight + 20) return;
        var ink = TrackInk;
        double wpx = AxisPx(t);
        if (sel) dc.DrawGeometry(null, new Pen(SelGlow, wpx + 7) { LineJoin = PenLineJoin.Round }, Line(pts, 0));
        dc.DrawGeometry(null, new Pen(ink, wpx) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat }, Line(pts, 0));
        // błędy geometrii – czerwone kółko w wierzchołku
        foreach (var (v, _, err) in res.Notes) if (err && v < t.Vertices.Count) dc.DrawEllipse(null, ErrPen, ToScreen(t.Vertices[v].P), 9, 9);
        // numer toru: kropka i numer nad osią (jak w ISDR), w środku widocznej części toru
        if (t.Number.Length > 0 && res.Elements.Count > 0)
        {
            // z dala od rozjazdów (do 80 px), przy równych – najbliżej środka widocznej części
            double len = res.Length, bestS = -1, lo = double.MaxValue, hi = double.MinValue;
            var cand = new List<(double S, Point P)>();
            for (int i = 0; i <= 60; i++)
            {
                double sk = len * i / 60; var q = ToScreen(res.At(sk).P);
                if (q.X > 30 && q.X < ActualWidth - 30 && q.Y > 20 && q.Y < ActualHeight - 20) { lo = Math.Min(lo, sk); hi = Math.Max(hi, sk); cand.Add((sk, q)); }
            }
            var marks = TurnoutMarks();
            double bestScore = double.MinValue;
            foreach (var (sk, q) in cand)
            {
                double free = 80; foreach (var m in marks) free = Math.Min(free, (m - q).Length);
                double score = free - Math.Abs(sk - (lo + hi) / 2) / Math.Max(1e-9, hi - lo) * 10;
                if (score > bestScore) { bestScore = score; bestS = sk; }
            }
            if (bestS >= 0)
            {
                var (mp, az) = res.At(bestS);
                var ps = ToScreen(mp); var d = ScreenDir(az); var n = new Vector(d.Y, -d.X); if (n.Y > 0) n = -n;   // normalna w górę ekranu
                var ft = new FormattedText(t.Number, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, UiFont, 12, ink, dpi);
                var c = ps + n * 10;
                dc.DrawEllipse(ink, null, new Point(c.X - ft.Width / 2 - 5, c.Y), 2.2, 2.2);
                dc.DrawText(ft, new Point(c.X - ft.Width / 2 + 1, c.Y - ft.Height / 2));
            }
        }
    }

    /// <summary>Punkty rozjazdów i skrzyżowań na ekranie (PR, ŚR, KR) – numery torów stają z dala od nich.</summary>
    List<Point> TurnoutMarks()
    {
        var res = new List<Point>();
        foreach (var t in Doc.P.Turnouts) if (Rozjazdy.Geometry(Doc.P, t) is TurnoutGeo g) { res.Add(ToScreen(g.PR)); res.Add(ToScreen(g.SR)); res.Add(ToScreen(g.KRMain)); }
        foreach (var c in Doc.P.Crossings) if (Rozjazdy.CrossGeo(Doc.P, c) is var (p, _, _, _)) res.Add(ToScreen(p));
        return res;
    }

    /// <summary>Łamana osi przesunięta o d [m w układzie] w prawo (− w lewo) – na ekranie.</summary>
    StreamGeometry Line(List<(XY P, double Az)> pts, double d)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            for (int i = 0; i < pts.Count; i++)
            {
                var (p, az) = pts[i];
                var s = ToScreen(d == 0 ? p : p + Element.Right(az) * d);
                if (i == 0) c.BeginFigure(s, false, false); else c.LineTo(s, true, true);
            }
        }
        g.Freeze(); return g;
    }

    void DrawTrackEdit(DrawingContext dc, UtTrack t, double dpi)
    {
        var src = t; var res = Geometry(t);
        if (vDrag is var (vid, vi) && vid == t.Id && vDragMoved)
        {
            src = new UtTrack { Id = t.Id + "~", Vertices = t.Vertices.Select(v => v.Clone()).ToList() };
            src.Vertices[vi].X = vDragTo.X; src.Vertices[vi].Y = vDragTo.Y; res = Geometry(src);
        }
        var v = src.Vertices;
        // wielobok stycznych
        for (int i = 1; i < v.Count; i++) dc.DrawLine(PolyPen, ToScreen(v[i - 1].P), ToScreen(v[i].P));
        // granice elementów (początki i końce przejściowych i łuków)
        foreach (var e in res.Elements.Where(e => e.Kind != ElKind.Prosta))
            foreach (var (p, az) in new[] { e.At(0), e.At(e.Len) })
            {
                var sp = ToScreen(p); var d = ScreenDir(az); var n = new Vector(-d.Y, d.X) * 6;
                dc.DrawLine(TickPenT, sp - n, sp + n);
            }
        for (int i = 0; i < v.Count; i++)
        {
            var s = ToScreen(v[i].P);
            dc.DrawRectangle(i == SelectedVertex ? HandleSel : HandleFill, HandlePen, new Rect(s.X - 4.5, s.Y - 4.5, 9, 9));
            if (i > 0 && i < v.Count - 1 && v[i].R > 0)
                Label(dc, v[i].L > 0 ? $"R {v[i].R:0.##} · L {v[i].L:0.##}" : $"R {v[i].R:0.##}", s + new Vector(9, -20), Ink, dpi);
        }
    }

    void DrawDraft(DrawingContext dc, double dpi)
    {
        if (hover is XY h)
        {
            var hs = ToScreen(h);
            var sn = Snap(hs);
            if (sn.Kind != SnapKind.Brak)
            {
                var p = ToScreen(sn.P);
                if (sn.Kind == SnapKind.Tor) dc.DrawEllipse(null, SnapPen, p, 6, 6); else dc.DrawRectangle(null, SnapPen, new Rect(p.X - 6, p.Y - 6, 12, 12));
            }
        }
        if (draft.Count == 0) return;
        var pts = draft.ToList();
        if (hover is XY hv) pts.Add(DraftPoint(ToScreen(hv)));
        // podgląd z łukami (promień i przejściowa jak w nowych wierzchołkach; przy przedłużeniu – z wierzchołkami toru)
        var verts = pts.Select(p => new UtVertex { X = p.X, Y = p.Y, R = NewR, L = NewL }).ToList();
        if (extendId != null && Track(extendId) is UtTrack et)
        {
            var old = et.Vertices.Select(x => x.Clone()).ToList();
            if (extendAtStart) { old.Reverse(); }
            old[^1].R = NewR; old[^1].L = NewL;
            verts = old.Concat(verts.Skip(1)).ToList();
        }
        var res = Trasa.Compute(verts, KAt(pts[0]));
        var sp = res.Sample(0.5);
        if (sp.Count >= 2) dc.DrawGeometry(null, new Pen(DraftInk, 2) { LineJoin = PenLineJoin.Round }, Line(sp, 0));
        for (int i = 1; i < pts.Count; i++) dc.DrawLine(DraftPoly, ToScreen(pts[i - 1]), ToScreen(pts[i]));
        foreach (var p in draft) { var s = ToScreen(p); dc.DrawRectangle(DraftInk, null, new Rect(s.X - 3, s.Y - 3, 6, 6)); }
        if (pts.Count >= 2)
        {
            var a = pts[^2]; var b = pts[^1];
            string txt = LenBuf.Length > 0 ? $"{LenBuf} m ⏎" : Len3(Geo.GroundDistance(a, b)) + $" · az {Geo.Azimuth(a, b):0.0}°";
            Label(dc, txt, ToScreen(b) + new Vector(12, 8), DraftInk, dpi);
        }
    }
}

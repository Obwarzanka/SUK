using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Wyposażenie toru (etap 5, część 2): odcinki kontroli niezajętości, perony, przejazdy, kilometraż linii, pochylenia niwelety;
/// warstwy (Ig-10 § 4 ust. 4, nazwy z zał. 8) i tryb wydruku.
/// Narzędzia: Odcinek – klik na torze (nazwa kawałka między granicami); Peron – wciśnij na torze w miejscu początku krawędzi
/// i przeciągnij wzdłuż toru do końca, po stronie peronu; Przejazd – klik (droga prostopadle) albo przeciągnięcie wzdłuż osi drogi;
/// Kilometraż – wciśnij na torze i przeciągnij w stronę rosnącego km linii. Wskaż: klik zaznacza, przeciągnięcie przesuwa wzdłuż toru.
/// </summary>
public partial class UtView
{
    // ------------------------------------------------------------------ warstwy
    /// <summary>Warstwy (klucz, nazwa wg Ig-10 zał. 8 albo edytora).</summary>
    public static readonly (string Key, string Name)[] Warstwy =
    {
        ("tory", "układ torowy (osie torów, kozły)"), ("rozjazdy", "rozjazdy i skrzyżowania"), ("sygnalizator", "sygnalizator (semafory, tarcze)"),
        ("wskazniki", "wskaźniki (W4, W5, W17)"), ("wykolejnica", "wykolejnica"), ("licznik osi", "licznik osi"), ("zlacze", "złącze izolowane"),
        ("odcinki", "odcinki kontroli niezajętości"), ("peron", "peron"), ("drogi", "drogi (przejazdy)"), ("kilometraz", "kilometraż (km, hm)"),
        ("opis", "opis (pochylenia niwelety, numery)"), ("podklady", "podkłady (ortofotomapa, grafiki)"),
    };
    public readonly HashSet<string> Hidden = new();
    public bool Shown(string layer) => !Hidden.Contains(layer);
    public static string LayerOf(string devKind) => devKind switch
    {
        UtDevice.Semafor or UtDevice.TarczaOstrz or UtDevice.TarczaMan => "sygnalizator",
        UtDevice.W4 or UtDevice.W5 or UtDevice.Ukres => "wskazniki",
        UtDevice.Wykolejnica => "wykolejnica",
        UtDevice.LicznikOsi => "licznik osi",
        UtDevice.Zlacze => "zlacze",
        _ => "tory",
    };

    /// <summary>Wydruk / eksport: bez zaznaczeń, uwag, podpowiedzi, linijek, podziałki i podglądu na żywo.</summary>
    public bool PrintMode;
    Pen ErrP => PrintMode ? null : ErrPen;

    // ------------------------------------------------------------------ zaznaczenie
    /// <summary>Zaznaczony obiekt wyposażenia: rodzaj (<see cref="Kontrola"/>.K…) i id.</summary>
    public (string Kind, string Id)? SelectedObj { get; private set; }
    public void SelectObj(string kind, string id)
    {
        SelectedObj = kind != null ? (kind, id) : null;
        SelectedTrack = null; SelectedVertex = -1; SelectedTurnout = null; SelectedCrossing = null; SelectedDevice = null;
        InvalidateVisual(); SelectionChanged?.Invoke();
    }
    public event Action<string, string> ObjMenu;
    /// <summary>Obiekt przesunięty wzdłuż toru: rodzaj, id, przesunięcie km [m].</summary>
    public event Action<string, string, double> ObjMoved;
    public event Action<string, double> SectionPlace;
    public event Action<string, double, double, int> PlatformPlace;
    /// <summary>Przejazd: tor, km, kąt drogi do osi toru [°], zasięg drogi [m].</summary>
    public event Action<string, double, double, double> LevelCrossingPlace;
    public event Action<string, double, int> KmPlace;

    // ------------------------------------------------------------------ odcinki (pamięć wyniku)
    string odcSig; OdcinkiResult odcRes;
    /// <summary>Podział na odcinki dla bieżącego projektu (liczony ponownie po zmianie).</summary>
    public OdcinkiResult OdcinkiNow()
    {
        if (Doc == null) return new OdcinkiResult();
        var sig = Doc.UndoName + "|" + string.Join(";", Doc.P.Tracks.Select(Sig)) + "|" + string.Join(";", Doc.P.Devices.Where(Odcinki.IsBoundary).Select(d => $"{d.Track}:{d.S}")) +
            "|" + string.Join(";", Doc.P.Sections.Select(s => $"{s.Id}:{s.Track}:{s.S}:{s.Name}")) + "|" + string.Join(";", Doc.P.Crossings.Select(c => $"{c.TrackA}{c.SA}{c.TrackB}{c.SB}"));
        if (sig == odcSig && odcRes != null) return odcRes;
        odcSig = sig; odcRes = Odcinki.Compute(Doc.P); return odcRes;
    }

    // ------------------------------------------------------------------ narzędzia
    (string Track, double S)? objFrom;

    bool ObjToolPress(Point s)
    {
        var id = HitTrack(s); if (id == null) return false;
        objFrom = (id, Geometry(Track(id)).Nearest(ToWorld(s), 0.2).S);
        return true;
    }

    /// <summary>Kierunek wzdłuż toru i strona kursora względem punktu (+1 – w stronę rosnącego km / po prawej).</summary>
    (int Along, int Side, double AlongM, double AcrossM) DragDir(string track, double s, Point at)
    {
        var r = Geometry(Track(track)); var (p, az) = r.At(s);
        var v = ToWorld(at) - p; var d = Element.Dir(az);
        double along = v.X * d.X + v.Y * d.Y, across = d.X * v.Y - d.Y * v.X;
        return (along >= 0 ? 1 : -1, across >= 0 ? 1 : -1, along / r.K, across / r.K);
    }

    void ObjToolRelease(Point at)
    {
        var (track, s) = objFrom.Value; objFrom = null;
        var r = Geometry(Track(track));
        bool click = (ToScreen(r.At(s).P) - at).Length < 8;
        switch (tool)
        {
            case UtTool.Peron:
            {
                double s1 = Math.Clamp(r.Nearest(ToWorld(at), 0.2).S, 0, r.Length);
                if (Math.Abs(s1 - s) < 2) { DraftInfo?.Invoke("Peron: wciśnij na torze w miejscu początku krawędzi i przeciągnij wzdłuż toru do jej końca, po stronie peronu"); break; }
                var (_, side, _, _) = DragDir(track, s1, at);
                PlatformPlace?.Invoke(track, Math.Round(Math.Min(s, s1), 2), Math.Round(Math.Max(s, s1), 2), side);
                break;
            }
            case UtTool.Przejazd:
            {
                double ang = 90, reach = 15;
                if (!click)
                {
                    var (_, az) = r.At(s); var v = ToWorld(at) - r.At(s).P;
                    double a = Math.Atan2(v.Y, v.X) - az;
                    ang = Math.Round(((a * 180 / Math.PI) % 180 + 180) % 180);
                    if (ang < 1) ang = 90;
                    reach = Math.Max(10, Math.Round(Math.Sqrt(v.X * v.X + v.Y * v.Y) / r.K));
                }
                LevelCrossingPlace?.Invoke(track, Math.Round(s, 2), ang, reach);
                break;
            }
            case UtTool.Km:
            {
                int dir = click ? 1 : DragDir(track, s, at).Along;
                KmPlace?.Invoke(track, Math.Round(s, 2), dir);
                break;
            }
        }
        InvalidateVisual();
    }

    void OdcClick(Point p)
    {
        var id = HitTrack(p);
        if (id == null) { DraftInfo?.Invoke("Odcinek: kliknij na torze w kawałku między granicami (licznikami osi, złączami, końcami torów)"); return; }
        SectionPlace?.Invoke(id, Math.Round(Geometry(Track(id)).Nearest(ToWorld(p), 0.2).S, 2));
    }

    // ------------------------------------------------------------------ zaznaczanie i przesuwanie (Wskaż)
    (string Kind, string Id, double Grab, double Ref, double Delta)? objDrag;

    /// <summary>Obiekt wyposażenia pod kursorem.</summary>
    public (string Kind, string Id)? HitObj(Point s)
    {
        if (Doc == null) return null;
        var p = Doc.P;
        if (HitLine(s) is var (lk, lid)) return (lk, lid);
        if (Shown("kilometraz"))
            foreach (var k in p.KmPoints)
                if (KmMark(k) is Point q && (q - s).Length < 9) return (Kontrola.KKm, k.Id);
        if (Shown("drogi"))
            foreach (var c in p.LevelCrossings)
                if (Pt(c.Track, c.S) is Point q && (q - s).Length < 9) return (Kontrola.KPrzejazd, c.Id);
        if (Shown("odcinki"))
            foreach (var sc in p.Sections)
                if (SectionLabelAt(sc) is Rect rr && rr.Contains(s)) return (Kontrola.KOdcinek, sc.Id);
        if (Shown("peron"))
            foreach (var pl in p.Platforms)
                if (PlatformPoly(pl) is List<Point> poly && Inside(poly, s)) return (Kontrola.KPeron, pl.Id);
        return null;
    }

    static bool Inside(List<Point> poly, Point s)
    {
        bool c = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            if ((poly[i].Y > s.Y) != (poly[j].Y > s.Y) && s.X < (poly[j].X - poly[i].X) * (s.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X) c = !c;
        return c;
    }

    Point? Pt(string track, double s)
    {
        var t = Track(track); if (t == null) return null;
        var r = Geometry(t); if (r.Elements.Count == 0) return null;
        return ToScreen(r.At(Math.Clamp(s, 0, r.Length)).P);
    }

    (string Track, double S) ObjRef(string kind, string id) => kind switch
    {
        Kontrola.KPeron => Doc.P.Platforms.Where(x => x.Id == id).Select(x => (x.Track, x.S0)).FirstOrDefault(),
        Kontrola.KPrzejazd => Doc.P.LevelCrossings.Where(x => x.Id == id).Select(x => (x.Track, x.S)).FirstOrDefault(),
        Kontrola.KKm => Doc.P.KmPoints.Where(x => x.Id == id).Select(x => (x.Track, x.S)).FirstOrDefault(),
        Kontrola.KOdcinek => Doc.P.Sections.Where(x => x.Id == id).Select(x => (x.Track, x.S)).FirstOrDefault(),
        _ => (null, 0),
    };

    bool PressObj(Point s)
    {
        if (HitObj(s) is not var (kind, id)) return false;
        if (SelectedObj != (kind, id)) SelectObj(kind, id);
        var (track, rs) = ObjRef(kind, id); var t = Track(track); if (t == null) return true;
        objDrag = (kind, id, Geometry(t).Nearest(ToWorld(s), 0.2).S - rs, rs, 0);
        return true;
    }

    void DragObj(Point s)
    {
        var (kind, id, grab, rs, _) = objDrag.Value;
        var (track, _) = ObjRef(kind, id); var t = Track(track); if (t == null) return;
        var r = Geometry(t);
        double ns = Math.Clamp(r.Nearest(ToWorld(s), 0.2).S - grab, 0, r.Length);
        objDrag = (kind, id, grab, rs, Math.Round(ns - rs, 2));
        DraftInfo?.Invoke($"przesunięcie {objDrag.Value.Delta.ToString("+0.00;-0.00", CultureInfo.InvariantCulture)} m wzdłuż toru");
        InvalidateVisual();
    }

    void EndObjDrag()
    {
        var (kind, id, _, _, delta) = objDrag.Value; objDrag = null;
        if (dragging && Math.Abs(delta) > 1e-6) ObjMoved?.Invoke(kind, id, delta);
        InvalidateVisual();
    }

    double DragDelta(string kind, string id) => objDrag is var (k, i, _, _, d) && k == kind && i == id && dragging ? d : 0;

    // ------------------------------------------------------------------ rysowanie: odcinki
    static readonly Color[] SecColors =
    {
        Color.FromRgb(0x2a, 0x9d, 0x8f), Color.FromRgb(0xe7, 0x6f, 0x51), Color.FromRgb(0x5e, 0x60, 0xce), Color.FromRgb(0xd6, 0x9e, 0x0b),
        Color.FromRgb(0x9b, 0x5d, 0xe5), Color.FromRgb(0x2b, 0x93, 0x48), Color.FromRgb(0xc2, 0x41, 0x8c), Color.FromRgb(0x1f, 0x7a, 0xb8),
    };
    static readonly Pen NoSecPen = new Pen(new SolidColorBrush(Color.FromArgb(0xb0, 0xd0, 0x18, 0x18)), 5) { DashStyle = new DashStyle(new double[] { 1.2, 1 }, 0) };

    /// <summary>Kolor odcinka (wg pierwszej litery id – stały przy zmianach innych odcinków).</summary>
    static Color SecColor(UtSection s) => SecColors[Math.Abs(s.Id.GetHashCode()) % SecColors.Length];

    void DrawSections(DrawingContext dc, double dpi)
    {
        if (Doc == null || !Shown("odcinki") || (Doc.P.Sections.Count == 0 && tool != UtTool.Odcinek)) return;
        var o = OdcinkiNow();
        string selSec = SelectedObj is (Kontrola.KOdcinek, var sid) ? sid : null;
        int selComp = selSec != null && o.CompOf.TryGetValue(selSec, out var sc0) ? sc0 : -1;
        int hoverComp = -1;
        if (tool == UtTool.Odcinek && hover is XY h && HitTrack(ToScreen(h)) is string ht) hoverComp = o.At(ht, Geometry(Track(ht)).Nearest(h, 0.5).S);
        foreach (var k in o.Pieces)
        {
            var t = Track(k.Track); if (t == null) continue;
            var r = Geometry(t); if (r.Elements.Count == 0 || k.Len < 1e-3) continue;
            var names = o.Names.TryGetValue(k.Comp, out var nn) ? nn : new();
            Pen pen;
            if (names.Count == 0) { if (Doc.P.Sections.Count == 0 && k.Comp != hoverComp) continue; pen = NoSecPen; }
            else
            {
                var c = SecColor(names[0]); byte a = (byte)(k.Comp == selComp ? 0xd0 : 0x70);
                pen = new Pen(new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B)), k.Comp == selComp ? 9 : 6) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat, LineJoin = PenLineJoin.Round };
                if (names.Count > 1) pen.DashStyle = new DashStyle(new double[] { 2, 1 }, 0);
            }
            if (k.Comp == hoverComp && names.Count == 0) pen = new Pen(new SolidColorBrush(Color.FromArgb(0x90, 0x10, 0x60, 0xd0)), 8);
            var pts = new List<(XY P, double Az)>();
            for (double s = k.S0; s < k.S1; s += Math.Max(0.5, mpp * 2)) pts.Add(r.At(s));
            pts.Add(r.At(k.S1));
            dc.DrawGeometry(null, pen, Line(pts, 0));
            // granice kawałka – krótkie kreski w poprzek
            if (!PrintMode && names.Count > 0)
                foreach (var se in new[] { k.S0, k.S1 })
                {
                    var (bp, baz) = r.At(se); var sp = ToScreen(bp); var d = ScreenDir(baz); var n = new Vector(-d.Y, d.X) * 5;
                    dc.DrawLine(new Pen(pen.Brush, 1), sp - n, sp + n);
                }
        }
        // nazwy odcinków (pod torem, w miejscu nazwy)
        foreach (var s in Doc.P.Sections)
        {
            if (SectionLabelAt(s) is not Rect rr) continue;
            var c = SecColor(s);
            var ft = new FormattedText(s.Name.Length > 0 ? s.Name : "?", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, UiFont, 11.5, new SolidColorBrush(Color.FromRgb((byte)(c.R * 0.7), (byte)(c.G * 0.7), (byte)(c.B * 0.7))), dpi);
            dc.DrawRoundedRectangle(s.Id == selSec ? new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0xf0, 0xc0)) : Halo, new Pen(new SolidColorBrush(c), s.Id == selSec ? 1.6 : 0.8), rr, 3, 3);
            dc.DrawText(ft, new Point(rr.X + 4, rr.Y + 1));
        }
        if (tool == UtTool.Odcinek && hoverComp >= 0 && hover is XY hh)
        {
            var nn = o.Names.TryGetValue(hoverComp, out var x) ? x : new();
            Label(dc, nn.Count == 0 ? $"bez odcinka – {o.Length(hoverComp):0.#} m – klik: nadaj nazwę" : $"odcinek {string.Join(", ", nn.Select(q => q.Name))} – {o.Length(hoverComp):0.#} m", ToScreen(hh) + new Vector(12, 10), Blue, dpi);
        }
    }

    /// <summary>Prostokąt nazwy odcinka na ekranie (pod osią toru w miejscu nazwy).</summary>
    Rect? SectionLabelAt(UtSection s)
    {
        if (Pt(s.Track, s.S) is not Point p) return null;
        var r = Geometry(Track(s.Track)); var d = ScreenDir(r.At(Math.Clamp(s.S, 0, r.Length)).Az); var n = new Vector(-d.Y, d.X); if (n.Y < 0) n = -n;   // w dół ekranu
        double w = 8 + 6.6 * Math.Max(1, s.Name.Length), hgt = 17;
        var c = p + n * 16;
        return new Rect(c.X - w / 2, c.Y - hgt / 2, w, hgt);
    }

    // ------------------------------------------------------------------ rysowanie: perony
    /// <summary>Obrys peronu na ekranie: krawędź przy torze (od S0 do S1), potem druga strona (wstecz).</summary>
    List<Point> PlatformPoly(UtPlatform pl, double delta = 0)
    {
        var t = Track(pl.Track); if (t == null) return null;
        var r = Geometry(t); if (r.Elements.Count == 0) return null;
        double s0 = Math.Clamp(Math.Min(pl.S0, pl.S1) + delta, 0, r.Length), s1 = Math.Clamp(Math.Max(pl.S0, pl.S1) + delta, 0, r.Length);
        var a = new List<Point>(); var b = new List<Point>();
        TrasaResult r2 = pl.Track2 != null && Track(pl.Track2) is UtTrack t2 ? Geometry(t2) : null;
        int n = Math.Max(2, (int)Math.Ceiling((s1 - s0) / 2));
        for (int i = 0; i <= n; i++)
        {
            double s = s0 + (s1 - s0) * i / n; var (p, az) = r.At(s); var rt = Element.Right(az) * (pl.Side * r.K);
            a.Add(ToScreen(p + rt * pl.Offset));
            if (r2 != null)
            {
                // druga krawędź: rzut na tor 2, odsunięta o Offset w stronę toru 1
                var (s2, _) = r2.Nearest(p, 0.5); var q = r2.At(s2).P; var dv = p - q; double l = Math.Sqrt(dv.X * dv.X + dv.Y * dv.Y);
                b.Add(ToScreen(l > 1e-6 ? q + dv * (pl.Offset * r.K / l) : p + rt * (pl.Offset + pl.Width)));
            }
            else b.Add(ToScreen(p + rt * (pl.Offset + pl.Width)));
        }
        b.Reverse(); a.AddRange(b);
        return a;
    }

    void DrawPlatforms(DrawingContext dc, double dpi)
    {
        if (Doc == null || !Shown("peron")) return;
        double k = PxPerMm;
        foreach (var pl in Doc.P.Platforms)
        {
            var poly = PlatformPoly(pl, DragDelta(Kontrola.KPeron, pl.Id)); if (poly == null) continue;
            bool sel = SelectedObj is (Kontrola.KPeron, var id) && id == pl.Id;
            int half = poly.Count / 2;
            var g = new StreamGeometry();
            using (var c = g.Open()) { c.BeginFigure(poly[0], true, true); for (int i = 1; i < poly.Count; i++) c.LineTo(poly[i], true, true); }
            g.Freeze();
            dc.DrawGeometry(sel ? new SolidColorBrush(Color.FromArgb(0x50, 0xff, 0xa0, 0x10)) : Brushes.White, new Pen(DevInk, Math.Max(0.8, 0.18 * k)), g);
            // krawędź peronowa – podwójna linia (Ig-10 s. 52: odstęp a = 0,7 mm)
            void Edge(List<Point> e)
            {
                if (e.Count < 2) return;
                var eg = new StreamGeometry();
                using (var c = eg.Open()) { c.BeginFigure(e[0], false, false); for (int i = 1; i < e.Count; i++) c.LineTo(e[i], true, true); }
                eg.Freeze();
                dc.DrawGeometry(null, new Pen(DevInk, Math.Max(1.4, 0.7 * k + 0.36 * k)), eg);
                dc.DrawGeometry(null, new Pen(Brushes.White, Math.Max(0.4, 0.7 * k - 0.18 * k)), eg);
            }
            Edge(poly.Take(half).ToList());
            if (pl.Track2 != null) Edge(poly.Skip(half).ToList());
            if (LabelsOn || PrintMode)
            {
                var mid = new Point((poly[half / 2].X + poly[poly.Count - 1 - half / 2].X) / 2, (poly[half / 2].Y + poly[poly.Count - 1 - half / 2].Y) / 2);
                var ft = new FormattedText($"per. {pl.Name}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Arial"), FontPx(k) * 0.85, DevInk, dpi);
                dc.DrawText(ft, new Point(mid.X - ft.Width / 2, mid.Y - ft.Height / 2));
            }
        }
        if (tool == UtTool.Peron && objFrom is var (tr, s0) && hover is XY h && Track(tr) is UtTrack t)
        {
            var r = Geometry(t); double s1 = Math.Clamp(r.Nearest(h, 0.2).S, 0, r.Length);
            var (_, side, _, _) = DragDir(tr, s1, ToScreen(h));
            var gp = PlatformPoly(new UtPlatform { Track = tr, S0 = Math.Min(s0, s1), S1 = Math.Max(s0, s1), Side = side });
            if (gp != null) { for (int i = 1; i < gp.Count; i++) dc.DrawLine(new Pen(Red, 1.2), gp[i - 1], gp[i]); }
            Label(dc, $"peron {Math.Abs(s1 - s0):0.#} m po {(side > 0 ? "prawej" : "lewej")}", ToScreen(h) + new Vector(12, 10), Red, dpi);
        }
    }

    // ------------------------------------------------------------------ rysowanie: przejazdy
    /// <summary>Oś drogi przejazdu: środek (na osi toru) i kierunek [rad].</summary>
    (XY P, double Az, double K)? CrossingAxis(UtLevelCrossing c, double delta = 0)
    {
        var t = Track(c.Track); if (t == null) return null;
        var r = Geometry(t); if (r.Elements.Count == 0) return null;
        var (p, az) = r.At(Math.Clamp(c.S + delta, 0, r.Length));
        return (p, az + c.Angle * Math.PI / 180, r.K);
    }

    void DrawLevelCrossings(DrawingContext dc, double dpi)
    {
        if (Doc == null || !Shown("drogi")) return;
        double k = PxPerMm;
        var dash = new DashStyle(new double[] { 2, 1 }, 0);       // Ig-10 s. 57: drogi – kreska 2 mm, odstęp 1 mm (w grubościach pióra)
        foreach (var c in Doc.P.LevelCrossings)
        {
            if (CrossingAxis(c, DragDelta(Kontrola.KPrzejazd, c.Id)) is not var (p, az, kk)) continue;
            bool sel = SelectedObj is (Kontrola.KPrzejazd, var id) && id == c.Id;
            var d = Element.Dir(az); var n = Element.Right(az);
            double w = c.Width / 2 * kk, l = c.Reach * kk;
            double pw = Math.Max(1, 0.18 * k * 1.4);
            var pen = new Pen(sel ? Blue : DevInk, pw) { DashStyle = new DashStyle(new double[] { 2 * k / pw, 1 * k / pw }, 0) };
            if (k < 1.5) pen.DashStyle = dash;
            foreach (var sgn in new[] { -1.0, 1.0 })
                dc.DrawLine(pen, ToScreen(p - d * l + n * (sgn * w)), ToScreen(p + d * l + n * (sgn * w)));
            if (sel) dc.DrawEllipse(null, new Pen(Blue, 1.5), ToScreen(p), 7, 7);
            // rogatki (Ig-10 s. 53): belka w poprzek drogi na jej wylotach (połowa szerokości drogi, po prawej stronie wjazdu)
            if (!string.IsNullOrEmpty(c.Barriers))
            {
                var bar = new Pen(DevInk, Math.Max(2, 1 * k)) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
                foreach (var sgn in new[] { -1.0, 1.0 })
                {
                    var e = p + d * (sgn * l * 0.85);
                    dc.DrawLine(bar, ToScreen(e + n * (sgn * w)), ToScreen(e));
                    if (c.Barriers == UtLevelCrossing.RogatkiElektr) dc.DrawEllipse(Brushes.White, new Pen(DevInk, 1), ToScreen(e + n * (sgn * w)), Math.Max(2, 0.5 * k), Math.Max(2, 0.5 * k));
                }
            }
            if (LabelsOn || PrintMode)
            {
                var km = Kilometraz.Text(Doc.P, c.Track, c.S);
                var txt = UtLevelCrossing.CatName(c.Cat) + (c.Name.Length > 0 ? " " + c.Name : "") + (km.Length > 0 ? " " + km : "");
                var ft = new FormattedText(txt, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Arial"), FontPx(k) * 0.85, sel ? Blue : DevInk, dpi);
                // na końcu drogi, który na ekranie jest wyżej
                var e1 = ToScreen(p + d * l); var e2 = ToScreen(p - d * l); var top = e1.Y < e2.Y ? e1 : e2;
                dc.DrawText(ft, new Point(top.X - ft.Width / 2, top.Y - ft.Height - 2));
            }
        }
        if (tool == UtTool.Przejazd && objFrom is var (tr, s0) && hover is XY h && Track(tr) is UtTrack t)
        {
            var r = Geometry(t); var (pp, az) = r.At(s0); var v = h - pp; double len = Math.Sqrt(v.X * v.X + v.Y * v.Y);
            double a = len * 1 / mpp < 8 ? az + Math.PI / 2 : Math.Atan2(v.Y, v.X);
            double l = Math.Max(10 * r.K, len);
            dc.DrawLine(new Pen(Red, 1.5), ToScreen(pp - Element.Dir(a) * l), ToScreen(pp + Element.Dir(a) * l));
            Label(dc, $"przejazd, kąt {((a - az) * 180 / Math.PI % 180 + 180) % 180:0}°", ToScreen(h) + new Vector(12, 10), Red, dpi);
        }
    }

    // ------------------------------------------------------------------ rysowanie: kilometraż
    Point? KmMark(UtKmPoint k)
    {
        if (Pt(k.Track, k.S + DragDelta(Kontrola.KKm, k.Id)) is not Point p) return null;
        var r = Geometry(Track(k.Track)); var d = ScreenDir(r.At(Math.Clamp(k.S, 0, r.Length)).Az); var n = new Vector(-d.Y, d.X); if (n.Y > 0) n = -n;
        return p + n * 14;
    }

    void DrawKm(DrawingContext dc, double dpi)
    {
        if (Doc == null || !Shown("kilometraz")) return;
        double k = PxPerMm; var p = Doc.P;
        var pen = new Pen(DevInk, Math.Max(0.8, 0.18 * k * 1.3));
        // wskaźniki hektometrowe i kilometrowe (Ig-10 s. 56, niestabilizowane): kreska w poprzek toru i km z jedną cyfrą po kropce
        foreach (var tid in p.KmPoints.Select(x => x.Track).Distinct())
        {
            var t = Track(tid); if (t == null) continue; var r = Geometry(t); if (r.Elements.Count == 0) continue;
            foreach (var line in Kilometraz.Lines(p, tid))
            {
                // km na początku i końcu toru – kolejne pełne hektometry (co kilometr, gdy hektometry za gęsto)
                double? k0 = Kilometraz.OnTrack(p, tid, line, 0), k1 = Kilometraz.OnTrack(p, tid, line, r.Length);
                if (k0 is not double a || k1 is not double b) continue;
                double step = 100 / mpp < 18 ? 1000 : 100;
                if (1000 / mpp < 18) continue;
                for (double km = Math.Ceiling(Math.Min(a, b) / step) * step; km <= Math.Max(a, b); km += step)
                {
                    if (SOfKm(p, tid, line, km, r.Length) is not double s) continue;
                    var (pt, az) = r.At(s); var sp = ToScreen(pt); var d = ScreenDir(az); var n = new Vector(-d.Y, d.X); if (n.Y > 0) n = -n;
                    if (sp.X < -50 || sp.Y < -50 || sp.X > ActualWidth + 50 || sp.Y > ActualHeight + 50) continue;
                    bool whole = Math.Abs(km / 1000 - Math.Round(km / 1000)) < 1e-6;
                    dc.DrawLine(whole ? new Pen(DevInk, pen.Thickness * 1.6) : pen, sp - n * (1.5 * k), sp + n * (1.5 * k));
                    if (LabelsOn || PrintMode)
                    {
                        var ft = new FormattedText((km / 1000).ToString("0.0", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Arial"), Math.Max(8, FontPx(k) * (whole ? 0.85 : 0.7)), DevInk, dpi);
                        var c = sp + n * (1.5 * k + 2);
                        dc.DrawText(ft, new Point(c.X + 2, c.Y - ft.Height));
                    }
                }
            }
        }
        // punkty kilometrażu (projektanta): chorągiewka z km i numerem linii
        foreach (var kp in p.KmPoints)
        {
            if (Pt(kp.Track, kp.S + DragDelta(Kontrola.KKm, kp.Id)) is not Point sp || KmMark(kp) is not Point m) continue;
            bool sel = SelectedObj is (Kontrola.KKm, var id) && id == kp.Id;
            var ink = sel ? Blue : Magenta;
            if (PrintMode) continue;
            dc.DrawLine(new Pen(ink, 1.2), sp, m);
            var r = Geometry(Track(kp.Track)); var d = ScreenDir(r.At(Math.Clamp(kp.S, 0, r.Length)).Az) * kp.Dir;
            dc.DrawGeometry(ink, null, Tri(m + d * 8, m + new Vector(-d.Y, d.X) * 4, m - new Vector(-d.Y, d.X) * 4));
            Label(dc, $"km {Kilometraz.Fmt(kp.Km)}" + (kp.Line.Length > 0 ? $" l. {kp.Line}" : ""), m + new Vector(6, -20), ink, dpi);
        }
        if (tool == UtTool.Km && hover is XY h)
        {
            var hs = ToScreen(h); string tr = objFrom?.Track ?? HitTrack(hs);
            if (tr != null)
            {
                double s = objFrom?.S ?? Geometry(Track(tr)).Nearest(h, 0.2).S;
                int dir = objFrom != null && (ToScreen(Geometry(Track(tr)).At(s).P) - hs).Length >= 8 ? DragDir(tr, s, hs).Along : 1;
                var (pt, az) = Geometry(Track(tr)).At(s); var sp = ToScreen(pt); var dd = ScreenDir(az) * dir;
                dc.DrawLine(new Pen(Red, 1.5), sp, sp + dd * 30);
                dc.DrawGeometry(Red, null, Tri(sp + dd * 36, sp + dd * 28 + new Vector(-dd.Y, dd.X) * 4, sp + dd * 28 - new Vector(-dd.Y, dd.X) * 4));
                var cur = Kilometraz.Text(Doc.P, tr, s);
                Label(dc, (objFrom == null ? "wciśnij i przeciągnij w stronę rosnącego km linii" : "km linii rośnie w tę stronę") + (cur.Length > 0 ? $" (teraz {cur})" : ""), hs + new Vector(12, 10), Red, dpi);
            }
        }
    }

    /// <summary>km toru, w którym kilometraż linii wynosi <paramref name="km"/> (null – poza torem).</summary>
    static double? SOfKm(UtProject p, string track, string line, double km, double len)
    {
        // kilometraż na torze jest odcinkami liniowy – węzły: 0, punkty, koniec
        var nodes = new List<double> { 0 }; nodes.AddRange(p.KmPoints.Where(x => x.Track == track && x.Line == line).Select(x => x.S).Where(s => s > 0 && s < len).OrderBy(s => s)); nodes.Add(len);
        for (int i = 0; i + 1 < nodes.Count; i++)
        {
            double a = Kilometraz.OnTrack(p, track, line, nodes[i]) ?? 0, b = Kilometraz.OnTrack(p, track, line, nodes[i + 1]) ?? 0;
            if ((km - a) * (km - b) <= 0 && Math.Abs(b - a) > 1e-9) return nodes[i] + (km - a) / (b - a) * (nodes[i + 1] - nodes[i]);
        }
        return null;
    }

    // ------------------------------------------------------------------ rysowanie: pochylenia niwelety
    /// <summary>Ig-10 s. 58: w załomie kreska w poprzek toru, nad nią pochylenie [‰] i długość [m] odcinków po obu stronach.</summary>
    void DrawGrades(DrawingContext dc, double dpi)
    {
        if (Doc == null || !Shown("opis") || (!LabelsOn && !PrintMode)) return;
        double k = PxPerMm;
        foreach (var t in Doc.P.Tracks)
        {
            var v = t.Profile; if (v == null || v.Count < 2) continue;
            var r = Geometry(t); if (r.Elements.Count == 0) continue;
            var secs = Profil.Sections(v);
            for (int i = 0; i < v.Count; i++)
            {
                var (pt, az) = r.At(Math.Clamp(v[i].S, 0, r.Length)); var sp = ToScreen(pt);
                if (sp.X < -80 || sp.Y < -80 || sp.X > ActualWidth + 80 || sp.Y > ActualHeight + 80) continue;
                var d = ScreenDir(az); var n = new Vector(-d.Y, d.X); if (n.Y > 0) n = -n;
                double h = 6 * k;
                // po stronie, gdzie dalej do sąsiedniego toru (znak nie wchodzi na inny tor)
                if (Clearance(sp + n * h, t.Id) < Clearance(sp - n * h, t.Id)) n = -n;
                dc.DrawLine(new Pen(DevInk, Math.Max(0.8, 0.18 * k * 1.3)), sp, sp + n * h);
                dc.DrawEllipse(DevInk, null, sp + n * h, Math.Max(1.2, 0.5 * k), Math.Max(1.2, 0.5 * k));
                double fs = Math.Max(8, FontPx(k) * 0.7);
                void Txt(string s, double side, double row)
                {
                    var ft = new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Arial"), fs, DevInk, dpi);
                    var c = sp + n * (h - row * (ft.Height + 1));
                    dc.DrawText(ft, new Point(side < 0 ? c.X - ft.Width - 3 : c.X + 3, c.Y - ft.Height));
                }
                bool Room(int j) => j >= 0 && j < secs.Count && (ToScreen(r.At(Math.Clamp(secs[j].S1, 0, r.Length)).P) - ToScreen(r.At(Math.Clamp(secs[j].S0, 0, r.Length)).P)).Length > 80;
                if (i > 0 && Room(i - 1)) { Txt(secs[i - 1].Promil.ToString("0.0", CultureInfo.InvariantCulture), -1, 0); Txt(secs[i - 1].Len.ToString("0", CultureInfo.InvariantCulture), -1, -1); }
                if (i < secs.Count && Room(i)) { Txt(secs[i].Promil.ToString("0.0", CultureInfo.InvariantCulture), 1, 0); Txt(secs[i].Len.ToString("0", CultureInfo.InvariantCulture), 1, -1); }
            }
        }
    }

    /// <summary>Odległość punktu ekranu od najbliższego innego toru [px].</summary>
    double Clearance(Point q, string self)
    {
        double best = double.MaxValue; var w = ToWorld(q);
        foreach (var t in Doc.P.Tracks)
        {
            if (t.Id == self) continue;
            var r = Geometry(t); if (r.Elements.Count == 0) continue;
            best = Math.Min(best, (ToScreen(r.At(r.Nearest(w, 1).S).P) - q).Length);
        }
        return best;
    }

    /// <summary>Najbliższy tor pod kursorem (świat): id i km; null – żaden w promieniu kilku px.</summary>
    public (string Track, double S)? NearTrack(XY w)
    {
        var id = HitTrack(ToScreen(w)); if (id == null) return null;
        return (id, Geometry(Track(id)).Nearest(w, 0.2).S);
    }
}

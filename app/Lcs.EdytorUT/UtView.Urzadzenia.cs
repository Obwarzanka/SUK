using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Urządzenia na torze (Ig-10 zał. 8): narzędzie Urządzenie – wciśnij na torze w miejscu urządzenia i przeciągnij w stronę jazdy,
/// której dotyczy, po stronie, po której stoi (urządzenia bez kierunku – klik); kozioł przyciąga się do końca toru. We Wskaż: klik
/// zaznacza, przeciągnięcie przesuwa wzdłuż toru, PPM – menu. Znaki w mm arkusza (jak rozjazdy), komory w stronę jazdy (Ig-10 „A ⊢○⊘”).
/// </summary>
public partial class UtView
{
    /// <summary>Rodzaj wstawianego urządzenia.</summary>
    public string DevKind = UtDevice.Semafor;
    public string SelectedDevice { get; private set; }
    /// <summary>Urządzenie do wstawienia: tor, km, kierunek działania, strona.</summary>
    public event Action<string, double, int, int> DevicePlace;
    public event Action<string, double> DeviceMoved;
    public event Action<string> DeviceMenu;

    public void SelectDevice(string id)
    {
        SelectedDevice = id; SelectedTurnout = null; SelectedCrossing = null; SelectedTrack = null; SelectedVertex = -1;
        InvalidateVisual(); SelectionChanged?.Invoke();
    }

    UtDevice Dev(string id) => Doc?.P.Devices.FirstOrDefault(x => x.Id == id);

    // ------------------------------------------------------------------ narzędzie
    (string Track, double S)? devFrom;

    bool DevPress(Point s)
    {
        var id = HitTrack(s); if (id == null) return false;
        devFrom = (id, Geometry(Track(id)).Nearest(ToWorld(s), 0.2).S);
        return true;
    }

    /// <summary>Kilometraż, kierunek i strona z wciśnięcia i puszczenia (kozioł – na bliższym końcu toru, do 10 m).</summary>
    (double S, int Dir, int Side) DevFromDrag(string track, double s, Point at)
    {
        var r = Geometry(Track(track));
        if (DevKind == UtDevice.Koziol)
        {
            bool end = s > r.Length / 2; double se = end ? r.Length : 0;
            return (Math.Abs(s - se) <= 10 ? se : s, end ? 1 : -1, 1);
        }
        var (p, az) = r.At(s);
        var pa = ToScreen(p);
        if ((at - pa).Length < 8 || !UtDevice.KindInfo(DevKind).Directed) return (s, 1, 1);
        var v = ToWorld(at) - p; var d = Element.Dir(az);
        double along = v.X * d.X + v.Y * d.Y, across = d.X * v.Y - d.Y * v.X;
        int dir = along >= 0 ? 1 : -1;
        int side = Math.Sign(across) * dir; if (side == 0) side = 1;
        return (s, dir, side);
    }

    void DevRelease(Point s)
    {
        var (track, km) = devFrom.Value; devFrom = null;
        var (sk, dir, side) = DevFromDrag(track, km, s);
        DevicePlace?.Invoke(track, sk, dir, side);
        InvalidateVisual();
    }

    // ------------------------------------------------------------------ zaznaczanie i przesuwanie
    (string Id, double Grab, double S)? devDrag;

    string HitDevice(Point s)
    {
        if (Doc == null) return null;
        string best = null; double bd = 10;
        foreach (var d in Doc.P.Devices)
        {
            if (Urzadzenia.Pos(Doc.P, d) is not var (p, az, _)) continue;
            var b = ToScreen(p); var dd = ScreenDir(az); var n = new Vector(-dd.Y, dd.X) * d.Side; double k = PxPerMm;
            foreach (var q in new[] { b, b + n * (2.5 * k), b + n * (2.5 * k) + dd * (4 * k) })
            { double dist = (q - s).Length; if (dist < bd) { bd = dist; best = d.Id; } }
        }
        return best;
    }

    bool PressDevice(Point s)
    {
        if (HitDevice(s) is not string id) return false;
        var d = Dev(id);
        if (SelectedDevice != id) SelectDevice(id);
        devDrag = (id, Geometry(Track(d.Track)).Nearest(ToWorld(s), 0.2).S - d.S, d.S);
        return true;
    }

    void DragDevice(Point s)
    {
        var (id, grab, _) = devDrag.Value; var d = Dev(id); if (d == null) return;
        var r = Geometry(Track(d.Track));
        devDrag = (id, grab, Math.Round(Math.Clamp(r.Nearest(ToWorld(s), 0.2).S - grab, 0, r.Length), 2));
        InvalidateVisual();
    }

    void EndDeviceDrag()
    {
        var (id, _, ns) = devDrag.Value; devDrag = null;
        if (dragging) DeviceMoved?.Invoke(id, ns);
        InvalidateVisual();
    }

    // ------------------------------------------------------------------ rysowanie
    static readonly Pen DevPen = FrozenPen(Color.FromRgb(0x10, 0x10, 0x10), 1.2), DevThick = FrozenPen(Color.FromRgb(0x10, 0x10, 0x10), 2.2);
    static readonly Brush DevInk = Frozen(Color.FromRgb(0x10, 0x10, 0x10));

    /// <summary>Px na mm dla znaków urządzeń – skala arkusza (jak rozjazdy i tory).</summary>
    double DevK => PxPerMm;

    void DrawDevices(DrawingContext dc, double dpi)
    {
        if (Doc == null) return;
        foreach (var d0 in Doc.P.Devices)
        {
            var d = d0;
            if (devDrag is var (did, _, ds) && did == d0.Id) d = new UtDevice { Id = d0.Id, Kind = d0.Kind, Name = d0.Name, Track = d0.Track, S = ds, Dir = d0.Dir, Side = d0.Side, Heads = d0.Heads, Dwarf = d0.Dwarf };
            if (Urzadzenia.Pos(Doc.P, d) is not var (p, az, _)) continue;
            var b = ToScreen(p);
            if (b.X < -100 || b.Y < -100 || b.X > ActualWidth + 100 || b.Y > ActualHeight + 100) continue;
            if (d.Id == SelectedDevice) { double rs = Math.Clamp(1.5 * PxPerMm, 6, 12); dc.DrawEllipse(RzSel, null, b, rs, rs); }
            DrawDevice(dc, d, b, ScreenDir(az), DevK, DevInk, dpi, LabelsOn);
            var notes = Urzadzenia.Check(Doc.P, d0);
            if (notes.Count > 0) { dc.DrawEllipse(null, ErrPen, b, 9, 9); ErrLabel(dc, (d.Name.Length > 0 ? d.Name + ": " : "") + string.Join("; ", notes), b, dpi); }
        }
        // podgląd wstawiania
        if (tool == UtTool.Urzadzenie && hover is XY h && Doc != null)
        {
            var hs = ToScreen(h);
            string track = devFrom?.Track ?? HitTrack(hs);
            if (track != null)
            {
                double km = devFrom?.S ?? Geometry(Track(track)).Nearest(h, 0.2).S;
                var (sk, dir, side) = DevFromDrag(track, km, hs);
                var g = new UtDevice { Kind = DevKind, Name = Urzadzenia.NextName(Doc.P, DevKind), Track = track, S = sk, Dir = dir, Side = side };
                if (Urzadzenia.Pos(Doc.P, g) is var (gp, gaz, _))
                    DrawDevice(dc, g, ToScreen(gp), ScreenDir(gaz), DevK, Red, dpi, true);
                if (devFrom == null && UtDevice.KindInfo(DevKind).Directed && DevKind != UtDevice.Koziol)
                    Label(dc, "wciśnij i przeciągnij w stronę jazdy, po stronie urządzenia", hs + new Vector(12, 10), Red, dpi);
            }
        }
    }

    /// <summary>
    /// Znak urządzenia w punkcie <paramref name="b"/> na osi toru: x – w kierunku działania <paramref name="d"/>, y – w stronę urządzenia
    /// (Side), w mm arkusza × <paramref name="k"/>. Komory sygnalizatora od masztu w stronę jazdy (+x), nazwa za stopą (−x).
    /// </summary>
    static void DrawDevice(DrawingContext dc, UtDevice dv, Point b, Vector d, double k, Brush ink, double dpi, bool labels)
    {
        var n = new Vector(-d.Y, d.X) * dv.Side;
        Point P(double x, double y) => b + d * (x * k) + n * (y * k);
        // grubości wg Ig-10: linie znaku 0,18 mm, wyróżnienie (komora świecąca, belka) 0,45 mm
        var pen = new Pen(ink, Math.Max(1, 0.18 * k)); var thick = new Pen(ink, Math.Max(1.6, 0.45 * k));
        void L(double x1, double y1, double x2, double y2, Pen pp = null) => dc.DrawLine(pp ?? pen, P(x1, y1), P(x2, y2));
        void C(double x, double y, double r, bool fill = false, bool cross = false)
        {
            dc.DrawEllipse(fill ? ink : Brushes.White, pen, P(x, y), r * k, r * k);
            if (cross) L(x - r * 0.7, y + r * 0.7, x + r * 0.7, y - r * 0.7);
        }
        void T(string s, double x, double y, double away = 0)
        {
            if (string.IsNullOrEmpty(s) || !labels) return;
            var ft = new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Arial"), FontPx(k), ink, dpi);
            // away ±1: napis odsunięty od punktu w kierunku ±x znaku o połowę swojej szerokości (nie wchodzi na stopę)
            var c = P(x, y) + d * (away * (ft.Width / 2 + 2));
            dc.DrawText(ft, new Point(c.X - ft.Width / 2, c.Y - ft.Height / 2));
        }
        const double Y = 2.4, R = 1;                              // oś znaku sygnalizatora od osi toru, promień komory (Ø 2 mm) [mm]
        switch (dv.Kind)
        {
            case UtDevice.Semafor:
            case UtDevice.TarczaOstrz:
            case UtDevice.TarczaMan:
            {
                // BSK ISDR „Oznaczenia urządzeń srk” ss3–ss9 (i Ig-10 zał. 8 s. 27): nazwa, stopa (wysokość komory), maszt ok. 2,5 komory
                // (z wysięgnikiem – z odsadzeniem w stronę toru; karzełkowy – bez), W24 na maszcie, pasy świetlne, W21, komory od masztu
                // w stronę jazdy. Kolory: C – kreska w poprzek masztu, Z – wzdłuż, P – ukośna, B – pusta, N – krzyż; świecąca w stanie
                // zasadniczym – gruba kreska; mogąca pulsować – ząbki na obwodzie.
                var lamps = UtDevice.ParseLights(dv.LightsOrDefault); char lit = dv.LitOrDefault; string mast = dv.MastOrDefault;
                double x = 0, yy = Y;
                L(0, Y - 1, 0, Y + 1);                             // stopa
                if (mast == UtDevice.MastWysiegnik)
                {
                    L(0, Y, 3, Y); L(3, Y, 3, Y - 0.8); L(3, Y - 0.8, 4, Y - 0.8);
                    x = 4; yy = Y - 0.8;
                }
                else if (mast == UtDevice.MastProsty) { L(0, Y, 5, Y); x = 5; }
                else if (mast == UtDevice.MastBramka)
                {
                    // bramka sygnałowa: belka w poprzek toru ze słupkami na końcach, sygnalizator zawieszony na belce (do sprawdzenia przez właściciela)
                    L(0, -3, 0, Y + 1.4);
                    dc.DrawRectangle(ink, null, new Rect(P(-0.4, -3.4), P(0.4, -2.6)));
                    dc.DrawRectangle(ink, null, new Rect(P(-0.4, Y + 1.0), P(0.4, Y + 1.8)));
                    L(0, Y, 2.8, Y); x = 2.8;
                }
                if (dv.W24 && dv.Kind == UtDevice.Semafor && mast != UtDevice.MastKarzel) L(1.7, Y + 0.9, 2.3, Y - 0.9);   // W24 – kreska ukośna na maszcie
                // pasy świetlne pod komorami (BSK ss5): od masztu pomarańczowy, potem zielony; tylko semafor
                foreach (var (on, c) in new[] { (dv.StripeP && dv.Kind == UtDevice.Semafor, 'P'), (dv.StripeZ && dv.Kind == UtDevice.Semafor, 'Z') })
                {
                    if (!on) continue;
                    dc.DrawRectangle(Brushes.White, pen, new Rect(P(x, yy - 1), P(x + 1, yy + 1)));
                    if (c == 'Z') L(x, yy, x + 1, yy); else L(x + 0.1, yy + 0.9, x + 0.9, yy - 0.9);
                    x += 1;
                }
                if (!string.IsNullOrEmpty(dv.W21) && dv.Kind == UtDevice.Semafor)
                {
                    // W21 (BSK ss6): skrzynka na maszcie pod komorami, cyfra „pionowo” – górą w stronę komór, jak na stojącym semaforze
                    dc.DrawRectangle(Brushes.White, pen, new Rect(P(x, yy - 1), P(x + 1.6, yy + 1)));
                    if (labels && k >= 2)
                    {
                        var ft = new FormattedText(dv.W21, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Arial"), 1.5 * k, ink, dpi);
                        var cc = P(x + 0.8, yy); var up = P(1, 0) - P(0, 0); up.Normalize();
                        double ang = Math.Atan2(up.Y, up.X) * 180 / Math.PI + 90;      // obrót: góra cyfry (0, −1) → kierunek komór
                        dc.PushTransform(new RotateTransform(ang, cc.X, cc.Y));
                        dc.DrawText(ft, new Point(cc.X - ft.Width / 2, cc.Y - ft.Height / 2));
                        dc.Pop();
                    }
                    x += 1.6;
                }
                bool litDone = false;
                foreach (var (col, pulse) in lamps)
                {
                    double cx = x + R; x += 2 * R;
                    dc.DrawEllipse(Brushes.White, pen, P(cx, yy), R * k, R * k);
                    bool isLit = !litDone && col == lit; if (isLit) litDone = true;
                    var mp = isLit ? thick : pen; double a = R * 0.7;
                    switch (col)
                    {
                        case 'C': L(cx, yy - R, cx, yy + R, mp); break;
                        case 'Z': L(cx - R, yy, cx + R, yy, mp); break;
                        case 'P': L(cx - a, yy + a, cx + a, yy - a, mp); break;
                        case 'N': L(cx - a, yy + a, cx + a, yy - a, mp); L(cx - a, yy - a, cx + a, yy + a, mp); break;
                        case 'B': if (isLit) dc.DrawEllipse(null, thick, P(cx, yy), R * 0.55 * k, R * 0.55 * k); break;
                    }
                    if (pulse)
                        foreach (var an in new[] { 45.0, 135, 225, 315 })
                        {
                            double ca = Math.Cos(an * Math.PI / 180), sa = Math.Sin(an * Math.PI / 180);
                            L(cx + ca * R, yy + sa * R, cx + ca * R * 1.35, yy + sa * R * 1.35);
                        }
                }
                T(dv.Name, -0.6, Y, -1);
                break;
            }
            case UtDevice.Wykolejnica:
            {
                // BSK w1: gruba belka w poprzek toru, strzałka w przód i w stronę wykolejenia
                var bar = new Pen(ink, Math.Max(2, 0.7 * k));
                L(0, -2.2, 0, 2.2, bar);
                L(-1.4, -1.4, 1.6, 1.6);
                var tip = P(1.9, 1.9); var back = P(1.1, 1.1); var side = P(1.6, 1.1) - P(1.1, 1.6);
                dc.DrawGeometry(ink, null, Tri(tip, back + side * 0.5, back - side * 0.5));
                T(dv.Name, -0.6, 2.8, -1);
                break;
            }
            case UtDevice.Koziol:
                L(0, -1.5, 0, 1.5, thick);                         // belka kozła (Ig-10 s. 25: a = 1,5 mm na stronę)
                L(0, 0, 1, 0);                                     // b = 1 mm za belką
                break;
            case UtDevice.LicznikOsi:
                // BSK o9: czujnik licznika osi – dwie połączone kropki obok toru
                L(-1.3, 1.1, 1.3, 1.1);
                dc.DrawEllipse(ink, null, P(-0.6, 1.1), 0.5 * k, 0.5 * k); dc.DrawEllipse(ink, null, P(0.6, 1.1), 0.5 * k, 0.5 * k);
                T(dv.Name, 0, 2.8);
                break;
            case UtDevice.Zlacze:
                // BSK o4: złącze izolowane na styku dwóch odcinków izolowanych – „I”
                L(0, -1.2, 0, 1.2);
                L(-0.6, -1.2, 0.6, -1.2); L(-0.6, 1.2, 0.6, 1.2);
                break;
            case UtDevice.Ukres:
                L(0, 0.6, 0, 2.4, thick);                          // W17 – w międzytorzu po stronie urządzenia
                break;
        }
    }

    static StreamGeometry Tri(Point a, Point b, Point c)
    {
        var g = new StreamGeometry();
        using (var x = g.Open()) { x.BeginFigure(a, true, true); x.LineTo(b, true, false); x.LineTo(c, true, false); }
        g.Freeze(); return g;
    }
}

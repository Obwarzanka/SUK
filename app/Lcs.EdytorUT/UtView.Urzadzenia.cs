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
        SelectedDevice = id; SelectedTurnout = null; SelectedCrossing = null; SelectedTrack = null; SelectedVertex = -1; SelectedObj = null;
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
        var v = ToWorld(at) - p; var d = Element.Dir(az);
        double along = v.X * d.X + v.Y * d.Y, across = d.X * v.Y - d.Y * v.X;
        // bez kierunku (licznik osi, złącze, ukres): po tej stronie toru, po której kliknięto (właściciel 2026-10-17)
        if (!UtDevice.KindInfo(DevKind).Directed) return (s, 1, across < 0 ? -1 : 1);
        if ((at - pa).Length < 8) return (s, 1, 1);
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
            var b = ToScreen(BelkaPos(d, p)); var dd = ScreenDir(az); var n = new Vector(-dd.Y, dd.X) * d.Side; double k = PxPerMm;
            var pts = new List<Point> { b, b + n * (2.5 * k), b + n * (2.5 * k) + dd * (4 * k) };
            // sygnalizator: cały rząd komór (wiszący na bramce – od belki, na maszcie – za masztem), żeby chwycić go za dowolną komorę
            if (Urzadzenia.Sygnalizator(d))
            {
                double len = d.MastOrDefault is UtDevice.MastBramka or UtDevice.MastWysiegnik ? HangingLen(d) + 3 : 5 + 2 * UtDevice.ParseLights(d.LightsOrDefault).Count;
                for (double x = 1; x <= len; x += 1) pts.Add(b + n * (2.4 * k) + dd * (x * k));
            }
            foreach (var q in pts)
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
        // sygnalizatory wiszące na wspólnej konstrukcji (bramka, wysięgnik nad kilkoma torami) – bez własnego masztu
        var hang = Urzadzenia.Konstrukcje(Doc.P).SelectMany(x => x.M).Select(x => x.Id).ToHashSet();
        foreach (var d0 in Doc.P.Devices)
        {
            var d = d0;
            if (!Shown(LayerOf(d0.Kind))) continue;
            if (devDrag is var (did, _, ds) && did == d0.Id) { d = d0.Copy(); d.S = ds; }
            if (Urzadzenia.Pos(Doc.P, d) is not var (p, az, _)) continue;
            var b = ToScreen(BelkaPos(d, p));
            if (b.X < -100 || b.Y < -100 || b.X > ActualWidth + 100 || b.Y > ActualHeight + 100) continue;
            if (d.Id == SelectedDevice && !PrintMode) { double rs = Math.Clamp(1.5 * PxPerMm, 6, 12); dc.DrawEllipse(RzSel, null, b, rs, rs); }
            DrawDevice(dc, d, b, ScreenDir(az), DevK, DevInk, dpi, LabelsOn, hang.Contains(d0.Id));
            var notes = Urzadzenia.Check(Doc.P, d0);
            if (notes.Count > 0) { dc.DrawEllipse(null, ErrP, b, 9, 9); ErrLabel(dc, (d.Name.Length > 0 ? d.Name + ": " : "") + string.Join("; ", notes), b, dpi); }
        }
        DrawGantries(dc);
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
    /// Semafor wiszący na bramce: komory od górnej (przy belce) do dolnej, pod nimi (dalej od belki) W21 i pasy świetlne – jak na planach
    /// schematycznych (Semaforek, Łódź Widzew); nazwa za ostatnim elementem.
    /// </summary>
    static void DrawHangingHeads(DrawingContext dc, UtDevice dv, Func<double, double, Point> P, Action<double, double, double, double, Pen> L, Action<double, double, double, double> Box, Pen pen, Pen thick, Brush ink,
        List<(char C, int Pulse)> lamps, char lit, double x, double Y, double R, double k, double dpi, bool labels, Action<string, double, double, double> T)
    {
        double yy = Y; bool litDone = false;
        // komora świecąca – pierwsza od dołu o kolorze stanu zasadniczego (jak na maszcie)
        int litIdx = -1; for (int i = 0; i < lamps.Count && !litDone; i++) if (lamps[i].C == lit && lamps[i].Pulse != UtDevice.Nieczynna) { litIdx = i; litDone = true; }
        for (int i = lamps.Count - 1; i >= 0; i--)
        {
            var (col, pulse) = lamps[i];
            double cx = x + R; x += 2 * R;
            dc.DrawEllipse(Brushes.White, pen, P(cx, yy), R * k, R * k);
            if (pulse == UtDevice.Nieczynna) { Kreskowana(L, cx, yy, R, pen); continue; }
            var mp = i == litIdx ? thick : pen; double a = R * 0.7;
            switch (col)
            {
                case 'C': L(cx, yy - R, cx, yy + R, mp); break;
                case 'Z': L(cx - R, yy, cx + R, yy, mp); break;
                case 'P': L(cx - a, yy + a, cx + a, yy - a, mp); break;
                case 'N': L(cx - a, yy + a, cx + a, yy - a, mp); L(cx - a, yy - a, cx + a, yy + a, mp); break;
                case 'B': if (i == litIdx) dc.DrawEllipse(null, thick, P(cx, yy), R * 0.55 * k, R * 0.55 * k); break;
            }
            foreach (var an in PulseTicks(pulse))
            {
                double ca = Math.Cos(an * Math.PI / 180), sa = Math.Sin(an * Math.PI / 180);
                L(cx + ca * R, yy + sa * R, cx + ca * R * 1.35, yy + sa * R * 1.35, pen);
            }
        }
        if (!string.IsNullOrEmpty(dv.W21) && dv.Kind == UtDevice.Semafor)
        {
            Box(x, yy - 1, x + 1.6, yy + 1);
            if (labels && k >= 2)
            {
                var ft = new FormattedText(dv.W21, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Arial"), 1.5 * k, ink, dpi);
                var cc = P(x + 0.8, yy); var up = P(0, 0) - P(1, 0); up.Normalize();
                double ang = Math.Atan2(up.Y, up.X) * 180 / Math.PI + 90;      // wiszący: góra cyfry w stronę belki
                dc.PushTransform(new RotateTransform(ang, cc.X, cc.Y));
                dc.DrawText(ft, new Point(cc.X - ft.Width / 2, cc.Y - ft.Height / 2));
                dc.Pop();
            }
            x += 1.6;
        }
        // pasy świetlne pod komorami wiszącego semafora (dalej od belki), od komór: pomarańczowy, zielony – jak BSK ss5 w odbiciu
        foreach (var (on, c) in new[] { (dv.StripeP && dv.Kind == UtDevice.Semafor, 'P'), (dv.StripeZ && dv.Kind == UtDevice.Semafor, 'Z') })
        {
            if (!on) continue;
            Pas(Box, L, pen, c, x, yy);
            x += PasW;
        }
        T(dv.Name, x + 0.6, Y, 1);
    }

    /// <summary>
    /// Kreski na obwodzie komory: światło stałe lub pulsujące – 2 (ukośnie, na przeciwnych stronach), tylko pulsujące – 4
    /// (uwaga właściciela 2026-10-17, plan Łódź Widzew – L2).
    /// </summary>
    /// <summary>Komora nieczynna: kreskowana ukośnie (3 cięciwy „/”), bez znaku koloru – uwaga właściciela 2026-10-22 (plan, semafor E7).</summary>
    static void Kreskowana(Action<double, double, double, double, Pen> L, double cx, double cy, double R, Pen pen)
    {
        double s = Math.Sqrt(0.5);
        foreach (var o in new[] { -0.5 * R, 0, 0.5 * R })
        {
            double h = Math.Sqrt(R * R - o * o);
            double mx = cx + o * s, my = cy + o * s;              // środek cięciwy: przesunięty w poprzek kreskowania (1, 1)
            L(mx - h * s, my + h * s, mx + h * s, my - h * s, pen);
        }
    }

    static double[] PulseTicks(int mode) => mode switch
    {
        UtDevice.StaleLubPulsujace => new[] { 45.0, 225 },
        UtDevice.TylkoPulsujace => new[] { 45.0, 135, 225, 315 },
        _ => Array.Empty<double>(),
    };

    /// <summary>
    /// Długość głowicy semafora na bramce od belki [mm arkusza] – łącznik i komory; wyznacza długość słupów bramki. Pasy świetlne i W21
    /// (pod komorami) jej nie zmieniają (uwaga właściciela 2026-10-20: „pas wydłuża konstrukcję”).
    /// </summary>
    public static double HangingLen(UtDevice dv) => 1.2 + 2 * UtDevice.ParseLights(dv.LightsOrDefault).Count;

    /// <summary>Pas świetlny (BSK ISDR ss5): wąski prostokąt 0,7 × 2,2 mm w osi znaku, ciut wyższy od komory.</summary>
    const double PasW = 0.7, PasH = 1.1;

    /// <summary>
    /// Pas świetlny w osi znaku od <paramref name="x"/>: pomarańczowy – kreska ukośna (jak komora pomarańczowa), zielony – kreska wzdłuż masztu
    /// (jak komora zielona); prostokąt obrócony razem ze znakiem (wcześniej prostokąt równoległy do ekranu – na skośnym torze kwadrat albo kreska).
    /// </summary>
    static void Pas(Action<double, double, double, double> Box, Action<double, double, double, double, Pen> L, Pen pen, char c, double x, double yy)
    {
        Box(x, yy - PasH, x + PasW, yy + PasH);
        if (c == 'Z') L(x, yy, x + PasW, yy, pen); else L(x + 0.12, yy + PasH - 0.15, x + PasW - 0.12, yy - PasH + 0.15, pen);
    }

    /// <summary>Bramki: grupy sygnalizatorów na jednej bramce (połączone albo w tym samym miejscu – <see cref="Urzadzenia.Konstrukcje"/>).</summary>
    public List<List<UtDevice>> GantryGroups() => Doc == null ? new() : Urzadzenia.Konstrukcje(Doc.P).Where(k => k.Mast == UtDevice.MastBramka).Select(k => k.M).ToList();

    /// <summary>
    /// Belki i słupy konstrukcji (Semaforek, Łódź Widzew): belka w poprzek torów w miejscu sygnalizatorów, poza skrajne tory; słup – odcinek
    /// równoległy do torów w stronę komór, długi jak najdłuższy znak, zakończony kreską stopy. Bramka – słupy na obu końcach, wysięgnik
    /// wspólny – jeden słup; liczbę słupów, stronę jedynego słupa i tory, nad którymi belka przechodzi bez sygnalizatora, ustawia projektant
    /// (<see cref="Urzadzenia.Ustawienia"/>; jeden słup bez wyboru strony – po stronie większości sygnalizatorów).
    /// </summary>
    /// <summary>
    /// Miejsce znaku sygnalizatora na planie: na konstrukcji z przesuniętą belką (<see cref="UtDevice.BelkaOff"/>) znak wisi na belce,
    /// o przesunięcie wzdłuż toru od miejsca semafora (km bez zmian – kreskowana linia w <see cref="DrawGantries"/>); podgląd przeciągania belki.
    /// </summary>
    XY BelkaPos(UtDevice d, XY p)
    {
        if (!Urzadzenia.Sygnalizator(d) || string.IsNullOrEmpty(d.Konstrukcja)) return p;
        double off = d.BelkaOff ?? 0;
        if (gantryDrag is var (gid, _, goff) && Dev(gid)?.Konstrukcja == d.Konstrukcja) off = goff;
        if (Math.Abs(off) < 0.005) return p;
        // kierunek – pierwszego członka konstrukcji (jak belka)
        var first = Doc.P.Devices.FirstOrDefault(x => x.Konstrukcja == d.Konstrukcja && Urzadzenia.Sygnalizator(x)) ?? d;
        if (Urzadzenia.Pos(Doc.P, first) is not var (_, az0, k0)) return p;
        return p + Element.Dir(az0) * (off * k0);
    }

    /// <summary>Układ konstrukcji na ekranie: belka (dwa końce), słupy (odcinek i stopa), łączniki belka – semafor przy przesuniętej belce.</summary>
    record GantryLay(List<UtDevice> M, Point B0, Point B1, List<(Point A, Point B)> Posts, List<(Point A, Point B)> Hangers, XY W0, double Az0, double K0, double Off);

    /// <summary>Przeciąganie belki: członkowie (id pierwszego), chwyt [m wzdłuż toru od miejsca sygnalizatorów], bieżące przesunięcie [m].</summary>
    (string Id, double Grab, double Off)? gantryDrag;
    /// <summary>Belka przesunięta przeciągnięciem: id pierwszego członka konstrukcji, nowe przesunięcie [m].</summary>
    public event Action<string, double> GantryMoved;

    List<GantryLay> GantryLayout()
    {
        var res = new List<GantryLay>();
        if (Doc == null || !Shown(LayerOf(UtDevice.Semafor))) return res;
        double k = DevK;
        foreach (var (mast, grp) in Urzadzenia.Konstrukcje(Doc.P))
        {
            // położenie członków: punkt na osi toru, kierunek komór (ekran), normalna, strona
            var m = new List<(Point B, Vector D, UtDevice Dv)>(); (XY P, double Az, double K)? w0 = null; var true0 = new List<XY>();
            foreach (var dv in grp)
            {
                var d = dv; if (devDrag is var (did, _, ds) && did == dv.Id) { d = dv.Copy(); d.S = ds; }
                if (Urzadzenia.Pos(Doc.P, d) is var (p, az, kk)) { m.Add((ToScreen(p), ScreenDir(az), dv)); w0 ??= (p, az, kk); true0.Add(p); }
            }
            if (m.Count == 0) continue;
            var dir = m[0].D; var o = m[0].B;
            // belka w poprzek torów w terenie: w Schemacie (rozciągnięcie w poprzek) prostopadła w terenie nie jest prostopadła na ekranie –
            // kierunek belki to obraz normalnej toru, a położenie członków w układzie (wzdłuż dir, wzdłuż belki)
            var (pw0, az0, k0) = w0.Value;
            double off = gantryDrag is var (gid, _, goff) && gid == grp[0].Id ? goff : Urzadzenia.BelkaOff(grp);
            var nrm = ToScreen(pw0 + Element.Right(az0) * k0) - ToScreen(pw0); nrm.Normalize();
            var perp = new Vector(-dir.Y, dir.X); if (nrm * perp < 0) nrm = -nrm;
            double det = dir.X * nrm.Y - dir.Y * nrm.X;
            (double A, double C) Loc(Point b) { var w = b - o; return ((w.X * nrm.Y - w.Y * nrm.X) / det, (dir.X * w.Y - dir.Y * w.X) / det); }
            double along = m.Average(q => Loc(q.B).A) + Loc(ToScreen(pw0 + Element.Dir(az0) * (off * k0))).A;
            // zasięg w poprzek: tory i semafory członków (oś toru ± 1,5 mm, semafor po swojej stronie do 2,4 + 1 + 1,5 mm)
            double lo = double.MaxValue, hi = double.MinValue;
            foreach (var (b, _, dv) in m)
            {
                double c = Loc(b).C, sy = dv.Side * (2.4 + 1 + 1.5) * k;
                lo = Math.Min(lo, Math.Min(c - 1.5 * k, c + sy)); hi = Math.Max(hi, Math.Max(c + 1.5 * k, c + sy));
            }
            var (slupy, strona, tory) = Urzadzenia.Ustawienia(mast, grp);
            var pw = Urzadzenia.Pos(Doc.P, grp[0]).Value.P;                                  // miejsce konstrukcji w terenie
            foreach (var tid in tory)
            {
                if (Doc.P.Tracks.FirstOrDefault(t => t.Id == tid) is not UtTrack t) continue;
                var rt = Geometry(t); if (rt.Elements.Count == 0) continue;
                double c = Loc(ToScreen(rt.At(rt.Nearest(pw, 0.25).S).P)).C;                 // oś toru pod belką
                lo = Math.Min(lo, c - 1.5 * k); hi = Math.Max(hi, c + 1.5 * k);
            }
            double len = m.Max(q => HangingLen(q.Dv)) * k + 1 * k;
            Point Q(double a, double c) => o + dir * a + nrm * c;
            var posts = new List<(Point, Point)>();
            foreach (var c in slupy == 2 ? new[] { lo, hi } : new[] { (strona != 0 ? strona : m.Sum(q => q.Dv.Side)) >= 0 ? hi : lo })
            {
                posts.Add((Q(along, c), Q(along + len, c)));                                     // słup (w stronę komór)
                posts.Add((Q(along + len, c - 0.8 * k), Q(along + len, c + 0.8 * k)));          // stopa
            }
            // belka przesunięta – znaki wiszą na belce, kreskowana linia wzdłuż toru do miejsca semafora (km do zależności) z kreską w poprzek
            var hang = new List<(Point, Point)>();
            if (Math.Abs(off) > 0.01)
                for (int i = 0; i < m.Count; i++)
                {
                    var tp = ToScreen(true0[i]); var bp = ToScreen(true0[i] + Element.Dir(az0) * (off * k0));
                    hang.Add((tp, bp));
                    var nn = new Vector(-m[i].D.Y, m[i].D.X) * (1.2 * k);
                    hang.Add((tp - nn, tp + nn));
                }
            res.Add(new GantryLay(grp, Q(along, lo), Q(along, hi), posts, hang, pw0, az0, k0, off));
        }
        return res;
    }

    void DrawGantries(DrawingContext dc)
    {
        double k = DevK; var pen = new Pen(DevInk, Math.Max(1, 0.18 * k));
        var dash = new Pen(DevInk, Math.Max(1, 0.18 * k)) { DashStyle = new DashStyle(new double[] { 3, 2 }, 0) };
        foreach (var g in GantryLayout())
        {
            dc.DrawLine(pen, g.B0, g.B1);                                                       // belka
            foreach (var (a, b) in g.Posts) dc.DrawLine(pen, a, b);
            foreach (var (a, b) in g.Hangers) dc.DrawLine(dash, a, b);
        }
    }

    /// <summary>Belki konstrukcji na ekranie (id pierwszego członka, końce) – do testów.</summary>
    public List<(string Id, Point A, Point B)> GantryBeams() => GantryLayout().Select(g => (g.M[0].Id, g.B0, g.B1)).ToList();

    /// <summary>Belka konstrukcji pod kursorem (do 6 px od belki).</summary>
    GantryLay HitGantry(Point s)
    {
        foreach (var g in GantryLayout())
        {
            var d = g.B1 - g.B0; double l2 = d.LengthSquared; if (l2 < 1) continue;
            double t = Math.Clamp(((s - g.B0) * d) / l2, 0, 1);
            if ((g.B0 + d * t - s).Length < 6) return g;
        }
        return null;
    }

    /// <summary>Kilometraż kursora wzdłuż toru pierwszego członka od miejsca sygnalizatorów [m].</summary>
    static double AlongM(GantryLay g, XY w) { var d = Element.Dir(g.Az0); return ((w - g.W0).X * d.X + (w - g.W0).Y * d.Y) / g.K0; }

    bool PressGantry(Point s)
    {
        if (HitGantry(s) is not GantryLay g) return false;
        gantryDrag = (g.M[0].Id, AlongM(g, ToWorld(s)) - g.Off, g.Off);
        if (SelectedDevice == null || g.M.All(x => x.Id != SelectedDevice)) SelectDevice(g.M[0].Id);
        return true;
    }

    void DragGantry(Point s)
    {
        var (id, grab, _) = gantryDrag.Value;
        var g = GantryLayout().FirstOrDefault(x => x.M[0].Id == id); if (g == null) return;
        gantryDrag = (id, grab, Math.Round(AlongM(g, ToWorld(s)) - grab, 1));
        DraftInfo?.Invoke($"Belka przesunięta o {gantryDrag.Value.Off:0.0} m względem semaforów (semafory zostają)");
        InvalidateVisual();
    }

    void EndGantryDrag()
    {
        var (id, _, off) = gantryDrag.Value; gantryDrag = null;
        if (dragging) GantryMoved?.Invoke(id, off);
        InvalidateVisual();
    }

    /// <summary>
    /// Znak urządzenia w punkcie <paramref name="b"/> na osi toru: x – w kierunku działania <paramref name="d"/>, y – w stronę urządzenia
    /// (Side), w mm arkusza × <paramref name="k"/>. Komory sygnalizatora od masztu w stronę jazdy (+x), nazwa za stopą (−x).
    /// </summary>
    static void DrawDevice(DrawingContext dc, UtDevice dv, Point b, Vector d, double k, Brush ink, double dpi, bool labels, bool hanging = false)
    {
        var n = new Vector(-d.Y, d.X) * dv.Side;
        Point P(double x, double y) => b + d * (x * k) + n * (y * k);
        // grubości wg Ig-10: linie znaku 0,18 mm, wyróżnienie (komora świecąca, belka) 0,45 mm
        var pen = new Pen(ink, Math.Max(1, 0.18 * k)); var thick = new Pen(ink, Math.Max(1.6, 0.45 * k));
        void L(double x1, double y1, double x2, double y2, Pen pp = null) => dc.DrawLine(pp ?? pen, P(x1, y1), P(x2, y2));
        // prostokąt w układzie znaku (obrócony razem z torem) – białe wypełnienie, obwód cienką linią
        void Box(double x1, double y1, double x2, double y2)
        {
            var g = new StreamGeometry();
            using (var c = g.Open()) { c.BeginFigure(P(x1, y1), true, true); c.PolyLineTo(new[] { P(x2, y1), P(x2, y2), P(x1, y2) }, true, false); }
            g.Freeze();
            dc.DrawGeometry(Brushes.White, pen, g);
        }
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
                // zasadniczym – gruba kreska; światło stałe lub pulsujące – 2 kreski na obwodzie, tylko pulsujące – 4 (PulseTicks).
                var lamps = UtDevice.ParseLights(dv.LightsOrDefault); char lit = dv.LitOrDefault; string mast = dv.MastOrDefault;
                double x = 0, yy = Y;
                bool gantry = mast == UtDevice.MastBramka || hanging;      // wisi na belce (bramka albo wspólny wysięgnik)
                if (!gantry) L(0, Y - 1, 0, Y + 1);                // stopa (na bramce – bez: semafor wisi na belce)
                if (mast == UtDevice.MastWysiegnik && !gantry)
                {
                    L(0, Y, 3, Y); L(3, Y, 3, Y - 0.8); L(3, Y - 0.8, 4, Y - 0.8);
                    x = 4; yy = Y - 0.8;
                }
                else if (mast == UtDevice.MastProsty) { L(0, Y, 5, Y); x = 5; }
                else if (gantry)
                {
                    // bramka (Semaforek, plan Łódź Widzew: L3 / L5, S1 / S3, D / E): semafor wisi na belce – krótki łącznik od belki
                    // (belkę i słupy rysuje DrawGantries, wspólne dla semaforów w tym samym miejscu)
                    L(0, Y, 1.2, Y); x = 1.2;
                }
                if (dv.W24 && dv.Kind == UtDevice.Semafor && mast != UtDevice.MastKarzel)
                {
                    if (gantry) L(0.3, Y + 0.9, 0.9, Y - 0.9);                     // W24 – kreska ukośna na łączniku przy belce
                    else L(1.7, Y + 0.9, 2.3, Y - 0.9);                                 // W24 – kreska ukośna na maszcie
                }
                if (gantry) { DrawHangingHeads(dc, dv, P, (a, b2, c2, d2, pp) => L(a, b2, c2, d2, pp), Box, pen, thick, ink, lamps, lit, x, Y, R, k, dpi, labels, T); break; }
                // pasy świetlne pod komorami (BSK ss5): od masztu pomarańczowy, potem zielony; tylko semafor
                foreach (var (on, c) in new[] { (dv.StripeP && dv.Kind == UtDevice.Semafor, 'P'), (dv.StripeZ && dv.Kind == UtDevice.Semafor, 'Z') })
                {
                    if (!on) continue;
                    Pas(Box, (a, b2, c2, d2, pp) => L(a, b2, c2, d2, pp), pen, c, x, yy);
                    x += PasW;
                }
                if (!string.IsNullOrEmpty(dv.W21) && dv.Kind == UtDevice.Semafor)
                {
                    // W21 (BSK ss6): skrzynka na maszcie pod komorami, cyfra „pionowo” – górą w stronę komór, jak na stojącym semaforze
                    Box(x, yy - 1, x + 1.6, yy + 1);
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
                    if (pulse == UtDevice.Nieczynna) { Kreskowana((a1, b1, c1, d1, pp) => L(a1, b1, c1, d1, pp), cx, yy, R, pen); continue; }
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
                    foreach (var an in PulseTicks(pulse))
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
                // BSK o9: czujnik licznika osi – dwie połączone kropki tuż przy torze (do 1,15 mm od osi – nie wchodzi na komory semafora,
                // które zaczynają się 1,4 mm od osi); nazwa po drugiej stronie toru – nie chowa się pod sygnalizatorem w tym samym miejscu
                L(-1.0, 0.75, 1.0, 0.75);
                dc.DrawEllipse(ink, null, P(-0.5, 0.75), 0.4 * k, 0.4 * k); dc.DrawEllipse(ink, null, P(0.5, 0.75), 0.4 * k, 0.4 * k);
                T(dv.Name, 0, -1.6);
                break;
            case UtDevice.Zlacze:
                // BSK o4: złącze izolowane na styku dwóch odcinków izolowanych – „I”
                L(0, -1.2, 0, 1.2);
                L(-0.6, -1.2, 0.6, -1.2); L(-0.6, 1.2, 0.6, 1.2);
                break;
            case UtDevice.Ukres:
                L(0, 0.6, 0, 2.4, thick);                          // W17 – w międzytorzu po stronie urządzenia
                break;
            case UtDevice.W4:
            {
                // Ig-10 s. 33: tarcza 2 × 3 mm na słupku 2 mm, krzyż w środku (0,35 mm)
                L(0, 1, 0, 3);
                Box(-1, 3, 1, 6);                                   // tarcza obrócona z torem
                var cr = new Pen(ink, Math.Max(1, 0.35 * k));
                L(0, 3, 0, 6, cr); L(-1, 4.5, 1, 4.5, cr);
                T(dv.Name, -0.6, 4.5, -1);
                break;
            }
            case UtDevice.W5:
            {
                // Ig-10 s. 30: wskaźnik granicy przetaczania – półkole nad poprzeczką 3,5 mm na słupku 2 mm
                L(0, 1, 0, 3);
                L(-1.75, 3, 1.75, 3);
                var arc = new StreamGeometry();
                using (var g = arc.Open())
                {
                    g.BeginFigure(P(-1, 3), false, false);
                    for (int i = 1; i <= 12; i++) { double a = Math.PI * i / 12; g.LineTo(P(-Math.Cos(a), 3 + Math.Sin(a)), true, true); }
                }
                dc.DrawGeometry(null, pen, arc);
                T(dv.Name, -0.6, 4, -1);
                break;
            }
            case UtDevice.W8:
            {
                // słupek 2 mm jak wskaźniki Ig-10 s. 34, tablica wg Ie-1: trójkąt wierzchołkiem w dół (ku słupkowi), liczba = 0,1 prędkości
                L(0, 1, 0, 3);
                var tri = Tri(P(0, 3), P(-1.7, 6), P(1.7, 6));
                dc.DrawGeometry(Brushes.White, pen, tri);
                T(dv.Vw is int v8 ? (v8 / 10.0).ToString("0.#", CultureInfo.InvariantCulture) : "?", 0, 7.2);
                T(dv.Name, -0.6, 4.5, -1);
                break;
            }
            case UtDevice.W9:
            {
                // słupek i tablica 2 × 3 mm (Ig-10 s. 34), wg Ie-1: kąt wierzchołkiem w dół (strona początku) dotyka przeciwległego boku, liczba = 0,1 prędkości
                L(0, 1, 0, 3);
                Box(-1, 3, 1, 6);
                if (dv.Vw is int v9) { L(-1, 6, 0, 3); L(1, 6, 0, 3); T((v9 / 10.0).ToString("0.#", CultureInfo.InvariantCulture), 0, 7.2); }
                else { L(-1, 3, 0, 6); L(1, 3, 0, 6); }       // tylko koniec odcinka: kąt w górę
                if (dv.W9C) T("C", 1.9, 4.5);
                T(dv.Name, -0.6, 4.5, -1);
                break;
            }
        }
    }

    static StreamGeometry Tri(Point a, Point b, Point c)
    {
        var g = new StreamGeometry();
        using (var x = g.Open()) { x.BeginFigure(a, true, true); x.LineTo(b, true, false); x.LineTo(c, true, false); }
        g.Freeze(); return g;
    }
}

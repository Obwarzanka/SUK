using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>Narzędzia płótna.</summary>
public enum UtTool { Wskaz, Miarka, Obszar, Kalibracja, Przesun, Kafelki, Tor, Rozjazd, Rownolegly, Urzadzenie, LukOdDo, Odcinek, Peron, Przejazd, Km, LukPunkty, Szlak, Slad, Obiekt }

/// <summary>
/// Płótno edytora UT w metrach PUWG 1992. Widok Teren: północ (X) do góry, wschód (Y) w prawo, 1:1. Widok Schemat: oś stacji
/// (azymut <see cref="AxisAz"/>) poziomo w prawo, w poprzek rozciągnięte <see cref="Stretch"/> razy – jak plan schematyczny (ISDR, Ig-10 1:2000 / 1:500).
/// Rysowanie w pikselach ekranu – przeliczenie świat → ekran w double (współrzędne rzędu 10⁵ m nie tracą dokładności w WPF).
/// Mysz: kółko – przybliżenie wokół kursora; przeciąganie LPM (we Wskaż i po przesunięciu o kilka px w innych narzędziach),
/// środkowym przyciskiem albo Spacja + LPM – przesuwanie widoku.
/// Kolejność warstw: siatka, podgląd na żywo, podkłady projektu (od dołu), nakładki narzędzi, linijki.
/// </summary>
public partial class UtView : FrameworkElement
{
    public UtDocument Doc;
    public readonly ImageCache Cache = new();
    public readonly LivePreview Live;
    public const double MinMpp = 0.002, MaxMpp = 2000;
    public const double RulerTop = 22, RulerLeft = 70;
    const double ClickTol = 4;

    XY center = new(470000, 500000);
    // środek widoku we współrzędnych osi: u – kilometraż wzdłuż osi, v – odsunięcie w prawo [m układu]
    double cu = 500000, cv = -470000;
    double mpp = 1000;
    public XY Center => center;
    OsSchematu os = OsSchematu.ProstaAz(Math.PI / 2);
    double stretch = 1;
    /// <summary>Wersja osi i rozciągnięcia – ramki torów we współrzędnych osi liczone na nowo po zmianie.</summary>
    int osVer;
    /// <summary>Oś widoku (Teren – prosta na wschód; Schemat – po torach, <see cref="OsSchematu"/>).</summary>
    public OsSchematu Os => os;
    /// <summary>Azymut kierunku biegnącego na ekranie w prawo w środku widoku [rad] (Teren – π/2, wschód).</summary>
    public double AxisAz => os.AzAt(cu);
    /// <summary>Azymut osi [rad] przy punkcie (oś po linii skręca razem z torami).</summary>
    public double AxisAzAt(XY p) => os.AzAt(os.UV(p).U);
    OsSchematu projOs; (UtDocument, int, string, bool, double?) projOsKey;
    /// <summary>
    /// Azymut osi schematu projektu przy punkcie [rad] – także w widoku Teren (Shift: koniec łuku równoległy do osi stacji).
    /// W Schemacie – oś widoku; w Terenie – oś liczona z bieżących torów (jak dawniej <see cref="UtProject.SchematAz"/>), z pamięci do zmiany projektu.
    /// </summary>
    public double StationAzAt(XY p)
    {
        if (Schemat || Doc == null) return AxisAzAt(p);
        // podpis geometrii wszystkich torów (liczby wierzchołków i elementów) – oś na nowo po każdej zmianie torów
        var h = new HashCode(); foreach (var t in Doc.P.Tracks) { h.Add(t.Id); foreach (var x in SigVals(t)) h.Add(x); }
        var key = (Doc, h.ToHashCode(), Doc.P.AxisTrack, Doc.P.AxisRev, Doc.P.AxisAz);
        if (projOs == null || key != projOsKey) { projOs = OsSchematu.Dla(Doc.P); projOsKey = key; }
        return projOs.AzAt(projOs.UV(p).U);
    }
    /// <summary>Rozciągnięcie w poprzek osi (Teren – 1).</summary>
    public double Stretch => stretch;
    /// <summary>Widok Schemat (obrót do poziomu, rozciągnięcie, białe tło bez siatki i linijek).</summary>
    public bool Schemat { get; private set; }
    /// <summary>Widok Schemat: podkłady widoczne (domyślnie ukryte).</summary>
    public bool SchematBg;
    bool BgOn => (!Schemat || SchematBg) && Shown("podklady");
    /// <summary>Metry na piksel ekranu.</summary>
    public double Mpp => mpp;
    public bool ShowGrid = true;
    /// <summary>Siatka w widoku Schemat (wzdłuż i w poprzek osi) – domyślnie wyłączona (czysty schemat jak w ISDR).</summary>
    public bool ShowGridSchemat;
    /// <summary>
    /// Test: prawdziwa klawiatura nie wpływa na płótno (Spacja – przesuwanie, Ctrl – bez przyciągania, Shift – przedłużenie);
    /// wcześniej pisanie przy komputerze w trakcie testu zamieniało przeciągnięcie w przesuwanie widoku. Klawisze testu – <see cref="TestMods"/>.
    /// </summary>
    public static bool IgnoreKeyboard;
    public static ModifierKeys TestMods;
    static ModifierKeys Mods => IgnoreKeyboard ? TestMods : Keyboard.Modifiers;
    public event Action<XY?> CursorMoved;
    public event Action ViewChanged;

    // ---- narzędzia
    UtTool tool;
    public UtTool Tool { get => tool; set { if (tool == UtTool.Tor && value != UtTool.Tor) CancelDraft(); if (value != UtTool.Rownolegly) { parSrc = null; parD = null; } tool = value; areaFrom = null; vDrag = null; rzFrom = null; rzDrag = null; tDrag = null; devFrom = null; devDrag = null; gantryDrag = null; gantryEnd = null; arcFrom = null; objFrom = null; objDrag = null; obFrom = null; obPress = null; painting2 = false; fitNetFrom = null; Cursor = value == UtTool.Wskaz ? null : Cursors.Cross; InvalidateVisual(); ToolChanged?.Invoke(); } }
    public event Action ToolChanged;
    /// <summary>Miarka: punkty łamanej.</summary>
    public readonly List<XY> MeasurePts = new();
    public event Action MeasureChanged;
    /// <summary>Obszar: zaznaczony prostokąt (dwa narożniki).</summary>
    public event Action<XY, XY> AreaSelected;
    /// <summary>Kalibracja: kliknięcie w punkcie świata.</summary>
    public event Action<XY> CalClick;
    /// <summary>Kalibracja: PPM – rezygnacja z bieżącego punktu.</summary>
    public event Action CalCancel;
    /// <summary>Przesuń: podkład przeciągnięty o wektor [m].</summary>
    public event Action<string, XY> BgDragged;
    /// <summary>Zaznaczony podkład (obwódka, Przesuń, kalibracja).</summary>
    public string SelectedBg;
    /// <summary>Kafelki: malowanie LPM (start = true przy wciśnięciu), PPM / środkowy – przesuwanie widoku.</summary>
    public event Action<XY, bool> Paint;
    public event Action PaintEnd;
    /// <summary>Rysowanie podkładów projektu (okno mapki – bez).</summary>
    public bool ShowBackgrounds = true;
    /// <summary>Dodatkowa nakładka (np. siatka kafelków w oknie mapki) – nad podkładami, pod linijkami.</summary>
    public Action<DrawingContext> Overlay;
    /// <summary>Kursor nad płótnem (świat) – dla nakładek.</summary>
    public XY? Hover => hover;
    /// <summary>Kalibracja: wskazany piksel obrazu czekający na miejsce docelowe.</summary>
    public (double Col, double Row)? CalPending;

    static readonly Typeface UiFont = new("Segoe UI"), MonoFont = new("Consolas");
    static readonly Brush Paper = Frozen(Color.FromRgb(0xfb, 0xfb, 0xf8)), RulerBg = Frozen(Color.FromRgb(0xee, 0xee, 0xea)),
        Ink = Frozen(Color.FromRgb(0x30, 0x30, 0x30)), Hint = Frozen(Color.FromRgb(0x80, 0x80, 0x80)),
        Red = Frozen(Color.FromRgb(0xd0, 0x18, 0x18)), Halo = Frozen(Color.FromArgb(0xd8, 0xff, 0xff, 0xff)),
        Blue = Frozen(Color.FromRgb(0x10, 0x60, 0xd0)), AreaFill = Frozen(Color.FromArgb(0x30, 0x10, 0x60, 0xd0)), Magenta = Frozen(Color.FromRgb(0xc0, 0x10, 0xa0));
    static readonly Pen MinorPen = FrozenPen(Color.FromRgb(0xea, 0xea, 0xe6), 1), MajorPen = FrozenPen(Color.FromRgb(0xcc, 0xcc, 0xc6), 1),
        TickPen = FrozenPen(Color.FromRgb(0x70, 0x70, 0x70), 1), EdgePen = FrozenPen(Color.FromRgb(0xb0, 0xb0, 0xa8), 1),
        BarPen = FrozenPen(Color.FromRgb(0x30, 0x30, 0x30), 2), MeasurePen = FrozenPen(Color.FromRgb(0xd0, 0x18, 0x18), 2),
        HaloPen = FrozenPen(Color.FromArgb(0xc0, 0xff, 0xff, 0xff), 4.5), AreaPen = FrozenPen(Color.FromRgb(0x10, 0x60, 0xd0), 1.5),
        CalPen = FrozenPen(Color.FromRgb(0xc0, 0x10, 0xa0), 1.6);
    static readonly Pen MissingPen = Dashed(Color.FromRgb(0xd0, 0x18, 0x18), 1.5), SelPen = Dashed(Color.FromRgb(0x10, 0x60, 0xd0), 1.5);

    public UtView()
    {
        ClipToBounds = true; Focusable = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        Live = new LivePreview(LiveView);
        Live.Changed += InvalidateVisual;
        Cache.Loaded += InvalidateVisual;
        MouseWheel += (s, e) => { ZoomAt(e.GetPosition(this), e.Delta > 0 ? 1 / 1.2 : 1.2); e.Handled = true; };
        MouseDown += (s, e) => { Focus(); if (Press(e.GetPosition(this), e.ChangedButton, e.ClickCount)) { CaptureMouse(); e.Handled = true; } };
        MouseMove += (s, e) => Drag(e.GetPosition(this), e.LeftButton == MouseButtonState.Pressed || e.MiddleButton == MouseButtonState.Pressed || (panFrom != null && e.RightButton == MouseButtonState.Pressed));
        MouseUp += (s, e) => { if (Release(e.GetPosition(this), e.ChangedButton)) e.Handled = true; if (IsMouseCaptured && Mouse.LeftButton == MouseButtonState.Released && Mouse.MiddleButton == MouseButtonState.Released && Mouse.RightButton == MouseButtonState.Released) ReleaseMouseCapture(); };
        MouseLeave += (s, e) => { hover = null; CursorMoved?.Invoke(null); InvalidateVisual(); };
        SizeChanged += (s, e) => { InvalidateVisual(); Live.Schedule(); };
    }

    // ------------------------------------------------------------------ przeliczenia
    double Cx => ActualWidth / 2;
    double Cy => ActualHeight / 2;
    // świat → ekran: u – wzdłuż osi (w prawo), v – w prawo od osi (na ekranie w dół), v rozciągnięte; ekran jest liniowy w (u, v)
    public Point ToScreen(XY p)
    {
        var (u, v) = os.UV(p);
        return new(Cx + (u - cu) / mpp, Cy + (v - cv) * stretch / mpp);
    }
    public XY ToWorld(Point s) => os.World(cu + (s.X - Cx) * mpp, cv + (s.Y - Cy) * mpp / stretch);
    /// <summary>Współrzędne osi punktu: kilometraż wzdłuż osi i odsunięcie w prawo [m układu].</summary>
    public (double U, double V) UV(XY p) => os.UV(p);
    /// <summary>Wektor na ekranie [px] odpowiadający wektorowi w świecie [m w układzie] przy punkcie <paramref name="at"/> (oś po linii – zależy od miejsca).</summary>
    public Vector ScreenVec(XY at, XY d)
    {
        if (os.Prosta)
        {
            double a = os.AzAt(0), c = Math.Cos(a), s = Math.Sin(a);
            return new((d.X * c + d.Y * s) / mpp, (-d.X * s + d.Y * c) * stretch / mpp);
        }
        double len = Math.Sqrt(d.X * d.X + d.Y * d.Y); if (len < 1e-12) return default;
        // różnica centralna na długości odcinka łamanej osi (2 m): cięciwy osi w łuku nie przechylają kierunku
        const double e = 1;
        return (ToScreen(at + d * (e / len)) - ToScreen(at - d * (e / len))) * (len / (2 * e));
    }
    /// <summary>Jednostkowy kierunek na ekranie dla azymutu w świecie przy punkcie <paramref name="at"/>.</summary>
    public Vector ScreenDir(XY at, double azimuth) { var v = ScreenVec(at, Element.Dir(azimuth)); v.Normalize(); return v; }

    void Moved() { InvalidateVisual(); ViewChanged?.Invoke(); Live.Schedule(); OsmAuto(); }
    void SetCenterUV(double u, double v) { cu = u; cv = v; center = os.World(u, v); }
    public void SetView(XY c, double m) { center = c; (cu, cv) = os.UV(c); mpp = Math.Clamp(m, MinMpp, MaxMpp); Moved(); }
    /// <summary>Teren (schemat = false) albo Schemat z osią <paramref name="axis"/> i rozciągnięciem; punkt na środku zostaje.</summary>
    public void SetMode(bool schemat, OsSchematu axis, double str)
    {
        var c = center;
        Schemat = schemat;
        os = schemat ? axis : OsSchematu.ProstaAz(Math.PI / 2); stretch = schemat ? Math.Max(1, str) : 1;
        center = c; (cu, cv) = os.UV(c); osVer++;
        Moved();
    }
    /// <summary>Jak wyżej, z osią prostą pod azymutem [rad].</summary>
    public void SetMode(bool schemat, double axis, double str) => SetMode(schemat, OsSchematu.ProstaAz(axis), str);
    /// <summary>Przybliżenie (k &lt; 1) albo oddalenie (k &gt; 1) z punktem pod kursorem w miejscu.</summary>
    public void ZoomAt(Point s, double k)
    {
        double wu = cu + (s.X - Cx) * mpp, wv = cv + (s.Y - Cy) * mpp / stretch;
        mpp = Math.Clamp(mpp * k, MinMpp, MaxMpp);
        SetCenterUV(wu - (s.X - Cx) * mpp, wv - (s.Y - Cy) * mpp / stretch);
        Moved();
    }
    /// <summary>Przesunięcie obrazu o wektor w pikselach (jak przeciągnięcie myszą).</summary>
    public void PanBy(Vector d) { SetCenterUV(cu - d.X * mpp, cv - d.Y * mpp / stretch); Moved(); }
    /// <summary>Prostokąt (dwa narożniki) w oknie z marginesem.</summary>
    public void FitWorld(XY a, XY b) => FitPoints(new[] { a, b, new XY(a.X, b.Y), new XY(b.X, a.Y) });
    /// <summary>Punkty w oknie z marginesem (w bieżącej osi i rozciągnięciu).</summary>
    public void FitPoints(IReadOnlyList<XY> pts)
    {
        if (pts.Count == 0) return;
        double minU = double.MaxValue, maxU = double.MinValue, minV = double.MaxValue, maxV = double.MinValue;
        foreach (var p in pts) { var (u, v) = os.UV(p); minU = Math.Min(minU, u); maxU = Math.Max(maxU, u); minV = Math.Min(minV, v); maxV = Math.Max(maxV, v); }
        double w = Math.Max(1, ActualWidth - (Schemat ? 0 : RulerLeft) - 40), h = Math.Max(1, ActualHeight - (Schemat ? 0 : RulerTop) - 40);
        double m = Math.Max((maxU - minU) / w, (maxV - minV) * stretch / h);
        mpp = Math.Clamp(Math.Max(m, 0.01), MinMpp, MaxMpp);
        SetCenterUV((minU + maxU) / 2, (minV + maxV) / 2);
        Moved();
    }

    /// <summary>Ramka w układzie obejmująca ekran (przy obrocie i osi po linii – większa): obwód ekranu przeliczony na świat.</summary>
    public (XY Min, XY Max) ViewBoxWorld()
    {
        double w = ActualWidth, h = ActualHeight; int k = os.Prosta ? 1 : 16;
        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        void Add(double sx, double sy) { var q = ToWorld(new Point(sx, sy)); x0 = Math.Min(x0, q.X); y0 = Math.Min(y0, q.Y); x1 = Math.Max(x1, q.X); y1 = Math.Max(y1, q.Y); }
        for (int i = 0; i <= k; i++) { double f = i / (double)k; Add(w * f, 0); Add(w * f, h); Add(0, h * f); Add(w, h * f); }
        return (new XY(x0, y0), new XY(x1, y1));
    }

    /// <summary>Podgląd na żywo: prostokąt PUWG obejmujący ekran (przy obrocie – większy), rozdzielczość w poprzek.</summary>
    (XY TopLeft, double Mpp, int W, int H) LiveView()
    {
        if (Schemat && !SchematBg) return (ToWorld(new Point(0, 0)), mpp, 0, 0);
        var (mn, mx) = ViewBoxWorld(); double m = mpp / stretch;
        return (new XY(mx.X, mn.Y), m, (int)((mx.Y - mn.Y) / m), (int)((mx.X - mn.X) / m));
    }

    // ------------------------------------------------------------------ mysz (wspólne dla myszy i testu)
    Point? downAt, panFrom; MouseButton downBtn; bool dragging;
    XY? areaFrom; Point? bgDragFrom;
    XY? hover;
    Vector bgOffsetPx;
    bool painting;

    /// <summary>Wciśnięcie przycisku; true – płótno przejmuje mysz.</summary>
    public bool Press(Point p, MouseButton b, int clicks = 1)
    {
        if (b == MouseButton.Right && tool == UtTool.Kafelki) { panFrom = p; Cursor = Cursors.SizeAll; return true; }
        if (b == MouseButton.Right) return false;
        if (b == MouseButton.Middle || (b == MouseButton.Left && !IgnoreKeyboard && Keyboard.IsKeyDown(Key.Space))) { panFrom = p; Cursor = Cursors.SizeAll; return true; }
        if (b != MouseButton.Left) return false;
        downAt = p; downBtn = b; dragging = false;
        if (clicks >= 2 && DoubleClick(p)) { downAt = null; return true; }
        if (tool == UtTool.Wskaz && PressMulti(p)) return true;
        if (tool == UtTool.Wskaz && (PressTrackMove(p) || PressVertex(p) || PressDevice(p) || PressGantry(p) || PressObj(p) || PressTurnout(p))) return true;
        if (tool is UtTool.Peron or UtTool.Przejazd or UtTool.Km && ObjToolPress(p)) return true;
        if (tool == UtTool.Obiekt && ObPress(p)) return true;
        if (tool == UtTool.Urzadzenie && DevPress(p)) return true;
        if (tool == UtTool.Rozjazd && RzPress(p)) return true;
        if (tool == UtTool.Kafelki) { painting = true; Paint?.Invoke(ToWorld(p), true); InvalidateVisual(); }
        else if (tool == UtTool.Obszar) areaFrom = ToWorld(p);
        else if (tool == UtTool.Przesun && SelectedBg != null) bgDragFrom = p;
        return true;
    }

    public void Drag(Point p, bool buttonDown)
    {
        hover = ToWorld(p);
        if (tool == UtTool.Szlak && !buttonDown && panFrom == null) SzlakHover(p);
        if (panFrom is Point f)
        {
            if (buttonDown) { PanBy(p - f); panFrom = p; }
            else EndPan();
        }
        else if ((boxFrom != null || ctrlClick != null) && buttonDown) DragMulti(p);
        else if (painting && buttonDown) { Paint?.Invoke(hover.Value, false); InvalidateVisual(); }
        else if (vDrag != null && buttonDown) { DragVertex(p); }
        else if (hDrag != null && buttonDown) { DragHandle(p); }
        else if (rzFrom != null && buttonDown) { rzTo = hover.Value; InvalidateVisual(); }
        else if ((devFrom != null || objFrom != null) && buttonDown) { InvalidateVisual(); }
        else if (obPress is var (_, _, opa) && buttonDown) { if ((p - opa).Length > ClickTol) dragging = true; InvalidateVisual(); }
        else if (objDrag != null && buttonDown) { if (downAt is Point d3 && (p - d3).Length > ClickTol) dragging = true; if (dragging) DragObj(p); }
        else if (devDrag != null && buttonDown) { if (downAt is Point d2 && (p - d2).Length > ClickTol) dragging = true; if (dragging) DragDevice(p); }
        else if (gantryDrag != null && buttonDown) { if (downAt is Point d4 && (p - d4).Length > ClickTol) dragging = true; if (dragging) DragGantry(p); }
        else if (gantryEnd != null && buttonDown) { if (downAt is Point d5 && (p - d5).Length > ClickTol) dragging = true; if (dragging) DragGantryEnd(p); }
        else if (tDrag != null && buttonDown) { if (downAt is Point d1 && (p - d1).Length > ClickTol) dragging = true; if (dragging) DragTrackMove(p); }
        else if (rzDrag != null && buttonDown) { if (downAt is Point d0 && (p - d0).Length > ClickTol) dragging = true; if (dragging) DragTurnout(p); }
        else if (downAt is Point d && buttonDown)
        {
            if ((p - d).Length > ClickTol) dragging = true;
            if (bgDragFrom is Point bf) bgOffsetPx = p - bf;
            else if (areaFrom == null && dragging && PaintMode && (painting2 || Mods.HasFlag(ModifierKeys.Control))) PaintPt(d, p);
            else if (areaFrom == null && dragging && tool is UtTool.Miarka or UtTool.Kalibracja or UtTool.Przesun or UtTool.Wskaz or UtTool.Tor or UtTool.Rozjazd or UtTool.Rownolegly or UtTool.LukOdDo or UtTool.Odcinek or UtTool.LukPunkty or UtTool.Szlak or UtTool.Slad) { panFrom = p; Cursor = Cursors.SizeAll; PanBy(p - d); }
        }
        CursorMoved?.Invoke(hover);
        if (tool != UtTool.Wskaz || vDrag != null || hDrag != null || rzDrag != null || tDrag != null || devDrag != null || gantryDrag != null || gantryEnd != null || objDrag != null) InvalidateVisual();
    }

    public bool Release(Point p, MouseButton b)
    {
        if (b == MouseButton.Right && panFrom != null) { EndPan(); return true; }
        if (b == MouseButton.Right)
        {
            if (tool == UtTool.Miarka && MeasurePts.Count > 0) { MeasurePts.Clear(); MeasureChanged?.Invoke(); InvalidateVisual(); return true; }
            if (tool == UtTool.Kalibracja) { CalCancel?.Invoke(); InvalidateVisual(); return true; }
            if (tool == UtTool.Obszar) { areaFrom = null; InvalidateVisual(); return true; }
            if (tool == UtTool.Tor) { FinishDraft(); return true; }
            if (tool == UtTool.Wskaz && HitObj(p) is var (ok, oid)) { SelectObj(ok, oid); ObjMenu?.Invoke(ok, oid); return true; }
            if (tool == UtTool.Wskaz && HitDevice(p) is string md) { SelectDevice(md); DeviceMenu?.Invoke(md); return true; }
            if (tool == UtTool.Wskaz && HitTurnout(p) is string mt) { SelectTurnout(mt); TurnoutMenu?.Invoke(mt); return true; }
            if (tool == UtTool.Wskaz && HitTrack(p) is string mtr && Track(mtr) is UtTrack mtt)
            {
                // PPM na jednym z zaznaczonych torów – zaznaczenie zostaje (menu: połącz zaznaczone)
                if (!InMulti(Kontrola.KTor, mtr)) { Multi.Clear(); Select(mtr); }
                TrackMenu?.Invoke(mtr, Geometry(mtt).Nearest(ToWorld(p), 0.2).S); return true;
            }
            if (tool == UtTool.Rownolegly) { if (parD != null) parD = null; else parSrc = null; InvalidateVisual(); return true; }
            if (tool == UtTool.LukOdDo && arcFrom != null) { arcFrom = null; InvalidateVisual(); return true; }
            if (tool == UtTool.LukPunkty && fitArc != null) { if (!FitApply()) { fitArc = null; FitPts.Clear(); InvalidateVisual(); } return true; }
            if (tool == UtTool.Szlak && szlakFrom != null) { SzlakCancel(); return true; }
            if (tool == UtTool.Obiekt && obFrom != null) { ObCancel(); return true; }
            if (tool == UtTool.Slad && SladPts.Count > 0) { if (SladPreview != null) SladAccept(); else _ = SladLicz(); return true; }
            return false;
        }
        if (ReleaseMulti(p)) { downAt = null; return true; }
        if (painting2) { downAt = null; EndPaint(); return true; }
        if (panFrom != null) { EndPan(); downAt = null; return true; }
        if (painting) { painting = false; downAt = null; PaintEnd?.Invoke(); InvalidateVisual(); return true; }
        if (vDrag != null) { downAt = null; EndVertexDrag(p); return true; }
        if (hDrag != null) { downAt = null; EndHandleDrag(); return true; }
        if (rzFrom != null) { downAt = null; RzRelease(p); return true; }
        if (rzDrag != null) { downAt = null; EndTurnoutDrag(p); return true; }
        if (tDrag != null) { downAt = null; EndTrackMove(); return true; }
        if (devDrag != null) { downAt = null; EndDeviceDrag(); return true; }
        if (gantryDrag != null) { downAt = null; EndGantryDrag(); return true; }
        if (gantryEnd != null) { downAt = null; EndGantryEndDrag(); return true; }
        if (devFrom != null) { downAt = null; DevRelease(p); return true; }
        if (objDrag != null) { downAt = null; EndObjDrag(); return true; }
        if (objFrom != null) { downAt = null; ObjToolRelease(p); return true; }
        if (obPress != null) { downAt = null; ObRelease(p); return true; }
        if (downAt == null) return false;
        var w = ToWorld(p); downAt = null;
        if (areaFrom is XY a)
        {
            areaFrom = null; InvalidateVisual();
            if (dragging) AreaSelected?.Invoke(a, w);
            return true;
        }
        if (bgDragFrom is Point bf)
        {
            bgDragFrom = null; bgOffsetPx = default;
            var v = p - bf;
            if (v.Length > 0.5) BgDragged?.Invoke(SelectedBg, ToWorld(p) - ToWorld(bf));
            InvalidateVisual(); return true;
        }
        if (dragging) return true;
        if (tool == UtTool.Miarka) { MeasurePts.Add(w); MeasureChanged?.Invoke(); InvalidateVisual(); }
        else if (tool == UtTool.Kalibracja) { CalClick?.Invoke(w); InvalidateVisual(); }
        else if (tool == UtTool.Tor) DraftClick(p);
        else if (tool == UtTool.Rownolegly) ParClick(p);
        else if (tool == UtTool.LukOdDo) ArcClick(p);
        else if (tool == UtTool.LukPunkty) FitClick(p);
        else if (tool == UtTool.Slad) SladClick(p);
        else if (tool == UtTool.Odcinek) OdcClick(p);
        else if (tool == UtTool.Szlak) SzlakClick(p);
        else if (tool == UtTool.Obiekt) DraftInfo?.Invoke($"{Obiekty.Info(ObKind).Name}: kliknij na torze");
        else if (tool == UtTool.Rozjazd) { if (Katalog.Find(RzType)?.IsCrossing == true) CrossingPlace?.Invoke(w); else DraftInfo?.Invoke("Rozjazd: wciśnij na torze w miejscu początku rozjazdu i przeciągnij w stronę odgałęzienia"); }
        else if (tool == UtTool.Wskaz) SelectAt(p);
        return true;
    }

    void EndPan() { panFrom = null; Cursor = tool == UtTool.Wskaz ? null : Cursors.Cross; }

    /// <summary>Test: klik w punkcie ekranu.</summary>
    public void SimClick(Point p, MouseButton b = MouseButton.Left) { Press(p, b); Release(p, b); }
    /// <summary>Test: dwuklik (drugie wciśnięcie z ClickCount = 2).</summary>
    public void SimDoubleClick(Point p) { Press(p, MouseButton.Left); Release(p, MouseButton.Left); Press(p, MouseButton.Left, 2); Release(p, MouseButton.Left); }
    /// <summary>Test: przeciągnięcie myszą z punktu do punktu (kroki co ok. 10 px).</summary>
    public void SimDrag(Point a, Point b, MouseButton btn = MouseButton.Left)
    {
        Press(a, btn);
        int n = Math.Max(5, (int)((b - a).Length / 10));
        for (int i = 1; i <= n; i++) Drag(a + (b - a) * (i / (double)n), true);
        Release(b, btn);
    }

    // ------------------------------------------------------------------ siatka
    /// <summary>Najmniejsza „okrągła” wartość 1 / 2 / 5 · 10ᵏ nie mniejsza niż v.</summary>
    public static double Nice(double v)
    {
        double p = Math.Pow(10, Math.Floor(Math.Log10(v)));
        foreach (var m in new[] { 1.0, 2, 5, 10 }) if (m * p >= v * (1 - 1e-9)) return m * p;
        return 10 * p;
    }
    /// <summary>Krok siatki drobnej (co najmniej 14 px) i głównej (podpisy na linijkach) [m].</summary>
    public (double Minor, double Major) GridSteps()
    {
        double minor = Nice(14 * mpp);
        return (minor, Nice(minor * 4.5));
    }

    // ------------------------------------------------------------------ rysowanie
    /// <summary>Liczba kafelków podkładów narysowanych w ostatniej klatce (do testu).</summary>
    public int DrawnTiles { get; private set; }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight, dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        frame++;
        dc.DrawRectangle(Schemat ? Brushes.White : Paper, null, new Rect(0, 0, w, h));
        if (w < 10 || h < 10) return;
        var (minor, major) = GridSteps();
        var tl = ToWorld(new Point(0, 0)); var br = ToWorld(new Point(w, h));
        if (ShowGrid && !Schemat && !PrintMode) DrawGrid(dc, w, h, minor, major, tl, br);
        if (ShowGridSchemat && Schemat && !PrintMode) DrawGridSchemat(dc, w, h);

        // podgląd na żywo (prostokąt PUWG obejmujący obrócony ekran); w schemacie przygaszony
        if (BgOn && !PrintMode)
        {
            var lv = LiveView(); var ltl = lv.TopLeft; var lbr = new XY(ltl.X - lv.H * lv.Mpp, ltl.Y + lv.W * lv.Mpp);
            if (Schemat) dc.PushOpacity(0.55);
            if (Live.ortoImg is var (ob, og)) DrawImage(dc, ob, og, ob.PixelWidth, ob.PixelHeight, 1);
            foreach (var (tb, tg) in Live.VisibleTiles(ltl, lbr)) DrawImage(dc, tb, tg, tb.PixelWidth, tb.PixelHeight, 1);
            if (Schemat) dc.Pop();
        }

        // podkłady projektu
        int drawn = 0;
        if (Doc != null && ShowBackgrounds && BgOn)
            foreach (var bg in Doc.P.Backgrounds)
            {
                if (!bg.Visible) continue;
                bool moving = bg.Id == SelectedBg && bgDragFrom != null;
                if (moving) dc.PushTransform(new TranslateTransform(bgOffsetPx.X, bgOffsetPx.Y));
                double op = bg.Opacity;
                if (tool == UtTool.Kalibracja && bg.Id == SelectedBg && CalPending != null) op = Math.Min(op, 0.45);   // widać, co jest pod spodem
                foreach (var t in bg.Tiles)
                {
                    if (!Visible(t.Geo, t.W, t.H, w, h)) continue;
                    var file = Doc.Resolve(t.File);
                    if (Cache.IsMissing(file)) { DrawMissing(dc, t, dpi); continue; }
                    double k = t.Geo.Mpp / mpp;      // piksel obrazu na ekranie [px]
                    int level = k >= 1 ? 0 : Math.Clamp((int)Math.Floor(Math.Log2(1 / k)), 0, 7);
                    var bmp = Cache.Get(file, level, t.W);
                    if (bmp != null) { DrawImage(dc, bmp, t.Geo, t.W, t.H, op); drawn++; }
                }
                if (moving) dc.Pop();
            }
        DrawnTiles = drawn;

        // linie kolejowe (OpenRailwayMap) na wierzchu – także nad podkładami projektu
        if (BgOn && Live.Overlay != null && !PrintMode)
        {
            var lv = LiveView(); var ltl = lv.TopLeft; var lbr = new XY(ltl.X - lv.H * lv.Mpp, ltl.Y + lv.W * lv.Mpp);
            if (Schemat) dc.PushOpacity(0.55);
            foreach (var (tb, tg) in Live.VisibleTiles(ltl, lbr, Live.Overlay)) DrawImage(dc, tb, tg, tb.PixelWidth, tb.PixelHeight, 1);
            if (Schemat) dc.Pop();
        }

        if (!PrintMode) DrawOsm(dc);
        if (!PrintMode) DrawSelection(dc);
        DrawMulti(dc);
        if (MarkS is var (mt, ms) && Track(mt) is UtTrack mtr && !PrintMode)
        { var g = Geometry(Dragged(mtr)); if (g.Elements.Count > 0) { var q = ToScreen(g.At(Math.Clamp(ms, 0, g.Length)).P); dc.DrawEllipse(null, new Pen(Brushes.OrangeRed, 2), q, 7, 7); } }
        DrawLevelCrossings(dc, dpi);
        DrawPlatforms(dc, dpi);
        DrawSections(dc, dpi);
        DrawPosts(dc, dpi);
        DrawTracks(dc, dpi);
        DrawObiekty(dc, dpi);
        DrawTurnouts(dc, dpi);
        DrawDevices(dc, dpi);
        DrawKm(dc, dpi);
        DrawGrades(dc, dpi);
        if (PrintMode) return;
        DrawParallel(dc, dpi);
        DrawArcBetween(dc, dpi);
        DrawArcFit(dc, dpi);
        DrawSlad(dc, dpi);
        Overlay?.Invoke(dc);
        DrawCalibration(dc, dpi);
        DrawMeasure(dc, dpi);
        DrawArea(dc, dpi);
        DrawHint(dc, w, dpi);
        DrawScaleBar(dc, h, dpi);
        DrawNorth(dc, w, dpi);
        if (BgOn) DrawAttribution(dc, w, h, dpi);
        if (!Schemat) DrawRulers(dc, w, h, minor, major, tl, br, dpi);
    }

    /// <summary>Macierz piksel obrazu → piksel ekranu (oś prosta).</summary>
    Matrix ScreenMatrix(Affine g)
    {
        // piksel (c, r) → świat: X = D·c + E·r + F, Y = A·c + B·r + C; potem świat → ekran (obrót, rozciągnięcie)
        double a = os.AzAt(0), cosA = Math.Cos(a), sinA = Math.Sin(a), k = stretch / mpp;
        return new((cosA * g.D + sinA * g.A) / mpp, (-sinA * g.D + cosA * g.A) * k,
                   (cosA * g.E + sinA * g.B) / mpp, (-sinA * g.E + cosA * g.B) * k,
                   Cx + (cosA * g.F + sinA * g.C - cu) / mpp, Cy + (-sinA * g.F + cosA * g.C - cv) * k);
    }

    /// <summary>Obwód obrazu na ekranie: narożniki (oś prosta) albo punkty co 1/16 boku (oś po linii – boki wygięte).</summary>
    List<Point> Outline(Affine g, int W, int H)
    {
        int k = os.Prosta ? 1 : 16; var res = new List<Point>(4 * k);
        for (int i = 0; i < k; i++) res.Add(ToScreen(g.World(W * i / (double)k, 0)));
        for (int i = 0; i < k; i++) res.Add(ToScreen(g.World(W, H * i / (double)k)));
        for (int i = 0; i < k; i++) res.Add(ToScreen(g.World(W - W * i / (double)k, H)));
        for (int i = 0; i < k; i++) res.Add(ToScreen(g.World(0, H - H * i / (double)k)));
        return res;
    }

    bool Visible(Affine g, int W, int H, double w, double h)
    {
        var pts = Outline(g, W, H);
        return pts.Max(p => p.X) >= 0 && pts.Min(p => p.X) <= w && pts.Max(p => p.Y) >= 0 && pts.Min(p => p.Y) <= h;
    }

    void DrawImage(DrawingContext dc, BitmapSource bmp, Affine g, int W, int H, double opacity)
    {
        if (!os.Prosta) { DrawImageWarped(dc, bmp, g, W, H, opacity); return; }
        dc.PushTransform(new MatrixTransform(ScreenMatrix(g)));
        if (opacity < 1) dc.PushOpacity(opacity);
        dc.DrawImage(bmp, new Rect(0, 0, W, H));
        if (opacity < 1) dc.Pop();
        dc.Pop();
    }

    // ---- obraz przy osi po linii: kratki ekranu, w każdej przekształcenie afiniczne z narożników
    const int WarpCell = 40;
    XY[,] warpGrid; int warpFrame = -1, frame;
    /// <summary>Punkty świata w narożnikach kratek ekranu (raz na klatkę, wspólne dla wszystkich obrazów).</summary>
    XY WarpCorner(int ix, int iy)
    {
        int nx = (int)Math.Ceiling(ActualWidth / WarpCell) + 1, ny = (int)Math.Ceiling(ActualHeight / WarpCell) + 1;
        if (warpFrame != frame || warpGrid == null || warpGrid.GetLength(0) != nx + 1 || warpGrid.GetLength(1) != ny + 1)
        {
            warpGrid = new XY[nx + 1, ny + 1];
            for (int i = 0; i <= nx; i++) for (int j = 0; j <= ny; j++) warpGrid[i, j] = ToWorld(new Point(i * WarpCell, j * WarpCell));
            warpFrame = frame;
        }
        return warpGrid[ix, iy];
    }

    void DrawImageWarped(DrawingContext dc, BitmapSource bmp, Affine g, int W, int H, double opacity)
    {
        var o = Outline(g, W, H);
        double bx0 = Math.Max(0, o.Min(p => p.X)), bx1 = Math.Min(ActualWidth, o.Max(p => p.X)), by0 = Math.Max(0, o.Min(p => p.Y)), by1 = Math.Min(ActualHeight, o.Max(p => p.Y));
        if (bx0 >= bx1 || by0 >= by1) return;
        if (opacity < 1) dc.PushOpacity(opacity);
        int ix0 = (int)(bx0 / WarpCell), ix1 = (int)Math.Ceiling(bx1 / WarpCell), iy0 = (int)(by0 / WarpCell), iy1 = (int)Math.Ceiling(by1 / WarpCell);
        for (int ix = ix0; ix < ix1; ix++)
            for (int iy = iy0; iy < iy1; iy++)
            {
                var (c00, r00) = g.Pixel(WarpCorner(ix, iy)); var (c10, r10) = g.Pixel(WarpCorner(ix + 1, iy));
                var (c01, r01) = g.Pixel(WarpCorner(ix, iy + 1)); var (c11, r11) = g.Pixel(WarpCorner(ix + 1, iy + 1));
                // kratka poza obrazem
                if (Math.Max(Math.Max(c00, c10), Math.Max(c01, c11)) < 0 || Math.Min(Math.Min(c00, c10), Math.Min(c01, c11)) > W
                    || Math.Max(Math.Max(r00, r10), Math.Max(r01, r11)) < 0 || Math.Min(Math.Min(r00, r10), Math.Min(r01, r11)) > H) continue;
                // ekran → obraz z trzech narożników, odwrócone: obraz → ekran
                double x = ix * WarpCell, y = iy * WarpCell;
                double m11 = (c10 - c00) / WarpCell, m12 = (r10 - r00) / WarpCell, m21 = (c01 - c00) / WarpCell, m22 = (r01 - r00) / WarpCell;
                var m = new Matrix(m11, m12, m21, m22, c00 - x * m11 - y * m21, r00 - x * m12 - y * m22);
                if (!m.HasInverse) continue;
                m.Invert();
                // kratki zachodzą na siebie o 1 px – bez jasnych szczelin na stykach (wygładzanie krawędzi przycięcia)
                dc.PushClip(new RectangleGeometry(new Rect(x, y, WarpCell + 1, WarpCell + 1)));
                dc.PushTransform(new MatrixTransform(m));
                dc.DrawImage(bmp, new Rect(0, 0, W, H));
                dc.Pop(); dc.Pop();
            }
        if (opacity < 1) dc.Pop();
    }

    StreamGeometry Quad(Affine g, int W, int H)
    {
        var pts = Outline(g, W, H); var sg = new StreamGeometry();
        using (var c = sg.Open())
        {
            c.BeginFigure(pts[0], false, true);
            foreach (var q in pts.Skip(1)) c.LineTo(q, true, false);
        }
        return sg;
    }

    void DrawMissing(DrawingContext dc, UtTile t, double dpi)
    {
        dc.DrawGeometry(null, MissingPen, Quad(t.Geo, t.W, t.H));
        var c = ToScreen(t.Geo.World(t.W / 2.0, t.H / 2.0));
        var tx = Text("brak pliku: " + System.IO.Path.GetFileName(t.File), 11, Red, dpi, UiFont);
        dc.DrawText(tx, new Point(c.X - tx.Width / 2, c.Y - tx.Height / 2));
    }

    void DrawSelection(DrawingContext dc)
    {
        var bg = Doc?.P.Backgrounds.FirstOrDefault(b => b.Id == SelectedBg);
        if (bg == null || bg.Tiles.Count == 0) return;
        if (bgDragFrom != null) dc.PushTransform(new TranslateTransform(bgOffsetPx.X, bgOffsetPx.Y));
        if (bg.Grid is UtGrid g && !g.IsGlobal) dc.DrawGeometry(null, SelPen, Quad(Affine.NorthUp(g.X0, g.Y0, g.Mpp), g.Cols * g.TileSize, g.Rows * g.TileSize));
        else if (bg.Grid is UtGrid gg)
        {
            // mozaika: tylko obwód (krawędzie kafelków bez sąsiada), bez kresek między kafelkami
            var have = bg.Tiles.Select(t => (t.Row, t.Col)).ToHashSet();
            foreach (var t in bg.Tiles)
            {
                Point P(double c, double r) => ToScreen(t.Geo.World(c, r));
                int n = t.W;
                if (!have.Contains((t.Row - 1, t.Col))) dc.DrawLine(SelPen, P(0, 0), P(n, 0));
                if (!have.Contains((t.Row + 1, t.Col))) dc.DrawLine(SelPen, P(0, n), P(n, n));
                if (!have.Contains((t.Row, t.Col - 1))) dc.DrawLine(SelPen, P(0, 0), P(0, n));
                if (!have.Contains((t.Row, t.Col + 1))) dc.DrawLine(SelPen, P(n, 0), P(n, n));
            }
        }
        else foreach (var t in bg.Tiles) dc.DrawGeometry(null, SelPen, Quad(t.Geo, t.W, t.H));
        if (bgDragFrom != null) dc.Pop();
    }

    void DrawCalibration(DrawingContext dc, double dpi)
    {
        var bg = Doc?.P.Backgrounds.FirstOrDefault(b => b.Id == SelectedBg);
        if (bg?.Calibration == null || bg.Tiles.Count == 0 || tool != UtTool.Kalibracja) return;
        var g = bg.Tiles[0].Geo; int i = 0;
        foreach (var cp in bg.Calibration)
        {
            i++;
            var src = ToScreen(g.World(cp.Col, cp.Row)); var dst = ToScreen(cp.Target);
            dc.DrawLine(CalPen, src, dst);
            dc.DrawEllipse(null, CalPen, src, 5, 5);
            Cross(dc, dst, 7, CalPen);
            Label(dc, i.ToString(), dst + new Vector(8, -18), Magenta, dpi);
        }
        if (CalPending is var (pc, pr))
        {
            var s = ToScreen(g.World(pc, pr));
            dc.DrawEllipse(null, CalPen, s, 6, 6);
            if (hover is XY hv) dc.DrawLine(CalPen, s, ToScreen(hv));
            Label(dc, $"{bg.Calibration.Count + 1}: wskaż miejsce docelowe", s + new Vector(9, 6), Magenta, dpi);
        }
    }

    void DrawMeasure(DrawingContext dc, double dpi)
    {
        if (MeasurePts.Count == 0 && tool != UtTool.Miarka) return;
        var pts = MeasurePts.Select(ToScreen).ToList();
        bool rubber = tool == UtTool.Miarka && hover != null && MeasurePts.Count > 0;
        if (rubber) pts.Add(ToScreen(hover.Value));
        for (int i = 1; i < pts.Count; i++) { dc.DrawLine(HaloPen, pts[i - 1], pts[i]); dc.DrawLine(MeasurePen, pts[i - 1], pts[i]); }
        foreach (var p in pts.Take(MeasurePts.Count)) dc.DrawRectangle(Red, null, new Rect(p.X - 3, p.Y - 3, 6, 6));
        var all = rubber ? MeasurePts.Append(hover.Value).ToList() : MeasurePts;
        for (int i = 1; i < all.Count; i++)
        {
            var m = new Point((pts[i - 1].X + pts[i].X) / 2, (pts[i - 1].Y + pts[i].Y) / 2);
            if ((pts[i] - pts[i - 1]).Length < 40) continue;
            Label(dc, Len3(Geo.GroundDistance(all[i - 1], all[i])), m + new Vector(6, -18), Red, dpi);
        }
        if (all.Count > 2) Label(dc, "Σ " + Len3(Length(all)), pts[^1] + new Vector(10, 4), Red, dpi);
    }

    /// <summary>Długość łamanej w terenie [m].</summary>
    public static double Length(IReadOnlyList<XY> pts) { double s = 0; for (int i = 1; i < pts.Count; i++) s += Geo.GroundDistance(pts[i - 1], pts[i]); return s; }

    void DrawArea(DrawingContext dc, double dpi)
    {
        if (areaFrom is not XY a || hover is not XY b) return;
        var r = new Rect(ToScreen(a), ToScreen(b));
        dc.DrawRectangle(AreaFill, AreaPen, r);
        Label(dc, $"{Len(Geo.GroundDistance(a, new XY(a.X, b.Y)))} × {Len(Geo.GroundDistance(a, new XY(b.X, a.Y)))}", r.BottomRight + new Vector(6, 2), Blue, dpi);
    }

    static void Cross(DrawingContext dc, Point p, double r, Pen pen) { dc.DrawLine(pen, p + new Vector(-r, 0), p + new Vector(r, 0)); dc.DrawLine(pen, p + new Vector(0, -r), p + new Vector(0, r)); }

    static void Label(DrawingContext dc, string s, Point at, Brush b, double dpi)
    {
        var t = Text(s, 12, b, dpi, UiFont);
        dc.DrawRoundedRectangle(Halo, null, new Rect(at.X - 3, at.Y - 1, t.Width + 6, t.Height + 2), 3, 3);
        dc.DrawText(t, at);
    }

    void DrawGrid(DrawingContext dc, double w, double h, double minor, double major, XY tl, XY br)
    {
        foreach (var step in new[] { minor, major })
        {
            var pen = step == major ? MajorPen : MinorPen;
            for (double y = Math.Ceiling(tl.Y / step) * step; y <= br.Y; y += step)
            {
                if (step == minor && IsMul(y, major)) continue;
                double sx = Crisp(ToScreen(new XY(0, y)).X); dc.DrawLine(pen, new Point(sx, 0), new Point(sx, h));
            }
            for (double x = Math.Ceiling(br.X / step) * step; x <= tl.X; x += step)
            {
                if (step == minor && IsMul(x, major)) continue;
                double sy = Crisp(ToScreen(new XY(x, 0)).Y); dc.DrawLine(pen, new Point(0, sy), new Point(w, sy));
            }
        }
    }

    /// <summary>
    /// Siatka schematu: linie w poprzek osi co okrągłe metry wzdłuż osi i linie wzdłuż osi co okrągłe metry w poprzek (z rozciągnięciem),
    /// odstęp co najmniej 14 px; co któraś grubsza.
    /// </summary>
    void DrawGridSchemat(DrawingContext dc, double w, double h)
    {
        double uc = cu, vc = cv;
        double minU = Nice(14 * mpp), majU = Nice(minU * 4.5);
        double minV = Nice(14 * mpp / stretch), majV = Nice(minV * 4.5);
        double u0 = uc - Cx * mpp, u1 = uc + (w - Cx) * mpp, v0 = vc - Cy * mpp / stretch, v1 = vc + (h - Cy) * mpp / stretch;
        for (double u = Math.Ceiling(u0 / minU) * minU; u <= u1; u += minU)
        {
            double x = Crisp(Cx + (u - uc) / mpp);
            dc.DrawLine(IsMul(u, majU) ? MajorPen : MinorPen, new Point(x, 0), new Point(x, h));
        }
        for (double v = Math.Ceiling(v0 / minV) * minV; v <= v1; v += minV)
        {
            double y = Crisp(Cy + (v - vc) * stretch / mpp);
            dc.DrawLine(IsMul(v, majV) ? MajorPen : MinorPen, new Point(0, y), new Point(w, y));
        }
    }

    /// <summary>Kroki siatki schematu [m]: wzdłuż (drobny, główny) i w poprzek (drobny, główny).</summary>
    public (double MinU, double MajU, double MinV, double MajV) GridStepsSchemat()
    {
        double minU = Nice(14 * mpp), minV = Nice(14 * mpp / stretch);
        return (minU, Nice(minU * 4.5), minV, Nice(minV * 4.5));
    }

    static bool IsMul(double v, double step) { double r = Math.Abs(v / step - Math.Round(v / step)); return r < 1e-6; }
    static double Crisp(double v) => Math.Round(v) + 0.5;

    void DrawRulers(DrawingContext dc, double w, double h, double minor, double major, XY tl, XY br, double dpi)
    {
        dc.DrawRectangle(RulerBg, null, new Rect(0, 0, w, RulerTop));
        dc.DrawRectangle(RulerBg, null, new Rect(0, 0, RulerLeft, h));
        int dec = Math.Max(0, (int)Math.Ceiling(-Math.Log10(major) - 1e-9));
        // linijka górna: Y (wschód)
        double lastLabel = double.NegativeInfinity;
        for (double y = Math.Ceiling(tl.Y / minor) * minor; y <= br.Y; y += minor)
        {
            double sx = Crisp(ToScreen(new XY(0, y)).X); if (sx < RulerLeft) continue;
            bool big = IsMul(y, major);
            dc.DrawLine(TickPen, new Point(sx, RulerTop - (big ? 9 : 4)), new Point(sx, RulerTop));
            if (!big) continue;
            var t = Text(Geo.M(y, dec), 10.5, Ink, dpi, MonoFont);
            if (sx - t.Width / 2 < lastLabel + 8 || sx + t.Width / 2 > w - 4) continue;
            dc.DrawText(t, new Point(sx - t.Width / 2, 1)); lastLabel = sx + t.Width / 2;
        }
        // linijka lewa: X (północ)
        lastLabel = double.NegativeInfinity;
        for (double x = Math.Ceiling(br.X / minor) * minor; x <= tl.X; x += minor)
        {
            double sy = Crisp(ToScreen(new XY(x, 0)).Y); if (sy < RulerTop) continue;
            bool big = IsMul(x, major);
            dc.DrawLine(TickPen, new Point(RulerLeft - (big ? 9 : 4), sy), new Point(RulerLeft, sy));
            if (!big) continue;
            var t = Text(Geo.M(x, dec), 10.5, Ink, dpi, MonoFont);
            if (sy - t.Height / 2 < RulerTop || sy + t.Height / 2 > h - 2) continue;
            if (Math.Abs(sy - lastLabel) < t.Height + 4) continue;
            dc.DrawText(t, new Point(RulerLeft - 11 - t.Width, sy - t.Height / 2)); lastLabel = sy;
        }
        dc.DrawLine(EdgePen, new Point(RulerLeft, RulerTop - 0.5), new Point(w, RulerTop - 0.5));
        dc.DrawLine(EdgePen, new Point(RulerLeft - 0.5, RulerTop), new Point(RulerLeft - 0.5, h));
        dc.DrawRectangle(RulerBg, null, new Rect(0, 0, RulerLeft, RulerTop));
        var c = Text("Y →", 10, Hint, dpi, UiFont); dc.DrawText(c, new Point(RulerLeft - c.Width - 4, 4));
        var c2 = Text("X ↑", 10, Hint, dpi, UiFont); dc.DrawText(c2, new Point(4, 4));
    }

    void DrawScaleBar(DrawingContext dc, double h, double dpi)
    {
        double len = Nice(110 * mpp); double px = len / mpp;
        double x0 = (Schemat ? 0 : RulerLeft) + 16, y0 = h - 18;
        var t = Text(Len(len) + (Schemat && stretch > 1 ? $"   (w poprzek ×{stretch:0.#})" : ""), 11, Ink, dpi, UiFont);
        dc.DrawRoundedRectangle(Halo, null, new Rect(x0 - 6, y0 - 12, px + t.Width + 20, 22), 3, 3);
        dc.DrawLine(BarPen, new Point(x0, y0), new Point(x0 + px, y0));
        dc.DrawLine(BarPen, new Point(x0, y0 - 5), new Point(x0, y0 + 1)); dc.DrawLine(BarPen, new Point(x0 + px, y0 - 5), new Point(x0 + px, y0 + 1));
        dc.DrawText(t, new Point(x0 + px + 6, y0 - t.Height / 2 - 2));
    }

    void DrawNorth(DrawingContext dc, double w, double dpi)
    {
        double x = w - 26, y = RulerTop + 14;
        dc.DrawEllipse(Halo, null, new Point(x, y + 16), 14, 18);
        // strzałka w kierunku północy na ekranie (w schemacie obrócona)
        var nv = ScreenVec(center, new XY(1, 0)); double rot = Math.Atan2(nv.X, -nv.Y) * 180 / Math.PI;
        dc.PushTransform(new RotateTransform(rot, x, y + 11));
        var g = new StreamGeometry();
        using (var c = g.Open()) { c.BeginFigure(new Point(x, y), true, true); c.LineTo(new Point(x + 6, y + 18), true, false); c.LineTo(new Point(x, y + 14), true, false); c.LineTo(new Point(x - 6, y + 18), true, false); }
        dc.DrawGeometry(Ink, null, g);
        dc.Pop();
        var t = Text("N", 11, Ink, dpi, UiFont); dc.DrawText(t, new Point(x - t.Width / 2, y + 19));
    }

    void DrawAttribution(DrawingContext dc, double w, double h, double dpi)
    {
        var parts = new List<string>();
        if (Live.Source != LivePreview.None) parts.Add(Live.Attribution);
        if (Live.Overlay != null) parts.Add(LivePreview.OrmAttribution);
        if (Doc != null && ShowBackgrounds) parts.AddRange(Doc.P.Backgrounds.Where(b => b.Visible && b.Source.Length > 0).Select(b => b.Source));
        var s = string.Join(" · ", parts.Distinct());
        if (Live.Status.Length > 0) s = Live.Status + (s.Length > 0 ? "   ·   " + s : "");
        if (s.Length == 0) return;
        var t = Text(s, 10.5, Ink, dpi, UiFont);
        dc.DrawRectangle(Halo, null, new Rect(w - t.Width - 10, h - t.Height - 4, t.Width + 10, t.Height + 4));
        dc.DrawText(t, new Point(w - t.Width - 5, h - t.Height - 2));
    }

    void DrawHint(DrawingContext dc, double w, double dpi)
    {
        if (Doc == null || !ShowBackgrounds || Doc.P.Backgrounds.Count > 0 || mpp < 40) return;
        var t = Text("Pusty projekt. W polu „Punkt” wpisz współrzędne obszaru: z Google Maps (np. 51.7631, 19.5438) albo X Y w PUWG 1992.", 12.5, Hint, dpi, UiFont);
        dc.DrawText(t, new Point(Math.Max(RulerLeft + 10, (w - t.Width) / 2), RulerTop + 12));
    }

    public static string Len(double m) => m >= 1000 ? (m / 1000).ToString("0.###", Pl) + " km" : m >= 1 ? m.ToString("0.##", Pl) + " m" : (m * 100).ToString("0.#", Pl) + " cm";
    /// <summary>Długość z dokładnością do 1 cm.</summary>
    public static string Len3(double m) => m.ToString("#,0.00", Pl).Replace(' ', ' ') + " m";

    static readonly CultureInfo Pl = CultureInfo.GetCultureInfo("pl-PL");
    static FormattedText Text(string s, double size, Brush b, double dpi, Typeface f) => new(s, Pl, FlowDirection.LeftToRight, f, size, b, dpi);
    static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
    static Pen FrozenPen(Color c, double t) { var p = new Pen(Frozen(c), t) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }; p.Freeze(); return p; }
    static Pen Dashed(Color c, double t) { var p = new Pen(Frozen(c), t) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) }; p.Freeze(); return p; }

    /// <summary>
    /// Linia kreskowana przycięta do widoku (z marginesem): WPF dzieli kreskowaną linię na kreski na całej długości, więc punkt
    /// daleko poza ekranem (np. przecięcie prawie równoległych prostych) zawieszał okno (2026-10-21).
    /// </summary>
    void DashLine(DrawingContext dc, Pen pen, Point a, Point b)
    {
        if (double.IsNaN(a.X + a.Y + b.X + b.Y) || double.IsInfinity(a.X + a.Y + b.X + b.Y)) return;
        double x0 = -50, y0 = -50, x1 = ActualWidth + 50, y1 = ActualHeight + 50, t0 = 0, t1 = 1, dx = b.X - a.X, dy = b.Y - a.Y;
        foreach (var (pp, q) in new[] { (-dx, a.X - x0), (dx, x1 - a.X), (-dy, a.Y - y0), (dy, y1 - a.Y) })
        {
            if (Math.Abs(pp) < 1e-12) { if (q < 0) return; continue; }
            double r = q / pp;
            if (pp < 0) { if (r > t1) return; if (r > t0) t0 = r; }
            else { if (r < t0) return; if (r < t1) t1 = r; }
        }
        dc.DrawLine(pen, new Point(a.X + t0 * dx, a.Y + t0 * dy), new Point(a.X + t1 * dx, a.Y + t1 * dy));
    }
}

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Wykres krzywizny zaznaczonego toru (pod mapą): krzywizna 1000/R od kilometrażu (łuk w prawo w górę), etykiety R i L, uwagi
/// (<see cref="Krzywizna.Check"/>) czerwonymi znacznikami. Tor z elementów (<see cref="UtAlign"/>) edytuje się tutaj: przeciągnięcie
/// granicy elementów w poziomie zmienia długości dwóch sąsiednich elementów, przeciągnięcie poziomu łuku w pionie – promień
/// (przejściowe dopasowują się same), PPM – podział elementu, wstawienie przejściowej, usunięcie, zamiana prosta / łuk.
/// Kółko myszy – przybliżenie wzdłuż km, przeciągnięcie pustego miejsca – przesuwanie.
/// Ślad (narzędzie Ślad, § 5h) – pomarańczowa krzywizna śladu i łuki rozpoznane z niej (R, L, kąt): na km zaznaczonego toru,
/// gdy ślad biegnie wzdłuż niego, inaczej – na km śladu (bez toru).
/// </summary>
public class KrzywiznaView : FrameworkElement
{
    public UtTrack Track { get; private set; }
    TrasaResult res; List<KrzywiznaOdc> prof = new(); List<KrzywiznaUwaga> uwagi = new();
    double s0View, s1View;            // widoczny zakres km
    double kMax = 1.0 / 2000;
    /// <summary>Podgląd w trakcie przeciągania (tor z elementów).</summary>
    public event Action<UtAlign> Preview;
    /// <summary>Zatwierdzenie zmiany trasy z elementów (nazwa do cofania).</summary>
    public event Action<UtAlign, string> Commit;
    /// <summary>Prośba o zamianę wieloboku na trasę z elementów.</summary>
    public event Action ToAlign;
    /// <summary>Km pod kursorem (do pokazania miejsca na mapie), null – poza wykresem.</summary>
    public event Action<double?> HoverS;
    public double? CursorS { get; set; }
    public string LastInfo { get; private set; } = "";
    List<(double S, double K)> slad = new();
    List<(double S0, double S1, string Lab, double K)> sladLuki = new();
    double sladLen;
    /// <summary>Krzywizna śladu na wykresie (liczba próbek) – do testu.</summary>
    public int SladCount => slad.Count;
    /// <summary>Podpisy łuków śladu – do testu.</summary>
    public IReadOnlyList<string> SladLabels => sladLuki.Select(x => x.Lab).ToList();
    double Total => res?.Length ?? sladLen;
    /// <summary>Widoczny zakres km [m] – do testu.</summary>
    public (double S0, double S1) ViewRange => (s0View, s1View);

    const double Left = 56, Right = 12, Top = 18, Bottom = 22;
    static readonly CultureInfo Pl = CultureInfo.GetCultureInfo("pl-PL");
    static readonly Typeface Ui = new("Segoe UI");

    public KrzywiznaView() { ClipToBounds = true; Focusable = true; }

    public void Show(UtTrack t, TrasaResult r)
    {
        bool same = Track?.Id == t?.Id;
        Track = t; res = r;
        prof = r != null ? Krzywizna.Profile(r) : new(); uwagi = t != null && r != null ? Krzywizna.Check(t, r) : new();
        double L = r?.Length ?? 0;
        if (!same || s1View <= s0View || s1View > L + 1) { s0View = 0; s1View = Math.Max(L, 1); }
        kMax = Math.Max(1.0 / 2000, prof.Count > 0 ? prof.Max(p => Math.Max(Math.Abs(p.K0), Math.Abs(p.K1))) * 1.15 : 0);
        FitSladK();
        InvalidateVisual();
    }

    /// <summary>
    /// Krzywizna śladu (km – na osi wykresu: zaznaczonego toru albo śladu) i łuki rozpoznane ze śladu; pusta lista – bez śladu.
    /// <paramref name="len"/> – długość śladu (oś, gdy nie ma toru).
    /// </summary>
    public void ShowSlad(List<(double S, double K)> pts, List<(double S0, double S1, string Lab, double K)> luki, double len)
    {
        slad = pts ?? new(); sladLuki = luki ?? new(); sladLen = len;
        if (res == null && slad.Count > 0 && (s1View - s0View < 5 || s1View > len + 1 || s0View > len)) { s0View = 0; s1View = Math.Max(len, 1); }
        if (res == null && slad.Count == 0) { s0View = 0; s1View = 1; }
        FitSladK();
        InvalidateVisual();
    }

    void FitSladK()
    {
        if (slad.Count == 0) return;
        // skala do 98 % próbek (pojedyncze skoki szumu nie spłaszczają wykresu), najwyżej R 100
        var a = slad.Select(x => Math.Abs(x.K)).OrderBy(x => x).ToList();
        double q = Math.Min(a[(int)((a.Count - 1) * 0.98)], 1.0 / 100);
        kMax = Math.Max(kMax, q * 1.15);
    }

    double X(double s) => Left + (s - s0View) / Math.Max(1e-9, s1View - s0View) * (ActualWidth - Left - Right);
    double S(double x) => s0View + (x - Left) / Math.Max(1, ActualWidth - Left - Right) * (s1View - s0View);
    double Y(double k) => Top + (ActualHeight - Top - Bottom) / 2 - k / kMax * (ActualHeight - Top - Bottom) / 2;
    double K(double y) => -(y - Top - (ActualHeight - Top - Bottom) / 2) / ((ActualHeight - Top - Bottom) / 2) * kMax;

    FormattedText Text(string s, double size, Brush b) => new(s, Pl, FlowDirection.LeftToRight, Ui, size, b, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        dc.DrawRectangle(Brushes.White, new Pen(Brushes.Gainsboro, 1), new Rect(0, 0, w, h));
        if ((Track == null || res == null || prof.Count == 0) && slad.Count == 0)
        {
            dc.DrawText(Text("Wykres krzywizny – zaznacz tor (albo narysuj ślad narzędziem Ślad, F)", 12, Brushes.Gray), new Point(10, 8));
            return;
        }
        var axis = new Pen(Brushes.Silver, 1);
        dc.DrawLine(axis, new Point(Left, Y(0)), new Point(w - Right, Y(0)));
        dc.DrawLine(axis, new Point(Left, Top), new Point(Left, h - Bottom));
        // podziałka krzywizny (1000/R) i km
        foreach (double kk in new[] { kMax * 0.9, -kMax * 0.9 })
        {
            double y = Y(kk); dc.DrawLine(new Pen(Brushes.Gainsboro, 1) { DashStyle = DashStyles.Dot }, new Point(Left, y), new Point(w - Right, y));
            dc.DrawText(Text($"R {1 / Math.Abs(kk):0}", 10, Brushes.Gray), new Point(4, y - 7));
        }
        dc.DrawText(Text("krzywizna (w prawo ↑)", 10, Brushes.Gray), new Point(4, 2));
        double step = NiceStep((s1View - s0View) / 8);
        for (double s = Math.Ceiling(s0View / step) * step; s <= s1View; s += step)
        {
            double x = X(s); dc.DrawLine(axis, new Point(x, h - Bottom), new Point(x, h - Bottom + 4));
            dc.DrawText(Text($"km {s:0.#}", 10, Brushes.Gray), new Point(x - 14, h - Bottom + 4));
        }
        // krzywizna
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(0x10, 0x50, 0xb0)), 2);
        var geo = new StreamGeometry();
        using (var c = geo.Open())
        {
            bool first = true;
            foreach (var p in prof)
            {
                var a = new Point(X(p.S0), Y(p.K0)); var b = new Point(X(p.S1), Y(p.K1));
                if (first) { c.BeginFigure(a, false, false); first = false; } else c.LineTo(a, true, false);   // skok – pionowa kreska
                c.LineTo(b, true, false);
            }
        }
        geo.Freeze(); dc.DrawGeometry(null, pen, geo);
        DrawSlad(dc, w, h);
        // etykiety elementów i uchwyty
        bool edit = Track?.Align != null;
        var hb = new SolidColorBrush(Color.FromRgb(0xff, 0x90, 0x00));
        foreach (var p in prof)
        {
            double xm = (X(p.S0) + X(p.S1)) / 2, len = p.S1 - p.S0;
            if (X(p.S1) - X(p.S0) < 28) continue;
            string lab = p.Kind == ElKind.Luk ? $"R {1 / Math.Abs(p.K0):0}" : p.Kind == ElKind.Przejsciowa ? $"L {len:0}" : $"{len:0} m";
            var ft = Text(lab, 10.5, p.Kind == ElKind.Prosta ? Brushes.Gray : Brushes.Black);
            double ym = Y((p.K0 + p.K1) / 2) + (p.K0 + p.K1 >= 0 ? -15 : 3);
            dc.DrawText(ft, new Point(xm - ft.Width / 2, Math.Clamp(ym, Top - 14, h - Bottom - 14)));
            if (edit && p.Kind == ElKind.Luk) dc.DrawEllipse(hb, null, new Point(xm, Y(p.K0)), 5, 5);
        }
        if (edit)
            for (int i = 1; i < prof.Count; i++) { var x = X(prof[i].S0); dc.DrawRectangle(hb, null, new Rect(x - 3, Y(prof[i].K0) - 3, 6, 6)); }
        // uwagi
        foreach (var u in uwagi)
        {
            double x = X(u.S); if (x < Left || x > w - Right) continue;
            var tri = new StreamGeometry(); using (var c = tri.Open()) { c.BeginFigure(new Point(x, h - Bottom - 1), true, true); c.LineTo(new Point(x - 5, h - Bottom - 10), true, false); c.LineTo(new Point(x + 5, h - Bottom - 10), true, false); }
            tri.Freeze(); dc.DrawGeometry(Brushes.Crimson, null, tri);
        }
        if (uwagi.Count > 0) dc.DrawText(Text($"uwagi: {uwagi.Count} – " + uwagi[0].Text + (uwagi.Count > 1 ? " …" : ""), 10.5, Brushes.Crimson), new Point(Left + 4, h - Bottom - 26));
        else dc.DrawText(Text(Track == null ? "ślad – krzywizna z punktów (km śladu); poziomo – łuk, pochyło – krzywa przejściowa, zero – prosta" : edit ? "tor z elementów – przeciągaj granice (■) i poziom łuków (●), PPM – więcej" : "wielobok – PPM: zamień na trasę z elementów, żeby edytować tutaj", 10.5, Brushes.Gray), new Point(Left + 4, h - Bottom - 26));
        if (CursorS is double cs && cs >= s0View && cs <= s1View) dc.DrawLine(new Pen(Brushes.OrangeRed, 1), new Point(X(cs), Top), new Point(X(cs), h - Bottom));
    }

    static readonly Brush SladInk = Frz(Color.FromRgb(0xff, 0x6a, 0x00));
    static Brush Frz(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    void DrawSlad(DrawingContext dc, double w, double h)
    {
        if (slad.Count == 0) return;
        var g = new StreamGeometry(); bool open = false; double lastS = double.NaN;
        using (var c = g.Open())
            foreach (var (s, k) in slad)
            {
                if (s < s0View || s > s1View) { open = false; continue; }
                var p = new Point(X(s), Y(Math.Clamp(k, -kMax * 1.05, kMax * 1.05)));
                // przerwa (ślad obok toru, krawędź wykresu) – nowa linia
                if (!open || Math.Abs(s - lastS) > 5) { c.BeginFigure(p, false, false); open = true; } else c.LineTo(p, true, false);
                lastS = s;
            }
        g.Freeze(); dc.DrawGeometry(null, new Pen(SladInk, 1.4), g);
        var dash = new Pen(SladInk, 1) { DashStyle = DashStyles.Dash };
        foreach (var (s0, s1, lab, k) in sladLuki)
        {
            if (s1 < s0View || s0 > s1View) continue;
            double x0 = X(s0), x1 = X(s1), y = Y(k);
            dc.DrawLine(dash, new Point(x0, Top), new Point(x0, h - Bottom)); dc.DrawLine(dash, new Point(x1, Top), new Point(x1, h - Bottom));
            // pod poziomem łuku śladu (nad nim – podpis R toru)
            var ft = Text(lab, 10.5, SladInk); double ty = Math.Clamp(y + (k >= 0 ? 18 : -32), Top, h - Bottom - 40);
            dc.DrawRectangle(Brushes.White, null, new Rect((x0 + x1) / 2 - ft.Width / 2 - 2, ty, ft.Width + 4, ft.Height));
            dc.DrawText(ft, new Point((x0 + x1) / 2 - ft.Width / 2, ty));
        }
    }

    /// <summary>Uchwyt granicy przed elementem <paramref name="ei"/> (punkt na wykresie) – do testu.</summary>
    public Point? BoundaryPoint(int ei) { var p = prof.FirstOrDefault(x => x.Index == ei); return p == null ? null : new Point(X(p.S0), Y(p.K0)); }
    /// <summary>Uchwyt poziomu łuku <paramref name="ei"/> – do testu.</summary>
    public Point? ArcPoint(int ei) { var p = prof.FirstOrDefault(x => x.Index == ei && x.Kind == ElKind.Luk); return p == null ? null : new Point((X(p.S0) + X(p.S1)) / 2, Y(p.K0)); }
    /// <summary>Punkt wykresu dla km i krzywizny – do testu.</summary>
    public Point At(double s, double k) => new(X(s), Y(k));
    public IReadOnlyList<KrzywiznaUwaga> Uwagi => uwagi;

    static double NiceStep(double v) { double p = Math.Pow(10, Math.Floor(Math.Log10(Math.Max(v, 1e-6)))); foreach (var m in new[] { 1, 2, 5, 10 }) if (m * p >= v) return m * p; return 10 * p; }

    // ------------------------------------------------------------------ mysz
    enum Drag { None, Pan, Boundary, Level }
    Drag drag; int dragIdx; Point dragFrom; UtAlign dragBase; double panS0, panS1; UtAlign dragNow;

    /// <summary>Uchwyt pod kursorem: granica (indeks elementu za granicą) albo poziom łuku (indeks elementu).</summary>
    (Drag, int) HitHandle(Point p)
    {
        if (Track?.Align == null) return (Drag.None, -1);
        for (int i = 0; i < prof.Count; i++)
            if (prof[i].Kind == ElKind.Luk && (new Point((X(prof[i].S0) + X(prof[i].S1)) / 2, Y(prof[i].K0)) - p).Length <= 7) return (Drag.Level, prof[i].Index);
        for (int i = 1; i < prof.Count; i++)
            if (Math.Abs(X(prof[i].S0) - p.X) <= 5) return (Drag.Boundary, prof[i].Index);
        return (Drag.None, -1);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { Focus(); PressAt(e.GetPosition(this)); CaptureMouse(); e.Handled = true; }
    protected override void OnMouseMove(MouseEventArgs e) { MoveTo(e.GetPosition(this), e.LeftButton == MouseButtonState.Pressed); }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { ReleaseAt(e.GetPosition(this)); ReleaseMouseCapture(); e.Handled = true; }
    protected override void OnMouseLeave(MouseEventArgs e) { HoverS?.Invoke(null); }
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if ((Track == null || res == null) && slad.Count == 0) return;
        double s = S(e.GetPosition(this).X), f = e.Delta > 0 ? 0.8 : 1.25, L = Total;
        double a = s - (s - s0View) * f, b = s + (s1View - s) * f;
        if (b - a > L) { a = 0; b = L; }
        s0View = Math.Max(0, a); s1View = Math.Min(L, Math.Max(s0View + 5, b)); InvalidateVisual(); e.Handled = true;
    }

    /// <summary>Wciśnięcie (mysz albo test).</summary>
    public void PressAt(Point p)
    {
        LastInfo = "";
        var (d, i) = HitHandle(p);
        dragFrom = p; drag = d == Drag.None ? Drag.Pan : d; dragIdx = i; dragBase = Track?.Align?.Clone(); dragNow = null; panS0 = s0View; panS1 = s1View;
    }

    public void MoveTo(Point p, bool down)
    {
        if (Track != null && res != null) { var s = S(p.X); HoverS?.Invoke(s >= 0 && s <= res.Length ? s : null); }
        if (!down || drag == Drag.None) return;
        if (drag == Drag.Pan)
        {
            double ds = (p.X - dragFrom.X) / Math.Max(1, ActualWidth - Left - Right) * (panS1 - panS0), L = Total;
            double a = Math.Clamp(panS0 - ds, 0, Math.Max(0, L - (panS1 - panS0)));
            s0View = a; s1View = a + (panS1 - panS0); InvalidateVisual(); return;
        }
        if (dragBase == null) return;
        var al = dragBase.Clone(); var els = al.Els;
        if (drag == Drag.Boundary && dragIdx > 0 && dragIdx < els.Count)
        {
            double ds = S(p.X) - S(dragFrom.X);
            ds = Math.Clamp(ds, -(els[dragIdx - 1].Len - 0.5), els[dragIdx].Len - 0.5);
            ds = Math.Round(ds * 10) / 10;
            els[dragIdx - 1].Len += ds; els[dragIdx].Len -= ds;
            LastInfo = $"granica: {els[dragIdx - 1].Nazwa} {els[dragIdx - 1].Len:0.0} m | {els[dragIdx].Nazwa} {els[dragIdx].Len:0.0} m";
        }
        else if (drag == Drag.Level && dragIdx >= 0 && dragIdx < els.Count)
        {
            double k = K(p.Y), k0 = UtAlignEl.K(els[dragIdx].R0);
            if (Math.Sign(k) != Math.Sign(k0) || Math.Abs(k) < 1e-6) k = Math.Sign(k0) * 1e-6;
            double r = Math.Round(1 / Math.Abs(k)) * Math.Sign(k0);
            if (Math.Abs(r) < 50) r = 50 * Math.Sign(k0);
            // długość łuku tak, żeby kąt zwrotu się nie zmienił (tor za łukiem nie odjeżdża w bok)
            double ang = els[dragIdx].Len / Math.Abs(els[dragIdx].R0);
            els[dragIdx].R0 = els[dragIdx].R1 = r; els[dragIdx].Len = Math.Max(0.5, ang * Math.Abs(r));
            TrasaElem.Normalize(al);
            LastInfo = $"łuk R {Math.Abs(r):0} m, długość {els[dragIdx].Len:0.0} m (kąt zwrotu bez zmian)";
        }
        dragNow = al; Preview?.Invoke(al);
    }

    public void ReleaseAt(Point p)
    {
        if (dragNow != null && drag is Drag.Boundary or Drag.Level) Commit?.Invoke(dragNow, drag == Drag.Boundary ? "Granica elementów" : "Promień łuku");
        drag = Drag.None; dragNow = null; dragBase = null;
    }

    // ------------------------------------------------------------------ menu
    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        var cm = BuildMenu(e.GetPosition(this)); if (cm.Items.Count == 0) return;
        cm.PlacementTarget = this; cm.IsOpen = true; e.Handled = true;
    }

    /// <summary>Menu PPM w miejscu wykresu.</summary>
    public ContextMenu BuildMenu(Point p) => BuildMenuAt(S(p.X));

    public ContextMenu BuildMenuAt(double s)
    {
        var cm = new ContextMenu();
        MenuItem Item(string h, Action a) { var mi = new MenuItem { Header = h }; mi.Click += (o, e) => a(); cm.Items.Add(mi); return mi; }
        if (Track == null) return cm;
        if (Track.Align == null) { Item("Zamień na trasę z elementów (edycja na wykresie)", () => ToAlign?.Invoke()); return cm; }
        var a = Track.Align; double acc = 0; int idx = -1;
        for (int i = 0; i < a.Els.Count; i++) { if (s <= acc + a.Els[i].Len || i == a.Els.Count - 1) { idx = i; break; } acc += a.Els[i].Len; }
        if (idx < 0) return cm;
        var el = a.Els[idx]; double into = s - acc;
        cm.Items.Add(new MenuItem { Header = $"Element {idx + 1}: {el.Nazwa}, {el.Len:0.0} m" + (el.Luk ? $", R {Math.Abs(el.R0):0}" : el.Przejsciowa ? $", R {Math.Abs(el.R0):0} → {Math.Abs(el.R1):0}" : ""), IsEnabled = false, FontWeight = FontWeights.SemiBold });
        Item("Podziel element tutaj", () => Do(x => { var e2 = x.Els[idx].Clone(); double f = Math.Clamp(into, 0.5, x.Els[idx].Len - 0.5); e2.Len = x.Els[idx].Len - f; x.Els[idx].Len = f; if (x.Els[idx].Przejsciowa) { double k0 = UtAlignEl.K(x.Els[idx].R0), k1 = UtAlignEl.K(e2.R1), km = k0 + (k1 - k0) * f / (f + e2.Len); double rm = Math.Abs(km) < 1e-12 ? 0 : 1 / km; x.Els[idx].R1 = rm; e2.R0 = rm; } x.Els.Insert(idx + 1, e2); }, "Podział elementu"));
        // granica najbliżej kliknięcia
        int b = into < el.Len / 2 ? idx : idx + 1;
        if (b > 0 && b < a.Els.Count && !a.Els[b - 1].Przejsciowa && !a.Els[b].Przejsciowa && Math.Abs(UtAlignEl.K(a.Els[b - 1].R1) - UtAlignEl.K(a.Els[b].R0)) > 1e-9)
        {
            double lt = Math.Min(30, Math.Min(a.Els[b - 1].Len, a.Els[b].Len) * 0.8);
            Item($"Wstaw przejściową {lt:0} m na granicy (km {acc + (b == idx ? 0 : el.Len):0.0})", () => Do(x =>
            {
                x.Els[b - 1].Len -= lt / 2; x.Els[b].Len -= lt / 2;
                x.Els.Insert(b, new UtAlignEl { Len = lt, R0 = x.Els[b - 1].R1, R1 = x.Els[b].R0 });
            }, "Wstawienie przejściowej"));
        }
        if (el.Luk) Item("Zamień na prostą", () => Do(x => { x.Els[idx].R0 = x.Els[idx].R1 = 0; }, "Zamiana na prostą"));
        if (el.Prosta)
        {
            var near = a.Els.Where(e => e.Luk).OrderBy(e => Math.Abs(a.Els.IndexOf(e) - idx)).FirstOrDefault();
            double r = near?.R0 ?? 1000;
            Item($"Zamień na łuk R {Math.Abs(r):0} w prawo", () => Do(x => { x.Els[idx].R0 = x.Els[idx].R1 = Math.Abs(r); }, "Zamiana na łuk"));
            Item($"Zamień na łuk R {Math.Abs(r):0} w lewo", () => Do(x => { x.Els[idx].R0 = x.Els[idx].R1 = -Math.Abs(r); }, "Zamiana na łuk"));
        }
        if (a.Els.Count > 1)
            Item("Usuń element (długość do poprzedniego)", () => Do(x => { double l = x.Els[idx].Len; x.Els.RemoveAt(idx); x.Els[Math.Max(0, idx - 1)].Len += l; }, "Usunięcie elementu"));
        return cm;
    }

    void Do(Action<UtAlign> f, string what)
    {
        var a = Track.Align.Clone(); f(a); TrasaElem.Normalize(a);
        if (a.Els.Any(e => e.Len <= 0.05)) { LastInfo = "za krótki element – nie da się"; return; }
        LastInfo = ""; Commit?.Invoke(a, what);
    }
}

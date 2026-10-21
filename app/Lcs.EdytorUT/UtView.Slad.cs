using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Ślad osi toru (narzędzie „Ślad”, F) i sieć torów z OpenStreetMap jako linie (PLAN_EDYTOR_UT.md § 5h):
/// klik przy linii OSM – punkt na linii, kolejne kliknięcia – ślad biegnie po liniach (przez rozjazdy); klik obok linii albo z Ctrl – punkt
/// ze zdjęcia; Ctrl + przeciągnięcie – malowanie punktów po osi toru na zdjęciu (co 2 m), samo przeciągnięcie – przesuwanie mapy. Krzywizna śladu i rozpoznane łuki – na wykresie
/// krzywizny i w panelu; Enter – propozycja „Tor po śladzie” (trasa z elementów dopasowana do śladu), drugi Enter – nowy tor.
/// Backspace – cofnij ostatni krok, Esc – wyczyść ślad. Malowanie działa też w „Łuku z punktów” (po wyborze łuku).
/// </summary>
public partial class UtView
{
    /// <summary>Sieć torów OSM (pobrane obszary razem).</summary>
    public OsmSiec Osm;
    /// <summary>Sieć OSM rysowana zawsze (inaczej – tylko w narzędziach Ślad i Łuk z punktów).</summary>
    public bool OsmShown;
    /// <summary>Punkty śladu po kolei.</summary>
    public readonly List<XY> SladPts = new();
    readonly List<int> sladSteps = new();
    (XY P, long A, long B)? sladNetEnd;
    /// <summary>Baza krzywizny śladu [m]; 0 – dobrana do rozrzutu punktów.</summary>
    public double SladBaza;
    /// <summary>Propozycja toru po śladzie (Enter), null – brak.</summary>
    public SladTor SladPreview { get; private set; }
    public bool SladBusy { get; private set; }
    /// <summary>Ślad zmieniony (punkty, propozycja) – panel i wykres krzywizny.</summary>
    public event Action SladChanged;
    /// <summary>Zatwierdzona propozycja: nowy tor – wielobok (true, domyślnie) albo trasa z elementów (false).</summary>
    public event Action<SladTor, bool> SladAccepted;
    /// <summary>Odległość kliknięcia od linii OSM, przy której punkt idzie na linię [px].</summary>
    const double NetSnapPx = 12;

    static readonly Pen OsmPen = FrozenPen(Color.FromRgb(0xff, 0x6a, 0x00), 1.3), SladPen = FrozenPen(Color.FromArgb(0xb0, 0xff, 0x40, 0x00), 4);
    static readonly Brush OsmNode = Frozen(Color.FromRgb(0xff, 0x6a, 0x00)), SladDot = Frozen(Color.FromRgb(0xb0, 0x20, 0x00));

    bool PaintMode => tool == UtTool.Slad || (tool == UtTool.LukPunkty && fitArc != null);
    bool painting2; XY lastPaint;

    /// <summary>Ctrl + przeciągnięcie w narzędziu Ślad / Łuk z punktów: punkty po drodze kursora, co 2 m w terenie (i co najmniej 3 px).
    /// Samo przeciągnięcie przesuwa mapę jak w innych narzędziach.</summary>
    void PaintPt(Point from, Point p)
    {
        double k = Doc != null ? Geo.ScaleFactor(ToWorld(p)) : 1, step = Math.Max(2 * k, 3 * mpp);
        var w = ToWorld(p);
        if (!painting2)
        {
            painting2 = true; var w0 = ToWorld(from);
            if (tool == UtTool.Slad) { sladSteps.Add(SladPts.Count); sladNetEnd = null; SladPreview = null; SladPts.Add(w0); }
            else FitPts.Add(w0);
            lastPaint = w0;
        }
        // droga kursora próbkowana – szybki ruch nie zostawia dziur
        double d = XY.Dist(lastPaint, w);
        if (d < step) return;
        int n = (int)Math.Floor(d / step); var a = lastPaint;
        for (int i = 1; i <= n; i++) { var q = a + (w - a) * (i * step / d); if (tool == UtTool.Slad) SladPts.Add(q); else FitPts.Add(q); lastPaint = q; }
        if (tool == UtTool.LukPunkty) DraftInfo?.Invoke($"Łuk z punktów: malowanie – {FitPts.Count} pkt (puść, żeby dopasować)");
    }

    void EndPaint()
    {
        painting2 = false;
        if (tool == UtTool.Slad) { SladChanged?.Invoke(); DraftInfo?.Invoke(SladInfo()); }
        else Refit();
        InvalidateVisual();
    }

    /// <summary>Punkt na linii OSM pod kursorem (do <see cref="NetSnapPx"/> px), null – brak.</summary>
    (XY P, long A, long B)? NetAt(Point s)
    {
        if (Osm == null || Osm.Ways.Count == 0) return null;
        if (Osm.Nearest(ToWorld(s)) is not var (q, a, b, _)) return null;
        return (ToScreen(q) - s).Length <= NetSnapPx ? (q, a, b) : null;
    }

    void SladClick(Point s)
    {
        var w = ToWorld(s);
        var net = Mods.HasFlag(ModifierKeys.Control) ? null : NetAt(s);
        sladSteps.Add(SladPts.Count); SladPreview = null;
        if (net is var (q, a, b))
        {
            var path = sladNetEnd is var (fp, fa, fb) && SladPts.Count > 0 ? Osm.Path((fp, fa, fb), (q, a, b)) : null;
            if (path != null) SladPts.AddRange(path.Skip(1)); else SladPts.Add(q);
            sladNetEnd = (q, a, b);
        }
        else { SladPts.Add(w); sladNetEnd = null; }
        SladChanged?.Invoke(); DraftInfo?.Invoke(SladInfo()); InvalidateVisual();
    }

    public void SladClear() { SladPts.Clear(); sladSteps.Clear(); sladNetEnd = null; SladPreview = null; SladChanged?.Invoke(); InvalidateVisual(); }

    /// <summary>Cofnięcie ostatniego kroku śladu (klik albo jedno malowanie).</summary>
    public void SladUndo()
    {
        if (sladSteps.Count == 0) return;
        int n = sladSteps[^1]; sladSteps.RemoveAt(sladSteps.Count - 1);
        SladPts.RemoveRange(n, SladPts.Count - n);
        sladNetEnd = null; SladPreview = null;
        SladChanged?.Invoke(); DraftInfo?.Invoke(SladInfo()); InvalidateVisual();
    }

    double SladK => SladPts.Count > 0 ? Geo.ScaleFactor(SladPts[0]) : 1;
    /// <summary>Długość śladu w terenie [m].</summary>
    public double SladLen => Slad.Length(SladPts) / SladK;
    /// <summary>Baza krzywizny użyta dla śladu (wpisana albo dobrana) [m].</summary>
    public double SladBazaUzyta => SladBaza > 0 ? SladBaza : SladPts.Count >= 3 ? Slad.BazaAuto(SladPts, SladK) : Slad.Baza;
    /// <summary>Krzywizna śladu (wykres) i rozpoznane łuki (przybliżone).</summary>
    public (List<SladK> K, List<SladLuk> Luki) SladKrzywizna()
    {
        if (SladPts.Count < 3) return (new(), new());
        double b = SladBazaUzyta; var kr = Slad.Krzywizna(SladPts, SladK, b);
        return (kr, Slad.Luki(kr, b));
    }

    string SladInfo()
    {
        if (SladPts.Count == 0) return "Ślad: kliknij przy linii OSM (pomarańczowej) albo na osi toru na zdjęciu; Ctrl + przeciągnięcie – maluj punkty; przeciągnięcie – przesuń mapę";
        return $"Ślad: {SladPts.Count} pkt, {Len(SladLen)} – kolejne kliknięcia przy linii OSM biegną po liniach; Ctrl – punkt ze zdjęcia; Ctrl + przeciągnięcie – maluj; Enter – tor po śladzie (propozycja); Backspace – cofnij; Esc – wyczyść";
    }

    /// <summary>Propozycja toru po śladzie (liczona w tle).</summary>
    public async Task SladLicz()
    {
        if (SladBusy) return;
        if (SladPts.Count < 3) { DraftInfo?.Invoke("Ślad: za mało punktów – kliknij albo namaluj ślad osi toru"); return; }
        SladBusy = true; DraftInfo?.Invoke("Tor po śladzie: liczę łuki i dopasowanie…");
        var pts = SladPts.ToList(); double k = SladK, b = SladBazaUzyta;
        try { SladPreview = await Task.Run(() => Slad.TorPoSladzie(pts, k, b)); }
        finally { SladBusy = false; }
        if (SladPreview.Error != null) { DraftInfo?.Invoke("Tor po śladzie: " + SladPreview.Error); SladPreview = null; }
        else DraftInfo?.Invoke(SladPreviewInfo() + " · Enter – utwórz tor (wielobok), Esc – odrzuć");
        SladChanged?.Invoke(); InvalidateVisual();
    }

    /// <summary>Opis propozycji: elementy, odchyłki, pominięte punkty.</summary>
    public string SladPreviewInfo()
    {
        if (SladPreview?.A is not UtAlign a) return "";
        var luki = a.Els.Where(e => !e.Prosta).Select(e => e.Luk ? $"R {Math.Abs(e.R0):0}" : $"L {e.Len:0}");
        return $"Tor po śladzie (propozycja): {a.Els.Count} elementów ({string.Join(" | ", luki)}), odchyłka średnia {SladPreview.Rms * 100:0.#} cm, największa {SladPreview.Max * 100:0.#} cm" +
               (SladPreview.Pominiete.Count > 0 ? $", pominięte odstające punkty: {SladPreview.Pominiete.Count}" : "") +
               (SladPreview.Rms > 0.1 ? " – ślad mało dokładny (np. OSM): R przybliżony, krzywych przejściowych z niego nie widać; po utworzeniu popraw łuki Łukiem z punktów na zdjęciu (J, malowanie)" : "");
    }

    public void SladAccept(bool wielobok = true)
    {
        if (SladPreview?.A == null) return;
        var p = SladPreview; SladPreview = null;
        SladAccepted?.Invoke(p, wielobok);
        SladChanged?.Invoke(); InvalidateVisual();
    }

    public bool SladKey(Key k)
    {
        if (tool != UtTool.Slad) return false;
        if (k == Key.Back) { SladUndo(); return true; }
        if (k == Key.Escape && (SladPts.Count > 0 || SladPreview != null))
        {
            if (SladPreview != null) { SladPreview = null; SladChanged?.Invoke(); DraftInfo?.Invoke("Tor po śladzie odrzucony – " + SladInfo()); }
            else { SladClear(); DraftInfo?.Invoke(SladInfo()); }
            InvalidateVisual(); return true;
        }
        if (k == Key.Enter) { if (SladPreview != null) SladAccept(); else _ = SladLicz(); return true; }
        return false;
    }

    /// <summary>
    /// Prostokąt PUWG obejmujący cały widok, z zapasem 10 %: w Schemacie mapa jest obrócona (oś schematu poziomo) i rozciągnięta
    /// w poprzek, więc dwa narożniki ekranu (lewy górny, prawy dolny) nie wyznaczają widocznego obszaru – liczą się wszystkie cztery.
    /// </summary>
    public (XY A, XY B) ViewBox()
    {
        double w = ActualWidth, h = ActualHeight;
        var c = new[] { ToWorld(new Point(0, 0)), ToWorld(new Point(w, 0)), ToWorld(new Point(0, h)), ToWorld(new Point(w, h)) };
        double x0 = c.Min(p => p.X), x1 = c.Max(p => p.X), y0 = c.Min(p => p.Y), y1 = c.Max(p => p.Y), mx = (x1 - x0) * 0.1, my = (y1 - y0) * 0.1;
        return (new XY(x0 - mx, y0 - my), new XY(x1 + mx, y1 + my));
    }

    /// <summary>Widok w całości w pobranej sieci OSM: narożniki, środki boków i środek ekranu (nie prostokąt z zapasem – w obróconym
    /// Schemacie jest dużo większy od ekranu, więc po każdym ruchu wyglądałby na niepokryty).</summary>
    public bool OsmCovers()
    {
        if (Osm == null) return false;
        double w = ActualWidth, h = ActualHeight;
        for (int i = 0; i <= 2; i++) for (int j = 0; j <= 2; j++) { var p = ToWorld(new Point(w * i / 2, h * j / 2)); if (!Osm.Covers(p, p)) return false; }
        return true;
    }

    bool osmBusy;
    readonly System.Windows.Threading.DispatcherTimer osmTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };
    bool osmTimerInit;

    /// <summary>W Śladzie i Łuku z punktów (albo z widoczną siecią OSM) po przesunięciu / przybliżeniu mapy brakujący obszar dobiera się sam (0,7 s po ruchu).</summary>
    void OsmAuto()
    {
        if (Test.Running && !OsmAutoTest) return;
        if (!osmTimerInit) { osmTimerInit = true; osmTimer.Tick += async (s, e) => { osmTimer.Stop(); if (OsmWanted && !osmBusy && !OsmCovers()) await OsmPobierz(); }; }
        if (OsmWanted && !OsmCovers()) { osmTimer.Stop(); osmTimer.Start(); }
    }

    /// <summary>Test: samo dobieranie sieci także w teście (bez sieci – zamiennik <see cref="OsmFetchForTest"/>).</summary>
    public bool OsmAutoTest;
    /// <summary>Test: zamiast Overpass – sieć dla prostokąta.</summary>
    public Func<XY, XY, OsmSiec> OsmFetchForTest;
    bool OsmWanted => Osm != null && (OsmShown || tool is UtTool.Slad or UtTool.LukPunkty);

    /// <summary>Sieć OSM dla widoku (Overpass), dołączona do pobranej; liczba dróg albo −1 przy błędzie.</summary>
    public async Task<int> OsmPobierz()
    {
        var (a, b) = ViewBox();
        double k = Geo.ScaleFactor(a);
        if (XY.Dist(a, b) / k > 8000) { DraftInfo?.Invoke("Sieć OSM: przybliż widok (najwyżej ok. 5 km) i pobierz jeszcze raz"); return -1; }
        if (OsmCovers()) return Osm.Ways.Count;
        if (osmBusy) return Osm?.Ways.Count ?? -1;
        osmBusy = true;
        DraftInfo?.Invoke("Sieć OSM: pobieranie osi torów z OpenStreetMap…");
        try
        {
            var s = OsmFetchForTest != null ? OsmFetchForTest(a, b) : await OsmPunkty.Siec(a, b, CancellationToken.None);
            s.Areas.Add((a, b));
            (Osm ??= new OsmSiec()).Merge(s);
            DraftInfo?.Invoke($"Sieć OSM: {s.Ways.Count} dróg kolejowych w widoku ({OsmPunkty.Attribution})");
            InvalidateVisual();
            return s.Ways.Count;
        }
        catch (Exception ex) { DraftInfo?.Invoke("Sieć OSM: " + Pobieranie.Reason(ex)); return -1; }
        finally { osmBusy = false; }
    }

    void DrawOsm(DrawingContext dc)
    {
        if (Osm == null || !(OsmShown || tool is UtTool.Slad or UtTool.LukPunkty)) return;
        double w = ActualWidth, h = ActualHeight;
        var g = new StreamGeometry();
        using (var c = g.Open())
            foreach (var (a, b) in Osm.Segments())
            {
                var p = ToScreen(a); var q = ToScreen(b);
                if (Math.Max(p.X, q.X) < 0 || Math.Min(p.X, q.X) > w || Math.Max(p.Y, q.Y) < 0 || Math.Min(p.Y, q.Y) > h) continue;
                c.BeginFigure(p, false, false); c.LineTo(q, true, false);
            }
        g.Freeze(); dc.DrawGeometry(null, OsmPen, g);
        if (mpp < 0.6)
            foreach (var p0 in Osm.Nodes.Values)
            {
                var p = ToScreen(p0); if (p.X < 0 || p.X > w || p.Y < 0 || p.Y > h) continue;
                dc.DrawEllipse(OsmNode, null, p, 2, 2);
            }
    }

    void DrawSlad(DrawingContext dc, double dpi)
    {
        if (SladPts.Count > 0 && (tool == UtTool.Slad || SladPreview != null))
        {
            var g = new StreamGeometry();
            using (var c = g.Open()) { c.BeginFigure(ToScreen(SladPts[0]), false, false); foreach (var q in SladPts.Skip(1)) c.LineTo(ToScreen(q), true, false); }
            g.Freeze(); dc.DrawGeometry(null, SladPen, g);
            if (SladPts.Count < 2000) foreach (var q in SladPts) dc.DrawEllipse(SladDot, null, ToScreen(q), 1.8, 1.8);
        }
        if (SladPreview?.A is UtAlign a)
        {
            var r = TrasaElem.Compute(a, SladK);
            dc.DrawGeometry(null, new Pen(Blue, 2.2) { DashStyle = new DashStyle(new double[] { 5, 2 }, 0) }, Line(r.Sample(0.5), 0));
            // granice elementów
            foreach (var e in r.Elements.Skip(1)) { var p = ToScreen(e.Start); dc.DrawEllipse(Brushes.White, new Pen(Blue, 1.5), p, 3.5, 3.5); }
            var red = new Pen(Red, 1.6);
            foreach (var i in SladPreview.Pominiete)
            {
                if (i >= SladPts.Count) continue; var q = ToScreen(SladPts[i]);
                dc.DrawLine(red, q + new Vector(-5, -5), q + new Vector(5, 5)); dc.DrawLine(red, q + new Vector(-5, 5), q + new Vector(5, -5));
                Label(dc, $"pominięty {SladPreview.Dev[i] * 100:0} cm", q + new Vector(7, -16), Red, dpi);
            }
        }
        if (tool == UtTool.Slad && hover is XY hv)
        {
            if (!Mods.HasFlag(ModifierKeys.Control) && NetAt(ToScreen(hv)) is var (q, _, _)) dc.DrawEllipse(null, SnapPen, ToScreen(q), 6, 6);
            Label(dc, SladBusy ? "liczę…" : SladPreview != null ? "Enter – utwórz tor" : SladPts.Count == 0 ? "klik przy linii OSM albo na osi toru" : $"{SladPts.Count} pkt, {Len(SladLen)}", ToScreen(hv) + new Vector(12, 8), DraftInk, dpi);
        }
    }
}

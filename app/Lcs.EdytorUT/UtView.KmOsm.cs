using System.Windows;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Propozycje punktów km ze słupków OSM (narzędzie km, panel „Kilometraż ze słupków OSM”): romb na osi toru w miejscu słupka
/// (wypełniony – zaznaczony do dodania), kreska do słupka z mapy, opis „l. 17 km 3.000”. Do projektu trafiają dopiero po zatwierdzeniu.
/// </summary>
public partial class UtView
{
    /// <summary>Propozycje punktów km (nie zapisują się w projekcie).</summary>
    public List<PropozycjaKm> KmOsm = new();
    /// <summary>Słupki (id OSM) zaznaczone do dodania.</summary>
    public readonly HashSet<long> KmOsmWybrane = new();
    /// <summary>Test: zamiast Overpass – słupki dla prostokąta.</summary>
    public Func<XY, XY, List<SlupekKm>> KmOsmFetchForTest;

    static readonly Color KmOsmKolor = Color.FromRgb(0x7b, 0x2f, 0xbe);
    static readonly Pen KmOsmPen = FrozenPen(KmOsmKolor, 1.6), KmOsmDash = FrozenDash(KmOsmKolor);
    static readonly Brush KmOsmBrush = Frozen(KmOsmKolor);

    static Pen FrozenDash(Color c) { var p = new Pen(new SolidColorBrush(c), 1) { DashStyle = new DashStyle(new double[] { 3, 2 }, 0) }; p.Freeze(); return p; }

    /// <summary>Słupki km z OSM dla widoku i 1,5 km dookoła (sąsiednie słupki dają kierunek kilometrażu); null – błąd (opis w pasku).</summary>
    public async Task<List<SlupekKm>> KmOsmPobierz()
    {
        var (a, b) = ViewBox();
        double k = Geo.ScaleFactor(a);
        if (XY.Dist(a, b) / k > 8000) { DraftInfo?.Invoke("Słupki km: przybliż widok (najwyżej ok. 5 km) i szukaj jeszcze raz"); return null; }
        double m = 1500 * k;
        var a2 = new XY(Math.Min(a.X, b.X) - m, Math.Min(a.Y, b.Y) - m); var b2 = new XY(Math.Max(a.X, b.X) + m, Math.Max(a.Y, b.Y) + m);
        DraftInfo?.Invoke("Słupki km: pobieranie z OpenStreetMap…");
        try { return KmOsmFetchForTest != null ? KmOsmFetchForTest(a2, b2) : await OsmPunkty.Slupki(a2, b2, CancellationToken.None); }
        catch (Exception ex) { DraftInfo?.Invoke("Słupki km: " + Pobieranie.Reason(ex)); return null; }
    }

    /// <summary>Miejsce propozycji na osi toru (świat); null – toru już nie ma.</summary>
    public XY? KmOsmAt(PropozycjaKm x) => Track(x.Track) is UtTrack t && Geometry(t) is var g && g.Elements.Count > 0 ? g.At(Math.Clamp(x.S, 0, g.Length)).P : null;

    /// <summary>Rysowanie propozycji (nakładka <see cref="Overlay"/>).</summary>
    public void DrawKmOsm(DrawingContext dc)
    {
        if (KmOsm.Count == 0 || Doc == null) return;
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        foreach (var x in KmOsm)
        {
            if (KmOsmAt(x) is not XY w) continue;
            var p = ToScreen(w); var q = ToScreen(x.Slupek.P);
            if (p.X < -50 || p.Y < -50 || p.X > ActualWidth + 50 || p.Y > ActualHeight + 50) continue;
            if ((q - p).Length > 2) { dc.DrawLine(KmOsmDash, p, q); dc.DrawEllipse(null, KmOsmPen, q, 2.5, 2.5); }
            bool sel = KmOsmWybrane.Contains(x.Slupek.Id);
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(p + new Vector(0, -6), true, true);
                c.LineTo(p + new Vector(6, 0), true, false); c.LineTo(p + new Vector(0, 6), true, false); c.LineTo(p + new Vector(-6, 0), true, false);
            }
            g.Freeze();
            dc.DrawGeometry(sel ? KmOsmBrush : Brushes.White, KmOsmPen, g);
            string line = x.Slupek.Line.Length > 0 ? $"l. {x.Slupek.Line} " : "";
            Label(dc, $"{line}km {Kilometraz.Fmt(x.Slupek.Km)}{(sel ? "" : " ?")}", p + new Vector(8, -18), KmOsmBrush, dpi);
        }
    }
}

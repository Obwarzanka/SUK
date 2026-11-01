using System.Windows;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Podpowiedzi z OSM (panel „Podpowiedzi z OSM”): sygnalizatory i inne urządzenia – znak urządzenia w proponowanym miejscu, kreska
/// do obiektu z mapy, opis „A →”; numery zwrotnic i torów – opis „nr 12 ?” przy PR rozjazdu i w środku toru. Kolor jak przy słupkach km
/// (propozycje OSM). Do projektu trafiają dopiero po zatwierdzeniu.
/// </summary>
public partial class UtView
{
    /// <summary>Propozycje urządzeń (nie zapisują się w projekcie).</summary>
    public List<PropozycjaUrz> UrzOsm = new();
    /// <summary>Propozycje numerów zwrotnic i torów.</summary>
    public List<PropozycjaNr> NrOsm = new();
    /// <summary>Propozycje zaznaczone do przyjęcia (<see cref="PropozycjaUrz.Klucz"/>, <see cref="PropozycjaNr.Klucz"/>).</summary>
    public readonly HashSet<string> OsmWybrane = new();
    /// <summary>Test: zamiast Overpass – dane dla prostokąta.</summary>
    public Func<XY, XY, OsmUrzadzenia> UrzOsmFetchForTest;

    /// <summary>Urządzenia, zwrotnice i tory z OSM dla widoku i 200 m dookoła; null – błąd (opis w pasku).</summary>
    public async Task<OsmUrzadzenia> UrzOsmPobierz()
    {
        var (a, b) = ViewBox();
        double k = Geo.ScaleFactor(a);
        if (XY.Dist(a, b) / k > 6000) { DraftInfo?.Invoke("Podpowiedzi z OSM: przybliż widok (najwyżej ok. 4 km) i szukaj jeszcze raz"); return null; }
        double m = 200 * k;
        var a2 = new XY(Math.Min(a.X, b.X) - m, Math.Min(a.Y, b.Y) - m); var b2 = new XY(Math.Max(a.X, b.X) + m, Math.Max(a.Y, b.Y) + m);
        DraftInfo?.Invoke("Podpowiedzi z OSM: pobieranie z OpenStreetMap…");
        try { return UrzOsmFetchForTest != null ? UrzOsmFetchForTest(a2, b2) : await OsmPunkty.Urzadzenia(a2, b2, CancellationToken.None); }
        catch (Exception ex) { DraftInfo?.Invoke("Podpowiedzi z OSM: " + Pobieranie.Reason(ex)); return null; }
    }

    /// <summary>Miejsce propozycji na planie (świat): urządzenie – na osi toru, zwrotnica – PR rozjazdu, tor – środek toru; null – obiektu już nie ma.</summary>
    public XY? OsmAt(object x)
    {
        XY? Na(string track, double s) => Track(track) is UtTrack t && Geometry(t) is var g && g.Elements.Count > 0 ? g.At(Math.Clamp(s, 0, g.Length)).P : null;
        return x switch
        {
            PropozycjaUrz u => Na(u.Track, u.S),
            PropozycjaNr { Rozjazd: true } z => Doc?.P.Turnouts.FirstOrDefault(t => t.Id == z.Id) is UtTurnout t ? Na(t.Track, t.S) : null,
            PropozycjaNr n => Track(n.Id) is UtTrack t ? Na(t.Id, Geometry(t).Length / 2) : null,
            _ => null,
        };
    }

    /// <summary>Rysowanie propozycji (nakładka <see cref="Overlay"/>).</summary>
    public void DrawUrzOsm(DrawingContext dc)
    {
        if (UrzOsm.Count + NrOsm.Count == 0 || Doc == null) return;
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        bool Widac(Point p) => p.X > -50 && p.Y > -50 && p.X < ActualWidth + 50 && p.Y < ActualHeight + 50;
        foreach (var x in UrzOsm)
        {
            if (OsmAt(x) is not XY w) continue;
            var p = ToScreen(w); if (!Widac(p)) continue;
            var q = ToScreen(x.Osm.P);
            if ((q - p).Length > 2) { dc.DrawLine(KmOsmDash, p, q); dc.DrawEllipse(null, KmOsmPen, q, 2.5, 2.5); }
            bool sel = OsmWybrane.Contains(x.Klucz);
            var g = new UtDevice { Kind = x.Kind, Name = x.Name, Track = x.Track, S = x.S, Dir = x.Dir, Side = x.Side };
            if (Urzadzenia.Pos(Doc.P, g) is var (gp, gaz, _))
                DrawDevice(dc, g, ToScreen(gp), ScreenDir(gaz), DevK, sel ? KmOsmBrush : Brushes.MediumPurple, dpi, false);
            string kier = UtDevice.KindInfo(x.Kind).Directed && x.Kind != UtDevice.Koziol ? (x.Dir > 0 ? " →" : " ←") : "";
            Label(dc, $"{(x.Name.Length > 0 ? x.Name : x.Opis)}{kier}{(sel ? "" : " ?")}", p + new Vector(8, 6), KmOsmBrush, dpi);
        }
        foreach (var x in NrOsm)
        {
            if (OsmAt(x) is not XY w) continue;
            var p = ToScreen(w); if (!Widac(p)) continue;
            bool sel = OsmWybrane.Contains(x.Klucz);
            dc.DrawEllipse(sel ? KmOsmBrush : Brushes.White, KmOsmPen, p, 4, 4);
            Label(dc, $"{(x.Rozjazd ? "zwr." : "tor")} {x.Osm}{(sel ? "" : " ?")}", p + new Vector(7, x.Rozjazd ? -20 : 4), KmOsmBrush, dpi);
        }
    }
}

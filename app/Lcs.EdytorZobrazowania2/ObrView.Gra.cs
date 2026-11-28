using System.Windows;
using System.Windows.Media;

namespace Lcs.EdytorZobrazowania2;

/// <summary>Znaczniki zakładki „Gra” (SimRail): odcinki i elementy bez obiektu gry – pomarańczowa przerywana obwiednia / kółko (tylko w edytorze).</summary>
public partial class ObrView
{
    /// <summary>Łamane odcinków do przypięcia [j.].</summary>
    public readonly List<List<Point>> GraBrak = new();
    /// <summary>Miejsca elementów do przypięcia [j.].</summary>
    public readonly List<Point> GraBrakPunkty = new();
    static readonly Brush GraBrush = B(0xff, 0x8c, 0x00, 0xd0);

    void DrawGra(DrawingContext dc)
    {
        if (GraBrak.Count == 0 && GraBrakPunkty.Count == 0) return;
        var pen = P(GraBrush, Math.Max(2, 3 * Zoom), new double[] { 1.5, 1.5 });
        foreach (var l in GraBrak.Where(l => l.Count >= 2))
            for (int i = 1; i < l.Count; i++) dc.DrawLine(pen, ToScreen(l[i - 1].X, l[i - 1].Y), ToScreen(l[i].X, l[i].Y));
        var ring = P(GraBrush, 2);
        double r = Math.Max(6, 9 * Zoom);
        foreach (var p in GraBrakPunkty) dc.DrawEllipse(null, ring, ToScreen(p.X, p.Y), r, r);
    }
}

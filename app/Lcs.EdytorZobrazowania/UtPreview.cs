using System.Windows;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorZobrazowania;

/// <summary>
/// Podgląd UT (PLAN_EDYTOR_ZOBRAZOWANIA.md § 4): schemat osi torów UT – najdłuższy tor poziomo, w poprzek rozciągnięty, żeby było widać
/// tory równoległe; tory już narysowane na obrazie – ciemne, pozostałe – jasne, wybrany tor – pomarańczowy. Tylko do podglądu.
/// </summary>
public class UtPreview : FrameworkElement
{
    UtProject ut;
    Dictionary<string, List<XY>> lines = new();
    double az;
    /// <summary>Tor UT wyróżniony (rysowany albo wybrany w panelu).</summary>
    public string Highlight { get; set; }
    /// <summary>Tory UT narysowane na obrazie.</summary>
    public HashSet<string> Drawn { get; set; } = new();
    /// <summary>Rozciągnięcie w poprzek względem wzdłuż (ostatnie rysowanie).</summary>
    public double Stretch { get; private set; } = 1;

    static Pen P(Color c, double w) { var p = new Pen(new SolidColorBrush(c), w) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }; p.Freeze(); return p; }
    static readonly Pen Light = P(Color.FromRgb(0xb8, 0xb8, 0xb4), 1.5), Dark = P(Color.FromRgb(0x30, 0x30, 0x30), 2), Hi = P(Color.FromRgb(0xff, 0x80, 0x00), 4);

    public void SetUt(UtProject p)
    {
        ut = p; lines = new();
        if (p == null) { InvalidateVisual(); return; }
        double best = -1; az = 0;
        foreach (var t in p.Tracks)
        {
            var r = Trasa.Compute(t);
            if (r.Elements.Count == 0) continue;
            lines[t.Id] = r.Sample(2).Select(x => x.P).ToList();
            if (r.Length > best) { best = r.Length; var a = lines[t.Id][0]; var b = lines[t.Id][^1]; az = Math.Atan2(b.Y - a.Y, b.X - a.X); }
        }
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.White, new Pen(new SolidColorBrush(Color.FromRgb(0xc8, 0xc8, 0xc2)), 1), new Rect(0, 0, ActualWidth, ActualHeight));
        if (lines.Count == 0 || ActualWidth < 20 || ActualHeight < 20) return;
        // układ: u – wzdłuż najdłuższego toru (w prawo), v – w poprzek (w lewo od kierunku = w górę)
        double cs = Math.Cos(az), sn = Math.Sin(az);
        (double U, double V) Uv(XY p) => (p.X * cs + p.Y * sn, p.X * sn - p.Y * cs);
        var all = lines.Values.SelectMany(l => l).Select(Uv).ToList();
        double u0 = all.Min(x => x.U), u1 = all.Max(x => x.U), v0 = all.Min(x => x.V), v1 = all.Max(x => x.V);
        double m = 10, w = ActualWidth - 2 * m, h = ActualHeight - 2 * m;
        double ku = w / Math.Max(1, u1 - u0), kv = Math.Min(h / Math.Max(0.1, v1 - v0), ku * 60);
        Stretch = kv / ku;
        double oy = m + (h - (v1 - v0) * kv) / 2;
        Point S(XY p) { var (u, v) = Uv(p); return new Point(m + (u - u0) * ku, oy + (v1 - v) * kv); }
        foreach (var pass in new[] { 0, 1, 2 })
            foreach (var (id, pts) in lines)
            {
                int k = id == Highlight ? 2 : Drawn.Contains(id) ? 1 : 0;
                if (k != pass) continue;
                var g = new StreamGeometry();
                using (var c = g.Open()) { c.BeginFigure(S(pts[0]), false, false); foreach (var q in pts.Skip(1)) c.LineTo(S(q), true, true); }
                g.Freeze();
                dc.DrawGeometry(null, pass == 2 ? Hi : pass == 1 ? Dark : Light, g);
            }
    }
}

using System.Windows;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorZobrazowania2;

/// <summary>
/// Podgląd UT w zakładce UT (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 6 pkt 4, § 8): schemat osi torów UT – najdłuższy tor poziomo, w poprzek
/// rozciągnięty, żeby było widać tory równoległe. Tory przypięte do obrazu – ciemne, pozostałe – jasne; obiekt wskazany na liście
/// „do wyjaśnienia” albo zaznaczony na obrazie – pomarańczowy (tor albo punkt). Tylko do podglądu, UT tylko do odczytu.
/// </summary>
public class UtPodglad : FrameworkElement
{
    Dictionary<string, List<XY>> lines = new();
    Dictionary<string, TrasaResult> trasy = new();
    double az;
    /// <summary>Tory przypięte do obrazu.</summary>
    public HashSet<string> Przypiete { get; set; } = new();
    /// <summary>Wyróżnione tory i punkty (tor, km).</summary>
    public HashSet<string> HiTory { get; } = new();
    public List<(string Track, double S)> HiPunkty { get; } = new();

    static Pen P(Color c, double w) { var p = new Pen(new SolidColorBrush(c), w) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }; p.Freeze(); return p; }
    static readonly Pen Light = P(Color.FromRgb(0xb8, 0xb8, 0xb4), 1.5), Dark = P(Color.FromRgb(0x30, 0x30, 0x30), 2), Hi = P(Color.FromRgb(0xff, 0x80, 0x00), 4);
    static readonly Brush HiFill = new SolidColorBrush(Color.FromRgb(0xff, 0x80, 0x00));

    public void SetUt(UtProject p)
    {
        lines = new(); trasy = new();
        if (p == null) { InvalidateVisual(); return; }
        double best = -1; az = 0;
        foreach (var t in p.Tracks)
        {
            var r = Trasa.Compute(t);
            if (r.Elements.Count == 0) continue;
            trasy[t.Id] = r;
            lines[t.Id] = r.Sample(2).Select(x => x.P).ToList();
            if (r.Length > best) { best = r.Length; var a = lines[t.Id][0]; var b = lines[t.Id][^1]; az = Math.Atan2(b.Y - a.Y, b.X - a.X); }
        }
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.White, new Pen(new SolidColorBrush(Color.FromRgb(0xc8, 0xc8, 0xc2)), 1), new Rect(0, 0, ActualWidth, ActualHeight));
        if (lines.Count == 0 || ActualWidth < 20 || ActualHeight < 20) return;
        // układ: u – wzdłuż najdłuższego toru (w prawo), v – w poprzek (w górę)
        double cs = Math.Cos(az), sn = Math.Sin(az);
        (double U, double V) Uv(XY p) => (p.X * cs + p.Y * sn, p.X * sn - p.Y * cs);
        var all = lines.Values.SelectMany(l => l).Select(Uv).ToList();
        double u0 = all.Min(x => x.U), u1 = all.Max(x => x.U), v0 = all.Min(x => x.V), v1 = all.Max(x => x.V);
        double m = 10, w = ActualWidth - 2 * m, h = ActualHeight - 2 * m;
        double ku = w / Math.Max(1, u1 - u0), kv = Math.Min(h / Math.Max(0.1, v1 - v0), ku * 60);
        double oy = m + (h - (v1 - v0) * kv) / 2;
        Point S(XY p) { var (u, v) = Uv(p); return new Point(m + (u - u0) * ku, oy + (v1 - v) * kv); }
        foreach (var pass in new[] { 0, 1, 2 })
            foreach (var (id, pts) in lines)
            {
                int k = HiTory.Contains(id) ? 2 : Przypiete.Contains(id) ? 1 : 0;
                if (k != pass) continue;
                var g = new StreamGeometry();
                using (var c = g.Open()) { c.BeginFigure(S(pts[0]), false, false); foreach (var q in pts.Skip(1)) c.LineTo(S(q), true, true); }
                g.Freeze();
                dc.DrawGeometry(null, pass == 2 ? Hi : pass == 1 ? Dark : Light, g);
            }
        foreach (var (tr, s) in HiPunkty)
            if (trasy.TryGetValue(tr, out var r)) dc.DrawEllipse(HiFill, null, S(r.At(s).P), 5, 5);
    }
}

using System.Windows;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorZobrazowania2;

/// <summary>
/// Etap E7 (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 5 sposób 2, § 7): narzędzie klocka (cień klocka w punkcie zaczepienia – węzeł, tor albo siatka; klik – wstawienie),
/// narzędzie szkicu (prostokąt obszaru szkicu), warstwa szkicu na życzenie – szara, przerywana, do przesunięcia jako całość przed przyjęciem.
/// </summary>
public partial class ObrView
{
    /// <summary>Klocek dla punktu zaczepienia (okno liczy wysokość łącznika z prowadnic rzędów); null – brak.</summary>
    public Func<int[], ObrGraf.Fragment> KlocekDla { get; set; }
    /// <summary>Klik narzędziem klocka: punkt zaczepienia [j.].</summary>
    public event Action<int[]> KlocekPlaced;
    /// <summary>Prostokąt szkicu wskazany narzędziem Szkic [x, y, szer., wys.] [j.].</summary>
    public event Action<int[]> SzkicRect;
    /// <summary>Szkic przeciągnięty o (dx, dy) [j.].</summary>
    public event Action<int, int> SzkicMoved;

    (Point Start, int Dx, int Dy)? szkicDrag;
    static readonly Pen SzkicPen = new(new SolidColorBrush(Color.FromArgb(0xb0, 0xa0, 0xa0, 0xa0)), 1.0) { DashStyle = new DashStyle(new double[] { 3, 2 }, 0) };

    /// <summary>Punkt zaczepienia klocka pod kursorem (jak pierwszy punkt pióra: węzeł, tor, siatka z prowadnicą).</summary>
    public int[] KlocekPunkt(Point s) { Draft.Clear(); return PenPoint(s).P; }

    void KlocekClick(Point p) { if (Obraz != null) KlocekPlaced?.Invoke(KlocekPunkt(p)); }

    /// <summary>Wskaż na szkicu (do 6 px od jego toru) – przeciąganie szkicu; true – obsłużone.</summary>
    bool SzkicDown(Point p)
    {
        if (Obraz?.Szkic is not ObrGraf.Fragment f) return false;
        var (x, y) = ToImage(p);
        var w = f.Wezly.ToDictionary(n => n.Id);
        bool hit = f.Odcinki.Any(e => w.TryGetValue(e.A, out var a) && w.TryGetValue(e.B, out var b) && ObrGraf.Rzut(a.P, b.P, x, y).Dist * Zoom <= 6);
        if (!hit) return false;
        szkicDrag = (p, 0, 0);
        return true;
    }

    void SzkicMove(Point p)
    {
        if (szkicDrag is var (st, _, _)) { var (x0, y0) = ToImage(st); var (x1, y1) = ToImage(p); szkicDrag = (st, ZobTory.Grid(x1 - x0, G), ZobTory.Grid(y1 - y0, G)); }
    }

    bool SzkicUp()
    {
        if (szkicDrag is not var (_, dx, dy)) return false;
        szkicDrag = null; ReleaseMouseCapture();
        if (dx != 0 || dy != 0) SzkicMoved?.Invoke(dx, dy);
        return true;
    }

    /// <summary>Fragment (klocek, szkic) jako cień: tory przerywane szare, symbole i opisy półprzezroczyste.</summary>
    void DrawCien(DrawingContext dc, ObrGraf.Fragment f, int dx, int dy)
    {
        var k = ObrKlocki.Kopia(f, false);
        ObrSzkic.Przesun(k, dx, dy);
        var tmp = ObrSzkic.JakoObraz(k, Obraz);
        var w = k.Wezly.ToDictionary(n => n.Id);
        dc.PushTransform(new MatrixTransform(Zoom, 0, 0, Zoom, Off.X, Off.Y));
        try
        {
            var pen = new Pen(Ghost, PulpitSymbole_Tor) { DashStyle = new DashStyle(new double[] { 1.2, 0.8 }, 0) };
            foreach (var e in k.Odcinki)
                if (w.TryGetValue(e.A, out var a) && w.TryGetValue(e.B, out var b)) dc.DrawLine(pen, new Point(a.P[0], a.P[1]), new Point(b.P[0], b.P[1]));
            foreach (var el in k.Elementy.Where(e => e.Typ is not (ObrGraf.Skrzyzowanie)))
            {
                if (el.Typ == ObrGraf.RozjazdKrzyzowy) { if (w.TryGetValue(el.Na, out var n)) dc.DrawEllipse(Ghost, null, new Point(n.P[0], n.P[1]), 3, 3); continue; }
                Symbol(dc, tmp, el, Ghost);
                for (int i = 0; i < el.Opisy.Count; i++)
                    if (el.Opisy[i].Tekst.Length > 0 && OpisPkt(tmp, el, el.Opisy[i], i) is var (ox, oy)) TekstW(dc, el.Opisy[i].Tekst, ox, oy, 8, Ghost);
            }
        }
        finally { dc.Pop(); }
    }
    const double PulpitSymbole_Tor = 4.6;

    /// <summary>Warstwa szkicu i cień klocka pod kursorem.</summary>
    void DrawKlocki(DrawingContext dc)
    {
        if (Obraz?.Szkic is ObrGraf.Fragment sz)
        {
            DrawCien(dc, sz, szkicDrag?.Dx ?? 0, szkicDrag?.Dy ?? 0);
            // ramka szkicu – tylko w edytorze
            if (sz.Wezly.Count > 0)
            {
                int ddx = szkicDrag?.Dx ?? 0, ddy = szkicDrag?.Dy ?? 0;
                var a = ToScreen(sz.Wezly.Min(n => n.P[0]) + ddx - 16, sz.Wezly.Min(n => n.P[1]) + ddy - 24); var b = ToScreen(sz.Wezly.Max(n => n.P[0]) + ddx + 16, sz.Wezly.Max(n => n.P[1]) + ddy + 24);
                dc.DrawRectangle(null, SzkicPen, new Rect(a, b));
            }
        }
        if (Tool == ObrTool.Klocek && KlocekDla != null && hoverScreen is Point hs && Obraz != null)
        {
            var p = KlocekPunkt(hs);
            if (KlocekDla(p) is ObrGraf.Fragment f) DrawCien(dc, f, p[0], p[1]);
        }
        if (Tool == ObrTool.Szkic && rect is var (ra, rb, _)) dc.DrawRectangle(null, SzkicPen, new Rect(ra, rb));
    }
}

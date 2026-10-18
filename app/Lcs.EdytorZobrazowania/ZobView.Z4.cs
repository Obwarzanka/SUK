using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorZobrazowania;

/// <summary>
/// Perony i napisy na obrazie (etap Z4): peron – prostokąt z podwójną ramką i numerem (jak <c>PanelView</c>), przypięty do peronu UT,
/// rysowany przeciągnięciem; napis – nazwa posterunku sąsiedniego, nastawnia (ikona i skrót w cudzysłowie) albo dowolny tekst. Zaznaczanie
/// i przesuwanie myszą we Wskaż (do siatki).
/// </summary>
public partial class ZobView
{
    /// <summary>Zaznaczony peron (id peronu UT) albo napis (id napisu).</summary>
    public string SelectedPeron { get; private set; }
    public string SelectedNapis { get; private set; }
    /// <summary>Peron narysowany narzędziem Peron: x, y, szer, wys [j.].</summary>
    public event Action<int[]> PeronDrawn;
    /// <summary>Klik narzędziem Napis.</summary>
    public event Action<int[]> NapisClick;
    /// <summary>Peron albo napis przesunięty: rodzaj („peron” / „napis”), id, przesunięcie [j.].</summary>
    public event Action<string, string, int, int> ItemMoved;

    (int X, int Y)? peronFrom; (int X, int Y)? peronTo;
    (string Kind, string Id, (int X, int Y) From, (int X, int Y) To)? itemDrag;

    static readonly Brush PlatformLine = B(0xb0, 0xb0, 0xb0), LabelBr = B(0xd0, 0xd0, 0xd0), Dim2 = B(0x80, 0x80, 0x80);

    public void SelectItem(string kind, string id)
    {
        Selected = null; SelectedSym = null;
        SelectedPeron = kind == "peron" ? id : null; SelectedNapis = kind == "napis" ? id : null;
        SelectionChanged?.Invoke(); InvalidateVisual();
    }

    // ------------------------------------------------------------------ rysowanie
    void DrawZ4(DrawingContext dc)
    {
        if (Obraz == null) return;
        dc.PushTransform(new MatrixTransform(Zoom, 0, 0, Zoom, Off.X, Off.Y));
        foreach (var p in Obraz.Perony.Where(p => p.Prost is { Length: 4 }))
        {
            var r = Prost(p);
            if (itemDrag is var (k, id, f, t) && k == "peron" && id == p.Ut) r.Offset(t.X - f.X, t.Y - f.Y);
            var pl = Ut?.Platforms.FirstOrDefault(x => x.Id == p.Ut);
            var pen = new Pen(pl == null ? B(0xff, 0x40, 0x40) : PlatformLine, 1.4);
            dc.DrawRectangle(null, pen, r);
            if (r.Width > 8 && r.Height > 8) dc.DrawRectangle(null, new Pen(PlatformLine, 0.8), new Rect(r.X + 3, r.Y + 3, r.Width - 6, r.Height - 6));
            var name = pl == null ? "?" : pl.Name.Length > 0 ? (pl.Name.StartsWith("Peron", StringComparison.OrdinalIgnoreCase) ? pl.Name : "Peron " + pl.Name) : "Peron";
            var ft = Txt(name, 10, LabelBr);
            dc.DrawText(ft, new Point(r.X + r.Width / 2 - ft.Width / 2, r.Y + r.Height / 2 - ft.Height / 2));
            if (SelectedPeron == p.Ut) dc.DrawRectangle(null, new Pen(Azure, 1.6), new Rect(r.X - 3, r.Y - 3, r.Width + 6, r.Height + 6));
        }
        foreach (var n in Obraz.Napisy.Where(n => n.P != null))
        {
            double x = n.P[0], y = n.P[1];
            if (itemDrag is var (k, id, f, t) && k == "napis" && id == n.Id) { x += t.X - f.X; y += t.Y - f.Y; }
            var rect = NapisRect(n, x, y);
            if (n.Rodzaj == ZobNapis.Nastawnia)
            {
                var ip = new Pen(Dim2, 1.4);
                dc.DrawRectangle(null, ip, new Rect(x - 11, y - 22, 22, 15));
                dc.DrawRectangle(null, ip, new Rect(x - 7, y - 19, 14, 6));
                dc.DrawEllipse(Dim2, null, new Point(x, y - 10), 1.6, 1.6);
                var ft = Txt("„" + n.Tekst + "”", 10, Dim2);
                dc.DrawText(ft, new Point(x - ft.Width / 2, y + 3 - ft.Height / 2));
            }
            else
            {
                var ft = Txt(n.Tekst, 10, Grey);
                dc.DrawText(ft, new Point(x - ft.Width / 2, y - ft.Height / 2));
            }
            if (SelectedNapis == n.Id) dc.DrawRectangle(null, new Pen(Azure, 1.6), rect);
        }
        if (Tool == ZobTool.Peron && peronFrom is var (fx, fy) && peronTo is var (tx, ty))
            dc.DrawRectangle(null, new Pen(White, 1) { DashStyle = DashStyles.Dash }, new Rect(new Point(fx, fy), new Point(tx, ty)));
        dc.Pop();
    }

    static Rect Prost(ZobPeron p) => new(p.Prost[0], p.Prost[1], p.Prost[2], p.Prost[3]);

    Rect NapisRect(ZobNapis n, double x, double y)
    {
        if (n.Rodzaj == ZobNapis.Nastawnia) return new Rect(x - 14, y - 25, 28, 36);
        double w = Math.Max(10, n.Tekst.Length * 5.6) / 2 + 3;
        return new Rect(x - w, y - 8, 2 * w, 16);
    }

    // ------------------------------------------------------------------ trafienia i przesuwanie (Wskaż)
    public (string Kind, string Id)? HitItem(Point s)
    {
        if (Obraz == null) return null;
        var (x, y) = ToImage(s); var pt = new Point(x, y);
        foreach (var n in Obraz.Napisy.Where(n => n.P != null)) if (NapisRect(n, n.P[0], n.P[1]).Contains(pt)) return ("napis", n.Id);
        foreach (var p in Obraz.Perony.Where(p => p.Prost is { Length: 4 })) { var r = Prost(p); r.Inflate(3, 3); if (r.Contains(pt)) return ("peron", p.Ut); }
        return null;
    }

    bool ItemDown(Point p)
    {
        if (HitItem(p) is not var (k, id)) return false;
        SelectItem(k, id);
        var g = Snap(p); itemDrag = (k, id, g, g);
        return true;
    }

    void ItemMove(Point p) { if (itemDrag is var (k, id, f, _)) itemDrag = (k, id, f, Snap(p)); }

    bool ItemUp()
    {
        if (itemDrag is not var (k, id, f, t)) return false;
        itemDrag = null;
        if (t.X != f.X || t.Y != f.Y) ItemMoved?.Invoke(k, id, t.X - f.X, t.Y - f.Y);
        InvalidateVisual();
        return true;
    }

    // ------------------------------------------------------------------ narzędzia Peron i Napis
    bool Z4ToolDown(Point p)
    {
        if (Tool == ZobTool.Peron) { peronFrom = peronTo = Snap(p); CaptureMouse(); return true; }
        if (Tool == ZobTool.Napis) { var (gx, gy) = Snap(p); NapisClick?.Invoke(new[] { gx, gy }); return true; }
        return false;
    }

    void Z4ToolMove(Point p) { if (peronFrom != null) { peronTo = Snap(p); InvalidateVisual(); } }

    bool Z4ToolUp()
    {
        if (peronFrom is not var (fx, fy) || peronTo is not var (tx, ty)) return false;
        peronFrom = peronTo = null; ReleaseMouseCapture();
        int x = Math.Min(fx, tx), y = Math.Min(fy, ty), w = Math.Abs(tx - fx), h = Math.Abs(ty - fy);
        if (w < 16 || h < 8) { Info?.Invoke("Peron: przeciągnij prostokąt co najmniej 16 × 8 j."); InvalidateVisual(); return true; }
        PeronDrawn?.Invoke(new[] { x, y, w, h });
        InvalidateVisual();
        return true;
    }

    /// <summary>Przeciągnięcie narzędziem Peron (test).</summary>
    public void SimPeron(Point a, Point b) { Move(a, ModifierKeys.None); LeftDown(a, ModifierKeys.None); Move(b, ModifierKeys.None); LeftUp(b); }
}

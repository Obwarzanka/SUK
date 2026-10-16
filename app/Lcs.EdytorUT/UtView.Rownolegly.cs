using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Narzędzie Równoległy (P) – tor równoległy odrysowany z podkładu: 1) klik na torze wzorcowym, 2) klik na zdjęciu na osi nowego
/// toru w miejscu jego początku (odstęp z kliknięcia: co 1 cm, Shift – co 0,25 m), 3) klik w miejscu końca albo Enter – do końca
/// toru wzorcowego w stronę kursora. Tor równoległy: proste przesunięte, łuki współśrodkowe (<see cref="Trasa.Offset"/>), przycięty
/// do odcinka (<see cref="Budowa.Trim"/>). PPM – krok wstecz, Esc – od nowa.
/// </summary>
public partial class UtView
{
    string parSrc; double? parD; double parS1;
    /// <summary>Tor równoległy gotowy: tor wzorcowy, wierzchołki, odstęp [m] (+ po prawej).</summary>
    public event Action<string, List<UtVertex>, double> ParallelDrawn;

    void ParReset() { parSrc = null; parD = null; InvalidateVisual(); }

    /// <summary>Odstęp od toru wzorcowego [m w terenie, + po prawej] i kilometraż rzutu kursora.</summary>
    (double D, double S)? ParOffset(Point s)
    {
        if (Track(parSrc) is not UtTrack t) return null;
        var r = Geometry(t); if (r.Elements.Count == 0) return null;
        var w = ToWorld(s); var (sk, _) = r.Nearest(w); var (p, az) = r.At(sk);
        var rt = Element.Right(az); double d = ((w - p).X * rt.X + (w - p).Y * rt.Y) / r.K;
        d = Mods.HasFlag(ModifierKeys.Shift) ? Math.Round(d * 4) / 4 : Math.Round(d * 100) / 100;
        return (d, sk);
    }

    /// <summary>Tor równoległy w odstępie d od kilometrażu s1 do s2 toru wzorcowego (null – za blisko toru).</summary>
    List<UtVertex> ParTrack(double d, double s1, double s2)
    {
        if (Track(parSrc) is not UtTrack t || Math.Abs(d) < 0.5) return null;
        var r = Geometry(t); double k = r.K;
        var off = Trasa.Offset(t, d); var ro = Trasa.Compute(off, k);
        if (ro.Elements.Count == 0) return null;
        XY Q(double s) { var (p, az) = r.At(s); return p + Element.Right(az) * (d * k); }
        double a = ro.Nearest(Q(s1)).S, b = ro.Nearest(Q(s2)).S;
        var v = Budowa.Trim(off, k, a, b);
        if (s2 < s1) v.Reverse();   // kierunek nowego toru jak rysowanie (od kliknięcia początku)
        return v.Count >= 2 ? v : null;
    }

    void ParClick(Point s)
    {
        if (parSrc == null)
        {
            parSrc = HitTrack(s) ?? SelectedTrack;
            DraftInfo?.Invoke(parSrc != null ? "Równoległy: kliknij na podkładzie oś nowego toru w miejscu jego początku (Shift – odstęp co 0,25 m)" : "Równoległy: najpierw kliknij tor wzorcowy");
        }
        else if (parD == null)
        {
            if (ParOffset(s) is not var (d, sk) || Math.Abs(d) < 0.5) { DraftInfo?.Invoke("Równoległy: za blisko toru wzorcowego – kliknij na osi nowego toru"); return; }
            parD = d; parS1 = sk;
            DraftInfo?.Invoke($"Równoległy: odstęp {d.ToString("0.##", CultureInfo.GetCultureInfo("pl-PL"))} m – kliknij koniec nowego toru albo Enter (do końca toru wzorcowego)");
        }
        else if (ParOffset(s) is var (_, s2)) ParFinish(s2);
        InvalidateVisual();
    }

    void ParFinish(double s2)
    {
        var v = ParTrack(parD.Value, parS1, s2);
        if (v == null) { DraftInfo?.Invoke("Równoległy: za krótki odcinek"); return; }
        var src = parSrc; double d = parD.Value;
        ParReset();
        ParallelDrawn?.Invoke(src, v, d);
    }

    /// <summary>Klawisze narzędzia: Enter – do końca toru wzorcowego (w stronę kursora), Esc – od nowa, Backspace – krok wstecz.</summary>
    public bool ParKey(Key k)
    {
        if (tool != UtTool.Rownolegly) return false;
        if (k == Key.Enter && parD != null && Track(parSrc) is UtTrack t)
        {
            var r = Geometry(t); double cur = hover is XY h ? r.Nearest(h).S : r.Length;
            ParFinish(cur >= parS1 ? r.Length : 0); return true;
        }
        if (k == Key.Back) { if (parD != null) parD = null; else parSrc = null; InvalidateVisual(); return true; }
        if (k == Key.Escape && parSrc != null) { ParReset(); return true; }
        return false;
    }

    static readonly Pen ParPen = Dashed(Color.FromRgb(0xe0, 0x30, 0x10), 1.6);

    void DrawParallel(DrawingContext dc, double dpi)
    {
        if (tool != UtTool.Rownolegly || hover is not XY h) return;
        var hs = ToScreen(h);
        if (parSrc == null)
        {
            if (HitTrack(hs) is string ht && Track(ht) is UtTrack tt) dc.DrawGeometry(null, new Pen(SelGlow, 6), Line(Geometry(tt).Sample(1), 0));
            Label(dc, "kliknij tor wzorcowy", hs + new Vector(12, 8), DraftInk, dpi);
            return;
        }
        if (Track(parSrc) is UtTrack src) dc.DrawGeometry(null, new Pen(SelGlow, 6), Line(Geometry(src).Sample(1), 0));
        if (ParOffset(hs) is not var (d, sk)) return;
        List<UtVertex> v; string info;
        if (parD == null)
        {
            var r = Geometry(Track(parSrc));
            v = ParTrack(d, 0, r.Length);
            info = $"odstęp {d.ToString("0.##", CultureInfo.GetCultureInfo("pl-PL"))} m ({(d > 0 ? "po prawej" : "po lewej")}) – klik: początek";
        }
        else
        {
            v = ParTrack(parD.Value, parS1, sk);
            info = $"odstęp {parD.Value.ToString("0.##", CultureInfo.GetCultureInfo("pl-PL"))} m, długość ok. {Len3(Math.Abs(sk - parS1))} – klik: koniec, Enter – do końca toru";
        }
        if (v != null)
        {
            var res = Trasa.Compute(v, KAt(v[0].P));
            dc.DrawGeometry(null, parD == null ? ParPen : new Pen(DraftInk, 2), Line(res.Sample(0.5), 0));
            if (parD != null) { var p0 = ToScreen(v[0].P); dc.DrawRectangle(DraftInk, null, new Rect(p0.X - 3, p0.Y - 3, 6, 6)); }
        }
        Label(dc, Math.Abs(d) < 0.5 && parD == null ? "za blisko toru wzorcowego" : info, hs + new Vector(12, 8), DraftInk, dpi);
    }
}

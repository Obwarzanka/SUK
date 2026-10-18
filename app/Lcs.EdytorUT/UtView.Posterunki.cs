using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Posterunki i szlaki (etap Z0, PLAN_EDYTOR_ZOBRAZOWANIA.md § 6): tory posterunków podświetlone kolorem posterunku, szlaki – droga
/// po torach z numerem. Narzędzie Szlak (B): klik na semaforze wjazdowym albo przy końcu toru na granicy obszaru, potem na drugim końcu.
/// Znaczniki tylko w edytorze (nie na wydruku).
/// </summary>
public partial class UtView
{
    /// <summary>Podświetlenie posterunków i szlaków (panel „Posterunki i szlaki” rozwinięty).</summary>
    public bool ShowPosts;
    /// <summary>Szlak: wybrano pierwszy koniec, czeka na drugi.</summary>
    public event Action<UtLineEnd, UtLineEnd> SzlakPlace;
    UtLineEnd szlakFrom;
    public UtLineEnd SzlakFrom => szlakFrom;

    // ------------------------------------------------------------------ drogi szlaków (pamięć wyniku)
    string drogiSig; Dictionary<string, SzlakDroga> drogiRes;
    public Dictionary<string, SzlakDroga> DrogiNow()
    {
        if (Doc == null) return new();
        var p = Doc.P;
        var sig = Doc.UndoName + "|" + string.Join(";", p.Tracks.Select(Sig)) + "|" + string.Join(";", p.Devices.Where(d => d.Kind == UtDevice.Semafor).Select(d => $"{d.Id}:{d.Track}:{d.S}:{d.Dir}:{d.Funkcja}"))
            + "|" + string.Join(";", p.Lines.Select(l => l.Id + string.Concat(l.Konce.Select(e => $"{e.Semafor}{e.Track}{e.AtStart}"))));
        if (sig == drogiSig && drogiRes != null) return drogiRes;
        drogiSig = sig; drogiRes = Szlaki.Drogi(p); return drogiRes;
    }

    // ------------------------------------------------------------------ narzędzie Szlak
    /// <summary>Koniec szlaku pod kursorem: semafor albo koniec toru (do 14 px).</summary>
    public UtLineEnd HitLineEnd(Point s)
    {
        if (Doc == null) return null;
        if (HitDevice(s) is string did && Doc.P.Devices.FirstOrDefault(x => x.Id == did) is UtDevice d && d.Kind == UtDevice.Semafor)
            return new UtLineEnd { Semafor = d.Id };
        UtLineEnd best = null; double bd = 14;
        foreach (var t in Doc.P.Tracks)
        {
            var r = Geometry(t); if (r.Elements.Count == 0) continue;
            foreach (var atStart in new[] { true, false })
            {
                double dist = (ToScreen(r.At(atStart ? 0 : r.Length).P) - s).Length;
                if (dist < bd) { bd = dist; best = new UtLineEnd { Track = t.Id, AtStart = atStart }; }
            }
        }
        return best;
    }

    void SzlakClick(Point p)
    {
        var e = HitLineEnd(p);
        if (e == null) { DraftInfo?.Invoke("Szlak: kliknij semafor wjazdowy posterunku albo koniec toru na granicy obszaru"); return; }
        if (szlakFrom == null)
        {
            szlakFrom = e;
            DraftInfo?.Invoke($"Szlak od: {Szlaki.Opis(Doc.P, e)} – teraz kliknij drugi koniec (semafor wjazdowy albo koniec toru na granicy obszaru); PPM / Esc – od nowa");
        }
        else
        {
            var a = szlakFrom; szlakFrom = null;
            if (a.Semafor == e.Semafor && a.Track == e.Track && a.AtStart == e.AtStart) { DraftInfo?.Invoke("Szlak: drugi koniec musi być inny niż pierwszy"); return; }
            SzlakPlace?.Invoke(a, e);
        }
        InvalidateVisual();
    }

    public void SzlakCancel() { szlakFrom = null; InvalidateVisual(); }

    // ------------------------------------------------------------------ rysowanie
    static readonly Color[] PostColors =
    {
        Color.FromRgb(0x1f, 0x77, 0xb4), Color.FromRgb(0xd6, 0x27, 0x28), Color.FromRgb(0x2c, 0xa0, 0x2c), Color.FromRgb(0x94, 0x67, 0xbd),
        Color.FromRgb(0xff, 0x7f, 0x0e), Color.FromRgb(0x17, 0xbe, 0xcf), Color.FromRgb(0x8c, 0x56, 0x4b), Color.FromRgb(0xe3, 0x77, 0xc2),
    };
    /// <summary>Kolor posterunku (wg kolejności na liście posterunków).</summary>
    public Color PostColor(string postId) { var i = Doc?.P.Posts.FindIndex(x => x.Id == postId) ?? -1; return i < 0 ? Colors.Gray : PostColors[i % PostColors.Length]; }
    static readonly Color LineColor = Color.FromRgb(0x55, 0x55, 0x55);

    bool PostsVisible => !PrintMode && Doc != null && (ShowPosts || tool == UtTool.Szlak || SelectedObj is (Kontrola.KSzlak or Kontrola.KPosterunek, _));

    List<(XY P, double Az)> PiecePts(TrasaResult r, double s0, double s1)
    {
        var pts = new List<(XY P, double Az)>();
        for (double s = s0; s < s1; s += Math.Max(0.5, mpp * 2)) pts.Add(r.At(s));
        pts.Add(r.At(s1));
        return pts;
    }

    void DrawPosts(DrawingContext dc, double dpi)
    {
        if (!PostsVisible) return;
        var p = Doc.P;
        string selPost = SelectedObj is (Kontrola.KPosterunek, var sp) ? sp : null;
        string selLine = SelectedObj is (Kontrola.KSzlak, var sl) ? sl : null;
        bool all = ShowPosts || tool == UtTool.Szlak;
        // tory posterunków: szeroki półprzezroczysty pas w kolorze posterunku
        foreach (var t in p.Tracks)
        {
            if (t.Post == null || (!all && t.Post != selPost)) continue;
            var r = Geometry(t); if (r.Elements.Count == 0) continue;
            var c = PostColor(t.Post); byte a = (byte)(t.Post == selPost ? 0x80 : 0x45);
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B)), 14) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            dc.DrawGeometry(null, pen, Line(PiecePts(r, 0, r.Length), 0));
        }
        // szlaki: pas kreskowany wzdłuż drogi
        var drogi = DrogiNow();
        foreach (var l in p.Lines)
        {
            if (!all && l.Id != selLine) continue;
            if (!drogi.TryGetValue(l.Id, out var d)) continue;
            bool sel = l.Id == selLine;
            var pen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(sel ? 0xa0 : 0x60), LineColor.R, LineColor.G, LineColor.B)), sel ? 16 : 12) { DashStyle = new DashStyle(new double[] { 0.6, 0.4 }, 0), StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
            foreach (var k in d.Kawalki)
            {
                var t = Track(k.Track); if (t == null) continue;
                var r = Geometry(t); if (r.Elements.Count == 0 || k.S1 - k.S0 < 1e-3) continue;
                dc.DrawGeometry(null, pen, Line(PiecePts(r, k.S0, k.S1), 0));
            }
        }
        // opisy: kody posterunków (przy najdłuższym torze) i numery szlaków (w połowie drogi)
        foreach (var x in p.Posts)
        {
            if (!all && x.Id != selPost) continue;
            var t = p.Tracks.Where(q => q.Post == x.Id).OrderByDescending(q => Geometry(q).Length).FirstOrDefault(); if (t == null) continue;
            var r = Geometry(t); if (r.Elements.Count == 0) continue;
            var c = PostColor(x.Id);
            Label(dc, $"{x.Kod} {x.Nazwa}".Trim(), ToScreen(r.At(r.Length / 2).P) + new Vector(-20, -34), new SolidColorBrush(c), dpi);
        }
        foreach (var l in p.Lines)
        {
            if ((!all && l.Id != selLine) || LineLabelAt(l) is not Rect rr) continue;
            var ft = new FormattedText($"szlak {(l.Numer.Length > 0 ? l.Numer : "?")}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, UiFont, 11.5, Brushes.Black, dpi);
            dc.DrawRoundedRectangle(l.Id == selLine ? new SolidColorBrush(Color.FromRgb(0xff, 0xf0, 0xc0)) : Halo, new Pen(new SolidColorBrush(LineColor), l.Id == selLine ? 1.6 : 0.8), rr, 3, 3);
            dc.DrawText(ft, new Point(rr.X + 4, rr.Y + 1));
            if (drogi.TryGetValue(l.Id, out var d) && d.Err != null) dc.DrawRoundedRectangle(null, ErrPen, rr, 3, 3);
        }
        // narzędzie Szlak: pierwszy koniec
        if (tool == UtTool.Szlak && szlakFrom != null && EndPoint(szlakFrom) is Point ep)
            dc.DrawEllipse(null, new Pen(Brushes.OrangeRed, 2.5), ep, 9, 9);
    }

    Point? EndPoint(UtLineEnd e)
    {
        if (e.Semafor != null) return Doc.P.Devices.FirstOrDefault(x => x.Id == e.Semafor) is UtDevice d ? Pt(d.Track, d.S) : null;
        var t = Track(e.Track); if (t == null) return null;
        var r = Geometry(t); return r.Elements.Count == 0 ? null : ToScreen(r.At(e.AtStart ? 0 : r.Length).P);
    }

    /// <summary>Prostokąt opisu szlaku (w połowie drogi, nad torem); bez drogi – przy pierwszym końcu.</summary>
    Rect? LineLabelAt(UtLine l)
    {
        Point? at = null;
        if (DrogiNow().TryGetValue(l.Id, out var d) && d.Kawalki.Count > 0)
        {
            double half = d.Dlugosc / 2;
            foreach (var k in d.Kawalki)
            {
                if (half <= k.S1 - k.S0 + 1e-9) { at = Pt(k.Track, k.S0 + half); break; }
                half -= k.S1 - k.S0;
            }
        }
        at ??= l.Konce.Count > 0 ? EndPoint(l.Konce[0]) : null;
        if (at is not Point c) return null;
        double w = 8 + 6.6 * (6 + Math.Max(1, l.Numer.Length)), h = 17;
        return new Rect(c.X - w / 2, c.Y - 30, w, h);
    }

    /// <summary>Opis szlaku pod kursorem (gdy posterunki i szlaki są widoczne).</summary>
    (string Kind, string Id)? HitLine(Point s)
    {
        if (!PostsVisible) return null;
        foreach (var l in Doc.P.Lines) if (LineLabelAt(l) is Rect rr && rr.Contains(s)) return (Kontrola.KSzlak, l.Id);
        return null;
    }
}

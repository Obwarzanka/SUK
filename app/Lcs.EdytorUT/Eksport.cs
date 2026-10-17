using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Wydruk w skali (etap 7): plan rysowany tym samym płótnem co w edytorze (<see cref="UtView.PrintMode"/> – bez zaznaczeń, uwag,
/// podglądu na żywo), na kartkach o zadanym formacie albo na jednym arkuszu dopasowanym do planu. Skala jest dokładna: 1 mm na kartce
/// = N mm w układzie PUWG 1992 (Teren: 1:N; Schemat: wzdłuż osi 1:N, w poprzek 1:N/rozciągnięcie – Ig-10 § 3: 1:2000 / 1:500).
/// PNG – z rozdzielczością zapisaną w pliku (pHYs), PDF – strona w wymiarze kartki z obrazem planu.
/// </summary>
public static class Eksport
{
    public record Opcje(bool Schemat, int Scale, double Stretch, int Dpi, string Format, bool WholeProject, XY ViewCenter, double ViewW, double ViewH, IReadOnlyCollection<string> Hidden);
    /// <summary>Strona: obraz, wymiar kartki [mm], przeliczenie świat → piksel strony (do sprawdzenia skali).</summary>
    public record Strona(BitmapSource Image, double WidthMm, double HeightMm, Func<XY, Point> ToPx);

    /// <summary>Formaty kartek (poziomo) [mm]; „dopasowany” – jeden arkusz w wymiarze planu.</summary>
    public static readonly (string Name, double W, double H)[] Formats =
    {
        ("dopasowany (jeden arkusz)", 0, 0), ("A4 poziomo", 297, 210), ("A3 poziomo", 420, 297), ("A2 poziomo", 594, 420), ("A1 poziomo", 841, 594), ("A0 poziomo", 1189, 841),
    };
    public const double MarginMm = 10, CaptionMm = 9;
    const double DipPerMm = 96 / 25.4;

    /// <summary>Zasięg planu: osie torów, perony, przejazdy (z marginesem 15 m).</summary>
    public static List<XY> Extent(UtProject p)
    {
        var pts = new List<XY>();
        foreach (var t in p.Tracks) { var r = Trasa.Compute(t); if (r.Elements.Count > 0) pts.AddRange(r.Sample(5).Select(q => q.P)); }
        foreach (var lc in p.LevelCrossings)
            if (p.Tracks.FirstOrDefault(x => x.Id == lc.Track) is UtTrack t && Trasa.Compute(t) is var r && r.Elements.Count > 0)
            {
                var (q, az) = r.At(Math.Clamp(lc.S, 0, r.Length)); var d = Element.Dir(az + lc.Angle * Math.PI / 180) * (lc.Reach * r.K);
                pts.Add(q + d); pts.Add(q - d);
            }
        return pts;
    }

    public static List<Strona> Render(UtDocument doc, Opcje o)
    {
        var p = doc.P;
        double az = o.Schemat ? p.SchematAz() : Math.PI / 2, str = o.Schemat ? Math.Max(1, o.Stretch) : 1;
        double cosA = Math.Cos(az), sinA = Math.Sin(az);
        double mpp = o.Scale * 0.0254 / 96;                       // m układu na DIP (1/96 cala) kartki wzdłuż osi
        // zasięg w osiach widoku (u – wzdłuż, v – w poprzek) [m]
        double minU, maxU, minV, maxV;
        if (o.WholeProject)
        {
            var pts = Extent(p); if (pts.Count == 0) return new();
            minU = pts.Min(q => q.X * cosA + q.Y * sinA) - 15; maxU = pts.Max(q => q.X * cosA + q.Y * sinA) + 15;
            minV = pts.Min(q => -q.X * sinA + q.Y * cosA) - 15 / str * 2; maxV = pts.Max(q => -q.X * sinA + q.Y * cosA) + 15 / str * 2;
        }
        else
        {
            double uc = o.ViewCenter.X * cosA + o.ViewCenter.Y * sinA, vc = -o.ViewCenter.X * sinA + o.ViewCenter.Y * cosA;
            minU = uc - o.ViewW / 2; maxU = uc + o.ViewW / 2; minV = vc - o.ViewH / 2; maxV = vc + o.ViewH / 2;
        }
        double contentW = (maxU - minU) / mpp, contentH = (maxV - minV) * str / mpp;   // [DIP]
        var fmt = Formats.FirstOrDefault(f => f.Name == o.Format); if (fmt.Name == null) fmt = Formats[0];
        double areaW, areaH;
        if (fmt.W <= 0) { areaW = contentW; areaH = contentH; }
        else { areaW = (fmt.W - 2 * MarginMm) * DipPerMm; areaH = (fmt.H - 2 * MarginMm - CaptionMm) * DipPerMm; }
        int cols = Math.Max(1, (int)Math.Ceiling(contentW / areaW - 1e-6)), rows = Math.Max(1, (int)Math.Ceiling(contentH / areaH - 1e-6));
        double pageWmm = fmt.W > 0 ? fmt.W : areaW / DipPerMm + 2 * MarginMm, pageHmm = fmt.H > 0 ? fmt.H : areaH / DipPerMm + 2 * MarginMm + CaptionMm;
        // plan wyśrodkowany na kartkach
        // jedna kartka w osi – plan na środku; kilka – od początku (bez pustej połowy pierwszej kartki)
        double offU = cols == 1 ? (areaW - contentW) / 2 * mpp : 0, offV = rows == 1 ? (areaH - contentH) / 2 * mpp / str : 0;
        var res = new List<Strona>();
        for (int rI = 0; rI < rows; rI++)
            for (int cI = 0; cI < cols; cI++)
            {
                double u0 = minU - offU + cI * areaW * mpp, v0 = minV - offV + rI * areaH * mpp / str;
                double uc = u0 + areaW * mpp / 2, vc = v0 + areaH * mpp / str / 2;
                var center = new XY(cosA * uc - sinA * vc, sinA * uc + cosA * vc);
                var view = new UtView { Doc = doc, PrintMode = true, PrintPxPerMm = o.Schemat ? null : DipPerMm };
                foreach (var h in o.Hidden) view.Hidden.Add(h);
                view.Hidden.Add("podklady");
                view.SetMode(o.Schemat, az, str);
                view.SetView(center, mpp);
                view.Measure(new Size(areaW, areaH)); view.Arrange(new Rect(0, 0, areaW, areaH));
                string cap = $"{p.Name} – {(o.Schemat ? $"plan schematyczny, skala 1:{o.Scale} wzdłuż, 1:{o.Scale / str:0} w poprzek" : $"skala 1:{o.Scale}")}" +
                    (rows * cols > 1 ? $" – arkusz {rI * cols + cI + 1}/{rows * cols} (wiersz {rI + 1}, kolumna {cI + 1})" : "") + $" – stan na dzień {DateTime.Now:yyyy-MM-dd} – PUWG 1992";
                var img = Page(view, pageWmm, pageHmm, areaW, areaH, o.Dpi, cap);
                double k = o.Dpi / 96.0, mx = MarginMm * DipPerMm, my = MarginMm * DipPerMm;
                var vv = view;
                res.Add(new Strona(img, pageWmm, pageHmm, w => { var s = vv.ToScreen(w); return new Point((s.X + mx) * k, (s.Y + my) * k); }));
            }
        return res;
    }

    static BitmapSource Page(UtView view, double wMm, double hMm, double areaW, double areaH, int dpi, string caption)
    {
        double W = wMm * DipPerMm, H = hMm * DipPerMm, m = MarginMm * DipPerMm;
        var tile = new RenderTargetBitmap((int)Math.Round(areaW * dpi / 96), (int)Math.Round(areaH * dpi / 96), dpi, dpi, PixelFormats.Pbgra32);
        tile.Render(view);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, W, H));
            dc.DrawImage(tile, new Rect(m, m, areaW, areaH));
            var pen = new Pen(Brushes.Black, 0.5 * 0.35 * DipPerMm);
            dc.DrawRectangle(null, pen, new Rect(m, m, areaW, areaH));
            var ft = new FormattedText(caption, CultureInfo.GetCultureInfo("pl-PL"), FlowDirection.LeftToRight, new Typeface("Arial"), 2.5 * DipPerMm, Brushes.Black, 1);
            dc.DrawText(ft, new Point(m, m + areaH + (CaptionMm * DipPerMm - ft.Height) / 2));
            // podziałka 10 cm na kartce – do sprawdzenia wydruku linijką
            double bx = W - m - 100 * DipPerMm, by = m + areaH + CaptionMm * DipPerMm * 0.55;
            if (bx > m + ft.Width + 10)
            {
                dc.DrawLine(new Pen(Brushes.Black, 0.35 * DipPerMm), new Point(bx, by), new Point(bx + 100 * DipPerMm, by));
                for (int i = 0; i <= 10; i++) dc.DrawLine(new Pen(Brushes.Black, 0.2 * DipPerMm), new Point(bx + i * 10 * DipPerMm, by - (i % 5 == 0 ? 2 : 1) * DipPerMm), new Point(bx + i * 10 * DipPerMm, by));
                var t2 = new FormattedText("100 mm", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Arial"), 2 * DipPerMm, Brushes.Black, 1);
                dc.DrawText(t2, new Point(bx - t2.Width - 2 * DipPerMm, by - t2.Height / 2));
            }
        }
        var page = new RenderTargetBitmap((int)Math.Round(W * dpi / 96), (int)Math.Round(H * dpi / 96), dpi, dpi, PixelFormats.Pbgra32);
        page.Render(dv);
        page.Freeze();
        return page;
    }

    /// <summary>PNG (z rozdzielczością w pliku); kilka stron – pliki z numerem: plan_1.png, plan_2.png…</summary>
    public static List<string> SavePng(List<Strona> pages, string path)
    {
        var files = new List<string>();
        for (int i = 0; i < pages.Count; i++)
        {
            var f = pages.Count == 1 ? path : Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + $"_{i + 1}" + Path.GetExtension(path));
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(pages[i].Image));
            using var fs = File.Create(f); enc.Save(fs);
            files.Add(f);
        }
        return files;
    }

    /// <summary>
    /// PDF 1.4: każda strona w wymiarze kartki [pt = mm / 25,4 · 72], obraz planu RGB 8 bit (Flate) na całą stronę – wydruk „w rzeczywistej
    /// wielkości” (bez dopasowania do strony) zachowuje skalę.
    /// </summary>
    public static void SavePdf(List<Strona> pages, string path)
    {
        using var fs = File.Create(path);
        var offs = new List<long>();
        void W(string s) { var b = Encoding.ASCII.GetBytes(s); fs.Write(b, 0, b.Length); }
        void Obj(int n) { while (offs.Count < n) offs.Add(0); offs[n - 1] = fs.Position; W($"{n} 0 obj\n"); }
        W("%PDF-1.4\n%"); fs.Write(new byte[] { 0xe2, 0xe3, 0xcf, 0xd3, 10 }, 0, 5);
        int np = pages.Count;
        // 1 – katalog, 2 – strony, potem na stronę: strona, treść, obraz
        Obj(1); W("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        Obj(2); W("<< /Type /Pages /Kids [" + string.Join(" ", Enumerable.Range(0, np).Select(i => $"{3 + i * 3} 0 R")) + $"] /Count {np} >>\nendobj\n");
        for (int i = 0; i < np; i++)
        {
            var pg = pages[i]; int pn = 3 + i * 3;
            string w = F(pg.WidthMm / 25.4 * 72), h = F(pg.HeightMm / 25.4 * 72);
            Obj(pn); W($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {w} {h}] /Resources << /XObject << /Im0 {pn + 2} 0 R >> >> /Contents {pn + 1} 0 R >>\nendobj\n");
            var content = $"q {w} 0 0 {h} 0 0 cm /Im0 Do Q\n";
            Obj(pn + 1); W($"<< /Length {content.Length} >>\nstream\n{content}endstream\nendobj\n");
            var bmp = pg.Image; int pw = bmp.PixelWidth, ph = bmp.PixelHeight;
            var px = new byte[pw * ph * 4]; bmp.CopyPixels(px, pw * 4, 0);
            byte[] data;
            using (var ms = new MemoryStream())
            {
                using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true))
                {
                    var row = new byte[pw * 3];
                    for (int y = 0; y < ph; y++)
                    {
                        for (int x = 0; x < pw; x++) { int s = (y * pw + x) * 4; row[x * 3] = px[s + 2]; row[x * 3 + 1] = px[s + 1]; row[x * 3 + 2] = px[s]; }
                        z.Write(row, 0, row.Length);
                    }
                }
                data = ms.ToArray();
            }
            Obj(pn + 2); W($"<< /Type /XObject /Subtype /Image /Width {pw} /Height {ph} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode /Length {data.Length} >>\nstream\n");
            fs.Write(data, 0, data.Length); W("\nendstream\nendobj\n");
        }
        long xref = fs.Position;
        W($"xref\n0 {offs.Count + 1}\n0000000000 65535 f \n");
        foreach (var o in offs) W($"{o:0000000000} 00000 n \n");
        W($"trailer\n<< /Size {offs.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
    }

    static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}

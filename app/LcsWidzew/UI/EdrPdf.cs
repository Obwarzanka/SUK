using System.IO;
using System.Text;
using System.Windows.Media.Imaging;

namespace LcsWidzew.UI;

/// <summary>
/// Zapis wyciągu EDR do PDF (E9e, pomysł jak `RasterPdf` w EDR.exe): każda strona to obraz JPEG na stronie A4, bez zewnętrznych bibliotek.
/// </summary>
public static class EdrPdf
{
    /// <summary>Strony (obrazy) do pliku PDF; <paramref name="szer"/> × <paramref name="wys"/> – rozmiar strony w px przy 96 dpi (A4: 794 × 1123).</summary>
    public static void Zapisz(string plik, IReadOnlyList<BitmapSource> strony, double szer, double wys)
    {
        using var f = new FileStream(plik, FileMode.Create, FileAccess.Write);
        var poz = new List<long>();
        void Pisz(string s) { var b = Encoding.ASCII.GetBytes(s); f.Write(b, 0, b.Length); }
        Pisz("%PDF-1.4\n%âãÏÓ\n");
        double w = szer * 72 / 96, h = wys * 72 / 96;
        int n = strony.Count;
        // obiekty: 1 – katalog, 2 – drzewo stron, potem dla każdej strony: strona, treść, obraz
        void Obiekt(int nr) { while (poz.Count < nr) poz.Add(0); poz[nr - 1] = f.Position; Pisz($"{nr} 0 obj\n"); }
        Obiekt(1); Pisz("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        Obiekt(2); Pisz($"<< /Type /Pages /Count {n} /Kids [{string.Join(" ", Enumerable.Range(0, n).Select(i => $"{3 + i * 3} 0 R"))}] >>\nendobj\n");
        for (int i = 0; i < n; i++)
        {
            int s = 3 + i * 3;
            var enc = new JpegBitmapEncoder { QualityLevel = 88 };
            enc.Frames.Add(BitmapFrame.Create(strony[i]));
            using var m = new MemoryStream(); enc.Save(m);
            var jpg = m.ToArray();
            var tresc = string.Format(System.Globalization.CultureInfo.InvariantCulture, "q {0:0.##} 0 0 {1:0.##} 0 0 cm /Im0 Do Q", w, h);
            Obiekt(s); Pisz($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {w.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)} {h.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}] /Resources << /XObject << /Im0 {s + 2} 0 R >> >> /Contents {s + 1} 0 R >>\nendobj\n");
            Obiekt(s + 1); Pisz($"<< /Length {tresc.Length} >>\nstream\n{tresc}\nendstream\nendobj\n");
            Obiekt(s + 2); Pisz($"<< /Type /XObject /Subtype /Image /Width {strony[i].PixelWidth} /Height {strony[i].PixelHeight} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {jpg.Length} >>\nstream\n");
            f.Write(jpg, 0, jpg.Length); Pisz("\nendstream\nendobj\n");
        }
        long xref = f.Position;
        Pisz($"xref\n0 {poz.Count + 1}\n0000000000 65535 f \n");
        foreach (var p in poz) Pisz($"{p:0000000000} 00000 n \n");
        Pisz($"trailer\n<< /Size {poz.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
    }
}

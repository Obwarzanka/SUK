using System.IO;
using System.Printing;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lcs.Model;
using Microsoft.Win32;

namespace Lcs.EdytorZobrazowania2;

/// <summary>
/// Etap E8 (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 9–10): obraz do PNG i wydruk – wygląd jak na pulpicie, bez znaczników edytora (siatka, prowadnice,
/// strefy, węzły, ⚠, szkic); kopia obrazu – punkt wyjścia do drugiego obrazu albo obrazu poglądowego (dalej poprawia człowiek).
/// </summary>
public partial class MainWindow
{
    /// <summary>Obraz jak na pulpicie w skali <paramref name="k"/> pikseli na 1 j.</summary>
    public BitmapSource RenderObraz(ObrObraz o, double k = 1)
    {
        var v = new ObrView { Obraz = o, Siatka = Doc.P.Siatka, ShowGrid = false, ShowRzedy = false, Czysty = true };
        int w = (int)Math.Ceiling(o.Szer * k), h = (int)Math.Ceiling(o.Wys * k);
        v.Measure(new Size(w, h)); v.Arrange(new Rect(0, 0, w, h)); v.SetView(k, new Point(0, 0)); v.UpdateLayout();
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(v); rtb.Freeze();
        return rtb;
    }

    public bool SavePng(string path, double k = 1)
    {
        if (Doc == null || Obraz == null) return false;
        try
        {
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(RenderObraz(Obraz, k)));
            using (var f = File.Create(path)) enc.Save(f);
            Message($"Zapisano obraz „{Obraz.Nazwa}” do {Path.GetFileName(path)} ({Obraz.Szer * k:0} × {Obraz.Wys * k:0} px)");
            return true;
        }
        catch (Exception ex) { Message("Błąd zapisu PNG: " + ex.Message); if (!NoPrompt) MessageBox.Show(this, ex.Message, "PNG"); return false; }
    }

    void ExportPng()
    {
        if (Doc == null || Obraz == null) return;
        var dlg = new SaveFileDialog { Filter = "Obraz PNG (*.png)|*.png", InitialDirectory = Path.GetDirectoryName(Doc.DefaultPath), FileName = SafeName(Obraz.Nazwa) + ".png" };
        if (dlg.ShowDialog(this) != true) return;
        SavePng(dlg.FileName, 1);
    }

    static string SafeName(string n) => string.Concat(n.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    /// <summary>Wydruk obrazu na jednej kartce (poziomo, dopasowany do strony), pod obrazem nazwa obrazu i projektu UT.</summary>
    void PrintObraz()
    {
        if (Doc == null || Obraz == null) return;
        var pd = new PrintDialog();
        try { pd.PrintTicket.PageOrientation = PageOrientation.Landscape; } catch { }
        if (pd.ShowDialog() != true) return;
        pd.PrintVisual(Wydruk(Obraz, pd.PrintableAreaWidth, pd.PrintableAreaHeight), $"Obraz {Obraz.Nazwa}");
        Message($"Wysłano do druku: {Obraz.Nazwa}");
    }

    /// <summary>Strona wydruku: obraz dopasowany do strony <paramref name="pw"/> × <paramref name="ph"/>, podpis w lewym dolnym rogu.</summary>
    public DrawingVisual Wydruk(ObrObraz o, double pw, double ph)
    {
        var bmp = RenderObraz(o, 2);
        double k = Math.Min(pw / o.Szer, (ph - 20) / o.Wys) * 0.95;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawImage(bmp, new Rect((pw - o.Szer * k) / 2, (ph - 20 - o.Wys * k) / 2, o.Szer * k, o.Wys * k));
            dc.DrawText(new FormattedText($"{o.Nazwa} – {Doc.Ut.Name}", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, Brushes.Black, 1.0), new Point(10, ph - 16));
        }
        return dv;
    }

    /// <summary>
    /// Kopia bieżącego obrazu (zakładka zaraz za nim) z rysunkiem, symbolami i przypięciami – np. obraz poglądowy z obrazu szczegółowego
    /// (zmień rodzaj i uprość). Ten sam obiekt UT na dwóch obrazach jest dozwolony.
    /// </summary>
    public ObrObraz CopyImage()
    {
        if (Doc == null || Obraz == null) return null;
        CommitName();
        var src = Obraz;
        var baza = (src.Nazwa.Length > 0 ? src.Nazwa : "Obraz") + " (kopia";
        var nazwa = baza + ")";
        for (int n = 2; Doc.P.Obrazy.Any(o => o.Nazwa == nazwa); n++) nazwa = $"{baza} {n})";
        var k = JsonSerializer.Deserialize<ObrObraz>(JsonSerializer.Serialize(src, UtDocument.Json), UtDocument.Json);
        k.Id = ObrId.Nowy(); k.Nazwa = nazwa; k.Szkic = null;
        var srcId = src.Id;
        Doc.Change("Kopia obrazu", p => p.Obrazy.Insert(p.Obrazy.FindIndex(o => o.Id == srcId) + 1, k));
        ShowImage(k.Id);
        Message($"Kopia obrazu „{src.Nazwa}” → „{nazwa}” ({k.Odcinki.Count} odcinków, {k.Elementy.Count} elementów)" +
            (k.Rodzaj == ZobObraz.Szczegolowy && k.Posterunki.Count > 0 ? " – te same obiekty UT są na obu obrazach; zmień posterunki albo rodzaj (poglądowy)" : ""));
        return k;
    }
}

using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lcs.Model;
using LcsWidzew.Sim;

namespace Lcs.EdytorZobrazowania;

/// <summary>
/// Test etapu Z6 (PLAN_EDYTOR_ZOBRAZOWANIA.md § 4–5): podgląd „jak na pulpicie” mijanki z Z5 (dwa obrazy szczegółowe, poglądowy) –
/// kod pulpitu i komputer zależności symulatora w edytorze: strony z obrazów, stan zasadniczy, przebieg A→C2 przez łącznik wspólny dla
/// obu obrazów (droga zielona na obu stronach, sygnał zezwalający na A), opisy na żądanie, powrót do stanu zasadniczego, pliki bez zmian.
/// </summary>
public static partial class Test
{
    static int Zielone(string png)
    {
        var bmp = new PngBitmapDecoder(new Uri(png), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        var c = new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);
        var px = new byte[c.PixelWidth * c.PixelHeight * 4]; c.CopyPixels(px, c.PixelWidth * 4, 0);
        int n = 0;
        for (int i = 0; i < px.Length; i += 4) if (px[i + 1] > 200 && px[i] < 60 && px[i + 2] < 60) n++;     // zielony pulpitu (0, 230, 0)
        return n;
    }

    static async Task RunZ6(MainWindow w, string shot)
    {
        var dir = Path.Combine(Path.GetTempPath(), "edytor_zob_test", "z5");
        string H(string f) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f)));
        var zobH = H(w.Doc.Path); var utH = H(w.Doc.UtPath);
        var o1 = w.Doc.P.Obrazy.First(o => o.Rodzaj == ZobObraz.Szczegolowy);
        w.ShowImage(o1.Id);
        var pw = w.Podglad();
        Ok(pw != null && pw.Station.View.Pages.Count == 2 && pw.Routes.Count > 0, $"podgląd pulpitu: {pw?.Station.View.Pages.Count} obrazy szczegółowe (bez poglądowego), {pw?.Routes.Count} przebiegów ({w.LastMessage})");
        Ok(pw.Uwagi.Any(u => u.StartsWith("obraz poglądowy")) && pw.Panel.Page.Id == o1.Id && pw.Station.View.PageViews.Count == 2, "strona bieżącego obrazu, geometria każdej strony");
        // okno poza ekranem i bez aktywowania, żeby pulpit się narysował
        pw.WindowStartupLocation = WindowStartupLocation.Manual; pw.ShowActivated = false; pw.ShowInTaskbar = false; pw.Width = 1500; pw.Height = 800;
        pw.Left = SystemParameters.VirtualScreenLeft - 3200; pw.Top = SystemParameters.VirtualScreenTop;
        pw.Show(); await Settle(pw); pw.Panel.FitPage(); await Settle(pw);
        var ind = pw.Ses.Receive();
        Ok(ind.Routes.Count == 0 && ind.Sigs.Values.All(s => s.Aspect == Aspect.Stop), "stan zasadniczy: bez przebiegów, sygnalizatory „Stój”");
        string Png(string name) { var p = Path.Combine(dir, name); pw.Save(p); return p; }
        int g0 = Zielone(Png("pulpit_zasadniczy.png"));
        Ok(g0 < 400, $"stan zasadniczy: na pulpicie tylko zielone kreski „szlak wolny” ({g0} px zielonych)");

        // przykładowy przebieg A → C2 (przez łącznik 1/2 – wspólny dla obu obrazów)
        int ri = pw.Routes.FindIndex(r => r.Type == "P" && LcsWidzew.Model.Station.Lbl(r.Start) == "A" && LcsWidzew.Model.Station.Lbl(r.End) == "C2");
        Ok(ri >= 0, "w tablicy przebiegów: P A → C2");
        pw.RouteBox.SelectedIndex = ri;
        Click(All<ButtonBase>(pw).First(b => b.Content as string == "Nastaw"));
        Ok(pw.Status.Text.StartsWith("✓"), $"Nastaw – polecenie przyjęte ({pw.Status.Text})");
        pw.Czas(30); await Settle(pw);
        ind = pw.Ses.Receive();
        var start = pw.Routes[ri].Start;
        Ok(ind.Routes.Values.Any(r => r.Start == start && r.State != "setting") && ind.Sigs[start].Aspect == Aspect.Train, $"po 30 s: przebieg utwierdzony, semafor A – sygnał zezwalający ({ind.Sigs[start].Aspect})");
        int g1 = Zielone(Png("pulpit_przebieg_s1.png"));
        pw.ShowPage(1); await Settle(pw);
        int g2 = Zielone(Png("pulpit_przebieg_s2.png"));
        Ok(g1 > g0 + 500 && g2 > 300, $"droga przebiegu zielona na stronie 1 ({g1} px) i na stronie 2 – tor 2 i łącznik wspólne ({g2} px)");
        Ok(pw.Panel.Page.Id == w.Doc.P.Obrazy.Where(o => o.Rodzaj == ZobObraz.Szczegolowy).ElementAt(1).Id, "strona 2 – obraz „Mijanka – tor 2”");
        pw.OpisyBox.IsChecked = true; Click(pw.OpisyBox);
        Ok(pw.Panel.ShowOpisy, "Opisy odcinków na żądanie – przełącznik pulpitu");
        pw.ShowPage(0);
        Click(All<ButtonBase>(pw).First(b => b.Content as string == "Stan zasadniczy"));
        Ok(pw.Ses.Receive().Routes.Count == 0 && pw.Panel.Page.Id == o1.Id, "Stan zasadniczy – nowy komputer zależności, ta sama strona");
        Ok(H(w.Doc.Path) == zobH && H(w.Doc.UtPath) == utH, "zobrazowanie i UT bez zmian (podgląd nic nie zapisuje)");

        if (shot != null)
        {
            string Shot(string suf) => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(shot))!, Path.GetFileNameWithoutExtension(shot) + suf);
            File.Copy(Path.Combine(dir, "pulpit_przebieg_s1.png"), Shot("_z6_s1.png"), true);
            File.Copy(Path.Combine(dir, "pulpit_przebieg_s2.png"), Shot("_z6_s2.png"), true);
            pw.Nastaw(ri); pw.Czas(30); await Settle(pw);
            Save(pw, Shot("_z6_okno.png"));
        }
        pw.Close();
    }
}

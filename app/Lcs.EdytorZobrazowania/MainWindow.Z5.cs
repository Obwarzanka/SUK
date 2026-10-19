using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lcs.Model;
using Microsoft.Win32;

namespace Lcs.EdytorZobrazowania;

/// <summary>
/// Etap Z5 w oknie (PLAN_EDYTOR_ZOBRAZOWANIA.md § 4–5): obiekty innych posterunków w panelu UT (ten sam obiekt na dwóch obrazach),
/// „Przypnij do innego obiektu UT”, obraz do PNG i wydruk – wygląd jak na pulpicie, bez znaczników edytora.
/// </summary>
public partial class MainWindow
{
    public readonly CheckBox InneBox = new()
    {
        Content = "obiekty innych posterunków", Margin = new Thickness(0, 0, 0, 6),
        ToolTip = "Pokaż w panelu także obiekty UT innych posterunków – ten sam obiekt może być na dwóch obrazach (np. wspólna głowica); „Sprawdź” ich nie wymaga",
    };
    bool utInne;
    static readonly Brush ObcyOnFg = new SolidColorBrush(Color.FromRgb(0x50, 0x80, 0x50));

    // ------------------------------------------------------------------ przypięcie do innego obiektu UT
    /// <summary>Panel symbolu: przypięcie do innego obiektu UT tego samego rodzaju (np. po rozcięciu toru albo zmianie w UT).</summary>
    void PrzypnijSym(StackPanel P, ZobRodzaj r, ZobSymbol s)
    {
        if (r == ZobRodzaj.Szlak) return;
        var lista = ZobUt.Obiekty(Doc.Ut, Obraz, true).Where(x => x.Grupa == Grupa(r) && ZobSymbole.RodzajUt(Doc.Ut, x.Id) == r && x.Id != s.Ut && !x.NaObrazie).ToList();
        if (lista.Count == 0) return;
        P.Children.Add(Label("Przypnij do innego obiektu UT"));
        var cb = new ComboBox { Margin = new Thickness(0, 2, 0, 4) };
        foreach (var x in lista) cb.Items.Add(x.Opis);
        var b = Btn("Przypnij", (a, e) =>
        {
            if (cb.SelectedIndex < 0) return;
            var nu = lista[cb.SelectedIndex].Id; var old = s.Ut;
            Ch("Przypięcie do innego obiektu UT", o => { var x = ZobSymbole.Lista(o, r).FirstOrDefault(q => q.Ut == old); if (x != null) x.Ut = nu; });
            View.SelectSym(r, nu);
            Message($"Symbol przypięty do: {SymOpis(nu)}");
        }, "Symbol zostaje w tym miejscu, przypięty do wybranego obiektu UT");
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        cb.Width = 180; row.Children.Add(cb); b.Margin = new Thickness(6, 0, 0, 0); row.Children.Add(b);
        P.Children.Add(row);
    }

    /// <summary>Panel toru obrazu: przypięcie do innego toru UT (km przycinane do jego długości).</summary>
    void PrzypnijTor(StackPanel P, ZobTor t)
    {
        var lista = ZobUt.Obiekty(Doc.Ut, Obraz, true).Where(x => x.Grupa == ZobUt.GTory && x.Id != t.Ut.Tor).ToList();
        if (lista.Count == 0) return;
        P.Children.Add(Label("Przypnij do innego toru UT"));
        var cb = new ComboBox { Margin = new Thickness(0, 2, 0, 4), Width = 180 };
        foreach (var x in lista) cb.Items.Add(x.Opis);
        var b = Btn("Przypnij", (a, e) =>
        {
            if (cb.SelectedIndex < 0) return;
            var nu = lista[cb.SelectedIndex].Id; double len = UtLen(nu); var id = t.Id;
            double s0 = Math.Round(Math.Clamp(t.Ut.S0, 0, len), 2), s1 = Math.Round(Math.Clamp(t.Ut.S1, 0, len), 2);
            if (Math.Abs(s1 - s0) < 0.5) { s0 = t.Ut.S0 <= t.Ut.S1 ? 0 : Math.Round(len, 2); s1 = t.Ut.S0 <= t.Ut.S1 ? Math.Round(len, 2) : 0; }
            if (ZobTory.Zachodzi(Obraz, nu, s0, s1, id)) { Message("Ten kilometraż toru jest już na obrazie w innym kawałku"); return; }
            Ch("Przypięcie toru do innego toru UT", o => { var x = o.Tory.First(q => q.Id == id); x.Ut.Tor = nu; x.Ut.S0 = s0; x.Ut.S1 = s1; });
            Message($"Tor obrazu przypięty do: {TorName(nu)}, km {Km(s0)}–{Km(s1)}");
        }, "Łamana zostaje, zmienia się tor UT (i km, jeśli nowy tor jest krótszy)");
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        row.Children.Add(cb); b.Margin = new Thickness(6, 0, 0, 0); row.Children.Add(b);
        P.Children.Add(row);
    }

    // ------------------------------------------------------------------ podgląd pulpitu (Z6)
    /// <summary>Okno podglądu „jak na pulpicie” dla bieżącego zobrazowania, na stronie bieżącego obrazu (obraz poglądowy – pierwsza strona).</summary>
    public PodgladWindow Podglad()
    {
        if (Doc == null) { Message("Podgląd pulpitu: najpierw otwórz zobrazowanie"); return null; }
        CommitName();
        try
        {
            var pw = new PodgladWindow(Doc.Ut, Doc.P, Obraz?.Rodzaj == ZobObraz.Szczegolowy ? Obraz.Id : null) { Owner = this };
            if (!NoPrompt) pw.Show();
            Message($"Podgląd pulpitu: {pw.Station.View.Pages.Count} obrazów, {pw.Routes.Count} przebiegów" + (pw.Uwagi.Count > 0 ? $", uwagi przekształcenia: {pw.Uwagi.Count}" : ""));
            return pw;
        }
        catch (Exception ex) { Message("Podgląd pulpitu: " + ex.Message); if (!NoPrompt) MessageBox.Show(this, ex.Message, "Podgląd pulpitu"); return null; }
    }

    // ------------------------------------------------------------------ PNG i wydruk
    /// <summary>Obraz jak na pulpicie (stan zasadniczy, bez znaczników edytora) w skali <paramref name="k"/> pikseli na 1 j.</summary>
    public BitmapSource RenderObraz(ZobObraz o, double k = 1)
    {
        var v = new ZobView { Obraz = o, Ut = Doc.Ut, Siatka = Doc.P.Siatka, ShowGrid = false, Czysty = true };
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

    /// <summary>Wydruk obrazu na jednej kartce (poziomo, dopasowany do strony).</summary>
    void PrintObraz()
    {
        if (Doc == null || Obraz == null) return;
        var pd = new PrintDialog();
        try { pd.PrintTicket.PageOrientation = PageOrientation.Landscape; } catch { }
        if (pd.ShowDialog() != true) return;
        var bmp = RenderObraz(Obraz, 2);
        double pw = pd.PrintableAreaWidth, ph = pd.PrintableAreaHeight, k = Math.Min(pw / Obraz.Szer, ph / Obraz.Wys) * 0.95;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawImage(bmp, new Rect((pw - Obraz.Szer * k) / 2, (ph - Obraz.Wys * k) / 2, Obraz.Szer * k, Obraz.Wys * k));
            dc.DrawText(new FormattedText($"{Obraz.Nazwa} – {Doc.Ut.Name}", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, Brushes.Black, 1.0), new Point(10, ph - 16));
        }
        pd.PrintVisual(dv, $"Obraz {Obraz.Nazwa}");
        Message($"Wysłano do druku: {Obraz.Nazwa}");
    }
}

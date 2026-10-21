using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;
using Microsoft.Win32;

namespace Lcs.EdytorUT;

/// <summary>
/// Etap 7 w oknie: kontrola układu („Sprawdź” – lista uwag, klik pokazuje miejsce i zaznacza obiekt), warstwy (widoczność, Ig-10 § 4
/// ust. 4), wydruk w skali do PNG / PDF, okno profilu podłużnego.
/// </summary>
public partial class MainWindow
{
    public readonly Expander CheckExpander = new() { IsExpanded = false, Margin = new Thickness(0, 0, 0, 6) };
    public readonly Expander LayerExpander = new() { IsExpanded = false, Margin = new Thickness(0, 0, 0, 6) };
    public readonly ListBox CheckList = new() { MaxHeight = 320, FontSize = 12, BorderThickness = new Thickness(0) };
    public List<Uwaga> LastCheck { get; private set; } = new();
    readonly Dictionary<string, CheckBox> layerBoxes = new();

    void InitKontrola()
    {
        CheckExpander.Header = Header("Kontrola układu");
        var cp = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
        cp.Children.Add(SmallBtn("Sprawdź ponownie", () => RunCheck(true), "Kontrola całego układu (F7)"));
        ScrollViewer.SetHorizontalScrollBarVisibility(CheckList, ScrollBarVisibility.Disabled);
        CheckList.SelectionChanged += (s, e) => { if (CheckList.SelectedItem is ListBoxItem li && li.Tag is Uwaga u) ShowUwaga(u); };
        cp.Children.Add(CheckList);
        CheckExpander.Content = cp;

        LayerExpander.Header = Header("Warstwy");
        var lp = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
        lp.Children.Add(new TextBlock { Text = "Nazwy wg Ig-10 zał. 8. Widoczność dotyczy ekranu i wydruku.", Foreground = Brushes.DimGray, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) });
        foreach (var (key, name) in UtView.Warstwy)
        {
            var cb = new CheckBox { Content = name, IsChecked = true, Margin = new Thickness(0, 1, 0, 1) };
            string k = key;
            cb.Click += (s, e) => SetLayer(k, cb.IsChecked == true);
            layerBoxes[key] = cb; lp.Children.Add(cb);
        }
        LayerExpander.Content = lp;
    }

    /// <summary>Widoczność warstwy (zapisywana z widokiem, bez cofania).</summary>
    public void SetLayer(string key, bool on)
    {
        if (on) View.Hidden.Remove(key); else View.Hidden.Add(key);
        Doc.P.View.Hidden = View.Hidden.Count > 0 ? View.Hidden.ToList() : null;
        if (layerBoxes.TryGetValue(key, out var cb)) cb.IsChecked = on;
        if (key == "podklady") View.Live.Schedule();
        View.InvalidateVisual();
    }

    /// <summary>Warstwy z widoku projektu (po otwarciu).</summary>
    void ApplyLayers()
    {
        View.Hidden.Clear();
        foreach (var h in Doc.P.View.Hidden ?? new()) View.Hidden.Add(h);
        foreach (var (k, cb) in layerBoxes) cb.IsChecked = !View.Hidden.Contains(k);
    }

    public readonly Expander RepairExpander = new() { IsExpanded = false, Margin = new Thickness(0, 0, 0, 6), Visibility = Visibility.Collapsed };
    public readonly ListBox RepairList = new() { MaxHeight = 320, FontSize = 12, BorderThickness = new Thickness(0) };
    public NaprawaWynik LastRepair { get; private set; }

    /// <summary>
    /// „Napraw tory i łuki” (cały układ albo tor <paramref name="track"/>, <see cref="Naprawa.Napraw"/>): zbędne wierzchołki, łuki, które się
    /// nie mieszczą, rozjazdy na granicy łuku i na przejściowej; jedna zmiana (Ctrl+Z). Wynik – lista w panelu „Naprawa”.
    /// </summary>
    public NaprawaWynik RepairAll(string track = null)
    {
        CommitPending();
        NaprawaWynik res = null;
        Doc.Change(track != null ? "Napraw tor i łuki" : "Napraw tory i łuki", p => res = Naprawa.Napraw(p, track));
        LastRepair = res;
        RepairList.Items.Clear();
        ScrollViewer.SetHorizontalScrollBarVisibility(RepairList, ScrollBarVisibility.Disabled);
        foreach (var z in res.Zmiany) RepairList.Items.Add(new ListBoxItem { Content = new TextBlock { Text = "✔ " + z, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.ForestGreen, Width = 290 } });
        foreach (var z in res.Zostalo) RepairList.Items.Add(new ListBoxItem { Content = new TextBlock { Text = "⚠ " + z, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DarkOrange, Width = 290 } });
        if (RepairList.Items.Count == 0) RepairList.Items.Add(new ListBoxItem { Content = new TextBlock { Text = "✔ Nie było czego naprawiać", Foreground = Brushes.ForestGreen }, IsEnabled = false });
        RepairExpander.Header = Header($"Naprawa – zmiany: {res.Zmiany.Count}, do decyzji: {res.Zostalo.Count}");
        RepairExpander.Content = RepairList;
        RepairExpander.Visibility = Visibility.Visible; RepairExpander.IsExpanded = true;
        Message(res.Zmiany.Count == 0 ? $"Napraw tory i łuki: bez zmian{(res.Zostalo.Count > 0 ? $", do decyzji {res.Zostalo.Count} – lista w panelu „Naprawa”" : "")}"
            : $"Napraw tory i łuki: zmian {res.Zmiany.Count} (Ctrl+Z cofa wszystkie), do decyzji {res.Zostalo.Count} – lista w panelu „Naprawa”");
        return res;
    }

    /// <summary>Kontrola układu: lista w panelu (błędy na czerwono, uwagi na pomarańczowo); <paramref name="open"/> – rozwija listę.</summary>
    public List<Uwaga> RunCheck(bool open)
    {
        LastCheck = Kontrola.Run(Doc.P);
        CheckList.Items.Clear();
        int ne = LastCheck.Count(u => u.Error), nw = LastCheck.Count - ne;
        foreach (var u in LastCheck)
            CheckList.Items.Add(new ListBoxItem
            {
                Content = new TextBlock { Text = (u.Error ? "✖ " : "⚠ ") + u.Text, TextWrapping = TextWrapping.Wrap, Foreground = u.Error ? Brushes.Firebrick : Brushes.DarkOrange, Width = 290 },
                Tag = u, ToolTip = u.At is XY ? "Kliknij – pokaż na planie" : null,
            });
        if (LastCheck.Count == 0) CheckList.Items.Add(new ListBoxItem { Content = new TextBlock { Text = "✔ Bez uwag", Foreground = Brushes.ForestGreen }, IsEnabled = false });
        CheckExpander.Header = Header(LastCheck.Count == 0 ? "Kontrola układu – bez uwag" : $"Kontrola układu – błędy: {ne}, uwagi: {nw}");
        if (open) CheckExpander.IsExpanded = true;
        Message(LastCheck.Count == 0 ? "Sprawdź: układ bez uwag" : $"Sprawdź: błędy {ne}, uwagi {nw} – lista w panelu „Kontrola układu”, klik pokazuje miejsce");
        return LastCheck;
    }

    /// <summary>Uwaga na planie: widok w miejscu, zaznaczenie obiektu.</summary>
    public void ShowUwaga(Uwaga u)
    {
        if (u.At is XY at) View.SetView(at, Math.Min(View.Mpp, 0.3));
        switch (u.Kind)
        {
            case Kontrola.KTor when Doc.P.Tracks.Any(t => t.Id == u.Id): View.Select(u.Id); break;
            case Kontrola.KRozjazd when Doc.P.Turnouts.Any(t => t.Id == u.Id): View.SelectTurnout(u.Id); break;
            case Kontrola.KSkrzyzowanie when Doc.P.Crossings.Any(t => t.Id == u.Id): View.SelectCrossing(u.Id); break;
            case Kontrola.KUrzadzenie when Doc.P.Devices.Any(t => t.Id == u.Id): View.SelectDevice(u.Id); break;
            case Kontrola.KOdcinek or Kontrola.KPeron or Kontrola.KPrzejazd or Kontrola.KKm or Kontrola.KSzlak or Kontrola.KPosterunek when u.Id != null: View.SelectObj(u.Kind, u.Id); break;
        }
        Message(u.Text);
    }

    // ------------------------------------------------------------------ eksport
    /// <summary>Opcje eksportu z bieżącego widoku (Schemat: skala arkusza projektu i rozciągnięcie widoku).</summary>
    public Eksport.Opcje ExportOptions(bool schemat, int scale, int dpi, string format, bool whole) =>
        new(schemat, scale, schemat ? View.Stretch : 1, dpi, format, whole, View.Center, View.ActualWidth * View.Mpp, View.ActualHeight * View.Mpp / View.Stretch, View.Hidden.ToList());

    void OpenExport()
    {
        var dlg = new Window { Title = "Eksport w skali", Owner = this, SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, FontFamily = FontFamily, FontSize = 13 };
        var sp = new StackPanel { Margin = new Thickness(14), Width = 380 };
        ComboBox Combo(string label, IEnumerable<string> items, int sel) { sp.Children.Add(Label(label)); var c = new ComboBox { Margin = new Thickness(0, 2, 0, 8) }; foreach (var i in items) c.Items.Add(i); c.SelectedIndex = sel; sp.Children.Add(c); return c; }
        var view = Combo("Widok", new[] { $"Schemat Ig-10 (oś poziomo, w poprzek ×{View.Stretch:0})", "Teren (północ u góry, 1:1)" }, View.Schemat ? 0 : 1);
        int[] scales = { 500, 1000, 2000, 4000, 5000 };
        var scale = Combo("Skala (Schemat – wzdłuż osi)", scales.Select(s => $"1:{s}"), View.Schemat ? Array.IndexOf(scales, Doc.P.DrawingScale) : 1);
        view.SelectionChanged += (s, e) => scale.SelectedIndex = view.SelectedIndex == 0 ? Math.Max(0, Array.IndexOf(scales, Doc.P.DrawingScale)) : 1;
        var range = Combo("Zakres", new[] { "cały projekt", "bieżący widok" }, 0);
        var fmt = Combo("Kartka", Eksport.Formats.Select(f => f.Name), 0);
        int[] dpis = { 150, 200, 300 };
        var dpi = Combo("Rozdzielczość", dpis.Select(d => $"{d} dpi"), 1);
        sp.Children.Add(new TextBlock { Text = "Drukuj w „rzeczywistym rozmiarze” (bez dopasowania do strony) – wtedy 1 mm na kartce = skala. Na dole kartki podziałka 100 mm do sprawdzenia linijką. Podkłady (ortofotomapa) nie są drukowane.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, FontSize = 12, Margin = new Thickness(0, 0, 0, 8) });
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = new Button { Content = "Zapisz…", IsDefault = true, Width = 90, Margin = new Thickness(0, 0, 6, 0) };
        var cancel = new Button { Content = "Anuluj", IsCancel = true, Width = 80 };
        ok.Click += (s, e) =>
        {
            var sd = new SaveFileDialog { Filter = "PDF (*.pdf)|*.pdf|Obraz PNG (*.png)|*.png", FileName = SafeName(Doc.P.Name) + ".pdf", InitialDirectory = Doc.Dir ?? ProjectsDir() };
            if (sd.ShowDialog(dlg) != true) return;
            var o = ExportOptions(view.SelectedIndex == 0, scales[scale.SelectedIndex], dpis[dpi.SelectedIndex], Eksport.Formats[fmt.SelectedIndex].Name, range.SelectedIndex == 0);
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                var n = ExportTo(sd.FileName, o);
                dlg.DialogResult = true;
                Message($"Zapisano {Path.GetFileName(sd.FileName)}: {n} {(n == 1 ? "kartka" : "kartek")}, skala 1:{o.Scale}");
            }
            catch (Exception ex) { MessageBox.Show(dlg, ex.Message, "Eksport"); }
            finally { Mouse.OverrideCursor = null; }
        };
        row.Children.Add(ok); row.Children.Add(cancel); sp.Children.Add(row);
        dlg.Content = sp; dlg.ShowDialog();
    }

    /// <summary>Eksport do pliku (.pdf albo .png); wynik – liczba kartek.</summary>
    public int ExportTo(string file, Eksport.Opcje o)
    {
        var pages = Eksport.Render(Doc, o);
        if (pages.Count == 0) throw new InvalidOperationException("Pusty projekt – nie ma czego drukować");
        if (file.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) Eksport.SavePng(pages, file); else Eksport.SavePdf(pages, file);
        LastExport = pages;
        return pages.Count;
    }
    /// <summary>Ostatni eksport (test: sprawdzenie skali).</summary>
    public List<Eksport.Strona> LastExport { get; private set; }

    // ------------------------------------------------------------------ profil
    ProfilWindow profWin;
    /// <summary>Okno profilu podłużnego zaznaczonego toru (jedno, przełącza się z zaznaczeniem).</summary>
    public ProfilWindow OpenProfile(bool show = true)
    {
        var id = View.SelectedTrack;
        if (id == null) { Message("Profil: zaznacz tor (Wskaż, klik na torze)"); return null; }
        if (profWin == null || profWin.WasClosed)
        {
            profWin = new ProfilWindow(this) { Owner = this };
            profWin.Closed += (s, e) => profWin.WasClosed = true;
        }
        profWin.SetTrack(id);
        if (show) { profWin.Show(); profWin.Activate(); }
        return profWin;
    }
}

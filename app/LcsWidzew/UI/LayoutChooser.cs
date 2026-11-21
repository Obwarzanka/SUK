using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LcsWidzew.Net;

namespace LcsWidzew.UI;

/// <summary>
/// Wybór wersji układu przy starcie: układ podstawowy (Data/station.json) albo projekty z Data/projekty
/// (np. układ z generatora OSM, ten sam układ ze zobrazowaniem przeniesionym ze starego). Ostatni wybór zapamiętany.
/// </summary>
public static class LayoutChooser
{
    public static string Choose(string basePath)
    {
        var dataDir = Path.GetDirectoryName(basePath) ?? ".";
        var files = new List<string> { basePath };
        var proj = Path.Combine(dataDir, "projekty");
        if (Directory.Exists(proj))
            files.AddRange(Directory.GetFiles(proj, "*.json").Where(f => !Path.GetFileName(f).Contains("raport") && !Path.GetFileNameWithoutExtension(f).Contains('.')).OrderBy(f => f));
        if (files.Count < 2) return basePath;
        var memo = Path.Combine(dataDir, "ostatni_uklad.txt");
        string last = null;
        try { if (File.Exists(memo)) last = File.ReadAllText(memo).Trim(); } catch { }

        string Describe(string f)
        {
            string name = "";
            try { using var d = JsonDocument.Parse(File.ReadAllText(f)); if (d.RootElement.TryGetProperty("name", out var n)) name = n.GetString(); } catch { name = "(nie można odczytać)"; }
            var rel = Path.GetRelativePath(dataDir, f);
            string sr = "";
            try { var m = SimRailMapa.Wczytaj(SimRailMapa.PlikDla(f)); if (m.Posterunki.Count > 0) sr = $" · SimRail: {string.Join(", ", m.Posterunki)} ({m.Mapa.Count} obiektów w mapie)"; } catch { }
            return $"{name}\n{rel} · zmieniony {File.GetLastWriteTime(f):yyyy-MM-dd HH:mm}{sr}";
        }
        var w = new Window { Title = "Wybór układu – LCS", Width = 700, Height = 420, WindowStartupLocation = WindowStartupLocation.CenterScreen, Background = new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x1e)), ResizeMode = ResizeMode.NoResize };
        var sp = new StackPanel { Margin = new Thickness(14) };
        sp.Children.Add(new TextBlock { Text = "Którą wersję układu uruchomić?", Foreground = Brushes.White, FontSize = 15, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) });
        var lb = new ListBox { Height = 230, Background = Brushes.Black, Foreground = Brushes.White, FontSize = 12.5 };
        foreach (var f in files) lb.Items.Add(new ListBoxItem { Content = (f == basePath ? "★ " : "") + Describe(f), Tag = f, Padding = new Thickness(4, 3, 4, 3) });
        lb.SelectedIndex = Math.Max(0, files.FindIndex(f => string.Equals(f, last, StringComparison.OrdinalIgnoreCase)));
        sp.Children.Add(lb);
        // posterunek SimRail: krótki nasłuch moda SimRailMost i propozycja układu przypisanego do tego posterunku (<układ>.simrail.json)
        // LCS_SIMRAIL_PORT – inny port stanów moda (jak w MelonPreferences.cfg, sekcja [SimRailMost]; także do testów)
        int portGry = int.TryParse(Environment.GetEnvironmentVariable("LCS_SIMRAIL_PORT"), out var pg) && pg is > 0 and < 65536 ? pg : SimRailOdbiornik.DomyslnyPort;
        var gra = new TextBlock { Foreground = Brushes.Gray, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0),
                                  Text = $"SimRail: nasłuch posterunku z gry (UDP {portGry})…" };
        sp.Children.Add(gra);
        var stop = new System.Threading.CancellationTokenSource();
        bool wybranoRecznie = false;
        lb.PreviewMouseDown += (a, e) => wybranoRecznie = true;
        lb.PreviewKeyDown += (a, e) => wybranoRecznie = true;
        w.Loaded += async (a, e) =>
        {
            var st = await SimRailMapa.WykryjPosterunek(portGry, TimeSpan.FromSeconds(30), stop.Token);
            if (stop.IsCancellationRequested) return;
            if (st == null) { gra.Text = $"SimRail: brak posterunku z gry – mod SimRailMost nie nadaje albo port {portGry} zajmuje inny program."; return; }
            var pasuje = SimRailMapa.UkladyDlaPosterunku(files, st);
            if (pasuje.Count == 0)
            {
                gra.Foreground = Brushes.Orange;
                gra.Text = $"SimRail: w grze posterunek „{st}” – żaden układ nie jest do niego przypisany. Układ projektuje się w edytorach UT i zobrazowania; " +
                           "posterunek przypisuje się w oknie „SimRail” (przycisk „Przypisz posterunek z gry”).";
                return;
            }
            gra.Foreground = Brushes.LightGreen;
            gra.Text = $"SimRail: w grze posterunek „{st}” → {string.Join(", ", pasuje.Select(p => Path.GetRelativePath(dataDir, p)))}" + (wybranoRecznie ? "" : " (zaznaczony)");
            if (!wybranoRecznie) lb.SelectedIndex = files.FindIndex(f => string.Equals(f, pasuje[0], StringComparison.OrdinalIgnoreCase));
        };
        w.Closed += (a, e) => stop.Cancel();
        string result = null;
        var bp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        var ok = new Button { Content = "Uruchom", Padding = new Thickness(16, 4, 16, 4), IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Zamknij", Padding = new Thickness(16, 4, 16, 4), IsCancel = true };
        ok.Click += (a, e) => { result = (lb.SelectedItem as ListBoxItem)?.Tag as string; w.Close(); };
        lb.MouseDoubleClick += (a, e) => { result = (lb.SelectedItem as ListBoxItem)?.Tag as string; w.Close(); };
        cancel.Click += (a, e) => w.Close();
        bp.Children.Add(ok); bp.Children.Add(cancel); sp.Children.Add(bp);
        w.Content = sp;
        w.ShowDialog();
        if (result != null) try { File.WriteAllText(memo, result); } catch { }
        return result;
    }
}

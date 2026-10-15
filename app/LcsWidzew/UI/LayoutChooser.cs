using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
            files.AddRange(Directory.GetFiles(proj, "*.json").Where(f => !Path.GetFileName(f).Contains("raport")).OrderBy(f => f));
        if (files.Count < 2) return basePath;
        var memo = Path.Combine(dataDir, "ostatni_uklad.txt");
        string last = null;
        try { if (File.Exists(memo)) last = File.ReadAllText(memo).Trim(); } catch { }

        string Describe(string f)
        {
            string name = "";
            try { using var d = JsonDocument.Parse(File.ReadAllText(f)); if (d.RootElement.TryGetProperty("name", out var n)) name = n.GetString(); } catch { name = "(nie można odczytać)"; }
            var rel = Path.GetRelativePath(dataDir, f);
            return $"{name}\n{rel} · zmieniony {File.GetLastWriteTime(f):yyyy-MM-dd HH:mm}";
        }
        var w = new Window { Title = "Wybór układu – LCS", Width = 640, Height = 380, WindowStartupLocation = WindowStartupLocation.CenterScreen, Background = new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x1e)), ResizeMode = ResizeMode.NoResize };
        var sp = new StackPanel { Margin = new Thickness(14) };
        sp.Children.Add(new TextBlock { Text = "Którą wersję układu uruchomić?", Foreground = Brushes.White, FontSize = 15, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) });
        var lb = new ListBox { Height = 230, Background = Brushes.Black, Foreground = Brushes.White, FontSize = 12.5 };
        foreach (var f in files) lb.Items.Add(new ListBoxItem { Content = (f == basePath ? "★ " : "") + Describe(f), Tag = f, Padding = new Thickness(4, 3, 4, 3) });
        lb.SelectedIndex = Math.Max(0, files.FindIndex(f => string.Equals(f, last, StringComparison.OrdinalIgnoreCase)));
        sp.Children.Add(lb);
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

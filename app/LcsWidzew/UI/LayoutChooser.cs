using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LcsWidzew.SimRail;

namespace LcsWidzew.UI;

/// <summary>
/// Wybór układu przy starcie, w dwóch osobnych częściach:
/// <list type="bullet">
/// <item><b>SUK – układ torowy</b>: układ podstawowy (Data/station.json) i projekty z Data/projekty – własny komputer zależności
///       (z UT); taki układ może też mieć mapę SimRail (<c>&lt;układ&gt;.simrail.json</c>) i działać jako pulpit zdalny z okna „SimRail”;</item>
/// <item><b>SimRail – pulpit zdalny</b>: posterunki zbudowane z pulpitów gry (<see cref="SimRailStacje"/>, posterunek.cmd) – stany
///       i zależności z gry, uruchamiane od razu jako pulpit zdalny SimRail.</item>
/// </list>
/// Krótki nasłuch moda SimRailMost zaznacza układ przypisany do posterunku z gry. Ostatni wybór zapamiętany.
/// </summary>
public static class LayoutChooser
{
    sealed record Pozycja(string Plik, bool SimRail);

    /// <summary>Wybrany plik układu (null – zamknięto okno); <paramref name="simRail"/> – posterunek SimRail (start jako pulpit zdalny).</summary>
    public static string Choose(string basePath, out bool simRail)
    {
        simRail = false;
        var dataDir = Path.GetDirectoryName(basePath) ?? ".";
        var uklady = new List<string> { basePath };
        var proj = Path.Combine(dataDir, "projekty");
        if (Directory.Exists(proj))
            uklady.AddRange(Directory.GetFiles(proj, "*.json").Where(f => !Path.GetFileName(f).Contains("raport") && !Path.GetFileNameWithoutExtension(f).Contains('.')).OrderBy(f => f));
        var katSr = SimRailStacje.Katalog();
        var posterunki = SimRailStacje.Lista(katSr);
        if (uklady.Count + posterunki.Count < 2) return basePath;
        var memo = Path.Combine(dataDir, "ostatni_uklad.txt");
        string last = null;
        try { if (File.Exists(memo)) last = File.ReadAllText(memo).Trim(); } catch { }

        string OpisUkladu(string f)
        {
            string name = "";
            try { using var d = JsonDocument.Parse(File.ReadAllText(f)); if (d.RootElement.TryGetProperty("name", out var n)) name = n.GetString(); } catch { name = "(nie można odczytać)"; }
            var rel = Path.GetRelativePath(dataDir, f);
            string sr = "";
            try { var m = SimRailMapa.Wczytaj(SimRailMapa.PlikDla(f)); if (m.Posterunki.Count > 0) sr = $" · mapa SimRail: {string.Join(", ", m.Posterunki)} ({m.Mapa.Count} obiektów)"; } catch { }
            return $"{(f == basePath ? "★ " : "")}{name}\n{rel} · zmieniony {File.GetLastWriteTime(f):yyyy-MM-dd HH:mm}{sr}";
        }
        string OpisPosterunku(SimRailStacje.Uklad u) =>
            $"{string.Join(", ", u.Posterunki.DefaultIfEmpty(u.Nazwa))}{(u.Kody.Count > 0 ? $" ({string.Join(", ", u.Kody.Distinct())})" : "")}\n" +
            $"{u.Nazwa} · pulpit z {File.GetLastWriteTime(u.Pulpit):yyyy-MM-dd HH:mm}" +
            (u.Nieaktualny ? " · zobrazowanie nowsze – uruchom przez posterunek.cmd uruchom, żeby przebudować" : "");

        var w = new Window { Title = "Wybór układu – LCS", Width = 720, Height = 520, WindowStartupLocation = WindowStartupLocation.CenterScreen, Background = new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x1e)), ResizeMode = ResizeMode.NoResize };
        var sp = new StackPanel { Margin = new Thickness(14) };
        sp.Children.Add(new TextBlock { Text = "Którą wersję układu uruchomić?", Foreground = Brushes.White, FontSize = 15, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) });
        var lb = new ListBox { Height = 340, Background = Brushes.Black, Foreground = Brushes.White, FontSize = 12.5 };
        void Naglowek(string t, string pod) => lb.Items.Add(new ListBoxItem
        {
            Focusable = false, IsHitTestVisible = false, Padding = new Thickness(2, lb.Items.Count == 0 ? 2 : 10, 2, 2),
            Content = new TextBlock { Inlines = { new System.Windows.Documents.Run(t) { FontWeight = FontWeights.Bold, Foreground = Brushes.Khaki },
                                                 new System.Windows.Documents.Run("  " + pod) { Foreground = Brushes.Gray, FontSize = 11.5 } } },
        });
        var poz = new List<(ListBoxItem It, Pozycja P)>();
        void Dodaj(string opis, Pozycja p) { var it = new ListBoxItem { Content = opis, Tag = p, Padding = new Thickness(14, 3, 4, 3) }; lb.Items.Add(it); poz.Add((it, p)); }
        Naglowek("SUK – układ torowy", "własny komputer zależności (UT, edytory UT i zobrazowania)");
        foreach (var f in uklady) Dodaj(OpisUkladu(f), new Pozycja(f, false));
        if (posterunki.Count > 0)
        {
            Naglowek("SimRail – pulpit zdalny", "stany i zależności z gry (mod SimRailMost) · " + katSr);
            foreach (var u in posterunki) Dodaj(OpisPosterunku(u), new Pozycja(u.Pulpit, true));
        }
        void Zaznacz(string plik) { var i = poz.FindIndex(x => string.Equals(x.P.Plik, plik, StringComparison.OrdinalIgnoreCase)); lb.SelectedItem = (i >= 0 ? poz[i] : poz[0]).It; }
        Zaznacz(last);
        sp.Children.Add(lb);
        // posterunek SimRail: krótki nasłuch moda SimRailMost i propozycja układu przypisanego do tego posterunku – tylko gdy jest
        // choć jeden posterunek SimRail albo układ z mapą SimRail (inaczej okno nie słucha gry)
        bool sluchac = posterunki.Count > 0 || uklady.Any(f => { try { return SimRailMapa.Wczytaj(SimRailMapa.PlikDla(f)).Posterunki.Count > 0; } catch { return false; } });
        // LCS_SIMRAIL_PORT – inny port stanów moda (jak w MelonPreferences.cfg, sekcja [SimRailMost]; także do testów)
        int portGry = int.TryParse(Environment.GetEnvironmentVariable("LCS_SIMRAIL_PORT"), out var pg) && pg is > 0 and < 65536 ? pg : SimRailOdbiornik.DomyslnyPort;
        var gra = new TextBlock { Foreground = Brushes.Gray, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0),
                                  Text = sluchac ? $"SimRail: nasłuch posterunku z gry (UDP {portGry})…" : "" };
        sp.Children.Add(gra);
        var stop = new System.Threading.CancellationTokenSource();
        bool wybranoRecznie = false;
        lb.PreviewMouseDown += (a, e) => wybranoRecznie = true;
        lb.PreviewKeyDown += (a, e) => wybranoRecznie = true;
        if (sluchac) w.Loaded += async (a, e) =>
        {
            var st = await SimRailMapa.WykryjPosterunek(portGry, TimeSpan.FromSeconds(30), stop.Token);
            if (stop.IsCancellationRequested) return;
            if (st == null) { gra.Text = $"SimRail: brak posterunku z gry – mod SimRailMost nie nadaje albo port {portGry} zajmuje inny program."; return; }
            // najpierw posterunki SimRail (rejestr), potem układy SUK z mapą SimRail
            var pasuje = posterunki.Where(u => u.Posterunki.Any(p => SimRailMapa.Klucz(p) == SimRailMapa.Klucz(st))).Select(u => u.Pulpit)
                                   .Concat(SimRailMapa.UkladyDlaPosterunku(uklady, st)).ToList();
            if (pasuje.Count == 0)
            {
                gra.Foreground = Brushes.Orange;
                gra.Text = $"SimRail: w grze posterunek „{st}” – nie ma go na liście. Nowy pulpit zdalny: posterunek.cmd nowy \"{st}\" " +
                           "(w paczce: stacje\\nowa_stacja.cmd); układ SUK z UT przypisuje się w oknie „SimRail” („Przypisz posterunek z gry”).";
                return;
            }
            gra.Foreground = Brushes.LightGreen;
            gra.Text = $"SimRail: w grze posterunek „{st}” → {string.Join(", ", pasuje.Select(Path.GetFileName))}" + (wybranoRecznie ? "" : " (zaznaczony)");
            if (!wybranoRecznie) Zaznacz(pasuje[0]);
        };
        w.Closed += (a, e) => stop.Cancel();
        Pozycja result = null;
        var bp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        var ok = new Button { Content = "Uruchom", Padding = new Thickness(16, 4, 16, 4), IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Zamknij", Padding = new Thickness(16, 4, 16, 4), IsCancel = true };
        ok.Click += (a, e) => { result = (lb.SelectedItem as ListBoxItem)?.Tag as Pozycja; if (result != null) w.Close(); };
        lb.MouseDoubleClick += (a, e) => { result = (lb.SelectedItem as ListBoxItem)?.Tag as Pozycja; if (result != null) w.Close(); };
        cancel.Click += (a, e) => w.Close();
        bp.Children.Add(ok); bp.Children.Add(cancel); sp.Children.Add(bp);
        w.Content = sp;
        w.ShowDialog();
        if (result == null) return null;
        try { File.WriteAllText(memo, result.Plik); } catch { }
        simRail = result.SimRail;
        return result.Plik;
    }
}

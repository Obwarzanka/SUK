using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Kilometraż ze słupków OSM (panel w narzędziu km): „Szukaj słupków km w widoku” – propozycje punktów km pogrupowane w osie
/// kilometracji (jedna oś na linię), projektant zaznacza punkty, które potwierdza, i dodaje je jednym krokiem (Ctrl+Z cofa).
/// </summary>
public partial class MainWindow
{
    public readonly StackPanel KmOsmPanel = new() { Margin = new Thickness(0, 0, 0, 6), Visibility = Visibility.Collapsed };
    /// <summary>Numer linii wpisany dla słupków bez numeru w OSM.</summary>
    string kmOsmLinia = "";
    string kmOsmInfo = "";
    bool kmOsmBusy;

    void InitKmOsm()
    {
        View.Overlay = View.DrawKmOsm;
        View.ToolChanged += RefreshKmOsmPanel;
    }

    /// <summary>Szukanie słupków w widoku i propozycje (także z testu).</summary>
    public async Task KmOsmSzukaj()
    {
        if (kmOsmBusy) return;
        kmOsmBusy = true; RefreshKmOsmPanel();
        try
        {
            var sl = await View.KmOsmPobierz();
            if (sl == null) { kmOsmInfo = "Nie udało się pobrać słupków – opis w pasku stanu."; return; }
            View.KmOsm = KilometrazOsm.Propozycje(Doc.P, sl);
            View.KmOsmWybrane.Clear();
            int szac = sl.Count(x => x.Szacowany);
            kmOsmInfo = $"W OSM: {sl.Count} słupków km w widoku i do 1,5 km dookoła" + (szac > 0 ? $", w tym {szac} z położeniem szacowanym (source=interpolation)" : "")
                + $". Przy torach projektu: {View.KmOsm.Count}" + (View.KmOsm.Count == 0 && sl.Count > 0 ? " (słupki dalej niż 25 m od osi torów albo ich km już jest w projekcie)" : "") + ".";
            Message($"Słupki km z OSM: {View.KmOsm.Count} propozycji – zaznacz te, które potwierdzasz, i dodaj ({OsmPunkty.Attribution.Replace("Punkty", "Dane")})");
        }
        finally { kmOsmBusy = false; RefreshKmOsmPanel(); View.InvalidateVisual(); }
    }

    /// <summary>Dodanie zaznaczonych propozycji jako punktów km (jeden krok cofania).</summary>
    public void KmOsmDodaj()
    {
        var sel = View.KmOsm.Where(x => View.KmOsmWybrane.Contains(x.Slupek.Id)).ToList();
        if (sel.Count == 0) { Message("Słupki km: zaznacz punkty do dodania"); return; }
        var pts = sel.Select(x => KilometrazOsm.Punkt(x, x.Slupek.Line.Length > 0 ? null : kmOsmLinia.Trim())).ToList();
        Doc.Change(sel.Count == 1 ? "Punkt kilometrażu z OSM" : $"Kilometraż z OSM ({sel.Count} punktów)", p => p.KmPoints.AddRange(pts));
        View.KmOsm = View.KmOsm.Except(sel).ToList();
        foreach (var x in sel) View.KmOsmWybrane.Remove(x.Slupek.Id);
        Message($"Kilometraż z OSM: dodano {sel.Count} punktów km" + string.Concat(pts.GroupBy(x => x.Line).Select(g => $", linia {(g.Key.Length > 0 ? g.Key : "bez numeru")}: {g.Count()}")) + " – sprawdź „Sprawdź” i km w panelu toru (Ctrl+Z cofa)");
        RefreshKmOsmPanel(); View.InvalidateVisual();
    }

    public void RefreshKmOsmPanel()
    {
        var P = KmOsmPanel; P.Children.Clear();
        bool on = View.Tool == UtTool.Km || View.KmOsm.Count > 0;
        P.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (!on) return;
        P.Children.Add(Header("Kilometraż ze słupków OSM"));
        P.Children.Add(Note("Słupki kilometrowe z OpenStreetMap jako propozycje punktów km na osi toru. Słupki jednej linii tworzą jedną oś kilometracji (tor, przy którym stoi ich najwięcej), inna linia – osobna oś. Do projektu trafiają tylko punkty, które zaznaczysz."));
        P.Children.Add(SmallBtn(kmOsmBusy ? "Szukam…" : "Szukaj słupków km w widoku", () => _ = KmOsmSzukaj(), "Overpass API: słupki railway=milestone w widoku i do 1,5 km dookoła; numer linii z torów OSM (ref)"));
        if (kmOsmInfo.Length > 0) P.Children.Add(Note(kmOsmInfo));
        foreach (var os in View.KmOsm.GroupBy(x => x.Slupek.Line).OrderBy(g => g.Key.Length == 0))
        {
            var l = os.ToList();
            string tory = string.Join(", ", l.Select(x => TrNr(x.Track)).Distinct());
            var all = new CheckBox { Content = $"Oś linii {(os.Key.Length > 0 ? os.Key : "bez numeru")} – tor {tory} ({l.Count})", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 2),
                IsChecked = l.All(x => View.KmOsmWybrane.Contains(x.Slupek.Id)), ToolTip = "Zaznacz albo odznacz wszystkie punkty tej osi" };
            all.Click += (s, e) => { foreach (var x in l) if (all.IsChecked == true) View.KmOsmWybrane.Add(x.Slupek.Id); else View.KmOsmWybrane.Remove(x.Slupek.Id); RefreshKmOsmPanel(); View.InvalidateVisual(); };
            P.Children.Add(all);
            if (os.Key.Length == 0)
            {
                var tb = new TextBox { Text = kmOsmLinia, Width = 70, Margin = new Thickness(18, 0, 0, 2), HorizontalAlignment = HorizontalAlignment.Left, ToolTip = "Numer linii dla słupków bez numeru w OSM" };
                tb.LostFocus += (s, e) => kmOsmLinia = tb.Text;
                P.Children.Add(new TextBlock { Text = "numer linii:", Margin = new Thickness(18, 0, 0, 0), FontSize = 12 });
                P.Children.Add(tb);
            }
            foreach (var x in l)
            {
                var row = new DockPanel { Margin = new Thickness(18, 0, 0, 1) };
                var go = SmallBtn("↗", () => { if (View.KmOsmAt(x) is XY w) View.SetView(w, Math.Min(View.Mpp, 0.3)); }, "Pokaż na mapie");
                go.Padding = new Thickness(4, 0, 4, 0); DockPanel.SetDock(go, System.Windows.Controls.Dock.Right); row.Children.Add(go);
                string uw = x.Uwaga.Length > 0 ? "\n" + x.Uwaga : "";
                var cb = new CheckBox { Content = new TextBlock { Text = $"km {Kilometraz.Fmt(x.Slupek.Km)} – tor {TrNr(x.Track)}, {F(x.Dist, "0.0")} m od osi{uw}", TextWrapping = TextWrapping.Wrap, FontSize = 12 },
                    IsChecked = View.KmOsmWybrane.Contains(x.Slupek.Id), Foreground = x.Uwaga.Length > 0 ? Brushes.SaddleBrown : Brushes.Black };
                cb.Click += (s, e) => { if (cb.IsChecked == true) View.KmOsmWybrane.Add(x.Slupek.Id); else View.KmOsmWybrane.Remove(x.Slupek.Id); RefreshKmOsmPanel(); View.InvalidateVisual(); };
                row.Children.Add(cb);
                P.Children.Add(row);
            }
        }
        if (View.KmOsm.Count > 0)
        {
            var act = new WrapPanel { Margin = new Thickness(0, 6, 0, 4) };
            act.Children.Add(SmallBtn($"Dodaj zaznaczone ({View.KmOsm.Count(x => View.KmOsmWybrane.Contains(x.Slupek.Id))})", KmOsmDodaj, "Zaznaczone propozycje jako punkty km (Ctrl+Z cofa)"));
            act.Children.Add(SmallBtn("Wyczyść propozycje", () => { View.KmOsm.Clear(); View.KmOsmWybrane.Clear(); kmOsmInfo = ""; RefreshKmOsmPanel(); View.InvalidateVisual(); }, "Usuń listę propozycji (projekt bez zmian)"));
            P.Children.Add(act);
            P.Children.Add(Note("Słupek w OSM stoi tam, gdzie wstawił go autor mapy – przy „położenie szacowane” sprawdź km na zdjęciu albo planie. Dane © współtwórcy OpenStreetMap (ODbL)."));
        }
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Podpowiedzi z OSM (panel w narzędziach Tor, Rozjazd i Urządzenie): „Szukaj w widoku” – propozycje miejsc sygnalizatorów
/// (i wykolejnic, kozłów, W4 / W5), numerów zwrotnic i numerów torów przy torach projektu; projektant zaznacza, co potwierdza,
/// i przyjmuje jednym krokiem (Ctrl+Z cofa). Decyzja właściciela 2026-10-27.
/// </summary>
public partial class MainWindow
{
    public readonly StackPanel UrzOsmPanel = new() { Margin = new Thickness(0, 0, 0, 6), Visibility = Visibility.Collapsed };
    string urzOsmInfo = "";
    bool urzOsmBusy;

    void InitUrzOsm()
    {
        var prev = View.Overlay;
        View.Overlay = dc => { prev?.Invoke(dc); View.DrawUrzOsm(dc); };
        View.ToolChanged += RefreshUrzOsmPanel;
    }

    /// <summary>Szukanie w widoku i propozycje (także z testu).</summary>
    public async Task UrzOsmSzukaj()
    {
        if (urzOsmBusy) return;
        urzOsmBusy = true; RefreshUrzOsmPanel();
        try
        {
            var d = await View.UrzOsmPobierz();
            if (d == null) { urzOsmInfo = "Nie udało się pobrać danych – opis w pasku stanu."; return; }
            View.UrzOsm = UrzadzeniaOsm.Urzadzenia(Doc.P, d);
            View.NrOsm = UrzadzeniaOsm.NumeryZwrotnic(Doc.P, d).Concat(UrzadzeniaOsm.NumeryTorow(Doc.P, d)).ToList();
            View.OsmWybrane.Clear();
            int sig = d.Obiekty.Count(o => o.Railway == "signal"), zw = d.Obiekty.Count(o => o.Railway == "switch");
            urzOsmInfo = $"W OSM w widoku i do 200 m dookoła: {sig} sygnalizatorów ({d.Obiekty.Count(o => o.Railway == "signal" && o.Tag("ref").Length > 0)} z nazwą), "
                + $"{zw} zwrotnic ({d.Obiekty.Count(o => o.Railway == "switch" && o.Tag("ref").Length > 0)} z numerem), {d.Tory.Count} odcinków torów z numerem. "
                + $"Propozycje przy torach projektu: urządzenia {View.UrzOsm.Count}, numery zwrotnic {View.NrOsm.Count(x => x.Rozjazd)}, numery torów {View.NrOsm.Count(x => !x.Rozjazd)}.";
            Message($"Podpowiedzi z OSM: {View.UrzOsm.Count + View.NrOsm.Count} propozycji – zaznacz te, które potwierdzasz, i przyjmij ({OsmPunkty.Attribution.Replace("Punkty", "Dane")})");
        }
        finally { urzOsmBusy = false; RefreshUrzOsmPanel(); View.InvalidateVisual(); }
    }

    /// <summary>Przyjęcie zaznaczonych propozycji: nowe urządzenia, numery zwrotnic i torów (jeden krok cofania).</summary>
    public void UrzOsmPrzyjmij()
    {
        var su = View.UrzOsm.Where(x => View.OsmWybrane.Contains(x.Klucz)).ToList();
        var sn = View.NrOsm.Where(x => View.OsmWybrane.Contains(x.Klucz)).ToList();
        if (su.Count + sn.Count == 0) { Message("Podpowiedzi z OSM: zaznacz propozycje do przyjęcia"); return; }
        Doc.Change(su.Count + sn.Count == 1 ? "Podpowiedź z OSM" : $"Podpowiedzi z OSM ({su.Count + sn.Count})", p =>
        {
            // najpierw nazwy z OSM, potem kolejne wolne nazwy dla urządzeń bez nazwy
            foreach (var x in su.OrderBy(x => x.Name.Length == 0)) p.Devices.Add(UrzadzeniaOsm.Urzadzenie(p, x));
            foreach (var x in sn)
                if (x.Rozjazd) { if (p.Turnouts.FirstOrDefault(t => t.Id == x.Id) is UtTurnout t) t.Number = x.Osm; }
                else if (p.Tracks.FirstOrDefault(t => t.Id == x.Id) is UtTrack t) t.Number = x.Osm;
        });
        View.UrzOsm = View.UrzOsm.Except(su).ToList(); View.NrOsm = View.NrOsm.Except(sn).ToList();
        foreach (var k in su.Select(x => x.Klucz).Concat(sn.Select(x => x.Klucz))) View.OsmWybrane.Remove(k);
        Message($"Podpowiedzi z OSM: dodano urządzeń {su.Count}, numery zwrotnic {sn.Count(x => x.Rozjazd)}, numery torów {sn.Count(x => !x.Rozjazd)} – sprawdź miejsca na zdjęciu i „Sprawdź” (Ctrl+Z cofa)");
        RefreshUrzOsmPanel(); View.InvalidateVisual();
    }

    public void RefreshUrzOsmPanel()
    {
        var P = UrzOsmPanel; P.Children.Clear();
        bool on = View.Tool is UtTool.Tor or UtTool.Rozjazd or UtTool.Urzadzenie || View.UrzOsm.Count + View.NrOsm.Count > 0;
        P.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (!on) return;
        P.Children.Add(Header("Podpowiedzi z OSM"));
        P.Children.Add(Note("Miejsca sygnalizatorów, wykolejnic i kozłów oraz numery zwrotnic i torów z OpenStreetMap – tylko przy torach już narysowanych. Do projektu trafia tylko to, co zaznaczysz."));
        P.Children.Add(SmallBtn(urzOsmBusy ? "Szukam…" : "Szukaj w widoku", () => _ = UrzOsmSzukaj(), "Overpass API: railway=signal, switch, derail, buffer_stop i tory z railway:track_ref w widoku i do 200 m dookoła"));
        if (urzOsmInfo.Length > 0) P.Children.Add(Note(urzOsmInfo));
        void Grupa(string tytul, List<(string Klucz, string Tekst, string Uwaga, object X)> l)
        {
            if (l.Count == 0) return;
            var all = new CheckBox { Content = $"{tytul} ({l.Count})", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 2),
                IsChecked = l.All(x => View.OsmWybrane.Contains(x.Klucz)), ToolTip = "Zaznacz albo odznacz wszystkie" };
            all.Click += (s, e) => { foreach (var x in l) if (all.IsChecked == true) View.OsmWybrane.Add(x.Klucz); else View.OsmWybrane.Remove(x.Klucz); RefreshUrzOsmPanel(); View.InvalidateVisual(); };
            P.Children.Add(all);
            foreach (var x in l)
            {
                var row = new DockPanel { Margin = new Thickness(18, 0, 0, 1) };
                var go = SmallBtn("↗", () => { if (View.OsmAt(x.X) is XY w) View.SetView(w, Math.Min(View.Mpp, 0.3)); }, "Pokaż na mapie");
                go.Padding = new Thickness(4, 0, 4, 0); DockPanel.SetDock(go, System.Windows.Controls.Dock.Right); row.Children.Add(go);
                var cb = new CheckBox { Content = new TextBlock { Text = x.Tekst + (x.Uwaga.Length > 0 ? "\n" + x.Uwaga : ""), TextWrapping = TextWrapping.Wrap, FontSize = 12 },
                    IsChecked = View.OsmWybrane.Contains(x.Klucz), Foreground = x.Uwaga.Length > 0 ? Brushes.SaddleBrown : Brushes.Black };
                cb.Click += (s, e) => { if (cb.IsChecked == true) View.OsmWybrane.Add(x.Klucz); else View.OsmWybrane.Remove(x.Klucz); RefreshUrzOsmPanel(); View.InvalidateVisual(); };
                row.Children.Add(cb);
                P.Children.Add(row);
            }
        }
        string Kier(PropozycjaUrz x) => UtDevice.KindInfo(x.Kind).Directed && x.Kind != UtDevice.Koziol ? (x.Dir > 0 ? ", w stronę rosnącego km" : ", w stronę malejącego km") : "";
        Grupa("Sygnalizatory i urządzenia", View.UrzOsm.Select(x => (x.Klucz, $"{x.Opis}{(x.Name.Length > 0 ? " " + x.Name : "")} – tor {TrNr(x.Track)}, {F(x.S, "0")} m{Kier(x)}, {F(x.Dist, "0.0")} m od osi", x.Uwaga, (object)x)).ToList());
        Grupa("Numery zwrotnic", View.NrOsm.Where(x => x.Rozjazd).Select(x => (x.Klucz, $"zwrotnica {x.Osm} – {F(x.Dist, "0.0")} m od PR rozjazdu{(x.Obecny.Length == 0 ? " bez numeru" : "")}", x.Uwaga, (object)x)).ToList());
        Grupa("Numery torów", View.NrOsm.Where(x => !x.Rozjazd).Select(x => (x.Klucz, $"tor {x.Osm} – dziś {(x.Obecny.Length > 0 ? x.Obecny : "bez numeru")}, średnio {F(x.Dist, "0.0")} m od osi", x.Uwaga, (object)x)).ToList());
        if (View.UrzOsm.Count + View.NrOsm.Count > 0)
        {
            var act = new WrapPanel { Margin = new Thickness(0, 6, 0, 4) };
            int n = View.UrzOsm.Count(x => View.OsmWybrane.Contains(x.Klucz)) + View.NrOsm.Count(x => View.OsmWybrane.Contains(x.Klucz));
            act.Children.Add(SmallBtn($"Przyjmij zaznaczone ({n})", UrzOsmPrzyjmij, "Zaznaczone urządzenia i numery do projektu (Ctrl+Z cofa)"));
            act.Children.Add(SmallBtn("Wyczyść propozycje", () => { View.UrzOsm.Clear(); View.NrOsm.Clear(); View.OsmWybrane.Clear(); urzOsmInfo = ""; RefreshUrzOsmPanel(); View.InvalidateVisual(); }, "Usuń listę propozycji (projekt bez zmian)"));
            P.Children.Add(act);
            P.Children.Add(Note("Obiekt w OSM stoi tam, gdzie wstawił go autor mapy – miejsce sygnalizatora sprawdź na zdjęciu, funkcję semafora i komory ustaw w panelu. Dane © współtwórcy OpenStreetMap (ODbL)."));
        }
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Ślad osi toru w oknie (PLAN_EDYTOR_UT.md § 5h): panel „Ślad” (długość, rozrzut punktów, baza krzywizny, łuki rozpoznane z krzywizny,
/// „Tor po śladzie” – propozycja i utworzenie toru), sieć OSM jako linie (pole w panelu „Podkłady”), krzywizna śladu na wykresie.
/// </summary>
public partial class MainWindow
{
    public readonly StackPanel SladPanel = new() { Margin = new Thickness(0, 0, 0, 6), Visibility = Visibility.Collapsed };
    public readonly CheckBox OsmBox = new() { Content = "sieć torów OSM (linie) – do śladu i łuków", Margin = new Thickness(0, 0, 0, 4),
        ToolTip = "Osie torów z OpenStreetMap jako linie (pomarańczowe) – klik przy linii w narzędziu Ślad biegnie po niej; w Łuku z punktów Shift + klik dwa razy – węzły z odcinka. W narzędziach Ślad i Łuk z punktów widać je zawsze" };
    public readonly Button OsmBtn = new() { Content = "Pobierz sieć OSM z widoku", Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 0, 0, 8), HorizontalAlignment = HorizontalAlignment.Left,
        ToolTip = "Overpass API: drogi kolejowe w widoku (do ok. 5 km); kolejne obszary dołączają się" };
    public readonly TextBox SladBazaBox = new() { Width = 50, Text = "", VerticalContentAlignment = VerticalAlignment.Center,
        ToolTip = "Długość okna, w którym liczy się kierunek śladu [m]; puste – dobrana do rozrzutu punktów (OSM ok. 60–100 m, zdjęcie 15–25 m). Mniejsza – widać krótsze łuki i przejściowe, większa – mniej szumu" };

    void InitSlad()
    {
        OsmBox.Click += (s, e) => { View.OsmShown = OsmBox.IsChecked == true; if (View.OsmShown && View.Osm == null && !Test.Running) _ = View.OsmPobierz(); View.InvalidateVisual(); };
        OsmBtn.Click += async (s, e) => { await View.OsmPobierz(); View.InvalidateVisual(); };
        View.SladChanged += () => { RefreshSladPanel(); UpdateKrzyw(); };
        View.ToolChanged += () =>
        {
            RefreshSladPanel();
            if (View.Tool == UtTool.Slad && !Test.Running && !View.OsmCovers())
                _ = View.OsmPobierz();
        };
        View.SladAccepted += (p, wielobok) =>
        {
            var a = p.A;
            var t = new UtTrack { Align = a, Vertices = new() { new UtVertex { X = a.X, Y = a.Y }, new UtVertex { X = a.X, Y = a.Y } } };
            TrasaElem.SyncEnds(t);
            string conv = "";
            if (wielobok)
            {
                // wielobok stycznych (wierzchołki z R, L) – działają wszystkie narzędzia toru: przedłużanie, łączenie, wierzchołki, rozjazdy
                var (v, max) = TrasaElem.ToPolygon(a, Trasa.KFor(t));
                t.Align = null; t.Vertices = v; conv = $", wielobok {v.Count} wierzchołków (od trasy z elementów najwyżej {max * 100:0.#} cm)";
            }
            Doc.Change("Tor po śladzie", d => d.Tracks.Add(t));
            View.SladClear();
            // od razu Wskaż – w Śladzie kliknięcie dokłada punkty śladu, więc nowego toru nie dało się zaznaczyć ani edytować
            SetTool(UtTool.Wskaz);
            View.Select(t.Id);
            var r = View.Geometry(t);
            Message($"Tor po śladzie: {a.Els.Count} elementów{conv}, długość {UtView.Len3(r.Length)}, odchyłka od śladu średnio {p.Rms * 100:0.#} cm – to propozycja: sprawdź na zdjęciu, popraw „Łukiem z punktów” albo na wykresie krzywizny; nadaj numer (Ctrl+Z cofa)");
        };
        SladBazaBox.LostFocus += (s, e) => CommitBaza();
        SladBazaBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) { CommitBaza(); e.Handled = true; } };
        void CommitBaza()
        {
            double b = Num(SladBazaBox.Text) is double x && x >= 6 ? x : 0;
            if (Math.Abs(b - View.SladBaza) < 1e-9) return;
            View.SladBaza = b; RefreshSladPanel(); UpdateKrzyw();
        }
    }

    /// <summary>Panel „Ślad” (widoczny w narzędziu Ślad albo gdy ślad jest).</summary>
    public void RefreshSladPanel()
    {
        SladPanel.Children.Clear();
        bool on = View.Tool == UtTool.Slad || View.SladPts.Count > 0;
        SladPanel.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (!on) return;
        SladPanel.Children.Add(Header("Ślad"));
        if (View.SladPts.Count == 0)
        {
            SladPanel.Children.Add(Note("Klik przy pomarańczowej linii OSM – ślad biegnie po liniach (przez rozjazdy); klik obok albo z Ctrl – punkt ze zdjęcia; Ctrl + przeciągnięcie – malowanie po osi toru; samo przeciągnięcie przesuwa mapę. Na wykresie pod mapą krzywizna śladu: poziomo – łuk (wysokość = R), pochyło – krzywa przejściowa (szerokość = L), zero – prosta."));
            if (View.Osm == null) SladPanel.Children.Add(SmallBtn("Pobierz sieć OSM z widoku", async () => { await View.OsmPobierz(); RefreshSladPanel(); }, OsmBtn.ToolTip as string));
            return;
        }
        double k = Geo.ScaleFactor(View.SladPts[0]);
        var (sig, step) = Slad.Rozrzut(View.SladPts, k);
        SladPanel.Children.Add(Note($"{View.SladPts.Count} punktów, długość {UtView.Len3(View.SladLen)}; rozrzut w poprzek ok. {sig * 100:0.#} cm, punkty co ok. {step:0.#} m"));
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 4) };
        row.Children.Add(new TextBlock { Text = "baza krzywizny [m] ", VerticalAlignment = VerticalAlignment.Center });
        if (!SladBazaBox.IsKeyboardFocused) SladBazaBox.Text = View.SladBaza > 0 ? F(View.SladBaza) : "";
        if (SladBazaBox.Parent is Panel old) old.Children.Remove(SladBazaBox);
        row.Children.Add(SladBazaBox);
        row.Children.Add(new TextBlock { Text = View.SladBaza > 0 ? "" : $"  (dobrana: {F(View.SladBazaUzyta)} m)", VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.DimGray });
        SladPanel.Children.Add(row);
        var (_, luki) = View.SladKrzywizna();
        SladPanel.Children.Add(Label(luki.Count == 0 ? "Łuki ze śladu: brak (prosta albo ślad za krótki)" : $"Łuki ze śladu (z wykresu, przybliżone): {luki.Count}"));
        int i = 0;
        foreach (var l in luki) SladPanel.Children.Add(Note($"{++i}. " + Slad.Opis(l)));
        var act = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
        act.Children.Add(SmallBtn(View.SladBusy ? "Liczę…" : "Tor po śladzie", () => _ = View.SladLicz(), "Propozycja toru z elementów (prosta, krzywa przejściowa, łuk) dopasowana do śladu – R i L dokładniej niż z wykresu; punkty odstające pomijane (Enter)"));
        if (View.SladPreview != null)
        {
            act.Children.Add(SmallBtn("Utwórz tor", () => View.SladAccept(true), "Nowy tor z propozycji jako wielobok stycznych (R i L w wierzchołkach) – działają wszystkie narzędzia toru; Ctrl+Z cofa (Enter)"));
            act.Children.Add(SmallBtn("Utwórz jako trasę z elementów", () => View.SladAccept(false), "Nowy tor z propozycji jako ciąg elementów (różne przejściowe, łuk koszowy) – edycja na wykresie krzywizny; Ctrl+Z cofa"));
        }
        act.Children.Add(SmallBtn("Wyczyść ślad", () => { View.SladClear(); Message("Ślad wyczyszczony"); }, "Usuń ślad (Esc)"));
        SladPanel.Children.Add(act);
        if (View.SladPreview != null) SladPanel.Children.Add(Note(View.SladPreviewInfo()));
        SladPanel.Children.Add(Note("Ślad i propozycja nie zapisują się w projekcie. Tor po śladzie to propozycja – sprawdź ją na zdjęciu."));
    }

    static TextBlock Note(string s) => new() { Text = s, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, FontSize = 12, Margin = new Thickness(0, 1, 0, 3) };

    /// <summary>Krzywizna śladu na wykresie: na km zaznaczonego toru (punkty śladu do 15 m od niego) albo na km śladu.</summary>
    void UpdateKrzywSlad(UtTrack t)
    {
        if (View.SladPts.Count < 3) { Krzyw.ShowSlad(null, null, 0); return; }
        var (kr, luki) = View.SladKrzywizna();
        var r = t != null ? View.Geometry(t) : null;
        double? OnTrack(XY p) { if (r == null || r.Elements.Count == 0) return null; var (s, d) = r.Nearest(p, 2); return d / r.K <= 15 ? s : null; }
        XY PAt(double s) => kr.Count == 0 ? View.SladPts[0] : kr[Math.Clamp((int)Math.Round(s - kr[0].S), 0, kr.Count - 1)].P;
        var pts = new List<(double S, double K)>(); var labs = new List<(double, double, string, double)>();
        string Lab(SladLuk l) => $"≈ R {Math.Abs(l.R):0}" + (l.L1 > 0 || l.L2 > 0 ? $", L {l.L1:0}/{l.L2:0}" : "") + $", {Math.Abs(l.Kat) * 180 / Math.PI:0.0}°";
        if (r != null && kr.Count(x => OnTrack(x.P) != null) > kr.Count / 2)
        {
            foreach (var x in kr) if (OnTrack(x.P) is double s) pts.Add((s, x.K));
            foreach (var l in luki)
                if (OnTrack(PAt(l.S0)) is double a && OnTrack(PAt(l.S1)) is double b) labs.Add((Math.Min(a, b), Math.Max(a, b), Lab(l), 1 / l.R));
            Krzyw.ShowSlad(pts, labs, View.SladLen);
            return;
        }
        if (t != null) Krzyw.Show(null, null);           // ślad nie biegnie wzdłuż zaznaczonego toru – wykres śladu
        foreach (var x in kr) pts.Add((x.S, x.K));
        foreach (var l in luki) labs.Add((l.S0, l.S1, Lab(l), 1 / l.R));
        Krzyw.ShowSlad(pts, labs, View.SladLen);
    }
}

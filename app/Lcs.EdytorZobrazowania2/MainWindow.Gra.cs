using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorZobrazowania2;

/// <summary>
/// Zakładka „Gra” (PLAN_POSTERUNKI_SIMRAIL.md, P3) – tylko dla zobrazowań z pulpitu SimRail (obok jest mapa <c>&lt;układ&gt;.simrail.json</c>).
/// Przypięcie elementów i odcinków rysunku do obiektów gry widać i poprawia się w edytorze: stan z konwertera (<see cref="ObrStanGry"/>,
/// „Przelicz” – <c>zob_do_suk.py --tylko-przypiecia</c>, jedno źródło reguł), wybór ręczny ID gry albo „bez obiektu gry”, lista do przypięcia,
/// obiekty gry nieumieszczone na rysunku, na płótnie – znaczniki pozycji bez obiektu gry. Przypięcie zapisuje się od razu w mapie
/// (osobny plik, nie w zobrazowaniu; poprzednia wersja w .bak) – cofnij / ponów zobrazowania go nie dotyczy.
/// </summary>
public partial class MainWindow
{
    public readonly TabItem TabGra = new() { Header = "Gra" };
    public readonly StackPanel GraPanel = new() { Margin = new Thickness(10) };
    /// <summary>Pole ID gry (edytowalne, z kandydatami) i przyciski – dla testu.</summary>
    public readonly ComboBox GraIdBox = new() { IsEditable = true, Margin = new Thickness(0, 2, 0, 6) };
    public ObrMapaGry MapaGry { get; private set; }
    public ObrStanGry StanGry { get; private set; }
    bool graZbudowana;
    /// <summary>Tło pozycji listy „Do przypięcia” (test).</summary>
    public static Brush GraListaTlo => WyjBg;

    void InitGra()
    {
        TabGra.Content = new ScrollViewer { Content = GraPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        View.SelectionChanged += () => { if (Side.SelectedItem == TabGra) RefreshGra(); };
    }

    /// <summary>Po otwarciu zobrazowania: mapa gry i stan przypięć obok pliku; zakładka Gra tylko przy mapie.</summary>
    void WczytajGre()
    {
        MapaGry = null; StanGry = null;
        try { MapaGry = ObrMapaGry.Wczytaj(Doc?.Path); StanGry = MapaGry == null ? null : ObrStanGry.Wczytaj(Doc.Path); }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or InvalidOperationException) { Message("Mapa gry: " + ex.Message); }
        if (MapaGry != null && !Side.Items.Contains(TabGra)) Side.Items.Insert(Side.Items.IndexOf(TabUt) + 1, TabGra);
        if (MapaGry == null && Side.Items.Contains(TabGra)) { if (Side.SelectedItem == TabGra) Side.SelectedItem = TabObraz; Side.Items.Remove(TabGra); }
        ZnacznikiGry();
    }

    /// <summary>Wybrane do przypięcia: jeden element z obiektem gry (zwrotnica także po kliknięciu rozjazdu) albo odcinki toru.</summary>
    (string Rodzaj, List<string> Ids) WybraneGra()
    {
        var o = Obraz; if (o == null) return (null, new());
        if (View.SelEl.Count == 1 && o.Elementy.FirstOrDefault(x => x.Id == View.SelEl.First()) is { } el && ObrMapaGry.RodzajElementu(el.Typ) != null)
            return (ObrMapaGry.Elementy, new() { el.Id });
        if (View.SelEl.Count == 0 && View.SelE.Count == 0 && View.SelW.Count == 1 && ObrSymbole.ZwrotnicaW(o, View.SelW.First()) is { } z)
            return (ObrMapaGry.Elementy, new() { z.Id });
        if (View.SelE.Count > 0) return (ObrMapaGry.Odcinki, View.SelE.ToList());
        return (null, new());
    }

    string OpisGra(string rodzaj, string id) =>
        rodzaj == ObrMapaGry.Odcinki ? $"odcinek {id}" : ObrSprawdz.Opis(Obraz, id);

    void RefreshGra()
    {
        GraPanel.Children.Clear();
        graZbudowana = true;
        var o = Obraz;
        if (MapaGry == null || o == null) { GraPanel.Children.Add(Note("Zobrazowanie bez mapy obiektów gry (SimRail).")); return; }
        GraPanel.Children.Add(Header("Gra (SimRail)"));
        GraPanel.Children.Add(Note($"Posterunek gry: {MapaGry.Posterunek} ({MapaGry.Kod}). Mapa: {Path.GetFileName(MapaGry.Plik)} – przypięcie zapisuje się od razu."));
        var bp = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        bp.Children.Add(Btn("Przelicz (F5)", (s, e) => PrzeliczGre(), "Stan przypięć z konwertera (zob_do_suk.py --tylko-przypiecia): po numerze, położeniu, sąsiadach. Zobrazowanie zapisuje się najpierw."));
        GraPanel.Children.Add(bp);
        if (StanGry == null) GraPanel.Children.Add(Note("Brak stanu przypięć – „Przelicz” (albo posterunek.cmd przypiecia <układ>)."));
        else
        {
            var st = StanGry.Obrazu(o.Id).ToList();
            int ile(Func<ObrStanGry.Poz, bool> f) => st.Count(x => f(x.P));
            GraPanel.Children.Add(Note($"Stan z {StanGry.Utworzono:yyyy-MM-dd HH:mm}: z mapy {ile(p => p.Stan == ObrStanGry.Mapa)}, wyliczone {ile(p => p.Stan is ObrStanGry.Numer or ObrStanGry.Polozenie or ObrStanGry.Sasiedzi)}, " +
                                       $"bez obiektu gry {ile(p => p.Stan == ObrStanGry.BezObiektu)}, do przypięcia {ile(ObrStanGry.DoPrzypiecia)}."));
        }

        // --- wybrane na płótnie
        var (rodzaj, ids) = WybraneGra();
        GraPanel.Children.Add(Label(ids.Count == 0 ? "Zaznacz na płótnie element (semafor, tarcza, zwrotnica…) albo tor." :
                                    ids.Count == 1 ? OpisGra(rodzaj, ids[0]) : $"Zaznaczone odcinki toru: {ids.Count}"));
        if (ids.Count > 0)
        {
            foreach (var id in ids.Take(6))
            {
                var g = MapaGry.Gra(o.Id, rodzaj, id); var p = StanGry?.Stan(o.Id, rodzaj, id);
                var tekst = g == ObrMapaGry.Bez ? "bez obiektu gry (świadomie)" : g != null ? $"{g} – przypięte w mapie"
                          : p?.Gra != null ? $"{p.Gra} – {ObrStanGry.Opis(p.Stan)}" : p != null ? ObrStanGry.Opis(p.Stan) : "brak w stanie przypięć (Przelicz)";
                GraPanel.Children.Add(new TextBlock { Text = (ids.Count > 1 ? $"{id}: " : "") + tekst, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2),
                                                      Foreground = g == null && (p == null || ObrStanGry.DoPrzypiecia(p)) ? BladFg : Brushes.Black });
            }
            if (ids.Count > 6) GraPanel.Children.Add(Note($"… i {ids.Count - 6} dalszych"));
            // kandydaci: z konwertera (sąsiedzi, numery), potem wszystkie znane ID tego rodzaju
            var el = rodzaj == ObrMapaGry.Elementy ? o.Elementy.FirstOrDefault(x => x.Id == ids[0]) : null;
            var kand = ids.SelectMany(id => StanGry?.Stan(o.Id, rodzaj, id)?.Kandydaci ?? Array.Empty<string>()).Distinct().ToList();
            var znane = MapaGry.Znane(rodzaj, el == null ? null : ObrMapaGry.RodzajElementu(el.Typ));
            GraIdBox.Items.Clear();
            foreach (var k in kand.Concat(znane.Except(kand))) GraIdBox.Items.Add(k);
            var biez = ids.Select(id => MapaGry.Gra(o.Id, rodzaj, id) ?? StanGry?.Stan(o.Id, rodzaj, id)?.Gra).Distinct().ToList();
            GraIdBox.Text = biez.Count == 1 && biez[0] != ObrMapaGry.Bez ? biez[0] ?? kand.FirstOrDefault() ?? "" : kand.FirstOrDefault() ?? "";
            GraIdBox.ToolTip = "ID obiektu gry (railNetworkName); na liście – najpierw kandydaci z konwertera, potem wszystkie znane z mapy";
            if (GraIdBox.Parent is Panel pp) pp.Children.Remove(GraIdBox);
            GraPanel.Children.Add(GraIdBox);
            var row = new WrapPanel();
            row.Children.Add(Btn("Przypnij", (s, e) => PrzypnijGra(GraIdBox.Text), "Zapisz w mapie: te elementy / odcinki = ten obiekt gry"));
            row.Children.Add(new Border { Width = 4 });
            row.Children.Add(Btn("Bez obiektu gry", (s, e) => PrzypnijGra(ObrMapaGry.Bez), "Świadomie bez obiektu gry (np. tor dorysowany tylko dla wyglądu) – bez wpisu w nieprzypisane.txt"));
            row.Children.Add(new Border { Width = 4 });
            row.Children.Add(Btn("Usuń", (s, e) => PrzypnijGra(null), "Usuń przypięcie z mapy – konwerter znów zaproponuje obiekt (po numerze, położeniu, sąsiadach)"));
            GraPanel.Children.Add(row);
        }

        // --- do przypięcia
        if (StanGry != null)
        {
            var dp = StanGry.Obrazu(o.Id).Where(x => ObrStanGry.DoPrzypiecia(x.P) && MapaGry.Gra(o.Id, x.Rodzaj, x.Id) == null
                                                   && (x.Rodzaj == ObrMapaGry.Odcinki ? ObrGraf.Odcinek(o, x.Id) != null : o.Elementy.Any(e => e.Id == x.Id))).ToList();
            GraPanel.Children.Add(new TextBlock { Text = $"Do przypięcia ({dp.Count})", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 4) });
            if (dp.Count == 0) GraPanel.Children.Add(Note("Wszystko przypięte albo świadomie bez obiektu gry."));
            foreach (var x in dp.OrderBy(x => x.Rodzaj).ThenBy(x => x.Id))
            {
                var (r, id) = (x.Rodzaj, x.Id);
                var b = new Button { Content = new TextBlock { Text = $"{OpisGra(r, id)} – {ObrStanGry.Opis(x.P.Stan)}" + (x.P.Kandydaci.Count > 0 ? $" ({string.Join(", ", x.P.Kandydaci.Take(3))})" : ""), TextWrapping = TextWrapping.Wrap },
                                     HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(4, 2, 4, 2), Margin = new Thickness(0, 1, 0, 1), Background = WyjBg, BorderBrush = WyjBorder,
                                     ToolTip = "Zaznacz na płótnie" };
                b.Click += (s, e) => { Pokaz(id); RefreshGra(); };
                GraPanel.Children.Add(b);
            }
            if (StanGry.Nieumieszczone.Count > 0)
            {
                GraPanel.Children.Add(new TextBlock { Text = $"Obiekty gry bez miejsca na rysunku ({StanGry.Nieumieszczone.Count})", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 4) });
                GraPanel.Children.Add(Note("Były w pulpicie gry, a ich element / odcinek usunięto w edytorze – dorysuj i przypnij albo zostaw."));
                foreach (var g in StanGry.Nieumieszczone) GraPanel.Children.Add(Note("• " + g));
            }
        }
    }

    /// <summary>Przypięcie zaznaczonych do obiektu gry (<paramref name="gra"/>), „bez obiektu gry” (<see cref="ObrMapaGry.Bez"/>) albo usunięcie (null).</summary>
    public bool PrzypnijGra(string gra)
    {
        var o = Obraz; var (rodzaj, ids) = WybraneGra();
        if (MapaGry == null || o == null || ids.Count == 0) return false;
        if (gra != null && string.IsNullOrWhiteSpace(gra)) { Message("Gra: wpisz albo wybierz ID obiektu gry"); return false; }
        foreach (var id in ids) { MapaGry.Ustaw(o.Id, rodzaj, id, gra); StanGry?.Ustaw(o.Id, rodzaj, id, gra); }
        try { MapaGry.Zapisz(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Message("Nie można zapisać mapy gry: " + ex.Message); return false; }
        Message($"Gra: {(ids.Count == 1 ? OpisGra(rodzaj, ids[0]) : $"{ids.Count} odcinków")} → {(gra == null ? "przypięcie usunięte" : gra == ObrMapaGry.Bez ? "bez obiektu gry" : gra)} (zapisane w {Path.GetFileName(MapaGry.Plik)})");
        ZnacznikiGry(); RefreshGra();
        return true;
    }

    /// <summary>Stan przypięć od nowa z konwertera (Python: zob_do_suk.py --tylko-przypiecia). Zobrazowanie najpierw zapisane – konwerter czyta plik.</summary>
    public string PrzeliczGre()
    {
        if (MapaGry == null || Doc?.Path == null) return "brak mapy gry";
        if (Doc.Dirty && !SaveDoc(false)) return "zobrazowanie niezapisane";
        var skrypt = SkryptKonwertera();
        if (skrypt == null) { Message("Gra: nie znaleziono zob_do_suk.py (tools\\simrail w repozytorium albo stacje\\narzedzia w paczce)"); return "brak konwertera"; }
        string wynik = null;
        foreach (var (exe, pre) in new[] { ("python", ""), ("py", "-3 ") })
        {
            try
            {
                var psi = new ProcessStartInfo(exe, $"{pre}-I \"{skrypt}\" \"{Doc.Path}\" --tylko-przypiecia")
                    { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8 };
                psi.Environment["PYTHONIOENCODING"] = "utf-8";
                using var p = Process.Start(psi);
                var outT = p.StandardOutput.ReadToEndAsync(); var errT = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(120_000)) { try { p.Kill(); } catch (InvalidOperationException) { } wynik = "przekroczony czas"; break; }
                wynik = p.ExitCode == 0 ? null : (errT.Result.Trim().Split('\n').LastOrDefault() ?? $"kod {p.ExitCode}");
                if (wynik == null) Message("Gra: " + outT.Result.Trim().Split('\n').LastOrDefault());
                break;
            }
            catch (System.ComponentModel.Win32Exception) { wynik = "brak Pythona 3 (python / py)"; }
        }
        if (wynik != null) { Message("Gra – Przelicz: " + wynik); return wynik; }
        StanGry = ObrStanGry.Wczytaj(Doc.Path);
        ZnacznikiGry();
        if (Side.SelectedItem == TabGra || graZbudowana) RefreshGra();
        Side.SelectedItem = TabGra;
        return null;
    }

    /// <summary>zob_do_suk.py: SUK_NARZEDZIA, potem w górę od programu – tools\simrail (repozytorium) albo stacje\narzedzia (paczka wydania).</summary>
    static string SkryptKonwertera()
    {
        var env = Environment.GetEnvironmentVariable("SUK_NARZEDZIA");
        if (!string.IsNullOrEmpty(env) && File.Exists(Path.Combine(env, "zob_do_suk.py"))) return Path.Combine(env, "zob_do_suk.py");
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            foreach (var k in new[] { Path.Combine(d.FullName, "tools", "simrail", "zob_do_suk.py"), Path.Combine(d.FullName, "stacje", "narzedzia", "zob_do_suk.py") })
                if (File.Exists(k)) return k;
        return null;
    }

    /// <summary>Znaczniki na płótnie: odcinki i elementy do przypięcia (bez obiektu gry, nieświadomie).</summary>
    void ZnacznikiGry()
    {
        View.GraBrak.Clear(); View.GraBrakPunkty.Clear();
        var o = Obraz;
        if (MapaGry != null && StanGry != null && o != null)
            foreach (var (r, id, p) in StanGry.Obrazu(o.Id))
            {
                if (!ObrStanGry.DoPrzypiecia(p) || MapaGry.Gra(o.Id, r, id) != null) continue;
                if (r == ObrMapaGry.Odcinki && ObrGraf.Odcinek(o, id) is { } e)
                {
                    var (a, b) = ObrGraf.Konce(o, e);
                    View.GraBrak.Add(new[] { a }.Concat(e.Pkt).Append(b).Select(q => new Point(q[0], q[1])).ToList());
                }
                else if (o.Elementy.FirstOrDefault(x => x.Id == id) is { } el && ObrSymbole.Pozycja(o, el) is { } poz) View.GraBrakPunkty.Add(new Point(poz.X, poz.Y));
            }
        View.InvalidateVisual();
    }
}

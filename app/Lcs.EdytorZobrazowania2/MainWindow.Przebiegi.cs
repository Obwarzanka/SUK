using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;
using LcsWidzew.Sim;
using Stacja = LcsWidzew.Model.Station;

namespace Lcs.EdytorZobrazowania2;

/// <summary>
/// Etap 3 – tablica przebiegów w edytorze zobrazowania (ROZWOJ.md, decyzja właściciela 2026-10-26): jak w starym edytorze – klik na semafor
/// albo tarczę na obrazie, w zakładce Właściwości „Przebiegi od …” (✓ w tablicy, ★ zasadniczy, V, klik – droga na obrazie); okno
/// „Przebiegi…” (F6) – przegląd całej tablicy. Zmiany w <see cref="ObrProject.Przebiegi"/> z cofaniem, kontrola przy „Do symulatora…”.
/// Symulator czyta tylko przebiegi z tablicy.
/// </summary>
public partial class MainWindow
{
    public readonly Button PrzebiegiBtn = new() { Content = "Przebiegi…", Padding = new Thickness(6, 2, 6, 2), ToolTip = "Tablica przebiegów (F6): propozycje z układu, przyjęcie, przebieg zasadniczy, kontrola i test każdego przebiegu; symulator bierze tylko przebiegi z tablicy" };
    PrzebiegiWindow przebiegiWin;

    /// <summary>Okno tablicy przebiegów (jedno na edytor).</summary>
    public PrzebiegiWindow Przebiegi()
    {
        if (Doc == null) { Message("Przebiegi: najpierw otwórz zobrazowanie"); return null; }
        try
        {
            if (przebiegiWin == null)
            {
                przebiegiWin = new PrzebiegiWindow(this) { Owner = this };
                przebiegiWin.Closed += (s, e) => przebiegiWin = null;
            }
            else przebiegiWin.Przelicz();
            if (!NoPrompt) { przebiegiWin.Show(); przebiegiWin.Activate(); }
            Message($"Przebiegi: w układzie {przebiegiWin.Uklad.Count}, w tablicy {Doc.P.Przebiegi?.Count.ToString() ?? "– (nie zaprojektowana)"}");
            return przebiegiWin;
        }
        catch (Exception ex) { Message("Przebiegi: " + ex.Message); if (!NoPrompt) MessageBox.Show(this, ex.Message, "Przebiegi"); return null; }
    }

    static int Kolejnosc(ObrPrzebieg a, ObrPrzebieg b)
    {
        int c = (a.Typ == "P" ? 0 : 1).CompareTo(b.Typ == "P" ? 0 : 1);
        if (c == 0) c = string.CompareOrdinal(Stacja.Lbl(a.Od), Stacja.Lbl(b.Od));
        if (c == 0) c = string.CompareOrdinal(Stacja.Lbl(a.Do), Stacja.Lbl(b.Do));
        return c != 0 ? c : string.CompareOrdinal(a.Zwr, b.Zwr);
    }

    /// <summary>
    /// Przyjęcie przebiegów do tablicy. Propozycja zasadniczego: wariant 0 z układu, gdy dla tej pary początek – koniec nie ma jeszcze
    /// zasadniczego (człowiek zmienia przyciskiem „Zasadniczy”). Zwraca liczbę dodanych.
    /// </summary>
    public int PrzyjmijPrzebiegi(IEnumerable<RouteDef> routes)
    {
        var nowe = routes.ToList(); int n = 0;
        Doc.Change(nowe.Count == 1 ? $"Przebieg {TablicaPrzebiegow.Opis(TablicaPrzebiegow.Z(nowe[0]))} do tablicy" : $"Przebiegi do tablicy ({nowe.Count})", q =>
        {
            q.Przebiegi ??= new();
            foreach (var r in nowe)
            {
                if (q.Przebiegi.Any(p => TablicaPrzebiegow.Pasuje(p, r))) continue;
                bool zas = r.Variant == 0 && !q.Przebiegi.Any(p => p.Zasadniczy && p.Typ == r.Type && p.Od == r.Start && p.Do == r.End);
                q.Przebiegi.Add(TablicaPrzebiegow.Z(r, zas)); n++;
            }
            q.Przebiegi.Sort(Kolejnosc);
        });
        Message(n > 0 ? $"Przebiegi: do tablicy dodano {n} (Ctrl+Z cofa)" : "Przebiegi: wybrane są już w tablicy");
        return n;
    }

    /// <summary>Usunięcie przebiegów z tablicy (klucze <see cref="ObrPrzebieg.Klucz"/>); tablica pusta zostaje pusta – nie wraca do wyznaczania przez symulator.</summary>
    public int UsunPrzebiegi(IEnumerable<string> klucze)
    {
        var k = klucze.ToHashSet(); int n = 0;
        if (Doc.P.Przebiegi == null || k.Count == 0) return 0;
        Doc.Change(k.Count == 1 ? $"Przebieg usunięty z tablicy" : $"Przebiegi usunięte z tablicy ({k.Count})", q => n = q.Przebiegi.RemoveAll(p => k.Contains(p.Klucz)));
        Message($"Przebiegi: z tablicy usunięto {n} (Ctrl+Z cofa)");
        return n;
    }

    /// <summary>Przebieg zasadniczy dla swojej pary początek – koniec (pozostałe warianty tej pary – wariantowe).</summary>
    public bool UstawZasadniczy(string klucz)
    {
        var p = Doc.P.Przebiegi?.FirstOrDefault(x => x.Klucz == klucz);
        if (p == null) { Message("Przebiegi: zasadniczy może być tylko przebieg z tablicy"); return false; }
        var (t, od, @do) = (p.Typ, p.Od, p.Do);
        bool ok = Doc.Change($"Przebieg zasadniczy {TablicaPrzebiegow.Opis(p)}", q => { foreach (var x in q.Przebiegi.Where(x => x.Typ == t && x.Od == od && x.Do == @do)) x.Zasadniczy = x.Klucz == klucz; });
        Message($"Przebiegi: {TablicaPrzebiegow.Opis(p)} – zasadniczy");
        return ok;
    }

    /// <summary>Uwagi do „Do symulatora…”: tablica nie zaprojektowana albo błędy kontroli tablicy.</summary>
    List<string> UwagiPrzebiegow(ObrKompilacja.Wynik k)
    {
        if (Doc.P.Przebiegi == null) return new() { "tablica przebiegów nie jest zaprojektowana (Przebiegi…, F6) – symulator wyznaczy przebiegi z układu sam" };
        var m = TablicaPrzebiegow.ZUkladu(Doc.Ut, k.Zob);
        return TablicaPrzebiegow.Sprawdz(Doc.P.Przebiegi, m.St, m.Routes, m).Where(u => u.Blad).Select(u => "przebiegi: " + u.Tekst).ToList();
    }

    // ------------------------------------------------------------------ panel „Przebiegi od …” (zakładka Właściwości)
    TablicaPrzebiegow.Model modelUkladu; UtProject modelUt;
    string drogaKlucz, drogaEl;
    /// <summary>Wyniki „Testuj” z panelu (klucz <see cref="ObrPrzebieg.Klucz"/>).</summary>
    public readonly Dictionary<string, TablicaPrzebiegow.Test> TestyPrzebiegow = new();
    /// <summary>Panel przebiegów zaznaczonego sygnalizatora (testy); null – nie ma.</summary>
    public StackPanel PrzebiegiPanel { get; private set; }
    static readonly Brush KolorP = new SolidColorBrush(Color.FromRgb(0x10, 0x80, 0x30)), KolorM = new SolidColorBrush(Color.FromRgb(0x90, 0x70, 0x00));

    /// <summary>Przebiegi możliwe w układzie – liczone raz dla wczytanego UT (zależą tylko od UT).</summary>
    public TablicaPrzebiegow.Model ModelUkladu()
    {
        if (modelUkladu == null || !ReferenceEquals(modelUt, Doc.Ut)) { modelUkladu = TablicaPrzebiegow.ZUkladu(Doc.Ut, Kompiluj().Zob); modelUt = Doc.Ut; }
        return modelUkladu;
    }

    /// <summary>Semafor / tarcza na obrazie → nazwa sygnalizatora w symulatorze (przez przypięcie do UT); null – nieprzypięty albo inny symbol.</summary>
    public string SygnalElementu(ObrElement el)
    {
        if (el.Typ is not (ObrSymbole.Semafor or ObrSymbole.Tarcza)) return null;
        var pr = Obraz.Przypiecia.FirstOrDefault(p => p.El == el.Id && p.Stan is ObrPrzypiecie.Pewne or ObrPrzypiecie.Potwierdzone);
        return pr?.Ut != null && ModelUkladu().W.Sygnaly.TryGetValue(pr.Ut, out var k) ? k : null;
    }

    /// <summary>Panel jak w starym edytorze: przebiegi od sygnalizatora pogrupowane wg końca.</summary>
    void PrzebiegiSemafora(ObrElement el)
    {
        PrzebiegiPanel = null;
        if (el.Typ is not (ObrSymbole.Semafor or ObrSymbole.Tarcza)) return;
        string sig;
        try { sig = SygnalElementu(el); }
        catch (Exception ex) { PropPanel.Children.Add(Note("Przebiegi: błąd układu – " + ex.Message)); return; }
        var sp = new StackPanel(); PrzebiegiPanel = sp; PropPanel.Children.Add(sp);
        if (sig == null) { sp.Children.Add(Note("Przebiegi: symbol nie jest przypięty do UT – po przypięciu (F8) tu będą przebiegi od niego.")); return; }
        var m = ModelUkladu();
        var proj = Doc.P.Przebiegi;
        // propozycje z układu i własne przebiegi z tablicy (spoza propozycji)
        var raw = m.Routes.Where(r => r.Start == sig).ToList();
        foreach (var p in proj?.Where(p => p.Od == sig && !raw.Any(r => TablicaPrzebiegow.Pasuje(p, r))) ?? Enumerable.Empty<ObrPrzebieg>())
            if (TablicaPrzebiegow.Droga(m, p) is RouteDef wr) raw.Add(wr);
        raw = TablicaPrzebiegow.Porzadek(raw).ToList();
        WlasnyPanel(sp, sig);
        sp.Children.Add(new TextBlock { Text = $"Przebiegi od {Stacja.Lbl(sig)}", FontWeight = FontWeights.SemiBold, FontSize = 14, Margin = new Thickness(0, 10, 0, 2) });
        if (raw.Count == 0) { sp.Children.Add(Note("Od tego sygnalizatora nie ma przebiegów w układzie.")); return; }
        sp.Children.Add(Note(proj == null
            ? "Tablica przebiegów nie jest jeszcze zaprojektowana – symulator wyznacza przebiegi sam. Zaznacz ✓ przebiegi, które mają być w tablicy (do symulatora pójdą tylko one)."
            : $"W tablicy {raw.Count(r => proj.Any(p => TablicaPrzebiegow.Pasuje(p, r)))} z {raw.Count}. ✓ – w tablicy, ★ – zasadniczy (pozostałe do tego samego końca – wariantowe), V – prędkość przebiegu [km/h]. Klik na opisie – droga na obrazie."));
        foreach (var grp in raw.GroupBy(r => (r.Type, r.End)))
        {
            var r0 = grp.First();
            string kon = r0.EndKind switch { "szlak" => " (szlak)", "szlakM" => " (na szlak)", "gr" => " (do semafora wjazdowego)", "kz" => " (koniec toru)", _ => "" };
            sp.Children.Add(new TextBlock { Text = $"{(grp.Key.Type == "P" ? "Pociągowy" : "Manewrowy")} → {Stacja.Lbl(grp.Key.End)}{kon}", FontWeight = FontWeights.SemiBold, Foreground = grp.Key.Type == "P" ? KolorP : KolorM, Margin = new Thickness(0, 6, 0, 1) });
            foreach (var r in grp) sp.Children.Add(WierszPrzebiegu(r, proj?.FirstOrDefault(p => TablicaPrzebiegow.Pasuje(p, r)), m.St));
        }
        var row = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        row.Children.Add(Btn($"Wszystkie od {Stacja.Lbl(sig)}", (s, e) => PrzyjmijPrzebiegi(raw), "Wszystkie przebiegi od tego sygnalizatora do tablicy (niepotrzebne odznacz)"));
        var t = Btn("Testuj", (s, e) => TestujOd(sig), "Każdy przebieg z tablicy od tego sygnalizatora osobno: polecenie dyżurnego w stanie zasadniczym, do 30 s – czy dochodzi do sygnału zezwalającego");
        t.Margin = new Thickness(6, 0, 0, 0); row.Children.Add(t);
        sp.Children.Add(row);
        sp.Children.Add(Note("Całą tablicę widać w oknie „Przebiegi…” (F6). Zależności specjalne (droga ochronna, sprzeczne, dopuszczone) – w symulatorze, „Tablica przebiegów”."));
    }

    // ------------------------------------------------------------------ własny przebieg (decyzja właściciela 2026-10-27)
    /// <summary>Warunki własnego przebiegu w panelu: sygnalizator, rodzaj, koniec, odcinek „przez”, położenia zwrotnic (bez wpisu – dowolne).</summary>
    public string WlasnyOd, WlasnyTyp = "P", WlasnyDo, WlasnyPrzez;
    public readonly Dictionary<string, string> WlasneZwr = new();
    /// <summary>Drogi spełniające warunki (ostatnie wyszukanie w panelu).</summary>
    public List<RouteDef> WlasneDrogi { get; private set; } = new();

    /// <summary>Ustawienie warunków własnego przebiegu (jak wybór w panelu) – zwraca drogi w układzie, które je spełniają.</summary>
    public List<RouteDef> WlasnyPrzebieg(string od, string typ, string @do, IDictionary<string, string> zwr = null, string przez = null)
    {
        WlasnyOd = od; WlasnyTyp = typ; WlasnyDo = @do; WlasnyPrzez = przez;
        WlasneZwr.Clear(); if (zwr != null) foreach (var (k, v) in zwr) WlasneZwr[k] = v;
        WlasneDrogi = SzukajWlasnych();
        RefreshWlasciwosci();
        return WlasneDrogi;
    }

    List<RouteDef> SzukajWlasnych() => WlasnyOd == null || WlasnyDo == null ? new() :
        TablicaPrzebiegow.Drogi(ModelUkladu(), WlasnyOd, WlasnyTyp, WlasnyDo, WlasneZwr.Select(kv => (kv.Key, kv.Value)), WlasnyPrzez);

    void WlasnyPanel(StackPanel sp, string sig)
    {
        if (WlasnyOd != sig) { WlasnyOd = sig; WlasnyDo = null; WlasnyPrzez = null; WlasneZwr.Clear(); WlasneDrogi = new(); }
        var m = ModelUkladu();
        var st = m.St.Signals[sig];
        sp.Children.Add(new TextBlock { Text = $"Własny przebieg od {Stacja.Lbl(sig)}", FontWeight = FontWeights.SemiBold, FontSize = 14, Margin = new Thickness(0, 12, 0, 2) });
        sp.Children.Add(Note("Wybierz koniec, a w razie potrzeby położenia zwrotnic i odcinek, przez który ma iść. Program pokaże drogi w układzie, które to spełniają – także takie, których nie ma wśród propozycji."));
        var typy = new List<string>(); if (st.IsTrain) typy.Add("P"); if (st.ForShunts) typy.Add("M");
        if (typy.Count == 0) return;
        if (!typy.Contains(WlasnyTyp)) WlasnyTyp = typy[0];
        // wszystkie drogi danego rodzaju bez warunków – końce, a dla końca: zwrotnice i odcinki na drodze
        var wszystkie = TablicaPrzebiegow.Drogi(m, sig, WlasnyTyp);
        var grid = new Grid { Margin = new Thickness(6, 2, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); grid.ColumnDefinitions.Add(new ColumnDefinition());
        void Wiersz(string label, UIElement el)
        {
            int r = grid.RowDefinitions.Count; grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var t = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 1, 6, 1) };
            Grid.SetRow(t, r); Grid.SetRow(el, r); Grid.SetColumn(el, 1); grid.Children.Add(t); grid.Children.Add(el);
        }
        ComboBox Combo(IEnumerable<(string Val, string Txt)> items, string sel, Action<string> set, string tip)
        {
            var c = new ComboBox { Margin = new Thickness(0, 1, 0, 1), ToolTip = tip, MinWidth = 120 };
            var l = items.ToList();
            foreach (var (_, txt) in l) c.Items.Add(txt);
            c.SelectedIndex = Math.Max(0, l.FindIndex(x => x.Val == sel));
            c.SelectionChanged += (s, e) => { if (c.SelectedIndex >= 0) { set(l[c.SelectedIndex].Val); WlasneDrogi = SzukajWlasnych(); Dispatcher.BeginInvoke(RefreshWlasciwosci); } };
            return c;
        }
        if (typy.Count > 1)
            Wiersz("Rodzaj", Combo(typy.Select(t => (t, t == "P" ? "pociągowy" : "manewrowy")), WlasnyTyp, v => { WlasnyTyp = v; WlasnyDo = null; WlasnyPrzez = null; WlasneZwr.Clear(); }, "Rodzaj przebiegu"));
        var konce = wszystkie.GroupBy(r => r.End).Select(g => (g.Key, Stacja.Lbl(g.Key) + g.First().EndKind switch { "szlak" => " (szlak)", "szlakM" => " (na szlak)", "gr" => " (do wjazdowego)", "kz" => " (koniec toru)", _ => "" })).ToList();
        if (konce.Count == 0) { sp.Children.Add(Note("Od tego sygnalizatora nie ma w układzie dróg tego rodzaju.")); return; }
        Wiersz("Koniec", Combo(konce.Prepend((null, "– wybierz –")), WlasnyDo, v => { WlasnyDo = v; WlasnyPrzez = null; WlasneZwr.Clear(); }, "Koniec przebiegu (sygnalizator, szlak, koniec toru)"));
        if (WlasnyDo != null)
        {
            var doKonca = wszystkie.Where(r => r.End == WlasnyDo).ToList();
            var odc = doKonca.SelectMany(r => r.Secs).Distinct().OrderBy(Stacja.Lbl, StringComparer.Ordinal).ToList();
            Wiersz("Przez", Combo(odc.Select(s => (s, Stacja.Lbl(s))).Prepend((null, "dowolnie")), WlasnyPrzez, v => WlasnyPrzez = v, "Odcinek, przez który ma iść przebieg"));
            foreach (var z in doKonca.SelectMany(r => r.Sw.Select(q => q.Num)).Distinct().OrderBy(n => Stacja.Lbl(n), StringComparer.Ordinal))
            {
                var num = z;
                Wiersz($"Zwrotnica {Stacja.Lbl(num)}", Combo(new (string, string)[] { (null, "dowolnie"), ("+", "+ (zasadnicze)"), ("-", "− (przełożone)") },
                    WlasneZwr.GetValueOrDefault(num), v => { if (v == null) WlasneZwr.Remove(num); else WlasneZwr[num] = v; }, "Położenie zwrotnicy w przebiegu"));
            }
        }
        sp.Children.Add(grid);
        if (WlasnyDo == null) return;
        if (WlasneDrogi.Count == 0) { sp.Children.Add(Note("Nie ma w układzie drogi, która spełnia te warunki.")); return; }
        var proj = Doc.P.Przebiegi;
        sp.Children.Add(new TextBlock { Text = WlasneDrogi.Count == 1 ? "Jest jedna taka droga:" : $"Takich dróg w układzie: {WlasneDrogi.Count}", Margin = new Thickness(6, 6, 0, 2) });
        foreach (var r in WlasneDrogi)
        {
            var w = new DockPanel { Margin = new Thickness(6, 0, 0, 1) };
            bool jest = proj?.Any(p => TablicaPrzebiegow.Pasuje(p, r)) == true;
            var b = new Button { Content = jest ? "w tablicy" : "Dodaj", IsEnabled = !jest, Padding = new Thickness(6, 0, 6, 0), Margin = new Thickness(0, 0, 6, 0), ToolTip = "Dodaj ten przebieg do tablicy przebiegów" };
            b.Click += (s, e) => PrzyjmijPrzebiegi(new[] { r });
            DockPanel.SetDock(b, System.Windows.Controls.Dock.Left); w.Children.Add(b);
            var t = new TextBlock { Text = $"[{(r.SwText == "" ? "bez zwrotnic" : r.SwText)}]  odcinki: {string.Join(" ", r.Secs.Select(Stacja.Lbl))}", TextWrapping = TextWrapping.Wrap, Cursor = Cursors.Hand, ToolTip = $"{r.Id}\nklik – droga na obrazie", VerticalAlignment = VerticalAlignment.Center };
            t.MouseLeftButtonUp += (s, e) => PokazDroge(r);
            w.Children.Add(t);
            sp.Children.Add(w);
        }
    }

    UIElement WierszPrzebiegu(RouteDef r, ObrPrzebieg p, Stacja st)
    {
        var klucz = TablicaPrzebiegow.Z(r).Klucz;
        var row = new DockPanel { Margin = new Thickness(6, 0, 0, 1) };
        var on = new CheckBox { IsChecked = p != null, VerticalAlignment = VerticalAlignment.Center, ToolTip = "przebieg w tablicy przebiegów" };
        on.Click += (s, e) => { if (on.IsChecked == true) PrzyjmijPrzebiegi(new[] { r }); else UsunPrzebiegi(new[] { klucz }); };
        var star = new Button { Content = p?.Zasadniczy == true ? "★" : "☆", Padding = new Thickness(4, 0, 4, 0), Margin = new Thickness(4, 0, 4, 0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.DarkGoldenrod, FontSize = 15, IsEnabled = p != null, ToolTip = "przebieg zasadniczy (pozostałe do tego samego końca – wariantowe)" };
        star.Click += (s, e) => UstawZasadniczy(klucz);
        DockPanel.SetDock(on, System.Windows.Controls.Dock.Left); DockPanel.SetDock(star, System.Windows.Controls.Dock.Left);
        row.Children.Add(on); row.Children.Add(star);
        if (r.Type == "P")
        {
            var pred = LcsWidzew.Model.Zaleznosci.Predkosci;
            var v = new ComboBox { Width = 58, Margin = new Thickness(0, 0, 4, 0), IsEnabled = p != null, VerticalAlignment = VerticalAlignment.Center, ToolTip = $"Prędkość przebiegu [km/h] – pierwsza pozycja: z torów i zwrotnic ({r.VCalc:0} km/h), * – ustalona" };
            v.Items.Add($"{r.VCalc:0}");
            foreach (var x in pred) v.Items.Add($"{x}*");
            v.SelectedIndex = p?.V > 0 ? Array.IndexOf(pred, p.V) + 1 : 0;
            v.SelectionChanged += (s, e) => { if (v.SelectedIndex >= 0) UstawV(klucz, v.SelectedIndex == 0 ? 0 : pred[v.SelectedIndex - 1]); };
            DockPanel.SetDock(v, System.Windows.Controls.Dock.Left); row.Children.Add(v);
        }
        string uw = p?.V > 0 && st.Signals.TryGetValue(r.Start, out var sd) && sd.Dobor(p.V).Uwaga is string su ? $"\n⚠ {su}" : "";
        string test = TestyPrzebiegow.TryGetValue(klucz, out var tw) ? $"\n{(tw.Wynik == TablicaPrzebiegow.Test.Sygnal ? "✓" : "✗")} test: {tw.Wynik}{(tw.Wynik == TablicaPrzebiegow.Test.Sygnal ? "" : " – " + tw.Tekst)}" : "";
        var txt = new TextBlock
        {
            Text = (p == null ? "poza tablicą" : p.Zasadniczy ? "zasadniczy" : "wariantowy") + $"  [{(r.SwText == "" ? "bez zwrotnic" : r.SwText)}]" + uw + test,
            Foreground = p == null ? Brushes.Gray : Brushes.Black, FontWeight = drogaKlucz == klucz ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Cursor = Cursors.Hand,
            ToolTip = $"{r.Id}\nodcinki: {string.Join(" ", r.Secs.Select(Stacja.Lbl))}" + (r.Overlap != null ? $"\ndroga ochronna: {Stacja.Lbl(r.Overlap)}" : "") + (r.Flank.Count > 0 ? $"\nochrona boczna: {r.FlankText}" : "") + "\nklik – droga na obrazie",
        };
        txt.MouseLeftButtonUp += (s, e) => PokazDroge(r);
        row.Children.Add(txt);
        return row;
    }

    /// <summary>Prędkość przebiegu pociągowego z tablicy (0 – z torów i zwrotnic).</summary>
    public bool UstawV(string klucz, int v)
    {
        var p = Doc.P.Przebiegi?.FirstOrDefault(x => x.Klucz == klucz);
        if (p == null || p.V == v) return false;
        bool ok = Doc.Change($"Prędkość przebiegu {TablicaPrzebiegow.Opis(p)}", q => q.Przebiegi.First(x => x.Klucz == klucz).V = v);
        Message($"Przebiegi: {TablicaPrzebiegow.Opis(p)} – {(v > 0 ? $"V {v} km/h" : "prędkość z torów i zwrotnic")}");
        return ok;
    }

    /// <summary>Droga przebiegu na bieżącym obrazie: tory symulatora → km UT → łamane torów obrazu (kompilacja obrazu). Zwraca liczbę łamanych.</summary>
    public int PokazDroge(RouteDef r)
    {
        var m = ModelUkladu();
        var zo = Kompiluj().Zob.Obrazy.FirstOrDefault(x => x.Id == Obraz?.Id);
        var res = new List<List<Point>>();
        if (zo != null)
            foreach (var eid in r.Edges)
            {
                if (!m.W.Krawedzie.TryGetValue(eid, out var kr)) continue;
                foreach (var t in zo.Tory.Where(t => t.Ut.Tor == kr.Tor && t.Pkt.Count >= 2 && Math.Abs(t.Ut.S1 - t.Ut.S0) > 1e-6))
                {
                    double a = Math.Max(Math.Min(kr.S0, kr.S1), Math.Min(t.Ut.S0, t.Ut.S1)), b = Math.Min(Math.Max(kr.S0, kr.S1), Math.Max(t.Ut.S0, t.Ut.S1));
                    if (b - a < 0.05) continue;
                    double d = t.Ut.S1 - t.Ut.S0, f0 = (a - t.Ut.S0) / d, f1 = (b - t.Ut.S0) / d;
                    res.Add(Wycinek(t.Pkt, Math.Min(f0, f1), Math.Max(f0, f1)));
                }
            }
        drogaKlucz = TablicaPrzebiegow.Z(r).Klucz; drogaEl = View.SelEl.Count == 1 ? View.SelEl.First() : null;
        View.Droga = res; View.InvalidateVisual();
        RefreshWlasciwosci();
        Message(res.Count > 0 ? $"{r.Id}: droga na obrazie; odcinki {string.Join(" ", r.Secs.Select(Stacja.Lbl))}" : $"{r.Id}: drogi nie ma na tym obrazie (tory nieprzypięte – F8)");
        return res.Count;
    }

    /// <summary>Część łamanej od ułamka f0 do f1 jej długości.</summary>
    static List<Point> Wycinek(List<int[]> pkt, double f0, double f1)
    {
        var pts = pkt.Select(p => new Point(p[0], p[1])).ToList();
        double len = 0; for (int i = 1; i < pts.Count; i++) len += (pts[i] - pts[i - 1]).Length;
        double a = f0 * len, b = f1 * len, s = 0;
        var res = new List<Point>();
        for (int i = 1; i < pts.Count; i++)
        {
            double l = (pts[i] - pts[i - 1]).Length; if (l <= 0) continue;
            double s0 = s, s1 = s + l; s = s1;
            if (s1 < a || s0 > b) continue;
            var p0 = pts[i - 1]; var dv = pts[i] - p0;
            if (res.Count == 0) res.Add(p0 + dv * ((Math.Max(a, s0) - s0) / l));
            res.Add(p0 + dv * ((Math.Min(b, s1) - s0) / l));
        }
        return res;
    }

    /// <summary>Test przebiegów z tablicy od sygnalizatora – wynik przy każdym przebiegu w panelu.</summary>
    public List<TablicaPrzebiegow.Test> TestujOd(string sig)
    {
        var proj = Doc.P.Przebiegi;
        var od = proj?.Where(p => p.Od == sig).ToList();
        if (od == null || od.Count == 0) { Message("Testuj: od tego sygnalizatora nie ma przebiegów w tablicy"); return new(); }
        var old = Cursor; Cursor = Cursors.Wait;
        try
        {
            var res = TablicaPrzebiegow.Testuj(Doc.Ut, Kompiluj().Zob, proj, od);
            foreach (var t in res) TestyPrzebiegow[t.P.Klucz] = t;
            RefreshWlasciwosci();
            Message($"Testuj {Stacja.Lbl(sig)}: {res.Count} przebiegów, sygnał zezwalający {res.Count(t => t.Wynik == TablicaPrzebiegow.Test.Sygnal)}");
            return res;
        }
        finally { Cursor = old; }
    }
}

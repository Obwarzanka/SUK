using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Metody budowy łuku w panelu i na wykresie krzywizny (PLAN_EDYTOR_UT.md § 5g): łuk w wierzchołku wieloboku (L na wyjściu, długość łuku
/// i styczna z klawiatury, blokada R), trasa z elementów (tabela, zamiana wielobok ↔ elementy), wykres krzywizny pod mapą.
/// </summary>
public partial class MainWindow
{
    public readonly KrzywiznaView Krzyw = new() { Height = 200 };
    public readonly ToggleButton KrzywBtn = new() { Content = "Krzywizna", IsChecked = true, Padding = new Thickness(6, 2, 6, 2), ToolTip = "Wykres krzywizny zaznaczonego toru pod mapą (edycja toru z elementów, uwagi do łuków)" };
    Border krzywHost;

    UIElement BuildKrzywizna()
    {
        krzywHost = new Border { Child = Krzyw, BorderBrush = Brushes.Silver, BorderThickness = new Thickness(0, 1, 0, 0) };
        KrzywBtn.Click += (s, e) => { krzywHost.Visibility = KrzywBtn.IsChecked == true ? Visibility.Visible : Visibility.Collapsed; UpdateKrzyw(); };
        Krzyw.Preview += a => { if (Krzyw.Track is UtTrack t) { View.AlignPreview = (t.Id, a); View.InvalidateVisual(); Message(Krzyw.LastInfo); } };
        Krzyw.Commit += (a, what) =>
        {
            View.AlignPreview = null;
            if (Krzyw.Track is not UtTrack t) return;
            var test = TrasaElem.Compute(a, Trasa.KFor(t));
            if (!test.Ok) { Message("Nie da się: " + string.Join("; ", test.Notes.Select(n => n.Text))); View.InvalidateVisual(); return; }
            ChTrack(t.Id, what, x => x.Align = a);
            Message(what + (Krzyw.LastInfo.Length > 0 ? ": " + Krzyw.LastInfo : "") + " (Ctrl+Z przywraca)");
        };
        Krzyw.ToAlign += () => { if (Krzyw.Track is UtTrack t) ToAlignTrack(t.Id); };
        View.VerticesChanged += (id, v, what, info) => { ChTrack(id, what, x => x.Vertices = v); Message(info + " (Ctrl+Z przywraca)"); };
        View.AlignFitDone += (id, a, info) => { ChTrack(id, "Łuk z punktów", x => x.Align = a); View.Select(id); Message(info); };
        Krzyw.HoverS += s => { View.MarkS = s is double v && Krzyw.Track is UtTrack t ? (t.Id, v) : null; View.InvalidateVisual(); };
        View.CursorMoved += p =>
        {
            if (Krzyw.Track is not UtTrack t || p is not XY w) { if (Krzyw.CursorS != null) { Krzyw.CursorS = null; Krzyw.InvalidateVisual(); } return; }
            var r = View.Geometry(t); var (s, d) = r.Nearest(w, 1);
            Krzyw.CursorS = d / r.K < 25 * View.Mpp ? s : null; Krzyw.InvalidateVisual();
        };
        return krzywHost;
    }

    /// <summary>Wykres krzywizny dla zaznaczonego toru (po zmianie zaznaczenia i każdej zmianie projektu).</summary>
    public void UpdateKrzyw()
    {
        if (KrzywBtn.IsChecked != true) return;
        var t = Doc?.P.Tracks.FirstOrDefault(x => x.Id == View.SelectedTrack);
        Krzyw.Show(t, t != null ? View.Geometry(t) : null);
    }

    /// <summary>Wielobok → trasa z elementów (edycja na wykresie krzywizny).</summary>
    public void ToAlignTrack(string id)
    {
        var t = Doc.P.Tracks.FirstOrDefault(x => x.Id == id); if (t == null || t.Align != null) return;
        if (Doc.P.Turnouts.Any(z => z.Branch == id)) { Message("To odgałęzienie rozjazdu – jego geometrię ustawia rozjazd, zostaje wielobokiem"); return; }
        var (a, max) = TrasaElem.FromPolygon(t); if (a == null) return;
        ChTrack(id, "Zamiana na trasę z elementów", x => x.Align = a);
        View.Select(id);
        Message($"Tor zamieniony na trasę z elementów ({a.Els.Count}) – edycja na wykresie krzywizny pod mapą i w tabeli w panelu; przejściowe jako klotoidy, odchyłka od wieloboku najwyżej {max * 100:0.#} cm (Ctrl+Z przywraca)");
    }

    /// <summary>Trasa z elementów → wielobok (przybliżenie).</summary>
    public void ToPolygonTrack(string id)
    {
        var t = Doc.P.Tracks.FirstOrDefault(x => x.Id == id); if (t?.Align == null) return;
        var (v, max) = TrasaElem.ToPolygon(t.Align, Trasa.KFor(t));
        ChTrack(id, "Zamiana na wielobok", x => { x.Align = null; x.Vertices = v; });
        View.Select(id);
        Message($"Tor zamieniony na wielobok ({v.Count} wierzchołki) – odchyłka od trasy z elementów najwyżej {max * 100:0.#} cm" + (max > 0.05 ? " (wielobok nie ma przejściowej między łukami – łuki koszowe przybliżone jednym R)" : "") + " (Ctrl+Z przywraca)");
    }

    /// <summary>Tabela elementów toru z elementów (długość, R; przejściowa – R0 → R1 z sąsiadów).</summary>
    void BuildAlignTable(UtTrack t)
    {
        var id = t.Id; var a = t.Align;
        TrackPanel.Children.Add(Label("Trasa z elementów – w terenie [m] (R: + w prawo, − w lewo)"));
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 6) };
        foreach (var w in new[] { 24.0, 82, 66, 90 }) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });
        int row = 0;
        void Cell(UIElement e, int c) { Grid.SetRow(e, row); Grid.SetColumn(e, c); grid.Children.Add(e); }
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        foreach (var (h, c) in new[] { ("", 0), ("rodzaj", 1), ("długość", 2), ("promień R", 3) }) Cell(new TextBlock { Text = h, FontSize = 11, Foreground = Brushes.DimGray }, c);
        for (int i = 0; i < a.Els.Count; i++)
        {
            row++; grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            int ei = i; var el = a.Els[i];
            Cell(new TextBlock { Text = $"{i + 1}", VerticalAlignment = VerticalAlignment.Center }, 0);
            Cell(new TextBlock { Text = el.Nazwa, VerticalAlignment = VerticalAlignment.Center }, 1);
            TextBox Box(double val, int col, string tip, Action<UtAlignEl, double> set, string what)
            {
                var tb = new TextBox { Text = F(val, "0.##"), Margin = new Thickness(0, 1, 4, 1), ToolTip = tip };
                void Commit() { if (Num(tb.Text) is double x && Math.Abs(x - val) > 1e-9) AlignEdit(id, al => set(al.Els[ei], x), what); }
                tb.LostFocus += (s, e) => Commit(); tb.KeyDown += (s, e) => { if (e.Key == Key.Enter) Commit(); };
                Cell(tb, col); return tb;
            }
            Box(el.Len, 2, $"element {i + 1}: długość [m]", (x, v) => x.Len = v, "Długość elementu");
            if (el.Luk) Box(el.R0, 3, $"element {i + 1}: promień łuku [m], + w prawo, − w lewo", (x, v) => { if (v != 0) x.R0 = x.R1 = v; }, "Promień łuku");
            else if (el.Przejsciowa) Cell(new TextBlock { Text = $"{Rt(el.R0)} → {Rt(el.R1)}", VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.DimGray, ToolTip = "Promienie przejściowej wynikają z sąsiednich elementów" }, 3);
        }
        TrackPanel.Children.Add(grid);
        TrackPanel.Children.Add(SmallBtn("Zamień na wielobok", () => ToPolygonTrack(id), "Z powrotem wielobok stycznych (wierzchołki z R, L) – przybliżenie: łuk koszowy z przejściową między łukami zastąpi jeden R"));
    }

    static string Rt(double r) => r == 0 ? "prosta" : $"{Math.Abs(r):0}";

    /// <summary>Zmiana trasy z elementów z kontrolą (długości > 0) i porządkowaniem przejściowych.</summary>
    void AlignEdit(string id, Action<UtAlign> f, string what)
    {
        var t = Doc.P.Tracks.FirstOrDefault(x => x.Id == id); if (t?.Align == null) return;
        var a = t.Align.Clone(); f(a); TrasaElem.Normalize(a);
        if (a.Els.Any(e => e.Len <= 0.05)) { Message("Długość elementu musi być większa od 0"); RefreshTrackPanel(); return; }
        ChTrack(id, what, x => x.Align = a);
    }

    /// <summary>Łuk w zaznaczonym wierzchołku wieloboku: L na wyjściu, długość łuku i styczna T z klawiatury (→ R), kąt, blokada R.</summary>
    void BuildArcDetails(UtTrack t, TrasaResult r)
    {
        int vi = View.SelectedVertex; var id = t.Id;
        if (vi <= 0 || vi >= t.Vertices.Count - 1 || t.Vertices[vi].R <= 0) return;
        var v = t.Vertices[vi]; double k = r.K;
        double alpha = Math.Abs(Math.IEEERemainder(Trasa.Az(t.Vertices[vi].P, t.Vertices[vi + 1].P) - Trasa.Az(t.Vertices[vi - 1].P, t.Vertices[vi].P), 2 * Math.PI));
        double arcLen = r.Elements.Where(e => e.Vertex == vi && e.Kind == ElKind.Luk).Sum(e => e.Len) / k;
        double T1 = r.Tangent.TryGetValue(vi, out var tt) ? tt / k : 0;
        TrackPanel.Children.Add(Label($"Łuk w W{vi + 1}: kąt zwrotu {F(alpha * 180 / Math.PI, "0.###")}°"));
        var g = new Grid { Margin = new Thickness(0, 2, 0, 6) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        int row = 0;
        void Row(string label, double val, string tip, Action<double> commit)
        {
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }; Grid.SetRow(l, row); g.Children.Add(l);
            var tb = new TextBox { Text = F(val, "0.###"), Margin = new Thickness(0, 1, 0, 1), ToolTip = tip }; Grid.SetRow(tb, row); Grid.SetColumn(tb, 1); g.Children.Add(tb);
            void C() { if (Num(tb.Text) is double x && x >= 0 && Math.Abs(x - val) > 1e-6) commit(x); }
            tb.LostFocus += (s, e) => C(); tb.KeyDown += (s, e) => { if (e.Key == Key.Enter) C(); };
            row++;
        }
        Row("przejściowa na wyjściu L2 [m]", v.LOut, "Krzywa przejściowa na wyjściu z łuku (L – na wejściu); taka sama jak L – zostaw równą", x => ChTrack(id, "Przejściowa na wyjściu", tr => tr.Vertices[vi].L2 = Math.Abs(x - tr.Vertices[vi].L) < 1e-9 ? null : x));
        Row("długość łuku kołowego [m] → R", arcLen, "Wpisz długość części kołowej – program dobierze R (kąt i przejściowe bez zmian)", x => RadiusFrom(id, vi, rr => rr.Elements.Where(e => e.Vertex == vi && e.Kind == ElKind.Luk).Sum(e => e.Len) / rr.K, x, "długość łuku"));
        Row("styczna T [m] → R", T1, "Wpisz długość stycznej (od wierzchołka do początku łuku / przejściowej) – program dobierze R", x => RadiusFrom(id, vi, rr => rr.Tangent.TryGetValue(vi, out var q) ? q / rr.K : double.NaN, x, "styczna"));
        TrackPanel.Children.Add(g);
        var lk = new CheckBox { Content = "zablokuj R (dopasowania i uchwyt łuku go nie zmieniają)", IsChecked = v.LockR, Margin = new Thickness(0, 0, 0, 6) };
        lk.Click += (s, e) => ChTrack(id, "Blokada promienia", tr => tr.Vertices[vi].LockR = lk.IsChecked == true);
        TrackPanel.Children.Add(lk);
    }

    /// <summary>R dobrany (połowienie) tak, żeby miara łuku (długość łuku albo styczna) miała zadaną wartość.</summary>
    void RadiusFrom(string id, int vi, Func<TrasaResult, double> measure, double target, string what)
    {
        var t = Doc.P.Tracks.FirstOrDefault(x => x.Id == id); if (t == null) return;
        if (t.Vertices[vi].LockR) { Message("Promień zablokowany – odblokuj go"); RefreshTrackPanel(); return; }
        double k = Trasa.KFor(t);
        double M(double R) { var c = t.Vertices.Select(x => x.Clone()).ToList(); c[vi].R = R; var rr = Trasa.Compute(c, k); return rr.Notes.Any(n => n.Error && n.Vertex == vi) ? double.NaN : measure(rr); }
        double lo = 1, hi = 200000;
        if (!(M(hi) >= target)) hi = 1e6;
        for (int i = 0; i < 80; i++) { double m = Math.Sqrt(lo * hi), val = M(m); if (double.IsNaN(val) || val < target) lo = m; else hi = m; }
        double R = Math.Round(hi, 2);
        if (double.IsNaN(M(R)) || Math.Abs(M(R) - target) > 0.05) { Message($"Nie da się: {what} {F(target)} m – łuk się nie mieści albo kąt za mały"); RefreshTrackPanel(); return; }
        ChTrack(id, "Promień z " + what, tr => tr.Vertices[vi].R = R);
        Message($"W{vi + 1}: {what} {F(target)} m → R {F(R)} m");
    }
}

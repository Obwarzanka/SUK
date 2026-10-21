using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Łuk z opisu planu (PLAN_EDYTOR_UT.md § 5h, p. 5): na zeskanowanym planie schematycznym rysunek w poprzek nie jest w skali, ale opis
/// łuku – tak (R, L, styczna T, długość łuku Ł, kąt zwrotu α, km początku i końca). W panelu łuku wpisuje się to, co jest na planie;
/// „Zastosuj z planu” ustawia R (i L) – wprost albo dobrany do T, Ł albo km PŁ / KŁ – a porównanie plan / rysunek pokazuje, czy proste
/// są narysowane tak jak na planie (różny kąt albo km – przesuń wierzchołek albo prostą).
/// </summary>
public partial class MainWindow
{
    /// <summary>Wpisane wartości z planu dla łuku (tor, wierzchołek) – zostają po odświeżeniu panelu.</summary>
    readonly Dictionary<(string, int), Dictionary<string, string>> arcPlan = new();
    (string Id, int Vi, string Text)? arcPlanInfo;
    /// <summary>Pola „z planu” ostatnio zbudowanego panelu łuku – do testu.</summary>
    public readonly Dictionary<string, TextBox> ArcPlanBoxes = new();

    static readonly (string Key, string Label, string Tip)[] PlanFields =
    {
        ("R", "R [m]", "Promień łuku z planu"),
        ("L", "L [m]", "Długość krzywych przejściowych z planu (obie jednakowe)"),
        ("T", "styczna T [m]", "Styczna (od wierzchołka do początku krzywej) – przy braku R promień dobierze się do niej"),
        ("Ł", "długość łuku Ł [m]", "Długość części kołowej – przy braku R i T promień dobierze się do niej"),
        ("α", "kąt zwrotu α [°]", "Kąt zwrotu z planu – tylko do porównania (kąt wynika z prostych na rysunku)"),
        ("PŁ", "km początku (PŁ / PKP)", "Kilometraż linii początku krzywej (z przejściową – jej początku), np. 12.345; bez R, T i Ł promień dobierze się do niego"),
        ("KŁ", "km końca (KŁ / KKP)", "Kilometraż linii końca krzywej; do porównania albo (bez innych) do dobrania promienia"),
    };

    /// <summary>Miary łuku w wierzchołku vi z rysunku: R, L, T, Ł, α [°], km PŁ, km KŁ (null – brak kilometrażu).</summary>
    (double R, double L, double T, double Lk, double Alfa, double? KmP, double? KmK) ArcMeasures(UtTrack t, TrasaResult r, int vi)
    {
        double k = r.K, acc = 0, s0 = double.NaN, s1 = double.NaN;
        foreach (var e in r.Elements)
        {
            if (e.Vertex == vi && e.Kind != ElKind.Prosta) { if (double.IsNaN(s0)) s0 = acc / k; s1 = (acc + e.Len) / k; }
            acc += e.Len;
        }
        double alpha = Math.Abs(Math.IEEERemainder(Trasa.Az(t.Vertices[vi].P, t.Vertices[vi + 1].P) - Trasa.Az(t.Vertices[vi - 1].P, t.Vertices[vi].P), 2 * Math.PI)) * 180 / Math.PI;
        double? Km(double s) => double.IsNaN(s) ? null : Kilometraz.At(Doc.P, r.At(s).P, t.Id, s).Select(x => (double?)x.Km).FirstOrDefault();
        return (t.Vertices[vi].R, t.Vertices[vi].L, r.Tangent.TryGetValue(vi, out var tt) ? tt / k : 0,
                r.Elements.Where(e => e.Vertex == vi && e.Kind == ElKind.Luk).Sum(e => e.Len) / k, alpha, Km(s0), Km(s1));
    }

    void BuildArcPlan(UtTrack t, TrasaResult r, int vi)
    {
        var id = t.Id; ArcPlanBoxes.Clear();
        if (!arcPlan.TryGetValue((id, vi), out var vals)) arcPlan[(id, vi)] = vals = new();
        var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        int row = 0;
        foreach (var (key, label, tip) in PlanFields)
        {
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }; Grid.SetRow(l, row); g.Children.Add(l);
            var tb = new TextBox { Text = vals.TryGetValue(key, out var v) ? v : "", Margin = new Thickness(0, 1, 0, 1), ToolTip = tip };
            Grid.SetRow(tb, row); Grid.SetColumn(tb, 1); g.Children.Add(tb);
            var k2 = key; tb.TextChanged += (s, e) => vals[k2] = tb.Text;
            tb.KeyDown += (s, e) => { if (e.Key == Key.Enter) { ApplyArcPlan(id, vi); e.Handled = true; } };
            ArcPlanBoxes[key] = tb; row++;
        }
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = "Wpisz, co jest w opisie łuku na planie (puste – pomiń). R wprost albo dobrany do T, Ł albo km początku; reszta – porównanie z rysunkiem.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, FontSize = 12 });
        sp.Children.Add(g);
        var act = new WrapPanel();
        act.Children.Add(SmallBtn("Zastosuj z planu", () => ApplyArcPlan(id, vi), "Ustaw R (i L) z planu, potem porównaj plan z rysunkiem (Enter w polu)"));
        act.Children.Add(SmallBtn("Porównaj", () => CompareArcPlan(id, vi, null), "Tylko porównanie plan / rysunek, bez zmian"));
        act.Children.Add(SmallBtn("Wyczyść", () => { vals.Clear(); arcPlanInfo = null; RefreshTrackPanel(); }, "Wyczyść pola z planu"));
        sp.Children.Add(act);
        if (arcPlanInfo is var (iid, ivi, txt) && iid == id && ivi == vi)
            sp.Children.Add(new TextBlock { Text = txt, TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 4, 0, 0) });
        TrackPanel.Children.Add(new Expander { Header = "Łuk z opisu planu", Content = sp, IsExpanded = vals.Values.Any(x => x.Trim().Length > 0) || arcPlanInfo?.Id == id, Margin = new Thickness(0, 0, 0, 6) });
    }

    Dictionary<string, double> PlanValues(string id, int vi)
    {
        var res = new Dictionary<string, double>();
        if (!arcPlan.TryGetValue((id, vi), out var vals)) return res;
        foreach (var (key, txt) in vals)
        {
            if (string.IsNullOrWhiteSpace(txt)) continue;
            double? v = key is "PŁ" or "KŁ" ? Kilometraz.Parse(txt) : Num(txt);
            if (v is double d) res[key] = d;
        }
        return res;
    }

    /// <summary>„Zastosuj z planu”: L z planu, R wprost albo dobrany (T → Ł → km PŁ → km KŁ), potem porównanie.</summary>
    public void ApplyArcPlan(string id, int vi)
    {
        var t = Doc.P.Tracks.FirstOrDefault(x => x.Id == id); if (t == null || vi <= 0 || vi >= t.Vertices.Count - 1) return;
        var pv = PlanValues(id, vi);
        if (pv.Count == 0) { Message("Łuk z planu: wpisz co najmniej jedną wartość z opisu łuku"); return; }
        if (t.Vertices[vi].LockR && !pv.ContainsKey("L")) { CompareArcPlan(id, vi, "promień zablokowany – tylko porównanie"); return; }
        double k = Trasa.KFor(t);
        double L = pv.TryGetValue("L", out var lp) ? lp : t.Vertices[vi].L;
        double? R = null; string how = null;
        if (t.Vertices[vi].LockR) R = t.Vertices[vi].R;
        else if (pv.TryGetValue("R", out var rp)) { R = rp; how = "R z planu"; }
        else
        {
            foreach (var (key, name) in new[] { ("T", "styczna"), ("Ł", "długość łuku"), ("PŁ", "km początku"), ("KŁ", "km końca") })
            {
                if (!pv.TryGetValue(key, out var target)) continue;
                Func<UtTrack, TrasaResult, double> m = key switch
                {
                    "T" => (tt, rr) => rr.Tangent.TryGetValue(vi, out var q) ? q / rr.K : double.NaN,
                    "Ł" => (tt, rr) => rr.Elements.Where(e => e.Vertex == vi && e.Kind == ElKind.Luk).Sum(e => e.Len) / rr.K,
                    "PŁ" => (tt, rr) => ArcMeasures(tt, rr, vi).KmP ?? double.NaN,
                    _ => (tt, rr) => ArcMeasures(tt, rr, vi).KmK ?? double.NaN,
                };
                R = SolveR(t, vi, L, m, target);
                if (R == null) { CompareArcPlan(id, vi, $"nie da się dobrać R do: {name} {FmtPlan(key, target)} – łuk się nie mieści albo proste są narysowane inaczej niż na planie"); return; }
                how = $"R dobrany do: {name} {FmtPlan(key, target)}"; break;
            }
        }
        if (R == null && !pv.ContainsKey("L")) { CompareArcPlan(id, vi, null); return; }
        double newR = R ?? t.Vertices[vi].R;
        var test = t.Vertices.Select(x => x.Clone()).ToList(); test[vi].R = newR; test[vi].L = L; if (pv.ContainsKey("L")) test[vi].L2 = null;
        var tr = Trasa.Compute(test, k);
        if (tr.Notes.Any(n => n.Error && (n.Vertex == vi || n.Vertex == vi + 1))) { CompareArcPlan(id, vi, $"R {F(newR)} m, L {F(L)} m nie mieści się między sąsiednimi wierzchołkami – popraw proste"); return; }
        ChTrack(id, "Łuk z planu", x => { x.Vertices[vi].R = newR; x.Vertices[vi].L = L; if (pv.ContainsKey("L")) x.Vertices[vi].L2 = null; });
        View.Select(id, vi);
        CompareArcPlan(id, vi, (how ?? "L z planu") + $": R {F(newR)} m, L {F(L)} m");
    }

    /// <summary>R (przy L) dobrany tak, żeby miara łuku miała zadaną wartość: przegląd R od 20 m do 50 km i połowienie w przedziale zmiany znaku.</summary>
    static double? SolveR(UtTrack t, int vi, double L, Func<UtTrack, TrasaResult, double> m, double target)
    {
        double k = Trasa.KFor(t);
        double F(double R)
        {
            var c = new UtTrack { Id = t.Id, Vertices = t.Vertices.Select(x => x.Clone()).ToList() }; c.Vertices[vi].R = R; c.Vertices[vi].L = L; c.Vertices[vi].L2 = null;
            var rr = Trasa.Compute(c.Vertices, k);
            if (rr.Notes.Any(n => n.Error && (n.Vertex == vi || n.Vertex == vi + 1))) return double.NaN;
            return m(c, rr) - target;
        }
        double prevR = double.NaN, prevF = double.NaN;
        for (int i = 0; i <= 120; i++)
        {
            double R = 20 * Math.Pow(2500, i / 120.0), f = F(R);
            if (!double.IsNaN(f) && !double.IsNaN(prevF) && Math.Sign(f) != Math.Sign(prevF))
            {
                double lo = prevR, hi = R, flo = prevF;
                for (int j = 0; j < 60; j++) { double mid = Math.Sqrt(lo * hi), fm = F(mid); if (double.IsNaN(fm)) break; if (Math.Sign(fm) == Math.Sign(flo)) { lo = mid; flo = fm; } else hi = mid; }
                double res = Math.Round(Math.Sqrt(lo * hi), 1);
                return Math.Abs(F(res)) < 0.2 ? res : Math.Round(Math.Sqrt(lo * hi), 2);
            }
            if (f == 0) return Math.Round(R, 1);
            prevR = R; prevF = f;
        }
        return null;
    }

    static string FmtPlan(string key, double v) => key is "PŁ" or "KŁ" ? "km " + Kilometraz.Fmt(v) : key == "α" ? F(v, "0.###") + "°" : F(v) + " m";

    /// <summary>Porównanie plan / rysunek dla wpisanych wartości (tekst w panelu i w pasku).</summary>
    public void CompareArcPlan(string id, int vi, string head)
    {
        var t = Doc.P.Tracks.FirstOrDefault(x => x.Id == id); if (t == null) return;
        var r = View.Geometry(t); var pv = PlanValues(id, vi);
        var m = ArcMeasures(t, r, vi);
        var lines = new List<string>(); if (head != null) lines.Add(head);
        void Cmp(string key, string name, double? draw, double tol, string unit, string hint)
        {
            if (!pv.TryGetValue(key, out var plan)) return;
            if (draw is not double d) { lines.Add($"{name}: plan {FmtPlan(key, plan)}, rysunek – brak (tor bez kilometrażu)"); return; }
            double diff = d - plan; bool ok = Math.Abs(diff) <= tol;
            lines.Add($"{name}: plan {FmtPlan(key, plan)}, rysunek {FmtPlan(key, d)}" + (ok ? " ✓" : $" (różnica {(diff > 0 ? "+" : "−")}{F(Math.Abs(diff), "0.##")}{(unit == "°" ? "" : " ")}{unit})" + (hint != null ? " – " + hint : "")));
        }
        Cmp("R", "R", m.R, 0.5, "m", null);
        Cmp("L", "L", m.L, 0.5, "m", null);
        Cmp("T", "styczna T", m.T, 0.1, "m", "wierzchołek albo kąt inny niż na planie");
        Cmp("Ł", "długość łuku", m.Lk, 0.1, "m", null);
        Cmp("α", "kąt zwrotu", m.Alfa, 0.01, "°", "proste narysowane pod innym kątem niż na planie – przesuń sąsiedni wierzchołek");
        Cmp("PŁ", "km początku", m.KmP, 1, "m", "łuk przesunięty wzdłuż toru – przesuń wierzchołek albo sprawdź kilometraż");
        Cmp("KŁ", "km końca", m.KmK, 1, "m", "łuk przesunięty wzdłuż toru – przesuń wierzchołek albo sprawdź kilometraż");
        if (lines.Count == 0) lines.Add("Łuk z planu: wpisz wartości z opisu łuku");
        var txt = string.Join("\n", lines);
        arcPlanInfo = (id, vi, txt);
        RefreshTrackPanel();
        Message("Łuk z planu: " + string.Join("; ", lines));
    }
}

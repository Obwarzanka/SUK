using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LcsWidzew.Model;
using LcsWidzew.Sim;

namespace LcsWidzew.UI;

// ---------------------------------------------------------------------- tablica przebiegów i zależności specjalne
/// <summary>
/// Tablica przebiegów (wyznaczona z układu) z panelem zależności specjalnych zapisywanych w &lt;układ&gt;.zaleznosci.json.
/// Zależności wpisuje człowiek – program podpowiada tylko obiekty układu (lista) i sprawdza dane przed zapisem.
/// Edycja tylko na komputerze z komputerem zależności (sim != null); na stanowisku sieciowym okno jest tylko do odczytu.
/// </summary>
public class RoutesWindow : DarkWindow
{
    public class Row { public string Typ { get; set; } public string Od { get; set; } public string Do { get; set; } public string V { get; set; } public string Zwrotnice { get; set; } public string Odcinki { get; set; } public string Ochronna { get; set; } public string Zal { get; set; } public RouteDef R; }

    readonly Simulator sim;
    readonly DataGrid g = Grid();
    readonly TextBox f = Box();
    readonly TextBlock info = T("", 11, Brushes.Gray);
    List<Row> all = new();
    RouteDef cur;

    static string Ochr(RouteDef r) => r.NoOverlap ? "brak*" : r.OverlapAlts.Count > 0 ? string.Join(" | ", r.OverlapAlts.Select(a => a.Opis)) + "*" : Station.Lbl(r.Overlap ?? "");
    static string ZalText(RouteDef r) => string.Join("; ", new[]
    {
        r.Excl.Count > 0 ? "sprzeczne: " + string.Join(", ", r.Excl) : null, r.Allow.Count > 0 ? "dopuszczone: " + string.Join(", ", r.Allow) : null,
        r.Conds.Count > 0 ? "warunki: " + string.Join(", ", r.Conds) : null
    }.Where(x => x != null));

    public RoutesWindow(Dictionary<string, List<RouteDef>> table, Simulator sim = null) : base("Tablica przebiegów (z układu) i zależności specjalne", 1150, sim != null ? 820 : 600)
    {
        this.sim = sim;
        Col(g, "Typ", "Typ", 60); Col(g, "Od", "Od", 70); Col(g, "Do", "Do", 70); Col(g, "V [km/h]", "V", 60); Col(g, "Zwrotnice", "Zwrotnice", 260); Col(g, "Odcinki", "Odcinki"); Col(g, "Dr. ochr.", "Ochronna", 120); Col(g, "Zależności specjalne", "Zal", 220);
        f.ToolTip = "Filtr (np. S1, W17_1)";
        f.TextChanged += (a, b) => Filter();
        Fill(table);
        var dock = new DockPanel { Margin = new Thickness(8), Background = Background };
        var top = new StackPanel(); top.Children.Add(info); top.Children.Add(f); DockPanel.SetDock(top, Dock.Top); dock.Children.Add(top);
        if (sim != null) { var ed = Editor(); DockPanel.SetDock(ed, Dock.Bottom); dock.Children.Add(ed); g.SelectionChanged += (a, b) => Select((g.SelectedItem as Row)?.R); }
        dock.Children.Add(g); Content = dock;
    }

    void Fill(Dictionary<string, List<RouteDef>> table)
    {
        all = table.OrderBy(k => k.Key).SelectMany(k => k.Value).Select(r => new Row
        {
            R = r, Typ = (r.Type == "P" ? "poc." : "man.") + (r.Variant > 0 ? " w" + r.Variant : ""), Od = r.Start, Do = Station.Lbl(r.End), V = $"{r.VMax:0}{(r.VSet > 0 ? "*" : "")}",
            Zwrotnice = r.SwText + (r.Ders.Count > 0 ? " " + string.Join(" ", r.Ders.Select(Station.Lbl)) : ""), Odcinki = string.Join(" ", r.Secs.Select(Station.Lbl)), Ochronna = Ochr(r), Zal = ZalText(r)
        }).ToList();
        info.Text = $"{all.Count} przebiegów. * – z zależności specjalnych" + (sim != null ? $" (plik {System.IO.Path.GetFileName(sim.L.Zal.Plik ?? "–")}). Wybierz przebieg, by edytować." : ".") + " Filtr:";
        Filter();
    }

    /// <summary>Tryb zrzutu: filtr i wybór pierwszego pasującego przebiegu.</summary>
    public void Pokaz(string filtr)
    {
        f.Text = filtr; UpdateLayout();
        var row = g.Items.Cast<Row>().FirstOrDefault(r => r.Zal != "" || r.Ochronna.EndsWith('*')) ?? g.Items.Cast<Row>().FirstOrDefault();
        if (row != null) { g.SelectedItem = row; g.ScrollIntoView(row); }
    }

    void Filter() => g.ItemsSource = all.Where(r => f.Text == "" || (r.Od + " " + r.Do).Contains(f.Text, StringComparison.OrdinalIgnoreCase)).ToList();

    // ------------------------------------------------------------------ edytor
    sealed class ListEd
    {
        public ListBox List; public ComboBox Pick;
        public List<string> Items => List.Items.Cast<string>().ToList();
        public void Set(IEnumerable<string> v) { List.Items.Clear(); foreach (var x in v) List.Items.Add(x); }
    }

    readonly TextBlock edTitle = T("Wybierz przebieg z tablicy.", 13, Brushes.White, true);
    readonly TextBlock edStatus = T("", 11, Brushes.Gray);
    readonly CheckBox onlyVar = new() { Content = "tylko ten wariant (położenia zwrotnic)", Foreground = Brushes.White, Margin = new Thickness(0, 0, 16, 0) };
    readonly CheckBox noOv = new() { Content = "bez drogi ochronnej", Foreground = Brushes.White };
    ListEd edOv, edExcl, edAllow, edFlAdd, edFlDel, edCond;
    FrameworkElement edBody;

    ListEd MakeList(WrapPanel host, string title, string tip)
    {
        var sp = new StackPanel { Width = 270, Margin = new Thickness(0, 0, 10, 6) };
        sp.Children.Add(T(title, 11.5, Brushes.White, true));
        var e = new ListEd
        {
            List = new ListBox { Height = 88, Background = Brushes.Black, Foreground = Brushes.White, BorderBrush = Brushes.DimGray, FontFamily = new FontFamily("Consolas"), FontSize = 11.5 },
            Pick = new ComboBox { IsEditable = true, IsTextSearchEnabled = true, Margin = new Thickness(0, 3, 0, 0), ToolTip = tip, FontFamily = new FontFamily("Consolas") }
        };
        sp.Children.Add(e.List); sp.Children.Add(e.Pick);
        var bp = new StackPanel { Orientation = Orientation.Horizontal };
        bp.Children.Add(Btn("Dodaj", (a, b) => { var t = (e.Pick.Text ?? "").Trim(); if (t != "" && !e.Items.Contains(t)) e.List.Items.Add(Zaleznosci.Norm(t)); }));
        bp.Children.Add(Btn("Usuń", (a, b) => { if (e.List.SelectedItem != null) e.List.Items.Remove(e.List.SelectedItem); }));
        sp.Children.Add(bp);
        host.Children.Add(sp);
        return e;
    }

    FrameworkElement Editor()
    {
        var sp = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        sp.Children.Add(edTitle);
        var body = new StackPanel();
        var opt = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) }; opt.Children.Add(onlyVar); opt.Children.Add(noOv); body.Children.Add(opt);
        var wp = new WrapPanel();
        edOv = MakeList(wp, "Droga ochronna – warianty", "Wariant: odcinki i zwrotnice z położeniem oddzielone spacją, np. MT:Z1/2 MT:2+. Kolejność = pierwszeństwo.");
        edExcl = MakeList(wp, "Przebiegi sprzeczne (dodatkowe)", "Przebieg, z którym ten się wyklucza (obustronnie)");
        edAllow = MakeList(wp, "Przebiegi dopuszczone jednocześnie", "Przebieg, który może być nastawiony razem z tym mimo wspólnego odcinka");
        edFlAdd = MakeList(wp, "Ochrona boczna – dodaj", "Zwrotnica z położeniem (np. MT:4+), wykolejnica albo sygnalizator („Stój”)");
        edFlDel = MakeList(wp, "Ochrona boczna – usuń", "Element ochrony bocznej wyznaczonej z układu, który nie ma obowiązywać");
        edCond = MakeList(wp, "Warunki dodatkowe", "MT:3+ (zwrotnica w położeniu), MT:T1 wolny, MT:W1 założona / zdjęta, MT:B stój");
        body.Children.Add(wp);
        var bp = new StackPanel { Orientation = Orientation.Horizontal };
        bp.Children.Add(Btn("Zapisz zależności", (a, b) => Save(), new SolidColorBrush(Color.FromRgb(0x1d, 0x4d, 0x2a))));
        bp.Children.Add(Btn("Przywróć", (a, b) => Select(cur)));
        bp.Children.Add(Btn("Przelicz tablicę", (a, b) => Recalc()));
        bp.Children.Add(Btn("Sprawdź plik", (a, b) => { var uw = Check(); edStatus.Text = uw.Count == 0 ? "Zależności specjalne bez uwag." : string.Join(Environment.NewLine, uw.Take(8)); }));
        body.Children.Add(bp);
        body.Children.Add(edStatus);
        edBody = body; body.IsEnabled = false;
        sp.Children.Add(body);
        return new Border { BorderBrush = Brushes.DimGray, BorderThickness = new Thickness(0, 1, 0, 0), Child = sp };
    }

    IEnumerable<string> Keys => sim.RouteTable.Values.SelectMany(v => v).Select(r => r.Key).Distinct().OrderBy(x => x);
    IEnumerable<string> SwPos => sim.L.Switches.Keys.OrderBy(x => x.Length).ThenBy(x => x).SelectMany(n => new[] { n + "+", n + "-" });

    ZalPrzebieg Entry(RouteDef r) => sim.L.Zal.Przebiegi.FirstOrDefault(p => p.Type == r.Type && p.Start == r.Start && p.End == r.End && p.Sw == r.SwKey)
                                     ?? sim.L.Zal.Przebiegi.FirstOrDefault(p => p.Type == r.Type && p.Start == r.Start && p.End == r.End && p.Sw == "");

    void Select(RouteDef r)
    {
        cur = r; edBody.IsEnabled = r != null;
        if (r == null) { edTitle.Text = "Wybierz przebieg z tablicy."; return; }
        edTitle.Text = $"Zależności specjalne: {r.Key}" + (r.Variant > 0 ? $" (wariant {r.Variant}: {r.SwText})" : "");
        var z = Entry(r) ?? new ZalPrzebieg();
        onlyVar.IsChecked = !string.IsNullOrEmpty(z.Sw); noOv.IsChecked = z.BezOchronnej;
        edOv.Set(z.DrogaOchronna.Select(w => string.Join(" ", w.Odcinki.Concat(w.Zwrotnice))));
        edExcl.Set(z.Sprzeczne); edAllow.Set(z.Dopuszczone); edFlAdd.Set(z.OchronaDodaj); edFlDel.Set(z.OchronaUsun); edCond.Set(z.Warunki);
        // podpowiedzi – obiekty układu (droga ochronna z układu na początku listy)
        var L = sim.L; var ovSug = new List<string>();
        if (r.Overlap != null)
        {
            var sws = L.Switches.Values.Where(w => w.Sec == r.Overlap).Select(w => w.Num).ToList();
            ovSug.Add(r.Overlap); ovSug.AddRange(sws.SelectMany(n => new[] { $"{r.Overlap} {n}+", $"{r.Overlap} {n}-" }));
        }
        edOv.Pick.ItemsSource = ovSug.Concat(L.Sections.Keys.OrderBy(x => x)).Distinct().ToList();
        var others = Keys.Where(k => k != r.Key).ToList();
        edExcl.Pick.ItemsSource = others; edAllow.Pick.ItemsSource = others;
        edFlAdd.Pick.ItemsSource = SwPos.Concat(L.Derailers.Keys.OrderBy(x => x)).Concat(L.Signals.Values.Where(s => s.Type != "odstepowy").Select(s => s.Name).OrderBy(x => x)).ToList();
        edFlDel.Pick.ItemsSource = r.Flank.Select(fl => fl.Kind == "sw" ? fl.Id + fl.Pos : fl.Id).ToList();
        edCond.Pick.ItemsSource = SwPos.Concat(L.Sections.Keys.OrderBy(x => x).Select(s => s + " wolny"))
            .Concat(L.Derailers.Keys.SelectMany(d => new[] { d + " założona", d + " zdjęta" })).Concat(L.Signals.Values.Where(s => s.Type != "odstepowy").Select(s => s.Name + " stój")).ToList();
        edStatus.Text = r.Flank.Count > 0 ? "Ochrona boczna z układu: " + r.FlankText : "";
    }

    List<string> Check() => sim.L.Zal.Sprawdz(sim.L, Keys);

    void Save()
    {
        if (cur == null) return;
        var r = cur; var Z = sim.L.Zal;
        var z = new ZalPrzebieg { Start = r.Start, End = r.End, Type = r.Type, Sw = onlyVar.IsChecked == true ? r.SwKey : "", BezOchronnej = noOv.IsChecked == true };
        foreach (var v in edOv.Items)
        {
            var w = new ZalOchronna();
            foreach (var t in v.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                if (t.Length > 1 && t[^1] is '+' or '-' && sim.L.Switches.ContainsKey(t[..^1])) w.Zwrotnice.Add(t); else w.Odcinki.Add(t);
            z.DrogaOchronna.Add(w);
        }
        z.Sprzeczne = edExcl.Items; z.Dopuszczone = edAllow.Items; z.OchronaDodaj = edFlAdd.Items; z.OchronaUsun = edFlDel.Items; z.Warunki = edCond.Items;
        var old = Z.Przebiegi.ToList();
        Z.Przebiegi.RemoveAll(p => p.Type == z.Type && p.Start == z.Start && p.End == z.End && p.Sw == z.Sw);
        if (!z.Pusty) Z.Przebiegi.Add(z);
        var uw = Check();
        if (uw.Count > 0 && MessageBox.Show(this, "Uwagi do zależności specjalnych:" + Environment.NewLine + string.Join(Environment.NewLine, uw.Take(12)) + Environment.NewLine + Environment.NewLine + "Zapisać mimo to?", "Zależności specjalne", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        { Z.Przebiegi.Clear(); Z.Przebiegi.AddRange(old); return; }
        try { Z.Zapisz(); }
        catch (Exception ex) { Z.Przebiegi.Clear(); Z.Przebiegi.AddRange(old); edStatus.Text = "Nie zapisano: " + ex.Message; return; }
        edStatus.Text = $"Zapisano {System.IO.Path.GetFileName(Z.Plik)} (poprzednia wersja w .bak). " + Recalc();
    }

    string Recalc()
    {
        var why = sim.PrzeliczTablice();
        var msg = why ?? "Tablica przebiegów przeliczona.";
        if (why == null) { var key = cur?.Key; var sw = cur?.SwKey; Fill(sim.RouteTable); var row = all.FirstOrDefault(x => x.R.Key == key && x.R.SwKey == sw); if (row != null) { g.SelectedItem = row; g.ScrollIntoView(row); } }
        edStatus.Text = msg;
        return msg;
    }
}

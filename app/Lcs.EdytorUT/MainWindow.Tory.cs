using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Tory w oknie: narzędzie Tor (domyślne R i L nowych wierzchołków na pasku), panel zaznaczonego toru
/// (numer, kategoria, elektryfikacja, Vmax, elementy trasy z długościami w terenie, R i L w wierzchołkach, położenie wierzchołka,
/// tor równoległy, odwrócenie kierunku, usuwanie).
/// </summary>
public partial class MainWindow
{
    public readonly TextBox NewRBox = new() { Width = 52, Text = "300", VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Promień łuku w nowych wierzchołkach [m] (0 – bez łuku)" };
    public readonly TextBox NewLBox = new() { Width = 40, Text = "0", VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Długość krzywych przejściowych (parabola 3°) w nowych wierzchołkach [m] (0 – bez)" };
    public readonly System.Windows.Controls.Primitives.ToggleButton ProstaBtn = new() { Content = "─ Prosta", IsChecked = true, Padding = new Thickness(4, 2, 4, 2), ToolTip = "Tor: prosta na przedłużeniu końca (Tab – łuk); Alt – wolny kierunek" },
        LukBtn = new() { Content = "◠ Łuk", Padding = new Thickness(4, 2, 4, 2), ToolTip = "Tor: łuk styczny o promieniu R, strona i kąt za kursorem (Tab – prosta); Shift – koniec równoległy do osi schematu" };
    public readonly StackPanel TrackPanel = new() { Margin = new Thickness(0, 0, 0, 6) };
    static readonly CultureInfo Pl = CultureInfo.GetCultureInfo("pl-PL");

    void InitTory()
    {
        View.TrackDrawn += OnTrackDrawn;
        View.VertexMoved += (id, i, p) =>
        {
            // wierzchołek na skosie rozjazdu: zmienia się długość prostej skosu (rozjazd ustawia wierzchołek w Sync)
            if (View.SkewTurnout(id, i) is UtTurnout rz && Rozjazdy.OnSkew(Doc.P, rz, p) is var (_, len))
            {
                Doc.Change("Długość skosu za rozjazdem", d => { var x = d.Turnouts.First(q => q.Id == rz.Id); x.BranchLen = Math.Round(len, 3); });
                Message($"Skos za rozjazdem {rz.Number}: {F(len)} m od środka rozjazdu (kąt 1:{F(Katalog.Find(rz.Type)?.N ?? 0)} zachowany)");
            }
            else ChTrack(id, "Przesunięcie wierzchołka", t => { t.Vertices[i].X = p.X; t.Vertices[i].Y = p.Y; });
        };
        View.VertexInserted += (id, i, p) =>
        {
            ChTrack(id, "Nowy wierzchołek", t => t.Vertices.Insert(i, new UtVertex { X = p.X, Y = p.Y, R = View.NewR, L = View.NewL }));
            View.Select(id, i);
        };
        View.ArcRadiusChanged += (id, i, r) => { ChTrack(id, "Promień łuku", t => t.Vertices[i].R = r); Message($"Łuk W{i + 1}: R {F(r)} m"); };
        View.ModeChanged += () => { ProstaBtn.IsChecked = View.Mode == UtView.TorMode.Prosta; LukBtn.IsChecked = View.Mode == UtView.TorMode.Luk; if (View.Tool == UtTool.Tor) ToolShown(); };
        ProstaBtn.Click += (s, e) => { SetTool(UtTool.Tor); View.Mode = UtView.TorMode.Prosta; };
        LukBtn.Click += (s, e) => { SetTool(UtTool.Tor); View.Mode = UtView.TorMode.Luk; };
        View.ParallelDrawn += (src, vs, d) =>
        {
            var st = Doc.P.Tracks.FirstOrDefault(x => x.Id == src);
            var n = new UtTrack { Vertices = vs, Elec = st?.Elec ?? false };
            Doc.Change("Tor równoległy", p => p.Tracks.Add(n));
            View.Select(n.Id);
            Message($"Tor równoległy {F(Math.Abs(d))} m po {(d > 0 ? "prawej" : "lewej")} toru {st?.Number}: długość {UtView.Len3(View.Geometry(n).Length)}");
        };
        View.SelectionChanged += RefreshTrackPanel;
        View.DraftInfo += m => { if (m.Length > 0) Message(m); };
        void Defaults()
        {
            if (Num(NewRBox.Text) is double r && r >= 0) View.NewR = r;
            if (Num(NewLBox.Text) is double l && l >= 0) View.NewL = l;
            View.InvalidateVisual();
        }
        NewRBox.TextChanged += (s, e) => Defaults(); NewLBox.TextChanged += (s, e) => Defaults();
    }

    static double? Num(string s) => double.TryParse((s ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    static string F(double v, string fmt = "0.##") => v.ToString(fmt, Pl);

    void ChTrack(string id, string what, Action<UtTrack> act) =>
        Doc.Change(what, p => { var t = p.Tracks.FirstOrDefault(x => x.Id == id); if (t != null) act(t); });

    /// <summary>Tor z narzędzia Tor: nowy, przedłużony (cały wielobok) albo połączony z innym (tamten wchłonięty).</summary>
    void OnTrackDrawn(List<UtVertex> vs, string replace, string delete)
    {
        string id = replace, what;
        if (replace != null)
        {
            what = delete != null ? "Połączenie torów" : "Przedłużenie toru";
            Doc.Change(what, p =>
            {
                var t = p.Tracks.First(x => x.Id == replace); t.Vertices = vs;
                if (delete != null) p.Tracks.RemoveAll(x => x.Id == delete);
            });
        }
        else
        {
            var t = new UtTrack { Vertices = vs };
            id = t.Id; what = "Nowy tor";
            Doc.Change(what, p => p.Tracks.Add(t));
        }
        View.Select(id);
        var tr = Doc.P.Tracks.First(x => x.Id == id); var r = View.Geometry(tr);
        Message($"{what}: {tr.Vertices.Count} wierzchołków, długość {UtView.Len3(r.Length)} w terenie" + (r.Notes.Count > 0 ? " – " + string.Join("; ", r.Notes.Select(n => $"W{n.Vertex + 1}: {n.Text}")) : ""));
    }

    /// <summary>Del: zaznaczony wierzchołek (gdy zostają co najmniej 2) albo cały tor.</summary>
    public bool DeleteSelection()
    {
        if (DeleteTurnoutOrCrossing()) return true;
        var id = View.SelectedTrack; var t = Doc.P.Tracks.FirstOrDefault(x => x.Id == id); if (t == null) return false;
        int i = View.SelectedVertex;
        if (i >= 0 && View.Locked(id, i)) { Message("Ten wierzchołek ustawia rozjazd – usuń rozjazd albo przesuń go"); return true; }
        if (i >= 0 && View.SkewTurnout(id, i) != null) { Message("Ten wierzchołek leży na skosie rozjazdu – można go tylko przesuwać wzdłuż skosu"); return true; }
        if (Doc.P.Turnouts.Any(r => r.Branch == id) && (i < 0 || t.Vertices.Count <= 3)) { Message("To odgałęzienie rozjazdu – usuń rozjazd (zaznacz go i Del)"); return true; }
        if (i >= 0 && t.Vertices.Count > 2) { ChTrack(id, "Usunięcie wierzchołka", x => x.Vertices.RemoveAt(i)); View.Select(id); Message($"Usunięty wierzchołek W{i + 1}"); }
        else
        {
            // z torem znikają jego rozjazdy (i skrzyżowania): odgałęzienia do skosu i tory łączące łączników – też; odgałęzienia
            // przedłużone (bocznice) zostają jako zwykłe tory
            var rz = Doc.P.Turnouts.Where(r => r.Track == id).ToList();
            var gone = new HashSet<string> { id }; var kept = new List<string>();
            foreach (var r in rz)
            {
                var br = Doc.P.Tracks.FirstOrDefault(x => x.Id == r.Branch); if (br == null) continue;
                if (r.Partner != null || br.Vertices.Count <= 3) gone.Add(br.Id); else kept.Add(r.Number);
            }
            Doc.Change("Usunięcie toru", p => p.Tracks.RemoveAll(x => gone.Contains(x.Id)));
            View.Select(null);
            Message(rz.Count == 0 ? "Tor usunięty (Ctrl+Z przywraca)"
                : $"Tor usunięty z rozjazdami {string.Join(", ", rz.Select(r => r.Number))}" + (kept.Count > 0 ? $"; odgałęzienia rozjazdów {string.Join(", ", kept)} zostały jako zwykłe tory" : "") + " (Ctrl+Z przywraca)");
        }
        return true;
    }

    // ------------------------------------------------------------------ panel toru
    void RefreshTrackPanel()
    {
        TrackPanel.Children.Clear();
        if (BuildObjPanel()) return;
        var id = View.SelectedTrack;
        var t = Doc?.P.Tracks.FirstOrDefault(x => x.Id == id);
        if (t == null) return;   // np. po cofnięciu – zaznaczenie zostaje, ponowienie przywraca panel
        var r = View.Geometry(t);
        TrackPanel.Children.Add(Header(t.Number.Length > 0 ? $"Tor {t.Number}" : "Tor (bez numeru)"));

        var num = new TextBox { Text = t.Number, Margin = new Thickness(0, 2, 0, 6) };
        void CommitNum() { var n = num.Text.Trim(); if (n != t.Number) ChTrack(id, "Numer toru", x => x.Number = n); }
        num.LostFocus += (s, e) => CommitNum(); num.KeyDown += (s, e) => { if (e.Key == Key.Enter) CommitNum(); };
        TrackPanel.Children.Add(Label("Numer toru")); TrackPanel.Children.Add(num);

        var cat = new ComboBox { Margin = new Thickness(0, 2, 0, 6) };
        var cats = new[] { ("", "boczny"), (UtTrack.MainD, "główny dodatkowy"), (UtTrack.MainZ, "główny zasadniczy") };
        foreach (var (_, n) in cats) cat.Items.Add(n);
        cat.SelectedIndex = Math.Max(0, Array.FindIndex(cats, c => c.Item1 == t.Cat));
        cat.SelectionChanged += (s, e) => ChTrack(id, "Kategoria toru", x => x.Cat = cats[cat.SelectedIndex].Item1);
        TrackPanel.Children.Add(Label("Kategoria (Ig-10: grubość osi)")); TrackPanel.Children.Add(cat);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        var elec = new CheckBox { Content = "zelektryfikowany", IsChecked = t.Elec, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        elec.Click += (s, e) => ChTrack(id, "Elektryfikacja toru", x => x.Elec = elec.IsChecked == true);
        var vmax = new TextBox { Text = t.Vmax > 0 ? t.Vmax.ToString() : "", Width = 44, ToolTip = "Prędkość największa [km/h]" };
        void CommitV() { int v = (int)(Num(vmax.Text) ?? 0); if (v != t.Vmax && v >= 0) ChTrack(id, "Prędkość toru", x => x.Vmax = v); }
        vmax.LostFocus += (s, e) => CommitV(); vmax.KeyDown += (s, e) => { if (e.Key == Key.Enter) CommitV(); };
        row.Children.Add(elec); row.Children.Add(new TextBlock { Text = "Vmax ", VerticalAlignment = VerticalAlignment.Center }); row.Children.Add(vmax); row.Children.Add(new TextBlock { Text = " km/h", VerticalAlignment = VerticalAlignment.Center });
        TrackPanel.Children.Add(row);

        TrackPanel.Children.Add(new TextBlock
        {
            Text = $"Długość {UtView.Len3(r.Length)} w terenie\n(w układzie {UtView.Len3(r.LenGrid)}, skala odwzorowania {r.K.ToString("0.000000", CultureInfo.InvariantCulture)})",
            TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6),
        });

        // elementy trasy
        var els = new List<string>();
        foreach (var e in r.Elements)
        {
            double len = e.Len / r.K;
            els.Add(e.Kind switch
            {
                ElKind.Prosta => $"prosta {F(len)} m",
                ElKind.Przejsciowa => $"przejściowa L = {F(e.Lx / r.K)} m ({F(len)} m)",
                _ => $"łuk {(e.R > 0 ? "w prawo" : "w lewo")} R = {F(Math.Abs(e.R) / r.K)} m: {F(len)} m ({F(e.Len / Math.Abs(e.R) * 180 / Math.PI, "0.###")}°)",
            });
        }
        TrackPanel.Children.Add(new TextBlock { Text = string.Join("\n", els), FontSize = 12, Margin = new Thickness(0, 0, 0, 4), TextWrapping = TextWrapping.Wrap });
        foreach (var (v, text, err) in r.Notes)
            TrackPanel.Children.Add(new TextBlock { Text = $"W{v + 1}: {text}", Foreground = err ? Brushes.Firebrick : Brushes.DarkOrange, TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 0, 0, 2) });

        // wierzchołki: R i L
        TrackPanel.Children.Add(Label("Wierzchołki – w terenie [m]"));
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 6) };
        foreach (var w in new[] { 46.0, 70, 60, 70 }) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        foreach (var (txt, col) in new[] { ("promień R", 1), ("przejściowa L", 3) })
        {
            var h = new TextBlock { Text = txt, FontSize = 11, Foreground = Brushes.DimGray };
            Grid.SetColumn(h, col); if (col == 3) { Grid.SetColumn(h, 2); Grid.SetColumnSpan(h, 2); h.Margin = new Thickness(18, 0, 0, 0); }
            grid.Children.Add(h);
        }
        for (int i = 0; i < t.Vertices.Count; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            int vi = i, gr = i + 1; var v = t.Vertices[i];
            var lbl = new TextBlock { Text = $"W{i + 1}", VerticalAlignment = VerticalAlignment.Center, FontWeight = i == View.SelectedVertex ? FontWeights.Bold : FontWeights.Normal, Cursor = Cursors.Hand };
            lbl.MouseLeftButtonUp += (s, e) => { View.Select(id, vi); View.SetView(v.P, View.Mpp); };
            Grid.SetRow(lbl, gr); grid.Children.Add(lbl);
            if (i == 0 || i == t.Vertices.Count - 1)
            {
                var end = new TextBlock { Text = i == 0 ? "początek" : "koniec", Foreground = Brushes.DimGray, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetRow(end, gr); Grid.SetColumn(end, 1); Grid.SetColumnSpan(end, 3); grid.Children.Add(end);
                continue;
            }
            TextBox Field(double val, int col, string tip, Action<UtVertex, double> set)
            {
                var tb = new TextBox { Text = val > 0 ? F(val, "0.###") : "0", Margin = new Thickness(0, 1, 4, 1), ToolTip = tip };
                void Commit() { if (Num(tb.Text) is double x && x >= 0 && Math.Abs(x - (col == 1 ? t.Vertices[vi].R : t.Vertices[vi].L)) > 1e-9) ChTrack(id, col == 1 ? "Promień łuku" : "Krzywa przejściowa", tr => set(tr.Vertices[vi], x)); }
                tb.LostFocus += (s, e) => Commit(); tb.KeyDown += (s, e) => { if (e.Key == Key.Enter) Commit(); };
                Grid.SetRow(tb, vi + 1); Grid.SetColumn(tb, col); grid.Children.Add(tb); return tb;
            }
            Field(v.R, 1, $"W{i + 1}: promień łuku [m]", (x, val) => x.R = val);
            Field(v.L, 3, $"W{i + 1}: długość krzywych przejściowych [m]", (x, val) => x.L = val);
            var lt = new TextBlock { Text = "L", VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 6, 0) };
            Grid.SetRow(lt, gr); Grid.SetColumn(lt, 2); grid.Children.Add(lt);
        }
        TrackPanel.Children.Add(grid);

        // położenie zaznaczonego wierzchołka
        if (View.SelectedVertex >= 0 && View.SelectedVertex < t.Vertices.Count)
        {
            int vi = View.SelectedVertex; var v = t.Vertices[vi];
            TrackPanel.Children.Add(Label($"Wierzchołek W{vi + 1}: X (północ), Y (wschód) [m]"));
            var xy = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 6) };
            var xb = new TextBox { Text = v.X.ToString("0.000", CultureInfo.InvariantCulture), Width = 110, Margin = new Thickness(0, 0, 4, 0) };
            var yb = new TextBox { Text = v.Y.ToString("0.000", CultureInfo.InvariantCulture), Width = 110 };
            void CommitXY() { if (Num(xb.Text) is double x && Num(yb.Text) is double y && (Math.Abs(x - v.X) > 1e-6 || Math.Abs(y - v.Y) > 1e-6)) ChTrack(id, "Położenie wierzchołka", tr => { tr.Vertices[vi].X = x; tr.Vertices[vi].Y = y; }); }
            xb.KeyDown += (s, e) => { if (e.Key == Key.Enter) CommitXY(); }; yb.KeyDown += (s, e) => { if (e.Key == Key.Enter) CommitXY(); };
            xy.Children.Add(xb); xy.Children.Add(yb); TrackPanel.Children.Add(xy);
        }

        // tor równoległy
        TrackPanel.Children.Add(Label("Tor równoległy – międzytorze [m]"));
        var par = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 6) };
        var dist = new TextBox { Text = "4,75", Width = 50, Margin = new Thickness(0, 0, 4, 0), ToolTip = "Odległość osi torów [m]" };
        par.Children.Add(dist);
        par.Children.Add(SmallBtn("← po lewej", () => Parallel(id, -(Num(dist.Text) ?? 4.75)), "Nowy tor po lewej stronie (patrząc w kierunku toru)"));
        par.Children.Add(SmallBtn("po prawej →", () => Parallel(id, Num(dist.Text) ?? 4.75), "Nowy tor po prawej stronie (patrząc w kierunku toru)"));
        TrackPanel.Children.Add(par);

        var act = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        act.Children.Add(SmallBtn("Odwróć kierunek", () => ChTrack(id, "Odwrócenie kierunku toru", x => x.Vertices.Reverse()), "Początek toru na drugim końcu (kilometraż toru od drugiej strony)"));
        act.Children.Add(SmallBtn("Pokaż", () => FitTrack(t), "Cały tor w oknie"));
        act.Children.Add(SmallBtn("Usuń tor", () => { View.Select(id); DeleteSelection(); }, "Usuń tor (Del)"));
        TrackPanel.Children.Add(act);
        TrackPanel.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 10) });
    }

    /// <summary>Tor równoległy w odległości d [m]: + po prawej, − po lewej.</summary>
    public UtTrack Parallel(string id, double d)
    {
        var t = Doc.P.Tracks.FirstOrDefault(x => x.Id == id); if (t == null || Math.Abs(d) < 0.01) return null;
        var n = new UtTrack { Vertices = Trasa.Offset(t, d), Cat = "", Elec = t.Elec, Vmax = 0 };
        Doc.Change("Tor równoległy", p => p.Tracks.Add(n));
        View.Select(n.Id);
        Message($"Tor równoległy w odległości {F(Math.Abs(d))} m po {(d > 0 ? "prawej" : "lewej")}");
        return n;
    }

    void FitTrack(UtTrack t)
    {
        View.FitPoints(View.Geometry(t).Sample(1).Select(q => q.P).ToList());
    }
}

using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Rozjazdy w oknie: typ z katalogu na pasku (Dane/rozjazdy.json), numery wpisuje projektant (okno po wskazaniu miejsca),
/// panel rozjazdu (numer, typ, położenie zasadnicze, km PR, kierunek, strona, dane typu, uwagi) i skrzyżowania.
/// </summary>
public partial class MainWindow
{
    public readonly ComboBox RzTypeBox = new() { Width = 170, ToolTip = "Typ rozjazdu z katalogu (Dane/rozjazdy.json)" };
    /// <summary>Test: numery zamiast okna (null – kolejne wolne).</summary>
    public Func<string[], string[]> AskNumbersForTest;

    public SymbolsWindow SymWin { get; private set; }
    public SymbolsWindow OpenSymbols()
    {
        if (SymWin == null) { SymWin = new SymbolsWindow(this, () => View.InvalidateVisual()) { ShowActivated = !Test.Running }; SymWin.Closed += (s, e) => SymWin = null; SymWin.Show(); }
        else SymWin.Activate();
        return SymWin;
    }

    void InitRozjazdy()
    {
        try { Katalog.Load(Path.Combine(AppContext.BaseDirectory, "Dane", "rozjazdy.json")); } catch (Exception ex) { Message("Katalog rozjazdów: " + ex.Message + " – dane domyślne"); }
        try { Symbole.Load(Path.Combine(AppContext.BaseDirectory, "Dane", "symbole_ig10.json")); } catch (Exception ex) { Message("Symbole Ig-10: błąd w pliku – " + ex.Message); }
        foreach (var t in Katalog.Types) RzTypeBox.Items.Add(t.Name);
        RzTypeBox.SelectedIndex = 0; View.RzType = Katalog.Types[0].Name;
        RzTypeBox.SelectionChanged += (s, e) => { if (RzTypeBox.SelectedItem is string n) { View.RzType = n; if (View.Tool == UtTool.Rozjazd) ToolShown(); } };
        View.TurnoutPlace += OnTurnoutPlace;
        View.CrossoverJoin += (aId, bId) =>
        {
            string err = null; double shift = 0;
            var a = Doc.P.Turnouts.First(x => x.Id == aId); var b = Doc.P.Turnouts.First(x => x.Id == bId);
            Doc.Change("Łącznik z dwóch rozjazdów", p => err = Rozjazdy.MakeCrossover(p, aId, bId, out shift));
            if (err != null) { Message($"Łącznik {a.Number}/{b.Number}: {err}"); return; }
            View.SelectTurnout(aId);
            Message($"Łącznik {a.Number}/{b.Number}: rozjazd {b.Number} przesunięty o {F(shift)} m, żeby skosy się zeszły" + Notes(b));
        };
        View.CrossingPlace += OnCrossingPlace;
        View.TurnoutMoved += (id, s) =>
        {
            var t = Doc.P.Turnouts.First(x => x.Id == id);
            // drugi rozjazd łącznika trzyma się przecięcia – przesuwa się cały łącznik (rozjazd pierwszy)
            var master = t.BranchAtEnd && t.Partner != null ? Doc.P.Turnouts.First(x => x.Id == t.Partner) : t;
            double delta = s - t.S;
            Doc.Change("Przesunięcie rozjazdu", p => { var m = p.Turnouts.First(x => x.Id == master.Id); m.S += delta; });
            Message($"Rozjazd {t.Number} przesunięty o {F(Math.Abs(delta))} m");
        };
    }

    /// <summary>Numery z okna (domyślnie kolejne wolne); null – rezygnacja.</summary>
    string[] AskNumbers(string title, string[] labels)
    {
        var def = labels.Select((_, i) => Rozjazdy.NextNumber(Doc.P, i).ToString()).ToArray();
        if (AskNumbersForTest != null) return AskNumbersForTest(def) ?? def;
        if (NoPrompt) return def;
        var dlg = new Window { Title = title, Owner = this, SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, FontFamily = FontFamily, FontSize = 13 };
        var sp = new StackPanel { Margin = new Thickness(14) };
        var boxes = new List<TextBox>();
        for (int i = 0; i < labels.Length; i++)
        {
            sp.Children.Add(new TextBlock { Text = labels[i], Margin = new Thickness(0, i == 0 ? 0 : 8, 0, 2) });
            var tb = new TextBox { Text = def[i], Width = 160 }; boxes.Add(tb); sp.Children.Add(tb);
        }
        var ok = new Button { Content = "OK", IsDefault = true, Width = 80, Margin = new Thickness(0, 12, 6, 0) };
        var cancel = new Button { Content = "Anuluj", IsCancel = true, Width = 80, Margin = new Thickness(0, 12, 0, 0) };
        ok.Click += (s, e) => dlg.DialogResult = true;
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; row.Children.Add(ok); row.Children.Add(cancel);
        sp.Children.Add(row); dlg.Content = sp;
        dlg.Loaded += (s, e) => { boxes[0].Focus(); boxes[0].SelectAll(); };
        return dlg.ShowDialog() == true ? boxes.Select(b => b.Text.Trim()).ToArray() : null;
    }

    bool NumbersOk(string[] nums)
    {
        foreach (var n in nums)
        {
            if (n.Length == 0) { Message("Wpisz numer rozjazdu"); return false; }
            if (Rozjazdy.NumberUsed(Doc.P, n)) { Message($"Numer {n} jest już zajęty – rozjazd nie został wstawiony"); return false; }
        }
        if (nums.Distinct().Count() != nums.Length) { Message("Numery zwrotnic łącznika muszą być różne"); return false; }
        return true;
    }

    void OnTurnoutPlace(UtView.RzPlace pl)
    {
        var (track, s, dir, side, target) = (pl.Track, pl.S, pl.Dir, pl.Side, pl.Target);
        var type = View.RzType;
        if (target != null)
        {
            var (t1, t2, link, err) = Rozjazdy.NewCrossover(Doc.P, track, s, dir, side, target, type, "", "");
            if (err != null) { Message("Łącznik: " + err); return; }
            var nums = AskNumbers("Łącznik – numery rozjazdów", new[] { "Rozjazd na torze, z którego zaczyna się łącznik", "Rozjazd na torze sąsiednim" });
            if (nums == null || !NumbersOk(nums)) return;
            t1.Number = nums[0]; t2.Number = nums[1];
            Doc.Change("Łącznik", p => { p.Tracks.Add(link); p.Turnouts.Add(t1); p.Turnouts.Add(t2); });
            View.SelectTurnout(t1.Id);
            var g = View.Geometry(Doc.P.Tracks.First(x => x.Id == link.Id));
            Message($"Łącznik {t1.Number}/{t2.Number} ({type}): tor łączący {UtView.Len3(g.Length)}" + Notes(t1) + Notes(t2));
        }
        else
        {
            double rz = View.NewR > 0 ? View.NewR : 300;
            var (t, br) = Rozjazdy.NewSingle(Doc.P, track, s, dir, side, type, "", pl.Offset, pl.EndAlong, rz);
            var nums = AskNumbers("Rozjazd – numer", new[] { "Numer rozjazdu" });
            if (nums == null || !NumbersOk(nums)) return;
            t.Number = nums[0];
            Doc.Change("Rozjazd", p => { p.Tracks.Add(br); p.Turnouts.Add(t); });
            View.SelectTurnout(t.Id);
            Message(pl.Offset > 0
                ? $"Rozjazd {t.Number} ({type}, {(side > 0 ? "prawy" : "lewy")}) – tor odgałęźny równoległy w odstępie {F(pl.Offset)} m (łuk R {F(rz)}); przedłuż go narzędziem Tor od końca" + Notes(t)
                : $"Rozjazd {t.Number} ({type}, {(side > 0 ? "prawy" : "lewy")}) – odgałęzienie do KR; przedłuż je narzędziem Tor od końca" + Notes(t));
        }
    }

    string Notes(UtTurnout t) { var n = Rozjazdy.Check(Doc.P, Doc.P.Turnouts.FirstOrDefault(x => x.Id == t.Id) ?? t); return n.Count > 0 ? " – UWAGA: " + string.Join("; ", n) : ""; }

    void OnCrossingPlace(XY w)
    {
        var typ = Katalog.Find(View.RzType);
        // przecięcie dwóch torów najbliżej kliknięcia
        (string A, string B, double SA, double SB, double Ang, double D)? best = null;
        var tr = Doc.P.Tracks;
        for (int i = 0; i < tr.Count; i++)
            for (int j = i + 1; j < tr.Count; j++)
            {
                var ra = View.Geometry(tr[i]); var rb = View.Geometry(tr[j]);
                if (Rozjazdy.Intersect(ra, rb, w) is var (sa, sb, ang))
                {
                    double d = (View.ToScreen(ra.At(sa).P) - View.ToScreen(w)).Length;
                    if (d < 20 && (best == null || d < best.Value.D)) best = (tr[i].Id, tr[j].Id, sa, sb, ang, d);
                }
            }
        if (best is not var (a, b, sA, sB, an, _)) { Message("Kliknij na przecięciu dwóch torów (najpierw narysuj tor przecinający)"); return; }
        if (Doc.P.Crossings.Any(c => (c.TrackA == a && c.TrackB == b || c.TrackA == b && c.TrackB == a) && Math.Abs(c.SA - sA) < 1)) { Message("Tu już jest skrzyżowanie"); return; }
        var nums = AskNumbers(typ.Kind == RozjazdTyp.St ? "Skrzyżowanie – numer" : "Rozjazd krzyżowy – numer", new[] { typ.Kind == RozjazdTyp.St ? "Numer skrzyżowania (może być pusty)" : "Numer rozjazdu krzyżowego (zwrotnice a–d)" });
        if (nums == null) return;
        if (typ.Kind != RozjazdTyp.St && !NumbersOk(nums)) return;
        var c = new UtCrossing { Number = nums[0], Type = typ.Name, TrackA = a, TrackB = b, SA = sA, SB = sB };
        Doc.Change(typ.Kind == RozjazdTyp.St ? "Skrzyżowanie" : "Rozjazd krzyżowy", p => p.Crossings.Add(c));
        View.SelectCrossing(c.Id);
        double dev = Math.Abs(an - typ.Alpha) * 180 / Math.PI;
        Message($"{typ.Name}: kąt przecięcia {an * 180 / Math.PI:0.###}° (typ 1:{F(typ.N)} = {typ.Alpha * 180 / Math.PI:0.###}°)" + (dev > 0.2 ? $" – UWAGA: różnica {dev:0.##}°" : ""));
    }

    /// <summary>Usunięcie rozjazdu: z odgałęzieniem (łącznik – oba rozjazdy i tor łączący).</summary>
    bool DeleteTurnoutOrCrossing()
    {
        if (View.SelectedTurnout is string id && Doc.P.Turnouts.FirstOrDefault(x => x.Id == id) is UtTurnout t)
        {
            Doc.Change("Usunięcie rozjazdu", p =>
            {
                var ids = new[] { t.Id, t.Partner }.Where(x => x != null).ToHashSet();
                p.Turnouts.RemoveAll(x => ids.Contains(x.Id));
                p.Tracks.RemoveAll(x => x.Id == t.Branch);
            });
            View.Select(null); Message($"Rozjazd {t.Number} usunięty z odgałęzieniem (Ctrl+Z przywraca)"); return true;
        }
        if (View.SelectedCrossing is string cid)
        {
            Doc.Change("Usunięcie skrzyżowania", p => p.Crossings.RemoveAll(x => x.Id == cid));
            View.Select(null); Message("Skrzyżowanie usunięte"); return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ panele
    bool BuildObjPanel()
    {
        if (View.SelectedTurnout is string id && Doc.P.Turnouts.FirstOrDefault(x => x.Id == id) is UtTurnout t) { BuildTurnoutPanel(t); return true; }
        if (View.SelectedCrossing is string cid && Doc.P.Crossings.FirstOrDefault(x => x.Id == cid) is UtCrossing c) { BuildCrossingPanel(c); return true; }
        return View.SelectedTurnout != null || View.SelectedCrossing != null;   // obiekt zniknął (np. po cofnięciu) – pusty panel
    }

    void ChTurnout(string id, string what, Action<UtTurnout> act) => Doc.Change(what, p => { var t = p.Turnouts.FirstOrDefault(x => x.Id == id); if (t != null) act(t); });

    void BuildTurnoutPanel(UtTurnout t)
    {
        var P = TrackPanel; var typ = Katalog.Find(t.Type);
        var partner = t.Partner != null ? Doc.P.Turnouts.FirstOrDefault(x => x.Id == t.Partner) : null;
        P.Children.Add(Header(partner != null ? $"Rozjazd {t.Number} (łącznik z {partner.Number})" : $"Rozjazd {t.Number}"));
        var num = new TextBox { Text = t.Number, Margin = new Thickness(0, 2, 0, 6) };
        void CommitNum()
        {
            var n = num.Text.Trim(); if (n == t.Number) return;
            if (n.Length == 0 || Rozjazdy.NumberUsed(Doc.P, n, t.Id)) { Message(n.Length == 0 ? "Numer nie może być pusty" : $"Numer {n} jest już zajęty"); num.Text = t.Number; return; }
            ChTurnout(t.Id, "Numer rozjazdu", x => x.Number = n);
        }
        num.LostFocus += (s, e) => CommitNum(); num.KeyDown += (s, e) => { if (e.Key == Key.Enter) CommitNum(); };
        P.Children.Add(Label("Numer rozjazdu")); P.Children.Add(num);

        var tb = new ComboBox { Margin = new Thickness(0, 2, 0, 6) };
        foreach (var k in Katalog.Types.Where(k => k.Kind == RozjazdTyp.Rz)) tb.Items.Add(k.Name);
        tb.SelectedItem = t.Type;
        tb.SelectionChanged += (s, e) => { if (tb.SelectedItem is string n) Doc.Change("Typ rozjazdu", p => { foreach (var x in p.Turnouts.Where(x => x.Id == t.Id || x.Id == t.Partner)) x.Type = n; }); };
        P.Children.Add(Label("Typ (łącznik – oba rozjazdy)")); P.Children.Add(tb);

        var zas = new ComboBox { Margin = new Thickness(0, 2, 0, 6) };
        zas.Items.Add("+  na wprost"); zas.Items.Add("−  na zwrot");
        zas.SelectedIndex = t.Zas == "-" ? 1 : 0;
        zas.SelectionChanged += (s, e) => ChTurnout(t.Id, "Położenie zasadnicze", x => x.Zas = zas.SelectedIndex == 1 ? "-" : "+");
        P.Children.Add(Label("Położenie zasadnicze (klin na rysunku wg Ig-10)")); P.Children.Add(zas);
        var man = new CheckBox { Content = "zwrotnica nieuzależniona (ręczna)", IsChecked = t.Manual, Margin = new Thickness(0, 0, 0, 6) };
        man.Click += (s, e) => ChTurnout(t.Id, "Zwrotnica ręczna", x => x.Manual = man.IsChecked == true);
        P.Children.Add(man);

        var main = Doc.P.Tracks.FirstOrDefault(x => x.Id == t.Track);
        P.Children.Add(Label($"Tor zasadniczy {(main?.Number.Length > 0 ? main.Number : "(bez numeru)")} – km PR [m od początku toru]"));
        var km = new TextBox { Text = t.S.ToString("0.00", CultureInfo.InvariantCulture), Margin = new Thickness(0, 2, 0, 6), IsEnabled = !t.BranchAtEnd, ToolTip = t.BranchAtEnd ? "Drugi rozjazd łącznika leży na przecięciu – przesuń pierwszy" : "Kilometraż początku rozjazdu, Enter – zastosuj" };
        km.KeyDown += (s, e) => { if (e.Key == Key.Enter && Num(km.Text) is double v) ChTurnout(t.Id, "Kilometraż rozjazdu", x => x.S = v); };
        P.Children.Add(km);
        P.Children.Add(new TextBlock { Text = $"{(t.Side > 0 ? "prawy" : "lewy")}, w stronę {(t.Dir > 0 ? "rosnącego" : "malejącego")} kilometrażu toru", Margin = new Thickness(0, 0, 0, 4) });
        if (partner == null)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            row.Children.Add(SmallBtn("Zmień stronę", () => ChTurnout(t.Id, "Strona rozjazdu", x => x.Side = -x.Side), "Odgałęzienie na drugą stronę toru"));
            row.Children.Add(SmallBtn("Odwróć", () => ChTurnout(t.Id, "Kierunek rozjazdu", x => { var ty = Katalog.Find(x.Type); x.S += x.Dir * (ty.A + ty.B); x.Dir = -x.Dir; }), "Rozjazd w drugą stronę (PR po drugiej stronie, to samo miejsce na torze)"));
            P.Children.Add(row);
        }
        if (typ != null)
            P.Children.Add(new TextBlock
            {
                Text = $"R = {F(typ.R)} m, skos 1:{F(typ.N)} ({F(typ.Alpha * 180 / Math.PI, "0.###")}°)\na = {F(typ.A, "0.###")} m, b = {F(typ.B, "0.###")} m{(typ.Approx ? " (przybliżone – wpisz katalogowe w Dane/rozjazdy.json)" : "")}\nV na zwrot {typ.Vbok} km/h",
                TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 0, 6),
            });
        foreach (var n in Rozjazdy.Check(Doc.P, t).Concat(partner != null ? Rozjazdy.Check(Doc.P, partner).Select(x => $"rozjazd {partner.Number}: {x}") : Enumerable.Empty<string>()))
            P.Children.Add(new TextBlock { Text = n, Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        var act = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 6) };
        act.Children.Add(SmallBtn("Odgałęzienie", () => { if (t.Branch != null) View.Select(t.Branch); }, "Zaznacz tor odgałęzienia (przedłużysz go narzędziem Tor od końca)"));
        act.Children.Add(SmallBtn("Usuń rozjazd", () => { View.SelectTurnout(t.Id); DeleteTurnoutOrCrossing(); }, "Usuń rozjazd z odgałęzieniem (Del)"));
        P.Children.Add(act);
        P.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 10) });
    }

    void BuildCrossingPanel(UtCrossing c)
    {
        var P = TrackPanel; var typ = Katalog.Find(c.Type);
        P.Children.Add(Header(typ?.Kind == RozjazdTyp.St ? $"Skrzyżowanie {c.Number}" : $"Rozjazd krzyżowy {c.Number}"));
        var num = new TextBox { Text = c.Number, Margin = new Thickness(0, 2, 0, 6) };
        num.KeyDown += (s, e) => { if (e.Key == Key.Enter) { var n = num.Text.Trim(); if (!Rozjazdy.NumberUsed(Doc.P, n, c.Id)) Doc.Change("Numer skrzyżowania", p => p.Crossings.First(x => x.Id == c.Id).Number = n); else Message($"Numer {n} jest już zajęty"); } };
        P.Children.Add(Label("Numer (zwrotnice: numer + a, b, c, d)")); P.Children.Add(num);
        var tb = new ComboBox { Margin = new Thickness(0, 2, 0, 6) };
        foreach (var k in Katalog.Types.Where(k => k.IsCrossing)) tb.Items.Add(k.Name);
        tb.SelectedItem = c.Type;
        tb.SelectionChanged += (s, e) => { if (tb.SelectedItem is string n) Doc.Change("Typ skrzyżowania", p => p.Crossings.First(x => x.Id == c.Id).Type = n); };
        P.Children.Add(Label("Typ")); P.Children.Add(tb);
        if (typ?.Kind == RozjazdTyp.Rkp)
            P.Children.Add(SmallBtn("Przejazd po drugiej stronie", () => Doc.Change("Strona przejazdu", p => { var x = p.Crossings.First(q => q.Id == c.Id); x.Slip = -x.Slip; }), "Rkp ma jeden przejazd – zmień stronę"));
        if (Rozjazdy.CrossGeo(Doc.P, c) is var (_, _, _, ang) && typ != null)
        {
            double dev = Math.Abs(ang - typ.Alpha) * 180 / Math.PI;
            P.Children.Add(new TextBlock { Text = $"Kąt przecięcia {F(ang * 180 / Math.PI, "0.###")}° (typ 1:{F(typ.N)} = {F(typ.Alpha * 180 / Math.PI, "0.###")}°)", Foreground = dev > 0.2 ? Brushes.Firebrick : Brushes.DimGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 4) });
        }
        string TN(string id) { var x = Doc.P.Tracks.FirstOrDefault(q => q.Id == id); return x?.Number.Length > 0 ? x.Number : "(bez numeru)"; }
        P.Children.Add(new TextBlock { Text = $"Tory {TN(c.TrackA)} (km {F(c.SA)}) i {TN(c.TrackB)} (km {F(c.SB)})", FontSize = 12, Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 0, 6) });
        P.Children.Add(SmallBtn("Usuń", () => { View.SelectCrossing(c.Id); DeleteTurnoutOrCrossing(); }, "Usuń skrzyżowanie (tory zostają)"));
        P.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 10) });
    }
}

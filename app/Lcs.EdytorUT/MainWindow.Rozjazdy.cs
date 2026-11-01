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
        View.TurnoutMenu += id => { var cm = BuildTurnoutMenu(id); cm.PlacementTarget = View; cm.IsOpen = true; };
        View.CrossoverJoin += (aId, bId) => View_CrossoverJoin(aId, bId);
        View.CrossingPlace += OnCrossingPlace;
        View.CrossingNew += (track, s, dir, side) =>
        {
            var typ = Katalog.Find(View.RzType);
            var (c, cross) = Rozjazdy.NewCrossing(Doc.P, track, s, dir, side, typ.Name, "");
            if (c == null) { Message("Brak toru"); return; }
            var nums = AskNumbers(typ.Kind == RozjazdTyp.St ? "Skrzyżowanie – numer" : "Rozjazd krzyżowy – numer", new[] { typ.Kind == RozjazdTyp.St ? "Numer skrzyżowania (może być pusty)" : "Numer rozjazdu krzyżowego (zwrotnice a–d)" });
            if (nums == null) return;
            if (typ.Kind != RozjazdTyp.St && !NumbersOk(nums)) return;
            c.Number = nums[0];
            Doc.Change(typ.Kind == RozjazdTyp.St ? "Skrzyżowanie" : "Rozjazd krzyżowy", p => { p.Tracks.Add(cross); p.Crossings.Add(c); });
            View.SelectCrossing(c.Id);
            Message($"{typ.Name} {c.Number}: tor przecinający pod 1:{F(typ.N)} – przedłuż go narzędziem Tor od końców");
        };
        View.TurnoutMoved += (id, s) =>
        {
            var t = Doc.P.Turnouts.First(x => x.Id == id);
            // drugi rozjazd łącznika trzyma się przecięcia – przesuwa się cały łącznik (rozjazd pierwszy)
            var master = t.BranchAtEnd && t.Partner != null ? Doc.P.Turnouts.First(x => x.Id == t.Partner) : t;
            double delta = s - t.S;
            Doc.Change("Przesunięcie rozjazdu", p => { var m = p.Turnouts.First(x => x.Id == master.Id); m.S += delta; });
            Message($"Rozjazd {t.Number} przesunięty o {F(Math.Abs(delta))} m" + AutoMount(master.Id, master.Partner));
        };
    }

    /// <summary>Menu PPM rozjazdu: strona, odwrócenie, położenie zasadnicze, zwrotnica ręczna, odgałęzienie, usunięcie.</summary>
    void View_CrossoverJoin(string aId, string bId)
    {
        string err = null; double shift = 0;
        var a = Doc.P.Turnouts.First(x => x.Id == aId); var b = Doc.P.Turnouts.First(x => x.Id == bId);
        Doc.Change("Łącznik z dwóch rozjazdów", p => err = Rozjazdy.MakeCrossover(p, aId, bId, out shift, true));
        if (err != null) { Message($"Łącznik {a.Number}/{b.Number}: {err}"); return; }
        View.SelectTurnout(aId);
        string am = AutoMount(aId, bId);
        Message($"Łącznik {a.Number}/{b.Number}: rozjazd {b.Number} przesunięty o {F(shift)} m, żeby skosy się zeszły" + am + Notes(b));
    }

    public ContextMenu BuildTurnoutMenu(string id)
    {
        var cm = new ContextMenu();
        var t = Doc.P.Turnouts.FirstOrDefault(x => x.Id == id); if (t == null) return cm;
        MenuItem Item(string h, Action a, bool en = true) { var mi = new MenuItem { Header = h, IsEnabled = en }; mi.Click += (s, e) => a(); cm.Items.Add(mi); return mi; }
        cm.Items.Add(new MenuItem { Header = $"Rozjazd {t.Number} ({t.Type})", IsEnabled = false, FontWeight = FontWeights.SemiBold });
        Item(t.Partner != null ? "Zmień numery łącznika…" : "Zmień numer…", () => RenumberTurnout(id));
        Item("Zmień stronę", () => ChTurnout(id, "Strona rozjazdu", x => x.Side = -x.Side), t.Partner == null);
        Item("Odwróć", () => ChTurnout(id, "Kierunek rozjazdu", x => { var ty = Katalog.Find(x.Type); x.S += x.Dir * (ty.A + ty.B); x.Dir = -x.Dir; }), t.Partner == null);
        Item(t.Zas == "-" ? "Położenie zasadnicze: na wprost (+)" : "Położenie zasadnicze: na zwrot (−)", () => ChTurnout(id, "Położenie zasadnicze", x => x.Zas = x.Zas == "-" ? "+" : "-"));
        Item(t.Manual ? "Zwrotnica uzależniona" : "Zwrotnica ręczna (nieuzależniona)", () => ChTurnout(id, "Zwrotnica ręczna", x => x.Manual = !x.Manual));
        if (t.Partner == null)
            foreach (var (b, sh) in Rozjazdy.CrossoverCandidates(Doc.P, id))
                Item($"Łącznik z rozjazdem {b.Number} (przesunie się o {F(sh)} m)", () => View_CrossoverJoin(id, b.Id));
        foreach (var w in Naprawa.Warianty(Doc.P, t)) Item(WariantText(w), () => ApplyWariant(id, w));
        cm.Items.Add(new Separator());
        Item("Zaznacz odgałęzienie", () => { if (t.Branch != null) View.Select(t.Branch); });
        Item(t.Partner != null ? "Usuń łącznik" : "Usuń rozjazd", () => { View.SelectTurnout(id); DeleteTurnoutOrCrossing(); });
        return cm;
    }

    /// <summary>Napis przycisku wariantu montażu: „Popraw łuk: …”, „Przesuń rozjazd: …”, „Typ: …”.</summary>
    static string WariantText(Wariant w) => (w.Luk ? "Popraw łuk: " : w.Type != null ? "Typ: " : "Przesuń: ") + w.Label;

    /// <summary>
    /// Wariant montażu rozjazdu (<see cref="Naprawa.Warianty"/>): łuk toru zasadniczego (zmiana R / L jednego łuku, kilometraż obiektów
    /// toru przeliczony – zostają w terenie), przesunięcie rozjazdu albo inny typ.
    /// </summary>
    public void ApplyWariant(string turnoutId, Wariant w)
    {
        var z = Doc.P.Turnouts.FirstOrDefault(x => x.Id == turnoutId); if (z == null) return;
        string what = w.Luk ? "Popraw łuk" : w.Type != null ? "Typ rozjazdu" : "Przesunięcie rozjazdu";
        Doc.Change(what, p => { if (p.Turnouts.FirstOrDefault(x => x.Id == turnoutId) is UtTurnout t) Naprawa.Zastosuj(p, t, w); });
        View.SelectTurnout(turnoutId);
        Message($"{what} przy rozjeździe {z.Number}: {w.Label}" + (w.Luk && w.R1 != w.R0 ? $"; zmiana R o {w.R1 - w.R0:+0;−0} m" : "") + " (Ctrl+Z przywraca)");
    }

    /// <summary>
    /// Montaż rozjazdu łukowego „gdy jest taka potrzeba”: rozjazd (i drugi łącznika) wstawiony albo przesunięty na granicę prostej i łuku
    /// albo na krzywą przejściową – łuk toru dopasowuje się sam, jeśli oś przesuwa się najwyżej o <see cref="Naprawa.AutoDev"/>
    /// (osobna zmiana – Ctrl+Z cofa samą poprawkę). Zwraca dopisek do komunikatu.
    /// </summary>
    string AutoMount(params string[] ids)
    {
        var (kroki, nie) = Naprawa.AutoPlan(Doc.P, ids);
        if (kroki.Count > 0) Doc.Change("Montaż rozjazdu na łuku", p => Naprawa.Wykonaj(p, kroki));
        var msg = kroki.Select(k => $"rozjazd {k.Number} zamontowany: {k.W.Label}").ToList();
        if (kroki.Count > 0) msg[^1] += " (Ctrl+Z cofa samą poprawkę)";
        if (nie.Count > 0) msg.Add($"{(nie.Count > 1 ? "rozjazdy" : "rozjazd")} {string.Join("/", nie)} nie {(nie.Count > 1 ? "leżą" : "leży")} na stałym promieniu – warianty montażu w panelu rozjazdu");
        return msg.Count > 0 ? " – " + string.Join("; ", msg) : "";
    }

    /// <summary>
    /// Nowy numer wstawionego rozjazdu (w łączniku – obu rozjazdów naraz, więc można je zamienić miejscami).
    /// Numer zajęty w posterunku – po potwierdzeniu zamiana z tamtym rozjazdem.
    /// </summary>
    public void RenumberTurnout(string id)
    {
        var t = Doc.P.Turnouts.FirstOrDefault(x => x.Id == id); if (t == null) return;
        var pa = t.Partner != null ? Doc.P.Turnouts.FirstOrDefault(x => x.Id == t.Partner) : null;
        var all = pa != null ? new[] { t, pa } : new[] { t };
        var nums = AskNumbers(pa != null ? $"Łącznik {t.Number}/{pa.Number} – numery" : $"Rozjazd {t.Number} – numer",
            all.Select(x => $"Numer rozjazdu (teraz {x.Number})").ToArray(), all.Select(x => x.Number).ToArray());
        if (nums == null) return;
        ApplyNumbers(pa != null ? "Numery łącznika" : "Numer rozjazdu", all.Select(x => x.Id).ToArray(), nums);
    }

    /// <summary>Test: odpowiedź na pytanie o zamianę numerów zamiast okna (null – okno, przy NoPrompt – tak).</summary>
    public Func<string, bool> ConfirmForTest;
    bool Confirm(string text, string title)
    {
        if (ConfirmForTest != null) return ConfirmForTest(text);
        if (NoPrompt) return true;
        return MessageBox.Show(this, text, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    bool numBusy;
    /// <summary>
    /// Nowe numery rozjazdów / skrzyżowań (jedno Ctrl+Z); numer zajęty w posterunku – pytanie o zamianę (<see cref="Rozjazdy.PlanNumbers"/>).
    /// False – nic się nie zmieniło (ten sam numer, błąd, rezygnacja).
    /// </summary>
    public bool ApplyNumbers(string what, string[] ids, string[] nums)
    {
        if (numBusy) return false;      // okno pytania zdejmuje fokus z pola – drugie zatwierdzenie z LostFocus
        numBusy = true;
        try
        {
            string Num(string id) => Doc.P.Turnouts.FirstOrDefault(x => x.Id == id)?.Number ?? Doc.P.Crossings.FirstOrDefault(x => x.Id == id)?.Number ?? "";
            var olds = ids.Select(Num).ToArray();
            nums = nums.Select(n => (n ?? "").Trim()).ToArray();
            if (nums.SequenceEqual(olds)) return false;
            var plan = Rozjazdy.PlanNumbers(Doc.P, ids, nums);
            if (plan.Blad != null) { Message(plan.Blad + " – numer nie został zmieniony"); return false; }
            if (plan.Zamiany.Count > 0 && !Confirm(
                    $"Numer {string.Join(", ", nums.Where((n, i) => n != olds[i] && n.Length > 0))} jest już zajęty w tym posterunku.\n\nZamienić numery?\n" +
                    string.Join("\n", plan.Zamiany.Select(z => "  " + z)), "Zamiana numerów"))
            { Message("Numer nie został zmieniony"); return false; }
            Doc.Change(what, p => Rozjazdy.ApplyNumbers(p, plan));
            Message($"{(ids.Length > 1 ? "Rozjazdy" : "Numer")} {string.Join("/", olds)} → {string.Join("/", nums)}" +
                    (plan.Zamiany.Count > 0 ? "; zamiana: " + string.Join(", ", plan.Zamiany) : "") + " (Ctrl+Z przywraca)");
            return true;
        }
        finally { numBusy = false; }
    }

    /// <summary>Numery z okna (domyślnie kolejne wolne albo <paramref name="def"/>); null – rezygnacja.</summary>
    string[] AskNumbers(string title, string[] labels, string[] def = null)
    {
        def ??= labels.Select((_, i) => Rozjazdy.NextNumber(Doc.P, i).ToString()).ToArray();
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

    bool NumbersOk(string[] nums, string track = null)
    {
        foreach (var n in nums)
        {
            if (n.Length == 0) { Message("Wpisz numer rozjazdu"); return false; }
            if (Rozjazdy.NumberUsed(Doc.P, n, null, track)) { Message($"Numer {n} jest już zajęty – rozjazd nie został wstawiony"); return false; }
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
            // tory nierównoległe: łuk wyrównujący we wstawce; gdy przy tym samym typie się nie mieści (albo rozjazdy nachodzą na siebie) –
            // drugi rozjazd innego typu tej samej szyny, najbliższy kątem (próba na kopii)
            bool Zle(string ty2)
            {
                var tmp = UtDocument.Deserialize(UtDocument.Serialize(Doc.P));
                var (a, b, l, e) = Rozjazdy.NewCrossover(tmp, track, s, dir, side, target, type, "", "", ty2);
                if (e != null) return true;
                tmp.Tracks.Add(l); tmp.Turnouts.Add(a); tmp.Turnouts.Add(b); Rozjazdy.Sync(tmp);
                return new[] { a.Id, b.Id }.Any(id => tmp.Turnouts.FirstOrDefault(x => x.Id == id) is not UtTurnout z
                    || Rozjazdy.Check(tmp, z).Any(n => n.Contains("łącznik") || n.Contains("wstawk") || n.Contains("łuk wyrównując")));
            }
            string type2 = null;
            if (Katalog.Find(type) is RozjazdTyp ty1 && Zle(null))
            {
                string rail = ty1.Name.Split(' ').ElementAtOrDefault(1) ?? "";
                type2 = Katalog.Types.Where(x => x.Kind == RozjazdTyp.Rz && x.Name != type && x.Name.Contains(rail))
                    .OrderBy(x => Math.Abs(x.Alpha - ty1.Alpha)).FirstOrDefault(x => !Zle(x.Name))?.Name;
            }
            var (t1, t2, link, err) = Rozjazdy.NewCrossover(Doc.P, track, s, dir, side, target, type, "", "", type2);
            if (err != null) { Message("Łącznik: " + err); return; }
            var nums = AskNumbers("Łącznik – numery rozjazdów", new[] { "Rozjazd na torze, z którego zaczyna się łącznik", "Rozjazd na torze sąsiednim" });
            if (nums == null || !NumbersOk(nums, track)) return;
            t1.Number = nums[0]; t2.Number = nums[1];
            Doc.Change("Łącznik", p => { p.Tracks.Add(link); p.Turnouts.Add(t1); p.Turnouts.Add(t2); });
            View.SelectTurnout(t1.Id);
            string am = AutoMount(t1.Id, t2.Id);
            var g = View.Geometry(Doc.P.Tracks.First(x => x.Id == link.Id));
            var lt1 = Doc.P.Turnouts.FirstOrDefault(x => x.Id == t1.Id); var lt2 = Doc.P.Turnouts.FirstOrDefault(x => x.Id == t2.Id);
            string luk = lt1 != null && lt2 != null && Rozjazdy.Geometry(Doc.P, lt1) is TurnoutGeo gq1 && Rozjazdy.Geometry(Doc.P, lt2) is TurnoutGeo gq2
                && Rozjazdy.Equalizer(gq1, gq2) is var (_, dq, rq) && dq > 1e-4 ? $", łuk wyrównujący R {rq:0} m ({dq * 180 / Math.PI:0.##}°)" : "";
            Message($"Łącznik {t1.Number}/{t2.Number} ({type}{(type2 != null ? $" + {type2} – z tym samym typem łuk wyrównujący się nie mieści" : "")}): tor łączący {UtView.Len3(g.Length)}{luk}" + am + Notes(t1) + Notes(t2));
        }
        else
        {
            double rz = View.NewR > 0 ? View.NewR : 300;
            var (t, br) = Rozjazdy.NewSingle(Doc.P, track, s, dir, side, type, "", pl.Offset, pl.EndAlong, rz);
            var nums = AskNumbers("Rozjazd – numer", new[] { "Numer rozjazdu" });
            if (nums == null || !NumbersOk(nums, track)) return;
            t.Number = nums[0];
            Doc.Change("Rozjazd", p => { p.Tracks.Add(br); p.Turnouts.Add(t); });
            View.SelectTurnout(t.Id);
            string am = AutoMount(t.Id);
            Message((pl.Offset > 0
                ? $"Rozjazd {t.Number} ({type}, {(side > 0 ? "prawy" : "lewy")}) – tor odgałęźny równoległy w odstępie {F(pl.Offset)} m (łuk R {F(rz)}); przedłuż go narzędziem Tor od końca"
                : $"Rozjazd {t.Number} ({type}, {(side > 0 ? "prawy" : "lewy")}) – odgałęzienie do KR; przedłuż je narzędziem Tor od końca") + am + Notes(t));
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
        if (BuildWyposazeniePanel()) return true;
        if (View.SelectedDevice is string did && Doc.P.Devices.FirstOrDefault(x => x.Id == did) is UtDevice dv) { BuildDevicePanel(dv); return true; }
        if (View.SelectedTurnout is string id && Doc.P.Turnouts.FirstOrDefault(x => x.Id == id) is UtTurnout t) { BuildTurnoutPanel(t); return true; }
        if (View.SelectedCrossing is string cid && Doc.P.Crossings.FirstOrDefault(x => x.Id == cid) is UtCrossing c) { BuildCrossingPanel(c); return true; }
        return View.SelectedTurnout != null || View.SelectedCrossing != null || View.SelectedDevice != null;   // obiekt zniknął (np. po cofnięciu) – pusty panel
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
            if (!ApplyNumbers("Numer rozjazdu", new[] { t.Id }, new[] { n })) num.Text = Doc.P.Turnouts.FirstOrDefault(x => x.Id == t.Id)?.Number ?? t.Number;
        }
        num.LostFocus += (s, e) => CommitNum(); num.KeyDown += (s, e) => { if (e.Key == Key.Enter) CommitNum(); };
        P.Children.Add(Label("Numer rozjazdu (Enter – zastosuj; zajęty – zamiana)")); P.Children.Add(num);
        if (partner != null)
            P.Children.Add(SmallBtn($"Zmień numery łącznika {t.Number}/{partner.Number}…", () => RenumberTurnout(t.Id), "Oba numery naraz – można je też zamienić miejscami"));

        var tb = new ComboBox { Margin = new Thickness(0, 2, 0, 6) };
        foreach (var k in Katalog.Types.Where(k => k.Kind == RozjazdTyp.Rz)) tb.Items.Add(k.Name);
        tb.SelectedItem = t.Type;
        // łącznik: każdy rozjazd ma własny typ (łuk wyrównujący we wstawce bierze różnicę kątów); przycisk – ten typ dla obu
        tb.SelectionChanged += (s, e) => { if (tb.SelectedItem is string n && n != t.Type) Doc.Change("Typ rozjazdu", p => { foreach (var x in p.Turnouts.Where(x => x.Id == t.Id)) x.Type = n; }); };
        P.Children.Add(Label(partner != null ? "Typ tego rozjazdu (drugi rozjazd łącznika może mieć inny)" : "Typ")); P.Children.Add(tb);
        if (partner != null && partner.Type != t.Type)
            P.Children.Add(SmallBtn($"Ten sam typ dla {partner.Number}", () => Doc.Change("Typ rozjazdu", p => { foreach (var x in p.Turnouts.Where(x => x.Id == t.Partner)) x.Type = t.Type; }), $"Rozjazd {partner.Number}: {partner.Type} → {t.Type}"));

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
        km.KeyDown += (s, e) => { if (e.Key == Key.Enter && Num(km.Text) is double v) { ChTurnout(t.Id, "Kilometraż rozjazdu", x => x.S = v); if (AutoMount(t.Id, t.Partner) is { Length: > 0 } am) Message("Kilometraż rozjazdu" + am); } };
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
        if (Rozjazdy.Geometry(Doc.P, t) is TurnoutGeo g && g.Kind != "Rz")
            P.Children.Add(new TextBlock
            {
                Text = $"Rozjazd łukowy {(g.Kind == "Rłj" ? "jednostronny (Rłj)" : "dwustronny (Rłd)")} – wygięty {typ?.Name}, dopasowuje się do łuku toru:\n" +
                       $"tor zasadniczy R {F(g.RMain, "0")} m, tor zwrotny {(g.RDiv > 0 ? $"R {F(g.RDiv, "0")} m" : "prosty")}" +
                       (g.VDiv > 0 ? $"\nV na zwrot z promienia ≈ {g.VDiv} km/h (3,6·√(0,65·R))" : ""),
                TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Brushes.DarkSlateBlue, Margin = new Thickness(0, 0, 0, 6),
            });
        foreach (var w in Rozjazdy.Wstawki(Doc.P, t))
            P.Children.Add(new TextBlock
            {
                Text = w.Crossover
                    ? $"Wstawka międzyrozjazdowa {t.Number}/{w.Other}: {F(w.Len, "0.##")} m (KR – KR), najmniejsza {F(w.Min, "0.#")} m; ψ = {F(w.Psi, "0.##")} m/s³ (dop. 1)" + (w.REq > 0 ? $"; łuk wyrównujący R {F(w.REq, "0")} m (skosy rozchodzą się o {F(w.DevDeg, "0.##")}° – tory w łuku)" : "") + (w.Tor ? "; ponad 30 m – osobny tor z numerem" : "")
                    : $"Wstawka do rozjazdu {w.Other}: {F(w.Len, "0.##")} m",
                TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 0, 0, 4),
                Foreground = w.Crossover && (w.Len < w.Min - 0.01 || w.Psi > 1) ? Brushes.Firebrick : Brushes.DimGray,
                ToolTip = "Wstawka międzyrozjazdowa – odcinek między stykami rozjazdów, do 30 m (dłuższy to tor z numerem). W łączniku z krzyżownicami łukowymi " +
                          "końcami do siebie: l ≥ v/10, nie mniej niż 6 m (warunki techniczne § 24 ust. 2 pkt 4); ψ = V·(a₁+a₂)/(3,6·(20 + w)) ≤ 1 m/s³.",
            });
        if (partner == null && t.Single)
            foreach (var (b, sh) in Rozjazdy.CrossoverCandidates(Doc.P, t.Id))
                P.Children.Add(SmallBtn($"Łącznik z rozjazdem {b.Number} (przesunie się o {F(sh)} m)", () => View_CrossoverJoin(t.Id, b.Id),
                    $"Rozjazdy {t.Number} i {b.Number} jako łącznik ze wspólną wstawką; rozjazd {b.Number} przesunie się tak, żeby skosy się zeszły. Przedłużenia odgałęzień łączące oba rozjazdy znikną."));
        foreach (var n in Rozjazdy.Check(Doc.P, t).Concat(partner != null ? Rozjazdy.Check(Doc.P, partner).Select(x => $"rozjazd {partner.Number}: {x}") : Enumerable.Empty<string>()))
            P.Children.Add(new TextBlock { Text = n, Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        foreach (var w in Naprawa.Warianty(Doc.P, t))
            P.Children.Add(SmallBtn(WariantText(w), () => ApplyWariant(t.Id, w),
                w.Luk ? "Montaż na łuku: granica łuku przesuwa się poza rozjazd – zmienia się R albo przejściowa jednego łuku (wierzchołek i proste zostają); rozjazdy i urządzenia toru zostają w terenie"
                : w.Type != null ? "Inny typ rozjazdu – tor zwrotny rozjazdu łukowego nie mniejszy niż 140 m" : "Rozjazd przesuwa się wzdłuż toru w całości na prostą albo na stały promień (łuk toru bez zmian)"));
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
        void CommitNum()
        {
            var n = num.Text.Trim(); if (n == c.Number) return;
            if (!ApplyNumbers("Numer skrzyżowania", new[] { c.Id }, new[] { n })) num.Text = Doc.P.Crossings.FirstOrDefault(x => x.Id == c.Id)?.Number ?? c.Number;
        }
        num.LostFocus += (s, e) => CommitNum(); num.KeyDown += (s, e) => { if (e.Key == Key.Enter) CommitNum(); };
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

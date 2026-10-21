using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Urządzenia w oknie: rodzaj na pasku, nazwa z okna (podpowiedź kolejnej wolnej), panel urządzenia (nazwa, km, kierunek, strona,
/// komory), menu PPM, usuwanie.
/// </summary>
public partial class MainWindow
{
    public readonly ComboBox DevKindBox = new() { Width = 150, ToolTip = "Rodzaj urządzenia (Ig-10 zał. 8)" };
    /// <summary>Test: nazwa zamiast okna (null – podpowiedź).</summary>
    public Func<string, string> AskNameForTest;

    void InitUrzadzenia()
    {
        foreach (var k in UtDevice.Kinds) DevKindBox.Items.Add(k.Name);
        DevKindBox.SelectedIndex = 0;
        DevKindBox.SelectionChanged += (s, e) =>
        {
            if (DevKindBox.SelectedIndex < 0) return;
            View.DevKind = UtDevice.Kinds[DevKindBox.SelectedIndex].Key;
            if (View.Tool == UtTool.Urzadzenie) ToolShown(); else SetTool(UtTool.Urzadzenie);
        };
        View.DevicePlace += OnDevicePlace;
        View.DeviceMoved += (id, s) => MoveDevice(id, s, "Przesunięcie urządzenia");
        View.DeviceMenu += id => { var cm = BuildDeviceMenu(id); cm.PlacementTarget = View; cm.IsOpen = true; };
    }

    /// <summary>Nowy km urządzenia; sygnalizator na wspólnej konstrukcji ciągnie za sobą pozostałe – bramka zostaje w jednym przekroju.</summary>
    public void MoveDevice(string id, double s, string what)
    {
        int n = 0;
        Doc.Change(what, p => { if (p.Devices.FirstOrDefault(x => x.Id == id) is UtDevice d) { d.S = s; n = Urzadzenia.Wyrownaj(p, d); } });
        if (n > 0 && Urzadzenia.KonstrukcjaOf(Doc.P, id) is var (mast, m))
            Message($"{what}: {(mast == UtDevice.MastWysiegnik ? "wysięgnik" : "bramka")} {string.Join("/", m.Select(x => x.Name))} przesunięta w całości (jeden przekrój)");
    }

    void ChDevice(string id, string what, Action<UtDevice> act) => Doc.Change(what, p => { var d = p.Devices.FirstOrDefault(x => x.Id == id); if (d != null) act(d); });

    string AskName(string title, string label, string def)
    {
        if (AskNameForTest != null) return AskNameForTest(def) ?? def;
        if (NoPrompt) return def;
        var dlg = new Window { Title = title, Owner = this, SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, FontFamily = FontFamily, FontSize = 13 };
        var sp = new StackPanel { Margin = new Thickness(14) };
        sp.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 2) });
        var tb = new TextBox { Text = def, Width = 160 }; sp.Children.Add(tb);
        var ok = new Button { Content = "OK", IsDefault = true, Width = 80, Margin = new Thickness(0, 12, 6, 0) };
        var cancel = new Button { Content = "Anuluj", IsCancel = true, Width = 80, Margin = new Thickness(0, 12, 0, 0) };
        ok.Click += (s, e) => dlg.DialogResult = true;
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; row.Children.Add(ok); row.Children.Add(cancel);
        sp.Children.Add(row); dlg.Content = sp;
        dlg.Loaded += (s, e) => { tb.Focus(); tb.SelectAll(); };
        return dlg.ShowDialog() == true ? tb.Text.Trim() : null;
    }

    void OnDevicePlace(string track, double s, int dir, int side)
    {
        var kind = View.DevKind; var info = UtDevice.KindInfo(kind);
        string name = "";
        if (!string.IsNullOrEmpty(info.Prefix) || kind == UtDevice.Semafor)
        {
            name = AskName(info.Name, $"Nazwa ({info.Name.ToLower()})", Urzadzenia.NextName(Doc.P, kind, track));
            if (name == null) return;
            if (Urzadzenia.NameTaken(Doc.P, name, track)) { Message($"Nazwa {name} jest już zajęta w tym posterunku – urządzenie nie zostało wstawione"); return; }
        }
        var d = new UtDevice { Kind = kind, Name = name, Track = track, S = Math.Round(s, 2), Dir = dir, Side = side };
        Doc.Change(info.Name, p => p.Devices.Add(d));
        View.SelectDevice(d.Id);
        var tr = Doc.P.Tracks.FirstOrDefault(x => x.Id == track);
        Message($"{info.Name} {name} na torze {tr?.Number} km {F(d.S)}" + (info.Directed && kind != UtDevice.Koziol ? $", dla jazdy w stronę {(dir > 0 ? "rosnącego" : "malejącego")} km, po {(side > 0 ? "prawej" : "lewej")}" : ""));
    }

    /// <summary>Usunięcie zaznaczonego urządzenia (Del).</summary>
    bool DeleteDevice()
    {
        if (View.SelectedDevice is not string id || Doc.P.Devices.FirstOrDefault(x => x.Id == id) is not UtDevice d) return false;
        Doc.Change("Usunięcie urządzenia", p => p.Devices.RemoveAll(x => x.Id == id));
        View.Select(null); Message($"{UtDevice.KindInfo(d.Kind).Name} {d.Name} usunięty (Ctrl+Z przywraca)");
        return true;
    }

    public ContextMenu BuildDeviceMenu(string id)
    {
        var cm = new ContextMenu();
        var d = Doc.P.Devices.FirstOrDefault(x => x.Id == id); if (d == null) return cm;
        MenuItem Item(string h, Action a, bool en = true) { var mi = new MenuItem { Header = h, IsEnabled = en }; mi.Click += (s, e) => a(); cm.Items.Add(mi); return mi; }
        cm.Items.Add(new MenuItem { Header = $"{UtDevice.KindInfo(d.Kind).Name} {d.Name}", IsEnabled = false, FontWeight = FontWeights.SemiBold });
        Item("Zmień stronę", () => ChDevice(id, "Strona urządzenia", x => x.Side = -x.Side));
        Item("Odwróć kierunek", () => ChDevice(id, "Kierunek urządzenia", x => x.Dir = -x.Dir), d.Directed);
        if (Urzadzenia.Sygnalizator(d))
        {
            // wspólna bramka / wysięgnik: dołącz sygnalizator w pobliżu albo odłącz
            var join = new MenuItem { Header = "Połącz na wspólnej bramce / wysięgniku z" };
            foreach (var x in Urzadzenia.DoPolaczenia(Doc.P, d))
            {
                var mi = new MenuItem { Header = $"{UtDevice.KindInfo(x.Kind).Name.ToLower()} {x.Name}" }; var oid = x.Id;
                mi.Click += (s, e) => JoinStructure(id, oid); join.Items.Add(mi);
            }
            join.IsEnabled = join.Items.Count > 0; cm.Items.Add(join);
            Item("Odłącz od wspólnej konstrukcji", () => ChDevice(id, "Odłączenie od konstrukcji", Urzadzenia.Odlacz), Urzadzenia.KonstrukcjaOf(Doc.P, id) is { M.Count: >= 2 });
        }
        cm.Items.Add(new Separator());
        Item("Usuń", () => { View.SelectDevice(id); DeleteDevice(); });
        return cm;
    }

    void BuildDevicePanel(UtDevice d)
    {
        var P = TrackPanel; var info = UtDevice.KindInfo(d.Kind);
        P.Children.Add(Header($"{info.Name} {d.Name}"));
        P.Children.Add(new TextBlock { Text = $"Ig-10 {info.Ig10}", Foreground = Brushes.DimGray, FontSize = 12, Margin = new Thickness(0, -4, 0, 6) });
        var name = new TextBox { Text = d.Name, Margin = new Thickness(0, 2, 0, 6) };
        void CommitName()
        {
            var n = name.Text.Trim(); if (n == d.Name) return;
            if (Urzadzenia.NameTaken(Doc.P, n, d.Track, d.Id)) { Message($"Nazwa {n} jest już zajęta w tym posterunku"); name.Text = d.Name; return; }
            ChDevice(d.Id, "Nazwa urządzenia", x => x.Name = n);
        }
        name.LostFocus += (s, e) => CommitName(); name.KeyDown += (s, e) => { if (e.Key == Key.Enter) CommitName(); };
        P.Children.Add(Label("Nazwa")); P.Children.Add(name);
        if (d.Kind == UtDevice.Semafor) FunkcjaCombo(P, d);
        if (d.Powtarzajacy) PowtarzaCombo(P, d);
        var tr = Doc.P.Tracks.FirstOrDefault(x => x.Id == d.Track);
        if (Posterunki.Of(Doc.P, d.Track) is UtPost post) P.Children.Add(new TextBlock { Text = $"Posterunek {PostName(post)} (z toru)", Foreground = Brushes.DimGray, FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
        P.Children.Add(Label($"Tor {(tr?.Number.Length > 0 ? tr.Number : "(bez numeru)")} – km [m od początku toru]"));
        var km = new TextBox { Text = d.S.ToString("0.00", CultureInfo.InvariantCulture), Margin = new Thickness(0, 2, 0, 6) };
        km.KeyDown += (s, e) => { if (e.Key == Key.Enter && Num(km.Text) is double v) MoveDevice(d.Id, v, "Kilometraż urządzenia"); };
        P.Children.Add(km);
        if (d.Directed)
            P.Children.Add(new TextBlock { Text = $"dla jazdy w stronę {(d.Dir > 0 ? "rosnącego" : "malejącego")} km toru, po {(d.Side > 0 ? "prawej" : "lewej")}", Margin = new Thickness(0, 0, 0, 4) });
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        row.Children.Add(SmallBtn("Zmień stronę", () => ChDevice(d.Id, "Strona urządzenia", x => x.Side = -x.Side), "Urządzenie po drugiej stronie toru"));
        if (d.Directed) row.Children.Add(SmallBtn("Odwróć kierunek", () => ChDevice(d.Id, "Kierunek urządzenia", x => x.Dir = -x.Dir), "Dla jazdy w drugą stronę"));
        P.Children.Add(row);
        if (d.Kind is UtDevice.Semafor or UtDevice.TarczaOstrz or UtDevice.TarczaMan) BuildSignalConfig(P, d);
        foreach (var n in Urzadzenia.Check(Doc.P, d))
            P.Children.Add(new TextBlock { Text = n, Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        P.Children.Add(SmallBtn("Usuń", () => { View.SelectDevice(d.Id); DeleteDevice(); }, "Usuń urządzenie (Del)"));
        P.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 10) });
    }
    /// <summary>
    /// Wspólna konstrukcja: z kim sygnalizator wisi na bramce / wysięgniku, „Połącz z…” (sygnalizatory w pobliżu, ten sam kierunek)
    /// i „Odłącz”. Połączenie przesuwa dołączane sygnalizatory w miejsce konstrukcji.
    /// </summary>
    void StructureConfig(StackPanel P, UtDevice d)
    {
        var k = Urzadzenia.KonstrukcjaOf(Doc.P, d.Id);
        bool joined = k is { M.Count: >= 2 };
        string what = (k?.Mast ?? d.MastOrDefault) == UtDevice.MastWysiegnik ? "wysięgniku" : "bramce";
        P.Children.Add(Label("Konstrukcja (bramka, wysięgnik nad torami)"));
        if (joined)
            P.Children.Add(new TextBlock { Text = $"Na wspólnym {what}: " + string.Join(", ", k.Value.M.Select(x => Doc.P.Tracks.FirstOrDefault(t => t.Id == x.Track) is UtTrack t && t.Number.Length > 0 ? $"{x.Name} (tor {t.Number})" : x.Name)),
                                           TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 4) });
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 6) };
        var cand = Urzadzenia.DoPolaczenia(Doc.P, d);
        var cb = new ComboBox { MinWidth = 150, ToolTip = "Sygnalizator w pobliżu (do 60 m, ten sam kierunek) – zawiśnie na tej samej belce; przesunie się na swoim torze w miejsce konstrukcji" };
        cb.Items.Add(cand.Count > 0 ? "Połącz z…" : "(brak sygnalizatorów w pobliżu)");
        foreach (var x in cand)
        {
            var tn = Doc.P.Tracks.FirstOrDefault(t => t.Id == x.Track)?.Number;
            cb.Items.Add($"{UtDevice.KindInfo(x.Kind).Name.ToLower()} {x.Name}" + (string.IsNullOrEmpty(tn) ? "" : $" (tor {tn})"));
        }
        cb.SelectedIndex = 0; cb.IsEnabled = cand.Count > 0;
        cb.SelectionChanged += (s, e) => { if (cb.SelectedIndex > 0) JoinStructure(d.Id, cand[cb.SelectedIndex - 1].Id); };
        row.Children.Add(cb);
        if (joined && Urzadzenia.Przekrzywienie(Doc.P, d) > 0.05)
            P.Children.Add(SmallBtn($"Ustaw bramkę w przekroju {d.Name}", () =>
            {
                int n = 0; Doc.Change("Bramka w jednym przekroju", p => { if (p.Devices.FirstOrDefault(x => x.Id == d.Id) is UtDevice dd) n = Urzadzenia.Wyrownaj(p, dd); });
                Message($"Konstrukcja w przekroju {d.Name}: przesunięte {n} (Ctrl+Z cofa)");
            }, $"Pozostałe sygnalizatory konstrukcji przesuwają się na swoich torach w poprzek od {d.Name} (o {Urzadzenia.Przekrzywienie(Doc.P, d):0.##} m) – belka prostopadle do torów, jak na planie"));
        if (joined) row.Children.Add(SmallBtn("Odłącz", () => { ChDevice(d.Id, "Odłączenie od konstrukcji", Urzadzenia.Odlacz); Message($"{d.Name} odłączony – osobna konstrukcja"); }, "Sygnalizator na własnej bramce / własnym wysięgniku"));
        P.Children.Add(row);
        if (k is not var (mast, m)) return;              // pojedynczy wysięgnik – własny maszt, bez ustawień konstrukcji

        // słupy: dwa (bramka) albo jeden – po lewej / po prawej patrząc w kierunku jazdy; belka także nad torami bez sygnalizatora
        var ids = m.Select(x => x.Id).ToHashSet();
        void SetAll(string what, Action<UtDevice> act) => Doc.Change(what, p => { foreach (var x in p.Devices.Where(x => ids.Contains(x.Id))) act(x); });
        var (slupy, strona, tory) = Urzadzenia.Ustawienia(mast, m);
        if (strona == 0) strona = m.Sum(x => x.Side) >= 0 ? 1 : -1;
        P.Children.Add(Label("Słupy konstrukcji"));
        var posts = new WrapPanel { Margin = new Thickness(0, 2, 0, 6) };
        foreach (var (txt, n, side) in new[] { ("dwa (po obu stronach)", 2, 0), ("jeden po lewej", 1, -1), ("jeden po prawej", 1, 1) })
        {
            var rb = new RadioButton { Content = txt, GroupName = "slupy" + d.Id, IsChecked = slupy == n && (n == 2 || strona == side), Margin = new Thickness(0, 0, 10, 2),
                                       ToolTip = n == 2 ? "Bramka: belka na dwóch słupach" : "Belka na jednym słupie (jak wysięgnik), po tej stronie patrząc w kierunku jazdy" };
            rb.Click += (s, e) => SetAll("Słupy konstrukcji", x => { x.Slupy = n; x.SlupStrona = n == 1 ? side : null; });
            posts.Children.Add(rb);
        }
        P.Children.Add(posts);
        var near = Urzadzenia.ToryPrzyKonstrukcji(Doc.P, m);
        if (near.Count == 0 && tory.Count == 0) return;
        P.Children.Add(Label("Belka także nad torem (bez sygnalizatora)"));
        var trs = new WrapPanel { Margin = new Thickness(0, 2, 0, 6) };
        foreach (var (t, dist) in near.Take(6))
        {
            var cb2 = new CheckBox { Content = $"tor {(t.Number.Length > 0 ? t.Number : "(bez numeru)")} ({dist:0.0} m)", IsChecked = tory.Contains(t.Id), Margin = new Thickness(0, 0, 10, 2) };
            var tid = t.Id;
            cb2.Click += (s, e) =>
            {
                var nt = tory.ToList(); if (cb2.IsChecked == true) nt.Add(tid); else nt.Remove(tid);
                SetAll("Belka nad torem", x => x.NadTorami = nt.Count > 0 ? nt.ToList() : null);
            };
            trs.Children.Add(cb2);
        }
        P.Children.Add(trs);
    }

    /// <summary>Połączenie sygnalizatora <paramref name="otherId"/> (z jego konstrukcją) z konstrukcją <paramref name="id"/>.</summary>
    public void JoinStructure(string id, string otherId)
    {
        int n = 0;
        Doc.Change("Połączenie konstrukcji", p =>
        {
            var a = p.Devices.FirstOrDefault(x => x.Id == id); var b = p.Devices.FirstOrDefault(x => x.Id == otherId);
            if (a != null && b != null) n = Urzadzenia.Polacz(p, a, b);
        });
        var d = Doc.P.Devices.FirstOrDefault(x => x.Id == id); var k = Urzadzenia.KonstrukcjaOf(Doc.P, id);
        if (d != null && k != null)
            Message($"Wspólna {(k.Value.Mast == UtDevice.MastWysiegnik ? "konstrukcja – wysięgnik" : "bramka")}: {string.Join(", ", k.Value.M.Select(x => x.Name))} (dołączone przesunięte w miejsce {d.Name}; Ctrl+Z cofa)");
        View.SelectDevice(id);
    }

    /// <summary>
    /// Konfiguracja sygnalizatora świetlnego (BSK ISDR „Oznaczenia urządzeń srk”): układ gotowy, komory od masztu (kolor, pulsowanie,
    /// kolejność), komora świecąca w stanie zasadniczym, maszt, pasy świetlne, W24, W21, zapis komór tekstem.
    /// </summary>
    void BuildSignalConfig(StackPanel P0, UtDevice d)
    {
        // wszystko w jednym polu „Konfiguracja semafora / tarczy”
        var P = new StackPanel { Margin = new Thickness(4) };
        P0.Children.Add(new GroupBox { Header = d.Kind == UtDevice.Semafor ? "Konfiguracja semafora" : "Konfiguracja tarczy", Content = P, Margin = new Thickness(0, 4, 0, 8), Padding = new Thickness(2) });
        var lamps = UtDevice.ParseLights(d.LightsOrDefault);
        void SetLamps(List<(char C, int Pulse)> l, string what = "Komory sygnalizatora") =>
            ChDevice(d.Id, what, x => { x.Lights = UtDevice.FormatLights(l); x.Heads = l.Count; if (!string.IsNullOrEmpty(x.Lit) && l.All(q => q.C != x.Lit[0])) x.Lit = null; });

        P.Children.Add(Label("Układ gotowy"));
        var pre = new ComboBox { Margin = new Thickness(0, 2, 0, 6), ToolTip = "Wg BSK ISDR: ss3–ss9" };
        pre.Items.Add("(wybierz…)");
        foreach (var x in UtDevice.Presets) pre.Items.Add(x.Name);
        pre.SelectedIndex = 0;
        pre.SelectionChanged += (s, e) =>
        {
            if (pre.SelectedIndex <= 0) return;
            var x = UtDevice.Presets[pre.SelectedIndex - 1];
            ChDevice(d.Id, "Układ sygnalizatora", q => { q.Lights = x.Lights; q.Heads = UtDevice.ParseLights(x.Lights).Count; q.Lit = x.Lit.ToString(); q.Mast = x.Mast; q.W24 = x.W24; q.StripeZ = q.StripeP = x.Stripes; q.W21 = null; });
        };
        P.Children.Add(pre);

        // na bramce / wysięgniku semafor wisi – „od masztu” to od dołu, czyli od komory najdalszej od belki (przy belce jest górna)
        bool hangs = d.MastOrDefault is UtDevice.MastBramka or UtDevice.MastWysiegnik;
        P.Children.Add(Label(hangs ? "Komory od dołu, jak na maszcie (kolor, światło) – semafor wisi: 1 najdalej od belki, ostatnia (górna) przy belce"
                                   : "Komory od masztu (kolor, światło)"));
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 4) };
        foreach (var w in new[] { 18.0, 92, 118, 24, 24, 24 }) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });
        for (int i = 0; i < lamps.Count; i++)
        {
            int ii = i; grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            void Put(UIElement el, int col) { Grid.SetRow(el, ii); Grid.SetColumn(el, col); grid.Children.Add(el); }
            Put(new TextBlock { Text = (i + 1).ToString(), VerticalAlignment = VerticalAlignment.Center }, 0);
            var cb = new ComboBox { Margin = new Thickness(0, 1, 4, 1) };
            foreach (var c in UtDevice.Colors) cb.Items.Add(c.Name);
            cb.SelectedIndex = Array.FindIndex(UtDevice.Colors, c => c.Key == lamps[i].C);
            cb.SelectionChanged += (s, e) => { if (cb.SelectedIndex < 0) return; var l = lamps.ToList(); l[ii] = (UtDevice.Colors[cb.SelectedIndex].Key, l[ii].Pulse); SetLamps(l); };
            Put(cb, 1);
            var pu = new ComboBox { Margin = new Thickness(0, 1, 4, 1), ToolTip = "Światło: stałe; stałe lub pulsujące (na planie 2 kreski); tylko pulsujące (4 kreski)" };
            foreach (var m in UtDevice.PulseModes) pu.Items.Add(m.Name);
            pu.SelectedIndex = Math.Clamp(lamps[i].Pulse, 0, 2);
            pu.SelectionChanged += (s, e) => { if (pu.SelectedIndex < 0) return; var l = lamps.ToList(); l[ii] = (l[ii].C, pu.SelectedIndex); SetLamps(l); };
            Put(pu, 2);
            Put(SmallBtn("↑", () => { if (ii == 0) return; var l = lamps.ToList(); (l[ii - 1], l[ii]) = (l[ii], l[ii - 1]); SetLamps(l); }, "Komora bliżej masztu"), 3);
            Put(SmallBtn("↓", () => { if (ii == lamps.Count - 1) return; var l = lamps.ToList(); (l[ii + 1], l[ii]) = (l[ii], l[ii + 1]); SetLamps(l); }, "Komora dalej od masztu"), 4);
            Put(SmallBtn("✕", () => { if (lamps.Count <= 1) return; var l = lamps.ToList(); l.RemoveAt(ii); SetLamps(l); }, "Usuń komorę"), 5);
        }
        P.Children.Add(grid);
        var lrow = new StackPanel { Orientation = Orientation.Horizontal };
        lrow.Children.Add(SmallBtn("+ komora", () => { if (lamps.Count >= 7) return; var l = lamps.ToList(); l.Add(('B', UtDevice.Stale)); SetLamps(l); }, "Nowa komora na końcu (najdalej od masztu, na bramce – przy belce), biała"));
        if (lamps.Count > 1)
            lrow.Children.Add(SmallBtn("↕ Odwróć kolejność", () => { var l = lamps.ToList(); l.Reverse(); SetLamps(l, "Odwrócenie kolejności komór"); },
                "Komory w odwrotnej kolejności – gdy wpisano je od góry (np. z planu, od belki bramki), a liczy się od dołu"));
        P.Children.Add(lrow);

        P.Children.Add(Label("Komory tekstem (C Z P B N; * – stałe lub pulsujące, ** – tylko pulsujące)"));
        var txt = new TextBox { Text = d.LightsOrDefault, Margin = new Thickness(0, 2, 0, 6), ToolTip = "Np. B**PCZ* – od masztu: biała tylko pulsująca, pomarańczowa, czerwona, zielona stała lub pulsująca; Enter – zastosuj" };
        txt.KeyDown += (s, e) =>
        {
            if (e.Key != Key.Enter) return;
            var t = txt.Text.Trim().ToUpperInvariant();
            if (!UtDevice.ValidLights(t)) { Message("Komory: litery C Z P B N (od masztu), * po literze – pulsuje, 1–7 komór"); return; }
            SetLamps(UtDevice.ParseLights(t));
        };
        P.Children.Add(txt);

        P.Children.Add(Label("Świeci w stanie zasadniczym"));
        var lit = new ComboBox { Margin = new Thickness(0, 2, 0, 6) };
        var litKeys = new List<char> { ' ' }; lit.Items.Add("żadna");
        foreach (var c in UtDevice.Colors.Where(c => lamps.Any(q => q.C == c.Key))) { litKeys.Add(c.Key); lit.Items.Add(c.Name); }
        lit.SelectedIndex = Math.Max(0, litKeys.IndexOf(d.LitOrDefault));
        lit.SelectionChanged += (s, e) => { if (lit.SelectedIndex >= 0) ChDevice(d.Id, "Komora świecąca", x => x.Lit = litKeys[lit.SelectedIndex] == ' ' ? "-" : litKeys[lit.SelectedIndex].ToString()); };
        P.Children.Add(lit);

        P.Children.Add(Label("Mocowanie"));
        var mounts = new WrapPanel { Margin = new Thickness(0, 2, 0, 6) };
        foreach (var m in UtDevice.Masts)
        {
            var rb = new RadioButton { Content = m.Name, GroupName = "mast" + d.Id, IsChecked = m.Key == d.MastOrDefault, Margin = new Thickness(0, 0, 10, 2) };
            var key = m.Key;
            // na wspólnej konstrukcji bramka ↔ wysięgnik zmienia całą konstrukcję; maszt i karzeł – odłącza od niej
            rb.Click += (s, e) =>
            {
                var ids = Urzadzenia.KonstrukcjaOf(Doc.P, d.Id) is { M.Count: >= 2 } kk && key is UtDevice.MastBramka or UtDevice.MastWysiegnik
                    ? kk.M.Select(x => x.Id).ToHashSet() : new HashSet<string> { d.Id };
                Doc.Change("Mocowanie sygnalizatora", p =>
                {
                    foreach (var x in p.Devices.Where(x => ids.Contains(x.Id)))
                    {
                        x.Mast = key; x.Dwarf = key == UtDevice.MastKarzel;
                        if (key is UtDevice.MastProsty or UtDevice.MastKarzel) x.Konstrukcja = null;
                    }
                });
            };
            mounts.Children.Add(rb);
        }
        P.Children.Add(mounts);
        StructureConfig(P, d);

        if (d.Kind != UtDevice.Semafor) return;          // pasy, W24, W21 – tylko na semaforach
        P.Children.Add(Label("Wskaźniki i pasy świetlne"));
        var row = new WrapPanel { Margin = new Thickness(0, 2, 0, 4) };
        CheckBox Flag(string t, bool v, Action<UtDevice, bool> set)
        {
            var c = new CheckBox { Content = t, IsChecked = v, Margin = new Thickness(0, 0, 10, 2) };
            c.Click += (s, e) => ChDevice(d.Id, t, x => set(x, c.IsChecked == true));
            row.Children.Add(c); return c;
        }
        Flag("pas pomarańczowy", d.StripeP, (x, v) => x.StripeP = v);
        Flag("pas zielony", d.StripeZ, (x, v) => x.StripeZ = v);
        Flag("W24", d.W24, (x, v) => x.W24 = v);
        P.Children.Add(row);
        var w21 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        var w21c = new CheckBox { Content = "W21, cyfra: ", IsChecked = !string.IsNullOrEmpty(d.W21), VerticalAlignment = VerticalAlignment.Center };
        w21c.Click += (s, e) => ChDevice(d.Id, "Wskaźnik W21", x => x.W21 = w21c.IsChecked == true ? (string.IsNullOrEmpty(x.W21) ? "6" : x.W21) : null);
        w21.Children.Add(w21c);
        var w21b = new TextBox { Text = d.W21 ?? "", Width = 40, ToolTip = "Cyfra wskaźnika W21 – prędkość w dziesiątkach km/h (np. 6 = 60 km/h), puste – brak; Enter" };
        w21b.KeyDown += (s, e) =>
        {
            if (e.Key != Key.Enter) return;
            var v = w21b.Text.Trim();
            // W21 pokazuje cyfrę = prędkość / 10 km/h (np. 4, 6, 10)
            if (v.Length > 0 && !(int.TryParse(v, out var n) && n is >= 1 and <= 16)) { Message("W21: liczba 1–16 (prędkość w dziesiątkach km/h), puste – brak wskaźnika"); w21b.Text = d.W21 ?? ""; return; }
            ChDevice(d.Id, "Wskaźnik W21", x => x.W21 = v.Length > 0 ? v : null);
        };
        w21.Children.Add(w21b);
        // W21 świetlny (Ie-1): zapala się z sygnałem zezwalającym i pokazuje prędkość przebiegu – w symulatorze liczba z prędkości przebiegu
        var w21s = new CheckBox { Content = "świetlny", IsChecked = d.W21Swietlny, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0),
                                  ToolTip = "W21 świetlny – liczba z prędkości przebiegu (symulator); bez zaznaczenia – tablica odblaskowa z wpisaną cyfrą" };
        w21s.Click += (s, e) => ChDevice(d.Id, "Wskaźnik W21 świetlny", x => x.W21Swietlny = w21s.IsChecked == true);
        w21.Children.Add(w21s);
        P.Children.Add(w21);
    }
}

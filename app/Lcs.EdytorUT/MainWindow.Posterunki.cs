using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Posterunki i szlaki w oknie (etap Z0, PLAN_EDYTOR_ZOBRAZOWANIA.md § 6): lista w panelu, nowy posterunek (zaznaczone tory przechodzą
/// do niego), posterunek toru w panelu toru i zaznaczenia, szlak narzędziem Szlak (B), panele posterunku i szlaku, funkcja semafora.
/// </summary>
public partial class MainWindow
{
    public readonly Expander PostExpander = new() { IsExpanded = false, Margin = new Thickness(0, 0, 0, 6) };
    readonly StackPanel postList = new() { Margin = new Thickness(0, 6, 0, 0) };

    void InitPosterunki()
    {
        PostExpander.Header = Header("Posterunki i szlaki");
        PostExpander.Content = postList;
        PostExpander.Expanded += (s, e) => { View.ShowPosts = true; View.InvalidateVisual(); };
        PostExpander.Collapsed += (s, e) => { View.ShowPosts = false; View.InvalidateVisual(); };
        View.SzlakPlace += OnSzlakPlace;
    }

    static string PostName(UtPost x) => $"{x.Kod} {x.Nazwa}".Trim();

    /// <summary>Lista posterunków i szlaków w panelu (po każdej zmianie).</summary>
    void RefreshPosts()
    {
        postList.Children.Clear();
        if (Doc == null) return;
        var p = Doc.P;
        var drogi = View.DrogiNow();
        foreach (var x in p.Posts)
        {
            int n = p.Tracks.Count(t => t.Post == x.Id);
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new Rectangle { Width = 12, Height = 12, Fill = new SolidColorBrush(View.PostColor(x.Id)), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(new TextBlock { Text = $"{PostName(x)} – torów {n}" });
            var id = x.Id;
            var b = new Button { Content = row, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 2), Padding = new Thickness(4, 1, 4, 1), ToolTip = "Zaznacz posterunek – panel z kodem, nazwą i torami" };
            b.Click += (s, e) => View.SelectObj(Kontrola.KPosterunek, id);
            postList.Children.Add(b);
        }
        postList.Children.Add(SmallBtn("+ posterunek", () => NewPost(), "Nowy posterunek ruchu (stacja, mijanka, posterunek odgałęźny); zaznaczone tory przechodzą do niego. Tor przypiszesz też w panelu toru."));
        postList.Children.Add(new Separator { Margin = new Thickness(0, 6, 0, 6) });
        foreach (var l in p.Lines)
        {
            var d = drogi.TryGetValue(l.Id, out var dd) ? dd : null;
            string typ = UtLine.Typy.FirstOrDefault(t => t.Key == l.Typ).Name ?? l.Typ;
            var txt = $"szlak {(l.Numer.Length > 0 ? l.Numer : "?")}" + (l.NazwaSkr.Length > 0 ? $" {l.NazwaSkr}" : "") + " – " + (d?.Err != null ? "brak drogi" : $"{UtView.Len(d?.Dlugosc ?? 0)}, {typ.Split(' ')[0]}" + (l.Samoczynna ? $" {l.Stawnosc}-st." : ""));
            var id = l.Id;
            var b = new Button { Content = txt, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 2), Padding = new Thickness(4, 1, 4, 1), Foreground = d?.Err != null ? Brushes.Firebrick : Brushes.Black, ToolTip = d?.Err ?? (l.Nazwa.Length > 0 ? l.Nazwa + " – " : "") + "zaznacz szlak – panel z blokadą, końcami i drogą" };
            b.Click += (s, e) => View.SelectObj(Kontrola.KSzlak, id);
            postList.Children.Add(b);
        }
        postList.Children.Add(SmallBtn("+ szlak (B)", () => SetTool(UtTool.Szlak), "Narzędzie Szlak: klik OD (semafor, koniec toru albo miejsce na torze), klik DO – z podglądem drogi; Ctrl + klik na torze – końce same. Szlak z torów: zaznacz tory, w panelu „Należy do” – „+ nowy szlak” albo istniejący szlak (B)"));
        postList.Children.Add(new TextBlock { Text = "Tory posterunków i szlaki są podświetlone na planie, gdy ten pasek jest rozwinięty (tylko w edytorze, nie na wydruku).", Foreground = Brushes.DimGray, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
    }

    /// <summary>Tory zaznaczone (kilka albo jeden).</summary>
    List<string> SelectedTracks() => View.Multi.Count > 0
        ? View.Multi.Where(x => x.Kind == Kontrola.KTor).Select(x => x.Id).ToList()
        : View.SelectedTrack != null ? new List<string> { View.SelectedTrack } : new List<string>();

    public UtPost NewPost()
    {
        var kod = AskName("Nowy posterunek", "Kod posterunku (np. LW) – przedrostek nazw w symulatorze", "");
        if (string.IsNullOrEmpty(kod)) return null;
        if (Doc.P.Posts.Any(x => x.Kod == kod)) { Message($"Kod posterunku {kod} jest już zajęty"); return null; }
        var nazwa = AskName("Nowy posterunek", "Nazwa posterunku (np. Łódź Widzew)", kod);
        if (nazwa == null) return null;
        var post = new UtPost { Kod = kod, Nazwa = nazwa };
        var tracks = SelectedTracks();
        Doc.Change("Nowy posterunek", p =>
        {
            p.Posts.Add(post);
            foreach (var t in p.Tracks.Where(t => tracks.Contains(t.Id))) t.Post = post.Id;
        });
        View.ClearMulti(); View.SelectObj(Kontrola.KPosterunek, post.Id);
        PostExpander.IsExpanded = true;
        Message($"Posterunek {PostName(post)}" + (tracks.Count > 0 ? $": przypisane tory {string.Join(", ", tracks.Select(TrNr))}" : " – tory przypisz w panelu toru (pole „Posterunek”) albo zaznacz kilka i wybierz posterunek"));
        return post;
    }

    /// <summary>Nowy szlak z końców; <paramref name="tory"/> – tory szlaku (z panelu toru) tracą posterunek.</summary>
    void OnSzlakPlace(UtLineEnd a, UtLineEnd b) => OnSzlakPlace(a, b, null);
    void OnSzlakPlace(UtLineEnd a, UtLineEnd b, List<string> tory)
    {
        int n = 1; while (Doc.P.Lines.Any(x => x.Numer == n.ToString())) n++;
        var num = AskName("Szlak", "Oznaczenie szlaku (np. 1A, 2ŁW)", n.ToString());
        if (string.IsNullOrEmpty(num)) return;
        if (Doc.P.Lines.Any(x => x.Numer == num)) { Message($"Oznaczenie szlaku {num} jest już zajęte"); return; }
        var l = new UtLine { Numer = num, Konce = new() { a, b } };
        // rodzaj blokady: jak na pozostałych szlakach obszaru; nazwy – z posterunków na końcach
        if (Doc.P.Lines.Count > 0) { l.Typ = Doc.P.Lines[^1].Typ; l.Stawnosc = Doc.P.Lines[^1].Stawnosc; }
        (l.Nazwa, l.NazwaSkr) = Szlaki.ProponujNazwy(Doc.P, l);
        Doc.Change("Szlak", p => { p.Lines.Add(l); if (tory != null) foreach (var t in p.Tracks.Where(t => tory.Contains(t.Id))) t.Post = null; });
        View.SelectObj(Kontrola.KSzlak, l.Id);
        PostExpander.IsExpanded = true;
        var d = View.DrogiNow().GetValueOrDefault(l.Id);
        Message(d?.Err != null ? $"Szlak {num}: {d.Err}" : $"Szlak {num} {l.Nazwa}: {Szlaki.Opis(Doc.P, a)} – {Szlaki.Opis(Doc.P, b)}, {UtView.Len(d?.Dlugosc ?? 0)}" + (d?.Semafory.Count > 0 ? $", semafory na szlaku: {string.Join(", ", d.Semafory.Select(x => x.Name))}" : "")
                + (a.Semafor == null || b.Semafor == null ? " – wpisz w panelu posterunek sąsiedni przy końcu na granicy obszaru (nazwa szlaku uzupełni się)" : ""));
    }

    // ------------------------------------------------------------------ przynależność toru: posterunek albo szlak
    /// <summary>
    /// Lista „Należy do” dla toru albo kilku torów: posterunek, szlak (tory dochodzą do szlaku – końce przesuwają się na koniec ciągu,
    /// <see cref="Szlaki.Rozszerz"/>), nowy posterunek, nowy szlak z zaznaczonych torów (<see cref="Szlaki.ZTorow"/>).
    /// </summary>
    void PostCombo(StackPanel P, List<string> tracks, string label)
    {
        if (tracks.Count == 0) return;
        var p = Doc.P;
        var drogi = View.DrogiNow();
        P.Children.Add(Label(label));
        var cb = new ComboBox { Margin = new Thickness(0, 2, 0, 6), ToolTip = "Posterunek ruchu (rozjazdy i urządzenia na torze dziedziczą posterunek) albo szlak, do którego należy tor. Szlak: tory muszą łączyć się końcami z jego drogą – końce szlaku przesuną się na koniec ciągu (semafor do 30 m od końca toru albo koniec toru)." };
        cb.Items.Add("(brak – poza posterunkami i szlakami)");
        foreach (var x in p.Posts) cb.Items.Add(PostName(x));
        foreach (var l in p.Lines) cb.Items.Add($"szlak {(l.Numer.Length > 0 ? l.Numer : "?")}" + (l.NazwaSkr.Length > 0 ? $" {l.NazwaSkr}" : ""));
        cb.Items.Add("+ nowy posterunek…");
        cb.Items.Add("+ nowy szlak z " + (tracks.Count > 1 ? "tych torów…" : "tego toru…"));
        int nP = p.Posts.Count, nL = p.Lines.Count;
        // szlak toru: droga szlaku przechodzi przez tor (kawałek dłuższy niż 0,5 m)
        UtLine SzlakToru(string id) => p.Lines.FirstOrDefault(l => drogi.TryGetValue(l.Id, out var d) && d.Kawalki.Any(k => k.Track == id && k.S1 - k.S0 > 0.5));
        var cur = tracks.Select(id => p.Tracks.FirstOrDefault(t => t.Id == id)?.Post is string pid ? "P" + pid : SzlakToru(id) is UtLine sl ? "L" + sl.Id : "").Distinct().ToList();
        cb.SelectedIndex = cur.Count != 1 ? -1 : cur[0] == "" ? 0 : cur[0][0] == 'P' ? p.Posts.FindIndex(x => x.Id == cur[0][1..]) + 1 : nP + 1 + p.Lines.FindIndex(x => x.Id == cur[0][1..]);
        int init = cb.SelectedIndex;
        string nr = string.Join(", ", tracks.Select(TrNr));
        cb.SelectionChanged += (s, e) =>
        {
            int i = cb.SelectedIndex;
            if (i < 0 || i == init) return;
            if (i == nP + nL + 1) { NewPost(); return; }
            if (i == nP + nL + 2)
            {
                var (kon, err) = Szlaki.ZTorow(Doc.P, tracks, View.TopoR, View.TopoStyki);
                if (err != null) { Message("Nowy szlak: " + err); cb.SelectedIndex = init; return; }
                OnSzlakPlace(kon[0].E, kon[1].E, tracks);
                return;
            }
            if (i > nP)
            {
                var l = p.Lines[i - nP - 1];
                var (konce, err) = Szlaki.Rozszerz(Doc.P, l, tracks, View.TopoR, View.TopoStyki);
                if (err != null) { Message($"Szlak {l.Numer}: {err}"); cb.SelectedIndex = init; return; }
                var lid = l.Id;
                Doc.Change("Tory szlaku", q =>
                {
                    foreach (var t in q.Tracks.Where(t => tracks.Contains(t.Id))) t.Post = null;
                    var ql = q.Lines.First(x => x.Id == lid);
                    ql.Konce = konce.Select(k => new UtLineEnd { Semafor = k.Semafor, Track = k.Track, AtStart = k.AtStart, S = k.S, Sasiad = k.Sasiad, Oznaczenie = k.Oznaczenie }).ToList();
                });
                var d = View.DrogiNow().GetValueOrDefault(lid);
                var nl = Doc.P.Lines.First(x => x.Id == lid);
                Message($"Tory {nr} należą do szlaku {nl.Numer}: {Szlaki.Opis(Doc.P, nl.Konce[0])} – {Szlaki.Opis(Doc.P, nl.Konce[1])}" + (d?.Err != null ? $" – {d.Err}" : $", {UtView.Len(d?.Dlugosc ?? 0)}"));
                return;
            }
            string pid = i == 0 ? null : p.Posts[i - 1].Id;
            Doc.Change("Posterunek toru", q => { foreach (var t in q.Tracks.Where(t => tracks.Contains(t.Id))) t.Post = pid; });
            var wSzlaku = tracks.Select(SzlakToru).Where(x => x != null).Distinct().Select(x => x.Numer).ToList();
            Message((pid == null ? $"Tory {nr} bez posterunku" : $"Tory {nr} należą do posterunku {PostName(p.Posts[i - 1])}")
                + (wSzlaku.Count > 0 ? $" – droga szlaku {string.Join(", ", wSzlaku)} nadal przez nie przechodzi; zmień jego końce narzędziem Szlak (B)" : ""));
        };
        P.Children.Add(cb);
    }

    // ------------------------------------------------------------------ funkcja semafora
    void FunkcjaCombo(StackPanel P, UtDevice d)
    {
        P.Children.Add(Label("Funkcja semafora"));
        var cb = new ComboBox { Margin = new Thickness(0, 2, 0, 6), ToolTip = "Od funkcji zależy symbol na pulpicie (np. trójkąt końca przebiegu przy wjazdowym) i rola w zależnościach" };
        cb.Items.Add("(nie określona)");
        foreach (var f in UtDevice.Funkcje) cb.Items.Add(f.Name);
        cb.SelectedIndex = string.IsNullOrEmpty(d.Funkcja) ? 0 : Array.FindIndex(UtDevice.Funkcje, f => f.Key == d.Funkcja) + 1;
        cb.SelectionChanged += (s, e) => { if (cb.SelectedIndex >= 0) ChDevice(d.Id, "Funkcja semafora", x => x.Funkcja = cb.SelectedIndex == 0 ? null : UtDevice.Funkcje[cb.SelectedIndex - 1].Key); };
        P.Children.Add(cb);
    }

    // ------------------------------------------------------------------ semafor powtarzany (tarcza ostrzegawcza, powtarzacz)
    void PowtarzaCombo(StackPanel P, UtDevice d)
    {
        var p = Doc.P;
        P.Children.Add(Label("Powtarza (semafor albo szlak)"));
        var opts = new List<(string Id, string Name)> { (null, "(nie wskazano)") };
        opts.AddRange(p.Devices.Where(x => x.Id != d.Id && x.Kind == UtDevice.Semafor && !x.Powtarzajacy && x.Funkcja != UtDevice.FPk && x.Name.Length > 0)
            .Select(x => (x.Id, Urzadzenia.PowtarzaOpis(p, x.Id))).OrderBy(x => x.Item2, StringComparer.CurrentCulture));
        opts.AddRange(p.Lines.Where(l => l.Numer.Length > 0).Select(l => (l.Id, $"szlak {l.Numer}")).OrderBy(x => x.Item2, StringComparer.CurrentCulture));
        if (!string.IsNullOrEmpty(d.Powtarza) && !opts.Any(x => x.Id == d.Powtarza)) opts.Add((d.Powtarza, Urzadzenia.PowtarzaOpis(p, d.Powtarza) ?? "(usunięty)"));
        var cb = new ComboBox { Margin = new Thickness(0, 2, 0, 2), ToolTip = "Semafor, którego sygnał powtarza (na pulpicie zielony, gdy semafor podaje sygnał zezwalający), albo szlak – informacja z sąsiedniej stacji" };
        foreach (var o in opts) cb.Items.Add(o.Name);
        cb.SelectedIndex = Math.Max(0, opts.FindIndex(x => x.Id == d.Powtarza));
        cb.SelectionChanged += (s, e) => { if (cb.SelectedIndex >= 0 && opts[cb.SelectedIndex].Id != d.Powtarza) ChDevice(d.Id, "Semafor powtarzany", x => x.Powtarza = opts[cb.SelectedIndex].Id); };
        P.Children.Add(cb);
        P.Children.Add(SmallBtn("Zaproponuj", () =>
        {
            var m = Urzadzenia.Propozycja(p, d);
            if (m == null) { Message($"{d.Name}: w kierunku działania (do 3 km) nie ma semafora – wskaż z listy"); return; }
            ChDevice(d.Id, "Semafor powtarzany", x => x.Powtarza = m.Id);
            Message($"{d.Name} powtarza {Urzadzenia.PowtarzaOpis(p, m.Id)} – propozycja: pierwszy semafor w kierunku działania; sprawdź");
        }, "Pierwszy semafor przed urządzeniem w kierunku jego działania – tylko propozycja, sprawdź ją"));
    }

    // ------------------------------------------------------------------ panele
    void BuildPostPanel(UtPost x)
    {
        var P = TrackPanel; var p = Doc.P;
        void Ch(string what, Action<UtPost> a) => Doc.Change(what, q => { var y = q.Posts.FirstOrDefault(z => z.Id == x.Id); if (y != null) a(y); });
        P.Children.Add(Header($"Posterunek {PostName(x)}"));
        P.Children.Add(new TextBlock { Text = "obszar w UT: tory przypisane do posterunku, rozjazdy i urządzenia dziedziczą posterunek z toru", Foreground = Brushes.DimGray, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, -4, 0, 6) });
        Field(P, "Kod (przedrostek nazw w symulatorze, np. LW)", x.Kod, n =>
        {
            if (n.Length == 0 || p.Posts.Any(y => y.Id != x.Id && y.Kod == n)) { Message(n.Length == 0 ? "Kod nie może być pusty" : $"Kod {n} jest już zajęty"); RefreshTrackPanel(); return; }
            Ch("Kod posterunku", y => y.Kod = n);
        });
        Field(P, "Nazwa (opis na obrazach)", x.Nazwa, n => Ch("Nazwa posterunku", y => y.Nazwa = n));
        var tracks = p.Tracks.Where(t => t.Post == x.Id).ToList();
        var ids = tracks.Select(t => t.Id).ToHashSet();
        int nz = p.Turnouts.Count(z => ids.Contains(z.Track)), ns = p.Devices.Count(d => ids.Contains(d.Track) && d.Kind == UtDevice.Semafor);
        P.Children.Add(new TextBlock { Text = $"Tory ({tracks.Count}): {(tracks.Count > 0 ? string.Join(", ", tracks.Select(t => t.Number.Length > 0 ? t.Number : "?")) : "brak")}\nRozjazdy: {nz}, semafory: {ns}", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) });
        Notes(P, Posterunki.Check(p, View.DrogiNow(), Topologia.Routes(p)).Where(u => u.Id == x.Id).Select(u => u.Text));
        P.Children.Add(SmallBtn("Usuń", () => DeleteObj(), "Usuń posterunek (Del) – tory zostają, bez posterunku"));
        P.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 10) });
    }

    void BuildLinePanel(UtLine l)
    {
        var P = TrackPanel; var p = Doc.P;
        void Ch(string what, Action<UtLine> a) => Doc.Change(what, q => { var y = q.Lines.FirstOrDefault(z => z.Id == l.Id); if (y != null) a(y); });
        P.Children.Add(Header($"Szlak {(l.Numer.Length > 0 ? l.Numer : "(bez oznaczenia)")}"));
        P.Children.Add(new TextBlock { Text = "szlak z blokadą liniową (KOMPUTERY.pdf 1.6); tory szlaku wyznacza program z układu – po torach między końcami", Foreground = Brushes.DimGray, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, -4, 0, 6) });
        Field(P, "Oznaczenie szlaku (np. 1A)", l.Numer, n =>
        {
            if (n.Length == 0 || p.Lines.Any(y => y.Id != l.Id && y.Numer == n)) { Message(n.Length == 0 ? "Oznaczenie nie może być puste" : $"Oznaczenie {n} jest już zajęte"); RefreshTrackPanel(); return; }
            Ch("Oznaczenie szlaku", y => y.Numer = n);
        });
        Field(P, "Nazwa pełna (np. Łódź Widzew – Łódź Andrzejów)", l.Nazwa, n => Ch("Nazwa szlaku", y => y.Nazwa = n));
        Field(P, "Nazwa skrócona (np. ŁW – ŁA)", l.NazwaSkr, n => Ch("Nazwa skrócona szlaku", y => y.NazwaSkr = n), "Na planie przy oznaczeniu szlaku");
        var (pn, ps) = Szlaki.ProponujNazwy(p, l);
        if (pn != l.Nazwa || ps != l.NazwaSkr)
            P.Children.Add(SmallBtn($"Zaproponuj: {pn} ({ps})", () => Ch("Nazwy szlaku", y => (y.Nazwa, y.NazwaSkr) = Szlaki.ProponujNazwy(Doc.P, y)),
                "Nazwy z posterunków na końcach szlaku (nazwa i kod posterunku semafora wjazdowego albo posterunek sąsiedni przy końcu na granicy obszaru)"));
        var row = new Grid { Margin = new Thickness(0, 0, 0, 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) }); row.ColumnDefinitions.Add(new ColumnDefinition());
        var c1 = new StackPanel(); var c2 = new StackPanel(); Grid.SetColumn(c2, 2); row.Children.Add(c1); row.Children.Add(c2);
        Field(c1, "Linia kolejowa", l.Linia, n => Ch("Linia szlaku", y => y.Linia = n));
        Field(c2, "Tor szlakowy", l.Tor, n => Ch("Tor szlakowy", y => y.Tor = n));
        P.Children.Add(row);
        P.Children.Add(Label("Blokada liniowa"));
        var typ = new ComboBox { Margin = new Thickness(0, 2, 0, 6) };
        foreach (var t in UtLine.Typy) typ.Items.Add(t.Name);
        typ.SelectedIndex = Math.Max(0, Array.FindIndex(UtLine.Typy, t => t.Key == l.Typ));
        typ.SelectionChanged += (s, e) => { if (typ.SelectedIndex >= 0) Ch("Blokada liniowa", y => y.Typ = UtLine.Typy[typ.SelectedIndex].Key); };
        P.Children.Add(typ);
        if (l.Samoczynna)
        {
            P.Children.Add(Label("Stawność SBL"));
            var st = new ComboBox { Margin = new Thickness(0, 2, 0, 6) };
            foreach (var k in new[] { 2, 3, 4 }) st.Items.Add($"{k}-stawna");
            st.SelectedIndex = Math.Clamp(l.Stawnosc - 2, 0, 2);
            st.SelectionChanged += (s, e) => { if (st.SelectedIndex >= 0) Ch("Stawność SBL", y => y.Stawnosc = st.SelectedIndex + 2); };
            P.Children.Add(st);
        }
        for (int i = 0; i < l.Konce.Count; i++)
        {
            int ii = i; var e = l.Konce[i];
            var gp = new StackPanel { Margin = new Thickness(4) };
            P.Children.Add(new GroupBox { Header = $"Koniec {i + 1}: {Szlaki.Opis(p, e)}", Content = gp, Margin = new Thickness(0, 2, 0, 6), Padding = new Thickness(2) });
            // posterunek sąsiedni: nazwy szlaku z propozycji idą za nim (gdy projektant ich nie zmienił)
            if (e.Semafor == null) Field(gp, "Posterunek sąsiedni (spoza obszaru)", e.Sasiad, n => Ch("Posterunek sąsiedni", y =>
            {
                var (on, os) = Szlaki.ProponujNazwy(Doc.P, y);
                y.Konce[ii].Sasiad = n;
                var (nn, ns) = Szlaki.ProponujNazwy(Doc.P, y);
                if (y.Nazwa.Length == 0 || y.Nazwa == on) y.Nazwa = nn;
                if (y.NazwaSkr.Length == 0 || y.NazwaSkr == os) y.NazwaSkr = ns;
            }));
            Field(gp, "Oznaczenie końca na pulpicie (np. 1G)", e.Oznaczenie, n => Ch("Oznaczenie końca szlaku", y => y.Konce[ii].Oznaczenie = n), "Napis przy strzałce blokady na pulpicie; puste – oznaczenie szlaku");
        }
        var d = View.DrogiNow().GetValueOrDefault(l.Id);
        if (d != null && d.Err == null)
        {
            P.Children.Add(new TextBlock { Text = $"Długość {UtView.Len3(d.Dlugosc)}", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
            P.Children.Add(new TextBlock { Text = string.Join("\n", d.Kawalki.Select(k => $"tor {TrNr(k.Track)}: km {F(k.S0)} – {F(k.S1)}")), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) });
            P.Children.Add(new TextBlock { Text = d.Semafory.Count > 0 ? $"Semafory na szlaku: {string.Join(", ", d.Semafory.Select(x => x.Name))}" : "Bez semaforów na szlaku (jeden odstęp)", FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) });
        }
        Notes(P, Szlaki.Check(p, View.DrogiNow()).Where(u => u.Id == l.Id).Select(u => u.Text));
        P.Children.Add(SmallBtn("Usuń", () => DeleteObj(), "Usuń szlak (Del) – tory i semafory zostają"));
        P.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 10) });
    }
}

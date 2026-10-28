using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Wyposażenie toru w oknie (etap 5, część 2): odcinki kontroli niezajętości, perony, przejazdy, punkty kilometrażu – wstawianie
/// (nazwy z okna, podpowiedź), panele, menu PPM, usuwanie, przesuwanie wzdłuż toru.
/// </summary>
public partial class MainWindow
{
    void InitWyposazenie()
    {
        View.SectionPlace += OnSectionPlace;
        View.PlatformPlace += OnPlatformPlace;
        View.LevelCrossingPlace += OnLevelCrossingPlace;
        View.KmPlace += OnKmPlace;
        View.ObjMoved += (kind, id, d) => Doc.Change("Przesunięcie wzdłuż toru", p =>
        {
            switch (kind)
            {
                case Kontrola.KPeron: if (p.Platforms.FirstOrDefault(x => x.Id == id) is UtPlatform pl) { pl.S0 += d; pl.S1 += d; } break;
                case Kontrola.KPrzejazd: if (p.LevelCrossings.FirstOrDefault(x => x.Id == id) is UtLevelCrossing c) c.S += d; break;
                case Kontrola.KKm: if (p.KmPoints.FirstOrDefault(x => x.Id == id) is UtKmPoint k) k.S += d; break;
                case Kontrola.KOdcinek: if (p.Sections.FirstOrDefault(x => x.Id == id) is UtSection s) s.S += d; break;
                case Kontrola.KObiekt:
                    if (p.Obiekty.FirstOrDefault(x => x.Id == id) is UtObiekt o) { o.S0 += d; o.S1 += d; foreach (var t in o.Tory ?? new()) { t.S0 += d; t.S1 += d; } }
                    break;
            }
        });
        View.ObjMenu += (kind, id) =>
        {
            var cm = new ContextMenu();
            var mi = new MenuItem { Header = "Usuń" }; mi.Click += (s, e) => DeleteObj();
            if (kind == Kontrola.KPeron) { var sw = new MenuItem { Header = "Zmień stronę" }; sw.Click += (s, e) => Doc.Change("Strona peronu", p => { var x = p.Platforms.First(q => q.Id == id); x.Side = -x.Side; }); cm.Items.Add(sw); }
            if (kind == Kontrola.KObiekt) ObiektMenu(cm, id);
            if (kind == Kontrola.KKm) { var sw = new MenuItem { Header = "Odwróć kierunek kilometrażu" }; sw.Click += (s, e) => Doc.Change("Kierunek kilometrażu", p => { var x = p.KmPoints.First(q => q.Id == id); x.Dir = -x.Dir; }); cm.Items.Add(sw); }
            cm.Items.Add(mi);
            cm.PlacementTarget = View; cm.IsOpen = true;
        };
    }

    // ------------------------------------------------------------------ wstawianie
    void OnSectionPlace(string track, double s)
    {
        var o = View.OdcinkiNow(); int comp = o.At(track, s);
        if (comp >= 0 && o.Names[comp].Count > 0)
        {
            var ex = o.Names[comp][0];
            View.SelectObj(Kontrola.KOdcinek, ex.Id);
            Message($"Ten kawałek należy już do odcinka {ex.Name} ({F(o.Length(comp))} m) – nazwę zmienisz w panelu; nowy odcinek wymaga granicy (licznik osi / złącze izolowane)");
            return;
        }
        var name = AskName("Odcinek kontroli niezajętości", "Nazwa odcinka (np. It1, Iz5/7, It2-3)", Odcinki.Suggest(Doc.P, o, comp));
        if (string.IsNullOrEmpty(name)) return;
        if (Doc.P.Sections.Any(x => x.Name == name)) { Message($"Nazwa odcinka {name} jest już zajęta"); return; }
        // rodzaj kontroli: jak u sąsiada (najczęstszy w projekcie), domyślnie liczniki osi
        var kind = Doc.P.Sections.GroupBy(x => x.Kind).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault() ?? UtSection.LicznikOsi;
        var sec = new UtSection { Name = name, Track = track, S = s, Kind = kind };
        Doc.Change("Odcinek kontroli niezajętości", p => p.Sections.Add(sec));
        View.SelectObj(Kontrola.KOdcinek, sec.Id);
        var o2 = View.OdcinkiNow(); int c2 = o2.CompOf.TryGetValue(sec.Id, out var cc) ? cc : -1;
        Message($"Odcinek {name}: {(c2 >= 0 ? F(o2.Length(c2)) + " m – " + Odcinki.Where(Doc.P, o2, c2) : "poza torem")}");
    }

    void OnPlatformPlace(string track, double s0, double s1, int side)
    {
        int n = 1; while (Doc.P.Platforms.Any(x => x.Name == n.ToString())) n++;
        var name = AskName("Peron", "Numer peronu", n.ToString());
        if (name == null) return;
        var pl = new UtPlatform { Name = name, Track = track, S0 = s0, S1 = s1, Side = side };
        Doc.Change("Peron", p => p.Platforms.Add(pl));
        View.SelectObj(Kontrola.KPeron, pl.Id);
        Message($"Peron {name}: krawędź {F(pl.Len)} m po {(side > 0 ? "prawej" : "lewej")} stronie toru {TrNr(track)}");
    }

    void OnLevelCrossingPlace(string track, double s, double angle, double reach)
    {
        var name = AskName("Przejazd kolejowo-drogowy", "Opis (np. nazwa drogi), może być pusty", "");
        if (name == null) return;
        var c = new UtLevelCrossing { Name = name, Track = track, S = s, Angle = angle, Reach = reach };
        Doc.Change("Przejazd", p => p.LevelCrossings.Add(c));
        View.SelectObj(Kontrola.KPrzejazd, c.Id);
        Message($"Przejazd na torze {TrNr(track)} km {F(s)}, kąt {F(angle, "0")}°" + (Kilometraz.Text(Doc.P, track, s) is var km && km.Length > 0 ? ", " + km : ""));
    }

    void OnKmPlace(string track, double s, int dir)
    {
        var lines = Doc.P.KmPoints.Select(k => k.Line).Distinct().ToList();
        var cur = Kilometraz.At(Doc.P, View.Geometry(Doc.P.Tracks.First(x => x.Id == track)).At(s).P, track, s);
        string def = (lines.FirstOrDefault() ?? "") + " " + (cur.Count > 0 ? Kilometraz.Fmt(cur[0].Km) : "0.000");
        var txt = AskName("Kilometraż linii", "Numer linii i km w tym miejscu, np. „17 25.452”", def.Trim());
        if (txt == null) return;
        var parts = txt.Trim().Split(new[] { ' ', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries);
        string line = parts.Length >= 2 ? parts[0] : lines.FirstOrDefault() ?? "";
        if (parts.Length == 0 || Kilometraz.Parse(parts[^1]) is not double km) { Message("Kilometraż: wpisz numer linii i km, np. „17 25.452”"); return; }
        var k = new UtKmPoint { Line = line, Track = track, S = s, Km = km, Dir = dir };
        Doc.Change("Punkt kilometrażu", p => p.KmPoints.Add(k));
        View.SelectObj(Kontrola.KKm, k.Id);
        Message($"Kilometraż linii {line}: km {Kilometraz.Fmt(km)} na torze {TrNr(track)}, rośnie w stronę {(dir > 0 ? "rosnącego" : "malejącego")} km toru");
    }

    string TrNr(string id) => Doc.P.Tracks.FirstOrDefault(x => x.Id == id) is UtTrack t && t.Number.Length > 0 ? t.Number : "(bez numeru)";

    bool DeleteObj()
    {
        if (View.SelectedObj is not var (kind, id)) return false;
        string what = kind switch
        {
            Kontrola.KPeron => "Usunięcie peronu", Kontrola.KPrzejazd => "Usunięcie przejazdu", Kontrola.KKm => "Usunięcie punktu km",
            Kontrola.KPosterunek => "Usunięcie posterunku", Kontrola.KSzlak => "Usunięcie szlaku", Kontrola.KObiekt => "Usunięcie obiektu", _ => "Usunięcie odcinka",
        };
        Doc.Change(what, p =>
        {
            p.Platforms.RemoveAll(x => x.Id == id); p.LevelCrossings.RemoveAll(x => x.Id == id);
            p.KmPoints.RemoveAll(x => x.Id == id); p.Sections.RemoveAll(x => x.Id == id);
            p.Lines.RemoveAll(x => x.Id == id); p.Obiekty.RemoveAll(x => x.Id == id);
            // posterunek: tory zostają, bez posterunku
            if (p.Posts.RemoveAll(x => x.Id == id) > 0) foreach (var t in p.Tracks.Where(t => t.Post == id)) t.Post = null;
        });
        View.Select(null); Message(what + " (Ctrl+Z przywraca)");
        return true;
    }

    // ------------------------------------------------------------------ panele
    bool BuildWyposazeniePanel()
    {
        if (View.SelectedObj is not var (kind, id)) return false;
        var p = Doc.P;
        switch (kind)
        {
            case Kontrola.KOdcinek when p.Sections.FirstOrDefault(x => x.Id == id) is UtSection s: BuildSectionPanel(s); break;
            case Kontrola.KPeron when p.Platforms.FirstOrDefault(x => x.Id == id) is UtPlatform pl: BuildPlatformPanel(pl); break;
            case Kontrola.KPrzejazd when p.LevelCrossings.FirstOrDefault(x => x.Id == id) is UtLevelCrossing c: BuildLevelCrossingPanel(c); break;
            case Kontrola.KKm when p.KmPoints.FirstOrDefault(x => x.Id == id) is UtKmPoint k: BuildKmPanel(k); break;
            case Kontrola.KObiekt when p.Obiekty.FirstOrDefault(x => x.Id == id) is UtObiekt o: BuildObiektPanel(o); break;
            case Kontrola.KPosterunek when p.Posts.FirstOrDefault(x => x.Id == id) is UtPost po: BuildPostPanel(po); break;
            case Kontrola.KSzlak when p.Lines.FirstOrDefault(x => x.Id == id) is UtLine l: BuildLinePanel(l); break;
        }
        return true;
    }

    TextBox Field(StackPanel P, string label, string val, Action<string> commit, string tip = null)
    {
        P.Children.Add(Label(label));
        var tb = new TextBox { Text = val, Margin = new Thickness(0, 2, 0, 6), ToolTip = tip };
        string last = val;
        void C() { if (tb.Text.Trim() != last) { last = tb.Text.Trim(); commit(last); } }
        tb.LostFocus += (s, e) => C(); tb.KeyDown += (s, e) => { if (e.Key == Key.Enter) C(); };
        P.Children.Add(tb); return tb;
    }

    void Notes(StackPanel P, IEnumerable<string> notes)
    {
        foreach (var n in notes) P.Children.Add(new TextBlock { Text = n, Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 0, 0, 2) });
    }

    void BuildSectionPanel(UtSection s)
    {
        var P = TrackPanel; var o = View.OdcinkiNow();
        int comp = o.CompOf.TryGetValue(s.Id, out var c) ? c : -1;
        var rz = comp >= 0 ? Odcinki.Turnouts(Doc.P, o, comp) : new();
        P.Children.Add(Header($"Odcinek {s.Name}"));
        P.Children.Add(new TextBlock { Text = rz.Count > 0 ? $"odcinek zwrotnicowy (rozjazdy {string.Join(", ", rz.Select(x => x.Number))})" : "odcinek torowy", Foreground = Brushes.DimGray, FontSize = 12, Margin = new Thickness(0, -4, 0, 6) });
        Field(P, "Nazwa", s.Name, n =>
        {
            if (n.Length == 0 || Doc.P.Sections.Any(x => x.Id != s.Id && x.Name == n)) { Message(n.Length == 0 ? "Nazwa nie może być pusta" : $"Nazwa {n} jest już zajęta"); RefreshTrackPanel(); return; }
            Doc.Change("Nazwa odcinka", p => p.Sections.First(x => x.Id == s.Id).Name = n);
        });
        P.Children.Add(Label("Rodzaj kontroli niezajętości"));
        var kb = new ComboBox { Margin = new Thickness(0, 2, 0, 6) };
        foreach (var k in UtSection.Kinds) kb.Items.Add(k.Name);
        kb.SelectedIndex = Math.Max(0, Array.FindIndex(UtSection.Kinds, k => k.Key == s.Kind));
        kb.SelectionChanged += (q, e) => { if (kb.SelectedIndex >= 0) Doc.Change("Rodzaj odcinka", p => p.Sections.First(x => x.Id == s.Id).Kind = UtSection.Kinds[kb.SelectedIndex].Key); };
        P.Children.Add(kb);
        if (comp >= 0)
        {
            P.Children.Add(new TextBlock { Text = $"Długość {UtView.Len3(o.Length(comp))}", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
            var lines = o.Of(comp).Select(k => $"tor {TrNr(k.Track)}: km {F(k.S0)} – {F(k.S1)} ({F(k.Len)} m)");
            P.Children.Add(new TextBlock { Text = string.Join("\n", lines), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) });
            // granice: urządzenia na końcach kawałków
            var b = new List<string>();
            foreach (var k in o.Of(comp))
                foreach (var d in Doc.P.Devices.Where(d => d.Track == k.Track && Odcinki.IsBoundary(d) && (Math.Abs(d.S - k.S0) < 0.01 || Math.Abs(d.S - k.S1) < 0.01)))
                    b.Add($"{UtDevice.KindInfo(d.Kind).Name.ToLower()} {d.Name}".Trim());
            P.Children.Add(new TextBlock { Text = "Granice: " + (b.Count > 0 ? string.Join(", ", b.Distinct()) : "tylko końce torów"), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) });
            if (o.Names[comp].Count > 1) Notes(P, new[] { $"w tym kawałku są też odcinki: {string.Join(", ", o.Names[comp].Where(x => x.Id != s.Id).Select(x => x.Name))} – brak granicy" });
        }
        else Notes(P, new[] { "miejsce nazwy poza torem" });
        P.Children.Add(SmallBtn("Usuń", () => DeleteObj(), "Usuń odcinek (Del) – kawałek toru zostaje bez nazwy"));
        P.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 10) });
    }

    void BuildPlatformPanel(UtPlatform pl)
    {
        var P = TrackPanel;
        void Ch(string what, Action<UtPlatform> a) => Doc.Change(what, p => { var x = p.Platforms.FirstOrDefault(q => q.Id == pl.Id); if (x != null) a(x); });
        P.Children.Add(Header($"Peron {pl.Name}"));
        P.Children.Add(new TextBlock { Text = "Ig-10 zał. 8, s. 52; wykaz – zał. 5", Foreground = Brushes.DimGray, FontSize = 12, Margin = new Thickness(0, -4, 0, 6) });
        Field(P, "Numer peronu", pl.Name, n => Ch("Numer peronu", x => x.Name = n));
        Field(P, $"Tor {TrNr(pl.Track)} – krawędź od km / do km [m od początku toru]", $"{F(pl.S0)} – {F(pl.S1)}", t =>
        {
            var a = t.Split(new[] { '–', '-', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (a.Length == 2 && Num(a[0]) is double s0 && Num(a[1]) is double s1 && Math.Abs(s1 - s0) >= 1) Ch("Długość peronu", x => { x.S0 = Math.Min(s0, s1); x.S1 = Math.Max(s0, s1); });
            else Message("Peron: wpisz km początku i końca krawędzi, np. „120 – 320”");
        });
        P.Children.Add(new TextBlock { Text = $"Długość krawędzi {UtView.Len3(pl.Len)}" + (Kilometraz.Text(Doc.P, pl.Track, pl.S0) is var k0 && k0.Length > 0 ? $"\nPP {k0}, KP {Kilometraz.Text(Doc.P, pl.Track, pl.S1)}" : ""), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
        Field(P, "Odległość krawędzi od osi toru [m]", F(pl.Offset, "0.000"), t => { if (Num(t) is double v && v > 0.5 && v < 5) Ch("Odległość krawędzi peronu", x => x.Offset = v); });
        Field(P, "Wysokość krawędzi nad główką szyny [m]", F(pl.Height, "0.00"), t => { if (Num(t) is double v && v >= 0 && v < 2) Ch("Wysokość peronu", x => x.Height = v); });
        if (pl.Track2 == null) Field(P, "Szerokość peronu [m]", F(pl.Width), t => { if (Num(t) is double v && v > 0.5 && v < 50) Ch("Szerokość peronu", x => x.Width = v); });
        var row = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        row.Children.Add(SmallBtn("Zmień stronę", () => Ch("Strona peronu", x => x.Side = -x.Side), "Peron po drugiej stronie toru"));
        // druga krawędź: najbliższy tor po stronie peronu
        string other = NeighbourTrack(pl);
        if (pl.Track2 == null && other != null) row.Children.Add(SmallBtn($"Dwukrawędziowy (tor {TrNr(other)})", () => Ch("Peron dwukrawędziowy", x => x.Track2 = other), "Druga krawędź przy sąsiednim torze po stronie peronu (peron wyspowy)"));
        if (pl.Track2 != null) row.Children.Add(SmallBtn("Jednokrawędziowy", () => Ch("Peron jednokrawędziowy", x => x.Track2 = null), "Bez drugiej krawędzi"));
        P.Children.Add(row);
        if (pl.Track2 != null) P.Children.Add(new TextBlock { Text = $"Druga krawędź przy torze {TrNr(pl.Track2)}", FontSize = 12, Margin = new Thickness(0, 0, 0, 6) });
        P.Children.Add(SmallBtn("Usuń", () => DeleteObj(), "Usuń peron (Del)"));
        P.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 10) });
    }

    /// <summary>Najbliższy tor po stronie peronu (do 30 m od osi w środku peronu).</summary>
    string NeighbourTrack(UtPlatform pl)
    {
        var t = Doc.P.Tracks.FirstOrDefault(x => x.Id == pl.Track); if (t == null) return null;
        var r = View.Geometry(t); var (p, az) = r.At((pl.S0 + pl.S1) / 2);
        string best = null; double bd = 30;
        foreach (var u in Doc.P.Tracks.Where(x => x.Id != t.Id))
        {
            var ru = View.Geometry(u); if (ru.Elements.Count == 0) continue;
            if (Rozjazdy.RayHit(ru, p, az + pl.Side * Math.PI / 2) is var (_, d) && d / r.K < bd && d / r.K > pl.Offset * 2) { bd = d / r.K; best = u.Id; }
        }
        return best;
    }

    void BuildLevelCrossingPanel(UtLevelCrossing c)
    {
        var P = TrackPanel;
        void Ch(string what, Action<UtLevelCrossing> a) => Doc.Change(what, p => { var x = p.LevelCrossings.FirstOrDefault(q => q.Id == c.Id); if (x != null) a(x); });
        P.Children.Add(Header(char.ToUpper(UtLevelCrossing.CatName(c.Cat)[0]) + UtLevelCrossing.CatName(c.Cat)[1..] + (c.Name.Length > 0 ? " " + c.Name : "")));
        P.Children.Add(new TextBlock { Text = "Ig-10 § 4 ust. 1 pkt 13; zał. 8, s. 53, 57, 60, 64", Foreground = Brushes.DimGray, FontSize = 12, Margin = new Thickness(0, -4, 0, 6) });
        Field(P, "Opis (droga)", c.Name, n => Ch("Opis przejazdu", x => x.Name = n));
        P.Children.Add(Label("Kategoria"));
        var cat = new ComboBox { Margin = new Thickness(0, 2, 0, 6) };
        foreach (var k in UtLevelCrossing.Cats) cat.Items.Add(k == "P" ? "przejście (P)" : "kat. " + k);
        cat.SelectedIndex = Math.Max(0, Array.IndexOf(UtLevelCrossing.Cats, c.Cat));
        cat.SelectionChanged += (s, e) => { if (cat.SelectedIndex >= 0) Ch("Kategoria przejazdu", x => x.Cat = UtLevelCrossing.Cats[cat.SelectedIndex]); };
        P.Children.Add(cat);
        Field(P, $"Tor {TrNr(c.Track)} – km [m od początku toru]", F(c.S), t => { if (Num(t) is double v) Ch("Kilometraż przejazdu", x => x.S = v); });
        var km = Kilometraz.Text(Doc.P, c.Track, c.S);
        if (km.Length > 0) P.Children.Add(new TextBlock { Text = km, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, -2, 0, 6) });
        Field(P, "Kąt skrzyżowania z osią toru [°]", F(c.Angle, "0.#"), t => { if (Num(t) is double v && v > 0 && v < 180) Ch("Kąt przejazdu", x => x.Angle = v); });
        Field(P, "Szerokość drogi [m]", F(c.Width), t => { if (Num(t) is double v && v > 0.5 && v < 60) Ch("Szerokość drogi", x => x.Width = v); });
        Field(P, "Droga na planie – w obie strony od osi toru [m]", F(c.Reach), t => { if (Num(t) is double v && v >= 2 && v < 500) Ch("Długość drogi", x => x.Reach = v); });
        P.Children.Add(Label("Rogatki (Ig-10 s. 53)"));
        var rb = new ComboBox { Margin = new Thickness(0, 2, 0, 6) };
        var keys = new[] { "", UtLevelCrossing.RogatkiMech, UtLevelCrossing.RogatkiElektr };
        foreach (var k in new[] { "brak", "z napędem mechanicznym", "z napędem elektrycznym" }) rb.Items.Add(k);
        rb.SelectedIndex = Math.Max(0, Array.IndexOf(keys, c.Barriers ?? ""));
        rb.SelectionChanged += (s, e) => { if (rb.SelectedIndex >= 0) Ch("Rogatki", x => x.Barriers = keys[rb.SelectedIndex]); };
        P.Children.Add(rb);
        P.Children.Add(SmallBtn("Usuń", () => DeleteObj(), "Usuń przejazd (Del)"));
        P.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 10) });
    }

    void BuildKmPanel(UtKmPoint k)
    {
        var P = TrackPanel;
        void Ch(string what, Action<UtKmPoint> a) => Doc.Change(what, p => { var x = p.KmPoints.FirstOrDefault(q => q.Id == k.Id); if (x != null) a(x); });
        P.Children.Add(Header($"Kilometraż: km {Kilometraz.Fmt(k.Km)}"));
        P.Children.Add(new TextBlock { Text = "punkt odniesienia kilometrażu linii na osi toru (Ig-10 § 4 ust. 1 pkt 17–21)", Foreground = Brushes.DimGray, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, -4, 0, 6) });
        Field(P, "Numer linii", k.Line, n => Ch("Numer linii", x => x.Line = n));
        Field(P, "km linii w tym miejscu (np. 25.452)", Kilometraz.Fmt(k.Km), t => { if (Kilometraz.Parse(t) is double v) Ch("km punktu", x => x.Km = v); else Message("km: np. 25.452"); });
        Field(P, $"Tor {TrNr(k.Track)} – miejsce [m od początku toru]", F(k.S), t => { if (Num(t) is double v) Ch("Miejsce punktu km", x => x.S = v); });
        P.Children.Add(new TextBlock { Text = $"km linii rośnie w stronę {(k.Dir > 0 ? "rosnącego" : "malejącego")} km toru", Margin = new Thickness(0, 0, 0, 4) });
        P.Children.Add(SmallBtn("Odwróć kierunek", () => Ch("Kierunek kilometrażu", x => x.Dir = -x.Dir), "Kilometraż linii rośnie w drugą stronę"));
        Notes(P, Kilometraz.Check(Doc.P).Where(x => x.Track == k.Track).Select(x => x.Text));
        P.Children.Add(SmallBtn("Usuń", () => DeleteObj(), "Usuń punkt km (Del)"));
        P.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 10) });
    }

    /// <summary>Kilometraż i wysokość toru w panelu toru: początek i koniec, granica obszaru, profil podłużny.</summary>
    void TrackExtras(UtTrack t, TrasaResult r)
    {
        var P = TrackPanel; string id = t.Id;
        string k0 = Kilometraz.Text(Doc.P, id, 0), k1 = Kilometraz.Text(Doc.P, id, r.Length);
        if (k0.Length > 0) P.Children.Add(new TextBlock { Text = $"Początek {k0}\nKoniec {k1}", FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
        if (t.Profile.Count > 0)
        {
            var secs = Profil.Sections(t.Profile);
            P.Children.Add(new TextBlock { Text = $"Niweleta: {F(Profil.Z(t.Profile, 0))} → {F(Profil.Z(t.Profile, r.Length))} m n.p.m., pochylenia " + string.Join(" / ", secs.Select(x => F(x.Promil, "0.0") + "‰")), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) });
        }
        var ends = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        foreach (var start in new[] { true, false })
        {
            var cb = new CheckBox { Content = start ? "początek – granica obszaru" : "koniec – granica obszaru", IsChecked = start ? t.OpenStart : t.OpenEnd, Margin = new Thickness(0, 0, 10, 2), ToolTip = "Tor biegnie dalej poza projektem (np. szlak) – „Sprawdź” nie zgłasza otwartego końca" };
            bool st = start;
            cb.Click += (s, e) => ChTrack(id, "Granica obszaru", x => { if (st) x.OpenStart = cb.IsChecked == true; else x.OpenEnd = cb.IsChecked == true; });
            ends.Children.Add(cb);
        }
        P.Children.Add(ends);
    }
}

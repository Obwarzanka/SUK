using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorZobrazowania;

/// <summary>
/// Tory obrazu w oknie (etap Z2, PLAN_EDYTOR_ZOBRAZOWANIA.md § 4): narzędzia Wskaż / Tor / Łącznik, wybór toru UT (panel UT albo
/// lista torów przy węźle), kawałek toru UT (km od–do – pierwszy jeszcze nie narysowany), panel toru obrazu, podgląd UT.
/// </summary>
public partial class MainWindow
{
    public readonly UtPreview Preview = new() { Height = 150 };
    readonly Dictionary<ZobTool, ToggleButton> toolBtns = new();
    UtProject previewUt;
    readonly Dictionary<string, double> lenCache = new();
    UtProject lenUt;
    List<Styk> styki; UtProject stykiUt;
    /// <summary>Pola km od / do w panelu toru obrazu (ostatnio zbudowane – do testów).</summary>
    public TextBox KmFromBox { get; private set; }
    public TextBox KmToBox { get; private set; }

    ToggleButton ToolBtn(ZobTool t, string text, string tip)
    {
        var b = new ToggleButton { Content = text, ToolTip = tip, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(1, 0, 1, 0) };
        b.Click += (s, e) => SetTool(t);
        toolBtns[t] = b;
        return b;
    }

    void InitTory()
    {
        View.ToolChanged += ToolShown;
        View.TorDone += OnTorDone;
        View.TorNeedsUt += OnTorNeedsUt;
        View.LacznikDone += OnLacznik;
        View.SelectionChanged += () => { if (!refreshing && Doc != null) RefreshImgPanel(); RefreshPreview(); };
        View.VertexMoved += (id, i, pt) => Ch("Przesunięcie punktu toru", o => { var t = o.Tory.FirstOrDefault(x => x.Id == id); if (t != null && i < t.Pkt.Count) t.Pkt[i] = pt; });
        View.Info += Message;
        ToolShown();
    }

    public void SetTool(ZobTool t)
    {
        View.Tool = t; View.DraftUt = null;
        Message(t switch
        {
            ZobTool.Tor => "Tor: wybierz tor w panelu UT albo kliknij węzeł obrazu (koniec toru) – lista torów UT; potem punkty łamanej",
            ZobTool.Lacznik => "Łącznik: kliknij tor obrazu w miejscu pierwszej zwrotnicy, potem drugi tor",
            ZobTool.Miarka => "Miarka: klikaj punkty – długość w j. i w kratkach siatki; PPM – wyczyść, Esc – koniec",
            ZobTool.Zwrotnica or ZobTool.Sygnalizator or ZobTool.Wykolejnica or ZobTool.Koziol => "",
            _ => "Wskaż: klik na torze – zaznaczenie; przeciągnięcie wierzchołka; puste miejsce – przesuwanie widoku",
        });
        if (RodzajNarzedzia(t) is ZobRodzaj sr && Doc != null && Obraz != null) PickSym(sr, utSelected);
        // tor wybrany w panelu UT i jeszcze nie cały na obrazie – od razu jego brakujący kawałek
        if (t == ZobTool.Tor && Doc != null && Obraz != null && utSelected != null && Doc.Ut.Tracks.Any(x => x.Id == utSelected)
            && ZobTory.Luki(Obraz, utSelected, UtLen(utSelected)).Count > 0) StartTor(utSelected);
    }

    void ToolShown()
    {
        foreach (var (t, b) in toolBtns) { b.IsChecked = t == View.Tool; b.Background = t == View.Tool ? TabOn : Brushes.Transparent; }
        RefreshPreview();
    }

    /// <summary>Po nowym dokumencie: bez zaznaczenia i rysowania.</summary>
    void ResetTools()
    {
        View.CancelDraft(); View.CancelLacznik(); View.DraftUt = null; View.Highlight = null; View.Select(null); utSelected = null;
    }

    void RefreshPreview()
    {
        if (Doc?.Ut != previewUt) { previewUt = Doc?.Ut; Preview.SetUt(previewUt); }
        Preview.Drawn = Obraz?.Tory.Select(t => t.Ut?.Tor).Where(x => x != null).ToHashSet() ?? new();
        Preview.Highlight = View.DraftUt ?? SelectedTor?.Ut?.Tor ?? View.Highlight;
        Preview.InvalidateVisual();
    }

    public ZobTor SelectedTor => Obraz?.Tory.FirstOrDefault(t => t.Id == View.Selected);

    double UtLen(string id)
    {
        if (lenUt != Doc.Ut) { lenCache.Clear(); lenUt = Doc.Ut; }
        if (!lenCache.TryGetValue(id, out var l))
            lenCache[id] = l = Doc.Ut.Tracks.FirstOrDefault(t => t.Id == id) is UtTrack t ? Trasa.Compute(t).Length : 0;
        return l;
    }

    List<Styk> Styki()
    {
        if (stykiUt != Doc.Ut) { stykiUt = Doc.Ut; styki = Topologia.Styki(Doc.Ut); }
        return styki;
    }

    /// <summary>Opis toru UT jak w panelu UT (np. „tor 1 (gz)”, „tor łączący 1/2”).</summary>
    string TorName(string utId) =>
        ZobUt.Obiekty(Doc.Ut, Obraz).FirstOrDefault(x => x.Id == utId)?.Opis
        ?? (Doc.Ut.Tracks.FirstOrDefault(t => t.Id == utId) is UtTrack t && t.Number.Length > 0 ? $"tor {t.Number}" : "tor (spoza obrazu)");

    static string Km(double s) => s.ToString("0.##", CultureInfo.CurrentCulture);

    /// <summary>Obiekt wybrany w panelu UT: podświetlenie, a w narzędziu Tor – początek rysowania tego toru.</summary>
    void UtClicked(ZobUtObj o)
    {
        utSelected = o.Id;
        bool tor = o.Grupa == ZobUt.GTory;
        View.Highlight = tor ? o.Id : null;
        if (!tor && UtSymClicked(o)) { RefreshPreview(); View.InvalidateVisual(); return; }
        if (tor && View.Tool == ZobTool.Tor) StartTor(o.Id);
        else if (tor && View.Tool == ZobTool.Lacznik) Message($"Łącznik: {o.Opis} – kliknij tor obrazu w miejscu pierwszej zwrotnicy, potem drugi tor");
        else if (tor && Obraz.Tory.FirstOrDefault(t => t.Ut?.Tor == o.Id) is ZobTor zt) { View.Select(zt.Id); Message($"{o.Opis} – na obrazie" + (o.Czesc != null ? $" w części ({o.Czesc})" : "")); }
        else Message(o.NaObrazie ? $"{o.Opis} – na obrazie" : $"{o.Opis} – nie ma go jeszcze na obrazie" + (tor ? " (narzędzie Tor – T)" : ""));
        RefreshPreview(); View.InvalidateVisual();
    }

    /// <summary>Narzędzie Tor dla toru UT: kawałek = pierwszy jeszcze nie narysowany przedział km.</summary>
    public bool StartTor(string utId)
    {
        var luki = ZobTory.Luki(Obraz, utId, UtLen(utId));
        if (luki.Count == 0)
        {
            View.DraftUt = null;
            Message($"{TorName(utId)} jest już na obrazie w całości – ten sam km nie może być dwa razy na jednym obrazie");
            return false;
        }
        if (View.Tool != ZobTool.Tor) View.Tool = ZobTool.Tor;
        View.CancelDraft(); View.DraftUt = utId; View.Highlight = utId;
        var (a, b) = luki[0];
        Message($"Tor: {TorName(utId)}, km {Km(a)}–{Km(b)} – klikaj punkty łamanej (Shift – poziomo, Ctrl – co 45°); koniec: klik na węźle, PPM, Enter, dwuklik; Esc – rezygnacja");
        RefreshPreview();
        return true;
    }

    /// <summary>Narzędzie Tor od węzła obrazu: tor UT i pierwszy punkt.</summary>
    public bool StartTorAt(string utId, int[] p, string torObrazu = null)
    {
        if (!StartTor(utId)) return false;
        View.StartDraft(p, torObrazu);
        return true;
    }

    /// <summary>Tory UT obrazu jeszcze nie narysowane w całości; przy węźle toru obrazu – najpierw tory połączone z jego torem UT.</summary>
    public List<(string Id, string Opis)> TorCandidates(string torObrazu)
    {
        var all = ZobUt.Obiekty(Doc.Ut, Obraz).Where(x => x.Grupa == ZobUt.GTory && !x.NaObrazie).ToList();
        var near = torObrazu != null && Obraz.Tory.FirstOrDefault(t => t.Id == torObrazu)?.Ut?.Tor is string ut ? ZobTory.Sasiednie(Doc.Ut, ut, Styki()) : new HashSet<string>();
        return all.OrderBy(x => near.Contains(x.Id) ? 0 : 1).Select(x => (x.Id, x.Opis)).ToList();
    }

    void OnTorNeedsUt(int[] p, string tor)
    {
        var c = TorCandidates(tor);
        if (c.Count == 0) { Message("Wszystkie tory UT tego obrazu są już narysowane"); return; }
        if (NoPrompt) { Message("Tor: wybierz tor UT w panelu UT"); return; }
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = "Który tor UT rysować od tego miejsca?", IsEnabled = false });
        foreach (var (id, opis) in c)
        {
            var mi = new MenuItem { Header = opis };
            mi.Click += (s, e) => StartTorAt(id, p, tor);
            menu.Items.Add(mi);
        }
        menu.PlacementTarget = View; menu.IsOpen = true;
    }

    void OnTorDone(List<int[]> pts)
    {
        var ut = View.DraftUt; if (ut == null || Obraz == null) return;
        var luki = ZobTory.Luki(Obraz, ut, UtLen(ut));
        if (luki.Count == 0) { Message($"{TorName(ut)} jest już na obrazie w całości"); return; }
        var (a, b) = luki[0];
        // kierunek km: z toru UT, z którym styka się węzeł początkowy albo końcowy rysowania (jak w poprzednim edytorze – tor od węzła)
        bool? startAtKm0 = null;
        string Ut(string tor) => Obraz.Tory.FirstOrDefault(x => x.Id == tor)?.Ut?.Tor;
        if (View.DraftStartTor is string st && Ut(st) is string su && ZobTory.KoniecPrzy(Doc.Ut, ut, su, Styki()) is bool k1) startAtKm0 = k1;
        else if (View.DraftEndTor is string et && Ut(et) is string eu && ZobTory.KoniecPrzy(Doc.Ut, ut, eu, Styki()) is bool k2) startAtKm0 = !k2;
        bool rev = startAtKm0 == false;
        var t = new ZobTor { Ut = new ZobUtTor { Tor = ut, S0 = Math.Round(rev ? b : a, 2), S1 = Math.Round(rev ? a : b, 2) }, Pkt = pts };
        Ch("Tor obrazu", o => o.Tory.Add(t));
        View.DraftUt = null;
        int odd = pts.Zip(pts.Skip(1)).Count(x => !ZobTory.Osiowy(x.First, x.Second));
        Message($"{TorName(ut)} na obrazie: km {Km(t.Ut.S0)}–{Km(t.Ut.S1)}, punktów {pts.Count}" + (odd > 0 ? $" – {odd} odcinków nie co 45° (czerwona kreska)" : "") + ". Następny tor: wybierz w panelu UT albo kliknij węzeł");
        RefreshPreview();
    }

    void OnLacznik(string ta, int[] pa, string tb, int[] qb)
    {
        var A = Obraz.Tory.FirstOrDefault(t => t.Id == ta); var B = Obraz.Tory.FirstOrDefault(t => t.Id == tb);
        if (A == null || B == null) return;
        int g = Doc.P.Siatka;
        var rb = ZobTory.Rzut(B.Pkt, qb[0], qb[1]);
        if (B.Pkt[rb.Seg][1] != B.Pkt[rb.Seg + 1][1]) { Message("Łącznik rysuje się między torami poziomymi – tu narysuj tor łączący narzędziem Tor"); return; }
        int yB = B.Pkt[rb.Seg][1];
        var cands = ZobTory.Laczniki(Doc.Ut, A.Ut.Tor, B.Ut.Tor).Where(c => ZobTory.Luki(Obraz, c.Track, UtLen(c.Track)).Count > 0).ToList();
        if (cands.Count == 0) { Message($"Między torami UT {TorName(A.Ut.Tor)} i {TorName(B.Ut.Tor)} nie ma toru łączącego dwóch rozjazdów, który nie jest jeszcze na obrazie"); return; }
        // tor łącznika: wybrany w panelu UT, inaczej ten, którego rozjazd na torze A leży najbliżej km wskazanego miejsca
        var pick = cands.FirstOrDefault(c => c.Track == utSelected);
        if (pick.Track == null)
        {
            double km = A.Ut.S0 + ZobTory.Rzut(A.Pkt, pa[0], pa[1]).Frac * (A.Ut.S1 - A.Ut.S0);
            pick = cands.OrderBy(c => Math.Abs(c.KmA - km)).First();
        }
        var pts = ZobTory.Lacznik(pa, qb[0], yB, g);
        if (pts == null) { Message("Łącznik: tory bliżej niż 2 kroki siatki – odsuń tory albo narysuj tor łączący narzędziem Tor"); return; }
        if (ZobTory.Rzut(B.Pkt, pts[^1][0], pts[^1][1]).Dist > 0.5) { Message($"Łącznik: koniec ({pts[^1][0]}, {pts[^1][1]}) wypada poza torem {TorName(B.Ut.Tor)} – kliknij dalej od końca toru"); return; }
        double len = UtLen(pick.Track);
        var t = new ZobTor { Ut = new ZobUtTor { Tor = pick.Track, S0 = pick.StartAtA ? 0 : Math.Round(len, 2), S1 = pick.StartAtA ? Math.Round(len, 2) : 0 }, Pkt = pts };
        Ch("Łącznik", o => o.Tory.Add(t));
        Message($"Łącznik: {TorName(pick.Track)} – {string.Join(" – ", pts.Select(q => $"({q[0]}, {q[1]})"))}");
    }

    public void DeleteSelected()
    {
        if (View.SelectedSym != null) { DeleteSym(); return; }
        var t = SelectedTor; if (t == null) return;
        var name = TorName(t.Ut.Tor);
        Ch("Zdjęcie toru z obrazu", o => o.Tory.RemoveAll(x => x.Id == t.Id));
        View.Select(null);
        Message($"{name} zdjęty z obrazu (w UT zostaje; Ctrl+Z przywraca)");
    }

    public void MoveSelected(int dx, int dy)
    {
        var t = SelectedTor; if (t == null) return;
        var id = t.Id;
        Ch("Przesunięcie toru", o =>
        {
            var x = o.Tory.First(q => q.Id == id);
            // symbole stojące na tym torze (obiekty UT tego toru) jadą razem z nim, z opisami
            foreach (ZobRodzaj r in Enum.GetValues<ZobRodzaj>())
                foreach (var s in ZobSymbole.Lista(o, r))
                    if (s.P != null && ZobSymbole.Miejsce(Doc.Ut, s.Ut)?.Track == x.Ut.Tor && ZobSymbole.NaTorze(x, s.P))
                    {
                        s.P = new[] { s.P[0] + dx, s.P[1] + dy };
                        if (s.Opis != null) s.Opis = new[] { s.Opis[0] + dx, s.Opis[1] + dy };
                    }
            x.Pkt = x.Pkt.Select(q => new[] { q[0] + dx, q[1] + dy }).ToList();
        });
    }

    void Reverse()
    {
        var t = SelectedTor; if (t == null) return;
        Ch("Odwrócenie kierunku km", o => { var x = o.Tory.First(q => q.Id == t.Id); (x.Ut.S0, x.Ut.S1) = (x.Ut.S1, x.Ut.S0); });
    }

    void CommitKm()
    {
        var t = SelectedTor; if (t == null || refreshing || KmFromBox == null) return;
        double? N(string s) => double.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
        double len = UtLen(t.Ut.Tor);
        if (N(KmFromBox.Text) is not double a || N(KmToBox.Text) is not double b || a < -0.01 || b < -0.01 || a > len + 0.01 || b > len + 0.01 || Math.Abs(a - b) < 0.5)
        { Message($"Km od – do: liczby od 0 do {Km(len)} m, różne o co najmniej 0,5 m"); RefreshImgPanel(); return; }
        a = Math.Round(Math.Clamp(a, 0, len), 2); b = Math.Round(Math.Clamp(b, 0, len), 2);
        if (a == t.Ut.S0 && b == t.Ut.S1) return;
        if (ZobTory.Zachodzi(Obraz, t.Ut.Tor, a, b, t.Id)) { Message("Ten kilometraż jest już na obrazie w innym kawałku tego toru"); RefreshImgPanel(); return; }
        var id = t.Id;
        Ch("Kilometraż toru obrazu", o => { var x = o.Tory.First(q => q.Id == id); x.Ut.S0 = a; x.Ut.S1 = b; });
    }

    /// <summary>Panel zaznaczonego toru obrazu (nad panelem obrazu).</summary>
    void BuildTrackPanel()
    {
        KmFromBox = KmToBox = null;
        var t = SelectedTor; if (t == null) return;
        var P = ImgPanel;
        P.Children.Add(Header("Tor obrazu"));
        P.Children.Add(new TextBlock { Text = $"UT: {TorName(t.Ut.Tor)}", TextWrapping = TextWrapping.Wrap });
        double len = UtLen(t.Ut.Tor);
        P.Children.Add(new TextBlock { Text = $"długość toru w UT {len:0.00} m – tylko do odczytu", Foreground = Brushes.DimGray, FontSize = 12, Margin = new Thickness(0, 0, 0, 6) });
        var lk = Label("Kawałek toru UT: km od – do [m od początku toru]"); lk.TextWrapping = TextWrapping.Wrap; P.Children.Add(lk);
        KmFromBox = new TextBox { Width = 90, Margin = new Thickness(0, 2, 6, 6), Text = Km(t.Ut.S0) };
        KmToBox = new TextBox { Width = 90, Margin = new Thickness(0, 2, 0, 6), Text = Km(t.Ut.S1) };
        foreach (var box in new[] { KmFromBox, KmToBox })
        {
            box.LostFocus += (s, e) => CommitKm();
            box.KeyDown += (s, e) => { if (e.Key == Key.Enter) CommitKm(); };
        }
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(KmFromBox); row.Children.Add(new TextBlock { Text = "–", Margin = new Thickness(0, 4, 6, 0) }); row.Children.Add(KmToBox);
        P.Children.Add(row);
        P.Children.Add(new TextBlock { Text = "Pierwszy punkt łamanej to „km od”. Długości na obrazie nie muszą się zgadzać z UT – to schemat.", Foreground = Brushes.DimGray, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) });
        int odd = t.Pkt.Zip(t.Pkt.Skip(1)).Count(x => !ZobTory.Osiowy(x.First, x.Second));
        P.Children.Add(new TextBlock { Text = $"Punktów łamanej: {t.Pkt.Count}" + (odd > 0 ? $"; {odd} odcinków nie co 45°" : ""), Foreground = odd > 0 ? MissFg : Brushes.Black, Margin = new Thickness(0, 0, 0, 6) });
        var btns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        btns.Children.Add(Btn("Odwróć kierunek km", (s, e) => Reverse(), "Zamień km od i do – pierwszy punkt łamanej odpowiada drugiemu końcowi kawałka toru UT"));
        var del = Btn("Usuń z obrazu", (s, e) => DeleteSelected(), "Zdejmij tor z obrazu (Del); w UT zostaje"); del.Margin = new Thickness(6, 0, 0, 0);
        btns.Children.Add(del);
        P.Children.Add(btns);
        P.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 10) });
    }

    /// <summary>Klawisze narzędzi; true – obsłużony (wołane też z testów).</summary>
    public bool ToolKey(Key k, bool shift = false)
    {
        int g = Doc?.P.Siatka ?? 8, step = shift ? 5 * g : g;
        switch (k)
        {
            case Key.V: SetTool(ZobTool.Wskaz); break;
            case Key.T: SetTool(ZobTool.Tor); break;
            case Key.R: SetTool(ZobTool.Lacznik); break;
            case Key.M: SetTool(ZobTool.Miarka); break;
            case Key.Z: SetTool(ZobTool.Zwrotnica); break;
            case Key.S: SetTool(ZobTool.Sygnalizator); break;
            case Key.W: SetTool(ZobTool.Wykolejnica); break;
            case Key.K: SetTool(ZobTool.Koziol); break;
            case Key.Enter when View.Tool == ZobTool.Tor: View.FinishDraft(); break;
            case Key.Back when View.Tool == ZobTool.Tor: View.UndoPoint(); break;
            case Key.Escape:
                if (View.Tool == ZobTool.Tor && View.Draft.Count > 0) View.CancelDraft();
                else if (View.Tool == ZobTool.Lacznik && View.LacznikFrom != null) View.CancelLacznik();
                else SetTool(ZobTool.Wskaz);
                break;
            case Key.Delete when View.Tool == ZobTool.Wskaz: DeleteSelected(); break;
            case Key.Left when SelectedTor != null: MoveSelected(-step, 0); break;
            case Key.Right when SelectedTor != null: MoveSelected(step, 0); break;
            case Key.Up when SelectedTor != null: MoveSelected(0, -step); break;
            case Key.Down when SelectedTor != null: MoveSelected(0, step); break;
            default: return false;
        }
        return true;
    }
}

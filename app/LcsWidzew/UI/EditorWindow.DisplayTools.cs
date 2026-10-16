using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LcsWidzew.Model;

namespace LcsWidzew.UI;

/// <summary>
/// Narzędzia edytora zobrazowania: rysowanie obrazu elementów, które już są w układzie torowym (UT), i przypisywanie
/// rysunku do identyfikatorów UT. Nic tu nie tworzy torów, zwrotnic ani sygnalizatorów w układzie – zmienia się tylko
/// sekcja „display” (położenia węzłów i przebieg torów na obrazach pulpitu).
/// • Tor (T): ciąg torów UT od zwrotnicy / końca do zwrotnicy / końca (przez sygnalizatory i zwykłe węzły) rysowany
///   łamaną; węzły pośrednie (np. semafory) – wzdłuż łamanej wg długości z UT albo w miejscu, gdzie już stoją.
/// • Semafor (S), Zwrotnica (Z), Kozioł (K), Wykolejnica (W): klik – umieszczenie wybranego elementu UT w tym miejscu.
/// • Wskaż: klik na torze obrazu – zaznaczenie toru; panel – właściwości elementu z UT i „Przypisz do …”.
/// </summary>
public partial class EditorWindow
{
    /// <summary>Ciąg torów UT: od węzła From przez Edges (kolejno) do węzła To.</summary>
    sealed record DChain(string From, List<string> Edges, string To);
    DChain dChain;                                  // rysowany ciąg
    readonly List<Point> dChainPts = new();         // punkty łamanej (pierwszy – początek ciągu)
    string dEdge;                                   // zaznaczony tor obrazu

    /// <summary>Narzędzia dostępne w edytorze zobrazowania.</summary>
    static bool DispTool(Tool t) => t is Tool.Select or Tool.Track or Tool.Signal or Tool.Switch or Tool.TrackEnd or Tool.Derailer or Tool.Measure || DecoTool(t);
    static bool DPlaceTool(Tool t) => t is Tool.Signal or Tool.Switch or Tool.TrackEnd or Tool.Derailer;

    static readonly Dictionary<Tool, string> DispTips = new()
    {
        [Tool.Select] = "Wskaż: klik na węźle (semafor, zwrotnica…) albo na torze – zaznaczenie i właściwości z układu torowego; przeciąganie węzła – przesuwa go na obrazie. Przeciąganie pustego miejsca – przesuwanie widoku, kółko – powiększenie, Shift+przeciąganie – prostokąt. Del – usuń z obrazu (układ bez zmian).",
        [Tool.Track] = "Tor na obrazie: klik na węźle obrazu (np. zwrotnicy) albo w wolnym miejscu – wybór toru z układu (UT), który jeszcze nie jest na obrazie; dalej klikaj punkty łamanej, klik na węźle końcowym albo PPM / Enter / dwuklik – koniec. Rysuje cały ciąg od zwrotnicy do zwrotnicy (przez semafory – stają wzdłuż linii wg długości z UT albo tam, gdzie już stoją). Shift – poziomo, Ctrl – 45°. Nie tworzy torów w układzie.",
        [Tool.Signal] = "Semafor / tarcza na obrazie: klik – umieszcza w tym miejscu wybrany sygnalizator z układu (UT), którego jeszcze nie ma na obrazie. Nie tworzy sygnalizatorów w układzie.",
        [Tool.Switch] = "Zwrotnica na obrazie: klik – umieszcza w tym miejscu wybraną zwrotnicę z układu (UT), której jeszcze nie ma na obrazie.",
        [Tool.TrackEnd] = "Kozioł / koniec toru na obrazie: klik – umieszcza w tym miejscu wybrany kozioł z układu (UT).",
        [Tool.Derailer] = "Wykolejnica na obrazie: klik – umieszcza w tym miejscu wybraną wykolejnicę z układu (UT).",
        [Tool.Measure] = "Miarka na obrazie: klikaj punkty – odległości w jednostkach obrazu (j.) i kratkach siatki. Dwuklik / Enter / PPM – koniec, Esc – wyczyść.",
    };
    readonly Dictionary<Tool, object> utTips = new();

    /// <summary>Paski narzędzi w edytorze zobrazowania: narzędzia obrazu z podpowiedziami „na obrazie”; w UT – podpowiedzi UT.</summary>
    void ApplyDispToolTips()
    {
        foreach (var (t, b) in toolBtns)
        {
            if (!utTips.ContainsKey(t)) utTips[t] = b.ToolTip;
            var key = ((string)utTips[t]).Split("  [").Skip(1).FirstOrDefault();
            b.ToolTip = dispMode && DispTips.TryGetValue(t, out var tip) ? tip + (key != null ? "  [" + key : "") : utTips[t];
        }
    }

    // ------------------------------------------------------------------ ciągi torów UT
    bool ChainBreak(string n)
    {
        var nd = st.Nodes[n];
        return nd.Switch != null || nd.Edges.Count(e => !st.Edges[e].Link) != 2 || nd.Edges.Any(e => st.Edges[e].Link);
    }
    string OtherEnd(string e, string n) => st.Edges[e].A == n ? st.Edges[e].B : st.Edges[e].A;

    /// <summary>Ciąg zawierający tor e (od węzła rozgałęzienia / końca do węzła rozgałęzienia / końca).</summary>
    DChain ChainOf(string e)
    {
        string cur = st.Edges[e].A, ce = e; int guard = 0;
        while (!ChainBreak(cur) && guard++ < 10000)
        {
            var nx = st.Nodes[cur].Edges.FirstOrDefault(x => x != ce && !st.Edges[x].Link);
            if (nx == null || nx == e) break;
            ce = nx; cur = OtherEnd(nx, cur);
        }
        return ChainFrom(cur, ce);
    }
    DChain ChainFrom(string n, string e)
    {
        var edges = new List<string> { e }; var cur = OtherEnd(e, n); int guard = 0;
        while (cur != n && !ChainBreak(cur) && guard++ < 10000)
        {
            var nx = st.Nodes[cur].Edges.FirstOrDefault(x => !edges.Contains(x) && !st.Edges[x].Link);
            if (nx == null) break;
            edges.Add(nx); cur = OtherEnd(nx, cur);
        }
        return new DChain(n, edges, cur);
    }
    static DChain Reverse(DChain c) { var l = c.Edges.ToList(); l.Reverse(); return new DChain(c.To, l, c.From); }
    List<DChain> AllChains()
    {
        var seen = new HashSet<string>(); var res = new List<DChain>();
        foreach (var e in st.Edges.Values.Where(x => !x.Link).OrderBy(x => x.Id))
        {
            if (seen.Contains(e.Id)) continue;
            var c = ChainOf(e.Id); foreach (var x in c.Edges) seen.Add(x); res.Add(c);
        }
        return res;
    }
    bool Placed(string n) => doc.DPos(n) != null;
    bool ChainOnDisplay(DChain c) => c.Edges.All(doc.OnDisplay);

    /// <summary>Opis węzła dla wyboru: zwrotnica, sygnalizatory, kozioł, koniec toru.</summary>
    string NDesc(string n)
    {
        var nd = st.Nodes[n];
        if (nd.Switch != null) return "zwr. " + st.Switches[nd.Switch].Label;
        if (nd.Signals.Count > 0) return string.Join(", ", nd.Signals.Select(s => st.Signals.TryGetValue(s, out var sd) ? sd.Label : Station.Lbl(s)));
        if (st.TrackEnds.ContainsKey(n)) return "kozioł";
        if (nd.Edges.Any(e => st.Edges[e].Link)) return "łącznik obrazów";
        if (nd.Edges.Count == 1) return "koniec toru";
        return n;
    }
    string ChainDesc(DChain c)
    {
        var e0 = st.Edges[c.Edges[0]];
        var tr = c.Edges.Select(x => st.Edges[x].Track).FirstOrDefault(t => t != "");
        var inner = new List<string>(); var cur = c.From;
        foreach (var e in c.Edges.Take(c.Edges.Count - 1)) { cur = OtherEnd(e, cur); inner.AddRange(st.Nodes[cur].Signals.Select(s => st.Signals.TryGetValue(s, out var sd) ? sd.Label : Station.Lbl(s))); }
        double len = c.Edges.Sum(x => st.Edges[x].Len);
        var txt = $"{(tr != null ? "tor " + tr + " " : "")}odc. {Station.Lbl(e0.Sec)}: {NDesc(c.From)} → {NDesc(c.To)}{(inner.Count > 0 ? " (przez " + string.Join(", ", inner) + ")" : "")}, {len:0} m [{c.Edges[0]}]";
        return txt.Replace("|", "/");
    }

    /// <summary>Wybór z listy (okno z rozwijaną listą); test losowy bez odpowiedzi – pierwsza pozycja.</summary>
    int Choose(string title, string label, List<string> opts)
    {
        if (opts.Count == 0) return -1;
        if (opts.Count == 1) return 0;
        if (FuzzMode && AutoAnswers.Count == 0) return 0;
        var r = Ask(title, (label, string.Join("|", opts.Select(o => o.Replace("|", "/")))));
        if (r == null) return -1;
        var k = opts.IndexOf(r[0]); if (k < 0) k = opts.FindIndex(o => o.Contains(r[0]));      // test: fragment opisu (np. „[e12]”)
        return Math.Max(0, k);
    }

    // ------------------------------------------------------------------ mysz (zobrazowanie)
    /// <summary>Narzędzia obrazu (tor, umieszczanie, miarka). True – kliknięcie obsłużone.</summary>
    bool DToolDown(Point w, MouseButtonEventArgs e)
    {
        if (tool == Tool.Measure) { MeasureDown(w, e); panel.InvalidateVisual(); return true; }
        if (tool == Tool.Track) { DChainDown(w, e); panel.InvalidateVisual(); return true; }
        if (DPlaceTool(tool)) { DPlaceDown(w); panel.InvalidateVisual(); return true; }
        return false;
    }

    Point DChainPoint(Point w)
    {
        var p = Snap(w);
        if (dChainPts.Count == 0) return p;
        var last = dChainPts[^1];
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) p.Y = last.Y;
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) p = Snap45((last.X, last.Y), p);
        return p;
    }

    void DChainDown(Point w, MouseButtonEventArgs e)
    {
        if (dChain == null) { DChainStart(w); return; }
        var nid = DPickNode(w);
        if (nid != null && nid == dChain.To) { DChainFinish(); return; }
        if (e?.ClickCount == 2) { DChainFinish(); return; }
        if (nid != null && nid != dChain.From && ChainInner(dChain).Contains(nid) && doc.DPos(nid) is { } ip) { var q = new Point(ip.X, ip.Y); if ((dChainPts[^1] - q).Length >= 0.5) dChainPts.Add(q); return; }
        if (nid != null && nid != dChain.From) { status.Text = $"✗ węzeł {NDesc(nid)} nie jest końcem tego toru (koniec: {NDesc(dChain.To)}) – kliknij obok albo zakończ PPM / Enter"; return; }
        var p = DChainPoint(w);
        if (dChainPts.Count > 0 && (dChainPts[^1] - p).Length < 0.5) return;
        dChainPts.Add(p);
        status.Text = $"Tor na obrazie: {ChainDesc(dChain)} – punkty {dChainPts.Count}; klik na {NDesc(dChain.To)}{(Placed(dChain.To) ? "" : " (jeszcze nie na obrazie)")} albo PPM / Enter / dwuklik – koniec, Backspace – cofnij punkt, Esc – rezygnacja";
    }

    List<string> DChainNodes(DChain c) { var r = new List<string> { c.From }; r.AddRange(ChainInner(c)); r.Add(c.To); return r.Where(n => DS.Nodes.TryGetValue(n, out var x) && x.X > Station.Hidden + 1).Distinct().ToList(); }
    /// <summary>Tory obrazu, których oba końce są zaznaczone (przesuwają się w całości).</summary>
    List<string> DSelEdges() => DS == null ? new() : DS.Edges.Values.Where(e => !e.Link && e.Pts.Count >= 2 && e.Pts[0].X > Station.Hidden + 1 && dSel.Contains(e.A) && dSel.Contains(e.B)).Select(e => e.Id).ToList();
    List<string> ChainInner(DChain c) { var r = new List<string>(); var cur = c.From; foreach (var e in c.Edges.Take(c.Edges.Count - 1)) { cur = OtherEnd(e, cur); r.Add(cur); } return r; }

    void DChainStart(Point w)
    {
        var nid = DPickNode(w);
        var cands = new List<DChain>();
        foreach (var c in AllChains().Where(c => !ChainOnDisplay(c)))
        {
            if (nid != null) { if (c.From == nid) cands.Add(c); else if (c.To == nid) cands.Add(Reverse(c)); }
            else if (!Placed(c.From)) cands.Add(c);
            else if (!Placed(c.To)) cands.Add(Reverse(c));
        }
        if (cands.Count == 0)
        {
            status.Text = nid != null ? $"✗ z węzła {NDesc(nid)} nie wychodzi żaden tor UT, którego nie ma na obrazie" : "✗ brak torów UT do narysowania od wolnego miejsca – zacznij klikiem na węźle obrazu (np. zwrotnicy), z którego tor wychodzi";
            return;
        }
        // wybór z listy „Nieumieszczone” w panelu – bez pytania
        DChain pick = null;
        if (AutoAnswers.Count == 0 && dHiddenSel is string pe) pick = cands.FirstOrDefault(c => c.Edges.Contains(pe));
        pick ??= cands[PanelPick(cands.Select(ChainDesc).ToList(), null)];
        dChain = pick; dChainPts.Clear(); dCands = cands;
        var start = doc.DPos(pick.From) is { } sp ? new Point(sp.X, sp.Y) : Snap(w);
        dChainPts.Add(start);
        status.Text = $"Tor na obrazie: {ChainDesc(pick)} – klikaj punkty łamanej; klik na {NDesc(pick.To)}{(Placed(pick.To) ? "" : " (jeszcze nie na obrazie – koniec tam, gdzie zakończysz)")} albo PPM / Enter / dwuklik – koniec" + (cands.Count > 1 ? $"   ·  inny tor z tego węzła ({cands.Count}) – lista w panelu" : "");
        DClearSel(); ShowProps();
    }

    List<DChain> dCands;                 // tory UT do wyboru przy rysowanym ciągu (lista w panelu, bez okna)
    string dPlaceSel;                    // element UT wybrany w panelu dla narzędzi Zwrotnica / Semafor / Kozioł / Wykolejnica
    string dHiddenSel;                   // pozycja zaznaczona na liście „Nieumieszczone” (zostaje po odświeżeniu panelu)

    /// <summary>Wybór bez okna: test – odpowiedź z kolejki (fragment opisu), inaczej pozycja wybrana w panelu albo pierwsza.</summary>
    int PanelPick(List<string> opts, string panelKey)
    {
        if (AutoAnswers.Count > 0) { var r = AutoAnswers.Dequeue()[0]; var k = opts.FindIndex(o => o == r || o.Contains(r)); return Math.Max(0, k); }
        var j = panelKey == null ? -1 : opts.FindIndex(o => o.Contains("[" + panelKey + "]"));
        return Math.Max(0, j);
    }

    /// <summary>Panel narzędzi obrazu: lista torów do wyboru (rysowany ciąg) albo elementów UT do umieszczenia – zamiast okien.</summary>
    void DToolPanel()
    {
        var yel = new SolidColorBrush(Color.FromRgb(0xff, 0xe0, 0x40));
        if (tool == Tool.Track && dChain != null && dCands is { Count: > 1 })
        {
            props.Children.Add(T($"Rysowany tor – z {NDesc(dChain.From)} wychodzi {dCands.Count} torów UT; kliknij inny, żeby go wybrać:", 12, yel, true));
            var lb = new ListBox { Height = Math.Min(200, 24 * dCands.Count + 6), Background = Brushes.Black, Foreground = Brushes.White };
            foreach (var c in dCands) lb.Items.Add(new ListBoxItem { Content = ChainDesc(c), ToolTip = ChainDesc(c) });
            lb.SelectedIndex = dCands.FindIndex(c => c.Edges[0] == dChain.Edges[0]);
            lb.SelectionChanged += (a, e) => { if (lb.SelectedIndex >= 0 && dChain != null) { dChain = dCands[lb.SelectedIndex]; status.Text = "Tor na obrazie: " + ChainDesc(dChain); panel.InvalidateVisual(); } };
            props.Children.Add(lb);
        }
        else if (tool == Tool.Track)
            props.Children.Add(T("Narzędzie Tor: klik na węźle obrazu (np. zwrotnicy) – rysujesz tor UT, który z niego wychodzi (gdy jest ich kilka – wybór na liście, bez okien). Tor z listy „Nieumieszczone” niżej – zaznacz go przed kliknięciem.", 10.5, yel));
        if (DPlaceTool(tool))
        {
            var list = DUnplaced(tool);
            props.Children.Add(T($"{ToolName(tool)} – do umieszczenia ({list.Count}); zaznacz i kliknij miejsce na obrazie:", 12, yel, true));
            if (list.Count == 0) props.Children.Add(T("wszystkie są już na obrazie", 10.5, Brushes.Gray));
            else
            {
                var lb = new ListBox { Height = Math.Min(220, 24 * list.Count + 6), Background = Brushes.Black, Foreground = Brushes.White };
                foreach (var it in list) lb.Items.Add(new ListBoxItem { Content = it.Text, Tag = it.Id });
                var si = list.FindIndex(x => x.Id == dPlaceSel); lb.SelectedIndex = si >= 0 ? si : 0; dPlaceSel = list[lb.SelectedIndex].Id;
                lb.SelectionChanged += (a, e) => { if (lb.SelectedItem is ListBoxItem li) dPlaceSel = (string)li.Tag; };
                props.Children.Add(lb);
            }
        }
    }

    void DChainCancel(string msg) { dChain = null; dChainPts.Clear(); dCands = null; status.Text = msg; panel.InvalidateVisual(); }

    /// <summary>Zapis narysowanego ciągu: łamana dzielona na tory UT wg długości (albo w miejscach węzłów, które już stoją przy linii).</summary>
    void DChainFinish()
    {
        var c = dChain; if (c == null) return;
        var pts = dChainPts.ToList();
        if (doc.DPos(c.To) is { } pe && (pts[^1] - new Point(pe.X, pe.Y)).Length > 0.05) pts.Add(new Point(pe.X, pe.Y));
        double total = 0; for (int i = 0; i < pts.Count - 1; i++) total += (pts[i + 1] - pts[i]).Length;
        if (pts.Count < 2 || total < 1) { status.Text = "Tor na obrazie: kliknij co najmniej jeden punkt dalej (albo Esc – rezygnacja)"; return; }
        // węzły ciągu i miejsca podziału łamanej (odległość od początku)
        var nodes = new List<string> { c.From }; var cur = c.From;
        foreach (var e in c.Edges) { cur = OtherEnd(e, cur); nodes.Add(cur); }
        var lens = c.Edges.Select(e => Math.Max(1, st.Edges[e].Len)).ToList();
        var cum = new double[nodes.Count]; for (int i = 1; i < nodes.Count; i++) cum[i] = cum[i - 1] + lens[i - 1];
        var at = new double?[nodes.Count]; at[0] = 0; at[^1] = total;
        double lastFixed = 0;
        for (int i = 1; i < nodes.Count - 1; i++)
        {
            if (doc.DPos(nodes[i]) is not { } q) continue;
            var (s, d) = PolyProject(pts, new Point(q.X, q.Y));
            if (d <= Math.Max(Grid * 2, PickR * 2) && s > lastFixed + 0.5 && s < total - 0.5) { at[i] = s; lastFixed = s; }
        }
        for (int i = 1; i < nodes.Count - 1; i++)
        {
            if (at[i] != null) continue;
            int a = i - 1, b = i + 1; while (at[b] == null) b++;
            at[i] = at[a] + (at[b] - at[a]) * (cum[i] - cum[a]) / Math.Max(1e-9, cum[b] - cum[a]);
        }
        doc.Snapshot();
        for (int i = 0; i < c.Edges.Count; i++)
        {
            var piece = PolyCut(pts, at[i].Value, at[i + 1].Value);
            doc.SetDPos(nodes[i], piece[0].X, piece[0].Y); doc.SetDPos(nodes[i + 1], piece[^1].X, piece[^1].Y);
            var p = piece.Select(x => (x.X, x.Y)).ToList();
            if (st.Edges[c.Edges[i]].A != nodes[i]) p.Reverse();
            doc.SetDPts(c.Edges[i], p);
        }
        foreach (var n in nodes.Distinct()) DFixEnds(n);
        var desc = ChainDesc(c);
        dChain = null; dChainPts.Clear(); dCands = null; dHiddenSel = null;
        Changed($"Narysowano na obrazie: {desc} ({c.Edges.Count} tor{(c.Edges.Count == 1 ? "" : "y")} UT)");
        status.Text += "   ·  kolejny tor: klik na węźle obrazu";
    }

    /// <summary>Końce torów obrazu w węźle – w położeniu węzła na obrazie.</summary>
    void DFixEnds(string n)
    {
        if (doc.DPos(n) is not { } p) return;
        foreach (var e in doc.EdgesAt(n))
        {
            var id = StationDoc.Sv(e["id"]); var pts = doc.DPts(id); if (pts == null || pts.Count < 2) continue;
            if (StationDoc.Sv(e["a"]) == n) pts[0] = p;
            if (StationDoc.Sv(e["b"]) == n) pts[^1] = p;
            doc.SetDPts(id, pts);
        }
    }

    /// <summary>Rzut punktu na łamaną: odległość wzdłuż łamanej od początku i odległość od niej.</summary>
    static (double S, double D) PolyProject(List<Point> pts, Point q)
    {
        double s = 0, bestS = 0, bestD = double.MaxValue;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            var a = pts[i]; var v = pts[i + 1] - a; double l2 = v.LengthSquared, l = Math.Sqrt(l2);
            double t = l2 == 0 ? 0 : Math.Clamp(((q - a) * v) / l2, 0, 1);
            var d = (a + v * t - q).Length;
            if (d < bestD) { bestD = d; bestS = s + t * l; }
            s += l;
        }
        return (bestS, bestD);
    }
    /// <summary>Kawałek łamanej między odległościami s0 i s1 od początku (z punktami załamania po drodze).</summary>
    static List<Point> PolyCut(List<Point> pts, double s0, double s1)
    {
        Point At(double s)
        {
            double acc = 0;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                double l = (pts[i + 1] - pts[i]).Length;
                if (acc + l >= s || i == pts.Count - 2) return pts[i] + (pts[i + 1] - pts[i]) * (l > 0 ? Math.Clamp((s - acc) / l, 0, 1) : 0);
                acc += l;
            }
            return pts[^1];
        }
        var res = new List<Point> { At(s0) }; double a2 = 0;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            a2 += (pts[i + 1] - pts[i]).Length;
            if (i < pts.Count - 2 && a2 > s0 + 1e-6 && a2 < s1 - 1e-6) res.Add(pts[i + 1]);
        }
        res.Add(At(s1));
        return res;
    }

    bool DChainKey(Key k)
    {
        if (dChain == null) return false;
        switch (k)
        {
            case Key.Enter: DChainFinish(); return true;
            case Key.Back: if (dChainPts.Count > 1) dChainPts.RemoveAt(dChainPts.Count - 1); else DChainCancel("Tor na obrazie – rezygnacja"); return true;
            case Key.Escape: DChainCancel("Tor na obrazie – rezygnacja"); return true;
        }
        return false;
    }
    /// <summary>PPM w trakcie rysowania toru: koniec (gdy są co najmniej 2 punkty), inaczej rezygnacja.</summary>
    bool DChainRight()
    {
        if (dChain == null) return false;
        if (dChainPts.Count >= 2 || (dChainPts.Count == 1 && Placed(dChain.To))) DChainFinish(); else DChainCancel("Tor na obrazie – rezygnacja");
        return true;
    }

    // ------------------------------------------------------------------ umieszczanie urządzeń UT
    /// <summary>Elementy UT danego rodzaju, których węzłów nie ma na obrazie: (węzeł, rodzaj w panelu, id, opis).</summary>
    List<(string Node, string Kind, string Id, string Text)> DUnplaced(Tool t)
    {
        var res = new List<(string, string, string, string)>();
        string Track(string n) => st.Nodes[n].Edges.Select(x => st.Edges[x]).Select(x => x.Track != "" ? "tor " + x.Track : "odc. " + Station.Lbl(x.Sec)).FirstOrDefault() ?? "";
        string Hid(string n) => Placed(n) ? " – ukryty, klik przywraca" : "";
        if (t == Tool.Signal) foreach (var s in st.Signals.Values.Where(s => !Placed(s.Node) || doc.SymHidden(s.Name)).OrderBy(s => s.Label)) res.Add((s.Node, "sig", s.Name, $"{s.Label} – {s.Type}, {Track(s.Node)}{Hid(s.Node)} [{s.Name}]"));
        if (t == Tool.Switch) foreach (var s in st.Switches.Values.Where(s => !Placed(s.Node)).OrderBy(s => s.Label)) res.Add((s.Node, "sw", s.Num, $"zwrotnica {s.Label} [{s.Num}]"));
        if (t == Tool.TrackEnd) foreach (var (n, te) in st.TrackEnds.Where(x => !Placed(x.Key) || doc.SymHidden(x.Key)).OrderBy(x => x.Key)) res.Add((n, "te", n, $"kozioł {te.Disp} – {Track(n)}{Hid(n)} [{n}]"));
        if (t == Tool.Derailer) foreach (var d in st.Derailers.Values.Where(d => !Placed(d.Node) || doc.SymHidden(d.Name)).OrderBy(d => d.Label)) res.Add((d.Node, "der", d.Name, $"wykolejnica {d.Label} – {Track(d.Node)}{Hid(d.Node)} [{d.Name}]"));
        return res.Select(x => (x.Item1, x.Item2, x.Item3, x.Item4.Replace("|", "/"))).ToList();
    }
    static string ToolName(Tool t) => t switch { Tool.Signal => "Sygnalizator", Tool.Switch => "Zwrotnica", Tool.TrackEnd => "Kozioł", _ => "Wykolejnica" };

    void DPlaceDown(Point w)
    {
        if (DPickNode(w) is { } on && DS.Nodes[on] is var onn && (new Point(onn.X, onn.Y) - w).Length * panel.Scale < 3) { status.Text = $"Tu jest już {NDesc(on)} – przeciągnij go w „Wskaż” albo kliknij wolne miejsce"; return; }
        var list = DUnplaced(tool);
        if (list.Count == 0) { status.Text = $"{ToolName(tool)}: wszystkie z układu (UT) są już na obrazie"; return; }
        var it = list[PanelPick(list.Select(x => x.Text).ToList(), dPlaceSel)]; var p = Snap(w);
        if (Placed(it.Node) && doc.SymHidden(it.Id))
        {
            doc.Snapshot(); doc.SetSymHidden(it.Id, false); DClearSel(); dPlaceSel = null;
            Changed($"Przywrócono symbol {SymLbl(it.Id)} na pulpicie – w jego miejscu na torze (przesuniesz go w „Wskaż”)");
            return;
        }
        doc.Snapshot(); doc.SetDPos(it.Node, p.X, p.Y); DFixEnds(it.Node);
        DClearSel(); dPlaceSel = null;
        Changed($"Umieszczono na obrazie: {it.Text}. Następny – zaznaczony na liście w panelu; tory przy nim – narzędziem Tor (od tego węzła)");
    }

    bool dShiftDel;                      // Del jak z Shift (przycisk w panelu, test)

    /// <summary>Symbole w węźle: sygnalizatory, wykolejnice, kozioł (identyfikatory).</summary>
    List<string> SymsAt(string n)
    {
        var r = new List<string>();
        if (st.Nodes.TryGetValue(n, out var nd)) r.AddRange(nd.Signals);
        r.AddRange(st.Derailers.Values.Where(d => d.Node == n).Select(d => d.Name));
        if (st.TrackEnds.ContainsKey(n)) r.Add(n);
        return r;
    }
    string SymLbl(string id) => st.Signals.TryGetValue(id, out var s) ? s.Label : st.Derailers.TryGetValue(id, out var d) ? d.Label : st.TrackEnds.ContainsKey(id) ? "kozioł" : id;

    /// <summary>Del na obrazie (bez Shift): tory z obu końcami w zaznaczeniu – zdjęte z obrazu; węzeł bez torów na obrazie – zdjęty;
    /// węzeł na torze obrazu – tylko jego symbol (semafor, wykolejnica, kozioł) ukryty na pulpicie, tor zostaje. Zwrotnicy i zwykłego
    /// punktu na torze nie da się zdjąć bez torów – komunikat (Shift+Del – razem z torami).</summary>
    void DHideSymbols()
    {
        var edges = dSel.Count > 1 ? DSelEdges() : new List<string>();
        bool HasTracks(string n) => doc.EdgesAt(n).Any(e => doc.OnDisplay(StationDoc.Sv(e["id"])) && !edges.Contains(StationDoc.Sv(e["id"])));
        var nodes = dSel.Where(n => !HasTracks(n)).ToList();
        var syms = dSel.Where(HasTracks).SelectMany(n => SymsAt(n).Where(s => !doc.SymHidden(s))).ToList();
        var stuck = dSel.Where(n => HasTracks(n) && SymsAt(n).Count == 0).ToList();
        if (edges.Count + nodes.Count + syms.Count == 0)
        {
            status.Text = stuck.Count > 0 ? $"✗ {NDesc(stuck[0])} łączy tory na obrazie – nie da się go zdjąć bez torów. Zdejmij tory (klik na torze + Del) albo Shift+Del – razem z torami." : "Symbole zaznaczonych elementów są już ukryte";
            return;
        }
        doc.Snapshot();
        foreach (var e in edges) doc.DispEdges.Remove(e);
        foreach (var n in nodes) { doc.HideNode(n); foreach (var s in SymsAt(n)) doc.SetSymHidden(s, false); }
        foreach (var s in syms) doc.SetSymHidden(s, true);
        DClearSel();
        var parts = new List<string>();
        if (edges.Count > 0) parts.Add($"tory {edges.Count}");
        if (nodes.Count > 0) parts.Add($"punkty {nodes.Count}");
        if (syms.Count > 0) parts.Add($"ukryte symbole: {string.Join(", ", syms.Select(SymLbl))} (tor zostaje; przywrócenie – panel elementu albo narzędzie Semafor / Kozioł / Wykolejnica)");
        Changed("Usunięto z obrazu pulpitu: " + string.Join("; ", parts) + (stuck.Count > 0 ? $"; bez zmian {stuck.Count} (zwrotnica / punkt na torze – Shift+Del razem z torami)" : "") + ". Układ torowy bez zmian.");
    }

    // ------------------------------------------------------------------ zaznaczenie i właściwości
    /// <summary>Element UT w węźle obrazu (do panelu właściwości): zwrotnica, sygnalizator, kozioł, wykolejnica.</summary>
    (string Kind, string Id)? NodeElement(string n)
    {
        if (!st.Nodes.TryGetValue(n, out var nd)) return null;
        if (nd.Switch != null && doc.Switches.ContainsKey(nd.Switch)) return ("sw", nd.Switch);
        if (nd.Signals.FirstOrDefault(doc.Signals.ContainsKey) is string s) return ("sig", s);
        if (doc.TrackEnds.ContainsKey(n)) return ("te", n);
        if (st.Derailers.Values.FirstOrDefault(d => d.Node == n && doc.Derailers.ContainsKey(d.Name)) is { } dr) return ("der", dr.Name);
        return null;
    }
    string ElementNode(string kind, string id) => kind switch
    {
        "sig" => st.Signals.TryGetValue(id, out var s) ? s.Node : null, "sw" => st.Switches.TryGetValue(id, out var w) ? w.Node : null,
        "te" => id, "der" => st.Derailers.TryGetValue(id, out var d) ? d.Node : null, _ => null
    };

    /// <summary>Nagłówek panelu w zobrazowaniu dla zaznaczonego elementu UT: powiązanie z UT i polecenia obrazu.</summary>
    void DElementHeader()
    {
        var blue = new SolidColorBrush(Color.FromRgb(0x70, 0xc0, 0xff));
        if (selKind == "edge")
        {
            var c = st.Edges.ContainsKey(selId) ? ChainOf(selId) : null;
            props.Children.Add(T("Na obrazie pulpitu: tor " + selId, 13, blue, true));
            if (c != null) props.Children.Add(T($"Ciąg w układzie (UT): {ChainDesc(c)}; {c.Edges.Count(doc.OnDisplay)} z {c.Edges.Count} torów ciągu na obrazie.", 10.5, Brushes.LightGray));
            props.Children.Add(Btn("Przypisz ten rysunek do innego toru UT…", (a, e) => Safe(() => DRebindEdge(selId))));
            if (c != null) props.Children.Add(Btn("Narysuj ciąg od nowa (narzędzie Tor)", (a, e) => Safe(() => DRedraw(c))));
            props.Children.Add(Btn("Usuń tor z obrazu (Del)", (a, e) => Safe(DHideSel)));
        }
        else if (ElementNode(selKind, selId) is string n)
        {
            props.Children.Add(T($"Na obrazie pulpitu: węzeł {n}", 13, blue, true));
            var others = st.Nodes.TryGetValue(n, out var nd) ? nd.Signals.Where(s => s != selId).ToList() : new();
            if (others.Count > 0)
            {
                var row = new WrapPanel(); row.Children.Add(T("W tym węźle także: ", 10.5, Brushes.Gray));
                foreach (var s in others) row.Children.Add(Btn(st.Signals.TryGetValue(s, out var sd) ? sd.Label : s, (a, e) => { selKind = "sig"; selId = s; ShowProps(); }));
                props.Children.Add(row);
            }
            if (selKind is "sig" or "der" or "te" && doc.SymHidden(selId))
            {
                props.Children.Add(T("⚠ Symbol ukryty na pulpicie (węzeł zostaje na torze obrazu).", 11, Brushes.Orange));
                var sid = selId; props.Children.Add(Btn("Pokaż symbol na pulpicie", (a, e) => Safe(() => { doc.Snapshot(); doc.SetSymHidden(sid, false); Changed($"Przywrócono symbol {SymLbl(sid)} na pulpicie"); })));
            }
            props.Children.Add(Btn("Przypisz ten punkt do innego elementu UT…", (a, e) => Safe(() => DRebindNode(n))));
            props.Children.Add(Btn(doc.EdgesAt(n).Any(x => doc.OnDisplay(StationDoc.Sv(x["id"]))) ? "Ukryj symbol na pulpicie (Del) – tor zostaje" : "Usuń z obrazu pulpitu (Del)", (a, e) => Safe(DHideSel)));
            props.Children.Add(Btn("Usuń punkt razem z torami (Shift+Del)", (a, e) => Safe(() => { dShiftDel = true; DHideSel(); })));
        }
        props.Children.Add(T("Właściwości w układzie torowym (UT) – zmiana tutaj zmienia układ:", 10.5, Brushes.Gray));
    }

    /// <summary>Rysowanie ciągu od nowa: tory ciągu zdejmowane z obrazu (węzły końcowe zostają), potem narzędzie Tor od początku.</summary>
    void DRedraw(DChain c)
    {
        doc.Snapshot();
        foreach (var e in c.Edges) doc.DispEdges.Remove(e);
        var cur = c.From; foreach (var e in c.Edges.Take(c.Edges.Count - 1)) { cur = OtherEnd(e, cur); if (doc.EdgesAt(cur).All(x => !doc.OnDisplay(StationDoc.Sv(x["id"])))) doc.DispNodes.Remove(cur); }
        Changed("Ciąg zdjęty z obrazu – narysuj go od nowa");
        SetTool(Tool.Track); DClearSel();
        if (!Placed(c.From) && Placed(c.To)) c = Reverse(c);
        dChain = c; dChainPts.Clear();
        if (doc.DPos(c.From) is { } p) { dChainPts.Add(new Point(p.X, p.Y)); status.Text = $"Tor na obrazie: {ChainDesc(c)} – klikaj punkty od {NDesc(c.From)}; klik na {NDesc(c.To)} albo PPM / Enter – koniec"; }
        else { dChain = null; status.Text = "Ciąg zdjęty z obrazu – kliknij narzędziem Tor w miejscu początku"; }
        ShowProps(); panel.InvalidateVisual();
    }

    /// <summary>Przypisanie rysunku toru obrazu innemu torowi UT (którego nie ma na obrazie). Końce nowego toru – w końcach rysunku
    /// (albo w jego węzłach, jeśli już są na obrazie); stary tor schodzi z obrazu.</summary>
    void DRebindEdge(string eid)
    {
        var pts = doc.DPts(eid); if (pts == null) return;
        var cands = st.Edges.Values.Where(x => !x.Link && x.Id != eid && !doc.OnDisplay(x.Id)).OrderBy(x => x.Sec).ThenBy(x => x.Id).ToList();
        if (cands.Count == 0) { status.Text = "Wszystkie tory UT są już na obrazie – przypisanie do innego toru: najpierw zdejmij go z obrazu (Del)"; return; }
        string Desc(Edge x) => $"{(x.Track != "" ? "tor " + x.Track + " " : "")}odc. {Station.Lbl(x.Sec)}: {NDesc(x.A)} – {NDesc(x.B)}, {x.Len:0} m [{x.Id}]".Replace("|", "/");
        var i = Choose("Przypisz do toru UT", $"Rysunek toru {eid} ma przedstawiać tor z układu (UT):", cands.Select(Desc).ToList());
        if (i < 0) return;
        var ne = cands[i];
        // który koniec rysunku to A nowego toru: węzeł już na obrazie wyznacza stronę, inaczej – jak w starym torze
        var p0 = pts[0]; var p1 = pts[^1];
        bool rev = false;
        if (doc.DPos(ne.A) is { } pa) rev = Dist(pa, p1) < Dist(pa, p0);
        else if (doc.DPos(ne.B) is { } pb) rev = Dist(pb, p0) < Dist(pb, p1);
        if (rev) pts.Reverse();
        doc.Snapshot();
        doc.DispEdges.Remove(eid);
        if (!Placed(ne.A)) doc.SetDPos(ne.A, pts[0].X, pts[0].Y);
        if (!Placed(ne.B)) doc.SetDPos(ne.B, pts[^1].X, pts[^1].Y);
        pts[0] = doc.DPos(ne.A).Value; pts[^1] = doc.DPos(ne.B).Value;
        doc.SetDPts(ne.Id, pts);
        Changed($"Rysunek przypisany do toru UT {ne.Id} ({Desc(ne)}); tor {eid} zdjęty z obrazu");
        DClearSel(); dEdge = ne.Id; DSyncSingle(); ShowProps();
    }
    static double Dist((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>Przypisanie punktu obrazu (węzła) innemu elementowi UT, którego nie ma na obrazie: nowy węzeł staje w tym miejscu,
    /// stary schodzi z obrazu razem z torami, które do niego dochodzą (tory nowego elementu – narzędziem Tor).</summary>
    void DRebindNode(string n)
    {
        if (doc.DPos(n) is not { } p) return;
        var cands = new List<(string Node, string Kind, string Id, string Text)>();
        foreach (var t in new[] { Tool.Signal, Tool.Switch, Tool.TrackEnd, Tool.Derailer }) cands.AddRange(DUnplaced(t));
        if (cands.Count == 0) { status.Text = "Wszystkie sygnalizatory, zwrotnice, kozły i wykolejnice UT są już na obrazie"; return; }
        var i = Choose("Przypisz do elementu UT", $"Punkt {NDesc(n)} na obrazie ma przedstawiać element z układu (UT):", cands.Select(x => x.Text).ToList());
        if (i < 0) return;
        var it = cands[i];
        int lost = doc.EdgesAt(n).Count(x => doc.OnDisplay(StationDoc.Sv(x["id"])));
        doc.Snapshot(); doc.HideNode(n); doc.SetDPos(it.Node, p.X, p.Y); DFixEnds(it.Node);
        Changed($"Punkt przypisany do: {it.Text}; {NDesc(n)} zdjęty z obrazu{(lost > 0 ? $" razem z {lost} torami (narysuj tory nowego elementu narzędziem Tor)" : "")}");
        DClearSel(); dSel.Add(it.Node); DSyncSingle(); ShowProps();
    }

    // ------------------------------------------------------------------ rysowanie (podgląd)
    void DDrawTools(DrawingContext dc, double scale)
    {
        var az = new SolidColorBrush(Color.FromRgb(0x00, 0xa0, 0xff));
        var yel = new SolidColorBrush(Color.FromRgb(0xff, 0xe0, 0x40));
        // tory zaznaczonej grupy
        foreach (var gid in dSel.Count > 1 ? DSelEdges() : new())
            if (gid != dEdge && DS.Edges[gid] is var ge)
                for (int i = 0; i < ge.Pts.Count - 1; i++) dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(0x60, 0x00, 0xa0, 0xff)), 6 / scale), new Point(ge.Pts[i].X, ge.Pts[i].Y), new Point(ge.Pts[i + 1].X, ge.Pts[i + 1].Y));
        // zaznaczony tor obrazu
        if (dEdge != null && DS.Edges.TryGetValue(dEdge, out var se) && se.Pts.Count >= 2)
            for (int i = 0; i < se.Pts.Count - 1; i++) dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(0x90, 0x00, 0xa0, 0xff)), 8 / scale), new Point(se.Pts[i].X, se.Pts[i].Y), new Point(se.Pts[i + 1].X, se.Pts[i + 1].Y));
        // przebieg podświetlony w panelu sygnalizatora
        if (routeEdges != null && selKind == "sig")
            foreach (var eid in routeEdges) if (DS.Edges.TryGetValue(eid, out var ed) && ed.Pts.Count >= 2 && ed.Pts[0].X > Station.Hidden + 1)
                for (int i = 0; i < ed.Pts.Count - 1; i++) dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(0xa0, 0x00, 0xe6, 0x00)), 9 / scale), new Point(ed.Pts[i].X, ed.Pts[i].Y), new Point(ed.Pts[i + 1].X, ed.Pts[i + 1].Y));
        // rysowany ciąg: łamana do kursora i węzeł końcowy
        if (tool == Tool.Track && dChain != null && dChainPts.Count > 0)
        {
            var pen = new Pen(yel, 2.5 / scale);
            for (int i = 0; i < dChainPts.Count - 1; i++) dc.DrawLine(pen, dChainPts[i], dChainPts[i + 1]);
            var m = DPickNode(mouse) == dChain.To && doc.DPos(dChain.To) is { } te ? new Point(te.X, te.Y) : DChainPoint(mouse);
            dc.DrawLine(new Pen(yel, 1.5 / scale) { DashStyle = DashStyles.Dash }, dChainPts[^1], m);
            foreach (var q in dChainPts) dc.DrawEllipse(yel, null, q, 2.5 / scale, 2.5 / scale);
            if (doc.DPos(dChain.To) is { } pe) dc.DrawEllipse(null, new Pen(Brushes.LimeGreen, 2 / scale), new Point(pe.X, pe.Y), 9 / scale, 9 / scale);
            var ft = Ft($"{ChainDesc(dChain)}  → {NDesc(dChain.To)}", Math.Clamp(10 / scale, 0.05, 40), yel);
            dc.DrawText(ft, new Point(m.X + 10 / scale, m.Y - ft.Height - 6 / scale));
        }
        // węzeł pod kursorem w narzędziu Tor (początek ciągu)
        if (tool == Tool.Track && dChain == null && DPickNode(mouse) is { } hn && DS.Nodes.TryGetValue(hn, out var hnd))
            dc.DrawEllipse(null, new Pen(Brushes.LimeGreen, 2 / scale), new Point(hnd.X, hnd.Y), 8 / scale, 8 / scale);
        if (tool == Tool.Measure) DrawMeasures(dc, scale, Math.Clamp(9 / scale, 0.05, 30));
        // symbole ukryte na pulpicie – w edytorze szary przerywany znacznik (do zaznaczenia i przywrócenia)
        if (doc.Disp?["hiddenSyms"] is JsonArray hsa)
            foreach (var hid in hsa.Select(StationDoc.Sv).Where(x => x != null))
            {
                var hxn = st.Signals.TryGetValue(hid, out var hs) ? hs.Node : st.Derailers.TryGetValue(hid, out var hd) ? hd.Node : hid;
                if (!DS.Nodes.TryGetValue(hxn, out var hxd) || hxd.X <= Station.Hidden + 1 || !OnPage(hxd.X)) continue;
                var gp = new Pen(Brushes.Gray, 1.2 / scale) { DashStyle = DashStyles.Dash };
                dc.DrawEllipse(null, gp, new Point(hxd.X, hxd.Y), 7 / scale, 7 / scale);
                dc.DrawText(Ft(SymLbl(hid) + " (ukryty)", Math.Clamp(9 / scale, 0.05, 30), Brushes.Gray), new Point(hxd.X + 8 / scale, hxd.Y - 16 / scale));
            }
    }
}

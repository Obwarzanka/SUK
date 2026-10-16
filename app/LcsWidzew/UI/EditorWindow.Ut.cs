using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LcsWidzew.Model;

namespace LcsWidzew.UI;

/// <summary>Rodzaj okna edytora: układ torowy (plan schematyczny Ig-10) albo zobrazowanie (obrazy pulpitu) – osobne okna.</summary>
public enum EditorKind { Uklad, Zobrazowanie }

/// <summary>
/// Edytor układu torowego (UT) na planie schematycznym Ig-10 (skala podłużna ≠ poprzeczna): tory stacyjne poziomo w pasach co
/// międzytorze, kilometraż rośnie w prawo. Rozjazd i łącznik to obiekty wg typu rozjazdu (skos 1:n, PR / KR), sygnalizator
/// przypina się do toru i obraca jednym poleceniem. Narzędzia „swobodne” (łuki, tor równoległy, rozjazd pod dowolnym kątem)
/// zostają dla planów rysowanych po zdjęciu w skali rzeczywistej.
/// </summary>
public partial class EditorWindow
{
    public EditorKind Kind { get; private set; } = EditorKind.Uklad;

    /// <summary>Przekazanie dokumentu do okna drugiego rodzaju (bez pytania o zapis przy zamknięciu tego okna).</summary>
    public (StationDoc Doc, string Path, bool Dirty) HandOver() { handedOver = true; doc.DecoOnDisplay = false; return (doc, path, dirty); }
    public void MarkDirty() => dirty = true;
    bool handedOver;

    /// <summary>Narzędzia planu schematycznego Ig-10 (tryb UT, skala poprzeczna inna niż podłużna).</summary>
    bool Schem => !dispMode && panel.PlanView && doc != null && !doc.RealScale;

    readonly ComboBox turnKindBox = new() { Width = 170, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 6, 0) };
    string TurnKind => turnKindBox.SelectedItem as string ?? SwitchDef.Types[0].Kind;
    /// <summary>Odstęp pasów torów (międzytorze) w jednostkach planu.</summary>
    double BandH => Gap * doc.Uy;

    // narzędzie Rozjazd: miejsce zwrotnicy (tor, x) i punkt wciśnięcia (przeciąganie w stronę odgałęzienia)
    (string Edge, double X, double Y, Point Press)? utTurn;
    bool utTurnDragged;
    // narzędzie Tor: długość wpisywana z klawiatury
    string lenInput = "";
    // przeciąganie rozjazdu wzdłuż toru i toru w pionie
    HashSet<string> utGroup; double utApplied; bool utVertical; Point utStart; bool utMoved;

    // ------------------------------------------------------------------ przyciąganie
    double SnapXm(double x) => SnapOn ? Math.Round(x / doc.Ux) * doc.Ux : Math.Round(x, 2);
    double SnapBand(double y) => Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) ? Math.Round(y, 2) : Math.Round(y / BandH) * BandH;
    Point SchemSnap(Point w) => new(SnapXm(w.X), SnapBand(w.Y));

    /// <summary>Kierunek toru A→B w prawo? (kilometraż rośnie w prawo – A→B jest normalnie w prawo)</summary>
    bool AbRight(string eid) { var p = doc.EdgePts(eid); return p[^1].X >= p[0].X; }

    // ------------------------------------------------------------------ zdarzenia
    /// <summary>Wciśnięcie myszy w narzędziach UT; false – obsługa jak dotąd.</summary>
    bool UtDown(Point w, MouseButtonEventArgs e)
    {
        if (!Schem) return false;
        bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift), ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        switch (tool)
        {
            case Tool.Track: UtTrackClick(w, ctrl); return true;
            case Tool.Turnout: UtTurnoutDown(w); return true;
            case Tool.Signal when !(AutoAnswers.Count > 0 && AutoAnswers.Peek().Length >= 3 && AutoAnswers.Peek()[2] is string ad && (ad.StartsWith("→") || ad.StartsWith("←"))):
                UtSignalClick(w, ctrl); return true;
            case Tool.Select when !shift && !ctrl && e?.ClickCount != 2:
            {
                if (UtPickSig(w) is { } sg) { ClearMulti(); Select("sig", sg); utGroup = new() { StationDoc.Sv(doc.Signals[sg]["node"]) }; UtStartDrag(w, false); return true; }
                if (UtPickSw(w) is { } sw && doc.Switches[sw] is JsonObject so) { ClearMulti(); Select("sw", sw); utGroup = doc.TurnoutNodes(StationDoc.Sv(so["node"])); UtStartDrag(w, false); return true; }
                if (PickNode(w) != null) return false;
                if (PickEdge(w) is { } ed && doc.Edge(ed) is { } eo && StationDoc.IsHorizontal(eo) && !StationDoc.IsLink(eo))
                {
                    ClearMulti(); Select("edge", ed); utGroup = doc.BandLine(ed); UtStartDrag(w, true);
                    status.Text = "Tor " + (StationDoc.Sv(eo["track"]) is { Length: > 0 } tn ? tn : ed) + " – przeciągnij w pionie, żeby przenieść cały tor na inny pas (skosy rozjazdów wracają do skosu wg typu)";
                    return true;
                }
                return false;
            }
        }
        return false;
    }

    void UtStartDrag(Point w, bool vertical) { utStart = w; utApplied = 0; utVertical = vertical; utMoved = false; }

    bool UtMove(Point w, MouseEventArgs e)
    {
        if (!Schem) return false;
        if (utGroup != null && (e == null || e.LeftButton == MouseButtonState.Pressed))
        {
            double d = utVertical ? SnapBand(w.Y - utStart.Y) : (SnapOn ? Math.Round((w.X - utStart.X) / doc.Ux) * doc.Ux : Math.Round(w.X - utStart.X, 2));
            if (!utMoved && Math.Abs((utVertical ? w.Y - utStart.Y : w.X - utStart.X) * panel.Scale) < 4) return true;
            if (!utMoved) { doc.Snapshot(); utMoved = true; }
            if (Math.Abs(d - utApplied) > 1e-9)
            {
                if (utVertical) doc.MoveGroup(utGroup, 0, d - utApplied); else doc.MoveGroup(utGroup, d - utApplied, 0);
                utApplied = d; dirty = true; Rebuild();
            }
            return true;
        }
        if (tool == Tool.Turnout && utTurn != null) { if ((w - utTurn.Value.Press).Length * panel.Scale > 8) utTurnDragged = true; panel.InvalidateVisual(); return true; }
        return false;
    }

    bool UtUp(Point w)
    {
        if (!Schem) return false;
        if (utGroup != null)
        {
            var g = utGroup; utGroup = null;
            if (!utMoved) return true;
            if (utVertical)
            {
                int k = doc.RefitSkews(g, Gap);
                Changed($"Tor przeniesiony o {utApplied / BandH:+0.#;-0.#} pasa ({Math.Abs(utApplied) / doc.Uy:0.##} m w poprzek){(k > 0 ? $"; skosy wg typu – poprawiono {k}" : "")}");
            }
            else Changed($"Przesunięto o {utApplied / doc.Ux:+0.#;-0.#} m wzdłuż toru");
            return true;
        }
        if (tool == Tool.Turnout && utTurn != null && utTurnDragged) { UtTurnoutCommit(w); return true; }
        return false;
    }

    bool UtRight()
    {
        if (!Schem) return false;
        // porzucony początek toru (bez żadnego toru) – nie zostaje jako pusty węzeł
        if (chainFrom != null && doc.Nodes.ContainsKey(chainFrom) && doc.EdgesAt(chainFrom).Count == 0 && !doc.Signals.Any(x => StationDoc.Sv(x.Value["node"]) == chainFrom)) { doc.Nodes.Remove(chainFrom); Rebuild(); }
        if (utTurn != null) { utTurn = null; status.Text = "Rozjazd – rezygnacja"; panel.InvalidateVisual(); return true; }
        if (lenInput != "") { lenInput = ""; panel.InvalidateVisual(); return true; }
        return false;
    }

    /// <summary>Klawisze UT: F – obróć sygnalizator, Shift+F – strona toru, cyfry + Enter – długość toru w narzędziu Tor.</summary>
    bool UtKey(Key k)
    {
        if (!Schem) return false;
        if (k == Key.F && selKind == "sig" && doc.Signals[selId] is JsonObject so)
        {
            doc.Snapshot();
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) { so["side"] = StationDoc.Sv(so["side"]) == "L" ? "P" : "L"; if (StationDoc.Sv(so["side"]) == "P") so.Remove("side"); Changed("Sygnalizator po drugiej stronie toru"); }
            else { so["dir"] = -(int)StationDoc.D(so["dir"]); Changed("Obrócono sygnalizator – obowiązuje " + SigTravelText(selId)); }
            return true;
        }
        if (tool == Tool.Track && chainFrom != null)
        {
            string ch = k switch { >= Key.D0 and <= Key.D9 => ((int)(k - Key.D0)).ToString(), >= Key.NumPad0 and <= Key.NumPad9 => ((int)(k - Key.NumPad0)).ToString(), Key.OemComma or Key.OemPeriod or Key.Decimal => ".", _ => null };
            if (ch != null) { lenInput += ch; status.Text = $"Długość toru: {lenInput} m – Enter: tor tej długości w stronę kursora"; panel.InvalidateVisual(); return true; }
            if (k == Key.Back && lenInput != "") { lenInput = lenInput[..^1]; panel.InvalidateVisual(); return true; }
            if (k == Key.Enter && lenInput != "") { var s = lenInput; lenInput = ""; Safe(() => UtTrackLen(s)); return true; }
        }
        return false;
    }

    // ------------------------------------------------------------------ Tor
    void UtTrackClick(Point w, bool free)
    {
        var (p, k, id) = SnapTarget(w);
        if (chainFrom == null)
        {
            string start;
            if (k == "node") start = id;
            else if (k == "edge") { status.Text = "To środek toru – rozjazd wstawisz narzędziem Rozjazd (R). Tor zaczynaj w wolnym miejscu albo na końcu toru."; return; }
            else
            {
                var q = SchemSnap(w);
                if (doc.NodeNear(q.X, q.Y, 0.5 * doc.Ux) is { } nn) start = nn;
                else if (doc.HorizEdgeAt(q.X, q.Y) != null) { status.Text = "Tu leży tor – zacznij na jego końcu albo wstaw rozjazd (R)"; return; }
                else { doc.Snapshot(); start = doc.AddNode(Post, q.X, q.Y); Rebuild(); }
            }
            chainFrom = start; chainSec = null; lenInput = "";
            status.Text = "TOR: wskaż koniec toru (poziomo w pasie; Ctrl – dowolnie) albo wpisz długość w metrach i Enter. PPM / Esc – koniec";
            return;
        }
        var a = doc.Pos(chainFrom);
        Point end; string endNode = null;
        if (k == "node" && id != chainFrom) { endNode = id; end = p; if (!free && Math.Abs(end.Y - a.Y) > 0.05) { status.Text = "✗ Ten węzeł leży na innym pasie – tory na różnych pasach łączy rozjazd (R); Ctrl – tor skośny"; return; } }
        else if (free) end = k == "edge" ? p : SchemSnap(w);
        else end = new Point(SnapXm(w.X), a.Y);
        UtTrackTo(end, endNode, free, k == "edge" && free ? id : null);
    }

    void UtTrackLen(string s)
    {
        if (chainFrom == null || !double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var m) || m < 1) { status.Text = "✗ Podaj długość ≥ 1 m"; return; }
        var a = doc.Pos(chainFrom); int sx = mouse.X >= a.X ? 1 : -1;
        var end = new Point(a.X + sx * m * doc.Ux, a.Y);
        UtTrackTo(end, doc.NodeNear(end.X, end.Y, 0.05 * doc.Ux), false, null);
    }

    void UtTrackTo(Point end, string endNode, bool free, string splitEdge)
    {
        var a = doc.Pos(chainFrom);
        if (doc.PlanLen(a.X, a.Y, end.X, end.Y) < 1) { status.Text = "✗ Za krótki tor (< 1 m)"; return; }
        if (!free)
        {
            double x0 = Math.Min(a.X, end.X), x1 = Math.Max(a.X, end.X);
            foreach (var e in doc.EdgeObjs.Where(e => !StationDoc.IsLink(e) && StationDoc.IsHorizontal(e)))
            {
                var pts = doc.EdgePts(StationDoc.Sv(e["id"])); if (Math.Abs(pts[0].Y - a.Y) > 0.05) continue;
                double e0 = Math.Min(pts[0].X, pts[^1].X), e1 = Math.Max(pts[0].X, pts[^1].X);
                if (Math.Min(x1, e1) - Math.Max(x0, e0) > 0.01) { status.Text = "✗ Nowy tor nachodzi na istniejący tor w tym pasie – skończ na jego końcu (zielony znacznik) albo rysuj w innym pasie"; return; }
            }
            endNode ??= doc.NodeNear(end.X, end.Y, 0.05 * doc.Ux);
        }
        doc.Snapshot();
        try
        {
            if (endNode == null) endNode = splitEdge != null ? doc.SplitEdge(splitEdge, end.X, end.Y, Post, true) : doc.AddNode(Post, Math.Round(end.X, 2), Math.Round(end.Y, 2));
            if (endNode == chainFrom) { doc.Undo(); return; }
            int n0 = doc.EdgesAt(endNode).Count;
            var eid = AddChainEdge(endNode);
            if (eid == null) { doc.Undo(); Rebuild(); return; }
            Changed($"Tor {doc.PlanLen(a.X, a.Y, end.X, end.Y):0.#} m (odcinek {Station.Lbl(chainSec)}){(n0 >= 2 ? " – w węźle 3 tory: wstaw zwrotnicę (Z)" : "")} – wskaż dalej albo PPM", true);
            if (n0 >= 1) { chainFrom = null; status.Text += "  ·  połączono z istniejącym torem"; }
            Select("edge", eid);
        }
        catch (InvalidOperationException ex) { doc.Undo(); Rebuild(); status.Text = "✗ " + ex.Message; }
    }

    // ------------------------------------------------------------------ Rozjazd
    void UtTurnoutDown(Point w)
    {
        if (utTurn != null) { UtTurnoutCommit(w); return; }
        var ed = PickEdge(w);
        if (ed == null || doc.Edge(ed) is not { } eo || !StationDoc.IsHorizontal(eo) || StationDoc.IsLink(eo)) { status.Text = "ROZJAZD: wciśnij na torze (poziomym) w miejscu zwrotnicy i przeciągnij w stronę odgałęzienia (albo kliknij tor, potem kliknij stronę)"; return; }
        var pts = doc.EdgePts(ed); double x = Math.Clamp(SnapXm(w.X), Math.Min(pts[0].X, pts[^1].X), Math.Max(pts[0].X, pts[^1].X));
        utTurn = (ed, x, pts[0].Y, w); utTurnDragged = false;
        status.Text = $"ROZJAZD {TurnKind}: przeciągnij / kliknij w stronę odgałęzienia – w lewo / w prawo i w górę / w dół; na torze obok – łącznik z drugą zwrotnicą, w wolnym miejscu – odgałęzienie z torem do kursora. PPM – rezygnacja";
    }

    (int Sx, int Sy, int Bands, double EndX)? UtTurnParams(Point w)
    {
        if (utTurn is not { } t) return null;
        double dx = w.X - t.X, dy = w.Y - t.Y;
        if (Math.Abs(dy) < BandH * 0.3) return null;
        int sx = dx >= 0 ? 1 : -1, sy = dy > 0 ? 1 : -1;
        int bands = Math.Max(1, (int)Math.Round(Math.Abs(dy) / BandH));
        return (sx, sy, bands, SnapXm(w.X));
    }

    /// <summary>Opis końca skosu do pytania o numer (strona względem zwrotnicy).</summary>
    static string EndSide(double dx, double dy) => (dy < 0 ? "w górę" : "w dół") + "-" + (dx < 0 ? "w lewo" : "w prawo");

    void UtTurnoutCommit(Point w)
    {
        var t = utTurn.Value; var pr = UtTurnParams(w); utTurn = null; utTurnDragged = false;
        if (pr is not { } q) { status.Text = "✗ Wskaż stronę odgałęzienia: kursor nad albo pod torem (co najmniej ⅓ międzytorza)"; panel.InvalidateVisual(); return; }
        // numery wpisuje projektant: ten rozjazd i zwrotnice na końcach skosów (gdzie skos dochodzi do toru)
        var kind = TurnKind; var cls = SwitchDef.Class(kind);
        var ends = doc.TurnoutEnds(t.X, t.Y, q.Sx, q.Sy, q.Bands, Gap, kind);
        var fields = new List<(string, string)>
        {
            (cls switch { "rkp" => "Numer rozjazdu krzyżowego podwójnego (zwrotnice …ab i …cd)", "rkj" => "Numer rozjazdu krzyżowego pojedynczego (zwrotnice …a i …b)", "skrz" => "Numer skrzyżowania torów", _ => "Numer rozjazdu" }, "")
        };
        foreach (var en in ends) if (doc.EndKind(en.X, en.Y) == "switch") fields.Add(($"Numer zwrotnicy na końcu skosu ({EndSide(en.X - t.X, en.Y - t.Y)}) – {(cls == "rz" ? "łącznik rozjazdowy" : "rozjazd zwyczajny")}", ""));
        if (cls == "rz") fields.Add(("Położenie zasadnicze zwrotnicy", "na wprost (+ na tor prosty)|na zwrot (+ na odgałęzienie)"));
        // test losowy (bez okien): kolejne wolne numery
        int nx = int.TryParse(NextSwitchNum(), out var nv) ? nv : 1;
        var r = FuzzMode && AutoAnswers.Count == 0 ? fields.Select((f, i) => f.Item2.Contains('|') ? f.Item2.Split('|')[0] : (nx + i).ToString()).ToArray() : Ask($"Nowy rozjazd – {kind}", fields.ToArray());
        if (r == null) { status.Text = "Rozjazd – rezygnacja"; panel.InvalidateVisual(); return; }
        var nums = (cls == "rz" ? r.Take(r.Length - 1) : r).Select(x => x.Trim()).ToArray();
        bool basicMinus = cls == "rz" && r[^1].StartsWith("na zwrot");
        doc.Snapshot();
        try
        {
            var res = doc.AddTurnout(t.Edge, t.X, q.Sx, q.Sy, q.Bands, Gap, kind, Post, nums, basicMinus, chkSwSec.IsChecked == true, q.EndX);
            Changed(res.Msg + (cls == "rz" ? $" – rozjazd {doc.TurnoutHand(res.Node)}{(basicMinus ? ", zasadniczo na zwrot" : "")}" : ""));
            var key = doc.Switches.FirstOrDefault(x => StationDoc.Sv(x.Value["node"]) == res.Node).Key; if (key != null) Select("sw", key);
        }
        catch (InvalidOperationException ex) { doc.Undo(); Rebuild(); status.Text = "✗ " + ex.Message; }
    }

    /// <summary>Skrzyżowanie torów (bez zwrotnic), do którego należy tor: numer albo null.</summary>
    string SkrzOf(string eid)
    {
        var sec = StationDoc.Sv(doc.Edge(eid)?["sec"]); if (sec == null) return null;
        var lbl = sec.Split(':').Last(); if (lbl.Length < 2 || lbl[0] != 'z') return null;
        if (doc.Switches.Any(x => StationDoc.Sv(x.Value["sec"]) == sec)) return null;
        var es = doc.EdgeObjs.Where(e => StationDoc.Sv(e["sec"]) == sec).ToList();
        return es.Any(StationDoc.IsHorizontal) && es.Any(e => !StationDoc.IsHorizontal(e)) ? lbl[1..] : null;
    }

    // ------------------------------------------------------------------ Semafor / tarcza
    string UtPickSig(Point w)
    {
        string best = null; double bd = R * 1.3;
        foreach (var s in st.Signals.Values)
        {
            if (!st.Nodes.TryGetValue(s.Node, out var n)) continue;
            var d = (new Point(n.X, n.Y) - w).Length; if (d < bd && doc.Signals.ContainsKey(s.Name)) { bd = d; best = s.Name; }
        }
        return best;
    }
    string UtPickSw(Point w)
    {
        string best = null; double bd = R;
        foreach (var (k, v) in doc.Switches) { var n = StationDoc.Sv(v["node"]); if (!doc.Nodes.ContainsKey(n)) continue; var (x, y) = doc.Pos(n); var d = Math.Sqrt((x - w.X) * (x - w.X) + (y - w.Y) * (y - w.Y)); if (d < bd) { bd = d; best = k; } }
        return best;
    }

    /// <summary>Opis kierunku obowiązywania sygnalizatora na planie: w prawo / w lewo (kilometraż rosnący / malejący).</summary>
    string SigTravelText(string key)
    {
        if (!st.Signals.TryGetValue(key, out var s)) return "";
        var v = Ig10.TravelDir(st, s);
        return v.X > 0.1 ? "dla jazdy w prawo (w stronę rosnącego kilometra)" : v.X < -0.1 ? "dla jazdy w lewo (w stronę malejącego kilometra)" : v.Y > 0 ? "dla jazdy w dół planu" : "dla jazdy w górę planu";
    }

    /// <summary>
    /// Semafor na planie: klik na torze – sygnalizator przypięty do toru w tym miejscu, obowiązuje dla jazdy w prawo (Ctrl – w lewo),
    /// po prawej stronie toru. Klik na istniejącym sygnalizatorze – obrót (drugi kierunek jazdy); F / Shift+F w „Wskaż”.
    /// </summary>
    void UtSignalClick(Point w, bool left)
    {
        if (UtPickSig(w) is { } sg && doc.Signals[sg] is JsonObject so)
        {
            doc.Snapshot(); so["dir"] = -(int)StationDoc.D(so["dir"]); Changed("Obrócono " + Station.Lbl(sg) + " – obowiązuje " + SigTravelText(sg)); Select("sig", sg); return;
        }
        var (p, k, id) = SnapTarget(w);
        if (k == "grid") { status.Text = "SEMAFOR: kliknij tor w miejscu sygnalizatora (klik na sygnalizatorze – obrót)"; return; }
        if (k == "node" && doc.EdgesAt(id).Count > 2) { status.Text = "✗ Sygnalizator nie może stać w punkcie zwrotnicy – kliknij tor obok"; return; }
        if (k == "node" && doc.Signals.Any(x => StationDoc.Sv(x.Value["node"]) == id)) { status.Text = "✗ W tym miejscu stoi już sygnalizator"; return; }
        var r = Ask("Nowy sygnalizator", ("Nazwa (np. S1, Tm12, A; dla PK – np. PK1)", ""), ("Typ", "wyjazdowy|wyjazdowy bez funkcji manewrowej|wjazdowy|tm|to|sp|pk – punkt końcowy przebiegu"),
            ("Powtarzany semafor / sygnalizator SBL / szlak (dla to / sp)", ""), ("Przebiegi kończące się w PK (dla pk)", "pociągowe i manewrowe|pociągowe|manewrowe"));
        if (r == null || r[0].Trim() == "") return;
        doc.Snapshot();
        try
        {
            string nid, eid;
            if (k == "node") { nid = id; eid = StationDoc.Sv(doc.EdgesAt(id)[0]["id"]); }
            else
            {
                var pts = doc.EdgePts(id); bool hz = pts.All(q => Math.Abs(q.Y - pts[0].Y) < 0.05);
                double x = hz ? SnapXm(p.X) : p.X, y = hz ? pts[0].Y : p.Y;
                nid = doc.NodeNear(x, y, 0.3 * doc.Ux) is { } nn && doc.EdgesAt(nn).Count == 2 ? nn : doc.SplitEdge(id, x, y, Post, true); eid = id;
            }
            // obowiązuje dla jazdy w prawo (A→B toru jest w prawo – kilometraż), Ctrl – w lewo
            int dir = (AbRight(StationDoc.Sv(doc.EdgesAt(nid)[0]["id"])) ? 1 : -1) * (left ? -1 : 1);
            bool noMs = r[1] == "wyjazdowy bez funkcji manewrowej"; bool pk = r[1].StartsWith("pk");
            var key = doc.AddSignal(nid, r[0].Trim(), noMs ? "wyjazdowy" : pk ? "pk" : r[1], dir, Post, null, pk ? null : r[2].Trim());
            if (noMs) doc.Signals[key]["shunt"] = false;
            if (pk) doc.Signals[key]["routes"] = r.Length > 3 ? (r[3].StartsWith("pociągowe i") ? "PM" : r[3].StartsWith("poc") ? "P" : "M") : "PM";
            Changed($"Dodano {r[1]} {r[0]}"); Select("sig", key);
            status.Text = $"{r[1]} {r[0]} – obowiązuje {SigTravelText(key)}. Obróć: F albo klik na nim tym narzędziem; strona toru: Shift+F.";
        }
        catch (InvalidOperationException ex) { doc.Undo(); Rebuild(); status.Text = "✗ " + ex.Message; }
    }

    // ------------------------------------------------------------------ rysowanie
    void DrawUt(DrawingContext dc, double scale)
    {
        if (!Schem) return;
        var az = new SolidColorBrush(Color.FromRgb(0x00, 0x8c, 0xe0));
        double fs = Math.Clamp(9 / scale, 0.05, 30);
        // pasy torów (międzytorze) – delikatne linie pomocnicze
        if (chkGrid.IsChecked == true)
        {
            var v = panel.ViewWorld; double h = BandH;
            if (h * scale > 6)
            {
                var bp = new Pen(new SolidColorBrush(Color.FromArgb(0x50, 0x70, 0x90, 0xc0)), 1 / scale) { DashStyle = new DashStyle(new double[] { 2, 6 }, 0) };
                for (double y = Math.Floor(v.Top / h) * h; y <= v.Bottom; y += h) dc.DrawLine(bp, new Point(v.Left, y), new Point(v.Right, y));
            }
        }
        // podgląd toru
        if (tool == Tool.Track && chainFrom != null && doc.Nodes.ContainsKey(chainFrom))
        {
            var a = doc.Pos(chainFrom); Point m;
            if (lenInput != "" && double.TryParse(lenInput, NumberStyles.Float, CultureInfo.InvariantCulture, out var lm)) m = new Point(a.X + (mouse.X >= a.X ? 1 : -1) * lm * doc.Ux, a.Y);
            else { var (p, k, _) = SnapTarget(mouse); m = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ? (k == "grid" ? SchemSnap(mouse) : p) : k == "node" ? p : new Point(SnapXm(mouse.X), a.Y); }
            dc.DrawLine(new Pen(az, 2 / scale) { DashStyle = DashStyles.Dash }, new Point(a.X, a.Y), m);
            var ft = Ft($"{doc.PlanLen(a.X, a.Y, m.X, m.Y):0.#} m{(lenInput != "" ? "  (wpisano)" : "")}", fs, az);
            dc.DrawText(ft, new Point((a.X + m.X) / 2 - ft.Width / 2, m.Y - ft.Height - 3 / scale));
        }
        // podgląd rozjazdu
        if (tool == Tool.Turnout && utTurn is { } t)
        {
            var pen = new Pen(az, 2.5 / scale) { DashStyle = DashStyles.Dash };
            dc.DrawEllipse(null, new Pen(az, 1.5 / scale), new Point(t.X, t.Y), 5 / scale, 5 / scale);
            if (UtTurnParams(mouse) is { } q)
            {
                var kind = TurnKind; var cls = SwitchDef.Class(kind);
                var (n, _, ta, tb) = StationDoc.TurnoutGeom(kind);
                var ends = doc.TurnoutEnds(t.X, t.Y, q.Sx, q.Sy, q.Bands, Gap, kind);
                var c0 = new Point(t.X, t.Y); var tk = new Pen(az, 1.5 / scale); double hh = 4 / scale;
                string what;
                if (cls == "rz")
                {
                    var b = new Point(ends[0].X, ends[0].Y); dc.DrawLine(pen, c0, b);
                    var ek = doc.EndKind(b.X, b.Y);
                    if (ek == "free" && Math.Sign(q.EndX - b.X) == q.Sx && Math.Abs(q.EndX - b.X) >= doc.Ux) dc.DrawLine(pen, b, new Point(q.EndX, b.Y));
                    foreach (var xx in new[] { t.X - q.Sx * ta * doc.Ux, t.X + q.Sx * tb * doc.Ux }) dc.DrawLine(tk, new Point(xx, t.Y - hh), new Point(xx, t.Y + hh));
                    what = (q.Sx * q.Sy > 0 ? "prawy" : "lewy") + " · " + (ek == "switch" ? "łącznik na tor obok" : ek == "join" ? "do końca toru" : "odgałęzienie");
                }
                else
                {
                    double h = StationDoc.CrossHalf(kind) * doc.Ux;
                    var e1 = new Point(ends[0].X, ends[0].Y); var e2 = new Point(ends[1].X, ends[1].Y);
                    if (cls == "rkp") { var n1 = new Point(t.X + q.Sx * h, t.Y); var n2 = new Point(t.X - q.Sx * h, t.Y); dc.DrawLine(pen, n1, e1); dc.DrawLine(pen, n2, e2); dc.DrawLine(new Pen(az, 4 / scale), n1, n2); }
                    else { dc.DrawLine(pen, e2, e1); if (cls == "rkj") dc.DrawLine(pen, new Point(t.X - q.Sx * h, t.Y), c0 + (e1 - c0) * (h / Math.Abs(e1.X - c0.X))); }
                    foreach (var xx in new[] { t.X - h, t.X + h }) dc.DrawLine(tk, new Point(xx, t.Y - hh), new Point(xx, t.Y + hh));
                    what = (cls == "rkp" ? "krzyżowy podwójny" : cls == "rkj" ? "krzyżowy pojedynczy" : "skrzyżowanie") + $" · końce: {string.Join(", ", ends.Select(en => doc.EndKind(en.X, en.Y) switch { "switch" => "zwrotnica", "join" => "koniec toru", "busy" => "zajęte!", _ => "wolny" }))}";
                }
                var ft = Ft($"{kind} · {what} · skos {q.Bands * Gap * n:0.#} m", fs, az);
                dc.DrawText(ft, new Point(mouse.X + 10 / scale, mouse.Y - ft.Height - 8 / scale));
            }
        }
    }

    /// <summary>Paski narzędzi w oknie UT: narzędzia schematu zamiast swobodnych (łuki, tor równoległy, rozjazdy jako skosy).</summary>
    void ApplyUtToolbars()
    {
        if (modeBtn != null) modeBtn.Visibility = Visibility.Collapsed;
        bool sch = Schem;
        foreach (var b in new UIElement[] { arcModeBtn, toolBtns.GetValueOrDefault(Tool.Arc), toolBtns.GetValueOrDefault(Tool.Parallel), planBtn })
            if (b != null) b.Visibility = sch || dispMode ? Visibility.Collapsed : Visibility.Visible;
        turnKindBox.Visibility = dispMode ? Visibility.Collapsed : Visibility.Visible;
        if (autoBtn != null)
        {
            autoBtn.Visibility = dispMode || doc == null || !doc.RealScale ? Visibility.Collapsed : Visibility.Visible;
            autoBtn.Content = "Przekształć na schemat Ig-10…";
        }
        if (toolBtns.TryGetValue(Tool.Track, out var tb) && sch) tb.ToolTip = "Tor: klik w wolnym miejscu albo na końcu toru, klik – koniec. Tor leży poziomo w pasie (co międzytorze); długość można wpisać z klawiatury i Enter. Ctrl – tor skośny. PPM / Esc – koniec.  [T]";
        if (toolBtns.TryGetValue(Tool.Turnout, out var rb) && sch) rb.ToolTip = "Rozjazd wg typu (lista obok): wciśnij na torze w miejscu zwrotnicy i przeciągnij w stronę odgałęzienia (albo kliknij tor, potem stronę). Na tor obok – łącznik z drugą zwrotnicą, w wolne miejsce – odgałęzienie z nowym torem. Skos 1:n wg typu, odcinek zwrotnicowy od PR do KR.  [R]";
        if (toolBtns.TryGetValue(Tool.Signal, out var sb) && sch) sb.ToolTip = "Semafor / tarcza: klik na torze – przypięty do toru, obowiązuje dla jazdy w prawo (Ctrl – w lewo). Klik na sygnalizatorze – obrót. W „Wskaż”: F – obróć, Shift+F – druga strona toru, przeciąganie – wzdłuż toru.  [S]";
        Title = Kind == EditorKind.Zobrazowanie ? "Edytor zobrazowania – obrazy pulpitu (LCS)" : "Edytor układu torowego – plan schematyczny (LCS)";
    }

    /// <summary>Przekształcenie planu rysowanego w skali rzeczywistej na schemat Ig-10 (1:2000 / 1:500) – propozycja autoukładu.</summary>
    void ConvertToSchem()
    {
        if (!NoPrompt && !FuzzMode && MessageBox.Show(this, $"Przekształcić plan na schemat Ig-10 (1:2000 wzdłuż, 1:500 w poprzek)?{(char)10}{(char)10}Tory zostaną ułożone poziomo w pasach (propozycja autoukładu – do poprawek). Długości torów i połączenia bez zmian, zobrazowanie bez zmian. Ctrl+Z cofa.", "Schemat Ig-10", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        doc.Snapshot(); doc.EnsureDisplay();
        doc.RescalePlan(2000, 500);
        var rep = doc.AutoLayoutPlan();
        Changed("Plan przekształcony na schemat Ig-10: " + rep);
        panel.FitAll(); ApplyUtToolbars();
    }
}

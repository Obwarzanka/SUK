using System.Globalization;
using System.Text.Json.Nodes;

namespace LcsWidzew.Model;

/// <summary>
/// Układ torowy jako plan schematyczny wg Ig-10 (skala podłużna / poprzeczna, kilometraż rośnie od lewej do prawej):
/// tory stacyjne leżą poziomo w pasach co międzytorze, rozjazdy i łączniki to skosy wg typu rozjazdu (skos 1:n w metrach,
/// w poprzek w skali poprzecznej). Rozjazd jest obiektem z typem: punkt środkowy (zwrotnica w węźle), początek (PR)
/// i koniec (KR) – granice odcinka zwrotnicowego.
/// </summary>
public partial class StationDoc
{
    /// <summary>
    /// Geometria rozjazdu z typu: skos 1:n, promień R [m], a – od początku (PR) do punktu środkowego, b – od punktu
    /// środkowego do końca (KR) [m]. a, b – przybliżone (styczna łuku R·tg(α/2); Rz 1:9 R190: b = 16,615 m) – do sprawdzenia
    /// z katalogiem rozjazdów.
    /// </summary>
    public static (double N, double R, double A, double B) TurnoutGeom(string kind)
    {
        var t = SwitchDef.Types.FirstOrDefault(x => x.Kind == kind);
        double n = ParseRatio(t.Ratio) is double rn && rn > 1 ? rn : 9, r = t.R > 0 ? t.R : 190;
        double a = r * Math.Tan(Math.Atan(1 / n) / 2), b = Math.Abs(r - 190) < 1 && Math.Abs(n - 9) < 0.01 ? 16.615 : a;
        return (n, r, Math.Round(a, 2), Math.Round(b, 2));
    }

    public static double? ParseRatio(string ratio)
    {
        if (string.IsNullOrWhiteSpace(ratio)) return null;
        var s = ratio.Trim(); if (s.StartsWith("1:")) s = s[2..];
        return double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    /// <summary>Tor poziomy (wszystkie punkty na jednej wysokości).</summary>
    public static bool IsHorizontal(JsonObject e) { var p = Pts(e); return p.All(q => Math.Abs(q.Y - p[0].Y) < 0.05); }

    /// <summary>Tor poziomy na wysokości y obejmujący x (z końcami); null – brak.</summary>
    public JsonObject HorizEdgeAt(double x, double y, double tol = 0.3)
    {
        foreach (var e in EdgeObjs)
        {
            if (IsLink(e) || !IsHorizontal(e)) continue;
            var p = Pts(e); if (Math.Abs(p[0].Y - y) > tol) continue;
            double x0 = Math.Min(p[0].X, p[^1].X), x1 = Math.Max(p[0].X, p[^1].X);
            if (x >= x0 - 0.01 && x <= x1 + 0.01) return e;
        }
        return null;
    }

    /// <summary>Węzeł na wysokości y w odległości ≤ tol (j. planu) w poziomie; null – brak.</summary>
    public string NodeNear(double x, double y, double tol, double tolY = 0.3) =>
        Nodes.Where(n => Math.Abs(D(n.Value["y"]) - y) <= tolY && Math.Abs(D(n.Value["x"]) - x) <= tol)
             .OrderBy(n => Math.Abs(D(n.Value["x"]) - x)).Select(n => n.Key).FirstOrDefault();

    /// <summary>Węzeł na torze poziomym w punkcie x: istniejący zwykły węzeł w pobliżu (≤ tol) albo podział toru.</summary>
    string PointOnTrack(JsonObject e, double x, double tol, string post, bool allowBusy = false)
    {
        var p = Pts(e); double y = p[0].Y;
        foreach (var n in new[] { Sv(e["a"]), Sv(e["b"]) })
            if (Math.Abs(Pos(n).X - x) <= tol) return allowBusy || IsPlainNode(n) || EdgesAt(n).Count == 1 ? n : throw new InvalidOperationException($"W tym miejscu jest już zwrotnica, sygnalizator albo inny obiekt (węzeł {n}) – przesuń rozjazd");
        return SplitEdge(Sv(e["id"]), x, y, post, true);
    }

    /// <summary>Podział toru poziomego w x (gdy x leży wewnątrz toru, dalej niż 0,5 m od węzłów) – granica odcinka zwrotnicowego.</summary>
    void SplitAtX(string nid, int s, double dist, string post)
    {
        var (x, y) = Pos(nid); double tx = x + s * dist * Ux;
        foreach (var e in EdgesAt(nid))
        {
            if (!IsHorizontal(e)) continue;
            var o = Sv(e["a"]) == nid ? Sv(e["b"]) : Sv(e["a"]); var ox = Pos(o).X;
            if (Math.Sign(ox - x) != s) continue;
            if (Math.Abs(ox - x) > dist * Ux + 0.5 * Ux) SplitEdge(Sv(e["id"]), tx, y, post, true);
            return;
        }
    }

    /// <summary>Wynik wstawienia rozjazdu: węzeł zwrotnicy, węzeł zwrotnicy po drugiej stronie łącznika (albo null), koniec odgałęzienia.</summary>
    public record TurnoutResult(string Node, string Node2, string End, string Msg);
    public record struct Point2(double X, double Y);

    /// <summary>Co leży w punkcie końca skosu: "switch" – tor (będzie zwrotnica), "join" – koniec toru, "busy" – węzeł zajęty, "free" – wolne miejsce.</summary>
    public string EndKind(double x, double y)
    {
        var n = NodeNear(x, y, 1.5 * Ux);
        if (n != null) return EdgesAt(n).Count == 1 ? "join" : IsPlainNode(n) ? "switch" : "busy";
        return HorizEdgeAt(x, y) != null ? "switch" : "free";
    }

    /// <summary>
    /// Końce skosów rozjazdu (do podglądu i pytania o numery): rozjazd zwyczajny – jeden koniec; krzyżowy / skrzyżowanie –
    /// dwa (skos przechodzi przez tor w obie strony).
    /// </summary>
    public List<Point2> TurnoutEnds(double x, double y0, int sx, int sy, int bands, double gapM, string kind)
    {
        var (n, _, _, _) = TurnoutGeom(kind); double B = sy * bands * gapM * Uy, D = sx * bands * gapM * n * Ux;
        var cls = SwitchDef.Class(kind);
        if (cls == "rz") return new() { new(x + D, y0 + B) };
        double h = cls == "rkp" ? CrossHalf(kind) * Ux * sx : 0;
        return new() { new(x + h + D, y0 + B), new(x - h - D, y0 - B) };
    }

    /// <summary>Odległość połówek rozjazdu krzyżowego (ab / cd) i granic skrzyżowania od środka [m].</summary>
    public static double CrossHalf(string kind) => SwitchDef.Class(kind) == "rkp" ? 3 : Math.Max(4, TurnoutGeom(kind).B / 2);   // Rkp: połówki blisko środka – na planie znak jak skrzyżowanie

    /// <summary>Typ rozjazdu zwyczajnego o tym samym skosie (zwrotnice na końcach skosu rozjazdu krzyżowego / skrzyżowania).</summary>
    static string PlainKindFor(string kind)
    {
        var r = SwitchDef.Types.FirstOrDefault(t => t.Kind == kind).Ratio;
        return SwitchDef.Types.FirstOrDefault(t => t.Kind.StartsWith("Rz ") && t.Ratio == r).Kind ?? SwitchDef.Types[0].Kind;
    }

    void CheckNum(string post, string label)
    {
        if (string.IsNullOrWhiteSpace(label)) throw new InvalidOperationException("Podaj numer rozjazdu");
        if (Switches.ContainsKey($"{post}:{label.Trim()}")) throw new InvalidOperationException($"Zwrotnica {label.Trim()} już istnieje");
    }

    /// <summary>Węzeł końca skosu: istniejący węzeł (koniec toru / zwykły punkt), punkt na torze albo nowy węzeł; sw – będzie zwrotnica.</summary>
    string EndNode(double x1, double y1, string post, out bool sw, out string kindAt)
    {
        kindAt = EndKind(x1, y1);
        if (kindAt == "busy") throw new InvalidOperationException("Koniec skosu wypada w węźle ze zwrotnicą / sygnalizatorem – przesuń rozjazd");
        var nn = NodeNear(x1, y1, 1.5 * Ux);
        if (nn != null) { sw = EdgesAt(nn).Count == 2; return nn; }
        if (HorizEdgeAt(x1, y1) is { } te) { sw = true; return PointOnTrack(te, x1, 0.6 * Ux, post); }
        sw = false; return AddNode(post, Math.Round(x1, 2), Math.Round(y1, 2));
    }

    /// <summary>Skos od węzła a do b; do zwrotnicy na drugim końcu – dwa tory z przerwą izolacyjną w środku.</summary>
    void Skew(string a, string b, bool split, string post)
    {
        if (!split) { AddEdge(a, b, null, post); return; }
        var (ax, ay) = Pos(a); var (bx, by) = Pos(b);
        var j = AddNode(post, Math.Round((ax + bx) / 2, 2), Math.Round((ay + by) / 2, 2));
        AddEdge(a, j, null, post); AddEdge(j, b, null, post);
    }

    void CheckBetween(double x, double y0, double dx, double dy, int bands)
    {
        for (int j = 1; j < bands; j++)
            if (HorizEdgeAt(x + dx * j / bands, y0 + dy * j / bands) is { } mid)
                throw new InvalidOperationException($"Skos przecina tor {(Sv(mid["track"]) is { Length: > 0 } tr ? tr : Sv(mid["id"]))} – zrób łącznik na sąsiedni tor albo wstaw na nim rozjazd krzyżowy (Rkp / Rkj) lub skrzyżowanie");
    }

    /// <summary>Zwrotnica na końcu skosu (rozjazd zwyczajny tego samego skosu), z odcinkiem PR–KR; tipSide – strona ostrza (±1 w poziomie).</summary>
    string EndSwitch(string node, string label, string kind, int tipSide, bool swSec, string post)
    {
        CheckNum(post, label);
        var (_, _, a, b) = TurnoutGeom(kind);
        var before = EdgesAt(node).Where(IsHorizontal).Select(q => Sv(q["sec"])).Distinct().ToList();
        if (swSec) { SplitAtX(node, tipSide, a, post); SplitAtX(node, -tipSide, b, post); }
        Orient();
        var k = AddSwitch(node, label.Trim(), post, swSec); SetKind(k, kind);
        if (swSec) foreach (var s0 in before) SplitDisconnectedSec(s0, post);     // tor przecięty odcinkiem zwrotnicowym – osobne odcinki po obu stronach
        return k;
    }

    /// <summary>
    /// Rozjazd na planie schematycznym: zwrotnica w punkcie x toru poziomego eid, odgałęzienie w stronę sx (±1 w poziomie)
    /// i sy (±1 w pionie, w dół dodatnio) skosem wg typu na tor odległy o „bands” międzytorzy. Na torze docelowym – druga
    /// zwrotnica (łącznik rozjazdowy) albo połączenie z końcem toru; bez toru – nowy tor do endX (gdy dalej niż koniec skosu).
    /// Odcinek zwrotnicowy: od PR do KR. Numery podaje użytkownik: nums[0] – ten rozjazd, kolejne – zwrotnice na końcach skosów
    /// (w kolejności TurnoutEnds, tylko tam, gdzie koniec wypada na torze). basicMinus – położenie zasadnicze na zwrot.
    /// Rozjazdy krzyżowe (Rkp – połówki ab / cd, Rkj – zwrotnice a / b) i skrzyżowanie torów – skos przez tor w obie strony.
    /// </summary>
    public TurnoutResult AddTurnout(string eid, double x, int sx, int sy, int bands, double gapM, string kind, string post, string[] nums, bool basicMinus, bool swSec, double? endX = null)
    {
        var e = Edge(eid) ?? throw new InvalidOperationException("Brak toru");
        if (!IsHorizontal(e)) throw new InvalidOperationException("Rozjazd wstawia się na torze poziomym (plan schematyczny)");
        if (bands < 1) bands = 1;
        var cls = SwitchDef.Class(kind);
        if (cls != "rz") return AddCrossing(e, x, sx, sy, bands, gapM, kind, cls, post, nums, swSec);
        var (n, _, a, b) = TurnoutGeom(kind);
        double y0 = Pts(e)[0].Y, dy = sy * bands * gapM * Uy, dx = sx * bands * gapM * n * Ux;
        double x1 = x + dx, y1 = y0 + dy;
        CheckBetween(x, y0, dx, dy, bands);
        CheckNum(post, nums.ElementAtOrDefault(0));
        var A = PointOnTrack(e, x, 0.6 * Ux, post);
        if (EdgesAt(A).Count != 2) throw new InvalidOperationException("Zwrotnica wymaga miejsca w środku toru (nie na jego końcu)");
        var B = EndNode(x1, y1, post, out var sw2, out var at);
        if (sw2) CheckNum(post, nums.ElementAtOrDefault(1));
        if (sw2 && nums[0].Trim() == nums[1].Trim()) throw new InvalidOperationException("Obie zwrotnice łącznika mają ten sam numer");
        var (bx, by) = Pos(B);
        Skew(A, B, sw2, post);
        string end = B;
        if (at == "free" && endX is double ex && Math.Sign(ex - bx) == sx && Math.Abs(ex - bx) >= 1 * Ux)
        {
            end = AddNode(post, Math.Round(ex, 2), Math.Round(by, 2));
            AddEdge(B, end, null, post);
        }
        if (swSec) { SplitAtX(A, -sx, a, post); SplitAtX(A, sx, b, post); }
        if (swSec && sw2) { SplitAtX(B, sx, a, post); SplitAtX(B, -sx, b, post); }
        Orient();       // zwrotnica: ostrze i odgałęzienia wg geometrii – nowe tory zorientowane spójnie przed ustaleniem (+) / (−)
        var mainSecs = new[] { A, sw2 ? B : null }.Where(q => q != null).SelectMany(EdgesAt).Where(IsHorizontal).Select(q => Sv(q["sec"])).Distinct().ToList();
        var n1 = nums[0].Trim(); var k1 = AddSwitch(A, n1, post, swSec); SetKind(k1, kind);
        if (basicMinus) Switches[k1]["zas"] = "-";
        string msg = $"Rozjazd {n1} ({kind})";
        if (sw2)
        {
            var n2 = nums[1].Trim(); var k2 = AddSwitch(B, n2, post, swSec); SetKind(k2, kind);
            if (swSec) CrossoverSecs(k1);
            msg = $"Łącznik rozjazdowy {n1}/{n2} ({kind}, {bands * gapM * n:0.#} m wzdłuż)";
        }
        // tor przecięty odcinkiem zwrotnicowym: części po obu stronach – osobne odcinki
        if (swSec) foreach (var s0 in mainSecs) SplitDisconnectedSec(s0, post);
        if (!sw2 && at == "join") msg += " – odgałęzienie połączone z końcem toru";
        else if (!sw2) msg += end != B ? " – odgałęzienie z nowym torem" : " – odgałęzienie (koniec rozjazdu, dorysuj tor dalej narzędziem Tor)";
        return new(A, sw2 ? B : null, end, msg);
    }

    /// <summary>
    /// Rozjazd krzyżowy / skrzyżowanie na torze poziomym w x: skos (wg typu) przechodzi przez tor, końce – na torach w pasach
    /// obok (zwrotnice) albo wolne. Rkp: dwie połówki na torze (N ab – bliżej początku linii, z lewej; N cd), skos dochodzi do
    /// połówek – 4 drogi jazdy. Rkj: skos przecina tor bez węzła, jedno połączenie toru ze skosem – zwrotnice N a (z lewej) i N b.
    /// Skrzyżowanie: skos przecina tor, bez zwrotnic. Wspólny odcinek z{N} – jazdy po torze i po skosie wzajemnie się wykluczają.
    /// </summary>
    TurnoutResult AddCrossing(JsonObject e, double x, int sx, int sy, int bands, double gapM, string kind, string cls, string post, string[] nums, bool swSec)
    {
        var (n, _, _, b) = TurnoutGeom(kind);
        double y0 = Pts(e)[0].Y, B = sy * bands * gapM * Uy, D = sx * bands * gapM * n * Ux, hm = CrossHalf(kind), h = hm * Ux;
        var N = nums.ElementAtOrDefault(0)?.Trim();
        if (string.IsNullOrEmpty(N)) throw new InvalidOperationException("Podaj numer rozjazdu krzyżowego / skrzyżowania");
        var labels = cls == "rkp" ? new[] { N + "ab", N + "cd" } : cls == "rkj" ? new[] { N + "a", N + "b" } : Array.Empty<string>();
        foreach (var l in labels) CheckNum(post, l);
        if (cls == "skrz" && EdgeObjs.Any(q => Sv(q["sec"]) == $"{post}:z{N}")) throw new InvalidOperationException($"Numer {N} jest już zajęty (odcinek z{N})");
        double xs = cls == "rkp" ? sx * h : 0;
        CheckBetween(x + xs, y0, D, B, bands); CheckBetween(x - xs, y0, -D, -B, bands);
        var ends = TurnoutEnds(x, y0, sx, sy, bands, gapM, kind);
        var sec = $"{post}:z{N}";
        // tor poziomy: węzły po obu stronach środka (połówki Rkp / granice skrzyżowania)
        var Lm = PointOnTrack(e, x - h, 0.3 * Ux, post);
        var e2 = HorizEdgeAt(x + h - 0.01, y0) ?? throw new InvalidOperationException("Rozjazd krzyżowy nie mieści się na torze");
        var Rm = PointOnTrack(e2, x + h, 0.3 * Ux, post);
        if (EdgesAt(Lm).Count != 2 || EdgesAt(Rm).Count != 2) throw new InvalidOperationException("Rozjazd krzyżowy wymaga toru po obu stronach (nie na końcu toru)");
        var mainSecs = new[] { Lm, Rm }.SelectMany(EdgesAt).Select(q => Sv(q["sec"])).Distinct().ToList();
        string near1 = sx > 0 ? Rm : Lm, near2 = sx > 0 ? Lm : Rm;    // połówka po stronie końca skosu 1 / 2
        var endNodes = new List<(string Node, bool Sw)>();
        var diag = new List<JsonObject>();
        string diagSw = null;
        if (cls == "rkp")
        {
            for (int i = 0; i < 2; i++)
            {
                var en = EndNode(ends[i].X, ends[i].Y, post, out var sw, out _); endNodes.Add((en, sw));
                int before = Edges.Count; Skew(i == 0 ? near1 : near2, en, sw, post); diag.AddRange(EdgeObjs.Skip(before));
            }
        }
        else
        {
            // skos przez tor bez węzła: punkty skosu w odległości h od środka (granice odcinka skrzyżowania)
            double t = h / Math.Abs(D);
            var P1 = AddNode(post, Math.Round(x + D * t, 2), Math.Round(y0 + B * t, 2)); var P2 = AddNode(post, Math.Round(x - D * t, 2), Math.Round(y0 - B * t, 2));
            int before = Edges.Count;
            var en1 = EndNode(ends[0].X, ends[0].Y, post, out var s1, out _); var en2 = EndNode(ends[1].X, ends[1].Y, post, out var s2, out _);
            endNodes.Add((en1, s1)); endNodes.Add((en2, s2));
            Skew(P1, en1, s1, post); Skew(P2, en2, s2, post); AddEdge(P2, P1, sec, post);
            if (cls == "rkj") { AddEdge(near2, P1, sec, post); diagSw = P1; }
            diag.AddRange(EdgeObjs.Skip(before));
        }
        // odcinek rozjazdu: tor między połówkami / granicami i krótkie odcinki na zewnątrz (do końca rozjazdu)
        if (swSec) { SplitAtX(Lm, -1, Math.Max(1, b - hm), post); SplitAtX(Rm, 1, Math.Max(1, b - hm), post); }
        Orient();
        foreach (var q in EdgesAt(Lm).Concat(EdgesAt(Rm)).Where(IsHorizontal)) q["sec"] = sec;
        foreach (var q in diag.Where(q => Sv(q["a"]) == Lm || Sv(q["b"]) == Lm || Sv(q["a"]) == Rm || Sv(q["b"]) == Rm || (diagSw != null && (Sv(q["a"]) == diagSw || Sv(q["b"]) == diagSw)))) q["sec"] = sec;
        var keys = new List<string>();
        if (cls == "rkp") { keys.Add(AddSwitch(Lm, labels[0], post, false)); keys.Add(AddSwitch(Rm, labels[1], post, false)); }
        else if (cls == "rkj")
        {
            var mainSw = near2; var (mx, _) = Pos(mainSw); var (dx2, _) = Pos(diagSw);
            keys.Add(AddSwitch(mx < dx2 ? mainSw : diagSw, labels[0], post, false)); keys.Add(AddSwitch(mx < dx2 ? diagSw : mainSw, labels[1], post, false));
        }
        foreach (var k in keys) { Switches[k]["sec"] = sec; Switches[k]["grp"] = N; SetKind(k, kind); }
        var core = EdgeObjs.Where(q => Sv(q["sec"]) == sec).ToList();
        // zwrotnice na końcach skosów (tory w pasach obok)
        var msg = new List<string>(); int ni = 1; var plain = PlainKindFor(kind);
        for (int i = 0; i < endNodes.Count; i++)
        {
            if (!endNodes[i].Sw) continue;
            var lab = nums.ElementAtOrDefault(ni++);
            var k = EndSwitch(endNodes[i].Node, lab, plain, i == 0 ? sx : -sx, swSec, post);
            if (swSec) CrossoverSecs(k);
            msg.Add(Station.Lbl(k));
        }
        foreach (var q in core) q["sec"] = sec;      // odcinek rozjazdu krzyżowego / skrzyżowania – bez zmian po podziale skosów
        if (swSec) foreach (var s0 in mainSecs.Where(q => q != sec)) SplitDisconnectedSec(s0, post);
        var name = cls == "rkp" ? $"Rozjazd krzyżowy podwójny {N} ({labels[0]} / {labels[1]})" : cls == "rkj" ? $"Rozjazd krzyżowy pojedynczy {N} ({labels[0]} / {labels[1]})" : $"Skrzyżowanie torów {N}";
        return new(keys.Count > 0 ? Sv(Switches[keys[0]]["node"]) : Lm, keys.Count > 1 ? Sv(Switches[keys[1]]["node"]) : null, null,
            name + (msg.Count > 0 ? $"; na końcach skosu zwrotnice {string.Join(", ", msg)}" : ""));
    }

    /// <summary>Odcinek złożony z niepołączonych części: części poza pierwszą (najdłuższą) dostają nowe identyfikatory.</summary>
    public int SplitDisconnectedSec(string sec, string post)
    {
        var es = EdgeObjs.Where(e => Sv(e["sec"]) == sec).ToList(); if (es.Count < 2) return 0;
        var comps = new List<List<JsonObject>>(); var left = new HashSet<JsonObject>(es);
        while (left.Count > 0)
        {
            var c = new List<JsonObject>(); var q = new Queue<JsonObject>(); var f = left.First(); left.Remove(f); q.Enqueue(f);
            while (q.Count > 0)
            {
                var e = q.Dequeue(); c.Add(e);
                foreach (var n in new[] { Sv(e["a"]), Sv(e["b"]) })
                    foreach (var o in EdgesAt(n).Where(left.Contains).ToList()) { left.Remove(o); q.Enqueue(o); }
            }
            comps.Add(c);
        }
        if (comps.Count < 2) return 0;
        foreach (var c in comps.OrderByDescending(c => c.Sum(e => D(e["len"]))).Skip(1)) { var ns = NewSecId(post); foreach (var e in c) e["sec"] = ns; }
        return comps.Count - 1;
    }

    void SetKind(string swKey, string kind)
    {
        if (Switches[swKey] is not JsonObject o || string.IsNullOrEmpty(kind)) return;
        var t = SwitchDef.Types.FirstOrDefault(x => x.Kind == kind); if (t.Kind == null) return;
        o["kind"] = kind; if (t.R > 0) o["radius"] = t.R; if (t.Ratio != "") o["ratio"] = t.Ratio;
    }

    /// <summary>Tory odgałęzienia zwrotnicy (−) aż do drugiej zwrotnicy albo do pierwszego toru poziomego / końca (bez niego).</summary>
    public (List<JsonObject> Chain, List<string> Mids, string End) TurnoutBranch(string nid)
    {
        var first = MinusLeg(nid); var chain = new List<JsonObject>(); var mids = new List<string>(); string cur = nid, end = null;
        for (var e = first; e != null && chain.Count < 20;)
        {
            if (IsHorizontal(e)) { end = cur; break; }
            chain.Add(e);
            var o = Sv(e["a"]) == cur ? Sv(e["b"]) : Sv(e["a"]);
            if (!IsPlainNode(o)) { end = o; break; }
            var next = EdgesAt(o).First(x => x != e);
            if (IsHorizontal(next) || IsLink(next)) { end = o; break; }
            mids.Add(o); e = next; cur = o;
        }
        return (chain, mids, end ?? cur);
    }

    /// <summary>Kierunek rozjazdu (Ig-10, wykaz rozjazdów): lewy / prawy patrząc od początku (iglic) w stronę odgałęzienia.</summary>
    public string TurnoutHand(string nid)
    {
        var ed = EdgesAt(nid); var minus = MinusLeg(nid); if (minus == null) return "";
        var (x, y) = Pos(nid);
        var others = ed.Where(e => e != minus).ToList();
        var tipSide = ed.Select(e => (e, d: EdgeDir(e, nid))).GroupBy(q => q.d).FirstOrDefault(g => g.Count() == 1)?.First().e;
        if (tipSide == null) return "";
        var tp = Pts(tipSide); var tq = Sv(tipSide["a"]) == nid ? tp[1] : tp[^2];
        double fx = x - tq.X, fy = y - tq.Y;                       // kierunek jazdy od iglic
        var mp = Pts(minus); var mq = Sv(minus["a"]) == nid ? mp[1] : mp[^2];
        double vx = mq.X - x, vy = mq.Y - y;
        double cross = fx * vy - fy * vx;                           // oś Y w dół: > 0 – w prawo
        return cross > 0 ? "prawy" : cross < 0 ? "lewy" : "";
    }

    /// <summary>
    /// Usunięcie rozjazdu: zwrotnica i jej odgałęzienie (skos) aż do drugiej zwrotnicy (łącznik – obie zwrotnice) albo końca
    /// skosu; tory zasadnicze zostają ciągłe (węzły scalane, gdy to zwykłe punkty).
    /// </summary>
    public string DeleteTurnout(string swKey)
    {
        if (Switches[swKey] is not JsonObject sw) throw new InvalidOperationException("Brak zwrotnicy");
        if (Sv(sw["grp"]) is string grp) return DeleteCrossing(Sv(sw["post"]) ?? StationDoc.SecPost(swKey), grp);
        var nid = Sv(sw["node"]);
        var (chain, mids, end) = TurnoutBranch(nid);
        if (chain.Count == 0) { Switches.Remove(swKey); Nodes[nid]["kind"] = ""; return "Usunięto zwrotnicę (bez odgałęzienia skośnego)"; }
        var other = Switches.FirstOrDefault(x => Sv(x.Value["node"]) == end && x.Key != swKey).Key;
        foreach (var e in chain) Edges.Remove(e);
        foreach (var m in mids) { Nodes.Remove(m); if (Disp != null) DispNodes.Remove(m); }
        var sec = Sv(sw["sec"]);
        Switches.Remove(swKey); Nodes[nid]["kind"] = ""; Nodes[nid]["ref"] = "";
        string sec2 = null;
        if (other != null) { sec2 = Sv(Switches[other]["sec"]); Switches.Remove(other); Nodes[end]["kind"] = ""; Nodes[end]["ref"] = ""; }
        // odcinek zwrotnicowy na torze zasadniczym – z powrotem do odcinka toru (sąsiedniego); zbędne węzły PR / KR scalane
        var region = new HashSet<string>();
        foreach (var (n, s0) in new[] { (nid, sec), (other != null ? end : null, sec2) })
        {
            if (n == null || s0 == null || !Nodes.ContainsKey(n)) continue;
            var mine = EdgeObjs.Where(x => Sv(x["sec"]) == s0 && IsHorizontal(x)).ToList();
            var rn = mine.SelectMany(x => new[] { Sv(x["a"]), Sv(x["b"]) }).Append(n).ToHashSet();
            var nb = rn.SelectMany(EdgesAt).Where(x => IsHorizontal(x) && Sv(x["sec"]) != s0 && !IsSzlak(x)).Select(x => Sv(x["sec"])).FirstOrDefault()
                     ?? NewSecId(Sv(Nodes[n]["post"]) ?? "", "it");
            foreach (var x in mine) x["sec"] = nb;
            region.UnionWith(rn);
        }
        if (other == null && Nodes.ContainsKey(end) && EdgesAt(end).Count == 0) Nodes.Remove(end);
        Simplify(region.Where(Nodes.ContainsKey).ToList());
        return other != null ? $"Usunięto łącznik rozjazdowy {Sv(sw["label"])} (z drugą zwrotnicą)" : $"Usunięto rozjazd {Sv(sw["label"])}";
    }

    /// <summary>
    /// Części rozjazdu krzyżowego / skrzyżowania N: węzły odcinka z{N}, tory skośne (skos przez tor aż do zwrotnic na końcach albo
    /// wolnych końców) i zwrotnice na końcach skosów.
    /// </summary>
    public (HashSet<string> Nodes, HashSet<JsonObject> Diag, List<string> EndSw) CrossingParts(string post, string N)
    {
        var sec = $"{post}:z{N}";
        var nodes = EdgeObjs.Where(e => Sv(e["sec"]) == sec).SelectMany(e => new[] { Sv(e["a"]), Sv(e["b"]) }).ToHashSet();
        var diag = new HashSet<JsonObject>(); var endSw = new List<string>();
        var q = new Queue<string>(nodes); var seen = new HashSet<string>(nodes);
        while (q.Count > 0)
        {
            var n = q.Dequeue();
            foreach (var e in EdgesAt(n).Where(e => !IsHorizontal(e) && !IsLink(e)))
            {
                if (!diag.Add(e)) continue;
                var o = Sv(e["a"]) == n ? Sv(e["b"]) : Sv(e["a"]);
                var sk = Switches.FirstOrDefault(x => Sv(x.Value["node"]) == o).Key;
                if (sk != null && Sv(Switches[sk]["grp"]) != N) { if (!endSw.Contains(sk)) endSw.Add(sk); seen.Add(o); continue; }
                if (seen.Add(o)) q.Enqueue(o);
            }
        }
        nodes.UnionWith(seen);
        return (nodes, diag, endSw);
    }

    /// <summary>Usunięcie rozjazdu krzyżowego / skrzyżowania N: skos z połączeniem i zwrotnicami na końcach; tory poziome zostają ciągłe.</summary>
    public string DeleteCrossing(string post, string N)
    {
        var (nodes, diag, endSw) = CrossingParts(post, N);
        var secs = new List<(string Node, string Sec)>();
        foreach (var k in Switches.Where(x => Sv(x.Value["grp"]) == N && Sv(x.Value["post"]) == post).Select(x => x.Key).Concat(endSw).ToList())
        {
            var nd = Sv(Switches[k]["node"]); secs.Add((nd, Sv(Switches[k]["sec"])));
            Switches.Remove(k); if (Nodes[nd] is JsonObject no) { no["kind"] = ""; no["ref"] = ""; }
        }
        var sec = $"{post}:z{N}";
        var anyMain = EdgeObjs.Where(e => Sv(e["sec"]) == sec && IsHorizontal(e)).Select(e => Sv(e["a"])).FirstOrDefault();
        if (anyMain != null) secs.Add((anyMain, sec));
        foreach (var e in diag) Edges.Remove(e);
        foreach (var n in nodes.ToList()) if (Nodes.ContainsKey(n) && EdgesAt(n).Count == 0) { Nodes.Remove(n); if (Disp != null) DispNodes.Remove(n); }
        var region = RestoreSecs(secs);
        Simplify(region.Where(Nodes.ContainsKey).ToList());
        return $"Usunięto {(endSw.Count > 0 ? $"rozjazd krzyżowy / skrzyżowanie {N} ze zwrotnicami na końcach skosu ({string.Join(", ", endSw.Select(Station.Lbl))})" : $"rozjazd krzyżowy / skrzyżowanie {N}")}";
    }

    /// <summary>Odcinki zwrotnicowe na torach poziomych po usunięciu zwrotnic – z powrotem do odcinka sąsiedniego toru; zwraca węzły regionu.</summary>
    HashSet<string> RestoreSecs(IEnumerable<(string Node, string Sec)> items)
    {
        var region = new HashSet<string>();
        foreach (var (n, s0) in items)
        {
            if (n == null || s0 == null || !Nodes.ContainsKey(n)) continue;
            var mine = EdgeObjs.Where(x => Sv(x["sec"]) == s0 && IsHorizontal(x)).ToList();
            if (mine.Count == 0) continue;
            var rn = mine.SelectMany(x => new[] { Sv(x["a"]), Sv(x["b"]) }).Append(n).ToHashSet();
            var nb = rn.SelectMany(EdgesAt).Where(x => IsHorizontal(x) && Sv(x["sec"]) != s0 && !IsSzlak(x)).Select(x => Sv(x["sec"])).FirstOrDefault()
                     ?? NewSecId(Sv(Nodes[n]["post"]) ?? "", "it");
            foreach (var x in mine) x["sec"] = nb;
            region.UnionWith(rn);
        }
        return region;
    }

    /// <summary>Węzły rozjazdu, które przesuwa się razem ze zwrotnicą: punkt środkowy, PR / KR (tory odcinka zwrotnicowego), łącznik i druga zwrotnica.</summary>
    public HashSet<string> TurnoutNodes(string nid)
    {
        var set = new HashSet<string> { nid };
        var grpSw = Switches.Select(x => (JsonObject)x.Value).FirstOrDefault(x => Sv(x["node"]) == nid && Sv(x["grp"]) != null);
        void AddSw(string n)
        {
            set.Add(n);
            var sw = Switches.Select(x => (JsonObject)x.Value).FirstOrDefault(x => Sv(x["node"]) == n); if (sw == null) return;
            var sec = Sv(sw["sec"]);
            foreach (var e in EdgesAt(n).Where(x => IsHorizontal(x) && Sv(x["sec"]) == sec))
            {
                var o = Sv(e["a"]) == n ? Sv(e["b"]) : Sv(e["a"]);
                if (IsPlainNode(o) && EdgesAt(o).All(x => IsHorizontal(x))) set.Add(o);
            }
        }
        if (grpSw != null)
        {
            var (cn, _, es) = CrossingParts(Sv(grpSw["post"]), Sv(grpSw["grp"]));
            set.UnionWith(cn); foreach (var k in es) AddSw(Sv(Switches[k]["node"]));
            return set;
        }
        AddSw(nid);
        var (chain, mids, end) = TurnoutBranch(nid);
        foreach (var m in mids) set.Add(m);
        if (chain.Count > 0)
        {
            // środek łącznika (przerwa izolacyjna) i druga zwrotnica
            foreach (var e in chain) { set.Add(Sv(e["a"])); set.Add(Sv(e["b"])); }
            if (Switches.Any(x => Sv(x.Value["node"]) == end)) AddSw(end);
            else if (EdgesAt(end).Count == 1) set.Add(end);
            else if (IsPlainNode(end) && EdgesAt(end).Count == 2 && EdgesAt(end).Any(x => IsHorizontal(x))) set.Add(end);
        }
        return set;
    }

    /// <summary>Węzły toru poziomego (ciąg torów na jednej wysokości, także przez zwrotnice) – do przesuwania toru w pionie.</summary>
    public HashSet<string> BandLine(string eid)
    {
        var e0 = Edge(eid); var res = new HashSet<string>();
        if (e0 == null || !IsHorizontal(e0)) return res;
        var y = Pts(e0)[0].Y; var q = new Queue<string>(new[] { Sv(e0["a"]), Sv(e0["b"]) });
        while (q.Count > 0)
        {
            var n = q.Dequeue(); if (!res.Add(n)) continue;
            foreach (var e in EdgesAt(n))
                if (IsHorizontal(e) && !IsLink(e) && Math.Abs(Pts(e)[0].Y - y) < 0.05) { var o = Sv(e["a"]) == n ? Sv(e["b"]) : Sv(e["a"]); if (!res.Contains(o)) q.Enqueue(o); }
        }
        return res;
    }

    /// <summary>
    /// Po przesunięciu toru w pionie: skosy rozjazdów przy nim dostają z powrotem skos wg typu – zwrotnica na przesuniętym torze
    /// przesuwa się w poziomie (razem z PR / KR), gdy nie przeskoczy sąsiednich węzłów. Zwraca liczbę poprawionych.
    /// </summary>
    public int RefitSkews(HashSet<string> moved, double gapM)
    {
        int k = 0;
        foreach (var (key, sv) in Switches.ToList())
        {
            if (Sv(sv["grp"]) != null) continue;      // rozjazd krzyżowy – bez poprawiania skosu
            var a = Sv(sv["node"]); var (chain, _, end) = TurnoutBranch(a);
            if (chain.Count == 0 || moved.Contains(a) == moved.Contains(end)) continue;
            var fixedN = moved.Contains(a) ? end : a; var movN = moved.Contains(a) ? a : end;
            var (n, _, _, _) = TurnoutGeom(Sv(sv["kind"]) ?? "");
            if (ParseRatio(Sv(sv["ratio"])) is double rn && rn > 1) n = rn;
            var (fx, fy) = Pos(fixedN); var (mx, my) = Pos(movN);
            double want = fx + Math.Sign(mx - fx) * Math.Abs(my - fy) / Uy * n * Ux;
            var grp = Switches.Any(x => Sv(x.Value["node"]) == movN) ? TurnoutNodes(movN).Where(moved.Contains).ToHashSet() : new HashSet<string> { movN };
            double ddx = want - mx;
            if (Math.Abs(ddx) < 0.01) continue;
            // nie przeskoczyć sąsiednich węzłów toru
            bool ok = true;
            foreach (var g in grp)
                foreach (var e in EdgesAt(g).Where(IsHorizontal))
                {
                    var o = Sv(e["a"]) == g ? Sv(e["b"]) : Sv(e["a"]); if (grp.Contains(o)) continue;
                    double gx = Pos(g).X, ox = Pos(o).X;
                    if (Math.Sign(ox - gx) != Math.Sign(ox - gx - ddx) || Math.Abs(ox - gx - ddx) < 0.5 * Ux) ok = false;
                }
            if (!ok) continue;
            MoveGroup(grp, ddx, 0); k++;
        }
        return k;
    }
}

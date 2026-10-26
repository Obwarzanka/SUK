using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Tory na płótnie: rysowanie osi (jedna linia jak w ISDR: tory główne 2 px, boczne 1,2 px, numer toru nad osią),
/// narzędzie Tor (odcinki styczne: prosta / łuk, długość z klawiatury, łączenie z końcem innego toru łukiem albo łukami odwrotnymi),
/// zaznaczanie, przeciąganie wierzchołków i uchwytów łuków (promień), wstawianie wierzchołka dwuklikiem.
/// </summary>
public partial class UtView
{
    /// <summary>Zaznaczony tor i wierzchołek (−1 – cały tor).</summary>
    public string SelectedTrack { get; private set; }
    public int SelectedVertex { get; private set; } = -1;
    public event Action SelectionChanged;
    /// <summary>Narzędzie Tor: wierzchołki toru (cały tor), tor zastępowany albo null (nowy), tor wchłonięty przy połączeniu albo null.</summary>
    public event Action<List<UtVertex>, string, string> TrackDrawn;
    public event Action<string, int, XY> VertexMoved;
    public event Action<string, int, XY> VertexInserted;
    /// <summary>Dwuklik na łuku: podział łuku (tor, wierzchołek łuku, km) – nowy wierzchołek bez załamania.</summary>
    public event Action<string, int, double> ArcSplitAt;
    /// <summary>Zmiana stanu rysowania (opis do paska stanu).</summary>
    public event Action<string> DraftInfo;
    /// <summary>Promień i przejściowa w nowych wierzchołkach [m].</summary>
    public double NewR = 300, NewL = 0;

    public const double Gauge = 1.435, RailHead = 0.072;
    const double SnapPx = 10, HandlePx = 7, HitPx = 6;

    // ---- geometria torów (pamięć podręczna wg wierzchołków)
    /// <summary>Geometria toru z pamięci: podpis (liczby wierzchołków / elementów), trasa, punkty osi co 0,5 m, ramka w układzie.</summary>
    sealed class GeoCache
    {
        public double[] Sig; public TrasaResult Res; public List<(XY P, double Az)> Pts;
        public double X0, Y0, X1, Y1;
    }
    readonly Dictionary<string, GeoCache> geo = new();

    // podpis bez napisów i bez przydziału pamięci przy porównaniu (dawniej string.Join przy każdym rysowaniu każdego toru)
    static int SigLen(UtTrack t) => 1 + t.Vertices.Count * 5 + 1 + (t.Align is UtAlign a ? 3 + a.Els.Count * 3 : 0);
    static IEnumerable<double> SigVals(UtTrack t)
    {
        yield return t.Vertices.Count;
        foreach (var v in t.Vertices) { yield return v.X; yield return v.Y; yield return v.R; yield return v.L; yield return v.L2 ?? double.NaN; }
        yield return t.Align is UtAlign a ? a.Els.Count : -1;
        if (t.Align is UtAlign b) { yield return b.X; yield return b.Y; yield return b.Az; foreach (var e in b.Els) { yield return e.Len; yield return e.R0; yield return e.R1; } }
    }
    /// <summary>Krótki podpis toru do pamięci wyników liczonych z całego projektu (odcinki, drogi szlaków): id i skrót liczb osi.</summary>
    static string Sig(UtTrack t)
    {
        var h = new HashCode(); foreach (var x in SigVals(t)) h.Add(x);
        return t.Id + ":" + h.ToHashCode().ToString("x8");
    }
    static bool SameSig(UtTrack t, double[] sig)
    {
        if (sig.Length != SigLen(t)) return false;
        var vs = t.Vertices; if (sig[0] != vs.Count) return false;
        int j = 1;
        for (int i = 0; i < vs.Count; i++)
        {
            var v = vs[i];
            if (!sig[j].Equals(v.X) || !sig[j + 1].Equals(v.Y) || !sig[j + 2].Equals(v.R) || !sig[j + 3].Equals(v.L) || !sig[j + 4].Equals(v.L2 ?? double.NaN)) return false;
            j += 5;
        }
        if (t.Align is not UtAlign a) return sig[j] == -1;
        if (sig[j] != a.Els.Count || !sig[j + 1].Equals(a.X) || !sig[j + 2].Equals(a.Y) || !sig[j + 3].Equals(a.Az)) return false;
        j += 4;
        foreach (var e in a.Els) { if (!sig[j].Equals(e.Len) || !sig[j + 1].Equals(e.R0) || !sig[j + 2].Equals(e.R1)) return false; j += 3; }
        return true;
    }

    public TrasaResult Geometry(UtTrack t) => Cached(t).Res;
    GeoCache Cached(UtTrack t)
    {
        if (geo.TryGetValue(t.Id, out var c) && SameSig(t, c.Sig)) return c;
        var r = Trasa.Compute(t);
        c = new GeoCache { Sig = SigVals(t).ToArray(), Res = r, Pts = r.Sample(0.5), X0 = double.MaxValue, Y0 = double.MaxValue, X1 = double.MinValue, Y1 = double.MinValue };
        foreach (var (p, _) in c.Pts) { c.X0 = Math.Min(c.X0, p.X); c.Y0 = Math.Min(c.Y0, p.Y); c.X1 = Math.Max(c.X1, p.X); c.Y1 = Math.Max(c.Y1, p.Y); }
        geo[t.Id] = c; return c;
    }

    /// <summary>Ramka toru na ekranie (z ramki w układzie – przekształcenie jest afiniczne, więc obejmuje całą oś).</summary>
    Rect ScreenBox(GeoCache c)
    {
        if (c.Pts.Count == 0) return Rect.Empty;
        var a = ToScreen(new XY(c.X0, c.Y0)); var b = ToScreen(new XY(c.X1, c.Y0)); var d = ToScreen(new XY(c.X0, c.Y1)); var e = ToScreen(new XY(c.X1, c.Y1));
        double x0 = Math.Min(Math.Min(a.X, b.X), Math.Min(d.X, e.X)), x1 = Math.Max(Math.Max(a.X, b.X), Math.Max(d.X, e.X));
        double y0 = Math.Min(Math.Min(a.Y, b.Y), Math.Min(d.Y, e.Y)), y1 = Math.Max(Math.Max(a.Y, b.Y), Math.Max(d.Y, e.Y));
        return new Rect(x0, y0, x1 - x0, y1 - y0);
    }

    /// <summary>Punkt ekranu może leżeć najwyżej <paramref name="px"/> od osi toru (odsiew przed dokładnym szukaniem).</summary>
    bool NearBox(UtTrack t, Point s, double px)
    {
        var b = ScreenBox(Cached(t)); if (b.IsEmpty) return false;
        b.Inflate(px, px); return b.Contains(s);
    }

    UtTrack Track(string id) => Doc?.P.Tracks.FirstOrDefault(t => t.Id == id);

    public void Select(string track, int vertex = -1)
    {
        if (SelectedTrack == track && SelectedVertex == vertex && SelectedTurnout == null && SelectedCrossing == null && SelectedDevice == null && SelectedObj == null) return;
        SelectedTrack = track; SelectedVertex = track == null ? -1 : vertex; SelectedTurnout = null; SelectedCrossing = null; SelectedDevice = null; SelectedObj = null;
        InvalidateVisual(); SelectionChanged?.Invoke();
    }

    // ------------------------------------------------------------------ przyciąganie
    public enum SnapKind { Brak, Koniec, Wierzcholek, Tor }
    public record SnapResult(XY P, SnapKind Kind, string Track, int Vertex);

    /// <summary>Przyciąganie (Ctrl – wyłączone): koniec toru, wierzchołek, punkt na osi toru – w promieniu 10 px.</summary>
    public SnapResult Snap(Point s, string skipTrack = null, int skipVertex = -1)
    {
        var w = ToWorld(s);
        if (Doc == null || Mods.HasFlag(ModifierKeys.Control)) return new(w, SnapKind.Brak, null, -1);
        SnapResult best = null; double bd = SnapPx;
        foreach (var t in Doc.P.Tracks)
            for (int i = 0; i < t.Vertices.Count; i++)
            {
                if (t.Id == skipTrack && i == skipVertex) continue;
                double d = (ToScreen(t.Vertices[i].P) - s).Length;
                bool end = i == 0 || i == t.Vertices.Count - 1;
                if (d < bd + (end ? 3 : 0)) { bd = d; best = new(t.Vertices[i].P, end ? SnapKind.Koniec : SnapKind.Wierzcholek, t.Id, i); }
            }
        if (best != null) return best;
        foreach (var t in Doc.P.Tracks)
        {
            if (t.Id == skipTrack || !NearBox(t, s, bd)) continue;
            var r = Geometry(t); if (r.Elements.Count == 0) continue;
            var (sKm, dist) = r.Nearest(w);
            var q = r.At(sKm).P; double sd = (ToScreen(q) - s).Length;
            if (sd < bd) { bd = sd; best = new(q, SnapKind.Tor, t.Id, -1); }
        }
        return best ?? new(w, SnapKind.Brak, null, -1);
    }

    // ------------------------------------------------------------------ narzędzie Tor
    /// <summary>Tryb narzędzia Tor: prosta na przedłużeniu albo łuk styczny.</summary>
    public enum TorMode { Prosta, Luk }
    TorMode torMode;
    public TorMode Mode { get => torMode; set { torMode = value; LenBuf = ""; DraftChanged(); ModeChanged?.Invoke(); } }
    public event Action ModeChanged;
    /// <summary>Uchwyt łuku przeciągnięty: tor, wierzchołek, nowy promień [m].</summary>
    public event Action<string, int, double> ArcRadiusChanged;

    // rysowany wielobok (przy przedłużeniu – z wierzchołkami toru, ostatni to koniec); historia do Backspace
    readonly List<UtVertex> dv = new();
    readonly Stack<(List<UtVertex> V, int Base)> hist = new();
    int baseCount, keepFrom;     // liczba wierzchołków toru przedłużanego; od którego indeksu wolno usuwać wierzchołki na prostej
    string extendId; bool extendAtStart;
    /// <summary>Długość wpisywana z klawiatury [m w terenie]: prosta – długość, łuk – długość łuku.</summary>
    public string LenBuf { get; private set; } = "";
    /// <summary>Punkty narysowanej części (od początku rysowania albo od przedłużanego końca).</summary>
    public IReadOnlyList<XY> Draft => dv.Skip(Math.Max(0, baseCount - 1)).Select(v => v.P).ToList();

    /// <summary>W toku rysowania (Tor) albo wskazywania (Równoległy) – Del nie usuwa zaznaczenia.</summary>
    public bool Busy => dv.Count > 0 || parSrc != null || devFrom != null || arcFrom != null;

    double KAt(XY p) => Geo.InPoland(p) ? Geo.ScaleFactor(p) : 1;
    static List<UtVertex> Clone(IEnumerable<UtVertex> v) => v.Select(x => x.Clone()).ToList();
    double RNow => NewR > 0 ? NewR : 300;

    /// <summary>Kandydat na kolejny odcinek: wielobok po dodaniu, opis, cel połączenia (koniec innego toru), błąd.</summary>
    record Cand(List<UtVertex> V, string Info, SnapResult Join, string CrossTo = null);
    /// <summary>Łącznik z dwóch rozjazdów: rozjazd, od którego odgałęzienia zaczęto, i rozjazd wskazany.</summary>
    public event Action<string, string> CrossoverJoin;

    /// <summary>Rozjazd pojedynczy z samym odgałęzieniem (3 wierzchołki), którego odgałęzieniem jest tor; null – inny tor.</summary>
    UtTurnout StubOwner(string track) =>
        Doc != null && Rozjazdy.SkewOwner(Doc.P, track) is UtTurnout t && Track(track)?.Vertices.Count == 3 ? t : null;

    /// <summary>
    /// Start na końcu odgałęzienia rozjazdu A (nic jeszcze nie narysowano) i kursor na odgałęzieniu rozjazdu B – łącznik:
    /// podgląd na kopii projektu (B przesunięty wzdłuż toru tak, żeby skosy się zeszły).
    /// </summary>
    Cand CrossCandidate(Point s)
    {
        if (extendId == null || hist.Count > 0 || StubOwner(extendId) is not UtTurnout a) return null;
        var hit = HitTrack(s); if (hit == null || hit == extendId || StubOwner(hit) is not UtTurnout b) return null;
        var tmp = UtDocument.Deserialize(UtDocument.Serialize(Doc.P));
        var err = Rozjazdy.MakeCrossover(tmp, a.Id, b.Id, out var shift);
        if (err != null) return new(null, $"Łącznik {a.Number}/{b.Number}: {err}", null);
        var link = tmp.Tracks.First(x => x.Id == a.Branch);
        var note = Rozjazdy.Check(tmp, tmp.Turnouts.First(x => x.Id == b.Id));
        return new(Clone(link.Vertices), $"łącznik {a.Number}/{b.Number} – rozjazd {b.Number} przesunie się o {shift.ToString("0.##", CultureInfo.GetCultureInfo("pl-PL"))} m" + (note.Count > 0 ? " – " + string.Join("; ", note) : "") + " – klik", null, b.Id);
    }

    /// <summary>Koniec toru, z którym można się połączyć (nie przedłużany tor, nie koniec ustawiany przez rozjazd).</summary>
    bool JoinOk(SnapResult sn) =>
        sn.Kind == SnapKind.Koniec && Track(sn.Track) is UtTrack t && t.Vertices.Count >= 2 && t.Id != extendId && !Locked(t.Id, sn.Vertex);

    Cand Candidate(Point s, double? typed = null)
    {
        if (dv.Count == 0) return null;
        var sn = Snap(s, extendId);
        if (typed == null && CrossCandidate(s) is Cand cc) return cc;
        var e = dv[^1].P; double k = KAt(e);
        var az = Budowa.EndAz(dv);
        // koniec na końcu innego toru – połączenie styczne (łuk albo łuki odwrotne)
        if (typed == null && az != null && JoinOk(sn))
        {
            var tv = Track(sn.Track).Vertices; bool atStart = sn.Vertex == 0;
            double az2 = atStart ? Trasa.Az(tv[0].P, tv[1].P) : Trasa.Az(tv[^1].P, tv[^2].P);   // dojazd w głąb toru
            var mid = Budowa.Connect(e, az.Value, sn.P, az2, RNow, NewL, k, out var r, out var note);
            if (mid == null) return new(null, "Połączenie: " + note, null);
            var nv = Clone(dv); nv.AddRange(mid); nv.Add(new UtVertex { X = sn.P.X, Y = sn.P.Y });
            string what = mid.Count == 0 ? "połączenie na prostej" : mid.Count == 1 ? $"połączenie łukiem R {r:0.#}" : $"połączenie łukami odwrotnymi R {r:0.#}";
            return new(nv, what + (note != null ? " – " + note : "") + " – klik łączy tory", sn);
        }
        List<UtVertex> res; string info;
        if (torMode == TorMode.Luk && az != null)   // bez kierunku (pierwszy odcinek) – prosta
        {
            if (az == null) return new(null, "Łuk: najpierw prosta (kierunek) albo zacznij na końcu toru", null);
            var w = ToWorld(s); var rt = Element.Right(az.Value);
            double side = (w - e).X * rt.X + (w - e).Y * rt.Y >= 0 ? 1 : -1;
            double ang;
            if (NewR <= 0)
            {
                // R = 0 na pasku: łuk styczny do końca, przechodzący przez kursor – promień za kursorem (co 1 m)
                var d = Element.Dir(az.Value); var v = w - e;
                double along = v.X * d.X + v.Y * d.Y, lat = Math.Abs(d.X * v.Y - d.Y * v.X);
                if (lat < 1e-3) return new(null, "Łuk (R z kursora): odsuń kursor w bok od kierunku toru", null);
                double rf = Math.Max(1, Math.Round((along * along + lat * lat) / (2 * lat) / k));
                ang = typed is double lf ? side * Math.Min(lf / rf, 170 * Math.PI / 180) : side * Math.Clamp(2 * Math.Atan2(lat, along), 0.5 * Math.PI / 180, 170 * Math.PI / 180);
                res = Budowa.Arc(dv, rf, NewL, ang, k);
                return new(res, $"łuk R {rf:0} (z kursora) {(ang > 0 ? "w prawo" : "w lewo")}, {Math.Abs(ang) * 180 / Math.PI:0.##}°, {Len3(rf * Math.Abs(ang))}", null);
            }
            if (typed is double ls) ang = side * Math.Min(ls / RNow, 170 * Math.PI / 180);
            else if (Mods.HasFlag(ModifierKeys.Shift) && Doc != null && Budowa.ArcAngleParallel(dv, side, Doc.P.SchematAz()) is var pa && pa != 0) ang = pa;
            else ang = Budowa.ArcAngleTo(dv, RNow, k, w);
            res = Budowa.Arc(dv, RNow, NewL, ang, k);
            info = $"łuk R {RNow:0.##} {(ang > 0 ? "w prawo" : "w lewo")}, {Math.Abs(ang) * 180 / Math.PI:0.##}°, {Len3(RNow * Math.Abs(ang))}";
        }
        else if (az == null || Mods.HasFlag(ModifierKeys.Alt))
        {
            // pierwszy odcinek albo Alt: kierunek wolny (z Alt – załamanie z łukiem R w wierzchołku)
            var w = sn.P;
            if (typed is double l0)
            {
                var c = ToWorld(s) - e; if (c.Length < 1e-9) return null;
                w = e + c * (l0 * k / c.Length);
            }
            if (XY.Dist(w, e) < 1e-6) return null;
            res = Clone(dv);
            if (az != null) { res[^1].R = NewR; res[^1].L = NewR > 0 ? NewL : 0; }
            res.Add(new UtVertex { X = w.X, Y = w.Y });
            info = $"prosta {Len3(Geo.GroundDistance(e, w))}, az {Geo.Azimuth(e, w):0.0}°" + (az != null ? " (Alt – załamanie z łukiem)" : "");
        }
        else if (baseCount == 0 && dv.Count == 2)
        {
            // tor ma dopiero jedną prostą: klik przestawia jej koniec – kierunek od początku do kursora (dalej = dokładniej)
            var a0 = dv[0].P; var w = sn.P;
            if (typed is double l1) { var c = ToWorld(s) - a0; if (c.Length < 1e-9) return null; w = a0 + c * (l1 * k / c.Length); }
            if (XY.Dist(w, a0) < 1e-6) return null;
            res = new() { dv[0].Clone(), new UtVertex { X = w.X, Y = w.Y } };
            info = $"prosta {Len3(Geo.GroundDistance(a0, w))}, az {Geo.Azimuth(a0, w):0.00}° – klik przestawia koniec (Tab – łuk)";
        }
        else
        {
            var w = ToWorld(s); var d = Element.Dir(az.Value);
            double len = typed ?? ((w - e).X * d.X + (w - e).Y * d.Y) / k;
            if (len < 0.01) return new(null, "Prosta: przesuń kursor do przodu (Alt – inny kierunek, Tab – łuk)", null);
            res = Budowa.Straight(dv, len, k);
            // jak daleko kursor jest od kierunku – żeby było widać, że przedłużenie rozjeżdża się ze zdjęciem
            double off = Math.Abs(d.X * (w - e).Y - d.Y * (w - e).X) / k;
            info = $"prosta {Len3(len)}" + (typed == null && off >= 0.2 ? $" – kursor {off.ToString("0.0", CultureInfo.GetCultureInfo("pl-PL"))} m obok kierunku (Alt – inny kierunek)" : "");
        }
        return new(res, info, null);
    }

    /// <summary>Przyjęcie odcinka: zapamiętanie stanu (Backspace) i uproszczenie wieloboku.</summary>
    void Commit(List<UtVertex> v)
    {
        hist.Push((Clone(dv), baseCount));
        dv.Clear(); dv.AddRange(v);
        baseCount -= Budowa.Simplify(dv, keepFrom, baseCount);
        LenBuf = "";
    }

    void DraftClick(Point s)
    {
        if (dv.Count == 0) { StartDraft(s); return; }
        var c = Candidate(s);
        if (c?.V == null) { if (c != null) DraftInfo?.Invoke(c.Info); return; }
        if (c.CrossTo != null && StubOwner(extendId) is UtTurnout ca) { CancelDraft(); CrossoverJoin?.Invoke(ca.Id, c.CrossTo); return; }
        Commit(c.V);
        if (c.Join != null) { FinishDraft(c.Join); return; }
        DraftChanged();
    }

    void StartDraft(Point s)
    {
        var sn = Snap(s);
        extendId = null; baseCount = 0; keepFrom = 1; hist.Clear(); dv.Clear();
        // start na końcu istniejącego toru – przedłużenie stycznie
        if (sn.Kind == SnapKind.Koniec && Track(sn.Track) is UtTrack t && t.Vertices.Count >= 2 && !Locked(t.Id, sn.Vertex))
        {
            if (t.Align != null) { DraftInfo?.Invoke(AlignInfo); return; }
            extendId = t.Id; extendAtStart = sn.Vertex == 0;
            dv.AddRange(Clone(t.Vertices)); if (extendAtStart) dv.Reverse();
            baseCount = dv.Count; keepFrom = baseCount - 1;
            if (!extendAtStart && Doc != null && Rozjazdy.SkewOwner(Doc.P, t.Id) != null) keepFrom = Math.Max(keepFrom, 3);   // wierzchołek na skosie zostaje
        }
        else dv.Add(new UtVertex { X = sn.P.X, Y = sn.P.Y });
        LenBuf = ""; DraftChanged();
    }

    /// <summary>Długość z klawiatury [m w terenie]: prosta (w stronę kursora, gdy kierunek wolny) albo łuk o tej długości.</summary>
    public bool DraftLength(double len, Point? cursor = null)
    {
        if (dv.Count == 0 || len <= 0) return false;
        var c = Candidate(cursor ?? (hover is XY h ? ToScreen(h) : ToScreen(dv[^1].P)), len);
        if (c?.V == null) { if (c != null) DraftInfo?.Invoke(c.Info); return false; }
        Commit(c.V); DraftChanged();
        return true;
    }

    /// <summary>Klawisz w narzędziu Tor: cyfry i przecinek – długość, Enter – odcinek / koniec, Tab – prosta / łuk, Backspace – cofnij, Esc – rezygnacja.</summary>
    public bool TorKey(Key k)
    {
        if (tool != UtTool.Tor) return false;
        if (k == Key.Tab) { Mode = torMode == TorMode.Prosta ? TorMode.Luk : TorMode.Prosta; return true; }
        string ch = k switch
        {
            >= Key.D0 and <= Key.D9 => ((char)('0' + (k - Key.D0))).ToString(),
            >= Key.NumPad0 and <= Key.NumPad9 => ((char)('0' + (k - Key.NumPad0))).ToString(),
            Key.OemComma or Key.OemPeriod or Key.Decimal => ",",
            _ => null,
        };
        if (ch != null && dv.Count > 0) { if (!(ch == "," && LenBuf.Contains(','))) LenBuf += ch; DraftChanged(); return true; }
        if (k == Key.Enter)
        {
            if (LenBuf.Length > 0 && double.TryParse(LenBuf.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var len)) DraftLength(len, hover != null ? ToScreen(hover.Value) : null);
            else FinishDraft();
            return true;
        }
        if (k == Key.Back)
        {
            if (LenBuf.Length > 0) LenBuf = LenBuf[..^1];
            else if (hist.Count > 0) { var (v, b) = hist.Pop(); dv.Clear(); dv.AddRange(v); baseCount = b; }
            else CancelDraft();
            DraftChanged(); return true;
        }
        if (k == Key.Escape && dv.Count > 0) { CancelDraft(); return true; }
        return false;
    }

    /// <summary>Tor, który można wchłonąć przy połączeniu: nic na nim nie leży (inaczej zostaje osobno – łączenie z przeniesieniem
    /// obiektów: zaznacz oba tory i Ctrl+J, <see cref="Laczenie"/>).</summary>
    bool MergeOk(UtTrack b) => Doc != null && b.Id != extendId && Laczenie.Free(Doc.P, b);

    /// <summary>Koniec rysowania; <paramref name="join"/> – połączenie z końcem innego toru (scalenie, jeśli wolno).</summary>
    public void FinishDraft(SnapResult join = null)
    {
        if (dv.Count - Math.Max(0, baseCount - 1) < 2) { CancelDraft(); return; }
        var verts = Clone(dv); string replace = extendId, delete = null; bool atStart = extendAtStart;
        if (join != null && Track(join.Track) is UtTrack b && MergeOk(b))
        {
            var bv = Clone(b.Vertices); bool bStart = join.Vertex == 0; if (!bStart) bv.Reverse();
            verts.AddRange(bv.Skip(1));
            Budowa.Simplify(verts, keepFrom);
            if (replace == null) { replace = b.Id; if (!bStart) verts.Reverse(); }   // nowy odcinek wchodzi do toru B, kierunek B zostaje
            else delete = b.Id;
        }
        if (extendId != null && atStart) verts.Reverse();
        CancelDraft();
        TrackDrawn?.Invoke(verts, replace, delete);
    }

    public void CancelDraft() { dv.Clear(); hist.Clear(); extendId = null; baseCount = 0; LenBuf = ""; DraftChanged(); }

    void DraftChanged()
    {
        InvalidateVisual();
        int n = Draft.Count;
        DraftInfo?.Invoke(dv.Count == 0 ? "" : $"Tor ({(torMode == TorMode.Luk ? "łuk" : "prosta")}): {Math.Max(0, n - 1)} odc." + (extendId != null ? " (przedłużenie toru)" : "") + (LenBuf.Length > 0 ? $" · {(torMode == TorMode.Luk ? "długość łuku" : "długość")}: {LenBuf} m – Enter" : ""));
    }

    bool DoubleClick(Point p)
    {
        if (tool == UtTool.Tor) { FinishDraft(); return true; }
        if (tool == UtTool.Wskaz && Track(SelectedTrack) is UtTrack t)
        {
            if (t.Align != null) { DraftInfo?.Invoke(AlignInfo); return true; }
            var w = ToWorld(p);
            // dwuklik na łuku (na osi, w części kołowej) – podział łuku w tym miejscu: nowy wierzchołek bez zmiany kształtu, a nie
            // załamanie R 0 na boku wieloboku (przesunięcie takiego wierzchołka robiło ostre załamanie i psuło sąsiedni łuk)
            {
                var rr = Geometry(t); var (sk, _) = rr.Nearest(w, 0.25);
                if ((ToScreen(rr.At(sk).P) - p).Length <= HitPx && Ciecie.KawalekAt(rr, sk) is { Luk: true } kl && t.Vertices[kl.Vertex].L <= 0)
                { ArcSplitAt?.Invoke(t.Id, kl.Vertex, sk); return true; }
            }
            // wstawienie wierzchołka na boku wieloboku stycznych najbliżej kursora
            int bi = -1; double bd = HitPx * 2; XY bq = default;
            for (int i = 1; i < t.Vertices.Count; i++)
            {
                var a = t.Vertices[i - 1].P; var b = t.Vertices[i].P; var d = b - a; double l2 = d.X * d.X + d.Y * d.Y;
                double u = l2 > 0 ? Math.Clamp(((w.X - a.X) * d.X + (w.Y - a.Y) * d.Y) / l2, 0.02, 0.98) : 0;
                var q = a + d * u; double dist = (ToScreen(q) - p).Length;
                if (dist < bd) { bd = dist; bi = i; bq = q; }
            }
            if (bi < 0)
            {
                // dwuklik na osi toru (na łuku) – wierzchołek na boku, którego łuk dotyczy
                var r = Geometry(t); var (sKm, dist) = r.Nearest(w);
                if ((ToScreen(r.At(sKm).P) - p).Length > HitPx) return false;
                double acc = 0; Element el = r.Elements[0];
                foreach (var e in r.Elements) { if (acc + e.Len >= sKm * r.K) { el = e; break; } acc += e.Len; }
                bi = Math.Clamp(el.Vertex, 1, t.Vertices.Count - 1);
                var a = t.Vertices[bi - 1].P; var b = t.Vertices[bi].P; var dd = b - a; double l2 = dd.X * dd.X + dd.Y * dd.Y;
                bq = a + dd * Math.Clamp(((w.X - a.X) * dd.X + (w.Y - a.Y) * dd.Y) / l2, 0.02, 0.98);
            }
            if (Locked(t.Id, bi) && Locked(t.Id, bi - 1)) { DraftInfo?.Invoke("Ten odcinek odgałęzienia ustawia rozjazd – wierzchołka nie można tu dodać"); return true; }
            VertexInserted?.Invoke(t.Id, bi, bq);
            return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ zaznaczanie i wierzchołki
    (string Track, int Index)? vDrag; XY vDragTo; bool vDragMoved;
    /// <summary>Przeciąganie uchwytu łuku (zamiast wierzchołka) i promień na żywo.</summary>
    bool vArc; double vArcR;

    /// <summary>Uchwyty łuków zaznaczonego toru: wierzchołek i środek łuku (wierzchołki ustawiane przez rozjazd – bez uchwytu).</summary>
    List<(int I, XY Mid)> ArcHandles(UtTrack t)
    {
        var res = new List<(int, XY)>(); var r = Geometry(t);
        for (int i = 1; i < t.Vertices.Count - 1; i++)
        {
            if (Locked(t.Id, i)) continue;
            if (t.Vertices[i].R > 0 && Budowa.ArcMid(r, i) is XY m) res.Add((i, m));
            else if (t.Vertices[i].R <= 0 && Bisector(t.Vertices, i) is XY u)
            {
                // załamanie bez łuku: uchwyt 16 px od wierzchołka do środka kąta – przeciągnięcie tworzy łuk
                var vs = ToScreen(t.Vertices[i].P); var su = ScreenVec(u); su.Normalize();
                res.Add((i, ToWorld(vs + su * 16)));
            }
        }
        return res;
    }

    /// <summary>Kierunek od wierzchołka do środka kąta (tam leży łuk) albo null – wierzchołek na prostej.</summary>
    static XY? Bisector(IReadOnlyList<UtVertex> v, int i)
    {
        var a = v[i - 1].P - v[i].P; var b = v[i + 1].P - v[i].P;
        if (a.Length < 1e-9 || b.Length < 1e-9) return null;
        var u = a * (1 / a.Length) + b * (1 / b.Length);
        return u.Length < 1e-4 ? null : u * (1 / u.Length);       // prawie prosto (załamanie < ok. 0,01°) – bez uchwytu
    }

    /// <summary>Uchwyty łuków i załamań zaznaczonego toru na ekranie (test).</summary>
    public List<(int I, Point P)> ArcHandlePoints() => Track(SelectedTrack) is UtTrack t ? ArcHandles(t).Select(h => (h.I, ToScreen(h.Mid))).ToList() : new();

    public string HitTrack(Point s)
    {
        if (Doc == null) return null;
        var w = ToWorld(s); string best = null; double bd = HitPx;
        foreach (var t in Doc.P.Tracks)
        {
            if (!NearBox(t, s, bd)) continue;
            var r = Geometry(t); if (r.Elements.Count == 0) continue;
            double d = (ToScreen(r.At(r.Nearest(w).S).P) - s).Length;
            if (d < bd) { bd = d; best = t.Id; }
        }
        return best;
    }

    void SelectAt(Point s)
    {
        if (HitTurnout(s) is string rz) { SelectTurnout(rz); return; }
        if (HitCrossing(s) is string cr) { SelectCrossing(cr); return; }
        if (Track(SelectedTrack) is UtTrack t)
            for (int i = 0; i < t.Vertices.Count; i++)
                if ((ToScreen(t.Vertices[i].P) - s).Length <= HandlePx) { Select(t.Id, i); return; }
        Select(HitTrack(s));
    }

    bool PressVertex(Point s)
    {
        if (Track(SelectedTrack) is not UtTrack t) return false;
        if (t.Align != null) return false;
        if (PressHandles(t, s)) return true;
        foreach (var (ai, m) in ArcHandles(t))
            if ((ToScreen(m) - s).Length <= HandlePx)
            {
                vDrag = (t.Id, ai); vArc = true; vArcR = t.Vertices[ai].R; vDragMoved = false;
                Select(t.Id, ai);
                return true;
            }
        vArc = false;
        for (int i = 0; i < t.Vertices.Count; i++)
            if ((ToScreen(t.Vertices[i].P) - s).Length <= HandlePx)
            {
                if (Locked(t.Id, i)) { Select(t.Id, i); DraftInfo?.Invoke("Ten wierzchołek ustawia rozjazd – przesuń rozjazd (zaznacz go i przeciągnij wzdłuż toru)"); return true; }
                vDrag = (t.Id, i); vDragTo = t.Vertices[i].P; vDragMoved = false;
                Select(t.Id, i);
                return true;
            }
        return false;
    }

    void DragVertex(Point s)
    {
        if (downAt is Point d && (s - d).Length > ClickTol) vDragMoved = true;
        if (!vDragMoved) return;
        if (vArc && Track(vDrag.Value.Track) is UtTrack at)
        {
            // środek łuku za kursorem po dwusiecznej; przy wierzchołku (do 5 cm) – bez łuku (R 0)
            int i = vDrag.Value.Index; var vp = at.Vertices[i].P;
            if (Bisector(at.Vertices, i) is XY u)
            {
                var w = ToWorld(s); double dist = (w - vp).X * u.X + (w - vp).Y * u.Y, kk = KAt(vp);
                vArcR = dist < 0.05 * kk ? 0 : Budowa.RadiusForMid(at.Vertices, i, dist, kk);
            }
            DraftInfo?.Invoke(vArcR > 0 ? $"Łuk W{i + 1}: R {vArcR:0} m" : $"W{i + 1}: bez łuku (załamanie) – odciągnij od wierzchołka");
            InvalidateVisual(); return;
        }
        vDragTo = Snap(s, vDrag.Value.Track, vDrag.Value.Index).P;
        // wierzchołek odgałęzienia za rozjazdem – tylko wzdłuż skosu rozjazdu
        if (SkewTurnout(vDrag.Value.Track, vDrag.Value.Index) is UtTurnout ot && Rozjazdy.OnSkew(Doc.P, ot, vDragTo) is var (q, _)) vDragTo = q;
        InvalidateVisual();
    }

    void EndVertexDrag(Point s)
    {
        var (id, i) = vDrag.Value; vDrag = null;
        if (vDragMoved && vArc) ArcRadiusChanged?.Invoke(id, i, vArcR);
        else if (vDragMoved) VertexMoved?.Invoke(id, i, vDragTo);
        vArc = false;
        InvalidateVisual();
    }

    // ------------------------------------------------------------------ rysowanie torów
    static readonly Brush TrackInk = Frozen(Color.FromRgb(0x18, 0x18, 0x18)), TrackElec = Frozen(Color.FromRgb(0x10, 0x48, 0xc8)),
        SelGlow = Frozen(Color.FromArgb(0x90, 0xff, 0xa0, 0x10)), HandleFill = Frozen(Colors.White), HandleSel = Frozen(Color.FromRgb(0xff, 0x90, 0x00)),
        DraftInk = Frozen(Color.FromRgb(0xe0, 0x30, 0x10)), SnapInk = Frozen(Color.FromRgb(0x10, 0xa0, 0x30));
    static readonly Pen PolyPen = Dashed(Color.FromRgb(0x60, 0x60, 0x60), 1), HandlePen = FrozenPen(Color.FromRgb(0x20, 0x20, 0x20), 1.2),
        TickPenT = FrozenPen(Color.FromRgb(0x20, 0x20, 0x20), 1.2), ErrPen = FrozenPen(Color.FromRgb(0xd0, 0x18, 0x18), 2.2),
        DraftPoly = Dashed(Color.FromRgb(0xe0, 0x30, 0x10), 1.2), SnapPen = FrozenPen(Color.FromRgb(0x10, 0xa0, 0x30), 2);

    /// <summary>Szerokość osi [px], stała przy każdym przybliżeniu: główny zasadniczy 2, główny dodatkowy 1,6, boczny 1,2.</summary>
    static double AxisPx(UtTrack t) => t.Cat == UtTrack.MainZ ? 2 : t.Cat == UtTrack.MainD ? 1.6 : 1.2;

    void DrawTracks(DrawingContext dc, double dpi)
    {
        if (Doc == null) return;
        if (Shown("tory"))
        foreach (var t in Doc.P.Tracks)
        {
            var src = Dragged(t);
            var c = Cached(src);
            // poza ekranem – pomiń (ramka z pamięci, bez przeliczania wszystkich punktów)
            var bx = ScreenBox(c);
            if (bx.IsEmpty || bx.Right < -20 || bx.Left > ActualWidth + 20 || bx.Bottom < -20 || bx.Top > ActualHeight + 20) continue;
            bool sel = t.Id == SelectedTrack;
            DrawAxis(dc, src, c.Res, c.Pts, sel, dpi);
        }
        if (Track(SelectedTrack) is UtTrack st && !PrintMode) DrawTrackEdit(dc, st, dpi);
        if (tool == UtTool.Tor) DrawDraft(dc, dpi);
    }

    /// <summary>Krótki opis błędu przy czerwonym kółku (do 60 znaków, pełny w panelu).</summary>
    void ErrLabel(DrawingContext dc, string text, Point at, double dpi)
    {
        if (string.IsNullOrEmpty(text) || PrintMode) return;
        if (text.Length > 60) text = text[..57] + "…";
        var ft = new FormattedText(text, CultureInfo.GetCultureInfo("pl-PL"), FlowDirection.LeftToRight, UiFont, 11, Red, dpi);
        var o = at + new Vector(12, 4);
        dc.DrawRoundedRectangle(Halo, null, new Rect(o.X - 2, o.Y - 1, ft.Width + 4, ft.Height + 2), 2, 2);
        dc.DrawText(ft, o);
    }

    /// <summary>Podgląd trasy z elementów w trakcie przeciągania na wykresie krzywizny.</summary>
    public (string Id, UtAlign A)? AlignPreview;
    /// <summary>Miejsce na torze wskazane na wykresie krzywizny (kółko na mapie).</summary>
    public (string Track, double S)? MarkS;

    static UtAlign Moved(UtAlign a, XY v) { var c = a.Clone(); c.X += v.X; c.Y += v.Y; return c; }

    /// <summary>Tor z elementów – narzędzia wierzchołków go nie zmieniają (edycja na wykresie krzywizny).</summary>
    public bool IsAlign(string id) => Track(id)?.Align != null;
    public const string AlignInfo = "To tor z elementów – edytuj go na wykresie krzywizny pod mapą albo zamień na wielobok (panel toru)";

    /// <summary>Tor w trakcie przeciągania wierzchołka albo uchwytu łuku (podgląd) albo sam tor.</summary>
    UtTrack Dragged(UtTrack t)
    {
        if (hDrag is var (hid, _, _) && hid == t.Id && hPreview != null)
            return new UtTrack { Id = t.Id + "~", Cat = t.Cat, Elec = t.Elec, Number = t.Number, Vertices = hPreview };
        if (AlignPreview is var (pid, pa) && pid == t.Id)
        { var c = new UtTrack { Id = t.Id + "~", Cat = t.Cat, Elec = t.Elec, Number = t.Number, Vertices = t.Vertices.Select(v => v.Clone()).ToList(), Align = pa }; TrasaElem.SyncEnds(c); return c; }
        if (tDrag is var (tid, _) && tid == t.Id && dragging)
            return new UtTrack { Id = t.Id + "~", Cat = t.Cat, Elec = t.Elec, Number = t.Number, Vertices = t.Vertices.Select(v => { var c = v.Clone(); c.X += tDragVec.X; c.Y += tDragVec.Y; return c; }).ToList(),
                Align = t.Align is UtAlign al ? Moved(al, tDragVec) : null };
        if (vDrag is not var (vid, vi) || vid != t.Id || !vDragMoved) return t;
        var src = new UtTrack { Id = t.Id + "~", Cat = t.Cat, Elec = t.Elec, Number = t.Number, Vertices = t.Vertices.Select(v => v.Clone()).ToList() };
        if (vArc) src.Vertices[vi].R = vArcR; else { src.Vertices[vi].X = vDragTo.X; src.Vertices[vi].Y = vDragTo.Y; }
        return src;
    }

    void DrawAxis(DrawingContext dc, UtTrack t, TrasaResult res, List<(XY P, double Az)> pts, bool sel, double dpi)
    {
        if (pts.Count < 2) return;
        var ink = TrackInk;
        double wpx = AxisPx(t);
        if (sel && !PrintMode) dc.DrawGeometry(null, new Pen(SelGlow, wpx + 7) { LineJoin = PenLineJoin.Round }, Line(pts, 0));
        dc.DrawGeometry(null, new Pen(ink, wpx) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat }, Line(pts, 0));
        // błędy geometrii – czerwone kółko w wierzchołku
        foreach (var (v, text, err) in res.Notes)
            if (err && v < t.Vertices.Count) { var ep = ToScreen(t.Vertices[v].P); dc.DrawEllipse(null, ErrP, ep, 9, 9); ErrLabel(dc, text, ep, dpi); }
        // numer toru: kropka i numer nad osią (jak w ISDR), w środku widocznej części toru
        if (t.Number.Length > 0 && res.Elements.Count > 0)
        {
            // z dala od rozjazdów (do 80 px), przy równych – najbliżej środka widocznej części
            double len = res.Length, bestS = -1, lo = double.MaxValue, hi = double.MinValue;
            var cand = new List<(double S, Point P)>();
            for (int i = 0; i <= 60; i++)
            {
                double sk = len * i / 60; var q = ToScreen(res.At(sk).P);
                if (q.X > 30 && q.X < ActualWidth - 30 && q.Y > 20 && q.Y < ActualHeight - 20) { lo = Math.Min(lo, sk); hi = Math.Max(hi, sk); cand.Add((sk, q)); }
            }
            var marks = TurnoutMarks();
            double bestScore = double.MinValue;
            foreach (var (sk, q) in cand)
            {
                double free = 80; foreach (var m in marks) free = Math.Min(free, (m - q).Length);
                double score = free - Math.Abs(sk - (lo + hi) / 2) / Math.Max(1e-9, hi - lo) * 10;
                if (score > bestScore) { bestScore = score; bestS = sk; }
            }
            if (bestS >= 0)
            {
                var (mp, az) = res.At(bestS);
                var ps = ToScreen(mp); var d = ScreenDir(az); var n = new Vector(d.Y, -d.X); if (n.Y > 0) n = -n;   // normalna w górę ekranu
                var ft = new FormattedText(t.Number, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, UiFont, 12, ink, dpi);
                var c = ps + n * 10;
                dc.DrawEllipse(ink, null, new Point(c.X - ft.Width / 2 - 5, c.Y), 2.2, 2.2);
                dc.DrawText(ft, new Point(c.X - ft.Width / 2 + 1, c.Y - ft.Height / 2));
            }
        }
    }

    /// <summary>Punkty rozjazdów i skrzyżowań na ekranie (PR, ŚR, KR) – numery torów stają z dala od nich.</summary>
    List<Point> TurnoutMarks()
    {
        var res = new List<Point>();
        foreach (var t in Doc.P.Turnouts) if (Rozjazdy.Geometry(Doc.P, t) is TurnoutGeo g) { res.Add(ToScreen(g.PR)); res.Add(ToScreen(g.SR)); res.Add(ToScreen(g.KRMain)); }
        foreach (var c in Doc.P.Crossings) if (Rozjazdy.CrossGeo(Doc.P, c) is var (p, _, _, _)) res.Add(ToScreen(p));
        return res;
    }

    /// <summary>Łamana osi przesunięta o d [m w układzie] w prawo (− w lewo) – na ekranie.</summary>
    StreamGeometry Line(List<(XY P, double Az)> pts, double d)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            for (int i = 0; i < pts.Count; i++)
            {
                var (p, az) = pts[i];
                var s = ToScreen(d == 0 ? p : p + Element.Right(az) * d);
                if (i == 0) c.BeginFigure(s, false, false); else c.LineTo(s, true, true);
            }
        }
        g.Freeze(); return g;
    }

    void DrawTrackEdit(DrawingContext dc, UtTrack t, double dpi)
    {
        var src = Dragged(t); var res = Geometry(src);
        var v = src.Vertices;
        // wielobok stycznych
        for (int i = 1; i < v.Count; i++) DashLine(dc, PolyPen, ToScreen(v[i - 1].P), ToScreen(v[i].P));
        // granice elementów (początki i końce przejściowych i łuków)
        foreach (var e in res.Elements.Where(e => e.Kind != ElKind.Prosta))
            foreach (var (p, az) in new[] { e.At(0), e.At(e.Len) })
            {
                var sp = ToScreen(p); var d = ScreenDir(az); var n = new Vector(-d.Y, d.X) * 6;
                dc.DrawLine(TickPenT, sp - n, sp + n);
            }
        for (int i = 0; i < v.Count; i++)
        {
            var s = ToScreen(v[i].P);
            dc.DrawRectangle(i == SelectedVertex ? HandleSel : HandleFill, HandlePen, new Rect(s.X - 4.5, s.Y - 4.5, 9, 9));
        }
        DrawHandles(dc, src);
        // uchwyty łuków (kółko w środku łuku) z promieniem
        foreach (var (i, m) in ArcHandles(src))
        {
            var ms = ToScreen(m);
            if (v[i].R <= 0 && tool != UtTool.Wskaz) continue;     // uchwyt załamania działa tylko we Wskaż
            dc.DrawEllipse(i == SelectedVertex ? HandleSel : HandleFill, v[i].R > 0 ? ArcHandlePen : CornerHandlePen, ms, 5.5, 5.5);
            Label(dc, v[i].R <= 0 ? "R 0 – przeciągnij" : v[i].L > 0 ? $"R {v[i].R:0.##} · L {v[i].L:0.##}" : $"R {v[i].R:0.##}", ms + new Vector(9, -20), Ink, dpi);
        }
    }

    static readonly Pen ArcHandlePen = FrozenPen(Color.FromRgb(0xe0, 0x70, 0x00), 2), CornerHandlePen = Dashed(Color.FromRgb(0xe0, 0x70, 0x00), 2);

    void DrawDraft(DrawingContext dc, double dpi)
    {
        if (hover is not XY h) { DrawDraftDone(dc); return; }
        var hs = ToScreen(h);
        var sn = Snap(hs, extendId);
        if (sn.Kind != SnapKind.Brak)
        {
            var p = ToScreen(sn.P);
            if (sn.Kind == SnapKind.Tor) dc.DrawEllipse(null, SnapPen, p, 6, 6); else dc.DrawRectangle(null, SnapPen, new Rect(p.X - 6, p.Y - 6, 12, 12));
        }
        DrawDraftDone(dc);
        if (dv.Count == 0) return;
        double? typed = LenBuf.Length > 0 && double.TryParse(LenBuf.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var lb) ? lb : null;
        var c = Candidate(hs, typed);
        if (c?.V != null)
        {
            var res = Trasa.Compute(c.V, KAt(dv[^1].P));
            var sp = res.Sample(0.5);
            if (sp.Count >= 2) dc.DrawGeometry(null, new Pen(DraftInk, 1.6) { LineJoin = PenLineJoin.Round, DashStyle = new DashStyle(new double[] { 4, 2 }, 0) }, Line(sp, 0));
            if (!res.Ok) Label(dc, string.Join("; ", res.Notes.Where(n => n.Error).Select(n => n.Text)), hs + new Vector(12, 30), Red, dpi);
        }
        if (c != null) Label(dc, (LenBuf.Length > 0 ? $"{LenBuf} m ⏎ · " : "") + c.Info, hs + new Vector(12, 8), c.V == null ? Red : DraftInk, dpi);
    }

    /// <summary>Narysowana już część (pełny wielobok rysowanego toru).</summary>
    void DrawDraftDone(DrawingContext dc)
    {
        if (dv.Count == 0) return;
        var res = Trasa.Compute(dv, KAt(dv[^1].P));
        var sp = res.Sample(0.5);
        if (sp.Count >= 2) dc.DrawGeometry(null, new Pen(DraftInk, 2) { LineJoin = PenLineJoin.Round }, Line(sp, 0));
        var e = ToScreen(dv[^1].P); dc.DrawRectangle(DraftInk, null, new Rect(e.X - 3, e.Y - 3, 6, 6));
    }
}

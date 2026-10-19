using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Zaznaczanie kilku elementów naraz (Wskaż): Shift + klik albo Ctrl + klik dodaje / zdejmuje element (tor, rozjazd, skrzyżowanie,
/// urządzenie, odcinek, peron, przejazd, punkt km), Shift + przeciągnięcie – prostokąt (wszystko, co leży w nim w całości).
/// Del usuwa wszystkie (jedno Ctrl+Z), Esc albo zwykły klik – koniec zaznaczenia. Do tego menu PPM na torze: usunięcie kawałka
/// (prosta albo łuk pod kursorem) i rozcięcie toru (<see cref="Ciecie"/>).
/// </summary>
public partial class UtView
{
    /// <summary>Zaznaczone elementy (rodzaje jak w <see cref="Kontrola"/>: tor, rozjazd, skrzyzowanie, urzadzenie, odcinek, peron, przejazd, km).</summary>
    public readonly List<(string Kind, string Id)> Multi = new();
    /// <summary>PPM na torze we Wskaż: tor, km kliknięcia [m w terenie].</summary>
    public event Action<string, double> TrackMenu;
    Point? boxFrom, boxTo, ctrlClick;

    public bool InMulti(string kind, string id) => Multi.Contains((kind, id));

    /// <summary>Element pod kursorem (w kolejności jak przy zwykłym kliknięciu).</summary>
    public (string Kind, string Id)? HitAny(Point s)
    {
        if (HitDevice(s) is string d) return (Kontrola.KUrzadzenie, d);
        if (HitObj(s) is var (ok, oid)) return (ok, oid);
        if (HitTurnout(s) is string z) return (Kontrola.KRozjazd, z);
        if (HitCrossing(s) is string c) return (Kontrola.KSkrzyzowanie, c);
        if (HitTrack(s) is string t) return (Kontrola.KTor, t);
        return null;
    }

    (string Kind, string Id)? CurrentSingle() =>
        SelectedDevice != null ? (Kontrola.KUrzadzenie, SelectedDevice) : SelectedObj is var (k, i) ? (k, i) :
        SelectedTurnout != null ? (Kontrola.KRozjazd, SelectedTurnout) : SelectedCrossing != null ? (Kontrola.KSkrzyzowanie, SelectedCrossing) :
        SelectedTrack != null ? (Kontrola.KTor, SelectedTrack) : null;

    void ClearSingle() { SelectedTrack = null; SelectedVertex = -1; SelectedTurnout = null; SelectedCrossing = null; SelectedDevice = null; SelectedObj = null; }

    /// <summary>Shift / Ctrl + klik: element pod kursorem do zaznaczenia albo z niego (pojedyncze zaznaczenie wchodzi do zbioru).</summary>
    public void ToggleMulti(Point s)
    {
        if (HitAny(s) is not var (k, id)) return;
        if (Multi.Count == 0 && CurrentSingle() is var (ck, cid) && (ck, cid) != (k, id)) Multi.Add((ck, cid));
        if (!Multi.Remove((k, id))) Multi.Add((k, id));
        ClearSingle();
        if (Multi.Count == 1) { var (k1, i1) = Multi[0]; Multi.Clear(); SelectKind(k1, i1); return; }
        DraftInfo?.Invoke(MultiInfo());
        InvalidateVisual(); SelectionChanged?.Invoke();
    }

    /// <summary>Zwykłe zaznaczenie jednego elementu danego rodzaju.</summary>
    public void SelectKind(string kind, string id)
    {
        switch (kind)
        {
            case Kontrola.KTor: Select(id); break;
            case Kontrola.KRozjazd: SelectTurnout(id); break;
            case Kontrola.KSkrzyzowanie: SelectCrossing(id); break;
            case Kontrola.KUrzadzenie: SelectDevice(id); break;
            default: SelectObj(kind, id); break;
        }
    }

    public void ClearMulti()
    {
        if (Multi.Count == 0) return;
        Multi.Clear(); InvalidateVisual(); SelectionChanged?.Invoke();
    }

    /// <summary>Zaznaczenie listy elementów (np. z testu).</summary>
    public void SetMulti(IEnumerable<(string Kind, string Id)> items)
    {
        Multi.Clear(); Multi.AddRange(items.Distinct()); ClearSingle();
        InvalidateVisual(); SelectionChanged?.Invoke();
    }

    public string MultiInfo()
    {
        var n = Multi.GroupBy(x => x.Kind).Select(g => $"{KindName(g.Key, g.Count())} {g.Count()}");
        return $"Zaznaczone: {Multi.Count} ({string.Join(", ", n)}) – Shift + klik dodaje / zdejmuje, Del usuwa wszystkie" +
            (Multi.Count(x => x.Kind == Kontrola.KTor) >= 2 ? ", Ctrl+J łączy tory" : "") + ", Esc – koniec";
    }

    public static string KindName(string kind, int n) => kind switch
    {
        Kontrola.KTor => n == 1 ? "tor" : "tory",
        Kontrola.KRozjazd => n == 1 ? "rozjazd" : "rozjazdy",
        Kontrola.KSkrzyzowanie => n == 1 ? "skrzyżowanie" : "skrzyżowania",
        Kontrola.KUrzadzenie => n == 1 ? "urządzenie" : "urządzenia",
        Kontrola.KOdcinek => n == 1 ? "odcinek" : "odcinki",
        Kontrola.KPeron => n == 1 ? "peron" : "perony",
        Kontrola.KPrzejazd => n == 1 ? "przejazd" : "przejazdy",
        Kontrola.KKm => n == 1 ? "punkt km" : "punkty km",
        _ => kind,
    };

    /// <summary>Wciśnięcie we Wskaż z Ctrl (zaznaczanie) albo z Shift (klik – zaznaczanie, przeciągnięcie – prostokąt); zwykły klik kończy zaznaczenie kilku.</summary>
    bool PressMulti(Point s)
    {
        bool ctrl = Mods.HasFlag(ModifierKeys.Control), shift = Mods.HasFlag(ModifierKeys.Shift);
        if (ctrl)
        {
            // Ctrl + przeciągnięcie zaznaczonego toru przesuwa tor – klik bez ruchu i tak przełącza (EndTrackMove)
            if (Multi.Count == 0 && SelectedTrack != null && HitTrack(s) == SelectedTrack) return false;
            ctrlClick = s; return true;
        }
        if (shift) { boxFrom = boxTo = s; return true; }
        if (Multi.Count > 0) { Multi.Clear(); SelectionChanged?.Invoke(); InvalidateVisual(); }
        return false;
    }

    bool DragMulti(Point s)
    {
        if (boxFrom == null) return ctrlClick != null;
        boxTo = s; InvalidateVisual(); return true;
    }

    bool ReleaseMulti(Point s)
    {
        if (ctrlClick is Point c) { ctrlClick = null; if ((s - c).Length <= ClickTol) ToggleMulti(s); return true; }
        if (boxFrom is not Point a) return false;
        boxFrom = boxTo = null;
        var r = new Rect(a, s);
        if ((s - a).Length <= ClickTol) { InvalidateVisual(); ToggleMulti(s); return true; }      // Shift + klik
        SetMulti(Multi.Concat(InBox(r)));
        if (Multi.Count == 1) { var (k1, i1) = Multi[0]; Multi.Clear(); SelectKind(k1, i1); }
        else DraftInfo?.Invoke(Multi.Count == 0 ? "Prostokąt: nic nie leży w nim w całości" : MultiInfo());
        return true;
    }

    /// <summary>Elementy leżące w całości w prostokącie ekranu (tor – cała oś, peron – oba końce, reszta – punkt na torze).</summary>
    List<(string, string)> InBox(Rect r)
    {
        var res = new List<(string, string)>(); if (Doc == null) return res;
        bool In(XY p) => r.Contains(ToScreen(p));
        foreach (var t in Doc.P.Tracks)
        {
            // ramka toru w prostokącie – cały tor w nim; ramka obok – na pewno nie; inaczej punkty osi z pamięci
            var c = Cached(t); var b = ScreenBox(c); if (b.IsEmpty || !r.IntersectsWith(b)) continue;
            if (r.Contains(b) || c.Pts.All(q => In(q.P))) res.Add((Kontrola.KTor, t.Id));
        }
        foreach (var z in Doc.P.Turnouts) if (Rozjazdy.Geometry(Doc.P, z) is TurnoutGeo g && In(g.PR) && In(g.KRMain)) res.Add((Kontrola.KRozjazd, z.Id));
        foreach (var c in Doc.P.Crossings) if (Rozjazdy.CrossGeo(Doc.P, c) is var (cp, _, _, _) && In(cp)) res.Add((Kontrola.KSkrzyzowanie, c.Id));
        XY? At(string track, double s) => Track(track) is UtTrack t && Geometry(t) is var g && g.Elements.Count > 0 ? g.At(Math.Clamp(s, 0, g.Length)).P : null;
        foreach (var d in Doc.P.Devices) if (At(d.Track, d.S) is XY p && In(p)) res.Add((Kontrola.KUrzadzenie, d.Id));
        foreach (var x in Doc.P.Sections) if (At(x.Track, x.S) is XY p && In(p)) res.Add((Kontrola.KOdcinek, x.Id));
        foreach (var x in Doc.P.LevelCrossings) if (At(x.Track, x.S) is XY p && In(p)) res.Add((Kontrola.KPrzejazd, x.Id));
        foreach (var x in Doc.P.KmPoints) if (At(x.Track, x.S) is XY p && In(p)) res.Add((Kontrola.KKm, x.Id));
        foreach (var x in Doc.P.Platforms) if (At(x.Track, x.S0) is XY p0 && At(x.Track, x.S1) is XY p1 && In(p0) && In(p1)) res.Add((Kontrola.KPeron, x.Id));
        return res;
    }

    public bool MultiKey(Key k)
    {
        if (k == Key.Escape && Multi.Count > 0) { ClearMulti(); DraftInfo?.Invoke("Zaznaczenie zdjęte"); return true; }
        return false;
    }

    /// <summary>Podświetlenie zaznaczonych (kilku) i prostokąt zaznaczania.</summary>
    void DrawMulti(DrawingContext dc)
    {
        if (boxFrom is Point a && boxTo is Point b)
        {
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(0x20, 0x60, 0xd0)), 1) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) };
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x22, 0x20, 0x60, 0xd0)), pen, new Rect(a, b));
        }
        if (Multi.Count == 0 || Doc == null || PrintMode) return;
        var hl = new Pen(new SolidColorBrush(Color.FromArgb(0x70, 0xff, 0x90, 0x00)), 9) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        foreach (var (k, id) in Multi)
        {
            if (k == Kontrola.KTor && Track(id) is UtTrack t) { dc.DrawGeometry(null, hl, Line(Cached(t).Pts, 0)); continue; }
            XY? p = k switch
            {
                Kontrola.KRozjazd => Doc.P.Turnouts.FirstOrDefault(x => x.Id == id) is UtTurnout z ? Rozjazdy.Geometry(Doc.P, z)?.SR : null,
                Kontrola.KSkrzyzowanie => Doc.P.Crossings.FirstOrDefault(x => x.Id == id) is UtCrossing c && Rozjazdy.CrossGeo(Doc.P, c) is var (cp, _, _, _) ? cp : null,
                Kontrola.KUrzadzenie => Doc.P.Devices.FirstOrDefault(x => x.Id == id) is UtDevice d && Track(d.Track) is UtTrack dt ? Geometry(dt).At(d.S).P : null,
                _ => ObjRef(k, id) is var (tr, s) && Track(tr) is UtTrack ot ? Geometry(ot).At(s).P : null,
            };
            if (p is XY q) dc.DrawEllipse(RzSel, null, ToScreen(q), 11, 11);
        }
    }
}

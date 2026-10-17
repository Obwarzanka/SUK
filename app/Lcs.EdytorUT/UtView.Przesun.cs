using System.Windows;
using System.Windows.Input;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Przesuwanie myszą i klawiaturą: strzałki – zaznaczony rozjazd wzdłuż toru (0,1 m, Shift – 1 m), zaznaczony wierzchołek albo cały
/// tor w kierunku strzałki na ekranie (5 cm, Shift – 0,5 m; w Schemacie góra / dół = w poprzek osi, czyli międzytorze);
/// Ctrl + przeciągnięcie zaznaczonego toru – przesunięcie całego toru (rozjazdy na nim jadą razem).
/// </summary>
public partial class UtView
{
    /// <summary>Tor przesunięty o wektor [m w układzie].</summary>
    public event Action<string, XY> TrackMoved;
    /// <summary>PPM na rozjeździe (Wskaż) – menu.</summary>
    public event Action<string> TurnoutMenu;

    (string Id, XY From)? tDrag; XY tDragVec;

    /// <summary>Jednostkowy wektor w świecie odpowiadający kierunkowi na ekranie (w Schemacie – z uwzględnieniem obrotu i rozciągnięcia).</summary>
    XY WorldDir(Vector v)
    {
        var c = new Point(ActualWidth / 2, ActualHeight / 2);
        var u = ToWorld(c + v) - ToWorld(c); double l = u.Length;
        return l > 0 ? u * (1 / l) : new XY(0, 0);
    }

    /// <summary>Strzałka: przesunięcie zaznaczonego rozjazdu, wierzchołka albo toru. True – obsłużona.</summary>
    public bool NudgeKey(Key k, bool shift)
    {
        if (Doc == null || Busy) return false;
        Vector v = k switch { Key.Left => new(-1, 0), Key.Right => new(1, 0), Key.Up => new(0, -1), Key.Down => new(0, 1), _ => default };
        if (v.Length == 0) return false;
        if (Dev(SelectedDevice) is UtDevice dv && Urzadzenia.Pos(Doc.P, dv) is var (_, daz, _))
        {
            double dd = v * (ScreenDir(daz) * dv.Dir);              // kierunek rosnącego km toru na ekranie
            if (Math.Abs(dd) < 0.2) return true;
            DeviceMoved?.Invoke(dv.Id, Math.Round(dv.S + Math.Sign(dd) * (shift ? 1 : 0.1), 3));
            return true;
        }
        if (SelectedTurnout != null && Doc.P.Turnouts.FirstOrDefault(x => x.Id == SelectedTurnout) is UtTurnout t)
        {
            var m = t.BranchAtEnd && t.Partner != null ? Doc.P.Turnouts.FirstOrDefault(x => x.Id == t.Partner) ?? t : t;
            var g = Rozjazdy.Geometry(Doc.P, m); if (g == null) return false;
            var d0 = ScreenDir(g.Az) * m.Dir;                 // kierunek rosnącego kilometrażu toru na ekranie
            double dot = v * d0;
            if (Math.Abs(dot) < 0.2) return true;              // strzałka w poprzek toru – rozjazd jedzie tylko wzdłuż
            TurnoutMoved?.Invoke(m.Id, Math.Round(m.S + Math.Sign(dot) * (shift ? 1 : 0.1), 3));
            return true;
        }
        if (Track(SelectedTrack) is UtTrack tr)
        {
            var u = WorldDir(v); double step = (shift ? 0.5 : 0.05) * KAt(tr.Vertices[0].P);
            if (SelectedVertex >= 0 && SelectedVertex < tr.Vertices.Count)
            {
                if (Locked(tr.Id, SelectedVertex)) { DraftInfo?.Invoke("Ten wierzchołek ustawia rozjazd – przesuń rozjazd"); return true; }
                VertexMoved?.Invoke(tr.Id, SelectedVertex, tr.Vertices[SelectedVertex].P + u * step);
            }
            else TrackMoved?.Invoke(tr.Id, u * step);
            return true;
        }
        return false;
    }

    /// <summary>Wskaż + Ctrl: wciśnięcie na zaznaczonym torze – przesuwanie całego toru.</summary>
    bool PressTrackMove(Point s)
    {
        if (!Mods.HasFlag(ModifierKeys.Control) || SelectedTrack == null || HitTrack(s) != SelectedTrack) return false;
        tDrag = (SelectedTrack, ToWorld(s)); tDragVec = new XY(0, 0);
        return true;
    }

    void DragTrackMove(Point s)
    {
        tDragVec = ToWorld(s) - tDrag.Value.From;
        DraftInfo?.Invoke($"Przesunięcie toru o {Len3(tDragVec.Length / KAt(tDrag.Value.From))}");
        InvalidateVisual();
    }

    void EndTrackMove()
    {
        var (id, from) = tDrag.Value; tDrag = null;
        if (dragging && tDragVec.Length > 1e-6) TrackMoved?.Invoke(id, tDragVec);
        else if (!dragging) ToggleMulti(ToScreen(from));     // Ctrl + klik na zaznaczonym torze – zaznaczanie kilku
        InvalidateVisual();
    }
}

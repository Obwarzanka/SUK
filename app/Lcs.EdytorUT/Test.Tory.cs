using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>Test etapu 3 – tory: geometria trasy i budowa toru klikami jak użytkownik (część testu --test).</summary>
public static partial class Test
{
    /// <summary>Zrzut z bliska (szyny): nazwa_szyny.png.</summary>
    static string shotRails;

    static UtVertex V(XY p, double r = 0, double l = 0) => new() { X = p.X, Y = p.Y, R = r, L = l };

    /// <summary>Ciągłość: koniec każdego elementu = początek następnego (położenie [m], azymut [rad]).</summary>
    static (double Pos, double Az) Continuity(TrasaResult r)
    {
        double dp = 0, da = 0;
        for (int i = 1; i < r.Elements.Count; i++)
        {
            var (p1, a1) = r.Elements[i - 1].At(r.Elements[i - 1].Len); var (p2, a2) = r.Elements[i].At(0);
            dp = Math.Max(dp, XY.Dist(p1, p2)); da = Math.Max(da, Math.Abs(Math.IEEERemainder(a1 - a2, 2 * Math.PI)));
        }
        return (dp, da);
    }

    static async Task RunTory(MainWindow w)
    {
        var inv = CultureInfo.InvariantCulture;
        string M(double v) => v.ToString("0.0000", inv);
        // ---------------- geometria (model)
        var o = Geo.ToPuwg92(51.7631, 19.5438); double k = Geo.ScaleFactor(o);
        XY Go(XY from, double azDeg, double len) => from + Element.Dir(azDeg * Math.PI / 180) * (len * k);
        var a0 = Go(o, 280, 400); var a1 = o; var a2 = Go(o, 130, 350);    // zwrot o 30° w prawo
        // łuk bez przejściowych
        var r1 = Trasa.Compute(new[] { V(a0), V(a1, 300), V(a2) }, k);
        var c1 = Continuity(r1);
        Ok(r1.Ok && r1.Elements.Select(e => e.Kind).SequenceEqual(new[] { ElKind.Prosta, ElKind.Luk, ElKind.Prosta }) && c1.Pos < 1e-9 && c1.Az < 1e-12, $"łuk R 300 w wierzchołku: prosta – łuk – prosta, styki ciągłe (położenie {c1.Pos:0e0} m, kierunek {c1.Az:0e0} rad)");
        var arc = r1.Elements[1]; var cen = arc.Start + Element.Right(arc.Az0) * arc.R;
        double rdev = Enumerable.Range(0, 21).Max(i => Math.Abs(XY.Dist(arc.At(arc.Len * i / 20).P, cen) - 300 * k));
        Ok(rdev < 1e-9 && Math.Abs(arc.Len / k - 300 * 30 * Math.PI / 180) < 1e-6, $"łuk: punkty w odległości R od środka, długość R·α = {M(arc.Len / k)} m");
        Ok(Math.Abs(r1.Tangent[1] / k - 300 * Math.Tan(15 * Math.PI / 180)) < 1e-6, $"styczna T = R·tg(α/2) = {M(r1.Tangent[1] / k)} m");
        Ok(Math.Abs(r1.Length - (400 + 350 - 2 * 300 * Math.Tan(15 * Math.PI / 180) + 300 * 30 * Math.PI / 180)) < 1e-6, $"długość toru w terenie = proste + łuk ({M(r1.Length)} m)");
        // z przejściowymi
        var r2 = Trasa.Compute(new[] { V(a0), V(a1, 300, 60), V(a2) }, k);
        var c2 = Continuity(r2);
        Ok(r2.Ok && r2.Elements.Select(e => e.Kind).SequenceEqual(new[] { ElKind.Prosta, ElKind.Przejsciowa, ElKind.Luk, ElKind.Przejsciowa, ElKind.Prosta }) && c2.Pos < 1e-6 && c2.Az < 1e-9,
            $"R 300 + przejściowe L 60 (parabola 3°): 5 elementów, styki ciągłe (położenie {c2.Pos:0e0} m, kierunek {c2.Az:0e0} rad)");
        var tr = r2.Elements[1]; var arc2 = r2.Elements[2]; var cen2 = arc2.Start + Element.Right(arc2.Az0) * arc2.R;
        double tau = Math.Atan(60.0 / 600);
        Ok(Math.Abs(arc2.Len / Math.Abs(arc2.R) - (30 * Math.PI / 180 - 2 * tau)) < 1e-9 && Math.Abs(XY.Dist(tr.End, cen2) - 300 * k) < 1e-9, $"łuk kołowy ma kąt α − 2τ (τ = atan(L/2R) = {tau * 180 / Math.PI:0.###}°), koniec przejściowej na okręgu");
        // krzywizna: na początku przejściowej 0, na końcu ≈ 1/R
        double Curv(Element e, double s) { double h = 0.05; var (_, z1) = e.At(Math.Max(0, s - h)); var (_, z2) = e.At(Math.Min(e.Len, s + h)); return Math.Abs(z2 - z1) / (Math.Min(e.Len, s + h) - Math.Max(0, s - h)); }
        double kEnd = Curv(tr, tr.Len) * 300 * k, kStart = Curv(tr, 0) * 300 * k;
        Ok(kStart < 0.01 && Math.Abs(kEnd - 1) < 0.02, $"krzywizna przejściowej: na początku {kStart:0.000}/R, na końcu {kEnd:0.000}/R");
        var outE = r2.Elements[3]; var lastStraight = r2.Elements[4];
        Ok(XY.Dist(outE.End, lastStraight.Start) < 1e-9 && Math.Abs(Math.IEEERemainder(lastStraight.Az0 - Trasa.Az(a1, a2), 2 * Math.PI)) < 1e-12, "przejściowa wyjściowa kończy się na stycznej wyjściowej");
        // długość prostej = odległość geodezyjna
        var b0 = Go(o, 100, 0); var b1 = Go(o, 100, 1000);
        var r3 = Trasa.Compute(new[] { V(b0), V(b1) }, k);
        var (la, lo) = Geo.ToWgs84(b0); var (la2, lo2) = Geo.ToWgs84(b1);
        Ok(Math.Abs(r3.Length - 1000) < 0.002 && Math.Abs(r3.Length - Geo.GeodesicDistance(la, lo, la2, lo2)) < 0.002, $"prosta 1000 m w terenie = odległość geodezyjna ({M(r3.Length)} / {M(Geo.GeodesicDistance(la, lo, la2, lo2))} m)");
        // kilometraż i najbliższy punkt
        var (pEnd, _) = r2.At(r2.Length); var (pMid, _) = r2.At(500);
        Ok(XY.Dist(pEnd, a2) < 1e-6 && Math.Abs(r2.Nearest(pMid).S - 500) < 0.01, "kilometraż: koniec toru w ostatnim wierzchołku, punkt na 500 m odnaleziony");
        // błędy
        var r4 = Trasa.Compute(new[] { V(a0), V(a1, 3000), V(a2) }, k);
        Ok(!r4.Ok && r4.Notes.Any(n => n.Text.Contains("nie mieści")), "za duży promień – błąd „łuk nie mieści się”");
        var r5 = Trasa.Compute(new[] { V(a0), V(Go(a1, 0, 0), 300, 400), V(a2) }, k);
        Ok(!r5.Ok && r5.Notes.Any(n => n.Text.Contains("za długie")), "za długa przejściowa – błąd");
        var r6 = Trasa.Compute(new[] { V(a0), V(a1, 0), V(a2) }, k);
        Ok(r6.Ok && r6.Notes.Any(n => n.Text.Contains("załamanie")), "wierzchołek bez łuku – ostrzeżenie o załamaniu");
        // tor równoległy
        var t1 = new UtTrack { Vertices = new() { V(a0), V(a1, 300), V(a2) } };
        var off = new UtTrack { Vertices = Trasa.Offset(t1, 4.75) };
        var rOff = Trasa.Compute(off); var rT1 = Trasa.Compute(t1);
        double dmax = 0, dmin = 1e9;
        for (double s = 5; s < rT1.Length - 5; s += 2.5) { var d = rOff.Nearest(rT1.At(s).P, 0.05).Dist / k; dmax = Math.Max(dmax, d); dmin = Math.Min(dmin, d); }
        Ok(Math.Abs(dmax - 4.75) < 0.001 && Math.Abs(dmin - 4.75) < 0.001 && Math.Abs(off.Vertices[1].R - (300 - 4.75)) < 1e-9, $"tor równoległy 4,75 m po wewnętrznej stronie łuku: odległość {dmin:0.0000}–{dmax:0.0000} m, R = 295,25 m");

        // ---------------- budowa toru klikami na ortofotomapie
        var dir = Path.Combine(Path.GetTempPath(), "edytor_ut_test", "etap3"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        w.NewDoc(); w.SaveDoc(false, Path.Combine(dir, "tory_test.ut.json"));
        var d0 = w.Doc;
        w.LiveBox.SelectedIndex = 0;
        w.GoTo("51.7631, 19.5438"); w.View.SetView(w.View.Center, 0.5);
        Find<ToggleButton>(w, b => (b.Content as string)?.Contains("Tor") == true).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Ok(w.View.Tool == UtTool.Tor, "przycisk Tor – narzędzie Tor");
        w.NewRBox.Text = "500"; w.NewLBox.Text = "40";
        Ok(w.View.NewR == 500 && w.View.NewL == 40, "R 500, L 40 dla nowych wierzchołków (pola na pasku)");
        var c = w.View.Center;
        Point S(XY p) => w.View.ToScreen(p);
        var q0 = Go(c, 280, 260); var q1 = Go(c, 100, 0); var q2 = Go(c, 108, 220);
        w.View.SimClick(S(q0)); w.View.SimClick(S(q1)); w.View.SimClick(S(q2));
        Ok(w.View.Draft.Count == 3, "trzy wierzchołki kliknięte");
        // długość z klawiatury: 150 m w stronę kursora
        var cursor = Go(q2, 125, 80);
        w.View.Drag(S(cursor), false);
        foreach (var key in new[] { Key.D1, Key.D5, Key.D0 }) KeyPress(w, key);
        Ok(w.View.LenBuf == "150", "wpisana długość 150");
        KeyPress(w, Key.Enter);
        var q3 = w.View.Draft[^1];
        Ok(w.View.Draft.Count == 4 && Math.Abs(Geo.GroundDistance(q2, q3) - 150) < 0.001 && Math.Abs(Geo.Azimuth(q2, q3) - Geo.Azimuth(q2, cursor)) < 1e-6, $"Enter: wierzchołek w odległości {M(Geo.GroundDistance(q2, q3))} m w terenie, w stronę kursora");
        KeyPress(w, Key.D9); KeyPress(w, Key.Back); Ok(w.View.LenBuf == "" && w.View.Draft.Count == 4, "Backspace kasuje wpisywaną cyfrę");
        w.View.SimClick(S(Go(q3, 300, 1)), MouseButton.Right);
        var tr1 = d0.P.Tracks.SingleOrDefault();
        Ok(tr1 != null && tr1.Vertices.Count == 4 && tr1.Vertices[1].R == 500 && tr1.Vertices[2].L == 40 && w.View.Draft.Count == 0, "PPM – koniec: tor z 4 wierzchołkami, R 500 / L 40 w wierzchołkach wewnętrznych");
        var g1 = w.View.Geometry(tr1);
        Ok(g1.Ok && g1.Elements.Count(e => e.Kind == ElKind.Luk) == 2 && Continuity(g1).Pos < 1e-6, $"geometria toru bez błędów: 2 łuki z przejściowymi, długość {M(g1.Length)} m");
        Ok(w.View.SelectedTrack == tr1.Id && Find<TextBlock>(w.TrackPanel, tb => tb.Text == "Tor (bez numeru)") != null, "nowy tor zaznaczony, panel toru");
        Ok(UndoDepth(w) == 1 && d0.UndoName == "Nowy tor", "jedno Ctrl+Z na cały tor");

        // panel: numer, kategoria, promień w wierzchołku
        var boxes = new List<TextBox>(); void Collect(DependencyObject x) { if (x is TextBox tb) boxes.Add(tb); foreach (var ch in LogicalTreeHelper.GetChildren(x).OfType<DependencyObject>()) Collect(ch); }
        Collect(w.TrackPanel);
        Type(boxes[0], "1");
        Ok(d0.P.Tracks[0].Number == "1" && Find<TextBlock>(w.TrackPanel, tb => tb.Text == "Tor 1") != null, "numer toru 1 z panelu");
        Find<ComboBox>(w.TrackPanel, _ => true).SelectedIndex = 2;
        Ok(d0.P.Tracks[0].Cat == UtTrack.MainZ, "kategoria: główny zasadniczy");
        boxes.Clear(); Collect(w.TrackPanel);
        var rBox = boxes.First(b => (b.ToolTip as string) == "W2: promień łuku [m]");
        rBox.Text = "800"; Enter(rBox);
        Ok(d0.P.Tracks[0].Vertices[1].R == 800, "promień w W2 zmieniony na 800 m w panelu");
        var g2 = w.View.Geometry(d0.P.Tracks[0]);
        Ok(g2.Elements.Any(e => e.Kind == ElKind.Luk && Math.Abs(Math.Abs(e.R) / g2.K - 800) < 1e-6), "łuk przeliczony z R 800");

        // przeciąganie wierzchołka (Wskaż)
        KeyPress(w, Key.Escape); Ok(w.View.Tool == UtTool.Wskaz, "Esc – Wskaż");
        var t = d0.P.Tracks[0]; var vOld = t.Vertices[2].P;
        w.View.SimDrag(S(vOld), S(vOld) + new Vector(30, 12));
        var vNew = d0.P.Tracks[0].Vertices[2].P;
        Ok(Math.Abs(vNew.Y - vOld.Y - 30 * w.View.Mpp) < 1e-6 && Math.Abs(vNew.X - vOld.X + 12 * w.View.Mpp) < 1e-6 && d0.UndoName == "Przesunięcie wierzchołka", "przeciągnięcie wierzchołka W3 o 30 × 12 px");
        w.DoUndo(); Ok(XY.Dist(d0.P.Tracks[0].Vertices[2].P, vOld) < 1e-9, "Ctrl+Z – wierzchołek wrócił");
        // dwuklik na boku wieloboku – nowy wierzchołek, Del – usunięcie
        t = d0.P.Tracks[0];
        w.View.Select(t.Id);
        var mid = (t.Vertices[0].P + t.Vertices[1].P) * 0.5;
        w.View.SimDoubleClick(S(mid));
        Ok(d0.P.Tracks[0].Vertices.Count == 5 && XY.Dist(d0.P.Tracks[0].Vertices[1].P, mid) < 0.01 && w.View.SelectedVertex == 1, "dwuklik na boku wieloboku – nowy wierzchołek W2");
        KeyPress(w, Key.Delete);
        Ok(d0.P.Tracks[0].Vertices.Count == 4, "Del – zaznaczony wierzchołek usunięty");
        // przedłużenie toru od końca
        w.SetTool(UtTool.Tor);
        t = d0.P.Tracks[0]; var end = t.Vertices[^1].P;
        w.View.SimClick(S(end));
        var ext = Go(end, 115, 120);
        w.View.SimClick(S(ext));
        w.View.SimClick(S(ext), MouseButton.Right);
        Ok(d0.P.Tracks.Count == 1 && d0.P.Tracks[0].Vertices.Count == 5 && XY.Dist(d0.P.Tracks[0].Vertices[^1].P, ext) < 0.01 && d0.P.Tracks[0].Vertices[3].R == 500, "start na końcu toru – tor przedłużony, dawny koniec dostał łuk");
        // tor równoległy z panelu
        w.SetTool(UtTool.Wskaz); w.View.Select(d0.P.Tracks[0].Id);
        Click(BtnIn(w.TrackPanel, "po prawej →"));
        Ok(d0.P.Tracks.Count == 2, "tor równoległy po prawej (4,75 m)");
        var rp = w.View.Geometry(d0.P.Tracks[1]); var rm = w.View.Geometry(d0.P.Tracks[0]);
        double pmin = 1e9, pmax = 0;
        for (double s = 20; s < rm.Length - 20; s += 5) { var dd = rp.Nearest(rm.At(s).P, 0.05).Dist / rm.K; pmin = Math.Min(pmin, dd); pmax = Math.Max(pmax, dd); }
        Ok(pmin > 4.73 && pmax < 4.77, $"tor równoległy: międzytorze {pmin:0.000}–{pmax:0.000} m (na przejściowych do ±2 cm)");
        // nowy tor zaczęty na osi toru (przyciąganie do toru)
        w.SetTool(UtTool.Tor);
        var on = rm.At(rm.Length * 0.3).P;
        w.View.SimClick(S(on) + new Vector(3, 2));
        var st = w.View.Draft[0];
        Ok(rm.Nearest(st, 0.05).Dist < 0.01, "przyciąganie: początek nowego toru na osi toru");
        w.View.SimClick(S(Go(st, 20, 100)));
        KeyPress(w, Key.Escape);
        Ok(w.View.Draft.Count == 0 && d0.P.Tracks.Count == 2, "Esc – rysowanie porzucone, tor nie powstał");
        // usunięcie toru i przywrócenie
        w.SetTool(UtTool.Wskaz);
        w.View.SimClick(S(rp.At(rp.Length / 2).P));
        Ok(w.View.SelectedTrack == d0.P.Tracks[1].Id, "klik na torze – zaznaczony");
        KeyPress(w, Key.Delete); Ok(d0.P.Tracks.Count == 1, "Del – tor usunięty");
        w.DoUndo(); Ok(d0.P.Tracks.Count == 2, "Ctrl+Z – tor wrócił");
        // zapis i odczyt
        Ok(w.SaveDoc(false), "zapis projektu z torami");
        var re = UtDocument.Load(d0.Path);
        Ok(UtDocument.Serialize(re.P) == UtDocument.Serialize(d0.P) && re.P.Tracks.Count == 2 && Math.Abs(Trasa.Compute(re.P.Tracks[0]).Length - rm.Length) < 1e-9, "odczyt: tory i ich długości identyczne");

        // widok do zrzutów
        w.View.Select(d0.P.Tracks[0].Id);
        w.View.FitWorld(d0.P.Tracks.SelectMany(x => x.Vertices).Select(v => v.P).Aggregate((p, q) => new XY(Math.Min(p.X, q.X), Math.Min(p.Y, q.Y))), d0.P.Tracks.SelectMany(x => x.Vertices).Select(v => v.P).Aggregate((p, q) => new XY(Math.Max(p.X, q.X), Math.Max(p.Y, q.Y))));
        if (shotRails != null)
        {
            var keep = (w.View.Center, w.View.Mpp);
            w.View.SetView(rm.At(rm.Length * 0.45).P, 0.04);
            await Settle(w); Save(w, shotRails);
            w.View.SetView(keep.Center, keep.Mpp);
        }
    }
}

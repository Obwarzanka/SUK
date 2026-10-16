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
        Ok(w.View.NewR == 500 && w.View.NewL == 40, "R 500, L 40 dla nowych łuków (pola na pasku)");
        Ok(w.View.Mode == UtView.TorMode.Prosta && w.ProstaBtn.IsChecked == true, "tryb Prosta na start");
        var c = w.View.Center;
        Point S(XY p) => w.View.ToScreen(p);
        var q0 = Go(c, 280, 260); var q1 = Go(c, 100, 0);
        w.View.SimClick(S(q0)); w.View.SimClick(S(q1));
        Ok(w.View.Draft.Count == 2, "pierwsza prosta: dwa punkty, kierunek za kursorem");
        // łuk z klawiatury: 120 m w prawo (kursor po prawej stronie toru)
        Click(w.LukBtn);
        Ok(w.View.Mode == UtView.TorMode.Luk && w.LukBtn.IsChecked == true && w.ProstaBtn.IsChecked == false, "przycisk Łuk – tryb łuku");
        var cursor = Go(Go(q1, 100, 60), 190, 30);
        w.View.Drag(S(cursor), false);
        foreach (var key in new[] { Key.D1, Key.D2, Key.D0 }) KeyPress(w, key);
        Ok(w.View.LenBuf == "120", "wpisana długość łuku 120");
        KeyPress(w, Key.Enter);
        // prosta 150 m na przedłużeniu łuku (Tab – przełączenie)
        KeyPress(w, Key.Tab);
        Ok(w.View.Mode == UtView.TorMode.Prosta, "Tab – z powrotem prosta");
        w.View.Drag(S(Go(c, 150, 400)), false);
        foreach (var key in new[] { Key.D1, Key.D5, Key.D0 }) KeyPress(w, key);
        KeyPress(w, Key.Enter);
        KeyPress(w, Key.D9); KeyPress(w, Key.Back); Ok(w.View.LenBuf == "", "Backspace kasuje wpisywaną cyfrę");
        w.View.SimClick(S(q0), MouseButton.Right);
        var tr1 = d0.P.Tracks.SingleOrDefault();
        Ok(tr1 != null && tr1.Vertices.Count == 3 && tr1.Vertices[1].R == 500 && tr1.Vertices[1].L == 40 && w.View.Draft.Count == 0,
            $"PPM – koniec: tor prosta – łuk – prosta = 3 wierzchołki (punkty na prostych usunięte), R 500 / L 40 ({tr1?.Vertices.Count} wierzch.)");
        var g1 = w.View.Geometry(tr1);
        double turn = Math.IEEERemainder(g1.Elements[^1].Az0 - g1.Elements[0].Az0, 2 * Math.PI);
        Ok(g1.Ok && g1.Elements.Select(e => e.Kind).SequenceEqual(new[] { ElKind.Prosta, ElKind.Przejsciowa, ElKind.Luk, ElKind.Przejsciowa, ElKind.Prosta }) && Continuity(g1).Pos < 1e-6 && Continuity(g1).Az < 1e-9,
            "geometria: prosta, przejściowa, łuk, przejściowa, prosta – styki ciągłe");
        Ok(Math.Abs(g1.Elements[0].Len / g1.K - 260) < 1e-3 && Math.Abs(g1.Elements[^1].Len / g1.K - 150) < 1e-3 && Math.Abs(turn - 120.0 / 500) < 1e-9,
            $"prosta 260 m do początku łuku, łuk w prawo o 120/500 rad ({turn * 180 / Math.PI:0.###}°), prosta 150 m za łukiem");
        Ok(w.View.SelectedTrack == tr1.Id && Find<TextBlock>(w.TrackPanel, tb => tb.Text == "Tor (bez numeru)") != null, "nowy tor zaznaczony, panel toru");
        Ok(UndoDepth(w) == 1 && d0.UndoName == "Nowy tor", "jedno Ctrl+Z na cały tor");
        // łuk myszą: kąt za kursorem, Backspace cofa odcinek
        w.SetTool(UtTool.Tor); w.View.Mode = UtView.TorMode.Prosta;
        var f0 = Go(c, 0, 300); w.View.SimClick(S(f0)); w.View.SimClick(S(Go(f0, 90, 100)));
        w.View.Mode = UtView.TorMode.Luk;
        var aim = Go(Go(f0, 90, 100), 45, 80);
        w.View.SimClick(S(aim));
        Ok(w.View.Draft.Count == 3, "łuk myszą: wierzchołek i koniec łuku (punkt na prostej przed łukiem usunięty)");
        KeyPress(w, Key.Back);
        Ok(w.View.Draft.Count == 2, "Backspace – łuk cofnięty");
        KeyPress(w, Key.Escape); Ok(w.View.Draft.Count == 0 && d0.P.Tracks.Count == 1, "Esc – rysowanie porzucone");
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
        int nv0 = t.Vertices.Count;
        w.View.SimDoubleClick(S(mid));
        Ok(d0.P.Tracks[0].Vertices.Count == nv0 + 1 && XY.Dist(d0.P.Tracks[0].Vertices[1].P, mid) < 0.01 && w.View.SelectedVertex == 1, "dwuklik na boku wieloboku – nowy wierzchołek W2");
        KeyPress(w, Key.Delete);
        Ok(d0.P.Tracks[0].Vertices.Count == nv0, "Del – zaznaczony wierzchołek usunięty");
        // przedłużenie toru od końca
        w.SetTool(UtTool.Tor);
        t = d0.P.Tracks[0]; var end = t.Vertices[^1].P; int nvx = t.Vertices.Count; double lenx = w.View.Geometry(t).Length;
        w.View.Mode = UtView.TorMode.Prosta;
        w.View.SimClick(S(end));
        w.View.Drag(S(Go(end, 150, 50)), false);
        KeyPress(w, Key.D8); KeyPress(w, Key.D0); KeyPress(w, Key.Enter);
        w.View.SimClick(S(end), MouseButton.Right);
        var tx = d0.P.Tracks[0]; var gx = w.View.Geometry(tx);
        Ok(d0.P.Tracks.Count == 1 && tx.Vertices.Count == nvx && Math.Abs(gx.Length - lenx - 80) < 1e-3 && gx.Ok && d0.UndoName == "Przedłużenie toru",
            $"start na końcu toru – przedłużenie stycznie o 80 m (koniec przesunięty, bez nowego wierzchołka; {UtView.Len3(lenx)} → {UtView.Len3(gx.Length)}, wierzch. {nvx} → {tx.Vertices.Count}, {d0.UndoName})");
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

        // ---------------- łączenie torów
        w.SetTool(UtTool.Tor); w.View.Mode = UtView.TorMode.Prosta; w.NewRBox.Text = "300"; w.NewLBox.Text = "0";
        UtTrack Line2(XY a, double az, double len)
        {
            w.View.SimClick(S(a)); w.View.SimClick(S(Go(a, az, len))); w.View.SimClick(S(a), MouseButton.Right);
            return d0.P.Tracks.Last();
        }
        var pa = Go(c, 200, 700); w.View.SetView(Go(pa, 100, 300), 1);
        var tA = Line2(pa, 100, 200);
        var pb = Go(Go(Go(pa, 100, 200), 100, 140), 190, 6);
        var tB = Line2(pb, 100, 200);
        int ntr0 = d0.P.Tracks.Count;
        w.View.SimClick(S(tA.Vertices[^1].P));
        if (shotRails != null) { w.View.Drag(S(tB.Vertices[0].P) + new Vector(2, 1), false); await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_laczenie.png")); }
        w.View.SimClick(S(tB.Vertices[0].P));
        var tAB = d0.P.Tracks.FirstOrDefault(x => x.Id == tA.Id); var gAB = tAB != null ? w.View.Geometry(tAB) : null;
        Ok(tAB != null && d0.P.Tracks.Count == ntr0 - 1 && d0.P.Tracks.All(x => x.Id != tB.Id) && d0.UndoName == "Połączenie torów", "koniec toru A → początek toru B: jeden tor (B wchłonięty)");
        Ok(gAB != null && gAB.Ok && Continuity(gAB).Pos < 1e-6 && Continuity(gAB).Az < 1e-9 && gAB.Elements.Count(e => e.Kind == ElKind.Luk) == 2 && Math.Abs(gAB.Length - (200 + 200 + 140)) < 1,
            $"esowanie łukami odwrotnymi (zmiana rozstawu 6 m na 140 m), styki ciągłe, długość {UtView.Len3(gAB?.Length ?? 0)}");
        // jeden łuk: tor C na wschód, tor D zaczyna się 90° dalej w prawo; R 300 się nie mieści – zmniejszony
        var pc = Go(c, 200, 900); w.View.SetView(Go(pc, 100, 300), 1);
        var tC = Line2(pc, 100, 200);
        var xC = Go(Go(pc, 100, 200), 100, 150); var tD = Line2(Go(xC, 190, 150), 190, 200);
        w.View.SimClick(S(tC.Vertices[^1].P)); w.View.SimClick(S(tD.Vertices[0].P));
        var tCD = d0.P.Tracks.First(x => x.Id == tC.Id); var gCD = w.View.Geometry(tCD);
        var arcCD = gCD.Elements.Where(e => e.Kind == ElKind.Luk).ToList();
        Ok(d0.P.Tracks.All(x => x.Id != tD.Id) && gCD.Ok && arcCD.Count == 1 && Math.Abs(Math.Abs(arcCD[0].R) / gCD.K - 150) < 0.2 && Continuity(gCD).Az < 1e-9 && w.LastMessage.Length > 0,
            $"połączenie jednym łukiem 90°: R zmniejszony do największego, jaki się mieści ({Math.Abs(arcCD.FirstOrDefault()?.R ?? 0) / gCD.K:0.0} m)");
        // uchwyt łuku: przeciągnięcie w stronę wierzchołka zmniejsza R
        w.SetTool(UtTool.Wskaz); w.View.Select(tCD.Id);
        int ia = tCD.Vertices.FindIndex(v => v.R > 0); double r0 = tCD.Vertices[ia].R;
        var mA = Budowa.ArcMid(gCD, ia).Value; var vA = tCD.Vertices[ia].P;
        w.View.SimDrag(S(mA), S(mA) + (S(vA) - S(mA)) * 0.4);
        var tCD2 = d0.P.Tracks.First(x => x.Id == tC.Id); var gCD2 = w.View.Geometry(tCD2);
        var m2 = Budowa.ArcMid(gCD2, ia).Value;
        Ok(tCD2.Vertices[ia].R < r0 - 10 && gCD2.Ok && d0.UndoName == "Promień łuku" && Math.Abs(XY.Dist(m2, vA) - XY.Dist(mA, vA) * 0.6) < 0.6 * k,
            $"uchwyt łuku przeciągnięty do wierzchołka: R {r0:0} → {tCD2.Vertices[ia].R:0} m, środek łuku pod kursorem");
        w.DoUndo(); Ok(d0.P.Tracks.First(x => x.Id == tC.Id).Vertices[ia].R == r0, "Ctrl+Z – promień wrócił");
        if (shotRails != null) { w.View.FitPoints(gCD.Sample(2).Select(q => q.P).ToList()); await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_uchwyt_luku.png")); }
        // Alt – wolny kierunek (załamanie z łukiem R w wierzchołku)
        w.SetTool(UtTool.Tor); w.View.Mode = UtView.TorMode.Prosta;
        var pe = Go(c, 200, 1100); w.View.SetView(Go(pe, 100, 200), 1);
        w.View.SimClick(S(pe)); w.View.SimClick(S(Go(pe, 100, 150)));
        UtView.TestMods = ModifierKeys.Alt; w.View.SimClick(S(Go(Go(pe, 100, 150), 130, 150))); UtView.TestMods = ModifierKeys.None;
        w.View.SimClick(S(pe), MouseButton.Right);
        var tE = d0.P.Tracks.Last();
        Ok(tE.Vertices.Count == 3 && tE.Vertices[1].R == 300 && w.View.Geometry(tE).Ok, "Alt – wolny kierunek: wierzchołek z łukiem R 300");
        w.SetTool(UtTool.Wskaz);
        foreach (var id in new[] { tA.Id, tC.Id, tE.Id }) d0.Change("test", pp => pp.Tracks.RemoveAll(x => x.Id == id));

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

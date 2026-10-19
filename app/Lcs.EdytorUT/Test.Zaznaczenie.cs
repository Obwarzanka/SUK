using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Test usuwania kawałka toru i zaznaczania kilku elementów, jak użytkownik: tor prosta – łuk R 500 – prosta klikami, liczniki osi;
/// menu PPM toru: „Usuń ten kawałek” (łuk) i „Rozetnij tor tutaj” (na prostej i w łuku); Ctrl + klik dwóch liczników + Del;
/// Shift + prostokąt + Del; Esc; Shift + klik dwóch torów i Ctrl+J (styk, wstawka z łukami odwrotnymi); szybkość przy 400 torach.
/// </summary>
public static partial class Test
{
    static async Task RunZaznaczenie(MainWindow w)
    {
        const double lineAz = 70;
        var dir = Path.Combine(Path.GetTempPath(), "edytor_ut_test", "zaznaczenie"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        w.SetSchemat(false);
        w.NewDoc(); w.SaveDoc(false, Path.Combine(dir, "stacja.ut.json"));
        var d = w.Doc;
        var o = Geo.ToPuwg92(51.7631, 19.5438); double k = Geo.ScaleFactor(o);
        XY Go(XY from, double azDeg, double len) => from + Element.Dir(azDeg * Math.PI / 180) * (len * k);
        Point S(XY p) => w.View.ToScreen(p);
        void Look(XY p, double mpp) => w.View.SetView(p, mpp);

        // tor: prosta 300 m – łuk R 500 w lewo o 30° – prosta 300 m
        w.SetTool(UtTool.Tor); w.View.Mode = UtView.TorMode.Prosta; w.NewRBox.Text = "500"; w.NewLBox.Text = "0";
        var a = Go(o, lineAz + 180, 300); double az0 = lineAz * Math.PI / 180, R = 500 * k;
        var cen = o - Element.Right(az0) * R; var e30 = cen + Element.Right(az0 - 30 * Math.PI / 180) * R; var c = Go(e30, lineAz - 30, 300);
        Look(o, 1.5);
        w.View.SimClick(S(a)); w.View.SimClick(S(o));
        w.View.Mode = UtView.TorMode.Luk; w.View.SimClick(S(e30));
        w.View.Mode = UtView.TorMode.Prosta; w.View.SimClick(S(c)); w.View.SimClick(S(c), MouseButton.Right);
        var t = d.P.Tracks.Single(); t.Number = "7"; var r = w.View.Geometry(t);
        var parts = Ciecie.Kawalki(r);
        Ok(parts.Count == 3 && parts[1].Luk && Math.Abs(parts[1].Len - 500 * Math.PI / 6) < 0.5, $"tor 7: prosta – łuk R 500 ({parts.ElementAtOrDefault(1)?.Len:0.0} m) – prosta, 3 kawałki");
        // liczniki osi: km 100, na łuku, za łukiem (+50 m)
        double sArc = (parts[1].S0 + parts[1].S1) / 2, sAfter = parts[1].S1 + 50;
        var lo = new[] { 100.0, sArc, sAfter }.Select((s, i) => new UtDevice { Kind = UtDevice.LicznikOsi, Name = $"lo{i + 1}", Track = t.Id, S = s }).ToList();
        d.Change("Liczniki", p => p.Devices.AddRange(lo));

        // --- PPM na łuku: „Usuń ten kawałek – łuk W2”
        w.SetTool(UtTool.Wskaz);
        var cm = w.BuildTrackMenu(t.Id, sArc);
        var mi = cm.Items.OfType<MenuItem>().FirstOrDefault(x => (x.Header as string)?.StartsWith("Usuń ten kawałek – łuk W2") == true);
        Ok(mi != null, $"menu PPM toru na łuku: „{mi?.Header}”");
        mi?.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        var tA = d.P.Tracks.FirstOrDefault(x => x.Id == t.Id); var tB = d.P.Tracks.FirstOrDefault(x => x.Id != t.Id);
        var rA = tA != null ? w.View.Geometry(tA) : null; var rB = tB != null ? w.View.Geometry(tB) : null;
        Ok(d.P.Tracks.Count == 2 && rA.Ok && rB.Ok && Math.Abs(rA.Length - parts[1].S0) < 0.01 && Math.Abs(rB.Length - (r.Length - parts[1].S1)) < 0.01
            && XY.Dist(rA.At(rA.Length).P, r.At(parts[1].S0).P) < 1e-6 && XY.Dist(rB.At(0).P, r.At(parts[1].S1).P) < 1e-6,
            $"łuk usunięty: tor 7 do początku łuku ({rA?.Length:0.00} m), nowy tor od końca łuku ({rB?.Length:0.00} m), położenie bez zmian");
        Ok(d.P.Devices.Count == 2 && d.P.Devices.Any(x => x.Name == "lo1" && x.Track == t.Id && x.S == 100) && d.P.Devices.Any(x => x.Name == "lo3" && x.Track == tB?.Id && Math.Abs(x.S - 50) < 1e-6)
            && w.LastMessage.Contains("usunięte z nim obiekty: 1"),
            $"licznik na łuku usunięty z kawałkiem, lo3 przeszedł na nowy tor (km 50) – {w.LastMessage}");
        w.DoUndo();
        Ok(d.P.Tracks.Count == 1 && d.P.Devices.Count == 3, "Ctrl+Z – tor i liczniki wróciły");

        // --- „Rozetnij tor tutaj” na prostej km 150; w łuku – niedostępne
        MenuItem CutItem(ContextMenu m) => m.Items.OfType<MenuItem>().First(x => (x.Header as string)?.StartsWith("Rozetnij tor tutaj") == true);
        cm = w.BuildTrackMenu(t.Id, 150);
        CutItem(cm).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        tB = d.P.Tracks.FirstOrDefault(x => x.Id != t.Id); rA = w.View.Geometry(d.P.Tracks.First(x => x.Id == t.Id)); rB = tB != null ? w.View.Geometry(tB) : null;
        Ok(d.P.Tracks.Count == 2 && Math.Abs(rA.Length - 150) < 0.01 && Math.Abs(rA.Length + rB.Length - r.Length) < 0.01 && XY.Dist(rA.At(rA.Length).P, rB.At(0).P) < 1e-6
            && d.P.Devices.Count(x => x.Track == tB.Id) == 2,
            $"rozcięty w km 150: {rA.Length:0.00} + {rB?.Length:0.00} m, końce się stykają ({XY.Dist(rA.At(rA.Length).P, rB.At(0).P):0.######}), liczniki lo2 i lo3 na nowym torze ({d.P.Devices.Count(x => x.Track == tB.Id)}; tory {d.P.Tracks.Count})");
        w.DoUndo();

        // --- Ctrl + klik dwóch liczników, Del
        XY DevAt(string name) { var dv = d.P.Devices.First(x => x.Name == name); return w.View.Geometry(d.P.Tracks.First(x => x.Id == dv.Track)).At(dv.S).P; }
        Look(DevAt("lo1"), 0.2); w.View.SimClick(S(DevAt("lo1")));
        Ok(w.View.SelectedDevice == d.P.Devices.First(x => x.Name == "lo1").Id, "klik – zaznaczony lo1");
        UtView.TestMods = ModifierKeys.Control;
        Look(DevAt("lo3"), 0.2); w.View.SimClick(S(DevAt("lo3")));
        UtView.TestMods = ModifierKeys.None;
        Ok(w.View.Multi.Count == 2 && w.TrackPanel.Children.OfType<TextBlock>().Any(x => x.Text == "Zaznaczone: 2"), $"Ctrl + klik lo3 – zaznaczone 2 urządzenia, panel „Zaznaczone: 2”");
        KeyPress(w, Key.Delete);
        Ok(d.P.Devices.Count == 1 && d.P.Devices[0].Name == "lo2" && w.View.Multi.Count == 0 && d.UndoName.StartsWith("Usunięcie zaznaczonych"), $"Del – usunięte lo1 i lo3 ({w.LastMessage})");
        w.DoUndo(); Ok(d.P.Devices.Count == 3, "jedno Ctrl+Z – oba wróciły");

        // --- Shift + prostokąt przez cały tor: tor i 3 liczniki; Esc; potem znowu i Del
        w.View.FitPoints(new[] { a, o, e30, c });
        var all = new[] { S(a), S(o), S(e30), S(c) };
        var p0 = new Point(all.Min(q => q.X) - 30, all.Min(q => q.Y) - 30); var p1 = new Point(all.Max(q => q.X) + 30, all.Max(q => q.Y) + 30);
        UtView.TestMods = ModifierKeys.Shift; w.View.SimDrag(p0, p1); UtView.TestMods = ModifierKeys.None;
        Ok(w.View.Multi.Count == 4 && w.View.InMulti(Kontrola.KTor, t.Id) && w.View.Multi.Count(x => x.Kind == Kontrola.KUrzadzenie) == 3, $"Shift + prostokąt: tor 7 i 3 liczniki ({w.View.Multi.Count})");
        if (shotRails != null) { await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_zaznaczenie.png")); }
        KeyPress(w, Key.Escape);
        Ok(w.View.Multi.Count == 0, "Esc – zaznaczenie zdjęte");
        UtView.TestMods = ModifierKeys.Shift; w.View.SimDrag(p0, p1); UtView.TestMods = ModifierKeys.None;
        KeyPress(w, Key.Delete);
        Ok(d.P.Tracks.Count == 0 && d.P.Devices.Count == 0, "prostokąt + Del – tor i liczniki usunięte");
        w.DoUndo(); Ok(d.P.Tracks.Count == 1 && d.P.Devices.Count == 3, "Ctrl+Z – wszystko wróciło");
        // zwykły klik kończy zaznaczenie kilku
        UtView.TestMods = ModifierKeys.Shift; w.View.SimDrag(p0, p1); UtView.TestMods = ModifierKeys.None;
        w.View.SimClick(new Point(5, 5));
        Ok(w.View.Multi.Count == 0, "zwykły klik obok – koniec zaznaczenia kilku");

        // --- „Rozetnij tor tutaj” w środku łuku: łuk dzieli się na dwa, kształt bez zmian; licznik lo2 (w miejscu cięcia) na nowym torze
        cm = w.BuildTrackMenu(t.Id, sArc);
        Ok(CutItem(cm).IsEnabled, $"w łuku „{CutItem(cm).Header}” dostępne");
        CutItem(cm).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        tA = d.P.Tracks.First(x => x.Id == t.Id); tB = d.P.Tracks.FirstOrDefault(x => x.Id != t.Id);
        rA = w.View.Geometry(tA); rB = tB != null ? w.View.Geometry(tB) : null;
        double shape = rB == null ? 99 : new[] { 0.0, 10, 50, 100, rB.Length }.Max(x => XY.Dist(rB.At(x).P, r.At(sArc + x).P));
        Ok(d.P.Tracks.Count == 2 && Math.Abs(rA.Length - sArc) < 0.01 && Math.Abs(rA.Length + rB.Length - r.Length) < 0.01 && shape < 1e-3 && rA.Notes.Count == 0 && rB.Notes.Count == 0,
            $"rozcięty w łuku km {sArc:0.00}: {rA.Length:0.00} + {rB?.Length:0.00} m, kształt bez zmian (odchyłka {shape * 1000:0.###} mm) – {w.LastMessage}");
        Ok(d.P.Devices.Count(x => x.Track == tB?.Id) == 2 && d.P.Devices.Any(x => x.Name == "lo3" && Math.Abs(x.S - (sAfter - sArc)) < 0.01), "lo2 i lo3 na nowym torze, km od jego początku");

        // --- Shift + klik obu części, Ctrl+J – z powrotem jeden tor, urządzenia na swoich miejscach
        w.View.FitPoints(new[] { a, o, e30, c });
        UtView.TestMods = ModifierKeys.Shift;
        w.View.SimClick(S(rA.At(50).P)); w.View.SimClick(S(rB.At(rB.Length - 50).P));
        UtView.TestMods = ModifierKeys.None;
        Ok(w.View.Multi.Count == 2 && w.View.InMulti(Kontrola.KTor, tA.Id) && w.View.InMulti(Kontrola.KTor, tB.Id)
            && Find<Button>(w.TrackPanel, x => (x.Content as string)?.StartsWith("Połącz tory (2)") == true) != null,
            $"Shift + klik dwóch torów – zaznaczone {w.View.Multi.Count}, w panelu „Połącz tory (2)”");
        var cmJ = w.BuildTrackMenu(tA.Id, 50);
        Ok(cmJ.Items.OfType<MenuItem>().Any(x => (x.Header as string)?.StartsWith("Połącz zaznaczone tory (2)") == true), "PPM na zaznaczonym torze: „Połącz zaznaczone tory (2)”");
        Ok(w.JoinSelected(), $"Ctrl+J: {w.LastMessage}");
        var tj = d.P.Tracks.SingleOrDefault(); var rj = tj != null ? w.View.Geometry(tj) : null;
        Ok(tj?.Id == t.Id && tj.Number == "7" && Math.Abs(rj.Length - r.Length) < 0.01 && rj.Notes.Count == 0 && new[] { 0.0, 200, sArc, 500, r.Length }.All(x => XY.Dist(rj.At(x).P, r.At(x).P) < 1e-3),
            $"jeden tor 7 jak przed rozcięciem ({rj?.Length:0.00} m, {tj?.Vertices.Count} wierzchołki)");
        Ok(d.P.Devices.All(x => x.Track == t.Id) && d.P.Devices.Any(x => x.Name == "lo3" && Math.Abs(x.S - sAfter) < 0.01) && d.P.Devices.Any(x => x.Name == "lo2" && Math.Abs(x.S - sArc) < 0.01),
            "liczniki wróciły na tor 7 z kilometrażem jak przed rozcięciem");
        w.DoUndo(); Ok(d.P.Tracks.Count == 2, "Ctrl+Z – znowu dwa tory");
        w.DoUndo(); Ok(d.P.Tracks.Count == 1, "Ctrl+Z – tor przed rozcięciem");

        // --- łączenie ze wstawką: tor P (200 m) i tor Q narysowany od dalszego końca, 60 m dalej, przesunięty o 4,5 m – łuki odwrotne, Q odwrócony
        var b0 = Go(o, lineAz - 90, 400); var b1 = Go(b0, lineAz, 200);
        var q1 = Go(Go(b1, lineAz, 60), lineAz + 90, 4.5); var q2 = Go(q1, lineAz, 200);
        w.SetTool(UtTool.Tor); w.View.Mode = UtView.TorMode.Prosta; w.NewRBox.Text = "300";
        Look(b0, 1.5); w.View.SimClick(S(b0)); Look(b1, 1.5); w.View.SimClick(S(b1)); w.View.SimClick(S(b1), MouseButton.Right);
        Look(q2, 1.5); w.View.SimClick(S(q2)); Look(q1, 1.5); w.View.SimClick(S(q1)); w.View.SimClick(S(q1), MouseButton.Right);
        var tP = d.P.Tracks[^2]; var tQ = d.P.Tracks[^1];
        Ok(d.P.Tracks.Count == 3 && XY.Dist(tQ.Vertices[0].P, q2) < 0.05, "narysowane tory P i Q (Q od dalszego końca)");
        w.SetTool(UtTool.Wskaz); w.View.FitPoints(new[] { b0, q2 });
        // klik P (zwykłe zaznaczenie), Shift + klik Q – oba
        w.View.SimClick(S(Go(b0, lineAz, 100)));
        UtView.TestMods = ModifierKeys.Shift; w.View.SimClick(S(Go(q1, lineAz, 100))); UtView.TestMods = ModifierKeys.None;
        Ok(w.View.Multi.Count == 2 && w.View.InMulti(Kontrola.KTor, tP.Id) && w.View.InMulti(Kontrola.KTor, tQ.Id), $"klik P, Shift + klik Q – zaznaczone {w.View.Multi.Count}");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        bool okJ = w.JoinSelected(); sw.Stop();
        var tPQ = d.P.Tracks.FirstOrDefault(x => x.Id == tP.Id); var rPQ = tPQ != null ? w.View.Geometry(tPQ) : null;
        double maxKink = 0;
        if (rPQ != null) for (int i = 1; i < rPQ.Elements.Count; i++) maxKink = Math.Max(maxKink, Math.Abs(Math.IEEERemainder(rPQ.Elements[i].Az0 - rPQ.Elements[i - 1].At(rPQ.Elements[i - 1].Len).Az, 2 * Math.PI)));
        Ok(okJ && d.P.Tracks.Count == 2 && rPQ != null && rPQ.Notes.Count == 0 && maxKink < 1e-6 && XY.Dist(rPQ.At(0).P, b0) < 0.05 && XY.Dist(rPQ.At(rPQ.Length).P, q2) < 0.05,
            $"P + Q: jeden tor od P do dalszego końca Q, styczny (załamanie {maxKink:0.0e0} rad), {rPQ?.Length:0.00} m, {sw.ElapsedMilliseconds} ms – {w.LastMessage}");
        if (shotRails != null) { w.View.Select(tP.Id); w.View.FitPoints(new[] { b1, q1 }); await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_laczenie.png")); }
        w.DoUndo(); Ok(d.P.Tracks.Count == 3, "Ctrl+Z – P i Q osobno");

        // --- szybkość: 400 torów, trafianie myszą i prostokąt (ramki z pamięci zamiast przeliczania osi)
        var many = Enumerable.Range(0, 400).Select(i =>
        {
            var s0 = Go(Go(o, lineAz + 90, 300 + (i % 20) * 6), lineAz, (i / 20) * 260); var s1 = Go(s0, lineAz, 250);
            return new UtTrack { Number = $"p{i}", Vertices = new() { new UtVertex { X = s0.X, Y = s0.Y }, new UtVertex { X = s1.X, Y = s1.Y } } };
        }).ToList();
        d.Change("Dużo torów", p => p.Tracks.AddRange(many));
        Look(Go(Go(o, lineAz + 90, 330), lineAz, 2000), 2);
        var hp = S(Go(Go(o, lineAz + 90, 300 + 5 * 6), lineAz, 7 * 260 + 100));
        w.View.HitTrack(hp);
        sw.Restart(); string hit = null; for (int i = 0; i < 2000; i++) hit = w.View.HitTrack(hp); sw.Stop();
        Ok(hit == many[7 * 20 + 5].Id && sw.ElapsedMilliseconds < 2000, $"2000 × trafienie toru wśród {d.P.Tracks.Count}: {sw.ElapsedMilliseconds} ms");
        w.View.FitPoints(many.SelectMany(x => x.Vertices.Select(v => v.P)).ToList());
        var mm = many.SelectMany(x => x.Vertices.Select(v => S(v.P))).ToList();
        sw.Restart();
        UtView.TestMods = ModifierKeys.Shift; w.View.SimDrag(new Point(mm.Min(q => q.X) - 10, mm.Min(q => q.Y) - 10), new Point(mm.Max(q => q.X) + 10, mm.Max(q => q.Y) + 10)); UtView.TestMods = ModifierKeys.None;
        sw.Stop();
        Ok(many.All(x => w.View.InMulti(Kontrola.KTor, x.Id)) && sw.ElapsedMilliseconds < 2000, $"prostokąt przez 400 torów: zaznaczone wszystkie ({w.View.Multi.Count} elementów z torami obok), {sw.ElapsedMilliseconds} ms");
        KeyPress(w, Key.Escape); w.DoUndo();
        await Task.CompletedTask;
    }
}

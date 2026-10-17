using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Test metod budowy łuku (PLAN_EDYTOR_UT.md § 5g), jak użytkownik: (B) w panelu łuku L2, długość łuku i styczna → R, blokada R;
/// (D) uchwyty na torze – prosta równolegle, początek łuku; (A) zamiana na trasę z elementów, wykres krzywizny – granica i poziom łuku
/// przeciągnięte myszą, podział elementu, R z tabeli (skok krzywizny), „Wstaw przejściową”, zamiana z powrotem; J na torze z elementów
/// (łuk koszowy R 600 → przejściowa → R 900 ze zdjęcia); (E) uwagi w „Sprawdź”; (F) punkty z OSM.
/// </summary>
public static partial class Test
{
    static async Task RunMetodyLuku(MainWindow w)
    {
        const double lineAz = 50;
        var dir = Path.Combine(Path.GetTempPath(), "edytor_ut_test", "metodyluku"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        w.SetSchemat(false);
        w.NewDoc(); w.SaveDoc(false, Path.Combine(dir, "stacja.ut.json"));
        var d = w.Doc;
        var o = Geo.ToPuwg92(51.7631, 19.5438); double k = Geo.ScaleFactor(o);
        XY Go(XY from, double azDeg, double len) => from + Element.Dir(azDeg * Math.PI / 180) * (len * k);
        Point S(XY p) => w.View.ToScreen(p);
        TextBox Tip(string start) => Find<TextBox>(w.TrackPanel, x => (x.ToolTip as string)?.StartsWith(start) == true);

        // tor: prosta 400 – łuk R 600, L 60, zwrot 30° – prosta 400
        var a = Go(o, lineAz + 180, 400); var c = Go(o, lineAz + 30, 400);
        var t = new UtTrack { Number = "8", Cat = UtTrack.MainZ, Vmax = 100, Vertices = new() { new() { X = a.X, Y = a.Y }, new() { X = o.X, Y = o.Y, R = 600, L = 60 }, new() { X = c.X, Y = c.Y } } };
        d.Change("Tor 8", p => p.Tracks.Add(t));
        w.SetTool(UtTool.Wskaz); w.View.Select(t.Id, 1);

        // --- (B) L2, długość łuku → R, styczna → R, blokada R
        Type(Tip("Krzywa przejściowa na wyjściu"), "30");
        t = d.P.Tracks.First(x => x.Id == t.Id); var r = w.View.Geometry(t);
        Ok(t.Vertices[1].L2 == 30 && r.Ok && Math.Abs(r.Tangent[1] - r.Tangent2[1]) > 1 && Continuity(r).Az < 1e-9,
            $"(B) L2 = 30 przy L = 60: styczne {r.Tangent[1] / k:0.00} / {r.Tangent2[1] / k:0.00} m, tor ciągły");
        Type(Tip("Wpisz długość części kołowej"), "200");
        t = d.P.Tracks.First(x => x.Id == t.Id); r = w.View.Geometry(t);
        double arc = r.Elements.Where(e => e.Kind == ElKind.Luk).Sum(e => e.Len) / k;
        Ok(Math.Abs(arc - 200) < 0.1 && w.LastMessage.Contains("→ R"), $"(B) długość łuku 200 m → R {t.Vertices[1].R} m (łuk {arc:0.00} m)");
        Type(Tip("Wpisz długość stycznej"), "180");
        t = d.P.Tracks.First(x => x.Id == t.Id); r = w.View.Geometry(t);
        Ok(Math.Abs(r.Tangent[1] / k - 180) < 0.1, $"(B) styczna 180 m → R {t.Vertices[1].R} m (T {r.Tangent[1] / k:0.00} m)");
        var lockBox = Find<CheckBox>(w.TrackPanel, x => (x.Content as string)?.StartsWith("zablokuj R") == true);
        lockBox.IsChecked = true; lockBox.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        t = d.P.Tracks.First(x => x.Id == t.Id); double rLocked = t.Vertices[1].R;
        var fitL = Budowa.FitArc(t.Vertices, 1, new[] { w.View.Geometry(t).At(450).P + Element.Right(lineAz * Math.PI / 180) * (2 * k) }, k, false);
        Ok(t.Vertices[1].LockR && fitL.R == rLocked, $"(B) zablokowany R {rLocked} – „Łuk z punktów” go nie zmienia");
        lockBox = Find<CheckBox>(w.TrackPanel, x => (x.Content as string)?.StartsWith("zablokuj R") == true);
        lockBox.IsChecked = false; lockBox.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

        // --- (D) uchwyt prostej: przesunięcie równoległe o 3 m w prawo; uchwyt początku łuku
        t = d.P.Tracks.First(x => x.Id == t.Id); r = w.View.Geometry(t);
        double az23 = Trasa.Az(t.Vertices[1].P, t.Vertices[2].P);
        var st = r.Elements.First(e => e.Kind == ElKind.Prosta); var mid = st.At(st.Len / 2).P;
        w.View.SetView(mid, 0.3); w.View.Select(t.Id);
        var to = mid + Element.Right(st.Az0) * (3 * k);
        w.View.SimDrag(S(mid), S(to));
        t = d.P.Tracks.First(x => x.Id == t.Id); r = w.View.Geometry(t);
        double off = Budowa.Dist(r, to) / k, dAz = Math.Abs(Math.IEEERemainder(Trasa.Az(t.Vertices[1].P, t.Vertices[2].P) - az23, 2 * Math.PI));
        Ok(r.Ok && off < 0.02 && dAz < 1e-9 && d.UndoName == "Przesunięcie prostej", $"(D) prosta W1–W2 przesunięta równolegle o 3 m (oś {off * 100:0.#} cm od celu), prosta W2–W3 – ten sam kierunek");
        var ts = r.Elements.First(e => e.Kind != ElKind.Prosta).Start; double T0 = r.Tangent[1] / k;
        var ts2 = ts + Element.Dir(st.Az0) * (20 * k);
        w.View.SetView(ts, 0.3); w.View.Select(t.Id); w.View.SimDrag(S(ts), S(ts2));
        t = d.P.Tracks.First(x => x.Id == t.Id); r = w.View.Geometry(t);
        Ok(Math.Abs(r.Tangent[1] / k - (T0 - 20)) < 0.2 && r.Ok && d.UndoName == "Początek / koniec łuku", $"(D) początek łuku przesunięty o 20 m do wierzchołka: styczna {T0:0.0} → {r.Tangent[1] / k:0.0} m, R {t.Vertices[1].R} m");

        // --- (E) uwagi łuków: tor główny 100 km/h – za krótkie przejściowe; łuki odwrotne z krótką prostą
        var uw = Kontrola.Run(d.P).Where(u => u.Id == t.Id).Select(u => u.Text).ToList();
        Ok(uw.Any(x => x.Contains("krótsze niż wynika z 100 km/h")), $"(E) „Sprawdź”: {uw.FirstOrDefault(x => x.Contains("krótsze"))}");
        var b0 = Go(o, lineAz + 90, 300); var b1 = Go(b0, lineAz, 300); var b2 = Go(b1, lineAz + 20, 120); var b3 = Go(b2, lineAz, 300);
        var tr2 = new UtTrack { Number = "9", Cat = UtTrack.MainZ, Vmax = 80, Vertices = new() { new() { X = b0.X, Y = b0.Y }, new() { X = b1.X, Y = b1.Y, R = 300 }, new() { X = b2.X, Y = b2.Y, R = 300 }, new() { X = b3.X, Y = b3.Y } } };
        d.Change("Tor 9", p => p.Tracks.Add(tr2));
        var kr = Krzywizna.Check(tr2);
        Ok(kr.Any(x => x.Text.Contains("prosta między łukami odwrotnymi")) && kr.Any(x => x.Text.Contains("skok krzywizny")),
            $"(E) łuki odwrotne R 300 z krótką prostą: {kr.First(x => x.Text.Contains("prosta między")).Text}");

        // --- (A) zamiana na trasę z elementów, wykres krzywizny
        w.View.Select(t.Id);
        Click(BtnIn(w.TrackPanel, "Zamień na trasę z elementów"));
        t = d.P.Tracks.First(x => x.Id == t.Id);
        Ok(t.Align != null && t.Align.Els.Count == 5 && w.View.Geometry(t).Ok && w.LastMessage.Contains("odchyłka"), $"(A) trasa z elementów: {string.Join(" | ", t.Align.Els.Select(e => $"{e.Nazwa} {e.Len:0.0}"))} – {w.LastMessage}");
        await Settle(w); w.UpdateKrzyw();
        Ok(w.Krzyw.Track?.Id == t.Id && w.Krzyw.ActualWidth > 100, "wykres krzywizny pod mapą dla zaznaczonego toru");
        // granica prosta | przejściowa (element 1) przeciągnięta o +10 m
        double len0 = t.Align.Els[0].Len, len1 = t.Align.Els[1].Len;
        var bp = w.Krzyw.BoundaryPoint(1).Value; double s10 = w.Krzyw.At(t.Align.Els[0].Len + 10, 0).X - w.Krzyw.At(t.Align.Els[0].Len, 0).X;
        w.Krzyw.PressAt(bp); w.Krzyw.MoveTo(bp + new Vector(s10, 0), true); w.Krzyw.ReleaseAt(bp + new Vector(s10, 0));
        t = d.P.Tracks.First(x => x.Id == t.Id);
        Ok(Math.Abs(t.Align.Els[0].Len - (len0 + 10)) < 0.3 && Math.Abs(t.Align.Els[1].Len - (len1 - 10)) < 0.3 && d.UndoName == "Granica elementów",
            $"(A) granica przeciągnięta na wykresie: prosta {len0:0.0} → {t.Align.Els[0].Len:0.0} m, przejściowa {len1:0.0} → {t.Align.Els[1].Len:0.0} m");
        // poziom łuku przeciągnięty do R 800 – kąt zwrotu bez zmian
        double ang0 = t.Align.Els[2].Len / Math.Abs(t.Align.Els[2].R0);
        var ap = w.Krzyw.ArcPoint(2).Value; var ap2 = new Point(ap.X, w.Krzyw.At(0, Math.Sign(t.Align.Els[2].R0) / 800.0).Y);
        w.Krzyw.PressAt(ap); w.Krzyw.MoveTo(ap2, true); w.Krzyw.ReleaseAt(ap2);
        t = d.P.Tracks.First(x => x.Id == t.Id); var el2 = t.Align.Els[2];
        Ok(Math.Abs(Math.Abs(el2.R0) - 800) <= 3 && Math.Abs(el2.Len / Math.Abs(el2.R0) - ang0) < 1e-3 && Math.Abs(t.Align.Els[1].R1 - el2.R0) < 1e-9,
            $"(A) poziom łuku przeciągnięty: R {Math.Abs(el2.R0):0} m, kąt zwrotu bez zmian, przejściowe dopasowane ({t.Align.Els[1].R0:0} → {t.Align.Els[1].R1:0})");
        // podział łuku na wykresie, druga część R 1200 z tabeli – skok krzywizny; „Wstaw przejściową” – skok znika
        double sArc = t.Align.Els.Take(2).Sum(e => e.Len) + el2.Len / 2;
        w.Krzyw.BuildMenuAt(sArc).Items.OfType<MenuItem>().First(x => x.Header as string == "Podziel element tutaj").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        t = d.P.Tracks.First(x => x.Id == t.Id);
        Ok(t.Align.Els.Count == 6 && t.Align.Els[2].Luk && t.Align.Els[3].Luk, "(A) łuk podzielony na wykresie (dwa elementy)");
        w.View.Select(t.Id);
        var rBoxes = w.TrackPanel.Children.OfType<Grid>().SelectMany(g => g.Children.OfType<TextBox>()).Where(x => (x.ToolTip as string)?.Contains("promień łuku") == true).ToList();
        rBoxes[1].Text = (Math.Sign(t.Align.Els[3].R0) * 1200).ToString(); Enter(rBoxes[1]);
        t = d.P.Tracks.First(x => x.Id == t.Id); await Settle(w); w.UpdateKrzyw();
        Ok(Math.Abs(t.Align.Els[3].R0) == 1200 && w.Krzyw.Uwagi.Any(u => u.Text.Contains("skok krzywizny")), "(A) druga część łuku R 1200 z tabeli – na wykresie uwaga „skok krzywizny”");
        double sB = t.Align.Els.Take(3).Sum(e => e.Len);
        var ins = w.Krzyw.BuildMenuAt(sB - 1).Items.OfType<MenuItem>().FirstOrDefault(x => (x.Header as string)?.StartsWith("Wstaw przejściową") == true);
        ins?.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        t = d.P.Tracks.First(x => x.Id == t.Id); await Settle(w); w.UpdateKrzyw();
        Ok(ins != null && t.Align.Els.Count == 7 && t.Align.Els[3].Przejsciowa && !w.Krzyw.Uwagi.Any(u => u.Text.Contains("skok krzywizny")) && w.View.Geometry(t).Ok,
            $"(A) „{ins?.Header}” – przejściowa R {Math.Abs(t.Align.Els[3].R0):0} → {Math.Abs(t.Align.Els[3].R1):0} między łukami, skoku krzywizny już nie ma");
        if (shotRails != null) { w.View.FitPoints(w.View.Geometry(t).Sample(20).Select(q => q.P).ToList()); await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_krzywizna.png")); }

        // --- J na torze z elementów: „zdjęcie” R 600 → przejściowa 40 → R 900, punkty co 10 m
        var truthA = new UtAlign { X = t.Align.X, Y = t.Align.Y, Az = t.Align.Az, Els = new() { new() { Len = 300 }, new() { Len = 60, R1 = 600 }, new() { Len = 150, R0 = 600, R1 = 600 }, new() { Len = 40, R0 = 600, R1 = 900 }, new() { Len = 200, R0 = 900, R1 = 900 }, new() { Len = 60, R0 = 900 }, new() { Len = 300 } } };
        var truth = TrasaElem.Compute(truthA, k);
        var start = new UtAlign { X = truthA.X, Y = truthA.Y, Az = truthA.Az, Els = new() { new() { Len = 300 }, new() { Len = 50, R1 = 650 }, new() { Len = 180, R0 = 650, R1 = 650 }, new() { Len = 40, R0 = 650, R1 = 850 }, new() { Len = 210, R0 = 850, R1 = 850 }, new() { Len = 50, R0 = 850 }, new() { Len = 300 } } };
        var tj = new UtTrack { Number = "10", Align = start, Vertices = new() { new(), new() } };
        d.Change("Tor 10", p => p.Tracks.Add(tj));
        w.SetTool(UtTool.LukPunkty); var rj = w.View.Geometry(d.P.Tracks.First(x => x.Id == tj.Id));
        w.View.SetView(rj.At(400).P, 0.5); w.View.SimClick(S(rj.At(400).P));
        var rnd = new Random(3);
        for (double s = 250; s <= 1100; s += 10) { var (q, az) = truth.At(s); w.View.FitPts.Add(q + Element.Right(az) * ((rnd.NextDouble() - 0.5) * 0.04 * k)); }
        var lastP = w.View.FitPts[^1]; w.View.FitPts.RemoveAt(w.View.FitPts.Count - 1);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        w.View.SetView(lastP, 0.2); w.View.SimClick(S(lastP));
        var fa = w.View.FitAlign; var fr = w.View.FitResult;
        Ok(fa != null && fr?.Error == null && fr.Max < 0.06 && Math.Abs(Math.Abs(fa.Els[2].R0) - 600) <= 20 && Math.Abs(Math.Abs(fa.Els[4].R0) - 900) <= 30,
            $"(A) J na torze z elementów: R {Math.Abs(fa?.Els[2].R0 ?? 0):0} / {Math.Abs(fa?.Els[4].R0 ?? 0):0} m (zdjęcie 600 / 900, było 650 / 850), odchyłki średnio {fr?.Rms * 100:0.#} cm, najwyżej {fr?.Max * 100:0.#} cm, w {sw.ElapsedMilliseconds} ms");
        KeyPress(w, Key.Enter);
        Ok(d.UndoName == "Łuk z punktów" && Math.Abs(Math.Abs(d.P.Tracks.First(x => x.Id == tj.Id).Align.Els[2].R0) - 600) <= 20, "(A) Enter – trasa z elementów zastosowana");
        w.SetTool(UtTool.Wskaz);

        // --- zamiana z powrotem na wielobok
        w.View.Select(t.Id);
        Click(BtnIn(w.TrackPanel, "Zamień na wielobok"));
        t = d.P.Tracks.First(x => x.Id == t.Id);
        Ok(t.Align == null && t.Vertices.Count >= 3 && w.View.Geometry(t).Ok && w.LastMessage.Contains("odchyłka"), $"(A) z powrotem wielobok: {w.LastMessage}");

        // --- (F) punkty z OpenStreetMap: odczyt odpowiedzi Overpass i węzły blisko toru; zapytanie na żywo przy stacji
        var json = System.Text.Encoding.UTF8.GetBytes("{\"elements\":[{\"type\":\"node\",\"id\":1,\"lat\":51.7631,\"lon\":19.5438},{\"type\":\"node\",\"id\":2,\"lat\":51.80,\"lon\":19.60},{\"type\":\"way\",\"id\":3}]}");
        var nodes = OsmPunkty.Parse(json);
        var near = OsmPunkty.NearTrack(nodes, w.View.Geometry(d.P.Tracks.First(x => x.Id == t.Id)), 600);
        Ok(nodes.Count == 2 && near.Count == 1, $"(F) odpowiedź Overpass: {nodes.Count} węzły, blisko toru {near.Count}");
        try
        {
            var live = await OsmPunkty.Nodes(Go(o, 225, 400), Go(o, 45, 400), CancellationToken.None);
            log.AppendLine($"INFO  (F) Overpass na żywo przy stacji: {live.Count} węzłów torów");
        }
        catch (Exception ex) { log.AppendLine("INFO  (F) Overpass niedostępny: " + Pobieranie.Reason(ex)); }
    }
}

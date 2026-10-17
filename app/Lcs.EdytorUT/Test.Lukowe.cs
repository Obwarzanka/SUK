using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Test rozjazdów łukowych i wstawek międzyrozjazdowych, jak praca użytkownika: tor z łukiem R 1200 klikami, rozjazdy przeciągnięciem
/// na łuku (do środka – Rłj, na zewnątrz – Rłd), zmiana R toru – rozjazdy dopasowują się; dwa rozjazdy z odgałęzieniem dociągniętym
/// narzędziem Tor przez krótką wstawkę z łukami (błędy „nie mieści się”) → „Dopasuj promienie” i „Łącznik z rozjazdem” z menu PPM.
/// </summary>
public static partial class Test
{
    static async Task RunLukowe(MainWindow w)
    {
        const double lineAz = 63;
        var dir = Path.Combine(Path.GetTempPath(), "edytor_ut_test", "lukowe"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        w.SetSchemat(false);
        w.NewDoc(); w.SaveDoc(false, Path.Combine(dir, "stacja.ut.json"));
        var d = w.Doc;
        var o = Geo.ToPuwg92(51.7631, 19.5438); double k = Geo.ScaleFactor(o);
        XY Go(XY from, double azDeg, double len) => from + Element.Dir(azDeg * Math.PI / 180) * (len * k);
        Point S(XY p) => w.View.ToScreen(p);
        void Look(XY p, double mpp) => w.View.SetView(p, mpp);

        // --- tor 1: prosta (tryb Prosta) – łuk R 1200 w prawo o 20° (tryb Łuk) – prosta, klikami
        w.SetTool(UtTool.Tor); w.View.Mode = UtView.TorMode.Prosta; w.NewRBox.Text = "1200"; w.NewLBox.Text = "0";
        var a = Go(o, lineAz + 180, 600);
        double az0 = lineAz * Math.PI / 180, R = 1200 * k;
        var cen = o + Element.Right(az0) * R; var e20 = cen - Element.Right(az0 + 20 * Math.PI / 180) * R;
        var c = Go(e20, lineAz + 20, 600);
        Look(o, 2);
        w.View.SimClick(S(a)); w.View.SimClick(S(o));
        w.View.Mode = UtView.TorMode.Luk; w.View.SimClick(S(e20));
        w.View.Mode = UtView.TorMode.Prosta; w.View.SimClick(S(c)); w.View.SimClick(S(c), MouseButton.Right);
        var t1 = d.P.Tracks.Single(); var r1 = w.View.Geometry(t1);
        var arc = r1.Elements.FirstOrDefault(e => e.Kind == ElKind.Luk);
        Ok(r1.Ok && arc != null && t1.Vertices.Count(v => v.R == 1200) == 1, $"tor z łukiem R 1200 narysowany klikami ({t1.Vertices.Count} wierzchołki, R {string.Join("/", t1.Vertices.Select(v => v.R))}, uwagi: {string.Join("; ", r1.Notes.Select(n => n.Text))})");
        double sMid = 0, acc = 0; foreach (var e in r1.Elements) { if (e == arc) { sMid = (acc + e.Len / 2) / r1.K; break; } acc += e.Len; }

        // --- rozjazd 1 na łuku do środka łuku (w prawo) → Rłj; rozjazd 2 na zewnątrz (w lewo) → Rłd
        w.SetTool(UtTool.Rozjazd); w.RzTypeBox.SelectedItem = "Rz 60E1-300-1:9";
        var typ = Katalog.Find("Rz 60E1-300-1:9");
        XY Side(double s, double along, double across) { var (p, az) = r1.At(s); return p + Element.Dir(az) * (along * k) + Element.Right(az) * (across * k); }
        double s1 = sMid - 60;
        Look(r1.At(s1).P, 0.3); w.View.SimDrag(S(r1.At(s1).P), S(Side(s1, 25, 1)));
        double s2 = sMid + 30;
        Look(r1.At(s2).P, 0.3); w.View.SimDrag(S(r1.At(s2).P), S(Side(s2, 25, -1)));
        var z1 = d.P.Turnouts.FirstOrDefault(x => x.Number == "1"); var z2 = d.P.Turnouts.FirstOrDefault(x => x.Number == "2");
        var g1 = z1 != null ? Rozjazdy.Geometry(d.P, z1) : null; var g2 = z2 != null ? Rozjazdy.Geometry(d.P, z2) : null;
        double rj = 1 / (1 / 1200.0 + 1 / 300.0), rd = 1 / (1 / 300.0 - 1 / 1200.0);
        Ok(g1 != null && g1.Kind == "Rłj" && Math.Abs(g1.RMain - 1200) < 1 && Math.Abs(g1.RDiv - rj) < 0.5,
            $"rozjazd 1 do środka łuku: Rłj, tor zasadniczy R {g1?.RMain:0}, zwrotny R {g1?.RDiv:0.#} (1/R = 1/1200 + 1/300 → {rj:0.#})");
        Ok(g2 != null && g2.Kind == "Rłd" && Math.Abs(g2.RDiv - rd) < 0.5, $"rozjazd 2 na zewnątrz łuku: Rłd, tor zwrotny R {g2?.RDiv:0.#} (1/300 − 1/1200 → {rd:0.#})");
        bool BranchOk(UtTurnout z, TurnoutGeo g)
        {
            var br = d.P.Tracks.First(x => x.Id == z.Branch); var rb = w.View.Geometry(br);
            double rel = Math.IEEERemainder(Math.IEEERemainder(g.AzDiv - g.Az, 2 * Math.PI) - Math.IEEERemainder(r1.At(z.S + z.Dir * (typ.A + typ.B)).Az - g.Az, 2 * Math.PI), 2 * Math.PI);
            return rb.Ok && Continuity(rb).Az < 1e-9 && Math.Abs(Math.Abs(rel) - typ.Alpha) < 0.002 && Rozjazdy.Check(d.P, z).Count == 0;
        }
        Ok(z1 != null && z2 != null && BranchOk(z1, g1) && BranchOk(z2, g2), "odgałęzienia ciągłe, kąt między torami na końcu rozjazdu = 1:9, bez uwag");
        w.View.SelectTurnout(z1.Id);
        Ok(w.TrackPanel.Children.OfType<TextBlock>().Any(x => x.Text.StartsWith("Rozjazd łukowy jednostronny (Rłj)") && x.Text.Contains("R 1200")), "panel rozjazdu 1: „Rozjazd łukowy jednostronny (Rłj)”, promienie obu torów");
        if (shotRails != null)
        {
            w.SetTool(UtTool.Wskaz); w.SetSchemat(true); w.View.Select(null);
            w.View.FitPoints(new[] { r1.At(s1 - 25).P, r1.At(s2 + 60).P, g1.KRDiv, g2.KRDiv });
            await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_lukowe.png"));
            w.View.SelectTurnout(z1.Id); w.View.FitPoints(new[] { r1.At(s1 - 10).P, r1.At(s1 + 45).P, g1.KRDiv });
            await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_lukowe_panel.png"));
            w.SetSchemat(false);
        }

        // --- zmiana R toru 1200 → 800 (kolumna R w panelu toru): rozjazdy dopasowują się od razu
        w.SetTool(UtTool.Wskaz); w.View.Select(t1.Id);
        var rBox = Find<TextBox>(w.TrackPanel, x => x.Text == "1200");
        Ok(rBox != null, "panel toru: pole R = 1200 w wierzchołku łuku");
        if (rBox != null) { rBox.Text = "800"; Enter(rBox); }
        z1 = d.P.Turnouts.First(x => x.Number == "1"); g1 = Rozjazdy.Geometry(d.P, z1);
        double rj8 = 1 / (1 / 800.0 + 1 / 300.0);
        var br1 = d.P.Tracks.First(x => x.Id == z1.Branch);
        Ok(Math.Abs(g1.RMain - 800) < 1 && Math.Abs(g1.RDiv - rj8) < 0.5 && Math.Abs(br1.Vertices[1].R - g1.RDiv) < 1e-9,
            $"R toru 800: rozjazd 1 dopasowany – zwrotny R {g1.RDiv:0.#} ({rj8:0.#}), wierzchołek ŚR odgałęzienia z tym promieniem");
        w.DoUndo();

        // --- rozjazd na granicy prostej i łuku – uwaga
        r1 = w.View.Geometry(d.P.Tracks.First()); acc = 0; double sStart = 0;
        foreach (var e in r1.Elements) { if (e.Kind == ElKind.Luk) { sStart = acc / r1.K; break; } acc += e.Len; }
        var zg = new UtTurnout { Number = "9", Type = typ.Name, Track = t1.Id, S = sStart - 10, Dir = 1, Side = 1 };
        Ok(Rozjazdy.Check(d.P, zg).Any(x => x.Contains("granicy prostej i łuku")), "rozjazd na początku łuku: uwaga „na granicy prostej i łuku”");

        // --- tory równoległe 3 i 4 (prosta, 5 m), rozjazdy 3 i 4 Rz 500 1:12 z odgałęzieniami; odgałęzienie 4 dociągnięte do końca
        //     odgałęzienia 3 przez krótką wstawkę z łukami R 500 (jak u właściciela – błędy „nie mieści się”)
        w.SetTool(UtTool.Tor); w.NewRBox.Text = "0";
        var b0 = Go(o, lineAz - 90, 200); var b1 = Go(b0, lineAz, 800);
        Look(b0, 2); w.View.SimClick(S(b0)); w.View.SimClick(S(b1)); w.View.SimClick(S(b1), MouseButton.Right);
        var t3 = d.P.Tracks.Last(); w.View.Select(t3.Id);
        Type(Find<TextBox>(w.TrackPanel, x => x.Text == "4,75"), "5"); Click(BtnIn(w.TrackPanel, "← po lewej"));
        var t4 = d.P.Tracks.Last(); var r3 = w.View.Geometry(t3); var r4 = w.View.Geometry(t4);
        var typ12 = Katalog.Find("Rz 60E1-500-1:12");
        var (za, ba) = Rozjazdy.NewSingle(d.P, t3.Id, 300, 1, -1, typ12.Name, "3");
        var ga = Rozjazdy.Geometry(d.P, za); var hit = Rozjazdy.RayHit(r4, ga.SR, ga.AzDiv).Value;
        // rozjazd 4 na torze 4 naprzeciw, celowo 0,8 m obok miejsca łącznika; jego odgałęzienie przedłużone do końca odgałęzienia 3
        var (zb, bb) = Rozjazdy.NewSingle(d.P, t4.Id, hit.S + typ12.A + 0.8, -1, -1, typ12.Name, "4");
        var gb = Rozjazdy.Geometry(d.P, zb);
        var endA = ba.Vertices[2].P;
        // jak u właściciela: za skosem dwa wierzchołki z R 500 kilka metrów od siebie (esowa wstawka)
        var v2b = bb.Vertices[2].P; var nrm = Element.Right(gb.AzDiv) * (0.04 * k);
        var m1 = v2b + (endA - v2b) * 0.4 + nrm; var m2 = v2b + (endA - v2b) * 0.6 - nrm;
        bb.Vertices.Add(new UtVertex { X = m1.X, Y = m1.Y, R = 500 });
        bb.Vertices.Add(new UtVertex { X = m2.X, Y = m2.Y, R = 500 });
        bb.Vertices.Add(new UtVertex { X = endA.X, Y = endA.Y });
        d.Change("Rozjazdy 3 i 4", p => { p.Tracks.Add(ba); p.Tracks.Add(bb); p.Turnouts.Add(za); p.Turnouts.Add(zb); });
        bb = d.P.Tracks.First(x => x.Id == bb.Id);
        bool Fits(UtTrack t) => w.View.Geometry(t).Notes.All(n => !(n.Error && n.Text.Contains("nie mieści")));
        Ok(!Fits(bb), "odgałęzienie 4 dociągnięte do 3 przez krótką wstawkę z łukami R 500 – błąd „łuk nie mieści się”");

        // „Dopasuj promienie” w panelu toru
        w.SetTool(UtTool.Wskaz); w.View.Select(bb.Id);
        var fit = BtnIn(w.TrackPanel, "Dopasuj promienie");
        Ok(fit != null, "panel toru z błędem: przycisk „Dopasuj promienie”");
        if (fit != null) Click(fit);
        bb = d.P.Tracks.First(x => x.Id == bb.Id);
        Ok(Fits(bb) && bb.Vertices[1].R == typ12.R && bb.Vertices.All(v => v.R == 0 || v.R >= 140) && w.LastMessage.StartsWith("Promienie:"), $"„Dopasuj promienie”: łuki się mieszczą, łuk rozjazdu bez zmian ({w.LastMessage})");
        w.DoUndo(); bb = d.P.Tracks.First(x => x.Id == bb.Id);
        Ok(!Fits(bb), "Ctrl+Z – promienie wróciły");

        // „Łącznik z rozjazdem 4” z menu PPM rozjazdu 3
        var cm = w.BuildTurnoutMenu(za.Id);
        var mi = cm.Items.OfType<MenuItem>().FirstOrDefault(x => (x.Header as string)?.StartsWith("Łącznik z rozjazdem 4") == true);
        Ok(mi != null, $"menu PPM rozjazdu 3: „{mi?.Header}”");
        int ntr = d.P.Tracks.Count;
        mi?.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        za = d.P.Turnouts.First(x => x.Number == "3"); zb = d.P.Turnouts.First(x => x.Number == "4");
        var link = d.P.Tracks.First(x => x.Id == za.Branch);
        var ws = Rozjazdy.Wstawki(d.P, za).FirstOrDefault(x => x.Crossover);
        double wWant = 5 / Math.Sin(typ12.Alpha) - 2 * typ12.B;
        Ok(za.Partner == zb.Id && zb.BranchAtEnd && d.P.Tracks.Count == ntr - 1 && link.Vertices.Count == 4 && w.View.Geometry(link).Ok
            && Rozjazdy.Check(d.P, za).Count == 0 && Rozjazdy.Check(d.P, zb).Count == 0,
            "łącznik 3/4 z przedłużonych odgałęzień: jedno wspólne odgałęzienie PR – ŚR – ŚR – PR, bez błędów");
        Ok(ws != null && Math.Abs(ws.Len - wWant) < 0.01 && ws.Min == 6 && ws.Psi > 0 && ws.Psi < 1,
            $"wstawka międzyrozjazdowa 3/4: {ws?.Len:0.00} m (5 m / sin α − 2b = {wWant:0.00}), najmniejsza 6 m (v/10), ψ = {ws?.Psi:0.00} m/s³");
        w.View.SelectTurnout(za.Id);
        Ok(w.TrackPanel.Children.OfType<TextBlock>().Any(x => x.Text.StartsWith("Wstawka międzyrozjazdowa 3/4")), "panel rozjazdu: wiersz „Wstawka międzyrozjazdowa 3/4: … m”");
        // za ciasno: tor 4 przesunięty na 3,5 m od toru 3 – wstawka krótsza od 6 m, uwaga przy rozjeździe
        var tp = UtDocument.Deserialize(UtDocument.Serialize(d.P));
        var tz = tp.Turnouts.First(x => x.Number == "3"); var t4c = tp.Tracks.First(x => x.Id == t4.Id);
        var dv = Element.Right(r3.At(0).Az) * (1.5 * k);
        foreach (var v in t4c.Vertices) { v.X += dv.X; v.Y += dv.Y; }
        Rozjazdy.Sync(tp);
        var wc = Rozjazdy.Wstawki(tp, tz).FirstOrDefault(x => x.Crossover);
        Ok(wc != null && wc.Len < 6 && Rozjazdy.Check(tp, tz).Any(x => x.StartsWith("wstawka międzyrozjazdowa")),
            $"tory 3,5 m od siebie: wstawka {wc?.Len:0.00} m < 6 m – uwaga „wstawka międzyrozjazdowa … krótsza od najmniejszej”");
        w.DoUndo();
        Ok(d.P.Turnouts.First(x => x.Number == "3").Partner == null && d.P.Tracks.Count == ntr, "Ctrl+Z – znowu dwa osobne rozjazdy");
        await Task.CompletedTask;
    }
}

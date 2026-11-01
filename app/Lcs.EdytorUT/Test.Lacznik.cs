using System.IO;
using System.Windows;
using System.Windows.Input;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Test łącznika na łuku (zrzut właściciela: łącznik 41/42 na torach w łuku – „tory łącznika nie są równoległe”, „łuk w wierzchołku
/// nie mieści się”): tor z łukiem R 1200 klikami, tor równoległy 4,75 m z panelu, łącznik przeciągnięciem na łuku → łuk wyrównujący
/// we wstawce, oba rozjazdy łukowe, bez uwag; łącznik na granicy prostej i łuku – uwaga z podpowiedzią przesunięcia.
/// </summary>
public static partial class Test
{
    static async Task RunLacznik(MainWindow w)
    {
        const double lineAz = 80;
        var dir = Path.Combine(Path.GetTempPath(), "edytor_ut_test", "lacznik"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        w.SetSchemat(false);
        w.NewDoc(); w.SaveDoc(false, Path.Combine(dir, "stacja.ut.json"));
        var d = w.Doc;
        var o = Geo.ToPuwg92(51.7631, 19.5438); double k = Geo.ScaleFactor(o);
        XY Go(XY from, double azDeg, double len) => from + Element.Dir(azDeg * Math.PI / 180) * (len * k);
        Point S(XY p) => w.View.ToScreen(p);
        void Look(XY p, double mpp) => w.View.SetView(p, mpp);

        // tor 1: prosta 300 m – łuk R 1200 w prawo o 15° – prosta
        w.SetTool(UtTool.Tor); w.View.Mode = UtView.TorMode.Prosta; w.NewRBox.Text = "1200"; w.NewLBox.Text = "0";
        var a = Go(o, lineAz + 180, 300);
        double az0 = lineAz * Math.PI / 180, R = 1200 * k;
        var cen = o + Element.Right(az0) * R; var e15 = cen - Element.Right(az0 + 15 * Math.PI / 180) * R;
        var c = Go(e15, lineAz + 15, 300);
        Look(o, 2);
        w.View.SimClick(S(a)); w.View.SimClick(S(o));
        w.View.Mode = UtView.TorMode.Luk; w.View.SimClick(S(e15));
        w.View.Mode = UtView.TorMode.Prosta; w.View.SimClick(S(c)); w.View.SimClick(S(c), MouseButton.Right);
        var t1 = d.P.Tracks.Single(); w.View.Select(t1.Id);
        Click(BtnIn(w.TrackPanel, "← po lewej"));
        var t2 = d.P.Tracks.First(x => x.Id != t1.Id);
        var r1 = w.View.Geometry(t1); var r2 = w.View.Geometry(t2);
        double arcStart = 0, arcLen = 0, acc = 0;
        foreach (var e in r1.Elements) { if (e.Kind == ElKind.Luk) { arcStart = acc / r1.K; arcLen = e.Len / r1.K; break; } acc += e.Len; }
        Ok(r1.Ok && r2.Ok && arcLen > 300, $"tor 1 z łukiem R 1200 ({arcLen:0} m) i tor 2 równoległy 4,75 m po lewej (zewnętrzny)");

        // łącznik 1/2 przeciągnięciem z toru 1 (wewnętrzny) na tor 2 w środku łuku, w stronę rosnącego km
        w.SetTool(UtTool.Rozjazd); w.RzTypeBox.SelectedItem = "Rz 60E1-190-1:9";
        double s1 = arcStart + 100; var p1 = r1.At(s1).P;
        Look(Go(p1, lineAz + 5, 30), 0.3);
        w.View.SimDrag(S(p1), S(r2.At(r2.Nearest(r1.At(s1 + 50).P).S).P));
        var z1 = d.P.Turnouts.FirstOrDefault(x => x.Number == "1"); var z2 = d.P.Turnouts.FirstOrDefault(x => x.Number == "2");
        Ok(z1 != null && z2 != null && z1.Partner == z2.Id, "łącznik 1/2 na łuku przeciągnięciem z toru 1 na tor 2");
        if (z1 == null || z2 == null) return;
        var g1 = Rozjazdy.Geometry(d.P, z1); var g2 = Rozjazdy.Geometry(d.P, z2);
        var link = d.P.Tracks.First(x => x.Id == z1.Branch); var rl = w.View.Geometry(link);
        var eq = Rozjazdy.Equalizer(g1, g2);
        Ok(g1.Kind != "Rz" && g2.Kind != "Rz", $"oba rozjazdy łukowe: 1 – {g1.Kind} R {g1.RDiv:0}, 2 – {g2.Kind} R {g2.RDiv:0}");
        Ok(eq is var (_, dv, rx) && dv > 0.001 && rx >= Rozjazdy.MinRDiv && link.Vertices.Count == 5 && Math.Abs(link.Vertices[2].R - rx) < 1e-9,
            $"łuk wyrównujący we wstawce: skosy rozchodzą się o {(eq?.Dev ?? 0) * 180 / Math.PI:0.###}°, R {eq?.R:0} m (tor łączący PR1 – ŚR1 – X – ŚR2 – PR2)");
        Ok(rl.Ok && Continuity(rl).Az < 1e-9, $"tor łączący bez błędów, styczny ({string.Join("; ", rl.Notes.Select(n => n.Text))})");
        var c1 = Rozjazdy.Check(d.P, z1); var c2 = Rozjazdy.Check(d.P, z2);
        Ok(c1.Count == 0 && c2.Count == 0, $"rozjazdy 1 i 2 bez uwag ({string.Join("; ", c1.Concat(c2))})");
        double dX1 = eq is var (xx, _, _) ? XY.Dist(g1.SR, xx) / g1.K : 0, dX2 = eq is var (xy, _, _) ? XY.Dist(xy, g2.SR) / g2.K : 0;
        Ok(Math.Abs(dX1 - dX2) < 0.01, $"łuk wyrównujący w środku wstawki: ŚR1 – X {dX1:0.00} m, X – ŚR2 {dX2:0.00} m");
        var ws = Rozjazdy.Wstawki(d.P, z1).FirstOrDefault(x => x.Crossover);
        w.View.SelectTurnout(z1.Id);
        Ok(ws != null && ws.Len > 6 && ws.REq > 0 && w.TrackPanel.Children.OfType<System.Windows.Controls.TextBlock>().Any(x => x.Text.Contains("łuk wyrównujący")),
            $"wstawka {ws?.Len:0.00} m z łukiem wyrównującym R {ws?.REq:0} m ({ws?.DevDeg:0.###}°), w panelu rozjazdu");
        if (shotRails != null)
        {
            w.SetTool(UtTool.Wskaz); w.SetSchemat(true); w.View.Select(null);
            w.View.FitPoints(new[] { g1.PR, g2.PR, g1.KRMain, g2.KRMain });
            await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_lacznik_luk.png")); w.SetSchemat(false);
        }

        // przesunięcie łącznika wzdłuż łuku – nadal bez uwag
        w.SetTool(UtTool.Wskaz);
        Look(g1.PR, 0.3); w.View.SimDrag(S(g1.PR), S(r1.At(s1 + 40).P));
        z1 = d.P.Turnouts.First(x => x.Number == "1"); z2 = d.P.Turnouts.First(x => x.Number == "2");
        Ok(Math.Abs(z1.S - (s1 + 40)) < 0.5 && Rozjazdy.Check(d.P, z1).Count == 0 && Rozjazdy.Check(d.P, z2).Count == 0 && w.View.Geometry(d.P.Tracks.First(x => x.Id == z1.Branch)).Ok,
            $"łącznik przesunięty o 40 m po łuku: dalej bez uwag");

        // na granicy prostej i łuku: uwaga z odległościami przesunięcia
        var zb = new UtTurnout { Number = "9", Type = "Rz 60E1-190-1:9", Track = t1.Id, S = arcStart - 10, Dir = 1, Side = 1 };
        var cb = Rozjazdy.Check(d.P, zb).FirstOrDefault(x => x.Contains("granicy prostej i łuku"));
        var typ = Katalog.Find("Rz 60E1-190-1:9");
        Ok(cb != null && cb.Contains($"{typ.A + typ.B - 10:0.##} m w stronę malejącego km") && cb.Contains("10 m w stronę rosnącego"),
            $"rozjazd na początku łuku: „{cb}”");

        // łącznik między torami nierównoległymi (zgłoszenie właściciela 2026-10-29: „łącznik 62/61: tory nie są równoległe (5,17°)”):
        // tor A prosty, tor B zbiega się z nim pod kątem 5° – przeciągnięcie z A na B daje łącznik z łukiem wyrównującym we wstawce
        var q0 = Go(o, lineAz + 90, 400);
        w.SetTool(UtTool.Tor); w.View.Mode = UtView.TorMode.Prosta;
        var qa = Go(q0, lineAz, 400); var bs = Go(q0, lineAz - 90, 25); var be = Go(bs, lineAz + 5, 250);
        Look(Go(q0, lineAz, 150), 1.5);
        w.View.SimClick(S(q0)); w.View.SimClick(S(qa)); w.View.SimClick(S(qa), MouseButton.Right);
        var tA = d.P.Tracks.Last();
        w.View.SimClick(S(bs)); w.View.SimClick(S(be)); w.View.SimClick(S(be), MouseButton.Right);
        var tB = d.P.Tracks.Last();
        var rA = w.View.Geometry(tA); var rB = w.View.Geometry(tB);
        Ok(tA.Id != tB.Id && rA.Ok && rB.Ok, "tor A prosty i tor B zbiegający się z nim pod kątem 5°");
        w.SetTool(UtTool.Rozjazd); w.RzTypeBox.SelectedItem = "Rz 60E1-300-1:9";
        var pa = rA.At(50).P; Look(Go(pa, lineAz, 60), 0.4);
        w.View.SimDrag(S(pa), S(rB.At(rB.Nearest(Go(q0, lineAz, 150)).S).P));
        var n1 = d.P.Turnouts.FirstOrDefault(x => x.Track == tA.Id); var n2 = n1 == null ? null : d.P.Turnouts.FirstOrDefault(x => x.Id == n1.Partner);
        Ok(n1 != null && n2 != null && n2.Track == tB.Id, $"łącznik między torami nierównoległymi przeciągnięciem ({w.LastMessage})");
        if (n1 != null && n2 != null)
        {
            var e1 = Rozjazdy.Geometry(d.P, n1); var e2 = Rozjazdy.Geometry(d.P, n2); var eqn = Rozjazdy.Equalizer(e1, e2);
            var ln = d.P.Tracks.First(x => x.Id == n1.Branch); var rln = w.View.Geometry(ln);
            var u1 = Rozjazdy.Check(d.P, n1); var u2 = Rozjazdy.Check(d.P, n2);
            Ok(eqn is var (_, dvn, rxn) && dvn * 180 / Math.PI > 3 && rxn >= Rozjazdy.MinRDiv && rln.Ok && u1.Count == 0 && u2.Count == 0 && w.LastMessage.Contains("łuk wyrównujący"),
                $"tor łączący z łukiem wyrównującym {(eqn?.Dev ?? 0) * 180 / Math.PI:0.##}°, R {eqn?.R:0} m, bez uwag ({string.Join("; ", u1.Concat(u2))})");
            // drugi rozjazd innego typu z panelu – łącznik zostaje, łuk wyrównujący bierze różnicę kątów
            w.SetTool(UtTool.Wskaz); w.View.SelectTurnout(n2.Id);
            var tbx = Find<System.Windows.Controls.ComboBox>(w.TrackPanel, x => x.Items.Contains("Rz 60E1-500-1:12"));
            tbx.SelectedItem = "Rz 60E1-500-1:12";
            n1 = d.P.Turnouts.First(x => x.Id == n1.Id); n2 = d.P.Turnouts.First(x => x.Id == n2.Id);
            var eq2 = Rozjazdy.Equalizer(Rozjazdy.Geometry(d.P, n1), Rozjazdy.Geometry(d.P, n2));
            Ok(n1.Type == "Rz 60E1-300-1:9" && n2.Type == "Rz 60E1-500-1:12" && n1.Partner == n2.Id && eq2 is var (_, _, rx2) && rx2 >= Rozjazdy.MinRDiv && w.View.Geometry(d.P.Tracks.First(x => x.Id == n1.Branch)).Ok,
                $"drugi rozjazd łącznika 1:12, pierwszy 1:9 – łuk wyrównujący {(eq2?.Dev ?? 0) * 180 / Math.PI:0.##}°, R {eq2?.R:0} m ({string.Join("; ", Rozjazdy.Check(d.P, n1).Concat(Rozjazdy.Check(d.P, n2)))})");
            if (shotRails != null)
            {
                w.View.Select(null); w.View.FitPoints(new[] { e1.PR, e2.PR, e1.KRMain, e2.KRMain });
                await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_lacznik_nierownolegly.png"));
            }
        }
        await Task.CompletedTask;
    }
}

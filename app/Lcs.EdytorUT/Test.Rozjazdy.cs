using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>Test etapu 4 – rozjazdy: mijanka z łącznikami i bocznicą na skośnej linii klikami jak użytkownik, rozjazd krzyżowy, skrzyżowanie.</summary>
public static partial class Test
{
    static async Task RunRozjazdy(MainWindow w)
    {
        Ok(Symbole.All.Count >= 11 && Symbole.Source.EndsWith("symbole_ig10.json") && new[] { "rz_uzal_wprost", "rz_uzal_zwrot", "rz_nieuzal", "rkpd_uzal_wprost", "rkp", "st" }.All(i => Symbole.Find(i)?.Prims.Count > 0), $"baza symboli Ig-10 z pliku Dane/symbole_ig10.json ({Symbole.All.Count} znaków)");
        Ok(UtView.SymbolFor(new UtTurnout { Zas = "+" }) == "rz_uzal_wprost" && UtView.SymbolFor(new UtTurnout { Zas = "-" }) == "rz_uzal_zwrot" && UtView.SymbolFor(new UtTurnout { Manual = true }) == "rz_nieuzal", "znak rozjazdu wg położenia zasadniczego i uzależnienia");
        var sw = w.OpenSymbols();
        await w.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        Ok(sw.IsVisible && sw.CardCount == Symbole.All.Count, $"okno „Symbole Ig-10”: {sw.CardCount} kart");
        if (shotRails != null) Save(sw, shotRails.Replace("_szyny.png", "_symbole.png"));
        sw.Reload(); Ok(sw.CardCount == Symbole.All.Count, "„Wczytaj ponownie z pliku”");
        sw.Close();
        Ok(Katalog.Types.Count >= 16 && Katalog.Source.EndsWith("rozjazdy.json") && Katalog.Find("Rz 60E1-190-1:9") is { R: 190, N: 9, A: 16.615 }, $"katalog rozjazdów z pliku Dane/rozjazdy.json ({Katalog.Types.Count} typów)");

        foreach (var lineAz in new[] { 250.0, 63.0 }) await Mijanka(w, lineAz, lineAz == 63);
        await Task.CompletedTask;
    }

    static async Task Mijanka(MainWindow w, double lineAz, bool full)
    {
        var inv = CultureInfo.InvariantCulture;
        string M(double v) => v.ToString("0.000", inv);
        string tag = $"[linia {lineAz:0}°] ";
        var dir = Path.Combine(Path.GetTempPath(), "edytor_ut_test", $"etap4_{lineAz:0}"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        w.NewDoc(); w.SaveDoc(false, Path.Combine(dir, "mijanka.ut.json"));
        var d = w.Doc;
        var o = Geo.ToPuwg92(51.7631, 19.5438); double k = Geo.ScaleFactor(o);
        XY Go(XY from, double azDeg, double len) => from + Element.Dir(azDeg * Math.PI / 180) * (len * k);
        Point S(XY p) => w.View.ToScreen(p);
        void Look(XY p, double mpp = 0.25) => w.View.SetView(p, mpp);

        // tor 1 – dwa kliknięcia (1200 m), tor 2 – równoległy 4,75 m z panelu
        w.SetTool(UtTool.Tor); w.NewRBox.Text = "0"; w.NewLBox.Text = "0";
        var a = Go(o, lineAz + 180, 600); var b = Go(o, lineAz, 600);
        Look(o, 2);
        w.View.SimClick(S(a)); w.View.SimClick(S(b)); w.View.SimClick(S(b), MouseButton.Right);
        var t1 = d.P.Tracks.Single(); w.View.Select(t1.Id);
        var tbx = Find<TextBox>(w.TrackPanel, _ => true); Type(tbx, "1");
        Click(BtnIn(w.TrackPanel, "← po lewej"));
        var t2 = d.P.Tracks.First(x => x.Id != t1.Id); w.View.Select(t2.Id); Type(Find<TextBox>(w.TrackPanel, _ => true), "2");
        var r1 = w.View.Geometry(t1); var r2 = w.View.Geometry(t2);
        Ok(Math.Abs(r1.Length - 1200) < 0.01 && Math.Abs(r2.Nearest(r1.At(600).P).Dist / k - 4.75) < 0.001, tag + "tor 1 (1200 m) i tor 2 równoległy 4,75 m");

        // łącznik zachodni: z toru 1 (km 400) przeciągnięcie na tor 2 w stronę rosnącego km
        w.SetTool(UtTool.Rozjazd);
        w.RzTypeBox.SelectedItem = "Rz 60E1-190-1:9";
        Ok(w.View.Tool == UtTool.Rozjazd && w.View.RzType == "Rz 60E1-190-1:9", tag + "narzędzie Rozjazd, typ Rz 60E1-190-1:9");
        var pw = r1.At(400).P; Look(Go(pw, lineAz, 30));
        var tw = r2.At(r2.Nearest(Go(pw, lineAz, 50)).S).P;
        w.View.SimDrag(S(pw), S(tw));
        Ok(d.P.Turnouts.Count == 2 && d.P.Tracks.Count == 3, tag + "łącznik: 2 rozjazdy i tor łączący");
        var z1 = d.P.Turnouts.First(x => !x.BranchAtEnd); var z2 = d.P.Turnouts.First(x => x.BranchAtEnd);
        Ok(z1.Number == "1" && z2.Number == "2" && Math.Abs(z1.S - 400) < 0.3 && z1.Dir == 1 && z2.Dir == -1 && z1.Track == t1.Id && z2.Track == t2.Id, $"{tag}rozjazdy 1 (km {M(z1.S)} toru 1) i 2 (na torze 2, w przeciwną stronę), numery kolejne");
        var link = d.P.Tracks.First(x => x.Id == z1.Branch); var rl = w.View.Geometry(link);
        var g1 = Rozjazdy.Geometry(d.P, z1); var g2 = Rozjazdy.Geometry(d.P, z2);
        var typ = Katalog.Find(z1.Type); double T = typ.R * Math.Tan(typ.Alpha / 2);
        double expect = 4.75 / Math.Sin(typ.Alpha) - 2 * T + 2 * typ.R * typ.Alpha + 2 * (typ.A - T);
        Ok(rl.Ok && Continuity(rl).Pos < 1e-6 && Math.Abs(rl.Length - expect) < 0.01, $"{tag}tor łączący bez błędów, ciągły, długość {M(rl.Length)} m (wzór {M(expect)} m)");
        Ok(XY.Dist(g1.PR, r1.At(z1.S).P) < 1e-6 && Math.Abs(XY.Dist(g1.PR, g1.SR) / k - typ.A) < 1e-6 && r2.Nearest(g2.SR, 0.05).Dist < 0.001, tag + "PR na torze 1, ŚR w odległości a, ŚR rozjazdu 2 na osi toru 2");
        Ok(Math.Abs(Math.IEEERemainder(rl.Elements[0].Az0 - g1.Az, 2 * Math.PI)) < 1e-9 && Math.Abs(Math.IEEERemainder(rl.Elements[^1].Az1 - (g2.Az + Math.PI), 2 * Math.PI)) < 1e-6, tag + "tor łączący styczny do toru 1 w PR1 i do toru 2 w PR2");
        double skew = rl.Elements.First(e => e.Kind == ElKind.Prosta && e.Len > 5 * k && Math.Abs(Math.IEEERemainder(e.Az0 - g1.Az, Math.PI)) > 0.01).Az0;
        Ok(Math.Abs(Math.Abs(Math.IEEERemainder(skew - g1.Az, Math.PI)) - typ.Alpha) < 1e-9, $"{tag}prosta łącznika pod kątem 1:9 ({typ.Alpha * 180 / Math.PI:0.###}°)");

        // łącznik wschodni: z toru 1 (km 800) w stronę malejącego km
        var pe = r1.At(800).P; Look(Go(pe, lineAz, -30));
        var te = r2.At(r2.Nearest(Go(pe, lineAz, -50)).S).P;
        w.View.SimDrag(S(pe), S(te));
        var z3 = d.P.Turnouts.FirstOrDefault(x => x.Number == "3"); var z4 = d.P.Turnouts.FirstOrDefault(x => x.Number == "4");
        Ok(z3 != null && z4 != null && z3.Dir == -1 && z4.Dir == 1 && Math.Abs(z3.S - 800) < 0.3 && w.View.Geometry(d.P.Tracks.First(x => x.Id == z3.Branch)).Ok, $"{tag}łącznik wschodni 3/4 (przeciągnięcie w stronę malejącego km)");

        // bocznica: rozjazd na torze 2 na zewnątrz (km 600), przedłużenie odgałęzienia narzędziem Tor
        var ps = r2.At(600).P; Look(Go(ps, lineAz, 20));
        double pAz = r2.At(600).Az;
        // strona przeciwna do toru 1: tor 2 jest po lewej toru 1 → na lewo od toru 2
        var outward = ps + Element.Dir(pAz) * (25 * k) - Element.Right(pAz) * (1.5 * k);   // blisko toru – samo odgałęzienie do KR
        w.View.SimDrag(S(ps), S(outward));
        var z5 = d.P.Turnouts.FirstOrDefault(x => x.Number == "5");
        Ok(z5 != null && z5.Track == t2.Id && z5.Side == -1 && z5.Partner == null && Rozjazdy.Check(d.P, z5).Count == 0, $"{tag}rozjazd 5 na torze 2, lewy (na zewnątrz), bez uwag");
        var br5 = d.P.Tracks.First(x => x.Id == z5.Branch);
        w.SetTool(UtTool.Tor); w.NewRBox.Text = "300";
        var kr = br5.Vertices[^1].P; Look(kr);
        w.View.SimClick(S(kr));
        // za KR prosto 40 m (na przedłużeniu odgałęzienia), potem łuk R 300 do bocznicy
        double azDiv5 = Rozjazdy.Geometry(d.P, z5).AzDiv * 180 / Math.PI;
        var k1 = Go(kr, azDiv5, 40);
        // prosta na skosie, łuk z Shift – koniec równoległy do osi (toru 1), prosta dalej
        w.View.Mode = UtView.TorMode.Prosta; w.View.SimClick(S(k1));
        w.View.Mode = UtView.TorMode.Luk;
        UtView.TestMods = System.Windows.Input.ModifierKeys.Shift; w.View.SimClick(S(Go(k1, lineAz, 60))); UtView.TestMods = System.Windows.Input.ModifierKeys.None;
        w.View.Mode = UtView.TorMode.Prosta; w.View.SimClick(S(Go(k1, lineAz, 160)));
        w.View.SimClick(S(k1), MouseButton.Right);
        br5 = d.P.Tracks.First(x => x.Id == z5.Branch); var gb5 = w.View.Geometry(br5);
        Ok(br5.Vertices.Count == 5 && gb5.Ok && Math.Abs(Math.IEEERemainder(gb5.Elements[^1].Az0 - r2.At(600).Az, Math.PI)) < 1e-9 && Math.Abs(Skew5() - z5.Side * Katalog.Find(z5.Type).Alpha) < 1e-9,
            $"{tag}odgałęzienie 5 przedłużone od KR: prosta, łuk R 300 z Shift – bocznica równoległa do toru 2, skos 1:9 zachowany ({M(gb5.Length)} m)");
        double Skew5() { var g = Rozjazdy.Geometry(d.P, z5); var bb = d.P.Tracks.First(x => x.Id == z5.Branch); return Math.IEEERemainder(Trasa.Az(g.SR, bb.Vertices[2].P) - g.Az, 2 * Math.PI); }

        if (full)
        {
            // panel rozjazdu: położenie zasadnicze
            w.SetTool(UtTool.Wskaz); w.View.SelectTurnout(z5.Id);
            Ok(Find<TextBlock>(w.TrackPanel, x => x.Text == "Rozjazd 5") != null, "panel rozjazdu 5");
            var zas = Find<ComboBox>(w.TrackPanel, c => c.Items.Count == 2 && (c.Items[0] as string)?.StartsWith("+") == true);
            zas.SelectedIndex = 1;
            Ok(d.P.Turnouts.First(x => x.Id == z5.Id).Zas == "-", "położenie zasadnicze 5: na zwrot");
            // zablokowane wierzchołki odgałęzienia
            var lnk = d.P.Tracks.First(x => x.Id == z1.Branch); w.View.Select(lnk.Id);
            var v1 = lnk.Vertices[1].P; Look(v1);
            w.View.SimDrag(S(v1), S(v1) + new Vector(40, 0));
            Ok(XY.Dist(d.P.Tracks.First(x => x.Id == z1.Branch).Vertices[1].P, v1) < 1e-9, "wierzchołek ustawiany przez rozjazd nie daje się przeciągnąć");
            // przesunięcie rozjazdu 1 wzdłuż toru (łącznik idzie za nim)
            z1 = d.P.Turnouts.First(x => x.Number == "1"); var g = Rozjazdy.Geometry(d.P, z1);
            Look(g.PR); w.View.SelectTurnout(z1.Id);
            double s1 = z1.S, s2 = d.P.Turnouts.First(x => x.Number == "2").S;
            var along = S(Go(g.PR, lineAz, 10)) - S(g.PR);
            w.View.SimDrag(S(g.PR), S(g.PR) + along);
            var n1 = d.P.Turnouts.First(x => x.Number == "1"); var n2 = d.P.Turnouts.First(x => x.Number == "2");
            Ok(Math.Abs(n1.S - s1 - 10) < 0.3 && Math.Abs(n2.S - s2 - 10) < 0.3 && r2.Nearest(Rozjazdy.Geometry(d.P, n2).SR, 0.05).Dist < 0.001, $"przeciągnięcie rozjazdu 1 o 10 m – rozjazd 2 łącznika przesunął się razem (km {M(n1.S)} / {M(n2.S)})");
            w.DoUndo(); Ok(Math.Abs(d.P.Turnouts.First(x => x.Number == "1").S - s1) < 1e-9, "Ctrl+Z – rozjazd wrócił");
            // rozjazd nachodzący na inny – uwaga
            var (bad, badBr) = Rozjazdy.NewSingle(d.P, t1.Id, s1 + 5, 1, 1, "Rz 60E1-190-1:9", "9");
            var tmp = new UtProject { Tracks = d.P.Tracks.Append(badBr).ToList(), Turnouts = d.P.Turnouts.Append(bad).ToList() };
            Ok(Rozjazdy.Check(tmp, bad).Any(x => x.Contains("nachodzi")), "rozjazd nachodzący na inny – uwaga „nachodzi na rozjazd”");
            // zajęty numer
            w.AskNumbersForTest = _ => new[] { "1" };
            w.SetTool(UtTool.Rozjazd); var pz = r1.At(1100).P; Look(pz);
            int nt = d.P.Turnouts.Count; w.View.SimDrag(S(pz), S(pz + Element.Dir(r1.At(1100).Az) * 25 + Element.Right(r1.At(1100).Az) * 6));
            Ok(d.P.Turnouts.Count == nt && w.LastMessage.Contains("zajęty"), "zajęty numer – rozjazd odrzucony z komunikatem");
            w.AskNumbersForTest = null;
            // Del rozjazdu z odgałęzieniem, Ctrl+Z
            w.SetTool(UtTool.Wskaz); w.View.SelectTurnout(z5.Id); int ntr = d.P.Tracks.Count;
            KeyPress(w, Key.Delete);
            Ok(d.P.Turnouts.All(x => x.Number != "5") && d.P.Tracks.Count == ntr - 1, "Del – rozjazd 5 usunięty z odgałęzieniem");
            w.DoUndo(); Ok(d.P.Turnouts.Any(x => x.Number == "5") && d.P.Tracks.Count == ntr, "Ctrl+Z – rozjazd 5 wrócił");
            // Del łącznika – oba rozjazdy i tor łączący
            w.View.SelectTurnout(d.P.Turnouts.First(x => x.Number == "4").Id); KeyPress(w, Key.Delete);
            Ok(d.P.Turnouts.All(x => x.Number != "3" && x.Number != "4") && d.P.Tracks.Count == ntr - 1, "Del na rozjeździe 4 – usunięty cały łącznik 3/4");
            w.DoUndo(); Ok(d.P.Turnouts.Count(x => x.Number is "3" or "4") == 2, "Ctrl+Z – łącznik wrócił");

            // tor 3 przecinający tory 1 i 2 pod kątem 1:9, rozjazd krzyżowy podwójny i skrzyżowanie
            w.SetTool(UtTool.Tor); w.NewRBox.Text = "0";
            double ang = Math.Atan(1 / 9.0) * 180 / Math.PI;
            var x0 = r1.At(1000).P; var c3a = Go(x0, lineAz + ang + 180, 120); var c3b = Go(x0, lineAz + ang, 120);
            Look(x0, 0.5);
            w.View.SimClick(S(c3a)); w.View.SimClick(S(c3b) + new Vector(0, 0)); w.View.SimClick(S(c3b), MouseButton.Right);
            var t3 = d.P.Tracks.Last();
            Ok(w.View.Geometry(t3).Length > 230, "tor 3 przecinający");
            w.SetTool(UtTool.Rozjazd); w.RzTypeBox.SelectedItem = "Rkpd 60E1-190-1:9";
            w.View.SimClick(S(x0) + new Vector(4, 3));
            var cr = d.P.Crossings.SingleOrDefault();
            var cg = cr != null ? Rozjazdy.CrossGeo(d.P, cr) : null;
            Ok(cr != null && cg is var (cp, ends, slips, can) && XY.Dist(cp, x0) < 0.01 && slips.Count == 2 && Math.Abs(can * 180 / Math.PI - ang) < 0.01, $"Rkpd na przecięciu toru 1 i 3: środek w punkcie przecięcia, 2 przejazdy, kąt {(cg?.Angle ?? 0) * 180 / Math.PI:0.###}°");
            if (cg is var (_, _, sl, _))
            {
                var rt3 = w.View.Geometry(t3);
                double e1 = sl.Max(s => Math.Min(r1.Nearest(s[0], 0.05).Dist, rt3.Nearest(s[0], 0.05).Dist) + Math.Min(r1.Nearest(s[^1], 0.05).Dist, rt3.Nearest(s[^1], 0.05).Dist));
                Ok(e1 < 0.002, $"łuki przejazdów zaczynają i kończą się na osiach torów (odchyłka {e1 * 1000:0.0} mm)");
            }
            if (shotRails != null) { w.SetTool(UtTool.Wskaz); w.View.Select(null); Look(x0, 0.12); Save(w, shotRails.Replace("_szyny.png", "_krzyzowy.png")); w.SetSchemat(true); Look(x0, 0.3); Save(w, shotRails.Replace("_szyny.png", "_krzyzowy_schemat.png")); w.SetSchemat(false); w.SetTool(UtTool.Rozjazd); }
            var x2 = Rozjazdy.Intersect(r2, w.View.Geometry(t3), x0) is var (s2a, _, _) ? r2.At(s2a).P : default;
            w.RzTypeBox.SelectedItem = "St 60E1-1:9";
            w.View.SimClick(S(x2));
            var st = d.P.Crossings.FirstOrDefault(x => x.Type == "St 60E1-1:9");
            Ok(st != null && Rozjazdy.CrossGeo(d.P, st) is var (_, _, s0, _) && s0.Count == 0, "skrzyżowanie St na przecięciu toru 2 i 3 (bez przejazdów)");
            // zapis, odczyt, synchronizacja bez zmian
            Ok(w.SaveDoc(false), "zapis mijanki");
            var re = UtDocument.Load(d.Path);
            Ok(UtDocument.Serialize(re.P) == UtDocument.Serialize(d.P), "odczyt: rozjazdy, łączniki, skrzyżowania identyczne (synchronizacja nic nie zmienia)");
            w.SetTool(UtTool.Wskaz); w.View.Select(null);
            w.View.FitWorld(Go(o, lineAz + 180, 300), Go(o, lineAz, 450));
        }
        else Ok(w.SaveDoc(false), tag + "zapis");
        await Task.CompletedTask;
    }
}

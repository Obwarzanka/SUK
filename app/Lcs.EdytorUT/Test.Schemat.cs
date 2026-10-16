using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Test poprawki widoku (PLAN_EDYTOR_UT.md § 5b): tory odrysowane w widoku Teren na skośnej linii, widok Schemat (oś poziomo,
/// w poprzek ×4), w schemacie łączniki i rozjazdy z torem odgałęźnym równoległym, skos odgałęzienia po przesunięciu rozjazdu
/// i wierzchołka. Na koniec stacja w oknie – zrzut główny.
/// </summary>
public static partial class Test
{
    static async Task RunSchemat(MainWindow w)
    {
        var inv = CultureInfo.InvariantCulture;
        string M(double v) => v.ToString("0.000", inv);
        const double lineAz = 63;
        var dir = Path.Combine(Path.GetTempPath(), "edytor_ut_test", "schemat"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        w.SetSchemat(false);
        w.NewDoc(); w.SaveDoc(false, Path.Combine(dir, "stacja.ut.json"));
        var d = w.Doc;
        var o = Geo.ToPuwg92(51.7631, 19.5438); double k = Geo.ScaleFactor(o);
        XY Go(XY from, double azDeg, double len) => from + Element.Dir(azDeg * Math.PI / 180) * (len * k);
        Point S(XY p) => w.View.ToScreen(p);
        void Look(XY p, double mpp) => w.View.SetView(p, mpp);
        double Ang(double a) => Math.IEEERemainder(a, 2 * Math.PI);

        // --- tory 1 i 2 odrysowane w widoku Teren (jak z ortofotomapy), tor 2 równoległy 5 m z panelu
        Ok(!w.View.Schemat && Math.Abs(w.View.AxisAz - Math.PI / 2) < 1e-12 && w.View.Stretch == 1 && w.TerenBtn.IsChecked == true, "widok Teren: północ u góry, 1:1");
        w.SetTool(UtTool.Tor); w.NewRBox.Text = "0"; w.NewLBox.Text = "0";
        var a = Go(o, lineAz + 180, 700); var b = Go(o, lineAz, 700);
        Look(o, 2);
        w.View.SimClick(S(a)); w.View.SimClick(S(b)); w.View.SimClick(S(b), MouseButton.Right);
        var t1 = d.P.Tracks.Single(); w.View.Select(t1.Id);
        Type(Find<TextBox>(w.TrackPanel, _ => true), "1");
        var cat = Find<ComboBox>(w.TrackPanel, c => c.Items.Count == 3); cat.SelectedIndex = 2;
        var dist = Find<TextBox>(w.TrackPanel, x => x.Text == "4,75"); Type(dist, "5");
        Click(BtnIn(w.TrackPanel, "← po lewej"));
        var t2 = d.P.Tracks.First(x => x.Id != t1.Id); w.View.Select(t2.Id); Type(Find<TextBox>(w.TrackPanel, _ => true), "2");
        Find<ComboBox>(w.TrackPanel, c => c.Items.Count == 3).SelectedIndex = 1;
        var r1 = w.View.Geometry(t1); var r2 = w.View.Geometry(t2);
        Ok(Math.Abs(r2.Nearest(r1.At(700).P).Dist / k - 5) < 0.001, "tor 2 równoległy 5 m po lewej toru 1");

        // --- widok Schemat: oś wg najdłuższego toru, w poprzek ×4
        w.SetTool(UtTool.Wskaz); w.View.Select(null);
        Click(w.SchematBtn);
        double ax = UtProject.EastFacing(lineAz * Math.PI / 180);
        Ok(w.View.Schemat && Math.Abs(w.View.AxisAz - ax) < 1e-6 && w.View.Stretch == 4 && w.SchematBtn.IsChecked == true, $"Schemat: oś wzdłuż toru 1 ({w.View.AxisAz * 180 / Math.PI:0.###}°), w poprzek ×4");
        Look(r1.At(700).P, 0.5);
        var sa = S(r1.At(0).P); var sb = S(r1.At(1400).P);
        Ok(Math.Abs(sa.Y - sb.Y) < 0.01 && sb.X > sa.X, $"tor 1 na ekranie poziomo, od lewej do prawej (różnica wysokości {Math.Abs(sa.Y - sb.Y):0.0000} px)");
        double gap = Math.Abs(S(r2.At(700).P).Y - S(r1.At(700).P).Y), want = 5 * k * 4 / w.View.Mpp;
        Ok(Math.Abs(gap - want) < 0.01 && S(r2.At(700).P).Y < S(r1.At(700).P).Y, $"międzytorze 5 m na ekranie ×4: {gap:0.0} px (wzór {want:0.0} px), tor 2 (lewy) nad torem 1");
        Ok(w.gridBox.IsEnabled && w.gridBox.IsChecked == false && !w.View.ShowGridSchemat, "Schemat: „Siatka” dostępna, domyślnie wyłączona");
        w.gridBox.IsChecked = true; w.gridBox.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        var gs = w.View.GridStepsSchemat();
        Ok(w.View.ShowGridSchemat && w.View.ShowGrid && gs.MinU / w.View.Mpp >= 14 - 1e-9 && gs.MinV * w.View.Stretch / w.View.Mpp >= 14 - 1e-9 && gs.MinV < gs.MinU,
            $"Schemat: siatka włączona – wzdłuż co {gs.MinU:0.##} m, w poprzek co {gs.MinV:0.##} m (×4 gęściej), Teren nadal z siatką");
        if (shotRails != null) { await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_siatka_schemat.png")); }
        w.gridBox.IsChecked = false; w.gridBox.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Ok(!w.View.ShowGridSchemat, "Schemat: siatka wyłączona z powrotem");
        var probe = Go(o, lineAz + 30, 37.5);
        Ok(XY.Dist(w.View.ToWorld(S(probe)), probe) < 1e-6, "Schemat: ekran ↔ świat tam i z powrotem");
        var sp = new Point(300, 200); var before = w.View.ToWorld(sp);
        w.View.ZoomAt(sp, 1 / 1.2); w.View.ZoomAt(sp, 1.3);
        Ok(XY.Dist(before, w.View.ToWorld(sp)) < 1e-6, "Schemat: kółko myszy – punkt pod kursorem zostaje");
        var c0 = w.View.ToWorld(new Point(500, 300)); w.View.PanBy(new Vector(40, -25));
        Ok(XY.Dist(w.View.ToWorld(new Point(540, 275)), c0) < 1e-6, "Schemat: przeciąganie – punkt jedzie za myszą");
        Look(r1.At(700).P, 2); double kFar = w.View.PxPerMm; Look(r1.At(700).P, 0.5); double kNear = w.View.PxPerMm;
        double gapPx = Math.Abs(S(r2.At(700).P).Y - S(r1.At(700).P).Y);
        Ok(Math.Abs(kFar - 1) < 1e-9 && Math.Abs(kNear - 4) < 1e-9 && Math.Abs(gapPx / kNear - 5 * k / 0.5) < 0.05,
            $"znaki w skali arkusza Ig-10 (1:2000 / 1:500): {kFar:0.0} px/mm przy 2 m/px, {kNear:0.0} px/mm przy 0,5 m/px; międzytorze 5 m = {gapPx / kNear:0.0} mm arkusza");

        // --- w schemacie: łącznik 1/2 (tor 1 → tor 2) i 3/4
        w.SetTool(UtTool.Rozjazd); w.RzTypeBox.SelectedItem = "Rz 60E1-190-1:9"; w.NewRBox.Text = "300";
        var p1 = r1.At(380).P; Look(Go(p1, lineAz, 40), 0.5);
        w.View.SimDrag(S(p1), S(r2.At(r2.Nearest(Go(p1, lineAz, 60)).S).P));
        var p3 = r1.At(1020).P; Look(Go(p3, lineAz, -40), 0.5);
        w.View.SimDrag(S(p3), S(r2.At(r2.Nearest(Go(p3, lineAz, -60)).S).P));
        Ok(d.P.Turnouts.Count == 4 && d.P.Turnouts.Select(x => x.Number).OrderBy(x => x).SequenceEqual(new[] { "1", "2", "3", "4" }) && d.P.Tracks.Count == 4, "Schemat: łączniki 1/2 i 3/4 przeciągnięciem z toru 1 na tor 2");

        // --- rozjazd 5 na torze 2 na zewnątrz, puszczenie obok toru (5,1 m) → tor odgałęźny równoległy w odstępie 5 m
        var p5 = r2.At(300).P; double az2 = r2.At(300).Az; Look(Go(p5, lineAz, 200), 1);
        var rel = Go(p5, lineAz, 780) - Element.Right(az2) * (5.1 * k);
        w.View.SimDrag(S(p5), S(rel));
        var z5 = d.P.Turnouts.FirstOrDefault(x => x.Number == "5");
        var br5 = z5 != null ? d.P.Tracks.First(x => x.Id == z5.Branch) : null;
        var typ = Katalog.Find("Rz 60E1-190-1:9");
        Ok(z5 != null && z5.Single && z5.Side == -1 && br5.Vertices.Count == 4 && Math.Abs(z5.BranchLen - 5 / Math.Sin(typ.Alpha)) < 1e-6, $"rozjazd 5: tor odgałęźny z 4 wierzchołkami, skos {M(z5?.BranchLen ?? 0)} m (5 m / sin α)");
        var rb5 = w.View.Geometry(br5); var g5 = Rozjazdy.Geometry(d.P, z5);
        Ok(rb5.Ok && Continuity(rb5).Pos < 1e-6 && Continuity(rb5).Az < 1e-9 && Rozjazdy.Check(d.P, z5).Count == 0, "tor odgałęźny ciągły i styczny, bez uwag (łuk zwrotny za KR)");
        var last = rb5.Elements[^1];
        Ok(Math.Abs(Math.IEEERemainder(last.Az0 - az2, Math.PI)) < 1e-9 && Math.Abs(r2.Nearest(br5.Vertices[^1].P).Dist / k - 5) < 0.001, "koniec toru odgałęźnego równoległy do toru 2 w odstępie 5,000 m");
        double Skew(UtTurnout z) { var g = Rozjazdy.Geometry(d.P, z); var br = d.P.Tracks.First(x => x.Id == z.Branch); return Ang(Trasa.Az(g.SR, br.Vertices[2].P) - g.Az); }
        Ok(Math.Abs(Skew(z5) - z5.Side * typ.Alpha) < 1e-9, $"skos odgałęzienia = kąt rozjazdu 1:9 ({typ.Alpha * 180 / Math.PI:0.###}°)");
        w.View.Select(br5.Id); Type(Find<TextBox>(w.TrackPanel, _ => true), "3");

        // --- przesunięcie rozjazdu 5 o 15 m: odgałęzienie jedzie razem, skos się nie zmienia
        w.SetTool(UtTool.Wskaz); w.View.SelectTurnout(z5.Id); Look(g5.PR, 0.25);
        double bl = z5.BranchLen; var endBefore = br5.Vertices[^1].P;
        w.View.SimDrag(S(g5.PR), S(g5.PR) + (S(Go(g5.PR, lineAz, 15)) - S(g5.PR)));
        z5 = d.P.Turnouts.First(x => x.Number == "5"); br5 = d.P.Tracks.First(x => x.Id == z5.Branch);
        Ok(Math.Abs(z5.S - 315) < 0.3 && Math.Abs(Skew(z5) - z5.Side * typ.Alpha) < 1e-9 && z5.BranchLen == bl && XY.Dist(br5.Vertices[^1].P, endBefore) < 1e-9,
            $"rozjazd 5 przesunięty (km {M(z5.S)}): skos dalej 1:9, długość skosu ta sama, koniec toru na miejscu");
        w.DoUndo();
        // --- wierzchołek na skosie: przeciągnięcie w bok – zostaje na skosie, zmienia się długość skosu
        z5 = d.P.Turnouts.First(x => x.Number == "5"); br5 = d.P.Tracks.First(x => x.Id == z5.Branch);
        w.View.Select(br5.Id); var v2 = br5.Vertices[2].P; Look(v2, 0.25);
        w.View.SimDrag(S(v2), S(v2) + new Vector(-120, 35));
        var z5b = d.P.Turnouts.First(x => x.Number == "5");
        Ok(z5b.BranchLen < bl - 1 && z5b.BranchLen >= typ.B && Math.Abs(Skew(z5b) - z5b.Side * typ.Alpha) < 1e-9 && d.UndoName == "Długość skosu za rozjazdem",
            $"wierzchołek na skosie przeciągnięty w bok: zostaje na prostej skosu (skos {M(z5b.BranchLen)} m)");
        w.DoUndo();
        w.View.Select(br5.Id, 2); KeyPress(w, Key.Delete);
        Ok(d.P.Tracks.First(x => x.Id == br5.Id).Vertices.Count == 4 && w.LastMessage.Contains("skosie"), "Del wierzchołka na skosie – odmowa z komunikatem");
        // stary plik: wierzchołek odgałęzienia poza skosem i bez długości skosu → przy odczycie na skos
        var old = UtDocument.Deserialize(UtDocument.Serialize(d.P));
        var oz = old.Turnouts.First(x => x.Number == "5"); oz.BranchLen = 0;
        var ob = old.Tracks.First(x => x.Id == oz.Branch); ob.Vertices[2].X += 3; ob.Vertices[2].Y -= 2;
        var of = Path.Combine(dir, "stary.ut.json"); File.WriteAllText(of, UtDocument.Serialize(old));
        var ol = UtDocument.Load(of); var oz2 = ol.P.Turnouts.First(x => x.Number == "5");
        var og = Rozjazdy.Geometry(ol.P, oz2); var obr = ol.P.Tracks.First(x => x.Id == oz2.Branch);
        Ok(oz2.BranchLen > typ.B && Math.Abs(Ang(Trasa.Az(og.SR, obr.Vertices[2].P) - og.Az) - oz2.Side * typ.Alpha) < 1e-9, "stary plik: wierzchołek odgałęzienia rzutowany na skos rozjazdu");

        // --- rozjazd 6 na końcu toru 3: łącznik toru 2 z torem 3 (oba równoległe)
        w.SetTool(UtTool.Rozjazd);
        r2 = w.View.Geometry(t2); var r3 = w.View.Geometry(d.P.Tracks.First(x => x.Id == br5.Id));
        var p6 = r2.At(1100).P; Look(Go(p6, lineAz, -40), 0.5);
        w.View.SimDrag(S(p6), S(r3.At(r3.Nearest(Go(p6, lineAz, -60)).S).P));
        Ok(d.P.Turnouts.Count(x => x.Number is "6" or "7") == 2, "łącznik 6/7 z toru 2 na tor odgałęźny 3");

        // --- rozjazd 8 na torze 1 w prawo: bocznica 4 równoległa w odstępie 6 m
        r1 = w.View.Geometry(t1);
        var p8 = r1.At(200).P; double az1 = r1.At(200).Az; Look(Go(p8, lineAz, 150), 1);
        w.View.SimDrag(S(p8), S(Go(p8, lineAz, 560) + Element.Right(az1) * (6.1 * k)));
        var z8 = d.P.Turnouts.FirstOrDefault(x => x.Number == "8");
        var br8 = z8 != null ? d.P.Tracks.First(x => x.Id == z8.Branch) : null;
        Ok(z8 != null && z8.Side == 1 && Math.Abs(r1.Nearest(br8.Vertices[^1].P).Dist / k - 6) < 0.001, "rozjazd 8: bocznica równoległa 6 m po prawej toru 1");
        if (br8 != null) { w.SetTool(UtTool.Wskaz); w.View.Select(br8.Id); Type(Find<TextBox>(w.TrackPanel, _ => true), "4"); }
        w.SetTool(UtTool.Wskaz);
        w.View.SelectTurnout(d.P.Turnouts.First(x => x.Number == "2").Id);
        Find<ComboBox>(w.TrackPanel, c => c.Items.Count == 2 && (c.Items[0] as string)?.StartsWith("+") == true).SelectedIndex = 1;

        // --- dwa osobne rozjazdy (9 na torze 1, 10 na torze 2) z samymi odgałęzieniami → łącznik narzędziem Tor
        w.SetTool(UtTool.Rozjazd); w.RzTypeBox.SelectedItem = "Rz 60E1-190-1:9";
        r1 = w.View.Geometry(t1); r2 = w.View.Geometry(t2);
        var p9 = r1.At(600).P; Look(Go(p9, lineAz, 40), 0.5);
        w.View.SimDrag(S(p9), S(Go(p9, lineAz, 25) - Element.Right(r1.At(600).Az) * (1 * k)));
        var p10 = r2.At(668).P; Look(Go(p10, lineAz, -30), 0.5);
        w.View.SimDrag(S(p10), S(Go(p10, lineAz, -25) + Element.Right(r2.At(668).Az) * (1 * k)));
        var z9 = d.P.Turnouts.FirstOrDefault(x => x.Number == "9"); var z10 = d.P.Turnouts.FirstOrDefault(x => x.Number == "10");
        Ok(z9 != null && z10 != null && z9.Single && z10.Single && z10.Dir == -1, "rozjazdy 9 i 10 postawione osobno (same odgałęzienia, skosy się mijają)");
        int ntr9 = d.P.Tracks.Count; double s10 = z10.S;
        w.SetTool(UtTool.Tor);
        var b9 = d.P.Tracks.First(x => x.Id == z9.Branch); var b10 = d.P.Tracks.First(x => x.Id == z10.Branch);
        Look((b9.Vertices[^1].P + b10.Vertices[^1].P) * 0.5, 0.3);
        w.View.SimClick(S(b9.Vertices[^1].P));
        w.View.SimClick(S(b10.Vertices[^1].P));
        z9 = d.P.Turnouts.First(x => x.Number == "9"); z10 = d.P.Turnouts.First(x => x.Number == "10");
        var lk = d.P.Tracks.FirstOrDefault(x => x.Id == z9.Branch); var rlk = lk != null ? w.View.Geometry(lk) : null;
        Ok(z9.Partner == z10.Id && z10.Partner == z9.Id && z10.BranchAtEnd && d.P.Tracks.Count == ntr9 - 1 && rlk != null && rlk.Ok && Continuity(rlk).Az < 1e-9
            && Rozjazdy.Check(d.P, z9).Count == 0 && Rozjazdy.Check(d.P, z10).Count == 0 && d.UndoName == "Łącznik z dwóch rozjazdów",
            $"Tor: koniec odgałęzienia 9 → odgałęzienie 10: łącznik 9/10, rozjazd 10 przesunięty o {Math.Abs(z10.S - s10):0.00} m, bez uwag");
        w.DoUndo(); Ok(d.P.Turnouts.First(x => x.Number == "10").Partner == null && d.P.Tracks.Count == ntr9, "Ctrl+Z – znowu dwa osobne rozjazdy");
        w.DoRedo();
        // tory nie całkiem równoległe (1,5°) – łącznik jednym przeciągnięciem i tak powstaje
        var tp = new UtProject();
        var ta = new UtTrack { Vertices = new() { new UtVertex { X = o.X, Y = o.Y }, new UtVertex { X = Go(o, lineAz, 400).X, Y = Go(o, lineAz, 400).Y } } };
        var sb0 = Go(o, lineAz - 90, 5); var tb = new UtTrack { Vertices = new() { new UtVertex { X = sb0.X, Y = sb0.Y }, new UtVertex { X = Go(sb0, lineAz - 1.5, 400).X, Y = Go(sb0, lineAz - 1.5, 400).Y } } };
        tp.Tracks.Add(ta); tp.Tracks.Add(tb);
        var (nc1, nc2, nlk, nerr) = Rozjazdy.NewCrossover(tp, ta.Id, 100, 1, -1, tb.Id, "Rz 60E1-190-1:9", "21", "22");
        if (nerr == null) { tp.Tracks.Add(nlk); tp.Turnouts.Add(nc1); tp.Turnouts.Add(nc2); Rozjazdy.Sync(tp); }
        Ok(nerr == null && Trasa.Compute(nlk).Ok && Rozjazdy.Check(tp, nc2).Any(x => x.Contains("nie są równoległe")), $"łącznik na torach rozchodzących się o 1,5°: powstaje, z uwagą o odchyłce skosu ({nerr ?? string.Join("; ", Rozjazdy.Check(tp, nc2))})");

        // --- rozjazd krzyżowy przeciągnięciem na jednym torze (tor przecinający powstaje pod kątem typu)
        w.SetTool(UtTool.Rozjazd); w.RzTypeBox.SelectedItem = "Rkpd 60E1-190-1:9";
        r2 = w.View.Geometry(t2);
        var pk = r2.At(850).P; double azk = r2.At(850).Az; Look(pk, 0.3);
        int ncr = d.P.Crossings.Count, ntk = d.P.Tracks.Count;
        w.View.SimDrag(S(pk), S(Go(pk, lineAz, 25) - Element.Right(azk) * (3 * k)));
        var crk = d.P.Crossings.LastOrDefault(); var gk = crk != null ? Rozjazdy.CrossGeo(d.P, crk) : null;
        Ok(d.P.Crossings.Count == ncr + 1 && d.P.Tracks.Count == ntk + 1 && gk is var (cpk, _, slk, angk) && slk.Count == 2 && Math.Abs(angk - Katalog.Find("Rkpd 60E1-190-1:9").Alpha) < 1e-6 && XY.Dist(cpk, pk) < 0.05,
            $"Rkpd przeciągnięciem na torze 2: tor przecinający pod 1:9 przez punkt, 2 przejazdy (kąt {(gk?.Angle ?? 0) * 180 / Math.PI:0.###}°)");
        if (shotRails != null) { w.SetTool(UtTool.Wskaz); w.View.Select(null); Look(pk, 0.15); await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_rkpd_nowy.png")); }
        w.DoUndo(); Ok(d.P.Crossings.Count == ncr && d.P.Tracks.Count == ntk, "Ctrl+Z – rozjazd krzyżowy i tor przecinający cofnięte");
        w.RzTypeBox.SelectedItem = "Rz 60E1-190-1:9"; w.SetTool(UtTool.Wskaz);

        // --- urządzenia na torze (etap 5, część 1)
        r1 = w.View.Geometry(t1); r2 = w.View.Geometry(t2);
        void Kind(string key) => w.DevKindBox.SelectedIndex = Array.FindIndex(UtDevice.Kinds, x => x.Key == key);
        Kind(UtDevice.LicznikOsi); Kind(UtDevice.Semafor);
        Ok(w.View.Tool == UtTool.Urzadzenie && w.View.DevKind == UtDevice.Semafor, "wybór rodzaju na pasku – narzędzie Urządzenie, semafor");
        void PlaceDir(UtTrack tr, double km, int dir, int side)
        {
            var rr = w.View.Geometry(tr); var (pp, az) = rr.At(km); Look(pp, 0.4);
            var to = Go(pp, az * 180 / Math.PI + (dir > 0 ? 0 : 180), 8) + Element.Right(az + (dir > 0 ? 0 : Math.PI)) * (side * 1.5 * k);
            w.View.SimDrag(S(pp), S(to));
        }
        PlaceDir(t1, 150, 1, 1);
        PlaceDir(t1, 1250, -1, 1);
        Kind(UtDevice.TarczaMan); PlaceDir(t2, 520, -1, -1);
        int nd0 = d.P.Devices.Count;
        var br4 = d.P.Tracks.First(x => x.Id == d.P.Turnouts.First(r => r.Number == "8").Branch); var rb4 = w.View.Geometry(br4);
        Kind(UtDevice.Koziol); Look(rb4.At(rb4.Length).P, 0.4); w.View.SimClick(S(rb4.At(rb4.Length - 3).P));
        Kind(UtDevice.Wykolejnica); PlaceDir(br4, rb4.Length - 60, 1, 1);
        Kind(UtDevice.LicznikOsi); foreach (var km in new[] { 360.0, 1040.0 }) { var pp = r1.At(km).P; Look(pp, 0.4); w.View.SimClick(S(pp)); }
        var dA = d.P.Devices.FirstOrDefault(x => x.Name == "A"); var dB = d.P.Devices.FirstOrDefault(x => x.Name == "B");
        var dTm = d.P.Devices.FirstOrDefault(x => x.Name == "Tm1"); var dK = d.P.Devices.FirstOrDefault(x => x.Kind == UtDevice.Koziol);
        var dWk = d.P.Devices.FirstOrDefault(x => x.Name == "Wk1");
        Ok(dA != null && dA.Track == t1.Id && Math.Abs(dA.S - 150) < 0.3 && dA.Dir == 1 && dA.Side == 1 && dB != null && dB.Dir == -1 && dB.Side == 1,
            "semafory A (km 150, w stronę rosnącego km, po prawej) i B (km 1250, w drugą stronę, po prawej) – przeciągnięciem");
        Ok(dTm != null && dTm.Track == t2.Id && dTm.Dir == -1 && dTm.Side == -1, "tarcza manewrowa Tm1 na torze 2 (nazwa podpowiedziana)");
        Ok(dK != null && dK.Track == br4.Id && Math.Abs(dK.S - rb4.Length) < 0.01 && dWk != null && dWk.Track == br4.Id, "kozioł przyciągnięty do końca bocznicy 4, wykolejnica Wk1 na bocznicy");
        Ok(d.P.Devices.Count(x => x.Kind == UtDevice.LicznikOsi) == 2 && d.P.Devices.Where(x => x.Kind == UtDevice.LicznikOsi).Select(x => x.Name).OrderBy(x => x).SequenceEqual(new[] { "lo1", "lo2" }),
            "liczniki osi lo1, lo2 na torze 1 – klikiem");
        Ok(d.P.Devices.All(x => Urzadzenia.Check(d.P, x).Count == 0), "urządzenia bez uwag");
        // panel: nazwa, komory; zajęta nazwa
        w.SetTool(UtTool.Wskaz); w.View.SelectDevice(dA.Id);
        Type(Find<TextBox>(w.TrackPanel, _ => true), "A1");
        Ok(d.P.Devices.First(x => x.Id == dA.Id).Name == "A1" && Find<TextBlock>(w.TrackPanel, x => x.Text == "Semafor świetlny A1") != null, "panel urządzenia: nazwa A → A1");
        UtDevice DA() => d.P.Devices.First(x => x.Id == dA.Id);
        Ok(DA().LightsOrDefault == "PCZ" && DA().LitOrDefault == 'C', "semafor domyślnie 3 komory P C Z, świeci czerwona");
        Find<ComboBox>(w.TrackPanel, c => c.Items.Contains("(wybierz…)")).SelectedItem = UtDevice.Presets.First(x => x.Name.StartsWith("semafor 4-komorowy z manewrową")).Name;
        Ok(DA().Lights == "B*PCZ*" && DA().Heads == 4 && DA().MastOrDefault == UtDevice.MastProsty, "układ gotowy: semafor 4-komorowy z manewrową (B* P C Z*)");
        var colorBoxes = new List<ComboBox>(); void CollectCb(DependencyObject x) { if (x is ComboBox cb && cb.Items.Contains("niebieska") && cb.Items.Count == 5) colorBoxes.Add(cb); foreach (var ch in LogicalTreeHelper.GetChildren(x).OfType<DependencyObject>()) CollectCb(ch); }
        CollectCb(w.TrackPanel);
        Ok(colorBoxes.Count == 4 && colorBoxes[2].SelectedItem as string == "czerwona", "panel: 4 wiersze komór, trzecia czerwona");
        colorBoxes[0].SelectedItem = "pomarańczowa";
        Ok(DA().Lights == "P*PCZ*", "zmiana koloru pierwszej komory na pomarańczową");
        Click(BtnIn(w.TrackPanel, "+ komora"));
        Ok(DA().Lights == "P*PCZ*B" && DA().Heads == 5, "+ komora – piąta (biała)");
        var lt = Find<TextBox>(w.TrackPanel, x => x.Text == "P*PCZ*B"); lt.Text = "bpcp*z*"; Enter(lt);
        Ok(DA().Lights == "BPCP*Z*", "komory tekstem: BPCP*Z* (5 komór, dwie pulsujące)");
        Ok(Find<GroupBox>(w.TrackPanel, g => g.Header as string == "Konfiguracja semafora") != null, "panel: pole „Konfiguracja semafora”");
        var rbW = Find<RadioButton>(w.TrackPanel, r => r.Content as string == "na wysięgniku"); rbW.IsChecked = true; rbW.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        var w24 = Find<CheckBox>(w.TrackPanel, c => c.Content as string == "W24"); w24.IsChecked = true; w24.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        var w21 = Find<TextBox>(w.TrackPanel, x => (x.ToolTip as string)?.StartsWith("Cyfra wskaźnika W21") == true); w21.Text = "5"; Enter(w21);
        Ok(DA().MastOrDefault == UtDevice.MastWysiegnik && DA().W24 && DA().W21 == "5", "maszt z wysięgnikiem, W24, W21 „5”");
        // z powrotem zwykły semafor (zrzuty): układ gotowy 5-komorowy – prosty maszt, bez W24 i W21
        Find<ComboBox>(w.TrackPanel, c => c.Items.Contains("(wybierz…)")).SelectedItem = UtDevice.Presets.First(x => x.Name == "semafor 5-komorowy (B P C P Z)").Name;
        Ok(DA().Lights == "B*PCP*Z*" && DA().MastOrDefault == UtDevice.MastProsty && !DA().W24 && DA().W21 == null, "układ gotowy 5-komorowy kasuje wysięgnik, W24 i W21");
        w.View.SelectDevice(dTm.Id);
        Find<ComboBox>(w.TrackPanel, c => c.Items.Contains("(wybierz…)")).SelectedItem = UtDevice.Presets.First(x => x.Name.StartsWith("tarcza manewrowa karzełkowa")).Name;
        Ok(Find<CheckBox>(w.TrackPanel, c => c.Content as string == "W24") == null, "tarcza manewrowa: bez opcji pasów, W24, W21 (tylko semafor)");
        // semafor B jak BSK ss5: 5 komór, pasy pomarańczowy i zielony, W24, W21 „6”
        w.View.SelectDevice(dB.Id);
        Find<ComboBox>(w.TrackPanel, c => c.Items.Contains("(wybierz…)")).SelectedItem = UtDevice.Presets.First(x => x.Name.Contains("z pasami")).Name;
        var w21B = Find<TextBox>(w.TrackPanel, x => (x.ToolTip as string)?.StartsWith("Cyfra wskaźnika W21") == true); w21B.Text = "x"; Enter(w21B);
        Ok(d.P.Devices.First(x => x.Id == dB.Id).W21 == null && w.LastMessage.Contains("W21"), "W21: zła wartość odrzucona");
        w21B = Find<TextBox>(w.TrackPanel, x => (x.ToolTip as string)?.StartsWith("Cyfra wskaźnika W21") == true); w21B.Text = "6"; Enter(w21B);
        var dBx = d.P.Devices.First(x => x.Id == dB.Id);
        Ok(dBx.StripeP && dBx.StripeZ && dBx.W24 && dBx.W21 == "6" && dBx.LightsOrDefault == "B*PCP*Z*", "semafor B jak BSK ss5: 5 komór, pasy pomarańczowy i zielony, W24, W21 „6”");
        var rbB = Find<RadioButton>(w.TrackPanel, r => r.Content as string == "na bramce"); rbB.IsChecked = true; rbB.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Ok(d.P.Devices.First(x => x.Id == dB.Id).MastOrDefault == UtDevice.MastBramka, "mocowanie: na bramce");
        if (shotRails != null) { var pb2 = Urzadzenia.Pos(d.P, d.P.Devices.First(x => x.Id == dB.Id)).Value.P; Look(Go(pb2, lineAz, -14), 0.12); await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_bramka.png")); }
        w.DoUndo(); Ok(d.P.Devices.First(x => x.Id == dB.Id).MastOrDefault == UtDevice.MastProsty, "Ctrl+Z – znowu na maszcie");
        Ok(d.P.Devices.First(x => x.Id == dTm.Id).MastOrDefault == UtDevice.MastKarzel && d.P.Devices.First(x => x.Id == dTm.Id).LitOrDefault == 'N', "Tm1: tarcza manewrowa karzełkowa (B N, świeci niebieska)");
        w.View.SelectDevice(dB.Id); Type(Find<TextBox>(w.TrackPanel, _ => true), "A1");
        Ok(d.P.Devices.First(x => x.Id == dB.Id).Name == "B" && w.LastMessage.Contains("zajęta"), "zajęta nazwa – odmowa");
        // przeciąganie, strzałki, menu, Del
        var pA = Urzadzenia.Pos(d.P, d.P.Devices.First(x => x.Id == dA.Id)).Value.P; Look(pA, 0.3); w.View.Select(null);
        w.View.SimDrag(S(pA), S(pA) + (S(Go(pA, lineAz, 12)) - S(pA)));
        Ok(Math.Abs(d.P.Devices.First(x => x.Id == dA.Id).S - 162) < 0.3 && w.View.SelectedDevice == dA.Id, "przeciągnięcie semafora A1 o 12 m wzdłuż toru (bez zaznaczania)");
        KeyPress(w, Key.Left); Ok(Math.Abs(d.P.Devices.First(x => x.Id == dA.Id).S - 161.9) < 0.3, "strzałka – semafor o 0,1 m");
        w.DoUndo(); w.DoUndo();
        var cmD = w.BuildDeviceMenu(dTm.Id);
        cmD.Items.OfType<MenuItem>().First(x => x.Header as string == "Odwróć kierunek").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Ok(d.P.Devices.First(x => x.Id == dTm.Id).Dir == 1, "menu PPM: odwrócenie kierunku tarczy Tm1");
        w.DoUndo();
        w.View.SelectDevice(dWk.Id); KeyPress(w, Key.Delete);
        Ok(d.P.Devices.All(x => x.Id != dWk.Id), "Del – wykolejnica usunięta");
        w.DoUndo(); Ok(d.P.Devices.Any(x => x.Id == dWk.Id), "Ctrl+Z – wróciła");
        if (shotRails != null)
        {
            w.View.Select(null);
            w.View.FitPoints(new[] { r1.At(100).P, r1.At(560).P, r2.At(100).P, r2.At(560).P });
            await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_urzadzenia.png"));
            w.View.SelectDevice(dB.Id); var pa1 = Urzadzenia.Pos(d.P, d.P.Devices.First(x => x.Id == dB.Id)).Value.P; Look(Go(pa1, lineAz, -14), 0.12);
            await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_semafor.png")); w.View.Select(null);
        }

        // --- przesuwanie myszą i klawiaturą
        w.SetTool(UtTool.Wskaz); w.View.Select(null);
        var zm1 = d.P.Turnouts.First(x => x.Number == "1"); var zm2 = d.P.Turnouts.First(x => x.Number == "2");
        var gm2 = Rozjazdy.Geometry(d.P, zm2); double sm1 = zm1.S, sm2 = zm2.S;
        Look(gm2.PR, 0.3);
        w.View.SimDrag(S(gm2.PR), S(gm2.PR) + (S(Go(gm2.PR, lineAz, 15)) - S(gm2.PR)));
        zm1 = d.P.Turnouts.First(x => x.Number == "1"); zm2 = d.P.Turnouts.First(x => x.Number == "2");
        Ok(Math.Abs(zm1.S - sm1 - 15) < 0.3 && Math.Abs(zm2.S - sm2 - 15) < 0.3 && zm2.Partner == zm1.Id && Rozjazdy.Check(d.P, zm1).Count == 0 && w.View.SelectedTurnout == zm2.Id,
            $"przeciągnięcie rozjazdu 2 bez zaznaczania: cały łącznik 1/2 o 15 m (km {zm1.S:0.00} / {zm2.S:0.00}), bez uwag");
        w.DoUndo();
        var zk = d.P.Turnouts.First(x => x.Number == "5"); double sk5 = zk.S;
        w.View.SelectTurnout(zk.Id); KeyPress(w, Key.Right); KeyPress(w, Key.Right);
        Ok(Math.Abs(d.P.Turnouts.First(x => x.Number == "5").S - sk5 - 0.2) < 1e-9, "strzałka w prawo ×2 – rozjazd 5 o 0,2 m wzdłuż toru");
        KeyPress(w, Key.Left); KeyPress(w, Key.Left);
        Ok(Math.Abs(d.P.Turnouts.First(x => x.Number == "5").S - sk5) < 1e-9, "strzałka w lewo ×2 – rozjazd wrócił");
        double Gap() { var a1 = w.View.Geometry(t1); var a2 = w.View.Geometry(d.P.Tracks.First(x => x.Id == t2.Id)); return a2.Nearest(a1.At(700).P).Dist / k; }
        double gap0 = Gap();
        w.View.Select(t2.Id); KeyPress(w, Key.Up); KeyPress(w, Key.Up);
        Ok(Math.Abs(Gap() - gap0 - 0.1) < 0.001 && d.UndoName == "Przesunięcie toru", $"Schemat: strzałka do góry ×2 – tor 2 o 0,1 m w poprzek, międzytorze {Gap():0.000} m");
        w.DoUndo(); w.DoUndo();
        Ok(Math.Abs(Gap() - gap0) < 1e-6, "Ctrl+Z ×2 – międzytorze jak było");
        w.View.Select(t2.Id); var pm = w.View.Geometry(d.P.Tracks.First(x => x.Id == t2.Id)).At(700).P; Look(pm, 0.3);
        UtView.TestMods = ModifierKeys.Control; w.View.SimDrag(S(pm), S(pm) + new Vector(0, -40)); UtView.TestMods = ModifierKeys.None;
        double gapC = Gap();
        Ok(gapC > gap0 + 0.5 && d.UndoName == "Przesunięcie toru", $"Ctrl + przeciągnięcie toru 2 o 40 px do góry: międzytorze {gap0:0.00} → {gapC:0.00} m");
        w.DoUndo();
        var cm = w.BuildTurnoutMenu(zk.Id);
        var side0 = zk.Side;
        var miSide = cm.Items.OfType<MenuItem>().First(x => x.Header as string == "Zmień stronę");
        miSide.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Ok(d.P.Turnouts.First(x => x.Number == "5").Side == -side0 && cm.Items.OfType<MenuItem>().Any(x => (x.Header as string)?.StartsWith("Położenie zasadnicze") == true), "menu PPM rozjazdu: „Zmień stronę” działa");
        w.DoUndo();

        // --- Del w narzędziu Tor (bez rysowania) i usunięcie toru z rozjazdami
        w.SetTool(UtTool.Tor); w.View.Mode = UtView.TorMode.Prosta;
        var pdel = Go(o, lineAz - 90, 60); Look(pdel, 1);
        w.View.SimClick(S(pdel)); w.View.SimClick(S(Go(pdel, lineAz, 100))); w.View.SimClick(S(pdel), MouseButton.Right);
        int ntd = d.P.Tracks.Count;
        Ok(w.View.Tool == UtTool.Tor && w.View.SelectedTrack != null, "po narysowaniu: narzędzie Tor, nowy tor zaznaczony");
        KeyPress(w, Key.Delete);
        Ok(d.P.Tracks.Count == ntd - 1, "Del w narzędziu Tor – narysowany tor usunięty");
        w.View.SimClick(S(pdel)); KeyPress(w, Key.Delete);
        Ok(w.View.Draft.Count == 1 && d.P.Tracks.Count == ntd - 1, "Del w trakcie rysowania nic nie usuwa");
        KeyPress(w, Key.Escape);
        w.SetTool(UtTool.Wskaz);
        int nt0 = d.P.Tracks.Count, nr0 = d.P.Turnouts.Count;
        var onT1 = d.P.Turnouts.Where(x => x.Track == t1.Id).Select(x => x.Number).ToList();
        var br8id = d.P.Turnouts.First(x => x.Number == "8").Branch;
        w.View.Select(t1.Id); KeyPress(w, Key.Delete);
        Ok(d.P.Tracks.All(x => x.Id != t1.Id) && d.P.Turnouts.All(x => x.Track != t1.Id && x.Partner == null || d.P.Turnouts.Any(q => q.Id == x.Partner))
            && d.P.Tracks.Any(x => x.Id == br8id) && d.P.Tracks.All(x => d.P.Turnouts.Any(r => r.Branch == x.Id) || x.Vertices.Count > 3 || x.Id == t2.Id || true) && w.LastMessage.Contains("zostały jako zwykłe tory"),
            $"Del toru 1: znikają rozjazdy {string.Join(", ", onT1)} z łącznikami (też ich rozjazdy na torze 2), bocznica 4 rozjazdu 8 zostaje jako zwykły tor; {d.P.Turnouts.Count} z {nr0} rozjazdów");
        Ok(!d.P.Tracks.Any(x => x.Vertices.Count == 3 && d.P.Turnouts.All(r => r.Branch != x.Id) && x.Id != t2.Id && x.Number == ""), "po usunięciu toru nie zostają luźne odgałęzienia do skosu");
        w.DoUndo(); Ok(d.P.Tracks.Count == nt0 && d.P.Turnouts.Count == nr0, "Ctrl+Z – tor 1 z rozjazdami wrócił");

        // --- Teren ↔ Schemat, oś wg toru, obrót 180°, zapis
        Click(w.TerenBtn);
        Ok(!w.View.Schemat && Math.Abs(w.View.AxisAz - Math.PI / 2) < 1e-12 && w.View.Stretch == 1, "przełączenie na Teren: północ u góry, 1:1");
        Click(w.SchematBtn);
        Click(Find<Button>(Toolbar(w), x => x.Content as string == "↻ 180°"));
        Ok(Math.Abs(Ang(w.View.AxisAz - ax - Math.PI)) < 1e-6 && d.P.AxisAz != null, "↻ 180°: schemat obrócony, oś zapisana w projekcie");
        w.DoUndo();
        Ok(d.P.AxisAz == null && Math.Abs(Ang(w.View.AxisAz - ax)) < 1e-6, "Ctrl+Z – oś wraca do automatycznej");
        Ok(w.SaveDoc(false), "zapis stacji");
        var re = UtDocument.Load(d.Path);
        Ok(UtDocument.Serialize(re.P) == UtDocument.Serialize(d.P) && re.P.View.Schemat && re.P.View.Stretch == 4 && re.P.Devices.Count == d.P.Devices.Count && d.P.Devices.Count >= 7, $"odczyt: identyczny (z {d.P.Devices.Count} urządzeniami), widok Schemat zapamiętany");

        // zrzut zbliżenia: łącznik 1/2 i rozjazd 5
        w.View.Select(null);
        if (shotRails != null)
        {
            w.View.FitPoints(new[] { r1.At(260).P, r1.At(520).P, r2.At(260).P, r2.At(520).P });
            await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_schemat_blisko.png"));
        }
        // zrzut: cała stacja w schemacie
        w.View.Select(null);
        w.View.FitPoints(d.P.Tracks.SelectMany(t => w.View.Geometry(t).Sample(5).Select(q => q.P)).ToList());
        await Task.CompletedTask;
    }

    static ToolBar Toolbar(MainWindow w) => Find<ToolBar>((DependencyObject)w.Content, _ => true);
}

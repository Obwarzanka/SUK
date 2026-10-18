using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Test etapów 5 (część 2), 6 i 7 jak praca użytkownika: mijanka na skośnej linii w widoku Schemat (łącznik 1/2, bocznica z rozjazdem 3
/// i kozłem), liczniki osi klikami, nazwy odcinków narzędziem Odcinek, W4 / W5, semafor, peron i przejazd przeciągnięciem, kilometraż
/// dwoma punktami, profil z NMT GUGiK (sieć), „Sprawdź” do zera uwag, błędy celowe, warstwy, zapis i odczyt, wydruk w skali
/// (pomiar na obrazie kartki).
/// </summary>
public static partial class Test
{
    static async Task RunWyposazenie(MainWindow w, string shot)
    {
        var inv = CultureInfo.InvariantCulture;
        const double lineAz = 63;
        var dir = Path.Combine(Path.GetTempPath(), "edytor_ut_test", "wyposazenie"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        w.SetSchemat(false);
        w.NewDoc(); w.SaveDoc(false, Path.Combine(dir, "mijanka.ut.json"));
        var d = w.Doc;
        var o = Geo.ToPuwg92(51.7631, 19.5438); double k = Geo.ScaleFactor(o);
        XY Go(XY from, double azDeg, double len) => from + Element.Dir(azDeg * Math.PI / 180) * (len * k);
        Point S(XY p) => w.View.ToScreen(p);
        void Look(XY p, double mpp) => w.View.SetView(p, mpp);

        // --- mijanka: tor 1 (1000 m) klikami, tor 2 równoległy 5 m z panelu, Schemat
        w.SetTool(UtTool.Tor); w.View.Mode = UtView.TorMode.Prosta; w.NewRBox.Text = "300"; w.NewLBox.Text = "0";
        var a = Go(o, lineAz + 180, 500); var b = Go(o, lineAz, 500);
        Look(o, 2); w.View.SimClick(S(a)); w.View.SimClick(S(b)); w.View.SimClick(S(b), MouseButton.Right);
        var t1 = d.P.Tracks.Single(); w.View.Select(t1.Id);
        Type(Find<TextBox>(w.TrackPanel, _ => true), "1");
        Find<ComboBox>(w.TrackPanel, c => c.Items.Count == 3).SelectedIndex = 2;
        Type(Find<TextBox>(w.TrackPanel, x => x.Text == "4,75"), "5");
        Click(BtnIn(w.TrackPanel, "← po lewej"));
        var t2 = d.P.Tracks.First(x => x.Id != t1.Id); w.View.Select(t2.Id); Type(Find<TextBox>(w.TrackPanel, _ => true), "2");
        Find<ComboBox>(w.TrackPanel, c => c.Items.Count == 3).SelectedIndex = 1;
        w.SetTool(UtTool.Wskaz); w.View.Select(null); Click(w.SchematBtn);
        var r1 = w.View.Geometry(t1); var r2 = w.View.Geometry(t2);
        // łącznik 1/2 z toru 1 na tor 2 (km 380), rozjazd 3 na torze 2 na zewnątrz do bocznicy równoległej 5 m (km 700)
        w.SetTool(UtTool.Rozjazd); w.RzTypeBox.SelectedItem = "Rz 60E1-190-1:9";
        var p1 = r1.At(380).P; Look(Go(p1, lineAz, 40), 0.5);
        w.View.SimDrag(S(p1), S(r2.At(r2.Nearest(Go(p1, lineAz, 60)).S).P));
        var p3 = r2.At(700).P; Look(Go(p3, lineAz, 120), 1);
        w.View.SimDrag(S(p3), S(Go(p3, lineAz, 280) - Element.Right(r2.At(700).Az) * (5.1 * k)));
        var z3 = d.P.Turnouts.FirstOrDefault(x => x.Number == "3");
        var sid = z3 != null ? d.P.Tracks.First(x => x.Id == z3.Branch) : null;
        Ok(d.P.Turnouts.Count == 3 && z3 != null && sid.Vertices.Count == 4, "mijanka w schemacie: łącznik 1/2, rozjazd 3 do bocznicy równoległej");
        w.SetTool(UtTool.Wskaz); w.View.Select(sid.Id); Type(Find<TextBox>(w.TrackPanel, _ => true), "3");
        var rs = w.View.Geometry(sid);
        void Kind(string key) => w.DevKindBox.SelectedIndex = Array.FindIndex(UtDevice.Kinds, x => x.Key == key);
        Kind(UtDevice.Koziol); Look(rs.At(rs.Length).P, 0.4); w.View.SimClick(S(rs.At(rs.Length - 2).P));
        Ok(d.P.Devices.Any(x => x.Kind == UtDevice.Koziol && x.Track == sid.Id && Math.Abs(x.S - rs.Length) < 0.01), "kozioł na końcu bocznicy 3");

        // --- liczniki osi (granice odcinków) klikami
        Kind(UtDevice.LicznikOsi);
        void Click1(UtTrack t, double km) { var rr = w.View.Geometry(t); var pp = rr.At(km).P; Look(pp, 0.4); w.View.SimClick(S(pp)); }
        foreach (var km in new[] { 300.0, 520 }) Click1(t1, km);
        foreach (var km in new[] { 300.0, 520, 650, 800 }) Click1(t2, km);
        Click1(sid, 60);
        Ok(d.P.Devices.Count(x => x.Kind == UtDevice.LicznikOsi) == 7, "7 liczników osi wstawionych klikami (lo1–lo7)");
        var od = Odcinki.Compute(d.P);
        Ok(od.Count == 8 && Kontrola.Run(d.P).All(u => !u.Text.Contains("odcink")), $"podział na 8 kawałków między granicami; bez nazw odcinków – „Sprawdź” jeszcze ich nie wymaga ({od.Count})");

        // --- nazwy odcinków narzędziem Odcinek (podpowiedzi: numer toru, z + numery rozjazdów)
        w.SetTool(UtTool.Odcinek);
        Ok(w.View.Tool == UtTool.Odcinek && w.LastMessage.StartsWith("Odcinek:"), "narzędzie Odcinek (O) z opisem na pasku stanu");
        var asked = new List<string>(); w.AskNameForTest = def => { asked.Add(def); return null; };
        Click1(t1, 150); Click1(t1, 450); Click1(t1, 700); Click1(t2, 150); Click1(t2, 580); Click1(t2, 720); Click1(t2, 900); Click1(sid, rs.Length - 30);
        Ok(asked.SequenceEqual(new[] { "1", "z1/2", "1a", "2", "2a", "z3", "2b", "3" }), $"podpowiedzi nazw: {string.Join(", ", asked)}");
        Ok(d.P.Sections.Count == 8 && Odcinki.Check(d.P, Odcinki.Compute(d.P)).Count == 0, "8 odcinków nazwanych, każdy kawałek toru ma odcinek, bez uwag");
        int nsec = d.P.Sections.Count;
        Click1(t2, 440);
        Ok(d.P.Sections.Count == nsec && w.LastMessage.Contains("należy już do odcinka z1/2") && w.View.SelectedObj is (Kontrola.KOdcinek, _), "klik na torze 2 w obrębie łącznika – ten sam odcinek z1/2 (zaznaczony), bez nowego");
        w.AskNameForTest = null;
        od = Odcinki.Compute(d.P);
        var sz = d.P.Sections.First(x => x.Name == "z1/2"); int cz = od.CompOf[sz.Id];
        var link = d.P.Tracks.First(x => x.Id == d.P.Turnouts.First(z => z.Number == "1").Branch);
        double expLen = 220 + 220 + w.View.Geometry(link).Length;
        Ok(Math.Abs(od.Length(cz) - expLen) < 0.05 && od.Of(cz).Count() == 3, $"odcinek z1/2: tor 1 km 300–520, tor łączący, tor 2 km 300–520 = {od.Length(cz):0.00} m (wzór {expLen:0.00} m)");
        Ok(Find<TextBlock>(w.TrackPanel, x => x.Text.Contains("odcinek zwrotnicowy (rozjazdy 1, 2)")) != null, "panel odcinka: odcinek zwrotnicowy (rozjazdy 1, 2), granice, długość");
        Find<ComboBox>(w.TrackPanel, c => c.Items.Contains("obwód torowy")).SelectedIndex = 1;
        Ok(d.P.Sections.First(x => x.Name == "z1/2").Kind == UtSection.ObwodTorowy, "rodzaj kontroli niezajętości z panelu: obwód torowy");
        // usunięcie licznika między odcinkami 2a i z3 – odcinki się łączą, „Sprawdź” to zgłasza; Ctrl+Z
        var lo650 = d.P.Devices.First(x => x.Kind == UtDevice.LicznikOsi && x.Track == t2.Id && Math.Abs(x.S - 650) < 0.5);
        w.SetTool(UtTool.Wskaz); w.View.SelectDevice(lo650.Id); w.DeleteSelection();
        var ch = Kontrola.Run(d.P);
        Ok(ch.Any(u => u.Error && u.Text.Contains("odcinki 2a, z3 nie są rozdzielone")), "bez licznika km 650 – „odcinki 2a, z3 nie są rozdzielone”");
        w.DoUndo();
        Ok(Odcinki.Check(d.P, Odcinki.Compute(d.P)).Count == 0, "Ctrl+Z – licznik wrócił, odcinki rozdzielone");
        // przesunięcie nazwy odcinka myszą (we Wskaż) w obrębie kawałka
        var s1 = d.P.Sections.First(x => x.Name == "1"); var lp = w.View.Geometry(t1).At(150).P; Look(lp, 0.4);
        w.View.SimClick(S(lp)); // klik na torze – zaznacza tor, nie przesuwa
        Ok(w.View.SelectedTrack == t1.Id, "Wskaż: klik na osi toru zaznacza tor (nazwa odcinka leży pod torem)");

        // --- W4, W5, semafor A
        Kind(UtDevice.W4);
        void PlaceDir(UtTrack tr, double km, int dirr, int side)
        {
            var rr = w.View.Geometry(tr); var (pp, az) = rr.At(km); Look(pp, 0.4);
            var to = Go(pp, az * 180 / Math.PI + (dirr > 0 ? 0 : 180), 8) + Element.Right(az + (dirr > 0 ? 0 : Math.PI)) * (side * 1.5 * k);
            w.View.SimDrag(S(pp), S(to));
        }
        PlaceDir(t1, 600, 1, 1);
        Kind(UtDevice.W5); PlaceDir(t2, 100, -1, -1);
        Kind(UtDevice.Semafor); PlaceDir(t1, 60, 1, 1);
        var w4 = d.P.Devices.FirstOrDefault(x => x.Kind == UtDevice.W4); var w5 = d.P.Devices.FirstOrDefault(x => x.Kind == UtDevice.W5);
        Ok(w4 != null && w4.Track == t1.Id && w4.Dir == 1 && Math.Abs(w4.S - 600) < 0.3 && w5 != null && w5.Dir == -1 && w5.Side == -1, "W4 (km 600, w stronę rosnącego km) i W5 (tor 2, w drugą stronę, po lewej) przeciągnięciem");
        Ok(d.P.Devices.Any(x => x.Kind == UtDevice.Semafor && x.Name == "A"), "semafor A");
        w.View.SelectDevice(d.P.Devices.First(x => x.Name == "A").Id);
        Find<ComboBox>(w.TrackPanel, c => c.Items.Contains("wjazdowy")).SelectedIndex = 1;
        Ok(d.P.Devices.First(x => x.Name == "A").Funkcja == UtDevice.FWjazdowy, "semafor A – funkcja z panelu: wjazdowy");

        // --- peron przeciągnięciem wzdłuż toru 1 (km 400 → 600) po prawej
        w.SetTool(UtTool.Peron);
        var pa = r1.At(400).P; var pb = r1.At(600).P; Look(r1.At(500).P, 0.5);
        w.View.SimDrag(S(pa), S(pb + Element.Right(r1.At(600).Az) * (3 * k)));
        var pl = d.P.Platforms.FirstOrDefault();
        Ok(pl != null && Math.Abs(pl.S0 - 400) < 0.5 && Math.Abs(pl.S1 - 600) < 0.5 && pl.Side == 1 && pl.Name == "1" && w.View.SelectedObj is (Kontrola.KPeron, _),
            $"peron 1 przeciągnięciem: km {pl?.S0:0.0}–{pl?.S1:0.0}, po prawej toru 1, zaznaczony");
        Ok(Find<TextBlock>(w.TrackPanel, x => x.Text.StartsWith("Długość krawędzi 200,00 m")) != null, "panel peronu: długość krawędzi 200,00 m");
        Type(Find<TextBox>(w.TrackPanel, x => x.Text == "0,76"), "0,55");
        Ok(d.P.Platforms[0].Height == 0.55, "wysokość peronu 0,55 m z panelu");
        // przesunięcie peronu myszą we Wskaż o ok. 20 m
        w.SetTool(UtTool.Wskaz);
        var mid = Go(r1.At(500).P, lineAz + 90, 3.5); Look(mid, 0.5);
        Ok(w.View.HitObj(S(mid)) is (Kontrola.KPeron, _), "peron trafiany kliknięciem w obrys");
        w.View.SimDrag(S(mid), S(Go(mid, lineAz, 20)));
        Ok(Math.Abs(d.P.Platforms[0].S0 - 420) < 1 && Math.Abs(d.P.Platforms[0].Len - 200) < 1e-6 && d.UndoName == "Przesunięcie wzdłuż toru", $"peron przesunięty myszą wzdłuż toru: km {d.P.Platforms[0].S0:0.0}");
        w.DoUndo();

        // --- przejazd: klik na torze 1 (droga prostopadle), kategoria A, rogatki
        w.SetTool(UtTool.Przejazd); var pc = r1.At(880).P; Look(pc, 0.5); w.View.SimClick(S(pc));
        var lc = d.P.LevelCrossings.FirstOrDefault();
        Ok(lc != null && Math.Abs(lc.S - 880) < 0.5 && lc.Angle == 90 && w.View.SelectedObj is (Kontrola.KPrzejazd, _), "przejazd klikiem: km 880, droga prostopadle");
        Find<ComboBox>(w.TrackPanel, c => c.Items.Contains("kat. A")).SelectedIndex = 0;
        Find<ComboBox>(w.TrackPanel, c => c.Items.Contains("z napędem elektrycznym")).SelectedIndex = 2;
        Ok(d.P.LevelCrossings[0].Cat == "A" && d.P.LevelCrossings[0].Barriers == UtLevelCrossing.RogatkiElektr, "kategoria A, rogatki z napędem elektrycznym (panel)");
        // przejazd skośny przeciągnięciem wzdłuż osi drogi (60°) i usunięcie Del
        var pc2 = r1.At(950).P; Look(pc2, 0.5); w.View.SimDrag(S(pc2), S(pc2 + Element.Dir(r1.At(950).Az + 60 * Math.PI / 180) * (20 * k)));
        var lc2 = d.P.LevelCrossings.LastOrDefault();
        Ok(d.P.LevelCrossings.Count == 2 && Math.Abs(lc2.Angle - 60) <= 1 && Math.Abs(lc2.Reach - 20) <= 1, $"przejazd przeciągnięciem: kąt {lc2.Angle:0}°, droga {lc2.Reach:0} m");
        w.DeleteSelection();
        Ok(d.P.LevelCrossings.Count == 1, "Del – przejazd skośny usunięty");

        // --- kilometraż: punkt km 12.300 na początku toru 1 (przeciągnięcie w stronę rosnącego km) i km 13.300 na końcu
        w.SetTool(UtTool.Km);
        w.AskNameForTest = def => "17 12.300";
        var k0 = r1.At(0).P; Look(k0, 0.4); w.View.SimDrag(S(r1.At(2).P), S(Go(r1.At(2).P, lineAz, 10)));
        w.AskNameForTest = def => "17 13.320";
        var k1 = r1.At(r1.Length - 2).P; Look(k1, 0.4); w.View.SimDrag(S(k1), S(Go(k1, lineAz, -10)));
        w.AskNameForTest = null;
        var kp = d.P.KmPoints.OrderBy(x => x.S).ToList();
        Ok(kp.Count == 2 && kp[0].Line == "17" && Math.Abs(kp[0].Km - 12300) < 1e-6 && kp[0].Dir == 1 && kp[1].Dir == -1, "punkty km linii 17 przeciągnięciem (drugi przeciągnięty wstecz – kierunek −1)");
        Ok(Kilometraz.Check(d.P).Any(x => x.Text.Contains("nie rośnie")), "km rośnie zgodnie z torem, a punkt 2 wskazuje odwrotnie – uwaga");
        w.View.SelectObj(Kontrola.KKm, kp[1].Id); Click(BtnIn(w.TrackPanel, "Odwróć kierunek"));
        Ok(Kilometraz.Check(d.P).Any(x => x.Text.Contains("kilometr skrócony o 24")), $"km 13.320 na końcu: kilometr skrócony – uwaga ({string.Join("; ", Kilometraz.Check(d.P).Select(x => x.Text))})");
        Type(Find<TextBox>(w.TrackPanel, x => x.Text == "13.320"), "13.298");
        Ok(Kilometraz.Check(d.P).Count == 0, "km 13.298 z panelu – bez uwag");
        double kmMid = Kilometraz.OnTrack(d.P, t1.Id, "17", 500) ?? 0;
        var p2m = r2.At(r2.Nearest(r1.At(500).P).S).P;
        var proj = Kilometraz.At(d.P, p2m);
        Ok(Math.Abs(kmMid - 12799) < 0.6 && proj.Count == 1 && Math.Abs(proj[0].Km - kmMid) < 0.05, $"km toru 1 w połowie {Kilometraz.Fmt(kmMid)}, rzut z toru 2 {Kilometraz.Fmt(proj.FirstOrDefault().Km)}");
        Ok(Kilometraz.Text(d.P, t1.Id, d.P.LevelCrossings[0].S).StartsWith("km 13.180"), $"przejazd: {Kilometraz.Text(d.P, t1.Id, d.P.LevelCrossings[0].S)}");

        // --- profil podłużny: teren z NMT GUGiK, niweleta z terenu, punkty kontrolne
        w.SetTool(UtTool.Wskaz); w.View.Select(t1.Id);
        var pw = w.OpenProfile(false);
        Ok(pw != null && pw.TrackId == t1.Id && pw.Title.Contains("tor 1"), "okno profilu toru 1");
        bool nmt = await pw.LoadTerrain();
        var tt = d.P.Tracks.First(x => x.Id == t1.Id);
        if (!nmt) Ok(false, "NMT GUGiK niedostępny: " + pw.Status);
        else
        {
            Ok(tt.Terrain.Count >= 100 && tt.Terrain.All(q => q.Z > 100 && q.Z < 300), $"teren z NMT: {tt.Terrain.Count} punktów co 10 m, {tt.Terrain.Min(q => q.Z):0.0}–{tt.Terrain.Max(q => q.Z):0.0} m n.p.m.");
            pw.FromTerrain();
            var prof = tt.Profile; var secs = Profil.Sections(prof);
            double worst = tt.Terrain.Max(q => Math.Abs(Profil.Z(prof, q.S) - q.Z));
            Ok(prof.Count >= 2 && Math.Abs(prof[0].S) < 1e-6 && Math.Abs(prof[^1].S - w.View.Geometry(tt).Length) < 0.01 && secs.All(s => s.Len >= 100 - 1e-6) && worst < 2,
                $"niweleta z terenu: {prof.Count} załomów, odcinki ≥ 100 m, największa odchyłka od terenu {worst:0.00} m");
            // punkty kontrolne: wysokość z NMT w pojedynczym punkcie (GetHByXY) = teren profilu w tym km
            double maxd = 0; int nOk = 0;
            foreach (var km in new[] { 100.0, 500, 900 })
            {
                var q = w.View.Geometry(tt).At(km).P;
                try
                {
                    var txt = System.Text.Encoding.UTF8.GetString(await Pobieranie.Retry(() => Pobieranie.GetFresh(FormattableString.Invariant($"{Pobieranie.NmtService}?request=GetHByXY&x={q.X:0.##}&y={q.Y:0.##}"), CancellationToken.None), CancellationToken.None));
                    if (double.TryParse(txt.Trim(), NumberStyles.Float, inv, out var h)) { maxd = Math.Max(maxd, Math.Abs(h - Profil.Ground(tt.Terrain, km))); nOk++; }
                }
                catch { }
            }
            Ok(nOk == 3 && maxd < 0.3, $"punkty kontrolne km 100 / 500 / 900: NMT w punkcie = teren profilu (największa różnica {maxd:0.00} m)");
            Ok(Math.Abs(Profil.Z(prof, 0) - prof[0].Z) < 1e-9 && d.UndoName == "Niweleta z terenu", "Z niwelety na początku toru, zmiana z cofaniem");
            // załom przeciągnięty w pionie (+0,5 m) – pochylenia się zmieniają
            double zb = prof[1].Z; pw.MovePoint(1, zb + 0.5);
            Ok(Math.Abs(tt.Profile[1].Z - zb - 0.5) < 1e-9 && d.UndoName == "Wysokość załomu", "załom P2 podniesiony o 0,5 m (przeciągnięcie na wykresie)");
            w.DoUndo();
        }
        // łuk pionowy: niweleta 100 → 105 → 100 m na 500 m (±10 ‰), Rv 5000 – w załomie strzałka T²/(2Rv) = 0,25 m
        var vtest = new List<UtProfilePoint> { new() { S = 0, Z = 100 }, new() { S = 500, Z = 105, Rv = 5000 }, new() { S = 1000, Z = 100 } };
        Ok(Math.Abs(Profil.Z(vtest, 500) - 104.75) < 1e-9 && Math.Abs(Profil.Z(vtest, 449.99) - (100 + 0.01 * 449.99)) < 1e-3 && Math.Abs(Profil.Tangent(vtest, 1) - 50) < 1e-9,
            $"łuk pionowy Rv 5000 przy Δi = 20 ‰: styczna 50 m, w załomie {Profil.Z(vtest, 500):0.000} m (105 − 0,25)");
        if (shot != null)
        {
            pw.Left = SystemParameters.VirtualScreenLeft - 3200; pw.Top = SystemParameters.VirtualScreenTop; pw.ShowActivated = false; pw.ShowInTaskbar = false; pw.Width = 1100; pw.Height = 640;
            pw.Show(); await Task.Delay(300); await w.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Save(pw, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(shot))!, Path.GetFileNameWithoutExtension(shot) + "_profil.png"));
            pw.Close();
        }

        // --- „Sprawdź”: otwarte końce torów 1 i 2 → zaznaczone jako granica obszaru → bez uwag
        var c0 = w.RunCheck(true);
        Ok(c0.Count(u => u.Text.StartsWith("otwarty koniec toru")) == 4 && w.CheckExpander.IsExpanded, $"Sprawdź: 4 otwarte końce torów 1 i 2 ({c0.Count} uwag: {string.Join(" | ", c0.Select(u => u.Text))})");
        foreach (var t in new[] { t1, t2 })
        {
            w.View.Select(t.Id);
            foreach (var txt in new[] { "początek – granica obszaru", "koniec – granica obszaru" })
            { var cb = Find<CheckBox>(w.TrackPanel, x => x.Content as string == txt); cb.IsChecked = true; cb.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); }
        }
        var cOk = w.RunCheck(true);
        Ok(cOk.Count == 0 && (w.CheckExpander.Header as TextBlock)?.Text == "Kontrola układu – bez uwag", $"mijanka z semaforem, kozłem, odcinkami, peronem, przejazdem i km: „Sprawdź” bez uwag ({string.Join(" | ", cOk.Select(u => u.Text))})");
        // błędy celowe: rozjazd bez numeru, urządzenie poza torem, tor nakładający się na tor 1, odcinek bez granicy
        d.Change("test: błędy", p =>
        {
            p.Turnouts.First(z => z.Number == "3").Number = "";
            p.Devices.First(x => x.Kind == UtDevice.W4).S = 5000;
            p.Tracks.Add(new UtTrack { Number = "9", OpenStart = true, OpenEnd = true, Vertices = new() { new UtVertex { X = r1.At(100).P.X, Y = r1.At(100).P.Y }, new UtVertex { X = r1.At(200).P.X, Y = r1.At(200).P.Y } } });
        });
        var cBad = w.RunCheck(true);
        Ok(cBad.Any(u => u.Error && u.Text == "rozjazd bez numeru") && cBad.Any(u => u.Error && u.Text.Contains("W4") && u.Text.Contains("poza torem")) && cBad.Any(u => u.Error && u.Text.Contains("tory 1 i 9 nakładają się")),
            $"Sprawdź: rozjazd bez numeru, W4 poza torem, tory 1 i 9 nakładają się ({string.Join(" | ", cBad.Select(u => u.Text))})");
        var uw = cBad.First(u => u.Text == "rozjazd bez numeru");
        w.CheckList.SelectedIndex = cBad.IndexOf(uw);
        Ok(w.View.SelectedTurnout == d.P.Turnouts.First(z => z.Number == "").Id && XY.Dist(w.View.Center, uw.At.Value) < 1e-6, "klik na uwadze: widok w miejscu rozjazdu, rozjazd zaznaczony");
        w.DoUndo();
        Ok(w.RunCheck(false).Count == 0, "Ctrl+Z – znowu bez uwag");

        // --- warstwy: ukrycie odcinków i peronów (ekran i wydruk), zapis z widokiem
        w.LayerExpander.IsExpanded = true;
        var lb = Find<CheckBox>((DependencyObject)w.LayerExpander.Content, x => x.Content as string == "peron");
        lb.IsChecked = false; lb.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Look(mid, 0.5);
        Ok(!w.View.Shown("peron") && w.View.HitObj(S(Go(r1.At(500).P, lineAz + 90, 3.5))) == null, "warstwa „peron” ukryta – peronu nie widać i nie da się go kliknąć");
        lb.IsChecked = true; lb.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        w.SetLayer("odcinki", false);
        Ok(w.SaveDoc(false), "zapis projektu");
        var l = UtDocument.Load(d.Path);
        Ok(l.P.Sections.Count == 8 && l.P.Platforms.Count == 1 && l.P.LevelCrossings.Count == 1 && l.P.KmPoints.Count == 2 && l.P.Tracks.First(x => x.Id == t1.Id).Profile.Count == tt.Profile.Count
            && l.P.Tracks.First(x => x.Id == t1.Id).OpenStart && l.P.View.Hidden?.SequenceEqual(new[] { "odcinki" }) == true && UtDocument.Serialize(l.P) == UtDocument.Serialize(d.P),
            "zapis i odczyt: odcinki, peron, przejazd, km, profil, granice obszaru, ukryte warstwy – projekt identyczny");
        w.SetLayer("odcinki", true);

        // --- zrzuty: stacja w schemacie (odcinki, peron, przejazd, km), z bliska łącznik z odcinkami
        if (shot != null)
        {
            w.SetTool(UtTool.Wskaz); w.View.Select(null);
            w.View.FitPoints(Eksport.Extent(d.P)); await Settle(w);
            Save(w, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(shot))!, Path.GetFileNameWithoutExtension(shot) + "_wyposazenie.png"));
            w.View.SelectObj(Kontrola.KOdcinek, sz.Id); Look(Go(r1.At(420).P, lineAz + 90, 2.5), 0.25); await Settle(w);
            Save(w, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(shot))!, Path.GetFileNameWithoutExtension(shot) + "_odcinki.png"));
            w.View.SelectObj(Kontrola.KPeron, d.P.Platforms[0].Id); Look(Go(r1.At(560).P, lineAz + 90, 2.5), 0.25); await Settle(w);
            Save(w, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(shot))!, Path.GetFileNameWithoutExtension(shot) + "_peron_w4.png"));
            w.View.Select(null);
        }

        // --- wydruk: schemat Ig-10 1:2000 / 1:500 do PDF na A3 – kartki, wymiar strony
        var pdf = Path.Combine(dir, "mijanka.pdf");
        int npg = w.ExportTo(pdf, w.ExportOptions(true, 2000, 150, "A3 poziomo", true));
        var pdfTxt = File.ReadAllText(pdf, System.Text.Encoding.Latin1);
        var boxes = Regex.Matches(pdfTxt, @"/MediaBox \[0 0 ([\d.]+) ([\d.]+)\]");
        Ok(npg >= 1 && boxes.Count == npg && boxes.Cast<Match>().All(m => Math.Abs(double.Parse(m.Groups[1].Value, inv) - 420 / 25.4 * 72) < 0.01 && Math.Abs(double.Parse(m.Groups[2].Value, inv) - 297 / 25.4 * 72) < 0.01)
            && pdfTxt.StartsWith("%PDF-1.4") && pdfTxt.TrimEnd().EndsWith("%%EOF"),
            $"PDF: {npg} kartek A3 (1:2000 wzdłuż – 1000 m toru = 500 mm), strona 420 × 297 mm = {(boxes.Count > 0 ? boxes[0].Groups[1].Value + " × " + boxes[0].Groups[2].Value : "?")} pt");
        var pg = w.LastExport[0];
        Ok(Math.Abs(pg.Image.PixelWidth / 150.0 * 25.4 - 420) < 0.2 && Math.Abs(pg.Image.DpiX - 150) < 1e-6, $"obraz kartki {pg.Image.PixelWidth} × {pg.Image.PixelHeight} px przy 150 dpi = 420 mm");
        // skala na kartce schematu: odległość osi torów 1 i 2 (5 m) w poprzek 1:500 = 10 mm, 100 m wzdłuż 1:2000 = 50 mm
        double pxmm = 150 / 25.4;
        var all = w.LastExport; Eksport.Strona pgm = null; foreach (var x in all) { var q = x.ToPx(r1.At(500).P); if (q.X > 0 && q.Y > 0 && q.X < x.Image.PixelWidth && q.Y < x.Image.PixelHeight) pgm = x; }
        if (pgm != null)
        {
            var q1 = pgm.ToPx(r1.At(500).P); var q2 = pgm.ToPx(r2.At(r2.Nearest(r1.At(500).P).S).P); var q3 = pgm.ToPx(r1.At(600).P);
            Ok(Math.Abs((q1 - q2).Length / pxmm - 5 * k / 0.5) < 0.05 && Math.Abs((q3 - q1).Length / pxmm - 100 * k / 2) < 0.05,
                $"kartka schematu: międzytorze {(q1 - q2).Length / pxmm:0.00} mm (5 m w 1:500), 100 m toru {(q3 - q1).Length / pxmm:0.00} mm (1:2000)");
            var pg4 = all.First(x => x.ToPx(r1.At(250).P) is var q && q.X > 0 && q.Y > 0 && q.X < x.Image.PixelWidth && q.Y < x.Image.PixelHeight);
            var q4 = pg4.ToPx(r1.At(250).P); var q5 = pg4.ToPx(r2.At(r2.Nearest(r1.At(250).P).S).P);
            Ok(Dark(pg4.Image, q4) && !Dark(pg4.Image, q4 + (q4 - q5) * 0.5), "na kartce oś toru 1 tam, gdzie wynika ze skali (ciemny piksel), 5 mm obok – biało");
        }
        else Ok(false, "kartka z połową toru 1 nie znaleziona");

        // --- wydruk w skali 1:1000 (Teren): tor 100 m wschód–zachód zmierzony na obrazie kartki = 100 mm · skala odwzorowania
        w.SetSchemat(false);
        w.NewDoc();
        var e0 = Go(o, 90, -50); var e1 = Go(o, 90, 50);
        w.SetTool(UtTool.Tor); w.View.Mode = UtView.TorMode.Prosta; Look(o, 0.2); w.View.SimClick(S(e0)); w.View.SimClick(S(e1)); w.View.SimClick(S(e1), MouseButton.Right);
        var tm = w.Doc.P.Tracks.Single(); var rm = w.View.Geometry(tm);
        var png = Path.Combine(dir, "pomiar_1000.png");
        w.ExportTo(png, w.ExportOptions(false, 1000, 200, Eksport.Formats[0].Name, true));
        var img = w.LastExport[0].Image; var cy = w.LastExport[0].ToPx(o);
        int row = (int)Math.Round(cy.Y), x0 = (int)Math.Round(cy.X), x1 = x0;
        while (x0 > 0 && Dark(img, new Point(x0 - 1, row))) x0--;          // od środka toru w obie strony – do końca ciemnej linii (ramka kartki dalej)
        while (x1 < img.PixelWidth - 1 && Dark(img, new Point(x1 + 1, row))) x1++;
        double mm = (x1 - x0 + 1) / 200.0 * 25.4, want = rm.LenGrid / 1000 * 1000;
        Ok(x0 > 0 && Math.Abs(mm - want) < 0.3, $"wydruk 1:1000: tor {rm.Length:0.00} m (w układzie {rm.LenGrid:0.000} m) zmierzony na kartce = {mm:0.00} mm (wzór {want:0.00} mm)");
        var fi = BitmapDecoder.Create(new Uri(png), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        Ok(Math.Abs(fi.DpiX - 200) < 0.5, $"PNG z rozdzielczością w pliku: {fi.DpiX:0} dpi – program graficzny drukuje w skali");
        if (shot != null) File.Copy(png, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(shot))!, Path.GetFileNameWithoutExtension(shot) + "_wydruk_1000.png"), true);
        if (shot != null) Eksport.SavePng(new() { pg }, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(shot))!, Path.GetFileNameWithoutExtension(shot) + "_wydruk_schemat.png"));
    }

    /// <summary>Piksel ciemny (oś toru, znak) na obrazie kartki.</summary>
    static bool Dark(BitmapSource img, Point p)
    {
        int x = (int)Math.Round(p.X), y = (int)Math.Round(p.Y);
        if (x < 0 || y < 0 || x >= img.PixelWidth || y >= img.PixelHeight) return false;
        var px = new byte[4]; img.CopyPixels(new Int32Rect(x, y, 1, 1), px, 4, 0);
        return px[0] + px[1] + px[2] < 3 * 128;
    }
}

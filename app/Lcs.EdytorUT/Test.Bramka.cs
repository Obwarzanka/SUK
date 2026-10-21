using System.IO;
using System.Windows;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Test bramki sygnałowej jak na planie schematycznym (Semaforek, Łódź Widzew – L3 / L5 przy peronie 2): dwa tory 5 m od siebie,
/// semafory L3 i L5 „na bramce” w tym samym miejscu → jedna bramka (belka przez oba tory, dwa słupy ze stopą), semafory bez własnej
/// stopy; trzeci semafor na bramce 100 m dalej – osobna bramka; zwykły semafor na maszcie – bez bramki.
/// </summary>
public static partial class Test
{
    static void CollectLogical(DependencyObject o, List<System.Windows.Controls.ComboBox> res)
    {
        if (o is System.Windows.Controls.ComboBox cb && (cb.ToolTip as string)?.StartsWith("Światło:") == true) res.Add(cb);
        foreach (var ch in LogicalTreeHelper.GetChildren(o).OfType<DependencyObject>()) CollectLogical(ch, res);
    }

    static async Task RunBramka(MainWindow w)
    {
        const double lineAz = 90;
        var dir = Path.Combine(Path.GetTempPath(), "edytor_ut_test", "bramka"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        w.SetSchemat(false);
        w.NewDoc(); w.SaveDoc(false, Path.Combine(dir, "stacja.ut.json"));
        var d = w.Doc;
        var o = Geo.ToPuwg92(51.7631, 19.5438); double k = Geo.ScaleFactor(o);
        XY Go(XY from, double azDeg, double len) => from + Element.Dir(azDeg * Math.PI / 180) * (len * k);
        var a = Go(o, lineAz + 180, 300); var b = Go(o, lineAz, 300);
        var t3 = new UtTrack { Number = "3", Vertices = new() { new() { X = a.X, Y = a.Y }, new() { X = b.X, Y = b.Y } } };
        var a5 = Go(a, lineAz + 90, 5); var b5 = Go(b, lineAz + 90, 5);
        var t5 = new UtTrack { Number = "5", Vertices = new() { new() { X = a5.X, Y = a5.Y }, new() { X = b5.X, Y = b5.Y } } };
        // L3, L5: dla jazdy w stronę malejącego km (w lewo), po lewej stronie toru (patrząc w kierunku jazdy), na bramce w km 300
        UtDevice Sig(string n, UtTrack t, double s, string mast) => new() { Kind = UtDevice.Semafor, Name = n, Track = t.Id, S = s, Dir = -1, Side = -1, Mast = mast, W24 = true, StripeP = true, Funkcja = UtDevice.FWyjazdowy };
        var l3 = Sig("L3", t3, 300, UtDevice.MastBramka); var l5 = Sig("L5", t5, 300.5, UtDevice.MastBramka);
        var l7 = Sig("L7", t3, 400, UtDevice.MastBramka); var m4 = Sig("M4", t5, 200, UtDevice.MastProsty);
        d.Change("Bramka", p => { p.Tracks.Add(t3); p.Tracks.Add(t5); p.Devices.AddRange(new[] { l3, l5, l7, m4 }); });
        var groups = w.View.GantryGroups();
        Ok(groups.Count == 2 && groups.Any(g => g.Count == 2 && g.Any(x => x.Name == "L3") && g.Any(x => x.Name == "L5")) && groups.Any(g => g.Count == 1 && g[0].Name == "L7"),
            $"bramki: L3 + L5 w tym samym miejscu – jedna bramka, L7 100 m dalej – osobna, M4 na maszcie – bez bramki ({string.Join(" | ", groups.Select(g => string.Join("+", g.Select(x => x.Name))))})");
        Ok(Urzadzenia.Check(d.P, l3).Count == 0 && Urzadzenia.Check(d.P, l5).Count == 0, "semafory na bramce bez uwag");
        // światło komory: stałe / stałe lub pulsujące (2 kreski) / tylko pulsujące (4 kreski)
        var lp = UtDevice.ParseLights("B**PCZ*");
        Ok(lp.Select(x => x.Pulse).SequenceEqual(new[] { UtDevice.TylkoPulsujace, UtDevice.Stale, UtDevice.Stale, UtDevice.StaleLubPulsujace }) && UtDevice.FormatLights(lp) == "B**PCZ*",
            "zapis komór „B**PCZ*”: biała tylko pulsująca, pomarańczowa i czerwona stałe, zielona stała lub pulsująca – i z powrotem ten sam zapis");
        var l2 = new UtDevice { Kind = UtDevice.Semafor, Name = "L2", Track = t3.Id, S = 150, Dir = -1, Side = -1, Lights = "Z*PCP*B**", W24 = true, StripeP = true };
        d.Change("L2", p => p.Devices.Add(l2));
        w.SetTool(UtTool.Wskaz); w.View.SelectDevice(l2.Id);
        var combos = Find<System.Windows.Controls.GroupBox>(w.TrackPanel, _ => true);
        var pulseBoxes = new List<System.Windows.Controls.ComboBox>();
        void Collect(DependencyObject o) { for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(o); i++) { var ch = System.Windows.Media.VisualTreeHelper.GetChild(o, i); if (ch is System.Windows.Controls.ComboBox cb && (cb.ToolTip as string)?.StartsWith("Światło:") == true) pulseBoxes.Add(cb); Collect(ch); } }
        w.TrackPanel.UpdateLayout(); Collect(w.TrackPanel);
        if (pulseBoxes.Count == 0) foreach (var g in LogicalTreeHelper.GetChildren(w.TrackPanel).OfType<DependencyObject>()) CollectLogical(g, pulseBoxes);
        Ok(pulseBoxes.Count == 5 && pulseBoxes[1].SelectedIndex == 0 && pulseBoxes[0].SelectedIndex == 1 && pulseBoxes[4].SelectedIndex == 2, $"panel: dla każdej komory lista „stałe / stałe lub pulsujące / tylko pulsujące” ({pulseBoxes.Count})");
        if (pulseBoxes.Count == 5) { pulseBoxes[2].SelectedIndex = 2; }
        Ok(d.P.Devices.First(x => x.Id == l2.Id).Lights == "Z*PC**P*B**", $"zmiana w panelu: czerwona tylko pulsująca → zapis {d.P.Devices.First(x => x.Id == l2.Id).Lights}");
        if (shotRails != null) { w.View.Select(null); w.SetSchemat(true); var pl2 = Urzadzenia.Pos(d.P, d.P.Devices.First(x => x.Id == l2.Id)).Value.P; w.View.SetView(Go(pl2, lineAz + 180, 12), 0.25); await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_swiatla_pulsujace.png")); w.SetSchemat(false); }
        // licznik osi: po tej stronie toru, po której kliknięto
        w.SetSchemat(false); w.SetTool(UtTool.Urzadzenie);
        w.DevKindBox.SelectedIndex = Array.FindIndex(UtDevice.Kinds, x => x.Key == UtDevice.LicznikOsi);
        var r3 = w.View.Geometry(d.P.Tracks.First(x => x.Id == t3.Id));
        foreach (var (km, off, want) in new[] { (100.0, 0.35, 1), (120.0, -0.35, -1) })
        {
            var (pp, azz) = r3.At(km); w.View.SetView(pp, 0.1);
            int n0 = d.P.Devices.Count;
            w.View.SimClick(w.View.ToScreen(pp + Element.Right(azz) * (off * k)));
            var lo = d.P.Devices.Count > n0 ? d.P.Devices[^1] : null;
            Ok(lo != null && lo.Kind == UtDevice.LicznikOsi && lo.Side == want, $"licznik osi kliknięty {(off > 0 ? "po prawej" : "po lewej")} stronie toru – strona {lo?.Side}");
        }
        // licznik osi w miejscu semafora L2, po tej samej stronie (zrzut właściciela) – kropki nie wchodzą na komory, nazwa po drugiej stronie
        var loL2 = new UtDevice { Kind = UtDevice.LicznikOsi, Name = "lo9", Track = t3.Id, S = 148, Dir = 1, Side = 1 };
        d.Change("lo9", p => p.Devices.Add(loL2));
        if (shotRails != null)
        {
            w.SetTool(UtTool.Wskaz); w.View.Select(null); w.SetSchemat(false);
            var pl = Urzadzenia.Pos(d.P, loL2).Value.P; w.View.SetView(Go(pl, lineAz + 180, 3), 0.025); await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_licznik_przy_semaforze.png"));
        }
        w.SetTool(UtTool.Wskaz); w.View.Select(null); w.SetSchemat(true);
        var c = Go(o, lineAz, 0); w.View.SetView(Go(c, lineAz + 90, 2.5), 0.12);
        if (shotRails != null) { await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_bramka_widzew.png")); }
        w.SetSchemat(false);

        // łączenie bramek: L9 na bramce 20 m od L7 (osobne bramki) – „Połącz z…” w panelu L7 → jedna bramka, L9 przesunięty w km 400
        var l9 = Sig("L9", t5, 380, UtDevice.MastBramka);
        d.Change("L9", p => p.Devices.Add(l9));
        Ok(w.View.GantryGroups().Count(g => g.Any(x => x.Name is "L7" or "L9")) == 2, "L7 i L9 20 m od siebie – dwie osobne bramki");
        Ok(Urzadzenia.DoPolaczenia(d.P, l7).Any(x => x.Id == l9.Id) && !Urzadzenia.DoPolaczenia(d.P, l7).Any(x => x.Id == m4.Id), "do połączenia z L7: L9 (20 m), bez M4 (200 m)");
        w.SetTool(UtTool.Wskaz); w.View.SelectDevice(l7.Id);
        var join = Find<System.Windows.Controls.ComboBox>(w.TrackPanel, x => (x.ToolTip as string)?.StartsWith("Sygnalizator w pobliżu") == true);
        int ji = join == null ? -1 : join.Items.Cast<object>().ToList().FindIndex(x => (x as string)?.Contains("L9") == true);
        if (ji > 0) join.SelectedIndex = ji;
        var D = (string id) => d.P.Devices.First(x => x.Id == id);
        var gj = w.View.GantryGroups().FirstOrDefault(g => g.Any(x => x.Id == l7.Id));
        Ok(ji > 0 && gj != null && gj.Count == 2 && gj.Any(x => x.Id == l9.Id) && Math.Abs(D(l9.Id).S - 400) < 0.2 && D(l7.Id).Konstrukcja == D(l9.Id).Konstrukcja,
            $"panel L7 „Połącz z…” L9 – jedna bramka, L9 w km {D(l9.Id).S:0.0}");
        Ok(Urzadzenia.Check(d.P, D(l9.Id)).Count == 0, "L9 na wspólnej bramce bez uwag");
        d.Change("L9 dalej", p => p.Devices.First(x => x.Id == l9.Id).S = 370);
        Ok(Urzadzenia.Check(d.P, D(l9.Id)).Any(x => x.Contains("ponad 8 m")), "L9 odsunięty o 30 m – uwaga „ponad 8 m”");
        w.DoUndo();
        // odłączenie L5 od L3 (stoją w tym samym miejscu – bez klucza bramka byłaby wspólna)
        d.Change("Odłącz L5", p => Urzadzenia.Odlacz(p.Devices.First(x => x.Id == l5.Id)));
        Ok(w.View.GantryGroups().Any(g => g.Count == 1 && g[0].Id == l3.Id) && w.View.GantryGroups().Any(g => g.Count == 1 && g[0].Id == l5.Id), "„Odłącz” L5 – L3 i L5 na osobnych bramkach");
        w.DoUndo();
        Ok(w.View.GantryGroups().Any(g => g.Count == 2 && g.Any(x => x.Id == l3.Id) && g.Any(x => x.Id == l5.Id)), "Ctrl+Z – L3 i L5 znowu na jednej bramce");

        // wysięgnik wspólny: W1 (tor 3) i W2 (tor 5) na wysięgnikach – osobne maszty; połączone – jeden słup i belka nad oboma torami
        var w1 = Sig("W1", t3, 500, UtDevice.MastWysiegnik); var w2 = Sig("W2", t5, 512, UtDevice.MastWysiegnik);
        w1.Side = w2.Side = 1; w1.StripeP = w2.StripeP = false;
        d.Change("W1 W2", p => p.Devices.AddRange(new[] { w1, w2 }));
        Ok(!Urzadzenia.Konstrukcje(d.P).Any(x => x.M.Any(q => q.Id == w1.Id || q.Id == w2.Id)), "W1 i W2 na wysięgnikach – każdy na własnym maszcie");
        w.JoinStructure(w1.Id, w2.Id);
        var kw = Urzadzenia.KonstrukcjaOf(d.P, w1.Id);
        Ok(kw is { Mast: UtDevice.MastWysiegnik } && kw.Value.M.Count == 2 && Math.Abs(D(w2.Id).S - 500) < 0.2 && w.View.GantryGroups().All(g => g.All(x => x.Id != w1.Id)),
            $"W1 + W2 – wspólny wysięgnik (nie bramka), W2 w km {D(w2.Id).S:0.0}");
        var menu = w.BuildDeviceMenu(w1.Id).Items.OfType<System.Windows.Controls.MenuItem>().FirstOrDefault(x => (x.Header as string)?.StartsWith("Odłącz od wspólnej") == true);
        Ok(menu != null && menu.IsEnabled, "menu PPM: „Odłącz od wspólnej konstrukcji”");
        // zmiana mocowania W2 na bramkę – zmienia całą konstrukcję; na maszt – odłącza
        w.View.SelectDevice(w2.Id);
        var rbB = Find<System.Windows.Controls.RadioButton>(w.TrackPanel, x => x.Content as string == "na bramce");
        rbB?.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Ok(D(w1.Id).MastOrDefault == UtDevice.MastBramka && D(w2.Id).MastOrDefault == UtDevice.MastBramka, "mocowanie „na bramce” w W2 – cała konstrukcja (W1 i W2) na bramce");
        w.DoUndo();
        if (shotRails != null)
        {
            w.View.Select(null); w.SetSchemat(true);
            var pw = Urzadzenia.Pos(d.P, D(w1.Id)).Value.P; w.View.SetView(Go(pw, lineAz + 90, 2.5), 0.12); await Settle(w);
            Save(w, shotRails.Replace("_szyny.png", "_wysiegnik_wspolny.png"));
            w.SetSchemat(false);
        }

        // bramka z jednym semaforem nad dwoma torami: S5 na torze 3, belka także nad torem 5 (panel), dwa słupy → jeden po lewej
        var s5 = Sig("S5", t3, 560, UtDevice.MastBramka); s5.StripeP = false;
        d.Change("S5", p => p.Devices.Add(s5));
        w.View.SelectDevice(s5.Id);
        var cbT5 = Find<System.Windows.Controls.CheckBox>(w.TrackPanel, x => (x.Content as string)?.StartsWith("tor 5") == true);
        if (cbT5 != null) { cbT5.IsChecked = true; cbT5.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); }
        Ok(cbT5 != null && D(s5.Id).NadTorami?.SequenceEqual(new[] { t5.Id }) == true, "panel S5: „Belka także nad torem” – tor 5 (5 m)");
        var st = Urzadzenia.Ustawienia(UtDevice.MastBramka, new() { D(s5.Id) });
        Ok(st.Slupy == 2 && st.NadTorami.Count == 1, "S5: bramka nad torami 3 i 5, dwa słupy");
        if (shotRails != null)
        {
            w.View.Select(null); w.SetSchemat(true);
            var ps = Urzadzenia.Pos(d.P, D(s5.Id)).Value.P; w.View.SetView(Go(ps, lineAz + 90, 2.5), 0.12); await Settle(w);
            Save(w, shotRails.Replace("_szyny.png", "_bramka_dwa_tory.png"));
            w.SetSchemat(false); w.View.SelectDevice(s5.Id);
        }
        var rb1 = Find<System.Windows.Controls.RadioButton>(w.TrackPanel, x => x.Content as string == "jeden po lewej");
        rb1?.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Ok(D(s5.Id).Slupy == 1 && D(s5.Id).SlupStrona == -1 && D(s5.Id).MastOrDefault == UtDevice.MastBramka, "S5: „jeden po lewej” – bramka na jednym słupie");
        // dołączenie semafora z toru 5 przejmuje ustawienia, tor 5 nie jest już „bez sygnalizatora”
        var s6 = Sig("S6", t5, 565, UtDevice.MastProsty); s6.StripeP = false;
        d.Change("S6", p => p.Devices.Add(s6));
        w.JoinStructure(s5.Id, s6.Id);
        var st2 = Urzadzenia.Ustawienia(UtDevice.MastBramka, Urzadzenia.KonstrukcjaOf(d.P, s5.Id)?.M ?? new());
        Ok(D(s6.Id).Slupy == 1 && D(s6.Id).SlupStrona == -1 && st2.NadTorami.Count == 0 && Math.Abs(D(s6.Id).S - 560) < 0.2, $"S6 dołączony: jeden słup po lewej, tor 5 ma już semafor (słupy {D(s6.Id).Slupy}, strona {D(s6.Id).SlupStrona}, nad torami {st2.NadTorami.Count}, km {D(s6.Id).S:0.0}, klucz {D(s5.Id).Konstrukcja}/{D(s6.Id).Konstrukcja})");
        if (shotRails != null)
        {
            w.View.Select(null); w.SetSchemat(true);
            var ps = Urzadzenia.Pos(d.P, D(s5.Id)).Value.P; w.View.SetView(Go(ps, lineAz + 90, 2.5), 0.12); await Settle(w);
            Save(w, shotRails.Replace("_szyny.png", "_bramka_jeden_slup.png"));
            w.SetSchemat(false);
        }

        // pasy świetlne na bramce (uwaga właściciela 2026-10-20: „pas wydłuża konstrukcję głupio i się źle renderuje”, „pas świetlny nie wygląda jak on”):
        // długość słupów nie zależy od pasów i W21; pas – wąski prostokąt obrócony z torem (BSK ss5); tory skośne (azymut 70°) jak na zdjęciu właściciela
        var zP = D(l3.Id).Copy(); zP.StripeP = zP.StripeZ = true; zP.W21 = "6";
        var bezP = D(l3.Id).Copy(); bezP.StripeP = bezP.StripeZ = false; bezP.W21 = null;
        Ok(UtView.HangingLen(zP) == UtView.HangingLen(bezP), $"bramka: pasy i W21 nie wydłużają słupów ({UtView.HangingLen(zP)} = {UtView.HangingLen(bezP)} mm)");
        var o2 = Go(o, 0, 2000); XY G2(XY from, double az, double len) => from + Element.Dir(az * Math.PI / 180) * (len * k);
        var t1s = new UtTrack { Number = "11", Vertices = new() { new() { X = G2(o2, 250, 200).X, Y = G2(o2, 250, 200).Y }, new() { X = G2(o2, 70, 200).X, Y = G2(o2, 70, 200).Y } } };
        var o3 = G2(o2, 160, 4.5);
        var t2s = new UtTrack { Number = "13", Vertices = new() { new() { X = G2(o3, 250, 200).X, Y = G2(o3, 250, 200).Y }, new() { X = G2(o3, 70, 200).X, Y = G2(o3, 70, 200).Y } } };
        UtDevice SigS(string n, UtTrack t, bool pas) => new() { Kind = UtDevice.Semafor, Name = n, Track = t.Id, S = 200, Dir = -1, Side = 1, Mast = UtDevice.MastBramka, Lights = "Z*P*CPB**", StripeP = pas, Funkcja = UtDevice.FWyjazdowy };
        var sD = SigS("D", t1s, true); var sE = SigS("E", t2s, true);
        d.Change("Bramka skośna", p => { p.Tracks.Add(t1s); p.Tracks.Add(t2s); p.Devices.AddRange(new[] { sD, sE }); });
        Ok(w.View.GantryGroups().Any(g => g.Count == 2 && g.Any(x => x.Name == "D") && g.Any(x => x.Name == "E")), "bramka skośna: D i E na jednej bramce, oba z pasem pomarańczowym");
        if (shotRails != null)
        {
            w.View.Select(null); w.SetSchemat(false);
            w.View.SetView(G2(o2, 160, 2.25), 0.05); await Settle(w);
            Save(w, shotRails.Replace("_szyny.png", "_bramka_pasy_skos.png"));
            d.Change("Pasy zielone", p => { foreach (var x in p.Devices.Where(x => x.Id == sD.Id || x.Id == sE.Id)) x.StripeZ = true; });
            await Settle(w);
            Save(w, shotRails.Replace("_szyny.png", "_bramka_pasy_skos_PZ.png"));
        }

        // bramka w jednym przekroju (zgłoszenie właściciela 2026-10-21, B1 / B102 / B101 schodkami co ~5 m – belka ukośna):
        // przesunięcie jednego semafora ciągnie pozostałe; bramka już przekrzywiona – „Ustaw bramkę w przekroju”
        w.JoinStructure(sD.Id, sE.Id);
        double Skos() => Urzadzenia.Przekrzywienie(d.P, D(sD.Id));
        Ok(Urzadzenia.KonstrukcjaOf(d.P, sD.Id) is { M.Count: 2 } && Skos() < 0.05, $"D i E połączone – jeden przekrój ({Skos() * 100:0.#} cm)");
        double sE0 = D(sE.Id).S;
        w.MoveDevice(sD.Id, D(sD.Id).S - 10, "Przesunięcie urządzenia");
        Ok(Math.Abs(D(sE.Id).S - sE0) > 9 && Skos() < 0.05 && w.LastMessage.Contains("w całości"), $"D przesunięty o 10 m – E za nim, bramka dalej w jednym przekroju ({Skos() * 100:0.#} cm; {w.LastMessage})");
        d.Change("Krzywo", p => p.Devices.First(x => x.Id == sE.Id).S += 6);
        w.SetTool(UtTool.Wskaz); w.View.SelectDevice(sD.Id);
        var ub = Find<System.Windows.Controls.Button>(w.TrackPanel, b => (b.Content as string) == "Ustaw bramkę w przekroju D");
        Ok(ub != null, $"bramka przekrzywiona o {Skos():0.##} m – w panelu D „Ustaw bramkę w przekroju D”");
        if (ub != null) { Click(ub); Ok(Skos() < 0.05 && d.UndoName == "Bramka w jednym przekroju", $"po kliknięciu – jeden przekrój ({Skos() * 100:0.#} cm)"); }

        // przesunięcie samej belki (właściciel 2026-10-21: „przesuwanie bramki bez przesuwania semaforów”): przeciągnięcie belki – semafory w swoim km
        double sD0 = D(sD.Id).S, sE1 = D(sE.Id).S;
        var (pD, azD, kD) = Urzadzenia.Pos(d.P, D(sD.Id)).Value; w.View.SetView(pD, 0.05); await Settle(w);
        var beam = w.View.GantryBeams().FirstOrDefault(x => x.Id == sD.Id || x.Id == sE.Id);
        Ok(beam.Id != null, "belka bramki D/E na planie");
        if (beam.Id != null)
        {
            // chwyt na belce poza znakami semaforów (koniec od strony bez komór), przeciągnięcie o 6 m w stronę jazdy D
            var along = w.View.ToScreen(pD + Element.Dir(azD) * (6 * kD)) - w.View.ToScreen(pD);
            bool done = false;
            foreach (double t in new[] { 0.04, 0.96, 0.5 })
            {
                var g = beam.A + (beam.B - beam.A) * t;
                w.View.SimDrag(g, g + along);
                if (Math.Abs(Urzadzenia.BelkaOff(Urzadzenia.KonstrukcjaOf(d.P, sD.Id).Value.M) - 6) < 0.2) { done = true; break; }
                if (Math.Abs(D(sD.Id).S - sD0) > 0.01) w.DoUndo();     // chwycony semafor zamiast belki – cofnij i spróbuj dalej
            }
            var mm = Urzadzenia.KonstrukcjaOf(d.P, sD.Id).Value.M;
            Ok(done && Math.Abs(D(sD.Id).S - sD0) < 1e-9 && Math.Abs(D(sE.Id).S - sE1) < 1e-9 && d.UndoName == "Przesunięcie belki" && mm.All(x => Math.Abs((x.BelkaOff ?? 0) - Urzadzenia.BelkaOff(mm)) < 1e-9),
                $"belka przeciągnięta o {Urzadzenia.BelkaOff(mm):0.0} m, semafory D i E w swoim km ({w.LastMessage})");
            var nb = w.View.GantryBeams().First(x => x.Id == beam.Id);
            double moved = ((nb.A - beam.A) * along) / along.Length / (along.Length / 6);
            Ok(Math.Abs(moved - 6) < 0.3, $"belka na planie przesunięta o {moved:0.0} m wzdłuż toru");
            if (shotRails != null) { await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_bramka_przesunieta.png")); }
            // przeciągnięcie semafora – belka jedzie z nim, przesunięcie zostaje
            w.MoveDevice(sD.Id, sD0 + 5, "Przesunięcie urządzenia");
            Ok(Math.Abs(Urzadzenia.BelkaOff(Urzadzenia.KonstrukcjaOf(d.P, sD.Id).Value.M) - 6) < 0.2 && Urzadzenia.Przekrzywienie(d.P, D(sD.Id)) < 0.05, "semafor przesunięty – belka dalej 6 m obok, bramka w jednym przekroju");
            w.View.SelectDevice(sD.Id);
            var zb = Find<System.Windows.Controls.Button>(w.TrackPanel, b => (b.Content as string) == "Zeruj");
            Ok(zb != null, "panel: pole przesunięcia belki i „Zeruj”");
            if (zb != null) { Click(zb); Ok(Urzadzenia.BelkaOff(Urzadzenia.KonstrukcjaOf(d.P, sD.Id).Value.M) == 0, "„Zeruj” – belka w miejscu semaforów"); }
        }
    }
}

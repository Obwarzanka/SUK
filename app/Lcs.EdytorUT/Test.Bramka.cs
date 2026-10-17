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
        UtDevice Sig(string n, UtTrack t, double s, string mast) => new() { Kind = UtDevice.Semafor, Name = n, Track = t.Id, S = s, Dir = -1, Side = -1, Mast = mast, W24 = true, StripeP = true };
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
    }
}

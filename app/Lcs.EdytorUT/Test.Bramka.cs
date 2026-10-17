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
        w.SetTool(UtTool.Wskaz); w.View.Select(null); w.SetSchemat(true);
        var c = Go(o, lineAz, 0); w.View.SetView(Go(c, lineAz + 90, 2.5), 0.12);
        if (shotRails != null) { await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_bramka_widzew.png")); }
        w.SetSchemat(false);
    }
}

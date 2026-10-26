using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Komora nieczynna (uwaga właściciela 2026-10-22, plan – semafor E7: nieczynna, nieczynna, czerwona, nieczynna, nieczynna, pas, W24) jak
/// praca użytkownika: tor klikami, semafor przeciągnięciem, nazwa E7, układ gotowy z pasami i W24, cztery komory „nieczynna” z listy
/// w panelu, zapis tekstem z „-”, świecąca czerwona, eksport do symulatora tylko z komorami czynnymi, zrzut.
/// </summary>
public static partial class Test
{
    static async Task RunKomory(MainWindow w, string shot)
    {
        const double lineAz = 63;
        var dir = Path.Combine(Path.GetTempPath(), "edytor_ut_test", "komory"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        w.SetSchemat(false);
        w.NewDoc(); w.SaveDoc(false, Path.Combine(dir, "komory.ut.json"));
        var d = w.Doc;
        var o = Geo.ToPuwg92(51.7650, 19.5500); double k = Geo.ScaleFactor(o);
        XY Go(XY from, double azDeg, double len) => from + Element.Dir(azDeg * Math.PI / 180) * (len * k);
        Point S(XY p) => w.View.ToScreen(p);
        void Look(XY p, double mpp) => w.View.SetView(p, mpp);

        // zapis tekstem: „-” po literze – nieczynna (pulsowanie nie dotyczy nieczynnej)
        var pl = UtDevice.ParseLights("B-P-CP-Z-");
        Ok(pl.Count == 5 && pl.Count(x => x.Pulse == UtDevice.Nieczynna) == 4 && pl[2] == ('C', UtDevice.Stale) && UtDevice.FormatLights(pl) == "B-P-CP-Z-"
            && UtDevice.ValidLights("B-P-CP-Z-") && UtDevice.Czynne("B-P-CP-Z-").Count == 1 && UtDevice.ParseLights("Z-*")[0].Pulse == UtDevice.Nieczynna,
            "zapis komór: B-P-CP-Z- – 5 komór, 4 nieczynne, czynna tylko czerwona");

        // tor i semafor przeciągnięciem w stronę jazdy
        w.SetTool(UtTool.Tor); w.View.Mode = UtView.TorMode.Prosta; w.NewRBox.Text = "300"; w.NewLBox.Text = "0";
        var a = Go(o, lineAz + 180, 200); var b = Go(o, lineAz, 200);
        Look(o, 1); w.View.SimClick(S(a)); w.View.SimClick(S(b)); w.View.SimClick(S(b), MouseButton.Right);
        var t1 = d.P.Tracks.Single(); w.View.Select(t1.Id); Type(Find<TextBox>(w.TrackPanel, _ => true), "1");
        w.SetTool(UtTool.Wskaz); w.View.Select(null); Click(w.SchematBtn);
        var r1 = w.View.Geometry(t1);
        w.DevKindBox.SelectedIndex = Array.FindIndex(UtDevice.Kinds, x => x.Key == UtDevice.Semafor); w.SetTool(UtTool.Urzadzenie);
        var (pp, az) = r1.At(200); Look(pp, 0.3);
        w.View.SimDrag(S(pp), S(Go(pp, az * 180 / Math.PI, 8) + Element.Right(az) * (1.5 * k)));
        var sem = d.P.Devices.SingleOrDefault(x => x.Kind == UtDevice.Semafor);
        Ok(sem != null && sem.Dir == 1, "semafor przeciągnięciem w stronę jazdy");
        w.SetTool(UtTool.Wskaz); w.View.SelectDevice(sem.Id);
        Type(Find<TextBox>(w.TrackPanel, _ => true), "E7");
        UtDevice E7() => d.P.Devices.First(x => x.Id == sem.Id);
        Find<ComboBox>(w.TrackPanel, c => c.Items.Contains("(wybierz…)")).SelectedItem = UtDevice.Presets.First(x => x.Name.Contains("z pasami")).Name;
        Ok(E7().Name == "E7" && E7().Lights == "B*PCP*Z*" && E7().W24 && E7().StripeP, "E7: układ gotowy 5-komorowy z pasami i W24");

        // komory 1, 2, 4, 5 – „nieczynna” z listy światła w panelu (panel buduje się na nowo po każdej zmianie)
        List<ComboBox> Pulses() { var l = new List<ComboBox>(); void C(DependencyObject x) { if (x is ComboBox cb && cb.Items.Contains("nieczynna")) l.Add(cb); foreach (var ch in LogicalTreeHelper.GetChildren(x).OfType<DependencyObject>()) C(ch); } C(w.TrackPanel); return l; }
        Ok(Pulses().Count == 5 && Pulses()[0].Items.Count == 4, "panel: przy każdej z 5 komór lista światła z opcją „nieczynna”");
        foreach (var i in new[] { 0, 1, 3, 4 }) Pulses()[i].SelectedItem = "nieczynna";
        Ok(E7().Lights == "B-P-CP-Z-" && E7().LitOrDefault == 'C' && d.UndoName == "Komory sygnalizatora", $"komory 1, 2, 4, 5 nieczynne z panelu: zapis {E7().Lights}, świeci czerwona");
        Ok(Find<TextBox>(w.TrackPanel, x => x.Text == "B-P-CP-Z-") != null && Pulses()[2].SelectedItem as string == "stałe", "pole „Komory tekstem” pokazuje B-P-CP-Z-, czerwona – stałe");
        Pulses()[0].SelectedItem = "stałe";
        Ok(E7().Lights == "BP-CP-Z-", "komora 1 z powrotem czynna (stałe)");
        w.DoUndo();
        var lt = Find<TextBox>(w.TrackPanel, x => x.Text == "B-P-CP-Z-"); lt.Text = "b-p-cp*z-"; Enter(lt);
        Ok(E7().Lights == "B-P-CP*Z-", "komory tekstem małymi literami: b-p-cp*z- → B-P-CP*Z-");
        w.DoUndo();
        var uw = Urzadzenia.Check(d.P, E7()).Select(x => x.ToString()).ToList();
        Ok(!uw.Any(x => x.Contains("komor")), $"semafor z komorami nieczynnymi – bez uwag o komorach ({string.Join(" | ", uw)})");

        // eksport do symulatora: liczą się tylko komory czynne
        var lights = UtDevice.Czynne(E7().LightsOrDefault);
        Ok(lights.Count == 1 && !lights.Any(x => x.C == 'B'), "do symulatora: 1 komora czynna (czerwona), manewrowa nieczynna – bez sygnalizacji manewrowej");

        if (shot != null)
        {
            Look(Go(Urzadzenia.Pos(d.P, E7()).Value.P, lineAz, 6), 0.06); w.View.SelectDevice(null); await Settle(w);
            Save(w, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(shot))!, Path.GetFileNameWithoutExtension(shot) + "_komory_nieczynne.png"));
        }
    }
}

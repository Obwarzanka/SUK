using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Test usuwania kawałka toru i zaznaczania kilku elementów, jak użytkownik: tor prosta – łuk R 500 – prosta klikami, liczniki osi;
/// menu PPM toru: „Usuń ten kawałek” (łuk) i „Rozetnij tor tutaj”; Ctrl + klik dwóch liczników + Del; Shift + prostokąt + Del; Esc.
/// </summary>
public static partial class Test
{
    static async Task RunZaznaczenie(MainWindow w)
    {
        const double lineAz = 70;
        var dir = Path.Combine(Path.GetTempPath(), "edytor_ut_test", "zaznaczenie"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        w.SetSchemat(false);
        w.NewDoc(); w.SaveDoc(false, Path.Combine(dir, "stacja.ut.json"));
        var d = w.Doc;
        var o = Geo.ToPuwg92(51.7631, 19.5438); double k = Geo.ScaleFactor(o);
        XY Go(XY from, double azDeg, double len) => from + Element.Dir(azDeg * Math.PI / 180) * (len * k);
        Point S(XY p) => w.View.ToScreen(p);
        void Look(XY p, double mpp) => w.View.SetView(p, mpp);

        // tor: prosta 300 m – łuk R 500 w lewo o 30° – prosta 300 m
        w.SetTool(UtTool.Tor); w.View.Mode = UtView.TorMode.Prosta; w.NewRBox.Text = "500"; w.NewLBox.Text = "0";
        var a = Go(o, lineAz + 180, 300); double az0 = lineAz * Math.PI / 180, R = 500 * k;
        var cen = o - Element.Right(az0) * R; var e30 = cen + Element.Right(az0 - 30 * Math.PI / 180) * R; var c = Go(e30, lineAz - 30, 300);
        Look(o, 1.5);
        w.View.SimClick(S(a)); w.View.SimClick(S(o));
        w.View.Mode = UtView.TorMode.Luk; w.View.SimClick(S(e30));
        w.View.Mode = UtView.TorMode.Prosta; w.View.SimClick(S(c)); w.View.SimClick(S(c), MouseButton.Right);
        var t = d.P.Tracks.Single(); t.Number = "7"; var r = w.View.Geometry(t);
        var parts = Ciecie.Kawalki(r);
        Ok(parts.Count == 3 && parts[1].Luk && Math.Abs(parts[1].Len - 500 * Math.PI / 6) < 0.5, $"tor 7: prosta – łuk R 500 ({parts.ElementAtOrDefault(1)?.Len:0.0} m) – prosta, 3 kawałki");
        // liczniki osi: km 100, na łuku, za łukiem (+50 m)
        double sArc = (parts[1].S0 + parts[1].S1) / 2, sAfter = parts[1].S1 + 50;
        var lo = new[] { 100.0, sArc, sAfter }.Select((s, i) => new UtDevice { Kind = UtDevice.LicznikOsi, Name = $"lo{i + 1}", Track = t.Id, S = s }).ToList();
        d.Change("Liczniki", p => p.Devices.AddRange(lo));

        // --- PPM na łuku: „Usuń ten kawałek – łuk W2”
        w.SetTool(UtTool.Wskaz);
        var cm = w.BuildTrackMenu(t.Id, sArc);
        var mi = cm.Items.OfType<MenuItem>().FirstOrDefault(x => (x.Header as string)?.StartsWith("Usuń ten kawałek – łuk W2") == true);
        Ok(mi != null, $"menu PPM toru na łuku: „{mi?.Header}”");
        mi?.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        var tA = d.P.Tracks.FirstOrDefault(x => x.Id == t.Id); var tB = d.P.Tracks.FirstOrDefault(x => x.Id != t.Id);
        var rA = tA != null ? w.View.Geometry(tA) : null; var rB = tB != null ? w.View.Geometry(tB) : null;
        Ok(d.P.Tracks.Count == 2 && rA.Ok && rB.Ok && Math.Abs(rA.Length - parts[1].S0) < 0.01 && Math.Abs(rB.Length - (r.Length - parts[1].S1)) < 0.01
            && XY.Dist(rA.At(rA.Length).P, r.At(parts[1].S0).P) < 1e-6 && XY.Dist(rB.At(0).P, r.At(parts[1].S1).P) < 1e-6,
            $"łuk usunięty: tor 7 do początku łuku ({rA?.Length:0.00} m), nowy tor od końca łuku ({rB?.Length:0.00} m), położenie bez zmian");
        Ok(d.P.Devices.Count == 2 && d.P.Devices.Any(x => x.Name == "lo1" && x.Track == t.Id && x.S == 100) && d.P.Devices.Any(x => x.Name == "lo3" && x.Track == tB?.Id && Math.Abs(x.S - 50) < 1e-6)
            && w.LastMessage.Contains("usunięte z nim obiekty: 1"),
            $"licznik na łuku usunięty z kawałkiem, lo3 przeszedł na nowy tor (km 50) – {w.LastMessage}");
        w.DoUndo();
        Ok(d.P.Tracks.Count == 1 && d.P.Devices.Count == 3, "Ctrl+Z – tor i liczniki wróciły");

        // --- „Rozetnij tor tutaj” na prostej km 150; w łuku – niedostępne
        cm = w.BuildTrackMenu(t.Id, sArc);
        Ok(cm.Items.OfType<MenuItem>().First(x => x.Header as string == "Rozetnij tor tutaj").IsEnabled == false, "w łuku „Rozetnij tor tutaj” wyłączone (wielobok styczny nie zaczyna się w łuku)");
        cm = w.BuildTrackMenu(t.Id, 150);
        cm.Items.OfType<MenuItem>().First(x => x.Header as string == "Rozetnij tor tutaj").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        tB = d.P.Tracks.FirstOrDefault(x => x.Id != t.Id); rA = w.View.Geometry(d.P.Tracks.First(x => x.Id == t.Id)); rB = tB != null ? w.View.Geometry(tB) : null;
        Ok(d.P.Tracks.Count == 2 && Math.Abs(rA.Length - 150) < 0.01 && Math.Abs(rA.Length + rB.Length - r.Length) < 0.01 && XY.Dist(rA.At(rA.Length).P, rB.At(0).P) < 1e-6
            && d.P.Devices.Count(x => x.Track == tB.Id) == 2,
            $"rozcięty w km 150: {rA.Length:0.00} + {rB?.Length:0.00} m, końce się stykają ({XY.Dist(rA.At(rA.Length).P, rB.At(0).P):0.######}), liczniki lo2 i lo3 na nowym torze ({d.P.Devices.Count(x => x.Track == tB.Id)}; tory {d.P.Tracks.Count})");
        w.DoUndo();

        // --- Ctrl + klik dwóch liczników, Del
        XY DevAt(string name) { var dv = d.P.Devices.First(x => x.Name == name); return w.View.Geometry(d.P.Tracks.First(x => x.Id == dv.Track)).At(dv.S).P; }
        Look(DevAt("lo1"), 0.2); w.View.SimClick(S(DevAt("lo1")));
        Ok(w.View.SelectedDevice == d.P.Devices.First(x => x.Name == "lo1").Id, "klik – zaznaczony lo1");
        UtView.TestMods = ModifierKeys.Control;
        Look(DevAt("lo3"), 0.2); w.View.SimClick(S(DevAt("lo3")));
        UtView.TestMods = ModifierKeys.None;
        Ok(w.View.Multi.Count == 2 && w.TrackPanel.Children.OfType<TextBlock>().Any(x => x.Text == "Zaznaczone: 2"), $"Ctrl + klik lo3 – zaznaczone 2 urządzenia, panel „Zaznaczone: 2”");
        KeyPress(w, Key.Delete);
        Ok(d.P.Devices.Count == 1 && d.P.Devices[0].Name == "lo2" && w.View.Multi.Count == 0 && d.UndoName.StartsWith("Usunięcie zaznaczonych"), $"Del – usunięte lo1 i lo3 ({w.LastMessage})");
        w.DoUndo(); Ok(d.P.Devices.Count == 3, "jedno Ctrl+Z – oba wróciły");

        // --- Shift + prostokąt przez cały tor: tor i 3 liczniki; Esc; potem znowu i Del
        w.View.FitPoints(new[] { a, o, e30, c });
        var all = new[] { S(a), S(o), S(e30), S(c) };
        var p0 = new Point(all.Min(q => q.X) - 30, all.Min(q => q.Y) - 30); var p1 = new Point(all.Max(q => q.X) + 30, all.Max(q => q.Y) + 30);
        UtView.TestMods = ModifierKeys.Shift; w.View.SimDrag(p0, p1); UtView.TestMods = ModifierKeys.None;
        Ok(w.View.Multi.Count == 4 && w.View.InMulti(Kontrola.KTor, t.Id) && w.View.Multi.Count(x => x.Kind == Kontrola.KUrzadzenie) == 3, $"Shift + prostokąt: tor 7 i 3 liczniki ({w.View.Multi.Count})");
        if (shotRails != null) { await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_zaznaczenie.png")); }
        KeyPress(w, Key.Escape);
        Ok(w.View.Multi.Count == 0, "Esc – zaznaczenie zdjęte");
        UtView.TestMods = ModifierKeys.Shift; w.View.SimDrag(p0, p1); UtView.TestMods = ModifierKeys.None;
        KeyPress(w, Key.Delete);
        Ok(d.P.Tracks.Count == 0 && d.P.Devices.Count == 0, "prostokąt + Del – tor i liczniki usunięte");
        w.DoUndo(); Ok(d.P.Tracks.Count == 1 && d.P.Devices.Count == 3, "Ctrl+Z – wszystko wróciło");
        // zwykły klik kończy zaznaczenie kilku
        UtView.TestMods = ModifierKeys.Shift; w.View.SimDrag(p0, p1); UtView.TestMods = ModifierKeys.None;
        w.View.SimClick(new Point(5, 5));
        Ok(w.View.Multi.Count == 0, "zwykły klik obok – koniec zaznaczenia kilku");
        await Task.CompletedTask;
    }
}

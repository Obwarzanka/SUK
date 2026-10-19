using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Lcs.Model;
using Stacja = LcsWidzew.Model.Station;

namespace Lcs.EdytorZobrazowania;

/// <summary>
/// Test rozjazdów krzyżowych i skrzyżowań (etap 9, decyzje właściciela 2026-10-19 – <see cref="Krzyzowe"/>) jak praca użytkownika:
/// trzy układy – tor 1 poziomy i tor 2 przecinający go pod skosem 1:9, tarcze manewrowe na końcach obu torów; skrzyżowanie St,
/// rozjazd krzyżowy pojedynczy Rkp, podwójny Rkpd. Tory i końce ab / cd narysowane klikami (Rkp / Rkpd jak na pulpitach LCS – tor 2
/// nawiasami do zwrotnic), „Sprawdź”, „Do symulatora” (zwrotnice, węzły, brakujący przejazd Rkp, wnętrze toru 2), tablica przebiegów z komputera zależności (przez krzyż tylko na wprost), przebieg przez Rkpd na pulpicie.
/// </summary>
public static partial class Test
{
    /// <summary>Układ testowy: tor 1 (Y 0–600 m), tor 2 – skos 1:9 przez km 300 toru 1, tarcze T1–T4, skrzyżowanie nr 1 typu <paramref name="type"/>.</summary>
    static string UkladKrzyzowy(string dir, string name, string type, int slip = 1)
    {
        var p = new UtProject { Name = name };
        var t1 = new UtTrack { Number = "1", Vertices = new() { new UtVertex { X = 0, Y = 0 }, new UtVertex { X = 0, Y = 600 } } };
        var t2 = new UtTrack { Number = "2", Vertices = new() { new UtVertex { X = 30, Y = 30 }, new UtVertex { X = -30, Y = 570 } } };
        p.Tracks.Add(t1); p.Tracks.Add(t2);
        double l2 = Trasa.Compute(t2).Length;
        p.Crossings.Add(new UtCrossing { Number = "1", Type = type, TrackA = t1.Id, TrackB = t2.Id, SA = 300, SB = l2 / 2, Slip = slip });
        foreach (var (n, tr, s) in new[] { ("T1", t1.Id, 20.0), ("T2", t1.Id, 580.0), ("T3", t2.Id, 20.0), ("T4", t2.Id, l2 - 20) })
            p.Devices.Add(new UtDevice { Kind = UtDevice.TarczaMan, Name = n, Track = tr, S = s, Dir = 1 });
        var f = Path.Combine(dir, name + ".ut.json");
        File.WriteAllText(f, UtDocument.Serialize(p));
        if (File.Exists(ZobDocument.ZobPathFor(f))) File.Delete(ZobDocument.ZobPathFor(f));
        return f;
    }

    static int Zolte(string png)
    {
        var bmp = new System.Windows.Media.Imaging.PngBitmapDecoder(new Uri(png), System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad).Frames[0];
        var c = new System.Windows.Media.Imaging.FormatConvertedBitmap(bmp, System.Windows.Media.PixelFormats.Bgra32, null, 0);
        var px = new byte[c.PixelWidth * c.PixelHeight * 4]; c.CopyPixels(px, c.PixelWidth * 4, 0);
        int n = 0;
        for (int i = 0; i < px.Length; i += 4) if (px[i + 2] > 200 && px[i + 1] > 180 && px[i] < 80) n++;
        return n;
    }

    static async Task RunKrzyzowe(MainWindow w, string shot)
    {
        var dir = Path.Combine(Path.GetTempPath(), "edytor_zob_test", "krzyzowe"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        const ModifierKeys Shift = ModifierKeys.Shift, Ctrl = ModifierKeys.Control;

        async Task<(Stacja St, List<LcsWidzew.Sim.RouteDef> Routes, JsonObject Json, PodgladWindow Pw)> Rysuj(string type, int slip, bool rkpd)
        {
            var name = type.Split(' ')[0].ToLowerInvariant() + (slip < 0 ? "_lewy" : "");
            Ok(w.NewFromUt(UkladKrzyzowy(dir, name, type, slip)), $"{type}: nowe zobrazowanie z UT");
            await Settle(w); w.View.Fit();
            Point S(double x, double y) => w.View.ToScreen(x, y);
            var ut = w.Doc.Ut; var o = w.Obraz;
            // tor 1 poziomo
            w.ToolKey(Key.T);
            Leaf(w, "tor 1").IsSelected = true;
            w.View.SimClick(S(96, 520)); w.View.SimClick(S(1824, 520), MouseButton.Left, Shift); w.View.SimClick(S(1824, 520), MouseButton.Right);
            if (rkpd)
            {
                // Rkpd jak na pulpitach LCS (wzór właściciela – 18ab / 18cd): oba tory poziomo, tor 2 w dwóch kawałkach dochodzi nawiasem „[”
                // do punktu zwrotnicy ab i wychodzi nawiasem „]” od punktu cd (wnętrze rozjazdu na torze 2 się nie rysuje)
                // nawiasy rysuje się w przybliżeniu (punkt 8 j. od toru przyciąga się do jego linii, gdy jest bliżej niż 8 px na ekranie)
                w.View.Actual(); w.View.ZoomAt(new Point(w.View.ActualWidth / 2, w.View.ActualHeight / 2), 2); w.View.CenterOn(960, 568); await Settle(w);
                Leaf(w, "tor 2").IsSelected = true;
                Ok(w.LastMessage.Contains("km 0–") && !w.LastMessage.Contains("–543"), $"{type}: tor 2 – pierwszy kawałek do końca ab ({w.LastMessage})");
                w.View.SimClick(S(96, 616)); w.View.SimClick(S(904, 616), MouseButton.Left, Shift); w.View.SimClick(S(912, 608), MouseButton.Left, Ctrl);
                w.View.SimClick(S(912, 528), MouseButton.Left, Ctrl); w.View.SimClick(S(920, 520), MouseButton.Left, Ctrl); w.View.SimClick(S(920, 520), MouseButton.Right);
                Leaf(w, "tor 2").IsSelected = true;
                w.View.SimClick(S(1000, 520)); w.View.SimClick(S(1008, 528), MouseButton.Left, Ctrl); w.View.SimClick(S(1008, 608), MouseButton.Left, Ctrl);
                w.View.SimClick(S(1016, 616), MouseButton.Left, Ctrl); w.View.SimClick(S(1824, 616), MouseButton.Left, Shift); w.View.SimClick(S(1824, 616), MouseButton.Right);
                w.View.Fit(); await Settle(w);
                var k2 = o.Tory.Where(t => t.Ut.Tor == ut.Tracks[1].Id).ToList();
                Ok(k2.Count == 2 && ZobUt.Obiekty(ut, o).First(x => x.Opis.StartsWith("tor 2")).NaObrazie, $"{type}: tor 2 na obrazie w całości – dwa kawałki (km {string.Join(", ", k2.Select(t => $"{t.Ut.S0:0.#}–{t.Ut.S1:0.#}"))}), wnętrze rozjazdu pominięte");
            }
            else
            {
                Leaf(w, "tor 2").IsSelected = true;
                w.View.SimClick(S(96, 424)); w.View.SimClick(S(832, 424), MouseButton.Left, Shift); w.View.SimClick(S(1024, 616), MouseButton.Left, Ctrl);
                w.View.SimClick(S(1824, 616), MouseButton.Left, Shift); w.View.SimClick(S(1824, 616), MouseButton.Right);
            }
            // zwrotnice końców (St – bez symbolu)
            var rz = ZobUt.Obiekty(ut, o).Where(x => x.Grupa == ZobUt.GRozjazdy).Select(x => x.Opis).ToList();
            if (type.StartsWith("St")) Ok(rz.Count == 0, "St: w panelu UT bez zwrotnic – skrzyżowanie to krzyż z torów obrazu");
            else
            {
                Ok(rz.Count == 2 && rz[0].StartsWith("rozjazd krzyżowy 1 – koniec ab") && rz[1].StartsWith("rozjazd krzyżowy 1 – koniec cd"), $"{type}: w panelu UT dwa końce ({string.Join("; ", rz)})");
                w.ToolKey(Key.Z);
                var pts = new[] { (920, 520), (1000, 520) };
                Leaf(w, "rozjazd krzyżowy 1 – koniec ab").IsSelected = true; w.View.SimClick(S(pts[0].Item1 + 5, pts[0].Item2));
                Leaf(w, "rozjazd krzyżowy 1 – koniec cd").IsSelected = true; w.View.SimClick(S(pts[1].Item1, pts[1].Item2));
                var zs = o.Zwrotnice.Select(s => $"{s.Ut[^2..]}({s.P[0]},{s.P[1]})").ToList();
                Ok(o.Zwrotnice.Count == 2 && o.Zwrotnice.Zip(pts).All(x => x.First.P[0] == x.Second.Item1 && x.First.P[1] == x.Second.Item2),
                    $"{type}: zwrotnice 1ab, 1cd na obrazie {string.Join(" ", zs)} – ab przyciągnięta do końca toru 2");
            }
            // tarcze manewrowe
            w.ToolKey(Key.S);
            foreach (var (n, x, y) in new[] { ("T1", 152, 520), ("T2", 1768, 520), ("T3", 160, rkpd ? 616 : 424), ("T4", 1760, 616) })
            { Leaf(w, n + " – ").IsSelected = true; w.View.SimClick(S(x, y)); }
            var spr = w.Sprawdz().Where(u => !u.Text.Contains("numer toru")).ToList();
            Ok(spr.Count == 0, $"{type}: Sprawdź bez uwag (poza numerami torów) {string.Join(" | ", spr.Select(u => u.Text))}");
            if (!type.StartsWith("St")) Ok(w.SaveDoc(false), $"{type}: zapis");
            var dsym = DoSymulatora.Buduj(ut, w.Doc.P);
            var pw = w.Podglad();
            var routes = pw.Sim.RouteTable.Values.SelectMany(v => v).Where(r => r.Type == "M").ToList();
            return (pw.Station, routes, dsym.Json, pw);
        }

        string Ends(List<LcsWidzew.Sim.RouteDef> rs, string from) => string.Join(",", rs.Where(r => Stacja.Lbl(r.Start) == from).Select(r => Stacja.Lbl(r.End)).Distinct().OrderBy(x => x));

        // --- skrzyżowanie St: węzeł krzyża na obu torach, przez krzyż tylko na wprost
        {
            var (st, rs, js, pw) = await Rysuj("St 60E1-1:9", 1, false);
            var kr = js["nodes"]["UT:kr1"];
            int n4 = ((JsonArray)js["edges"]).Count(e => (string)e["a"] == "UT:kr1" || (string)e["b"] == "UT:kr1");
            Ok(kr != null && (string)kr["kind"] == "crossing" && n4 == 4 && ((JsonObject)js["switches"]).Count == 0, $"St: węzeł krzyża UT:kr1 z 4 torami, bez zwrotnic ({n4})");
            var dn = js["display"]["nodes"]["UT:kr1"];
            Ok(dn != null && (double)dn[0] == 928 && (double)dn[1] == 520, $"St: krzyż na pulpicie w punkcie przecięcia torów obrazu ({dn?[0]}, {dn?[1]})");
            Ok(Ends(rs, "T1") == "T2" && Ends(rs, "T3") == "T4", $"St: przebiegi przez krzyż tylko na wprost – T1 → {Ends(rs, "T1")}, T3 → {Ends(rs, "T3")}");
            pw.Close();
        }

        // --- rozjazd krzyżowy pojedynczy Rkp (przejazd łukiem z toru 1 przed środkiem na tor 2 za środkiem): na obrazie i w symulatorze jak Rkpd
        //     (decyzja właściciela 2026-10-19), komputer zależności nie dopuszcza jazdy przez brakujący przejazd (tor 2 → tor 1)
        {
            var (st, rs, js, pw) = await Rysuj("Rkp 60E1-190-1:9", 1, true);
            var sw = (JsonObject)js["switches"];
            Ok(sw.Count == 2 && sw.ContainsKey("UT:1ab") && sw.ContainsKey("UT:1cd") && js["nodes"]["UT:kr1"] == null, $"Rkp: zwrotnice 1ab, 1cd zwrócone ostrzami, bez krzyża ({string.Join(", ", sw.Select(x => x.Key))})");
            var np = js["noPass"] as JsonArray;
            Ok(np?.Count == 1 && st.NoPass.Count == 1, $"Rkp: jedna para torów bez przejazdu w pliku symulatora ({np?.ToJsonString()})");
            Ok(Ends(rs, "T1") == "T2,T4" && Ends(rs, "T3") == "T4", $"Rkp: T1 → {Ends(rs, "T1")} (na wprost i przejazdem), T3 → {Ends(rs, "T3")} (bez brakującego przejazdu na tor 1)");
            var t1t4 = rs.FirstOrDefault(r => Stacja.Lbl(r.Start) == "T1" && Stacja.Lbl(r.End) == "T4");
            Ok(t1t4 != null && t1t4.Sw.Any(x => x == ("UT:1ab", "+")) && t1t4.Sw.Any(x => x == ("UT:1cd", "-")), $"Rkp: T1 → T4 przez 1ab+ i 1cd− ({string.Join(" ", t1t4?.Sw.Select(x => x.Num + x.Pos) ?? new string[0])})");
            // przebieg T1 → T4 przejazdem na pulpicie
            pw.WindowStartupLocation = WindowStartupLocation.Manual; pw.ShowActivated = false; pw.ShowInTaskbar = false; pw.Width = 1500; pw.Height = 800;
            pw.Left = SystemParameters.VirtualScreenLeft - 3200; pw.Top = SystemParameters.VirtualScreenTop;
            pw.Show(); await Settle(pw); pw.Panel.FitPage(); await Settle(pw);
            pw.RouteBox.SelectedIndex = pw.Routes.FindIndex(r => r.Type == "M" && Stacja.Lbl(r.Start) == "T1" && Stacja.Lbl(r.End) == "T4");
            Click(All<ButtonBase>(pw).First(b => b.Content as string == "Nastaw"));
            pw.Czas(30); await Settle(pw);
            var png = Path.Combine(dir, "rkp_pulpit.png"); pw.Save(png);
            Ok(Zolte(png) > 300, $"Rkp: droga T1 → T4 przez przejazd żółta na pulpicie ({Zolte(png)} px)");
            var r2 = pw.Ses.Exec("UT:T3 UT:T2 Manewr");
            Ok(!r2.Ok, $"Rkp: polecenie T3 T2 Manewr (przez brakujący przejazd) odrzucone ({r2.Msg})");
            if (shot != null) File.Copy(png, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(shot))!, Path.GetFileNameWithoutExtension(shot) + "_rkp_pulpit.png"), true);
            pw.Close();
        }

        // --- rozjazd krzyżowy podwójny Rkpd: zwrotnice ab / cd zwrócone ostrzami, łącznik – kawałek toru 1, wnętrze toru 2 pominięte
        {
            var (st, rs, js, pw) = await Rysuj("Rkpd 60E1-190-1:9", 1, true);
            var sw = (JsonObject)js["switches"];
            Ok(sw.Count == 2 && sw.ContainsKey("UT:1ab") && sw.ContainsKey("UT:1cd") && (string)sw["UT:1ab"]["label"] == "1ab" && js["nodes"]["UT:kr1"] == null, "Rkpd: zwrotnice 1ab, 1cd jak w układzie Widzewa (17ab / 17cd), bez węzła krzyża");
            var e3 = ((JsonArray)js["edges"]).OfType<JsonObject>().Where(e => (string)e["a"] == "UT:sw1ab" || (string)e["b"] == "UT:sw1ab").ToList();
            var link = e3.FirstOrDefault(e => (string)e["a"] == "UT:sw1cd" || (string)e["b"] == "UT:sw1cd");
            Ok(e3.Count == 3 && link != null && (string)link["track"] == "1", $"Rkpd: w węźle 1ab 3 tory, łącznik ab–cd to tor 1 ({e3.Count})");
            Ok(st.Switches["UT:1ab"].Tip == link?["id"]?.ToString() && st.Switches["UT:1cd"].Tip == link?["id"]?.ToString(), "Rkpd: w symulatorze ostrza obu zwrotnic na łączniku (dwa rozjazdy zwrócone ostrzami, Ie-104 § 8 pkt 9)");
            var dab = js["display"]["nodes"]["UT:sw1ab"];
            Ok(dab != null && (double)dab[0] == 920 && (double)dab[1] == 520, "Rkpd: węzeł 1ab na pulpicie w punkcie symbolu");
            Ok(Ends(rs, "T1") == "T2,T4" && Ends(rs, "T3") == "T2,T4", $"Rkpd: wszystkie cztery jazdy – T1 → {Ends(rs, "T1")}, T3 → {Ends(rs, "T3")}");
            // przebieg T1 → T4 na pulpicie (przez 1ab+, 1cd−)
            pw.WindowStartupLocation = WindowStartupLocation.Manual; pw.ShowActivated = false; pw.ShowInTaskbar = false; pw.Width = 1500; pw.Height = 800;
            pw.Left = SystemParameters.VirtualScreenLeft - 3200; pw.Top = SystemParameters.VirtualScreenTop;
            pw.Show(); await Settle(pw); pw.Panel.FitPage(); await Settle(pw);
            int ri = pw.Routes.FindIndex(r => r.Type == "M" && Stacja.Lbl(r.Start) == "T1" && Stacja.Lbl(r.End) == "T4");
            Ok(ri >= 0, "w tablicy przebiegów: M T1 → T4");
            pw.RouteBox.SelectedIndex = ri;
            Click(All<ButtonBase>(pw).First(b => b.Content as string == "Nastaw"));
            pw.Czas(30); await Settle(pw);
            var ind = pw.Ses.Receive();
            Ok(ind.Routes.Values.Any(r => Stacja.Lbl(r.Start) == "T1" && r.State != "setting"), $"Rkpd: przebieg T1 → T4 nastawiony ({pw.Status.Text})");
            var png = Path.Combine(dir, "rkpd_pulpit.png"); pw.Save(png);
            int g = Zielone(png), y = Zolte(png);
            Ok(g < 200 && y > 300, $"Rkpd: droga przebiegu manewrowego żółta na pulpicie ({y} px żółtych; zielone tylko numery zwrotnic utwierdzonych – {g} px)");
            if (shot != null)
            {
                string Shot(string suf) => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(shot))!, Path.GetFileNameWithoutExtension(shot) + suf);
                File.Copy(png, Shot("_rkpd_pulpit.png"), true);
                w.View.Fit(); await Settle(w); Save(w, Shot("_rkpd_edytor.png"));
            }
            pw.Close();
        }
    }
}

using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Controls;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Kilometraż ze słupków OSM jak użytkownik: linia 17 dwutorowa (tory 1 i 2 co 4,5 m), linia 458 (tor 3) i tor 4 bez linii w OSM.
/// Odpowiedź Overpass zbudowana z „terenu”: słupki 17 – km 3 (z <c>railway:position:exact</c> 3.012), km 4 bliżej toru 2
/// (szacowany), km 5 – już w projekcie; 458 – km 1 przy torze 3 i km 2 za początkiem toru (kilometraż maleje na wschód);
/// słupek bez numeru linii przy torze 4. Narzędzie km → „Szukaj słupków km w widoku” → oś linii 17 na torze 1 → „Dodaj zaznaczone”.
/// </summary>
public static partial class Test
{
    static async Task RunKmOsm(MainWindow w, string shot)
    {
        w.SetSchemat(false);
        w.NewDoc();
        var o = Geo.ToPuwg92(51.77, 19.50); double k = Geo.ScaleFactor(o);
        XY At(double s, double off) => new(o.X - off * k, o.Y + s * k);          // tor na wschód, off – na południe [m w terenie]
        UtTrack Tor(string nr, double off, double s0, double s1) => new() { Number = nr, Vertices = new() { new UtVertex { X = At(s0, off).X, Y = At(s0, off).Y }, new UtVertex { X = At(s1, off).X, Y = At(s1, off).Y } } };
        UtTrack t1 = Tor("1", 0, 0, 2500), t2 = Tor("2", 4.5, 0, 2500), t3 = Tor("3", 300, 0, 800), t4 = Tor("4", -200, 0, 900);
        w.Doc.Change("tory testu", p => p.Tracks.AddRange(new[] { t1, t2, t3, t4 }));
        w.Doc.Change("km 5 linii 17", p => p.KmPoints.Add(new UtKmPoint { Line = "17", Track = t1.Id, S = 2300, Km = 5000, Dir = 1 }));

        // --- odpowiedź Overpass z „terenu”
        var sb = new StringBuilder("{\"elements\":[");
        long id = 1;
        string LL(XY p) { var (la, lo) = Geo.ToWgs84(p); return string.Format(CultureInfo.InvariantCulture, "\"lat\":{0:0.0000000},\"lon\":{1:0.0000000}", la, lo); }
        void Slupek(XY p, string km, string extra = "") => sb.Append($"{{\"type\":\"node\",\"id\":{id++},{LL(p)},\"tags\":{{\"railway\":\"milestone\",\"railway:position\":\"{km}\"{extra}}}}},");
        void Droga(string rf, XY a, XY b) => sb.Append($"{{\"type\":\"way\",\"id\":{id++},\"tags\":{{\"railway\":\"rail\",\"ref\":\"{rf}\"}},\"geometry\":[{{{LL(a)}}},{{{LL(b)}}}]}},");
        Slupek(At(300, -1.5), "3", ",\"railway:position:exact\":\"3.012\"");
        Slupek(At(1300, 3), "4", ",\"source\":\"interpolation\"");
        Slupek(At(2300, -1.5), "5");
        Slupek(At(400, 302), "1");
        Slupek(At(-600, 302), "2");
        Slupek(At(500, -203), "7", ",\"source\":\"interpolation\"");
        Droga("17", At(-500, 0), At(3000, 0)); Droga("17", At(-500, 4.5), At(3000, 4.5)); Droga("458", At(-1000, 300), At(900, 300));
        var json = Encoding.UTF8.GetBytes(sb.ToString().TrimEnd(',') + "]}");
        var sl = KilometrazOsm.Parse(json);
        Ok(sl.Count == 6 && sl.First(x => x.Km == 3012).Line == "17" && sl.First(x => x.Km == 1000).Line == "458" && sl.First(x => x.Km == 7000).Line == ""
           && sl.Count(x => x.Szacowany) == 2, $"odczyt Overpass: {sl.Count} słupków, linie z torów OSM ({string.Join(" ", sl.Select(x => $"{x.Line}/{Kilometraz.Fmt(x.Km)}"))}), km dokładny 3.012, szacowane 2");

        // --- jak użytkownik: narzędzie km, szukanie w widoku
        w.View.KmOsmFetchForTest = (a, b) => KilometrazOsm.Parse(json);
        w.View.SetView(At(1250, 100), 2.2);
        w.SetTool(UtTool.Km);
        Ok(w.KmOsmPanel.Visibility == System.Windows.Visibility.Visible && BtnIn(w.KmOsmPanel, "Szukaj słupków km w widoku") != null, "narzędzie km: panel „Kilometraż ze słupków OSM”");
        await w.KmOsmSzukaj();
        var pr = w.View.KmOsm;
        var p17 = pr.Where(x => x.Slupek.Line == "17").ToList();
        Ok(p17.Count == 2 && p17.All(x => x.Track == t1.Id && x.Dir == 1), $"oś linii 17: {p17.Count} propozycje na torze 1 (km 4 bliżej toru 2 też na osi), km 5 pominięty – już jest; kierunek +1");
        Ok(p17.FirstOrDefault(x => x.Slupek.Km == 4000)?.Uwaga.Contains("szacowane") == true, "km 4: uwaga „położenie szacowane w OSM”");
        var p458 = pr.FirstOrDefault(x => x.Slupek.Line == "458");
        Ok(p458 != null && p458.Track == t3.Id && p458.Dir == -1 && Math.Abs(p458.S - 400) < 0.5, $"oś linii 458 na torze 3: km 1.000 w {p458?.S:0.0} m, kierunek {p458?.Dir} (km maleje na wschód – z sąsiedniego słupka spoza projektu)");
        var p0 = pr.FirstOrDefault(x => x.Slupek.Line == "");
        Ok(p0 != null && p0.Track == t4.Id && p0.Uwaga.Contains("brak numeru linii") && p0.Uwaga.Contains("kierunek do sprawdzenia"), $"słupek bez linii w OSM: tor 4, uwagi „{p0?.Uwaga}”");
        Ok(w.Doc.P.KmPoints.Count == 1, "propozycje nie zmieniają projektu");

        // --- zatwierdzenie osi linii 17
        var os17 = Find<CheckBox>(w.KmOsmPanel, c => c.Content as string == "Oś linii 17 – tor 1 (2)");
        Ok(os17 != null, "panel: „Oś linii 17 – tor 1 (2)”");
        if (os17 != null) { os17.IsChecked = true; Click(os17); }
        var add = BtnIn(w.KmOsmPanel, "Dodaj zaznaczone (2)");
        Ok(add != null, "„Dodaj zaznaczone (2)”");
        if (shot != null) { await Settle(w); Save(w, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(shot)), Path.GetFileNameWithoutExtension(shot) + "_km_osm.png")); }
        if (add != null) Click(add);
        var km = w.Doc.P.KmPoints.Where(x => x.Line == "17").OrderBy(x => x.Km).ToList();
        Ok(km.Count == 3 && km[0].Km == 3012 && km[1].Km == 4000 && km.All(x => x.Track == t1.Id && x.Dir == 1), $"dodane: linia 17 km {string.Join(", ", km.Select(x => Kilometraz.Fmt(x.Km)))} na torze 1");
        Ok(!Kilometraz.Check(w.Doc.P).Any(x => x.Text.Contains("nie rośnie")), "kilometraż linii 17 rośnie zgodnie z punktami");
        Ok(w.View.KmOsm.Count == 2 && w.Doc.UndoName.StartsWith("Kilometraż z OSM"), "na liście zostały propozycje 458 i bez linii; jeden krok cofania");
        w.Doc.Undo();
        Ok(w.Doc.P.KmPoints.Count == 1, "Ctrl+Z – punkty z OSM cofnięte");
        Click(BtnIn(w.KmOsmPanel, "Wyczyść propozycje"));
        Ok(w.View.KmOsm.Count == 0, "„Wyczyść propozycje”");
        w.View.KmOsmFetchForTest = null;
        w.SetTool(UtTool.Wskaz);

        // --- na żywo (tylko informacja – Overpass bywa przeciążony): słupki przy Łodzi Widzewie
        try
        {
            var live = await OsmPunkty.Slupki(Geo.ToPuwg92(51.755, 19.47), Geo.ToPuwg92(51.78, 19.56), CancellationToken.None);
            log.AppendLine($"INFO  Overpass na żywo przy stacji: {live.Count} słupków km, linie {string.Join(", ", live.GroupBy(x => x.Line.Length > 0 ? x.Line : "?").Select(g => $"{g.Key}: {g.Count()}"))}, szacowane {live.Count(x => x.Szacowany)}");
        }
        catch (Exception ex) { log.AppendLine("INFO  Overpass niedostępny: " + Pobieranie.Reason(ex)); }
    }
}

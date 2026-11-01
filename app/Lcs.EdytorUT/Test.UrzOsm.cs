using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Controls;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Podpowiedzi z OSM jak użytkownik: tor 1 i tor bez numeru 4,5 m na południe, łącznik (dwa rozjazdy bez numerów), semafor X już
/// w projekcie. Odpowiedź Overpass z „terenu”: drogi OSM po osiach z numerami torów 1 i 2, semafor A (forward), B (backward, na torze 2),
/// tarcza 12, sygnalizator bez rodzaju, sam radiostop, semafor przy X (już jest), zwrotnice 3 i 4 przy PR, wykolejnica 1, kozioł.
/// Narzędzie Urządzenie → „Szukaj w widoku” → zaznaczenie grup → „Przyjmij zaznaczone” → Ctrl+Z.
/// </summary>
public static partial class Test
{
    static async Task RunUrzOsm(MainWindow w, string shot)
    {
        w.SetSchemat(false);
        w.NewDoc();
        var o = Geo.ToPuwg92(51.77, 19.50); double k = Geo.ScaleFactor(o);
        XY At(double s, double off) => new(o.X - off * k, o.Y + s * k);          // tor na wschód, off – na południe [m w terenie]
        UtTrack Tor(string nr, double off) => new() { Number = nr, Vertices = new() { new UtVertex { X = At(0, off).X, Y = At(0, off).Y }, new UtVertex { X = At(1500, off).X, Y = At(1500, off).Y } } };
        UtTrack t1 = Tor("1", 0), t2 = Tor("", 4.5);
        w.Doc.Change("tory testu", p => p.Tracks.AddRange(new[] { t1, t2 }));
        string err = null;
        w.Doc.Change("łącznik testu", p => { var (c1, c2, lk, e) = Rozjazdy.NewCrossover(p, t1.Id, 500, 1, 1, t2.Id, "Rz 60E1-190-1:9", "", ""); err = e; if (e == null) { p.Tracks.Add(lk); p.Turnouts.Add(c1); p.Turnouts.Add(c2); Rozjazdy.Sync(p); } });
        w.Doc.Change("semafor X", p => p.Devices.Add(new UtDevice { Kind = UtDevice.Semafor, Name = "X", Track = t1.Id, S = 1300, Dir = 1 }));
        var zw = w.Doc.P.Turnouts.OrderBy(x => x.Track == t1.Id ? 0 : 1).ToList();
        Ok(err == null && zw.Count == 2 && zw.All(x => x.Number == ""), $"łącznik z toru 1 na tor bez numeru, rozjazdy bez numerów ({err})");
        XY PR(UtTurnout t) { var tr = w.Doc.P.Tracks.First(x => x.Id == t.Track); return Trasa.Compute(tr).At(t.S).P; }
        var (pr1, pr2) = (PR(zw[0]), PR(zw[1]));

        // --- odpowiedź Overpass z „terenu”: węzły z tagami i drogi z listą węzłów
        var sb = new StringBuilder("{\"elements\":[");
        long id = 100;
        string LL(XY p) { var (la, lo) = Geo.ToWgs84(p); return string.Format(CultureInfo.InvariantCulture, "\"lat\":{0:0.0000000},\"lon\":{1:0.0000000}", la, lo); }
        var w1 = new List<(long, XY)>(); var w2 = new List<(long, XY)>();
        long Wezel(XY p, string tags, List<(long, XY)> droga)
        {
            long n = id++;
            if (tags != null) sb.Append($"{{\"type\":\"node\",\"id\":{n},{LL(p)},\"tags\":{{{tags}}}}},");
            droga?.Add((n, p));
            return n;
        }
        Wezel(At(-100, 0), null, w1);
        Wezel(At(200, 0.3), "\"railway\":\"signal\",\"ref\":\"A\",\"railway:signal:main\":\"PL-PKP:s\",\"railway:signal:direction\":\"forward\",\"railway:signal:position\":\"right\"", w1);
        Wezel(At(300, 0), "\"railway\":\"signal\",\"railway:signal:direction\":\"backward\"", w1);
        Wezel(At(350, 0), "\"railway\":\"signal\",\"railway:signal:radio\":\"PL-PKP:radiostop\",\"railway:signal:direction\":\"forward\"", w1);
        var p1 = pr1 + (At(502, 0) - At(500, 0));
        Wezel(p1, "\"railway\":\"switch\",\"ref\":\"3\"", w1);
        Wezel(At(900, 0), "\"railway\":\"signal\",\"ref\":\"12\",\"railway:signal:minor\":\"PL-PKP:z\",\"railway:signal:direction\":\"forward\"", w1);
        Wezel(At(1305, 0), "\"railway\":\"signal\",\"ref\":\"X\",\"railway:signal:main\":\"PL-PKP:s\",\"railway:signal:direction\":\"forward\"", w1);
        Wezel(At(1600, 0), null, w1);
        Wezel(At(2, 4.5), "\"railway\":\"buffer_stop\"", w2);
        Wezel(At(100, 4.5), "\"railway\":\"derail\",\"ref\":\"1\"", w2);
        Wezel(pr2, "\"railway\":\"switch\",\"ref\":\"4\"", w2);
        Wezel(At(1200, 4.5), "\"railway\":\"signal\",\"ref\":\"B\",\"railway:signal:combined\":\"PL-PKP:s\",\"railway:signal:direction\":\"backward\"", w2);
        Wezel(At(1600, 4.5), null, w2);
        void Droga(string nr, List<(long N, XY P)> l) => sb.Append($"{{\"type\":\"way\",\"id\":{id++},\"nodes\":[{string.Join(",", l.Select(x => x.N))}],\"tags\":{{\"railway\":\"rail\",\"railway:track_ref\":\"{nr}\"}},\"geometry\":[{string.Join(",", l.Select(x => "{" + LL(x.P) + "}"))}]}},");
        Droga("1", w1); Droga("2", w2.OrderBy(x => x.Item2.Y).ToList());
        var json = Encoding.UTF8.GetBytes(sb.ToString().TrimEnd(',') + "]}");
        var dane = UrzadzeniaOsm.Parse(json);
        Ok(dane.Obiekty.Count == 10 && dane.Tory.Count == 2 && dane.Obiekty.All(x => x.Kierunki.Count == 1), $"odczyt Overpass: {dane.Obiekty.Count} obiektów, {dane.Tory.Count} tory z numerem, kierunek drogi przy każdym węźle");

        // --- jak użytkownik: narzędzie Urządzenie, „Szukaj w widoku”
        w.View.UrzOsmFetchForTest = (a, b) => UrzadzeniaOsm.Parse(json);
        w.View.SetView(At(750, 50), 1.2);
        w.SetTool(UtTool.Urzadzenie);
        Ok(w.UrzOsmPanel.Visibility == System.Windows.Visibility.Visible && BtnIn(w.UrzOsmPanel, "Szukaj w widoku") != null, "narzędzie Urządzenie: panel „Podpowiedzi z OSM”");
        await w.UrzOsmSzukaj();
        var u = w.View.UrzOsm;
        PropozycjaUrz U(string name) => u.FirstOrDefault(x => x.Name == name);
        Ok(U("A") is { Kind: UtDevice.Semafor, Dir: 1, Side: 1 } a1 && a1.Track == t1.Id && Math.Abs(a1.S - 200) < 0.5, $"semafor A: tor 1, km 200, kierunek +1 (forward = kierunek drogi OSM), strona prawa ({U("A")?.S:0.0})");
        Ok(U("B") is { Kind: UtDevice.Semafor, Dir: -1 } b1 && b1.Track == t2.Id, "semafor B (backward) na torze bez numeru – kierunek −1");
        Ok(U("Tm12") is { Kind: UtDevice.TarczaMan, Dir: 1 }, "tarcza 12 (minor) – Tm12");
        var bez = u.FirstOrDefault(x => x.Kind == UtDevice.Semafor && x.Name == "");
        Ok(bez != null && bez.Dir == -1 && bez.Uwaga.Contains("rodzaj nieoznaczony"), $"sygnalizator bez rodzaju i nazwy: uwaga „{bez?.Uwaga}”");
        Ok(!u.Any(x => Math.Abs(x.S - 350) < 1) && !u.Any(x => x.Name == "X"), "sam radiostop pominięty; semafor przy X – już jest w projekcie");
        Ok(U("Wk1") is { Kind: UtDevice.Wykolejnica } wk && wk.Uwaga.Contains("kierunek do sprawdzenia"), "wykolejnica Wk1 – kierunek do sprawdzenia");
        Ok(u.FirstOrDefault(x => x.Kind == UtDevice.Koziol) is { S: 0, Dir: -1 } kz && kz.Track == t2.Id, "kozioł na początku toru bez numeru");
        var nr = w.View.NrOsm;
        Ok(nr.Any(x => x.Rozjazd && x.Id == zw[0].Id && x.Osm == "3" && x.Dist < 2.5) && nr.Any(x => x.Rozjazd && x.Id == zw[1].Id && x.Osm == "4"), $"numery zwrotnic: 3 i 4 przy PR rozjazdów ({string.Join(", ", nr.Where(x => x.Rozjazd).Select(x => $"{x.Osm} {x.Dist:0.0} m"))})");
        Ok(nr.Count(x => !x.Rozjazd) == 1 && nr.First(x => !x.Rozjazd) is { Osm: "2" } n2 && n2.Id == t2.Id, $"numer toru: 2 dla toru bez numeru; tor 1 i łącznik bez propozycji ({string.Join("; ", nr.Where(x => !x.Rozjazd).Select(x => $"{x.Osm}: {x.Uwaga}"))})");
        Ok(w.Doc.P.Devices.Count == 1 && zw.All(x => x.Number == ""), "propozycje nie zmieniają projektu");

        // --- zaznaczenie grup i przyjęcie
        foreach (var g in new[] { "Sygnalizatory i urządzenia", "Numery zwrotnic", "Numery torów" })
            if (Find<CheckBox>(w.UrzOsmPanel, c => (c.Content as string)?.StartsWith(g) == true) is CheckBox cb) { cb.IsChecked = true; Click(cb); }
        int n = u.Count + nr.Count;
        var add = BtnIn(w.UrzOsmPanel, $"Przyjmij zaznaczone ({n})");
        Ok(add != null, $"„Przyjmij zaznaczone ({n})” po zaznaczeniu trzech grup");
        if (shot != null) { await Settle(w); Save(w, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(shot)), Path.GetFileNameWithoutExtension(shot) + "_urz_osm.png")); }
        if (add != null) Click(add);
        var P = w.Doc.P;
        Ok(P.Devices.Count == 1 + u.Count && P.Devices.Any(x => x.Name == "A" && x.Track == t1.Id && x.Dir == 1) && P.Devices.Any(x => x.Kind == UtDevice.Semafor && x.Name.Length > 0 && x.Name != "A" && x.Name != "B" && x.Name != "X"),
            $"dodane urządzenia: {string.Join(", ", P.Devices.Select(x => x.Name))} (sygnalizator bez nazwy – kolejna wolna)");
        Ok(P.Turnouts.Select(x => x.Number).OrderBy(x => x).SequenceEqual(new[] { "3", "4" }) && P.Tracks.First(x => x.Id == t2.Id).Number == "2", "numery zwrotnic 3, 4 i toru 2 w projekcie");
        Ok(w.View.UrzOsm.Count + w.View.NrOsm.Count == 0 && w.Doc.UndoName.StartsWith("Podpowiedzi z OSM"), "lista pusta; jeden krok cofania");
        w.Doc.Undo();
        Ok(w.Doc.P.Devices.Count == 1 && w.Doc.P.Turnouts.All(x => x.Number == "") && w.Doc.P.Tracks.First(x => x.Id == t2.Id).Number == "", "Ctrl+Z – urządzenia i numery cofnięte");
        w.View.UrzOsmFetchForTest = null;
        w.SetTool(UtTool.Wskaz);
        Ok(w.UrzOsmPanel.Visibility == System.Windows.Visibility.Collapsed, "Wskaż bez propozycji – panel schowany");

        // --- na żywo (tylko informacja – Overpass bywa przeciążony): Łódź Widzew
        try
        {
            var live = await OsmPunkty.Urzadzenia(Geo.ToPuwg92(51.745, 19.50), Geo.ToPuwg92(51.775, 19.56), CancellationToken.None);
            log.AppendLine($"INFO  Overpass na żywo przy Łodzi Widzewie: sygnalizatorów {live.Obiekty.Count(x => x.Railway == "signal")}, zwrotnic {live.Obiekty.Count(x => x.Railway == "switch")} ({live.Obiekty.Count(x => x.Railway == "switch" && x.Tag("ref").Length > 0)} z numerem), odcinków torów z numerem {live.Tory.Count}");
        }
        catch (Exception ex) { log.AppendLine("INFO  Overpass niedostępny: " + Pobieranie.Reason(ex)); }
    }
}

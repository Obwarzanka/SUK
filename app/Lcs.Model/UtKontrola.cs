namespace Lcs.Model;

/// <summary>Uwaga z kontroli układu: błąd albo ostrzeżenie, miejsce (do pokazania na planie) i obiekt.</summary>
public record Uwaga(bool Error, string Text, XY? At, string Kind, string Id);

/// <summary>
/// Kontrola układu („Sprawdź”, etap 7): geometria torów, otwarte końce, rozjazdy (bez numeru, uwagi), urządzenia (poza torem, nazwy),
/// nakładające się tory, odcinki kontroli niezajętości, perony, przejazdy, kilometraż, profil.
/// </summary>
public static class Kontrola
{
    public const string KTor = "tor", KRozjazd = "rozjazd", KSkrzyzowanie = "skrzyzowanie", KUrzadzenie = "urzadzenie", KOdcinek = "odcinek",
        KPeron = "peron", KPrzejazd = "przejazd", KKm = "km", KSzlak = "szlak", KPosterunek = "posterunek";

    public static List<Uwaga> Run(UtProject p)
    {
        var res = new List<Uwaga>();
        var r = Topologia.Routes(p);
        var styki = Topologia.Styki(p, r);
        XY? At(string track, double s) => track != null && r.TryGetValue(track, out var rt) && rt.Elements.Count > 0 ? rt.At(Math.Clamp(s, 0, rt.Length)).P : null;
        string Nr(UtTrack t) => t.Number.Length > 0 ? t.Number : "(bez numeru)";

        foreach (var t in p.Tracks)
        {
            var rt = r[t.Id];
            foreach (var n in rt.Notes)
                res.Add(new Uwaga(n.Error, $"tor {Nr(t)}, {(t.Align != null ? "element " : "W")}{n.Vertex + 1}: {n.Text}", t.Align == null && n.Vertex < t.Vertices.Count ? t.Vertices[n.Vertex].P : null, KTor, t.Id));
            if (rt.Elements.Count == 0) continue;
            // łuki: skok krzywizny, prosta między łukami, promień i przejściowa wobec prędkości (ostrzeżenia)
            foreach (var u in Krzywizna.Check(t, rt)) res.Add(new Uwaga(false, $"tor {Nr(t)}: {u.Text}", At(t.Id, u.S), KTor, t.Id));
            // otwarte końce: bez połączenia, bez kozła oporowego i nie na granicy obszaru
            foreach (var atStart in new[] { true, false })
            {
                double se = atStart ? 0 : rt.Length;
                if ((atStart ? t.OpenStart : t.OpenEnd) || Topologia.Connected(styki, t.Id, atStart, rt.Length)) continue;
                if (p.Devices.Any(d => d.Track == t.Id && d.Kind == UtDevice.Koziol && Math.Abs(d.S - se) <= 1)) continue;
                if (p.Turnouts.Any(z => z.Branch == t.Id && z.Single && !atStart && t.Vertices.Count == 3)) { res.Add(new Uwaga(false, $"odgałęzienie rozjazdu {p.Turnouts.First(z => z.Branch == t.Id).Number} kończy się za skosem – przedłuż je narzędziem Tor albo postaw kozioł", At(t.Id, se), KTor, t.Id)); continue; }
                res.Add(new Uwaga(false, $"otwarty koniec toru {Nr(t)} ({(atStart ? "początek" : "koniec")}) – połącz z torem, postaw kozioł oporowy albo zaznacz w panelu toru „granica obszaru”", At(t.Id, se), KTor, t.Id));
            }
            if (t.Number.Length == 0 && !p.Turnouts.Any(z => z.Branch == t.Id) && !p.Crossings.Any(c => c.TrackB == t.Id))
                res.Add(new Uwaga(false, "tor bez numeru", At(t.Id, rt.Length / 2), KTor, t.Id));
        }
        // nakładające się tory (wspólny odcinek osi dłuższy niż 3 m poza rozjazdami)
        var tl = p.Tracks.Where(t => r[t.Id].Elements.Count > 0).ToList();
        for (int i = 0; i < tl.Count; i++)
            for (int j = i + 1; j < tl.Count; j++)
                if (Overlap(p, tl[i], tl[j], r) is (double s, double len)) res.Add(new Uwaga(true, $"tory {Nr(tl[i])} i {Nr(tl[j])} nakładają się na długości {len:0.#} m", At(tl[i].Id, s), KTor, tl[i].Id));

        foreach (var z in p.Turnouts)
        {
            var g = Rozjazdy.Geometry(p, z);
            if (z.Number.Length == 0) res.Add(new Uwaga(true, "rozjazd bez numeru", g?.SR, KRozjazd, z.Id));
            else if (Rozjazdy.NumberUsed(p, z.Number, z.Id) && p.Turnouts.First(x => x.Number == z.Number && Posterunki.Grupa(p, x.Track) == Posterunki.Grupa(p, z.Track)).Id == z.Id) res.Add(new Uwaga(true, $"numer rozjazdu {z.Number} powtórzony", g?.SR, KRozjazd, z.Id));
            foreach (var n in Rozjazdy.Check(p, z)) res.Add(new Uwaga(true, $"rozjazd {z.Number}: {n}", g?.SR, KRozjazd, z.Id));
        }
        foreach (var c in p.Crossings)
        {
            var g = Rozjazdy.CrossGeo(p, c);
            if (c.Number.Length == 0 && Katalog.Find(c.Type)?.Kind != RozjazdTyp.St) res.Add(new Uwaga(true, "rozjazd krzyżowy bez numeru", g?.P, KSkrzyzowanie, c.Id));
        }
        foreach (var d in p.Devices)
            foreach (var n in Urzadzenia.Check(p, d))
                res.Add(new Uwaga(n.Contains("poza torem") || n.Contains("brak toru"), $"{UtDevice.KindInfo(d.Kind).Name} {d.Name}: {n}", At(d.Track, d.S), KUrzadzenie, d.Id));
        foreach (var d in p.Devices.Where(d => d.Kind == UtDevice.Semafor && d.Name.Length == 0))
            res.Add(new Uwaga(true, "semafor bez nazwy", At(d.Track, d.S), KUrzadzenie, d.Id));

        // posterunki i szlaki
        var drogi = Szlaki.Drogi(p, r, styki);
        foreach (var (err, text, track, s, id) in Szlaki.Check(p, drogi)) res.Add(new Uwaga(err, text, At(track, s), id != null ? KSzlak : KUrzadzenie, id ?? p.Devices.FirstOrDefault(d => d.Track == track && Math.Abs(d.S - s) < 1e-6)?.Id));
        foreach (var (err, text, track, id) in Posterunki.Check(p, drogi, r)) res.Add(new Uwaga(err, text, track != null && r.TryGetValue(track, out var pt) && pt.Elements.Count > 0 ? pt.At(pt.Length / 2).P : null, id != null ? KPosterunek : KTor, id ?? track));

        var o = Odcinki.Compute(p, r);
        foreach (var (text, track, s, sec) in Odcinki.Check(p, o))
            res.Add(new Uwaga(!text.StartsWith("granica"), text, At(track, s), sec != null ? KOdcinek : KTor, sec ?? track));

        foreach (var pl in p.Platforms)
        {
            var rt = r.TryGetValue(pl.Track, out var x) ? x : null;
            string nm = $"peron {pl.Name}";
            if (rt == null) { res.Add(new Uwaga(true, nm + ": brak toru", null, KPeron, pl.Id)); continue; }
            if (Math.Min(pl.S0, pl.S1) < -0.01 || Math.Max(pl.S0, pl.S1) > rt.Length + 0.01) res.Add(new Uwaga(true, nm + ": wychodzi poza tor", At(pl.Track, pl.S0), KPeron, pl.Id));
            if (pl.Len < 1) res.Add(new Uwaga(true, nm + ": długość krawędzi poniżej 1 m", At(pl.Track, pl.S0), KPeron, pl.Id));
            if (pl.Track2 != null && !p.Tracks.Any(t => t.Id == pl.Track2)) res.Add(new Uwaga(true, nm + ": brak toru drugiej krawędzi", At(pl.Track, pl.S0), KPeron, pl.Id));
            if (pl.Name.Length == 0) res.Add(new Uwaga(false, "peron bez numeru", At(pl.Track, pl.S0), KPeron, pl.Id));
        }
        foreach (var lc in p.LevelCrossings)
        {
            if (!r.TryGetValue(lc.Track, out var rt)) { res.Add(new Uwaga(true, "przejazd: brak toru", null, KPrzejazd, lc.Id)); continue; }
            if (lc.S < -0.01 || lc.S > rt.Length + 0.01) res.Add(new Uwaga(true, $"{UtLevelCrossing.CatName(lc.Cat)} {lc.Name}: poza torem", At(lc.Track, lc.S), KPrzejazd, lc.Id));
            if (lc.Angle < 10 || lc.Angle > 170) res.Add(new Uwaga(true, $"{UtLevelCrossing.CatName(lc.Cat)} {lc.Name}: kąt skrzyżowania {lc.Angle:0}° – zbyt ostry", At(lc.Track, lc.S), KPrzejazd, lc.Id));
            if (p.Turnouts.Any(z => z.Track == lc.Track && Katalog.Find(z.Type) is RozjazdTyp ty && lc.S > Math.Min(z.S, z.S + z.Dir * (ty.A + ty.B)) && lc.S < Math.Max(z.S, z.S + z.Dir * (ty.A + ty.B))))
                res.Add(new Uwaga(true, $"{UtLevelCrossing.CatName(lc.Cat)} {lc.Name}: w obrębie rozjazdu", At(lc.Track, lc.S), KPrzejazd, lc.Id));
        }
        foreach (var (text, track, s) in Kilometraz.Check(p)) res.Add(new Uwaga(false, text, At(track, s), KTor, track));
        foreach (var k in p.KmPoints.Where(k => !r.ContainsKey(k.Track) || k.S < -0.01 || k.S > r[k.Track].Length + 0.01))
            res.Add(new Uwaga(true, $"punkt kilometrażu km {Kilometraz.Fmt(k.Km)}: poza torem", At(k.Track, k.S), KKm, k.Id));
        if (p.KmPoints.Count == 0 && p.Tracks.Count > 0)
            res.Add(new Uwaga(false, "brak kilometrażu linii – wstaw punkt km narzędziem Kilometraż (Ig-10 § 4 ust. 1 pkt 17–21)", null, KKm, null));
        foreach (var (text, track, s) in Profil.Check(p, styki, r)) res.Add(new Uwaga(false, text, At(track, s), KTor, track));
        return res.OrderBy(u => u.Error ? 0 : 1).ToList();
    }

    /// <summary>
    /// Wspólny odcinek osi dwóch torów (odległość &lt; 0,3 m, ten sam kierunek) dłuższy niż 3 m – poza rozjazdem, z którego jeden tor
    /// odgałęzia się od drugiego. Wynik: km na torze a i długość [m]; null – nie nakładają się.
    /// </summary>
    static (double S, double Len)? Overlap(UtProject p, UtTrack a, UtTrack b, Dictionary<string, TrasaResult> r)
    {
        var ra = r[a.Id]; var rb = r[b.Id];
        // szybkie odrzucenie – prostokąty wieloboków
        double pad = 1;
        if (a.Vertices.Max(v => v.X) + pad < b.Vertices.Min(v => v.X) || b.Vertices.Max(v => v.X) + pad < a.Vertices.Min(v => v.X) ||
            a.Vertices.Max(v => v.Y) + pad < b.Vertices.Min(v => v.Y) || b.Vertices.Max(v => v.Y) + pad < a.Vertices.Min(v => v.Y)) return null;
        // strefy rozjazdów, w których tory a i b się rozchodzą (odgałęzienie od PR do KR) – pominięte
        var skip = new List<(double S0, double S1)>();
        foreach (var z in p.Turnouts)
        {
            var ty = Katalog.Find(z.Type); if (ty == null) continue;
            double l = ty.A + ty.B + 2;
            if (z.Track == a.Id && z.Branch == b.Id) skip.Add((Math.Min(z.S, z.S + z.Dir * l), Math.Max(z.S, z.S + z.Dir * l)));
            if (z.Track == b.Id && z.Branch == a.Id) skip.Add(z.BranchAtEnd ? (ra.Length - l, ra.Length) : (0, l));
        }
        double run = 0, start = 0, best = 0, bestS = 0;
        for (double s = 0; s <= ra.Length; s += 0.5)
        {
            bool ok = false;
            if (!skip.Any(q => s >= q.S0 && s <= q.S1))
            {
                var (pt, az) = ra.At(s); var (sb, dist) = rb.Nearest(pt, 0.5);
                if (dist < 0.3 && sb > 0.01 && sb < rb.Length - 0.01) { var azb = rb.At(sb).Az; ok = Math.Abs(Math.Sin(az - azb)) < 0.03; }
            }
            if (ok) { if (run == 0) start = s; run += 0.5; if (run > best) { best = run; bestS = start; } }
            else run = 0;
        }
        return best > 3 ? (bestS, best) : null;
    }
}

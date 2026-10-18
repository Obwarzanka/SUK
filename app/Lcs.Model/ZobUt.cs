namespace Lcs.Model;

/// <summary>Obiekt UT do umieszczenia na obrazie: grupa w panelu UT, id, opis, czy jest już na obrazie.</summary>
public record ZobUtObj(string Grupa, string Id, string Opis, bool NaObrazie);

/// <summary>
/// Obiekty UT obrazu (PLAN_EDYTOR_ZOBRAZOWANIA.md § 3–4): co z UT należy do posterunków obrazu i czy jest już narysowane.
/// Obraz bez posterunków (albo UT bez posterunków) – cały obszar.
/// </summary>
public static class ZobUt
{
    public const string GTory = "Tory", GRozjazdy = "Rozjazdy", GSygnalizatory = "Sygnalizatory", GWykolejnice = "Wykolejnice",
        GKozly = "Kozły", GPrzerwy = "Przerwy izolacyjne", GOdcinki = "Odcinki", GPerony = "Perony", GPrzejazdy = "Przejazdy", GSzlaki = "Szlaki";
    public static readonly string[] Grupy = { GTory, GRozjazdy, GSygnalizatory, GWykolejnice, GKozly, GPrzerwy, GOdcinki, GPerony, GPrzejazdy, GSzlaki };

    /// <summary>Tory UT obrazu: tory posterunków obrazu i tory szlaków, które dochodzą do nich (wartość – szlak albo null).</summary>
    public static Dictionary<string, UtLine> Tory(UtProject ut, ZobObraz o, out List<UtLine> szlaki)
    {
        var posts = o.Posterunki.Where(id => ut.Posts.Any(x => x.Id == id)).ToHashSet();
        bool all = posts.Count == 0;
        var res = new Dictionary<string, UtLine>();
        foreach (var t in ut.Tracks.Where(t => all || (t.Post != null && posts.Contains(t.Post)))) res[t.Id] = null;
        var drogi = ut.Lines.Count > 0 ? Szlaki.Drogi(ut) : new();
        bool AtPost(UtLineEnd e) =>
            all || (e.Semafor != null && ut.Devices.FirstOrDefault(d => d.Id == e.Semafor) is UtDevice sd && res.ContainsKey(sd.Track) && res[sd.Track] == null);
        szlaki = ut.Lines.Where(l => l.Konce.Any(AtPost)).ToList();
        foreach (var l in szlaki)
            if (drogi.TryGetValue(l.Id, out var dr))
                foreach (var k in dr.Kawalki)
                    if (!res.ContainsKey(k.Track) || res[k.Track] == null && ut.Tracks.First(t => t.Id == k.Track).Post == null) res[k.Track] = l;
        return res;
    }

    static string TrNr(UtProject ut, string id) => ut.Tracks.FirstOrDefault(t => t.Id == id) is UtTrack t && t.Number.Length > 0 ? t.Number : "?";

    /// <summary>Lista obiektów UT obrazu po grupach, w kolejności panelu.</summary>
    public static List<ZobUtObj> Obiekty(UtProject ut, ZobObraz o)
    {
        var tory = Tory(ut, o, out var szlaki);
        var res = new List<ZobUtObj>();
        // tory z numerem po numerze, potem tory bez numeru (np. tor łączący łącznika – opis z numerów rozjazdów)
        foreach (var t in ut.Tracks.Where(t => tory.ContainsKey(t.Id)).OrderBy(t => t.Number.Length == 0).ThenBy(t => t.Number.Length).ThenBy(t => t.Number))
        {
            var cat = t.Cat.Length > 0 ? $" ({t.Cat})" : "";
            var sz = tory[t.Id] is UtLine l ? $" – szlak {l.Numer}" : "";
            var rz = ut.Turnouts.Where(z => z.Branch == t.Id && z.Number.Length > 0).Select(z => z.Number).ToList();
            var nm = t.Number.Length > 0 ? $"tor {t.Number}" : rz.Count == 2 ? $"tor łączący {rz[0]}/{rz[1]}" : rz.Count == 1 ? $"tor bez numeru (za rozjazdem {rz[0]})" : "tor bez numeru";
            res.Add(new(GTory, t.Id, $"{nm}{cat}{sz}", o.Tory.Any(x => x.Ut?.Tor == t.Id)));
        }
        foreach (var z in ut.Turnouts.Where(z => tory.ContainsKey(z.Track)).Select(z => (z.Id, z.Number, "rozjazd"))
                     .Concat(ut.Crossings.Where(c => tory.ContainsKey(c.TrackA)).Select(c => (c.Id, c.Number, "skrzyżowanie")))
                     .OrderBy(x => x.Number.Length).ThenBy(x => x.Number))
            res.Add(new(GRozjazdy, z.Id, $"{z.Item3} {(z.Number.Length > 0 ? z.Number : "(bez numeru)")}", o.Zwrotnice.Any(x => x.Ut == z.Id)));
        foreach (var d in ut.Devices.Where(d => tory.ContainsKey(d.Track)).OrderBy(d => d.Name))
        {
            var (g, lista) = d.Kind switch
            {
                UtDevice.Semafor or UtDevice.TarczaMan or UtDevice.TarczaOstrz => (GSygnalizatory, o.Sygnalizatory),
                UtDevice.Wykolejnica => (GWykolejnice, o.Wykolejnice),
                UtDevice.Koziol => (GKozly, o.Kozly),
                UtDevice.LicznikOsi or UtDevice.Zlacze => (GPrzerwy, null),
                _ => (null, null),
            };
            if (g == null) continue;
            var kind = (UtDevice.Kinds.FirstOrDefault(k => k.Key == d.Kind).Name ?? d.Kind).ToLowerInvariant();
            if (d.Kind == UtDevice.Semafor) kind = string.IsNullOrEmpty(d.Funkcja) ? "semafor – funkcja nie określona" : $"semafor {UtDevice.FunkcjaName(d.Funkcja)}";
            var nm = d.Name.Length > 0 ? d.Name : $"tor {TrNr(ut, d.Track)} km {d.S:0}";
            bool on = lista != null ? lista.Any(x => x.Ut == d.Id) : o.Przerwy.Any(x => x.Ut == d.Id);
            res.Add(new(g, d.Id, g == GKozly ? nm : $"{nm} – {kind}", on));
        }
        foreach (var s in ut.Sections.Where(s => tory.ContainsKey(s.Track)).OrderBy(s => s.Name))
            res.Add(new(GOdcinki, s.Id, s.Name.Length > 0 ? s.Name : "(bez nazwy)", o.Odcinki.Any(x => x.Ut == s.Id)));
        foreach (var pl in ut.Platforms.Where(x => tory.ContainsKey(x.Track)).OrderBy(x => x.Name))
            res.Add(new(GPerony, pl.Id, $"peron {(pl.Name.Length > 0 ? pl.Name : "(bez numeru)")}", o.Perony.Any(x => x.Ut == pl.Id)));
        foreach (var c in ut.LevelCrossings.Where(x => tory.ContainsKey(x.Track)))
            res.Add(new(GPrzejazdy, c.Id, $"{UtLevelCrossing.CatName(c.Cat)}{(c.Name.Length > 0 ? " – " + c.Name : "")}", o.Przejazdy.Any(x => x.Ut == c.Id)));
        foreach (var l in szlaki)
            res.Add(new(GSzlaki, l.Id, $"szlak {(l.Numer.Length > 0 ? l.Numer : "(bez numeru)")}", o.Szlaki.Any(x => x.Ut == l.Id)));
        return res;
    }
}

namespace Lcs.Model;

/// <summary>
/// Obiekt UT do umieszczenia na obrazie: grupa w panelu UT, id, opis, czy jest już na obrazie; <see cref="Czesc"/> – tor narysowany
/// tylko w części (opis brakujących km); <see cref="Obcy"/> – obiekt innego posterunku (może być na obrazie, „Sprawdź” go nie wymaga).
/// </summary>
public record ZobUtObj(string Grupa, string Id, string Opis, bool NaObrazie, string Czesc = null, bool Obcy = false);

/// <summary>
/// Obiekty UT obrazu (PLAN_EDYTOR_ZOBRAZOWANIA.md § 3–4): co z UT należy do posterunków obrazu i czy jest już narysowane.
/// Obraz bez posterunków (albo UT bez posterunków) – cały obszar.
/// </summary>
public static class ZobUt
{
    public const string GTory = "Tory", GRozjazdy = "Rozjazdy", GSygnalizatory = "Sygnalizatory", GWykolejnice = "Wykolejnice",
        GKozly = "Kozły", GPrzerwy = "Przerwy izolacyjne", GOdcinki = "Odcinki", GPerony = "Perony", GPrzejazdy = "Przejazdy", GSzlaki = "Szlaki", GNumery = "Numery torów";
    public static readonly string[] Grupy = { GTory, GRozjazdy, GSygnalizatory, GWykolejnice, GKozly, GPrzerwy, GOdcinki, GNumery, GPerony, GPrzejazdy, GSzlaki };

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

    /// <summary>Końce szlaku przy posterunkach obrazu (semafor wjazdowy na torze posterunku obrazu; obraz bez posterunków – każdy z semaforem).</summary>
    public static List<int> KonceNaObrazie(UtProject ut, ZobObraz o, UtLine l)
    {
        var posts = o.Posterunki.Where(id => ut.Posts.Any(x => x.Id == id)).ToHashSet();
        var res = new List<int>();
        for (int i = 0; i < l.Konce.Count; i++)
        {
            var e = l.Konce[i];
            if (e.Semafor == null || ut.Devices.FirstOrDefault(d => d.Id == e.Semafor) is not UtDevice sd) continue;
            var post = ut.Tracks.FirstOrDefault(t => t.Id == sd.Track)?.Post;
            if (posts.Count == 0 || post != null && posts.Contains(post)) res.Add(i);
        }
        return res;
    }

    static string TrNr(UtProject ut, string id) => ut.Tracks.FirstOrDefault(t => t.Id == id) is UtTrack t && t.Number.Length > 0 ? t.Number : "?";

    /// <summary>Lista obiektów UT obrazu po grupach, w kolejności panelu.</summary>
    /// <summary>
    /// Obiekty UT obrazu; <paramref name="inne"/> – także obiekty innych posterunków (oznaczone <see cref="ZobUtObj.Obcy"/>) – ten sam obiekt
    /// może być na dwóch obrazach (decyzja właściciela 2026-10-17, plan § 9 pkt 6), np. wspólna głowica.
    /// </summary>
    public static List<ZobUtObj> Obiekty(UtProject ut, ZobObraz o, bool inne)
    {
        var own = Obiekty(ut, o);
        if (!inne || !o.Posterunki.Any(id => ut.Posts.Any(p => p.Id == id))) return own;
        var cale = new ZobObraz
        {
            Id = o.Id, Rodzaj = o.Rodzaj, Posterunki = new(), Tory = o.Tory, Zwrotnice = o.Zwrotnice, Sygnalizatory = o.Sygnalizatory, Wykolejnice = o.Wykolejnice,
            Kozly = o.Kozly, Przerwy = o.Przerwy, Odcinki = o.Odcinki, NumeryTorow = o.NumeryTorow, Perony = o.Perony, Przejazdy = o.Przejazdy, Szlaki = o.Szlaki, Napisy = o.Napisy,
        };
        var ids = own.Select(x => (x.Grupa, x.Id)).ToHashSet();
        return own.Concat(Obiekty(ut, cale).Where(x => !ids.Contains((x.Grupa, x.Id))).Select(x => x with { Obcy = true })).ToList();
    }

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
            // na obrazie = cały kilometraż toru narysowany (kawałkami); część – brakujące km
            double len = Trasa.Compute(t).Length;
            var luki = ZobTory.Luki(o, t.Id, len);
            string czesc = luki.Count > 0 && o.Tory.Any(x => x.Ut?.Tor == t.Id) ? "brak km " + string.Join(", ", luki.Select(x => $"{x.A:0}–{x.B:0}")) : null;
            res.Add(new(GTory, t.Id, $"{nm}{cat}{sz}", luki.Count == 0, czesc));
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
        // numery torów stacyjnych (Ie-104 § 5 ust. 2 – widoczne stale): tory z numerem, nie szlakowe
        foreach (var t in ut.Tracks.Where(t => tory.ContainsKey(t.Id) && tory[t.Id] == null && t.Number.Length > 0).OrderBy(t => t.Number.Length).ThenBy(t => t.Number))
            res.Add(new(GNumery, t.Id, $"numer toru {t.Number}", o.NumeryTorow.Any(x => x.Ut == t.Id)));
        foreach (var l in szlaki)
        {
            // strzałka blokady przy każdym końcu szlaku na posterunku obrazu (szlak wewnętrzny – przy obu)
            var konce = KonceNaObrazie(ut, o, l);
            int on = konce.Count(k => o.Szlaki.Any(x => x.Ut == l.Id && (x.Koniec ?? 0) == k));
            res.Add(new(GSzlaki, l.Id, $"szlak {(l.Numer.Length > 0 ? l.Numer : "(bez numeru)")}", konce.Count > 0 && on == konce.Count,
                on > 0 && on < konce.Count ? $"strzałka {on} z {konce.Count}" : null));
        }
        return res;
    }
}

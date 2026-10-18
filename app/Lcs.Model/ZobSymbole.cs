namespace Lcs.Model;

/// <summary>Rodzaj symbolu obrazu przypiętego do urządzenia albo rozjazdu UT (etap Z3).</summary>
public enum ZobRodzaj { Zwrotnica, Sygnalizator, Wykolejnica, Kozioł }

/// <summary>Uwaga „Sprawdź” obrazu: błąd / uwaga, tekst, obiekt UT (do zaznaczenia), punkt obrazu.</summary>
public record ZobUwaga(bool Error, string Text, string Ut, int[] P);

/// <summary>
/// Symbole na torach obrazu (PLAN_EDYTOR_ZOBRAZOWANIA.md § 3, etap Z3): zwrotnice, sygnalizatory, wykolejnice, kozły – punkt na linii
/// toru obrazu, przypięty do obiektu UT. Kierunek symbolu wynika z kierunku działania w UT i z kierunku km kawałka toru obrazu
/// (nie wpisuje się go). „Sprawdź” – nieumieszczone, przypięcia do nieistniejących obiektów, symbol poza swoim torem, kolejność wzdłuż km.
/// </summary>
public static class ZobSymbole
{
    /// <summary>Lista symboli obrazu danego rodzaju.</summary>
    public static List<ZobSymbol> Lista(ZobObraz o, ZobRodzaj r) => r switch
    {
        ZobRodzaj.Zwrotnica => o.Zwrotnice, ZobRodzaj.Sygnalizator => o.Sygnalizatory, ZobRodzaj.Wykolejnica => o.Wykolejnice, _ => o.Kozly,
    };

    /// <summary>Rodzaj symbolu dla obiektu UT (null – obiekt nie ma symbolu na torze, np. licznik osi).</summary>
    public static ZobRodzaj? RodzajUt(UtProject ut, string id)
    {
        if (ut.Turnouts.Any(z => z.Id == id) || ut.Crossings.Any(c => c.Id == id)) return ZobRodzaj.Zwrotnica;
        return ut.Devices.FirstOrDefault(d => d.Id == id)?.Kind switch
        {
            UtDevice.Semafor or UtDevice.TarczaMan or UtDevice.TarczaOstrz => ZobRodzaj.Sygnalizator,
            UtDevice.Wykolejnica => ZobRodzaj.Wykolejnica,
            UtDevice.Koziol => ZobRodzaj.Kozioł,
            _ => null,
        };
    }

    /// <summary>Tor UT i km obiektu (zwrotnica – PR na torze zasadniczym, skrzyżowanie – środek na torze A).</summary>
    public static (string Track, double S)? Miejsce(UtProject ut, string id)
    {
        if (ut.Turnouts.FirstOrDefault(z => z.Id == id) is UtTurnout z) return (z.Track, z.S);
        if (ut.Crossings.FirstOrDefault(c => c.Id == id) is UtCrossing c) return (c.TrackA, c.SA);
        if (ut.Devices.FirstOrDefault(d => d.Id == id) is UtDevice d) return (d.Track, d.S);
        return null;
    }

    /// <summary>Nazwa obiektu do komunikatów (np. „semafor A”, „zwrotnica 3”, „kozioł na torze 2”).</summary>
    public static string Nazwa(UtProject ut, string id)
    {
        if (ut.Turnouts.FirstOrDefault(z => z.Id == id) is UtTurnout z) return $"zwrotnica {(z.Number.Length > 0 ? z.Number : "(bez numeru)")}";
        if (ut.Crossings.FirstOrDefault(c => c.Id == id) is UtCrossing c) return $"rozjazd krzyżowy {(c.Number.Length > 0 ? c.Number : "(bez numeru)")}";
        if (ut.Devices.FirstOrDefault(d => d.Id == id) is UtDevice d)
        {
            var tr = ut.Tracks.FirstOrDefault(t => t.Id == d.Track)?.Number ?? "?";
            return d.Kind switch
            {
                UtDevice.Semafor => $"semafor {d.Name}", UtDevice.TarczaMan => $"tarcza manewrowa {d.Name}", UtDevice.TarczaOstrz => $"tarcza ostrzegawcza {d.Name}",
                UtDevice.Wykolejnica => $"wykolejnica {d.Name}", UtDevice.Koziol => $"kozioł na torze {tr} km {d.S:0}", _ => d.Name,
            };
        }
        return "obiekt usunięty z UT";
    }

    /// <summary>Kawałki toru UT na obrazie zawierające km <paramref name="s"/> (z tolerancją 0,5 m).</summary>
    public static IEnumerable<ZobTor> Kawalki(ZobObraz o, string track, double s) =>
        o.Tory.Where(t => t.Ut?.Tor == track && s >= Math.Min(t.Ut.S0, t.Ut.S1) - 0.5 && s <= Math.Max(t.Ut.S0, t.Ut.S1) + 0.5 && t.Pkt.Count >= 2);

    /// <summary>Km toru UT w miejscu łamanej (liniowo po długości łamanej – to schemat).</summary>
    public static double KmNa(ZobTor t, int[] p)
    {
        var r = ZobTory.Rzut(t.Pkt, p[0], p[1]);
        return t.Ut.S0 + r.Frac * (t.Ut.S1 - t.Ut.S0);
    }

    /// <summary>
    /// Kierunek symbolu na obrazie dla kierunku działania <paramref name="utDir"/> (+1 – w stronę rosnącego km toru UT): (dx, dy) – jednostkowy
    /// kierunek odcinka łamanej w punkcie, w który zwrócone jest ostrze (na torze poziomym: dx = ±1, dy = 0).
    /// </summary>
    public static (int Dx, int Dy) Kierunek(ZobTor t, int[] p, int utDir)
    {
        var r = ZobTory.Rzut(t.Pkt, p[0], p[1]);
        var a = t.Pkt[r.Seg]; var b = t.Pkt[r.Seg + 1];
        int kmUp = t.Ut.S1 >= t.Ut.S0 ? 1 : -1;
        int k = utDir * kmUp;
        return (Math.Sign(b[0] - a[0]) * k, Math.Sign(b[1] - a[1]) * k);
    }

    /// <summary>Punkt leży na łamanej toru (do 0,5 j.).</summary>
    public static bool NaTorze(ZobTor t, int[] p) => t.Pkt.Count >= 2 && ZobTory.Rzut(t.Pkt, p[0], p[1]).Dist <= 0.5;

    /// <summary>Tor obrazu, na którym leży symbol obiektu UT (kawałek z km obiektu, na którego linii jest punkt); null – żaden.</summary>
    public static ZobTor TorSymbolu(UtProject ut, ZobObraz o, ZobSymbol s) =>
        Miejsce(ut, s.Ut) is var (tr, km) ? Kawalki(o, tr, km).FirstOrDefault(t => NaTorze(t, s.P)) : null;

    /// <summary>„Sprawdź” symboli obrazu (Z3): nieumieszczone, przypięcia, położenie na torze, kolejność wzdłuż km toru.</summary>
    public static List<ZobUwaga> Sprawdz(UtProject ut, ZobObraz o)
    {
        var res = new List<ZobUwaga>();
        var obj = ZobUt.Obiekty(ut, o);
        foreach (var x in obj.Where(x => !x.NaObrazie && x.Grupa is ZobUt.GTory or ZobUt.GRozjazdy or ZobUt.GSygnalizatory or ZobUt.GWykolejnice or ZobUt.GKozly))
            res.Add(new(false, $"nie ma na obrazie: {x.Opis}" + (x.Czesc != null ? $" ({x.Czesc})" : ""), x.Id, null));
        foreach (var t in o.Tory.Where(t => !ut.Tracks.Any(u => u.Id == t.Ut?.Tor)))
            res.Add(new(true, "tor obrazu przypięty do toru, którego nie ma w UT (usunięty albo rozcięty) – przypnij ponownie", t.Ut?.Tor, t.Pkt.FirstOrDefault()));
        foreach (ZobRodzaj r in Enum.GetValues<ZobRodzaj>())
            foreach (var s in Lista(o, r))
            {
                if (RodzajUt(ut, s.Ut) != r) { res.Add(new(true, $"symbol przypięty do obiektu, którego nie ma w UT ({r.ToString().ToLowerInvariant()}) – przypnij ponownie albo zdejmij", s.Ut, s.P)); continue; }
                if (TorSymbolu(ut, o, s) == null)
                {
                    var (tr, km) = Miejsce(ut, s.Ut).Value;
                    res.Add(new(true, $"{Nazwa(ut, s.Ut)} nie leży na torze obrazu toru {ut.Tracks.First(x => x.Id == tr).Number} w km {km:0} (tor obrazu przesunięty albo kawałek bez tego km)", s.Ut, s.P));
                }
            }
        // kolejność wzdłuż toru obrazu taka jak km w UT
        foreach (var t in o.Tory.Where(t => t.Pkt.Count >= 2))
        {
            var on = new List<(string Id, double KmUt, double KmObraz, int[] P)>();
            foreach (ZobRodzaj r in Enum.GetValues<ZobRodzaj>())
                foreach (var s in Lista(o, r))
                    if (Miejsce(ut, s.Ut) is var (tr, km) && tr == t.Ut.Tor && NaTorze(t, s.P)) on.Add((s.Ut, km, KmNa(t, s.P), s.P));
            var byUt = on.OrderBy(x => x.KmUt).ToList();
            for (int i = 1; i < byUt.Count; i++)
                if (byUt[i].KmObraz < byUt[i - 1].KmObraz - 1e-6 && Math.Abs(byUt[i].KmUt - byUt[i - 1].KmUt) > 0.5)
                    res.Add(new(true, $"kolejność na torze {ut.Tracks.First(x => x.Id == t.Ut.Tor).Number}: {Nazwa(ut, byUt[i - 1].Id)} (km {byUt[i - 1].KmUt:0}) i {Nazwa(ut, byUt[i].Id)} (km {byUt[i].KmUt:0}) są na obrazie w odwrotnej kolejności", byUt[i].Id, byUt[i].P));
        }
        return res;
    }
}

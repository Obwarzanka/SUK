namespace Lcs.Model;

/// <summary>
/// Rodzaj symbolu obrazu przypiętego do obiektu UT: Z3 – zwrotnica, sygnalizator, wykolejnica, kozioł; Z4 – przerwa izolacyjna
/// (licznik osi / złącze), numer toru, przejazd, strzałka blokady szlaku, opis odcinka.
/// </summary>
public enum ZobRodzaj { Zwrotnica, Sygnalizator, Wykolejnica, Kozioł, Przerwa, NumerToru, Przejazd, Szlak, OpisOdcinka }

/// <summary>Uwaga „Sprawdź” obrazu: błąd / uwaga, tekst, obiekt UT (do zaznaczenia), punkt obrazu.</summary>
public record ZobUwaga(bool Error, string Text, string Ut, int[] P);

/// <summary>
/// Symbole na torach obrazu (PLAN_EDYTOR_ZOBRAZOWANIA.md § 3, etapy Z3–Z4): punkt na linii toru obrazu, przypięty do obiektu UT
/// (opis odcinka – punkt opisu obok toru). Kierunek symbolu wynika z kierunku działania w UT i z kierunku km kawałka toru obrazu
/// (nie wpisuje się go). „Sprawdź” – nieumieszczone, przypięcia do nieistniejących obiektów, symbol poza swoim torem, kolejność wzdłuż km.
/// </summary>
public static class ZobSymbole
{
    /// <summary>Lista symboli obrazu danego rodzaju.</summary>
    public static List<ZobSymbol> Lista(ZobObraz o, ZobRodzaj r) => r switch
    {
        ZobRodzaj.Zwrotnica => o.Zwrotnice, ZobRodzaj.Sygnalizator => o.Sygnalizatory, ZobRodzaj.Wykolejnica => o.Wykolejnice, ZobRodzaj.Kozioł => o.Kozly,
        ZobRodzaj.Przerwa => o.Przerwy, ZobRodzaj.NumerToru => o.NumeryTorow, ZobRodzaj.Przejazd => o.Przejazdy, ZobRodzaj.Szlak => o.Szlaki,
        _ => o.Odcinki,
    };

    /// <summary>Symbol leży w miejscu km obiektu na torze – punkt zaczepienia przy przenoszeniu obrazu na pulpit i w kolejności wzdłuż toru.</summary>
    public static bool Kotwica(ZobRodzaj r) => r is not (ZobRodzaj.NumerToru or ZobRodzaj.Szlak or ZobRodzaj.OpisOdcinka);
    /// <summary>Symbol stoi na linii toru obrazu (opis odcinka – obok toru).</summary>
    public static bool NaLinii(ZobRodzaj r) => r != ZobRodzaj.OpisOdcinka;

    /// <summary>Rodzaj symbolu dla obiektu UT (null – obiekt nie ma symbolu na obrazie).</summary>
    public static ZobRodzaj? RodzajUt(UtProject ut, string id)
    {
        if (id == null) return null;
        if (ut.Turnouts.Any(z => z.Id == id) || ut.Crossings.Any(c => c.Id == id)) return ZobRodzaj.Zwrotnica;
        if (ut.Tracks.Any(t => t.Id == id)) return ZobRodzaj.NumerToru;
        if (ut.LevelCrossings.Any(c => c.Id == id)) return ZobRodzaj.Przejazd;
        if (ut.Lines.Any(l => l.Id == id)) return ZobRodzaj.Szlak;
        if (ut.Sections.Any(s => s.Id == id)) return ZobRodzaj.OpisOdcinka;
        return ut.Devices.FirstOrDefault(d => d.Id == id)?.Kind switch
        {
            UtDevice.Semafor or UtDevice.TarczaMan or UtDevice.TarczaOstrz => ZobRodzaj.Sygnalizator,
            UtDevice.Wykolejnica => ZobRodzaj.Wykolejnica,
            UtDevice.Koziol => ZobRodzaj.Kozioł,
            UtDevice.LicznikOsi or UtDevice.Zlacze => ZobRodzaj.Przerwa,
            _ => null,
        };
    }

    /// <summary>
    /// Tor UT i km obiektu (zwrotnica – PR na torze zasadniczym, skrzyżowanie – środek na torze A, szlak – koniec <paramref name="koniec"/>,
    /// odcinek – miejsce nazwy w UT); numer toru – km NaN (dowolny kawałek toru).
    /// </summary>
    public static (string Track, double S)? Miejsce(UtProject ut, string id, int koniec = 0)
    {
        if (ut.Turnouts.FirstOrDefault(z => z.Id == id) is UtTurnout z) return (z.Track, z.S);
        if (ut.Crossings.FirstOrDefault(c => c.Id == id) is UtCrossing c) return (c.TrackA, c.SA);
        if (ut.Devices.FirstOrDefault(d => d.Id == id) is UtDevice d) return (d.Track, d.S);
        if (ut.LevelCrossings.FirstOrDefault(x => x.Id == id) is UtLevelCrossing lc) return (lc.Track, lc.S);
        if (ut.Sections.FirstOrDefault(x => x.Id == id) is UtSection sc) return (sc.Track, sc.S);
        if (ut.Tracks.Any(t => t.Id == id)) return (id, double.NaN);
        if (ut.Lines.FirstOrDefault(x => x.Id == id) is UtLine l && koniec >= 0 && koniec < l.Konce.Count)
        {
            var e = l.Konce[koniec];
            if (e.Semafor != null && ut.Devices.FirstOrDefault(x => x.Id == e.Semafor) is UtDevice sd) return (sd.Track, sd.S);
            if (e.Track != null && ut.Tracks.FirstOrDefault(t => t.Id == e.Track) is UtTrack tt) return (e.Track, e.AtStart ? 0 : Trasa.Compute(tt).Length);
        }
        return null;
    }

    public static (string Track, double S)? Miejsce(UtProject ut, ZobSymbol s) => Miejsce(ut, s.Ut, s.Koniec ?? 0);

    /// <summary>Nazwa obiektu do komunikatów (np. „semafor A”, „zwrotnica 3”, „kozioł na torze 2”).</summary>
    public static string Nazwa(UtProject ut, string id, int? koniec = null)
    {
        if (ut.Turnouts.FirstOrDefault(z => z.Id == id) is UtTurnout z) return $"zwrotnica {(z.Number.Length > 0 ? z.Number : "(bez numeru)")}";
        if (ut.Crossings.FirstOrDefault(c => c.Id == id) is UtCrossing c) return $"rozjazd krzyżowy {(c.Number.Length > 0 ? c.Number : "(bez numeru)")}";
        if (ut.Tracks.FirstOrDefault(t => t.Id == id) is UtTrack t) return $"numer toru {(t.Number.Length > 0 ? t.Number : "?")}";
        if (ut.LevelCrossings.FirstOrDefault(x => x.Id == id) is UtLevelCrossing lc) return UtLevelCrossing.CatName(lc.Cat) + (lc.Name.Length > 0 ? $" {lc.Name}" : "");
        if (ut.Sections.FirstOrDefault(x => x.Id == id) is UtSection sc) return $"opis odcinka {(sc.Name.Length > 0 ? sc.Name : "(bez nazwy)")}";
        if (ut.Lines.FirstOrDefault(x => x.Id == id) is UtLine l)
            return $"strzałka szlaku {(l.Numer.Length > 0 ? l.Numer : "(bez numeru)")}" + (koniec is int k && k < l.Konce.Count ? $" ({Szlaki.Opis(ut, l.Konce[k])})" : "");
        if (ut.Devices.FirstOrDefault(d => d.Id == id) is UtDevice d)
        {
            var tr = ut.Tracks.FirstOrDefault(x => x.Id == d.Track)?.Number ?? "?";
            return d.Kind switch
            {
                UtDevice.Semafor => $"semafor {d.Name}", UtDevice.TarczaMan => $"tarcza manewrowa {d.Name}", UtDevice.TarczaOstrz => $"tarcza ostrzegawcza {d.Name}",
                UtDevice.Wykolejnica => $"wykolejnica {d.Name}", UtDevice.Koziol => $"kozioł na torze {tr} km {d.S:0}",
                UtDevice.LicznikOsi => $"przerwa – licznik osi {d.Name}".TrimEnd(), UtDevice.Zlacze => $"przerwa – złącze izolowane na torze {tr} km {d.S:0}", _ => d.Name,
            };
        }
        return "obiekt usunięty z UT";
    }

    /// <summary>Kawałki toru UT na obrazie zawierające km <paramref name="s"/> (z tolerancją 0,5 m; NaN – każdy kawałek toru).</summary>
    public static IEnumerable<ZobTor> Kawalki(ZobObraz o, string track, double s) =>
        o.Tory.Where(t => t.Ut?.Tor == track && t.Pkt.Count >= 2 && (double.IsNaN(s) || s >= Math.Min(t.Ut.S0, t.Ut.S1) - 0.5 && s <= Math.Max(t.Ut.S0, t.Ut.S1) + 0.5));

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
        s.P != null && Miejsce(ut, s) is var (tr, km) ? Kawalki(o, tr, km).FirstOrDefault(t => NaTorze(t, s.P)) : null;

    /// <summary>Kierunek strzałki szlaku na zewnątrz posterunku (od semafora wjazdowego w stronę szlaku) w miejscu symbolu.</summary>
    public static (int Dx, int Dy) NaZewnatrz(UtProject ut, ZobObraz o, ZobSymbol s)
    {
        var t = TorSymbolu(ut, o, s);
        var l = ut.Lines.FirstOrDefault(x => x.Id == s.Ut);
        if (t == null || l == null) return (1, 0);
        var e = l.Konce[Math.Clamp(s.Koniec ?? 0, 0, l.Konce.Count - 1)];
        // semafor wjazdowy działa dla jazdy ze szlaku do posterunku – szlak leży w drugą stronę
        int dir = e.Semafor != null && ut.Devices.FirstOrDefault(d => d.Id == e.Semafor) is UtDevice sd ? -sd.Dir : e.AtStart ? -1 : 1;
        return Kierunek(t, s.P, dir);
    }

    /// <summary>„Sprawdź” obrazu (Z3–Z4): nieumieszczone, przypięcia, położenie na torze, kolejność wzdłuż km toru, opisy przy swoich odcinkach.</summary>
    public static List<ZobUwaga> Sprawdz(UtProject ut, ZobObraz o)
    {
        var res = new List<ZobUwaga>();
        var obj = ZobUt.Obiekty(ut, o);
        foreach (var x in obj.Where(x => !x.NaObrazie))
            res.Add(new(false, $"nie ma na obrazie: {(x.Grupa == ZobUt.GOdcinki ? "opis odcinka " : "")}{x.Opis}" + (x.Czesc != null ? $" ({x.Czesc})" : ""), x.Id, null));
        foreach (var t in o.Tory.Where(t => !ut.Tracks.Any(u => u.Id == t.Ut?.Tor)))
            res.Add(new(true, "tor obrazu przypięty do toru, którego nie ma w UT (usunięty albo rozcięty) – przypnij ponownie", t.Ut?.Tor, t.Pkt.FirstOrDefault()));
        foreach (var pr in o.Perony.Where(p => !ut.Platforms.Any(x => x.Id == p.Ut)))
            res.Add(new(true, "peron przypięty do peronu, którego nie ma w UT – zdejmij albo przypnij ponownie", pr.Ut, pr.Prost?.Take(2).ToArray()));
        foreach (ZobRodzaj r in Enum.GetValues<ZobRodzaj>())
            foreach (var s in Lista(o, r))
            {
                if (s.P == null) continue;
                if (RodzajUt(ut, s.Ut) != r) { res.Add(new(true, $"symbol przypięty do obiektu, którego nie ma w UT ({Nazwa(ut, s.Ut)}) – przypnij ponownie albo zdejmij", s.Ut, s.P)); continue; }
                var (tr, km) = Miejsce(ut, s) ?? (null, 0);
                if (tr == null) { res.Add(new(true, $"{Nazwa(ut, s.Ut, s.Koniec)}: brak miejsca w UT", s.Ut, s.P)); continue; }
                var trNr = ut.Tracks.FirstOrDefault(x => x.Id == tr)?.Number ?? "?";
                if (r == ZobRodzaj.OpisOdcinka)
                {
                    // opis przy swoim odcinku – do 48 j. od toru obrazu toru nazwy odcinka
                    if (!Kawalki(o, tr, double.NaN).Any(t => ZobTory.Rzut(t.Pkt, s.P[0], s.P[1]).Dist <= 48))
                        res.Add(new(false, $"{Nazwa(ut, s.Ut)} daleko od toru {trNr} na obrazie (opis ma być jednoznacznie przy odcinku – Ie-104 § 4 ust. 15 pkt 5)", s.Ut, s.P));
                    continue;
                }
                if (TorSymbolu(ut, o, s) == null)
                    res.Add(new(true, $"{Nazwa(ut, s.Ut, s.Koniec)} nie leży na torze obrazu toru {trNr}" + (double.IsNaN(km) ? "" : $" w km {km:0}") + " (tor obrazu przesunięty albo kawałek bez tego km)", s.Ut, s.P));
            }
        // kolejność wzdłuż toru obrazu taka jak km w UT (symbole w miejscu km obiektu)
        foreach (var t in o.Tory.Where(t => t.Pkt.Count >= 2))
        {
            var on = new List<(string Id, double KmUt, double KmObraz, int[] P)>();
            foreach (ZobRodzaj r in Enum.GetValues<ZobRodzaj>().Where(Kotwica))
                foreach (var s in Lista(o, r))
                    if (s.P != null && Miejsce(ut, s) is var (tr, km) && tr == t.Ut.Tor && NaTorze(t, s.P)) on.Add((s.Ut, km, KmNa(t, s.P), s.P));
            // każdy symbol porównany z najdalszym (na obrazie) symbolem o mniejszym km w UT – także przy kilku symbolach w tym samym km
            var byUt = on.OrderBy(x => x.KmUt).ToList();
            (string Id, double KmUt, double KmObraz, int[] P)? far = null, farPrev = null;
            for (int i = 0; i < byUt.Count; i++)
            {
                if (i > 0 && byUt[i].KmUt - byUt[i - 1].KmUt > 0.5) farPrev = far;
                if (farPrev is var (fid, fkm, fob, _) && byUt[i].KmObraz < fob - 1e-6)
                    res.Add(new(true, $"kolejność na torze {ut.Tracks.First(x => x.Id == t.Ut.Tor).Number}: {Nazwa(ut, fid)} (km {fkm:0}) i {Nazwa(ut, byUt[i].Id)} (km {byUt[i].KmUt:0}) są na obrazie w odwrotnej kolejności", byUt[i].Id, byUt[i].P));
                if (far == null || byUt[i].KmObraz > far.Value.KmObraz) far = byUt[i];
            }
        }
        return res;
    }
}

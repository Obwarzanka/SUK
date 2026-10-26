namespace Lcs.Model;

/// <summary>
/// Odczyt obrazu starego edytora (plik <c>*.zob.json</c> w wersji 1) do wersji 2 (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 4, etap E5): tory obrazu → odcinki
/// grafu (łamane przez pióro – rozjazdy i skrzyżowania powstają same, km dzielony wzdłuż łamanej), symbole → elementy na odcinkach / w węzłach,
/// opisy przesunięte → przesunięcie <see cref="ObrOpis.D"/>, przypięcia → potwierdzone (człowiek je kiedyś ustawił). Plik wersji 1 zostaje bez zmian.
/// </summary>
public static class ObrZWersji1
{
    public record Wynik(ObrProject P, List<string> Uwagi);

    public static Wynik Przeksztalc(ZobProject v1, UtProject ut)
    {
        var uw = new List<string>();
        var p = new ObrProject { Ut = v1.Ut, UtZapis = v1.UtZapis, Siatka = v1.Siatka };
        foreach (var zo in v1.Obrazy)
        {
            var o = new ObrObraz { Id = zo.Id, Nazwa = zo.Nazwa, Rodzaj = zo.Rodzaj, Posterunki = zo.Posterunki.ToList(), Szer = zo.Szer, Wys = zo.Wys, OpisyOdcinkow = zo.OpisyOdcinkow };
            o.Strefy = ObrObraz.StrefyDomyslne(o.Szer);
            p.Obrazy.Add(o);
            Obraz(ut, zo, o, uw);
        }
        return new Wynik(p, uw);
    }

    static void Potw(ObrObraz o, string el, string ut) => o.Przypiecia.Add(new ObrPrzypiecie { El = el, Ut = ut, Stan = ObrPrzypiecie.Potwierdzone });

    static double Dl(int[] a, int[] b) => Math.Sqrt(Math.Pow(b[0] - a[0], 2) + Math.Pow(b[1] - a[1], 2));

    /// <summary>Położenie punktu na łamanej: część długości od początku (0–1).</summary>
    static double Frac(List<int[]> pts, int[] q)
    {
        double total = 0, at = 0, best = double.MaxValue;
        for (int i = 1; i < pts.Count; i++) total += Dl(pts[i - 1], pts[i]);
        double acc = 0;
        for (int i = 1; i < pts.Count; i++)
        {
            var r = ObrGraf.Rzut(pts[i - 1], pts[i], q[0], q[1]);
            double seg = Dl(pts[i - 1], pts[i]);
            if (r.Dist < best) { best = r.Dist; at = acc + r.T * seg; }
            acc += seg;
        }
        return total < 1e-9 ? 0 : at / total;
    }

    static void Obraz(UtProject ut, ZobObraz zo, ObrObraz o, List<string> uw)
    {
        // ---------- tory: łamana przez pióro, km wzdłuż łamanej
        // łamana zerowej długości (wszystkie punkty w jednym miejscu) – km toru zebrane w punkcie przy krawędzi obrazu
        var punkty = zo.Tory.Where(t => t.Pkt.Count >= 1 && t.Ut?.Tor != null && t.Pkt.All(p => p[0] == t.Pkt[0][0] && p[1] == t.Pkt[0][1])).ToList();
        foreach (var t in zo.Tory.Where(t => t.Pkt.Count >= 2 && t.Ut?.Tor != null).Except(punkty))
        {
            var lam = ObrGraf.Rysuj(o, t.Pkt.Select(x => new[] { x[0], x[1] }).ToList());
            foreach (var id in lam.Odcinki)
            {
                var e = ObrGraf.Odcinek(o, id);
                var (a, b) = ObrGraf.Konce(o, e);
                double fa = Frac(t.Pkt, a), fb = Frac(t.Pkt, b);
                o.Przypiecia.RemoveAll(x => x.El == id);
                o.Przypiecia.Add(new ObrPrzypiecie
                {
                    El = id, Stan = ObrPrzypiecie.Potwierdzone,
                    Tor = new ZobUtTor { Tor = t.Ut.Tor, S0 = t.Ut.S0 + (t.Ut.S1 - t.Ut.S0) * fa, S1 = t.Ut.S0 + (t.Ut.S1 - t.Ut.S0) * fb },
                });
                if (t.BezKontroli) e.Styl = ObrOdcinek.BezKontroli;
            }
            // skrzyżowania z łamanych bez wspólnego wierzchołka – element skrzyżowania (rozjazd krzyżowy ustawia człowiek)
        }
        ObrGraf.Upros(o);
        foreach (var t in punkty)
        {
            var w = ObrGraf.WezelW(o, t.Pkt[0]);
            (w.Poza ??= new()).Add(new ZobUtTor { Tor = t.Ut.Tor, S0 = t.Ut.S0, S1 = t.Ut.S1 });
        }

        // ---------- odcinek pod punktem (do 0,6 j. od linii)
        (ObrOdcinek E, double T)? Na(int[] q, string track = null)
        {
            var best = o.Odcinki.Where(e => track == null || o.Przypiecia.FirstOrDefault(x => x.El == e.Id)?.Tor?.Tor == track)
                .Select(e => { var (a, b) = ObrGraf.Konce(o, e); return (e, r: ObrGraf.Rzut(a, b, q[0], q[1])); })
                .OrderBy(z => z.r.Dist).FirstOrDefault();
            return best.e == null || track == null && best.r.Dist > 0.6 ? null : (best.e, best.r.T);
        }
        int Kier(ObrOdcinek e, int dir)
        {
            var t = o.Przypiecia.FirstOrDefault(x => x.El == e.Id)?.Tor;
            int s = t == null ? 1 : Math.Sign(t.S1 - t.S0); if (s == 0) s = 1;
            return s == (dir < 0 ? -1 : 1) ? 1 : -1;
        }
        void OpisZ(ObrElement el, int[] p0, int[] opis)
        {
            if (el.Opisy.FirstOrDefault() is ObrOpis d && opis != null && p0 != null) d.D = new[] { opis[0] - p0[0], opis[1] - p0[1] };
        }
        ObrElement NaTorze(string typ, ZobSymbol s, int kier, string tekst, string styl, string grupa)
        {
            if (Na(s.P) is not var (e, t)) { uw.Add($"{zo.Nazwa}: {grupa} {tekst} w ({s.P[0]}, {s.P[1]}) nie leży na torze obrazu – pominięty"); return null; }
            var el = ObrSymbole.Nowy(o, typ, e.Id, t, ObrSymbole.Kierunkowy(typ) ? Kier(e, kier) : 1);
            el.Styl = styl ?? el.Styl;
            if (el.Opisy.FirstOrDefault() is ObrOpis d) { d.Tekst = tekst ?? ""; d.D = ObrSymbole.OpisDomyslny(o, el); }
            OpisZ(el, s.P, s.Opis);
            if (s.Ut != null) Potw(o, el.Id, s.Ut);
            return el;
        }

        // ---------- zwrotnice: węzeł rozjazdu, numer, „+”
        foreach (var s in zo.Zwrotnice.Where(s => s.P != null))
        {
            var w = ObrGraf.WezelW(o, s.P);
            Potw(o, w.Id, s.Ut);
            string nr = ut.Turnouts.FirstOrDefault(z => z.Id == s.Ut)?.Number ?? (Krzyzowe.Parse(ut, s.Ut) is var (kc, kk) ? $"{kc.Number}{kk}" : "");
            var el = ObrSymbole.Nowy(o, ObrSymbole.Zwrotnica, w.Id, 0, 1);
            el.Opisy[0].Tekst = nr;
            if (ut.Turnouts.FirstOrDefault(z => z.Id == s.Ut)?.Zas == "-") el.Styl = ObrSymbole.PlusBok;
            el.Opisy[0].D = ObrSymbole.OpisDomyslny(o, el);
            OpisZ(el, s.P, s.Opis);
        }
        // ---------- sygnalizatory wg rodzaju i funkcji z UT
        foreach (var s in zo.Sygnalizatory.Where(s => s.P != null))
        {
            var d = ut.Devices.FirstOrDefault(x => x.Id == s.Ut);
            if (d == null) { uw.Add($"{zo.Nazwa}: sygnalizator przypięty do obiektu, którego nie ma w UT – pominięty"); continue; }
            var (typ, styl) = ObrSymbole.ZUt(d) ?? (ObrSymbole.Semafor, ObrSymbole.SemPociagowy);
            NaTorze(typ, s, d.Dir, d.Name, styl, "sygnalizator");
        }
        foreach (var s in zo.Wykolejnice.Where(s => s.P != null)) NaTorze(ObrSymbole.Wykolejnica, s, 1, ut.Devices.FirstOrDefault(x => x.Id == s.Ut)?.Name, null, "wykolejnica");
        foreach (var s in zo.Przerwy.Where(s => s.P != null)) NaTorze(ObrSymbole.Przerwa, s, 1, null, null, "przerwa");
        foreach (var s in zo.NumeryTorow.Where(s => s.P != null)) NaTorze(ObrSymbole.NumerToru, s, 1, ut.Tracks.FirstOrDefault(x => x.Id == s.Ut)?.Number, null, "numer toru");
        foreach (var s in zo.Przejazdy.Where(s => s.P != null))
        {
            var el = NaTorze(ObrSymbole.Przejazd, s, 1, ut.LevelCrossings.FirstOrDefault(x => x.Id == s.Ut)?.Name, null, "przejazd");
            if (el != null && s.Zasieg is { Length: 2 }) { el.Rozm = s.Zasieg.ToArray(); if (s.Opis == null) el.Opisy[0].D = ObrSymbole.OpisDomyslny(o, el); }
        }
        foreach (var s in zo.Szlaki.Where(s => s.P != null))
        {
            var el = NaTorze(ObrSymbole.StrzalkaBlokady, s, 1, null, null, "strzałka blokady");
            if (el != null) el.Pola = s.Koniec ?? 0;
        }
        // kozły – w węźle końca toru
        foreach (var s in zo.Kozly.Where(s => s.P != null))
        {
            var w = o.Wezly.FirstOrDefault(x => x.P[0] == s.P[0] && x.P[1] == s.P[1]);
            if (w == null || ObrGraf.Przy(o, w.Id).Count != 1) { uw.Add($"{zo.Nazwa}: kozioł w ({s.P[0]}, {s.P[1]}) nie na końcu toru obrazu – pominięty"); continue; }
            var el = ObrSymbole.Nowy(o, ObrSymbole.Koziol, w.Id, 0, 1);
            Potw(o, el.Id, s.Ut);
        }
        // opisy odcinków – obok toru odcinka: element na najbliższym odcinku toru UT nazwy, przesunięty do miejsca opisu
        foreach (var s in zo.Odcinki.Where(s => s.P != null))
        {
            var sc = ut.Sections.FirstOrDefault(x => x.Id == s.Ut);
            if (sc == null || Na(s.P, sc.Track) is not var (e, t)) { uw.Add($"{zo.Nazwa}: opis odcinka w ({s.P[0]}, {s.P[1]}) bez toru odcinka na obrazie – pominięty"); continue; }
            var el = ObrSymbole.Nowy(o, ObrSymbole.OpisOdcinka, e.Id, t, 1);
            el.Opisy[0].Tekst = sc.Name;
            var (a, b) = ObrGraf.Konce(o, e);
            var px = a[0] + (b[0] - a[0]) * t; var py = a[1] + (b[1] - a[1]) * t;
            el.Opisy[0].D = new[] { (int)Math.Round(s.P[0] - px), (int)Math.Round(s.P[1] - py) };
            Potw(o, el.Id, s.Ut);
        }
        foreach (var pr in zo.Perony.Where(x => x.Prost is { Length: 4 }))
        {
            var el = ObrSymbole.Nowy(o, ObrSymbole.Peron, null, 0, 1, new[] { pr.Prost[0], pr.Prost[1] });
            el.Rozm = new[] { pr.Prost[2], pr.Prost[3] };
            el.Opisy[0].Tekst = ut.Platforms.FirstOrDefault(x => x.Id == pr.Ut)?.Name ?? "";
            el.Opisy[0].D = ObrSymbole.OpisDomyslny(o, el);
            if (pr.Ut != null) Potw(o, el.Id, pr.Ut);
        }
        foreach (var n in zo.Napisy.Where(n => n.P != null))
        {
            var el = ObrSymbole.Nowy(o, ObrSymbole.Napis, null, 0, 1, n.P.ToArray());
            el.Opisy[0].Tekst = n.Tekst; el.Styl = n.Rodzaj;
            o.Przypiecia.Add(new ObrPrzypiecie { El = el.Id, Stan = ObrPrzypiecie.Brak });
        }
    }
}

namespace Lcs.Model;

/// <summary>Kawałek osi toru do usunięcia: prosta albo cały łuk w wierzchołku (z krzywymi przejściowymi), km od–do [m w terenie].</summary>
public record TorKawalek(double S0, double S1, int Vertex, bool Luk)
{
    public double Len => S1 - S0;
}

/// <summary>Wynik cięcia: błąd (null – dobrze), nowy tor (część za cięciem), tor usunięty w całości, opis.</summary>
public record CiecieWynik(string Error, string NewTrack, bool Deleted, string Info);

/// <summary>
/// Usuwanie kawałka toru i rozcinanie toru. Część przed cięciem zostaje tym samym torem (numer, rozjazdy, urządzenia), część za
/// cięciem staje się nowym torem – przechodzi na niego wszystko, co na niej leży (rozjazdy, skrzyżowania, urządzenia, odcinki,
/// perony, przejazdy, punkty km, profil), z kilometrażem liczonym od nowego początku. Co leży w usuniętym kawałku – znika.
/// Cięcie nie może trafić w rozjazd ani w tor łączący łącznika.
/// </summary>
public static class Ciecie
{
    const double E = 0.01;

    /// <summary>Kawałki osi toru: proste i łuki (łuk z przejściowymi w jednym wierzchołku – jeden kawałek).</summary>
    public static List<TorKawalek> Kawalki(TrasaResult r)
    {
        var res = new List<TorKawalek>(); double acc = 0;
        foreach (var e in r.Elements)
        {
            double a = acc / r.K, b = (acc + e.Len) / r.K; acc += e.Len;
            bool luk = e.Kind != ElKind.Prosta;
            if (luk && res.Count > 0 && res[^1].Luk && res[^1].Vertex == e.Vertex) res[^1] = res[^1] with { S1 = b };
            else res.Add(new TorKawalek(a, b, e.Vertex, luk));
        }
        return res;
    }

    /// <summary>Kawałek pod kilometrażem <paramref name="s"/>.</summary>
    public static TorKawalek KawalekAt(TrasaResult r, double s) => Kawalki(r).FirstOrDefault(k => s >= k.S0 - 1e-6 && s <= k.S1 + 1e-6);

    /// <summary>
    /// Rozcięcie toru w punkcie. W łuku (bez krzywych przejściowych) łuk dzieli się najpierw na dwa o tym samym R
    /// (<see cref="Budowa.SplitArc"/>) – oba tory kończą się w miejscu cięcia, kształt bez zmian. Bliżej niż 0,5 m od końca łuku –
    /// cięcie na końcu łuku.
    /// </summary>
    public static CiecieWynik Split(UtProject p, string trackId, double s)
    {
        var t = p.Tracks.FirstOrDefault(x => x.Id == trackId); if (t == null) return new("brak toru", null, false, null);
        var r = Trasa.Compute(t);
        if (t.Align == null && KawalekAt(r, s) is { Luk: true } kl)
        {
            if (s < kl.S0 + 0.5) s = kl.S0;
            else if (s > kl.S1 - 0.5) s = kl.S1;
            else
            {
                if (t.Vertices[kl.Vertex].L > 0 || t.Vertices[kl.Vertex].L2 > 0) return new("łuk z krzywymi przejściowymi – rozetnij tor na prostej albo na końcu łuku", null, false, null);
                var (nv, err) = Budowa.SplitArc(t.Vertices, kl.Vertex, Trasa.KFor(t), s);
                if (err != null) return new(err, null, false, null);
                t.Vertices = nv;
            }
        }
        return Cut(p, trackId, s, s);
    }

    /// <summary>Usunięcie kawałka toru od <paramref name="sa"/> do <paramref name="sb"/> [m w terenie]; sa = sb – samo rozcięcie.</summary>
    public static CiecieWynik Cut(UtProject p, string trackId, double sa, double sb)
    {
        var t = p.Tracks.FirstOrDefault(x => x.Id == trackId); if (t == null) return new("brak toru", null, false, null);
        var r = Trasa.Compute(t); double L = r.Length, k = r.K;
        if (sa > sb) (sa, sb) = (sb, sa);
        sa = Math.Clamp(sa, 0, L); sb = Math.Clamp(sb, 0, L);
        bool keepA = sa > E, keepB = sb < L - E;
        if (!keepA && !keepB) return new("to cały tor – usuń go (zaznacz i Del)", null, false, null);
        if (keepA && keepB && sb - sa < E && (sa < E || sa > L - E)) return new("w tym miejscu tor się kończy", null, false, null);

        // rozjazdy: tor łączący łącznika nie do cięcia; odgałęzienie – tylko za końcem rozjazdu; cięcie nie w obrębie rozjazdu
        foreach (var z in p.Turnouts.Where(z => z.Branch == trackId))
        {
            if (z.Partner != null) return new($"to tor łączący łącznika {z.Number} – usuń łącznik", null, false, null);
            var g = Rozjazdy.Geometry(p, z); double end = (g?.Typ.A ?? 0) * (g?.Q ?? 1) + (g?.B ?? 0);
            if (sa < end - E) return new($"cięcie w obrębie rozjazdu {z.Number} (na jego odgałęzieniu) – tnij za końcem rozjazdu", null, false, null);
        }
        foreach (var z in p.Turnouts.Where(z => z.Track == trackId))
        {
            var typ = Katalog.Find(z.Type); if (typ == null) continue;
            double a = Math.Min(z.S, z.S + z.Dir * (typ.A + typ.B)), b = Math.Max(z.S, z.S + z.Dir * (typ.A + typ.B));
            if (b > sa - E && a < sb + E) return new($"cięcie trafia w rozjazd {z.Number} – usuń go albo tnij obok", null, false, null);
        }
        foreach (var c in p.Crossings)
        {
            double cs = c.TrackA == trackId ? c.SA : c.TrackB == trackId ? c.SB : double.NaN;
            if (!double.IsNaN(cs) && cs > sa - 5 && cs < sb + 5) return new($"cięcie trafia w skrzyżowanie / rozjazd krzyżowy {c.Number} – usuń je albo tnij obok", null, false, null);
        }

        // nowe wierzchołki obu części (tor z elementów – przycięte elementy)
        UtAlign aa = null, ab = null;
        if (t.Align != null) { if (keepA) aa = TrasaElem.Trim(t.Align, k, 0, sa); if (keepB) ab = TrasaElem.Trim(t.Align, k, sb, L); }
        var va = keepA ? (aa != null ? new List<UtVertex> { new(), new() } : Budowa.Trim(t.Vertices, k, 0, sa)) : null;
        var vb = keepB ? (ab != null ? new List<UtVertex> { new(), new() } : Budowa.Trim(t.Vertices, k, sb, L)) : null;
        if ((va != null && va.Count < 2) || (vb != null && vb.Count < 2)) return new("za krótki kawałek po cięciu", null, false, null);

        var nb = keepB ? new UtTrack { Number = keepA ? "" : t.Number, Cat = t.Cat, Elec = t.Elec, Vmax = t.Vmax, Vertices = vb, OpenEnd = t.OpenEnd, Align = ab } : null;
        if (nb?.Align != null) TrasaElem.SyncEnds(nb);
        int removed = 0;
        if (!keepA)
        {
            // usuwany początek: tor zostaje tym samym torem (numer, odgałęzienia), obiekty za cięciem – km − sb
            t.Vertices = vb; t.Align = ab; TrasaElem.SyncEnds(t); t.OpenStart = false;
            Drop(p, trackId, s => s < sb - E, ref removed);
            Move(p, trackId, trackId, sb);
            t.Profile = t.Profile.Where(q => q.S >= sb - E).Select(q => new UtProfilePoint { S = q.S - sb, Z = q.Z, Rv = q.Rv }).ToList();
            t.Terrain = t.Terrain.Where(q => q.S >= sb - E).Select(q => new UtProfilePoint { S = q.S - sb, Z = q.Z, Rv = q.Rv }).ToList();
            return new(null, null, false, $"usunięto początek toru ({sb:0.##} m)" + (removed > 0 ? $"; usunięte z nim obiekty: {removed}" : ""));
        }
        t.Vertices = va; if (aa != null) { t.Align = aa; TrasaElem.SyncEnds(t); }
        if (!keepB) { t.OpenEnd = false; Drop(p, trackId, s => s > sa + E, ref removed); return new(null, null, false, $"usunięto koniec toru ({L - sa:0.##} m)" + (removed > 0 ? $"; usunięte z nim obiekty: {removed}" : "")); }
        p.Tracks.Insert(p.Tracks.IndexOf(t) + 1, nb);
        t.OpenEnd = false;
        Drop(p, trackId, s => s > sa + E && s < sb - E, ref removed);
        Move(p, trackId, nb.Id, sb);
        // profil i teren
        nb.Profile = t.Profile.Where(q => q.S >= sb - E).Select(q => new UtProfilePoint { S = q.S - sb, Z = q.Z, Rv = q.Rv }).ToList();
        nb.Terrain = t.Terrain.Where(q => q.S >= sb - E).Select(q => new UtProfilePoint { S = q.S - sb, Z = q.Z, Rv = q.Rv }).ToList();
        t.Profile = t.Profile.Where(q => q.S <= sa + E).ToList(); t.Terrain = t.Terrain.Where(q => q.S <= sa + E).ToList();
        return new(null, nb.Id, false, sb - sa < E ? $"tor rozcięty w km {sa:0.##}" : $"usunięty kawałek km {sa:0.##}–{sb:0.##} ({sb - sa:0.##} m), tor rozcięty na dwa" + (removed > 0 ? $"; usunięte z nim obiekty: {removed}" : ""));
    }

    /// <summary>Obiekty za <paramref name="from"/> na torze <paramref name="track"/> → tor <paramref name="to"/>, km − from.</summary>
    static void Move(UtProject p, string track, string to, double from)
    {
        foreach (var z in p.Turnouts.Where(z => z.Track == track && z.S >= from - E)) { z.Track = to; z.S -= from; }
        foreach (var c in p.Crossings) { if (c.TrackA == track && c.SA >= from) { c.TrackA = to; c.SA -= from; } if (c.TrackB == track && c.SB >= from) { c.TrackB = to; c.SB -= from; } }
        foreach (var d in p.Devices.Where(d => d.Track == track && d.S >= from - E)) { d.Track = to; d.S -= from; }
        foreach (var s in p.Sections.Where(s => s.Track == track && s.S >= from - E)) { s.Track = to; s.S -= from; }
        foreach (var x in p.LevelCrossings.Where(x => x.Track == track && x.S >= from - E)) { x.Track = to; x.S -= from; }
        foreach (var x in p.KmPoints.Where(x => x.Track == track && x.S >= from - E)) { x.Track = to; x.S -= from; }
        foreach (var x in p.Platforms.Where(x => x.Track == track && Math.Min(x.S0, x.S1) >= from - E)) { x.Track = to; x.S0 -= from; x.S1 -= from; }
    }

    /// <summary>Usunięcie obiektów toru w miejscach spełniających warunek (peron – przycięty do części, która zostaje).</summary>
    static void Drop(UtProject p, string track, Func<double, bool> gone, ref int n)
    {
        n += p.Devices.RemoveAll(d => d.Track == track && gone(d.S));
        n += p.Sections.RemoveAll(s => s.Track == track && gone(s.S));
        n += p.LevelCrossings.RemoveAll(x => x.Track == track && gone(x.S));
        n += p.KmPoints.RemoveAll(x => x.Track == track && gone(x.S));
        n += p.Platforms.RemoveAll(x => x.Track == track && gone(Math.Min(x.S0, x.S1)) && gone(Math.Max(x.S0, x.S1)));
        foreach (var x in p.Platforms.Where(x => x.Track == track))
        {
            // peron częściowo w usuniętym kawałku – przycięty do części poza nim
            double a = Math.Min(x.S0, x.S1), b = Math.Max(x.S0, x.S1);
            if (gone(b) && !gone(a)) { while (gone(b) && b > a) b -= 0.5; }
            else if (gone(a) && !gone(b)) { while (gone(a) && a < b) a += 0.5; }
            if (x.S0 <= x.S1) { x.S0 = a; x.S1 = b; } else { x.S1 = a; x.S0 = b; }
        }
    }
}

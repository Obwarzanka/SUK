namespace Lcs.Model;

/// <summary>Wynik łączenia: błąd (null – dobrze), tor wynikowy, opis.</summary>
public record LaczenieWynik(string Error, string Track, string Info);

/// <summary>
/// Łączenie zaznaczonych torów w jeden. Kolejno łączy najbliższe sobie końce: gdy się stykają – wprost (przy załamaniu łuk R
/// w miejscu styku), gdy są od siebie – stycznie łukiem albo łukami odwrotnymi (<see cref="Budowa.Connect"/>). Tor, do którego się
/// dołącza, zostaje (numer, rozjazdy, urządzenia); drugi znika, a wszystko, co na nim leży (rozjazdy, skrzyżowania, urządzenia,
/// odcinki, perony, przejazdy, punkty km, profil), przechodzi na tor wynikowy z kilometrażem od jego początku. Kierunek toru
/// odwraca się tylko wtedy, gdy nic na nim nie leży. Nie łączy się torów z elementów, torów łączących łączników ani początku
/// odgałęzienia rozjazdu.
/// </summary>
public static class Laczenie
{
    /// <summary>Końce stykają się [m w układzie].</summary>
    public const double Touch = 0.01;
    /// <summary>Kierunki zgodne – bez łuku w miejscu styku [rad] (ok. 0,006°).</summary>
    const double SameAz = 1e-4;

    public static LaczenieWynik Join(UtProject p, IReadOnlyList<string> ids, double rWant, double l)
    {
        var left = ids.Distinct().Select(id => p.Tracks.FirstOrDefault(t => t.Id == id)).ToList();
        if (left.Any(t => t == null)) return new("brak toru", null, null);
        if (left.Count < 2) return new("zaznacz co najmniej dwa tory (Shift + klik)", null, null);
        foreach (var t in left) if (Why(p, t) is string e) return new(e, null, null);
        var info = new List<string>(); string last = null;
        while (left.Count > 1)
        {
            // najbliższa para końców spośród zaznaczonych
            (UtTrack A, bool AEnd, UtTrack B, bool BEnd, double D) best = default; best.D = double.MaxValue;
            for (int i = 0; i < left.Count; i++)
                for (int j = i + 1; j < left.Count; j++)
                    foreach (bool ae in new[] { true, false })
                        foreach (bool be in new[] { false, true })
                        {
                            double d = XY.Dist(End(left[i], ae), End(left[j], be));
                            if (d < best.D - 1e-9) best = (left[i], ae, left[j], be, d);
                        }
            var (a, aEnd, b, bEnd, _) = best;
            // tor zostający dołącza swoim końcem początek drugiego
            UtTrack kept, abs;
            if (aEnd && !bEnd) (kept, abs) = (a, b);
            else if (!aEnd && bEnd) (kept, abs) = (b, a);
            else if (aEnd)                                   // koniec – koniec: odwrócić któryś
            {
                if (Free(p, b)) { Reverse(b); (kept, abs) = (a, b); }
                else if (Free(p, a)) { Reverse(a); (kept, abs) = (b, a); }
                else return new($"tory {Nr(a)} i {Nr(b)} mają przeciwne kierunki i na obu coś leży – nie da się ich połączyć bez odwracania kilometrażu", null, null);
            }
            else                                             // początek – początek
            {
                if (Free(p, a)) { Reverse(a); (kept, abs) = (a, b); }
                else if (Free(p, b)) { Reverse(b); (kept, abs) = (b, a); }
                else return new($"tory {Nr(a)} i {Nr(b)} mają przeciwne kierunki i na obu coś leży – nie da się ich połączyć bez odwracania kilometrażu", null, null);
            }
            var r = JoinTwo(p, kept, abs, rWant, l);
            if (r.Error != null) return r;
            info.Add(r.Info); left.Remove(abs); last = kept.Id;
        }
        return new(null, last, string.Join("; ", info));
    }

    static XY End(UtTrack t, bool end) => end ? t.Vertices[^1].P : t.Vertices[0].P;
    static string Nr(UtTrack t) => string.IsNullOrEmpty(t.Number) ? "(bez numeru)" : t.Number;

    /// <summary>Przeszkoda w łączeniu toru (null – można).</summary>
    static string Why(UtProject p, UtTrack t)
    {
        if (t.Align != null) return $"tor {Nr(t)} jest z elementów – zamień go na wielobok (panel toru)";
        if (t.Vertices.Count < 2) return $"tor {Nr(t)} nie ma osi";
        if (p.Turnouts.FirstOrDefault(z => z.Branch == t.Id && z.Partner != null) is UtTurnout zl) return $"tor {Nr(t)} to tor łączący łącznika {zl.Number}";
        return null;
    }

    /// <summary>Nic nie leży na torze ani się do niego nie odwołuje – można odwrócić jego kierunek.</summary>
    public static bool Free(UtProject p, UtTrack t)
    {
        string id = t.Id;
        return !p.Turnouts.Any(z => z.Track == id || z.Branch == id) && !p.Crossings.Any(c => c.TrackA == id || c.TrackB == id)
            && !p.Devices.Any(x => x.Track == id) && !p.Sections.Any(x => x.Track == id) && !p.LevelCrossings.Any(x => x.Track == id)
            && !p.KmPoints.Any(x => x.Track == id) && !p.Platforms.Any(x => x.Track == id || x.Track2 == id) && !Obiekty.NaTorze(p, id).Any()
            && !p.Lines.Any(x => x.Konce.Any(e => e.Track == id)) && t.Profile.Count == 0 && t.Terrain.Count == 0;
    }

    static void Reverse(UtTrack t)
    {
        t.Vertices.Reverse();
        // R / L należą do wierzchołka – zostają; L2 (przejściowa wyjściowa) zamienia się z L
        foreach (var v in t.Vertices) if (v.L2 is double l2) { v.L2 = v.L; v.L = l2; }
        (t.OpenStart, t.OpenEnd) = (t.OpenEnd, t.OpenStart);
    }

    /// <summary>Koniec toru <paramref name="kept"/> z początkiem toru <paramref name="abs"/>; <paramref name="abs"/> znika.</summary>
    static LaczenieWynik JoinTwo(UtProject p, UtTrack kept, UtTrack abs, double rWant, double l)
    {
        if (p.Turnouts.FirstOrDefault(z => z.Branch == abs.Id) is UtTurnout zb) return new($"początek toru {Nr(abs)} ustawia rozjazd {zb.Number} – dołącz tor do końca odgałęzienia", null, null);
        foreach (var ln in p.Lines)
            foreach (var e in ln.Konce)
                if ((e.Track == kept.Id && !e.AtStart) || (e.Track == abs.Id && e.AtStart)) return new($"na łączonym końcu kończy się szlak {ln.Numer} – najpierw zmień koniec szlaku", null, null);
        double k = Trasa.KFor(kept);
        var kv = kept.Vertices.Select(x => x.Clone()).ToList(); var av = abs.Vertices.Select(x => x.Clone()).ToList();
        XY e1 = kv[^1].P, e2 = av[0].P;
        double az1 = Budowa.EndAz(kv) ?? 0, az2 = Trasa.Az(av[0].P, av[1].P);
        double gap = XY.Dist(e1, e2), turn = Math.Abs(Math.IEEERemainder(az2 - az1, 2 * Math.PI));
        var verts = new List<UtVertex>(kv); int from = kv.Count - 1, hi = from; string how;
        if (gap < Touch)
        {
            if (turn < SameAz) how = "styk";
            else { verts[^1].R = rWant; verts[^1].L = l; how = $"załamanie {turn * 180 / Math.PI:0.##}° – łuk R {rWant:0} m w miejscu styku"; }
            verts.AddRange(av.Skip(1));
        }
        else
        {
            var mid = Budowa.Connect(e1, az1, e2, az2, rWant, l, k, out double usedR, out string note);
            if (mid == null) return new($"tory {Nr(kept)} i {Nr(abs)}: {note ?? "nie da się połączyć stycznie"}", null, null);
            verts.AddRange(mid); verts.AddRange(av); hi = from + mid.Count + 1;
            how = $"wstawka {gap / k:0.##} m" + (mid.Count == 0 ? " na prostej" : mid.Count == 1 ? $", łuk R {usedR:0.#} m" : $", łuki odwrotne R {usedR:0.#} m") + (note != null ? $" ({note})" : "");
        }
        // punkty na prostej w miejscu połączenia – precz (dalej wierzchołki zostają: mogą je ustawiać rozjazdy)
        for (int i = Math.Max(1, from); i <= hi && i < verts.Count - 1;)
        {
            double a1 = Trasa.Az(verts[i - 1].P, verts[i].P), a2 = Trasa.Az(verts[i].P, verts[i + 1].P);
            if (XY.Dist(verts[i - 1].P, verts[i].P) < 1e-6 || Math.Abs(Math.IEEERemainder(a2 - a1, 2 * Math.PI)) < 1e-7) { verts.RemoveAt(i); hi--; } else i++;
        }
        double absLen = Trasa.Compute(abs).Length;
        kept.Vertices = verts;
        var r = Trasa.Compute(kept); double off = r.Length - absLen;
        string id = kept.Id, ab = abs.Id;
        foreach (var z in p.Turnouts.Where(z => z.Track == ab)) { z.Track = id; z.S += off; }
        foreach (var c in p.Crossings) { if (c.TrackA == ab) { c.TrackA = id; c.SA += off; } if (c.TrackB == ab) { c.TrackB = id; c.SB += off; } }
        foreach (var x in p.Devices.Where(x => x.Track == ab)) { x.Track = id; x.S += off; }
        foreach (var x in p.Sections.Where(x => x.Track == ab)) { x.Track = id; x.S += off; }
        foreach (var x in p.LevelCrossings.Where(x => x.Track == ab)) { x.Track = id; x.S += off; }
        foreach (var x in p.KmPoints.Where(x => x.Track == ab)) { x.Track = id; x.S += off; }
        foreach (var x in p.Platforms) { if (x.Track == ab) { x.Track = id; x.S0 += off; x.S1 += off; } if (x.Track2 == ab) x.Track2 = id; }
        Obiekty.Przenies(p, ab, id, off);
        foreach (var e in p.Lines.SelectMany(x => x.Konce).Where(e => e.Track == ab)) { e.Track = id; if (e.S != null) e.S += off; }
        kept.Profile.AddRange(abs.Profile.Select(q => new UtProfilePoint { S = q.S + off, Z = q.Z, Rv = q.Rv }));
        kept.Terrain.AddRange(abs.Terrain.Select(q => new UtProfilePoint { S = q.S + off, Z = q.Z, Rv = q.Rv }));
        kept.OpenEnd = abs.OpenEnd;
        if (string.IsNullOrEmpty(kept.Number)) kept.Number = abs.Number;
        kept.Post ??= abs.Post;
        p.Tracks.Remove(abs);
        return new(null, id, $"tor {Nr(kept)} + {Nr(abs)}: {how}" + (r.Notes.Count > 0 ? " – " + string.Join("; ", r.Notes.Select(n => $"W{n.Vertex + 1}: {n.Text}")) : ""));
    }
}

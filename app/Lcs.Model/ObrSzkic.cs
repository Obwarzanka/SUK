namespace Lcs.Model;

/// <summary>
/// Szkic na życzenie (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 7, etap E7; decyzja właściciela 2026-10-22: tylko na polecenie człowieka, do przyjęcia albo odrzucenia).
/// Z wybranych torów UT program proponuje rysunek w prostokącie wskazanym przez człowieka: tory w rzędach w kolejności z UT (w poprzek głównej osi
/// układu), położenie wzdłuż – z położenia w UT, rozjazdy w kolejności km, łączniki ukosem 45° do sąsiedniego rzędu, sygnalizatory, kozły, numery torów
/// i zwrotnic z UT – od razu przypięte („pewne”). Obiekty UT już na obrazie są pomijane. Wynik – osobna warstwa (<see cref="ObrObraz.Szkic"/>). Bez WPF.
/// </summary>
public static class ObrSzkic
{
    /// <summary>Tory UT obrazu jeszcze nienarysowane (żaden odcinek obrazu nie jest do nich przypięty).</summary>
    public static List<string> Nienarysowane(UtProject ut, ObrObraz o)
    {
        var tory = ObrPrzypinanie.ToryObrazu(ut, o);
        var jest = o.Przypiecia.Where(p => p.Tor?.Tor != null && p.Stan != ObrPrzypiecie.DoWyjasnienia).Select(p => p.Tor.Tor).ToHashSet();
        return ut.Tracks.Where(t => tory.Contains(t.Id) && !jest.Contains(t.Id)).Select(t => t.Id).ToList();
    }

    /// <summary>Propozycja szkicu torów <paramref name="tory"/> (null – nienarysowane tory obrazu) w prostokącie [x, y, szer., wys.] [j.].</summary>
    public static ObrGraf.Fragment Zaproponuj(UtProject ut, ObrObraz o, IEnumerable<string> tory, int[] prost, int g)
    {
        var ids = (tory ?? Nienarysowane(ut, o)).ToHashSet();
        var r = ut.Tracks.Where(t => ids.Contains(t.Id)).ToDictionary(t => t.Id, t => Trasa.Compute(t));
        foreach (var k in r.Keys.Where(k => r[k].Elements.Count == 0).ToList()) r.Remove(k);
        if (r.Count == 0) return null;
        // oś główna – najdłuższy tor; u – wzdłuż, v – w poprzek (w górę)
        var naj = r.OrderByDescending(x => x.Value.Length).First().Value;
        var p0 = naj.At(0).P; var p1 = naj.At(naj.Length).P;
        double az = Math.Atan2(p1.Y - p0.Y, p1.X - p0.X), cs = Math.Cos(az), sn = Math.Sin(az);
        double U(string t, double s) { var p = r[t].At(Math.Clamp(s, 0, r[t].Length)).P; return p.X * cs + p.Y * sn; }
        double V(string t, double s) { var p = r[t].At(Math.Clamp(s, 0, r[t].Length)).P; return p.X * sn - p.Y * cs; }
        // łączniki: tor odgałęźny dwóch rozjazdów (para) – rysowany ukosem między rzędami; reszta – rzędy
        var lacz = ut.Turnouts.Where(z => z.Partner != null && z.Branch != null && r.ContainsKey(z.Branch)).Select(z => z.Branch).ToHashSet();
        var rzedy = r.Keys.Where(t => !lacz.Contains(t)).OrderByDescending(t => V(t, r[t].Length / 2)).ToList();
        if (rzedy.Count == 0) return null;
        int x0 = prost[0] + 2 * g, x1 = prost[0] + prost[2] - 2 * g;
        int krok = rzedy.Count == 1 ? 0 : Math.Clamp((prost[3] - 4 * g) / (rzedy.Count - 1) / g * g, 6 * g, 12 * g);
        var rzadY = rzedy.Select((t, i) => (t, y: ZobTory.Grid(prost[1] + 2 * g + i * krok, g))).ToDictionary(z => z.t, z => z.y);
        double umin = rzedy.Min(t => Math.Min(U(t, 0), U(t, r[t].Length))), umax = rzedy.Max(t => Math.Max(U(t, 0), U(t, r[t].Length)));
        int X(double u) => ZobTory.Grid(x0 + (u - umin) / Math.Max(1e-9, umax - umin) * (x1 - x0), g);

        var s = new ObrObraz { Szer = o.Szer, Wys = o.Wys, Posterunki = o.Posterunki };
        var lamane = new List<(string Track, int[] A, int[] B, double KmA, double KmB)>();
        void Rysuj(string tr, int[] a, int[] b, double ka, double kb)
        {
            if (a[0] == b[0] && a[1] == b[1]) return;
            ObrGraf.Rysuj(s, new List<int[]> { a, b });
            lamane.Add((tr, a, b, ka, kb));
        }
        // rzędy
        var konceRzedu = new Dictionary<string, (int[] A, int[] B)>();
        foreach (var t in rzedy)
        {
            double len = r[t].Length; int y = rzadY[t];
            var a = new[] { X(U(t, 0)), y }; var b = new[] { X(U(t, len)), y };
            konceRzedu[t] = (a, b);
            Rysuj(t, a, b, 0, len);
        }
        // rozjazdy: punkt na rzędzie toru zasadniczego; łącznik – ukos 45° do rzędu drugiego rozjazdu (drugi rozjazd tam, gdzie kończy się ukos)
        var pkt = new Dictionary<string, int[]>();
        foreach (var z in ut.Turnouts.Where(z => rzadY.ContainsKey(z.Track))) pkt[z.Id] = new[] { X(U(z.Track, z.S)), rzadY[z.Track] };
        foreach (var z in ut.Turnouts.Where(z => z.Partner != null && lacz.Contains(z.Branch) && !z.BranchAtEnd && pkt.ContainsKey(z.Id)))
        {
            var zp = ut.Turnouts.FirstOrDefault(x => x.Id == z.Partner);
            if (zp == null || !rzadY.ContainsKey(zp.Track)) continue;
            var a = pkt[z.Id]; int dy = rzadY[zp.Track] - a[1];
            if (dy == 0) continue;
            int sg = Math.Sign(X(U(zp.Track, zp.S)) - a[0]); if (sg == 0) sg = z.Dir >= 0 ? 1 : -1;
            var b = new[] { a[0] + sg * Math.Abs(dy), rzadY[zp.Track] };
            pkt[zp.Id] = b;
            Rysuj(z.Branch, a, b, 0, r[z.Branch].Length);
        }
        // tor odgałęźny bez pary (np. bocznica w osobnym rzędzie) – ukos od rozjazdu do początku jego rzędu
        foreach (var z in ut.Turnouts.Where(z => z.Partner == null && z.Branch != null && rzadY.ContainsKey(z.Branch) && pkt.ContainsKey(z.Id)))
        {
            var a = pkt[z.Id]; int dy = rzadY[z.Branch] - a[1]; if (dy == 0) continue;
            var start = z.BranchAtEnd ? konceRzedu[z.Branch].B : konceRzedu[z.Branch].A;
            int sg = Math.Sign(start[0] - a[0]); if (sg == 0) sg = 1;
            var b = new[] { a[0] + sg * Math.Abs(dy), rzadY[z.Branch] };
            ObrGraf.Rysuj(s, new List<int[]> { a, b });
            lamane.Add((z.Branch, a, b, z.BranchAtEnd ? r[z.Branch].Length : 0, z.BranchAtEnd ? r[z.Branch].Length : 0));
        }
        ObrGraf.Upros(s);

        // przypięcia odcinków: łamana, na której leżą oba końce – km liniowo wzdłuż niej
        foreach (var e in s.Odcinki)
        {
            var (a, b) = ObrGraf.Konce(s, e);
            var l = lamane.FirstOrDefault(q => ObrGraf.NaLinii(q.A, q.B, a) && ObrGraf.NaLinii(q.A, q.B, b));
            if (l.Track == null) continue;
            double Km(int[] p) { double L = Dl(l.A, l.B); return L < 1e-9 ? l.KmA : l.KmA + (l.KmB - l.KmA) * Dl(l.A, p) / L; }
            s.Przypiecia.Add(new ObrPrzypiecie { El = e.Id, Tor = new ZobUtTor { Tor = l.Track, S0 = Km(a), S1 = Km(b) }, Stan = ObrPrzypiecie.Pewne });
        }
        var uz = o.Przypiecia.Where(p => p.Ut != null).Select(p => p.Ut).ToHashSet();
        // zwrotnice z numerami
        foreach (var (zid, p) in pkt)
        {
            var w = s.Wezly.FirstOrDefault(x => x.P[0] == p[0] && x.P[1] == p[1]);
            if (w == null || ObrGraf.RodzajWezla(s, w.Id) != ObrGraf.Rodzaj.Rozjazd || uz.Contains(zid)) continue;
            var z = ut.Turnouts.First(x => x.Id == zid);
            var el = ObrSymbole.Nowy(s, ObrSymbole.Zwrotnica, w.Id, 0, 1);
            el.Opisy[0].Tekst = z.Number;
            if (z.Zas == "-") el.Styl = ObrSymbole.PlusBok;
            el.Opisy[0].D = ObrSymbole.OpisDomyslny(s, el);
            s.Przypiecia.Add(new ObrPrzypiecie { El = w.Id, Ut = zid, Stan = ObrPrzypiecie.Pewne });
        }
        // sygnalizatory i wykolejnice na rzędach, kozły na końcach, numery torów
        (ObrOdcinek E, double T)? Na(int[] p) =>
            s.Odcinki.Select(e => { var (a, b) = ObrGraf.Konce(s, e); return (e, rz: ObrGraf.Rzut(a, b, p[0], p[1])); }).Where(z => z.rz.Dist < 0.6).Select(z => ((ObrOdcinek, double)?)(z.e, z.rz.T)).FirstOrDefault();
        foreach (var d in ut.Devices.Where(d => rzadY.ContainsKey(d.Track) && !uz.Contains(d.Id)))
        {
            var p = new[] { X(U(d.Track, d.S)), rzadY[d.Track] };
            if (d.Kind == UtDevice.Koziol)
            {
                var (ka, kb) = konceRzedu[d.Track];
                var kon = Math.Abs(d.S) < Math.Abs(d.S - r[d.Track].Length) ? ka : kb;
                var w = s.Wezly.FirstOrDefault(x => x.P[0] == kon[0] && x.P[1] == kon[1]);
                if (w == null || ObrGraf.Przy(s, w.Id).Count != 1) continue;
                var k = ObrSymbole.Nowy(s, ObrSymbole.Koziol, w.Id, 0, 1);
                s.Przypiecia.Add(new ObrPrzypiecie { El = k.Id, Ut = d.Id, Stan = ObrPrzypiecie.Pewne });
                continue;
            }
            string typ, styl = null;
            if (d.Kind == UtDevice.Wykolejnica) typ = ObrSymbole.Wykolejnica;
            else if (d.Kind == UtDevice.Semafor && d.Funkcja == UtDevice.FOdstepowy) continue;      // odstępowe – odstępy SBL szlaku (E6)
            else if (ObrSymbole.ZUt(d) is var (zt, zs)) { typ = zt; styl = zs; }
            else continue;
            if (Na(p) is not var (e, t)) continue;
            var pr = s.Przypiecia.FirstOrDefault(x => x.El == e.Id)?.Tor;
            int kier = pr == null ? 1 : Math.Sign(pr.S1 - pr.S0) == (d.Dir < 0 ? -1 : 1) ? 1 : -1;
            var el = ObrSymbole.Nowy(s, typ, e.Id, t, ObrSymbole.Kierunkowy(typ) ? kier : 1);
            el.Styl = styl ?? el.Styl;
            if (el.Opisy.FirstOrDefault() is ObrOpis op) { op.Tekst = d.Name; op.D = ObrSymbole.OpisDomyslny(s, el); }
            s.Przypiecia.Add(new ObrPrzypiecie { El = el.Id, Ut = d.Id, Stan = ObrPrzypiecie.Pewne });
        }
        foreach (var t in rzedy.Where(t => ut.Tracks.First(x => x.Id == t).Number.Length > 0))
        {
            var (a, b) = konceRzedu[t];
            var m = new[] { ZobTory.Grid((a[0] + b[0]) / 2.0, g), a[1] };
            if (Na(m) is not var (e, tt)) continue;
            var el = ObrSymbole.Nowy(s, ObrSymbole.NumerToru, e.Id, tt, 1);
            el.Opisy[0].Tekst = ut.Tracks.First(x => x.Id == t).Number;
            s.Przypiecia.Add(new ObrPrzypiecie { El = el.Id, Ut = t, Stan = ObrPrzypiecie.Pewne });
        }
        return new ObrGraf.Fragment { Wezly = s.Wezly, Odcinki = s.Odcinki, Elementy = s.Elementy, Przypiecia = s.Przypiecia };
    }

    static double Dl(int[] a, int[] b) => Math.Sqrt(Math.Pow(b[0] - a[0], 2) + Math.Pow(b[1] - a[1], 2));

    /// <summary>Szkic przesunięty o (dx, dy) (przed przyjęciem – plan § 7).</summary>
    public static void Przesun(ObrGraf.Fragment f, int dx, int dy)
    {
        foreach (var w in f.Wezly) w.P = new[] { w.P[0] + dx, w.P[1] + dy };
        foreach (var e in f.Elementy.Where(e => e.P != null)) e.P = new[] { e.P[0] + dx, e.P[1] + dy };
    }

    /// <summary>Obraz tymczasowy z treścią fragmentu (do rysowania szkicu tym samym kodem co obraz).</summary>
    public static ObrObraz JakoObraz(ObrGraf.Fragment f, ObrObraz o) =>
        new() { Id = o.Id, Szer = o.Szer, Wys = o.Wys, Wezly = f.Wezly, Odcinki = f.Odcinki, Elementy = f.Elementy, Przypiecia = f.Przypiecia };
}

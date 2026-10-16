using System.Text.Json.Nodes;

namespace LcsWidzew.Model;

/// <summary>
/// Kształt torów na planie: punkty załamania (bez węzłów), łuki po okręgu przez 3 punkty, łuk styczny do toru, łuk o zadanym
/// promieniu w narożniku między dwoma torami. Geometria liczona w metrach (x / Ux, y / Uy) – w skali rzeczywistej to prawdziwy
/// okrąg, promień w metrach trafia do toru („radius”). Zmienia tylko rysunek planu (i długość, gdy długości są z rysunku).
/// </summary>
public partial class StationDoc
{
    (double X, double Y) ToM((double X, double Y) p) => (p.X / Ux, p.Y / Uy);
    (double X, double Y) FromM((double X, double Y) m) => (Math.Round(m.X * Ux, 2), Math.Round(m.Y * Uy, 2));

    public List<(double X, double Y)> EdgePts(string eid) => Pts(Edge(eid));

    /// <summary>Nowy punkt załamania na odcinku łamanej najbliższym do (x, y).</summary>
    public int InsertEdgePt(string eid, double x, double y)
    {
        var e = Edge(eid); var p = Pts(e);
        int best = 0; double bd = double.MaxValue; (double X, double Y) bp = (x, y);
        for (int i = 0; i < p.Count - 1; i++)
        {
            double dx = p[i + 1].X - p[i].X, dy = p[i + 1].Y - p[i].Y, l2 = dx * dx + dy * dy;
            double t = l2 == 0 ? 0 : Math.Clamp(((x - p[i].X) * dx + (y - p[i].Y) * dy) / l2, 0.02, 0.98);
            double d = Math.Pow(p[i].X + t * dx - x, 2) + Math.Pow(p[i].Y + t * dy - y, 2);
            if (d < bd) { bd = d; best = i; bp = (p[i].X + t * dx, p[i].Y + t * dy); }
        }
        // punkt na torze (rzut kliknięcia) – kształt bez zmian, dopóki punkt nie zostanie przeciągnięty
        p.Insert(best + 1, bp); SetPts(e, p); AutoLen(e);
        return best + 1;
    }

    public void MoveEdgePt(string eid, int idx, double x, double y)
    {
        var e = Edge(eid); var p = Pts(e);
        if (idx <= 0 || idx >= p.Count - 1) throw new InvalidOperationException("To koniec toru – przesuń węzeł");
        p[idx] = (x, y); SetPts(e, p); AutoLen(e);
    }

    public void RemoveEdgePt(string eid, int idx)
    {
        var e = Edge(eid); var p = Pts(e);
        if (idx <= 0 || idx >= p.Count - 1) return;
        p.RemoveAt(idx); SetPts(e, p); AutoLen(e);
    }

    /// <summary>Kształt toru z listy punktów (końce w węzłach toru); długość – z rysunku (nowy tor) albo gdy długości są z rysunku.</summary>
    public void SetEdgeShape(string eid, List<(double X, double Y)> pts, bool newTrack)
    {
        var e = Edge(eid); var a = Pos(Sv(e["a"])); var b = Pos(Sv(e["b"]));
        pts = pts.ToList(); pts[0] = a; pts[^1] = b;
        SetPts(e, pts);
        if (newTrack) e["len"] = NewLen(pts); else AutoLen(e);
    }

    /// <summary>Okrąg przez 3 punkty (w metrach): środek i promień; null – punkty w linii.</summary>
    static ((double X, double Y) C, double R)? Circle((double X, double Y) a, (double X, double Y) b, (double X, double Y) c)
    {
        double d = 2 * (a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y));
        if (Math.Abs(d) < 1e-9) return null;
        double a2 = a.X * a.X + a.Y * a.Y, b2 = b.X * b.X + b.Y * b.Y, c2 = c.X * c.X + c.Y * c.Y;
        var cc = ((a2 * (b.Y - c.Y) + b2 * (c.Y - a.Y) + c2 * (a.Y - b.Y)) / d, (a2 * (c.X - b.X) + b2 * (a.X - c.X) + c2 * (b.X - a.X)) / d);
        return (cc, Math.Sqrt(Math.Pow(a.X - cc.Item1, 2) + Math.Pow(a.Y - cc.Item2, 2)));
    }

    /// <summary>Punkty łuku (w metrach) od kąta a0 o kąt sweep, cięciwa ≤ 4 m (co najmniej 8 odcinków).</summary>
    static List<(double X, double Y)> ArcPts((double X, double Y) c, double r, double a0, double sweep)
    {
        int n = (int)Math.Clamp(Math.Ceiling(Math.Abs(sweep) * r / 4), 8, 720);
        return Enumerable.Range(0, n + 1).Select(i => { double t = a0 + sweep * i / n; return (c.X + r * Math.Cos(t), c.Y + r * Math.Sin(t)); }).ToList();
    }

    /// <summary>Łuk przez 3 punkty planu (A – początek, M – punkt na łuku, B – koniec): punkty w jednostkach planu i promień [m].</summary>
    public (List<(double X, double Y)> Pts, double R) Arc3((double X, double Y) A, (double X, double Y) M, (double X, double Y) B)
    {
        var a = ToM(A); var m = ToM(M); var b = ToM(B);
        if (Circle(a, m, b) is not { } c) return (new() { A, B }, 0);
        double ang((double X, double Y) p) => Math.Atan2(p.Y - c.C.Y, p.X - c.C.X);
        double t0 = ang(a), tm = ang(m), t1 = ang(b);
        double Norm(double x) { while (x < 0) x += 2 * Math.PI; while (x >= 2 * Math.PI) x -= 2 * Math.PI; return x; }
        // przeciwnie do wskazówek: od a do b przez m albo zgodnie z nimi
        double ccw = Norm(t1 - t0), mid = Norm(tm - t0);
        double sweep = mid <= ccw ? ccw : ccw - 2 * Math.PI;
        return (ArcPts(c.C, c.R, t0, sweep).Select(FromM).ToList(), c.R);
    }

    /// <summary>Łuk styczny: od A w kierunku t (jednostki planu) do B. Punkty planu i promień [m]; w linii – prosta.</summary>
    public (List<(double X, double Y)> Pts, double R) ArcTangent((double X, double Y) A, (double X, double Y) dirPlan, (double X, double Y) B)
    {
        var a = ToM(A); var b = ToM(B); var t = (X: dirPlan.X / Ux, Y: dirPlan.Y / Uy);
        double tl = Math.Sqrt(t.X * t.X + t.Y * t.Y); if (tl < 1e-9) return (new() { A, B }, 0);
        t = (t.X / tl, t.Y / tl);
        var n = (X: -t.Y, Y: t.X); var d = (X: b.X - a.X, Y: b.Y - a.Y);
        double dn = d.X * n.X + d.Y * n.Y, dd = d.X * d.X + d.Y * d.Y;
        if (Math.Abs(dn) < 1e-6) return (new() { A, B }, 0);
        double r = dd / (2 * dn);                       // ze znakiem: >0 – środek po stronie n
        var c = (X: a.X + n.X * r, Y: a.Y + n.Y * r);
        double R = Math.Abs(r), t0 = Math.Atan2(a.Y - c.Y, a.X - c.X), t1 = Math.Atan2(b.Y - c.Y, b.X - c.X);
        // kierunek obiegu: rosnący kąt, gdy styczna rosnącego kąta w A zgadza się z kierunkiem t; inaczej malejący
        int sgn = (-Math.Sin(t0)) * t.X + Math.Cos(t0) * t.Y >= 0 ? 1 : -1;
        double sweep = (t1 - t0) * sgn;
        while (sweep <= 0) sweep += 2 * Math.PI; while (sweep > 2 * Math.PI) sweep -= 2 * Math.PI;
        sweep *= sgn;
        return (ArcPts(c, R, t0, sweep).Select(FromM).ToList(), R);
    }

    /// <summary>Kierunek jazdy „wychodzący” z węzła na przedłużeniu toru, który się w nim kończy (do łuku stycznego); null – brak jednego toru.</summary>
    public (double X, double Y)? TangentAt(string nid)
    {
        var at = EdgesAt(nid); if (at.Count != 1) return null;
        var p = Pts(at[0]); if (Sv(at[0]["a"]) == nid) p.Reverse();     // ... → węzeł
        var end = p[^1];
        // tor-łuk: styczna z okręgu przez początek, środek i koniec (punkty pośrednie są zaokrąglone – krótki odcinek dałby błąd kierunku)
        if (p.Count >= 5 && D(at[0]["radius"]) > 0 && Circle(ToM(p[0]), ToM(p[p.Count / 2]), ToM(end)) is { } c)
        {
            var em = ToM(end); var rad = (X: em.X - c.C.X, Y: em.Y - c.C.Y);
            var tan = (X: -rad.Y, Y: rad.X); var chord = ToM(p[^2]);
            if (tan.X * (em.X - chord.X) + tan.Y * (em.Y - chord.Y) < 0) tan = (-tan.X, -tan.Y);
            return (tan.X * Ux, tan.Y * Uy);
        }
        var q = p[^2];
        return (end.X - q.X, end.Y - q.Y);
    }

    /// <summary>
    /// Łuk o promieniu R [m] w narożniku: węzeł zwykły z dwoma torami zostaje na środku łuku, tory dochodzą do niego po łuku
    /// stycznym do obu kierunków (długość styczna T = R·tg(δ/2)). Promień zapisany w obu torach.
    /// </summary>
    public string FilletNode(string nid, double R)
    {
        if (R <= 0) throw new InvalidOperationException("Promień musi być dodatni");
        var at = EdgesAt(nid);
        if (at.Count != 2 || Switches.Any(s => Sv(s.Value["node"]) == nid)) throw new InvalidOperationException("Łuk w narożniku – tylko w węźle z dwoma torami (bez zwrotnicy)");
        var e1 = at[0]; var e2 = at[1];
        List<(double X, double Y)> Toward(JsonObject e) { var p = Pts(e); if (Sv(e["a"]) == nid) p.Reverse(); return p; }   // ... → węzeł
        var p1 = Toward(e1); var p2 = Toward(e2);
        var P = ToM(Pos(nid)); var A = ToM(p1[^2]); var B = ToM(p2[^2]);
        (double X, double Y) U((double X, double Y) v) { double l = Math.Sqrt(v.X * v.X + v.Y * v.Y); return (v.X / l, v.Y / l); }
        var u1 = U((A.X - P.X, A.Y - P.Y)); var u2 = U((B.X - P.X, B.Y - P.Y));
        double cosT = Math.Clamp(u1.X * u2.X + u1.Y * u2.Y, -1, 1), theta = Math.Acos(cosT);
        if (theta > Math.PI - 1e-3) throw new InvalidOperationException("Tory w linii prostej – nie ma narożnika");
        if (theta < 1e-3) throw new InvalidOperationException("Tory zawracają – łuk niemożliwy");
        double T = R / Math.Tan(theta / 2);
        double l1 = Math.Sqrt(Math.Pow(A.X - P.X, 2) + Math.Pow(A.Y - P.Y, 2)), l2 = Math.Sqrt(Math.Pow(B.X - P.X, 2) + Math.Pow(B.Y - P.Y, 2));
        if (T >= l1 - 0.5 || T >= l2 - 0.5) throw new InvalidOperationException($"Za duży promień: styczna {T:0.#} m, a proste odcinki przy narożniku mają {l1:0.#} m i {l2:0.#} m – zmniejsz promień albo wydłuż tory");
        var T1 = (X: P.X + u1.X * T, Y: P.Y + u1.Y * T); var T2 = (X: P.X + u2.X * T, Y: P.Y + u2.Y * T);
        var bis = U((u1.X + u2.X, u1.Y + u2.Y)); double dc = R / Math.Sin(theta / 2);
        var C = (X: P.X + bis.X * dc, Y: P.Y + bis.Y * dc);
        double a1 = Math.Atan2(T1.Y - C.Y, T1.X - C.X), a2 = Math.Atan2(T2.Y - C.Y, T2.X - C.X);
        double sweep = a2 - a1; while (sweep > Math.PI) sweep -= 2 * Math.PI; while (sweep < -Math.PI) sweep += 2 * Math.PI;
        var arc = ArcPts(C, R, a1, sweep);                  // T1 → T2
        int mid = arc.Count / 2; var Mm = arc[mid];
        var newPos = FromM(Mm);
        // kolejność węzłów w poziomie bez zmian (kierunek jazdy torów)
        var o1 = Pos(Sv(e1["a"]) == nid ? Sv(e1["b"]) : Sv(e1["a"])); var o2 = Pos(Sv(e2["a"]) == nid ? Sv(e2["b"]) : Sv(e2["a"])); var old = Pos(nid);
        foreach (var o in new[] { o1, o2 })
            if (Math.Abs(o.X - old.X) >= 0.5 && Math.Sign(o.X - newPos.X) != Math.Sign(o.X - old.X)) throw new InvalidOperationException("Łuk odwróciłby kierunek toru – za duży promień");
        var q1 = p1.Take(p1.Count - 1).Append(FromM(T1)).Concat(arc.Skip(1).Take(mid).Select(FromM)).ToList();                 // ... → środek łuku
        var q2 = p2.Take(p2.Count - 1).Append(FromM(T2)).Concat(arc.Skip(mid).Reverse().Skip(1).Select(FromM)).ToList();       // ... → środek łuku
        Nodes[nid]["x"] = newPos.X; Nodes[nid]["y"] = newPos.Y;
        foreach (var (e, q) in new[] { (e1, q1), (e2, q2) })
        {
            if (Sv(e["a"]) == nid) q.Reverse();
            SetPts(e, q); e["radius"] = Math.Round(R); AutoLen(e);
        }
        return $"łuk R = {R:0} m, styczne {T:0.#} m, długość łuku {R * (Math.PI - theta):0.#} m";
    }

    /// <summary>
    /// Połączenie torów: węzeł „from” (upuszczony) zlewa się z węzłem „into” – tory z „from” kończą się w „into”.
    /// Dozwolone, gdy razem będzie najwyżej 3 tory, węzły nie są już połączone torem, a w „from” nie ma zwrotnicy,
    /// sygnalizatora, kozła, wykolejnicy ani granicy szlaku. Zwraca opis; błąd – wyjątek.
    /// </summary>
    public string WeldNodes(string from, string into)
    {
        if (from == into) throw new InvalidOperationException("To ten sam węzeł");
        var ef = EdgesAt(from); var ei = EdgesAt(into);
        if (ef.Any(e => Sv(e["a"]) == into || Sv(e["b"]) == into)) throw new InvalidOperationException("Węzły są już połączone torem – usuń ten tor albo scal węzeł (⊕ Scal tory)");
        if (ef.Count + ei.Count > 3) throw new InvalidOperationException($"Razem byłoby {ef.Count + ei.Count} torów w węźle – najwyżej 3 (zwrotnica)");
        bool Busy(string n) => Switches.Any(x => Sv(x.Value["node"]) == n) || Signals.Any(x => Sv(x.Value["node"]) == n) || Derailers.Any(x => Sv(x.Value["node"]) == n)
            || TrackEnds.ContainsKey(n) || Boundaries.ContainsKey(n) || FarEnds.ContainsKey(n) || Ports.ContainsKey(n);
        if (Busy(from)) throw new InvalidOperationException("W przeciąganym węźle jest zwrotnica, sygnalizator, kozioł, wykolejnica albo granica szlaku – przeciągnij drugi koniec");
        var (ix, iy) = Pos(into);
        foreach (var e in ef)
        {
            var p = Pts(e);
            if (Sv(e["a"]) == from) { e["a"] = into; p[0] = (ix, iy); } else { e["b"] = into; p[^1] = (ix, iy); }
            SetPts(e, p); AutoLen(e);
            // zobrazowanie: koniec toru w węźle docelowym (gdy jest na obrazie), inaczej tor zdjęty z obrazu
            if (DPts(Sv(e["id"])) is { } dp)
            {
                if (DPos(into) is { } q) { if (Sv(e["a"]) == into) dp[0] = q; else dp[^1] = q; SetDPts(Sv(e["id"]), dp); }
                else DispEdges.Remove(Sv(e["id"]));
            }
        }
        Bumpers.Remove(from);
        Nodes.Remove(from); if (Disp != null) DispNodes.Remove(from);
        int n = EdgesAt(into).Count;
        return n == 3 ? "3 tory w węźle – wstaw zwrotnicę (Z) albo popraw połączenie" : $"tory połączone ({n} w węźle)";
    }

    /// <summary>
    /// Łamana równoległa w odległości d [m] (strona: +1 / -1 względem normalnej (-ty, tx) w metrach) – tor obok w zadanym
    /// międzytorzu. Wierzchołki przesunięte wzdłuż dwusiecznej (łuk – promień R ± d).
    /// </summary>
    public List<(double X, double Y)> OffsetPolyline(IList<(double X, double Y)> pts, double d, int side)
    {
        var m = pts.Select(ToM).ToList(); var res = new List<(double X, double Y)>();
        (double X, double Y) N(int i) { double dx = m[i + 1].X - m[i].X, dy = m[i + 1].Y - m[i].Y, l = Math.Sqrt(dx * dx + dy * dy); return l < 1e-9 ? (0, 0) : (-dy / l, dx / l); }
        for (int i = 0; i < m.Count; i++)
        {
            var na = i > 0 ? N(i - 1) : N(0); var nb = i < m.Count - 1 ? N(i) : N(m.Count - 2);
            var bx = na.X + nb.X; var by = na.Y + nb.Y; double bl = Math.Sqrt(bx * bx + by * by);
            if (bl < 1e-9) { bx = nb.X; by = nb.Y; bl = 1; }
            bx /= bl; by /= bl;
            double k = Math.Clamp(bx * nb.X + by * nb.Y, 0.2, 1);       // cos połowy kąta załamania
            res.Add(FromM((m[i].X + side * bx * d / k, m[i].Y + side * by * d / k)));
        }
        return res;
    }

    /// <summary>Promień łuku [m], gdy punkty łamanej leżą na okręgu (odchyłka &lt; 0,3 m); null – nie łuk.</summary>
    public double? FitRadius(IList<(double X, double Y)> pts)
    {
        if (pts.Count < 5) return null;
        var m = pts.Select(ToM).ToList();
        if (Circle(m[0], m[m.Count / 2], m[^1]) is not { } c || c.R > 50000) return null;
        return m.All(p => Math.Abs(Math.Sqrt(Math.Pow(p.X - c.C.X, 2) + Math.Pow(p.Y - c.C.Y, 2)) - c.R) < 0.3) ? Math.Round(c.R) : null;
    }
}

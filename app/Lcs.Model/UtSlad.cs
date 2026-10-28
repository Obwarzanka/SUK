namespace Lcs.Model;

/// <summary>Próbka krzywizny śladu: km wzdłuż śladu [m w terenie], krzywizna [1/m w terenie, + w prawo], punkt śladu (w układzie).</summary>
public record SladK(double S, double K, XY P);

/// <summary>
/// Łuk rozpoznany na śladzie z wykresu krzywizny (propozycja do sprawdzenia): km śladu od–do całej krzywej (z przejściowymi) [m w terenie],
/// R [m w terenie, + w prawo], przejściowe na wejściu i wyjściu L1 / L2 [m], kąt zwrotu [rad, + w prawo].
/// </summary>
public record SladLuk(double S0, double S1, double R, double L1, double L2, double Kat)
{
    public double Lc => Math.Max(0, S1 - S0 - L1 - L2);
}

/// <summary>Tor po śladzie: trasa z elementów, odchyłki punktów [m w terenie] (pominięte – osobno), błąd, przeskoki śladu na sąsiedni tor.</summary>
public record SladTor(UtAlign A, double Rms, double Max, List<double> Dev, HashSet<int> Pominiete, string Error, List<SladPrzeskok> Przeskoki = null)
{
    /// <summary>Baza krzywizny, z którą wyszła propozycja [m].</summary>
    public double Baza { get; init; }
}

/// <summary>
/// Ciągły odcinek, na którym tor i ślad się rozchodzą: punkty od–do (indeksy śladu), km śladu od–do [m w terenie], największa
/// odchyłka [m]; <paramref name="Es"/> – tor skręca tam esem w bok (przejście przez połączenie torów – prawie na pewno przeskok śladu
/// na sąsiedni tor), inaczej punkty śladu daleko od toru (przeskok albo łuk, którego dopasowanie nie złapało).
/// </summary>
public record SladPrzeskok(int I0, int I1, double S0, double S1, double Max, bool Es = false);

/// <summary>
/// Ślad osi toru: łamana punktów z OpenStreetMap (OpenRailwayMap) albo kliknięta / namalowana na zdjęciu. Z niego: krzywizna wzdłuż
/// śladu (kąt między cięciwami długości bazy), rozpoznanie łuków (R, L, kąt – poziome odcinki wykresu to łuki, pochyłe – krzywe przejściowe),
/// szkic trasy z elementów i dopasowanie jej do punktów (początek i azymut też swobodne). Tor po śladzie to propozycja – projektant
/// ją sprawdza i poprawia (PLAN_EDYTOR_UT.md § 5h). Punkty w układzie (PUWG 1992), długości w terenie = w układzie / k.
/// </summary>
public static class Slad
{
    /// <summary>Domyślna baza krzywizny [m]: długość każdej z dwóch cięciw.</summary>
    public const double Baza = 20;
    /// <summary>Krzywizna mniejsza niż 1 / <see cref="RProsta"/> to prosta (szum OSM i klikania).</summary>
    public const double RProsta = 6000;

    /// <summary>Długość łamanej [m w układzie].</summary>
    public static double Length(IReadOnlyList<XY> pts) { double s = 0; for (int i = 1; i < pts.Count; i++) s += XY.Dist(pts[i - 1], pts[i]); return s; }

    /// <summary>Łamana co <paramref name="step"/> [m w układzie] (punkty na odcinkach), bez powtórzeń w miejscu.</summary>
    public static List<XY> Resample(IReadOnlyList<XY> pts, double step)
    {
        var res = new List<XY>(); if (pts.Count == 0) return res;
        res.Add(pts[0]); double carry = 0;
        for (int i = 1; i < pts.Count; i++)
        {
            var a = pts[i - 1]; var d = pts[i] - a; double len = d.Length; if (len < 1e-9) continue;
            double t = step - carry;
            while (t <= len + 1e-9) { res.Add(a + d * (t / len)); t += step; }
            carry = len - (t - step);
        }
        if (XY.Dist(res[^1], pts[^1]) > step * 0.3) res.Add(pts[^1]);
        return res;
    }

    /// <summary>
    /// Rozrzut punktów śladu w poprzek [m w terenie] (σ z mediany odległości punktu od cięciwy sąsiadów) i średni odstęp punktów [m].
    /// </summary>
    public static (double Sigma, double Step) Rozrzut(IReadOnlyList<XY> pts, double k)
    {
        if (pts.Count < 3) return (0, 1);
        var e = new List<double>(); var st = new List<double>();
        for (int i = 1; i + 1 < pts.Count; i++)
        {
            var a = pts[i - 1]; var d = pts[i + 1] - a; double l = d.Length; if (l < 1e-9) continue;
            var w = pts[i] - a; e.Add(Math.Abs(w.X * d.Y - w.Y * d.X) / l / k);
        }
        for (int i = 1; i < pts.Count; i++) st.Add(XY.Dist(pts[i - 1], pts[i]) / k);
        if (e.Count == 0) return (0, 1);
        e.Sort(); st.Sort();
        // odległość od cięciwy = szum środkowego punktu i połowa szumu końców: σ·√1,5
        return (1.4826 * e[e.Count / 2] / Math.Sqrt(1.5), Math.Max(1, st[st.Count / 2]));
    }

    /// <summary>
    /// Baza krzywizny dobrana do śladu: szum wykresu (kierunek z regresji w oknie bazy, krzywizna z różnicy kierunków) mniejszy niż
    /// połowa progu prostej – √2·σ·√(12·Δ) / b^2,5 ≤ 1 / (2·<see cref="RProsta"/>); od 15 do 100 m.
    /// </summary>
    public static double BazaAuto(IReadOnlyList<XY> pts, double k)
    {
        var (sigma, step) = Rozrzut(pts, k);
        double num = Math.Sqrt(2) * Math.Max(sigma, 0.01) * Math.Sqrt(12 * step) * 2 * RProsta;
        return Math.Round(Math.Clamp(Math.Pow(num, 0.4), 15, 100));
    }

    /// <summary>
    /// Krzywizna wzdłuż śladu: punkty co 1 m; kierunek śladu w każdym miejscu z prostej najlepiej dopasowanej do punktów w oknie długości
    /// bazy (regresja – szum się uśrednia); krzywizna = różnica kierunków w oknach przed i za punktem przez bazę (dla okręgu dokładnie 1/R).
    /// Na początku i końcu śladu (jedna baza) – bez wartości.
    /// </summary>
    public static List<SladK> Krzywizna(IReadOnlyList<XY> pts, double k, double baza = Baza)
    {
        var res = new List<SladK>();
        if (pts.Count < 2) return res;
        var p = Resample(pts, k);                 // co 1 m w terenie
        int h = Math.Max(2, (int)Math.Round(baza / 2)), n = p.Count;
        if (n < 4 * h + 1) return res;
        // sumy prefiksowe (względem pierwszego punktu – bez utraty dokładności przy współrzędnych 10⁵ m)
        var o = p[0];
        var sx = new double[n + 1]; var sy = new double[n + 1]; var sxx = new double[n + 1]; var syy = new double[n + 1]; var sxy = new double[n + 1];
        for (int i = 0; i < n; i++)
        {
            double x = p[i].X - o.X, y = p[i].Y - o.Y;
            sx[i + 1] = sx[i] + x; sy[i + 1] = sy[i] + y; sxx[i + 1] = sxx[i] + x * x; syy[i + 1] = syy[i] + y * y; sxy[i + 1] = sxy[i] + x * y;
        }
        // kierunek osi głównej punktów od a do b włącznie, zwrócony zgodnie z kolejnością
        double Heading(int a, int b)
        {
            int c = b - a + 1;
            double mx = (sx[b + 1] - sx[a]) / c, my = (sy[b + 1] - sy[a]) / c;
            double cxx = (sxx[b + 1] - sxx[a]) / c - mx * mx, cyy = (syy[b + 1] - syy[a]) / c - my * my, cxy = (sxy[b + 1] - sxy[a]) / c - mx * my;
            double th = 0.5 * Math.Atan2(2 * cxy, cxx - cyy);
            var d = p[b] - p[a]; if (Math.Cos(th) * d.X + Math.Sin(th) * d.Y < 0) th += Math.PI;
            return th;
        }
        var head = new double[n];
        for (int i = h; i < n - h; i++) head[i] = Heading(i - h, i + h);
        for (int i = 2 * h; i < n - 2 * h; i++)
        {
            double da = Math.IEEERemainder(head[i + h] - head[i - h], 2 * Math.PI);
            res.Add(new SladK(i, da / (2 * h), p[i]));          // kąt na 2h m w terenie
        }
        return res;
    }

    /// <summary>
    /// Łuki z wykresu krzywizny: kawałki, gdzie |krzywizna| &gt; 1 / <see cref="RProsta"/> w jedną stronę (krótkie przerwy łączone);
    /// kąt zwrotu = całka krzywizny, R z poziomu wykresu (mediana najwyższej części), przejściowe z szerokości zboczy (po odjęciu
    /// rozmycia bazy), położenie – środek ciężkości krzywizny. Wartości przybliżone – dokładnie liczy <see cref="TorPoSladzie"/>.
    /// </summary>
    public static List<SladLuk> Luki(IReadOnlyList<SladK> kr, double baza = Baza)
    {
        var res = new List<SladLuk>(); int n = kr.Count; if (n < 3) return res;
        double kMin = 1 / RProsta;
        int i = 0;
        while (i < n)
        {
            if (Math.Abs(kr[i].K) <= kMin) { i++; continue; }
            int sg = Math.Sign(kr[i].K), a = i, b = i;
            // do końca kawałka w tę stronę; przerwa krótsza od bazy – ten sam łuk
            int j = i;
            while (j < n)
            {
                if (Math.Sign(kr[j].K) == sg && Math.Abs(kr[j].K) > kMin) { b = j; j++; continue; }
                int g = j; while (g < n && !(Math.Sign(kr[g].K) == sg && Math.Abs(kr[g].K) > kMin)) { if (Math.Sign(kr[g].K) == -sg && Math.Abs(kr[g].K) > kMin) break; g++; }
                if (g < n && Math.Sign(kr[g].K) == sg && kr[g].S - kr[b].S < baza) { j = g; continue; }
                break;
            }
            // rozszerzenie do miejsc, gdzie krzywizna spada prawie do zera
            while (a > 0 && Math.Sign(kr[a - 1].K) == sg && Math.Abs(kr[a - 1].K) > kMin / 4) a--;
            while (b < n - 1 && Math.Sign(kr[b + 1].K) == sg && Math.Abs(kr[b + 1].K) > kMin / 4) b++;
            i = b + 1;
            double ds = kr.Count > 1 ? kr[1].S - kr[0].S : 1, alpha = 0, mom = 0, peak = 0;
            for (int q = a; q <= b; q++) { alpha += kr[q].K * ds; mom += kr[q].S * kr[q].K * ds; peak = Math.Max(peak, Math.Abs(kr[q].K)); }
            if (Math.Abs(alpha) < 0.5 * Math.PI / 180) continue;            // mniej niż 0,5° – szum
            var top = new List<double>(); for (int q = a; q <= b; q++) if (Math.Abs(kr[q].K) >= 0.7 * peak) top.Add(Math.Abs(kr[q].K));
            top.Sort(); double kp = top[top.Count / 2];
            double lsum = Math.Abs(alpha) / kp;                               // Lc + L (przy równych przejściowych)
            int w85 = 0; for (int q = a; q <= b; q++) if (Math.Abs(kr[q].K) >= 0.85 * kp) w85++;
            int w50 = 0; for (int q = a; q <= b; q++) if (Math.Abs(kr[q].K) >= 0.5 * kp) w50++;
            // zbocze trapezu (przejściowa + rozmycie bazy): od 50 % do 85 % poziomu to 35 % zbocza po każdej stronie
            double ramp = Math.Max(0, (w50 - w85) * ds / 2 / 0.35);
            double l = Math.Max(0, ramp - 1.1 * baza);
            if (l < 8) l = 0;
            if (lsum - l < 2) l = Math.Max(0, lsum - 2);
            if (1 / kp > RProsta / 2 && Math.Abs(alpha) < Math.PI / 180) continue;   // bardzo duży R i kąt poniżej 1° – szum
            double sm = mom / alpha, tot = lsum + l;
            res.Add(new SladLuk(Math.Round(sm - tot / 2, 1), Math.Round(sm + tot / 2, 1), Math.Round(sg / kp), Math.Round(l), Math.Round(l), alpha));
        }
        return res;
    }

    /// <summary>
    /// Szkic trasy z elementów ze śladu i rozpoznanych łuków: proste między łukami (co najmniej 1 m), przejściowe, łuki; początek w pierwszym
    /// punkcie śladu, azymut z pierwszej prostej.
    /// </summary>
    public static UtAlign Szkic(IReadOnlyList<XY> pts, double k, IReadOnlyList<SladLuk> luki)
    {
        var p = Resample(pts, k); double len = (p.Count - 1);         // co 1 m – indeks = km
        XY At(double s) => p[Math.Clamp((int)Math.Round(s), 0, p.Count - 1)];
        double s1 = luki.Count > 0 ? Math.Max(2, Math.Min(luki[0].S0, 60)) : Math.Min(len, 60);
        var d = At(s1) - p[0];
        var a = new UtAlign { X = p[0].X, Y = p[0].Y, Az = Math.Atan2(d.Y, d.X) };
        double cur = 0;
        foreach (var l in luki)
        {
            double st = Math.Max(1, l.S0 - cur);
            a.Els.Add(new UtAlignEl { Len = Math.Round(st, 2) });
            double lc = Math.Max(2, l.Lc);
            if (l.L1 > 0) a.Els.Add(new UtAlignEl { Len = l.L1, R0 = 0, R1 = l.R });
            a.Els.Add(new UtAlignEl { Len = Math.Round(lc, 2), R0 = l.R, R1 = l.R });
            if (l.L2 > 0) a.Els.Add(new UtAlignEl { Len = l.L2, R0 = l.R, R1 = 0 });
            cur = Math.Max(cur + st, l.S0) + l.L1 + lc + l.L2;
        }
        a.Els.Add(new UtAlignEl { Len = Math.Round(Math.Max(1, len - cur), 2) });
        return a;
    }

    /// <summary>
    /// Dopasowanie trasy z elementów do punktów, z początkiem i azymutem swobodnymi (przesunięcie w poprzek pierwszej prostej i obrót):
    /// długości wszystkich elementów i promienie łuków – Levenberg–Marquardt na odległościach punktów od osi ze znakiem.
    /// </summary>
    public static (UtAlign A, double Rms, double Max, List<double> Dev, string Error) Dopasuj(UtAlign a, double k, IReadOnlyList<XY> pts)
    {
        if (pts.Count < 3) return (null, 0, 0, null, "za mało punktów śladu");
        var res = Lm(a, k, pts, true);
        // R do pełnych metrów, potem jeszcze raz długości i początek przy stałych R (zaokrąglenie R przesuwa dalszą część trasy)
        foreach (var e in res.Els) if (e.Luk) e.R0 = e.R1 = Math.Round(e.R0);
        TrasaElem.Normalize(res);
        res = Lm(res, k, pts, false);
        foreach (var e in res.Els) e.Len = Math.Round(e.Len, 2);
        res.X = Math.Round(res.X, 3); res.Y = Math.Round(res.Y, 3);
        TrasaElem.Normalize(res);
        var rr = TrasaElem.Compute(res, k); if (!rr.Ok) return (null, 0, 0, null, "nie udało się dopasować trasy do śladu");
        var dev = pts.Select(q => Budowa.Dist(rr, q) / k).ToList();
        return (res, Math.Sqrt(dev.Sum(x => x * x) / dev.Count), dev.Max(), dev, null);
    }

    /// <summary>
    /// Levenberg–Marquardt: przesunięcie początku w poprzek, obrót, długości elementów i (gdy <paramref name="withR"/>) promienie łuków.
    /// Ostatnia prosta w czasie dopasowania dłuższa o 50 m (punkty za jej końcem nie ciągną), potem przycięta do rzutu ostatniego punktu.
    /// Poza odległością punktu od osi liczy się też kilometraż: najbliższy punkt osi ma leżeć mniej więcej tyle od początku, ile punkt
    /// od początku śladu (<see cref="KmTol"/>) – inaczej trasa z pętlą albo zawrócona obok śladu też „pasuje” (blisko punktów, a zupełnie
    /// nie tam). Promienie od <see cref="RMin"/> do <see cref="RMax"/>, elementy nie dłuższe niż ślad.
    /// </summary>
    static UtAlign Lm(UtAlign a, double k, IReadOnlyList<XY> pts, bool withR)
    {
        bool lastStraight = a.Els.Count > 1 && a.Els[^1].Prosta;
        var vars = new List<(int I, bool R)>();
        for (int i = 0; i < a.Els.Count; i++) { if (!(lastStraight && i == a.Els.Count - 1)) vars.Add((i, false)); if (withR && a.Els[i].Luk) vars.Add((i, true)); }
        int n = vars.Count + 2;
        var nrm = Element.Right(a.Az);
        double maxLen = Length(pts) / k + 100;
        var sp = new double[pts.Count];
        for (int i = 1; i < pts.Count; i++) sp[i] = sp[i - 1] + XY.Dist(pts[i - 1], pts[i]) / k;
        UtAlign Make(double[] x)
        {
            var c = a.Clone();
            c.X = a.X + nrm.X * x[0] * k; c.Y = a.Y + nrm.Y * x[0] * k; c.Az = a.Az + x[1] / 1000;
            for (int j = 0; j < vars.Count; j++)
            {
                var (i, isR) = vars[j];
                if (isR) { double r = Math.Exp(x[j + 2]) * Math.Sign(a.Els[i].R0); c.Els[i].R0 = c.Els[i].R1 = r; }
                else c.Els[i].Len = Math.Exp(x[j + 2]);
            }
            if (lastStraight) c.Els[^1].Len = a.Els[^1].Len + 50;
            TrasaElem.Normalize(c); return c;
        }
        double[] Res(double[] x)
        {
            var c = Make(x); var bad = Enumerable.Repeat(1e3, 2 * pts.Count + 1).ToArray();
            if (c.Els.Any(e => e.Len < 0.2 || e.Len > maxLen || (!e.Prosta && Math.Max(Math.Abs(e.R0), Math.Abs(e.R1)) is var rr && (rr < RMin || rr > RMax)))) return bad;
            var r = TrasaElem.Compute(c, k); if (!r.Ok) return bad;
            var res = new double[2 * pts.Count + 1];
            for (int i = 0; i < pts.Count; i++)
            {
                res[i] = Budowa.SignedDistS(r, pts[i], out double s);
                // kilometraż: poza tolerancją reszta rośnie – pętla albo trasa obok śladu nie przejdzie
                double ex = Math.Abs(s - sp[i]) - KmTol(sp[i]);
                res[pts.Count + i] = ex > 0 ? ex * k * 0.2 : 0;
            }
            // elementy za ostatnim punktem nic by nie kosztowały i dopasowanie mogłoby je dowolnie wydłużyć: wszystko przed ostatnią
            // prostą kończy się przed ostatnim punktem śladu, a trasa bez prostej na końcu – tam, gdzie ślad
            double sEnd = lastStraight ? c.Els.Take(c.Els.Count - 1).Sum(e => e.Len) : c.Els.Sum(e => e.Len);
            double over = lastStraight ? sEnd - sp[^1] : Math.Abs(sEnd - sp[^1]) - KmTol(sp[^1]);
            res[^1] = over > 0 ? over * k * Math.Sqrt(pts.Count) * 0.2 : 0;
            return res;
        }
        var x0 = new double[n];
        for (int j = 0; j < vars.Count; j++) { var (i, isR) = vars[j]; x0[j + 2] = Math.Log(isR ? Math.Abs(a.Els[i].R0) : Math.Max(a.Els[i].Len, 0.5)); }
        double Cost(double[] x) => Res(x).Sum(q => q * q);
        var best = Budowa.LevMar(Res, x0, n, 100);
        best = Budowa.LevMar(Res, best, n, 60);          // drugi przebieg od wyniku (źle uwarunkowane – długości przesuwają dalszą część)
        if (Cost(best) > Cost(x0)) best = x0;
        var res = Make(best);
        if (lastStraight)
        {
            var r0 = TrasaElem.Compute(res, k); var le = r0.Elements[^1]; var w = pts[^1] - le.Start; var dz = Element.Dir(le.Az0);
            res.Els[^1].Len = Math.Max(1, (w.X * dz.X + w.Y * dz.Y) / k);
        }
        return res;
    }

    /// <summary>
    /// Punkty odstające (np. węzeł OSM przy rozjeździe, pomyłka kliknięcia): odchyłka większa niż 3,5 σ (σ z mediany odchyłek)
    /// i niż <paramref name="min"/> [m]. Gdy takich jest więcej niż ćwierć punktów, to nie pojedyncze błędy, tylko zły kształt –
    /// wtedy żaden. Do 5 punktów – żaden.
    /// </summary>
    public static HashSet<int> Odstajace(IReadOnlyList<double> dev, double min = 0.3)
    {
        var res = new HashSet<int>(); if (dev.Count < 6) return res;
        var s = dev.Select(Math.Abs).OrderBy(x => x).ToList();
        double sigma = 1.4826 * s[s.Count / 2], thr = Math.Max(min, 3.5 * sigma);
        for (int i = 0; i < dev.Count; i++) if (Math.Abs(dev[i]) > thr) res.Add(i);
        if (res.Count > dev.Count / 4) res.Clear();
        return res;
    }

    /// <summary>Granice promienia łuku w dopasowaniu [m w terenie]: najmniejszy jak tor zwrotny rozjazdu (warunki techniczne § 24).</summary>
    public const double RMin = Rozjazdy.MinRDiv, RMax = 100000;
    /// <summary>Dopuszczalna różnica kilometrażu punktu na trasie i na śladzie [m] (cięciwy śladu są krótsze od łuków, szum wydłuża).</summary>
    static double KmTol(double s) => 10 + 0.02 * s;

    /// <summary>Do dopasowania najwyżej tyle punktów (równo przerzedzone) – czas obliczeń.</summary>
    public const int MaxPkt = 200;

    /// <summary>Mniejsze bazy krzywizny próbowane, gdy z dobraną tor nie trzyma się śladu [m] (nie mniej niż połowa dobranej – mniejsza
    /// robi łuki z szumu, a dopasowanie wielu elementów trwa minutami).</summary>
    static readonly double[] BazyProby = { 40, 30 };

    /// <summary>
    /// Tor po śladzie: krzywizna → łuki → szkic → dopasowanie; punkty odstające pominięte i dopasowanie powtórzone; przejściowe krótsze
    /// niż 3 m usunięte. Wynik to propozycja trasy z elementów. Baza 0 – dobrana do śladu (<see cref="BazaAuto"/>); gdy z nią tor
    /// wyraźnie nie trzyma się śladu (brak wyniku, odchyłka ponad 3 rozrzuty punktów, ciąg odległych punktów), także mniejsze
    /// (<see cref="BazyProby"/>): dobrana z rozrzutu potrafi rozmyć krótką prostą między dwoma łukami w jeden łuk (zgłoszenie 2026-10-27,
    /// Andrzejów: R 900 i R 430 z prostą 90 m przy bazie 58 m – tor do 4 m obok śladu). Bierze się wynik z najmniejszą odchyłką
    /// (z karą za pominięte punkty i ostrzeżenia), a przy prawie równych – z największą bazą.
    /// </summary>
    public static SladTor TorPoSladzie(IReadOnlyList<XY> pts, double k, double baza = 0)
    {
        if (baza > 0 || pts.Count < 3) return TorPoSladzie1(pts, k, baza > 0 ? baza : Baza);
        double auto = BazaAuto(pts, k);
        var first = TorPoSladzie1(pts, k, auto);
        double sigma = Rozrzut(pts, k).Sigma;
        bool zle = first.A == null || first.Rms > Math.Max(0.15, 3 * sigma) || (first.Przeskoki?.Any(p => !p.Es) ?? false);
        var bazy = BazyProby.Where(b => b < auto - 5 && b >= auto / 2).ToArray();
        if (!zle || bazy.Length == 0) return first;
        var wyn = new SladTor[bazy.Length];
        Parallel.For(0, bazy.Length, i => wyn[i] = TorPoSladzie1(pts, k, bazy[i]));
        var ok = new[] { first }.Concat(wyn).Where(w => w.A != null).ToList();
        if (ok.Count == 0) return first;
        double Ocena(SladTor w) => w.Rms + 0.5 * w.Pominiete.Count / Math.Max(1, pts.Count) + 0.3 * (w.Przeskoki?.Count ?? 0);
        double best = ok.Min(Ocena);
        return ok.First(w => Ocena(w) <= best * 1.15 + 0.02);
    }

    static SladTor TorPoSladzie1(IReadOnlyList<XY> pts, double k, double baza)
    {
        if (pts.Count < 3 || Length(pts) / k < 2 * baza + 5) return new SladTor(null, 0, 0, null, new(), $"ślad za krótki (co najmniej {2 * baza + 5:0} m)");
        var luki = Doprecyzuj(pts, k, Luki(Krzywizna(pts, k, baza), baza));
        var a = Szkic(pts, k, luki);
        var use = Thin(pts);
        var f = Dopasuj(a, k, use); if (f.Error != null) return new SladTor(null, 0, 0, null, new(), f.Error);
        // przejściowe szczątkowe (poniżej 3 m) – bez nich
        if (f.A.Els.Any(e => e.Przejsciowa && e.Len < 3))
        {
            var b = f.A.Clone(); b.Els.RemoveAll(e => e.Przejsciowa && e.Len < 3); TrasaElem.Normalize(b);
            var f2 = Dopasuj(b, k, use); if (f2.Error == null && f2.Rms < f.Rms * 1.05) f = f2;
        }
        var rr = TrasaElem.Compute(f.A, k);
        var all = pts.Select(q => Budowa.Dist(rr, q) / k).ToList();
        var outl = Odstajace(all, 0.5);
        if (outl.Count > 0)
        {
            var keep = Thin(pts.Where((q, i) => !outl.Contains(i)).ToList());
            var f3 = Dopasuj(f.A, k, keep);
            if (f3.Error == null) { f = f3; rr = TrasaElem.Compute(f.A, k); all = pts.Select(q => Budowa.Dist(rr, q) / k).ToList(); }
        }
        // szczątkowe łuki (załamania z szumu: kąt poniżej 2°) – bez nich, gdy dopasowanie prawie nie gorsze
        Scal(f.A);
        var useK = outl.Count > 0 ? Thin(pts.Where((q, i) => !outl.Contains(i)).ToList()) : use;
        for (int it = 0; it < 6; it++)
        {
            var grp = Grupy(f.A).Where(g => Math.Abs(g.Kat) < 2 * Math.PI / 180).OrderBy(g => Math.Abs(g.Kat)).ToList();
            bool done = false;
            foreach (var g in grp)
            {
                var b = f.A.Clone(); double len = b.Els.Skip(g.I).Take(g.J - g.I + 1).Sum(e => e.Len);
                b.Els.RemoveRange(g.I, g.J - g.I + 1); b.Els.Insert(g.I, new UtAlignEl { Len = len }); TrasaElem.Normalize(b);
                var fb = Dopasuj(b, k, useK);
                if (fb.Error == null && fb.Rms <= f.Rms * 1.05 + 0.01) { f = fb; Scal(f.A); done = true; break; }
            }
            if (!done) break;
        }
        rr = TrasaElem.Compute(f.A, k);
        all = pts.Select(q => Budowa.Dist(rr, q) / k).ToList();
        var used = all.Where((d, i) => !outl.Contains(i)).ToList();
        double rms = Math.Sqrt(used.Sum(x => x * x) / Math.Max(1, used.Count));
        // trasa ma biec wzdłuż śladu: każdy jej punkt blisko śladu, długość jak śladu
        double away = rr.Sample(5).Max(q => DoLamanej(pts, q.P)) / k, lt = Length(pts) / k;
        double sig = Rozrzut(pts, k).Sigma;
        if (away > Math.Max(10, 20 * sig) || rms > Math.Max(2, 6 * sig) || Math.Abs(rr.Length - lt) > KmTol(lt))
            return new SladTor(null, 0, 0, null, new(), $"dopasowanie odbiega od śladu ({away:0.#} m, długość {rr.Length:0} m przy śladzie {lt:0} m) – podziel ślad na krótsze kawałki albo zmień bazę krzywizny");
        var prz = Przeskoki(pts, k, all, outl).Concat(Esy(f.A, rr, pts, k)).OrderBy(p => p.I0).ToList();
        for (int i = prz.Count - 1; i > 0; i--)
            if (prz[i].I0 <= prz[i - 1].I1) { prz[i - 1] = prz[i - 1] with { I1 = Math.Max(prz[i - 1].I1, prz[i].I1), S1 = Math.Max(prz[i - 1].S1, prz[i].S1), Max = Math.Max(prz[i - 1].Max, prz[i].Max), Es = prz[i - 1].Es || prz[i].Es }; prz.RemoveAt(i); }
        return new SladTor(f.A, rms, used.Count > 0 ? used.Max() : 0, all, outl, null, prz) { Baza = baza };
    }

    /// <summary>Es przeskoku: para przeciwnych łuków nie łagodniejszych niż R <see cref="EsR"/>, z kątami znoszącymi się, na długości
    /// do <see cref="EsLen"/> m, przesuwająca tor w bok o <see cref="EsOff"/> – jak połączenie torów (dwa rozjazdy).</summary>
    public const double EsR = 500, EsLen = 150; public static readonly (double Min, double Max) EsOff = (2.5, 7);

    /// <summary>
    /// Przeskoki, które dopasowanie poprowadziło za śladem: trasa skręca esem na sąsiedni tor (połączenie torów), a nie pomija punktów.
    /// Dwa esy w przeciwne strony – tam i z powrotem – to jeden przeskok od pierwszego do drugiego. Km i punkty – na śladzie.
    /// </summary>
    public static List<SladPrzeskok> Esy(UtAlign a, TrasaResult rr, IReadOnlyList<XY> pts, double k)
    {
        // zwroty: kolejne krzywe (łuki, przejściowe) w jedną stronę – km od–do na trasie [m w terenie], kąt [rad], najmniejszy R
        var zw = new List<(double S0, double S1, double Kat, double MinR, int Sign)>();
        double s = 0; bool prevCurve = false;
        foreach (var e in a.Els)
        {
            if (e.Prosta) { s += e.Len; prevCurve = false; continue; }
            int sg = Math.Sign(e.R0 != 0 ? e.R0 : e.R1);
            double kat = e.Len * (UtAlignEl.K(e.R0) + UtAlignEl.K(e.R1)) / 2;
            double r = new[] { e.R0, e.R1 }.Where(x => x != 0).Min(Math.Abs);
            if (prevCurve && zw[^1].Sign == sg) { var z = zw[^1]; zw[^1] = z with { S1 = s + e.Len, Kat = z.Kat + kat, MinR = Math.Min(z.MinR, r) }; }
            else zw.Add((s, s + e.Len, kat, r, sg));
            s += e.Len; prevCurve = true;
        }
        var esy = new List<(double S0, double S1, double Off)>();
        for (int i = 0; i + 1 < zw.Count; i++)
        {
            var (z1, z2) = (zw[i], zw[i + 1]);
            double m = Math.Max(Math.Abs(z1.Kat), Math.Abs(z2.Kat));
            if (z1.Sign == z2.Sign || Math.Min(z1.MinR, z2.MinR) > EsR || m < Math.PI / 180 || Math.Abs(z1.Kat + z2.Kat) > 0.35 * m || z2.S1 - z1.S0 > EsLen) continue;
            var (p0, az0) = rr.At(z1.S0); var d = rr.At(z2.S1).P - p0; var dz = Element.Dir(az0);
            double off = (d.X * dz.Y - d.Y * dz.X) / k;
            if (Math.Abs(off) < EsOff.Min || Math.Abs(off) > EsOff.Max) continue;
            esy.Add((z1.S0, z2.S1, off)); i++;
        }
        var res = new List<SladPrzeskok>(); if (esy.Count == 0) return res;
        // km na trasie → punkty i km na śladzie
        var sp = pts.Select(q => rr.Nearest(q).S).ToList();
        var st = new double[pts.Count]; for (int i = 1; i < pts.Count; i++) st[i] = st[i - 1] + XY.Dist(pts[i - 1], pts[i]) / k;
        for (int i = 0; i < esy.Count; i++)
        {
            var (a0, a1, off) = esy[i]; double mx = Math.Abs(off);
            if (i + 1 < esy.Count && Math.Sign(esy[i + 1].Off) != Math.Sign(off)) { a1 = esy[i + 1].S1; mx = Math.Max(mx, Math.Abs(esy[i + 1].Off)); i++; }
            int i0 = sp.FindIndex(x => x >= a0), i1 = sp.FindLastIndex(x => x <= a1);
            if (i0 < 0 || i1 < i0) continue;
            res.Add(new SladPrzeskok(i0, i1, st[i0], st[i1], mx, true));
        }
        return res;
    }

    /// <summary>Odchyłka, od której punkt śladu leży raczej na sąsiednim torze niż na tym [m] (rozstaw torów 3,5–5 m).</summary>
    public const double PrzeskokDev = 2;
    /// <summary>Przeskok: co najmniej tyle punktów po kolei i tyle metrów śladu – pojedynczy węzeł obok (błąd mapowania, pomyłka
    /// kliknięcia) to nie przeskok.</summary>
    public const int PrzeskokPkt = 3; public const double PrzeskokLen = 10;

    /// <summary>
    /// Ciągłe odcinki śladu daleko od trasy (punkty odstające albo dalej niż <see cref="PrzeskokDev"/>): ślad biegnie tam po sąsiednim
    /// torze. Odcinek poszerzony o sąsiednie punkty dalej niż 1 m (zjazd przez rozjazd).
    /// </summary>
    public static List<SladPrzeskok> Przeskoki(IReadOnlyList<XY> pts, double k, IReadOnlyList<double> dev, ICollection<int> outl)
    {
        var res = new List<SladPrzeskok>(); int n = Math.Min(pts.Count, dev.Count);
        var s = new double[n]; for (int i = 1; i < n; i++) s[i] = s[i - 1] + XY.Dist(pts[i - 1], pts[i]) / k;
        bool Far(int i) => outl.Contains(i) || Math.Abs(dev[i]) > PrzeskokDev;
        for (int i = 0; i < n; i++)
        {
            if (!Far(i)) continue;
            int j = i; while (j + 1 < n && Far(j + 1)) j++;
            int a = i, b = j;
            while (a > 0 && Math.Abs(dev[a - 1]) > 1) a--;
            while (b + 1 < n && Math.Abs(dev[b + 1]) > 1) b++;
            if (j - i + 1 >= PrzeskokPkt && s[b] - s[a] >= PrzeskokLen)
            {
                if (res.Count > 0 && a <= res[^1].I1) { var p = res[^1]; res[^1] = p with { I1 = b, S1 = s[b], Max = Math.Max(p.Max, Enumerable.Range(a, b - a + 1).Max(q => Math.Abs(dev[q]))) }; }
                else res.Add(new SladPrzeskok(a, b, s[a], s[b], Enumerable.Range(a, b - a + 1).Max(q => Math.Abs(dev[q]))));
            }
            i = j;
        }
        return res;
    }

    /// <summary>Opis przeskoku do komunikatu.</summary>
    public static string Opis(SladPrzeskok p) => $"km śladu {p.S0:0}–{p.S1:0} m ({(p.Es ? "es w bok o" : "do")} {p.Max:0.0} m{(p.Es ? "" : " od toru")})";

    /// <summary>Sąsiednie proste razem (po usunięciu łuku albo zwinięciu elementu do zera).</summary>
    static void Scal(UtAlign a)
    {
        var e = a.Els;
        for (int i = e.Count - 1; i > 0; i--)
            if (e[i].Prosta && e[i - 1].Prosta) { e[i - 1].Len = Math.Round(e[i - 1].Len + e[i].Len, 2); e.RemoveAt(i); }
    }

    /// <summary>Grupy krzywych (elementy między prostymi): od–do i kąt zwrotu [rad].</summary>
    static List<(int I, int J, double Kat)> Grupy(UtAlign a)
    {
        var res = new List<(int, int, double)>(); var e = a.Els;
        for (int i = 0; i < e.Count; i++)
        {
            if (e[i].Prosta) continue;
            int j = i; while (j + 1 < e.Count && !e[j + 1].Prosta) j++;
            double kat = 0; for (int q = i; q <= j; q++) kat += e[q].Len * (UtAlignEl.K(e[q].R0) + UtAlignEl.K(e[q].R1)) / 2;
            if (i > 0 && j < e.Count - 1) res.Add((i, j, kat));
            i = j;
        }
        return res;
    }

    /// <summary>Odległość punktu od łamanej śladu [m w układzie].</summary>
    static double DoLamanej(IReadOnlyList<XY> pts, XY p)
    {
        double best = double.MaxValue;
        for (int i = 1; i < pts.Count; i++)
        {
            var a = pts[i - 1]; var d = pts[i] - a; double l2 = d.X * d.X + d.Y * d.Y;
            double t = l2 > 0 ? Math.Clamp(((p.X - a.X) * d.X + (p.Y - a.Y) * d.Y) / l2, 0, 1) : 0;
            best = Math.Min(best, XY.Dist(p, a + d * t));
        }
        return best;
    }

    /// <summary>
    /// Każdy łuk osobno: punkty od połowy prostej przed nim do połowy prostej za nim, model prosta – (przejściowa) – łuk – (przejściowa) – prosta
    /// dopasowany przy kilku długościach przejściowych na start (0, 15, 30, 50, 75, 100 m); wygrywa najmniejsza odchyłka, a przy prawie równej
    /// (do 1 %) – krótsza przejściowa. Na rozrzuconym śladzie (OSM) wykres krzywizny przejściowych nie pokaże, a dopasowanie – tak.
    /// </summary>
    public static List<SladLuk> Doprecyzuj(IReadOnlyList<XY> pts, double k, IReadOnlyList<SladLuk> luki)
    {
        var sp = new double[pts.Count];
        for (int i = 1; i < pts.Count; i++) sp[i] = sp[i - 1] + XY.Dist(pts[i - 1], pts[i]) / k;
        var res = new SladLuk[luki.Count];
        Parallel.For(0, luki.Count, j =>
        {
            var l = luki[j]; res[j] = l;
            double prevEnd = j > 0 ? luki[j - 1].S1 : 0, nextStart = j + 1 < luki.Count ? luki[j + 1].S0 : sp[^1];
            double wa = Math.Max(Math.Max(0, (prevEnd + l.S0) / 2), l.S0 - 120), wb = Math.Min(Math.Min(sp[^1], (l.S1 + nextStart) / 2), l.S1 + 120);
            var loc = new List<XY>(); for (int i = 0; i < pts.Count; i++) if (sp[i] >= wa && sp[i] <= wb) loc.Add(pts[i]);
            if (loc.Count < 8) return;
            var lp = Thin(loc, 150);
            double s0 = sp.First(x => x >= wa), lsum = Math.Abs(l.Kat * l.R), sm = (l.S0 + l.S1) / 2, s1 = sp.Last(x => x <= wb);
            XY At(double s) { int i = Array.FindIndex(sp, x => x >= s); if (i <= 0) return pts[0]; double t = (s - sp[i - 1]) / Math.Max(1e-9, sp[i] - sp[i - 1]); return pts[i - 1] + (pts[i] - pts[i - 1]) * t; }
            var cands = new List<(double L, UtAlign A, double Rms)>();
            foreach (double L in new[] { 0.0, 15, 30, 50, 75, 100 })
            {
                if (L > 0.45 * lsum + 1e-9 && L > 0) continue;
                double tot = lsum + L, ts = sm - tot / 2, lc = Math.Max(2, lsum - L);
                double st1 = Math.Max(1, ts - s0), st2 = Math.Max(1, s1 - (ts + tot));
                var p0 = At(s0); var d = At(s0 + Math.Max(5, Math.Min(st1, 30))) - p0;
                var al = new UtAlign { X = p0.X, Y = p0.Y, Az = Math.Atan2(d.Y, d.X) };
                al.Els.Add(new UtAlignEl { Len = st1 });
                if (L > 0) al.Els.Add(new UtAlignEl { Len = L, R1 = l.R });
                al.Els.Add(new UtAlignEl { Len = lc, R0 = l.R, R1 = l.R });
                if (L > 0) al.Els.Add(new UtAlignEl { Len = L, R0 = l.R });
                al.Els.Add(new UtAlignEl { Len = st2 });
                TrasaElem.Normalize(al);
                var f = Dopasuj(al, k, lp);
                if (f.Error == null && Zgodny(f.A, l, s1 - s0)) lock (cands) cands.Add((L, f.A, f.Rms));
            }
            if (cands.Count == 0) return;
            double best = cands.Min(c => c.Rms);
            var pick = cands.Where(c => c.Rms <= best * 1.01 + 1e-4).OrderBy(c => c.L).First().A;
            var e = pick.Els; int ci = e.FindIndex(x => x.Luk); if (ci < 0) return;
            double l1 = ci > 0 && e[ci - 1].Przejsciowa ? e[ci - 1].Len : 0, l2 = ci + 1 < e.Count && e[ci + 1].Przejsciowa ? e[ci + 1].Len : 0;
            double tsN = s0 + e.Take(ci).Where(x => x.Prosta).Sum(x => x.Len);
            double r = e[ci].R0, kat = Math.Sign(r) * (e[ci].Len / Math.Abs(r) + (l1 + l2) / (2 * Math.Abs(r)));
            res[j] = new SladLuk(Math.Round(tsN, 1), Math.Round(tsN + l1 + e[ci].Len + l2, 1), r, Math.Round(l1), Math.Round(l2), kat);
        });
        return res.ToList();
    }

    /// <summary>
    /// Wynik doprecyzowania zgodny z łukiem z wykresu: jeden łuk w tę samą stronę, kąt zwrotu różny najwyżej o połowę (i 1°), R nie mniejszy
    /// niż ćwierć R z wykresu, ani łuk ani przejściowe nie dłuższe niż cała krzywa z wykresu z zapasem. Inaczej dopasowanie lokalne „uciekło”
    /// (np. z łuku 1,8° zrobiło się 84°, R 168 m z przejściowymi po 200 m) – zostaje łuk z wykresu. Krzywa kończy się przed końcem okna
    /// <paramref name="okno"/> [m] (inaczej koniec łuku wychodzi za ślad).
    /// </summary>
    static bool Zgodny(UtAlign a, SladLuk l, double okno)
    {
        var e = a.Els; int ci = e.FindIndex(x => x.Luk); if (ci < 0 || e.Count(x => x.Luk) != 1) return false;
        int last = e.FindLastIndex(x => !x.Prosta); if (e.Take(last + 1).Sum(x => x.Len) > okno - 2) return false;
        double kat = 0, len = 0; foreach (var x in e) if (!x.Prosta) { kat += x.Len * (UtAlignEl.K(x.R0) + UtAlignEl.K(x.R1)) / 2; len += x.Len; }
        if (Math.Sign(kat) != Math.Sign(l.Kat) || Math.Abs(kat - l.Kat) > Math.Max(Math.PI / 180, 0.5 * Math.Abs(l.Kat))) return false;
        if (Math.Abs(e[ci].R0) < Math.Abs(l.R) / 4) return false;
        return len <= 2 * (l.S1 - l.S0) + 60;
    }

    static List<XY> Thin(IReadOnlyList<XY> pts, int max = MaxPkt)
    {
        if (pts.Count <= max) return pts.ToList();
        var res = new List<XY>(); double st = (pts.Count - 1) / (double)(max - 1);
        for (int i = 0; i < max; i++) res.Add(pts[(int)Math.Round(i * st)]);
        return res;
    }

    /// <summary>Opis łuku do panelu: „łuk w prawo R ≈ 612 m, L ≈ 45 / 45 m, kąt 12,30°, km śladu 120–310”.</summary>
    public static string Opis(SladLuk l)
    {
        var pl = System.Globalization.CultureInfo.GetCultureInfo("pl-PL");
        string ls = l.L1 > 0 || l.L2 > 0 ? $", L ≈ {l.L1.ToString("0", pl)} / {l.L2.ToString("0", pl)} m" : ", bez przejściowych";
        return $"łuk w {(l.R > 0 ? "prawo" : "lewo")} R ≈ {Math.Abs(l.R).ToString("0", pl)} m{ls}, kąt {(Math.Abs(l.Kat) * 180 / Math.PI).ToString("0.00", pl)}°, km śladu {l.S0.ToString("0", pl)}–{l.S1.ToString("0", pl)}";
    }
}

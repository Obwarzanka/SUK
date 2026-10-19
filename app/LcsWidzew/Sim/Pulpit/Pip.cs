using LcsWidzew.Model;

namespace LcsWidzew.Sim;

/// <summary>
/// PIP – numery pociągów na pulpicie (Ie-104 § 8 pkt 26; PODZIAL_MODULOW.md krok 9). Część systemu nadrzędnego: zna tylko
/// zajętości odcinków meldowane przez komputer zależności (<see cref="SecState.TrainOcc"/>) i układ torowy, nie listę pociągów.
/// Każdy numer ma czoło – odcinek, na który ostatnio wszedł pociąg – i okno: ostatni tor stacyjny albo odstęp szlakowy,
/// przez który przeszło czoło (tam numer jest wyświetlany). Gdy zajmuje się odcinek połączony zajętymi odcinkami z czołem
/// numeru, czoło przechodzi na ten odcinek. Numer znika, gdy jego odcinek czoła się zwolni bez przejścia dalej (pociąg
/// opuścił obszar albo został odstawiony). Numery przychodzą z zapowiedzi posterunku sąsiedniego i z rozkładu jazdy.
/// </summary>
public class Pip
{
    readonly Simulator S;
    Station L => S.L;

    /// <summary>Numer pociągu w PIP.</summary>
    public class Numer
    {
        public string Nr, Czolo, Okno;
        /// <summary>Poprzedni odcinek czoła (skąd przyszedł pociąg).</summary>
        public string Poprzedni;
        /// <summary>Odcinek czoła był już zajęty (zapowiedziany numer czeka na wjazd pociągu).</summary>
        public bool Zajety;
        /// <summary>Kolejność wejścia do okna (dwa pociągi na jednym torze).</summary>
        public long OknoOd;
        /// <summary>Czas ostatniego przejścia czoła (pociąg w ruchu ma pierwszeństwo przed stojącym).</summary>
        public double CzoloOd;
    }

    public readonly List<Numer> Numery = new();
    readonly Dictionary<string, List<string>> sasiedzi = new();
    HashSet<string> zajete = new();
    long seq;

    public Pip(Simulator s)
    {
        S = s;
        // sąsiedztwo odcinków w układzie torowym (wspólny węzeł)
        foreach (var n in L.Nodes.Values)
        {
            var secs = n.Edges.Select(e => L.Edges[e].Sec).Where(x => x != null).Distinct().ToList();
            foreach (var a in secs) foreach (var b in secs) if (a != b) { if (!sasiedzi.TryGetValue(a, out var l)) sasiedzi[a] = l = new(); if (!l.Contains(b)) l.Add(b); }
        }
    }

    /// <summary>Okno numeru pociągu: tor stacyjny albo odstęp szlakowy.</summary>
    public bool Okno(string sec) => sec.Contains(":JT") || (L.Sections.TryGetValue(sec, out var d) && (d.Kind == "szlak" || d.TorUt));

    /// <summary>Numer od posterunku sąsiedniego (zapowiedź) albo z rozkładu – pociąg wjedzie / stoi na odcinku sec.</summary>
    public void Nadaj(string nr, string sec)
    {
        if (string.IsNullOrEmpty(nr) || sec == null) return;
        Numery.Add(new Numer { Nr = nr, Czolo = sec, Okno = Okno(sec) ? sec : null, OknoOd = ++seq });
    }

    /// <summary>Zmiana numeru pociągu wg rozkładu jazdy (koniec biegu, dalej jako inny pociąg).</summary>
    public void ZmienNumer(string stary, string nowy)
    {
        if (stary == nowy) return;
        var n = Numery.FirstOrDefault(x => x.Nr == stary); if (n != null) n.Nr = nowy;
    }

    /// <summary>Cykl: przejście czoła numerów po zajętościach odcinków (po ruchu pociągów i meldunkach kart).</summary>
    public void Tick()
    {
        var teraz = S.Secs.Where(x => x.Value.TrainOcc).Select(x => x.Key).ToHashSet();
        foreach (var n in teraz.Where(x => !zajete.Contains(x)))
        {
            // zapowiedziany numer – pociąg wjechał na odcinek
            var czeka = Numery.FirstOrDefault(x => x.Czolo == n && !x.Zajety);
            if (czeka != null) { czeka.Zajety = true; czeka.CzoloOd = S.T; continue; }
            var m = Najblizszy(n, teraz);
            if (m == null) continue;
            Przejdz(m, n);
        }
        // odcinek czoła zwolniony bez przejścia dalej: pociąg wjechał na odcinek już zajęty (np. drugi pociąg na torze) – czoło
        // na zajęty odcinek sąsiedni (nie ten, z którego przyszedł); bez zajętego sąsiada – pociąg opuścił obszar, numer znika
        foreach (var m in Numery.Where(x => x.Zajety && !teraz.Contains(x.Czolo)).ToList())
        {
            var obok = (sasiedzi.GetValueOrDefault(m.Czolo) ?? new()).Where(teraz.Contains).ToList();
            var dalej = obok.Where(x => x != m.Poprzedni).OrderByDescending(Okno).FirstOrDefault() ?? obok.FirstOrDefault();
            if (dalej != null) Przejdz(m, dalej); else Numery.Remove(m);
        }
        zajete = teraz;
    }

    void Przejdz(Numer m, string n)
    {
        m.Poprzedni = m.Czolo; m.Czolo = n; m.Zajety = true; m.CzoloOd = S.T;
        if (Okno(n) && m.Okno != n) { m.Okno = n; m.OknoOd = ++seq; }
    }

    /// <summary>
    /// Numer, którego czoło leży najbliżej odcinka n (po zajętych odcinkach). Przy kilku tak samo bliskich – ten, którego czoło
    /// przesunęło się ostatnio (pociąg w ruchu, nie stojący przed semaforem), potem wcześniej wpisany do okna.
    /// </summary>
    Numer Najblizszy(string n, HashSet<string> occ)
    {
        var seen = new HashSet<string> { n };
        var poziom = new List<string> { n };
        while (poziom.Count > 0)
        {
            var dalej = new List<string>();
            foreach (var c in poziom)
                foreach (var s in sasiedzi.GetValueOrDefault(c) ?? new())
                    if (occ.Contains(s) && seen.Add(s)) dalej.Add(s);
            var m = Numery.Where(x => x.Zajety && dalej.Contains(x.Czolo)).OrderByDescending(x => x.CzoloOd).ThenBy(x => x.OknoOd).FirstOrDefault();
            if (m != null) return m;
            poziom = dalej;
        }
        return null;
    }

    /// <summary>Wyświetlacze numerów: okno → numery (w kolejności wejścia).</summary>
    public Dictionary<string, List<string>> Okna() =>
        Numery.Where(x => x.Okno != null).GroupBy(x => x.Okno).ToDictionary(g => g.Key, g => g.OrderBy(x => x.OknoOd).Select(x => x.Nr).ToList());
}

namespace Lcs.Lacznosc;

/// <summary>Stan okienka numeru pociągu na szlaku (kolor na schemacie).</summary>
public enum StanOkna { Zapytanie, Zgoda, NaSzlaku, Stoj, Odmowa }

/// <summary>Pociąg „zamówiony” albo jadący po szlaku do / od posterunku sąsiedniego.</summary>
public sealed class PociagNaSzlaku
{
    public string Nr { get; init; }
    public int? Tor { get; set; }
    public StanOkna Stan { get; set; }
    /// <summary>true – od nas do sąsiada (►), false – od sąsiada do nas (◄).</summary>
    public bool OdNas { get; set; }
    public DateTime Zmiana { get; set; } = DateTime.Now;
    /// <summary>Tylko z zajętości w grze (bez komunikatu w czacie) – okienko rysowane, ale nie zapisywane w stanie szlaku.</summary>
    public bool ZGry { get; init; }
}

/// <summary>Szlak do jednego posterunku sąsiedniego: tory (nazwy z komunikatów gry) i pociągi na nim.</summary>
public sealed class Szlak
{
    public string Sasiad { get; init; }
    public List<int> Tory { get; } = new();
    public List<PociagNaSzlaku> Pociagi { get; } = new();
    public bool Lewa { get; set; }
    /// <summary>Tory ustawione ręcznie – program nie dopisuje torów z komunikatów.</summary>
    public bool Reczne { get; set; }
    /// <summary>Pociągi, które gra pokazuje teraz na odcinkach tego szlaku (nr, tor z nazwy odcinka albo null, od nas).</summary>
    public List<(string Nr, int? Tor, bool OdNas)> ZGry { get; } = new();
}

/// <summary>
/// Stan szlaków z komunikatów EZP (w obie strony) i zdarzeń z gry. Kierunek okienka:
/// zapytanie / odjazd wysłane przez nas oraz zgoda / odmowa od sąsiada – pociąg od nas (►); odwrotnie – do nas (◄).
/// Odwołanie i przyjazd usuwają pociąg ze szlaku.
/// </summary>
public sealed class StanSzlakow
{
    readonly Dictionary<string, Szlak> _szlaki = new(StringComparer.OrdinalIgnoreCase);
    public event Action<string, int> NowyTor;   // sąsiad, tor – do zapamiętania w ustawieniach

    public IReadOnlyCollection<Szlak> Wszystkie => _szlaki.Values;

    public Szlak Daj(string sasiad)
    {
        if (!_szlaki.TryGetValue(sasiad, out var s)) _szlaki[sasiad] = s = new Szlak { Sasiad = sasiad };
        return s;
    }

    public void Usun(string sasiad) => _szlaki.Remove(sasiad);
    public void Wyczysc() => _szlaki.Clear();

    public void PoznajTor(string sasiad, int tor)
    {
        var s = Daj(sasiad);
        if (tor <= 0 || s.Reczne || s.Tory.Contains(tor)) return;
        s.Tory.Add(tor); s.Tory.Sort();
        NowyTor?.Invoke(sasiad, tor);
    }

    /// <summary>Komunikat z rozmowy (przychodzący albo wysłany) – zmiana okienka na szlaku.</summary>
    public void Zastosuj(Wpis w)
    {
        if (string.IsNullOrEmpty(w.Sasiad)) return;
        if (w.Tor is int t) PoznajTor(w.Sasiad, t);
        if (string.IsNullOrEmpty(w.Pociag)) return;
        var s = Daj(w.Sasiad);
        bool odNasGdyWych = w.Klasa is "TrainRequestMessage" or "TrainDepartedMessage";
        StanOkna? stan = w.Klasa switch
        {
            "TrainRequestMessage" => StanOkna.Zapytanie,
            "TrainPermissionMessage" => StanOkna.Zgoda,
            "TrainDepartedMessage" => StanOkna.NaSzlaku,
            "TrainDenialMessage" => StanOkna.Odmowa,
            _ => null,
        };
        if (w.Klasa is "TrainCancelledMessage" or "TrainArrivedMessage") { s.Pociagi.RemoveAll(p => p.Nr == w.Pociag); return; }
        if (stan == null) return;
        Ustaw(s, w.Pociag, w.Wychodzacy ? odNasGdyWych : !odNasGdyWych, stan.Value, w.Tor);
    }

    public void Ustaw(Szlak s, string nr, bool odNas, StanOkna stan, int? tor)
    {
        var p = s.Pociagi.FirstOrDefault(x => x.Nr == nr);
        if (p == null) s.Pociagi.Add(p = new PociagNaSzlaku { Nr = nr });
        p.OdNas = odNas; p.Stan = stan; p.Zmiana = DateTime.Now;
        if (tor.HasValue) p.Tor = tor;
    }

    /// <summary>Pociąg przyjechał (z gry / z API) – zdejmij go ze wszystkich szlaków w danym kierunku.</summary>
    public List<(string Sasiad, PociagNaSzlaku P)> Przyjechal(string nr, bool odNas)
    {
        var wynik = new List<(string, PociagNaSzlaku)>();
        foreach (var s in _szlaki.Values)
            foreach (var p in s.Pociagi.Where(x => x.Nr == nr && x.OdNas == odNas && x.Stan is StanOkna.NaSzlaku or StanOkna.Zgoda or StanOkna.Stoj).ToList())
            { s.Pociagi.Remove(p); wynik.Add((s.Sasiad, p)); }
        return wynik;
    }

    /// <summary>Stare odmowy (10 min), zapytania bez odpowiedzi (30 min) oraz zgody i „na szlaku” bez przyjęcia (60 min) znikają same.</summary>
    public bool Sprzataj()
    {
        bool zm = false;
        foreach (var s in _szlaki.Values)
            zm |= s.Pociagi.RemoveAll(p => (p.Stan == StanOkna.Odmowa && (DateTime.Now - p.Zmiana).TotalMinutes > 10)
                                          || (p.Stan == StanOkna.Zapytanie && (DateTime.Now - p.Zmiana).TotalMinutes > 30)
                                          || (p.Stan is StanOkna.Zgoda or StanOkna.NaSzlaku && (DateTime.Now - p.Zmiana).TotalMinutes > 60)) > 0;
        return zm;
    }

    public PociagNaSzlaku Znajdz(string sasiad, string nr) => _szlaki.TryGetValue(sasiad, out var s) ? s.Pociagi.FirstOrDefault(p => p.Nr == nr) : null;
}

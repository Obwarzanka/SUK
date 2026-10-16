namespace LcsWidzew.Sim;

/// <summary>
/// Urządzenia przytorowe i sterowniki obiektowe z kartami (Zrodla/EBILock_komputerownia.md rozdz. 4, etap 7).
/// Między komputerem zależności (rozkazy → / ← statusy) a terenem, w którym jeżdżą pociągi.
/// Na razie: karty kontroli niezajętości odcinków (krok 4); zwrotnice, sygnalizatory, zasilanie – kolejne kroki.
/// </summary>
public class Urzadzenia
{
    readonly Simulator S;
    /// <summary>Karty odcinków (kolejność jak w układzie).</summary>
    public readonly Dictionary<string, KartaOdcinka> Odcinki = new();

    public Urzadzenia(Simulator s)
    {
        S = s;
        foreach (var id in s.Secs.Keys) Odcinki[id] = new KartaOdcinka(s, id);
    }

    // ------------------------------------------------------------------ teren ← ruch pociągów
    /// <summary>Ruch pociągów zgłasza odcinki, na których w tym cyklu są osie pojazdów; karty liczą stan i meldują go KZ.</summary>
    public void Osie(IReadOnlySet<string> zajete)
    {
        foreach (var k in Odcinki.Values) { k.Pojazd = zajete.Contains(k.Id); k.Publikuj(); }
        foreach (var k in Odcinki.Values) k.KontrolaZliczenia();
        foreach (var k in Odcinki.Values) k.PierwszyPrzejazd();
    }

    /// <summary>Teren: czy na odcinku są osie pojazdu (niezależnie od stanu urządzenia kontroli niezajętości).</summary>
    public bool Pojazd(string sec) => Odcinki[sec].Pojazd;

    // ------------------------------------------------------------------ rozkazy komputera zależności
    /// <summary>Zerowanie licznika osi (ZeroLO po potwierdzeniu dyżurnego).</summary>
    public void Zeruj(string sec) => Odcinki[sec].Zeruj();
}

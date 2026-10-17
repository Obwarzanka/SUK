namespace LcsWidzew.Sim;

/// <summary>
/// Urządzenia przytorowe i sterowniki obiektowe z kartami (Zrodla/EBILock_komputerownia.md rozdz. 4, etap 7).
/// Między komputerem zależności (rozkazy → / ← statusy) a terenem, w którym jeżdżą pociągi.
/// Karty kontroli niezajętości odcinków (krok 4), zwrotnic i wykolejnic (krok 5); sygnalizatory, zasilanie – kolejne kroki.
/// </summary>
public partial class Urzadzenia
{
    readonly Simulator S;
    /// <summary>Karty odcinków (kolejność jak w układzie).</summary>
    public readonly Dictionary<string, KartaOdcinka> Odcinki = new();
    /// <summary>Karty zwrotnic (sterowniki zwrotnicowe z napędami).</summary>
    public readonly Dictionary<string, KartaZwrotnicy> Zwrotnice = new();
    /// <summary>Karty wykolejnic.</summary>
    public readonly Dictionary<string, KartaWykolejnicy> Wykolejnice = new();
    /// <summary>Karty sygnalizatorów (semafory wjazdowe, wyjazdowe, odstępowe, tarcze manewrowe).</summary>
    public readonly Dictionary<string, KartaSygnalizatora> Sygnalizatory = new();
    /// <summary>Sygnalizatory z kartą (świetlne, sterowane przez KZ).</summary>
    public static bool MaKarte(Model.SignalDef d) => d.Type is "wjazdowy" or "wyjazdowy" or "odstepowy" or "tm";

    public Urzadzenia(Simulator s)
    {
        S = s;
        foreach (var id in s.Secs.Keys) Odcinki[id] = new KartaOdcinka(s, id);
        foreach (var id in s.Sws.Keys) Zwrotnice[id] = new KartaZwrotnicy(s, id);
        foreach (var id in s.Ders.Keys) Wykolejnice[id] = new KartaWykolejnicy(s, id);
        foreach (var d in s.L.Signals.Values) if (MaKarte(d)) Sygnalizatory[d.Name] = new KartaSygnalizatora(s, d.Name);
    }

    /// <summary>Cykl kart (przed komputerem zależności): wykonanie rozkazów z poprzedniego cyklu, przekładanie, statusy do KZ.</summary>
    public void Tick()
    {
        foreach (var k in Zwrotnice.Values) { k.Tick(); k.Publikuj(); }
        foreach (var k in Wykolejnice.Values) { k.Tick(); k.Publikuj(); }
        foreach (var k in Sygnalizatory.Values) { k.Tick(); k.Publikuj(); }
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
    /// <summary>Teren: położenie iglic zwrotnicy (po nim jedzie pociąg).</summary>
    public string Iglice(string sw) => Zwrotnice[sw].Iglice;
    /// <summary>Teren: pojazd najeżdża zwrotnicę od strony gałęzi „+” / „-” (rozprucie, gdy iglice inaczej).</summary>
    public void Najazd(string sw, string zGalezi) => Zwrotnice[sw].Najazd(zGalezi);
    /// <summary>Teren: wykolejnica na szynie.</summary>
    public bool WykolejnicaNaTorze(string name) => Wykolejnice[name].Zalozona;
    /// <summary>Teren: sygnał widziany przez maszynistę (kod Ie-1); null – sygnalizator bez karty.</summary>
    public string SygnalWidziany(string sig) => Sygnalizatory.TryGetValue(sig, out var k) ? k.Wyswietlany : null;

    // ------------------------------------------------------------------ rozkazy komputera zależności
    /// <summary>Zerowanie licznika osi (ZeroLO po potwierdzeniu dyżurnego).</summary>
    public void Zeruj(string sec) => Odcinki[sec].Zeruj();
    /// <summary>Przestawienie zwrotnicy do położenia „+” / „-”.</summary>
    public void Przestaw(string sw, string pol) => Zwrotnice[sw].Rozkaz(pol);
    /// <summary>Założenie (true) / zdjęcie (false) wykolejnicy.</summary>
    public void Wykolejnica(string name, bool zaloz) => Wykolejnice[name].Rozkaz(zaloz);
    /// <summary>Sygnał do wyświetlenia (kod Ie-1).</summary>
    public void Sygnal(string sig, string kod) { if (Sygnalizatory.TryGetValue(sig, out var k)) k.Rozkaz(kod); }
}

namespace LcsWidzew.Sim;

/// <summary>
/// Urządzenia przytorowe i sterowniki obiektowe z kartami (Zrodla/EBILock_komputerownia.md rozdz. 4, etap 7).
/// Między komputerem zależności (rozkazy → / ← statusy) a terenem, w którym jeżdżą pociągi.
/// Karty kontroli niezajętości odcinków (krok 4), zwrotnic i wykolejnic (krok 5), sygnalizatorów (krok 6), zasilanie (krok 7).
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
    /// <summary>Zasilanie: sieć, UPS, agregat, napięcie nastawcze.</summary>
    public readonly Zasilanie Zasilanie;
    /// <summary>Sygnalizatory z kartą (świetlne, sterowane przez KZ).</summary>
    public static bool MaKarte(Model.SignalDef d) => d.Type is "wjazdowy" or "wyjazdowy" or "odstepowy" or "tm";

    public Urzadzenia(Simulator s)
    {
        S = s;
        Zasilanie = new Zasilanie(s);
        foreach (var id in s.Secs.Keys) Odcinki[id] = new KartaOdcinka(s, id);
        foreach (var id in s.Sws.Keys) Zwrotnice[id] = new KartaZwrotnicy(s, id);
        foreach (var id in s.Ders.Keys) Wykolejnice[id] = new KartaWykolejnicy(s, id);
        foreach (var d in s.L.Signals.Values) if (MaKarte(d)) Sygnalizatory[d.Name] = new KartaSygnalizatora(s, d.Name);
    }

    /// <summary>
    /// Cykl kart (przed komputerem zależności): wykonanie rozkazów z poprzedniego cyklu, przekładanie, statusy do KZ – faza
    /// <see cref="FazaWejscia.Karty"/> rejestru wejść (KZ dostaje zamrożone statusy po cyklu wszystkich kart, w kolejności zgłoszeń).
    /// </summary>
    public void Tick()
    {
        var faza = S.Wejscia.Faza(FazaWejscia.Karty);
        try
        {
            Zasilanie.Tick(); Zasilanie.Publikuj();
            foreach (var k in Zwrotnice.Values) { k.Tick(); k.Publikuj(); }
            foreach (var k in Wykolejnice.Values) { k.Tick(); k.Publikuj(); }
            foreach (var k in Sygnalizatory.Values) { k.Tick(); k.Publikuj(); }
        }
        catch (Exception ex) { RejestrWejscCyklu.ZamknijPoBledzie(faza, ex); throw; }
        faza.Dispose();
    }

    // ------------------------------------------------------------------ teren ← ruch pociągów
    /// <summary>
    /// Ruch pociągów zgłasza odcinki, na których w tym cyklu są osie pojazdów; karty liczą stan i meldują go KZ. Zajętość wszystkich
    /// odcinków – faza <see cref="FazaWejscia.Ruch"/> (KZ dostaje ją razem, zanim karta wykryje koniec przejazdu); kontrola zliczenia
    /// i pierwszy przejazd – natychmiastowa faza <see cref="FazaWejscia.ZdarzenieKarty"/> (status przed alarmem/wpisem karty, jak dawniej).
    /// </summary>
    public void Osie(IReadOnlySet<string> zajete)
    {
        var faza = S.Wejscia.Faza(FazaWejscia.Ruch);
        try { foreach (var k in Odcinki.Values) { k.Pojazd = zajete.Contains(k.Id); k.Publikuj(); } }
        catch (Exception ex) { RejestrWejscCyklu.ZamknijPoBledzie(faza, ex); throw; }
        faza.Dispose();
        faza = S.Wejscia.Faza(FazaWejscia.ZdarzenieKarty);
        try
        {
            foreach (var k in Odcinki.Values) k.KontrolaZliczenia();
            foreach (var k in Odcinki.Values) k.PierwszyPrzejazd();
        }
        catch (Exception ex) { RejestrWejscCyklu.ZamknijPoBledzie(faza, ex); throw; }
        faza.Dispose();
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
    public void Zeruj(string sec) => S.Wejscia.WFazie(FazaWejscia.Rozkaz, () => { LogRozkaz("odcinek", sec, "zerowanie licznika osi"); Odcinki[sec].Zeruj(); return 0; });
    /// <summary>Przestawienie zwrotnicy do położenia „+” / „-”.</summary>
    public void Przestaw(string sw, string pol) { LogRozkaz("zwrotnica", sw, $"przestaw do „{pol}”"); Zwrotnice[sw].Rozkaz(pol); }
    /// <summary>Założenie (true) / zdjęcie (false) wykolejnicy.</summary>
    public void Wykolejnica(string name, bool zaloz) { LogRozkaz("wykolejnica", name, zaloz ? "załóż" : "zdejmij"); Wykolejnice[name].Rozkaz(zaloz); }
    /// <summary>Sygnał do wyświetlenia (kod Ie-1).</summary>
    public void Sygnal(string sig, string kod) { if (!Sygnalizatory.TryGetValue(sig, out var k)) return; LogRozkaz("sygnalizator", sig, kod == "" ? "wygaś" : "sygnał " + kod); k.Rozkaz(kod); }
    /// <summary>Napięcie nastawcze: załącz (NN) / odłącz (ONN).</summary>
    public void NapiecieNastawcze(bool zal) => S.Wejscia.WFazie(FazaWejscia.Rozkaz, () => { LogRozkaz("zasilanie", Zasilanie.Id, zal ? "załącz napięcie nastawcze" : "odłącz napięcie nastawcze"); Zasilanie.RozkazNN(zal); return 0; });
    /// <summary>Uruchomienie agregatu (AGR).</summary>
    public void UruchomAgregat() => S.Wejscia.WFazie(FazaWejscia.Rozkaz, () => { LogRozkaz("zasilanie", Zasilanie.Id, "uruchom agregat"); Zasilanie.RozkazAgregat(); return 0; });
}

namespace LcsWidzew.Sim;

/// <summary>
/// Zasilanie urządzeń srk jako urządzenie (Ie-20 § 6 ust. 7, § 23, zał. pkt 21; PODZIAL_MODULOW.md krok 7):
/// sieć, UPS, agregat prądotwórczy (samoczynny rozruch po zaniku sieci) i napięcie nastawcze napędów zwrotnic i wykolejnic.
/// Teren: to, co jest naprawdę w szafie zasilającej. Komputer zależności dostaje status (<see cref="Publikuj"/> → rejestr wejść
/// cyklu, S02A.02) i trzyma jego kopię w <see cref="Simulator.Power"/>. Rozkazy (NN, ONN, AGR) i zmiany zasilania z usterki
/// zgłaszane są od razu – w natychmiastowej fazie Rozkaz/Usterka, bez czekania na cykl (przełączenie w szafie, nie sterownik w pętli).
/// </summary>
public class Zasilanie
{
    readonly Simulator S;
    /// <summary>Identyfikator karty (dziennik KZ ↔ karty, Komputerownia).</summary>
    public const string Id = "zasilanie";
    /// <summary>Teren: zasilanie sieciowe obecne.</summary>
    public bool Siec = true;
    /// <summary>Teren: UPS sprawny (podtrzymuje urządzenia przy zaniku sieci; bez modelu rozładowania).</summary>
    public bool Ups = true;
    /// <summary>Teren: agregat pracuje.</summary>
    public bool Agregat;
    /// <summary>Agregat uruchamia się samoczynnie po zaniku sieci (po <see cref="CzasRozruchu"/> s).</summary>
    public bool AgregatSamoczynny = true;
    /// <summary>Usterka „mainsGenFail”: agregat nie uruchamia się samoczynnie (tylko poleceniem AGR).</summary>
    public bool AgregatUszkodzony;
    /// <summary>Teren: napięcie nastawcze załączone (bez niego napędy zwrotnic stoją).</summary>
    public bool NN = true;
    /// <summary>Czas zaniku sieci (null – sieć jest).</summary>
    public double? ZanikOd;
    public const double CzasRozruchu = 15;
    /// <summary>Ostatni rozkaz KZ (podgląd w programie Komputerownia) i czas jego odebrania.</summary>
    public string OstRozkaz; public double? OstRozkazT;

    public Zasilanie(Simulator s) { S = s; }

    /// <summary>Cykl: samoczynny rozruch agregatu po zaniku sieci.</summary>
    public void Tick()
    {
        if (!Siec && !Agregat && AgregatSamoczynny && !AgregatUszkodzony && ZanikOd is double o && S.T - o > CzasRozruchu)
        {
            Agregat = true; S.Log("Zdarzenie", "Agregat prądotwórczy uruchomiony samoczynnie", "Zasilanie", "AGR");
        }
    }

    StatusZasilania ostatni;
    /// <summary>Status przez rejestr wejść cyklu; niezmieniony stan – ten sam niezmienny rekord co poprzednio.</summary>
    public void Publikuj()
    {
        bool nieRuszyl = !this.Siec && !this.Agregat && AgregatUszkodzony;
        if (ostatni == null || ostatni.Siec != Siec || ostatni.Ups != Ups || ostatni.Agregat != Agregat || ostatni.NapiecieNastawcze != NN || ostatni.AgregatNieRuszyl != nieRuszyl)
            ostatni = new StatusZasilania { Obiekt = Id, Siec = this.Siec, Ups = this.Ups, Agregat = this.Agregat, NapiecieNastawcze = NN, AgregatNieRuszyl = nieRuszyl };
        S.Wejscia.Zglos(ostatni);
    }

    /// <summary>Rozkaz KZ: załącz (true) / odłącz (false) napięcie nastawcze.</summary>
    public void RozkazNN(bool zal) { OstRozkaz = zal ? "załącz napięcie nastawcze" : "odłącz napięcie nastawcze"; OstRozkazT = S.T; NN = zal; Publikuj(); }

    /// <summary>Rozkaz KZ: uruchom agregat (polecenie AGR – także przy agregacie, który nie ruszył samoczynnie).</summary>
    public void RozkazAgregat() { OstRozkaz = "uruchom agregat"; OstRozkazT = S.T; Agregat = true; AgregatUszkodzony = false; Publikuj(); }

    /// <summary>Usterki instruktora: „mains” – zanik sieci, „mainsGenFail” – zanik sieci i agregat nie rusza samoczynnie.</summary>
    public bool Usterka(string rodzaj, bool usun)
    {
        if (rodzaj is not ("mains" or "mainsGenFail")) return false;
        if (!usun) { Siec = false; ZanikOd = S.T; AgregatUszkodzony = rodzaj == "mainsGenFail"; }
        else { Siec = true; Agregat = false; ZanikOd = null; AgregatUszkodzony = false; }
        Publikuj();
        return true;
    }
}

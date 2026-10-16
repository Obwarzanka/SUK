namespace LcsWidzew.Sim;

/// <summary>
/// Karta kontroli niezajętości odcinka (Zrodla/EBILock_komputerownia.md 4.5): obwód torowy albo licznik osi.
/// Teren: osie pojazdów na odcinku (zgłasza ruch pociągów). Karta wylicza stan i melduje go komputerowi zależności
/// (<see cref="Publikuj"/> → <see cref="SecState"/>). Przyjmuje rozkaz zerowania licznika osi (ZeroLO).
/// </summary>
public class KartaOdcinka
{
    readonly Simulator S;
    public readonly string Id;
    /// <summary>Licznik osi (true) albo obwód torowy (false).</summary>
    public readonly bool LicznikOsi;

    /// <summary>Teren: na odcinku są osie pojazdu.</summary>
    public bool Pojazd;
    /// <summary>Stan urządzenia: null – sprawne; „occ” – zajętość bez pojazdu (usterka); „reset” – licznik wymaga zerowania; „await” – po zerowaniu czeka na pierwszy przejazd.</summary>
    public string Stan;
    /// <summary>Licznik osi: błąd zliczenia przy najbliższym przejeździe (usterka instruktora).</summary>
    public bool Miscount;
    /// <summary>Zajętość w poprzednim cyklu (wykrycie końca przejazdu).</summary>
    public bool PrevOcc;
    /// <summary>Po zerowaniu: przejazd kontrolny już się zaczął.</summary>
    public bool SawTrain;

    public KartaOdcinka(Simulator s, string id)
    {
        S = s; Id = id;
        LicznikOsi = s.L.Sections.TryGetValue(id, out var d) && d.AxleCounter;
    }

    /// <summary>Status dla komputera zależności: zajętość przez pojazd i stan urządzenia.</summary>
    public void Publikuj() { var k = S.Secs[Id]; k.TrainOcc = Pojazd; k.Fault = Stan; }

    /// <summary>Koniec przejazdu przy błędzie zliczenia: liczba osi się nie zgadza – odcinek zostaje zajęty, wymagane ZeroLO.</summary>
    public void KontrolaZliczenia()
    {
        if (Miscount && PrevOcc && !Pojazd && Stan == null)
        {
            Miscount = false; Stan = "reset"; Publikuj();
            S.Alarm($"Odcinek {Id}: licznik osi – niezgodność liczby osi po przejeździe, odcinek zajęty (sprawdź niezajętość, ZeroLO)", "Odcinek", Id, "lo" + Id, () => Stan == "reset");
        }
        PrevOcc = Pojazd;
    }

    /// <summary>Po zerowaniu: pierwszy przejazd (wjazd i zwolnienie) przywraca kontrolę niezajętości.</summary>
    public void PierwszyPrzejazd()
    {
        if (Stan != "await") return;
        if (Pojazd) SawTrain = true;
        else if (SawTrain) { Stan = null; SawTrain = false; Publikuj(); S.Log("Zdarzenie", "Pierwszy przejazd zakończony – kontrola niezajętości przywrócona", "Odcinek", Id); }
    }

    /// <summary>Rozkaz komputera zależności: zerowanie licznika osi (ZeroLO po potwierdzeniu).</summary>
    public void Zeruj() { Stan = "await"; Publikuj(); }

    /// <summary>Usterka instruktora: „secOcc” – zajętość bez pojazdu, „loMiscount” – błąd zliczenia osi. Zwraca false, gdy usterka nie dotyczy odcinka.</summary>
    public bool Usterka(string rodzaj, bool usun)
    {
        switch (rodzaj)
        {
            case "secOcc" when !LicznikOsi:
                // obwód torowy: zajętość bez pojazdu (zwarcie toków, uszkodzenie); po ustąpieniu przyczyny odcinek wolny bez zerowania
                if (!usun) { Stan = "occ"; Publikuj(); S.Alarm($"Odcinek {Id} (obwód torowy): zajętość bez pojazdu", "Odcinek", Id, "occ" + Id, () => Stan != null); }
                else { Stan = null; Publikuj(); S.Log("Zdarzenie", $"Odcinek {Id} (obwód torowy): zajętość ustąpiła – odcinek wolny", "Odcinek", Id); }
                return true;
            case "secOcc":
                if (!usun) { Stan = "occ"; Publikuj(); S.Alarm($"Odcinek {Id}: zajętość bez pojazdu", "Odcinek", Id, "occ" + Id, () => Stan != null); }
                else { Stan = "reset"; Publikuj(); S.Alarm($"Odcinek {Id}: wymagane potwierdzenie zerowania (ZeroLO)", "Odcinek", Id, "rst" + Id, () => Stan == "reset"); }
                return true;
            case "loMiscount":
                if (!LicznikOsi) { S.Log("Instruktor", $"Odcinek {Id} kontrolowany obwodem torowym – błąd zliczenia osi nie dotyczy", "Odcinek", Id); return false; }
                Miscount = !usun;
                return true;
        }
        return false;
    }
}

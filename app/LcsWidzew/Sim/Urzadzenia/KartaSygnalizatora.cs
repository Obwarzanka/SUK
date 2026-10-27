namespace LcsWidzew.Sim;

/// <summary>
/// Karta sygnalizatora (Zrodla/EBILock_komputerownia.md 4.4, PODZIAL_MODULOW.md krok 6): rozkaz KZ z konkretnym sygnałem wg Ie-1
/// (S1…S13a, Sz, Ms1, Ms2), wykonanie w następnym cyklu, zapalenie komór, status do KZ przez rejestr wejść cyklu (sygnał wyświetlany, uszkodzenie światła
/// zezwalającego, brak łączności). Bez modelu włókien rezerwowych i sygnału niższego (decyzja właściciela, PODZIAL_MODULOW.md § 7 pkt 2):
/// przy uszkodzeniu światła zezwalającego sygnalizator zostaje na sygnale „Stój”.
/// </summary>
public class KartaSygnalizatora
{
    readonly Simulator S;
    public readonly string Id;
    /// <summary>Tarcza manewrowa (stan zasadniczy Ms1) albo semafor (S1).</summary>
    public readonly bool Tarcza;
    /// <summary>Teren: sygnał wyświetlany (kod z tabeli Ie-1; „” – ciemny).</summary>
    public string Wyswietlany;
    /// <summary>Ostatni rozkaz KZ (sygnał) i czas jego odebrania.</summary>
    public string OstRozkaz; public double? OstRozkazT;
    string rozkaz;
    /// <summary>Uszkodzenie światła zezwalającego (usterka instruktora „sigLamp”) – sygnalizator nie zapala sygnału zezwalającego.</summary>
    public bool UszkZezwalajacego;
    /// <summary>Brak łączności karty z komputerem zależności („sigNodata”) – sterownik w stanie bezpiecznym („Stój”).</summary>
    public bool BrakLacznosci;
    /// <summary>Rozkaz zezwalający nie został wykonany (uszkodzenie) – do statusu.</summary>
    public bool NieZapalil;

    public KartaSygnalizatora(Simulator s, string id)
    {
        S = s; Id = id;
        Tarcza = s.L.Signals[id].Type == "tm";
        Wyswietlany = Stoj;
    }

    public string Stoj => Tarcza ? "Ms1" : "S1";

    /// <summary>Rozkaz KZ: sygnał do wyświetlenia (wykonanie w następnym cyklu).</summary>
    public void Rozkaz(string kod) { rozkaz = kod; OstRozkaz = kod; OstRozkazT = S.T; }

    public void Tick()
    {
        var cel = rozkaz ?? OstRozkaz ?? Stoj; rozkaz = null;
        if (BrakLacznosci) { Wyswietlany = Stoj; NieZapalil = false; return; }
        var d = Sygnaly.Def(cel);
        NieZapalil = UszkZezwalajacego && d != null && d.Zezwalajacy && cel != "Sz";
        Wyswietlany = NieZapalil ? Stoj : cel;
    }

    StatusSygnalizatora ostatni;
    /// <summary>Status przez rejestr wejść cyklu; niezmieniony stan – ten sam niezmienny rekord co poprzednio.</summary>
    public void Publikuj()
    {
        if (ostatni == null || !string.Equals(ostatni.Wyswietlany, Wyswietlany, StringComparison.Ordinal) || ostatni.UszkodzenieZezwalajacego != UszkZezwalajacego || ostatni.BrakLacznosci != BrakLacznosci)
            ostatni = new StatusSygnalizatora { Obiekt = Id, Wyswietlany = Wyswietlany, UszkodzenieZezwalajacego = UszkZezwalajacego, BrakLacznosci = this.BrakLacznosci };
        S.Wejscia.Zglos(ostatni);
    }

    /// <summary>Usterki instruktora: „sigLamp” – nie wyświetla sygnału zezwalającego, „sigNodata” – brak komunikacji z obiektem.</summary>
    public bool Usterka(string rodzaj, bool usun)
    {
        switch (rodzaj)
        {
            case "sigLamp": UszkZezwalajacego = !usun; return true;
            case "sigNodata": BrakLacznosci = !usun; return true;
        }
        return false;
    }
}

namespace LcsWidzew.Sim;

/// <summary>
/// Karta zwrotnicy – sterownik zwrotnicowy z napędem (Zrodla/EBILock_komputerownia.md 4.3, PODZIAL_MODULOW.md krok 5).
/// Teren: położenie iglic (<see cref="Iglice"/>), przekładanie napędem, zamarznięcie, uszkodzenie po rozpruciu.
/// Przyjmuje rozkaz przestawienia od komputera zależności (<see cref="Rozkaz"/>), wykonuje go w następnym cyklu
/// i melduje status (<see cref="Publikuj"/> → <see cref="SwState"/>): położenie z kontrolą albo brak kontroli,
/// przekładanie, przekroczony czas przekładania. Rozprucie rozstrzyga komputer zależności (brak kontroli przy zajętym odcinku).
/// </summary>
public class KartaZwrotnicy
{
    readonly Simulator S;
    public readonly string Id;
    /// <summary>Napęd ręczny / zwrotnica nieuzależniona – przekładanie 4× dłużej.</summary>
    public readonly bool Reczna;

    /// <summary>Teren: położenie iglic „+” / „-” (w czasie przekładania – położenie wyjściowe, aż do dojścia iglic).</summary>
    public string Iglice = "+";
    /// <summary>Przekładanie w toku (cel, położenie wyjściowe, czas zakończenia i początku).</summary>
    public MoveInfo Ruch;
    /// <summary>Rozkaz odebrany od KZ, wykonywany w następnym cyklu.</summary>
    string rozkaz;
    /// <summary>Zamarznięcie: napęd nie dociąga iglic do położenia docelowego (wraca tylko do wyjściowego).</summary>
    public bool Zamarznieta;
    /// <summary>Uszkodzenie układu kontroli położenia (usterka instruktora) – iglice na miejscu, brak kontroli.</summary>
    public bool UszkKontroli;
    /// <summary>Iglice uszkodzone mechanicznie (rozprucie przez pojazd) – brak kontroli do naprawy.</summary>
    public bool Uszkodzona;

    public KartaZwrotnicy(Simulator s, string id)
    {
        S = s; Id = id;
        Reczna = s.L.Switches[id].Manual;
    }

    /// <summary>Kontrola położenia: iglice przylegają, napęd w spoczynku, układ kontroli sprawny.</summary>
    public bool Kontrola => Ruch == null && rozkaz == null && !UszkKontroli && !Uszkodzona;
    /// <summary>Nadzór czasu przekładania: przekładanie trwa dłużej niż <see cref="Simulator.Cfg.SwTimeout"/>.</summary>
    public bool PrzekroczonyCzas => Ruch != null && S.T - Ruch.Started > Simulator.Cfg.SwTimeout;

    /// <summary>Rozkaz komputera zależności: przestaw do położenia „+” / „-” (wykonanie w następnym cyklu).</summary>
    public void Rozkaz(string pol) => rozkaz = pol;

    /// <summary>Cykl karty: przyjęcie rozkazu, przekładanie (napięcie nastawcze), dojście iglic.</summary>
    public void Tick()
    {
        if (rozkaz != null)
        {
            if (Uszkodzona) rozkaz = null;            // iglice uszkodzone – napęd nie przekłada
            else
            {
                var from = Ruch != null ? Ruch.From : Iglice;
                double czas = Reczna ? Simulator.Cfg.SwTime * 4 : Simulator.Cfg.SwTime;
                Ruch = new MoveInfo { Target = rozkaz, From = from, Until = S.T + czas, Started = S.T };
                rozkaz = null;
            }
        }
        if (Ruch == null) return;
        if (!S.Power.NN && !Zamarznieta) return;      // bez napięcia nastawczego napęd stoi
        if (Zamarznieta)
        {
            // zamarznięta zwrotnica wraca tylko do położenia wyjściowego (Ie-20 §24 ust. 6)
            if (Ruch.Target == Ruch.From && S.T >= Ruch.Until) { Iglice = Ruch.Target; Ruch = null; }
            return;
        }
        if (S.T >= Ruch.Until) { Iglice = Ruch.Target; Ruch = null; }
    }

    /// <summary>Status dla komputera zależności (kopia w <see cref="SwState"/>).</summary>
    public void Publikuj() => S.StatusZwrotnicy(Id, Kontrola ? Iglice : null, Ruch != null || rozkaz != null, PrzekroczonyCzas);

    /// <summary>
    /// Teren: pojazd najeżdża zwrotnicę od strony gałęzi <paramref name="pol"/>. Iglice w innym położeniu albo w ruchu – rozprucie:
    /// iglice zostają dociśnięte do gałęzi pojazdu i uszkodzone (brak kontroli). Zwraca true przy rozpruciu.
    /// </summary>
    public bool Najazd(string pol)
    {
        if (Iglice == pol && Ruch == null && rozkaz == null) return false;
        Iglice = pol; Ruch = null; rozkaz = null; Uszkodzona = true;
        return true;
    }

    /// <summary>Usterki instruktora: „swNoCtrl” – brak kontroli położenia, „swFrozen” – zamarznięcie, „swRozpr” – uszkodzenie iglic.</summary>
    public bool Usterka(string rodzaj, bool usun)
    {
        switch (rodzaj)
        {
            case "swNoCtrl": UszkKontroli = !usun; return true;
            case "swFrozen": Zamarznieta = !usun; return true;
            case "swRozpr": Uszkodzona = !usun; return true;
        }
        return false;
    }
}

/// <summary>
/// Karta wykolejnicy (sterownik z napędem wykolejnicy). Rozkaz założenia / zdjęcia od KZ, przekładanie w czasie
/// <see cref="Simulator.Cfg.SwTime"/>, status do <see cref="DerState"/>. Teren: wykolejnica na szynie (<see cref="Zalozona"/>).
/// </summary>
public class KartaWykolejnicy
{
    readonly Simulator S;
    public readonly string Id;
    /// <summary>Teren: wykolejnica na szynie (zmienia się po dojściu napędu).</summary>
    public bool Zalozona = true;
    /// <summary>Przekładanie: cel i czas zakończenia.</summary>
    public bool? Cel; public double Until;
    bool? rozkaz;

    public KartaWykolejnicy(Simulator s, string id) { S = s; Id = id; }

    /// <summary>Rozkaz KZ: założyć (true) / zdjąć (false); wykonanie w następnym cyklu.</summary>
    public void Rozkaz(bool zaloz) => rozkaz = zaloz;

    public void Tick()
    {
        if (rozkaz is bool r)
        {
            rozkaz = null;
            if (r != Zalozona || Cel != null) { Cel = r; Until = S.T + Simulator.Cfg.SwTime; }
        }
        if (Cel is bool c && S.T >= Until) { Zalozona = c; Cel = null; }
    }

    public void Publikuj() => S.StatusWykolejnicy(Id, Cel == null && rozkaz == null ? Zalozona : null);
}

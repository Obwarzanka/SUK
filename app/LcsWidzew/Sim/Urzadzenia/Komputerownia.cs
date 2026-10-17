using LcsWidzew.Model;

namespace LcsWidzew.Sim;

/// <summary>Wiersz podglądu karty dla programu Komputerownia: rozkaz od KZ, status do KZ, stan terenu, usterki.</summary>
public class KartaWiersz
{
    /// <summary>Rodzaj karty: „odcinek”, „zwrotnica”, „wykolejnica” (później „sygnalizator”).</summary>
    public string Rodzaj;
    public string Id, Post, Obiekt, Typ;
    /// <summary>Ostatni rozkaz komputera zależności i czas symulacji jego odebrania (null – brak rozkazu).</summary>
    public string Rozkaz; public double? RozkazT;
    /// <summary>Status meldowany komputerowi zależności.</summary>
    public string Status;
    /// <summary>Teren (to, co jest naprawdę przy torze).</summary>
    public string Teren;
    /// <summary>Usterki karty / urządzenia (klucze z <see cref="Instruktor.Faults"/>), opis słowny w <see cref="UsterkiOpis"/>.</summary>
    public List<string> Usterki = new(); public string UsterkiOpis = "";
    /// <summary>Stan inny niż zasadniczy (rozkaz w toku, brak kontroli, usterka) – do filtru „tylko nietypowe”.</summary>
    public bool Nietypowa;
}

public class UsterkaDef { public string Key, Name, Kind; }

/// <summary>Stan wszystkich kart – odpowiedź na żądanie „karty” programu Komputerownia.</summary>
public class KomputerowniaStan
{
    public string Stacja, Zegar; public double T;
    public Dictionary<string, string> Posterunki = new();
    public List<KartaWiersz> Karty = new();
    public List<UsterkaDef> Usterki = new();
}

public partial class Urzadzenia
{
    /// <summary>Podgląd kart dla programu Komputerownia (osobny program, łączy się przez sieć).</summary>
    public KomputerowniaStan Stan()
    {
        var res = new KomputerowniaStan { Stacja = S.L.Name, Zegar = S.Clock(), T = S.T };
        foreach (var (k, v) in S.L.Posts) res.Posterunki[k] = v;
        foreach (var f in Instruktor.Faults.Where(f => f.Target is "sec" or "sw" or "sig")) res.Usterki.Add(new UsterkaDef { Key = f.Key, Name = f.Name, Kind = f.Target switch { "sec" => "odcinek", "sw" => "zwrotnica", _ => "sygnalizator" } });
        foreach (var k in Odcinki.Values) res.Karty.Add(Wiersz(k));
        foreach (var k in Zwrotnice.Values) res.Karty.Add(Wiersz(k));
        foreach (var k in Wykolejnice.Values) res.Karty.Add(Wiersz(k));
        foreach (var k in Sygnalizatory.Values) res.Karty.Add(Wiersz(k));
        return res;
    }

    KartaWiersz Nowy(string rodzaj, string id, string typ) => new() { Rodzaj = rodzaj, Id = id, Post = Station.PostOf(id), Obiekt = Station.Lbl(id), Typ = typ };

    KartaWiersz Wiersz(KartaOdcinka k)
    {
        var w = Nowy("odcinek", k.Id, k.LicznikOsi ? "licznik osi" : "obwód torowy");
        w.Rozkaz = k.OstRozkaz; w.RozkazT = k.OstRozkazT;
        w.Status = (k.Pojazd ? "zajęty" : k.Stan == null ? "wolny" : "zajęty") + k.Stan switch
        {
            "occ" => " – zajętość bez pojazdu", "reset" => " – wymaga zerowania", "await" => " – oczekuje na pierwszy przejazd", _ => "",
        };
        w.Teren = k.Pojazd ? "osie pojazdu na odcinku" : "brak pojazdu";
        if (k.Stan == "occ") w.Usterki.Add("secOcc");
        if (k.Miscount) w.Usterki.Add("loMiscount");
        w.UsterkiOpis = string.Join(", ", new[] { k.Stan == "occ" ? "zajętość bez pojazdu" : null, k.Miscount ? "błąd zliczenia przy najbliższym przejeździe" : null }.Where(x => x != null));
        w.Nietypowa = k.Stan != null || k.Miscount;
        return w;
    }

    KartaWiersz Wiersz(KartaZwrotnicy k)
    {
        var w = Nowy("zwrotnica", k.Id, k.Reczna ? "ręczna" : "elektryczny");
        w.Rozkaz = k.OstRozkaz != null ? $"przestaw do „{k.OstRozkaz}”" : null; w.RozkazT = k.OstRozkazT;
        w.Status = k.Kontrola ? $"kontrola „{k.Iglice}”" : k.Ruch != null ? $"przekładanie → „{k.Ruch.Target}”, {Math.Max(0, k.Ruch.Until - S.T):0.0} s" : k.RozkazWToku ? "rozkaz przyjęty" : "BRAK KONTROLI";
        if (k.PrzekroczonyCzas) w.Status += ", przekroczony czas przekładania";
        w.Teren = $"iglice „{k.Iglice}”" + (k.Ruch != null ? (k.Zamarznieta && k.Ruch.Target != k.Ruch.From ? ", napęd pracuje – iglice nie dochodzą" : ", w ruchu") : "") + (k.Uszkodzona ? ", iglice uszkodzone" : "");
        if (k.UszkKontroli) w.Usterki.Add("swNoCtrl");
        if (k.Zamarznieta) w.Usterki.Add("swFrozen");
        if (k.Uszkodzona) w.Usterki.Add("swRozpr");
        w.UsterkiOpis = string.Join(", ", new[] { k.UszkKontroli ? "uszkodzony układ kontroli" : null, k.Zamarznieta ? "zamarznięcie" : null, k.Uszkodzona ? "iglice uszkodzone" : null }.Where(x => x != null));
        w.Nietypowa = !k.Kontrola || w.Usterki.Count > 0;
        return w;
    }

    KartaWiersz Wiersz(KartaWykolejnicy k)
    {
        var w = Nowy("wykolejnica", k.Id, "elektryczny");
        w.Rozkaz = k.OstRozkaz is bool r ? (r ? "założyć" : "zdjąć") : null; w.RozkazT = k.OstRozkazT;
        w.Status = k.Cel is bool c ? $"przekładanie → {(c ? "założona" : "zdjęta")}, {Math.Max(0, k.Until - S.T):0.0} s" : k.Zalozona ? "założona" : "zdjęta";
        w.Teren = k.Zalozona ? "na szynie" : "zdjęta z szyny";
        w.Nietypowa = k.Cel != null;
        return w;
    }

    static string Opis(string kod) => kod == null ? "–" : Sygnaly.Def(kod) is { } d ? (kod == "" ? "ciemny" : $"{kod} „{d.Nazwa}”") : kod;

    KartaWiersz Wiersz(KartaSygnalizatora k)
    {
        var d = S.L.Signals[k.Id];
        var w = Nowy("sygnalizator", k.Id, d.Type switch { "wjazdowy" => "semafor wjazdowy", "wyjazdowy" => "semafor wyjazdowy", "odstepowy" => "semafor odstępowy", "tm" => "tarcza manewrowa", _ => d.Type });
        if (string.IsNullOrEmpty(w.Post) && d.Szlak != null) w.Post = d.Szlak;
        w.Rozkaz = k.OstRozkaz != null ? Opis(k.OstRozkaz) : null; w.RozkazT = k.OstRozkazT;
        w.Status = $"wyświetla {(k.Wyswietlany == "" ? "– (ciemny)" : k.Wyswietlany)}" + (k.NieZapalil ? ", NIE ZAPALIŁ SYGNAŁU ZEZWALAJĄCEGO" : "") + (k.BrakLacznosci ? ", BRAK ŁĄCZNOŚCI" : "");
        w.Teren = Sygnaly.Def(k.Wyswietlany)?.OpisSwiatel() ?? k.Wyswietlany;
        if (k.UszkZezwalajacego) w.Usterki.Add("sigLamp");
        if (k.BrakLacznosci) w.Usterki.Add("sigNodata");
        w.UsterkiOpis = string.Join(", ", new[] { k.UszkZezwalajacego ? "uszkodzone światło zezwalające" : null, k.BrakLacznosci ? "brak łączności z kartą" : null }.Where(x => x != null));
        w.Nietypowa = w.Usterki.Count > 0 || (k.OstRozkaz != null && k.OstRozkaz != k.Wyswietlany);
        return w;
    }
}

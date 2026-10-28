using System.Globalization;
using LcsWidzew.Model;

namespace LcsWidzew.Sim;

/// <summary>Jedna wersja wiersza dziennika ruchu (EDR nie kasuje wpisów – każda zmiana to nowa wersja).</summary>
public class EdrWersja
{
    /// <summary>Kolumny dziennika: numer pociągu (kol. 1 / 2 wg parzystości), tor stacyjny (3), droga wolna (4),
    /// pociąg odjechał (5), pociąg przyjechał (6), uwagi (7). Godziny „HH:mm”, „---” – nie dotyczy.</summary>
    public string Nr = "", Tor = "", Dw = "", Odj = "", Prz = "", Uwagi = "";
    public bool Lewy;
    /// <summary>Kto i kiedy wprowadził tę wersję (czas symulacji, data i godzina do sekundy), które pole zmieniono.</summary>
    public string Kto, Zmiana, Pole;
    public double T;
    /// <summary>Kolumna 8: powiadomienie dróżników przejazdowych (id dróżnika → godzina albo ukośnik „\\”).</summary>
    public Dictionary<string, string> Dr = new();
    /// <summary>Godzina wprowadzenia do EDR każdej wpisanej godziny (pole → HH:mm); komórka pokazuje „godzina / godzina wprowadzenia” (film ISDR).</summary>
    public Dictionary<string, string> Wpis = new();
    public EdrWersja Kopia() { var w = (EdrWersja)MemberwiseClone(); w.Dr = new(Dr ?? new()); w.Wpis = new(Wpis ?? new()); return w; }
}

/// <summary>Wiersz dziennika: przyjęcie / wyprawienie pociągu albo wpis na całą szerokość (objęcie dyżuru, zmiana doby).</summary>
public class EdrWiersz
{
    public int Id;
    /// <summary>„przyj”, „wypr” albo „tekst”.</summary>
    public string Rodzaj;
    public string Tekst;
    public List<EdrWersja> Wersje = new();
    /// <summary>Telefonogram wpisany do dziennika ruchu (Rodzaj „telefonogram”).</summary>
    public EdrTelefonogram Tel;
    /// <summary>Jazda do km i z powrotem: drugi wiersz pary (klamra; Rodzaj „powrot” – wiersz powrotu ze szlaku).</summary>
    public int Para;
    public bool Pierwszy => Para != 0 && Rodzaj != "powrot";
    /// <summary>Pola wymagane do uznania zapisu za kompletny (niebieskie tło, dopóki puste).</summary>
    public List<string> Wymagane = new();
    [System.Text.Json.Serialization.JsonIgnore] public EdrWersja W => Wersje.Count > 0 ? Wersje[^1] : null;
    public bool Kompletny => Rodzaj is "tekst" or "telefonogram" or "wpis" || Wymagane.All(p => Edr.Wartosc(W, p) != "");
}

/// <summary>
/// Telefonogram albo inna okoliczność w dzienniku telefonicznym posterunku (film ISDR: okno „Telefonogram do Dziennika Telefonicznego”,
/// podręcznik EDR rozdz. 7.3). Telefonogram z dziennika ruchu trafia też do dziennika ruchu jako wpis na całą szerokość.
/// Wzory telefonogramów – później (na razie „[bez szablonu]”).
/// </summary>
public class EdrTelefonogram
{
    /// <summary>„telefonogram” albo „okolicznosc” (Rejestruj inne okoliczności).</summary>
    public string Rodzaj = "telefonogram";
    /// <summary>Numer własny (kolejny w dobie na posterunku), numer odbiorcy (numer telefonogramu u drugiej strony).</summary>
    public int Nr;
    public string NrOdbiorcy = "";
    public bool Nadaje = true;
    public string Data = "", Godz = "", CzasZak = "", Nadal = "", Odebral = "", Wzor = "[bez szablonu]", Tresc = "", Notatka = "";
    /// <summary>Posterunek, który nadał i który odebrał (nazwy do wpisu „Nadał … , odebrał …”).</summary>
    public string NadalPost = "", OdebralPost = "";
    /// <summary>Dziennik ruchu, w którym jest wpis (null – tylko dziennik telefoniczny); posterunek; doba.</summary>
    public string Dziennik, Post;
    public int Doba;
    /// <summary>Zmiany stanu bieżącego torów szlakowych wprowadzone tym telefonogramem.</summary>
    public List<EdrZmianaToru> Zmiany = new();
    public string Kto, Zapisano;
    /// <summary>Godzina wprowadzenia do EDR (w podpisie „Nadał …, odebrał … godz. …”; godzina nadania – osobno).</summary>
    public string Wprowadzono = "";
}

/// <summary>Zmiana stanu bieżącego toru szlakowego telefonogramem (null – bez zmiany): status, rodzaj ruchu, kierunek zasadniczy, prowadzenie ruchu jak dla.</summary>
public class EdrZmianaToru { public string Szlak, Status, Rodzaj, Podstawa; public int? Zasadniczy; }

/// <summary>Strona dziennika – jedna doba.</summary>
public class EdrStrona { public int Doba; public string Data; public List<EdrWiersz> Wiersze = new(); }

/// <summary>Dziennik ruchu jednego szlaku przyległego do posterunku (wszystkie tory szlakowe do tego samego sąsiada po tej samej linii).</summary>
public class EdrDziennik
{
    public string Id, Post, Nazwa, Sasiad, Linia, Typ;
    public List<string> Tory = new(), Szlaki = new();
    public List<EdrStrona> Strony = new();
    public string Prowadzi;
    /// <summary>Zakładka okna EDR: dzienniki z tym samym numerem stoją obok siebie (konfiguracja posterunków).</summary>
    public int Zakladka;
    /// <summary>Parzystość numerów z kierunku szlaku (BlockDef.EvenDir): 1 – wyprawiane parzyste, −1 – przyjmowane parzyste, 0 – nie ustalono.</summary>
    public int Parzysty;
    /// <summary>Tory szlakowe w kolejności ikon (tor lewy przed prawym, wg ustawień) ze stanem z wpisów dziennika.</summary>
    public List<EdrTorSzlakowy> ToryStan = new();
    /// <summary>Ustawienia zasadnicze dziennika (kierunek zasadniczy i parzysty, pola wymagane, tory szlakowe) – plik &lt;układ&gt;.edr.json.</summary>
    public EdrUstawienia Ustawienia = new();
    /// <summary>Ustawienia bieżące: zasadnicze zmienione telefonogramami (zamknięcie toru, ruch dwukierunkowy, zapowiadanie telefoniczne …).</summary>
    public EdrUstawienia Biezace = new();
}

/// <summary>
/// Ustawienia zasadnicze dziennika ruchu (podręcznik EDR rozdz. 5.2.1, okno „Ustawienia … dziennika ruchu” na stanowisku ISDR):
/// kierunek parzysty, opcje wypełniania (pola wymagane), tory szlakowe. Ustala je człowiek w oknie EDR (Konfiguracja).
/// </summary>
public class EdrUstawienia
{
    /// <summary>Posterunki dróżników przejazdowych na szlaku (podręcznik EDR, Rys. 24) – kolumna 8 dziennika.</summary>
    public List<EdrDroznik> Droznicy = new();
    /// <summary>Kierunek pociągów parzystych: 1 – DO posterunku sąsiedniego, −1 – Z posterunku sąsiedniego, 0 – nieustalony.</summary>
    public int Parzysty;
    /// <summary>Opcje wypełniania dziennika: „Tor stacyjny”, „Droga wolna”, „Pociąg odjechał”, „Pociąg przyjechał” wymagane.</summary>
    public bool WymTor = true, WymDw, WymOdj = true, WymPrz = true;
    public List<EdrTorUstawienia> Tory = new();
    public EdrUstawienia Kopia() { var u = (EdrUstawienia)MemberwiseClone(); u.Tory = Tory.Select(t => t.Kopia()).ToList(); u.Droznicy = (Droznicy ?? new()).Select(x => x.Kopia()).ToList(); return u; }
}

/// <summary>
/// Posterunek dróżnika przejazdowego w ustawieniach dziennika (podręcznik EDR, Rys. 24): km, numer na linii, nazwa, oznaczenie w dzienniku ruchu,
/// powiadamianie o odjeździe pociągów – według szlaku (jednotorowego: godzina; dwu- i więcej torowego: ukośnik), dla pociągów, w dzienniku ruchu.
/// </summary>
public class EdrDroznik
{
    public string Id = Guid.NewGuid().ToString("N")[..8], Km = "", Numer = "", Nazwa = "", Oznaczenie = "";
    /// <summary>„Jednotorowych” – wpis godziny, „Dwu i więcej torowych” – ukośnik.</summary>
    public string WgSzlaku = "Jednotorowych";
    /// <summary>„W obu kierunkach”, „Z posterunku sąsiedniego”, „DO posterunku sąsiedniego”.</summary>
    public string DlaPociagow = "W obu kierunkach";
    public bool WDzienniku = true;
    public bool Godzina => WgSzlaku == "Jednotorowych";
    /// <summary>Dotyczy pociągu przyjmowanego (z posterunku sąsiedniego) albo wyprawianego (do niego).</summary>
    public bool Dotyczy(string rodzaj) => WDzienniku && (DlaPociagow == "W obu kierunkach" || (rodzaj is "przyj" ? DlaPociagow == "Z posterunku sąsiedniego" : DlaPociagow == "DO posterunku sąsiedniego"));
    public EdrDroznik Kopia() => (EdrDroznik)MemberwiseClone();
}

/// <summary>Wymagane sprawdzenie cykliczne posterunku (przyciski w prawym górnym rogu okna EDR; zakładka konfiguracji „Wymagane sprawdzenia”).</summary>
public class EdrSprawdzenie
{
    public string Id = Guid.NewGuid().ToString("N")[..8], Nazwa = "";
    /// <summary>Co ile minut.</summary>
    public int Okres = 240;
    public EdrSprawdzenie Kopia() => (EdrSprawdzenie)MemberwiseClone();
}

/// <summary>Stan sprawdzenia dla okna: ostatnie wykonanie, następny termin, „ok” / „wkrotce” (≤ 15 min) / „po_terminie”.</summary>
public class EdrSprawdzenieStan { public string Post, Id, Nazwa, Ostatnie = "", Kto = "", Nastepne = "", Stan = "ok"; public int Okres; }

/// <summary>Tor szlakowy w ustawieniach dziennika.</summary>
public class EdrTorUstawienia
{
    public string Szlak, Tor;
    /// <summary>„lewy” / „prawy” względem posterunku (szlak wielotorowy), puste – jednotorowy.</summary>
    public string Strona = "";
    public string Status = "Otwarty", Rodzaj = "Dwukierunkowy";
    /// <summary>Zasadniczy kierunek ruchu: −1 – Z posterunku sąsiedniego, 1 – DO posterunku sąsiedniego, 0 – nieustalony.</summary>
    public int Zasadniczy;
    /// <summary>Prowadzenie dziennika ruchu jak dla (podstawa prowadzenia ruchu na torze).</summary>
    public string Podstawa;
    /// <summary>Rodzaj urządzenia srk (z układu, tylko do odczytu).</summary>
    public string Urzadzenie;
    public EdrTorUstawienia Kopia() => (EdrTorUstawienia)MemberwiseClone();
}

/// <summary>
/// Tor szlakowy w nagłówku dziennika (podręcznik EDR, Rys. 37): zasadniczy kierunek (szare strzałki), zajętość i kierunek jazdy pociągu
/// (czerwony tor, niebieska strzałka) – z wpisów w dzienniku, nie z urządzeń srk (decyzja właściciela 2026-10-27).
/// </summary>
public class EdrTorSzlakowy
{
    public string Szlak, Tor;
    /// <summary>Zasadniczy kierunek: −1 – do naszego posterunku, 1 – od naszego posterunku, 0 – dwukierunkowy (nieustalony).</summary>
    public int Kierunek;
    /// <summary>Pociągi na torze wg dziennika (odjechał, a jeszcze nie przyjechał) i kierunek ich jazdy (−1 do nas, 1 od nas).</summary>
    public List<string> Pociagi = new();
    public int Ruch;
    public string Strona, Status;
    public bool Zajety => Pociagi.Count > 0;
}

/// <summary>Tor stacyjny w kontrolce zajętości: zajęty od przyjazdu pociągu na ten tor (wpis przyjęcia) do jego odjazdu (wpis wyprawienia).</summary>
public class EdrTorStacyjny { public string Tor, Pociag, Od; public bool Zajety => Pociag != null; }

/// <summary>Wiersz wykazu pociągów posterunku (plan z rozkładu jazdy i wykonanie z ruchu pociągów).</summary>
public class EdrPociag
{
    public string Klucz { get; set; }
    /// <summary>Numer przy przyjeździe na posterunek i przy odjeździe (inny po zmianie numeru, np. po zmianie czoła).</summary>
    public string Nr { get; set; }
    public string NrWyj { get; set; }
    public string Rodz { get; set; }
    public string Nazwa { get; set; }
    /// <summary>Szlak przyjazdu i odjazdu (id szlaku z układu) i nazwa posterunku sąsiedniego.</summary>
    public string ZSzlak { get; set; }
    public string ZKierunku { get; set; }
    public string WSzlak { get; set; }
    public string WKierunku { get; set; }
    public string Linia { get; set; }
    /// <summary>Tor stacyjny z rozkładu (kolumna PT) i tor, na którym pociąg był.</summary>
    public string Tor { get; set; }
    public string TorRzecz { get; set; }
    public string PrzPlan { get; set; }
    public string PrzOdch { get; set; }
    public string PrzRzecz { get; set; }
    public string OdjPlan { get; set; }
    public string OdjOdch { get; set; }
    public string OdjRzecz { get; set; }
    /// <summary>„ph” – postój handlowy wg rozkładu; puste – przejazd.</summary>
    public string Postoj { get; set; }
    public string Stan { get; set; }
    public bool Przyjety { get; set; }
    public bool Wyprawiony { get; set; }
    public bool NaStacji { get; set; }
    public double Sort { get; set; }
    /// <summary>Postój zamówiony (z rozkładu) i obliczony (z wykonania) [min], jak „P. zam.” i „P. obl.” w wykazie EDR.</summary>
    public string PZam { get; set; }
    public string PObl { get; set; }
    /// <summary>Wstęga (pasek zaznaczenia wg legendy EDR): „przyjazd”, „odjazd”, „przejazd” (bez zatrzymania) albo pusta.</summary>
    public string Wstega { get; set; }
    /// <summary>Czas przejazdu potwierdzony (kolor kolumny „Rodz.”): „czlowiek” – wpis dyżurnego w dzienniku (zielony), „auto” – urządzenie (czerwony),
    /// „dyspozytor” (szary); puste – niepotwierdzony.</summary>
    public string Potw { get; set; }
    public bool Potwierdzony => !string.IsNullOrEmpty(Potw);
    /// <summary>K – kursuje, NK – nie kursuje (zaznacza dyspozytor; EDR tylko pokazuje rozkład).</summary>
    public bool Kursuje { get; set; } = true;
    public bool NieKursuje => !Kursuje;
    /// <summary>Kategoria pociągu wg Ir-11, stacja początkowa i końcowa biegu, przewoźnik.</summary>
    public string StacjaPocz { get; set; }
    public string StacjaKonc { get; set; }
    public string Przewoznik { get; set; }
    /// <summary>Uwagi eksploatacyjne i przewoźnika (dyspozytor), uwagi własne dyżurnego.</summary>
    public string UwagiEksp { get; set; }
    public string UwagiPrzew { get; set; }
    public string UwagiWlasne { get; set; }
    /// <summary>Godziny z datą („dd-MM HH:mm”, „Prezentuj daty”).</summary>
    public string PrzPlanD { get; set; }
    public string PrzRzeczD { get; set; }
    public string OdjPlanD { get; set; }
    public string OdjRzeczD { get; set; }
    /// <summary>Opis pociągu: kod rodzaju z rozkładu, pojazd, długość; trasa wg rozkładu.</summary>
    public string Kod { get; set; }
    public string Pojazd { get; set; }
    public string Dlugosc { get; set; }
    public List<EdrTrasa> Trasa { get; set; } = new();
    /// <summary>Kolumny „Nr poc.” wykazu: numer nieparzysty (1) i parzysty (2) – pociąg zmieniający tylko parzystość ma oba.</summary>
    public string NrNieparzysty => new[] { Nr, NrWyj }.FirstOrDefault(n => !string.IsNullOrEmpty(n) && Edr.KolumnaNumeru(n) == 1) ?? "";
    public string NrParzysty => new[] { Nr, NrWyj }.FirstOrDefault(n => !string.IsNullOrEmpty(n) && Edr.KolumnaNumeru(n) == 2) ?? "";
    public EdrPociag Kopia() => (EdrPociag)MemberwiseClone();
}

/// <summary>Wykonanie rozkładu na posterunku: przyjazd czoła na stację i odjazd ze stacji (z ruchu pociągów).</summary>
public class EdrRuch { public string Nr, NrWyj, ZSzlak, WSzlak, Tor; public double? Prz, Odj; }

/// <summary>Stan EDR dla stanowiska: jego dzienniki, zegar i data bieżąca.</summary>
public class EdrData
{
    public List<EdrDziennik> Dzienniki = new();
    /// <summary>Tory stacyjne posterunków (lista do okna przyjęcia / wyprawienia).</summary>
    public Dictionary<string, List<string>> ToryStacyjne = new();
    /// <summary>Wykaz pociągów każdego posterunku stanowiska i nazwy posterunków.</summary>
    public Dictionary<string, List<EdrPociag>> Wykaz = new();
    public Dictionary<string, string> Posterunki = new();
    /// <summary>Dziennik telefoniczny każdego posterunku stanowiska (wszystkie doby).</summary>
    public Dictionary<string, List<EdrTelefonogram>> Telefoniczny = new();
    /// <summary>Numeracja telefonogramów posterunków („dobowa”, „miesieczna”, „roczna”) i następny numer własny.</summary>
    public Dictionary<string, string> Numeracja = new();
    public Dictionary<string, int> NastepnyNr = new();
    /// <summary>Wymagane sprawdzenia cykliczne posterunków stanowiska (konfiguracja i stan).</summary>
    public Dictionary<string, List<EdrSprawdzenie>> SprawdzeniaUst = new();
    public List<EdrSprawdzenieStan> Sprawdzenia = new();
    /// <summary>Kontrolka zajętości torów stacyjnych każdego posterunku stanowiska (z wpisów w dziennikach).</summary>
    public Dictionary<string, List<EdrTorStacyjny>> ZajetoscTorow = new();
    public string Zegar, Data, Uzytkownik, ZalogowanyOd;
    /// <summary>Stanowisko zalogowanego (do „Wyciąg sporządził … Stanowisko …”).</summary>
    public string Rola;
    /// <summary>Konfiguracja posterunków i dzienników (administrator): bieżące definicje, czy z układu, szlaki układu, czy zalogowany może zmieniać.</summary>
    public List<EdrPosterunekUst> PosterunkiUst = new();
    public bool ZUkladu, Admin;
    public List<EdrSzlakUkladu> SzlakiUkladu = new();
    public EdrBezWypadku BezWypadku = new();
    public string BezWypadkuCzas = "", BladKonfiguracji;
    public int Doba;
    /// <summary>Czas symulacji [s] (zakres wykazu -/+ godz.).</summary>
    public double T;
    public long Wersja;
}

/// <summary>Polecenie okna EDR: nowy wiersz („przyj” / „wypr” z danymi) albo zmiana pola wiersza („pole”).</summary>
public class EdrPolecenie { public string Dziennik, Rodzaj, Pole, Wartosc, Klucz, Post; public List<EdrPosterunekUst> Posterunki; public int Wiersz; public EdrWersja Dane; public EdrUstawienia Ustawienia; public EdrTelefonogram Tel; public List<EdrSprawdzenie> Sprawdzenia; }

/// <summary>
/// EDR – elektroniczny dziennik ruchu (PLAN_EDR.md, Zrodla/EDR_notatki.md). System PLK obok komputera zależności:
/// niczego nie nastawia, tylko zapisuje to, co wpisze dyżurny. Jeden dziennik na szlak przyległy do posterunku,
/// strona = doba, wpisy z historią wersji. Wpisy automatyczne: objęcie dyżuru i zmiana doby.
/// </summary>
public partial class Edr
{
    readonly Simulator S;
    public readonly List<EdrDziennik> Dzienniki = new();
    public readonly Dictionary<string, List<string>> ToryStacyjne = new();
    /// <summary>Dzień, w którym zaczęła się symulacja (data strony doby 0).</summary>
    public DateTime Dzien0 = DateTime.Today;
    /// <summary>Licznik zmian – okno odświeża się, gdy się zmieni.</summary>
    public long Wersja;
    int nextId = 1;
    readonly Dictionary<string, string> zalogowany = new();
    /// <summary>Odcinek szlakowy → szlak; wykonanie rozkładu (pociąg|posterunek) i ostatnie położenie czoła pociągów.</summary>
    readonly Dictionary<string, string> blokOdcinka = new();
    public readonly Dictionary<string, EdrRuch> Ruch = new();
    /// <summary>Plik ustawień zasadniczych dzienników obok pliku układu (null – bez zapisu).</summary>
    public string PlikUstawien;
    /// <summary>Dziennik telefoniczny posterunków (wszystkie doby) – telefonogramy i inne okoliczności zapisane w nim (bez telefonogramów z dzienników ruchu).</summary>
    public readonly Dictionary<string, List<EdrTelefonogram>> Telefoniczny = new();
    /// <summary>Numeracja telefonogramów posterunku: „dobowa” (domyślnie), „miesieczna” albo „roczna” – plik ustawień.</summary>
    public readonly Dictionary<string, string> Numeracja = new();
    public static readonly string[] Numeracje = { "dobowa", "miesieczna", "roczna" };
    /// <summary>Wymagane sprawdzenia cykliczne posterunków (plik ustawień) i ostatnie wykonania (posterunek|id → czas symulacji, kto).</summary>
    public readonly Dictionary<string, List<EdrSprawdzenie>> Sprawdzenia = new();
    readonly Dictionary<string, (double T, string Kto)> wykonane = new();
    public static readonly string[] Podstawy = { "Samoczynna blokada liniowa", "Jednoodstępowa (półsamoczynna)", "Półsamoczynna blokada liniowa", "Telefoniczne zapowiadanie pociągów" };
    readonly Dictionary<string, (string Post, string Blok)> slad = new();

    public static readonly string[] Godzinowe = { "Dw", "Odj", "Prz" };
    public static readonly string[] Pola = { "Nr", "Tor", "Dw", "Odj", "Prz", "Uwagi" };

    public Edr(Simulator s)
    {
        S = s;
        PlikUstawien = S.L.SourcePath == null ? null : Path.Combine(Path.GetDirectoryName(S.L.SourcePath) ?? "", Path.GetFileNameWithoutExtension(S.L.SourcePath) + ".edr.json");
        WczytajUstawienia();
    }


    /// <summary>Tory w kolejności numerów (2 przed 10, 4a po 4).</summary>
    public static int PorownajTory(string a, string b)
    {
        static (int, string) K(string s) { int i = 0; while (i < s.Length && char.IsDigit(s[i])) i++; return (i > 0 && int.TryParse(s[..i], out var n) ? n : int.MaxValue, s[i..]); }
        var (na, ra) = K(a); var (nb, rb) = K(b);
        return na != nb ? na.CompareTo(nb) : string.CompareOrdinal(ra, rb);
    }

    // ------------------------------------------------------------------ czas
    public int Doba => (int)Math.Floor((S.StartClock + S.T) / 86400);
    public string DataDoby(int doba) => Dzien0.AddDays(doba).ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
    /// <summary>Bieżąca godzina i minuta (domyślna wartość pól godzinowych).</summary>
    public string Teraz() => S.Clock()[..5];

    public static string Wartosc(EdrWersja w, string pole) => w == null ? "" : pole.StartsWith("Dr:") ? w.Dr?.GetValueOrDefault(pole[3..]) ?? "" : pole switch
    {
        "Nr" => w.Nr, "Tor" => w.Tor, "Dw" => w.Dw, "Odj" => w.Odj, "Prz" => w.Prz, "Uwagi" => w.Uwagi, _ => ""
    } ?? "";

    static void Ustaw(EdrWersja w, string pole, string v)
    {
        if (pole.StartsWith("Dr:")) { w.Dr ??= new(); if (v == "") w.Dr.Remove(pole[3..]); else w.Dr[pole[3..]] = v; return; }
        switch (pole) { case "Nr": w.Nr = v; break; case "Tor": w.Tor = v; break; case "Dw": w.Dw = v; break; case "Odj": w.Odj = v; break; case "Prz": w.Prz = v; break; case "Uwagi": w.Uwagi = v; break; }
    }

    /// <summary>
    /// Godzina „H:mm” / „HH:mm” / „HHmm”, „---” (nie dotyczy) albo pusta; null – błędna. Z drugą godziną (pole wyboru w edytorze komórki,
    /// film ISDR): „08:00 / 08:12” – obie części sprawdzane.
    /// </summary>
    public static string Godzina(string v)
    {
        v = (v ?? "").Trim();
        if (v.Contains('/'))
        {
            var cz = v.Split('/');
            if (cz.Length != 2) return null;
            var (a, b) = (Godzina(cz[0]), Godzina(cz[1]));
            if (a is null or "" or "---" || b is null or "---") return null;
            return b == "" ? a : $"{a} / {b}";
        }
        if (v == "" || v == "---" || v == "-") return v == "" ? "" : "---";
        if (v.Length == 4 && v.All(char.IsDigit)) v = v[..2] + ":" + v[2..];
        var p = v.Split(':');
        if (p.Length == 2 && int.TryParse(p[0], out var h) && int.TryParse(p[1], out var m) && h is >= 0 and < 24 && m is >= 0 and < 60) return $"{h:00}:{m:00}";
        return null;
    }

    /// <summary>
    /// Pola wymagane w wierszu pociągu: numer, tor stacyjny, pociąg odjechał i przyjechał; przy półsamoczynnej blokadzie liniowej
    /// i telefonicznym zapowiadaniu dodatkowo droga wolna (podręcznik EDR, Rys. 37 i 40; do potwierdzenia przez właściciela).
    /// </summary>
    public static List<string> WymaganeDla(string typ)
    {
        var l = new List<string> { "Nr", "Tor", "Odj", "Prz" };
        if (typ is "pol" or "pol1" or "tel") l.Insert(2, "Dw");
        return l;
    }

    /// <summary>Pola wymagane wg opcji wypełniania z ustawień dziennika (numer pociągu – zawsze).</summary>
    public static List<string> Wymagane(EdrUstawienia u)
    {
        var l = new List<string> { "Nr" };
        if (u.WymTor) l.Add("Tor");
        // „droga wolna” także przy bieżącym telefonicznym zapowiadaniu pociągów (podręcznik EDR rozdz. 7.2: wybór pól zależy od bieżącej podstawy prowadzenia ruchu)
        if (u.WymDw || u.Tory.Any(t => t.Podstawa == Podstawy[3])) l.Add("Dw");
        if (u.WymOdj) l.Add("Odj");
        if (u.WymPrz) l.Add("Prz");
        return l;
    }

    // ------------------------------------------------------------------ ustawienia zasadnicze (plik <układ>.edr.json)
    static readonly System.Text.Json.JsonSerializerOptions JsonOpt = new() { IncludeFields = true, WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    class PlikEdr { public Dictionary<string, EdrUstawienia> Dzienniki = new(); public Dictionary<string, string> Numeracja = new(); public Dictionary<string, List<EdrSprawdzenie>> Sprawdzenia = new(); public List<EdrPosterunekUst> Posterunki; public EdrBezWypadku BezWypadku; }

    /// <summary>Wczytuje plik ustawień (konfiguracja posterunków, ustawienia zasadnicze dzienników …) i buduje dzienniki – ustawienia z pliku mają pierwszeństwo.</summary>
    public void WczytajUstawienia()
    {
        Wczytaj();
        ZbudujDzienniki(zPliku: true);
    }

    void Wczytaj()
    {
        if (PlikUstawien == null || !File.Exists(PlikUstawien)) return;
        try
        {
            var f = System.Text.Json.JsonSerializer.Deserialize<PlikEdr>(File.ReadAllText(PlikUstawien), JsonOpt);
            // posterunki i dzienniki z konfiguracji administratora (błędna – z układu)
            if (f?.Posterunki != null)
            {
                var l = f.Posterunki.Select(x => x.Kopia()).ToList();
                if (SprawdzPosterunki(l) is string blad) { BladKonfiguracji = $"konfiguracja posterunków z pliku pominięta: {blad}"; S.Log("Zdarzenie", "EDR: " + BladKonfiguracji, "System", ""); }
                else posterunkiUst = l;
            }
            if (f?.BezWypadku != null) BezWypadku = f.BezWypadku;
            zapisaneUst = f?.Dzienniki ?? new();
            foreach (var (pk, n) in f?.Numeracja ?? new()) if (JestPost(pk) && Numeracje.Contains(n)) Numeracja[pk] = n;
            foreach (var (pk, l) in f?.Sprawdzenia ?? new()) if (JestPost(pk) && SprawdzSprawdzenia(l) == null) Sprawdzenia[pk] = l.Select(x => x.Kopia()).ToList();
        }
        catch (Exception ex) { S.Log("Zdarzenie", $"EDR: nie wczytano ustawień dzienników ({Path.GetFileName(PlikUstawien)}): {ex.Message}", "System", ""); }
    }

    void ZapiszUstawienia()
    {
        if (PlikUstawien == null) return;
        var f = new PlikEdr(); foreach (var d in Dzienniki) f.Dzienniki[d.Id] = d.Ustawienia;
        foreach (var (pk, n) in Numeracja) f.Numeracja[pk] = n;
        foreach (var (pk, l) in Sprawdzenia) f.Sprawdzenia[pk] = l;
        f.Posterunki = posterunkiUst; f.BezWypadku = BezWypadku;
        File.WriteAllText(PlikUstawien, System.Text.Json.JsonSerializer.Serialize(f, JsonOpt));
    }

    /// <summary>Błąd ustawień (null – poprawne): te same tory szlakowe co w układzie, kierunki −1 / 0 / 1, strony toru bez powtórzeń.</summary>
    static string Sprawdz(EdrDziennik d, EdrUstawienia u)
    {
        if (u?.Tory == null || u.Tory.Count != d.Ustawienia.Tory.Count || u.Tory.Any(t => !d.Ustawienia.Tory.Any(x => x.Szlak == t.Szlak))) return "Tory szlakowe nie zgadzają się z układem";
        if (u.Parzysty is < -1 or > 1 || u.Tory.Any(t => t.Zasadniczy is < -1 or > 1)) return "Błędny kierunek";
        if (u.Tory.Where(t => t.Strona is "lewy" or "prawy").GroupBy(t => t.Strona).Any(g => g.Count() > 1)) return "Tor lewy i prawy muszą być różne";
        if (u.Tory.Any(t => t.Rodzaj == "Jednokierunkowy" && t.Zasadniczy == 0)) return "Tor jednokierunkowy musi mieć zasadniczy kierunek ruchu";
        u.Droznicy ??= new();
        if (u.Droznicy.Any(x => string.IsNullOrWhiteSpace(x.Oznaczenie))) return "Posterunek dróżnika musi mieć oznaczenie w dzienniku ruchu";
        if (u.Droznicy.GroupBy(x => x.Oznaczenie.Trim()).Any(g => g.Count() > 1) || u.Droznicy.GroupBy(x => x.Id).Any(g => g.Count() > 1)) return "Oznaczenia posterunków dróżników muszą być różne";
        if (u.Droznicy.Any(x => x.WgSzlaku is not ("Jednotorowych" or "Dwu i więcej torowych") || x.DlaPociagow is not ("W obu kierunkach" or "Z posterunku sąsiedniego" or "DO posterunku sąsiedniego"))) return "Błędny sposób powiadamiania dróżnika";
        return null;
    }

    /// <summary>Przyjmuje ustawienia (bez pól tylko do odczytu z układu: numer toru, urządzenie srk).</summary>
    static void Przyjmij(EdrDziennik d, EdrUstawienia u)
    {
        var n = u.Kopia();
        foreach (var t in n.Tory) { var z = d.Ustawienia.Tory.First(x => x.Szlak == t.Szlak); t.Tor = z.Tor; t.Urzadzenie = z.Urzadzenie; t.Status ??= "Otwarty"; t.Strona ??= ""; }
        d.Ustawienia = n; d.Parzysty = n.Parzysty;
        // bieżące: nowe zasadnicze, a na nie – zmiany wprowadzone telefonogramami
        var b = n.Kopia();
        foreach (var t in d.Strony.SelectMany(s => s.Wiersze).Where(w => w.Tel != null).OrderBy(w => w.Id)) Zastosuj(b, t.Tel.Zmiany);
        d.Biezace = b;
    }

    static void Zastosuj(EdrUstawienia u, List<EdrZmianaToru> zmiany)
    {
        foreach (var z in zmiany ?? new())
        {
            var t = u.Tory.FirstOrDefault(x => x.Szlak == z.Szlak);
            if (t == null) continue;
            if (!string.IsNullOrEmpty(z.Status)) t.Status = z.Status;
            if (!string.IsNullOrEmpty(z.Rodzaj)) t.Rodzaj = z.Rodzaj;
            if (!string.IsNullOrEmpty(z.Podstawa)) t.Podstawa = z.Podstawa;
            if (z.Zasadniczy is int k) t.Zasadniczy = k;
        }
    }

    /// <summary>Kolumna numeru pociągu: 1 – nieparzysty, 2 – parzysty (wg ostatniej cyfry).</summary>
    public static int KolumnaNumeru(string nr)
    {
        var c = (nr ?? "").Trim().LastOrDefault(char.IsDigit);
        return c == default ? 1 : (c - '0') % 2 == 1 ? 1 : 2;
    }

    // ------------------------------------------------------------------ stanowiska
    /// <summary>Dziennik należy do stanowiska, które steruje posterunkiem (bez stanowisk – wszystkie).</summary>
    bool Moj(EdrDziennik d, Desk k) => MojPost(d.Post, k);
    bool MojPost(string post, Desk k) => k == null || !S.Control.TryGetValue(post, out var o) || o == null || o == k.Id;

    EdrStrona Strona(EdrDziennik d)
    {
        int doba = Doba;
        var s = d.Strony.LastOrDefault();
        if (s != null && s.Doba == doba) return s;
        s = new EdrStrona { Doba = doba, Data = DataDoby(doba) };
        d.Strony.Add(s);
        Tekst(d, s, $"Północ dnia {s.Data}");
        Wersja++;
        return s;
    }

    void Tekst(EdrDziennik d, EdrStrona s, string t)
    {
        s.Wiersze.Add(new EdrWiersz { Id = nextId++, Rodzaj = "tekst", Tekst = t, Wersje = { new EdrWersja { Kto = d.Prowadzi, T = S.T, Zmiana = Znacznik() } } });
        Wersja++;
    }

    string Znacznik() => $"{DataDoby(Doba)} {S.Clock()}";

    /// <summary>Wpisy automatyczne: nowa strona przy zmianie doby, „Północ dnia …”, „Dyżur przyjął …” w dziennikach stanowiska po zalogowaniu.</summary>
    public void Aktualizuj(Desk k)
    {
        var user = k?.User?.Name ?? S.User?.Name;
        if (k != null && user != null && !zalogowany.ContainsKey(k.Id)) zalogowany[k.Id] = Znacznik();
        foreach (var d in Dzienniki.Where(x => Moj(x, k)))
        {
            var s = Strona(d);
            if (user != null && d.Prowadzi != user)
            {
                d.Prowadzi = user;
                Tekst(d, s, $"Dyżur przyjął {user} dnia {s.Data} o godzinie {Teraz()}");
            }
        }
    }

    /// <summary>Wywoływane w cyklu symulacji: objęcie dyżuru i zmiana doby także przy zamkniętym oknie EDR.</summary>
    public void Tick()
    {
        if (S.Trains != null)
            foreach (var t in S.Trains.List)
            {
                if (t.Segs.Count == 0 || t.Shunt && t.Duty == null) continue;
                var h = t.Segs[^1];
                string sec = h.E != null ? S.L.Edges[h.E].Sec : null, blok = h.E == null ? h.Szlak : sec != null ? blokOdcinka.GetValueOrDefault(sec) : null;
                string post = blok == null && sec != null ? Station.PostOf(sec) : null;
                Zarejestruj(t.Id, t.Num, post, blok, post != null && h.E != null ? S.L.Edges[h.E].Track : null);
            }
        if (S.Desks.Count == 0) { Aktualizuj(null); return; }
        foreach (var k in S.Desks.Values) if (k.User != null) Aktualizuj(k);
    }

    /// <summary>
    /// Położenie czoła pociągu: na stacji posterunku <paramref name="post"/> albo na szlaku <paramref name="blok"/>. Wjazd na stację – przyjazd
    /// (z szlaku, z którego przyjechał), zjazd ze stacji – odjazd (na szlak, na który wjechał). Pociąg pojawiający się na stacji – bez przyjazdu.
    /// </summary>
    public void Zarejestruj(string id, string num, string post, string blok, string tor)
    {
        if (post == null && blok == null) return;
        bool byl = slad.TryGetValue(id, out var p);
        // tor stacyjny: ostatni tor z numerem pod czołem w czasie pobytu na stacji (głowice nie mają numeru toru)
        if (post != null && !string.IsNullOrEmpty(tor) && Ruch.TryGetValue(id + "|" + post, out var rt) && rt.Odj == null && rt.Tor != tor) { rt.Tor = tor; Wersja++; }
        if (byl && p.Post == post && p.Blok == blok) return;
        slad[id] = (post, blok);
        if (byl && p.Post != null && p.Post != post && Ruch.TryGetValue(id + "|" + p.Post, out var r0) && r0.Odj == null)
        {
            r0.Odj = S.T; r0.NrWyj = num; r0.WSzlak = blok; Wersja++;
        }
        if (post != null && (!byl || p.Post != post) && !Ruch.ContainsKey(id + "|" + post))
        {
            Ruch[id + "|" + post] = new EdrRuch { Nr = num, NrWyj = num, Prz = byl ? S.T : null, ZSzlak = byl && p.Post == null ? p.Blok : null, Tor = string.IsNullOrEmpty(tor) ? null : tor };
            Wersja++;
        }
    }

    // ------------------------------------------------------------------ wykaz pociągów
    /// <summary>Numery pociągu w biegu (zmiany numeru na posterunkach, jednostka z zaplecza).</summary>
    static List<string> Numery(TimetableEntry q)
    {
        var l = new List<string> { q.Duty?.Num ?? q.Num };
        if (q.Duty?.Num != null) l.Add(q.Num);
        for (var t = q.Duty?.Term ?? q.Term; t != null; t = t.Then) if (t.NewNum != null && !l.Contains(t.NewNum)) l.Add(t.NewNum);
        return l;
    }

    /// <summary>
    /// Kolejność posterunków LCS w biegu pociągu: z planu GTFS (wg godzin przyjazdu), a w innych rozkładach – posterunek szlaku przyjazdu
    /// (albo toru, na którym zaczyna bieg), posterunki zmian czoła i posterunek szlaku odjazdu.
    /// </summary>
    List<string> Sekwencja(TimetableEntry q)
    {
        var l = new List<string>();
        void Dodaj(string p) { if (!string.IsNullOrEmpty(p) && (l.Count == 0 || l[^1] != p)) l.Add(p); }
        string PostSzlaku(string b) => b != null && S.L.Blocks.TryGetValue(b, out var bd) && bd.Ends.Count == 1 ? bd.Ends[0].Post : null;
        if (q.PlanPrz?.Count > 0) { foreach (var p in q.PlanPrz.OrderBy(x => x.Value).Select(x => x.Key)) Dodaj(p); return l; }
        Dodaj(PostSzlaku(q.From) ?? (q.StartSec != null ? Station.PostOf(q.StartSec) : null));
        for (var t = q.Duty?.Term ?? q.Term; t != null; t = t.Then) { Dodaj(t.AtPost); if (t.To != null) Dodaj(PostSzlaku(t.To)); }
        Dodaj(PostSzlaku(q.Duty?.To ?? q.To));
        return l;
    }

    /// <summary>Szlak łączący dwa posterunki LCS (szlak wewnętrzny z końcami na obu), który ma dziennik na posterunku <paramref name="a"/>.</summary>
    string Laczacy(string a, string b) => a == b ? null : S.L.Blocks.Values.Where(x => x.Ends.Any(e => e.Post == a) && x.Ends.Any(e => e.Post == b))
        .Select(x => x.Id).FirstOrDefault(id => Dzienniki.Any(d => d.Post == a && d.Szlaki.Contains(id)));

    /// <summary>Ten sam pociąg mimo zmiany parzystości numeru (ostatnia cyfra 0/1, 2/3 …).</summary>
    public static bool TenSam(string a, string b)
    {
        a = (a ?? "").Trim(); b = (b ?? "").Trim();
        if (a == b) return a != "";
        return a.Length == b.Length && a.Length > 1 && a[..^1] == b[..^1] && char.IsDigit(a[^1]) && char.IsDigit(b[^1]) && (a[^1] - '0') / 2 == (b[^1] - '0') / 2;
    }

    /// <summary>Ostatnia cyfra numeru dostosowana do parzystości kierunku (nieparzysty → o 1 mniejszy, parzysty → o 1 większy).</summary>
    public static string Wymus(string nr, bool parzysty)
    {
        nr = (nr ?? "").Trim();
        if (nr == "" || !char.IsDigit(nr[^1])) return nr;
        int c = nr[^1] - '0';
        if ((c % 2 == 0) == parzysty) return nr;
        return nr[..^1] + (char)('0' + (parzysty ? c - 1 : c + 1));
    }

    string Hm(double? t) => t is double v ? S.Clock(v)[..5] : "";
    string HmD(double? t) => t is double v ? Dzien0.AddDays(Math.Floor((S.StartClock + v) / 86400)).ToString("dd-MM", CultureInfo.InvariantCulture) + " " + Hm(v) : "";

    /// <summary>Trasa pociągu wg rozkładu: posterunki LCS w kolejności przyjazdu.</summary>
    List<EdrTrasa> TrasaPociagu(TimetableEntry q, string nr)
    {
        // bieg z rozkładu rzeczywistego – tylko odcinek z tym numerem (obieg jednostki: przyjazd jednym numerem, odjazd innym)
        var pb = q.Przebieg?.Where(x => TenSam(x.Num, nr)).ToList();
        if (pb?.Count > 0)
        {
            var l = new List<EdrTrasa>();
            foreach (var x in pb)
            {
                // ten sam posterunek dwa razy z rzędu (przyjazd i odjazd zapisane osobno) – jeden wiersz
                if (l.Count > 0 && l[^1].Posterunek == NazwaPosterunku(x.Post)) { l[^1].Odjazd = HmD(x.Dep); continue; }
                l.Add(new EdrTrasa { Posterunek = NazwaPosterunku(x.Post), Przyjazd = HmD(x.Arr), Odjazd = HmD(x.Dep), Tor = x.Track ?? "", Postoj = x.Dep - x.Arr >= 30 ? "ph" : "" });
            }
            // numer zaczyna się albo kończy w obszarze LCS (obieg jednostki): bez przyjazdu na pierwszym i bez odjazdu na ostatnim posterunku
            if (q.Przebieg.IndexOf(pb[0]) > 0) l[0].Przyjazd = "";
            if (q.Przebieg.IndexOf(pb[^1]) < q.Przebieg.Count - 1) l[^1].Odjazd = "";
            return l;
        }
        return TrasaPlanu(q);
    }

    List<EdrTrasa> TrasaPlanu(TimetableEntry q) => (q.PlanPrz ?? new()).OrderBy(x => x.Value).Select(x =>
    {
        var odj = q.PlanOdj?.GetValueOrDefault(x.Key);
        return new EdrTrasa { Posterunek = NazwaPosterunku(x.Key), Przyjazd = HmD(x.Value), Odjazd = HmD(odj), Tor = q.PlanTor?.GetValueOrDefault(x.Key) ?? "", Postoj = odj is double o && o - x.Value >= 30 ? "ph" : "" };
    }).ToList();
    static string Minuty(double? od, double? @do) => od is double a && @do is double b ? ((b - a) / 60).ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("pl-PL")) : "";

    static string Odch(double? plan, double? rzecz)
    {
        if (plan is not double p || rzecz is not double r) return "";
        int m = (int)Math.Round((r - p) / 60);
        return m > 0 ? "+" + m : m.ToString();
    }

    string SasiadSzlaku(string post, string blok) => blok == null ? null : Dzienniki.FirstOrDefault(d => d.Post == post && d.Szlaki.Contains(blok))?.Sasiad
        ?? (S.L.Blocks.TryGetValue(blok, out var b) ? b.Nbr : blok);

    /// <summary>Pociągi przejeżdżające przez posterunek: z rozkładu (plan) i z ruchu (wykonanie), wpisy w dziennikach.</summary>
    public List<EdrPociag> Wykaz(string post)
    {
        var res = new List<EdrPociag>();
        if (S.Trains == null) return res;
        bool Konczy(string b) => b != null && S.L.Blocks.TryGetValue(b, out var bd) && bd.Ends.Any(e => e.Post == post);
        var wiersze = Dzienniki.Where(d => d.Post == post).SelectMany(d => d.Strony.SelectMany(s => s.Wiersze)).Where(w => w.Rodzaj is "przyj" or "wypr").ToList();
        foreach (var q in S.Rozklad.Queue)
        {
            if (q.Loco && q.Duty == null) continue;
            var nums = Numery(q);
            var term = new List<TrainTerm>(); for (var t = q.Duty?.Term ?? q.Term; t != null; t = t.Then) term.Add(t);
            string z = Konczy(q.From) ? q.From : null;
            // szlak odjazdu z planu – tylko gdy pociąg nie zmienia czoła wcześniej na innym posterunku (wtedy wiadomo dopiero z ruchu)
            string w = term.Count == 0 || term[0].AtPost == post ? new[] { q.Duty?.To ?? q.To }.Concat(term.Select(t => t.To)).FirstOrDefault(Konczy) : null;
            bool start = q.StartSec != null && Station.PostOf(q.StartSec) == post;
            bool plan = q.PlanPrz?.ContainsKey(post) == true || q.PlanOdj?.ContainsKey(post) == true || q.DepBy?.ContainsKey(post) == true || q.StopAt?.Contains(post) == true
                || term.Any(t => t.AtPost == post);
            var rec = Ruch.Where(x => x.Key.EndsWith("|" + post) && (nums.Contains(x.Value.Nr) || nums.Contains(x.Value.NrWyj))).Select(x => x.Value).FirstOrDefault();
            if (z == null && w == null && !start && !plan && rec == null) continue;
            z ??= rec?.ZSzlak; w ??= rec?.WSzlak;
            // szlak między posterunkami LCS (np. pociąg z Widzewa do Fabrycznej): z kolejności posterunków w biegu pociągu
            var seq = Sekwencja(q);
            int ip = seq.IndexOf(post);
            if (ip >= 0)
            {
                // najbliższy posterunek w biegu, do którego prowadzi szlak (posterunki bez szlaków, np. przystanek w tunelu – pomijane)
                for (int j = ip - 1; z == null && j >= 0; j--) z = Laczacy(seq[j], post);
                for (int j = ip + 1; w == null && j < seq.Count; j++) w = Laczacy(post, seq[j]);
                // zmiana czoła na posterunku: pociąg wraca – szlak odjazdu w stronę, z której przyjechał
                if (w == null && term.Any(t => t.AtPost == post && t.Reverse)) for (int j = ip - 1; w == null && j >= 0; j--) w = Laczacy(post, seq[j]);
            }
            double? przPlan = q.PlanPrz?.GetValueOrDefault(post) is double pp ? pp : null;
            double? odjPlan = q.PlanOdj?.TryGetValue(post, out var po) == true ? po : q.DepBy?.TryGetValue(post, out var db) == true ? db
                : q.Duty?.DepBy?.TryGetValue(post, out var dd) == true ? dd : start ? q.Duty?.Dep ?? q.Dep : z != null && q.Stop && q.StopAt == null && q.Term == null ? q.Dep : null;
            string nr = nums[0], nrWyj = rec?.NrWyj ?? term.LastOrDefault(t => t.AtPost == post)?.NewNum ?? nr;
            // tor z rozkładu: GTFS – na każdym posterunku; rozkład wbudowany / plik układu – krótki zapis „tor” pociągu wjeżdżającego z szlaku posterunku
            var tor = q.PlanTor?.GetValueOrDefault(post) ?? (start ? q.Duty?.Track ?? Station.Lbl(q.StartSec) : null)
                ?? (q.PlanTor == null && z != null && q.Track is string qt && qt.Length is > 0 and <= 4 && !qt.Contains(' ') ? qt : null) ?? "";
            bool ph = q.StopAt?.Contains(post) == true || q.Stop && q.StopAt == null || term.Any(t => t.AtPost == post) || przPlan != null && odjPlan != null && odjPlan - przPlan >= 30;
            bool wDrodze = q.Done || S.Trains.List.Any(t => nums.Contains(t.Num));
            string stan = rec == null ? (wDrodze ? "w drodze" : "planowy") : rec.Odj != null ? "odjechał" : "na stacji";
            // wstęga: pociąg zbliża się z szlaku posterunku (przyjazd / przejazd) albo stoi na stacji (odjazd / przejazd)
            string wstega = rec == null ? (wDrodze && z != null ? (ph ? "przyjazd" : "przejazd") : "") : rec.Odj == null ? (ph ? "odjazd" : "przejazd") : "";
            var klucz = $"{q.Num}@{q.T:0}";
            // czas rzeczywisty: z ruchu; bez niego – planowy, a odjazd przesunięty o opóźnienie przyjazdu (bez opóźnienia – czas planowy, uwaga właściciela 2026-10-28)
            double opPrz = rec?.Prz is double rp && przPlan is double pp2 ? Math.Max(0, rp - pp2) : 0;
            double? przR = rec?.Prz ?? (rec == null ? przPlan : null), odjR = rec?.Odj ?? (odjPlan is double op ? op + opPrz : null);
            // potwierdzenie czasu: wpis dyżurnego w dzienniku (godzina odjazdu albo przyjazdu), dyspozytor, urządzenie
            var wpisane = wiersze.Any(x => nums.Concat(new[] { nrWyj }).Any(n => TenSam(n, x.W.Nr)) && (Jest(x.W.Odj) || Jest(x.W.Prz)));
            string potw = wpisane ? "czlowiek" : potwDysp.Contains(klucz) ? "dyspozytor" : rec?.Prz != null || rec?.Odj != null ? "auto" : "";
            var wp = new EdrPociag
            {
                Klucz = klucz, Nr = nr, NrWyj = nrWyj, Rodz = q.KatIr11 ?? "", Nazwa = q.Name ?? "",
                ZSzlak = z, ZKierunku = z != null ? SasiadSzlaku(post, z) : start ? "tor " + tor : "", WSzlak = w, WKierunku = w != null ? SasiadSzlaku(post, w) : "",
                Linia = new[] { z, w }.Where(x => x != null && S.L.Blocks.ContainsKey(x)).Select(x => S.L.Blocks[x].Line).FirstOrDefault(x => !string.IsNullOrEmpty(x)) ?? "",
                Tor = tor, TorRzecz = rec?.Tor ?? "", PrzPlan = Hm(przPlan), PrzRzecz = Hm(przR), PrzOdch = Odch(przPlan, przR),
                OdjPlan = Hm(odjPlan), OdjRzecz = Hm(odjR), OdjOdch = Odch(odjPlan, odjR), Postoj = ph ? "ph" : "", Stan = stan, NaStacji = stan == "na stacji",
                PZam = ph ? Minuty(przPlan, odjPlan) is var pz && pz != "" ? pz : "0,0" : "0,0", PObl = Minuty(rec?.Prz, rec?.Odj) is var pb && pb != "" ? pb : "0,0",
                Wstega = wstega, Potw = potw, Kursuje = kursuje.TryGetValue(klucz, out var ku) ? ku : !q.NieKursuje,
                StacjaPocz = q.Origin ?? "", StacjaKonc = q.Destination ?? "", Przewoznik = q.Przewoznik ?? "", Kod = q.Cat ?? "", Pojazd = q.Veh ?? "", Dlugosc = q.Len is double ln ? $"{ln:0} m" : "",
                UwagiEksp = uwagiEksp.GetValueOrDefault(klucz) ?? q.UwagiEksp ?? "", UwagiPrzew = uwagiPrzew.GetValueOrDefault(klucz) ?? q.UwagiPrzew ?? "", UwagiWlasne = uwagiWlasne.GetValueOrDefault(post + "|" + klucz) ?? "",
                PrzPlanD = HmD(przPlan), PrzRzeczD = HmD(przR), OdjPlanD = HmD(odjPlan), OdjRzeczD = HmD(odjR), Trasa = TrasaPociagu(q, nr),
                Przyjety = wiersze.Any(x => x.Rodzaj == "przyj" && nums.Concat(new[] { nrWyj }).Any(n => TenSam(n, x.W.Nr))),
                Wyprawiony = wiersze.Any(x => x.Rodzaj == "wypr" && nums.Concat(new[] { nrWyj }).Any(n => TenSam(n, x.W.Nr))),
                Sort = przPlan ?? odjPlan ?? rec?.Prz ?? q.T
            };
            // każdy pociąg ze swoim numerem ma osobny wiersz (uwaga właściciela 2026-10-28): nowy numer na posterunku (np. po zmianie czoła) –
            // wiersz pociągu przyjeżdżającego (bez odjazdu) i wiersz pociągu odjeżdżającego (bez przyjazdu); zmiana samej parzystości – jeden wiersz
            if (nrWyj != nr && !TenSam(nr, nrWyj))
            {
                var odj = (EdrPociag)wp.Kopia();
                wp.NrWyj = nr; wp.WSzlak = null; wp.WKierunku = ""; wp.OdjPlan = wp.OdjRzecz = wp.OdjOdch = wp.OdjPlanD = wp.OdjRzeczD = "";
                wp.Wstega = rec == null ? wstega : ""; wp.Stan = rec == null ? stan : "przyjechał"; wp.NaStacji = false; wp.Wyprawiony = false;
                odj.Klucz = klucz + "#" + nrWyj; odj.Nr = nrWyj; odj.ZSzlak = null; odj.ZKierunku = ""; odj.PrzPlan = odj.PrzRzecz = odj.PrzOdch = odj.PrzPlanD = odj.PrzRzeczD = "";
                odj.Wstega = rec == null ? "" : wstega; odj.Stan = rec == null ? "planowy" : stan; odj.Przyjety = false; odj.Sort = odjPlan ?? wp.Sort;
                odj.UwagiEksp = uwagiEksp.GetValueOrDefault(odj.Klucz) ?? ""; odj.UwagiPrzew = uwagiPrzew.GetValueOrDefault(odj.Klucz) ?? ""; odj.UwagiWlasne = uwagiWlasne.GetValueOrDefault(post + "|" + odj.Klucz) ?? "";
                odj.Kursuje = kursuje.TryGetValue(odj.Klucz, out var ko) ? ko : !q.NieKursuje;
                odj.Potw = wiersze.Any(x => TenSam(nrWyj, x.W.Nr) && (Jest(x.W.Odj) || Jest(x.W.Prz))) ? "czlowiek" : potwDysp.Contains(odj.Klucz) ? "dyspozytor" : rec?.Odj != null ? "auto" : "";
                wp.Potw = wiersze.Any(x => TenSam(nr, x.W.Nr) && (Jest(x.W.Odj) || Jest(x.W.Prz))) ? "czlowiek" : potwDysp.Contains(klucz) ? "dyspozytor" : rec?.Prz != null ? "auto" : "";
                // relacja i nazwa osobno: pociąg przyjeżdżający kończy bieg na tym posterunku, odjeżdżający tu zaczyna (rozkład skleja obieg jednostki)
                var naz = (q.Name ?? "").Split(" / ");
                wp.StacjaKonc = NazwaPosterunku(post); odj.StacjaPocz = NazwaPosterunku(post);
                if (naz.Length == 2) { wp.Nazwa = naz[0]; odj.Nazwa = naz[1]; }
                odj.Trasa = TrasaPociagu(q, nrWyj);
                res.Add(odj);
            }
            res.Add(wp);
        }
        return res.OrderBy(x => x.Sort).ToList();
    }

    static bool Jest(string godz) => !string.IsNullOrEmpty(godz) && godz != "---";

    /// <summary>
    /// Stan torów szlakowych dziennika z wpisów: pociąg z wypełnionym „odjechał” i pustym „przyjechał” zajmuje tor – przy przyjęciu tor
    /// z zasadniczym kierunkiem do nas, przy wyprawieniu – od nas, „po torze lewym” – drugi; tor dwukierunkowy – w obu kierunkach.
    /// </summary>
    public void StanSzlaku(EdrDziennik d)
    {
        var ts = d.Biezace.Tory.OrderBy(t => t.Strona == "lewy" ? 0 : t.Strona == "prawy" ? 2 : 1).ThenBy(t => t.Tor, Comparer<string>.Create(PorownajTory));
        d.ToryStan = ts.Select(t => new EdrTorSzlakowy { Szlak = t.Szlak, Tor = t.Tor, Kierunek = t.Zasadniczy, Strona = t.Strona, Status = t.Status }).ToList();
        if (d.ToryStan.Count == 0) return;
        foreach (var w in d.Strony.SelectMany(s => s.Wiersze).Where(w => w.Rodzaj is "przyj" or "wypr"))
        {
            var v = w.W;
            // jazda do km i z powrotem: tor zajęty od odjazdu do wpisu przyjazdu w wierszu powrotu
            var powrot = w.Pierwszy ? d.Strony.SelectMany(s => s.Wiersze).FirstOrDefault(x => x.Id == w.Para) : null;
            if (!Jest(v.Odj) || (powrot != null ? Jest(powrot.W.Prz) || powrot.W.Prz == "---" : Jest(v.Prz) || v.Prz == "---")) continue;
            int kier = w.Rodzaj == "przyj" ? -1 : 1, szuk = v.Lewy ? -kier : kier;
            // tor o zasadniczym kierunku jazdy pociągu; bez ustalonego kierunku – przyjęcie po torze lewym, wyprawienie po prawym (ruch prawostronny)
            var t = d.ToryStan.FirstOrDefault(x => x.Kierunek == szuk) ?? d.ToryStan.FirstOrDefault(x => x.Kierunek == 0 && x.Strona == (szuk < 0 ? "lewy" : "prawy"))
                ?? d.ToryStan.FirstOrDefault(x => x.Kierunek == 0) ?? d.ToryStan[0];
            t.Pociagi.Add(v.Nr); t.Ruch = kier;
        }
    }

    /// <summary>Zajętość torów stacyjnych posterunku z wpisów wszystkich jego dzienników, w kolejności wpisów.</summary>
    public List<EdrTorStacyjny> ZajetoscTorowStacyjnych(string post)
    {
        var tory = (ToryStacyjne.GetValueOrDefault(post) ?? new()).Select(t => new EdrTorStacyjny { Tor = t }).ToList();
        var wiersze = Dzienniki.Where(d => d.Post == post).SelectMany(d => d.Strony.SelectMany(s => s.Wiersze)).Where(w => w.Rodzaj is "przyj" or "wypr" or "powrot").OrderBy(w => w.Id);
        foreach (var w in wiersze)
        {
            var v = w.W;
            if (w.Rodzaj is "przyj" or "powrot" && Jest(v.Prz) && v.Tor != "")
            {
                var t = tory.FirstOrDefault(x => x.Tor == v.Tor);
                if (t == null) tory.Add(t = new EdrTorStacyjny { Tor = v.Tor });
                t.Pociag = v.Nr; t.Od = v.Prz;
            }
            else if (w.Rodzaj == "wypr" && Jest(v.Odj))
                foreach (var t in tory.Where(x => x.Pociag != null && (TenSam(x.Pociag, v.Nr) || v.Tor != "" && x.Tor == v.Tor))) { t.Pociag = null; t.Od = null; }
        }
        return tory.OrderBy(x => x.Tor, Comparer<string>.Create(PorownajTory)).ToList();
    }

    public EdrData DlaStanowiska(Desk k)
    {
        Aktualizuj(k);
        foreach (var d in Dzienniki.Where(x => Moj(x, k))) StanSzlaku(d);
        var user = k?.User?.Name ?? S.User?.Name;
        return new EdrData
        {
            Dzienniki = Dzienniki.Where(x => Moj(x, k)).ToList(), ToryStacyjne = ToryStacyjne, Zegar = S.Clock(), Data = DataDoby(Doba), Doba = Doba,
            T = S.T, Uzytkownik = user, Rola = k?.User?.Role ?? S.User?.Role, ZalogowanyOd = k != null && zalogowany.TryGetValue(k.Id, out var z) ? z : null, Wersja = Wersja,
            Posterunki = Posterunki.Where(p => MojPost(p.Kod, k)).ToDictionary(p => p.Kod, p => NazwaPosterunku(p.Kod)),
            Wykaz = Posterunki.Select(p => p.Kod).Where(p => MojPost(p, k)).ToDictionary(p => p, Wykaz),
            ZajetoscTorow = Posterunki.Select(p => p.Kod).Where(p => MojPost(p, k)).ToDictionary(p => p, ZajetoscTorowStacyjnych),
            Telefoniczny = Posterunki.Select(p => p.Kod).Where(p => MojPost(p, k)).ToDictionary(p => p, p => Telefoniczny.GetValueOrDefault(p) ?? new()),
            Numeracja = Posterunki.Select(p => p.Kod).Where(p => MojPost(p, k)).ToDictionary(p => p, p => Numeracja.GetValueOrDefault(p, "dobowa")),
            NastepnyNr = Posterunki.Select(p => p.Kod).Where(p => MojPost(p, k)).ToDictionary(p => p, NastepnyNumer),
            SprawdzeniaUst = Posterunki.Select(p => p.Kod).Where(p => MojPost(p, k)).ToDictionary(p => p, p => Sprawdzenia.GetValueOrDefault(p) ?? new()),
            PosterunkiUst = Posterunki.Select(x => x.Kopia()).ToList(), ZUkladu = posterunkiUst == null, SzlakiUkladu = SzlakiUkladu(), Admin = Admin(k),
            BezWypadku = BezWypadku, BezWypadkuCzas = CzasBezWypadku(), BladKonfiguracji = BladKonfiguracji,
            Sprawdzenia = StanSprawdzen(k)
        };
    }

    // ------------------------------------------------------------------ polecenia okna
    public CmdResult Wykonaj(Desk k, EdrPolecenie p)
    {
        if (p?.Rodzaj == "telefonogram") return Telefonogram(k, p);
        if (p != null && Konfiguracja(k, p) is CmdResult kr) return kr;
        if (p?.Rodzaj is "sprawdzenia" or "sprawdzenie")
        {
            if (!JestPost(p.Wartosc)) return new(false, "Nie ma takiego posterunku");
            if (!MojPost(p.Wartosc, k)) return new(false, $"Posterunek {NazwaPosterunku(p.Wartosc)} prowadzi inne stanowisko");
            if (p.Rodzaj == "sprawdzenia")
            {
                if (SprawdzSprawdzenia(p.Sprawdzenia) is string blad) return new(false, blad);
                Sprawdzenia[p.Wartosc] = p.Sprawdzenia.Select(x => x.Kopia()).ToList(); Wersja++;
                try { ZapiszUstawienia(); } catch (Exception ex) { return new(false, $"Sprawdzenia przyjęte, ale nie zapisane w pliku: {ex.Message}"); }
                return new(true, $"Wymagane sprawdzenia posterunku {NazwaPosterunku(p.Wartosc)}: {p.Sprawdzenia.Count}");
            }
            var sp = Sprawdzenia.GetValueOrDefault(p.Wartosc)?.FirstOrDefault(x => x.Id == p.Pole);
            if (sp == null) return new(false, "Nie ma takiego sprawdzenia");
            var kto = k?.User?.Name ?? S.User?.Name;
            if (kto == null) return new(false, "Brak zalogowanego użytkownika");
            // wykonanie zapisuje się w dzienniku telefonicznym jako inne okoliczności
            var r = Telefonogram(k, new EdrPolecenie { Rodzaj = "telefonogram", Wartosc = p.Wartosc, Tel = new EdrTelefonogram { Rodzaj = "okolicznosc", Tresc = $"Sprawdzenie: {sp.Nazwa} – wykonane." + (string.IsNullOrWhiteSpace(p.Dane?.Uwagi) ? "" : " " + p.Dane.Uwagi.Trim()) } });
            if (!r.Ok) return r;
            wykonane[p.Wartosc + "|" + sp.Id] = (S.T, kto); Wersja++;
            return new(true, $"Sprawdzenie „{sp.Nazwa}” wykonane o {Teraz()} – zapis w dzienniku telefonicznym");
        }
        if (p?.Rodzaj == "numeracja")
        {
            if (!JestPost(p.Wartosc) || !Numeracje.Contains(p.Pole)) return new(false, "Błędna numeracja");
            if (!MojPost(p.Wartosc, k)) return new(false, $"Posterunek {NazwaPosterunku(p.Wartosc)} prowadzi inne stanowisko");
            Numeracja[p.Wartosc] = p.Pole; Wersja++;
            try { ZapiszUstawienia(); } catch (Exception ex) { return new(false, $"Numeracja przyjęta, ale nie zapisana w pliku: {ex.Message}"); }
            return new(true, $"Numeracja telefonogramów posterunku {NazwaPosterunku(p.Wartosc)}: {NazwaNumeracji(p.Pole)}");
        }
        var d = Dzienniki.FirstOrDefault(x => x.Id == p?.Dziennik);
        if (d == null) return new(false, "Nie ma takiego dziennika ruchu");
        if (!Moj(d, k)) return new(false, $"Dziennik {d.Nazwa} prowadzi inne stanowisko");
        var user = k?.User?.Name ?? S.User?.Name;
        if (user == null) return new(false, "Brak zalogowanego użytkownika");
        Aktualizuj(k);
        var s = Strona(d);
        switch (p.Rodzaj)
        {
            case "przyj":
            case "wypr":
            {
                var w = p.Dane?.Kopia() ?? new EdrWersja();
                foreach (var pole in Pola)
                {
                    var v = (Wartosc(w, pole) ?? "").Trim();
                    if (Godzinowe.Contains(pole)) { v = Godzina(v); if (v == null) return new(false, $"Błędna godzina w polu {NazwaPola(pole)}"); }
                    Ustaw(w, pole, v);
                }
                if (w.Nr == "") return new(false, "Wpisz numer pociągu");
                string zmiana = "";
                if (d.Parzysty != 0)
                {
                    var nr = Wymus(w.Nr, p.Rodzaj == "wypr" ? d.Parzysty == 1 : d.Parzysty == -1);
                    if (nr != w.Nr) { zmiana = $" (numer dostosowany do kierunku szlaku: {w.Nr} → {nr})"; w.Nr = nr; }
                }
                w.Kto = user; w.T = S.T; w.Zmiana = Znacznik(); w.Pole = null;
                w.Wpis = Godzinowe.Where(g => Jest(Wartosc(w, g))).ToDictionary(g => g, g => Teraz());
                var r = new EdrWiersz { Id = nextId++, Rodzaj = p.Rodzaj, Wymagane = Wymagane(d.Biezace), Wersje = { w } };
                r.Wymagane.AddRange(d.Biezace.Droznicy.Where(x => x.Dotyczy(p.Rodzaj)).Select(x => "Dr:" + x.Id));
                s.Wiersze.Add(r);
                if (p.Pole == "powrot")
                {
                    // jazda do km i z powrotem (podręcznik EDR rozdz. 7.5): wyjazd z uwagą „do km … i z powrotem” i wiersz powrotu w klamrze
                    var km = (p.Wartosc ?? "").Trim();
                    if (km == "") { s.Wiersze.Remove(r); return new(false, "Wpisz kilometr, do którego jedzie pociąg"); }
                    w.Prz = "---"; r.Wymagane.Remove("Prz");
                    w.Uwagi = $"do km {km} i z powrotem" + (w.Uwagi != "" ? "; " + w.Uwagi : "");
                    var pw = new EdrWersja { Nr = w.Nr, Dw = "---", Odj = "---", Kto = user, T = S.T, Zmiana = w.Zmiana };
                    var r2 = new EdrWiersz { Id = nextId++, Rodzaj = "powrot", Para = r.Id, Wymagane = new() { "Prz" }, Wersje = { pw } };
                    r.Para = r2.Id; s.Wiersze.Add(r2);
                }
                Wersja++;
                return new(true, $"{(p.Rodzaj == "przyj" ? "Przyjęcie" : "Wyprawienie")} pociągu {w.Nr} zapisane w dzienniku {d.Nazwa}{zmiana}");
            }
            case "ustawienia":
            {
                if (Sprawdz(d, p.Ustawienia) is string blad) return new(false, blad);
                Przyjmij(d, p.Ustawienia); Wersja++;
                try { ZapiszUstawienia(); } catch (Exception ex) { return new(false, $"Ustawienia przyjęte, ale nie zapisane w pliku: {ex.Message}"); }
                S.Log("Zdarzenie", $"EDR: zmieniono ustawienia zasadnicze dziennika ruchu {d.Nazwa} ({user})", "EDR", d.Nazwa);
                return new(true, $"Zapisano ustawienia dziennika {d.Nazwa}");
            }
            case "wpis":
            {
                var t = (p.Wartosc ?? "").Trim();
                if (t == "") return new(false, "Wpisz treść");
                s.Wiersze.Add(new EdrWiersz { Id = nextId++, Rodzaj = "wpis", Tekst = t, Wersje = { new EdrWersja { Kto = user, T = S.T, Zmiana = Znacznik(), Uwagi = Teraz() } } });
                Wersja++;
                return new(true, $"Wpis zapisany w dzienniku {d.Nazwa}");
            }
            case "pole":
            {
                var r = d.Strony.SelectMany(x => x.Wiersze).FirstOrDefault(x => x.Id == p.Wiersz);
                if (r == null || r.Rodzaj is "tekst" or "telefonogram" or "wpis") return new(false, r?.Rodzaj == "telefonogram" ? "Telefonogramu nie zmienia się – sprostowanie kolejnym telefonogramem" : "Nie ma takiego wiersza");
                var dr = p.Pole?.StartsWith("Dr:") == true ? d.Biezace.Droznicy.FirstOrDefault(x => x.Id == p.Pole[3..]) : null;
                if (!Pola.Contains(p.Pole) && dr == null) return new(false, "Nie ma takiego pola");
                var v = (p.Wartosc ?? "").Trim();
                if (Godzinowe.Contains(p.Pole) || dr?.Godzina == true) { v = Godzina(v); if (v == null) return new(false, $"Błędna godzina w polu {NazwaPola(p.Pole)}"); }
                else if (dr != null && v is not ("" or "\\")) return new(false, $"Dróżnik {dr.Oznaczenie}: powiadomienie jak na linii dwu- i więcej torowej – ukośnik „\\”");
                if (p.Pole == "Nr" && v == "") return new(false, "Numer pociągu nie może być pusty");
                if (p.Pole == "Nr" && d.Parzysty != 0) v = Wymus(v, r.Rodzaj == "wypr" ? d.Parzysty == 1 : d.Parzysty == -1);
                var stara = r.W;
                if (Wartosc(stara, p.Pole) == v) return new(true, "");
                // kolejne zmiany tego samego pola przez tę samą osobę w ciągu 10 s (np. kółkiem myszy) – jedna wersja
                var cel = r.Wersje.Count > 1 && stara.Kto == user && stara.Pole == p.Pole && S.T - stara.T < 10 ? stara : null;
                if (cel != null) { Ustaw(stara, p.Pole, v); stara.T = S.T; stara.Zmiana = Znacznik(); }
                else
                {
                    cel = stara.Kopia(); Ustaw(cel, p.Pole, v); cel.Kto = user; cel.T = S.T; cel.Zmiana = Znacznik(); cel.Pole = p.Pole;
                    r.Wersje.Add(cel);
                }
                cel.Wpis ??= new();
                if (Jest(v) && (Godzinowe.Contains(p.Pole) || dr?.Godzina == true)) cel.Wpis[p.Pole] = Teraz(); else cel.Wpis.Remove(p.Pole);
                Wersja++;
                return new(true, "");
            }
        }
        return new(false, "Nieznane polecenie EDR");
    }

    /// <summary>
    /// Telefonogram albo inna okoliczność: wpis w dzienniku telefonicznym posterunku (numer kolejny w dobie), a z dziennika ruchu –
    /// także wpis na całą szerokość dziennika ruchu i zmiany stanu bieżącego torów szlakowych.
    /// </summary>
    CmdResult Telefonogram(Desk k, EdrPolecenie p)
    {
        var t = p.Tel;
        if (t == null) return new(false, "Brak danych telefonogramu");
        var d = p.Dziennik == null ? null : Dzienniki.FirstOrDefault(x => x.Id == p.Dziennik);
        if (p.Dziennik != null && d == null) return new(false, "Nie ma takiego dziennika ruchu");
        var post = d?.Post ?? p.Wartosc;
        if (!JestPost(post)) return new(false, "Nie ma takiego posterunku");
        if (!MojPost(post, k)) return new(false, $"Posterunek {NazwaPosterunku(post)} prowadzi inne stanowisko");
        var user = k?.User?.Name ?? S.User?.Name;
        if (user == null) return new(false, "Brak zalogowanego użytkownika");
        Aktualizuj(k);
        var n = new EdrTelefonogram
        {
            Rodzaj = t.Rodzaj == "okolicznosc" ? "okolicznosc" : "telefonogram", Nadaje = t.Nadaje, NrOdbiorcy = (t.NrOdbiorcy ?? "").Trim(), Wzor = string.IsNullOrWhiteSpace(t.Wzor) ? "[bez szablonu]" : t.Wzor,
            Tresc = (t.Tresc ?? "").Trim(), Notatka = (t.Notatka ?? "").Trim(), Data = string.IsNullOrWhiteSpace(t.Data) ? DataDoby(Doba) : t.Data.Trim(),
            Dziennik = d?.Id, Post = post, Doba = Doba, Kto = user, Zapisano = Znacznik(), Zmiany = t.Zmiany?.Where(z => z != null).ToList() ?? new()
        };
        if (n.Tresc == "") return new(false, "Wpisz adres i treść rozmowy lub telefonogramu");
        n.Godz = Godzina(string.IsNullOrWhiteSpace(t.Godz) ? Teraz() : t.Godz);
        n.CzasZak = Godzina(string.IsNullOrWhiteSpace(t.CzasZak) ? Teraz() : t.CzasZak);
        if (n.Godz is null or "" or "---" || n.CzasZak is null or "---") return new(false, "Błędna godzina");
        string tu = NazwaPosterunku(post), tam = d?.Sasiad ?? "";
        if (n.Rodzaj == "telefonogram")
        {
            if (n.Nadaje) { n.Nadal = user; n.NadalPost = tu; n.Odebral = (t.Odebral ?? "").Trim(); n.OdebralPost = string.IsNullOrWhiteSpace(t.OdebralPost) ? tam : t.OdebralPost.Trim(); }
            else { n.Nadal = (t.Nadal ?? "").Trim(); n.NadalPost = string.IsNullOrWhiteSpace(t.NadalPost) ? tam : t.NadalPost.Trim(); n.Odebral = user; n.OdebralPost = tu; }
            if (n.Nadaje ? n.Odebral == "" : n.Nadal == "") return new(false, n.Nadaje ? "Wpisz, kto odebrał telefonogram" : "Wpisz, kto nadał telefonogram");
        }
        else { n.Nadal = user; n.NadalPost = tu; }
        if (d != null)
        {
            foreach (var z in n.Zmiany)
            {
                if (!d.Biezace.Tory.Any(x => x.Szlak == z.Szlak)) return new(false, $"Tor szlaku {z.Szlak} nie należy do dziennika {d.Nazwa}");
                if (z.Zasadniczy is int zk && zk is < -1 or > 1) return new(false, "Błędny kierunek");
            }
        }
        else n.Zmiany.Clear();
        n.Nr = NastepnyNumer(post);
        n.Wprowadzono = Teraz();
        // telefonogram z dziennika ruchu – tylko w dzienniku ruchu; z dziennika telefonicznego (i inne okoliczności) – tylko w telefonicznym
        if (d == null) { if (!Telefoniczny.TryGetValue(post, out var lt)) Telefoniczny[post] = lt = new(); lt.Add(n); }
        if (d != null)
        {
            var s = Strona(d);
            s.Wiersze.Add(new EdrWiersz { Id = nextId++, Rodzaj = "telefonogram", Tel = n, Tekst = n.Tresc, Wersje = { new EdrWersja { Kto = user, T = S.T, Zmiana = n.Zapisano } } });
            Zastosuj(d.Biezace, n.Zmiany);
        }
        Wersja++;
        S.Log("Telefon", $"EDR {tu}: {(n.Rodzaj == "okolicznosc" ? "inne okoliczności" : n.Nadaje ? "telefonogram nadany" : "telefonogram odebrany")} nr {n.Nr}: {n.Tresc.Split('\n')[0]}", "EDR", d?.Nazwa ?? tu);
        return new(true, n.Rodzaj == "okolicznosc" ? $"Inne okoliczności (nr {n.Nr}) zapisane w dzienniku telefonicznym"
            : $"Telefonogram nr {n.Nr} zapisany w {(d != null ? $"dzienniku ruchu {d.Nazwa}" : "dzienniku telefonicznym")}");
    }

    /// <summary>Zdanie pod treścią telefonogramu (podręcznik EDR, Rys. 39): godzina – wprowadzenia do EDR (godzina nadania jest w treści / osobnym polu).</summary>
    public static string Podpis(EdrTelefonogram t)
    {
        var g = string.IsNullOrEmpty(t.Wprowadzono) ? t.Godz : t.Wprowadzono;
        return t.Rodzaj == "okolicznosc" ? $"Wpisał {t.Nadal} {t.NadalPost} godz. {g}" : $"Nadał {t.Nadal} {t.NadalPost}, odebrał {t.Odebral} {t.OdebralPost} godz. {g}";
    }

    static string SprawdzSprawdzenia(List<EdrSprawdzenie> l)
    {
        if (l == null) return "Brak listy sprawdzeń";
        if (l.Count > 4) return "Najwyżej cztery wymagane sprawdzenia (przyciski w prawym górnym rogu)";
        if (l.Any(x => string.IsNullOrWhiteSpace(x.Nazwa))) return "Sprawdzenie musi mieć nazwę";
        if (l.Any(x => x.Okres is < 1 or > 10080)) return "Okres sprawdzenia: od 1 min do 7 dni";
        if (l.GroupBy(x => x.Id).Any(g => g.Count() > 1)) return "Powtórzone sprawdzenie";
        return null;
    }

    /// <summary>Stan sprawdzeń: następny termin od ostatniego wykonania (bez wykonania – od początku symulacji), „wkrotce” – 15 min przed terminem.</summary>
    public List<EdrSprawdzenieStan> StanSprawdzen(Desk k) => Sprawdzenia.Where(x => MojPost(x.Key, k)).SelectMany(x => x.Value.Select(sp =>
    {
        var w = wykonane.TryGetValue(x.Key + "|" + sp.Id, out var o) ? o : ((double T, string Kto)?)null;
        double termin = (w?.T ?? 0) + sp.Okres * 60;
        return new EdrSprawdzenieStan
        {
            Post = x.Key, Id = sp.Id, Nazwa = sp.Nazwa, Okres = sp.Okres, Ostatnie = w != null ? Hm(w.Value.T) : "", Kto = w?.Kto ?? "", Nastepne = Hm(termin),
            Stan = S.T >= termin ? "po_terminie" : termin - S.T <= 900 ? "wkrotce" : "ok"
        };
    })).ToList();

    public static string NazwaNumeracji(string n) => n switch { "miesieczna" => "miesięczna", "roczna" => "roczna", _ => "dobowa" };

    /// <summary>
    /// Następny numer własny telefonogramu posterunku: kolejny w dobie, miesiącu albo roku (numeracja z ustawień posterunku),
    /// wspólny dla telefonogramów z dzienników ruchu i wpisów dziennika telefonicznego.
    /// </summary>
    public int NastepnyNumer(string post)
    {
        var num = Numeracja.GetValueOrDefault(post, "dobowa");
        var dzis = Dzien0.AddDays(Doba);
        bool Ten(EdrTelefonogram t)
        {
            var dt = Dzien0.AddDays(t.Doba);
            return num switch { "roczna" => dt.Year == dzis.Year, "miesieczna" => dt.Year == dzis.Year && dt.Month == dzis.Month, _ => t.Doba == Doba };
        }
        var wszystkie = (Telefoniczny.GetValueOrDefault(post) ?? new()).Concat(Dzienniki.Where(d => d.Post == post).SelectMany(d => d.Strony.SelectMany(s => s.Wiersze)).Where(w => w.Tel != null).Select(w => w.Tel));
        return wszystkie.Where(Ten).Select(t => t.Nr).DefaultIfEmpty(0).Max() + 1;
    }

    public static string NazwaPola(string p) => p.StartsWith("Dr:") ? "dróżnik" : p switch
    {
        "Nr" => "Nr pociągu", "Tor" => "Tor stacyjny", "Dw" => "Droga wolna", "Odj" => "Pociąg odjechał", "Prz" => "Pociąg przyjechał", "Uwagi" => "Uwagi", _ => p
    };
}

using System.Globalization;
using LcsWidzew.Model;

namespace LcsWidzew.Sim;

/// <summary>Jedna wersja wiersza dziennika ruchu (EDR nie kasuje wpisów – każda zmiana to nowa wersja).</summary>
public class EdrWersja
{
    /// <summary>Kolumny dziennika: numer pociągu (kol. 1 / 2 wg parzystości), tor stacyjny (3), droga wolna (4),
    /// pociąg odjechał (5), pociąg przyjechał (6), uwagi (7). Godziny „HH:mm”, „---” – nie dotyczy.</summary>
    public string Nr = "", Tor = "", Dw = "", Odj = "", Prz = "", Uwagi = "";
    /// <summary>Dodatkowe pola wpisu (E9c, jak w EDR.exe – PPM na numerze pociągu): godzina żądania pozwolenia i data wpisu (dd-MM-yyyy, puste – data strony).</summary>
    public string Zad = "", DataWpisu = "";
    public bool Lewy;
    /// <summary>Kto i kiedy wprowadził tę wersję (czas symulacji, data i godzina do sekundy), które pole zmieniono.</summary>
    public string Kto, Zmiana, Pole;
    public double T;
    /// <summary>Kolumna 8: powiadomienie dróżników przejazdowych (id dróżnika → godzina albo ukośnik „\\”).</summary>
    public Dictionary<string, string> Dr = new();
    /// <summary>Godzina wprowadzenia do EDR każdej wpisanej godziny (pole → HH:mm); komórka pokazuje „godzina / godzina wprowadzenia” (film ISDR).</summary>
    public Dictionary<string, string> Wpis = new();
    /// <summary>Wykreślenie wpisu (E8d): przyczyna; null – wpis ważny. Wpisu się nie kasuje – wykreślenie to nowa wersja.</summary>
    public string Wykreslono;
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
    /// <summary>Wpis wykreślony (z przyczyną) – zostaje w dzienniku, nie liczy się do stanu torów ani wykazu.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public bool Wykreslony => W?.Wykreslono != null;
    /// <summary>Wiersz wczytany z pliku dzienników (wcześniejsze uruchomienie) – historia, bez wpływu na bieżący ruch.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public bool Wczytany;
    public bool Kompletny => Wykreslony || Rodzaj is "tekst" or "telefonogram" or "wpis" || Wymagane.All(p => Edr.Wartosc(W, p) != "");
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
    /// <summary>Wzór (E8e): numer wzoru z Data/telefonogramy.json (puste – „[bez szablonu]”) i wartości jego pól.</summary>
    public string WzorNr = "";
    public Dictionary<string, string> Pola = new();
}

/// <summary>Zmiana stanu bieżącego toru szlakowego telefonogramem (null – bez zmiany): status, rodzaj ruchu, kierunek zasadniczy, prowadzenie ruchu jak dla.</summary>
/// <summary>„*” w rodzaju ruchu i podstawie – przywrócenie zasadniczego z ustawień dziennika; PoPociagu – zmiana dopiero po przyjeździe tego pociągu (wzór 21a).</summary>
public class EdrZmianaToru { public string Szlak, Status, Rodzaj, Podstawa, PoPociagu, Trakcja; public int? Zasadniczy; }

/// <summary>Strona dziennika – jedna doba.</summary>
public class EdrStrona { public int Doba; public string Data; public List<EdrWiersz> Wiersze = new(); }

/// <summary>Dziennik ruchu jednego szlaku przyległego do posterunku (wszystkie tory szlakowe do tego samego sąsiada po tej samej linii).</summary>
public class EdrDziennik
{
    public string Id, Post, Nazwa, Sasiad, Linia, Typ;
    public List<string> Tory = new(), Szlaki = new();
    public List<EdrStrona> Strony = new();
    public string Prowadzi;
    /// <summary>Dyżur zdany ręcznie (E9b) – do przyjęcia dyżuru nikt nie prowadzi dziennika (bez automatycznego przyjęcia).</summary>
    public bool Zdany;
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
/// powiadamianie o odjeździe pociągów jak w EDR.exe (E9a, `CrossingRules`): tylko przy wyprawieniu pociągu (nie przy powrocie ze szlaku),
/// „Według szlaku”: Jednotorowych – ukośnik, Dwutorowych – godzina, Wszystkich – godzina na szlaku wielotorowym; „Dla pociągów”: wszystkich,
/// parzystych albo nieparzystych; „Rejestrowanie godziny” (np. zamknięcia rogatek) – zawsze godzina.
/// </summary>
public class EdrDroznik
{
    public string Id = Guid.NewGuid().ToString("N")[..8], Km = "", Numer = "", Nazwa = "", Oznaczenie = "";
    public static readonly string[] WgSzlakow = { "Jednotorowych", "Dwutorowych", "Wszystkich" }, DlaPociagowL = { "Wszystkich", "Parzystych", "Nieparzystych" };
    /// <summary>„Jednotorowych” – ukośnik, „Dwutorowych” – godzina, „Wszystkich” – godzina na szlaku wielotorowym, ukośnik na jednotorowym.</summary>
    public string WgSzlaku = "Dwutorowych";
    /// <summary>„Wszystkich”, „Parzystych”, „Nieparzystych” (wg ostatniej cyfry numeru pociągu).</summary>
    public string DlaPociagow = "Wszystkich";
    /// <summary>Rejestrowanie godziny (np. zamknięcia rogatek, przewidywanego wyjazdu) – wpis godziny także przy „Jednotorowych”.</summary>
    public bool RejestrujGodzine;
    public bool WDzienniku = true;
    /// <summary>Powiadomienie godziną (true) albo ukośnikiem; <paramref name="tory"/> – liczba torów szlaku.</summary>
    public bool Godzina(int tory) => RejestrujGodzine || WgSzlaku switch { "Jednotorowych" => false, "Wszystkich" => tory > 1, _ => true };
    /// <summary>Dotyczy wyprawienia pociągu (nie przyjęcia i nie powrotu ze szlaku), z filtrem parzystości numeru.</summary>
    public bool Dotyczy(string rodzaj, string nr)
    {
        if (!WDzienniku || rodzaj != "wypr") return false;
        if (DlaPociagow is not ("Parzystych" or "Nieparzystych")) return true;
        var c = (nr ?? "").LastOrDefault(char.IsDigit);
        return c != default && ((c - '0') % 2 == 0) == (DlaPociagow == "Parzystych");
    }
    /// <summary>Wartości z wcześniejszych wersji (E5): „Dwu i więcej torowych” → „Dwutorowych”, kierunki → „Wszystkich”.</summary>
    public void Normalizuj()
    {
        if (WgSzlaku == "Dwu i więcej torowych") WgSzlaku = "Dwutorowych";
        if (!WgSzlakow.Contains(WgSzlaku)) WgSzlaku = "Dwutorowych";
        if (!DlaPociagowL.Contains(DlaPociagow)) DlaPociagow = "Wszystkich";
    }
    public EdrDroznik Kopia() => (EdrDroznik)MemberwiseClone();
}

/// <summary>Wymagane sprawdzenie cykliczne posterunku (przyciski w prawym górnym rogu okna EDR; zakładka konfiguracji „Wymagane sprawdzenia”).</summary>
public class EdrSprawdzenie
{
    public string Id = Guid.NewGuid().ToString("N")[..8], Nazwa = "";
    /// <summary>Godziny sprawdzeń w dobie „HH:mm; HH:mm” (E9a, jak w EDR.exe – „Liczba sprawdzeń na dobę”); puste – bez sprawdzeń o stałych godzinach.</summary>
    public string Godziny = "";
    /// <summary>Sprawdzenie N minut po przejęciu dyżuru (null – nie).</summary>
    public int? PoDyzurze;
    public bool Aktywne = true;
    /// <summary>Co ile minut (zapis z E5, używany tylko bez godzin i bez terminu po dyżurze) – 0: nie.</summary>
    public int Okres;
    public EdrSprawdzenie Kopia() => (EdrSprawdzenie)MemberwiseClone();
    /// <summary>Godziny w dobie [min od północy].</summary>
    public List<int> Minuty() => (Godziny ?? "").Split(';', ',').Select(x => Edr.Godzina(x.Trim())).Where(g => g is { Length: 5 }).Select(g => int.Parse(g[..2]) * 60 + int.Parse(g[3..])).Distinct().OrderBy(x => x).ToList();
    public string Opis() => string.Join("; ", new[]
    {
        Minuty().Count > 0 ? "o " + string.Join(", ", Minuty().Select(m => $"{m / 60:00}:{m % 60:00}")) : null,
        PoDyzurze is int pd ? $"{pd} min po przejęciu dyżuru" : null,
        Okres > 0 && Minuty().Count == 0 && PoDyzurze == null ? $"co {Okres} min" : null
    }.Where(x => x != null));
}

/// <summary>Stan sprawdzenia dla okna: ostatnie wykonanie, termin (zaległy albo następny), „ok” / „wkrotce” (≤ 15 min) / „po_terminie”.</summary>
public class EdrSprawdzenieStan { public string Post, Id, Nazwa, Ostatnie = "", Kto = "", Nastepne = "", Stan = "ok", Opis = ""; public int Okres; }

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
    /// <summary>Trakcja elektryczna na torze (E9a, wzory 31 / 32): „Załączona”, „Wyłączona”, puste – bez sieci trakcyjnej / nie podano.</summary>
    public string Trakcja = "";
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
    public string Strona, Status, Trakcja;
    public bool Zajety => Pociagi.Count > 0;
}

/// <summary>Pociąg na torze stacyjnym (kontrolka): klucz („w:id” – z wpisu przyjęcia, „r:id” – postawiony ręcznie), numer, od której godziny, rezerwacja.</summary>
public class EdrNaTorze { public string Klucz, Nr, Od; public bool Zarezerwowany, Recznie; }

/// <summary>
/// Tor stacyjny w kontrolce zajętości (jak w EDR.exe): zajęty od przyjazdu pociągu na ten tor (wpis przyjęcia z „przyjechał”) albo od ręcznego
/// postawienia pociągu przez dyżurnego do odjazdu (wpis wyprawienia z „odjechał”); zarezerwowany – przyjęcie z torem, jeszcze bez „przyjechał”.
/// Na torze może stać kilka pociągów.
/// </summary>
public class EdrTorStacyjny
{
    public string Tor;
    /// <summary>Pierwszy pociąg zajmujący tor i godzina (zgodność z wcześniejszym zapisem).</summary>
    public string Pociag, Od;
    public List<EdrNaTorze> Pociagi = new();
    public bool Zajety => Pociagi.Any(p => !p.Zarezerwowany);
    public bool Zarezerwowany => Pociagi.Count > 0 && !Zajety;
}

/// <summary>
/// Ręczna zmiana zajętości toru stacyjnego (kontrolka, jak w EDR.exe): pociąg postawiony na torze (WierszId 0) albo przesunięcie / usunięcie
/// pociągu z wpisu przyjęcia (WierszId). Postawiony zwalnia tor przy odjeździe tego pociągu, którego nie było przy postawieniu (Znane – wiersze
/// wyprawienia z „odjechał” w chwili postawienia, jak KnownDepartures w EDR.exe). Tylko bieżąca sesja.
/// </summary>
public class EdrZajetoscReczna
{
    public int Id, WierszId; public string Post, Tor, Nr, Dodano, Kto; public bool Usuniety; public List<int> Znane = new();
    /// <summary>Do wyciągu z zajętości torów (E9c): data postawienia, tor przy postawieniu, zmiany „dd-MM-yyyy HH:mm|tor” (pusty tor – usunięcie).</summary>
    public string Data = "", Tor0;
    public List<string> Zmiany = new();
}

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
    /// <summary>Kolumna „P T”: peron cyframi rzymskimi i tor cyframi arabskimi, np. „II 5” (uwaga właściciela 2026-10-28).</summary>
    public string PT { get; set; } = "";
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
    /// <summary>Dane lokalne (E9d): pociąg STÓJ, prognoza, towary niebezpieczne, TWR, przesyłka nadzwyczajna, zgłoszona gotowość; pociąg lokalny (E9e).</summary>
    public bool Stoj { get; set; }
    public bool Prognoza { get; set; }
    public bool StojPrognoza => Stoj && Prognoza;
    public bool TN { get; set; }
    public bool TWR { get; set; }
    public bool PN { get; set; }
    public bool Gotowy { get; set; }
    public bool Lokalny { get; set; }
    /// <summary>Kolejny przejazd przez posterunek (E9c): 1 – pierwszy, 2 – drugi …</summary>
    public int Przejazd { get; set; } = 1;
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
    /// <summary>Błąd odczytu pliku dzienników (zapis wyłączony) – komunikat w oknie EDR.</summary>
    public string BladDziennikow;
    public int Doba;
    /// <summary>Czas symulacji [s] (zakres wykazu -/+ godz.).</summary>
    public double T;
    public long Wersja;
    /// <summary>E9: użytkownicy lokalni (bez haseł), zalogowany użytkownik lokalny i jego rola, tryb obserwatora, historia zmian (ostatnie),
    /// dane lokalne pociągów posterunków stanowiska (posterunek|klucz), usługi, pociągi lokalne, początek dyżuru posterunków (GG:MM).</summary>
    public List<EdrUzytkownik> Uzytkownicy = new();
    public string Operator, OperatorRola;
    public bool Obserwator;
    public List<EdrZdarzenie> Historia = new();
    public Dictionary<string, EdrPociagLokalny> Lokalne = new();
    public List<EdrUsluga> Uslugi = new();
    public List<EdrPociagLok> PociagiLok = new();
    public Dictionary<string, string> DyzurOd = new();
    /// <summary>Ręczne zmiany zajętości torów stacyjnych posterunków stanowiska (wyciąg z zajętości torów).</summary>
    public List<EdrZajetoscReczna> Reczne = new();
}

/// <summary>Polecenie okna EDR: nowy wiersz („przyj” / „wypr” z danymi) albo zmiana pola wiersza („pole”).</summary>
public class EdrPolecenie
{
    public string Dziennik, Rodzaj, Pole, Wartosc, Klucz, Post, Pole2;
    public List<EdrPosterunekUst> Posterunki; public int Wiersz; public EdrWersja Dane; public EdrUstawienia Ustawienia; public EdrTelefonogram Tel; public List<EdrSprawdzenie> Sprawdzenia;
    /// <summary>E9: użytkownicy lokalni, dane lokalne pociągu, wiersz zakładki, usługa, pociąg lokalny (i lista z importu CSV), treść pliku (wczytanie sesji / konfiguracji).</summary>
    public List<EdrUzytkownik> Uzytkownicy; public EdrPociagLokalny Lok; public EdrWierszLok WierszLok; public EdrUsluga Usluga;
    public EdrPociagLok PociagLok; public List<EdrPociagLok> PociagiLok; public string Plik;
}

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
    /// <summary>
    /// Zapis dzienników ruchu i telefonicznych do pliku &lt;układ&gt;.edr_dzienniki.json (E8d, decyzja właściciela 2026-10-28: ciągła historia –
    /// każde uruchomienie dopisuje do tych samych dzienników). Włączany tylko w pracy na pulpicie (nie w selftestach i zrzutach).
    /// </summary>
    public static bool ZapisDziennikow;
    public string PlikDziennikow;
    static readonly HashSet<string> migawka = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Test: nowe uruchomienie programu – kopie plików dzienników sprzed sesji zrobią się od nowa.</summary>
    public static void NoweUruchomienie() => migawka.Clear();
    /// <summary>Błąd odczytu pliku dzienników – zapis wyłączony, żeby nie zastąpić pliku (null – bez błędu).</summary>
    public string BladDziennikow;
    bool brudne;
    /// <summary>Dzienniki z pliku, których nie ma w obecnej konfiguracji – zapisywane dalej bez zmian.</summary>
    List<PlikDzDziennik> obceDzienniki = new();
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

    public static readonly string[] Godzinowe = { "Dw", "Odj", "Prz", "Zad" };
    public static readonly string[] Pola = { "Nr", "Tor", "Dw", "Odj", "Prz", "Uwagi", "Zad", "Data" };

    public Edr(Simulator s)
    {
        S = s;
        PlikUstawien = S.L.SourcePath == null ? null : Path.Combine(Path.GetDirectoryName(S.L.SourcePath) ?? "", Path.GetFileNameWithoutExtension(S.L.SourcePath) + ".edr.json");
        PlikDziennikow = S.L.SourcePath == null ? null : Path.Combine(Path.GetDirectoryName(S.L.SourcePath) ?? "", Path.GetFileNameWithoutExtension(S.L.SourcePath) + ".edr_dzienniki.json");
        WczytajUstawienia();
        if (ZapisDziennikow) WczytajDzienniki();
    }

    // ------------------------------------------------------------------ zapis dzienników (plik <układ>.edr_dzienniki.json)
    public class PlikDzDziennik { public string Id, Post, Nazwa, Prowadzi; public bool Zdany; public List<EdrStrona> Strony = new(); }
    /// <summary>Plik dzienników (E9: także historia zmian, dane lokalne pociągów, usługi i pociągi lokalne).</summary>
    public class PlikDz
    {
        public int Wersja = 1; public List<PlikDzDziennik> Dzienniki = new(); public Dictionary<string, List<EdrTelefonogram>> Telefoniczny = new();
        public List<EdrZdarzenie> Historia; public Dictionary<string, EdrPociagLokalny> Lokalne; public List<EdrUsluga> Uslugi; public List<EdrPociagLok> PociagiLok;
    }

    int DobaDaty(string data, int domyslna) =>
        DateTime.TryParseExact((data ?? "").Length >= 10 ? data[..10] : data ?? "", "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? (int)(dt.Date - Dzien0.Date).TotalDays : domyslna;

    /// <summary>
    /// Wczytuje dzienniki z pliku: strony dzienników o tym samym id (doba z daty strony względem dnia startu), dziennik telefoniczny, prowadzący.
    /// Wiersze z pliku są historią (<see cref="EdrWiersz.Wczytany"/>) – nie zajmują torów i nie liczą się w wykazie. Błędny plik – bez zapisu.
    /// </summary>
    public void WczytajDzienniki()
    {
        if (PlikDziennikow == null) return;
        // stan pliku przed pierwszą symulacją w tym uruchomieniu programu (kolejne scenariusze nie nadpisują kopii)
        if (!File.Exists(PlikDziennikow)) { if (migawka.Add(PlikDziennikow)) { try { File.Delete(PlikDziennikow + ".sesja"); } catch { } } return; }
        try
        {
            ZastosujPlik(System.Text.Json.JsonSerializer.Deserialize<PlikDz>(File.ReadAllText(PlikDziennikow), JsonOpt), true);
            // stan pliku przed tą sesją – „Nie zapisuj” przy zamknięciu programu przywraca go (E9e, decyzja właściciela 2026-10-28)
            if (migawka.Add(PlikDziennikow)) { try { File.Copy(PlikDziennikow, PlikDziennikow + ".sesja", true); } catch { } }
        }
        catch (Exception ex)
        {
            BladDziennikow = $"nie wczytano dzienników ({Path.GetFileName(PlikDziennikow)}): {ex.Message} – plik zostaje bez zmian, dzienniki tej sesji nie są zapisywane";
            S.Log("Zdarzenie", "EDR: " + BladDziennikow, "System", "");
        }
    }

    /// <summary>
    /// Dzienniki z pliku (start programu albo „Wczytaj sesję z dziennikami”): strony dzienników o tym samym id jako historia, dziennik telefoniczny,
    /// historia zmian, dane lokalne pociągów, usługi, pociągi lokalne. <paramref name="start"/> – przy starcie (dzienniki spoza konfiguracji – zachowane).
    /// </summary>
    void ZastosujPlik(PlikDz f, bool start)
    {
        if (f == null || f.Wersja != 1 || f.Dzienniki == null) throw new InvalidDataException("nieobsługiwana wersja pliku");
        if (start) obceDzienniki = new();
        foreach (var z in f.Dzienniki)
        {
            var d = Dzienniki.FirstOrDefault(x => x.Id == z.Id);
            if (d == null) { if (start) obceDzienniki.Add(z); continue; }
            foreach (var s in z.Strony ?? new())
            {
                s.Doba = DobaDaty(s.Data, s.Doba);
                foreach (var w in s.Wiersze) { w.Wczytany = true; foreach (var v in w.Wersje) v.T = -1e9; }
            }
            d.Strony = (z.Strony ?? new()).OrderBy(s => s.Doba).ToList();
            d.Prowadzi = z.Prowadzi; d.Zdany = z.Zdany;
            foreach (var t in d.Strony.SelectMany(s => s.Wiersze).Where(w => w.Tel != null)) t.Tel.Doba = DobaDaty(t.Tel.Zapisano, t.Tel.Doba);
            Przyjmij(d, d.Ustawienia);
        }
        foreach (var (pk, l) in f.Telefoniczny ?? new())
        {
            foreach (var t in l) t.Doba = DobaDaty(t.Zapisano, t.Doba);
            Telefoniczny[pk] = l;
        }
        Historia = f.Historia ?? new(); Lokalne = f.Lokalne ?? new(); Uslugi = f.Uslugi ?? new(); PociagiLok = f.PociagiLok ?? new();
        if (start) historiaStart = Historia.Count;
        var ids = Dzienniki.SelectMany(d => d.Strony.SelectMany(s => s.Wiersze)).SelectMany(w => new[] { w.Id, w.Para })
            .Concat(obceDzienniki.SelectMany(d => d.Strony.SelectMany(s => s.Wiersze)).Select(w => w.Id));
        nextId = Math.Max(nextId, ids.DefaultIfEmpty(0).Max() + 1);
        Wersja++;
    }

    /// <summary>Stan dzienników do pliku (zapis ciągły i „Zapisz sesję z dziennikami”).</summary>
    public PlikDz StanPliku()
    {
        var f = new PlikDz { Historia = Historia, Lokalne = Lokalne, Uslugi = Uslugi, PociagiLok = PociagiLok };
        foreach (var d in Dzienniki) f.Dzienniki.Add(new PlikDzDziennik { Id = d.Id, Post = d.Post, Nazwa = d.Nazwa, Prowadzi = d.Prowadzi, Zdany = d.Zdany, Strony = d.Strony });
        f.Dzienniki.AddRange(obceDzienniki);
        foreach (var (pk, l) in Telefoniczny) f.Telefoniczny[pk] = l;
        return f;
    }
    public string StanPlikuJson() => System.Text.Json.JsonSerializer.Serialize(StanPliku(), JsonOpt);

    /// <summary>
    /// Koniec pracy programu (E9e, decyzja właściciela 2026-10-28 – pytanie „Zachować dane sesji EDR?” przy każdym zamknięciu): zapis albo
    /// przywrócenie pliku dzienników sprzed tej sesji (bez pliku sprzed sesji – plik zostaje usunięty).
    /// </summary>
    public void ZakonczSesje(bool zachowaj)
    {
        if (!ZapisDziennikow || PlikDziennikow == null || BladDziennikow != null) return;
        var kopia = PlikDziennikow + ".sesja";
        try
        {
            if (zachowaj) { ZapiszDzienniki(); if (File.Exists(kopia)) File.Delete(kopia); return; }
            ZapisDziennikow = false;
            if (File.Exists(kopia)) File.Copy(kopia, PlikDziennikow, true); else if (File.Exists(PlikDziennikow)) File.Delete(PlikDziennikow);
            if (File.Exists(kopia)) File.Delete(kopia);
        }
        catch (Exception ex) { S.Log("Zdarzenie", $"EDR: zakończenie sesji – {ex.Message}", "System", ""); }
    }

    /// <summary>Czy w tej sesji zmieniono dzienniki (pytanie przy zamknięciu tylko wtedy).</summary>
    public bool ZmianySesji => Historia.Count > historiaStart || Dzienniki.Any(d => d.Strony.Any(s => s.Wiersze.Any(w => !w.Wczytany)));
    int historiaStart;

    /// <summary>Zapis dzienników: plik tymczasowy, potem podmiana z kopią „.bak” – przerwany zapis nie psuje pliku (jak w EDR.exe).</summary>
    public void ZapiszDzienniki()
    {
        brudne = false;
        if (!ZapisDziennikow || PlikDziennikow == null || BladDziennikow != null) return;
        var f = StanPliku();
        var tmp = PlikDziennikow + ".tmp";
        try
        {
            File.WriteAllText(tmp, System.Text.Json.JsonSerializer.Serialize(f, JsonOpt));
            if (File.Exists(PlikDziennikow)) File.Replace(tmp, PlikDziennikow, PlikDziennikow + ".bak", true);
            else File.Move(tmp, PlikDziennikow);
        }
        catch (Exception ex)
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            S.Log("Zdarzenie", $"EDR: nie zapisano dzienników ({Path.GetFileName(PlikDziennikow)}): {ex.Message}", "System", "");
        }
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
        "Nr" => w.Nr, "Tor" => w.Tor, "Dw" => w.Dw, "Odj" => w.Odj, "Prz" => w.Prz, "Uwagi" => w.Uwagi, "Zad" => w.Zad, "Data" => w.DataWpisu, _ => ""
    } ?? "";

    static void Ustaw(EdrWersja w, string pole, string v)
    {
        if (pole.StartsWith("Dr:")) { w.Dr ??= new(); if (v == "") w.Dr.Remove(pole[3..]); else w.Dr[pole[3..]] = v; return; }
        switch (pole) { case "Nr": w.Nr = v; break; case "Tor": w.Tor = v; break; case "Dw": w.Dw = v; break; case "Odj": w.Odj = v; break; case "Prz": w.Prz = v; break; case "Uwagi": w.Uwagi = v; break; case "Zad": w.Zad = v; break; case "Data": w.DataWpisu = v; break; }
    }

    /// <summary>Data wpisu „dd-MM-yyyy” (pusta – data strony); null – błędna.</summary>
    public static string DataWpisu(string v)
    {
        v = (v ?? "").Trim();
        if (v == "") return "";
        return DateTime.TryParseExact(v, new[] { "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy", "d.M.yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) : null;
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
        // bez dwukropka: „0812” i „812” → 08:12 (E8d, jak w EDR.exe)
        if (v.Length is 3 or 4 && v.All(char.IsDigit)) v = v[..^2] + ":" + v[^2..];
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

    /// <summary>
    /// Pola wymagane wg opcji wypełniania z ustawień bieżących dziennika (numer pociągu – zawsze). Droga wolna także przy telefonicznym zapowiadaniu,
    /// na szlaku jednotorowym, przy wprowadzonym ruchu jednotorowym (tor zasadniczo jednokierunkowy dwukierunkowo) i przy zamkniętym torze (E8d, jak w EDR.exe).
    /// </summary>
    public static List<string> Wymagane(EdrUstawienia u, EdrUstawienia zasadnicze = null)
    {
        var l = new List<string> { "Nr" };
        if (u.WymTor) l.Add("Tor");
        bool jednotorowy = u.Tory.Count == 1 || u.Tory.Any(t => t.Status == "Zamknięty")
            || zasadnicze != null && u.Tory.Any(t => t.Rodzaj == "Dwukierunkowy" && zasadnicze.Tory.FirstOrDefault(x => x.Szlak == t.Szlak)?.Rodzaj == "Jednokierunkowy");
        // „droga wolna” także przy bieżącym telefonicznym zapowiadaniu pociągów (podręcznik EDR rozdz. 7.2: wybór pól zależy od bieżącej podstawy prowadzenia ruchu)
        if (u.WymDw || jednotorowy || u.Tory.Any(t => t.Podstawa == Podstawy[3])) l.Add("Dw");
        if (u.WymOdj) l.Add("Odj");
        if (u.WymPrz) l.Add("Prz");
        return l;
    }

    // ------------------------------------------------------------------ ustawienia zasadnicze (plik <układ>.edr.json)
    static readonly System.Text.Json.JsonSerializerOptions JsonOpt = new() { IncludeFields = true, WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    class PlikEdr { public Dictionary<string, EdrUstawienia> Dzienniki = new(); public Dictionary<string, string> Numeracja = new(); public Dictionary<string, List<EdrSprawdzenie>> Sprawdzenia = new(); public List<EdrPosterunekUst> Posterunki; public EdrBezWypadku BezWypadku; public List<EdrUzytkownik> Uzytkownicy; }

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
            if (f?.Uzytkownicy != null) Uzytkownicy = f.Uzytkownicy.Where(u => !string.IsNullOrWhiteSpace(u.Id) && EdrUzytkownik.Role.Contains(u.Rola)).ToList();
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
        f.Posterunki = posterunkiUst; f.BezWypadku = BezWypadku; f.Uzytkownicy = Uzytkownicy.Count > 0 ? Uzytkownicy.Select(u => { var x = u.Kopia(); x.Haslo = null; x.MaHaslo = false; return x; }).ToList() : null;
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
        foreach (var x in u.Droznicy) x.Normalizuj();
        if (u.Tory.Any(t => (t.Trakcja ?? "") is not ("" or "Załączona" or "Wyłączona"))) return "Błędny stan trakcji elektrycznej";
        return null;
    }

    /// <summary>Przyjmuje ustawienia (bez pól tylko do odczytu z układu: numer toru, urządzenie srk).</summary>
    static void Przyjmij(EdrDziennik d, EdrUstawienia u)
    {
        var n = u.Kopia();
        foreach (var t in n.Tory) { var z = d.Ustawienia.Tory.First(x => x.Szlak == t.Szlak); t.Tor = z.Tor; t.Urzadzenie = z.Urzadzenie; t.Status ??= "Otwarty"; t.Strona ??= ""; t.Trakcja ??= ""; }
        foreach (var x in n.Droznicy ??= new()) x.Normalizuj();
        d.Ustawienia = n; d.Parzysty = n.Parzysty;
        PrzeliczBiezace(d);
    }

    /// <summary>Bieżące = zasadnicze, a na nie – zmiany telefonogramów w kolejności wpisów (zamknięcie po przejeździe – od przyjazdu pociągu).</summary>
    static void PrzeliczBiezace(EdrDziennik d)
    {
        var b = d.Ustawienia.Kopia();
        foreach (var t in d.Strony.SelectMany(s => s.Wiersze).Where(w => w.Tel != null).OrderBy(w => w.Id)) Zastosuj(b, t.Tel.Zmiany, d);
        d.Biezace = b;
    }

    static void Zastosuj(EdrUstawienia u, List<EdrZmianaToru> zmiany, EdrDziennik d)
    {
        foreach (var z in zmiany ?? new())
        {
            var t = u.Tory.FirstOrDefault(x => x.Szlak == z.Szlak);
            if (t == null) continue;
            // wzór 21a: tor zamknięty dopiero po przyjeździe pociągu (wpis „przyjechał” w dzienniku)
            if (!string.IsNullOrEmpty(z.PoPociagu) && !d.Strony.SelectMany(s => s.Wiersze).Any(w => w.Rodzaj is "przyj" or "wypr" && Biezacy(w) && TenSam(w.W.Nr, z.PoPociagu) && Jest(w.W.Prz))) continue;
            var zas = d.Ustawienia.Tory.FirstOrDefault(x => x.Szlak == z.Szlak);
            if (!string.IsNullOrEmpty(z.Status)) t.Status = z.Status;
            if (!string.IsNullOrEmpty(z.Rodzaj)) t.Rodzaj = z.Rodzaj == "*" ? zas?.Rodzaj ?? t.Rodzaj : z.Rodzaj;
            if (!string.IsNullOrEmpty(z.Podstawa)) t.Podstawa = z.Podstawa == "*" ? zas?.Podstawa : z.Podstawa;
            if (z.Zasadniczy is int k) t.Zasadniczy = k;
            if (!string.IsNullOrEmpty(z.Trakcja)) t.Trakcja = z.Trakcja == "*" ? zas?.Trakcja ?? "" : z.Trakcja;
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
        brudne = true;
        s.Wiersze.Add(new EdrWiersz { Id = nextId++, Rodzaj = "tekst", Tekst = t, Wersje = { new EdrWersja { Kto = d.Prowadzi, T = S.T, Zmiana = Znacznik() } } });
        Wersja++;
    }

    string Znacznik() => $"{DataDoby(Doba)} {S.Clock()}";

    /// <summary>
    /// Wpisy automatyczne: nowa strona przy zmianie doby, „Północ dnia …”, „Dyżur przyjął …” w dziennikach stanowiska po zalogowaniu do symulatora
    /// (decyzja właściciela 2026-10-28: automatycznie tylko przy zalogowaniu; potem przyjęcie i zdanie dyżuru ręcznie – E9b) i w nowym dzienniku.
    /// </summary>
    public void Aktualizuj(Desk k)
    {
        var user = k?.User?.Name ?? S.User?.Name;
        if (k != null && user != null && !zalogowany.ContainsKey(k.Id)) zalogowany[k.Id] = Znacznik();
        var moje = Dzienniki.Where(x => Moj(x, k)).ToList();
        foreach (var d in moje) Strona(d);
        if (user == null) return;
        bool nowyLogin = logowanie.GetValueOrDefault(Klucz(k)) != user;
        logowanie[Klucz(k)] = user;
        var kto = Kto(k);
        foreach (var g in moje.Where(d => nowyLogin ? d.Prowadzi != user : d.Prowadzi == null && !d.Zdany).GroupBy(d => d.Post))
            PrzyjmijDyzur(g.Key, nowyLogin ? user : kto, Teraz(), S.T, g);
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
        if (S.Desks.Count == 0) Aktualizuj(null);
        else foreach (var k in S.Desks.Values) if (k.User != null) Aktualizuj(k);
        if (brudne) ZapiszDzienniki();
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
        if (post != null && !string.IsNullOrEmpty(tor) && OstatniRuch(id, post, out _) is EdrRuch rt && rt.Odj == null && rt.Tor != tor) { rt.Tor = tor; Wersja++; }
        if (byl && p.Post == post && p.Blok == blok) return;
        slad[id] = (post, blok);
        if (byl && p.Post != null && p.Post != post && OstatniRuch(id, p.Post, out _) is EdrRuch r0 && r0.Odj == null)
        {
            r0.Odj = S.T; r0.NrWyj = num; r0.WSzlak = blok; Wersja++;
        }
        // kolejny przejazd przez ten sam posterunek (E9c, jak wiersz na punkt trasy w EDR.exe): osobny zapis „id|post#n”
        if (post != null && (!byl || p.Post != post) && (OstatniRuch(id, post, out int n) is not EdrRuch ost || ost.Odj != null))
        {
            Ruch[n == 0 ? id + "|" + post : $"{id}|{post}#{n + 1}"] = new EdrRuch { Nr = num, NrWyj = num, Prz = byl ? S.T : null, ZSzlak = byl && p.Post == null ? p.Blok : null, Tor = string.IsNullOrEmpty(tor) ? null : tor };
            Wersja++;
        }
    }

    /// <summary>Ostatni przejazd pociągu przez posterunek (<paramref name="n"/> – liczba przejazdów, 0 – nie było).</summary>
    EdrRuch OstatniRuch(string id, string post, out int n)
    {
        n = 0;
        if (!Ruch.TryGetValue(id + "|" + post, out var r)) return null;
        n = 1;
        while (Ruch.TryGetValue($"{id}|{post}#{n + 1}", out var r2)) { r = r2; n++; }
        return r;
    }

    /// <summary>Przejazdy pociągu (numery <paramref name="nums"/>) przez posterunek w kolejności.</summary>
    List<EdrRuch> RuchyNaPosterunku(string post, List<string> nums)
    {
        int Nr(string k) { var i = k.LastIndexOf('#'); return i > k.LastIndexOf('|') && int.TryParse(k[(i + 1)..], out var n) ? n : 1; }
        string Post(string k) { var s = k[(k.LastIndexOf('|') + 1)..]; var i = s.IndexOf('#'); return i >= 0 ? s[..i] : s; }
        return Ruch.Where(x => Post(x.Key) == post && (nums.Contains(x.Value.Nr) || nums.Contains(x.Value.NrWyj))).OrderBy(x => Nr(x.Key)).ThenBy(x => x.Value.Prz ?? x.Value.Odj ?? 0).Select(x => x.Value).ToList();
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
    /// <summary>„P T”: peron z rozkładu cyframi rzymskimi (liczba zamieniana, zapis rzymski zostaje) i tor cyframi arabskimi.</summary>
    internal static string PeronTor(string peron, string tor)
    {
        var p = (peron ?? "").Trim();
        if (int.TryParse(p, out var n) && n is > 0 and < 40)
        {
            p = "";
            foreach (var (w, z) in new[] { (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I") }) while (n >= w) { p += z; n -= w; }
        }
        return (p + " " + (tor ?? "").Trim()).Trim();
    }
    string HmD(double? t) => t is double v ? Dzien0.AddDays(Math.Floor((S.StartClock + v) / 86400)).ToString("dd-MM", CultureInfo.InvariantCulture) + " " + Hm(v) : "";

    /// <summary>Trasa pociągu wg rozkładu: posterunki LCS w kolejności przyjazdu.</summary>
    List<EdrTrasa> TrasaPociagu(TimetableEntry q, string nr)
    {
        // bieg z rozkładu rzeczywistego – tylko odcinek z tym numerem (obieg jednostki: przyjazd jednym numerem, odjazd innym)
        var pb = q.Przebieg?.Where(x => TenSam(x.Num, nr)).ToList();
        if (pb?.Count > 0)
        {
            var l = new List<(string Post, double? Arr, double? Dep, string Tor, string Peron, string Num, int Lp)>();
            foreach (var x in pb)
            {
                // ten sam posterunek dwa razy z rzędu (przyjazd i odjazd zapisane osobno) – jeden wiersz
                if (l.Count > 0 && l[^1].Post == x.Post) { l[^1] = l[^1] with { Dep = x.Dep }; continue; }
                l.Add((x.Post, x.Arr, x.Dep, x.Track, x.Peron, x.Num, q.Przebieg.IndexOf(x) + 1));
            }
            // numer zaczyna się albo kończy w obszarze LCS (obieg jednostki): bez przyjazdu na pierwszym i bez odjazdu na ostatnim posterunku
            if (q.Przebieg.IndexOf(pb[0]) > 0) l[0] = l[0] with { Arr = null };
            if (q.Przebieg.IndexOf(pb[^1]) < q.Przebieg.Count - 1) l[^1] = l[^1] with { Dep = null };
            return l.Select(x => PunktTrasy(q, x.Post, x.Arr, x.Dep, x.Tor, x.Peron, x.Num ?? nr, x.Lp)).ToList();
        }
        return TrasaPlanu(q, nr);
    }
    List<EdrTrasa> TrasaPlanu(TimetableEntry q, string nr) => (q.PlanPrz ?? new()).OrderBy(x => x.Value).Select((x, i) =>
        PunktTrasy(q, x.Key, x.Value, q.PlanOdj?.TryGetValue(x.Key, out var o) == true ? o : null, q.PlanTor?.GetValueOrDefault(x.Key), q.PlanPeron?.GetValueOrDefault(x.Key), nr, i + 1)).ToList();

    /// <summary>
    /// Wiersz trasy (E8c): plan z rozkładu, rzeczywiste z ruchu na posterunku (<see cref="Ruch"/>); bez przejazdu – czas planowy, jak w wykazie
    /// (uwaga właściciela 2026-10-28). Postój zamówiony z planu, obliczony z ruchu; „ph” przy postoju co najmniej 30 s.
    /// </summary>
    EdrTrasa PunktTrasy(TimetableEntry q, string post, double? arr, double? dep, string tor, string peron, string nr, int lp)
    {
        var rec = Ruch.Where(x => x.Key.EndsWith("|" + post) && (TenSam(x.Value.Nr, nr) || TenSam(x.Value.NrWyj, nr))).Select(x => x.Value).FirstOrDefault();
        double? przR = rec?.Prz ?? (rec == null ? arr : null), odjR = rec?.Odj ?? (rec?.Prz is double rp && arr is double a0 && dep is double d0 ? d0 + Math.Max(0, rp - a0) : rec == null ? dep : null);
        bool ph = arr is double a && dep is double d && d - a >= 30;
        return new EdrTrasa
        {
            Posterunek = NazwaPosterunku(post), Przyjazd = HmD(arr), Odjazd = HmD(dep), Tor = tor ?? "", Postoj = ph ? "ph" : "",
            PrzPlanData = Dz(arr), PrzPlanGodz = Hm(arr), PrzRzeczData = Dz(przR), PrzRzeczGodz = Hm(przR), PrzOdch = Odch(arr, przR),
            Z = rec?.Prz != null || rec?.Odj != null ? "✓" : "", Lp = lp.ToString(CultureInfo.InvariantCulture),
            PZam = ph ? Minuty(arr, dep) : "0,0", PObl = Minuty(rec?.Prz, rec?.Odj) is var pb && pb != "" ? pb : "0,0", PT = PeronTor(peron, tor),
            OdjPlanData = Dz(dep), OdjPlanGodz = Hm(dep), OdjRzeczData = Dz(odjR), OdjRzeczGodz = Hm(odjR), OdjOdch = Odch(dep, odjR),
            Rodz = q.KatIr11 ?? "", Nr = nr ?? "", SeriaPl = q.Veh ?? "", DlPl = q.Len is double ln ? ln.ToString("0", CultureInfo.InvariantCulture) : "",
        };
    }
    /// <summary>Data „dd-MM-yy” jak w trasie EDR.exe.</summary>
    string Dz(double? t) => t is double v ? Dzien0.AddDays(Math.Floor((S.StartClock + v) / 86400)).ToString("dd-MM-yy", CultureInfo.InvariantCulture) : "";
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
        bool Konczy(string b) => b != null && S.L.Blocks.TryGetValue(b, out var bd) && bd.Ends.Any(e => e.Post == post);
        var wiersze = Dzienniki.Where(d => d.Post == post).SelectMany(d => d.Strony.SelectMany(s => s.Wiersze)).Where(w => w.Rodzaj is "przyj" or "wypr" && Biezacy(w)).ToList();
        // wpisy z dziennikiem (szlaki) – przy kilku przejazdach przez posterunek wpis dotyczy przejazdu, którego szlak ma ten dziennik
        var szlakiWiersza = Dzienniki.Where(d => d.Post == post).SelectMany(d => d.Strony.SelectMany(s => s.Wiersze).Select(w => (w.Id, d.Szlaki))).GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First().Szlaki);
        // bez ruchu pociągów (np. test) – tylko pociągi lokalne
        foreach (var q in S.Trains == null ? Enumerable.Empty<TimetableEntry>() : S.Rozklad.Queue)
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
            // przejazdy przez posterunek wg biegu z rozkładu rzeczywistego (E9c): ten sam posterunek kilka razy (obieg z powrotem) – wiersz na każdy przejazd
            var wiz = new List<(int Od, int Do)>();
            for (int i = 0; i < (q.Przebieg?.Count ?? 0); i++)
                if (q.Przebieg[i].Post == post) { if (wiz.Count > 0 && wiz[^1].Do == i - 1) wiz[^1] = (wiz[^1].Od, i); else wiz.Add((i, i)); }
            var ruchy = RuchyNaPosterunku(post, nums);
            int ileWiz = Math.Max(1, wiz.Count > 1 ? wiz.Count : 1);
            // szlaki z rozkładu na początek i koniec biegu (pierwszy przejazd – przyjazd, ostatni – odjazd, np. „LF>1A” po zmianie czoła)
            string z0 = Konczy(q.From) ? q.From : null, w0 = new[] { q.Duty?.To ?? q.To }.Concat(term.Select(t => t.To)).LastOrDefault(Konczy); var rec0 = rec;
            for (int vi = 0; vi < ileWiz; vi++)
            {
            bool wiele = ileWiz > 1;
            var wz = wiele ? wiz[vi] : default;
            if (wiele)
            {
                // szlaki przejazdu: z posterunku przed nim i do posterunku po nim (pierwszy – także szlak przyjazdu z rozkładu, ostatni – odjazdu)
                string prev = wz.Od > 0 ? q.Przebieg[wz.Od - 1].Post : null, next = wz.Do < q.Przebieg.Count - 1 ? q.Przebieg[wz.Do + 1].Post : null;
                z = (prev != null ? Laczacy(prev, post) ?? Laczacy(post, prev) : null) ?? (vi == 0 ? z0 : null);
                w = (next != null ? Laczacy(post, next) : null) ?? (vi == ileWiz - 1 ? w0 : null);
                rec = vi < ruchy.Count ? ruchy[vi] : null;
                if (rec != null) { z ??= rec.ZSzlak; w ??= rec.WSzlak; }
                if (rec?.WSzlak != null) w = rec.WSzlak;
                if (rec?.ZSzlak != null) z = rec.ZSzlak;
            }
            double? przPlan = wiele ? (wz.Od > 0 || z0 != null ? q.Przebieg[wz.Od].Arr : null) : q.PlanPrz?.GetValueOrDefault(post) is double pp ? pp : null;
            double? odjPlan = wiele ? (wz.Do < q.Przebieg.Count - 1 || w0 != null ? q.Przebieg[wz.Do].Dep : null) : q.PlanOdj?.TryGetValue(post, out var po) == true ? po : q.DepBy?.TryGetValue(post, out var db) == true ? db
                : q.Duty?.DepBy?.TryGetValue(post, out var dd) == true ? dd : start ? q.Duty?.Dep ?? q.Dep : z != null && q.Stop && q.StopAt == null && q.Term == null ? q.Dep : null;
            string nr = wiele ? q.Przebieg[wz.Od].Num ?? nums[0] : nums[0], nrWyj = wiele ? rec?.NrWyj ?? q.Przebieg[wz.Do].Num ?? nr : rec?.NrWyj ?? term.LastOrDefault(t => t.AtPost == post)?.NewNum ?? nr;
            // tor z rozkładu: GTFS – na każdym posterunku; rozkład wbudowany / plik układu – krótki zapis „tor” pociągu wjeżdżającego z szlaku posterunku
            var tor = (wiele ? q.Przebieg[wz.Od].Track : null) ?? q.PlanTor?.GetValueOrDefault(post) ?? (start ? q.Duty?.Track ?? Station.Lbl(q.StartSec) : null)
                ?? (q.PlanTor == null && z != null && q.Track is string qt && qt.Length is > 0 and <= 4 && !qt.Contains(' ') ? qt : null) ?? "";
            bool ph = wiele ? przPlan != null && odjPlan != null && odjPlan - przPlan >= 30
                : q.StopAt?.Contains(post) == true || q.Stop && q.StopAt == null || term.Any(t => t.AtPost == post) || przPlan != null && odjPlan != null && odjPlan - przPlan >= 30;
            var numsV = wiele ? new List<string> { nr, nrWyj }.Distinct().ToList() : nums;
            bool wDrodze = q.Done || S.Trains.List.Any(t => nums.Contains(t.Num));
            string stan = rec == null ? (wDrodze ? "w drodze" : "planowy") : rec.Odj != null ? "odjechał" : "na stacji";
            // wstęga: pociąg zbliża się z szlaku posterunku (przyjazd / przejazd) albo stoi na stacji (odjazd / przejazd)
            string wstega = rec == null ? (wDrodze && z != null ? (ph ? "przyjazd" : "przejazd") : "") : rec.Odj == null ? (ph ? "odjazd" : "przejazd") : "";
            var klucz = $"{q.Num}@{q.T:0}" + (vi > 0 ? "@" + (vi + 1) : "");
            // czas rzeczywisty: z ruchu; bez niego – planowy, a odjazd przesunięty o opóźnienie przyjazdu (bez opóźnienia – czas planowy, uwaga właściciela 2026-10-28)
            double opPrz = rec?.Prz is double rp && przPlan is double pp2 ? Math.Max(0, rp - pp2) : 0;
            double? przR = rec?.Prz ?? (rec == null ? przPlan : null), odjR = rec?.Odj ?? (odjPlan is double op ? op + opPrz : null);
            // potwierdzenie czasu: wpis dyżurnego w dzienniku (godzina odjazdu albo przyjazdu), dyspozytor, urządzenie
            // przy kilku przejazdach: przyjęcie – w dzienniku szlaku przyjazdu, wyprawienie – w dzienniku szlaku odjazdu tego przejazdu
            bool Ten(EdrWiersz x) => !wiele || (x.Rodzaj == "przyj" ? z != null && szlakiWiersza[x.Id].Contains(z) : w != null && szlakiWiersza[x.Id].Contains(w));
            var wpisane = wiersze.Any(x => Ten(x) && numsV.Concat(new[] { nrWyj }).Any(n => TenSam(n, x.W.Nr)) && (Jest(x.W.Odj) || Jest(x.W.Prz)));
            string potw = wpisane ? "czlowiek" : potwDysp.Contains(klucz) ? "dyspozytor" : rec?.Prz != null || rec?.Odj != null ? "auto" : "";
            var wp = new EdrPociag
            {
                Klucz = klucz, Nr = nr, NrWyj = nrWyj, Rodz = q.KatIr11 ?? "", Nazwa = q.Name ?? "",
                ZSzlak = z, ZKierunku = z != null ? SasiadSzlaku(post, z) : start ? "tor " + tor : "", WSzlak = w, WKierunku = w != null ? SasiadSzlaku(post, w) : "",
                Linia = new[] { z, w }.Where(x => x != null && S.L.Blocks.ContainsKey(x)).Select(x => S.L.Blocks[x].Line).FirstOrDefault(x => !string.IsNullOrEmpty(x)) ?? "",
                Tor = tor, PT = PeronTor(wiele ? q.Przebieg[wz.Od].Peron : q.PlanPeron?.GetValueOrDefault(post), tor), TorRzecz = rec?.Tor ?? "", PrzPlan = Hm(przPlan), PrzRzecz = Hm(przR), PrzOdch = Odch(przPlan, przR),
                OdjPlan = Hm(odjPlan), OdjRzecz = Hm(odjR), OdjOdch = Odch(odjPlan, odjR), Postoj = ph ? "ph" : "", Stan = stan, NaStacji = stan == "na stacji",
                PZam = ph ? Minuty(przPlan, odjPlan) is var pz && pz != "" ? pz : "0,0" : "0,0", PObl = Minuty(rec?.Prz, rec?.Odj) is var pb && pb != "" ? pb : "0,0",
                Wstega = wstega, Potw = potw, Kursuje = kursuje.TryGetValue(klucz, out var ku) ? ku : !q.NieKursuje,
                StacjaPocz = q.Origin ?? "", StacjaKonc = q.Destination ?? "", Przewoznik = q.Przewoznik ?? "", Kod = q.Cat ?? "", Pojazd = q.Veh ?? "", Dlugosc = q.Len is double ln ? $"{ln:0} m" : "",
                UwagiEksp = uwagiEksp.GetValueOrDefault(klucz) ?? q.UwagiEksp ?? "", UwagiPrzew = uwagiPrzew.GetValueOrDefault(klucz) ?? q.UwagiPrzew ?? "", UwagiWlasne = uwagiWlasne.GetValueOrDefault(post + "|" + klucz) ?? "",
                PrzPlanD = HmD(przPlan), PrzRzeczD = HmD(przR), OdjPlanD = HmD(odjPlan), OdjRzeczD = HmD(odjR), Trasa = TrasaPociagu(q, nr),
                Przyjety = wiersze.Any(x => x.Rodzaj == "przyj" && Ten(x) && numsV.Concat(new[] { nrWyj }).Any(n => TenSam(n, x.W.Nr))),
                Wyprawiony = wiersze.Any(x => x.Rodzaj == "wypr" && Ten(x) && numsV.Concat(new[] { nrWyj }).Any(n => TenSam(n, x.W.Nr))),
                Sort = przPlan ?? odjPlan ?? rec?.Prz ?? q.T, Przejazd = vi + 1
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
                odj.Potw = wiersze.Any(x => Ten(x) && TenSam(nrWyj, x.W.Nr) && (Jest(x.W.Odj) || Jest(x.W.Prz))) ? "czlowiek" : potwDysp.Contains(odj.Klucz) ? "dyspozytor" : rec?.Odj != null ? "auto" : "";
                wp.Potw = wiersze.Any(x => Ten(x) && TenSam(nr, x.W.Nr) && (Jest(x.W.Odj) || Jest(x.W.Prz))) ? "czlowiek" : potwDysp.Contains(klucz) ? "dyspozytor" : rec?.Prz != null ? "auto" : "";
                // relacja i nazwa osobno: pociąg przyjeżdżający kończy bieg na tym posterunku, odjeżdżający tu zaczyna (rozkład skleja obieg jednostki)
                var naz = (q.Name ?? "").Split(" / ");
                wp.StacjaKonc = NazwaPosterunku(post); odj.StacjaPocz = NazwaPosterunku(post);
                if (naz.Length == 2) { wp.Nazwa = naz[0]; odj.Nazwa = naz[1]; }
                odj.Trasa = TrasaPociagu(q, nrWyj);
                NalozLokalne(post, odj);
                res.Add(odj);
            }
            NalozLokalne(post, wp);
            res.Add(wp);
            }
        }
        res.AddRange(WykazLokalny(post).Select(x => { NalozLokalne(post, x); return x; }));
        return res.OrderBy(x => x.Sort).ToList();
    }

    static bool Jest(string godz) => !string.IsNullOrEmpty(godz) && godz != "---";
    /// <summary>Wiersz liczy się do bieżącego ruchu: nie wykreślony i nie z wcześniejszego uruchomienia (plik dzienników).</summary>
    static bool Biezacy(EdrWiersz w) => !w.Wykreslony && !w.Wczytany;

    // tor szlakowy z uwag (E8d, jak w EDR.exe): „po torze lewym / prawym”, „po (zamkniętym) torze nr N”
    static readonly System.Text.RegularExpressions.Regex UwLewy = new(@"\bpo\s+(?:torze\s+)?lewym\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant),
        UwPrawy = new(@"\bpo\s+(?:torze\s+)?prawym\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant),
        UwNumer = new(@"\bpo\s+(?:zamkniętym\s+)?torze\s+(?:zamkniętym\s+)?(?:nr\.?|numer)\s*\[?\s*(\w+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>Tor szlakowy wskazany w uwagach: numery toru, „po torze lewym”, „po torze prawym”.</summary>
    public static (List<string> Numery, bool Lewy, bool Prawy) TorZUwag(string uwagi)
    {
        uwagi ??= "";
        return (UwNumer.Matches(uwagi).Select(m => m.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList(), UwLewy.IsMatch(uwagi), UwPrawy.IsMatch(uwagi));
    }

    /// <summary>
    /// Sprawdzenie toru szlakowego z uwag wiersza przyjęcia / wyprawienia (null – poprawny): jeden numer, należy do szlaku,
    /// nie jednocześnie lewy i prawy, numer zgodny z lewym / prawym dla kierunku jazdy (lewy – tor o zasadniczym kierunku przeciwnym).
    /// </summary>
    public static string SprawdzTorZUwag(EdrDziennik d, string rodzaj, EdrWersja w)
    {
        if (rodzaj is not ("przyj" or "wypr") || w == null) return null;
        var (nr, lewyT, prawy) = TorZUwag(w.Uwagi);
        bool lewy = w.Lewy || lewyT;
        if (nr.Count > 1) return "Uwagi wskazują różne numery torów szlakowych – podaj jeden tor dla tego przejazdu";
        if (lewy && prawy) return "Uwagi wskazują jednocześnie tor lewy i prawy – popraw opis przejazdu";
        if (nr.Count == 0) return null;
        var t = d.Biezace.Tory.FirstOrDefault(x => string.Equals(x.Tor, nr[0], StringComparison.OrdinalIgnoreCase));
        if (t == null) return $"Tor szlakowy nr {nr[0]} nie należy do szlaku {d.Nazwa} (tory: {string.Join(", ", d.Biezace.Tory.Select(x => x.Tor))})";
        int kier = rodzaj == "przyj" ? -1 : 1;
        if (lewy && t.Zasadniczy == kier) return $"Tor nr {t.Tor} jest dla tego kierunku torem zasadniczym (prawym), a uwagi mówią „po torze lewym”";
        if (prawy && t.Zasadniczy == -kier) return $"Tor nr {t.Tor} jest dla tego kierunku torem lewym, a uwagi mówią „po torze prawym”";
        return null;
    }

    /// <summary>
    /// „Pociąg przyjechał” nie wcześniej niż „pociąg odjechał” (null – poprawnie). Przejście przez północ: odjazd od 18:00, przyjazd przed 6:00 –
    /// przyjazd następnej doby (E8d, jak w EDR.exe). Przy dwóch godzinach w komórce liczy się pierwsza.
    /// </summary>
    public static string SprawdzGodziny(EdrWersja w)
    {
        static string Pierwsza(string g) => (g ?? "").Split('/')[0].Trim();
        string odj = Pierwsza(w?.Odj), prz = Pierwsza(w?.Prz);
        if (!Jest(odj) || !Jest(prz) || string.CompareOrdinal(prz, odj) >= 0) return null;
        if (string.CompareOrdinal(odj, "18:00") >= 0 && string.CompareOrdinal(prz, "06:00") < 0) return null;
        return $"„Pociąg przyjechał” ({prz}) nie może być wcześniej niż „pociąg odjechał” ({odj}); przejście przez północ – odjazd od 18:00 i przyjazd przed 6:00";
    }

    /// <summary>
    /// Stan torów szlakowych dziennika z wpisów: pociąg z wypełnionym „odjechał” i pustym „przyjechał” zajmuje tor – przy przyjęciu tor
    /// z zasadniczym kierunkiem do nas, przy wyprawieniu – od nas, „po torze lewym” – drugi; tor dwukierunkowy – w obu kierunkach.
    /// </summary>
    public void StanSzlaku(EdrDziennik d)
    {
        PrzeliczBiezace(d);
        var ts = d.Biezace.Tory.OrderBy(t => t.Strona == "lewy" ? 0 : t.Strona == "prawy" ? 2 : 1).ThenBy(t => t.Tor, Comparer<string>.Create(PorownajTory));
        d.ToryStan = ts.Select(t => new EdrTorSzlakowy { Szlak = t.Szlak, Tor = t.Tor, Kierunek = t.Zasadniczy, Strona = t.Strona, Status = t.Status, Trakcja = t.Trakcja }).ToList();
        if (d.ToryStan.Count == 0) return;
        foreach (var w in d.Strony.SelectMany(s => s.Wiersze).Where(w => w.Rodzaj is "przyj" or "wypr" && Biezacy(w)))
        {
            var v = w.W;
            // jazda do km i z powrotem: tor zajęty od odjazdu do wpisu przyjazdu w wierszu powrotu
            var powrot = w.Pierwszy ? d.Strony.SelectMany(s => s.Wiersze).FirstOrDefault(x => x.Id == w.Para) : null;
            if (!Jest(v.Odj) || (powrot != null ? Jest(powrot.W.Prz) || powrot.W.Prz == "---" : Jest(v.Prz) || v.Prz == "---")) continue;
            // tor wskazany w uwagach („po torze nr N”, „po torze lewym”) ma pierwszeństwo przed kierunkiem zasadniczym (E8d)
            var (nrUw, lewyUw, _) = TorZUwag(v.Uwagi);
            int kier = w.Rodzaj == "przyj" ? -1 : 1, szuk = v.Lewy || lewyUw ? -kier : kier;
            // tor o zasadniczym kierunku jazdy pociągu; bez ustalonego kierunku – przyjęcie po torze lewym, wyprawienie po prawym (ruch prawostronny)
            var t = (nrUw.Count == 1 ? d.ToryStan.FirstOrDefault(x => string.Equals(x.Tor, nrUw[0], StringComparison.OrdinalIgnoreCase)) : null)
                ?? d.ToryStan.FirstOrDefault(x => x.Kierunek == szuk) ?? d.ToryStan.FirstOrDefault(x => x.Kierunek == 0 && x.Strona == (szuk < 0 ? "lewy" : "prawy"))
                ?? d.ToryStan.FirstOrDefault(x => x.Kierunek == 0) ?? d.ToryStan[0];
            t.Pociagi.Add(v.Nr); t.Ruch = kier;
        }
    }

    /// <summary>Ręczne zmiany zajętości torów stacyjnych (kontrolka) – bieżąca sesja.</summary>
    public readonly List<EdrZajetoscReczna> ZajetosciReczne = new();
    int nastepnaReczna = 1;

    /// <summary>
    /// Zajętość torów stacyjnych posterunku z wpisów jego dzienników i ręcznych zmian dyżurnego, w kolejności zdarzeń (jak w EDR.exe):
    /// przyjęcie z torem – rezerwacja, z „przyjechał” – zajęcie; pociąg postawiony ręcznie – zajęcie (do nowego odjazdu tego pociągu); wyprawienie
    /// z „odjechał” – zwolnienie toru przez ten pociąg (także rezerwacji) i przez pociągi z wpisów na torze wyprawienia.
    /// </summary>
    public List<EdrTorStacyjny> ZajetoscTorowStacyjnych(string post)
    {
        var tory = (ToryStacyjne.GetValueOrDefault(post) ?? new()).Select(t => new EdrTorStacyjny { Tor = t }).ToList();
        EdrTorStacyjny Tor(string nr) { var t = tory.FirstOrDefault(x => x.Tor == nr); if (t == null) tory.Add(t = new EdrTorStacyjny { Tor = nr }); return t; }
        var reczne = ZajetosciReczne.Where(x => x.Post == post).ToList();
        var zdarzenia = Dzienniki.Where(d => d.Post == post).SelectMany(d => d.Strony.SelectMany(s => s.Wiersze)).Where(w => w.Rodzaj is "przyj" or "wypr" or "powrot" && Biezacy(w))
            .Select(w => (Klucz: (double)w.Id, W: w, R: (EdrZajetoscReczna)null))
            .Concat(reczne.Where(x => x.WierszId == 0 && !x.Usuniety).Select(x => (Klucz: double.MinValue, W: (EdrWiersz)null, R: x)))
            .OrderBy(x => x.Klucz);
        foreach (var (_, w, r) in zdarzenia)
        {
            if (r != null) { Tor(r.Tor).Pociagi.Add(new EdrNaTorze { Klucz = "r:" + r.Id, Nr = r.Nr, Od = r.Dodano, Recznie = true }); continue; }
            var v = w.W;
            if (w.Rodzaj is "przyj" or "powrot" && v.Tor != "" && v.Prz != "---")
            {
                var zm = reczne.LastOrDefault(x => x.WierszId == w.Id);
                if (zm?.Usuniety == true) continue;
                Tor(zm?.Tor ?? v.Tor).Pociagi.Add(new EdrNaTorze { Klucz = "w:" + w.Id, Nr = v.Nr, Od = Jest(v.Prz) ? v.Prz : "", Zarezerwowany = !Jest(v.Prz) });
            }
            else if (w.Rodzaj == "wypr" && Jest(v.Odj))
                foreach (var t in tory)
                    t.Pociagi.RemoveAll(x => x.Recznie ? TenSam(x.Nr, v.Nr) && !reczne.First(z => "r:" + z.Id == x.Klucz).Znane.Contains(w.Id)
                        : TenSam(x.Nr, v.Nr) || v.Tor != "" && t.Tor == v.Tor && !x.Zarezerwowany);
        }
        foreach (var t in tory) { var z = t.Pociagi.FirstOrDefault(x => !x.Zarezerwowany); t.Pociag = z?.Nr; t.Od = z?.Od; }
        return tory.OrderBy(x => x.Tor, Comparer<string>.Create(PorownajTory)).ToList();
    }

    /// <summary>
    /// Ręczna zmiana zajętości toru stacyjnego (polecenie „torst”, kontrolka jak w EDR.exe): „dodaj” – pociąg <c>Dane.Nr</c> na tor <c>Wartosc</c>,
    /// „przesun” – pociąg <c>Klucz</c> na tor <c>Wartosc</c>, „usun” – pociąg <c>Klucz</c> z toru.
    /// </summary>
    CmdResult TorStacyjny(Desk k, EdrPolecenie p)
    {
        var post = p.Post;
        if (!JestPost(post)) return new(false, "Nie ma takiego posterunku");
        if (!MojPost(post, k)) return new(false, $"Posterunek {NazwaPosterunku(post)} prowadzi inne stanowisko");
        var user = Kto(k);
        if (user == null) return new(false, "Brak zalogowanego użytkownika");
        var tory = ZajetoscTorowStacyjnych(post);
        var cel = (p.Wartosc ?? "").Trim();
        var naz = NazwaPosterunku(post);
        if (p.Pole is "dodaj" or "przesun" && !(ToryStacyjne.GetValueOrDefault(post) ?? new()).Contains(cel)) return new(false, $"Tor {cel} nie jest torem stacyjnym posterunku {naz}");
        if (p.Pole == "dodaj")
        {
            var nr = (p.Dane?.Nr ?? "").Trim();
            if (nr == "") return new(false, "Podaj numer pociągu");
            if (nr.Length > 40) return new(false, "Numer pociągu może mieć do 40 znaków");
            if (tory.First(t => t.Tor == cel).Pociagi.Any(x => TenSam(x.Nr, nr))) return new(false, $"Pociąg {nr} jest już na torze {cel}");
            var znane = Dzienniki.Where(d => d.Post == post).SelectMany(d => d.Strony.SelectMany(s => s.Wiersze)).Where(w => w.Rodzaj == "wypr" && Biezacy(w) && TenSam(w.W.Nr, nr) && Jest(w.W.Odj)).Select(w => w.Id).ToList();
            ZajetosciReczne.Add(new EdrZajetoscReczna { Id = nastepnaReczna++, Post = post, Tor = cel, Tor0 = cel, Nr = nr, Znane = znane, Dodano = Teraz(), Data = DataDoby(Doba), Kto = user });
            Wersja++;
            S.Log("Zdarzenie", $"EDR {naz}: pociąg {nr} postawiony na torze {cel} ({user})", "EDR", naz);
            return new(true, $"Pociąg {nr} na torze {cel}");
        }
        var (tz, na) = tory.SelectMany(t => t.Pociagi.Select(x => (t, x))).FirstOrDefault(x => x.x.Klucz == p.Klucz);
        if (na == null) return new(false, "Na torach stacyjnych nie ma takiego pociągu");
        int id = int.Parse(na.Klucz[2..]);
        EdrZajetoscReczna Zmiana()
        {
            if (na.Recznie) return ZajetosciReczne.First(x => x.Id == id);
            var z = ZajetosciReczne.LastOrDefault(x => x.WierszId == id);
            if (z == null) ZajetosciReczne.Add(z = new EdrZajetoscReczna { Id = nastepnaReczna++, Post = post, WierszId = id, Tor = tz.Tor, Nr = na.Nr, Kto = user });
            return z;
        }
        if (p.Pole == "przesun")
        {
            if (cel == tz.Tor) return new(false, $"Pociąg {na.Nr} już jest na torze {cel}");
            var zm = Zmiana(); zm.Tor = cel; zm.Zmiany.Add($"{DataDoby(Doba)} {Teraz()}|{cel}"); Wersja++;
            S.Log("Zdarzenie", $"EDR {naz}: pociąg {na.Nr} przesunięty z toru {tz.Tor} na tor {cel} ({user})", "EDR", naz);
            return new(true, $"Pociąg {na.Nr} przesunięty z toru {tz.Tor} na tor {cel}");
        }
        if (p.Pole == "usun")
        {
            var zu = Zmiana(); zu.Usuniety = true; zu.Zmiany.Add($"{DataDoby(Doba)} {Teraz()}|"); Wersja++;
            S.Log("Zdarzenie", $"EDR {naz}: pociąg {na.Nr} usunięty z toru {tz.Tor} ({user})", "EDR", naz);
            return new(true, $"Pociąg {na.Nr} usunięty z toru {tz.Tor}");
        }
        return new(false, "Nieznana zmiana toru stacyjnego");
    }

    public EdrData DlaStanowiska(Desk k)
    {
        Aktualizuj(k);
        foreach (var d in Dzienniki.Where(x => Moj(x, k))) StanSzlaku(d);
        var user = Kto(k);
        return new EdrData
        {
            Dzienniki = Dzienniki.Where(x => Moj(x, k)).ToList(), ToryStacyjne = ToryStacyjne, Zegar = S.Clock(), Data = DataDoby(Doba), Doba = Doba,
            T = S.T, Uzytkownik = user, Rola = Operator(k)?.Rola ?? k?.User?.Role ?? S.User?.Role,
            Uzytkownicy = Uzytkownicy.Select(u => u.Publiczny()).ToList(), Operator = Operator(k)?.Nazwa, OperatorRola = Operator(k)?.Rola, Obserwator = Obserwator(k),
            Historia = Historia.Skip(Math.Max(0, Historia.Count - 400)).ToList(),
            Lokalne = Lokalne.Where(x => MojPost(x.Key.Split('|')[0], k)).ToDictionary(x => x.Key, x => x.Value),
            Uslugi = Uslugi.Where(x => MojPost(x.Post, k)).ToList(), PociagiLok = PociagiLok.Where(x => MojPost(x.Post, k)).ToList(),
            DyzurOd = dyzurOd.Where(x => MojPost(x.Key, k)).ToDictionary(x => x.Key, x => Hm(x.Value)), Reczne = ZajetosciReczne.Where(x => MojPost(x.Post, k)).ToList(), ZalogowanyOd = k != null && zalogowany.TryGetValue(k.Id, out var z) ? z : null, Wersja = Wersja,
            Posterunki = Posterunki.Where(p => MojPost(p.Kod, k)).ToDictionary(p => p.Kod, p => NazwaPosterunku(p.Kod)),
            Wykaz = Posterunki.Select(p => p.Kod).Where(p => MojPost(p, k)).ToDictionary(p => p, Wykaz),
            ZajetoscTorow = Posterunki.Select(p => p.Kod).Where(p => MojPost(p, k)).ToDictionary(p => p, ZajetoscTorowStacyjnych),
            Telefoniczny = Posterunki.Select(p => p.Kod).Where(p => MojPost(p, k)).ToDictionary(p => p, p => Telefoniczny.GetValueOrDefault(p) ?? new()),
            Numeracja = Posterunki.Select(p => p.Kod).Where(p => MojPost(p, k)).ToDictionary(p => p, p => Numeracja.GetValueOrDefault(p, "dobowa")),
            NastepnyNr = Posterunki.Select(p => p.Kod).Where(p => MojPost(p, k)).ToDictionary(p => p, NastepnyNumer),
            SprawdzeniaUst = Posterunki.Select(p => p.Kod).Where(p => MojPost(p, k)).ToDictionary(p => p, p => Sprawdzenia.GetValueOrDefault(p) ?? new()),
            PosterunkiUst = Posterunki.Select(x => x.Kopia()).ToList(), ZUkladu = posterunkiUst == null, SzlakiUkladu = SzlakiUkladu(), Admin = Admin(k),
            BezWypadku = BezWypadku, BezWypadkuCzas = CzasBezWypadku(), BladKonfiguracji = BladKonfiguracji, BladDziennikow = BladDziennikow,
            Sprawdzenia = StanSprawdzen(k)
        };
    }

    // ------------------------------------------------------------------ polecenia okna
    /// <summary>Polecenie okna EDR; po udanej zmianie – zapis dzienników do pliku (E8d).</summary>
    public CmdResult Wykonaj(Desk k, EdrPolecenie p)
    {
        var r = WykonajPolecenie(k, p);
        if (r.Ok) { ZapiszPolecenie(k, p, r); ZapiszDzienniki(); }
        return r;
    }

    CmdResult WykonajPolecenie(Desk k, EdrPolecenie p)
    {
        if (p == null) return new(false, "Brak polecenia");
        if (PolecenieE9(k, p) is CmdResult r9) return r9;
        if (p.Rodzaj is "wczytajsesje" or "konfpost") return PolecenieSesji(k, p);
        if (p?.Rodzaj == "telefonogram") return Telefonogram(k, p);
        if (p?.Rodzaj == "torst") return TorStacyjny(k, p);
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
            var kto = Kto(k);
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
        var user = Kto(k);
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
                    if (pole == "Data") { v = DataWpisu(v); if (v == null) return new(false, "Data wpisu w postaci dd-MM-rrrr"); }
                    Ustaw(w, pole, v);
                }
                if (w.Nr == "") return new(false, "Wpisz numer pociągu");
                if (SprawdzGodziny(w) is string bg) return new(false, bg);
                if (SprawdzTorZUwag(d, p.Rodzaj, w) is string bt) return new(false, bt);
                string zmiana = "";
                if (d.Parzysty != 0)
                {
                    var nr = Wymus(w.Nr, p.Rodzaj == "wypr" ? d.Parzysty == 1 : d.Parzysty == -1);
                    if (nr != w.Nr) { zmiana = $" (numer dostosowany do kierunku szlaku: {w.Nr} → {nr})"; w.Nr = nr; }
                }
                w.Kto = user; w.T = S.T; w.Zmiana = Znacznik(); w.Pole = null;
                w.Wpis = Godzinowe.Where(g => Jest(Wartosc(w, g))).ToDictionary(g => g, g => Teraz());
                var r = new EdrWiersz { Id = nextId++, Rodzaj = p.Rodzaj, Wymagane = Wymagane(d.Biezace, d.Ustawienia), Wersje = { w } };
                r.Wymagane.AddRange(d.Biezace.Droznicy.Where(x => x.Dotyczy(p.Rodzaj, w.Nr)).Select(x => "Dr:" + x.Id));
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
            case "wykresl":
            {
                // wykreślenie wpisu z przyczyną (E8d, jak w EDR.exe): wpis zostaje, nowa wersja z przyczyną; para „do km i z powrotem” – oba wiersze
                var r = d.Strony.SelectMany(x => x.Wiersze).FirstOrDefault(x => x.Id == p.Wiersz);
                if (r == null) return new(false, "Nie ma takiego wiersza");
                if (r.Rodzaj == "tekst") return new(false, "Wpisów automatycznych (dyżur, północ) nie wykreśla się");
                if (r.Rodzaj == "telefonogram") return new(false, "Telefonogramu nie wykreśla się – sprostowanie kolejnym telefonogramem");
                if (r.Wykreslony) return new(false, "Wpis jest już wykreślony");
                var przyczyna = (p.Wartosc ?? "").Trim();
                if (przyczyna == "") return new(false, "Podaj przyczynę wykreślenia");
                var para = r.Para != 0 ? d.Strony.SelectMany(x => x.Wiersze).FirstOrDefault(x => x.Id == r.Para && !x.Wykreslony) : null;
                foreach (var x in new[] { r, para }.Where(x => x != null))
                {
                    var nw = x.W.Kopia(); nw.Wykreslono = przyczyna; nw.Kto = user; nw.T = S.T; nw.Zmiana = Znacznik(); nw.Pole = "Wykreślenie";
                    x.Wersje.Add(nw);
                }
                Wersja++;
                S.Log("Zdarzenie", $"EDR: wykreślono wpis {(r.Rodzaj == "wpis" ? "" : "pociągu " + r.W.Nr + " ")}w dzienniku {d.Nazwa} – {przyczyna} ({user})", "EDR", d.Nazwa);
                return new(true, $"Wpis {(r.Rodzaj == "wpis" ? "" : "pociągu " + r.W.Nr + " ")}wykreślony: {przyczyna}");
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
                if (r.Wykreslony) return new(false, "Wykreślonego wpisu nie można zmieniać");
                var dr = p.Pole?.StartsWith("Dr:") == true ? d.Biezace.Droznicy.FirstOrDefault(x => x.Id == p.Pole[3..]) : null;
                if (!Pola.Contains(p.Pole) && dr == null) return new(false, "Nie ma takiego pola");
                var v = (p.Wartosc ?? "").Trim();
                if (Godzinowe.Contains(p.Pole) || dr?.Godzina(d.Tory.Count) == true) { v = Godzina(v); if (v == null) return new(false, $"Błędna godzina w polu {NazwaPola(p.Pole)}"); }
                else if (dr != null && v is not ("" or "\\")) return new(false, $"Dróżnik {dr.Oznaczenie}: powiadomienie ukośnikiem „\\” (według szlaku: {dr.WgSzlaku.ToLowerInvariant()})");
                if (dr != null && !dr.Dotyczy(r.Rodzaj, r.W.Nr)) return new(false, $"Dróżnik {dr.Oznaczenie}: powiadomienie nie dotyczy tego pociągu (tylko wyprawienie, dla pociągów: {dr.DlaPociagow.ToLowerInvariant()})");
                if (p.Pole == "Data") { v = DataWpisu(v); if (v == null) return new(false, "Data wpisu w postaci dd-MM-rrrr"); }
                if (p.Pole == "Nr" && v == "") return new(false, "Numer pociągu nie może być pusty");
                if (p.Pole == "Nr" && d.Parzysty != 0) v = Wymus(v, r.Rodzaj == "wypr" ? d.Parzysty == 1 : d.Parzysty == -1);
                var stara = r.W;
                if (Wartosc(stara, p.Pole) == v) return new(true, "");
                var proba = stara.Kopia(); Ustaw(proba, p.Pole, v);
                if (p.Pole is "Odj" or "Prz" && SprawdzGodziny(proba) is string bg2) return new(false, bg2);
                if (p.Pole == "Uwagi" && SprawdzTorZUwag(d, r.Rodzaj, proba) is string bt2) return new(false, bt2);
                // kolejne zmiany tego samego pola przez tę samą osobę w ciągu 10 s (np. kółkiem myszy) – jedna wersja
                var cel = r.Wersje.Count > 1 && stara.Kto == user && stara.Pole == p.Pole && S.T - stara.T < 10 ? stara : null;
                if (cel != null) { Ustaw(stara, p.Pole, v); stara.T = S.T; stara.Zmiana = Znacznik(); }
                else
                {
                    cel = stara.Kopia(); Ustaw(cel, p.Pole, v); cel.Kto = user; cel.T = S.T; cel.Zmiana = Znacznik(); cel.Pole = p.Pole;
                    r.Wersje.Add(cel);
                }
                cel.Wpis ??= new();
                if (Jest(v) && (Godzinowe.Contains(p.Pole) || dr?.Godzina(d.Tory.Count) == true)) cel.Wpis[p.Pole] = Teraz(); else cel.Wpis.Remove(p.Pole);
                // zmiana numeru zmienia parzystość – dróżnicy z filtrem parzystych / nieparzystych (E9a)
                if (p.Pole == "Nr" && r.Rodzaj == "wypr")
                {
                    r.Wymagane.RemoveAll(x => x.StartsWith("Dr:"));
                    r.Wymagane.AddRange(d.Biezace.Droznicy.Where(x => x.Dotyczy(r.Rodzaj, v)).Select(x => "Dr:" + x.Id));
                }
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
        var user = Kto(k);
        if (user == null) return new(false, "Brak zalogowanego użytkownika");
        Aktualizuj(k);
        var n = new EdrTelefonogram
        {
            Rodzaj = t.Rodzaj == "okolicznosc" ? "okolicznosc" : "telefonogram", Nadaje = t.Nadaje, NrOdbiorcy = (t.NrOdbiorcy ?? "").Trim(), Wzor = string.IsNullOrWhiteSpace(t.Wzor) ? "[bez szablonu]" : t.Wzor,
            Tresc = (t.Tresc ?? "").Trim(), Notatka = (t.Notatka ?? "").Trim(), Data = string.IsNullOrWhiteSpace(t.Data) ? DataDoby(Doba) : t.Data.Trim(),
            Dziennik = d?.Id, Post = post, Doba = Doba, Kto = user, Zapisano = Znacznik(), Zmiany = t.Zmiany?.Where(z => z != null).ToList() ?? new()
        };
        // wzór telefonogramu (E8e): treść z pól wzoru, zmiana stanu szlaku ze skutku wzoru (zamiast ręcznej)
        if (n.Rodzaj == "telefonogram" && EdrWzory.Wzor(t.Wzor) is EdrWzor wz)
        {
            string tu0 = NazwaPosterunku(post), tam0 = d?.Sasiad ?? (string.IsNullOrWhiteSpace(t.OdebralPost) ? t.NadalPost : t.OdebralPost) ?? "";
            var (tr, bl) = EdrWzory.Tresc(wz, t.Pola, n.Nadaje ? tu0 : tam0, n.Nadaje ? tam0 : tu0, d?.Biezace.Tory.Select(x => x.Tor).ToList());
            if (bl != null) return new(false, $"Wzór {wz.Nr}: {bl}");
            n.Tresc = tr; n.Wzor = wz.Tytul; n.WzorNr = wz.Nr; n.Pola = new(t.Pola ?? new());
            n.Zmiany = EdrWzory.Skutek(wz, t.Pola, d);
        }
        else if (!string.IsNullOrWhiteSpace(t.Wzor) && t.Wzor != "[bez szablonu]" && n.Rodzaj == "telefonogram") return new(false, $"Nie ma wzoru telefonogramu {t.Wzor}");
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
            PrzeliczBiezace(d);
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
        return t.Rodzaj is "okolicznosc" or "sluzbowy" ? $"Wpisał {t.Nadal} {t.NadalPost} godz. {g}" : $"Nadał {t.Nadal} {t.NadalPost}, odebrał {t.Odebral} {t.OdebralPost} godz. {g}";
    }

    static string SprawdzSprawdzenia(List<EdrSprawdzenie> l)
    {
        if (l == null) return "Brak listy sprawdzeń";
        if (l.Count > 4) return "Najwyżej cztery wymagane sprawdzenia (przyciski w prawym górnym rogu)";
        if (l.Any(x => string.IsNullOrWhiteSpace(x.Nazwa))) return "Sprawdzenie musi mieć nazwę";
        foreach (var x in l)
        {
            var godz = (x.Godziny ?? "").Split(';', ',').Select(g => g.Trim()).Where(g => g != "").ToList();
            if (godz.Any(g => Godzina(g) is not { Length: 5 })) return $"{x.Nazwa}: godziny sprawdzeń w postaci GG:MM; GG:MM";
            if (godz.Count > 12) return $"{x.Nazwa}: najwyżej 12 sprawdzeń na dobę";
            if (x.PoDyzurze is < 1 or > 1440) return $"{x.Nazwa}: sprawdzenie po przejęciu dyżuru – od 1 do 1440 min";
            if (x.Okres is < 0 or > 10080) return $"{x.Nazwa}: okres od 1 min do 7 dni";
            if (godz.Count == 0 && x.PoDyzurze == null && x.Okres == 0) return $"{x.Nazwa}: podaj godziny sprawdzeń albo sprawdzenie po przejęciu dyżuru";
            x.Godziny = string.Join("; ", x.Minuty().Select(m => $"{m / 60:00}:{m % 60:00}"));
        }
        if (l.GroupBy(x => x.Id).Any(g => g.Count() > 1)) return "Powtórzone sprawdzenie";
        return null;
    }

    /// <summary>
    /// Stan sprawdzeń (E9a, jak `DueChecks` w EDR.exe): termin – ostatnia miniona godzina sprawdzenia w dobie albo N minut po przejęciu dyżuru;
    /// bez wykonania po tym terminie – „po_terminie”; następny termin w ciągu 15 min – „wkrotce”. Zapis z E5 (co N min) – od ostatniego wykonania.
    /// </summary>
    public List<EdrSprawdzenieStan> StanSprawdzen(Desk k) => Sprawdzenia.Where(x => MojPost(x.Key, k)).SelectMany(x => x.Value.Where(sp => sp.Aktywne).Select(sp =>
    {
        var w = wykonane.TryGetValue(x.Key + "|" + sp.Id, out var o) ? o : ((double T, string Kto)?)null;
        double polnoc = Math.Floor((S.StartClock + S.T) / 86400) * 86400 - S.StartClock;
        var terminy = new List<double>();
        foreach (var m in sp.Minuty()) foreach (var dz in new[] { -1, 0, 1 }) terminy.Add(polnoc + dz * 86400 + m * 60);
        if (sp.PoDyzurze is int pd && dyzurOd.TryGetValue(x.Key, out var od)) terminy.Add(od + pd * 60);
        if (sp.Okres > 0 && sp.Minuty().Count == 0 && sp.PoDyzurze == null) terminy.Add((w?.T ?? 0) + sp.Okres * 60);
        // terminy przed początkiem symulacji się nie liczą (sprawdzenie o 05:00 przy starcie o 06:00 nie jest zaległe)
        terminy.RemoveAll(t => t < 0);
        double? minal = terminy.Where(t => t <= S.T).Select(t => (double?)t).DefaultIfEmpty(null).Max(), nast = terminy.Where(t => t > S.T).Select(t => (double?)t).DefaultIfEmpty(null).Min();
        bool zalegle = minal != null && (w == null || w.Value.T < minal);
        return new EdrSprawdzenieStan
        {
            Post = x.Key, Id = sp.Id, Nazwa = sp.Nazwa, Okres = sp.Okres, Opis = sp.Opis(), Ostatnie = w != null ? Hm(w.Value.T) : "", Kto = w?.Kto ?? "",
            Nastepne = zalegle ? Hm(minal) : Hm(nast), Stan = zalegle ? "po_terminie" : nast is double n && n - S.T <= 900 ? "wkrotce" : "ok"
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
        "Nr" => "Nr pociągu", "Tor" => "Tor stacyjny", "Dw" => "Droga wolna", "Odj" => "Pociąg odjechał", "Prz" => "Pociąg przyjechał", "Uwagi" => "Uwagi", "Wykreślenie" => "Wykreślenie",
        "Zad" => "Żądanie pozwolenia", "Data" => "Data wpisu", _ => p
    };
}

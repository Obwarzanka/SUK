namespace Lcs.Model;

/// <summary>
/// Zobrazowanie w wersji 2 (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 4): obrazy pulpitu jako samodzielny graf rysunku – węzły, odcinki toru,
/// elementy na odcinkach / w węzłach, opisy przy elementach – i osobno przypięcia do UT. Rysuje się bez UT (§ 0 pkt 2), przypina
/// osobnym krokiem. Plik <c>*.zob.json</c> obok projektu UT z <c>"version": 2</c>; UT jest tylko do odczytu.
/// Współrzędne obrazu w jednostkach „j.” (całkowite, krok siatki 8), 1 j. = 1 piksel monitora przy powiększeniu 1.
/// </summary>
public class ObrProject
{
    public const int Wersja = 2;
    public int Version { get; set; } = Wersja;
    /// <summary>Ścieżka projektu UT względem pliku zobrazowania (poza katalogiem – bezwzględna).</summary>
    public string Ut { get; set; } = "";
    /// <summary>Czas zapisu pliku UT, z którym obraz był ostatnio zapisany – przy zmianie UT edytor ostrzega.</summary>
    public DateTime? UtZapis { get; set; }
    /// <summary>Krok przyciągania [j.] (decyzja właściciela 2026-10-17, § 0 pkt 6).</summary>
    public int Siatka { get; set; } = 8;
    public List<ObrObraz> Obrazy { get; set; } = new();
    /// <summary>
    /// Tablica przebiegów projektowana przez człowieka (ROZWOJ.md etap 3, decyzja właściciela 2026-10-26): symulator czyta tylko te przebiegi.
    /// Null – jeszcze nie projektowano (symulator wyznacza przebiegi z topologii jak dotąd).
    /// </summary>
    public List<ObrPrzebieg> Przebiegi { get; set; }
}

/// <summary>
/// Przebieg w tablicy przebiegów: rodzaj, sygnalizator początkowy, koniec i położenia zwrotnic – nazwy jak w pliku symulatora
/// („MT:A”, „MT:1+ MT:2-”, posortowane); droga, odcinki i ochrona boczna wynikają z układu.
/// </summary>
public class ObrPrzebieg
{
    /// <summary>„P” – pociągowy, „M” – manewrowy.</summary>
    public string Typ { get; set; } = "P";
    public string Od { get; set; } = "";
    public string Do { get; set; } = "";
    public string Zwr { get; set; } = "";
    /// <summary>Przebieg zasadniczy dla tej pary początek – koniec (Ie-104: zasadniczy i wariantowe).</summary>
    public bool Zasadniczy { get; set; }
    /// <summary>Prędkość przebiegu pociągowego ustalona przez projektanta [km/h] (40, 50, 60, 70, 80, 100, 120, 130); 0 – z torów i zwrotnic.</summary>
    public int V { get; set; }

    public string Klucz => $"{Typ} {Od}→{Do} [{Zwr}]";
}

/// <summary>Obraz pulpitu: szczegółowy (posterunek, Ie-104 § 5) albo poglądowy (obszar, § 6).</summary>
public class ObrObraz
{
    public string Id { get; set; } = ObrId.Nowy();
    public string Nazwa { get; set; } = "";
    /// <summary><see cref="ZobObraz.Szczegolowy"/> albo <see cref="ZobObraz.Pogladowy"/> – te same rodzaje co w wersji 1.</summary>
    public string Rodzaj { get; set; } = ZobObraz.Szczegolowy;
    /// <summary>Posterunki z UT pokazane na obrazie (id <see cref="UtPost"/>; Ie-104 § 5 ust. 1).</summary>
    public List<string> Posterunki { get; set; } = new();
    /// <summary>Wymiary obrazu [j.] (decyzja właściciela 2026-10-17: 1920 × 1080).</summary>
    public int Szer { get; set; } = 1920;
    public int Wys { get; set; } = 1080;
    /// <summary>Opisy odcinków na pulpicie – jak <see cref="ZobObraz.OpisyOdcinkow"/>.</summary>
    public string OpisyOdcinkow { get; set; } = ZobObraz.OpisNaZadanie;
    /// <summary>Prowadnice rzędów torów – rzędne y [j.], rosnąco (§ 2: tory w równych rzędach). Tylko w edytorze, nie na pulpicie.</summary>
    public List<int> Rzedy { get; set; } = new();
    /// <summary>Miejsca zarezerwowane przez pulpit (panel poleceń, zegar…) – tory nie powinny na nie wchodzić (§ 2, ramy ekranu).</summary>
    public List<ObrStrefa> Strefy { get; set; } = new();
    public List<ObrWezel> Wezly { get; set; } = new();
    public List<ObrOdcinek> Odcinki { get; set; } = new();
    public List<ObrElement> Elementy { get; set; } = new();
    /// <summary>Przypięcia elementów rysunku do UT – oddzielone od rysunku (§ 4: zmiana rysunku nie gubi przypięć).</summary>
    public List<ObrPrzypiecie> Przypiecia { get; set; } = new();
    /// <summary>Szkic na życzenie (plan § 7, etap E7) – propozycja z UT na osobnej warstwie, nie jest częścią obrazu do „Przyjmij”; null – brak.</summary>
    public ObrGraf.Fragment Szkic { get; set; }

    /// <summary>
    /// Domyślne strefy zarezerwowane (decyzja właściciela 2026-10-22, plan § 11 pkt 7): panel Polecenia / Historia 480 × 160 j.
    /// w prawym górnym rogu (jak w ISDR). Człowiek zmienia je w zakładce Obraz.
    /// </summary>
    public static List<ObrStrefa> StrefyDomyslne(int szer) => new() { new ObrStrefa { Rodzaj = ObrStrefa.Polecenia, Prost = new[] { szer - 480, 0, 480, 160 } } };
}

/// <summary>Prostokąt zarezerwowany przez pulpit [x, y, szer, wys] w j.</summary>
public class ObrStrefa
{
    public string Rodzaj { get; set; } = Polecenia;
    public int[] Prost { get; set; } = { 0, 0, 0, 0 };

    public const string Polecenia = "polecenia", Zegar = "zegar", Inna = "inna";
    public static readonly (string Key, string Name)[] Rodzaje = { (Polecenia, "panel Polecenia / Historia"), (Zegar, "zegar i serwery"), (Inna, "inna") };
    public static string RodzajName(string r) => Rodzaje.FirstOrDefault(x => x.Key == r).Name ?? r;
}

/// <summary>Węzeł grafu rysunku – punkt siatki [j.]; rozjazd = węzeł z trzema odcinkami (§ 3 wariant A).</summary>
public class ObrWezel
{
    public string Id { get; set; } = ObrId.Nowy();
    public int[] P { get; set; } = { 0, 0 };
    /// <summary>
    /// Kawałki torów UT poza obrazem zebrane w tym punkcie (koniec toru przy krawędzi obrazu – np. dalszy ciąg szlaku do drugiej stacji obszaru):
    /// na pulpicie te km są w tym punkcie (jak łamana zerowej długości w obrazie starego edytora). Null – brak.
    /// </summary>
    public List<ZobUtTor> Poza { get; set; }
}

/// <summary>Odcinek toru między dwoma węzłami, z załamaniami <see cref="Pkt"/> po drodze (kierunki 0° / 45° / 90°).</summary>
public class ObrOdcinek
{
    public string Id { get; set; } = ObrId.Nowy();
    public string A { get; set; }
    public string B { get; set; }
    /// <summary>Załamania między węzłami A i B [j.].</summary>
    public List<int[]> Pkt { get; set; } = new();
    public string Styl { get; set; } = Ciagly;

    /// <summary>Ciągły; bez kontroli niezajętości – przerywany (Ie-104 § 8 ust. 1); rozebrany.</summary>
    public const string Ciagly = "ciagly", BezKontroli = "bezKontroli", Rozebrany = "rozebrany";
}

/// <summary>
/// Element rysunku: symbol na odcinku (<see cref="Na"/> = odcinek, <see cref="T"/> 0–1) albo w węźle, albo wolny w punkcie <see cref="P"/>
/// (kolumna PIP, napis). Typy dochodzą w etapach E3 / E6 (§ 10).
/// </summary>
public class ObrElement
{
    public string Id { get; set; } = ObrId.Nowy();
    public string Typ { get; set; } = "";
    /// <summary>Id odcinka albo węzła; null – element wolny w punkcie <see cref="P"/>.</summary>
    public string Na { get; set; }
    /// <summary>Położenie na odcinku – część jego długości od węzła A (przesunięcie węzła przesuwa symbol).</summary>
    public double T { get; set; }
    /// <summary>Kierunek symbolu: +1 – od A do B, −1 – od B do A.</summary>
    public int Kier { get; set; } = 1;
    /// <summary>Punkt elementu wolnego [j.] (peron – lewy górny róg, napis – środek).</summary>
    public int[] P { get; set; }
    /// <summary>Rozmiar [j.]: peron – szerokość, wysokość; przejazd – zasięg linii w górę i w dół od toru.</summary>
    public int[] Rozm { get; set; }
    public string Tekst { get; set; }
    public string Styl { get; set; }
    /// <summary>Liczba pól (rząd odstępów SBL, kolumna PIP).</summary>
    public int? Pola { get; set; }
    /// <summary>Części klocka (koniec szlaku: strzałka, semafor wjazdowy, nazwa sąsiada).</summary>
    public List<string> Czesci { get; set; }
    public List<ObrOpis> Opisy { get; set; } = new();
}

/// <summary>Opis przypięty do elementu (nazwa, numer…) – <see cref="D"/> = przesunięcie względem elementu [j.], jedzie z nim.</summary>
public class ObrOpis
{
    public string Rola { get; set; } = "";
    public string Tekst { get; set; } = "";
    public int[] D { get; set; } = { 0, 0 };
}

/// <summary>
/// Przypięcie elementu rysunku do UT (§ 4, § 6). <see cref="Ut"/> – id obiektu UT, dla odcinka toru – kawałek toru UT
/// (<see cref="ZobUtTor"/>); <see cref="Kandydaci"/> – przy stanie „do wyjaśnienia”.
/// </summary>
public class ObrPrzypiecie
{
    /// <summary>Element rysunku: odcinek toru, węzeł rozjazdu albo element (symbol, opis).</summary>
    public string El { get; set; }
    public string Ut { get; set; }
    /// <summary>Odcinek toru: tor UT, <see cref="ZobUtTor.S0"/> – km w węźle A odcinka, <see cref="ZobUtTor.S1"/> – km w węźle B (może być malejący).</summary>
    public ZobUtTor Tor { get; set; }
    /// <summary>Odcinek toru: długość toru UT [m] przy wyznaczaniu km (F8); inna niż teraz – UT zmieniony, km nieaktualne. Null – plik sprzed 2026-10-26.</summary>
    public double? DlToru { get; set; }
    public string Stan { get; set; } = DoWyjasnienia;
    public List<string> Kandydaci { get; set; }
    /// <summary>Dlaczego „do wyjaśnienia” (np. „2 kandydaci”, „brak w UT”, „kolejność różna niż w UT”).</summary>
    public string Powod { get; set; }

    public const string Pewne = "pewne", Potwierdzone = "potwierdzone", DoWyjasnienia = "doWyjasnienia", Brak = "brak";
}

public static class ObrId
{
    public static string Nowy() => Guid.NewGuid().ToString("N")[..8];
}

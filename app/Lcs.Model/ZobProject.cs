namespace Lcs.Model;

/// <summary>
/// Zobrazowanie obszaru (PLAN_EDYTOR_ZOBRAZOWANIA.md § 2): obrazy pulpitu wg Ie-104 narysowane przez człowieka, każdy element
/// przypięty do obiektu UT przez jego <c>Id</c>. Plik <c>*.zob.json</c> obok projektu UT; UT jest tylko do odczytu.
/// Współrzędne obrazu w jednostkach „j.” (całkowite, krok siatki 8), 1 j. = 1 piksel monitora przy powiększeniu 1.
/// </summary>
public class ZobProject
{
    public int Version { get; set; } = 1;
    /// <summary>Ścieżka projektu UT względem pliku zobrazowania (poza katalogiem – bezwzględna).</summary>
    public string Ut { get; set; } = "";
    /// <summary>Czas zapisu pliku UT, z którym obraz był ostatnio zapisany – przy zmianie UT edytor ostrzega.</summary>
    public DateTime? UtZapis { get; set; }
    /// <summary>Krok przyciągania [j.].</summary>
    public int Siatka { get; set; } = 8;
    public List<ZobObraz> Obrazy { get; set; } = new();
}

/// <summary>Obraz pulpitu: szczegółowy (posterunek, Ie-104 § 5) albo poglądowy (obszar, § 6).</summary>
public class ZobObraz
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Nazwa { get; set; } = "";
    public string Rodzaj { get; set; } = Szczegolowy;
    /// <summary>Posterunki z UT pokazane na obrazie (id <see cref="UtPost"/>; Ie-104 § 5 ust. 1).</summary>
    public List<string> Posterunki { get; set; } = new();
    /// <summary>Wymiary obrazu [j.] (decyzja właściciela 2026-10-17: 1920 × 1080).</summary>
    public int Szer { get; set; } = 1920;
    public int Wys { get; set; } = 1080;

    public List<ZobTor> Tory { get; set; } = new();
    public List<ZobSymbol> Zwrotnice { get; set; } = new();
    public List<ZobSymbol> Sygnalizatory { get; set; } = new();
    public List<ZobSymbol> Wykolejnice { get; set; } = new();
    public List<ZobSymbol> Kozly { get; set; } = new();
    public List<ZobPrzerwa> Przerwy { get; set; } = new();
    public List<ZobOpis> Odcinki { get; set; } = new();
    public List<ZobSymbol> NumeryTorow { get; set; } = new();
    public List<ZobPeron> Perony { get; set; } = new();
    public List<ZobSymbol> Przejazdy { get; set; } = new();
    public List<ZobSymbol> Szlaki { get; set; } = new();
    public List<ZobNapis> Napisy { get; set; } = new();

    public const string Szczegolowy = "szczegolowy", Pogladowy = "pogladowy";
    public static readonly (string Key, string Name)[] Rodzaje = { (Szczegolowy, "szczegółowy (posterunek)"), (Pogladowy, "poglądowy (obszar)") };
    public static string RodzajName(string r) => Rodzaje.FirstOrDefault(x => x.Key == r).Name ?? r;
}

/// <summary>Kawałek toru UT (tor, km od–do) na obrazie.</summary>
public class ZobUtTor
{
    public string Tor { get; set; }
    public double S0 { get; set; }
    public double S1 { get; set; }
}

/// <summary>Tor obrazu: łamana [j.] przypięta do kawałka toru UT (nie łączy dwóch torów UT).</summary>
public class ZobTor
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public ZobUtTor Ut { get; set; } = new();
    public List<int[]> Pkt { get; set; } = new();
}

/// <summary>Symbol w punkcie obrazu przypięty do obiektu UT (zwrotnica, sygnalizator, wykolejnica, kozioł, numer toru, przejazd, szlak).</summary>
public class ZobSymbol
{
    public string Ut { get; set; }
    public int[] P { get; set; }
    /// <summary>Strona opisu (−1 / +1); 0 – domyślna.</summary>
    public int Strona { get; set; }
    /// <summary>Miejsce opisu [j.]; null – domyślne.</summary>
    public int[] Opis { get; set; }
}

/// <summary>Przerwa izolacyjna na torze obrazu (licznik osi / złącze UT).</summary>
public class ZobPrzerwa
{
    public string Ut { get; set; }
    /// <summary>Id toru obrazu (<see cref="ZobTor.Id"/>).</summary>
    public string Tor { get; set; }
    public int[] P { get; set; }
}

/// <summary>Opis odcinka kontroli niezajętości.</summary>
public class ZobOpis
{
    public string Ut { get; set; }
    public int[] Opis { get; set; }
}

/// <summary>Peron – prostokąt [x, y, szer, wys] w j.</summary>
public class ZobPeron
{
    public string Ut { get; set; }
    public int[] Prost { get; set; }
}

/// <summary>Napis bez znaczenia ruchowego (nazwa posterunku, „Peron 3”).</summary>
public class ZobNapis
{
    public string Tekst { get; set; } = "";
    public int[] P { get; set; }
    public string Rodzaj { get; set; } = "";
}

namespace Lcs.Model;

/// <summary>
/// Projekt układu torowego (UT): rzeczywista geometria w PUWG 1992 [m], wysokości [m].
/// Zapis w pliku <c>*.ut.json</c>. Kolejne elementy (podkłady, tory, rozjazdy, urządzenia) dochodzą w etapach
/// opisanych w PLAN_EDYTOR_UT.md.
/// </summary>
public class UtProject
{
    /// <summary>Wersja formatu pliku.</summary>
    public int Version { get; set; } = 1;
    /// <summary>Nazwa obszaru (stacja, węzeł, odcinek linii) – wpisuje projektant.</summary>
    public string Name { get; set; } = "Nowy obszar";
    /// <summary>Układ współrzędnych – zawsze PUWG 1992.</summary>
    public string Crs { get; set; } = "EPSG:2180";
    /// <summary>Skala rysunku 1:N – wielkość symboli Ig-10 (mm arkusza) i wydruku. Geometria od niej nie zależy.</summary>
    public int DrawingScale { get; set; } = 1000;
    /// <summary>Uwagi projektanta.</summary>
    public string Notes { get; set; } = "";
    /// <summary>Podkłady od dołu do góry (ortofotomapy, grafiki).</summary>
    public List<UtBackground> Backgrounds { get; set; } = new();
    /// <summary>Tory (osie torów – wieloboki styczne z łukami).</summary>
    public List<UtTrack> Tracks { get; set; } = new();
    /// <summary>Ostatni widok edytora (nie podlega cofaniu).</summary>
    public UtViewState View { get; set; } = new();

    public static readonly int[] DrawingScales = { 500, 1000, 2000 };
}

/// <summary>Widok edytora: środek ekranu w PUWG 1992 i rozdzielczość [m na piksel ekranu].</summary>
public class UtViewState
{
    public double X { get; set; } = 470000;
    public double Y { get; set; } = 500000;
    public double Mpp { get; set; } = 1000;
}

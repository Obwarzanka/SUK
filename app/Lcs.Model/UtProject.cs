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
    /// <summary>Rozjazdy zwyczajne (na torach, po kilometrażu).</summary>
    public List<UtTurnout> Turnouts { get; set; } = new();
    /// <summary>Skrzyżowania torów i rozjazdy krzyżowe.</summary>
    public List<UtCrossing> Crossings { get; set; } = new();
    /// <summary>
    /// Oś schematu: azymut [°] kierunku, który w widoku Schemat biegnie w prawo (układ „do poziomu”); null – kierunek najdłuższego toru.
    /// Tylko widok – geometria się nie zmienia.
    /// </summary>
    public double? AxisAz { get; set; }
    /// <summary>Ostatni widok edytora (nie podlega cofaniu).</summary>
    public UtViewState View { get; set; } = new();

    public static readonly int[] DrawingScales = { 500, 1000, 2000 };

    /// <summary>Oś schematu [rad]: wpisana albo cięciwa najdłuższego toru (zwrócona na wschód); bez torów – wschód.</summary>
    public double SchematAz()
    {
        if (AxisAz is double a) return a * Math.PI / 180;
        UtTrack best = null; double bl = 0;
        foreach (var t in Tracks)
        {
            if (t.Vertices.Count < 2) continue;
            double l = XY.Dist(t.Vertices[0].P, t.Vertices[^1].P);
            if (l > bl) { bl = l; best = t; }
        }
        if (best == null) return Math.PI / 2;
        return EastFacing(Trasa.Az(best.Vertices[0].P, best.Vertices[^1].P));
    }

    /// <summary>Kierunek zwrócony na wschód (składowa Y ≥ 0), w przedziale [0, π).</summary>
    public static double EastFacing(double az)
    {
        az = Math.IEEERemainder(az, 2 * Math.PI); if (az < 0) az += 2 * Math.PI;
        return az >= Math.PI ? az - Math.PI : az;
    }
}

/// <summary>Widok edytora: środek ekranu w PUWG 1992 i rozdzielczość [m na piksel ekranu].</summary>
public class UtViewState
{
    public double X { get; set; } = 470000;
    public double Y { get; set; } = 500000;
    public double Mpp { get; set; } = 1000;
    /// <summary>Widok Schemat (oś poziomo, rozciągnięcie w poprzek) – false: Teren (północ u góry, 1:1).</summary>
    public bool Schemat { get; set; } = true;
    /// <summary>Rozciągnięcie w poprzek osi w widoku Schemat (1, 2, 4).</summary>
    public double Stretch { get; set; } = 4;
    /// <summary>Podkłady widoczne w widoku Schemat.</summary>
    public bool ShowBg { get; set; }
}

namespace Lcs.Model;

/// <summary>
/// Podkład edytora: ortofotomapa pobrana z Geoportalu (siatka kafelków w PUWG 1992) albo grafika z pliku
/// (z plikiem world albo skalibrowana punktami). Lista w projekcie od dołu do góry.
/// Podkład jest tylko tłem do rysowania – nie trafia do symulatora.
/// </summary>
public class UtBackground
{
    public const string Orto = "orto", Image = "grafika";

    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    /// <summary><see cref="Orto"/> albo <see cref="Image"/>.</summary>
    public string Kind { get; set; } = Image;
    public bool Visible { get; set; } = true;
    /// <summary>Krycie 0–1.</summary>
    public double Opacity { get; set; } = 1;
    /// <summary>Źródło i prawa (np. „Ortofotomapa © GUGiK, geoportal.gov.pl”).</summary>
    public string Source { get; set; } = "";
    public List<UtTile> Tiles { get; set; } = new();
    /// <summary>Ortofotomapa: siatka kafelków (stała siatka <see cref="UtGrid.Global"/> – kolejne pobrania tej rozdzielczości dokładają się do jednej mozaiki).</summary>
    public UtGrid Grid { get; set; }
    /// <summary>Ortofotomapa w stałej siatce: kafelki zamówione (pobrane i nie) – brakujące można dopobrać.</summary>
    public List<UtCell> Wanted { get; set; }
    /// <summary>Grafika: pary punktów kalibracji (piksel obrazu → miejsce w PUWG 1992).</summary>
    public List<UtCalPoint> Calibration { get; set; }
    /// <summary>Grafika: sposób kalibracji – <c>podobienstwo</c> (przesunięcie, obrót, jedna skala; od 2 punktów) albo <c>afiniczne</c> (od 3 punktów, dwie skale i skos).</summary>
    public string CalMode { get; set; }

    public const string Similarity = "podobienstwo", AffineMode = "afiniczne";
}

/// <summary>Jeden plik obrazu podkładu i jego położenie: piksel (kolumna, wiersz) → PUWG 1992.</summary>
public class UtTile
{
    /// <summary>Ścieżka względem katalogu projektu albo bezwzględna.</summary>
    public string File { get; set; }
    public int W { get; set; }
    public int H { get; set; }
    public Affine Geo { get; set; }
    /// <summary>Ortofotomapa: miejsce w siatce.</summary>
    public int Row { get; set; }
    public int Col { get; set; }
}

/// <summary>Kafelek siatki (wiersz, kolumna).</summary>
public readonly record struct UtCell(int Row, int Col);

/// <summary>
/// Siatka kafelków ortofotomapy: lewy górny róg (X – północ, Y – zachodnia krawędź), rozdzielczość, wymiar kafelka.
/// Stała siatka (<see cref="Global"/>): początek w (0, 0) PUWG 1992 – ta sama dla wszystkich projektów i pobrań danej rozdzielczości,
/// wiersze liczone na południe od X = 0 (dla Polski ujemne), kolumny na wschód od Y = 0. Rows / Cols = 0.
/// Siatka lokalna (pierwsze pobrania, 2026-10-16): początek w rogu obszaru, Rows × Cols.
/// </summary>
public class UtGrid
{
    public static UtGrid Global(double mpp, int tile = 2048) => new() { X0 = 0, Y0 = 0, Mpp = mpp, TileSize = tile };
    public bool IsGlobal => X0 == 0 && Y0 == 0 && Rows == 0 && Cols == 0;
    /// <summary>Bok kafelka [m].</summary>
    public double Side => TileSize * Mpp;
    /// <summary>Kafelek, w którym leży punkt.</summary>
    public UtCell CellAt(XY p) => new((int)Math.Floor((X0 - p.X) / Side), (int)Math.Floor((p.Y - Y0) / Side));
    /// <summary>Kafelki pokrywające prostokąt (dwa dowolne narożniki).</summary>
    public IEnumerable<UtCell> CellsIn(XY a, XY b)
    {
        var c1 = CellAt(new XY(Math.Max(a.X, b.X), Math.Min(a.Y, b.Y)));
        var c2 = CellAt(new XY(Math.Min(a.X, b.X), Math.Max(a.Y, b.Y)));
        for (int r = c1.Row; r <= c2.Row; r++) for (int c = c1.Col; c <= c2.Col; c++) yield return new UtCell(r, c);
    }

    public double X0 { get; set; }
    public double Y0 { get; set; }
    public double Mpp { get; set; }
    public int TileSize { get; set; }
    public int Rows { get; set; }
    public int Cols { get; set; }

    /// <summary>Położenie kafelka (wiersz, kolumna) – lewy górny róg piksela (0, 0).</summary>
    public Affine TileGeo(int row, int col) => Affine.NorthUp(X0 - row * TileSize * Mpp, Y0 + col * TileSize * Mpp, Mpp);
    /// <summary>Granice kafelka w PUWG 1992: (min X, min Y, max X, max Y).</summary>
    public (double MinX, double MinY, double MaxX, double MaxY) TileBox(int row, int col)
    {
        double top = X0 - row * TileSize * Mpp, left = Y0 + col * TileSize * Mpp, s = TileSize * Mpp;
        return (top - s, left, top, left + s);
    }
}

/// <summary>Punkt kalibracji grafiki: piksel obrazu (kolumna, wiersz) i jego miejsce w PUWG 1992.</summary>
public class UtCalPoint
{
    public double Col { get; set; }
    public double Row { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public XY Target => new(X, Y);
}

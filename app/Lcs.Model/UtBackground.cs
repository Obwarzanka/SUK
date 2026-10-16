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
    /// <summary>Ortofotomapa: siatka kafelków (do dopobrania brakujących).</summary>
    public UtGrid Grid { get; set; }
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

/// <summary>Siatka kafelków ortofotomapy: lewy górny róg (X – północ, Y – zachód krawędź), rozdzielczość, wymiar kafelka.</summary>
public class UtGrid
{
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

namespace LcsWidzew.Sim;

/// <summary>Barwa kategorii pociągu (kanały RGB 0–255). Interfejs dobiera własny typ rysowania.</summary>
public readonly record struct RgbColor(byte R, byte G, byte B)
{
    public static RgbColor FromRgb(byte red, byte green, byte blue) => new(red, green, blue);
}

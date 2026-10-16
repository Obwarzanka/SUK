using System.Text.Json;

namespace Lcs.Model;

/// <summary>
/// Baza symboli Ig-10 (zał. 8 – znaki umowne planów schematycznych): plik Dane/symbole_ig10.json, edytowalny przez właściciela.
/// Znak to lista elementów we współrzędnych mm arkusza: x – wzdłuż toru (od punktu zaczepienia w kierunku znaku), y – w stronę „strony”
/// znaku (rozjazd – w stronę odgałęzienia). Rysunek: mm × skala rysunku (z dolną granicą na ekranie).
/// </summary>
public class SymbolDef
{
    public string Id { get; set; }
    public string Name { get; set; }
    /// <summary>Miejsce w Ig-10 (np. „zał. 8, s. 18”).</summary>
    public string Ig10 { get; set; } = "";
    /// <summary>Punkt zaczepienia (opis).</summary>
    public string Anchor { get; set; } = "";
    public List<SymPrim> Prims { get; set; } = new();
}

/// <summary>
/// Element znaku: <c>line</c> (łamana <see cref="Pts"/>, grubość <see cref="W"/> mm), <c>poly</c> (wielokąt; <see cref="Fill"/>:
/// <c>ink</c> – czarny, <c>paper</c> – biały, puste – bez wypełnienia; obrys W, 0 – bez), <c>circle</c> (<see cref="At"/>, <see cref="R"/>),
/// <c>text</c> (<see cref="At"/>, wysokość <see cref="H"/> mm, <see cref="Text"/> – „{nr}” zastępowane numerem, zawsze poziomo).
/// </summary>
public class SymPrim
{
    public string T { get; set; }
    public List<double[]> Pts { get; set; }
    public double W { get; set; } = 0.18;
    public string Fill { get; set; }
    public double[] At { get; set; }
    public double R { get; set; }
    public double H { get; set; } = 1.8;
    public string Text { get; set; }
}

public static class Symbole
{
    public static List<SymbolDef> All { get; private set; } = new();
    public static string Source { get; private set; } = "brak pliku";
    public static SymbolDef Find(string id) => All.FirstOrDefault(s => s.Id == id);

    public static void Load(string file)
    {
        if (!File.Exists(file)) { All = new(); Source = "brak pliku " + file; return; }
        var list = JsonSerializer.Deserialize<List<SymbolDef>>(File.ReadAllText(file), UtDocument.Json) ?? new();
        All = list; Source = file;
    }
}

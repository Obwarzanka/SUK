using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LcsWidzew.SimRail;

/// <summary>
/// Posterunki SimRail w SUK – pulpity zdalne zbudowane z pulpitów gry (<c>tools/simrail/posterunek.py</c>). Osobno od układów SUK z UT
/// (<c>Data/station.json</c>, <c>Data/projekty</c>): tu zależności i topologię ma gra, SUK jest tylko pulpitem.
/// <para>Katalog stacji (jak w posterunek.py): <c>SUK_STACJE</c>, potem <c>stacje</c> paczki wydania (dwa poziomy nad programem),
/// potem <c>&lt;gra&gt;\UserData\SimRailMost\suk\stacje</c>. W nim <c>posterunki.json</c> (rejestr: posterunek gry → kod → układ)
/// i katalog na układ: <c>&lt;układ&gt;\&lt;układ&gt;_zob.json</c> (pulpit), <c>&lt;układ&gt;\edytor\&lt;układ&gt;.zob.json</c> (zobrazowanie).</para>
/// </summary>
public static class SimRailStacje
{
    /// <summary>Układ posterunku SimRail: pulpit SUK, posterunki gry z rejestru (albo z mapy), czy pulpit jest starszy niż zobrazowanie.</summary>
    public sealed record Uklad(string Nazwa, string Pulpit, IReadOnlyList<string> Posterunki, IReadOnlyList<string> Kody, bool Nieaktualny);

    /// <summary>Katalog gry SimRail: SIMRAIL_DIR albo biblioteki Steam (rejestr Windows, libraryfolders.vdf); null – nie znaleziono.</summary>
    public static string KatalogGry()
    {
        var env = Environment.GetEnvironmentVariable("SIMRAIL_DIR");
        if (!string.IsNullOrWhiteSpace(env)) return env;
        var kand = new List<string>();
        try
        {
            foreach (var (klucz, wart) in new[] { (@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath"), (@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath") })
            {
                if (Microsoft.Win32.Registry.GetValue(klucz, wart, null) is not string steam || steam == "") continue;
                kand.Add(Path.Combine(steam, "steamapps", "common", "SimRail"));
                var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdf))
                    foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                        kand.Add(Path.Combine(m.Groups[1].Value.Replace(@"\\", @"\"), "steamapps", "common", "SimRail"));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        kand.Add(@"C:\Program Files (x86)\Steam\steamapps\common\SimRail");
        return kand.FirstOrDefault(k => File.Exists(Path.Combine(k, "SimRail.exe")));
    }

    /// <summary>Katalog stacji SimRail (null – brak).</summary>
    public static string Katalog()
    {
        var env = Environment.GetEnvironmentVariable("SUK_STACJE");
        if (!string.IsNullOrWhiteSpace(env)) return Directory.Exists(env) ? env : null;
        // paczka wydania: <paczka>\programy\SUK\LcsWidzew.exe, stacje w <paczka>\stacje
        var pacz = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "stacje"));
        if (File.Exists(Path.Combine(pacz, "posterunki.json")) || File.Exists(Path.Combine(pacz, "posterunek.cmd"))) return pacz;
        var gra = KatalogGry();
        var k = gra == null ? null : Path.Combine(gra, "UserData", "SimRailMost", "suk", "stacje");
        return k != null && Directory.Exists(k) ? k : null;
    }

    /// <summary>Układy posterunków SimRail w katalogu stacji: katalogi z pulpitem <c>&lt;układ&gt;_zob.json</c>, opis z rejestru.</summary>
    public static List<Uklad> Lista(string katalog)
    {
        var wynik = new List<Uklad>();
        if (katalog == null || !Directory.Exists(katalog)) return wynik;
        var rej = new List<(string Nazwa, string Kod, string Uklad)>();
        try
        {
            var plik = Path.Combine(katalog, "posterunki.json");
            if (File.Exists(plik))
            {
                using var d = JsonDocument.Parse(File.ReadAllText(plik));
                foreach (var p in d.RootElement.GetProperty("posterunki").EnumerateArray())
                    rej.Add((Tekst(p, "nazwaGry"), Tekst(p, "kod"), Tekst(p, "uklad")));
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException or InvalidOperationException or UnauthorizedAccessException) { }
        foreach (var kat in Directory.GetDirectories(katalog).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var u = Path.GetFileName(kat);
            var pulpit = Path.Combine(kat, u + "_zob.json");
            if (!File.Exists(pulpit)) continue;
            var wp = rej.Where(r => string.Equals(r.Uklad, u, StringComparison.OrdinalIgnoreCase)).ToList();
            IReadOnlyList<string> post = wp.Count > 0 ? wp.Select(r => r.Nazwa).ToList() : SprobujMape(pulpit);
            var zob = Path.Combine(kat, "edytor", u + ".zob.json");
            bool stary = File.Exists(zob) && File.GetLastWriteTimeUtc(zob) > File.GetLastWriteTimeUtc(pulpit);
            wynik.Add(new Uklad(u, pulpit, post, wp.Select(r => r.Kod).ToList(), stary));
        }
        return wynik;
    }

    static IReadOnlyList<string> SprobujMape(string pulpit)
    {
        try { return SimRailMapa.Wczytaj(SimRailMapa.PlikDla(pulpit)).Posterunki; } catch (Exception ex) when (ex is IOException or JsonException) { return Array.Empty<string>(); }
    }

    static string Tekst(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}

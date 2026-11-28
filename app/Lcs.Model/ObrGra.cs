using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lcs.Model;

/// <summary>
/// Przypięcia zobrazowania do obiektów gry SimRail (PLAN_POSTERUNKI_SIMRAIL.md, P3; decyzja A – mapa osobnym plikiem):
/// <c>&lt;układ&gt;.simrail.json</c> obok <c>&lt;układ&gt;.zob.json</c> – obraz → element / odcinek rysunku → ID obiektu gry
/// (railNetworkName). Model zobrazowania zostaje ogólny; mapę tworzy <c>pulpit_do_zob.py</c>, przypięcia ręczne zapisuje edytor
/// (zakładka Gra), pulpit SUK buduje z niej <c>zob_do_suk.py</c>. Wartość <see cref="Bez"/> – świadomie bez obiektu gry.
/// Pozostałe pola mapy (posterunek, kod, numery, geom, blokady) zostają bez zmian.
/// </summary>
public sealed class ObrMapaGry
{
    /// <summary>Przypięcie „bez obiektu gry” (świadomie) – konwerter nie szuka wtedy obiektu po numerze ani położeniu.</summary>
    public const string Bez = "-";
    public const string Elementy = "elementy", Odcinki = "odcinki";

    readonly JsonObject root;
    bool kopia;
    public string Plik { get; }
    public string Posterunek => root["posterunek"]?.GetValue<string>() ?? "";
    public string Kod => root["kod"]?.GetValue<string>() ?? "";

    ObrMapaGry(string plik, JsonObject r) { Plik = plik; root = r; }

    /// <summary>Plik mapy dla zobrazowania: „Rogow.zob.json” → „Rogow.simrail.json”.</summary>
    public static string PlikDla(string zob) => zob == null ? null
        : zob.EndsWith(".zob.json", StringComparison.OrdinalIgnoreCase) ? zob[..^".zob.json".Length] + ".simrail.json" : zob + ".simrail.json";

    /// <summary>Mapa obok zobrazowania; null – nie ma (zobrazowanie spoza SimRail).</summary>
    public static ObrMapaGry Wczytaj(string zob)
    {
        var p = PlikDla(zob);
        if (p == null || !File.Exists(p)) return null;
        return JsonNode.Parse(File.ReadAllText(p)) is JsonObject r ? new ObrMapaGry(p, r) : null;
    }

    JsonObject Grupa(string obraz, string rodzaj, bool utworz)
    {
        if (root["obrazy"] is not JsonObject ob) { if (!utworz) return null; root["obrazy"] = ob = new JsonObject(); }
        if (ob[obraz] is not JsonObject o) { if (!utworz) return null; ob[obraz] = o = new JsonObject(); }
        if (o[rodzaj] is not JsonObject g) { if (!utworz) return null; o[rodzaj] = g = new JsonObject(); }
        return g;
    }

    /// <summary>ID gry przypięte w mapie (null – brak, <see cref="Bez"/> – świadomie bez obiektu gry).</summary>
    public string Gra(string obraz, string rodzaj, string id) => Grupa(obraz, rodzaj, false)?[id]?.GetValue<string>();

    /// <summary>Przypięcie ręczne: ID gry, <see cref="Bez"/> albo null (usunięcie – konwerter znów zaproponuje).</summary>
    public void Ustaw(string obraz, string rodzaj, string id, string gra)
    {
        var g = Grupa(obraz, rodzaj, gra != null);
        if (g == null) return;
        if (string.IsNullOrWhiteSpace(gra)) g.Remove(id); else g[id] = gra.Trim();
    }

    /// <summary>Znane ID gry danego rodzaju (wszystkie obrazy): elementy – z numerów (<c>zwr:</c>, <c>syg:</c>, <c>wyk:</c>) i przypięć,
    /// odcinki – obwody / tory z przypięć odcinków. Do wyboru ręcznego.</summary>
    public List<string> Znane(string rodzaj, string prefiksNumeru = null)
    {
        var w = new SortedSet<string>(StringComparer.Ordinal);
        if (root["obrazy"] is not JsonObject ob) return w.ToList();
        foreach (var (_, on) in ob)
        {
            if (on is not JsonObject o) continue;
            if (rodzaj == Elementy && prefiksNumeru != null && o["numery"] is JsonObject nr)
                foreach (var (k, v) in nr)
                    if (k.StartsWith(prefiksNumeru + ":", StringComparison.Ordinal) && v is JsonArray a)
                        foreach (var x in a) if (x?.GetValue<string>() is string s) w.Add(s);
            if (o[rodzaj] is JsonObject g && prefiksNumeru == null)
                foreach (var (_, v) in g) if (v?.GetValue<string>() is string s && s != Bez) w.Add(s);
        }
        return w.ToList();
    }

    public void Zapisz()
    {
        // poprzednia wersja – raz na otwarcie mapy (.bak), jak przy zapisie zobrazowania
        if (!kopia && File.Exists(Plik)) { File.Copy(Plik, Plik + ".bak", true); kopia = true; }
        var tmp = Plik + ".tmp";
        File.WriteAllText(tmp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        File.Move(tmp, Plik, true);
    }

    /// <summary>Rodzaj obiektu gry dla typu elementu (jak RODZ_EL w zob_do_suk.py); null – element bez obiektu gry (napis, peron…).</summary>
    public static string RodzajElementu(string typ) => typ switch
    {
        ObrSymbole.Zwrotnica => "zwr",
        ObrSymbole.Semafor or ObrSymbole.Tarcza or ObrSymbole.Powtarzacz or ObrSymbole.Pk => "syg",
        ObrSymbole.Wykolejnica => "wyk",
        _ => null,
    };
}

/// <summary>
/// Stan przypięć z konwertera (<c>zob_do_suk.py --tylko-przypiecia</c> → <c>&lt;układ&gt;.przypiecia_gry.json</c>): dla każdego
/// elementu i odcinka – ID gry, skąd (mapa, numer, położenie, sąsiedzi, bez) albo brak / niejednoznaczny z kandydatami.
/// Jedno źródło reguł – edytor tylko pokazuje wynik konwertera.
/// </summary>
public sealed class ObrStanGry
{
    public sealed record Poz(string Gra, string Stan, IReadOnlyList<string> Kandydaci);
    public const string Mapa = "mapa", Numer = "numer", Polozenie = "polozenie", Sasiedzi = "sasiedzi", BezObiektu = "bez", Brak = "brak", Niejednoznaczny = "niejednoznaczny";

    public string Plik { get; private init; }
    public DateTime Utworzono { get; private init; }
    public List<string> Nieumieszczone { get; } = new();
    readonly Dictionary<(string Obraz, string Rodzaj, string Id), Poz> poz = new();

    public static string PlikDla(string zob) => zob == null ? null
        : zob.EndsWith(".zob.json", StringComparison.OrdinalIgnoreCase) ? zob[..^".zob.json".Length] + ".przypiecia_gry.json" : zob + ".przypiecia_gry.json";

    public static ObrStanGry Wczytaj(string zob)
    {
        var p = PlikDla(zob);
        if (p == null || !File.Exists(p)) return null;
        if (JsonNode.Parse(File.ReadAllText(p)) is not JsonObject r) return null;
        var s = new ObrStanGry { Plik = p, Utworzono = DateTime.TryParse(r["utworzono"]?.GetValue<string>(), out var t) ? t : File.GetLastWriteTime(p) };
        if (r["obrazy"] is JsonObject ob)
            foreach (var (oid, on) in ob)
                foreach (var rodz in new[] { ObrMapaGry.Elementy, ObrMapaGry.Odcinki })
                    if (on?[rodz] is JsonObject g)
                        foreach (var (id, v) in g)
                            s.poz[(oid, rodz, id)] = new Poz(v?["gra"]?.GetValue<string>(), v?["stan"]?.GetValue<string>() ?? Brak,
                                (v?["kandydaci"] as JsonArray)?.Select(x => x?.GetValue<string>()).Where(x => x != null).ToList() ?? new List<string>());
        if (r["nieumieszczone"] is JsonArray n) s.Nieumieszczone.AddRange(n.Select(x => x?.GetValue<string>()).Where(x => x != null));
        return s;
    }

    public Poz Stan(string obraz, string rodzaj, string id) => poz.TryGetValue((obraz, rodzaj, id), out var p) ? p : null;

    /// <summary>Po przypięciu ręcznym w edytorze – stan tej pozycji bez ponownego uruchamiania konwertera.</summary>
    public void Ustaw(string obraz, string rodzaj, string id, string gra) =>
        poz[(obraz, rodzaj, id)] = gra == null ? new Poz(null, Brak, Stan(obraz, rodzaj, id)?.Kandydaci ?? Array.Empty<string>())
                                 : gra == ObrMapaGry.Bez ? new Poz(null, BezObiektu, Array.Empty<string>()) : new Poz(gra, Mapa, Array.Empty<string>());

    public IEnumerable<(string Rodzaj, string Id, Poz P)> Obrazu(string obraz) =>
        poz.Where(kv => kv.Key.Obraz == obraz).Select(kv => (kv.Key.Rodzaj, kv.Key.Id, kv.Value));

    /// <summary>Do przypięcia: brak albo niejednoznaczny.</summary>
    public static bool DoPrzypiecia(Poz p) => p != null && (p.Stan == Brak || p.Stan == Niejednoznaczny);

    public static string Opis(string stan) => stan switch
    {
        Mapa => "z mapy", Numer => "po numerze z opisu", Polozenie => "po położeniu (leży na odcinku z gry)", Sasiedzi => "po sąsiadach (ten sam obwód po obu końcach)",
        BezObiektu => "świadomie bez obiektu gry", Niejednoznaczny => "do wyjaśnienia – numer niejednoznaczny", Brak => "brak obiektu gry", _ => stan,
    };
}

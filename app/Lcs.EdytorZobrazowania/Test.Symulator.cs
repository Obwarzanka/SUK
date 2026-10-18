using System.IO;
using System.Text.Json.Nodes;
using Lcs.Model;

namespace Lcs.EdytorZobrazowania;

/// <summary>
/// Test „Do symulatora” (etap 9) na mijance z Z3: plik symulatora z UT i obrazu – zwrotnice, sygnalizatory z funkcją, szlaki z odstępami,
/// węzły pulpitu dokładnie w symbolach obrazu, długości torów z km UT, plik Data/station.json bez zmian.
/// </summary>
public static partial class Test
{
    static async Task RunSymulator(MainWindow w)
    {
        await Settle(w);
        var ut = w.Doc.Ut; var o = w.Obraz;
        var dir = Path.Combine(Path.GetTempPath(), "edytor_zob_test", "sym"); Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "mijanka.json");
        var proj = MainWindow.SimProjectsDir();
        var basic = proj != null ? Path.Combine(proj, "..", "station.json") : null;
        var basicHash = basic != null && File.Exists(basic) ? Hash(basic) : null;
        Ok(proj != null && w.SimPathFor() == Path.Combine(proj, "mijanka.json"), $"plik symulatora domyślnie w LcsWidzew/Data/projekty ({w.SimPathFor()})");

        var uw = w.DoSymulatora(path);
        Ok(uw != null && File.Exists(path) && w.LastMessage.StartsWith("Do symulatora: zapisano mijanka.json"), $"Do symulatora – zapisano ({w.LastMessage})");
        var j = JsonNode.Parse(File.ReadAllText(path)).AsObject();
        var sw = j["switches"].AsObject(); var sig = j["signals"].AsObject(); var nodes = j["nodes"].AsObject(); var edges = j["edges"].AsArray();
        Ok(sw.Count == 4 && sw.All(x => x.Key.StartsWith("MJ:") && nodes.ContainsKey((string)x.Value["node"])), $"4 zwrotnice z kodem posterunku ({string.Join(", ", sw.Select(x => x.Key))})");
        Ok(sw.All(x => edges.Count(e => (string)e["a"] == (string)x.Value["node"] || (string)e["b"] == (string)x.Value["node"]) == 3), "w każdym węźle zwrotnicy 3 tory (ostrze, + i −)");
        string Typ(string n) => (string)sig["MJ:" + n]?["type"];
        Ok(Typ("A") == "wjazdowy" && Typ("B") == "wjazdowy" && Typ("F") == "wyjazdowy" && Typ("S60") == "odstepowy", "sygnalizatory: typ z funkcji semafora w UT");
        Ok((string)sig["MJ:A"]["szlak"] == "1A" && (string)sig["MJ:B"]["szlak"] == "2A" && (string)sig["MJ:S60"]["szlak"] == "1A", "wjazdowe – końce szlaków, odstępowy – na szlaku 1A");
        var bl = j["blocks"].AsObject();
        Ok(bl.Count == 2 && bl["1A"]["far"] != null && (int)bl["1A"]["ends"][0]["outward"] == -1 && bl["1A"]["secsWE"].AsArray().Count == 1 && (int)bl["2A"]["aspects"] == 3,
            "szlaki 1A i 2A: koniec na granicy obszaru, wjazdowy A zwrócony od szlaku, jeden odstęp (bez liczników osi), stawność");
        Ok(uw.Any(x => x.StartsWith("odcinek bez nazwy")), "uwaga: odcinki bez nazw i liczników osi w UT tej mijanki");
        var bumps = j["bumpers"].AsObject();
        Ok(bumps.Count == 2, "kozły na końcach toru 2");
        double len1 = edges.Where(e => (string)e["track"] == "1").Sum(e => (double)e["len"]);
        Ok(Math.Abs(len1 - 1200) < 0.5, $"tor 1: suma długości kawałków = km UT ({len1:0.#} m)");
        // pulpit: węzeł semafora i zwrotnicy dokładnie w symbolu obrazu
        var dn = j["display"]["nodes"].AsObject();
        bool At(string key, ZobRodzaj r, string utId) => ZobSymbole.Lista(o, r).FirstOrDefault(s => s.Ut == utId) is ZobSymbol s && dn[key] is JsonArray p && Math.Abs((double)p[0] - s.P[0]) < 0.01 && Math.Abs((double)p[1] - s.P[1]) < 0.01;
        string A = ut.Devices.First(x => x.Name == "A").Id, Z3 = ut.Turnouts.First(x => x.Number == "3").Id;
        Ok(At((string)sig["MJ:A"]["node"], ZobRodzaj.Sygnalizator, A) && At((string)sw["MJ:3"]["node"], ZobRodzaj.Zwrotnica, Z3), "pulpit: węzły semafora A i zwrotnicy 3 w miejscu symboli obrazu");
        Ok(j["display"]["edges"].AsObject().Count == edges.Count && j["pages"].AsArray().Count == w.Doc.P.Obrazy.Count, "pulpit: wszystkie tory na obrazach, strona na każdy obraz");
        Ok((string)j["orient"] == "ab" && j["routeRules"].AsArray().Count == 0, "orientacja A→B, bez reguł przebiegów (projektuje człowiek w symulatorze)");
        Ok(basicHash == null || Hash(basic) == basicHash, "Data/station.json bez zmian");
    }
}

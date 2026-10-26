using System.IO;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Lcs.Model;
using K = System.Windows.Input.Key;

namespace Lcs.EdytorZobrazowania2;

/// <summary>
/// Test etapu E5 (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 10): obraz starego edytora (mijanka_testowa.zob.json, wersja 1) otwiera się w nowym edytorze
/// (przekształcenie do wersji 2), eksport do symulatora daje ten sam station.json co stary edytor, także po F8; podgląd pulpitu kodem symulatora;
/// „Do symulatora…” zapisuje plik i nie rusza Data/station.json; mijanka narysowana od zera (E4) – eksport i podgląd.
/// </summary>
public static partial class Test
{
    /// <summary>Różnice dwóch drzew JSON (ścieżki) – do 10.</summary>
    static List<string> Roznice(JsonNode a, JsonNode b, string path = "$")
    {
        var r = new List<string>();
        void Go(JsonNode x, JsonNode y, string p)
        {
            if (r.Count >= 10) return;
            if (x is JsonObject ox && y is JsonObject oy)
            {
                foreach (var k in ox.Select(kv => kv.Key).Union(oy.Select(kv => kv.Key)))
                    if (!ox.ContainsKey(k) || !oy.ContainsKey(k)) r.Add($"{p}.{k}: tylko w {(ox.ContainsKey(k) ? "starym" : "nowym")}");
                    else Go(ox[k], oy[k], $"{p}.{k}");
            }
            else if (x is JsonArray ax && y is JsonArray ay)
            {
                if (ax.Count != ay.Count) { r.Add($"{p}: {ax.Count} ≠ {ay.Count} pozycji"); return; }
                for (int i = 0; i < ax.Count; i++) Go(ax[i], ay[i], $"{p}[{i}]");
            }
            else if (x?.ToJsonString() != y?.ToJsonString()) r.Add($"{p}: {x?.ToJsonString()} ≠ {y?.ToJsonString()}");
        }
        Go(a, b, path);
        return r;
    }

    static async Task RunE5(MainWindow w, string shot)
    {
        var dir = Path.Combine(Path.GetTempPath(), "edytor_zob2_test_e5"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        var ut = Path.Combine(dir, "mijanka_testowa.ut.json"); var v1 = Path.Combine(dir, "mijanka_testowa.zob.json");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Testy", "mijanka_testowa.ut.json"), ut);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Testy", "mijanka_testowa.zob.json"), v1);
        var stationMain = MainWindow.SimProjectsDir() is string spd ? Path.Combine(spd, "..", "station.json") : null;
        var stationHash = stationMain != null && File.Exists(stationMain) ? Hash(stationMain) : null;

        // --- stary edytor: ten sam plik → station.json
        var stary = ZobDocument.Load(v1);
        var jStary = Lcs.Model.DoSymulatora.Buduj(stary.Ut, stary.P, "test").Json;

        // --- nowy edytor: odczyt wersji 1
        Ok(w.OpenPath(v1) && w.Doc.ZWersji1 != null && w.Doc.P.Version == 2, $"mijanka_testowa.zob.json (wersja 1) otwarta w nowym edytorze ({w.LastMessage})");
        var o = w.Obraz;
        Ok(o.Nazwa == stary.P.Obrazy[0].Nazwa && Rozjazdy(w) == 4 && o.Elementy.Count(e => e.Typ == ObrSymbole.Zwrotnica) == 4, $"obraz „{o.Nazwa}”: 4 rozjazdy z numerami ({Graf(w)})");
        Ok(o.Elementy.Count(e => ObrSymbole.Kierunkowy(e.Typ)) == 10 && o.Elementy.Count(e => e.Typ == ObrSymbole.Koziol) == 2, "10 sygnalizatorów i 2 kozły jako elementy na torach");
        Ok(w.Doc.UwagiKonwersji.Count == 0, $"przekształcenie bez pominięć ({string.Join("; ", w.Doc.UwagiKonwersji)})");
        Ok(o.Przypiecia.All(p => p.Stan is ObrPrzypiecie.Potwierdzone or ObrPrzypiecie.Brak) && ObrSprawdz.DoPrzypiecia(o).All(id => o.Przypiecia.Any(p => p.El == id)),
            "przypięcia z wersji 1 – potwierdzone, każdy element przypięty");
        var a = o.Elementy.FirstOrDefault(e => e.Typ == ObrSymbole.Semafor && e.Opisy[0].Tekst == "A");
        Ok(a?.Styl == ObrSymbole.SemWjazdowy, "semafor A – odmiana wjazdowy (funkcja z UT)");
        var spr = w.Sprawdz();
        Ok(spr.Count(x => x.Blad) == 0, $"Sprawdź bez błędów ({string.Join("; ", spr.Where(x => x.Blad).Select(x => x.Tekst).Take(3))})");

        // --- kompilacja i eksport – ten sam station.json co stary edytor
        var k = w.Kompiluj();
        var jNowy = Lcs.Model.DoSymulatora.Buduj(w.Doc.Ut, k.Zob, "test").Json;
        var roz = Roznice(jStary, jNowy);
        Ok(roz.Count == 0 && k.Uwagi.Count == 0, $"eksport do symulatora identyczny ze starym edytorem ({(roz.Count == 0 ? "bez różnic" : string.Join("; ", roz))})");
        Key(w, K.F8);
        var roz2 = Roznice(jStary, Lcs.Model.DoSymulatora.Buduj(w.Doc.Ut, w.Kompiluj().Zob, "test").Json);
        Ok(w.Obraz.Przypiecia.Count(p => p.Stan == ObrPrzypiecie.DoWyjasnienia) == 0 && roz2.Count == 0, $"po F8: lista pusta, eksport nadal identyczny ({(roz2.Count == 0 ? "bez różnic" : string.Join("; ", roz2))})");

        // --- podgląd pulpitu (kod pulpitu i komputer zależności symulatora)
        var pw = w.Podglad();
        Ok(pw != null && pw.Station != null && pw.Routes.Count > 0, $"Podgląd pulpitu: {w.LastMessage}");
        pw?.Close();

        // --- Do symulatora: plik w katalogu tymczasowym, Data/station.json bez zmian
        var sim = Path.Combine(dir, "mijanka_testowa.json");
        var uw = w.DoSymulatora(sim);
        Ok(uw != null && File.Exists(sim) && Roznice(jStary, JsonNode.Parse(File.ReadAllText(sim))).All(x => x.StartsWith("$.source")), $"Do symulatora: zapisany plik = eksport starego edytora ({w.LastMessage})");
        Ok(w.SimPathFor().EndsWith("mijanka_testowa.json"), $"nazwa pliku symulatora bez „.v2” ({Path.GetFileName(w.SimPathFor())})");
        Ok(stationHash == null || Hash(stationMain) == stationHash, "Data/station.json bez zmian");

        // --- obraz narysowany od zera (E4): zapis i eksport
        var e4 = Path.Combine(Path.GetTempPath(), "edytor_zob2_test_e4", "mijanka.zob.json");
        if (File.Exists(e4))
        {
            Ok(w.OpenPath(e4) && w.Doc.ZWersji1 == null, "obraz z E4 (wersja 2) otwarty");
            var kk = w.Kompiluj();
            var j = Lcs.Model.DoSymulatora.Buduj(w.Doc.Ut, kk.Zob, "test");
            var disp = j.Json["display"]?.AsObject();
            Ok(kk.Uwagi.Count == 0 && kk.Zob.Obrazy[0].Tory.Count >= 6 && kk.Zob.Obrazy[0].Zwrotnice.Count == 4 && disp?["edges"]?.AsObject().Count > 0
                && !j.Uwagi.Any(x => x.Contains("nie ma na żadnym obrazie")), $"kompilacja mijanki z E4: {kk.Zob.Obrazy[0].Tory.Count} torów obrazu, 4 zwrotnice, wszystkie kawałki torów UT na obrazie ({string.Join("; ", j.Uwagi.Take(3))})");
            var pw2 = w.Podglad();
            Ok(pw2 != null && pw2.Routes.Count > 0, $"podgląd pulpitu mijanki z E4: {w.LastMessage}");
            if (shot != null && pw2 != null)
            {
                OffScreen(pw2); pw2.Show(); await Settle(pw2); Save(pw2, Path.ChangeExtension(shot, null) + "_podglad.png");
            }
            pw2?.Close();
        }

        if (shot != null)
        {
            w.OpenPath(v1); w.View.Fit(); await Settle(w); Save(w, shot);
        }
    }
}

using System.IO;
using System.Text.Json.Nodes;
using System.Windows.Controls;
using Lcs.Model;

namespace Lcs.EdytorZobrazowania2;

/// <summary>
/// Zakładka „Gra” (PLAN_POSTERUNKI_SIMRAIL.md, P3): mapa obiektów SimRail obok zobrazowania, stan przypięć z konwertera, przypięcie ręczne,
/// „bez obiektu gry”, usunięcie, znaczniki na płótnie; „Przelicz” przez zob_do_suk.py (gdy jest Python i konwerter).
/// </summary>
public static partial class Test
{
    static async Task RunGra(MainWindow w, string shot)
    {
        var dir = Path.Combine(Path.GetTempPath(), "edytor_zob2_test_gra"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        var ut = Path.Combine(dir, "mijanka_testowa.ut.json"); var zob = Path.Combine(dir, "mijanka_testowa.zob.json");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Testy", "mijanka_testowa.ut.json"), ut);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Testy", "mijanka_testowa.zob.json"), zob);
        Ok(w.OpenPath(zob) && w.SaveDoc(false), "mijanka_testowa jako wersja 2 (bez mapy gry)");
        zob = w.Doc.Path;                 // wersja 2 obok: mijanka_testowa.v2.zob.json
        await Settle(w);
        Ok(!w.Side.Items.Contains(w.TabGra), "zobrazowanie bez mapy gry – bez zakładki Gra (SUK z UT)");

        // mapa gry i stan przypięć jak z pulpit_do_zob.py / zob_do_suk.py: pierwszy odcinek z mapy, drugi – brak, sygnalizator – niejednoznaczny
        var o = w.Obraz;
        var odc = o.Odcinki.Take(2).Select(e => e.Id).ToList();
        var syg = o.Elementy.First(e => ObrMapaGry.RodzajElementu(e.Typ) == "syg");
        File.WriteAllText(ObrMapaGry.PlikDla(zob), new JsonObject
        {
            ["posterunek"] = "Testowo", ["kod"] = "TT",
            ["obrazy"] = new JsonObject { [o.Id] = new JsonObject { ["elementy"] = new JsonObject(), ["odcinki"] = new JsonObject { [odc[0]] = "t100;iz1" },
                                                                    ["numery"] = new JsonObject { ["syg:A"] = new JsonArray("s_a1", "s_a2") } } },
        }.ToJsonString());
        File.WriteAllText(ObrStanGry.PlikDla(zob), new JsonObject
        {
            ["utworzono"] = "2026-11-28T12:00:00",
            ["obrazy"] = new JsonObject { [o.Id] = new JsonObject
            {
                ["elementy"] = new JsonObject { [syg.Id] = new JsonObject { ["gra"] = null, ["stan"] = "niejednoznaczny", ["kandydaci"] = new JsonArray("s_a1", "s_a2") } },
                ["odcinki"] = new JsonObject { [odc[0]] = new JsonObject { ["gra"] = "t100;iz1", ["stan"] = "mapa" },
                                               [odc[1]] = new JsonObject { ["gra"] = null, ["stan"] = "brak", ["kandydaci"] = new JsonArray("t100;iz1") } },
            } },
            ["nieumieszczone"] = new JsonArray("z_usunieta"),
        }.ToJsonString());
        Ok(w.OpenPath(zob), "zobrazowanie z mapą gry otwarte ponownie");
        await Settle(w);
        Ok(w.Side.Items.Contains(w.TabGra) && w.MapaGry?.Kod == "TT" && w.StanGry != null, "zakładka Gra, mapa (kod TT) i stan przypięć wczytane");
        Ok(w.View.GraBrak.Count == 1 && w.View.GraBrakPunkty.Count == 1, $"znaczniki na płótnie: odcinek i element do przypięcia ({w.View.GraBrak.Count}, {w.View.GraBrakPunkty.Count})");
        w.Side.SelectedItem = w.TabGra; await Settle(w);
        var lista = All<Button>(w.GraPanel).Where(b => b.Background == MainWindow.GraListaTlo).ToList();
        Ok(lista.Count == 2 && All<TextBlock>(w.GraPanel).Any(t => t.Text == "• z_usunieta"), $"Do przypięcia: 2 pozycje, obiekt gry bez miejsca na rysunku ({lista.Count})");

        // odcinek z listy → zaznaczony, kandydat z sąsiada w polu ID, Przypnij
        Click(lista.First(b => (b.Content as TextBlock)?.Text.StartsWith("odcinek " + odc[1]) == true)); await Settle(w);
        Ok(w.View.SelE.SetEquals(new[] { odc[1] }) && w.GraIdBox.Text == "t100;iz1", $"klik w liście – odcinek zaznaczony, kandydat „{w.GraIdBox.Text}”");
        Click(Btn(w.GraPanel, "Przypnij")); await Settle(w);
        var m = JsonNode.Parse(File.ReadAllText(ObrMapaGry.PlikDla(zob)));
        Ok(m["obrazy"][o.Id]["odcinki"][odc[1]]?.GetValue<string>() == "t100;iz1" && File.Exists(ObrMapaGry.PlikDla(zob) + ".bak"), "Przypnij – zapis w mapie od razu (poprzednia wersja .bak)");
        Ok(w.View.GraBrak.Count == 0, "znacznik odcinka zniknął");

        // sygnalizator: bez obiektu gry, potem usunięcie przypięcia
        w.View.Select(null, null, new[] { syg.Id }); await Settle(w);
        Ok(w.GraIdBox.Items.Contains("s_a1") && w.GraIdBox.Items.Contains("s_a2"), "sygnalizator: kandydaci po numerze z konwertera na liście");
        Click(Btn(w.GraPanel, "Bez obiektu gry")); await Settle(w);
        m = JsonNode.Parse(File.ReadAllText(ObrMapaGry.PlikDla(zob)));
        Ok(m["obrazy"][o.Id]["elementy"][syg.Id]?.GetValue<string>() == ObrMapaGry.Bez && w.View.GraBrakPunkty.Count == 0, "Bez obiektu gry – „-” w mapie, bez znacznika");
        Click(Btn(w.GraPanel, "Usuń")); await Settle(w);
        m = JsonNode.Parse(File.ReadAllText(ObrMapaGry.PlikDla(zob)));
        Ok(m["obrazy"][o.Id]["elementy"][syg.Id] == null && m["kod"]?.GetValue<string>() == "TT" && m["obrazy"][o.Id]["numery"] != null,
            "Usuń – przypięcie zdjęte z mapy, reszta mapy (kod, numery) bez zmian");
        Ok(w.Doc.P.Obrazy[0].Elementy.Count == o.Elementy.Count && !w.Doc.Dirty, "zakładka Gra nie zmienia zobrazowania (cofnij / zapis bez zmian)");

        // Przelicz – konwerter z repozytorium (zob_do_suk.py --tylko-przypiecia); bez Pythona – pominięte
        var wynik = w.PrzeliczGre();
        if (wynik is "brak konwertera" or "brak Pythona 3 (python / py)") Ok(true, $"Przelicz pominięte ({wynik})");
        else
        {
            var st = w.StanGry;
            Ok(wynik == null && st != null && st.Stan(o.Id, ObrMapaGry.Odcinki, odc[1])?.Gra == "t100;iz1" && st.Stan(o.Id, ObrMapaGry.Odcinki, odc[0])?.Stan == ObrStanGry.Mapa,
                $"Przelicz (zob_do_suk.py): przypięcia z mapy w stanie ({wynik ?? "OK"})");
        }
        if (shot != null) { w.View.Fit(); await Settle(w); Save(w, shot); }
    }
}

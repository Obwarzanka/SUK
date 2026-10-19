using System.Text.Json.Nodes;
using LcsWidzew.Model;
using LcsWidzew.Sim;
using Xunit;

namespace Lcs.Tabor.Testy;

/// <summary>
/// Etap B2 (PLAN_EDYTOR_TABORU.md § 8): pociąg ze składem w symulatorze na kopii mijanki testowej z dopisanym pochyleniem toru 1
/// (cała linia w jednym spadku – od szlaku 1Z ku 1W pod górę przy dodatnim ‰). Pociąg ze szlaku 1Z zatrzymuje się przed semaforem A
/// („Stój”), potem dyżurny nastawia A → C1 i wyjazd C1 na 1W; pomiar czasu od pojawienia się do opuszczenia obszaru.
/// </summary>
public class JazdaTesty
{
    public static string RepoDir() => Repo();
    static string Repo()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "PLAN_EDYTOR_TABORU.md"))) return d.FullName;
        throw new DirectoryNotFoundException("Nie znaleziono katalogu repozytorium");
    }

    /// <summary>Kopia mijanki z pochyleniem <paramref name="poch"/> ‰ na wszystkich torach (od A do B).</summary>
    static string Mijanka(double poch)
    {
        var src = Path.Combine(Repo(), "app", "LcsWidzew", "Data", "projekty", "mijanka_testowa.json");
        var j = JsonNode.Parse(File.ReadAllText(src));
        foreach (var e in j["edges"].AsArray()) e["poch"] = poch;
        var dir = Directory.CreateTempSubdirectory("jazda").FullName;
        var f = Path.Combine(dir, "mijanka_poch.json");
        File.WriteAllText(f, j.ToJsonString());
        return f;
    }

    public record Wynik(double? Opuscil, double? Stanal, double VPrzedA, double IPod, int Spad, List<string> Zdarzenia);

    static Wynik Jedz(double poch, string sklad, double tMax = 1500)
    {
        var st = Station.Load(Mijanka(poch));
        var sim = new Simulator(st); var tr = new Trains(sim);
        sim.User = new User { Name = "auto", Role = "dyżurny ruchu" };
        sim.Page = st.Pages.First().Id;
        sim.Rozklad.Queue = new() { new TimetableEntry { T = 1, Num = "77701", Cat = sklad == "ET22+40xEaos" ? "TOW" : "PR", From = "1Z", To = "1W", Sklad = sklad } };
        double? stanal = null, opuscil = null, ip = 0; double vMax = 0; bool nastawione = false;
        for (int i = 0; i < tMax * 4; i++)
        {
            sim.Tick(0.25);
            // pozwolenie na szlak dla posterunku sąsiedniego (SBL dwukierunkowa – jak automatyczny dyżurny w selftescie)
            if (sim.Blocks["1Z"].Req is { EndIdx: -1 }) sim.Exec("1Z Pzk");
            var t = tr.List.FirstOrDefault(x => x.Num == "77701");
            if (t == null) { if (sim.Rozklad.Queue[0].Done) { opuscil = sim.T; break; } continue; }
            if (!nastawione) vMax = Math.Max(vMax, t.V * 3.6);
            if (!nastawione && t.WaitingAt == "MT:A" && t.V == 0)
            {
                stanal = sim.T; if (t.Fiz != null) ip = t.IPod;
                foreach (var start in new[] { "MT:A", "MT:C1" })
                {
                    var r = sim.RouteTable[start].First(x => x.Type == "P" && (start == "MT:A" ? x.End == "MT:C1" : true));
                    var res = sim.Exec($"{Station.Lbl(r.Start)} {Station.Lbl(r.End)} Pociąg");
                    if (!res.Ok && start == "MT:C1") { sim.Exec($"{r.End} Wbl E"); res = sim.Exec($"{Station.Lbl(r.Start)} {Station.Lbl(r.End)} Pociąg"); }
                    Assert.True(res.Ok, res.Msg);
                }
                nastawione = true;
            }
        }
        return new Wynik(opuscil, stanal, vMax, ip ?? 0, sim.Stats.Spad, sim.Events.Select(e => e.Zdarzenie).ToList());
    }

    [Fact]
    public void PodGoreDluzejZGoryKrocej()
    {
        var gora = Jedz(8, "EN57"); var poz = Jedz(0, "EN57"); var dol = Jedz(-8, "EN57");
        Assert.All(new[] { gora, poz, dol }, w => { Assert.NotNull(w.Stanal); Assert.NotNull(w.Opuscil); Assert.Equal(0, w.Spad); });
        Assert.True(gora.Opuscil - gora.Stanal > poz.Opuscil - poz.Stanal && poz.Opuscil - poz.Stanal > dol.Opuscil - dol.Stanal,
            $"od zatrzymania przed A do opuszczenia obszaru: pod górę {gora.Opuscil - gora.Stanal:0} s, poziom {poz.Opuscil - poz.Stanal:0} s, z góry {dol.Opuscil - dol.Stanal:0} s");
        Assert.InRange(gora.IPod, 7.9, 8.1);
        Assert.InRange(dol.IPod, -8.1, -7.9);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "jazda_mijanka.txt"),
            "EN57, mijanka (kopia z pochyleniem toru 1), od zatrzymania przed A do opuszczenia obszaru przez 1W:\n" +
            $"  pod górę 8 ‰: {gora.Opuscil - gora.Stanal:0} s\n  poziom: {poz.Opuscil - poz.Stanal:0} s\n  z góry 8 ‰: {dol.Opuscil - dol.Stanal:0} s\n" +
            $"Prędkość najwyższa na szlaku przed A: {gora.VPrzedA:0} / {poz.VPrzedA:0} / {dol.VPrzedA:0} km/h\n");
    }

    [Fact]
    public void ZatrzymujeSiePrzedSygnalemStojNaSpadku()
    {
        // ciężki towarowy na spadku 10 ‰ – maszynista hamuje wcześniej i nie mija semafora A
        var w = Jedz(-10, "ET22+40xEaos", 900);
        Assert.NotNull(w.Stanal);
        Assert.Equal(0, w.Spad);
    }

    [Fact]
    public void CiezkiTowarowyNieRuszaPodGore()
    {
        // wyprawiony ze szlaku z 80 km/h na wzniesienie 12 ‰ – zwalnia, staje na szlaku i nie rusza (3320 t, ET22 360 kN)
        var w = Jedz(12, "ET22+40xEaos", 900);
        Assert.Null(w.Stanal);
        Assert.Null(w.Opuscil);
        Assert.Contains(w.Zdarzenia, z => z.Contains("77701 nie może ruszyć") && z.Contains("wzniesieniu"));
    }

    [Fact]
    public void BezSkladuJakDotad()
    {
        // pociąg bez składu – ruch uproszczony: pochylenie nie zmienia czasu
        var a = Jedz(8, null); var b = Jedz(-8, null);
        Assert.NotNull(a.Opuscil);
        Assert.Equal(a.Opuscil, b.Opuscil);
    }

    [Fact]
    public void NieznanySkladUwaga()
    {
        var st = Station.Load(Mijanka(0));
        var sim = new Simulator(st); var tr = new Trains(sim);
        sim.Rozklad.Queue = new() { new TimetableEntry { T = 1, Num = "1", Cat = "PR", From = "1Z", To = "1W", Sklad = "nie ma" } };
        sim.User = new User { Name = "auto", Role = "dyżurny ruchu" }; sim.Page = st.Pages.First().Id;
        for (int i = 0; i < 400 && tr.List.Count == 0; i++) { sim.Tick(0.25); if (sim.Blocks["1Z"].Req is { EndIdx: -1 }) sim.Exec("1Z Pzk"); }
        Assert.Contains(sim.Remarks, r => r.Text.Contains("nie ma składu"));
        Assert.Null(tr.List.Single().Fiz);
    }
}

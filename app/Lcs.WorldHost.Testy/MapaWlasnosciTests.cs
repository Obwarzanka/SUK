using System.Security.Cryptography;
using System.Text.Json;
using Lcs.Logi;
using LcsWidzew.Model;
using LcsWidzew.Sim;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using M = LcsWidzew.Sim.MapaWlasnosci;

namespace Lcs.WorldHost.Testy;

/// <summary>S02A.01 – mapa własności postId/kzId: przypisania z danych, brak domyślnego właściciela, granice, determinizm, logi F24.</summary>
public sealed class MapaWlasnosciTests
{
    static string Fixture => Path.Combine(AppContext.BaseDirectory, "TestData", "station_test.json");
    static Station Load() => Station.Load(Fixture);

    [Fact]
    public void KazdyPosterunekMaStalyKzIUrzadzeniaMajaWlascicielaZDanych()
    {
        byte[] before = SHA256.HashData(File.ReadAllBytes(Fixture));
        var station = Load(); var map = new Simulator(station).Wlasnosc;
        Assert.Equal(station.Posts.Keys.OrderBy(k => k, StringComparer.Ordinal), map.Posterunki.Select(p => p.PostId));
        Assert.All(map.Posterunki, p => Assert.Equal(M.PrzedrostekKz + p.PostId, p.KzId));
        Assert.Equal(map.Posterunki.Select(p => p.KzId).Distinct().Count(), map.Posterunki.Count);
        foreach (var sw in station.Switches.Values) Assert.Equal("KZ-" + sw.Post, map.KzObiektu(M.Rodzaj.Zwrotnica, sw.Num));
        foreach (var sg in station.Signals.Values) Assert.Equal("KZ-" + sg.Post, map.KzObiektu(M.Rodzaj.Sygnalizator, sg.Name));
        foreach (var d in station.Derailers.Values) Assert.Equal("KZ-" + d.Post, map.KzObiektu(M.Rodzaj.Wykolejnica, d.Name));
        foreach (var sc in station.Sections.Keys.Where(k => station.Posts.ContainsKey(Station.PostOf(k))))
            Assert.Equal("KZ-" + Station.PostOf(sc), map.KzObiektu(M.Rodzaj.Odcinek, sc));
        // każdy obiekt przypisany ma właściciela wśród swoich odbiorców; nieprzypisany nie ma kzId
        Assert.All(map.Obiekty, o =>
        {
            if (o.Status == M.Status.Przypisany) { Assert.NotNull(o.KzId); Assert.Contains(o.KzId, o.Odbiorcy); Assert.NotEqual(M.Zrodlo.Brak, o.Zrodlo); }
            else { Assert.Null(o.KzId); Assert.Null(map.KzObiektu(o.Rodzaj, o.Id)); }
        });
        // lokalne końce blokad: właściciel z pola „post” końca
        foreach (var b in station.Blocks.Values)
            for (int i = 0; i < b.Ends.Count; i++) Assert.Equal("KZ-" + b.Ends[i].Post, map.KzKoncaBlokady(b.Id, i));
        Assert.Equal(station.Blocks.Values.Sum(b => b.Ends.Count), map.KonceBlokad.Count);
        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(Fixture)));
    }

    [Fact]
    public void NiejednoznacznyINieznanyWlascicielPozostajeJawnymProblememBezDomyslnejZgody()
    {
        var station = Load(); var map = new Simulator(station).Wlasnosc;
        // odstęp szlaku między dwoma posterunkami LCS: węzły obu posterunków – mapa nie wybiera właściciela za człowieka
        foreach (var sc in station.Sections.Keys.Where(k => Station.PostOf(k) == "" && Wezly(station, k).Count > 1))
        {
            var o = map.ObiektMapy(M.Rodzaj.Odcinek, sc)!;
            Assert.Equal(M.Status.Niejednoznaczny, o.Status); Assert.Null(map.KzObiektu(M.Rodzaj.Odcinek, sc));
            Assert.Equal(Wezly(station, sc), o.Kandydaci);
            Assert.Contains(map.Problemy, p => p.Kod == M.KodProblemu.SectionPostAmbiguous && p.Id == sc);
        }
        // jedna szafa zasilania obszaru: brak domeny zasilania – nieznany właściciel, wszyscy jako odbiorcy statusu
        var z = map.ObiektMapy(M.Rodzaj.Zasilanie, Zasilanie.Id)!;
        Assert.Equal(M.Status.Nieznany, z.Status); Assert.Equal(map.Posterunki.Select(p => p.KzId), z.Odbiorcy);
        Assert.Contains(map.Problemy, p => p.Kod == M.KodProblemu.PowerDomainUnassigned);
        // nieznany cel i nieznany posterunek: null, nie domyślny KZ
        Assert.Null(map.KzObiektu(M.Rodzaj.Zwrotnica, "NIEISTNIEJACA"));
        Assert.Null(map.KzObiektu("inny-rodzaj", station.Switches.Keys.First()));
        Assert.Null(map.KzPosterunku("NIEISTNIEJACY")); Assert.Null(map.KzKoncaBlokady("NIEISTNIEJACY", 0));
        Assert.Null(map.KzObiektu(null!, null!));
    }

    [Fact]
    public void BledneDaneSaZglaszaneZamiastPrzypisywaneDomyslnie()
    {
        var station = Load();
        var sw = station.Switches.Values.OrderBy(s => s.Num, StringComparer.Ordinal).First();
        var sw2 = station.Switches.Values.OrderBy(s => s.Num, StringComparer.Ordinal).Last();
        var sig = station.Signals.Values.OrderBy(s => s.Name, StringComparer.Ordinal).First();
        var blk = station.Blocks.Values.OrderBy(b => b.Id, StringComparer.Ordinal).First();
        station.Posts["ZŁY ID"] = "spacja w identyfikatorze"; station.Posts["X:Y"] = "dwukropek";
        sw.Post = "NIEMA";                                   // posterunek spoza listy
        sig.Post = "";                                       // brak posterunku
        string inny = station.Posts.Keys.First(p => p != Station.PostOf(sw2.Num) && M.PoprawnyIdentyfikatorPosterunku(p));
        sw2.Post = inny;                                     // pole „post” niezgodne z przedrostkiem klucza
        blk.Ends[0].Post = "NIEMA";
        var start = station.Signals.Values.First(s => s.Post == "LW").Name;
        var tablica = new Dictionary<string, List<RouteDef>> { [start] = new() { new RouteDef { Id = "P lewy", Start = start, End = "x", Type = "M", Secs = { "BRAK-ODCINKA" } } } };
        var map = M.Zbuduj(station, tablica);
        Assert.Null(map.KzPosterunku("ZŁY ID")); Assert.Null(map.KzPosterunku("X:Y"));
        Assert.Contains(map.Problemy, p => p.Kod == M.KodProblemu.PostIdInvalid && p.Id == "ZŁY ID");
        Assert.Contains(map.Problemy, p => p.Kod == M.KodProblemu.PostIdInvalid && p.Id == "X:Y");
        Assert.Null(map.KzObiektu(M.Rodzaj.Zwrotnica, sw.Num)); Assert.Null(map.KzObiektu(M.Rodzaj.Sygnalizator, sig.Name));
        Assert.Contains(map.Problemy, p => p.Kod == M.KodProblemu.ObjectPostUnknown && p.Id == sw.Num);
        Assert.Contains(map.Problemy, p => p.Kod == M.KodProblemu.ObjectPostUnknown && p.Id == sig.Name);
        Assert.Equal("KZ-" + inny, map.KzObiektu(M.Rodzaj.Zwrotnica, sw2.Num));
        Assert.Contains(map.Problemy, p => p.Kod == M.KodProblemu.PostFieldPrefixMismatch && p.Id == sw2.Num);
        Assert.Null(map.KzKoncaBlokady(blk.Id, 0));
        Assert.Contains(map.Problemy, p => p.Kod == M.KodProblemu.BlockEndPostUnknown && p.Id == blk.Id + "#0");
        Assert.Equal(M.RodzajSzlaku.Nieokreslony, map.Szlaki.Single(s => s.BlokadaId == blk.Id).Rodzaj);
        Assert.Contains(map.Problemy, p => p.Kod == M.KodProblemu.DanglingReference && p.Id == "BRAK-ODCINKA");
    }

    [Fact]
    public void GraniceOpisujaOdwolaniaDoObiektowInnegoKzIWspolnyStanSzlaku()
    {
        var station = Load(); var map = new Simulator(station).Wlasnosc;
        var kz = map.Posterunki.Select(p => p.KzId).ToHashSet();
        Assert.NotEmpty(map.Granice);
        Assert.All(map.Granice, g =>
        {
            Assert.NotEqual(g.ZKz, g.DoKz); Assert.Contains(g.ZKz, kz); Assert.Contains(g.DoKz, kz);
            Assert.True(g.LiczbaOdwolan >= g.Konteksty.Count && g.Konteksty.Count <= M.MaxKontekstow);
            if (g.ObiektRodzaj != M.Rodzaj.Blokada)
            {
                Assert.Equal(g.DoKz, map.KzObiektu(g.ObiektRodzaj, g.ObiektId));
                Assert.Contains(g.ZKz, map.ObiektMapy(g.ObiektRodzaj, g.ObiektId)!.Odbiorcy);
            }
        });
        // szlak między dwoma KZ: obie strony zależą od wspólnego dziś stanu blokady (decyzja D03), oba kierunki granicy
        foreach (var s in map.Szlaki.Where(s => s.Rodzaj == M.RodzajSzlaku.WewnetrznyMiedzyKz))
        {
            var a = s.KzKoncow[0]!; var b = s.KzKoncow[1]!;
            Assert.Contains(map.Granice, g => g.Rodzaj == M.RodzajGranicy.BlokadaKoniec && g.ZKz == a && g.DoKz == b && g.ObiektId == s.BlokadaId);
            Assert.Contains(map.Granice, g => g.Rodzaj == M.RodzajGranicy.BlokadaKoniec && g.ZKz == b && g.DoKz == a && g.ObiektId == s.BlokadaId);
            Assert.Contains(map.Problemy, p => p.Kod == M.KodProblemu.BlockInternalCrossKz && p.Id == s.BlokadaId);
        }
        Assert.Contains(map.Szlaki, s => s.Rodzaj == M.RodzajSzlaku.WewnetrznyMiedzyKz);
        Assert.All(map.Szlaki.Where(s => s.Rodzaj == M.RodzajSzlaku.Zewnetrzny), s => Assert.False(string.IsNullOrEmpty(s.Sasiad)));
        // uzależnienie sygnału przez granicę posterunków jest inwentaryzowane (Kod() czyta dziś globalny Sigs)
        Assert.Contains(map.Granice, g => g.Rodzaj == M.RodzajGranicy.NastepnySygnal);
    }

    [Fact]
    public void MapaJestDeterministycznaINiezaleznaOdKolejnosciSlownikow()
    {
        var sim = new Simulator(Load()); var first = sim.Wlasnosc;
        Assert.Same(first, sim.Wlasnosc);
        Assert.Equal(first.Json, new Simulator(Load()).Wlasnosc.Json);
        var reversed = Load();
        reversed.Nodes = Reverse(reversed.Nodes); reversed.Edges = Reverse(reversed.Edges); reversed.Sections = Reverse(reversed.Sections);
        reversed.Switches = Reverse(reversed.Switches); reversed.Signals = Reverse(reversed.Signals); reversed.Derailers = Reverse(reversed.Derailers);
        reversed.Blocks = Reverse(reversed.Blocks); reversed.Posts = Reverse(reversed.Posts);
        var table = new Dictionary<string, List<RouteDef>>();
        foreach (var (k, v) in sim.RouteTable.Reverse()) table[k] = Enumerable.Reverse(v).ToList();
        var second = M.Zbuduj(reversed, table, sim.NastepnySygnalZaPrzebiegiem, sim.NastepnySygnalZaOdstepowym);
        Assert.Equal(first.Json, second.Json); Assert.Equal(first.Sha256, second.Sha256);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(first.Json))), first.Sha256);
        using var doc = JsonDocument.Parse(first.Json);
        Assert.Equal(M.WersjaSchematu, doc.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(M.Kontrakt, doc.RootElement.GetProperty("kontrakt").GetString());
        Assert.DoesNotContain("\r", first.Json);
    }

    [Fact]
    public void MapaJestNiemutowalnaMigawkaDanychIPrzeliczenieTablicyJaOdswieza()
    {
        var station = Load(); var sim = new Simulator(station); var map = sim.Wlasnosc;
        var sw = station.Switches.Values.First(); string kz = map.KzObiektu(M.Rodzaj.Zwrotnica, sw.Num)!;
        Assert.Throws<NotSupportedException>(() => ((ICollection<M.Obiekt>)map.Obiekty).Add(map.Obiekty[0]));
        Assert.Throws<NotSupportedException>(() => ((ICollection<string>)map.Obiekty[0].Odbiorcy).Clear());
        Assert.Throws<NotSupportedException>(() => ((ICollection<M.Granica>)map.Granice).Clear());
        sw.Post = "NIEMA";
        Assert.Equal(kz, map.KzObiektu(M.Rodzaj.Zwrotnica, sw.Num));     // migawka – zmiana danych po budowie jej nie zmienia
        Assert.Null(sim.PrzeliczTablice());
        Assert.NotSame(map, sim.Wlasnosc); Assert.Null(sim.Wlasnosc.KzObiektu(M.Rodzaj.Zwrotnica, sw.Num));
        Assert.DoesNotContain(typeof(M).GetFields(), f => !f.IsLiteral);
        Assert.All(typeof(M).GetProperties(), p => Assert.Null(p.SetMethod));
    }

    [Fact]
    public void F24LogiMapyMajaLimitRedakcjeIKontekst()
    {
        WorldLog.Register(); Rejestruj();
        string directory = TestArtifacts.Directory("S02A01-logi-mapy");
        var station = Load();
        for (int i = 0; i < 20; i++) station.Posts[$"ZŁY {i}"] = "x";            // ponad limit rekordów problemów
        station.Posts["password=" + ProbaLogow.ZnacznikSekretu] = "sekret";        // identyfikator wyglądający na sekret
        station.Posts["a\r\n{\"wstrzykniete\":1}"] = "CR/LF";
        station.Posts[new string('Q', 300)] = "za długi";
        var map = new Simulator(station).Wlasnosc;
        int problems = map.Problemy.Count; Assert.True(problems > MapaWlasnosciLog.MaxProblemowWLogu);
        using (var engine = SilnikLogow.Uruchom(new OpcjeLogow { Aplikacja = "Lcs.WorldHost", Katalog = directory, Profil = ProfilLogow.Serwer }))
        {
            var log = engine.Modul(MapaWlasnosciLog.Modul, MapaWlasnosciLog.Wersja);
            MapaWlasnosciLog.Zapisz(log, map);
            MapaWlasnosciLog.Blad(log, new IOException("password=" + ProbaLogow.ZnacznikSekretu + "_MAPA\r\n{\"wstrzykniete\":2}"));
            var state = engine.Zatrzymaj(); File.WriteAllText(Path.Combine(directory, "stan.txt"), state.ToString());
            Assert.True(state.ZamkniecieKompletne); Assert.Equal(0, state.StratyRazem);
        }
        var records = TestArtifacts.Records(directory); string all = string.Join('\n', records.Select(r => r.GetRawText()));
        Assert.DoesNotContain(ProbaLogow.ZnacznikSekretu, all, StringComparison.Ordinal);
        Assert.DoesNotContain(records, r => r.TryGetProperty("wstrzykniete", out _));
        var mine = records.Where(r => Text(r, "moduleId") == MapaWlasnosciLog.Modul).ToArray();
        Assert.All(mine, r => { Assert.Equal(1, r.GetProperty("schemaVersion").GetInt32()); Assert.Equal(MapaWlasnosciLog.Wersja, Text(r, "moduleVersion")); });
        var built = Assert.Single(mine, r => Text(r, "eventCode") == MapaWlasnosciLog.KodZbudowano);
        Assert.Equal(MapaWlasnosciLog.IdZbudowano, built.GetProperty("eventId").GetInt32());
        Assert.Equal(map.Sha256, Text(built, "contentHash")); Assert.Equal("Degraded", Text(built, "outcome"));
        var counters = built.GetProperty("counters");
        Assert.Equal(problems, counters.GetProperty("problems").GetInt64());
        Assert.Equal(MapaWlasnosciLog.MaxProblemowWLogu, counters.GetProperty("problemsLogged").GetInt64());
        Assert.Equal(problems - MapaWlasnosciLog.MaxProblemowWLogu, counters.GetProperty("problemsSuppressed").GetInt64());
        var unresolved = mine.Where(r => Text(r, "eventCode") == MapaWlasnosciLog.KodNierozstrzygniety).ToArray();
        Assert.Equal(MapaWlasnosciLog.MaxProblemowWLogu, unresolved.Length);
        Assert.All(unresolved, r => { Assert.Equal("Warning", Text(r, "severity")); Assert.Matches("^[A-Z][A-Z0-9_]{1,63}$", Text(r, "reasonCode")); });
        Assert.DoesNotContain(unresolved, r => Text(r, "targetId").Contains(' ') || Text(r, "targetId").Length > 128);
        var failed = Assert.Single(mine, r => Text(r, "eventCode") == MapaWlasnosciLog.KodBlad);
        Assert.Equal("OWNERSHIP_MAP_BUILD_FAILED", Text(failed, "reasonCode"));
    }

    [Fact]
    public void HostSwiataPublikujeMapePrzyStarcieTylkoRazIBezZmianyDanych()
    {
        WorldLog.Register(); Rejestruj();
        string directory = TestArtifacts.Directory("S02A01-host-mapa");
        string copy = Path.Combine(directory, "station_test.json"); File.Copy(Fixture, copy);
        byte[] before = SHA256.HashData(File.ReadAllBytes(copy));
        using (var engine = SilnikLogow.Uruchom(new OpcjeLogow { Aplikacja = "Lcs.WorldHost", Katalog = Path.Combine(directory, "logi"), Profil = ProfilLogow.Serwer }))
        {
            var runtime = new SimulatorRuntime(copy, "podstawowy", NullLogger.Instance, NullLogger.Instance, false, engine.Modul(MapaWlasnosciLog.Modul, MapaWlasnosciLog.Wersja));
            var loop = new WorldLoop(() => runtime, clock: new ManualClock()); loop.Start(); loop.Finish();
            engine.Zatrzymaj();
        }
        var records = TestArtifacts.Records(Path.Combine(directory, "logi")).Where(r => Text(r, "moduleId") == MapaWlasnosciLog.Modul).ToArray();
        var built = Assert.Single(records, r => Text(r, "eventCode") == MapaWlasnosciLog.KodZbudowano);
        Assert.Equal(new Simulator(Station.Load(copy)).Wlasnosc.Sha256, Text(built, "contentHash"));
        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(copy)));
    }

    static void Rejestruj()
    {
        foreach (var (id, kod, pola) in MapaWlasnosciLog.Definicje)
            Assert.True(KatalogZdarzen.Zarejestruj(new DefinicjaZdarzenia(id, kod, KontraktLogu.Strumien.Diagnostyka, pola)));
    }

    static List<string> Wezly(Station st, string sec) => st.Sections[sec].Edges.SelectMany(e => new[] { st.Edges[e].A, st.Edges[e].B })
        .Select(n => st.Nodes[n].Post).Where(p => !string.IsNullOrEmpty(p)).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToList();
    static Dictionary<string, T> Reverse<T>(Dictionary<string, T> d) { var r = new Dictionary<string, T>(); foreach (var (k, v) in d.Reverse()) r[k] = v; return r; }
    static string Text(JsonElement record, string key) => record.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
}

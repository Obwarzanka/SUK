using System.Text.Json;
using Xunit;

namespace Lcs.Logi.Testy;

/// <summary>Zgodność kopii katalogu w kodzie z kontraktem kontrakty/logi/katalog_zdarzen_v1.json i reguły rejestracji kodów.</summary>
public class KatalogTesty
{
    static JsonElement Katalog()
    {
        var repo = Pom.KatalogRepo();
        if (repo == null) Assert.Skip("Brak katalogu kontrakty/ w drzewie nad testem (uruchom z kopii repo).");
        using var d = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "kontrakty", "logi", "katalog_zdarzen_v1.json")));
        return d.RootElement.Clone();
    }

    [Fact]
    public void PodzakresyZgodneZKontraktem()
    {
        var k = Katalog();
        var zJson = k.GetProperty("obszary").EnumerateArray()
            .SelectMany(o => o.GetProperty("podzakresy").EnumerateArray())
            .Select(p => (p.GetProperty("od").GetInt32(), p.GetProperty("do").GetInt32(), p.GetProperty("prefiks").GetString())).ToList();
        var zKodu = KatalogZdarzen.Podzakresy.Select(p => (p.Od, p.Do, p.Prefiks)).ToList();
        Assert.Equal(zJson, zKodu);
    }

    [Fact]
    public void KodyBibliotekiSaWKatalogu()
    {
        var k = Katalog();
        var schemat = new HashSet<string>(KontraktLogu.PolaKontekstu.Keys);
        var kody = k.GetProperty("kody").EnumerateArray().ToDictionary(x => x.GetProperty("eventId").GetInt32());
        foreach (var d in ZdarzeniaLogow.Wszystkie.Concat(ZdarzeniaProcesu.Definicje))
        {
            Assert.True(kody.TryGetValue(d.Id, out var j), $"brak {d.Id} {d.Kod} w katalogu");
            Assert.Equal(d.Kod, j.GetProperty("eventCode").GetString());
            Assert.Equal(d.Strumien, j.GetProperty("stream").GetString());
            var pola = j.GetProperty("pola").EnumerateArray().Select(x => x.GetString()).ToHashSet();
            Assert.Subset(pola, d.Pola.ToHashSet());
            Assert.All(pola, p => Assert.True(schemat.Contains(p) || d.Pola.Contains(p), $"{d.Kod}: pole {p} spoza schematu i definicji"));
            Assert.Same(d, KatalogZdarzen.Definicja(d.Id));
        }
    }

    [Fact]
    public void RejestracjaOdrzucaDefinicjeNiezgodne()
    {
        long przed = KatalogZdarzen.OdrzuconeDefinicje;
        Assert.False(KatalogZdarzen.Zarejestruj(new DefinicjaZdarzenia(1500, "REZERWA.X", KontraktLogu.Strumien.Diagnostyka)));      // rezerwa
        Assert.False(KatalogZdarzen.Zarejestruj(new DefinicjaZdarzenia(2150, "WORLD.X", KontraktLogu.Strumien.Diagnostyka)));        // zły prefiks
        Assert.False(KatalogZdarzen.Zarejestruj(new DefinicjaZdarzenia(2151, "AUTH.X", KontraktLogu.Strumien.Diagnostyka, "userPassword"))); // pole sekretu
        Assert.False(KatalogZdarzen.Zarejestruj(new DefinicjaZdarzenia(2152, "AUTH.X", "inny")));                                     // zły strumień
        Assert.False(KatalogZdarzen.Zarejestruj(new DefinicjaZdarzenia(2153, "AUTH.X", KontraktLogu.Strumien.Diagnostyka, "moduleId")));  // pole zastrzeżone
        Assert.False(KatalogZdarzen.Zarejestruj(new DefinicjaZdarzenia(9101, "LOG.INNY", KontraktLogu.Strumien.Diagnostyka)));       // konflikt z istniejącym
        Assert.True(KatalogZdarzen.Zarejestruj(new DefinicjaZdarzenia(9101, "LOG.DIAGNOSTICS_DROPPED", KontraktLogu.Strumien.Diagnostyka))); // ta sama treść
        Assert.Equal(przed + 6, KatalogZdarzen.OdrzuconeDefinicje);
    }

    [Fact]
    public void KodZgodnyZPodzakresem()
    {
        Assert.True(KatalogZdarzen.ZgodnyZPodzakresem(3601, "REJ.EVENT_RECORDED"));
        Assert.True(KatalogZdarzen.ZgodnyZPodzakresem(2101, "AUTH.COMMAND_REJECTED"));
        Assert.False(KatalogZdarzen.ZgodnyZPodzakresem(2101, "AUTHX.COMMAND"));
        Assert.False(KatalogZdarzen.ZgodnyZPodzakresem(3700, "REZERWA.A"));
        Assert.False(KatalogZdarzen.ZgodnyZPodzakresem(999, "PROC.X"));
        Assert.False(KatalogZdarzen.ZgodnyZPodzakresem(1001, "proc.started"));
    }
}

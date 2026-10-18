using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Lcs.Logi.Testy;

/// <summary>L02: limit kolejki nie zastępuje granic pamięci adaptera poza kolejką.</summary>
public class LimityPamieciTesty
{
    [Fact]
    public void DlugiSzablonNieJestZachowywanyPoPrzyjeciuRekordu()
    {
        var sink = new SinkTestowy();
        var s = SilnikLogow.Uruchom(Pom.Opcje(Pom.NowyKatalog("cache"), sink));
        var log = s.Modul("Test.Cache", "1.0.0");
        EmitujDlugieSzablony(log, 1); // rozgrzewka mostu i adaptera przed pomiarem
        Assert.True(SpinWait.SpinUntil(() => s.Stan().WKolejce == 0, 5000));
        long przed = GC.GetTotalMemory(true);
        EmitujDlugieSzablony(log, 16);
        s.Zatrzymaj();
        long przyrost = GC.GetTotalMemory(true) - przed;
        // Szesnaście różnych wejść po 1 mln znaków nie może zostać kluczami cache (32 MiB).
        // Zostają wyłącznie ograniczone rekordy/formaty i tekst sinku testowego.
        Assert.True(przyrost < 2 * 1024 * 1024, $"Zachowano surowe szablony: {przyrost} B");
        Assert.Equal(17, sink.Rekordy().Count(r => r.Tekst("moduleId") == "Test.Cache"));
        Assert.All(sink.Rekordy(), r => Assert.True(r.Tekst("@mt").Length <= KontraktLogu.MaxZnakowTekstu));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void EmitujDlugieSzablony(ILogger log, int ile)
    {
        for (int i = 0; i < ile; i++)
        {
            string szablon = new string((char)('a' + i), 1_000_000);
            // Własny stan MEL omija cache dynamicznych szablonów LogInformation/LogWarning,
            // aby pomiar dotyczył biblioteki SUK, a nie niezależnego cache MEL.
            var stan = new[] { new KeyValuePair<string, object>("{OriginalFormat}", szablon) };
            log.Log(LogLevel.Warning, new EventId(9003, "LOG.PROBE"), stan, null, static (a, b) => "");
        }
    }

    [Fact]
    public void NadmiarPolILicznikowJestOdrzucanyPrzedBudowaRekordu()
    {
        var sink = new SinkTestowy();
        var s = SilnikLogow.Uruchom(Pom.Opcje(Pom.NowyKatalog("pola"), sink));
        var log = s.Modul("Test.Limity", "1.0.0");
        var stan = Enumerable.Range(0, 10_000).Select(i => new KeyValuePair<string, object>("obce" + i, "x")).ToList();
        stan.Insert(0, new("counters", Enumerable.Range(0, 10_000).ToDictionary(i => "licznik" + i, i => (object)i)));
        stan.Add(new("{OriginalFormat}", "Nadmiar pól"));
        log.Log(LogLevel.Warning, new EventId(9003, "LOG.PROBE"), stan, null, static (a, b) => "");
        s.Zatrzymaj();
        var r = sink.Rekordy().Single(x => x.Tekst("moduleId") == "Test.Limity");
        Assert.True(r.GetProperty("counters").EnumerateObject().Count() <= KontraktLogu.MaxLicznikow);
        Assert.True(r.Licznik("rejectedFields") >= 10_000);
        Assert.False(r.Ma("obce9999"));
    }
}

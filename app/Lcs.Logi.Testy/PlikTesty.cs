using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Lcs.Logi.Testy;

/// <summary>L03: rotacja rozmiarem, retencja liczby i wieku, brak dostępu do katalogu, odzyskanie zapisu, nadzór miejsca (B06–B08).</summary>
public class PlikTesty
{
    static void Paczkami(ILogger log, int n)
    {
        for (int i = 0; i < n; i++)
        {
            ZdarzeniaProcesu.Zatrzymano(log, i);
            if (i % 200 == 199) Thread.Sleep(20);
        }
    }

    [Fact]
    public void RotacjaRozmiaremIRetencjaLiczbyPlikow()
    {
        var kat = Pom.NowyKatalog("rotacja");
        var o = Pom.Opcje(kat); o.MaxBajtowPliku = 16 * 1024; o.MaxPlikow = 3;
        var s = SilnikLogow.Uruchom(o);
        Paczkami(s.Modul("Test.Rotacja", "1.0.0"), 2000);
        var st = s.Zatrzymaj();
        var pliki = Directory.GetFiles(kat, "Test-*.jsonl");
        Pom.Pomiar("L03_rotacja", $"limit_pliku=16384 max_plikow=3 rekordy={st.Przyjete} pliki={pliki.Length} rozmiary={string.Join(",", pliki.Select(f => new FileInfo(f).Length))}");
        Assert.True(pliki.Length <= 3, $"plików {pliki.Length}");
        Assert.True(pliki.Length >= 2, "brak rotacji");
        // Serilog.Sinks.File kończy plik po przekroczeniu limitu: plik ≤ limit + jeden rekord (≤ 8 KiB).
        Assert.All(pliki, f => Assert.True(new FileInfo(f).Length <= 16 * 1024 + KontraktLogu.MaxBajtowRekordu, $"{f}: {new FileInfo(f).Length} B"));
        Assert.Equal(0, st.StratyRazem);
        Pom.Zachowaj(kat, "L03_rotacja");
    }

    [Fact]
    public void RetencjaWiekuNieDotykaInnychPlikow()
    {
        var rodzic = Pom.NowyKatalog("retencja");       // własny katalog nadrzędny – testy net8/net10 działają równolegle w jednym %TEMP%
        var kat = Path.Combine(rodzic, "Logs");
        Directory.CreateDirectory(kat);
        var stare = new[] { "Test-20200101.jsonl", "Test-20200102_001.jsonl" };
        foreach (var f in stare) File.WriteAllText(Path.Combine(kat, f), "{}\n");
        var obce = new[] { "station.json", "projekt.ut.json", "Inny-20200101.jsonl" };
        foreach (var f in obce) File.WriteAllText(Path.Combine(kat, f), "dane właściciela " + f);
        var sumy = obce.ToDictionary(f => f, f => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(kat, f)))));
        File.WriteAllText(Path.Combine(rodzic, "station.json"), "obok katalogu logów");
        var przed = Directory.GetFileSystemEntries(rodzic, "*", SearchOption.AllDirectories).Where(f => !f.StartsWith(kat)).ToList();

        var o = Pom.Opcje(kat); o.MaxBajtowPliku = 16 * 1024; o.MaxWiekPliku = TimeSpan.FromDays(7);
        var s = SilnikLogow.Uruchom(o);
        Paczkami(s.Modul("Test.Retencja", "1.0.0"), 300);
        s.Zatrzymaj();

        foreach (var f in stare) Assert.False(File.Exists(Path.Combine(kat, f)), $"{f} nie usunięty mimo wieku > 7 dni");
        foreach (var f in obce) Assert.Equal(sumy[f], Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(kat, f)))));
        Assert.Equal(przed, Directory.GetFileSystemEntries(rodzic, "*", SearchOption.AllDirectories).Where(f => !f.StartsWith(kat)).ToList());   // nic poza katalogiem logów
    }

    [Fact]
    public void BrakDostepuDoKatalogu()
    {
        if (OperatingSystem.IsWindows()) Assert.Skip("Próba praw dostępu przez chmod – Linux.");
        var rodzic = Pom.NowyKatalog("acl");
        File.SetUnixFileMode(rodzic, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            var s = SilnikLogow.Uruchom(Pom.Opcje(Path.Combine(rodzic, "Logs")));
            var log = s.Modul("Test.Acl", "1.0.0");
            for (int i = 0; i < 20; i++) ZdarzeniaProcesu.Zatrzymano(log, i);   // emisja nie rzuca i nie czeka
            Assert.True(SpinWait.SpinUntil(() => s.Stan().StratyWgModuluIPowodu.GetValueOrDefault("Test.Acl|SINK_FAILED") == 20, 5000));
            var st = s.Stan();
            Assert.Equal(StanZapisu.Niegotowy, st.Zapis);
            Assert.StartsWith("KATALOG_NIEDOSTEPNY", st.PowodNiegotowosci);
            Assert.Equal(20, st.Przyjete);
            Assert.Equal(20, st.StratyWgModuluIPowodu["Test.Acl|SINK_FAILED"]);
            var k = s.Zatrzymaj();
            Assert.Equal(StanZapisu.Niegotowy, k.Zapis);
            Assert.False(Directory.Exists(Path.Combine(rodzic, "Logs")));
            Pom.Pomiar("L03_brak_dostepu", k.ToString().Replace('\n', ' '));
        }
        finally { File.SetUnixFileMode(rodzic, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
    }

    [Fact]
    public void OdzyskanieZapisuPoAwariiJestWidoczne()
    {
        var sink = new SinkTestowy { Rzucaj = true };
        var s = SilnikLogow.Uruchom(Pom.Opcje(Pom.NowyKatalog("odzyskanie"), sink));
        var log = s.Modul("Test.Odzyskanie", "1.0.0");
        for (int i = 0; i < 10; i++) ZdarzeniaProcesu.Zatrzymano(log, i);
        Assert.True(SpinWait.SpinUntil(() => s.Stan().StratyWgModuluIPowodu.GetValueOrDefault("Test.Odzyskanie|SINK_FAILED") == 10, 5000));
        var awaria = s.Stan();
        Assert.Equal(StanZapisu.Niegotowy, awaria.Zapis);
        Assert.Equal("ZAPIS_NIEUDANY", awaria.PowodNiegotowosci);
        sink.Rzucaj = false;
        for (int i = 0; i < 10; i++) ZdarzeniaProcesu.Zatrzymano(log, 100 + i);
        Assert.True(SpinWait.SpinUntil(() => s.Stan().Zapis == StanZapisu.Gotowy, 3000));
        var st = s.Zatrzymaj();
        Pom.Zachowaj(sink.Linie, "L03_odzyskanie");
        var r = sink.Rekordy();
        var zapis = r.Single(x => x.Tekst("eventCode") == "LOG.SINK_FAILED");
        Assert.Equal(st.BledyZapisu, zapis.Liczba("droppedCount"));       // 10 rekordów modułu + LOG.STARTED
        Assert.Equal(11, st.BledyZapisu);
        Assert.Equal(10, r.Count(x => x.Tekst("moduleId") == "Test.Odzyskanie"));
        Assert.Contains(r, x => x.Tekst("eventCode") == "LOG.DIAGNOSTICS_DROPPED" && x.Tekst("reasonCode") == "SINK_FAILED"
            && x.Tekst("targetId") == "Test.Odzyskanie" && x.Liczba("droppedCount") == 10);
    }

    [Fact]
    public void KatalogOdblokowanyPozniejZostajeNiegotowyDoPonowieniaSerilog()
    {
        if (OperatingSystem.IsWindows()) Assert.Skip("Próba praw dostępu przez chmod – Linux.");
        // Serilog.Sinks.File po nieudanym otwarciu pliku ponawia je przy następnym punkcie kontrolnym, najpóźniej po 30 min
        // (RollingFileSink.OpenFile, v7.0.0). Do tego czasu stan Niegotowy i liczniki strat są widoczne.
        var rodzic = Pom.NowyKatalog("ponowienie");
        File.SetUnixFileMode(rodzic, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        var s = SilnikLogow.Uruchom(Pom.Opcje(Path.Combine(rodzic, "Logs")));
        var log = s.Modul("Test.Ponowienie", "1.0.0");
        ZdarzeniaProcesu.Zatrzymano(log, 1);
        Assert.True(SpinWait.SpinUntil(() => s.Stan().StratyWgModuluIPowodu.GetValueOrDefault("Test.Ponowienie|SINK_FAILED") == 1, 5000));
        File.SetUnixFileMode(rodzic, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        ZdarzeniaProcesu.Zatrzymano(log, 2);
        Assert.True(SpinWait.SpinUntil(() => s.Stan().StratyWgModuluIPowodu.GetValueOrDefault("Test.Ponowienie|SINK_FAILED") == 2, 5000));
        var st = s.Zatrzymaj();
        Assert.Equal(StanZapisu.Niegotowy, st.Zapis);
        Assert.Equal(2, st.StratyWgModuluIPowodu["Test.Ponowienie|SINK_FAILED"]);
    }

    [Fact]
    public void MaloMiejscaOgraniczaDiagnostyke()
    {
        var sink = new SinkTestowy();
        long wolne = 8;
        var o = Pom.Opcje(Pom.NowyKatalog("miejsce"), sink);
        o.WolneMiejsce = () => (Interlocked.Read(ref wolne), 100); o.OkresNadzoruMiejsca = TimeSpan.FromMilliseconds(50); o.OkresGrupowania = TimeSpan.FromMilliseconds(50);
        var s = SilnikLogow.Uruchom(o);
        var log = s.Modul("Test.Miejsce", "1.0.0");
        Assert.Equal(StanMiejsca.Ograniczenie, s.Stan().Miejsce);
        ZdarzeniaProcesu.Zatrzymano(log, 1);                                  // Information – pominięty
        ZdarzeniaProcesu.Blad(log, new IOException("x"), "TEST_PROBE");       // Error – przyjęty
        Assert.Equal(1, s.Stan().Przyjete);
        Interlocked.Exchange(ref wolne, 12);
        Assert.True(SpinWait.SpinUntil(() => s.Stan().Miejsce == StanMiejsca.Ostrzezenie, 5000));
        ZdarzeniaProcesu.Zatrzymano(log, 2);
        Interlocked.Exchange(ref wolne, 50);
        Assert.True(SpinWait.SpinUntil(() => s.Stan().Miejsce == StanMiejsca.Normalny, 5000));
        s.Zatrzymaj();
        Pom.Zachowaj(sink.Linie, "B08_miejsce");
        var r = sink.Rekordy();
        var niskie = r.Where(x => x.Tekst("eventCode") == "LOG.DISK_SPACE_LOW").ToList();
        Assert.Equal(new[] { "DISK_BELOW_LIMIT", "DISK_BELOW_WARNING" }, niskie.Select(x => x.Tekst("reasonCode")));
        Assert.Equal(8, niskie[0].Licznik("freePercent"));
        Assert.Single(r, x => x.Tekst("eventCode") == "LOG.DISK_SPACE_RESTORED");
        Assert.Equal(2, r.Count(x => x.Tekst("moduleId") == "Test.Miejsce"));
    }
}

using Microsoft.Extensions.Logging;
using Xunit;

namespace Lcs.Logi.Testy;

/// <summary>L05: sekrety i CR/LF w polach, wyjątkach i szablonie; jedna poprawna linia JSON na rekord; brak wyjścia na konsolę.</summary>
public class RedakcjaTesty
{
    const string Z = ProbaLogow.ZnacznikSekretu;

    [Fact]
    public void ZnacznikiSekretowNieTrafiajaDoPlikow()
    {
        var kat = Pom.NowyKatalog("sekrety");
        var stareOut = Console.Out; var stareErr = Console.Error;
        var konsola = new StringWriter();
        Console.SetOut(konsola); Console.SetError(konsola);
        StanLogow st;
        try
        {
            var s = SilnikLogow.Uruchom(Pom.Opcje(kat));
            var log = s.Modul("Test.Redakcja", "1.0.0");
            ProbaLogow.Wykonaj(log);
            var jwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0." + Z + "jwt";
            ZdarzeniaProcesu.Blad(log, new Exception("Server=db;User Id=suk;Password=" + Z + "_G;Database=x"), "TEST_PROBE");
            ZdarzeniaProcesu.Blad(log, new Exception("Cookie: sesja=" + Z + "_H; Set-Cookie: a=" + Z + "_I\nauthorization:\"Basic " + Z + "_J\"\n" + jwt), "TEST_PROBE");
            using (log.BeginScope(new Dictionary<string, object> { ["accessToken"] = Z + "_K", ["Authorization"] = "Bearer " + Z + "_L", ["targetId"] = "token=" + Z + "_M" }))
                ZdarzeniaProcesu.Zatrzymano(log, 1);
#pragma warning disable CA2254, CA1848, CA2017
            log.Log(LogLevel.Warning, new EventId(9003, "LOG.PROBE"), "Szablon z sekretem password=" + Z + "_N\r\n{\"@t\":\"x\"}");
#pragma warning restore CA2254, CA1848, CA2017
            st = s.Zatrzymaj();
        }
        finally { Console.SetOut(stareOut); Console.SetError(stareErr); }
        Pom.Zachowaj(kat, "L05_sekrety");
        var tekst = string.Concat(Directory.GetFiles(kat).Select(File.ReadAllText));
        Assert.DoesNotContain(Z, tekst);
        Assert.Equal("", konsola.ToString());
        var linie = Pom.Linie(kat);
        Assert.Equal(st.Przyjete + st.Wlasne, linie.Count);                  // jeden rekord = jedna linia, nic nie „rozbija” linii
        var r = linie.Select(Pom.Parsuj).ToList();
        Assert.All(r, x => Assert.DoesNotContain('\n', x.Tekst("@mt")));
        var proba = r.Single(x => x.Tekst("eventCode") == "LOG.PROBE" && x.Tekst("opis")?.StartsWith("tekst z CR/LF") == true);
        Assert.Equal(6, proba.Licznik("rejectedFields"));                    // password, token, ApiKey, correlationId, channelId, eventCode
        Assert.DoesNotContain('\r', proba.Tekst("opis"));
        Assert.DoesNotContain('\u2028', proba.Tekst("opis"));
        Assert.DoesNotContain('\u0007', proba.Tekst("opis"));
        var dlugi = r.Single(x => x.Tekst("eventCode") == "LOG.PROBE" && x.Tekst("opis")?.StartsWith("xxx") == true);
        Assert.Equal(KontraktLogu.MaxZnakowTekstu, dlugi.Tekst("opis").Length);
        Assert.Equal(1, dlugi.Licznik("truncatedFields"));
        var blad = r.First(x => x.Tekst("eventCode") == "PROC.UNHANDLED_EXCEPTION");
        Assert.Contains(Redakcja.Znacznik, blad.Tekst("@x"));
        Assert.Contains("[UZYTKOWNIK]", blad.Tekst("@x"));
        Assert.True(blad.Tekst("@x").Length <= KontraktLogu.MaxZnakowWyjatku);
        var stop = r.Single(x => x.Tekst("eventCode") == "PROC.STOPPED");
        Assert.False(stop.Ma("targetId"));                                    // identyfikator z sekretem odrzucony
        Assert.Equal(3, stop.Licznik("rejectedFields"));
    }

    [Theory]
    [InlineData("C:\\Users\\Ktos\\AppData\\Local\\SUK", "C:\\Users\\[UZYTKOWNIK]\\AppData\\Local\\SUK")]
    [InlineData("/home/kacper/plik", "/home/[UZYTKOWNIK]/plik")]
    [InlineData("połączenie 192.168.1.5:47120 odrzucone", "połączenie [IP]:47120 odrzucone")]
    [InlineData("Microsoft Windows NT 10.0.26200.0", "Microsoft Windows NT 10.0.26200.0")]
    [InlineData("password = \"tajne haslo\" dalej", "password = " + Redakcja.Znacznik + " dalej")]
    [InlineData("haslo:abc;x", "haslo:" + Redakcja.Znacznik + ";x")]
    [InlineData("Authorization: Negotiate YIIabcdefgh", "Authorization: " + Redakcja.Znacznik + " " + Redakcja.Znacznik)]
    [InlineData("zwykły tekst bez sekretów", "zwykły tekst bez sekretów")]
    public void MaskowanieWartosci(string wejscie, string oczekiwane) => Assert.Equal(oczekiwane, Redakcja.Tekst(wejscie));

    [Fact]
    public void SterujaceIPrzyciecie()
    {
        Assert.Equal("a b c d", Redakcja.Tekst("a\rb\nc\u2028d"));
        Assert.Equal("a\r\nb c", Redakcja.Sterujace("a\r\nb\u0000c", true));
        var t = Redakcja.Tekst(new string('a', 511) + "😀");
        Assert.Equal(KontraktLogu.MaxZnakowTekstu, t.Length);
        Assert.False(char.IsHighSurrogate(t[^2]));
        Assert.Null(Redakcja.Wyjatek(null));
    }
}

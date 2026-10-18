using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
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
            ZdarzeniaProcesu.Blad(log, new Exception("{\"password\":\"" + Z + "_JSON\",\"access_token\":\"" + Z + "_JSON_TOKEN\"}"), "TEST_PROBE");
            ZdarzeniaProcesu.Blad(log, new Exception("C:\\Users\\Jan Kowalski\\plik; [2001:db8:abcd::42]:47120"), "TEST_PROBE");
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
        Assert.DoesNotContain("Kowalski", tekst);
        Assert.DoesNotContain("2001:db8", tekst);
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
    [InlineData("C:\\Users\\Jan Kowalski\\AppData\\Local\\SUK", "C:\\Users\\[UZYTKOWNIK]\\AppData\\Local\\SUK")]
    [InlineData("C:\\Documents and Settings\\Jan Kowalski\\plik", "C:\\Documents and Settings\\[UZYTKOWNIK]\\plik")]
    [InlineData("/home/kacper/plik", "/home/[UZYTKOWNIK]/plik")]
    [InlineData("/Users/Jan Kowalski/plik", "/Users/[UZYTKOWNIK]/plik")]
    [InlineData("połączenie 192.168.1.5:47120 odrzucone", "połączenie [IP]:47120 odrzucone")]
    [InlineData("połączenie 2001:db8:abcd::42 odrzucone", "połączenie [IP] odrzucone")]
    [InlineData("[2001:db8::42]:47120", "[IP]:47120")]
    [InlineData("[::ffff:192.168.1.5]:47120", "[IP]:47120")]
    [InlineData("fe80::1%12", "[IP]")]
    [InlineData("Godzina 12:34:56; MAC aa:bb:cc:dd:ee:ff", "Godzina 12:34:56; MAC aa:bb:cc:dd:ee:ff")]
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

    [Theory]
    [InlineData("{\"password\":\"SEKRET_TESTOWY_JSON\"}")]
    [InlineData("{\"access_token\": \"SEKRET_TESTOWY_JSON\"}")]
    [InlineData("{'apiKey': 'SEKRET_TESTOWY_JSON'}")]
    [InlineData("{\"password\":\"pierwsza \\\"SEKRET_TESTOWY_JSON\\\" końcówka\"}")]
    [InlineData("{\"password\":\"SEKRET_TESTOWY_JSON")]
    [InlineData("Authorization: Bearer abc")]
    [InlineData("Authorization: Basic YTpi")]
    public void SekretyWJsonIPrzyUcieciuSaMaskowane(string wejscie)
    {
        var tekst = Redakcja.Tekst(wejscie);
        var wyjatek = Redakcja.Wyjatek(new Exception(wejscie));
        Assert.Contains(Redakcja.Znacznik, tekst);
        Assert.DoesNotContain("SEKRET_TESTOWY", tekst);
        Assert.DoesNotContain("SEKRET_TESTOWY", wyjatek);
        Assert.DoesNotContain("abc", tekst);
        Assert.DoesNotContain("YTpi", tekst);
    }

    [Fact]
    public void DuzeWejscieNieWymuszaKopiiCalegoTekstu()
    {
        _ = Redakcja.Tekst("rozgrzewka\0");
        _ = Redakcja.Wyjatek(new Exception("rozgrzewka"));
        var tekst = "\0" + new string('x', 4_000_000);
        var ex = new Exception(tekst);
        long poczatek = GC.GetAllocatedBytesForCurrentThread();
        var t = Redakcja.Tekst(tekst);
        var w = Redakcja.Wyjatek(ex);
        long alokacje = GC.GetAllocatedBytesForCurrentThread() - poczatek;
        Assert.Equal(KontraktLogu.MaxZnakowTekstu, t.Length);
        Assert.True(w.Length <= KontraktLogu.MaxZnakowWyjatku);
        Assert.True(alokacje < 128 * 1024, $"Kopia dużego wejścia: {alokacje} B");
    }

    [Fact]
    public void WyjatekNieFormatujeNieograniczonegoGrafuAniNadpisanegoToString()
    {
        var wew = new BezToString("password=" + Z);
        var graf = new AggregateException(Enumerable.Repeat<Exception>(wew, 10_000));
        string w = Redakcja.Wyjatek(graf);
        Assert.Contains(nameof(BezToString), w);
        Assert.Contains(Redakcja.Znacznik, w);
        Assert.DoesNotContain(Z, w);
        Assert.True(w.Length <= KontraktLogu.MaxZnakowWyjatku);
        Assert.False(wew.WywolanoToString);
    }

    [Fact]
    public void WyjatekZachowujeMiejsceBleduBezSciezekPlikowych()
    {
        var w = Redakcja.Wyjatek(RzucWyjatek());
        Assert.Contains(nameof(RzucWyjatek), w);
        Assert.Contains("błąd kontrolowany", w);
        Assert.DoesNotContain(".cs:line", w);
    }

    [Fact]
    public void GlebokiStosNieJestMaterializowanyPrzezRedakcje()
    {
        _ = Redakcja.Wyjatek(GlebokiWyjatek(1));
        var ex = GlebokiWyjatek(2000); // samo rzucenie poza pomiarem; mierzymy wyłącznie adapter
        long przed = GC.GetAllocatedBytesForCurrentThread();
        string w = Redakcja.Wyjatek(ex);
        long alokacje = GC.GetAllocatedBytesForCurrentThread() - przed;
        Assert.Contains(nameof(RzucGleboko), w);
        Assert.True(alokacje < 32 * 1024, $"Materializacja głębokiego stosu: {alokacje} B");
    }

    [Theory]
    [InlineData("2001:db8:1234:5678:90ab:cdef:1234:")]
    [InlineData("[2001:db8:1234:5678:90ab:cdef:1234:")]
    public void Ipv6UcietyDokladnieNaGranicyNieUjawniaPrefiksu(string adres)
    {
        // Znak zamykający adres wypada poza pobierany fragment, tuż przed znacznikiem cięcia.
        // Długi sekret przed adresem znika podczas redakcji: prefiks IPv6 pojawiłby się wtedy
        // także w krótkim polu 512 znaków, mimo że pierwotnie leżał przy granicy 4096.
        const string klucz = "password=\"";
        string tekst = klucz + new string('x', Redakcja.MaxZnakowWejscia - 1 - klucz.Length - 2 - adres.Length) + "\" " + adres + "abcd";
        var t = Redakcja.Tekst(tekst);
        Assert.DoesNotContain("2001:db8", t);
        Assert.Contains("[IP]", t);
        string naglowek = typeof(Exception).FullName + ": ";
        string wiadomosc = new string(' ', Redakcja.MaxZnakowWejscia - 1 - naglowek.Length - adres.Length) + adres + "abcd";
        var w = Redakcja.Wyjatek(new Exception(wiadomosc));
        Assert.DoesNotContain("2001:db8", w);
        Assert.Contains("[IP]", w);
        Assert.Equal("Godzina 12:34:56", Redakcja.Tekst("Godzina 12:34:56"));
        Assert.Equal("Godzina 12:34:56…", Redakcja.Tekst("Godzina 12:34:56…"));
    }

    static Exception GlebokiWyjatek(int glebokosc)
    {
        try { RzucGleboko(glebokosc); }
        catch (Exception ex) { return ex; }
        throw new InvalidOperationException("Nie rzucono wyjątku testowego");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void RzucGleboko(int n)
    {
        if (n == 0) throw new InvalidOperationException("głęboki błąd kontrolowany");
        RzucGleboko(n - 1);
        GC.KeepAlive(n); // blokuje optymalizację do wywołania ogonowego
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static Exception RzucWyjatek()
    {
        try { throw new InvalidOperationException("błąd kontrolowany"); }
        catch (Exception ex) { return ex; }
    }

    sealed class BezToString(string tekst) : Exception(tekst)
    {
        public bool WywolanoToString;
        public override string ToString() { WywolanoToString = true; throw new InvalidOperationException("Nie wolno formatować całego wyjątku"); }
    }
}

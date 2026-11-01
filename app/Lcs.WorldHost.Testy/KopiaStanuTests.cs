using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LcsWidzew.Model;
using LcsWidzew.Sim;
using Xunit;

namespace Lcs.WorldHost.Testy;

/// <summary>
/// S02A.03 (wydajność biernego obliczenia): pula kopii stanu KZ, nakładka znanych statusów, szybka ścieżka KanonJson
/// i SHA-256 z bufora. Każda optymalizacja ma dawać dokładnie ten sam stan, kolejność i bajty co prosta wersja.
/// </summary>
public sealed class KopiaStanuTests
{
    const BindingFlags Wszystkie = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly JsonSerializerOptions Json = new() { IncludeFields = true };
    static string Fixture => Path.Combine(AppContext.BaseDirectory, "TestData", "station_test.json");
    static Simulator New() => new(Station.Load(Fixture));

    // ------------------------------------------------------------------ Kopiuj: każde pole, bez wspólnych kontenerów

    static IEnumerable<FieldInfo> Pola(Type t)
    {
        for (var x = t; x != null && x != typeof(object); x = x.BaseType)
            foreach (var f in x.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                if (!f.IsInitOnly && !f.IsLiteral) yield return f;
    }

    /// <summary>Wartość różna od domyślnej i zależna od ziarna, dla każdego typu pola stanu KZ.</summary>
    static object? Wartosc(Type t, string nazwa, int ziarno)
    {
        string tekst = $"{nazwa}_{ziarno}";
        if (t == typeof(string)) return tekst;
        if (t == typeof(bool)) return ziarno % 2 == 1;
        if (t == typeof(int)) return 7 + ziarno;
        if (t == typeof(long)) return 70L + ziarno;
        if (t == typeof(double)) return 3.5 + ziarno;
        if (t == typeof(int?)) return (int?)(5 + ziarno);
        if (t == typeof(double?)) return (double?)(1.25 + ziarno);
        if (t == typeof(bool?)) return (bool?)(ziarno % 2 == 0);
        if (t.IsEnum) { var v = Enum.GetValues(t); return v.GetValue(ziarno % v.Length); }
        if (t == typeof(bool[])) return new[] { ziarno % 2 == 1, ziarno % 2 == 0, true };
        if (t == typeof(HashSet<string>)) return new HashSet<string> { tekst, tekst + "b" };
        if (t == typeof(List<string>)) return new List<string> { tekst, tekst + "b" };
        if (t == typeof(List<(string, string)>)) return new List<(string, string)> { (tekst, "+"), (tekst + "b", "-") };
        if (t == typeof(List<(string, string, string, string)>)) return new List<(string, string, string, string)> { ("sw", tekst, "+", "x") };
        if (t == typeof(List<ZalOchronna>)) return new List<ZalOchronna> { new() { Odcinki = { tekst } } };
        if (t == typeof(MoveInfo) || t == typeof(BlockReq)) return Wypelnij(Activator.CreateInstance(t)!, ziarno);
        throw new InvalidOperationException($"Test nie zna typu pola {nazwa}: {t} – dopisz go tutaj i do Kopiuj w KopiaStanu.cs.");
    }

    static object Wypelnij(object o, int ziarno)
    {
        foreach (var f in Pola(o.GetType())) f.SetValue(o, Wartosc(f.FieldType, f.Name, ziarno));
        return o;
    }

    public static TheoryData<Type> TypyStanu => new() { typeof(SecState), typeof(SwState), typeof(SigState), typeof(DerState), typeof(BlockState), typeof(PowerState), typeof(Route) };

    [Theory]
    [MemberData(nameof(TypyStanu))]
    public void KopiujNadpisujeKazdePoleIDajeWlasneKontenery(Type typ)
    {
        var kopiuj = typeof(KomputerZaleznosci).GetMethod("Kopiuj", Wszystkie, new[] { typ, typ });
        Assert.NotNull(kopiuj);
        foreach (var (zrodlo, cel) in new[] { (Wypelnij(Activator.CreateInstance(typ)!, 1), Wypelnij(Activator.CreateInstance(typ)!, 2)),
                                              (Activator.CreateInstance(typ)!, Wypelnij(Activator.CreateInstance(typ)!, 3)) })
        {
            // Obiekt z puli ma wartości z poprzedniego obliczenia – kopia musi nadpisać KAŻDE pole (także null w MoveInfo/BlockReq).
            kopiuj!.Invoke(null, new[] { zrodlo, cel });
            Assert.Equal(JsonSerializer.Serialize(zrodlo, typ, Json), JsonSerializer.Serialize(cel, typ, Json));
            foreach (var f in Pola(typ))
            {
                var a = f.GetValue(zrodlo); var b = f.GetValue(cel);
                // Kontenery i obiekty zmienne nie mogą być wspólne (zmiana kopii nie może zmienić świata).
                if (a is IEnumerable and not string || a is MoveInfo or BlockReq) Assert.False(ReferenceEquals(a, b), $"{typ.Name}.{f.Name} współdzielone");
            }
        }
    }

    // ------------------------------------------------------------------ pula kopii: ten sam stan i kolejność co nowa kopia

    static string Stan(KomputerZaleznosci k) => JsonSerializer.Serialize(new { k.Secs, k.Sws, k.Sigs, k.Ders, k.Blocks, k.Routes, k.Power, k.SssPosts, k.ZssPosts }, Json);
    static KomputerZaleznosci Kopia(KomputerZaleznosci kz) => (KomputerZaleznosci)typeof(KomputerZaleznosci).GetMethod("KopiaDoObliczen", Wszystkie)!.Invoke(kz, null)!;
    static void Zwolnij(KomputerZaleznosci kz, KomputerZaleznosci k, bool zatwierdzona) => typeof(KomputerZaleznosci).GetMethod("ZwolnijKopie", Wszystkie)!.Invoke(kz, new object[] { k, zatwierdzona });
    static object? Pula(KomputerZaleznosci kz) => typeof(KomputerZaleznosci).GetField("pula", Wszystkie)!.GetValue(kz);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MemoPierwszegoOdstepuNieWspoldzieliSlownikaAniListTakzeZPuli(bool zPuli)
    {
        var s = New();
        var bloki = s.L.Blocks.Values.Where(b => b.Ends.Count > 0).Take(2).ToArray();
        Assert.Equal(2, bloki.Length);
        var pole = typeof(KomputerZaleznosci).GetField("pierwszyOdstepCache", Wszystkie)!;
        Dictionary<(string, int), List<string>> Memo(KomputerZaleznosci k)
            => (Dictionary<(string, int), List<string>>)pole.GetValue(k)!;
        var listaSwiata = s.Kz.PierwszyOdstep(bloki[0], 0);
        if (zPuli)
        {
            var poprzednia = Kopia(s.Kz);
            s.Kz.PrzyjmijStan(poprzednia);
            Zwolnij(s.Kz, poprzednia, zatwierdzona: true);
        }
        var kopia = Kopia(s.Kz);
        Assert.NotSame(Memo(s.Kz), Memo(kopia));
        var listaKopii = kopia.PierwszyOdstep(bloki[0], 0);
        Assert.Equal(listaSwiata, listaKopii);
        Assert.NotSame(listaSwiata, listaKopii);
        listaKopii.Add("TYLKO_KOPIA");
        kopia.PierwszyOdstep(bloki[1], 0);
        Assert.DoesNotContain("TYLKO_KOPIA", listaSwiata);
        Assert.False(Memo(s.Kz).ContainsKey((bloki[1].Id, 0)));
        Zwolnij(s.Kz, kopia, zatwierdzona: false);

        var przyjeta = Kopia(s.Kz);
        var nowaLista = przyjeta.PierwszyOdstep(bloki[1], 0);
        s.Kz.PrzyjmijStan(przyjeta);
        Assert.Equal(nowaLista, Memo(s.Kz)[(bloki[1].Id, 0)]);
        Assert.NotSame(nowaLista, Memo(s.Kz)[(bloki[1].Id, 0)]);
        nowaLista.Add("PO_PRZYJECIU");
        Assert.DoesNotContain("PO_PRZYJECIU", Memo(s.Kz)[(bloki[1].Id, 0)]);
    }

    [Fact]
    public void KopiaZPuliJestTakaJakNowaIJakSwiatTakzePoZmianiePrzebiegow()
    {
        var s = New();
        for (int i = 0; i < 5; i++) s.Tick(0.1);
        var trasy = s.RouteTable.Values.SelectMany(x => x).Where(r => r.Type == "P" && r.EndKind == "sig").ToList();
        int nastawione = 0;
        foreach (var r in trasy)
        {
            if (s.WhyNot(r).Count != 0) continue;
            var plan = s.Kz.Evaluate(KrokKz.Polecenie, new PolecenieSystemowe("POCIAG", new[] { r.Start, r.End }, 0, r.Start));
            Assert.Equal("Executed", s.Wyjscia.Zatwierdz(plan).Stan);
            if (plan.Wynik.Ok && ++nastawione == 3) break;
            s.Tick(0.1);
        }
        Assert.NotEmpty(s.Routes);
        for (int i = 0; i < 30; i++)
        {
            s.Tick(0.1);
            Assert.NotNull(Pula(s.Kz)); // po zatwierdzeniu kopia czeka jako pula
            var zPuli = Kopia(s.Kz); string a = Stan(zPuli);
            Zwolnij(s.Kz, zPuli, zatwierdzona: false); Assert.Null(Pula(s.Kz)); // odrzucona kopia nie wraca do puli
            var nowa = Kopia(s.Kz); string b = Stan(nowa);
            Assert.Equal(Stan(s.Kz), a); Assert.Equal(a, b);
            Zwolnij(s.Kz, nowa, zatwierdzona: true);
        }
    }

    [Fact]
    public void SynchronizacjaZachowujeKolejnoscSwiataIPonownieUzywaObiektowPuli()
    {
        var sync = typeof(KomputerZaleznosci).GetMethod("Synchronizuj", Wszystkie)!.MakeGenericMethod(typeof(SecState));
        var kopiuj = (Action<SecState, SecState>)Delegate.CreateDelegate(typeof(Action<SecState, SecState>),
            typeof(KomputerZaleznosci).GetMethod("Kopiuj", Wszystkie, new[] { typeof(SecState), typeof(SecState) })!);
        var swiat = new Dictionary<string, SecState> { ["A"] = new() { Id = "A", Lock = "x" }, ["B"] = new() { Id = "B" }, ["C"] = new() { Id = "C", TrainOcc = true } };
        var pula = new Dictionary<string, SecState> { ["B"] = new() { Id = "stary" }, ["A"] = new() { Id = "stary", Fault = "occ" }, ["D"] = new() };
        var obiektB = pula["B"];
        var wynik = (Dictionary<string, SecState>)sync.Invoke(null, new object[] { swiat, pula, kopiuj })!;
        Assert.Equal(new[] { "A", "B", "C" }, wynik.Keys);                           // kolejność świata, nie puli
        Assert.Same(obiektB, wynik["B"]);                                           // obiekt puli użyty ponownie
        Assert.Equal(JsonSerializer.Serialize(swiat, Json), JsonSerializer.Serialize(wynik, Json));
        Assert.All(wynik, p => Assert.NotSame(swiat[p.Key], p.Value));            // nigdy obiekt świata
        // Te same klucze w tej samej kolejności: ten sam kontener, tylko wartości.
        swiat["A"].Lock = "y";
        var drugi = (Dictionary<string, SecState>)sync.Invoke(null, new object[] { swiat, wynik, kopiuj })!;
        Assert.Same(wynik, drugi); Assert.Equal("y", drugi["A"].Lock);
        // Ten sam zbiór kluczy w innej kolejności: nowy słownik w kolejności świata (od niej zależy kolejność skutków).
        var odwrotna = new Dictionary<string, SecState> { ["C"] = new(), ["B"] = new(), ["A"] = new() };
        var trzeci = (Dictionary<string, SecState>)sync.Invoke(null, new object[] { swiat, odwrotna, kopiuj })!;
        Assert.Equal(new[] { "A", "B", "C" }, trzeci.Keys); Assert.NotSame(odwrotna, trzeci); Assert.Same(odwrotna["B"], trzeci["B"]);
    }

    [Fact]
    public void KopiaPrzebiegowNieDziedziczyWolnychSlotowSlownikaPuli()
    {
        var s = New();
        foreach (var id in new[] { "A", "B", "C" }) s.Routes[id] = new Route { Uid = id };
        var poprzednia = Kopia(s.Kz);
        poprzednia.Routes.Clear();
        foreach (var id in new[] { "X", "A", "Y", "B", "C" }) poprzednia.Routes[id] = new Route { Uid = id };
        poprzednia.Routes.Remove("X"); poprzednia.Routes.Remove("Y");
        Assert.Equal(s.Routes.Keys, poprzednia.Routes.Keys); // aktywne ABC, lecz układ dziur kontenerów jest inny
        Zwolnij(s.Kz, poprzednia, zatwierdzona: true);
        var nastepna = Kopia(s.Kz);
        s.Routes["D"] = new Route { Uid = "D" };
        nastepna.Routes["D"] = new Route { Uid = "D" };
        Assert.Equal(s.Routes.Keys, nastepna.Routes.Keys); // ABCD; stary kontener puli dałby ADBC
        Assert.NotSame(s.Routes["A"], nastepna.Routes["A"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KopiaZbioruResetujeHistorieWolnychSlotowTakzeGdyJestPusty(bool pusty)
    {
        var zrodlo = pusty ? new HashSet<string>() : new HashSet<string> { "A", "C" };
        var cel = new HashSet<string> { "A", "B", "C" };
        if (pusty) { cel.Remove("A"); cel.Remove("B"); cel.Remove("C"); }
        else cel.Remove("B");
        Assert.Equal(zrodlo.ToArray(), cel.ToArray());
        var kopiuj = typeof(KomputerZaleznosci).GetMethod("Zbior", Wszystkie)!.MakeGenericMethod(typeof(string));
        kopiuj.Invoke(null, new object[] { zrodlo, cel });
        zrodlo.Add("D"); cel.Add("D"); zrodlo.Add("E"); cel.Add("E");
        Assert.Equal(zrodlo.ToArray(), cel.ToArray()); // kolejny właściciel Shared.First() też musi być zgodny
    }

    [Fact]
    public void ZnaneStatusyOdrzuconegoObliczeniaNieTrafiajaDoSwiata()
    {
        var s = New(); s.Tick(0.1);
        var znane = (Dictionary<string, string>)typeof(KomputerZaleznosci).GetField("znaneStatusy", Wszystkie)!.GetValue(s.Kz)!;
        string przed = JsonSerializer.Serialize(znane);
        var partia = new PartiaWejsc { NrPartii = 1, Faza = FazaWejscia.Karty, Statusy = System.Collections.Immutable.ImmutableArray.Create<StatusUrzadzenia>(
            new StatusZasilania { Obiekt = Zasilanie.Id, Siec = false, Ups = true, NapiecieNastawcze = true }) };
        var p = s.Kz.Evaluate(KrokKz.Statusy, partia: partia);
        Assert.Contains(p.Efekty, e => e.Rodzaj == RodzajEfektu.Status);
        Assert.Equal(przed, JsonSerializer.Serialize(znane));                        // obliczenie pisze tylko do delty
        s.Wyjscia.Odrzuc(p);
        Assert.Equal(przed, JsonSerializer.Serialize(znane));
        var q = s.Kz.Evaluate(KrokKz.Statusy, partia: partia);
        Assert.Contains(q.Efekty, e => e.Rodzaj == RodzajEfektu.Status);           // po odrzuceniu status nadal jest nowy
        s.Wyjscia.Zatwierdz(q);
        Assert.NotEqual(przed, JsonSerializer.Serialize(znane));                     // przyjęcie nałożyło deltę
        var r = s.Kz.Evaluate(KrokKz.Statusy, partia: partia);
        Assert.DoesNotContain(r.Efekty, e => e.Rodzaj == RodzajEfektu.Status && e.Obiekt == Zasilanie.Id); // ten sam status – bez wpisu
        s.Wyjscia.Odrzuc(r);
    }

    // ------------------------------------------------------------------ KanonJson i SHA-256 z bufora: te same bajty

    /// <summary>Wersja KanonJson.Tekst sprzed S02A.03 (pętla po znakach) – wzorzec dla szybkiej ścieżki.</summary>
    static void TekstWzorzec(StringBuilder sb, string? s)
    {
        if (s == null) { sb.Append("null"); return; }
        const string Hex = "0123456789abcdef";
        sb.Append('"'); int start = 0;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            string? krotka = c switch { '"' => "\\\"", '\\' => "\\\\", '\b' => "\\b", '\t' => "\\t", '\n' => "\\n", '\f' => "\\f", '\r' => "\\r", _ => null };
            bool samotny = char.IsHighSurrogate(c) ? i + 1 >= s.Length || !char.IsLowSurrogate(s[i + 1]) : char.IsLowSurrogate(c) && (i == 0 || !char.IsHighSurrogate(s[i - 1]));
            if (krotka == null && c >= 0x20 && !samotny) continue;
            sb.Append(s, start, i - start);
            if (krotka != null) sb.Append(krotka); else sb.Append("\\u").Append(Hex[c >> 12]).Append(Hex[(c >> 8) & 0xF]).Append(Hex[(c >> 4) & 0xF]).Append(Hex[c & 0xF]);
            start = i + 1;
        }
        sb.Append(s, start, s.Length - start).Append('"');
    }

    [Fact]
    public void SzybkaSciezkaKanonJsonDajeTeSameBajtyCoPetlaPoZnakach()
    {
        var paleta = Enumerable.Range(0, 0x21).Select(i => (char)i)
            .Concat("\"\\/aZ:|\u0105\u0141\u20ac\u007f\u0085\u2028\u2029\ufeff\ud83d\ude00\ud800\udfff\udbff\udc00".ToCharArray()).ToArray();
        var losowe = new Random(20261028);
        var proby = new List<string?> { null, "", "LW:6", "zwrotnica|LW:6", "a\"b", "\\", "\n", "\u0000", "\ud83d\ude00", "\ud83d", "\ude00x", "x\ud83d", "\ud83d\ude00\u0105\u0119" };
        for (int i = 0; i < 5000; i++) proby.Add(new string(Enumerable.Range(0, losowe.Next(0, 24)).Select(_ => paleta[losowe.Next(paleta.Length)]).ToArray()));
        foreach (var s in proby)
        {
            var a = new StringBuilder(); var b = new StringBuilder();
            KanonJson.Tekst(a, s); TekstWzorzec(b, s);
            Assert.Equal(b.ToString(), a.ToString());
        }
    }

    [Fact]
    public void Sha256ZBuforaJestTakiSamJakZNapisuTakzePrzyParzeSurogatowNaStykuFragmentow()
    {
        var losowe = new Random(7);
        var paleta = "abc\u0105\u20ac\u2028\ud83d\ude00\ud800".ToCharArray();
        for (int proba = 0; proba < 300; proba++)
        {
            var sb = new StringBuilder(16);
            int n = losowe.Next(0, 40);
            for (int i = 0; i < n; i++) sb.Append(new string(Enumerable.Range(0, losowe.Next(1, 600)).Select(_ => paleta[losowe.Next(paleta.Length)]).ToArray()));
            string oczekiwany = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
            Assert.Equal(oczekiwany, KanonJson.Sha256(sb));
        }
        // Para surogatów rozdzielona granicą fragmentów bufora.
        var rozdzielona = new StringBuilder(16).Append(new string('a', 15)).Append("\ud83d\ude00").Append("koniec");
        var fragmenty = new List<ReadOnlyMemory<char>>(); foreach (var f in rozdzielona.GetChunks()) fragmenty.Add(f);
        Assert.True(fragmenty.Count > 1 && char.IsHighSurrogate(fragmenty[0].Span[^1]));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rozdzielona.ToString()))), KanonJson.Sha256(rozdzielona));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Array.Empty<byte>())), KanonJson.Sha256(new StringBuilder()));
    }
}

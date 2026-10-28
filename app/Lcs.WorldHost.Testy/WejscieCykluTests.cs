using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Lcs.Logi;
using LcsWidzew.Model;
using LcsWidzew.Sim;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using M = LcsWidzew.Sim.MapaWlasnosci;

namespace Lcs.WorldHost.Testy;

/// <summary>
/// S02A.02 – zamrożone, numerowane wejście cyklu: statusy kart tylko przez rejestr i jawne fazy, rozsyłanie według mapy własności,
/// te same wejścia dla wszystkich testowych odbiorców KZ, brak callbacków i mutowalnych referencji, zachowanie symulacji bez zmian, logi F24.
/// </summary>
public sealed class WejscieCykluTests
{
    static string Fixture => Path.Combine(AppContext.BaseDirectory, "TestData", "station_test.json");
    static Station Load() => Station.Load(Fixture);

    sealed class Zapis : IOdbiorcaWejscCyklu
    {
        public readonly List<WejscieCyklu> Wejscia = new();
        public void Odbierz(WejscieCyklu wejscie) => Wejscia.Add(wejscie);
    }

    sealed class Wadliwy : IOdbiorcaWejscCyklu
    {
        public int Wywolania;
        public void Odbierz(WejscieCyklu wejscie) { Wywolania++; throw new InvalidOperationException("password=" + ProbaLogow.ZnacznikSekretu + "_ODBIORCA"); }
    }

    /// <summary>Odbiorca próbujący wejść do świata: usterka instruktora (tryb 0) albo zgłoszenie statusu karty (tryb 1).</summary>
    sealed class Wtracajacy(Simulator s, int tryb) : IOdbiorcaWejscCyklu
    {
        public int Wywolania;
        public Exception? Blad;
        public void Odbierz(WejscieCyklu wejscie)
        {
            Wywolania++;
            try { if (tryb == 0) s.Instruktor.ApplyFault("mains", null, false); else s.Urzadzenia.Odcinki.Values.First().Publikuj(); }
            catch (Exception ex) { Blad = ex; throw; }
        }
    }

    static Simulator Scenariusz(string scenario, out Trains trains, SimulatorOptions? options = null)
    {
        var sim = new Simulator(Load(), options: options) { User = new User { Name = "test", Role = "dyżurny ruchu" } };
        trains = new Trains(sim); _ = Sygnaly.Tab; _ = trains.Tabor; trains.Load(scenario);
        return sim;
    }

    static Dictionary<string, (Zapis A, Zapis B)> Odbiorcy(Simulator sim)
    {
        var res = new Dictionary<string, (Zapis, Zapis)>(StringComparer.Ordinal);
        foreach (var kz in sim.Wejscia.Trasa!.KzIds)
        {
            var a = new Zapis(); var b = new Zapis();
            sim.Wejscia.Subskrybuj(kz, a); sim.Wejscia.Subskrybuj(kz, b);
            res[kz] = (a, b);
        }
        return res;
    }

    [Fact]
    public void WejscieIStatusySaNiezmienneBezDelegatowIReferencjiDoStanu()
    {
        var typy = new[] { typeof(WejscieCyklu), typeof(PartiaWejsc), typeof(StatusUrzadzenia), typeof(StatusOdcinka), typeof(StatusZwrotnicy),
            typeof(StatusWykolejnicy), typeof(StatusSygnalizatora), typeof(StatusZasilania), typeof(TrasaWejsc.Wpis) };
        var dozwolone = new HashSet<Type> { typeof(string), typeof(bool), typeof(bool?), typeof(long), typeof(int), typeof(FazaWejscia), typeof(Type),
            typeof(ImmutableArray<StatusUrzadzenia>), typeof(ImmutableArray<WejscieCyklu>), typeof(ImmutableArray<string>), typeof(ImmutableArray<int>) };
        foreach (var t in typy)
        {
            foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                Assert.Contains(p.PropertyType, dozwolone);
                Assert.False(typeof(Delegate).IsAssignableFrom(p.PropertyType));
                // tylko init: setter (jeśli jest) ma modyfikator IsExternalInit – po utworzeniu obiekt jest niezmienny
                if (p.SetMethod is { } set) Assert.Contains(typeof(IsExternalInit), set.ReturnParameter.GetRequiredCustomModifiers());
            }
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                Assert.True(f.IsInitOnly, $"{t.Name}.{f.Name} musi być readonly");
        }
        // żadnej referencji do kart, terenu, pociągów, Simulator ani KZ w kontrakcie wejścia
        Assert.DoesNotContain(typy.SelectMany(t => t.GetProperties()), p => p.PropertyType == typeof(Simulator) || p.PropertyType == typeof(KomputerZaleznosci));
        // KZ nie wystawia publicznych metod statusów – karty nie mogą go wywołać z pominięciem rejestru
        Assert.DoesNotContain(typeof(KomputerZaleznosci).GetMethods(BindingFlags.Instance | BindingFlags.Public), m => m.Name.StartsWith("Status", StringComparison.Ordinal));
        Assert.DoesNotContain(typeof(KomputerZaleznosci).GetMethods(BindingFlags.Instance | BindingFlags.Public), m => m.Name == "Przyjmij");
        // odbiorca nie ma drogi zwrotnej: interfejs ma jedną metodę bez wyniku
        var m = Assert.Single(typeof(IOdbiorcaWejscCyklu).GetMethods());
        Assert.Equal(typeof(void), m.ReturnType); Assert.Equal(typeof(WejscieCyklu), Assert.Single(m.GetParameters()).ParameterType);
        // tablice niemutowalne i bez możliwości zmiany przez rzutowanie
        var w = new WejscieCyklu { KzId = "KZ-X", Statusy = ImmutableArray.Create<StatusUrzadzenia>(new StatusOdcinka { Obiekt = "A", Pojazd = true }) };
        Assert.Throws<NotSupportedException>(() => ((IList<StatusUrzadzenia>)w.Statusy)[0] = new StatusOdcinka { Obiekt = "B" });
    }

    [Fact]
    public void StatusKartyTrafiaDoKzDopieroNaGranicyFazy()
    {
        var sim = new Simulator(Load());
        var t = sim.Wejscia.Trasa!; var zapis = new Zapis();
        string sec = sim.L.Sections.Keys.OrderBy(k => k, StringComparer.Ordinal).First();     // usterka secOcc: „occ” (obwód torowy i licznik osi)
        string kz = t.WpisObiektu(M.Rodzaj.Odcinek, sec)!.Odbiorcy[0];
        sim.Wejscia.Subskrybuj(kz, zapis);
        using (sim.Wejscia.Faza(FazaWejscia.Karty))                                    // faza regularna: zamrożenie na końcu fazy
        {
            Assert.True(sim.Urzadzenia.Odcinki[sec].Usterka("secOcc", false));
            Assert.Null(sim.Secs[sec].Fault);                                           // karta nie pisze do KZ – zgłoszenie czeka na granicę
            Assert.Empty(zapis.Wejscia);
        }
        Assert.Equal("occ", sim.Secs[sec].Fault);                                      // granica fazy: KZ przyjął zamrożony status
        var w = Assert.Single(zapis.Wejscia);
        Assert.Equal(FazaWejscia.Karty, w.Faza); Assert.Equal(kz, w.KzId); Assert.Equal(1, w.NrWejscia); Assert.Equal(0, w.Cykl);
        var s = Assert.IsType<StatusOdcinka>(Assert.Single(w.Statusy));
        Assert.Equal(sec, s.Obiekt); Assert.Equal("occ", s.Stan); Assert.False(s.Pojazd);
        Assert.Equal(t.Sha256, w.SkrotTrasy);
        // późniejsza zmiana karty nie zmienia już wydanego wejścia
        sim.Urzadzenia.Odcinki[sec].Stan = null;
        Assert.Equal("occ", ((StatusOdcinka)w.Statusy[0]).Stan);
    }

    [Fact]
    public void FazyNatychmiastoweDoreczajaStatusPrzedAlarmemIWpisemKarty()
    {
        // bazowa kolejność (19 §6, recenzja S02A.02 ust. 1): w chwili alarmu/wpisu karty KZ zna już jej status (i wpis KZ↔karty),
        // a przy końcu przejazdu – zajętość wszystkich odcinków z tego samego ruchu
        var sim = new Simulator(Load());
        var ac = sim.L.Sections.Values.Where(s => s.AxleCounter).Select(s => s.Id).OrderBy(x => x, StringComparer.Ordinal).Take(3).ToArray();
        var t = sim.Wejscia.Trasa!;
        var zapis = t.KzIds.ToDictionary(k => k, _ => new Zapis(), StringComparer.Ordinal);
        foreach (var (k, z) in zapis) sim.Wejscia.Subskrybuj(k, z);
        var widziane = new List<string>();
        sim.AlarmAdded += a =>
        {
            if (a.Obiekt == ac[0] && a.Zdarzenie.Contains("zajętość bez pojazdu", StringComparison.Ordinal))
                widziane.Add($"usterka:{sim.SecOcc(ac[0])}:{sim.Secs[ac[0]].Fault}:{sim.Urzadzenia.Dziennik[^1].Obiekt}");
            if (a.Obiekt == ac[1] && a.Zdarzenie.Contains("niezgodność", StringComparison.Ordinal))
                widziane.Add($"zliczenie:{sim.Secs[ac[1]].Fault}:{sim.Secs[ac[2]].TrainOcc}");
        };
        sim.EventAdded += e =>
        {
            if (e.Obiekt == ac[1] && e.Zdarzenie.StartsWith("Pierwszy przejazd", StringComparison.Ordinal)) widziane.Add($"przejazd:{sim.Secs[ac[1]].Fault ?? "null"}");
        };
        sim.Tick(0.25); sim.Urzadzenia.Osie(new HashSet<string>());                      // pierwsze statusy (dziennik KZ↔karty notuje zmiany)
        sim.Instruktor.ApplyFault("secOcc", ac[0], false);                               // USTERKA
        sim.Instruktor.ApplyFault("loMiscount", ac[1], false);
        sim.Urzadzenia.Osie(new HashSet<string> { ac[1] });                              // przejazd przez ac[1]
        sim.Urzadzenia.Osie(new HashSet<string> { ac[2] });                              // koniec przejazdu (błąd zliczenia) i zajęty ac[2] w tym samym ruchu
        sim.Urzadzenia.Zeruj(ac[1]);                                                     // ROZKAZ (ZeroLO)
        Assert.Equal("await", sim.Secs[ac[1]].Fault);
        sim.Urzadzenia.Osie(new HashSet<string> { ac[1] });
        sim.Urzadzenia.Osie(new HashSet<string>());                                      // pierwszy przejazd zakończony
        Assert.Equal(new[] { $"usterka:True:occ:{ac[0]}", "zliczenie:reset:True", "przejazd:null" }, widziane);
        var wszystkie = zapis.Values.SelectMany(z => z.Wejscia).ToList();
        Assert.Contains(wszystkie, w => w.Faza == FazaWejscia.ZdarzenieKarty && w.Statusy.Any(s => s is StatusOdcinka o && o.Obiekt == ac[1] && o.Stan == "reset"));
        Assert.Contains(wszystkie, w => w.Faza == FazaWejscia.ZdarzenieKarty && w.Statusy.Any(s => s is StatusOdcinka o && o.Obiekt == ac[1] && o.Stan == null));
        Assert.Contains(wszystkie, w => w.Faza == FazaWejscia.Rozkaz && w.Statusy.Any(s => s is StatusOdcinka o && o.Obiekt == ac[1] && o.Stan == "await"));
        Assert.Contains(wszystkie, w => w.Faza == FazaWejscia.Usterka && w.Statusy.Any(s => s is StatusOdcinka o && o.Obiekt == ac[0] && o.Stan == "occ"));
        // natychmiastowe wejście = jedno zgłoszenie; regularna faza RUCH – zajętość wszystkich odcinków naraz
        Assert.All(wszystkie.Where(w => w.Faza is FazaWejscia.ZdarzenieKarty or FazaWejscia.Usterka or FazaWejscia.Rozkaz), w => Assert.Single(w.Statusy));
        var ruch = zapis.Values.SelectMany(z => z.Wejscia.Where(w => w.Faza == FazaWejscia.Ruch).Take(1)).SelectMany(w => w.Statusy).Select(x => x.Obiekt).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(sim.L.Sections.Count, ruch.Count);
        Assert.Equal(0, sim.Wejscia.Liczniki["violations"]);
    }

    [Fact]
    public void ZdarzenieKzWywolujaceUsterkeJestDoreczaneZagniezdzenieJakDawniej()
    {
        // subskrybent publicznego zdarzenia Simulator wprowadza usterkę w trakcie przyjmowania statusu przez KZ (recenzja, ust. 2)
        var sim = new Simulator(Load());
        string ac = sim.L.Sections.Values.Where(s => s.AxleCounter).Select(s => s.Id).OrderBy(x => x, StringComparer.Ordinal).First();
        string sw = sim.L.Switches.Values.Where(s => s.Sec != ac).Select(s => s.Num).OrderBy(x => x, StringComparer.Ordinal).First();
        var t = sim.Wejscia.Trasa!;
        var zapis = t.KzIds.ToDictionary(k => k, _ => new Zapis(), StringComparer.Ordinal);
        foreach (var (k, z) in zapis) sim.Wejscia.Subskrybuj(k, z);
        var widziane = new List<string>();
        sim.AlarmAdded += a =>
        {
            if (a.Obiekt == "SIEĆ") { sim.Instruktor.ApplyFault("secOcc", ac, false); widziane.Add("siec:" + sim.Secs[ac].Fault); }
            else if (a.TypObiektu == "Zwrotnica" && a.Zdarzenie.Contains("brak kontroli", StringComparison.Ordinal)) { sim.Instruktor.ApplyFault("secOcc", ac, true); widziane.Add("zwrotnica:" + sim.Secs[ac].Fault); }
        };
        sim.Tick(0.25);
        sim.Instruktor.ApplyFault("mains", null, false);       // USTERKA → alarm KZ → zagnieżdżona faza USTERKA (NESTED_PHASE)
        sim.Instruktor.ApplyFault("swNoCtrl", sw, false);      // status zwrotnicy w następnej fazie KARTY → alarm KZ w trakcie doręczania (REENTRANT_PHASE)
        sim.Tick(0.25);
        Assert.Equal(new[] { "siec:occ", "zwrotnica:reset" }, widziane);   // skutek od razu, jak dawne bezpośrednie wywołanie KZ
        var l = sim.Wejscia.Liczniki;
        Assert.Equal(1, l["nestedPhases"]); Assert.Equal(1, l["reentrantPhases"]); Assert.Equal(2, l["violations"]); Assert.Equal(0, l["deferredDeliveries"]);
        foreach (var (kz, z) in zapis)
        {
            Assert.Equal(Enumerable.Range(1, z.Wejscia.Count).Select(x => (long)x), z.Wejscia.Select(w => w.NrWejscia));
            Assert.All(z.Wejscia.Zip(z.Wejscia.Skip(1)), p => Assert.True(p.Second.NrPartii > p.First.NrPartii));
        }
        var lw = zapis[t.WpisObiektu(M.Rodzaj.Odcinek, ac)!.Odbiorcy[0]].Wejscia;
        int siec = lw.FindIndex(w => w.Statusy.Any(s => s is StatusZasilania p && !p.Siec));
        int occ = lw.FindIndex(w => w.Statusy.Any(s => s is StatusOdcinka o && o.Obiekt == ac && o.Stan == "occ"));
        Assert.True(siec >= 0 && occ == siec + 1);                              // zagnieżdżona partia zaraz po zewnętrznej
    }

    [Fact]
    public void OdbiorcaNieMaDrogiZwrotnejDoSwiata()
    {
        var sim = Scenariusz("podstawowy", out _); var wzor = Scenariusz("podstawowy", out _);
        var t = sim.Wejscia.Trasa!;
        var a = new Wtracajacy(sim, 0); var b = new Wtracajacy(sim, 1); var dobry = new Zapis();
        sim.Wejscia.Subskrybuj(t.KzIds[0], a); sim.Wejscia.Subskrybuj(t.KzIds[0], b); sim.Wejscia.Subskrybuj(t.KzIds[0], dobry);
        for (int i = 0; i < 100; i++) { sim.Tick(0.25); wzor.Tick(0.25); }
        Assert.Equal(1, a.Wywolania); Assert.IsType<InvalidOperationException>(a.Blad);
        Assert.Equal(1, b.Wywolania); Assert.IsType<InvalidOperationException>(b.Blad);
        Assert.Equal(2, sim.Wejscia.Liczniki["receiverFailures"]);
        Assert.Equal(200, dobry.Wejscia.Count);
        Assert.True(sim.Urzadzenia.Zasilanie.Siec);                             // usterka z odbiorcy nie weszła do terenu
        Assert.Equal(Dziennik(wzor), Dziennik(sim)); Assert.Equal(Stan(wzor), Stan(sim));
    }

    [Fact]
    public void PetlaZdarzenKzKonczySieZatrzymaniemRejestruBezPrzepelnieniaStosu()
    {
        // subskrybent, który na każde „Przywrócono zasilanie” znowu wyłącza i włącza sieć – w bazie nieskończona rekurencja
        WorldLog.Register(); Rejestruj();
        string directory = TestArtifacts.Directory("S02A02-petla");
        using var engine = SilnikLogow.Uruchom(new OpcjeLogow { Aplikacja = "Lcs.WorldHost", Katalog = directory, Profil = ProfilLogow.Serwer });
        var sim = new Simulator(Load(), options: new SimulatorOptions { WejscieCykluLogger = engine.Modul(WejscieCykluLog.Modul, WejscieCykluLog.Wersja) });
        sim.Tick(0.25);
        sim.Instruktor.ApplyFault("mains", null, false);
        int reakcje = 0;
        sim.EventAdded += e =>
        {
            if (e.Obiekt != "SIEĆ" || !e.Zdarzenie.StartsWith("Przywrócono zasilanie", StringComparison.Ordinal)) return;
            reakcje++;
            sim.Instruktor.ApplyFault("mains", null, false);
            sim.Instruktor.ApplyFault("mains", null, true);
        };
        var ex = Assert.Throws<InvalidOperationException>(() => sim.Instruktor.ApplyFault("mains", null, true));
        Assert.Contains("Pętla doręczeń", ex.Message, StringComparison.Ordinal);
        var l = sim.Wejscia.Liczniki;
        Assert.Equal(1, l["stopped"]); Assert.True(l["deferredDeliveries"] > 0);
        Assert.InRange(reakcje, RejestrWejscCyklu.MaxPartiiNaDoreczenie / 2 - 10, RejestrWejscCyklu.MaxPartiiNaDoreczenie + 10);
        Assert.Throws<InvalidOperationException>(() => sim.Tick(0.25));     // fail-stop: świat wymaga ponownego uruchomienia
        // przyczyna jest w logach mimo limitu rekordów: DELIVERY_LIMIT zawsze i od razu podsumowanie 4116 z rodzajami naruszeń
        Assert.True(engine.Zatrzymaj().ZamkniecieKompletne);
        var mine = TestArtifacts.Records(directory).Where(r => Text(r, "moduleId") == WejscieCykluLog.Modul).ToArray();
        Assert.Single(mine, r => Text(r, "eventCode") == WejscieCykluLog.KodNaruszenie && Text(r, "reasonCode") == "DELIVERY_LIMIT");
        Assert.All(mine.Where(r => Text(r, "eventCode") == WejscieCykluLog.KodNaruszenie).GroupBy(r => Text(r, "reasonCode")),
            g => Assert.InRange(g.Count(), 1, WejscieCykluLog.MaxNaruszenWLogu));                         // limit na kod
        var podsumowanie = Assert.Single(mine, r => Text(r, "eventCode") == WejscieCykluLog.KodPodsumowanie).GetProperty("counters");
        Assert.Equal(1, podsumowanie.GetProperty("stopped").GetInt64());
        Assert.Equal(l["violations"], podsumowanie.GetProperty("violations").GetInt64());
        Assert.True(podsumowanie.GetProperty("violationsSuppressed").GetInt64() > 0);
        Assert.Equal(l["deferredDeliveries"], podsumowanie.GetProperty("deferredDeliveries").GetInt64());
    }

    [Fact]
    public void WyjatekWnetrzaFazyNieJestMaskowanyBledemDoreczenia()
    {
        // błąd w fazie KARTY i błąd przy jej zamknięciu (subskrybent alarmu KZ) – oba w AggregateException, żaden nie ginie
        var sim = new Simulator(Load());
        sim.AlarmAdded += a => { if (a.Obiekt == "SIEĆ") throw new InvalidOperationException("z KZ"); };
        sim.Tick(0.25);
        var ex = Assert.Throws<AggregateException>(() => sim.Wejscia.WFazie<int>(FazaWejscia.Karty, () =>
        {
            sim.Urzadzenia.Zasilanie.Usterka("mains", false);                      // status czeka na granicę fazy
            throw new ApplicationException("pierwotny");
        }));
        Assert.Collection(ex.InnerExceptions, e => Assert.Equal("pierwotny", e.Message), e => Assert.Equal("z KZ", e.Message));
        // status mimo to doręczony do KZ: KZ przyjął „BRAK SIECI” (wpis karta → KZ) i podniósł alarm, którego subskrybent zawiódł
        Assert.StartsWith("BRAK SIECI", sim.Urzadzenia.Dziennik.Last(k => k.Rodzaj == "zasilanie").Tresc, StringComparison.Ordinal);
        // bez błędu przy zamknięciu – wychodzi wyjątek pierwotny (nie AggregateException)
        Assert.Throws<ApplicationException>(() => sim.Wejscia.WFazie<int>(FazaWejscia.Karty, () => throw new ApplicationException("pierwotny")));
    }

    sealed class Zmieniajacy(Simulator s, int tryb) : IOdbiorcaWejscCyklu
    {
        public int Wywolania; public Exception? Blad;
        public void Odbierz(WejscieCyklu wejscie)
        {
            Wywolania++;
            try
            {
                switch (tryb)
                {
                    case 0: s.Tick(0.25); break;
                    case 1: s.Exec("ONN", true); break;
                    case 2: s.PrzeliczTablice(); break;
                    default: s.Nadrzedny.CancelPending(); break;
                }
            }
            catch (Exception ex) { Blad = ex; throw; }
        }
    }

    [Fact]
    public void OdbiorcaNieMozeWykonacCykluPoleceniaAniPrzeliczenia()
    {
        // recenzja S02A.02 (audyt 27.10): cykl, polecenia i przeliczenie tablicy z wnętrza odbiorcy są odrzucane przed jakimkolwiek skutkiem
        var sim = Scenariusz("podstawowy", out _); var wzor = Scenariusz("podstawowy", out _);
        var t = sim.Wejscia.Trasa!;
        var odb = Enumerable.Range(0, 4).Select(i => new Zmieniajacy(sim, i)).ToArray();
        foreach (var o in odb) sim.Wejscia.Subskrybuj(t.KzIds[0], o);
        for (int i = 0; i < 40; i++) { sim.Tick(0.25); wzor.Tick(0.25); }
        Assert.All(odb, o => { Assert.Equal(1, o.Wywolania); Assert.IsType<InvalidOperationException>(o.Blad); });
        Assert.Equal(4, sim.Wejscia.Liczniki["receiverFailures"]);
        Assert.Equal(wzor.NrCyklu, sim.NrCyklu); Assert.Equal(wzor.T, sim.T);
        Assert.True(sim.Urzadzenia.Zasilanie.NN);
        Assert.Equal(wzor.Urzadzenia.Dziennik.Count, sim.Urzadzenia.Dziennik.Count);      // brak wpisu „KZ → karta” z odbiorcy
        Assert.Equal(Dziennik(wzor), Dziennik(sim)); Assert.Equal(Stan(wzor), Stan(sim));
    }

    [Fact]
    public void PulpitBudujeTabeleRozsylaniaPrzyStarcieINumerujeOdPoczatku()
    {
        WorldLog.Register(); Rejestruj();
        string directory = TestArtifacts.Directory("S02A02-pulpit-wejscia");
        using (var engine = SilnikLogow.Uruchom(new OpcjeLogow { Aplikacja = "LcsWidzew", Katalog = directory, Profil = ProfilLogow.Klient }))
        {
            using (var host = new SingleplayerHost(Load(), "podstawowy", new User { Name = "test", Role = "dyżurny ruchu" }, clock: new ManualClock(),
                options: new SimulatorOptions { WejscieCykluLogger = engine.Modul(WejscieCykluLog.Modul, WejscieCykluLog.Wersja) }))
            {
                var sim = host.Session.Sim;
                Assert.NotNull(sim.Wejscia.Trasa);
                for (int i = 0; i < 5; i++) { host.Frame(true, 1); host.Step(); }
                string kz = sim.Wejscia.Trasa!.KzIds[0];
                long nr = sim.Wejscia.OstatniNrWejscia(kz);
                Assert.Equal(2 * sim.NrCyklu, nr);                                     // KARTY i RUCH od pierwszego cyklu, bez luk
                var z = new Zapis(); sim.Wejscia.Subskrybuj(kz, z);
                host.Frame(true, 1); host.Step();
                Assert.Equal(nr + 1, z.Wejscia[0].NrWejscia);                          // odbiorca dołączony później – od bieżącego numeru
            }
            engine.Zatrzymaj();
        }
        var records = TestArtifacts.Records(directory).Where(r => Text(r, "moduleId") == WejscieCykluLog.Modul).ToArray();
        Assert.Single(records, r => Text(r, "eventCode") == WejscieCykluLog.KodTrasa);
        Assert.DoesNotContain(records, r => Text(r, "eventCode") is WejscieCykluLog.KodNaruszenie or WejscieCykluLog.KodPodsumowanie or WejscieCykluLog.KodBrakTrasy);
    }

    [Fact]
    public void WyjatekWTrakcieDoreczeniaNieZostawiaLukUOdbiorcow()
    {
        var sim = new Simulator(Load()); var t = sim.Wejscia.Trasa!;
        var zapis = t.KzIds.ToDictionary(k => k, _ => new Zapis(), StringComparer.Ordinal);
        foreach (var (k, z) in zapis) sim.Wejscia.Subskrybuj(k, z);
        bool rzucaj = true;
        sim.AlarmAdded += a => { if (rzucaj && a.Obiekt == "SIEĆ") throw new InvalidOperationException("błąd subskrybenta"); };
        sim.Tick(0.25);
        Assert.Throws<InvalidOperationException>(() => sim.Instruktor.ApplyFault("mains", null, false));    // wyjątek z wnętrza KZ obszaru
        rzucaj = false;
        for (int i = 0; i < 4; i++) sim.Tick(0.25);
        foreach (var (kz, z) in zapis)
        {
            Assert.Equal(Enumerable.Range(1, z.Wejscia.Count).Select(x => (long)x), z.Wejscia.Select(w => w.NrWejscia));
            Assert.Equal(sim.Wejscia.OstatniNrWejscia(kz), z.Wejscia.Count);
            Assert.Contains(z.Wejscia, w => w.Faza == FazaWejscia.Usterka && w.Statusy.Any(s => s is StatusZasilania p && !p.Siec));
        }
        // status obiektu bez karty – odrzucony w miejscu zgłoszenia; poprawne zgłoszenia tej fazy nie giną
        var karta = sim.Urzadzenia.Odcinki.Values.First();
        using (sim.Wejscia.Faza(FazaWejscia.Karty))
        {
            karta.Pojazd = true; karta.Publikuj();
            Assert.Throws<ArgumentException>(() => sim.Wejscia.Zglos(new StatusOdcinka { Obiekt = "NIEMA" }));
            Assert.Throws<ArgumentException>(() => sim.Wejscia.Zglos(new StatusZasilania { Obiekt = "inna" }));
            Assert.Throws<ArgumentException>(() => sim.Wejscia.Zglos(new StatusZwrotnicy { Obiekt = karta.Id }));     // rodzaj niezgodny z kartą
        }
        Assert.True(sim.Secs[karta.Id].TrainOcc);
        Assert.Equal(0, sim.Wejscia.Liczniki["stopped"]);
    }

    [Fact]
    public void PostacKanonicznaZgodnaZWektoramiKontraktu()
    {
        // te same wektory sprawdza kontrakty/testy/sprawdz_kontrakty.py (Python): bajty i SHA-256 niezależne od biblioteki JSON (recenzja, ust. 4)
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "wejscie_cyklu_kanon.json")));
        var wektory = doc.RootElement.GetProperty("wektory").EnumerateArray().ToArray();
        Assert.True(wektory.Length >= 4);
        foreach (var v in wektory)
        {
            var j = v.GetProperty("wejscie");
            var w = new WejscieCyklu
            {
                KzId = j.GetProperty("kzId").GetString(), NrWejscia = j.GetProperty("nrWejscia").GetInt64(), NrPartii = j.GetProperty("nrPartii").GetInt64(),
                Cykl = j.GetProperty("cykl").GetInt64(), Faza = Enum.GetValues<FazaWejscia>().Single(f => WejscieCyklu.NazwaFazy(f) == j.GetProperty("faza").GetString()),
                CzasSymulacjiMs = j.GetProperty("czasSymulacjiMs").GetInt64(), SkrotTrasy = j.GetProperty("skrotTrasy").GetString(),
                Statusy = j.GetProperty("statusy").EnumerateArray().Select(StatusZJson).ToImmutableArray(),
            };
            Assert.Equal(v.GetProperty("kanoniczny").GetString(), w.Kanoniczny());
            Assert.Equal(v.GetProperty("sha256").GetString(), w.Sha256());
        }
        // samotny surogat: \udxxx jak w RFC 8785 (kontrakt odrzuca takie identyfikatory, zapis pozostaje deterministyczny); para – dosłownie
        var sb = new System.Text.StringBuilder(); KanonJson.Tekst(sb, "a\ud800b\udc00"); Assert.Equal("\"a\\ud800b\\udc00\"", sb.ToString());
        sb.Clear(); KanonJson.Tekst(sb, "\U0001F600\u007f\u0000"); Assert.Equal("\"\U0001F600\u007f\\u0000\"", sb.ToString());
    }

    [Fact]
    public void BezPosterunkowWejsciaNieSaRozsylaneAKzObszaruDzialaDalej()
    {
        WorldLog.Register(); Rejestruj();
        string directory = TestArtifacts.Directory("S02A02-bez-posterunkow");
        var station = Load(); station.Posts.Clear(); station.Posts["ZŁY ID"] = "Posterunek bez poprawnego identyfikatora";
        using (var engine = SilnikLogow.Uruchom(new OpcjeLogow { Aplikacja = "Lcs.WorldHost", Katalog = directory, Profil = ProfilLogow.Serwer }))
        {
            var sim = new Simulator(station, options: new SimulatorOptions { WejscieCykluLogger = engine.Modul(WejscieCykluLog.Modul, WejscieCykluLog.Wersja) });
            Assert.Null(sim.Wejscia.Trasa);                                                    // fail-closed: bez tabeli nie ma wejść KZ
            Assert.Throws<InvalidOperationException>(() => sim.Wejscia.Subskrybuj("KZ-LW", new Zapis()));
            sim.Tick(0.25);
            sim.Instruktor.ApplyFault("mains", null, false);
            Assert.False(sim.Power.Mains);                                                    // przejściowy KZ obszaru nadal dostaje partie
            Assert.Equal(1, sim.Wejscia.Liczniki["routingUnavailable"]); Assert.Equal(0, sim.Wejscia.Liczniki["inputs"]);
            engine.Zatrzymaj();
        }
        var records = TestArtifacts.Records(directory).Where(r => Text(r, "moduleId") == WejscieCykluLog.Modul).ToArray();
        var brak = Assert.Single(records, r => Text(r, "eventCode") == WejscieCykluLog.KodBrakTrasy);
        Assert.Equal(WejscieCykluLog.PowodBrakuPosterunkow, Text(brak, "reasonCode")); Assert.Equal("Error", Text(brak, "severity"));
    }

    [Fact]
    public void TestowiOdbiorcyDostajaTeSameWejsciaWKolejnosciIWedlugTrasy()
    {
        var sim = Scenariusz("podstawowy", out _);
        var odb = Odbiorcy(sim); var t = sim.Wejscia.Trasa!;
        string ac = sim.L.Sections.Values.Where(s => s.AxleCounter).Select(s => s.Id).OrderBy(x => x, StringComparer.Ordinal).First();
        for (int i = 1; i <= 1200; i++)
        {
            sim.Tick(0.25);
            if (i == 40) sim.Instruktor.ApplyFault("mains", null, false);
            if (i == 80) sim.Instruktor.ApplyFault("mains", null, true);
            if (i == 100) sim.Instruktor.ApplyFault("secOcc", ac, false);
            if (i == 140) Assert.True(sim.Exec("ONN", true).Ok);
            if (i == 160) Assert.True(sim.Exec("NN", true).Ok);
        }
        var poPartii = new Dictionary<long, List<WejscieCyklu>>();
        foreach (var (kz, (a, b)) in odb)
        {
            // ten sam strumień: te same obiekty i ta sama treść kanoniczna u obu odbiorców
            Assert.Equal(a.Wejscia.Count, b.Wejscia.Count);
            Assert.All(a.Wejscia.Zip(b.Wejscia), p => { Assert.Same(p.First, p.Second); Assert.Equal(p.First.Sha256(), p.Second.Sha256()); });
            // numeracja bez luk, partie i cykle rosnące, regularne fazy w każdym cyklu
            Assert.Equal(Enumerable.Range(1, a.Wejscia.Count).Select(x => (long)x), a.Wejscia.Select(w => w.NrWejscia));
            Assert.Equal(sim.Wejscia.OstatniNrWejscia(kz), a.Wejscia.Count);
            Assert.All(a.Wejscia.Zip(a.Wejscia.Skip(1)), p => { Assert.True(p.Second.NrPartii > p.First.NrPartii); Assert.True(p.Second.Cykl >= p.First.Cykl); });
            Assert.Equal(1200, a.Wejscia.Count(w => w.Faza == FazaWejscia.Karty));
            Assert.Equal(1200, a.Wejscia.Count(w => w.Faza == FazaWejscia.Ruch));
            foreach (var w in a.Wejscia)
            {
                Assert.Equal(kz, w.KzId); Assert.Equal(t.Sha256, w.SkrotTrasy);
                // każdy status w wejściu KZ ma ten KZ w tabeli; porządek kanoniczny wg pozycji w mapie
                var poz = w.Statusy.Select(s => t.WpisObiektu(s.Rodzaj, s.Obiekt)!).ToList();
                Assert.All(poz, x => Assert.Contains(kz, x.Odbiorcy));
                Assert.True(poz.Zip(poz.Skip(1)).All(p => p.First.Pozycja <= p.Second.Pozycja));
                if (!poPartii.TryGetValue(w.NrPartii, out var l)) poPartii[w.NrPartii] = l = new();
                l.Add(w);
            }
        }
        // ta sama partia: status obiektu trafia do wszystkich KZ z tabeli – jako ten sam rekord, tyle samo razy
        foreach (var (_, wejscia) in poPartii)
        {
            Assert.Single(wejscia.Select(w => (w.Faza, w.Cykl, w.CzasSymulacjiMs)).Distinct());
            var rekordy = wejscia.SelectMany(w => w.Statusy).Distinct(ReferenceEqualityComparer.Instance).Cast<StatusUrzadzenia>();
            foreach (var r in rekordy)
            {
                var odbiorcy = t.WpisObiektu(r.Rodzaj, r.Obiekt)!.Odbiorcy;
                Assert.Equal(odbiorcy, wejscia.Where(w => w.Statusy.Any(s => ReferenceEquals(s, r))).Select(w => w.KzId).OrderBy(k => k, StringComparer.Ordinal));
            }
        }
        // jawne fazy natychmiastowe: usterki i NN/ONN
        var lw = odb[t.WpisObiektu(M.Rodzaj.Odcinek, ac)!.Odbiorcy[0]].A.Wejscia;
        Assert.Contains(lw, w => w.Faza == FazaWejscia.Usterka && w.Statusy.Any(s => s is StatusOdcinka o && o.Obiekt == ac && o.Stan == "occ"));
        Assert.All(odb.Values, o => Assert.Contains(o.A.Wejscia, w => w.Faza == FazaWejscia.Usterka && w.Statusy.Any(s => s is StatusZasilania z && !z.Siec)));
        Assert.All(odb.Values, o => Assert.Contains(o.A.Wejscia, w => w.Faza == FazaWejscia.Rozkaz && w.Statusy.Any(s => s is StatusZasilania z && !z.NapiecieNastawcze)));
        var licz = sim.Wejscia.Liczniki;
        Assert.Equal(0, licz["violations"]); Assert.Equal(0, licz["receiverFailures"]); Assert.Equal(0, licz["unknownObjects"]);
    }

    [Fact]
    public void RozsylanieWynikaZMapyWlasnosciIJestDeterministyczne()
    {
        var sim = new Simulator(Load()); var map = sim.Wlasnosc; var t = sim.Wejscia.Trasa!;
        Assert.Equal(map.Sha256, t.SkrotMapy);
        Assert.Equal(map.Posterunki.Select(p => p.KzId).OrderBy(k => k, StringComparer.Ordinal), t.KzIds);
        Assert.Equal(map.Obiekty.Count, t.Wpisy.Length);
        foreach (var o in map.Obiekty)
        {
            var w = t.WpisObiektu(o.Rodzaj, o.Id)!;
            Assert.NotEmpty(w.Odbiorcy);                                              // status nigdy nie ginie
            Assert.Equal(w.Odbiorcy.OrderBy(k => k, StringComparer.Ordinal), w.Odbiorcy);
            if (o.Status == M.Status.Przypisany) { Assert.Equal(TrasaWejsc.Powod.Wlasciciel, w.Powod); Assert.Equal(o.Odbiorcy, w.Odbiorcy); Assert.Contains(o.KzId!, w.Odbiorcy); }
            else if (o.Status == M.Status.Niejednoznaczny)
            {
                Assert.Equal(TrasaWejsc.Powod.Kandydaci, w.Powod);
                Assert.All(o.Kandydaci, p => Assert.Contains(map.KzPosterunku(p)!, w.Odbiorcy));     // ostrożnie: wszyscy kandydaci
            }
            else Assert.Equal(o.Odbiorcy.Count > 0 ? TrasaWejsc.Powod.OdbiorcyMapy : TrasaWejsc.Powod.WszystkieKz, w.Powod);
        }
        // odstępy jednoodstępowych szlaków między LCS (niejednoznaczne) – do obu KZ; szafa zasilania – do wszystkich KZ
        Assert.Contains(t.Wpisy, w => w.Powod == TrasaWejsc.Powod.Kandydaci && w.Odbiorcy.Length > 1);
        Assert.Equal(t.KzIds, t.WpisObiektu(M.Rodzaj.Zasilanie, Zasilanie.Id)!.Odbiorcy);
        Assert.Null(t.WpisObiektu(M.Rodzaj.Zwrotnica, "NIEISTNIEJACA"));
        // kolejność słowników układu nie zmienia tabeli
        var reversed = Load();
        reversed.Nodes = Reverse(reversed.Nodes); reversed.Edges = Reverse(reversed.Edges); reversed.Sections = Reverse(reversed.Sections);
        reversed.Switches = Reverse(reversed.Switches); reversed.Signals = Reverse(reversed.Signals); reversed.Derailers = Reverse(reversed.Derailers);
        reversed.Blocks = Reverse(reversed.Blocks); reversed.Posts = Reverse(reversed.Posts);
        var t2 = new Simulator(reversed).Wejscia.Trasa!;
        Assert.Equal(t.Json, t2.Json); Assert.Equal(t.Sha256, t2.Sha256);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(t.Json))), t.Sha256);
        Assert.Same(t, sim.Wejscia.Trasa);
        // przeliczenie tablicy przebiegów daje nową mapę i nową tabelę (ta sama treść)
        Assert.Null(sim.PrzeliczTablice());
        var t3 = sim.Wejscia.Trasa!; Assert.NotSame(t, t3); Assert.Equal(t.Sha256, t3.Sha256);
    }

    [Fact]
    public void KolejnoscSlownikowIZgloszenNieZmieniaTresciWejsc()
    {
        var a = new Simulator(Load());
        var st = Load();
        st.Sections = Reverse(st.Sections); st.Switches = Reverse(st.Switches); st.Signals = Reverse(st.Signals); st.Derailers = Reverse(st.Derailers);
        var b = new Simulator(st);
        var za = a.Wejscia.Trasa!.KzIds.ToDictionary(k => k, _ => new Zapis()); var zb = b.Wejscia.Trasa!.KzIds.ToDictionary(k => k, _ => new Zapis());
        foreach (var (k, z) in za) a.Wejscia.Subskrybuj(k, z);
        foreach (var (k, z) in zb) b.Wejscia.Subskrybuj(k, z);
        string occ = a.L.Sections.Keys.OrderBy(k => k, StringComparer.Ordinal).First();
        foreach (var s in new[] { a, b })
        {
            s.Tick(0.25);                                                 // faza kart: wszystkie karty w kolejności swoich słowników
            s.Urzadzenia.Osie(new HashSet<string> { occ });              // faza ruchu: wszystkie odcinki
            s.Tick(0.25);
        }
        Assert.Equal(za.Keys, zb.Keys);
        foreach (var k in za.Keys)
        {
            Assert.Equal(za[k].Wejscia.Count, zb[k].Wejscia.Count);
            Assert.Equal(za[k].Wejscia.Select(w => w.Kanoniczny()), zb[k].Wejscia.Select(w => w.Kanoniczny()));
        }
        Assert.Contains(za.Values.SelectMany(z => z.Wejscia), w => w.Faza == FazaWejscia.Ruch && w.Statusy.Any(s => s is StatusOdcinka o && o.Obiekt == occ && o.Pojazd));
        // kanoniczny JSON: jedna linia, kontrakt, bez znaków CR
        var j = za.Values.First().Wejscia[0].Kanoniczny();
        Assert.DoesNotContain("\n", j); Assert.DoesNotContain("\r", j);
        using var doc = JsonDocument.Parse(j);
        Assert.Equal(WejscieCyklu.Kontrakt, doc.RootElement.GetProperty("kontrakt").GetString());
        Assert.Equal(WejscieCyklu.WersjaSchematu, doc.RootElement.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public void OdbiorcyNieZmieniajaZachowaniaSymulacji()
    {
        var bez = Scenariusz("usterki", out _);
        var z = Scenariusz("usterki", out _); var odb = Odbiorcy(z);
        for (int i = 1; i <= 2400; i++)
        {
            foreach (var s in new[] { bez, z })
            {
                s.Tick(0.25);
                if (i == 60) s.Instruktor.ApplyFault("mains", null, false);
                if (i == 200) s.Instruktor.ApplyFault("mains", null, true);
                if (i == 300) s.Exec("ONN", true);
                if (i == 320) s.Exec("NN", true);
            }
        }
        Assert.NotEmpty(bez.Events);
        Assert.Equal(Dziennik(bez), Dziennik(z));
        Assert.Equal(Stan(bez), Stan(z));
        Assert.True(odb.Values.All(o => o.A.Wejscia.Count > 4800));
    }

    [Fact]
    public void StatusPozaFazaIFazaZagniezdzonaSaDoreczaneWBazowejKolejnosci()
    {
        var sim = new Simulator(Load());
        var t = sim.Wejscia.Trasa!; var zapis = new Zapis();
        string sec = sim.L.Sections.Keys.OrderBy(k => k, StringComparer.Ordinal).First(); string kz = t.WpisObiektu(M.Rodzaj.Odcinek, sec)!.Odbiorcy[0];
        sim.Wejscia.Subskrybuj(kz, zapis);
        var karta = sim.Urzadzenia.Odcinki[sec];
        // status poza fazą – nie ginie: od razu do KZ jako POZA_FAZA, licznik naruszeń
        karta.Pojazd = true; karta.Publikuj();
        Assert.True(sim.Secs[sec].TrainOcc);
        Assert.Equal(FazaWejscia.PozaFaza, Assert.Single(zapis.Wejscia).Faza);
        // faza zagnieżdżona: zgłoszenia zewnętrzne są doręczane przed wewnętrznymi (jak dawne natychmiastowe wywołanie KZ)
        using (sim.Wejscia.Faza(FazaWejscia.Karty))
        {
            karta.Pojazd = false; karta.Publikuj();
            using (sim.Wejscia.Faza(FazaWejscia.Usterka))
            {
                Assert.False(sim.Secs[sec].TrainOcc);                      // zewnętrzna partia doręczona przy otwarciu wewnętrznej
                karta.Pojazd = true; karta.Publikuj();
            }
            Assert.True(sim.Secs[sec].TrainOcc);
        }
        Assert.Equal(new[] { FazaWejscia.PozaFaza, FazaWejscia.Karty, FazaWejscia.Usterka, FazaWejscia.Karty }, zapis.Wejscia.Select(w => w.Faza));
        Assert.Equal(new[] { 1, 1, 1, 0 }, zapis.Wejscia.Select(w => w.Statusy.Count(s => s.Obiekt == sec)));
        Assert.True(sim.Secs[sec].TrainOcc);
        var l = sim.Wejscia.Liczniki;
        Assert.Equal(1, l["outsidePhase"]); Assert.Equal(1, l["nestedPhases"]); Assert.Equal(2, l["violations"]);
        Assert.Throws<ArgumentNullException>(() => sim.Wejscia.Zglos(null!));
        Assert.Throws<ArgumentException>(() => sim.Wejscia.Zglos(new StatusOdcinka { Obiekt = "" }));
    }

    [Fact]
    public void OdbiorcaZWyjatkiemJestWylaczanyBezWplywuNaSwiatIInnychOdbiorcow()
    {
        var sim = Scenariusz("podstawowy", out _);
        var t = sim.Wejscia.Trasa!; string kz = t.KzIds[0];
        var wadliwy = new Wadliwy(); var dobry = new Zapis();
        sim.Wejscia.Subskrybuj(kz, wadliwy); sim.Wejscia.Subskrybuj(kz, dobry);
        var wzor = Scenariusz("podstawowy", out _);
        for (int i = 0; i < 200; i++) { sim.Tick(0.25); wzor.Tick(0.25); }
        Assert.Equal(1, wadliwy.Wywolania);                                            // wyłączony po pierwszym błędzie – bez luk dalej
        Assert.Equal(400, dobry.Wejscia.Count);
        Assert.Equal(1, sim.Wejscia.Liczniki["receiverFailures"]);
        Assert.Equal(Dziennik(wzor), Dziennik(sim));
        // subskrypcja: nieznany KZ odrzucony; wyrejestrowanie zatrzymuje doręczanie
        Assert.Throws<ArgumentException>(() => sim.Wejscia.Subskrybuj("KZ-NIEMA", new Zapis()));
        Assert.Throws<ArgumentException>(() => sim.Wejscia.Subskrybuj(null!, new Zapis()));
        var krotki = new Zapis(); var sub = sim.Wejscia.Subskrybuj(kz, krotki);
        sim.Tick(0.25); sub.Dispose(); sim.Tick(0.25);
        Assert.Equal(2, krotki.Wejscia.Count);
        Assert.Same(dobry.Wejscia[400], krotki.Wejscia[0]); Assert.Equal(404, dobry.Wejscia.Count);
    }

    [Fact]
    public void F24LogiWejscMajaLimitRedakcjeIKontekst()
    {
        WorldLog.Register(); Rejestruj();
        string directory = TestArtifacts.Directory("S02A02-logi-wejsc");
        var station = Load();
        // posterunki z niepoprawnym identyfikatorem – ich obiekty bez właściciela: wiele obejść ponad limit rekordów
        foreach (var sw in station.Switches.Values.Take(10)) sw.Post = "ZŁY ID";
        foreach (var sg in station.Signals.Values.Take(10)) sg.Post = "";
        station.Switches.Values.Skip(10).First().Post = "password=" + ProbaLogow.ZnacznikSekretu;
        long fallbackow;
        using (var engine = SilnikLogow.Uruchom(new OpcjeLogow { Aplikacja = "Lcs.WorldHost", Katalog = directory, Profil = ProfilLogow.Serwer }))
        {
            var sim = new Simulator(station, options: new SimulatorOptions { WejscieCykluLogger = engine.Modul(WejscieCykluLog.Modul, WejscieCykluLog.Wersja) });
            var t = sim.Wejscia.Trasa!;
            fallbackow = t.Wpisy.Count(w => w.Powod != TrasaWejsc.Powod.Wlasciciel);
            Assert.True(fallbackow > WejscieCykluLog.MaxObejscWLogu);
            sim.Wejscia.Subskrybuj(t.KzIds[0], new Wadliwy());
            for (int i = 0; i < 20; i++) sim.Urzadzenia.Odcinki.Values.First().Publikuj();   // 20 naruszeń poza fazą – ponad limit
            sim.Tick(0.25);                                                                     // pierwsze podsumowanie 4116 (cykl 1): 20 / 4
            for (int i = 0; i < 20; i++) sim.Urzadzenia.Odcinki.Values.First().Publikuj();   // 20 kolejnych nieopisanych
            for (long i = 0; i < RejestrWejscCyklu.OkresPodsumowania - 1; i++) sim.Tick(0.25);
            Assert.Equal(RejestrWejscCyklu.OkresPodsumowania, sim.NrCyklu);                      // przed upływem okresu – bez drugiego podsumowania
            sim.Tick(0.25);                                                                     // drugie podsumowanie (cykl 3001): 40 / 24
            var state = engine.Zatrzymaj(); File.WriteAllText(Path.Combine(directory, "stan.txt"), state.ToString());
            Assert.True(state.ZamkniecieKompletne); Assert.Equal(0, state.StratyRazem);
            Assert.Equal(40, sim.Wejscia.Liczniki["violations"]);
            var records = TestArtifacts.Records(directory); string all = string.Join('\n', records.Select(r => r.GetRawText()));
            Assert.DoesNotContain(ProbaLogow.ZnacznikSekretu, all, StringComparison.Ordinal);
            var mine = records.Where(r => Text(r, "moduleId") == WejscieCykluLog.Modul).ToArray();
            Assert.All(mine, r => { Assert.Equal(1, r.GetProperty("schemaVersion").GetInt32()); Assert.Equal(WejscieCykluLog.Wersja, Text(r, "moduleVersion")); });
            var ready = Assert.Single(mine, r => Text(r, "eventCode") == WejscieCykluLog.KodTrasa);
            Assert.Equal(WejscieCykluLog.IdTrasa, ready.GetProperty("eventId").GetInt32());
            Assert.Equal(t.Sha256, Text(ready, "contentHash")); Assert.Equal(t.SkrotMapy, Text(ready, "mapHash")); Assert.Equal("Degraded", Text(ready, "outcome"));
            var c = ready.GetProperty("counters");
            Assert.Equal(t.Wpisy.Length, c.GetProperty("objects").GetInt64());
            Assert.Equal(WejscieCykluLog.MaxObejscWLogu, c.GetProperty("fallbackLogged").GetInt64());
            Assert.Equal(fallbackow - WejscieCykluLog.MaxObejscWLogu, c.GetProperty("fallbackSuppressed").GetInt64());
            var obejscia = mine.Where(r => Text(r, "eventCode") == WejscieCykluLog.KodObejscie).ToArray();
            Assert.Equal(WejscieCykluLog.MaxObejscWLogu, obejscia.Length);
            Assert.All(obejscia, r => { Assert.Equal("Warning", Text(r, "severity")); Assert.Matches("^OWNER_[A-Z_]+$", Text(r, "reasonCode")); });
            var naruszenia = mine.Where(r => Text(r, "eventCode") == WejscieCykluLog.KodNaruszenie).ToArray();
            Assert.Equal(WejscieCykluLog.MaxNaruszenWLogu, naruszenia.Length);
            Assert.All(naruszenia, r => Assert.Equal("STATUS_OUTSIDE_PHASE", Text(r, "reasonCode")));
            var blad = Assert.Single(mine, r => Text(r, "eventCode") == WejscieCykluLog.KodBladOdbiorcy);
            Assert.Equal("RECEIVER_EXCEPTION", Text(blad, "reasonCode")); Assert.Equal(t.KzIds[0], Text(blad, "kzId")); Assert.Equal("Failed", Text(blad, "outcome"));
            var podsumowania = mine.Where(r => Text(r, "eventCode") == WejscieCykluLog.KodPodsumowanie).Select(r => r.GetProperty("counters")).ToArray();
            Assert.Equal(2, podsumowania.Length);
            Assert.All(mine.Where(r => Text(r, "eventCode") == WejscieCykluLog.KodPodsumowanie), r => Assert.Equal(WejscieCykluLog.IdPodsumowanie, r.GetProperty("eventId").GetInt32()));
            Assert.Equal(20, podsumowania[0].GetProperty("violations").GetInt64()); Assert.Equal(20 - WejscieCykluLog.MaxNaruszenWLogu, podsumowania[0].GetProperty("violationsSuppressed").GetInt64());
            Assert.Equal(40, podsumowania[1].GetProperty("violations").GetInt64()); Assert.Equal(40 - WejscieCykluLog.MaxNaruszenWLogu, podsumowania[1].GetProperty("violationsSuppressed").GetInt64());
            Assert.Equal(40, podsumowania[1].GetProperty("outsidePhase").GetInt64());
            // podsumowanie powstaje przy zamrożeniu fazy KARTY, przed doręczeniem: błąd odbiorcy z cyklu 1 widać dopiero w drugim
            Assert.Equal(0, podsumowania[0].GetProperty("receiverFailures").GetInt64()); Assert.Equal(1, podsumowania[1].GetProperty("receiverFailures").GetInt64());
            Assert.All(podsumowania, pc => Assert.Equal(0, pc.GetProperty("receiverFailuresSuppressed").GetInt64()));
            // brak rekordu na każdy status: zapisy to tylko budowa tabeli, obejścia, naruszenia, błąd odbiorcy i dwa podsumowania
            Assert.Equal(1 + WejscieCykluLog.MaxObejscWLogu + WejscieCykluLog.MaxNaruszenWLogu + 1 + 2, mine.Length);
        }
    }

    [Fact]
    public void PodsumowanieNaKoncuSwiataOpisujeNaruszeniaPonadLimit()
    {
        // audyt S02A.02: krótki świat (< 3000 cykli) – naruszenia ponad limit rekordów nie znikają; PodsumujNaKoniec (Dispose hosta) daje jeden 4116
        WorldLog.Register(); Rejestruj();
        string directory = TestArtifacts.Directory("S02A02-podsumowanie-koniec");
        using (var engine = SilnikLogow.Uruchom(new OpcjeLogow { Aplikacja = "Lcs.WorldHost", Katalog = directory, Profil = ProfilLogow.Serwer }))
        {
            var sim = new Simulator(Load(), options: new SimulatorOptions { WejscieCykluLogger = engine.Modul(WejscieCykluLog.Modul, WejscieCykluLog.Wersja) });
            sim.Tick(0.25);
            for (int i = 0; i < 20; i++) sim.Urzadzenia.Odcinki.Values.First().Publikuj();   // 20 naruszeń poza fazą, 4 bez własnego rekordu
            sim.Wejscia.PodsumujNaKoniec();
            sim.Wejscia.PodsumujNaKoniec();                                                     // bez nowych nieopisanych – bez drugiego rekordu
            Assert.True(engine.Zatrzymaj().ZamkniecieKompletne);
        }
        var mine = TestArtifacts.Records(directory).Where(r => Text(r, "moduleId") == WejscieCykluLog.Modul).ToArray();
        Assert.Equal(WejscieCykluLog.MaxNaruszenWLogu, mine.Count(r => Text(r, "eventCode") == WejscieCykluLog.KodNaruszenie));
        var pc = Assert.Single(mine, r => Text(r, "eventCode") == WejscieCykluLog.KodPodsumowanie).GetProperty("counters");
        Assert.Equal(20, pc.GetProperty("violations").GetInt64()); Assert.Equal(20 - WejscieCykluLog.MaxNaruszenWLogu, pc.GetProperty("violationsSuppressed").GetInt64());
        Assert.Equal(20, pc.GetProperty("outsidePhase").GetInt64()); Assert.Equal(0, pc.GetProperty("stopped").GetInt64());
    }

    [Fact]
    public void HostSwiataBudujeTabeleRozsylaniaPrzyStarcieRaz()
    {
        WorldLog.Register(); Rejestruj();
        string directory = TestArtifacts.Directory("S02A02-host-wejscia");
        string copy = Path.Combine(directory, "station_test.json"); File.Copy(Fixture, copy);
        byte[] before = SHA256.HashData(File.ReadAllBytes(copy));
        using (var engine = SilnikLogow.Uruchom(new OpcjeLogow { Aplikacja = "Lcs.WorldHost", Katalog = Path.Combine(directory, "logi"), Profil = ProfilLogow.Serwer }))
        {
            var runtime = new SimulatorRuntime(copy, "podstawowy", NullLogger.Instance, NullLogger.Instance, false, null, engine.Modul(WejscieCykluLog.Modul, WejscieCykluLog.Wersja));
            var loop = new WorldLoop(() => runtime, clock: new ManualClock()); loop.Start(); loop.Finish();
            engine.Zatrzymaj();
        }
        var records = TestArtifacts.Records(Path.Combine(directory, "logi")).Where(r => Text(r, "moduleId") == WejscieCykluLog.Modul).ToArray();
        var ready = Assert.Single(records, r => Text(r, "eventCode") == WejscieCykluLog.KodTrasa);
        Assert.Equal(new Simulator(Station.Load(copy)).Wejscia.Trasa!.Sha256, Text(ready, "contentHash"));
        Assert.DoesNotContain(records, r => Text(r, "eventCode") == WejscieCykluLog.KodNaruszenie || Text(r, "eventCode") == WejscieCykluLog.KodBrakTrasy);
        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(copy)));
    }

    static void Rejestruj()
    {
        foreach (var (id, kod, pola) in WejscieCykluLog.Definicje)
            Assert.True(KatalogZdarzen.Zarejestruj(new DefinicjaZdarzenia(id, kod, KontraktLogu.Strumien.Diagnostyka, pola)));
    }

    static StatusUrzadzenia StatusZJson(JsonElement s)
    {
        string? Str(string k) => s.GetProperty(k).ValueKind == JsonValueKind.Null ? null : s.GetProperty(k).GetString();
        bool B(string k) => s.GetProperty(k).GetBoolean();
        string id = Str("id")!;
        return Str("rodzaj") switch
        {
            "odcinek" => new StatusOdcinka { Obiekt = id, Pojazd = B("pojazd"), Stan = Str("stan") },
            "zwrotnica" => new StatusZwrotnicy { Obiekt = id, Polozenie = Str("polozenie"), Przekladanie = B("przekladanie"), PrzekroczonyCzas = B("przekroczonyCzas") },
            "wykolejnica" => new StatusWykolejnicy { Obiekt = id, Zalozona = s.GetProperty("zalozona").ValueKind == JsonValueKind.Null ? null : B("zalozona") },
            "sygnalizator" => new StatusSygnalizatora { Obiekt = id, Wyswietlany = Str("wyswietlany"), UszkodzenieZezwalajacego = B("uszkodzenieZezwalajacego"), BrakLacznosci = B("brakLacznosci") },
            "zasilanie" => new StatusZasilania { Obiekt = id, Siec = B("siec"), Ups = B("ups"), Agregat = B("agregat"), NapiecieNastawcze = B("napiecieNastawcze"), AgregatNieRuszyl = B("agregatNieRuszyl") },
            _ => throw new InvalidDataException("Nieznany rodzaj statusu w wektorze."),
        };
    }

    static string Dziennik(Simulator s) => string.Join("\n", s.Events.Select(e => $"{e.Czas}|{e.Kategoria}|{e.TypObiektu}|{e.Obiekt}|{e.Zdarzenie}"))
        + "\n#\n" + string.Join("\n", s.Alarms.Select(e => $"{e.Czas}|{e.Obiekt}|{e.Zdarzenie}|{e.Active}"));
    static string Stan(Simulator s) =>
        string.Join(";", s.Secs.Values.OrderBy(x => x.Id, StringComparer.Ordinal).Select(x => $"{x.Id}:{x.TrainOcc}:{x.Fault}:{x.Lock}")) + "|" +
        string.Join(";", s.Sws.Values.OrderBy(x => x.Num, StringComparer.Ordinal).Select(x => $"{x.Num}:{x.Pos}:{x.NoCtrl}:{x.Rozpr}:{x.Moving?.Target}")) + "|" +
        string.Join(";", s.Sigs.Values.OrderBy(x => x.Name, StringComparer.Ordinal).Select(x => $"{x.Name}:{x.Aspect}:{x.Sig}:{x.Wyswietlany}")) + "|" +
        $"{s.Power.Mains}:{s.Power.Ups}:{s.Power.Gen}:{s.Power.NN}";
    static Dictionary<string, T> Reverse<T>(Dictionary<string, T> d) { var r = new Dictionary<string, T>(); foreach (var (k, v) in d.Reverse()) r[k] = v; return r; }
    static string Text(JsonElement record, string key) => record.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
}

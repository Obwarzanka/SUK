using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lcs.Logi;
using LcsWidzew.Model;
using LcsWidzew.Sim;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Lcs.WorldHost.Testy;

/// <summary>S02A.03: bierne obliczenie, odrzucenie stanu proponowanego i pojedyncze wykonanie wyjścia.</summary>
public sealed class PlanWyjsciaTests
{
    static string Fixture => Path.Combine(AppContext.BaseDirectory, "TestData", "station_test.json");
    static readonly JsonSerializerOptions Json = new() { IncludeFields = true };
    static Simulator New(SimulatorOptions? options = null) => new(Station.Load(Fixture), options: options);
    static PolecenieSystemowe Command(string code, params string[] objects) => new(code, objects, 0, objects.FirstOrDefault() ?? "");
    static PlanWyjscia Plan(Simulator sim, string code, params string[] objects) => sim.Kz.Evaluate(KrokKz.Polecenie, Command(code, objects));
    static PartiaWejsc Batch(params StatusUrzadzenia[] statusy) => new()
    {
        NrPartii = 1, Faza = FazaWejscia.Karty, Statusy = statusy.ToImmutableArray(),
    };
    static object? Private(object source, string name) => source.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(source);

    // Odcisk obejmuje też ukryty rozkaz karty, cache podglądu i liczniki KZ; nie obejmuje prywatnej propozycji.
    static string Snapshot(Simulator s) => JsonSerializer.Serialize(new
    {
        s.T, s.NrCyklu, s.Secs, s.Sws, s.Sigs, s.Ders, s.Blocks, s.Routes, s.Power,
        s.Kz.SssPosts, s.Kz.ZssPosts,
        Seq = Private(s.Kz, "seq"), NumerZadania = Private(s.Kz, "numerZadania"),
        OdstepCache = Private(s.Kz, "odstepCache"), NextSigCache = Private(s.Kz, "nextSigCache"),
        Cards = s.Urzadzenia,
        Requests = s.Urzadzenia.Zwrotnice.ToDictionary(p => p.Key, p => Private(p.Value, "rozkaz")),
        SignalRequests = s.Urzadzenia.Sygnalizatory.ToDictionary(p => p.Key, p => Private(p.Value, "rozkaz")),
        DerailerRequests = s.Urzadzenia.Wykolejnice.ToDictionary(p => p.Key, p => Private(p.Value, "rozkaz")),
        StatusCache = Private(s.Urzadzenia, "ostStatus"), CommunicationSeq = Private(s.Urzadzenia, "komSeq"),
        Events = s.Events.Select(e => new { e.Id, e.T, e.Czas, e.Kategoria, e.Zdarzenie, e.Obiekt, e.Operator }),
        Alarms = s.Alarms.Select(e => new { e.Id, e.T, e.Czas, e.Zdarzenie, e.Obiekt, e.Ack, e.Active }),
        s.Remarks, s.Stats, s.Nadrzedny.Book, s.Nadrzedny.BookDrafts,
    }, Json);

    static void Repeat(Simulator s, PlanWyjscia p, string expected = "Executed")
    {
        string before = Snapshot(s);
        for (int i = 0; i < 1000; i++) Assert.Equal(expected, s.Wyjscia.Zatwierdz(p).Stan);
        Assert.Equal(before, Snapshot(s));
    }

    [Theory]
    [InlineData("ONN")]
    [InlineData("NN")]
    [InlineData("AGR")]
    [InlineData("ZEROLO")]
    public void EvaluateNieZmieniaTerenuAniRejestrowDlaNatychmiastowychRozkazow(string code)
    {
        var logger = new CountLogger();
        var s = New(new SimulatorOptions { PlanWyjsciaLogger = logger });
        string? sec = null;
        if (code == "NN") s.Urzadzenia.NapiecieNastawcze(false);
        if (code == "AGR") s.Instruktor.ApplyFault("mainsGenFail", null!, false);
        if (code == "ZEROLO")
        {
            sec = s.L.Sections.Values.First(x => x.AxleCounter).Id;
            s.Instruktor.ApplyFault("secOcc", sec, false); s.Instruktor.ApplyFault("secOcc", sec, true);
        }
        string before = Snapshot(s); int logBefore = logger.Count;
        var request = Command(code, sec == null ? [] : [sec]);
        var p = s.Kz.Evaluate(KrokKz.Polecenie, request);
        Assert.True(p.Wynik.Ok); Assert.NotEmpty(p.Efekty);
        Assert.Same(p, s.Kz.Evaluate(KrokKz.Polecenie, request));
        Assert.Equal(before, Snapshot(s)); Assert.Equal(logBefore, logger.Count);
        Assert.Equal(p.ContentHash, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(p.Kanoniczny()))));
        Assert.Equal("Executed", s.Wyjscia.Zatwierdz(p).Stan);
        Repeat(s, p);
    }

    [Fact]
    public void OdrzuceniePrzebieguNieZostawiaUtwierdzenRuchuAniZdarzen()
    {
        var s = New();
        var r = s.RouteTable.Values.SelectMany(x => x).First(r => r.Type == "P" && r.EndKind == "sig" && s.WhyNot(r).Count == 0);
        string before = Snapshot(s);
        var request = Command("POCIAG", r.Start, r.End);
        var p = s.Kz.Evaluate(KrokKz.Polecenie, request);
        Assert.True(p.Wynik.Ok); Assert.Equal(before, Snapshot(s));
        Assert.Equal("Rejected", s.Wyjscia.Odrzuc(p).Stan);
        Assert.Equal(before, Snapshot(s)); Repeat(s, p, "Rejected");
        var next = s.Kz.Evaluate(KrokKz.Polecenie, request);
        Assert.True(next.Wynik.Ok); Assert.Equal("Executed", s.Wyjscia.Zatwierdz(next).Stan);
        Assert.NotEmpty(s.Routes);
    }

    [Fact]
    public void OdrzuconyKodSygnaluNieTlumilNastepnegoRozkazuKarty()
    {
        var s = New(); string before = Snapshot(s);
        var first = s.Kz.Evaluate(KrokKz.KodySygnalow);
        Assert.Contains(first.Efekty, e => e.Rodzaj == RodzajEfektu.Sygnal);
        s.Wyjscia.Odrzuc(first); Assert.Equal(before, Snapshot(s));
        var next = s.Kz.Evaluate(KrokKz.KodySygnalow);
        Assert.Equal(first.Efekty.Count(e => e.Rodzaj == RodzajEfektu.Sygnal), next.Efekty.Count(e => e.Rodzaj == RodzajEfektu.Sygnal));
        s.Wyjscia.Zatwierdz(next); Repeat(s, next);
        foreach (var e in next.Efekty.Where(e => e.Rodzaj == RodzajEfektu.Sygnal)) Assert.Equal(e.Wartosc, s.Urzadzenia.Sygnalizatory[e.Obiekt].OstRozkaz);
    }

    [Fact]
    public void StareZeroLoNieZerujePonowniePoPierwszymPrzejezdzie()
    {
        var s = New(); string sec = s.L.Sections.Values.First(x => x.AxleCounter).Id;
        s.Instruktor.ApplyFault("secOcc", sec, false); s.Instruktor.ApplyFault("secOcc", sec, true);
        var p = Plan(s, "ZEROLO", sec); Assert.True(p.Wynik.Ok);
        s.Wyjscia.Zatwierdz(p); Assert.Equal("await", s.Urzadzenia.Odcinki[sec].Stan);
        s.Urzadzenia.Osie(new HashSet<string> { sec }); s.Urzadzenia.Osie(new HashSet<string>());
        Assert.Null(s.Urzadzenia.Odcinki[sec].Stan); Assert.Null(s.Secs[sec].Fault);
        Repeat(s, p); Assert.Null(s.Urzadzenia.Odcinki[sec].Stan);
        Assert.Single(s.Urzadzenia.Dziennik, x => x.Kierunek == "KZ → karta" && x.Obiekt == sec && x.Tresc == "zerowanie licznika osi");
    }

    [Fact]
    public void StaryRozkazZasilaniaNieOdwracaNowszegoSkutku()
    {
        var s = New(); var off = Plan(s, "ONN"); s.Wyjscia.Zatwierdz(off);
        Assert.False(s.Power.NN);
        var on = Plan(s, "NN"); s.Wyjscia.Zatwierdz(on);
        Repeat(s, off); Assert.True(s.Power.NN); Assert.True(s.Urzadzenia.Zasilanie.NN);
        s.Instruktor.ApplyFault("mainsGenFail", null!, false);
        var gen = Plan(s, "AGR"); s.Wyjscia.Zatwierdz(gen); Assert.True(s.Urzadzenia.Zasilanie.Agregat);
        s.Instruktor.ApplyFault("mainsGenFail", null!, true);
        Repeat(s, gen); Assert.False(s.Urzadzenia.Zasilanie.Agregat);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ZwrotnicaIWykolejnicaDostajaJedenRozkaz(bool derailer)
    {
        var s = New(); string id = derailer ? s.Ders.Keys.First() : s.Sws.Keys.First();
        string before = Snapshot(s); var p = Plan(s, "MINUS", id);
        Assert.True(p.Wynik.Ok); Assert.Equal(before, Snapshot(s));
        Assert.Contains(p.Efekty, e => e.Rodzaj == (derailer ? RodzajEfektu.Wykolejnica : RodzajEfektu.Zwrotnica));
        s.Wyjscia.Zatwierdz(p); Repeat(s, p);
        Assert.Single(s.Urzadzenia.Dziennik, x => x.Kierunek == "KZ → karta" && x.Obiekt == id);
        s.Tick(Simulator.Cfg.SwTime + 1); s.Tick(Simulator.Cfg.SwTime + 1);
        Repeat(s, p);
        if (derailer) Assert.False(s.Urzadzenia.Wykolejnice[id].Zalozona);
        else Assert.Equal("-", s.Urzadzenia.Zwrotnice[id].Iglice);
    }

    [Fact]
    public void AlarmStatusIZdarzenieSaBiernymiDanymiARefleksjaNieWykrywaDrogiDoSwiata()
    {
        var s = New(); string before = Snapshot(s);
        var p = s.Kz.Evaluate(KrokKz.Statusy, partia: Batch(new StatusZasilania { Obiekt = Zasilanie.Id, Siec = false, Ups = true, NapiecieNastawcze = true }));
        Assert.Contains(p.Efekty, e => e.Rodzaj == RodzajEfektu.Alarm);
        Assert.Contains(p.Efekty, e => e.Rodzaj == RodzajEfektu.Status);
        Assert.Equal(before, Snapshot(s));
        foreach (var type in new[] { typeof(PlanWyjscia), typeof(EfektWyjscia) })
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                Assert.False(typeof(Delegate).IsAssignableFrom(field.FieldType));
                Assert.DoesNotContain(field.FieldType, new[] { typeof(Simulator), typeof(KomputerZaleznosci), typeof(Urzadzenia), typeof(Rejestrator), typeof(Trains) });
            }
        s.Wyjscia.Zatwierdz(p); Repeat(s, p);
        Assert.Single(s.Alarms); Assert.Single(s.Events, e => e.Kategoria == "Alarm");
    }

    [Fact]
    public void OcenaSygnaluZastepczegoPowstajeRazPoZatwierdzeniu()
    {
        var s = New(); string sig = s.L.Signals.Values.First(x => x.Type == "wjazdowy").Name;
        string before = Snapshot(s); var p = Plan(s, "SZ", sig);
        Assert.True(p.Wynik.Ok); Assert.Contains(p.Efekty, e => e.Rodzaj is RodzajEfektu.Uwaga or RodzajEfektu.Zdarzenie);
        Assert.Equal(before, Snapshot(s)); s.Wyjscia.Zatwierdz(p); Repeat(s, p);
        Assert.Single(s.Events, e => e.Kategoria is "Uwaga" or "Ocena");
    }

    [Fact]
    public void PonowneWejscieWTenSamPlanWidzialoRezerwacjePrzedAlarmem()
    {
        var s = New();
        var p = s.Kz.Evaluate(KrokKz.Statusy, partia: Batch(new StatusZasilania { Obiekt = Zasilanie.Id, Siec = false, Ups = true, NapiecieNastawcze = true }));
        int callbacks = 0;
        s.AlarmAdded += _ => { callbacks++; Assert.Equal("Committed", s.Wyjscia.Zatwierdz(p).Stan); };
        Assert.Equal("Executed", s.Wyjscia.Zatwierdz(p).Stan);
        Assert.Equal(1, callbacks); Assert.Single(s.Alarms); Assert.Equal(1, s.Wyjscia.Liczniki["duplicates"]);
    }

    [Fact]
    public void BladCallbackuZatrzymujeBramkeBezPowtorzeniaCzesciowegoSkutku()
    {
        var s = New();
        var p = s.Kz.Evaluate(KrokKz.Statusy, partia: Batch(new StatusZasilania { Obiekt = Zasilanie.Id, Siec = false, Ups = true, NapiecieNastawcze = true }));
        s.AlarmAdded += _ => throw new InvalidOperationException("blad probny");
        Assert.Throws<InvalidOperationException>(() => s.Wyjscia.Zatwierdz(p));
        Assert.True(s.Wyjscia.Zatrzymana); Repeat(s, p, "Failed"); Assert.Single(s.Alarms);
        Assert.Throws<InvalidOperationException>(() => s.Kz.Evaluate(KrokKz.Blokady));
        Assert.Throws<InvalidOperationException>(() => s.Tick(0.1));
    }

    [Fact]
    public void PrzechwyconyBladZagniezdzonegoPlanuZatrzymujeTakzePlanNadrzedny()
    {
        var s = New(); s.Tick(0.25); s.Urzadzenia.Osie(new HashSet<string>());
        string sw = s.Sws.Keys.First();
        string sec = s.L.Sections.Values.First(x => x.AxleCounter && x.Id != s.L.Switches[sw].Sec).Id;
        var cache = Assert.IsType<Dictionary<string, string>>(Private(s.Urzadzenia, "ostStatus"));
        string before = cache["odcinek|" + sec];
        var parent = s.Kz.Evaluate(KrokKz.Statusy, partia: Batch(
            new StatusZasilania { Obiekt = Zasilanie.Id, Siec = false, Ups = true, NapiecieNastawcze = true },
            new StatusOdcinka { Obiekt = sec, Pojazd = false, Stan = "occ" }));
        Assert.True(parent.Efekty.Length >= 3);
        Assert.Contains(parent.Efekty.SkipWhile(e => e.Rodzaj != RodzajEfektu.Alarm).Skip(1), e => e.Rodzaj == RodzajEfektu.Status && e.Obiekt == sec);
        PlanWyjscia? child = null; int swallowed = 0;
        s.AlarmAdded += alarm =>
        {
            // Rejestrator pokazuje obiekt bez prefiksu obszaru („6”, nie „LW:6”) – dopasowanie po typie obiektu.
            if (alarm.TypObiektu == "Zwrotnica") throw new InvalidOperationException("blad wewnatrz zagniezdzonego planu");
            if (alarm.Obiekt != "SIEĆ") return;
            try
            {
                child = s.Kz.Evaluate(KrokKz.Statusy, partia: Batch(new StatusZwrotnicy { Obiekt = sw, Polozenie = null, Przekladanie = false }));
                s.Wyjscia.Zatwierdz(child);
            }
            catch (InvalidOperationException) { swallowed++; } // integrator błędnie połknął wyjątek; fail-stop nadal obowiązuje
        };
        Assert.Throws<InvalidOperationException>(() => s.Wyjscia.Zatwierdz(parent));
        Assert.Equal(1, swallowed); Assert.NotNull(child); Assert.True(s.Wyjscia.Zatrzymana);
        Assert.Contains(child!.Efekty, e => e.Rodzaj == RodzajEfektu.Alarm && e.Obiekt == sw); // błąd naprawdę powstał w planie zagnieżdżonym
        Assert.Equal(before, cache["odcinek|" + sec]); // ostatni efekt nadrzędnego planu nie doszedł do wspólnego podglądu
        Assert.Equal("Failed", s.Wyjscia.Zatwierdz(parent).Stan);
        Assert.Equal("Failed", s.Wyjscia.Zatwierdz(child).Stan);
        var retained = Assert.IsType<Dictionary<long, (PlanWyjscia Plan, RozstrzygniecieWyjscia Wynik)>>(Private(s.Wyjscia, "wyniki"));
        Assert.DoesNotContain(retained, entry => entry.Key >= parent.Numer && entry.Value.Wynik.Stan == "Executed");
        Assert.Throws<InvalidOperationException>(() => s.Tick(0.25));
    }

    [Fact]
    public void PrzechwyconyBladZagniezdzonegoPlanuWOstatnimEfekcieNieKonczySieExecuted()
    {
        // A05: zagnieżdżony plan pada w callbacku OSTATNIEGO efektu nadrzędnego; bez kontroli po efekcie
        // nadrzędny plan zwróciłby „Executed” mimo zatrzymanej bramki (żaden następny efekt nie sprawdza gotowości).
        var s = New(); s.Tick(0.25); s.Urzadzenia.Osie(new HashSet<string>());
        string sw = s.Sws.Keys.First();
        var parent = s.Kz.Evaluate(KrokKz.Statusy, partia: Batch(new StatusZasilania { Obiekt = Zasilanie.Id, Siec = false, Ups = true, NapiecieNastawcze = true }));
        Assert.Equal(RodzajEfektu.Alarm, parent.Efekty[^1].Rodzaj);
        long committedBefore = s.Wyjscia.Liczniki["committed"];
        PlanWyjscia? child = null; int swallowed = 0;
        s.AlarmAdded += alarm =>
        {
            if (alarm.TypObiektu == "Zwrotnica") throw new InvalidOperationException("blad wewnatrz zagniezdzonego planu");
            if (alarm.Obiekt != "SIEĆ") return;
            try
            {
                child = s.Kz.Evaluate(KrokKz.Statusy, partia: Batch(new StatusZwrotnicy { Obiekt = sw, Polozenie = null, Przekladanie = false }));
                s.Wyjscia.Zatwierdz(child);
            }
            catch (InvalidOperationException) { swallowed++; }
        };
        Assert.Throws<InvalidOperationException>(() => s.Wyjscia.Zatwierdz(parent));
        Assert.Equal(1, swallowed); Assert.True(s.Wyjscia.Zatrzymana);
        Assert.Contains(child!.Efekty, e => e.Rodzaj == RodzajEfektu.Alarm && e.Obiekt == sw);
        Assert.Equal("Failed", s.Wyjscia.Zatwierdz(parent).Stan);
        Assert.Equal("Failed", s.Wyjscia.Zatwierdz(child).Stan);
        Assert.Equal(committedBefore + 2, s.Wyjscia.Liczniki["committed"]); // oba plany zarezerwowane, żaden nie jest „Executed”
        var retained = Assert.IsType<Dictionary<long, (PlanWyjscia Plan, RozstrzygniecieWyjscia Wynik)>>(Private(s.Wyjscia, "wyniki"));
        Assert.DoesNotContain(retained, entry => entry.Key >= parent.Numer && entry.Value.Wynik.Stan == "Executed");
    }

    [Fact]
    public void KonfliktTresciIPlanInnegoSwiataSaOdrzuconePrzedSkutkiem()
    {
        var a = New(); var b = New(); var p = Plan(a, "ONN");
        string beforeB = Snapshot(b);
        Assert.Equal("WORLD_MISMATCH", b.Wyjscia.Zatwierdz(p).Powod); Assert.Equal(beforeB, Snapshot(b));
        a.Wyjscia.Zatwierdz(p);
        var forged = new PlanWyjscia(p.Swiat, p.Numer, p.Cykl, p.CzasSymulacjiMs, p.Krok, p.InputHash,
            p.Efekty.Select(e => e with { Wartosc = "1" }).ToImmutableArray(), p.Wynik);
        string beforeA = Snapshot(a);
        Assert.Equal("PLAN_ID_CONFLICT", a.Wyjscia.Zatwierdz(forged).Powod); Assert.Equal(beforeA, Snapshot(a));
    }

    [Fact]
    public void WygaslePotwierdzenieNieOtwieraDrogiDoStaregoEffectId()
    {
        var s = New(); var old = Plan(s, "ONN"); s.Wyjscia.Zatwierdz(old);
        s.Wyjscia.Zatwierdz(Plan(s, "NN"));
        for (int i = 0; i <= BramkaWyjsc.MaxPotwierdzen; i++) s.Wyjscia.Zatwierdz(s.Kz.Evaluate(KrokKz.WszystkieStoj));
        string before = Snapshot(s);
        Assert.Equal("PLAN_TOO_OLD", s.Wyjscia.Zatwierdz(old).Powod);
        Assert.Equal(before, Snapshot(s)); Assert.True(s.Power.NN);
        Assert.InRange(s.Wyjscia.Decyzje.Length, 1, BramkaWyjsc.MaxPotwierdzen);
    }

    [Fact]
    public void PlanPoZmianieCzasuJestOdrzuconyIZwalniaPropozycje()
    {
        var s = New(); var old = Plan(s, "ONN");
        s.T += 1; // zaufana fasada historyczna nadal ma publiczne pole czasu; starego planu nie wolno przyjąć
        string before = Snapshot(s);
        var result = s.Wyjscia.Zatwierdz(old);
        Assert.Equal("Rejected", result.Stan); Assert.Equal("PLAN_NOT_CURRENT", result.Powod);
        Assert.Equal(before, Snapshot(s)); Assert.True(s.Power.NN); Assert.True(s.Urzadzenia.Zasilanie.NN);
        var fresh = Plan(s, "ONN");
        Assert.True(fresh.Numer > old.Numer);
        Assert.Equal("Executed", s.Wyjscia.Zatwierdz(fresh).Stan); Assert.False(s.Power.NN);
    }

    [Theory]
    [InlineData("Tick")]
    [InlineData("Exec")]
    [InlineData("ApplyFault")]
    public void OczekujacaPropozycjaBlokujeZmianeWejsciaPrzedJakimkolwiekSkutkiem(string operation)
    {
        var s = New(); var pending = Plan(s, "ONN"); string before = Snapshot(s);
        Assert.Throws<InvalidOperationException>(() =>
        {
            switch (operation)
            {
                case "Tick": s.Tick(0.25); break;
                case "Exec": s.Exec("ONN", true); break;
                case "ApplyFault": s.Instruktor.ApplyFault("mains", null!, false); break;
            }
        });
        Assert.Equal(before, Snapshot(s));
        Assert.Same(pending, Plan(s, "ONN")); // odrzucona operacja nie podmieniła ani nie unieważniła planu
        Assert.Equal("Rejected", s.Wyjscia.Odrzuc(pending).Stan);
        Assert.Equal("Executed", s.Wyjscia.Zatwierdz(Plan(s, "ONN")).Stan);
    }

    [Fact]
    public void PlanWypartyPodczasCallbackuNieWracaDoSlownikaPotwierdzen()
    {
        var s = New();
        var parent = s.Kz.Evaluate(KrokKz.Statusy, partia: Batch(new StatusZasilania { Obiekt = Zasilanie.Id, Siec = false, Ups = true, NapiecieNastawcze = true }));
        int callbacks = 0;
        s.AlarmAdded += _ =>
        {
            callbacks++;
            for (int i = 0; i <= BramkaWyjsc.MaxPotwierdzen; i++)
                Assert.Equal("Executed", s.Wyjscia.Zatwierdz(s.Kz.Evaluate(KrokKz.WszystkieStoj)).Stan);
        };
        Assert.Equal("Executed", s.Wyjscia.Zatwierdz(parent).Stan);
        Assert.Equal(1, callbacks);
        var retained = Assert.IsAssignableFrom<System.Collections.IDictionary>(Private(s.Wyjscia, "wyniki"));
        Assert.InRange(retained.Count, 1, BramkaWyjsc.MaxPotwierdzen);
        Assert.False(retained.Contains(parent.Numer));
        string before = Snapshot(s);
        for (int i = 0; i < 1000; i++) Assert.Equal("PLAN_TOO_OLD", s.Wyjscia.Zatwierdz(parent).Powod);
        Assert.Equal(before, Snapshot(s)); Assert.Single(s.Alarms); Assert.Equal(1, callbacks);
        Assert.InRange(retained.Count, 1, BramkaWyjsc.MaxPotwierdzen);
    }

    [Fact]
    public void DecyzjeCommittedIRejectedWchodzaRazDoNastepnegoRegularnegoWejscia()
    {
        var s = New(); string kz = s.Wejscia.Trasa!.KzIds[0];
        var a = new ZapisWejsc(); var b = new ZapisWejsc();
        using var subA = s.Wejscia.Subskrybuj(kz, a); using var subB = s.Wejscia.Subskrybuj(kz, b);
        var accepted = Plan(s, "ONN"); Assert.Equal("Executed", s.Wyjscia.Zatwierdz(accepted).Stan);
        var rejected = s.Kz.Evaluate(KrokKz.WszystkieStoj); Assert.Equal("Rejected", s.Wyjscia.Odrzuc(rejected).Stan);
        for (int i = 0; i < 1000; i++)
        {
            Assert.Equal("Executed", s.Wyjscia.Zatwierdz(accepted).Stan);
            Assert.Equal("Rejected", s.Wyjscia.Zatwierdz(rejected).Stan);
        }
        Assert.DoesNotContain(a.Wejscia, w => w.Faza is FazaWejscia.Karty or FazaWejscia.Ruch);
        s.Tick(0.25);
        var inputA = a.Wejscia.First(w => w.Faza == FazaWejscia.Karty);
        var inputB = b.Wejscia.First(w => w.Faza == FazaWejscia.Karty);
        Assert.Same(inputA, inputB);
        var committed = Assert.Single(inputA.Rozstrzygniecia, d => d.PlanId == accepted.PlanId);
        var refused = Assert.Single(inputA.Rozstrzygniecia, d => d.PlanId == rejected.PlanId);
        Assert.Equal("Committed", committed.Stan); Assert.Equal("Rejected", refused.Stan);
        Assert.True(committed.NrDecyzji < refused.NrDecyzji);
        Assert.Equal(accepted.ContentHash, committed.ContentHash); Assert.Equal(rejected.ContentHash, refused.ContentHash);
        Assert.All(inputA.Rozstrzygniecia.Zip(inputA.Rozstrzygniecia.Skip(1)), pair => Assert.True(pair.First.NrDecyzji < pair.Second.NrDecyzji));
        s.Tick(0.25);
        Assert.Single(a.Wejscia.SelectMany(w => w.Rozstrzygniecia), d => d.PlanId == accepted.PlanId);
        Assert.Single(a.Wejscia.SelectMany(w => w.Rozstrzygniecia), d => d.PlanId == rejected.PlanId);
        Assert.Equal(a.Wejscia.Count, b.Wejscia.Count);
        Assert.All(a.Wejscia.Zip(b.Wejscia), pair => Assert.Same(pair.First, pair.Second));

        // Rzeczywisty plan, decyzje i wejście do obejrzenia niezależnie od raportu testu.
        string directory = TestArtifacts.Directory("S02A03-plan-przyklad");
        File.WriteAllText(Path.Combine(directory, "plan.json"), accepted.Kanoniczny());
        File.WriteAllText(Path.Combine(directory, "decyzje.json"), JsonSerializer.Serialize(inputA.Rozstrzygniecia, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(directory, "wejscie.json"), inputA.Kanoniczny());
        File.WriteAllText(Path.Combine(directory, "sha256.json"), JsonSerializer.Serialize(new
        {
            plan = accepted.ContentHash, wejscie = inputA.Sha256(),
            decyzje = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, "decyzje.json")))),
        }));
    }

    [Fact]
    public void CallbackAlarmuWidziKoncowyStanPartiiAZagniezdzonaUsterkaNieJestNadpisana()
    {
        var s = New(); string sec = s.L.Sections.Values.First(x => x.AxleCounter).Id;
        var p = s.Kz.Evaluate(KrokKz.Statusy, partia: Batch(
            new StatusZasilania { Obiekt = Zasilanie.Id, Siec = false, Ups = true, NapiecieNastawcze = true },
            new StatusOdcinka { Obiekt = sec, Pojazd = false, Stan = "occ" }));
        Assert.True(s.Power.Mains); Assert.Null(s.Secs[sec].Fault);
        int callbacks = 0; string? zagniezdzonyStatus = null;
        var cache = Assert.IsType<Dictionary<string, string>>(Private(s.Urzadzenia, "ostStatus"));
        s.AlarmAdded += alarm =>
        {
            if (alarm.Obiekt != "SIEĆ") return;
            callbacks++;
            // S02A.03: cała proponowana partia jest przyjęta przed pierwszym opublikowanym alarmem.
            Assert.False(s.Power.Mains); Assert.Equal("occ", s.Secs[sec].Fault);
            s.Instruktor.ApplyFault("secOcc", sec, true);
            Assert.Equal("reset", s.Secs[sec].Fault);
            zagniezdzonyStatus = cache["odcinek|" + sec];
        };
        Assert.Equal("Executed", s.Wyjscia.Zatwierdz(p).Stan);
        Assert.Equal(1, callbacks); Assert.Equal("reset", s.Secs[sec].Fault);
        Assert.Equal("reset", s.Urzadzenia.Odcinki[sec].Stan); Assert.False(s.Power.Mains);
        Assert.NotNull(zagniezdzonyStatus); Assert.Equal(zagniezdzonyStatus, cache["odcinek|" + sec]);
        Assert.Equal(1, s.Wyjscia.Liczniki["supersededStatuses"]);
        Repeat(s, p); Assert.Equal("reset", s.Secs[sec].Fault);
        s.Urzadzenia.Osie(new HashSet<string>());
        Assert.Equal(zagniezdzonyStatus, cache["odcinek|" + sec]);
    }

    [Fact]
    public void ZagniezdzonyStatusInnegoObiektuNiePomijaPosrednichStatusowPartii()
    {
        var s = New(); s.Tick(0.25); s.Urzadzenia.Osie(new HashSet<string>());
        var secs = s.L.Sections.Values.Where(x => x.AxleCounter).Take(2).Select(x => x.Id).ToArray();
        Assert.Equal(2, secs.Length); string target = secs[0], other = secs[1];
        int odWpisu = s.Urzadzenia.Dziennik.Count;
        var p = s.Kz.Evaluate(KrokKz.Statusy, partia: Batch(
            new StatusZasilania { Obiekt = Zasilanie.Id, Siec = false, Ups = true, NapiecieNastawcze = true },
            new StatusOdcinka { Obiekt = target, Stan = "occ" },
            new StatusOdcinka { Obiekt = target, Stan = "reset" }));
        s.AlarmAdded += a => { if (a.Obiekt == "SIEĆ") s.Instruktor.ApplyFault("secOcc", other, false); };
        s.Wyjscia.Zatwierdz(p);
        var wpisy = s.Urzadzenia.Dziennik.Skip(odWpisu).Where(w => w.Rodzaj == "odcinek" && w.Obiekt == target).ToArray();
        Assert.Equal(2, wpisy.Length);
        Assert.Contains("bez pojazdu", wpisy[0].Tresc); Assert.Contains("zerowania", wpisy[1].Tresc);
        Assert.Equal(0, s.Wyjscia.Liczniki["supersededStatuses"]);
    }

    [Fact]
    public void PodsumowanieKrotkiegoSwiataZawieraWszystkiePlanyINiePowielaSieBezZmian()
    {
        foreach (var (id, code, fields) in PlanWyjsciaLog.Definicje)
            Assert.True(KatalogZdarzen.Zarejestruj(new DefinicjaZdarzenia(id, code, KontraktLogu.Strumien.Diagnostyka, fields)));
        string directory = TestArtifacts.Directory("S02A03-krotkie-podsumowanie");
        long effects = 0;
        using (var engine = SilnikLogow.Uruchom(new OpcjeLogow { Aplikacja = "Lcs.WorldHost", Katalog = directory, Profil = ProfilLogow.Serwer }))
        {
            var s = New(new SimulatorOptions { PlanWyjsciaLogger = engine.Modul(PlanWyjsciaLog.Modul, PlanWyjsciaLog.Wersja) });
            for (int i = 0; i < 10; i++)
            {
                var p = s.Kz.Evaluate(KrokKz.KodySygnalow); effects += p.Efekty.Length;
                Assert.Equal("Executed", s.Wyjscia.Zatwierdz(p).Stan);
            }
            Assert.True(effects > 0); Assert.Equal(10, s.Wyjscia.Liczniki["committed"]);
            Assert.Equal(0, s.Wyjscia.Liczniki["detailsSuppressed"]);
            s.Wyjscia.PodsumujNaKoniec(); s.Wyjscia.PodsumujNaKoniec();
            var state = engine.Zatrzymaj(); Assert.True(state.ZamkniecieKompletne); Assert.Equal(0, state.StratyRazem);
        }
        var records = TestArtifacts.Records(directory).Where(r => Text(r, "moduleId") == PlanWyjsciaLog.Modul).ToArray();
        Assert.Equal(10, records.Count(r => Text(r, "eventCode") == PlanWyjsciaLog.KodZatwierdzono));
        var summary = Assert.Single(records, r => Text(r, "eventCode") == PlanWyjsciaLog.KodPodsumowanie && r.GetProperty("counters").GetProperty("committed").GetInt64() == 10);
        Assert.Equal(effects, summary.GetProperty("counters").GetProperty("effectsExecuted").GetInt64());
        Assert.Equal(0, summary.GetProperty("counters").GetProperty("detailsSuppressed").GetInt64());
    }

    [Fact]
    public void F24WyjscieMaOgraniczoneLogiRedakcjeIPodsumowanie()
    {
        foreach (var (id, code, fields) in PlanWyjsciaLog.Definicje)
            Assert.True(KatalogZdarzen.Zarejestruj(new DefinicjaZdarzenia(id, code, KontraktLogu.Strumien.Diagnostyka, fields)));
        string directory = TestArtifacts.Directory("S02A03-logi-wyjscia");
        using (var engine = SilnikLogow.Uruchom(new OpcjeLogow { Aplikacja = "Lcs.WorldHost", Katalog = directory, Profil = ProfilLogow.Serwer }))
        {
            var s = New(new SimulatorOptions { PlanWyjsciaLogger = engine.Modul(PlanWyjsciaLog.Modul, PlanWyjsciaLog.Wersja) });
            for (int i = 0; i < 40; i++) s.Wyjscia.Zatwierdz(s.Kz.Evaluate(KrokKz.WszystkieStoj));
            for (int i = 0; i < 40; i++) s.Wyjscia.Odrzuc(s.Kz.Evaluate(KrokKz.WszystkieStoj));
            s.Wyjscia.PodsumujNaKoniec();
            var p = s.Kz.Evaluate(KrokKz.Statusy, partia: Batch(new StatusZasilania { Obiekt = Zasilanie.Id, Siec = false, Ups = true, NapiecieNastawcze = true }));
            s.AlarmAdded += _ => throw new InvalidOperationException("password=" + ProbaLogow.ZnacznikSekretu);
            Assert.Throws<InvalidOperationException>(() => s.Wyjscia.Zatwierdz(p));
            var state = engine.Zatrzymaj(); Assert.True(state.ZamkniecieKompletne); Assert.Equal(0, state.StratyRazem);
        }
        var records = TestArtifacts.Records(directory).Where(r => Text(r, "moduleId") == PlanWyjsciaLog.Modul).ToArray();
        Assert.Single(records, r => Text(r, "eventCode") == PlanWyjsciaLog.KodGotowa);
        Assert.Equal(16, records.Count(r => Text(r, "eventCode") == PlanWyjsciaLog.KodZatwierdzono));
        Assert.Equal(16, records.Count(r => Text(r, "eventCode") == PlanWyjsciaLog.KodOdrzucono));
        Assert.Single(records, r => Text(r, "eventCode") == PlanWyjsciaLog.KodBlad);
        Assert.Contains(records, r => Text(r, "eventCode") == PlanWyjsciaLog.KodPodsumowanie && r.GetProperty("counters").GetProperty("detailsSuppressed").GetInt64() >= 48);
        Assert.DoesNotContain(ProbaLogow.ZnacznikSekretu, string.Join('\n', records.Select(r => r.GetRawText())), StringComparison.Ordinal);
        Assert.All(records, r => { Assert.Equal(1, r.GetProperty("schemaVersion").GetInt32()); Assert.Equal(PlanWyjsciaLog.Wersja, Text(r, "moduleVersion")); });
    }

    static string? Text(JsonElement e, string field) => e.TryGetProperty(field, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
    sealed class ZapisWejsc : IOdbiorcaWejscCyklu
    {
        public readonly List<WejscieCyklu> Wejscia = new();
        public void Odbierz(WejscieCyklu input) => Wejscia.Add(input);
    }
    sealed class CountLogger : ILogger
    {
        public int Count;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter) => Count++;
    }
}

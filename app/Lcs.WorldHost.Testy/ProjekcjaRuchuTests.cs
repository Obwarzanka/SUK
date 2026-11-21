using System.Collections.Immutable;
using LcsWidzew.Model;
using LcsWidzew.Sim;
using Xunit;

namespace Lcs.WorldHost.Testy;

/// <summary>S02A.04: adapter zachowanego kroku 8 korzysta z wartości zatwierdzonego wyjścia, bez prywatnych replik.</summary>
public sealed class ProjekcjaRuchuTests
{
    static string Fixture => Path.Combine(AppContext.BaseDirectory, "TestData", "station_test.json");
    static Simulator New() => new(Station.Load(Fixture));
    static PolecenieSystemowe Command(string code, params string[] objects) => new(code, objects, 0, objects.FirstOrDefault() ?? "");
    static RouteDef Route(Simulator s) => s.RouteTable.Values.SelectMany(x => x)
        .First(r => r.Type == "P" && r.EndKind == "sig" && s.WhyNot(r).Count == 0);
    static ProjekcjaRuchu Stub() => new(37,
        [("bez-karty", Aspect.Sz, true), ("manewr", Aspect.Shunt, false), ("bez-przebiegu", Aspect.Train, false)],
        [("pociag", "train"), ("manewr", "shunt")], ["manewr"], [("tor", 60)]);

    [Fact]
    public void JawnyStubWartosciObslugujeHistoryczneZaleznosciBezKz()
    {
        var p = Stub();
        var a = new AdapterRuchu(() => p, _ => null!, id => id == "z-pojazdem");
        Assert.Equal(Aspect.Stop, a.Aspekt("bez-karty")); // NoData ogranicza Permits.
        Assert.True(a.Zastepczy("bez-karty")); // Dawny fallback Sz zachowany oddzielnie.
        Assert.True(a.DrogaPociagowa("pociag"));
        Assert.False(a.DrogaPociagowa("manewr"));
        Assert.True(a.DrogaManewrowa("manewr"));
        Assert.True(a.DrogaManewrowa("z-pojazdem"));
        Assert.False(a.DrogaManewrowa("pociag"));
        Assert.True(a.SygnalZmianyKierunku("manewr"));
        Assert.False(a.SygnalZmianyKierunku("bez-przebiegu"));
        Assert.Equal(60, a.LimitPrzebiegu("tor"));
        Assert.Equal(1e4, a.LimitPrzebiegu("nieznany"));
    }

    [Theory]
    [InlineData("S1", Aspect.Stop, false)]
    [InlineData("Sz", Aspect.Sz, true)]
    [InlineData("Ms2", Aspect.Shunt, false)]
    [InlineData("", Aspect.Stop, false)]
    public void WyswietlanySygnalKartyMaPierwszenstwoTakzeGdyCiemny(string kod, Aspect expected, bool sz)
    {
        var a = new AdapterRuchu(Stub, _ => kod, _ => false);
        Assert.Equal(expected, a.Aspekt("bez-karty"));
        Assert.Equal(sz, a.Zastepczy("bez-karty"));
    }

    [Fact]
    public void KonstruktorProjekcjiKopiujeWejscioweKontenery()
    {
        var sygnaly = new List<(string, Aspect, bool)> { ("S", Aspect.Sz, false) };
        var odcinki = new List<(string, string)> { ("o", "train") };
        var manewry = new List<string> { "S" };
        var limity = new List<(string, double)> { ("e", 40) };
        var zajetosci = new List<(string, bool)> { ("o", true) };
        var p = new ProjekcjaRuchu(7, sygnaly, odcinki, manewry, limity, zajetosci);
        sygnaly.Clear(); odcinki[0] = ("o", "shunt"); manewry.Clear(); limity[0] = ("e", 160);
        zajetosci[0] = ("o", false);
        Assert.Equal(new SygnalDlaRuchu(Aspect.Sz, false), p.Sygnaly["S"]);
        Assert.Equal("train", p.Utwierdzenia["o"]);
        Assert.Contains("S", p.PoczatkiManewrow);
        Assert.Equal(40, p.LimityPrzebiegow["e"]);
        Assert.True(p.Zajetosci["o"]);
        Assert.Empty(p.Sygnaly.SetItem("inny", new(Aspect.Train, false)).Keys.Except(["S", "inny"]));
        Assert.False(p.Sygnaly.ContainsKey("inny"));
    }

    [Theory]
    [InlineData(false, null, false, false)]
    [InlineData(true, null, false, true)]
    [InlineData(false, "occ", false, true)]
    [InlineData(false, "reset", false, true)]
    [InlineData(false, "await", false, true)]
    [InlineData(false, null, true, false)]
    [InlineData(true, null, true, true)]
    public void ZajetoscPodstawieniaZachowujePojazdIUsterkeBezDodaniaRegulyNoData(bool pojazd, string? fault, bool brakDanych, bool expected)
    {
        var s = New(); var sec = s.Secs.Keys.First();
        s.Secs[sec].TrainOcc = pojazd;
        s.Secs[sec].Fault = fault!;
        s.Secs[sec].NoData = brakDanych;
        s.UniewaznijProjekcjeRuchu(3);
        var a = new AdapterRuchu(() => s.ProjekcjaRuchu, _ => null!, _ => !pojazd);
        Assert.Equal(expected, a.Zajety(sec));
        Assert.Equal(expected, s.ProjekcjaRuchu.Zajetosci[sec]);
        // Inny stan osi w terenie nie zastępuje potwierdzonej zajętości w tej historycznej decyzji.
        Assert.Equal(expected, s.Kz.SecOcc(sec));
    }

    [Fact]
    public void BrakWymaganejZajetosciWProjekcjiNiePotwierdzaWolnegoToru()
    {
        var s = New(); var sec = s.L.Sections.Values.First(x => x.StationTrack && x.Edges.Count > 0).Id;
        var p = new ProjekcjaRuchu(9, [], [], [], []);
        var trains = new Trains(s, new AdapterRuchu(() => p, _ => null!, _ => false));
        Assert.Throws<KeyNotFoundException>(() => trains.SpawnOnTrack(new TimetableEntry { Num = "stub", Cat = "MAN", StartSec = sec, Loco = true }));
        Assert.Empty(trains.List);
    }

    [Fact]
    public void PodstawienieKorzystaZProjekcjiStubuZamiastZPrywatnegoStanuKz()
    {
        var s = New(); var sec = s.L.Sections.Values.First(x => x.StationTrack && x.Edges.Count > 0).Id;
        var p = new ProjekcjaRuchu(9, [], [], [], [], [(sec, true)]);
        var a = new AdapterRuchu(() => p, _ => null!, _ => false);
        var trains = new Trains(s, a);
        Assert.False(s.Kz.SecOcc(sec));
        Assert.False(trains.SpawnOnTrack(new TimetableEntry { Num = "stub", Cat = "MAN", StartSec = sec, Loco = true }));
        Assert.Empty(trains.List);
        s.Secs[sec].Fault = "occ";
        p = new ProjekcjaRuchu(10, [], [], [], [], [(sec, false)]);
        Assert.True(s.Kz.SecOcc(sec));
        Assert.True(trains.SpawnOnTrack(new TimetableEntry { Num = "stub", Cat = "MAN", StartSec = sec, Loco = true }));
        Assert.Single(trains.List);
    }

    [Fact]
    public void ZachowanaMigawkaNieDostajeZmienianychObiektowAniListKz()
    {
        var s = New();
        var sygnal = s.Sigs.Keys.First();
        var odcinek = s.Secs.Keys.First();
        var rd = Route(s);
        var edge = rd.Edges.First();
        s.Sigs[sygnal].Aspect = Aspect.Sz;
        s.Secs[odcinek].LockType = "shunt";
        s.Routes["syntetyczny"] = new Route { Start = rd.Start, Type = "P", VSet = 40, Edges = [edge] };
        s.UniewaznijProjekcjeRuchu(7); // Syntetyczny stan fixture, nie ścieżka produkcyjna.
        var p = s.ProjekcjaRuchu;
        Assert.Same(p, s.ProjekcjaRuchu);
        s.Sigs[sygnal].Aspect = Aspect.Stop;
        s.Secs[odcinek].LockType = null!;
        s.Routes["syntetyczny"].Edges.Clear();
        s.Routes.Clear();
        Assert.Equal(Aspect.Sz, p.Sygnaly[sygnal].Aspekt);
        Assert.Equal("shunt", p.Utwierdzenia[odcinek]);
        Assert.True(p.LimityPrzebiegow.ContainsKey(edge));
        Assert.Equal(7, p.NumerPlanu);
        Assert.Same(p, s.ProjekcjaRuchu);
    }

    [Fact]
    public void LimityZachowujaVSetTakzePodczasNastawianiaIPrzyZwolnionychOdcinkach()
    {
        var s = New(); var rd = Route(s); var edge = rd.Edges.First();
        s.Routes["pierwszy"] = new Route { Start = rd.Start, Type = "P", State = "setting", VSet = 40, Rel = 10, Edges = [edge] };
        s.Routes["drugi"] = new Route { Start = rd.Start, Type = "P", State = "set", VSet = 60, Edges = [edge] };
        s.Routes["manewr"] = new Route { Start = rd.Start, Type = "M", State = "setting", VSet = 20, Edges = [edge] };
        s.UniewaznijProjekcjeRuchu(1);
        double expected = s.Routes.Values.Where(r => r.Type == "P" && r.VSet > 0 && r.Edges.Contains(edge))
            .Select(r => Math.Min(r.VSet, s.Sygnalizowana(r).V)).Min();
        Assert.Equal(expected, s.ProjekcjaRuchu.LimityPrzebiegow[edge]);
        Assert.Contains(rd.Start, s.ProjekcjaRuchu.PoczatkiManewrow);
    }

    [Fact]
    public void EvaluateIOdrzucenieNiePublikujaProponowanejDrogi()
    {
        var s = New(); var rd = Route(s);
        var before = s.ProjekcjaRuchu;
        var p = s.Kz.Evaluate(KrokKz.Polecenie, Command("POCIAG", rd.Start, rd.End));
        Assert.True(p.Wynik.Ok);
        Assert.Same(before, s.ProjekcjaRuchu);
        s.Wyjscia.Odrzuc(p);
        Assert.Same(before, s.ProjekcjaRuchu);
        Assert.Empty(s.ProjekcjaRuchu.Utwierdzenia);
    }

    [Fact]
    public void ZatwierdzonaDrogaJestWidocznaPrzedPierwszymCallbackiemEfektu()
    {
        var s = New(); var rd = Route(s);
        var before = s.ProjekcjaRuchu;
        var p = s.Kz.Evaluate(KrokKz.Polecenie, Command("POCIAG", rd.Start, rd.End));
        var obserwacje = new List<ProjekcjaRuchu>();
        s.EventAdded += _ => obserwacje.Add(s.ProjekcjaRuchu);
        s.Wyjscia.Zatwierdz(p);
        var after = s.ProjekcjaRuchu;
        Assert.NotSame(before, after);
        Assert.NotEmpty(obserwacje);
        Assert.All(obserwacje, x => Assert.Same(after, x));
        Assert.Equal(p.Numer, after.NumerPlanu);
        Assert.Contains("train", after.Utwierdzenia.Values);
        Assert.Empty(before.Utwierdzenia);
        Assert.Same(after, s.ProjekcjaRuchu);
        s.Wyjscia.Zatwierdz(p);
        Assert.Same(after, s.ProjekcjaRuchu);
    }

    [Fact]
    public void ZagniezdzoneZatwierdzenieNieZostajeNadpisanePrzezKonczenieStarszegoPlanu()
    {
        var s = New(); var rd = Route(s);
        var p = s.Kz.Evaluate(KrokKz.Polecenie, Command("POCIAG", rd.Start, rd.End));
        ProjekcjaRuchu? nested = null;
        bool inside = false;
        s.EventAdded += _ =>
        {
            if (inside) return;
            inside = true;
            var next = s.Kz.Evaluate(KrokKz.Polecenie, Command("ONN"));
            s.Wyjscia.Zatwierdz(next);
            nested = s.ProjekcjaRuchu;
        };
        s.Wyjscia.Zatwierdz(p);
        Assert.NotNull(nested);
        Assert.True(nested.NumerPlanu > p.Numer);
        Assert.Same(nested, s.ProjekcjaRuchu);
    }
}

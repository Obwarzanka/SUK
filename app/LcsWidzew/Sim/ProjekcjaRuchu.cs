using System.Collections.Immutable;

namespace LcsWidzew.Sim;

/// <summary>Wartości zatwierdzonego sygnału potrzebne przejściowemu adapterowi odłożonego kroku 8.</summary>
public readonly record struct SygnalDlaRuchu(Aspect Aspekt, bool BrakDanych);

/// <summary>
/// Niezmienna projekcja zatwierdzonego wyjścia dla obecnego ruchu. Nie zawiera KZ, Route, SecState, SigState,
/// kart ani delegatów. Nie zastępuje sygnałów fizycznie wyświetlanych przez karty i nie odbiera kroku 8.
/// </summary>
public sealed class ProjekcjaRuchu
{
    public long NumerPlanu { get; }
    public ImmutableDictionary<string, SygnalDlaRuchu> Sygnaly { get; }
    public ImmutableDictionary<string, string> Utwierdzenia { get; }
    public ImmutableHashSet<string> PoczatkiManewrow { get; }
    public ImmutableDictionary<string, double> LimityPrzebiegow { get; }
    public ImmutableDictionary<string, bool> Zajetosci { get; }

    /// <summary>Wejście z samych wartości, również dla jawnego stubu przyszłego dostawcy zatwierdzonych wyjść.</summary>
    public ProjekcjaRuchu(long numerPlanu, IEnumerable<(string Id, Aspect Aspekt, bool BrakDanych)> sygnaly,
        IEnumerable<(string Id, string Rodzaj)> utwierdzenia, IEnumerable<string> poczatkiManewrow,
        IEnumerable<(string Tor, double Predkosc)> limityPrzebiegow,
        IEnumerable<(string Id, bool Zajety)> zajetosci = null)
    {
        NumerPlanu = numerPlanu;
        Sygnaly = sygnaly.ToImmutableDictionary(x => x.Id, x => new SygnalDlaRuchu(x.Aspekt, x.BrakDanych), StringComparer.Ordinal);
        Utwierdzenia = utwierdzenia.ToImmutableDictionary(x => x.Id, x => x.Rodzaj, StringComparer.Ordinal);
        PoczatkiManewrow = poczatkiManewrow.ToImmutableHashSet(StringComparer.Ordinal);
        LimityPrzebiegow = limityPrzebiegow.ToImmutableDictionary(x => x.Tor, x => x.Predkosc, StringComparer.Ordinal);
        Zajetosci = (zajetosci ?? Enumerable.Empty<(string Id, bool Zajety)>())
            .ToImmutableDictionary(x => x.Id, x => x.Zajety, StringComparer.Ordinal);
    }

    internal static ProjekcjaRuchu Zbuduj(KomputerZaleznosci kz, long numerPlanu)
    {
        var limity = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var r in kz.Routes.Values)
        {
            // Dotychczasowy ruch nie filtruje tu State ani Rel; zachowujemy to zachowanie odłożonego kroku 8.
            if (r.Type != "P" || !(r.VSet > 0)) continue;
            double v = Math.Min(r.VSet, kz.Sygnalizowana(r).V);
            foreach (var tor in r.Edges) limity[tor] = Math.Min(limity.GetValueOrDefault(tor, 1e4), v);
        }
        return new(numerPlanu, kz.Sigs.Select(x => (x.Key, x.Value.Aspect, x.Value.NoData)),
            kz.Secs.Where(x => x.Value.LockType != null).Select(x => (x.Key, x.Value.LockType)),
            kz.Routes.Values.Where(r => r.Type == "M").Select(r => r.Start).Where(x => x != null),
            limity.Select(x => (x.Key, x.Value)), kz.Secs.Keys.Select(id => (id, kz.SecOcc(id))));
    }
}

/// <summary>
/// Adapter obecnych reguł ruchu: stan fizyczny ma pierwszeństwo dla widzianego sygnału;
/// pozostałe historyczne zależności pochodzą wyłącznie z jawnej zatwierdzonej projekcji.
/// Dostawca projekcji nie wskazuje żadnej prywatnej repliki ani proponowanego obliczenia.
/// </summary>
public sealed class AdapterRuchu
{
    readonly Func<ProjekcjaRuchu> projekcja;
    readonly Func<string, string> widziany;
    readonly Func<string, bool> pojazd;

    public AdapterRuchu(Func<ProjekcjaRuchu> projekcja, Func<string, string> sygnalWidziany, Func<string, bool> pojazd)
    {
        this.projekcja = projekcja ?? throw new ArgumentNullException(nameof(projekcja));
        widziany = sygnalWidziany ?? throw new ArgumentNullException(nameof(sygnalWidziany));
        this.pojazd = pojazd ?? throw new ArgumentNullException(nameof(pojazd));
    }

    public Aspect Aspekt(string sygnal)
    {
        var kod = widziany(sygnal);
        if (kod != null) return Sygnaly.AspectOf(kod);
        var stan = projekcja().Sygnaly.GetValueOrDefault(sygnal);
        return stan.BrakDanych ? Aspect.Stop : stan.Aspekt;
    }

    // Historyczny fallback Sz nie sprawdza NoData; jego zmiana należy do odłożonego kroku 8.
    public bool Zastepczy(string sygnal) => (widziany(sygnal) ??
        (projekcja().Sygnaly.GetValueOrDefault(sygnal).Aspekt == Aspect.Sz ? "Sz" : null)) == "Sz";
    public bool DrogaPociagowa(string odcinek) => projekcja().Utwierdzenia.GetValueOrDefault(odcinek) == "train";
    public bool DrogaManewrowa(string odcinek) => projekcja().Utwierdzenia.GetValueOrDefault(odcinek) == "shunt" || pojazd(odcinek);
    public bool SygnalZmianyKierunku(string sygnal)
    {
        var p = projekcja();
        return p.Sygnaly.GetValueOrDefault(sygnal).Aspekt is Aspect.Shunt or Aspect.Train or Aspect.Sz && p.PoczatkiManewrow.Contains(sygnal);
    }
    public double LimitPrzebiegu(string tor) => projekcja().LimityPrzebiegow.GetValueOrDefault(tor, 1e4);
    // Podstawienie pociągu zachowuje dotychczasowe SecOcc: pojazd lub Fault; samo NoData nie blokuje podstawienia.
    // Jak dawne SecOcc: brak wymaganego obiektu jest błędem kontraktu, nigdy potwierdzeniem wolnego toru.
    public bool Zajety(string odcinek) => projekcja().Zajetosci[odcinek];
}

public partial class Simulator
{
    ProjekcjaRuchu projekcjaRuchu;
    long numerPlanuRuchu;

    /// <summary>
    /// Bieżąca zatwierdzona projekcja; powstaje leniwie, najwyżej raz pomiędzy zatwierdzeniami.
    /// Odrzucenie lub samo Evaluate nie zmieniają tej projekcji. Zachowana migawka nigdy nie zmienia wartości.
    /// </summary>
    public ProjekcjaRuchu ProjekcjaRuchu
    {
        get
        {
            if (projekcjaRuchu == null)
            {
                projekcjaRuchu = LcsWidzew.Sim.ProjekcjaRuchu.Zbuduj(Kz, numerPlanuRuchu);
                Granice.Projekcja(numerPlanuRuchu, projekcjaRuchu.Sygnaly.Count, projekcjaRuchu.Utwierdzenia.Count, projekcjaRuchu.LimityPrzebiegow.Count);
            }
            return projekcjaRuchu;
        }
    }

    /// <summary>Wyłącznie po przyjęciu stanu przez bramkę, przed publikacją efektów i ich callbackami.</summary>
    internal void UniewaznijProjekcjeRuchu(long numerPlanu)
    {
        numerPlanuRuchu = numerPlanu;
        projekcjaRuchu = null;
    }
}

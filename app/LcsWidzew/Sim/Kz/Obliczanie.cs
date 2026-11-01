using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace LcsWidzew.Sim;

public partial class KomputerZaleznosci
{
    List<EfektWyjscia> efektyObliczenia;
    double? czasObliczenia;
    Dictionary<string, bool> szlakSasiada, oczekujacySasiad;
    bool ruchObliczenia;
    long numerZadania;
    Dictionary<string, string> znaneStatusy = new(StringComparer.Ordinal);
    // Wyłącznie żywy KZ: numer ostatniego zatwierdzonego planu zmieniającego status tego obiektu.
    // Pozwala odrzucić zaległy wpis podglądu po zagnieżdżonym statusie tego samego obiektu.
    readonly Dictionary<string, long> wersjeStatusow = new(StringComparer.Ordinal);
    // Kopia obliczenia czyta znane statusy świata (słownik świata, tylko odczyt podczas obliczenia) i zapisuje własną deltę;
    // przyjęcie nakłada deltę na świat, odrzucenie ją porzuca (S02A.03, wydajność – bez kopii całego słownika).
    Dictionary<string, string> noweStatusy;
    sealed record Propozycja(PlanWyjscia Plan, KomputerZaleznosci Stan, double Czas);
    Propozycja propozycja;
    // Bufor klucza wejścia (wydajność): używany tylko na początku Evaluate żywego KZ, przed obliczeniem.
    readonly StringBuilder kluczBufor = new(4096);
    // Kolejności wynikające wyłącznie z projektu (stałego w trakcie pracy); wspólne dla kopii obliczeń – to nie stan KZ.
    readonly PorzadekProjektu porzadek = new();

    /// <summary>
    /// Oblicza na prywatnej kopii stanu. Nie wywołuje kart, rejestratora, loggera, książki ani statystyki.
    /// Statusy pochodzą z zamrożonej partii; polecenie jest już rozwiązane przez jeden system nadrzędny.
    /// Wielokrotne obliczenie przed decyzją daje ten sam plan i effectId. Jedna propozycja w locie.
    /// </summary>
    public PlanWyjscia Evaluate(KrokKz krok, PolecenieSystemowe polecenie = null, PartiaWejsc partia = null)
    {
        S.Wejscia.PozaOdbiorca(); S.Wyjscia.SprawdzGotowosc();
        if (efektyObliczenia != null) throw new InvalidOperationException("Nie można obliczać z wnętrza obliczenia KZ.");
        // Zamrożenie mutable listy adresów; algorytm i plan nigdy jej nie zachowują.
        var p = polecenie == null ? null : polecenie with { Obiekty = polecenie.Obiekty.ToImmutableArray() };
        if ((krok == KrokKz.Polecenie) != (p != null) || (krok == KrokKz.Statusy) != (partia != null))
            throw new ArgumentException("Krok KZ wymaga właściwego wejścia.");
        // Jeden przejściowy KZ obszaru zachowuje bazową kolejność kart z odczytanego projektu.
        // Rejestracja słowników adapterów jej nie zmienia; powtórzone statusy obiektu zachowują kolejność.
        if (partia != null) partia = porzadek.Uporzadkuj(L, partia);
        var klucz = kluczBufor.Clear().Append((int)krok).Append('|');
        if (p != null)
        {
            KanonJson.Tekst(klucz, p.Kod); KanonJson.Tekst(klucz, p.Arg0); klucz.Append(p.Koniec).Append('|');
            foreach (var id in p.Obiekty) { KanonJson.Tekst(klucz, id); klucz.Append('|'); }
        }
        if (partia != null)
        {
            klucz.Append(partia.NrPartii).Append('|');
            foreach (var s in partia.Statusy) { KanonJson.Tekst(klucz, s.Rodzaj); KanonJson.Tekst(klucz, s.Obiekt); s.PiszPola(klucz); }
        }
        string hash = KanonJson.Sha256(klucz);
        if (propozycja != null)
        {
            if (propozycja.Plan.InputHash == hash && propozycja.Czas == S.T && propozycja.Plan.Cykl == S.NrCyklu) return propozycja.Plan;
            throw new InvalidOperationException("Najpierw rozstrzygnij poprzedni plan KZ.");
        }
        var k = KopiaDoObliczen(); k.propozycja = null; k.czasObliczenia = S.T; k.efektyObliczenia = new();
        // Przejściowy adapter odczytów sąsiada; nie jest interfejsem KZ–KZ ani adapterem ruchu z S02A.04.
        k.ruchObliczenia = S.Trains != null;
        k.szlakSasiada = L.Blocks.Keys.ToDictionary(id => id, S.Otoczenie.SzlakZajetyUSasiada);
        k.oczekujacySasiad = L.Blocks.Keys.ToDictionary(id => id, id => S.Otoczenie.NbrPending(id, 60));
        CmdResult wynik = null;
        switch (krok)
        {
            case KrokKz.Statusy: k.PrzyjmijStatusy(partia); break;
            case KrokKz.Blokady: k.UpdateBlocksCore(); break;
            case KrokKz.Sygnaly: k.UpdateSignalsCore(); break;
            case KrokKz.Przebiegi: k.UpdateRoutesCore(); break;
            case KrokKz.KodySygnalow: k.SignalCodesCore(); break;
            case KrokKz.Polecenie: wynik = k.WykonajCore(p); break;
            case KrokKz.WszystkieStoj: wynik = k.WszystkieStojCore(); break;
            default: throw new ArgumentOutOfRangeException(nameof(krok));
        }
        long n = S.Wyjscia.NastepnyNumer;
        var efekty = k.efektyObliczenia.Select((e, i) => e with { EffectId = $"KZ-{n}-{i + 1}" }).ToImmutableArray();
        var plan = new PlanWyjscia(S.Wyjscia.Swiat, n, S.NrCyklu, (long)Math.Round(S.T * 1000), krok, hash, efekty, wynik);
        k.efektyObliczenia = null;
        propozycja = new(plan, k, S.T);
        return plan;
    }

    internal bool CzyPropozycja(PlanWyjscia plan) => ReferenceEquals(propozycja?.Plan, plan) && propozycja.Czas == S.T && plan.Cykl == S.NrCyklu;
    internal bool CzyWydany(PlanWyjscia plan) => ReferenceEquals(propozycja?.Plan, plan);
    internal void PozaPropozycja()
    {
        if (propozycja != null) throw new InvalidOperationException("Najpierw zatwierdź albo odrzuć bieżący plan KZ.");
    }
    internal long NowyNumerZadania() { PozaPropozycja(); return ++numerZadania; }
    internal void Rozstrzygnij(PlanWyjscia plan, bool przyjmij)
    {
        var p = propozycja;
        if (!ReferenceEquals(p?.Plan, plan)) throw new InvalidOperationException("Plan nie pochodzi z bieżącego obliczenia KZ.");
        propozycja = null;
        if (przyjmij)
        {
            PrzyjmijStan(p.Stan); numerZadania = p.Stan.numerZadania;
            foreach (var klucz in p.Stan.noweStatusy.Keys) wersjeStatusow[klucz] = plan.Numer;
            S.UniewaznijProjekcjeRuchu(plan.Numer);
        }
        ZwolnijKopie(p.Stan, przyjmij);
    }

    void Efekt(RodzajEfektu rodzaj, string obiekt = "", string wartosc = "", string typ = "", string kategoria = "", string zrodlo = "System",
        string klucz = null, WarunekAlarmu warunek = WarunekAlarmu.Brak, string przebieg = null, long zadanie = 0)
    {
        if (efektyObliczenia == null) throw new InvalidOperationException("Skutek KZ musi powstać wewnątrz Evaluate.");
        if (efektyObliczenia.Count >= 8192) throw new InvalidOperationException("Przekroczony limit 8192 efektów planu KZ.");
        efektyObliczenia.Add(new() { Rodzaj = rodzaj, Obiekt = obiekt, Wartosc = wartosc, Typ = typ, Kategoria = kategoria,
            Zrodlo = zrodlo, KluczAlarmu = klucz, Warunek = warunek, Przebieg = przebieg, Zadanie = zadanie });
    }

    void Log(string kategoria, string text, string typ = "", string obiekt = "", string zrodlo = "System")
    {
        if (efektyObliczenia != null) Efekt(RodzajEfektu.Zdarzenie, obiekt, text, typ, kategoria, zrodlo);
        else S.Log(kategoria, text, typ, obiekt, zrodlo); // tylko ostrzeżenia podczas budowy tablicy poza cyklem
    }
    void Alarm(string text, string typ = "", string obiekt = "", string key = null, WarunekAlarmu warunek = WarunekAlarmu.Brak, string przebieg = null, long zadanie = 0)
        => Efekt(RodzajEfektu.Alarm, obiekt, text, typ, klucz: key, warunek: warunek, przebieg: przebieg, zadanie: zadanie);
    void Remark(string text) { if (efektyObliczenia != null) Efekt(RodzajEfektu.Uwaga, wartosc: text); else S.Remark(text); }
    void Status(string typ, string id, string text)
    {
        string klucz = typ + "|" + id;
        if (noweStatusy == null) throw new InvalidOperationException("Status KZ musi powstać wewnątrz Evaluate.");
        if ((noweStatusy.TryGetValue(klucz, out var poprzedni) || znaneStatusy.TryGetValue(klucz, out poprzedni)) && poprzedni == text) return;
        noweStatusy[klucz] = text;
        Efekt(RodzajEfektu.Status, id, text, typ);
    }

    internal bool StatusNadalAktualny(EfektWyjscia e, long numerPlanu)
        => wersjeStatusow.GetValueOrDefault(e.Typ + "|" + e.Obiekt) <= numerPlanu;

    internal bool AlarmTrwa(EfektWyjscia e) => e.Warunek switch
    {
        WarunekAlarmu.BrakSieci => !Power.Mains,
        WarunekAlarmu.ZwrotnicaWRuchu => Sws.TryGetValue(e.Obiekt, out var w) && w.Moving != null,
        WarunekAlarmu.Rozprucie => Sws.TryGetValue(e.Obiekt, out var w) && w.Rozpr,
        WarunekAlarmu.BrakKontroli => Sws.TryGetValue(e.Obiekt, out var w) && w.NoCtrl,
        WarunekAlarmu.BrakDanychSygnalu => Sigs.TryGetValue(e.Obiekt, out var s) && s.NoData,
        WarunekAlarmu.BrakSygnaluPrzebiegu => Routes.TryGetValue(e.Przebieg ?? "", out var r) && !r.Passed && Sigs[r.Start].Aspect == Aspect.Stop,
        WarunekAlarmu.NastawianiePrzebiegu => Routes.TryGetValue(e.Przebieg ?? "", out var r) && r.State == "setting",
        WarunekAlarmu.BrakBlokuKoncowego => Blocks.TryGetValue(e.Obiekt, out var b) && b.AwaitEnd != null,
        WarunekAlarmu.ZadanieBlokady => Blocks.TryGetValue(e.Obiekt, out var b) && b.Req != null,
        WarunekAlarmu.AktualneZadanieBlokady => Blocks.TryGetValue(e.Obiekt, out var b) && b.Req?.Numer == e.Zadanie,
        _ => true,
    };

    /// <summary>
    /// Kolejności wynikające z projektu stacji (bez stanu KZ): porządek kart z <see cref="Model.Station.KolejnoscKart"/> i lista
    /// sygnalizatorów w tym porządku. Liczone raz dla danego projektu – wynik identyczny jak sortowanie przy każdym obliczeniu.
    /// </summary>
    sealed class PorzadekProjektu
    {
        Model.Station dla; int liczbaSygnalow = -1;
        Dictionary<(string Rodzaj, string Obiekt), int> karty;
        string[] sygnaly;

        void Przygotuj(Model.Station l)
        {
            if (ReferenceEquals(dla, l) && liczbaSygnalow == l.Signals.Count) return;
            // Klucze projektu mają postać „rodzaj|id”, rodzaj bez „|” – podział przy pierwszym „|” odtwarza parę jednoznacznie.
            karty = new Dictionary<(string, string), int>();
            foreach (var (k, v) in l.KolejnoscKart) { int i = k.IndexOf('|'); if (i > 0) karty[(k[..i], k[(i + 1)..])] = v; }
            sygnaly = l.Signals.Keys.OrderBy(n => l.KolejnoscKart.GetValueOrDefault("sygnalizator|" + n, int.MaxValue)).ThenBy(n => n, StringComparer.Ordinal).ToArray();
            dla = l; liczbaSygnalow = l.Signals.Count;
        }

        internal IReadOnlyList<string> Sygnaly(Model.Station l) { Przygotuj(l); return sygnaly; }

        /// <summary>Stabilne uporządkowanie partii jak OrderBy(kolejność karty).ThenBy(rodzaj).ThenBy(obiekt); już uporządkowana – bez kopii.</summary>
        internal PartiaWejsc Uporzadkuj(Model.Station l, PartiaWejsc partia)
        {
            Przygotuj(l);
            var st = partia.Statusy;
            int Klucz(StatusUrzadzenia s) => s is StatusZasilania ? -1 : karty.GetValueOrDefault((s.Rodzaj, s.Obiekt), int.MaxValue);
            bool uporzadkowana = true;
            int poprzedni = st.Length > 0 ? Klucz(st[0]) : 0;
            for (int i = 1; i < st.Length && uporzadkowana; i++)
            {
                int biezacy = Klucz(st[i]);
                int c = poprzedni.CompareTo(biezacy);
                if (c == 0) c = string.CompareOrdinal(st[i - 1].Rodzaj, st[i].Rodzaj);
                if (c == 0) c = string.CompareOrdinal(st[i - 1].Obiekt, st[i].Obiekt);
                uporzadkowana = c <= 0; poprzedni = biezacy;
            }
            if (uporzadkowana) return partia;
            return partia with { Statusy = st.OrderBy(Klucz).ThenBy(s => s.Rodzaj, StringComparer.Ordinal).ThenBy(s => s.Obiekt, StringComparer.Ordinal).ToImmutableArray() };
        }
    }

    internal void UpdateBlocks() => S.Wyjscia.Zatwierdz(Evaluate(KrokKz.Blokady));
    internal void UpdateSignals() => S.Wyjscia.Zatwierdz(Evaluate(KrokKz.Sygnaly));
    internal void UpdateRoutes() => S.Wyjscia.Zatwierdz(Evaluate(KrokKz.Przebiegi));
    internal void SignalCodes() => S.Wyjscia.Zatwierdz(Evaluate(KrokKz.KodySygnalow));
    public CmdResult Wykonaj(PolecenieSystemowe p, KontekstOperatora kontekst = null) { var plan = Evaluate(KrokKz.Polecenie, p); S.Wyjscia.Zatwierdz(plan, kontekst); return plan.Wynik; }
    public CmdResult WszystkieStoj(KontekstOperatora kontekst = null) { var plan = Evaluate(KrokKz.WszystkieStoj); S.Wyjscia.Zatwierdz(plan, kontekst); return plan.Wynik; }
}

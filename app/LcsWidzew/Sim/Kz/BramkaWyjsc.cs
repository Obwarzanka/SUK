using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace LcsWidzew.Sim;

/// <summary>
/// Jedno wyjście przejściowego KZ C# (S02A.03). Rezerwacja numeru przed callbackami zapobiega ponownemu wykonaniu effectId.
/// Jedna epoka świata, ograniczone potwierdzenia i znacznik starych numerów; bez trwałości/restartu S06 i selektora grup S02C.
/// </summary>
public sealed class BramkaWyjsc
{
    public const int MaxPotwierdzen = 256, MaxSzczegolow = 16;
    readonly Simulator S;
    readonly ILogger log;
    readonly Dictionary<long, (PlanWyjscia Plan, RozstrzygniecieWyjscia Wynik)> wyniki = new();
    readonly Queue<long> kolejnosc = new();
    readonly Queue<RozstrzygniecieWyjscia> decyzje = new();
    readonly Dictionary<string, int> logKody = new(StringComparer.Ordinal);
    long numer, nrDecyzji, zatwierdzone, odrzucone, duplikaty, wykonane, pominiete, wyparteStatusy, ostatniePodsumowanie = -3000;
    string ostatniStanPodsumowania;
    bool zatrzymana;

    public string Swiat { get; } = Guid.NewGuid().ToString("N");
    public long NastepnyNumer => checked(numer + 1);
    public bool Zatrzymana => zatrzymana;
    public ImmutableArray<RozstrzygniecieWyjscia> Decyzje => decyzje.ToImmutableArray();
    public IReadOnlyDictionary<string, long> Liczniki => new Dictionary<string, long>
    {
        ["committed"] = zatwierdzone, ["rejected"] = odrzucone, ["duplicates"] = duplikaty,
        ["effectsExecuted"] = wykonane, ["detailsSuppressed"] = pominiete, ["stopped"] = zatrzymana ? 1 : 0,
        ["supersededStatuses"] = wyparteStatusy,
    };

    internal BramkaWyjsc(Simulator s, ILogger logger)
    {
        S = s; log = logger;
        Zapisz(4121, null, "SINGLE_CSHARP_REFERENCE", "Ready");
    }

    internal void SprawdzGotowosc()
    {
        if (zatrzymana) throw new InvalidOperationException("Bramka wyjść KZ zatrzymana po błędzie wykonania; wymagany restart świata.");
    }

    /// <summary>Zatwierdza cały wydany plan; wywołanie z tym samym planem, także z callbacku, nie powtarza żadnego skutku.</summary>
    public RozstrzygniecieWyjscia Zatwierdz(PlanWyjscia plan, KontekstOperatora kontekst = null) => Rozstrzygnij(plan, true, "PLAN_ACCEPTED", kontekst);
    /// <summary>Odrzucenie odrzuca również proponowany stan KZ. Nie publikuje jego zdarzeń ani potwierdzenia wykonania.</summary>
    public RozstrzygniecieWyjscia Odrzuc(PlanWyjscia plan) => Rozstrzygnij(plan, false, "PLAN_REJECTED");

    RozstrzygniecieWyjscia Rozstrzygnij(PlanWyjscia plan, bool przyjmij, string powod, KontekstOperatora kontekst = null)
    {
        ArgumentNullException.ThrowIfNull(plan); S.Wejscia.PozaOdbiorca();
        kontekst ??= S.Nadrzedny.Kontekst();
        if (plan.Swiat != Swiat) return Odmowa(plan, "WORLD_MISMATCH");
        if (wyniki.TryGetValue(plan.Numer, out var stary))
        {
            if (!ReferenceEquals(stary.Plan, plan) || stary.Plan.ContentHash != plan.ContentHash) return Odmowa(plan, "PLAN_ID_CONFLICT");
            duplikaty++; Podsumuj(); return stary.Wynik;
        }
        if (plan.Numer <= numer) { duplikaty++; return Odmowa(plan, "PLAN_TOO_OLD"); }
        SprawdzGotowosc();
        if (plan.Numer != NastepnyNumer || !S.Kz.CzyWydany(plan)) return Odmowa(plan, "PLAN_NOT_CURRENT");
        if (przyjmij && !S.Kz.CzyPropozycja(plan)) { przyjmij = false; powod = "PLAN_NOT_CURRENT"; }
        // Rezerwacja przed stanem, statusem, alarmem i każdym innym callbackiem.
        numer = plan.Numer;
        var decyzja = new RozstrzygniecieWyjscia(++nrDecyzji, plan.PlanId, plan.ContentHash, przyjmij ? "Committed" : "Rejected", powod, 0);
        wyniki.Add(plan.Numer, (plan, decyzja)); kolejnosc.Enqueue(plan.Numer); Zapamietaj(decyzja);
        while (kolejnosc.Count > MaxPotwierdzen) wyniki.Remove(kolejnosc.Dequeue());
        if (przyjmij) zatwierdzone++; else odrzucone++;
        int ile = 0; string biezacy = plan.PlanId;
        try
        {
            S.Kz.Rozstrzygnij(plan, przyjmij);
            S.Wejscia.Rozstrzygniecie(decyzja);
            Szczegol(przyjmij ? 4122 : 4123, plan.PlanId, powod, decyzja.Stan);
            if (przyjmij)
                foreach (var efekt in plan.Efekty)
                {
                    // Każdy efekt należy do już zarezerwowanego planu, w jego ustalonej kolejności.
                    // Kontrola po efekcie: jego callback mógł uruchomić zagnieżdżony plan, który zatrzymał bramkę,
                    // a wyjątek został połknięty – wtedy nie wykonujemy dalszych efektów (A05).
                    SprawdzGotowosc(); biezacy = efekt.EffectId;
                    if (efekt.Rodzaj == RodzajEfektu.Status && !S.Kz.StatusNadalAktualny(efekt, plan.Numer)) wyparteStatusy++;
                    else { Wykonaj(efekt, kontekst); wykonane++; ile++; }
                    SprawdzGotowosc();
                }
            SprawdzGotowosc(); // także plan bez efektów nie kończy się „Executed” po zatrzymaniu bramki
            var wynik = decyzja with { Stan = przyjmij ? "Executed" : "Rejected", Wykonane = ile };
            if (wyniki.ContainsKey(plan.Numer)) wyniki[plan.Numer] = (plan, wynik);
            Podsumuj(); return wynik;
        }
        catch (Exception ex)
        {
            // Częściowy skutek lub callback mógł się wykonać. Nie zgadujemy i nie ponawiamy.
            zatrzymana = true;
            var blad = decyzja with { Stan = "Failed", Powod = "EXECUTION_UNKNOWN", Wykonane = ile };
            if (wyniki.ContainsKey(plan.Numer)) wyniki[plan.Numer] = (plan, blad);
            Zapisz(4124, biezacy, "EXECUTION_UNKNOWN", "Failed", ex); // efekt, w trakcie lub po którym nastąpił błąd
            PodsumujNaKoniec(); throw;
        }
    }

    RozstrzygniecieWyjscia Odmowa(PlanWyjscia p, string powod)
    {
        odrzucone++; Szczegol(4123, null, powod, "Rejected"); Podsumuj();
        return new(0, p.PlanId, p.ContentHash, "Rejected", powod, 0);
    }
    void Zapamietaj(RozstrzygniecieWyjscia d) { decyzje.Enqueue(d); if (decyzje.Count > MaxPotwierdzen) decyzje.Dequeue(); }

    void Wykonaj(EfektWyjscia e, KontekstOperatora kontekst)
    {
        switch (e.Rodzaj)
        {
            case RodzajEfektu.Zwrotnica: S.Urzadzenia.Przestaw(e.Obiekt, e.Wartosc); break;
            case RodzajEfektu.Wykolejnica: S.Urzadzenia.Wykolejnica(e.Obiekt, e.Wartosc == "1"); break;
            case RodzajEfektu.Sygnal: S.Urzadzenia.Sygnal(e.Obiekt, e.Wartosc); break;
            case RodzajEfektu.NapiecieNastawcze: S.Urzadzenia.NapiecieNastawcze(e.Wartosc == "1"); break;
            case RodzajEfektu.Agregat: S.Urzadzenia.UruchomAgregat(); break;
            case RodzajEfektu.ZeroLO: S.Urzadzenia.Zeruj(e.Obiekt); break;
            case RodzajEfektu.Zdarzenie: S.Log(e.Kategoria, e.Wartosc, e.Typ, e.Obiekt, e.Zrodlo, kontekst); break;
            case RodzajEfektu.Alarm:
                S.Alarm(e.Wartosc, e.Typ, e.Obiekt, e.KluczAlarmu, e.Warunek == WarunekAlarmu.Brak ? null : () => S.Kz.AlarmTrwa(e), kontekst); break;
            case RodzajEfektu.Uwaga: S.Remark(e.Wartosc, kontekst); break;
            case RodzajEfektu.Status: S.Urzadzenia.LogStatus(e.Typ, e.Obiekt, e.Wartosc); break;
            default: throw new InvalidOperationException("Nieznany efekt planu KZ.");
        }
    }

    void Szczegol(int id, string effectId, string powod, string wynik)
    {
        string kod = id + ":" + powod; logKody.TryGetValue(kod, out int n);
        if (n < MaxSzczegolow) { logKody[kod] = n + 1; Zapisz(id, effectId, powod, wynik); }
        else pominiete++;
    }
    void Zapisz(int id, string effect, string reason, string outcome, Exception ex = null)
        => PlanWyjsciaLog.Zapisz(log, id, effect, reason, outcome, S.NrCyklu, (long)Math.Round(S.T * 1000), Liczniki, ex);
    void Podsumuj() { if (S.NrCyklu - ostatniePodsumowanie >= 3000) PodsumujNaKoniec(); }
    public void PodsumujNaKoniec()
    {
        string stan = $"{numer}/{zatwierdzone}/{odrzucone}/{duplikaty}/{wykonane}/{pominiete}/{wyparteStatusy}/{zatrzymana}";
        if (ostatniStanPodsumowania == stan) return;
        ostatniePodsumowanie = S.NrCyklu; ostatniStanPodsumowania = stan;
        Zapisz(4125, null, "OUTPUT_SUMMARY", zatrzymana ? "Failed" : "Accepted");
    }
}

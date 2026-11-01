namespace LcsWidzew.Sim;

/// <summary>
/// S02A.03: prywatny stan proponowanego obliczenia. Kopia nie obejmuje świata, kart ani rejestratora;
/// odłączenie skutków zapewnia adapter obliczenia. Projekt układu i tablica przebiegów są współdzielone
/// wyłącznie do odczytu danych projektowych; leniwe cache topologii mają osobne kontenery i listy.
/// </summary>
public partial class KomputerZaleznosci
{
    /// <summary>
    /// Kopia ostatniego ZATWIERDZONEGO obliczenia – pula kontenerów dla następnego (wydajność; S02A.03).
    /// Jej obiekty nigdy nie trafiają do świata (przyjęcie kopiuje wartości) i po decyzji nikt ich nie trzyma.
    /// Odrzucone obliczenie porzuca pulę, więc cache topologii z odrzuconej propozycji nie przechodzi dalej.
    /// </summary>
    KomputerZaleznosci pula;

    internal KomputerZaleznosci KopiaDoObliczen()
    {
        var p = pula; pula = null;
        var k = (KomputerZaleznosci)MemberwiseClone();
        k.pula = null;
        // Wartości kopiowane do istniejących obiektów puli (bez setek alokacji na obliczenie);
        // kolejność kluczy zawsze jak w świecie – od niej zależy kolejność skutków.
        k.Secs = Synchronizuj(Secs, p?.Secs, Kopiuj);
        k.Sws = Synchronizuj(Sws, p?.Sws, Kopiuj);
        k.Sigs = Synchronizuj(Sigs, p?.Sigs, Kopiuj);
        k.Ders = Synchronizuj(Ders, p?.Ders, Kopiuj);
        k.Blocks = Synchronizuj(Blocks, p?.Blocks, Kopiuj);
        // Routes zmienia skład podczas obliczenia. Ten sam ciąg aktywnych kluczy w Dictionary nie oznacza tej samej
        // kolejności po następnym Add: kontener puli może mieć inne wolne sloty po dawnych Remove.
        // Świeża kopia przywraca bazową kolejność złożonych przebiegów i ich równoczesnych zdarzeń.
        k.Routes = KopiaSlownika(Routes, Kopiuj);
        k.Power = p?.Power ?? new PowerState(); Kopiuj(Power, k.Power);
        k.SssPosts = p?.SssPosts ?? new HashSet<string>(SssPosts.Comparer); Zbior(SssPosts, k.SssPosts);
        k.ZssPosts = p?.ZssPosts ?? new HashSet<string>(ZssPosts.Comparer); Zbior(ZssPosts, k.ZssPosts);
        if (p != null)
        {
            // Po zatwierdzeniu klucze cache puli i świata są równe (przyjęcie dopisało nowe wpisy), a wartości są czystą
            // funkcją projektu – osobne kontenery i listy zostają, bez kopiowania całości przy każdym obliczeniu.
            k.odstepCache = p.odstepCache; k.nextSigCache = p.nextSigCache;
            k.pierwszyOdstepCache = p.pierwszyOdstepCache;
        }
        else
        {
            k.odstepCache = odstepCache.ToDictionary(e => e.Key, e => new List<string>(e.Value), odstepCache.Comparer);
            k.nextSigCache = new Dictionary<string, string>(nextSigCache, nextSigCache.Comparer);
            k.pierwszyOdstepCache = pierwszyOdstepCache.ToDictionary(e => e.Key, e => new List<string>(e.Value), pierwszyOdstepCache.Comparer);
        }
        k.znaneStatusy = znaneStatusy; // wspólny odczyt; zapisy obliczenia trafiają tylko do delty
        k.noweStatusy = p?.noweStatusy ?? new Dictionary<string, string>(znaneStatusy.Comparer); k.noweStatusy.Clear();
        return k;
    }

    /// <summary>Po decyzji: zatwierdzona kopia staje się pulą, odrzucona jest porzucana.</summary>
    internal void ZwolnijKopie(KomputerZaleznosci obliczony, bool zatwierdzona) => pula = zatwierdzona ? obliczony : null;

    /// <summary>
    /// Przyjęcie zatwierdzonego stanu bez podmieniania istniejących obiektów odcinków, zwrotnic,
    /// sygnalizatorów, wykolejnic, blokad i przebiegów. Zachowane identyfikatory przebiegów zachowują
    /// również obiekty; nowe przebiegi są kopiowane, więc późniejsza zmiana kopii nie zmieni świata.
    /// Nie wykonuje kart, zapisów do rejestratora ani callbacków.
    /// </summary>
    internal void PrzyjmijStan(KomputerZaleznosci obliczony)
    {
        ArgumentNullException.ThrowIfNull(obliczony);
        if (!ReferenceEquals(S, obliczony.S)) throw new InvalidOperationException("Stan KZ pochodzi z innego świata.");
        if (ReferenceEquals(this, obliczony)) return;
        PrzyjmijSlownik(obliczony.Secs, Secs, Kopiuj);
        PrzyjmijSlownik(obliczony.Sws, Sws, Kopiuj);
        PrzyjmijSlownik(obliczony.Sigs, Sigs, Kopiuj);
        PrzyjmijSlownik(obliczony.Ders, Ders, Kopiuj);
        PrzyjmijSlownik(obliczony.Blocks, Blocks, Kopiuj);
        PrzyjmijSlownik(obliczony.Routes, Routes, Kopiuj);
        Kopiuj(obliczony.Power, Power);
        Zbior(obliczony.SssPosts, SssPosts);
        Zbior(obliczony.ZssPosts, ZssPosts);
        seq = obliczony.seq;
        // Prywatny słownik KZ: nałożenie delty obliczenia (zmienione statusy kart), bez kopii całego słownika.
        if (!ReferenceEquals(obliczony.znaneStatusy, znaneStatusy)) throw new InvalidOperationException("Kopia KZ nie pochodzi z tego stanu.");
        foreach (var (klucz, tekst) in obliczony.noweStatusy) znaneStatusy[klucz] = tekst;
        // Cache topologii rosną tylko o nowe wpisy (czysta funkcja projektu); świat dostaje własne listy.
        DopiszNowe(obliczony.odstepCache, odstepCache, l => new List<string>(l));
        DopiszNowe(obliczony.nextSigCache, nextSigCache, s => s);
        DopiszNowe(obliczony.pierwszyOdstepCache, pierwszyOdstepCache, l => new List<string>(l));
    }

    static void DopiszNowe<K, V>(Dictionary<K, V> z, Dictionary<K, V> doSlownika, Func<V, V> kopia)
    {
        if (z.Count == doSlownika.Count) return; // po przyjęciu poprzedniej kopii klucze są równe; nowe wpisy zmieniają liczbę
        foreach (var (k, v) in z) if (!doSlownika.ContainsKey(k)) doSlownika.Add(k, kopia(v));
    }

    /// <summary>
    /// Kopia słownika stanu do kontenera puli: przy tych samych kluczach w tej samej kolejności – same wartości;
    /// inaczej nowy słownik w kolejności świata z ponownym użyciem obiektów puli (Kopiuj nadpisuje każde pole).
    /// </summary>
    static Dictionary<string, V> Synchronizuj<V>(Dictionary<string, V> z, Dictionary<string, V> pula, Action<V, V> kopiuj) where V : new()
    {
        if (pula == null || !ReferenceEquals(pula.Comparer, z.Comparer)) return KopiaSlownika(z, kopiuj);
        if (pula.Count == z.Count)
        {
            bool zgodne = true;
            using (var ep = pula.GetEnumerator())
                foreach (var (id, stan) in z)
                {
                    if (!ep.MoveNext() || !z.Comparer.Equals(id, ep.Current.Key)) { zgodne = false; break; }
                    kopiuj(stan, ep.Current.Value);
                }
            if (zgodne) return pula;
        }
        var d = new Dictionary<string, V>(z.Count, z.Comparer);
        foreach (var (id, stan) in z) { if (!pula.TryGetValue(id, out var cel)) cel = new V(); kopiuj(stan, cel); d.Add(id, cel); }
        return d;
    }

    static Dictionary<string, V> KopiaSlownika<V>(Dictionary<string, V> z, Action<V, V> kopiuj) where V : new()
    {
        var doSlownika = new Dictionary<string, V>(z.Count, z.Comparer);
        foreach (var (id, stan) in z) { var k = new V(); kopiuj(stan, k); doSlownika.Add(id, k); }
        return doSlownika;
    }

    static void PrzyjmijSlownik<V>(Dictionary<string, V> z, Dictionary<string, V> doSlownika, Action<V, V> kopiuj) where V : new()
    {
        // Zwykle te same klucze w tej samej kolejności (kopia powstała ze świata): same wartości, bez wyszukiwań.
        if (z.Count == doSlownika.Count && ReferenceEquals(z.Comparer, doSlownika.Comparer))
        {
            bool zgodne = true;
            using (var ed = doSlownika.GetEnumerator())
                foreach (var (id, stan) in z)
                {
                    if (!ed.MoveNext() || !z.Comparer.Equals(id, ed.Current.Key)) { zgodne = false; break; }
                    kopiuj(stan, ed.Current.Value);
                }
            if (zgodne) return;
        }
        // Inny zbiór albo kolejność kluczy (np. nowy lub zwolniony przebieg) – dotychczasowa droga, ten sam wynik.
        foreach (var id in doSlownika.Keys.Where(id => !z.ContainsKey(id)).ToArray()) doSlownika.Remove(id);
        foreach (var (id, stan) in z)
        {
            if (!doSlownika.TryGetValue(id, out var cel)) { cel = new V(); doSlownika.Add(id, cel); }
            kopiuj(stan, cel);
        }
    }

    static void Lista<V>(List<V> z, List<V> doListy) { if (z.Count == 0 && doListy.Count == 0) return; doListy.Clear(); doListy.AddRange(z); }
    static void Zbior<V>(HashSet<V> z, HashSet<V> doZbioru)
    {
        if (ReferenceEquals(z, doZbioru)) return;
        // Równe elementy i ich bieżąca kolejność nie dowodzą równego układu wolnych slotów po Remove.
        // Clear resetuje tę historię także dla pustego zbioru; tablica kontenera może zostać ponownie użyta.
        doZbioru.Clear(); doZbioru.UnionWith(z);
    }
    static void Tablica(bool[] z, ref bool[] doTablicy)
    {
        if (doTablicy.Length != z.Length) doTablicy = new bool[z.Length];
        z.CopyTo(doTablicy, 0);
    }

    static void Kopiuj(SecState z, SecState d)
    {
        d.Id = z.Id; d.TrainOcc = z.TrainOcc; d.Fault = z.Fault; d.Closed = z.Closed; d.NoData = z.NoData;
        d.Lock = z.Lock; d.LockType = z.LockType; d.TimeRel = z.TimeRel;
        Zbior(z.Shared, d.Shared);
    }

    static void Kopiuj(SwState z, SwState d)
    {
        d.Num = z.Num; d.Pos = z.Pos; d.Lock = z.Lock;
        d.Stopped = z.Stopped; d.Rozpr = z.Rozpr; d.NoCtrl = z.NoCtrl; d.Frozen = z.Frozen;
        d.NoData = z.NoData; d.Local = z.Local; d.KsrDone = z.KsrDone;
        Zbior(z.Flank, d.Flank); Zbior(z.Ov, d.Ov);
        if (z.Moving == null) d.Moving = null;
        else
        {
            d.Moving ??= new MoveInfo();
            d.Moving.Target = z.Moving.Target; d.Moving.From = z.Moving.From;
            d.Moving.Until = z.Moving.Until; d.Moving.Started = z.Moving.Started; d.Moving.Alarmed = z.Moving.Alarmed;
        }
    }

    static void Kopiuj(SigState z, SigState d)
    {
        d.Name = z.Name; d.Route = z.Route; d.RouteEnd = z.RouteEnd; d.Aspect = z.Aspect;
        d.Closed = z.Closed; d.LampFault = z.LampFault; d.NoData = z.NoData; d.FailAlarm = z.FailAlarm; d.Dark = z.Dark;
        d.Sig = z.Sig; d.W21 = z.W21; d.W24 = z.W24; d.Wyswietlany = z.Wyswietlany; d.SzUntil = z.SzUntil;
        Zbior(z.Flank, d.Flank);
    }

    static void Kopiuj(DerState z, DerState d)
    {
        d.Name = z.Name; d.Lock = z.Lock; d.On = z.On; d.MovingTo = z.MovingTo; d.MoveUntil = z.MoveUntil;
        Zbior(z.Flank, d.Flank);
    }

    static void Kopiuj(BlockState z, BlockState d)
    {
        d.Id = z.Id; d.Dir = z.Dir; d.ReleaseUntil = z.ReleaseUntil; d.AzkUntil = z.AzkUntil; d.AzkDir = z.AzkDir;
        d.NbrSilent = z.NbrSilent; d.NbrStop = z.NbrStop;
        d.AwaitEnd = z.AwaitEnd; d.FreeSince = z.FreeSince; d.EndAlarmed = z.EndAlarmed;
        d.DPoCount = z.DPoCount; d.DKoCount = z.DKoCount;
        Tablica(z.StopEnd, ref d.StopEnd); Tablica(z.Pwl, ref d.Pwl); Tablica(z.PoReady, ref d.PoReady);
        Tablica(z.EntryOk, ref d.EntryOk); Tablica(z.DKo, ref d.DKo);
        if (z.Req == null) d.Req = null;
        else
        {
            d.Req ??= new BlockReq();
            d.Req.Numer = z.Req.Numer; d.Req.EndIdx = z.Req.EndIdx; d.Req.At = z.Req.At; d.Req.Alarmed = z.Req.Alarmed; d.Req.ForTrain = z.Req.ForTrain;
        }
    }

    static void Kopiuj(PowerState z, PowerState d)
    {
        d.Mains = z.Mains; d.Ups = z.Ups; d.Gen = z.Gen; d.NN = z.NN;
    }

    static void Kopiuj(Route z, Route d)
    {
        d.Id = z.Id; d.Start = z.Start; d.End = z.End; d.EndKind = z.EndKind; d.Type = z.Type;
        d.EndNode = z.EndNode; d.Overlap = z.Overlap; d.Minus = z.Minus; d.Variant = z.Variant;
        d.NoOverlap = z.NoOverlap; d.Off = z.Off; d.VCalc = z.VCalc; d.VSet = z.VSet;
        Lista(z.Edges, d.Edges); Lista(z.Secs, d.Secs); Lista(z.Ders, d.Ders); Lista(z.Sw, d.Sw);
        Lista(z.Flank, d.Flank); Lista(z.Excl, d.Excl); Lista(z.Allow, d.Allow); Lista(z.Conds, d.Conds);
        // Warianty drogi ochronnej są niezmiennymi danymi projektowymi: osobny kontener, wspólne elementy.
        Lista(z.OverlapAlts, d.OverlapAlts);
        d.Uid = z.Uid; d.State = z.State; d.Rel = z.Rel; d.Passed = z.Passed; d.SlowAlarm = z.SlowAlarm;
        d.ManualStop = z.ManualStop; d.T0 = z.T0; d.TimeRelUntil = z.TimeRelUntil; d.DoneAt = z.DoneAt;
        Lista(z.OvSecs, d.OvSecs); Lista(z.OvSw, d.OvSw); Zbior(z.WasOcc, d.WasOcc);
    }
}

using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace LcsWidzew.Sim;

/// <summary>
/// Tabela rozsyłania statusów (S02A.02): dla każdego obiektu mapy własności – lista kzId, do których trafia jego status.
/// Reguła v1, wyłącznie z mapy (bez nowych decyzji za człowieka): obiekt przypisany – właściciel i zadeklarowani odbiorcy mapy
/// (<see cref="MapaWlasnosci.Obiekt.Odbiorcy"/>, KZ korzystający z obiektu); niejednoznaczny – odbiorcy i wszyscy kandydaci;
/// nieznany – odbiorcy mapy, a gdy ich brak – wszystkie KZ. Status nie ginie: wejście jest ostrożne (więcej odbiorców), odwrotnie
/// niż polecenie, które dla nieprzypisanego obiektu musi być odrzucone. Niemutowalna i deterministyczna (kolejność ordinalna).
/// </summary>
public sealed class TrasaWejsc
{
    public const int WersjaSchematu = 1;
    public const string Kontrakt = "kz.trasa_wejsc";

    public static class Powod
    {
        public const string Wlasciciel = "WLASCICIEL", Kandydaci = "NIEJEDNOZNACZNY_KANDYDACI", OdbiorcyMapy = "NIEZNANY_ODBIORCY_MAPY",
            WszystkieKz = "NIEZNANY_WSZYSTKIE_KZ";
    }

    /// <summary>Wpis obiektu: <paramref name="Pozycja"/> – miejsce w porządku kanonicznym (rodzaj, id; jak w mapie własności).</summary>
    public sealed record Wpis(string Rodzaj, string Id, string Powod, ImmutableArray<string> Odbiorcy, int Pozycja)
    {
        /// <summary>Indeksy odbiorców w <see cref="KzIds"/> (rosnąco).</summary>
        internal ImmutableArray<int> IndeksyKz { get; init; } = ImmutableArray<int>.Empty;
    }

    /// <summary>Wszystkie kzId mapy (porządek ordinalny); co najmniej jeden – bez KZ tabela nie powstaje.</summary>
    public ImmutableArray<string> KzIds { get; }
    public ImmutableArray<Wpis> Wpisy { get; }
    /// <summary>SHA-256 mapy własności, z której zbudowano tabelę.</summary>
    public string SkrotMapy { get; }
    /// <summary>Kanoniczny JSON v1 tabeli (UTF-8, wcięcia 2, LF, minimalne ucieczki – <see cref="KanonJson"/>).</summary>
    public string Json { get; }
    public string Sha256 { get; }
    readonly Dictionary<string, Dictionary<string, Wpis>> wpisy = new(StringComparer.Ordinal);
    internal readonly ImmutableArray<int> WszystkieIndeksy;

    TrasaWejsc(MapaWlasnosci m)
    {
        KzIds = m.Posterunki.Select(p => p.KzId).OrderBy(k => k, StringComparer.Ordinal).ToImmutableArray();
        if (KzIds.IsEmpty) throw new InvalidOperationException("Mapa własności bez posterunków z poprawnym kzId.");
        var znane = KzIds.ToHashSet(StringComparer.Ordinal);
        var lista = new List<Wpis>(m.Obiekty.Count);
        foreach (var o in m.Obiekty)          // mapa jest już w porządku (rodzaj, id)
        {
            var kz = new SortedSet<string>(o.Odbiorcy.Where(znane.Contains), StringComparer.Ordinal);
            string powod;
            if (o.Status == MapaWlasnosci.Status.Przypisany) { if (o.KzId != null) kz.Add(o.KzId); powod = Powod.Wlasciciel; }
            else if (o.Status == MapaWlasnosci.Status.Niejednoznaczny)
            {
                foreach (var p in o.Kandydaci) if (m.KzPosterunku(p) is string k) kz.Add(k);
                powod = Powod.Kandydaci;
            }
            else powod = Powod.OdbiorcyMapy;
            if (kz.Count == 0) { kz.UnionWith(KzIds); powod = Powod.WszystkieKz; }
            var odb = kz.ToImmutableArray();
            lista.Add(new Wpis(o.Rodzaj, o.Id, powod, odb, lista.Count) { IndeksyKz = odb.Select(k => KzIds.IndexOf(k)).ToImmutableArray() });
        }
        Wpisy = lista.ToImmutableArray();
        foreach (var w in lista)
        {
            if (!wpisy.TryGetValue(w.Rodzaj, out var d)) wpisy[w.Rodzaj] = d = new Dictionary<string, Wpis>(StringComparer.Ordinal);
            d[w.Id] = w;
        }
        WszystkieIndeksy = Enumerable.Range(0, KzIds.Length).ToImmutableArray();
        SkrotMapy = m.Sha256;
        Json = DoJson();
        Sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Json)));
    }

    public static TrasaWejsc Zbuduj(MapaWlasnosci mapa) { ArgumentNullException.ThrowIfNull(mapa); return new TrasaWejsc(mapa); }

    /// <summary>Odbiorcy statusu obiektu; null – obiektu nie ma w mapie (rejestr kieruje wtedy status do wszystkich KZ i loguje naruszenie).</summary>
    public Wpis WpisObiektu(string rodzaj, string id) =>
        rodzaj != null && id != null && wpisy.TryGetValue(rodzaj, out var d) && d.TryGetValue(id, out var w) ? w : null;

    /// <summary>JSON z wcięciem 2 jak <c>json.dumps(indent=2, ensure_ascii=False)</c> (separatory „, ” i „: ”), z LF na końcu.</summary>
    string DoJson()
    {
        var sb = new StringBuilder(64 * 1024);
        void Lista(string pad, IReadOnlyList<string> v)
        {
            if (v.Count == 0) { sb.Append("[]"); return; }
            sb.Append("[\n");
            for (int i = 0; i < v.Count; i++) { sb.Append(pad).Append("  "); KanonJson.Tekst(sb, v[i]); sb.Append(i + 1 < v.Count ? ",\n" : "\n"); }
            sb.Append(pad).Append(']');
        }
        sb.Append("{\n  \"schemaVersion\": ").Append(WersjaSchematu).Append(",\n  \"kontrakt\": "); KanonJson.Tekst(sb, Kontrakt);
        sb.Append(",\n  \"skrotMapy\": "); KanonJson.Tekst(sb, SkrotMapy);
        sb.Append(",\n  \"kz\": "); Lista("  ", KzIds);
        sb.Append(",\n  \"wpisy\": ");
        if (Wpisy.IsEmpty) sb.Append("[]");
        else
        {
            sb.Append("[\n");
            for (int i = 0; i < Wpisy.Length; i++)
            {
                var e = Wpisy[i];
                sb.Append("    {\n      \"rodzaj\": "); KanonJson.Tekst(sb, e.Rodzaj);
                sb.Append(",\n      \"id\": "); KanonJson.Tekst(sb, e.Id);
                sb.Append(",\n      \"powod\": "); KanonJson.Tekst(sb, e.Powod);
                sb.Append(",\n      \"odbiorcy\": "); Lista("      ", e.Odbiorcy);
                sb.Append("\n    }").Append(i + 1 < Wpisy.Length ? ",\n" : "\n");
            }
            sb.Append("  ]");
        }
        sb.Append("\n}\n");
        return sb.ToString();
    }
}

/// <summary>
/// Rejestr wejść cyklu (S02A.02, 18 „Rejestr wejść”, 19 §7 pkt 2): jedyna droga statusów kart do komputerów zależności.
/// Karty zgłaszają niezmienne <see cref="StatusUrzadzenia"/> (<see cref="Zglos"/>) – nie wywołują KZ i nie zapisują jego słowników.
/// Na jawnej granicy fazy (<see cref="Faza"/>) rejestr zamraża zgłoszenia, numeruje je i kieruje według mapy własności do
/// <see cref="WejscieCyklu"/> każdego KZ; wszyscy odbiorcy danego KZ dostają ten sam obiekt. Fazy regularne (KARTY, RUCH)
/// zamrażają wszystkie zgłoszenia fazy naraz; w fazach natychmiastowych (ROZKAZ, USTERKA, ZDARZENIE_KARTY) każde zgłoszenie jest
/// doręczane od razu (jak dawne bezpośrednie wywołanie KZ – alarm i wpis karty widzą już przyjęty status). Przejściowy jeden KZ C#
/// całego układu przyjmuje całą partię w kolejności zgłoszeń (bazowe zachowanie). Odbiorcy wejść nie mogą zgłaszać statusów ani
/// otwierać faz (brak drogi zwrotnej do świata). Rejestr należy do świata (jeden wątek cyklu), nie do KZ.
/// </summary>
public sealed class RejestrWejscCyklu
{
    /// <summary>Najgłębsze zagnieżdżone doręczenie z wnętrza KZ (zdarzenie KZ → usterka → status); głębiej – po bieżącej partii.</summary>
    public const int MaxGlebokosc = 8;
    /// <summary>Najwięcej partii w jednym doręczeniu – zabezpieczenie przed pętlą zdarzeń (przekroczenie przerywa cykl i zatrzymuje
    /// rejestr: fail-stop zamiast przepełnienia stosu; kolejne fazy i zgłoszenia są odrzucane).</summary>
    public const int MaxPartiiNaDoreczenie = 10_000;
    /// <summary>Co ile cykli (najrzadziej) okresowe podsumowanie 4116, gdy limit rekordów 4114/4115 został wyczerpany.</summary>
    public const long OkresPodsumowania = 3000;

    readonly Simulator S;
    readonly ILogger log;
    readonly IOdbiorcaPartiiWejsc obszar;

    MapaWlasnosci mapaTrasy;
    TrasaWejsc trasa;
    bool trasaNiedostepna, trasaWymagana;

    sealed class OtwartaFaza(RejestrWejscCyklu r, FazaWejscia faza) : IDisposable
    {
        public readonly FazaWejscia Faza = faza;
        public readonly List<StatusUrzadzenia> Statusy = new();
        public bool Zamknieta;
        public void Dispose() => r.Zamknij(this);
    }

    sealed class Subskrypcja(RejestrWejscCyklu r, string kzId, IOdbiorcaWejscCyklu odbiorca) : IDisposable
    {
        public readonly string KzId = kzId;
        public readonly IOdbiorcaWejscCyklu Odbiorca = odbiorca;
        public bool Aktywna = true;
        public void Dispose() { Aktywna = false; r.Usun(this); }
    }

    readonly List<OtwartaFaza> stos = new();
    readonly Queue<PartiaWejsc> kolejkaObszaru = new(), kolejkaOdbiorcow = new();
    readonly Dictionary<string, long> nrWejscia = new(StringComparer.Ordinal);
    readonly Dictionary<string, List<Subskrypcja>> odbiorcy = new(StringComparer.Ordinal);
    int glebokosc, partieWDoreczeniu;
    bool wOdbiorcy, doreczanieOdbiorcom, zatrzymany;
    long nrStatusu, nrPartii, wejsc, naruszenia, bledyOdbiorcow, fazyZagniezdzone, pozaFaza, nieznaneObiekty, reentrancje, odroczone;
    int bledyWLogu;
    long zamkniecieKolejnosc, naruszeniaPominiete, bledyPominiete, zgloszoneNaruszeniaPominiete, zgloszoneBledyPominiete;
    long ostatniePodsumowanie = long.MinValue / 2;
    readonly Dictionary<string, int> wLoguKodu = new(StringComparer.Ordinal);

    internal RejestrWejscCyklu(Simulator s, IOdbiorcaPartiiWejsc obszar, ILogger log)
    {
        S = s; this.obszar = obszar; this.log = log;
    }

    /// <summary>
    /// Tabela rozsyłania (budowana z mapy własności przy pierwszym odczycie: host świata przy starcie, subskrypcja, numeracja); null –
    /// brak mapy, posterunków albo błąd budowy (log 4113, fail-closed). Od pierwszego odczytu rejestr numeruje wejścia KZ – w hostach
    /// od startu świata. Świat bez odbiorców i bez hosta (np. selftest) nie buduje mapy ani tabeli i nie płaci za nie.
    /// </summary>
    public TrasaWejsc Trasa
    {
        get
        {
            trasaWymagana = true;
            if (trasaNiedostepna) return null;
            MapaWlasnosci m;
            try { m = S.Wlasnosc; }
            catch (Exception) { trasaNiedostepna = true; WejscieCykluLog.TrasaNiedostepna(log, WejscieCykluLog.PowodBrakuMapy); return null; }
            if (m == null || !ReferenceEquals(m, mapaTrasy))
            {
                if (m == null || m.Posterunki.Count == 0) { trasaNiedostepna = true; WejscieCykluLog.TrasaNiedostepna(log, WejscieCykluLog.PowodBrakuPosterunkow); return null; }
                TrasaWejsc t;
                try { t = TrasaWejsc.Zbuduj(m); }
                catch (Exception) { trasaNiedostepna = true; WejscieCykluLog.TrasaNiedostepna(log, WejscieCykluLog.PowodBleduBudowy); return null; }
                trasa = t; mapaTrasy = m;
                WejscieCykluLog.Trasa(log, trasa);
                SprawdzPokrycie(trasa);
            }
            return trasa;
        }
    }

    /// <summary>Każda karta urządzeń musi mieć wpis w tabeli – inaczej jej status trafia do wszystkich KZ (naruszenie UNKNOWN_OBJECT).</summary>
    void SprawdzPokrycie(TrasaWejsc t)
    {
        var u = S.Urzadzenia;
        if (u == null) return;
        void Sprawdz(string rodzaj, IEnumerable<string> ids)
        {
            foreach (var id in ids.OrderBy(x => x, StringComparer.Ordinal))
                if (t.WpisObiektu(rodzaj, id) == null) { nieznaneObiekty++; Naruszenie("UNKNOWN_OBJECT", rodzaj, id); }
        }
        Sprawdz(MapaWlasnosci.Rodzaj.Odcinek, u.Odcinki.Keys);
        Sprawdz(MapaWlasnosci.Rodzaj.Zwrotnica, u.Zwrotnice.Keys);
        Sprawdz(MapaWlasnosci.Rodzaj.Wykolejnica, u.Wykolejnice.Keys);
        Sprawdz(MapaWlasnosci.Rodzaj.Sygnalizator, u.Sygnalizatory.Keys);
        Sprawdz(MapaWlasnosci.Rodzaj.Zasilanie, new[] { Zasilanie.Id });
    }

    /// <summary>Nowa mapa po przeliczeniu tablicy przebiegów – tabela zostanie zbudowana ponownie (także po wcześniejszym błędzie).</summary>
    internal void UniewaznijTrase() { trasaNiedostepna = false; mapaTrasy = null; trasa = null; }

    /// <summary>Fazy regularne zamrażają zgłoszenia na końcu fazy i dają wejście każdemu KZ; pozostałe są natychmiastowe.</summary>
    static bool Regularna(FazaWejscia f) => f is FazaWejscia.Karty or FazaWejscia.Ruch;

    /// <summary>
    /// Otwiera jawną fazę statusów (wyłącznie rdzeń: urządzenia, instruktor); zamknięcie (Dispose) zamraża zgłoszenia i doręcza
    /// wejścia. Faza otwarta wewnątrz innej najpierw doręcza dotychczasowe zgłoszenia zewnętrznej (bazowa kolejność skutków);
    /// faza z wnętrza zdarzenia KZ w trakcie doręczania jest doręczana zagnieżdżenie (jak dawniej). Oba przypadki – log 4114.
    /// Odbiorca wejść nie może otworzyć fazy (wyjątek – odbiorca zostaje wyłączony).
    /// </summary>
    internal IDisposable Faza(FazaWejscia faza)
    {
        if (wOdbiorcy) throw new InvalidOperationException("Odbiorca wejść KZ nie może otwierać faz statusów.");
        if (zatrzymany) throw Zatrzymany();
        if (stos.Count > 0)
        {
            fazyZagniezdzone++;
            Naruszenie("NESTED_PHASE", null, null);
            Zamroz(stos[^1]);
            Dorecz();
        }
        else if (glebokosc > 0) { reentrancje++; Naruszenie("REENTRANT_PHASE", null, null); }
        return Otworz(faza);
    }

    OtwartaFaza Otworz(FazaWejscia faza)
    {
        var f = new OtwartaFaza(this, faza);
        stos.Add(f);
        return f;
    }

    /// <summary>
    /// Wykonuje <paramref name="dzialanie"/> w jawnej fazie i ją zamyka. Wyjątek z wnętrza fazy nie jest maskowany: zgłoszenia są
    /// doręczane, a gdy doręczenie też zawiedzie, wychodzi <see cref="AggregateException"/> z oboma błędami.
    /// </summary>
    internal T WFazie<T>(FazaWejscia faza, Func<T> dzialanie)
    {
        var f = Faza(faza);
        T wynik;
        try { wynik = dzialanie(); }
        catch (Exception pierwotny) { ZamknijPoBledzie(f, pierwotny); throw; }
        f.Dispose();
        return wynik;
    }

    /// <summary>Zamknięcie fazy po wyjątku jej wnętrza: błąd doręczenia nie zastępuje błędu pierwotnego (oba w <see cref="AggregateException"/>).</summary>
    internal static void ZamknijPoBledzie(IDisposable faza, Exception pierwotny)
    {
        try { faza.Dispose(); }
        catch (Exception wtorny) { throw new AggregateException("Błąd w fazie statusów i przy jej zamknięciu.", pierwotny, wtorny); }
    }

    /// <summary>
    /// Wejścia świata (cykl, polecenia, usterki, przeliczenie tablicy) są niedostępne z wnętrza odbiorcy wejść KZ – odbiorca nie ma
    /// drogi zwrotnej do świata (wyjątek, odbiorca zostaje wyłączony).
    /// </summary>
    internal void PozaOdbiorca()
    {
        if (wOdbiorcy) throw new InvalidOperationException("Odbiorca wejść KZ nie może zmieniać świata (cykl, polecenia, usterki).");
    }

    /// <summary>
    /// Zgłoszenie statusu karty (wyłącznie rdzeń). W fazie regularnej status czeka na granicę fazy, w natychmiastowej – jest doręczany
    /// od razu. Bez otwartej fazy status jest doręczany od razu jako <see cref="FazaWejscia.PozaFaza"/> (nie ginie) z logiem naruszenia.
    /// Status obiektu bez karty urządzeń jest odrzucany od razu (błąd integracji w miejscu zgłoszenia, a nie przy doręczeniu).
    /// Odbiorca wejść nie może zgłaszać statusów (wyjątek – odbiorca zostaje wyłączony).
    /// </summary>
    internal void Zglos(StatusUrzadzenia status)
    {
        ArgumentNullException.ThrowIfNull(status);
        if (string.IsNullOrEmpty(status.Obiekt)) throw new ArgumentException("Status bez identyfikatora obiektu.", nameof(status));
        if (wOdbiorcy) throw new InvalidOperationException("Odbiorca wejść KZ nie może zgłaszać statusów.");
        if (zatrzymany) throw Zatrzymany();
        if (!MaKarte(status)) throw new ArgumentException("Status obiektu bez karty urządzeń.", nameof(status));
        nrStatusu++;
        if (stos.Count > 0)
        {
            var top = stos[^1];
            top.Statusy.Add(status);
            if (!Regularna(top.Faza)) { Zamroz(top); Dorecz(); }
            return;
        }
        pozaFaza++;
        Naruszenie(glebokosc > 0 ? "REENTRANT_STATUS" : "STATUS_OUTSIDE_PHASE", status.Rodzaj, status.Obiekt);
        using var f = Otworz(FazaWejscia.PozaFaza);     // jedno naruszenie na zgłoszenie (bez drugiego REENTRANT_PHASE)
        f.Statusy.Add(status);
    }

    /// <summary>
    /// Rejestracja odbiorcy wejść jednego KZ (od następnego wejścia). Nieznany kzId – wyjątek; brak mapy własności – wyjątek
    /// (wejść KZ nie rozsyłam bez mapy). Dispose wyrejestrowuje.
    /// </summary>
    public IDisposable Subskrybuj(string kzId, IOdbiorcaWejscCyklu odbiorca)
    {
        ArgumentNullException.ThrowIfNull(odbiorca);
        var t = Trasa ?? throw new InvalidOperationException("Brak mapy własności – wejścia KZ nie są rozsyłane.");
        if (kzId == null || !t.KzIds.Contains(kzId)) throw new ArgumentException("Nieznany kzId odbiorcy.", nameof(kzId));
        var sub = new Subskrypcja(this, kzId, odbiorca);
        if (!odbiorcy.TryGetValue(kzId, out var l)) odbiorcy[kzId] = l = new();
        l.Add(sub);
        return sub;
    }

    // ostatnio sprawdzone rekordy (karty zgłaszają ponownie ten sam niezmienny rekord, gdy stan się nie zmienił) – walidacja raz na rekord
    readonly StatusUrzadzenia[] sprawdzone = new StatusUrzadzenia[1024];

    /// <summary>Status dotyczy istniejącej karty (rodzaj zgodny z typem rekordu) – jedyne źródła statusów to karty urządzeń.</summary>
    bool MaKarte(StatusUrzadzenia s)
    {
        int h = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(s) & (sprawdzone.Length - 1);
        if (ReferenceEquals(sprawdzone[h], s)) return true;
        var u = S.Urzadzenia;
        bool ok = u != null && s switch
        {
            StatusOdcinka => u.Odcinki.ContainsKey(s.Obiekt),
            StatusZwrotnicy => u.Zwrotnice.ContainsKey(s.Obiekt),
            StatusWykolejnicy => u.Wykolejnice.ContainsKey(s.Obiekt),
            StatusSygnalizatora => u.Sygnalizatory.ContainsKey(s.Obiekt),
            StatusZasilania => s.Obiekt == Zasilanie.Id,
            _ => false,
        };
        if (ok) sprawdzone[h] = s;
        return ok;
    }

    static InvalidOperationException Zatrzymany() => new("Rejestr wejść KZ zatrzymany po pętli doręczeń – świat wymaga ponownego uruchomienia.");

    void Usun(Subskrypcja s) { if (odbiorcy.TryGetValue(s.KzId, out var l)) l.Remove(s); }
    bool MaOdbiorce(string kzId) => odbiorcy.TryGetValue(kzId, out var l) && l.Count > 0;

    /// <summary>Ostatni nadany numer wejścia KZ (0 – brak wejść albo tabela jeszcze nieodczytana).</summary>
    public long OstatniNrWejscia(string kzId) => kzId != null ? nrWejscia.GetValueOrDefault(kzId) : 0;

    /// <summary>Liczniki diagnostyczne rejestru (tylko odczyt, migawka).</summary>
    public IReadOnlyDictionary<string, long> Liczniki => new Dictionary<string, long>(StringComparer.Ordinal)
    {
        ["statuses"] = nrStatusu, ["batches"] = nrPartii, ["inputs"] = wejsc, ["violations"] = naruszenia, ["nestedPhases"] = fazyZagniezdzone,
        ["outsidePhase"] = pozaFaza, ["reentrantPhases"] = reentrancje, ["deferredDeliveries"] = odroczone, ["unknownObjects"] = nieznaneObiekty,
        ["receiverFailures"] = bledyOdbiorcow, ["routingUnavailable"] = trasaNiedostepna ? 1 : 0, ["stopped"] = zatrzymany ? 1 : 0,
        ["phaseCloseOrder"] = zamkniecieKolejnosc,
    };

    void Zamknij(OtwartaFaza f)
    {
        if (f.Zamknieta) return;
        int i = stos.LastIndexOf(f);
        if (i < 0) return;
        while (stos.Count - 1 > i) { zamkniecieKolejnosc++; Naruszenie("PHASE_CLOSE_ORDER", null, null); ZamknijGorna(); }
        ZamknijGorna();
    }

    void ZamknijGorna()
    {
        var f = stos[^1];
        stos.RemoveAt(stos.Count - 1);
        f.Zamknieta = true;
        Zamroz(f);
        Dorecz();
    }

    // bufory zamrażania (jeden wątek świata): indeksy statusów per KZ i klucze sortowania
    List<int>[] bufory = Array.Empty<List<int>>();
    long[] klucze = Array.Empty<long>();

    /// <summary>
    /// Zamrożenie zgłoszeń fazy: partia w kolejności zgłoszeń i wejścia KZ w porządku kanonicznym – pozycja obiektu w mapie
    /// (rodzaj, id), a dla kolejnych statusów jednego obiektu kolejność zgłoszenia. Obiekt spoza mapy (naruszenie) trafia do
    /// wszystkich KZ, na koniec wejścia w kolejności zgłoszenia. Numer wejścia KZ rośnie zawsze (faza regularna – każdy KZ,
    /// natychmiastowa – KZ ze statusem); obiekt <see cref="WejscieCyklu"/> powstaje tylko dla KZ, który ma aktywnego odbiorcę
    /// (bez odbiorców cykl nie płaci za sortowanie i kopie – dziś jedynym konsumentem jest KZ obszaru). Partia trafia do obu
    /// kolejek (KZ obszaru i odbiorców) w chwili zamrożenia – kolejność numerów u odbiorców nie zależy od zagnieżdżeń.
    /// </summary>
    void Zamroz(OtwartaFaza f)
    {
        var statusy = f.Statusy.ToImmutableArray();
        f.Statusy.Clear();
        bool regularna = Regularna(f.Faza);
        if (statusy.IsEmpty && !regularna) return;     // faza natychmiastowa bez zgłoszeń – brak wejścia
        long partia = ++nrPartii, cykl = S.NrCyklu, czas = (long)Math.Round(S.T * 1000, MidpointRounding.AwayFromZero);
        var wejscia = ImmutableArray<WejscieCyklu>.Empty;
        var t = trasaWymagana ? Trasa : null;          // bez odczytu tabeli (brak hosta i odbiorców) – tylko partia dla KZ obszaru
        if (t != null)
        {
            int nKz = t.KzIds.Length;
            bool zOdbiorcami = false;
            for (int k = 0; k < nKz && !zOdbiorcami; k++) zOdbiorcami = MaOdbiorce(t.KzIds[k]);
            if (regularna && !zOdbiorcami)
            {
                foreach (var kz in t.KzIds) { nrWejscia[kz] = nrWejscia.GetValueOrDefault(kz) + 1; wejsc++; }
            }
            else
            {
                if (bufory.Length != nKz) { bufory = new List<int>[nKz]; for (int k = 0; k < nKz; k++) bufory[k] = new List<int>(512); }
                foreach (var l in bufory) l.Clear();
                if (klucze.Length < statusy.Length) klucze = new long[Math.Max(statusy.Length, 2 * klucze.Length)];
                var pozycje = new int[statusy.Length];
                for (int i = 0; i < statusy.Length; i++)
                {
                    var s = statusy[i];
                    var wpis = t.WpisObiektu(s.Rodzaj, s.Obiekt);
                    if (wpis == null) { nieznaneObiekty++; Naruszenie("UNKNOWN_OBJECT", s.Rodzaj, s.Obiekt); }
                    pozycje[i] = wpis?.Pozycja ?? int.MaxValue;
                    foreach (int k in wpis != null ? wpis.IndeksyKz : t.WszystkieIndeksy) bufory[k].Add(i);
                }
                var b = ImmutableArray.CreateBuilder<WejscieCyklu>(nKz);
                for (int k = 0; k < nKz; k++)
                {
                    var l = bufory[k];
                    if (l.Count == 0 && !regularna) continue;
                    string kz = t.KzIds[k];
                    long nr = nrWejscia[kz] = nrWejscia.GetValueOrDefault(kz) + 1;
                    wejsc++;
                    if (!MaOdbiorce(kz)) continue;
                    var klucz = klucze.AsSpan(0, l.Count);
                    for (int j = 0; j < l.Count; j++) klucz[j] = ((long)pozycje[l[j]] << 32) | (uint)l[j];
                    klucz.Sort();
                    var st = ImmutableArray.CreateBuilder<StatusUrzadzenia>(l.Count);
                    foreach (long x in klucz) st.Add(statusy[(int)(x & 0xFFFFFFFF)]);
                    b.Add(new WejscieCyklu
                    {
                        KzId = kz, NrWejscia = nr, NrPartii = partia, Cykl = cykl, Faza = f.Faza, CzasSymulacjiMs = czas, SkrotTrasy = t.Sha256,
                        Statusy = st.MoveToImmutable(),
                    });
                }
                wejscia = b.ToImmutable();
            }
        }
        var p = new PartiaWejsc { NrPartii = partia, Cykl = cykl, Faza = f.Faza, CzasSymulacjiMs = czas, Statusy = statusy, Wejscia = wejscia };
        kolejkaObszaru.Enqueue(p);
        if (!wejscia.IsEmpty) kolejkaOdbiorcow.Enqueue(p);
        if (f.Faza == FazaWejscia.Karty) Podsumowanie();
    }

    /// <summary>
    /// Doręczenie: najpierw KZ obszaru (wyjątek przerywa cykl jak dotąd), potem – na najwyższym poziomie – odbiorcy wejść (izolowani,
    /// także po wyjątku KZ, więc bez luk w numeracji). Wywołanie z wnętrza KZ (zdarzenie → usterka → status) doręcza KZ zagnieżdżenie,
    /// jak dawne bezpośrednie wywołanie; powyżej <see cref="MaxGlebokosc"/> partia czeka na koniec bieżącej (log 4114).
    /// </summary>
    void Dorecz()
    {
        if (zatrzymany) return;          // po pętli doręczeń: nic więcej nie trafia do KZ (także przy zwijaniu faz wyjątkiem)
        if (glebokosc >= MaxGlebokosc) { odroczone++; Naruszenie("DELIVERY_DEPTH", null, null); return; }
        bool gora = glebokosc == 0;
        if (gora) partieWDoreczeniu = 0;
        glebokosc++;
        try
        {
            while (kolejkaObszaru.TryDequeue(out var p))
            {
                if (++partieWDoreczeniu > MaxPartiiNaDoreczenie)
                {
                    zatrzymany = true;
                    Naruszenie("DELIVERY_LIMIT", null, null);     // zawsze w logu (poza limitem) i od razu podsumowanie
                    Podsumowanie(wymus: true);
                    throw new InvalidOperationException("Pętla doręczeń wejść KZ (zdarzenia KZ wciąż tworzą nowe statusy) – rejestr zatrzymany.");
                }
                obszar?.Przyjmij(p);
            }
        }
        finally
        {
            glebokosc--;
            if (gora) DoreczOdbiorcom();
        }
    }

    void DoreczOdbiorcom()
    {
        if (doreczanieOdbiorcom) return;
        doreczanieOdbiorcom = true;
        try
        {
            while (kolejkaOdbiorcow.TryDequeue(out var p))
                foreach (var w in p.Wejscia)
                {
                    if (!odbiorcy.TryGetValue(w.KzId, out var l) || l.Count == 0) continue;
                    foreach (var sub in l.ToArray())
                    {
                        if (!sub.Aktywna) continue;
                        wOdbiorcy = true;
                        try { sub.Odbiorca.Odbierz(w); }
                        catch (Exception ex)
                        {
                            // odbiorca z luką w strumieniu nie może dalej liczyć – wyłączony, świat działa dalej
                            sub.Aktywna = false; Usun(sub); bledyOdbiorcow++;
                            if (bledyWLogu < WejscieCykluLog.MaxBledowOdbiorcowWLogu) { bledyWLogu++; WejscieCykluLog.BladOdbiorcy(log, w.KzId, w.NrWejscia, ex); }
                            else bledyPominiete++;
                        }
                        finally { wOdbiorcy = false; }
                    }
                }
        }
        finally { doreczanieOdbiorcom = false; }
    }

    /// <summary>Naruszenie: licznik zawsze; rekord 4114 najwyżej <see cref="WejscieCykluLog.MaxNaruszenWLogu"/> na kod (DELIVERY_LIMIT – zawsze).</summary>
    void Naruszenie(string kod, string rodzaj, string id)
    {
        naruszenia++;
        int n = wLoguKodu.GetValueOrDefault(kod);
        if (n >= WejscieCykluLog.MaxNaruszenWLogu && kod != "DELIVERY_LIMIT") { naruszeniaPominiete++; return; }
        wLoguKodu[kod] = n + 1;
        WejscieCykluLog.Naruszenie(log, kod, rodzaj, id, naruszenia);
    }

    /// <summary>
    /// Podsumowanie 4116, gdy od poprzedniego przybyły naruszenia lub błędy odbiorców bez własnego rekordu: okresowo na granicy fazy
    /// KARTY (pierwsze od razu, potem najwyżej co <see cref="OkresPodsumowania"/> cykli), przy zatrzymaniu rejestru i na końcu świata.
    /// </summary>
    void Podsumowanie(bool wymus = false)
    {
        if (naruszeniaPominiete == zgloszoneNaruszeniaPominiete && bledyPominiete == zgloszoneBledyPominiete) return;
        if (!wymus && S.NrCyklu - ostatniePodsumowanie < OkresPodsumowania) return;
        ostatniePodsumowanie = S.NrCyklu;
        zgloszoneNaruszeniaPominiete = naruszeniaPominiete; zgloszoneBledyPominiete = bledyPominiete;
        WejscieCykluLog.Podsumowanie(log, new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["violations"] = naruszenia, ["violationsSuppressed"] = naruszeniaPominiete,
            ["receiverFailures"] = bledyOdbiorcow, ["receiverFailuresSuppressed"] = bledyPominiete,
            ["outsidePhase"] = pozaFaza, ["nestedPhases"] = fazyZagniezdzone, ["reentrantPhases"] = reentrancje,
            ["phaseCloseOrder"] = zamkniecieKolejnosc, ["unknownObjects"] = nieznaneObiekty, ["deferredDeliveries"] = odroczone,
            ["stopped"] = zatrzymany ? 1 : 0,
        });
    }

    /// <summary>Koniec świata (host zamyka runtime): podsumowanie 4116, jeśli są naruszenia lub błędy odbiorców bez własnego rekordu.</summary>
    public void PodsumujNaKoniec() => Podsumowanie(wymus: true);
}

using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace LcsWidzew.Sim;

/// <summary>
/// Faza statusów (S02A.02): jawna granica cyklu, na której rejestr wejść (<see cref="RejestrWejscCyklu"/>) zamraża zgłoszone
/// statusy kart i rozsyła je jako numerowane <see cref="WejscieCyklu"/>. Kolejność faz w cyklu jest bazową kolejnością
/// <see cref="Simulator.Tick"/> (19 §6): karty → ruch. Fazy regularne (KARTY, RUCH) zamrażają zgłoszenia na końcu fazy; w fazach
/// natychmiastowych (rozkaz NN/ONN/AGR/ZeroLO, usterka instruktora, zdarzenie karty odcinka) każde zgłoszenie trafia do KZ od razu –
/// tak jak dawne bezpośrednie wywołanie: alarm i wpis karty widzą już przyjęty status, bez przesunięcia skutku do następnego cyklu.
/// </summary>
public enum FazaWejscia
{
    /// <summary>Cykl kart (Urzadzenia.Tick): zasilanie, zwrotnice, wykolejnice, sygnalizatory – przed KZ.</summary>
    Karty = 1,
    /// <summary>Teren po ruchu pociągów (Urzadzenia.Osie): karty odcinków, kontrola zliczenia, pierwszy przejazd.</summary>
    Ruch = 2,
    /// <summary>Natychmiastowy status po rozkazie KZ dla szafy zasilania (NN/ONN/AGR) albo karty odcinka (ZeroLO).</summary>
    Rozkaz = 3,
    /// <summary>Natychmiastowy status po usterce instruktora wprowadzonej w karcie.</summary>
    Usterka = 4,
    /// <summary>Natychmiastowy status karty odcinka po zdarzeniu w terenie wykrytym po ruchu (koniec przejazdu z błędem zliczenia,
    /// pierwszy przejazd po zerowaniu) – przed alarmem/wpisem karty, po zamrożeniu fazy RUCH.</summary>
    ZdarzenieKarty = 5,
    /// <summary>Status zgłoszony poza otwartą fazą (błąd integracji – log KZ.CYCLE_INPUT_PHASE_VIOLATION); dostarczany od razu.</summary>
    PozaFaza = 9,
}

/// <summary>
/// Niezmienny status jednego obiektu z karty sterownika (S02A.02, kontrakt <c>kontrakty/kz/wejscie_cyklu_v1.schema.json</c>).
/// Tylko wartości: bez delegatów i referencji do kart, terenu, pociągów ani stanu KZ. Kolejność zgłoszeń wynika z pozycji
/// w partii i w wejściu (kolejne statusy tego samego obiektu w kolejności zgłoszenia).
/// </summary>
public abstract record StatusUrzadzenia
{
    /// <summary>Rodzaj obiektu jak w mapie własności (<see cref="MapaWlasnosci.Rodzaj"/>).</summary>
    public abstract string Rodzaj { get; }
    /// <summary>Identyfikator obiektu z układu.</summary>
    public string Obiekt { get; init; }

    /// <summary>Pola rodzaju w postaci kanonicznej (<see cref="KanonJson"/>), każde poprzedzone przecinkiem.</summary>
    internal abstract void PiszPola(StringBuilder sb);
}

/// <summary>Status karty odcinka: osie pojazdu na odcinku i stan urządzenia kontroli niezajętości (null, „occ”, „reset”, „await”).</summary>
public sealed record StatusOdcinka : StatusUrzadzenia
{
    public override string Rodzaj => MapaWlasnosci.Rodzaj.Odcinek;
    public bool Pojazd { get; init; }
    public string Stan { get; init; }
    internal override void PiszPola(StringBuilder sb) { KanonJson.Logiczna(sb, "pojazd", Pojazd); KanonJson.Tekst(sb, "stan", Stan); }
}

/// <summary>Status karty zwrotnicy: położenie z kontrolą („+”/„-”, null – brak kontroli), przekładanie, przekroczony czas przekładania.</summary>
public sealed record StatusZwrotnicy : StatusUrzadzenia
{
    public override string Rodzaj => MapaWlasnosci.Rodzaj.Zwrotnica;
    public string Polozenie { get; init; }
    public bool Przekladanie { get; init; }
    public bool PrzekroczonyCzas { get; init; }
    internal override void PiszPola(StringBuilder sb) { KanonJson.Tekst(sb, "polozenie", Polozenie); KanonJson.Logiczna(sb, "przekladanie", Przekladanie); KanonJson.Logiczna(sb, "przekroczonyCzas", PrzekroczonyCzas); }
}

/// <summary>Status karty wykolejnicy: założona / zdjęta (null – przekładanie).</summary>
public sealed record StatusWykolejnicy : StatusUrzadzenia
{
    public override string Rodzaj => MapaWlasnosci.Rodzaj.Wykolejnica;
    public bool? Zalozona { get; init; }
    internal override void PiszPola(StringBuilder sb) => KanonJson.Logiczna(sb, "zalozona", Zalozona);
}

/// <summary>Status karty sygnalizatora: sygnał wyświetlany (kod Ie-1, „” – ciemny), uszkodzenie światła zezwalającego, brak łączności.</summary>
public sealed record StatusSygnalizatora : StatusUrzadzenia
{
    public override string Rodzaj => MapaWlasnosci.Rodzaj.Sygnalizator;
    public string Wyswietlany { get; init; }
    public bool UszkodzenieZezwalajacego { get; init; }
    public bool BrakLacznosci { get; init; }
    internal override void PiszPola(StringBuilder sb) { KanonJson.Tekst(sb, "wyswietlany", Wyswietlany); KanonJson.Logiczna(sb, "uszkodzenieZezwalajacego", UszkodzenieZezwalajacego); KanonJson.Logiczna(sb, "brakLacznosci", BrakLacznosci); }
}

/// <summary>Status szafy zasilania: sieć, UPS, agregat, napięcie nastawcze, agregat nie ruszył samoczynnie.</summary>
public sealed record StatusZasilania : StatusUrzadzenia
{
    public override string Rodzaj => MapaWlasnosci.Rodzaj.Zasilanie;
    public bool Siec { get; init; }
    public bool Ups { get; init; }
    public bool Agregat { get; init; }
    public bool NapiecieNastawcze { get; init; }
    public bool AgregatNieRuszyl { get; init; }
    internal override void PiszPola(StringBuilder sb)
    {
        KanonJson.Logiczna(sb, "siec", Siec); KanonJson.Logiczna(sb, "ups", Ups); KanonJson.Logiczna(sb, "agregat", Agregat);
        KanonJson.Logiczna(sb, "napiecieNastawcze", NapiecieNastawcze); KanonJson.Logiczna(sb, "agregatNieRuszyl", AgregatNieRuszyl);
    }
}

/// <summary>
/// Zamrożone, numerowane wejście cyklu jednego logicznego KZ (CycleInput, 18/19 §7 pkt 2; S02A.02). Powstaje w rejestrze na granicy
/// fazy, zawiera wyłącznie wartości (statusy kart skierowane do tego KZ według mapy własności, czas i numerację) i jest wspólne dla
/// wszystkich odbiorców tego KZ – ten sam obiekt i ta sama treść kanoniczna. Nie daje dostępu do kart, terenu, pociągów, prywatnego
/// stanu KZ ani do zwrotnego wywołania. v1 nie zawiera jeszcze intencji poleceń ani komunikatów KZ–KZ (S02A.03, S02A1, D03).
/// </summary>
public sealed record WejscieCyklu
{
    public const int WersjaSchematu = 1;
    public const string Kontrakt = "kz.wejscie_cyklu";

    /// <summary>kzId odbiorcy (mapa własności, KZ-{postId}).</summary>
    public string KzId { get; init; }
    /// <summary>inputSeq: kolejny numer wejścia tego KZ w świecie, od 1, bez luk.</summary>
    public long NrWejscia { get; init; }
    /// <summary>Numer granicy fazy w świecie (wspólny dla wejść wszystkich KZ z tej samej partii).</summary>
    public long NrPartii { get; init; }
    /// <summary>cycleId: numer cyklu świata (<see cref="Simulator.NrCyklu"/>; 0 – przed pierwszym cyklem).</summary>
    public long Cykl { get; init; }
    public FazaWejscia Faza { get; init; }
    /// <summary>Czas symulacji [ms] w chwili zamrożenia (zaokrąglony <see cref="Simulator.T"/>).</summary>
    public long CzasSymulacjiMs { get; init; }
    /// <summary>SHA-256 tabeli rozsyłania (<see cref="TrasaWejsc.Sha256"/>) – zgodność definicji odbiorców.</summary>
    public string SkrotTrasy { get; init; }
    /// <summary>Statusy w porządku kanonicznym: rodzaj, identyfikator (porządek ordinalny), kolejne zgłoszenia obiektu w kolejności zgłoszenia.</summary>
    public ImmutableArray<StatusUrzadzenia> Statusy { get; init; } = ImmutableArray<StatusUrzadzenia>.Empty;

    public static string NazwaFazy(FazaWejscia f) => f switch
    {
        FazaWejscia.Karty => "KARTY", FazaWejscia.Ruch => "RUCH", FazaWejscia.Rozkaz => "ROZKAZ", FazaWejscia.Usterka => "USTERKA",
        FazaWejscia.ZdarzenieKarty => "ZDARZENIE_KARTY", FazaWejscia.PozaFaza => "POZA_FAZA", _ => throw new ArgumentOutOfRangeException(nameof(f)),
    };

    /// <summary>
    /// Kanoniczny JSON v1 (jedna linia, separatory „,” i „:”, pola w stałej kolejności, ciągi wg RFC 8785 – <see cref="KanonJson"/>) –
    /// podstawa porównań i skrótu; bajt w bajt jak <c>json.dumps(..., ensure_ascii=False, separators=(",", ":"))</c>. Wynik trafia do
    /// plików i porównań, nigdy do HTML.
    /// </summary>
    public string Kanoniczny()
    {
        var sb = new StringBuilder(256 + 96 * Statusy.Length);
        sb.Append('{');
        KanonJson.Liczba(sb, "schemaVersion", WersjaSchematu, pierwsze: true);
        KanonJson.Tekst(sb, "kontrakt", Kontrakt);
        KanonJson.Tekst(sb, "kzId", KzId);
        KanonJson.Liczba(sb, "nrWejscia", NrWejscia);
        KanonJson.Liczba(sb, "nrPartii", NrPartii);
        KanonJson.Liczba(sb, "cykl", Cykl);
        KanonJson.Tekst(sb, "faza", NazwaFazy(Faza));
        KanonJson.Liczba(sb, "czasSymulacjiMs", CzasSymulacjiMs);
        KanonJson.Tekst(sb, "skrotTrasy", SkrotTrasy);
        KanonJson.Pole(sb, "statusy").Append('[');
        for (int i = 0; i < Statusy.Length; i++)
        {
            var s = Statusy[i];
            if (i > 0) sb.Append(',');
            sb.Append('{');
            KanonJson.Pole(sb, "rodzaj", pierwsze: true); KanonJson.Tekst(sb, s.Rodzaj);
            KanonJson.Tekst(sb, "id", s.Obiekt);
            s.PiszPola(sb);
            sb.Append('}');
        }
        sb.Append("]}");
        return sb.ToString();
    }

    /// <summary>SHA-256 (hex, małe litery) bajtów UTF-8 <see cref="Kanoniczny"/>.</summary>
    public string Sha256() => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Kanoniczny())));
}

/// <summary>
/// Partia jednej granicy fazy: wszystkie zamrożone statusy w kolejności zgłoszenia oraz utworzone z nich wejścia KZ. Przejściowy
/// odbiorca obszaru – jeden KZ C# całego układu (do S02B) – stosuje <see cref="Statusy"/> w kolejności zgłoszenia, co zachowuje
/// bazową kolejność dziennika; to te same niezmienne rekordy, które trafiły do <see cref="Wejscia"/>.
/// </summary>
public sealed record PartiaWejsc
{
    public long NrPartii { get; init; }
    public long Cykl { get; init; }
    public FazaWejscia Faza { get; init; }
    public long CzasSymulacjiMs { get; init; }
    public ImmutableArray<StatusUrzadzenia> Statusy { get; init; } = ImmutableArray<StatusUrzadzenia>.Empty;
    public ImmutableArray<WejscieCyklu> Wejscia { get; init; } = ImmutableArray<WejscieCyklu>.Empty;
}

/// <summary>
/// Odbiorca wejść jednego KZ (testowy odbiorca S02A.02, w przyszłości grupa/kanał, S02B). Dostaje każde wejście swojego KZ raz,
/// w kolejności <see cref="WejscieCyklu.NrWejscia"/>. Wyjątek odbiorcy wyłącza go (bez luk w strumieniu) i nie przerywa świata.
/// </summary>
public interface IOdbiorcaWejscCyklu
{
    void Odbierz(WejscieCyklu wejscie);
}

/// <summary>Przejściowy odbiorca całej partii – wyłącznie jeden KZ C# obszaru (wewnętrzny, poza kontraktem).</summary>
internal interface IOdbiorcaPartiiWejsc
{
    void Przyjmij(PartiaWejsc partia);
}

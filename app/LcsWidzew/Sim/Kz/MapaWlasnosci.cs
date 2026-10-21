using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using LcsWidzew.Model;

namespace LcsWidzew.Sim;

/// <summary>
/// Mapa własności (S02A.01, kontrakt <c>kontrakty/kz/mapa_wlasnosci_v1.schema.json</c>): stałe postId/kzId każdego posterunku,
/// właściciel danych każdego urządzenia (odcinek, zwrotnica, sygnalizator, wykolejnica, zasilanie), lokalne końce blokad
/// oraz inwentarz granic – odwołań przebiegów, ochrony, sygnałów i blokad do obiektów innego KZ, które dziś idą przez wspólne
/// słowniki jednego komputera zależności. Mapa jest wyprowadzana wyłącznie z danych zaprojektowanych przez człowieka
/// (pola „post”, przedrostki kluczy, posterunki węzłów) i z gotowej tablicy przebiegów; niczego nie projektuje ani nie wybiera
/// właściciela za człowieka: przypadek niejednoznaczny lub nieznany zostaje jawnym problemem, a <see cref="KzObiektu"/> zwraca
/// wtedy null (bez domyślnej zgody dla nieznanego celu). Nie scala KZ posterunków LCS i nie zmienia działania symulacji.
/// Wynik jest niemutowalny i deterministyczny – kolejność słowników nie zmienia JSON ani skrótu.
/// </summary>
public sealed class MapaWlasnosci
{
    public const int WersjaSchematu = 1;
    public const string Kontrakt = "kz.mapa_wlasnosci";
    /// <summary>Reguła v1: jeden logiczny KZ na posterunek, kzId = „KZ-” + postId. Inna reguła wymaga nowej wersji kontraktu.</summary>
    public const string RegulaKzId = "KZ-{postId}";
    public const string PrzedrostekKz = "KZ-";
    /// <summary>Najwięcej przykładowych kontekstów (przebiegów, szlaków) zapisanych przy jednej granicy; pozostałe tylko liczone.</summary>
    public const int MaxKontekstow = 64;

    public static class Rodzaj
    {
        public const string Odcinek = "odcinek", Zwrotnica = "zwrotnica", Sygnalizator = "sygnalizator", Wykolejnica = "wykolejnica",
            Zasilanie = "zasilanie", Blokada = "blokada", Posterunek = "posterunek", KoniecBlokady = "koniecBlokady";
    }

    public static class Status
    {
        public const string Przypisany = "PRZYPISANY", Niejednoznaczny = "NIEJEDNOZNACZNY", Nieznany = "NIEZNANY";
    }

    public static class Zrodlo
    {
        public const string PolePost = "pole_post", PrefiksKlucza = "prefiks_klucza", WezlyTorow = "wezly_torow", Brak = "brak";
    }

    public static class RodzajSzlaku
    {
        public const string Zewnetrzny = "ZEWNETRZNY", WewnetrznyJedenKz = "WEWNETRZNY_JEDEN_KZ", WewnetrznyMiedzyKz = "WEWNETRZNY_MIEDZY_KZ", Nieokreslony = "NIEOKRESLONY";
    }

    /// <summary>Rodzaje granic (odwołań KZ odbiorcy do obiektu właściciela innego KZ).</summary>
    public static class RodzajGranicy
    {
        public const string PrzebiegOdcinek = "PRZEBIEG_ODCINEK", PrzebiegZwrotnica = "PRZEBIEG_ZWROTNICA", PrzebiegWykolejnica = "PRZEBIEG_WYKOLEJNICA",
            OchronaBoczna = "OCHRONA_BOCZNA", DrogaOchronna = "DROGA_OCHRONNA", WarunekSpecjalny = "WARUNEK_SPECJALNY",
            ZaleznoscPrzebiegow = "ZALEZNOSC_PRZEBIEGOW", NastepnySygnal = "NASTEPNY_SYGNAL", OdstepowyNastepny = "ODSTEPOWY_NASTEPNY",
            BlokadaKoniec = "BLOKADA_KONIEC", BlokadaOdstep = "BLOKADA_ODSTEP", BlokadaSygnal = "BLOKADA_SYGNAL";
    }

    public static class KodProblemu
    {
        public const string PostIdInvalid = "POST_ID_INVALID", ObjectPostUnknown = "OBJECT_POST_UNKNOWN", PostFieldPrefixMismatch = "POST_FIELD_PREFIX_MISMATCH",
            SectionPostAmbiguous = "SECTION_POST_AMBIGUOUS", SectionPostUnknown = "SECTION_POST_UNKNOWN", SectionPrefixNodesMismatch = "SECTION_PREFIX_NODES_MISMATCH",
            BlockEndPostUnknown = "BLOCK_END_POST_UNKNOWN", BlockWithoutEnds = "BLOCK_WITHOUT_ENDS", BlockInternalCrossKz = "BLOCK_INTERNAL_CROSS_KZ",
            PowerDomainUnassigned = "POWER_DOMAIN_UNASSIGNED", PostWithoutObjects = "POST_WITHOUT_OBJECTS", DanglingReference = "DANGLING_REFERENCE";
    }

    public sealed record Posterunek(string PostId, string KzId, string Nazwa, int LiczbaObiektow);
    public sealed record Obiekt(string Rodzaj, string Id, string Status, string PostId, string KzId, string Zrodlo, IReadOnlyList<string> Kandydaci, IReadOnlyList<string> Odbiorcy);
    public sealed record KoniecBlokady(string BlokadaId, int Indeks, string Status, string PostId, string KzId, string SemaforWjazdowy, string Granica);
    public sealed record Szlak(string BlokadaId, string Rodzaj, string TypBlokady, IReadOnlyList<string> KzKoncow, string Sasiad);
    public sealed record Granica(string Rodzaj, string ZKz, string DoKz, string ObiektRodzaj, string ObiektId, int LiczbaOdwolan, IReadOnlyList<string> Konteksty);
    public sealed record Problem(string Kod, string Rodzaj, string Id, IReadOnlyList<string> Kandydaci);

    public string NazwaUkladu { get; }
    public string KodUkladu { get; }
    public int LiczbaPrzebiegow { get; }
    public int LiczbaZaleznosciSpecjalnych { get; }
    public IReadOnlyList<Posterunek> Posterunki { get; }
    public IReadOnlyList<Obiekt> Obiekty { get; }
    public IReadOnlyList<KoniecBlokady> KonceBlokad { get; }
    public IReadOnlyList<Szlak> Szlaki { get; }
    public IReadOnlyList<Granica> Granice { get; }
    public IReadOnlyList<Problem> Problemy { get; }
    /// <summary>Kanoniczny JSON v1 (UTF-8, wcięcia 2, LF) – artefakt do przeglądu przez człowieka i porównań.</summary>
    public string Json { get; }
    /// <summary>SHA-256 (hex, małe litery) bajtów UTF-8 <see cref="Json"/>.</summary>
    public string Sha256 { get; }
    /// <summary>Czas budowy [µs] – tylko diagnostyka, nie wchodzi do JSON.</summary>
    public long CzasBudowyUs { get; }

    readonly Dictionary<(string, string), Obiekt> obiekty;
    readonly Dictionary<(string, int), KoniecBlokady> konce;
    readonly Dictionary<string, string> kzPosterunku;

    /// <summary>kzId właściciela obiektu albo null, gdy obiekt nie istnieje lub jego właściciel jest niejednoznaczny/nieznany.</summary>
    public string KzObiektu(string rodzaj, string id) =>
        rodzaj != null && id != null && obiekty.TryGetValue((rodzaj, id), out var o) && o.Status == Status.Przypisany ? o.KzId : null;

    /// <summary>Wpis obiektu (także nieprzypisanego) albo null.</summary>
    public Obiekt ObiektMapy(string rodzaj, string id) => rodzaj != null && id != null && obiekty.TryGetValue((rodzaj, id), out var o) ? o : null;

    /// <summary>kzId lokalnego końca blokady albo null.</summary>
    public string KzKoncaBlokady(string blokadaId, int indeks) =>
        blokadaId != null && konce.TryGetValue((blokadaId, indeks), out var k) && k.Status == Status.Przypisany ? k.KzId : null;

    /// <summary>kzId posterunku albo null (posterunek nieznany lub z niepoprawnym identyfikatorem).</summary>
    public string KzPosterunku(string postId) => postId != null && kzPosterunku.TryGetValue(postId, out var kz) ? kz : null;

    MapaWlasnosci(Budowa b, long czasUs)
    {
        NazwaUkladu = b.L.Name; KodUkladu = b.L.Code; LiczbaPrzebiegow = b.LiczbaPrzebiegow; LiczbaZaleznosciSpecjalnych = b.L.Zal?.Przebiegi.Count ?? 0;
        obiekty = new(b.Obiekty.Count);
        var lista = new List<Obiekt>(b.Obiekty.Count);
        foreach (var w in b.Obiekty.Values.OrderBy(w => w.Rodzaj, StringComparer.Ordinal).ThenBy(w => w.Id, StringComparer.Ordinal))
        {
            var o = new Obiekt(w.Rodzaj, w.Id, w.Status, w.PostId, w.KzId, w.Zrodlo, Ro(w.Kandydaci), Ro(w.Odbiorcy));
            obiekty[(o.Rodzaj, o.Id)] = o; lista.Add(o);
        }
        Obiekty = lista.AsReadOnly();
        konce = new();
        foreach (var k in b.Konce) konce[(k.BlokadaId, k.Indeks)] = k;
        KonceBlokad = b.Konce.OrderBy(k => k.BlokadaId, StringComparer.Ordinal).ThenBy(k => k.Indeks).ToList().AsReadOnly();
        Szlaki = b.Szlaki.OrderBy(s => s.BlokadaId, StringComparer.Ordinal).ToList().AsReadOnly();
        kzPosterunku = new(b.Kz, StringComparer.Ordinal);
        var wlasne = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var o in Obiekty) if (o.KzId != null) wlasne[o.KzId] = wlasne.GetValueOrDefault(o.KzId) + 1;
        foreach (var k in KonceBlokad) if (k.KzId != null) wlasne[k.KzId] = wlasne.GetValueOrDefault(k.KzId) + 1;
        var posty = new List<Posterunek>();
        foreach (var (post, kz) in b.Kz.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            int n = wlasne.GetValueOrDefault(kz);
            posty.Add(new Posterunek(post, kz, b.L.Posts.GetValueOrDefault(post), n));
            if (n == 0) b.Dodaj(KodProblemu.PostWithoutObjects, Rodzaj.Posterunek, post);
        }
        Posterunki = posty.AsReadOnly();
        Granice = b.Granice.Select(g => new Granica(g.Key.Rodzaj, g.Key.Z, g.Key.Do, g.Key.ObiektRodzaj, g.Key.ObiektId, g.Value.Liczba,
                g.Value.Konteksty.Take(MaxKontekstow).ToList().AsReadOnly()))
            .OrderBy(g => g.Rodzaj, StringComparer.Ordinal).ThenBy(g => g.ZKz, StringComparer.Ordinal).ThenBy(g => g.DoKz, StringComparer.Ordinal)
            .ThenBy(g => g.ObiektRodzaj, StringComparer.Ordinal).ThenBy(g => g.ObiektId, StringComparer.Ordinal).ToList().AsReadOnly();
        Problemy = b.Problemy.Values.OrderBy(p => p.Kod, StringComparer.Ordinal).ThenBy(p => p.Rodzaj, StringComparer.Ordinal).ThenBy(p => p.Id, StringComparer.Ordinal).ToList().AsReadOnly();
        Json = DoJson();
        Sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Json)));
        CzasBudowyUs = czasUs;
    }

    static IReadOnlyList<string> Ro(IEnumerable<string> s) => s.ToList().AsReadOnly();

    static readonly Regex PoprawnyPostId = new(@"^[0-9A-Za-z_\u00C0-\u00D6\u00D8-\u00F6\u00F8-\u024F]{1,32}\z", RegexOptions.CultureInvariant);

    /// <summary>Czy identyfikator posterunku nadaje się na postId/kzId: litery łacińskie (także narodowe, Latin-1/Extended-A/B), cyfry, podkreślnik; 1–32 znaki (jak wzorzec w schemacie).</summary>
    public static bool PoprawnyIdentyfikatorPosterunku(string postId) => postId != null && PoprawnyPostId.IsMatch(postId);

    /// <summary>
    /// Buduje mapę z układu i (opcjonalnie) tablicy przebiegów. <paramref name="nastepnyZaPrzebiegiem"/> i
    /// <paramref name="nastepnyZaOdstepowym"/> to istniejące zapytania KZ o następny semafor (uzależnienie sygnału); bez nich
    /// granice NASTEPNY_SYGNAL / ODSTEPOWY_NASTEPNY nie są inwentaryzowane. Metoda nie zmienia układu ani tablicy.
    /// </summary>
    public static MapaWlasnosci Zbuduj(Station L, IReadOnlyDictionary<string, List<RouteDef>> tablica = null,
        Func<RouteDef, string> nastepnyZaPrzebiegiem = null, Func<string, string> nastepnyZaOdstepowym = null)
    {
        ArgumentNullException.ThrowIfNull(L);
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        var b = new Budowa(L);
        b.Posterunki();
        b.Urzadzenia();
        b.Blokady();
        if (tablica != null) b.Przebiegi(tablica, nastepnyZaPrzebiegiem);
        if (nastepnyZaOdstepowym != null) b.Odstepowe(nastepnyZaOdstepowym);
        return new MapaWlasnosci(b, (long)System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMicroseconds);
    }

    // ------------------------------------------------------------------ budowa (prywatny, jednorazowy stan)
    sealed class Wpis
    {
        public string Rodzaj, Id, Status, PostId, KzId, Zrodlo;
        public SortedSet<string> Kandydaci = new(StringComparer.Ordinal), Odbiorcy = new(StringComparer.Ordinal);
    }
    readonly record struct KluczGranicy(string Rodzaj, string Z, string Do, string ObiektRodzaj, string ObiektId);
    sealed class Odwolania { public int Liczba; public SortedSet<string> Konteksty = new(StringComparer.Ordinal); }

    sealed class Budowa(Station l)
    {
        public readonly Station L = l;
        public readonly Dictionary<string, string> Kz = new(StringComparer.Ordinal);
        public readonly Dictionary<(string, string), Wpis> Obiekty = new();
        public readonly List<KoniecBlokady> Konce = new();
        public readonly List<Szlak> Szlaki = new();
        public readonly Dictionary<KluczGranicy, Odwolania> Granice = new();
        public readonly Dictionary<(string, string, string), Problem> Problemy = new();
        public int LiczbaPrzebiegow;

        static IEnumerable<string> Po(IEnumerable<string> klucze) => klucze.OrderBy(k => k, StringComparer.Ordinal);
        string KzDla(string post) => post != null && Kz.TryGetValue(post, out var kz) ? kz : null;

        public void Dodaj(string kod, string rodzaj, string id, IEnumerable<string> kandydaci = null) =>
            Problemy.TryAdd((kod, rodzaj, id ?? ""), new Problem(kod, rodzaj, id ?? "", Ro((kandydaci ?? Array.Empty<string>()).OrderBy(x => x, StringComparer.Ordinal))));

        public void Posterunki()
        {
            foreach (var post in Po(L.Posts.Keys))
                if (PoprawnyIdentyfikatorPosterunku(post)) Kz[post] = PrzedrostekKz + post;
                else Dodaj(KodProblemu.PostIdInvalid, Rodzaj.Posterunek, post);
        }

        Wpis Nowy(string rodzaj, string id)
        {
            var w = new Wpis { Rodzaj = rodzaj, Id = id };
            Obiekty[(rodzaj, id)] = w;
            return w;
        }

        void Przypisz(Wpis w, string post, string zrodlo)
        {
            w.Status = Status.Przypisany; w.PostId = post; w.KzId = Kz[post]; w.Zrodlo = zrodlo; w.Odbiorcy.Add(w.KzId);
        }

        void ZPolaPost(string rodzaj, string id, string post)
        {
            var w = Nowy(rodzaj, id);
            if (KzDla(post) != null) Przypisz(w, post, Zrodlo.PolePost);
            else { w.Status = Status.Nieznany; w.Zrodlo = Zrodlo.Brak; Dodaj(KodProblemu.ObjectPostUnknown, rodzaj, id); }
            // pole „post” niezgodne z przedrostkiem klucza: system nadrzędny (CheckControl) bierze dziś przedrostek
            var pre = Station.PostOf(id);
            if (pre != "" && L.Posts.ContainsKey(pre) && !string.IsNullOrEmpty(post) && pre != post)
                Dodaj(KodProblemu.PostFieldPrefixMismatch, rodzaj, id, new[] { pre, post });
        }

        public void Urzadzenia()
        {
            foreach (var k in Po(L.Switches.Keys)) ZPolaPost(Rodzaj.Zwrotnica, k, L.Switches[k].Post);
            foreach (var k in Po(L.Signals.Keys)) ZPolaPost(Rodzaj.Sygnalizator, k, L.Signals[k].Post);
            foreach (var k in Po(L.Derailers.Keys)) ZPolaPost(Rodzaj.Wykolejnica, k, L.Derailers[k].Post);
            foreach (var id in Po(L.Sections.Keys))
            {
                var w = Nowy(Rodzaj.Odcinek, id);
                var wezly = new SortedSet<string>(StringComparer.Ordinal);
                foreach (var eid in L.Sections[id].Edges)
                    if (L.Edges.TryGetValue(eid, out var e))
                        foreach (var n in new[] { e.A, e.B })
                            if (n != null && L.Nodes.TryGetValue(n, out var nd) && !string.IsNullOrEmpty(nd.Post)) wezly.Add(nd.Post);
                var pre = Station.PostOf(id);
                if (KzDla(pre) != null)
                {
                    Przypisz(w, pre, Zrodlo.PrefiksKlucza);
                    if (wezly.Count > 0 && !wezly.Contains(pre)) Dodaj(KodProblemu.SectionPrefixNodesMismatch, Rodzaj.Odcinek, id, wezly);
                    continue;
                }
                var znane = wezly.Where(p => KzDla(p) != null).ToList();
                if (znane.Count == 1) Przypisz(w, znane[0], Zrodlo.WezlyTorow);
                else
                {
                    w.Zrodlo = Zrodlo.Brak;
                    w.Status = znane.Count > 1 ? Status.Niejednoznaczny : Status.Nieznany;
                    foreach (var p in znane) w.Kandydaci.Add(p);
                    Dodaj(znane.Count > 1 ? KodProblemu.SectionPostAmbiguous : KodProblemu.SectionPostUnknown, Rodzaj.Odcinek, id, znane);
                }
            }
            // jedna szafa zasilania obszaru (Sim/Urzadzenia/Zasilanie.cs) – bez zaprojektowanej domeny zasilania nie ma właściciela
            var z = Nowy(Rodzaj.Zasilanie, Zasilanie.Id);
            z.Status = Status.Nieznany; z.Zrodlo = Zrodlo.Brak;
            foreach (var kz in Kz.Values) z.Odbiorcy.Add(kz);
            Dodaj(KodProblemu.PowerDomainUnassigned, Rodzaj.Zasilanie, Zasilanie.Id);
        }

        /// <summary>KZ odbiorcy korzysta z obiektu (meldunek/stan potrzebny do jego zależności); obcy właściciel daje granicę.</summary>
        void Odwolanie(string zKz, string rodzaj, string id, string rodzajGranicy, string kontekst)
        {
            if (zKz == null || string.IsNullOrEmpty(id)) return;
            if (!Obiekty.TryGetValue((rodzaj, id), out var w)) { Dodaj(KodProblemu.DanglingReference, rodzaj, id); return; }
            w.Odbiorcy.Add(zKz);
            if (w.Status != Status.Przypisany || w.KzId == zKz) return;
            var k = new KluczGranicy(rodzajGranicy, zKz, w.KzId, rodzaj, id);
            if (!Granice.TryGetValue(k, out var o)) Granice[k] = o = new Odwolania();
            o.Liczba++;
            if (kontekst != null) o.Konteksty.Add(kontekst);
        }

        public void Blokady()
        {
            foreach (var id in Po(L.Blocks.Keys))
            {
                var b = L.Blocks[id];
                var kzKoncow = new List<string>();
                for (int i = 0; i < b.Ends.Count; i++)
                {
                    var e = b.Ends[i]; var kz = KzDla(e.Post);
                    Konce.Add(new KoniecBlokady(id, i, kz != null ? Status.Przypisany : Status.Nieznany, kz != null ? e.Post : null, kz, e.EntrySig, e.Boundary));
                    if (kz == null) Dodaj(KodProblemu.BlockEndPostUnknown, Rodzaj.KoniecBlokady, id + "#" + i);
                    kzKoncow.Add(kz);
                }
                string rodzaj;
                if (b.Ends.Count == 0) { rodzaj = RodzajSzlaku.Nieokreslony; Dodaj(KodProblemu.BlockWithoutEnds, Rodzaj.Blokada, id); }
                else if (kzKoncow.Any(k => k == null)) rodzaj = RodzajSzlaku.Nieokreslony;
                else if (b.Ends.Count == 1) rodzaj = RodzajSzlaku.Zewnetrzny;
                else if (kzKoncow.Distinct(StringComparer.Ordinal).Count() == 1) rodzaj = RodzajSzlaku.WewnetrznyJedenKz;
                else
                {
                    rodzaj = RodzajSzlaku.WewnetrznyMiedzyKz;
                    // dziś wspólny BlockState obu końców (State.cs) i Wbl jednym poleceniem dla Internal (Blocks.cs) – decyzja D03
                    Dodaj(KodProblemu.BlockInternalCrossKz, Rodzaj.Blokada, id, kzKoncow);
                    foreach (var a in kzKoncow.Distinct(StringComparer.Ordinal))
                        foreach (var c in kzKoncow.Distinct(StringComparer.Ordinal).Where(c => c != a))
                        {
                            var k = new KluczGranicy(RodzajGranicy.BlokadaKoniec, a, c, Rodzaj.Blokada, id);
                            if (!Granice.TryGetValue(k, out var o)) Granice[k] = o = new Odwolania();
                            o.Liczba++; o.Konteksty.Add("szlak " + id);
                        }
                }
                Szlaki.Add(new Szlak(id, rodzaj, b.Type, kzKoncow.AsReadOnly(), b.Ends.Count == 1 ? b.Nbr : null));
                // stan szlaku liczony przy każdym końcu (BlockConds, KTor, TorLamp, BlockAllowsExit): odstępy, semafory odstępowe i wjazdowe
                var odstepowe = L.Signals.Values.Where(s => s.Szlak == id && s.Type == "odstepowy").Select(s => s.Name).OrderBy(x => x, StringComparer.Ordinal).ToList();
                var wjazdowe = b.Ends.Select(x => x.EntrySig).Where(s => !string.IsNullOrEmpty(s) && L.Signals.ContainsKey(s)).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
                foreach (var kz in kzKoncow.Where(k => k != null).Distinct(StringComparer.Ordinal))
                {
                    foreach (var sc in b.SecsWE) Odwolanie(kz, Rodzaj.Odcinek, sc, RodzajGranicy.BlokadaOdstep, "szlak " + id);
                    foreach (var s in odstepowe.Concat(wjazdowe)) Odwolanie(kz, Rodzaj.Sygnalizator, s, RodzajGranicy.BlokadaSygnal, "szlak " + id);
                }
            }
        }

        public void Przebiegi(IReadOnlyDictionary<string, List<RouteDef>> tablica, Func<RouteDef, string> nastepny)
        {
            foreach (var start in Po(tablica.Keys))
            {
                var lista = tablica[start];
                if (lista == null) continue;
                foreach (var r in lista)
                {
                    if (r == null) continue;
                    LiczbaPrzebiegow++;
                    var kz = Obiekty.TryGetValue((Rodzaj.Sygnalizator, r.Start ?? ""), out var sw) && sw.Status == Status.Przypisany ? sw.KzId : null;
                    if (kz == null) continue;
                    var ctx = r.Id;
                    foreach (var sc in r.Secs) Odwolanie(kz, Rodzaj.Odcinek, sc, RodzajGranicy.PrzebiegOdcinek, ctx);
                    foreach (var (num, _) in r.Sw) Odwolanie(kz, Rodzaj.Zwrotnica, num, RodzajGranicy.PrzebiegZwrotnica, ctx);
                    foreach (var d in r.Ders) Odwolanie(kz, Rodzaj.Wykolejnica, d, RodzajGranicy.PrzebiegWykolejnica, ctx);
                    foreach (var f in r.Flank)
                    {
                        var rodzaj = f.Kind switch { "sw" => Rodzaj.Zwrotnica, "der" => Rodzaj.Wykolejnica, "sig" => Rodzaj.Sygnalizator, _ => null };
                        if (rodzaj != null) Odwolanie(kz, rodzaj, f.Id, RodzajGranicy.OchronaBoczna, ctx);
                    }
                    if (!string.IsNullOrEmpty(r.Overlap)) Odwolanie(kz, Rodzaj.Odcinek, r.Overlap, RodzajGranicy.DrogaOchronna, ctx);
                    foreach (var alt in r.OverlapAlts)
                    {
                        foreach (var sc in alt.Odcinki) Odwolanie(kz, Rodzaj.Odcinek, sc, RodzajGranicy.DrogaOchronna, ctx);
                        foreach (var z in alt.Zwrotnice)
                            Odwolanie(kz, Rodzaj.Zwrotnica, z.Length > 1 && z[^1] is '+' or '-' ? z[..^1] : z, RodzajGranicy.DrogaOchronna, ctx);
                    }
                    foreach (var c in r.Conds)
                    {
                        var (obj, _) = Zaleznosci.Warunek(c);
                        var rodzaj = L.Switches.ContainsKey(obj) ? Rodzaj.Zwrotnica : L.Sections.ContainsKey(obj) ? Rodzaj.Odcinek
                            : L.Derailers.ContainsKey(obj) ? Rodzaj.Wykolejnica : L.Signals.ContainsKey(obj) ? Rodzaj.Sygnalizator : null;
                        if (rodzaj != null) Odwolanie(kz, rodzaj, obj, RodzajGranicy.WarunekSpecjalny, ctx);
                        else if (!string.IsNullOrEmpty(obj)) Dodaj(KodProblemu.DanglingReference, "warunek", obj);
                    }
                    foreach (var key in r.Excl.Concat(r.Allow))
                    {
                        // klucz przebiegu „P start→koniec” (Zaleznosci.Klucz) – właściciel przebiegu to właściciel semafora początkowego
                        var n = Zaleznosci.Norm(key); int sp = n.IndexOf(' '), ar = n.IndexOf('→');
                        if (sp > 0 && ar > sp + 1) Odwolanie(kz, Rodzaj.Sygnalizator, n[(sp + 1)..ar], RodzajGranicy.ZaleznoscPrzebiegow, ctx);
                    }
                    if (r.Type == "P" && nastepny != null && nastepny(r) is string nx) Odwolanie(kz, Rodzaj.Sygnalizator, nx, RodzajGranicy.NastepnySygnal, ctx);
                }
            }
        }

        public void Odstepowe(Func<string, string> nastepny)
        {
            foreach (var k in Po(L.Signals.Keys))
            {
                if (L.Signals[k].Type != "odstepowy") continue;
                var kz = Obiekty[(Rodzaj.Sygnalizator, k)].KzId;
                if (kz != null && nastepny(k) is string nx) Odwolanie(kz, Rodzaj.Sygnalizator, nx, RodzajGranicy.OdstepowyNastepny, "semafor " + k);
            }
        }
    }

    // ------------------------------------------------------------------ kanoniczny JSON
    string DoJson()
    {
        var opcje = new JsonWriterOptions { Indented = true, NewLine = "\n", Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, opcje))
        {
            w.WriteStartObject();
            w.WriteNumber("schemaVersion", WersjaSchematu);
            w.WriteString("kontrakt", Kontrakt);
            w.WriteString("regulaKzId", RegulaKzId);
            w.WriteStartObject("zrodlo");
            w.WriteString("nazwa", NazwaUkladu); w.WriteString("kod", KodUkladu);
            w.WriteNumber("przebiegi", LiczbaPrzebiegow); w.WriteNumber("zaleznosciSpecjalne", LiczbaZaleznosciSpecjalnych);
            w.WriteEndObject();
            w.WriteStartArray("posterunki");
            foreach (var p in Posterunki)
            {
                w.WriteStartObject();
                w.WriteString("postId", p.PostId); w.WriteString("kzId", p.KzId); w.WriteString("nazwa", p.Nazwa); w.WriteNumber("liczbaObiektow", p.LiczbaObiektow);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteStartArray("obiekty");
            foreach (var o in Obiekty)
            {
                w.WriteStartObject();
                w.WriteString("rodzaj", o.Rodzaj); w.WriteString("id", o.Id); w.WriteString("status", o.Status);
                w.WriteString("postId", o.PostId); w.WriteString("kzId", o.KzId); w.WriteString("zrodlo", o.Zrodlo);
                Tablica(w, "kandydaci", o.Kandydaci); Tablica(w, "odbiorcy", o.Odbiorcy);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteStartArray("konceBlokad");
            foreach (var k in KonceBlokad)
            {
                w.WriteStartObject();
                w.WriteString("blokadaId", k.BlokadaId); w.WriteNumber("indeks", k.Indeks); w.WriteString("status", k.Status);
                w.WriteString("postId", k.PostId); w.WriteString("kzId", k.KzId); w.WriteString("semaforWjazdowy", k.SemaforWjazdowy); w.WriteString("granica", k.Granica);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteStartArray("szlaki");
            foreach (var s in Szlaki)
            {
                w.WriteStartObject();
                w.WriteString("blokadaId", s.BlokadaId); w.WriteString("rodzaj", s.Rodzaj); w.WriteString("typBlokady", s.TypBlokady);
                Tablica(w, "kzKoncow", s.KzKoncow); w.WriteString("sasiad", s.Sasiad);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteStartArray("granice");
            foreach (var g in Granice)
            {
                w.WriteStartObject();
                w.WriteString("rodzaj", g.Rodzaj); w.WriteString("zKz", g.ZKz); w.WriteString("doKz", g.DoKz);
                w.WriteString("obiektRodzaj", g.ObiektRodzaj); w.WriteString("obiektId", g.ObiektId);
                w.WriteNumber("liczbaOdwolan", g.LiczbaOdwolan); Tablica(w, "konteksty", g.Konteksty);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteStartArray("problemy");
            foreach (var p in Problemy)
            {
                w.WriteStartObject();
                w.WriteString("kod", p.Kod); w.WriteString("rodzaj", p.Rodzaj); w.WriteString("id", p.Id); Tablica(w, "kandydaci", p.Kandydaci);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteStartObject("podsumowanie");
            w.WriteNumber("posterunki", Posterunki.Count);
            w.WriteNumber("obiekty", Obiekty.Count);
            w.WriteNumber("przypisane", Obiekty.Count(o => o.Status == Status.Przypisany));
            w.WriteNumber("niejednoznaczne", Obiekty.Count(o => o.Status == Status.Niejednoznaczny));
            w.WriteNumber("nieznane", Obiekty.Count(o => o.Status == Status.Nieznany));
            w.WriteNumber("konceBlokad", KonceBlokad.Count);
            w.WriteNumber("szlaki", Szlaki.Count);
            w.WriteNumber("granice", Granice.Count);
            w.WriteNumber("problemy", Problemy.Count);
            w.WriteEndObject();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray()) + "\n";
    }

    static void Tablica(Utf8JsonWriter w, string nazwa, IReadOnlyList<string> wartosci)
    {
        w.WriteStartArray(nazwa);
        foreach (var v in wartosci) if (v == null) w.WriteNullValue(); else w.WriteStringValue(v);
        w.WriteEndArray();
    }
}

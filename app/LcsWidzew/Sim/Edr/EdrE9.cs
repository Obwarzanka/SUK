using System.Globalization;
using System.Security.Cryptography;

namespace LcsWidzew.Sim;

/// <summary>
/// Użytkownik lokalny EDR (E9b, jak „Użytkownicy lokalni” w EDR.exe; decyzja właściciela 2026-10-28 – osobna lista w EDR): identyfikator,
/// imię i nazwisko, rola (dyżurny ruchu / obserwator / koordynator), hasło lokalne (w pliku tylko sól i skrót PBKDF2).
/// </summary>
public class EdrUzytkownik
{
    public static readonly string[] Role = { "Dyżurny ruchu", "Obserwator", "Koordynator" };
    public string Id = "", Nazwa = "", Rola = "Dyżurny ruchu", Sol, Skrot;
    /// <summary>Nowe hasło – tylko w poleceniu „uzytkownicy” (nie zapisywane, nie odsyłane).</summary>
    public string Haslo;
    /// <summary>Do okna: czy hasło jest ustawione (skrót nie wychodzi poza symulator).</summary>
    public bool MaHaslo;
    public EdrUzytkownik Kopia() => (EdrUzytkownik)MemberwiseClone();
    public EdrUzytkownik Publiczny() => new() { Id = Id, Nazwa = Nazwa, Rola = Rola, MaHaslo = !string.IsNullOrEmpty(Skrot) };
    public void UstawHaslo(string haslo)
    {
        var s = RandomNumberGenerator.GetBytes(16);
        Sol = Convert.ToBase64String(s);
        Skrot = Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(haslo, s, 20000, HashAlgorithmName.SHA256, 32));
    }
    public bool Sprawdz(string haslo)
    {
        if (string.IsNullOrEmpty(Sol) || string.IsNullOrEmpty(Skrot)) return false;
        var h = Rfc2898DeriveBytes.Pbkdf2(haslo ?? "", Convert.FromBase64String(Sol), 20000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(h, Convert.FromBase64String(Skrot));
    }
}

/// <summary>Zmiana w EDR do okna „Historia zmian” (E9b): kiedy, kto, czynność, czego dotyczy, opis.</summary>
public class EdrZdarzenie { public string Czas { get; set; } = ""; public string Kto { get; set; } = ""; public string Czynnosc { get; set; } = ""; public string Obiekt { get; set; } = ""; public string Opis { get; set; } = ""; }

/// <summary>
/// Dane lokalne pociągu na posterunku (E9d, jak `Train` w EDR.exe – dane wpisywane w EDR, nie z rozkładu): pociąg STÓJ i prognoza,
/// godziny wprowadzone ręcznie, gotowość do odjazdu, analiza pociągu, towary niebezpieczne / TWR / przesyłka nadzwyczajna, lokalny opis,
/// wiersze zakładek (kody opóźnień, przesyłki, telegramy, zarządzenia, skomunikowania, przejścia składu, historia TN).
/// EDR jest tylko zobrazowaniem – nic z tego nie zmienia ruchu w symulacji.
/// </summary>
public class EdrPociagLokalny
{
    public bool Stoj;
    public string StojOd = "", PrognozaPrz = "", PrognozaOdj = "";
    /// <summary>Godziny przyjazdu i odjazdu wprowadzone w EDR („Wprowadzanie godzin”) i przyczyna dodania do czasu.</summary>
    public string CzasPrz = "", CzasOdj = "", Przyczyna = "";
    public string GotowoscZgl = "", GotowoscWpis = "", Maszynista = "";
    public string Brutto = "", Dlugosc = "";
    public List<string> Serie = new(), NumeryLok = new();
    public bool TN, TWR, PN;
    public string Ladunek = "";
    /// <summary>Lokalny opis pociągu (pola jak `TrainProfile` w EDR.exe) i pola wyboru.</summary>
    public Dictionary<string, string> Opis = new();
    public bool Skrajnia, Nacisk, Zamknieciowy, Sledzony;
    public List<EdrWierszLok> Wiersze = new();
    public EdrPociagLokalny Kopia()
    {
        var x = (EdrPociagLokalny)MemberwiseClone();
        x.Serie = new(Serie ?? new()); x.NumeryLok = new(NumeryLok ?? new()); x.Opis = new(Opis ?? new());
        x.Wiersze = (Wiersze ?? new()).Select(w => w.Kopia()).ToList();
        return x;
    }
}

/// <summary>Wiersz tabeli zakładki pociągu (dane lokalne): rodzaj tabeli, komórki, załącznik (plik telegramu PDF).</summary>
public class EdrWierszLok
{
    // właściwości (nie pola) – tabele zakładek wiążą się z nimi w oknie
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Rodzaj { get; set; } = "";
    public string Zalacznik { get; set; } = "";
    public List<string> Komorki { get; set; } = new();
    public EdrWierszLok Kopia() => new() { Id = Id, Rodzaj = Rodzaj, Zalacznik = Zalacznik, Komorki = new(Komorki ?? new()) };
}

/// <summary>Usługa na posterunku (zakładka „Usługi”, jak `ServiceRecord` w EDR.exe): manewry, postój, rozrząd, szerokie tory.</summary>
public class EdrUsluga
{
    public static readonly string[] Rodzaje = { "Manewry", "Postój", "Rozrząd", "Szerokie tory" };
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Post { get; set; } = "";
    public string Rodzaj { get; set; } = "Manewry";
    public string Pociag { get; set; } = "";
    public string Przewoznik { get; set; } = "";
    public string Tor { get; set; } = "";
    public string Od { get; set; } = "";
    public string Do { get; set; } = "";
    public string Uwagi { get; set; } = "";
    public string Status { get; set; } = "Zarejestrowano";
    public EdrUsluga Kopia() => (EdrUsluga)MemberwiseClone();
}

/// <summary>Pociąg lokalny w wykazie (E9e, „Dodaj dane lokalne…” / import CSV w EDR.exe) – tylko w EDR, nie jeździ w symulacji.</summary>
public class EdrPociagLok
{
    public string Id = Guid.NewGuid().ToString("N")[..8], Post = "", Nr = "", Rodzaj = "", Przewoznik = "", ZKierunku = "", WKierunku = "", Tor = "", Przyjazd = "", Odjazd = "", Uwagi = "";
    public EdrPociagLok Kopia() => (EdrPociagLok)MemberwiseClone();
}

/// <summary>Plik konfiguracji posterunku (E9e, „Zapisz / Wczytaj konfigurację posterunku…”).</summary>
public class EdrKonfPosterunku { public EdrPosterunekUst Posterunek; public Dictionary<string, EdrUstawienia> Ustawienia = new(); public List<EdrSprawdzenie> Sprawdzenia; public string Numeracja; }

public partial class Edr
{
    /// <summary>Tabele zakładek pociągu (E9d, jak `BuildDetails` w EDR.exe): id, tytuł, kolumny. „tn” – historia TN wpisywana automatycznie.</summary>
    public static readonly (string Id, string Tytul, string[] Kolumny)[] Tabele =
    {
        ("opoznienia", "Kody opóźnień", new[] { "Winny", "Przyczyna", "Opóźnienie", "Lokalizacja", "Opis przyczyny", "Opis lokalizacji", "Stacja wprowadz.", "W", "IZ opóźn. pierwotnego" }),
        ("nadzwyczajne", "Przesyłki nadzwyczajne lub inne wymagające szczególnego nadzoru", new[] { "Typ", "Przewóz od stacji", "Przewóz do stacji", "L. w.", "1. Wagon", "2. Wagon", "3. Wagon", "Opis" }),
        ("telegramy", "Telegramy zarządzające przejazd przesyłek – IDDE własne / PNK / PNZ", new[] { "IDDE własne / PNK / PNZ", "Nr telegramu", "Uwagi" }),
        ("telegramyobce", "Telegramy zarządzające przejazd przesyłek – IDDE sąsiednie", new[] { "IDDE sąsiednie", "Nr telegramu" }),
        ("zarzadzenia", "Zarządzenie Rozkładu Jazdy Pociągu", new[] { "Nr zamówienia", "Numer zarządzenia", "Nadawca", "Data nadania", "Obszar", "Data zrzutu" }),
        ("skomZ", "Pociągi ZDĄŻAJĄCE na skomunikowanie", new[] { "Nr pociągu", "Przyjazd / czas", "Opóźnienie", "Rodz. poc.", "Przewoźnik", "Relacja" }),
        ("skomNa", "Pociągi OCZEKUJĄCE na skomunikowanie", new[] { "Nr pociągu", "Przyjazd / czas", "Opóźnienie", "Rodz. poc.", "Przewoźnik", "Relacja" }),
        ("skladZ", "Przejście składu Z pociągu", new[] { "Nr pociągu", "Nr wagonów", "Opóźnienie", "Rodz. poc.", "Przewoźnik", "Relacja" }),
        ("skladNa", "Przejście składu NA pociąg", new[] { "Nr pociągu", "Nr wagonów", "Opóźnienie", "Rodz. poc.", "Przewoźnik", "Relacja" }),
        ("tn", "Historia wprowadzania informacji o Towarach Niebezpiecznych", new[] { "Czas", "Imię i nazwisko", "Stacja", "Działanie" }),
    };

    /// <summary>Pola lokalnego opisu pociągu (jak `TrainProfile` w EDR.exe): klucz, etykieta.</summary>
    public static readonly (string Id, string Etykieta)[] PolaOpisu =
    {
        ("Miedzynarodowy", "Numer międzynarodowy"), ("Nazwa", "Nazwa pociągu"), ("Zamowienie", "Numer zamówienia"), ("Typ", "Typ pociągu"),
        ("RodzajRozkladu", "Rodzaj rozkładu jazdy"), ("NrWniosku", "Nr wniosku"), ("DataWniosku", "Data wniosku"), ("Wnioskodawca", "Wnioskodawca"),
        ("Rezygnacja", "Rezygnacja"), ("DataRezygnacji", "Data rezygnacji"), ("Dodatkowe", "Informacje dodatkowe"), ("UwagiPrzew", "Uwagi przewoźnika"),
        ("UwagiEksp", "Uwagi eksploatacyjne"), ("MaxCzekania", "Czas oczekiwania (np. 15 min)"), ("PrognozaSkom", "Prognoza skomunikowania"),
    };

    /// <summary>Szablony uwag jazdy po zamkniętym torze (E9c, `WorkTrip.Templates` w EDR.exe).</summary>
    public static string[] SzablonyJazdy(string tu, string sasiad) => new[]
    {
        $"Po zamkniętym torze nr [Szlakowy], z postojem/pracą na kilometrze [...]. Zjazd do [{sasiad}] na/do godz. [...].",
        $"Po zamkniętym torze nr [Szlakowy] do kilometra [...] i z powrotem. Zjazd do [{tu}] na/do godz. [...].",
        "Po zamkniętym torze nr [Szlakowy] do kilometra [...].",
        "Po zamkniętym torze nr [Szlakowy], z wjazdem w kilometrze [...] do kilometra/posterunku [...].",
    };

    /// <summary>Użytkownicy lokalni (plik ustawień), zalogowany użytkownik lokalny stanowiska, początek dyżuru posterunku (czas symulacji).</summary>
    public List<EdrUzytkownik> Uzytkownicy = new();
    readonly Dictionary<string, EdrUzytkownik> operatorStanowiska = new();
    readonly Dictionary<string, string> logowanie = new();
    readonly Dictionary<string, double> dyzurOd = new();
    /// <summary>Historia zmian EDR (zapis dzienników), dane lokalne pociągów (posterunek|klucz), usługi, pociągi lokalne.</summary>
    public List<EdrZdarzenie> Historia = new();
    public Dictionary<string, EdrPociagLokalny> Lokalne = new();
    public List<EdrUsluga> Uslugi = new();
    public List<EdrPociagLok> PociagiLok = new();

    static string Klucz(Desk k) => k?.Id ?? "";
    /// <summary>Kto pracuje w EDR na stanowisku: zalogowany użytkownik lokalny albo użytkownik symulatora.</summary>
    public string Kto(Desk k) => operatorStanowiska.TryGetValue(Klucz(k), out var o) ? o.Nazwa : k?.User?.Name ?? S.User?.Name;
    EdrUzytkownik Operator(Desk k) => operatorStanowiska.GetValueOrDefault(Klucz(k));
    /// <summary>Tryb obserwatora (użytkownik lokalny z rolą „Obserwator”) – EDR tylko do odczytu.</summary>
    public bool Obserwator(Desk k) => Operator(k)?.Rola == "Obserwator";

    /// <summary>
    /// Przyjęcie dyżuru na posterunku (E9b): w każdym dzienniku ruchu posterunku „Dyżur przyjął … dnia … o godzinie …” i wpis służbowy w dzienniku
    /// telefonicznym (jak `BeginDuty` w EDR.exe). <paramref name="tylko"/> – tylko te dzienniki (nowy dziennik, zmiana zalogowanego).
    /// </summary>
    void PrzyjmijDyzur(string post, string kto, string godz, double t, IEnumerable<EdrDziennik> tylko = null)
    {
        foreach (var d in (tylko ?? Dzienniki.Where(x => x.Post == post)).ToList())
        {
            var s = Strona(d);
            d.Prowadzi = kto; d.Zdany = false;
            Tekst(d, s, $"Dyżur przyjął {kto} dnia {s.Data} o godzinie {godz}");
        }
        dyzurOd[post] = t;
        Sluzbowy(post, kto, $"Dyżur przyjął: {kto} dnia {DataDoby(Doba)} o godzinie {godz}");
    }

    /// <summary>Wpis służbowy w dzienniku telefonicznym posterunku (bez numeru): dyżur, gotowość, ładunek – jak notatki „TEL” w EDR.exe.</summary>
    void Sluzbowy(string post, string kto, string tresc)
    {
        if (!Telefoniczny.TryGetValue(post, out var l)) Telefoniczny[post] = l = new();
        var g = Teraz();
        l.Add(new EdrTelefonogram { Rodzaj = "sluzbowy", Nr = 0, Post = post, Doba = Doba, Data = DataDoby(Doba), Godz = g, CzasZak = g, Wprowadzono = g, Nadal = kto, NadalPost = NazwaPosterunku(post), Tresc = tresc, Kto = kto, Zapisano = Znacznik() });
        brudne = true; Wersja++;
    }

    void Zapisz(Desk k, string czynnosc, string obiekt, string opis)
    {
        Historia.Add(new EdrZdarzenie { Czas = Znacznik(), Kto = Kto(k) ?? "", Czynnosc = czynnosc, Obiekt = obiekt ?? "", Opis = opis ?? "" });
        brudne = true;
    }

    /// <summary>Godzina „GG:MM” dzisiejszej doby nie późniejsza niż teraz (dyżur, gotowość) → czas symulacji; null – błędna albo z przyszłości.</summary>
    double? CzasGodziny(string g)
    {
        g = Godzina(g);
        if (g is not { Length: 5 }) return null;
        double polnoc = Math.Floor((S.StartClock + S.T) / 86400) * 86400 - S.StartClock;
        double t = polnoc + (int.Parse(g[..2]) * 60 + int.Parse(g[3..])) * 60;
        return t <= S.T + 59 ? t : null;
    }

    /// <summary>Polecenia E9: dyżur, użytkownicy lokalni i logowanie, dane lokalne pociągu, usługi, pociągi lokalne, sesja (null – nie dotyczy).</summary>
    CmdResult PolecenieE9(Desk k, EdrPolecenie p)
    {
        switch (p.Rodzaj)
        {
            case "zaloguj":
            {
                var u = Uzytkownicy.FirstOrDefault(x => string.Equals(x.Id, (p.Pole ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
                if (u == null || !u.Sprawdz(p.Wartosc)) { Zapisz(k, "Nieudane logowanie", p.Pole, ""); return new(false, "Błędny identyfikator albo hasło"); }
                operatorStanowiska[Klucz(k)] = u.Kopia(); Wersja++;
                Zapisz(k, "Logowanie do EDR", u.Id, u.Rola);
                return new(true, $"Zalogowano do EDR: {u.Nazwa} ({u.Rola}){(u.Rola == "Obserwator" ? " – TRYB OBSERWATORA, tylko odczyt" : "")}");
            }
            case "eksportsesji": return new(true, StanPlikuJson());
            case "eksportkonf":
                if (!JestPost(p.Post)) return new(false, "Nie ma takiego posterunku");
                return new(true, KonfiguracjaPosterunkuJson(p.Post));
            case "wyloguj":
            {
                if (!operatorStanowiska.Remove(Klucz(k))) return new(false, "Nie ma zalogowanego użytkownika lokalnego");
                Wersja++;
                return new(true, $"Wylogowano z EDR – pracuje {Kto(k)}");
            }
            case "uzytkownicy":
            {
                if (!Admin(k)) return new(false, "Użytkowników lokalnych zmienia administrator (koordynator)");
                var nowe = new List<EdrUzytkownik>();
                foreach (var x in p.Uzytkownicy ?? new())
                {
                    var u = new EdrUzytkownik { Id = (x.Id ?? "").Trim(), Nazwa = (x.Nazwa ?? "").Trim(), Rola = x.Rola };
                    if (u.Id == "" || u.Nazwa == "") return new(false, "Użytkownik musi mieć identyfikator i imię i nazwisko");
                    if (!EdrUzytkownik.Role.Contains(u.Rola)) return new(false, $"{u.Nazwa}: nieznana rola „{u.Rola}”");
                    if (nowe.Any(y => string.Equals(y.Id, u.Id, StringComparison.OrdinalIgnoreCase))) return new(false, $"Identyfikator {u.Id} powtarza się");
                    var stary = Uzytkownicy.FirstOrDefault(y => string.Equals(y.Id, u.Id, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(x.Haslo)) { if (x.Haslo.Length < 4) return new(false, $"{u.Nazwa}: hasło ma co najmniej 4 znaki"); u.UstawHaslo(x.Haslo); }
                    else if (stary != null) { u.Sol = stary.Sol; u.Skrot = stary.Skrot; }
                    else return new(false, $"{u.Nazwa}: podaj hasło lokalne");
                    nowe.Add(u);
                }
                Uzytkownicy = nowe; Wersja++;
                foreach (var (kk, o) in operatorStanowiska.ToList()) if (!nowe.Any(x => x.Id == o.Id)) operatorStanowiska.Remove(kk);
                Zapisz(k, "Użytkownicy lokalni", "", $"{nowe.Count} użytkowników");
                try { ZapiszUstawienia(); } catch (Exception ex) { return new(false, $"Przyjęto, ale nie zapisano w pliku: {ex.Message}"); }
                return new(true, $"Użytkownicy lokalni: {nowe.Count}");
            }
        }
        if (Obserwator(k)) return new(false, "TRYB OBSERWATORA – EDR tylko do odczytu");
        switch (p.Rodzaj)
        {
            case "dyzur":
            {
                if (!JestPost(p.Post)) return new(false, "Nie ma takiego posterunku");
                if (!MojPost(p.Post, k)) return new(false, $"Posterunek {NazwaPosterunku(p.Post)} prowadzi inne stanowisko");
                var kto = Kto(k);
                if (kto == null) return new(false, "Brak zalogowanego użytkownika");
                var g = string.IsNullOrWhiteSpace(p.Wartosc) ? Teraz() : Godzina(p.Wartosc);
                if (CzasGodziny(g) is not double t) return new(false, "Godzina w postaci GG:MM, nie późniejsza niż bieżąca");
                var dz = Dzienniki.Where(x => x.Post == p.Post).ToList();
                if (p.Pole == "przyjmij")
                {
                    PrzyjmijDyzur(p.Post, kto, g, t);
                    Zapisz(k, "Przyjęcie dyżuru", NazwaPosterunku(p.Post), g);
                    return new(true, $"Dyżur na posterunku {NazwaPosterunku(p.Post)} przyjął {kto} o {g}");
                }
                if (p.Pole == "zdaj")
                {
                    var prow = dz.Select(x => x.Prowadzi).FirstOrDefault(x => x != null);
                    if (prow == null) return new(false, "Nikt nie prowadzi dzienników tego posterunku");
                    foreach (var d in dz.Where(x => x.Prowadzi != null))
                    {
                        var s = Strona(d);
                        Tekst(d, s, $"Dyżur zdał {d.Prowadzi} dnia {s.Data} o godzinie {g}");
                        d.Prowadzi = null; d.Zdany = true;
                    }
                    dyzurOd.Remove(p.Post);
                    Sluzbowy(p.Post, kto, $"Dyżur zdał: {prow} dnia {DataDoby(Doba)} o godzinie {g}");
                    Zapisz(k, "Zdanie dyżuru", NazwaPosterunku(p.Post), g);
                    return new(true, $"Dyżur na posterunku {NazwaPosterunku(p.Post)} zdał {prow} o {g}");
                }
                return new(false, "Nieznana czynność dyżuru");
            }
            case "pociag": return PolecenieLokalne(k, p);
            case "usluga":
            case "brakzgody":
            {
                if (!JestPost(p.Post)) return new(false, "Nie ma takiego posterunku");
                if (!MojPost(p.Post, k)) return new(false, $"Posterunek {NazwaPosterunku(p.Post)} prowadzi inne stanowisko");
                if (p.Rodzaj == "brakzgody")
                {
                    var u0 = Uslugi.FirstOrDefault(x => x.Id == p.Klucz && x.Post == p.Post);
                    if (u0 == null) return new(false, "Wybierz usługę");
                    var powod = (p.Wartosc ?? "").Trim();
                    if (powod == "") return new(false, "Podaj powód");
                    u0.Status = "Brak zgody"; u0.Uwagi = (u0.Uwagi ?? "") + (u0.Uwagi != "" ? "\n" : "") + "Brak zgody: " + powod; Wersja++;
                    Zapisz(k, "Brak zgody na usługę", u0.Pociag, powod);
                    return new(true, $"Brak zgody na usługę {u0.Rodzaj.ToLowerInvariant()} pociągu {u0.Pociag}");
                }
                var u = p.Usluga?.Kopia();
                if (u == null) return new(false, "Brak danych usługi");
                u.Post = p.Post; u.Pociag = (u.Pociag ?? "").Trim(); u.Przewoznik = (u.Przewoznik ?? "").Trim(); u.Uwagi ??= ""; u.Tor ??= "";
                if (!EdrUsluga.Rodzaje.Contains(u.Rodzaj)) return new(false, "Nieznany rodzaj usługi");
                if (u.Pociag == "") return new(false, "Wpisz numer pociągu");
                if (u.Przewoznik == "") return new(false, "Wpisz przewoźnika");
                u.Od = Godzina(string.IsNullOrWhiteSpace(u.Od) ? Teraz() : u.Od); u.Do = Godzina(u.Do ?? "");
                if (u.Od is not { Length: 5 } || u.Do is null or "---") return new(false, "Rozpoczęcie i zakończenie w postaci GG:MM");
                if (u.Do != "" && string.CompareOrdinal(u.Do, u.Od) < 0) return new(false, "Zakończenie poprzedza rozpoczęcie");
                u.Status = u.Do != "" ? "Zakończono" : "W trakcie";
                Uslugi.RemoveAll(x => x.Id == u.Id); Uslugi.Add(u); Wersja++;
                Zapisz(k, "Rejestracja usługi", u.Pociag, $"{u.Rodzaj}, tor {u.Tor}, {u.Od}–{u.Do}");
                return new(true, $"Usługa {u.Rodzaj.ToLowerInvariant()} pociągu {u.Pociag} zarejestrowana");
            }
            case "lokalny":
            case "usunlokalny":
            case "importcsv":
            {
                if (!JestPost(p.Post)) return new(false, "Nie ma takiego posterunku");
                if (!MojPost(p.Post, k)) return new(false, $"Posterunek {NazwaPosterunku(p.Post)} prowadzi inne stanowisko");
                if (p.Rodzaj == "usunlokalny")
                {
                    if (PociagiLok.RemoveAll(x => x.Id == p.Klucz && x.Post == p.Post) == 0) return new(false, "Nie ma takiego pociągu lokalnego");
                    Wersja++; Zapisz(k, "Usunięcie danych lokalnych", p.Klucz, "");
                    return new(true, "Pociąg lokalny usunięty z wykazu");
                }
                var lista = p.Rodzaj == "importcsv" ? p.PociagiLok ?? new() : new List<EdrPociagLok> { p.PociagLok };
                var gotowe = new List<EdrPociagLok>();
                foreach (var x0 in lista)
                {
                    if (x0 == null) return new(false, "Brak danych pociągu");
                    var x = x0.Kopia(); x.Post = p.Post;
                    foreach (var f in new[] { "Nr", "Rodzaj", "Przewoznik", "ZKierunku", "WKierunku", "Tor", "Uwagi" })
                        typeof(EdrPociagLok).GetField(f).SetValue(x, ((string)typeof(EdrPociagLok).GetField(f).GetValue(x) ?? "").Trim());
                    if (x.Nr == "") return new(false, "Pusty numer pociągu");
                    x.Przyjazd = Godzina(x.Przyjazd ?? ""); x.Odjazd = Godzina(x.Odjazd ?? "");
                    if (x.Przyjazd is null or "---" || x.Odjazd is null or "---") return new(false, $"Pociąg {x.Nr}: godziny w postaci GG:MM");
                    if (x.Przyjazd == "" && x.Odjazd == "") return new(false, $"Pociąg {x.Nr}: podaj przyjazd albo odjazd");
                    if (gotowe.Concat(PociagiLok).Any(y => y.Post == p.Post && y.Id != x.Id && y.Nr == x.Nr && (y.Przyjazd != "" ? y.Przyjazd : y.Odjazd) == (x.Przyjazd != "" ? x.Przyjazd : x.Odjazd)))
                        return new(false, $"Powtórzony pociąg {x.Nr} o tej samej godzinie");
                    gotowe.Add(x);
                }
                foreach (var x in gotowe) { PociagiLok.RemoveAll(y => y.Id == x.Id); PociagiLok.Add(x); }
                Wersja++;
                Zapisz(k, p.Rodzaj == "importcsv" ? "Import rozkładu z CSV" : "Dane lokalne pociągu", NazwaPosterunku(p.Post), string.Join(", ", gotowe.Select(x => x.Nr)));
                return new(true, p.Rodzaj == "importcsv" ? $"Zaimportowano pociągów: {gotowe.Count}" : $"Pociąg lokalny {gotowe[0].Nr} zapisany w wykazie");
            }
            case "nowasesja":
            {
                // nowa sesja ćwiczeniowa (jak w EDR.exe): dzienniki bieżących posterunków stanowiska od nowa, wcześniejsze strony – tylko w pliku kopii
                var moje = Dzienniki.Where(d => Moj(d, k)).ToList();
                foreach (var d in moje) { d.Strony.Clear(); d.Prowadzi = null; d.Zdany = false; }
                foreach (var pk in moje.Select(d => d.Post).Distinct()) { Telefoniczny.Remove(pk); dyzurOd.Remove(pk); }
                ZajetosciReczne.RemoveAll(x => moje.Any(d => d.Post == x.Post));
                foreach (var kk in logowanie.Keys.ToList()) logowanie.Remove(kk);
                Wersja++;
                Zapisz(k, "Nowa sesja ćwiczeniowa", "", $"{moje.Count} dzienników");
                Aktualizuj(k);
                return new(true, $"Nowa sesja ćwiczeniowa – dzienniki od nowa ({moje.Count})");
            }
        }
        return null;
    }

    /// <summary>Dane lokalne pociągu (polecenie „pociag”, Pole – czynność): STÓJ, prognoza, godziny, gotowość, analiza, ładunek, opis, wiersze zakładek.</summary>
    CmdResult PolecenieLokalne(Desk k, EdrPolecenie p)
    {
        if (!JestPost(p.Post)) return new(false, "Nie ma takiego posterunku");
        if (!MojPost(p.Post, k)) return new(false, $"Posterunek {NazwaPosterunku(p.Post)} prowadzi inne stanowisko");
        if (string.IsNullOrEmpty(p.Klucz)) return new(false, "Wybierz pociąg");
        var kto = Kto(k);
        var nr = (p.Wartosc ?? "").Trim();
        var klucz = p.Post + "|" + p.Klucz;
        var l = Lokalne.TryGetValue(klucz, out var st) ? st.Kopia() : new EdrPociagLokalny();
        var d = p.Lok ?? new EdrPociagLokalny();
        string czyn, opis;
        string G(string v, string nazwa, bool wym)
        {
            var g = Godzina(v ?? "");
            if (g == null || g == "---" || wym && g == "") throw new ArgumentException($"{nazwa}: godzina w postaci GG:MM");
            return g;
        }
        try
        {
            switch (p.Pole)
            {
                case "stoj":
                    l.Stoj = true; l.StojOd = Teraz();
                    czyn = "Postój pociągu (pociąg STÓJ)"; opis = $"od {l.StojOd}";
                    Sluzbowy(p.Post, kto, $"Pociąg {nr}: wprowadzono postój (POCIĄG STÓJ) o godz. {l.StojOd}.");
                    break;
                case "odwolajstoj":
                    if (!l.Stoj) return new(false, $"Pociąg {nr} nie ma postoju (pociąg STÓJ)");
                    l.Stoj = false; l.StojOd = ""; l.PrognozaPrz = l.PrognozaOdj = "";
                    czyn = "Odwołanie postoju pociągu"; opis = "";
                    Sluzbowy(p.Post, kto, $"Pociąg {nr}: odwołano postój (POCIĄG STÓJ) o godz. {Teraz()}.");
                    break;
                case "prognoza":
                {
                    var a = G(d.PrognozaPrz, "Przyjazd", false); var o = G(d.PrognozaOdj, "Odjazd", false);
                    if (a == "" && o == "") return new(false, "Podaj prognozę przyjazdu albo odjazdu");
                    if (a != "" && o != "" && string.CompareOrdinal(a, o) > 0 && !(string.CompareOrdinal(a, "18:00") >= 0 && string.CompareOrdinal(o, "06:00") < 0)) return new(false, "Przyjazd nie może być późniejszy od odjazdu.");
                    l.PrognozaPrz = a; l.PrognozaOdj = o;
                    czyn = "Prognoza pociągu"; opis = $"przyjazd {a}, odjazd {o}";
                    break;
                }
                case "czasy":
                {
                    var a = G(d.CzasPrz, "Przyjazd", false); var o = G(d.CzasOdj, "Odjazd", false);
                    if (a != "" && o != "" && string.CompareOrdinal(a, o) > 0 && !(string.CompareOrdinal(a, "18:00") >= 0 && string.CompareOrdinal(o, "06:00") < 0)) return new(false, "Przyjazd nie może być późniejszy od odjazdu.");
                    l.CzasPrz = a; l.CzasOdj = o; l.Przyczyna = (d.Przyczyna ?? "").Trim();
                    czyn = "Rzeczywisty czas przyjazdu i odjazdu"; opis = $"przyjazd {a}, odjazd {o}" + (l.Przyczyna != "" ? $" ({l.Przyczyna})" : "");
                    break;
                }
                case "gotowosc":
                {
                    var g = G(d.GotowoscZgl, "Gotowość zgłoszono", true);
                    var m = (d.Maszynista ?? "").Trim();
                    if (m == "") return new(false, "Wpisz nazwisko maszynisty");
                    l.GotowoscZgl = g; l.Maszynista = m; l.GotowoscWpis = Teraz();
                    czyn = "Gotowość pociągu"; opis = $"{g}, maszynista {m}";
                    Sluzbowy(p.Post, kto, $"Pociąg {nr}: zgłoszono gotowość o godz. {g}. Maszynista: {m}");
                    break;
                }
                case "analiza":
                {
                    static string Liczba(string v, string n)
                    {
                        v = (v ?? "").Trim().Replace(',', '.');
                        if (v == "") return "";
                        if (!double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) || x <= 0) throw new ArgumentException($"{n}: liczba dodatnia");
                        return x.ToString("0.#", CultureInfo.InvariantCulture);
                    }
                    l.Brutto = Liczba(d.Brutto, "Brutto składu"); l.Dlugosc = Liczba(d.Dlugosc, "Długość pociągu");
                    l.Serie = (d.Serie ?? new()).Take(3).Select(x => (x ?? "").Trim()).ToList(); l.NumeryLok = (d.NumeryLok ?? new()).Take(3).Select(x => (x ?? "").Trim()).ToList();
                    czyn = "Analiza pociągu"; opis = $"brutto {l.Brutto} t, długość {l.Dlugosc} m, lokomotywy: {string.Join(", ", l.Serie.Zip(l.NumeryLok, (a, b) => (a + " " + b).Trim()).Where(x => x != ""))}";
                    break;
                }
                case "ladunek":
                {
                    if (l.TN != d.TN)
                        l.Wiersze.Add(new EdrWierszLok { Rodzaj = "tn", Komorki = new() { Znacznik(), kto ?? "", NazwaPosterunku(p.Post), d.TN ? "Wprowadzono oznaczenie TN" : "Usunięto oznaczenie TN" } });
                    l.TN = d.TN; l.TWR = d.TWR; l.PN = d.PN; l.Ladunek = (d.Ladunek ?? "").Trim();
                    czyn = "Dane ładunku"; opis = $"TN={(l.TN ? "tak" : "nie")}, TWR={(l.TWR ? "tak" : "nie")}, PN={(l.PN ? "tak" : "nie")}. {l.Ladunek}".Trim();
                    Sluzbowy(p.Post, kto, $"Pociąg {nr}: {opis}");
                    break;
                }
                case "opis":
                    l.Opis = (d.Opis ?? new()).Where(x => PolaOpisu.Any(y => y.Id == x.Key) && !string.IsNullOrWhiteSpace(x.Value)).ToDictionary(x => x.Key, x => x.Value.Trim());
                    l.Skrajnia = d.Skrajnia; l.Nacisk = d.Nacisk; l.Zamknieciowy = d.Zamknieciowy; l.Sledzony = d.Sledzony;
                    czyn = "Lokalny opis pociągu"; opis = string.Join("; ", l.Opis.Select(x => $"{x.Key}: {x.Value}"));
                    break;
                case "wiersz":
                {
                    var w = p.WierszLok?.Kopia();
                    if (w == null || !Tabele.Any(x => x.Id == w.Rodzaj) || w.Rodzaj == "tn") return new(false, "Nieznana tabela");
                    var kol = Tabele.First(x => x.Id == w.Rodzaj).Kolumny.Length;
                    w.Komorki = (w.Komorki ?? new()).Select(x => (x ?? "").Trim()).Concat(Enumerable.Repeat("", kol)).Take(kol).ToList();
                    if (w.Komorki.All(x => x == "")) return new(false, "Wprowadź dane w co najmniej jednej kolumnie.");
                    w.Zalacznik = (w.Zalacznik ?? "").Trim();
                    if (w.Zalacznik != "" && !w.Zalacznik.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) return new(false, "Wskaż plik PDF telegramu.");
                    l.Wiersze.RemoveAll(x => x.Id == w.Id); l.Wiersze.Add(w);
                    czyn = "Dane lokalne – " + Tabele.First(x => x.Id == w.Rodzaj).Tytul; opis = string.Join(" | ", w.Komorki);
                    break;
                }
                case "usunwiersz":
                    if (l.Wiersze.RemoveAll(x => x.Id == p.Pole2 && x.Rodzaj != "tn") == 0) return new(false, "Nie ma takiego wiersza");
                    czyn = "Usunięcie danych lokalnych"; opis = p.Pole2;
                    break;
                default: return new(false, "Nieznana czynność");
            }
        }
        catch (ArgumentException ex) { return new(false, ex.Message); }
        Lokalne[klucz] = l; Wersja++;
        Zapisz(k, czyn, $"pociąg {nr}", opis);
        return new(true, $"{czyn} – pociąg {nr}");
    }

    /// <summary>
    /// Wczytanie sesji z dziennikami (plik zapisany przez „Zapisz sesję z dziennikami…” – jak plik ciągłej historii) i konfiguracji posterunku
    /// (plik „Zapisz konfigurację posterunku…”: posterunek z dziennikami, ustawienia zasadnicze, sprawdzenia, numeracja).
    /// </summary>
    CmdResult PolecenieSesji(Desk k, EdrPolecenie p)
    {
        try
        {
            if (p.Rodzaj == "wczytajsesje")
            {
                if (!Admin(k) && Dzienniki.Any(d => !Moj(d, k))) return new(false, "Sesję wczytuje administrator albo stanowisko prowadzące wszystkie posterunki");
                var f = System.Text.Json.JsonSerializer.Deserialize<PlikDz>(p.Plik ?? "", JsonOpt);
                ZastosujPlik(f, false);
                foreach (var d in Dzienniki) StanSzlaku(d);
                return new(true, $"Wczytano sesję z dziennikami: {f.Dzienniki.Count(z => Dzienniki.Any(d => d.Id == z.Id))} dzienników (wpisy jako historia)");
            }
            if (!Admin(k)) return new(false, "Konfigurację posterunku wczytuje administrator (koordynator)");
            var kp = System.Text.Json.JsonSerializer.Deserialize<EdrKonfPosterunku>(p.Plik ?? "", JsonOpt);
            if (kp?.Posterunek == null || string.IsNullOrWhiteSpace(kp.Posterunek.Kod)) return new(false, "Plik nie zawiera konfiguracji posterunku");
            var l = Posterunki.Select(x => x.Kopia()).ToList();
            l.RemoveAll(x => x.Kod == kp.Posterunek.Kod); l.Add(kp.Posterunek.Kopia());
            if (SprawdzPosterunki(l) is string blad) return new(false, blad);
            posterunkiUst = l; BladKonfiguracji = null;
            ZbudujDzienniki();
            foreach (var (id, u) in kp.Ustawienia ?? new())
                if (Dzienniki.FirstOrDefault(d => d.Id == id) is EdrDziennik d && Sprawdz(d, u) == null) Przyjmij(d, u);
            if (kp.Sprawdzenia != null && SprawdzSprawdzenia(kp.Sprawdzenia) == null) Sprawdzenia[kp.Posterunek.Kod] = kp.Sprawdzenia;
            if (Numeracje.Contains(kp.Numeracja ?? "")) Numeracja[kp.Posterunek.Kod] = kp.Numeracja;
            foreach (var d in Dzienniki.Where(x => x.Post == kp.Posterunek.Kod && x.Prowadzi == null)) Strona(d);
            ZapiszUstawienia();
            return new(true, $"Wczytano konfigurację posterunku {NazwaPosterunku(kp.Posterunek.Kod)}: {kp.Posterunek.Dzienniki.Count} dzienników");
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidDataException or NotSupportedException) { return new(false, "Błędny plik: " + ex.Message); }
    }

    /// <summary>Konfiguracja posterunku do pliku (E9e): posterunek z dziennikami, ustawienia zasadnicze dzienników, sprawdzenia, numeracja.</summary>
    public string KonfiguracjaPosterunkuJson(string post) => System.Text.Json.JsonSerializer.Serialize(new EdrKonfPosterunku
    {
        Posterunek = Posterunki.FirstOrDefault(x => x.Kod == post)?.Kopia(),
        Ustawienia = Dzienniki.Where(d => d.Post == post).ToDictionary(d => d.Id, d => d.Ustawienia),
        Sprawdzenia = Sprawdzenia.GetValueOrDefault(post), Numeracja = Numeracja.GetValueOrDefault(post, "dobowa")
    }, JsonOpt);

    /// <summary>Opis polecenia do historii zmian (polecenia dziennika ruchu).</summary>
    string OpisPolecenia(EdrPolecenie p, CmdResult r)
    {
        var d = p.Dziennik != null ? Dzienniki.FirstOrDefault(x => x.Id == p.Dziennik) : null;
        return p.Rodzaj switch
        {
            "pole" => $"{NazwaPola(p.Pole ?? "")}: {(string.IsNullOrEmpty(p.Wartosc) ? "(puste)" : p.Wartosc)} (wiersz {p.Wiersz})",
            _ => r.Msg ?? ""
        } + (d != null && p.Rodzaj != "pole" ? "" : "");
    }

    static readonly Dictionary<string, string> Czynnosci = new()
    {
        ["przyj"] = "Przyjęcie pociągu", ["wypr"] = "Wyprawienie pociągu", ["pole"] = "Zmiana wpisu", ["wykresl"] = "Wykreślenie wpisu", ["ustawienia"] = "Ustawienia zasadnicze dziennika",
        ["wpis"] = "Wpis o dowolnej treści", ["telefonogram"] = "Telefonogram", ["torst"] = "Zajętość toru stacyjnego", ["sprawdzenia"] = "Wymagane sprawdzenia", ["sprawdzenie"] = "Wykonanie sprawdzenia",
        ["numeracja"] = "Numeracja telefonogramów", ["posterunki"] = "Posterunki ruchu i dzienniki", ["posterunkizukladu"] = "Posterunki ruchu z układu", ["bezwypadku"] = "Czas bez wypadku",
        ["uwagi"] = "Uwagi pociągu", ["kursuje"] = "Kursuje / nie kursuje", ["potwierdz"] = "Potwierdzenie czasu przez dyspozytora", ["wczytajsesje"] = "Wczytanie sesji z dziennikami",
        ["konfpost"] = "Wczytanie konfiguracji posterunku",
    };

    /// <summary>Wiersz historii po udanym poleceniu dziennika (polecenia E9 zapisują się same).</summary>
    void ZapiszPolecenie(Desk k, EdrPolecenie p, CmdResult r)
    {
        if (p == null || !r.Ok || !Czynnosci.TryGetValue(p.Rodzaj ?? "", out var cz)) return;
        var d = p.Dziennik != null ? Dzienniki.FirstOrDefault(x => x.Id == p.Dziennik) : null;
        Zapisz(k, cz, d?.Nazwa ?? (p.Post != null ? NazwaPosterunku(p.Post) : p.Wartosc != null && JestPost(p.Wartosc) ? NazwaPosterunku(p.Wartosc) : ""), OpisPolecenia(p, r));
    }

    /// <summary>Pociągi lokalne posterunku jako wiersze wykazu (bez ruchu w symulacji).</summary>
    IEnumerable<EdrPociag> WykazLokalny(string post)
    {
        double? T(string g) => g is { Length: 5 } ? CzasDoby(g) : null;
        foreach (var x in PociagiLok.Where(x => x.Post == post))
        {
            double? a = T(x.Przyjazd), o = T(x.Odjazd);
            yield return new EdrPociag
            {
                Klucz = "lok:" + x.Id, Nr = x.Nr, NrWyj = x.Nr, Rodz = x.Rodzaj, Nazwa = "", ZKierunku = x.ZKierunku, WKierunku = x.WKierunku, Linia = "", Tor = x.Tor, TorRzecz = "",
                PrzPlan = x.Przyjazd, PrzRzecz = x.Przyjazd, OdjPlan = x.Odjazd, OdjRzecz = x.Odjazd, PrzOdch = "", OdjOdch = "", Postoj = a != null && o != null && o - a >= 30 ? "ph" : "",
                PT = x.Tor, Stan = "lokalny", PZam = Minuty(a, o) is var pz && pz != "" ? pz : "0,0", PObl = "0,0", Wstega = "", Potw = "", Kursuje = true,
                StacjaPocz = "", StacjaKonc = "", Przewoznik = x.Przewoznik, UwagiEksp = "", UwagiPrzew = "", UwagiWlasne = x.Uwagi, Kod = "", Pojazd = "", Dlugosc = "",
                PrzPlanD = HmD(a), PrzRzeczD = HmD(a), OdjPlanD = HmD(o), OdjRzeczD = HmD(o), Sort = a ?? o ?? 0, Lokalny = true
            };
        }
    }

    /// <summary>Godzina dzisiejszej doby (GG:MM) → czas symulacji.</summary>
    double CzasDoby(string g) => Math.Floor((S.StartClock + S.T) / 86400) * 86400 - S.StartClock + (int.Parse(g[..2]) * 60 + int.Parse(g[3..])) * 60;

    /// <summary>Dane lokalne pociągu w wierszu wykazu: STÓJ, prognoza, godziny z EDR (potwierdzone przez człowieka), gotowość, TN / TWR / PN.</summary>
    void NalozLokalne(string post, EdrPociag x)
    {
        if (!Lokalne.TryGetValue(post + "|" + x.Klucz, out var l)) return;
        x.Stoj = l.Stoj; x.Prognoza = l.PrognozaPrz != "" || l.PrognozaOdj != "";
        x.TN = l.TN; x.TWR = l.TWR; x.PN = l.PN; x.Gotowy = l.GotowoscZgl != "";
        if (l.PrognozaPrz != "" && x.PrzPlan != "") { x.PrzRzecz = l.PrognozaPrz; x.PrzRzeczD = x.PrzPlanD.Length > 6 ? x.PrzPlanD[..6] + l.PrognozaPrz : l.PrognozaPrz; }
        if (l.PrognozaOdj != "" && x.OdjPlan != "") { x.OdjRzecz = l.PrognozaOdj; x.OdjRzeczD = x.OdjPlanD.Length > 6 ? x.OdjPlanD[..6] + l.PrognozaOdj : l.PrognozaOdj; }
        if (l.CzasPrz != "" && x.PrzPlan != "") { x.PrzRzecz = l.CzasPrz; x.PrzRzeczD = x.PrzPlanD.Length > 6 ? x.PrzPlanD[..6] + l.CzasPrz : l.CzasPrz; x.Potw = "czlowiek"; }
        if (l.CzasOdj != "" && x.OdjPlan != "") { x.OdjRzecz = l.CzasOdj; x.OdjRzeczD = x.OdjPlanD.Length > 6 ? x.OdjPlanD[..6] + l.CzasOdj : l.CzasOdj; x.Potw = "czlowiek"; }
        x.PrzOdch = OdchHm(x.PrzPlan, x.PrzRzecz); x.OdjOdch = OdchHm(x.OdjPlan, x.OdjRzecz);
        if (l.Opis.TryGetValue("UwagiPrzew", out var up) && string.IsNullOrEmpty(x.UwagiPrzew)) x.UwagiPrzew = up;
        if (l.Opis.TryGetValue("UwagiEksp", out var ue) && string.IsNullOrEmpty(x.UwagiEksp)) x.UwagiEksp = ue;
    }

    static string OdchHm(string plan, string rzecz)
    {
        if (plan is not { Length: 5 } || rzecz is not { Length: 5 }) return "";
        int m = (int.Parse(rzecz[..2]) * 60 + int.Parse(rzecz[3..])) - (int.Parse(plan[..2]) * 60 + int.Parse(plan[3..]));
        if (m < -720) m += 1440; else if (m > 720) m -= 1440;
        return m > 0 ? "+" + m : m.ToString();
    }
}

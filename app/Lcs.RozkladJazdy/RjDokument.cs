using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lcs.RozkladJazdy;

/// <summary>
/// Rozkład jazdy układu – plik <c>&lt;układ&gt;.rj.json</c> obok pliku układu (PLAN_EDYTOR_RJ.md § 3, etap R1). Wspólny dla symulatora
/// i edytora rozkładu jazdy. Pociąg ma odcinki (numer, kategoria wg Ir-11, przewoźnik) i bieg przez posterunki układu; punkt biegu
/// wskazuje odcinek, z którym pociąg z niego odjeżdża – zmiana numeru albo kategorii jest w punkcie.
/// Czasy „GG:MM:SS” (godziny mogą przekraczać 23 – następna doba): w rozkładzie dobowym – czas doby, we względnym – od początku scenariusza.
/// </summary>
public class RjDokument
{
    public const string Rozszerzenie = ".rj.json";
    public int Wersja = 1;
    /// <summary>Dzień rozkładu (rrrr-mm-dd) i źródło (import, poprawki ręczne).</summary>
    public string Dzien, Zrodlo;
    /// <summary>Czasy od początku scenariusza („Ruch podstawowy”) zamiast czasu doby (scenariusze rozkładu rzeczywistego).</summary>
    public bool Wzgledny;
    public List<RjPrzewoznik> Przewoznicy = new();
    /// <summary>Tory odcinka zaplecza, z których kolejno wyjeżdżają jednostki zaczynające bieg w zapleczu (<see cref="RjStart.ZZaplecza"/>).</summary>
    public List<RjTorZaplecza> ToryZaplecza = new();
    public List<RjPociag> Pociagi = new();

    public static string PlikDla(string stationPath) => stationPath == null ? null : Path.Combine(Path.GetDirectoryName(stationPath) ?? "", Path.GetFileNameWithoutExtension(stationPath) + Rozszerzenie);

    public static readonly JsonSerializerOptions Opcje = new()
    {
        IncludeFields = true, WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true,
    };

    public static RjDokument Parsuj(string json)
    {
        var d = JsonSerializer.Deserialize<RjDokument>(json, Opcje) ?? throw new InvalidDataException("pusty plik rozkładu");
        if (d.Wersja != 1) throw new InvalidDataException($"nieobsługiwana wersja rozkładu {d.Wersja}");
        d.Przewoznicy ??= new(); d.ToryZaplecza ??= new(); d.Pociagi ??= new();
        foreach (var p in d.Pociagi) { p.Odcinki ??= new(); p.Bieg ??= new(); }
        return d;
    }

    public string Json() => JsonSerializer.Serialize(this, Opcje);

    /// <summary>Zapis: plik tymczasowy, potem podmiana z kopią „.bak” (przerwany zapis nie psuje pliku).</summary>
    public void Zapisz(string plik)
    {
        var tmp = plik + ".tmp";
        File.WriteAllText(tmp, Json());
        if (File.Exists(plik)) File.Replace(tmp, plik, plik + ".bak", true); else File.Move(tmp, plik);
    }

    public static RjDokument Wczytaj(string plik) => Parsuj(File.ReadAllText(plik));

    /// <summary>Czas „GG:MM:SS” (ułamek sekundy, gdy jest) z sekund.</summary>
    public static string Czas(double? s)
    {
        if (s is not double v) return null;
        var znak = v < 0 ? "-" : ""; v = Math.Abs(v);
        long cale = (long)Math.Floor(v); double ulamek = v - cale;
        var t = $"{znak}{cale / 3600:00}:{cale / 60 % 60:00}:{cale % 60:00}";
        return ulamek > 0 ? t + ulamek.ToString(".######", CultureInfo.InvariantCulture) : t;
    }

    /// <summary>Sekundy z „GG:MM:SS” albo „GG:MM” (null – puste albo błędne).</summary>
    public static double? Sekundy(string t)
    {
        t = (t ?? "").Trim();
        if (t == "") return null;
        double znak = 1; if (t.StartsWith('-')) { znak = -1; t = t[1..]; }
        var c = t.Split(':');
        if (c.Length is < 2 or > 3 || !int.TryParse(c[0], out var h) || !int.TryParse(c[1], out var m) || m is < 0 or > 59) return null;
        double s = 0;
        if (c.Length == 3 && (!double.TryParse(c[2], NumberStyles.Float, CultureInfo.InvariantCulture, out s) || s is < 0 or >= 60)) return null;
        return znak * (h * 3600 + m * 60 + s);
    }

    /// <summary>Błędy dokumentu (puste – poprawny): pociąg bez numeru, odcinek spoza listy, błędny czas, powtórzone id.</summary>
    public List<string> Sprawdz()
    {
        var b = new List<string>();
        foreach (var g in Pociagi.GroupBy(p => p.Id).Where(g => g.Key != null && g.Count() > 1)) b.Add($"powtórzone id pociągu {g.Key}");
        foreach (var p in Pociagi)
        {
            var n = $"pociąg {p.Id ?? p.Odcinki.FirstOrDefault()?.Numer}";
            if (p.Odcinki.Count == 0 || p.Odcinki.Any(o => string.IsNullOrWhiteSpace(o.Numer))) b.Add($"{n}: brak numeru");
            if (Sekundy(p.Pojawia) == null) b.Add($"{n}: błędny czas pojawienia się „{p.Pojawia}”");
            foreach (var (x, i) in p.Bieg.Select((x, i) => (x, i)))
            {
                if (x.Odcinek < 0 || x.Odcinek >= p.Odcinki.Count) b.Add($"{n}: punkt {i + 1} – odcinek {x.Odcinek} spoza listy");
                if (x.Przyjazd != null && Sekundy(x.Przyjazd) == null || x.Odjazd != null && Sekundy(x.Odjazd) == null) b.Add($"{n}: punkt {i + 1} – błędny czas");
            }
            if (p.Start == null && p.SzlakZ == null) b.Add($"{n}: brak szlaku wjazdu i toru startu");
            if (p.Rodzic != null && !Pociagi.Any(x => x.Id == p.Rodzic)) b.Add($"{n}: nie ma pociągu {p.Rodzic} (rodzic)");
        }
        return b;
    }
}

/// <summary>Tor zaplecza: odcinek układu i numer toru (do opisu).</summary>
public class RjTorZaplecza { public string Odcinek, Tor; }

/// <summary>Przewoźnik: kod (jak w rozkładzie, np. IC, ŁKA) i pełna nazwa.</summary>
public class RjPrzewoznik { public string Kod, Nazwa; }

/// <summary>Pociąg: opis (karta pociągu), odcinki, bieg przez posterunki, początek i koniec biegu w obszarze.</summary>
public class RjPociag
{
    public string Id, Nazwa, NrMiedzynarodowy, Zamowienie, RodzajRozkladu, Pojazd, Sklad, RelacjaOd, RelacjaDo, UwagiEksp, UwagiPrzew;
    public double? Dlugosc;
    /// <summary>Opis do podpowiedzi w symulatorze (plan biegu w skrócie).</summary>
    public string Opis;
    /// <summary>Kiedy pociąg wchodzi do scenariusza (wybór pociągów okna czasu; wjazd ze szlaku).</summary>
    public string Pojawia;
    /// <summary>Szlak wjazdu (od posterunku sąsiedniego spoza obszaru) i wyjazdu (null – kończy bieg w obszarze).</summary>
    public string SzlakZ, SzlakDo;
    /// <summary>Odjazd (np. z toru startu albo pierwszego posterunku).</summary>
    public string Odjazd;
    /// <summary>Plik układu: postój na każdym peronie (bez listy posterunków).</summary>
    public bool PostojWszedzie;
    public RjStart Start;
    public RjKoniec Koniec;
    /// <summary>Pociąg towarzyszący (np. zjazd luzem lokomotywy): w scenariuszu tylko razem z tym pociągiem.</summary>
    public string Rodzic;
    public List<RjOdcinek> Odcinki = new();
    public List<RjPunkt> Bieg = new();
}

/// <summary>Odcinek biegu: numer, kategoria wg Ir-11, kategoria ruchu w symulatorze (ŁKA, IC, PRÓŻ, LUZ…), przewoźnik, uwagi.</summary>
public class RjOdcinek { public string Numer, Kat, KatRuchu, Przewoznik, Uwagi; }

/// <summary>
/// Punkt biegu: posterunek układu, przyjazd i odjazd, postój, tor, peron, odcinek odjazdu, zmiana czoła; <see cref="KatRuchu"/> – zmiana kategorii
/// ruchu od tego punktu (null – bez zmiany).
/// </summary>
public class RjPunkt
{
    public string Post, Przyjazd, Odjazd, Tor, Peron, KatRuchu;
    public bool Postoj, ZmianaCzola;
    public int Odcinek;
}

/// <summary>
/// Pociąg zaczyna bieg na torze: odcinek układu, kierunek, lokomotywa manewrowa; z zaplecza – jednostka (numer manewrowy) wyjeżdża z kolejnego toru
/// zaplecza i staje się pociągiem (kierunek odjazdu, opis z „{tor}”).
/// </summary>
public class RjStart
{
    public string Odcinek, Pojawia, NrJednostki, OpisZaplecza, TorOdjazdu;
    public int? Kierunek;
    public bool Lok, ZZaplecza;
    public int KierunekOdjazdu;
}

/// <summary>Koniec biegu w obszarze: odstawienie na posterunku (numer manewrowy), przed nim – zmiana czoła i dojazd do odstawienia.</summary>
public class RjKoniec
{
    public string Post, NrManewrowy, ZmianaCzolaW, KatRuchu;
    public double Po;
}

using System.Text.RegularExpressions;
using Serilog.Core;

namespace Lcs.Logi;

/// <summary>Profil limitów z 25 § 6 (budżety B02/B03 kolejki, B06/B07 plików).</summary>
public enum ProfilLogow
{
    /// <summary>Klient, edytor, lokalny singleplayer: kolejka 1024 rekordy / 2 MiB, plik 32 MiB, 8 plików, 7 dni, %LocalAppData%\SUK\&lt;aplikacja&gt;\Logs.</summary>
    Klient,
    /// <summary>Proces świata/bramy: kolejka 4096 rekordów / 8 MiB, plik 64 MiB, 16 plików, 7 dni, %ProgramData%\SUK\Logs.</summary>
    Serwer,
}

/// <summary>Ustawienia silnika logów. Wartości domyślne pochodzą z profilu; jawne ustawienia służą testom i pomiarom.</summary>
public sealed class OpcjeLogow
{
    static readonly Regex AplikacjaWzor = new(@"^[A-Za-z][A-Za-z0-9._-]{0,31}$", RegexOptions.CultureInvariant);

    /// <summary>Nazwa aplikacji – prefiks pliku i katalogu domyślnego (np. LcsWidzew). Bez ścieżek i spacji.</summary>
    public string Aplikacja { get; set; }
    /// <summary>Katalog plików; null = domyślny dla profilu (poza katalogiem programu i projektów).</summary>
    public string Katalog { get; set; }
    public ProfilLogow Profil { get; set; } = ProfilLogow.Klient;
    /// <summary>Minimalny poziom (Debug/Trace domyślnie wyłączone – 25 § 7).</summary>
    public Microsoft.Extensions.Logging.LogLevel MinimalnyPoziom { get; set; } = Microsoft.Extensions.Logging.LogLevel.Information;

    public int? MaxRekordowKolejki { get; set; }
    public long? MaxBajtowKolejki { get; set; }
    public long? MaxBajtowPliku { get; set; }
    public int? MaxPlikow { get; set; }
    public TimeSpan? MaxWiekPliku { get; set; }

    /// <summary>B04: najdłuższe grupowanie zapisu przed opróżnieniem bufora pliku.</summary>
    public TimeSpan OkresGrupowania { get; set; } = TimeSpan.FromSeconds(1);
    /// <summary>B04: limit opróżnienia kolejki przy zamknięciu.</summary>
    public TimeSpan LimitZamkniecia { get; set; } = TimeSpan.FromSeconds(5);
    /// <summary>B08: co ile worker sprawdza wolne miejsce na dysku z logami.</summary>
    public TimeSpan OkresNadzoruMiejsca { get; set; } = TimeSpan.FromSeconds(30);
    public double ProgOstrzezeniaProcent { get; set; } = 15;
    public double ProgOgraniczeniaProcent { get; set; } = 10;

    /// <summary>Test: zamiast pliku – podany sink (np. blokujący, żeby sprawdzić przepełnienie kolejki).</summary>
    internal ILogEventSink ZapisTestowy { get; set; }
    /// <summary>Test: źródło wolnego/całkowitego miejsca zamiast DriveInfo.</summary>
    internal Func<(long Wolne, long Calkowite)> WolneMiejsce { get; set; }

    internal int RekordyKolejki => MaxRekordowKolejki ?? (Profil == ProfilLogow.Serwer ? 4096 : 1024);
    internal long BajtyKolejki => MaxBajtowKolejki ?? (Profil == ProfilLogow.Serwer ? 8L << 20 : 2L << 20);
    internal long BajtyPliku => MaxBajtowPliku ?? (Profil == ProfilLogow.Serwer ? 64L << 20 : 32L << 20);
    internal int Pliki => MaxPlikow ?? (Profil == ProfilLogow.Serwer ? 16 : 8);
    internal TimeSpan WiekPliku => MaxWiekPliku ?? TimeSpan.FromDays(7);

    internal void Sprawdz()
    {
        if (Aplikacja == null || !AplikacjaWzor.IsMatch(Aplikacja)) throw new ArgumentException("Nazwa aplikacji logów: litery, cyfry, . _ -, do 32 znaków.", nameof(Aplikacja));
        if (RekordyKolejki < 16 || BajtyKolejki < 16 * 1024) throw new ArgumentException("Za mały limit kolejki logów (min. 16 rekordów i 16 KiB).");
        if (BajtyPliku < 1024 || Pliki < 1) throw new ArgumentException("Za mały limit pliku logów.");
    }

    /// <summary>Katalog domyślny: klient %LocalAppData%\SUK\&lt;aplikacja&gt;\Logs, serwer %ProgramData%\SUK\Logs.</summary>
    internal string KatalogDocelowy()
    {
        if (!string.IsNullOrWhiteSpace(Katalog)) return Path.GetFullPath(Katalog);
        return Profil == ProfilLogow.Serwer
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData, Environment.SpecialFolderOption.DoNotVerify), "SUK", "Logs")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify), "SUK", Aplikacja, "Logs");
    }
}

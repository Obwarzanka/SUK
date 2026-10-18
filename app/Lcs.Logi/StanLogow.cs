using System.Globalization;
using System.Text;

namespace Lcs.Logi;

public enum StanZapisu { Uruchamianie, Gotowy, Niegotowy, Zatrzymany }
public enum StanMiejsca { Nieznany, Normalny, Ostrzezenie, Ograniczenie }

/// <summary>Migawka liczników i stanu zdrowia logowania – dostępna niezależnie od tego, czy plik działa (25 § 6).</summary>
public sealed class StanLogow
{
    public string ProcessRunId { get; init; }
    public string Aplikacja { get; init; }
    public ProfilLogow Profil { get; init; }
    public string Katalog { get; init; }
    public StanZapisu Zapis { get; init; }
    public string PowodNiegotowosci { get; init; }
    public StanMiejsca Miejsce { get; init; }
    public double WolneProcent { get; init; }
    /// <summary>Rekordy przyjęte do kolejki.</summary>
    public long Przyjete { get; init; }
    /// <summary>Rekordy z kolejki przekazane do sinku plikowego bez zgłoszonej awarii.</summary>
    public long PrzekazaneDoPliku { get; init; }
    /// <summary>Rekordy własne workera (start, podsumowania strat, stop) – nie przechodzą przez kolejkę wejściową.</summary>
    public long Wlasne { get; init; }
    public long BledyZapisu { get; init; }
    public long Obciete { get; init; }
    public long Zastapione { get; init; }
    public long StratyRazem { get; init; }
    public IReadOnlyDictionary<string, long> StratyWgPowodu { get; init; }
    public IReadOnlyDictionary<string, long> StratyWgModuluIPowodu { get; init; }
    public int WKolejce { get; init; }
    public int MaxWKolejce { get; init; }
    public long BajtyWKolejce { get; init; }
    public long MaxBajtowWKolejce { get; init; }
    public int LimitRekordow { get; init; }
    public long LimitBajtow { get; init; }
    public double CzasZamknieciaMs { get; init; }
    public bool ZamkniecieKompletne { get; init; }

    public override string ToString()
    {
        var c = CultureInfo.InvariantCulture;
        var b = new StringBuilder();
        void L(string k, object v) => b.Append(k).Append('=').Append(Convert.ToString(v, c)).Append('\n');
        L("processRunId", ProcessRunId); L("aplikacja", Aplikacja); L("profil", Profil); L("katalog", Katalog);
        L("zapis", Zapis); L("powod_niegotowosci", PowodNiegotowosci ?? ""); L("miejsce", Miejsce); L("wolne_procent", WolneProcent.ToString("0.0", c));
        L("przyjete", Przyjete); L("przekazane_do_pliku", PrzekazaneDoPliku); L("wlasne", Wlasne); L("bledy_zapisu", BledyZapisu);
        L("obciete", Obciete); L("zastapione", Zastapione); L("straty_razem", StratyRazem);
        foreach (var kv in StratyWgPowodu.OrderBy(k => k.Key, StringComparer.Ordinal)) L("strata." + kv.Key, kv.Value);
        foreach (var kv in StratyWgModuluIPowodu.OrderBy(k => k.Key, StringComparer.Ordinal)) L("strata_modul." + kv.Key, kv.Value);
        L("w_kolejce", WKolejce); L("max_w_kolejce", MaxWKolejce); L("limit_rekordow", LimitRekordow);
        L("bajty_w_kolejce", BajtyWKolejce); L("max_bajtow_w_kolejce", MaxBajtowWKolejce); L("limit_bajtow", LimitBajtow);
        L("czas_zamkniecia_ms", CzasZamknieciaMs.ToString("0.0", c)); L("zamkniecie_kompletne", ZamkniecieKompletne);
        return b.ToString();
    }
}

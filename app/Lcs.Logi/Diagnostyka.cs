using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog.Extensions.Logging;

namespace Lcs.Logi;

/// <summary>
/// Wspólny silnik logów aplikacji bez kontenera hosta (pulpit WPF, edytory, narzędzia). Przed <see cref="Start"/> i po
/// <see cref="Zatrzymaj"/> moduły dostają <see cref="NullLogger"/> – brak logów nie zmienia działania programu.
/// Host z kontenerem (S02) używa <see cref="RozszerzeniaHosta.DodajSukLogi"/>.
/// </summary>
public static class Diagnostyka
{
    static SilnikLogow silnik;
    static readonly object zamek = new();

    public static SilnikLogow Silnik => Volatile.Read(ref silnik);

    /// <summary>Uruchamia silnik (raz na proces). Zwraca false, gdy nie da się go uruchomić – program działa dalej bez logów.</summary>
    public static bool Start(OpcjeLogow opcje)
    {
        lock (zamek)
        {
            if (silnik != null) return true;
            try { Volatile.Write(ref silnik, SilnikLogow.Uruchom(opcje)); return true; }
            catch (Exception) { return false; }   // np. błędne opcje albo ścieżka – logi nie mogą zatrzymać programu
        }
    }

    /// <summary>Logger modułu (kategoria = moduleId); bez uruchomionego silnika – NullLogger.</summary>
    public static ILogger Modul(string moduleId, string moduleVersion) => Silnik?.Modul(moduleId, moduleVersion) ?? NullLogger.Instance;

    /// <summary>Opróżnia kolejkę w limicie B04 i zamyka pliki. Zwraca stan końcowy albo null, gdy silnik nie działał.</summary>
    public static StanLogow Zatrzymaj(TimeSpan? limit = null)
    {
        SilnikLogow s;
        lock (zamek) { s = silnik; Volatile.Write(ref silnik, null); }
        return s?.Zatrzymaj(limit);
    }
}

/// <summary>Integracja z Microsoft.Extensions.Logging hosta (Generic Host w S02): jedynym dostawcą zostaje gorący logger SUK.</summary>
public static class RozszerzeniaHosta
{
    /// <summary>Usuwa domyślnych dostawców (konsola, Debug, EventLog – 25 § 3.5) i dodaje silnik SUK.</summary>
    public static ILoggingBuilder DodajSukLogi(this ILoggingBuilder builder, SilnikLogow silnik)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(silnik);
        builder.ClearProviders();
        builder.SetMinimumLevel(LogLevel.Trace);   // poziom filtruje silnik (przełącznik poziomu i nadzór miejsca)
        builder.AddProvider(new SerilogLoggerProvider(silnik.Goracy, dispose: false));
        return builder;
    }
}

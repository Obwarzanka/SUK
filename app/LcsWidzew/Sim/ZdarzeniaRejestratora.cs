// SYSLIB1015: pola rekordu nie muszą występować w szablonie komunikatu – trafiają jako właściwości strukturalne.
#pragma warning disable SYSLIB1015
using Lcs.Logi;
using LcsWidzew.Model;
using Microsoft.Extensions.Logging;

namespace LcsWidzew.Sim;

/// <summary>
/// Kopia wpisu dziennika pulpitu w logach technicznych (REJ.EVENT_RECORDED 3601, strumień domain – 25 § 1–2). Zapisuje tylko
/// kategorię z zamkniętej listy, typ obiektu, identyfikator obiektu urządzeń/pociągu, posterunek i numer wpisu (rejId = Id w oknie
/// Zdarzenia). Bez treści wpisu (może zawierać tekst wpisany przez użytkownika, telefon, książkę), bez operatora i stanowiska.
/// Okno Zdarzenia i eksport .zdarzenia działają jak dotąd; kopia nie jest źródłem prawdy.
/// </summary>
static partial class ZdarzeniaRejestratora
{
    public const string Modul = "LcsWidzew.Rejestrator";
    public static readonly string Wersja = typeof(ZdarzeniaRejestratora).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    public static readonly DefinicjaZdarzenia Zapisano = new(3601, "REJ.EVENT_RECORDED", KontraktLogu.Strumien.Domenowy, "kategoria", "typObiektu", "rejId");

    static ZdarzeniaRejestratora() => KatalogZdarzen.Zarejestruj(Zapisano);

    /// <summary>Typy obiektów, których identyfikator może trafić do logu (urządzenia, ruch). Pozostałe (Użytkownik, Stanowisko, Łączność, Książka…) – bez identyfikatora.</summary>
    static readonly HashSet<string> TypyZIdentyfikatorem = new(StringComparer.Ordinal)
    {
        "Semafor", "Odcinek", "Pociąg", "Blokada liniowa", "Zwrotnica", "Przebieg", "Zasilanie", "Wykolejnica", "Instruktor", "Polecenie", "System",
    };
    static readonly HashSet<string> Kategorie = new(StringComparer.Ordinal) { "Zdarzenie", "Ruch", "Polecenie", "Instruktor", "Odrzucone", "Uwaga", "Telefon", "Ocena", "Alarm" };

    public static void Wpis(ILogger log, Station L, EventRecord e, string obiektSurowy)
    {
        if (!log.IsEnabled(LogLevel.Information)) return;
        try
        {
            var kat = e.Kategoria == "E-1758" ? "E1758" : Kategorie.Contains(e.Kategoria ?? "") ? e.Kategoria : "Inna";
            var typ = string.IsNullOrEmpty(e.TypObiektu) ? null : TypyZIdentyfikatorem.Contains(e.TypObiektu) ? e.TypObiektu : "Inny";
            var cel = typ != null && typ != "Inny" && !string.IsNullOrEmpty(e.Obiekt) ? e.Obiekt : null;
            var post = string.IsNullOrEmpty(obiektSurowy) ? "" : Station.PostOf(obiektSurowy);
            LogWpis(log, kat, typ, e.Id, cel, post != "" && L.Posts.ContainsKey(post) ? post : null);
        }
        catch (Exception) { }   // kopia w logach nie może przerwać cyklu symulacji ani zmienić dziennika pulpitu
    }

    [LoggerMessage(EventId = 3601, EventName = "REJ.EVENT_RECORDED", Level = LogLevel.Information, Message = "Wpis dziennika pulpitu ({kategoria})")]
    static partial void LogWpis(ILogger log, string kategoria, string typObiektu, long rejId, string targetId, string postId);
}

namespace LcsWidzew.Sim;

/// <summary>Metadane istniejącego adaptera rejestratora. Katalog i provider logów należą do hosta.</summary>
public static class CoreLogging
{
    public const string Modul = "LcsWidzew.Rejestrator";
    public static string Wersja => typeof(CoreLogging).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    public const int EventId = 3601;
    public const string EventCode = "REJ.EVENT_RECORDED";
    public static IReadOnlyList<string> PolaZdarzeniaRejestratora { get; } = Array.AsReadOnly(new[] { "kategoria", "typObiektu", "rejId" });
}

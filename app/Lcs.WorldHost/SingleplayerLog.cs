using Lcs.Logi;
using Microsoft.Extensions.Logging;

namespace Lcs.WorldHost;

public static partial class SingleplayerLog
{
    public const string Module = "Singleplayer.Session";
    public static void Register()
    {
        WorldLog.Register();
        foreach (var (id, code) in new[] { (6101, "SP.STARTED"), (6102, "SP.STOPPED"), (6103, "SP.OPERATION"), (6104, "SP.FAILED") })
            if (!KatalogZdarzen.Zarejestruj(new(id, code, KontraktLogu.Strumien.Diagnostyka, "operation")))
                throw new InvalidOperationException("Niezgodny katalog adaptera singleplayer.");
    }
    [LoggerMessage(6101, LogLevel.Information, "Uruchomiono lokalny adapter pulpitu", EventName = "SP.STARTED")]
    public static partial void Started(ILogger log);
    [LoggerMessage(6102, LogLevel.Information, "Zamknięto lokalny adapter pulpitu", EventName = "SP.STOPPED")]
    public static partial void Stopped(ILogger log);
    [LoggerMessage(6103, LogLevel.Debug, "Operacja lokalnej sesji ({operation})", EventName = "SP.OPERATION")]
    public static partial void Operation(ILogger log, string operation);
    [LoggerMessage(6104, LogLevel.Error, "Błąd lokalnego adaptera ({reasonCode})", EventName = "SP.FAILED")]
    public static partial void Failed(ILogger log, Exception exception, string reasonCode);
}

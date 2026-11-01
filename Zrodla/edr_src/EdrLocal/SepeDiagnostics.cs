using System;
using System.Text.RegularExpressions;

namespace EdrLocal;

public static class SepeDiagnostics
{
	public static string Clean(string text)
	{
		return Regex.Replace(text ?? "", "(?i)(key=)[^\\s&\"'<>]+", "$1[ukryto]");
	}

	public static string Reason(Exception error)
	{
		return Clean((error is InvalidOperationException) ? error.Message : ("Nie udało się wykonać operacji SEPE.\r\nSzczegóły: " + error.GetType().Name + ": " + error.Message));
	}

	public static string Notice(SepeWriteRequest request, string station, string reason)
	{
		return Clean("Nie zapisano godzin pociągu " + request.Number + ".\r\nPosterunek: " + station + "\r\nPrzyjazd: " + (request.ArrivalChanged ? Ui.FullTime(request.Arrival) : "bez zmiany") + "\r\nOdjazd: " + (request.DepartureChanged ? Ui.FullTime(request.Departure) : "bez zmiany") + "\r\n\r\n" + reason + "\r\n\r\nWysyłanie zostało wstrzymane. Godziny pozostają lokalnie.\r\nPo poprawieniu wpisu lub połączenia użyj SimRail → Wyślij oczekujące godziny.\r\nSzczegóły: SimRail → Ostatni błąd zapisu SEPE.");
	}

	public static string Report(SepeWriteRequest request, SimRailSession session, string reason)
	{
		return Clean("Serwer: " + session.ServerCode + "; posterunek: " + session.PointId + "\r\nPociąg: " + request.Number + "; punkt trasy: " + request.PointIndex + "\r\nPrzyjazd: " + (request.ArrivalChanged ? Ui.FullTime(request.Arrival) : "bez zmiany") + "\r\nOdjazd: " + (request.DepartureChanged ? Ui.FullTime(request.Departure) : "bez zmiany") + "\r\nCzas serwera: " + session.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n\r\n" + reason + "\r\n\r\nWpis pozostaje lokalnie. Po poprawieniu godzin lub połączenia użyj SimRail → Wyślij oczekujące godziny.");
	}
}

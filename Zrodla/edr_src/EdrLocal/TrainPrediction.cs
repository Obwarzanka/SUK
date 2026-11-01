using System;

namespace EdrLocal;

public static class TrainPrediction
{
	public static DateTime Arrival(Train t)
	{
		return t.Arrival ?? t.ArrivalForecast ?? t.PlanArrival;
	}

	public static DateTime Departure(Train t)
	{
		if (t.Departure.HasValue)
		{
			return t.Departure.Value;
		}
		if (t.Forecast.HasValue)
		{
			return t.Forecast.Value;
		}
		return t.PlanDeparture.AddMinutes(Math.Max(0.0, (Arrival(t) - t.PlanArrival).TotalMinutes));
	}

	public static string Hint(Train t, bool arrival)
	{
		if (t.SourceProvider == "TD2")
		{
			string text = ((arrival ? t.LocalArrivalRecorded : t.LocalDepartureRecorded) ? "Czas wpisany w EDR: " : (t.ExternalConfirmed ? "Czas potwierdzony w TD2: " : "Czas z rozkładu TD2: "));
			return text + Ui.FullTime(arrival ? Arrival(t) : Departure(t));
		}
		if (t.SourceProvider == "SimRail")
		{
			DateTime? t2 = (arrival ? t.Arrival : t.Departure);
			if (arrival ? t.ExternalManualArrival : t.ExternalManualDeparture)
			{
				return "Godzina lokalna, oczekuje na potwierdzenie SEPE: " + Ui.FullTime(t2);
			}
			if (t.ExternalConfirmed && t2.HasValue)
			{
				return "Czas potwierdzony w SEPE: " + Ui.FullTime(t2);
			}
			return "Czas przewidywany z SEPE: " + Ui.FullTime(arrival ? Arrival(t) : Departure(t));
		}
		if (!(arrival ? t.Arrival.HasValue : t.Departure.HasValue))
		{
			return "Czas przewidywany: " + Ui.FullTime(arrival ? Arrival(t) : Departure(t)) + ". Nie jest zatwierdzeniem przejazdu.";
		}
		return "Czas zatwierdzony w EDR: " + Ui.FullTime(arrival ? t.Arrival : t.Departure);
	}
}

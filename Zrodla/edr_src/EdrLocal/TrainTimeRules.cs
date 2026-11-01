using System;

namespace EdrLocal;

public static class TrainTimeRules
{
	public static void Validate(DateTime? arrival, DateTime? departure, DateTime now, bool serverClock)
	{
		if (arrival.HasValue && departure.HasValue && departure < arrival)
		{
			throw new TrainTimeException("Godzina odjazdu nie może być wcześniejsza niż godzina przyjazdu.");
		}
		if (serverClock && arrival.HasValue && arrival > now)
		{
			throw new TrainTimeException("Nastąpiła próba podania godziny przyjazdu późniejszej niż obecna (teraz jest " + now.ToString("HH:mm:ss") + ").");
		}
	}
}

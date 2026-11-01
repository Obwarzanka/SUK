using System;
using System.Globalization;

namespace EdrLocal;

public static class ClockDates
{
	public static bool ExplicitDate(string value)
	{
		DateTime result;
		return DateTime.TryParseExact((value ?? "").Trim(), new string[3] { "dd.MM.yyyy HH:mm", "yyyy-MM-dd HH:mm", "dd-MM-yyyy HH:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
	}

	public static DateTime? Resolve(string value, DateTime reference, DateTime? previous, DateTime? notBefore, DateTime now)
	{
		DateTime? result = Engine.JournalTime(value, reference, previous, null);
		if (!result.HasValue || ExplicitDate(value))
		{
			return result;
		}
		DateTime dateTime = previous ?? reference;
		if (previous.HasValue)
		{
			DateTime? dateTime2 = previous;
			DateTime dateTime3 = now.AddHours(12.0);
			if (dateTime2.HasValue && dateTime2.GetValueOrDefault() > dateTime3 && previous.Value.Date == reference.Date && reference <= now)
			{
				dateTime = reference;
			}
		}
		DateTime dateTime4 = dateTime.Date.Add(result.Value.TimeOfDay);
		TimeSpan timeSpan = dateTime4 - dateTime;
		if (timeSpan > TimeSpan.FromHours(12.0))
		{
			dateTime4 = dateTime4.AddDays(-1.0);
		}
		else if (timeSpan < TimeSpan.FromHours(-12.0))
		{
			dateTime4 = dateTime4.AddDays(1.0);
		}
		if (notBefore.HasValue)
		{
			DateTime dateTime5 = dateTime4;
			DateTime? dateTime6 = notBefore;
			if (dateTime6.HasValue && dateTime5 < dateTime6.GetValueOrDefault() && notBefore.Value.Hour >= 18 && dateTime4.Hour < 6 && dateTime4.Date <= notBefore.Value.Date)
			{
				dateTime4 = notBefore.Value.Date.AddDays(1.0).Add(dateTime4.TimeOfDay);
			}
		}
		return dateTime4;
	}
}

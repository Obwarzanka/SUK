using System;
using System.Collections.Generic;
using System.Linq;

namespace EdrLocal;

public static class TelephoneNumbering
{
	public static string Period(DateTime time, bool annual)
	{
		return time.ToString(annual ? "yyyy" : "yyyy-MM");
	}

	public static Dictionary<string, int> Numbers(AppState state, Station station)
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		bool annual = station.Options != null && station.Options.TelephoneAnnual;
		foreach (IGrouping<string, JournalEntry> item in from x in state.Entries
			where x.StationId == station.Id && x.SectionId == "TEL"
			orderby x.Time
			group x by Period(x.Time, annual))
		{
			int num = 1;
			foreach (JournalEntry item2 in item)
			{
				int num2 = item2.TelephoneOrdinal ?? num;
				dictionary[item2.Id] = num2;
				num = Math.Max(num, num2 + 1);
			}
		}
		return dictionary;
	}

	public static int Next(AppState state, Station station, DateTime time)
	{
		bool annual = station.Options != null && station.Options.TelephoneAnnual;
		string period = Period(time, annual);
		Dictionary<string, int> map = Numbers(state, station);
		int num = (from x in state.Entries
			where x.StationId == station.Id && x.SectionId == "TEL" && Period(x.Time, annual) == period
			select map[x.Id]).DefaultIfEmpty(0).Max() + 1;
		StationOptions options = station.Options;
		if (options != null && options.TelephoneNextPeriod == period)
		{
			num = Math.Max(num, options.TelephoneNextNumber);
		}
		return num;
	}

	public static void Ensure(AppState state)
	{
		foreach (Station station in state.Stations)
		{
			if (station.Options == null)
			{
				station.Options = new StationOptions();
			}
			foreach (JournalEntry entry in (from x in state.Entries
				where x.StationId == station.Id && x.SectionId == "TEL" && !x.TelephoneOrdinal.HasValue
				orderby x.Time
				select x).ToList())
			{
				List<JournalEntry> entries = state.Entries;
				Func<JournalEntry, bool> predicate = (JournalEntry x) => x.StationId == station.Id && x.SectionId == "TEL" && x.TelephoneOrdinal.HasValue && Period(x.Time, station.Options.TelephoneAnnual) == Period(entry.Time, station.Options.TelephoneAnnual);
				int num = (from x in entries.Where(predicate)
					select x.TelephoneOrdinal.Value).DefaultIfEmpty(0).Max() + 1;
				if (station.Options.TelephoneNextPeriod == Period(entry.Time, station.Options.TelephoneAnnual))
				{
					num = Math.Max(num, station.Options.TelephoneNextNumber);
				}
				entry.TelephoneOrdinal = num;
			}
		}
	}
}

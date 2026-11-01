using System;
using System.Collections.Generic;
using System.Linq;

namespace EdrLocal;

public static class StationOccupancy
{
	private static bool SameTrain(string train, string number, JournalEntry other)
	{
		if (string.IsNullOrEmpty(train) || string.IsNullOrEmpty(other.TrainId))
		{
			return string.Equals(number, other.Number, StringComparison.CurrentCultureIgnoreCase);
		}
		return train == other.TrainId;
	}

	internal static string DepartureKey(JournalEntry entry)
	{
		return entry.Id + "|" + (entry.Left.HasValue ? entry.Left.Value.Ticks.ToString() : "");
	}

	internal static IEnumerable<JournalEntry> Departures(AppState state, string station, string train, string number)
	{
		return state.Entries.Where((JournalEntry d) => d.StationId == station && d.Type == "Pociąg" && !d.Cancelled && d.Direction == "DO" && d.Left.HasValue && SameTrain(train, number, d));
	}

	public static List<StationTrackItem> AtStation(AppState state, string station)
	{
		List<StationTrackItem> list = new List<StationTrackItem>();
		List<StationTrackOccupation> source = state.StationTrackOccupations ?? new List<StationTrackOccupation>();
		foreach (JournalEntry entry in state.Entries.Where((JournalEntry e) => e.StationId == station && e.Type == "Pociąg" && e.Direction == "Z" && !e.Cancelled && !string.IsNullOrEmpty(e.Track)))
		{
			IEnumerable<JournalEntry> source2 = Departures(state, station, entry.TrainId, entry.Number);
			Func<JournalEntry, bool> predicate = (JournalEntry d) => d.Left >= (entry.Arrived ?? entry.Left ?? entry.Time);
			if (!source2.Any(predicate))
			{
				StationTrackOccupation stationTrackOccupation = source.LastOrDefault((StationTrackOccupation e) => e.StationId == station && e.EntryId == entry.Id && e.SourceTrack == entry.Track);
				if (stationTrackOccupation == null || !stationTrackOccupation.Removed)
				{
					list.Add(new StationTrackItem
					{
						Key = "entry:" + entry.Id,
						Track = ((stationTrackOccupation == null) ? entry.Track : stationTrackOccupation.Track),
						Number = entry.Number,
						TrainId = entry.TrainId,
						Reserved = !entry.Arrived.HasValue,
						Order = (entry.Arrived ?? entry.Time)
					});
				}
			}
		}
		foreach (StationTrackOccupation edit in source.Where((StationTrackOccupation e) => e.StationId == station && string.IsNullOrEmpty(e.EntryId) && !e.Removed))
		{
			IEnumerable<JournalEntry> source3 = Departures(state, station, edit.TrainId, edit.Number);
			Func<JournalEntry, bool> predicate2 = (JournalEntry d) => !(edit.KnownDepartures ?? new List<string>()).Contains(DepartureKey(d));
			if (!source3.Any(predicate2))
			{
				list.Add(new StationTrackItem
				{
					Key = "manual:" + edit.Id,
					Track = edit.Track,
					Number = edit.Number,
					TrainId = edit.TrainId,
					Reserved = false,
					Order = edit.AddedAt
				});
			}
		}
		return (from e in list
			orderby e.Order descending, e.Key
			select e).ToList();
	}
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace EdrLocal;

public static class StationSettings
{
	public static bool RequiresStationTracks(Station station)
	{
		return StationIdentity.Type(station) != "POSTERUNEK ODGAŁĘŹNY";
	}

	public static bool NoStationTracks(Station station)
	{
		if (!RequiresStationTracks(station))
		{
			if (station.TrackSettings != null)
			{
				return station.TrackSettings.Count == 0;
			}
			if (station.Tracks != null)
			{
				return station.Tracks.Count == 0;
			}
			return true;
		}
		return false;
	}

	public static bool TrackAllowed(Station station, string value, JournalEntry previous)
	{
		if (!string.IsNullOrEmpty(value) && !station.Tracks.Contains(value))
		{
			if (NoStationTracks(station) && previous != null && previous.StationId == station.Id)
			{
				return previous.Track == value;
			}
			return false;
		}
		return true;
	}

	public static bool Conducted(Section section)
	{
		if (!section.Td2Archived && !section.Td2AreaHidden)
		{
			return section.Conducted != false;
		}
		return false;
	}

	public static string Title(Section section)
	{
		if (!string.IsNullOrEmpty(section.Td2RouteKey))
		{
			return Td2SectionSettings.Title(section);
		}
		if (!string.IsNullOrWhiteSpace(section.JournalName))
		{
			return section.JournalName;
		}
		return section.Name;
	}

	public static IEnumerable<string> TrackChoices(Station station, string current)
	{
		return station.Tracks.Concat(string.IsNullOrEmpty(current) ? new string[0] : new string[1] { current }).Distinct();
	}

	public static List<string> VisibleTracks(Station station)
	{
		if (station.TrackSettings != null)
		{
			return (from x in station.TrackSettings
				where x.Visible
				select x.Number).ToList();
		}
		return station.Tracks;
	}

	public static void Normalize(Station station)
	{
		if (station.Tracks == null)
		{
			station.Tracks = new List<string>();
		}
		SectionRouting.LabelDuplicates(station);
		if (station.Options == null)
		{
			station.Options = new StationOptions();
		}
		if (station.Options.StationTrackRefreshMinutes == 0)
		{
			station.Options.StationTrackRefreshMinutes = 10;
		}
		if (station.JournalTabs == null)
		{
			station.JournalTabs = new List<JournalTabDefinition>();
		}
		if (station.TrackSettings == null)
		{
			station.TrackSettings = station.Tracks.Select((string x) => new StationTrackSetting
			{
				Number = x,
				Visible = true
			}).ToList();
		}
		if (station.RequiredChecks == null)
		{
			station.RequiredChecks = (station.Checks ?? new List<string>()).Select((string x) => new RequiredCheckSetting
			{
				Name = x,
				Enabled = true,
				Hours = ""
			}).ToList();
		}
		foreach (Section section in station.Sections)
		{
			Td2SectionSettings.Normalize(section);
			if (!section.RightTrackNumber.HasValue)
			{
				section.RightTrackNumber = ((section.Tracks == 1) ? 1 : ((section.NormalIncomingTrack != 1) ? 1 : 2));
			}
			if (section.Crossings == null)
			{
				section.Crossings = new List<CrossingSetting>();
			}
			HashSet<string> hashSet = new HashSet<string>();
			foreach (CrossingSetting crossing in section.Crossings)
			{
				if (string.IsNullOrWhiteSpace(crossing.Id) || !hashSet.Add(crossing.Id))
				{
					crossing.Id = Guid.NewGuid().ToString("N");
					hashSet.Add(crossing.Id);
				}
			}
			if (string.IsNullOrEmpty(section.JournalName))
			{
				section.JournalName = section.Name;
			}
			if (!section.Conducted.HasValue)
			{
				section.Conducted = true;
			}
			SectionTrackDirections.Normalize(section);
		}
	}

	public static void Validate(Station station, AppState state)
	{
		if (!string.IsNullOrEmpty(station.PostType) && !StationIdentity.Types.Contains(station.PostType))
		{
			throw new Exception("Wybierz typ posterunku z listy.");
		}
		if (station.Options != null && ((station.Options.AutomaticReleaseSeconds.HasValue && (station.Options.AutomaticReleaseSeconds < 1 || station.Options.AutomaticReleaseSeconds > 86400)) || station.Options.StationTrackRefreshMinutes < 1 || station.Options.StationTrackRefreshMinutes > 1440 || station.Options.JournalVisibility < 0 || station.Options.JournalVisibility > 2))
		{
			throw new Exception("Sprawdź czas usunięcia zajętości (1–86400 s), odświeżanie torów (1–1440 min) i widoczność dzienników.");
		}
		if (station.Td2AreaPosts != null && (station.Td2AreaPosts.Any((Td2AreaPost x) => x == null || string.IsNullOrEmpty(x.Id) || x.Id.Length > 20 || !x.Id.All(char.IsDigit) || x.Id == station.SimRailPointId) || (from x in station.Td2AreaPosts
			group x by x.Id).Any((IGrouping<string, Td2AreaPost> x) => x.Count() > 1)))
		{
			throw new Exception("Wybierz różne części stacji; posterunek główny jest już w obszarze.");
		}
		if (string.IsNullOrWhiteSpace(station.Name) || station.Sections.Count == 0)
		{
			throw new Exception("Wpisz nazwę posterunku i przynajmniej jeden szlak.");
		}
		if (station.Sections.Any((Section x) => string.IsNullOrWhiteSpace(x.Name) || (string.IsNullOrWhiteSpace(x.Td2RouteKey) && string.IsNullOrWhiteSpace(x.Line)) || x.Tracks < 1 || x.Tracks > 2 || x.NormalIncomingTrack < 0 || x.NormalIncomingTrack > x.Tracks))
		{
			throw new Exception("Szlak musi mieć nazwę, linię i 1 lub 2 tory; tor wjazdowy musi należeć do szlaku.");
		}
		if (station.Sections.Where((Section x) => !x.Td2Archived && string.IsNullOrEmpty(x.Td2RouteKey)).GroupBy((Section x) => SectionRouting.Line(x.Line) + "|" + SectionRouting.Neighbour(x.Name), StringComparer.CurrentCultureIgnoreCase).Any((IGrouping<string, Section> g) => g.Count() > 1))
		{
			throw new Exception("Ten sam szlak na tej samej linii występuje dwa razy.");
		}
		if ((from x in station.Sections
			where !x.Td2Archived && !x.Td2AreaHidden && !string.IsNullOrEmpty(x.Td2RouteKey)
			group x by x.Td2RouteKey).Any((IGrouping<string, Section> g) => g.Count() > 1))
		{
			throw new Exception("Ten sam szlak TD2 występuje dwa razy.");
		}
		if ((RequiresStationTracks(station) && station.TrackSettings.Count == 0) || station.TrackSettings.Any((StationTrackSetting x) => string.IsNullOrWhiteSpace(x.Number)) || (from x in station.TrackSettings
			group x by x.Number.Trim()).Any((IGrouping<string, StationTrackSetting> g) => g.Count() > 1) || (station.TrackSettings.Count > 0 && !station.TrackSettings.Any((StationTrackSetting x) => x.Visible)))
		{
			throw new Exception("Tory stacyjne muszą mieć unikalne numery; co najmniej jeden musi być widoczny.");
		}
		if (!station.Sections.Any(Conducted))
		{
			throw new Exception("Co najmniej jeden dziennik musi być prowadzony.");
		}
		List<string> tracks = station.TrackSettings.Select((StationTrackSetting x) => x.Number.Trim()).ToList();
		if (!NoStationTracks(station) && (state.Entries.Any((JournalEntry x) => x.StationId == station.Id && !string.IsNullOrEmpty(x.Track) && !tracks.Contains(x.Track)) || state.Trains.Any((Train x) => x.StationId == station.Id && !string.IsNullOrEmpty(x.Track) && !tracks.Contains(x.Track))))
		{
			throw new Exception("Nie można usunąć używanego toru stacyjnego. Można ukryć go w kontrolce.");
		}
		if (!NoStationTracks(station) && StationOccupancy.AtStation(state, station.Id).Any((StationTrackItem x) => !tracks.Contains(x.Track)))
		{
			throw new Exception("Nie można usunąć toru z zajętością. Usuń lub przenieś zajętość albo ukryj tor.");
		}
		List<string> ids = station.Sections.Select((Section x) => x.Id).ToList();
		if (state.Entries.Any((JournalEntry x) => x.StationId == station.Id && x.SectionId != "TEL" && !ids.Contains(x.SectionId)))
		{
			throw new Exception("Nie można usunąć dziennika zawierającego wpisy.");
		}
		foreach (Section section in station.Sections)
		{
			List<JournalEntry> entries = state.Entries;
			Func<JournalEntry, bool> predicate = (JournalEntry x) => x.StationId == station.Id && x.SectionId == section.Id && x.LineTrack > section.Tracks;
			if (entries.Any(predicate))
			{
				throw new Exception("Nie można usunąć używanego toru szlakowego.");
			}
			foreach (CrossingSetting item in section.Crossings ?? new List<CrossingSetting>())
			{
				if (string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.Kilometre) || !new string[2] { "Znak", "Godzina" }.Contains(item.NotificationMode))
				{
					throw new Exception("Uzupełnij nazwę, kilometr i sposób powiadomienia dróżnika.");
				}
			}
		}
		HashSet<string> hashSet = new HashSet<string>();
		foreach (JournalTabDefinition tab in station.JournalTabs)
		{
			if (string.IsNullOrWhiteSpace(tab.LeftId) || !ids.Contains(tab.LeftId) || !Conducted(station.Sections.First((Section x) => x.Id == tab.LeftId)))
			{
				throw new Exception("Zakładka musi wskazywać prowadzony dziennik.");
			}
			foreach (string id in new string[2] { tab.LeftId, tab.RightId }.Where((string x) => !string.IsNullOrEmpty(x)))
			{
				if (!ids.Contains(id) || !Conducted(station.Sections.First((Section x) => x.Id == id)) || !hashSet.Add(id))
				{
					throw new Exception("Dziennik może występować tylko w jednej zakładce.");
				}
			}
		}
		foreach (RequiredCheckSetting requiredCheck in station.RequiredChecks)
		{
			if (requiredCheck.AfterDutyMinutes.HasValue && (requiredCheck.AfterDutyMinutes < 0 || requiredCheck.AfterDutyMinutes > 1440))
			{
				throw new Exception("Czas sprawdzenia po przyjęciu dyżuru musi mieścić się w zakresie 0–1440 minut.");
			}
			if (string.IsNullOrWhiteSpace(requiredCheck.Name))
			{
				throw new Exception("Sprawdzenie musi mieć nazwę.");
			}
			string[] array = (requiredCheck.Hours ?? "").Split(new char[2] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string text in array)
			{
				if (!DateTime.TryParseExact(text.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var _))
				{
					throw new Exception("Godziny sprawdzeń wpisz w formacie HH:mm, rozdzielając średnikiem.");
				}
			}
		}
		station.Tracks = tracks;
		station.Checks = (from x in station.RequiredChecks
			where x.Enabled
			select x.Name).ToList();
	}
}

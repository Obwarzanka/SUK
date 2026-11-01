using System;
using System.Collections.Generic;
using System.Linq;

namespace EdrLocal;

public static class Td2Topology
{
	private static bool LegacyMatch(Section old, Section proposed)
	{
		if (!string.IsNullOrEmpty(old.Td2RouteKey))
		{
			return false;
		}
		if (proposed.Td2Kind == "Zewnętrzny")
		{
			return (string.IsNullOrEmpty(old.Td2LocalLine) ? (old.Line ?? "") : old.Td2LocalLine).Split('/').Any((string x) => SectionRouting.SameLine(x, proposed.Td2LocalLine));
		}
		if (SectionRouting.SameLine(old.Line, proposed.Td2LocalLine))
		{
			return SectionRouting.SameNeighbour(old.Name, proposed.Name);
		}
		return false;
	}

	public static void Reconcile(Station station, Station proposed, IEnumerable<JournalEntry> entries)
	{
		StationSettings.Normalize(station);
		List<JournalEntry> rows = entries.Where((JournalEntry x) => x.StationId == station.Id).ToList();
		station.Td2SceneName = proposed.Td2SceneName;
		station.Td2SceneId = proposed.Td2SceneId;
		station.Td2AreaCatalog = proposed.Td2AreaCatalog;
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		HashSet<string> consumed = new HashSet<string>();
		foreach (Section expected in proposed.Sections)
		{
			List<Section> sections = station.Sections;
			Func<Section, bool> predicate = (Section x) => !x.Td2Archived && x.Td2RouteKey == expected.Td2RouteKey;
			Section section = sections.FirstOrDefault(predicate);
			List<Section> list = (from x in station.Sections
				where !consumed.Contains(x.Id) && LegacyMatch(x, expected) && Enumerable.Count(proposed.Sections, (Section p) => LegacyMatch(x, p)) == 1
				orderby Enumerable.Count(rows, (JournalEntry e) => e.SectionId == x.Id) descending, x.ManualConfiguration descending
				select x).ToList();
			if (section == null)
			{
				section = list.FirstOrDefault();
				if (section == null)
				{
					section = Json.Clone(expected);
					section.Id = "td2-section-" + Guid.NewGuid().ToString("N");
					station.Sections.Add(section);
					station.SimulatorReviewed = false;
				}
				else
				{
					section.Name = expected.Name;
					section.Td2LocalLine = expected.Td2LocalLine;
					section.JournalName = expected.Td2LocalLine;
				}
				section.Td2RouteKey = expected.Td2RouteKey;
				section.Td2Kind = expected.Td2Kind;
				section.Td2PeerName = expected.Td2PeerName;
			}
			section.Td2LocalLine = expected.Td2LocalLine;
			section.Td2Kind = expected.Td2Kind;
			section.Td2PeerName = expected.Td2PeerName;
			section.Name = expected.Name;
			Td2SectionSettings.Normalize(section);
			foreach (Section old in list)
			{
				consumed.Add(old.Id);
				if (old != section)
				{
					if (rows.Any((JournalEntry e) => e.SectionId == old.Id) || old.ManualConfiguration)
					{
						old.Td2Archived = true;
						old.Conducted = false;
						old.Td2RouteKey = expected.Td2RouteKey;
						old.Td2Kind = expected.Td2Kind;
						old.Td2PeerName = expected.Td2PeerName;
						old.JournalName = expected.Td2LocalLine;
						old.Td2LocalLine = expected.Td2LocalLine;
					}
					else
					{
						station.Sections.Remove(old);
					}
					dictionary[old.Id] = section.Id;
				}
			}
		}
		foreach (Section item in station.Sections.Where((Section x) => string.IsNullOrEmpty(x.Td2RouteKey) && (x.Id ?? "").StartsWith("td2-section-")))
		{
			item.Td2Archived = true;
			item.Conducted = false;
		}
		HashSet<string> hashSet = new HashSet<string>(proposed.Sections.Select((Section x) => x.Td2RouteKey));
		foreach (Section item2 in station.Sections.Where((Section x) => !string.IsNullOrEmpty(x.Td2RouteKey) && (x.Id ?? "").StartsWith("td2-section-")))
		{
			item2.Td2AreaHidden = !hashSet.Contains(item2.Td2RouteKey);
		}
		HashSet<string> used = new HashSet<string>();
		List<JournalTabDefinition> list2 = new List<JournalTabDefinition>();
		foreach (JournalTabDefinition journalTab in station.JournalTabs)
		{
			List<string> list3 = new List<string>();
			string[] array = new string[2] { journalTab.LeftId, journalTab.RightId };
			foreach (string text in array)
			{
				if (!string.IsNullOrEmpty(text))
				{
					string target = (dictionary.TryGetValue(text, out var value) ? value : text);
					if (station.Sections.Any((Section x) => x.Id == target && StationSettings.Conducted(x)) && used.Add(target))
					{
						list3.Add(target);
					}
				}
			}
			if (list3.Count > 0)
			{
				list2.Add(new JournalTabDefinition
				{
					LeftId = list3[0],
					RightId = ((list3.Count > 1) ? list3[1] : null)
				});
			}
		}
		List<Section> list4 = station.Sections.Where((Section x) => StationSettings.Conducted(x) && !used.Contains(x.Id)).ToList();
		for (int num2 = 0; num2 < list4.Count; num2 += 2)
		{
			list2.Add(new JournalTabDefinition
			{
				LeftId = list4[num2].Id,
				RightId = ((num2 + 1 < list4.Count) ? list4[num2 + 1].Id : null)
			});
		}
		station.JournalTabs = list2;
		StationSettings.Normalize(station);
	}
}

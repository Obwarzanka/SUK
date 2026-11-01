using System;
using System.Collections.Generic;
using System.Linq;

namespace EdrLocal;

public static class WorkTrip
{
	public static string[] Templates(string local, string neighbour)
	{
		return new string[4]
		{
			"Po zamkniętym torze nr [Szlakowy], z postojem/pracą na kilometrze [...]. Zjazd do [" + neighbour + "] na/do godz. [...].",
			"Po zamkniętym torze nr [Szlakowy] do kilometra [...] i z powrotem. Zjazd do [" + local + "] na/do godz. [...].",
			"Po zamkniętym torze nr [Szlakowy] do kilometra [...].",
			"Po zamkniętym torze nr [Szlakowy], z wjazdem w kilometrze [...] do kilometra/posterunku [...]."
		};
	}

	public static bool Even(JournalEntry e, Section section)
	{
		char[] array = (e.Number ?? "").Where(char.IsDigit).ToArray();
		if (array.Length <= 0)
		{
			if (section != null)
			{
				if (!(e.Direction == "Z"))
				{
					return !section.ArrivalEven;
				}
				return section.ArrivalEven;
			}
			return false;
		}
		return (array[array.Length - 1] - 48) % 2 == 0;
	}

	public static List<JournalEntry> Order(IEnumerable<JournalEntry> source)
	{
		List<JournalEntry> source2 = source.OrderBy((JournalEntry x) => x.Time).ToList();
		HashSet<string> ids = new HashSet<string>(source2.Select((JournalEntry x) => x.Id));
		List<JournalEntry> list = new List<JournalEntry>();
		foreach (JournalEntry e in source2.Where((JournalEntry x) => string.IsNullOrEmpty(x.ReturnOfId) || !ids.Contains(x.ReturnOfId)))
		{
			list.Add(e);
			Func<JournalEntry, bool> predicate = (JournalEntry x) => x.ReturnOfId == e.Id;
			list.AddRange(source2.Where(predicate));
		}
		return list;
	}
}

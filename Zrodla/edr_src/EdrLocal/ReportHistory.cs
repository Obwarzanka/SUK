using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EdrLocal;

public static class ReportHistory
{
	public static Dictionary<string, List<ReportVersion>> Read(AppState state, IEnumerable<JournalEntry> entries)
	{
		Dictionary<string, List<ReportVersion>> result = entries.ToDictionary((JournalEntry x) => x.Id, (JournalEntry x) => new List<ReportVersion>());
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		foreach (Audit item in state.History)
		{
			AppState appState;
			AppState appState2;
			try
			{
				appState = Json.Read<AppState>(Encoding.UTF8.GetBytes(item.Before));
				appState2 = Json.Read<AppState>(Encoding.UTF8.GetBytes(item.After));
			}
			catch
			{
				continue;
			}
			Dictionary<string, JournalEntry> dictionary2 = appState.Entries.Where((JournalEntry x) => result.ContainsKey(x.Id)).ToDictionary((JournalEntry x) => x.Id);
			Dictionary<string, JournalEntry> dictionary3 = appState2.Entries.Where((JournalEntry x) => result.ContainsKey(x.Id)).ToDictionary((JournalEntry x) => x.Id);
			foreach (KeyValuePair<string, JournalEntry> item2 in dictionary3)
			{
				string key = item2.Key;
				string text = Json.Text(item2.Value);
				dictionary2.TryGetValue(key, out var value);
				string text2 = ((value == null) ? null : Json.Text(value));
				if (!(text == text2))
				{
					if (result[key].Count == 0 && value != null)
					{
						result[key].Add(new ReportVersion
						{
							Entry = value
						});
					}
					if (!dictionary.TryGetValue(key, out var value2) || text != value2)
					{
						result[key].Add(new ReportVersion
						{
							Entry = item2.Value,
							Changed = item.Time
						});
						dictionary[key] = text;
					}
				}
			}
		}
		foreach (JournalEntry entry in entries)
		{
			List<ReportVersion> list = result[entry.Id];
			if (list.Count == 0 || Json.Text(list[list.Count - 1].Entry) != Json.Text(entry))
			{
				list.Add(new ReportVersion
				{
					Entry = Json.Clone(entry)
				});
			}
			list.Reverse();
			for (int num = 0; num < list.Count; num++)
			{
				list[num].Previous = num > 0;
			}
		}
		return result;
	}
}

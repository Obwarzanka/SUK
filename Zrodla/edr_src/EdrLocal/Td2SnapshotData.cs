using System;
using System.Collections.Generic;
using System.Linq;

namespace EdrLocal;

public sealed class Td2SnapshotData
{
	public string Region;

	public List<Td2Record> Records;

	private Dictionary<string, Td2Record> byId;

	public Td2Record Find(string id)
	{
		if (byId == null)
		{
			byId = Records.ToDictionary((Td2Record x) => x.Id);
		}
		if (!byId.TryGetValue(id, out var value))
		{
			return null;
		}
		return value;
	}

	public List<SimRailLocation> Locations()
	{
		Dictionary<string, SimRailLocation> dictionary = new Dictionary<string, SimRailLocation>();
		foreach (Td2Record record in Records)
		{
			Dictionary<string, Tuple<string, string>> dictionary2 = Td2Directions.SceneLabels(record.Schedule);
			foreach (Td2Stop item in record.Schedule.stopPoints.Where(Td2Names.Operating))
			{
				if (!dictionary.ContainsKey(item.pointId))
				{
					bool flag = dictionary2.TryGetValue(item.pointId, out var value);
					dictionary[item.pointId] = new SimRailLocation
					{
						Id = item.pointId,
						Name = Td2Names.Name(item),
						SceneName = (flag ? value.Item1 : ""),
						SceneId = (flag ? value.Item2 : "")
					};
				}
			}
		}
		return dictionary.Values.OrderBy((SimRailLocation x) => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
	}
}

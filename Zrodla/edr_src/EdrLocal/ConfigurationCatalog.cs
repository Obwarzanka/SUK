using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace EdrLocal;

public static class ConfigurationCatalog
{
	public static List<ConfiguredStation> Build(SimRailSnapshot snapshot, string unit, CancellationToken token)
	{
		List<ConfiguredStation> list = new List<ConfiguredStation>();
		List<SimRailLocation> list2 = snapshot.Locations();
		List<SimRailLocation> list3 = list2.Where((SimRailLocation x) => snapshot.DispatcherNames.Contains(x.Name) || StationIdentity.KnownType(x.Name) != null).ToList();
		foreach (SimRailLocation item in ((list3.Count > 0) ? list3 : list2).OrderBy((SimRailLocation x) => x.Name))
		{
			token.ThrowIfCancellationRequested();
			if (!string.IsNullOrEmpty(item.Id) && item.Id.Length <= 20 && item.Id.All(char.IsDigit))
			{
				Station station = new SimRailSession(null, snapshot, item.Id).SuggestStation();
				StationSettings.Normalize(station);
				list.Add(new ConfiguredStation
				{
					Key = item.Id,
					UnitId = unit,
					Station = station
				});
			}
		}
		return list;
	}
}

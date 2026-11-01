using System;
using System.Collections.Generic;
using System.Linq;

namespace EdrLocal;

public static class Td2Area
{
	public static HashSet<string> Members(Station station, string point)
	{
		return new HashSet<string>(((station == null) ? new List<Td2AreaPost>() : (station.Td2AreaPosts ?? new List<Td2AreaPost>())).Select((Td2AreaPost x) => x.Id).Concat(new string[1] { point }));
	}

	public static void Validate(SimRailSession session, Station station)
	{
		if (station != null && station.Td2AreaPosts != null && station.Td2AreaPosts.Count != 0)
		{
			List<SimRailLocation> source = session.Snapshot.Td2.Locations();
			SimRailLocation own = source.FirstOrDefault((SimRailLocation x) => x.Id == session.PointId);
			if (own != null && !string.IsNullOrEmpty(own.SceneId) && source.Any((SimRailLocation x) => station.Td2AreaPosts.Any((Td2AreaPost p) => p.Id == x.Id) && !string.IsNullOrEmpty(x.SceneId) && x.SceneId != own.SceneId))
			{
				throw new InvalidOperationException("Obszar posterunku może obejmować wyłącznie części tej samej scenerii TD2.");
			}
		}
	}

	public static List<Td2Visit> Visits(Td2Schedule schedule, Station station, string point)
	{
		HashSet<string> hashSet = Members(station, point);
		List<Td2Visit> list = new List<Td2Visit>();
		Td2Visit td2Visit = null;
		for (int i = 0; i < schedule.stopPoints.Count; i++)
		{
			Td2Stop td2Stop = schedule.stopPoints[i];
			if (!Td2Names.Operating(td2Stop))
			{
				continue;
			}
			if (!hashSet.Contains(td2Stop.pointId))
			{
				td2Visit = null;
				continue;
			}
			if (td2Visit == null)
			{
				Td2Visit td2Visit2 = new Td2Visit();
				td2Visit2.First = i;
				td2Visit2.Last = i;
				td2Visit2.Anchor = i;
				td2Visit = td2Visit2;
				list.Add(td2Visit);
			}
			else
			{
				td2Visit.Last = i;
			}
			if (td2Stop.pointId == point && schedule.stopPoints[td2Visit.Anchor].pointId != point)
			{
				td2Visit.Anchor = i;
			}
		}
		return list;
	}

	public static List<Td2AreaPost> Choices(SimRailSession session, Station station)
	{
		if (session == null || !session.IsTd2 || session.Snapshot.Td2 == null)
		{
			return Json.Clone(station.Td2AreaCatalog ?? new List<Td2AreaPost>());
		}
		string scene = station.Td2SceneName ?? (from x in session.Snapshot.Td2.Locations()
			where x.Id == station.SimRailPointId
			select x.SceneName).FirstOrDefault();
		SimRailLocation simRailLocation = session.Snapshot.Td2.Locations().FirstOrDefault((SimRailLocation x) => x.Id == station.SimRailPointId);
		string sceneId = ((simRailLocation == null) ? station.Td2SceneId : simRailLocation.SceneId);
		return (from x in session.Snapshot.Td2.Locations()
			where x.Id != station.SimRailPointId && (string.IsNullOrEmpty(sceneId) ? (!string.IsNullOrEmpty(scene) && x.SceneName == scene) : (x.SceneId == sceneId))
			select new Td2AreaPost
			{
				Id = x.Id,
				Name = x.Name
			}).ToList();
	}
}

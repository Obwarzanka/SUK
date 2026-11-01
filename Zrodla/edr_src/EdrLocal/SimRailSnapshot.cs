using System;
using System.Collections.Generic;
using System.Linq;

namespace EdrLocal;

public sealed class SimRailSnapshot
{
	public Td2SnapshotData Td2;

	private SimRailTopology topology;

	public string ServerCode;

	public List<SimRailTrain> Trains;

	public HashSet<string> DispatcherNames;

	public DateTime GameTime;

	public int UtcOffset;

	public DateTime DownloadedUtc;

	public SimRailTopology Topology => topology ?? (topology = new SimRailTopology(Trains));

	public List<SimRailLocation> Locations()
	{
		if (Td2 != null)
		{
			return Td2.Locations();
		}
		return (from x in Trains.SelectMany((SimRailTrain x) => x.timetable ?? new List<SimRailPoint>())
			where !string.IsNullOrWhiteSpace(x.pointId) && !string.IsNullOrWhiteSpace(x.nameForPerson)
			group x by x.pointId into g
			select new SimRailLocation
			{
				Id = g.Key,
				Name = g.First().nameForPerson
			}).OrderBy((SimRailLocation x) => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
	}

	public SimRailTrain Find(string number)
	{
		return Trains.FirstOrDefault((SimRailTrain x) => x.trainNoLocal == number);
	}

	public string Neighbour(List<SimRailPoint> route, int at, int direction)
	{
		return Topology.Neighbour(route, at, direction, Topology.PointNames);
	}
}

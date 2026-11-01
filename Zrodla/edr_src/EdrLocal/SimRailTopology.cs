using System;
using System.Collections.Generic;
using System.Linq;

namespace EdrLocal;

public sealed class SimRailTopology
{
	public const int RouteVersion = 2;

	private readonly Dictionary<string, List<SimRailPoint>> lines;

	private readonly List<List<SimRailPoint>> routes;

	private readonly Dictionary<Tuple<string, string, string>, List<List<SimRailPoint>>> bridges = new Dictionary<Tuple<string, string, string>, List<List<SimRailPoint>>>();

	public IEnumerable<string> PointNames => (from x in lines.Values.SelectMany((List<SimRailPoint> x) => x)
		select x.nameForPerson).Distinct(StringComparer.CurrentCultureIgnoreCase);

	public SimRailTopology(IEnumerable<SimRailTrain> trains)
	{
		routes = trains.Select((SimRailTrain t) => (t.timetable ?? new List<SimRailPoint>()).OrderBy((SimRailPoint p) => p.indexOfPoint).ToList()).ToList();
		lines = (from x in routes.SelectMany((List<SimRailPoint> x) => x)
			where !string.IsNullOrEmpty(x.nameForPerson)
			group x by SimRailProvider.Value(x.line)).ToDictionary((IGrouping<string, SimRailPoint> g) => g.Key, (IGrouping<string, SimRailPoint> g) => (from x in g
			group x by x.nameForPerson into n
			select n.First()).ToList());
	}

	public string Line(List<SimRailPoint> route, int at, int direction)
	{
		string text = SimRailProvider.Value(route[at].line);
		int num = at + direction;
		if (num < 0 || num >= route.Count)
		{
			return text;
		}
		string text2 = SimRailProvider.Value(route[num].line);
		if (!(text2 != text) || !lines.TryGetValue(text2, out var value) || !value.Any((SimRailPoint x) => x.nameForPerson == route[at].nameForPerson))
		{
			return text;
		}
		return text2;
	}

	private bool OnLine(SimRailPoint point, string line)
	{
		if (lines.TryGetValue(line, out var value))
		{
			return value.Any((SimRailPoint x) => SamePoint(x, point));
		}
		return false;
	}

	private static bool SamePoint(SimRailPoint a, SimRailPoint b)
	{
		if (string.IsNullOrEmpty(a.pointId) || string.IsNullOrEmpty(b.pointId))
		{
			return SectionRouting.SameNeighbour(a.nameForPerson, b.nameForPerson);
		}
		return a.pointId == b.pointId;
	}

	public string Neighbour(List<SimRailPoint> route, int at, int direction, IEnumerable<string> allowed)
	{
		int num = at + direction;
		if (num < 0 || num >= route.Count)
		{
			return "";
		}
		string text = Line(route, at, direction);
		HashSet<string> names = new HashSet<string>(allowed.Select(SectionRouting.Neighbour), StringComparer.CurrentCultureIgnoreCase);
		names.Remove(SectionRouting.Neighbour(route[at].nameForPerson));
		if (SimRailProvider.Value(route[num].line) == text || OnLine(route[num], text))
		{
			List<Tuple<int, string>> list = new List<Tuple<int, string>>();
			foreach (List<SimRailPoint> item in BridgePaths(route[at].nameForPerson, route[num].nameForPerson, text))
			{
				SimRailPoint simRailPoint = item.FirstOrDefault((SimRailPoint x) => names.Contains(SectionRouting.Neighbour(x.nameForPerson)));
				if (simRailPoint != null)
				{
					list.Add(Tuple.Create(item.Count, SectionRouting.Neighbour(simRailPoint.nameForPerson)));
				}
			}
			if (list.Count > 0)
			{
				int longest = list.Max((Tuple<int, string> x) => x.Item1);
				List<string> list2 = (from x in list
					where x.Item1 == longest
					select x.Item2).Distinct(StringComparer.CurrentCultureIgnoreCase).ToList();
				if (list2.Count == 1)
				{
					return list2[0];
				}
				return "";
			}
			for (int num2 = num; num2 >= 0 && num2 < route.Count && OnLine(route[num2], text); num2 += direction)
			{
				if (names.Contains(SectionRouting.Neighbour(route[num2].nameForPerson)))
				{
					return SectionRouting.Neighbour(route[num2].nameForPerson);
				}
				if (SimRailProvider.Value(route[num2].line) != text)
				{
					break;
				}
			}
		}
		return ConnectedNeighbour(route, at, direction, names);
	}

	private string ConnectedNeighbour(List<SimRailPoint> route, int at, int direction, HashSet<string> names)
	{
		SimRailPoint point = route[at];
		string text = Line(route, at, direction);
		if (!lines.TryGetValue(text, out var value))
		{
			return "";
		}
		SimRailPoint center = value.FirstOrDefault((SimRailPoint x) => SamePoint(x, point));
		if (center == null)
		{
			return "";
		}
		int index = at + direction;
		int num = at - direction;
		double num2 = 0.0;
		if (SimRailProvider.Value(route[index].line) == text)
		{
			num2 = route[index].mileage - center.mileage;
		}
		else if (num >= 0 && num < route.Count && SimRailProvider.Value(route[num].line) == text)
		{
			num2 = center.mileage - route[num].mileage;
		}
		if (Math.Abs(num2) < 0.0001)
		{
			return "";
		}
		List<SimRailPoint> list = new List<SimRailPoint>();
		foreach (List<SimRailPoint> route2 in routes)
		{
			for (int num3 = 0; num3 < route2.Count; num3++)
			{
				if (!SamePoint(route2[num3], point))
				{
					continue;
				}
				int[] array = new int[2] { -1, 1 };
				foreach (int num5 in array)
				{
					for (int num6 = num3 + num5; num6 >= 0 && num6 < route2.Count; num6 += num5)
					{
						SimRailPoint item = route2[num6];
						SimRailPoint simRailPoint = value.FirstOrDefault((SimRailPoint x) => SamePoint(x, item));
						if (simRailPoint == null)
						{
							break;
						}
						if (names.Contains(SectionRouting.Neighbour(item.nameForPerson)))
						{
							if ((simRailPoint.mileage - center.mileage) * num2 > 0.0)
							{
								list.Add(simRailPoint);
							}
							break;
						}
						if (SimRailProvider.Value(item.line) != text)
						{
							break;
						}
					}
				}
			}
		}
		if (list.Count == 0)
		{
			return "";
		}
		double nearest = list.Min((SimRailPoint x) => Math.Abs(x.mileage - center.mileage));
		List<string> list2 = (from x in list
			where Math.Abs(Math.Abs(x.mileage - center.mileage) - nearest) < 0.0001
			select SectionRouting.Neighbour(x.nameForPerson)).Distinct(StringComparer.CurrentCultureIgnoreCase).ToList();
		if (list2.Count != 1)
		{
			return "";
		}
		return list2[0];
	}

	public string PriorNeighbour(List<SimRailPoint> route, int at, int direction, IEnumerable<string> allowed)
	{
		int num = at + direction;
		if (num < 0 || num >= route.Count)
		{
			return "";
		}
		string text = Line(route, at, direction);
		HashSet<string> names = new HashSet<string>(allowed.Select(SectionRouting.Neighbour), StringComparer.CurrentCultureIgnoreCase);
		names.Remove(SectionRouting.Neighbour(route[at].nameForPerson));
		if (SimRailProvider.Value(route[num].line) == text)
		{
			string nameForPerson = route[at].nameForPerson;
			string nameForPerson2 = route[num].nameForPerson;
			List<Tuple<int, string>> list = new List<Tuple<int, string>>();
			foreach (List<SimRailPoint> item in BridgePaths(nameForPerson, nameForPerson2, text))
			{
				SimRailPoint simRailPoint = item.FirstOrDefault((SimRailPoint x) => names.Contains(SectionRouting.Neighbour(x.nameForPerson)));
				if (simRailPoint != null)
				{
					list.Add(Tuple.Create(item.Count, SectionRouting.Neighbour(simRailPoint.nameForPerson)));
				}
			}
			if (list.Count > 0)
			{
				int longest = list.Max((Tuple<int, string> x) => x.Item1);
				List<string> list2 = (from x in list
					where x.Item1 == longest
					select x.Item2).Distinct(StringComparer.CurrentCultureIgnoreCase).ToList();
				if (list2.Count == 1)
				{
					return list2[0];
				}
			}
			for (int num2 = num; num2 >= 0 && num2 < route.Count && !(SimRailProvider.Value(route[num2].line) != text); num2 += direction)
			{
				if (names.Contains(SectionRouting.Neighbour(route[num2].nameForPerson)))
				{
					return SectionRouting.Neighbour(route[num2].nameForPerson);
				}
			}
		}
		return LegacyNeighbour(route, at, direction, names);
	}

	private List<List<SimRailPoint>> BridgePaths(string first, string last, string line)
	{
		Tuple<string, string, string> key = Tuple.Create(first, last, line);
		if (bridges.TryGetValue(key, out var value))
		{
			return value;
		}
		value = new List<List<SimRailPoint>>();
		foreach (List<SimRailPoint> route in routes)
		{
			for (int i = 0; i < route.Count; i++)
			{
				if (!(route[i].nameForPerson == first))
				{
					continue;
				}
				int[] array = new int[2] { -1, 1 };
				foreach (int num in array)
				{
					List<SimRailPoint> list = new List<SimRailPoint>();
					HashSet<string> hashSet = new HashSet<string>();
					hashSet.Add(first);
					HashSet<string> hashSet2 = hashSet;
					for (int k = i + num; k >= 0 && k < route.Count; k += num)
					{
						SimRailPoint simRailPoint = route[k];
						if (SimRailProvider.Value(simRailPoint.line) != line || !hashSet2.Add(simRailPoint.nameForPerson))
						{
							break;
						}
						list.Add(simRailPoint);
						if (simRailPoint.nameForPerson == last)
						{
							value.Add(list);
							break;
						}
					}
				}
			}
		}
		bridges[key] = value;
		return value;
	}

	public string LegacyNeighbour(List<SimRailPoint> route, int at, int direction, IEnumerable<string> allowed)
	{
		int num = at + direction;
		if (num < 0 || num >= route.Count)
		{
			return "";
		}
		SimRailPoint point = route[at];
		string text = Line(route, at, direction);
		if (!lines.TryGetValue(text, out var value))
		{
			return "";
		}
		HashSet<string> names = new HashSet<string>(allowed.Select(SectionRouting.Neighbour), StringComparer.CurrentCultureIgnoreCase);
		names.Remove(SectionRouting.Neighbour(point.nameForPerson));
		List<SimRailPoint> list = value.Where((SimRailPoint x) => names.Contains(SectionRouting.Neighbour(x.nameForPerson))).ToList();
		double km = value.FirstOrDefault((SimRailPoint x) => x.nameForPerson == point.nameForPerson)?.mileage ?? point.mileage;
		double delta = route[num].mileage - km;
		bool flag = SimRailProvider.Value(route[num].line) == text;
		if (!flag)
		{
			int num2 = at - direction;
			if (num2 >= 0 && num2 < route.Count && SimRailProvider.Value(route[num2].line) == text)
			{
				delta = km - route[num2].mileage;
				flag = true;
			}
		}
		if (flag && Math.Abs(delta) > 0.0001)
		{
			List<SimRailPoint> list2 = (from x in list
				where (x.mileage - km) * delta > 0.0
				orderby Math.Abs(x.mileage - km)
				select x).ToList();
			if (list2.Count > 0)
			{
				return SectionRouting.Neighbour(list2[0].nameForPerson);
			}
		}
		for (int num3 = num; num3 >= 0 && num3 < route.Count; num3 += direction)
		{
			if (names.Contains(SectionRouting.Neighbour(route[num3].nameForPerson)))
			{
				return SectionRouting.Neighbour(route[num3].nameForPerson);
			}
		}
		if (list.Count != 1)
		{
			return "";
		}
		return SectionRouting.Neighbour(list[0].nameForPerson);
	}
}

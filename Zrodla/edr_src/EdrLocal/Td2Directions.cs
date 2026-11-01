using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace EdrLocal;

public static class Td2Directions
{
	private sealed class Scene
	{
		public string Name;

		public string In;

		public string Out;

		public string Id;

		public int Start = -1;

		public int End = -1;
	}

	private static bool SameLine(string a, string b)
	{
		if (!string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b))
		{
			return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private static string Clean(string value)
	{
		return (value ?? "").Trim();
	}

	private static List<Scene> Scenes(Td2Schedule schedule)
	{
		List<Scene> scenes = new List<Scene>();
		string[] array = (schedule.timetablePath ?? "").Split(';');
		foreach (string text in array)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				continue;
			}
			string[] array2 = text.Split(',');
			if (array2.Length >= 3)
			{
				string text2 = Regex.Replace(Td2Names.Plain(array2[1]), "\\s+[a-f0-9]{8}\\.sc$", "", RegexOptions.IgnoreCase).Trim();
				if (text2.Length > 0)
				{
					scenes.Add(new Scene
					{
						Name = text2,
						Id = Regex.Match(array2[1], "([a-f0-9]{8})\\.sc$", RegexOptions.IgnoreCase).Groups[1].Value.ToLowerInvariant(),
						In = Clean(array2[0]),
						Out = Clean(array2[2])
					});
				}
			}
		}
		List<Td2Stop> stopPoints = schedule.stopPoints;
		int val = 0;
		int j;
		for (j = 0; j < scenes.Count; j++)
		{
			Scene scene = scenes[j];
			int num = ((scene.In.Length != 0 || j != 0) ? stopPoints.FindIndex(Math.Min(val, stopPoints.Count), (Td2Stop x) => SameLine(x.arrivalLine, scene.In)) : 0);
			if (num < 0)
			{
				num = stopPoints.FindIndex(Math.Min(val, stopPoints.Count), (Td2Stop x) => Td2Names.Name(x).Equals(scene.Name, StringComparison.CurrentCultureIgnoreCase));
			}
			if (num < 0)
			{
				continue;
			}
			scene.Start = num;
			int num2 = ((scene.Out.Length == 0) ? (stopPoints.Count - 1) : stopPoints.FindIndex(num, (Td2Stop x) => SameLine(x.departureLine, scene.Out)));
			if (num2 < num)
			{
				num2 = stopPoints.Count - 1;
			}
			if (j + 1 < scenes.Count && scenes[j + 1].In.Length > 0)
			{
				int num3 = stopPoints.FindIndex(num + 1, (Td2Stop x) => SameLine(x.arrivalLine, scenes[j + 1].In));
				if (num3 >= 0 && num3 <= num2)
				{
					num2 = Math.Max(num, num3 - 1);
				}
			}
			scene.End = num2;
			val = Math.Max(num2 + 1, num + 1);
		}
		return scenes;
	}

	public static Dictionary<string, Tuple<string, string>> SceneLabels(Td2Schedule schedule)
	{
		List<Scene> source = Scenes(schedule);
		Dictionary<string, Tuple<string, string>> dictionary = new Dictionary<string, Tuple<string, string>>();
		int at;
		for (at = 0; at < schedule.stopPoints.Count; at++)
		{
			string pointId = schedule.stopPoints[at].pointId;
			if (!string.IsNullOrEmpty(pointId) && !dictionary.ContainsKey(pointId))
			{
				Scene scene = source.FirstOrDefault((Scene x) => x.Start >= 0 && at >= x.Start && at <= x.End);
				if (scene != null)
				{
					dictionary[pointId] = Tuple.Create(scene.Name, scene.Id);
				}
			}
		}
		return dictionary;
	}

	public static string SceneFor(Td2Schedule schedule, string point)
	{
		int at = schedule.stopPoints.FindIndex((Td2Stop x) => x.pointId == point);
		Scene scene = Scenes(schedule).FirstOrDefault((Scene x) => x.Start >= 0 && at >= x.Start && at <= x.End);
		if (scene != null)
		{
			return scene.Name;
		}
		return "";
	}

	public static Td2Direction At(Td2Schedule schedule, int at, int direction)
	{
		List<Td2Stop> stopPoints = schedule.stopPoints;
		Td2Stop td2Stop = stopPoints[at];
		int other;
		for (other = at + direction; other >= 0 && other < stopPoints.Count && !Td2Names.Operating(stopPoints[other]); other += direction)
		{
		}
		if (other < 0 || other >= stopPoints.Count)
		{
			return new Td2Direction();
		}
		string text = ((direction < 0) ? Clean(td2Stop.arrivalLine) : Clean(td2Stop.departureLine));
		Td2Direction td2Direction = new Td2Direction();
		td2Direction.Name = Td2Names.Name(stopPoints[other]);
		td2Direction.Line = text;
		Td2Direction td2Direction2 = td2Direction;
		List<Scene> list = Scenes(schedule);
		int num = list.FindIndex((Scene x) => at >= x.Start && at <= x.End && x.Start >= 0);
		int num2 = list.FindIndex((Scene x) => other >= x.Start && other <= x.End && x.Start >= 0);
		if (num >= 0 && num2 >= 0 && num != num2)
		{
			td2Direction2.Name = list[num2].Name;
			string text2 = ((direction < 0) ? list[num2].Out : list[num].Out);
			string text3 = ((direction < 0) ? list[num].In : list[num2].In);
			if (text2.Length > 0 && text3.Length > 0)
			{
				td2Direction2.Line = text2 + "/" + text3;
			}
		}
		td2Direction2.LocalLine = text;
		td2Direction2.Peer = Td2Names.Name(stopPoints[other]);
		if (num >= 0)
		{
			td2Direction2.SceneName = list[num].Name;
			td2Direction2.SceneId = list[num].Id;
		}
		if (num >= 0 && num2 >= 0)
		{
			td2Direction2.Kind = ((num == num2) ? "Wewnętrzny" : "Zewnętrzny");
			if (num != num2)
			{
				td2Direction2.LocalLine = ((direction < 0) ? list[num].In : list[num].Out);
				td2Direction2.Peer = "";
			}
		}
		if (td2Direction2.LocalLine.Length == 0)
		{
			td2Direction2.LocalLine = "?";
		}
		ref string routeKey = ref td2Direction2.RouteKey;
		string text4;
		if (td2Direction2.Kind == "Zewnętrzny")
		{
			text4 = "E:";
		}
		else
		{
			text4 = ((td2Direction2.Kind == "Wewnętrzny") ? "I:" : "U:");
		}
		routeKey = text4 + td2Direction2.LocalLine.ToUpperInvariant();
		return td2Direction2;
	}
}

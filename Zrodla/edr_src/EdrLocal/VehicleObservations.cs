using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace EdrLocal;

public sealed class VehicleObservations
{
	private sealed class Observation
	{
		public List<string> Vehicles;

		public string Run;

		public DateTime Utc;
	}

	private readonly Dictionary<string, Observation> entries = new Dictionary<string, Observation>();

	private string server;

	private static string Key(SimRailTrain train)
	{
		if (string.IsNullOrWhiteSpace(train.trainNoLocal) || train.timetable == null || train.timetable.Count == 0 || train.timetable.All((SimRailPoint x) => string.IsNullOrWhiteSpace(x.arrivalTime) && string.IsNullOrWhiteSpace(x.departureTime)))
		{
			return null;
		}
		List<string> list = new List<string>();
		list.Add(train.trainNoLocal);
		list.Add(train.trainName);
		list.Add(train.startStation);
		list.Add(train.endStation);
		List<string> list2 = list;
		foreach (SimRailPoint item in train.timetable.OrderBy((SimRailPoint x) => x.indexOfPoint))
		{
			list2.AddRange(new string[5]
			{
				item.indexOfPoint.ToString(CultureInfo.InvariantCulture),
				item.pointId,
				item.displayedTrainNumber,
				item.arrivalTime,
				item.departureTime
			});
		}
		string s = Json.Text(list2);
		using SHA256 sHA = SHA256.Create();
		return Convert.ToBase64String(sHA.ComputeHash(Encoding.UTF8.GetBytes(s)));
	}

	public void Apply(string code, IEnumerable<SimRailTrain> trains)
	{
		if (server != code)
		{
			entries.Clear();
			server = code;
		}
		HashSet<string> valid = new HashSet<string>();
		foreach (IGrouping<string, SimRailTrain> item in from x in trains
			where x != null
			group x by x.trainNoLocal)
		{
			if (item.Count() != 1)
			{
				foreach (SimRailTrain item2 in item)
				{
					string text = Key(item2);
					if (text != null)
					{
						entries.Remove(text);
					}
				}
				continue;
			}
			SimRailTrain simRailTrain = item.Single();
			string text2 = Key(simRailTrain);
			if (text2 != null)
			{
				valid.Add(text2);
				Observation value;
				if (simRailTrain.VehicleMatchRejected)
				{
					entries.Remove(text2);
				}
				else if (simRailTrain.Vehicles != null && simRailTrain.Vehicles.Count > 0 && !simRailTrain.VehiclesFromObservation && !string.IsNullOrWhiteSpace(simRailTrain.VehicleRunId))
				{
					simRailTrain.VehiclesObservedUtc = DateTime.UtcNow;
					entries[text2] = new Observation
					{
						Vehicles = new List<string>(simRailTrain.Vehicles),
						Run = simRailTrain.VehicleRunId,
						Utc = simRailTrain.VehiclesObservedUtc.Value
					};
				}
				else if (entries.TryGetValue(text2, out value))
				{
					simRailTrain.Vehicles = new List<string>(value.Vehicles);
					simRailTrain.VehicleRunId = value.Run;
					simRailTrain.VehiclesFromObservation = true;
					simRailTrain.VehiclesObservedUtc = value.Utc;
				}
			}
		}
		foreach (string item3 in entries.Keys.Where((string x) => !valid.Contains(x)).ToList())
		{
			entries.Remove(item3);
		}
	}
}

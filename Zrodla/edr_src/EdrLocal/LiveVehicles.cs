using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace EdrLocal;

public static class LiveVehicles
{
	public static bool Attach(string server, IEnumerable<SimRailTrain> timetable, IEnumerable<SimRailRunningTrain> live)
	{
		List<SimRailTrain> source = timetable.ToList();
		foreach (SimRailTrain item in source.Where((SimRailTrain x) => x != null))
		{
			item.Vehicles = null;
			item.VehicleRunId = null;
			item.VehiclesFromObservation = false;
			item.VehiclesObservedUtc = null;
			item.VehicleMatchRejected = false;
		}
		Dictionary<string, List<SimRailRunningTrain>> dictionary = (from x in live ?? Enumerable.Empty<SimRailRunningTrain>()
			where x != null && x.ServerCode == server && !string.IsNullOrEmpty(x.TrainNoLocal)
			group x by x.TrainNoLocal).ToDictionary((IGrouping<string, SimRailRunningTrain> x) => x.Key, (IGrouping<string, SimRailRunningTrain> x) => x.ToList());
		foreach (IGrouping<string, SimRailTrain> item2 in from x in source
			where x != null && !string.IsNullOrEmpty(x.trainNoLocal)
			group x by x.trainNoLocal)
		{
			if (item2.Count() != 1)
			{
				foreach (SimRailTrain item3 in item2)
				{
					item3.VehicleMatchRejected = true;
				}
				continue;
			}
			SimRailTrain simRailTrain = item2.Single();
			if (!dictionary.TryGetValue(simRailTrain.trainNoLocal, out var value))
			{
				continue;
			}
			if (value.Count != 1)
			{
				simRailTrain.VehicleMatchRejected = true;
				continue;
			}
			SimRailRunningTrain simRailRunningTrain = value[0];
			if (!Same(simRailTrain.startStation, simRailRunningTrain.StartStation) || !Same(simRailTrain.endStation, simRailRunningTrain.EndStation) || !Same(simRailTrain.trainName, simRailRunningTrain.TrainName) || string.IsNullOrWhiteSpace(simRailRunningTrain.RunId) || simRailRunningTrain.Vehicles == null || simRailRunningTrain.Vehicles.Count == 0)
			{
				simRailTrain.VehicleMatchRejected = true;
				continue;
			}
			simRailTrain.Vehicles = simRailRunningTrain.Vehicles.Where((string x) => !string.IsNullOrWhiteSpace(x)).ToList();
			simRailTrain.VehicleRunId = simRailRunningTrain.RunId;
		}
		return true;
	}

	private static bool Same(string a, string b)
	{
		if (!string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b))
		{
			return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	public static bool TryLoad(string server, List<SimRailTrain> timetable, CancellationToken token, Func<byte[]> read)
	{
		foreach (SimRailTrain item in timetable)
		{
			item.Vehicles = null;
			item.VehicleRunId = null;
			item.VehiclesFromObservation = false;
			item.VehiclesObservedUtc = null;
			item.VehicleMatchRejected = false;
		}
		try
		{
			token.ThrowIfCancellationRequested();
			SimRailEnvelope<SimRailRunningTrain> simRailEnvelope = SimRailProvider.Parse<SimRailEnvelope<SimRailRunningTrain>>(read());
			if (simRailEnvelope == null || !simRailEnvelope.result || simRailEnvelope.data == null)
			{
				return false;
			}
			return Attach(server, timetable, simRailEnvelope.data);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch
		{
			token.ThrowIfCancellationRequested();
			return false;
		}
	}
}

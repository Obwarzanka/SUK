using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace EdrLocal;

public static class ConsistPlan
{
	public static void TryLoad(List<SimRailTrain> trains, CancellationToken token, Func<byte[]> download)
	{
		try
		{
			token.ThrowIfCancellationRequested();
			Attach(trains, SimRailProvider.Parse<List<PlannedTrain>>(download()));
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch
		{
			token.ThrowIfCancellationRequested();
		}
	}

	public static void Attach(List<SimRailTrain> trains, List<PlannedTrain> plans)
	{
		if (plans == null)
		{
			return;
		}
		ILookup<string, PlannedTrain> lookup = plans.Where((PlannedTrain plannedTrain2) => plannedTrain2 != null && !string.IsNullOrWhiteSpace(plannedTrain2.trainNoLocal)).ToLookup((PlannedTrain plannedTrain2) => plannedTrain2.trainNoLocal);
		ILookup<string, SimRailTrain> lookup2 = trains.ToLookup((SimRailTrain simRailTrain) => simRailTrain.trainNoLocal);
		foreach (SimRailTrain train in trains)
		{
			List<PlannedTrain> list = lookup[train.trainNoLocal].ToList();
			if (lookup2[train.trainNoLocal].Count() != 1 || list.Count != 1)
			{
				continue;
			}
			PlannedTrain plannedTrain = list[0];
			if (train.trainName != plannedTrain.trainName || train.startStation != plannedTrain.startStation || train.endStation != plannedTrain.endStation || train.timetable == null || plannedTrain.timetable == null || train.timetable.Count == 0)
			{
				continue;
			}
			SimRailPoint[] array = train.timetable.OrderBy((SimRailPoint simRailPoint) => simRailPoint.indexOfPoint).ToArray();
			bool flag = true;
			int num = -1;
			SimRailPoint[] array2 = array;
			foreach (SimRailPoint x in array2)
			{
				var list2 = (from y in plannedTrain.timetable.Select((SimRailPoint y, int i) => new
					{
						Point = y,
						Index = i
					})
					where y.Point != null && x.pointId == y.Point.pointId && x.arrivalTime == y.Point.arrivalTime && x.departureTime == y.Point.departureTime && x.displayedTrainNumber == y.Point.displayedTrainNumber
					select y).ToList();
				if (list2.Count != 1 || list2[0].Index <= num)
				{
					flag = false;
					break;
				}
				num = list2[0].Index;
			}
			if (flag)
			{
				train.locoType = plannedTrain.locoType;
				train.trainLength = Positive(plannedTrain.trainLength);
				train.trainWeight = Positive(plannedTrain.trainWeight);
			}
		}
	}

	private static decimal? Positive(decimal? value)
	{
		if (!(value > (decimal?)0m))
		{
			return null;
		}
		return value;
	}

	public static TrainConsist Project(SimRailTrain t)
	{
		string text = Regex.Replace(t.locoType ?? "", "\\s*\\([^)]*\\)", "").Trim();
		List<ActualTraction> list = ActualTraction.Read(t.VehicleMatchRejected ? null : t.Vehicles);
		if (text.Length == 0 && !t.trainLength.HasValue && !t.trainWeight.HasValue && list.Count == 0)
		{
			return null;
		}
		TrainConsist trainConsist = new TrainConsist();
		trainConsist.PlannedSeries = ((text.Length == 0) ? new string[0] : new string[1] { text });
		trainConsist.Series = ((text.Length == 0) ? new string[0] : new string[1] { text });
		trainConsist.PlannedLength = Positive(t.trainLength);
		trainConsist.Length = Positive(t.trainLength);
		trainConsist.PlannedGross = Positive(t.trainWeight);
		trainConsist.Gross = Positive(t.trainWeight);
		trainConsist.Numbers = new string[0];
		TrainConsist trainConsist2 = trainConsist;
		if (list.Count > 0)
		{
			trainConsist2.Series = list.Select((ActualTraction x) => x.Series).ToArray();
			trainConsist2.Numbers = list.Select((ActualTraction x) => x.Number).ToArray();
		}
		return trainConsist2;
	}

	public static TrainConsist Merge(TrainConsist plan, TrainConsist local)
	{
		if (plan == null)
		{
			return local;
		}
		if (local == null)
		{
			return plan;
		}
		if (local.ManualAnalysis)
		{
			plan.ManualAnalysis = true;
			plan.Gross = local.Gross;
			plan.Length = local.Length;
			plan.Series = local.Series;
			plan.Numbers = local.Numbers;
			plan.VehicleTypes = local.VehicleTypes;
		}
		return plan;
	}

	public static bool SameCourse(Train a, Train b)
	{
		if (a.ExternalTrainNo == b.ExternalTrainNo && a.Origin == b.Origin && a.Destination == b.Destination && a.PlanArrival == b.PlanArrival)
		{
			return a.PlanDeparture == b.PlanDeparture;
		}
		return false;
	}

	public static string Number(decimal? value)
	{
		if (!value.HasValue)
		{
			return "";
		}
		return value.Value.ToString("0.###", CultureInfo.CurrentCulture);
	}

	public static void RouteCells(string[] cells, TrainConsist c)
	{
		if (c != null)
		{
			cells[23] = string.Join(", ", c.PlannedSeries ?? new string[0]);
			cells[24] = Number(c.PlannedLength);
			cells[25] = Number(c.PlannedGross);
			cells[26] = string.Join(", ", from x in (c.Series ?? new string[0]).Select((string x, int n) => (!string.IsNullOrWhiteSpace(x)) ? (x + ((c.Numbers != null && n < c.Numbers.Length && !string.IsNullOrWhiteSpace(c.Numbers[n])) ? ("-" + c.Numbers[n]) : "")) : "")
				where x.Length > 0
				select x);
			cells[27] = Number(c.Length);
			cells[28] = Number(c.Gross);
		}
	}
}

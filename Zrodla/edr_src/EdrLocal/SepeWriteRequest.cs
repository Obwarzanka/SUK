using System;
using System.Collections.Generic;
using System.Linq;

namespace EdrLocal;

public sealed class SepeWriteRequest
{
	public string TrainId;

	public string Number;

	public string ExternalNumber;

	public string Origin;

	public string Destination;

	public int PointIndex;

	public DateTime PlanArrival;

	public DateTime PlanDeparture;

	public bool ArrivalChanged;

	public bool DepartureChanged;

	public bool AdjustDepartureForArrival;

	public bool DepartureCorrection;

	public DateTime? Arrival;

	public DateTime? Departure;

	public static SepeWriteRequest From(Train train, bool arrival, bool departure)
	{
		SepeWriteRequest sepeWriteRequest = new SepeWriteRequest();
		sepeWriteRequest.TrainId = train.Id;
		sepeWriteRequest.Number = train.Number;
		sepeWriteRequest.ExternalNumber = train.ExternalTrainNo;
		sepeWriteRequest.PointIndex = train.ExternalPointIndex;
		sepeWriteRequest.PlanArrival = train.PlanArrival;
		sepeWriteRequest.PlanDeparture = train.PlanDeparture;
		sepeWriteRequest.Origin = train.Origin;
		sepeWriteRequest.Destination = train.Destination;
		sepeWriteRequest.ArrivalChanged = arrival;
		sepeWriteRequest.DepartureChanged = departure;
		sepeWriteRequest.Arrival = train.Arrival;
		sepeWriteRequest.Departure = train.Departure;
		return sepeWriteRequest;
	}

	private SimRailPoint Point(SimRailSnapshot snapshot, string pointId)
	{
		List<SimRailTrain> list = snapshot.Trains.Where((SimRailTrain x) => x.trainNoLocal == ExternalNumber).ToList();
		if (list.Count != 1)
		{
			return null;
		}
		List<SimRailPoint> list2 = list[0].timetable.Where((SimRailPoint x) => x.pointId == pointId && x.indexOfPoint == PointIndex).ToList();
		if (list2.Count != 1)
		{
			return null;
		}
		return list2[0];
	}

	private bool Matches(SimRailPoint point)
	{
		if (point != null && (point.displayedTrainNumber ?? ExternalNumber) == Number && SameMinute(PlanArrival, SimRailProvider.Time(point.arrivalTime)))
		{
			return SameMinute(PlanDeparture, SimRailProvider.Time(point.departureTime));
		}
		return false;
	}

	public void Validate(SimRailSession session)
	{
		if (!Matches(Point(session.Snapshot, session.PointId)))
		{
			throw new InvalidOperationException("Rozkład lub tożsamość pociągu uległy zmianie. Odśwież dane przed ponowieniem zapisu.");
		}
	}

	public bool Confirmed(SimRailSnapshot snapshot, string pointId)
	{
		SimRailPoint simRailPoint = Point(snapshot, pointId);
		if (Matches(simRailPoint) && simRailPoint.isConfirmed && (!ArrivalChanged || SameMinute(Arrival, SimRailProvider.Time(simRailPoint.actualArrivalTime))))
		{
			if (DepartureChanged)
			{
				if (!DepartureCorrection)
				{
					return SameMinute(Departure, SimRailProvider.Time(simRailPoint.actualDepartureTime));
				}
				return AtOrAfterMinute(SimRailProvider.Time(simRailPoint.actualDepartureTime), Arrival);
			}
			return true;
		}
		return false;
	}

	public static bool AtOrAfterMinute(DateTime? value, DateTime? floor)
	{
		if (value.HasValue && floor.HasValue)
		{
			return value.Value.Ticks / 600000000 >= floor.Value.Ticks / 600000000;
		}
		return false;
	}

	public static bool SameMinute(DateTime? a, DateTime? b)
	{
		if (a.HasValue == b.HasValue)
		{
			if (a.HasValue)
			{
				return a.Value.ToString("yyyyMMddHHmm") == b.Value.ToString("yyyyMMddHHmm");
			}
			return true;
		}
		return false;
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EdrLocal;

public sealed class StopForecastRequest
{
	public SepeWriteRequest Identity;

	public string PointId;

	public string Server;

	public StopForecastOperation Operation;

	public bool ExpectedStopped;

	public DateTime Arrival;

	public DateTime Departure;

	public string LastConfirmationError { get; private set; }

	public bool Stop
	{
		get
		{
			return Operation == StopForecastOperation.Stop;
		}
		set
		{
			Operation = (value ? StopForecastOperation.Stop : StopForecastOperation.Forecast);
		}
	}

	public bool IsStopChange => Operation != StopForecastOperation.Forecast;

	public string Description
	{
		get
		{
			if (Operation != StopForecastOperation.CancelStop)
			{
				if (!Stop)
				{
					return "prognozy";
				}
				return "postoju";
			}
			return "odwołania postoju";
		}
	}

	public static bool IsForecastQuestion(string message)
	{
		return Regex.IsMatch(Regex.Replace(message ?? "", "\\s+", " ").Trim(), "(?i)^(Nastąpiła próba wprowadzenia godziny późniejszej niż obecna\\. Czy chcesz wprowadzić prognozę\\??|An attempt was made to enter a time later than the current time\\. (Do you want|Would you like) to (enter|set) a forecast\\??)$");
	}

	public static StopForecastRequest From(Train train, SimRailSession session, bool stop, DateTime arrival, DateTime departure)
	{
		return From(train, session, stop ? StopForecastOperation.Stop : StopForecastOperation.Forecast, arrival, departure);
	}

	public static StopForecastRequest From(Train train, SimRailSession session, StopForecastOperation operation, DateTime arrival, DateTime departure)
	{
		StopForecastRequest stopForecastRequest = new StopForecastRequest();
		stopForecastRequest.Identity = SepeWriteRequest.From(train, arrival: false, departure: false);
		stopForecastRequest.PointId = session.PointId;
		stopForecastRequest.Server = session.ServerCode;
		stopForecastRequest.Operation = operation;
		stopForecastRequest.ExpectedStopped = train.Stopped;
		stopForecastRequest.Arrival = arrival;
		stopForecastRequest.Departure = departure;
		return stopForecastRequest;
	}

	public SimRailPoint Point(SimRailSnapshot snapshot)
	{
		if (snapshot == null || snapshot.ServerCode != Server)
		{
			return null;
		}
		List<SimRailTrain> list = snapshot.Trains.Where((SimRailTrain x) => x.trainNoLocal == Identity.ExternalNumber).ToList();
		if (list.Count != 1)
		{
			return null;
		}
		List<SimRailPoint> list2 = list[0].timetable.Where((SimRailPoint x) => x.pointId == PointId && x.indexOfPoint == Identity.PointIndex).ToList();
		if (list2.Count != 1)
		{
			return null;
		}
		SimRailPoint simRailPoint = list2[0];
		if (!((simRailPoint.displayedTrainNumber ?? Identity.ExternalNumber) == Identity.Number) || !SepeWriteRequest.SameMinute(SimRailProvider.Time(simRailPoint.arrivalTime), Identity.PlanArrival) || !SepeWriteRequest.SameMinute(SimRailProvider.Time(simRailPoint.departureTime), Identity.PlanDeparture) || !(list[0].startStation == Identity.Origin) || !(list[0].endStation == Identity.Destination))
		{
			return null;
		}
		return simRailPoint;
	}

	public void Validate(SimRailSession session)
	{
		if (session.PointId != PointId || session.ServerCode != Server || !session.HasWriteLink || Point(session.Snapshot) == null)
		{
			throw new InvalidOperationException("Zmieniono posterunek, rozkład lub uprawnienia SEPE. Otwórz okno ponownie po odświeżeniu danych.");
		}
		if (Operation != StopForecastOperation.CancelStop && Arrival > Departure)
		{
			throw new TrainTimeException("Przyjazd nie może być późniejszy od odjazdu.");
		}
		if (IsStopChange && (Point(session.Snapshot).isStoped != ExpectedStopped || ExpectedStopped == Stop))
		{
			throw new InvalidOperationException("Zmieniono stan Pociąg STÓJ. Odśwież dane i otwórz okno ponownie.");
		}
	}

	public bool Confirmed(SimRailSnapshot snapshot)
	{
		SimRailPoint simRailPoint = Point(snapshot);
		if (simRailPoint != null)
		{
			if (Operation != StopForecastOperation.CancelStop)
			{
				if ((!Stop || simRailPoint.isStoped) && SepeWriteRequest.SameMinute(SimRailProvider.Time(simRailPoint.actualArrivalTime), Arrival))
				{
					return SepeWriteRequest.SameMinute(SimRailProvider.Time(simRailPoint.actualDepartureTime), Departure);
				}
				return false;
			}
			return !simRailPoint.isStoped;
		}
		return false;
	}

	private string ConfirmationError(SimRailSnapshot snapshot)
	{
		SimRailPoint simRailPoint = Point(snapshot);
		if (simRailPoint == null)
		{
			return "API nie wskazuje właściwego pociągu, posterunku i doby.";
		}
		if (IsStopChange && simRailPoint.isStoped != Stop)
		{
			if (!Stop)
			{
				return "API nadal wskazuje wprowadzony Pociąg STÓJ.";
			}
			return "API nadal wskazuje brak Pociąg STÓJ.";
		}
		return "API nie potwierdziło żądanych godzin: przyjazd " + Ui.FullTime(Arrival) + ", odjazd " + Ui.FullTime(Departure) + ".";
	}

	public static async Task<bool> Read(StopForecastRequest request, SimRailSession session, Action<SimRailSnapshot> accept, CancellationToken token, int pause = 1500)
	{
		request.LastConfirmationError = null;
		for (int i = 0; i < 3; i++)
		{
			await Task.Delay(pause, token);
			try
			{
				SimRailSnapshot snapshot = await session.Feed.SnapshotAsync(session.ServerCode, token);
				token.ThrowIfCancellationRequested();
				accept?.Invoke(snapshot);
				if (request.Confirmed(snapshot))
				{
					request.LastConfirmationError = null;
					return true;
				}
				request.LastConfirmationError = request.ConfirmationError(snapshot);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception error)
			{
				request.LastConfirmationError = SepeDiagnostics.Reason(error);
			}
		}
		return false;
	}
}

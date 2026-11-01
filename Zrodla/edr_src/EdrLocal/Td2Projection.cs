using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace EdrLocal;

public static class Td2Projection
{
	private static Td2SnapshotData Data(SimRailSession session)
	{
		if (session.Snapshot.Td2 == null)
		{
			throw new InvalidDataException("Brak rozkładu TD2.");
		}
		return session.Snapshot.Td2;
	}

	public static Station SuggestStation(SimRailSession session)
	{
		return SuggestStation(session, null);
	}

	public static Station SuggestStation(SimRailSession session, Station area)
	{
		Td2Area.Validate(session, area);
		Td2SnapshotData td2SnapshotData = Data(session);
		SimRailLocation simRailLocation = td2SnapshotData.Locations().First((SimRailLocation x) => x.Id == session.PointId);
		Td2Stop stop = td2SnapshotData.Records.SelectMany((Td2Record x) => x.Schedule.stopPoints).First((Td2Stop x) => x.pointId == session.PointId);
		Station station = new Station();
		station.Id = "TD2-" + session.PointId;
		station.Name = simRailLocation.Name;
		station.SimRailPointId = session.PointId;
		station.Td2SceneName = simRailLocation.SceneName;
		station.Td2AreaPosts = ((area == null) ? new List<Td2AreaPost>() : Json.Clone(area.Td2AreaPosts ?? new List<Td2AreaPost>()));
		station.PostType = (Regex.IsMatch(Td2Names.Raw(stop), ",\\s*podg\\.?$", RegexOptions.IgnoreCase) ? "POSTERUNEK ODGAŁĘŹNY" : "STACJA");
		station.Tracks = new List<string>();
		station.Sections = new List<Section>();
		station.Checks = new List<string> { "Sprawdzenie łączności" };
		station.SimulatorTopologyVersion = 2;
		station.RouteNeighboursVersion = 2;
		Station station2 = station;
		foreach (Td2Record record in td2SnapshotData.Records)
		{
			foreach (Td2Visit item in Td2Area.Visits(record.Schedule, station2, session.PointId))
			{
				int[] array = new int[2] { -1, 1 };
				foreach (int num2 in array)
				{
					Td2Direction neighbour = Td2Directions.At(record.Schedule, (num2 < 0) ? item.First : item.Last, num2);
					if (neighbour.Name.Length != 0)
					{
						if (string.IsNullOrEmpty(station2.Td2SceneId) && neighbour.SceneId.Length > 0)
						{
							station2.Td2SceneName = neighbour.SceneName;
							station2.Td2SceneId = neighbour.SceneId;
						}
						if (!station2.Sections.Any((Section x) => x.Td2RouteKey == neighbour.RouteKey))
						{
							Section section = new Section();
							section.Id = "td2-section-" + station2.Sections.Count;
							section.Td2RouteKey = neighbour.RouteKey;
							section.Td2Kind = neighbour.Kind;
							section.Td2PeerName = neighbour.Peer;
							section.Td2LocalLine = neighbour.LocalLine;
							section.Td2SettingsVersion = 1;
							section.Name = ((neighbour.Kind == "Zewnętrzny") ? neighbour.LocalLine : neighbour.Name);
							section.Line = "";
							section.Tracks = 1;
							section.NormalIncomingTrack = 1;
							section.ArrivalEven = ((num2 < 0) ? Even(Td2Provider.Text(record.Schedule.trainInfo.trainNo)) : (!Even(Td2Provider.Text(record.Schedule.trainInfo.trainNo))));
							section.Basis = "";
							section.Conducted = true;
							Section section2 = section;
							Td2SectionSettings.Normalize(section2);
							station2.Sections.Add(section2);
						}
					}
				}
				string text = Td2Names.Lane(record.Schedule.stopPoints[item.Anchor])[1];
				if (text.Length > 0 && !station2.Tracks.Contains(text))
				{
					station2.Tracks.Add(text);
				}
			}
		}
		station2.Td2AreaCatalog = Td2Area.Choices(session, station2);
		if (StationSettings.NoStationTracks(station2))
		{
			station2.Tracks.Clear();
		}
		SectionRouting.LabelDuplicates(station2);
		return station2;
	}

	private static bool Even(string number)
	{
		if (number.Length > 0 && char.IsDigit(number[number.Length - 1]))
		{
			return (number[number.Length - 1] - 48) % 2 == 0;
		}
		return false;
	}

	public static List<Train> Project(SimRailSession session, Station station)
	{
		List<Train> list = new List<Train>();
		foreach (Td2Record record in Data(session).Records)
		{
			Td2Schedule schedule = record.Schedule;
			Td2TrainInfo trainInfo = schedule.trainInfo;
			List<Td2Stop> stopPoints = schedule.stopPoints;
			Td2IdentityResult td2IdentityResult = Td2Identity.Resolve(record);
			foreach (Td2Visit item in Td2Area.Visits(schedule, station, session.PointId))
			{
				Td2Stop td2Stop = stopPoints[item.First];
				Td2Stop td2Stop2 = stopPoints[item.Last];
				Td2Stop td2Stop3 = stopPoints[item.Anchor];
				DateTime? dateTime = Td2Provider.Time(td2Stop.arrivalTime) ?? Td2Provider.Time(td2Stop.departureTime);
				DateTime? dateTime2 = Td2Provider.Time(td2Stop2.departureTime) ?? Td2Provider.Time(td2Stop2.arrivalTime) ?? dateTime;
				DateTime? dateTime3 = Td2Provider.Time(td2Stop.arrivalRealTime);
				DateTime? dateTime4 = Td2Provider.Time(td2Stop2.departureRealTime);
				Td2Direction td2Direction = Td2Directions.At(schedule, item.First, -1);
				Td2Direction td2Direction2 = Td2Directions.At(schedule, item.Last, 1);
				string[] array = Td2Names.Lane(td2Stop3);
				bool externalConfirmed = Td2Provider.Flag(td2Stop.confirmed) && Td2Provider.Flag(td2Stop2.confirmed);
				string number = Td2Provider.Text(trainInfo.trainNo);
				list.Add(new Train
				{
					Id = "td2:" + session.ServerCode + ":" + record.Id + ":" + Td2Provider.Text(td2Stop3.entryId),
					StationId = station.Id,
					Number = number,
					Kind = Td2Names.Plain(trainInfo.trainCategoryCode),
					From = td2Direction.Name,
					To = td2Direction2.Name,
					StartsHere = stopPoints.Take(item.First).All(Td2Names.Sbl),
					EndsHere = stopPoints.Skip(item.Last + 1).All(Td2Names.Sbl),
					Td2FirstPointIndex = item.First,
					Td2LastPointIndex = item.Last,
					LineIn = Td2SectionSettings.AssignedLine(station, td2Direction.RouteKey),
					LineOut = Td2SectionSettings.AssignedLine(station, td2Direction2.RouteKey),
					Td2IncomingPair = td2Direction.Line,
					Td2OutgoingPair = td2Direction2.Line,
					Origin = Td2Names.Name(stopPoints.First((Td2Stop x) => !Td2Names.Sbl(x))),
					Destination = Td2Names.Name(stopPoints.Last((Td2Stop x) => !Td2Names.Sbl(x))),
					PlanArrival = dateTime.Value,
					PlanDeparture = dateTime2.Value,
					Arrival = (Td2Provider.Flag(td2Stop.confirmed) ? dateTime3 : ((DateTime?)null)),
					Departure = (Td2Provider.Flag(td2Stop2.confirmed) ? dateTime4 : ((DateTime?)null)),
					ArrivalForecast = dateTime3,
					Forecast = dateTime4,
					Platform = (StationSettings.NoStationTracks(station) ? "" : array[0]),
					Track = (StationSettings.NoStationTracks(station) ? "" : array[1]),
					Carrier = td2IdentityResult.Carrier,
					CarrierSource = td2IdentityResult.CarrierSource,
					SourceProvider = "TD2",
					Td2IncomingKey = td2Direction.RouteKey,
					Td2OutgoingKey = td2Direction2.RouteKey,
					ExternalTrainNo = record.Id,
					ExternalPointIndex = item.Anchor,
					ExternalConfirmed = externalConfirmed,
					Runs = record.Runs,
					RemovedFromSource = record.Removed,
					Stopped = stopPoints.Skip(item.First).Take(item.Last - item.First + 1).Any((Td2Stop x) => Td2Provider.Flag(x.isStopped)),
					Dangerous = Td2Provider.Flag(trainInfo.hasDangerousCargo),
					Extraordinary = (Td2Provider.Flag(trainInfo.skr) || Td2Provider.Flag(trainInfo.hasExtraDeliveries)),
					HighRisk = Td2Provider.Flag(trainInfo.twr),
					ExternalPlannedStop = Td2Provider.Number(td2Stop3.pointStopTime),
					StopType = Td2Names.Plain(td2Stop3.pointStopType),
					ReadyDriver = Td2Names.Plain(trainInfo.driverName),
					Notes = "",
					Analysis = "",
					Cargo = "",
					Profile = new TrainProfile
					{
						Name = td2IdentityResult.Name,
						OperationalNotes = array[2],
						Additional = Td2Names.Plain(trainInfo.warningNotes),
						GaugeExceeded = Td2Provider.Flag(trainInfo.skr)
					}
				});
			}
		}
		return list;
	}

	private static string Date(DateTime? value)
	{
		if (!value.HasValue)
		{
			return "";
		}
		return value.Value.ToString("dd-MM-yy");
	}

	private static string Hour(DateTime? value)
	{
		return Ui.Time(value);
	}

	private static string Difference(DateTime? a, DateTime? b)
	{
		if (!a.HasValue || !b.HasValue)
		{
			return "";
		}
		return ((int)(a.Value - b.Value).TotalMinutes).ToString();
	}

	public static IEnumerable<DetailRow> Route(SimRailSession session, Train train)
	{
		Td2Record record = Data(session).Find(train.ExternalTrainNo);
		if (record == null)
		{
			yield break;
		}
		List<Td2Stop> route = record.Schedule.stopPoints;
		for (int at = 0; at < route.Count; at++)
		{
			Td2Stop stop = route[at];
			if (!Td2Names.Sbl(stop))
			{
				DateTime? pa = Td2Provider.Time(stop.arrivalTime);
				DateTime? pd = Td2Provider.Time(stop.departureTime);
				DateTime? aa = Td2Provider.Time(stop.arrivalRealTime);
				DateTime? ad = Td2Provider.Time(stop.departureRealTime);
				if (at == (train.Td2FirstPointIndex ?? train.ExternalPointIndex) && train.LocalArrivalRecorded)
				{
					aa = train.Arrival;
				}
				if (at == (train.Td2LastPointIndex ?? train.ExternalPointIndex) && train.LocalDepartureRecorded)
				{
					ad = train.Departure;
				}
				string[] lane = Td2Names.Lane(stop);
				string[] cells = new string[29]
				{
					Date(pa),
					Hour(pa),
					Date(aa),
					Hour(aa),
					Difference(aa, pa),
					Td2Provider.Flag(stop.confirmed) ? "✓" : "",
					at.ToString(),
					Td2Names.Name(stop),
					Td2Provider.Number(stop.pointStopTime).HasValue ? Td2Provider.Number(stop.pointStopTime).Value.ToString("0.0", CultureInfo.CurrentCulture) : "",
					(aa.HasValue && ad.HasValue) ? Math.Max(0.0, (ad.Value - aa.Value).TotalMinutes).ToString("0.0", CultureInfo.CurrentCulture) : "",
					Td2Names.Plain(stop.pointStopType),
					null,
					(Ui.PlatformText(lane[0]) + " " + lane[1]).Trim(),
					Date(pd),
					Hour(pd),
					Date(ad),
					Hour(ad),
					Difference(ad, pd),
					train.Kind,
					train.Number,
					null,
					null,
					Td2Names.Plain(record.Schedule.trainInfo.driverName),
					null,
					null,
					null,
					null,
					null,
					null
				};
				ConsistPlan.RouteCells(cells, train.Consist);
				yield return new DetailRow
				{
					Id = "td2-route-" + Td2Provider.Text(stop.entryId),
					Kind = "route",
					Cells = cells
				};
			}
		}
	}

	internal static void Presentation(SimRailSession session, DataGridViewRow row, Train train)
	{
		Td2Record td2Record = Data(session).Find(train.ExternalTrainNo);
		if (td2Record == null || train.ExternalPointIndex >= td2Record.Schedule.stopPoints.Count)
		{
			return;
		}
		_ = td2Record.Schedule.stopPoints[train.ExternalPointIndex];
		if (string.IsNullOrWhiteSpace(td2Record.Schedule.stopPoints[train.Td2FirstPointIndex ?? train.ExternalPointIndex].arrivalTime))
		{
			row.Cells["pa"].Value = "";
			row.Cells["da"].Value = "";
			if (!train.Arrival.HasValue)
			{
				row.Cells["ra"].Value = "";
			}
		}
		if (string.IsNullOrWhiteSpace(td2Record.Schedule.stopPoints[train.Td2LastPointIndex ?? train.ExternalPointIndex].departureTime))
		{
			row.Cells["pd"].Value = "";
			row.Cells["dd"].Value = "";
			if (!train.Departure.HasValue)
			{
				row.Cells["rd"].Value = "";
			}
		}
	}
}

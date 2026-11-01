using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace EdrLocal;

public sealed class SimRailSession
{
	private sealed class ConnectionState
	{
		public SimRailSnapshot Snapshot;

		public string Source;

		public HashSet<string> Points;

		public SimulatorSessionLock Lock;

		public readonly Stopwatch Clock = Stopwatch.StartNew();

		public DateTime Anchor;

		public string LastError;
	}

	private ConnectionState connection = new ConnectionState();

	public readonly ISimulatorFeed Feed;

	public SimRailSnapshot Snapshot
	{
		get
		{
			return connection.Snapshot;
		}
		private set
		{
			connection.Snapshot = value;
		}
	}

	public string PointId { get; private set; }

	public string ServerCode => Snapshot.ServerCode;

	public bool IsTd2
	{
		get
		{
			if (Feed != null)
			{
				return Feed.ProviderId == "TD2";
			}
			return false;
		}
	}

	public Uri AccessUrl
	{
		get
		{
			if (IsTd2 || string.IsNullOrEmpty(accessSource) || !SepeLink.Inspect(accessSource).PointIds.Contains(PointId))
			{
				return null;
			}
			return SepeLink.Validate(accessSource, ServerCode, PointId);
		}
	}

	public bool HasWriteLink => AccessUrl != null;

	public DateTime Now => connection.Anchor + connection.Clock.Elapsed;

	public string LastError
	{
		get
		{
			return connection.LastError;
		}
		set
		{
			connection.LastError = value;
		}
	}

	public Uri PublicUrl => new Uri("https://api.simrail.eu:8093/?serverCode=" + Uri.EscapeDataString(ServerCode) + "&stationId=" + Uri.EscapeDataString(PointId) + "&lang=pl");

	private string accessSource
	{
		get
		{
			return connection.Source;
		}
		set
		{
			connection.Source = value;
		}
	}

	private HashSet<string> selectedPoints
	{
		get
		{
			return connection.Points;
		}
		set
		{
			connection.Points = value;
		}
	}

	internal SimulatorSessionLock SessionLock
	{
		get
		{
			return connection.Lock;
		}
		set
		{
			connection.Lock = value;
		}
	}

	public string StoragePointId
	{
		get
		{
			SimRailLocation simRailLocation = ControlledLocations().FirstOrDefault((SimRailLocation x) => x.Name.Equals("Starzyny", StringComparison.OrdinalIgnoreCase));
			if (simRailLocation != null)
			{
				return simRailLocation.Id;
			}
			return PointId;
		}
	}

	public SimRailSession(ISimulatorFeed feed, SimRailSnapshot snapshot, string point)
	{
		Feed = feed;
		PointId = point;
		Accept(snapshot);
	}

	public void Accept(SimRailSnapshot snapshot)
	{
		if (Snapshot != null && Snapshot.ServerCode != snapshot.ServerCode)
		{
			throw new InvalidOperationException("Zmiana serwera wymaga nowej sesji.");
		}
		Snapshot = snapshot;
		connection.Anchor = snapshot.GameTime + ((snapshot.DownloadedUtc == default(DateTime)) ? TimeSpan.Zero : (DateTime.UtcNow - snapshot.DownloadedUtc));
		connection.Clock.Restart();
		LastError = null;
	}

	internal SimRailSession CreateView()
	{
		SimRailSession simRailSession = new SimRailSession(Feed, Snapshot, PointId);
		simRailSession.connection = connection;
		return simRailSession;
	}

	public void SetControlledPoints(IEnumerable<string> points)
	{
		HashSet<string> hashSet = new HashSet<string>(points);
		HashSet<string> known = new HashSet<string>(from x in Snapshot.Locations()
			select x.Id);
		if (hashSet.Count == 0 || !hashSet.Contains(PointId) || hashSet.Any((string x) => !known.Contains(x)))
		{
			throw new ArgumentException("Wybierz posterunki z pobranego rozkładu, w tym bieżący posterunek.");
		}
		if (SessionLock != null)
		{
			SessionLock.Include(hashSet);
		}
		selectedPoints = hashSet;
	}

	public void SetAccessLink(string text)
	{
		SetAccessLink(text, allowOtherPoints: false);
	}

	public void SetAccessLink(string text, bool allowOtherPoints)
	{
		if (IsTd2)
		{
			throw new InvalidOperationException("TD2 pobiera tylko rozkład jazdy.");
		}
		SepeLinkInfo sepeLinkInfo = SepeLink.Inspect(text);
		if (sepeLinkInfo.ServerCode != ServerCode)
		{
			throw new ArgumentException("Link musi dotyczyć wybranego serwera.");
		}
		if (!allowOtherPoints && !sepeLinkInfo.PointIds.Contains(PointId))
		{
			throw new ArgumentException("Link musi dotyczyć wybranego serwera i posterunku.");
		}
		HashSet<string> known = new HashSet<string>(from x in Snapshot.Locations()
			select x.Id);
		if (sepeLinkInfo.PointIds.Any((string x) => !known.Contains(x)))
		{
			throw new ArgumentException("Nie znaleziono wszystkich posterunków z linku w rozkładzie serwera. Pobierz rozkład ponownie.");
		}
		if (sepeLinkInfo.PointIds.Contains(PointId))
		{
			SepeLink.Validate(text, ServerCode, PointId);
		}
		if (SessionLock != null)
		{
			SessionLock.Include(sepeLinkInfo.PointIds);
		}
		accessSource = text;
		if (selectedPoints != null)
		{
			selectedPoints.UnionWith(sepeLinkInfo.PointIds);
		}
	}

	public void SelectPoint(string point)
	{
		if (!((selectedPoints == null) ? Snapshot.Locations() : ControlledLocations()).Any((SimRailLocation x) => x.Id == point))
		{
			throw new InvalidOperationException("Posterunek nie został dodany do tej sesji.");
		}
		PointId = point;
	}

	public List<SimRailLocation> ControlledLocations()
	{
		List<SimRailLocation> source = Snapshot.Locations();
		if (selectedPoints != null)
		{
			return source.Where((SimRailLocation x) => selectedPoints.Contains(x.Id)).ToList();
		}
		HashSet<string> ids = new HashSet<string> { PointId };
		SimRailLocation simRailLocation = source.First((SimRailLocation x) => x.Id == PointId);
		if (simRailLocation.Name.Equals("Starzyny", StringComparison.OrdinalIgnoreCase) || simRailLocation.Name.Equals("Sprowa", StringComparison.OrdinalIgnoreCase))
		{
			foreach (SimRailLocation item2 in source.Where((SimRailLocation x) => x.Name.Equals("Starzyny", StringComparison.OrdinalIgnoreCase) || x.Name.Equals("Sprowa", StringComparison.OrdinalIgnoreCase)))
			{
				ids.Add(item2.Id);
			}
		}
		if (!string.IsNullOrEmpty(accessSource))
		{
			string[] pointIds = SepeLink.Inspect(accessSource).PointIds;
			foreach (string item in pointIds)
			{
				ids.Add(item);
			}
		}
		return source.Where((SimRailLocation x) => ids.Contains(x.Id)).ToList();
	}

	public Station SuggestStation(string point)
	{
		SimRailSession simRailSession = new SimRailSession(Feed, Snapshot, point);
		return simRailSession.SuggestStation();
	}

	public Station SuggestStation()
	{
		return SuggestStation(legacy: false);
	}

	public Station SuggestStation(bool legacy)
	{
		return SuggestStationVersion((!legacy) ? 2 : 0);
	}

	internal Station SuggestStationVersion(int version)
	{
		if (IsTd2)
		{
			return Td2Projection.SuggestStation(this);
		}
		SimRailLocation simRailLocation = Snapshot.Locations().First((SimRailLocation x) => x.Id == PointId);
		Station station = new Station();
		station.Id = "SIMRAIL-" + PointId;
		station.Name = simRailLocation.Name;
		station.SimRailPointId = PointId;
		station.SimulatorTopologyVersion = 2;
		station.RouteNeighboursVersion = version;
		station.Sections = new List<Section>();
		station.Tracks = new List<string>();
		station.Checks = new List<string> { "Sprawdzenie łączności" };
		Station station2 = station;
		foreach (SimRailTrain train in Snapshot.Trains)
		{
			List<SimRailPoint> list = train.timetable.OrderBy((SimRailPoint x) => x.indexOfPoint).ToList();
			for (int num = 0; num < list.Count; num++)
			{
				if (!(list[num].pointId == PointId))
				{
					continue;
				}
				SimRailPoint simRailPoint = list[num];
				int[] array = new int[2] { -1, 1 };
				foreach (int num3 in array)
				{
					string neighbour = version switch
					{
						1 => Snapshot.Topology.PriorNeighbour(list, num, num3, Snapshot.Topology.PointNames), 
						0 => Snapshot.Topology.LegacyNeighbour(list, num, num3, Snapshot.Topology.PointNames), 
						_ => Snapshot.Neighbour(list, num, num3), 
					};
					if (!string.IsNullOrEmpty(neighbour) && !(neighbour == station2.Name))
					{
						string line = Snapshot.Topology.Line(list, num, num3);
						if (!station2.Sections.Any((Section x) => x.Name == neighbour && x.Line == line))
						{
							station2.Sections.Add(new Section
							{
								Id = "sr-section-" + station2.Sections.Count,
								Name = neighbour,
								Line = line,
								Tracks = 1,
								NormalIncomingTrack = 1,
								ArrivalEven = ((num3 < 0) ? Even(simRailPoint.displayedTrainNumber) : (!Even(simRailPoint.displayedTrainNumber))),
								Basis = "Blokada samoczynna"
							});
						}
					}
				}
				string text = SimRailProvider.Value(simRailPoint.track);
				if (text.Length > 0 && !station2.Tracks.Contains(text))
				{
					station2.Tracks.Add(text);
				}
			}
		}
		if (!StationSettings.RequiresStationTracks(station2))
		{
			station2.Tracks.Clear();
		}
		else if (station2.Tracks.Count == 0)
		{
			station2.Tracks.AddRange(new string[2] { "1", "2" });
		}
		station2.Sections = (from x in station2.Sections
			orderby x.Line, x.Name
			select x).ToList();
		SectionRouting.LabelDuplicates(station2);
		return station2;
	}

	private static bool Even(string text)
	{
		if (int.TryParse(text, out var result))
		{
			return result % 2 == 0;
		}
		return false;
	}

	public List<Train> Project(Station station)
	{
		if (IsTd2)
		{
			return Td2Projection.Project(this, station);
		}
		List<Train> list = new List<Train>();
		foreach (SimRailTrain train in Snapshot.Trains)
		{
			List<SimRailPoint> route = train.timetable.OrderBy((SimRailPoint x) => x.indexOfPoint).ToList();
			int at;
			for (at = 0; at < route.Count; at++)
			{
				SimRailPoint simRailPoint = route[at];
				if (simRailPoint.pointId != PointId)
				{
					continue;
				}
				DateTime? dateTime = SimRailProvider.Time(simRailPoint.arrivalTime) ?? SimRailProvider.Time(simRailPoint.departureTime);
				DateTime? dateTime2 = SimRailProvider.Time(simRailPoint.departureTime) ?? dateTime;
				if (dateTime.HasValue && dateTime2.HasValue)
				{
					DateTime? dateTime3 = SimRailProvider.Time(simRailPoint.actualArrivalTime);
					DateTime? dateTime4 = SimRailProvider.Time(simRailPoint.actualDepartureTime);
					CarrierResult carrierResult = SimRailTrainIdentity.CarrierInfo(train, simRailPoint.trainType);
					list.Add(new Train
					{
						Id = "sr:" + ServerCode + ":" + train.trainNoLocal + ":" + PointId + ":" + simRailPoint.indexOfPoint + ":" + dateTime.Value.ToString("yyyyMMdd"),
						StationId = station.Id,
						Number = (simRailPoint.displayedTrainNumber ?? train.trainNoLocal),
						Kind = simRailPoint.trainType,
						StartsHere = (at == 0 && string.Equals(train.startStation, simRailPoint.nameForPerson, StringComparison.CurrentCultureIgnoreCase)),
						EndsHere = (at == route.Count - 1 && string.Equals(train.endStation, simRailPoint.nameForPerson, StringComparison.CurrentCultureIgnoreCase)),
						From = Snapshot.Topology.Neighbour(route, at, -1, from x in station.Sections
							where SectionRouting.SameLine(x.Line, Snapshot.Topology.Line(route, at, -1))
							select SectionRouting.Neighbour(x.Name)),
						To = Snapshot.Topology.Neighbour(route, at, 1, from x in station.Sections
							where SectionRouting.SameLine(x.Line, Snapshot.Topology.Line(route, at, 1))
							select SectionRouting.Neighbour(x.Name)),
						Origin = train.startStation,
						Destination = train.endStation,
						Carrier = carrierResult.Name,
						CarrierSource = carrierResult.Evidence,
						LineIn = Snapshot.Topology.Line(route, at, -1),
						LineOut = Snapshot.Topology.Line(route, at, 1),
						Track = (StationSettings.NoStationTracks(station) ? "" : SimRailProvider.Value(simRailPoint.track)),
						Platform = (StationSettings.NoStationTracks(station) ? "" : (simRailPoint.platform ?? "")),
						PlanArrival = dateTime.Value,
						PlanDeparture = dateTime2.Value,
						Arrival = (simRailPoint.isConfirmed ? dateTime3 : ((DateTime?)null)),
						Departure = (simRailPoint.isConfirmed ? dateTime4 : ((DateTime?)null)),
						ArrivalForecast = dateTime3,
						Forecast = dateTime4,
						Consist = ConsistPlan.Project(train),
						Stopped = simRailPoint.isStoped,
						Runs = simRailPoint.isActive,
						QualityTracked = train.isQualityTracked,
						Dangerous = train.isDangerousCargo,
						HighRisk = train.isHighRiskCargo,
						Extraordinary = (train.isOverGauge || train.isOtherExceptional || train.isOverWeight == true),
						Notes = (train.ownNotes ?? ""),
						Profile = new TrainProfile
						{
							Name = SimRailTrainIdentity.Name(train, simRailPoint.trainType),
							OperationalNotes = (train.usageNotes ?? "")
						},
						StopType = ((simRailPoint.stopTypeNumber == 1) ? "ph" : ((simRailPoint.stopTypeNumber == 2) ? "pt" : "")),
						ExternalPlannedStop = simRailPoint.plannedStop,
						SourceProvider = "SimRail",
						ExternalTrainNo = train.trainNoLocal,
						ExternalPointIndex = simRailPoint.indexOfPoint,
						ExternalConfirmed = simRailPoint.isConfirmed,
						ExternalConfirmedBy = simRailPoint.confirmedBy
					});
				}
			}
		}
		return list;
	}

	public IEnumerable<DetailRow> Route(Train train)
	{
		if (IsTd2)
		{
			foreach (DetailRow item in Td2Projection.Route(this, train))
			{
				yield return item;
			}
			yield break;
		}
		SimRailTrain source = Snapshot.Find(train.ExternalTrainNo);
		if (source == null)
		{
			yield break;
		}
		foreach (SimRailPoint p in source.timetable.OrderBy((SimRailPoint x) => x.indexOfPoint))
		{
			DateTime? pa = SimRailProvider.Time(p.arrivalTime);
			DateTime? pd = SimRailProvider.Time(p.departureTime);
			DateTime? aa = SimRailProvider.Time(p.actualArrivalTime);
			DateTime? ad = SimRailProvider.Time(p.actualDepartureTime);
			string[] cells = new string[29]
			{
				Date(pa),
				Hour(pa),
				Date(aa),
				Hour(aa),
				Delay(aa, pa),
				p.isConfirmed ? "✓" : "",
				p.indexOfPoint.ToString(),
				p.nameForPerson,
				p.plannedStop.ToString("0.0", CultureInfo.CurrentCulture),
				p.stopDuration.ToString("0.0", CultureInfo.CurrentCulture),
				(p.stopTypeNumber == 1) ? "ph" : ((p.stopTypeNumber == 2) ? "pt" : ""),
				null,
				(Ui.PlatformText(p.platform) + " " + SimRailProvider.Value(p.track)).Trim(),
				Date(pd),
				Hour(pd),
				Date(ad),
				Hour(ad),
				Delay(ad, pd),
				p.trainType,
				p.displayedTrainNumber,
				null,
				null,
				null,
				null,
				null,
				null,
				null,
				null,
				null
			};
			ConsistPlan.RouteCells(cells, train.Consist);
			if (p.pointId == PointId && p.indexOfPoint == train.ExternalPointIndex)
			{
				cells[20] = Hour(train.ReadyReported);
				cells[21] = Hour(train.ReadyRecordedAt);
				cells[22] = train.ReadyDriver ?? "";
			}
			yield return new DetailRow
			{
				Id = "remote-" + p.indexOfPoint,
				Kind = "route",
				Cells = cells
			};
		}
	}

	private static string Date(DateTime? time)
	{
		if (!time.HasValue)
		{
			return "";
		}
		return time.Value.ToString("dd-MM-yy");
	}

	private static string Hour(DateTime? time)
	{
		if (!time.HasValue)
		{
			return "";
		}
		return time.Value.ToString("HH:mm");
	}

	private static string Delay(DateTime? actual, DateTime? plan)
	{
		if (!actual.HasValue || !plan.HasValue)
		{
			return "";
		}
		return ((int)(actual.Value - plan.Value).TotalMinutes).ToString();
	}
}

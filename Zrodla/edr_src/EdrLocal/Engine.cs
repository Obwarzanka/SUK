using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace EdrLocal;

public class Engine
{
	private readonly Dictionary<string, string> organizationPaths = new Dictionary<string, string>();

	private UnitPreference unitPreference;

	internal SimRailSnapshot LastAppliedSnapshot;

	public string ConfigurationProfileWarning { get; private set; }

	public string OrganizationCaption => UnitCaption;

	public string UnitSettingsDirectory { get; set; }

	public string UnitProvider
	{
		get
		{
			if (!IsTd2)
			{
				if (SimRail == null && !State.Trains.Any((Train x) => x.SourceProvider == "SimRail"))
				{
					return "td2";
				}
				return "simrail";
			}
			return "td2";
		}
	}

	private string UnitFile
	{
		get
		{
			string text = ((!LocalPersistence.Enabled) ? null : (UnitSettingsDirectory ?? (Ui.Testing ? null : DataLocations.Root)));
			if (text != null)
			{
				return Path.Combine(text, "jednostka-" + UnitProvider + ".json");
			}
			return null;
		}
	}

	public string UnitCaption
	{
		get
		{
			if (this.unitPreference == null)
			{
				this.unitPreference = new UnitPreference
				{
					Version = 1,
					Caption = DefaultOrganization(UnitProvider)
				};
				string unitFile = UnitFile;
				if (unitFile != null && File.Exists(unitFile))
				{
					UnitPreference unitPreference = Json.Read<UnitPreference>(File.ReadAllBytes(unitFile));
					if (unitPreference == null || unitPreference.Version != 1 || string.IsNullOrWhiteSpace(unitPreference.Caption) || unitPreference.Caption.Length > 120)
					{
						throw new InvalidDataException("Nieprawidłowa nazwa jednostki organizacyjnej.");
					}
					this.unitPreference = unitPreference;
				}
			}
			return this.unitPreference.Caption;
		}
	}

	public Store Store { get; private set; }

	public LocalUser User { get; set; }

	public string StationId { get; set; }

	public AppState State => Store.State;

	public Station Station => State.Stations.First((Station s) => s.Id == StationId);

	public bool IsCoordinator
	{
		get
		{
			if (User != null)
			{
				return User.Role == "Koordynator";
			}
			return false;
		}
	}

	public Duty ActiveDuty => State.Duties.FirstOrDefault((Duty d) => d.StationId == StationId && !d.End.HasValue);

	public bool ReadOnlySession { get; set; }

	public bool CanWrite
	{
		get
		{
			if (!ReadOnlySession && User != null && User.Role != "Obserwator" && ActiveDuty != null)
			{
				return ActiveDuty.UserId == User.Id;
			}
			return false;
		}
	}

	public DateTime LoggedInAt { get; private set; }

	private string SavedProvider
	{
		get
		{
			if (!IsTd2)
			{
				if (SimRail == null && !State.Trains.Any((Train x) => x.StationId == StationId && x.SourceProvider == "SimRail"))
				{
					return "td2";
				}
				return "simrail";
			}
			return "td2";
		}
	}

	private string SavedServer
	{
		get
		{
			if (SimRail != null)
			{
				return SimRail.ServerCode;
			}
			Train train = State.Trains.FirstOrDefault((Train x) => x.StationId == StationId && (x.Id ?? "").StartsWith("sr:"));
			if (train != null)
			{
				return train.Id.Split(':')[1];
			}
			return "";
		}
	}

	public int AutomaticReleaseSeconds
	{
		get
		{
			if (Station.Options != null)
			{
				return Station.Options.AutomaticReleaseSeconds ?? 60;
			}
			return 60;
		}
	}

	public bool SepeWritesEnabled
	{
		get
		{
			if (Station.Options != null)
			{
				return Station.Options.WriteSepe != false;
			}
			return true;
		}
	}

	public string StationProfileDirectory { get; set; }

	private string ProfileRoot
	{
		get
		{
			object obj = StationProfileDirectory;
			if (obj == null)
			{
				if (!Ui.Testing)
				{
					return DataLocations.StationProfiles(UnitProvider);
				}
				obj = null;
			}
			return (string)obj;
		}
	}

	public SimRailSession SimRail { get; set; }

	public DateTime Now
	{
		get
		{
			if (SimRail != null)
			{
				return SimRail.Now;
			}
			return DateTime.Now;
		}
	}

	public string ConfigurationSimulator { get; set; }

	public bool IsTd2
	{
		get
		{
			if (SimRail == null)
			{
				return ConfigurationSimulator == "td2";
			}
			return SimRail.IsTd2;
		}
	}

	public string SimulatorServerName
	{
		get
		{
			if (SimRail != null)
			{
				if (!IsTd2)
				{
					return SimRail.ServerCode;
				}
				return Td2Provider.ServerName(SimRail.ServerCode);
			}
			return "";
		}
	}

	public string SimulatorCaption
	{
		get
		{
			if (!IsTd2)
			{
				return "SimRail";
			}
			return "TD2";
		}
	}

	public event Action<SepeWriteRequest> SimRailTimeChanged;

	private DateTime? ClosureEffectiveAt(JournalEntry entry)
	{
		DateTime at = ((entry.Telephone == null) ? entry.Time : entry.Telephone.EffectiveAt);
		if (entry.Effect != "ZamknijPoPrzejezdzie")
		{
			return at;
		}
		if (entry.Telephone == null)
		{
			return null;
		}
		return (from e in State.Entries
			where e.StationId == entry.StationId && e.SectionId == entry.SectionId && e.LineTrack == entry.LineTrack && !e.Cancelled && e.Type == "Pociąg" && e.Number == entry.Telephone.TrainNumber && e.Arrived.HasValue && e.Arrived >= at && e.Arrived <= Now
			orderby e.Arrived
			select e).FirstOrDefault()?.Arrived;
	}

	private bool ApplyConfiguratorProfile(Station current, Station profile)
	{
		if (profile == null || string.IsNullOrEmpty(profile.ConfiguratorRevision) || profile.ConfiguratorRevision == current.ConfiguratorRevision)
		{
			return false;
		}
		try
		{
			Station next = Json.Clone(profile);
			next.Id = current.Id;
			StationSettings.Normalize(next);
			Dictionary<string, string> dictionary = new Dictionary<string, string>();
			HashSet<string> used = new HashSet<string>();
			foreach (Section section in next.Sections)
			{
				List<Section> sections = current.Sections;
				Func<Section, bool> predicate = (Section x) => x.Id == section.Id;
				Section section2 = sections.FirstOrDefault(predicate);
				if (section2 == null)
				{
					List<Section> list = current.Sections.Where((Section x) =>
					{
						if (string.IsNullOrEmpty(section.Td2RouteKey))
						{
							if (x.Line == section.Line)
							{
								return string.Equals(x.Name, section.Name, StringComparison.CurrentCultureIgnoreCase);
							}
							return false;
						}
						return x.Td2RouteKey == section.Td2RouteKey && !x.Td2Archived;
					}).ToList();
					if (list.Count == 1)
					{
						section2 = list[0];
					}
				}
				string id = section.Id;
				if (section2 != null)
				{
					section.Id = section2.Id;
				}
				if (!used.Add(section.Id))
				{
					throw new Exception("Kilka szlaków wskazuje ten sam dziennik.");
				}
				dictionary[id] = section.Id;
			}
			foreach (JournalTabDefinition journalTab in next.JournalTabs)
			{
				journalTab.LeftId = dictionary[journalTab.LeftId];
				if (!string.IsNullOrEmpty(journalTab.RightId))
				{
					journalTab.RightId = dictionary[journalTab.RightId];
				}
			}
			foreach (Section item in current.Sections.Where((Section x) => !used.Contains(x.Id) && State.Entries.Any((JournalEntry e) => e.StationId == current.Id && e.SectionId == x.Id)))
			{
				Section section3 = Json.Clone(item);
				section3.Conducted = false;
				if (!string.IsNullOrEmpty(section3.Td2RouteKey))
				{
					section3.Td2Archived = true;
				}
				next.Sections.Add(section3);
			}
			foreach (string track in (from x in State.Entries
				where x.StationId == current.Id && !string.IsNullOrEmpty(x.Track)
				select x.Track).Concat(from x in State.Trains
				where x.StationId == current.Id && !string.IsNullOrEmpty(x.Track)
				select x.Track).Distinct())
			{
				if (!next.TrackSettings.Any((StationTrackSetting x) => x.Number == track))
				{
					next.TrackSettings.Add(new StationTrackSetting
					{
						Number = track,
						Visible = false
					});
				}
			}
			StationSettings.Validate(next, State);
			Change("Zastosowanie konfiguracji z konfiguratora", current.Id, (AppState state) =>
			{
				state.Stations[state.Stations.FindIndex((Station x) => x.Id == current.Id)] = next;
			});
			return true;
		}
		catch (Exception ex)
		{
			ConfigurationProfileWarning = "Nie zastosowano nowej konfiguracji posterunku " + current.Name + ": " + ex.Message + " Poprzednia konfiguracja i historia pozostały bez zmian.";
			return false;
		}
	}

	public List<string> DueChecks()
	{
		List<string> list = new List<string>();
		foreach (RequiredCheckSetting check in Station.RequiredChecks ?? new List<RequiredCheckSetting>())
		{
			if (!check.Enabled)
			{
				continue;
			}
			DateTime? latest = null;
			if (check.AfterDutyMinutes.HasValue && ActiveDuty != null && ActiveDuty.Start.AddMinutes(check.AfterDutyMinutes.Value) <= Now)
			{
				ref DateTime? reference = ref latest;
				reference = ActiveDuty.Start.AddMinutes(check.AfterDutyMinutes.Value);
			}
			string[] array = ((check.DailyChecks == false) ? "" : (check.Hours ?? "")).Split(new char[2] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string text in array)
			{
				if (!DateTime.TryParseExact(text.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
				{
					continue;
				}
				DateTime dateTime = Now.Date + result.TimeOfDay;
				if (dateTime > Now)
				{
					continue;
				}
				if (latest.HasValue)
				{
					DateTime value = dateTime;
					DateTime? dateTime2 = latest;
					if (!(value > dateTime2))
					{
						continue;
					}
				}
				latest = dateTime;
			}
			if (latest.HasValue && !State.Entries.Any((JournalEntry x) => x.StationId == StationId && !x.Cancelled && x.Type == "Sprawdzenie" && x.Time >= latest.Value && x.Time <= Now && (x.CheckName == check.Name || (x.Text ?? "").StartsWith(check.Name + ":"))))
			{
				list.Add(check.Name + " (" + latest.Value.ToString("HH:mm") + ")");
			}
		}
		return list;
	}

	public void SetOrganizationSnapshot(ConfiguratorRepository repository)
	{
		organizationPaths.Clear();
		foreach (ConfiguredStation item in repository.Data.Stations.Where((ConfiguredStation x) => x.Registered != false))
		{
			organizationPaths[item.Key] = repository.SectionCaption(item.UnitId);
		}
	}

	public void ImportControlledStationData()
	{
		if (!LocalPersistence.Enabled || SimRail == null)
		{
			return;
		}
		string fullName = Directory.GetParent(Store.DirectoryPath).FullName;
		foreach (SimRailLocation location in SimRail.ControlledLocations())
		{
			List<Station> stations = State.Stations;
			Func<Station, bool> predicate = (Station x) => x.SimRailPointId == location.Id;
			if (stations.Any(predicate))
			{
				continue;
			}
			string text = Path.Combine(fullName, location.Id);
			if (!File.Exists(Path.Combine(text, "edr.json")))
			{
				continue;
			}
			Store store = new Store(text);
			store.Load();
			AppState data = store.State;
			Station station = data.Stations.FirstOrDefault((Station x) => x.SimRailPointId == location.Id);
			if (station == null)
			{
				continue;
			}
			Store.Commit((AppState s) =>
			{
				s.Stations.Add(Json.Clone(station));
				s.Trains.AddRange(data.Trains.Where((Train x) => x.StationId == station.Id && !s.Trains.Any((Train y) => y.Id == x.Id)));
				s.Entries.AddRange(data.Entries.Where((JournalEntry x) => x.StationId == station.Id && !s.Entries.Any((JournalEntry y) => y.Id == x.Id)));
				s.Duties.AddRange(data.Duties.Where((Duty x) => x.StationId == station.Id && !s.Duties.Any((Duty y) => y.Id == x.Id)));
				s.Services.AddRange(data.Services.Where((ServiceRecord x) => x.StationId == station.Id && !s.Services.Any((ServiceRecord y) => y.Id == x.Id)));
				s.History.AddRange(data.History.Where((Audit x) => !s.History.Any((Audit y) => y.Time == x.Time && y.Entity == x.Entity && y.Action == x.Action && y.Before == x.Before && y.After == x.After)));
			});
		}
	}

	public void EnsureControlledStations()
	{
		if (SimRail == null)
		{
			return;
		}
		List<Station> suggestions = (from x in SimRail.ControlledLocations()
			select SimRail.SuggestStation(x.Id)).ToList();
		Store.Commit((AppState s) =>
		{
			foreach (Station proposed in suggestions)
			{
				List<Station> stations = s.Stations;
				Func<Station, bool> predicate = (Station x) => x.SimRailPointId == proposed.SimRailPointId;
				Station station = stations.FirstOrDefault(predicate);
				if (station == null)
				{
					StationSettings.Normalize(proposed);
					s.Stations.Add(proposed);
					station = proposed;
				}
				if (station.SimulatorIdentityVersion < 1)
				{
					string text = StationIdentity.KnownType(station.Name);
					if (text != null && (string.IsNullOrEmpty(station.PostType) || station.PostType == "STACJA WĘZŁOWA"))
					{
						station.PostType = text;
					}
					station.SimulatorIdentityVersion = 1;
				}
			}
		});
	}

	public void SwitchSimulatorStation(string id)
	{
		if (SimRail == null)
		{
			throw new InvalidOperationException("Brak sesji SimRail.");
		}
		Station target = State.Stations.First((Station x) => x.Id == id);
		if (!SimRail.ControlledLocations().Any((SimRailLocation x) => x.Id == target.SimRailPointId))
		{
			throw new InvalidOperationException("Posterunek nie należy do tego połączenia SEPE.");
		}
		string stationId = StationId;
		string pointId = SimRail.PointId;
		try
		{
			SimRail.SelectPoint(target.SimRailPointId);
			StationId = id;
			RepairSuggestedNeighbours();
			RepairRouteNeighbours();
			InitializeStationProfiles();
			RepairRouteNeighbours();
			ApplySimRailSnapshot(SimRail.Snapshot);
		}
		catch
		{
			StationId = stationId;
			SimRail.SelectPoint(pointId);
			throw;
		}
	}

	public static string DefaultOrganization(string provider)
	{
		if (!string.Equals(provider, "simrail", StringComparison.OrdinalIgnoreCase))
		{
			return "TTSK";
		}
		return "EDR";
	}

	public void SetUnitCaption(string text)
	{
		text = (text ?? "").Trim();
		if (text.Length > 120 || text.IndexOfAny(new char[3] { '\r', '\n', '\t' }) >= 0)
		{
			throw new ArgumentException("Nazwa jednostki może mieć do 120 znaków w jednym wierszu.");
		}
		UnitPreference unitPreference = new UnitPreference();
		unitPreference.Version = 1;
		unitPreference.Caption = ((text.Length == 0) ? DefaultOrganization(UnitProvider) : text);
		UnitPreference value = unitPreference;
		if (UnitFile != null)
		{
			ConfiguratorRepository.AtomicWrite(UnitFile, Json.Bytes(value));
		}
		this.unitPreference = value;
	}

	public Engine(Store store)
	{
		Store = store;
		StationId = store.State.Stations[0].Id;
		LoggedInAt = DateTime.Now;
	}

	public void RequireWrite()
	{
		if (!CanWrite)
		{
			throw new Exception("Najpierw przyjmij dyżur jako operator lub koordynator tego posterunku.");
		}
	}

	public void RequireCoordinator()
	{
		if (!IsCoordinator)
		{
			throw new Exception("Ta czynność wymaga roli koordynatora.");
		}
	}

	public void Change(string action, string entity, Action<AppState> edit)
	{
		string before = Snapshot(State, entity);
		Store.Commit((AppState s) =>
		{
			edit(s);
			s.History.Add(new Audit
			{
				Time = DateTime.Now,
				User = ((User == null) ? "SYSTEM" : User.Id),
				UserName = ((User == null) ? "SYSTEM" : User.Name),
				Action = action,
				Entity = entity,
				Before = before,
				After = Snapshot(s, entity)
			});
		});
	}

	private static string Snapshot(AppState s, string entity)
	{
		return Json.Text(StateCopies.Audit(s));
	}

	public void BeginDuty(DateTime time)
	{
		if (User.Role == "Obserwator")
		{
			throw new Exception("Obserwator nie może przyjąć dyżuru.");
		}
		if (ActiveDuty != null)
		{
			throw new Exception("Dyżur jest już prowadzony przez: " + ActiveDuty.UserName);
		}
		if (time > Now.AddMinutes(1.0))
		{
			throw new Exception("Początek dyżuru nie może przypadać w przyszłości.");
		}
		Change("Przyjęcie dyżuru", StationId, (AppState s) =>
		{
			s.Duties.Add(new Duty
			{
				Id = Guid.NewGuid().ToString("N"),
				StationId = StationId,
				UserId = User.Id,
				UserName = User.Name,
				Start = time
			});
			AddShiftNotes(s, time, "Dyżur przyjął: " + User.Name);
		});
	}

	public void EndDuty(DateTime time)
	{
		RequireWrite();
		if (time < ActiveDuty.Start || time > Now.AddMinutes(1.0))
		{
			throw new Exception("Godzina zdania musi mieścić się między początkiem dyżuru a chwilą obecną.");
		}
		Change("Zdanie dyżuru", StationId, (AppState s) =>
		{
			s.Duties.First((Duty d) => d.StationId == StationId && !d.End.HasValue).End = time;
			AddShiftNotes(s, time, "Dyżur zdał: " + User.Name);
		});
	}

	private void AddShiftNotes(AppState s, DateTime time, string text)
	{
		foreach (Section item in Station.Sections.Where(StationSettings.Conducted))
		{
			s.Entries.Add(Note(item.Id, time, text, "Służbowy"));
		}
		s.Entries.Add(Note("TEL", time, text, "Służbowy"));
	}

	public JournalEntry Note(string section, DateTime time, string text, string type)
	{
		JournalEntry journalEntry = new JournalEntry();
		journalEntry.Id = Guid.NewGuid().ToString("N");
		journalEntry.StationId = StationId;
		journalEntry.SectionId = section;
		journalEntry.Time = time;
		journalEntry.Text = text;
		journalEntry.Type = type;
		journalEntry.User = User.Name;
		journalEntry.LineTrack = ((!(type == "Pociąg")) ? 1 : 0);
		return journalEntry;
	}

	public string TrainNumber(Train t, Section sec, string direction)
	{
		if (t.SourceProvider == "TD2")
		{
			return t.Number;
		}
		if (!int.TryParse(t.Number, out var result) || t.Number.Length < 2)
		{
			return t.Number;
		}
		bool flag = ((direction == "Z") ? sec.ArrivalEven : (!sec.ArrivalEven));
		if (result % 2 == 0 != flag)
		{
			result = ((result % 2 == 0) ? (result + 1) : (result - 1));
		}
		return result.ToString();
	}

	public void SaveJournal(JournalEntry entry)
	{
		RequireJournalWrite(entry.SectionId);
		Train previousTrain = State.Trains.FirstOrDefault((Train x) => x.Id == entry.TrainId);
		entry = Json.Clone(entry);
		if (entry.Type == "Pociąg")
		{
			entry.LineTrack = JournalTrack(entry);
		}
		NormalizeReturnEntry(entry);
		PrepareTelephone(entry);
		ValidateTimes(entry);
		ValidateJournalTrainTimes(entry, previousTrain);
		if (entry.Type == "Pociąg" && string.IsNullOrWhiteSpace(entry.Number))
		{
			throw new Exception("Podaj numer pociągu.");
		}
		if (!Station.Sections.Any((Section x) => x.Id == entry.SectionId) && entry.SectionId != "TEL")
		{
			throw new Exception("Nieprawidłowy dziennik.");
		}
		if (!StationSettings.TrackAllowed(Station, entry.Track, State.Entries.FirstOrDefault((JournalEntry x) => x.Id == entry.Id)))
		{
			throw new Exception("Tor stacyjny nie należy do posterunku.");
		}
		Section section = Station.Sections.FirstOrDefault((Section x) => x.Id == entry.SectionId);
		if (section != null && (entry.LineTrack < 1 || entry.LineTrack > section.Tracks))
		{
			throw new Exception("Nieprawidłowy tor szlakowy.");
		}
		if (entry.Type == "Pociąg" && !string.IsNullOrEmpty(entry.TrainId) && State.Entries.Any((JournalEntry e) => e.Id != entry.Id && e.TrainId == entry.TrainId && e.Direction == entry.Direction && !e.Cancelled))
		{
			throw new Exception("Ten pociąg ma już aktywny zapis w tym kierunku. Edytuj istniejący wiersz.");
		}
		Change("Zapis w dzienniku", entry.Id, (AppState s) =>
		{
			JournalEntry journalEntry = s.Entries.FirstOrDefault((JournalEntry e) => e.Id == entry.Id);
			if (journalEntry != null)
			{
				entry.TelephoneOrdinal = journalEntry.TelephoneOrdinal;
				if (journalEntry.Cancelled)
				{
					throw new Exception("Wykreślonego wpisu nie można zmieniać.");
				}
				s.Entries.Remove(journalEntry);
			}
			entry.User = User.Name;
			RecordLineIndication(entry, journalEntry);
			s.Entries.Add(Json.Clone(entry));
			if (journalEntry != null && journalEntry.Track != entry.Track && s.StationTrackOccupations != null)
			{
				s.StationTrackOccupations.RemoveAll((StationTrackOccupation x) => x.StationId == entry.StationId && x.EntryId == entry.Id);
			}
			SynchronizeReturnPair(s, entry);
			if (OwnJournalTimeChanged(entry, journalEntry, previousTrain))
			{
				Synchronize(s, entry.TrainId, entry.Direction);
			}
		});
		NotifySimRailTime(entry.TrainId, previousTrain);
	}

	public static void ValidateTimes(JournalEntry e)
	{
		if (e.Left.HasValue && e.Arrived.HasValue && e.Arrived.Value < e.Left.Value)
		{
			throw new Exception("Przyjazd nie może być wcześniejszy niż odjazd. Przy przejściu przez północ ustaw datę następnego dnia.");
		}
	}

	private static void Synchronize(AppState s, string id, string direction)
	{
		Train train = s.Trains.FirstOrDefault((Train x) => x.Id == id);
		if (train == null)
		{
			return;
		}
		IOrderedEnumerable<JournalEntry> source = from x in s.Entries
			where x.TrainId == id && !x.Cancelled && x.Type == "Pociąg"
			orderby x.Time descending
			select x;
		JournalEntry journalEntry = source.FirstOrDefault((JournalEntry x) => x.Direction == "Z");
		JournalEntry journalEntry2 = source.FirstOrDefault((JournalEntry x) => x.Direction == "DO");
		if (train.SourceProvider == "SimRail")
		{
			if (direction == "Z")
			{
				train.ExternalManualArrival = true;
			}
			if (direction == "DO")
			{
				train.ExternalManualDeparture = true;
			}
			train.ExternalManualTimes = train.ExternalManualArrival || train.ExternalManualDeparture;
		}
		if (train.SourceProvider == "TD2")
		{
			if (direction == "Z")
			{
				train.LocalArrivalRecorded = true;
			}
			if (direction == "DO")
			{
				train.LocalDepartureRecorded = true;
			}
		}
		if (direction == "Z")
		{
			train.Arrival = journalEntry?.Arrived;
		}
		if (direction == "DO")
		{
			train.Departure = journalEntry2?.Left;
			if (train.SourceProvider == "SimRail")
			{
				train.LocalDepartureRecorded = train.Departure.HasValue;
			}
			if (train.Departure.HasValue && train.SourceProvider != "SimRail")
			{
				train.Stopped = false;
			}
		}
	}

	public void Cancel(string id, string reason)
	{
		RequireJournalWrite(State.Entries.First((JournalEntry x) => x.Id == id).SectionId);
		if (string.IsNullOrWhiteSpace(reason))
		{
			throw new Exception("Podaj przyczynę wykreślenia.");
		}
		Change("Wykreślenie wpisu", id, (AppState s) =>
		{
			JournalEntry journalEntry = s.Entries.First((JournalEntry x) => x.Id == id);
			if (journalEntry.Cancelled)
			{
				throw new Exception("Wpis jest już wykreślony.");
			}
			if (journalEntry.Type == "Służbowy")
			{
				throw new Exception("Wpisów przyjęcia i zdania dyżuru nie można wykreślać.");
			}
			CancelReturnPair(s, journalEntry, reason);
			if ((journalEntry.Direction == "Z") ? journalEntry.Arrived.HasValue : journalEntry.Left.HasValue)
			{
				Synchronize(s, journalEntry.TrainId, journalEntry.Direction);
			}
		});
	}

	public string LineStatus(string section, int track)
	{
		List<JournalEntry> source = State.Entries.Where((JournalEntry e) => e.StationId == StationId && e.SectionId == section && e.LineTrack == track && !e.Cancelled).ToList();
		var anon = (from e in (from e in source
				where e.Effect == "Zamknij" || e.Effect == "ZamknijPoPrzejezdzie" || e.Effect == "Otwórz"
				select new
				{
					Entry = e,
					At = ClosureEffectiveAt(e)
				} into e
				where e.At.HasValue && e.At <= Now
				select e).Reverse()
			orderby e.At descending
			select e).FirstOrDefault();
		if (anon != null && anon.Entry.Effect != "Otwórz")
		{
			return "ZAMKNIĘTY";
		}
		string[] array = (from e in source
			where e.Type == "Pociąg" && LineMovementVisible(e)
			select e.Number).ToArray();
		if (array.Length <= 0)
		{
			return "WOLNY";
		}
		return "ZAJĘTY: " + string.Join(", ", array);
	}

	public string CurrentBasis(string section, int track, string direction = "Z")
	{
		JournalEntry journalEntry = (from e in State.Entries.Where((JournalEntry e) => e.StationId == StationId && e.SectionId == section && e.LineTrack == track && !e.Cancelled && (e.Effect == "Telefoniczne" || e.Effect == "Normalne") && (e.Telephone == null || e.Telephone.EffectiveAt <= Now)).Reverse()
			orderby (e.Telephone != null) ? e.Telephone.EffectiveAt : e.Time descending
			select e).FirstOrDefault();
		if (journalEntry == null || !(journalEntry.Effect == "Telefoniczne"))
		{
			return NormalBasis(section, track, direction);
		}
		if (journalEntry.Telephone == null || !(journalEntry.Telephone.TemplateId == "16a"))
		{
			return "Zapowiadanie telefoniczne";
		}
		return "Zapowiadanie radiotelefoniczne";
	}

	public string ResetSimRailJournals()
	{
		LocalPersistence.RequireEnabled();
		RequireCoordinator();
		if (SimRail == null || Store.IsTransient)
		{
			throw new InvalidOperationException("Reset dotyczy lokalnej sesji SimRail.");
		}
		string text = Path.Combine(Store.DirectoryPath, "przed-resetem-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".json");
		File.WriteAllBytes(text, Json.Bytes(State));
		HashSet<string> entities = new HashSet<string>((from x in State.Entries
			where x.StationId == StationId
			select x.Id).Concat(from x in State.Trains
			where x.StationId == StationId
			select x.Id).Concat(from x in State.Services
			where x.StationId == StationId
			select x.Id).Concat(from x in State.Duties
			where x.StationId == StationId
			select x.Id));
		entities.Add(StationId);
		List<Train> projected = SimRail.Project(Station);
		Store.Commit((AppState s) =>
		{
			s.Entries.RemoveAll((JournalEntry x) => x.StationId == StationId);
			s.Duties.RemoveAll((Duty x) => x.StationId == StationId);
			s.Services.RemoveAll((ServiceRecord x) => x.StationId == StationId);
			s.Trains.RemoveAll((Train x) => x.StationId == StationId);
			s.Trains.AddRange(projected);
			s.History.RemoveAll((Audit x) => entities.Contains(x.Entity));
		});
		return text;
	}

	public void StartSession(LocalUser user)
	{
		User = user;
		LoggedInAt = DateTime.Now;
	}

	private static bool OwnJournalTimeChanged(JournalEntry entry, JournalEntry old, Train train)
	{
		DateTime? dateTime = ((entry.Direction == "Z") ? entry.Arrived : entry.Left);
		if (old == null)
		{
			return dateTime.HasValue;
		}
		DateTime? dateTime2 = ((entry.Direction == "Z") ? old.Arrived : old.Left);
		if (train == null || !(train.SourceProvider == "SimRail"))
		{
			DateTime? dateTime3 = dateTime;
			DateTime? dateTime4 = dateTime2;
			if (dateTime3.HasValue == dateTime4.HasValue)
			{
				if (dateTime3.HasValue)
				{
					return dateTime3.GetValueOrDefault() != dateTime4.GetValueOrDefault();
				}
				return false;
			}
			return true;
		}
		return !SepeWriteRequest.SameMinute(dateTime, dateTime2);
	}

	public JournalTimeFields DocumentationFields(JournalEntry entry)
	{
		JournalTimeFields journalTimeFields = new JournalTimeFields();
		if (entry.Type != "Pociąg")
		{
			return journalTimeFields;
		}
		Section section = Station.Sections.FirstOrDefault((Section x) => x.Id == entry.SectionId);
		if (section == null)
		{
			return journalTimeFields;
		}
		string value = CurrentBasis(section.Id, (entry.LineTrack > 0) ? entry.LineTrack : JournalTrack(entry), entry.Direction);
		bool flag = TrafficBasis.IsTelephone(value);
		bool flag2 = flag || TrafficBasis.IsSemiAutomatic(value);
		bool flag3 = !string.IsNullOrEmpty(entry.ReturnOfId);
		bool flag4 = section.Tracks == 1 || CurrentSingleTrackTraffic(section.Id) || Enumerable.Range(1, section.Tracks).Any((int n) => LineStatus(section.Id, n) == "ZAMKNIĘTY");
		journalTimeFields.Permission = !flag3 && (flag || flag4);
		journalTimeFields.Left = !flag3 && (entry.Direction == "DO" || flag2);
		journalTimeFields.Arrived = !entry.ReturnFromLine && (entry.Direction == "Z" || flag2);
		return journalTimeFields;
	}

	public JournalEntry RegisterScheduledTrain(string trainId, string sectionId)
	{
		RequireWrite();
		Train t = State.Trains.First((Train x) => x.Id == trainId && x.StationId == StationId);
		bool flag = State.Entries.Any((JournalEntry x) => x.TrainId == t.Id && x.Direction == "Z" && !x.Cancelled);
		bool flag2 = State.Entries.Any((JournalEntry x) => x.TrainId == t.Id && x.Direction == "DO" && !x.Cancelled);
		if (flag && flag2)
		{
			return null;
		}
		string text = (flag ? "DO" : "Z");
		if (!flag && TrainDirections.Starts(t, Station))
		{
			text = "DO";
		}
		if (flag2 && text == "DO")
		{
			return null;
		}
		if (flag && TrainDirections.Ends(t, Station))
		{
			return null;
		}
		List<Section> list = SectionRouting.Candidates(Station.Sections, t, text);
		Section section;
		if (sectionId != null)
		{
			section = Station.Sections.FirstOrDefault((Section x) => x.Id == sectionId);
		}
		else
		{
			section = ((list.Count == 1) ? list[0] : null);
		}
		if (section == null)
		{
			throw new Exception("Nie można jednoznacznie wyznaczyć dziennika według kierunku i numeru linii. Użyj polecenia przyjęcia / wyprawienia na inny szlak.");
		}
		JournalEntry journalEntry = Note(section.Id, Now, "", "Pociąg");
		journalEntry.Direction = text;
		journalEntry.TrainId = t.Id;
		journalEntry.Number = TrainNumber(t, section, text);
		journalEntry.Track = (Station.Tracks.Contains(t.Track) ? t.Track : "");
		journalEntry.LineTrack = JournalTrack(journalEntry);
		SaveJournal(journalEntry);
		return journalEntry;
	}

	public static DateTime? JournalTime(string value, DateTime date, DateTime? previous, DateTime? notBefore)
	{
		value = (value ?? "").Trim();
		if (value.Length == 0)
		{
			return null;
		}
		if (DateTime.TryParseExact(value, new string[3] { "dd.MM.yyyy HH:mm", "yyyy-MM-dd HH:mm", "dd-MM-yyyy HH:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
		{
			return result;
		}
		if (Regex.IsMatch(value, "^\\d{3,4}$"))
		{
			value = value.Insert(value.Length - 2, ":");
		}
		if (!DateTime.TryParseExact(value, new string[4] { "H:mm", "HH:mm", "H.mm", "HH.mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
		{
			throw new Exception("Wpisz godzinę HH:mm lub HHmm, ewentualnie pełną datę dd.MM.rrrr HH:mm. Puste pole usuwa godzinę.");
		}
		DateTime dateTime = (previous ?? date).Date.AddHours(result.Hour).AddMinutes(result.Minute);
		if (notBefore.HasValue && dateTime < notBefore.Value && notBefore.Value.TimeOfDay.TotalHours >= 18.0 && result.Hour < 6)
		{
			dateTime = dateTime.AddDays(1.0);
		}
		return dateTime;
	}

	public int JournalTrack(JournalEntry entry)
	{
		if (!string.IsNullOrEmpty(entry.ReturnOfId))
		{
			JournalEntry journalEntry = State.Entries.FirstOrDefault((JournalEntry x) => x.Id == entry.ReturnOfId && !x.Cancelled);
			if (journalEntry != null)
			{
				return journalEntry.LineTrack;
			}
		}
		Section section = Station.Sections.FirstOrDefault((Section x) => x.Id == entry.SectionId);
		if (section == null)
		{
			throw new InvalidOperationException("Nie znaleziono szlaku wpisu pociągu.");
		}
		return JournalLineTracks.ForEntry(section, entry, State.Entries.FirstOrDefault((JournalEntry x) => x.Id == entry.Id));
	}

	private IEnumerable<JournalEntry> LineConditions(string section)
	{
		return from e in State.Entries.Where((JournalEntry e) => e.StationId == StationId && e.SectionId == section && !e.Cancelled && (e.Telephone == null || e.Telephone.EffectiveAt <= Now)).Reverse()
			orderby (e.Telephone != null) ? e.Telephone.EffectiveAt : e.Time descending
			select e;
	}

	public bool CurrentBidirectional(string section, int track)
	{
		JournalEntry journalEntry = LineConditions(section).FirstOrDefault((JournalEntry e) => e.Effect == "RuchDwutorowy" || (e.LineTrack == track && (e.Effect == "RuchJednotorowy" || e.Effect == "RuchDwukierunkowy" || e.Effect == "RuchJednokierunkowy")));
		if (journalEntry != null && journalEntry.Effect != "RuchDwutorowy")
		{
			return journalEntry.Effect != "RuchJednokierunkowy";
		}
		Section section2 = Station.Sections.First((Section x) => x.Id == section);
		LineTrackSetting lineTrackSetting = (section2.LineTracks ?? new List<LineTrackSetting>()).FirstOrDefault((LineTrackSetting x) => x.Number == track);
		if (lineTrackSetting != null)
		{
			return string.Equals(lineTrackSetting.TrafficType, "Dwukierunkowy", StringComparison.CurrentCultureIgnoreCase);
		}
		return false;
	}

	public bool CurrentSingleTrackTraffic(string section)
	{
		JournalEntry journalEntry = LineConditions(section).FirstOrDefault((JournalEntry e) => e.Effect == "RuchJednotorowy" || e.Effect == "RuchDwutorowy" || e.Effect == "RuchDwukierunkowy");
		if (journalEntry != null)
		{
			return journalEntry.Effect == "RuchJednotorowy";
		}
		return false;
	}

	public bool ElectricTractionAllowed(string section, int track)
	{
		JournalEntry journalEntry = LineConditions(section).FirstOrDefault((JournalEntry e) => e.LineTrack == track && (e.Effect == "TrakcjaWylaczona" || e.Effect == "TrakcjaPrzywrocona"));
		if (journalEntry != null)
		{
			return journalEntry.Effect == "TrakcjaPrzywrocona";
		}
		return true;
	}

	public string AuditUserName(Audit audit)
	{
		if (!string.IsNullOrWhiteSpace(audit.UserName))
		{
			return audit.UserName;
		}
		LocalUser localUser = State.Users.FirstOrDefault((LocalUser u) => u.Id == audit.User);
		if (localUser != null)
		{
			return localUser.Name;
		}
		return audit.User ?? "";
	}

	public static byte[] ReadPortableFile(string path)
	{
		LocalPersistence.RequireEnabled();
		if (new FileInfo(path).Length > 67108864)
		{
			throw new InvalidDataException("Plik przekracza 64 MB.");
		}
		return File.ReadAllBytes(path);
	}

	private void CheckTarget(string provider, string server, string point, string station)
	{
		if (provider != SavedProvider || server != SavedServer || ((!string.IsNullOrEmpty(Station.SimRailPointId)) ? (point != Station.SimRailPointId) : (station != StationId)))
		{
			throw new InvalidDataException("Dane dotyczą innego symulatora, serwera lub posterunku. Otwórz właściwy posterunek przed wczytaniem.");
		}
	}

	public void ExportStation(string path)
	{
		EnsureExportPath(path);
		Station station = Json.Clone(Station);
		StationSettings.Normalize(station);
		ConfiguratorRepository.AtomicWrite(Path.GetFullPath(path), Json.Bytes(new StationProfile
		{
			SchemaVersion = 1,
			PointId = (station.SimRailPointId ?? station.Id),
			SavedUtc = DateTime.UtcNow,
			Station = station
		}));
	}

	public void ImportStation(string path)
	{
		RequireCoordinator();
		StationProfile stationProfile = Json.Read<StationProfile>(ReadPortableFile(path));
		if (stationProfile == null || stationProfile.SchemaVersion != 1 || stationProfile.Station == null)
		{
			throw new InvalidDataException("To nie jest plik konfiguracji posterunku.");
		}
		Station station = Json.Clone(stationProfile.Station);
		if (station.Sections == null || station.Tracks == null)
		{
			throw new InvalidDataException("Brak szlaków lub torów w konfiguracji.");
		}
		if ((!string.IsNullOrEmpty(Station.SimRailPointId)) ? (station.SimRailPointId != Station.SimRailPointId) : (station.Id != StationId))
		{
			throw new InvalidDataException("Konfiguracja dotyczy innego posterunku.");
		}
		StationSettings.Normalize(station);
		station.ConfiguratorRevision = Guid.NewGuid().ToString("N");
		if (!ApplyConfiguratorProfile(Station, station))
		{
			throw new InvalidDataException(ConfigurationProfileWarning ?? "Nie można zastosować konfiguracji.");
		}
		if (SimRail != null && Station.SimulatorReviewed)
		{
			WriteStationProfile(Station);
		}
	}

	public WorkingDocument ExportWorking(string path, bool session)
	{
		EnsureExportPath(path);
		WorkingDocument workingDocument = new WorkingDocument();
		workingDocument.Version = 1;
		workingDocument.Kind = (session ? "sesja" : "rozklad");
		workingDocument.Provider = SavedProvider;
		workingDocument.Server = SavedServer;
		workingDocument.Point = Station.SimRailPointId;
		workingDocument.StationId = StationId;
		workingDocument.StationName = Station.Name;
		workingDocument.SavedUtc = DateTime.UtcNow;
		workingDocument.Sections = Station.Sections.Select((Section x) => new SavedSection
		{
			Id = x.Id,
			Name = x.Name,
			Line = x.Line
		}).ToList();
		workingDocument.Trains = Json.Clone(State.Trains.Where((Train x) => x.StationId == StationId).ToList());
		WorkingDocument workingDocument2 = workingDocument;
		if (session)
		{
			workingDocument2.Entries = Json.Clone(State.Entries.Where((JournalEntry x) => x.StationId == StationId).ToList());
			workingDocument2.Duties = Json.Clone(State.Duties.Where((Duty x) => x.StationId == StationId).ToList());
			workingDocument2.Services = Json.Clone(State.Services.Where((ServiceRecord x) => x.StationId == StationId).ToList());
			HashSet<string> ids = new HashSet<string>(workingDocument2.Entries.Select((JournalEntry x) => x.Id).Concat(workingDocument2.Trains.Select((Train x) => x.Id)).Concat(workingDocument2.Duties.Select((Duty x) => x.Id))
				.Concat(workingDocument2.Services.Select((ServiceRecord x) => x.Id)));
			ids.Add(StationId);
			workingDocument2.History = Json.Clone(State.History.Where((Audit x) => ids.Contains(x.Entity)).ToList());
		}
		ConfiguratorRepository.AtomicWrite(Path.GetFullPath(path), Json.Bytes(workingDocument2));
		return workingDocument2;
	}

	private void EnsureExportPath(string path)
	{
		LocalPersistence.RequireEnabled();
		if (Path.GetFullPath(path).Equals(Store.FilePath, StringComparison.OrdinalIgnoreCase) || Path.GetFullPath(path).Equals(Store.FilePath + ".bak", StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException("Wybierz inne miejsce niż aktywna baza i jej kopia.");
		}
	}

	public void ImportWorking(string path, bool session)
	{
		RequireCoordinator();
		WorkingDocument doc = Json.Read<WorkingDocument>(ReadPortableFile(path));
		if (doc == null || doc.Version != 1 || doc.Kind != (session ? "sesja" : "rozklad") || doc.Trains == null || doc.Sections == null || string.IsNullOrEmpty(doc.StationId))
		{
			throw new InvalidDataException("Wybierz właściwy plik: " + (session ? "sesja EDR" : "rozkład EDR") + ".");
		}
		CheckTarget(doc.Provider, doc.Server, doc.Point, doc.StationId);
		if (doc.Trains.Any((Train x) => x == null || x.StationId != doc.StationId || string.IsNullOrEmpty(x.Id) || string.IsNullOrWhiteSpace(x.Number) || x.PlanArrival == DateTime.MinValue || x.PlanDeparture < x.PlanArrival) || doc.Trains.Select((Train x) => x.Id).Distinct().Count() != doc.Trains.Count)
		{
			throw new InvalidDataException("Nieprawidłowe lub powtórzone dane pociągu.");
		}
		Dictionary<string, string> sectionMap = new Dictionary<string, string>();
		foreach (SavedSection sec in doc.Sections)
		{
			if (sec == null || string.IsNullOrEmpty(sec.Id) || sectionMap.ContainsKey(sec.Id))
			{
				throw new InvalidDataException("Nieprawidłowe dzienniki w pliku.");
			}
			List<Section> list = Station.Sections.Where((Section x) => x.Id == sec.Id && x.Name == sec.Name && x.Line == sec.Line).ToList();
			if (list.Count == 0)
			{
				list = Station.Sections.Where((Section x) => x.Name == sec.Name && x.Line == sec.Line).ToList();
			}
			if (list.Count == 1)
			{
				sectionMap.Add(sec.Id, list[0].Id);
			}
		}
		if (session)
		{
			if (doc.Entries == null || doc.Duties == null || doc.Services == null || doc.History == null)
			{
				throw new InvalidDataException("Brak części danych sesji.");
			}
			HashSet<string> trainIds = new HashSet<string>(doc.Trains.Select((Train x) => x.Id));
			if (doc.Entries.Any((JournalEntry x) => x == null || x.StationId != doc.StationId || string.IsNullOrEmpty(x.Id) || (x.SectionId != "TEL" && !sectionMap.ContainsKey(x.SectionId ?? "")) || (!string.IsNullOrEmpty(x.TrainId) && !trainIds.Contains(x.TrainId))) || doc.Entries.Select((JournalEntry x) => x.Id).Distinct().Count() != doc.Entries.Count)
			{
				throw new InvalidDataException("Wpisy nie pasują do dzienników lub pociągów. Najpierw wczytaj konfigurację posterunku z tej sesji.");
			}
			if (doc.Duties.Any((Duty x) => x == null || x.StationId != doc.StationId || string.IsNullOrEmpty(x.Id)) || doc.Duties.Select((Duty x) => x.Id).Distinct().Count() != doc.Duties.Count || Enumerable.Count(doc.Duties, (Duty x) => !x.End.HasValue) > 1 || doc.Services.Any((ServiceRecord x) => x == null || x.StationId != doc.StationId || string.IsNullOrEmpty(x.Id)) || doc.Services.Select((ServiceRecord x) => x.Id).Distinct().Count() != doc.Services.Count || doc.History.Any((Audit x) => x == null))
			{
				throw new InvalidDataException("Nieprawidłowe dyżury, usługi lub historia w pliku.");
			}
			if (doc.Duties.Any((Duty x) => !x.End.HasValue && x.UserName != User.Name))
			{
				throw new InvalidDataException("W sesji jest otwarty dyżur innego operatora. Zaloguj się jako ten operator.");
			}
		}
		if (session && doc.Entries.Any((JournalEntry x) => x.SectionId != "TEL" && (x.LineTrack < 1 || x.LineTrack > Station.Sections.First((Section y) => y.Id == sectionMap[x.SectionId]).Tracks) && x.Type == "Pociąg"))
		{
			throw new InvalidDataException("Wpis wskazuje nieprawidłowy tor szlakowy.");
		}
		List<string> tracks = ((Station.TrackSettings == null) ? Station.Tracks : Station.TrackSettings.Select((StationTrackSetting x) => x.Number).ToList());
		if (doc.Trains.Any((Train x) => !string.IsNullOrEmpty(x.Track) && !tracks.Contains(x.Track)) || (session && doc.Entries.Any((JournalEntry x) => !string.IsNullOrEmpty(x.Track) && !tracks.Contains(x.Track))))
		{
			throw new InvalidDataException("W pliku występują inne tory stacyjne. Najpierw wczytaj konfigurację posterunku.");
		}
		string path2 = Path.Combine(Store.DirectoryPath, "przed-wczytaniem-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".json");
		ConfiguratorRepository.AtomicWrite(path2, Json.Bytes(State));
		Change("Wczytanie " + doc.Kind, StationId, (AppState s) =>
		{
			HashSet<string> otherTrainIds = new HashSet<string>(from x in s.Trains
				where x.StationId != StationId
				select x.Id);
			if (doc.Trains.Any((Train x) => otherTrainIds.Contains(x.Id)))
			{
				throw new InvalidDataException("Identyfikator pociągu jest już używany na innym posterunku.");
			}
			HashSet<string> keep = (session ? new HashSet<string>() : new HashSet<string>(from x in s.Entries
				where x.StationId == StationId && !string.IsNullOrEmpty(x.TrainId)
				select x.TrainId));
			HashSet<string> incoming = new HashSet<string>(doc.Trains.Select((Train x) => x.Id));
			s.Trains.RemoveAll((Train x) => x.StationId == StationId && (!keep.Contains(x.Id) || incoming.Contains(x.Id)));
			foreach (Train train in doc.Trains)
			{
				train.StationId = StationId;
				s.Trains.Add(train);
			}
			if (session)
			{
				if (doc.Entries.Any((JournalEntry x) => s.Entries.Any((JournalEntry y) => y.StationId != StationId && y.Id == x.Id)) || doc.Duties.Any((Duty x) => s.Duties.Any((Duty y) => y.StationId != StationId && y.Id == x.Id)) || doc.Services.Any((ServiceRecord x) => s.Services.Any((ServiceRecord y) => y.StationId != StationId && y.Id == x.Id)))
				{
					throw new InvalidDataException("Identyfikator wpisu jest już używany na innym posterunku.");
				}
				s.Entries.RemoveAll((JournalEntry x) => x.StationId == StationId);
				s.Duties.RemoveAll((Duty x) => x.StationId == StationId);
				s.Services.RemoveAll((ServiceRecord x) => x.StationId == StationId);
				foreach (JournalEntry entry in doc.Entries)
				{
					entry.StationId = StationId;
					if (entry.SectionId != "TEL")
					{
						entry.SectionId = sectionMap[entry.SectionId];
					}
					s.Entries.Add(entry);
				}
				foreach (Duty d in doc.Duties)
				{
					d.StationId = StationId;
					List<LocalUser> users = s.Users;
					Func<LocalUser, bool> predicate = (LocalUser x) => x.Name == d.UserName;
					LocalUser localUser = users.FirstOrDefault(predicate);
					if (localUser != null)
					{
						d.UserId = localUser.Id;
					}
					s.Duties.Add(d);
				}
				foreach (ServiceRecord service in doc.Services)
				{
					service.StationId = StationId;
					s.Services.Add(service);
				}
				foreach (Audit audit in doc.History)
				{
					if (!s.History.Any((Audit x) => x.Time == audit.Time && x.Action == audit.Action && x.Entity == audit.Entity && x.After == audit.After))
					{
						s.History.Add(audit);
					}
				}
			}
		});
	}

	public static string RestoreLegacyDatabase(string source, string destination)
	{
		AppState appState = Json.Read<AppState>(ReadPortableFile(source));
		Store.Validate(appState);
		if (appState.Stations.Any((Station x) => x == null || x.Sections == null || x.Tracks == null) || appState.Stations.Select((Station x) => x.Id).Distinct().Count() != appState.Stations.Count)
		{
			throw new InvalidDataException("Nieprawidłowe posterunki w bazie.");
		}
		if (File.Exists(Path.Combine(destination, "edr.json")))
		{
			throw new IOException("Folder odzyskiwania musi być nowy.");
		}
		ConfiguratorRepository.AtomicWrite(Path.Combine(destination, "edr.json"), Json.Bytes(appState));
		return destination;
	}

	private void NotifySimRailTime(string id, Train previous)
	{
		if (SimRail == null || previous == null)
		{
			return;
		}
		Train train = State.Trains.FirstOrDefault((Train x) => x.Id == id && x.SourceProvider == "SimRail");
		if (train != null)
		{
			bool flag = !SepeWriteRequest.SameMinute(train.Arrival, previous.Arrival);
			bool flag2 = !SepeWriteRequest.SameMinute(train.Departure, previous.Departure);
			if (flag || flag2)
			{
				SimRailTimeChanged?.Invoke(SepeWriteRequest.From(train, flag, flag2));
			}
		}
	}

	internal Engine CreateWindowView()
	{
		Engine engine = new Engine(Store);
		engine.User = User;
		engine.StationId = StationId;
		engine.ReadOnlySession = ReadOnlySession;
		engine.SimRail = ((SimRail == null) ? null : SimRail.CreateView());
		engine.LoggedInAt = LoggedInAt;
		engine.StationProfileDirectory = StationProfileDirectory;
		engine.UnitSettingsDirectory = UnitSettingsDirectory;
		engine.unitPreference = unitPreference ?? new UnitPreference
		{
			Version = 1,
			Caption = UnitCaption
		};
		engine.LastAppliedSnapshot = LastAppliedSnapshot;
		return engine;
	}

	internal bool CanAdjustSepeDepartureForArrival(Train train)
	{
		if (SimRail != null && SimRail.HasWriteLink && SepeWritesEnabled && train != null && train.SourceProvider == "SimRail" && train.StationId == StationId && !train.ExternalManualDeparture && !train.LocalDepartureRecorded)
		{
			return !State.Entries.Any((JournalEntry x) => x.StationId == StationId && x.TrainId == train.Id && x.Type == "Pociąg" && x.Direction == "DO" && !x.Cancelled && x.Left.HasValue);
		}
		return false;
	}

	private void ValidateJournalTrainTimes(JournalEntry entry, Train previous)
	{
		if (SimRail == null || previous == null || (previous.SourceProvider != "SimRail" && previous.SourceProvider != "TD2") || entry.Type != "Pociąg")
		{
			return;
		}
		JournalEntry old = State.Entries.FirstOrDefault((JournalEntry x) => x.Id == entry.Id);
		if (OwnJournalTimeChanged(entry, old, previous))
		{
			if (entry.Direction == "Z" && entry.Arrived.HasValue && !SepeWriteRequest.SameMinute(entry.Arrived, previous.Arrival))
			{
				TrainTimeRules.Validate(entry.Arrived, CanAdjustSepeDepartureForArrival(previous) ? ((DateTime?)null) : previous.Departure, Now, serverClock: true);
			}
			if (entry.Direction == "DO" && entry.Left.HasValue && !SepeWriteRequest.SameMinute(entry.Left, previous.Departure))
			{
				TrainTimeRules.Validate(previous.Arrival, entry.Left, Now, serverClock: true);
			}
		}
	}

	public bool CanEditJournal(string section)
	{
		if (CanWrite)
		{
			if (!(section == "TEL"))
			{
				return Station.Sections.Any((Section x) => x.Id == section && StationSettings.Conducted(x));
			}
			return true;
		}
		return false;
	}

	public void RequireJournalWrite(string section)
	{
		RequireWrite();
		if (!CanEditJournal(section))
		{
			throw new InvalidOperationException("Ten dziennik nie jest prowadzony. Jest dostępny tylko do odczytu.");
		}
	}

	public bool LineMovementVisible(JournalEntry entry)
	{
		if (entry.Cancelled || !LineMovementActive(entry, State))
		{
			return false;
		}
		if (entry.Direction != "DO" || entry.ReturnFromLine || !AutomaticBasis(CurrentBasis(entry.SectionId, entry.LineTrack, "DO")))
		{
			return true;
		}
		return Now < (entry.LineIndicationStartedAt ?? entry.Left.Value).AddSeconds(AutomaticReleaseSeconds);
	}

	private void RecordLineIndication(JournalEntry entry, JournalEntry previous)
	{
		entry.LineIndicationStartedAt = previous?.LineIndicationStartedAt;
		if (!(entry.Type == "Pociąg") || !(entry.Direction == "DO") || !entry.Left.HasValue || entry.Arrived.HasValue)
		{
			return;
		}
		if (previous != null)
		{
			DateTime? left = previous.Left;
			DateTime? left2 = entry.Left;
			if (left.HasValue == left2.HasValue && (!left.HasValue || !(left.GetValueOrDefault() != left2.GetValueOrDefault())) && !(previous.Direction != entry.Direction) && !(previous.SectionId != entry.SectionId) && previous.LineTrack == entry.LineTrack)
			{
				return;
			}
		}
		entry.LineIndicationStartedAt = Now;
	}

	public static bool AutomaticBasis(string basis)
	{
		return TrafficBasis.IsAutomatic(basis);
	}

	public bool RepairSuggestedNeighbours()
	{
		if (SimRail == null || IsTd2 || Station.SimulatorTopologyVersion >= 2)
		{
			return false;
		}
		Station station = SimRail.SuggestStation();
		Dictionary<string, Section> changes = new Dictionary<string, Section>();
		foreach (IGrouping<string, Section> group in from x in station.Sections
			group x by x.Line)
		{
			List<Section> expected = group.ToList();
			List<Section> sections = Station.Sections;
			Func<Section, bool> predicate = (Section x) => x.Line == group.Key;
			List<Section> old = sections.Where(predicate).ToList();
			List<Section> list = expected.Where((Section x) => !old.Any((Section s) => s.Name == x.Name)).ToList();
			List<Section> list2 = old.Where((Section x) => (x.Id ?? "").StartsWith("sr-section-") && !expected.Any((Section s) => s.Name == x.Name) && (string.IsNullOrEmpty(x.JournalName) || x.JournalName == x.Name)).ToList();
			if (list.Count == 1 && list2.Count == 1)
			{
				changes.Add(list2[0].Id, list[0]);
			}
		}
		if (changes.Count == 0)
		{
			return false;
		}
		Change("Naprawa sugerowanego sąsiedztwa SimRail", StationId, (AppState state) =>
		{
			Station station2 = state.Stations.First((Station x) => x.Id == StationId);
			foreach (Section section in station2.Sections)
			{
				if (changes.TryGetValue(section.Id, out var value))
				{
					string name = section.Name;
					section.Name = value.Name;
					if (string.IsNullOrEmpty(section.JournalName) || section.JournalName == name)
					{
						section.JournalName = value.Name;
					}
					foreach (Train item in state.Trains.Where((Train x) => x.StationId == StationId))
					{
						if (item.From == name && item.LineIn == section.Line)
						{
							item.From = section.Name;
						}
						if (item.To == name && item.LineOut == section.Line)
						{
							item.To = section.Name;
						}
					}
				}
			}
			station2.SimulatorReviewed = false;
			station2.SimulatorTopologyVersion = 2;
		});
		return true;
	}

	private string ProfileFile(string point)
	{
		if (string.IsNullOrEmpty(ProfileRoot))
		{
			return null;
		}
		if (string.IsNullOrEmpty(point) || point.Length > 20 || !point.All(char.IsDigit))
		{
			throw new InvalidDataException("Nieprawidłowy identyfikator profilu posterunku.");
		}
		return Path.Combine(ProfileRoot, point + ".json");
	}

	private Station ReadStationProfile(string point)
	{
		string text = ProfileFile(point);
		if (text == null || !File.Exists(text))
		{
			return null;
		}
		try
		{
			if (new FileInfo(text).Length > 4194304)
			{
				return null;
			}
			StationProfile stationProfile = Json.Read<StationProfile>(File.ReadAllBytes(text));
			if (stationProfile == null || stationProfile.SchemaVersion != 1 || stationProfile.PointId != point || stationProfile.Station == null || stationProfile.Station.SimRailPointId != point || !stationProfile.Station.SimulatorReviewed)
			{
				return null;
			}
			return stationProfile.Station;
		}
		catch
		{
			return null;
		}
	}

	private void WriteStationProfile(Station station)
	{
		string text = ProfileFile(station.SimRailPointId);
		if (text == null)
		{
			return;
		}
		Directory.CreateDirectory(ProfileRoot);
		string text2 = text + "." + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			StationProfile stationProfile = new StationProfile();
			stationProfile.SchemaVersion = 1;
			stationProfile.PointId = station.SimRailPointId;
			stationProfile.SavedUtc = DateTime.UtcNow;
			stationProfile.Station = Json.Clone(station);
			StationProfile value = stationProfile;
			using (FileStream fileStream = new FileStream(text2, FileMode.CreateNew, FileAccess.Write, FileShare.None))
			{
				byte[] array = Json.Bytes(value);
				fileStream.Write(array, 0, array.Length);
				fileStream.Flush(flushToDisk: true);
			}
			if (File.Exists(text))
			{
				File.Replace(text2, text, text + ".bak", ignoreMetadataErrors: true);
			}
			else
			{
				File.Move(text2, text);
			}
		}
		finally
		{
			if (File.Exists(text2))
			{
				File.Delete(text2);
			}
		}
	}

	public void PersistStationProfile()
	{
		if (SimRail != null && Station.SimulatorReviewed)
		{
			Station station = ReadStationProfile(Station.SimRailPointId);
			if (station == null || string.IsNullOrEmpty(station.ConfiguratorRevision) || !(station.ConfiguratorRevision != Station.ConfiguratorRevision))
			{
				WriteStationProfile(Station);
			}
		}
	}

	private Station FindLegacyStationProfile(string point)
	{
		DirectoryInfo parent = Directory.GetParent(Store.DirectoryPath);
		if (parent == null || parent.Parent == null || parent.Parent.Name != "simrail")
		{
			return null;
		}
		List<string> list = (from x in SimRail.ControlledLocations()
			select x.Id).Concat(new string[2] { SimRail.StoragePointId, point }).Distinct().ToList();
		List<FileInfo> list2 = new List<FileInfo>();
		string[] directories = Directory.GetDirectories(parent.Parent.FullName);
		foreach (string path in directories)
		{
			foreach (string item in list)
			{
				string text = Path.Combine(path, item, "edr.json");
				if (File.Exists(text))
				{
					list2.Add(new FileInfo(text));
				}
			}
		}
		foreach (FileInfo item2 in list2.OrderByDescending((FileInfo x) => x.LastWriteTimeUtc))
		{
			try
			{
				if (item2.Length <= 67108864)
				{
					AppState appState = Json.Read<AppState>(File.ReadAllBytes(item2.FullName));
					Store.Validate(appState);
					Station station = appState.Stations.FirstOrDefault((Station x) => x.SimRailPointId == point && x.SimulatorReviewed);
					if (station != null)
					{
						return station;
					}
				}
			}
			catch
			{
			}
		}
		return null;
	}

	public void InitializeStationProfiles()
	{
		if (SimRail == null || string.IsNullOrEmpty(ProfileRoot))
		{
			return;
		}
		ConfigurationProfileWarning = null;
		foreach (string id in (from x in State.Stations
			where !string.IsNullOrEmpty(x.SimRailPointId)
			select x.Id).ToList())
		{
			List<Station> stations = State.Stations;
			Func<Station, bool> predicate = (Station x) => x.Id == id;
			Station station = stations.First(predicate);
			string path = ProfileFile(station.SimRailPointId);
			Station station2 = ReadStationProfile(station.SimRailPointId);
			if (ApplyConfiguratorProfile(station, station2) || (station2 != null && !string.IsNullOrEmpty(station2.ConfiguratorRevision) && station2.ConfiguratorRevision != station.ConfiguratorRevision))
			{
				continue;
			}
			if (station.SimulatorReviewed)
			{
				if (!File.Exists(path))
				{
					WriteStationProfile(station);
				}
			}
			else
			{
				if (State.Entries.Any((JournalEntry x) => x.StationId == id))
				{
					continue;
				}
				Station station3 = ReadStationProfile(station.SimRailPointId) ?? (LocalPersistence.Enabled ? FindLegacyStationProfile(station.SimRailPointId) : null);
				if (station3 == null)
				{
					continue;
				}
				Station restored;
				try
				{
					restored = Json.Clone(station3);
					restored.Id = id;
					StationSettings.Normalize(restored);
					foreach (string item in (from x in State.Trains
						where x.StationId == id && !string.IsNullOrEmpty(x.Track)
						select x.Track).Distinct())
					{
						if (!restored.Tracks.Contains(item))
						{
							restored.Tracks.Add(item);
							restored.TrackSettings.Add(new StationTrackSetting
							{
								Number = item,
								Visible = false
							});
						}
					}
					StationSettings.Validate(restored, State);
				}
				catch
				{
					continue;
				}
				Change("Przywrócenie lokalnego profilu posterunku", id, (AppState state) =>
				{
					state.Stations[state.Stations.FindIndex((Station x) => x.Id == id)] = restored;
				});
				if (!File.Exists(path))
				{
					WriteStationProfile(restored);
				}
			}
		}
	}

	public bool RepairRouteNeighbours()
	{
		if (SimRail == null || IsTd2 || Station.RouteNeighboursVersion >= 2)
		{
			return false;
		}
		int previousVersion = Station.RouteNeighboursVersion;
		Station station = SimRail.SuggestStation();
		Station legacy = SimRail.SuggestStationVersion((previousVersion != 0) ? 1 : 0);
		Dictionary<string, Section> changes = new Dictionary<string, Section>();
		HashSet<string> remove = new HashSet<string>();
		foreach (string line in legacy.Sections.Select((Section x) => x.Line).Concat(station.Sections.Select((Section x) => x.Line)).Distinct())
		{
			List<Section> sections = station.Sections;
			Func<Section, bool> predicate = (Section x) => x.Line == line;
			List<Section> expected = sections.Where(predicate).ToList();
			List<Section> old = Station.Sections.Where((Section x) => x.Line == line).ToList();
			List<Section> list = expected.Where((Section x) => !old.Any((Section y) => y.Name == x.Name)).ToList();
			List<Section> list2 = old.Where((Section x) => (x.Id ?? "").StartsWith("sr-section-") && (string.IsNullOrEmpty(x.JournalName) || x.JournalName == x.Name) && !expected.Any((Section y) => y.Name == x.Name) && legacy.Sections.Any((Section y) => y.Name == x.Name && y.Line == x.Line) && (!x.ManualConfiguration || (previousVersion == 1 && string.IsNullOrEmpty(Station.ConfiguratorRevision) && !State.Entries.Any((JournalEntry e) => e.StationId == StationId && e.SectionId == x.Id)))).ToList();
			if (list.Count == 1 && list2.Count == 1)
			{
				changes[list2[0].Id] = list[0];
			}
			else
			{
				if (list.Count != 0)
				{
					continue;
				}
				foreach (Section section in list2)
				{
					if (!State.Entries.Any((JournalEntry e) => e.StationId == StationId && e.SectionId == section.Id))
					{
						remove.Add(section.Id);
					}
				}
			}
		}
		if (changes.Count == 0 && remove.Count == 0)
		{
			Store.Commit((AppState s) =>
			{
				s.Stations.First((Station x) => x.Id == StationId).RouteNeighboursVersion = 2;
			});
			return false;
		}
		Change("Naprawa sąsiedztwa według tras pociągów", StationId, (AppState state) =>
		{
			Station station2 = state.Stations.First((Station x) => x.Id == StationId);
			foreach (Section section2 in station2.Sections)
			{
				if (changes.TryGetValue(section2.Id, out var value))
				{
					string name = section2.Name;
					section2.Name = value.Name;
					if (string.IsNullOrEmpty(section2.JournalName) || section2.JournalName == name)
					{
						section2.JournalName = value.Name;
					}
					foreach (Train item in state.Trains.Where((Train x) => x.StationId == StationId))
					{
						if (item.From == name && item.LineIn == section2.Line)
						{
							item.From = section2.Name;
						}
						if (item.To == name && item.LineOut == section2.Line)
						{
							item.To = section2.Name;
						}
					}
				}
			}
			station2.Sections.RemoveAll((Section x) => remove.Contains(x.Id));
			foreach (JournalTabDefinition item2 in station2.JournalTabs ?? new List<JournalTabDefinition>())
			{
				if (remove.Contains(item2.LeftId))
				{
					item2.LeftId = item2.RightId;
					item2.RightId = null;
				}
				if (remove.Contains(item2.RightId))
				{
					item2.RightId = null;
				}
			}
			if (station2.JournalTabs != null)
			{
				station2.JournalTabs.RemoveAll((JournalTabDefinition x) => string.IsNullOrEmpty(x.LeftId));
			}
			station2.RouteNeighboursVersion = 2;
			station2.SimulatorReviewed = false;
		});
		return true;
	}

	private string NormalBasis(string section, int track, string direction)
	{
		Section section2 = Station.Sections.First((Section x) => x.Id == section);
		LineTrackSetting lineTrackSetting = (section2.LineTracks ?? new List<LineTrackSetting>()).FirstOrDefault((LineTrackSetting x) => x.Number == track);
		string text;
		if (lineTrackSetting != null)
		{
			if (!(direction == "DO"))
			{
				return lineTrackSetting.Basis;
			}
			text = lineTrackSetting.BasisOut;
			if (text == null)
			{
				return lineTrackSetting.Basis;
			}
		}
		else
		{
			text = section2.Basis;
		}
		return text;
	}

	public void ApplySimRailSnapshot(SimRailSnapshot snapshot)
	{
		if (SimRail == null)
		{
			throw new InvalidOperationException("Brak sesji SimRail.");
		}
		if (SimRail.IsTd2)
		{
			ApplyTd2Snapshot(snapshot);
			return;
		}
		SimRailSnapshot snapshot2 = SimRail.Snapshot;
		SimRail.Accept(snapshot);
		try
		{
			List<Train> incoming = SimRail.Project(Station);
			if (incoming.Count == 0)
			{
				throw new Exception("Rozkład nie zawiera wybranego posterunku.");
			}
			Store.Commit((AppState s) =>
			{
				Dictionary<string, Train> dictionary = s.Trains.Where((Train x) => x.StationId == StationId && x.SourceProvider == "SimRail").ToDictionary((Train x) => x.Id);
				foreach (Train item in incoming)
				{
					if (dictionary.TryGetValue(item.Id, out var value))
					{
						item.LocalDepartureRecorded = value.LocalDepartureRecorded;
						if (value.ExternalManualArrival && (!item.ExternalConfirmed || !SepeWriteRequest.SameMinute(item.Arrival, value.Arrival)))
						{
							item.Arrival = value.Arrival;
							item.ExternalManualArrival = true;
						}
						if (value.ExternalManualDeparture && (!item.ExternalConfirmed || !SepeWriteRequest.SameMinute(item.Departure, value.Departure)))
						{
							item.Departure = value.Departure;
							item.ExternalManualDeparture = true;
						}
						item.ExternalManualTimes = item.ExternalManualArrival || item.ExternalManualDeparture;
						if (value.Profile != null)
						{
							string name = item.Profile.Name;
							string operationalNotes = item.Profile.OperationalNotes;
							item.Profile = Json.Clone(value.Profile);
							item.Profile.Name = name;
							item.Profile.OperationalNotes = operationalNotes;
						}
						item.DetailRows = value.DetailRows;
						item.Consist = ConsistPlan.Merge(item.Consist, ConsistPlan.SameCourse(item, value) ? value.Consist : null);
						item.Analysis = value.Analysis;
						item.Cargo = value.Cargo;
						item.Ready = value.Ready;
						item.ReadyReported = value.ReadyReported;
						item.ReadyRecordedAt = value.ReadyRecordedAt;
						item.ReadyDriver = value.ReadyDriver;
						item.DelayReason = value.DelayReason;
						if (!string.IsNullOrEmpty(value.Notes))
						{
							item.Notes = value.Notes;
						}
					}
					if (!string.IsNullOrEmpty(item.Track) && !s.Stations.First((Station x) => x.Id == StationId).Tracks.Contains(item.Track))
					{
						Station station = s.Stations.First((Station x) => x.Id == StationId);
						station.Tracks.Add(item.Track);
						if (station.TrackSettings != null)
						{
							station.TrackSettings.Add(new StationTrackSetting
							{
								Number = item.Track,
								Visible = true
							});
						}
					}
				}
				HashSet<string> newIds = new HashSet<string>(incoming.Select((Train x) => x.Id));
				s.Trains.RemoveAll((Train x) => x.StationId == StationId && x.SourceProvider == "SimRail" && newIds.Contains(x.Id));
				s.Trains.AddRange(incoming);
			});
			LastAppliedSnapshot = snapshot;
		}
		catch
		{
			SimRail.Accept(snapshot2);
			throw;
		}
	}

	public List<StationTrackItem> StationTrackItems(string track)
	{
		return (from e in StationOccupancy.AtStation(State, StationId)
			where e.Track == track
			select e).ToList();
	}

	public string TrackStatus(string track)
	{
		List<StationTrackItem> list = StationTrackItems(track);
		if (list.Count != 0)
		{
			return (list.Any((StationTrackItem e) => !e.Reserved) ? "ZAJĘTY: " : "REZERWACJA: ") + string.Join(", ", list.Select((StationTrackItem e) => e.Number));
		}
		return "WOLNY";
	}

	private void RequireStationTrack(string track)
	{
		RequireWrite();
		if (string.IsNullOrWhiteSpace(track) || !Station.Tracks.Contains(track))
		{
			throw new InvalidOperationException("Wybierz istniejący tor stacyjny.");
		}
	}

	public string AddStationTrackOccupation(string track, string number)
	{
		RequireStationTrack(track);
		number = (number ?? "").Trim();
		if (number.Length == 0)
		{
			throw new InvalidOperationException("Podaj numer pociągu lub opis zajętości.");
		}
		if (number.Length > 120)
		{
			throw new InvalidOperationException("Opis zajętości może mieć do 120 znaków.");
		}
		if (StationTrackItems(track).Any((StationTrackItem e) => string.Equals(e.Number, number, StringComparison.CurrentCultureIgnoreCase)))
		{
			throw new InvalidOperationException("Ten pociąg lub opis jest już na wskazanym torze.");
		}
		List<Train> list = State.Trains.Where((Train t) => t.StationId == StationId && t.Number == number).ToList();
		string text = ((list.Count == 1) ? list[0].Id : null);
		StationTrackOccupation occupation = new StationTrackOccupation
		{
			Id = Guid.NewGuid().ToString("N"),
			StationId = StationId,
			Track = track,
			Number = number,
			TrainId = text,
			AddedAt = Now,
			KnownDepartures = StationOccupancy.Departures(State, StationId, text, number).Select(StationOccupancy.DepartureKey).ToList()
		};
		Change("Dodanie zajętości toru stacyjnego", occupation.Id, (AppState s) =>
		{
			if (s.StationTrackOccupations == null)
			{
				s.StationTrackOccupations = new List<StationTrackOccupation>();
			}
			s.StationTrackOccupations.Add(occupation);
		});
		return "manual:" + occupation.Id;
	}

	public void MoveStationTrackOccupation(string key, string target)
	{
		RequireStationTrack(target);
		StationTrackItem stationTrackItem = StationOccupancy.AtStation(State, StationId).SingleOrDefault((StationTrackItem e) => e.Key == key);
		if (stationTrackItem == null)
		{
			throw new InvalidOperationException("Zajętość nie jest już aktualna. Odśwież kontrolkę torów.");
		}
		if (!(stationTrackItem.Track == target))
		{
			EditStationOccupation(stationTrackItem, target, remove: false);
		}
	}

	public void RemoveStationTrackOccupation(string key)
	{
		RequireWrite();
		StationTrackItem stationTrackItem = StationOccupancy.AtStation(State, StationId).SingleOrDefault((StationTrackItem e) => e.Key == key);
		if (stationTrackItem == null)
		{
			throw new InvalidOperationException("Zajętość nie jest już aktualna. Odśwież kontrolkę torów.");
		}
		EditStationOccupation(stationTrackItem, stationTrackItem.Track, remove: true);
	}

	private void EditStationOccupation(StationTrackItem item, string target, bool remove)
	{
		Change(remove ? "Usunięcie zajętości toru stacyjnego" : "Przeniesienie zajętości toru stacyjnego", item.Key, (AppState s) =>
		{
			if (s.StationTrackOccupations == null)
			{
				s.StationTrackOccupations = new List<StationTrackOccupation>();
			}
			List<StationTrackOccupation> stationTrackOccupations = s.StationTrackOccupations;
			if (item.Key.StartsWith("manual:"))
			{
				StationTrackOccupation stationTrackOccupation = stationTrackOccupations.Single((StationTrackOccupation e) => e.StationId == StationId && "manual:" + e.Id == item.Key);
				if (remove)
				{
					stationTrackOccupations.Remove(stationTrackOccupation);
				}
				else
				{
					stationTrackOccupation.Track = target;
				}
			}
			else
			{
				string id = item.Key.Substring(6);
				JournalEntry journalEntry = s.Entries.Single((JournalEntry e) => e.Id == id && e.StationId == StationId);
				stationTrackOccupations.RemoveAll((StationTrackOccupation e) => e.StationId == StationId && e.EntryId == id);
				stationTrackOccupations.Add(new StationTrackOccupation
				{
					Id = Guid.NewGuid().ToString("N"),
					StationId = StationId,
					EntryId = id,
					SourceTrack = journalEntry.Track,
					Track = target,
					Removed = remove,
					AddedAt = Now
				});
			}
		});
	}

	public void SetLocalForecast(string id, bool stop, DateTime arrival, DateTime departure)
	{
		SetLocalForecast(id, stop ? StopForecastOperation.Stop : StopForecastOperation.Forecast, arrival, departure);
	}

	public void SetLocalForecast(string id, StopForecastOperation operation, DateTime arrival, DateTime departure)
	{
		RequireWrite();
		if (SimRail != null)
		{
			throw new InvalidOperationException("Zmianę SimRail musi potwierdzić SEPE.");
		}
		bool forecast = operation == StopForecastOperation.Forecast;
		if (forecast && arrival > departure)
		{
			throw new TrainTimeException("Przyjazd nie może być późniejszy od odjazdu.");
		}
		string action;
		if (operation == StopForecastOperation.CancelStop)
		{
			action = "Odwołanie postoju pociągu";
		}
		else
		{
			action = (forecast ? "Prognoza pociągu" : "Postój pociągu");
		}
		Change(action, id, (AppState s) =>
		{
			Train train = s.Trains.Single((Train x) => x.Id == id);
			if (forecast)
			{
				train.ArrivalForecast = arrival;
				train.Forecast = departure;
			}
			else
			{
				train.Stopped = operation == StopForecastOperation.Stop;
			}
			List<JournalEntry> entries = s.Entries;
			Engine engine = this;
			DateTime now = Now;
			string number = train.Number;
			string text = ((operation == StopForecastOperation.CancelStop) ? ": odwołano pociąg STÓJ." : (forecast ? (": prognoza przyjazdu " + Ui.FullTime(arrival) + ", odjazdu " + Ui.FullTime(departure)) : ": pociąg STÓJ."));
			entries.Add(engine.Note("TEL", now, "Pociąg " + number + text, "Prognoza"));
		});
	}

	private void ApplyTd2Snapshot(SimRailSnapshot snapshot)
	{
		if (snapshot == null || snapshot.Td2 == null)
		{
			throw new InvalidDataException("Niekompletny rozkład TD2.");
		}
		SimRailSnapshot snapshot2 = SimRail.Snapshot;
		string text = Json.Text(Station);
		SimRail.Accept(snapshot);
		try
		{
			Station proposed = Td2Projection.SuggestStation(SimRail, Station);
			Store.Commit((AppState state) =>
			{
				Station station = state.Stations.First((Station x) => x.Id == StationId);
				Td2Topology.Reconcile(station, proposed, state.Entries);
				List<Train> list = SimRail.Project(station);
				Dictionary<string, Train> dictionary = state.Trains.Where((Train x) => x.StationId == StationId && x.SourceProvider == "TD2").ToDictionary((Train x) => x.Id);
				foreach (Train item in list)
				{
					if (dictionary.TryGetValue(item.Id, out var value))
					{
						item.LocalArrivalRecorded = value.LocalArrivalRecorded;
						item.LocalDepartureRecorded = value.LocalDepartureRecorded;
						if (value.LocalArrivalRecorded)
						{
							item.Arrival = value.Arrival;
						}
						if (value.LocalDepartureRecorded)
						{
							item.Departure = value.Departure;
						}
						item.Ready = value.Ready;
						item.ReadyReported = value.ReadyReported;
						item.ReadyRecordedAt = value.ReadyRecordedAt;
						item.Consist = ConsistPlan.Merge(item.Consist, value.Consist);
						item.Analysis = value.Analysis;
						item.Cargo = value.Cargo;
						item.DelayReason = value.DelayReason;
						item.DetailRows = value.DetailRows;
						if (value.Profile != null)
						{
							string name = item.Profile.Name;
							string operationalNotes = item.Profile.OperationalNotes;
							item.Profile = Json.Clone(value.Profile);
							if (!string.IsNullOrEmpty(name))
							{
								item.Profile.Name = name;
							}
							item.Profile.OperationalNotes = operationalNotes;
						}
						if (!string.IsNullOrEmpty(value.Notes))
						{
							item.Notes = value.Notes;
						}
						if (value.RemovedFromSource)
						{
							item.RemovedFromSource = true;
							item.Runs = snapshot.Td2.Find(item.ExternalTrainNo)?.Completed ?? false;
						}
					}
					if (!string.IsNullOrEmpty(item.Track))
					{
						Station station2 = state.Stations.First((Station x) => x.Id == StationId);
						if (!station2.Tracks.Contains(item.Track))
						{
							station2.Tracks.Add(item.Track);
							if (station2.TrackSettings != null)
							{
								station2.TrackSettings.Add(new StationTrackSetting
								{
									Number = item.Track,
									Visible = true
								});
							}
						}
					}
				}
				HashSet<string> ids = new HashSet<string>(list.Select((Train x) => x.Id));
				foreach (Train item2 in dictionary.Values.Where((Train x) => !ids.Contains(x.Id)))
				{
					Td2Record td2Record = snapshot.Td2.Find(item2.ExternalTrainNo);
					if (td2Record != null && !td2Record.Removed)
					{
						item2.Td2OutsideArea = true;
					}
					else
					{
						item2.Runs = td2Record?.Completed ?? false;
						item2.RemovedFromSource = true;
					}
				}
				state.Trains.RemoveAll((Train x) => x.StationId == StationId && x.SourceProvider == "TD2" && ids.Contains(x.Id));
				state.Trains.AddRange(list);
			});
			LastAppliedSnapshot = snapshot;
			if (text != Json.Text(Station))
			{
				PersistStationProfile();
			}
		}
		catch
		{
			SimRail.Accept(snapshot2);
			throw;
		}
	}

	public JournalEntry SaveTelephone(string section, JournalEntry original, TelephoneData data)
	{
		RequireWrite();
		JournalEntry entry = ((original == null) ? Note(section, data.SentAt, "", "Telefonogram") : Json.Clone(original));
		if (entry.SectionId != section)
		{
			throw new Exception("Nie można przenosić telefonogramu do innego dziennika.");
		}
		entry.Telephone = Json.Clone(data);
		SaveJournal(entry);
		return State.Entries.First((JournalEntry x) => x.Id == entry.Id);
	}

	private void PrepareTelephone(JournalEntry entry)
	{
		TelephoneData telephone = entry.Telephone;
		if (telephone == null)
		{
			if (State.Entries.Any((JournalEntry x) => x.Id == entry.Id && x.Telephone != null))
			{
				throw new Exception("Parametry zapisanego telefonogramu muszą zostać zachowane.");
			}
			return;
		}
		JournalEntry journalEntry = State.Entries.FirstOrDefault((JournalEntry x) => x.Id == entry.Id);
		if (journalEntry != null && (journalEntry.StationId != StationId || journalEntry.SectionId != entry.SectionId))
		{
			throw new Exception("Nie można zmieniać posterunku ani dziennika zapisanego telefonogramu.");
		}
		if (journalEntry != null && journalEntry.Telephone == null && entry.SectionId != "TEL")
		{
			throw new Exception("Starszy telefonogram jest dostępny do podglądu. Poprawkę wprowadź jako nowy wpis.");
		}
		if (journalEntry != null && journalEntry.Telephone != null && journalEntry.Telephone.TemplateId != telephone.TemplateId)
		{
			throw new Exception("Nie można zmieniać wzoru zapisanego telefonogramu.");
		}
		bool flag = entry.SectionId == "TEL";
		if (flag ? (telephone.TemplateId != "OWN") : (!TelephoneTemplates.Ids.Contains(telephone.TemplateId)))
		{
			throw new Exception("Ten wzór nie jest przeznaczony do wybranego dziennika.");
		}
		Section section = (flag ? null : Station.Sections.FirstOrDefault((Section x) => x.Id == entry.SectionId));
		if (!flag && section == null)
		{
			throw new Exception("Nieprawidłowy szlak.");
		}
		if (!flag && (telephone.Track < 1 || telephone.Track > section.Tracks))
		{
			throw new Exception("Nieprawidłowy numer toru szlakowego.");
		}
		if (telephone.SentAt == DateTime.MinValue || (!flag && telephone.EffectiveAt == DateTime.MinValue))
		{
			throw new Exception("Podaj datę i godzinę telefonogramu.");
		}
		if ((telephone.TemplateId == "16" || telephone.TemplateId == "16a" || telephone.TemplateId == "21" || telephone.TemplateId == "21a") && string.IsNullOrWhiteSpace(telephone.Reason))
		{
			throw new Exception("Podaj przyczynę / cel.");
		}
		if (new string[4] { "23", "24", "25", "26" }.Contains(telephone.TemplateId) && section.Tracks != 2)
		{
			throw new Exception("Ten wzór wymaga szlaku dwutorowego.");
		}
		if ((telephone.TemplateId == "23" || telephone.TemplateId == "26") && (string.IsNullOrWhiteSpace(telephone.TrainNumber) || !telephone.LastTrainDeparture.HasValue))
		{
			throw new Exception("Podaj numer ostatniego pociągu i godzinę jego odjazdu.");
		}
		if (telephone.TemplateId == "11" && string.IsNullOrWhiteSpace(telephone.TrainNumber))
		{
			throw new Exception("Podaj numer pociągu, którego nie należy wyprawiać.");
		}
		if ((telephone.TemplateId == "11" || telephone.TemplateId == "12") && string.IsNullOrWhiteSpace(telephone.PassingTrainNumber))
		{
			throw new Exception("Podaj numer pociągu, do którego przejazdu obowiązuje wstrzymanie.");
		}
		if (telephone.TemplateId == "21a" && string.IsNullOrWhiteSpace(telephone.TrainNumber))
		{
			throw new Exception("Podaj numer pociągu, po którego przejeździe tor ma zostać zamknięty.");
		}
		if (telephone.TemplateId == "022" && (string.IsNullOrWhiteSpace(telephone.WorkSupervisorName) || string.IsNullOrWhiteSpace(telephone.WorkSupervisorRole) || string.IsNullOrWhiteSpace(telephone.SpeedRestrictions)))
		{
			throw new Exception("Podaj nazwisko i stanowisko zawiadamiającego oraz ograniczenia prędkości (lub wpisz Brak).");
		}
		if (telephone.TemplateId == "30" && string.IsNullOrWhiteSpace(telephone.TrainNumber))
		{
			throw new Exception("Podaj numer pociągu.");
		}
		if ((telephone.TemplateId == "21" || telephone.TemplateId == "21a") && !new string[3] { "", "planowo", "awaryjnie" }.Contains(telephone.ClosureMode ?? ""))
		{
			throw new Exception("Nieprawidłowy rodzaj zamknięcia.");
		}
		if (flag && string.IsNullOrWhiteSpace(telephone.FreeText))
		{
			throw new Exception("Wpisz treść rozmowy lub telefonogramu.");
		}
		TelephoneData telephoneData = journalEntry?.Telephone;
		telephone.LocalStation = ((telephoneData == null) ? Station.Name.ToUpper() : telephoneData.LocalStation);
		TelephoneData telephoneData2 = telephone;
		string neighbourStation;
		if (telephoneData != null)
		{
			neighbourStation = telephoneData.NeighbourStation;
		}
		else
		{
			neighbourStation = (flag ? "" : section.Name.ToUpper());
		}
		telephoneData2.NeighbourStation = neighbourStation;
		telephone.LocalParty = ((telephoneData == null) ? User.Name : telephoneData.LocalParty);
		if (telephone.Incoming)
		{
			telephone.Receiver = telephone.LocalParty;
		}
		else
		{
			telephone.Sender = telephone.LocalParty;
		}
		if ((!flag || telephone.Incoming) && string.IsNullOrWhiteSpace(telephone.Incoming ? telephone.Sender : telephone.Receiver))
		{
			throw new Exception("Wpisz dane rozmówcy.");
		}
		entry.StationId = StationId;
		entry.Type = "Telefonogram";
		entry.Time = telephone.SentAt;
		entry.Direction = (telephone.Incoming ? "Odbieram" : "Nadaję");
		entry.Recipient = (telephone.Incoming ? telephone.Sender : telephone.Receiver);
		entry.LineTrack = (flag ? 1 : telephone.Track);
		entry.Effect = (flag ? "" : TelephoneTemplates.Effect(telephone.TemplateId));
		entry.Text = (string.IsNullOrWhiteSpace(telephone.Notice) ? "" : ("Zawiadomienie / Powiadomienie:\n" + telephone.Notice.Trim() + "\n")) + TelephoneTemplates.Body(telephone) + (string.IsNullOrWhiteSpace(telephone.AdditionalNote) ? "" : ("\n" + telephone.AdditionalNote.Trim()));
	}

	public void SetTrainTimes(string id, DateTime? arrival, DateTime? departure, bool inheritedDeparture = false)
	{
		RequireWrite();
		Train previous = State.Trains.First((Train x) => x.Id == id);
		bool arrivalOnly = !SepeWriteRequest.SameMinute(previous.Arrival, arrival) && (inheritedDeparture || SepeWriteRequest.SameMinute(previous.Departure, departure)) && CanAdjustSepeDepartureForArrival(previous);
		if (arrivalOnly)
		{
			departure = previous.Departure;
		}
		TrainTimeRules.Validate(arrival, arrivalOnly ? ((DateTime?)null) : departure, Now, SimRail != null);
		Change("Wprowadzanie godzin", id, (AppState s) =>
		{
			Train train = s.Trains.First((Train x) => x.Id == id);
			train.Arrival = arrival;
			train.Departure = departure;
			if (train.SourceProvider == "TD2")
			{
				if (!SepeWriteRequest.SameMinute(previous.Arrival, arrival))
				{
					train.LocalArrivalRecorded = true;
				}
				if (!SepeWriteRequest.SameMinute(previous.Departure, departure))
				{
					train.LocalDepartureRecorded = true;
				}
			}
			if (train.SourceProvider == "SimRail")
			{
				if (!SepeWriteRequest.SameMinute(previous.Arrival, arrival))
				{
					train.ExternalManualArrival = true;
				}
				if (!SepeWriteRequest.SameMinute(previous.Departure, departure))
				{
					train.ExternalManualDeparture = true;
					train.LocalDepartureRecorded = departure.HasValue;
				}
				train.ExternalManualTimes = train.ExternalManualArrival || train.ExternalManualDeparture;
			}
			if (departure.HasValue && train.SourceProvider != "SimRail")
			{
				train.Stopped = false;
			}
			foreach (JournalEntry item in s.Entries.Where((JournalEntry x) => x.TrainId == id && !x.Cancelled && x.Type == "Pociąg"))
			{
				if (item.Direction == "Z")
				{
					item.Arrived = arrival;
				}
				else if (item.Direction == "DO" && !arrivalOnly)
				{
					item.Left = departure;
				}
				ValidateTimes(item);
			}
			s.Entries.Add(Note("TEL", Now, "Pociąg " + train.Number + ": przyjazd " + Ui.FullTime(arrival) + ", odjazd " + Ui.FullTime(departure), "Czasy"));
		});
		NotifySimRailTime(id, previous);
	}

	public void SetDangerous(string id, bool value)
	{
		RequireWrite();
		if (State.Trains.First((Train x) => x.Id == id).Dangerous == value)
		{
			return;
		}
		Change("Oznaczenie TN", id, (AppState s) =>
		{
			Train train = s.Trains.First((Train x) => x.Id == id);
			train.Dangerous = value;
			if (train.DetailRows == null)
			{
				train.DetailRows = new List<DetailRow>();
			}
			train.DetailRows.Add(new DetailRow
			{
				Id = Guid.NewGuid().ToString("N"),
				Kind = "TN",
				Cells = new string[4]
				{
					DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
					User.Name,
					Station.Name,
					value ? "Wprowadzono oznaczenie TN" : "Usunięto oznaczenie TN"
				}
			});
		});
	}

	public static bool LineMovementActive(JournalEntry e, AppState state)
	{
		if (e.Left.HasValue && !e.Arrived.HasValue)
		{
			if (e.ReturnFromLine)
			{
				return !state.Entries.Any((JournalEntry r) => r.ReturnOfId == e.Id && !r.Cancelled && r.Arrived.HasValue);
			}
			return true;
		}
		return false;
	}

	private void NormalizeReturnEntry(JournalEntry e)
	{
		if (!string.IsNullOrEmpty(e.ReturnOfId))
		{
			JournalEntry journalEntry = State.Entries.FirstOrDefault((JournalEntry x) => x.Id == e.ReturnOfId && x.ReturnFromLine && !x.Cancelled);
			if (journalEntry == null)
			{
				throw new Exception("Brak aktywnego wyprawienia powiązanego z powrotem.");
			}
			if (e.SectionId != journalEntry.SectionId || e.StationId != journalEntry.StationId)
			{
				throw new Exception("Powrót musi pozostać w tym samym dzienniku co wyprawienie.");
			}
			e.Number = journalEntry.Number;
			e.Direction = "Z";
			e.Time = journalEntry.Time;
			e.LineTrack = journalEntry.LineTrack;
			e.Left = null;
			e.Permission = null;
			e.ReturnFromLine = false;
			e.ReturnArrival = null;
			if (journalEntry.Left.HasValue && e.Arrived.HasValue && e.Arrived < journalEntry.Left)
			{
				throw new Exception("Powrót nie może być wcześniejszy niż wyjazd na szlak.");
			}
		}
		else if (e.ReturnFromLine)
		{
			if (e.Type != "Pociąg" || e.Direction != "DO")
			{
				throw new Exception("Powrót ze szlaku wymaga wiersza wyprawienia.");
			}
			if (e.Left.HasValue && e.ReturnArrival.HasValue && e.ReturnArrival < e.Left)
			{
				throw new Exception("Powrót nie może być wcześniejszy niż wyjazd na szlak.");
			}
			e.Arrived = null;
		}
		else
		{
			JournalEntry journalEntry2 = State.Entries.FirstOrDefault((JournalEntry x) => x.ReturnOfId == e.Id);
			if (journalEntry2 != null && (journalEntry2.Arrived.HasValue || !string.IsNullOrWhiteSpace(journalEntry2.Track)))
			{
				throw new Exception("Powiązany powrót zawiera już dane. Nie można usunąć go przez odznaczenie pola; wykreśl błędną parę wpisów.");
			}
		}
	}

	private static void SynchronizeReturnPair(AppState state, JournalEntry e)
	{
		if (!string.IsNullOrEmpty(e.ReturnOfId))
		{
			JournalEntry journalEntry = state.Entries.First((JournalEntry x) => x.Id == e.ReturnOfId);
			journalEntry.ReturnArrival = e.Arrived;
			return;
		}
		JournalEntry journalEntry2 = state.Entries.FirstOrDefault((JournalEntry x) => x.ReturnOfId == e.Id);
		if (!e.ReturnFromLine)
		{
			if (journalEntry2 != null)
			{
				state.Entries.Remove(journalEntry2);
			}
			return;
		}
		if (journalEntry2 == null)
		{
			JournalEntry journalEntry3 = new JournalEntry();
			journalEntry3.Id = Guid.NewGuid().ToString("N");
			journalEntry3.ReturnOfId = e.Id;
			journalEntry3.Type = "Pociąg";
			journalEntry3.Track = "";
			journalEntry3.Text = "";
			journalEntry3.User = e.User;
			journalEntry2 = journalEntry3;
			state.Entries.Add(journalEntry2);
		}
		journalEntry2.StationId = e.StationId;
		journalEntry2.SectionId = e.SectionId;
		journalEntry2.TrainId = e.TrainId;
		journalEntry2.Number = e.Number;
		journalEntry2.Direction = "Z";
		journalEntry2.Time = e.Time;
		journalEntry2.LineTrack = e.LineTrack;
		journalEntry2.Arrived = e.ReturnArrival;
	}

	private static void CancelReturnPair(AppState state, JournalEntry entry, string reason)
	{
		string root = (string.IsNullOrEmpty(entry.ReturnOfId) ? entry.Id : entry.ReturnOfId);
		foreach (JournalEntry item in state.Entries.Where((JournalEntry x) => x.Id == root || x.ReturnOfId == root))
		{
			item.Cancelled = true;
			item.CancelReason = reason;
		}
	}
}

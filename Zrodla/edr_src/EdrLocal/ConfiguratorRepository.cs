using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EdrLocal;

public sealed class ConfiguratorRepository
{
	public readonly string Simulator;

	public readonly string Root;

	public readonly List<string> Warnings = new List<string>();

	public string DataFile => Path.Combine(Root, "konfiguratory", Simulator, "administracja.json");

	public string Profiles => Path.Combine(Root, "posterunki", Simulator);

	public AdministrationData Data { get; private set; }

	public string DefaultSectionId
	{
		get
		{
			ConfigurationUnit section = Data.Units.FirstOrDefault((ConfigurationUnit x) => x.Id == Data.DefaultSectionId && x.Kind == "ISE");
			if (section == null || !Data.Units.Any((ConfigurationUnit x) => x.Id == section.ParentId && x.Kind == "IZ"))
			{
				return null;
			}
			return section.Id;
		}
	}

	public string CatalogFile => Path.Combine(Root, "konfiguratory", Simulator, "katalog-pl3.json");

	public static string Provider(string value)
	{
		if (value != "simrail" && value != "td2")
		{
			throw new InvalidDataException("Wybierz konfigurator SimRail albo TD2.");
		}
		return value;
	}

	public ConfiguratorRepository(string simulator, string root)
	{
		Simulator = Provider(simulator);
		if (root == null && Ui.Testing)
		{
			throw new InvalidOperationException("Test wymaga własnego katalogu konfiguracji.");
		}
		Root = root ?? DataLocations.ConfigurationRoot;
		if (root == null)
		{
			DataLocations.StationProfiles(Simulator);
		}
	}

	public void Load()
	{
		Warnings.Clear();
		bool flag = false;
		if (File.Exists(DataFile))
		{
			if (new FileInfo(DataFile).Length > 16777216)
			{
				throw new InvalidDataException("Plik konfiguratora jest zbyt duży.");
			}
			Data = Json.Read<AdministrationData>(File.ReadAllBytes(DataFile));
			flag = MigrateStructure(Data);
			Validate(Data);
		}
		else
		{
			Data = new AdministrationData
			{
				SchemaVersion = 2,
				Provider = Simulator,
				Units = new List<ConfigurationUnit>(),
				Employees = new List<ConfigurationEmployee>(),
				Assignments = new List<ConfigurationAssignment>(),
				Stations = new List<ConfiguredStation>()
			};
		}
		if (Directory.Exists(Profiles))
		{
			foreach (string item in Directory.GetFiles(Profiles, "*.json").Take(1000))
			{
				try
				{
					if (new FileInfo(item).Length > 4194304)
					{
						throw new InvalidDataException("Za duży profil.");
					}
					StationProfile profile = Json.Read<StationProfile>(File.ReadAllBytes(item));
					if (profile == null || profile.SchemaVersion != 1 || profile.Station == null || !profile.Station.SimulatorReviewed || Path.GetFileNameWithoutExtension(item) != profile.PointId)
					{
						throw new InvalidDataException("Nieprawidłowy profil.");
					}
					ValidateKey(profile.PointId);
					if (Simulator == "simrail" && profile.Station.SimRailPointId != profile.PointId)
					{
						throw new InvalidDataException("Profil innego posterunku.");
					}
					Station station = Json.Clone(profile.Station);
					ValidateJournalIds(station);
					StationSettings.Normalize(station);
					StationSettings.Validate(station, EmptyState(station));
					ConfiguredStation configuredStation = Data.Stations.FirstOrDefault((ConfiguredStation x) => x.Key == profile.PointId);
					if (configuredStation != null)
					{
						configuredStation.Station = station;
						continue;
					}
					Data.Stations.Add(new ConfiguredStation
					{
						Key = profile.PointId,
						UnitId = null,
						Registered = false,
						Station = station
					});
				}
				catch (Exception ex)
				{
					Warnings.Add(Path.GetFileName(item) + ": " + ex.Message);
				}
			}
		}
		Validate(Data);
		bool flag2 = InitializeDefaultStructure();
		Validate(Data);
		if (flag)
		{
			string text = DataFile + ".struktura-v1.bak";
			if (!File.Exists(text))
			{
				File.Copy(DataFile, text);
			}
		}
		if (flag || flag2)
		{
			AtomicWrite(DataFile, Json.Bytes(Data));
		}
	}

	public static AppState EmptyState(Station station)
	{
		AppState appState = new AppState();
		appState.Stations = new List<Station> { station };
		appState.Entries = new List<JournalEntry>();
		appState.Trains = new List<Train>();
		return appState;
	}

	private static void ValidateJournalIds(Station station)
	{
		if (string.IsNullOrWhiteSpace(station.Id) || station.Sections == null || station.Sections.Any((Section x) => x == null || string.IsNullOrWhiteSpace(x.Id)) || station.Sections.Select((Section x) => x.Id).Distinct().Count() != station.Sections.Count)
		{
			throw new InvalidDataException("Dzienniki muszą mieć różne, niepuste identyfikatory.");
		}
	}

	private void ValidateKey(string key)
	{
		if (!string.IsNullOrEmpty(key))
		{
			bool num;
			if (!(Simulator == "simrail"))
			{
				if (key.Length <= 20 && key.All(char.IsDigit))
				{
					return;
				}
				num = !Guid.TryParseExact(key, "N", out var _);
			}
			else
			{
				if (key.Length > 20)
				{
					goto IL_0071;
				}
				num = !key.All(char.IsDigit);
			}
			if (!num)
			{
				return;
			}
		}
		goto IL_0071;
		IL_0071:
		throw new InvalidDataException("Nieprawidłowy identyfikator posterunku dla tego symulatora.");
	}

	private void Validate(AdministrationData data)
	{
		if (data == null || data.SchemaVersion != 2 || data.Provider != Simulator || data.Units == null || data.Employees == null || data.Assignments == null || data.Stations == null)
		{
			throw new InvalidDataException("Nieprawidłowy plik administracji. Istniejący plik nie został zastąpiony.");
		}
		if (data.Units.Any((ConfigurationUnit x) => x == null || string.IsNullOrWhiteSpace(x.Id) || string.IsNullOrWhiteSpace(x.Name)) || data.Units.Select((ConfigurationUnit x) => x.Id).Distinct().Count() != data.Units.Count)
		{
			throw new InvalidDataException("Nieprawidłowe jednostki organizacyjne.");
		}
		if (data.Stations.Any((ConfiguredStation x) => x == null || x.Station == null || string.IsNullOrWhiteSpace(x.Station.Name) || (x.Registered != false && !data.Units.Any((ConfigurationUnit u) => u.Id == x.UnitId && u.Kind == "ISE")) || (!string.IsNullOrEmpty(x.UnitId) && !data.Units.Any((ConfigurationUnit u) => u.Id == x.UnitId && u.Kind == "ISE"))) || data.Stations.Select((ConfiguredStation x) => x.Key).Distinct().Count() != data.Stations.Count)
		{
			throw new InvalidDataException("Nieprawidłowa lista posterunków.");
		}
		foreach (ConfiguredStation station in data.Stations)
		{
			ValidateKey(station.Key);
		}
		ValidateStructure(data);
	}

	internal static void AtomicWrite(string path, byte[] data)
	{
		LocalPersistence.RequireEnabled();
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		string text = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			using (FileStream fileStream = new FileStream(text, FileMode.CreateNew, FileAccess.Write, FileShare.None))
			{
				fileStream.Write(data, 0, data.Length);
				fileStream.Flush(flushToDisk: true);
			}
			if (File.Exists(path))
			{
				File.Replace(text, path, path + ".bak", ignoreMetadataErrors: true);
			}
			else
			{
				File.Move(text, path);
			}
		}
		finally
		{
			if (File.Exists(text))
			{
				File.Delete(text);
			}
		}
	}

	private void Commit(Action<AdministrationData> action)
	{
		AdministrationData administrationData = Json.Clone(Data);
		action(administrationData);
		Validate(administrationData);
		AtomicWrite(DataFile, Json.Bytes(administrationData));
		Data = administrationData;
	}

	public string AddUnit(string name)
	{
		return AddDepartment(name);
	}

	public void SaveEmployee(ConfigurationEmployee employee)
	{
		employee = Json.Clone(employee);
		employee.FirstName = (employee.FirstName ?? "").Trim();
		employee.LastName = (employee.LastName ?? "").Trim();
		employee.Identifier = (employee.Identifier ?? "").Trim();
		if (string.IsNullOrEmpty(employee.Id))
		{
			employee.Id = Guid.NewGuid().ToString("N");
		}
		Commit((AdministrationData s) =>
		{
			s.Employees.RemoveAll((ConfigurationEmployee x) => x.Id == employee.Id);
			s.Employees.Add(employee);
		});
	}

	public void RemoveEmployee(string id)
	{
		Commit((AdministrationData s) =>
		{
			s.Assignments.RemoveAll((ConfigurationAssignment x) => x.EmployeeId == id);
			s.Employees.RemoveAll((ConfigurationEmployee x) => x.Id == id);
		});
	}

	public void SetAssignments(string unit, string station, string role, IEnumerable<string> employeeIds)
	{
		List<string> ids = employeeIds.Distinct().ToList();
		Commit((AdministrationData s) =>
		{
			s.Assignments.RemoveAll((ConfigurationAssignment x) => x.UnitId == unit && x.StationKey == station && x.Role == role);
			s.Assignments.AddRange(ids.Select((string id) => new ConfigurationAssignment
			{
				UnitId = unit,
				StationKey = station,
				Role = role,
				EmployeeId = id
			}));
		});
	}

	public void Register(string unit, string key, Station station)
	{
		List<ConfiguredStation> list = Data.Stations.Where((ConfiguredStation x) => x.UnitId == unit && x.Registered != false).Select(Json.Clone).ToList();
		list.Add(new ConfiguredStation
		{
			Key = key,
			UnitId = unit,
			Station = station
		});
		SetRegistration(unit, list);
	}

	public void SetRegistered(string unit, IEnumerable<string> keys)
	{
		HashSet<string> ids = new HashSet<string>(keys);
		SetRegistration(unit, Data.Stations.Where((ConfiguredStation x) => ids.Contains(x.Key)).Select(Json.Clone).ToList());
	}

	public void SetRegistration(string unit, List<ConfiguredStation> selected)
	{
		Commit((AdministrationData s) =>
		{
			RequireSection(s, unit);
			if (selected.Select((ConfiguredStation x) => x.Key).Distinct().Count() != selected.Count)
			{
				throw new Exception("Ten sam posterunek został wybrany dwa razy.");
			}
			foreach (ConfiguredStation record in selected)
			{
				ValidateKey(record.Key);
				List<ConfiguredStation> stations = s.Stations;
				Func<ConfiguredStation, bool> predicate = (ConfiguredStation x) => x.Key == record.Key;
				ConfiguredStation configuredStation = stations.FirstOrDefault(predicate);
				if (configuredStation != null && configuredStation.Registered != false && configuredStation.UnitId != unit)
				{
					throw new Exception("Posterunek należy do innej sekcji. Użyj polecenia Przenieś do sekcji.");
				}
				if (configuredStation == null)
				{
					ConfiguredStation configuredStation2 = Json.Clone(record);
					configuredStation2.UnitId = unit;
					if (Simulator == "simrail")
					{
						configuredStation2.Station.SimRailPointId = configuredStation2.Key;
					}
					else if (configuredStation2.Key.Length <= 20 && configuredStation2.Key.All(char.IsDigit))
					{
						configuredStation2.Station.SimRailPointId = configuredStation2.Key;
					}
					else
					{
						configuredStation2.Station.SimRailPointId = null;
					}
					StationSettings.Normalize(configuredStation2.Station);
					s.Stations.Add(configuredStation2);
				}
				else
				{
					configuredStation.UnitId = unit;
				}
			}
			HashSet<string> hashSet = new HashSet<string>(selected.Select((ConfiguredStation x) => x.Key));
			foreach (ConfiguredStation item in s.Stations.Where((ConfiguredStation x) => x.UnitId == unit))
			{
				item.Registered = hashSet.Contains(item.Key);
			}
		});
	}

	public void SaveStation(string key, Station station)
	{
		ValidateKey(key);
		ConfiguredStation configuredStation = Data.Stations.First((ConfiguredStation x) => x.Key == key);
		station = Json.Clone(station);
		station.Id = configuredStation.Station.Id;
		station.SimRailPointId = ((Simulator == "simrail" || (key.Length <= 20 && key.All(char.IsDigit))) ? key : null);
		ValidateJournalIds(station);
		StationSettings.Normalize(station);
		StationSettings.Validate(station, EmptyState(station));
		station.SimulatorReviewed = true;
		station.SimulatorIdentityVersion = 1;
		station.RouteNeighboursVersion = 2;
		station.SimulatorTopologyVersion = 2;
		station.ConfiguratorRevision = Guid.NewGuid().ToString("N");
		foreach (Section section in station.Sections)
		{
			section.ManualConfiguration = true;
		}
		AdministrationData administrationData = Json.Clone(Data);
		administrationData.Stations.First((ConfiguredStation x) => x.Key == key).Station = station;
		Validate(administrationData);
		AtomicWrite(Path.Combine(Profiles, key + ".json"), Json.Bytes(new StationProfile
		{
			SchemaVersion = 1,
			PointId = key,
			SavedUtc = DateTime.UtcNow,
			Station = station
		}));
		AtomicWrite(DataFile, Json.Bytes(administrationData));
		Data = administrationData;
	}

	public Engine DraftEngine(string key)
	{
		Station station = Json.Clone(Data.Stations.First((ConfiguredStation x) => x.Key == key).Station);
		Store store = new Store(Path.Combine(Root, "roboczy", Simulator), transient: true);
		store.Load();
		store.Commit((AppState s) =>
		{
			s.Stations = new List<Station> { station };
			s.Trains.Clear();
			s.Entries.Clear();
			s.Duties.Clear();
			s.History.Clear();
			s.Services.Clear();
		});
		Engine engine = new Engine(store);
		engine.ConfigurationSimulator = Simulator;
		engine.User = store.State.Users.First((LocalUser x) => x.Id == "DEMO");
		return engine;
	}

	private bool InitializeDefaultStructure()
	{
		if (Data.DefaultStructureInitialized)
		{
			return false;
		}
		if (Data.Units.Count == 0)
		{
			string text = Guid.NewGuid().ToString("N");
			string text2 = Guid.NewGuid().ToString("N");
			Data.Units.Add(new ConfigurationUnit
			{
				Id = text,
				Kind = "IZ",
				Name = ((Simulator == "simrail") ? "SimRail" : "TTSK")
			});
			Data.Units.Add(new ConfigurationUnit
			{
				Id = text2,
				ParentId = text,
				Kind = "ISE",
				Name = ((Simulator == "simrail") ? "EDR" : "TTSK")
			});
			Data.DefaultSectionId = text2;
			Data.ExpandedNodes = new List<string>
			{
				"u:" + text,
				"u:" + text2,
				"p:" + text2
			};
			Data.SelectedNode = "u:" + text2;
		}
		Data.DefaultStructureInitialized = true;
		return true;
	}

	private bool MigrateStructure(AdministrationData data)
	{
		if (data == null || data.SchemaVersion != 1)
		{
			return false;
		}
		if (data.Provider != Simulator || data.Units == null || data.Stations == null || data.Units.Any((ConfigurationUnit x) => x == null || string.IsNullOrWhiteSpace(x.Id) || string.IsNullOrWhiteSpace(x.Name)) || data.Units.Select((ConfigurationUnit x) => x.Id).Distinct().Count() != data.Units.Count)
		{
			throw new InvalidDataException("Nieprawidłowa struktura starszego konfiguratora.");
		}
		string text = Guid.NewGuid().ToString("N");
		foreach (ConfigurationUnit unit in data.Units)
		{
			unit.Kind = "ISE";
			unit.ParentId = text;
		}
		if (data.Units.Count > 0)
		{
			data.Units.Insert(0, new ConfigurationUnit
			{
				Id = text,
				Name = "EDR",
				Kind = "IZ"
			});
		}
		data.SchemaVersion = 2;
		data.Employees = data.Employees ?? new List<ConfigurationEmployee>();
		data.Assignments = data.Assignments ?? new List<ConfigurationAssignment>();
		return true;
	}

	private static void ValidateStructure(AdministrationData data)
	{
		foreach (ConfigurationUnit unit in data.Units)
		{
			if ((unit.Kind != "IZ" && unit.Kind != "ISE") || (unit.Kind == "IZ" && !string.IsNullOrEmpty(unit.ParentId)) || (unit.Kind == "ISE" && !data.Units.Any((ConfigurationUnit x) => x.Id == unit.ParentId && x.Kind == "IZ")))
			{
				throw new InvalidDataException("Struktura musi mieć układ: zakład IZ → sekcja ISE → posterunki.");
			}
		}
		if (data.Units.GroupBy((ConfigurationUnit x) => (x.ParentId ?? "") + "|" + x.Kind + "|" + x.Name.Trim(), StringComparer.CurrentCultureIgnoreCase).Any((IGrouping<string, ConfigurationUnit> x) => x.Count() > 1))
		{
			throw new InvalidDataException("Ta nazwa jest już użyta w wybranej gałęzi struktury.");
		}
	}

	private static void RequireSection(AdministrationData data, string id)
	{
		if (!data.Units.Any((ConfigurationUnit x) => x.Id == id && x.Kind == "ISE"))
		{
			throw new Exception("Wybierz sekcję ISE, do której należą posterunki.");
		}
	}

	private static string Name(string value)
	{
		value = (value ?? "").Trim();
		if (value.Length == 0)
		{
			throw new Exception("Wpisz nazwę.");
		}
		return value;
	}

	private static string UnitName(string kind, string value)
	{
		value = Name(value);
		if (value.StartsWith(kind + " ", StringComparison.OrdinalIgnoreCase))
		{
			value = Name(value.Substring(kind.Length + 1));
		}
		return value;
	}

	public static string UnitCaption(ConfigurationUnit unit)
	{
		string text = unit.Name.Trim();
		if (!text.StartsWith(unit.Kind + " ", StringComparison.OrdinalIgnoreCase))
		{
			return unit.Kind + " " + text.ToUpper();
		}
		return text.ToUpper();
	}

	public string SectionCaption(string id)
	{
		ConfigurationUnit section = Data.Units.First((ConfigurationUnit x) => x.Id == id && x.Kind == "ISE");
		return UnitCaption(Data.Units.First((ConfigurationUnit x) => x.Id == section.ParentId)) + " / " + UnitCaption(section);
	}

	public string AddDepartment(string name)
	{
		name = UnitName("IZ", name);
		string id = Guid.NewGuid().ToString("N");
		Commit((AdministrationData s) =>
		{
			s.Units.Add(new ConfigurationUnit
			{
				Id = id,
				Name = name,
				Kind = "IZ"
			});
		});
		return id;
	}

	public string AddSection(string department, string name)
	{
		name = UnitName("ISE", name);
		string id = Guid.NewGuid().ToString("N");
		Commit((AdministrationData s) =>
		{
			if (!s.Units.Any((ConfigurationUnit x) => x.Id == department && x.Kind == "IZ"))
			{
				throw new Exception("Wybierz zakład IZ dla nowej sekcji.");
			}
			s.Units.Add(new ConfigurationUnit
			{
				Id = id,
				ParentId = department,
				Name = name,
				Kind = "ISE"
			});
		});
		return id;
	}

	public void RenameUnit(string id, string name)
	{
		name = UnitName(Data.Units.First((ConfigurationUnit x) => x.Id == id).Kind, name);
		Commit((AdministrationData s) =>
		{
			s.Units.First((ConfigurationUnit x) => x.Id == id).Name = name;
		});
	}

	public void MoveSection(string id, string department)
	{
		Commit((AdministrationData s) =>
		{
			ConfigurationUnit configurationUnit = s.Units.First((ConfigurationUnit x) => x.Id == id && x.Kind == "ISE");
			if (!s.Units.Any((ConfigurationUnit x) => x.Id == department && x.Kind == "IZ"))
			{
				throw new Exception("Wybierz docelowy zakład IZ.");
			}
			configurationUnit.ParentId = department;
		});
	}

	public void MoveStation(string key, string section)
	{
		Commit((AdministrationData s) =>
		{
			RequireSection(s, section);
			ConfiguredStation configuredStation = s.Stations.First((ConfiguredStation x) => x.Key == key);
			configuredStation.UnitId = section;
			configuredStation.Registered = true;
		});
	}

	public void UnregisterStation(string key)
	{
		Commit((AdministrationData s) =>
		{
			s.Stations.First((ConfiguredStation x) => x.Key == key).Registered = false;
		});
	}

	private static ConfigurationRemoval Removal(AdministrationData data, string id)
	{
		ConfigurationUnit unit = data.Units.First((ConfigurationUnit x) => x.Id == id);
		HashSet<string> ids = new HashSet<string> { id };
		foreach (ConfigurationUnit item in data.Units.Where((ConfigurationUnit x) => x.ParentId == id))
		{
			ids.Add(item.Id);
		}
		ConfigurationRemoval configurationRemoval = new ConfigurationRemoval();
		configurationRemoval.Caption = UnitCaption(unit);
		configurationRemoval.UnitIds = ids.ToList();
		configurationRemoval.SectionCount = Enumerable.Count(data.Units, (ConfigurationUnit x) => ids.Contains(x.Id) && x.Kind == "ISE");
		configurationRemoval.StationKeys = (from x in data.Stations
			where ids.Contains(x.UnitId)
			select x.Key).ToList();
		configurationRemoval.StationCount = Enumerable.Count(data.Stations, (ConfiguredStation x) => ids.Contains(x.UnitId) && x.Registered != false);
		return configurationRemoval;
	}

	public ConfigurationRemoval PreviewUnitRemoval(string id)
	{
		return Removal(Data, id);
	}

	public void RemoveUnit(string id)
	{
		Commit((AdministrationData s) =>
		{
			ConfigurationRemoval plan = Removal(s, id);
			string parentId = s.Units.First((ConfigurationUnit x) => x.Id == id).ParentId;
			HashSet<string> hashSet = new HashSet<string>(plan.UnitIds.Select((string x) => "u:" + x).Concat(plan.UnitIds.Select((string x) => "p:" + x)).Concat(plan.StationKeys.Select((string x) => "s:" + x)));
			foreach (ConfiguredStation item in s.Stations.Where((ConfiguredStation x) => plan.UnitIds.Contains(x.UnitId)))
			{
				item.UnitId = null;
				item.Registered = false;
			}
			s.Units.RemoveAll((ConfigurationUnit x) => plan.UnitIds.Contains(x.Id));
			if (s.ExpandedNodes != null)
			{
				s.ExpandedNodes.RemoveAll(hashSet.Contains);
			}
			if (hashSet.Contains(s.SelectedNode ?? ""))
			{
				s.SelectedNode = ((parentId == null) ? null : ("u:" + parentId));
			}
		});
	}

	public void SaveTreeState(IEnumerable<string> expanded, string selected)
	{
		List<string> keys = expanded.Distinct().Take(5000).ToList();
		if (Data.ExpandedNodes == null || !keys.SequenceEqual(Data.ExpandedNodes) || !(selected == Data.SelectedNode))
		{
			Commit((AdministrationData s) =>
			{
				s.ExpandedNodes = keys;
				s.SelectedNode = selected;
			});
		}
	}

	public void SaveCatalog(List<ConfiguredStation> stations)
	{
		if (Simulator != "simrail")
		{
			throw new Exception("Katalog PL3 dotyczy SimRail.");
		}
		List<ConfiguredStation> stations2 = stations.Select((ConfiguredStation x) => new ConfiguredStation
		{
			Key = x.Key,
			Station = Json.Clone(x.Station),
			Registered = false
		}).ToList();
		ValidateCatalog(stations2);
		AtomicWrite(CatalogFile, Json.Bytes(new ConfigurationCatalogCache
		{
			SchemaVersion = 1,
			ServerCode = "pl3",
			SavedUtc = DateTime.UtcNow,
			Stations = stations2
		}));
	}

	private void ValidateCatalog(List<ConfiguredStation> stations)
	{
		if (stations == null || stations.Count == 0 || stations.Count > 5000 || stations.Any((ConfiguredStation x) => x == null || x.Station == null || string.IsNullOrWhiteSpace(x.Station.Name)) || stations.Select((ConfiguredStation x) => x.Key).Distinct().Count() != stations.Count)
		{
			throw new InvalidDataException("Nieprawidłowy katalog posterunków PL3.");
		}
		foreach (ConfiguredStation station in stations)
		{
			ValidateKey(station.Key);
			if (station.Station.SimRailPointId != station.Key)
			{
				throw new InvalidDataException("Niezgodny identyfikator w katalogu PL3.");
			}
		}
	}

	public ConfigurationCatalogCache ReadCatalog()
	{
		if (Simulator != "simrail" || !File.Exists(CatalogFile))
		{
			return null;
		}
		try
		{
			if (new FileInfo(CatalogFile).Length > 16777216)
			{
				throw new InvalidDataException("Za duży katalog PL3.");
			}
			ConfigurationCatalogCache configurationCatalogCache = Json.Read<ConfigurationCatalogCache>(File.ReadAllBytes(CatalogFile));
			if (configurationCatalogCache == null || configurationCatalogCache.SchemaVersion != 1 || configurationCatalogCache.ServerCode != "pl3")
			{
				throw new InvalidDataException("Nieprawidłowa wersja katalogu PL3.");
			}
			ValidateCatalog(configurationCatalogCache.Stations);
			return configurationCatalogCache;
		}
		catch (Exception ex)
		{
			Warnings.Add("katalog-pl3.json: " + ex.Message);
			return null;
		}
	}
}

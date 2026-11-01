using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace EdrLocal;

public static class Profiles27Tests
{
	private static readonly List<string> log = new List<string>();

	private static void Check(bool ok, string text)
	{
		if (!ok)
		{
			throw new Exception("FAIL: " + text);
		}
		log.Add("PASS: " + text);
	}

	private static object Field(object o, string n)
	{
		return o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
	}

	private static SimRailPoint Point(string id, string name, double km)
	{
		SimRailPoint simRailPoint = new SimRailPoint();
		simRailPoint.pointId = id;
		simRailPoint.nameForPerson = name;
		simRailPoint.line = 171;
		simRailPoint.mileage = km;
		simRailPoint.track = 1;
		simRailPoint.displayedTrainNumber = "444015";
		simRailPoint.isActive = true;
		simRailPoint.arrivalTime = "2026-04-22 08:06:00";
		simRailPoint.departureTime = "2026-04-22 08:06:00";
		return simRailPoint;
	}

	private static SimRailTrain Train(string number, params SimRailPoint[] route)
	{
		for (int i = 0; i < route.Length; i++)
		{
			route[i].indexOfPoint = i;
		}
		SimRailTrain simRailTrain = new SimRailTrain();
		simRailTrain.trainNoLocal = number;
		simRailTrain.startStation = route.First().nameForPerson;
		simRailTrain.endStation = route.Last().nameForPerson;
		simRailTrain.timetable = route.ToList();
		return simRailTrain;
	}

	private static SimRailSnapshot Snapshot(string server)
	{
		SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
		simRailSnapshot.ServerCode = server;
		simRailSnapshot.GameTime = new DateTime(2026, 4, 22, 8, 30, 0);
		simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
		simRailSnapshot.DispatcherNames = new HashSet<string> { "Juliusz", "Dorota" };
		simRailSnapshot.Trains = new List<SimRailTrain>
		{
			Train("444015", Point("100", "Dorota", 0.0), Point("101", "Juliusz", 10.0), Point("102", "Sosnowiec Dańdówka", 20.0)),
			Train("444016", Point("102", "Sosnowiec Dańdówka", 20.0), Point("101", "Juliusz", 10.0), Point("100", "Dorota", 0.0)),
			Train("other", Point("900", "Dąbrowa Górnicza Towarowa", 9.0), Point("901", "Inna gałąź", 40.0))
		};
		return simRailSnapshot;
	}

	private static Engine Make(string folder, string server, string profiles, bool legacy)
	{
		SimRailSnapshot snapshot = Snapshot(server);
		SimRailSession simRailSession = new SimRailSession(null, snapshot, "101");
		Station station = simRailSession.SuggestStation(legacy);
		StationSettings.Normalize(station);
		Store store = new Store(Path.Combine(folder, "dane", "simrail", server, "101"));
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
		engine.SimRail = simRailSession;
		engine.User = store.State.Users.First((LocalUser x) => x.Id == "DEMO");
		engine.StationProfileDirectory = profiles;
		return engine;
	}

	public static bool Run(string folder)
	{
		Ui.Testing = true;
		Directory.CreateDirectory(folder);
		try
		{
			string text = Path.Combine(folder, "shared-profiles");
			Engine engine = Make(folder, "int2", text, legacy: true);
			Check(engine.Station.Sections.Any((Section x) => x.Name == "Dąbrowa Górnicza Towarowa"), "Przypadek odtwarza stary wybór innej gałęzi według kilometrażu.");
			Station station = engine.SimRail.SuggestStation();
			Check((from x in station.Sections
				select x.Name into x
				orderby x
				select x).SequenceEqual(new string[2] { "Dorota", "Sosnowiec Dańdówka" }.OrderBy((string x) => x)), "Nowa sugestia Juliusza wskazuje Dorotę i Sosnowiec Dańdówkę.");
			string oldId = engine.Station.Sections.First((Section x) => x.Name == "Dąbrowa Górnicza Towarowa").Id;
			engine.Store.Commit((AppState s) =>
			{
				s.Stations[0].SimulatorReviewed = true;
				s.Entries.Add(new JournalEntry
				{
					Id = "old",
					StationId = engine.StationId,
					SectionId = oldId,
					Time = engine.Now,
					Type = "Wpis",
					Text = "Starszy wpis",
					User = engine.User.Name
				});
			});
			Check(engine.RepairRouteNeighbours(), "Stara automatyczna konfiguracja jest naprawiana jednokrotnie.");
			Check(engine.Station.Sections.Any((Section x) => x.Id == oldId && x.Name == "Dorota") && engine.State.Entries.Single().SectionId == oldId, "Naprawa zachowuje identyfikator dziennika i starsze wpisy.");
			Check(!engine.Station.SimulatorReviewed && !engine.RepairRouteNeighbours(), "Naprawiona konfiguracja wymaga przeglądu i nie jest poprawiana ponownie.");
			engine.ApplySimRailSnapshot(engine.SimRail.Snapshot);
			Check(engine.State.Trains.All((Train x) => x.From != "Dąbrowa Górnicza Towarowa" && x.To != "Dąbrowa Górnicza Towarowa") && engine.State.Trains.Any((Train x) => x.From == "Dorota") && engine.State.Trains.Any((Train x) => x.To == "Dorota"), "Oba kierunki wykazu pociągów używają Doroty.");
			using (StationConfigurationForm stationConfigurationForm = new StationConfigurationForm(engine))
			{
				stationConfigurationForm.Show();
				Application.DoEvents();
				Station station2 = (Station)Field(stationConfigurationForm, "copy");
				station2.Sections.First((Section x) => x.Name == "Dorota").JournalName = "Dorota — własny dziennik";
				((RadioButton)Field(stationConfigurationForm, "customRelease")).Checked = true;
				((NumericUpDown)Field(stationConfigurationForm, "releaseSeconds")).Value = 120m;
				typeof(StationConfigurationForm).GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(stationConfigurationForm, null);
				Check(stationConfigurationForm.DialogResult == DialogResult.OK, "Zapis przez formularz kończy konfigurację poprawnie.");
			}
			string path = Path.Combine(text, "101.json");
			Check(File.Exists(path) && Json.Read<StationProfile>(File.ReadAllBytes(path)).Station.Sections.Any((Section x) => x.JournalName == "Dorota — własny dziennik"), "Formularz zapisuje lokalny profil z ręczną nazwą dziennika.");
			Check(engine.Station.Sections.All((Section x) => x.ManualConfiguration) && engine.Station.SimulatorReviewed, "Zatwierdzona konfiguracja chroni ręczne ustawienia.");
			Engine engine2 = Make(folder, "int3", text, legacy: false);
			engine2.InitializeStationProfiles();
			Check(engine2.Station.SimulatorReviewed && engine2.Station.Options.AutomaticReleaseSeconds == 120 && engine2.Station.Sections.Any((Section x) => x.JournalName == "Dorota — własny dziennik"), "Nowy serwer odtwarza profil i nie wymaga ponownego wpisywania konfiguracji.");
			Check(engine2.State.Entries.Count == 0 && engine2.State.Duties.Count == 0 && engine2.State.Trains.Count == 0, "Profil nie przenosi dzienników, dyżurów ani rozkładu między serwerami.");
			Store store = new Store(engine2.Store.DirectoryPath);
			store.Load();
			Engine engine3 = new Engine(store);
			engine3.SimRail = engine2.SimRail;
			engine3.User = store.State.Users.First((LocalUser x) => x.Id == "DEMO");
			engine3.StationProfileDirectory = text;
			Engine engine4 = engine3;
			engine4.InitializeStationProfiles();
			Check(engine4.Station.Sections.Any((Section x) => x.JournalName == "Dorota — własny dziennik") && engine4.Station.SimulatorReviewed, "Ponowne otwarcie zachowuje lokalną konfigurację.");
			Engine engine5 = Make(Path.Combine(folder, "new-version"), "int4", text, legacy: false);
			engine5.InitializeStationProfiles();
			Check(engine5.Station.SimulatorReviewed && engine5.Station.Options.AutomaticReleaseSeconds == 120, "Inny folder programu może użyć tego samego profilu posterunku.");
			engine2.Store.Commit((AppState s) =>
			{
				s.Stations[0].Sections.First((Section x) => x.Name == "Dorota").JournalName = "Odrębny dziennik int3";
			});
			engine2.InitializeStationProfiles();
			Check(engine2.Station.Sections.Any((Section x) => x.JournalName == "Odrębny dziennik int3"), "Istniejąca zatwierdzona konfiguracja serwera nie jest nadpisywana wspólnym profilem.");
			Engine withHistory = Make(folder, "int5", text, legacy: false);
			withHistory.Store.Commit((AppState s) =>
			{
				s.Entries.Add(new JournalEntry
				{
					Id = "keep",
					StationId = withHistory.StationId,
					SectionId = withHistory.Station.Sections[0].Id,
					Time = withHistory.Now,
					Type = "Wpis",
					Text = "Zachowaj",
					User = withHistory.User.Name
				});
			});
			string text2 = Json.Text(withHistory.Station);
			withHistory.InitializeStationProfiles();
			Check(Json.Text(withHistory.Station) == text2 && withHistory.State.Entries.Single().Id == "keep", "Przywracanie nie zastępuje konfiguracji zawierającej istniejące dzienniki.");
			Engine engine6 = Make(folder, "int6", text, legacy: true);
			engine6.Store.Commit((AppState s) =>
			{
				foreach (Section section in s.Stations[0].Sections)
				{
					section.ManualConfiguration = true;
				}
				s.Stations[0].SimulatorReviewed = true;
			});
			Check(!engine6.RepairRouteNeighbours() && engine6.Station.Sections.Any((Section x) => x.Name == "Dąbrowa Górnicza Towarowa"), "Nowe jawne ustawienia ręczne nie są nadpisywane automatyczną naprawą.");
			string folder2 = Path.Combine(folder, "old-portable");
			Engine engine7 = Make(folder2, "pl1", null, legacy: false);
			engine7.Store.Commit((AppState s) =>
			{
				s.Stations[0].SimulatorReviewed = true;
				s.Stations[0].Sections.First((Section x) => x.Name == "Dorota").JournalName = "Starszy profil Doroty";
			});
			Engine engine8 = Make(folder2, "pl2", Path.Combine(folder, "migrated-profiles"), legacy: false);
			engine8.InitializeStationProfiles();
			Check(engine8.Station.SimulatorReviewed && engine8.Station.Sections.Any((Section x) => x.JournalName == "Starszy profil Doroty") && File.Exists(Path.Combine(engine8.StationProfileDirectory, "101.json")), "Gotowa konfiguracja starszego serwera jest automatycznie przenoszona do profilu.");
			Engine engine9 = Make(folder, "int7", Path.Combine(folder, "corrupt-profiles"), legacy: false);
			Directory.CreateDirectory(engine9.StationProfileDirectory);
			File.WriteAllText(Path.Combine(engine9.StationProfileDirectory, "101.json"), "broken");
			engine9.InitializeStationProfiles();
			Check(File.ReadAllText(Path.Combine(engine9.StationProfileDirectory, "101.json")) == "broken", "Uszkodzony profil nie jest niszczony przy automatycznym odczycie.");
			StationProfile stationProfile = Json.Read<StationProfile>(File.ReadAllBytes(path));
			stationProfile.PointId = "999";
			stationProfile.Station.SimRailPointId = "999";
			Engine engine10 = Make(Path.Combine(folder, "isolated"), "pl3", Path.Combine(folder, "wrong-profile"), legacy: false);
			Directory.CreateDirectory(engine10.StationProfileDirectory);
			File.WriteAllBytes(Path.Combine(engine10.StationProfileDirectory, "101.json"), Json.Bytes(stationProfile));
			engine10.InitializeStationProfiles();
			Check(!engine10.Station.SimulatorReviewed, "Profil innego posterunku nie jest stosowany.");
			Engine engine11 = Make(Path.Combine(folder, "partial-isolated"), "pl4", Path.Combine(folder, "partial-profile"), legacy: false);
			Directory.CreateDirectory(engine11.StationProfileDirectory);
			File.WriteAllBytes(Path.Combine(engine11.StationProfileDirectory, "101.json"), Json.Bytes(new StationProfile
			{
				SchemaVersion = 1,
				PointId = "101",
				SavedUtc = DateTime.UtcNow,
				Station = new Station
				{
					SimRailPointId = "101",
					SimulatorReviewed = true
				}
			}));
			engine11.InitializeStationProfiles();
			Check(!engine11.Station.SimulatorReviewed, "Profil bez wymaganych list jest pomijany bez błędu otwarcia posterunku.");
			SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
			simRailSnapshot.Trains = new List<SimRailTrain>
			{
				Train("full", Point("1", "A", 0.0), Point("2", "B", 5.0), Point("3", "C", 10.0)),
				Train("fast", Point("1", "A", 0.0), Point("3", "C", 10.0)),
				Train("branch", Point("4", "X", 1.0), Point("5", "Y", 8.0))
			};
			SimRailSnapshot simRailSnapshot2 = simRailSnapshot;
			List<SimRailPoint> timetable = simRailSnapshot2.Trains[1].timetable;
			Check(simRailSnapshot2.Neighbour(timetable, 0, 1) == "B" && simRailSnapshot2.Neighbour(timetable, 1, -1) == "B", "Szczegółowa trasa uzupełnia sąsiada pominiętego przez ekspres na tej samej linii.");
			engine.ApplySimRailSnapshot(engine.SimRail.Snapshot);
			using (MainForm mainForm = new MainForm(engine))
			{
				mainForm.Show();
				Application.DoEvents();
				Check(typeof(MainForm).Assembly.GetName().Name == "EDR" && Path.GetFileName(Application.ExecutablePath) == "EDR.exe", "Nazwa assembly i pliku programu to EDR.");
				Check(mainForm.Text.Contains("EDR Produkcja (wersja 1.0.10.199)") && !mainForm.Text.Contains("Lokalny"), "Tytuł nowej wersji używa nazwy EDR.");
				Ui.Capture(mainForm, Path.Combine(folder, "juliusz.png"));
				mainForm.Dispose();
			}
			log.Add("ALL TESTS PASSED: " + log.Count);
			File.WriteAllLines(Path.Combine(folder, "wyniki-profile27.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
			return true;
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			File.WriteAllLines(Path.Combine(folder, "wyniki-profile27.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
			return false;
		}
	}
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace EdrLocal;

public static class Neighbours182Tests
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

	private static T Field<T>(object o, string n)
	{
		return (T)o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
	}

	private static SimRailSnapshot Snapshot(List<SimRailTrain> trains)
	{
		SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
		simRailSnapshot.ServerCode = "fixture182";
		simRailSnapshot.Trains = trains;
		simRailSnapshot.DispatcherNames = new HashSet<string>();
		simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
		simRailSnapshot.GameTime = SimRailProvider.Time(trains.First((SimRailTrain x) => x.trainNoLocal == "15104").timetable.First((SimRailPoint x) => x.pointId == "5703").arrivalTime).Value;
		return simRailSnapshot;
	}

	private static Engine EngineFor(string folder, SimRailSession session, Station station)
	{
		StationSettings.Normalize(station);
		Store store = new Store(Path.Combine(folder, Guid.NewGuid().ToString("N")));
		store.Load();
		store.Commit((AppState s) =>
		{
			s.Stations = new List<Station> { Json.Clone(station) };
			s.Trains.Clear();
			s.Entries.Clear();
			s.Duties.Clear();
			s.History.Clear();
		});
		Engine engine = new Engine(store);
		engine.SimRail = session;
		engine.User = store.State.Users.First((LocalUser x) => x.Id == "DEMO");
		return engine;
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		LocalPersistence.Enabled = false;
		try
		{
			string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src", "testdata", "lodz182.json");
			List<SimRailTrain> list = SimRailProvider.Parse<List<SimRailTrain>>(File.ReadAllBytes(path));
			SimRailSnapshot snapshot = Snapshot(list);
			SimRailSession simRailSession = new SimRailSession(null, snapshot, "5703");
			List<SimRailPoint> list2 = list.First((SimRailTrain x) => x.trainNoLocal == "15104").timetable.OrderBy((SimRailPoint x) => x.indexOfPoint).ToList();
			int at = list2.FindIndex((SimRailPoint x) => x.pointId == "5703");
			Station old = simRailSession.SuggestStationVersion(1);
			Check(snapshot.Topology.PriorNeighbour(list2, at, 1, snapshot.Topology.PointNames) == "Tomaszów Mazowiecki" && old.Sections.Any((Section x) => x.Name == "Tomaszów Mazowiecki"), "Zrzut API 15104 odtwarza błędny wybór Tomaszowa w algorytmie wersji 181.");
			Station station = simRailSession.SuggestStation();
			Check(station.Sections.Any((Section x) => x.Name == "Łódź Kaliska") && !station.Sections.Any((Section x) => x.Name == "Tomaszów Mazowiecki"), "Nowa sugestia zachowuje Łódź Kaliską i nie dodaje Tomaszowa z innej trasy.");
			StationSettings.Normalize(station);
			List<Train> source = simRailSession.Project(station);
			Train train = source.Single((Train x) => x.ExternalTrainNo == "15104");
			Check(train.From == "Łódź Chojny" && train.To == "Łódź Kaliska" && train.LineOut == "25", "15104: przyjazd z Chojen i odjazd do Łodzi Kaliskiej po linii 25 mimo znacznika linii 15 na następnym punkcie.");
			Check(source.Single((Train x) => x.ExternalTrainNo == "122001").From == "Łódź Kaliska" && source.Single((Train x) => x.ExternalTrainNo == "122001").To == "Łódź Chojny", "Przeciwny kierunek na linii 25 zachowuje prawidłowych sąsiadów.");
			Check(source.Single((Train x) => x.ExternalTrainNo == "152031").To == "Retkinia", "Odgałęzienie w stronę Retkini pozostaje zgodne z trasą.");
			List<SimRailTrain> list3 = Json.Clone(list);
			foreach (SimRailPoint item in from x in list3.SelectMany((SimRailTrain x) => x.timetable)
				where x.nameForPerson == "Tomaszów Mazowiecki"
				select x)
			{
				item.mileage = 1.576;
			}
			SimRailSession simRailSession2 = new SimRailSession(null, Snapshot(list3), "5703");
			Check(simRailSession2.Project(station).Single((Train x) => x.ExternalTrainNo == "15104").To == "Łódź Kaliska", "Bliższy kilometraż obcego odcinka nie zmienia sąsiada tego pociągu.");
			string[] allowed = new string[2] { "Łódź Chojny", "Tomaszów Mazowiecki" };
			Check(snapshot.Topology.Neighbour(list2, at, 1, allowed) != "Tomaszów Mazowiecki", "Ograniczenie starego profilu nie uprawnia do wyboru obcej gałęzi.");
			old.SimulatorReviewed = true;
			foreach (Section section in old.Sections)
			{
				section.ManualConfiguration = true;
			}
			Section wrong = old.Sections.Single((Section x) => x.Name == "Tomaszów Mazowiecki");
			Section keep = old.Sections.First((Section x) => x.Name == "Łódź Kaliska");
			keep.Tracks = 2;
			keep.Basis = "Zapowiadanie telefoniczne";
			keep.JournalName = "Mój dziennik Łódź Kaliska";
			old.JournalTabs = new List<JournalTabDefinition>
			{
				new JournalTabDefinition
				{
					LeftId = wrong.Id,
					RightId = keep.Id
				}
			};
			Engine engine = EngineFor(folder, simRailSession, old);
			engine.Store.Commit((AppState s) =>
			{
				s.Entries.Add(new JournalEntry
				{
					Id = "keep-history",
					StationId = old.Id,
					SectionId = keep.Id,
					Time = snapshot.GameTime,
					Type = "Wpis",
					Text = "Zachowany wpis"
				});
			});
			Check(engine.RepairRouteNeighbours(), "Zatwierdzona stara sugestia bez wpisów w błędnym dzienniku jest migrowana.");
			Check(!engine.Station.Sections.Any((Section x) => x.Name == "Tomaszów Mazowiecki") && engine.Station.Sections.Any((Section x) => x.Id == keep.Id && x.Tracks == 2 && x.Basis == "Zapowiadanie telefoniczne" && x.JournalName == keep.JournalName) && engine.State.Entries.Single().SectionId == keep.Id, "Usunięto wyłącznie pusty błędny dziennik; zachowano tory, podstawę, własną nazwę i historię poprawnego sąsiada.");
			Check(engine.Station.JournalTabs.Single().LeftId == keep.Id && engine.Station.JournalTabs.Single().RightId == null && !engine.RepairRouteNeighbours(), "Układ kart pozostaje poprawny, a migracja wykonuje się jeden raz.");
			engine.ApplySimRailSnapshot(snapshot);
			MainForm main = new MainForm(engine);
			try
			{
				main.Show();
				Application.DoEvents();
				main.Size = new Size(1920, 1040);
				main.SelectTrain(engine.State.Trains.Single((Train x) => x.ExternalTrainNo == "15104").Id);
				DataGridView dataGridView = Field<DataGridView>(main, "trains");
				DataGridViewRow dataGridViewRow = dataGridView.Rows.Cast<DataGridViewRow>().Single((DataGridViewRow x) => (string)x.Tag == main.SelectedTrain.Id);
				Check(Convert.ToString(dataGridViewRow.Cells["to"].Value) == "Łódź Kaliska" && !Field<TabControl>(main, "journalTabs").TabPages.Cast<TabPage>().Any((TabPage x) => x.Text.Contains("Tomaszów")), "Rzeczywisty wykaz i karty dzienników pokazują poprawionych sąsiadów.");
				Ui.Capture(main, Path.Combine(folder, "lodz-kaliska-15104.png"));
				main.Close();
			}
			finally
			{
				if (main != null)
				{
					((IDisposable)main).Dispose();
				}
			}
			Engine engine2 = EngineFor(folder, simRailSession, station);
			engine2.StationProfileDirectory = Path.Combine(folder, "profile");
			Directory.CreateDirectory(engine2.StationProfileDirectory);
			string path2 = Path.Combine(engine2.StationProfileDirectory, "5703.json");
			File.WriteAllBytes(path2, Json.Bytes(new StationProfile
			{
				SchemaVersion = 1,
				PointId = "5703",
				Station = Json.Clone(old),
				SavedUtc = DateTime.UtcNow
			}));
			engine2.InitializeStationProfiles();
			Check(engine2.Station.RouteNeighboursVersion == 1 && engine2.Station.Sections.Any((Section x) => x.Id == wrong.Id) && engine2.RepairRouteNeighbours() && !engine2.Station.Sections.Any((Section x) => x.Id == wrong.Id), "Ponowne połączenie naprawia również błędny dziennik odtworzony z profilu 181.");
			engine2.Store.Commit((AppState s) =>
			{
				s.Stations[0].SimulatorReviewed = true;
			});
			engine2.PersistStationProfile();
			StationProfile stationProfile = Json.Read<StationProfile>(File.ReadAllBytes(path2));
			Check(stationProfile.Station.RouteNeighboursVersion == 2 && !stationProfile.Station.Sections.Any((Section x) => x.Id == wrong.Id) && stationProfile.Station.Sections.Single((Section x) => x.Id == keep.Id).Tracks == 2, "Po zatwierdzeniu poprawiony profil utrwala nową topologię i wcześniejsze ustawienia torów.");
			Engine engine3 = EngineFor(folder, simRailSession, old);
			engine3.Store.Commit((AppState s) =>
			{
				s.Entries.Add(new JournalEntry
				{
					Id = "do-not-delete",
					StationId = old.Id,
					SectionId = wrong.Id,
					Time = snapshot.GameTime,
					Type = "Wpis",
					Text = "Historia błędnego dziennika"
				});
			});
			engine3.RepairRouteNeighbours();
			Check(engine3.State.Entries.Single().SectionId == wrong.Id && engine3.Station.Sections.Any((Section x) => x.Id == wrong.Id), "Dziennik z historią nie jest automatycznie usuwany.");
			Station station2 = Json.Clone(old);
			station2.Sections.Single((Section x) => x.Id == wrong.Id).JournalName = "Jawnie nazwany własny dziennik";
			Engine engine4 = EngineFor(folder, simRailSession, station2);
			engine4.RepairRouteNeighbours();
			Check(engine4.Station.Sections.Any((Section x) => x.Id == wrong.Id), "Własna nazwa dziennika chroni ręczną konfigurację przed usunięciem.");
			Station station3 = Json.Clone(old);
			station3.ConfiguratorRevision = "explicit-local-config";
			Engine engine5 = EngineFor(folder, simRailSession, station3);
			engine5.RepairRouteNeighbours();
			Check(engine5.Station.Sections.Any((Section x) => x.Id == wrong.Id), "Jawna rewizja konfiguratora nie jest zastępowana automatyczną migracją.");
			string path3 = Path.Combine(folder, "getEDRTimetables.json");
			if (File.Exists(path3))
			{
				List<SimRailTrain> list4 = SimRailProvider.Parse<List<SimRailTrain>>(File.ReadAllBytes(path3));
				Stopwatch stopwatch = Stopwatch.StartNew();
				SimRailSession simRailSession3 = new SimRailSession(null, Snapshot(list4), "5703");
				Station station4 = simRailSession3.SuggestStation();
				List<Train> list5 = simRailSession3.Project(station4);
				Check(list4.Count > 1000 && list5.Single((Train x) => x.ExternalTrainNo == "15104").To == "Łódź Kaliska" && !station4.Sections.Any((Section x) => x.Name == "Tomaszów Mazowiecki"), "Pełny zapisany zrzut API także wybiera Łódź Kaliską dla 15104 i nie tworzy szlaku do Tomaszowa.");
				Check(!list5.Any((Train x) => x.From == "Tomaszów Mazowiecki" || x.To == "Tomaszów Mazowiecki"), "Żaden przejazd przez ten PZS w pełnym zrzucie nie przejmuje Tomaszowa z obcej gałęzi.");
				stopwatch.Stop();
				log.Add("INFO: Pełny zrzut: " + list4.Count + " pociągów, " + list5.Count + " przejazdów przez PZS, " + stopwatch.ElapsedMilliseconds + " ms; sąsiedzi: " + string.Join(", ", station4.Sections.Select((Section x) => x.Name + " L. " + x.Line)));
			}
			log.Add("ALL OFFLINE NEIGHBOURS182 TESTS PASSED");
			return true;
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			return false;
		}
		finally
		{
			File.WriteAllLines(Path.Combine(folder, "wyniki-sasiedzi182.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

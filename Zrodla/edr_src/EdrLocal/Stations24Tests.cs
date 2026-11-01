using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace EdrLocal;

public static class Stations24Tests
{
	private static List<string> log = new List<string>();

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

	private static IEnumerable<Control> All(Control c)
	{
		yield return c;
		foreach (Control x in c.Controls)
		{
			foreach (Control item in All(x))
			{
				yield return item;
			}
		}
	}

	private static SimRailTrain Source(string number, int hour)
	{
		SimRailTrain simRailTrain = new SimRailTrain();
		simRailTrain.trainNoLocal = number;
		simRailTrain.startStation = "A";
		simRailTrain.endStation = "B";
		simRailTrain.timetable = new List<SimRailPoint>
		{
			new SimRailPoint
			{
				pointId = "9",
				nameForPerson = "Kozłów",
				indexOfPoint = 0,
				line = 64,
				mileage = 0.0
			},
			new SimRailPoint
			{
				pointId = "10",
				nameForPerson = "Starzyny",
				indexOfPoint = 1,
				line = 64,
				mileage = 5.0,
				track = 1,
				displayedTrainNumber = number,
				isActive = true,
				arrivalTime = "2026-04-21 " + hour.ToString("00") + ":00:00",
				departureTime = "2026-04-21 " + hour.ToString("00") + ":01:00"
			},
			new SimRailPoint
			{
				pointId = "11",
				nameForPerson = "Sprowa",
				indexOfPoint = 2,
				line = 64,
				mileage = 10.0,
				track = 1,
				displayedTrainNumber = number,
				isActive = true,
				arrivalTime = "2026-04-21 " + hour.ToString("00") + ":05:00",
				departureTime = "2026-04-21 " + hour.ToString("00") + ":06:00"
			},
			new SimRailPoint
			{
				pointId = "12",
				nameForPerson = "Psary",
				indexOfPoint = 3,
				line = 64,
				mileage = 15.0
			}
		};
		return simRailTrain;
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		try
		{
			SimRailSnapshot snapshot = new SimRailSnapshot
			{
				ServerCode = "test",
				Trains = new List<SimRailTrain>
				{
					Source("100", 8),
					Source("102", 12),
					Source("104", 20)
				},
				DispatcherNames = new HashSet<string> { "Starzyny" },
				GameTime = new DateTime(2026, 4, 21, 12, 10, 0),
				DownloadedUtc = DateTime.UtcNow
			};
			SimRailSession simRailSession = new SimRailSession(null, snapshot, "10");
			Check((from x in simRailSession.ControlledLocations()
				select x.Name).SequenceEqual(new string[2] { "Sprowa", "Starzyny" }), "Starzyny i Sprowa są dostępne mimo jednej nazwy na liście grywalnej.");
			simRailSession.SetAccessLink("https://api.simrail.eu:8093/?serverCode=test&stationId=10,11&key=PRIVATE");
			Check(SepeLink.Query(simRailSession.AccessUrl.Query)["stationId"] == "10", "Adres zapisu jest ograniczony do aktywnego posterunku.");
			simRailSession.SelectPoint("11");
			Check(simRailSession.HasWriteLink && SepeLink.Query(simRailSession.AccessUrl.Query)["stationId"] == "11", "Przełączenie wybiera właściwy posterunek z linku wieloposterunkowego.");
			Check(simRailSession.StoragePointId == "10", "Wspólna baza Starzyn i Sprowy ma ten sam folder z obu stron.");
			simRailSession.SelectPoint("10");
			Store store = new Store(Path.Combine(folder, "data", "10"));
			store.Load();
			Station star = simRailSession.SuggestStation();
			star.PostType = "STACJA WĘZŁOWA";
			store.Commit((AppState s) =>
			{
				s.Stations = new List<Station> { star };
				s.Trains.Clear();
				s.Entries.Clear();
				s.Duties.Clear();
			});
			Store store2 = new Store(Path.Combine(folder, "data", "11"));
			store2.Load();
			Station sprowa = simRailSession.SuggestStation("11");
			store2.Commit((AppState s) =>
			{
				s.Stations = new List<Station> { sprowa };
				s.Trains.Clear();
				s.Entries.Clear();
				s.Duties.Clear();
				s.Entries.Add(new JournalEntry
				{
					Id = "old-partner",
					StationId = sprowa.Id,
					SectionId = "TEL",
					Time = snapshot.GameTime.AddMinutes(-30.0),
					Type = "Wpis",
					Text = "Starszy wpis Sprowy",
					User = "Jan Kowalski"
				});
			});
			string text = File.ReadAllText(store2.FilePath);
			Engine engine = new Engine(store)
			{
				SimRail = simRailSession,
				User = store.State.Users.First((LocalUser x) => x.Id == "DEMO")
			};
			engine.ImportControlledStationData();
			engine.EnsureControlledStations();
			engine.ApplySimRailSnapshot(snapshot);
			Check(engine.State.Stations.Count == 2 && engine.State.Entries.Any((JournalEntry x) => x.Id == "old-partner"), "Dołączenie zachowuje istniejący dziennik drugiego posterunku.");
			Check(File.ReadAllText(store2.FilePath) == text, "Import nie zmienia starszej bazy partnera.");
			engine.ImportControlledStationData();
			engine.EnsureControlledStations();
			Check(engine.State.Stations.Count == 2 && Enumerable.Count(engine.State.Entries, (JournalEntry x) => x.Id == "old-partner") == 1, "Ponowne uruchomienie nie dubluje posterunków ani wpisów.");
			Check(StationIdentity.Type(engine.Station) == "POSTERUNEK ODGAŁĘŹNY" && MainForm.StationDisplay(engine.Station) == "STARZYNY (POSTERUNEK ODGAŁĘŹNY)", "Tytuł i lista pokazują poprawny typ Starzyn także po starszej konfiguracji.");
			Check(StationIdentity.Type(engine.State.Stations.First((Station x) => x.Name == "Sprowa")) == "POSTERUNEK ODGAŁĘŹNY", "Sprowa ma typ posterunku odgałęźnego.");
			engine.BeginDuty(engine.Now.AddMinutes(-20.0));
			JournalEntry note = engine.Note("TEL", engine.Now.AddMinutes(-2.0), "Rozmowa próbna", "Wpis");
			engine.SaveJournal(note);
			using (MainForm mainForm = new MainForm(engine))
			{
				mainForm.Show();
				Application.DoEvents();
				Check(mainForm.SelectedTrain.Number == "102", "Domyślnie zaznaczony jest pociąg najbliższy zegarowi serwera.");
				Train train = engine.State.Trains.First((Train x) => x.Number == "100");
				mainForm.SelectTrain(train.Id);
				mainForm.RefreshData();
				Check(mainForm.SelectedTrain.Id == train.Id, "Odświeżenie zachowuje ręcznie wybrany pociąg.");
				ComboBox comboBox = (ComboBox)Field(mainForm, "stations");
				Check(comboBox.Items.Count == 2 && comboBox.Items.Cast<string>().All((string x) => x.Contains("POSTERUNEK ODGAŁĘŹNY")), "Główna lista udostępnia oba podg z poprawnym typem.");
				comboBox.SelectedIndex = engine.State.Stations.FindIndex((Station x) => x.Name == "Sprowa");
				Application.DoEvents();
				Check(engine.Station.Name == "Sprowa" && simRailSession.PointId == "11" && mainForm.SelectedTrain.Number == "102", "Zmiana listy przełącza sesję, rozkład i domyślne zaznaczenie.");
				Check(engine.State.Trains.Where((Train x) => x.StationId == engine.StationId).All((Train x) => x.Id.Contains(":11:")), "Rozkład Sprowy zawiera identyfikatory właściwego punktu.");
				Check(engine.ActiveDuty == null && engine.State.Entries.Any((JournalEntry x) => x.Id == note.Id && x.StationId == star.Id), "Dyżury i dzienniki obu posterunków są oddzielne.");
				typeof(MainForm).GetField("sendingHours", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(mainForm, true);
				comboBox.SelectedIndex = engine.State.Stations.FindIndex((Station x) => x.Name == "Starzyny");
				Check(engine.Station.Name == "Sprowa" && comboBox.Text.StartsWith("SPROWA"), "Wysyłanie blokuje przełączenie adresata SEPE.");
				typeof(MainForm).GetField("sendingHours", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(mainForm, false);
				comboBox.SelectedIndex = engine.State.Stations.FindIndex((Station x) => x.Name == "Starzyny");
				Application.DoEvents();
				List<JournalPane> source = (List<JournalPane>)Field(mainForm, "panes");
				JournalPane journalPane = source.First((JournalPane x) => x.SectionId == "TEL");
				Check((from DataGridViewColumn x in journalPane.Grid.Columns
					select x.HeaderText).SequenceEqual(new string[5] { "Nr kol.", "Data", "Adres i treść", "Czas zakończenia", "Nadał / Odebrał" }), "Dziennik telefoniczny ma pięć kolumn we właściwej kolejności.");
				DataGridViewRow dataGridViewRow = journalPane.Grid.Rows.Cast<DataGridViewRow>().First((DataGridViewRow x) => ((JournalEntry)x.Tag).Id == note.Id);
				Check(Convert.ToString(dataGridViewRow.Cells["date"].Value) == "21-04-2026" && Convert.ToString(dataGridViewRow.Cells["time"].Value) == "12:08" && Convert.ToString(dataGridViewRow.Cells["recipient"].Value) == engine.User.Name, "Wpis ma datę, czas zakończenia i autora.");
				Check(journalPane.Grid.Rows.Cast<DataGridViewRow>().Any((DataGridViewRow x) => ((JournalEntry)x.Tag).Type == "Doba"), "Dziennik telefoniczny pokazuje wiersz północy.");
				Check(All(journalPane).OfType<ToolStrip>().SelectMany((ToolStrip x) => x.Items.Cast<ToolStripItem>()).Count((ToolStripItem x) => x.Text == "Rejestruj telefonogram" || x.Text == "Rejestruj inne okoliczności") == 2, "Nad tabelą są dwa przyciski tekstowe z przykładu.");
				journalPane.FocusEntry(note.Id);
				Ui.Capture(mainForm, Path.Combine(folder, "telefoniczny.png"));
				mainForm.Close();
			}
			simRailSession.SetAccessLink("https://api.simrail.eu:8093/?serverCode=test&stationId=10&key=PRIVATE");
			simRailSession.SelectPoint("11");
			Check(!simRailSession.HasWriteLink, "Link jednego punktu nie daje uprawnień zapisu do drugiego.");
			simRailSession.SelectPoint("10");
			Check(simRailSession.HasWriteLink, "Powrót przywraca link zapisu właściwego punktu.");
			Check(!File.ReadAllText(store.FilePath).Contains("PRIVATE"), "Klucz wieloposterunkowy nie trafia do bazy.");
			Store store3 = new Store(store.DirectoryPath);
			store3.Load();
			Check(store3.State.Stations.Count == 2 && store3.State.Entries.Any((JournalEntry x) => x.Id == note.Id), "Oba posterunki i dzienniki przetrwały ponowne wczytanie.");
			engine.SwitchSimulatorStation(sprowa.Id);
			engine.BeginDuty(engine.Now.AddMinutes(-5.0));
			JournalEntry secondNote = engine.Note("TEL", engine.Now.AddMinutes(-1.0), "Do resetu Sprowy", "Wpis");
			engine.SaveJournal(secondNote);
			engine.ResetSimRailJournals();
			Check(engine.State.Entries.Any((JournalEntry x) => x.Id == note.Id) && !engine.State.Entries.Any((JournalEntry x) => x.Id == secondNote.Id), "Reset Sprowy zachowuje dziennik Starzyn.");
			Check(engine.State.History.Any((Audit x) => x.Entity == note.Id), "Reset partnera zachowuje historię wpisów Starzyn.");
			engine.SwitchSimulatorStation(star.Id);
			store.Commit((AppState s) =>
			{
				s.Stations.First((Station x) => x.Id == star.Id).PostType = "STACJA";
			});
			engine.EnsureControlledStations();
			Check(StationIdentity.Type(engine.Station) == "STACJA", "Jawna zmiana typu po migracji nie jest ponownie nadpisywana.");
			Station outsider = simRailSession.SuggestStation("12");
			store.Commit((AppState s) =>
			{
				s.Stations.Add(outsider);
			});
			bool flag = false;
			try
			{
				engine.SwitchSimulatorStation(outsider.Id);
			}
			catch (InvalidOperationException)
			{
				flag = true;
			}
			Check(flag && engine.StationId == star.Id && simRailSession.PointId == "10", "Odrzucone przełączenie poza połączenie zachowuje poprzedni posterunek.");
			log.Add("ALL TESTS PASSED: " + log.Count);
			File.WriteAllLines(Path.Combine(folder, "wyniki-posterunki24.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
			return true;
		}
		catch (Exception ex2)
		{
			log.Add(ex2.ToString());
			File.WriteAllLines(Path.Combine(folder, "wyniki-posterunki24.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
			return false;
		}
	}
}

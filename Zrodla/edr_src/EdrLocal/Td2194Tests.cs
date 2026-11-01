using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace EdrLocal;

public static class Td2194Tests
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

	private static T Field<T>(object obj, string name)
	{
		return (T)obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
	}

	private static void Pump()
	{
		for (int i = 0; i < 4; i++)
		{
			Application.DoEvents();
		}
	}

	private static Td2Schedule Schedule(int id, bool confirmed, bool arrival)
	{
		List<Td2Stop> list = new List<Td2Stop>();
		for (int i = 0; i < 3; i++)
		{
			string text = new DateTime(2026, 10, 24, 7, 0, 0).AddMinutes(i * 5).ToString("yyyy-MM-ddTHH:mm:ssZ");
			list.Add(new Td2Stop
			{
				pointId = (101 + i).ToString(),
				entryId = id * 10 + i,
				pointNameRAW = (new string[3] { "Początkowa", "Pośrednia", "Końcowa" })[i],
				arrivalLine = ((i == 0) ? null : ("S" + i)),
				departureLine = ((i == 2) ? null : ("S" + (i + 1))),
				arrivalTime = ((i == 0) ? null : text),
				departureTime = ((i == 2) ? null : text),
				arrivalRealTime = i switch
				{
					0 => null, 
					2 => arrival ? text : null, 
					_ => text, 
				},
				departureRealTime = ((i == 2) ? null : text),
				confirmed = i switch
				{
					1 => 0, 
					2 => confirmed ? 1 : 0, 
					_ => 1, 
				},
				comments = ((i == 1) ? "I,1" : ""),
				pointStopTime = 0
			});
		}
		Td2Schedule td2Schedule = new Td2Schedule();
		td2Schedule.trainInfo = new Td2TrainInfo
		{
			timetableId = id,
			trainNo = 10000 + id * 2,
			trainCategoryCode = "ROJ",
			region = "eu"
		};
		td2Schedule.timetablePath = ",Sceneria aaaaaaaa.sc,";
		td2Schedule.stopPoints = list;
		return td2Schedule;
	}

	private static SimRailSnapshot Accept(Td2Provider provider, params Td2Schedule[] schedules)
	{
		return provider.Accept(Json.Bytes(new Td2Envelope
		{
			success = true,
			respCode = 10,
			message = schedules.ToList()
		}), "eu", DateTimeOffset.UtcNow);
	}

	private static Engine Create(string folder, Td2Provider provider, SimRailSnapshot snapshot, string point)
	{
		SimRailSession simRailSession = new SimRailSession(provider, snapshot, point);
		Station station = simRailSession.SuggestStation();
		station.Tracks = new List<string> { "1" };
		StationSettings.Normalize(station);
		station.SimulatorReviewed = true;
		Store store = new Store(Path.Combine(folder, Guid.NewGuid().ToString("N")), transient: true);
		store.Load();
		store.Commit((AppState s) =>
		{
			s.Stations = new List<Station> { station };
			s.Trains.Clear();
			s.Entries.Clear();
			s.Duties.Clear();
			s.History.Clear();
		});
		Engine engine = new Engine(store);
		engine.SimRail = simRailSession;
		engine.User = store.State.Users.First((LocalUser x) => x.Id == "DEMO");
		Engine engine2 = engine;
		engine2.ApplySimRailSnapshot(snapshot);
		engine2.BeginDuty(engine2.Now.AddHours(-1.0));
		return engine2;
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		bool enabled = LocalPersistence.Enabled;
		LocalPersistence.Enabled = false;
		try
		{
			Td2Provider td2Provider = new Td2Provider();
			Td2Schedule td2Schedule = Schedule(1, confirmed: true, arrival: true);
			Td2Schedule td2Schedule2 = Schedule(2, confirmed: false, arrival: true);
			Td2Schedule td2Schedule3 = Schedule(3, confirmed: true, arrival: false);
			SimRailSnapshot simRailSnapshot = Accept(td2Provider, td2Schedule, td2Schedule2, td2Schedule3);
			Check(simRailSnapshot.Td2.Find("1").Completed && !simRailSnapshot.Td2.Find("2").Completed && !simRailSnapshot.Td2.Find("3").Completed, "Zaliczenie wymaga zatwierdzenia oraz rzeczywistego przyjazdu na końcu rozkładu.");
			Engine engine = Create(folder, td2Provider, simRailSnapshot, "102");
			Train first = engine.State.Trains.Single((Train x) => x.ExternalTrainNo == "1");
			Check(!first.ExternalConfirmed && simRailSnapshot.Td2.Find("1").Completed, "Zaliczenie końca rozkładu jest niezależne od potwierdzenia wyświetlanej wcześniejszej stacji.");
			JournalEntry entry = engine.RegisterScheduledTrain(first.Id, null);
			simRailSnapshot = Accept(td2Provider);
			engine.ApplySimRailSnapshot(simRailSnapshot);
			first = engine.State.Trains.Single((Train x) => x.ExternalTrainNo == "1");
			Check(first.Runs && first.RemovedFromSource && simRailSnapshot.Td2.Find("1").Removed, "Usunięty, zaliczony rozkład zachowuje K, a pamięć usunięcia z API pozostaje odrębna.");
			Check(engine.State.Trains.Where((Train x) => x.ExternalTrainNo != "1").All((Train x) => !x.Runs && x.RemovedFromSource), "Usunięte rozkłady bez zatwierdzonego przyjazdu końcowego nadal dostają NK.");
			Check(engine.State.Entries.Any((JournalEntry x) => x.Id == entry.Id) && engine.SimRail.Route(first).Count() == 3, "Zachowane K nie usuwa wpisów ani trasy rozkładu.");
			engine.ApplySimRailSnapshot(Accept(td2Provider));
			Check(engine.State.Trains.Single((Train x) => x.ExternalTrainNo == "1").Runs, "K pozostaje po kolejnym odświeżeniu, również przy scalaniu z lokalnym stanem.");
			string[] array = new string[2] { "101", "103" };
			foreach (string text in array)
			{
				SimRailSession simRailSession = new SimRailSession(td2Provider, engine.SimRail.Snapshot, text);
				Check(simRailSession.Project(simRailSession.SuggestStation()).Single((Train x) => x.ExternalTrainNo == "1").Runs, "Ten sam zaliczony rozkład zachowuje K przy posterunku " + text + ".");
			}
			Td2Schedule td2Schedule4 = Schedule(1, confirmed: false, arrival: false);
			Td2Schedule td2Schedule5 = Schedule(2, confirmed: true, arrival: true);
			engine.ApplySimRailSnapshot(Accept(td2Provider, td2Schedule4, td2Schedule5));
			Check(engine.State.Trains.Single((Train x) => x.ExternalTrainNo == "1").Runs && engine.SimRail.Snapshot.Td2.Find("1").Completed, "Ponowne pojawienie tego samego usuniętego ID nie nadpisuje zachowanego zaliczenia.");
			Check(!engine.State.Trains.Single((Train x) => x.ExternalTrainNo == "2").Runs && !engine.SimRail.Snapshot.Td2.Find("2").Completed, "Nieukończony rozkład usunięty wcześniej nie dostaje wstecznego zaliczenia z późniejszej wersji tego samego ID.");
			Td2Schedule td2Schedule6 = Schedule(4, confirmed: false, arrival: false);
			td2Schedule6.trainInfo.trainNo = td2Schedule.trainInfo.trainNo;
			engine.ApplySimRailSnapshot(Accept(td2Provider, td2Schedule6));
			Check(Enumerable.Count(engine.State.Trains, (Train x) => x.Number == first.Number) == 2 && engine.State.Trains.Where((Train x) => x.Number == first.Number).All((Train x) => x.Runs), "Nowy identyfikator z tym samym numerem jest osobny; poprzedni zaliczony nadal ma K.");
			using (MainForm mainForm = new MainForm(engine))
			{
				mainForm.Show();
				Pump();
				mainForm.GetType().GetField("useTimeRange", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(mainForm, false);
				mainForm.RefreshData();
				Pump();
				DataGridView dataGridView = Field<DataGridView>(mainForm, "trains");
				DataGridViewRow dataGridViewRow = dataGridView.Rows.Cast<DataGridViewRow>().Single((DataGridViewRow x) => (string)x.Tag == first.Id);
				DataGridViewRow dataGridViewRow2 = dataGridView.Rows.Cast<DataGridViewRow>().Single((DataGridViewRow x) => engine.State.Trains.Single((Train t) => t.Id == (string)x.Tag).ExternalTrainNo == "2");
				Check((bool)dataGridViewRow.Cells["k"].Value && !(bool)dataGridViewRow.Cells["nk"].Value && !(bool)dataGridViewRow2.Cells["k"].Value && (bool)dataGridViewRow2.Cells["nk"].Value, "Rzeczywisty wykaz pokazuje K dla zaliczonego i NK dla nieukończonego rozkładu.");
				Field<CheckBox>(mainForm, "runningOnly").Checked = true;
				mainForm.RefreshData();
				Check(dataGridView.Rows.Cast<DataGridViewRow>().Any((DataGridViewRow x) => (string)x.Tag == first.Id) && dataGridView.Rows.Count == 2, "Filtr kursujących zachowuje zaliczony rozkład po usunięciu z API.");
				Ui.Capture(mainForm, Path.Combine(folder, "td2-zaliczone-k.png"));
				mainForm.Close();
				Pump();
			}
			Td2Schedule td2Schedule7 = Schedule(10, confirmed: true, arrival: true);
			Td2Stop td2Stop = Json.Clone(td2Schedule7.stopPoints.Last());
			td2Stop.pointId = "999";
			td2Stop.entryId = 999;
			td2Stop.pointNameRAW = "SBL_1, pe";
			td2Stop.confirmed = 0;
			td2Stop.arrivalRealTime = null;
			td2Schedule7.stopPoints.Add(td2Stop);
			Check(Accept(new Td2Provider(), td2Schedule7).Td2.Find("10").Completed, "Techniczny SBL za stacją końcową nie unieważnia jej zatwierdzonego przyjazdu.");
			Td2Schedule td2Schedule8 = Schedule(11, confirmed: true, arrival: true);
			td2Schedule8.stopPoints.Last().pointNameRAW = "Końcowa, po";
			Check(Accept(new Td2Provider(), td2Schedule8).Td2.Find("11").Completed, "Końcowy przystanek osobowy również może potwierdzić zakończenie rzeczywistego rozkładu.");
			Td2Provider provider = new Td2Provider();
			Engine engine2 = Create(folder, provider, Accept(provider, Schedule(20, confirmed: false, arrival: false)), "103");
			Train train = engine2.State.Trains.Single();
			JournalEntry journalEntry = engine2.Note(engine2.Station.Sections.First().Id, engine2.Now, "", "Pociąg");
			journalEntry.Direction = "Z";
			journalEntry.TrainId = train.Id;
			journalEntry.Number = train.Number;
			journalEntry.Track = "1";
			journalEntry.Arrived = engine2.Now.AddMinutes(-1.0);
			engine2.SaveJournal(journalEntry);
			engine2.ApplySimRailSnapshot(Accept(provider));
			Check(!engine2.State.Trains.Single().Runs && !engine2.SimRail.Snapshot.Td2.Find("20").Completed, "Sam lokalny przyjazd EDR bez zatwierdzenia terminala w API nie zmienia usuniętego rozkładu na K.");
			SimRailSnapshot simRailSnapshot2 = Accept(new Td2Provider(), Schedule(30, confirmed: false, arrival: false));
			Check(simRailSnapshot2.Td2.Find("30").Runs && !simRailSnapshot2.Td2.Find("30").Completed, "Aktywny, niezakończony rozkład pozostaje K.");
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			File.WriteAllLines(Path.Combine(folder, "wyniki-td2194.txt"), log, Encoding.UTF8);
			return false;
		}
		finally
		{
			LocalPersistence.Enabled = enabled;
		}
		File.WriteAllLines(Path.Combine(folder, "wyniki-td2194.txt"), log, Encoding.UTF8);
		return true;
	}
}

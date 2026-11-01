using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace EdrLocal;

public static class Confirmation20Tests
{
	private static readonly List<string> log = new List<string>();

	private static void Check(bool value, string label)
	{
		if (!value)
		{
			throw new Exception("FAIL: " + label);
		}
		log.Add("PASS: " + label);
	}

	private static DataGridViewCellStyle Kind(Engine engine, string id, bool capture, string folder)
	{
		using MainForm mainForm = new MainForm(engine);
		mainForm.Show();
		Application.DoEvents();
		mainForm.SelectTrain(id);
		mainForm.RefreshData();
		DataGridView dataGridView = (DataGridView)typeof(MainForm).GetField("trains", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mainForm);
		DataGridViewRow dataGridViewRow = dataGridView.Rows.Cast<DataGridViewRow>().First((DataGridViewRow x) => (string)x.Tag == id);
		DataGridViewCellStyle result = dataGridViewRow.Cells["kind"].Style.Clone();
		if (capture)
		{
			Ui.Capture(mainForm, Path.Combine(folder, "kolor.png"));
		}
		mainForm.DialogResult = DialogResult.Retry;
		mainForm.Close();
		return result;
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		try
		{
			DateTime value = new DateTime(2026, 4, 21);
			SimRailPoint simRailPoint = new SimRailPoint();
			simRailPoint.pointId = "60";
			simRailPoint.nameForPerson = "Baby";
			simRailPoint.indexOfPoint = 1;
			simRailPoint.line = 1;
			simRailPoint.mileage = 129.944;
			simRailPoint.track = 1;
			simRailPoint.displayedTrainNumber = "124";
			simRailPoint.trainType = "MPE";
			simRailPoint.isActive = true;
			simRailPoint.isStoped = true;
			simRailPoint.arrivalTime = "2026-04-21 19:24:00";
			simRailPoint.departureTime = "2026-04-21 19:26:00";
			simRailPoint.actualArrivalTime = "2026-04-21 19:24:00";
			simRailPoint.actualDepartureTime = "2026-04-21 19:26:00";
			SimRailPoint simRailPoint2 = simRailPoint;
			SimRailTrain simRailTrain = new SimRailTrain();
			simRailTrain.trainNoLocal = "124";
			simRailTrain.startStation = "Rokiciny";
			simRailTrain.endStation = "Piotrków Trybunalski";
			simRailTrain.timetable = new List<SimRailPoint>
			{
				new SimRailPoint
				{
					pointId = "59",
					nameForPerson = "Rokiciny",
					indexOfPoint = 0,
					line = 1,
					mileage = 114.176
				},
				simRailPoint2,
				new SimRailPoint
				{
					pointId = "61",
					nameForPerson = "Piotrków Trybunalski",
					indexOfPoint = 2,
					line = 1,
					mileage = 144.242
				}
			};
			SimRailTrain item = simRailTrain;
			SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
			simRailSnapshot.ServerCode = "test";
			simRailSnapshot.Trains = new List<SimRailTrain> { item };
			simRailSnapshot.DispatcherNames = new HashSet<string> { "Baby", "Rokiciny", "Piotrków Trybunalski" };
			simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
			simRailSnapshot.GameTime = value.AddHours(19.0).AddMinutes(40.0);
			SimRailSnapshot snapshot = simRailSnapshot;
			SimRailSession simRailSession = new SimRailSession(new SimRailProvider(), snapshot, "60");
			Station station = simRailSession.SuggestStation();
			StationSettings.Normalize(station);
			Store store = new Store(Path.Combine(folder, "data"));
			store.Load();
			store.Commit((AppState s) =>
			{
				s.Stations = new List<Station> { station };
				s.Trains.Clear();
				s.Entries.Clear();
				s.Duties.Clear();
			});
			Engine engine = new Engine(store);
			engine.SimRail = simRailSession;
			engine.User = store.State.Users.First((LocalUser x) => x.Id == "DEMO");
			Engine engine2 = engine;
			engine2.ApplySimRailSnapshot(snapshot);
			engine2.BeginDuty(engine2.Now.AddMinutes(-2.0));
			Train t = engine2.State.Trains.Single();
			string id = t.Id;
			Check(!t.ExternalConfirmed && !TimetableConfirmation.IsConfirmed(t), "Prognoza API nie stanowi potwierdzenia kategorii.");
			DataGridViewCellStyle dataGridViewCellStyle = Kind(engine2, id, capture: false, folder);
			Check(dataGridViewCellStyle.BackColor == Ui.Cream, "Początkowy kolor kategorii jest niepotwierdzony.");
			int writes = 0;
			engine2.SimRailTimeChanged += (SepeWriteRequest r) =>
			{
				writes++;
			};
			JournalEntry journalEntry = engine2.RegisterScheduledTrain(id, null);
			journalEntry.Arrived = value.AddHours(19.0).AddMinutes(25.0);
			engine2.SaveJournal(journalEntry);
			t = engine2.State.Trains.Single();
			Check(writes == 1 && t.ExternalManualArrival, "Lokalna godzina uruchamia wysyłkę i pozostaje oczekująca.");
			Check(!TimetableConfirmation.IsConfirmed(t), "Lokalny wpis nie trafia do filtra potwierdzonych.");
			Check(TrainPrediction.Hint(t, arrival: true).Contains("oczekuje"), "Opis godziny nie ogłasza nieotrzymanego potwierdzenia.");
			DataGridViewCellStyle dataGridViewCellStyle2 = Kind(engine2, id, capture: false, folder);
			Check(dataGridViewCellStyle2.BackColor == dataGridViewCellStyle.BackColor && dataGridViewCellStyle2.SelectionBackColor == dataGridViewCellStyle.SelectionBackColor, "Kolor kategorii i zaznaczenia nie zmienia się po lokalnym wpisie.");
			JournalEntry journalEntry2 = engine2.Note(station.Sections.First((Section x) => x.Name == t.To).Id, engine2.Now, "", "Pociąg");
			journalEntry2.Direction = "DO";
			journalEntry2.Number = "124";
			journalEntry2.TrainId = id;
			journalEntry2.Track = "1";
			journalEntry2.Left = value.AddHours(19.0).AddMinutes(30.0);
			engine2.SaveJournal(journalEntry2);
			Check(engine2.State.Trains.Single().Stopped, "Lokalny odjazd nie usuwa koloru STÓJ pochodzącego z SEPE.");
			simRailPoint2.confirmedBy = 1;
			engine2.ApplySimRailSnapshot(snapshot);
			Check(!engine2.State.Trains.Single().ExternalConfirmed, "Samo confirmedBy bez isConfirmed nie potwierdza kategorii.");
			DataGridViewCellStyle dataGridViewCellStyle3 = Kind(engine2, id, capture: false, folder);
			Check(dataGridViewCellStyle3.BackColor == Ui.Cream, "Niepotwierdzone pobranie API nie zmienia koloru.");
			simRailPoint2.isConfirmed = true;
			simRailPoint2.actualArrivalTime = "2026-04-21 19:25:00";
			simRailPoint2.actualDepartureTime = "2026-04-21 19:30:00";
			simRailPoint2.isStoped = false;
			engine2.ApplySimRailSnapshot(snapshot);
			t = engine2.State.Trains.Single();
			Check(t.ExternalConfirmed && !t.ExternalManualTimes, "Zgodne potwierdzenie API kończy oczekiwanie.");
			Check(!t.Stopped, "STÓJ znika dopiero po aktualizacji z SEPE.");
			DataGridViewCellStyle dataGridViewCellStyle4 = Kind(engine2, id, capture: true, folder);
			Check(dataGridViewCellStyle4.BackColor == Color.Green && dataGridViewCellStyle4.SelectionBackColor == Color.Green, "Potwierdzenie gracza w SEPE zmienia kolor na zielony.");
			simRailPoint2.confirmedBy = 3;
			engine2.ApplySimRailSnapshot(snapshot);
			journalEntry.Arrived = value.AddHours(19.0).AddMinutes(26.0);
			engine2.SaveJournal(journalEntry);
			DataGridViewCellStyle dataGridViewCellStyle5 = Kind(engine2, id, capture: false, folder);
			Check(dataGridViewCellStyle5.BackColor == Color.Maroon && dataGridViewCellStyle5.SelectionBackColor == Color.Maroon, "Korekta lokalna zachowuje poprzedni kolor potwierdzenia automatycznego.");
			simRailPoint2.isConfirmed = false;
			engine2.ApplySimRailSnapshot(snapshot);
			Check(!TimetableConfirmation.IsConfirmed(engine2.State.Trains.Single()), "Wycofanie potwierdzenia w API jest nadrzędne wobec lokalnych godzin.");
			Check(!Json.Clone(engine2.State.Trains.Single()).ExternalConfirmed, "Flaga potwierdzenia jest zachowana w kopii danych.");
			Train train = new Train();
			train.Arrival = value;
			train.SourceProvider = null;
			Train train2 = train;
			Check(TimetableConfirmation.IsConfirmed(train2), "Tryb ćwiczeń lokalnych zachowuje dotychczasową obsługę godzin.");
			engine2.EndDuty(engine2.Now);
			log.Add("ALL SEPE CONFIRMATION COLOR TESTS PASSED");
			return true;
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			return false;
		}
		finally
		{
			File.WriteAllLines(Path.Combine(folder, "wyniki-kolory-sepe.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

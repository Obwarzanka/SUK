using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace EdrLocal;

public static class SimRailTests
{
	private static readonly List<string> log = new List<string>();

	private static void Check(bool ok, string message)
	{
		if (!ok)
		{
			throw new Exception("FAIL: " + message);
		}
		log.Add("PASS: " + message);
	}

	private static void Reject(Action action, string message)
	{
		bool ok = false;
		try
		{
			action();
		}
		catch
		{
			ok = true;
		}
		Check(ok, message);
	}

	public static bool Run(string folder, string fixture, bool live)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		try
		{
			byte[] bytes = File.ReadAllBytes(fixture);
			List<SimRailTrain> list = SimRailProvider.Parse<List<SimRailTrain>>(bytes);
			Check(list.Count > 0 && list.All((SimRailTrain x) => x.timetable != null), "Deserializacja pełnego zrzutu EDR przez .NET Framework.");
			SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
			simRailSnapshot.ServerCode = "int5";
			simRailSnapshot.Trains = list;
			simRailSnapshot.GameTime = new DateTime(2026, 10, 18, 13, 0, 0);
			simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
			simRailSnapshot.DispatcherNames = new HashSet<string> { "Baby", "Piotrków Trybunalski", "Rokiciny" };
			SimRailSnapshot simRailSnapshot2 = simRailSnapshot;
			Check(simRailSnapshot2.Locations().Any((SimRailLocation x) => x.Id == "60"), "Punkt Baby 60 pochodzi z API.");
			SimRailSession session = new SimRailSession(new SimRailProvider(), simRailSnapshot2, "60");
			Station station = session.SuggestStation();
			StationSettings.Normalize(station);
			Check(station.Name == "Baby" && station.Sections.Any((Section x) => x.Name == "Rokiciny"), "Sąsiad i nazwa posterunku pochodzą z tras pociągów.");
			Check(station.Sections.All((Section x) => x.Tracks == 1), "Nieznana liczba torów wymaga przeglądu konfiguracji.");
			List<Train> list2 = session.Project(station);
			Check(list2.Count > 0 && list2.Select((Train x) => x.Id).Distinct().Count() == list2.Count, "Stabilne unikalne identyfikatory dla przejść przez posterunek.");
			Check(list2.All((Train x) => x.SourceProvider == "SimRail"), "Rozkład zachowuje identyfikator źródła.");
			Check(session.Route(list2[0]).Count() == simRailSnapshot2.Find(list2[0].ExternalTrainNo).timetable.Count, "Trasa zawiera wszystkie punkty źródłowego rozkładu.");
			Check(session.Now >= simRailSnapshot2.GameTime && session.Now < simRailSnapshot2.GameTime.AddMinutes(1.0), "Zegar serwera bez podwójnego doliczania strefy.");
			SimRailTrain simRailTrain = new SimRailTrain();
			simRailTrain.trainNoLocal = "123";
			simRailTrain.trainName = "Test";
			simRailTrain.startStation = "A";
			simRailTrain.endStation = "B";
			simRailTrain.timetable = new List<SimRailPoint>
			{
				new SimRailPoint
				{
					indexOfPoint = 1,
					pointId = "60",
					nameForPerson = "Baby",
					displayedTrainNumber = "123",
					line = 1,
					track = 2,
					platform = "2",
					arrivalTime = "2026-10-18 23:58:00",
					departureTime = "2026-10-19 00:02:00",
					actualArrivalTime = "2026-10-19 00:03:00",
					actualDepartureTime = "2026-10-19 00:07:00",
					isActive = true,
					stopTypeNumber = 2,
					plannedStop = 0.5
				}
			};
			SimRailTrain simRailTrain2 = simRailTrain;
			SimRailSnapshot simRailSnapshot3 = new SimRailSnapshot();
			simRailSnapshot3.ServerCode = "int5";
			simRailSnapshot3.Trains = new List<SimRailTrain> { simRailTrain2 };
			simRailSnapshot3.DispatcherNames = simRailSnapshot2.DispatcherNames;
			simRailSnapshot3.GameTime = new DateTime(2026, 10, 18, 23, 56, 0);
			SimRailSnapshot simRailSnapshot4 = simRailSnapshot3;
			SimRailSession simRailSession = new SimRailSession(new SimRailProvider(), simRailSnapshot4, "60");
			Train train = simRailSession.Project(station).Single();
			Check(train.StopType == "pt" && train.ExternalPlannedStop == 0.5, "Postój techniczny i ułamkowa minuta pochodzą z API.");
			Check(!train.Arrival.HasValue && !train.Departure.HasValue && train.Forecast.Value.Day == 19, "Prognoza przez północ nie potwierdza przejazdu.");
			simRailTrain2.timetable[0].isConfirmed = true;
			simRailTrain2.timetable[0].confirmedBy = 3;
			train = simRailSession.Project(station).Single();
			Check(train.Arrival.HasValue && train.ExternalConfirmedBy == 3, "Potwierdzenie automatyczne pozostaje odrębne od prognozy.");
			session.SetAccessLink("https://api.simrail.eu:8093/?serverCode=int5&stationId=60&key=test-only");
			Check(session.HasWriteLink, "Przyjęcie bieżącego linku zgodnego z serwerem i punktem.");
			Reject(() =>
			{
				session.SetAccessLink("https://other.example:8093/?serverCode=int5&stationId=60&key=x");
			}, "Odrzucenie obcego hosta.");
			Reject(() =>
			{
				session.SetAccessLink("https://api.simrail.eu:8093/?serverCode=pl1&stationId=60&key=x");
			}, "Odrzucenie linku do innego serwera.");
			Reject(() =>
			{
				session.SetAccessLink("https://api.simrail.eu:8093/?serverCode=int5&stationId=61&key=x");
			}, "Odrzucenie innego posterunku.");
			Reject(() =>
			{
				session.SetAccessLink("https://api.simrail.eu:8093/?serverCode=int5&stationId=60");
			}, "Odrzucenie linku bez klucza zapisu.");
			Check(!session.PublicUrl.AbsoluteUri.Contains("key"), "Publiczny adres nie zawiera klucza.");
			Reject(() =>
			{
				session.Accept(new SimRailSnapshot
				{
					ServerCode = "pl1"
				});
			}, "Odświeżanie nie zmienia samoczynnie serwera.");
			Store store = new Store(Path.Combine(folder, "database"));
			store.Load();
			store.Commit((AppState s) =>
			{
				s.Stations = new List<Station> { station };
				s.Trains.Clear();
				s.Entries.Clear();
				s.Duties.Clear();
			});
			Engine engine = new Engine(store)
			{
				SimRail = simRailSession,
				User = store.State.Users.First((LocalUser x) => x.Id == "DEMO")
			};
			engine.ApplySimRailSnapshot(simRailSnapshot4);
			engine.BeginDuty(engine.Now.AddMinutes(-5.0));
			JournalEntry en = engine.Note(station.Sections[0].Id, engine.Now, "Ręczny wpis", "Wpis");
			engine.SaveJournal(en);
			string id = engine.State.Trains[0].Id;
			simRailSnapshot4.GameTime = new DateTime(2026, 10, 19, 0, 10, 0);
			simRailSession.Accept(simRailSnapshot4);
			engine.SetTrainTimes(id, new DateTime(2026, 10, 19, 0, 4, 0), new DateTime(2026, 10, 19, 0, 8, 0));
			engine.Station.Sections[0].JournalName = "Własny dziennik";
			engine.ApplySimRailSnapshot(simRailSnapshot4);
			Check(engine.State.Entries.Any((JournalEntry x) => x.Id == en.Id) && engine.Station.Sections[0].JournalName == "Własny dziennik", "Odświeżanie zachowuje wpisy i konfigurację.");
			Check(engine.State.Trains.Single().ExternalManualTimes && engine.State.Trains.Single().Departure.Value.Minute == 8, "Odświeżanie zachowuje lokalne godziny różne od SEPE.");
			simRailTrain2.timetable[0].actualArrivalTime = "2026-10-19 00:04:00";
			simRailTrain2.timetable[0].actualDepartureTime = "2026-10-19 00:08:00";
			engine.ApplySimRailSnapshot(simRailSnapshot4);
			Check(!engine.State.Trains.Single().ExternalManualTimes, "Zgodne potwierdzenie API kończy lokalne nadpisanie godzin.");
			JournalEntry journalEntry = engine.Note(station.Sections[0].Id, engine.Now, "Szkic", "Pociąg");
			journalEntry.Direction = "Z";
			journalEntry.Number = "123";
			journalEntry.TrainId = id;
			journalEntry.Track = engine.State.Trains.Single().Track;
			engine.SaveJournal(journalEntry);
			journalEntry.Text = "Uwagi zmienione bez godziny";
			engine.SaveJournal(journalEntry);
			Check(engine.State.Trains.Single().Arrival.HasValue && !engine.State.Trains.Single().ExternalManualTimes, "Edycja uwag pustego wpisu nie kasuje potwierdzenia API.");
			engine.Cancel(journalEntry.Id, "Test pustego wpisu");
			Check(engine.State.Trains.Single().Arrival.HasValue, "Wykreślenie pustego wpisu nie kasuje potwierdzenia API.");
			Check(!File.ReadAllText(store.FilePath).Contains("test-only"), "Klucz SEPE nie jest zapisywany w bazie ani audycie.");
			Check(Enumerable.Count(engine.State.Entries, (JournalEntry x) => x.Type == "Pociąg" && !x.Cancelled) == 0, "Import czasu nie dopisuje fikcyjnego wpisu jazdy.");
			Station copy = Json.Clone(engine.Station);
			copy.TrackSettings[0].Visible = false;
			if (copy.TrackSettings.All((StationTrackSetting x) => !x.Visible))
			{
				copy.TrackSettings.Add(new StationTrackSetting
				{
					Number = "99",
					Visible = true
				});
			}
			StationSettings.Validate(copy, engine.State);
			Check(!StationSettings.VisibleTracks(copy).Contains(copy.TrackSettings[0].Number), "Ukrycie toru bez usuwania go z danych.");
			Section section = Json.Clone(copy.Sections[0]);
			section.Id = "other-line";
			section.Line = "999";
			copy.Sections.Add(section);
			StationSettings.Validate(copy, engine.State);
			Check(ok: true, "Ten sam sąsiad na różnych liniach jest dozwolony.");
			copy.JournalTabs.Add(new JournalTabDefinition
			{
				LeftId = copy.Sections[0].Id,
				RightId = copy.Sections[1].Id
			});
			StationSettings.Validate(copy, engine.State);
			copy.JournalTabs.Add(new JournalTabDefinition
			{
				LeftId = copy.Sections[0].Id
			});
			Reject(() =>
			{
				StationSettings.Validate(copy, engine.State);
			}, "Dziennik nie może występować w dwóch zakładkach.");
			copy.JournalTabs.RemoveAt(1);
			copy.RequiredChecks.Add(new RequiredCheckSetting
			{
				Name = "Test",
				Hours = "25:00",
				Enabled = true
			});
			Reject(() =>
			{
				StationSettings.Validate(copy, engine.State);
			}, "Odrzucenie błędnej godziny sprawdzenia.");
			RequiredCheckSetting requiredCheckSetting = engine.Station.RequiredChecks.First();
			requiredCheckSetting.Hours = "00:00";
			requiredCheckSetting.Enabled = true;
			Check(engine.DueChecks().Any(), "Godzina sprawdzenia powoduje oczekiwanie w harmonogramie.");
			JournalEntry journalEntry2 = engine.Note("TEL", engine.Now, "Sprawdzenie", "Sprawdzenie");
			journalEntry2.CheckName = requiredCheckSetting.Name;
			engine.SaveJournal(journalEntry2);
			Check(engine.DueChecks().Count == 0, "Wykonane sprawdzenie zamyka oczekujący termin.");
			using (StationConfigurationForm stationConfigurationForm = new StationConfigurationForm(engine))
			{
				stationConfigurationForm.Show();
				Application.DoEvents();
				Ui.Capture(stationConfigurationForm, Path.Combine(folder, "konfiguracja.png"));
				Check(stationConfigurationForm.Controls.OfType<TabControl>().Single().TabCount == 5, "Pięć kart konfiguracji posterunku.");
				typeof(StationConfigurationForm).GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(stationConfigurationForm, null);
				Check(stationConfigurationForm.DialogResult == DialogResult.OK, "Zapis konfiguracji przez formularz.");
			}
			using (MainForm mainForm = new MainForm(engine))
			{
				mainForm.Show();
				Application.DoEvents();
				mainForm.SelectTrain(id);
				mainForm.RefreshData();
				Ui.Capture(mainForm, Path.Combine(folder, "simrail.png"));
				Check(mainForm.Text.Contains(typeof(MainForm).Assembly.GetName().Version.ToString(2)), "Widok SimRail uruchamia się z importowanym rozkładem.");
				engine.EndDuty(engine.Now);
				mainForm.Close();
			}
			File.WriteAllText(Path.Combine(folder, "transfer.js"), SepeTimeTransfer.Script(new DateTime(2026, 10, 18, 23, 58, 0), new DateTime(2026, 10, 19, 0, 3, 0)));
			Check(!SepeBrowserForm.SelectionScript("123").Contains(".submit("), "Automatyzacja wyboru pociągu nie zatwierdza godzin.");
			if (live)
			{
				SimRailProvider simRailProvider = new SimRailProvider();
				List<SimRailServer> result = simRailProvider.ServersAsync(CancellationToken.None).GetAwaiter().GetResult();
				Check(result.Count > 0, "Odczyt rzeczywistej listy serwerów HTTPS.");
				SimRailSnapshot result2 = simRailProvider.SnapshotAsync("int5", CancellationToken.None).GetAwaiter().GetResult();
				Check(result2.Trains.Count > 0 && result2.Locations().Any((SimRailLocation x) => x.Id == "60"), "Odczyt bieżącego rozkładu, posterunków i zegara int5.");
				log.Add("LIVE: " + result2.Trains.Count + " rozkładów; zegar " + result2.GameTime.ToString("s"));
			}
			log.Add("ALL SIMRAIL TESTS PASSED");
			return true;
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			return false;
		}
		finally
		{
			File.WriteAllLines(Path.Combine(folder, "wyniki-simrail.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

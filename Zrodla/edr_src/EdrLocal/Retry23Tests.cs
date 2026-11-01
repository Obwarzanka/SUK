using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EdrLocal;

public static class Retry23Tests
{
	private sealed class Feed : ISimulatorFeed
	{
		public int Reads;

		public int Fail;

		public SimRailSnapshot Value;

		public string ProviderId => "test";

		public Task<List<SimRailServer>> ServersAsync(CancellationToken token)
		{
			return Task.FromResult(new List<SimRailServer>());
		}

		public Task<SimRailSnapshot> SnapshotAsync(string server, CancellationToken token)
		{
			Reads++;
			if (Reads <= Fail)
			{
				throw new IOException("Przejściowy błąd odczytu");
			}
			return Task.FromResult(Value);
		}
	}

	private static readonly List<string> log = new List<string>();

	private static void Check(bool ok, string message)
	{
		if (!ok)
		{
			throw new Exception("FAIL: " + message);
		}
		log.Add("PASS: " + message);
	}

	private static Exception Error(Action action)
	{
		try
		{
			action();
		}
		catch (Exception result)
		{
			return result;
		}
		throw new Exception("Brak oczekiwanego błędu.");
	}

	private static T Field<T>(object o, string name)
	{
		return (T)o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		try
		{
			int attempts = 0;
			int progress = 0;
			SepeRetry.Run((int n) =>
			{
				attempts++;
				if (n < 3)
				{
					throw new IOException("Nie wyświetlono godzin");
				}
				return Task.FromResult(0);
			}, () => true, (int n) =>
			{
				progress = n;
			}, CancellationToken.None, 0).GetAwaiter().GetResult();
			Check(attempts == 3 && progress == 3, "Przejściowe dwa błędy kończą się sukcesem trzeciej próby bez końcowego błędu.");
			attempts = 0;
			Exception ex = Error(() =>
			{
				SepeRetry.Run((int n) =>
				{
					attempts++;
					throw new IOException("Nie wyświetlono godzin");
				}, () => true, null, CancellationToken.None, 0).GetAwaiter().GetResult();
			});
			Check(attempts == 3 && ex.Message.Contains("po 3 próbach") && ex.Message.Contains("Próba 1:") && ex.Message.Contains("Próba 3:"), "Dopiero trzy nieudane przygotowania zwracają raport ze wszystkimi przyczynami.");
			attempts = 0;
			Error(() =>
			{
				SepeRetry.Run((int n) =>
				{
					attempts++;
					throw new TrainTimeException("Nieprawidłowe godziny");
				}, () => true, null, CancellationToken.None, 0).GetAwaiter().GetResult();
			});
			Check(attempts == 1, "Znany błąd walidacji czasu nie jest ponownie wysyłany trzy razy.");
			attempts = 0;
			Error(() =>
			{
				SepeRetry.Run((int n) =>
				{
					attempts++;
					throw new IOException("Niepewny wynik po Zapisz");
				}, () => false, null, CancellationToken.None, 0).GetAwaiter().GetResult();
			});
			Check(attempts == 1, "Możliwy zapis nie uruchamia ponownego wysyłania.");
			CancellationTokenSource cancel = new CancellationTokenSource();
			cancel.Cancel();
			attempts = 0;
			Check(Error(() =>
			{
				SepeRetry.Run((int n) =>
				{
					attempts++;
					return Task.FromResult(0);
				}, () => true, null, cancel.Token, 0).GetAwaiter().GetResult();
			}) is OperationCanceledException && attempts == 0, "Zamknięcie sesji przerywa ponawianie przed następną operacją.");
			DateTime day = default;
			ref DateTime reference = ref day;
			reference = new DateTime(2026, 4, 21);
			SimRailPoint simRailPoint = new SimRailPoint();
			simRailPoint.pointId = "60";
			simRailPoint.nameForPerson = "Baby";
			simRailPoint.indexOfPoint = 1;
			simRailPoint.line = 1;
			simRailPoint.mileage = 129.944;
			simRailPoint.track = 1;
			simRailPoint.displayedTrainNumber = "124";
			simRailPoint.arrivalTime = "2026-04-21 19:24:00";
			simRailPoint.departureTime = "2026-04-21 19:26:00";
			simRailPoint.actualArrivalTime = "2026-04-21 19:25:00";
			simRailPoint.isConfirmed = true;
			simRailPoint.confirmedBy = 1;
			simRailPoint.isActive = true;
			SimRailPoint simRailPoint2 = simRailPoint;
			SimRailTrain simRailTrain = new SimRailTrain();
			simRailTrain.trainNoLocal = "124";
			simRailTrain.startStation = "A";
			simRailTrain.endStation = "B";
			simRailTrain.timetable = new List<SimRailPoint> { simRailPoint2 };
			SimRailTrain item = simRailTrain;
			SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
			simRailSnapshot.ServerCode = "test";
			simRailSnapshot.Trains = new List<SimRailTrain> { item };
			simRailSnapshot.DispatcherNames = new HashSet<string> { "Baby" };
			simRailSnapshot.GameTime = day.AddHours(19.0).AddMinutes(40.0);
			simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
			SimRailSnapshot simRailSnapshot2 = simRailSnapshot;
			Feed feed = new Feed();
			feed.Value = simRailSnapshot2;
			feed.Fail = 2;
			Feed feed2 = feed;
			SimRailSession simRailSession = new SimRailSession(feed2, simRailSnapshot2, "60");
			Train train = new Train
			{
				Id = "test-train",
				StationId = "JS",
				Number = "124",
				ExternalTrainNo = "124",
				ExternalPointIndex = 1,
				SourceProvider = "SimRail",
				PlanArrival = day.AddHours(19.0).AddMinutes(24.0),
				PlanDeparture = day.AddHours(19.0).AddMinutes(26.0),
				Arrival = day.AddHours(19.0).AddMinutes(25.0),
				Track = "1"
			};
			SepeWriteRequest request = SepeWriteRequest.From(train, arrival: true, departure: false);
			int accepted = 0;
			SepeConfirmationResult result = SepeConfirmation.Read(request, simRailSession, (SimRailSnapshot s) =>
			{
				accepted++;
			}, null, CancellationToken.None, 0).GetAwaiter().GetResult();
			Check(result.Confirmed && feed2.Reads == 3 && accepted == 1, "Dwa błędy odczytu API nie blokują potwierdzenia trzecim odczytem.");
			feed2.Reads = 0;
			feed2.Fail = 0;
			accepted = 0;
			result = SepeConfirmation.Read(request, simRailSession, (SimRailSnapshot s) =>
			{
				accepted++;
				if (accepted < 3)
				{
					throw new IOException("Niepełne dane punktu");
				}
			}, null, CancellationToken.None, 0).GetAwaiter().GetResult();
			Check(result.Confirmed && feed2.Reads == 3, "Przejściowo niepełny odczyt punktu również jest ponawiany przed raportem.");
			feed2.Reads = 0;
			feed2.Fail = 3;
			result = SepeConfirmation.Read(request, simRailSession, null, null, CancellationToken.None, 0).GetAwaiter().GetResult();
			Check(!result.Confirmed && feed2.Reads == 3 && result.Reason.Contains("3 odczytach") && result.Reason.Contains("Przejściowy błąd"), "Trzy błędy API zachowują rzeczywistą przyczynę w raporcie.");
			feed2.Reads = 0;
			feed2.Fail = 0;
			simRailPoint2.actualArrivalTime = "2026-04-21 19:24:00";
			result = SepeConfirmation.Read(request, simRailSession, null, null, CancellationToken.None, 0).GetAwaiter().GetResult();
			Check(!result.Confirmed && feed2.Reads == 3, "Stara godzina API wymaga trzech odczytów i nie potwierdza nowej wartości.");
			simRailPoint2.actualArrivalTime = "2026-04-21 19:25:00";
			DateTime now = day.AddHours(7.0).AddSeconds(51.0);
			Exception ex2 = Error(() =>
			{
				TrainTimeRules.Validate(now, now.AddMinutes(-1.0), now, serverClock: true);
			});
			Check(ex2 is TrainTimeException && ex2.Message == "Godzina odjazdu nie może być wcześniejsza niż godzina przyjazdu.", "Lokalny komunikat chronologii odpowiada wzorowi SEPE.");
			ex2 = Error(() =>
			{
				TrainTimeRules.Validate(now.AddMinutes(1.0), now.AddMinutes(2.0), now, serverClock: true);
			});
			Check(ex2 is TrainTimeException && ex2.Message.Contains("późniejszej niż obecna (teraz jest 07:00:51)"), "Przyszły przyjazd pokazuje aktualny czas serwera z sekundami.");
			TrainTimeRules.Validate(day.AddHours(23.0).AddMinutes(58.0), day.AddDays(1.0).AddMinutes(3.0), day.AddDays(1.0).AddHours(1.0), serverClock: true);
			Check(ok: true, "Poprawna chronologia przez północ jest zachowana.");
			Store store = new Store(Path.Combine(folder, "data"));
			store.Load();
			store.Commit((AppState s) =>
			{
				s.Stations[0].Name = "Baby";
				s.Trains = new List<Train> { train };
				s.Entries.Clear();
				s.Duties.Clear();
			});
			Engine engine = new Engine(store)
			{
				SimRail = simRailSession,
				User = store.State.Users.First((LocalUser x) => x.Id == "DEMO")
			};
			engine.BeginDuty(engine.Now.AddMinutes(-1.0));
			int writes = 0;
			engine.SimRailTimeChanged += (SepeWriteRequest r) =>
			{
				writes++;
			};
			string text = Json.Text(engine.State);
			Error(() =>
			{
				engine.SetTrainTimes(train.Id, day.AddHours(20.0), day.AddHours(20.0).AddMinutes(1.0));
			});
			Check(writes == 0 && Json.Text(engine.State) == text, "Odrzucony przyszły przyjazd nie zmienia bazy ani nie tworzy zlecenia SEPE.");
			Error(() =>
			{
				engine.SetTrainTimes(train.Id, day.AddHours(19.0).AddMinutes(25.0), day.AddHours(19.0).AddMinutes(24.0));
			});
			Check(writes == 0 && Json.Text(engine.State) == text, "Odrzucona chronologia nie zmienia bazy ani nie uruchamia wysyłania.");
			engine.SetTrainTimes(train.Id, day.AddHours(19.0).AddMinutes(24.0), day.AddHours(19.0).AddMinutes(26.0));
			Check(writes == 1, "Poprawne godziny nadal uruchamiają pojedyncze zlecenie.");
			using (StationConfigurationForm stationConfigurationForm = new StationConfigurationForm(engine))
			{
				stationConfigurationForm.Show();
				Application.DoEvents();
				Field<ComboBox>(stationConfigurationForm, "postType").Text = "POSTERUNEK ODGAŁĘŹNY";
				typeof(StationConfigurationForm).GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(stationConfigurationForm, null);
			}
			Store store2 = new Store(store.DirectoryPath);
			store2.Load();
			Check(store2.State.Stations[0].PostType == "POSTERUNEK ODGAŁĘŹNY", "Typ posterunku wybrany w konfiguracji przetrwał ponowne wczytanie.");
			using (MainForm mainForm = new MainForm(engine))
			{
				mainForm.Show();
				Application.DoEvents();
				Check(mainForm.Text == "BABY (POSTERUNEK ODGAŁĘŹNY) - Elektroniczny Dziennik Ruchu - EDR Produkcja (wersja 1.0.10.199)", "Natywny tytuł ma żądany format i jawnie wybrany typ.");
				Check(Field<ToolStripStatusLabel>(mainForm, "statusUnit").Text.Contains("Jednostka organizacyjna: EDR"), "Tryb SimRail pokazuje jednostkę organizacyjną EDR.");
				Check(mainForm.FormBorderStyle == FormBorderStyle.Sizable && mainForm.ControlBox && mainForm.MaximizeBox, "Zachowano natywną ramkę i standardowe przyciski Windows.");
				Ui.Capture(mainForm, Path.Combine(folder, "tytul.png"));
				mainForm.Close();
			}
			engine.EndDuty(engine.Now);
			log.Add("ALL RETRY, VALIDATION AND IDENTITY TESTS PASSED");
			return true;
		}
		catch (Exception ex3)
		{
			log.Add(ex3.ToString());
			return false;
		}
		finally
		{
			File.WriteAllLines(Path.Combine(folder, "wyniki-poprawki23.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

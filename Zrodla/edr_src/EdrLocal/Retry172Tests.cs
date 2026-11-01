using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EdrLocal;

public static class Retry172Tests
{
	private sealed class Feed : ISimulatorFeed
	{
		public SimRailSnapshot Value;

		public int Reads;

		public bool ProviderDown;

		public string ProviderId => "fixture172";

		public Task<List<SimRailServer>> ServersAsync(CancellationToken token)
		{
			throw new NotSupportedException("Bez sieci.");
		}

		public Task<SimRailSnapshot> SnapshotAsync(string server, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			Reads++;
			if (ProviderDown)
			{
				throw new IOException("Lokalna awaria odczytu");
			}
			return Task.FromResult(Value);
		}
	}

	private sealed class Writer : ISepeHoursWriter, IDisposable
	{
		public int Calls;

		public bool Disposed;

		public Func<SepeWriteRequest, Task> Action;

		public bool SubmissionAttempted { get; set; }

		public Task Send(SepeWriteRequest request)
		{
			Calls++;
			return Action(request);
		}

		public void Dispose()
		{
			Disposed = true;
		}
	}

	private static readonly List<string> log = new List<string>();

	private static readonly DateTime day = new DateTime(2026, 10, 23, 12, 0, 0);

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

	private static void Flag(object o, string n, bool v)
	{
		o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, v);
	}

	private static object Call(object o, string n, params object[] args)
	{
		try
		{
			return o.GetType().GetMethod(n, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, args);
		}
		catch (TargetInvocationException ex)
		{
			throw ex.InnerException;
		}
	}

	private static void Pump(Func<bool> done)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (!done())
		{
			if (stopwatch.ElapsedMilliseconds > 20000)
			{
				throw new Exception("Timeout testu lokalnego.");
			}
			Application.DoEvents();
			Thread.Sleep(5);
		}
		Application.DoEvents();
	}

	private static SimRailSnapshot Snapshot()
	{
		SimRailTrain simRailTrain = new SimRailTrain();
		simRailTrain.trainNoLocal = "561113";
		simRailTrain.startStation = "A";
		simRailTrain.endStation = "C";
		simRailTrain.trainName = "TME";
		simRailTrain.timetable = new List<SimRailPoint>();
		SimRailTrain simRailTrain2 = simRailTrain;
		for (int i = 0; i < 3; i++)
		{
			simRailTrain2.timetable.Add(new SimRailPoint
			{
				pointId = (10 + i).ToString(),
				nameForPerson = (new string[3] { "A", "B", "C" })[i],
				indexOfPoint = i,
				line = 14,
				track = 1,
				displayedTrainNumber = "561113",
				trainType = "TME",
				isActive = true,
				arrivalTime = "2026-10-23 11:40:00",
				departureTime = "2026-10-23 11:41:00"
			});
		}
		SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
		simRailSnapshot.ServerCode = "test172";
		simRailSnapshot.GameTime = day;
		simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
		simRailSnapshot.DispatcherNames = new HashSet<string> { "A", "B", "C" };
		simRailSnapshot.Trains = new List<SimRailTrain> { simRailTrain2 };
		return simRailSnapshot;
	}

	private static void Clock(Engine engine, DateTime now)
	{
		SimRailSnapshot snapshot = engine.SimRail.Snapshot;
		snapshot.GameTime = now;
		snapshot.DownloadedUtc = DateTime.UtcNow;
		engine.SimRail.Accept(snapshot);
	}

	private static string Link(string key)
	{
		return "https://api.simrail.eu:8093/?serverCode=test172&stationId=11&key=TEST_ONLY_172_" + key;
	}

	public static bool Run(string folder)
	{
		bool result = false;
		Form host = new Form
		{
			ShowInTaskbar = false,
			Opacity = 0.0
		};
		try
		{
			host.Shown += (object s, EventArgs e) =>
			{
				host.BeginInvoke((Action)(() =>
				{
					result = RunCore(folder);
					host.Close();
				}));
			};
			Application.Run(host);
		}
		finally
		{
			if (host != null)
			{
				((IDisposable)host).Dispose();
			}
		}
		return result;
	}

	private static bool RunCore(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		LocalPersistence.Enabled = false;
		try
		{
			Feed feed = new Feed();
			feed.Value = Snapshot();
			Feed feed2 = feed;
			SimRailSession simRailSession = new SimRailSession(feed2, feed2.Value, "11");
			Station station = simRailSession.SuggestStation();
			StationSettings.Normalize(station);
			Store store = new Store(Path.Combine(folder, "memory"));
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
			engine2.ApplySimRailSnapshot(feed2.Value);
			engine2.BeginDuty(day.AddMinutes(-20.0));
			Train train = engine2.State.Trains.Single();
			string id = train.Id;
			engine2.SetTrainTimes(id, day.AddMinutes(-8.0), day.AddMinutes(-7.0));
			SimRailPoint remote = feed2.Value.Trains[0].timetable[1];
			engine2.SetTrainTimes(id, null, day.AddMinutes(-7.0));
			engine2.ApplySimRailSnapshot(feed2.Value);
			Check(engine2.State.Trains.Single().ExternalManualArrival, "Niepotwierdzone puste pole API nie kasuje oczekującego usunięcia godziny.");
			engine2.SetTrainTimes(id, day.AddMinutes(-8.0), day.AddMinutes(-7.0));
			MainForm main = new MainForm(engine2);
			try
			{
				System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
				timer.Interval = 20;
				using System.Windows.Forms.Timer timer2 = timer;
				timer2.Tick += (object s, EventArgs e) =>
				{
					SepeErrorForm[] array = Application.OpenForms.OfType<SepeErrorForm>().ToArray();
					foreach (SepeErrorForm sepeErrorForm in array)
					{
						sepeErrorForm.Close();
					}
				};
				timer2.Start();
				main.Show();
				Application.DoEvents();
				main.SelectTrain(id);
				simRailSession.SetAccessLink(Link("OLD"));
				Flag(main, "hoursPaused", v: true);
				Queue<SepeWriteRequest> queue = Field<Queue<SepeWriteRequest>>(main, "outgoingHours");
				Writer fail = new Writer
				{
					Action = (SepeWriteRequest r) =>
					{
						throw new InvalidOperationException("Wygasły link testowy.");
					}
				};
				main.HoursWriterFactory = () => new Writer
				{
					Action = (SepeWriteRequest r) =>
					{
						fail.Calls++;
						throw new InvalidOperationException("Wygasły link testowy.");
					}
				};
				Call(main, "RetrySepeWrites");
				Pump(() => Field<bool>(main, "hoursPaused") && !Field<bool>(main, "sendingHours"));
				Check(fail.Calls == 3 && queue.Count == 1 && queue.Peek().Arrival == day.AddMinutes(-8.0) && queue.Peek().Departure == day.AddMinutes(-7.0), "Po trzech błędach starego linku pełne zlecenie pozostaje w kolejce.");
				Task replace = (Task)Call(main, "ReplaceSepeLink", Link("NEW"));
				Pump(() => replace.IsCompleted);
				replace.GetAwaiter().GetResult();
				Check(queue.Count == 1 && simRailSession.AccessUrl.Query.Contains("NEW") && Field<bool>(main, "hoursPaused"), "Zmiana linku jest dozwolona z oczekującą kolejką i sama nie wysyła godzin.");
				Writer success = new Writer();
				success.Action = (SepeWriteRequest r) =>
				{
					remote.isConfirmed = true;
					remote.actualArrivalTime = r.Arrival.Value.ToString("yyyy-MM-dd HH:mm:ss");
					remote.actualDepartureTime = r.Departure.Value.ToString("yyyy-MM-dd HH:mm:ss");
					return Task.FromResult(0);
				};
				main.HoursWriterFactory = () => success;
				Call(main, "RetrySepeWrites");
				Pump(() => !Field<bool>(main, "sendingHours"));
				if (queue.Count > 0)
				{
					throw new Exception("Nie opróżniono kolejki: " + Field<string>(main, "lastSepeError") + "; Calls=" + success.Calls);
				}
				Check(success.Calls == 1 && !engine2.State.Trains.Single().ExternalManualTimes, "Ponowienie z nowym linkiem wysyła zachowane wartości i usuwa je po potwierdzeniu API.");
				Flag(main, "hoursPaused", v: true);
				remote.isConfirmed = false;
				engine2.SetTrainTimes(id, day.AddMinutes(-6.0), day.AddMinutes(-5.0));
				engine2.ApplySimRailSnapshot(feed2.Value);
				Train train2 = engine2.State.Trains.Single();
				Check(train2.ExternalManualArrival && train2.ExternalManualDeparture, "Odświeżenie bez potwierdzenia API zachowuje oba znaczniki lokalnych godzin.");
				remote.isConfirmed = true;
				remote.actualArrivalTime = "2026-10-23 11:54:00";
				remote.actualDepartureTime = "2026-10-23 11:55:00";
				int calls = success.Calls;
				Call(main, "RetrySepeWrites");
				Pump(() => !Field<bool>(main, "sendingHours") && queue.Count == 0);
				Check(success.Calls == calls && !engine2.State.Trains.Single().ExternalManualTimes, "Już potwierdzony zapis jest rozpoznawany przed ponowieniem bez drugiego kliknięcia Zapisz.");
				Flag(main, "hoursPaused", v: true);
				engine2.SetTrainTimes(id, day.AddMinutes(-4.0), day.AddMinutes(-3.0));
				TaskCompletionSource<bool> blocked = new TaskCompletionSource<bool>();
				Writer hanging = new Writer
				{
					Action = (SepeWriteRequest r) => blocked.Task
				};
				main.HoursWriterFactory = () => hanging;
				Field<ISepeHoursWriter>(main, "backgroundWriter")?.Dispose();
				typeof(MainForm).GetField("backgroundWriter", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(main, null);
				Call(main, "RetrySepeWrites");
				Pump(() => hanging.Calls == 1);
				Task task = (Task)Call(main, "ReplaceSepeLink", Link("WRONG").Replace("serverCode=test172", "serverCode=wrong"));
				Check(task.IsFaulted && Field<bool>(main, "sendingHours") && !hanging.Disposed, "Błędny link nie przerywa poprawnie rozpoczętej operacji ani nie zmienia uprawnień.");
				try
				{
					task.GetAwaiter().GetResult();
				}
				catch (ArgumentException)
				{
				}
				Task cancellation = (Task)Call(main, "ReplaceSepeLink", Link("THIRD"));
				Pump(() => cancellation.IsCompleted);
				cancellation.GetAwaiter().GetResult();
				Check(hanging.Disposed && !Field<bool>(main, "sendingHours") && queue.Count == 1 && simRailSession.AccessUrl.Query.Contains("THIRD"), "Zmiana linku przerywa trwające przygotowanie bez blokady i zachowuje godziny.");
				blocked.TrySetResult(result: true);
				Application.DoEvents();
				engine2.SetTrainTimes(id, day.AddMinutes(-5.0), day.AddMinutes(-3.0));
				engine2.SetTrainTimes(id, day.AddMinutes(-5.0), day.AddMinutes(-2.0));
				Flag(main, "decidingExit", v: true);
				Call(main, "RetrySepeWrites");
				Check(queue.Count == 1 && queue.Peek().Arrival == day.AddMinutes(-5.0) && queue.Peek().Departure == day.AddMinutes(-2.0), "Ponowienie łączy nowsze poprawki obu godzin zamiast wysyłać stare wartości lub duplikaty.");
				Flag(main, "hoursPaused", v: true);
				Flag(main, "decidingExit", v: false);
				feed2.ProviderDown = true;
				Call(main, "RetrySepeWrites");
				Pump(() => !Field<bool>(main, "sendingHours"));
				Check(queue.Count == 1 && Field<bool>(main, "hoursPaused"), "Awaria odczytu API przed wysłaniem również zachowuje oczekujące godziny.");
				feed2.ProviderDown = false;
				Writer uncertain = new Writer
				{
					SubmissionAttempted = true,
					Action = (SepeWriteRequest r) =>
					{
						throw new IOException("Nieznany wynik kliknięcia Zapisz.");
					}
				};
				main.HoursWriterFactory = () => uncertain;
				Call(main, "RetrySepeWrites");
				Pump(() => !Field<bool>(main, "sendingHours"));
				Check(uncertain.Calls == 1 && queue.Count == 1 && Field<bool>(main, "hoursPaused"), "Brak potwierdzenia po możliwym zapisie zachowuje zlecenie i nie ponawia kliknięcia trzy razy.");
				main.Close();
			}
			finally
			{
				if (main != null)
				{
					((IDisposable)main).Dispose();
				}
			}
			Clock(engine2, day);
			string section = engine2.Station.Sections[0].Id;
			JournalEntry entry = engine2.Note(section, day.AddMinutes(-5.0), "", "Pociąg");
			entry.Number = "TEST-SBL";
			entry.Direction = "DO";
			entry.Left = day.AddMinutes(-5.0);
			entry.Track = "1";
			engine2.SaveJournal(entry);
			entry = engine2.State.Entries.Single((JournalEntry x) => x.Id == entry.Id);
			DateTime value = entry.LineIndicationStartedAt.Value;
			Check(engine2.LineStatus(section, entry.LineTrack).StartsWith("ZAJĘTY"), "Odjazd wpisany pięć minut po czasie od razu włącza zajętość SBL.");
			Check(entry.Left == day.AddMinutes(-5.0), "Timer kontrolki nie zmienia rzeczywistej godziny odjazdu.");
			using (MainForm mainForm = new MainForm(engine2))
			{
				mainForm.Show();
				Application.DoEvents();
				JournalPane journalPane = Field<List<JournalPane>>(mainForm, "panes").First((JournalPane x) => x.SectionId == section);
				journalPane.RefreshSignals();
				RailSignal railSignal = Field<List<RailSignal>>(journalPane, "signals").Single((RailSignal x) => x.Number == entry.LineTrack);
				Check(railSignal.Status.StartsWith("ZAJĘTY") && railSignal.MovementIncoming == false, "Rzeczywista kontrolka szlaku pokazuje od razu zajętość i kierunek wyprawienia.");
				Ui.Capture(mainForm, Path.Combine(folder, "sbl-po-spoznionym-wpisie.png"));
				Clock(engine2, value.AddSeconds(59.0));
				Check(engine2.LineStatus(section, entry.LineTrack).StartsWith("ZAJĘTY"), "Domyślna zajętość utrzymuje się do 60 sekund od zarejestrowania odjazdu.");
				Clock(engine2, value.AddSeconds(61.0));
				journalPane.RefreshSignals();
				Check(railSignal.Status == "WOLNY" && !railSignal.MovementIncoming.HasValue, "Po czasie kontrolka wygasa bez dopisywania przyjazdu sąsiada.");
				entry = Json.Clone(engine2.State.Entries.Single((JournalEntry x) => x.Id == entry.Id));
				entry.Text = "Zmieniono tylko uwagę";
				engine2.SaveJournal(entry);
				Check(engine2.State.Entries.Single((JournalEntry x) => x.Id == entry.Id).LineIndicationStartedAt == value && engine2.LineStatus(section, entry.LineTrack) == "WOLNY", "Edycja uwag nie włącza ponownie wygasłej zajętości.");
				engine2.Station.Options.AutomaticReleaseSeconds = 120;
				Clock(engine2, value.AddSeconds(100.0));
				Check(engine2.LineStatus(section, entry.LineTrack).StartsWith("ZAJĘTY"), "Własny czas 120 sekund nadal jest respektowany.");
				mainForm.Close();
			}
			Check(!Directory.Exists(store.DirectoryPath), "Próba działa w pamięci i nie zapisuje danych operacyjnych.");
			log.Add("ALL LOCAL QUEUE, LINK REPLACEMENT AND SBL TESTS PASSED");
			return true;
		}
		catch (Exception ex2)
		{
			log.Add(ex2.ToString());
			return false;
		}
		finally
		{
			File.WriteAllLines(Path.Combine(folder, "wyniki-poprawki172.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

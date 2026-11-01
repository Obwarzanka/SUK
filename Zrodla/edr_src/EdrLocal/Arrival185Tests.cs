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

public static class Arrival185Tests
{
	private sealed class Feed : ISimulatorFeed
	{
		public SimRailSnapshot Value;

		public string ProviderId => "fixture185";

		public Task<List<SimRailServer>> ServersAsync(CancellationToken token)
		{
			throw new NotSupportedException("Bez sieci");
		}

		public Task<SimRailSnapshot> SnapshotAsync(string server, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			return Task.FromResult(Value);
		}
	}

	private sealed class Writer : ISepeHoursWriter, IDisposable
	{
		public Func<SepeWriteRequest, Task> Action;

		public bool SubmissionAttempted { get; set; }

		public Task Send(SepeWriteRequest request)
		{
			return Action(request);
		}

		public void Dispose()
		{
		}
	}

	private sealed class Case
	{
		public Engine Engine;

		public SimRailPoint Remote;

		public SimRailSession Session;

		public string Id;

		public JournalEntry Arrival()
		{
			Train t = Engine.State.Trains.Single();
			JournalEntry journalEntry = Engine.Note(Engine.Station.Sections.First((Section x) => x.Name == t.From).Id, now, "", "Pociąg");
			journalEntry.TrainId = t.Id;
			journalEntry.Number = t.Number;
			journalEntry.Direction = "Z";
			journalEntry.Track = "1";
			return journalEntry;
		}
	}

	private static readonly List<string> log = new List<string>();

	private static readonly DateTime now = new DateTime(2026, 10, 24, 7, 15, 0);

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

	private static void Set(object o, string n, object value)
	{
		o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, value);
	}

	private static void Call(object o, string n)
	{
		try
		{
			o.GetType().GetMethod(n, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, null);
		}
		catch (TargetInvocationException ex)
		{
			throw ex.InnerException;
		}
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

	private static void Pump(Func<bool> done)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (!done())
		{
			if (stopwatch.ElapsedMilliseconds > 15000)
			{
				throw new Exception("Timeout testu przyjazdu.");
			}
			Application.DoEvents();
			Thread.Sleep(5);
		}
		Application.DoEvents();
	}

	private static Case Make(string folder, bool link)
	{
		SimRailTrain simRailTrain = new SimRailTrain();
		simRailTrain.trainNoLocal = "47512";
		simRailTrain.startStation = "A";
		simRailTrain.endStation = "C";
		simRailTrain.trainName = "MPE";
		simRailTrain.timetable = new List<SimRailPoint>();
		SimRailTrain simRailTrain2 = simRailTrain;
		for (int i = 0; i < 3; i++)
		{
			simRailTrain2.timetable.Add(new SimRailPoint
			{
				pointId = (10 + i).ToString(),
				nameForPerson = (new string[3] { "A", "B", "C" })[i],
				indexOfPoint = i,
				line = 1,
				track = 1,
				displayedTrainNumber = "47512",
				trainType = "MPE",
				isActive = true,
				isConfirmed = true,
				isStoped = true,
				arrivalTime = "2026-10-24 07:00:00",
				departureTime = "2026-10-24 07:05:00",
				actualArrivalTime = "2026-10-24 07:00:00",
				actualDepartureTime = "2026-10-24 07:05:00"
			});
		}
		SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
		simRailSnapshot.ServerCode = "fixture185";
		simRailSnapshot.Trains = new List<SimRailTrain> { simRailTrain2 };
		simRailSnapshot.DispatcherNames = new HashSet<string> { "A", "B", "C" };
		simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
		simRailSnapshot.GameTime = now;
		SimRailSnapshot simRailSnapshot2 = simRailSnapshot;
		Feed feed = new Feed();
		feed.Value = simRailSnapshot2;
		Feed feed2 = feed;
		SimRailSession simRailSession = new SimRailSession(feed2, simRailSnapshot2, "11");
		Station station = simRailSession.SuggestStation();
		StationSettings.Normalize(station);
		Store store = new Store(Path.Combine(folder, Guid.NewGuid().ToString("N")));
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
		engine2.ApplySimRailSnapshot(simRailSnapshot2);
		engine2.BeginDuty(now.AddHours(-1.0));
		if (link)
		{
			simRailSession.SetAccessLink("https://api.simrail.eu:8093/?serverCode=fixture185&stationId=11&key=TEST_ONLY_185");
		}
		Case obj = new Case();
		obj.Engine = engine2;
		obj.Session = simRailSession;
		obj.Remote = simRailTrain2.timetable[1];
		obj.Id = engine2.State.Trains.Single().Id;
		return obj;
	}

	private static SepeWriteRequest Request()
	{
		SepeWriteRequest sepeWriteRequest = new SepeWriteRequest();
		sepeWriteRequest.TrainId = "TEST";
		sepeWriteRequest.Number = "47512";
		sepeWriteRequest.ExternalNumber = "47512";
		sepeWriteRequest.PointIndex = 1;
		sepeWriteRequest.PlanArrival = now.AddMinutes(-15.0);
		sepeWriteRequest.PlanDeparture = now.AddMinutes(-10.0);
		sepeWriteRequest.Arrival = now.AddMinutes(-3.0);
		sepeWriteRequest.Departure = now.AddMinutes(-10.0);
		sepeWriteRequest.ArrivalChanged = true;
		sepeWriteRequest.AdjustDepartureForArrival = true;
		return sepeWriteRequest;
	}

	private static async Task<string> Prepare(SepeWriteRequest request, string departure, Action<int> fieldWritten = null)
	{
		string submitted = null;
		await SepeHoursPreparation.Submit(() => request, (string code) =>
		{
			string text = code.Substring(code.LastIndexOf("window.EdrSepeBridge.", StringComparison.Ordinal));
			if (text.Contains(".identity("))
			{
				return Task.FromResult("\"matched\"");
			}
			if (text.Contains(".readTimes("))
			{
				return Task.FromResult(Json.Text(new string[2] { "2026-10-24 07:00", departure }));
			}
			if (text.Contains(".field("))
			{
				if (fieldWritten != null)
				{
					fieldWritten(int.Parse(text.Substring(text.LastIndexOf(',') + 1).TrimEnd(')', ';')));
				}
				return Task.FromResult("\"prepared\"");
			}
			if (text.Contains(".verify("))
			{
				return Task.FromResult("\"verified\"");
			}
			if (text.Contains(".submitRequest("))
			{
				submitted = text;
				return Task.FromResult("\"submitted\"");
			}
			throw new Exception("Nieznany skrypt lokalny");
		}, (int ms) => Task.FromResult(0), () => now, (bool value) =>
		{
		});
		return submitted;
	}

	private static void Policies(string folder)
	{
		SepeWriteRequest sepeWriteRequest = Request();
		List<int> fields = new List<int>();
		string result = Prepare(sepeWriteRequest, "2026-10-24 07:05", (int i) =>
		{
			fields.Add(i);
		}).GetAwaiter().GetResult();
		Check(sepeWriteRequest.DepartureChanged && sepeWriteRequest.DepartureCorrection && sepeWriteRequest.Departure == sepeWriteRequest.Arrival && result.Contains("07:12\",\"2026-10-24 07:12"), "Starszy odjazd formularza jest wyrównany do nowego przyjazdu w tym samym zapisie.");
		Check(fields.SequenceEqual(new int[6] { 3, 4, 5, 0, 1, 2 }), "Odjazd formularza jest korygowany przed ustawieniem późniejszego przyjazdu, bez przejściowo odwróconej pary.");
		sepeWriteRequest = Request();
		result = Prepare(sepeWriteRequest, "2026-10-24 07:20").GetAwaiter().GetResult();
		Check(!sepeWriteRequest.DepartureChanged && !sepeWriteRequest.DepartureCorrection && result.Contains("07:12\",null"), "Poprawna późniejsza godzina formularza pozostaje nietknięta.");
		sepeWriteRequest = Request();
		Prepare(sepeWriteRequest, "").GetAwaiter().GetResult();
		Check(!sepeWriteRequest.DepartureChanged, "Puste pole odjazdu nie jest dopisywane, gdy nie powoduje błędu chronologii.");
		sepeWriteRequest = Request();
		sepeWriteRequest.Arrival = sepeWriteRequest.Arrival.Value.AddSeconds(45.0);
		Prepare(sepeWriteRequest, "2026-10-24 07:12").GetAwaiter().GetResult();
		Check(!sepeWriteRequest.DepartureChanged, "Porównanie na poziomie minut nie koryguje zgodnych godzin z powodu sekund.");
		sepeWriteRequest = Request();
		sepeWriteRequest.Arrival = new DateTime(2026, 10, 25, 0, 2, 0);
		Prepare(sepeWriteRequest, "2026-10-24 23:58").GetAwaiter().GetResult();
		Check(sepeWriteRequest.Departure == new DateTime(2026, 10, 25, 0, 2, 0), "Korekta przenosi również datę odjazdu przy przejściu przez północ.");
		sepeWriteRequest = Request();
		sepeWriteRequest.DepartureChanged = true;
		sepeWriteRequest.DepartureCorrection = true;
		sepeWriteRequest.Departure = sepeWriteRequest.Arrival;
		Prepare(sepeWriteRequest, "2026-10-24 07:20").GetAwaiter().GetResult();
		Check(sepeWriteRequest.Departure == new DateTime(2026, 10, 24, 7, 20, 0), "Ponowienie nie cofa odjazdu, który został już przesunięty na późniejszy czas.");
		sepeWriteRequest = Request();
		sepeWriteRequest.DepartureChanged = true;
		Prepare(sepeWriteRequest, "2026-10-24 07:05").GetAwaiter().GetResult();
		Check(!sepeWriteRequest.DepartureCorrection && sepeWriteRequest.Departure == now.AddMinutes(-10.0), "Jawnie zmieniony odjazd nie podlega automatycznej korekcie przyjazdu.");
		Case noLink = Make(folder, link: false);
		JournalEntry entry = noLink.Arrival();
		entry.Arrived = now.AddMinutes(-3.0);
		string text = Json.Text(noLink.Engine.State);
		Check(Error(() =>
		{
			noLink.Engine.SaveJournal(entry);
		}) is TrainTimeException && Json.Text(noLink.Engine.State) == text, "Bez linku działa dotychczasowa walidacja i zapis jest atomowo odrzucany.");
		Case disabled = Make(folder, link: true);
		disabled.Engine.Station.Options.WriteSepe = false;
		entry = disabled.Arrival();
		entry.Arrived = now.AddMinutes(-3.0);
		Check(Error(() =>
		{
			disabled.Engine.SaveJournal(entry);
		}) is TrainTimeException, "Wyłączenie zapisu SEPE nie włącza automatycznej korekty.");
		Case actual = Make(folder, link: true);
		Train t = actual.Engine.State.Trains.Single();
		JournalEntry departureRow = actual.Engine.Note(actual.Engine.Station.Sections.First((Section x) => x.Name == t.To).Id, now.AddMinutes(-10.0), "", "Pociąg");
		departureRow.TrainId = t.Id;
		departureRow.Number = t.Number;
		departureRow.Direction = "DO";
		departureRow.Track = "1";
		departureRow.Left = now.AddMinutes(-10.0);
		actual.Engine.SaveJournal(departureRow);
		entry = actual.Arrival();
		entry.Arrived = now.AddMinutes(-3.0);
		Check(Error(() =>
		{
			actual.Engine.SaveJournal(entry);
		}) is TrainTimeException && actual.Engine.State.Entries.Single((JournalEntry x) => x.Id == departureRow.Id).Left == now.AddMinutes(-10.0), "Odjazd wpisany w lokalnym dzienniku jest chroniony przed cichym przestawieniem.");
		Case reported = Make(folder, link: true);
		reported.Engine.SetTrainTimes(reported.Id, now.AddMinutes(-15.0), now.AddMinutes(-9.0));
		reported.Remote.actualDepartureTime = "2026-10-24 07:06:00";
		reported.Engine.ApplySimRailSnapshot(reported.Session.Snapshot);
		entry = reported.Arrival();
		entry.Arrived = now.AddMinutes(-3.0);
		Check(!reported.Engine.State.Trains.Single().ExternalManualDeparture && reported.Engine.State.Trains.Single().LocalDepartureRecorded && Error(() =>
		{
			reported.Engine.SaveJournal(entry);
		}) is TrainTimeException, "Odjazd zgłoszony przez okno godzin pozostaje chroniony także po potwierdzeniu API.");
		Case obj = Make(folder, link: true);
		obj.Engine.SetTrainTimes(obj.Id, now.AddMinutes(-15.0), now.AddMinutes(-1.0));
		obj.Remote.actualDepartureTime = "2026-10-24 07:14:00";
		obj.Engine.ApplySimRailSnapshot(obj.Session.Snapshot);
		entry = obj.Arrival();
		entry.Arrived = now.AddMinutes(-3.0);
		obj.Engine.SaveJournal(entry);
		SepeWriteRequest sepeWriteRequest2 = SepeWriteRequest.From(obj.Engine.State.Trains.Single(), arrival: true, departure: false);
		sepeWriteRequest2.DepartureChanged = true;
		sepeWriteRequest2.DepartureCorrection = true;
		sepeWriteRequest2.Departure = sepeWriteRequest2.Arrival;
		using (MainForm obj2 = new MainForm(obj.Engine))
		{
			SepeWriteRequest sepeWriteRequest3 = (SepeWriteRequest)typeof(MainForm).GetMethod("CollectPendingSepeHours", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj2, new object[1] { sepeWriteRequest2 });
			Check(!sepeWriteRequest3.DepartureChanged && !sepeWriteRequest3.DepartureCorrection && sepeWriteRequest3.Departure == now.AddMinutes(-1.0), "Nowszy potwierdzony lokalny odjazd usuwa starą oczekującą korektę, zamiast być przez nią nadpisany.");
		}
		Case future = Make(folder, link: true);
		entry = future.Arrival();
		entry.Arrived = now.AddMinutes(1.0);
		Check(Error(() =>
		{
			future.Engine.SaveJournal(entry);
		}) is TrainTimeException, "Przyjazd w przyszłości nadal jest odrzucany mimo linku.");
		Case obj3 = Make(folder, link: true);
		obj3.Remote.isConfirmed = false;
		obj3.Engine.ApplySimRailSnapshot(obj3.Session.Snapshot);
		Train popupTrain = obj3.Engine.State.Trains.Single();
		JournalEntry blank = obj3.Engine.Note(obj3.Engine.Station.Sections.First((Section x) => x.Name == popupTrain.To).Id, now, "", "Pociąg");
		blank.TrainId = obj3.Id;
		blank.Number = popupTrain.Number;
		blank.Direction = "DO";
		blank.Track = "1";
		obj3.Engine.SaveJournal(blank);
		obj3.Engine.SetTrainTimes(obj3.Id, now.AddMinutes(-3.0), now.AddMinutes(-10.0), inheritedDeparture: true);
		DateTime? arrival = obj3.Engine.State.Trains.Single().Arrival;
		DateTime dateTime = now.AddMinutes(-3.0);
		Check(arrival.HasValue && arrival.GetValueOrDefault() == dateTime && !obj3.Engine.State.Trains.Single().Departure.HasValue && !obj3.Engine.State.Entries.Single((JournalEntry x) => x.Id == blank.Id).Left.HasValue, "Zmiana samego przyjazdu w oknie godzin nie zamienia odziedziczonej prognozy w lokalny odjazd i nie wypełnia pustego wyprawienia.");
		Case strict = Make(folder, link: true);
		Check(Error(() =>
		{
			strict.Engine.SetTrainTimes(strict.Id, now.AddMinutes(-3.0), now.AddMinutes(-8.0));
		}) is TrainTimeException, "Rzeczywiste wprowadzenie dwóch sprzecznych godzin nadal jest błędem.");
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
					result = Core(folder);
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

	private static bool Core(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		LocalPersistence.Enabled = false;
		try
		{
			Policies(folder);
			Case test = Make(folder, link: false);
			Engine engine = test.Engine;
			JournalEntry entry = test.Arrival();
			engine.SaveJournal(entry);
			MainForm main = new MainForm(engine);
			try
			{
				int notices = 0;
				int writes = 0;
				SepeWriteRequest sent = null;
				main.OperatorNoticeSink = (string a, string b) =>
				{
					notices++;
				};
				main.Show();
				Application.DoEvents();
				Set(main, "hoursPaused", true);
				test.Session.SetAccessLink("https://api.simrail.eu:8093/?serverCode=fixture185&stationId=11&key=TEST_ONLY_185");
				main.HoursWriterFactory = () => new Writer
				{
					SubmissionAttempted = true,
					Action = async (SepeWriteRequest request) =>
					{
						writes++;
						Check(request.AdjustDepartureForArrival && !request.DepartureChanged, "Kolejka zezwala na korektę pola SEPE wyłącznie dla uprawnionego wpisu przyjazdu.");
						await Prepare(request, "2026-10-24 07:05");
						sent = request;
						test.Remote.actualArrivalTime = request.Arrival.Value.ToString("yyyy-MM-dd HH:mm:ss");
						test.Remote.actualDepartureTime = request.Departure.Value.ToString("yyyy-MM-dd HH:mm:ss");
						test.Remote.isConfirmed = true;
					}
				};
				JournalPane journalPane = Field<List<JournalPane>>(main, "panes").Single((JournalPane x) => x.SectionId == entry.SectionId);
				journalPane.FocusEntry(entry.Id);
				DataGridViewRow dataGridViewRow = journalPane.Grid.Rows.Cast<DataGridViewRow>().Single((DataGridViewRow x) => ((JournalEntry)x.Tag).Id == entry.Id);
				journalPane.Grid.CurrentCell = dataGridViewRow.Cells["arrived"];
				journalPane.Grid.BeginEdit(selectAll: true);
				((TextBox)journalPane.Grid.EditingControl).Text = "07:12";
				Check(journalPane.Grid.EndEdit() && journalPane.CommitPending() && engine.State.Entries.Single((JournalEntry x) => x.Id == entry.Id).Arrived == now.AddMinutes(-3.0), "Rzeczywisty wiersz dziennika przyjmuje przyjazd późniejszy od starej godziny SEPE bez komunikatu błędu.");
				Check(!engine.State.Entries.Any((JournalEntry x) => x.TrainId == test.Id && x.Direction == "DO" && x.Left.HasValue), "Zapis przyjazdu nie tworzy ani nie wypełnia wyprawienia.");
				Call(main, "RetrySepeWrites");
				Pump(() => !Field<bool>(main, "sendingHours"));
				Check(writes == 1 && notices == 0 && sent != null && sent.DepartureCorrection && test.Remote.actualDepartureTime == test.Remote.actualArrivalTime, "Przyjazd i korekta odjazdu zapisują się w jednej próbie, bez błędu i ręcznego powtórzenia.");
				Check(!engine.State.Trains.Single().ExternalManualTimes && !engine.State.Trains.Single().LocalDepartureRecorded && engine.State.Trains.Single().Stopped && !engine.State.Entries.Any((JournalEntry x) => x.TrainId == test.Id && x.Direction == "DO" && x.Left.HasValue), "Potwierdzenie API kończy oczekiwanie, zachowuje STÓJ i nadal nie dopisuje odjazdu do dziennika.");
				test.Remote.actualDepartureTime = "2026-10-24 07:14:00";
				Check(sent.Confirmed(test.Session.Snapshot, "11"), "Późniejszy odjazd w API spełnia cel korekty i nie wymaga cofania go do przyjazdu.");
				SepeWriteRequest sepeWriteRequest = SepeWriteRequest.From(engine.State.Trains.Single(), arrival: true, departure: true);
				Check(!sepeWriteRequest.Confirmed(test.Session.Snapshot, "11"), "Zwykły ręczny odjazd nadal wymaga dokładnego potwierdzenia minuty.");
				main.Close();
			}
			finally
			{
				if (main != null)
				{
					((IDisposable)main).Dispose();
				}
			}
			log.Add("ALL OFFLINE ARRIVAL185 TESTS PASSED");
			return true;
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			return false;
		}
		finally
		{
			File.WriteAllLines(Path.Combine(folder, "wyniki-przyjazd185.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

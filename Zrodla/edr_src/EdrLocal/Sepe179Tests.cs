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

public static class Sepe179Tests
{
	private sealed class Feed : ISimulatorFeed
	{
		public SimRailSnapshot Value;

		public TaskCompletionSource<SimRailSnapshot> Pending;

		public int Reads;

		public string ProviderId => "fixture179";

		public Task<List<SimRailServer>> ServersAsync(CancellationToken token)
		{
			throw new NotSupportedException("Bez sieci");
		}

		public Task<SimRailSnapshot> SnapshotAsync(string server, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			Reads++;
			if (Pending != null)
			{
				return Pending.Task;
			}
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

	private static readonly List<string> log = new List<string>();

	private static readonly DateTime now = new DateTime(2026, 10, 24, 12, 0, 0);

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

	private static void Pump(Func<bool> done)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (!done())
		{
			if (stopwatch.ElapsedMilliseconds > 15000)
			{
				throw new Exception("Timeout lokalnego scenariusza.");
			}
			Application.DoEvents();
			Thread.Sleep(5);
		}
		Application.DoEvents();
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

	private static SepeWriteRequest Request(int arrival, int departure)
	{
		SepeWriteRequest sepeWriteRequest = new SepeWriteRequest();
		sepeWriteRequest.TrainId = "TEST";
		sepeWriteRequest.Number = "561113";
		sepeWriteRequest.ExternalNumber = "561113";
		sepeWriteRequest.PointIndex = 1;
		sepeWriteRequest.PlanArrival = now.AddMinutes(-20.0);
		sepeWriteRequest.PlanDeparture = now.AddMinutes(-19.0);
		sepeWriteRequest.Origin = "A";
		sepeWriteRequest.Destination = "C";
		sepeWriteRequest.ArrivalChanged = true;
		sepeWriteRequest.DepartureChanged = true;
		sepeWriteRequest.Arrival = now.AddMinutes(arrival);
		sepeWriteRequest.Departure = now.AddMinutes(departure);
		return sepeWriteRequest;
	}

	private static void Preparation()
	{
		int fields = 0;
		int submits = 0;
		int verifies = 0;
		bool attempted = false;
		SepeWriteRequest latest = Request(-8, -7);
		string sent = null;
		Func<string, Task<string>> script = (string code) =>
		{
			string text = code.Substring(code.LastIndexOf("window.EdrSepeBridge.", StringComparison.Ordinal));
			if (text.Contains(".identity("))
			{
				return Task.FromResult("\"matched\"");
			}
			if (text.Contains(".field("))
			{
				fields++;
				return Task.FromResult("\"prepared\"");
			}
			if (text.Contains(".verify("))
			{
				if (++verifies == 1)
				{
					latest = Request(-6, -5);
				}
				return Task.FromResult("\"verified\"");
			}
			if (text.Contains(".submitRequest("))
			{
				submits++;
				sent = text;
				return Task.FromResult("\"submitted\"");
			}
			throw new Exception("Nieoczekiwany skrypt");
		};
		SepeHoursPreparation.Submit(() => latest, script, (int ms) => Task.FromResult(0), () => now, (bool value) =>
		{
			attempted = value;
		}).GetAwaiter().GetResult();
		Check(fields == 12 && submits == 1 && attempted && sent.Contains("2026-10-24 11:54") && sent.Contains("2026-10-24 11:55"), "Lokalna zmiana obu godzin podczas przygotowania odtwarza pola i zapisuje wyłącznie najnowszą parę.");
		fields = 0;
		submits = 0;
		latest = Request(-6, -5);
		Func<string, Task<string>> ordinary = script;
		script = (string code) =>
		{
			if (code.Substring(code.LastIndexOf("window.EdrSepeBridge.", StringComparison.Ordinal)).Contains(".submitRequest("))
			{
				submits++;
				return Task.FromResult((submits == 1) ? "\"changed\"" : "\"submitted\"");
			}
			return ordinary(code);
		};
		SepeHoursPreparation.Submit(() => latest, script, (int ms) => Task.FromResult(0), () => now, (bool value) =>
		{
			attempted = value;
		}).GetAwaiter().GetResult();
		Check(fields == 12 && submits == 2 && attempted, "Późny render zwracający changed wymaga ponownego uzupełnienia pól, bez fałszywej chronologii.");
		submits = 0;
		script = (string code) =>
		{
			if (code.Substring(code.LastIndexOf("window.EdrSepeBridge.", StringComparison.Ordinal)).Contains(".submitRequest("))
			{
				submits++;
				throw new IOException("Niepewny wynik kliknięcia");
			}
			return ordinary(code);
		};
		Exception ex = Error(() =>
		{
			SepeHoursPreparation.Submit(() => latest, script, (int ms) => Task.FromResult(0), () => now, (bool value) =>
			{
				attempted = value;
			}).GetAwaiter().GetResult();
		});
		Check(ex is IOException && submits == 1 && attempted, "Niepewny wynik po Zapisz nie powtarza kliknięcia.");
		script = (string code) => Task.FromResult(code.Substring(code.LastIndexOf("window.EdrSepeBridge.", StringComparison.Ordinal)).Contains(".identity(") ? "\"different\"" : "\"submitted\"");
		submits = 0;
		attempted = false;
		Error(() =>
		{
			SepeHoursPreparation.Submit(() => latest, script, (int ms) => Task.FromResult(0), () => now, (bool value) =>
			{
				attempted = value;
			}).GetAwaiter().GetResult();
		});
		Check(!attempted, "Zmiana tożsamości zatrzymuje przygotowanie przed zapisem.");
		fields = 0;
		script = (string code) => code.Substring(code.LastIndexOf("window.EdrSepeBridge.", StringComparison.Ordinal)).Contains(".submitRequest(") ? Task.FromResult(Json.Text("validation: Czas przyjazdu jest późniejszy od czasu odjazdu w formularzu SEPE.")) : ordinary(code);
		ex = Error(() =>
		{
			SepeHoursPreparation.Submit(() => latest, script, (int ms) => Task.FromResult(0), () => now, (bool value) =>
			{
				attempted = value;
			}).GetAwaiter().GetResult();
		});
		Check(ex is SepeChronologyException && !attempted, "Rzeczywiste odrzucenie chronologii nadal trafia do okna ponowień 5 s / 30 s.");
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
			Preparation();
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
					arrivalTime = "2026-10-24 11:40:00",
					departureTime = "2026-10-24 11:41:00"
				});
			}
			SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
			simRailSnapshot.ServerCode = "test179";
			simRailSnapshot.GameTime = now;
			simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
			simRailSnapshot.DispatcherNames = new HashSet<string> { "A", "B", "C" };
			simRailSnapshot.Trains = new List<SimRailTrain> { simRailTrain2 };
			SimRailSnapshot simRailSnapshot2 = simRailSnapshot;
			Feed feed = new Feed
			{
				Value = simRailSnapshot2
			};
			SimRailSession simRailSession = new SimRailSession(feed, simRailSnapshot2, "11");
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
			engine2.ApplySimRailSnapshot(simRailSnapshot2);
			engine2.BeginDuty(now.AddMinutes(-20.0));
			string id = engine2.State.Trains.Single().Id;
			SimRailPoint remote = simRailTrain2.timetable[1];
			MainForm main = new MainForm(engine2);
			try
			{
				System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
				timer.Interval = 10;
				using System.Windows.Forms.Timer timer2 = timer;
				int errors = 0;
				timer2.Tick += (object s, EventArgs e) =>
				{
					SepeErrorForm[] array = Application.OpenForms.OfType<SepeErrorForm>().ToArray();
					foreach (SepeErrorForm sepeErrorForm in array)
					{
						errors++;
						sepeErrorForm.Close();
					}
				};
				timer2.Start();
				main.Show();
				Application.DoEvents();
				simRailSession.SetAccessLink("https://api.simrail.eu:8093/?serverCode=test179&stationId=11&key=TEST_ONLY_179");
				Set(main, "hoursPaused", true);
				engine2.SetTrainTimes(id, now.AddMinutes(-8.0), now.AddMinutes(-7.0));
				Train train = engine2.State.Trains.Single();
				JournalEntry entry = engine2.Note(station.Sections.First((Section x) => x.Name == train.To).Id, now.AddMinutes(-7.0), "", "Pociąg");
				entry.TrainId = id;
				entry.Number = train.Number;
				entry.Direction = "DO";
				entry.Track = train.Track;
				entry.Left = train.Departure;
				engine2.SaveJournal(entry);
				main.BuildJournals();
				main.RefreshData();
				JournalPane journalPane = Field<List<JournalPane>>(main, "panes").Single((JournalPane x) => x.SectionId == entry.SectionId);
				DataGridViewRow dataGridViewRow = journalPane.Grid.Rows.Cast<DataGridViewRow>().Single((DataGridViewRow x) => ((JournalEntry)x.Tag).Id == entry.Id);
				dataGridViewRow.Cells["left"].Value = "11:59";
				Check(engine2.State.Trains.Single().Departure == now.AddMinutes(-7.0), "Szkic odjazdu czeka w tabeli, a model ma jeszcze poprzednią godzinę.");
				int writes = 0;
				main.HoursWriterFactory = () => new Writer
				{
					SubmissionAttempted = true,
					Action = (SepeWriteRequest r) =>
					{
						writes++;
						int ok;
						if (r.Arrival == now.AddMinutes(-2.0))
						{
							DateTime? departure = r.Departure;
							DateTime dateTime = now.AddMinutes(-1.0);
							if (departure.HasValue && departure.GetValueOrDefault() == dateTime && r.ArrivalChanged)
							{
								ok = (r.DepartureChanged ? 1 : 0);
								goto IL_0094;
							}
						}
						ok = 0;
						goto IL_0094;
						IL_0094:
						Check((byte)ok != 0, "Automatyczna wysyłka używa edycji tabeli i zmian powstałych podczas odczytu API.");
						remote.isConfirmed = true;
						remote.actualArrivalTime = r.Arrival.Value.ToString("yyyy-MM-dd HH:mm:ss");
						remote.actualDepartureTime = r.Departure.Value.ToString("yyyy-MM-dd HH:mm:ss");
						return Task.FromResult(0);
					}
				};
				TaskCompletionSource<SimRailSnapshot> taskCompletionSource = new TaskCompletionSource<SimRailSnapshot>();
				feed.Pending = taskCompletionSource;
				Set(main, "hoursPaused", false);
				Call(main, "ProcessSepeWrites");
				Pump(() => feed.Reads == 1);
				Check(engine2.State.Trains.Single().Departure == now.AddMinutes(-1.0) && !Field<bool>(journalPane, "dirty"), "Automatyczna próba zatwierdza szkic dziennika tak jak polecenie ręczne.");
				engine2.SetTrainTimes(id, now.AddMinutes(-2.0), now.AddMinutes(-1.0));
				feed.Pending = null;
				taskCompletionSource.SetResult(simRailSnapshot2);
				Pump(() => !Field<bool>(main, "sendingHours"));
				Check(errors == 0 && writes == 1 && !engine2.State.Trains.Single().ExternalManualTimes && Field<Queue<SepeWriteRequest>>(main, "outgoingHours").Count == 0, "Obie godziny są potwierdzone bez ręcznego ponowienia i bez raportu błędu.");
				main.Close();
			}
			finally
			{
				if (main != null)
				{
					((IDisposable)main).Dispose();
				}
			}
			Check(!Directory.Exists(store.DirectoryPath), "Scenariusz działa bez sieci i nie zapisuje sesji.");
			log.Add("ALL OFFLINE SEPE179 TESTS PASSED");
			return true;
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			return false;
		}
		finally
		{
			File.WriteAllLines(Path.Combine(folder, "wyniki-sepe179.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

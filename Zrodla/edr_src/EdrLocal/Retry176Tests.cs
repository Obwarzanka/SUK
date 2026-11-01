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

public static class Retry176Tests
{
	private sealed class Feed : ISimulatorFeed
	{
		public SimRailSnapshot Value;

		public int Reads;

		public string ProviderId => "fixture176";

		public Task<List<SimRailServer>> ServersAsync(CancellationToken token)
		{
			throw new NotSupportedException("Bez sieci.");
		}

		public Task<SimRailSnapshot> SnapshotAsync(string server, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			Reads++;
			return Task.FromResult(Value);
		}
	}

	private sealed class Writer : ISepeHoursWriter, IDisposable
	{
		public bool Disposed;

		public Func<SepeWriteRequest, Task> Action;

		public bool SubmissionAttempted { get; set; }

		public Task Send(SepeWriteRequest r)
		{
			return Action(r);
		}

		public void Dispose()
		{
			Disposed = true;
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
				throw new Exception("Timeout lokalnego testu.");
			}
			Application.DoEvents();
			Thread.Sleep(5);
		}
		Application.DoEvents();
	}

	private static Exception Rejection()
	{
		return SepeChronologyException.FromPage("SEPE: Godzina odjazdu nie może być wcześniejsza niż godzina przyjazdu.");
	}

	private static void Policy()
	{
		string[] array = new string[5] { "Odjazd jest wcześniej niż przyjazd", "Godzina odjazdu nie może być wcześniejsza niż godzina przyjazdu.", "Walidacja formularza SEPE: Czas przyjazdu jest późniejszy od czasu odjazdu w formularzu SEPE.", "Departure time is earlier than arrival time", "Arrival time is later than departure time" };
		foreach (string text in array)
		{
			Check(SepeChronologyException.FromPage(text) is SepeChronologyException, "Rozpoznano odrzucenie chronologii: " + text);
		}
		Check(!(SepeChronologyException.FromPage("Brak uprawnień albo wygasły link") is SepeChronologyException), "Błąd uprawnień nie jest mylony z przejściową chronologią.");
		long elapsed = 0L;
		int calls = 0;
		List<int> waits = new List<int>();
		Exception ex = Error(() =>
		{
			SepeChronologyRetry.Run((int n, CancellationToken t) =>
			{
				calls++;
				throw Rejection();
			}, null, CancellationToken.None, (int ms, CancellationToken t) =>
			{
				waits.Add(ms);
				elapsed += ms;
				return Task.FromResult(0);
			}, () => elapsed).GetAwaiter().GetResult();
		});
		Check(elapsed == 30000 && calls == 6 && waits.Count == 6 && waits.All((int x) => x == 5000) && ex.Message.Contains("30 sekund"), "Stałe odrzucenie ponawia co 5 s i kończy błędem na granicy 30 s, bez siódmego zapisu.");
		elapsed = 0L;
		calls = 0;
		SepeChronologyRetry.Run((int n, CancellationToken t) =>
		{
			calls++;
			if (n < 3)
			{
				throw Rejection();
			}
			return Task.FromResult(0);
		}, null, CancellationToken.None, (int ms, CancellationToken t) =>
		{
			elapsed += ms;
			return Task.FromResult(0);
		}, () => elapsed).GetAwaiter().GetResult();
		Check(calls == 3 && elapsed == 10000, "Przejściowy błąd może zakończyć się sukcesem po 10 sekundach bez raportu błędu.");
		elapsed = 0L;
		calls = 0;
		List<int> shortWaits = new List<int>();
		Error(() =>
		{
			SepeChronologyRetry.Run((int n, CancellationToken t) =>
			{
				calls++;
				elapsed += 4000L;
				throw Rejection();
			}, null, CancellationToken.None, (int ms, CancellationToken t) =>
			{
				shortWaits.Add(ms);
				elapsed += ms;
				return Task.FromResult(0);
			}, () => elapsed).GetAwaiter().GetResult();
		});
		Check(elapsed == 34000 && calls == 4 && shortWaits.Last() == 3000, "Czas wykonywania kolejnych prób mieści się w oknie 30 s liczonym od pierwszego odrzucenia.");
		calls = 0;
		Error(() =>
		{
			SepeChronologyRetry.Run((int n, CancellationToken t) =>
			{
				calls++;
				throw new IOException("Nieznany wynik po Zapisz");
			}, null, CancellationToken.None).GetAwaiter().GetResult();
		});
		Check(calls == 1, "Niepewny wynik zapisu nie dostaje automatycznie zgody na ponowne kliknięcie.");
		calls = 0;
		Error(() =>
		{
			SepeChronologyRetry.Run((int n, CancellationToken t) =>
			{
				calls++;
				throw new TrainTimeException("Błąd lokalny");
			}, null, CancellationToken.None).GetAwaiter().GetResult();
		});
		Check(calls == 1, "Lokalna walidacja godzin nie jest ponawiana przez 30 sekund.");
		CancellationTokenSource cancel = new CancellationTokenSource();
		calls = 0;
		ex = Error(() =>
		{
			SepeChronologyRetry.Run((int n, CancellationToken t) =>
			{
				calls++;
				throw Rejection();
			}, null, cancel.Token, (int ms, CancellationToken t) =>
			{
				cancel.Cancel();
				t.ThrowIfCancellationRequested();
				return Task.FromResult(0);
			}).GetAwaiter().GetResult();
		});
		Check(ex is OperationCanceledException && calls == 1, "Zamknięcie sesji lub zmiana linku przerywa oczekiwanie przed następną próbą.");
		calls = 0;
		ex = Error(() =>
		{
			SepeRetry.Run((int n) =>
			{
				calls++;
				throw Rejection();
			}, () => true, null, CancellationToken.None, 0).GetAwaiter().GetResult();
		});
		Check(ex is SepeChronologyException && calls == 1, "Dawna pętla trzech szybkich prób przekazuje chronologię do nowego okna czasowego.");
	}

	private static string Link(string key)
	{
		return "https://api.simrail.eu:8093/?serverCode=test176&stationId=11&key=TEST_ONLY_176_" + key;
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
			Policy();
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
			simRailSnapshot.ServerCode = "test176";
			simRailSnapshot.GameTime = now;
			simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
			simRailSnapshot.DispatcherNames = new HashSet<string> { "A", "B", "C" };
			simRailSnapshot.Trains = new List<SimRailTrain> { simRailTrain2 };
			SimRailSnapshot simRailSnapshot2 = simRailSnapshot;
			Feed feed = new Feed();
			feed.Value = simRailSnapshot2;
			Feed feed2 = feed;
			SimRailSession simRailSession = new SimRailSession(feed2, simRailSnapshot2, "11");
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
			engine.User = store.State.Users.First((LocalUser x) => x.Id == "DEMO");
			engine.SimRail = simRailSession;
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
				simRailSession.SetAccessLink(Link("FIRST"));
				Flag(main, "hoursPaused", v: true);
				engine2.SetTrainTimes(id, now.AddMinutes(-8.0), now.AddMinutes(-7.0));
				int sends = 0;
				List<Writer> writers = new List<Writer>();
				Stopwatch stopwatch = Stopwatch.StartNew();
				main.HoursWriterFactory = () =>
				{
					Writer writer = new Writer
					{
						SubmissionAttempted = true
					};
					writer.Action = (SepeWriteRequest r) =>
					{
						sends++;
						if (sends == 1)
						{
							throw Rejection();
						}
						int ok;
						if (r.Arrival == now.AddMinutes(-6.0))
						{
							DateTime? departure = r.Departure;
							DateTime dateTime = now.AddMinutes(-5.0);
							if (departure.HasValue && departure.GetValueOrDefault() == dateTime && r.ArrivalChanged)
							{
								ok = (r.DepartureChanged ? 1 : 0);
								goto IL_00a3;
							}
						}
						ok = 0;
						goto IL_00a3;
						IL_00a3:
						Check((byte)ok != 0, "Ponowienie wysyła najnowszy komplet lokalnych godzin.");
						remote.isConfirmed = true;
						remote.actualArrivalTime = r.Arrival.Value.ToString("yyyy-MM-dd HH:mm:ss");
						remote.actualDepartureTime = r.Departure.Value.ToString("yyyy-MM-dd HH:mm:ss");
						return Task.FromResult(0);
					};
					writers.Add(writer);
					return writer;
				};
				Call(main, "RetrySepeWrites");
				Pump(() => Field<ToolStripMenuItem>(main, "simulatorState").Text.Contains("kolejna próba"));
				Check(errors == 0 && Field<bool>(main, "sendingHours") && writers[0].Disposed, "Pierwszy błąd nie otwiera raportu; odtwarza formularz i czeka w tle.");
				engine2.SetTrainTimes(id, now.AddMinutes(-6.0), now.AddMinutes(-5.0));
				Pump(() => !Field<bool>(main, "sendingHours"));
				Check(sends == 2 && errors == 0 && stopwatch.ElapsedMilliseconds >= 5000 && !engine2.State.Trains.Single().ExternalManualTimes, "Druga próba po co najmniej 5 s zapisuje godziny i kończy operację bez błędu.");
				Flag(main, "hoursPaused", v: true);
				engine2.SetTrainTimes(id, now.AddMinutes(-4.0), now.AddMinutes(-3.0));
				if (Field<ISepeHoursWriter>(main, "backgroundWriter") != null)
				{
					Field<ISepeHoursWriter>(main, "backgroundWriter").Dispose();
				}
				typeof(MainForm).GetField("backgroundWriter", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(main, null);
				int duplicate = 0;
				main.HoursWriterFactory = () => new Writer
				{
					SubmissionAttempted = true,
					Action = (SepeWriteRequest r) =>
					{
						duplicate++;
						remote.isConfirmed = true;
						remote.actualArrivalTime = r.Arrival.Value.ToString("yyyy-MM-dd HH:mm:ss");
						remote.actualDepartureTime = r.Departure.Value.ToString("yyyy-MM-dd HH:mm:ss");
						throw Rejection();
					}
				};
				Call(main, "RetrySepeWrites");
				Pump(() => !Field<bool>(main, "sendingHours"));
				Check(duplicate == 1 && errors == 0 && !engine2.State.Trains.Single().ExternalManualTimes, "Potwierdzony w międzyczasie zapis API kończy ponowienie bez drugiego wysłania.");
				Flag(main, "hoursPaused", v: true);
				engine2.SetTrainTimes(id, now.AddMinutes(-2.0), now.AddMinutes(-1.0));
				main.HoursWriterFactory = () => new Writer
				{
					Action = (SepeWriteRequest r) =>
					{
						throw Rejection();
					}
				};
				Call(main, "RetrySepeWrites");
				Pump(() => Field<ToolStripMenuItem>(main, "simulatorState").Text.Contains("kolejna próba"));
				Task replace = (Task)Call(main, "ReplaceSepeLink", Link("SECOND"));
				Pump(() => replace.IsCompleted);
				replace.GetAwaiter().GetResult();
				Check(!Field<bool>(main, "sendingHours") && Field<Queue<SepeWriteRequest>>(main, "outgoingHours").Count == 1 && errors == 0, "Zmiana linku przerywa okno ponowień, zachowując zlecenie bez błędu anulowania.");
				main.Close();
			}
			finally
			{
				if (main != null)
				{
					((IDisposable)main).Dispose();
				}
			}
			Check(!Directory.Exists(store.DirectoryPath), "Testy nie zapisują sesji ani nie korzystają z sieci.");
			log.Add("ALL OFFLINE CHRONOLOGY RETRY TESTS PASSED");
			return true;
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			return false;
		}
		finally
		{
			File.WriteAllLines(Path.Combine(folder, "wyniki-ponawianie176.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

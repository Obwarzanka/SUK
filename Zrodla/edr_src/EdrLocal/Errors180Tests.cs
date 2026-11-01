using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EdrLocal;

public static class Errors180Tests
{
	private sealed class Feed : ISimulatorFeed
	{
		public SimRailSnapshot Value;

		public string ProviderId => "fixture180";

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
		public bool Disposed;

		public Func<SepeWriteRequest, Task> Action;

		public bool SubmissionAttempted { get; set; }

		public Task Send(SepeWriteRequest request)
		{
			return Action(request);
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
				throw new Exception("Timeout testu komunikatów.");
			}
			Application.DoEvents();
			Thread.Sleep(5);
		}
		Application.DoEvents();
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
			simRailSnapshot.ServerCode = "test180";
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
			engine.SimRail = simRailSession;
			engine.User = store.State.Users.First((LocalUser x) => x.Id == "DEMO");
			Engine engine2 = engine;
			engine2.ApplySimRailSnapshot(simRailSnapshot2);
			engine2.BeginDuty(now.AddMinutes(-20.0));
			string id = engine2.State.Trains.Single().Id;
			engine2.SetTrainTimes(id, now.AddMinutes(-8.0), now.AddMinutes(-7.0));
			Train train = engine2.State.Trains.Single();
			SimRailPoint remote = simRailTrain2.timetable[1];
			JournalEntry entry = engine2.Note(station.Sections.First((Section x) => x.Name == train.To).Id, now.AddMinutes(-7.0), "Poprzedni wpis", "Pociąg");
			entry.TrainId = id;
			entry.Number = train.Number;
			entry.Direction = "DO";
			entry.Track = train.Track;
			entry.Left = train.Departure;
			engine2.SaveJournal(entry);
			MainForm main = new MainForm(engine2);
			try
			{
				System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
				timer.Interval = 10;
				using System.Windows.Forms.Timer timer2 = timer;
				main.Show();
				Application.DoEvents();
				int heartbeat = 0;
				timer2.Tick += (object s, EventArgs e) =>
				{
					heartbeat++;
				};
				timer2.Start();
				List<string> notices = new List<string>();
				bool released = false;
				Writer last = null;
				main.OperatorNoticeSink = (string title, string message) =>
				{
					notices.Add(title + "\n" + message);
					if (title.StartsWith("SEPE"))
					{
						released = !Field<bool>(main, "sendingHours") && Field<CancellationTokenSource>(main, "hoursCancellation") == null && Field<Task>(main, "hoursTask").IsCompleted && last != null && last.Disposed;
					}
				};
				JournalPane journalPane = Field<List<JournalPane>>(main, "panes").Single((JournalPane x) => x.SectionId == entry.SectionId);
				DataGridViewRow row = journalPane.Grid.Rows.Cast<DataGridViewRow>().Single((DataGridViewRow x) => ((JournalEntry)x.Tag).Id == entry.Id);
				journalPane.FocusEntry(entry.Id);
				journalPane.Grid.CurrentCell = row.Cells["left"];
				journalPane.Grid.BeginEdit(selectAll: true);
				((TextBox)journalPane.Grid.EditingControl).Text = "11:51";
				Check(journalPane.Grid.EndEdit() && journalPane.CommitPending(), "Odrzucony odjazd nie pozostawia walidacji blokującej opuszczenie edytora.");
				Pump(() => notices.Count == 1);
				DateTime? departure = engine2.State.Trains.Single().Departure;
				DateTime dateTime = now.AddMinutes(-7.0);
				Check(departure.HasValue && departure.GetValueOrDefault() == dateTime && Convert.ToString(row.Cells["left"].Value) == "11:53" && !Field<bool>(journalPane, "dirty"), "Cofnięto błędną edycję do poprzedniej godziny bez zmiany zapisanego pociągu.");
				Check(notices[0].Contains("561113") && notices[0].Contains("Godzina odjazdu"), "Zwykły komunikat identyfikuje pociąg i podaje przyczynę błędu.");
				TabControl tabControl = Field<TabControl>(main, "topTabs");
				tabControl.SelectedIndex = 1;
				tabControl.SelectedIndex = 0;
				journalPane.Grid.CurrentCell = row.Cells["notes"];
				row.Cells["notes"].Value = "Można dalej edytować";
				Check(journalPane.CommitPending() && engine2.State.Entries.Single((JournalEntry x) => x.Id == entry.Id).Text == "Można dalej edytować", "Po cofnięciu działają zakładki i kolejna poprawna edycja.");
				journalPane.Grid.CurrentCell = row.Cells["left"];
				row.Cells["left"].Value = "11:50";
				DataGridViewRow dataGridViewRow = journalPane.Grid.Rows.Cast<DataGridViewRow>().First((DataGridViewRow x) => x != row);
				journalPane.Grid.CurrentCell = dataGridViewRow.Cells["notes"];
				Pump(() => notices.Count == 2);
				Check(journalPane.Grid.CurrentRow == dataGridViewRow && !Field<bool>(journalPane, "dirty") && Convert.ToString(row.Cells["left"].Value) == "11:53", "RowValidating po cofnięciu pozwala przejść do innego wiersza.");
				simRailSession.SetAccessLink("https://api.simrail.eu:8093/?serverCode=test180&stationId=11&key=TEST_ONLY_180");
				Set(main, "hoursPaused", true);
				main.HoursWriterFactory = () =>
				{
					last = new Writer
					{
						Action = (SepeWriteRequest r) =>
						{
							throw new InvalidOperationException("Test błędu SEPE key=PRIVATE_180");
						}
					};
					return last;
				};
				Call(main, "RetrySepeWrites");
				Pump(() => notices.Count == 3);
				Check(released, "Stan wysyłania, token i transport są zwolnione, a zadanie zakończone przed pokazaniem komunikatu.");
				Check(notices[2].Contains("SEPE - pociąg 561113") && notices[2].Contains("Posterunek: B") && notices[2].Contains("11:52") && notices[2].Contains("11:53") && !notices[2].Contains("PRIVATE_180"), "Komunikat SEPE podaje numer, posterunek, obie godziny i maskuje klucz.");
				Check(Field<Queue<SepeWriteRequest>>(main, "outgoingHours").Count == 1 && Field<bool>(main, "hoursPaused") && engine2.State.Trains.Single().ExternalManualTimes, "Nieudany zapis jest zatrzymany, a poprawne lokalne godziny pozostają do ponowienia.");
				Check(Field<ToolStripMenuItem>(main, "simulatorState").OwnerItem.Text.Contains("561113") && Field<string>(main, "lastSepeError").Contains("Pociąg: 561113"), "Pociąg widać też w menu SimRail, a szczegóły pozostają w ostatnim raporcie.");
				Check(!Application.OpenForms.OfType<SepeErrorForm>().Any(), "Automatyczny błąd nie otwiera rozbudowanego modalnego raportu.");
				int before = heartbeat;
				main.Size = new Size(1100, 700);
				tabControl.SelectedIndex = 2;
				tabControl.SelectedIndex = 0;
				Pump(() => heartbeat > before);
				Check(main.Enabled && tabControl.SelectedIndex == 0, "Po błędzie działa pompa komunikatów, resize i zakładki.");
				main.HoursWriterFactory = () => new Writer
				{
					SubmissionAttempted = true,
					Action = (SepeWriteRequest r) =>
					{
						remote.isConfirmed = true;
						remote.actualArrivalTime = r.Arrival.Value.ToString("yyyy-MM-dd HH:mm:ss");
						remote.actualDepartureTime = r.Departure.Value.ToString("yyyy-MM-dd HH:mm:ss");
						return Task.FromResult(0);
					}
				};
				Call(main, "RetrySepeWrites");
				Pump(() => !Field<bool>(main, "sendingHours"));
				Check(notices.Count == 3 && !engine2.State.Trains.Single().ExternalManualTimes, "Ręczne ponowienie działa po komunikacie i nie pokazuje kolejnego błędu po sukcesie.");
				main.Close();
			}
			finally
			{
				if (main != null)
				{
					((IDisposable)main).Dispose();
				}
			}
			Check(!Directory.Exists(store.DirectoryPath), "Test działa lokalnie bez sieci i zapisu sesji.");
			log.Add("ALL OFFLINE ERRORS180 TESTS PASSED");
			return true;
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			return false;
		}
		finally
		{
			File.WriteAllLines(Path.Combine(folder, "wyniki-bledy180.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

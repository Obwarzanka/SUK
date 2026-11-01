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

public static class Windows188Tests
{
	private sealed class Feed : ISimulatorFeed
	{
		public SimRailSnapshot Value;

		public int Reads;

		public string ProviderId => "fixture188";

		public Task<SimRailSnapshot> SnapshotAsync(string server, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			Reads++;
			return Task.FromResult(Value);
		}

		public Task<List<SimRailServer>> ServersAsync(CancellationToken token)
		{
			throw new InvalidOperationException("Bez sieci");
		}

		public Task<List<string>> StationsAsync(string server, CancellationToken token)
		{
			throw new InvalidOperationException("Bez sieci");
		}
	}

	private sealed class Writer : ISepeHoursWriter, IDisposable
	{
		private readonly Feed feed;

		private readonly string point;

		public int Writes;

		public bool SubmissionAttempted => Writes > 0;

		public Writer(Feed value, string id)
		{
			feed = value;
			point = id;
		}

		public Task Send(SepeWriteRequest request)
		{
			Writes++;
			SimRailPoint simRailPoint = feed.Value.Trains.Single().timetable.Single((SimRailPoint x) => x.pointId == point);
			simRailPoint.isConfirmed = true;
			if (request.ArrivalChanged)
			{
				simRailPoint.actualArrivalTime = (request.Arrival.HasValue ? request.Arrival.Value.ToString("yyyy-MM-dd HH:mm:ss") : null);
			}
			if (request.DepartureChanged)
			{
				simRailPoint.actualDepartureTime = (request.Departure.HasValue ? request.Departure.Value.ToString("yyyy-MM-dd HH:mm:ss") : null);
			}
			return Task.FromResult(result: true);
		}

		public void Dispose()
		{
		}
	}

	private static readonly List<string> log = new List<string>();

	private static readonly DateTime now = new DateTime(2026, 10, 24, 7, 20, 0);

	private static void Check(bool ok, string text)
	{
		if (!ok)
		{
			throw new Exception("FAIL: " + text);
		}
		log.Add("PASS: " + text);
	}

	private static IEnumerable<Control> All(Control root)
	{
		foreach (Control child in root.Controls)
		{
			yield return child;
			foreach (Control item in All(child))
			{
				yield return item;
			}
		}
	}

	private static T Field<T>(object obj, string name)
	{
		return (T)obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
	}

	private static object Call(object obj, string name, params object[] args)
	{
		try
		{
			return obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args);
		}
		catch (TargetInvocationException ex)
		{
			throw ex.InnerException;
		}
	}

	private static void Pump()
	{
		for (int i = 0; i < 5; i++)
		{
			Application.DoEvents();
		}
	}

	private static void Wait(Task task)
	{
		DateTime dateTime = DateTime.UtcNow.AddSeconds(8.0);
		while (!task.IsCompleted && DateTime.UtcNow < dateTime)
		{
			Application.DoEvents();
			Thread.Sleep(3);
		}
		if (!task.IsCompleted)
		{
			throw new Exception("Przekroczono czas lokalnego testu.");
		}
		task.GetAwaiter().GetResult();
		Pump();
	}

	private static Engine Make(string folder, out Feed feed)
	{
		SimRailTrain simRailTrain = new SimRailTrain();
		simRailTrain.trainNoLocal = "47512";
		simRailTrain.trainName = "MPE";
		simRailTrain.startStation = "A";
		simRailTrain.endStation = "D";
		simRailTrain.timetable = new List<SimRailPoint>();
		SimRailTrain simRailTrain2 = simRailTrain;
		for (int i = 0; i < 4; i++)
		{
			simRailTrain2.timetable.Add(new SimRailPoint
			{
				pointId = (10 + i).ToString(),
				nameForPerson = (new string[4] { "A", "B", "C", "D" })[i],
				indexOfPoint = i,
				line = 1,
				track = 1,
				displayedTrainNumber = "47512",
				trainType = "MPE",
				isActive = true,
				arrivalTime = now.Date.AddHours(7.0).AddMinutes(i).ToString("yyyy-MM-dd HH:mm:ss"),
				departureTime = now.Date.AddHours(7.0).AddMinutes(i + 1).ToString("yyyy-MM-dd HH:mm:ss")
			});
		}
		SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
		simRailSnapshot.ServerCode = "fixture188";
		simRailSnapshot.Trains = new List<SimRailTrain> { simRailTrain2 };
		simRailSnapshot.DispatcherNames = new HashSet<string> { "A", "B", "C", "D" };
		simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
		simRailSnapshot.GameTime = now;
		SimRailSnapshot simRailSnapshot2 = simRailSnapshot;
		feed = new Feed
		{
			Value = simRailSnapshot2
		};
		SimRailSession simRailSession = new SimRailSession(feed, simRailSnapshot2, "11");
		simRailSession.SetControlledPoints(new string[2] { "11", "12" });
		simRailSession.SetAccessLink("https://api.simrail.eu:8093/?serverCode=fixture188&stationId=11,12&key=TEST_ONLY_188");
		Station station = simRailSession.SuggestStation();
		StationSettings.Normalize(station);
		station.Tracks = new List<string> { "1", "2" };
		station.TrackSettings = new List<StationTrackSetting>
		{
			new StationTrackSetting
			{
				Number = "1",
				Visible = true
			},
			new StationTrackSetting
			{
				Number = "2",
				Visible = true
			}
		};
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
		engine.User = store.State.Users.First((LocalUser x) => x.Id == "DEMO");
		engine.SimRail = simRailSession;
		Engine engine2 = engine;
		engine2.EnsureControlledStations();
		engine2.ApplySimRailSnapshot(simRailSnapshot2);
		engine2.BeginDuty(now.AddHours(-1.0));
		engine2.SetUnitCaption("ISE testowa");
		return engine2;
	}

	private static MainForm Open(MainForm main)
	{
		ToolStripMenuItem toolStripMenuItem = main.MainMenuStrip.Items.OfType<ToolStripMenuItem>().Single((ToolStripMenuItem x) => x.Text == "Widok");
		toolStripMenuItem.DropDownItems.OfType<ToolStripMenuItem>().Single((ToolStripMenuItem x) => x.Text == "Otwórz drugie okno").PerformClick();
		Pump();
		return Application.OpenForms.OfType<MainForm>().Last((MainForm x) => x != main);
	}

	private static JournalPane Pane(MainForm main, JournalEntry entry)
	{
		return Field<List<JournalPane>>(main, "panes").Single((JournalPane x) => x.SectionId == entry.SectionId);
	}

	private static DataGridViewRow Row(JournalPane pane, JournalEntry entry)
	{
		return pane.Grid.Rows.Cast<DataGridViewRow>().Single((DataGridViewRow x) => ((JournalEntry)x.Tag).Id == entry.Id);
	}

	private static JournalEntry Entry(Engine engine)
	{
		JournalEntry journalEntry = engine.Note(engine.Station.Sections.First().Id, engine.Now, "", "Pociąg");
		journalEntry.Direction = "Z";
		journalEntry.Number = "47512";
		journalEntry.Track = "1";
		engine.SaveJournal(journalEntry);
		return journalEntry;
	}

	private static void Context(string folder)
	{
		MainForm root = new MainForm(Make(folder, out var _));
		MainForm child = null;
		Exception failure = null;
		int step = 0;
		System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer
		{
			Interval = 70
		};
		try
		{
			timer.Tick += (object s, EventArgs e) =>
			{
				try
				{
					if (step == 0)
					{
						step++;
						timer.Stop();
						child = Open(root);
						timer.Start();
					}
					else if (step == 1)
					{
						step++;
						timer.Stop();
						root.Close();
						timer.Start();
					}
					else
					{
						Check(root.IsDisposed && child != null && !child.IsDisposed, "Zamknięcie pierwszego okna nie kończy pętli aplikacji, gdy drugie jest otwarte.");
						timer.Stop();
						child.Close();
						step++;
					}
				}
				catch (Exception ex)
				{
					failure = ex;
					timer.Stop();
					if (child != null)
					{
						child.Close();
					}
					root.Close();
				}
			};
			timer.Start();
			SessionWindows.Run(root);
		}
		finally
		{
			if (timer != null)
			{
				((IDisposable)timer).Dispose();
			}
		}
		if (failure != null)
		{
			throw failure;
		}
		Check(step == 3 && child.IsDisposed, "Aplikacja kończy wspólną sesję dopiero po zamknięciu ostatniego okna.");
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		bool enabled = LocalPersistence.Enabled;
		LocalPersistence.Enabled = false;
		try
		{
			Engine engine = Make(folder, out var feed);
			using (MainForm mainForm = new MainForm(engine))
			{
				mainForm.hoursPaused = true;
				mainForm.Show();
				Pump();
				Field<TabControl>(mainForm, "topTabs").SelectedIndex = 2;
				All(Field<Control>(mainForm, "filterPanel")).OfType<NumericUpDown>().Single().Value = 5m;
				All(Field<Control>(mainForm, "filterPanel")).OfType<CheckBox>().Single((CheckBox x) => x.Text == "Prezentuj daty").Checked = false;
				JournalEntry entry = Entry(engine);
				mainForm.RefreshData();
				Pump();
				MainForm child = Open(mainForm);
				try
				{
					child.hoursPaused = true;
					Check(!object.ReferenceEquals(mainForm.Engine, child.Engine) && object.ReferenceEquals(mainForm.Engine.Store, child.Engine.Store), "Drugie okno ma własny widok i ten sam magazyn danych sesji.");
					Check(child.Engine.SimRail.AccessUrl == mainForm.Engine.SimRail.AccessUrl && (from x in child.Engine.SimRail.ControlledLocations()
						select x.Id).SequenceEqual(from x in mainForm.Engine.SimRail.ControlledLocations()
						select x.Id), "Link SEPE i komplet wybranych posterunków są od razu wspólne.");
					Check(child.Engine.LoggedInAt == mainForm.Engine.LoggedInAt && child.Engine.OrganizationCaption == "ISE testowa" && child.Engine.CanWrite, "Nowe okno zachowuje użytkownika, czas logowania, jednostkę i przyjęty dyżur.");
					Check(Field<TabControl>(child, "topTabs").SelectedIndex == 2, "Nowe okno otwiera tę samą zakładkę, bez ekranu połączenia.");
					Check(All(Field<Control>(child, "filterPanel")).OfType<NumericUpDown>().Single().Value == 5m && !All(Field<Control>(child, "filterPanel")).OfType<CheckBox>().Single((CheckBox x) => x.Text == "Prezentuj daty").Checked, "Drugie okno kopiuje widoczne ustawienia odświeżania i prezentacji dat.");
					Field<TabControl>(child, "topTabs").SelectedIndex = 0;
					Check(Field<TabControl>(mainForm, "topTabs").SelectedIndex == 2, "Wybór zakładek jest niezależny w obu oknach.");
					Check(Row(Pane(child, entry), entry) != null, "Już zapisany dziennik jest od razu widoczny w nowym oknie.");
					JournalPane journalPane = Pane(mainForm, entry);
					journalPane.FocusEntry(entry.Id);
					DataGridView grid = journalPane.Grid;
					grid.CurrentCell = Row(journalPane, entry).Cells["notes"];
					grid.BeginEdit(selectAll: true);
					((TextBox)grid.EditingControl).Text = "Uwagi z pierwszego okna";
					JournalEntry journalEntry = Json.Clone(child.Engine.State.Entries.Single((JournalEntry x) => x.Id == entry.Id));
					journalEntry.Permission = now.AddMinutes(-8.0);
					child.Engine.SaveJournal(journalEntry);
					Pump();
					Check(((TextBox)grid.EditingControl).Text == "Uwagi z pierwszego okna", "Zmiana w drugim oknie nie usuwa niedokończonej edycji pierwszego.");
					Check(grid.EndEdit() && journalPane.CommitPending(), "Szkic pierwszego okna można zatwierdzić po zmianie w drugim.");
					Pump();
					JournalEntry journalEntry2 = engine.State.Entries.Single((JournalEntry x) => x.Id == entry.Id);
					Check(journalEntry2.Text == "Uwagi z pierwszego okna" && journalEntry2.Permission == now.AddMinutes(-8.0), "Zapis scala różne pola i nie cofa zmiany z drugiego okna.");
					Check(Convert.ToString(Row(Pane(child, entry), entry).Cells["notes"].Value) == journalEntry2.Text, "Zatwierdzona edycja odświeża drugi widok.");
					string station = engine.StationId;
					Field<ComboBox>(child, "stations").SelectedIndex = engine.State.Stations.FindIndex((Station x) => x.SimRailPointId == "12");
					Pump();
					Check(mainForm.Engine.StationId == station && mainForm.Engine.SimRail.PointId == "11" && child.Engine.SimRail.PointId == "12", "W drugim oknie można wybrać inny posterunek bez przestawiania pierwszego.");
					Wait((Task)Call(mainForm, "ReplaceSepeLink", "https://api.simrail.eu:8093/?serverCode=fixture188&stationId=11,12&key=UPDATED_TEST_ONLY_188"));
					Check(child.Engine.SimRail.HasWriteLink && mainForm.Engine.SimRail.AccessUrl.Query.Contains("UPDATED_TEST_ONLY_188") && child.Engine.SimRail.AccessUrl.Query.Contains("UPDATED_TEST_ONLY_188"), "Zmiana linku jest widoczna również w oknie innego posterunku.");
					SimRailSnapshot simRailSnapshot = Json.Clone(feed.Value);
					simRailSnapshot.GameTime = now.AddMinutes(1.0);
					simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
					child.Engine.ApplySimRailSnapshot(simRailSnapshot);
					Pump();
					Check(object.ReferenceEquals(mainForm.Engine.SimRail.Snapshot, child.Engine.SimRail.Snapshot) && Math.Abs((mainForm.Engine.Now - child.Engine.Now).TotalMilliseconds) < 50.0, "Odświeżenie danych i zegar są wspólne dla obu okien.");
					child.Engine.BeginDuty(now.AddMinutes(-30.0));
					Train train = child.Engine.State.Trains.Single((Train x) => x.StationId == child.Engine.StationId);
					Writer writer = new Writer(feed, "12");
					child.HoursWriterFactory = () => writer;
					child.hoursPaused = false;
					mainForm.sendingHours = true;
					child.Engine.SetTrainTimes(train.Id, now.AddMinutes(-5.0), null);
					Pump();
					Check(writer.Writes == 0 && child.outgoingHours.Count == 1, "Wpis z drugiego okna czeka, gdy inne okno wykonuje operację SEPE.");
					mainForm.sendingHours = false;
					child.BeginInvoke(new Action(child.ProcessSepeWrites));
					Pump();
					Wait(child.hoursTask);
					Check(writer.Writes == 1 && child.outgoingHours.Count == 0, "Po zwolnieniu operacji żądanie jest wysłane raz przez lokalny transport testowy.");
					child.hoursPaused = true;
					Field<ComboBox>(child, "stations").SelectedIndex = engine.State.Stations.FindIndex((Station x) => x.Id == station);
					Pump();
					SepeWriteRequest pending = SepeWriteRequest.From(engine.State.Trains.Single((Train x) => x.StationId == station), arrival: true, departure: false);
					mainForm.outgoingHours.Enqueue(pending);
					mainForm.Close();
					Pump();
					Check(!child.IsDisposed && child.outgoingHours.Any((SepeWriteRequest x) => x.TrainId == pending.TrainId), "Zamknięcie pierwszego widoku przekazuje oczekujące żądania drugiemu.");
					Check(child.Engine.State.Entries.Any((JournalEntry x) => x.Id == entry.Id) && child.Engine.SimRail.HasWriteLink, "Dziennik i link pozostają dostępne po zamknięciu pierwszego widoku.");
					JournalEntry journalEntry3 = Json.Clone(child.Engine.State.Entries.Single((JournalEntry x) => x.Id == entry.Id));
					journalEntry3.Text = "Po zamknięciu pierwszego";
					child.Engine.SaveJournal(journalEntry3);
					Pump();
					Check(Convert.ToString(Row(Pane(child, entry), entry).Cells["notes"].Value) == journalEntry3.Text, "Odświeżanie nadal działa po zamknięciu pierwszego widoku z oczekującą aktualizacją.");
					child.Close();
					Pump();
				}
				finally
				{
					if (child != null)
					{
						((IDisposable)child).Dispose();
					}
				}
			}
			Context(folder);
			log.Add("Bez sieci i bez zapisów w żywym SEPE. Użyto wyłącznie lokalnego transportu i fikcyjnych kluczy.");
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			File.WriteAllLines(Path.Combine(folder, "wyniki-okna188.txt"), log, Encoding.UTF8);
			MainForm[] array = Application.OpenForms.OfType<MainForm>().ToArray();
			foreach (MainForm mainForm2 in array)
			{
				try
				{
					mainForm2.Dispose();
				}
				catch
				{
				}
			}
			return false;
		}
		finally
		{
			LocalPersistence.Enabled = enabled;
		}
		File.WriteAllLines(Path.Combine(folder, "wyniki-okna188.txt"), log, Encoding.UTF8);
		return true;
	}
}

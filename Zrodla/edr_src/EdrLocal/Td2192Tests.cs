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

public static class Td2192Tests
{
	private sealed class Feed : ISimulatorFeed
	{
		public SimRailSnapshot Value;

		public string ProviderId => "SimRail";

		public Task<List<SimRailServer>> ServersAsync(CancellationToken token)
		{
			return Task.FromResult(new List<SimRailServer>());
		}

		public Task<SimRailSnapshot> SnapshotAsync(string server, CancellationToken token)
		{
			return Task.FromResult(Value);
		}
	}

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

	private static IEnumerable<Control> All(Control root)
	{
		yield return root;
		foreach (Control child in root.Controls)
		{
			foreach (Control item in All(child))
			{
				yield return item;
			}
		}
	}

	private static void Pump()
	{
		for (int i = 0; i < 4; i++)
		{
			Application.DoEvents();
		}
	}

	private static Td2Schedule Schedule(int id, string inner = "OG-OW")
	{
		string[] array = new string[4] { "Łapanów", "Okoń Główny", "Okoń Wschodni", "Ligota Trzeszcze" };
		string[] array2 = new string[4] { null, "Gw", inner, "Lg" };
		string[] array3 = new string[4] { "Lw", inner, "We", null };
		List<Td2Stop> list = new List<Td2Stop>();
		for (int i = 0; i < 4; i++)
		{
			string text = new DateTime(2026, 10, 24, 7, 0, 0).AddMinutes(i * 2).ToString("yyyy-MM-ddTHH:mm:ssZ");
			list.Add(new Td2Stop
			{
				pointId = (101 + i).ToString(),
				entryId = id * 100 + i,
				pointNameRAW = array[i],
				arrivalLine = array2[i],
				departureLine = array3[i],
				arrivalTime = ((i == 0) ? null : text),
				departureTime = ((i == 3) ? null : text),
				arrivalRealTime = ((i == 0) ? null : text),
				departureRealTime = ((i == 3) ? null : text),
				confirmed = 1,
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
		td2Schedule.timetablePath = ",Łapanów aaaaaaaa.sc,Lw;Gw,Okoń cccccccc.sc,We;Lg,Ligota bbbbbbbb.sc,";
		td2Schedule.stopPoints = list;
		return td2Schedule;
	}

	private static SimRailSnapshot Snapshot(Td2Provider provider, params Td2Schedule[] schedules)
	{
		return provider.Accept(Json.Bytes(new Td2Envelope
		{
			success = true,
			respCode = 10,
			message = schedules.ToList()
		}), "eu", DateTimeOffset.UtcNow);
	}

	private static Engine Create(string folder, ISimulatorFeed provider, SimRailSnapshot snapshot, string point = "102")
	{
		SimRailSession simRailSession = new SimRailSession(provider, snapshot, point);
		Station station = simRailSession.SuggestStation();
		station.Tracks = new List<string> { "1" };
		StationSettings.Normalize(station);
		station.SimulatorReviewed = true;
		foreach (Section section in station.Sections)
		{
			section.Basis = "Jednoodstępowa (półsamoczynna) blokada liniowa";
			foreach (LineTrackSetting lineTrack in section.LineTracks)
			{
				string basis = (lineTrack.BasisOut = section.Basis);
				lineTrack.Basis = basis;
			}
		}
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

	private static void NoRange(MainForm main)
	{
		main.GetType().GetField("useTimeRange", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(main, false);
		main.RefreshData();
		Pump();
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		bool enabled = LocalPersistence.Enabled;
		LocalPersistence.Enabled = false;
		try
		{
			Td2Provider provider = new Td2Provider();
			Td2Schedule td2Schedule = Schedule(1, "LG-LT");
			Td2Schedule td2Schedule2 = Schedule(2, "LG-LT (2)");
			Td2Stop td2Stop = td2Schedule.stopPoints[2];
			string pointNameRAW = (td2Schedule2.stopPoints[2].pointNameRAW = "Ligota Trzeszcze");
			td2Stop.pointNameRAW = pointNameRAW;
			SimRailSnapshot snapshot = Snapshot(provider, td2Schedule, td2Schedule2);
			Engine engine = Create(folder, provider, snapshot);
			List<Section> list = engine.Station.Sections.Where((Section x) => x.Td2Kind == "Wewnętrzny").ToList();
			Check(list.Count == 2 && list.Select((Section x) => x.Td2RouteKey).Distinct().Count() == 2, "Równoległe LG-LT i LG-LT (2) mają odrębne klucze mimo wspólnego sąsiada.");
			Check(list.All((Section x) => StationSettings.Title(x) == "Ligota Trzeszcze" && x.Line == ""), "Wewnętrzne dzienniki domyślnie mają nazwę posterunku, bez wymyślonego numeru linii.");
			Train train = engine.State.Trains.Single((Train x) => x.ExternalTrainNo == "1");
			Check(TrainDirections.Text(engine, train, "Z") == "Łapanów (Lw/Gw)" && TrainDirections.Text(engine, train, "DO") == "Ligota Trzeszcze", "Zewnętrzne oznaczenia są przy posterunku; wewnętrzny kierunek domyślnie pokazuje nazwę sąsiada.");
			ParallelEditor(engine, folder);
			train = engine.State.Trains.Single((Train x) => x.ExternalTrainNo == "1");
			Train train2 = engine.State.Trains.Single((Train x) => x.ExternalTrainNo == "2");
			Check(SectionRouting.Find(engine.Station.Sections, train, "DO").JournalName == "Ligota — linia 1" && SectionRouting.Find(engine.Station.Sections, train2, "DO").JournalName == "Ligota — linia 2", "Własne nazwy dzienników nie zmieniają powiązań równoległych szlaków z API.");
			Check(train.LineOut == "1" && train2.LineOut == "2" && TrainDirections.Lines(train) == "1", "Kolumna Nr L. korzysta z przypisanych linii zamiast oznaczeń szlaków TD2.");
			Check(TrainDirections.Text(engine, train, "DO") == "LG-LT" && TrainDirections.Text(engine, train2, "DO") == "Ligota Trzeszcze", "Opcję nazwy posterunku można wyłączyć dla jednego szlaku, zachowując drugi.");
			Dictionary<string, string> ids = engine.Station.Sections.ToDictionary((Section x) => x.Td2RouteKey, (Section x) => x.Id);
			engine.ApplySimRailSnapshot(snapshot);
			Check(engine.Station.Sections.All((Section x) => ids[x.Td2RouteKey] == x.Id) && engine.Station.Sections.Single((Section x) => x.Td2RouteKey == "I:LG-LT").Line == "1" && engine.Station.Sections.Single((Section x) => x.Td2RouteKey == "I:LG-LT").JournalName == "Ligota — linia 1", "Odświeżenie zachowuje nazwy, linie, ustawienia i identyfikatory dzienników.");
			using (MainForm mainForm = new MainForm(engine))
			{
				mainForm.Show();
				Pump();
				NoRange(mainForm);
				DataGridView dataGridView = Field<DataGridView>(mainForm, "trains");
				DataGridViewRow dataGridViewRow = dataGridView.Rows.Cast<DataGridViewRow>().Single((DataGridViewRow x) => (string)x.Tag == train.Id);
				Check(Convert.ToString(dataGridViewRow.Cells["from"].Value) == "Łapanów (Lw/Gw)" && Convert.ToString(dataGridViewRow.Cells["to"].Value) == "LG-LT", "Wykaz WinForms wyświetla ustawienia kierunków dla konkretnego szlaku.");
				CheckBox checkBox = All(mainForm).OfType<CheckBox>().Single((CheckBox x) => x.Text == "Pokaż szlaki");
				checkBox.Checked = false;
				Check(!TrainDirections.Text(engine, train, "Z").Contains("("), "Pokaż szlaki wyłącza dopiski oznaczeń przy zewnętrznych posterunkach.");
				checkBox.Checked = true;
				Ui.Capture(mainForm, Path.Combine(folder, "td2-rownolegle-dzienniki.png"));
				mainForm.Close();
				Pump();
			}
			Grouping(folder);
			Endpoints(folder);
			Profiles(engine, folder);
			Live(folder);
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			File.WriteAllLines(Path.Combine(folder, "wyniki-td2192.txt"), log, Encoding.UTF8);
			return false;
		}
		finally
		{
			LocalPersistence.Enabled = enabled;
		}
		File.WriteAllLines(Path.Combine(folder, "wyniki-td2192.txt"), log, Encoding.UTF8);
		return true;
	}

	private static void ParallelEditor(Engine engine, string folder)
	{
		using StationConfigurationForm stationConfigurationForm = new StationConfigurationForm(engine);
		stationConfigurationForm.Show();
		Pump();
		ConfigurationGrid configurationGrid = Field<ConfigurationGrid>(stationConfigurationForm, "sectionGrid");
		string[] array = new string[2] { "I:LG-LT", "I:LG-LT (2)" };
		foreach (string key in array)
		{
			List<Section> sections = engine.Station.Sections;
			Func<Section, bool> predicate = (Section x) => x.Td2RouteKey == key;
			Section section = sections.Single(predicate);
			int index = configurationGrid.Rows.Cast<DataGridViewRow>().ToList().FindIndex((DataGridViewRow x) => ((Section)x.DataBoundItem).Id == section.Id);
			configurationGrid.CurrentCell = configurationGrid.Rows[index].Cells[0];
			bool touched = false;
			Exception failure = null;
			System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer
			{
				Interval = 40
			};
			try
			{
				timer.Tick += (object s, EventArgs e) =>
				{
					Editor editor = Application.OpenForms.OfType<Editor>().FirstOrDefault((Editor x) => x.Text == "Ustawienia szlaku i dziennika TD2");
					if (editor == null)
					{
						return;
					}
					timer.Stop();
					try
					{
						TextBox textBox = All(editor).OfType<TextBox>().Single((TextBox x) => x.Name == "td2SourceLine");
						TextBox textBox2 = All(editor).OfType<TextBox>().Single((TextBox x) => x.Name == "td2AssignedLine");
						TextBox textBox3 = All(editor).OfType<TextBox>().Single((TextBox x) => x.Name == "td2JournalName");
						CheckBox checkBox = All(editor).OfType<CheckBox>().Single((CheckBox x) => x.Name == "td2UsePeerName");
						Check(textBox.ReadOnly && textBox.Text == ((key == "I:LG-LT") ? "LG-LT" : "LG-LT (2)"), "Edytor zachowuje źródłowe oznaczenie równoległego szlaku " + textBox.Text + ".");
						textBox2.Text = ((key == "I:LG-LT") ? "1" : "2");
						checkBox.Checked = key != "I:LG-LT";
						textBox3.Text = "Ligota — linia " + textBox2.Text;
						touched = true;
						if (key == "I:LG-LT")
						{
							Ui.Capture(editor, Path.Combine(folder, "td2-ustawienia-szlaku.png"));
						}
						All(editor).OfType<Button>().Single((Button x) => x.Text == "Zapisz").PerformClick();
					}
					catch (Exception ex)
					{
						failure = ex;
						editor.Close();
					}
				};
				timer.Start();
				Call(stationConfigurationForm, "EditSection", false);
				timer.Stop();
				if (failure != null)
				{
					throw failure;
				}
			}
			finally
			{
				if (timer != null)
				{
					((IDisposable)timer).Dispose();
				}
			}
			Check(touched, "Nazwa dziennika i linia zostały ustawione w rzeczywistym oknie edycji.");
		}
		Ui.Capture(stationConfigurationForm, Path.Combine(folder, "td2-konfiguracja-linii.png"));
		stationConfigurationForm.SaveDraft();
		Pump();
	}

	private static void Grouping(string folder)
	{
		Td2Provider provider = new Td2Provider();
		Td2Schedule td2Schedule = Schedule(10);
		SimRailSnapshot snapshot = Snapshot(provider, td2Schedule);
		Engine engine = Create(folder, provider, snapshot);
		Train train = engine.State.Trains.Single();
		string id = train.Id;
		Check(train.To == "Okoń Wschodni", "Bez ręcznego połączenia Okoń Wschodni pozostaje osobnym posterunkiem.");
		JournalEntry originalArrival = engine.RegisterScheduledTrain(id, null);
		JournalEntry originalDeparture = engine.RegisterScheduledTrain(id, null);
		string oldInnerId = originalDeparture.SectionId;
		using (StationConfigurationForm stationConfigurationForm = new StationConfigurationForm(engine))
		{
			stationConfigurationForm.Show();
			Pump();
			CheckedListBox checkedListBox = Field<CheckedListBox>(stationConfigurationForm, "td2AreaList");
			int num = checkedListBox.Items.Cast<Td2AreaPost>().ToList().FindIndex((Td2AreaPost x) => x.Id == "103");
			Check(num >= 0 && !checkedListBox.GetItemChecked(num), "Obszar posterunku proponuje części tej samej scenerii bez automatycznego łączenia po nazwie.");
			checkedListBox.SetItemChecked(num, value: true);
			Call(stationConfigurationForm, "ApplyTd2Area");
			All(stationConfigurationForm).OfType<TabControl>().First().SelectedTab = All(stationConfigurationForm).OfType<TabPage>().Single((TabPage x) => x.Text == "Obszar posterunku");
			Ui.Capture(stationConfigurationForm, Path.Combine(folder, "td2-obszar-posterunku.png"));
			stationConfigurationForm.SaveDraft();
			Pump();
		}
		train = engine.State.Trains.Single((Train x) => x.Id == id);
		Check(train.From == "Łapanów" && train.To == "Ligota" && train.Td2OutgoingKey == "E:WE", "Po włączeniu części stacji kierunek wychodzi poza cały obszar na zewnętrzny We.");
		int ok;
		if (Enumerable.Count(engine.State.Trains, (Train x) => !x.Td2OutsideArea) == 1)
		{
			int? td2FirstPointIndex = train.Td2FirstPointIndex;
			if (td2FirstPointIndex == 1 && td2FirstPointIndex.HasValue && train.Td2LastPointIndex == 2)
			{
				ok = ((train.Id == id) ? 1 : 0);
				goto IL_029f;
			}
		}
		ok = 0;
		goto IL_029f;
		IL_029f:
		Check((byte)ok != 0, "Wizyty głównej i części stacji są jednym wpisem rozkładu ze stabilnym identyfikatorem.");
		DateTime planArrival = train.PlanArrival;
		DateTime? dateTime = Td2Provider.Time(td2Schedule.stopPoints[1].arrivalTime);
		int ok2;
		if (planArrival == dateTime)
		{
			DateTime planDeparture = train.PlanDeparture;
			DateTime? dateTime2 = Td2Provider.Time(td2Schedule.stopPoints[2].departureTime);
			ok2 = ((planDeparture == dateTime2) ? 1 : 0);
		}
		else
		{
			ok2 = 0;
		}
		Check((byte)ok2 != 0, "Przyjazd obejmuje początek obszaru, a odjazd jego koniec.");
		Check(engine.State.Entries.Any((JournalEntry x) => x.Id == originalArrival.Id) && engine.State.Entries.Any((JournalEntry x) => x.Id == originalDeparture.Id) && engine.Station.Sections.Single((Section x) => x.Id == oldInnerId).Td2AreaHidden, "Łączenie nie usuwa dawnych wpisów; wewnętrzny dziennik pozostaje zachowany poza aktywnym układem.");
		Check(SectionRouting.Find(engine.Station.Sections, train, "DO").Td2LocalLine == "We", "Nowe wyprawienie po połączeniu kieruje do właściwego zewnętrznego dziennika.");
		Td2Schedule td2Schedule2 = Schedule(11);
		td2Schedule2.stopPoints.RemoveAt(1);
		engine.ApplySimRailSnapshot(Snapshot(provider, td2Schedule, td2Schedule2));
		Check(engine.State.Trains.Any((Train x) => x.ExternalTrainNo == "11" && !x.Td2OutsideArea), "Rozkład przechodzący wyłącznie przez włączoną część stacji również pojawia się w jej obszarze.");
		Td2Schedule td2Schedule3 = Schedule(12);
		td2Schedule3.stopPoints.Insert(2, new Td2Stop
		{
			pointId = "999",
			entryId = 1299,
			pointNameRAW = "Samodzielny posterunek",
			arrivalTime = "2026-10-24T07:03:00Z",
			departureTime = "2026-10-24T07:03:00Z",
			arrivalLine = "X",
			departureLine = "Y",
			confirmed = 1
		});
		Check(Td2Area.Visits(td2Schedule3, engine.Station, "102").Count == 2, "Wizyty rozdzielone innym samodzielnym posterunkiem nie są scalane przez cały rozkład.");
		using (StationConfigurationForm stationConfigurationForm2 = new StationConfigurationForm(engine))
		{
			stationConfigurationForm2.Show();
			Pump();
			CheckedListBox checkedListBox2 = Field<CheckedListBox>(stationConfigurationForm2, "td2AreaList");
			int index = checkedListBox2.Items.Cast<Td2AreaPost>().ToList().FindIndex((Td2AreaPost x) => x.Id == "103");
			checkedListBox2.SetItemChecked(index, value: false);
			stationConfigurationForm2.SaveDraft();
			Pump();
		}
		Train train2 = engine.State.Trains.Single((Train x) => x.ExternalTrainNo == "11");
		Check(train2.Td2OutsideArea && train2.Runs && !train2.RemovedFromSource, "Wyłączenie części stacji ukrywa jej wizytę bez fałszywego oznaczenia Nie kursuje.");
		Check(!engine.Station.Sections.Single((Section x) => x.Id == oldInnerId).Td2AreaHidden && engine.State.Trains.Single((Train x) => x.Id == id).To == "Okoń Wschodni", "Cofnięcie połączenia przywraca własny wewnętrzny dziennik i sąsiada.");
	}

	private static void Endpoints(string folder)
	{
		Td2Schedule td2Schedule = Schedule(20);
		td2Schedule.stopPoints = td2Schedule.stopPoints.Skip(1).ToList();
		td2Schedule.stopPoints[0].arrivalLine = null;
		Td2Stop td2Stop = td2Schedule.stopPoints[0];
		string arrivalTime = (td2Schedule.stopPoints[0].arrivalRealTime = null);
		td2Stop.arrivalTime = arrivalTime;
		td2Schedule.timetablePath = ",Okoń cccccccc.sc,We;Lg,Ligota bbbbbbbb.sc,";
		Td2Schedule td2Schedule2 = Schedule(21);
		td2Schedule2.stopPoints = td2Schedule2.stopPoints.Take(2).ToList();
		td2Schedule2.stopPoints[1].departureLine = null;
		Td2Stop td2Stop2 = td2Schedule2.stopPoints[1];
		string departureTime = (td2Schedule2.stopPoints[1].departureRealTime = null);
		td2Stop2.departureTime = departureTime;
		td2Schedule2.timetablePath = ",Łapanów aaaaaaaa.sc,Lw;Gw,Okoń cccccccc.sc,";
		Td2Provider provider = new Td2Provider();
		Engine engine = Create(folder, provider, Snapshot(provider, td2Schedule, td2Schedule2));
		Train train = engine.State.Trains.Single((Train x) => x.ExternalTrainNo == "20");
		Train train2 = engine.State.Trains.Single((Train x) => x.ExternalTrainNo == "21");
		Check(train.StartsHere && train2.EndsHere && TrainDirections.Text(engine, train, "Z") == "----------" && TrainDirections.Text(engine, train2, "DO") == "----------", "TD2 pokazuje ---------- dla początku i końca biegu.");
		Check(engine.RegisterScheduledTrain(train.Id, null).Direction == "DO" && engine.RegisterScheduledTrain(train.Id, null) == null, "Pociąg zaczynający bieg ma tylko wyprawienie, bez fikcyjnego przyjazdu z ----------.");
		Check(engine.RegisterScheduledTrain(train2.Id, null).Direction == "Z" && engine.RegisterScheduledTrain(train2.Id, null) == null, "Pociąg kończący bieg ma tylko przyjęcie, bez fikcyjnego wyprawienia do ----------.");
		Feed feed = new Feed();
		feed.Value = new SimRailSnapshot
		{
			ServerCode = "test192",
			GameTime = DateTime.Now,
			DownloadedUtc = DateTime.UtcNow,
			DispatcherNames = new HashSet<string> { "Okoń Główny" },
			Trains = new List<SimRailTrain>()
		};
		bool[] array = new bool[2] { true, false };
		foreach (bool flag in array)
		{
			List<SimRailPoint> list = new List<SimRailPoint>();
			int[] array2 = (flag ? new int[2] { 102, 104 } : new int[2] { 101, 102 });
			for (int num2 = 0; num2 < array2.Length; num2++)
			{
				list.Add(new SimRailPoint
				{
					pointId = array2[num2].ToString(),
					indexOfPoint = num2,
					nameForPerson = ((array2[num2] == 102) ? "Okoń Główny" : "Sąsiedni"),
					line = 1,
					track = 1,
					displayedTrainNumber = (flag ? "100" : "200"),
					trainType = "ROJ",
					arrivalTime = "2026-10-24 09:00:00",
					departureTime = "2026-10-24 09:01:00",
					isActive = true
				});
			}
			feed.Value.Trains.Add(new SimRailTrain
			{
				trainNoLocal = (flag ? "100" : "200"),
				startStation = (flag ? "Okoń Główny" : "Sąsiedni"),
				endStation = (flag ? "Sąsiedni" : "Okoń Główny"),
				timetable = list
			});
		}
		Engine engine2 = Create(folder, feed, feed.Value);
		Check(TrainDirections.Text(engine2, engine2.State.Trains.Single((Train x) => x.Number == "100"), "Z") == "----------" && TrainDirections.Text(engine2, engine2.State.Trains.Single((Train x) => x.Number == "200"), "DO") == "----------", "SimRail korzysta z tego samego oznaczenia początku i końca biegu.");
		SimRailTrain simRailTrain = Json.Clone(feed.Value.Trains.Single((SimRailTrain x) => x.trainNoLocal == "100"));
		simRailTrain.startStation = "Odległa stacja";
		SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
		simRailSnapshot.ServerCode = "partial192";
		simRailSnapshot.GameTime = DateTime.Now;
		simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
		simRailSnapshot.DispatcherNames = new HashSet<string> { "Okoń Główny" };
		simRailSnapshot.Trains = new List<SimRailTrain> { simRailTrain };
		SimRailSnapshot simRailSnapshot2 = simRailSnapshot;
		Feed feed2 = new Feed();
		feed2.Value = simRailSnapshot2;
		SimRailSession simRailSession = new SimRailSession(feed2, simRailSnapshot2, "102");
		Check(!simRailSession.Project(simRailSession.SuggestStation()).Single().StartsHere, "Początek dostępnego fragmentu trasy SR nie jest mylony z początkiem biegu pociągu.");
		Engine[] array3 = new Engine[2] { engine, engine2 };
		foreach (Engine engine3 in array3)
		{
			using MainForm mainForm = new MainForm(engine3);
			mainForm.Show();
			Pump();
			NoRange(mainForm);
			DataGridView dataGridView = Field<DataGridView>(mainForm, "trains");
			Check(dataGridView.Rows.Cast<DataGridViewRow>().Any((DataGridViewRow x) => Convert.ToString(x.Cells["from"].Value) == "----------") && dataGridView.Rows.Cast<DataGridViewRow>().Any((DataGridViewRow x) => Convert.ToString(x.Cells["to"].Value) == "----------"), engine3.SimulatorCaption + ": rzeczywisty wykaz zawiera znaczniki ----------.");
			mainForm.Close();
			Pump();
		}
	}

	private static void Profiles(Engine engine, string folder)
	{
		engine.StationProfileDirectory = Path.Combine(folder, "profiles");
		engine.PersistStationProfile();
		StationProfile stationProfile = Json.Read<StationProfile>(File.ReadAllBytes(Path.Combine(engine.StationProfileDirectory, "102.json")));
		Section section = stationProfile.Station.Sections.Single((Section x) => x.Td2RouteKey == "I:LG-LT");
		Check(section.Td2LocalLine == "LG-LT" && section.JournalName == "Ligota — linia 1" && section.Line == "1" && section.Td2UsePeerName == false, "Zapis profilu zachowuje nazwę, przypisaną linię, źródłowy szlak i wyłączenie nazwy posterunku.");
		Section section2 = new Section();
		section2.Id = "td2-section-old";
		section2.Td2RouteKey = "I:LG-LT (2)";
		section2.Td2Kind = "Wewnętrzny";
		section2.Td2PeerName = "Ligota Trzeszcze";
		section2.Name = "Ligota Trzeszcze";
		section2.Line = "LG-LT (2)";
		section2.JournalName = "LG-LT (2)";
		Section section3 = section2;
		Td2SectionSettings.Normalize(section3);
		Check(section3.Td2LocalLine == "LG-LT (2)" && section3.Line == "" && StationSettings.Title(section3) == "Ligota Trzeszcze", "Profil z poprzedniej wersji przenosi kod API do osobnego pola i dostaje nową domyślną nazwę.");
		Td2Provider provider = new Td2Provider();
		Td2Schedule td2Schedule = Schedule(1, "LG-LT");
		Td2Schedule td2Schedule2 = Schedule(2, "LG-LT (2)");
		Td2Stop td2Stop = td2Schedule.stopPoints[2];
		string pointNameRAW = (td2Schedule2.stopPoints[2].pointNameRAW = "Ligota Trzeszcze");
		td2Stop.pointNameRAW = pointNameRAW;
		Engine restored = Create(folder, provider, Snapshot(provider, td2Schedule, td2Schedule2));
		restored.StationProfileDirectory = engine.StationProfileDirectory;
		restored.Store.Commit((AppState state) =>
		{
			state.Stations.First((Station x) => x.Id == restored.StationId).SimulatorReviewed = false;
			state.Entries.Clear();
			state.Duties.Clear();
		});
		restored.InitializeStationProfiles();
		restored.ApplySimRailSnapshot(restored.SimRail.Snapshot);
		Section section4 = restored.Station.Sections.Single((Section x) => x.Td2RouteKey == "I:LG-LT");
		Check(section4.JournalName == "Ligota — linia 1" && section4.Line == "1" && section4.Td2UsePeerName == false, "Nowa sesja przywraca własną nazwę, linię i ustawienie wyświetlania z profilu.");
		Station station = Json.Clone(stationProfile.Station);
		station.Td2AreaPosts = new List<Td2AreaPost>
		{
			new Td2AreaPost
			{
				Id = "103",
				Name = "Okoń Wschodni"
			}
		};
		Check(Json.Clone(station).Td2AreaPosts.Single().Id == "103", "Ręcznie wybrany obszar posterunku przechodzi przez zapis i odczyt konfiguracji.");
	}

	private static void Live(string folder)
	{
		string path = Path.Combine(folder, "getTimetables.json");
		if (!File.Exists(path))
		{
			return;
		}
		Td2Provider td2Provider = new Td2Provider();
		SimRailSnapshot simRailSnapshot = td2Provider.Accept(File.ReadAllBytes(path), "eu", DateTimeOffset.UtcNow);
		int num = 0;
		bool flag = true;
		foreach (SimRailLocation item in simRailSnapshot.Locations())
		{
			SimRailSession simRailSession = new SimRailSession(td2Provider, simRailSnapshot, item.Id);
			Station station = simRailSession.SuggestStation();
			StationSettings.Normalize(station);
			foreach (Train item2 in simRailSession.Project(station))
			{
				if (!string.IsNullOrEmpty(item2.Td2IncomingKey))
				{
					num++;
					flag &= SectionRouting.Find(station.Sections, item2, "Z") != null;
				}
				if (!string.IsNullOrEmpty(item2.Td2OutgoingKey))
				{
					num++;
					flag &= SectionRouting.Find(station.Sections, item2, "DO") != null;
				}
			}
		}
		SimRailSession simRailSession2 = new SimRailSession(td2Provider, simRailSnapshot, simRailSnapshot.Locations().First().Id);
		Station own = simRailSession2.SuggestStation();
		SimRailLocation simRailLocation = simRailSnapshot.Locations().FirstOrDefault((SimRailLocation x) => !string.IsNullOrEmpty(x.SceneId) && x.SceneId != own.Td2SceneId);
		if (simRailLocation != null)
		{
			own.Td2AreaPosts = new List<Td2AreaPost>
			{
				new Td2AreaPost
				{
					Id = simRailLocation.Id,
					Name = simRailLocation.Name
				}
			};
			bool ok = false;
			try
			{
				Td2Projection.SuggestStation(simRailSession2, own);
			}
			catch (InvalidOperationException)
			{
				ok = true;
			}
			Check(ok, "Obszar posterunku nie łączy części z innej scenerii, nawet jeśli nazwy wyglądają podobnie.");
		}
		Check(flag && num > 0, "Rzeczywiste rozkłady nadal kierują wszystkie ruchy do właściwych dzienników (" + num + ").");
	}
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EdrLocal;

public static class Td2190Tests
{
	private static readonly List<string> log = new List<string>();

	private static readonly DateTimeOffset downloaded = new DateTimeOffset(2026, 10, 23, 22, 30, 0, TimeSpan.Zero);

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

	private static void Rejected(Action action, string text)
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
		Check(ok, text);
	}

	private static void Pump()
	{
		for (int i = 0; i < 4; i++)
		{
			Application.DoEvents();
		}
	}

	private static void Wait(Func<bool> done)
	{
		DateTime dateTime = DateTime.UtcNow.AddSeconds(8.0);
		while (!done() && DateTime.UtcNow < dateTime)
		{
			Application.DoEvents();
			Thread.Sleep(4);
		}
		if (!done())
		{
			throw new Exception("Przekroczono czas testu TD2.");
		}
		Pump();
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

	private static Td2Stop Stop(int index, string name, string a, string d, string incoming, string outgoing)
	{
		Td2Stop td2Stop = new Td2Stop();
		td2Stop.pointId = (101 + index).ToString();
		td2Stop.entryId = 9001 + index;
		td2Stop.pointNameRAW = name;
		td2Stop.pointName = "<strong>" + name + "</strong>";
		td2Stop.arrivalTime = a;
		td2Stop.departureTime = d;
		td2Stop.arrivalRealTime = a;
		td2Stop.departureRealTime = d;
		td2Stop.arrivalLine = incoming;
		td2Stop.departureLine = outgoing;
		td2Stop.confirmed = 1;
		td2Stop.pointStopTime = 0;
		td2Stop.pointStopType = "";
		td2Stop.comments = "";
		return td2Stop;
	}

	private static Td2Schedule Schedule(int id = 1001)
	{
		List<Td2Stop> list = new List<Td2Stop>();
		list.Add(Stop(0, "Alfa", null, "2026-10-23T21:58:00.000Z", null, "A-S"));
		list.Add(Stop(1, "SBL_1, pe", "2026-10-23T22:00:00.000Z", "2026-10-23T22:00:00.000Z", "A-S", "X"));
		list.Add(Stop(2, "Beta", "2026-10-23T22:03:00.000Z", "2026-10-23T22:09:00.000Z", "Y", "B-C"));
		list.Add(Stop(3, "Beta Osobowy, po", "2026-10-23T22:11:00.000Z", "2026-10-23T22:11:00.000Z", "B-C", "B-C"));
		list.Add(Stop(4, "C, podg", "2026-10-23T22:15:00.000Z", "2026-10-23T22:15:00.000Z", "B-C", "B-F"));
		list.Add(Stop(5, "Beta Koniec", "2026-10-23T22:20:00.000Z", null, "B-F", null));
		List<Td2Stop> list2 = list;
		list2[5].confirmed = 0;
		list2[5].arrivalRealTime = null;
		list2[2].comments = "I,2,Uwaga testowa";
		list2[2].confirmed = 0;
		list2[2].arrivalRealTime = "2026-10-23T22:05:00.000Z";
		list2[2].departureRealTime = "2026-10-23T22:10:00.000Z";
		list2[2].pointStopTime = 6;
		list2[2].pointStopType = "ph";
		Td2Schedule td2Schedule = new Td2Schedule();
		td2Schedule.trainInfo = new Td2TrainInfo
		{
			timetableId = id,
			trainNo = 47512,
			trainCategoryCode = "MPE",
			trainMaxSpeed = 120,
			region = "eu",
			driverName = "Maszynista testowy",
			skr = 1,
			twr = 1,
			hasDangerousCargo = 1,
			warningNotes = "Uwaga rozkładowa"
		};
		td2Schedule.timetablePath = ",Alfa aaaaaaaa.sc,X;Y,Beta bbbbbbbb.sc,";
		td2Schedule.stopPoints = list2;
		return td2Schedule;
	}

	private static byte[] Payload(params Td2Schedule[] schedules)
	{
		Td2Envelope td2Envelope = new Td2Envelope();
		td2Envelope.success = true;
		td2Envelope.respCode = 10;
		td2Envelope.message = schedules.ToList();
		return Json.Bytes(td2Envelope);
	}

	private static SimRailSnapshot Accept(Td2Provider provider, byte[] bytes)
	{
		SimRailSnapshot simRailSnapshot = provider.Accept(bytes, "eu", downloaded);
		simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
		return simRailSnapshot;
	}

	private static Engine EngineFor(string folder, Td2Provider provider, SimRailSnapshot snapshot)
	{
		SimRailSession simRailSession = new SimRailSession(provider, snapshot, "103");
		simRailSession.SetControlledPoints(new string[2] { "103", "105" });
		Station station = simRailSession.SuggestStation();
		StationSettings.Normalize(station);
		station.SimulatorReviewed = true;
		foreach (Section section in station.Sections)
		{
			section.ManualConfiguration = true;
			section.Basis = "Jednoodstępowa (półsamoczynna) blokada liniowa";
			foreach (LineTrackSetting lineTrack in section.LineTracks)
			{
				string basis = (lineTrack.BasisOut = "Jednoodstępowa (półsamoczynna) blokada liniowa");
				lineTrack.Basis = basis;
			}
		}
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
		engine2.ApplySimRailSnapshot(snapshot);
		engine2.BeginDuty(engine2.Now.AddHours(-1.0));
		return engine2;
	}

	private static void Live(string folder)
	{
		string path = Path.Combine(folder, "getTimetables.json");
		if (!File.Exists(path))
		{
			return;
		}
		Td2Provider td2Provider = new Td2Provider();
		SimRailSnapshot simRailSnapshot = Accept(td2Provider, File.ReadAllBytes(path));
		Check(simRailSnapshot.Td2.Records.Count > 0 && simRailSnapshot.Locations().Count > 0, "Odczytano rzeczywistą odpowiedź getTimetables z lokalnego pliku.");
		int num = 0;
		foreach (SimRailLocation item in simRailSnapshot.Locations())
		{
			SimRailSession simRailSession = new SimRailSession(td2Provider, simRailSnapshot, item.Id);
			Station station = simRailSession.SuggestStation();
			StationSettings.Normalize(station);
			num += simRailSession.Project(station).Count;
		}
		Check(num > simRailSnapshot.Td2.Records.Count, "Wszystkie rzeczywiste posterunki i wizyty dają się odwzorować bez pobierania pozycji online.");
		log.Add("Rzeczywiste dane: " + simRailSnapshot.Td2.Records.Count + " rozkładów, " + simRailSnapshot.Locations().Count + " posterunków, " + num + " wizyt.");
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		bool enabled = LocalPersistence.Enabled;
		LocalPersistence.Enabled = false;
		try
		{
			HttpWebRequest httpWebRequest = Td2Provider.CreateRequest(Td2Provider.Address("eu"));
			Check(httpWebRequest.Method == "GET" && string.IsNullOrEmpty(httpWebRequest.UserAgent) && !httpWebRequest.Headers.AllKeys.Any((string x) => x.Equals("User-Agent", StringComparison.OrdinalIgnoreCase)), "Żądanie TD2 jest GET i nie zawiera User-Agent.");
			httpWebRequest.Abort();
			Rejected(() =>
			{
				Td2Provider.Address("eu;writeToSWDR");
			}, "Region nie pozwala zmienić metody ani wstrzyknąć operacji API.");
			Check(Td2Provider.Time("2026-10-23T22:03:00.000Z") == new DateTime(2026, 10, 24, 0, 3, 0), "Znaczniki UTC są przeliczane na czas Polski, również przez północ.");
			Check(Td2Provider.Time("2026-01-10T12:00:00Z").Value.Hour == 13 && Td2Provider.Time("2026-07-10T12:00:00Z").Value.Hour == 14, "Przeliczanie dat uwzględnia zimowy i letni czas Polski.");
			Rejected(() =>
			{
				Td2Provider.Time("2026-10-23T22:03:00");
			}, "Data bez strefy nie jest interpretowana według przypadkowego zegara komputera.");
			byte[] current = Payload(Schedule());
			int fetches = 0;
			Td2Provider provider = new Td2Provider((string address, CancellationToken token) =>
			{
				Interlocked.Increment(ref fetches);
				if (address == "https://api.td2.info.pl/?method=getTrainsOnline")
				{
					return Json.Bytes(new Td2OnlineEnvelope
					{
						success = true,
						message = new List<Td2OnlineTrain>()
					});
				}
				Check(address == Td2Provider.Address("eu"), "Adapter rozkładu pobiera listę rozkładów TD2.");
				return current;
			});
			SimRailSnapshot simRailSnapshot = Accept(provider, current);
			Check((from x in simRailSnapshot.Locations()
				select x.Name).Contains("Beta") && !simRailSnapshot.Locations().Any((SimRailLocation x) => x.Name.Contains("SBL") || x.Name.Contains("Osobowy")), "Wybór posterunków pomija SBL i przystanki osobowe.");
			Td2Direction td2Direction = Td2Directions.At(simRailSnapshot.Td2.Records.Single().Schedule, 2, -1);
			Td2Direction td2Direction2 = Td2Directions.At(simRailSnapshot.Td2.Records.Single().Schedule, 2, 1);
			Check(td2Direction.Name == "Alfa" && td2Direction.Line == "X/Y" && td2Direction2.Name == "C" && td2Direction2.Line == "B-C", "Kierunki odróżniają połączenie scenerii i posterunek wewnętrzny, pomijając SBL i przystanek.");
			Td2Schedule td2Schedule = Schedule();
			td2Schedule.stopPoints[1].departureLine = "x";
			td2Schedule.stopPoints[2].arrivalLine = "y";
			Check(Td2Directions.At(td2Schedule, 2, -1).Line == "X/Y", "Dopasowanie połączeń scenerii nie zależy od wielkości liter oznaczeń.");
			Check(new SimRailSession(provider, simRailSnapshot, "103").SuggestStation().Sections.All((Section x) => string.IsNullOrEmpty(x.Basis)), "Propozycja konfiguracji nie wymyśla rodzaju blokady dla TD2.");
			Engine engine = EngineFor(folder, provider, simRailSnapshot);
			Train train = engine.State.Trains.Single((Train x) => x.StationId == engine.StationId);
			string id = train.Id;
			Check(engine.IsTd2 && engine.SimulatorCaption == "TD2" && engine.UnitProvider == "td2" && engine.OrganizationCaption == "TTSK", "Źródło, nazwa i jednostka są właściwe dla TD2.");
			Check(train.Number == "47512" && train.Kind == "MPE" && train.Track == "2" && train.Platform == "I" && train.ExternalPlannedStop == 6.0, "Numer, rodzaj, peron, tor i postój pochodzą z rozkładu.");
			Check(!train.Arrival.HasValue && train.ArrivalForecast == new DateTime(2026, 10, 24, 0, 5, 0) && !train.ExternalConfirmed, "Niepotwierdzony czas API pozostaje prognozą, tak jak w widoku SimRail.");
			Check(train.Carrier == "" && train.Consist == null, "Brakujące dane przewoźnika i składu nie są zgadywane.");
			Check(train.Dangerous && train.HighRisk && train.Extraordinary && train.Profile.GaugeExceeded, "Oznaczenia ładunków i uwagi są pobierane z pól TD2.");
			Check(!engine.Station.Sections.Any((Section x) => x.Basis == "Wieloodstępowa (samoczynna) blokada liniowa") && engine.Station.Sections.All((Section x) => x.ManualConfiguration), "Podstawa ruchu pochodzi z konfiguracji posterunku, nie z założenia o blokadzie.");
			List<DetailRow> list = engine.SimRail.Route(train).ToList();
			Check(list.Count == 5 && list.All((DetailRow x) => !x.Cells[7].StartsWith("SBL")) && list.First().Cells[0] == "" && list.Last().Cells[13] == "", "Trasa zachowuje brak przyjazdu na początku i odjazdu na końcu oraz pomija SBL.");
			int writes = 0;
			engine.SimRailTimeChanged += (SepeWriteRequest r) =>
			{
				writes++;
			};
			JournalEntry entry = engine.Note(engine.Station.Sections.First().Id, engine.Now, "", "Pociąg");
			entry.TrainId = id;
			entry.Number = train.Number;
			entry.Direction = "Z";
			entry.Track = "2";
			entry.Arrived = new DateTime(2026, 10, 24, 0, 7, 0);
			engine.SaveJournal(entry);
			Check(writes == 0 && engine.State.Trains.Single((Train x) => x.Id == id).LocalArrivalRecorded && !engine.State.Trains.Single((Train x) => x.Id == id).ExternalManualTimes, "Lokalna godzina dziennika TD2 nie tworzy oczekującej wysyłki do SEPE.");
			Td2Schedule td2Schedule2 = Schedule();
			td2Schedule2.stopPoints[2].confirmed = 1;
			td2Schedule2.stopPoints[2].arrivalRealTime = "2026-10-23T22:10:00.000Z";
			simRailSnapshot = Accept(provider, Payload(td2Schedule2));
			engine.ApplySimRailSnapshot(simRailSnapshot);
			Check(engine.State.Trains.Single((Train x) => x.Id == id).Arrival == entry.Arrived && engine.State.Entries.Any((JournalEntry x) => x.Id == entry.Id), "Odświeżenie nie usuwa własnej godziny ani wpisu dziennika.");
			Train train2 = engine.State.Trains.Single((Train x) => x.Id == id);
			Rejected(() =>
			{
				provider.Accept(Encoding.UTF8.GetBytes("{\"success\":false,\"message\":[]}"), "eu", downloaded);
			}, "Błąd API nie jest traktowany jako usunięcie rozkładów.");
			Rejected(() =>
			{
				provider.Accept(Encoding.UTF8.GetBytes("{\"success\":true,\"message\":[{}]}"), "eu", downloaded);
			}, "Niepełna odpowiedź jest odrzucana przed zmianą stanów pociągów.");
			Check(train2.Runs && !train2.RemovedFromSource, "Nieudany odczyt pozostawia dotychczasowe oznaczenie kursowania.");
			simRailSnapshot = Accept(provider, Payload());
			engine.ApplySimRailSnapshot(simRailSnapshot);
			train = engine.State.Trains.Single((Train x) => x.Id == id);
			Check(!train.Runs && train.RemovedFromSource && engine.State.Entries.Any((JournalEntry x) => x.Id == entry.Id), "Usunięty rozkład zostaje z oznaczeniem Nie kursuje i swoim dziennikiem.");
			Check(engine.SimRail.Route(train).Count() == 5 && simRailSnapshot.Locations().Any((SimRailLocation x) => x.Id == "103"), "Po usunięciu pozostają trasa oraz wybrany posterunek.");
			Td2Schedule td2Schedule3 = Schedule();
			td2Schedule3.stopPoints[2].comments = "III,9,Zmieniona trasa";
			engine.ApplySimRailSnapshot(Accept(provider, Payload(td2Schedule3)));
			Check(engine.State.Trains.Single((Train x) => x.Id == id).Track == "2", "Usunięty rozkład zachowuje swoją ostatnią trasę i dane po ponownym pojawieniu tego samego ID.");
			Check(!engine.State.Trains.Single((Train x) => x.Id == id).Runs, "Ten sam usunięty identyfikator nie jest ponownie oznaczany jako kursujący w tej sesji.");
			engine.ApplySimRailSnapshot(Accept(provider, Payload(Schedule(), Schedule(1002))));
			Check(Enumerable.Count(engine.State.Trains, (Train x) => x.StationId == engine.StationId) == 2 && engine.State.Trains.Single((Train x) => x.StationId == engine.StationId && x.Id != id).Runs, "Nowy rozkład z tym samym numerem jest osobną pozycją; poprzedni pozostaje Nie kursuje.");
			Td2Provider provider2 = new Td2Provider();
			Check(!Accept(provider2, Payload(Schedule())).Td2.Records.Single().Removed, "Nowa sesja nie dziedziczy oznaczeń usunięcia poprzedniej.");
			Rejected(() =>
			{
				engine.SimRail.SetAccessLink("https://api.simrail.eu:8093/?serverCode=eu&stationId=103&key=TEST_ONLY");
			}, "Sesja TD2 nie przyjmuje linku zapisu SEPE.");
			MainForm main = new MainForm(engine);
			try
			{
				main.Show();
				Pump();
				Check(Field<ComboBox>(main, "stations").SelectedIndex >= 0 && !string.IsNullOrWhiteSpace(Field<ComboBox>(main, "stations").Text), "Wybrany posterunek TD2 jest widoczny w polu wyboru.");
				Check(main.MainMenuStrip.Items.OfType<ToolStripMenuItem>().Any((ToolStripMenuItem x) => x.Text == "TD2") && !main.MainMenuStrip.Items.OfType<ToolStripMenuItem>().Any((ToolStripMenuItem x) => x.Text == "SimRail"), "Główne menu pokazuje TD2 i nie oferuje wysyłania godzin.");
				Check(Field<TabControl>(main, "topTabs").TabCount == 3, "TD2 korzysta z tych samych trzech zakładek EDR.");
				Check(main.outgoingHours.Count == 0, "Otwarcie widoku nie tworzy kolejki zapisu.");
				DataGridView dataGridView = Field<DataGridView>(main, "trains");
				DataGridViewRow dataGridViewRow = dataGridView.Rows.Cast<DataGridViewRow>().Single((DataGridViewRow x) => (string)x.Tag == id);
				Check((bool)dataGridViewRow.Cells["nk"].Value && !(bool)dataGridViewRow.Cells["k"].Value, "Wykaz oznacza zachowany rozkład checkboxem NK.");
				Call(main, "OpenSessionWindow");
				Pump();
				MainForm mainForm = Application.OpenForms.OfType<MainForm>().Single((MainForm x) => x != main);
				Check(mainForm.Engine.IsTd2 && object.ReferenceEquals(main.Engine.Store, mainForm.Engine.Store) && !mainForm.Engine.SimRail.HasWriteLink, "Drugie okno TD2 współdzieli tę samą sesję i rozkłady.");
				mainForm.Close();
				Pump();
				Check(Field<ComboBox>(main, "stations").SelectedIndex >= 0, "Zamknięcie drugiego okna zachowuje wybrany posterunek pierwszego.");
				Ui.Capture(main, Path.Combine(folder, "td2-wykaz.png"));
				Field<TabControl>(main, "topTabs").SelectedIndex = 2;
				Pump();
				Ui.Capture(main, Path.Combine(folder, "td2-trasa.png"));
				main.Close();
				Pump();
			}
			finally
			{
				if (main != null)
				{
					((IDisposable)main).Dispose();
				}
			}
			current = Payload(Schedule());
			Task<SimRailSnapshot> task = provider.SnapshotAsync("eu", CancellationToken.None);
			SimRailSnapshot result = task.GetAwaiter().GetResult();
			Check(fetches == 2 && result.Td2 != null, "Ścieżka pobierania pobiera rozkład i opcjonalny skład online dla metadanych.");
			Connection(folder);
			Live(folder);
			log.Add("Brak zapisów do API TD2 i żywego SEPE. Testy zapisu godzin dotyczą wyłącznie lokalnego dziennika.");
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			File.WriteAllLines(Path.Combine(folder, "wyniki-td2190.txt"), log, Encoding.UTF8);
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
		File.WriteAllLines(Path.Combine(folder, "wyniki-td2190.txt"), log, Encoding.UTF8);
		return true;
	}

	private static void Connection(string folder)
	{
		Td2Provider source = new Td2Provider((string address, CancellationToken token) => Payload(Schedule()));
		SimulatorConnectionForm form = new SimulatorConnectionForm(source, new ConfiguratorRepository("td2", Path.Combine(folder, "configuration")), Path.Combine(folder, "session"));
		try
		{
			form.Show();
			Wait(() => Field<ComboBox>(form, "servers").Items.Count > 0);
			Check(!All(form).OfType<Label>().Any((Label x) => x.Visible && x.Text.Contains("Link SEPE")), "Połączenie TD2 nie prosi o link SEPE.");
			form.BeginInvoke((Action)(() =>
			{
				Call(form, "LoadSnapshot");
			}));
			Wait(() => Field<ListBox>(form, "available").Items.Count > 0);
			ListBox listBox = Field<ListBox>(form, "available");
			listBox.SelectedIndex = listBox.Items.Cast<SimRailLocation>().ToList().FindIndex((SimRailLocation x) => x.Id == "103");
			Call(form, "AddSelected");
			Check(Field<Button>(form, "connect").Enabled, "Pobrany rozkład pozwala wybrać posterunek TD2.");
			Ui.Capture(form, Path.Combine(folder, "td2-polaczenie.png"));
			Call(form, "Connect");
			Check(form.ConnectedEngine.IsTd2 && form.ConnectedEngine.Store.IsTransient && form.NeedsConfiguration, "Połączenie tworzy sesję TD2 i wymaga zatwierdzenia rzeczywistej konfiguracji posterunku.");
			form.DatabaseLock.ReleaseMutex();
			form.DatabaseLock.Dispose();
			form.ConnectedEngine.SimRail.SessionLock = null;
		}
		finally
		{
			if (form != null)
			{
				((IDisposable)form).Dispose();
			}
		}
		using ModeForm root = new ModeForm();
		Check(All(root).OfType<Button>().Single((Button x) => x.Text == "TD2").Enabled, "Przycisk TD2 na starcie jest aktywny.");
	}
}

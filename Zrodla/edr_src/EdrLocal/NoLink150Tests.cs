using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EdrLocal;

public static class NoLink150Tests
{
	private sealed class Feed : ISimulatorFeed
	{
		public int Reads;

		public string ProviderId => "SimRail";

		public Task<List<SimRailServer>> ServersAsync(CancellationToken t)
		{
			throw new Exception("No network in this test.");
		}

		public Task<SimRailSnapshot> SnapshotAsync(string s, CancellationToken t)
		{
			Reads++;
			throw new Exception("No network in this test.");
		}
	}

	private static readonly List<string> log = new List<string>();

	private static readonly DateTime day = new DateTime(2026, 10, 22, 12, 0, 0);

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

	private static void Flag(object o, string n, bool value)
	{
		o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, value);
	}

	private static void Call(object o, string n, params object[] args)
	{
		try
		{
			o.GetType().GetMethod(n, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, args);
		}
		catch (TargetInvocationException ex)
		{
			throw ex.InnerException;
		}
	}

	private static void Menus(MainForm main, bool enabled)
	{
		ToolStripMenuItem toolStripMenuItem = main.MainMenuStrip.Items.OfType<ToolStripMenuItem>().Single((ToolStripMenuItem x) => x.Text == "SimRail");
		Call(toolStripMenuItem, "OnDropDownShow", EventArgs.Empty);
		Check((from x in toolStripMenuItem.DropDownItems.OfType<ToolStripMenuItem>()
			where x.Text.StartsWith("Wyślij")
			select x).All((ToolStripMenuItem x) => x.Enabled == enabled), "Menu SimRail: dostępność wysyłki = " + enabled);
		ContextMenuStrip contextMenuStrip = Field<DataGridView>(main, "trains").ContextMenuStrip;
		Call(contextMenuStrip, "OnOpening", new CancelEventArgs());
		Check(contextMenuStrip.Items.OfType<ToolStripMenuItem>().Single((ToolStripMenuItem x) => x.Text == "Wyślij godziny do SEPE").Enabled == enabled, "Menu pociągu: dostępność wysyłki = " + enabled);
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		LocalPersistence.Enabled = false;
		try
		{
			Feed feed = new Feed();
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
					arrivalTime = "2026-10-22 11:50:00",
					departureTime = "2026-10-22 11:51:00"
				});
			}
			SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
			simRailSnapshot.ServerCode = "test150";
			simRailSnapshot.GameTime = day;
			simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
			simRailSnapshot.DispatcherNames = new HashSet<string> { "A", "B", "C" };
			simRailSnapshot.Trains = new List<SimRailTrain> { simRailTrain2 };
			SimRailSnapshot snapshot = simRailSnapshot;
			SimRailSession simRailSession = new SimRailSession(feed, snapshot, "11");
			simRailSession.SetControlledPoints(new string[2] { "10", "11" });
			Station station = simRailSession.SuggestStation();
			StationSettings.Normalize(station);
			Store store = new Store(Path.Combine(folder, "session"));
			store.Load();
			store.Commit((AppState s) =>
			{
				s.Stations = new List<Station> { station };
				s.Trains.Clear();
				s.Entries.Clear();
				s.Duties.Clear();
				s.History.Clear();
			});
			Engine engine = new Engine(store)
			{
				User = store.State.Users.First((LocalUser x) => x.Id == "DEMO"),
				SimRail = simRailSession
			};
			engine.EnsureControlledStations();
			engine.ApplySimRailSnapshot(snapshot);
			engine.BeginDuty(day.AddMinutes(-10.0));
			using (MainForm mainForm = new MainForm(engine))
			{
				mainForm.Show();
				Application.DoEvents();
				mainForm.SelectTrain(engine.State.Trains.Single((Train x) => x.StationId == engine.StationId).Id);
				Queue<SepeWriteRequest> queue = Field<Queue<SepeWriteRequest>>(mainForm, "outgoingHours");
				string id = engine.State.Trains.Single((Train x) => x.StationId == engine.StationId).Id;
				engine.SetTrainTimes(id, day.AddMinutes(-5.0), day.AddMinutes(-4.0));
				Check(queue.Count == 0, "Bez linku zmiana godzin nie tworzy kolejki wysyłania.");
				Application.DoEvents();
				Check(Field<SepeBackgroundWriter>(mainForm, "backgroundWriter") == null && !Field<bool>(mainForm, "sendingHours") && Field<string>(mainForm, "lastSepeError") == null, "Brak linku nie uruchamia transportu, prób ani raportu błędu.");
				Train train = engine.State.Trains.Single((Train x) => x.Id == id);
				Check(train.ExternalManualArrival && train.ExternalManualDeparture && train.Arrival == day.AddMinutes(-5.0) && train.Departure == day.AddMinutes(-4.0), "Godziny i znaczniki lokalnego zapisu pozostają w sesji.");
				Menus(mainForm, enabled: false);
				Call(mainForm, "RetrySepeWrites");
				Call(mainForm, "TransferSepeHours");
				Check(queue.Count == 0 && Field<SepeBackgroundWriter>(mainForm, "backgroundWriter") == null, "Bezpośrednie wywołanie ręcznej wysyłki bez linku również niczego nie uruchamia.");
				queue.Enqueue(SepeWriteRequest.From(train, arrival: true, departure: true));
				Call(mainForm, "ProcessSepeWrites");
				Check(queue.Count == 0 && train.ExternalManualTimes && Field<string>(mainForm, "lastSepeError") == null, "Zabezpieczenie przetwarzania usuwa niewysyłalną kolejkę bez kasowania lokalnych godzin.");
				engine.ApplySimRailSnapshot(snapshot);
				Check(engine.State.Trains.Single((Train x) => x.Id == id).Arrival == day.AddMinutes(-5.0) && engine.State.Trains.Single((Train x) => x.Id == id).ExternalManualTimes, "Odświeżenie API zachowuje lokalne godziny bez linku.");
				simRailSession.SetAccessLink("https://api.simrail.eu:8093/?serverCode=test150&stationId=11&key=TEST_ONLY_150");
				Check(queue.Count == 0 && Field<SepeBackgroundWriter>(mainForm, "backgroundWriter") == null, "Dodanie linku samo nie wysyła wcześniejszych godzin.");
				Menus(mainForm, enabled: true);
				Flag(mainForm, "decidingExit", value: true);
				Call(mainForm, "RetrySepeWrites");
				Check(queue.Count == 1 && queue.Peek().Arrival == day.AddMinutes(-5.0) && queue.Peek().Departure == day.AddMinutes(-4.0), "Jawne ponowienie po dodaniu linku zbiera wcześniejsze lokalne godziny.");
				queue.Clear();
				Flag(mainForm, "hoursPaused", value: true);
				Flag(mainForm, "decidingExit", value: false);
				engine.SetTrainTimes(id, day.AddMinutes(-3.0), day.AddMinutes(-2.0));
				Check(queue.Count == 1 && queue.Peek().Arrival == day.AddMinutes(-3.0) && queue.Peek().Departure == day.AddMinutes(-2.0), "Z poprawnym linkiem nowa zmiana nadal tworzy właściwe zlecenie.");
				Application.DoEvents();
				queue.Clear();
				engine.Station.Options.WriteSepe = false;
				Menus(mainForm, enabled: false);
				engine.SetTrainTimes(id, day.AddMinutes(-2.0), day.AddMinutes(-1.0));
				Check(queue.Count == 0, "Wyłączenie zapisu w konfiguracji nadal zatrzymuje kolejkę mimo linku.");
				engine.Station.Options.WriteSepe = true;
				engine.SwitchSimulatorStation("SIMRAIL-10");
				Menus(mainForm, enabled: false);
				Call(mainForm, "QueueSepeWrite", SepeWriteRequest.From(train, arrival: true, departure: true));
				Check(!simRailSession.HasWriteLink && queue.Count == 0, "Link dla innego posterunku nie daje prawa wysyłania z aktywnego punktu.");
				engine.SwitchSimulatorStation("SIMRAIL-11");
				Menus(mainForm, enabled: true);
				Check(queue.Count == 0, "Powrót do uprawnionego posterunku nie wysyła starych godzin samoczynnie.");
				Check(feed.Reads == 0 && !Application.OpenForms.OfType<SepeErrorForm>().Any(), "Próba lokalna nie wykonuje odczytów sieci ani nie otwiera raportu SEPE.");
				mainForm.Close();
			}
			Check(!Directory.Exists(store.DirectoryPath), "Dane testowej sesji nie są utrwalane na dysku.");
			return true;
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			return false;
		}
		finally
		{
			File.WriteAllLines(Path.Combine(folder, "wyniki-brak-linku150.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

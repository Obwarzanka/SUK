using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EdrLocal;

internal static class Midnight169Tests
{
	private sealed class Feed : ISimulatorFeed
	{
		public SimRailSnapshot Value;

		public int Reads;

		public string ProviderId => "midnight169";

		public Task<List<SimRailServer>> ServersAsync(CancellationToken token)
		{
			throw new NotSupportedException("Test nie używa sieci.");
		}

		public Task<SimRailSnapshot> SnapshotAsync(string server, CancellationToken token)
		{
			Reads++;
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

	private static object Field(object instance, string name)
	{
		return instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
	}

	private static SimRailSnapshot Snapshot(DateTime time)
	{
		SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
		simRailSnapshot.ServerCode = "midnight169";
		simRailSnapshot.GameTime = time;
		simRailSnapshot.DispatcherNames = new HashSet<string> { "A", "B", "C" };
		simRailSnapshot.Trains = new List<SimRailTrain>
		{
			new SimRailTrain
			{
				trainNoLocal = "2704",
				startStation = "A",
				endStation = "C",
				timetable = new List<SimRailPoint>
				{
					new SimRailPoint
					{
						pointId = "10",
						nameForPerson = "A",
						indexOfPoint = 0,
						line = 17,
						arrivalTime = "2026-10-22 23:50:00",
						departureTime = "2026-10-22 23:50:00"
					},
					new SimRailPoint
					{
						pointId = "60",
						nameForPerson = "B",
						indexOfPoint = 1,
						line = 17,
						arrivalTime = "2026-10-22 23:55:00",
						departureTime = "2026-10-22 23:56:00",
						displayedTrainNumber = "2704",
						trainType = "ROJ",
						isActive = true,
						track = 1
					},
					new SimRailPoint
					{
						pointId = "20",
						nameForPerson = "C",
						indexOfPoint = 2,
						line = 17,
						arrivalTime = "2026-10-23 00:05:00",
						departureTime = "2026-10-23 00:05:00"
					}
				}
			}
		};
		return simRailSnapshot;
	}

	private static void CheckHeaders(IEnumerable<JournalPane> panes, DateTime day)
	{
		foreach (JournalPane pane in panes)
		{
			JournalEntry[] source = (from DataGridViewRow x in pane.Grid.Rows
				select (JournalEntry)x.Tag).ToArray();
			Check(pane.SelectedDay == day && source.Count((JournalEntry x) => x.Text.StartsWith("Północ dnia ")) == 1 && source.Single((JournalEntry x) => x.Type == "Doba").Text == "Północ dnia " + day.ToString("dd-MM-yyyy"), "Jeden nagłówek wybranej doby w dzienniku " + pane.SectionId + " / " + day.ToString("dd-MM-yyyy"));
		}
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		LocalPersistence.Enabled = false;
		try
		{
			DateTime time = new DateTime(2026, 10, 22, 23, 59, 30);
			DateTime time2 = time.AddMinutes(1.0);
			Feed feed = new Feed();
			feed.Value = Snapshot(time);
			Feed feed2 = feed;
			SimRailSession simRailSession = new SimRailSession(feed2, feed2.Value, "60");
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
			Engine engine = new Engine(store);
			engine.User = store.State.Users.First((LocalUser x) => x.Id == "DEMO");
			engine.SimRail = simRailSession;
			Engine engine2 = engine;
			engine2.ApplySimRailSnapshot(feed2.Value);
			engine2.BeginDuty(time.AddMinutes(-5.0));
			string text = Json.Text(engine2.State.Entries);
			int count = engine2.State.History.Count;
			using (MainForm mainForm = new MainForm(engine2))
			{
				mainForm.Show();
				Application.DoEvents();
				mainForm.Size = new Size(1920, 1040);
				mainForm.ApplyZoom();
				List<JournalPane> list = (List<JournalPane>)Field(mainForm, "panes");
				CheckHeaders(list, time.Date);
				feed2.Value = Snapshot(time2);
				MethodInfo method = typeof(MainForm).GetMethod("RefreshSimulator", BindingFlags.Instance | BindingFlags.NonPublic);
				method.Invoke(mainForm, new object[1] { false });
				Application.DoEvents();
				Check(feed2.Reads == 1 && engine2.Now.Date == time2.Date, "Odświeżenie SimRail przechodzi przez północ na podstawionej odpowiedzi, bez sieci.");
				CheckHeaders(list, time2.Date);
				Check(Json.Text(engine2.State.Entries) == text && engine2.State.History.Count == count, "Zmiana doby nie dopisuje zdarzenia północy ani nie usuwa wcześniejszych wpisów.");
				for (int num = 0; num < 3; num++)
				{
					method.Invoke(mainForm, new object[1] { false });
					Application.DoEvents();
				}
				CheckHeaders(list, time2.Date);
				Check(Json.Text(engine2.State.Entries) == text, "Kolejne odświeżenia po północy nie tworzą dodatkowych wpisów.");
				Ui.Capture(mainForm, Path.Combine(folder, "po-polnocy.png"));
				foreach (JournalPane item in list)
				{
					item.SetDay(time.Date);
				}
				CheckHeaders(list, time.Date);
				Check(list.All((JournalPane p) => p.Grid.Rows.Cast<DataGridViewRow>().Any((DataGridViewRow r) => ((JournalEntry)r.Tag).Text.StartsWith("Dyżur przyjął"))), "Wpisy przyjęcia dyżuru pozostają widoczne pod poprzednią datą.");
				foreach (JournalPane item2 in list)
				{
					item2.SetDay(time2.Date);
				}
				CheckHeaders(list, time2.Date);
				mainForm.Close();
			}
			Check(!Directory.Exists(store.DirectoryPath), "Test zmiany doby nie zapisuje sesji na dysku.");
			return true;
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			return false;
		}
		finally
		{
			File.WriteAllLines(Path.Combine(folder, "wyniki-polnoc169.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

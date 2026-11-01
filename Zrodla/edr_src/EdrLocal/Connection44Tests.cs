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

public static class Connection44Tests
{
	private sealed class Feed : ISimulatorFeed
	{
		public readonly List<string> Requests = new List<string>();

		public TaskCompletionSource<SimRailSnapshot> Pending;

		public TaskCompletionSource<List<SimRailServer>> PendingServers;

		public bool Fail;

		public string ProviderId => "SimRail";

		public static List<SimRailServer> Servers()
		{
			List<SimRailServer> list = new List<SimRailServer>();
			list.Add(new SimRailServer
			{
				ServerCode = "ui44a",
				ServerName = "Serwer A"
			});
			list.Add(new SimRailServer
			{
				ServerCode = "ui44b",
				ServerName = "Serwer B"
			});
			return list;
		}

		public Task<List<SimRailServer>> ServersAsync(CancellationToken token)
		{
			if (PendingServers != null)
			{
				return PendingServers.Task;
			}
			return Task.FromResult(Servers());
		}

		public Task<SimRailSnapshot> SnapshotAsync(string server, CancellationToken token)
		{
			Requests.Add(server);
			if (Pending != null)
			{
				return Pending.Task;
			}
			if (Fail)
			{
				throw new IOException("Test failure");
			}
			return Task.FromResult(Snapshot(server));
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

	private static void Reject(Action action, string text)
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

	private static T Field<T>(object obj, string name)
	{
		return (T)obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
	}

	private static void Call(object obj, string name)
	{
		try
		{
			obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, null);
		}
		catch (TargetInvocationException ex)
		{
			throw ex.InnerException;
		}
	}

	private static IEnumerable<Control> All(Control c)
	{
		yield return c;
		foreach (Control child in c.Controls)
		{
			foreach (Control item in All(child))
			{
				yield return item;
			}
		}
	}

	private static string[] Ids(SimulatorConnectionForm f)
	{
		return (from SimRailLocation x in Field<ListBox>(f, "selected").Items
			select x.Id into x
			orderby x
			select x).ToArray();
	}

	private static void Choose(ListBox list, params string[] ids)
	{
		list.ClearSelected();
		for (int i = 0; i < list.Items.Count; i++)
		{
			if (ids.Contains(((SimRailLocation)list.Items[i]).Id))
			{
				list.SetSelected(i, value: true);
			}
		}
	}

	private static void Pump(Func<bool> until)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (!until())
		{
			Application.DoEvents();
			if (stopwatch.ElapsedMilliseconds > 5000)
			{
				throw new Exception("Timed out waiting for UI.");
			}
			Thread.Sleep(10);
		}
		Application.DoEvents();
	}

	private static SimRailSnapshot Snapshot(string server)
	{
		List<SimRailPoint> list = new List<SimRailPoint>();
		int num = 0;
		string[][] array = new string[4][]
		{
			new string[2] { "10", "Łódź Andrzejów" },
			new string[2] { "11", "Łódź Marysin" },
			new string[2] { "12", "Gałkówek" },
			new string[2] { "13", "Test z linku" }
		};
		foreach (string[] array2 in array)
		{
			list.Add(new SimRailPoint
			{
				pointId = array2[0],
				nameForPerson = array2[1],
				indexOfPoint = num++,
				line = 17,
				track = 1,
				mileage = num * 5,
				trainType = "ROJ",
				displayedTrainNumber = "11534",
				arrivalTime = "2026-10-22 08:00:00",
				departureTime = "2026-10-22 08:01:00",
				isActive = true
			});
		}
		SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
		simRailSnapshot.ServerCode = server;
		simRailSnapshot.DispatcherNames = new HashSet<string> { "Łódź Andrzejów", "Łódź Marysin", "Gałkówek" };
		simRailSnapshot.GameTime = new DateTime(2026, 10, 22, 8, 0, 0);
		simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
		simRailSnapshot.Trains = new List<SimRailTrain>
		{
			new SimRailTrain
			{
				trainNoLocal = "11534",
				trainName = "ROJ - Ł",
				startStation = "A",
				endStation = "B",
				timetable = list
			}
		};
		return simRailSnapshot;
	}

	private static SimulatorConnectionForm Open(string folder, Feed feed)
	{
		SimulatorConnectionForm simulatorConnectionForm = new SimulatorConnectionForm(feed, new ConfiguratorRepository("simrail", Path.Combine(folder, "organization")), Path.Combine(folder, "no-data"));
		simulatorConnectionForm.Show();
		Application.DoEvents();
		return simulatorConnectionForm;
	}

	private static string Link(string ids, string server)
	{
		return "https://api.simrail.eu:8093/?serverCode=" + server + "&stationId=" + ids + "&key=TEST_ONLY_44";
	}

	private static void Paste(SimulatorConnectionForm f, string text)
	{
		Field<TextBox>(f, "link").Text = text;
		Call(f, "ApplyLink");
		Application.DoEvents();
	}

	private static void CloseConnection(SimulatorConnectionForm f)
	{
		if (f.DatabaseLock != null)
		{
			f.DatabaseLock.Dispose();
		}
		f.Dispose();
	}

	public static bool Run(string folder)
	{
		Ui.Testing = true;
		Directory.CreateDirectory(folder);
		bool enabled = LocalPersistence.Enabled;
		LocalPersistence.Enabled = false;
		try
		{
			SepeLinkInfo sepeLinkInfo = SepeLink.Inspect(Link("10%2C11%2C10", "ui44a") + "&stationId=12");
			Check(sepeLinkInfo.ServerCode == "ui44a" && sepeLinkInfo.PointIds.SequenceEqual(new string[3] { "10", "11", "12" }), "Link rozpoznaje zakodowane przecinki, powtarzany parametr i usuwa duplikaty.");
			Check(!Json.Text(sepeLinkInfo).Contains("TEST_ONLY") && !Json.Text(sepeLinkInfo).Contains("key"), "Publiczne metadane linku nie zawierają klucza SEPE.");
			string[] array = new string[8]
			{
				"http://api.simrail.eu:8093/?serverCode=ui44a&stationId=10&key=x",
				Link("10", "ui44a").Replace("api.simrail.eu", "evil.example"),
				Link("10", "ui44a").Replace(":8093", ":443"),
				Link("../10", "ui44a"),
				Link("", "ui44a"),
				Link("10", "../bad"),
				Link("10", "ui44a") + "&key=second",
				Link("10", "ui44a").Replace("&key=TEST_ONLY_44", "")
			};
			foreach (string bad in array)
			{
				Reject(() =>
				{
					SepeLink.Inspect(bad);
				}, "Nieprawidłowy link nie jest akceptowany (przypadek " + (log.Count - 1) + ").");
			}
			Feed feed = new Feed();
			using (SimulatorConnectionForm simulatorConnectionForm = Open(folder, feed))
			{
				Check(All(simulatorConnectionForm).OfType<ListBox>().Count() == 2 && !Field<Button>(simulatorConnectionForm, "connect").Enabled, "Okno ma dwie listy i nie otwiera przypadkowego posterunku.");
				Call(simulatorConnectionForm, "LoadSnapshot");
				Check(Field<ListBox>(simulatorConnectionForm, "available").Items.Count == 3 && Ids(simulatorConnectionForm).Length == 0, "Pobranie rozkładu pokazuje dostępne posterunki bez automatycznego wyboru.");
				Choose(Field<ListBox>(simulatorConnectionForm, "available"), "10", "11");
				Field<Button>(simulatorConnectionForm, "add").PerformClick();
				Check(Ids(simulatorConnectionForm).SequenceEqual(new string[2] { "10", "11" }) && Field<Button>(simulatorConnectionForm, "connect").Enabled, "Przycisk > dodaje kilka zaznaczonych posterunków bez linku.");
				Choose(Field<ListBox>(simulatorConnectionForm, "selected"), "10");
				Field<Button>(simulatorConnectionForm, "remove").PerformClick();
				Check(Ids(simulatorConnectionForm).SequenceEqual(new string[1] { "11" }) && Field<ListBox>(simulatorConnectionForm, "available").Items.Cast<SimRailLocation>().Any((SimRailLocation x) => x.Id == "10"), "Przycisk < przywraca usunięty posterunek do dostępnych.");
				Choose(Field<ListBox>(simulatorConnectionForm, "available"), "10", "12");
				Field<Button>(simulatorConnectionForm, "add").PerformClick();
				Call(simulatorConnectionForm, "LoadSnapshot");
				Check(Ids(simulatorConnectionForm).SequenceEqual(new string[3] { "10", "11", "12" }), "Ręczne ponowne pobranie tego samego serwera zachowuje wybór.");
				simulatorConnectionForm.ClientSize = new Size(844, 561);
				Application.DoEvents();
				Check(All(simulatorConnectionForm).OfType<ListBox>().All((ListBox x) => x.Height > 130 && x.Width > 280 && x.Bottom <= x.Parent.ClientSize.Height), "Obie listy mieszczą się także w minimalnym oknie.");
				Check(All(simulatorConnectionForm).OfType<Button>().All((Button x) => x.Right <= x.Parent.ClientSize.Width && x.Bottom <= x.Parent.ClientSize.Height), "Przyciski pozostają dostępne w minimalnym oknie.");
				Ui.Capture(simulatorConnectionForm, Path.Combine(folder, "polaczenie-reczne-1.0.10.199.png"));
				Call(simulatorConnectionForm, "Connect");
				Engine engine = simulatorConnectionForm.ConnectedEngine;
				Check(engine != null && engine.State.Stations.Count == 3 && engine.Store.IsTransient, "Połączenie tworzy w pamięci wszystkie trzy wybrane posterunki.");
				Check(engine.SimRail.ControlledLocations().Count == 3 && !engine.SimRail.HasWriteLink, "Ręczne dodawanie posterunków nie daje klucza zapisu do gry.");
				using (MainForm mainForm = new MainForm(engine))
				{
					mainForm.Show();
					Application.DoEvents();
					ComboBox comboBox = Field<ComboBox>(mainForm, "stations");
					Check(comboBox.Items.Count == 3, "Główny EDR udostępnia wszystkie dodane posterunki.");
					comboBox.SelectedIndex = engine.State.Stations.FindIndex((Station x) => x.SimRailPointId == "11");
					Application.DoEvents();
					Check(engine.SimRail.PointId == "11" && engine.Station.Name == "Łódź Marysin" && engine.State.Trains.Any((Train x) => x.StationId == engine.StationId), "Wybór w EDR przełącza posterunek i pobiera jego rozkład.");
					mainForm.Close();
				}
				simulatorConnectionForm.DatabaseLock.Dispose();
			}
			feed = new Feed();
			SimulatorConnectionForm f = Open(folder, feed);
			try
			{
				Field<TextBox>(f, "link").Text = Link("10%2C11%2C10", "ui44b");
				Pump(() => Ids(f).Length == 2);
				Check(((SimRailServer)Field<ComboBox>(f, "servers").SelectedItem).ServerCode == "ui44b" && feed.Requests.SequenceEqual(new string[1] { "ui44b" }), "Wklejenie linku samo wybiera serwer i pobiera jego rozkład.");
				Check(Ids(f).SequenceEqual(new string[2] { "10", "11" }) && Field<Button>(f, "connect").Enabled, "Link automatycznie dodaje wszystkie posterunki bez ręcznego wyboru.");
				Paste(f, Link("10,11,13", "ui44b"));
				Check(Ids(f).SequenceEqual(new string[3] { "10", "11", "13" }), "Link może dodać znany punkt również spoza listy grywalnych.");
				Choose(Field<ListBox>(f, "selected"), "13");
				Field<Button>(f, "remove").PerformClick();
				Check(Field<ListBox>(f, "available").Items.Cast<SimRailLocation>().Any((SimRailLocation x) => x.Id == "13"), "Punkt dodany przez link można usunąć i dodać ponownie z lewej listy.");
				Choose(Field<ListBox>(f, "selected"), "11");
				Call(f, "RemoveSelected");
				Call(f, "LoadSnapshot");
				Check(Ids(f).SequenceEqual(new string[1] { "10" }), "Usunięte posterunki nie wracają po ręcznym odświeżeniu rozkładu.");
				Choose(Field<ListBox>(f, "available"), "12");
				Call(f, "AddSelected");
				f.ClientSize = new Size(960, 620);
				Application.DoEvents();
				Ui.Capture(f, Path.Combine(folder, "polaczenie-sepe-1.0.10.199.png"));
				Check(Field<TextBox>(f, "link").UseSystemPasswordChar && !All(f).OfType<Label>().Any((Label x) => x.Text.Contains("TEST_ONLY")), "Klucz jest zamaskowany i nie trafia do etykiet statusu.");
				Call(f, "Connect");
				Engine engine2 = f.ConnectedEngine;
				Check(engine2.State.Stations.Count == 2 && (from x in engine2.SimRail.ControlledLocations()
					select x.Id into x
					orderby x
					select x).SequenceEqual(new string[2] { "10", "12" }), "Połączenie respektuje ostateczny wybór po usuwaniu pozycji z linku.");
				engine2.SwitchSimulatorStation("SIMRAIL-10");
				Check(engine2.SimRail.HasWriteLink && SepeLink.Query(engine2.SimRail.AccessUrl.Query)["stationId"] == "10", "Adres zapisu jest ograniczony do aktywnego posterunku objętego linkiem.");
				engine2.SwitchSimulatorStation("SIMRAIL-12");
				Check(!engine2.SimRail.HasWriteLink && !engine2.SimRail.PublicUrl.AbsoluteUri.Contains("key"), "Ręcznie dodany posterunek spoza linku pozostaje bez uprawnień zapisu.");
				Reject(() =>
				{
					engine2.SimRail.SelectPoint("11");
				}, "Usuniętego posterunku nie można przełączyć poza wybraną sesją.");
				engine2.SimRail.SetAccessLink(Link("12,13", "ui44b"));
				engine2.EnsureControlledStations();
				Check(engine2.State.Stations.Count == 3 && engine2.SimRail.ControlledLocations().Any((SimRailLocation x) => x.Id == "13"), "Późniejsza zmiana linku może dołączyć kolejny posterunek do sesji.");
				using (new SimulatorSessionLock("ui44b", new string[1] { "11" }))
				{
					Reject(() =>
					{
						engine2.SimRail.SetAccessLink(Link("12,11", "ui44b"));
					}, "Dodawanie linku blokuje posterunek otwarty już w innym oknie.");
					Check(engine2.SimRail.HasWriteLink && !engine2.SimRail.ControlledLocations().Any((SimRailLocation x) => x.Id == "11"), "Odrzucona zmiana zachowuje poprzedni link i wybór posterunków.");
				}
				engine2.SwitchSimulatorStation("SIMRAIL-10");
				Check(!engine2.SimRail.HasWriteLink, "Zmiana klucza nie pozostawia dawnych uprawnień na innym posterunku.");
				f.DatabaseLock.Dispose();
			}
			finally
			{
				if (f != null)
				{
					((IDisposable)f).Dispose();
				}
			}
			using (SimulatorConnectionForm simulatorConnectionForm2 = Open(folder, new Feed()))
			{
				Paste(simulatorConnectionForm2, Link("10,999", "ui44a"));
				Check(!Field<Button>(simulatorConnectionForm2, "connect").Enabled && Ids(simulatorConnectionForm2).Length == 0 && Field<Label>(simulatorConnectionForm2, "status").Text.Contains("Nie znaleziono"), "Nieznany punkt z linku blokuje połączenie i pokazuje czytelny błąd.");
				Paste(simulatorConnectionForm2, "nie link");
				Check(!Field<Button>(simulatorConnectionForm2, "connect").Enabled, "Nieprawidłowy tekst linku nie pozwala otworzyć sesji.");
				Paste(simulatorConnectionForm2, "");
				Choose(Field<ListBox>(simulatorConnectionForm2, "available"), "10");
				Call(simulatorConnectionForm2, "AddSelected");
				Check(Field<Button>(simulatorConnectionForm2, "connect").Enabled, "Wyczyszczenie błędnego linku pozwala wybrać posterunek ręcznie.");
				Paste(simulatorConnectionForm2, Link("10", "missing44"));
				Check(!Field<Button>(simulatorConnectionForm2, "connect").Enabled && Field<Label>(simulatorConnectionForm2, "status").Text.Contains("nie jest dostępny"), "Brak serwera z linku nie powoduje połączenia z innym serwerem.");
				Paste(simulatorConnectionForm2, "");
				Field<ComboBox>(simulatorConnectionForm2, "servers").SelectedIndex = 1;
				Check(Ids(simulatorConnectionForm2).Length == 0 && !Field<Button>(simulatorConnectionForm2, "connect").Enabled, "Zmiana serwera usuwa poprzedni wybór.");
				simulatorConnectionForm2.Close();
			}
			Feed feed2 = new Feed();
			feed2.PendingServers = new TaskCompletionSource<List<SimRailServer>>();
			feed = feed2;
			SimulatorConnectionForm f2 = Open(folder, feed);
			try
			{
				Paste(f2, Link("10,11", "ui44b"));
				feed.PendingServers.SetResult(Feed.Servers());
				Pump(() => Ids(f2).Length == 2);
				Check(feed.Requests.SequenceEqual(new string[1] { "ui44b" }), "Link wklejony przed pobraniem listy serwerów jest stosowany po jej nadejściu.");
				f2.Close();
			}
			finally
			{
				if (f2 != null)
				{
					((IDisposable)f2).Dispose();
				}
			}
			Feed feed3 = new Feed();
			feed3.Pending = new TaskCompletionSource<SimRailSnapshot>();
			feed = feed3;
			SimulatorConnectionForm f3 = Open(folder, feed);
			try
			{
				Paste(f3, Link("10", "ui44a"));
				Paste(f3, Link("11,12", "ui44b"));
				TaskCompletionSource<SimRailSnapshot> pending = feed.Pending;
				feed.Pending = null;
				pending.SetResult(Snapshot("ui44a"));
				Pump(() => Ids(f3).Length == 2);
				Check(feed.Requests.SequenceEqual(new string[2] { "ui44a", "ui44b" }) && Ids(f3).SequenceEqual(new string[2] { "11", "12" }), "Zmiana linku podczas pobierania stosuje najnowszy serwer i posterunki.");
				f3.Close();
			}
			finally
			{
				if (f3 != null)
				{
					((IDisposable)f3).Dispose();
				}
			}
			Feed feed4 = new Feed();
			feed4.Fail = true;
			feed = feed4;
			using (SimulatorConnectionForm simulatorConnectionForm3 = Open(folder, feed))
			{
				Paste(simulatorConnectionForm3, Link("10", "ui44a"));
				Check(feed.Requests.Count == 1 && !Field<Button>(simulatorConnectionForm3, "connect").Enabled && Field<Label>(simulatorConnectionForm3, "status").Text.Contains("Nie udało"), "Błąd pobierania nie zapętla automatycznych prób ani nie otwiera sesji.");
				feed.Fail = false;
				Call(simulatorConnectionForm3, "LoadSnapshot");
				Check(Ids(simulatorConnectionForm3).SequenceEqual(new string[1] { "10" }), "Ponowna próba po błędzie pobiera rozkład i automatycznie dodaje posterunki z linku.");
				simulatorConnectionForm3.Close();
			}
			Feed feed5 = new Feed();
			feed5.Pending = new TaskCompletionSource<SimRailSnapshot>();
			feed = feed5;
			SimulatorConnectionForm f4 = Open(folder, feed);
			try
			{
				Call(f4, "LoadSnapshot");
				feed.Pending.SetResult(Snapshot("ui44b"));
				Pump(() => !Field<bool>(f4, "loading"));
				Check(!Field<Button>(f4, "connect").Enabled && Ids(f4).Length == 0, "Odpowiedź z niewłaściwego serwera jest odrzucana.");
				f4.Close();
			}
			finally
			{
				if (f4 != null)
				{
					((IDisposable)f4).Dispose();
				}
			}
			using (new SimulatorSessionLock("lock44", new string[2] { "11", "12" }))
			{
				Reject(() =>
				{
					new SimulatorSessionLock("lock44", new string[2] { "10", "11" });
				}, "Dwa różne zestawy o wspólnym posterunku nie otwierają go równolegle.");
				using (new SimulatorSessionLock("lock44", new string[1] { "10" }))
				{
					Check(ok: true, "Nieudane blokowanie zestawu zwalnia już pobrane blokady.");
				}
				using (new SimulatorSessionLock("other44", new string[2] { "11", "12" }))
				{
					Check(ok: true, "Te same posterunki na innym serwerze można otworzyć niezależnie.");
				}
			}
			using (new SimulatorSessionLock("lock44", new string[3] { "10", "11", "12" }))
			{
				Check(ok: true, "Zamknięcie sesji zwalnia wszystkie posterunki.");
			}
			Check(!Directory.Exists(Path.Combine(folder, "no-data")) && !Directory.Exists(Path.Combine(folder, "organization")) && !LocalPersistence.Enabled, "Nowe połączenie nie tworzy bazy, profili ani konfiguratora.");
			log.Add("ALL CONNECTION 44 TESTS PASSED");
			return true;
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			return false;
		}
		finally
		{
			LocalPersistence.Enabled = enabled;
			File.WriteAllLines(Path.Combine(folder, "wyniki-polaczenie44.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

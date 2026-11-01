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

public static class Memory36Tests
{
	private sealed class Feed : ISimulatorFeed
	{
		public string ProviderId => "SimRail";

		public Task<List<SimRailServer>> ServersAsync(CancellationToken token)
		{
			List<SimRailServer> list = new List<SimRailServer>();
			list.Add(new SimRailServer
			{
				ServerCode = "memory36",
				ServerName = "Test"
			});
			return Task.FromResult(list);
		}

		public Task<SimRailSnapshot> SnapshotAsync(string code, CancellationToken token)
		{
			return Task.FromResult(snapshot);
		}
	}

	private sealed class CounterFeed : IAccidentCounterFeed
	{
		public int Count;

		public Task<AccidentCounterReading> FetchAsync(string provider, CancellationToken token)
		{
			Count++;
			AccidentCounterReading accidentCounterReading = new AccidentCounterReading();
			accidentCounterReading.SchemaVersion = 1;
			accidentCounterReading.Simulator = provider;
			accidentCounterReading.Revision = Count;
			accidentCounterReading.ServerEpochSeconds = 1792600000L;
			accidentCounterReading.LastResetEpochSeconds = 1792599900L;
			return Task.FromResult(accidentCounterReading);
		}
	}

	private static readonly List<string> log = new List<string>();

	private static SimRailSnapshot snapshot;

	private static void Check(bool ok, string text)
	{
		if (!ok)
		{
			throw new Exception("FAIL: " + text);
		}
		log.Add("PASS: " + text);
	}

	private static void Reject(Action a, string text)
	{
		bool ok = false;
		try
		{
			a();
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

	private static object Call(object obj, string name)
	{
		return obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, null);
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

	private static IEnumerable<ToolStripItem> Items(ToolStripItemCollection items)
	{
		foreach (ToolStripItem item in items)
		{
			yield return item;
			if (!(item is ToolStripDropDownItem menu))
			{
				continue;
			}
			foreach (ToolStripItem item2 in Items(menu.DropDownItems))
			{
				yield return item2;
			}
		}
	}

	private static SimulatorConnectionForm Connect(ConfiguratorRepository repo, string path)
	{
		SimulatorConnectionForm simulatorConnectionForm = new SimulatorConnectionForm(new Feed(), repo, path);
		simulatorConnectionForm.Show();
		Application.DoEvents();
		Call(simulatorConnectionForm, "LoadSnapshot");
		ListBox listBox = Field<ListBox>(simulatorConnectionForm, "available");
		listBox.SelectedIndex = listBox.Items.Cast<SimRailLocation>().ToList().FindIndex((SimRailLocation x) => x.Name == "Knapówka");
		Call(simulatorConnectionForm, "AddSelected");
		Call(simulatorConnectionForm, "Connect");
		return simulatorConnectionForm;
	}

	public static bool Run(string folder)
	{
		return Run(folder, null);
	}

	public static bool Run(string folder, string fixture)
	{
		Ui.Testing = true;
		Directory.CreateDirectory(folder);
		bool enabled = LocalPersistence.Enabled;
		try
		{
			LocalPersistence.Enabled = false;
			List<SimRailTrain> trains = SimRailProvider.Parse<List<SimRailTrain>>(File.ReadAllBytes(fixture ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src", "testdata", "knapowka36.json")));
			SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
			simRailSnapshot.ServerCode = "memory36";
			simRailSnapshot.GameTime = new DateTime(2026, 10, 21, 21, 44, 0);
			simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
			simRailSnapshot.Trains = trains;
			simRailSnapshot.DispatcherNames = new HashSet<string> { "Knapówka", "Psary", "Czarnca", "Włoszczowa Północ" };
			snapshot = simRailSnapshot;
			SimRailSession simRailSession = new SimRailSession(new Feed(), snapshot, "1772");
			Station station = simRailSession.SuggestStation();
			StationSettings.Normalize(station);
			log.Add("INFO: Fresh sections: " + string.Join("; ", station.Sections.Select((Section x) => x.Name + " / " + x.Line)));
			Check(station.Sections.Any((Section x) => x.Name == "Psary" && x.Line == "4"), "Świeża Knapówka zawiera Psary na linii 4.");
			Check(station.Sections.Any((Section x) => x.Name == "Włoszczowa Północ") && station.Sections.Any((Section x) => x.Name == "Czarnca"), "Knapówka zachowuje również Włoszczowę Północ i Czarncę.");
			Train train = simRailSession.Project(station).Single((Train x) => x.ExternalTrainNo == "5417");
			Check(train.From == "Włoszczowa Północ" && train.To == "Psary", "Pociąg 5417 ma kierunek Włoszczowa Północ → Psary, zgodnie z trasą.");
			string[][] array = new string[7][]
			{
				new string[3] { "ECE - EC „Moritz”", "ECE", "Moritz" },
				new string[3] { "EIJ - EIP", "EIJ", "" },
				new string[3] { "MPE - TLK „Cookie”", "MPE", "Cookie" },
				new string[3] { "TME", "TME", "" },
				new string[3] { "ROJ - R1", "ROJ", "" },
				new string[3] { "MHE - IC „Dwa Słowa”", "MHE", "Dwa Słowa" },
				new string[3] { "Nazwa własna", "MPE", "Nazwa własna" }
			};
			foreach (string[] array2 in array)
			{
				Check(SimRailTrainIdentity.Name(array2[0], array2[1]) == array2[2], "Pole N zawiera tylko nazwę własną: " + array2[0] + ".");
			}
			SimRailTrain simRailTrain = new SimRailTrain();
			simRailTrain.trainName = "MPE - TLK „Cookie”";
			Check(SimRailTrainIdentity.Carrier(simRailTrain, "MPE") == "PKP Intercity S.A.", "Usunięcie kategorii z pola N nie zmienia ustalania przewoźnika.");
			SimRailTrain simRailTrain2 = new SimRailTrain();
			simRailTrain2.trainName = "TDE";
			simRailTrain2.timetable = new List<SimRailPoint>
			{
				new SimRailPoint
				{
					trainType = "TDE"
				},
				new SimRailPoint
				{
					trainType = "TME"
				}
			};
			Check(SimRailTrainIdentity.Name(simRailTrain2, "TME") == "", "Zmiana kategorii na trasie nie zamienia starej kategorii w nazwę własną.");
			string text = Path.Combine(folder, "local-session");
			Store store = new Store(text);
			store.Load();
			Check(store.IsTransient && !Directory.Exists(text), "Nowy Store działa w pamięci i nie tworzy folderu danych.");
			Engine engine = new Engine(store)
			{
				User = store.State.Users.First((LocalUser x) => x.Id == "DEMO")
			};
			engine.BeginDuty(DateTime.Now.AddMinutes(-2.0));
			JournalEntry note = engine.Note(engine.Station.Sections[0].Id, DateTime.Now, "Tylko w pamięci", "Wpis");
			engine.SaveJournal(note);
			store.SetUiZoom(1.25f);
			store.Flush();
			Check(engine.State.Entries.Any((JournalEntry x) => x.Id == note.Id) && !Directory.Exists(text), "Wpis, dyżur, skala i Flush nie tworzą plików.");
			Store store2 = new Store(text);
			store2.Load();
			Check(!store2.State.Entries.Any((JournalEntry x) => x.Id == note.Id) && store2.State.Duties.Count == 0, "Nowa sesja nie odzyskuje poprzednich wpisów i dyżuru.");
			string text2 = Path.Combine(folder, "old-data");
			Directory.CreateDirectory(text2);
			string originalFile = Path.Combine(text2, "edr.json");
			File.WriteAllText(originalFile, "Stary plik pozostaje nienaruszony");
			Store store3 = new Store(text2);
			store3.Load();
			Check(store3.IsTransient && File.ReadAllText(originalFile) == "Stary plik pozostaje nienaruszony", "Stara baza nie jest ani odczytywana, ani nadpisywana.");
			Reject(() =>
			{
				engine.ExportStation(Path.Combine(folder, "should-not-exist.posterunek.json"));
			}, "Eksport konfiguracji jest wyłączony.");
			Reject(() =>
			{
				engine.ExportWorking(Path.Combine(folder, "should-not-exist.sesja.json"), session: true);
			}, "Eksport sesji jest wyłączony.");
			Reject(() =>
			{
				engine.ImportStation(originalFile);
			}, "Import starej konfiguracji jest wyłączony.");
			Reject(() =>
			{
				Engine.RestoreLegacyDatabase(originalFile, Path.Combine(folder, "should-not-exist-restore"));
			}, "Odzyskiwanie dawnych baz jest wyłączone.");
			Reject(() =>
			{
				ConfiguratorRepository.AtomicWrite(Path.Combine(folder, "should-not-exist.json"), new byte[0]);
			}, "Wspólny zapis atomowy jest wyłączony.");
			engine.UnitSettingsDirectory = Path.Combine(folder, "units");
			engine.SetUnitCaption("Nazwa tylko w sesji");
			Check(engine.UnitCaption == "Nazwa tylko w sesji" && !Directory.Exists(engine.UnitSettingsDirectory), "Zmiana jednostki działa wyłącznie w pamięci.");
			Check(new Engine(store).UnitCaption == "TTSK", "Nowy silnik nie przywraca zmienionej jednostki.");
			ConfiguratorRepository configuratorRepository = new ConfiguratorRepository("simrail", Path.Combine(folder, "appdata"));
			string profiles = configuratorRepository.Profiles;
			Directory.CreateDirectory(profiles);
			Station station2 = Json.Clone(station);
			station2.Sections.RemoveAll((Section x) => x.Name == "Psary");
			station2.SimulatorReviewed = true;
			station2.RouteNeighboursVersion = 1;
			station2.SimulatorTopologyVersion = 2;
			foreach (Section section in station2.Sections)
			{
				section.ManualConfiguration = true;
			}
			File.WriteAllBytes(Path.Combine(profiles, "1772.json"), Json.Bytes(new StationProfile
			{
				SchemaVersion = 1,
				PointId = "1772",
				SavedUtc = DateTime.UtcNow,
				Station = station2
			}));
			File.ReadAllText(Path.Combine(profiles, "1772.json"));
			string path = Path.Combine(folder, "simrail");
			Engine connectedEngine;
			using (SimulatorConnectionForm simulatorConnectionForm = Connect(configuratorRepository, path))
			{
				connectedEngine = simulatorConnectionForm.ConnectedEngine;
				Check(connectedEngine.Store.IsTransient && !Directory.Exists(path), "Połączenie SimRail nie tworzy bazy ani kopii zapasowych.");
				Check(!connectedEngine.Station.Sections.Any((Section x) => x.Name == "Psary") && connectedEngine.Station.SimulatorReviewed, "Zatwierdzony profil konfiguracji jest odtwarzany mimo sesji działającej w pamięci.");
				connectedEngine.Store.Commit((AppState s) =>
				{
					s.Stations.First().SimulatorReviewed = true;
				});
				connectedEngine.PersistStationProfile();
				connectedEngine.InitializeStationProfiles();
				Check(Json.Read<StationProfile>(File.ReadAllBytes(Path.Combine(profiles, "1772.json"))).Station.SimulatorReviewed, "Zatwierdzanie konfiguracji utrwala sam profil posterunku.");
				Check(!File.Exists(configuratorRepository.DataFile), "Połączenie nie zapisuje konfiguratora organizacyjnego.");
				simulatorConnectionForm.DatabaseLock.ReleaseMutex();
				simulatorConnectionForm.DatabaseLock.Dispose();
			}
			using (StationConfigurationForm stationConfigurationForm = new StationConfigurationForm(connectedEngine))
			{
				stationConfigurationForm.Show();
				Application.DoEvents();
				Call(stationConfigurationForm, "Save");
			}
			Check(connectedEngine.Station.SimulatorReviewed && !Directory.Exists(path) && File.Exists(Path.Combine(profiles, "1772.json")), "Edytor zapisuje konfigurację na dysku, a sesja nadal pozostaje wyłącznie w pamięci.");
			connectedEngine.BeginDuty(connectedEngine.Now.AddMinutes(-2.0));
			JournalEntry entry = connectedEngine.Note(connectedEngine.Station.Sections[0].Id, connectedEngine.Now, "Bieżący wpis", "Wpis");
			connectedEngine.SaveJournal(entry);
			using (MainForm mainForm = new MainForm(connectedEngine))
			{
				mainForm.Show();
				Application.DoEvents();
				mainForm.Size = new Size(1600, 900);
				Check(mainForm.AccidentCounterCaption == "Czas bez wypadku spow. przez pracownika PLK", "Przywrócono dokładny wcześniejszy podpis licznika PLK.");
				List<string> source = (from x in Items(mainForm.MainMenuStrip.Items)
					select x.Text).ToList();
				Check(!source.Any((string x) => x.StartsWith("Zapisz konfigurację") || x.StartsWith("Wczytaj") || x.StartsWith("Kopia") || x.StartsWith("Eksportuj")), "Menu nie zawiera zapisu, odczytu, kopii ani eksportu danych.");
				Check(!source.Any((string x) => x == "Nowa sesja ćwiczeniowa"), "SimRail nie pokazuje resetu lokalnego rozkładu szkoleniowego.");
				mainForm.SelectTrain(connectedEngine.State.Trains.Single((Train x) => x.ExternalTrainNo == "5417").Id);
				Ui.Capture(mainForm, Path.Combine(folder, "knapowka-1.0.10.199.png"));
				mainForm.Close();
				Check(mainForm.IsDisposed && !Application.OpenForms.OfType<ExitSessionForm>().Any(), "Zamknięcie sesji nie pyta o zapis i nie tworzy backupu.");
			}
			Check(!Directory.Exists(path), "Wpisy i zamknięcie SimRail pozostawiły folder danych nieutworzony.");
			using (SimulatorConnectionForm simulatorConnectionForm2 = Connect(configuratorRepository, path))
			{
				Check(simulatorConnectionForm2.ConnectedEngine.State.Entries.Count == 0 && simulatorConnectionForm2.ConnectedEngine.State.Duties.Count == 0, "Ponowne połączenie zaczyna nową pustą sesję.");
				simulatorConnectionForm2.DatabaseLock.ReleaseMutex();
				simulatorConnectionForm2.DatabaseLock.Dispose();
			}
			CounterFeed counterFeed = new CounterFeed();
			string path2 = Path.Combine(folder, "counter", "cache.json");
			TimeSpan elapsed = TimeSpan.Zero;
			AccidentCounterClient accidentCounterClient = new AccidentCounterClient("simrail", counterFeed, path2, () => DateTime.UtcNow, () => elapsed);
			Check(accidentCounterClient.PollIfDueAsync(CancellationToken.None).GetAwaiter().GetResult() && accidentCounterClient.HasReading && !Directory.Exists(Path.GetDirectoryName(path2)), "Licznik pobiera API bez zapisywania cache.");
			elapsed = TimeSpan.FromSeconds(59.0);
			accidentCounterClient.PollIfDueAsync(CancellationToken.None).GetAwaiter().GetResult();
			Check(counterFeed.Count == 1, "Licznik nie odpytuje API przed upływem minuty.");
			elapsed = TimeSpan.FromSeconds(60.0);
			accidentCounterClient.PollIfDueAsync(CancellationToken.None).GetAwaiter().GetResult();
			Check(counterFeed.Count == 2 && !File.Exists(path2), "Po minucie licznik odczytuje zmianę i nadal niczego nie zapisuje.");
			string path3 = Path.Combine(folder, "stale-counter.json");
			File.WriteAllText(path3, "Poprzedni cache");
			AccidentCounterClient accidentCounterClient2 = new AccidentCounterClient("simrail", new CounterFeed(), path3);
			Check(!accidentCounterClient2.HasReading, "Licznik nie wczytuje wcześniejszego cache.");
			accidentCounterClient2.PollIfDueAsync(CancellationToken.None).GetAwaiter().GetResult();
			Check(File.ReadAllText(path3) == "Poprzedni cache", "Licznik nie nadpisuje poprzedniego cache.");
			using (JournalReportForm c = new JournalReportForm(connectedEngine, tel: false, connectedEngine.Now))
			{
				Check(!All(c).OfType<ToolStrip>().SelectMany((ToolStrip x) => x.Items.Cast<ToolStripItem>()).Any((ToolStripItem x) => x.Text == "Zapisz PDF"), "Raport nie udostępnia zapisu PDF.");
			}
			using (ModeForm modeForm = new ModeForm())
			{
				Check(!All(modeForm).OfType<Button>().Any((Button x) => x.Text.Contains("Wczytaj") || x.Text.StartsWith("Konfigurator")), "Start nie oferuje odczytu dawnych baz ani konfiguratora.");
				modeForm.Show();
				Application.DoEvents();
				Ui.Capture(modeForm, Path.Combine(folder, "start-1.0.10.199.png"));
				modeForm.Close();
			}
			Check(!Directory.GetFiles(folder, "should-not-exist*", SearchOption.TopDirectoryOnly).Any(), "Zablokowane ścieżki zapisu nie utworzyły żadnego pliku.");
			Check(!SepeWebView.UserDataFolder.StartsWith(AppDomain.CurrentDomain.BaseDirectory, StringComparison.OrdinalIgnoreCase) && SepeWebView.UserDataFolder.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase), "Techniczny profil WebView2 używa tymczasowego folderu zamiast trwałych danych EDR.");
			log.Add("ALL MEMORY 36 TESTS PASSED");
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
			File.WriteAllLines(Path.Combine(folder, "wyniki-pamiec36.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

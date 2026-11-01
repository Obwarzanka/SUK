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

public static class Organization29Tests
{
	private sealed class Feed : ISimulatorFeed
	{
		public string LastServer;

		public bool Fail;

		public bool Delay;

		public TaskCompletionSource<SimRailSnapshot> Pending;

		public string ProviderId => "SimRail";

		public Task<List<SimRailServer>> ServersAsync(CancellationToken token)
		{
			throw new Exception("Konfigurator nie powinien pobierać listy serwerów.");
		}

		public Task<SimRailSnapshot> SnapshotAsync(string server, CancellationToken token)
		{
			LastServer = server;
			if (Delay)
			{
				Pending = new TaskCompletionSource<SimRailSnapshot>();
				token.Register(() =>
				{
					Pending.TrySetCanceled();
				});
				return Pending.Task;
			}
			if (Fail)
			{
				TaskCompletionSource<SimRailSnapshot> taskCompletionSource = new TaskCompletionSource<SimRailSnapshot>();
				taskCompletionSource.SetException(new IOException("Testowy brak połączenia"));
				return taskCompletionSource.Task;
			}
			return Task.FromResult(Snapshot());
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
		obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, null);
	}

	private static IEnumerable<Control> Controls(Control c)
	{
		yield return c;
		foreach (Control child in c.Controls)
		{
			foreach (Control item in Controls(child))
			{
				yield return item;
			}
		}
	}

	private static IEnumerable<TreeNode> Nodes(TreeNode root)
	{
		yield return root;
		foreach (TreeNode child in root.Nodes)
		{
			foreach (TreeNode item in Nodes(child))
			{
				yield return item;
			}
		}
	}

	private static Station Station(string key, string name)
	{
		Station station = new Station();
		station.Id = "station-" + key;
		station.Name = name;
		station.SimRailPointId = key;
		station.PostType = "POSTERUNEK ODGAŁĘŹNY";
		station.Tracks = new List<string> { "1", "2" };
		station.Checks = new List<string>();
		station.Sections = new List<Section>
		{
			new Section
			{
				Id = "a",
				Name = "Dorota",
				Line = "171",
				Tracks = 2,
				NormalIncomingTrack = 1,
				Basis = "Blokada samoczynna"
			},
			new Section
			{
				Id = "b",
				Name = "Sosnowiec Dańdówka",
				Line = "171",
				Tracks = 1,
				NormalIncomingTrack = 1,
				Basis = "Blokada samoczynna"
			}
		};
		Station station2 = station;
		StationSettings.Normalize(station2);
		return station2;
	}

	private static SimRailPoint Point(string id, string name, int at)
	{
		SimRailPoint simRailPoint = new SimRailPoint();
		simRailPoint.pointId = id;
		simRailPoint.nameForPerson = name;
		simRailPoint.indexOfPoint = at;
		simRailPoint.line = 171;
		simRailPoint.mileage = at * 5;
		simRailPoint.arrivalTime = "2026-04-22 08:00:00";
		simRailPoint.departureTime = "2026-04-22 08:00:00";
		simRailPoint.displayedTrainNumber = "444015";
		simRailPoint.track = 1;
		return simRailPoint;
	}

	private static SimRailSnapshot Snapshot()
	{
		SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
		simRailSnapshot.ServerCode = "pl3";
		simRailSnapshot.GameTime = new DateTime(2026, 4, 22, 8, 30, 0);
		simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
		simRailSnapshot.DispatcherNames = new HashSet<string> { "Dorota", "Juliusz", "Sosnowiec Dańdówka" };
		simRailSnapshot.Trains = new List<SimRailTrain>
		{
			new SimRailTrain
			{
				trainNoLocal = "444015",
				timetable = new List<SimRailPoint>
				{
					Point("100", "Dorota", 0),
					Point("101", "Juliusz", 1),
					Point("102", "Sosnowiec Dańdówka", 2)
				}
			}
		};
		return simRailSnapshot;
	}

	private static void WaitFor(StationRegistrationForm form)
	{
		DateTime dateTime = DateTime.UtcNow.AddSeconds(25.0);
		while (Field<bool>(form, "loading") && DateTime.UtcNow < dateTime)
		{
			Application.DoEvents();
			Thread.Sleep(10);
		}
		Check(!Field<bool>(form, "loading"), "Pobieranie listy kończy się bez blokowania okna.");
		Application.DoEvents();
	}

	private static void NameDialog(Action open, string name)
	{
		Exception failure = null;
		System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer
		{
			Interval = 80
		};
		try
		{
			timer.Tick += (object s, EventArgs e) =>
			{
				Editor editor = Application.OpenForms.OfType<Editor>().LastOrDefault();
				if (editor == null)
				{
					return;
				}
				timer.Stop();
				try
				{
					Controls(editor).OfType<TextBox>().First().Text = name;
					editor.Buttons.Controls.OfType<Button>().First((Button x) => x.Text == "Zapisz").PerformClick();
				}
				catch (Exception ex)
				{
					failure = ex;
					editor.Close();
				}
			};
			timer.Start();
			open();
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
	}

	public static bool Run(string folder)
	{
		Ui.Testing = true;
		Directory.CreateDirectory(folder);
		try
		{
			string root = Path.Combine(folder, "local-appdata");
			ConfiguratorRepository repo = new ConfiguratorRepository("simrail", root);
			repo.Load();
			foreach (ConfigurationUnit item in repo.Data.Units.Where((ConfigurationUnit x) => x.Kind == "IZ").ToList())
			{
				repo.RemoveUnit(item.Id);
			}
			Check(repo.Data.SchemaVersion == 2 && repo.Data.Units.Count == 0, "Nowa struktura zaczyna się bez fikcyjnych zakładów i sekcji.");
			string iz = repo.AddDepartment("Szczecin");
			string iz2 = repo.AddDepartment("Katowice");
			string ise = repo.AddSection(iz, "Szczecinek");
			string ise2 = repo.AddSection(iz2, "Katowice");
			Check(repo.Data.Units.First((ConfigurationUnit x) => x.Id == ise).ParentId == iz && repo.Data.Units.First((ConfigurationUnit x) => x.Id == ise).Kind == "ISE", "Sekcja ISE należy do wybranego zakładu IZ.");
			Reject(() =>
			{
				repo.AddSection(ise, "Nieprawidłowa");
			}, "Nie można umieścić sekcji w innej sekcji.");
			Reject(() =>
			{
				repo.AddDepartment(" szczecin ");
			}, "Ta sama nazwa zakładu nie jest dodawana ponownie.");
			Reject(() =>
			{
				repo.AddSection(iz, "SZCZECINEK");
			}, "Ta sama nazwa sekcji nie jest dodawana ponownie w zakładzie.");
			string twin = repo.AddSection(iz2, "Szczecinek");
			Check(Enumerable.Count(repo.Data.Units, (ConfigurationUnit x) => x.Name == "Szczecinek") == 2, "Różne zakłady mogą mieć sekcje o tej samej nazwie.");
			repo.Register(ise, "101", Station("101", "Juliusz"));
			repo.SaveStation("101", repo.Data.Stations.Single().Station);
			string configuratorRevision = repo.Data.Stations[0].Station.ConfiguratorRevision;
			Check(File.Exists(Path.Combine(repo.Profiles, "101.json")) && !string.IsNullOrEmpty(configuratorRevision), "Zapis posterunku tworzy profil i rewizję dla EDR.");
			Reject(() =>
			{
				repo.Register(iz, "100", Station("100", "Dorota"));
			}, "Posterunku nie można przypisać bezpośrednio do IZ.");
			Reject(() =>
			{
				repo.Register(ise2, "101", Station("101", "Nadpisanie"));
			}, "Dodanie listą nie przejmuje posterunku przypisanego do innej sekcji.");
			string text = Json.Text(repo.Data);
			Reject(() =>
			{
				repo.MoveSection(ise, iz2);
			}, "Przeniesienie sekcji z kolidującą nazwą jest odrzucane.");
			Check(text == Json.Text(repo.Data), "Nieudana zmiana pozostawia strukturę bez zmian.");
			repo.RenameUnit(ise, "Juliusz");
			repo.MoveSection(ise, iz2);
			Check(repo.Data.Units.First((ConfigurationUnit x) => x.Id == ise).ParentId == iz2 && repo.Data.Stations.Single().UnitId == ise, "Przeniesienie sekcji zachowuje jej posterunki.");
			repo.MoveStation("101", ise2);
			Check(repo.Data.Stations.Single().UnitId == ise2 && repo.Data.Stations[0].Station.ConfiguratorRevision == configuratorRevision, "Przeniesienie posterunku zachowuje konfigurację i rewizję.");
			Reject(() =>
			{
				repo.MoveStation("101", iz);
			}, "Przeniesienie posterunku wymaga sekcji ISE.");
			Check(repo.PreviewUnitRemoval(iz2).SectionCount == 3 && repo.PreviewUnitRemoval(iz2).StationCount == 1, "Podgląd usunięcia IZ obejmuje jego sekcje i przypisany posterunek.");
			Check(repo.PreviewUnitRemoval(ise2).SectionCount == 1 && repo.PreviewUnitRemoval(ise2).StationCount == 1, "Podgląd usunięcia ISE obejmuje jej posterunki.");
			repo.RemoveUnit(twin);
			Check(!repo.Data.Units.Any((ConfigurationUnit x) => x.Id == twin), "Pustą sekcję można usunąć.");
			repo.UnregisterStation("101");
			repo.RemoveUnit(ise2);
			Check(repo.Data.Stations.Single().UnitId == null && repo.Data.Stations.Single().Registered == false && File.Exists(Path.Combine(repo.Profiles, "101.json")), "Usunięcie pustej sekcji zachowuje nieprzypisany profil posterunku.");
			repo.MoveStation("101", ise);
			repo.SaveTreeState(new string[4]
			{
				"root",
				"u:" + iz2,
				"u:" + ise,
				"p:" + ise
			}, "s:101");
			ConfiguratorRepository configuratorRepository = new ConfiguratorRepository("simrail", root);
			configuratorRepository.Load();
			Check(configuratorRepository.Data.Units.First((ConfigurationUnit x) => x.Id == ise).ParentId == iz2 && configuratorRepository.Data.Stations.Single().UnitId == ise && configuratorRepository.Data.Stations.Single().Station.ConfiguratorRevision == configuratorRevision, "Restart odtwarza zmienioną hierarchię, przypisanie i ustawienia posterunku.");
			Check(configuratorRepository.Data.SelectedNode == "s:101" && configuratorRepository.Data.ExpandedNodes.Contains("u:" + ise), "Zapisywany jest wybrany posterunek i rozwinięte gałęzie drzewa.");
			Check(File.Exists(repo.DataFile + ".bak"), "Zmiany struktury zachowują kopię poprzedniego rejestru.");
			List<ConfiguredStation> list = ConfigurationCatalog.Build(Snapshot(), null, CancellationToken.None);
			repo.SaveCatalog(list);
			Check(repo.ReadCatalog().ServerCode == "pl3" && repo.ReadCatalog().Stations.Count == 3, "Katalog PL3 jest zapisany i odczytywany lokalnie.");
			ConfiguratorRepository configuratorRepository2 = new ConfiguratorRepository("simrail", root);
			configuratorRepository2.Load();
			Check(configuratorRepository2.ReadCatalog().Stations.Any((ConfiguredStation x) => x.Key == "101"), "Katalog PL3 działa po ponownym uruchomieniu bez pobierania.");
			List<ConfiguredStation> wrong = list.Select(Json.Clone).ToList();
			wrong[0].Key = "../bad";
			Reject(() =>
			{
				repo.SaveCatalog(wrong);
			}, "Nieprawidłowy identyfikator nie jest zapisywany do katalogu.");
			Check(repo.ReadCatalog().Stations.Count == 3, "Nieudany zapis pozostawia wcześniejszy katalog PL3.");
			Feed feed = new Feed();
			using (StationRegistrationForm stationRegistrationForm = new StationRegistrationForm(repo, ise, feed, autoLoad: false))
			{
				stationRegistrationForm.Show();
				Application.DoEvents();
				Check(Field<ListBox>(stationRegistrationForm, "assigned").Items.Count == 1 && Field<ListBox>(stationRegistrationForm, "available").Items.Count == 2, "Lista pokazuje zapisany posterunek oraz nieprzypisane pozycje PL3.");
				Call(stationRegistrationForm, "LoadStations");
				WaitFor(stationRegistrationForm);
				Check(feed.LastServer == "pl3" && !Controls(stationRegistrationForm).OfType<ComboBox>().Any(), "Pobieranie używa wyłącznie PL3 bez wyboru serwera.");
				Field<TextBox>(stationRegistrationForm, "search").Text = "Dorota";
				Check(Field<ListBox>(stationRegistrationForm, "available").Items.Count == 1, "Wyszukiwanie zawęża listę posterunków.");
				Field<ListBox>(stationRegistrationForm, "available").SelectedIndex = 0;
				Controls(stationRegistrationForm).OfType<Button>().First((Button x) => x.Text == "→").PerformClick();
				Check(Field<ListBox>(stationRegistrationForm, "assigned").Items.Count == 2, "Strzałka dodaje wybrany posterunek do sekcji.");
				feed.Fail = true;
				Call(stationRegistrationForm, "LoadStations");
				WaitFor(stationRegistrationForm);
				Check(Field<Label>(stationRegistrationForm, "status").Text.Contains("Testowy brak połączenia") && Field<ListBox>(stationRegistrationForm, "assigned").Items.Count == 2, "Błąd PL3 pokazuje przyczynę i zachowuje wybrane posterunki.");
				Ui.Capture(stationRegistrationForm, Path.Combine(folder, "lista-pl3.png"));
				Controls(stationRegistrationForm).OfType<Button>().First((Button x) => x.Text == "Zapisz").PerformClick();
			}
			Check(repo.Data.Stations.Count == 2 && repo.Data.Stations.All((ConfiguredStation x) => x.Registered != false && x.UnitId == ise), "Zapis listy utrwala przypisanie obu posterunków.");
			feed.Fail = false;
			using (StationRegistrationForm stationRegistrationForm2 = new StationRegistrationForm(repo, ise, feed, autoLoad: false))
			{
				stationRegistrationForm2.Show();
				Application.DoEvents();
				Field<ListBox>(stationRegistrationForm2, "assigned").SelectedIndex = 0;
				Controls(stationRegistrationForm2).OfType<Button>().First((Button x) => x.Text == "←").PerformClick();
				Check(Field<ListBox>(stationRegistrationForm2, "assigned").Items.Count == 1, "Strzałka w lewo usuwa pozycję z przygotowanej listy.");
				stationRegistrationForm2.Close();
			}
			Check(Enumerable.Count(repo.Data.Stations, (ConfiguredStation x) => x.Registered != false) == 2, "Anulowanie listy nie usuwa przypisania ze struktury.");
			Feed feed2 = new Feed();
			feed2.Delay = true;
			Feed feed3 = feed2;
			using (StationRegistrationForm stationRegistrationForm3 = new StationRegistrationForm(repo, ise, feed3, autoLoad: true))
			{
				stationRegistrationForm3.Show();
				Application.DoEvents();
				Check(feed3.LastServer == "pl3" && Field<bool>(stationRegistrationForm3, "loading"), "Lista PL3 jest pobierana automatycznie po otwarciu.");
				Controls(stationRegistrationForm3).OfType<Button>().First((Button x) => x.Text == "Zapisz").PerformClick();
				Application.DoEvents();
				Check(stationRegistrationForm3.DialogResult == DialogResult.OK && feed3.Pending.Task.IsCanceled, "Wolna odpowiedź nie blokuje zapisu istniejącej lokalnej listy.");
			}
			using (AdministrationForm administrationForm = new AdministrationForm(repo))
			{
				administrationForm.Show();
				Application.DoEvents();
				TreeView treeView = Field<TreeView>(administrationForm, "tree");
				List<TreeNode> source = treeView.Nodes.Cast<TreeNode>().SelectMany(Nodes).ToList();
				Check(!source.Any((TreeNode x) => new string[5] { "Użytkownicy", "Pracownicy", "Operatorzy", "Koordynatorzy", "Obserwatorzy" }.Contains(x.Text)), "Drzewo nie zawiera użytkowników, pracowników ani grup uprawnień.");
				TreeNode treeNode = source.First((TreeNode x) => x.Name == "u:" + ise);
				Check(treeNode.Parent.Name == "u:" + iz2 && treeNode.Parent.Text.StartsWith("IZ ") && treeNode.Text.StartsWith("ISE "), "Drzewo pokazuje rzeczywiste zagnieżdżenie IZ → ISE.");
				Check(treeView.ImageList.Images.ContainsKey("iz") && treeView.ImageList.Images.ContainsKey("ise") && treeView.ImageList.Images.ContainsKey("station") && treeView.ShowPlusMinus && !treeView.ShowLines, "Drzewo ma ikony zakładów, sekcji i posterunków oraz strzałki rozwijania.");
				typeof(AdministrationForm).GetField("contextOnBackground", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(administrationForm, true);
				Call(administrationForm, "FillContextMenu");
				ContextMenuStrip context = Field<ContextMenuStrip>(administrationForm, "context");
				Check(context.Items.OfType<ToolStripMenuItem>().Any((ToolStripMenuItem x) => x.Text.Contains("Dodaj zakład")), "Menu pod prawym przyciskiem na korzeniu zawiera dodawanie IZ.");
				NameDialog(() =>
				{
					context.Items.OfType<ToolStripMenuItem>().First().PerformClick();
				}, "Poznań");
				string created = repo.Data.Units.First((ConfigurationUnit x) => x.Name == "Poznań").Id;
				Check(repo.Data.Units.Any((ConfigurationUnit x) => x.Id == created && x.Kind == "IZ"), "Dodawanie zakładu przez menu zapisuje nowy IZ.");
				Call(administrationForm, "FillContextMenu");
				NameDialog(() =>
				{
					context.Items.OfType<ToolStripMenuItem>().First((ToolStripMenuItem x) => x.Text.Contains("Dodaj sekcję")).PerformClick();
				}, "Piła");
				string newSection = repo.Data.Units.First((ConfigurationUnit x) => x.Name == "Piła").Id;
				Check(repo.Data.Units.First((ConfigurationUnit x) => x.Id == newSection).ParentId == created, "Dodawanie sekcji przez menu zapisuje ją pod wybranym IZ.");
				Call(administrationForm, "FillContextMenu");
				Check(context.Items.OfType<ToolStripMenuItem>().Any((ToolStripMenuItem x) => x.Text.Contains("Dodaj posterunki z PL3")) && context.Items.OfType<ToolStripMenuItem>().Any((ToolStripMenuItem x) => x.Text.Contains("Przenieś do zakładu")), "Menu sekcji pozwala dodawać posterunki PL3 i przenosić sekcję.");
				treeView.SelectedNode = treeView.Nodes.Cast<TreeNode>().SelectMany(Nodes).First((TreeNode x) => x.Name == "s:101");
				Application.DoEvents();
				StationConfigurationForm stationConfigurationForm = Field<StationConfigurationForm>(administrationForm, "editor");
				Check(stationConfigurationForm != null && !stationConfigurationForm.TopLevel && Controls(stationConfigurationForm).OfType<TabControl>().First().TabPages.Count == 5, "Kliknięcie posterunku otwiera normalny konfigurator pięciu zakładek.");
				Check(!stationConfigurationForm.HasPendingChanges, "Otwarcie konfiguratora nie tworzy niezapisanych zmian.");
				Call(administrationForm, "FillContextMenu");
				Field<ComboBox>(stationConfigurationForm, "postType").Text = "STACJA";
				context.Items.OfType<ToolStripMenuItem>().First((ToolStripMenuItem x) => x.Text == "Konfiguruj posterunek").PerformClick();
				Check(Field<StationConfigurationForm>(administrationForm, "editor") == stationConfigurationForm && stationConfigurationForm.HasPendingChanges, "Ponowne polecenie Konfiguruj nie odrzuca edytowanych ustawień.");
				Field<RadioButton>(stationConfigurationForm, "customRelease").Checked = true;
				Field<NumericUpDown>(stationConfigurationForm, "releaseSeconds").Value = 90m;
				stationConfigurationForm.SaveDraft();
				Check(!stationConfigurationForm.HasPendingChanges && repo.Data.Stations.First((ConfiguredStation x) => x.Key == "101").Station.Options.AutomaticReleaseSeconds == 90, "Edytor zapisuje ustawienia do tego samego profilu posterunku.");
				Ui.Capture(administrationForm, Path.Combine(folder, "konfigurator-struktura.png"));
				TabControl tabControl = Controls(stationConfigurationForm).OfType<TabControl>().First();
				tabControl.SelectedIndex = 4;
				Application.DoEvents();
				Ui.Capture(administrationForm, Path.Combine(folder, "konfigurator-inne.png"));
				Call(administrationForm, "SaveView");
				administrationForm.Close();
			}
			ConfiguratorRepository configuratorRepository3 = new ConfiguratorRepository("simrail", root);
			configuratorRepository3.Load();
			using (AdministrationForm administrationForm2 = new AdministrationForm(configuratorRepository3))
			{
				administrationForm2.Show();
				Application.DoEvents();
				TreeView treeView2 = Field<TreeView>(administrationForm2, "tree");
				Check(treeView2.SelectedNode.Name == "s:101" && Field<StationConfigurationForm>(administrationForm2, "editor") != null, "Ponowne otwarcie przywraca zaznaczony posterunek i jego konfigurator.");
				administrationForm2.Size = new Size(1130, 730);
				Application.DoEvents();
				Check(Field<Panel>(administrationForm2, "host").AutoScroll && Field<StationConfigurationForm>(administrationForm2, "editor").Width >= 1120, "Mniejsze okno udostępnia przewijanie pełnego panelu ustawień.");
				administrationForm2.Close();
			}
			ConfiguratorRepository configuratorRepository4 = new ConfiguratorRepository("simrail", Path.Combine(folder, "old028"));
			Directory.CreateDirectory(Path.GetDirectoryName(configuratorRepository4.DataFile));
			AdministrationData administrationData = new AdministrationData();
			administrationData.SchemaVersion = 1;
			administrationData.Provider = "simrail";
			administrationData.Units = new List<ConfigurationUnit>
			{
				new ConfigurationUnit
				{
					Id = "old-section",
					Name = "Dawna sekcja"
				}
			};
			administrationData.Employees = new List<ConfigurationEmployee>();
			administrationData.Assignments = new List<ConfigurationAssignment>();
			administrationData.Stations = new List<ConfiguredStation>
			{
				new ConfiguredStation
				{
					Key = "101",
					UnitId = "old-section",
					Registered = true,
					Station = Station("101", "Juliusz")
				}
			};
			AdministrationData value = administrationData;
			byte[] array = Json.Bytes(value);
			File.WriteAllBytes(configuratorRepository4.DataFile, array);
			configuratorRepository4.Load();
			Check(configuratorRepository4.Data.SchemaVersion == 2 && configuratorRepository4.Data.Units.First((ConfigurationUnit x) => x.Id == "old-section").Kind == "ISE" && Enumerable.Count(configuratorRepository4.Data.Units, (ConfigurationUnit x) => x.Kind == "IZ") == 1, "Wersja 0.28 jest migrowana do hierarchii IZ/ISE.");
			Check(configuratorRepository4.Data.Stations.Single().UnitId == "old-section" && configuratorRepository4.Data.Stations.Single().Station.Sections[0].Id == "a", "Migracja zachowuje przypisanie posterunku i identyfikatory dzienników.");
			Check(File.ReadAllBytes(configuratorRepository4.DataFile + ".bak").SequenceEqual(array), "Migracja zachowuje dokładną kopię starego rejestru.");
			configuratorRepository4.Load();
			Check(Enumerable.Count(configuratorRepository4.Data.Units, (ConfigurationUnit x) => x.Kind == "IZ") == 1, "Ponowne uruchomienie nie powiela zakładu po migracji.");
			ConfiguratorRepository configuratorRepository5 = new ConfiguratorRepository("simrail", Path.Combine(folder, "profile-only"));
			Directory.CreateDirectory(configuratorRepository5.Profiles);
			File.Copy(Path.Combine(repo.Profiles, "101.json"), Path.Combine(configuratorRepository5.Profiles, "101.json"));
			configuratorRepository5.Load();
			Check(configuratorRepository5.Data.Units.Count == 2 && configuratorRepository5.Data.Stations.Single().Registered == false, "Istniejący profil pozostaje nieprzypisany obok nowej domyślnej struktury.");
			string department = configuratorRepository5.AddDepartment("Test");
			string unit = configuratorRepository5.AddSection(department, "Sekcja");
			configuratorRepository5.SetRegistration(unit, configuratorRepository5.Data.Stations);
			Check(configuratorRepository5.Data.Stations.Single().Registered == true && configuratorRepository5.Data.Stations.Single().Station.Options.AutomaticReleaseSeconds == 90, "Przypisanie istniejącego profilu zachowuje jego ustawienia.");
			string directory = Path.Combine(folder, "runtime");
			Store store = new Store(directory, transient: true);
			store.Load();
			Station st = Station("101", "Juliusz");
			st.SimulatorReviewed = true;
			store.Commit((AppState s) =>
			{
				s.Stations = new List<Station> { st };
				s.Trains.Clear();
				s.Entries.Clear();
				s.Duties.Clear();
				s.History.Clear();
				s.Services.Clear();
			});
			Engine engine = new Engine(store)
			{
				SimRail = new SimRailSession(null, Snapshot(), "101"),
				StationProfileDirectory = repo.Profiles
			};
			store.Commit((AppState s) =>
			{
				s.Entries.Add(new JournalEntry
				{
					Id = "old",
					StationId = st.Id,
					SectionId = "a",
					Time = engine.Now,
					Type = "Wpis",
					Text = "Historia"
				});
			});
			engine.InitializeStationProfiles();
			Check(engine.Station.Options.AutomaticReleaseSeconds == 90 && engine.State.Entries.Single().Text == "Historia", "Profil edytowany w nowej strukturze jest stosowany w EDR bez utraty historii.");
			int count = engine.State.History.Count;
			engine.InitializeStationProfiles();
			Check(engine.State.History.Count == count, "Ta sama rewizja nie jest stosowana drugi raz.");
			Station station = Json.Clone(repo.Data.Stations.First((ConfiguredStation x) => x.Key == "101").Station);
			station.Sections.RemoveAll((Section x) => x.Id == "a");
			station.JournalTabs.Clear();
			repo.SaveStation("101", station);
			engine.PersistStationProfile();
			Check(Json.Read<StationProfile>(File.ReadAllBytes(Path.Combine(repo.Profiles, "101.json"))).Station.ConfiguratorRevision == repo.Data.Stations.First((ConfiguredStation x) => x.Key == "101").Station.ConfiguratorRevision, "Otwarta sesja nie nadpisuje nowszej konfiguracji ze struktury.");
			engine.InitializeStationProfiles();
			Check(engine.Station.Sections.Any((Section x) => x.Id == "a" && x.Conducted == false) && engine.State.Entries.Single().Id == "old", "Zmiana struktury konfiguracji nie usuwa dziennika z wpisami.");
			configuratorRepository4.RenameUnit("old-section", "Nowa nazwa");
			Check(File.ReadAllBytes(configuratorRepository4.DataFile + ".struktura-v1.bak").SequenceEqual(array), "Oddzielna kopia migracji zachowuje oryginał także po kolejnych zapisach struktury.");
			string unit2 = repo.AddSection(iz2, "Inna sekcja");
			repo.Register(unit2, "102", Station("102", "Sosnowiec Dańdówka"));
			using (StationRegistrationForm stationRegistrationForm4 = new StationRegistrationForm(repo, ise, new Feed(), autoLoad: false))
			{
				stationRegistrationForm4.Show();
				Application.DoEvents();
				Check(!Field<ListBox>(stationRegistrationForm4, "available").Items.Cast<ConfiguredStation>().Any((ConfiguredStation x) => x.Key == "102") && Field<ListBox>(stationRegistrationForm4, "assigned").Items.Count == 2, "Lista nie oferuje ponownie posterunku przypisanego do innej sekcji.");
				stationRegistrationForm4.Close();
			}
			ConfiguratorRepository configuratorRepository6 = new ConfiguratorRepository("td2", root);
			configuratorRepository6.Load();
			Check(configuratorRepository6.Data.Units.Count == 2 && configuratorRepository6.Data.Units.All((ConfigurationUnit x) => x.Name == "TTSK") && configuratorRepository6.Data.Stations.Count == 0 && configuratorRepository6.ReadCatalog() == null, "Dane SimRail i katalog PL3 nie pojawiają się w TD2.");
			Reject(() =>
			{
				repo.AddDepartment("IZ Szczecin");
			}, "Prefiks IZ nie pozwala utworzyć drugiego identycznego zakładu.");
			Reject(() =>
			{
				repo.AddSection(iz2, "ISE Juliusz");
			}, "Prefiks ISE nie pozwala zdublować nazwy sekcji.");
			ConfiguratorRepository bad = new ConfiguratorRepository("simrail", Path.Combine(folder, "bad"));
			Directory.CreateDirectory(Path.GetDirectoryName(bad.DataFile));
			File.WriteAllText(bad.DataFile, "broken");
			Reject(() =>
			{
				bad.Load();
			}, "Uszkodzony rejestr nie jest zastępowany pustą strukturą.");
			Check(File.ReadAllText(bad.DataFile) == "broken", "Wadliwy oryginalny plik pozostaje zachowany.");
			File.WriteAllText(repo.CatalogFile, "broken");
			Check(repo.ReadCatalog() == null && File.ReadAllText(repo.CatalogFile) == "broken", "Wadliwy katalog jest pomijany bez jego kasowania.");
			BranchRemoval31Tests.Run(folder, log);
			log.Add("ALL TESTS PASSED: " + log.Count);
			File.WriteAllLines(Path.Combine(folder, "wyniki-struktura29.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
			return true;
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			File.WriteAllLines(Path.Combine(folder, "wyniki-struktura29.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
			return false;
		}
	}
}

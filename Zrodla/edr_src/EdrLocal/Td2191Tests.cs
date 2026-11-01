using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace EdrLocal;

public static class Td2191Tests
{
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

	private static Td2Schedule Schedule(int id, string remote, string remoteCode)
	{
		string[] array = new string[6] { "Poprzednia", "Małe Groby, po", "Chełminiwaźnice, podg", "Duże Groby, po", "Węglowce", remote };
		string[] array2 = new string[6] { null, "Bz", "Ch-Ch", "Ch-DG", "DG-We", remoteCode };
		string[] array3 = new string[6] { "Lg", "MG-Ch", "Ch-DG", "DG-We", "An", null };
		List<Td2Stop> list = new List<Td2Stop>();
		for (int i = 0; i < array.Length; i++)
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
				departureTime = ((i == array.Length - 1) ? null : text),
				arrivalRealTime = ((i == 0) ? null : text),
				departureRealTime = ((i == array.Length - 1) ? null : text),
				comments = ((i == 4) ? "I,1" : ""),
				pointStopTime = 0,
				confirmed = 1
			});
		}
		Td2Schedule td2Schedule = new Td2Schedule();
		td2Schedule.trainInfo = new Td2TrainInfo
		{
			timetableId = id,
			trainNo = 25454 + id * 2,
			trainCategoryCode = "TME",
			region = "eu"
		};
		td2Schedule.timetablePath = ",Poprzednia aaaaaaaa.sc,Lg;Bz,Węglowce dca58972.sc,An;" + remoteCode + "," + remote + " bbbbbbbb.sc,";
		td2Schedule.stopPoints = list;
		return td2Schedule;
	}

	private static SimRailSnapshot Snapshot(Td2Provider provider, params Td2Schedule[] list)
	{
		return provider.Accept(Json.Bytes(new Td2Envelope
		{
			success = true,
			respCode = 10,
			message = list.ToList()
		}), "eu", new DateTimeOffset(2026, 10, 24, 8, 0, 0, TimeSpan.Zero));
	}

	private static Engine Create(string folder, Td2Provider provider, SimRailSnapshot snapshot)
	{
		SimRailSession simRailSession = new SimRailSession(provider, snapshot, "105");
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

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		bool enabled = LocalPersistence.Enabled;
		LocalPersistence.Enabled = false;
		try
		{
			Td2Provider td2Provider = new Td2Provider();
			Td2Schedule td2Schedule = Schedule(1, "Wielichowo", "Sj");
			Td2Schedule td2Schedule2 = Schedule(2, "Łapanów", "Ew");
			SimRailSnapshot simRailSnapshot = Snapshot(td2Provider, td2Schedule, td2Schedule2);
			SimRailSession simRailSession = new SimRailSession(td2Provider, simRailSnapshot, "105");
			Station station = simRailSession.SuggestStation();
			StationSettings.Normalize(station);
			Check(station.Name == "Węglowce" && station.Td2SceneName == "Węglowce" && station.Td2SceneId == "dca58972", "Stacja i sceneria mają osobne pola, z nazwą i identyfikatorem scenerii z timetablePath.");
			Check(station.Sections.Count == 2 && (from x in station.Sections.Select(StationSettings.Title)
				orderby x
				select x).SequenceEqual(new string[2] { "An", "Chełminiwaźnice" }), "Dzienniki zachowują zewnętrzny An i domyślną nazwę wewnętrznego posterunku.");
			Check(station.Sections.Single((Section x) => x.Td2LocalLine == "An").Td2Kind == "Zewnętrzny" && station.Sections.Single((Section x) => x.Td2LocalLine == "DG-We").Td2Kind == "Wewnętrzny", "Konfiguracja odróżnia szlak zewnętrzny i wewnętrzny.");
			Check(station.Sections.Single((Section x) => x.Td2LocalLine == "DG-We").Td2PeerName == "Chełminiwaźnice" && station.Sections.Single((Section x) => x.Td2LocalLine == "An").Td2PeerName == "", "Wewnętrzny szlak ma rzeczywisty posterunek; zewnętrzny nie utrwala zmiennej podłączonej scenerii.");
			Check(simRailSnapshot.Locations().Single((SimRailLocation x) => x.Id == "103").SceneName == "Węglowce" && simRailSnapshot.Locations().Single((SimRailLocation x) => x.Id == "103").ToString()
				.Contains("Węglowce / Chełminiwaźnice"), "Wybór posterunku pokazuje scenerię i stację / posterunek.");
			Td2Direction td2Direction = Td2Directions.At(td2Schedule, 2, -1);
			Check(td2Direction.LocalLine == "Bz" && td2Direction.Kind == "Zewnętrzny" && td2Direction.RouteKey == "E:BZ", "Wyjazd z podg przez przystanek używa granicznego Bz, a nie wewnętrznego Ch-Ch.");
			Check(Td2Directions.At(td2Schedule, 4, -1).LocalLine == "DG-We" && Td2Directions.At(td2Schedule, 4, -1).Kind == "Wewnętrzny", "Szlak między posterunkami tej samej scenerii zachowuje lokalne DG-We.");
			Engine engine = Create(folder, td2Provider, simRailSnapshot);
			List<Train> source = engine.State.Trains.ToList();
			Train a = source.Single((Train x) => x.ExternalTrainNo == "1");
			Train train = source.Single((Train x) => x.ExternalTrainNo == "2");
			Check(a.To == "Wielichowo" && train.To == "Łapanów" && a.Td2OutgoingKey == train.Td2OutgoingKey, "Różne podłączone scenerie pozostają informacją przy pociągach i wskazują ten sam lokalny wyjazd.");
			Check(SectionRouting.Find(engine.Station.Sections, a, "DO").Td2LocalLine == "An" && SectionRouting.Find(engine.Station.Sections, train, "DO").Td2LocalLine == "An", "Przyjęcie / wyprawienie wybiera dziennik po kluczu lokalnego szlaku, niezależnie od sąsiedniej scenerii.");
			JournalEntry arrival = engine.RegisterScheduledTrain(a.Id, null);
			JournalEntry departure = engine.RegisterScheduledTrain(a.Id, null);
			Check(engine.Station.Sections.Single((Section x) => x.Id == arrival.SectionId).Td2LocalLine == "DG-We" && engine.Station.Sections.Single((Section x) => x.Id == departure.SectionId).Td2LocalLine == "An", "Szybki wpis kieruje przyjazd do DG-We, a odjazd do An.");
			string sectionId = departure.SectionId;
			engine.ApplySimRailSnapshot(Snapshot(td2Provider, td2Schedule, td2Schedule2, Schedule(3, "Nowa sceneria", "Zb")));
			Check(engine.Station.Sections.Count == 2 && engine.Station.Sections.Single((Section x) => x.Td2LocalLine == "An").Id == sectionId && engine.State.Entries.Any((JournalEntry x) => x.Id == departure.Id), "Nowe połączenie tego samego wyjazdu zachowuje dziennik, jego identyfikator i wpisy.");
			Check(SectionRouting.Find(engine.Station.Sections, new Train
			{
				SourceProvider = "TD2",
				Td2OutgoingKey = "E:NIEZNANY"
			}, "DO") == null, "Nieznany szlak nie wybiera losowego dziennika.");
			Migration(station, folder);
			LocalPersistence.Enabled = true;
			try
			{
				Profiles(engine, folder);
			}
			finally
			{
				LocalPersistence.Enabled = false;
			}
			using (StationConfigurationForm stationConfigurationForm = new StationConfigurationForm(engine))
			{
				stationConfigurationForm.Show();
				Pump();
				ConfigurationGrid configurationGrid = Field<ConfigurationGrid>(stationConfigurationForm, "sectionGrid");
				Check(stationConfigurationForm.Text == "Konfiguracja posterunku - Węglowce" && All(stationConfigurationForm).OfType<Label>().Any((Label x) => x.Text.Contains("Sceneria: Węglowce")) && configurationGrid.Columns.Contains("Td2Kind") && configurationGrid.Columns["Td2LocalLine"].HeaderText == "Szlak w TD2", "Konfigurator pokazuje scenerię, stację, oznaczenie oraz rodzaj szlaku.");
				Check(All(stationConfigurationForm).OfType<Label>().Any((Label x) => x.Text.Contains("Nazwy i linie ustawiane")), "Konfigurator wyjaśnia, według czego są dzielone dzienniki TD2.");
				Ui.Capture(stationConfigurationForm, Path.Combine(folder, "td2-konfiguracja.png"));
				stationConfigurationForm.SaveDraft();
				Pump();
			}
			Check(engine.Station.Sections.All((Section x) => StationSettings.Title(x) == Td2SectionSettings.AutomaticTitle(x)) && engine.State.Trains.Single((Train x) => x.Id == a.Id).To == "Wielichowo", "Zapis konfiguracji zachowuje oznaczenia dzienników oraz faktyczny kierunek konkretnego pociągu.");
			using (MainForm mainForm = new MainForm(engine))
			{
				mainForm.Show();
				Pump();
				mainForm.GetType().GetField("useTimeRange", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(mainForm, false);
				mainForm.RefreshData();
				Pump();
				Ui.Capture(mainForm, Path.Combine(folder, "td2-dzienniki.png"));
				mainForm.Close();
				Pump();
			}
			Section archived = Json.Clone(engine.Station.Sections.Single((Section x) => x.Td2LocalLine == "An"));
			archived.Id = "archive-report-check";
			archived.Td2Archived = true;
			archived.Conducted = false;
			engine.Store.Commit((AppState state) =>
			{
				state.Stations.First((Station x) => x.Id == engine.StationId).Sections.Insert(0, archived);
			});
			using (JournalReportForm journalReportForm = new JournalReportForm(engine, tel: false, engine.Now.Date))
			{
				ComboBox comboBox = Field<ComboBox>(journalReportForm, "section");
				Check(comboBox.SelectedIndex == 0 && comboBox.Text == "An (archiwalny)", "Wyciąg umożliwia wskazanie zachowanego archiwalnego dziennika według oznaczenia szlaku.");
				journalReportForm.ShowReport();
				Check(Field<Section>(Field<JournalReport>(journalReportForm, "report"), "reportSection").Id == archived.Id, "Wyciąg archiwum korzysta z właściwego identyfikatora, także przy powtórzonym oznaczeniu.");
				comboBox.SelectedIndex = 2;
				journalReportForm.ShowReport();
				Check(Field<Section>(Field<JournalReport>(journalReportForm, "report"), "reportSection").Id == sectionId, "Wyciąg aktywnego An nie pobiera wpisów z innego dawnego dziennika.");
			}
			Live(folder);
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			File.WriteAllLines(Path.Combine(folder, "wyniki-td2191.txt"), log, Encoding.UTF8);
			return false;
		}
		finally
		{
			LocalPersistence.Enabled = enabled;
		}
		File.WriteAllLines(Path.Combine(folder, "wyniki-td2191.txt"), log, Encoding.UTF8);
		return true;
	}

	private static void Migration(Station proposed, string folder)
	{
		Station legacy = Json.Clone(proposed);
		legacy.SimulatorReviewed = true;
		Section section = legacy.Sections.Single((Section x) => x.Td2LocalLine == "An");
		section.Td2RouteKey = null;
		section.Td2Kind = null;
		section.Name = "Wielichowo";
		section.Line = "An/Sj";
		section.JournalName = "Wielichowo";
		section.Id = "td2-section-legacy1";
		section.ManualConfiguration = true;
		section.Tracks = 2;
		section.Basis = "Wieloodstępowa (samoczynna) blokada liniowa";
		section.Crossings.Add(new CrossingSetting
		{
			Id = "keeper",
			Name = "Dróżnik",
			Kilometre = "1,200",
			NotificationMode = "Znak"
		});
		Section duplicate = Json.Clone(section);
		duplicate.Id = "td2-section-legacy2";
		duplicate.Name = "Łapanów";
		duplicate.Line = "An/Ew";
		duplicate.JournalName = "Łapanów";
		legacy.Sections.Add(duplicate);
		legacy.JournalTabs = new List<JournalTabDefinition>
		{
			new JournalTabDefinition
			{
				LeftId = section.Id,
				RightId = duplicate.Id
			}
		};
		List<JournalEntry> list = new List<JournalEntry>();
		list.Add(new JournalEntry
		{
			Id = "history1",
			StationId = legacy.Id,
			SectionId = section.Id
		});
		list.Add(new JournalEntry
		{
			Id = "history2",
			StationId = legacy.Id,
			SectionId = duplicate.Id
		});
		List<JournalEntry> list2 = list;
		Td2Topology.Reconcile(legacy, proposed, list2);
		Section active = legacy.Sections.Single((Section x) => !x.Td2Archived && x.Td2RouteKey == "E:AN");
		Check(active.Id == section.Id && active.Tracks == 2 && active.Basis == "Wieloodstępowa (samoczynna) blokada liniowa" && active.Crossings.Single().Id == "keeper", "Migracja zachowuje identyfikator, liczbę torów, blokadę i dróżnika dotychczasowego dziennika.");
		Check(legacy.Sections.Single((Section x) => x.Id == duplicate.Id).Td2Archived && list2.All((JournalEntry x) => legacy.Sections.Any((Section y) => y.Id == x.SectionId)), "Pozostały dawny dziennik z historią zostaje w archiwum; wpisy nie są usuwane ani scalane.");
		Check(legacy.JournalTabs.SelectMany((JournalTabDefinition x) => new string[2] { x.LeftId, x.RightId }).Count((string x) => x == active.Id) == 1 && StationSettings.Title(active) == "An", "Układ zakładek wskazuje jeden aktywny dziennik An po migracji.");
		Station station = Json.Clone(legacy);
		Td2Topology.Reconcile(station, proposed, list2);
		Check(Json.Text(station) == Json.Text(legacy), "Ponowne odświeżenie nie powtarza migracji i nie tworzy kolejnych dzienników.");
	}

	private static void Profiles(Engine engine, string folder)
	{
		ConfiguratorRepository configuratorRepository = new ConfiguratorRepository("td2", Path.Combine(folder, "configuration"));
		configuratorRepository.Load();
		string defaultSectionId = configuratorRepository.Data.DefaultSectionId;
		Check(!string.IsNullOrEmpty(defaultSectionId), "Konfigurator TD2 ma lokalną sekcję do zapisania profilu.");
		configuratorRepository.Register(defaultSectionId, engine.Station.SimRailPointId, engine.Station);
		configuratorRepository.SaveStation(engine.Station.SimRailPointId, engine.Station);
		StationProfile stationProfile = Json.Read<StationProfile>(File.ReadAllBytes(Path.Combine(configuratorRepository.Profiles, "105.json")));
		Check(stationProfile.Station.SimRailPointId == "105" && stationProfile.Station.Sections.All((Section x) => !string.IsNullOrEmpty(x.Td2RouteKey)), "Profil TD2 zachowuje identyfikator stacji i klucze szlaków w stałym katalogu konfiguracji.");
		configuratorRepository.Load();
		Engine engine2 = configuratorRepository.DraftEngine("105");
		using (StationConfigurationForm stationConfigurationForm = new StationConfigurationForm(engine2))
		{
			Check(engine2.IsTd2 && stationConfigurationForm.Text == "Konfiguracja posterunku - Węglowce" && Field<ConfigurationGrid>(stationConfigurationForm, "sectionGrid").Columns.Contains("Td2Kind"), "Ten sam podział TD2 działa również w samodzielnym konfiguratorze bez aktywnej sesji gry.");
		}
		engine.StationProfileDirectory = configuratorRepository.Profiles;
		engine.InitializeStationProfiles();
		Check(engine.Station.ConfiguratorRevision == stationProfile.Station.ConfiguratorRevision, "Zapis z konfiguratora jest stosowany do sesji TD2.");
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
		SimRailSession simRailSession = new SimRailSession(td2Provider, simRailSnapshot, "1704127545304");
		Station station = simRailSession.SuggestStation();
		Check(station.Sections.Any((Section x) => x.Td2LocalLine == "An" && x.Td2Kind == "Zewnętrzny") && station.Sections.Any((Section x) => x.Td2LocalLine == "DG-We" && x.Td2Kind == "Wewnętrzny") && Enumerable.Count(station.Sections, (Section x) => x.Td2LocalLine == "An") == 1, "Rzeczywiste rozkłady Węglowców tworzą jeden zewnętrzny An i wewnętrzny DG-We.");
		Station station2 = new SimRailSession(td2Provider, simRailSnapshot, "1704127791408").SuggestStation();
		Check(station2.Sections.Any((Section x) => x.Td2LocalLine == "Bz" && x.Td2Kind == "Zewnętrzny") && !station2.Sections.Any((Section x) => x.Td2LocalLine == "Ch-Ch" && x.Td2Kind == "Zewnętrzny"), "Rzeczywiste Chełminiwaźnice prowadzą zewnętrzny Bz przez przystanek, bez fałszywego Ch-Ch.");
		int num = 0;
		bool flag = true;
		foreach (SimRailLocation item in simRailSnapshot.Locations())
		{
			SimRailSession simRailSession2 = new SimRailSession(td2Provider, simRailSnapshot, item.Id);
			Station station3 = simRailSession2.SuggestStation();
			foreach (Train item2 in simRailSession2.Project(station3))
			{
				if (!string.IsNullOrEmpty(item2.Td2IncomingKey))
				{
					flag &= SectionRouting.Find(station3.Sections, item2, "Z") != null;
					num++;
				}
				if (!string.IsNullOrEmpty(item2.Td2OutgoingKey))
				{
					flag &= SectionRouting.Find(station3.Sections, item2, "DO") != null;
					num++;
				}
			}
		}
		Check(flag && num > 0, "Wszystkie kierunki rzeczywistych wizyt TD2 mają jednoznaczny lokalny dziennik (" + num + ").");
	}
}

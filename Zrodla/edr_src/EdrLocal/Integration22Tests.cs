using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace EdrLocal;

public static class Integration22Tests
{
	private static readonly List<string> log = new List<string>();

	private static void Check(bool value, string label)
	{
		if (!value)
		{
			throw new Exception("FAIL: " + label);
		}
		log.Add("PASS: " + label);
	}

	private static void Reject(Action action, string label)
	{
		bool value = false;
		try
		{
			action();
		}
		catch
		{
			value = true;
		}
		Check(value, label);
	}

	private static T Field<T>(object o, string name)
	{
		return (T)o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
	}

	private static void Call(object o, string name)
	{
		o.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, null);
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

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		try
		{
			List<SimRailTrain> trains = SimRailProvider.Parse<List<SimRailTrain>>(File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src", "testdata", "neighbours22.json")));
			SimRailSnapshot snapshot = new SimRailSnapshot
			{
				ServerCode = "test",
				Trains = trains,
				DispatcherNames = new HashSet<string> { "Baby", "Rokiciny", "Zawiercie" },
				GameTime = new DateTime(2026, 10, 18, 19, 40, 0),
				DownloadedUtc = DateTime.UtcNow
			};
			SimRailSession session = new SimRailSession(new SimRailProvider(), snapshot, "60");
			Action<DateTime> action = (DateTime t) =>
			{
				snapshot.GameTime = t;
				snapshot.DownloadedUtc = DateTime.UtcNow;
				session.Accept(snapshot);
			};
			Check(!snapshot.DispatcherNames.Contains("Piotrków Trybunalski") && snapshot.Locations().Any((SimRailLocation x) => x.Name == "Piotrków Trybunalski"), "Przypadek odtwarza rzeczywisty brak Piotrkowa na liście grywalnych posterunków.");
			Station station = session.SuggestStation();
			StationSettings.Normalize(station);
			Check(station.Sections.Count == 2 && station.Sections.Any((Section x) => x.Name == "Piotrków Trybunalski") && station.Sections.Any((Section x) => x.Name == "Rokiciny"), "Sugestia Bab korzysta z rozkładu i zawiera rzeczywistego sąsiada spoza listy grywalnej.");
			Check(station.Sections.All((Section x) => x.Name != "Zawiercie"), "Zawiercie nie trafia do konfiguracji Bab z rzeczywistych tras API.");
			Store store = new Store(Path.Combine(folder, "data"));
			store.Load();
			store.Commit((AppState s) =>
			{
				s.Stations = new List<Station> { station };
				s.Trains.Clear();
				s.Entries.Clear();
				s.Duties.Clear();
				s.History.Clear();
				s.Services.Clear();
			});
			Engine engine = new Engine(store)
			{
				SimRail = session,
				User = store.State.Users.First((LocalUser x) => x.Id == "DEMO")
			};
			engine.ApplySimRailSnapshot(snapshot);
			Check(engine.State.Trains.All((Train x) => x.From != "Zawiercie" && x.To != "Zawiercie"), "Oba kierunki pociągów używają poprawnych szlaków.");
			string repaired = engine.Station.Sections.First((Section x) => x.Name == "Piotrków Trybunalski").Id;
			store.Commit((AppState s) =>
			{
				Section section2 = s.Stations[0].Sections.First((Section x) => x.Id == repaired);
				s.Stations[0].SimulatorTopologyVersion = 0;
				string name = (section2.JournalName = "Zawiercie");
				section2.Name = name;
				section2.Basis = "Zapowiadanie telefoniczne";
				s.Entries.Add(new JournalEntry
				{
					Id = "legacy",
					StationId = station.Id,
					SectionId = repaired,
					Time = engine.Now,
					Type = "Wpis",
					Text = "Zachowaj oryginalny wpis",
					User = engine.User.Name
				});
			});
			Check(engine.RepairSuggestedNeighbours(), "Starsza automatyczna sugestia Zawiercia jest naprawiana przy połączeniu.");
			Check(engine.Station.Sections.First((Section x) => x.Id == repaired).Name == "Piotrków Trybunalski" && engine.State.Entries.First((JournalEntry x) => x.Id == "legacy").SectionId == repaired, "Naprawa zachowuje identyfikator dziennika oraz wcześniejsze wpisy.");
			Check(!engine.Station.SimulatorReviewed && engine.State.History.Any((Audit x) => x.Action.Contains("sąsiedztwa")), "Naprawa wymaga przeglądu konfiguracji i zostawia historię.");
			Check(!engine.RepairSuggestedNeighbours(), "Naprawa nie jest wykonywana ponownie na poprawnej konfiguracji.");
			Check(engine.Station.SimulatorTopologyVersion == 2, "Zapamiętanie przeglądu nowej topologii chroni późniejsze ręczne ustawienia.");
			engine.BeginDuty(engine.Now.AddMinutes(-2.0));
			string sectionId = engine.Station.Sections[0].Id;
			Section section = engine.Station.Sections[0];
			section.Basis = "Blokada samoczynna";
			foreach (LineTrackSetting lineTrack in section.LineTracks)
			{
				string basis = (lineTrack.BasisOut = "Wieloodstępowa (samoczynna) blokada liniowa");
				lineTrack.Basis = basis;
			}
			DateTime now = engine.Now;
			JournalEntry movement = engine.Note(sectionId, now, "", "Pociąg");
			movement.Number = "Rob22";
			movement.Direction = "DO";
			movement.Left = now;
			movement.Track = "1";
			engine.SaveJournal(movement);
			now = engine.State.Entries.First((JournalEntry x) => x.Id == movement.Id).LineIndicationStartedAt.Value;
			action(now.AddSeconds(59.0));
			Check(engine.LineStatus(sectionId, 1).StartsWith("ZAJĘTY"), "Wyprawienie na SBL jest zajęte przed upływem 60 sekund.");
			action(now.AddSeconds(60.0));
			Check(engine.LineStatus(sectionId, 1) == "WOLNY", "Wyprawienie na SBL zwalnia zajętość po 60 sekundach bez czasu sąsiada.");
			Check(!engine.State.Entries.First((JournalEntry x) => x.Id == movement.Id).Arrived.HasValue, "Wygaszenie kontrolki nie dopisuje fikcyjnej godziny przyjazdu.");
			engine.Station.Options.AutomaticReleaseSeconds = 120;
			action(now.AddSeconds(119.0));
			Check(engine.LineStatus(sectionId, 1).StartsWith("ZAJĘTY"), "Własny próg 120 sekund utrzymuje zajętość do granicy.");
			action(now.AddSeconds(120.0));
			Check(engine.LineStatus(sectionId, 1) == "WOLNY", "Własny próg zwalnia kontrolkę po 120 sekundach.");
			JournalEntry journalEntry = engine.Note(sectionId, engine.Now, "Telefoniczne", "Telefonogram");
			journalEntry.Effect = "Telefoniczne";
			engine.SaveJournal(journalEntry);
			action(now.AddSeconds(300.0));
			Check(engine.LineStatus(sectionId, 1).StartsWith("ZAJĘTY"), "Telefoniczne zapowiadanie wyłącza automatyczne wygaszanie.");
			engine.Cancel(journalEntry.Id, "Test");
			foreach (LineTrackSetting lineTrack2 in engine.Station.Sections.First((Section x) => x.Id == sectionId).LineTracks)
			{
				string basis2 = (lineTrack2.BasisOut = "Jednoodstępowa (półsamoczynna) blokada liniowa");
				lineTrack2.Basis = basis2;
			}
			Check(engine.LineStatus(sectionId, 1).StartsWith("ZAJĘTY"), "Blokada półsamoczynna nie jest mylona z samoczynną.");
			foreach (LineTrackSetting lineTrack3 in engine.Station.Sections.First((Section x) => x.Id == sectionId).LineTracks)
			{
				string basis3 = (lineTrack3.BasisOut = "Blokada samoczynna");
				lineTrack3.Basis = basis3;
			}
			movement = Json.Clone(engine.State.Entries.First((JournalEntry x) => x.Id == movement.Id));
			movement.ReturnFromLine = true;
			engine.SaveJournal(movement);
			Check(engine.LineStatus(sectionId, 1).StartsWith("ZAJĘTY"), "Jazda z powrotem ze szlaku nie znika po czasie SBL.");
			movement.ReturnFromLine = false;
			movement.Direction = "Z";
			engine.SaveJournal(movement);
			Check(engine.LineStatus(sectionId, 1).StartsWith("ZAJĘTY"), "Zajętość dla przyjęcia nie jest usuwana zegarem wyprawienia.");
			movement.Direction = "DO";
			engine.SaveJournal(movement);
			now = engine.State.Entries.First((JournalEntry x) => x.Id == movement.Id).LineIndicationStartedAt.Value;
			JournalEntry journalEntry2 = engine.Note(sectionId, engine.Now, "Zamknięty", "Telefonogram");
			journalEntry2.Effect = "Zamknij";
			engine.SaveJournal(journalEntry2);
			Check(engine.LineStatus(sectionId, 1) == "ZAMKNIĘTY", "Zamknięcie toru pozostaje nadrzędne nad upływem czasu.");
			engine.Cancel(journalEntry2.Id, "Test");
			engine.Station.Options.AutomaticReleaseSeconds = null;
			action(now.AddSeconds(40.0));
			using (MainForm mainForm = new MainForm(engine))
			{
				mainForm.Show();
				Application.DoEvents();
				JournalPane journalPane = Field<List<JournalPane>>(mainForm, "panes").First((JournalPane x) => x.SectionId == sectionId);
				RailSignal railSignal = Field<List<RailSignal>>(journalPane, "signals").First((RailSignal x) => x.Number == 1);
				journalPane.RefreshSignals();
				Check(railSignal.Status.StartsWith("ZAJĘTY"), "Kontrolka głównego okna pokazuje zajętość przed upływem czasu.");
				action(now.AddSeconds(60.0));
				Call(mainForm, "RefreshRuntimeIndicators");
				Check(railSignal.Status == "WOLNY", "Okresowe odświeżenie ikon usuwa zajętość bez przeładowania dziennika.");
				mainForm.Close();
			}
			using (StationConfigurationForm stationConfigurationForm = new StationConfigurationForm(engine))
			{
				stationConfigurationForm.Show();
				Application.DoEvents();
				TabControl tabControl = All(stationConfigurationForm).OfType<TabControl>().Single();
				tabControl.SelectedIndex = 2;
				ConfigurationGrid configurationGrid = Field<ConfigurationGrid>(stationConfigurationForm, "trackGrid");
				Check(configurationGrid.Columns.Contains("LineNumber") && configurationGrid.Columns.Contains("LineName") && configurationGrid.Columns.Contains("Kind"), "Tory stacyjne mają numer, przypisanie linii, rodzaj i widoczność.");
				StationTrackSetting stationTrackSetting = (StationTrackSetting)configurationGrid.Rows[0].DataBoundItem;
				stationTrackSetting.LineNumber = "1";
				stationTrackSetting.LineName = "Linia testowa";
				stationTrackSetting.Kind = "główny zasadniczy";
				Ui.Capture(stationConfigurationForm, Path.Combine(folder, "tory-stacyjne.png"));
				tabControl.SelectedIndex = 3;
				List<CheckSchedulePanel> list = All(stationConfigurationForm).OfType<CheckSchedulePanel>().ToList();
				Check(list.Count == 4, "Wymagane sprawdzenia mają cztery sekcje zgodne z podręcznikiem.");
				CheckSchedulePanel o = list.First();
				Field<CheckBox>(o, "active").Checked = true;
				Field<CheckBox>(o, "after").Checked = true;
				Field<NumericUpDown>(o, "delay").Value = 3m;
				Field<CheckBox>(o, "daily").Checked = false;
				Ui.Capture(stationConfigurationForm, Path.Combine(folder, "sprawdzenia.png"));
				tabControl.SelectedIndex = 4;
				Field<RadioButton>(stationConfigurationForm, "customRelease").Checked = true;
				Field<NumericUpDown>(stationConfigurationForm, "releaseSeconds").Value = 120m;
				Field<CheckBox>(stationConfigurationForm, "writeSepe").Checked = false;
				Field<RadioButton[]>(stationConfigurationForm, "visibility")[2].Checked = true;
				Ui.Capture(stationConfigurationForm, Path.Combine(folder, "inne.png"));
				Call(stationConfigurationForm, "Save");
			}
			Store store2 = new Store(store.DirectoryPath);
			store2.Load();
			Station station2 = store2.State.Stations[0];
			int? automaticReleaseSeconds = station2.Options.AutomaticReleaseSeconds;
			Check(automaticReleaseSeconds == 120 && automaticReleaseSeconds.HasValue && station2.Options.WriteSepe == false && station2.Options.JournalVisibility == 2, "Opcje Inne przetrwały zapis i ponowne wczytanie.");
			Check(station2.TrackSettings[0].LineNumber == "1" && station2.TrackSettings[0].Kind == "główny zasadniczy", "Przypisanie linii i rodzaj toru są zapisane.");
			RequiredCheckSetting check = engine.Station.RequiredChecks.First((RequiredCheckSetting x) => x.Name == "Łączność z dróżnikami przejazdowymi");
			int? afterDutyMinutes = check.AfterDutyMinutes;
			Check(afterDutyMinutes == 3 && afterDutyMinutes.HasValue && check.DailyChecks == false && check.Enabled, "Harmonogram po przejęciu dyżuru jest zapisany osobno od godzin dobowych.");
			Check(!engine.SepeWritesEnabled, "Wyłączony zapis do SEPE jest respektowany przez silnik.");
			engine.EndDuty(engine.Now);
			engine.BeginDuty(engine.Now);
			DateTime start = engine.ActiveDuty.Start;
			action(start.AddMinutes(2.0));
			Check(!engine.DueChecks().Any((string x) => x.StartsWith(check.Name)), "Sprawdzenie po dyżurze nie jest wymagane za wcześnie.");
			action(start.AddMinutes(3.0));
			Check(engine.DueChecks().Any((string x) => x.StartsWith(check.Name)), "Sprawdzenie staje się wymagane po ustawionej liczbie minut.");
			JournalEntry journalEntry3 = engine.Note("TEL", engine.Now, "Sprawdzono", "Sprawdzenie");
			journalEntry3.CheckName = check.Name;
			engine.SaveJournal(journalEntry3);
			Check(!engine.DueChecks().Any((string x) => x.StartsWith(check.Name)), "Wykonanie zamyka termin sprawdzenia po przejęciu dyżuru.");
			using (StationConfigurationForm stationConfigurationForm2 = new StationConfigurationForm(engine))
			{
				stationConfigurationForm2.Show();
				Application.DoEvents();
				CheckSchedulePanel checkSchedulePanel = All(stationConfigurationForm2).OfType<CheckSchedulePanel>().First();
				Field<CheckBox>(checkSchedulePanel, "daily").Checked = true;
				Field<NumericUpDown>(checkSchedulePanel, "count").Value = 3m;
				List<DateTimePicker> list2 = All(checkSchedulePanel).OfType<DateTimePicker>().ToList();
				list2[0].Value = DateTime.Today.AddHours(5.0);
				list2[1].Value = DateTime.Today.AddHours(13.0);
				list2[2].Value = DateTime.Today.AddHours(19.0);
				Call(stationConfigurationForm2, "Save");
			}
			check = engine.Station.RequiredChecks.First((RequiredCheckSetting x) => x.Name == check.Name);
			Check(check.DailyChecks == true && check.Hours == "05:00; 13:00; 19:00", "Zmiana liczby sprawdzeń tworzy i zapisuje osobne godziny dobowe.");
			action(engine.Now.Date.AddDays(1.0).AddHours(20.0));
			Check(engine.DueChecks().Any((string x) => x.StartsWith(check.Name)), "Następnego dnia termin dobowy wymaga nowego sprawdzenia.");
			StationTrackSetting stationTrackSetting2 = engine.Station.TrackSettings.Last();
			stationTrackSetting2.Visible = false;
			Check(!StationSettings.TrackChoices(engine.Station, null).Contains(stationTrackSetting2.Number) && StationSettings.TrackChoices(engine.Station, stationTrackSetting2.Number).Contains(stationTrackSetting2.Number), "Ukryty tor znika z nowych wyborów, pozostając dostępny w starym wpisie.");
			string hidden = engine.Station.Sections[1].Id;
			engine.Station.Sections[1].Conducted = false;
			using (MainForm mainForm2 = new MainForm(engine))
			{
				mainForm2.Show();
				Application.DoEvents();
				Check(Field<List<JournalPane>>(mainForm2, "panes").Any((JournalPane x) => x.SectionId == hidden), "Opcja pokazuje także nieprowadzone dzienniki.");
				Check(!engine.CanEditJournal(hidden), "Nieprowadzony dziennik pozostaje tylko do odczytu.");
				Train train = engine.State.Trains.First();
				engine.SetTrainTimes(train.Id, train.PlanArrival, train.PlanDeparture);
				Check(Field<Queue<SepeWriteRequest>>(mainForm2, "outgoingHours").Count == 0 && Field<SepeBackgroundWriter>(mainForm2, "backgroundWriter") == null, "Wyłączenie zapisu SEPE zachowuje godziny lokalnie bez uruchamiania transportu.");
				Reject(() =>
				{
					engine.SaveJournal(engine.Note(hidden, engine.Now, "Nie zapisuj", "Wpis"));
				}, "Ochrona nieprowadzonego dziennika działa również w silniku.");
				mainForm2.Close();
			}
			Check(SepeDiagnostics.Reason(new IOException("Brak połączenia; key=PRIVATE")).Contains("IOException") && !SepeDiagnostics.Reason(new IOException("key=PRIVATE")).Contains("PRIVATE"), "Nieoczekiwany błąd sieciowy zachowuje typ i przyczynę bez ujawniania klucza.");
			SepeWriteRequest request = SepeWriteRequest.From(engine.State.Trains.First(), arrival: true, departure: false);
			string text4 = SepeDiagnostics.Report(request, session, "SEPE: Zbyt późna godzina. https://example.test/?key=PRIVATE&x=1");
			Check(text4.Contains("Zbyt późna godzina") && text4.Contains("Pociąg:") && !text4.Contains("PRIVATE"), "Informacja o błędzie zachowuje przyczynę i pociąg oraz ukrywa klucz.");
			using (SepeErrorForm sepeErrorForm = new SepeErrorForm(text4))
			{
				sepeErrorForm.Show();
				Application.DoEvents();
				Check(sepeErrorForm.Details.ReadOnly && All(sepeErrorForm).OfType<Button>().Any((Button x) => x.Text == "Kopiuj informacje"), "Błąd SEPE ma czytelne szczegóły i przycisk kopiowania.");
				Ui.Capture(sepeErrorForm, Path.Combine(folder, "blad-sepe.png"));
				sepeErrorForm.Close();
			}
			engine.EndDuty(engine.Now);
			log.Add("ALL 0.22 INTEGRATION TESTS PASSED");
			return true;
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			return false;
		}
		finally
		{
			File.WriteAllLines(Path.Combine(folder, "wyniki-poprawki22.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EdrLocal;

public static class Configuration42Tests
{
	private sealed class Feed : ISimulatorFeed
	{
		public string ProviderId => "SimRail";

		public Task<List<SimRailServer>> ServersAsync(CancellationToken t)
		{
			List<SimRailServer> list = new List<SimRailServer>();
			list.Add(new SimRailServer
			{
				ServerCode = "test42",
				ServerName = "Test"
			});
			return Task.FromResult(list);
		}

		public Task<SimRailSnapshot> SnapshotAsync(string code, CancellationToken t)
		{
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

	private static void Choose(ComboBox box, string text)
	{
		box.SelectedIndex = box.Items.Cast<object>().ToList().FindIndex((object x) => x.ToString() == text);
		if (box.SelectedIndex < 0)
		{
			throw new Exception("Missing choice: " + text);
		}
	}

	private static SimRailSnapshot Snapshot()
	{
		SimRailTrain simRailTrain = new SimRailTrain();
		simRailTrain.trainNoLocal = "12345";
		simRailTrain.trainName = "TME";
		simRailTrain.startStation = "A";
		simRailTrain.endStation = "B";
		simRailTrain.carrierName = "Cargo";
		simRailTrain.timetable = new List<SimRailPoint>
		{
			new SimRailPoint
			{
				pointId = "1",
				nameForPerson = "Łódź Marysin",
				indexOfPoint = 0,
				line = 1
			},
			new SimRailPoint
			{
				pointId = "60",
				nameForPerson = "Test",
				indexOfPoint = 1,
				line = 1,
				track = 1,
				platform = "1",
				trainType = "TME",
				displayedTrainNumber = "12345",
				arrivalTime = "2026-10-22 00:04:00",
				departureTime = "2026-10-22 00:04:00",
				isActive = true
			},
			new SimRailPoint
			{
				pointId = "2",
				nameForPerson = "Łódź Andrzejów",
				indexOfPoint = 2,
				line = 1
			}
		};
		SimRailTrain item = simRailTrain;
		SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
		simRailSnapshot.ServerCode = "test42";
		simRailSnapshot.Trains = new List<SimRailTrain> { item };
		simRailSnapshot.DispatcherNames = new HashSet<string> { "Test", "Łódź Marysin", "Łódź Andrzejów" };
		simRailSnapshot.GameTime = new DateTime(2026, 10, 22, 0, 5, 0);
		simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
		return simRailSnapshot;
	}

	private static Engine Make(string folder)
	{
		Store store = new Store(folder);
		store.Load();
		Station station = new Station
		{
			Id = "TEST42",
			Name = "Test",
			PostType = "STACJA",
			SimRailPointId = "60",
			Tracks = new List<string> { "1", "2" },
			Checks = new List<string>(),
			Sections = new List<Section>
			{
				new Section
				{
					Id = "mary",
					Name = "Łódź Marysin",
					Line = "1",
					Tracks = 1,
					Basis = "Blokada samoczynna"
				},
				new Section
				{
					Id = "andr",
					Name = "Łódź Andrzejów",
					Line = "1",
					Tracks = 1,
					Basis = "Blokada samoczynna"
				},
				new Section
				{
					Id = "other",
					Name = "Zgierz",
					Line = "1",
					Tracks = 1,
					Basis = "Blokada samoczynna"
				}
			},
			JournalTabs = new List<JournalTabDefinition>
			{
				new JournalTabDefinition
				{
					LeftId = "mary",
					RightId = "andr"
				},
				new JournalTabDefinition
				{
					LeftId = "other"
				}
			}
		};
		StationSettings.Normalize(station);
		store.Commit((AppState s) =>
		{
			s.Stations = new List<Station> { station };
			s.Trains.Clear();
			s.Entries.Clear();
			s.Duties.Clear();
		});
		Engine engine = new Engine(store);
		engine.User = store.State.Users.First((LocalUser x) => x.Id == "DEMO");
		engine.SimRail = new SimRailSession(null, Snapshot(), "60");
		Engine engine2 = engine;
		engine2.ApplySimRailSnapshot(engine2.SimRail.Snapshot);
		return engine2;
	}

	public static bool Run(string folder)
	{
		Ui.Testing = true;
		Directory.CreateDirectory(folder);
		bool enabled = LocalPersistence.Enabled;
		LocalPersistence.Enabled = false;
		try
		{
			Engine engine = Make(Path.Combine(folder, "session"));
			using (StationConfigurationForm stationConfigurationForm = new StationConfigurationForm(engine))
			{
				stationConfigurationForm.Show();
				Application.DoEvents();
				All(stationConfigurationForm).OfType<TabControl>().Single().SelectedIndex = 1;
				Choose(Field<ComboBox>(stationConfigurationForm, "left"), "Łódź Andrzejów");
				Choose(Field<ComboBox>(stationConfigurationForm, "right"), "Łódź Marysin");
				Check(stationConfigurationForm.HasPendingChanges, "Zmiana samych list stron jest wykrywana jako zmiana konfiguracji.");
				stationConfigurationForm.SaveDraft();
			}
			Check(engine.Station.JournalTabs[0].LeftId == "andr" && engine.Station.JournalTabs[0].RightId == "mary", "Zapisz zatwierdza zamianę stron bez osobnego Zastosuj do zaznaczonej.");
			using (StationConfigurationForm stationConfigurationForm2 = new StationConfigurationForm(engine))
			{
				stationConfigurationForm2.Show();
				Application.DoEvents();
				Check(Field<ComboBox>(stationConfigurationForm2, "left").Text == "Łódź Andrzejów" && Field<ComboBox>(stationConfigurationForm2, "right").Text == "Łódź Marysin", "Ponowne otwarcie konfiguracji w tej sesji zachowuje strony.");
				Choose(Field<ComboBox>(stationConfigurationForm2, "left"), "Łódź Marysin");
				Choose(Field<ComboBox>(stationConfigurationForm2, "right"), "Łódź Andrzejów");
				ConfigurationGrid configurationGrid = Field<ConfigurationGrid>(stationConfigurationForm2, "tabsGrid");
				configurationGrid.CurrentCell = configurationGrid.Rows[1].Cells[1];
				Application.DoEvents();
				configurationGrid.CurrentCell = configurationGrid.Rows[0].Cells[1];
				Application.DoEvents();
				Check(Field<ComboBox>(stationConfigurationForm2, "left").Text == "Łódź Marysin" && Field<ComboBox>(stationConfigurationForm2, "right").Text == "Łódź Andrzejów", "Zmiana zaznaczonej zakładki nie gubi oczekującej pary.");
				Call(stationConfigurationForm2, "MoveTab", 1);
				stationConfigurationForm2.SaveDraft();
			}
			Check(engine.Station.JournalTabs[0].LeftId == "other" && engine.Station.JournalTabs[1].LeftId == "mary", "Przeniesienie zakładki zachowuje jej zmienione strony.");
			engine.ApplySimRailSnapshot(engine.SimRail.Snapshot);
			Check(engine.Station.JournalTabs[1].RightId == "andr", "Odświeżenie SimRail zachowuje układ dzienników.");
			using (MainForm mainForm = new MainForm(engine))
			{
				mainForm.Show();
				mainForm.Size = new Size(1500, 850);
				Application.DoEvents();
				TabControl tabControl = Field<TabControl>(mainForm, "journalTabs");
				TableLayoutPanel tableLayoutPanel = tabControl.TabPages[1].Controls.OfType<TableLayoutPanel>().Single();
				Check(((JournalPane)tableLayoutPanel.GetControlFromPosition(0, 0)).SectionId == "mary" && ((JournalPane)tableLayoutPanel.GetControlFromPosition(1, 0)).SectionId == "andr", "Główny widok respektuje stronę obu dzienników.");
				tabControl.SelectedIndex = 1;
				Ui.Capture(mainForm, Path.Combine(folder, "uklad-1.0.10.199.png"));
				mainForm.BuildJournals();
				Check(engine.Station.JournalTabs[1].LeftId == "mary", "Przebudowa widoku nie odwraca stron.");
				mainForm.Close();
			}
			StationConfigurationForm invalid = new StationConfigurationForm(engine);
			try
			{
				invalid.Show();
				Application.DoEvents();
				ConfigurationGrid configurationGrid2 = Field<ConfigurationGrid>(invalid, "tabsGrid");
				configurationGrid2.CurrentCell = configurationGrid2.Rows[1].Cells[1];
				Choose(Field<ComboBox>(invalid, "left"), "Łódź Marysin");
				Choose(Field<ComboBox>(invalid, "right"), "Łódź Marysin");
				Reject(() =>
				{
					invalid.SaveDraft();
				}, "Zapis odrzuca tę samą stronę dwukrotnie, zamiast ignorować wybór.");
				invalid.Close();
			}
			finally
			{
				if (invalid != null)
				{
					((IDisposable)invalid).Dispose();
				}
			}
			engine.Change("Test dodania dziennika", engine.StationId, (AppState s) =>
			{
				s.Stations[0].Sections.Add(new Section
				{
					Id = "fourth",
					Name = "Łódź Olechów",
					Line = "1",
					Tracks = 1,
					Basis = "Blokada samoczynna"
				});
			});
			StationSettings.Normalize(engine.Station);
			using (StationConfigurationForm stationConfigurationForm3 = new StationConfigurationForm(engine))
			{
				stationConfigurationForm3.Show();
				Application.DoEvents();
				Choose(Field<ComboBox>(stationConfigurationForm3, "left"), "Łódź Olechów");
				Choose(Field<ComboBox>(stationConfigurationForm3, "right"), "(brak drugiego dziennika)");
				Call(stationConfigurationForm3, "SetTab", false);
				stationConfigurationForm3.SaveDraft();
			}
			Check(engine.Station.JournalTabs.Count == 3 && engine.Station.JournalTabs[0].LeftId == "other" && engine.Station.JournalTabs[2].LeftId == "fourth", "Dodaj zakładkę nadal dodaje nowy układ bez nadpisania zaznaczonego.");
			using (StationConfigurationForm stationConfigurationForm4 = new StationConfigurationForm(engine))
			{
				stationConfigurationForm4.Show();
				Application.DoEvents();
				Field<ConfigurationGrid>(stationConfigurationForm4, "tabsGrid").CurrentCell = Field<ConfigurationGrid>(stationConfigurationForm4, "tabsGrid").Rows[1].Cells[1];
				Call(stationConfigurationForm4, "SwapTab");
				stationConfigurationForm4.SaveDraft();
			}
			Check(engine.Station.JournalTabs[1].LeftId == "andr" && engine.Station.JournalTabs[1].RightId == "mary", "Przycisk Zamień strony nadal zapisuje odwróconą parę.");
			using (StationConfigurationForm stationConfigurationForm5 = new StationConfigurationForm(engine))
			{
				stationConfigurationForm5.Show();
				Application.DoEvents();
				Field<ConfigurationGrid>(stationConfigurationForm5, "tabsGrid").CurrentCell = Field<ConfigurationGrid>(stationConfigurationForm5, "tabsGrid").Rows[1].Cells[1];
				Call(stationConfigurationForm5, "DetachTab");
				stationConfigurationForm5.SaveDraft();
			}
			Check(engine.Station.JournalTabs.Count == 4 && engine.Station.JournalTabs[1].LeftId == "andr" && engine.Station.JournalTabs[2].LeftId == "mary", "Odłącz zachowuje kolejność rozdzielonych dzienników.");
			engine = Make(Path.Combine(folder, "branch"));
			engine.BeginDuty(engine.Now.AddMinutes(-2.0));
			JournalEntry history = engine.Note("mary", engine.Now, "Zachowaj historię", "Pociąg");
			history.Number = "88001";
			history.Direction = "Z";
			history.Track = "1";
			engine.SaveJournal(history);
			using (StationConfigurationForm stationConfigurationForm6 = new StationConfigurationForm(engine))
			{
				stationConfigurationForm6.Show();
				Application.DoEvents();
				Field<ComboBox>(stationConfigurationForm6, "postType").Text = "POSTERUNEK ODGAŁĘŹNY";
				Field<BindingList<StationTrackSetting>>(stationConfigurationForm6, "tracks").Clear();
				stationConfigurationForm6.SaveDraft();
			}
			Check(engine.Station.Tracks.Count == 0 && engine.Station.TrackSettings.Count == 0, "Posterunek odgałęźny zapisuje konfigurację bez torów stacyjnych.");
			Check(engine.State.Trains.All((Train x) => x.Track == "" && x.Platform == ""), "Pociągi nie otrzymują fikcyjnego toru stacyjnego ani peronu na tym posterunku.");
			engine.SimRail.Snapshot.Trains[0].timetable[1].track = 9;
			engine.ApplySimRailSnapshot(engine.SimRail.Snapshot);
			Check(engine.Station.Tracks.Count == 0 && engine.State.Trains.Single().Track == "", "Odświeżenie API nie odtwarza usuniętych torów stacyjnych.");
			Check(engine.State.Entries.Single((JournalEntry x) => x.Id == history.Id).Track == "1", "Usunięcie torów z konfiguracji nie przepisuje historycznego wpisu.");
			history.Text = "Poprawiona uwaga";
			engine.SaveJournal(history);
			Check(engine.State.Entries.Single((JournalEntry x) => x.Id == history.Id).Text == "Poprawiona uwaga", "Historyczny wpis pozostaje edytowalny z zachowaniem dawnego numeru toru.");
			history.Track = "9";
			Reject(() =>
			{
				engine.SaveJournal(history);
			}, "Dawny wpis nie może otrzymać nowego nieistniejącego toru.");
			JournalEntry entry = engine.RegisterScheduledTrain(engine.State.Trains.Single().Id, null);
			Check(entry.Track == "" && engine.State.Entries.Any((JournalEntry x) => x.Id == entry.Id), "Nowy pociąg można zarejestrować bez toru stacyjnego.");
			entry.Text = "Uwagi";
			engine.SaveJournal(entry);
			Check(engine.State.Entries.Single((JournalEntry x) => x.Id == entry.Id).Text == "Uwagi", "Nowy wpis bez toru nadal można edytować.");
			JournalEntry invalidTrack = engine.Note("mary", engine.Now, "", "Pociąg");
			invalidTrack.Number = "88003";
			invalidTrack.Track = "1";
			Reject(() =>
			{
				engine.SaveJournal(invalidTrack);
			}, "Nowy wpis nie dziedziczy toru z historycznych zapisów.");
			using (MainForm mainForm2 = new MainForm(engine))
			{
				mainForm2.Show();
				mainForm2.Size = new Size(1500, 850);
				Application.DoEvents();
				Check(Field<FlowLayoutPanel>(mainForm2, "tracks").Controls.Count == 0, "Kontrolka zajętości nie tworzy przycisków fikcyjnych torów.");
				JournalPane journalPane = Field<List<JournalPane>>(mainForm2, "panes").First((JournalPane x) => x.SectionId == "mary");
				DataGridViewRow dataGridViewRow = journalPane.Grid.Rows.Cast<DataGridViewRow>().Single((DataGridViewRow x) => ((JournalEntry)x.Tag).Id == history.Id);
				dataGridViewRow.Cells["notes"].Value = "Edycja w tabeli";
				Check(journalPane.CommitPending(), "Edycja tabeli zachowuje historyczny numer usuniętego toru.");
				Ui.Capture(mainForm2, Path.Combine(folder, "podg-1.0.10.199.png"));
				mainForm2.Close();
			}
			Station station = Json.Clone(engine.Station);
			station.PostType = "STACJA";
			Reject(() =>
			{
				StationSettings.Validate(station, engine.State);
			}, "Zwykła stacja zachowuje wymóg co najmniej jednego toru.");
			station.PostType = "STACJA STYCZNA";
			Reject(() =>
			{
				StationSettings.Validate(station, engine.State);
			}, "Stacja styczna zachowuje walidację torów stacyjnych.");
			station.TrackSettings.Add(new StationTrackSetting
			{
				Number = "1",
				Visible = true
			});
			station.Tracks.Add("1");
			StationSettings.Validate(station, engine.State);
			Check(StationIdentity.Types.Contains("STACJA STYCZNA") && StationIdentity.Caption(station).Contains("(STACJA STYCZNA)"), "Nowy typ jest dostępny i widoczny w tytule.");
			using (StationConfigurationForm stationConfigurationForm7 = new StationConfigurationForm(engine))
			{
				stationConfigurationForm7.Show();
				Application.DoEvents();
				ComboBox comboBox = Field<ComboBox>(stationConfigurationForm7, "postType");
				Check(comboBox.Items.Cast<string>().Contains("STACJA STYCZNA"), "Konfigurator oferuje typ STACJA STYCZNA.");
				comboBox.Text = "STACJA STYCZNA";
				Field<BindingList<StationTrackSetting>>(stationConfigurationForm7, "tracks").Add(new StationTrackSetting
				{
					Number = "1",
					Visible = true
				});
				stationConfigurationForm7.SaveDraft();
			}
			Check(engine.Station.PostType == "STACJA STYCZNA", "Zapis konfiguracji zachowuje nowy typ posterunku.");
			SimRailSnapshot simRailSnapshot = Snapshot();
			simRailSnapshot.Trains[0].timetable[1].nameForPerson = "Starzyny";
			Station station2 = new SimRailSession(null, simRailSnapshot, "60").SuggestStation();
			StationSettings.Normalize(station2);
			Check(station2.Tracks.Count == 0 && StationSettings.NoStationTracks(station2), "Znany posterunek odgałęźny nie dostaje domyślnych torów stacyjnych.");
			using (ModeForm modeForm = new ModeForm())
			{
				modeForm.Show();
				Application.DoEvents();
				List<Button> list = All(modeForm).OfType<Button>().ToList();
				Check(list.Count == 2 && list[0].Text.StartsWith("SimRail") && list[0].Enabled, "Start ma SimRail jako pierwszą dostępną opcję.");
				Check(list[1].Text == "TD2" && list[1].Enabled && !All(modeForm).OfType<Label>().Any((Label x) => x.Text.Contains("kolejnej wersji")), "TD2 jest dostępnym źródłem rozkładu, bez przycisku lokalnego CSV.");
				Ui.Capture(modeForm, Path.Combine(folder, "start-1.0.10.199.png"));
				modeForm.Close();
			}
			using (SimulatorConnectionForm simulatorConnectionForm = new SimulatorConnectionForm(new Feed(), new ConfiguratorRepository("simrail", Path.Combine(folder, "organization")), Path.Combine(folder, "connection")))
			{
				simulatorConnectionForm.Show();
				Application.DoEvents();
				Call(simulatorConnectionForm, "LoadSnapshot");
				Application.DoEvents();
				Label label = All(simulatorConnectionForm).OfType<Label>().Single((Label x) => x.Text.Contains("Link SEPE można"));
				Check(label.Text.Contains("dodatkowe opcje posterunku") && label.Text.Contains("w przeglądarce") && label.Text.Contains("Bez linku godziny nie będą wpisywane automatycznie"), "Połączenie wyjaśnia uzyskanie linku i automatyczne wpisywanie godzin.");
				Ui.Capture(simulatorConnectionForm, Path.Combine(folder, "polaczenie-before.png"));
				log.Add(string.Concat("INFO: note=", label.Bounds, " parent=", label.Parent.ClientSize, " fontHeight=", label.Font.Height));
				Check(label.Bottom <= label.Parent.ClientSize.Height && label.Height >= label.Font.Height * 3, "Instrukcja SEPE mieści trzy czytelne wiersze.");
				Ui.Capture(simulatorConnectionForm, Path.Combine(folder, "polaczenie-1.0.10.199.png"));
				simulatorConnectionForm.Close();
			}
			Check(!Directory.Exists(Path.Combine(folder, "session")) && !Directory.Exists(Path.Combine(folder, "branch")) && !Directory.Exists(Path.Combine(folder, "organization")) && !Directory.Exists(Path.Combine(folder, "connection")) && !LocalPersistence.Enabled, "Konfiguracja i nowe okna nadal nie zapisują danych lokalnie.");
			log.Add("ALL CONFIGURATION 42 TESTS PASSED");
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
			File.WriteAllLines(Path.Combine(folder, "wyniki-konfiguracja42.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

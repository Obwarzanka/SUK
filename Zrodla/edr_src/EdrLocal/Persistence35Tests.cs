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

public static class Persistence35Tests
{
	private sealed class Feed : ISimulatorFeed
	{
		public string ProviderId => "SimRail";

		public Task<List<SimRailServer>> ServersAsync(CancellationToken t)
		{
			List<SimRailServer> list = new List<SimRailServer>();
			list.Add(new SimRailServer
			{
				ServerCode = "persist35",
				ServerName = "Test"
			});
			return Task.FromResult(list);
		}

		public Task<SimRailSnapshot> SnapshotAsync(string server, CancellationToken t)
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

	private static T Field<T>(object obj, string field)
	{
		return (T)obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
	}

	private static object Call(object obj, string method)
	{
		return obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, null);
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

	private static SimRailSnapshot Snapshot()
	{
		SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
		simRailSnapshot.ServerCode = "persist35";
		simRailSnapshot.GameTime = new DateTime(2026, 10, 21, 20, 0, 0);
		simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
		simRailSnapshot.DispatcherNames = new HashSet<string> { "Baby" };
		simRailSnapshot.Trains = new List<SimRailTrain>
		{
			new SimRailTrain
			{
				trainNoLocal = "123",
				trainName = "MPE - TLK „Cookie”",
				carrierName = "Intercity",
				startStation = "A",
				endStation = "B",
				timetable = new List<SimRailPoint>
				{
					Point("59", "Piotrków Trybunalski", 0),
					Point("60", "Baby", 1),
					Point("61", "Rokiciny", 2)
				}
			}
		};
		return simRailSnapshot;
	}

	private static SimRailPoint Point(string id, string name, int at)
	{
		SimRailPoint simRailPoint = new SimRailPoint();
		simRailPoint.pointId = id;
		simRailPoint.nameForPerson = name;
		simRailPoint.indexOfPoint = at;
		simRailPoint.trainType = "MPE";
		simRailPoint.line = 1;
		simRailPoint.track = 1;
		simRailPoint.displayedTrainNumber = "123";
		simRailPoint.arrivalTime = "2026-10-21 19:50:00";
		simRailPoint.departureTime = "2026-10-21 19:52:00";
		simRailPoint.isActive = true;
		return simRailPoint;
	}

	private static Engine Make(string root)
	{
		SimRailSession simRailSession = new SimRailSession(new Feed(), Snapshot(), "60");
		Station station = simRailSession.SuggestStation();
		StationSettings.Normalize(station);
		station.SimulatorReviewed = true;
		Store store = new Store(Path.Combine(root, "data"));
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
		Engine engine = new Engine(store);
		engine.SimRail = simRailSession;
		engine.User = store.State.Users.First((LocalUser x) => x.Id == "DEMO");
		engine.StationProfileDirectory = Path.Combine(root, "profiles");
		engine.UnitSettingsDirectory = Path.Combine(root, "units");
		Engine engine2 = engine;
		engine2.ApplySimRailSnapshot(simRailSession.Snapshot);
		return engine2;
	}

	public static bool Run(string folder)
	{
		Ui.Testing = true;
		Directory.CreateDirectory(folder);
		try
		{
			Engine e = Make(folder);
			Check(e.UnitCaption == "EDR" && Engine.DefaultOrganization("td2") == "TTSK", "Jednostki domyślne to EDR i TTSK, bez IZ/ISE.");
			e.SetUnitCaption("Własna jednostka");
			Engine engine = new Engine(e.Store);
			engine.SimRail = e.SimRail;
			engine.User = e.User;
			engine.UnitSettingsDirectory = e.UnitSettingsDirectory;
			Engine engine2 = engine;
			Check(engine2.UnitCaption == "Własna jednostka", "Zmiana jednostki przetrwała utworzenie nowej sesji.");
			engine2.SimRail = null;
			Engine engine3 = new Engine(e.Store);
			engine3.UnitSettingsDirectory = e.UnitSettingsDirectory;
			Check(engine3.UnitProvider == "simrail", "Odzyskana baza SimRail zachowuje właściwy rodzaj jednostki.");
			Store store = new Store(Path.Combine(folder, "td"));
			store.Load();
			Engine engine4 = new Engine(store);
			engine4.User = store.State.Users[0];
			engine4.UnitSettingsDirectory = e.UnitSettingsDirectory;
			Engine engine5 = engine4;
			Check(engine5.UnitCaption == "TTSK", "Jednostka TD2 pozostaje oddzielna od SimRail.");
			e.SetUnitCaption("");
			Check(e.UnitCaption == "EDR", "Puste pole przywraca jednostkę domyślną.");
			Reject(() =>
			{
				e.SetUnitCaption("A\nB");
			}, "Nazwa jednostki nie może łamać układu stopki.");
			Train t = e.State.Trains.Single();
			Check(t.Carrier == "PKP Intercity S.A." && t.Profile.Name == "MPE - TLK Cookie", "API ma pierwszeństwo, nazwa zachowuje kategorie i nie zawiera cudzysłowów.");
			string[][] array = new string[4][]
			{
				new string[2] { "KS", "Koleje Śląskie Sp. z o.o." },
				new string[2] { "Cargo", "PKP CARGO S.A." },
				new string[2] { "PCC", "PCC" },
				new string[2] { "Nowy przewoźnik", "Nowy przewoźnik" }
			};
			foreach (string[] array2 in array)
			{
				SimRailTrain simRailTrain = new SimRailTrain();
				simRailTrain.carrierName = array2[0];
				Check(SimRailTrainIdentity.Carrier(simRailTrain, "TME") == array2[1], "Odczyt przewoźnika API: " + array2[0] + ".");
			}
			string[] array3 = new string[3] { "IR", "S1", "S41" };
			foreach (string text in array3)
			{
				SimRailTrain simRailTrain2 = new SimRailTrain();
				simRailTrain2.trainName = "ROJ - " + text;
				Check(SimRailTrainIdentity.Carrier(simRailTrain2, "ROJ") == "", "Wspólne oznaczenie " + text + " nie daje zgadywanego przewoźnika.");
			}
			SimRailTrain simRailTrain3 = new SimRailTrain();
			simRailTrain3.trainName = "TME";
			Check(SimRailTrainIdentity.Carrier(simRailTrain3, "TME") == "" && SimRailTrainIdentity.Name("TME", "TME") == "", "Nieznany towarowy ma pustego przewoźnika i nie powtarza kategorii jako nazwy.");
			string[] array4 = new string[6] { "TLK", "IC", "EIC", "EIP", "EC", "ICN" };
			foreach (string text2 in array4)
			{
				SimRailTrain simRailTrain4 = new SimRailTrain();
				simRailTrain4.trainName = "MPE - " + text2;
				Check(SimRailTrainIdentity.Carrier(simRailTrain4, "MPE") == "PKP Intercity S.A.", "Jednoznaczna kategoria handlowa: " + text2 + ".");
			}
			SimRailTrain simRailTrain5 = new SimRailTrain();
			simRailTrain5.trainName = "ROJ - R1";
			int ok;
			if (SimRailTrainIdentity.Carrier(simRailTrain5, "ROJ").StartsWith("Koleje Mazowieckie"))
			{
				SimRailTrain simRailTrain6 = new SimRailTrain();
				simRailTrain6.trainName = "ROJ - ŁS";
				if (SimRailTrainIdentity.Carrier(simRailTrain6, "ROJ").StartsWith("Łódzka"))
				{
					SimRailTrain simRailTrain7 = new SimRailTrain();
					simRailTrain7.trainName = "ROJ - R";
					ok = ((SimRailTrainIdentity.Carrier(simRailTrain7, "ROJ") == "POLREGIO S.A.") ? 1 : 0);
					goto IL_0505;
				}
			}
			ok = 0;
			goto IL_0505;
			IL_0505:
			Check((byte)ok != 0, "Jednoznaczne kategorie regionalne korzystają z podanego przypisania.");
			e.BeginDuty(e.Now.AddMinutes(-10.0));
			JournalEntry entry = e.Note(e.Station.Sections[0].Id, e.Now.AddMinutes(-5.0), "Wpis do odzyskania", "Wpis");
			e.SaveJournal(entry);
			JournalEntry tel = e.Note("TEL", e.Now.AddMinutes(-4.0), "Telefon bez odbiorcy", "Wpis");
			e.SaveJournal(tel);
			e.Change("Dane pociągu", t.Id, (AppState s) =>
			{
				Train train = s.Trains.Single();
				train.Notes = "Własne uwagi";
				train.Analysis = "Analiza";
				train.Profile.CarrierNotes = "Treść przewoźnika";
				s.Services.Add(new ServiceRecord
				{
					Id = "service",
					StationId = e.StationId,
					Train = train.Number,
					Type = "Test",
					Start = e.Now
				});
			});
			e.Store.Flush();
			Store store2 = new Store(e.Store.DirectoryPath);
			store2.Load();
			Check(store2.State.Entries.Any((JournalEntry x) => x.Id == entry.Id) && store2.State.Duties.Any((Duty x) => !x.End.HasValue) && store2.State.Trains.Single().Notes == "Własne uwagi", "Bieżący zapis zachowuje treść dzienników, dyżur i pociągi po ponownym otwarciu.");
			e.ApplySimRailSnapshot(e.SimRail.Snapshot);
			Check(e.State.Trains.Single().Profile.CarrierNotes == "Treść przewoźnika" && e.State.Trains.Single().Analysis == "Analiza", "Odświeżanie API nie usuwa lokalnego profilu i analizy pociągu.");
			SimRailTrain simRailTrain8 = Json.Clone(e.SimRail.Snapshot.Trains.Single());
			simRailTrain8.trainNoLocal = "125";
			simRailTrain8.timetable.ForEach((SimRailPoint x) =>
			{
				x.displayedTrainNumber = "125";
			});
			SimRailSnapshot simRailSnapshot = Snapshot();
			simRailSnapshot.Trains = new List<SimRailTrain> { simRailTrain8 };
			e.ApplySimRailSnapshot(simRailSnapshot);
			Check(e.State.Trains.Count == 2 && e.State.Trains.Any((Train x) => x.Notes == "Własne uwagi"), "Pociąg po opuszczeniu okna API pozostaje w zapisanym rozkładzie.");
			string station = Path.Combine(folder, "baby.posterunek.json");
			string path = Path.Combine(folder, "baby.rozklad.json");
			string path2 = Path.Combine(folder, "baby.sesja.json");
			e.ExportStation(station);
			e.ExportWorking(path, session: false);
			e.ExportWorking(path2, session: true);
			Check(!File.ReadAllText(station).Contains("Wpis do odzyskania") && !File.ReadAllText(station).Contains("trainNoLocal"), "Konfiguracja posterunku nie zawiera rozkładu ani treści dzienników.");
			Check(!File.ReadAllText(path).Contains("Wpis do odzyskania") && Json.Read<WorkingDocument>(File.ReadAllBytes(path)).Entries == null, "Rozkład zapisuje się oddzielnie od wpisów i dyżurów.");
			Check(Json.Read<WorkingDocument>(File.ReadAllBytes(path2)).Entries.Any((JournalEntry x) => x.Id == entry.Id) && !File.ReadAllText(path2).Contains(e.User.Hash), "Sesja zawiera treść dzienników bez danych logowania użytkownika.");
			string text3 = Json.Text(e.Station);
			int count = e.State.Entries.Count;
			e.Change("Zmiana testowa", t.Id, (AppState s) =>
			{
				s.Trains.First((Train x) => x.Id == t.Id).Notes = "Zmienione";
			});
			e.ImportWorking(path, session: false);
			Check(e.State.Trains.First((Train x) => x.Id == t.Id).Notes == "Własne uwagi" && e.State.Entries.Count == count && Json.Text(e.Station) == text3, "Wczytanie rozkładu odtwarza pociągi bez zmiany dzienników i konfiguracji.");
			e.Change("Wyczyszczenie testowe", e.StationId, (AppState s) =>
			{
				s.Entries.Clear();
				s.Duties.Clear();
				s.Services.Clear();
			});
			int writes = 0;
			e.SimRailTimeChanged += (SepeWriteRequest r) =>
			{
				writes++;
			};
			e.ImportWorking(path2, session: true);
			Check(e.State.Entries.Any((JournalEntry x) => x.Id == entry.Id) && e.ActiveDuty != null && e.State.Services.Single().Id == "service" && Json.Text(e.Station) == text3, "Wczytanie sesji odtwarza wpisy, dyżur i usługi, zachowując ustawienia.");
			Check(writes == 0, "Wczytywanie danych nie wysyła godzin do SEPE.");
			Check(e.State.Entries.First((JournalEntry x) => x.Id == tel.Id).TelephoneOrdinal.HasValue, "Numeracja telefoniczna przetrwała zapis i odczyt.");
			WorkingDocument workingDocument = Json.Read<WorkingDocument>(File.ReadAllBytes(path2));
			workingDocument.Server = "other";
			string bad = Path.Combine(folder, "bad.json");
			File.WriteAllBytes(bad, Json.Bytes(workingDocument));
			string text4 = Json.Text(e.State);
			Reject(() =>
			{
				e.ImportWorking(bad, session: true);
			}, "Sesja z innego serwera jest odrzucana.");
			Check(Json.Text(e.State) == text4, "Odrzucony odczyt nie zmienia danych.");
			workingDocument.Server = "persist35";
			workingDocument.Point = "61";
			File.WriteAllBytes(bad, Json.Bytes(workingDocument));
			Reject(() =>
			{
				e.ImportWorking(bad, session: true);
			}, "Sesja z innego posterunku jest odrzucana.");
			Reject(() =>
			{
				e.ImportWorking(station, session: true);
			}, "Konfiguracja nie może zostać pomylona z sesją.");
			Reject(() =>
			{
				e.ExportWorking(e.Store.FilePath, session: false);
			}, "Eksport nie może nadpisać aktywnej bazy.");
			workingDocument = Json.Read<WorkingDocument>(File.ReadAllBytes(path2));
			workingDocument.Trains.Add(Json.Clone(workingDocument.Trains[0]));
			File.WriteAllBytes(bad, Json.Bytes(workingDocument));
			Reject(() =>
			{
				e.ImportWorking(bad, session: true);
			}, "Powtórzony identyfikator pociągu jest odrzucany.");
			workingDocument = Json.Read<WorkingDocument>(File.ReadAllBytes(path2));
			workingDocument.Entries.First().SectionId = "missing";
			File.WriteAllBytes(bad, Json.Bytes(workingDocument));
			Reject(() =>
			{
				e.ImportWorking(bad, session: true);
			}, "Sesja z brakującym dziennikiem jest odrzucana.");
			File.WriteAllText(bad, "broken");
			Reject(() =>
			{
				e.ImportWorking(bad, session: true);
			}, "Uszkodzony plik jest odrzucany.");
			Check(File.ReadAllText(bad) == "broken" && Json.Text(e.State) == text4, "Błędny plik i bieżąca baza pozostały bez zmian.");
			Check(Directory.GetFiles(e.Store.DirectoryPath, "przed-wczytaniem-*.json").Length >= 2, "Przed udanym odczytem powstają osobne kopie odzyskiwania.");
			StationProfile stationProfile = Json.Read<StationProfile>(File.ReadAllBytes(station));
			stationProfile.Station.Sections[0].JournalName = "Własny nagłówek";
			File.WriteAllBytes(station, Json.Bytes(stationProfile));
			string text5 = Json.Text(e.State.Entries);
			string text6 = Json.Text(e.State.Trains);
			e.ImportStation(station);
			Check(e.Station.Sections[0].JournalName == "Własny nagłówek" && Json.Text(e.State.Entries) == text5 && Json.Text(e.State.Trains) == text6, "Wczytanie ustawień zmienia konfigurację i zachowuje treść sesji.");
			Check(File.Exists(Path.Combine(e.StationProfileDirectory, "60.json")), "Wczytana konfiguracja jest zapamiętana niezależnie od bieżącego rozkładu.");
			stationProfile.Station.Sections[0].JournalName = "Drugi zapis profilu";
			File.WriteAllBytes(station, Json.Bytes(stationProfile));
			e.ImportStation(station);
			Check(Json.Read<StationProfile>(File.ReadAllBytes(Path.Combine(e.StationProfileDirectory, "60.json"))).Station.Sections[0].JournalName == "Drugi zapis profilu", "Ponowny import nadpisuje właściwy profil zamiast zatrzymywać poprzednią rewizję.");
			using (MainForm mainForm = new MainForm(e))
			{
				string text7 = Json.Text(e.State.Entries);
				Check(mainForm.FinishSession(ExitSessionChoice.Discard) && Json.Text(e.State.Entries) == text7 && e.ActiveDuty != null, "Nie zapisuj przy wyjściu nie usuwa zatwierdzonych dzienników ani dyżuru.");
			}
			using (ExitSessionForm c = new ExitSessionForm(canReset: true, pending: false))
			{
				Check(!All(c).OfType<Button>().Any((Button x) => x.Text.Contains("Wyczyść")), "Reset nie jest ukryty pod przyciskiem kończenia pracy.");
			}
			string legacy = Engine.RestoreLegacyDatabase(e.Store.FilePath, Path.Combine(folder, "recovered"));
			Store store3 = new Store(legacy);
			store3.Load();
			Check(store3.State.Entries.Any((JournalEntry x) => x.Id == entry.Id) && store3.State.Trains.Any((Train x) => x.Id == t.Id), "Dawny edr.json można odzyskać do nowego folderu z całą treścią.");
			Reject(() =>
			{
				Engine.RestoreLegacyDatabase(e.Store.FilePath, legacy);
			}, "Odzyskiwanie nie nadpisuje istniejącej bazy.");
			ConfiguratorRepository configuratorRepository = new ConfiguratorRepository("simrail", Path.Combine(folder, "org"));
			using (SimulatorConnectionForm simulatorConnectionForm = new SimulatorConnectionForm(new Feed(), configuratorRepository, Path.Combine(folder, "connection")))
			{
				simulatorConnectionForm.Show();
				Application.DoEvents();
				Call(simulatorConnectionForm, "LoadSnapshot");
				ListBox listBox = Field<ListBox>(simulatorConnectionForm, "available");
				listBox.SelectedIndex = listBox.Items.Cast<SimRailLocation>().ToList().FindIndex((SimRailLocation x) => x.Id == "60");
				Check(Field<Button>(simulatorConnectionForm, "connect").Enabled, "Połączenie jest dostępne bez jednostek IZ/ISE.");
				Call(simulatorConnectionForm, "AddSelected");
				Call(simulatorConnectionForm, "Connect");
				Check(simulatorConnectionForm.ConnectedEngine != null && !Application.OpenForms.OfType<OrganizationAssignmentForm>().Any(), "Zwykłe połączenie nie pokazuje konfiguratora ani popupu IZ/ISE.");
				Check(!File.Exists(configuratorRepository.DataFile), "Zwykłe połączenie nie tworzy struktury organizacyjnej.");
				simulatorConnectionForm.DatabaseLock.ReleaseMutex();
				simulatorConnectionForm.DatabaseLock.Dispose();
			}
			using (ModeForm c2 = new ModeForm())
			{
				Check(!All(c2).OfType<Button>().Any((Button x) => x.Text.StartsWith("Konfigurator")), "Konfigurator jest odłożony poza zwykły ekran startowy.");
				Check(All(c2).OfType<Button>().Any((Button x) => x.Text.Contains("dawną bazę")), "Ekran startowy pozwala wczytać dawną bazę.");
			}
			using (MainForm mainForm2 = new MainForm(e))
			{
				mainForm2.Show();
				Application.DoEvents();
				mainForm2.LoadPortable("rozklad", path);
				Check(Field<bool>(mainForm2, "savedTimetableView") && Field<bool>(mainForm2, "hoursPaused") && !Field<CheckBox>(mainForm2, "currentData").Checked, "Wczytanie pliku w oknie wstrzymuje odświeżanie API i wysyłkę godzin.");
				int count2 = e.State.Trains.Count;
				Call(mainForm2, "RefreshRuntimeIndicators");
				Check(e.State.Trains.Count == count2, "Przeliczanie wskaźników nie zastępuje wczytanego rozkładu.");
				DataGridView dataGridView = Field<DataGridView>(mainForm2, "trains");
				Check(dataGridView.Columns["carrier"].Visible && dataGridView.Columns["carrier"].DisplayIndex == dataGridView.Columns.Count - 2, "Przewoźnik jest ostatnią widoczną kolumną wykazu.");
				Check(mainForm2.AccidentCounterCaption == "Czas bez wypadku z winy PLK", "Licznik SimRail nadal mówi o winie PLK.");
				ToolStripStatusLabel toolStripStatusLabel = Field<ToolStripStatusLabel>(mainForm2, "statusUnit");
				Check(toolStripStatusLabel.Text.Contains("EDR") && !toolStripStatusLabel.Text.Contains("IZ "), "Stopka pokazuje samą jednostkę bez ścieżki IZ/ISE.");
				System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer
				{
					Interval = 80
				};
				try
				{
					timer.Tick += (object s, EventArgs a) =>
					{
						Editor editor = Application.OpenForms.OfType<Editor>().LastOrDefault();
						if (editor != null)
						{
							timer.Stop();
							All(editor).OfType<TextBox>().First().Text = "Ukryta zmiana";
							editor.Buttons.Controls.OfType<Button>().First((Button x) => x.Text == "Zapisz").PerformClick();
						}
					};
					timer.Start();
					toolStripStatusLabel.PerformClick();
				}
				finally
				{
					if (timer != null)
					{
						((IDisposable)timer).Dispose();
					}
				}
				Check(e.UnitCaption == "Ukryta zmiana", "Kliknięcie tekstu jednostki otwiera działającą edycję nazwy.");
				mainForm2.Size = new Size(1600, 900);
				Ui.Capture(mainForm2, Path.Combine(folder, "EDR-1.0.10.199.png"));
				Check(mainForm2.FinishSession(ExitSessionChoice.Save), "Zapis przy wyjściu kończy sesję po utrwaleniu danych.");
				mainForm2.Close();
			}
			store2 = new Store(e.Store.DirectoryPath);
			store2.Load();
			Check(store2.State.Entries.Any((JournalEntry x) => x.Id == entry.Id) && store2.State.Trains.Any((Train x) => x.Notes == "Własne uwagi"), "Treści EDR i pociągi przetrwały pełne zamknięcie i odczyt.");
			log.Add("ALL PERSISTENCE 35 TESTS PASSED");
			return true;
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			return false;
		}
		finally
		{
			File.WriteAllLines(Path.Combine(folder, "wyniki-zapis35.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

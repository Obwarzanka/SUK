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

internal static class Configuration170Tests
{
	private sealed class Feed : ISimulatorFeed
	{
		public readonly SimRailSnapshot Value;

		public string ProviderId => "configuration170";

		public Feed(string server)
		{
			Value = new SimRailSnapshot
			{
				ServerCode = server,
				GameTime = new DateTime(2026, 10, 23, 12, 0, 0),
				DispatcherNames = new HashSet<string> { "Korytów" },
				Trains = new List<SimRailTrain>
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
								line = 4,
								arrivalTime = "2026-10-23 11:50:00",
								departureTime = "2026-10-23 11:50:00"
							},
							new SimRailPoint
							{
								pointId = "1852",
								nameForPerson = "Korytów",
								indexOfPoint = 1,
								line = 4,
								track = 1,
								displayedTrainNumber = "2704",
								trainType = "ROJ",
								isActive = true,
								arrivalTime = "2026-10-23 11:55:00",
								departureTime = "2026-10-23 11:56:00"
							},
							new SimRailPoint
							{
								pointId = "20",
								nameForPerson = "C",
								indexOfPoint = 2,
								line = 4,
								arrivalTime = "2026-10-23 12:05:00",
								departureTime = "2026-10-23 12:05:00"
							}
						}
					}
				}
			};
		}

		public Task<List<SimRailServer>> ServersAsync(CancellationToken token)
		{
			List<SimRailServer> list = new List<SimRailServer>();
			list.Add(new SimRailServer
			{
				ServerCode = Value.ServerCode,
				ServerName = "Test lokalny"
			});
			return Task.FromResult(list);
		}

		public Task<SimRailSnapshot> SnapshotAsync(string server, CancellationToken token)
		{
			return Task.FromResult(Value);
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

	private static T Field<T>(object instance, string name)
	{
		return (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
	}

	private static void Call(object instance, string name)
	{
		try
		{
			instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, null);
		}
		catch (TargetInvocationException ex)
		{
			throw ex.InnerException;
		}
	}

	private static Engine Connect(string release, string server, ConfiguratorRepository repository)
	{
		using SimulatorConnectionForm simulatorConnectionForm = new SimulatorConnectionForm(new Feed(server), repository, Path.Combine(release, "simrail"));
		simulatorConnectionForm.Show();
		Application.DoEvents();
		Call(simulatorConnectionForm, "LoadSnapshot");
		ListBox listBox = Field<ListBox>(simulatorConnectionForm, "available");
		listBox.SelectedIndex = 0;
		Call(simulatorConnectionForm, "AddSelected");
		Call(simulatorConnectionForm, "Connect");
		Engine connectedEngine = simulatorConnectionForm.ConnectedEngine;
		Check(connectedEngine != null, "Połączenie korzysta z podstawionego API, bez dostępu do sieci.");
		simulatorConnectionForm.DatabaseLock.ReleaseMutex();
		simulatorConnectionForm.DatabaseLock.Dispose();
		connectedEngine.SimRail.SessionLock = null;
		return connectedEngine;
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		LocalPersistence.Enabled = false;
		try
		{
			string path = Path.Combine(folder, "case-" + Guid.NewGuid().ToString("N"));
			ConfiguratorRepository configuratorRepository = new ConfiguratorRepository("simrail", Path.Combine(path, "shared-user-settings"));
			string text = Path.Combine(configuratorRepository.Profiles, "1852.json");
			Check(DataLocations.ConfigurationRoot == Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EDR"), "Domyślny katalog konfiguracji jest w AppData użytkownika i nie zawiera numeru wersji.");
			string text2 = Path.Combine(path, "old-release");
			string text3 = Path.Combine(path, "updated-release");
			Engine engine = Connect(text2, "test170a", configuratorRepository);
			Check(!File.Exists(text) && !engine.Station.SimulatorReviewed, "Otwarcie świeżego posterunku nie zapisuje automatycznie sugerowanej konfiguracji.");
			engine.SimRail.SetAccessLink("https://api.simrail.eu:8093/?serverCode=test170a&stationId=1852&key=TEST_ONLY_CONFIGURATION_170");
			engine.BeginDuty(engine.Now.AddMinutes(-2.0));
			engine.SaveJournal(engine.Note(engine.Station.Sections[0].Id, engine.Now, "ENTRY_SHOULD_NOT_PERSIST_170", "Wpis"));
			using (StationConfigurationForm stationConfigurationForm = new StationConfigurationForm(engine))
			{
				stationConfigurationForm.Show();
				Application.DoEvents();
				Station station = Field<Station>(stationConfigurationForm, "copy");
				Section section = station.Sections[0];
				section.JournalName = "Własny nagłówek Korytowa";
				section.Tracks = 2;
				section.RightTrackNumber = 2;
				section.LineTracks = new List<LineTrackSetting>
				{
					new LineTrackSetting
					{
						Number = 1,
						NormalIncoming = true,
						TrafficType = "Dwukierunkowy",
						Basis = "Zapowiadanie telefoniczne",
						BasisOut = "Zapowiadanie radiotelefoniczne"
					},
					new LineTrackSetting
					{
						Number = 2,
						NormalIncoming = false,
						TrafficType = "Jednokierunkowy",
						Basis = "Wieloodstępowa (samoczynna) blokada liniowa",
						BasisOut = "Wieloodstępowa (samoczynna) blokada liniowa"
					}
				};
				section.Crossings.Add(new CrossingSetting
				{
					Id = "test-crossing",
					Name = "Dróżnik testowy",
					Kilometre = "12,345",
					NotificationMode = "Znak",
					Number = "7",
					Mark = "D7",
					InJournal = true
				});
				Field<BindingList<StationTrackSetting>>(stationConfigurationForm, "tracks").Add(new StationTrackSetting
				{
					Number = "77",
					Visible = true,
					LineNumber = "4",
					Kind = "Tor testowy"
				});
				Field<BindingList<RequiredCheckSetting>>(stationConfigurationForm, "checks").Add(new RequiredCheckSetting
				{
					Name = "Sprawdzenie testowe",
					Hours = "12:00",
					Enabled = true
				});
				Field<RadioButton>(stationConfigurationForm, "customRelease").Checked = true;
				Field<NumericUpDown>(stationConfigurationForm, "releaseSeconds").Value = 123m;
				Field<CheckBox>(stationConfigurationForm, "writeSepe").Checked = false;
				Call(stationConfigurationForm, "Save");
				Check(stationConfigurationForm.DialogResult == DialogResult.OK && File.Exists(text), "Zapisz w rzeczywistym formularzu utrwala konfigurację przy wyłączonym zapisie sesji.");
			}
			string text4 = File.ReadAllText(text);
			Check(!text4.Contains("ENTRY_SHOULD_NOT_PERSIST_170") && !text4.Contains("TEST_ONLY_CONFIGURATION_170") && !text4.Contains("\"Entries\"") && !text4.Contains("\"Duties\""), "Profil zawiera ustawienia, bez wpisów dziennika, dyżuru i klucza SEPE.");
			Engine engine2 = Connect(text2, "test170a", configuratorRepository);
			CheckSettings(engine2);
			Check(engine2.State.Entries.Count == 0 && engine2.State.Duties.Count == 0 && !engine2.SimRail.HasWriteLink, "Ponowne uruchomienie odtwarza ustawienia, ale rozpoczyna pustą sesję bez linku SEPE.");
			Engine engine3 = Connect(text3, "test170b", configuratorRepository);
			CheckSettings(engine3);
			Check(!Directory.Exists(text2) && !Directory.Exists(text3), "Zmiana folderu wydania i serwera zachowuje profil bez tworzenia bazy sesji.");
			using (StationConfigurationForm stationConfigurationForm2 = new StationConfigurationForm(engine3))
			{
				stationConfigurationForm2.Show();
				Application.DoEvents();
				Field<Station>(stationConfigurationForm2, "copy").Sections[0].JournalName = "Drugi zapis";
				Call(stationConfigurationForm2, "Save");
			}
			Check(Json.Read<StationProfile>(File.ReadAllBytes(text)).Station.Sections[0].JournalName == "Drugi zapis" && File.Exists(text + ".bak"), "Kolejny zapis aktualizuje profil atomowo z kopią poprzedniej konfiguracji.");
			Engine engine4 = Connect(text2, "test170a", configuratorRepository);
			Check(engine4.Station.Sections[0].JournalName == "Drugi zapis" && engine4.Station.Sections[0].Tracks == 2, "Zmiana konfiguracji na drugim serwerze jest widoczna po powrocie na pierwszy; liczba torów nie zmienia się.");
			Engine engine5 = Connect(text3, "test170c", configuratorRepository);
			string text5 = Path.Combine(path, "blocked-directory");
			File.WriteAllText(text5, "test");
			engine5.StationProfileDirectory = text5;
			using (StationConfigurationForm stationConfigurationForm3 = new StationConfigurationForm(engine5))
			{
				stationConfigurationForm3.Show();
				Application.DoEvents();
				bool flag = false;
				try
				{
					Call(stationConfigurationForm3, "Save");
				}
				catch (IOException)
				{
					flag = true;
				}
				Check(flag && stationConfigurationForm3.SavedStation == null && stationConfigurationForm3.DialogResult != DialogResult.OK, "Błąd zapisu na dysku nie jest zgłaszany jako udane zapisanie formularza.");
			}
			Check(File.ReadAllText(text).Contains("Drugi zapis") && !LocalPersistence.Enabled, "Nieudany zapis nie narusza poprzedniego profilu ani nie włącza zapisu sesji.");
			return true;
		}
		catch (Exception ex2)
		{
			log.Add(ex2.ToString());
			return false;
		}
		finally
		{
			File.WriteAllLines(Path.Combine(folder, "wyniki-konfiguracja170.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}

	private static void CheckSettings(Engine engine)
	{
		Station station = engine.Station;
		Section section = station.Sections[0];
		Check(station.SimulatorReviewed && section.JournalName == "Własny nagłówek Korytowa" && section.Tracks == 2 && section.LineTracks.Count == 2 && section.LineTracks[0].Basis == "Zapowiadanie telefoniczne" && section.LineTracks[0].BasisOut == "Zapowiadanie radiotelefoniczne" && section.LineTracks[0].TrafficType == "Dwukierunkowy", "Odtworzono nazwę dziennika, liczbę i kierunki torów oraz oba rodzaje blokady.");
		Check(station.TrackSettings.Any((StationTrackSetting x) => x.Number == "77" && x.Visible) && station.RequiredChecks.Any((RequiredCheckSetting x) => x.Name == "Sprawdzenie testowe") && section.Crossings.Any((CrossingSetting x) => x.Id == "test-crossing"), "Odtworzono tory stacyjne, wymagane sprawdzenia i dróżnika.");
		Check(station.Options.AutomaticReleaseSeconds == 123 && station.Options.WriteSepe == false, "Odtworzono opcje z zakładki Inne.");
	}
}

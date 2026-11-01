using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EdrLocal;

public static class Defaults33Tests
{
	private sealed class Feed : ISimulatorFeed
	{
		public string ProviderId => "SimRail";

		public Task<List<SimRailServer>> ServersAsync(CancellationToken token)
		{
			List<SimRailServer> list = new List<SimRailServer>();
			list.Add(new SimRailServer
			{
				ServerCode = "defaults33",
				ServerName = "Test SimRail"
			});
			return Task.FromResult(list);
		}

		public Task<SimRailSnapshot> SnapshotAsync(string server, CancellationToken token)
		{
			SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
			simRailSnapshot.ServerCode = server;
			simRailSnapshot.GameTime = new DateTime(2026, 4, 22, 8, 30, 0);
			simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
			simRailSnapshot.DispatcherNames = new HashSet<string> { "Juliusz" };
			simRailSnapshot.Trains = new List<SimRailTrain>
			{
				new SimRailTrain
				{
					trainNoLocal = "444015",
					timetable = new List<SimRailPoint>
					{
						new SimRailPoint
						{
							pointId = "100",
							nameForPerson = "Dorota",
							indexOfPoint = 0,
							line = 171,
							mileage = 0.0,
							track = 1,
							arrivalTime = "2026-04-22 08:00:00",
							departureTime = "2026-04-22 08:00:00"
						},
						new SimRailPoint
						{
							pointId = "101",
							nameForPerson = "Juliusz",
							indexOfPoint = 1,
							line = 171,
							mileage = 5.0,
							track = 1,
							arrivalTime = "2026-04-22 08:06:00",
							departureTime = "2026-04-22 08:06:00"
						}
					}
				}
			};
			return Task.FromResult(simRailSnapshot);
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

	private static T Field<T>(object obj, string name)
	{
		return (T)obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
	}

	private static AdministrationData Empty(string provider)
	{
		AdministrationData administrationData = new AdministrationData();
		administrationData.SchemaVersion = 2;
		administrationData.Provider = provider;
		administrationData.Units = new List<ConfigurationUnit>();
		administrationData.Stations = new List<ConfiguredStation>();
		administrationData.Employees = new List<ConfigurationEmployee>();
		administrationData.Assignments = new List<ConfigurationAssignment>();
		return administrationData;
	}

	public static bool Run(string folder)
	{
		Ui.Testing = true;
		Directory.CreateDirectory(folder);
		try
		{
			string root = Path.Combine(folder, "local-appdata");
			ConfiguratorRepository configuratorRepository = new ConfiguratorRepository("simrail", root);
			configuratorRepository.Load();
			ConfigurationUnit configurationUnit = configuratorRepository.Data.Units.Single((ConfigurationUnit x) => x.Kind == "IZ");
			ConfigurationUnit configurationUnit2 = configuratorRepository.Data.Units.Single((ConfigurationUnit x) => x.Kind == "ISE");
			Check(configurationUnit.Name == "SimRail" && configurationUnit2.Name == "EDR" && configurationUnit2.ParentId == configurationUnit.Id, "Nowy SimRail ma IZ SimRail i podległą ISE SimKol.");
			Check(configuratorRepository.Data.DefaultStructureInitialized && configuratorRepository.DefaultSectionId == configurationUnit2.Id && File.Exists(configuratorRepository.DataFile), "Domyślna struktura i znacznik pierwszego uruchomienia są zapisane od razu.");
			ConfiguratorRepository configuratorRepository2 = new ConfiguratorRepository("simrail", root);
			configuratorRepository2.Load();
			Check(configuratorRepository2.Data.Units.Count == 2 && configuratorRepository2.DefaultSectionId == configurationUnit2.Id, "Restart zachowuje te same domyślne jednostki bez duplikatów.");
			ConfiguratorRepository configuratorRepository3 = new ConfiguratorRepository("td2", root);
			configuratorRepository3.Load();
			Check(configuratorRepository3.Data.Units.Single((ConfigurationUnit x) => x.Kind == "IZ").Name == "TTSK" && configuratorRepository3.Data.Units.Single((ConfigurationUnit x) => x.Kind == "ISE").Name == "TTSK" && configuratorRepository3.Data.Units.Single((ConfigurationUnit x) => x.Kind == "ISE").ParentId == configuratorRepository3.Data.Units.Single((ConfigurationUnit x) => x.Kind == "IZ").Id, "Nowy TD2 ma IZ TTSK i podległą ISE TTSK.");
			Check(configuratorRepository3.DataFile != configuratorRepository.DataFile && configuratorRepository3.DefaultSectionId != configuratorRepository.DefaultSectionId, "Jednostki obu symulatorów pozostają niezależne.");
			using (AdministrationForm administrationForm = new AdministrationForm(configuratorRepository))
			{
				administrationForm.Show();
				Application.DoEvents();
				Check(!Field<Label>(administrationForm, "status").Text.Contains("lokalnie"), "Dolny pasek konfiguratora nie zawiera wskazanego napisu o lokalnym zapisie.");
				Ui.Capture(administrationForm, Path.Combine(folder, "domyslna-struktura-simrail.png"));
				administrationForm.Close();
			}
			using (AdministrationForm administrationForm2 = new AdministrationForm(configuratorRepository3))
			{
				administrationForm2.Show();
				Application.DoEvents();
				Ui.Capture(administrationForm2, Path.Combine(folder, "domyslna-struktura-td2.png"));
				administrationForm2.Close();
			}
			using (SimulatorConnectionForm simulatorConnectionForm = new SimulatorConnectionForm(new Feed(), configuratorRepository, Path.Combine(folder, "dane")))
			{
				simulatorConnectionForm.Show();
				Application.DoEvents();
				typeof(SimulatorConnectionForm).GetMethod("LoadSnapshot", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(simulatorConnectionForm, null);
				Application.DoEvents();
				Check(!simulatorConnectionForm.Controls.OfType<ComboBox>().Any(), "Główne okno połączenia nie zawiera stałych pól jednostek.");
				Check(Field<Button>(simulatorConnectionForm, "connect").Enabled, "Połączenie jest dostępne bez wyboru jednostek.");
				Ui.Capture(simulatorConnectionForm, Path.Combine(folder, "polaczenie-domyslne.png"));
				typeof(SimulatorConnectionForm).GetMethod("Connect", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(simulatorConnectionForm, null);
				Check(simulatorConnectionForm.ConnectedEngine.OrganizationCaption == "EDR", "Sesja używa samej jednostki EDR bez przypisania IZ/ISE.");
				simulatorConnectionForm.DatabaseLock.Dispose();
			}
			configuratorRepository.Load();
			Check(configuratorRepository.Data.Stations.Count == 0, "Zwykłe połączenie nie zapisuje struktury organizacyjnej.");
			ConfiguratorRepository configuratorRepository4 = new ConfiguratorRepository("simrail", Path.Combine(folder, "custom-old"));
			Directory.CreateDirectory(Path.GetDirectoryName(configuratorRepository4.DataFile));
			AdministrationData administrationData = Empty("simrail");
			administrationData.Units.Add(new ConfigurationUnit
			{
				Id = "own-iz",
				Kind = "IZ",
				Name = "Mój zakład"
			});
			administrationData.Units.Add(new ConfigurationUnit
			{
				Id = "own-ise",
				Kind = "ISE",
				ParentId = "own-iz",
				Name = "Moja sekcja"
			});
			File.WriteAllBytes(configuratorRepository4.DataFile, Json.Bytes(administrationData));
			configuratorRepository4.Load();
			Check(configuratorRepository4.Data.Units.Count == 2 && configuratorRepository4.Data.Units.All((ConfigurationUnit x) => x.Id.StartsWith("own-")) && configuratorRepository4.DefaultSectionId == null, "Własna starsza struktura nie jest nadpisywana ani rozszerzana domyślną gałęzią.");
			configuratorRepository4.RemoveUnit("own-iz");
			configuratorRepository4.Load();
			Check(configuratorRepository4.Data.Units.Count == 0, "Po świadomym usunięciu własnej struktury domyślne jednostki nie odtwarzają się.");
			ConfiguratorRepository configuratorRepository5 = new ConfiguratorRepository("simrail", Path.Combine(folder, "empty-old"));
			Directory.CreateDirectory(Path.GetDirectoryName(configuratorRepository5.DataFile));
			byte[] array = Json.Bytes(Empty("simrail"));
			File.WriteAllBytes(configuratorRepository5.DataFile, array);
			configuratorRepository5.Load();
			Check(configuratorRepository5.Data.Units.Count == 2 && configuratorRepository5.Data.Units.Any((ConfigurationUnit x) => x.Name == "SimRail") && configuratorRepository5.DefaultSectionId != null, "Pusty rejestr ze starszej wersji otrzymuje domyślną strukturę jednorazowo.");
			Check(File.ReadAllBytes(configuratorRepository5.DataFile + ".bak").SequenceEqual(array), "Inicjalizacja zachowuje kopię wcześniejszego pustego rejestru.");
			string id = configuratorRepository5.Data.Units.Single((ConfigurationUnit x) => x.Kind == "IZ").Id;
			configuratorRepository5.RenameUnit(id, "Własna nazwa");
			configuratorRepository5.Load();
			Check(configuratorRepository5.Data.Units.Single((ConfigurationUnit x) => x.Kind == "IZ").Name == "Własna nazwa", "Zmiana nazwy domyślnego zakładu pozostaje po restarcie.");
			configuratorRepository5.RemoveUnit(configuratorRepository5.DefaultSectionId);
			configuratorRepository5.Load();
			Check(configuratorRepository5.Data.Units.Count == 1 && configuratorRepository5.DefaultSectionId == null, "Usunięta domyślna ISE nie jest ponownie dodawana.");
			configuratorRepository5.RemoveUnit(id);
			configuratorRepository5.Load();
			Check(configuratorRepository5.Data.Units.Count == 0, "Usunięta cała domyślna gałąź nie wraca po uruchomieniu.");
			log.Add("ALL TESTS PASSED: " + log.Count);
			File.WriteAllLines(Path.Combine(folder, "wyniki-domyslne33.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
			return true;
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			File.WriteAllLines(Path.Combine(folder, "wyniki-domyslne33.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
			return false;
		}
	}
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace EdrLocal;

public static class Carriers41Tests
{
	private const string Cargo = "PKP Cargo S.A. w restrukturyzacji";

	private const string Intercity = "PKP Intercity S.A.";

	private static readonly List<string> log = new List<string>();

	private static void Check(bool ok, string text)
	{
		if (!ok)
		{
			throw new Exception("FAIL: " + text);
		}
		log.Add("PASS: " + text);
	}

	private static SimRailTrain Source(string number, string kind, string hour)
	{
		SimRailTrain simRailTrain = new SimRailTrain();
		simRailTrain.trainNoLocal = number;
		simRailTrain.trainName = kind;
		simRailTrain.startStation = "Częstochowa Towarowa";
		simRailTrain.endStation = "Poznań Franowo";
		simRailTrain.Vehicles = new List<string> { "4E/EU07-241:G", "424Z/wagon:P" };
		simRailTrain.timetable = new List<SimRailPoint>
		{
			new SimRailPoint
			{
				pointId = "60",
				indexOfPoint = 1,
				nameForPerson = "Baby",
				trainType = kind,
				displayedTrainNumber = number,
				arrivalTime = "2026-10-21 " + hour,
				departureTime = "2026-10-21 " + hour,
				isActive = true,
				line = 1,
				track = 1
			}
		};
		return simRailTrain;
	}

	public static bool Run(string folder)
	{
		Ui.Testing = true;
		Directory.CreateDirectory(folder);
		bool enabled = LocalPersistence.Enabled;
		LocalPersistence.Enabled = false;
		try
		{
			string[] array = new string[3] { "TME", "TNE", "TDE" };
			foreach (string text in array)
			{
				Check(SimRailTrainIdentity.Carrier(Source("474046", text, "23:51:00"), text) == "PKP Cargo S.A. w restrukturyzacji", "EU07-241 w towarowym " + text + " wskazuje Cargo.");
			}
			string[] array2 = new string[4] { "PWE", "PWJ", "MPE", "ROJ" };
			foreach (string text2 in array2)
			{
				Check(SimRailTrainIdentity.Carrier(Source("474046", text2, "23:51:00"), text2) == "PKP Intercity S.A.", "EU07-241 w pasażerskim lub próżnym składzie " + text2 + " wskazuje Intercity.");
			}
			Check(VehicleCarriers.Resolve(new string[1] { "4E/EU07-241:G" }) == "" && VehicleCarriers.Resolve(new string[1] { "4E/EU07-241:G" }, "???") == "", "Bez odpowiedniego rodzaju nie wybiera się dowolnego kandydata.");
			Check(VehicleCarriers.Resolve(new string[1] { "4E/EU07-241 Variant:R" }, " tme ") == "PKP Cargo S.A. w restrukturyzacji", "Normalizacja wariantu, orientacji i rodzaju zachowuje rozstrzygnięcie.");
			Check(CarrierCandidates.Select(new string[2] { "KS", "PCC" }, "TME") == "PCC Intermodal S.A." && CarrierCandidates.Select(new string[2] { "KS", "PCC" }, "ROJ") == "Koleje Śląskie Sp. z o.o.", "Reguła rozstrzyga pośród kandydatów również dla innych spółek.");
			Check(CarrierCandidates.Select(new string[2] { "Cargo", "PCC" }, "TME") == "" && CarrierCandidates.Select(new string[2] { "IC", "KS" }, "PWE") == "", "Dwóch zgodnych przewoźników nadal pozostaje nierozstrzygniętych.");
			Check(CarrierCandidates.Select(new string[2] { "KS", "IC" }, "TME") == "" && CarrierCandidates.Select(new string[2] { "Cargo", "PCC" }, "PWE") == "", "Reguła nie dodaje przewoźnika spoza listy kandydatów.");
			Check(CarrierCandidates.Select(new string[2] { "Nieznany A", "Nieznany B" }, "TME") == "", "Nieznane profile przewoźników nie są zgadywane.");
			Check(VehicleCarriers.Resolve(new string[2] { "4E/EU07-241:G", "4E/EU07-096:P" }, "TME") == "PKP Cargo S.A. w restrukturyzacji", "Dwie lokomotywy zgodne z wybranym Cargo dają jeden wynik.");
			Check(VehicleCarriers.Resolve(new string[2] { "4E/EU07-241:G", "4E/EU07-085:P" }, "TME") == "", "Sprzeczna druga lokomotywa nie jest ignorowana.");
			Check(VehicleCarriers.Resolve(new string[2] { "4E/EU07-241:G", "4E/EU07-999:P" }, "TME") == "", "Nieznana druga lokomotywa nadal blokuje pewne przypisanie.");
			Check(VehicleCarriers.Resolve(new string[1] { "4E/EU07-085:P" }, "TME") == "PKP Intercity S.A.", "Jednoznaczny wariant nie jest globalnie nadpisywany samym rodzajem TME.");
			SimRailTrain simRailTrain = Source("474046", "TME", "23:51:00");
			simRailTrain.carrierName = "PCC";
			Check(SimRailTrainIdentity.Carrier(simRailTrain, "TME") == "PCC Intermodal S.A.", "Jawny przewoźnik API zachowuje pierwszeństwo.");
			simRailTrain.carrierName = "";
			simRailTrain.Vehicles = new List<string> { "Elf/EN76-006", "4E/EU07-241:G" };
			Check(SimRailTrainIdentity.Carrier(simRailTrain, "TME") == "Koleje Śląskie Sp. z o.o.", "Pierwszeństwo konkretnego EZT pozostaje zachowane.");
			simRailTrain = Source("474046", "TME", "23:51:00");
			SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
			simRailSnapshot.ServerCode = "pl2";
			simRailSnapshot.Trains = new List<SimRailTrain>
			{
				simRailTrain,
				Source("794001", "PWE", "23:55:00")
			};
			simRailSnapshot.DispatcherNames = new HashSet<string> { "Baby" };
			simRailSnapshot.GameTime = new DateTime(2026, 10, 21, 23, 50, 0);
			simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
			SimRailSnapshot snapshot = simRailSnapshot;
			SimRailSession simRailSession = new SimRailSession(null, snapshot, "60");
			Station station = simRailSession.SuggestStation();
			station.Sections.Add(new Section
			{
				Id = "test",
				Name = "Rokiciny",
				Line = "1",
				Tracks = 1,
				Basis = "Blokada samoczynna"
			});
			StationSettings.Normalize(station);
			Store store = new Store(Path.Combine(folder, "no-data"));
			store.Load();
			store.Commit((AppState s) =>
			{
				s.Stations = new List<Station> { station };
				s.Trains.Clear();
				s.Entries.Clear();
				s.Duties.Clear();
			});
			Engine engine = new Engine(store);
			engine.SimRail = simRailSession;
			engine.User = store.State.Users.First((LocalUser x) => x.Id == "DEMO");
			Engine engine2 = engine;
			engine2.ApplySimRailSnapshot(snapshot);
			Check(engine2.State.Trains.Single((Train x) => x.Number == "474046").Carrier == "PKP Cargo S.A. w restrukturyzacji" && engine2.State.Trains.Single((Train x) => x.Number == "794001").Carrier == "PKP Intercity S.A.", "Projekcja rozróżnia dwie kategorie tej samej EU07-241.");
			using (MainForm mainForm = new MainForm(engine2))
			{
				mainForm.Show();
				mainForm.Size = new Size(1600, 850);
				mainForm.SelectTrain(engine2.State.Trains.Single((Train x) => x.Number == "474046").Id);
				Application.DoEvents();
				DataGridView dataGridView = (DataGridView)typeof(MainForm).GetField("trains", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mainForm);
				Check(dataGridView.Rows.Cast<DataGridViewRow>().Any((DataGridViewRow x) => Convert.ToString(x.Cells["carrier"].Value) == "PKP Cargo S.A. w restrukturyzacji") && dataGridView.Rows.Cast<DataGridViewRow>().Any((DataGridViewRow x) => Convert.ToString(x.Cells["carrier"].Value) == "PKP Intercity S.A."), "Wykaz pokazuje Cargo i Intercity zależnie od rodzaju.");
				Check(!dataGridView.ShowCellToolTips && dataGridView.Rows.Cast<DataGridViewRow>().All((DataGridViewRow x) => string.IsNullOrEmpty(x.Cells["carrier"].ToolTipText)), "Podpowiedzi pozostają wyłączone.");
				Ui.Capture(mainForm, Path.Combine(folder, "kandydaci-1.0.10.199.png"));
				mainForm.Close();
			}
			Check(!Directory.Exists(store.DirectoryPath) && !LocalPersistence.Enabled, "Nowe rozstrzyganie nie włącza zapisu na dysk.");
			log.Add("ALL CARRIER 41 TESTS PASSED");
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
			File.WriteAllLines(Path.Combine(folder, "wyniki-przewoznicy41.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

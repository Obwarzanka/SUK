using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace EdrLocal;

public static class Carriers40Tests
{
	private const string Cargo = "PKP Cargo S.A. w restrukturyzacji";

	private static readonly List<string> log = new List<string>();

	private static void Check(bool ok, string text)
	{
		if (!ok)
		{
			throw new Exception("FAIL: " + text);
		}
		log.Add("PASS: " + text);
	}

	public static bool Run(string folder)
	{
		Ui.Testing = true;
		Directory.CreateDirectory(folder);
		bool enabled = LocalPersistence.Enabled;
		LocalPersistence.Enabled = false;
		try
		{
			string[] array = new string[5] { "Cargo", "PKP CARGO S.A.", "PKP Cargo Spółka Akcyjna", "pkp cargo s.a. w restrukturyzacji", "PKP Cargo Spółka Akcyjna w restrukturyzacji" };
			foreach (string text in array)
			{
				Check(CarrierNames.Full(text) == "PKP Cargo S.A. w restrukturyzacji", "Normalizacja nazwy Cargo: " + text + ".");
			}
			Check(CarrierNames.Expanded("PKP Cargo S.A. w restrukturyzacji") == "PKP Cargo Spółka Akcyjna w restrukturyzacji" && CarrierNames.Full(CarrierNames.Expanded("PKP Cargo S.A. w restrukturyzacji")) == "PKP Cargo S.A. w restrukturyzacji", "Pole P rozwija S.A. i zachowuje dopisek o restrukturyzacji.");
			Check(VehicleCarriers.Resolve(new string[1] { "Traxx/E186-139:G" }) == "PKP Cargo S.A. w restrukturyzacji" && VehicleCarriers.Resolve(new string[1] { "Traxx/E186-139 Variant:P" }) == "PKP Cargo S.A. w restrukturyzacji", "E186-139 otrzymuje umowne PKP Cargo niezależnie od orientacji.");
			Check(VehicleCarriers.Resolve(new string[2] { "Traxx/E186-139:G", "201E/ET22-644:P" }) == "PKP Cargo S.A. w restrukturyzacji", "Umowne Akiem i wariant Cargo dają zgodne przypisanie.");
			SimRailTrain simRailTrain = new SimRailTrain();
			simRailTrain.trainNoLocal = "143139";
			simRailTrain.trainName = "TNE";
			simRailTrain.startStation = "Tomaszów Mazowiecki";
			simRailTrain.endStation = "Dąbrowa Górnicza Towarowa";
			simRailTrain.Vehicles = new List<string> { "Traxx/E186-139:G" };
			simRailTrain.timetable = new List<SimRailPoint>
			{
				new SimRailPoint
				{
					pointId = "924",
					indexOfPoint = 1,
					nameForPerson = "Gałkówek",
					trainType = "TNE",
					displayedTrainNumber = "143139",
					arrivalTime = "2026-10-21 19:49:00",
					departureTime = "2026-10-21 19:49:00",
					isActive = true,
					line = 25,
					track = 1
				}
			};
			SimRailTrain simRailTrain2 = simRailTrain;
			CarrierResult carrierResult = SimRailTrainIdentity.CarrierInfo(simRailTrain2, "TNE");
			Check(carrierResult.Name == "PKP Cargo S.A. w restrukturyzacji" && carrierResult.Evidence.Contains("Umowne") && carrierResult.Evidence.Contains("wiki podaje Akiem"), "143139 z E186-139 jest uzupełniany zgodnie z regułą użytkownika, z poprawną wewnętrzną podstawą.");
			simRailTrain2.carrierName = "Akiem";
			Check(SimRailTrainIdentity.CarrierInfo(simRailTrain2, "TNE").Name == "PKP Cargo S.A. w restrukturyzacji" && SimRailTrainIdentity.CarrierInfo(simRailTrain2, "TNE").Evidence.Contains("Umowne"), "Jawne Akiem w carrierName korzysta z tej samej reguły.");
			simRailTrain2.carrierName = "PCC";
			Check(SimRailTrainIdentity.Carrier(simRailTrain2, "TNE") == "PCC Intermodal S.A.", "Reguła lokomotywy nie zastępuje innego jawnego przewoźnika API.");
			simRailTrain2.carrierName = "";
			Check(VehicleCarriers.Resolve(new string[1] { "Traxx/E186-246:G" }) == "PCC Intermodal S.A." && VehicleCarriers.Resolve(new string[1] { "Traxx/E186-999:G" }) == "", "Inne warianty Traxx nie dziedziczą przypisania Akiem.");
			SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
			simRailSnapshot.ServerCode = "pl2";
			simRailSnapshot.Trains = new List<SimRailTrain> { simRailTrain2 };
			simRailSnapshot.DispatcherNames = new HashSet<string> { "Gałkówek" };
			simRailSnapshot.GameTime = new DateTime(2026, 10, 21, 19, 50, 0);
			simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
			SimRailSnapshot snapshot = simRailSnapshot;
			SimRailSession simRailSession = new SimRailSession(null, snapshot, "924");
			Station station = simRailSession.SuggestStation();
			station.Sections.Add(new Section
			{
				Id = "test",
				Name = "Koluszki",
				Line = "25",
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
			using (MainForm mainForm = new MainForm(engine2))
			{
				mainForm.Show();
				mainForm.Size = new Size(1600, 850);
				mainForm.SelectTrain(engine2.State.Trains.Single().Id);
				Application.DoEvents();
				DataGridView dataGridView = (DataGridView)typeof(MainForm).GetField("trains", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mainForm);
				Label label = (Label)typeof(MainForm).GetField("filterInfo", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mainForm);
				Check(Convert.ToString(dataGridView.Rows[0].Cells["carrier"].Value) == "PKP Cargo S.A. w restrukturyzacji" && label.Text.Contains("PKP Cargo Spółka Akcyjna w restrukturyzacji"), "Wykaz i pole P pokazują nowe nazwy Cargo dla 143139.");
				Check(!dataGridView.ShowCellToolTips && dataGridView.Rows.Cast<DataGridViewRow>().All((DataGridViewRow x) => string.IsNullOrEmpty(x.Cells["carrier"].ToolTipText)), "Wykaz nie wyświetla podpowiedzi, także automatycznych dla uciętych nazw.");
				typeof(MainForm).GetMethod("RefreshTrains", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(mainForm, null);
				Check(!dataGridView.ShowCellToolTips && string.IsNullOrEmpty(dataGridView.Rows[0].Cells["carrier"].ToolTipText), "Odświeżenie nie przywraca podpowiedzi przewoźnika.");
				Ui.Capture(mainForm, Path.Combine(folder, "cargo-1.0.10.199.png"));
				mainForm.Close();
			}
			Check(!Directory.Exists(store.DirectoryPath) && !LocalPersistence.Enabled, "Zmiany przewoźnika i podpowiedzi nie włączają lokalnego zapisu.");
			log.Add("ALL CARRIER 40 TESTS PASSED");
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
			File.WriteAllLines(Path.Combine(folder, "wyniki-przewoznicy40.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

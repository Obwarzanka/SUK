using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace EdrLocal;

public static class Carriers38Tests
{
	public sealed class CatalogRecord
	{
		public string Vehicle { get; set; }

		public string Carrier { get; set; }

		public string Source { get; set; }
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

	private static SimRailTrain Source()
	{
		SimRailTrain simRailTrain = new SimRailTrain();
		simRailTrain.trainNoLocal = "123";
		simRailTrain.trainName = "ROJ - IR";
		simRailTrain.startStation = "A";
		simRailTrain.endStation = "B";
		simRailTrain.timetable = new List<SimRailPoint>
		{
			new SimRailPoint
			{
				pointId = "60",
				nameForPerson = "Baby",
				indexOfPoint = 1,
				trainType = "ROJ",
				line = 1,
				track = 1,
				isActive = true,
				displayedTrainNumber = "123",
				arrivalTime = "2026-10-21 21:50:00",
				departureTime = "2026-10-21 21:52:00"
			}
		};
		return simRailTrain;
	}

	private static SimRailRunningTrain Running()
	{
		SimRailRunningTrain simRailRunningTrain = new SimRailRunningTrain();
		simRailRunningTrain.TrainNoLocal = "123";
		simRailRunningTrain.TrainName = "ROJ - IR";
		simRailRunningTrain.StartStation = "A";
		simRailRunningTrain.EndStation = "B";
		simRailRunningTrain.ServerCode = "pl3";
		simRailRunningTrain.RunId = "run1";
		simRailRunningTrain.Vehicles = new List<string> { "EN57/EN57-1000" };
		return simRailRunningTrain;
	}

	public static bool Run(string folder)
	{
		Ui.Testing = true;
		Directory.CreateDirectory(folder);
		bool enabled = LocalPersistence.Enabled;
		LocalPersistence.Enabled = false;
		try
		{
			string[][] array = new string[11][]
			{
				new string[2] { "Intercity", "PKP Intercity S.A." },
				new string[2] { "PKP Intercity", "PKP Intercity S.A." },
				new string[2] { "PKP INTERCITY S.A.", "PKP Intercity S.A." },
				new string[2] { "PKP Intercity Spółka Akcyjna", "PKP Intercity S.A." },
				new string[2] { "Cargo", "PKP Cargo S.A. w restrukturyzacji" },
				new string[2] { "PCC", "PCC Intermodal S.A." },
				new string[2] { "PCC INTERMODAL S.A.", "PCC Intermodal S.A." },
				new string[2] { "KS", "Koleje Śląskie Sp. z o.o." },
				new string[2] { "KM", "Koleje Mazowieckie - KM Sp. z o.o." },
				new string[2] { "Koleje Mazowieckie Sp. z o.o.", "Koleje Mazowieckie - KM Sp. z o.o." },
				new string[2] { "  \"Koleje Mazowieckie - KM\" sp. z o.o.  ", "Koleje Mazowieckie - KM Sp. z o.o." }
			};
			foreach (string[] array2 in array)
			{
				Check(CarrierNames.Full(array2[0]) == array2[1], "Pełna nazwa API: " + array2[0] + ".");
			}
			Check(CarrierNames.Full("Nieznany operator") == "Nieznany operator", "Nieznana spółka nie otrzymuje wymyślonej formy prawnej.");
			string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src", "testdata");
			List<CatalogRecord> list = Json.Read<List<CatalogRecord>>(File.ReadAllBytes(Path.Combine(path, "vehicle-carriers37.json")));
			Check(list.Count == 112 && VehicleCarriers.Count == 112, "Katalog obejmuje 112 jednoznacznych wariantów oficjalnej wiki.");
			Check(list.All((CatalogRecord x) => x.Source.StartsWith("https://wiki.simrail.eu/") && Regex.IsMatch(x.Carrier, "(?i)(S\\.A\\.|sp\\. z o\\.o\\.|GmbH|mbH|AG|a\\.s\\.|S\\.à r\\.l\\.)$")), "Wpisy katalogu mają źródło w oficjalnej wiki i pełną formę prawną.");
			Check(VehicleCarriers.Resolve(new string[1] { "EN57/EN57-1000" }) == "Koleje Mazowieckie - KM Sp. z o.o." && VehicleCarriers.Resolve(new string[1] { "EN57/EN57-206" }) == "POLREGIO S.A.", "EN57 jest rozróżniane po numerze jednostki, nie samym typie.");
			Check(VehicleCarriers.Resolve(new string[1] { "Elf/EN76-006" }) == "Koleje Śląskie Sp. z o.o." && VehicleCarriers.Resolve(new string[1] { "Elf/EN76-022" }) == "Koleje Mazowieckie - KM Sp. z o.o.", "EN76 rozróżnia KŚ i KM.");
			Check(VehicleCarriers.Resolve(new string[1] { "Impuls/36wed-010" }) == "Łódzka Kolej Aglomeracyjna sp. z o.o.", "Impuls wskazuje ŁKA z pełną formą spółki.");
			Check(VehicleCarriers.Resolve(new string[2] { "EN57/EN71-004", "EN57/EN57-650" }) == "POLREGIO S.A.", "Dwie jednostki tego samego przewoźnika dają jeden wynik.");
			Check(VehicleCarriers.Resolve(new string[2] { "EN57/EN57-1000", "EN57/EN57-650" }) == "", "Różni przewoźnicy w zestawie nie wybierają przypadkowo pierwszego.");
			Check(VehicleCarriers.Resolve(new string[1] { "EN57" }) == "" && VehicleCarriers.Resolve(new string[2] { "EN57/EN57-9999", "EN57/EN57-650" }) == "", "Niepełny typ i nieznana jednostka nie dają pewnego przewoźnika.");
			Check(VehicleCarriers.Resolve(new string[2] { "Traxx/E186-246:P", "629Z/PCC-wagon:P:40@Container" }) == "PCC Intermodal S.A.", "Towarowy wskazuje PCC z lokomotywy, pomijając wagony.");
			Check(VehicleCarriers.Resolve(new string[1] { "201E/ET22-644:R" }) == "PKP Cargo S.A. w restrukturyzacji", "ET22 zachowuje numer i pomija znacznik orientacji.");
			Check(VehicleCarriers.Resolve(new string[1] { "Pendolino/ED250-018 Variant:R" }) == "PKP Intercity S.A.", "Obsługiwany jest przyrostek Variant i orientacja.");
			Check(VehicleCarriers.Resolve(new string[1] { "Dragon2/E6ACTa-016:G" }) == "CARGOUNIT Sp. z o.o." && VehicleCarriers.Resolve(new string[1] { "BR285/BR285-124:G" }) == "ORLEN Kolej Sp. z o.o.", "Pozostali przewoźnicy lokomotyw mają pełne formy prawne.");
			Check(VehicleCarriers.Resolve(new string[1] { "4E/EU07-241:G" }) == "", "Niejednoznaczny wariant EU07-241 nie jest zgadywany.");
			SimRailTrain simRailTrain = Source();
			simRailTrain.carrierName = "PCC";
			simRailTrain.Vehicles = new List<string> { "EN57/EN57-1000" };
			CarrierResult carrierResult = SimRailTrainIdentity.CarrierInfo(simRailTrain, "ROJ");
			Check(carrierResult.Name == "Koleje Mazowieckie - KM Sp. z o.o." && carrierResult.Evidence.Contains("EZT"), "Konkretny EZT ma pierwszeństwo przed sprzecznym carrierName i kategorią.");
			simRailTrain.carrierName = "";
			carrierResult = SimRailTrainIdentity.CarrierInfo(simRailTrain, "ROJ");
			Check(carrierResult.Name == "Koleje Mazowieckie - KM Sp. z o.o." && carrierResult.Evidence.Contains("wiki"), "Puste carrierName jest uzupełniane z konkretnego pojazdu.");
			simRailTrain.Vehicles = null;
			simRailTrain.trainName = "ROJ - R1";
			Check(SimRailTrainIdentity.Carrier(simRailTrain, "ROJ") == "Koleje Mazowieckie - KM Sp. z o.o.", "Dotychczasowy słownik kategorii używa nowej nazwy KM.");
			simRailTrain.trainName = "ROJ - IR";
			Check(SimRailTrainIdentity.Carrier(simRailTrain, "ROJ") == "", "Bez pojazdu nie zgaduje się wspólnej kategorii IR.");
			simRailTrain = Source();
			simRailTrain.carrierName = "Cargo";
			simRailTrain.Vehicles = new List<string> { "201E/ET22-644:R" };
			simRailTrain.trainName = "TME";
			carrierResult = SimRailTrainIdentity.CarrierInfo(simRailTrain, "TME");
			Check(carrierResult.Name == "PKP Cargo S.A. w restrukturyzacji" && carrierResult.Evidence.Contains("carrierName"), "Lokomotywa nie zastępuje jawnego przewoźnika towarowego z API.");
			simRailTrain.carrierName = "PCC";
			carrierResult = SimRailTrainIdentity.CarrierInfo(simRailTrain, "TME");
			Check(carrierResult.Name == "PCC Intermodal S.A." && carrierResult.Evidence.Contains("carrierName"), "Sprzeczna lokomotywa pozostaje tylko uzupełnieniem carrierName.");
			simRailTrain.carrierName = "";
			carrierResult = SimRailTrainIdentity.CarrierInfo(simRailTrain, "TME");
			Check(carrierResult.Name == "PKP Cargo S.A. w restrukturyzacji" && carrierResult.Evidence.Contains("lokomotywy"), "Towarowy bez carrierName dostaje przewoźnika z ET22.");
			simRailTrain.trainName = "PWJ";
			simRailTrain.Vehicles = new List<string> { "Dragon2/E6ACTa-016:G" };
			carrierResult = SimRailTrainIdentity.CarrierInfo(simRailTrain, "PWJ");
			Check(carrierResult.Name == "CARGOUNIT Sp. z o.o." && carrierResult.Evidence.Contains("lokomotywy"), "Także PW bez kategorii handlowej korzysta z lokomotywy.");
			simRailTrain.trainName = "MPE - TLK Cookie";
			carrierResult = SimRailTrainIdentity.CarrierInfo(simRailTrain, "MPE");
			Check(carrierResult.Name == "PKP Intercity S.A." && carrierResult.Evidence.Contains("kategorii"), "Rozpoznana kategoria pasażerska poprzedza właściciela lokomotywy.");
			simRailTrain.trainName = "ROJ - R";
			simRailTrain.carrierName = "Cargo";
			simRailTrain.Vehicles = new List<string> { "Elf/EN76-006", "4E/EU07-9999" };
			carrierResult = SimRailTrainIdentity.CarrierInfo(simRailTrain, "ROJ");
			Check(carrierResult.Name == "Koleje Śląskie Sp. z o.o." && carrierResult.Evidence.Contains("EZT"), "EZT ma pierwszeństwo również przy doczepionej nierozpoznanej lokomotywie.");
			simRailTrain.Vehicles = new List<string> { "EN57/EN57-1000", "EN57/EN57-650" };
			carrierResult = SimRailTrainIdentity.CarrierInfo(simRailTrain, "ROJ");
			Check(carrierResult.Name == "PKP Cargo S.A. w restrukturyzacji" && carrierResult.Evidence.Contains("carrierName"), "Niejednoznaczny zestaw EZT pozostawia jawny przewoźnik API.");
			simRailTrain.Vehicles = new List<string> { "EN57/EN57-9999" };
			Check(SimRailTrainIdentity.Carrier(simRailTrain, "ROJ") == "PKP Cargo S.A. w restrukturyzacji", "Nieznany EZT nie przesłania danych API.");
			simRailTrain.trainName = "TME";
			simRailTrain.carrierName = "";
			simRailTrain.Vehicles = new List<string> { "Traxx/E186-246:P", "201E/ET22-644:R" };
			Check(SimRailTrainIdentity.Carrier(simRailTrain, "TME") == "", "Dwie lokomotywy różnych firm nie wybierają przypadkowego przewoźnika.");
			simRailTrain.Vehicles = null;
			Check(SimRailTrainIdentity.Carrier(simRailTrain, "TME") == "", "Przyszły towarowy bez konkretnego pojazdu nie dostaje zmyślonej firmy.");
			Check(CarrierNames.Expanded("PKP INTERCITY S.A.") == "PKP Intercity Spółka Akcyjna" && CarrierNames.Full("Intercity") == "PKP Intercity S.A.", "Nagłówek rozwija formę S.A., a tabela zachowuje skrót.");
			Check(CarrierNames.Expanded("KS") == "Koleje Śląskie Spółka z o.o." && CarrierNames.Expanded("ŁKA") == "Łódzka Kolej Aglomeracyjna Spółka z o.o.", "Formy Sp. z o.o. rozwijają się niezależnie od wielkości liter.");
			Check(CarrierNames.Expanded(null) == "" && CarrierNames.Expanded("Nieznany operator") == "Nieznany operator" && CarrierNames.Expanded("ČD Cargo, a.s.") == "ČD Cargo, a.s.", "Puste i obce formy prawne nie otrzymują polskich końcówek.");
			simRailTrain = Source();
			LiveVehicles.Attach("pl3", new SimRailTrain[1] { simRailTrain }, new SimRailRunningTrain[1] { Running() });
			Check(simRailTrain.Vehicles.Single() == "EN57/EN57-1000" && simRailTrain.VehicleRunId == "run1", "API bieżących pojazdów jest łączone z właściwym rozkładem.");
			string[] array3 = new string[8] { "server", "number", "start", "end", "name", "run", "vehicles", "duplicate" };
			foreach (string text in array3)
			{
				SimRailTrain simRailTrain2 = Source();
				SimRailRunningTrain simRailRunningTrain = Running();
				switch (text)
				{
				case "server":
					simRailRunningTrain.ServerCode = "pl2";
					break;
				case "number":
					simRailRunningTrain.TrainNoLocal = "124";
					break;
				case "start":
					simRailRunningTrain.StartStation = "C";
					break;
				case "end":
					simRailRunningTrain.EndStation = "C";
					break;
				case "name":
					simRailRunningTrain.TrainName = "ROJ - R";
					break;
				case "run":
					simRailRunningTrain.RunId = "";
					break;
				case "vehicles":
					simRailRunningTrain.Vehicles = null;
					break;
				}
				LiveVehicles.Attach("pl3", new SimRailTrain[1] { simRailTrain2 }, (text == "duplicate") ? new SimRailRunningTrain[2]
				{
					simRailRunningTrain,
					Running()
				} : new SimRailRunningTrain[1] { simRailRunningTrain });
				Check(simRailTrain2.Vehicles == null, "Odrzucenie niepasujących danych bieżących: " + text + ".");
			}
			SimRailTrain simRailTrain3 = Source();
			LiveVehicles.Attach("pl3", new SimRailTrain[1] { simRailTrain3 }, new SimRailRunningTrain[1] { Running() });
			LiveVehicles.Attach("pl3", new SimRailTrain[1] { simRailTrain3 }, new SimRailRunningTrain[0]);
			Check(simRailTrain3.Vehicles == null && simRailTrain3.VehicleRunId == null, "Brak bieżącego składu usuwa wcześniejsze powiązanie zamiast używać starego pojazdu.");
			SimRailTrain simRailTrain4 = Source();
			LiveVehicles.Attach("pl3", new SimRailTrain[2]
			{
				simRailTrain4,
				Source()
			}, new SimRailRunningTrain[1] { Running() });
			Check(simRailTrain4.Vehicles == null, "Powtórzony numer w rozkładzie nie dostaje przypadkowego pojazdu.");
			SimRailTrain simRailTrain5 = Source();
			Check(!LiveVehicles.TryLoad("pl3", new List<SimRailTrain> { simRailTrain5 }, CancellationToken.None, () =>
			{
				throw new IOException("Unavailable");
			}) && simRailTrain5.timetable.Count == 1, "Brak opcjonalnego API pojazdów nie blokuje rozkładu.");
			Check(!LiveVehicles.TryLoad("pl3", new List<SimRailTrain> { simRailTrain5 }, CancellationToken.None, () => Encoding.UTF8.GetBytes("broken")), "Uszkodzone API pojazdów jest pomijane.");
			bool ok = false;
			using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource())
			{
				cancellationTokenSource.Cancel();
				try
				{
					LiveVehicles.TryLoad("pl3", new List<SimRailTrain> { simRailTrain5 }, cancellationTokenSource.Token, () => new byte[0]);
				}
				catch (OperationCanceledException)
				{
					ok = true;
				}
			}
			Check(ok, "Anulowanie nadal przerywa pobieranie opcjonalnych danych.");
			List<SimRailTrain> list2 = SimRailProvider.Parse<List<SimRailTrain>>(File.ReadAllBytes(Path.Combine(path, "live-timetables37.json")));
			SimRailEnvelope<SimRailRunningTrain> simRailEnvelope = SimRailProvider.Parse<SimRailEnvelope<SimRailRunningTrain>>(File.ReadAllBytes(Path.Combine(path, "live-vehicles37.json")));
			LiveVehicles.Attach("pl3", list2, simRailEnvelope.data);
			Check(list2.Count == 147 && list2.All((SimRailTrain x) => x.Vehicles != null && x.Vehicles.Count > 0), "147 rzeczywistych bieżących składów PL3 pasuje do rozkładów po numerze, relacji i nazwie.");
			Check(!Json.Text(simRailEnvelope).Contains("SteamID") && !Json.Text(simRailEnvelope).Contains("ControlledBy"), "Model nie przechowuje identyfikatorów graczy z publicznej odpowiedzi.");
			SimRailTrain train = list2.Single((SimRailTrain x) => x.trainName == "PWJ");
			Check(SimRailTrainIdentity.CarrierInfo(train, "PWJ").Name.Length > 0, "Rzeczywisty PWJ z próbki PL3 otrzymuje przewoźnika z lokomotywy.");
			Check(list2.Where((SimRailTrain x) => x.trainName == "TME" && string.IsNullOrWhiteSpace(x.carrierName)).All((SimRailTrain x) => SimRailTrainIdentity.Carrier(x, "TME").Length > 0), "Towarowe TME z pustym carrierName i rozpoznanym składem są uzupełnione.");
			log.Add("INFO: vehicle fallback fills " + Enumerable.Count(list2, (SimRailTrain x) => string.IsNullOrWhiteSpace(x.carrierName) && VehicleCarriers.Resolve(x.Vehicles) != "") + " empty carrierName values in the sample.");
			SimRailTrain simRailTrain6 = Source();
			simRailTrain6.trainName = "TME";
			simRailTrain6.timetable[0].trainType = "TME";
			simRailTrain6.Vehicles = new List<string> { "Traxx/E186-246:P" };
			SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
			simRailSnapshot.ServerCode = "pl3";
			simRailSnapshot.Trains = new List<SimRailTrain> { simRailTrain6 };
			simRailSnapshot.DispatcherNames = new HashSet<string> { "Baby" };
			simRailSnapshot.GameTime = new DateTime(2026, 10, 21, 22, 0, 0);
			simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
			SimRailSnapshot snapshot = simRailSnapshot;
			SimRailSession simRailSession = new SimRailSession(null, snapshot, "60");
			Station station = simRailSession.SuggestStation();
			station.Sections.Add(new Section
			{
				Id = "other",
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
			using (MainForm mainForm = new MainForm(engine2))
			{
				mainForm.Show();
				Application.DoEvents();
				DataGridView dataGridView = (DataGridView)typeof(MainForm).GetField("trains", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mainForm);
				Check(Convert.ToString(dataGridView.Rows[0].Cells["carrier"].Value) == "PCC Intermodal S.A." && !dataGridView.ShowCellToolTips && string.IsNullOrEmpty(dataGridView.Rows[0].Cells["carrier"].ToolTipText), "Kolumna pokazuje przewoźnika z lokomotywy bez podpowiedzi.");
				Label label = (Label)typeof(MainForm).GetField("filterInfo", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mainForm);
				mainForm.SelectTrain(engine2.State.Trains[0].Id);
				Application.DoEvents();
				Check(label.Text.Contains("PCC Intermodal Spółka Akcyjna"), "Pole P w wykazie pokazuje rozwiniętą formę spółki.");
				typeof(MainForm).GetMethod("RefreshDetails", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(mainForm, null);
				Check(label.Text.Contains("PCC Intermodal Spółka Akcyjna") && Convert.ToString(dataGridView.Rows[0].Cells["carrier"].Value) == "PCC Intermodal S.A.", "Odświeżenie opisu zachowuje pełną formę u góry i skrót w kolumnie.");
				mainForm.Size = new Size(1500, 850);
				Ui.Capture(mainForm, Path.Combine(folder, "przewoznik-1.0.10.199.png"));
				mainForm.Close();
			}
			Check(!Directory.Exists(store.DirectoryPath) && !LocalPersistence.Enabled, "Ustalanie przewoźnika i widok nadal działają bez lokalnego zapisu.");
			log.Add("ALL CARRIER 38 TESTS PASSED");
			return true;
		}
		catch (Exception ex2)
		{
			log.Add(ex2.ToString());
			return false;
		}
		finally
		{
			LocalPersistence.Enabled = enabled;
			File.WriteAllLines(Path.Combine(folder, "wyniki-przewoznicy38.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

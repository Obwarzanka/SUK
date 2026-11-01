using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace EdrLocal;

public static class Carriers39Tests
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

	private static List<SimRailTrain> Load()
	{
		return SimRailProvider.Parse<List<SimRailTrain>>(File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src", "testdata", "carriers39-pl2-timetable.json")));
	}

	private static List<SimRailRunningTrain> Live()
	{
		return SimRailProvider.Parse<List<SimRailRunningTrain>>(File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src", "testdata", "carriers39-pl2-live.json")));
	}

	private static void Attach(VehicleObservations memory, List<SimRailTrain> trains, IEnumerable<SimRailRunningTrain> live)
	{
		LiveVehicles.Attach("pl2", trains, live);
		memory.Apply("pl2", trains);
	}

	public static bool Run(string folder)
	{
		Ui.Testing = true;
		Directory.CreateDirectory(folder);
		bool enabled = LocalPersistence.Enabled;
		LocalPersistence.Enabled = false;
		try
		{
			Check(VehicleCarriers.Resolve(new string[1] { "CD163/163_034-2:G" }) == "ČD Cargo, a.s.", "Wariant 163 034-2 wskazuje ČD Cargo, a.s. zgodnie z wiki.");
			VehicleObservations vehicleObservations = new VehicleObservations();
			List<SimRailTrain> list = Load();
			Attach(vehicleObservations, list, Live());
			SimRailTrain simRailTrain = list.Single((SimRailTrain x) => x.trainNoLocal == "154037");
			SimRailTrain simRailTrain2 = list.Single((SimRailTrain x) => x.trainNoLocal == "154039");
			Check(SimRailTrainIdentity.Carrier(simRailTrain, "TME") == "ČD Cargo, a.s.", "Rzeczywisty 154037 PL2 dostaje przewoźnika z konkretnej 163.");
			Check(simRailTrain2.Vehicles == null && SimRailTrainIdentity.Carrier(simRailTrain2, "TME") == "", "154039 nie przejmuje pojazdu 154037 o tej samej relacji.");
			Check(SimRailTrainIdentity.CarrierInfo(simRailTrain2, "TME").Evidence.Contains("konkretnego pojazdu"), "Pusty przewoźnik ma wyjaśnienie braku danych tego kursu.");
			Check(!simRailTrain.VehiclesFromObservation && simRailTrain.VehiclesObservedUtc.HasValue, "Nowy bieżący skład jest oznaczony datą obserwacji.");
			DateTime? vehiclesObservedUtc = simRailTrain.VehiclesObservedUtc;
			List<SimRailTrain> list2 = Load();
			Attach(vehicleObservations, list2, new SimRailRunningTrain[0]);
			simRailTrain = list2.Single((SimRailTrain x) => x.trainNoLocal == "154037");
			Check(simRailTrain.VehiclesFromObservation && simRailTrain.VehiclesObservedUtc == vehiclesObservedUtc && SimRailTrainIdentity.Carrier(simRailTrain, "TME") == "ČD Cargo, a.s.", "Zniknięcie składu z API zachowuje ostatnio ustalonego przewoźnika w pamięci.");
			Check(SimRailTrainIdentity.CarrierInfo(simRailTrain, "TME").Evidence.Contains("Ostatnio zaobserwowany") && SimRailTrainIdentity.CarrierInfo(simRailTrain, "TME").Evidence.Contains("UTC"), "Podpowiedź ujawnia użycie wcześniejszej obserwacji i jej czas.");
			Check(list2.Single((SimRailTrain x) => x.trainNoLocal == "154039").Vehicles == null, "Obserwacja nie uzupełnia sąsiedniego kursu.");
			List<SimRailTrain> list3 = Load();
			Check(!LiveVehicles.TryLoad("pl2", list3, CancellationToken.None, () =>
			{
				throw new IOException("offline");
			}), "Symulowana awaria opcjonalnego API nie przerywa rozkładu.");
			vehicleObservations.Apply("pl2", list3);
			Check(list3.Single((SimRailTrain x) => x.trainNoLocal == "154037").VehiclesFromObservation && SimRailTrainIdentity.Carrier(list3.Single((SimRailTrain x) => x.trainNoLocal == "154037"), "TME") == "ČD Cargo, a.s.", "Błąd opcjonalnego API zachowuje wcześniej rozpoznany skład.");
			List<SimRailTrain> list4 = Load();
			List<SimRailRunningTrain> list5 = Live();
			list5[0].Vehicles = new List<string> { "Traxx/E186-246:P" };
			list5[0].RunId = "new-run";
			Attach(vehicleObservations, list4, list5);
			simRailTrain = list4.Single((SimRailTrain x) => x.trainNoLocal == "154037");
			Check(!simRailTrain.VehiclesFromObservation && simRailTrain.VehicleRunId == "new-run" && SimRailTrainIdentity.Carrier(simRailTrain, "TME") == "PCC Intermodal S.A.", "Nowy odczyt zastępuje poprzedni pojazd i jego przewoźnika.");
			list4 = Load();
			list5 = Live();
			list5[0].Vehicles = new List<string> { "CD163/163_999-9:G" };
			Attach(vehicleObservations, list4, list5);
			simRailTrain = list4.Single((SimRailTrain x) => x.trainNoLocal == "154037");
			Check(!simRailTrain.VehiclesFromObservation && SimRailTrainIdentity.Carrier(simRailTrain, "TME") == "", "Nieznany nowy wariant nie zachowuje błędnie dawnego przewoźnika.");
			vehicleObservations = new VehicleObservations();
			Attach(vehicleObservations, Load(), Live());
			List<SimRailTrain> list6 = Load();
			list6[0].timetable[0].actualArrivalTime = "2026-10-21 19:00:00";
			Attach(vehicleObservations, list6, new SimRailRunningTrain[0]);
			Check(list6[0].VehiclesFromObservation, "Zmiana rzeczywistego czasu nie zmienia tożsamości planowanego kursu.");
			string[] array = new string[5] { "date", "departure", "number", "relation", "route" };
			foreach (string text in array)
			{
				vehicleObservations = new VehicleObservations();
				Attach(vehicleObservations, Load(), Live());
				List<SimRailTrain> list7 = Load();
				SimRailTrain simRailTrain3 = list7.Single((SimRailTrain x) => x.trainNoLocal == "154037");
				switch (text)
				{
				case "date":
					foreach (SimRailPoint item in simRailTrain3.timetable)
					{
						item.arrivalTime = (item.arrivalTime ?? "").Replace("2026-10-21", "2026-10-22");
						item.departureTime = (item.departureTime ?? "").Replace("2026-10-21", "2026-10-22");
					}
					break;
				case "departure":
					simRailTrain3.timetable[0].departureTime = "2026-10-21 18:06:00";
					break;
				case "number":
					simRailTrain3.trainNoLocal = "154041";
					break;
				case "relation":
					simRailTrain3.endStation = "Inna stacja";
					break;
				case "route":
					simRailTrain3.timetable[0].pointId = "other-point";
					break;
				}
				Attach(vehicleObservations, list7, new SimRailRunningTrain[0]);
				Check(simRailTrain3.Vehicles == null, "Obserwacja nie przenosi się na zmieniony kurs: " + text + ".");
			}
			vehicleObservations = new VehicleObservations();
			Attach(vehicleObservations, Load(), Live());
			List<SimRailTrain> list8 = Load();
			vehicleObservations.Apply("pl3", list8);
			Check(list8.All((SimRailTrain x) => x.Vehicles == null), "Obserwacja nie przenosi się na inny serwer.");
			vehicleObservations = new VehicleObservations();
			Attach(vehicleObservations, Load(), Live());
			List<SimRailTrain> list9 = Load();
			list5 = Live();
			list5[0].EndStation = "Inny cel";
			Attach(vehicleObservations, list9, list5);
			Check(list9.Single((SimRailTrain x) => x.trainNoLocal == "154037").Vehicles == null, "Sprzeczny bieżący odczyt blokuje dawną obserwację.");
			List<SimRailTrain> list10 = Load();
			Attach(vehicleObservations, list10, new SimRailRunningTrain[0]);
			Check(list10.Single((SimRailTrain x) => x.trainNoLocal == "154037").Vehicles == null, "Odrzucona obserwacja nie wraca przy kolejnym braku składu.");
			vehicleObservations = new VehicleObservations();
			Attach(vehicleObservations, Load(), Live());
			List<SimRailTrain> list11 = Load();
			list5 = Live();
			Attach(vehicleObservations, list11, new SimRailRunningTrain[2]
			{
				list5[0],
				list5[0]
			});
			Check(list11.Single((SimRailTrain x) => x.trainNoLocal == "154037").Vehicles == null, "Duplikaty bieżącego numeru nie korzystają z wcześniejszego składu.");
			vehicleObservations = new VehicleObservations();
			Attach(vehicleObservations, Load(), Live());
			List<SimRailTrain> list12 = Load();
			list12.Add(Json.Clone(list12[0]));
			Attach(vehicleObservations, list12, new SimRailRunningTrain[0]);
			Check(list12.All((SimRailTrain x) => x.Vehicles == null), "Duplikaty numeru w rozkładzie nie dostają obserwacji.");
			vehicleObservations = new VehicleObservations();
			Attach(vehicleObservations, Load(), Live());
			vehicleObservations.Apply("pl2", new SimRailTrain[0]);
			list10 = Load();
			Attach(vehicleObservations, list10, new SimRailRunningTrain[0]);
			Check(list10.All((SimRailTrain x) => x.Vehicles == null), "Usunięty kurs usuwa również obserwację z pamięci.");
			vehicleObservations = new VehicleObservations();
			list10 = Load();
			Attach(vehicleObservations, list10, new SimRailRunningTrain[0]);
			Check(list10.All((SimRailTrain x) => x.Vehicles == null), "Nowa sesja nie odczytuje dawnych obserwacji z dysku.");
			Check(CarrierNames.Expanded("ŁKA") == "Łódzka Kolej Aglomeracyjna Spółka z o.o." && CarrierNames.Full("ŁKA") == "Łódzka Kolej Aglomeracyjna sp. z o.o.", "Pole P ma Spółka z o.o., a tabela sp. z o.o.");
			Check(CarrierNames.Expanded("Intercity") == "PKP Intercity Spółka Akcyjna" && CarrierNames.Expanded("ČD Cargo") == "ČD Cargo, a.s.", "Forma Spółka Akcyjna i obce formy prawne pozostają poprawne.");
			Check(Directory.GetFiles(folder).Length == 0 && !LocalPersistence.Enabled, "Obserwacje nie zapisują żadnych plików użytkownika.");
			vehicleObservations = new VehicleObservations();
			list = Load();
			Attach(vehicleObservations, list, Live());
			SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
			simRailSnapshot.ServerCode = "pl2";
			simRailSnapshot.Trains = list;
			simRailSnapshot.DispatcherNames = new HashSet<string> { "Gałkówek" };
			simRailSnapshot.GameTime = new DateTime(2026, 10, 21, 19, 28, 0);
			simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
			SimRailSnapshot simRailSnapshot2 = simRailSnapshot;
			SimRailSession simRailSession = new SimRailSession(null, simRailSnapshot2, "924");
			Station station = simRailSession.SuggestStation();
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
			engine2.ApplySimRailSnapshot(simRailSnapshot2);
			Check(engine2.State.Trains.Single((Train x) => x.ExternalTrainNo == "154037").Carrier == "ČD Cargo, a.s." && engine2.State.Trains.Single((Train x) => x.ExternalTrainNo == "154039").Carrier == "", "Projekcja Gałkówka rozróżnia aktualny 154037 i przyszły 154039.");
			List<SimRailTrain> trains = Load();
			Attach(vehicleObservations, trains, new SimRailRunningTrain[0]);
			SimRailSnapshot simRailSnapshot3 = new SimRailSnapshot();
			simRailSnapshot3.ServerCode = "pl2";
			simRailSnapshot3.Trains = trains;
			simRailSnapshot3.DispatcherNames = simRailSnapshot2.DispatcherNames;
			simRailSnapshot3.GameTime = simRailSnapshot2.GameTime;
			simRailSnapshot3.DownloadedUtc = DateTime.UtcNow;
			simRailSnapshot2 = simRailSnapshot3;
			engine2.ApplySimRailSnapshot(simRailSnapshot2);
			Check(engine2.State.Trains.Single((Train x) => x.ExternalTrainNo == "154037").Carrier == "ČD Cargo, a.s." && engine2.State.Trains.Single((Train x) => x.ExternalTrainNo == "154037").CarrierSource.Contains("Ostatnio zaobserwowany"), "Odświeżenie EDR nie kasuje przewoźnika zaobserwowanego w tej sesji.");
			using (MainForm mainForm = new MainForm(engine2))
			{
				mainForm.Show();
				mainForm.Size = new Size(1500, 850);
				mainForm.SelectTrain(engine2.State.Trains.Single((Train x) => x.ExternalTrainNo == "154037").Id);
				Application.DoEvents();
				DataGridView dataGridView = (DataGridView)typeof(MainForm).GetField("trains", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mainForm);
				DataGridViewRow dataGridViewRow = dataGridView.Rows.Cast<DataGridViewRow>().Single((DataGridViewRow x) => Convert.ToString(x.Cells["carrier"].Value) == "ČD Cargo, a.s.");
				Check(!dataGridView.ShowCellToolTips && string.IsNullOrEmpty(dataGridViewRow.Cells["carrier"].ToolTipText), "Widok zachowuje przewoźnika bez podpowiedzi o źródle.");
				Ui.Capture(mainForm, Path.Combine(folder, "przewoznicy-1.0.10.199.png"));
				mainForm.Close();
			}
			Check(!Directory.Exists(store.DirectoryPath) && !LocalPersistence.Enabled, "Widok i obserwacje nadal nie tworzą bazy użytkownika.");
			log.Add("ALL CARRIER 39 TESTS PASSED");
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
			File.WriteAllLines(Path.Combine(folder, "wyniki-przewoznicy39.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EdrLocal;

public static class Appearance48Tests
{
	private static readonly List<string> log = new List<string>();

	private static readonly DateTime day = new DateTime(2026, 10, 22, 9, 0, 0);

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

	private static void Call(object obj, string name)
	{
		try
		{
			obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, null);
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

	private static SimRailTrain Source()
	{
		SimRailTrain simRailTrain = new SimRailTrain();
		simRailTrain.trainNoLocal = "474046";
		simRailTrain.trainName = "TME";
		simRailTrain.startStation = "A";
		simRailTrain.endStation = "C";
		simRailTrain.locoType = "EU07 (4E)";
		simRailTrain.trainLength = 268m;
		simRailTrain.trainWeight = 1363m;
		simRailTrain.VehicleRunId = "run48";
		simRailTrain.Vehicles = new List<string> { "4E/EU07-241:G", "424Z/424Z:P:0" };
		simRailTrain.isDangerousCargo = true;
		simRailTrain.isHighRiskCargo = true;
		simRailTrain.isOverGauge = true;
		simRailTrain.isQualityTracked = true;
		simRailTrain.usageNotes = "Próba kolorów";
		simRailTrain.timetable = new List<SimRailPoint>
		{
			new SimRailPoint
			{
				pointId = "10",
				nameForPerson = "A",
				indexOfPoint = 0,
				arrivalTime = "2026-10-22 08:50:00",
				departureTime = "2026-10-22 08:50:00",
				displayedTrainNumber = "474046",
				trainType = "TME",
				isActive = true,
				line = 17
			},
			new SimRailPoint
			{
				pointId = "60",
				nameForPerson = "B",
				indexOfPoint = 1,
				arrivalTime = "2026-10-22 09:10:00",
				departureTime = "2026-10-22 09:12:00",
				displayedTrainNumber = "474046",
				trainType = "TME",
				isActive = true,
				line = 17,
				track = 1
			},
			new SimRailPoint
			{
				pointId = "20",
				nameForPerson = "C",
				indexOfPoint = 2,
				arrivalTime = "2026-10-22 09:20:00",
				departureTime = "2026-10-22 09:20:00",
				displayedTrainNumber = "474046",
				trainType = "TME",
				isActive = true,
				line = 17
			}
		};
		return simRailTrain;
	}

	private static SimRailSnapshot Snapshot(SimRailTrain t)
	{
		SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
		simRailSnapshot.ServerCode = "test48";
		simRailSnapshot.Trains = new List<SimRailTrain> { t };
		simRailSnapshot.DispatcherNames = new HashSet<string> { "A", "B", "C" };
		simRailSnapshot.GameTime = day;
		simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
		return simRailSnapshot;
	}

	private static Engine Make(string path, SimRailTrain t)
	{
		Store store = new Store(path);
		store.Load();
		SimRailSession simRailSession = new SimRailSession(null, Snapshot(t), "60");
		Station station = simRailSession.SuggestStation();
		StationSettings.Normalize(station);
		store.Commit((AppState s) =>
		{
			s.Stations = new List<Station> { station };
			s.Trains.Clear();
			s.Entries.Clear();
			s.Duties.Clear();
			s.History.Clear();
		});
		Engine engine = new Engine(store);
		engine.User = store.State.Users.First((LocalUser x) => x.Id == "DEMO");
		engine.SimRail = simRailSession;
		Engine engine2 = engine;
		engine2.ApplySimRailSnapshot(simRailSession.Snapshot);
		engine2.BeginDuty(day.AddMinutes(-5.0));
		return engine2;
	}

	private static bool Mark(DataGridViewCell cell, Color back, Color ink)
	{
		if (cell.Style.BackColor == back && cell.Style.SelectionBackColor == back && cell.Style.ForeColor == ink)
		{
			return cell.Style.SelectionForeColor == ink;
		}
		return false;
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		LocalPersistence.Enabled = false;
		try
		{
			string[][] array = new string[7][]
			{
				new string[3] { "4E/EU07-241:G", "EU07", "241" },
				new string[3] { "CD163/163_034-2:P", "163", "034-2" },
				new string[3] { "Impuls/36wed-001", "36WEd", "001" },
				new string[3] { "Pendolino/ED250-018 Variant", "ED250", "018" },
				new string[3] { "Dragon2/E6ACTadb-027:G", "E6ACTa", "027" },
				new string[3] { "Ty2/Ty2-477", "Ty2", "477" },
				new string[3] { "BR285/BR285-123:P", "BR285", "123" }
			};
			foreach (string[] array2 in array)
			{
				List<ActualTraction> list = ActualTraction.Read(new string[1] { array2[0] });
				Check(list.Count == 1 && list[0].Series == array2[1] && list[0].Number == array2[2], "Numer i seria z pojazdu: " + array2[0] + ".");
			}
			Check(ActualTraction.Read(new string[4] { "424Z/424Z:P:0", "629Z/434Z_31514553133-5:G:25@RandomContainer1x40", "unknown/EU07-999", "4E/EU07-foo" }).Count == 0, "Wagony, nieznane rodziny i brak numeru nie tworzą fikcyjnych lokomotyw.");
			List<ActualTraction> list2 = ActualTraction.Read(new string[3] { "4E/EU07-241:G", "201E/ET22-644:R", "424Z/wagon:P" });
			Check(list2.Count == 2 && list2[0].Number == "241" && list2[1].Number == "644", "Wiele lokomotyw zachowuje właściwą kolejność i numery.");
			SimRailTrain simRailTrain = Source();
			TrainConsist trainConsist = ConsistPlan.Project(simRailTrain);
			Check(trainConsist.PlannedSeries.Single() == "EU07" && trainConsist.Series.Single() == "EU07" && trainConsist.Numbers.Single() == "241", "Seria planowa pozostaje z RJ; rzeczywista dostaje numer z konkretnego składu.");
			string[] array3 = new string[29];
			ConsistPlan.RouteCells(array3, trainConsist);
			Check(array3[23] == "EU07" && array3[26] == "EU07-241" && array3[24] == array3[27] && array3[25] == array3[28], "Kolumna Seria rz. pokazuje EU07-241; masa i długość nadal pochodzą z RJ.");
			simRailTrain.locoType = null;
			simRailTrain.trainLength = null;
			simRailTrain.trainWeight = null;
			trainConsist = ConsistPlan.Project(simRailTrain);
			Check(trainConsist != null && trainConsist.Numbers.Single() == "241" && !trainConsist.PlannedGross.HasValue && trainConsist.PlannedSeries.Length == 0, "Sam konkretny pojazd wystarcza do rzeczywistej serii, bez wymyślania planu i brutto.");
			simRailTrain.VehicleMatchRejected = true;
			Check(ConsistPlan.Project(simRailTrain) == null, "Odrzucone powiązanie kursu nie dostaje numeru lokomotywy.");
			simRailTrain = Source();
			simRailTrain.Vehicles = null;
			Check(ConsistPlan.Project(simRailTrain).Numbers.Length == 0, "Przyszły kurs bez obserwacji nie dostaje losowego numeru.");
			simRailTrain = Source();
			VehicleObservations vehicleObservations = new VehicleObservations();
			vehicleObservations.Apply("test48", new SimRailTrain[1] { simRailTrain });
			SimRailTrain simRailTrain2 = Source();
			simRailTrain2.Vehicles = null;
			simRailTrain2.VehicleRunId = null;
			vehicleObservations.Apply("test48", new SimRailTrain[1] { simRailTrain2 });
			Check(ConsistPlan.Project(simRailTrain2).Numbers.Single() == "241" && simRailTrain2.VehiclesFromObservation, "Numer pozostaje po zniknięciu bieżącego składu dzięki obserwacji tego samego kursu.");
			simRailTrain2 = Source();
			simRailTrain2.Vehicles = null;
			simRailTrain2.trainNoLocal = "474048";
			vehicleObservations.Apply("test48", new SimRailTrain[1] { simRailTrain2 });
			Check(ConsistPlan.Project(simRailTrain2).Numbers.Length == 0, "Obserwacja numeru lokomotywy nie przechodzi na inny kurs.");
			TrainConsist trainConsist2 = ConsistPlan.Project(Source());
			trainConsist2.ManualAnalysis = true;
			trainConsist2.Numbers = new string[1] { "999" };
			SimRailTrain simRailTrain3 = Source();
			simRailTrain3.Vehicles = new List<string> { "4E/EU07-096:G" };
			Check(ConsistPlan.Merge(ConsistPlan.Project(simRailTrain3), trainConsist2).Numbers.Single() == "999", "Ręczna analiza zachowuje pierwszeństwo przed kolejnym odczytem pojazdów.");
			string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src/testdata/live-vehicles37.json");
			SimRailEnvelope<SimRailRunningTrain> simRailEnvelope = SimRailProvider.Parse<SimRailEnvelope<SimRailRunningTrain>>(File.ReadAllBytes(path));
			Check(simRailEnvelope.data.Count == 147 && simRailEnvelope.data.All((SimRailRunningTrain x) => ActualTraction.Read(x.Vehicles).Count > 0), "147 zachowanych rzeczywistych składów ma odczytaną co najmniej jedną serię z numerem.");
			simRailTrain = Source();
			Engine engine = Make(Path.Combine(folder, "memory"), simRailTrain);
			Train train = engine.State.Trains.Single();
			Check(train.QualityTracked && train.Dangerous && train.HighRisk && train.Extraordinary, "Flagi JAKOŚĆ, TN, TWR i PN pochodzą z API SimRail.");
			Check(engine.SimRail.Route(train).All((DetailRow x) => x.Cells[26] == "EU07-241"), "Seria i numer są widoczne na całej trasie pociągu.");
			using (StopForecastForm stopForecastForm = new StopForecastForm(train, (StopForecastOperation stop, DateTime a, DateTime d) => Task.FromResult(result: true)))
			{
				stopForecastForm.Show();
				Application.DoEvents();
				Label label = All(stopForecastForm).OfType<Label>().Single((Label x) => x.Text == "UWAGA! W tym oknie możliwa jest tylko obsługa \"Pociąg STÓJ\" i PROGNOZY Przyjazdu / Odjazdu.");
				Check(label.ForeColor == Color.Red && label.Font.Bold, "Ostrzeżenie jest dosłownie zgodne z wzorem i zapisane czerwoną pogrubioną czcionką.");
				Check(TextRenderer.MeasureText(label.Text, label.Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width <= label.Width, "Pełny tekst ostrzeżenia mieści się bez ucięcia.");
				Ui.Capture(stopForecastForm, Path.Combine(folder, "postoj-1.0.10.199.png"));
				stopForecastForm.Close();
			}
			using (MainForm mainForm = new MainForm(engine))
			{
				mainForm.Show();
				mainForm.Size = new Size(1500, 900);
				Application.DoEvents();
				mainForm.SelectTrain(train.Id);
				mainForm.RefreshData();
				DataGridView dataGridView = Field<DataGridView>(mainForm, "trains");
				DataGridViewRow dataGridViewRow = dataGridView.Rows[0];
				Check(Mark(dataGridViewRow.Cells["from"], Color.FromArgb(190, 86, 133), Color.White), "TN: fioletowe Z kierunku i biały tekst także po zaznaczeniu.");
				Check(Mark(dataGridViewRow.Cells["to"], Color.FromArgb(255, 128, 64), Color.Black), "TWR: pomarańczowe W kierunku także po zaznaczeniu.");
				Check(Mark(dataGridViewRow.Cells["even"], Color.Yellow, Color.Black), "PN: żółty numer parzysty także po zaznaczeniu.");
				Check(Mark(dataGridViewRow.Cells["da"], EdrPalette.Quality, Color.Black), "JAKOŚĆ: właściwe tło różnicy przyjazdu także po zaznaczeniu.");
				Check(dataGridViewRow.Cells["k"].Style.SelectionBackColor == Color.FromArgb(255, 83, 83), "Uwagi eksploatacyjne zachowują czerwony znacznik K.");
				train = engine.State.Trains.Single();
				train.Kind = "MPE";
				train.Profile.CarrierNotes = "Uwagi przewoźnika";
				train.Stopped = true;
				train.Forecast = null;
				mainForm.RefreshData();
				dataGridViewRow = dataGridView.Rows[0];
				Check(Mark(dataGridViewRow.Cells["carrier"], Color.Yellow, Color.Black), "Uwagi przewoźnika pociągu pasażerskiego wyróżniają nazwę na żółto.");
				Check(Mark(dataGridViewRow.Cells["rd"], Color.Pink, Color.Black), "STÓJ bez prognozy jest różowy także przy zaznaczeniu.");
				train.Forecast = day.AddMinutes(20.0);
				mainForm.RefreshData();
				Check(Mark(dataGridView.Rows[0].Cells["rd"], Color.Orange, Color.Black), "STÓJ z prognozą jest pomarańczowy także przy zaznaczeniu.");
				Ui.Capture(mainForm, Path.Combine(folder, "oznaczenia-1.0.10.199.png"));
				Train train2 = train;
				Train train3 = train;
				Train train4 = train;
				Train train5 = train;
				bool flag = (train.Stopped = false);
				bool flag3 = (train5.QualityTracked = flag);
				bool flag5 = (train4.Extraordinary = flag3);
				bool dangerous = (train3.HighRisk = flag5);
				train2.Dangerous = dangerous;
				train.Profile.CarrierNotes = "";
				mainForm.RefreshData();
				dataGridViewRow = dataGridView.Rows[0];
				Check(dataGridViewRow.Cells["from"].Style.BackColor == EdrPalette.Incoming && dataGridViewRow.Cells["to"].Style.BackColor == EdrPalette.Outgoing && dataGridViewRow.Cells["even"].Style.BackColor == EdrPalette.OutgoingNumber && dataGridViewRow.Cells["da"].Style.BackColor == EdrPalette.Cream, "Usunięcie flag przywraca zwykłe tła i nie pozostawia starego ostrzeżenia.");
				string[] array4 = new string[4] { "end", "start", "pass", "stay" };
				foreach (string text in array4)
				{
					train.Origin = ((text == "start") ? engine.Station.Name : "A");
					train.Destination = ((text == "end") ? engine.Station.Name : "C");
					train.PlanDeparture = ((text == "stay") ? train.PlanArrival.AddMinutes(2.0) : train.PlanArrival);
					mainForm.RefreshData();
					Color color = text switch
					{
						"pass" => Color.Cyan, 
						"start" => Color.Lime, 
						"end" => Color.Magenta, 
						_ => Color.FromArgb(51, 153, 255), 
					};
					Check(dataGridView.Rows[0].DefaultCellStyle.SelectionBackColor.ToArgb() == color.ToArgb(), "Wstęga SimRail zgodna z ERJ 2022: " + text + ".");
				}
				train.Number = "474045";
				train.Extraordinary = true;
				mainForm.RefreshData();
				Check(Mark(dataGridView.Rows[0].Cells["odd"], Color.Yellow, Color.Black), "PN działa również dla numeru nieparzystego.");
				train.Number = simRailTrain.trainNoLocal;
				train.Kind = "TME";
				train.Origin = "A";
				train.Destination = "C";
				mainForm.RefreshData();
				TabControl tabControl = Field<TabControl>(mainForm, "topTabs");
				tabControl.SelectedIndex = 2;
				Call(mainForm, "RefreshDetails");
				DataGridView dataGridView2 = Field<Dictionary<string, DataGridView>>(mainForm, "detailGrids")["route"];
				dataGridView2.FirstDisplayedScrollingColumnIndex = 23;
				Check(dataGridView2.Rows.Count == 3 && dataGridView2.Rows[1].Cells[26].Value.ToString() == "EU07-241", "Rzeczywista tabela trasy pokazuje numer pobrany z pojazdu.");
				Ui.Capture(mainForm, Path.Combine(folder, "seria-rzeczywista-1.0.10.199.png"));
				mainForm.Close();
			}
			using (LegendForm legendForm = new LegendForm())
			{
				legendForm.Show();
				Application.DoEvents();
				List<Label> source = All(legendForm).OfType<Label>().ToList();
				Check(source.Any((Label x) => x.Text == "Z kierunku post." && x.BackColor == EdrPalette.Dangerous && x.ForeColor == Color.White) && source.Any((Label x) => x.Text == "W kierunku post." && x.BackColor == EdrPalette.HighRisk), "Legenda ma te same kolory i tekst co wykaz pociągów.");
				Ui.Capture(legendForm, Path.Combine(folder, "legenda-1.0.10.199.png"));
				legendForm.Close();
			}
			Check(!Directory.Exists(Path.Combine(folder, "memory")), "Poprawki nie włączają trwałego zapisu sesji.");
			return true;
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			return false;
		}
		finally
		{
			File.WriteAllLines(Path.Combine(folder, "wyniki-wyglad48.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

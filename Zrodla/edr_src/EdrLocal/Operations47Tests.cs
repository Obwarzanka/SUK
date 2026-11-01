using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EdrLocal;

public static class Operations47Tests
{
	private sealed class Feed : ISimulatorFeed
	{
		public int Reads;

		public SimRailSnapshot Value;

		public string ProviderId => "test47";

		public Task<List<SimRailServer>> ServersAsync(CancellationToken c)
		{
			throw new NotImplementedException();
		}

		public Task<SimRailSnapshot> SnapshotAsync(string s, CancellationToken c)
		{
			c.ThrowIfCancellationRequested();
			Reads++;
			return Task.FromResult(Value);
		}
	}

	private static List<string> log = new List<string>();

	private static DateTime day = new DateTime(2026, 10, 22, 9, 0, 0);

	private static void Check(bool ok, string name)
	{
		if (!ok)
		{
			throw new Exception("FAIL: " + name);
		}
		log.Add("PASS: " + name);
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
		simRailTrain.trainNoLocal = "2704";
		simRailTrain.trainName = "ROJ";
		simRailTrain.startStation = "A";
		simRailTrain.endStation = "C";
		simRailTrain.timetable = new List<SimRailPoint>
		{
			new SimRailPoint
			{
				pointId = "10",
				nameForPerson = "A",
				indexOfPoint = 0,
				arrivalTime = "2026-10-22 08:50:00",
				departureTime = "2026-10-22 08:50:00",
				displayedTrainNumber = "2704",
				trainType = "ROJ",
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
				displayedTrainNumber = "2704",
				trainType = "ROJ",
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
				displayedTrainNumber = "2704",
				trainType = "ROJ",
				isActive = true,
				line = 17
			}
		};
		return simRailTrain;
	}

	private static PlannedTrain Plan(SimRailTrain t)
	{
		PlannedTrain plannedTrain = new PlannedTrain();
		plannedTrain.trainNoLocal = t.trainNoLocal;
		plannedTrain.trainName = t.trainName;
		plannedTrain.startStation = t.startStation;
		plannedTrain.endStation = t.endStation;
		plannedTrain.locoType = "EN57 (5B+6B+5B)";
		plannedTrain.trainLength = 128m;
		plannedTrain.trainWeight = 290m;
		plannedTrain.timetable = Json.Clone(t.timetable);
		return plannedTrain;
	}

	private static SimRailSnapshot Snapshot(SimRailTrain t)
	{
		SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
		simRailSnapshot.ServerCode = "test47";
		simRailSnapshot.Trains = new List<SimRailTrain> { t };
		simRailSnapshot.DispatcherNames = new HashSet<string> { "A", "B", "C" };
		simRailSnapshot.GameTime = day;
		simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
		return simRailSnapshot;
	}

	private static Engine Make(string path, SimRailTrain source)
	{
		Store store = new Store(path);
		store.Load();
		SimRailSession simRailSession = new SimRailSession(null, Snapshot(source), "60");
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

	private static void Reject(Action a, string name)
	{
		bool ok = false;
		try
		{
			a();
		}
		catch (InvalidOperationException)
		{
			ok = true;
		}
		Check(ok, name);
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		LocalPersistence.Enabled = false;
		try
		{
			Check(VehicleCatalog.Entries.Count == 511 && VehicleCatalog.Entries.All((VehicleSpec x) => x.SourceRow < 519 && !string.IsNullOrWhiteSpace(x.Traction)), "Katalog zawiera 511 pojazdów z głównej tabeli; tabela czasu drzwi jest pominięta.");
			VehicleSpec vehicleSpec = VehicleCatalog.Find("EU07");
			int ok;
			decimal? length;
			if (vehicleSpec != null && vehicleSpec.Length > (decimal?)15m)
			{
				length = vehicleSpec.Length;
				if (length.GetValueOrDefault() < 17m && length.HasValue && !(vehicleSpec.Axles != (decimal?)4m))
				{
					ok = ((vehicleSpec.MaxSpeed >= (decimal?)120m) ? 1 : 0);
					goto IL_013e;
				}
			}
			ok = 0;
			goto IL_013e;
			IL_03ad:
			int ok2;
			Check((byte)ok2 != 0, "Planowane i domyślne rzeczywiste długość oraz brutto pochodzą z RJ.");
			TrainConsist trainConsist;
			Check(trainConsist.PlannedSeries.Single() == "EN57" && trainConsist.Series.Single() == "EN57" && trainConsist.Numbers.Length == 0, "Seria RJ jest oczyszczana z opisu technicznego; numer pojazdu nie jest wymyślany.");
			int[] array = new int[6] { 0, 1, 2, 3, 4, 5 };
			foreach (int num2 in array)
			{
				SimRailTrain simRailTrain = Source();
				PlannedTrain plannedTrain = Plan(simRailTrain);
				if (num2 == 0)
				{
					plannedTrain.startStation = "X";
				}
				if (num2 == 1)
				{
					plannedTrain.trainName = "X";
				}
				if (num2 == 2)
				{
					plannedTrain.timetable[1].arrivalTime = "2026-10-23 09:10:00";
				}
				if (num2 == 3)
				{
					plannedTrain.timetable[1].pointId = "61";
				}
				if (num2 == 4)
				{
					plannedTrain.timetable.RemoveAt(0);
				}
				if (num2 == 5)
				{
					plannedTrain.timetable[1].displayedTrainNumber = "2706";
				}
				List<SimRailTrain> list = new List<SimRailTrain>();
				list.Add(simRailTrain);
				ConsistPlan.Attach(list, new List<PlannedTrain> { plannedTrain });
				Check(ConsistPlan.Project(simRailTrain) == null, "Odrzucono błędne dopasowanie RJ: wariant " + num2 + ".");
			}
			SimRailTrain simRailTrain2 = Source();
			List<SimRailTrain> list2 = new List<SimRailTrain>();
			list2.Add(simRailTrain2);
			ConsistPlan.Attach(list2, new List<PlannedTrain>
			{
				Plan(simRailTrain2),
				Plan(simRailTrain2)
			});
			Check(ConsistPlan.Project(simRailTrain2) == null, "Duplikat numeru rozkładu nie przypisuje analizy.");
			List<SimRailTrain> list3 = new List<SimRailTrain>();
			SimRailTrain simRailTrain3;
			list3.Add(simRailTrain3);
			ConsistPlan.TryLoad(list3, CancellationToken.None, () =>
			{
				throw new WebException("test");
			});
			Check(simRailTrain3.trainWeight == (decimal?)290m, "Awaria opcjonalnego rozkładu parametrów nie usuwa danych EDR.");
			bool ok3 = false;
			CancellationToken token = new CancellationToken(canceled: true);
			try
			{
				List<SimRailTrain> list4 = new List<SimRailTrain>();
				list4.Add(simRailTrain3);
				ConsistPlan.TryLoad(list4, token, () => new byte[0]);
			}
			catch (OperationCanceledException)
			{
				ok3 = true;
			}
			Check(ok3, "Anulowanie pobierania parametrów jest respektowane.");
			Engine engine = Make(Path.Combine(folder, "memory"), simRailTrain3);
			Train train = engine.State.Trains.Single();
			List<DetailRow> list5 = engine.SimRail.Route(train).ToList();
			Check(list5.Count == 3 && list5.All((DetailRow x) => x.Cells[23] == "EN57" && x.Cells[24] == "128" && x.Cells[25] == "290" && x.Cells[26] == "EN57" && x.Cells[27] == "128" && x.Cells[28] == "290"), "Sześć kolumn analizy jest wypełnionych na całej trasie SimRail.");
			Check(list5.All((DetailRow x) => string.IsNullOrEmpty(x.Cells[22])), "Maszynista pozostaje pusty bez ręcznego wpisu.");
			train.Consist.ManualAnalysis = true;
			train.Consist.Length = 134m;
			train.Consist.Gross = 301m;
			train.Consist.Series = new string[1] { "EU07" };
			train.Consist.Numbers = new string[1] { "241" };
			simRailTrain3.trainLength = 130m;
			simRailTrain3.trainWeight = 295m;
			engine.ApplySimRailSnapshot(Snapshot(simRailTrain3));
			train = engine.State.Trains.Single();
			length = train.Consist.PlannedLength;
			int ok4;
			if (length.GetValueOrDefault() == 130m && length.HasValue && train.Consist.PlannedGross == (decimal?)295m)
			{
				length = train.Consist.Length;
				if (length.GetValueOrDefault() == 134m && length.HasValue && !(train.Consist.Gross != (decimal?)301m))
				{
					ok4 = ((train.Consist.Series[0] == "EU07") ? 1 : 0);
					goto IL_08a3;
				}
			}
			ok4 = 0;
			goto IL_08a3;
			IL_08a3:
			Check((byte)ok4 != 0, "Odświeżenie aktualizuje plan i zachowuje ręczną analizę rzeczywistą.");
			TrainConsist trainConsist2 = ConsistPlan.Merge(ConsistPlan.Project(simRailTrain3), new TrainConsist
			{
				Length = 1m,
				Gross = 1m
			});
			Check(trainConsist2.Length == (decimal?)130m && trainConsist2.Gross == (decimal?)295m, "Domyślne wartości rzeczywiste śledzą RJ do pierwszej ręcznej zmiany.");
			SimRailTrain simRailTrain4 = Source();
			PlannedTrain plannedTrain2 = Plan(simRailTrain4);
			plannedTrain2.timetable.Insert(1, new SimRailPoint
			{
				pointId = "15",
				arrivalTime = "2026-10-22 09:00:00",
				departureTime = "2026-10-22 09:00:00",
				displayedTrainNumber = "2704"
			});
			List<SimRailTrain> list6 = new List<SimRailTrain>();
			list6.Add(simRailTrain4);
			ConsistPlan.Attach(list6, new List<PlannedTrain> { plannedTrain2 });
			Check(simRailTrain4.trainLength == (decimal?)128m, "Pełny RJ może zawierać dodatkowe punkty pominięte w EDR, przy zachowaniu kolejności i dat.");
			SimRailTrain simRailTrain5 = Source();
			PlannedTrain plannedTrain3 = Plan(simRailTrain5);
			plannedTrain3.timetable.Reverse();
			List<SimRailTrain> list7 = new List<SimRailTrain>();
			list7.Add(simRailTrain5);
			ConsistPlan.Attach(list7, new List<PlannedTrain> { plannedTrain3 });
			Check(!simRailTrain5.trainLength.HasValue, "Niezgodna kolejność punktów nie przypisuje parametrów RJ.");
			Check(ConsistPlan.Merge(null, train.Consist).ManualAnalysis, "Chwilowy brak parametrów nie gubi ręcznej analizy.");
			StopForecastRequest request = StopForecastRequest.From(train, engine.SimRail, stop: false, day.AddMinutes(15.0), day.AddMinutes(17.0));
			Reject(() =>
			{
				request.Validate(engine.SimRail);
			}, "Prognoza wymaga aktualnego linku dla posterunku.");
			engine.SimRail.SetAccessLink("https://api.simrail.eu:8093/?serverCode=test47&stationId=60&key=test-only");
			request.Validate(engine.SimRail);
			Check(!request.Confirmed(engine.SimRail.Snapshot), "Niezapisana prognoza nie jest raportowana jako potwierdzona.");
			SimRailTrain simRailTrain6 = Json.Clone(simRailTrain3);
			simRailTrain6.timetable[1].actualArrivalTime = "2026-10-22 09:15:00";
			simRailTrain6.timetable[1].actualDepartureTime = "2026-10-22 09:17:00";
			Check(request.Confirmed(Snapshot(simRailTrain6)), "Prognozę potwierdzają oba czasy i brak potwierdzenia rzeczywistego przejazdu.");
			simRailTrain6.timetable[1].isConfirmed = true;
			Check(request.Confirmed(Snapshot(simRailTrain6)), "Zatwierdzenie pociągu może współistnieć z zapisaną prognozą; nadal wymagane są zgodne godziny.");
			engine.SimRail.Accept(Snapshot(simRailTrain6));
			request.Validate(engine.SimRail);
			Check(ok: true, "Prognoza jest dozwolona również dla zatwierdzonego pociągu.");
			request.Stop = true;
			Check(!request.Confirmed(Snapshot(simRailTrain6)), "Sam przejazd nie potwierdza operacji STÓJ.");
			simRailTrain6.timetable[1].isStoped = true;
			Check(request.Confirmed(Snapshot(simRailTrain6)), "Postój wymaga flagi isStoped z właściwego punktu API.");
			SimRailSnapshot simRailSnapshot = Snapshot(simRailTrain6);
			simRailSnapshot.ServerCode = "other";
			Check(!request.Confirmed(simRailSnapshot), "Inny serwer nie może potwierdzić postoju.");
			simRailTrain6.timetable[1].arrivalTime = "2026-10-23 09:10:00";
			Check(!request.Confirmed(Snapshot(simRailTrain6)), "Postój innej doby nie potwierdza bieżącej operacji.");
			simRailTrain6.timetable[1].arrivalTime = simRailTrain3.timetable[1].arrivalTime;
			engine.SimRail.Accept(Snapshot(simRailTrain3));
			request.Stop = false;
			request.Arrival = day.AddMinutes(20.0);
			request.Departure = day.AddMinutes(10.0);
			bool ok5 = false;
			try
			{
				request.Validate(engine.SimRail);
			}
			catch (TrainTimeException)
			{
				ok5 = true;
			}
			Check(ok5, "Prognoza sprawdza chronologię przyjazdu i odjazdu.");
			request.Arrival = day.AddMinutes(15.0);
			request.Departure = day.AddMinutes(17.0);
			simRailTrain3.timetable[1].actualArrivalTime = "2026-10-22 09:15:00";
			simRailTrain3.timetable[1].actualDepartureTime = "2026-10-22 09:17:00";
			request.Stop = true;
			File.WriteAllText(Path.Combine(folder, "stop.js"), SepeTimeTransfer.StopRequest(request.Identity), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			Feed feed = new Feed();
			feed.Value = Snapshot(simRailTrain3);
			Feed feed2 = feed;
			SimRailSession session = new SimRailSession(feed2, feed2.Value, "60");
			Check(!StopForecastRequest.Read(request, session, null, CancellationToken.None, 0).GetAwaiter().GetResult() && feed2.Reads == 3, "Brak potwierdzenia kończy się po trzech odczytach, bez ponowienia kliknięcia.");
			feed2.Reads = 0;
			simRailTrain3.timetable[1].isStoped = true;
			Check(StopForecastRequest.Read(request, session, null, CancellationToken.None, 0).GetAwaiter().GetResult() && feed2.Reads == 1, "Pierwsze właściwe potwierdzenie kończy odpytywanie.");
			engine.ApplySimRailSnapshot(Snapshot(simRailTrain3));
			using (MainForm mainForm = new MainForm(engine))
			{
				mainForm.Show();
				Application.DoEvents();
				DataGridView dataGridView = Field<DataGridView>(mainForm, "trains");
				Check(dataGridView.Columns["k"] is DataGridViewCheckBoxColumn && dataGridView.Columns["nk"] is DataGridViewCheckBoxColumn && dataGridView.ReadOnly && dataGridView.Columns["k"].ReadOnly && dataGridView.Columns["nk"].ReadOnly, "K i NK są natywnymi, tylko do odczytu kolumnami checkbox.");
				Check(dataGridView.Rows.Count == 1 && (bool)dataGridView.Rows[0].Cells["k"].Value && !(bool)dataGridView.Rows[0].Cells["nk"].Value, "K/NK odzwierciedlają uruchomienie pociągu jako wartości bool.");
				dataGridView.CurrentCell = dataGridView.Rows[0].Cells["k"];
				Check(!dataGridView.BeginEdit(selectAll: true) && (bool)dataGridView.CurrentCell.Value, "Checkboxu nie można przełączyć ręczną edycją.");
				Ui.Capture(mainForm, Path.Combine(folder, "timetable-1.0.10.199.png"));
				Exception error = null;
				System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer
				{
					Interval = 70
				};
				try
				{
					timer.Tick += (object s, EventArgs e) =>
					{
						Editor editor = Application.OpenForms.OfType<Editor>().FirstOrDefault();
						if (editor == null)
						{
							return;
						}
						timer.Stop();
						try
						{
							List<ComboBox> list10 = (from x in All(editor).OfType<ComboBox>()
								where x.Items.Contains("**BRAK**")
								select x).ToList();
							Check(list10.Count == 3 && list10.All((ComboBox x) => x.Items.Contains("E186") && x.Items.Contains("36WEd")), "Każda z trzech lokomotyw ma pełny katalog i dopuszcza ręczny wpis.");
							list10[0].Text = "EN57";
							Check(All(editor).OfType<Label>().Any((Label x) => x.Text.Contains("63,97 m") && x.Text.Contains("123 t")), "Zmiana serii pokazuje rzeczywiste parametry katalogu.");
							Ui.Capture(editor, Path.Combine(folder, "analysis-1.0.10.199.png"));
							list10[0].Text = "E186";
							list10[1].Text = "EN57AKMw1";
							GroupBox c = All(editor).OfType<GroupBox>().Single((GroupBox x) => x.Text == "Dane lokomotyw");
							All(c).OfType<TextBox>().First().Text = "139";
							All(editor).OfType<Button>().Single((Button x) => x.Text == "Zapisz").PerformClick();
						}
						catch (Exception ex4)
						{
							error = ex4;
						}
						finally
						{
							editor.Close();
						}
					};
					timer.Start();
					Call(mainForm, "TrainAnalysis");
				}
				finally
				{
					if (timer != null)
					{
						((IDisposable)timer).Dispose();
					}
				}
				if (error != null)
				{
					throw error;
				}
				TrainConsist consist = engine.State.Trains.Single().Consist;
				Check(consist.ManualAnalysis && consist.Series[0] == "E186" && consist.Series[1] == "EN57AKM" && consist.VehicleTypes[1] == "EN57AKMw1" && consist.Numbers[0] == "139" && consist.PlannedLength == (decimal?)130m, "Zapis ręcznej analizy przechowuje serię i numer oraz zachowuje wartości planowe.");
				engine.ApplySimRailSnapshot(Snapshot(simRailTrain3));
				Check(engine.State.Trains.Single().Consist.Series[0] == "E186", "Ręczna seria z formularza pozostaje po odświeżeniu API.");
				System.Windows.Forms.Timer timer2 = new System.Windows.Forms.Timer
				{
					Interval = 70
				};
				try
				{
					timer2.Tick += (object sender, EventArgs args) =>
					{
						StopForecastForm stopForecastForm4 = Application.OpenForms.OfType<StopForecastForm>().FirstOrDefault();
						if (stopForecastForm4 == null)
						{
							return;
						}
						timer2.Stop();
						try
						{
							Check(!Application.OpenForms.OfType<SepeBrowserForm>().Any(), "Akcja Poc. STÓJ otwiera natywne okno bez widocznej przeglądarki.");
						}
						catch (Exception ex4)
						{
							error = ex4;
						}
						finally
						{
							stopForecastForm4.Close();
						}
					};
					timer2.Start();
					Call(mainForm, "TrainStop");
				}
				finally
				{
					if (timer2 != null)
					{
						((IDisposable)timer2).Dispose();
					}
				}
				if (error != null)
				{
					throw error;
				}
				TabControl tabControl = Field<TabControl>(mainForm, "topTabs");
				tabControl.SelectedIndex = 2;
				Call(mainForm, "RefreshDetails");
				DataGridView dataGridView2 = Field<Dictionary<string, DataGridView>>(mainForm, "detailGrids")["route"];
				dataGridView2.FirstDisplayedScrollingColumnIndex = 23;
				Ui.Capture(mainForm, Path.Combine(folder, "route-1.0.10.199.png"));
				Check(dataGridView2.Rows.Count == 3 && dataGridView2.Rows[1].Cells[26].Value.ToString() == "E186-139, EN57AKM", "Widoczna trasa prezentuje ręczną serię rzeczywistą obok planu RJ.");
				mainForm.Close();
			}
			train.Stopped = false;
			int saves = 0;
			bool stopped = false;
			using (StopForecastForm stopForecastForm = new StopForecastForm(train, (StopForecastOperation stop, DateTime a, DateTime d) =>
			{
				saves++;
				stopped = stop == StopForecastOperation.Stop;
				return Task.FromResult(result: true);
			}))
			{
				stopForecastForm.Show();
				Application.DoEvents();
				Check(saves == 0 && stopForecastForm.Arrival.Value == TrainPrediction.Arrival(train) && stopForecastForm.Departure.Value == TrainPrediction.Departure(train), "Otwarcie okna ustawia czasy i nie wykonuje zapisu.");
				Check(All(stopForecastForm).OfType<Button>().Count((Button x) => x.Text.EndsWith(" min.")) == 32, "Okno zawiera osobne 16 skrótów minut dla przyjazdu i odjazdu.");
				stopForecastForm.Arrival.Value = day.Date.AddHours(23.0).AddMinutes(58.0);
				All(stopForecastForm.Arrival).OfType<Button>().First((Button x) => x.Text == "5 min.").PerformClick();
				Check(stopForecastForm.Arrival.Value == day.Date.AddDays(1.0).AddMinutes(3.0), "Dodawanie minut poprawnie przechodzi przez północ.");
				All(stopForecastForm.Arrival).OfType<Button>().First((Button x) => x.Text == "Przepisz czas").PerformClick();
				Check(stopForecastForm.Departure.Value == stopForecastForm.Arrival.Value, "Przepisz czas kopiuje datę i godzinę.");
				stopForecastForm.Arrival.Value = day.AddMinutes(15.0);
				stopForecastForm.Departure.Value = day.AddMinutes(17.0);
				Ui.Capture(stopForecastForm, Path.Combine(folder, "stop-forecast-1.0.10.199.png"));
				stopForecastForm.StopButton.PerformClick();
				Application.DoEvents();
				Check(saves == 1 && stopped && stopForecastForm.DialogResult == DialogResult.OK, "Przycisk STÓJ wykonuje wyłącznie świadomie wybraną operację.");
			}
			using (StopForecastForm stopForecastForm2 = new StopForecastForm(train, (StopForecastOperation stop, DateTime a, DateTime d) =>
			{
				saves++;
				stopped = stop == StopForecastOperation.Stop;
				return Task.FromResult(result: true);
			}))
			{
				stopForecastForm2.Show();
				stopForecastForm2.Arrival.Value = day.AddMinutes(18.0);
				stopForecastForm2.Departure.Value = day.AddMinutes(17.0);
				stopForecastForm2.ForecastButton.PerformClick();
				Check(saves == 1 && !string.IsNullOrWhiteSpace(stopForecastForm2.Status.Text), "Błędna chronologia zatrzymuje prognozę przed wysłaniem.");
				stopForecastForm2.Arrival.Value = day.AddMinutes(15.0);
				stopForecastForm2.ForecastButton.PerformClick();
				Application.DoEvents();
				Check(saves == 2 && !stopped, "Przycisk PROGNOZA ma odrębną operację od STÓJ.");
			}
			TaskCompletionSource<bool> pending = new TaskCompletionSource<bool>();
			using (StopForecastForm stopForecastForm3 = new StopForecastForm(train, (StopForecastOperation stop, DateTime a, DateTime d) => pending.Task))
			{
				stopForecastForm3.Show();
				stopForecastForm3.StopButton.PerformClick();
				Check(stopForecastForm3.Busy && !stopForecastForm3.StopButton.Enabled && !stopForecastForm3.ForecastButton.Enabled && !stopForecastForm3.CancelOperation.Enabled, "Trwająca operacja blokuje podwójne kliknięcie i przyciski.");
				stopForecastForm3.Close();
				Check(!stopForecastForm3.IsDisposed, "Okna nie można zamknąć w trakcie zapisu.");
				pending.SetResult(result: true);
				Application.DoEvents();
			}
			Check(!Directory.Exists(Path.Combine(folder, "memory")), "Nowe funkcje nie uruchamiają trwałego zapisu sesji.");
			return true;
			IL_013e:
			Check((byte)ok != 0, "Parametry EU07 obejmują długość przeliczoną z mm na m, osie i prędkość.");
			VehicleSpec vehicleSpec2 = VehicleCatalog.Find("EN57");
			Check(vehicleSpec2 != null && !(vehicleSpec2.Length != (decimal?)63.97m) && vehicleSpec2.Gross == (decimal?)123m, "EN57 ma parametry konkretnego wpisu arkusza: 63,97 m i 123 t.");
			Check(VehicleCatalog.Choices.Contains("36WEd") && VehicleCatalog.Choices.Contains("E186") && VehicleCatalog.Find("nieznany") == null, "Lista obejmuje jednostki i lokomotywy; brak dopasowania nie tworzy fikcyjnych parametrów.");
			Check(VehicleCatalog.Find("181") == null && VehicleCatalog.Find("EN57AKMw1") != null, "Niejednoznaczny kod SEPE nie wybiera losowego wariantu.");
			if (File.Exists(Path.Combine(folder, "getAllTimetables.json")))
			{
				List<SimRailTrain> list8 = SimRailProvider.Parse<List<SimRailTrain>>(File.ReadAllBytes(Path.Combine(folder, "getEDRTimetables.json")));
				ConsistPlan.TryLoad(list8, CancellationToken.None, () => File.ReadAllBytes(Path.Combine(folder, "getAllTimetables.json")));
				Check(list8.Count > 1000 && list8.All((SimRailTrain x) => ConsistPlan.Project(x) != null), "Pełne odpowiedzi publicznego API: ponad 1000 rozkładów deserializuje się i łączy bez utraty parametrów.");
			}
			simRailTrain3 = Source();
			PlannedTrain item = Plan(simRailTrain3);
			List<SimRailTrain> list9 = new List<SimRailTrain>();
			list9.Add(simRailTrain3);
			ConsistPlan.Attach(list9, new List<PlannedTrain> { item });
			trainConsist = ConsistPlan.Project(simRailTrain3);
			if (trainConsist != null && trainConsist.PlannedLength == (decimal?)128m)
			{
				length = trainConsist.Length;
				if (length.GetValueOrDefault() == 128m && length.HasValue && !(trainConsist.PlannedGross != (decimal?)290m))
				{
					ok2 = ((trainConsist.Gross == (decimal?)290m) ? 1 : 0);
					goto IL_03ad;
				}
			}
			ok2 = 0;
			goto IL_03ad;
		}
		catch (Exception ex3)
		{
			log.Add(ex3.ToString());
			return false;
		}
		finally
		{
			File.WriteAllLines(Path.Combine(folder, "wyniki-operacje47.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

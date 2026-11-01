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

internal static class Stop167Tests
{
	private sealed class Feed : ISimulatorFeed
	{
		public SimRailSnapshot Value;

		public int Reads;

		public string ProviderId => "fixture167";

		public Task<List<SimRailServer>> ServersAsync(CancellationToken token)
		{
			throw new NotSupportedException("Test nie używa sieci.");
		}

		public Task<SimRailSnapshot> SnapshotAsync(string server, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			Reads++;
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

	private static void Reject(Action action, string text)
	{
		bool ok = false;
		try
		{
			action();
		}
		catch (InvalidOperationException)
		{
			ok = true;
		}
		Check(ok, text);
	}

	private static object Field(object instance, string name)
	{
		return instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		LocalPersistence.Enabled = false;
		try
		{
			Store store = new Store(Path.Combine(folder, "local"));
			store.Load();
			Engine engine = new Engine(store)
			{
				User = store.State.Users.First()
			};
			engine.BeginDuty(engine.Now.AddMinutes(-1.0));
			string id = engine.State.Trains.First().Id;
			DateTime now = engine.Now;
			DateTime dateTime = now.AddMinutes(5.0);
			engine.SetLocalForecast(id, StopForecastOperation.Stop, now, dateTime);
			Train train = engine.State.Trains.First((Train x) => x.Id == id);
			Check(train.Stopped && !train.Forecast.HasValue && !train.ArrivalForecast.HasValue, "Sam STÓJ nie wprowadza prognozy przyjazdu ani odjazdu.");
			using (MainForm mainForm = new MainForm(engine))
			{
				mainForm.Show();
				Application.DoEvents();
				mainForm.SelectTrain(id);
				DataGridView dataGridView = (DataGridView)Field(mainForm, "trains");
				ContextMenuStrip contextMenuStrip = dataGridView.ContextMenuStrip;
				typeof(ToolStripDropDown).GetMethod("OnOpening", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(contextMenuStrip, new object[1]
				{
					new CancelEventArgs()
				});
				Check(contextMenuStrip.Items.Cast<ToolStripItem>().Any((ToolStripItem x) => x.Text.StartsWith("Odwołaj pociąg STÓJ")), "Menu pociągu oferuje odwołanie wprowadzonego STÓJ.");
				int calls = 0;
				using (StopForecastForm stopForecastForm = new StopForecastForm(train, (StopForecastOperation operation, DateTime a, DateTime d) =>
				{
					calls++;
					Check(operation == StopForecastOperation.CancelStop, "Przycisk odwołania wysyła odrębną operację CancelStop.");
					engine.SetLocalForecast(id, operation, a, d);
					return Task.FromResult(result: true);
				}))
				{
					stopForecastForm.Show(mainForm);
					Application.DoEvents();
					Check(calls == 0 && stopForecastForm.StopButton.Text.StartsWith("Odwołaj postój"), "Otwarcie okna z postojem pokazuje odwołanie i nie wykonuje zapisu.");
					Ui.Capture(stopForecastForm, Path.Combine(folder, "odwolaj-stoj.png"));
					stopForecastForm.StopButton.PerformClick();
					Application.DoEvents();
					Check(calls == 1 && stopForecastForm.DialogResult == DialogResult.OK, "Jedno kliknięcie odwołuje STÓJ i zamyka poprawnie okno.");
				}
				train = engine.State.Trains.First((Train x) => x.Id == id);
				Check(!train.Stopped && !train.Forecast.HasValue && !train.ArrivalForecast.HasValue, "Odwołanie STÓJ nie tworzy prognozy ani godzin rzeczywistych.");
				mainForm.Close();
			}
			engine.SetLocalForecast(id, StopForecastOperation.Forecast, now, dateTime);
			engine.SetLocalForecast(id, StopForecastOperation.Stop, now.AddHours(1.0), dateTime.AddHours(1.0));
			engine.SetLocalForecast(id, StopForecastOperation.CancelStop, now.AddHours(2.0), dateTime.AddHours(2.0));
			train = engine.State.Trains.First((Train x) => x.Id == id);
			Check(!train.Stopped && SepeWriteRequest.SameMinute(train.ArrivalForecast, now) && SepeWriteRequest.SameMinute(train.Forecast, dateTime), "Wprowadzenie i odwołanie postoju zachowuje istniejącą prognozę.");
			SimRailPoint simRailPoint = new SimRailPoint();
			simRailPoint.pointId = "60";
			simRailPoint.nameForPerson = "B";
			simRailPoint.indexOfPoint = 1;
			simRailPoint.displayedTrainNumber = "2704";
			simRailPoint.arrivalTime = "2026-10-22 09:10:00";
			simRailPoint.departureTime = "2026-10-22 09:12:00";
			simRailPoint.actualArrivalTime = "2026-10-22 09:10:00";
			simRailPoint.actualDepartureTime = "2026-10-22 09:12:00";
			simRailPoint.isStoped = true;
			SimRailPoint item = simRailPoint;
			SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
			simRailSnapshot.ServerCode = "test167";
			simRailSnapshot.Trains = new List<SimRailTrain>
			{
				new SimRailTrain
				{
					trainNoLocal = "2704",
					startStation = "A",
					endStation = "C",
					timetable = new List<SimRailPoint> { item }
				}
			};
			SimRailSnapshot simRailSnapshot2 = simRailSnapshot;
			Feed feed = new Feed();
			feed.Value = simRailSnapshot2;
			Feed feed2 = feed;
			SimRailSession session = new SimRailSession(feed2, simRailSnapshot2, "60");
			session.SetAccessLink("https://api.simrail.eu:8093/?serverCode=test167&stationId=60&key=TEST_ONLY_167");
			Train train2 = new Train();
			train2.Id = "fixture167";
			train2.Number = "2704";
			train2.ExternalTrainNo = "2704";
			train2.ExternalPointIndex = 1;
			train2.Origin = "A";
			train2.Destination = "C";
			train2.PlanArrival = new DateTime(2026, 10, 22, 9, 10, 0);
			train2.PlanDeparture = new DateTime(2026, 10, 22, 9, 12, 0);
			train2.Stopped = true;
			Train train3 = train2;
			StopForecastRequest cancel = StopForecastRequest.From(train3, session, StopForecastOperation.CancelStop, now, dateTime);
			cancel.Validate(session);
			Check(!cancel.Confirmed(simRailSnapshot2), "Wprowadzony postój nie jest potwierdzeniem jego odwołania.");
			Check(!StopForecastRequest.Read(cancel, session, null, CancellationToken.None, 0).GetAwaiter().GetResult() && feed2.Reads == 3, "Brak zmiany API kończy się błędem po trzech odczytach; test nie ponawia zapisu.");
			SimRailTrain simRailTrain = Json.Clone(simRailSnapshot2.Trains[0]);
			simRailTrain.timetable[0].isStoped = false;
			SimRailSnapshot simRailSnapshot3 = new SimRailSnapshot();
			simRailSnapshot3.ServerCode = "test167";
			simRailSnapshot3.Trains = new List<SimRailTrain> { simRailTrain };
			SimRailSnapshot snapshot = simRailSnapshot3;
			Check(cancel.Confirmed(snapshot), "Odwołanie potwierdza wyłącznie isStoped=false właściwego przejazdu.");
			SimRailTrain simRailTrain2 = Json.Clone(simRailTrain);
			simRailTrain2.timetable[0].pointId = "61";
			Check(!cancel.Confirmed(new SimRailSnapshot
			{
				ServerCode = "test167",
				Trains = new List<SimRailTrain> { simRailTrain2 }
			}), "Odwołanie na innym posterunku nie potwierdza operacji.");
			simRailTrain2 = Json.Clone(simRailTrain);
			simRailTrain2.timetable[0].arrivalTime = "2026-10-23 09:10:00";
			Check(!cancel.Confirmed(new SimRailSnapshot
			{
				ServerCode = "test167",
				Trains = new List<SimRailTrain> { simRailTrain2 }
			}), "Odwołanie dla innej doby nie potwierdza operacji.");
			Check(!cancel.Confirmed(new SimRailSnapshot
			{
				ServerCode = "other",
				Trains = new List<SimRailTrain> { simRailTrain }
			}), "Inny serwer nie potwierdza odwołania.");
			session.Accept(snapshot);
			Reject(() =>
			{
				cancel.Validate(session);
			}, "Zmiana stanu od otwarcia okna blokuje ponowne przełączenie STÓJ.");
			train3.Stopped = false;
			StopForecastRequest stopForecastRequest = StopForecastRequest.From(train3, session, StopForecastOperation.Stop, train3.PlanArrival, train3.PlanDeparture);
			stopForecastRequest.Validate(session);
			Check(!stopForecastRequest.Confirmed(snapshot) && stopForecastRequest.Confirmed(simRailSnapshot2), "Sam STÓJ wymaga isStoped=true oraz zgodnych zapisanych godzin.");
			File.WriteAllText(Path.Combine(folder, "stop.js"), SepeTimeTransfer.StopRequest(stopForecastRequest.Identity), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			File.WriteAllText(Path.Combine(folder, "cancel-stop.js"), SepeTimeTransfer.StopRequest(cancel.Identity, stopped: false), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			Completion(stopForecastRequest);
			Check(!Directory.Exists(store.DirectoryPath), "Testy operacji nie zapisują sesji ani klucza do bazy.");
			return true;
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			return false;
		}
		finally
		{
			File.WriteAllLines(Path.Combine(folder, "wyniki-stop167.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}

	private static void Completion(StopForecastRequest request)
	{
		Check(StopForecastRequest.IsForecastQuestion("Nastąpiła próba wprowadzenia godziny późniejszej niż obecna. Czy chcesz wprowadzić prognozę?") && !StopForecastRequest.IsForecastQuestion("Inne ostrzeżenie"), "Rozpoznawane jest konkretne pytanie o przyszłą godzinę; inne komunikaty nie są zatwierdzane.");
		int progress = 0;
		int replies = 0;
		int prompts = 0;
		Func<string, Task<string>> script = (string code) =>
		{
			if (code.Contains(".forecastReply("))
			{
				replies++;
				return Task.FromResult(Json.Text("accepted"));
			}
			progress++;
			return Task.FromResult(Json.Text((progress == 1) ? "forecast-warning: Nastąpiła próba wprowadzenia godziny późniejszej niż obecna. Czy chcesz wprowadzić prognozę?" : "closed"));
		};
		SepeOperationCompletion.Wait(request, script, () => Task.FromResult(result: true), (string text) =>
		{
			prompts++;
			return Task.FromResult(result: true);
		}).GetAwaiter().GetResult();
		Check(progress == 2 && replies == 1 && prompts == 1, "STÓJ czeka na świadomą zgodę na prognozę, a następnie na zakończenie formularza.");
		progress = (replies = (prompts = 0));
		bool flag = false;
		script = (string code) =>
		{
			if (code.Contains(".forecastReply("))
			{
				replies++;
				Check(code.Contains(",false)"), "Odmowa w EDR wybiera Anuluj w pytaniu SEPE.");
				return Task.FromResult(Json.Text("declined"));
			}
			progress++;
			return Task.FromResult(Json.Text("forecast-warning: Nastąpiła próba wprowadzenia godziny późniejszej niż obecna. Czy chcesz wprowadzić prognozę?"));
		};
		try
		{
			SepeOperationCompletion.Wait(request, script, () => Task.FromResult(result: true), (string text) =>
			{
				prompts++;
				return Task.FromResult(result: false);
			}).GetAwaiter().GetResult();
		}
		catch (ForecastDeclinedException)
		{
			flag = true;
		}
		Check(flag && progress == 1 && replies == 1 && prompts == 1, "Odmowa nie przechodzi do ponownego zapisu ani oczekiwania na nieistniejące potwierdzenie API.");
		request.Operation = StopForecastOperation.Forecast;
		progress = (replies = (prompts = 0));
		script = (string code) =>
		{
			if (code.Contains(".forecastReply("))
			{
				replies++;
				return Task.FromResult(Json.Text("accepted"));
			}
			progress++;
			return Task.FromResult(Json.Text((progress == 1) ? "forecast-warning: Nastąpiła próba wprowadzenia godziny późniejszej niż obecna. Czy chcesz wprowadzić prognozę?" : "closed"));
		};
		SepeOperationCompletion.Wait(request, script, () => Task.FromResult(result: true), (string text) =>
		{
			prompts++;
			return Task.FromResult(result: false);
		}).GetAwaiter().GetResult();
		Check(replies == 1 && prompts == 0, "Odrębny przycisk PROGNOZA stanowi świadomy wybór prognozy, bez dodatkowego pytania w EDR.");
		progress = 0;
		Reject(() =>
		{
			SepeOperationCompletion.Wait(request, (string code) =>
			{
				progress++;
				return Task.FromResult(Json.Text("waiting"));
			}, () => Task.FromResult(result: true), null).GetAwaiter().GetResult();
		}, "Formularz bez zakończenia daje błąd zamiast pozornego sukcesu.");
		Check(progress == 45, "Oczekiwanie jest ograniczone i nie zawiera ponownego kliknięcia zapisu.");
	}
}

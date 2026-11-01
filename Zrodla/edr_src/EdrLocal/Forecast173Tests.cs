using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EdrLocal;

public static class Forecast173Tests
{
	private sealed class Feed : ISimulatorFeed
	{
		public SimRailSnapshot Value;

		public int Reads;

		public string ProviderId => "fixture173";

		public Task<List<SimRailServer>> ServersAsync(CancellationToken token)
		{
			throw new NotSupportedException("Bez sieci.");
		}

		public Task<SimRailSnapshot> SnapshotAsync(string server, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			Reads++;
			return Task.FromResult(Value);
		}
	}

	private static readonly List<string> log = new List<string>();

	private static readonly DateTime arrival = new DateTime(2026, 10, 23, 22, 47, 0);

	private static readonly DateTime departure = new DateTime(2026, 10, 23, 23, 5, 0);

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

	private static SimRailSnapshot Snapshot()
	{
		SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
		simRailSnapshot.ServerCode = "test173";
		simRailSnapshot.GameTime = departure.AddMinutes(-5.0);
		simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
		simRailSnapshot.Trains = new List<SimRailTrain>
		{
			new SimRailTrain
			{
				trainNoLocal = "23937",
				startStation = "A",
				endStation = "C",
				timetable = new List<SimRailPoint>
				{
					new SimRailPoint
					{
						pointId = "60",
						nameForPerson = "B",
						indexOfPoint = 1,
						displayedTrainNumber = "23937",
						line = 8,
						track = 4,
						arrivalTime = "2026-10-23 22:47:00",
						departureTime = "2026-10-23 22:59:00",
						actualArrivalTime = "2026-10-23 22:47:00",
						actualDepartureTime = "2026-10-23 22:59:00",
						isConfirmed = true,
						confirmedBy = 1,
						isActive = true
					}
				}
			}
		};
		simRailSnapshot.DispatcherNames = new HashSet<string> { "B" };
		return simRailSnapshot;
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		LocalPersistence.Enabled = false;
		try
		{
			SimRailSnapshot simRailSnapshot = Snapshot();
			Feed feed = new Feed();
			feed.Value = simRailSnapshot;
			Feed feed2 = feed;
			SimRailSession session = new SimRailSession(feed2, simRailSnapshot, "60");
			session.SetAccessLink("https://api.simrail.eu:8093/?serverCode=test173&stationId=60&key=TEST_ONLY_173");
			Check(SepeBackgroundWriter.OperationUrl(session, StopForecastOperation.Stop) == session.AccessUrl && SepeBackgroundWriter.OperationUrl(session, StopForecastOperation.CancelStop) == session.AccessUrl, "STÓJ i odwołanie używają aktualnego linku z uprawnieniami zamiast linku publicznego.");
			Check(SepeBackgroundWriter.OperationUrl(session, StopForecastOperation.Forecast) == session.PublicUrl, "Osobna prognoza zachowuje formularz publiczny, bez przekazywania klucza zatwierdzającego rzeczywiste godziny.");
			Train train = new Train
			{
				Id = "fixture173",
				Number = "23937",
				ExternalTrainNo = "23937",
				ExternalPointIndex = 1,
				SourceProvider = "SimRail",
				ExternalConfirmed = true,
				Origin = "A",
				Destination = "C",
				PlanArrival = arrival,
				PlanDeparture = arrival.AddMinutes(12.0),
				Arrival = arrival,
				Departure = arrival.AddMinutes(12.0)
			};
			StopForecastRequest request = StopForecastRequest.From(train, session, StopForecastOperation.Forecast, arrival, departure);
			request.Validate(session);
			Check(ok: true, "Potwierdzony pociąg nie blokuje jawnego wprowadzenia prognozy.");
			Check(!request.Confirmed(simRailSnapshot), "Samo zatwierdzenie pociągu ze starą godziną odjazdu nie potwierdza nowej prognozy.");
			SimRailPoint simRailPoint = simRailSnapshot.Trains[0].timetable[0];
			simRailPoint.actualDepartureTime = "2026-10-23 23:05:00";
			Check(request.Confirmed(simRailSnapshot) && simRailPoint.isConfirmed, "Zgodna prognoza odjazdu jest potwierdzana również przy zachowanym isConfirmed=true.");
			Check(simRailPoint.actualArrivalTime == "2026-10-23 22:47:00", "Prognoza odjazdu zachowuje istniejącą godzinę przyjazdu.");
			simRailPoint.isConfirmed = false;
			Check(request.Confirmed(simRailSnapshot), "Prognoza działa również dla niezatwierdzonego pociągu.");
			simRailPoint.isConfirmed = true;
			SimRailSnapshot simRailSnapshot2 = Json.Clone(simRailSnapshot);
			simRailSnapshot2.Trains[0].timetable[0].actualArrivalTime = "2026-10-23 22:48:00";
			Check(!request.Confirmed(simRailSnapshot2), "Niezgodny przyjazd nadal blokuje potwierdzenie prognozy.");
			simRailSnapshot2 = Json.Clone(simRailSnapshot);
			simRailSnapshot2.Trains[0].timetable[0].pointId = "61";
			Check(!request.Confirmed(simRailSnapshot2), "Prognoza na innym posterunku nie potwierdza operacji.");
			simRailSnapshot2 = Json.Clone(simRailSnapshot);
			simRailSnapshot2.Trains[0].timetable[0].indexOfPoint = 2;
			Check(!request.Confirmed(simRailSnapshot2), "Inny przejazd przez ten sam posterunek nie potwierdza prognozy.");
			simRailSnapshot2 = Json.Clone(simRailSnapshot);
			simRailSnapshot2.Trains[0].timetable[0].departureTime = "2026-10-24 22:59:00";
			Check(!request.Confirmed(simRailSnapshot2), "Inna doba planowa nadal nie potwierdza operacji.");
			simRailSnapshot2 = Json.Clone(simRailSnapshot);
			simRailSnapshot2.ServerCode = "other";
			Check(!request.Confirmed(simRailSnapshot2), "Inny serwer nie potwierdza prognozy.");
			simRailSnapshot2 = Json.Clone(simRailSnapshot);
			simRailSnapshot2.Trains[0].endStation = "X";
			Check(!request.Confirmed(simRailSnapshot2), "Inna relacja nie potwierdza prognozy.");
			request.Arrival = departure.AddMinutes(1.0);
			bool ok = false;
			try
			{
				request.Validate(session);
			}
			catch (TrainTimeException)
			{
				ok = true;
			}
			Check(ok, "Prognoza zatwierdzonego pociągu nadal wymaga poprawnej chronologii.");
			request.Arrival = arrival;
			SimRailSession noLink = new SimRailSession(feed2, simRailSnapshot, "60");
			Reject(() =>
			{
				request.Validate(noLink);
			}, "Prognoza zatwierdzonego pociągu nadal wymaga linku z uprawnieniami.");
			Reject(() =>
			{
				SepeBackgroundWriter.OperationUrl(noLink, StopForecastOperation.Stop);
			}, "STÓJ bez aktualnego linku nie otwiera zastępczo publicznego formularza zapisu.");
			int scripts = 0;
			SepeOperationCompletion.Wait(request, (string code) =>
			{
				scripts++;
				return Task.FromResult(Json.Text("closed"));
			}, () => Task.FromResult(result: true), null).GetAwaiter().GetResult();
			Check(scripts == 1, "Zamknięcie formularza kończy wyłącznie oczekiwanie na interfejs.");
			simRailPoint.actualDepartureTime = "2026-10-23 22:59:00";
			feed2.Reads = 0;
			Check(!StopForecastRequest.Read(request, session, null, CancellationToken.None, 0).GetAwaiter().GetResult() && feed2.Reads == 3, "Zamknięcie formularza bez zgodnej nowej godziny nie daje pozornego sukcesu.");
			Check(request.LastConfirmationError.Contains("godzin"), "Brak prognozy ma czytelną przyczynę w raporcie potwierdzenia.");
			simRailPoint.actualDepartureTime = "2026-10-23 23:05:00";
			feed2.Reads = 0;
			Check(StopForecastRequest.Read(request, session, null, CancellationToken.None, 0).GetAwaiter().GetResult() && feed2.Reads == 1, "Zgodny odczyt prognozy zatwierdzonego pociągu kończy sprawdzanie API.");
			StopForecastRequest stopForecastRequest = StopForecastRequest.From(train, session, StopForecastOperation.Stop, arrival, departure);
			stopForecastRequest.Validate(session);
			Check(!stopForecastRequest.Confirmed(simRailSnapshot), "Prognoza bez isStoped nie potwierdza STÓJ.");
			Check(!StopForecastRequest.Read(stopForecastRequest, session, null, CancellationToken.None, 0).GetAwaiter().GetResult() && stopForecastRequest.LastConfirmationError.Contains("brak Pociąg STÓJ"), "Niewprowadzony STÓJ jest zgłaszany wprost, bez utożsamiania go z samą prognozą.");
			simRailPoint.isStoped = true;
			Check(stopForecastRequest.Confirmed(simRailSnapshot), "STÓJ zatwierdzonego pociągu wymaga flagi postoju i zgodnych godzin.");
			train.Stopped = true;
			StopForecastRequest stopForecastRequest2 = StopForecastRequest.From(train, session, StopForecastOperation.CancelStop, arrival, departure);
			stopForecastRequest2.Validate(session);
			simRailPoint.isStoped = false;
			Check(stopForecastRequest2.Confirmed(simRailSnapshot), "Odwołanie STÓJ zatwierdzonego pociągu nadal jest odrębną operacją.");
			int calls = 0;
			train.Stopped = false;
			using (StopForecastForm stopForecastForm = new StopForecastForm(train, (StopForecastOperation operation, DateTime a, DateTime d) =>
			{
				calls++;
				Check(operation == StopForecastOperation.Forecast, "Przycisk PROGNOZA zatwierdzonego pociągu wybiera operację prognozy.");
				StopForecastRequest stopForecastRequest3 = StopForecastRequest.From(train, session, operation, a, d);
				stopForecastRequest3.Validate(session);
				return Task.FromResult(result: true);
			}))
			{
				stopForecastForm.Show();
				Application.DoEvents();
				stopForecastForm.Arrival.Value = arrival;
				stopForecastForm.Departure.Value = departure;
				Check(stopForecastForm.ForecastButton.Enabled && calls == 0, "Otwarcie okna zatwierdzonego pociągu udostępnia prognozę bez samoczynnej operacji.");
				Ui.Capture(stopForecastForm, Path.Combine(folder, "prognoza-zatwierdzonego.png"));
				stopForecastForm.ForecastButton.PerformClick();
				Application.DoEvents();
				Check(calls == 1 && stopForecastForm.DialogResult == DialogResult.OK, "Jedno kliknięcie PROGNOZA zatwierdzonego pociągu poprawnie kończy okno.");
			}
			DateTime? dateTime = train.Arrival;
			DateTime dateTime2 = arrival;
			Check(dateTime.HasValue && dateTime.GetValueOrDefault() == dateTime2 && train.Departure == arrival.AddMinutes(12.0) && !train.ExternalManualTimes, "Obsługa prognozy sama nie dopisuje godzin rzeczywistych ani zleceń ich wysyłki.");
			File.WriteAllText(Path.Combine(folder, "stop.js"), SepeTimeTransfer.StopRequest(stopForecastRequest.Identity, stopped: true, arrival, departure, departure), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			File.WriteAllText(Path.Combine(folder, "forecast.js"), SepeTimeTransfer.SubmitRequest(request.Identity, arrival, departure, departure), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			log.Add("ALL OFFLINE CONFIRMED TRAIN FORECAST TESTS PASSED");
			return true;
		}
		catch (Exception ex2)
		{
			log.Add(ex2.ToString());
			return false;
		}
		finally
		{
			File.WriteAllLines(Path.Combine(folder, "wyniki-prognoza173.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

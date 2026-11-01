using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace EdrLocal;

public static class Session21Tests
{
	private static readonly List<string> log = new List<string>();

	private static void Check(bool value, string label)
	{
		if (!value)
		{
			throw new Exception("FAIL: " + label);
		}
		log.Add("PASS: " + label);
	}

	private static void Reject(Action action, string label)
	{
		bool value = false;
		try
		{
			action();
		}
		catch
		{
			value = true;
		}
		Check(value, label);
	}

	private static IEnumerable<Control> All(Control owner)
	{
		yield return owner;
		foreach (Control c in owner.Controls)
		{
			foreach (Control item in All(c))
			{
				yield return item;
			}
		}
	}

	private static void Flag(object owner, string name, bool value)
	{
		owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
	}

	private static SimRailTrain Source(string number, int minute)
	{
		SimRailTrain simRailTrain = new SimRailTrain();
		simRailTrain.trainNoLocal = number;
		simRailTrain.startStation = "A";
		simRailTrain.endStation = "B";
		simRailTrain.timetable = new List<SimRailPoint>
		{
			new SimRailPoint
			{
				pointId = "59",
				nameForPerson = "Rokiciny",
				indexOfPoint = 0,
				line = 1,
				mileage = 114.176
			},
			new SimRailPoint
			{
				pointId = "60",
				nameForPerson = "Baby",
				indexOfPoint = 1,
				line = 1,
				mileage = 129.944,
				track = 1,
				platform = "II",
				displayedTrainNumber = number,
				trainType = "MPE",
				isActive = true,
				arrivalTime = "2026-04-21 19:" + minute + ":00",
				departureTime = "2026-04-21 19:" + (minute + 2) + ":00",
				actualArrivalTime = "2026-04-21 19:" + minute + ":00",
				actualDepartureTime = "2026-04-21 19:" + (minute + 2) + ":00"
			},
			new SimRailPoint
			{
				pointId = "61",
				nameForPerson = "Piotrków Trybunalski",
				indexOfPoint = 2,
				line = 1,
				mileage = 144.242
			}
		};
		return simRailTrain;
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		try
		{
			SimRailTrain simRailTrain = Source("124", 24);
			SimRailTrain item = Source("126", 30);
			SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
			simRailSnapshot.ServerCode = "test";
			simRailSnapshot.Trains = new List<SimRailTrain> { simRailTrain, item };
			simRailSnapshot.DispatcherNames = new HashSet<string> { "Baby", "Rokiciny", "Piotrków Trybunalski" };
			simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
			simRailSnapshot.GameTime = new DateTime(2026, 4, 21, 19, 40, 0);
			SimRailSnapshot simRailSnapshot2 = simRailSnapshot;
			SimRailSession session = new SimRailSession(new SimRailProvider(), simRailSnapshot2, "60");
			Station station = session.SuggestStation();
			StationSettings.Normalize(station);
			station.SimulatorReviewed = true;
			station.JournalTabs = new List<JournalTabDefinition>
			{
				new JournalTabDefinition
				{
					LeftId = station.Sections[1].Id,
					RightId = station.Sections[0].Id
				}
			};
			Store store = new Store(Path.Combine(folder, "data"));
			store.Load();
			store.Commit((AppState s) =>
			{
				s.Stations = new List<Station> { station };
				s.Trains.Clear();
				s.Entries.Clear();
				s.Duties.Clear();
				s.Layout = "Dół";
				s.UiZoom = 1.25f;
			});
			Engine engine = new Engine(store)
			{
				SimRail = session,
				User = store.State.Users.First((LocalUser x) => x.Id == "DEMO")
			};
			engine.ApplySimRailSnapshot(simRailSnapshot2);
			engine.BeginDuty(engine.Now.AddMinutes(-2.0));
			Train train = engine.State.Trains.First((Train x) => x.Number == "124");
			Train nextTrain = engine.State.Trains.First((Train x) => x.Number == "126");
			List<SepeWriteRequest> writes = new List<SepeWriteRequest>();
			engine.SimRailTimeChanged += (SepeWriteRequest r) =>
			{
				writes.Add(r);
			};
			JournalEntry entry = engine.RegisterScheduledTrain(train.Id, null);
			entry.Arrived = new DateTime(2026, 4, 21, 19, 25, 0);
			engine.SaveJournal(entry);
			SepeWriteRequest request = writes.Single();
			string text = Path.Combine(folder, "scripts");
			Directory.CreateDirectory(text);
			File.WriteAllText(Path.Combine(text, "select.js"), SepeTimeTransfer.Select(request));
			File.WriteAllText(Path.Combine(text, "identity.js"), SepeTimeTransfer.Identity(request));
			for (int num = 0; num < 6; num++)
			{
				File.WriteAllText(Path.Combine(text, "field" + num + ".js"), SepeTimeTransfer.Field(entry.Arrived, entry.Arrived.Value.AddMinutes(2.0), simRailSnapshot2.GameTime, num));
			}
			File.WriteAllText(Path.Combine(text, "submit.js"), SepeTimeTransfer.SubmitRequest(request, entry.Arrived, entry.Arrived.Value.AddMinutes(2.0), simRailSnapshot2.GameTime));
			Check(request.TrainId == train.Id && request.ExternalNumber == "124" && request.PointIndex == 1, "Operacja przechowuje identyfikator pociągu i konkretnego punktu API.");
			Check(request.PlanArrival == train.PlanArrival && request.PlanDeparture == train.PlanDeparture, "Operacja zachowuje planową datę i obie godziny przejazdu.");
			train.Arrival = new DateTime(2026, 4, 21, 19, 27, 0);
			Check(request.Arrival.Value.Minute == 25, "Późniejsza edycja modelu nie zmienia oczekującego zlecenia.");
			request.Validate(session);
			Check(value: true, "Aktualna tożsamość przejazdu przechodzi kontrolę przed wysyłką.");
			SimRailPoint simRailPoint = simRailTrain.timetable[1];
			simRailPoint.displayedTrainNumber = "126";
			Reject(() =>
			{
				request.Validate(session);
			}, "Zmiana numeru punktu blokuje wysyłanie do następnego pociągu.");
			simRailPoint.displayedTrainNumber = "124";
			simRailPoint.arrivalTime = "2026-04-22 19:24:00";
			Reject(() =>
			{
				request.Validate(session);
			}, "Ten sam numer następnego dnia nie przejmuje zapisu.");
			simRailPoint.arrivalTime = "2026-04-21 19:24:00";
			simRailPoint.indexOfPoint = 3;
			Reject(() =>
			{
				request.Validate(session);
			}, "Inny indeks przejazdu przez posterunek jest odrzucany.");
			simRailPoint.indexOfPoint = 1;
			simRailTrain.timetable.Add(simRailPoint);
			Reject(() =>
			{
				request.Validate(session);
			}, "Powielone punkty API nie są rozstrzygane pierwszym trafieniem.");
			simRailTrain.timetable.RemoveAt(simRailTrain.timetable.Count - 1);
			simRailPoint.actualArrivalTime = "2026-04-21 19:25:30";
			simRailPoint.isConfirmed = false;
			Check(!request.Confirmed(simRailSnapshot2, "60"), "Pasująca godzina bez isConfirmed nie potwierdza zapisu.");
			simRailPoint.isConfirmed = true;
			Check(request.Confirmed(simRailSnapshot2, "60"), "API potwierdza właściwy przejazd i minutę.");
			Check(!request.Confirmed(simRailSnapshot2, "61"), "Potwierdzenie innego posterunku nie kończy operacji.");
			simRailPoint.isConfirmed = false;
			simRailPoint.actualArrivalTime = "2026-04-21 19:24:00";
			using (ReferenceForm referenceForm = new ReferenceForm())
			{
				referenceForm.Show();
				Application.DoEvents();
				int count = Application.OpenForms.Count;
				using (new SepeBackgroundWriter(referenceForm, session))
				{
					WebView2 val = referenceForm.Controls.OfType<WebView2>().Single();
					Check(!((Control)(object)val).Visible && !((Control)(object)val).TabStop && Application.OpenForms.Count == count, "Transport w tle nie tworzy widocznego okna ani elementu do zaznaczenia klawiaturą.");
				}
				Check(!referenceForm.Controls.OfType<WebView2>().Any(), "Zamknięcie transportu usuwa ukrytą kontrolkę przeglądarki.");
				referenceForm.Close();
			}
			MainForm main = new MainForm(engine);
			try
			{
				main.Show();
				Application.DoEvents();
				main.SelectTrain(nextTrain.Id);
				Check(request.TrainId != main.SelectedTrain.Id && request.Number == "124", "Wybranie następnego pociągu w EDR nie zmienia adresata zapisanej operacji.");
				string text2 = Json.Text(engine.State);
				Check(!main.FinishSession(ExitSessionChoice.Cancel) && Json.Text(engine.State) == text2, "Anuluj pozostawia sesję i dzienniki bez zmian.");
				main.SelectTrain(train.Id);
				Timer timer = new Timer
				{
					Interval = 100
				};
				try
				{
					timer.Tick += (object s, EventArgs e) =>
					{
						Editor editor = Application.OpenForms.OfType<Editor>().FirstOrDefault();
						if (editor != null)
						{
							timer.Stop();
							List<OperationTimePanel> list = All(editor).OfType<OperationTimePanel>().ToList();
							list[0].Value = new DateTime(2026, 4, 21, 19, 25, 0);
							list[1].Value = new DateTime(2026, 4, 21, 19, 28, 0);
							main.SelectTrain(nextTrain.Id);
							Ui.Capture(editor, Path.Combine(folder, "godziny.png"));
							editor.Buttons.Controls.OfType<Button>().First((Button x) => x.Text == "Zapisz").PerformClick();
						}
					};
					timer.Start();
					typeof(MainForm).GetMethod("TrainTimes", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(main, null);
				}
				finally
				{
					if (timer != null)
					{
						((IDisposable)timer).Dispose();
					}
				}
				Check(writes.Last().TrainId == train.Id && writes.Last().Departure.Value.Minute == 28, "Lokalne okno godzin zachowuje adresata mimo zmiany zaznaczenia w tle.");
				Check(!Application.OpenForms.OfType<SepeBrowserForm>().Any(), "Wprowadzanie godzin SimRail nie otwiera okna SEPE.");
				int count2 = writes.Count;
				Train train2 = engine.State.Trains.First((Train x) => x.Id == train.Id);
				engine.SetTrainTimes(train2.Id, train2.Arrival, train2.Departure);
				Check(writes.Count == count2, "Ponowne zatwierdzenie tych samych godzin nie wysyła drugiego zapisu.");
				Check(main.FinishSession(ExitSessionChoice.Save), "Zapisz pozwala zakończyć sesję.");
				main.Close();
			}
			finally
			{
				if (main != null)
				{
					((IDisposable)main).Dispose();
				}
			}
			Store store2 = new Store(store.DirectoryPath);
			store2.Load();
			Check(store2.State.Entries.Any((JournalEntry x) => x.Id == entry.Id) && store2.State.Duties.Any((Duty x) => !x.End.HasValue), "Zapisz zachowuje dzienniki i otwarty dyżur po ponownym wczytaniu.");
			Check(store2.State.Trains.First((Train x) => x.Id == request.TrainId).ExternalManualTimes, "Niepotwierdzone godziny pozostają do ponowienia po uruchomieniu.");
			using (ExitSessionForm exitSessionForm = new ExitSessionForm(canReset: true, pending: true))
			{
				exitSessionForm.Show();
				Application.DoEvents();
				Ui.Capture(exitSessionForm, Path.Combine(folder, "wyjscie.png"));
				Check(All(exitSessionForm).OfType<Button>().Count((Button x) => new string[3] { "Zapisz", "Nie zapisuj", "Anuluj" }.Contains(x.Text)) == 3, "Przy wyjściu dostępne są trzy konkretne decyzje.");
				All(exitSessionForm).OfType<Button>().Single((Button x) => x.Text == "Nie zapisuj").PerformClick();
				Check(exitSessionForm.Choice == ExitSessionChoice.Discard, "Nie zapisuj pomija szkice zamiast resetowania dzienników.");
			}
			using (ExitSessionForm exitSessionForm2 = new ExitSessionForm(canReset: false, pending: false))
			{
				Check(All(exitSessionForm2).OfType<Button>().Single((Button x) => x.Text == "Nie zapisuj").Enabled, "Obserwator może zamknąć okno bez zapisu szkiców, bez resetowania dzienników.");
				exitSessionForm2.Show();
				exitSessionForm2.Close();
				Check(exitSessionForm2.Choice == ExitSessionChoice.Cancel, "Zamknięcie pytania krzyżykiem oznacza Anuluj.");
			}
			string text3 = Json.Text(engine.Station);
			float uiZoom = engine.State.UiZoom;
			string layout = engine.State.Layout;
			string text4 = Json.Text(engine.State);
			MainForm main2 = new MainForm(engine);
			try
			{
				main2.Show();
				Application.DoEvents();
				Flag(main2, "sendingHours", value: true);
				Reject(() =>
				{
					main2.FinishSession(ExitSessionChoice.Reset);
				}, "Reset podczas trwającego wysyłania jest blokowany.");
				Check(Json.Text(engine.State) == text4, "Zablokowany reset nie zmienia bazy.");
				Flag(main2, "sendingHours", value: false);
				Check(main2.FinishSession(ExitSessionChoice.Reset), "Jawny reset wykonuje czyszczenie i pozwala zamknąć aplikację.");
				main2.Close();
			}
			finally
			{
				if (main2 != null)
				{
					((IDisposable)main2).Dispose();
				}
			}
			Check(engine.State.Entries.Count == 0 && engine.State.Duties.Count == 0 && engine.State.Services.Count == 0, "Reset usuwa lokalne wpisy, dyżur i usługi.");
			Check(Json.Text(engine.Station) == text3 && engine.State.UiZoom == uiZoom && engine.State.Layout == layout, "Reset zachowuje konfigurację posterunku, kolejność dzienników i skalę.");
			Check(engine.State.Trains.Count == 2 && engine.State.Trains.All((Train x) => !x.ExternalManualTimes), "Po resecie pozostaje źródłowy rozkład bez lokalnych korekt.");
			Check(!simRailPoint.isConfirmed && simRailPoint.actualArrivalTime == "2026-04-21 19:24:00", "Reset lokalny nie zmienia danych źródłowych SEPE.");
			string[] files = Directory.GetFiles(store.DirectoryPath, "przed-resetem-*.json");
			Check(files.Length == 1 && Json.Read<AppState>(File.ReadAllBytes(files[0])).Entries.Any((JournalEntry x) => x.Id == entry.Id), "Niezależna kopia sprzed resetu zawiera poprzednie dzienniki.");
			store2 = new Store(store.DirectoryPath);
			store2.Load();
			Check(store2.State.Duties.Count == 0 && store2.State.Entries.Count == 0 && store2.State.Stations[0].SimulatorReviewed, "Ponowne uruchomienie wczytuje pustą sesję i gotową konfigurację.");
			engine.User = engine.State.Users.First((LocalUser x) => x.Role == "Obserwator");
			Reject(() =>
			{
				engine.ResetSimRailJournals();
			}, "Kontrola uprawnień resetu działa również poza oknem dialogowym.");
			log.Add("ALL SESSION AND IDENTITY TESTS PASSED");
			return true;
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			return false;
		}
		finally
		{
			File.WriteAllLines(Path.Combine(folder, "wyniki-sesja-tozsamosc.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

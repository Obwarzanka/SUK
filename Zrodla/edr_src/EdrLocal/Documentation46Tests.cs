using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace EdrLocal;

public static class Documentation46Tests
{
	private static readonly List<string> log = new List<string>();

	private static readonly DateTime day = new DateTime(2026, 10, 22);

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

	private static void Call(object obj, string name, params object[] args)
	{
		try
		{
			obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args);
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

	private static DateTime At(int minute)
	{
		return day.AddHours(9.0).AddMinutes(minute);
	}

	private static SimRailSnapshot Snapshot()
	{
		SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
		simRailSnapshot.ServerCode = "doc46";
		simRailSnapshot.GameTime = At(30);
		simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
		simRailSnapshot.DispatcherNames = new HashSet<string> { "Łódź Marysin", "Łódź Widzew", "Zgierz Północ" };
		simRailSnapshot.Trains = new List<SimRailTrain>
		{
			new SimRailTrain
			{
				trainNoLocal = "2704",
				trainName = "ROJ",
				startStation = "Łódź Marysin",
				endStation = "Zgierz Północ",
				timetable = new List<SimRailPoint>
				{
					new SimRailPoint
					{
						pointId = "11",
						nameForPerson = "Łódź Marysin",
						indexOfPoint = 0,
						line = 17
					},
					new SimRailPoint
					{
						pointId = "60",
						nameForPerson = "Łódź Widzew",
						indexOfPoint = 1,
						line = 17,
						track = 4,
						displayedTrainNumber = "2704",
						trainType = "ROJ",
						arrivalTime = "2026-10-22 09:10:00",
						departureTime = "2026-10-22 09:12:00",
						actualArrivalTime = "2026-10-22 09:10:00",
						actualDepartureTime = "2026-10-22 09:12:00",
						isActive = true,
						isConfirmed = true
					},
					new SimRailPoint
					{
						pointId = "12",
						nameForPerson = "Zgierz Północ",
						indexOfPoint = 2,
						line = 17
					}
				}
			}
		};
		return simRailSnapshot;
	}

	private static Engine Make(string path)
	{
		Store store = new Store(path);
		store.Load();
		Station station = new Station
		{
			Id = "DOC46",
			Name = "Łódź Widzew",
			PostType = "STACJA",
			SimRailPointId = "60",
			Tracks = new List<string> { "4" },
			Checks = new List<string>(),
			Sections = new List<Section>
			{
				new Section
				{
					Id = "semi",
					Name = "Łódź Marysin",
					Line = "17",
					Tracks = 1,
					NormalIncomingTrack = 1,
					ArrivalEven = true,
					Basis = "Jednoodstępowa (półsamoczynna) blokada liniowa"
				},
				new Section
				{
					Id = "sbl",
					Name = "Głinnik",
					Line = "17",
					Tracks = 1,
					NormalIncomingTrack = 1,
					Basis = "Wieloodstępowa (samoczynna) blokada liniowa"
				},
				new Section
				{
					Id = "double",
					Name = "Zgierz Północ",
					Line = "17",
					Tracks = 2,
					NormalIncomingTrack = 1,
					Basis = "Wieloodstępowa (samoczynna) blokada liniowa"
				}
			},
			JournalTabs = new List<JournalTabDefinition>
			{
				new JournalTabDefinition
				{
					LeftId = "semi",
					RightId = "sbl"
				},
				new JournalTabDefinition
				{
					LeftId = "double"
				}
			}
		};
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
		engine.SimRail = new SimRailSession(null, Snapshot(), "60");
		Engine engine2 = engine;
		engine2.ApplySimRailSnapshot(engine2.SimRail.Snapshot);
		engine2.BeginDuty(At(0));
		return engine2;
	}

	private static JournalEntry Entry(Engine engine, string section, string direction, string number)
	{
		JournalEntry journalEntry = engine.Note(section, At(28), "", "Pociąg");
		journalEntry.Direction = direction;
		journalEntry.Number = number;
		journalEntry.Track = "4";
		return journalEntry;
	}

	private static JournalEntry Saved(Engine engine, JournalEntry e)
	{
		return Json.Clone(engine.State.Entries.Single((JournalEntry x) => x.Id == e.Id));
	}

	private static DataGridViewRow Row(JournalPane pane, JournalEntry e)
	{
		return pane.Grid.Rows.Cast<DataGridViewRow>().Single((DataGridViewRow x) => ((JournalEntry)x.Tag).Id == e.Id);
	}

	private static void Type(JournalPane pane, JournalEntry entry, string column, string text)
	{
		pane.Grid.CurrentCell = Row(pane, entry).Cells[column];
		Check(pane.Grid.BeginEdit(selectAll: true), "Komórka " + column + " pozwala na ręczną edycję.");
		((TextBox)pane.Grid.EditingControl).Text = text;
		Check(pane.Grid.EndEdit() && pane.CommitPending(), "Edycja " + column + " zatwierdza się bez błędu.");
	}

	private static void Dialog(Action invoke, Action<Editor> action)
	{
		Exception error = null;
		Timer timer = new Timer
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
					action(editor);
				}
				catch (Exception ex)
				{
					error = ex;
				}
				finally
				{
					editor.Close();
				}
			};
			timer.Start();
			invoke();
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
	}

	private static TelephoneData Closure(DateTime effective)
	{
		TelephoneData telephoneData = new TelephoneData();
		telephoneData.TemplateId = "21";
		telephoneData.Track = 2;
		telephoneData.SentAt = At(29);
		telephoneData.EffectiveAt = effective;
		telephoneData.Receiver = "Test";
		telephoneData.Reason = "Test zamknięcia";
		telephoneData.ClosureMode = "";
		return telephoneData;
	}

	public static bool Run(string folder)
	{
		Ui.Testing = true;
		Directory.CreateDirectory(folder);
		bool enabled = LocalPersistence.Enabled;
		LocalPersistence.Enabled = false;
		try
		{
			Check(TrafficBasis.Choices.Length == 3 && TrafficBasis.Choices.Distinct().Count() == 3 && !TrafficBasis.Choices.Contains("Blokada samoczynna"), "Lista zawiera trzy różne podstawy bez zdublowanej samoczynnej.");
			Check(TrafficBasis.Display("Blokada samoczynna") == "Wieloodstępowa (samoczynna) blokada liniowa" && Engine.AutomaticBasis("Blokada samoczynna"), "Dawna krótka nazwa SBL nadal jest rozpoznawana i ma pełną nazwę w edytorze.");
			Check(!Engine.AutomaticBasis("Jednoodstępowa (półsamoczynna) blokada liniowa") && !Engine.AutomaticBasis("Blokada półsamoczynna") && TrafficBasis.IsSemiAutomatic("Jednoodstępowa blokada liniowa"), "Blokada półsamoczynna nie jest mylona z samoczynną.");
			Engine engine = Make(Path.Combine(folder, "no-data"));
			JournalEntry incoming = Entry(engine, "semi", "Z", "2704");
			JournalTimeFields journalTimeFields = engine.DocumentationFields(incoming);
			Check(journalTimeFields.Left && journalTimeFields.Arrived && journalTimeFields.Permission, "Półsamoczynna na jednotorze przewiduje drogę wolną, odjazd sąsiada i własny przyjazd.");
			JournalEntry journalEntry = Entry(engine, "semi", "DO", "2705");
			journalTimeFields = engine.DocumentationFields(journalEntry);
			Check(journalTimeFields.Left && journalTimeFields.Arrived && journalTimeFields.Permission, "Wyprawienie na półsamoczynnej przewiduje własny odjazd i przyjazd do sąsiada.");
			JournalEntry journalEntry2 = Entry(engine, "sbl", "Z", "2706");
			journalTimeFields = engine.DocumentationFields(journalEntry2);
			Check(journalTimeFields.Permission && !journalTimeFields.Left && journalTimeFields.Arrived, "Na jednotorze z SBL droga wolna jest przewidziana niezależnie od podstawy ruchu.");
			JournalEntry entry = Entry(engine, "double", "Z", "2708");
			journalTimeFields = engine.DocumentationFields(entry);
			Check(!journalTimeFields.Permission && !journalTimeFields.Left && journalTimeFields.Arrived, "Zwykły dwutor z SBL zachowuje dokumentowanie własnego przyjazdu.");
			Section section = engine.Station.Sections.Single((Section x) => x.Id == "double");
			foreach (LineTrackSetting lineTrack in section.LineTracks)
			{
				lineTrack.Basis = "Jednoodstępowa (półsamoczynna) blokada liniowa";
			}
			journalTimeFields = engine.DocumentationFields(entry);
			Check(journalTimeFields.Left && journalTimeFields.Arrived && !journalTimeFields.Permission, "Półsamoczynna na dwutorze przewiduje oba czasy bez automatycznego żądania drogi.");
			foreach (LineTrackSetting lineTrack2 in section.LineTracks)
			{
				lineTrack2.Basis = "Wieloodstępowa (samoczynna) blokada liniowa";
			}
			section.LineTracks[0].Basis = "Jednoodstępowa (półsamoczynna) blokada liniowa";
			section.LineTracks[0].BasisOut = "Wieloodstępowa (samoczynna) blokada liniowa";
			Check(engine.DocumentationFields(entry).Left && !engine.DocumentationFields(Entry(engine, "double", "DO", "2709")).Arrived, "Reguły rozróżniają podstawę ruchu Z i DO na tym samym torze.");
			section.LineTracks[0].Basis = "Wieloodstępowa (samoczynna) blokada liniowa";
			JournalEntry journalEntry3 = engine.SaveTelephone("double", null, Closure(At(45)));
			Check(!engine.DocumentationFields(entry).Permission, "Zamknięcie z przyszłą godziną nie zmienia pól przed wejściem w życie.");
			engine.SaveTelephone("double", journalEntry3, Closure(At(20)));
			Check(engine.DocumentationFields(entry).Permission, "Po zamknięciu drugiego toru droga wolna jest przewidziana na czynnym torze z SBL.");
			engine.Cancel(journalEntry3.Id, "Test odwołania");
			Check(!engine.DocumentationFields(entry).Permission, "Wykreślone zamknięcie nie zmienia wymagań.");
			journalEntry3 = engine.SaveTelephone("double", null, Closure(At(20)));
			engine.SaveTelephone("double", null, new TelephoneData
			{
				TemplateId = "22",
				Track = 2,
				SentAt = At(29),
				EffectiveAt = At(21),
				Receiver = "Test"
			});
			Check(!engine.DocumentationFields(entry).Permission, "Otwarcie drugiego toru przywraca zwykłe pola dla dwutoru.");
			JournalEntry journalEntry4 = engine.Note("double", At(27), "Test", "Telefonogram");
			journalEntry4.Effect = "Telefoniczne";
			engine.SaveJournal(journalEntry4);
			journalTimeFields = engine.DocumentationFields(entry);
			Check(journalTimeFields.Permission && journalTimeFields.Left && journalTimeFields.Arrived, "Telefoniczne zapowiadanie zachowuje pełną dokumentację czasów.");
			engine.Cancel(journalEntry4.Id, "Test");
			JournalEntry journalEntry5 = Entry(engine, "semi", "DO", "Rob1");
			journalEntry5.ReturnFromLine = true;
			journalTimeFields = engine.DocumentationFields(journalEntry5);
			Check(journalTimeFields.Left && journalTimeFields.Permission && !journalTimeFields.Arrived, "Wyjazd z powrotem ze szlaku nie wymaga fikcyjnego przyjazdu do sąsiada.");
			JournalEntry journalEntry6 = Entry(engine, "semi", "Z", "Rob1");
			journalEntry6.ReturnOfId = "root";
			journalTimeFields = engine.DocumentationFields(journalEntry6);
			Check(!journalTimeFields.Left && !journalTimeFields.Permission && journalTimeFields.Arrived, "Wiersz powrotu pozostawia tylko własny przyjazd.");
			List<SepeWriteRequest> list = new List<SepeWriteRequest>();
			engine.SimRailTimeChanged += list.Add;
			incoming.TrainId = engine.State.Trains.Single().Id;
			incoming.Arrived = At(10);
			engine.SaveJournal(incoming);
			engine.ApplySimRailSnapshot(engine.SimRail.Snapshot);
			list.Clear();
			incoming = Saved(engine, incoming);
			incoming.Permission = At(4);
			incoming.Left = At(5);
			engine.SaveJournal(incoming);
			Check(list.Count == 0 && engine.State.Trains.Single().Arrival == At(10) && engine.State.Trains.Single().Departure == At(12), "Odjazd sąsiada i droga wolna nie zmieniają godzin własnego posterunku ani nie wysyłają zdarzenia SEPE.");
			Check(!engine.State.Trains.Single().ExternalManualArrival && !engine.State.Trains.Single().ExternalManualDeparture && !engine.State.Trains.Single().ExternalManualTimes, "Dokumentacja nie oznacza potwierdzonych godzin jako oczekujących na wysłanie.");
			engine.ApplySimRailSnapshot(engine.SimRail.Snapshot);
			Check(Saved(engine, incoming).Left == At(5) && Saved(engine, incoming).Permission == At(4), "Odświeżenie API zachowuje ręczną dokumentację sąsiada i drogi wolnej.");
			using (JournalReport journalReport = new JournalReport(engine, new List<JournalEntry> { Saved(engine, incoming) }, telephone: false, history: false, engine.Station.Sections[0], At(0), At(30)))
			{
				ReportRow reportRow = journalReport.Rows.Single((ReportRow x) => !x.Day);
				Check(reportRow.Cells.Single((ReportCell x) => x.Column == 5).Text == "09" && reportRow.Cells.Single((ReportCell x) => x.Column == 6).Text == "05" && reportRow.Cells.Single((ReportCell x) => x.Column == 4).Text == "04", "Wyciąg z dziennika uwzględnia dokumentowane czasy.");
			}
			incoming = Saved(engine, incoming);
			incoming.Left = null;
			engine.SaveJournal(incoming);
			Check(list.Count == 0 && !engine.State.Trains.Single().ExternalManualTimes, "Usunięcie godziny sąsiada również nie powoduje wysłania do SEPE.");
			incoming = Saved(engine, incoming);
			incoming.Arrived = At(11);
			engine.SaveJournal(incoming);
			Check(list.Count == 1 && list[0].ArrivalChanged && !list[0].DepartureChanged && list[0].Arrival == At(11), "Zmiana własnego przyjazdu nadal wywołuje tylko zapis przyjazdu do SEPE.");
			engine.SimRail.Snapshot.Trains[0].timetable[1].actualArrivalTime = "2026-10-22 09:11:00";
			engine.ApplySimRailSnapshot(engine.SimRail.Snapshot);
			list.Clear();
			journalEntry.TrainId = engine.State.Trains.Single().Id;
			journalEntry.Left = At(12);
			engine.SaveJournal(journalEntry);
			engine.ApplySimRailSnapshot(engine.SimRail.Snapshot);
			list.Clear();
			journalEntry = Saved(engine, journalEntry);
			journalEntry.Arrived = At(16);
			journalEntry.Permission = At(11);
			engine.SaveJournal(journalEntry);
			Check(list.Count == 0 && engine.State.Trains.Single().Departure == At(12) && !engine.State.Trains.Single().ExternalManualTimes, "Przyjazd do sąsiedniego posterunku nie zmienia własnego odjazdu ani stanu SEPE.");
			journalEntry = Saved(engine, journalEntry);
			journalEntry.Left = At(13);
			engine.SaveJournal(journalEntry);
			Check(list.Count == 1 && list[0].DepartureChanged && !list[0].ArrivalChanged && list[0].Departure == At(13), "Zmiana własnego odjazdu nadal wywołuje tylko zapis odjazdu do SEPE.");
			engine.SimRail.Snapshot.Trains[0].timetable[1].actualDepartureTime = "2026-10-22 09:13:00";
			engine.ApplySimRailSnapshot(engine.SimRail.Snapshot);
			list.Clear();
			engine.Station.Options.WriteSepe = false;
			SimRailPoint simRailPoint = engine.SimRail.Snapshot.Trains[0].timetable[1];
			simRailPoint.actualArrivalTime = "2026-10-22 09:09:00";
			simRailPoint.actualDepartureTime = "2026-10-22 09:10:00";
			engine.ApplySimRailSnapshot(engine.SimRail.Snapshot);
			incoming = Saved(engine, incoming);
			incoming.Left = At(8);
			engine.SaveJournal(incoming);
			int ok;
			if (list.Count == 0)
			{
				DateTime? arrival = engine.State.Trains.Single().Arrival;
				DateTime dateTime = At(9);
				if (arrival.HasValue && arrival.GetValueOrDefault() == dateTime && engine.State.Trains.Single().Departure == At(10))
				{
					ok = ((!engine.State.Trains.Single().ExternalManualTimes) ? 1 : 0);
					goto IL_0f04;
				}
			}
			ok = 0;
			goto IL_0f04;
			IL_0f04:
			Check((byte)ok != 0, "Dokumentacja przy starszym własnym czasie w wierszu nie nadpisuje nowszych danych API.");
			JournalEntry journalEntry7 = Saved(engine, incoming);
			journalEntry7.Arrived = At(20);
			bool ok2 = false;
			try
			{
				engine.SaveJournal(journalEntry7);
			}
			catch (TrainTimeException)
			{
				ok2 = true;
			}
			Check(ok2, "Rzeczywista zmiana własnego czasu nadal podlega walidacji względem rozkładu.");
			simRailPoint.actualArrivalTime = "2026-10-22 09:11:00";
			simRailPoint.actualDepartureTime = "2026-10-22 09:13:00";
			engine.ApplySimRailSnapshot(engine.SimRail.Snapshot);
			simRailPoint.actualArrivalTime = "2026-10-22 09:11:30";
			engine.ApplySimRailSnapshot(engine.SimRail.Snapshot);
			engine.Store.Commit((AppState state) =>
			{
				state.Entries.Single((JournalEntry x) => x.Id == incoming.Id).Arrived = At(11).AddSeconds(30.0);
			});
			JournalEntry journalEntry8 = Entry(engine, "semi", "Z", "2710");
			engine.SaveJournal(journalEntry8);
			engine.SaveJournal(journalEntry2);
			MainForm main = new MainForm(engine);
			try
			{
				main.Show();
				main.Size = new Size(1600, 850);
				Application.DoEvents();
				List<JournalPane> source = Field<List<JournalPane>>(main, "panes");
				JournalPane pane = source.Single((JournalPane x) => x.SectionId == "semi");
				JournalPane pane2 = source.Single((JournalPane x) => x.SectionId == "sbl");
				Check(Convert.ToString(Row(pane, journalEntry8).Cells["left"].FormattedValue) != "---" && Row(pane, journalEntry8).Cells["left"].Style.BackColor == EdrPalette.Required, "Puste pole odjazdu sąsiada na półsamoczynnej jest wyróżnione zamiast kresek.");
				Check(Row(pane, journalEntry8).Cells["permission"].Style.BackColor == EdrPalette.Required && Row(pane2, journalEntry2).Cells["permission"].Style.BackColor == EdrPalette.Required, "Droga wolna jest wyróżniona na jednotorze półsamoczynnym i z SBL.");
				Check(Convert.ToString(Row(pane2, journalEntry2).Cells["left"].FormattedValue) == "---", "SBL zachowuje kreski w niewymaganym czasie odjazdu sąsiada.");
				Type(pane, incoming, "left", "09:06");
				DateTime? left = Saved(engine, incoming).Left;
				DateTime dateTime2 = At(6);
				Check(left.HasValue && left.GetValueOrDefault() == dateTime2 && list.Count == 0 && !engine.State.Trains.Single().ExternalManualTimes, "Edycja odjazdu sąsiada w tabeli pozostaje wyłącznie dokumentacją.");
				DateTime? arrival2 = engine.State.Trains.Single().Arrival;
				DateTime dateTime3 = At(11).AddSeconds(30.0);
				Check(arrival2.HasValue && arrival2.GetValueOrDefault() == dateTime3 && list.Count == 0 && !engine.State.Trains.Single().ExternalManualTimes, "Minutowa precyzja tabeli nie zmienia sekund czasu z API podczas edycji dokumentacji.");
				Type(pane, incoming, "permission", "09:04");
				Check(Saved(engine, incoming).Permission == At(4) && list.Count == 0, "Edycja drogi wolnej w tabeli nie wysyła godzin do SEPE.");
				Ui.Capture(main, Path.Combine(folder, "dokumentacja-1.0.10.199.png"));
				Dialog(() =>
				{
					main.EditJournal(Saved(engine, incoming), null, "Z", "semi");
				}, (Editor d) =>
				{
					ReferenceTimeInput referenceTimeInput = All(d).OfType<ReferenceTimeInput>().Single((ReferenceTimeInput x) => x.Top == 176);
					referenceTimeInput.Active.Checked = true;
					referenceTimeInput.Input.Text = "09:07";
					d.Buttons.Controls.OfType<Button>().Single((Button x) => x.Text == "Zapisz").PerformClick();
				});
				DateTime? left2 = Saved(engine, incoming).Left;
				DateTime dateTime4 = At(7);
				Check(left2.HasValue && left2.GetValueOrDefault() == dateTime4 && list.Count == 0 && !engine.State.Trains.Single().ExternalManualTimes, "Formularz również zapisuje odjazd sąsiada bez zmiany godzin SEPE.");
				main.Close();
			}
			finally
			{
				if (main != null)
				{
					((IDisposable)main).Dispose();
				}
			}
			section = engine.Station.Sections.Single((Section x) => x.Id == "double");
			foreach (LineTrackSetting lineTrack3 in section.LineTracks)
			{
				string basis = (lineTrack3.BasisOut = "Blokada samoczynna");
				lineTrack3.Basis = basis;
			}
			StationConfigurationForm config = new StationConfigurationForm(engine);
			try
			{
				config.Show();
				Application.DoEvents();
				Field<ConfigurationGrid>(config, "sectionGrid").CurrentCell = Field<ConfigurationGrid>(config, "sectionGrid").Rows[2].Cells[0];
				Dialog(() =>
				{
					Call(config, "EditSection", false);
				}, (Editor d) =>
				{
					List<ComboBox> list2 = (from x in All(d).OfType<ComboBox>()
						where x.Items.Cast<object>().Any((object y) => Convert.ToString(y) == "Wieloodstępowa (samoczynna) blokada liniowa")
						select x).ToList();
					Check(list2.Count == 4 && list2.All((ComboBox x) => x.Items.Count == 3 && !x.Items.Cast<object>().Any((object y) => Convert.ToString(y) == "Blokada samoczynna")), "Rzeczywisty edytor nie dubluje SBL w żadnym z czterech pól podstawy.");
					Check(list2.All((ComboBox x) => x.Text == "Wieloodstępowa (samoczynna) blokada liniowa"), "Stara krótka nazwa jest wyświetlana jako pełna nazwa w edytorze.");
					Ui.Capture(d, Path.Combine(folder, "podstawy-ruchu-1.0.10.199.png"));
				});
				config.Close();
			}
			finally
			{
				if (config != null)
				{
					((IDisposable)config).Dispose();
				}
			}
			Check(!Directory.Exists(Path.Combine(folder, "no-data")) && !LocalPersistence.Enabled, "Dokumentacja i nowe pola pozostają wyłącznie w pamięci sesji.");
			log.Add("ALL DOCUMENTATION 46 TESTS PASSED");
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
			File.WriteAllLines(Path.Combine(folder, "wyniki-dokumentacja46.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

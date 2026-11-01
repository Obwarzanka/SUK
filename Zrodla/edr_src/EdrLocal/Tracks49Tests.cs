using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace EdrLocal;

public static class Tracks49Tests
{
	private static readonly List<string> log = new List<string>();

	private static readonly DateTime day = new DateTime(2026, 10, 22);

	private static DateTime At(int h, int m)
	{
		return day.AddHours(h).AddMinutes(m);
	}

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

	private static T Field<T>(object obj, string name)
	{
		return (T)obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
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

	private static Section Section(bool incomingTwo)
	{
		Section section = new Section();
		section.Id = "sie";
		section.Name = "Sieradz";
		section.Line = "14";
		section.Tracks = 2;
		section.ArrivalEven = incomingTwo;
		section.NormalIncomingTrack = ((!incomingTwo) ? 1 : 2);
		section.RightTrackNumber = 2;
		section.Basis = "Jednoodstępowa (półsamoczynna) blokada liniowa";
		section.LineTracks = new List<LineTrackSetting>
		{
			new LineTrackSetting
			{
				Number = 1,
				NormalIncoming = !incomingTwo,
				Basis = "Jednoodstępowa (półsamoczynna) blokada liniowa",
				BasisOut = "Jednoodstępowa (półsamoczynna) blokada liniowa"
			},
			new LineTrackSetting
			{
				Number = 2,
				NormalIncoming = incomingTwo,
				Basis = "Jednoodstępowa (półsamoczynna) blokada liniowa",
				BasisOut = "Jednoodstępowa (półsamoczynna) blokada liniowa"
			}
		};
		return section;
	}

	private static SimRailSnapshot Snapshot()
	{
		SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
		simRailSnapshot.ServerCode = "test49";
		simRailSnapshot.GameTime = At(11, 45);
		simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
		simRailSnapshot.DispatcherNames = new HashSet<string> { "Sieradz", "Zduńska Wola", "Łask" };
		simRailSnapshot.Trains = new List<SimRailTrain>
		{
			new SimRailTrain
			{
				trainNoLocal = "7328",
				startStation = "Sieradz",
				endStation = "Łódź Fabryczna",
				trainName = "MOJ",
				timetable = new List<SimRailPoint>
				{
					new SimRailPoint
					{
						pointId = "10",
						nameForPerson = "Sieradz",
						indexOfPoint = 0,
						line = 14
					},
					new SimRailPoint
					{
						pointId = "60",
						nameForPerson = "Zduńska Wola",
						indexOfPoint = 1,
						line = 14,
						track = 2,
						displayedTrainNumber = "7328",
						trainType = "MOJ",
						isActive = true,
						arrivalTime = "2026-10-22 11:40:00",
						departureTime = "2026-10-22 11:41:00"
					},
					new SimRailPoint
					{
						pointId = "20",
						nameForPerson = "Łask",
						indexOfPoint = 2,
						line = 14
					}
				}
			}
		};
		return simRailSnapshot;
	}

	private static Engine Make(string folder)
	{
		Store store = new Store(folder);
		store.Load();
		Station station = new Station
		{
			Id = "ZW49",
			Name = "Zduńska Wola",
			PostType = "STACJA",
			SimRailPointId = "60",
			Tracks = new List<string> { "1", "2" },
			Checks = new List<string>(),
			Sections = new List<Section>
			{
				Section(incomingTwo: true),
				new Section
				{
					Id = "las",
					Name = "Łask",
					Line = "14",
					Tracks = 2,
					ArrivalEven = false,
					NormalIncomingTrack = 1,
					Basis = "Jednoodstępowa (półsamoczynna) blokada liniowa"
				}
			},
			Options = new StationOptions
			{
				WriteSepe = false
			},
			JournalTabs = new List<JournalTabDefinition>
			{
				new JournalTabDefinition
				{
					LeftId = "sie",
					RightId = "las"
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
		engine2.BeginDuty(At(11, 13));
		return engine2;
	}

	private static JournalEntry Entry(Engine e, string number, string direction, string text)
	{
		JournalEntry journalEntry = e.Note("sie", At(11, 32), text, "Pociąg");
		journalEntry.Number = number;
		journalEntry.Direction = direction;
		journalEntry.Track = "2";
		journalEntry.Left = At(11, 32);
		return journalEntry;
	}

	private static JournalEntry Saved(Engine e, JournalEntry row)
	{
		return Json.Clone(e.State.Entries.Single((JournalEntry x) => x.Id == row.Id));
	}

	private static RailSignal Signal(JournalPane pane, int number)
	{
		return Field<List<RailSignal>>(pane, "signals").Single((RailSignal x) => x.Number == number);
	}

	private static void Type(JournalPane pane, string id, string name, string text)
	{
		DataGridViewRow dataGridViewRow = pane.Grid.Rows.Cast<DataGridViewRow>().Single((DataGridViewRow x) => ((JournalEntry)x.Tag).Id == id);
		pane.Grid.CurrentCell = dataGridViewRow.Cells[name];
		if (!pane.Grid.IsCurrentCellInEditMode)
		{
			pane.Grid.BeginEdit(selectAll: true);
		}
		((TextBox)pane.Grid.EditingControl).Text = text;
		Check(pane.Grid.EndEdit() && pane.CommitPending(), "Zapis komórki " + name + " przez rzeczywisty edytor.");
	}

	private static void Dialog(MainForm main, JournalEntry row, Action<Editor> action)
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
			main.EditJournal(row, null, row.Direction, row.SectionId);
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

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		LocalPersistence.Enabled = false;
		try
		{
			Section sec = Section(incomingTwo: true);
			Check(JournalLineTracks.Resolve(sec, "Z", "") == 2 && JournalLineTracks.Resolve(sec, "DO", "") == 1, "Sieradz: bez uwag Z po torze 2, DO po torze 1.");
			Check(JournalLineTracks.Resolve(sec, "Z", "Po torze lewym") == 1 && JournalLineTracks.Resolve(sec, "DO", "Po torze lewym") == 2, "Tor lewy jest przeciwny do zasadniczego dla danego kierunku, a nie zawsze nr 2.");
			Section section = Section(incomingTwo: false);
			Check(JournalLineTracks.Resolve(section, "Z", "") == 1 && JournalLineTracks.Resolve(section, "DO", "") == 2 && JournalLineTracks.Resolve(section, "Z", "Po torze lewym") == 2 && JournalLineTracks.Resolve(section, "DO", "Po torze lewym") == 1, "Odwrotna konfiguracja poprawnie odwraca domyślne i lewe tory.");
			sec.NormalIncomingTrack = 1;
			sec.RightTrackNumber = 1;
			Check(JournalLineTracks.Resolve(sec, "Z", "") == 2 && JournalLineTracks.Resolve(sec, "Z", "Po torze lewym") == 1, "Wybór korzysta z zasadniczych kierunków torów, niezależnie od kolejności ikon i starego pola pomocniczego.");
			string[] array = new string[4] { "po lewym", "Po torze lewym", "PO   TORZE   LEWYM.", "Zgłoszono: po torze lewym, ostrożnie." };
			foreach (string text in array)
			{
				Check(JournalLineTracks.Resolve(sec, "Z", text) == 1 && JournalLineTracks.Resolve(sec, "DO", text) == 2, "Rozpoznawana fraza: " + text);
			}
			string[] array2 = new string[7] { "po torze nr [2]", "po torze numer [2]", "po zamkniętym torze nr [2]", "po zamkniętym torze numer [2]", "po torze zamkniętym nr [2]", "po torze zamkniętym numer [2]", "Po torze nr. 2" };
			foreach (string text2 in array2)
			{
				Check(JournalLineTracks.Resolve(sec, "Z", text2) == 2 && JournalLineTracks.Resolve(sec, "DO", text2) == 2, "Jawny numer toru zachowuje znaczenie w obu kierunkach: " + text2);
			}
			Check(JournalLineTracks.Resolve(sec, "Z", "Po torze nr 1") == 1 && JournalLineTracks.Resolve(sec, "DO", "Po torze nr 1") == 1, "Jawny tor nr 1 nie jest mylony z torem normalnym ani parzystością pociągu.");
			Reject(() =>
			{
				JournalLineTracks.Resolve(sec, "Z", "Po torze nr 3");
			}, "Numer nieistniejącego toru jest odrzucany.");
			Reject(() =>
			{
				JournalLineTracks.Resolve(sec, "Z", "Po torze nr 1 i po torze nr 2");
			}, "Dwa sprzeczne numery toru są odrzucane.");
			Reject(() =>
			{
				JournalLineTracks.Resolve(sec, "Z", "Po torze lewym, po torze nr 2");
			}, "Sprzeczny opis toru lewego i numeru toru jest odrzucany.");
			Check(JournalLineTracks.Resolve(sec, "Z", "Po torze lewym, po torze nr [1]") == 1, "Zgodny opis toru lewego z jego numerem jest akceptowany.");
			Section section2 = Section(incomingTwo: true);
			section2.Tracks = 1;
			section2.LineTracks = section2.LineTracks.Take(1).ToList();
			Check(JournalLineTracks.Resolve(section2, "Z", "Po torze lewym") == 1 && !JournalLineTracks.IsLeft(section2, "Z", 1), "Jednotor nie tworzy fikcyjnego drugiego toru.");
			Section ambiguous = Section(incomingTwo: true);
			ambiguous.LineTracks.ForEach((LineTrackSetting x) =>
			{
				x.NormalIncoming = true;
			});
			Reject(() =>
			{
				JournalLineTracks.Resolve(ambiguous, "Z", "");
			}, "Niejednoznaczne kierunki wymagają jawnego numeru zamiast losowego toru.");
			Check(JournalLineTracks.Resolve(ambiguous, "Z", "Po torze nr 2") == 2, "Jawny numer pozostaje dostępny także przy nietypowej konfiguracji kierunków.");
			Engine engine = Make(Path.Combine(folder, "memory"));
			List<SepeWriteRequest> list = new List<SepeWriteRequest>();
			engine.SimRailTimeChanged += list.Add;
			Train train = engine.State.Trains.Single();
			JournalEntry registered = engine.RegisterScheduledTrain(train.Id, "sie");
			Check(registered.LineTrack == 2 && Saved(engine, registered).LineTrack == 2, "Szybka rejestracja przyjazdu od razu wybiera tor szlakowy 2.");
			registered.Left = At(11, 32);
			engine.SaveJournal(registered);
			Check(engine.LineStatus("sie", 2).Contains("7328") && engine.LineStatus("sie", 1) == "WOLNY", "Przypadek 7328: odjazd z Sieradza zajmuje tor 2, tor 1 pozostaje wolny.");
			Check(list.Count == 0, "Godzina sąsiedniego posterunku i wybór toru nie wysyłają własnych godzin do SEPE.");
			using (MainForm mainForm = new MainForm(engine))
			{
				mainForm.Show();
				mainForm.Size = new Size(1500, 850);
				Application.DoEvents();
				JournalPane journalPane = Field<List<JournalPane>>(mainForm, "panes").Single((JournalPane x) => x.SectionId == "sie");
				journalPane.RefreshSignals();
				Check(!Signal(journalPane, 1).Incoming && Signal(journalPane, 2).Incoming && Signal(journalPane, 2).Status.Contains("7328") && Signal(journalPane, 2).MovementIncoming == true && !Signal(journalPane, 1).MovementIncoming.HasValue, "Nagłówek ma 1 ↑ wolny i 2 ↓ zajęty rzeczywistym przyjazdem.");
				Ui.Capture(mainForm, Path.Combine(folder, "sieradz-tor-normalny-1.0.10.199.png"));
				Type(journalPane, registered.Id, "notes", "Po torze lewym");
				Check(Saved(engine, registered).LineTrack == 1 && engine.LineStatus("sie", 1).Contains("7328") && engine.LineStatus("sie", 2) == "WOLNY", "Edycja uwag w tabeli przenosi zajętość na rzeczywisty tor lewy nr 1.");
				Check(!Signal(journalPane, 1).Incoming && Signal(journalPane, 1).MovementIncoming == true && !Signal(journalPane, 2).MovementIncoming.HasValue, "Niebieska strzałka przyjazdu na torze 1 nie odwraca zasadniczej szarej strzałki.");
				Check(list.Count == 0 && !engine.State.Trains.Single().ExternalManualTimes, "Sama zmiana uwag nie uruchamia zapisu SEPE ani oczekiwania godzin.");
				Ui.Capture(mainForm, Path.Combine(folder, "sieradz-tor-lewy-1.0.10.199.png"));
				Dialog(mainForm, Saved(engine, registered), (Editor form) =>
				{
					CheckBox checkBox = All(form).OfType<CheckBox>().Single((CheckBox x) => x.Text == "Po torze lewym");
					Check(checkBox.Checked && checkBox.ForeColor == Color.Red && !checkBox.AutoCheck, "Formularz rozpoznaje czerwony znacznik toru lewego nr 1 i nie pozwala ręcznie go przełączać.");
					TextBox textBox = All(form).OfType<TextBox>().Single((TextBox x) => x.Multiline);
					textBox.Text = "";
					Check(!checkBox.Checked && checkBox.ForeColor == Color.Black, "Usunięcie frazy w formularzu odznacza interpretację toru lewego.");
					textBox.Text = "Po torze nr [1]";
					Check(checkBox.Checked, "Jawny numer 1 również jest rozpoznawany jako lewy przy przyjeździe.");
					Ui.Capture(form, Path.Combine(folder, "formularz-toru-lewego-1.0.10.199.png"));
				});
				Type(journalPane, registered.Id, "notes", "");
				Check(Saved(engine, registered).LineTrack == 2 && engine.LineStatus("sie", 2).Contains("7328"), "Usunięcie uwag przywraca właściwy tor zasadniczy nr 2.");
				Type(journalPane, registered.Id, "left", "11:33");
				Check(Saved(engine, registered).LineTrack == 2 && ((JournalEntry)journalPane.Grid.Rows.Cast<DataGridViewRow>().Single((DataGridViewRow x) => ((JournalEntry)x.Tag).Id == registered.Id).Tag).LineTrack == 2, "Edycja samej godziny zachowuje tor 2 również w modelu wiersza tabeli.");
				JournalEntry journalEntry = Entry(engine, "16108", "DO", "");
				engine.SaveJournal(journalEntry);
				mainForm.RefreshData();
				Check(Saved(engine, journalEntry).LineTrack == 1 && Signal(journalPane, 1).MovementIncoming == false && Signal(journalPane, 2).MovementIncoming == true, "Równoczesny wyjazd zajmuje tor 1, niezależnie od przyjazdu po torze 2.");
				JournalEntry journalEntry2 = Saved(engine, registered);
				journalEntry2.Arrived = At(11, 40);
				engine.SaveJournal(journalEntry2);
				mainForm.RefreshData();
				Check(engine.LineStatus("sie", 2) == "WOLNY" && engine.LineStatus("sie", 1).Contains("16108"), "Potwierdzenie przyjazdu zwalnia wyłącznie tor 2.");
				Check(list.Count == 1 && list[0].ArrivalChanged && !list[0].DepartureChanged, "Zmiana własnego przyjazdu zachowuje dotychczasowe zdarzenie SEPE.");
				Dialog(mainForm, Saved(engine, journalEntry), (Editor form) =>
				{
					TextBox textBox = All(form).OfType<TextBox>().Single((TextBox x) => x.Multiline);
					textBox.Text = "Po torze lewym";
					CheckBox checkBox = All(form).OfType<CheckBox>().Single((CheckBox x) => x.Text == "Po torze lewym");
					Check(checkBox.Checked && checkBox.ForeColor == Color.Red, "Przy wyprawieniu ta sama fraza rozpoznaje tor lewy nr 2.");
					form.Buttons.Controls.OfType<Button>().Single((Button x) => x.Text == "Zapisz").PerformClick();
				});
				Check(Saved(engine, journalEntry).LineTrack == 2 && engine.LineStatus("sie", 1) == "WOLNY" && engine.LineStatus("sie", 2).Contains("16108"), "Zapis z formularza przenosi wyjazd na tor 2 tak samo jak edycja tabeli.");
				mainForm.Close();
			}
			engine = Make(Path.Combine(folder, "return-memory"));
			JournalEntry root = Entry(engine, "Rob1", "DO", "Po zamkniętym torze nr [2] do km 42 i z powrotem");
			root.ReturnFromLine = true;
			engine.SaveJournal(root);
			root = Saved(engine, root);
			JournalEntry journalEntry3 = engine.State.Entries.Single((JournalEntry x) => x.ReturnOfId == root.Id);
			Check(root.LineTrack == 2 && journalEntry3.LineTrack == 2, "Wyjazd i powrót ze szlaku korzystają z tego samego jawnie wskazanego toru.");
			root.Text = "Po zamkniętym torze nr 1 do km 42 i z powrotem";
			engine.SaveJournal(root);
			journalEntry3 = Json.Clone(engine.State.Entries.Single((JournalEntry x) => x.ReturnOfId == root.Id));
			Check(Saved(engine, root).LineTrack == 1 && journalEntry3.LineTrack == 1, "Poprawienie toru w wyjeździe roboczym aktualizuje również powiązany powrót.");
			journalEntry3.Arrived = At(11, 40);
			journalEntry3.LineTrack = 2;
			engine.SaveJournal(journalEntry3);
			Check(Saved(engine, journalEntry3).LineTrack == 1 && engine.LineStatus("sie", 1) == "WOLNY", "Powrót nie przelicza toru z nowego kierunku i zwalnia właściwy tor.");
			JournalEntry journalEntry4 = Entry(engine, "Known", "Z", "");
			journalEntry4.LineTrack = 1;
			engine.SaveJournal(journalEntry4);
			journalEntry4 = Saved(engine, journalEntry4);
			journalEntry4.Text = "Uwaga niezwiązana z torem";
			journalEntry4.Left = At(11, 31);
			engine.SaveJournal(journalEntry4);
			Check(Saved(engine, journalEntry4).LineTrack == 1, "Zwykła korekta czasu i uwagi nie przenosi wcześniej jawnie zapisanego toru.");
			JournalEntry journalEntry5 = engine.Note("sie", At(11, 30), "Po torze lewym", "Telefonogram");
			journalEntry5.LineTrack = 2;
			journalEntry5.Effect = "Zamknij";
			engine.SaveJournal(journalEntry5);
			Check(engine.LineStatus("sie", 2) == "ZAMKNIĘTY" && Saved(engine, journalEntry5).LineTrack == 2, "Interpretacja uwag pociągu nie zmienia numeru toru telefonogramu.");
			Check(!Directory.Exists(Path.Combine(folder, "memory")) && !Directory.Exists(Path.Combine(folder, "return-memory")), "Poprawka działa w pamięci i nie włącza trwałego zapisu.");
			return true;
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			return false;
		}
		finally
		{
			File.WriteAllLines(Path.Combine(folder, "wyniki-tory49.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

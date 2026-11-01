using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace EdrLocal;

public static class Multiline43Tests
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

	private static void Reject(Action action, string text)
	{
		bool ok = false;
		try
		{
			action();
		}
		catch
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

	private static SimRailTrain Source(string number, int incoming, int outgoing)
	{
		SimRailTrain simRailTrain = new SimRailTrain();
		simRailTrain.trainNoLocal = number;
		simRailTrain.trainName = "TME";
		simRailTrain.startStation = "A";
		simRailTrain.endStation = "B";
		simRailTrain.carrierName = "Cargo";
		simRailTrain.timetable = new List<SimRailPoint>
		{
			new SimRailPoint
			{
				pointId = "1",
				nameForPerson = "Sąsiedni A",
				indexOfPoint = 0,
				line = incoming,
				mileage = 0.0
			},
			new SimRailPoint
			{
				pointId = "60",
				nameForPerson = "Test",
				indexOfPoint = 1,
				line = incoming,
				mileage = 10.0,
				track = 1,
				trainType = "TME",
				displayedTrainNumber = number,
				arrivalTime = "2026-10-22 00:10:00",
				departureTime = "2026-10-22 00:11:00",
				isActive = true
			},
			new SimRailPoint
			{
				pointId = "2",
				nameForPerson = "Sąsiedni B",
				indexOfPoint = 2,
				line = outgoing,
				mileage = 20.0
			}
		};
		return simRailTrain;
	}

	private static Section Section(string id, string name, string line)
	{
		Section section = new Section();
		section.Id = id;
		section.Name = name;
		section.Line = line;
		section.Tracks = 2;
		section.NormalIncomingTrack = 1;
		section.ArrivalEven = true;
		section.Basis = "Blokada samoczynna";
		return section;
	}

	private static void Dialog(MainForm main, Train train, string direction, string expected)
	{
		Exception error = null;
		Timer timer = new Timer
		{
			Interval = 100
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
					ComboBox comboBox = All(editor).OfType<ComboBox>().First((ComboBox x) => x.Items.Cast<object>().Any((object y) => y.ToString().Contains("Sąsiedni A")));
					Check(comboBox.Text == expected, "Formularz " + direction + " wybiera " + expected + ".");
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
			main.EditJournal(null, train, direction, null);
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
		Ui.Testing = true;
		Directory.CreateDirectory(folder);
		bool enabled = LocalPersistence.Enabled;
		LocalPersistence.Enabled = false;
		try
		{
			string[] array = new string[7] { " L. 445", " LK 445", " Lk445", " (LK 445)", " - L.445", " / linia 445", " linia kolejowa 445" };
			foreach (string text in array)
			{
				Check(SectionRouting.Neighbour("Sąsiedni A" + text) == "Sąsiedni A" && SectionRouting.SuffixLine("Sąsiedni A" + text) == "445", "Ignorowanie dopisku: " + text + ".");
			}
			Check(SectionRouting.Neighbour("Łódź PZS R145") == "Łódź PZS R145" && SectionRouting.Neighbour("Posterunek 3") == "Posterunek 3", "Normalizacja zachowuje cyfry i oznaczenia należące do nazwy posterunku.");
			Check(SectionRouting.Neighbour("A L. 445 bocznica") == "A L. 445 bocznica", "Niekońcowy tekst nie jest obcinany jako dopisek linii.");
			Check(SectionRouting.SameLine("LK 0445", "445") && SectionRouting.SameNeighbour(" SĄSIEDNI   A L. 445 ", "Sąsiedni A"), "Dopasowanie obsługuje format numeru linii, odstępy i wielkość liter.");
			SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
			simRailSnapshot.ServerCode = "test43";
			simRailSnapshot.Trains = new List<SimRailTrain>
			{
				Source("101", 1, 1),
				Source("102", 2, 2),
				Source("103", 1, 2)
			};
			simRailSnapshot.DispatcherNames = new HashSet<string> { "Test", "Sąsiedni A", "Sąsiedni B" };
			simRailSnapshot.GameTime = new DateTime(2026, 10, 22, 0, 12, 0);
			simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
			SimRailSnapshot snapshot = simRailSnapshot;
			SimRailSession simRailSession = new SimRailSession(null, snapshot, "60");
			Station station = simRailSession.SuggestStation();
			StationSettings.Normalize(station);
			Check(station.Sections.Count == 4 && (from x in station.Sections.Where((Section x) => x.Name == "Sąsiedni A").Select(StationSettings.Title)
				orderby x
				select x).SequenceEqual(new string[2] { "Sąsiedni A L. 1", "Sąsiedni A L. 2" }), "Propozycja rozróżnia dwa dzienniki do tego samego sąsiada dopiskiem L.");
			Station station2 = new Station
			{
				Id = "TEST43",
				Name = "Test",
				PostType = "STACJA",
				SimRailPointId = "60",
				Tracks = new List<string> { "1", "2" },
				Checks = new List<string>(),
				Sections = new List<Section>
				{
					Section("a1", "Sąsiedni A L. 1", "1"),
					Section("b1", "Sąsiedni B", "1"),
					Section("a2", "Sąsiedni A (LK 2)", "LK 002"),
					Section("b2", "Sąsiedni B Lk 2", "2")
				},
				JournalTabs = new List<JournalTabDefinition>
				{
					new JournalTabDefinition
					{
						LeftId = "a1",
						RightId = "a2"
					},
					new JournalTabDefinition
					{
						LeftId = "b1",
						RightId = "b2"
					}
				}
			};
			StationSettings.Normalize(station2);
			Check(StationSettings.Title(station2.Sections.Single((Section x) => x.Id == "a2")) == "Sąsiedni A L. 2", "Domyślna nazwa dziennika ma ujednolicony dopisek.");
			Station station3 = Json.Clone(station2);
			station3.Sections[0].JournalName = "Dalekobieżne";
			station3.Sections[2].JournalName = "Podmiejskie";
			StationSettings.Normalize(station3);
			Check(station3.Sections[0].JournalName == "Dalekobieżne" && station3.Sections[2].JournalName == "Podmiejskie", "Własne nazwy dzienników pozostają zachowane.");
			Check(SectionRouting.Find(station2.Sections, "Sąsiedni A", "2").Id == "a2" && SectionRouting.Find(station2.Sections, "Sąsiedni A L. 1", "2").Id == "a2", "Wybór opiera się na polu numeru linii, a dopisek nazwy jest pomijany.");
			Check(SectionRouting.Find(station2.Sections, "Sąsiedni A", null) == null && SectionRouting.Find(station2.Sections, "Sąsiedni A", "999") == null, "Brak lub niedopasowany numer linii nie wybiera losowego równoległego dziennika.");
			Store store = new Store(Path.Combine(folder, "no-data"));
			store.Load();
			store.Commit((AppState s) =>
			{
				s.Stations = new List<Station> { station2 };
				s.Trains.Clear();
				s.Entries.Clear();
				s.Duties.Clear();
			});
			Engine engine = new Engine(store)
			{
				SimRail = simRailSession,
				User = store.State.Users.First((LocalUser x) => x.Id == "DEMO")
			};
			StationSettings.Validate(station2, engine.State);
			engine.ApplySimRailSnapshot(snapshot);
			engine.BeginDuty(engine.Now.AddMinutes(-2.0));
			Check(engine.State.Trains.All((Train x) => x.From == "Sąsiedni A" && x.To == "Sąsiedni B"), "Projekcja SimRail zachowuje sąsiadów pomimo dopisków w konfiguracji.");
			Train train = engine.State.Trains.Single((Train x) => x.ExternalTrainNo == "103");
			Check(train.LineIn == "1" && train.LineOut == "2", "Pociąg zmieniający linię ma oddzielny numer linii przyjazdu i wyjazdu.");
			using (MainForm mainForm = new MainForm(engine))
			{
				mainForm.Show();
				mainForm.Size = new Size(1600, 850);
				Application.DoEvents();
				Train train2 = engine.State.Trains.Single((Train x) => x.ExternalTrainNo == "102");
				Dialog(mainForm, train2, "Z", "Sąsiedni A L. 2");
				Dialog(mainForm, train2, "DO", "Sąsiedni B L. 2");
				mainForm.SelectTrain(train2.Id);
				Ui.Capture(mainForm, Path.Combine(folder, "wielotory-1.0.10.199.png"));
				mainForm.Close();
			}
			string[][] array2 = new string[3][]
			{
				new string[3] { "101", "a1", "b1" },
				new string[3] { "102", "a2", "b2" },
				new string[3] { "103", "a1", "b2" }
			};
			foreach (string[] pair in array2)
			{
				List<Train> trains = engine.State.Trains;
				Func<Train, bool> predicate = (Train x) => x.ExternalTrainNo == pair[0];
				Train train3 = trains.Single(predicate);
				JournalEntry journalEntry = engine.RegisterScheduledTrain(train3.Id, null);
				JournalEntry journalEntry2 = engine.RegisterScheduledTrain(train3.Id, null);
				Check(journalEntry.SectionId == pair[1] && journalEntry2.SectionId == pair[2], "Dwuklik dobiera obie linie właściwie dla " + pair[0] + ".");
			}
			Station invalid = Json.Clone(station2);
			invalid.Sections.Add(Section("duplicate", "sąsiedni A LK 1", "001"));
			Reject(() =>
			{
				StationSettings.Validate(invalid, engine.State);
			}, "Ten sam sąsiad i linia nie mogą wystąpić dwa razy pod różnymi dopiskami.");
			Train ambiguous = new Train
			{
				Id = "ambiguous",
				StationId = station2.Id,
				Number = "90001",
				Kind = "TME",
				From = "Sąsiedni A",
				To = "Sąsiedni B",
				Origin = "A",
				Destination = "B",
				LineIn = "",
				LineOut = "2",
				PlanArrival = engine.Now,
				PlanDeparture = engine.Now,
				Track = "1",
				Profile = new TrainProfile()
			};
			engine.Change("Test niepełnej linii", ambiguous.Id, (AppState s) =>
			{
				s.Trains.Add(ambiguous);
			});
			Reject(() =>
			{
				engine.RegisterScheduledTrain(ambiguous.Id, null);
			}, "Automatyczna rejestracja nie zgaduje dziennika przy brakującej linii.");
			JournalEntry journalEntry3 = engine.RegisterScheduledTrain(ambiguous.Id, "a2");
			Check(journalEntry3.SectionId == "a2", "Jawny ręczny wybór dziennika nadal działa.");
			List<Section> list = Json.Clone(station2.Sections);
			list.Single((Section x) => x.Id == "a2").Conducted = false;
			Check(SectionRouting.Find(list, "Sąsiedni A", "2") == null, "Nieprowadzony dziennik nie jest automatycznie wybierany.");
			List<Section> list2 = new List<Section>();
			list2.Add(Section("only", "Jedyny sąsiad LK 3", "3"));
			List<Section> sections = list2;
			Check(SectionRouting.Find(sections, "Jedyny sąsiad", null).Id == "only", "Brak numeru linii jest dopuszczalny przy jednym jednoznacznym dzienniku.");
			engine.ApplySimRailSnapshot(snapshot);
			Check(engine.State.Trains.Where((Train x) => x.SourceProvider == "SimRail").All((Train x) => x.From == "Sąsiedni A" && x.To == "Sąsiedni B") && engine.Station.JournalTabs[0].LeftId == "a1" && engine.Station.JournalTabs[0].RightId == "a2", "Odświeżenie zachowuje rozpoznanie sąsiadów i układ dzienników.");
			Check(!Directory.Exists(store.DirectoryPath) && !LocalPersistence.Enabled, "Obsługa wielu linii nadal nie zapisuje danych na dysku.");
			log.Add("ALL MULTILINE 43 TESTS PASSED");
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
			File.WriteAllLines(Path.Combine(folder, "wyniki-wielotory43.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

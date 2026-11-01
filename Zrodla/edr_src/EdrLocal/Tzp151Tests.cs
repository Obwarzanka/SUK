using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace EdrLocal;

public static class Tzp151Tests
{
	private static readonly List<string> log = new List<string>();

	private static readonly DateTime day = new DateTime(2026, 10, 22, 12, 0, 0);

	private static void Check(bool ok, string text)
	{
		if (!ok)
		{
			throw new Exception("FAIL: " + text);
		}
		log.Add("PASS: " + text);
	}

	private static T Field<T>(object o, string n)
	{
		return (T)o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
	}

	private static void Call(object o, string n, params object[] args)
	{
		try
		{
			o.GetType().GetMethod(n, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, args);
		}
		catch (TargetInvocationException ex)
		{
			throw ex.InnerException;
		}
	}

	private static RailSignal Signal(JournalPane p, int n)
	{
		return Field<List<RailSignal>>(p, "signals").Single((RailSignal x) => x.Number == n);
	}

	private static TelephoneData Data(string id, int track, DateTime sent, DateTime effective)
	{
		TelephoneData telephoneData = new TelephoneData();
		telephoneData.TemplateId = id;
		telephoneData.Track = track;
		telephoneData.SentAt = sent;
		telephoneData.EffectiveAt = effective;
		telephoneData.Receiver = "Dyżurny sąsiedni";
		telephoneData.Reason = "usterka blokady";
		telephoneData.ClosureMode = "awaryjnie";
		return telephoneData;
	}

	private static Bitmap Draw(RailSignal signal)
	{
		Bitmap bitmap = new Bitmap(signal.Width, signal.Height);
		signal.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
		return bitmap;
	}

	private static int Changes(Bitmap a, Bitmap b, Rectangle r)
	{
		int num = 0;
		for (int i = r.Top; i < r.Bottom; i++)
		{
			for (int j = r.Left; j < r.Right; j++)
			{
				if (a.GetPixel(j, i).ToArgb() != b.GetPixel(j, i).ToArgb())
				{
					num++;
				}
			}
		}
		return num;
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		LocalPersistence.Enabled = false;
		try
		{
			SimRailTrain simRailTrain = new SimRailTrain();
			simRailTrain.trainNoLocal = "7328";
			simRailTrain.trainName = "MOJ";
			simRailTrain.startStation = "Sieradz";
			simRailTrain.endStation = "Łask";
			simRailTrain.timetable = new List<SimRailPoint>();
			SimRailTrain simRailTrain2 = simRailTrain;
			for (int i = 0; i < 3; i++)
			{
				simRailTrain2.timetable.Add(new SimRailPoint
				{
					pointId = (new string[3] { "10", "60", "20" })[i],
					nameForPerson = (new string[3] { "Sieradz", "Zduńska Wola", "Łask" })[i],
					indexOfPoint = i,
					line = 14,
					track = 1,
					displayedTrainNumber = "7328",
					trainType = "MOJ",
					isActive = true,
					arrivalTime = "2026-10-22 11:50:00",
					departureTime = "2026-10-22 11:51:00"
				});
			}
			SimRailSnapshot snapshot = new SimRailSnapshot
			{
				ServerCode = "tzp151",
				GameTime = day,
				DownloadedUtc = DateTime.UtcNow,
				DispatcherNames = new HashSet<string> { "Sieradz", "Zduńska Wola", "Łask" },
				Trains = new List<SimRailTrain> { simRailTrain2 }
			};
			SimRailSession session = new SimRailSession(null, snapshot, "60");
			Action<DateTime> action = (DateTime t) =>
			{
				snapshot.GameTime = t;
				snapshot.DownloadedUtc = DateTime.UtcNow;
				session.Accept(snapshot);
			};
			Station station = new Station
			{
				Id = "SIMRAIL-60",
				SimRailPointId = "60",
				Name = "Zduńska Wola",
				PostType = "STACJA",
				Tracks = new List<string> { "1", "2" },
				Checks = new List<string>(),
				Sections = new List<Section>
				{
					new Section
					{
						Id = "sie",
						Name = "Sieradz",
						Line = "14",
						Tracks = 2,
						NormalIncomingTrack = 1,
						ArrivalEven = false,
						Basis = "Jednoodstępowa (półsamoczynna) blokada liniowa"
					},
					new Section
					{
						Id = "las",
						Name = "Łask",
						Line = "14",
						Tracks = 1,
						NormalIncomingTrack = 1,
						Basis = "Jednoodstępowa (półsamoczynna) blokada liniowa"
					}
				},
				Options = new StationOptions
				{
					WriteSepe = false
				}
			};
			StationSettings.Normalize(station);
			Store store = new Store(Path.Combine(folder, "session"));
			store.Load();
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
			engine.SimRail = session;
			Engine engine2 = engine;
			engine2.ApplySimRailSnapshot(snapshot);
			engine2.BeginDuty(day.AddMinutes(-5.0));
			using (MainForm mainForm = new MainForm(engine2))
			{
				mainForm.Show();
				mainForm.Size = new Size(1600, 900);
				Application.DoEvents();
				mainForm.ApplyZoom();
				JournalPane journalPane = Field<List<JournalPane>>(mainForm, "panes").Single((JournalPane x) => x.SectionId == "sie");
				JournalPane p = Field<List<JournalPane>>(mainForm, "panes").Single((JournalPane x) => x.SectionId == "las");
				journalPane.RefreshSignals();
				Check(!Signal(journalPane, 1).Telephone && !Signal(journalPane, 2).Telephone, "Blokada półsamoczynna nie jest oznaczana jako TZP.");
				engine2.Station.Sections[0].LineTracks.Single((LineTrackSetting x) => x.Number == 1).Basis = "Zapowiadanie telefoniczne";
				journalPane.RefreshSignals();
				Check(Signal(journalPane, 1).Telephone && !Signal(journalPane, 2).Telephone, "Konfiguracja Z pokazuje telefon wyłącznie przy właściwym torze.");
				Check(Signal(journalPane, 1).Status == "WOLNY" && !Signal(journalPane, 1).MovementIncoming.HasValue, "Ikonka TZP nie tworzy zajętości ani kierunku rzeczywistej jazdy.");
				engine2.Station.Sections[0].LineTracks.Single((LineTrackSetting x) => x.Number == 1).Basis = "Jednoodstępowa (półsamoczynna) blokada liniowa";
				journalPane.RefreshSignals();
				Check(!Signal(journalPane, 1).Telephone, "Powrót podstawy Z do blokady usuwa telefon bez zmiany statusu toru.");
				engine2.Station.Sections[0].LineTracks.Single((LineTrackSetting x) => x.Number == 2).BasisOut = "Zapowiadanie telefoniczne";
				journalPane.RefreshSignals();
				Check(!Signal(journalPane, 1).Telephone && Signal(journalPane, 2).Telephone, "Podstawa TZP ustawiona tylko dla DO także ma znacznik.");
				engine2.Station.Sections[0].LineTracks.Single((LineTrackSetting x) => x.Number == 2).BasisOut = "Jednoodstępowa (półsamoczynna) blokada liniowa";
				JournalEntry journalEntry = engine2.SaveTelephone("sie", null, Data("16", 1, day, day.AddMinutes(10.0)));
				journalPane.RefreshSignals();
				Check(!Signal(journalPane, 1).Telephone, "Telefonogram z przyszłą godziną nie zapala telefonu przed czasem.");
				action(day.AddMinutes(10.0));
				Call(mainForm, "RefreshRuntimeIndicators");
				Check(Signal(journalPane, 1).Telephone && !Signal(journalPane, 2).Telephone, "Odświeżanie okresowe włącza TZP o godzinie obowiązywania telefonogramu [16].");
				Check(!Signal(p, 1).Telephone, "Telefonogram nie zmienia ikonki innego szlaku.");
				engine2.SaveTelephone("sie", null, Data("21", 1, engine2.Now, day.AddMinutes(10.0)));
				JournalEntry journalEntry2 = engine2.Note("sie", day.AddMinutes(10.0), "Po torze nr [1]", "Pociąg");
				journalEntry2.Number = "731TEST";
				journalEntry2.Direction = "Z";
				journalEntry2.Track = "1";
				journalEntry2.Left = day.AddMinutes(10.0);
				engine2.SaveJournal(journalEntry2);
				mainForm.RefreshData();
				Check(Signal(journalPane, 1).Telephone && Signal(journalPane, 1).Status == "ZAMKNIĘTY" && Signal(journalPane, 1).MovementIncoming == true, "TZP współistnieje z zamknięciem toru oraz zajętością i strzałką przyjazdu.");
				using (Bitmap bitmap = Draw(Signal(journalPane, 1)))
				{
					int width = Signal(journalPane, 1).Width;
					float num = (float)width / 40f;
					Check(width == (int)Math.Round(60f * mainForm.CurrentZoom / 1.375f) && bitmap.GetPixel((int)Math.Round(30f * num), (int)Math.Round(7f * num)).ToArgb() == Color.Red.ToArgb() && bitmap.GetPixel((int)Math.Round(19f * num), (int)Math.Round(20f * num)).ToArgb() == Color.Blue.ToArgb(), "Tablica zamknięcia i niebieska strzałka pozostają widoczne przy telefonie.");
				}
				Ui.Capture(mainForm, Path.Combine(folder, "tzp-zamkniety-zajety.png"));
				JournalEntry journalEntry3 = engine2.SaveTelephone("sie", null, Data("17", 1, engine2.Now, day.AddMinutes(15.0)));
				journalPane.RefreshSignals();
				Check(Signal(journalPane, 1).Telephone, "Przyszłe odwołanie nie usuwa aktywnego TZP przed czasem.");
				action(day.AddMinutes(15.0));
				Call(mainForm, "RefreshRuntimeIndicators");
				Check(!Signal(journalPane, 1).Telephone && Signal(journalPane, 1).Status == "ZAMKNIĘTY" && Signal(journalPane, 1).MovementIncoming == true, "Telefonogram [17] usuwa telefon, zachowując zamknięcie i zajętość.");
				engine2.Cancel(journalEntry3.Id, "Błędne odwołanie");
				journalPane.RefreshSignals();
				Check(Signal(journalPane, 1).Telephone, "Wykreślenie odwołania przywraca poprzedni obowiązujący telefonogram.");
				engine2.Cancel(journalEntry.Id, "Błędne wprowadzenie");
				journalPane.RefreshSignals();
				Check(!Signal(journalPane, 1).Telephone, "Wykreślenie wprowadzenia usuwa telefon z ikony.");
				JournalEntry journalEntry4 = engine2.SaveTelephone("sie", null, Data("16", 2, engine2.Now, day.AddMinutes(15.0)));
				journalPane.RefreshSignals();
				Check(!Signal(journalPane, 1).Telephone && Signal(journalPane, 2).Telephone, "Nowe TZP na drugim torze nie zapala pierwszego toru.");
				TelephoneData telephoneData = Json.Clone(journalEntry4.Telephone);
				telephoneData.EffectiveAt = day.AddMinutes(20.0);
				journalEntry4 = engine2.SaveTelephone("sie", journalEntry4, telephoneData);
				journalPane.RefreshSignals();
				Check(!Signal(journalPane, 2).Telephone, "Edycja początku obowiązywania aktualizuje ikonkę bez ponownego otwarcia okna.");
				action(day.AddMinutes(20.0));
				Call(mainForm, "RefreshRuntimeIndicators");
				Check(Signal(journalPane, 2).Telephone, "Zmieniona godzina ponownie uaktywnia ikonkę we właściwym czasie.");
				engine2.Station.Sections[0].RightTrackNumber = 1;
				mainForm.BuildJournals();
				mainForm.RefreshData();
				journalPane = Field<List<JournalPane>>(mainForm, "panes").Single((JournalPane x) => x.SectionId == "sie");
				Check((from x in Field<List<RailSignal>>(journalPane, "signals")
					select x.Number).SequenceEqual(new int[2] { 2, 1 }) && Signal(journalPane, 2).Telephone && !Signal(journalPane, 1).Telephone, "Zamiana położenia ikon zachowuje przypisanie TZP do numeru toru.");
				engine2.Station.Sections[0].LineTracks.Single((LineTrackSetting x) => x.Number == 1).Basis = "Zapowiadanie telefoniczne";
				engine2.SaveTelephone("sie", null, Data("17", 1, engine2.Now, day.AddMinutes(20.0)));
				journalPane.RefreshSignals();
				Check(Signal(journalPane, 1).Telephone, "Odwołanie przywraca konfigurację zasadniczą, również jeśli ta używa TZP.");
				float[] array = new float[3] { 1000f, 1500f, 1920f };
				for (int num2 = 0; num2 < array.Length; num2++)
				{
					float num3 = array[num2];
					mainForm.Size = new Size((int)num3, 900);
					mainForm.ApplyZoom();
					Application.DoEvents();
					RailSignal railSignal = Signal(journalPane, 2);
					Check(railSignal.Width == (int)Math.Round(60f * mainForm.CurrentZoom / 1.375f) && railSignal.Parent.ClientSize.Width >= railSignal.Right && railSignal.Parent.ClientSize.Height >= railSignal.Bottom, "Telefon mieści się przy torze dla szerokości okna " + num3.ToString("0.0") + ".");
					Ui.Capture(mainForm, Path.Combine(folder, "tzp-skala-" + num3.ToString("0.0") + ".png"));
				}
				mainForm.Close();
			}
			using (Bitmap bitmap2 = new Bitmap(440, 290))
			{
				using Graphics graphics = Graphics.FromImage(bitmap2);
				using Font font = new Font("Segoe UI", 9f);
				graphics.Clear(Color.White);
				int num4 = 0;
				string[] array2 = new string[4] { "WOLNY", "ZAJĘTY: TEST", "ZAMKNIĘTY", "ZAMKNIĘTY" };
				foreach (string status in array2)
				{
					graphics.DrawString((new string[4] { "TZP: tor wolny", "TZP: tor zajęty", "TZP: tor zamknięty", "TZP: zamknięty i zajęty" })[num4], font, Brushes.Black, 2f, num4 * 72 + 12);
					int num5 = 0;
					float[] array = new float[3] { 1f, 1.5f, 2f };
					foreach (float num7 in array)
					{
						RailSignal railSignal2 = new RailSignal();
						railSignal2.Number = 1;
						railSignal2.Telephone = true;
						railSignal2.Incoming = true;
						railSignal2.Status = status;
						railSignal2.MovementIncoming = ((num4 == 1 || num4 == 3) ? new bool?(true) : ((bool?)null));
						railSignal2.Size = new Size((int)(40f * num7), (int)(30f * num7));
						using RailSignal railSignal3 = railSignal2;
						using Bitmap bitmap3 = Draw(railSignal3);
						graphics.DrawImageUnscaled(bitmap3, 170 + num5++ * 85, num4 * 72);
						RailSignal railSignal4 = new RailSignal();
						railSignal4.Number = 1;
						railSignal4.Telephone = false;
						railSignal4.Incoming = true;
						railSignal4.Status = status;
						railSignal4.MovementIncoming = railSignal3.MovementIncoming;
						railSignal4.Size = railSignal3.Size;
						using RailSignal signal = railSignal4;
						using Bitmap a = Draw(signal);
						Rectangle r = new Rectangle((int)(27f * num7), 0, (int)(13f * num7), (int)(30f * num7));
						Check(Changes(a, bitmap3, r) > 15 && Changes(a, bitmap3, new Rectangle(0, 0, (int)(25f * num7), bitmap3.Height)) == 0, "Renderer dodaje czytelny znacznik bez zmiany szyn i strzałek: stan " + num4 + ", skala " + num7 + ".");
					}
					num4++;
				}
				bitmap2.Save(Path.Combine(folder, "tzp-porownanie.png"));
			}
			Check(!Directory.Exists(store.DirectoryPath), "Test TZP pozostaje w pamięci sesji.");
			return true;
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			return false;
		}
		finally
		{
			File.WriteAllLines(Path.Combine(folder, "wyniki-tzp151.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace EdrLocal;

public static class Directions45Tests
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

	private static T Field<T>(object value, string name)
	{
		return (T)value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
	}

	private static void Call(object value, string name, params object[] args)
	{
		try
		{
			value.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, args);
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

	private static ComboBox Choice(Control parent, string title)
	{
		Label label = parent.Controls.OfType<Label>().Single((Label x) => x.Text == title);
		return parent.Controls.OfType<ComboBox>().Single((ComboBox x) => x.Left == label.Left && x.Top == label.Top + 23);
	}

	private static Section Section(string id, string name, bool even, int tracks)
	{
		Section section = new Section();
		section.Id = id;
		section.Name = name;
		section.Line = "4";
		section.Tracks = tracks;
		section.ArrivalEven = even;
		section.NormalIncomingTrack = 1;
		section.RightTrackNumber = ((tracks != 2) ? 1 : 2);
		section.Basis = "Blokada samoczynna";
		return section;
	}

	private static Engine Make(string path, int tracks)
	{
		Store store = new Store(path);
		store.Load();
		Station station = new Station
		{
			Id = "GW45",
			Name = "Góra Włodowska",
			PostType = "STACJA",
			Tracks = new List<string> { "1", "2" },
			Checks = new List<string>(),
			Sections = new List<Section>
			{
				Section("ps", "Psary", even: false, tracks),
				Section("za", "Zawiercie", even: true, tracks)
			},
			JournalTabs = new List<JournalTabDefinition>
			{
				new JournalTabDefinition
				{
					LeftId = "ps",
					RightId = "za"
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
		return engine;
	}

	private static bool Incoming(Section section, int number)
	{
		return section.LineTracks.Single((LineTrackSetting x) => x.Number == number).NormalIncoming;
	}

	private static void Edit(StationConfigurationForm config, int row, Action<Editor> action)
	{
		Field<ConfigurationGrid>(config, "sectionGrid").CurrentCell = Field<ConfigurationGrid>(config, "sectionGrid").Rows[row].Cells[0];
		Exception failure = null;
		Timer timer = new Timer
		{
			Interval = 60
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
					editor.Buttons.Controls.OfType<Button>().Single((Button x) => x.Text == "Zapisz").PerformClick();
				}
				catch (Exception ex)
				{
					failure = ex;
					editor.Close();
				}
			};
			timer.Start();
			Call(config, "EditSection", false);
		}
		finally
		{
			if (timer != null)
			{
				((IDisposable)timer).Dispose();
			}
		}
		if (failure != null)
		{
			throw failure;
		}
	}

	private static RailSignal Signal(JournalPane pane, int number)
	{
		return Field<List<RailSignal>>(pane, "signals").Single((RailSignal x) => x.Number == number);
	}

	private static GroupBox Basic(Editor d)
	{
		return All(d).OfType<GroupBox>().Single((GroupBox x) => x.Text == "Podstawowe ustawienia");
	}

	private static GroupBox Left(Editor d)
	{
		return All(d).OfType<GroupBox>().Single((GroupBox x) => x.Text == "Tor szlakowy lewy");
	}

	private static GroupBox Right(Editor d)
	{
		return All(d).OfType<GroupBox>().Single((GroupBox x) => x.Text == "Tor szlakowy prawy");
	}

	public static bool Run(string folder)
	{
		Ui.Testing = true;
		Directory.CreateDirectory(folder);
		bool enabled = LocalPersistence.Enabled;
		LocalPersistence.Enabled = false;
		try
		{
			Engine engine = Make(Path.Combine(folder, "no-data"), 2);
			Section section = engine.Station.Sections[0];
			Section section2 = engine.Station.Sections[1];
			Check(Incoming(section, 1) && !Incoming(section, 2), "Domyślne Psary: tor 1 wjazd, tor 2 wyjazd.");
			Check(!Incoming(section2, 1) && Incoming(section2, 2), "Domyślne Zawiercie: tor 1 wyjazd, tor 2 wjazd.");
			Check(section.NormalIncomingTrack == 1 && section2.NormalIncomingTrack == 2, "Pole pomocnicze kierunku jest zgodne z ustawieniami poszczególnych torów.");
			string text = Json.Text(engine.Station);
			StationSettings.Normalize(engine.Station);
			Check(Json.Text(engine.Station) == text, "Powtórna normalizacja nie zmienia kierunków ani kolejności.");
			Section section3 = Section("partial", "Brak drugiego toru", even: true, 2);
			section3.LineTracks = new List<LineTrackSetting>
			{
				new LineTrackSetting
				{
					Number = 1,
					NormalIncoming = true,
					Basis = "Własna podstawa",
					TrafficType = "Dwukierunkowy"
				}
			};
			SectionTrackDirections.Normalize(section3);
			Check(section3.LineTracks.Count == 2 && !Incoming(section3, 1) && Incoming(section3, 2), "Przejście z niepełnego opisu na dwa tory nie pozostawia dwóch błędnych kierunków.");
			Check(section3.LineTracks[0].Basis == "Własna podstawa" && section3.LineTracks[0].TrafficType == "Dwukierunkowy", "Uzupełnienie kierunków zachowuje podstawę i rodzaj ruchu.");
			Section section4 = Section("custom", "Własne kierunki", even: true, 2);
			section4.LineTracks = new List<LineTrackSetting>
			{
				new LineTrackSetting
				{
					Number = 1,
					NormalIncoming = true
				},
				new LineTrackSetting
				{
					Number = 2,
					NormalIncoming = false
				}
			};
			SectionTrackDirections.Normalize(section4);
			Check(Incoming(section4, 1) && !Incoming(section4, 2) && section4.NormalIncomingTrack == 1, "Kompletny ręczny opis torów ma pierwszeństwo przed domyślną parzystością.");
			Section section5 = Section("single", "Jednotor", even: false, 1);
			SectionTrackDirections.Normalize(section5);
			Check(SectionTrackDirections.Incoming(section5, 1), "Jednotor zachowuje skonfigurowany kierunek wjazdowy.");
			section5.LineTracks[0].NormalIncoming = false;
			SectionTrackDirections.Normalize(section5);
			Check(!SectionTrackDirections.Incoming(section5, 1) && section5.NormalIncomingTrack == 0, "Jednotor zachowuje ręczne ustawienie wyjazdowe.");
			engine = Make(Path.Combine(folder, "manual-editor"), 1);
			using (StationConfigurationForm stationConfigurationForm = new StationConfigurationForm(engine))
			{
				stationConfigurationForm.Show();
				Application.DoEvents();
				Edit(stationConfigurationForm, 0, (Editor d) =>
				{
					Choice(Basic(d), "Rodzaj linii na szlaku").Text = "Dwutorowa";
					Check(Choice(Left(d), "Zasadniczy kierunek ruchu").SelectedIndex == 0 && Choice(Right(d), "Zasadniczy kierunek ruchu").SelectedIndex == 1, "Edytor po wyborze dwóch torów ustawia Psary: 1 Z, 2 DO.");
				});
				Edit(stationConfigurationForm, 1, (Editor d) =>
				{
					Choice(Basic(d), "Rodzaj linii na szlaku").Text = "Dwutorowa";
					Check(Choice(Left(d), "Zasadniczy kierunek ruchu").SelectedIndex == 1 && Choice(Right(d), "Zasadniczy kierunek ruchu").SelectedIndex == 0, "Edytor po wyborze dwóch torów ustawia Zawiercie: 1 DO, 2 Z.");
				});
				stationConfigurationForm.SaveDraft();
			}
			section = engine.Station.Sections[0];
			section2 = engine.Station.Sections[1];
			Check(Incoming(section, 1) && !Incoming(section2, 1) && Incoming(section2, 2), "Główne Zapisz zachowuje odmienne kierunki obu szlaków.");
			using (StationConfigurationForm stationConfigurationForm2 = new StationConfigurationForm(engine))
			{
				stationConfigurationForm2.Show();
				Application.DoEvents();
				Edit(stationConfigurationForm2, 1, (Editor d) =>
				{
					Check(Choice(Left(d), "Zasadniczy kierunek ruchu").SelectedIndex == 1, "Ponowne otwarcie pokazuje zapisany kierunek Zawiercia.");
					Choice(Basic(d), "Kierunek nieparzysty").SelectedIndex = 0;
					Check(Choice(Left(d), "Zasadniczy kierunek ruchu").SelectedIndex == 0 && Choice(Right(d), "Zasadniczy kierunek ruchu").SelectedIndex == 1, "Zmiana kierunku nieparzystego przelicza oba tory.");
					Choice(Basic(d), "Kierunek nieparzysty").SelectedIndex = 1;
					Check(Choice(Left(d), "Zasadniczy kierunek ruchu").SelectedIndex == 1 && Choice(Right(d), "Zasadniczy kierunek ruchu").SelectedIndex == 0, "Powrót kierunku nieparzystego przywraca układ Zawiercia.");
				});
				stationConfigurationForm2.SaveDraft();
			}
			using (MainForm mainForm = new MainForm(engine))
			{
				mainForm.Show();
				mainForm.Size = new Size(1600, 850);
				Application.DoEvents();
				List<JournalPane> source = Field<List<JournalPane>>(mainForm, "panes");
				JournalPane journalPane = source.Single((JournalPane x) => x.SectionId == "ps");
				JournalPane journalPane2 = source.Single((JournalPane x) => x.SectionId == "za");
				Check(Signal(journalPane, 1).Incoming && !Signal(journalPane, 2).Incoming, "Nagłówek Psar wyświetla 1 ↓ i 2 ↑.");
				Check(!Signal(journalPane2, 1).Incoming && Signal(journalPane2, 2).Incoming, "Nagłówek Zawiercia wyświetla 1 ↑ i 2 ↓.");
				Check((from x in Field<List<RailSignal>>(journalPane, "signals")
					select x.Number).SequenceEqual(new int[2] { 1, 2 }) && (from x in Field<List<RailSignal>>(journalPane2, "signals")
					select x.Number).SequenceEqual(new int[2] { 1, 2 }), "Numery i położenie torów pozostają zgodne z konfiguracją.");
				Ui.Capture(mainForm, Path.Combine(folder, "strzalki-1.0.10.199.png"));
				section2 = engine.Station.Sections[1];
				section2.NormalIncomingTrack = 1;
				journalPane2.RefreshSignals();
				Check(!Signal(journalPane2, 1).Incoming && Signal(journalPane2, 2).Incoming, "Nieaktualne pole pomocnicze nie odwraca strzałek wbrew konfiguracji torów.");
				section2.LineTracks.Single((LineTrackSetting x) => x.Number == 1).NormalIncoming = true;
				section2.LineTracks.Single((LineTrackSetting x) => x.Number == 2).NormalIncoming = false;
				journalPane2.RefreshSignals();
				Check(Signal(journalPane2, 1).Incoming && !Signal(journalPane2, 2).Incoming, "Odświeżenie od razu odczytuje ręcznie zmienione kierunki torów.");
				section2.LineTracks.Single((LineTrackSetting x) => x.Number == 1).NormalIncoming = false;
				section2.LineTracks.Single((LineTrackSetting x) => x.Number == 2).NormalIncoming = true;
				journalPane2.RefreshSignals();
				engine.BeginDuty(engine.Now.AddMinutes(-5.0));
				JournalEntry journalEntry = engine.Note("za", engine.Now.AddMinutes(-1.0), "Jazda przeciwna do zasadniczej", "Pociąg");
				journalEntry.Number = "41126";
				journalEntry.Direction = "Z";
				journalEntry.LineTrack = 1;
				journalEntry.Track = "1";
				journalEntry.Left = engine.Now.AddMinutes(-1.0);
				engine.SaveJournal(journalEntry);
				journalPane2.RefreshSignals();
				Check(!Signal(journalPane2, 1).Incoming && Signal(journalPane2, 1).MovementIncoming == true, "Niebieska strzałka rzeczywistego wjazdu nie odwraca szarego kierunku zasadniczego.");
				Check(Signal(journalPane2, 1).Status.StartsWith("ZAJĘTY"), "Zajętość toru nadal jest wskazywana.");
				using (Bitmap bitmap = new Bitmap(40, 30))
				{
					using Bitmap bitmap2 = new Bitmap(40, 30);
					RailSignal railSignal = new RailSignal();
					railSignal.Incoming = true;
					using RailSignal railSignal2 = railSignal;
					railSignal2.DrawToBitmap(bitmap, new Rectangle(0, 0, 40, 30));
					railSignal2.Incoming = false;
					railSignal2.DrawToBitmap(bitmap2, new Rectangle(0, 0, 40, 30));
					Check(bitmap.GetPixel(2, 24).ToArgb() == Color.Gray.ToArgb() && bitmap2.GetPixel(2, 16).ToArgb() == Color.Gray.ToArgb() && bitmap.GetPixel(2, 16).ToArgb() != Color.Gray.ToArgb() && bitmap2.GetPixel(2, 24).ToArgb() != Color.Gray.ToArgb(), "Renderer zachowuje ↓ dla wjazdu i ↑ dla wyjazdu.");
				}
				mainForm.Close();
			}
			using (StationConfigurationForm stationConfigurationForm3 = new StationConfigurationForm(engine))
			{
				stationConfigurationForm3.Show();
				Application.DoEvents();
				Edit(stationConfigurationForm3, 1, (Editor d) =>
				{
					Choice(Left(d), "Zasadniczy kierunek ruchu").SelectedIndex = 0;
					Choice(Right(d), "Zasadniczy kierunek ruchu").SelectedIndex = 1;
				});
				stationConfigurationForm3.SaveDraft();
			}
			Check(Incoming(engine.Station.Sections[1], 1) && !Incoming(engine.Station.Sections[1], 2), "Jawna korekta zasadniczych kierunków pozostaje dostępna i jest zapisywana w sesji.");
			using (StationConfigurationForm stationConfigurationForm4 = new StationConfigurationForm(engine))
			{
				stationConfigurationForm4.Show();
				Application.DoEvents();
				Edit(stationConfigurationForm4, 1, (Editor d) =>
				{
					Check(Choice(Left(d), "Zasadniczy kierunek ruchu").SelectedIndex == 0, "Ponowne otwarcie nie nadpisuje ręcznie ustawionego kierunku.");
				});
				stationConfigurationForm4.SaveDraft();
			}
			Section section6 = engine.Station.Sections[1];
			section6.RightTrackNumber = 1;
			using (MainForm mainForm2 = new MainForm(engine))
			{
				mainForm2.Show();
				Application.DoEvents();
				JournalPane journalPane3 = Field<List<JournalPane>>(mainForm2, "panes").Single((JournalPane x) => x.SectionId == "za");
				Check((from x in Field<List<RailSignal>>(journalPane3, "signals")
					select x.Number).SequenceEqual(new int[2] { 2, 1 }), "Zamiana fizycznej kolejności torów zachowuje ich numery.");
				Check(Signal(journalPane3, 1).Incoming && !Signal(journalPane3, 2).Incoming, "Kierunek jest przypisany do numeru toru niezależnie od jego położenia.");
				mainForm2.Close();
			}
			Check(!Directory.Exists(Path.Combine(folder, "no-data")) && !Directory.Exists(Path.Combine(folder, "manual-editor")) && !LocalPersistence.Enabled, "Zmiana kierunków nie włącza trwałego zapisu.");
			log.Add("ALL DIRECTIONS 45 TESTS PASSED");
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
			File.WriteAllLines(Path.Combine(folder, "wyniki-kierunki45.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

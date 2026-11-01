using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace EdrLocal;

public static class Traffic153Tests
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

	private static void Reject(Action a, string text)
	{
		bool ok = false;
		try
		{
			a();
		}
		catch
		{
			ok = true;
		}
		Check(ok, text);
	}

	private static T Field<T>(object o, string n)
	{
		return (T)o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
	}

	private static object Call(object o, string n, params object[] args)
	{
		try
		{
			return o.GetType().GetMethod(n, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, args);
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

	private static TelephoneData Data(string id, int track, DateTime at)
	{
		TelephoneData telephoneData = new TelephoneData();
		telephoneData.TemplateId = id;
		telephoneData.Track = track;
		telephoneData.SentAt = at;
		telephoneData.EffectiveAt = at;
		telephoneData.Receiver = "Anna";
		telephoneData.Reason = "usterka";
		telephoneData.TrainNumber = "91918";
		telephoneData.LastTrainDeparture = at.AddMinutes(-5.0);
		return telephoneData;
	}

	private static RailSignal Signal(JournalPane p, int n)
	{
		return Field<List<RailSignal>>(p, "signals").Single((RailSignal x) => x.Number == n);
	}

	private static void Dialog(MainForm main, Action open, Action<Form> inspect)
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
				Form form = Application.OpenForms.Cast<Form>().LastOrDefault((Form x) => x != main && x.Visible);
				if (form == null)
				{
					return;
				}
				timer.Stop();
				try
				{
					inspect(form);
				}
				catch (Exception ex)
				{
					error = ex;
				}
				finally
				{
					form.Close();
				}
			};
			timer.Start();
			open();
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
			Store store = new Store(Path.Combine(folder, "session"));
			store.Load();
			SimRailSnapshot snap = new SimRailSnapshot
			{
				ServerCode = "test153",
				Trains = new List<SimRailTrain>(),
				DispatcherNames = new HashSet<string>(),
				GameTime = day,
				DownloadedUtc = DateTime.UtcNow
			};
			SimRailSession session = new SimRailSession(null, snap, "60");
			Action<DateTime> action = (DateTime t) =>
			{
				snap.GameTime = t;
				snap.DownloadedUtc = DateTime.UtcNow;
				session.Accept(snap);
			};
			Engine engine = new Engine(store)
			{
				User = store.State.Users.First((LocalUser x) => x.Id == "DEMO"),
				SimRail = session
			};
			engine.User.Name = "Dom";
			StationSettings.Normalize(engine.Station);
			engine.BeginDuty(day.AddMinutes(-10.0));
			int writes = 0;
			engine.SimRailTimeChanged += (SepeWriteRequest r) =>
			{
				writes++;
			};
			Check(TelephoneTemplates.Ids.Length == 22 && TelephoneTemplates.Ids.Length == TelephoneTemplates.Titles.Length && TelephoneTemplates.Ids.Distinct().Count() == 22, "Lista ma 22 różne wzory, w tym 9–12, 16a, 17a, 23–27 i 31–32.");
			string sec = "ZAR";
			Section section = engine.Station.Sections.First((Section x) => x.Id == sec);
			foreach (LineTrackSetting lineTrack in section.LineTracks)
			{
				lineTrack.TrafficType = "Jednokierunkowy";
				string basis = (lineTrack.BasisOut = "Wieloodstępowa (samoczynna) blokada liniowa");
				lineTrack.Basis = basis;
			}
			Check(!engine.CurrentBidirectional(sec, 1) && !engine.CurrentBidirectional(sec, 2), "Ruch jednokierunkowy z konfiguracji ma początkowo pojedyncze strzałki.");
			JournalEntry journalEntry = engine.SaveTelephone(sec, null, Data("23", 1, day));
			Check(journalEntry.Text == "Od JAWORZYNA ŚLĄSKA do ŻARÓW wprowadzam ruch jednotorowy dwukierunkowy po torze numer 1, po którym ostatni pociąg 91918 odjechał o 11:55.", "Wzór 23 zachowuje treść, stacje, tor oraz ostatni pociąg i godzinę.");
			Check(engine.CurrentBidirectional(sec, 1) && !engine.CurrentBidirectional(sec, 2) && engine.CurrentSingleTrackTraffic(sec), "Wzór 23 włącza ruch jednotorowy i dwukierunkowy na wskazanym torze.");
			JournalEntry journalEntry2 = engine.Note(sec, day, "Po torze nr [1]", "Pociąg");
			journalEntry2.Direction = "DO";
			journalEntry2.Number = "123";
			journalEntry2.LineTrack = 1;
			Check(engine.DocumentationFields(journalEntry2).Permission, "Ruch jednotorowy na szlaku dwutorowym wymaga drogi wolnej także na SBL.");
			Check(engine.LineStatus(sec, 2) == "WOLNY", "Wprowadzenie ruchu jednotorowego nie dopisuje fikcyjnego zamknięcia innego toru.");
			TelephoneData telephoneData = Data("25", 1, day);
			telephoneData.EffectiveAt = day.AddMinutes(5.0);
			JournalEntry journalEntry3 = engine.SaveTelephone(sec, null, telephoneData);
			Check(engine.CurrentSingleTrackTraffic(sec) && engine.CurrentBidirectional(sec, 1), "Przyszły wzór 25 nie przywraca ruchu dwutorowego przed czasem.");
			action(day.AddMinutes(5.0));
			Check(!engine.CurrentSingleTrackTraffic(sec) && !engine.CurrentBidirectional(sec, 1), "Wzór 25 przywraca ustawienia zasadnicze obu torów o wskazanej godzinie.");
			engine.Cancel(journalEntry3.Id, "Test");
			Check(engine.CurrentSingleTrackTraffic(sec) && engine.CurrentBidirectional(sec, 1), "Wykreślenie przywrócenia ruchu odtwarza poprzedni stan.");
			engine.Cancel(journalEntry.Id, "Test");
			JournalEntry journalEntry4 = engine.SaveTelephone(sec, null, Data("24", 2, engine.Now));
			Check(journalEntry4.Text == "Wprowadzić ruch jednotorowy dwukierunkowy od JAWORZYNA ŚLĄSKA do ŻARÓW po torze numer 2." && engine.CurrentBidirectional(sec, 2), "Wzór 24 ma odrębną treść polecenia i dotyczy wybranego toru.");
			JournalEntry journalEntry5 = engine.SaveTelephone(sec, null, Data("26", 1, engine.Now));
			Check(journalEntry5.Text.Contains("Przy czynnych obu torach wprowadzam od JAWORZYNA ŚLĄSKA do ŻARÓW ruch dwukierunkowy po torze numer 1") && !engine.CurrentSingleTrackTraffic(sec), "Wzór 26 rozróżnia ruch przy czynnych obu torach od ruchu jednotorowego.");
			JournalEntry journalEntry6 = engine.SaveTelephone(sec, null, Data("27", 1, engine.Now));
			Check(!engine.CurrentBidirectional(sec, 1) && engine.CurrentBidirectional(sec, 2) && journalEntry6.Text.Contains("Przywracam ruch jednokierunkowy po torze numer 1"), "Wzór 27 przywraca jednokierunkowość tylko wskazanego toru.");
			engine.Cancel(journalEntry6.Id, "Test");
			Check(engine.CurrentBidirectional(sec, 1), "Wykreślenie wzoru 27 odtwarza dwukierunkowość.");
			engine.Cancel(journalEntry5.Id, "Test");
			engine.Cancel(journalEntry4.Id, "Test");
			TelephoneData telephoneData2 = Data("31", 2, engine.Now);
			telephoneData2.EffectiveAt = day.AddMinutes(10.0);
			JournalEntry journalEntry7 = engine.SaveTelephone(sec, null, telephoneData2);
			Check(engine.ElectricTractionAllowed(sec, 2), "Przyszłe ograniczenie trakcji nie obowiązuje przed czasem.");
			action(day.AddMinutes(10.0));
			Check(!engine.ElectricTractionAllowed(sec, 2) && engine.ElectricTractionAllowed(sec, 1) && engine.LineStatus(sec, 2) == "WOLNY", "Wzór 31 ogranicza trakcję elektryczną na konkretnym torze, bez zamknięcia dla wszystkich pociągów.");
			Check(journalEntry7.Text == "Tor numer 2 od JAWORZYNA ŚLĄSKA do ŻARÓW od godziny 12 minut 10 nieprzejezdny dla pociągów z trakcją elektryczną.", "Wzór 31 ma brzmienie z przesłanego zestawienia.");
			JournalEntry journalEntry8 = engine.SaveTelephone(sec, null, Data("32", 2, engine.Now));
			Check(engine.ElectricTractionAllowed(sec, 2) && journalEntry8.Text.Contains("przywracam ruch trakcją elektryczną."), "Wzór 32 przywraca ruch trakcją elektryczną.");
			engine.Cancel(journalEntry8.Id, "Test");
			Check(!engine.ElectricTractionAllowed(sec, 2), "Wykreślenie wzoru 32 przywraca poprzednie ograniczenie.");
			engine.SaveTelephone(sec, null, Data("16", 1, engine.Now));
			JournalEntry p17a = engine.SaveTelephone(sec, null, Data("17a", 1, engine.Now));
			Check(p17a.Text.Contains("odwołuję radiotelefoniczne zapowiadanie pociągów") && engine.CurrentBasis(sec, 1) == "Wieloodstępowa (samoczynna) blokada liniowa", "Wzór 17a jest odwołaniem radiotelefonicznego zapowiadania i przywraca podstawę zasadniczą.");
			TelephoneData telephoneData3 = Data("16a", 2, engine.Now);
			telephoneData3.EffectiveAt = day.AddMinutes(15.0);
			JournalEntry journalEntry9 = engine.SaveTelephone(sec, null, telephoneData3);
			Check(journalEntry9.Text == "O 12:15 wprowadzam radiotelefoniczne zapowiadanie pociągów po torze numer 2 od JAWORZYNA ŚLĄSKA do ŻARÓW z powodu usterka." && engine.CurrentBasis(sec, 2) == "Wieloodstępowa (samoczynna) blokada liniowa", "Wzór 16a ma właściwą treść i czeka do godziny obowiązywania.");
			action(day.AddMinutes(15.0));
			Check(engine.CurrentBasis(sec, 2) == "Zapowiadanie radiotelefoniczne" && engine.CurrentBasis(sec, 1) == "Wieloodstępowa (samoczynna) blokada liniowa" && TrafficBasis.IsTelephone(engine.CurrentBasis(sec, 2)), "16a włącza radiotelefoniczne zapowiadanie tylko na właściwym torze.");
			JournalEntry journalEntry10 = engine.Note(sec, engine.Now, "Po torze nr [2]", "Pociąg");
			journalEntry10.Direction = "Z";
			journalEntry10.LineTrack = 2;
			JournalTimeFields journalTimeFields = engine.DocumentationFields(journalEntry10);
			Check(journalTimeFields.Permission && journalTimeFields.Left && journalTimeFields.Arrived, "16a wymaga dokumentowania drogi wolnej i obu godzin przejazdu.");
			JournalEntry journalEntry11 = engine.SaveTelephone(sec, null, Data("17a", 2, engine.Now));
			Check(engine.CurrentBasis(sec, 2) == "Wieloodstępowa (samoczynna) blokada liniowa", "17a odwołuje wprowadzenie przez 16a i przywraca podstawę zasadniczą.");
			engine.Cancel(journalEntry11.Id, "Test");
			Check(engine.CurrentBasis(sec, 2) == "Zapowiadanie radiotelefoniczne", "Wykreślenie 17a przywraca obowiązujący 16a.");
			engine.Cancel(journalEntry9.Id, "Test");
			TelephoneData noCause = Data("16a", 2, engine.Now);
			noCause.Reason = "";
			Reject(() =>
			{
				engine.SaveTelephone(sec, null, noCause);
			}, "Wzór 16a wymaga przyczyny tak samo jak 16.");
			string[] array = new string[4] { "23", "24", "25", "26" };
			foreach (string id in array)
			{
				Reject(() =>
				{
					engine.SaveTelephone("SWM", null, Data(id, 1, engine.Now));
				}, "Wzór " + id + " nie jest zapisywany jako ruch dwutorowy na szlaku jednotorowym.");
			}
			TelephoneData bad = Data("23", 1, engine.Now);
			bad.LastTrainDeparture = null;
			Reject(() =>
			{
				engine.SaveTelephone(sec, null, bad);
			}, "Wzór 23 wymaga godziny odjazdu ostatniego pociągu.");
			bad = Data("26", 1, engine.Now);
			bad.TrainNumber = "";
			Reject(() =>
			{
				engine.SaveTelephone(sec, null, bad);
			}, "Wzór 26 wymaga numeru ostatniego pociągu.");
			TelephoneData telephoneData4 = Data("31", 1, engine.Now);
			telephoneData4.Incoming = true;
			telephoneData4.Sender = "Anna";
			JournalEntry journalEntry12 = engine.SaveTelephone(sec, null, telephoneData4);
			Check(journalEntry12.Text.Contains("od ŻARÓW do JAWORZYNA ŚLĄSKA") && journalEntry12.Telephone.Receiver == "Dom", "Odbierany nowy wzór zachowuje kierunek i dane operatora.");
			Check(writes == 0, "Nowe telefonogramy nie wysyłają godzin pociągów do SEPE.");
			Audit audit = engine.State.History.Last();
			Check(audit.User == "DEMO" && audit.UserName == "Dom" && engine.AuditUserName(audit) == "Dom", "Historia zachowuje identyfikator i nazwę operatora z chwili zmiany.");
			engine.Store.Commit((AppState s) =>
			{
				s.Users.First((LocalUser x) => x.Id == "DEMO").Name = "Inny";
			});
			engine.User = engine.State.Users.First((LocalUser x) => x.Id == "DEMO");
			engine.Change("Kolejna zmiana", "test", (AppState s) =>
			{
			});
			Check(engine.AuditUserName(engine.State.History.First((Audit x) => x.Action == audit.Action && x.Time == audit.Time)) == "Dom", "Późniejsza zmiana nazwy nie zmienia nazwiska autora wcześniejszego wpisu.");
			Check(engine.AuditUserName(new Audit
			{
				User = "DEMO"
			}) == "Inny" && engine.AuditUserName(new Audit
			{
				User = "SYSTEM"
			}) == "SYSTEM", "Starsza historia ma odczyt nazwy konta, a SYSTEM pozostaje rozpoznawalny.");
			engine.User.Name = "Dom";
			MainForm main = new MainForm(engine);
			try
			{
				main.Show();
				main.Size = new Size(1600, 900);
				Application.DoEvents();
				JournalPane journalPane = Field<List<JournalPane>>(main, "panes").Single((JournalPane x) => x.SectionId == sec);
				engine.Station.Sections.First((Section x) => x.Id == sec).LineTracks.First((LineTrackSetting x) => x.Number == 1).TrafficType = "Dwukierunkowy";
				journalPane.RefreshSignals();
				Check(Signal(journalPane, 1).Bidirectional && !Signal(journalPane, 2).Bidirectional, "Nagłówek odczytuje dwukierunkowość z konfiguracji dla numeru toru.");
				engine.SaveTelephone(sec, null, Data("26", 2, engine.Now));
				journalPane.RefreshSignals();
				Check(Signal(journalPane, 1).Bidirectional && Signal(journalPane, 2).Bidirectional, "Nagłówek aktualizuje obie strzałki po telefonogramie bez zmiany zajętości.");
				Ui.Capture(main, Path.Combine(folder, "strzalki-dwukierunkowe.png"));
				float[] array2 = new float[3] { 1000f, 1400f, 1900f };
				for (int num = 0; num < array2.Length; num++)
				{
					float num2 = array2[num];
					main.Size = new Size((int)num2, 900);
					main.ApplyZoom();
					Application.DoEvents();
					TextBox textBox = Field<TextBox>(main, "search");
					SilverButton silverButton = textBox.Parent.Controls.OfType<SilverButton>().Single();
					textBox.Parent.PerformLayout();
					Check(silverButton.Height == textBox.Height && silverButton.Top == textBox.Top, "Przycisk czyszczenia numeru ma wysokość i położenie pola: " + num2);
					textBox.Text = "91918";
					silverButton.PerformClick();
					Check(textBox.Text == "", "Przycisk po wyrównaniu nadal czyści pole: " + num2);
					Ui.Capture(main, Path.Combine(folder, "pole-numeru-" + num2.ToString("0.0") + ".png"));
				}
				Dialog(main, () =>
				{
					main.History(p17a.Id);
				}, (Form f) =>
				{
					DataGridView dataGridView = All(f).OfType<DataGridView>().Single();
					Check(dataGridView.Rows.Count > 0 && dataGridView.Rows.Cast<DataGridViewRow>().All((DataGridViewRow r) => Convert.ToString(r.Cells["user"].Value) == "Dom"), "Okno historii pokazuje Dom zamiast DEMO.");
					Ui.Capture(f, Path.Combine(folder, "historia-operator.png"));
				});
				Dialog(main, () =>
				{
					main.Situation();
				}, (Form f) =>
				{
					List<Label> source = All(f).OfType<Label>().ToList();
					Check(source.Any((Label x) => x.Text.StartsWith("Status:")) && !source.Any((Label x) => x.Text.Contains("kierunkowy") || x.Text.Contains("Blokada") || x.Text.Contains("nieprzejezdny")), "Sytuacja ruchowa pokazuje wyłącznie dane układu ze wzoru.");
					Ui.Capture(f, Path.Combine(folder, "sytuacja-ruchowa.png"));
				});
				array = new string[9] { "16a", "17a", "23", "24", "25", "26", "27", "31", "32" };
				foreach (string text2 in array)
				{
					using TelephoneEditor telephoneEditor = new TelephoneEditor(engine, sec, null);
					telephoneEditor.Show(main);
					Application.DoEvents();
					telephoneEditor.TemplateBox.SelectedIndex = Array.IndexOf(TelephoneTemplates.Ids, text2);
					telephoneEditor.ReceiverBox.Text = "Anna";
					telephoneEditor.TrainBox.Text = "91918";
					telephoneEditor.EventTimeBox.Text = "11:55";
					Application.DoEvents();
					FlowLayoutPanel sentence = Field<FlowLayoutPanel>(telephoneEditor, "sentence");
					Check(sentence.Controls.Count > 4 && sentence.Controls.Cast<Control>().All((Control c) => c.Bottom <= sentence.ClientSize.Height), "Wzór " + text2 + " ma parametry mieszczące się w formularzu.");
					TelephoneData telephoneData5 = (TelephoneData)Call(telephoneEditor, "ReadData");
					Check(telephoneData5.TemplateId == text2 && ((text2 != "23" && text2 != "26") || (telephoneData5.LastTrainDeparture.HasValue && telephoneData5.EffectiveAt == telephoneData5.SentAt)), "Formularz odczytuje właściwe parametry wzoru " + text2 + ".");
					Ui.Capture(telephoneEditor, Path.Combine(folder, "telefonogram-" + text2 + ".png"));
					telephoneEditor.Close();
				}
				main.Close();
			}
			finally
			{
				if (main != null)
				{
					((IDisposable)main).Dispose();
				}
			}
			RailSignal railSignal = new RailSignal();
			railSignal.Bidirectional = true;
			railSignal.Telephone = true;
			railSignal.Incoming = true;
			railSignal.MovementIncoming = true;
			railSignal.Status = "ZAJĘTY: TEST";
			using (RailSignal railSignal2 = railSignal)
			{
				using Bitmap bitmap = new Bitmap(40, 30);
				railSignal2.DrawToBitmap(bitmap, new Rectangle(0, 0, 40, 30));
				Check(bitmap.GetPixel(2, 16).ToArgb() == Color.Gray.ToArgb() && bitmap.GetPixel(2, 24).ToArgb() == Color.Gray.ToArgb() && bitmap.GetPixel(19, 20).ToArgb() == Color.Blue.ToArgb(), "Renderer pokazuje dwa groty zasadnicze obok odrębnej niebieskiej strzałki jazdy.");
				bitmap.Save(Path.Combine(folder, "tor-dwukierunkowy.png"));
			}
			Check(!Directory.Exists(store.DirectoryPath), "Nowe funkcje nie zapisują sesji na dysku.");
			return true;
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			return false;
		}
		finally
		{
			File.WriteAllLines(Path.Combine(folder, "wyniki-ruch153.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

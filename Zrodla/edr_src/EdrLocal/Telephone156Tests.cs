using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace EdrLocal;

public static class Telephone156Tests
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

	private static TelephoneData Data(string id, DateTime at)
	{
		TelephoneData telephoneData = new TelephoneData();
		telephoneData.TemplateId = id;
		telephoneData.Track = 1;
		telephoneData.SentAt = at;
		telephoneData.EffectiveAt = at;
		telephoneData.Receiver = "Anna";
		telephoneData.TrainNumber = "91918";
		telephoneData.Reason = "roboty torowe";
		telephoneData.WorkSupervisorName = "Anna Nowak";
		telephoneData.WorkSupervisorRole = "kierownik robót";
		telephoneData.SpeedRestrictions = "40 km/h w km 10,000–11,000";
		return telephoneData;
	}

	private static TelephoneData Read(TelephoneEditor f)
	{
		return (TelephoneData)typeof(TelephoneEditor).GetMethod("ReadData", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(f, null);
	}

	private static JournalEntry Passage(Engine e, string number, int track)
	{
		JournalEntry journalEntry = e.Note("ZAR", day, "Po torze nr [" + track + "]", "Pociąg");
		journalEntry.Number = number;
		journalEntry.Direction = "Z";
		journalEntry.LineTrack = track;
		journalEntry.Track = "1";
		journalEntry.Left = day;
		journalEntry.Arrived = day.AddMinutes(5.0);
		return journalEntry;
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		LocalPersistence.Enabled = false;
		try
		{
			Check(TelephoneTemplates.Ids.SequenceEqual(new string[22]
			{
				"9", "10", "11", "12", "16", "16a", "17", "17a", "21", "21a",
				"022", "22", "23", "24", "25", "26", "27", "28", "29", "30",
				"31", "32"
			}), "Katalog ma dokładnie 22 pozycje w kolejności ze zdjęć, z 022 między 21a i 22.");
			Check(TelephoneTemplates.Titles[0] == "[9] Żądanie wstrzymania wyprawiania pociągów." && TelephoneTemplates.Titles[13] == "[24] Żądanie wprowadzenia ruchu jednotorowego dwukierunkowego." && TelephoneTemplates.Titles[10] == "[022] Zakończenie robót na torze szlakowym.", "Nazwy żądań i kod 022 są zgodne z pokazaną listą EDR.");
			Store store = new Store(Path.Combine(folder, "session"));
			store.Load();
			SimRailSnapshot snapshot = new SimRailSnapshot
			{
				ServerCode = "test156",
				Trains = new List<SimRailTrain>(),
				DispatcherNames = new HashSet<string>(),
				GameTime = day,
				DownloadedUtc = DateTime.UtcNow
			};
			SimRailSession session = new SimRailSession(null, snapshot, "60");
			Action<DateTime> action = (DateTime t) =>
			{
				snapshot.GameTime = t;
				snapshot.DownloadedUtc = DateTime.UtcNow;
				session.Accept(snapshot);
			};
			Engine engine = new Engine(store)
			{
				SimRail = session
			};
			engine.StartSession(store.State.Users.First((LocalUser x) => x.Id == "DEMO"));
			engine.BeginDuty(day.AddMinutes(-5.0));
			int writes = 0;
			engine.SimRailTimeChanged += (SepeWriteRequest r) =>
			{
				writes++;
			};
			JournalEntry journalEntry = engine.SaveTelephone("ZAR", null, Data("21a", day));
			Check(journalEntry.Text == "Tor numer 1 od JAWORZYNA ŚLĄSKA do ŻARÓW zamknięty po przejeździe pociągu numer 91918 z powodu roboty torowe.", "Treść 21a zastępuje godzinę przejazdem wskazanego pociągu zgodnie z §53 ust.4 Ir-1.");
			Check(engine.LineStatus("ZAR", 1) == "WOLNY", "21a nie zamyka toru od razu po wpisaniu planowanego warunku.");
			action(day.AddMinutes(5.0));
			JournalEntry entry = Passage(engine, "91920", 1);
			engine.SaveJournal(entry);
			Check(engine.LineStatus("ZAR", 1) == "WOLNY", "Przyjazd innego pociągu nie uruchamia zamknięcia 21a.");
			JournalEntry entry2 = Passage(engine, "91918", 2);
			engine.SaveJournal(entry2);
			Check(engine.LineStatus("ZAR", 1) == "WOLNY", "Przejazd po innym torze nie uruchamia zamknięcia.");
			JournalEntry actual = Passage(engine, "91918", 1);
			actual.Arrived = null;
			engine.SaveJournal(actual);
			Check(engine.LineStatus("ZAR", 1).StartsWith("ZAJĘTY"), "Sam odjazd lub zajętość nie udają potwierdzenia przejazdu.");
			actual = Json.Clone(engine.State.Entries.First((JournalEntry x) => x.Id == actual.Id));
			actual.Arrived = day.AddMinutes(5.0);
			engine.SaveJournal(actual);
			Check(engine.LineStatus("ZAR", 1) == "ZAMKNIĘTY", "21a zamyka tor po zapisanym ukończeniu przejazdu właściwego pociągu.");
			engine.Cancel(actual.Id, "Błędny przejazd");
			Check(engine.LineStatus("ZAR", 1) == "WOLNY", "Wykreślenie dowodu przejazdu cofa automatyczny skutek warunkowy.");
			JournalEntry entry3 = Passage(engine, "91918", 1);
			engine.SaveJournal(entry3);
			Check(engine.LineStatus("ZAR", 1) == "ZAMKNIĘTY", "Poprawny ponowny zapis przejazdu spełnia warunek.");
			engine.Cancel(journalEntry.Id, "Błędny telefonogram");
			Check(engine.LineStatus("ZAR", 1) == "WOLNY", "Wykreślenie 21a usuwa jego skutek.");
			action(day.AddMinutes(6.0));
			engine.SaveTelephone("ZAR", null, Data("21", engine.Now));
			JournalEntry journalEntry2 = engine.SaveTelephone("ZAR", null, Data("022", engine.Now));
			Check(journalEntry2.Text.Contains("Roboty na torze 1 szlaku JAWORZYNA ŚLĄSKA – ŻARÓW zostały ukończone o godzinie 12 minut 06") && journalEntry2.Text.Contains("Anna Nowak, kierownik robót.") && journalEntry2.Text.Contains("40 km/h"), "022 zawiera zakończenie robót, ograniczenia oraz nazwisko i stanowisko zawiadamiającego.");
			Check(journalEntry2.Effect == "" && engine.LineStatus("ZAR", 1) == "ZAMKNIĘTY", "Zawiadomienie 022 nie zastępuje otwarcia toru wzorem 22.");
			action(day.AddMinutes(7.0));
			engine.SaveTelephone("ZAR", null, Data("22", engine.Now));
			Check(engine.LineStatus("ZAR", 1) == "WOLNY", "Dopiero odrębny telefonogram 22 otwiera tor.");
			TelephoneData bad = Data("21a", engine.Now);
			bad.TrainNumber = "";
			Reject(() =>
			{
				engine.SaveTelephone("ZAR", null, bad);
			}, "21a wymaga numeru pociągu.");
			bad = Data("21a", engine.Now);
			bad.Reason = "";
			Reject(() =>
			{
				engine.SaveTelephone("ZAR", null, bad);
			}, "21a wymaga przyczyny lub celu.");
			int[] array = new int[3] { 0, 1, 2 };
			foreach (int num2 in array)
			{
				TelephoneData value = Data("022", engine.Now);
				if (num2 == 0)
				{
					value.WorkSupervisorName = "";
				}
				if (num2 == 1)
				{
					value.WorkSupervisorRole = "";
				}
				if (num2 == 2)
				{
					value.SpeedRestrictions = "";
				}
				Reject(() =>
				{
					engine.SaveTelephone("ZAR", null, value);
				}, "022 wymaga danych zawiadamiającego i informacji o ograniczeniach: przypadek " + num2 + ".");
			}
			using (MainForm mainForm = new MainForm(engine))
			{
				mainForm.Show();
				Application.DoEvents();
				string[] array2 = new string[2] { "21a", "022" };
				foreach (string text in array2)
				{
					using TelephoneEditor telephoneEditor = new TelephoneEditor(engine, "ZAR", null);
					telephoneEditor.Show(mainForm);
					telephoneEditor.TemplateBox.SelectedIndex = Array.IndexOf(TelephoneTemplates.Ids, text);
					telephoneEditor.ReceiverBox.Text = "Anna";
					telephoneEditor.TrainBox.Text = "91918";
					telephoneEditor.ReasonBox.Text = "roboty torowe";
					telephoneEditor.EventTimeBox.Text = ((text == "21a") ? "" : "12:06");
					telephoneEditor.WorkNameBox.Text = "Anna Nowak";
					telephoneEditor.WorkRoleBox.Text = "kierownik robót";
					telephoneEditor.SpeedBox.Text = "Brak";
					Application.DoEvents();
					TelephoneData telephoneData = Read(telephoneEditor);
					Check(telephoneData.TemplateId == text && (text != "21a" || telephoneData.EffectiveAt == telephoneData.SentAt), "Formularz " + text + " odczytuje parametry bez dodatkowej, nieobecnej godziny w 21a.");
					JournalEntry journalEntry3 = engine.SaveTelephone("ZAR", null, telephoneData);
					Check(journalEntry3.Telephone.TemplateId == text && (text != "022" || journalEntry3.Text.Contains("Ograniczenia prędkości: Brak")), "Zapis formularza " + text + " zachowuje pełne parametry.");
					FlowLayoutPanel sentence = Field<FlowLayoutPanel>(telephoneEditor, "sentence");
					Check(sentence.Controls.Cast<Control>().All((Control c) => c.Right <= sentence.ClientSize.Width && c.Bottom <= sentence.ClientSize.Height), "Pola wzoru " + text + " mieszczą się w części parametrów.");
					Check(telephoneEditor.Buttons.Bottom <= telephoneEditor.ClientSize.Height, "Przyciski zapisu są w oknie wzoru " + text + ".");
					Ui.Capture(telephoneEditor, Path.Combine(folder, "telefonogram-" + text + ".png"));
					if (text == "022")
					{
						All(telephoneEditor).OfType<CheckBox>().Single((CheckBox x) => x.Text == "Zawiadomienie / Powiadomienie").Checked = true;
						Application.DoEvents();
						Check(telephoneEditor.Buttons.Bottom <= telephoneEditor.ClientSize.Height && telephoneEditor.ClientSize.Height <= Screen.FromControl(telephoneEditor).WorkingArea.Height - 45, "022 z dodatkowym zawiadomieniem zachowuje dostępne przyciski i mieści okno na ekranie.");
						Ui.Capture(telephoneEditor, Path.Combine(folder, "telefonogram-022-powiadomienie.png"));
					}
					telephoneEditor.Close();
				}
				mainForm.Close();
			}
			Check(writes == 0, "21a i 022 nie wysyłają rzeczywistych godzin do SEPE.");
			Check(!Directory.Exists(store.DirectoryPath), "Nowe wzory pozostają wyłącznie w pamięci sesji.");
			return true;
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			return false;
		}
		finally
		{
			File.WriteAllLines(Path.Combine(folder, "wyniki-telefonogramy156.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

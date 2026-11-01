using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace EdrLocal;

public static class Telephone25Tests
{
	private static List<string> log = new List<string>();

	private static void Check(bool ok, string text)
	{
		if (!ok)
		{
			throw new Exception("FAIL: " + text);
		}
		log.Add("PASS: " + text);
	}

	private static object Field(object o, string n)
	{
		return o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
	}

	private static IEnumerable<Control> All(Control c)
	{
		yield return c;
		foreach (Control x in c.Controls)
		{
			foreach (Control item in All(x))
			{
				yield return item;
			}
		}
	}

	private static JournalEntry Add(Engine e, DateTime time, string text)
	{
		JournalEntry entry = e.Note("TEL", time, text, "Wpis");
		e.SaveJournal(entry);
		return e.State.Entries.First((JournalEntry x) => x.Id == entry.Id);
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		try
		{
			Store store = new Store(Path.Combine(folder, "data"));
			store.Load();
			Engine engine = new Engine(store);
			engine.User = store.State.Users.First((LocalUser x) => x.Id == "DEMO");
			Engine engine2 = engine;
			engine2.BeginDuty(engine2.Now.AddHours(-1.0));
			TelephoneData telephoneData = new TelephoneData();
			telephoneData.TemplateId = "OWN";
			telephoneData.SentAt = engine2.Now.AddMinutes(-5.0);
			telephoneData.EffectiveAt = engine2.Now.AddMinutes(-5.0);
			telephoneData.FreeText = "Rozmowa bez odbiorcy";
			telephoneData.Incoming = false;
			TelephoneData telephoneData2 = telephoneData;
			JournalEntry journalEntry = engine2.SaveTelephone("TEL", null, telephoneData2);
			Check(string.IsNullOrEmpty(journalEntry.Telephone.Receiver) && journalEntry.Telephone.Sender == engine2.User.Name, "Własny wpis telefoniczny można zapisać bez odbiorcy.");
			Check(TelephonePresentation.Column(journalEntry) == engine2.User.Name && !TelephonePresentation.JournalLine(journalEntry).Contains("odebrał"), "Brak odbiorcy nie daje pustego ukośnika ani fikcyjnego rozmówcy.");
			telephoneData2.Incoming = true;
			telephoneData2.Sender = "Rozmówca";
			JournalEntry journalEntry2 = engine2.SaveTelephone("TEL", null, telephoneData2);
			Check(journalEntry2.Telephone.Receiver == engine2.User.Name, "Wpis odebrany nadal wskazuje lokalnego odbiorcę.");
			bool ok = false;
			try
			{
				engine2.SaveTelephone(engine2.Station.Sections[0].Id, null, new TelephoneData
				{
					TemplateId = "22",
					SentAt = engine2.Now,
					EffectiveAt = engine2.Now,
					Track = 1,
					Receiver = ""
				});
			}
			catch
			{
				ok = true;
			}
			Check(ok, "Ruchowy telefonogram nadal wymaga rozmówcy.");
			using (MainForm mainForm = new MainForm(engine2))
			{
				mainForm.Show();
				Application.DoEvents();
				using (TelephoneEditor telephoneEditor = new TelephoneEditor(engine2, "TEL", null))
				{
					telephoneEditor.Show(mainForm);
					Application.DoEvents();
					telephoneEditor.FreeBox.Text = "Zapis przez okno bez odbiorcy";
					telephoneEditor.ReceiverBox.Text = "";
					telephoneEditor.Buttons.Controls.OfType<Button>().Single((Button x) => x.Text == "Zapisz").PerformClick();
					Check(telephoneEditor.SavedEntry != null && string.IsNullOrEmpty(telephoneEditor.SavedEntry.Telephone.Receiver), "Formularz zapisuje treść z pustym odbiorcą bez komunikatu błędu.");
				}
				using (StationConfigurationForm stationConfigurationForm = new StationConfigurationForm(engine2))
				{
					stationConfigurationForm.Show(mainForm);
					Application.DoEvents();
					All(stationConfigurationForm).OfType<TabControl>().First().SelectedTab = All(stationConfigurationForm).OfType<TabPage>().Single((TabPage x) => x.Text == "Inne");
					Application.DoEvents();
					Check(All(stationConfigurationForm).OfType<GroupBox>().Any((GroupBox x) => x.Text == "Numeracja dziennika telefonicznego"), "Opcje numeracji są w zakładce Inne.");
					RadioButton radioButton = (RadioButton)Field(stationConfigurationForm, "telephoneAnnual");
					NumericUpDown numericUpDown = (NumericUpDown)Field(stationConfigurationForm, "telephoneNext");
					radioButton.Checked = true;
					numericUpDown.Value = 100m;
					Ui.Capture(stationConfigurationForm, Path.Combine(folder, "numeracja.png"));
					typeof(StationConfigurationForm).GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(stationConfigurationForm, null);
				}
				Check(engine2.Station.Options.TelephoneAnnual && TelephoneNumbering.Next(engine2.State, engine2.Station, engine2.Now) == 100, "Konfiguracja zapisuje tryb roczny i nowy numer 100.");
				mainForm.Dispose();
			}
			JournalEntry hundred = Add(engine2, engine2.Now.AddMinutes(-1.0), "Numer ręczny");
			Check(hundred.TelephoneOrdinal == 100 && TelephoneNumbering.Next(engine2.State, engine2.Station, engine2.Now) == 101, "Następny wpis dostaje ustawiony numer, a licznik przechodzi na 101.");
			JournalEntry journalEntry3 = Json.Clone(hundred);
			journalEntry3.Text = "Poprawiona treść";
			engine2.SaveJournal(journalEntry3);
			Check(engine2.State.Entries.First((JournalEntry x) => x.Id == hundred.Id).TelephoneOrdinal == 100, "Edycja treści zachowuje numer.");
			engine2.Cancel(hundred.Id, "Błędny wpis");
			JournalEntry journalEntry4 = Add(engine2, engine2.Now, "Po wykreśleniu");
			Check(journalEntry4.TelephoneOrdinal == 101, "Wykreślenie nie zwalnia numeru.");
			store.Commit((AppState s) =>
			{
				s.Entries.Clear();
				s.Duties.Clear();
				s.Stations[0].Options.TelephoneAnnual = false;
				s.Stations[0].Options.TelephoneNextPeriod = null;
			});
			engine2.BeginDuty(engine2.Now.AddMinutes(-1.0));
			store.Commit((AppState s) =>
			{
				s.Entries.Clear();
			});
			JournalEntry jan = Add(engine2, new DateTime(2026, 1, 31, 23, 59, 0), "Styczeń");
			JournalEntry journalEntry5 = Add(engine2, new DateTime(2026, 2, 1), "Luty");
			Check(jan.TelephoneOrdinal == 1 && journalEntry5.TelephoneOrdinal == 1, "Numeracja miesięczna resetuje się 1. dnia miesiąca.");
			JournalEntry journalEntry6 = Add(engine2, new DateTime(2026, 2, 1, 0, 1, 0), "Luty drugi");
			Check(journalEntry6.TelephoneOrdinal == 2, "Kolejne wpisy tego samego miesiąca zwiększają licznik.");
			JournalEntry journalEntry7 = Add(engine2, new DateTime(2026, 1, 1), "Dopisany styczeń");
			Check(journalEntry7.TelephoneOrdinal == 2 && engine2.State.Entries.First((JournalEntry x) => x.Id == jan.Id).TelephoneOrdinal == 1, "Wpis z wcześniejszą datą nie przenumerowuje zapisanych pozycji.");
			store.Commit((AppState s) =>
			{
				s.Entries.Clear();
				s.Stations[0].Options.TelephoneAnnual = true;
			});
			JournalEntry journalEntry8 = Add(engine2, new DateTime(2026, 12, 31, 23, 59, 0), "Grudzień");
			JournalEntry journalEntry9 = Add(engine2, new DateTime(2027, 1, 1), "Nowy rok");
			JournalEntry nextMonth = Add(engine2, new DateTime(2027, 2, 1), "Luty roczny");
			int? telephoneOrdinal = journalEntry8.TelephoneOrdinal;
			Check(telephoneOrdinal == 1 && telephoneOrdinal.HasValue && journalEntry9.TelephoneOrdinal == 1 && nextMonth.TelephoneOrdinal == 2, "Numeracja roczna resetuje się 1 stycznia i trwa przez następne miesiące.");
			using (JournalReport journalReport = new JournalReport(engine2, engine2.State.Entries, telephone: true, history: false, null, new DateTime(2027, 1, 1), new DateTime(2027, 12, 31, 23, 59, 59)))
			{
				Check(journalReport.Rows.Any((ReportRow r) => r.Cells.Any((ReportCell c) => c.Column == 0 && c.Text == "2") && r.Cells.Any((ReportCell c) => c.Text == "Luty roczny")), "Wyciąg telefoniczny korzysta z tego samego zapisanego numeru.");
			}
			Store store2 = new Store(store.DirectoryPath);
			store2.Load();
			Check(store2.State.Entries.First((JournalEntry x) => x.Id == nextMonth.Id).TelephoneOrdinal == 2 && store2.State.Stations[0].Options.TelephoneAnnual, "Numery i ustawienie resetu przetrwały ponowne wczytanie.");
			log.Add("ALL TESTS PASSED: " + log.Count);
			File.WriteAllLines(Path.Combine(folder, "wyniki-telefoniczny25.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
			return true;
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			File.WriteAllLines(Path.Combine(folder, "wyniki-telefoniczny25.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
			return false;
		}
	}
}

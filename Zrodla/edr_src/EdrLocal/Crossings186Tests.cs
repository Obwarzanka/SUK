using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace EdrLocal;

public static class Crossings186Tests
{
	private static readonly List<string> log = new List<string>();

	[DllImport("user32.dll")]
	private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

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

	private static JournalEntry Saved(Engine e, JournalEntry row)
	{
		return Json.Clone(e.State.Entries.Single((JournalEntry x) => x.Id == row.Id));
	}

	private static DataGridViewRow Row(JournalPane pane, JournalEntry entry)
	{
		return pane.Grid.Rows.Cast<DataGridViewRow>().Single((DataGridViewRow x) => ((JournalEntry)x.Tag).Id == entry.Id);
	}

	private static string Col(CrossingSetting c)
	{
		return "crossing_" + c.Id;
	}

	private static void Click(DataGridView grid, DataGridViewCell cell)
	{
		grid.Focus();
		grid.FirstDisplayedScrollingColumnIndex = cell.ColumnIndex;
		Rectangle cellDisplayRectangle = grid.GetCellDisplayRectangle(cell.ColumnIndex, cell.RowIndex, cutOverflow: false);
		IntPtr lParam = new IntPtr((cellDisplayRectangle.Top + cellDisplayRectangle.Height / 2 << 16) | (cellDisplayRectangle.Left + cellDisplayRectangle.Width / 2));
		SendMessage(grid.Handle, 513, new IntPtr(1), lParam);
		SendMessage(grid.Handle, 514, IntPtr.Zero, lParam);
		Application.DoEvents();
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		bool enabled = LocalPersistence.Enabled;
		LocalPersistence.Enabled = false;
		try
		{
			Store store = new Store(Path.Combine(folder, "memory"));
			store.Load();
			Engine engine = new Engine(store);
			engine.User = store.State.Users.First((LocalUser x) => x.Id == "DEMO");
			Engine engine2 = engine;
			Section section = engine2.Station.Sections.First();
			DateTime now = engine2.Now;
			DateTime date = now.Date;
			CrossingSetting crossingSetting = new CrossingSetting();
			crossingSetting.Id = "mark";
			crossingSetting.Name = "Dróżnik pierwszy";
			crossingSetting.Mark = "D1";
			crossingSetting.Kilometre = "2,340";
			crossingSetting.AlongTrack = "Jednotorowych";
			crossingSetting.NotificationMode = "Godzina";
			crossingSetting.Trains = "Wszystkich";
			crossingSetting.InJournal = true;
			CrossingSetting crossingSetting2 = crossingSetting;
			CrossingSetting crossingSetting3 = new CrossingSetting();
			crossingSetting3.Id = "hour";
			crossingSetting3.Name = "Dróżnik drugi";
			crossingSetting3.Mark = "D2";
			crossingSetting3.Kilometre = "4,400";
			crossingSetting3.AlongTrack = "Dwutorowych";
			crossingSetting3.NotificationMode = "Znak";
			crossingSetting3.Trains = "Wszystkich";
			crossingSetting3.InJournal = true;
			CrossingSetting crossingSetting4 = crossingSetting3;
			CrossingSetting crossingSetting5 = new CrossingSetting();
			crossingSetting5.Id = "odd";
			crossingSetting5.Name = "Dróżnik nieparzysty";
			crossingSetting5.Kilometre = "5,000";
			crossingSetting5.AlongTrack = "Jednotorowych";
			crossingSetting5.NotificationMode = "Znak";
			crossingSetting5.Trains = "Nieparzystych";
			crossingSetting5.InJournal = true;
			CrossingSetting crossingSetting6 = crossingSetting5;
			CrossingSetting crossingSetting7 = new CrossingSetting();
			crossingSetting7.Id = "hidden";
			crossingSetting7.Name = "Dróżnik ukryty";
			crossingSetting7.Kilometre = "6,000";
			crossingSetting7.AlongTrack = "Jednotorowych";
			crossingSetting7.NotificationMode = "Znak";
			crossingSetting7.Trains = "Wszystkich";
			crossingSetting7.InJournal = false;
			crossingSetting7.RecordClosure = true;
			CrossingSetting crossingSetting8 = crossingSetting7;
			section.Crossings = new List<CrossingSetting> { crossingSetting2, crossingSetting4, crossingSetting6, crossingSetting8 };
			section.NotifyCrossings = true;
			StationSettings.Normalize(engine2.Station);
			engine2.BeginDuty(now.AddHours(-1.0));
			JournalEntry journalEntry = engine2.Note(section.Id, now, "", "Pociąg");
			journalEntry.Direction = "DO";
			journalEntry.Number = "92804";
			journalEntry.Track = engine2.Station.Tracks.First();
			engine2.SaveJournal(journalEntry);
			Check(!CrossingRules.UsesTime(section, crossingSetting2) && CrossingRules.UsesTime(section, crossingSetting4), "Jak dla szlaków wyznacza ukośnik lub godzinę niezależnie od starego pola i faktycznej liczby torów.");
			Check(CrossingRules.Required(journalEntry, section, crossingSetting2) && !CrossingRules.Required(journalEntry, section, crossingSetting6), "Wymagania uwzględniają parzystość konkretnego numeru pociągu.");
			JournalEntry journalEntry2 = Json.Clone(journalEntry);
			journalEntry2.Direction = "Z";
			Check(!CrossingRules.Required(journalEntry2, section, crossingSetting2), "Powiadomienia dotyczą wiersza wyprawienia, nie przyjęcia.");
			journalEntry2.Direction = "DO";
			journalEntry2.Cancelled = true;
			Check(!CrossingRules.Required(journalEntry2, section, crossingSetting2), "Wykreślony wiersz nie wymaga powiadomień.");
			journalEntry2.Cancelled = false;
			journalEntry2.ReturnOfId = journalEntry.Id;
			Check(!CrossingRules.Required(journalEntry2, section, crossingSetting2), "Wiersz powrotu nie powtarza powiadomień wyprawienia.");
			JournalEntry journalEntry3 = Json.Clone(journalEntry);
			journalEntry3.Notification = crossingSetting2.Name + ": /; " + crossingSetting4.Name + ": 07:53";
			Check(CrossingRules.Value(journalEntry3, section, crossingSetting2) == "/" && CrossingRules.Value(journalEntry3, section, crossingSetting4) == "07:53", "Dawny zbiorczy zapis odczytuje się oddzielnie dla obu dróżników.");
			CrossingRules.Set(journalEntry3, section, crossingSetting2, "", null);
			Check(CrossingRules.Value(journalEntry3, section, crossingSetting2) == "" && CrossingRules.Value(journalEntry3, section, crossingSetting4) == "07:53", "Usunięcie pierwszego powiadomienia zachowuje drugie przy migracji starego zapisu.");
			Check(CrossingRules.Visible(section).Count == 3 && CrossingRules.Label(crossingSetting2) == "D1", "Widoczność i oznaczenie dróżnika w nagłówku pochodzą z konfiguracji.");
			using (MainForm mainForm = new MainForm(engine2))
			{
				mainForm.Show();
				Application.DoEvents();
				JournalPane journalPane = Field<List<JournalPane>>(mainForm, "panes").Single((JournalPane x) => x.SectionId == section.Id);
				journalPane.FocusEntry(journalEntry.Id);
				DataGridView grid = journalPane.Grid;
				DataGridViewRow dataGridViewRow = Row(journalPane, journalEntry);
				Check(!grid.Columns["notify"].Visible && grid.Columns.Cast<DataGridViewColumn>().Count((DataGridViewColumn x) => x.Tag is CrossingSetting) == 3, "Trzech widocznych dróżników ma trzy osobne podkolumny zamiast wspólnego pola.");
				Check(dataGridViewRow.Cells[Col(crossingSetting2)] is SlashCell && dataGridViewRow.Cells[Col(crossingSetting4)] is ClockCell, "Każdy dróżnik ma właściwy typ edytora w tym samym wierszu.");
				Check(dataGridViewRow.Cells[Col(crossingSetting2)].Style.BackColor == EdrPalette.Required && dataGridViewRow.Cells[Col(crossingSetting4)].Style.BackColor == EdrPalette.Required && dataGridViewRow.Cells[Col(crossingSetting6)].ReadOnly, "Niebieskie pola wskazują wymagane powiadomienia; pozostały dróżnik jest tylko do odczytu.");
				Click(grid, dataGridViewRow.Cells[Col(crossingSetting2)]);
				Check(grid.EndEdit() && journalPane.CommitPending(), "Zaznaczenie dróżnika w komórce zapisuje się bez błędu.");
				Check(CrossingRules.Value(Saved(engine2, journalEntry), section, crossingSetting2) == "/" && CrossingRules.Value(Saved(engine2, journalEntry), section, crossingSetting4) == "", "Kliknięcie pierwszego dróżnika nie powiadamia drugiego.");
				grid.CurrentCell = Row(journalPane, journalEntry).Cells[Col(crossingSetting2)];
				grid.Focus();
				SendMessage(grid.Handle, 256, new IntPtr(32), IntPtr.Zero);
				SendMessage(grid.Handle, 257, new IntPtr(32), IntPtr.Zero);
				Check(grid.EndEdit() && journalPane.CommitPending() && CrossingRules.Value(Saved(engine2, journalEntry), section, crossingSetting2) == "", "Spacja pozwala usunąć powiadomienie pierwszego dróżnika.");
				Click(grid, Row(journalPane, journalEntry).Cells[Col(crossingSetting2)]);
				grid.EndEdit();
				journalPane.CommitPending();
				dataGridViewRow = Row(journalPane, journalEntry);
				Click(grid, dataGridViewRow.Cells[Col(crossingSetting4)]);
				Check(grid.EditingControl is ClockEditingControl && Convert.ToString(((ClockEditingControl)grid.EditingControl).EditingControlFormattedValue) == "" && ((ClockEditingControl)grid.EditingControl).SuggestionText == engine2.Now.ToString("HH:mm"), "Pierwsze kliknięcie pustej godziny dróżnika pokazuje niezapisaną sugestię bieżącego czasu.");
				((TextBox)grid.EditingControl).Text = "07:53";
				Check(grid.EndEdit() && journalPane.CommitPending(), "Godzina drugiego dróżnika przechodzi zwykły zapis wiersza.");
				dataGridViewRow = Row(journalPane, journalEntry);
				grid.CurrentCell = dataGridViewRow.Cells["notes"];
				grid.BeginEdit(selectAll: true);
				((TextBox)grid.EditingControl).Text = "Uwagi po powiadomieniu";
				Check(grid.EndEdit() && journalPane.CommitPending(), "Edycja uwag po powiadomieniu kilku dróżników nie waliduje zbiorczego tekstu jak godziny.");
				JournalEntry journalEntry4 = Saved(engine2, journalEntry);
				Check(CrossingRules.Value(journalEntry4, section, crossingSetting2) == "/" && CrossingRules.Value(journalEntry4, section, crossingSetting4) == "07:53" && journalEntry4.Text == "Uwagi po powiadomieniu", "Zapis uwag zachowuje niezależne powiadomienia.");
				journalPane.RefreshData();
				dataGridViewRow = Row(journalPane, journalEntry);
				Check(Convert.ToString(dataGridViewRow.Cells[Col(crossingSetting4)].Value) == "07:53" && dataGridViewRow.Cells[Col(crossingSetting4)].Style.BackColor == Color.White, "Odświeżenie odtwarza godzinę i usuwa niebieskie oznaczenie już wypełnionego pola.");
				grid.CurrentCell = dataGridViewRow.Cells["notes"];
				grid.BeginEdit(selectAll: true);
				((TextBox)grid.EditingControl).Text = "W trakcie edycji";
				JournalEntry journalEntry5 = engine2.Note(section.Id, engine2.Now, "", "Pociąg");
				journalEntry5.Direction = "DO";
				journalEntry5.Number = "92805";
				journalEntry5.Track = journalEntry.Track;
				engine2.SaveJournal(journalEntry5);
				journalPane.RefreshData();
				Check(Row(journalPane, journalEntry5).Cells.Count == grid.Columns.Count && Row(journalPane, journalEntry5).Cells[Col(crossingSetting6)].Style.BackColor == EdrPalette.Required, "Scalanie nowego nieparzystego wiersza podczas edycji zachowuje wszystkie kolumny dróżników.");
				grid.EndEdit();
				Check(journalPane.CommitPending(), "Odświeżenie podczas edycji nie blokuje zapisu uwag.");
				grid.CurrentCell = Row(journalPane, journalEntry).Cells["notes"];
				grid.EndEdit();
				journalPane.CommitPending();
				journalPane.Width = 1000;
				grid.HorizontalScrollingOffset = 0;
				Application.DoEvents();
				using (Bitmap bitmap = new Bitmap(journalPane.Width, journalPane.Height))
				{
					journalPane.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
					bitmap.Save(Path.Combine(folder, "dziennik-droznikow.png"));
				}
				mainForm.Close();
			}
			string text = Json.Text(Saved(engine2, journalEntry));
			using (CrossingNotificationForm crossingNotificationForm = new CrossingNotificationForm(engine2, Saved(engine2, journalEntry), section))
			{
				crossingNotificationForm.Show();
				Application.DoEvents();
				crossingNotificationForm.NotifyAll();
				Check(Json.Text(Saved(engine2, journalEntry)) == text, "Powiadom wszystkich zmienia szkic, bez zapisu przed OK.");
				Check(All(crossingNotificationForm).OfType<CheckBox>().Count((CheckBox x) => x.Text == "Powiadomiono") == 3, "Okno obejmuje właściwych dróżników, także ukrytego w tabeli, i pomija nieparzystego.");
				using (Bitmap bitmap2 = new Bitmap(crossingNotificationForm.Width, crossingNotificationForm.Height))
				{
					crossingNotificationForm.DrawToBitmap(bitmap2, new Rectangle(Point.Empty, bitmap2.Size));
					bitmap2.Save(Path.Combine(folder, "powiadomienia-okno.png"));
				}
				((Button)crossingNotificationForm.AcceptButton).PerformClick();
				Application.DoEvents();
			}
			JournalEntry entry = Saved(engine2, journalEntry);
			Check(CrossingRules.Value(entry, section, crossingSetting2) == "/" && CrossingRules.Value(entry, section, crossingSetting4) == "07:53" && CrossingRules.Value(entry, section, crossingSetting8) == "/", "Powiadom wszystkich zachowuje istniejącą godzinę i zapisuje osobno właściwych dróżników.");
			Check(CrossingRules.Find(entry, section, crossingSetting8).RecordedAt.HasValue, "Dodatkowa godzina ukrytego dróżnika jest zapisana niezależnie od ukośnika.");
			CrossingRules.Set(entry, section, crossingSetting8, "", null);
			engine2.SaveJournal(entry);
			using (CrossingNotificationForm crossingNotificationForm2 = new CrossingNotificationForm(engine2, Saved(engine2, journalEntry), section))
			{
				CheckBox checkBox = All(crossingNotificationForm2).OfType<CheckBox>().Last((CheckBox x) => x.Text == "Powiadomiono");
				checkBox.Checked = true;
				crossingNotificationForm2.SaveNotifications();
			}
			entry = Saved(engine2, journalEntry);
			Check(CrossingRules.Value(entry, section, crossingSetting8) == "/" && !CrossingRules.Find(entry, section, crossingSetting8).RecordedAt.HasValue, "Dodatkowa godzina jest opcjonalna: sam znacznik pojedynczego dróżnika nie wymaga czasu.");
			text = Json.Text(entry);
			using (CrossingNotificationForm crossingNotificationForm3 = new CrossingNotificationForm(engine2, entry, section))
			{
				crossingNotificationForm3.NotifyAll();
			}
			Check(Json.Text(Saved(engine2, journalEntry)) == text, "Zamknięcie okna bez OK nie zmienia zapisanych powiadomień.");
			CrossingRules.Set(entry, section, crossingSetting4, "", null);
			engine2.SaveJournal(entry);
			using (CrossingNotificationForm crossingNotificationForm4 = new CrossingNotificationForm(engine2, Saved(engine2, journalEntry), section))
			{
				crossingNotificationForm4.NotifyAll();
				crossingNotificationForm4.SaveNotifications();
			}
			Check(CrossingRules.Value(Saved(engine2, journalEntry), section, crossingSetting4) == engine2.Now.ToString("HH:mm"), "Powiadom wszystkich uzupełnia brakującą godzinę bieżącym czasem.");
			JournalEntry journalEntry6 = Json.Read<JournalEntry>(Encoding.UTF8.GetBytes(Json.Text(Saved(engine2, journalEntry))));
			Check(CrossingRules.Value(journalEntry6, section, crossingSetting8) == "/" && journalEntry6.CrossingNotifications.Select((CrossingNotification x) => x.CrossingId).Distinct().Count() == 4, "Serializacja zachowuje osobne identyfikatory i dodatkowe godziny dróżników.");
			using (JournalReport journalReport = new JournalReport(engine2, new List<JournalEntry> { journalEntry6 }, telephone: false, history: false, section, date, date.AddDays(1.0)))
			{
				List<ReportCell> list = journalReport.Rows.Single((ReportRow x) => !x.Day).Cells.Where((ReportCell x) => x.Column >= 10).ToList();
				Check(list.Count == 3 && list[0].Text == "/" && list[1].Text == engine2.Now.ToString("HH:mm"), "Wyciąg ma oddzielne komórki widocznych dróżników, bez ukrytego.");
				using (Bitmap bitmap3 = new Bitmap(827, 1169))
				{
					using (Graphics g = Graphics.FromImage(bitmap3))
					{
						journalReport.DrawPage(g, 0);
					}
					bitmap3.Save(Path.Combine(folder, "wyciag-droznikow.png"));
				}
				string path = Path.Combine(folder, "droznikow.csv");
				LocalPersistence.Enabled = true;
				try
				{
					journalReport.Export(path);
				}
				finally
				{
					LocalPersistence.Enabled = false;
				}
				string[] array = File.ReadAllLines(path);
				Check(array[0].Contains("Dróżnik D1") && array[0].Contains("Dróżnik D2") && array[0].Contains("Dróżnik 5,000"), "CSV ma osobne podpisane kolumny dla wszystkich widocznych dróżników.");
			}
			using (JournalReport journalReport2 = new JournalReport(engine2, new List<JournalEntry> { journalEntry6 }, telephone: false, history: true, section, date, date.AddDays(1.0)))
			{
				using (Bitmap image = new Bitmap(827, 1169))
				{
					using Graphics g2 = Graphics.FromImage(image);
					journalReport2.DrawPage(g2, 0);
				}
				Check(journalReport2.Widths.Length == 14, "Wyciąg z historią zachowuje dodatkową kolumnę zmiany przy trzech dróżnikach.");
			}
			Section section2 = Json.Clone(section);
			section2.Crossings = new List<CrossingSetting> { crossingSetting2 };
			JournalEntry journalEntry7 = Json.Clone(journalEntry);
			journalEntry7.Notification = "/";
			journalEntry7.CrossingNotifications = null;
			Check(CrossingRules.Value(journalEntry7, section2, crossingSetting2) == "/", "Pojedynczy dawny ukośnik jest nadal odczytywany.");
			section2.Crossings.Add(Json.Clone(crossingSetting2));
			section2.Crossings[0].Id = null;
			section2.Crossings[1].Id = null;
			Station station = Json.Clone(engine2.Station);
			station.Sections = new List<Section> { section2 };
			StationSettings.Normalize(station);
			Check(section2.Crossings.All((CrossingSetting x) => !string.IsNullOrEmpty(x.Id)) && section2.Crossings.Select((CrossingSetting x) => x.Id).Distinct().Count() == 2, "Starsze konfiguracje otrzymują trwałe, różne identyfikatory dróżników.");
			log.Add("Bez sieci, zapisów do SEPE i zmian w danych użytkownika.");
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			File.WriteAllLines(Path.Combine(folder, "wyniki-droznikow186.txt"), log, Encoding.UTF8);
			return false;
		}
		finally
		{
			LocalPersistence.Enabled = enabled;
		}
		File.WriteAllLines(Path.Combine(folder, "wyniki-droznikow186.txt"), log, Encoding.UTF8);
		return true;
	}
}

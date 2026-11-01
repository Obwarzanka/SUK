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

public static class Clock184Tests
{
	private static readonly List<string> log = new List<string>();

	private static readonly DateTime day = new DateTime(2026, 10, 24);

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

	private static T Field<T>(object o, string n)
	{
		return (T)o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
	}

	private static void Click(DataGridView grid, DataGridViewCell cell)
	{
		Rectangle cellDisplayRectangle = grid.GetCellDisplayRectangle(cell.ColumnIndex, cell.RowIndex, cutOverflow: false);
		IntPtr lParam = new IntPtr((cellDisplayRectangle.Top + cellDisplayRectangle.Height / 2 << 16) | (cellDisplayRectangle.Left + cellDisplayRectangle.Width / 2));
		grid.Focus();
		SendMessage(grid.Handle, 513, new IntPtr(1), lParam);
		SendMessage(grid.Handle, 514, IntPtr.Zero, lParam);
		Application.DoEvents();
	}

	private static void Accept(TextBox box)
	{
		SendMessage(box.Handle, 515, new IntPtr(1), new IntPtr((box.Height / 2 << 16) | (box.Width / 2)));
		SendMessage(box.Handle, 514, IntPtr.Zero, new IntPtr((box.Height / 2 << 16) | (box.Width / 2)));
		Application.DoEvents();
	}

	private static void Clock(Engine engine, int minute)
	{
		SimRailSnapshot snapshot = engine.SimRail.Snapshot;
		snapshot.GameTime = day.AddHours(7.0).AddMinutes(minute);
		snapshot.DownloadedUtc = DateTime.UtcNow;
		engine.SimRail.Accept(snapshot);
	}

	private static void Dialogs(string folder)
	{
		DateTime current = day.AddHours(8.0).AddMinutes(20.0);
		Form form = new Form();
		form.Width = 420;
		form.Height = 240;
		using Form form2 = form;
		Button button = new Button();
		button.Text = "Inne pole";
		button.Top = 140;
		Button button2 = button;
		form2.Controls.Add(button2);
		ReferenceTimeInput referenceTimeInput = new ReferenceTimeInput(null, day.AddDays(-1.0), blue: false, () => current);
		referenceTimeInput.Top = 10;
		ReferenceTimeInput referenceTimeInput2 = referenceTimeInput;
		ReferenceTimeInput referenceTimeInput3 = new ReferenceTimeInput(null, day, blue: false, () => current);
		referenceTimeInput3.Top = 50;
		ReferenceTimeInput referenceTimeInput4 = referenceTimeInput3;
		ReferenceTimeInput referenceTimeInput5 = new ReferenceTimeInput(day.AddHours(6.0).AddMinutes(3.0), day, blue: false, () => current);
		referenceTimeInput5.Top = 90;
		ReferenceTimeInput referenceTimeInput6 = referenceTimeInput5;
		form2.Controls.Add(referenceTimeInput2);
		form2.Controls.Add(referenceTimeInput4);
		form2.Controls.Add(referenceTimeInput6);
		form2.ActiveControl = button2;
		form2.Show();
		button2.Focus();
		Application.DoEvents();
		Check(ClockMask.Value(referenceTimeInput2.Input.Text) == "" && !referenceTimeInput2.Active.Checked, "Samo utworzenie formularza nie wypełnia nieaktywnego pola czasu.");
		current = day.AddHours(8.0).AddMinutes(21.0);
		referenceTimeInput2.Input.Focus();
		Application.DoEvents();
		Check(ClockMask.Value(referenceTimeInput2.Input.Text) == "" && !referenceTimeInput2.Active.Checked, "Samo wejście w pusty czas pozostawia niezatwierdzoną sugestię.");
		Accept(referenceTimeInput2.Input);
		Check(referenceTimeInput2.Input.Text == "08:21" && referenceTimeInput2.Active.Checked && referenceTimeInput2.Value == current, "Dwuklik w pusty czas formularza przyjmuje bieżący zegar i aktywuje wartość.");
		Check(ClockMask.Value(referenceTimeInput4.Input.Text) == "" && !referenceTimeInput4.Active.Checked, "Pozostałe puste godziny formularza nie są uzupełniane razem z wybranym polem.");
		current = day.AddHours(8.0).AddMinutes(23.0);
		referenceTimeInput6.Input.Focus();
		Application.DoEvents();
		Check(referenceTimeInput6.Input.Text == "06:03" && referenceTimeInput6.Value == day.AddHours(6.0).AddMinutes(3.0), "Już wpisana godzina formularza nie jest nadpisywana.");
		referenceTimeInput2.Input.Focus();
		referenceTimeInput2.Input.Text = "";
		button2.Focus();
		Application.DoEvents();
		Check(!referenceTimeInput2.Active.Checked && !referenceTimeInput2.Value.HasValue, "Usunięcie proponowanej godziny pozostaje możliwe.");
		current = day.AddHours(8.0).AddMinutes(24.0);
		referenceTimeInput2.Input.Focus();
		Application.DoEvents();
		Accept(referenceTimeInput2.Input);
		Check(referenceTimeInput2.Input.Text == "08:24" && referenceTimeInput2.Value == current, "Nowe wejście w ponownie puste pole pobiera nowy czas.");
		referenceTimeInput4.Input.ContextMenuStrip.Items.Clear();
		referenceTimeInput4.Input.ContextMenuStrip.Show(form2, new Point(10, 80));
		Application.DoEvents();
		referenceTimeInput4.Input.ContextMenuStrip.Items[1].PerformClick();
		referenceTimeInput4.Input.ContextMenuStrip.Close();
		button2.Focus();
		current = day.AddDays(1.0).AddMinutes(1.0);
		referenceTimeInput4.Input.Focus();
		Application.DoEvents();
		Accept(referenceTimeInput4.Input);
		Check(referenceTimeInput4.Value == day.AddDays(-1.0).AddMinutes(1.0), "Wybrany ręcznie dzień jest zachowany przy podpowiedzi bieżącej godziny.");
		form2.Close();
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		LocalPersistence.Enabled = false;
		try
		{
			SimRailTrain simRailTrain = new SimRailTrain();
			simRailTrain.trainNoLocal = "561113";
			simRailTrain.startStation = "A";
			simRailTrain.endStation = "C";
			simRailTrain.trainName = "TME";
			simRailTrain.timetable = new List<SimRailPoint>();
			SimRailTrain simRailTrain2 = simRailTrain;
			for (int i = 0; i < 3; i++)
			{
				simRailTrain2.timetable.Add(new SimRailPoint
				{
					pointId = (10 + i).ToString(),
					nameForPerson = (new string[3] { "A", "B", "C" })[i],
					indexOfPoint = i,
					line = 14,
					track = 1,
					displayedTrainNumber = "561113",
					trainType = "TME",
					isActive = true,
					arrivalTime = "2026-10-24 07:00:00",
					departureTime = "2026-10-24 07:01:00"
				});
			}
			SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
			simRailSnapshot.ServerCode = "fixture184";
			simRailSnapshot.Trains = new List<SimRailTrain> { simRailTrain2 };
			simRailSnapshot.DispatcherNames = new HashSet<string> { "A", "B", "C" };
			simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
			simRailSnapshot.GameTime = day.AddHours(7.0).AddMinutes(10.0);
			SimRailSnapshot snapshot = simRailSnapshot;
			SimRailSession simRailSession = new SimRailSession(null, snapshot, "11");
			Station station = simRailSession.SuggestStation();
			StationSettings.Normalize(station);
			Store store = new Store(Path.Combine(folder, "memory"));
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
			engine.SimRail = simRailSession;
			engine.User = store.State.Users.First((LocalUser x) => x.Id == "DEMO");
			Engine engine2 = engine;
			engine2.ApplySimRailSnapshot(snapshot);
			engine2.BeginDuty(day.AddHours(6.0));
			Train train = engine2.State.Trains.Single();
			JournalEntry entry = engine2.Note(station.Sections.First((Section x) => x.Name == train.From).Id, engine2.Now, "", "Pociąg");
			entry.TrainId = train.Id;
			entry.Direction = "Z";
			entry.Number = train.Number;
			entry.Track = "1";
			engine2.SaveJournal(entry);
			int writes = 0;
			engine2.SimRailTimeChanged += (SepeWriteRequest r) =>
			{
				writes++;
			};
			using (MainForm mainForm = new MainForm(engine2))
			{
				mainForm.Show();
				Application.DoEvents();
				JournalPane journalPane = Field<List<JournalPane>>(mainForm, "panes").Single((JournalPane x) => x.SectionId == entry.SectionId);
				journalPane.FocusEntry(entry.Id);
				DataGridView grid = journalPane.Grid;
				DataGridViewRow dataGridViewRow = grid.Rows.Cast<DataGridViewRow>().Single((DataGridViewRow x) => ((JournalEntry)x.Tag).Id == entry.Id);
				grid.CurrentCell = dataGridViewRow.Cells["permission"];
				if (!grid.IsCurrentCellInEditMode)
				{
					grid.BeginEdit(selectAll: true);
				}
				Check(((TextBox)grid.EditingControl).Text == "__:__" && writes == 0, "Programowe zaznaczenie komórki nie tworzy samo godziny ani wysyłki.");
				grid.CancelEdit();
				int num = 10;
				string[] array = new string[3] { "permission", "left", "arrived" };
				foreach (string text in array)
				{
					Clock(engine2, num);
					Click(grid, dataGridViewRow.Cells[text]);
					ClockEditingControl clockEditingControl = grid.EditingControl as ClockEditingControl;
					Check(clockEditingControl != null && clockEditingControl.IsSuggestion && Convert.ToString(clockEditingControl.EditingControlFormattedValue) == "" && clockEditingControl.SuggestionText == "07:" + num, "Pierwsze kliknięcie pokazuje niezapisaną sugestię czasu serwera: " + text + " (" + ((clockEditingControl == null) ? "brak edytora" : clockEditingControl.Text) + ", " + ((grid.CurrentCell == null) ? "brak komórki" : grid.CurrentCell.OwningColumn.Name) + ").");
					Check(writes == 0, "Przed zatwierdzeniem podpowiedzi nie ma wysyłki: " + text + ".");
					Accept(clockEditingControl);
					Check(grid.EndEdit() && journalPane.CommitPending(), "Dwuklik zatwierdza proponowaną godzinę: " + text + ".");
					num++;
				}
				Check(writes == 1 && engine2.State.Trains.Single().Arrival == day.AddHours(7.0).AddMinutes(12.0), "Tylko zatwierdzony własny przyjazd synchronizuje godzinę pociągu.");
				Clock(engine2, 15);
				Click(grid, dataGridViewRow.Cells["arrived"]);
				Check(((TextBox)grid.EditingControl).Text == "07:12", "Ponowne kliknięcie zapisanej godziny nie zmienia jej na aktualną.");
				grid.EndEdit();
				journalPane.CommitPending();
				Check(writes == 1, "Samo ponowne wejście w istniejącą godzinę nie tworzy drugiego zlecenia.");
				grid.CurrentCell = dataGridViewRow.Cells["permission"];
				if (!grid.IsCurrentCellInEditMode)
				{
					grid.BeginEdit(selectAll: true);
				}
				((TextBox)grid.EditingControl).Text = "";
				grid.EndEdit();
				journalPane.CommitPending();
				Clock(engine2, 16);
				Click(grid, dataGridViewRow.Cells["permission"]);
				Check(((ClockEditingControl)grid.EditingControl).SuggestionText == "07:16" && Convert.ToString(((ClockEditingControl)grid.EditingControl).EditingControlFormattedValue) == "", "Po usunięciu godziny kolejne kliknięcie używa nowego czasu.");
				grid.CancelEdit();
				Check(ClockMask.Value(Convert.ToString(dataGridViewRow.Cells["permission"].Value)) == "" && !engine2.State.Entries.Single((JournalEntry x) => x.Id == entry.Id).Permission.HasValue, "Anulowanie podpowiedzi pozostawia poprzednią pustą wartość.");
				engine2.ReadOnlySession = true;
				grid.CurrentCell = dataGridViewRow.Cells["notes"];
				Click(grid, dataGridViewRow.Cells["permission"]);
				Check(!grid.IsCurrentCellInEditMode && ClockMask.Value(Convert.ToString(dataGridViewRow.Cells["permission"].Value)) == "", "Tryb podglądu nie uzupełnia pustej godziny.");
				engine2.ReadOnlySession = false;
				Ui.Capture(mainForm, Path.Combine(folder, "godziny-z-dziennika.png"));
				mainForm.Close();
			}
			Dialogs(folder);
			Check(!Directory.Exists(store.DirectoryPath), "Sprawdzenia działają lokalnie bez trwałego zapisu sesji.");
			log.Add("ALL OFFLINE CLOCK184 TESTS PASSED");
			return true;
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			return false;
		}
		finally
		{
			File.WriteAllLines(Path.Combine(folder, "wyniki-godziny184.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

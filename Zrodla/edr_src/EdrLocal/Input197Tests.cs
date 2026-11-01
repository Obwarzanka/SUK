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

public static class Input197Tests
{
	private static readonly List<string> log = new List<string>();

	private static readonly DateTime now = new DateTime(2026, 10, 25, 0, 9, 11);

	[DllImport("user32.dll")]
	private static extern IntPtr SendMessage(IntPtr handle, int msg, IntPtr wParam, IntPtr lParam);

	private static void Check(bool ok, string text)
	{
		if (!ok)
		{
			throw new Exception("FAIL: " + text);
		}
		log.Add("PASS: " + text);
	}

	private static void Pump()
	{
		for (int i = 0; i < 5; i++)
		{
			Application.DoEvents();
		}
	}

	private static Form Popup()
	{
		return Application.OpenForms.Cast<Form>().SingleOrDefault((Form f) => f.Name == "clockCompletion" && f.Visible);
	}

	private static void Click(Control box)
	{
		box.Focus();
		IntPtr lParam = new IntPtr((box.Height / 2 << 16) | (box.Width / 2));
		SendMessage(box.Handle, 513, new IntPtr(1), lParam);
		SendMessage(box.Handle, 514, IntPtr.Zero, lParam);
		Pump();
	}

	private static void Click(ReferenceGrid grid, DataGridViewCell cell)
	{
		grid.Focus();
		Rectangle cellDisplayRectangle = grid.GetCellDisplayRectangle(cell.ColumnIndex, cell.RowIndex, cutOverflow: false);
		IntPtr lParam = new IntPtr((cellDisplayRectangle.Top + cellDisplayRectangle.Height / 2 << 16) | (cellDisplayRectangle.Left + cellDisplayRectangle.Width / 2));
		SendMessage(grid.Handle, 513, new IntPtr(1), lParam);
		SendMessage(grid.Handle, 514, IntPtr.Zero, lParam);
		Pump();
	}

	private static void Choose()
	{
		Form form = Popup();
		Control control = form.Controls[0];
		SendMessage(control.Handle, 513, new IntPtr(1), new IntPtr(262148));
		SendMessage(control.Handle, 514, IntPtr.Zero, new IntPtr(262148));
		Pump();
	}

	private static void Key(Control box, Keys key)
	{
		box.GetType().GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(box, new object[1]
		{
			new KeyEventArgs(key)
		});
		Pump();
	}

	private static void Double(Control box)
	{
		SendMessage(box.Handle, 515, new IntPtr(1), new IntPtr((box.Height / 2 << 16) | (box.Width / 2)));
		SendMessage(box.Handle, 514, IntPtr.Zero, IntPtr.Zero);
		Pump();
	}

	private static void Dialogs(string folder)
	{
		Form form = new Form();
		form.Text = "Podpowiedź godziny — 197";
		form.ClientSize = new Size(430, 190);
		using Form form2 = form;
		Button button = new Button();
		button.Text = "Inne pole";
		button.Location = new Point(20, 140);
		Button button2 = button;
		ReferenceTimeInput referenceTimeInput = new ReferenceTimeInput(null, now, blue: false, () => now);
		referenceTimeInput.Location = new Point(20, 25);
		ReferenceTimeInput referenceTimeInput2 = referenceTimeInput;
		ReferenceTimeInput referenceTimeInput3 = new ReferenceTimeInput(null, now, blue: false, () => now);
		referenceTimeInput3.Location = new Point(220, 25);
		ReferenceTimeInput referenceTimeInput4 = referenceTimeInput3;
		form2.Controls.AddRange(new Control[3] { button2, referenceTimeInput2, referenceTimeInput4 });
		form2.ActiveControl = button2;
		form2.Show();
		form2.Hide();
		form2.Show();
		button2.Focus();
		Pump();
		ClockTextBox clockTextBox = (ClockTextBox)referenceTimeInput2.Input;
		Check(clockTextBox.SuggestionText == "" && ClockMask.Value(clockTextBox.Text) == "" && Popup() == null, "Nieaktywne puste pola nie pokazują dodatkowych godzin.");
		Ui.Capture(form2, Path.Combine(folder, "formularz-domyslny.png"));
		clockTextBox.Focus();
		Pump();
		Check(Popup() == null && !referenceTimeInput2.Active.Checked, "Sam fokus nie otwiera listy i nie uzupełnia wartości.");
		Click(clockTextBox);
		Form form3 = Popup();
		Check(form3 != null && clockTextBox.SuggestionText == "00:09" && clockTextBox.Focused, "Kliknięcie otwiera jedną podpowiedź i zachowuje fokus edytora.");
		Check(!referenceTimeInput2.Active.Checked && !referenceTimeInput2.Value.HasValue && ClockMask.Value(clockTextBox.Text) == "", "Widoczna podpowiedź nie jest zapisaną godziną.");
		Check(form3.Top == clockTextBox.PointToScreen(new Point(0, clockTextBox.Height)).Y, "Lista znajduje się bezpośrednio pod aktywnym polem.");
		Ui.Capture(form3, Path.Combine(folder, "lista-podpowiedzi.png"));
		clockTextBox.Text = "0_:__";
		Pump();
		Check(clockTextBox.SuggestionText == "00:09", "Pasujący początek godziny zachowuje podpowiedź.");
		Choose();
		Check(clockTextBox.Text == "00:09" && referenceTimeInput2.Value == now.Date.AddMinutes(9.0) && Popup() == null, "Kliknięcie pozycji uzupełnia zwykły tekst i zamyka listę.");
		Click(clockTextBox);
		Check(Popup() == null && clockTextBox.Text == "00:09", "Kliknięcie zapisanej godziny nie proponuje jej zastąpienia.");
		clockTextBox.Text = "";
		Click(clockTextBox);
		Key(clockTextBox, Keys.Escape);
		Check(Popup() == null && !referenceTimeInput2.Value.HasValue, "Escape zamyka podpowiedź bez przyjęcia czasu.");
		Click(clockTextBox);
		clockTextBox.Text = "1_:__";
		Pump();
		Check(Popup() == null && clockTextBox.Text == "1_:__", "Niepasujący wpis chowa listę bez zmiany wpisanych znaków.");
		clockTextBox.Text = "";
		Click(clockTextBox);
		button2.Focus();
		Pump();
		Check(Popup() == null && !referenceTimeInput2.Active.Checked, "Przejście do innego pola odrzuca niezaznaczoną podpowiedź.");
		Click(clockTextBox);
		Click(referenceTimeInput4.Input);
		Check(Popup() != null && clockTextBox.SuggestionText == "" && ((ClockTextBox)referenceTimeInput4.Input).SuggestionText == "00:09", "Tylko aktualnie kliknięte pole pokazuje listę.");
		button2.Focus();
		clockTextBox.Text = "";
		Click(clockTextBox);
		clockTextBox.Select(0, 0);
		SendMessage(clockTextBox.Handle, 258, new IntPtr(48), IntPtr.Zero);
		Pump();
		Check(clockTextBox.Text == "0_:__" && clockTextBox.SuggestionText == "00:09", "Wpisanie znaku filtruje podpowiedź bez przyjmowania czasu.");
		Key(clockTextBox, Keys.Return);
		Check(clockTextBox.Text == "00:09" && referenceTimeInput2.Value == now.Date.AddMinutes(9.0), "Enter przyjmuje widoczną propozycję w formularzu.");
		clockTextBox.Text = "";
		Double(clockTextBox);
		Check(clockTextBox.Text == "00:09" && referenceTimeInput2.Active.Checked, "Dwuklik nadal przyjmuje bieżący czas.");
		clockTextBox.Text = "";
		Click(clockTextBox);
		form2.Left += 5;
		Pump();
		Check(Popup() == null, "Przesunięcie okna chowa podpowiedź.");
		clockTextBox.ReadOnly = true;
		Click(clockTextBox);
		Check(Popup() == null && ClockMask.Value(clockTextBox.Text) == "", "Pole tylko do odczytu nie oferuje uzupełnienia.");
		clockTextBox.ReadOnly = false;
		Click(clockTextBox);
		form2.Close();
		Pump();
		Check(Popup() == null, "Zamknięcie formularza zamyka także podpowiedź.");
	}

	private static void Journal(string folder)
	{
		Engine engine = (Engine)typeof(Input196Tests).GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[1] { folder });
		Section section = engine.Station.Sections.First();
		JournalEntry entry = engine.Note(section.Id, now, "", "Pociąg");
		entry.Direction = "Z";
		entry.Number = "474000";
		entry.Track = "1";
		engine.SaveJournal(entry);
		int writes = 0;
		engine.SimRailTimeChanged += (SepeWriteRequest r) =>
		{
			writes++;
		};
		using MainForm mainForm = new MainForm(engine);
		mainForm.Show();
		Pump();
		List<JournalPane> source = (List<JournalPane>)typeof(MainForm).GetField("panes", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mainForm);
		JournalPane journalPane = source.Single((JournalPane p) => p.SectionId == section.Id);
		journalPane.FocusEntry(entry.Id);
		ReferenceGrid referenceGrid = (ReferenceGrid)journalPane.Grid;
		DataGridViewRow dataGridViewRow = referenceGrid.Rows.Cast<DataGridViewRow>().Single((DataGridViewRow r) => ((JournalEntry)r.Tag).Id == entry.Id);
		Check(dataGridViewRow.Cells.Cast<DataGridViewCell>().OfType<ClockCell>().All((ClockCell c) => c.SuggestionText == "" && ClockMask.Value(Convert.ToString(c.Value)) == ""), "Nieedytowany wiersz ma poprzednie puste pola zamiast szarych godzin.");
		Ui.Capture(mainForm, Path.Combine(folder, "dziennik-domyslny.png"));
		Click(referenceGrid, dataGridViewRow.Cells["arrived"]);
		ClockEditingControl clockEditingControl = (ClockEditingControl)referenceGrid.EditingControl;
		Check(Popup() != null && clockEditingControl.SuggestionText == "00:09" && clockEditingControl.Focused && writes == 0, "Kliknięcie czasu w dzienniku pokazuje podpowiedź bez wysyłki.");
		Check(ClockMask.Value(Convert.ToString(dataGridViewRow.Cells["left"].Value)) == "" && ((ClockCell)dataGridViewRow.Cells["left"]).SuggestionText == "", "Sąsiednie komórki pozostają puste podczas podpowiedzi.");
		Key(clockEditingControl, Keys.Escape);
		referenceGrid.CancelEdit();
		journalPane.CommitPending();
		Check(!engine.State.Entries.Single((JournalEntry e) => e.Id == entry.Id).Arrived.HasValue && writes == 0, "Odrzucenie podpowiedzi w dzienniku nie zapisuje ani nie wysyła godziny.");
		Click(referenceGrid, dataGridViewRow.Cells["arrived"]);
		Choose();
		Check(((TextBox)referenceGrid.EditingControl).Text == "00:09" && referenceGrid.IsCurrentCellInEditMode && Popup() == null, "Wybór z listy uzupełnia aktywny edytor dziennika.");
		Check(referenceGrid.EndEdit() && journalPane.CommitPending() && engine.State.Entries.Single((JournalEntry e) => e.Id == entry.Id).Arrived == now.Date.AddMinutes(9.0), "Wybraną godzinę zapisuje normalne zatwierdzenie wiersza.");
		Click(referenceGrid, dataGridViewRow.Cells["permission"]);
		Double(referenceGrid.EditingControl);
		Check(engine.State.Entries.Single((JournalEntry e) => e.Id == entry.Id).Permission == now.Date.AddMinutes(9.0), "Dwuklik pustej godziny nadal zatwierdza ją od razu.");
		Click(referenceGrid, dataGridViewRow.Cells["left"]);
		clockEditingControl = (ClockEditingControl)referenceGrid.EditingControl;
		clockEditingControl.Text = "00:__";
		Choose();
		Check(clockEditingControl.LastChosenTime == now.Date.AddMinutes(9.0) && clockEditingControl.Text == "00:09", "Uzupełnianie pasującego prefiksu w dzienniku zachowuje pełną datę.");
		referenceGrid.CancelEdit();
		mainForm.Close();
		Pump();
		Check(Popup() == null, "Zamknięcie okna dziennika nie zostawia listy na ekranie.");
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		bool enabled = LocalPersistence.Enabled;
		LocalPersistence.Enabled = false;
		try
		{
			Dialogs(folder);
			Journal(folder);
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			File.WriteAllLines(Path.Combine(folder, "wyniki-input197.txt"), log, Encoding.UTF8);
			return false;
		}
		finally
		{
			LocalPersistence.Enabled = enabled;
		}
		File.WriteAllLines(Path.Combine(folder, "wyniki-input197.txt"), log, Encoding.UTF8);
		return true;
	}
}

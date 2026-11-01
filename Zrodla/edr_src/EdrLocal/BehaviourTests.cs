using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace EdrLocal;

internal static class BehaviourTests
{
	private static string progress;

	private static void Check(bool value, string text, List<string> log)
	{
		if (!value)
		{
			throw new Exception("FAIL: " + text);
		}
		log.Add("PASS: " + text);
		File.AppendAllText(progress, text + Environment.NewLine);
	}

	private static object Field(object instance, string name)
	{
		return instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
	}

	public static void Run(MainForm main, string folder, List<string> log)
	{
		progress = Path.Combine(folder, "progress.txt");
		File.WriteAllText(progress, "START");
		typeof(MainForm).GetField("useTimeRange", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(main, false);
		Engine e = main.Engine;
		Train t = e.State.Trains[4];
		main.SelectTrain(t.Id);
		DataGridView dataGridView = (DataGridView)Field(main, "trains");
		int index = dataGridView.Rows.Cast<DataGridViewRow>().First((DataGridViewRow r) => (string)r.Tag == t.Id).Index;
		MethodInfo method = typeof(DataGridView).GetMethod("OnCellDoubleClick", BindingFlags.Instance | BindingFlags.NonPublic);
		int count = Application.OpenForms.Count;
		method.Invoke(dataGridView, new object[1]
		{
			new DataGridViewCellEventArgs(dataGridView.Columns["even"].Index, index)
		});
		Application.DoEvents();
		JournalEntry arrival = e.State.Entries.Single((JournalEntry x) => x.TrainId == t.Id && x.Direction == "Z" && !x.Cancelled);
		Check(Application.OpenForms.Count == count, "Dwuklik numeru rejestruje przyjęcie bez okna dialogowego.", log);
		Check(arrival.SectionId == "SWI" && arrival.Number == "69005" && arrival.Track == t.Track && !arrival.Left.HasValue && !arrival.Arrived.HasValue, "Dziennik, parzystość i tor pochodzą z konfiguracji; rejestracja nie wymyśla czasów.", log);
		List<JournalPane> source = (List<JournalPane>)Field(main, "panes");
		JournalPane journalPane = source.First((JournalPane p) => p.SectionId == arrival.SectionId);
		DataGridView grid = journalPane.Grid;
		DataGridViewRow dataGridViewRow = grid.Rows.Cast<DataGridViewRow>().Single((DataGridViewRow r) => ((JournalEntry)r.Tag).Id == arrival.Id);
		Type(grid, dataGridViewRow, "left", "2358");
		Type(grid, dataGridViewRow, "arrived", "0003");
		Check(!e.State.Entries.First((JournalEntry x) => x.Id == arrival.Id).Left.HasValue, "Edycja kilku komórek pozostaje w bieżącym wierszu do zatwierdzenia.", log);
		grid.CurrentCell = grid.Rows[0].Cells["notes"];
		Application.DoEvents();
		JournalEntry journalEntry = e.State.Entries.First((JournalEntry x) => x.Id == arrival.Id);
		Check(journalEntry.Left.HasValue && journalEntry.Left.Value.Hour == 23 && journalEntry.Arrived.Value.Date == journalEntry.Left.Value.Date.AddDays(1.0), "Zmiana wiersza zapisuje godziny i rozpoznaje przejazd przez północ.", log);
		Check(e.State.Trains.First((Train x) => x.Id == t.Id).Arrival == journalEntry.Arrived, "Zapis bezpośredni aktualizuje czas rzeczywisty na wykazie pociągów.", log);
		Type(grid, dataGridViewRow, "notes", "Po zamkniętym torze numer 2");
		Check(journalPane.CommitPending(), "Zapis przyciskiem / Ctrl+S zatwierdza bieżący wiersz.", log);
		Check(e.State.Entries.First((JournalEntry x) => x.Id == arrival.Id).LineTrack == 2, "Wpis uwag interpretuje jazdę po torze lewym.", log);
		Type(grid, dataGridViewRow, "notes", "");
		Type(grid, dataGridViewRow, "arrived", "");
		Check(journalPane.CommitPending(), "Puste pole usuwa wcześniej wpisaną godzinę.", log);
		Check(e.State.Entries.First((JournalEntry x) => x.Id == arrival.Id).LineTrack == 1 && !e.State.Trains.First((Train x) => x.Id == t.Id).Arrival.HasValue, "Usunięcie uwag cofa tor lewy; usunięcie przyjazdu cofa potwierdzenie.", log);
		Type(grid, dataGridViewRow, "left", "10:00");
		Type(grid, dataGridViewRow, "arrived", "09:00");
		string text = Json.Text(e.State);
		Check(!journalPane.CommitPending() && Json.Text(e.State) == text, "Błędna chronologia nie nadpisuje danych i pozostawia wiersz do poprawy.", log);
		Type(grid, dataGridViewRow, "arrived", "10:05");
		Check(journalPane.CommitPending(), "Błędny wiersz można poprawić i zapisać bez utraty pozostałych pól.", log);
		grid.CurrentCell = dataGridViewRow.Cells["notes"];
		grid.BeginEdit(selectAll: true);
		((TextBox)grid.EditingControl).Text = "Niezakończona edycja";
		journalPane.RefreshData();
		((CompactDatePicker)Field(main, "timetableDay")).Value = DateTime.Now;
		main.RefreshData();
		Check(grid.IsCurrentCellInEditMode && ((TextBox)grid.EditingControl).Text == "Niezakończona edycja", "Odświeżanie nie przerywa rozpoczętej edycji komórki.", log);
		Check(journalPane.CommitPending(), "Zapis edytora aktualnie otwartej komórki obejmuje wpisany tekst.", log);
		main.SelectTrain(t.Id);
		index = dataGridView.Rows.Cast<DataGridViewRow>().First((DataGridViewRow r) => (string)r.Tag == t.Id).Index;
		method.Invoke(dataGridView, new object[1]
		{
			new DataGridViewCellEventArgs(dataGridView.Columns["even"].Index, index)
		});
		Application.DoEvents();
		Check(Enumerable.Count(e.State.Entries, (JournalEntry x) => x.TrainId == t.Id && !x.Cancelled) == 2 && Application.OpenForms.Count == count, "Drugi dwuklik rejestruje wyprawienie bez dialogu.", log);
		int count2 = e.State.History.Count;
		method.Invoke(dataGridView, new object[1]
		{
			new DataGridViewCellEventArgs(dataGridView.Columns["even"].Index, index)
		});
		Check(e.State.History.Count == count2, "Kolejny dwuklik po przyjęciu i wyprawieniu nie dodaje duplikatu.", log);
		Store store = new Store(e.Store.DirectoryPath);
		store.Load();
		Check(store.State.Entries.First((JournalEntry x) => x.Id == arrival.Id).Text == "Niezakończona edycja", "Zmiany z komórek przetrwały ponowne wczytanie JSON.", log);
		TabControl tabControl = (TabControl)Field(main, "topTabs");
		Dictionary<string, DataGridView> dictionary = (Dictionary<string, DataGridView>)Field(main, "detailGrids");
		Check(dictionary.Count == 11 && dictionary["TN"].Columns[0].HeaderText == "Czas", "Zakładki korzystają z oddzielnych tabel, a TN z historii oznaczenia.", log);
		e.SetDangerous(t.Id, value: true);
		e.SetDangerous(t.Id, value: false);
		main.SelectTrain(t.Id);
		Check(dictionary["TN"].Rows.Count == 2 && CellValue(dictionary["TN"], 1, 3) == "Usunięto oznaczenie TN", "Historia TN pokazuje oddzielne wprowadzenie i usunięcie oznaczenia.", log);
		string notes = e.State.Trains.First((Train x) => x.Id == t.Id).Notes;
		e.Change("Dane do weryfikacji zakładek", t.Id, (AppState s) =>
		{
			Train train = s.Trains.First((Train y) => y.Id == t.Id);
			train.DetailRows.Add(new DetailRow
			{
				Id = "order-test",
				Kind = "orders",
				Cells = new string[6] { "1633206", "TELEGRAM TESTOWY", "Nadawca lokalny", "2026-10-17", "IDDE", "2026-10-17" }
			});
			train.DetailRows.Add(new DetailRow
			{
				Id = "connection-test",
				Kind = "connections-in",
				Cells = new string[6] { "12345", "10:06", "5", "ROJ", "Przewoźnik lokalny", "A - B" }
			});
		});
		main.SelectTrain(t.Id);
		Check(CellValue(dictionary["orders"], 0, 1) == "TELEGRAM TESTOWY" && CellValue(dictionary["connections-in"], 0, 0) == "12345" && e.State.Trains.First((Train x) => x.Id == t.Id).Notes == notes, "Zarządzenie i skomunikowanie mają niezależne dane; uwagi własne pozostają zachowane.", log);
		DateTime value = e.State.Entries.First((JournalEntry x) => x.Id == arrival.Id).Arrived.Value;
		e.SetTrainTimes(t.Id, value, value.AddMinutes(2.0));
		Check(e.State.Entries.First((JournalEntry x) => x.TrainId == t.Id && x.Direction == "DO" && !x.Cancelled).Left == value.AddMinutes(2.0), "Okno czasów synchronizuje wyprawienie z dziennikiem.", log);
		string text2 = Json.Text(e.State);
		bool flag = false;
		try
		{
			e.SetTrainTimes(t.Id, value, value.AddMinutes(-1.0));
		}
		catch
		{
			flag = true;
		}
		Check(flag && Json.Text(e.State) == text2, "Błędny zapis czasu z formularza jest odrzucany atomowo.", log);
		typeof(MainForm).GetField("confirmationFilter", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(main, 1);
		main.SelectTrain(t.Id);
		Check(dataGridView.Rows.Cast<DataGridViewRow>().All((DataGridViewRow r) =>
		{
			Train train = e.State.Trains.First((Train y) => y.Id == (string)r.Tag);
			return train.Arrival.HasValue || train.Departure.HasValue;
		}), "Filtr potwierdzone pokazuje wyłącznie pociągi z rzeczywistym czasem.", log);
		typeof(MainForm).GetField("confirmationFilter", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(main, 0);
		main.SelectTrain(t.Id);
		LocalUser user = e.User;
		e.User = e.State.Users.First((LocalUser x) => x.Role == "Obserwator");
		journalPane.RefreshData();
		DataGridViewRow dataGridViewRow2 = journalPane.Grid.Rows.Cast<DataGridViewRow>().First((DataGridViewRow r) => ((JournalEntry)r.Tag).Id == arrival.Id);
		Check(dataGridViewRow2.ReadOnly, "Obserwator otrzymuje nieedytowalne komórki dziennika.", log);
		e.User = user;
		journalPane.RefreshData();
		main.MaximumSize = Size.Empty;
		main.Size = new Size(1920, 1040);
		main.ApplyZoom();
		Application.DoEvents();
		Ui.Capture(main, Path.Combine(folder, "before-size-check.png"));
		File.WriteAllText(Path.Combine(folder, "size-metrics.txt"), main.CurrentZoom + "|" + dataGridView.Font.Size + "|" + dataGridView.Columns["from"].Width);
		Check(main.CurrentZoom == 1.25f && dataGridView.Font.Size >= 10f && dataGridView.Columns["from"].Width >= 145, "Full HD dobiera 125% i powiększa natywne czcionki oraz kolumny.", log);
		Ui.Capture(main, Path.Combine(folder, "full-hd.png"));
		foreach (TabPage tabPage in tabControl.TabPages)
		{
			tabControl.SelectedTab = tabPage;
			Application.DoEvents();
			Ui.Capture(main, Path.Combine(folder, "tab-" + tabControl.SelectedIndex + ".png"));
		}
		tabControl.SelectedIndex = 0;
		MethodInfo method2 = typeof(MainForm).GetMethod("SetLayout", BindingFlags.Instance | BindingFlags.NonPublic);
		method2.Invoke(main, new object[2] { "Prawo", false });
		Check(((TabControl)Field(main, "journalTabs")).TabPages.Count == e.Station.Sections.Count + 1, "Układ z prawej rozdziela dzienniki na pojedyncze zakładki.", log);
		method2.Invoke(main, new object[2] { "Okno", false });
		Form form = (Form)Field(main, "detached");
		form.Close();
		Application.DoEvents();
		Check((string)Field(main, "currentLayout") == "Prawo", "Zamknięcie oddzielnego okna przywraca poprzedni układ dzienników.", log);
		method2.Invoke(main, new object[2] { "Dół", false });
		main.Size = new Size(1259, 708);
		main.ApplyZoom();
		main.SelectTrain(e.State.Trains[0].Id);
	}

	private static string CellValue(DataGridView grid, int row, int column)
	{
		return Convert.ToString(grid.Rows[row].Cells[column].Value);
	}

	private static void Type(DataGridView grid, DataGridViewRow row, string column, string text)
	{
		File.AppendAllText(progress, "TYPE " + column + "=" + text + " select\n");
		grid.CurrentCell = row.Cells[column];
		File.AppendAllText(progress, " begin\n");
		if (!grid.IsCurrentCellInEditMode)
		{
			grid.BeginEdit(selectAll: true);
		}
		if (!(grid.EditingControl is TextBox textBox))
		{
			throw new Exception("Brak edytora komórki " + column);
		}
		File.AppendAllText(progress, " type\n");
		textBox.Text = text;
		File.AppendAllText(progress, " end\n");
		if (!grid.EndEdit())
		{
			throw new Exception("Nie zatwierdzono komórki " + column);
		}
	}
}

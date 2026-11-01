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

public static class Input196Tests
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

	private static T Field<T>(object obj, string name)
	{
		return (T)obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
	}

	private static void Pump()
	{
		for (int i = 0; i < 4; i++)
		{
			Application.DoEvents();
		}
	}

	private static void InvokeWheel(Control control, int delta, int x)
	{
		control.GetType().GetMethod("OnMouseWheel", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(control, new object[1]
		{
			new HandledMouseEventArgs(MouseButtons.None, 0, x, control.Height / 2, delta)
		});
		Pump();
	}

	private static void Double(Control control)
	{
		SendMessage(control.Handle, 515, new IntPtr(1), new IntPtr((control.Height / 2 << 16) | (control.Width / 2)));
		SendMessage(control.Handle, 514, IntPtr.Zero, new IntPtr((control.Height / 2 << 16) | (control.Width / 2)));
		Pump();
	}

	private static void Click(DataGridView grid, DataGridViewCell cell)
	{
		Rectangle cellDisplayRectangle = grid.GetCellDisplayRectangle(cell.ColumnIndex, cell.RowIndex, cutOverflow: false);
		IntPtr lParam = new IntPtr((cellDisplayRectangle.Top + cellDisplayRectangle.Height / 2 << 16) | (cellDisplayRectangle.Left + cellDisplayRectangle.Width / 2));
		grid.Focus();
		SendMessage(grid.Handle, 513, new IntPtr(1), lParam);
		SendMessage(grid.Handle, 514, IntPtr.Zero, lParam);
		Pump();
	}

	private static Engine Create(string folder)
	{
		SimRailTrain simRailTrain = new SimRailTrain();
		simRailTrain.trainNoLocal = "38172";
		simRailTrain.startStation = "A";
		simRailTrain.endStation = "C";
		simRailTrain.trainName = "ROJ";
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
				displayedTrainNumber = "38172",
				trainType = "ROJ",
				isActive = true,
				arrivalTime = "2026-10-25 00:00:00",
				departureTime = "2026-10-25 00:02:00"
			});
		}
		SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
		simRailSnapshot.ServerCode = "input196";
		simRailSnapshot.Trains = new List<SimRailTrain> { simRailTrain2 };
		simRailSnapshot.DispatcherNames = new HashSet<string> { "A", "B", "C" };
		simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
		simRailSnapshot.GameTime = now;
		SimRailSnapshot snapshot = simRailSnapshot;
		SimRailSession simRailSession = new SimRailSession(null, snapshot, "11");
		Station station = simRailSession.SuggestStation();
		station.Tracks = new List<string> { "1", "2", "3" };
		StationSettings.Normalize(station);
		Store store = new Store(Path.Combine(folder, "memory"), transient: true);
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
		engine2.BeginDuty(now.Date.AddHours(-1.0));
		return engine2;
	}

	private static void Dialogs(string folder)
	{
		DateTime clock = now;
		Form form = new Form();
		form.ClientSize = new Size(390, 240);
		using Form form2 = form;
		Button button = new Button();
		button.Text = "Inne pole";
		button.Top = 180;
		Button button2 = button;
		form2.Controls.Add(button2);
		ReferenceTimeInput referenceTimeInput = new ReferenceTimeInput(null, now, blue: false, () => clock);
		referenceTimeInput.Location = new Point(25, 20);
		ReferenceTimeInput referenceTimeInput2 = referenceTimeInput;
		ReferenceTimeInput referenceTimeInput3 = new ReferenceTimeInput(now.Date.AddMinutes(-1.0), now, blue: true, () => clock);
		referenceTimeInput3.Location = new Point(25, 60);
		ReferenceTimeInput referenceTimeInput4 = referenceTimeInput3;
		form2.Controls.Add(referenceTimeInput2);
		form2.Controls.Add(referenceTimeInput4);
		form2.ActiveControl = button2;
		form2.Show();
		button2.Focus();
		Pump();
		ClockTextBox clockTextBox = (ClockTextBox)referenceTimeInput2.Input;
		Check(ClockMask.Value(clockTextBox.Text) == "" && clockTextBox.SuggestionText == "" && !referenceTimeInput2.Value.HasValue, "Puste pole przed kliknięciem zachowuje wygląd bez dodatkowej godziny.");
		clockTextBox.Focus();
		Pump();
		Check(ClockMask.Value(clockTextBox.Text) == "" && !referenceTimeInput2.Active.Checked, "Samo wejście i pojedynczy klik nie przyjmują sugestii.");
		Ui.Capture(form2, Path.Combine(folder, "sugestie-przed-zatwierdzeniem.png"));
		Double(clockTextBox);
		Check(referenceTimeInput2.Active.Checked && referenceTimeInput2.Value == now.Date.AddMinutes(9.0), "Rzeczywisty dwuklik w formularzu przyjmuje bieżącą godzinę.");
		ClockTextBox clockTextBox2 = (ClockTextBox)referenceTimeInput4.Input;
		Double(clockTextBox2);
		Check(referenceTimeInput4.Value == now.Date.AddMinutes(-1.0), "Dwuklik nie nadpisuje istniejącej godziny.");
		InvokeWheel(clockTextBox2, 120, clockTextBox2.Width * 3 / 4);
		Check(referenceTimeInput4.Value == now.Date, "Kółko nad minutami przenosi 23:59 na 00:00 następnego dnia.");
		InvokeWheel(clockTextBox2, -120, clockTextBox2.Width * 3 / 4);
		Check(referenceTimeInput4.Value == now.Date.AddMinutes(-1.0), "Kółko w drugą stronę przywraca poprzedni dzień.");
		InvokeWheel(clockTextBox2, 120, clockTextBox2.Width / 4);
		Check(referenceTimeInput4.Value == now.Date.AddMinutes(59.0), "Kółko nad godzinami zmienia godzinę, zachowując minuty i datę.");
		InvokeWheel(clockTextBox2, 60, clockTextBox2.Width * 3 / 4);
		Check(referenceTimeInput4.Value == now.Date.AddMinutes(59.0), "Niepełny obrót kółka nie wykonuje podwójnej zmiany.");
		InvokeWheel(clockTextBox2, 60, clockTextBox2.Width * 3 / 4);
		Check(referenceTimeInput4.Value == now.Date.AddHours(1.0), "Dwa częściowe kroki dają jedną minutę z poprawnym przeniesieniem godziny.");
		DateTime? value = referenceTimeInput4.Value;
		clockTextBox2.ReadOnly = true;
		InvokeWheel(clockTextBox2, 120, clockTextBox2.Width / 4);
		Check(referenceTimeInput4.Value == value, "Pole tylko do odczytu nie reaguje na zmianę czasu kółkiem.");
		clockTextBox2.ReadOnly = false;
		clockTextBox.Focus();
		clockTextBox.Text = "";
		button2.Focus();
		Pump();
		Check(!referenceTimeInput2.Value.HasValue, "Sugestię można odrzucić przez wyczyszczenie pola.");
		ReferenceTimeInput referenceTimeInput5 = new ReferenceTimeInput(null, now, blue: false, () => clock);
		referenceTimeInput5.Location = new Point(25, 100);
		ReferenceTimeInput referenceTimeInput6 = referenceTimeInput5;
		form2.Controls.Add(referenceTimeInput6);
		referenceTimeInput6.Input.Focus();
		referenceTimeInput6.Input.Text = "23:50";
		Check(referenceTimeInput6.Value == now.Date.AddMinutes(-10.0), "Ręczne 23:50 tuż po północy oznacza poprzedni wieczór.");
		referenceTimeInput6.Input.ContextMenuStrip.Show(form2, new Point(180, 110));
		Pump();
		referenceTimeInput6.Input.ContextMenuStrip.Items[2].PerformClick();
		referenceTimeInput6.Input.ContextMenuStrip.Close();
		Check(referenceTimeInput6.Value == now.Date.AddHours(23.0).AddMinutes(50.0), "Jawnie wybrana data pozostaje nadrzędna wobec automatycznego rozpoznawania dnia.");
		using (OperationTimePicker operationTimePicker = new OperationTimePicker(now.Date.AddMinutes(-1.0)))
		{
			form2.Controls.Add(operationTimePicker);
			operationTimePicker.Location = new Point(0, 140);
			ClockPartUpDown[] array = (from x in operationTimePicker.Controls.OfType<ClockPartUpDown>()
				orderby x.Left
				select x).ToArray();
			InvokeWheel(array[1], 120, 10);
			Check(operationTimePicker.Value == now.Date, "Oddzielny wybór godzin i minut także przenosi datę przez północ.");
		}
		Ui.Capture(form2, Path.Combine(folder, "czas-po-zatwierdzeniu.png"));
		form2.Close();
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		bool enabled = LocalPersistence.Enabled;
		LocalPersistence.Enabled = false;
		try
		{
			Check(ClockDates.Resolve("23:50", now, null, null, now) == now.Date.AddMinutes(-10.0), "Odjazd 23:50 wpisywany o 00:09 otrzymuje datę poprzedniego dnia.");
			Check(ClockDates.Resolve("00:01", now, null, now.Date.AddMinutes(-10.0), now) == now.Date.AddMinutes(1.0), "Przyjazd 00:01 po odjeździe 23:50 nie przeskakuje o dodatkową dobę.");
			Check(ClockDates.Resolve("00:30", now, null, null, now) == now.Date.AddMinutes(30.0), "Rzeczywista przyszła godzina nie jest automatycznie przesuwana w przeszłość.");
			Check(ClockDates.Resolve("2026-10-26 00:01", now, null, null, now) == now.Date.AddDays(1.0).AddMinutes(1.0), "Jawna pełna data nie jest samoczynnie zmieniana.");
			Dialogs(folder);
			Engine engine = Create(folder);
			Train train = engine.State.Trains.Single();
			Section section = engine.Station.Sections.First((Section x) => x.Name == train.From);
			JournalEntry entry = engine.Note(section.Id, now, "", "Pociąg");
			entry.Direction = "Z";
			entry.TrainId = train.Id;
			entry.Number = train.Number;
			entry.Track = "1";
			entry.Left = now.Date.AddHours(23.0).AddMinutes(50.0);
			engine.Store.Commit((AppState s) =>
			{
				s.Entries.Add(entry);
			});
			int writes = 0;
			engine.SimRailTimeChanged += (SepeWriteRequest r) =>
			{
				writes++;
			};
			using (TelephoneEditor telephoneEditor = new TelephoneEditor(engine, section.Id, null))
			{
				telephoneEditor.Show();
				Pump();
				ClockTextBox clockTextBox = (ClockTextBox)telephoneEditor.EventTimeBox;
				clockTextBox.Text = "23:59";
				InvokeWheel(clockTextBox, 120, clockTextBox.Width * 3 / 4);
				TelephoneData telephoneData = (TelephoneData)typeof(TelephoneEditor).GetMethod("ReadData", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(telephoneEditor, null);
				Check(telephoneData.EffectiveAt == now.Date.AddDays(1.0), "Godzina zdarzenia w telefonogramie zachowuje następny dzień po przewinięciu 23:59.");
				clockTextBox.Text = "";
				clockTextBox.ShowSuggestions();
				bool flag = false;
				try
				{
					typeof(TelephoneEditor).GetMethod("ReadData", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(telephoneEditor, null);
				}
				catch (TargetInvocationException ex)
				{
					flag = ex.InnerException.Message.Contains("Podaj godzinę");
				}
				Check(flag && clockTextBox.SuggestionText == "00:09", "Sugestia w wymaganym polu telefonogramu nie zastępuje zatwierdzonej godziny.");
				telephoneEditor.Close();
				Pump();
			}
			using MainForm mainForm = new MainForm(engine);
			List<string> notices = new List<string>();
			mainForm.OperatorNoticeSink = (string title, string text) =>
			{
				notices.Add(text);
			};
			mainForm.Show();
			Pump();
			JournalPane journalPane = Field<List<JournalPane>>(mainForm, "panes").Single((JournalPane x) => x.SectionId == section.Id);
			journalPane.FocusEntry(entry.Id);
			ReferenceGrid referenceGrid = (ReferenceGrid)journalPane.Grid;
			DataGridViewRow dataGridViewRow = referenceGrid.Rows.Cast<DataGridViewRow>().Single((DataGridViewRow x) => ((JournalEntry)x.Tag).Id == entry.Id);
			Click(referenceGrid, dataGridViewRow.Cells["arrived"]);
			ClockEditingControl clockEditingControl = (ClockEditingControl)referenceGrid.EditingControl;
			Check(clockEditingControl.IsSuggestion && Convert.ToString(clockEditingControl.EditingControlFormattedValue) == "" && clockEditingControl.SuggestionText == "00:09" && writes == 0, "Pojedynczy klik w dzienniku tylko pokazuje sugestię i nie wysyła godziny.");
			referenceGrid.EndEdit();
			journalPane.CommitPending();
			Check(!engine.State.Entries.Single((JournalEntry x) => x.Id == entry.Id).Arrived.HasValue && writes == 0, "Opuszczenie nieprzyjętej sugestii nie zapisuje godziny.");
			referenceGrid.CurrentCell = dataGridViewRow.Cells["arrived"];
			referenceGrid.BeginEdit(selectAll: false);
			((TextBox)referenceGrid.EditingControl).Text = "00:01";
			Check(referenceGrid.EndEdit() && journalPane.CommitPending(), "Godzinę po północy można normalnie zatwierdzić.");
			JournalEntry journalEntry = engine.State.Entries.Single((JournalEntry x) => x.Id == entry.Id);
			DateTime? left = journalEntry.Left;
			DateTime dateTime = now.Date.AddMinutes(-10.0);
			Check(left.HasValue && left.GetValueOrDefault() == dateTime && journalEntry.Arrived == now.Date.AddMinutes(1.0) && notices.Count == 0, "Odtworzony przypadek 23:50 → 00:01 zachowuje poprawne daty bez błędu przyszłego przyjazdu.");
			referenceGrid.CurrentCell = dataGridViewRow.Cells["arrived"];
			referenceGrid.BeginEdit(selectAll: false);
			((TextBox)referenceGrid.EditingControl).Text = "00:30";
			referenceGrid.EndEdit();
			journalPane.CommitPending();
			Pump();
			Check(engine.State.Entries.Single((JournalEntry x) => x.Id == entry.Id).Arrived == now.Date.AddMinutes(1.0) && notices.Any((string x) => x.Contains("późniejszej")), "Walidacja nadal odrzuca rzeczywiście przyszły przyjazd, przywracając zapis bez blokowania okna.");
			JournalEntry fresh = engine.Note(section.Id, now, "", "Pociąg");
			fresh.Direction = "Z";
			fresh.Number = "999";
			fresh.Track = "1";
			engine.SaveJournal(fresh);
			journalPane.RefreshData();
			dataGridViewRow = referenceGrid.Rows.Cast<DataGridViewRow>().Single((DataGridViewRow x) => ((JournalEntry)x.Tag).Id == fresh.Id);
			Click(referenceGrid, dataGridViewRow.Cells["permission"]);
			clockEditingControl = (ClockEditingControl)referenceGrid.EditingControl;
			Double(clockEditingControl);
			Check(engine.State.Entries.Single((JournalEntry x) => x.Id == fresh.Id).Permission == now.Date.AddMinutes(9.0), "Dwuklik sugestii w dzienniku od razu zapisuje zaakceptowaną godzinę.");
			referenceGrid.CurrentCell = dataGridViewRow.Cells["permission"];
			referenceGrid.BeginEdit(selectAll: false);
			clockEditingControl = (ClockEditingControl)referenceGrid.EditingControl;
			InvokeWheel(clockEditingControl, -1200, clockEditingControl.Width * 3 / 4);
			Check(referenceGrid.EndEdit() && journalPane.CommitPending() && engine.State.Entries.Single((JournalEntry x) => x.Id == fresh.Id).Permission == now.Date.AddMinutes(-1.0), "Zmiana minut kółkiem w dzienniku zachowuje datę poprzedniego dnia.");
			referenceGrid.CurrentCell = dataGridViewRow.Cells["permission"];
			referenceGrid.BeginEdit(selectAll: false);
			clockEditingControl = (ClockEditingControl)referenceGrid.EditingControl;
			InvokeWheel(clockEditingControl, 120, clockEditingControl.Width * 3 / 4);
			referenceGrid.EndEdit();
			journalPane.CommitPending();
			Check(engine.State.Entries.Single((JournalEntry x) => x.Id == fresh.Id).Permission == now.Date, "Przeniesienie 23:59 na 00:00 w dzienniku zapisuje prawidłową datę.");
			referenceGrid.CurrentCell = dataGridViewRow.Cells["left"];
			referenceGrid.BeginEdit(selectAll: false);
			clockEditingControl = (ClockEditingControl)referenceGrid.EditingControl;
			referenceGrid.CancelEdit();
			Check(!engine.State.Entries.Single((JournalEntry x) => x.Id == fresh.Id).Left.HasValue, "Anulowanie pustego edytora nie przyjmuje sugestii.");
			Check(((ClockCell)dataGridViewRow.Cells["arrived"]).SuggestionText == "", "Niezaznaczona pusta komórka nie pokazuje podpowiedzi czasu.");
			Rectangle cellDisplayRectangle = referenceGrid.GetCellDisplayRectangle(dataGridViewRow.Cells["permission"].ColumnIndex, dataGridViewRow.Index, cutOverflow: false);
			typeof(ReferenceGrid).GetMethod("OnMouseWheel", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(referenceGrid, new object[1]
			{
				new HandledMouseEventArgs(MouseButtons.None, 0, cellDisplayRectangle.Left + cellDisplayRectangle.Width * 3 / 4, cellDisplayRectangle.Top + cellDisplayRectangle.Height / 2, 120)
			});
			referenceGrid.EndEdit();
			journalPane.CommitPending();
			Check(engine.State.Entries.Single((JournalEntry x) => x.Id == fresh.Id).Permission == now.Date.AddMinutes(1.0) && referenceGrid.CurrentCell.RowIndex == dataGridViewRow.Index, "Kółko nad komórką czasu zmienia minutę bez przechodzenia do innego wiersza.");
			engine.ReadOnlySession = true;
			Check(((ClockCell)dataGridViewRow.Cells["arrived"]).SuggestionText == "", "Widok tylko do odczytu nie oferuje sugestii edycyjnych.");
			Click(referenceGrid, dataGridViewRow.Cells["arrived"]);
			Check(!referenceGrid.IsCurrentCellInEditMode, "Tryb podglądu nadal blokuje przyjmowanie godzin.");
			engine.ReadOnlySession = false;
			JournalEntry journalEntry2 = engine.Note(section.Id, now, "", "Pociąg");
			journalEntry2.Direction = "Z";
			journalEntry2.Number = "555";
			journalEntry2.Track = "2";
			engine.SaveJournal(journalEntry2);
			mainForm.RefreshData();
			Colors(engine, mainForm, folder);
			ToolStripMenuItem toolStripMenuItem = mainForm.MainMenuStrip.Items.OfType<ToolStripMenuItem>().Single((ToolStripMenuItem x) => x.Text == "Pomoc");
			Check(toolStripMenuItem.DropDownItems.Cast<ToolStripItem>().All((ToolStripItem x) => x.Text != "O programie" && x.Text != "Licencja WebView2") && toolStripMenuItem.DropDownItems.Cast<ToolStripItem>().Any((ToolStripItem x) => x.Text == "Legenda"), "Pomoc zachowuje legendę bez O programie i licencji WebView2.");
			Check(BundledWebView2.License.Contains("Microsoft"), "Oryginalna informacja o licencji pozostaje w paczce i zasobach programu.");
			Ui.Capture(mainForm, Path.Combine(folder, "dziennik-polnoc-sugestie.png"));
			mainForm.Close();
			Pump();
		}
		catch (Exception ex2)
		{
			log.Add(ex2.ToString());
			File.WriteAllLines(Path.Combine(folder, "wyniki-input196.txt"), log, Encoding.UTF8);
			return false;
		}
		finally
		{
			LocalPersistence.Enabled = enabled;
		}
		File.WriteAllLines(Path.Combine(folder, "wyniki-input196.txt"), log, Encoding.UTF8);
		return true;
	}

	private static void Colors(Engine engine, MainForm main, string folder)
	{
		List<Button> source = Field<FlowLayoutPanel>(main, "tracks").Controls.OfType<Button>().ToList();
		using StationTrackComboBox stationTrackComboBox = new StationTrackComboBox(engine, "1");
		Check(StationTrackColors.ForTrack(engine, "1") == Color.Firebrick && StationTrackColors.ForTrack(engine, "2") == Color.Orange && StationTrackColors.ForTrack(engine, "3") == Color.FromArgb(25, 134, 18), "Lista rozróżnia zajęty, zarezerwowany i wolny tor.");
		Check(source.All((Button x) => x.BackColor == StationTrackColors.ForTrack(engine, x.Text)), "Lista i dolne przyciski torów korzystają z identycznych kolorów zajętości.");
		using Bitmap bitmap = new Bitmap(160, 30);
		using Graphics graphics = Graphics.FromImage(bitmap);
		typeof(StationTrackComboBox).GetMethod("OnDrawItem", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(stationTrackComboBox, new object[1]
		{
			new DrawItemEventArgs(graphics, new Font("Segoe UI", 12f), new Rectangle(0, 0, 160, 30), stationTrackComboBox.Items.IndexOf("1"), DrawItemState.None)
		});
		int num = 0;
		for (int num2 = 0; num2 < bitmap.Height; num2++)
		{
			for (int num3 = 0; num3 < bitmap.Width; num3++)
			{
				if (bitmap.GetPixel(num3, num2).ToArgb() == Color.Firebrick.ToArgb())
				{
					num++;
				}
			}
		}
		Check(num > 0, "Rzeczywisty rysunek zajętego numeru w liście jest czerwony.");
		bitmap.Save(Path.Combine(folder, "tor-zajety-lista.png"));
	}
}

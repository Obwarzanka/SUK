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

public static class Input198Tests
{
	private static readonly DateTime now = new DateTime(2026, 10, 25, 0, 9, 11);

	private static readonly List<string> log = new List<string>();

	[DllImport("user32.dll")]
	private static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);

	private static void Check(bool ok, string text)
	{
		if (!ok)
		{
			throw new Exception("FAIL: " + text);
		}
		log.Add("PASS: " + text);
	}

	private static void Reject(Action action, string text)
	{
		bool ok = false;
		try
		{
			action();
		}
		catch (Exception)
		{
			ok = true;
		}
		Check(ok, text);
	}

	private static void Pump()
	{
		for (int i = 0; i < 5; i++)
		{
			Application.DoEvents();
		}
	}

	private static T Field<T>(object o, string n)
	{
		return (T)o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
	}

	private static void Fire(Control c, string n, EventArgs e)
	{
		c.GetType().GetMethod(n, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(c, new object[1] { e });
		Pump();
	}

	private static void Click(Control c)
	{
		c.Focus();
		IntPtr l = new IntPtr((c.Height / 2 << 16) | (c.Width / 2));
		SendMessage(c.Handle, 513, new IntPtr(1), l);
		SendMessage(c.Handle, 514, IntPtr.Zero, l);
		Pump();
	}

	private static void Click(ReferenceGrid g, DataGridViewCell c)
	{
		g.Focus();
		Rectangle cellDisplayRectangle = g.GetCellDisplayRectangle(c.ColumnIndex, c.RowIndex, cutOverflow: false);
		IntPtr l = new IntPtr((cellDisplayRectangle.Top + cellDisplayRectangle.Height / 2 << 16) | (cellDisplayRectangle.Left + cellDisplayRectangle.Width / 2));
		SendMessage(g.Handle, 513, new IntPtr(1), l);
		SendMessage(g.Handle, 514, IntPtr.Zero, l);
		Pump();
	}

	private static void Double(Control c)
	{
		SendMessage(c.Handle, 515, new IntPtr(1), new IntPtr((c.Height / 2 << 16) | (c.Width / 2)));
		SendMessage(c.Handle, 514, IntPtr.Zero, IntPtr.Zero);
		Pump();
	}

	private static Engine Create(string folder)
	{
		return (Engine)typeof(Input196Tests).GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[1] { folder });
	}

	private static JournalEntry Entry(Engine e, string number, string track, DateTime? arrival)
	{
		JournalEntry x = e.Note(e.Station.Sections[0].Id, now.AddMinutes(-12.0), "", "Pociąg");
		x.Number = number;
		x.Direction = "Z";
		x.Track = track;
		if (!arrival.HasValue)
		{
			x.Time = now.AddMinutes(-4.0);
		}
		x.Arrived = arrival;
		e.SaveJournal(x);
		return Json.Clone(e.State.Entries.Single((JournalEntry a) => a.Id == x.Id));
	}

	private static Button Track(MainForm m, string n)
	{
		return Field<FlowLayoutPanel>(m, "tracks").Controls.OfType<Button>().Single((Button b) => b.Text == n);
	}

	private static void Inline(string folder)
	{
		Form form = new Form();
		form.Text = "Godziny 198";
		form.ClientSize = new Size(440, 150);
		using Form form2 = form;
		Button button = new Button();
		button.Text = "Inne pole";
		button.Top = 100;
		Button button2 = button;
		ReferenceTimeInput referenceTimeInput = new ReferenceTimeInput(null, now, blue: false, () => now);
		referenceTimeInput.Location = new Point(20, 20);
		ReferenceTimeInput referenceTimeInput2 = referenceTimeInput;
		ReferenceTimeInput referenceTimeInput3 = new ReferenceTimeInput(now.AddMinutes(-1.0), now, blue: false, () => now);
		referenceTimeInput3.Location = new Point(210, 20);
		ReferenceTimeInput referenceTimeInput4 = referenceTimeInput3;
		form2.Controls.AddRange(new Control[3] { button2, referenceTimeInput2, referenceTimeInput4 });
		form2.ActiveControl = button2;
		form2.Show();
		form2.Hide();
		form2.Show();
		button2.Focus();
		Pump();
		ClockTextBox clockTextBox = (ClockTextBox)referenceTimeInput2.Input;
		Check(!clockTextBox.IsSuggestion && ClockMask.Value(clockTextBox.Text) == "" && !referenceTimeInput2.Value.HasValue, "Przed kliknięciem pole wygląda jak zwykłe puste pole.");
		Click(clockTextBox);
		Check(clockTextBox.Text == "00:09" && clockTextBox.IsSuggestion && clockTextBox.SelectionStart == 0 && clockTextBox.SelectionLength == 5, "Kliknięcie od razu podaje zaznaczony tekst bieżącej godziny w samym polu.");
		Check(!referenceTimeInput2.Active.Checked && !referenceTimeInput2.Value.HasValue, "Podpowiedź w tekście pozostaje niezatwierdzona.");
		Check(!Application.OpenForms.Cast<Form>().Any((Form f) => f.Name == "clockCompletion"), "Nie powstaje osobna lista ani okienko podpowiedzi.");
		Ui.Capture(form2, Path.Combine(folder, "godzina-zaznaczona-w-polu.png"));
		button2.Focus();
		Pump();
		Check(ClockMask.Value(clockTextBox.Text) == "" && !referenceTimeInput2.Value.HasValue, "Opuszczenie samej sugestii przywraca puste pole bez zapisu.");
		Click(clockTextBox);
		Fire(clockTextBox, "OnKeyDown", new KeyEventArgs(Keys.Escape));
		Check(!clockTextBox.IsSuggestion && ClockMask.Value(clockTextBox.Text) == "", "Escape odrzuca zaznaczoną propozycję.");
		Click(clockTextBox);
		SendMessage(clockTextBox.Handle, 258, new IntPtr(49), IntPtr.Zero);
		Pump();
		Check(!clockTextBox.IsSuggestion && clockTextBox.Text == "1_:__", "Pisanie nadpisuje zaznaczoną propozycję od początku.");
		clockTextBox.Text = "00:01";
		Check(referenceTimeInput2.Value == now.Date.AddMinutes(1.0), "Ręcznie wpisany czas nadal ma zwykłą wartość i datę.");
		clockTextBox.Text = "";
		Click(clockTextBox);
		Double(clockTextBox);
		Check(!clockTextBox.IsSuggestion && referenceTimeInput2.Active.Checked && referenceTimeInput2.Value == now.Date.AddMinutes(9.0), "Dwuklik zatwierdza widoczną propozycję godziny.");
		ClockTextBox clockTextBox2 = (ClockTextBox)referenceTimeInput4.Input;
		Click(clockTextBox2);
		Double(clockTextBox2);
		Check(referenceTimeInput4.Value == now.Date.AddMinutes(8.0) && !clockTextBox2.IsSuggestion, "Kliknięcie i dwuklik nie nadpisują wpisanej wcześniej godziny.");
		clockTextBox.Text = "";
		Click(clockTextBox);
		Fire(clockTextBox, "OnKeyDown", new KeyEventArgs(Keys.Return));
		Check(referenceTimeInput2.Value == now.Date.AddMinutes(9.0) && !clockTextBox.IsSuggestion, "Enter zatwierdza propozycję w formularzu.");
		clockTextBox.Text = "";
		clockTextBox.ReadOnly = true;
		Click(clockTextBox);
		Check(!clockTextBox.IsSuggestion && ClockMask.Value(clockTextBox.Text) == "", "Tryb tylko do odczytu nie proponuje godzin.");
		form2.Close();
		Pump();
	}

	private static void Occupations(string folder)
	{
		Engine e = Create(folder);
		JournalEntry journalEntry = Entry(e, "69275", "1", now.AddMinutes(-8.0));
		JournalEntry journalEntry2 = Entry(e, "1613", "1", null);
		Check(e.TrackStatus("1").StartsWith("ZAJĘTY") && e.StationTrackItems("1").Count == 2, "Tor obsługuje jednocześnie zajętość i rezerwację.");
		List<StationTrackItem> source = e.StationTrackItems("1");
		Check(source.Last().Number == "69275" && !source.Last().Reserved && source.First().Reserved, "Lista od dołu pokazuje kolejność wjazdu, z osobnymi stanami pociągów.");
		string text = Json.Text(e.State.Entries);
		int writes = 0;
		e.SimRailTimeChanged += (SepeWriteRequest r) =>
		{
			writes++;
		};
		e.MoveStationTrackOccupation("entry:" + journalEntry.Id, "2");
		Check(e.TrackStatus("2").Contains("69275") && e.StationTrackItems("1").Single().Number == "1613", "Przeniesienie wybranego pociągu pozostawia drugi na pierwotnym torze.");
		Check(Json.Text(e.State.Entries) == text && writes == 0, "Ręczny manewr nie zmienia wpisów dziennika ani godzin SEPE.");
		e.RemoveStationTrackOccupation("entry:" + journalEntry.Id);
		Check(e.TrackStatus("2") == "WOLNY" && e.TrackStatus("1").StartsWith("REZERWACJA"), "Usunięcie jednej zajętości nie usuwa rezerwacji drugiego pociągu.");
		e.ApplySimRailSnapshot(e.SimRail.Snapshot);
		Check(e.TrackStatus("2") == "WOLNY", "Odświeżenie rozkładu nie odtwarza ręcznie zdjętego pociągu.");
		journalEntry.Track = "3";
		e.SaveJournal(journalEntry);
		Check(e.StationTrackItems("3").Single().Number == "69275", "Zmiana toru w dzienniku aktualizuje kontrolkę po wcześniejszym ręcznym usunięciu.");
		journalEntry.Track = "";
		e.SaveJournal(journalEntry);
		Check(e.TrackStatus("3") == "WOLNY", "Usunięcie numeru toru w dzienniku zwalnia kontrolkę.");
		string manual = e.AddStationTrackOccupation("2", "wagony towarowe");
		Check(e.TrackStatus("2").Contains("wagony towarowe"), "Można dodać dowolną zajętość opisową.");
		e.MoveStationTrackOccupation(manual, "3");
		Check(e.TrackStatus("2") == "WOLNY" && e.TrackStatus("3").Contains("wagony towarowe"), "Zajętość opisową można przenieść bez wpisu w dzienniku.");
		Check(Json.Clone(e.State).StationTrackOccupations.Any((StationTrackOccupation x) => "manual:" + x.Id == manual), "Zajętości przechodzą przez normalną transakcję i serializację stanu.");
		Engine engine = e.CreateWindowView();
		Check(engine.TrackStatus("3") == e.TrackStatus("3"), "Drugie okno korzysta ze wspólnej zajętości.");
		e.RemoveStationTrackOccupation(manual);
		Check(engine.TrackStatus("3") == "WOLNY", "Usunięcie jest widoczne również w drugiej instancji widoku.");
		e.AddStationTrackOccupation("2", "38172");
		int count = e.State.Entries.Count;
		JournalEntry journalEntry3 = e.Note(e.Station.Sections[1].Id, now, "", "Pociąg");
		journalEntry3.Number = "38172";
		journalEntry3.TrainId = e.State.Trains.Single().Id;
		journalEntry3.Direction = "DO";
		journalEntry3.Track = "2";
		journalEntry3.Left = now.AddMinutes(-1.0);
		e.SaveJournal(journalEntry3);
		Check(e.TrackStatus("2") == "WOLNY" && e.State.Entries.Count == count + 1, "Zapis odjazdu automatycznie usuwa ręcznie wstawiony pociąg, także przy wpisie po czasie.");
		string reservation = "entry:" + journalEntry2.Id;
		e.MoveStationTrackOccupation(reservation, "2");
		Check(e.TrackStatus("2").StartsWith("REZERWACJA"), "Przeniesiona rezerwacja zachowuje stan oczekiwania na przyjazd.");
		journalEntry2.Arrived = now.AddMinutes(-2.0);
		e.SaveJournal(journalEntry2);
		Check(e.TrackStatus("2").StartsWith("ZAJĘTY") && e.TrackStatus("1") == "WOLNY", "Potwierdzenie przyjazdu zmienia przeniesioną rezerwację w zajętość na właściwym torze.");
		int count2 = e.State.StationTrackOccupations.Count;
		Reject(() =>
		{
			e.AddStationTrackOccupation("99", "100");
		}, "Nie można dodać zajętości na nieistniejącym torze.");
		Reject(() =>
		{
			e.AddStationTrackOccupation("1", " ");
		}, "Pusta zajętość jest odrzucana.");
		Reject(() =>
		{
			e.MoveStationTrackOccupation("entry:stary", "1");
		}, "Nieaktualne przeciągnięcie nie zmienia kontrolki.");
		e.ReadOnlySession = true;
		Reject(() =>
		{
			e.AddStationTrackOccupation("1", "100");
		}, "Obserwator nie dodaje zajętości.");
		Reject(() =>
		{
			e.MoveStationTrackOccupation(reservation, "1");
		}, "Obserwator nie przenosi zajętości.");
		Reject(() =>
		{
			e.RemoveStationTrackOccupation(reservation);
		}, "Obserwator nie usuwa zajętości.");
		Check(e.State.StationTrackOccupations.Count == count2, "Odrzucone operacje nie zmieniają stanu.");
		e.ReadOnlySession = false;
		journalEntry2.Track = "3";
		e.SaveJournal(journalEntry2);
		Check(e.TrackStatus("2") == "WOLNY" && e.TrackStatus("3").Contains("1613"), "Jawna zmiana toru dziennika zastępuje wcześniejszy ręczny manewr.");
		journalEntry2.Track = "1";
		e.SaveJournal(journalEntry2);
		e.MoveStationTrackOccupation(reservation, "3");
		Station config = Json.Clone(e.Station);
		config.TrackSettings.RemoveAll((StationTrackSetting t) => t.Number == "3");
		Reject(() =>
		{
			StationSettings.Validate(config, e.State);
		}, "Konfigurator nie usuwa definicji toru z aktywną ręczną zajętością.");
		Check(writes == 1, "Tylko rzeczywisty odjazd powiązanego pociągu tworzy zdarzenie SEPE; manewry go nie tworzą.");
		JournalEntry journalEntry4 = Entry(e, "500", "2", now.AddMinutes(-3.0));
		journalEntry4.TrainId = "visit-one";
		e.SaveJournal(journalEntry4);
		JournalEntry journalEntry5 = Entry(e, "500", "2", now.AddMinutes(-2.0));
		journalEntry5.TrainId = "visit-two";
		e.SaveJournal(journalEntry5);
		JournalEntry journalEntry6 = e.Note(e.Station.Sections[1].Id, now, "", "Pociąg");
		journalEntry6.Number = "500";
		journalEntry6.TrainId = journalEntry4.TrainId;
		journalEntry6.Direction = "DO";
		journalEntry6.Left = now.AddMinutes(-1.0);
		e.SaveJournal(journalEntry6);
		Check(e.StationTrackItems("2").Count == 1 && e.StationTrackItems("2")[0].TrainId == journalEntry5.TrainId, "Odjazd nie usuwa innego pociągu o tym samym numerze, gdy ma inne ID wizyty.");
		Station station = Json.Clone(e.Station);
		station.Id = "foreign";
		e.Store.Commit((AppState state) =>
		{
			state.Stations.Add(station);
		});
		Engine engine2 = e.CreateWindowView();
		engine2.StationId = "foreign";
		Check(engine2.StationTrackItems("2").Count == 0, "Zajętości i ręczne manewry są rozdzielone między posterunkami.");
	}

	private static void UserInterface(string folder)
	{
		Directory.CreateDirectory(folder);
		Engine engine = Create(folder);
		JournalEntry a = Entry(engine, "69275", "1", now.AddMinutes(-8.0));
		Entry(engine, "1613", "1", null);
		using MainForm mainForm = new MainForm(engine);
		mainForm.Show();
		mainForm.Hide();
		mainForm.Show();
		Pump();
		JournalPane journalPane = Field<List<JournalPane>>(mainForm, "panes").Single((JournalPane p) => p.SectionId == a.SectionId);
		journalPane.SetDay(a.Time);
		journalPane.FocusEntry(a.Id);
		ReferenceGrid referenceGrid = (ReferenceGrid)journalPane.Grid;
		DataGridViewRow dataGridViewRow = referenceGrid.Rows.Cast<DataGridViewRow>().Single((DataGridViewRow r) => ((JournalEntry)r.Tag).Id == a.Id);
		Click(referenceGrid, dataGridViewRow.Cells["track"]);
		StationTrackEditingControl stationTrackEditingControl = referenceGrid.EditingControl as StationTrackEditingControl;
		Check(stationTrackEditingControl != null && stationTrackEditingControl.Items.Cast<string>().SequenceEqual(new string[4] { "", "1", "2", "3" }), "Tor stacyjny w samym dzienniku ma listę wyboru z pustą pozycją.");
		Check(stationTrackEditingControl.DroppedDown, "Kliknięcie komórki od razu rozwija listę torów.");
		Check(StationTrackColors.ForTrack(engine, "1") == Color.Firebrick && StationTrackColors.ForTrack(engine, "2") == Color.FromArgb(25, 134, 18), "Lista korzysta z tych samych kolorów zajętości co kontrolka.");
		stationTrackEditingControl.SelectedItem = "2";
		stationTrackEditingControl.DroppedDown = false;
		Check(referenceGrid.EndEdit() && journalPane.CommitPending() && engine.TrackStatus("2").Contains("69275"), "Wybór toru z listy zapisuje wiersz i przenosi automatyczną zajętość.");
		Click(referenceGrid, dataGridViewRow.Cells["track"]);
		stationTrackEditingControl = (StationTrackEditingControl)referenceGrid.EditingControl;
		stationTrackEditingControl.SelectedItem = "";
		stationTrackEditingControl.DroppedDown = false;
		Check(referenceGrid.EndEdit() && journalPane.CommitPending() && engine.TrackStatus("2") == "WOLNY", "Pusta pozycja usuwa tor z wiersza i kontrolki.");
		Click(referenceGrid, dataGridViewRow.Cells["permission"]);
		ClockEditingControl clockEditingControl = (ClockEditingControl)referenceGrid.EditingControl;
		Check(clockEditingControl.Text == "00:09" && clockEditingControl.IsSuggestion && clockEditingControl.SelectionLength == 5 && Convert.ToString(clockEditingControl.EditingControlFormattedValue) == "", "Komórka godziny ma od razu zaznaczoną propozycję, oddzieloną od zapisywanej wartości.");
		Ui.Capture(mainForm, Path.Combine(folder, "dziennik-podpowiedz-w-tekscie.png"));
		referenceGrid.EndEdit();
		journalPane.CommitPending();
		Check(!engine.State.Entries.Single((JournalEntry x) => x.Id == a.Id).Permission.HasValue, "Opuszczenie sugestii w dzienniku nie zapisuje godziny.");
		Click(referenceGrid, dataGridViewRow.Cells["permission"]);
		Double(referenceGrid.EditingControl);
		Check(engine.State.Entries.Single((JournalEntry x) => x.Id == a.Id).Permission == now.Date.AddMinutes(9.0), "Dwuklik przyjmuje i zapisuje godzinę w dzienniku.");
		Exception error = null;
		Timer timer = new Timer
		{
			Interval = 60
		};
		try
		{
			timer.Tick += (object s, EventArgs args) =>
			{
				Editor editor = Application.OpenForms.OfType<Editor>().SingleOrDefault((Editor f) => f.Text == "Tor stacyjny - nowa zajętość");
				if (editor == null)
				{
					return;
				}
				timer.Stop();
				try
				{
					editor.Fields.Controls.OfType<TextBox>().Single().Text = "wagony towarowe";
					editor.Buttons.Controls.OfType<Button>().Single((Button b) => b.Text == "Zapisz").PerformClick();
				}
				catch (Exception ex)
				{
					error = ex;
					editor.Close();
				}
			};
			timer.Start();
			Track(mainForm, "3").PerformClick();
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
		Check(engine.TrackStatus("3").Contains("wagony towarowe"), "Lewy klik toru otwiera działający dialog dodawania zajętości.");
		JournalEntry fresh = engine.Note(a.SectionId, now, "", "Pociąg");
		fresh.Direction = "Z";
		fresh.Number = "777";
		engine.SaveJournal(fresh);
		mainForm.RefreshData();
		journalPane.SetDay(now);
		dataGridViewRow = referenceGrid.Rows.Cast<DataGridViewRow>().Single((DataGridViewRow r) => ((JournalEntry)r.Tag).Id == fresh.Id);
		Click(referenceGrid, dataGridViewRow.Cells["permission"]);
		clockEditingControl = (ClockEditingControl)referenceGrid.EditingControl;
		Check(clockEditingControl.IsSuggestion, "Nowy wiersz ma aktywną sugestię przed klawiszem Enter.");
		Message msg = Message.Create(clockEditingControl.Handle, 256, new IntPtr(13), IntPtr.Zero);
		if (!clockEditingControl.PreProcessMessage(ref msg))
		{
			SendMessage(clockEditingControl.Handle, 256, new IntPtr(13), IntPtr.Zero);
		}
		Pump();
		Check(engine.State.Entries.Single((JournalEntry x) => x.Id == fresh.Id).Permission == now.Date.AddMinutes(9.0), "Enter przetworzony przez Windows Forms zatwierdza sugestię w dzienniku.");
		engine.AddStationTrackOccupation("1", "69275");
		mainForm.RefreshData();
		Button c = Track(mainForm, "1");
		Fire(c, "OnMouseEnter", EventArgs.Empty);
		Form form = Application.OpenForms.Cast<Form>().Single((Form f) => f.Name == "stationTrackOccupants" && f.Visible);
		Check(form.Controls.Count == 2 && form.Controls.Cast<Control>().Any((Control control) => control.Text == "1613" && control.BackColor == Color.Orange) && form.Controls.Cast<Control>().Any((Control control) => control.Text == "69275" && control.BackColor == Color.Firebrick), "Najechanie na tor pokazuje listę z czerwonym zajętym i pomarańczowym zarezerwowanym pociągiem.");
		Ui.Capture(form, Path.Combine(folder, "lista-pociagow-na-torze.png"));
		DataObject dataObject = new DataObject();
		dataObject.SetData(typeof(StationTrackDrag), new StationTrackDrag
		{
			Store = engine.Store,
			StationId = engine.StationId,
			Key = "entry:" + engine.State.Entries.Single((JournalEntry x) => x.Number == "1613").Id
		});
		DragEventArgs e = new DragEventArgs(dataObject, 0, 0, 0, DragDropEffects.Move, DragDropEffects.None);
		c = Track(mainForm, "2");
		Fire(c, "OnDragEnter", e);
		Check(e.Effect == DragDropEffects.Move, "Docelowy tor przyjmuje przeciągnięcie konkretnego pociągu.");
		Fire(c, "OnDragDrop", e);
		Check(engine.TrackStatus("2").Contains("1613") && engine.StationTrackItems("1").Single().Number == "69275", "Obsługa upuszczenia przenosi wybrany pociąg na docelowy tor.");
		c = Track(mainForm, "3");
		mainForm.ConfirmStationTrackRemoval = (string q) => false;
		c.ContextMenuStrip.Show(c, new Point(0, 0));
		Pump();
		Check(c.ContextMenuStrip.Items[0].Text.Contains("wagony towarowe"), "Prawy klik wskazuje konkretny pociąg lub opis do usunięcia.");
		((ToolStripMenuItem)c.ContextMenuStrip.Items[0]).PerformClick();
		c.ContextMenuStrip.Close();
		Check(engine.TrackStatus("3").Contains("wagony towarowe"), "Anulowanie potwierdzenia pozostawia zajętość.");
		mainForm.ConfirmStationTrackRemoval = (string q) => true;
		c.ContextMenuStrip.Show(c, new Point(0, 0));
		Pump();
		((ToolStripMenuItem)c.ContextMenuStrip.Items[0]).PerformClick();
		Check(engine.TrackStatus("3") == "WOLNY", "Potwierdzone menu kontekstowe usuwa wybraną zajętość.");
		engine.Station.TrackSettings.Single((StationTrackSetting t) => t.Number == "3").Visible = false;
		mainForm.RefreshData();
		Check(!Field<FlowLayoutPanel>(mainForm, "tracks").Controls.OfType<Button>().Any((Button b) => b.Text == "3") && StationSettings.TrackChoices(engine.Station, "").Contains("3"), "Ukrycie toru w kontrolce nie usuwa go z możliwości wyboru w dzienniku.");
		DataObject dataObject2 = new DataObject();
		dataObject2.SetData(typeof(StationTrackDrag), new StationTrackDrag
		{
			Store = engine.Store,
			StationId = "foreign",
			Key = "entry:" + a.Id
		});
		DragEventArgs e2 = new DragEventArgs(dataObject2, 0, 0, 0, DragDropEffects.Move, DragDropEffects.None);
		Fire(Track(mainForm, "1"), "OnDragEnter", e2);
		Check(e2.Effect == DragDropEffects.None, "Nie można upuścić zajętości innego posterunku.");
		engine.ReadOnlySession = true;
		c = Track(mainForm, "1");
		e = new DragEventArgs(dataObject, 0, 0, 0, DragDropEffects.Move, DragDropEffects.None);
		Fire(c, "OnDragEnter", e);
		Check(e.Effect == DragDropEffects.None, "Tryb podglądu blokuje przeciąganie w interfejsie.");
		engine.ReadOnlySession = false;
		dataGridViewRow = referenceGrid.Rows.Cast<DataGridViewRow>().Single((DataGridViewRow r) => ((JournalEntry)r.Tag).Id == fresh.Id);
		Click(referenceGrid, dataGridViewRow.Cells["left"]);
		Check(((ClockEditingControl)referenceGrid.EditingControl).IsSuggestion, "Przed zamknięciem pozostaje niezatwierdzona sugestia czasu.");
		Ui.Capture(mainForm, Path.Combine(folder, "tory-po-manewrach.png"));
		mainForm.Close();
		Pump();
		Check(!Application.OpenForms.Cast<Form>().Any((Form f) => f.Name == "stationTrackOccupants"), "Zamknięcie programu usuwa okno listy zajętości.");
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		bool enabled = LocalPersistence.Enabled;
		LocalPersistence.Enabled = false;
		try
		{
			Inline(folder);
			Occupations(Path.Combine(folder, "domain"));
			UserInterface(Path.Combine(folder, "ui"));
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			File.WriteAllLines(Path.Combine(folder, "wyniki-input198.txt"), log, Encoding.UTF8);
			return false;
		}
		finally
		{
			LocalPersistence.Enabled = enabled;
		}
		File.WriteAllLines(Path.Combine(folder, "wyniki-input198.txt"), log, Encoding.UTF8);
		return true;
	}
}

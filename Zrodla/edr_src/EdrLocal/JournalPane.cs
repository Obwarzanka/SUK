using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace EdrLocal;

public class JournalPane : UserControl
{
	private bool dirty;

	private bool committing;

	private string dirtyId;

	private DateTime lastClockCue;

	private readonly MainForm main;

	private readonly string sectionId;

	private Label heading;

	private Label operatorLabel;

	private CompactDatePicker day;

	private bool refreshing;

	private List<RailSignal> signals = new List<RailSignal>();

	public string LastEditError { get; private set; }

	public string SectionId => sectionId;

	public bool CanEdit => main.Engine.CanEditJournal(sectionId);

	internal bool HasPendingWindowDraft
	{
		get
		{
			if (!dirty)
			{
				return Grid.IsCurrentCellInEditMode;
			}
			return true;
		}
	}

	public DataGridView Grid { get; private set; }

	public DateTime SelectedDay => day.Value.Date;

	private IEnumerable<DataGridViewColumn> CrossingColumns()
	{
		return from DataGridViewColumn x in Grid.Columns
			where x.Tag is CrossingSetting
			select x;
	}

	private CrossingSetting CrossingColumn(string name)
	{
		if (!Grid.Columns.Contains(name))
		{
			return null;
		}
		return Grid.Columns[name].Tag as CrossingSetting;
	}

	private void AddCrossingColumns()
	{
		Section section = main.Engine.Station.Sections.First((Section x) => x.Id == sectionId);
		if (CrossingRules.All(section).Count == 0)
		{
			return;
		}
		Grid.Columns["notify"].Visible = false;
		foreach (CrossingSetting item in CrossingRules.Visible(section))
		{
			object obj;
			if (!CrossingRules.UsesTime(section, item))
			{
				DataGridViewCheckBoxColumn dataGridViewCheckBoxColumn = new DataGridViewCheckBoxColumn();
				dataGridViewCheckBoxColumn.CellTemplate = new SlashCell();
				dataGridViewCheckBoxColumn.TrueValue = "/";
				dataGridViewCheckBoxColumn.FalseValue = "";
				dataGridViewCheckBoxColumn.ValueType = typeof(string);
				obj = dataGridViewCheckBoxColumn;
			}
			else
			{
				ClockCell clockCell = new ClockCell();
				clockCell.Clock = () => main.Engine.Now;
				clockCell.CanSuggest = () => CanEdit;
				obj = new DataGridViewColumn(clockCell);
			}
			DataGridViewColumn dataGridViewColumn = (DataGridViewColumn)obj;
			dataGridViewColumn.Name = "crossing_" + item.Id;
			dataGridViewColumn.HeaderText = CrossingRules.Label(item);
			dataGridViewColumn.Tag = item;
			dataGridViewColumn.Width = 60;
			dataGridViewColumn.MinimumWidth = 44;
			dataGridViewColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
			dataGridViewColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
			Grid.Columns.Add(dataGridViewColumn);
		}
	}

	private void FillCrossingCells(DataGridViewRow row, JournalEntry entry)
	{
		if (sectionId == "TEL")
		{
			return;
		}
		Section section = main.Engine.Station.Sections.First((Section x) => x.Id == sectionId);
		foreach (DataGridViewColumn item in CrossingColumns())
		{
			row.Cells[item.Index].Value = ((entry.Type == "Pociąg") ? CrossingRules.Value(entry, section, (CrossingSetting)item.Tag) : "");
		}
	}

	private void StyleCrossingCells(DataGridViewRow row, JournalEntry entry)
	{
		Section section = main.Engine.Station.Sections.First((Section x) => x.Id == sectionId);
		foreach (DataGridViewColumn item in CrossingColumns())
		{
			CrossingSetting crossing = (CrossingSetting)item.Tag;
			DataGridViewCell dataGridViewCell = row.Cells[item.Index];
			bool flag = CrossingRules.Required(entry, section, crossing);
			dataGridViewCell.ReadOnly = !CanEdit || !flag;
			dataGridViewCell.Style.BackColor = ((flag && string.IsNullOrEmpty(Convert.ToString(dataGridViewCell.Value))) ? EdrPalette.Required : Color.White);
		}
	}

	private void ReadCrossingCells(JournalEntry entry, JournalEntry original, DataGridViewRow row)
	{
		Section section = main.Engine.Station.Sections.First((Section x) => x.Id == sectionId);
		foreach (DataGridViewColumn item in CrossingColumns())
		{
			CrossingSetting crossing = (CrossingSetting)item.Tag;
			string text = Convert.ToString(row.Cells[item.Index].Value).Trim();
			ClockCell clockCell = row.Cells[item.Index] as ClockCell;
			bool flag = clockCell != null && clockCell.EditedTime.HasValue && !SepeWriteRequest.SameMinute(clockCell.EditedTime, CrossingRules.Find(original, section, crossing).RecordedAt);
			if (!(text == CrossingRules.Value(original, section, crossing)) || flag)
			{
				if (!CrossingRules.Required(original, section, crossing))
				{
					throw new Exception("Ten dróżnik nie wymaga powiadomienia dla tego wiersza.");
				}
				DateTime? recorded;
				if (clockCell != null && clockCell.EditedTime.HasValue)
				{
					recorded = clockCell.EditedTime;
				}
				else
				{
					recorded = (CrossingRules.UsesTime(section, crossing) ? ClockDates.Resolve(text, entry.Time, CrossingRules.Find(original, section, crossing).RecordedAt, null, main.Engine.Now) : ((DateTime?)null));
				}
				CrossingRules.Set(entry, section, crossing, text, recorded);
			}
		}
	}

	public void RefreshSignals()
	{
		foreach (RailSignal signal in signals)
		{
			Section section = main.Engine.Station.Sections.First((Section x) => x.Id == sectionId);
			bool flag = SectionTrackDirections.Incoming(section, signal.Number);
			bool flag2 = main.Engine.CurrentBidirectional(sectionId, signal.Number);
			bool flag3 = TrafficBasis.IsTelephone(main.Engine.CurrentBasis(sectionId, signal.Number)) || TrafficBasis.IsTelephone(main.Engine.CurrentBasis(sectionId, signal.Number, "DO"));
			string text = main.Engine.LineStatus(sectionId, signal.Number);
			JournalEntry journalEntry = (from x in main.Engine.State.Entries
				where x.StationId == main.Engine.StationId && x.SectionId == sectionId && x.LineTrack == signal.Number && !x.Cancelled && x.Type == "Pociąg" && main.Engine.LineMovementVisible(x)
				orderby x.Left descending
				select x).FirstOrDefault();
			bool? flag4 = ((journalEntry == null) ? ((bool?)null) : new bool?(journalEntry.Direction == "Z"));
			if (signal.Incoming == flag && !(signal.Status != text))
			{
				bool? movementIncoming = signal.MovementIncoming;
				bool? flag5 = flag4;
				if (movementIncoming == true == (flag5 == true) && movementIncoming.HasValue == flag5.HasValue && signal.Telephone == flag3 && signal.Bidirectional == flag2)
				{
					continue;
				}
			}
			signal.Incoming = flag;
			signal.Status = text;
			signal.MovementIncoming = flag4;
			signal.Telephone = flag3;
			signal.Bidirectional = flag2;
			signal.Invalidate();
		}
	}

	private void EnableCellEditing()
	{
		if (sectionId == "TEL")
		{
			return;
		}
		Grid.ReadOnly = false;
		Grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
		Grid.EditMode = DataGridViewEditMode.EditOnEnter;
		Grid.Columns["indicator"].ReadOnly = true;
		Grid.DataError += (object s, DataGridViewDataErrorEventArgs e) =>
		{
			if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && Grid.Columns[e.ColumnIndex].CellTemplate is ClockCell)
			{
				e.Cancel = true;
				e.ThrowException = false;
				LastEditError = ((e.Exception == null) ? "Uzupełnij godzinę HH:mm." : e.Exception.Message);
				Grid.Rows[e.RowIndex].Cells[e.ColumnIndex].ErrorText = LastEditError;
			}
		};
		Grid.CellBeginEdit += (object s, DataGridViewCellCancelEventArgs e) =>
		{
			if (refreshing || e.RowIndex < 0)
			{
				e.Cancel = true;
			}
			else
			{
				DataGridViewRow dataGridViewRow = Grid.Rows[e.RowIndex];
				if (!(dataGridViewRow.Tag is JournalEntry { Cancelled: false } journalEntry) || journalEntry.Type != "Pociąg" || !CanEdit)
				{
					e.Cancel = true;
				}
				else
				{
					string name = Grid.Columns[e.ColumnIndex].Name;
					if ((name == "odd" || name == "even") && string.IsNullOrEmpty(Convert.ToString(dataGridViewRow.Cells[e.ColumnIndex].Value)))
					{
						e.Cancel = true;
					}
				}
			}
		};
		((ReferenceGrid)Grid).ClockAccepted += (object s, EventArgs e) =>
		{
			if (!refreshing && !committing)
			{
				CommitPending();
			}
		};
		Grid.CellEndEdit += (object s, DataGridViewCellEventArgs e) =>
		{
			if (!refreshing && e.RowIndex >= 0 && e.ColumnIndex >= 0)
			{
				ClockCell clockCell = Grid.Rows[e.RowIndex].Cells[e.ColumnIndex] as ClockCell;
				JournalEntry journalEntry = Grid.Rows[e.RowIndex].Tag as JournalEntry;
				if (clockCell != null && clockCell.EditedTime.HasValue && journalEntry != null)
				{
					dirty = true;
					dirtyId = journalEntry.Id;
				}
			}
		};
		Grid.CellValueChanged += (object s, DataGridViewCellEventArgs e) =>
		{
			if (!refreshing && e.RowIndex >= 0 && Grid.Rows[e.RowIndex].Tag is JournalEntry journalEntry)
			{
				dirty = true;
				dirtyId = journalEntry.Id;
			}
		};
		Grid.CellValidating += (object s, DataGridViewCellValidatingEventArgs e) =>
		{
			if (refreshing || e.RowIndex < 0 || !Grid.IsCurrentCellDirty)
			{
				return;
			}
			try
			{
				JournalEntry journalEntry = (JournalEntry)Grid.Rows[e.RowIndex].Tag;
				JournalPane journalPane = this;
				JournalEntry original = journalEntry;
				string name = Grid.Columns[e.ColumnIndex].Name;
				string value = ((!(Grid.Rows[e.RowIndex].Cells[e.ColumnIndex] is SlashCell)) ? Convert.ToString(e.FormattedValue) : (Convert.ToBoolean(e.FormattedValue) ? "/" : ""));
				journalPane.ReadCell(original, name, value);
				Grid.Rows[e.RowIndex].Cells[e.ColumnIndex].ErrorText = "";
				LastEditError = null;
			}
			catch (Exception ex)
			{
				e.Cancel = true;
				LastEditError = ex.Message;
				Grid.Rows[e.RowIndex].Cells[e.ColumnIndex].ErrorText = ex.Message;
			}
		};
		Grid.RowValidating += (object s, DataGridViewCellCancelEventArgs e) =>
		{
			if (!refreshing && !committing && dirty && !SaveDraft())
			{
				e.Cancel = true;
			}
		};
		Grid.Leave += (object s, EventArgs e) =>
		{
			if (!refreshing && !committing)
			{
				CommitPending();
			}
		};
		Grid.KeyDown += (object s, KeyEventArgs e) =>
		{
			if (e.Control && e.KeyCode == Keys.S)
			{
				e.SuppressKeyPress = true;
				CommitPending();
			}
			if (e.KeyCode == Keys.Escape && !Grid.IsCurrentCellInEditMode)
			{
				dirty = false;
				dirtyId = null;
				RefreshData();
			}
		};
	}

	private void ReadCell(JournalEntry original, string name, string value)
	{
		switch (name)
		{
		case "permission":
		case "left":
		case "arrived":
		{
			string value2 = value;
			DateTime time = original.Time;
			DateTime? previous;
			if (name == "permission")
			{
				previous = original.Permission;
			}
			else
			{
				previous = ((name == "left") ? original.Left : original.Arrived);
			}
			Engine.JournalTime(value2, time, previous, null);
			break;
		}
		}
		if (name == "track" && !StationSettings.TrackAllowed(main.Engine.Station, value, main.Engine.State.Entries.FirstOrDefault((JournalEntry x) => x.Id == original.Id)))
		{
			throw new Exception("Nieznany tor stacyjny: " + value);
		}
		if ((name == "odd" || name == "even") && string.IsNullOrWhiteSpace(value))
		{
			throw new Exception("Numer pociągu nie może być pusty.");
		}
		CrossingSetting crossingSetting = CrossingColumn(name);
		if (crossingSetting != null)
		{
			CrossingRules.ValidateValue(main.Engine.Station.Sections.First((Section x) => x.Id == sectionId), crossingSetting, value, original.Time);
		}
		if (name == "notify" && CrossingRules.All(main.Engine.Station.Sections.First((Section x) => x.Id == sectionId)).Count == 0 && value.Length > 0 && value != "/")
		{
			Engine.JournalTime(value, original.Time, null, null);
		}
	}

	private DateTime? ReadDraftClock(DataGridViewRow row, string name, string text, JournalEntry entry, DateTime? previous, DateTime? notBefore)
	{
		if (row.Cells[name] is ClockCell { EditedTime: not null, EditedTime: var editedTime } clockCell && editedTime.Value.ToString("HH:mm") == text)
		{
			return clockCell.EditedTime;
		}
		return ClockDates.Resolve(text, entry.Time, previous, notBefore, main.Engine.Now);
	}

	public void RefreshClockSuggestions()
	{
		DateTime now = main.Engine.Now;
		DateTime dateTime = now.Date.AddHours(now.Hour).AddMinutes(now.Minute);
		if (!(dateTime == lastClockCue))
		{
			lastClockCue = dateTime;
			Grid.Invalidate();
		}
	}

	private bool SaveDraft()
	{
		if (!dirty)
		{
			return true;
		}
		DataGridViewRow row = Grid.Rows.Cast<DataGridViewRow>().FirstOrDefault((DataGridViewRow r) => ((JournalEntry)r.Tag).Id == dirtyId);
		if (row == null)
		{
			return false;
		}
		try
		{
			JournalEntry original = (JournalEntry)row.Tag;
			JournalEntry entry = Json.Clone(main.Engine.State.Entries.FirstOrDefault((JournalEntry x) => x.Id == original.Id) ?? original);
			Func<string, string> func = (string name) => MergeWindowDraft(row, original, entry, name);
			string[] array = new string[6] { "track", "permission", "left", "arrived", "notes", "notify" };
			foreach (string text in array)
			{
				ReadCell(entry, text, func(text));
			}
			JournalEntry journalEntry = entry;
			string number;
			if (string.IsNullOrEmpty(entry.ReturnOfId))
			{
				number = ((func("odd").Length > 0) ? func("odd") : func("even"));
			}
			else
			{
				number = original.Number;
			}
			journalEntry.Number = number;
			if (entry.Number.Length == 0)
			{
				throw new Exception("Podaj numer pociągu.");
			}
			entry.Track = func("track");
			entry.Permission = ReadDraftClock(row, "permission", func("permission"), entry, entry.Permission, null);
			entry.Left = ReadDraftClock(row, "left", func("left"), entry, entry.Left, entry.Permission);
			entry.Arrived = ReadDraftClock(row, "arrived", func("arrived"), entry, entry.Arrived, entry.Left);
			entry.Text = func("notes");
			ReadCrossingCells(entry, original, row);
			if (CrossingRules.All(main.Engine.Station.Sections.First((Section x) => x.Id == sectionId)).Count == 0)
			{
				entry.Notification = func("notify");
			}
			if (entry.ReturnFromLine)
			{
				entry.ReturnArrival = main.Engine.State.Entries.First((JournalEntry x) => x.Id == entry.Id).ReturnArrival;
			}
			main.Engine.SaveJournal(entry);
			entry = Json.Clone(main.Engine.State.Entries.Single((JournalEntry x) => x.Id == entry.Id));
			row.Tag = entry;
			row.ErrorText = "";
			dirty = false;
			dirtyId = null;
			LastEditError = null;
			refreshing = true;
			row.Cells["permission"].Value = Ui.Time(entry.Permission);
			row.Cells["left"].Value = Ui.Time(entry.Left);
			row.Cells["arrived"].Value = Ui.Time(entry.Arrived);
			foreach (ClockCell item in row.Cells.Cast<DataGridViewCell>().OfType<ClockCell>())
			{
				item.EditedTime = null;
			}
			RefreshSavedWindowRow(row, entry);
			refreshing = false;
			UpdateRequiredCells(row, entry);
			main.JournalCommitted();
			return true;
		}
		catch (TrainTimeException error)
		{
			RestoreRejectedDraft(row, error);
			return true;
		}
		catch (Exception ex)
		{
			LastEditError = ex.Message;
			row.ErrorText = ex.Message;
			return false;
		}
	}

	private void RestoreRejectedDraft(DataGridViewRow row, TrainTimeException error)
	{
		JournalEntry previous = (JournalEntry)row.Tag;
		JournalEntry journalEntry = main.Engine.State.Entries.FirstOrDefault((JournalEntry x) => x.Id == previous.Id) ?? previous;
		refreshing = true;
		try
		{
			row.Tag = Json.Clone(journalEntry);
			row.Cells["track"].Value = journalEntry.Track;
			row.Cells["permission"].Value = Ui.Time(journalEntry.Permission);
			row.Cells["left"].Value = Ui.Time(journalEntry.Left);
			row.Cells["arrived"].Value = Ui.Time(journalEntry.Arrived);
			row.Cells["notes"].Value = journalEntry.Text;
			row.Cells["notify"].Value = journalEntry.Notification;
			FillCrossingCells(row, journalEntry);
			string[] array = new string[2] { "odd", "even" };
			foreach (string columnName in array)
			{
				if (!string.IsNullOrEmpty(Convert.ToString(row.Cells[columnName].Value)))
				{
					row.Cells[columnName].Value = journalEntry.Number;
				}
			}
			row.ErrorText = "";
			foreach (DataGridViewCell cell in row.Cells)
			{
				cell.ErrorText = "";
				if (cell is ClockCell clockCell)
				{
					clockCell.EditedTime = null;
				}
			}
			dirty = false;
			dirtyId = null;
			LastEditError = error.Message;
			UpdateRequiredCells(row, journalEntry);
		}
		finally
		{
			refreshing = false;
		}
		main.PostOperatorNotice("EDR - pociąg " + journalEntry.Number, "Pociąg " + journalEntry.Number + "\r\n" + error.Message);
	}

	public bool CommitPending()
	{
		if (refreshing || committing)
		{
			return true;
		}
		committing = true;
		try
		{
			if (Grid.IsCurrentCellInEditMode && !Grid.EndEdit())
			{
				return false;
			}
			return SaveDraft();
		}
		finally
		{
			committing = false;
		}
	}

	private void UpdateRequiredCells(DataGridViewRow row, JournalEntry en)
	{
		StyleCrossingCells(row, en);
		JournalTimeFields journalTimeFields = main.Engine.DocumentationFields(en);
		string[] array = new string[4] { "track", "permission", "left", "arrived" };
		foreach (string text in array)
		{
			bool flag = text switch
			{
				"left" => journalTimeFields.Left, 
				"permission" => journalTimeFields.Permission, 
				"track" => en.Direction == "Z", 
				_ => journalTimeFields.Arrived, 
			};
			row.Cells[text].Style.BackColor = ((flag && string.IsNullOrEmpty(Convert.ToString(row.Cells[text].Value))) ? EdrPalette.Required : Color.White);
		}
	}

	public void FocusEntry(string id)
	{
		DataGridViewRow dataGridViewRow = Grid.Rows.Cast<DataGridViewRow>().FirstOrDefault((DataGridViewRow r) => ((JournalEntry)r.Tag).Id == id);
		if (dataGridViewRow != null)
		{
			Control control = this;
			while (control != null && !(control is TabPage))
			{
				control = control.Parent;
			}
			if (control is TabPage)
			{
				((TabControl)control.Parent).SelectedTab = (TabPage)control;
			}
			Grid.CurrentCell = dataGridViewRow.Cells[(sectionId == "TEL") ? "text" : "track"];
			Grid.FirstDisplayedScrollingRowIndex = dataGridViewRow.Index;
			Grid.Focus();
		}
	}

	private object[] EntryCells(JournalEntry e)
	{
		if (sectionId == "TEL")
		{
			return new object[5]
			{
				e.Time.ToString("dd.MM HH:mm"),
				e.Type,
				TelephonePresentation.Column(e),
				e.Text,
				e.User
			};
		}
		if (e.Type != "Pociąg")
		{
			return new object[9] { "", "", "", "", "", "", "", e.Text, "" };
		}
		Section section = main.Engine.Station.Sections.First((Section x) => x.Id == sectionId);
		bool flag = WorkTrip.Even(e, section);
		string text = (string.IsNullOrEmpty(e.ReturnOfId) ? e.Number : "");
		return new object[9]
		{
			"",
			flag ? "" : text,
			flag ? text : "",
			e.Track,
			Ui.Time(e.Permission),
			Ui.Time(e.Left),
			Ui.Time(e.Arrived),
			e.Text,
			e.Notification
		}.Concat(CrossingColumns().Select((Func<DataGridViewColumn, object>)((DataGridViewColumn c) => CrossingRules.Value(e, main.Engine.Station.Sections.First((Section x) => x.Id == sectionId), (CrossingSetting)c.Tag)))).ToArray();
	}

	private void StyleMergedRow(DataGridViewRow row, JournalEntry entry)
	{
		row.Tag = entry;
		FillCrossingCells(row, entry);
		row.Height = (int)Math.Round((float)((entry.Type == "Pociąg") ? 25 : 32) * (main.CurrentZoom / 1.375f));
		row.ReadOnly = !CanEdit || entry.Cancelled || entry.Type != "Pociąg";
		row.DefaultCellStyle.BackColor = ((entry.Type == "Pociąg") ? Color.White : Color.FromArgb(233, 233, 233));
		row.DefaultCellStyle.ForeColor = (entry.Cancelled ? Color.Gray : Color.Black);
		if (entry.Cancelled)
		{
			row.DefaultCellStyle.Font = new Font(Grid.Font, FontStyle.Strikeout);
		}
		else
		{
			row.DefaultCellStyle.Font = null;
		}
		if (entry.Type == "Pociąg")
		{
			UpdateRequiredCells(row, entry);
			row.Cells["notes"].Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
		}
	}

	private void MergeJournalEntries()
	{
		List<JournalEntry> list = WorkTrip.Order(main.Engine.State.Entries.Where((JournalEntry e) => e.StationId == main.Engine.StationId && e.SectionId == sectionId && e.Time.Date == day.Value.Date));
		if (sectionId != "TEL")
		{
			list.Insert(0, new JournalEntry
			{
				Id = "day-" + sectionId + day.Value.ToString("yyyyMMdd"),
				Time = day.Value.Date,
				Type = "Doba",
				Text = "Północ dnia " + day.Value.ToString("dd-MM-yyyy")
			});
		}
		refreshing = true;
		try
		{
			if (sectionId != "TEL")
			{
				Grid.Columns["indicator"].Visible = list.Any((JournalEntry x) => x.ReturnFromLine || !string.IsNullOrEmpty(x.ReturnOfId));
			}
			for (int num = 0; num < list.Count; num++)
			{
				JournalEntry entry = list[num];
				DataGridViewRow dataGridViewRow = Grid.Rows.Cast<DataGridViewRow>().FirstOrDefault((DataGridViewRow r) => r.Tag is JournalEntry && ((JournalEntry)r.Tag).Id == entry.Id);
				if (dataGridViewRow == null)
				{
					Grid.Rows.Insert(Math.Min(num, Grid.Rows.Count), EntryCells(entry));
					dataGridViewRow = Grid.Rows[Math.Min(num, Grid.Rows.Count - 1)];
					StyleMergedRow(dataGridViewRow, entry);
				}
				else if (entry.Id != dirtyId && (!Grid.IsCurrentCellInEditMode || Grid.CurrentCell.RowIndex != dataGridViewRow.Index))
				{
					JournalEntry value = (JournalEntry)dataGridViewRow.Tag;
					if (Json.Text(value) != Json.Text(entry))
					{
						object[] array = EntryCells(entry);
						for (int num2 = 0; num2 < array.Length; num2++)
						{
							dataGridViewRow.Cells[num2].Value = array[num2];
						}
						StyleMergedRow(dataGridViewRow, entry);
					}
					else if (entry.Type == "Pociąg")
					{
						UpdateRequiredCells(dataGridViewRow, entry);
					}
				}
				else if (entry.Type == "Pociąg")
				{
					UpdateRequiredCells(dataGridViewRow, entry);
				}
				bool flag = !CanEdit || entry.Cancelled || entry.Type != "Pociąg";
				if (dataGridViewRow.ReadOnly != flag)
				{
					dataGridViewRow.ReadOnly = flag;
				}
			}
			List<string> list2 = list.Select((JournalEntry x) => x.Id).ToList();
			DataGridViewRow[] array2 = Grid.Rows.Cast<DataGridViewRow>().ToArray();
			foreach (DataGridViewRow dataGridViewRow2 in array2)
			{
				if (dataGridViewRow2.Tag is JournalEntry journalEntry && !list2.Contains(journalEntry.Id) && journalEntry.Id != dirtyId && (!Grid.IsCurrentCellInEditMode || Grid.CurrentCell.RowIndex != dataGridViewRow2.Index))
				{
					Grid.Rows.Remove(dataGridViewRow2);
				}
			}
		}
		finally
		{
			refreshing = false;
		}
		Grid.Invalidate();
	}

	private void PaintReturnBrace(object sender, DataGridViewRowPostPaintEventArgs e)
	{
		if (sectionId == "TEL" || !Grid.Columns["indicator"].Visible || !(Grid.Rows[e.RowIndex].Tag is JournalEntry journalEntry) || (!journalEntry.ReturnFromLine && string.IsNullOrEmpty(journalEntry.ReturnOfId)))
		{
			return;
		}
		int num = (journalEntry.ReturnFromLine ? e.RowIndex : (e.RowIndex - 1));
		if (num < 0 || num + 1 >= Grid.Rows.Count)
		{
			return;
		}
		JournalEntry journalEntry2 = Grid.Rows[num].Tag as JournalEntry;
		JournalEntry journalEntry3 = Grid.Rows[num + 1].Tag as JournalEntry;
		if (journalEntry2 == null || journalEntry3 == null || journalEntry3.ReturnOfId != journalEntry2.Id)
		{
			return;
		}
		Rectangle cellDisplayRectangle = Grid.GetCellDisplayRectangle(Grid.Columns["indicator"].Index, e.RowIndex, cutOverflow: false);
		float num2 = (journalEntry.ReturnFromLine ? e.RowBounds.Top : (e.RowBounds.Top - Grid.Rows[num].Height));
		float num3 = num2 + (float)Grid.Rows[num].Height + (float)Grid.Rows[num + 1].Height;
		float num4 = (float)cellDisplayRectangle.Left + (float)cellDisplayRectangle.Width * 0.72f;
		float num5 = (float)cellDisplayRectangle.Left + (float)cellDisplayRectangle.Width * 0.25f;
		float num6 = (num2 + num3) / 2f;
		float num7 = (float)cellDisplayRectangle.Left + (float)cellDisplayRectangle.Width * 0.48f;
		GraphicsState gstate = e.Graphics.Save();
		e.Graphics.SetClip(Rectangle.Intersect(cellDisplayRectangle, e.RowBounds));
		e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
		using (GraphicsPath graphicsPath = new GraphicsPath())
		{
			using Pen pen = new Pen(journalEntry.Cancelled ? Color.Gray : Color.FromArgb(55, 55, 35), Math.Max(1f, main.CurrentZoom));
			graphicsPath.AddBezier(num4, num2 + 2f, num7, num2 + 2f, num7, num2 + 4f, num7, num6 - 5f);
			graphicsPath.AddBezier(num7, num6 - 5f, num7, num6 - 1f, num5, num6 - 1f, num5, num6);
			graphicsPath.AddBezier(num5, num6, num7, num6 + 1f, num7, num6 + 2f, num7, num6 + 5f);
			graphicsPath.AddBezier(num7, num6 + 5f, num7, num3 - 4f, num7, num3 - 2f, num4, num3 - 2f);
			e.Graphics.DrawPath(pen, graphicsPath);
		}
		e.Graphics.Restore(gstate);
	}

	private string DraftCell(JournalEntry entry, string name)
	{
		switch (name)
		{
		case "track":
			return entry.Track ?? "";
		case "permission":
			return Ui.Time(entry.Permission);
		case "left":
			return Ui.Time(entry.Left);
		case "arrived":
			return Ui.Time(entry.Arrived);
		case "notes":
			return entry.Text ?? "";
		case "notify":
			return entry.Notification ?? "";
		case "odd":
		case "even":
		{
			bool flag = WorkTrip.Even(entry, main.Engine.Station.Sections.First((Section x) => x.Id == sectionId));
			if (name == "even" != flag)
			{
				return "";
			}
			return entry.Number ?? "";
		}
		default:
			return "";
		}
	}

	private string MergeWindowDraft(DataGridViewRow row, JournalEntry original, JournalEntry latest, string name)
	{
		string text = Convert.ToString(row.Cells[name].Value).Trim();
		if (!(text == DraftCell(original, name).Trim()))
		{
			return text;
		}
		return DraftCell(latest, name).Trim();
	}

	private void RefreshSavedWindowRow(DataGridViewRow row, JournalEntry entry)
	{
		object[] array = EntryCells(entry);
		for (int i = 0; i < array.Length; i++)
		{
			row.Cells[i].Value = array[i];
		}
	}

	public void DiscardPending()
	{
		refreshing = true;
		dirty = false;
		dirtyId = null;
		Grid.CancelEdit();
	}

	public JournalPane(MainForm owner, string section)
	{
		JournalPane journalPane = this;
		main = owner;
		sectionId = section;
		Dock = DockStyle.Fill;
		Margin = Padding.Empty;
		Padding = Padding.Empty;
		BackColor = Color.White;
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Left,
			Width = ((!(section == "TEL")) ? 46 : 0),
			WrapContents = false,
			Margin = Padding.Empty,
			Padding = Padding.Empty
		};
		if (section != "TEL")
		{
			Section section2 = main.Engine.Station.Sections.First((Section x) => x.Id == section);
			flowLayoutPanel.Width = section2.Tracks * 40;
			foreach (int item3 in SectionTrackDirections.DisplayOrder(section2))
			{
				RailSignal railSignal = new RailSignal
				{
					Number = item3,
					Incoming = SectionTrackDirections.Incoming(section2, item3)
				};
				signals.Add(railSignal);
				flowLayoutPanel.Controls.Add(railSignal);
			}
		}
		heading = new Label
		{
			Font = new Font("Arial", 12f, FontStyle.Bold),
			Dock = DockStyle.Fill,
			TextAlign = ((section == "TEL") ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleCenter),
			Margin = Padding.Empty
		};
		ToolStrip toolStrip = ReferenceTheme.Strip(26);
		toolStrip.Dock = DockStyle.Right;
		toolStrip.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;
		toolStrip.BackColor = Color.White;
		List<ToolStripButton> list = new List<ToolStripButton>();
		if (section != "TEL")
		{
			list.Add(ReferenceTheme.Icon("receive", "Przyjmij pociąg", () =>
			{
				main.EditJournal(null, null, "Z", sectionId);
			}));
			list.Add(ReferenceTheme.Icon("dispatch", "Wypraw pociąg", () =>
			{
				main.EditJournal(null, null, "DO", sectionId);
			}));
			if (CrossingRules.HasCrossings(main.Engine.Station.Sections.First((Section x) => x.Id == section)))
			{
				list.Add(ReferenceTheme.Icon("crossing", "Powiadom dróżników", () =>
				{
					main.NotifyCrossings(Selected());
				}));
			}
		}
		if (section == "TEL")
		{
			ToolStripButton item = Ui.Tool("Rejestruj telefonogram", "", () =>
			{
				main.Telephone(sectionId);
			});
			ToolStripButton item2 = Ui.Tool("Rejestruj inne okoliczności", "", () =>
			{
				main.FreeNote(sectionId);
			});
			list.Add(item);
			list.Add(item2);
		}
		else
		{
			list.Add(ReferenceTheme.Icon("telephone", "Zarejestruj telefonogram", () =>
			{
				main.Telephone(sectionId);
			}));
			list.Add(ReferenceTheme.Icon("info", "Wpis o dowolnej treści", () =>
			{
				main.FreeNote(sectionId);
			}));
			list.Add(ReferenceTheme.Icon("settings", "Bieżąca sytuacja ruchowa", () =>
			{
				main.Situation();
			}));
		}
		toolStrip.CanOverflow = false;
		foreach (ToolStripButton item4 in list)
		{
			toolStrip.Items.Add(item4);
		}
		JournalHeaderBar value = new JournalHeaderBar(heading, flowLayoutPanel, toolStrip);
		Grid = Ui.Grid(journal: true);
		ReferenceTheme.Grid(Grid, journal: true);
		Grid.DefaultCellStyle.ForeColor = Color.Black;
		Grid.RowTemplate.Height = 19;
		Grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(255, 228, 163);
		Grid.ColumnHeadersVisible = section == "TEL";
		if (section == "TEL")
		{
			Grid.Tag = "telephone";
			Grid.ColumnHeadersHeight = 22;
			Ui.Column(Grid, "ordinal", "Nr kol.", 70);
			Ui.Column(Grid, "date", "Data", 90);
			Ui.Column(Grid, "text", "Adres i treść", 480);
			Ui.Column(Grid, "time", "Czas zakończenia", 110);
			Ui.Column(Grid, "recipient", "Nadał / Odebrał", 190);
			Grid.Columns["text"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
			Grid.Columns["text"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
		}
		else
		{
			Ui.Column(Grid, "indicator", "", 14);
			Ui.Column(Grid, "odd", "Nr pociągu", 58);
			Ui.Column(Grid, "even", "Nr pociągu", 58);
			Ui.Column(Grid, "track", "Tor stacy.", 42);
			Ui.Column(Grid, "permission", "Droga wolna", 54);
			Ui.Column(Grid, "left", "Poc. odjechał", 65);
			Ui.Column(Grid, "arrived", "Poc. przyjech.", 66);
			Ui.Column(Grid, "notes", "Uwagi", 195);
			Ui.Column(Grid, "notify", "O jeździe pociągu zawiadomiono", 107);
			Grid.Columns["notes"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
			Grid.Columns["notes"].MinimumWidth = 70;
			Grid.Columns["indicator"].Visible = false;
			Grid.Columns["notify"].Visible = main.Engine.Station.Sections.First((Section x) => x.Id == section).NotifyCrossings;
			AddCrossingColumns();
		}
		if (section == "TEL")
		{
			Grid.CellDoubleClick += (object s, DataGridViewCellEventArgs e) =>
			{
				if (e.RowIndex >= 0)
				{
					EditSelected();
				}
			};
		}
		else
		{
			Grid.Columns["track"].CellTemplate = new StationTrackCell
			{
				Engine = main.Engine
			};
			string[] array = new string[3] { "permission", "left", "arrived" };
			foreach (string columnName in array)
			{
				Grid.Columns[columnName].CellTemplate = new ClockCell
				{
					Clock = () => main.Engine.Now,
					CanSuggest = () => CanEdit
				};
			}
			EnableCellEditing();
		}
		ContextMenuStrip context = new ContextMenuStrip();
		context.Items.Add(Ui.Menu("Edytuj wpis", () =>
		{
			EditSelected();
		}));
		context.Items.Add(Ui.Menu("Wykreśl błędny wpis…", () =>
		{
			JournalEntry journalEntry = Selected();
			if (journalEntry != null)
			{
				main.CancelEntry(journalEntry);
			}
		}));
		context.Items.Add(Ui.Menu("Historia wpisu", () =>
		{
			JournalEntry journalEntry = Selected();
			if (journalEntry != null)
			{
				main.History(journalEntry.Id);
			}
		}));
		context.Opening += (object s, CancelEventArgs e) =>
		{
			JournalEntry journalEntry = journalPane.Selected();
			bool enabled = journalEntry != null && !journalEntry.Cancelled && journalEntry.Type != "Doba" && journalEntry.Type != "Służbowy" && journalPane.CanEdit;
			context.Items[0].Enabled = enabled;
			context.Items[1].Enabled = enabled;
			context.Items[2].Enabled = journalEntry != null && journalEntry.Type != "Doba";
		};
		Grid.ContextMenuStrip = context;
		Grid.CellToolTipTextNeeded += (object s, DataGridViewCellToolTipTextNeededEventArgs e) =>
		{
			if (e.RowIndex >= 0 && Grid.Rows[e.RowIndex].Tag is JournalEntry { Type: "Telefonogram" } journalEntry)
			{
				e.ToolTipText = journalEntry.Text + "\n" + TelephonePresentation.JournalLine(journalEntry);
			}
		};
		Grid.CellMouseDown += (object s, DataGridViewCellMouseEventArgs e) =>
		{
			if (e.RowIndex >= 0 && e.Button == MouseButtons.Right)
			{
				Grid.CurrentCell = ((e.ColumnIndex >= 0 && Grid.Columns[e.ColumnIndex].Visible) ? Grid.Rows[e.RowIndex].Cells[e.ColumnIndex] : Grid.Rows[e.RowIndex].Cells.Cast<DataGridViewCell>().First((DataGridViewCell c) => c.Visible));
			}
		};
		if (section != "TEL")
		{
			Grid.AllowDrop = true;
			Grid.DragEnter += (object s, DragEventArgs e) =>
			{
				if (e.Data.GetDataPresent(DataFormats.Text))
				{
					e.Effect = DragDropEffects.Copy;
				}
			};
			Grid.DragDrop += (object s, DragEventArgs e) =>
			{
				Ui.Run(this, () =>
				{
					string id = e.Data.GetData(DataFormats.Text) as string;
					Train train = main.Engine.State.Trains.FirstOrDefault((Train x) => x.Id == id);
					if (train != null)
					{
						main.QuickRegister(train, sectionId);
					}
				});
			};
			Grid.RowPostPaint += PaintNote;
			Grid.RowPostPaint += PaintReturnBrace;
		}
		else
		{
			Grid.RowPostPaint += PaintTelephoneDivider;
		}
		Panel panel = new Panel
		{
			Dock = DockStyle.Bottom,
			Height = 20,
			BackColor = Color.White
		};
		ToolStrip toolStrip2 = ReferenceTheme.Strip(20);
		toolStrip2.Dock = DockStyle.Left;
		toolStrip2.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;
		toolStrip2.Width = 242;
		toolStrip2.BackColor = panel.BackColor;
		toolStrip2.Items.Add(Nav("|<", "Pierwszy dzień", () =>
		{
			SetDay(FirstDay());
		}));
		toolStrip2.Items.Add(Nav("<", "Poprzedni dzień", () =>
		{
			SetDay(day.Value.AddDays(-1.0));
		}));
		day = new CompactDatePicker
		{
			Value = main.Engine.Now.Date,
			CustomFormat = "dd.MM.yyyy",
			Width = 94,
			Height = 16
		};
		toolStrip2.Items.Add(new ToolStripControlHost(day)
		{
			AutoSize = false,
			Size = new Size(94, 16),
			Margin = Padding.Empty,
			Padding = Padding.Empty
		});
		day.ValueChanged += (object s, EventArgs e) =>
		{
			if (!refreshing)
			{
				RefreshData();
			}
		};
		toolStrip2.Items.Add(Nav(">", "Następny dzień", () =>
		{
			SetDay(day.Value.AddDays(1.0));
		}));
		toolStrip2.Items.Add(Nav(">|", "Bieżący dzień", () =>
		{
			SetDay(main.Engine.Now.Date);
		}));
		toolStrip2.Items.Add(Nav("∧", "Pierwszy wpis", () =>
		{
			if (Grid.Rows.Count > 0)
			{
				Grid.FirstDisplayedScrollingRowIndex = 0;
			}
		}));
		toolStrip2.Items.Add(Nav("∨", "Ostatni wpis", () =>
		{
			if (Grid.Rows.Count > 0)
			{
				Grid.FirstDisplayedScrollingRowIndex = Grid.Rows.Count - 1;
			}
		}));
		operatorLabel = new Label
		{
			Dock = DockStyle.Fill,
			Font = ReferenceTheme.Small,
			TextAlign = ContentAlignment.MiddleCenter
		};
		SilverButton silverButton = new SilverButton
		{
			Text = "Zapisz",
			Dock = DockStyle.Right,
			Width = 84,
			Font = ReferenceTheme.Small
		};
		silverButton.Click += (object s, EventArgs e) =>
		{
			Ui.Run(this, () =>
			{
				if (!CommitPending())
				{
					throw new Exception(LastEditError ?? "Popraw wpis przed zapisaniem.");
				}
				RefreshData();
			});
		};
		panel.Controls.Add(operatorLabel);
		panel.Controls.Add(toolStrip2);
		panel.Controls.Add(silverButton);
		Panel panel2 = new Panel
		{
			Dock = DockStyle.Fill,
			Padding = new Padding(1, 0, 1, 0)
		};
		panel2.Controls.Add(Grid);
		if (section != "TEL")
		{
			panel2.Controls.Add(new JournalHeader(Grid));
		}
		Controls.Add(panel2);
		Controls.Add(panel);
		Controls.Add(value);
		Grid.CellFormatting += FormatInactiveTime;
		RefreshData();
	}

	private ToolStripButton Nav(string text, string hint, Action action)
	{
		ToolStripButton toolStripButton = Ui.Tool(text, hint, action);
		toolStripButton.AutoSize = false;
		toolStripButton.Size = new Size(24, 16);
		toolStripButton.Margin = new Padding(1, 0, 0, 0);
		toolStripButton.Font = ReferenceTheme.Small;
		toolStripButton.ForeColor = EdrPalette.Navy;
		return toolStripButton;
	}

	private DateTime FirstDay()
	{
		List<JournalEntry> list = main.Engine.State.Entries.Where((JournalEntry e) => e.StationId == main.Engine.StationId && e.SectionId == sectionId).ToList();
		if (list.Count != 0)
		{
			return list.Min((JournalEntry e) => e.Time).Date;
		}
		return main.Engine.Now.Date;
	}

	public void SetDay(DateTime value)
	{
		if (CommitPending())
		{
			refreshing = true;
			day.Value = ((value.Date > main.Engine.Now.Date) ? main.Engine.Now.Date : value.Date);
			refreshing = false;
			RefreshData();
		}
	}

	public JournalEntry Selected()
	{
		if (Grid.CurrentRow != null)
		{
			return Grid.CurrentRow.Tag as JournalEntry;
		}
		return null;
	}

	private void EditSelected()
	{
		if (!CommitPending())
		{
			return;
		}
		JournalEntry journalEntry = Selected();
		if (journalEntry != null && !(journalEntry.Type == "Doba"))
		{
			if (journalEntry.Type == "Pociąg")
			{
				main.EditJournal(journalEntry, null, journalEntry.Direction, sectionId);
			}
			else if (journalEntry.Type == "Telefonogram")
			{
				main.Telephone(sectionId, journalEntry);
			}
			else
			{
				main.FreeNote(sectionId, journalEntry);
			}
		}
	}

	public void RefreshData()
	{
		if (IsDisposed)
		{
			return;
		}
		RefreshSignals();
		if (dirty || Grid.IsCurrentCellInEditMode)
		{
			MergeJournalEntries();
			return;
		}
		refreshing = true;
		string selected = ((Selected() == null) ? null : Selected().Id);
		string text = ((Grid.CurrentCell == null) ? null : Grid.CurrentCell.OwningColumn.Name);
		int firstDisplayedScrollingRowIndex = Grid.FirstDisplayedScrollingRowIndex;
		Engine engine = main.Engine;
		Section section = engine.Station.Sections.FirstOrDefault((Section x) => x.Id == sectionId);
		heading.Text = ((section == null) ? "Dziennik telefoniczny" : StationSettings.Title(section));
		heading.Tag = ((section == null) ? "" : (section.Name + " / linia " + section.Line));
		Grid.Rows.Clear();
		List<JournalEntry> source = (from e in engine.State.Entries
			where e.StationId == engine.StationId && e.SectionId == sectionId && e.Time.Date == day.Value.Date
			orderby e.Time
			select e).ToList();
		source = WorkTrip.Order(source);
		if (sectionId != "TEL")
		{
			Grid.Columns["indicator"].Visible = source.Any((JournalEntry x) => x.ReturnFromLine || !string.IsNullOrEmpty(x.ReturnOfId));
		}
		source.Insert(0, new JournalEntry
		{
			Id = "day-" + sectionId + day.Value.ToString("yyyyMMdd"),
			Time = day.Value.Date,
			Type = "Doba",
			Text = "Północ dnia " + day.Value.ToString("dd-MM-yyyy")
		});
		Dictionary<string, int> dictionary = TelephoneNumbering.Numbers(engine.State, engine.Station);
		foreach (JournalEntry item in source)
		{
			int index;
			if (sectionId == "TEL")
			{
				dictionary.TryGetValue(item.Id, out var value);
				index = Grid.Rows.Add((item.Type == "Doba") ? "" : value.ToString(), (item.Type == "Doba") ? "" : item.Time.ToString("dd-MM-yyyy"), item.Text, (item.Type == "Doba") ? "" : item.Time.ToString("HH:mm"), TelephonePresentation.Column(item));
			}
			else if (item.Type == "Pociąg")
			{
				bool flag = WorkTrip.Even(item, section);
				string text2 = (string.IsNullOrEmpty(item.ReturnOfId) ? item.Number : "");
				index = Grid.Rows.Add("", flag ? "" : text2, flag ? text2 : "", item.Track, Ui.Time(item.Permission), Ui.Time(item.Left), Ui.Time(item.Arrived), item.Text, item.Notification);
			}
			else
			{
				index = Grid.Rows.Add("", "", "", "", "", "", "", item.Text, "");
			}
			DataGridViewRow dataGridViewRow = Grid.Rows[index];
			dataGridViewRow.Tag = item;
			FillCrossingCells(dataGridViewRow, item);
			dataGridViewRow.DefaultCellStyle.BackColor = Color.White;
			dataGridViewRow.Height = (int)Math.Round((float)((item.Type == "Pociąg") ? 25 : 32) * (main.CurrentZoom / 1.375f));
			dataGridViewRow.ReadOnly = !CanEdit || item.Cancelled || item.Type != "Pociąg";
			if (item.Type != "Pociąg")
			{
				dataGridViewRow.DefaultCellStyle.BackColor = Color.FromArgb(233, 233, 233);
			}
			else if (sectionId != "TEL")
			{
				UpdateRequiredCells(dataGridViewRow, item);
				dataGridViewRow.Cells["notes"].Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
			}
			if (sectionId == "TEL" && item.Type != "Doba")
			{
				dataGridViewRow.DefaultCellStyle.BackColor = Color.White;
			}
			if (item.Cancelled)
			{
				dataGridViewRow.DefaultCellStyle.Font = new Font(Grid.Font, FontStyle.Strikeout);
				dataGridViewRow.DefaultCellStyle.ForeColor = Color.Gray;
				dataGridViewRow.Cells[0].ToolTipText = "Wykreślono: " + item.CancelReason;
			}
		}
		refreshing = false;
		operatorLabel.Text = "Prowadzi: " + ((engine.ActiveDuty == null) ? "-" : engine.ActiveDuty.UserName);
		if (Grid.Rows.Count <= 0)
		{
			return;
		}
		DataGridViewRow dataGridViewRow2 = Grid.Rows.Cast<DataGridViewRow>().FirstOrDefault((DataGridViewRow r) => ((JournalEntry)r.Tag).Id == selected);
		if (dataGridViewRow2 != null && text != null)
		{
			Grid.CurrentCell = dataGridViewRow2.Cells[text];
			if (firstDisplayedScrollingRowIndex >= 0 && firstDisplayedScrollingRowIndex < Grid.Rows.Count)
			{
				Grid.FirstDisplayedScrollingRowIndex = firstDisplayedScrollingRowIndex;
			}
		}
		else
		{
			Grid.ClearSelection();
			if (Grid.IsHandleCreated && Grid.Visible && Grid.ClientSize.Height > Grid.RowTemplate.Height)
			{
				Grid.FirstDisplayedScrollingRowIndex = Grid.Rows.Count - 1;
			}
		}
	}

	private void PaintTelephoneDivider(object sender, DataGridViewRowPostPaintEventArgs e)
	{
		if (Grid.Rows[e.RowIndex].Tag is JournalEntry journalEntry && !(journalEntry.Type != "Doba"))
		{
			Rectangle rectangle = new Rectangle(e.RowBounds.X, e.RowBounds.Y, e.RowBounds.Width - 1, e.RowBounds.Height - 1);
			using (SolidBrush brush = new SolidBrush(Color.FromArgb(233, 233, 233)))
			{
				e.Graphics.FillRectangle(brush, rectangle);
			}
			TextRenderer.DrawText(e.Graphics, journalEntry.Text, Grid.Font, rectangle, Color.Black, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
		}
	}

	private string NoteDisplay(JournalEntry entry)
	{
		string text = entry.Text ?? "";
		if (text.StartsWith("Dyżur przyjął:") || text.StartsWith("Dyżur zdał:"))
		{
			text = text.Replace(":", "") + " dnia " + entry.Time.ToString("dd-MM-yyyy") + " o godzinie " + entry.Time.ToString("HH:mm");
		}
		return text;
	}

	private void FormatInactiveTime(object sender, DataGridViewCellFormattingEventArgs e)
	{
		if (e.RowIndex >= 0 && !(sectionId == "TEL") && Grid.Rows[e.RowIndex].Tag is JournalEntry journalEntry && !(journalEntry.Type != "Pociąg") && string.IsNullOrEmpty(Convert.ToString(e.Value)))
		{
			string name = Grid.Columns[e.ColumnIndex].Name;
			JournalTimeFields journalTimeFields = main.Engine.DocumentationFields(journalEntry);
			if ((!string.IsNullOrEmpty(journalEntry.ReturnOfId) && (name == "permission" || name == "left")) || (name == "left" && !journalTimeFields.Left) || (name == "arrived" && !journalTimeFields.Arrived))
			{
				e.Value = "---";
				e.FormattingApplied = true;
			}
		}
	}

	private void PaintNote(object sender, DataGridViewRowPostPaintEventArgs e)
	{
		if (!(Grid.Rows[e.RowIndex].Tag is JournalEntry { Type: not "Pociąg" } journalEntry))
		{
			return;
		}
		Rectangle rectangle = new Rectangle(e.RowBounds.X, e.RowBounds.Y, e.RowBounds.Width - 1, e.RowBounds.Height - 1);
		if (Grid.Rows[e.RowIndex].Selected || (Grid.CurrentCell != null && Grid.CurrentCell.RowIndex == e.RowIndex && Grid.SelectedCells.Count > 0))
		{
			ReferenceTheme.Gradient(e.Graphics, rectangle, Color.FromArgb(255, 246, 207), Color.FromArgb(255, 218, 140));
		}
		else
		{
			using SolidBrush brush = new SolidBrush(Color.FromArgb(233, 233, 233));
			e.Graphics.FillRectangle(brush, rectangle);
		}
		string text = TelephonePresentation.JournalLine(journalEntry);
		Rectangle bounds = rectangle;
		TextFormatFlags flags = TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;
		using Font font = new Font(Grid.Font, (!journalEntry.Cancelled) ? FontStyle.Bold : FontStyle.Strikeout);
		using Font font2 = new Font(Grid.Font.FontFamily, Grid.Font.Size * 0.85f, journalEntry.Cancelled ? FontStyle.Strikeout : FontStyle.Regular);
		if (text.Length > 0)
		{
			int num = font2.Height + 1;
			bounds.Height = Math.Max(font.Height, rectangle.Height - num);
			TextRenderer.DrawText(bounds: new Rectangle(rectangle.X + 2, rectangle.Bottom - num, rectangle.Width - 4, num), dc: e.Graphics, text: text, font: font2, foreColor: Color.Gray, flags: flags);
		}
		TextRenderer.DrawText(e.Graphics, NoteDisplay(journalEntry) + (journalEntry.Cancelled ? (" [WYKREŚLONO: " + journalEntry.CancelReason + "]") : ""), font, bounds, journalEntry.Cancelled ? Color.Gray : Color.Black, flags);
	}
}

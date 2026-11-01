using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace EdrLocal;

public class ClockCell : DataGridViewTextBoxCell
{
	public Func<DateTime> Clock { get; set; }

	public Func<bool> CanSuggest { get; set; }

	public DateTime? EditedTime { get; set; }

	public string SuggestionText => "";

	public override Type EditType => typeof(ClockEditingControl);

	public override object Clone()
	{
		ClockCell clockCell = (ClockCell)base.Clone();
		clockCell.Clock = Clock;
		clockCell.CanSuggest = CanSuggest;
		clockCell.EditedTime = null;
		return clockCell;
	}

	public DateTime? SourceTime()
	{
		JournalEntry journalEntry = ((OwningRow == null) ? null : (OwningRow.Tag as JournalEntry));
		if (journalEntry == null)
		{
			return null;
		}
		string name = OwningColumn.Name;
		return ((EditedTime.HasValue && EditedTime.Value.ToString("HH:mm") == Convert.ToString(Value)) ? EditedTime : ((DateTime?)null)) ?? (name switch
		{
			"arrived" => journalEntry.Arrived, 
			"left" => journalEntry.Left, 
			"permission" => journalEntry.Permission, 
			_ => null, 
		});
	}

	public DateTime? Resolve(string text)
	{
		JournalEntry journalEntry = ((OwningRow == null) ? null : (OwningRow.Tag as JournalEntry));
		DateTime dateTime = ((Clock == null) ? DateTime.Now : Clock());
		return ClockDates.Resolve(text, journalEntry?.Time ?? dateTime, SourceTime(), null, dateTime);
	}

	public override void InitializeEditingControl(int rowIndex, object value, DataGridViewCellStyle style)
	{
		base.InitializeEditingControl(rowIndex, value, style);
		ClockEditingControl editor = (ClockEditingControl)DataGridView.EditingControl;
		editor.Clock = Clock;
		editor.ValueProvider = () => Resolve(editor.Text);
		if (EditedTime.HasValue && EditedTime.Value.ToString("HH:mm") != Convert.ToString(Value))
		{
			EditedTime = null;
		}
		editor.ResetTime(Convert.ToString(Value), EditedTime);
		editor.EditingControlValueChanged = false;
		DataGridView.NotifyCurrentCellDirty(dirty: false);
	}

	public override object ParseFormattedValue(object value, DataGridViewCellStyle style, TypeConverter formattedConverter, TypeConverter valueConverter)
	{
		string text = ClockMask.Value(Convert.ToString(value));
		Engine.JournalTime(text, DateTime.Today, null, null);
		ClockEditingControl clockEditingControl = ((DataGridView == null) ? null : (DataGridView.EditingControl as ClockEditingControl));
		EditedTime = ((clockEditingControl != null && clockEditingControl.Text == text) ? clockEditingControl.LastChosenTime : ((DateTime?)null));
		ErrorText = "";
		return text;
	}
}

using System;
using System.Globalization;
using System.Windows.Forms;

namespace EdrLocal;

public class ClockEditingControl : DataGridViewTextBoxEditingControl
{
	private readonly ClockMask mask;

	private bool writing;

	private int wheelRemainder;

	private readonly ClockCompletion completion;

	public Func<DateTime> Clock { get; set; }

	public Func<DateTime?> ValueProvider { get; set; }

	public DateTime? LastChosenTime { get; private set; }

	public bool IsSuggestion
	{
		get
		{
			if (completion != null)
			{
				return completion.Visible;
			}
			return false;
		}
	}

	public string ValueText
	{
		get
		{
			if (!IsSuggestion)
			{
				return ClockMask.Value(Text);
			}
			return "";
		}
	}

	public string SuggestionText => completion.SuggestionText;

	public override object EditingControlFormattedValue
	{
		get
		{
			if (!IsSuggestion)
			{
				return ClockMask.Value(Text);
			}
			return "";
		}
		set
		{
			ResetTime(Convert.ToString(value));
		}
	}

	public ClockEditingControl()
	{
		mask = new ClockMask(this);
		TextAlign = HorizontalAlignment.Center;
		Func<Func<DateTime>> source = () => Clock;
		completion = new ClockCompletion(this, source, () => !ReadOnly && EditingControlDataGridView != null && EditingControlDataGridView.IsCurrentCellInEditMode, RenderSuggestion, (DateTime time) =>
		{
			Choose(time);
		});
	}

	public void ResetTime(string value, DateTime? chosen = null)
	{
		if (completion != null)
		{
			completion.Forget();
		}
		writing = true;
		try
		{
			mask.Reset(value);
			LastChosenTime = chosen;
		}
		finally
		{
			writing = false;
		}
	}

	public void ShowSuggestions()
	{
		completion.Show();
	}

	private void RenderSuggestion(string text)
	{
		if (EditingControlDataGridView != null && !EditingControlDataGridView.IsDisposed && !EditingControlDataGridView.Disposing)
		{
			bool editingControlValueChanged = EditingControlValueChanged;
			writing = true;
			try
			{
				mask.Reset(text);
			}
			finally
			{
				writing = false;
			}
			EditingControlValueChanged = editingControlValueChanged;
			if (EditingControlDataGridView != null)
			{
				EditingControlDataGridView.NotifyCurrentCellDirty(editingControlValueChanged);
			}
		}
	}

	public bool CompleteSuggestion()
	{
		if (!completion.Accept())
		{
			return FillCurrentTime();
		}
		return true;
	}

	private void Choose(DateTime time)
	{
		completion.Forget();
		DateTime value = time.Date.AddHours(time.Hour).AddMinutes(time.Minute);
		ResetTime(value.ToString("HH:mm"), value);
		EditingControlValueChanged = true;
		EditingControlDataGridView.NotifyCurrentCellDirty(dirty: true);
		Invalidate();
	}

	public bool FillCurrentTime()
	{
		if (completion.Accept())
		{
			return true;
		}
		if (ReadOnly || Clock == null || ClockMask.Value(Text).Length > 0 || EditingControlDataGridView == null || !EditingControlDataGridView.IsCurrentCellInEditMode)
		{
			return false;
		}
		Choose(Clock());
		SelectAll();
		return true;
	}

	public bool AdjustWheel(int delta, int x)
	{
		if (IsSuggestion)
		{
			completion.Accept();
		}
		if (ReadOnly || Clock == null || EditingControlDataGridView == null || !EditingControlDataGridView.IsCurrentCellInEditMode)
		{
			return false;
		}
		int num = ClockInteraction.Steps(ref wheelRemainder, delta);
		if (num == 0)
		{
			return true;
		}
		DateTime? dateTime = LastChosenTime;
		if (!dateTime.HasValue && DateTime.TryParseExact(Text.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
		{
			dateTime = ((ValueProvider == null) ? new DateTime?(Clock().Date.Add(result.TimeOfDay)) : ValueProvider());
		}
		DateTime dateTime2 = dateTime ?? Clock();
		try
		{
			Choose(ClockInteraction.Hours(this, x) ? dateTime2.AddHours(num) : dateTime2.AddMinutes(num));
		}
		catch (ArgumentOutOfRangeException)
		{
			return true;
		}
		Select((!ClockInteraction.Hours(this, x)) ? 3 : 0, 2);
		return true;
	}

	protected override void OnMouseDoubleClick(MouseEventArgs e)
	{
		base.OnMouseDoubleClick(e);
		if (e.Button == MouseButtons.Left)
		{
			if (EditingControlDataGridView is ReferenceGrid referenceGrid)
			{
				referenceGrid.AcceptSuggestedClock();
			}
			else
			{
				FillCurrentTime();
			}
		}
	}

	protected override bool ProcessCmdKey(ref Message message, Keys keys)
	{
		if (keys == Keys.Return && IsSuggestion && EditingControlDataGridView is ReferenceGrid referenceGrid && referenceGrid.AcceptSuggestedClock())
		{
			return true;
		}
		return base.ProcessCmdKey(ref message, keys);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		if (e.Modifiers == Keys.None && e.KeyCode == Keys.Return && IsSuggestion && EditingControlDataGridView is ReferenceGrid referenceGrid && referenceGrid.AcceptSuggestedClock())
		{
			e.SuppressKeyPress = true;
		}
		else
		{
			base.OnKeyDown(e);
		}
	}

	protected override void OnMouseWheel(MouseEventArgs e)
	{
		if (AdjustWheel(e.Delta, e.X))
		{
			if (e is HandledMouseEventArgs e2)
			{
				e2.Handled = true;
			}
		}
		else
		{
			base.OnMouseWheel(e);
		}
	}

	protected override void OnTextChanged(EventArgs e)
	{
		if (!writing)
		{
			LastChosenTime = null;
		}
		base.OnTextChanged(e);
		Invalidate();
	}

	protected override void WndProc(ref Message m)
	{
		base.WndProc(ref m);
		if (m.Msg == 515 && EditingControlDataGridView is ReferenceGrid referenceGrid)
		{
			referenceGrid.AcceptSuggestedClock();
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			completion.Dispose();
		}
		base.Dispose(disposing);
	}

	public override object GetEditingControlFormattedValue(DataGridViewDataErrorContexts context)
	{
		if (!IsSuggestion)
		{
			return ClockMask.Value(Text);
		}
		return "";
	}

	public override void PrepareEditingControlForEdit(bool selectAll)
	{
		base.PrepareEditingControlForEdit(selectAll);
		if (ClockMask.Value(Text).Length == 0)
		{
			Select(0, 0);
		}
	}

	public override bool EditingControlWantsInputKey(Keys keyData, bool gridWants)
	{
		Keys keys = keyData & Keys.KeyCode;
		if ((!completion.Visible || (keys != Keys.Escape && keys != Keys.Down && keys != Keys.Return)) && keys != Keys.Left && keys != Keys.Right && keys != Keys.Home && keys != Keys.End && keys != Keys.Back && keys != Keys.Delete)
		{
			return base.EditingControlWantsInputKey(keyData, gridWants);
		}
		return true;
	}
}

using System;
using System.Globalization;
using System.Windows.Forms;

namespace EdrLocal;

public class ClockTextBox : TextBox
{
	private readonly ClockMask mask;

	private readonly ClockCompletion completion;

	private bool writing;

	private int wheelRemainder;

	private DateTime? chosenValue;

	public Func<DateTime> Clock { get; set; }

	public Func<DateTime?> ValueProvider { get; set; }

	public DateTime LastSeededTime { get; private set; }

	public DateTime? ChosenTime => chosenValue;

	public bool LastChangeWasWheel { get; private set; }

	public bool IsChoosing => writing;

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

	public event EventHandler TimeSeeded;

	public ClockTextBox()
	{
		mask = new ClockMask(this);
		mask.Reset("");
		TextAlign = HorizontalAlignment.Center;
		Func<Func<DateTime>> source = () => Clock;
		completion = new ClockCompletion(this, source, () => !ReadOnly && Enabled, (string text) =>
		{
			mask.Reset(text);
		}, (DateTime time) =>
		{
			Choose(time);
		});
	}

	public void ShowSuggestions()
	{
		completion.Show();
	}

	private bool CompleteSuggestion()
	{
		if (!completion.Accept())
		{
			return FillCurrentTime();
		}
		return true;
	}

	private void Choose(DateTime value, bool wheel = false)
	{
		completion.Forget();
		LastChangeWasWheel = wheel;
		LastSeededTime = value.Date.AddHours(value.Hour).AddMinutes(value.Minute);
		chosenValue = LastSeededTime;
		writing = true;
		try
		{
			mask.Reset(LastSeededTime.ToString("HH:mm"));
		}
		finally
		{
			writing = false;
		}
		TimeSeeded?.Invoke(this, EventArgs.Empty);
		Invalidate();
	}

	public bool FillCurrentTime()
	{
		if (completion.Accept())
		{
			return true;
		}
		if (ReadOnly || !Enabled || Clock == null || ClockMask.Value(Text).Length > 0)
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
		if (ReadOnly || !Enabled || Clock == null)
		{
			return false;
		}
		int num = ClockInteraction.Steps(ref wheelRemainder, delta);
		if (num == 0)
		{
			return true;
		}
		DateTime? dateTime = chosenValue;
		if (!dateTime.HasValue && DateTime.TryParseExact(Text.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
		{
			dateTime = ((ValueProvider == null) ? new DateTime?(Clock().Date.Add(result.TimeOfDay)) : ValueProvider());
		}
		DateTime dateTime2 = dateTime ?? Clock();
		try
		{
			Choose(ClockInteraction.Hours(this, x) ? dateTime2.AddHours(num) : dateTime2.AddMinutes(num), wheel: true);
		}
		catch (ArgumentOutOfRangeException)
		{
			return true;
		}
		Select((!ClockInteraction.Hours(this, x)) ? 3 : 0, 2);
		return true;
	}

	protected override void OnEnter(EventArgs e)
	{
		base.OnEnter(e);
		Invalidate();
	}

	protected override void OnMouseDoubleClick(MouseEventArgs e)
	{
		base.OnMouseDoubleClick(e);
		if (e.Button == MouseButtons.Left)
		{
			FillCurrentTime();
		}
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		if ((e.KeyCode == Keys.Return || e.KeyCode == Keys.Tab) && e.Modifiers == Keys.None && completion.Accept())
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
			chosenValue = null;
		}
		base.OnTextChanged(e);
		if (!writing)
		{
			Invalidate();
		}
	}

	protected override void WndProc(ref Message m)
	{
		base.WndProc(ref m);
		if (m.Msg == 515)
		{
			FillCurrentTime();
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
}

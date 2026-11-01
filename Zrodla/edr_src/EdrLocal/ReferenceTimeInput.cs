using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace EdrLocal;

public class ReferenceTimeInput : UserControl
{
	private ToolTip tip = new ToolTip();

	private bool manualDay;

	private bool clockChosen;

	private DateTime? originalTime;

	private Func<DateTime> clockSource;

	public TextBox Input { get; private set; }

	public CheckBox Active { get; private set; }

	public DateTime Day { get; set; }

	public DateTime? Value
	{
		get
		{
			if (!Active.Checked)
			{
				return null;
			}
			if (!DateTime.TryParseExact(Input.Text.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
			{
				throw new Exception("Wpisz czas w formacie HH:mm.");
			}
			if (manualDay || clockChosen)
			{
				return Day.AddHours(result.Hour).AddMinutes(result.Minute);
			}
			if (originalTime.HasValue && Input.Text.Trim() == originalTime.Value.ToString("HH:mm"))
			{
				return originalTime.Value.Date.Add(result.TimeOfDay);
			}
			return ClockDates.Resolve(Input.Text, clockSource(), originalTime, null, clockSource());
		}
	}

	public ReferenceTimeInput(DateTime? value, DateTime fallback, bool blue, Func<DateTime> clock = null)
	{
		ReferenceTimeInput referenceTimeInput = this;
		Size = new Size(110, 26);
		originalTime = value;
		clockSource = clock ?? ((Func<DateTime>)(() => fallback));
		Day = (value ?? fallback).Date;
		Input = new ClockTextBox
		{
			Bounds = new Rectangle(0, 0, 80, 26),
			Font = ReferenceTheme.Dialog,
			Text = (value.HasValue ? value.Value.ToString("HH:mm") : ""),
			BackColor = (blue ? Color.FromArgb(154, 188, 221) : Color.White)
		};
		Active = new CheckBox
		{
			Bounds = new Rectangle(87, 4, 17, 17),
			Checked = value.HasValue
		};
		Controls.Add(Input);
		Controls.Add(Active);
		ClockTextBox clockInput = (ClockTextBox)Input;
		clockInput.Clock = () =>
		{
			DateTime dateTime = clockSource();
			return (!manualDay) ? dateTime : Day.Add(dateTime.TimeOfDay);
		};
		clockInput.ValueProvider = () => Value;
		clockInput.TimeSeeded += (object s, EventArgs e) =>
		{
			if (!referenceTimeInput.manualDay || clockInput.LastChangeWasWheel)
			{
				referenceTimeInput.Day = clockInput.LastSeededTime.Date;
			}
			referenceTimeInput.clockChosen = true;
			referenceTimeInput.Active.Checked = true;
			referenceTimeInput.UpdateTip();
		};
		Input.TextChanged += (object s, EventArgs e) =>
		{
			if (!clockInput.IsSuggestion)
			{
				if (!clockInput.IsChoosing)
				{
					referenceTimeInput.clockChosen = false;
					if (!referenceTimeInput.manualDay && DateTime.TryParseExact(referenceTimeInput.Input.Text.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var _))
					{
						DateTime? dateTime = ClockDates.Resolve(referenceTimeInput.Input.Text, referenceTimeInput.clockSource(), referenceTimeInput.originalTime, null, referenceTimeInput.clockSource());
						if (dateTime.HasValue)
						{
							referenceTimeInput.Day = dateTime.Value.Date;
						}
						referenceTimeInput.UpdateTip();
					}
				}
				referenceTimeInput.Active.Checked = ClockMask.Value(referenceTimeInput.Input.Text).Length > 0;
			}
		};
		Active.CheckedChanged += (object s, EventArgs e) =>
		{
			if (referenceTimeInput.Active.Checked)
			{
				clockInput.FillCurrentTime();
			}
		};
		ContextMenuStrip menu = new ContextMenuStrip();
		menu.Opening += (object s, CancelEventArgs e) =>
		{
			menu.Items.Clear();
			menu.Items.Add("Data: " + referenceTimeInput.Day.ToString("yyyy-MM-dd"));
			menu.Items.Add(Ui.Menu("Poprzedni dzień", () =>
			{
				referenceTimeInput.manualDay = true;
				referenceTimeInput.Day = referenceTimeInput.Day.AddDays(-1.0);
				referenceTimeInput.UpdateTip();
			}));
			menu.Items.Add(Ui.Menu("Następny dzień", () =>
			{
				referenceTimeInput.manualDay = true;
				referenceTimeInput.Day = referenceTimeInput.Day.AddDays(1.0);
				referenceTimeInput.UpdateTip();
			}));
		};
		Input.ContextMenuStrip = menu;
		UpdateTip();
	}

	private void UpdateTip()
	{
		tip.SetToolTip(Input, "Data: " + Day.ToString("yyyy-MM-dd") + ". Prawy przycisk pozwala zmienić dzień przy jeździe przez północ.");
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			tip.Dispose();
		}
		base.Dispose(disposing);
	}
}

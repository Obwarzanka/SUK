using System;
using System.Drawing;
using System.Windows.Forms;

namespace EdrLocal;

public class OperationTimePicker : UserControl
{
	private CompactDatePicker date;

	private NumericUpDown hour;

	private NumericUpDown minute;

	public DateTime Value
	{
		get
		{
			return date.Value.Date.AddHours((int)hour.Value).AddMinutes((int)minute.Value);
		}
		set
		{
			date.Value = value.Date;
			hour.Value = value.Hour;
			minute.Value = value.Minute;
		}
	}

	public OperationTimePicker(DateTime value)
	{
		Size = new Size(380, 38);
		date = new CompactDatePicker
		{
			Bounds = new Rectangle(0, 0, 160, 36),
			CustomFormat = "yyyy-MM-dd",
			Font = new Font("Segoe UI", 13f, FontStyle.Bold)
		};
		hour = new ClockPartUpDown
		{
			Bounds = new Rectangle(190, 0, 60, 36),
			Minimum = 0m,
			Maximum = 23m,
			Font = date.Font,
			TextAlign = HorizontalAlignment.Center
		};
		minute = new ClockPartUpDown
		{
			Bounds = new Rectangle(279, 0, 60, 36),
			Minimum = 0m,
			Maximum = 59m,
			Font = date.Font,
			TextAlign = HorizontalAlignment.Center
		};
		Controls.Add(date);
		Controls.Add(hour);
		Controls.Add(minute);
		Controls.Add(new Label
		{
			Text = ":",
			Bounds = new Rectangle(255, 0, 20, 36),
			Font = date.Font,
			TextAlign = ContentAlignment.MiddleCenter
		});
		ClockPartUpDown clockPartUpDown = (ClockPartUpDown)hour;
		Action<int> value2 = (int steps) =>
		{
			Value = Value.AddHours(steps);
		};
		clockPartUpDown.WheelStep += value2;
		((ClockPartUpDown)minute).WheelStep += (int steps) =>
		{
			Value = Value.AddMinutes(steps);
		};
		Value = value;
	}
}

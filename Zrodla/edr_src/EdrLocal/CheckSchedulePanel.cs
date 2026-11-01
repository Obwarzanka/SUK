using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace EdrLocal;

public sealed class CheckSchedulePanel : GroupBox
{
	private readonly RequiredCheckSetting check;

	private readonly CheckBox active;

	private readonly CheckBox after;

	private readonly CheckBox daily;

	private readonly NumericUpDown delay;

	private readonly NumericUpDown count;

	private readonly FlowLayoutPanel times;

	private readonly Panel body;

	public CheckSchedulePanel(RequiredCheckSetting value)
	{
		check = value;
		Text = "";
		active = new CheckBox
		{
			Text = check.Name,
			Checked = check.Enabled,
			AutoSize = true,
			Location = new Point(12, 0),
			Font = new Font("Tahoma", 8.25f, FontStyle.Bold)
		};
		Controls.Add(active);
		body = new Panel
		{
			Dock = DockStyle.Fill,
			Padding = new Padding(10, 25, 8, 8)
		};
		Controls.Add(body);
		active.BringToFront();
		after = new CheckBox
		{
			Text = "Ilość minut po których nastąpi sprawdzenie po przejęciu dyżuru:",
			Checked = check.AfterDutyMinutes.HasValue,
			Bounds = new Rectangle(16, 35, 480, 25)
		};
		delay = new NumericUpDown
		{
			Minimum = 0m,
			Maximum = 1440m,
			Value = (check.AfterDutyMinutes ?? 5),
			Bounds = new Rectangle(38, 68, 80, 24)
		};
		daily = new CheckBox
		{
			Text = "Liczba sprawdzeń na dobę:",
			Checked = (check.DailyChecks != false && !string.IsNullOrWhiteSpace(check.Hours)),
			Bounds = new Rectangle(16, 104, 210, 25)
		};
		count = new NumericUpDown
		{
			Minimum = 1m,
			Maximum = 12m,
			Value = 2m,
			Bounds = new Rectangle(235, 104, 60, 24)
		};
		times = new FlowLayoutPanel
		{
			Bounds = new Rectangle(30, 143, 500, 75),
			AutoScroll = true
		};
		body.Controls.Add(after);
		body.Controls.Add(delay);
		body.Controls.Add(daily);
		body.Controls.Add(count);
		body.Controls.Add(times);
		string[] array = (from x in (check.Hours ?? "").Split(new char[2] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
			select x.Trim()).ToArray();
		count.Value = Math.Max(1, Math.Min(12, (array.Length == 0) ? 2 : array.Length));
		Rebuild(array);
		count.ValueChanged += (object s, EventArgs e) =>
		{
			Rebuild((from x in times.Controls.OfType<DateTimePicker>()
				select x.Value.ToString("HH:mm")).ToArray());
		};
		Action enabled = () =>
		{
			body.Enabled = active.Checked;
			delay.Enabled = after.Checked;
			count.Enabled = daily.Checked;
			times.Enabled = daily.Checked;
		};
		active.CheckedChanged += (object s, EventArgs e) =>
		{
			enabled();
		};
		after.CheckedChanged += (object s, EventArgs e) =>
		{
			enabled();
		};
		daily.CheckedChanged += (object s, EventArgs e) =>
		{
			enabled();
		};
		enabled();
		Resize += (object s, EventArgs e) =>
		{
			times.Width = Math.Max(200, ClientSize.Width - 50);
			after.Width = Math.Max(200, ClientSize.Width - 32);
		};
	}

	private void Rebuild(string[] values)
	{
		Control[] array = times.Controls.Cast<Control>().ToArray();
		foreach (Control control in array)
		{
			control.Dispose();
		}
		times.Controls.Clear();
		for (int j = 0; j < (int)count.Value; j++)
		{
			if (j >= values.Length || !DateTime.TryParseExact(values[j], "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
			{
				result = DateTime.Today.AddHours((5 + j * 12) % 24);
			}
			times.Controls.Add(new Label
			{
				Text = j + 1 + ".",
				AutoSize = true,
				Margin = new Padding(2, 7, 1, 0)
			});
			times.Controls.Add(new DateTimePicker
			{
				Format = DateTimePickerFormat.Custom,
				CustomFormat = "HH:mm",
				ShowUpDown = true,
				Value = result,
				Width = 73,
				Margin = new Padding(1, 3, 14, 3)
			});
		}
	}

	public void Save()
	{
		check.Enabled = active.Checked;
		check.AfterDutyMinutes = (after.Checked ? new int?((int)delay.Value) : ((int?)null));
		check.DailyChecks = daily.Checked;
		check.Hours = string.Join("; ", from x in times.Controls.OfType<DateTimePicker>()
			select x.Value.ToString("HH:mm"));
	}
}

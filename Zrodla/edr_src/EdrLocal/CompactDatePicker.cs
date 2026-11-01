using System;
using System.Drawing;
using System.Windows.Forms;

namespace EdrLocal;

public class CompactDatePicker : Control
{
	private ToolStripDropDown calendarPopup;

	private MonthCalendar calendar;

	private DateTime value = DateTime.Today;

	public bool TimeSettings { get; set; }

	public string CustomFormat { get; set; }

	public DateTime Value
	{
		get
		{
			return value;
		}
		set
		{
			if (!(this.value == value))
			{
				this.value = value;
				Invalidate();
				if (ValueChanged != null)
				{
					ValueChanged(this, EventArgs.Empty);
				}
			}
		}
	}

	public event EventHandler ValueChanged;

	public CompactDatePicker()
	{
		Height = 16;
		Font = ReferenceTheme.Small;
		BackColor = Color.White;
		CustomFormat = "yyyy-MM-dd";
		SetStyle(ControlStyles.UserPaint | ControlStyles.Selectable | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		TabStop = true;
		AccessibleRole = AccessibleRole.ComboBox;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		e.Graphics.Clear(BackColor);
		ControlPaint.DrawBorder(e.Graphics, ClientRectangle, Color.FromArgb(160, 160, 160), ButtonBorderStyle.Solid);
		TextRenderer.DrawText(e.Graphics, Value.ToString(CustomFormat), Font, new Rectangle(2, 0, Width - 14, Height), Color.FromArgb(30, 60, 85), TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
		ReferenceTheme.Silver(e.Graphics, new Rectangle(Width - 12, 1, 11, Height - 2), pressed: false);
		TextRenderer.DrawText(e.Graphics, "▦", ReferenceTheme.Small, new Rectangle(Width - 12, 0, 11, Height), Color.Black, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		base.OnMouseDown(e);
		if (TimeSettings)
		{
			using (Editor editor = new Editor("Ustawienia", 380, 190))
			{
				DateTimePicker date = editor.Add("Czas pobrania danych", Ui.Date(Value, nullable: false, active: true), 42);
				editor.Save(() =>
				{
					Value = date.Value;
				});
				editor.ShowDialog(this);
				return;
			}
		}
		if (IsDisposed || Disposing)
		{
			return;
		}
		if (calendarPopup == null)
		{
			calendar = new MonthCalendar
			{
				MaxSelectionCount = 1
			};
			calendarPopup = new ToolStripDropDown
			{
				Padding = Padding.Empty
			};
			calendarPopup.Items.Add(new ToolStripControlHost(calendar)
			{
				Padding = Padding.Empty,
				Margin = Padding.Empty
			});
			calendar.DateSelected += (object s, DateRangeEventArgs a) =>
			{
				DateTime start = a.Start;
				calendarPopup.Close();
				if (!IsDisposed && !Disposing)
				{
					Value = start;
				}
			};
		}
		if (calendarPopup.Visible)
		{
			calendarPopup.Close();
			return;
		}
		calendar.SetDate(Value);
		calendarPopup.Show(this, new Point(0, Height));
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && calendarPopup != null)
		{
			ToolStripDropDown toolStripDropDown = calendarPopup;
			calendarPopup = null;
			toolStripDropDown.Dispose();
			calendar = null;
		}
		base.Dispose(disposing);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Left)
		{
			Value = Value.AddDays(-1.0);
		}
		else if (e.KeyCode == Keys.Right)
		{
			Value = Value.AddDays(1.0);
		}
		else if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Down)
		{
			OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
		}
		base.OnKeyDown(e);
	}
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EdrLocal;

internal sealed class StationTrackPopup : Form
{
	private readonly Timer timer;

	private readonly Control anchor;

	private readonly Engine engine;

	private readonly Action<StationTrackItem> remove;

	private bool dragging;

	protected override bool ShowWithoutActivation => true;

	protected override CreateParams CreateParams
	{
		get
		{
			CreateParams createParams = base.CreateParams;
			createParams.ExStyle |= 134217856;
			return createParams;
		}
	}

	internal StationTrackPopup(Control source, Engine context, List<StationTrackItem> items, Action<StationTrackItem> delete)
	{
		anchor = source;
		engine = context;
		remove = delete;
		Name = "stationTrackOccupants";
		FormBorderStyle = FormBorderStyle.None;
		ShowInTaskbar = false;
		ShowIcon = false;
		StartPosition = FormStartPosition.Manual;
		AutoScaleMode = AutoScaleMode.None;
		BackColor = SystemColors.WindowFrame;
		Padding = new Padding(1);
		Font = source.Font;
		Func<StationTrackItem, int> selector = (StationTrackItem x) => TextRenderer.MeasureText(x.Number, Font).Width;
		int val = items.Max(selector) + 12;
		int num = Font.Height + 8;
		val = Math.Max(source.Width, val);
		ClientSize = new Size(val, num * items.Count + 2);
		foreach (StationTrackItem item in items)
		{
			Label label = new Label
			{
				Text = item.Number,
				Name = "occupant",
				Tag = item,
				AutoSize = false,
				Size = new Size(val - 2, num),
				Location = new Point(1, 1 + num * Controls.Count),
				TextAlign = ContentAlignment.MiddleCenter,
				Font = Font,
				BackColor = (item.Reserved ? Color.Orange : Color.Firebrick),
				ForeColor = (item.Reserved ? Color.Black : Color.White),
				Cursor = (engine.CanWrite ? Cursors.SizeAll : Cursors.Default)
			};
			Controls.Add(label);
			Point start = Point.Empty;
			label.MouseDown += (object s, MouseEventArgs e) =>
			{
				if (e.Button == MouseButtons.Left)
				{
					start = e.Location;
				}
			};
			label.MouseMove += (object s, MouseEventArgs e) =>
			{
				if (!engine.CanWrite || dragging || e.Button != MouseButtons.Left || new Rectangle(start.X - SystemInformation.DragSize.Width / 2, start.Y - SystemInformation.DragSize.Height / 2, SystemInformation.DragSize.Width, SystemInformation.DragSize.Height).Contains(e.Location))
				{
					return;
				}
				dragging = true;
				try
				{
					DataObject dataObject = new DataObject();
					dataObject.SetData(typeof(StationTrackDrag), new StationTrackDrag
					{
						Store = engine.Store,
						StationId = engine.StationId,
						Key = item.Key
					});
					label.DoDragDrop(dataObject, DragDropEffects.Move);
				}
				finally
				{
					dragging = false;
				}
			};
			ContextMenuStrip menu = new ContextMenuStrip();
			ToolStripMenuItem command = Ui.Menu("Usuń pociąg " + item.Number, () =>
			{
				remove(item);
			});
			menu.Items.Add(command);
			menu.Opening += (object s, CancelEventArgs e) =>
			{
				command.Enabled = engine.CanWrite;
			};
			label.ContextMenuStrip = menu;
			label.Disposed += (object s, EventArgs e) =>
			{
				menu.Dispose();
			};
		}
		Rectangle workingArea = Screen.FromControl(source).WorkingArea;
		Point location = source.PointToScreen(new Point(0, -Height));
		if (location.Y < workingArea.Top)
		{
			location.Y = source.PointToScreen(new Point(0, source.Height)).Y;
		}
		location.X = Math.Max(workingArea.Left, Math.Min(location.X, workingArea.Right - Width));
		Location = location;
		timer = new Timer
		{
			Interval = 200
		};
		timer.Tick += (object s, EventArgs e) =>
		{
			if (!dragging && !Controls.Cast<Control>().Any((Control c) => c.ContextMenuStrip != null && c.ContextMenuStrip.Visible))
			{
				Point mousePosition = Control.MousePosition;
				if (anchor.IsDisposed || !anchor.Visible || (!Bounds.Contains(mousePosition) && !anchor.RectangleToScreen(anchor.ClientRectangle).Contains(mousePosition)))
				{
					Hide();
					timer.Stop();
				}
			}
		};
		timer.Start();
	}

	protected override void WndProc(ref Message m)
	{
		if (m.Msg == 33)
		{
			m.Result = new IntPtr(3);
		}
		else
		{
			base.WndProc(ref m);
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			timer.Dispose();
		}
		base.Dispose(disposing);
	}
}

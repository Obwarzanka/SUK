using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EdrLocal;

public class ReportCanvas : Panel
{
	private JournalReport report;

	private int page;

	private float zoom = 1f;

	private bool fit = true;

	private bool dragging;

	private Point dragStart;

	private Point scrollStart;

	public bool Pan { get; set; }

	public JournalReport Report
	{
		get
		{
			return report;
		}
		set
		{
			report = value;
			Page = 0;
			Reflow();
		}
	}

	public int Page
	{
		get
		{
			return page;
		}
		set
		{
			page = ((report != null) ? Math.Max(0, Math.Min(report.PageCount - 1, value)) : 0);
			AutoScrollPosition = Point.Empty;
			Invalidate();
		}
	}

	public ReportCanvas()
	{
		DoubleBuffered = true;
		AutoScroll = true;
		BackColor = Color.FromArgb(174, 174, 174);
		Dock = DockStyle.Fill;
		EventHandler value = (object s, EventArgs e) =>
		{
			Reflow();
		};
		Resize += value;
	}

	public void Zoom(float scale)
	{
		fit = scale <= 0f;
		if (!fit)
		{
			zoom = scale;
		}
		AutoScrollPosition = Point.Empty;
		Reflow();
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		base.OnMouseDown(e);
		if (Pan && e.Button == MouseButtons.Left)
		{
			dragging = true;
			dragStart = e.Location;
			scrollStart = new Point(-AutoScrollPosition.X, -AutoScrollPosition.Y);
			Capture = true;
			Cursor = Cursors.Hand;
		}
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		if (dragging)
		{
			AutoScrollPosition = new Point(scrollStart.X + dragStart.X - e.X, scrollStart.Y + dragStart.Y - e.Y);
		}
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		base.OnMouseUp(e);
		dragging = false;
		Capture = false;
		Cursor = (Pan ? Cursors.Hand : Cursors.Default);
	}

	private void Reflow()
	{
		if (fit)
		{
			zoom = Math.Max(0.2f, (float)(ClientSize.Width - 44) / 827f);
		}
		AutoScrollMinSize = ((report == null) ? Size.Empty : new Size((int)(827f * zoom) + 28, (int)(1169f * zoom) + 28));
		Invalidate();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		if (report != null)
		{
			float num = Math.Max(14f, ((float)ClientSize.Width - 827f * zoom) / 2f);
			GraphicsState gstate = e.Graphics.Save();
			e.Graphics.TranslateTransform((float)AutoScrollPosition.X + num, AutoScrollPosition.Y + 14);
			e.Graphics.ScaleTransform(zoom, zoom);
			report.DrawPage(e.Graphics, page);
			e.Graphics.Restore(gstate);
		}
	}
}

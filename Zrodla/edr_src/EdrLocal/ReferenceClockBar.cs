using System;
using System.Drawing;
using System.Windows.Forms;

namespace EdrLocal;

public class ReferenceClockBar : Control
{
	private readonly MainForm main;

	private readonly DateTime started = DateTime.Now;

	public ReferenceClockBar(MainForm form)
	{
		main = form;
		DoubleBuffered = true;
		Size = new Size(245, 82);
		BackColor = Color.Navy;
	}

	public void Render(Graphics g)
	{
		float num = (float)Width / 245f;
		int num2 = (int)Math.Round(30f * num);
		int num3 = (int)Math.Round(15f * num);
		int num4 = (int)Math.Round(14f * num);
		g.Clear(Color.Navy);
		using (SolidBrush brush = new SolidBrush(Color.FromArgb(14, 87, 16)))
		{
			g.FillRectangle(brush, 0, 0, Width, num2);
		}
		using (Font font = new Font("Arial", Math.Max(10f, (float)Math.Round(10f * num)), FontStyle.Italic, GraphicsUnit.Pixel))
		{
			TextRenderer.DrawText(g, main.AccidentCounterCaption, font, new Rectangle(2, 0, Width - 4, num3), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
		}
		TimeSpan timeSpan = ((main.Engine.SimRail == null) ? (DateTime.Now - started) : main.AccidentCounterDuration);
		using (Font font2 = new Font("Arial", (float)Math.Round(12f * num), FontStyle.Bold, GraphicsUnit.Pixel))
		{
			TextRenderer.DrawText(g, timeSpan.Days + " dni " + timeSpan.Hours + " godz. " + timeSpan.Minutes + " min.", font2, new Rectangle(2, num3, Width - 4, num2 - num3), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
		}
		using (Font font3 = new Font("Arial", (float)Math.Round(11f * num), FontStyle.Bold, GraphicsUnit.Pixel))
		{
			TextRenderer.DrawText(g, main.Engine.Now.ToString("dd-MM-yyyy"), font3, new Rectangle(3, num2, Width - 6, num4), Color.Yellow, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
		}
		Rectangle bounds = new Rectangle(2, num2 + num4, Width - 4, Height - num2 - num4 - 2);
		using Font font4 = new Font("Arial", (float)Math.Round(32f * num), FontStyle.Bold, GraphicsUnit.Pixel);
		TextRenderer.DrawText(g, main.Engine.Now.ToString("HH:mm:ss"), font4, bounds, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Render(e.Graphics);
	}

	protected override void OnDoubleClick(EventArgs e)
	{
		base.OnDoubleClick(e);
		Ui.Run(this, () =>
		{
			main.Situation();
		});
	}
}

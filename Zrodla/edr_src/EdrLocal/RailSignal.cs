using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EdrLocal;

public class RailSignal : Control
{
	public string Status = "WOLNY";

	public bool Incoming;

	public bool Telephone;

	public bool Bidirectional;

	public bool? MovementIncoming;

	public int Number = 1;

	public RailSignal()
	{
		BackColor = Color.White;
		Size = new Size(40, 30);
		Margin = Padding.Empty;
		DoubleBuffered = true;
	}

	public void Render(Graphics g)
	{
		g.Clear(BackColor);
		g.ScaleTransform((float)Width / 40f, (float)Height / 30f);
		g.SmoothingMode = SmoothingMode.None;
		bool flag = Status.StartsWith("ZAJĘTY") || MovementIncoming.HasValue;
		bool flag2 = Status == "ZAMKNIĘTY";
		g.FillRectangle(Brushes.White, 0, 0, 11, 10);
		g.DrawRectangle(Pens.Gray, 0, 0, 11, 10);
		using (Font font = new Font("Tahoma", 8f, FontStyle.Regular, GraphicsUnit.Pixel))
		{
			StringFormat stringFormat = new StringFormat();
			stringFormat.Alignment = StringAlignment.Center;
			stringFormat.LineAlignment = StringAlignment.Center;
			using StringFormat format = stringFormat;
			g.DrawString(Number.ToString(), font, Brushes.Navy, new RectangleF(0f, 0f, 11f, 10f), format);
		}
		Point[] points = (Incoming ? new Point[7]
		{
			new Point(4, 12),
			new Point(7, 12),
			new Point(7, 22),
			new Point(10, 22),
			new Point(5, 28),
			new Point(0, 22),
			new Point(4, 22)
		} : new Point[7]
		{
			new Point(4, 28),
			new Point(7, 28),
			new Point(7, 18),
			new Point(10, 18),
			new Point(5, 12),
			new Point(0, 18),
			new Point(4, 18)
		});
		if (Bidirectional)
		{
			points = new Point[10]
			{
				new Point(4, 17),
				new Point(0, 17),
				new Point(5, 12),
				new Point(10, 17),
				new Point(7, 17),
				new Point(7, 23),
				new Point(10, 23),
				new Point(5, 28),
				new Point(0, 23),
				new Point(4, 23)
			};
		}
		g.FillPolygon(Brushes.Gray, points);
		g.TranslateTransform(0f, -3f);
		using (Pen pen = new Pen(flag ? Color.Red : Color.FromArgb(25, 25, 25), 2f))
		{
			for (int i = 13; i <= 27; i += 4)
			{
				g.DrawLine(pen, 12, i, 26, i);
			}
			g.DrawLine(pen, 15, 11, 15, 29);
			g.DrawLine(pen, 23, 11, 23, 29);
		}
		if (flag && MovementIncoming.HasValue)
		{
			Point[] points2 = (MovementIncoming.Value ? new Point[7]
			{
				new Point(18, 12),
				new Point(21, 12),
				new Point(21, 23),
				new Point(24, 23),
				new Point(19, 28),
				new Point(14, 23),
				new Point(18, 23)
			} : new Point[7]
			{
				new Point(18, 28),
				new Point(21, 28),
				new Point(21, 17),
				new Point(24, 17),
				new Point(19, 12),
				new Point(14, 17),
				new Point(18, 17)
			});
			using Pen pen2 = new Pen(Color.White, 2.5f);
			g.DrawPolygon(pen2, points2);
			g.FillPolygon(Brushes.Blue, points2);
		}
		if (Telephone)
		{
			SmoothingMode smoothingMode = g.SmoothingMode;
			g.SmoothingMode = SmoothingMode.AntiAlias;
			using (SolidBrush brush = new SolidBrush(Color.FromArgb(176, 0, 0)))
			{
				using Pen pen3 = new Pen(Color.FromArgb(176, 0, 0), 1.1f);
				using GraphicsPath graphicsPath = new GraphicsPath();
				graphicsPath.StartFigure();
				graphicsPath.AddBezier(35, 12, 36, 10, 38, 10, 39, 14);
				graphicsPath.AddBezier(39, 14, 40, 19, 36, 25, 32, 28);
				graphicsPath.AddBezier(32, 28, 30, 30, 27, 29, 27, 26);
				graphicsPath.AddBezier(27, 26, 27, 24, 29, 22, 30, 23);
				graphicsPath.AddBezier(30, 23, 32, 24, 34, 21, 35, 18);
				graphicsPath.AddBezier(35, 18, 36, 16, 35, 16, 33, 15);
				graphicsPath.AddBezier(33, 15, 33, 14, 34, 13, 35, 12);
				graphicsPath.CloseFigure();
				g.DrawBezier(pen3, 30f, 25f, 35f, 20f, 39f, 27f, 36f, 29f);
				g.DrawBezier(pen3, 36f, 29f, 33f, 31f, 31f, 28f, 33f, 26f);
				g.DrawBezier(pen3, 33f, 26f, 32f, 21f, 28f, 21f, 27f, 23f);
				g.FillPath(brush, graphicsPath);
			}
			g.SmoothingMode = smoothingMode;
		}
		if (flag2)
		{
			int num = (Telephone ? 4 : 15);
			g.DrawLine(Pens.DimGray, 31, num + 8, 31, num + 14);
			g.FillRectangle(Brushes.White, 26, num, 13, 9);
			g.DrawRectangle(Pens.Gray, 26, num, 13, 9);
			g.FillRectangle(Brushes.Red, 28, num + 2, 9, 5);
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Render(e.Graphics);
	}
}

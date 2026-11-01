using System.Drawing;
using System.Drawing.Drawing2D;

namespace EdrLocal;

internal static class ReportTools
{
	public static Bitmap Glyph(string name)
	{
		Bitmap bitmap = new Bitmap(18, 18);
		using (Graphics graphics = Graphics.FromImage(bitmap))
		{
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			using Pen pen = new Pen(Color.FromArgb(65, 75, 80));
			Point[] array;
			switch (name)
			{
			case "Zapisz PDF":
				graphics.FillRectangle(Brushes.SlateGray, 2, 2, 13, 14);
				graphics.DrawRectangle(pen, 2, 2, 13, 14);
				graphics.FillRectangle(Brushes.WhiteSmoke, 5, 2, 7, 5);
				graphics.FillRectangle(Brushes.White, 5, 10, 7, 6);
				graphics.DrawLine(pen, 10, 3, 10, 5);
				goto end_IL_0029;
			case "Drukuj":
				graphics.FillRectangle(Brushes.WhiteSmoke, 5, 1, 9, 7);
				graphics.DrawRectangle(pen, 5, 1, 9, 7);
				graphics.FillRectangle(Brushes.Silver, 2, 6, 14, 8);
				graphics.DrawRectangle(pen, 2, 6, 14, 8);
				graphics.FillRectangle(Brushes.White, 5, 11, 9, 6);
				graphics.DrawRectangle(pen, 5, 11, 9, 6);
				graphics.DrawLine(Pens.Gray, 7, 13, 12, 13);
				goto end_IL_0029;
			case "hand":
				graphics.FillRectangle(Brushes.Wheat, 6, 4, 7, 11);
				graphics.FillRectangle(Brushes.Wheat, 3, 8, 5, 6);
				graphics.DrawLines(Pens.SaddleBrown, new Point[12]
				{
					new Point(6, 15),
					new Point(3, 10),
					new Point(4, 8),
					new Point(6, 10),
					new Point(6, 3),
					new Point(8, 2),
					new Point(9, 8),
					new Point(12, 4),
					new Point(14, 5),
					new Point(13, 14),
					new Point(11, 16),
					new Point(6, 15)
				});
				goto end_IL_0029;
			default:
				array = new Point[3]
				{
					new Point(5, 3),
					new Point(12, 9),
					new Point(5, 15)
				};
				break;
			case "◀":
				array = new Point[3]
				{
					new Point(12, 3),
					new Point(5, 9),
					new Point(12, 15)
				};
				break;
			}
			Point[] points = array;
			graphics.FillPolygon(Brushes.SteelBlue, points);
			graphics.DrawPolygon(pen, points);
			end_IL_0029:;
		}
		return bitmap;
	}
}

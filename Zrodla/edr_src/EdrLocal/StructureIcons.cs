using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EdrLocal;

internal static class StructureIcons
{
	[DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
	private static extern int SetWindowTheme(IntPtr window, string app, string id);

	public static void Explorer(IntPtr handle)
	{
		SetWindowTheme(handle, "Explorer", null);
	}

	public static ImageList Create()
	{
		ImageList imageList = new ImageList();
		imageList.ColorDepth = ColorDepth.Depth32Bit;
		imageList.ImageSize = new Size(16, 16);
		imageList.TransparentColor = Color.Transparent;
		ImageList imageList2 = imageList;
		List<Bitmap> originals = new List<Bitmap>();
		string[] array = new string[5] { "root", "iz", "ise", "folder", "station" };
		foreach (string key in array)
		{
			Bitmap bitmap = Draw(key);
			originals.Add(bitmap);
			imageList2.Images.Add(key, bitmap);
		}
		imageList2.Disposed += (object s, EventArgs e) =>
		{
			foreach (Bitmap item in originals)
			{
				item.Dispose();
			}
		};
		return imageList2;
	}

	private static Bitmap Draw(string key)
	{
		Bitmap bitmap = new Bitmap(16, 16);
		using (Graphics graphics = Graphics.FromImage(bitmap))
		{
			graphics.Clear(Color.Transparent);
			switch (key)
			{
			case "iz":
			case "ise":
			{
				Color color = ((key == "iz") ? Color.FromArgb(112, 96, 244) : Color.FromArgb(158, 149, 239));
				using (SolidBrush brush = new SolidBrush(color))
				{
					graphics.FillPolygon(brush, new Point[3]
					{
						new Point(1, 7),
						new Point(7, 1),
						new Point(13, 7)
					});
					graphics.FillRectangle(brush, 3, 7, 9, 7);
				}
				using (SolidBrush brush2 = new SolidBrush(Color.White))
				{
					graphics.FillRectangle(brush2, 5, 8, 2, 5);
					graphics.FillRectangle(brush2, 9, 8, 1, 2);
				}
				using (Pen pen2 = new Pen(Color.White))
				{
					graphics.DrawLine(pen2, 8, 6, 11, 6);
				}
				if (key == "iz")
				{
					using (Font font = new Font("Tahoma", 5.5f, FontStyle.Bold, GraphicsUnit.Point))
					{
						using SolidBrush brush3 = new SolidBrush(Color.FromArgb(74, 65, 180));
						graphics.FillRectangle(Brushes.White, 8, 10, 8, 6);
						graphics.DrawString("IZ", font, brush3, 7f, 9f);
					}
					break;
				}
				graphics.FillRectangle(Brushes.White, 9, 8, 7, 8);
				Glyph(graphics, new string[7] { "01111", "11000", "11000", "01110", "00011", "00011", "11110" }, 10, 8, Color.FromArgb(104, 93, 181));
				break;
			}
			case "folder":
			case "root":
			{
				using (SolidBrush brush4 = new SolidBrush((key == "root") ? Color.FromArgb(96, 154, 204) : Color.FromArgb(237, 202, 105)))
				{
					graphics.FillRectangle(brush4, 1, 4, 6, 3);
					graphics.FillRectangle(brush4, 1, 6, 14, 8);
				}
				using (Pen pen3 = new Pen((key == "root") ? Color.FromArgb(53, 102, 151) : Color.FromArgb(168, 139, 65)))
				{
					graphics.DrawRectangle(pen3, 1, 6, 13, 8);
				}
				break;
			}
			default:
			{
				using (Pen pen = new Pen(Color.FromArgb(92, 96, 96)))
				{
					graphics.DrawLine(pen, 3, 2, 3, 14);
					graphics.DrawLine(pen, 7, 2, 7, 14);
					for (int i = 3; i < 15; i += 3)
					{
						graphics.DrawLine(pen, 1, i, 9, i);
					}
				}
				Glyph(graphics, new string[8] { "11110", "11011", "11011", "11110", "11000", "11000", "11000", "11000" }, 10, 4, Color.FromArgb(52, 55, 55));
				break;
			}
			}
		}
		return bitmap;
	}

	private static void Glyph(Graphics graphics, string[] rows, int left, int top, Color color)
	{
		using SolidBrush brush = new SolidBrush(color);
		for (int i = 0; i < rows.Length; i++)
		{
			for (int j = 0; j < rows[i].Length; j++)
			{
				if (rows[i][j] == '1')
				{
					graphics.FillRectangle(brush, left + j, top + i, 1, 1);
				}
			}
		}
	}
}

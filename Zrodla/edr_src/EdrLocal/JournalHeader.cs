using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EdrLocal;

public class JournalHeader : Control
{
	private readonly DataGridView grid;

	public JournalHeader(DataGridView source)
	{
		grid = source;
		Dock = DockStyle.Top;
		Height = 40;
		DoubleBuffered = true;
		DataGridView dataGridView = grid;
		ScrollEventHandler value = (object s, ScrollEventArgs e) =>
		{
			Invalidate();
		};
		dataGridView.Scroll += value;
		grid.ColumnWidthChanged += (object s, DataGridViewColumnEventArgs e) =>
		{
			Invalidate();
		};
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.Clear(Color.FromArgb(59, 59, 59));
		int num = (int)Math.Round((float)(Height * 13) / 40f);
		int num2 = num * 2;
		int num3 = -grid.HorizontalScrollingOffset;
		for (int i = 0; i < grid.Columns.Count; i++)
		{
			DataGridViewColumn dataGridViewColumn = grid.Columns[i];
			if (dataGridViewColumn.Visible)
			{
				int num4 = dataGridViewColumn.Width;
				using (Pen pen = new Pen(Color.FromArgb(160, 160, 160)))
				{
					graphics.DrawRectangle(pen, num3, 0, num4, num2);
				}
				ReferenceTheme.Gradient(graphics, new Rectangle(num3, num2, num4, Height - num2), Color.FromArgb(170, 170, 170), Color.FromArgb(15, 15, 15));
				using (Pen pen2 = new Pen(Color.FromArgb(140, 140, 140)))
				{
					graphics.DrawRectangle(pen2, num3, num2, num4, Height - num2 - 1);
				}
				if (dataGridViewColumn.Name != "odd" && dataGridViewColumn.Name != "even" && !(dataGridViewColumn.Tag is CrossingSetting))
				{
					Draw(graphics, dataGridViewColumn.HeaderText, new Rectangle(num3 + 1, 0, num4 - 2, num));
				}
				string value;
				if (dataGridViewColumn.Name == "odd")
				{
					value = "1";
				}
				else if (dataGridViewColumn.Name == "even")
				{
					value = "2";
				}
				else if (dataGridViewColumn.Name == "track")
				{
					value = "3";
				}
				else if (dataGridViewColumn.Name == "permission")
				{
					value = "4";
				}
				else if (dataGridViewColumn.Name == "left")
				{
					value = "5";
				}
				else if (dataGridViewColumn.Name == "arrived")
				{
					value = "6";
				}
				else if (dataGridViewColumn.Name == "notes")
				{
					value = "7";
				}
				else
				{
					value = ((dataGridViewColumn.Name == "notify") ? "8" : "");
				}
				Draw(graphics, value, new Rectangle(num3, num, num4, num));
				if (dataGridViewColumn.Name == "odd" || dataGridViewColumn.Name == "even")
				{
					Draw(graphics, (dataGridViewColumn.Name == "odd") ? "Nieparzysty" : "Parzysty", new Rectangle(num3, num2, num4, Height - num2));
				}
				if (dataGridViewColumn.Tag is CrossingSetting)
				{
					Draw(graphics, dataGridViewColumn.HeaderText, new Rectangle(num3, num2, num4, Height - num2));
				}
				num3 += num4;
			}
		}
		List<DataGridViewColumn> posts = (from DataGridViewColumn c in grid.Columns
			where c.Visible && c.Tag is CrossingSetting
			select c).ToList();
		if (posts.Count > 0)
		{
			int num5 = -grid.HorizontalScrollingOffset + (from DataGridViewColumn c in grid.Columns
				where c.Visible && c.Index < posts[0].Index
				select c).Sum((DataGridViewColumn c) => c.Width);
			int num6 = posts.Sum((DataGridViewColumn c) => c.Width);
			using (SolidBrush brush = new SolidBrush(Color.FromArgb(59, 59, 59)))
			{
				graphics.FillRectangle(brush, new Rectangle(num5 + 1, 1, num6 - 2, num2 - 2));
			}
			Draw(graphics, "O jeździe pociągu zawiadomiono", new Rectangle(num5, 0, num6, num));
			Draw(graphics, "8", new Rectangle(num5, num, num6, num));
		}
		int num7 = (grid.Columns["indicator"].Visible ? grid.Columns["indicator"].Width : 0) - grid.HorizontalScrollingOffset;
		int num8 = grid.Columns["odd"].Width + grid.Columns["even"].Width;
		using (SolidBrush brush2 = new SolidBrush(Color.FromArgb(59, 59, 59)))
		{
			graphics.FillRectangle(brush2, new Rectangle(num7, 1, num8, num - 2));
		}
		Draw(graphics, "Nr pociągu", new Rectangle(num7, 0, num8, num));
	}

	private void Draw(Graphics g, string value, Rectangle r)
	{
		TextRenderer.DrawText(g, value, grid.Font, r, Color.White, TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
	}
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace EdrLocal;

public static class ReferenceTheme
{
	public const float InterfaceSize = 1.375f;

	public static readonly Font Small = new Font("Tahoma", 9f, FontStyle.Regular);

	public static readonly Font SmallBold = new Font("Tahoma", 9f, FontStyle.Bold);

	public static readonly Font Dialog = new Font("Segoe UI", 9f);

	private static Dictionary<string, Image> images = new Dictionary<string, Image>();

	private static Icon applicationIcon;

	public static Icon ApplicationIcon
	{
		get
		{
			if (applicationIcon == null)
			{
				using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EdrLocal.Assets.app.ico");
				if (stream == null)
				{
					throw new InvalidOperationException("Brak osadzonej ikony aplikacji.");
				}
				using Icon icon = new Icon(stream, new Size(32, 32));
				applicationIcon = (Icon)icon.Clone();
			}
			return applicationIcon;
		}
	}

	public static Image Asset(string name)
	{
		if (!images.ContainsKey(name))
		{
			using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EdrLocal.Assets." + name + ".png");
			if (stream == null)
			{
				return null;
			}
			images[name] = new Bitmap(stream);
		}
		return images[name];
	}

	public static void Gradient(Graphics g, Rectangle r, Color top, Color bottom)
	{
		if (r.Width < 1 || r.Height < 1)
		{
			return;
		}
		using LinearGradientBrush brush = new LinearGradientBrush(r, top, bottom, 90f);
		g.FillRectangle(brush, r);
	}

	public static void Silver(Graphics g, Rectangle r, bool pressed)
	{
		Gradient(g, r, pressed ? Color.FromArgb(170, 170, 170) : Color.White, Color.FromArgb(171, 171, 171));
		using (Pen pen = new Pen(Color.FromArgb(125, 125, 125)))
		{
			g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
		}
		using Pen pen2 = new Pen(Color.White);
		g.DrawRectangle(pen2, r.X + 1, r.Y + 1, r.Width - 3, r.Height - 3);
	}

	public static TabControl Tabs()
	{
		ReferenceTabs referenceTabs = new ReferenceTabs();
		referenceTabs.Dock = DockStyle.Fill;
		referenceTabs.Font = Small;
		referenceTabs.Padding = new Point(11, 1);
		referenceTabs.ItemSize = new Size(0, 16);
		referenceTabs.DrawMode = TabDrawMode.OwnerDrawFixed;
		return referenceTabs;
	}

	public static void Grid(DataGridView grid, bool journal)
	{
		grid.Font = Small;
		grid.RowTemplate.Height = 16;
		grid.ColumnHeadersHeight = 14;
		grid.DefaultCellStyle.Padding = Padding.Empty;
		grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
		grid.ColumnHeadersDefaultCellStyle.Font = SmallBold;
		grid.DefaultCellStyle.ForeColor = Color.Black;
		grid.BackgroundColor = Color.White;
		grid.BorderStyle = BorderStyle.FixedSingle;
		grid.GridColor = Color.FromArgb(215, 215, 215);
		grid.DefaultCellStyle.SelectionBackColor = EdrPalette.Cyan;
		grid.DefaultCellStyle.SelectionForeColor = Color.Black;
		grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
		if (journal)
		{
			grid.CurrentCellChanged += (object s, EventArgs e) =>
			{
				grid.Invalidate();
			};
		}
		grid.CellPainting += (object s, DataGridViewCellPaintingEventArgs e) =>
		{
			if (e.ColumnIndex >= 0)
			{
				Rectangle cellBounds = e.CellBounds;
				string name = grid.Columns[e.ColumnIndex].Name;
				if (e.RowIndex >= 0)
				{
					e.PaintBackground(e.ClipBounds, cellsPaintSelectionBackground: true);
					JournalEntry journalEntry = grid.Rows[e.RowIndex].Tag as JournalEntry;
					bool flag = journal && journalEntry != null && journalEntry.Type == "Pociąg" && grid.CurrentCell != null && grid.CurrentCell.RowIndex == e.RowIndex && grid.SelectedCells.Count > 0;
					if (flag)
					{
						if (e.CellStyle.BackColor == EdrPalette.Required)
						{
							using SolidBrush brush = new SolidBrush(EdrPalette.Required);
							e.Graphics.FillRectangle(brush, cellBounds);
						}
						else
						{
							Gradient(e.Graphics, cellBounds, Color.FromArgb(255, 239, 199), Color.FromArgb(253, 212, 168));
						}
					}
					e.Paint(e.ClipBounds, DataGridViewPaintParts.Border);
					if (grid.Columns[e.ColumnIndex] is DataGridViewCheckBoxColumn)
					{
						e.Paint(e.ClipBounds, DataGridViewPaintParts.All);
					}
					else
					{
						TextFormatFlags textFormatFlags = TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;
						textFormatFlags = (TextFormatFlags)((int)textFormatFlags | ((e.CellStyle.Alignment != DataGridViewContentAlignment.MiddleLeft) ? 1 : 0));
						bool flag2 = (e.State & DataGridViewElementStates.Selected) != 0;
						string text = Convert.ToString(e.FormattedValue);
						Color foreColor = (flag ? Color.Black : (flag2 ? e.CellStyle.SelectionForeColor : e.CellStyle.ForeColor));
						TextRenderer.DrawText(e.Graphics, text, e.CellStyle.Font ?? grid.Font, new Rectangle(cellBounds.X + 1 + ((name == "to" && grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Tag as string == "registered") ? Math.Max(4, (int)Math.Round((double)grid.Font.Height * 0.45)) : 0), cellBounds.Y, cellBounds.Width - 2 - (((name == "from" || name == "to") && grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Tag as string == "registered") ? Math.Max(4, (int)Math.Round((double)grid.Font.Height * 0.45)) : 0), cellBounds.Height), foreColor, textFormatFlags);
					}
					if ((name == "from" || name == "to") && grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Tag as string == "registered")
					{
						int num = Math.Max(4, (int)Math.Round((double)grid.Font.Height * 0.45));
						e.Graphics.FillRectangle(Brushes.Gray, (name == "to") ? (cellBounds.Left + 1) : (cellBounds.Right - num - 1), cellBounds.Top + 1, num, cellBounds.Height - 2);
					}
					e.Handled = true;
				}
				else if (e.RowIndex == -1)
				{
					bool flag3 = Convert.ToString(grid.Tag) == "route";
					Color bottom = (flag3 ? RouteAppearance.Header(e.ColumnIndex) : EdrPalette.Header(name, Convert.ToString(grid.Tag) == "detail"));
					bool flag4 = Convert.ToString(grid.Tag) == "telephone";
					Color top = Color.White;
					if (flag4)
					{
						top = Color.FromArgb(165, 165, 165);
						bottom = Color.FromArgb(35, 35, 35);
					}
					Gradient(e.Graphics, cellBounds, top, bottom);
					using (Pen pen = new Pen(Color.FromArgb(190, 190, 190)))
					{
						e.Graphics.DrawRectangle(pen, cellBounds.X, cellBounds.Y, cellBounds.Width - 1, cellBounds.Height - 1);
					}
					Graphics graphics = e.Graphics;
					string headerText = grid.Columns[e.ColumnIndex].HeaderText;
					Font font = grid.ColumnHeadersDefaultCellStyle.Font ?? grid.Font;
					Rectangle bounds = new Rectangle(cellBounds.X + 1, cellBounds.Y, cellBounds.Width - 2, cellBounds.Height);
					Color foreColor2;
					if (!flag3)
					{
						switch (name)
						{
						default:
							foreColor2 = Color.White;
							break;
						case "odd":
						case "from":
						case "line":
							foreColor2 = Color.Black;
							break;
						}
					}
					else
					{
						foreColor2 = RouteAppearance.HeaderInk(e.ColumnIndex);
					}
					TextRenderer.DrawText(graphics, headerText, font, bounds, foreColor2, TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
					e.Handled = true;
				}
			}
		};
	}

	public static ToolStrip Strip(int height)
	{
		ToolStrip toolStrip = new ToolStrip();
		toolStrip.GripStyle = ToolStripGripStyle.Hidden;
		toolStrip.AutoSize = false;
		toolStrip.Height = height;
		toolStrip.Font = Small;
		toolStrip.Padding = Padding.Empty;
		toolStrip.Renderer = new ReferenceStripRenderer();
		toolStrip.ImageScalingSize = new Size(28, 20);
		return toolStrip;
	}

	public static ToolStripButton Icon(string asset, string hint, Action action)
	{
		ToolStripButton item = new ToolStripButton
		{
			Image = Asset(asset),
			ImageScaling = ToolStripItemImageScaling.SizeToFit,
			DisplayStyle = ToolStripItemDisplayStyle.Image,
			ToolTipText = hint,
			AutoSize = false,
			Size = new Size(31, 22),
			Margin = new Padding(1, 0, 1, 0)
		};
		item.Click += (object s, EventArgs e) =>
		{
			Ui.Run(item.GetCurrentParent(), action);
		};
		return item;
	}
}

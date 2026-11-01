using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace EdrLocal;

public sealed class ConfigurationGrid : ReferenceGrid
{
	public readonly List<Tuple<string, int, int>> Groups = new List<Tuple<string, int, int>>();

	public ConfigurationGrid()
	{
		Dock = DockStyle.Fill;
		DoubleBuffered = true;
		SelectWithWheel = false;
		ReadOnly = true;
		MultiSelect = false;
		AllowUserToAddRows = false;
		AllowUserToDeleteRows = false;
		AllowUserToResizeRows = false;
		RowHeadersVisible = false;
		AutoGenerateColumns = false;
		BackgroundColor = Color.White;
		BorderStyle = BorderStyle.FixedSingle;
		GridColor = Color.FromArgb(210, 210, 210);
		Font = new Font("Tahoma", 8.25f);
		RowTemplate.Height = 22;
		EnableHeadersVisualStyles = false;
		ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
		ColumnHeadersHeight = 54;
		DefaultCellStyle.SelectionBackColor = Color.FromArgb(255, 228, 163);
		DefaultCellStyle.SelectionForeColor = Color.Black;
		DefaultCellStyle.NullValue = "";
		ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(62, 62, 62);
		ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
		ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
		SelectionMode = DataGridViewSelectionMode.FullRowSelect;
		EditMode = DataGridViewEditMode.EditOnEnter;
		EventHandler value = (object s, EventArgs e) =>
		{
			if (IsCurrentCellDirty && CurrentCell is DataGridViewCheckBoxCell)
			{
				CommitEdit(DataGridViewDataErrorContexts.Commit);
			}
		};
		CurrentCellDirtyStateChanged += value;
		DataError += (object s, DataGridViewDataErrorEventArgs e) =>
		{
			e.ThrowException = false;
		};
	}

	protected override void OnCellPainting(DataGridViewCellPaintingEventArgs e)
	{
		if (e.RowIndex == -1 && e.ColumnIndex >= 0 && Groups.Count > 0)
		{
			Rectangle cellBounds = e.CellBounds;
			using (SolidBrush brush = new SolidBrush(ColumnHeadersDefaultCellStyle.BackColor))
			{
				e.Graphics.FillRectangle(brush, cellBounds);
			}
			Rectangle rect = new Rectangle(cellBounds.X, cellBounds.Y + 21, cellBounds.Width, cellBounds.Height - 21);
			using (Pen pen = new Pen(Color.FromArgb(150, 150, 150)))
			{
				e.Graphics.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
			}
			TextRenderer.DrawText(e.Graphics, Columns[e.ColumnIndex].HeaderText, Font, Rectangle.Inflate(rect, -3, -1), Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
			e.Handled = true;
		}
		base.OnCellPainting(e);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		if (Groups.Count == 0)
		{
			return;
		}
		foreach (Tuple<string, int, int> group in Groups)
		{
			Rectangle cellDisplayRectangle = GetCellDisplayRectangle(group.Item2, -1, cutOverflow: true);
			Rectangle cellDisplayRectangle2 = GetCellDisplayRectangle(group.Item3, -1, cutOverflow: true);
			Rectangle rect = new Rectangle(cellDisplayRectangle.Left, 1, cellDisplayRectangle2.Right - cellDisplayRectangle.Left, 21);
			if (rect.Width > 0)
			{
				using (SolidBrush brush = new SolidBrush(Color.FromArgb(62, 62, 62)))
				{
					e.Graphics.FillRectangle(brush, rect);
				}
				using (Pen pen = new Pen(Color.Gray))
				{
					e.Graphics.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
				}
				TextRenderer.DrawText(e.Graphics, group.Item1, Font, Rectangle.Inflate(rect, -3, 0), Color.White, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
			}
		}
	}
}

using System;
using System.Drawing;
using System.Windows.Forms;

namespace EdrLocal;

public sealed class SlashCell : DataGridViewCheckBoxCell
{
	public SlashCell()
	{
		TrueValue = "/";
		FalseValue = "";
		ValueType = typeof(string);
		Style.NullValue = "";
	}

	protected override void OnContentClick(DataGridViewCellEventArgs e)
	{
		if (DataGridView != null && !DataGridView.ReadOnly && !ReadOnly && !DataGridView.IsCurrentCellInEditMode)
		{
			DataGridView.BeginEdit(selectAll: false);
		}
		base.OnContentClick(e);
	}

	protected override void Paint(Graphics g, Rectangle clip, Rectangle bounds, int row, DataGridViewElementStates state, object value, object formatted, string error, DataGridViewCellStyle style, DataGridViewAdvancedBorderStyle border, DataGridViewPaintParts parts)
	{
		bool flag = DataGridView != null && DataGridView.CurrentCell == this && DataGridView.IsCurrentCellInEditMode;
		base.Paint(g, clip, bounds, row, state, value, formatted, error, style, border, flag ? parts : (parts & ~DataGridViewPaintParts.ContentForeground));
		if (!flag && (parts & DataGridViewPaintParts.ContentForeground) != 0)
		{
			Color foreColor = (((state & DataGridViewElementStates.Selected) != 0) ? style.SelectionForeColor : style.ForeColor);
			TextRenderer.DrawText(g, (Convert.ToString(value) == "/") ? "/" : "", style.Font, bounds, foreColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
		}
	}
}

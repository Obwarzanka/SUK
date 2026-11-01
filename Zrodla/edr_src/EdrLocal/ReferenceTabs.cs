using System.Drawing;
using System.Windows.Forms;

namespace EdrLocal;

public class ReferenceTabs : TabControl
{
	protected override void OnDrawItem(DrawItemEventArgs e)
	{
		Rectangle tabRect = GetTabRect(e.Index);
		ReferenceTheme.Gradient(e.Graphics, tabRect, (e.Index == SelectedIndex) ? Color.White : Color.FromArgb(235, 235, 235), (e.Index == SelectedIndex) ? Color.FromArgb(247, 247, 247) : Color.FromArgb(205, 205, 205));
		using (Pen pen = new Pen(Color.FromArgb(175, 175, 175)))
		{
			e.Graphics.DrawRectangle(pen, tabRect.X, tabRect.Y, tabRect.Width - 1, tabRect.Height - 1);
		}
		Color foreColor = ((TabPages[e.Index].Text == "Dziennik telefoniczny") ? Color.Green : Color.FromArgb(30, 40, 55));
		TextRenderer.DrawText(e.Graphics, TabPages[e.Index].Text, Font, tabRect, foreColor, TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
	}
}

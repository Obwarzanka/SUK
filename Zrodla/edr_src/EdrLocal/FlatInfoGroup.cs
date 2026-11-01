using System.Drawing;
using System.Windows.Forms;

namespace EdrLocal;

public class FlatInfoGroup : GroupBox
{
	protected override void OnPaint(PaintEventArgs e)
	{
		e.Graphics.Clear(BackColor);
		TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(0, 0, Width, Font.Height + 2), Color.Black, TextFormatFlags.NoPadding);
	}
}

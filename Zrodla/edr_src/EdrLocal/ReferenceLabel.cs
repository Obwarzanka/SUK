using System.Windows.Forms;

namespace EdrLocal;

public class ReferenceLabel : Label
{
	protected override void OnPaint(PaintEventArgs e)
	{
		TextFormatFlags flags = (TextFormatFlags)(0x10000800 | (Text.Contains("\n") ? 16 : 32));
		TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor, flags);
	}
}

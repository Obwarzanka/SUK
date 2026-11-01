using System.Drawing;
using System.Windows.Forms;

namespace EdrLocal;

public class TnButton : Button
{
	public bool Alert;

	public TnButton()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		ReferenceTheme.Gradient(e.Graphics, ClientRectangle, Alert ? Color.Yellow : Color.Yellow, Alert ? Color.OrangeRed : Color.Lime);
		ControlPaint.DrawBorder(e.Graphics, ClientRectangle, Color.Gray, ButtonBorderStyle.Solid);
		TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Color.Black, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
	}
}

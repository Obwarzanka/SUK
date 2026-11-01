using System.Drawing;
using System.Windows.Forms;

namespace EdrLocal;

public class SilverButton : Button
{
	private bool down;

	public SilverButton()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		FlatStyle = FlatStyle.Flat;
		Font = ReferenceTheme.Dialog;
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		down = true;
		base.OnMouseDown(e);
		Invalidate();
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		down = false;
		base.OnMouseUp(e);
		Invalidate();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		ReferenceTheme.Silver(e.Graphics, ClientRectangle, down);
		TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Enabled ? ForeColor : Color.Gray, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
		if (Focused)
		{
			ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4));
		}
	}
}

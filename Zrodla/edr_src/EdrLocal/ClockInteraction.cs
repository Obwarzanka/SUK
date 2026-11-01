using System;
using System.Drawing;
using System.Windows.Forms;

namespace EdrLocal;

internal static class ClockInteraction
{
	public static bool Hours(TextBox box, int x)
	{
		TextFormatFlags flags = TextFormatFlags.NoPadding;
		int width = TextRenderer.MeasureText("00:00", box.Font, Size.Empty, flags).Width;
		int num;
		if (box.TextAlign == HorizontalAlignment.Center)
		{
			num = (box.ClientSize.Width - width) / 2;
		}
		else
		{
			num = ((box.TextAlign == HorizontalAlignment.Right) ? (box.ClientSize.Width - width - 2) : 2);
		}
		int width2 = TextRenderer.MeasureText("00", box.Font, Size.Empty, flags).Width;
		int width3 = TextRenderer.MeasureText(":", box.Font, Size.Empty, flags).Width;
		return x < num + width2 + width3 / 2;
	}

	public static int Steps(ref int remainder, int delta)
	{
		remainder += delta;
		int result = remainder / SystemInformation.MouseWheelScrollDelta;
		remainder %= SystemInformation.MouseWheelScrollDelta;
		return result;
	}

	public static void PaintSuggestion(TextBox box, Graphics graphics, string text)
	{
		Rectangle bounds = new Rectangle(2, 1, Math.Max(0, box.ClientSize.Width - 4), Math.Max(0, box.ClientSize.Height - 2));
		TextRenderer.DrawText(graphics, text, box.Font, bounds, SystemColors.GrayText, box.BackColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
	}
}

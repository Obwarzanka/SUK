using System;
using System.Drawing;
using System.Windows.Forms;

namespace EdrLocal;

public class AdditionalTrainInfo : Label
{
	private Font nameFont;

	internal Font PrefixFont
	{
		get
		{
			if (nameFont == null)
			{
				nameFont = new Font(Font, Font.Style | FontStyle.Bold);
			}
			return nameFont;
		}
	}

	internal Font FontForLine(string line)
	{
		if (!line.StartsWith("N:", StringComparison.Ordinal))
		{
			return Font;
		}
		return PrefixFont;
	}

	protected override void OnFontChanged(EventArgs e)
	{
		if (nameFont != null)
		{
			nameFont.Dispose();
			nameFont = null;
		}
		base.OnFontChanged(e);
		Invalidate();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		string[] array = Text.Replace("\r", "").Split('\n');
		int num = Padding.Top;
		TextFormatFlags textFormatFlags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
		int num2 = TextRenderer.MeasureText(e.Graphics, "Do:", PrefixFont, Size.Empty, textFormatFlags).Width + TextRenderer.MeasureText(e.Graphics, " ", Font, Size.Empty, textFormatFlags).Width;
		string[] array2 = array;
		foreach (string text in array2)
		{
			Font font = FontForLine(text);
			Rectangle bounds = new Rectangle(Padding.Left, num, Math.Max(0, ClientSize.Width - Padding.Horizontal), font.Height);
			int num3 = text.IndexOf(':');
			string text2 = ((num3 < 0) ? "" : text.Substring(0, num3 + 1));
			switch (text2)
			{
			case "Z:":
			case "Do:":
			case "P:":
			case "N:":
				TextRenderer.DrawText(e.Graphics, text2, PrefixFont, new Rectangle(bounds.X, bounds.Y, Math.Min(num2, bounds.Width), bounds.Height), ForeColor, textFormatFlags);
				TextRenderer.DrawText(bounds: new Rectangle(bounds.X + num2, bounds.Y, Math.Max(0, bounds.Width - num2), bounds.Height), dc: e.Graphics, text: text.Substring(num3 + 1).TrimStart(), font: font, foreColor: ForeColor, flags: textFormatFlags | TextFormatFlags.EndEllipsis);
				break;
			default:
				TextRenderer.DrawText(e.Graphics, text, font, bounds, ForeColor, textFormatFlags | TextFormatFlags.EndEllipsis);
				break;
			}
			num += Font.Height;
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && nameFont != null)
		{
			nameFont.Dispose();
			nameFont = null;
		}
		base.Dispose(disposing);
	}
}

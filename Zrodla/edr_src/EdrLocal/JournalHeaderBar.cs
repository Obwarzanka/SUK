using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EdrLocal;

public sealed class JournalHeaderBar : Panel
{
	private readonly Label caption;

	private readonly FlowLayoutPanel rails;

	private readonly ToolStrip tools;

	private float dpi = 1f;

	private bool arranging;

	public JournalHeaderBar(Label heading, FlowLayoutPanel signals, ToolStrip buttons)
	{
		caption = heading;
		rails = signals;
		tools = buttons;
		Dock = DockStyle.Top;
		BackColor = Color.White;
		Padding = (Margin = Padding.Empty);
		caption.Dock = (rails.Dock = (tools.Dock = DockStyle.None));
		caption.AutoEllipsis = true;
		Controls.Add(caption);
		Controls.Add(rails);
		Controls.Add(tools);
		EventHandler value = (object s, EventArgs e) =>
		{
			Arrange();
		};
		SizeChanged += value;
		tools.Layout += (object s, LayoutEventArgs e) =>
		{
			Arrange();
		};
		ApplyDpi(1f);
	}

	private int Px(int value)
	{
		return (int)Math.Round((float)value * dpi);
	}

	public void ApplyDpi(float scale)
	{
		dpi = scale;
		arranging = true;
		try
		{
			foreach (RailSignal item in rails.Controls.OfType<RailSignal>())
			{
				item.Size = new Size(Px(60), Px(45));
				item.Margin = Padding.Empty;
			}
			tools.ImageScalingSize = new Size(Px(50), Px(35));
			ToolStrip toolStrip = tools;
			Padding padding = (tools.Margin = Padding.Empty);
			toolStrip.Padding = padding;
			foreach (ToolStripItem item2 in tools.Items)
			{
				if (item2.DisplayStyle == ToolStripItemDisplayStyle.Image && item2.Image != null)
				{
					item2.AutoSize = false;
					item2.Size = new Size(Px(52), Px(39));
					item2.Padding = Padding.Empty;
					item2.Margin = new Padding(Px(1), 0, Px(1), 0);
				}
			}
		}
		finally
		{
			arranging = false;
		}
		Arrange();
	}

	private void Arrange()
	{
		if (arranging || ClientSize.Width < 1)
		{
			return;
		}
		arranging = true;
		SuspendLayout();
		try
		{
			int num = ClientSize.Width;
			int num2 = Px(48);
			int num3 = rails.Controls.Cast<Control>().Sum((Control x) => x.Width + x.Margin.Horizontal);
			int num4 = tools.Items.Cast<ToolStripItem>().Sum((ToolStripItem x) => x.Width + x.Margin.Horizontal) + Px(3);
			int num5 = Px(120);
			int num6 = Px(1);
			int num7 = 0;
			int num8 = num2;
			Rectangle bounds;
			if (num >= num3 + num4 + num5)
			{
				bounds = new Rectangle(num3, 0, num - num3 - num4, num2);
			}
			else if (num >= num3 + num4 + Px(2))
			{
				int num9 = Px(24);
				bounds = new Rectangle(0, 0, num, num9);
				num6 += num9;
				num7 = num9;
				num8 += num9;
			}
			else
			{
				bounds = new Rectangle(num3, 0, Math.Max(0, num - num3), num2);
				num7 = num2;
				num8 = num2 * 2;
			}
			Height = num8;
			caption.Bounds = bounds;
			rails.Bounds = new Rectangle(0, num6, num3, Px(45));
			tools.Bounds = new Rectangle(Math.Max(0, num - num4), num7, num4, num2);
		}
		finally
		{
			ResumeLayout(performLayout: true);
			arranging = false;
		}
	}
}

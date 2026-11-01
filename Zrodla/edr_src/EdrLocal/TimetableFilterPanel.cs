using System;
using System.Drawing;
using System.Windows.Forms;

namespace EdrLocal;

public class TimetableFilterPanel : Panel
{
	private readonly MainForm owner;

	private bool arranging;

	public FlowLayoutPanel Options { get; private set; }

	public FlowLayoutPanel SearchRow { get; private set; }

	public TableLayoutPanel Notes { get; private set; }

	public Panel Info { get; private set; }

	public TimetableFilterPanel(MainForm main)
	{
		owner = main;
		Dock = DockStyle.Top;
		BackColor = Color.White;
		Height = 80;
		Margin = Padding.Empty;
		Options = Flow();
		SearchRow = Flow();
		Notes = new TableLayoutPanel
		{
			ColumnCount = 6,
			RowCount = 1,
			Margin = Padding.Empty,
			Padding = Padding.Empty
		};
		for (int i = 0; i < 3; i++)
		{
			Notes.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			Notes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
		}
		Info = new Panel
		{
			Text = "",
			ForeColor = EdrPalette.Navy,
			Font = ReferenceTheme.Small,
			Margin = Padding.Empty,
			Padding = new Padding(5, 13, 5, 3)
		};
		Controls.Add(Options);
		Controls.Add(SearchRow);
		Controls.Add(Notes);
		Controls.Add(Info);
	}

	private static FlowLayoutPanel Flow()
	{
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel();
		flowLayoutPanel.AutoSize = false;
		flowLayoutPanel.WrapContents = true;
		flowLayoutPanel.FlowDirection = FlowDirection.LeftToRight;
		flowLayoutPanel.Margin = Padding.Empty;
		flowLayoutPanel.Padding = Padding.Empty;
		flowLayoutPanel.BackColor = Color.White;
		return flowLayoutPanel;
	}

	public static FlowLayoutPanel Group(params Control[] controls)
	{
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel();
		flowLayoutPanel.AutoSize = true;
		flowLayoutPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		flowLayoutPanel.WrapContents = false;
		flowLayoutPanel.Margin = new Padding(0, 0, 3, 0);
		flowLayoutPanel.Padding = Padding.Empty;
		FlowLayoutPanel flowLayoutPanel2 = flowLayoutPanel;
		foreach (Control control in controls)
		{
			control.Dock = DockStyle.None;
			control.Margin = new Padding(0, 0, 2, 0);
			flowLayoutPanel2.Controls.Add(control);
		}
		return flowLayoutPanel2;
	}

	public void Reflow()
	{
		if (arranging || owner.IsApplyingScale || Options == null || Width < 1)
		{
			return;
		}
		arranging = true;
		try
		{
			float currentZoom = owner.CurrentZoom;
			int num = Math.Max(2, (int)Math.Round(2f * currentZoom));
			bool flag = (float)Width / currentZoom >= 1030f;
			int num2 = (int)Math.Round(235f * currentZoom);
			int num3 = (flag ? ((int)Math.Round(675f * currentZoom)) : Math.Max(200, Width - num2 - num * 3));
			int num4 = num;
			FlowLayoutPanel[] array = new FlowLayoutPanel[2] { Options, SearchRow };
			foreach (FlowLayoutPanel flowLayoutPanel in array)
			{
				int num5 = ((flag && flowLayoutPanel == Options) ? (Width - (int)Math.Round(255f * currentZoom) - num * 2) : num3);
				flowLayoutPanel.SetBounds(num, num4, num5, 1);
				foreach (Control control in flowLayoutPanel.Controls)
				{
					control.PerformLayout();
					control.Size = control.GetPreferredSize(Size.Empty);
				}
				int num6 = Math.Max(Font.Height + 3, flowLayoutPanel.GetPreferredSize(new Size(num5, 0)).Height);
				flowLayoutPanel.SetBounds(num, num4, num5, num6);
				flowLayoutPanel.PerformLayout();
				num4 += num6 + num;
			}
			int num7 = Math.Max(Font.Height * 2 + 4, (int)Math.Round(22f * currentZoom));
			Notes.SetBounds(num, num4, num3, num7);
			num4 += num7 + num;
			int num8 = (flag ? (Options.Bottom + num) : num);
			int num9 = Math.Max(num4, num8 + Font.Height * 4 + num * 2);
			Info.Padding = new Padding(num, (!flag) ? (Font.Height + num) : 0, num, 0);
			Info.SetBounds(flag ? ((int)Math.Round(680f * currentZoom)) : (Width - num2 - num), num8, flag ? Math.Max(num2, Width - (int)Math.Round(935f * currentZoom)) : num2, num9 - num8 - num);
			if (Height != num9)
			{
				Height = num9;
			}
		}
		finally
		{
			arranging = false;
		}
	}

	protected override void OnLayout(LayoutEventArgs e)
	{
		base.OnLayout(e);
		Reflow();
	}
}

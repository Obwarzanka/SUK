using System;
using System.Drawing;
using System.Windows.Forms;

namespace EdrLocal;

public class StationTrackComboBox : ComboBox
{
	protected Engine engine;

	protected StationTrackComboBox()
	{
		Name = "stationTrack";
		DropDownStyle = ComboBoxStyle.DropDown;
		DrawMode = DrawMode.OwnerDrawFixed;
		BackColor = SystemColors.Window;
		ForeColor = SystemColors.WindowText;
	}

	public StationTrackComboBox(Engine source, string selected)
		: this()
	{
		Configure(source, selected);
	}

	public void Configure(Engine source, string selected)
	{
		engine = source;
		BeginUpdate();
		try
		{
			Items.Clear();
			Items.Add("");
			foreach (string item in StationSettings.TrackChoices(source.Station, selected))
			{
				Items.Add(item);
			}
			Text = selected ?? "";
			BackColor = SystemColors.Window;
			ForeColor = SystemColors.WindowText;
		}
		finally
		{
			EndUpdate();
		}
	}

	protected override void OnDrawItem(DrawItemEventArgs e)
	{
		bool flag = (e.State & DrawItemState.Selected) != 0;
		using (SolidBrush brush = new SolidBrush(flag ? SystemColors.Highlight : SystemColors.Window))
		{
			e.Graphics.FillRectangle(brush, e.Bounds);
		}
		if (e.Index >= 0)
		{
			string text = Convert.ToString(Items[e.Index]);
			Graphics graphics = e.Graphics;
			string text2 = text;
			Font font = Font;
			Rectangle bounds = e.Bounds;
			Color foreColor;
			if (flag)
			{
				foreColor = SystemColors.HighlightText;
			}
			else
			{
				foreColor = ((engine == null) ? SystemColors.WindowText : StationTrackColors.ForTrack(engine, text));
			}
			TextRenderer.DrawText(graphics, text2, font, bounds, foreColor, TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
		}
		e.DrawFocusRectangle();
	}

	protected override void OnDropDown(EventArgs e)
	{
		BackColor = SystemColors.Window;
		ForeColor = SystemColors.WindowText;
		base.OnDropDown(e);
		Invalidate();
	}
}

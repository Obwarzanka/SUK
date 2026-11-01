using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EdrLocal;

public sealed class SituationForm : ReferenceForm
{
	private readonly Engine engine;

	private readonly float zoom;

	private readonly Font regular;

	private readonly Font bold;

	private readonly Font noticeFont;

	private readonly Panel sections;

	private readonly List<Control> groups = new List<Control>();

	private int S(int value)
	{
		return (int)Math.Round((float)value * zoom);
	}

	public SituationForm(Engine value, float scale)
	{
		engine = value;
		zoom = Math.Max(1f, scale / 1.375f);
		regular = new Font("Tahoma", 9f);
		bold = new Font("Tahoma", 9f, FontStyle.Bold);
		noticeFont = new Font("Tahoma", 7.5f, FontStyle.Bold);
		Text = "Posterunek ruchu - sytuacja bieżąca";
		Font = regular;
		StartPosition = FormStartPosition.CenterParent;
		MinimizeBox = false;
		MaximizeBox = false;
		Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
		ClientSize = new Size(Math.Min(S(650), workingArea.Width - 50), Math.Min(S(445), workingArea.Height - 75));
		MinimumSize = new Size(S(500), S(280));
		Panel panel = new Panel
		{
			Dock = DockStyle.Top,
			Height = S(48),
			BackColor = Color.FromArgb(245, 245, 220),
			Padding = new Padding(S(6))
		};
		Label value2 = new Label
		{
			Text = "Posterunek:  " + engine.Station.Name.ToUpper() + " (" + StationIdentity.Type(engine.Station) + ")",
			Font = bold,
			Dock = DockStyle.Top,
			Height = S(21),
			AutoEllipsis = true
		};
		Label value3 = new Label
		{
			Text = "Bieżąca sytuacja ruchowa na szlakach przyległych:",
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft
		};
		panel.Controls.Add(value3);
		panel.Controls.Add(value2);
		Panel panel2 = new Panel
		{
			Dock = DockStyle.Bottom,
			Height = S(60),
			BackColor = Color.FromArgb(230, 230, 230)
		};
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Right,
			Width = S(178),
			FlowDirection = FlowDirection.LeftToRight,
			Padding = new Padding(0, S(9), 0, 0),
			WrapContents = false
		};
		SilverButton silverButton = new SilverButton
		{
			Text = "OK",
			Size = new Size(S(80), S(29)),
			Font = regular,
			DialogResult = DialogResult.OK
		};
		SilverButton silverButton2 = new SilverButton
		{
			Text = "Anuluj",
			Size = new Size(S(80), S(29)),
			Font = regular,
			DialogResult = DialogResult.Cancel
		};
		flowLayoutPanel.Controls.Add(silverButton);
		flowLayoutPanel.Controls.Add(silverButton2);
		AcceptButton = silverButton;
		CancelButton = silverButton2;
		Label value4 = new Label
		{
			Text = "UWAGA! Przed przekazaniem dyżuru należy upewnić się, że powyższa informacja jest zgodna ze stanem faktycznym. Jeżeli tak nie jest, należy kliknąć przycisk „Anuluj” i uzupełnić/dostosować zapisy/ustawienia w dziennikach ruchu.",
			ForeColor = Color.Red,
			Font = noticeFont,
			Dock = DockStyle.Fill,
			Padding = new Padding(S(4)),
			AutoEllipsis = true
		};
		panel2.Controls.Add(value4);
		panel2.Controls.Add(flowLayoutPanel);
		sections = new Panel
		{
			Dock = DockStyle.Fill,
			AutoScroll = true,
			BackColor = Color.White,
			Padding = new Padding(S(5))
		};
		Controls.Add(sections);
		Controls.Add(panel);
		Controls.Add(panel2);
		sections.BringToFront();
		foreach (Section item in engine.Station.Sections.Where(StationSettings.Conducted))
		{
			BuildSection(item);
		}
		sections.SizeChanged += (object s, EventArgs e) =>
		{
			Reflow();
		};
		Shown += (object s, EventArgs e) =>
		{
			Reflow();
		};
		Disposed += (object s, EventArgs e) =>
		{
			regular.Dispose();
			bold.Dispose();
			noticeFont.Dispose();
		};
		Reflow();
	}

	private Label Label(string text, bool strong)
	{
		Label label = new Label();
		label.Text = text;
		label.Font = (strong ? bold : regular);
		label.AutoSize = false;
		label.AutoEllipsis = true;
		label.TextAlign = ContentAlignment.MiddleLeft;
		label.Margin = Padding.Empty;
		return label;
	}

	private void BuildSection(Section section)
	{
		Panel panel = new Panel();
		panel.BackColor = Color.White;
		panel.Height = S(31 + section.Tracks * 30);
		panel.Margin = Padding.Empty;
		panel.Tag = section.Id;
		Panel panel2 = panel;
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
		tableLayoutPanel.Dock = DockStyle.Top;
		tableLayoutPanel.Height = S(25);
		tableLayoutPanel.ColumnCount = 2;
		tableLayoutPanel.RowCount = 1;
		tableLayoutPanel.Margin = Padding.Empty;
		TableLayoutPanel tableLayoutPanel2 = tableLayoutPanel;
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62f));
		Label label = Label("Szlak do/z: " + section.Name.ToUpper(), strong: true);
		label.Dock = DockStyle.Fill;
		Label label2 = Label("Linia: " + section.Line + "   " + (section.LineName ?? ""), strong: true);
		label2.Dock = DockStyle.Fill;
		tableLayoutPanel2.Controls.Add(label, 0, 0);
		tableLayoutPanel2.Controls.Add(label2, 1, 0);
		TableLayoutPanel tableLayoutPanel3 = new TableLayoutPanel();
		tableLayoutPanel3.Dock = DockStyle.Fill;
		tableLayoutPanel3.ColumnCount = 2;
		tableLayoutPanel3.RowCount = section.Tracks;
		tableLayoutPanel3.Margin = new Padding(S(8), 0, 0, 0);
		tableLayoutPanel3.Padding = new Padding(S(9), 0, 0, 0);
		TableLayoutPanel tableLayoutPanel4 = tableLayoutPanel3;
		tableLayoutPanel4.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(200)));
		tableLayoutPanel4.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		int num = 0;
		foreach (int item in SectionTrackDirections.DisplayOrder(section))
		{
			tableLayoutPanel4.RowStyles.Add(new RowStyle(SizeType.Absolute, S(30)));
			string text;
			if (section.Tracks == 1)
			{
				text = "";
			}
			else
			{
				text = ((num == 0) ? " (lewy)" : " (prawy)");
			}
			Label label3 = Label("Tor szlakowy" + text + " nr:  " + item, strong: false);
			label3.Dock = DockStyle.Fill;
			tableLayoutPanel4.Controls.Add(label3, 0, num);
			string text2 = engine.LineStatus(section.Id, item);
			Label label4 = Label("Status:  " + text2, strong: true);
			label4.Dock = DockStyle.Fill;
			label4.ForeColor = (text2.StartsWith("WOLNY") ? Color.Green : Color.Red);
			label4.Tag = "status:" + section.Id + ":" + item;
			tableLayoutPanel4.Controls.Add(label4, 1, num++);
		}
		Panel panel3 = new Panel();
		panel3.Dock = DockStyle.Bottom;
		panel3.Height = 1;
		panel3.BackColor = Color.LightGray;
		Panel value = panel3;
		panel2.Controls.Add(tableLayoutPanel4);
		panel2.Controls.Add(tableLayoutPanel2);
		panel2.Controls.Add(value);
		tableLayoutPanel4.BringToFront();
		sections.Controls.Add(panel2);
		groups.Add(panel2);
	}

	private void Reflow()
	{
		int num = S(4);
		int num2 = Math.Max(S(400), sections.ClientSize.Width - S(12) - (sections.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0));
		foreach (Control group in groups)
		{
			group.SetBounds(S(4), num, num2, group.Height);
			num += group.Height + S(5);
		}
		sections.AutoScrollMinSize = new Size(0, num);
	}
}

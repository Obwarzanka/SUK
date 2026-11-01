using System;
using System.Drawing;
using System.Windows.Forms;

namespace EdrLocal;

public class Editor : ReferenceForm
{
	public TableLayoutPanel Fields { get; private set; }

	public FlowLayoutPanel Buttons { get; private set; }

	public Editor(string title, int width, int height)
	{
		Text = title;
		Font = ReferenceTheme.Dialog;
		ClientSize = new Size(width, height - 25);
		StartPosition = FormStartPosition.CenterParent;
		MinimizeBox = false;
		MaximizeBox = false;
		FormBorderStyle = FormBorderStyle.FixedDialog;
		ShowInTaskbar = false;
		AutoScaleMode = AutoScaleMode.None;
		Buttons = new FlowLayoutPanel
		{
			BackColor = Color.FromArgb(212, 212, 212),
			Dock = DockStyle.Bottom,
			Height = 43,
			FlowDirection = FlowDirection.RightToLeft,
			Padding = new Padding(4)
		};
		Controls.Add(Buttons);
		Panel panel = new Panel
		{
			Dock = DockStyle.Fill,
			AutoScroll = true,
			Padding = new Padding(12)
		};
		Fields = new TableLayoutPanel
		{
			Dock = DockStyle.Top,
			AutoSize = true,
			ColumnCount = 2
		};
		Fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170f));
		Fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		panel.Controls.Add(Fields);
		Controls.Add(panel);
		panel.BringToFront();
		Action action = () =>
		{
			DialogResult = DialogResult.Cancel;
			Close();
		};
		Button button = Ui.Button("Anuluj", action);
		Buttons.Controls.Add(button);
		CancelButton = button;
		Shown += (object s, EventArgs e) =>
		{
			if (Fields.Parent.Visible)
			{
				ClientSize = new Size(ClientSize.Width, Math.Min(ClientSize.Height, Math.Max(165, Fields.PreferredSize.Height + Padding.Vertical + Buttons.Height + Fields.Parent.Padding.Vertical + 6)));
			}
		};
	}

	public T Add<T>(string label, T control, int height) where T : Control
	{
		int row = Fields.RowCount++;
		Fields.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
		Label label2 = new Label();
		label2.Text = label;
		label2.Dock = DockStyle.Fill;
		label2.TextAlign = ContentAlignment.MiddleLeft;
		label2.AutoEllipsis = true;
		Label control2 = label2;
		control.Dock = DockStyle.Fill;
		Padding margin = new Padding(3, 5, 3, 5);
		control.Margin = margin;
		Fields.Controls.Add(control2, 0, row);
		Fields.Controls.Add(control, 1, row);
		return control;
	}

	public void Info(string text, int height)
	{
		Label label = new Label();
		label.Text = text;
		label.Dock = DockStyle.Fill;
		label.ForeColor = Color.FromArgb(70, 70, 70);
		label.TextAlign = ContentAlignment.MiddleLeft;
		Label control = label;
		int row = Fields.RowCount++;
		Fields.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
		Fields.Controls.Add(control, 0, row);
		Fields.SetColumnSpan(control, 2);
	}

	public Panel AbsoluteBody()
	{
		Fields.Parent.Visible = false;
		Panel panel = new Panel();
		panel.Dock = DockStyle.Fill;
		panel.BackColor = Color.FromArgb(240, 240, 240);
		Panel panel2 = panel;
		Controls.Add(panel2);
		panel2.BringToFront();
		Buttons.BackColor = Color.FromArgb(212, 212, 212);
		return panel2;
	}

	public void Save(Action action)
	{
		bool saved = false;
		Button button = Ui.Button("Zapisz", () =>
		{
			if (saved)
			{
				DialogResult = DialogResult.OK;
				Close();
			}
			else
			{
				action();
				saved = true;
				DialogResult = DialogResult.OK;
				Close();
			}
		});
		Buttons.Controls.Add(button);
		AcceptButton = button;
	}
}

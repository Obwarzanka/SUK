using System;
using System.Drawing;
using System.Windows.Forms;

namespace EdrLocal;

public sealed class ModeForm : ReferenceForm
{
	public string Provider { get; private set; }

	public string LegacyDatabase { get; private set; }

	public bool Simulator { get; private set; }

	public string ConfiguratorProvider { get; private set; }

	public ModeForm()
	{
		Text = "EDR - tryb pracy";
		ClientSize = new Size(540, 220);
		StartPosition = FormStartPosition.CenterScreen;
		Font = ReferenceTheme.Dialog;
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.TopDown,
			Padding = new Padding(22),
			WrapContents = false
		};
		Controls.Add(flowLayoutPanel);
		flowLayoutPanel.Controls.Add(new Label
		{
			Text = "Wybierz źródło rozkładu",
			AutoSize = true,
			Font = new Font(Font, FontStyle.Bold)
		});
		Action action = () =>
		{
			Provider = "simrail";
			Simulator = true;
			DialogResult = DialogResult.OK;
			Close();
		};
		Button button = Ui.Button("SimRail", action);
		button.AutoSize = false;
		button.Size = new Size(485, 48);
		flowLayoutPanel.Controls.Add(button);
		Button button2 = Ui.Button("TD2", () =>
		{
			Provider = "td2";
			Simulator = true;
			DialogResult = DialogResult.OK;
			Close();
		});
		button2.AutoSize = false;
		button2.Size = new Size(485, 48);
		button2.Enabled = true;
		flowLayoutPanel.Controls.Add(button2);
	}
}

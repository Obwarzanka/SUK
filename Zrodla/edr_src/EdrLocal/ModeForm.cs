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
		Button button3 = Ui.Button("Lokalnie (ręczny rozkład / CSV)", () =>
		{
			Simulator = false;
			DialogResult = DialogResult.OK;
			Close();
		});
		button3.AutoSize = false;
		button3.Size = new Size(485, 48);
		flowLayoutPanel.Controls.Add(button3);
		Button button4 = Ui.Button("Konfigurator SimRail", () =>
		{
			ConfiguratorProvider = "simrail";
			DialogResult = DialogResult.OK;
			Close();
		});
		button4.AutoSize = false;
		button4.Size = new Size(240, 40);
		Button button5 = Ui.Button("Konfigurator TD2", () =>
		{
			ConfiguratorProvider = "td2";
			DialogResult = DialogResult.OK;
			Close();
		});
		button5.AutoSize = false;
		button5.Size = new Size(240, 40);
		FlowLayoutPanel configurators = new FlowLayoutPanel
		{
			AutoSize = true,
			WrapContents = false,
			Margin = Padding.Empty
		};
		configurators.Controls.Add(button4);
		configurators.Controls.Add(button5);
		flowLayoutPanel.Controls.Add(configurators);
		ClientSize = new Size(540, 340);
	}
}

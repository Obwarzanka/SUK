using System;
using System.Drawing;
using System.Windows.Forms;

namespace EdrLocal;

public sealed class BranchRemovalForm : ReferenceForm
{
	public BranchRemovalForm(ConfigurationRemoval plan)
	{
		Text = "Usuń gałąź struktury";
		Font = ReferenceTheme.Dialog;
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MinimizeBox = false;
		MaximizeBox = false;
		ShowInTaskbar = false;
		StartPosition = FormStartPosition.CenterParent;
		ClientSize = new Size(590, 245);
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 1,
			Padding = new Padding(18),
			RowCount = 3
		};
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 70f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		tableLayoutPanel.Controls.Add(new Label
		{
			Text = "Usunąć " + plan.Caption + " wraz z zawartością?",
			Dock = DockStyle.Fill,
			Font = new Font(Font, FontStyle.Bold)
		}, 0, 0);
		tableLayoutPanel.Controls.Add(new Label
		{
			Text = "Usunięte ze struktury:\nSekcje ISE: " + plan.SectionCount + "\nPosterunki ruchu: " + plan.StationCount,
			Dock = DockStyle.Fill
		}, 0, 1);
		tableLayoutPanel.Controls.Add(new Label
		{
			Text = "Zapisane profile posterunków oraz dzienniki pozostaną zachowane.\nPosterunki będzie można ponownie dodać do struktury.",
			Dock = DockStyle.Fill
		}, 0, 2);
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Bottom,
			Height = 43,
			FlowDirection = FlowDirection.RightToLeft,
			Padding = new Padding(4)
		};
		Action action = () =>
		{
			DialogResult = DialogResult.Cancel;
			Close();
		};
		Button button = Ui.Button("Anuluj", action);
		Button value = Ui.Button("Usuń ze struktury", () =>
		{
			DialogResult = DialogResult.OK;
			Close();
		});
		flowLayoutPanel.Controls.Add(button);
		flowLayoutPanel.Controls.Add(value);
		CancelButton = button;
		AcceptButton = button;
		Controls.Add(tableLayoutPanel);
		Controls.Add(flowLayoutPanel);
		tableLayoutPanel.BringToFront();
	}
}

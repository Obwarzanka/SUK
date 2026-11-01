using System;
using System.Drawing;
using System.Windows.Forms;

namespace EdrLocal;

public sealed class SepeErrorForm : ReferenceForm
{
	public readonly TextBox Details;

	public SepeErrorForm(string report)
	{
		Text = "Nie zapisano godzin w SEPE";
		ClientSize = new Size(720, 380);
		StartPosition = FormStartPosition.CenterParent;
		Font = ReferenceTheme.Dialog;
		Details = new TextBox
		{
			Multiline = true,
			ReadOnly = true,
			ScrollBars = ScrollBars.Vertical,
			Dock = DockStyle.Fill,
			Text = SepeDiagnostics.Clean(report),
			BackColor = Color.White
		};
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Bottom,
			Height = 42,
			FlowDirection = FlowDirection.RightToLeft,
			Padding = new Padding(5)
		};
		Action action = () =>
		{
			Close();
		};
		Button button = Ui.Button("Zamknij", action);
		flowLayoutPanel.Controls.Add(button);
		flowLayoutPanel.Controls.Add(Ui.Button("Kopiuj informacje", () =>
		{
			try
			{
				Clipboard.SetText(Details.Text);
			}
			catch
			{
				MessageBox.Show(this, "Schowek jest zajęty. Zaznacz tekst i skopiuj go klawiszami Ctrl+C.", "Kopiowanie");
			}
		}));
		Controls.Add(Details);
		Controls.Add(flowLayoutPanel);
		CancelButton = button;
	}
}

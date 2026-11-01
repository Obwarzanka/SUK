using System.Drawing;
using System.Windows.Forms;

namespace EdrLocal;

public sealed class ExitSessionForm : ReferenceForm
{
	public ExitSessionChoice Choice { get; private set; }

	public ExitSessionForm(bool canReset, bool pending)
	{
		Text = "Zakończenie pracy";
		ClientSize = new Size(620, 225);
		StartPosition = FormStartPosition.CenterParent;
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = false;
		MinimizeBox = false;
		Font = ReferenceTheme.Dialog;
		Label value = new Label
		{
			Text = "Zachować dane sesji EDR?",
			Font = new Font(Font, FontStyle.Bold),
			AutoSize = true,
			Location = new Point(20, 18)
		};
		Controls.Add(value);
		Controls.Add(new Label
		{
			Text = "Zapisz: zachowaj dzienniki i dyżur do dalszej pracy.\nNie zapisuj: pomiń niedokończoną edycję komórek.\nWcześniej zatwierdzone wpisy, dyżur i konfiguracja pozostają zapisane.\nGodziny wysłane do SEPE pozostają na serwerze." + (pending ? "\nWcześniej zatwierdzone godziny oczekujące pozostaną w bazie." : ""),
			Location = new Point(20, 52),
			Size = new Size(580, 119)
		});
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Bottom,
			Height = 44,
			FlowDirection = FlowDirection.RightToLeft,
			Padding = new Padding(8, 3, 8, 3)
		};
		Controls.Add(flowLayoutPanel);
		Button button = Ui.Button("Anuluj", () =>
		{
			Choose(ExitSessionChoice.Cancel);
		});
		Button value2 = Ui.Button("Nie zapisuj", () =>
		{
			Choose(ExitSessionChoice.Discard);
		});
		Button button2 = Ui.Button("Zapisz", () =>
		{
			Choose(ExitSessionChoice.Save);
		});
		flowLayoutPanel.Controls.Add(button);
		flowLayoutPanel.Controls.Add(value2);
		flowLayoutPanel.Controls.Add(button2);
		AcceptButton = button2;
		CancelButton = button;
	}

	private void Choose(ExitSessionChoice value)
	{
		Choice = value;
		DialogResult = ((value != ExitSessionChoice.Cancel) ? DialogResult.OK : DialogResult.Cancel);
		Close();
	}
}

using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EdrLocal;

public class LocationForm : ReferenceForm
{
	public bool ConfigureAfterOpening { get; private set; }

	public LocationForm(Engine engine)
	{
		LocationForm locationForm = this;
		Text = "EDR - Określanie lokalizacji aplikacji";
		FormBorderStyle = FormBorderStyle.FixedDialog;
		ClientSize = new Size(659, 268);
		StartPosition = FormStartPosition.CenterScreen;
		MinimizeBox = false;
		MaximizeBox = false;
		Font = new Font("Segoe UI", 10f);
		Panel panel = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = Color.White
		};
		Controls.Add(panel);
		panel.Controls.Add(new Label
		{
			Text = "Zalogowano do wersji szkoleniowej aplikacji EDR.",
			Font = new Font("Segoe UI", 10f, FontStyle.Bold),
			Bounds = new Rectangle(34, 15, 598, 29)
		});
		panel.Controls.Add(new Label
		{
			Text = "Zgodnie z przydzielonymi uprawnieniami możesz wybrać lokalizację spośród\ndostępnych poniżej:",
			Font = new Font("Segoe UI", 10f, FontStyle.Bold),
			Bounds = new Rectangle(34, 56, 600, 45)
		});
		RadioButton value = new RadioButton
		{
			Text = "Znajduję się na posterunku ruchu:",
			Checked = true,
			Bounds = new Rectangle(16, 113, 265, 27)
		};
		RadioButton outside = new RadioButton
		{
			Text = "Znajduję się poza posterunkiem ruchu.",
			Bounds = new Rectangle(16, 157, 400, 27),
			Enabled = engine.IsCoordinator
		};
		panel.Controls.Add(value);
		panel.Controls.Add(outside);
		ComboBox station = Ui.Combo(engine.State.Stations.Select((Station x) => MainForm.StationDisplay(x)), MainForm.StationDisplay(engine.Station), editable: false);
		station.Dock = DockStyle.None;
		station.Bounds = new Rectangle(280, 113, 350, 28);
		station.Font = new Font("Segoe UI", 10f);
		panel.Controls.Add(station);
		outside.CheckedChanged += (object s, EventArgs e) =>
		{
			station.Enabled = !outside.Checked;
		};
		SilverButton silverButton = new SilverButton
		{
			Text = "Anuluj",
			Bounds = new Rectangle(529, 207, 100, 34)
		};
		silverButton.Click += (object s, EventArgs e) =>
		{
			DialogResult = DialogResult.Cancel;
			Close();
		};
		SilverButton silverButton2 = new SilverButton
		{
			Text = "Wybierz",
			Bounds = new Rectangle(419, 207, 100, 34)
		};
		silverButton2.Click += (object s, EventArgs e) =>
		{
			engine.StationId = engine.State.Stations[station.SelectedIndex].Id;
			locationForm.ConfigureAfterOpening = outside.Checked;
			locationForm.DialogResult = DialogResult.OK;
			locationForm.Close();
		};
		panel.Controls.Add(silverButton);
		panel.Controls.Add(silverButton2);
		CancelButton = silverButton;
		AcceptButton = silverButton2;
	}
}

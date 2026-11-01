using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EdrLocal;

public class StartupForm : ReferenceForm
{
	public bool BeginDuty { get; private set; }

	public bool ObserveOnly { get; private set; }

	public StartupForm(Engine engine)
	{
		StartupForm startupForm = this;
		Text = "EDR - Uruchamianie aplikacji";
		FormBorderStyle = FormBorderStyle.FixedDialog;
		ClientSize = new Size(1000, 573);
		StartPosition = FormStartPosition.CenterScreen;
		MinimizeBox = false;
		MaximizeBox = false;
		Font = ReferenceTheme.Dialog;
		Panel panel = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = Color.White,
			Padding = new Padding(8, 0, 8, 0)
		};
		Controls.Add(panel);
		Panel panel2 = new Panel
		{
			Dock = DockStyle.Top,
			Height = 97,
			BackColor = Color.White
		};
		panel2.Controls.Add(new Label
		{
			Text = "Przyjmij lub obserwuj",
			Font = new Font("Segoe UI", 12f, FontStyle.Bold),
			Bounds = new Rectangle(2, 5, 900, 26)
		});
		panel2.Controls.Add(new Label
		{
			Text = "Na posterunku znajdują się następujące dzienniki ruchu, kliknij „Przyjmij dyżur” żeby rozpocząć dyżur lub „Obserwuj dyżur” żeby rozpocząć podgląd dyżuru.",
			Font = ReferenceTheme.Dialog,
			ForeColor = Color.FromArgb(0, 40, 90),
			Bounds = new Rectangle(12, 38, 955, 27)
		});
		panel2.Controls.Add(new Label
		{
			Text = "Dostępne dzienniki ruchu",
			Font = new Font("Segoe UI", 12f, FontStyle.Bold),
			Bounds = new Rectangle(2, 70, 940, 26)
		});
		DataGridView dataGridView = Ui.Grid(journal: false);
		ReferenceTheme.Grid(dataGridView, journal: false);
		dataGridView.Font = ReferenceTheme.Dialog;
		dataGridView.ColumnHeadersHeight = 50;
		dataGridView.RowTemplate.Height = 40;
		dataGridView.DefaultCellStyle.ForeColor = Color.FromArgb(0, 55, 90);
		dataGridView.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
		dataGridView.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
		string[] array = new string[6] { "Linia", "Szlak z/do", "Nazwa dziennika ruchu", "Prowadzi dyżur", "Przekazał dyżur", "Ostatnie przekazanie dyżuru" };
		foreach (string text in array)
		{
			Ui.Column(dataGridView, text, text, 161);
		}
		Duty duty = (from x in engine.State.Duties
			where x.StationId == engine.StationId && x.End.HasValue
			orderby x.End descending
			select x).FirstOrDefault();
		foreach (Section section in engine.Station.Sections)
		{
			dataGridView.Rows.Add("Linia " + section.Line, section.Name.ToUpper() + " (STACJA)", section.Name, (engine.ActiveDuty == null) ? "" : engine.ActiveDuty.UserName, (duty == null) ? "" : duty.UserName, (duty == null) ? "" : duty.End.Value.ToString("yyyy-MM-dd HH:mm:ss"));
		}
		dataGridView.ClearSelection();
		FlowLayoutPanel buttons = new FlowLayoutPanel
		{
			Dock = DockStyle.Bottom,
			Height = 43,
			FlowDirection = FlowDirection.RightToLeft,
			BackColor = Color.White,
			Padding = new Padding(0, 3, 0, 0)
		};
		Action<string, Action> action = (string text2, Action act) =>
		{
			SilverButton silverButton = new SilverButton
			{
				Text = text2,
				Size = new Size(100, 31),
				Margin = new Padding(3)
			};
			silverButton.Click += (object s, EventArgs e) =>
			{
				act();
			};
			buttons.Controls.Add(silverButton);
		};
		action("Anuluj", () =>
		{
			DialogResult = DialogResult.Cancel;
			Close();
		});
		action("Obserwuj dyżur", () =>
		{
			BeginDuty = false;
			ObserveOnly = true;
			DialogResult = DialogResult.OK;
			Close();
		});
		action(engine.CanWrite ? "Kontynuuj dyżur" : "Przyjmij dyżur", () =>
		{
			if (engine.User.Role == "Obserwator")
			{
				MessageBox.Show(startupForm, "Obserwator może wybrać podgląd dyżuru.", "EDR");
			}
			else
			{
				startupForm.BeginDuty = !engine.CanWrite;
				startupForm.DialogResult = DialogResult.OK;
				startupForm.Close();
			}
		});
		panel.Controls.Add(dataGridView);
		panel.Controls.Add(panel2);
		panel.Controls.Add(buttons);
	}
}

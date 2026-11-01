using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EdrLocal;

public class LoginForm : ReferenceForm
{
	public LocalUser SelectedUser { get; private set; }

	public LoginForm(Store store)
	{
		LoginForm loginForm = this;
		Text = "EDR 1.0.10.199 - Logowanie";
		FormBorderStyle = FormBorderStyle.FixedDialog;
		ClientSize = new Size(407, 202);
		MinimizeBox = false;
		MaximizeBox = false;
		StartPosition = FormStartPosition.CenterScreen;
		Font = ReferenceTheme.Dialog;
		Panel panel = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = Color.White
		};
		Label label = new Label
		{
			Dock = DockStyle.Bottom,
			Height = 23,
			BackColor = Color.FromArgb(239, 237, 237),
			Text = "Identyfikator aplikacji: lokalna",
			TextAlign = ContentAlignment.MiddleLeft,
			Font = ReferenceTheme.Dialog,
			ForeColor = Color.FromArgb(0, 25, 55)
		};
		Controls.Add(panel);
		Controls.Add(label);
		TextBox login = new TextBox
		{
			Bounds = new Rectangle(121, 25, 250, 28),
			Text = "DEMO",
			Font = new Font("Segoe UI", 11f)
		};
		TextBox password = new TextBox
		{
			Bounds = new Rectangle(121, 63, 250, 28),
			UseSystemPasswordChar = true,
			Font = new Font("Segoe UI", 11f)
		};
		panel.Controls.Add(login);
		panel.Controls.Add(password);
		panel.Controls.Add(new Label
		{
			Text = "Login:",
			Bounds = new Rectangle(28, 30, 90, 24),
			Font = new Font("Segoe UI", 10f),
			ForeColor = Color.FromArgb(0, 25, 55)
		});
		panel.Controls.Add(new Label
		{
			Text = "Hasło:",
			Bounds = new Rectangle(28, 68, 90, 24),
			Font = new Font("Segoe UI", 10f),
			ForeColor = Color.FromArgb(0, 25, 55)
		});
		SilverButton silverButton = new SilverButton
		{
			Text = "Anuluj",
			Bounds = new Rectangle(271, 117, 100, 34)
		};
		EventHandler value = (object s, EventArgs e) =>
		{
			DialogResult = DialogResult.Cancel;
			Close();
		};
		silverButton.Click += value;
		panel.Controls.Add(silverButton);
		CancelButton = silverButton;
		int failed = 0;
		DateTime until = DateTime.MinValue;
		SilverButton silverButton2 = new SilverButton
		{
			Text = "Zaloguj",
			Bounds = new Rectangle(161, 117, 100, 34)
		};
		silverButton2.Click += (object s, EventArgs e) =>
		{
			Ui.Run(loginForm, () =>
			{
				if (DateTime.Now < until)
				{
					throw new Exception("Ponów próbę za " + Math.Ceiling((until - DateTime.Now).TotalSeconds) + " s.");
				}
				LocalUser localUser = store.State.Users.FirstOrDefault((LocalUser x) => x.Id.Equals(login.Text.Trim(), StringComparison.OrdinalIgnoreCase));
				if (localUser == null || !localUser.Verify(password.Text))
				{
					failed++;
					if (failed >= 3)
					{
						until = DateTime.Now.AddMinutes(5.0);
						failed = 0;
					}
					throw new Exception("Nieprawidłowy identyfikator lub hasło lokalne.");
				}
				loginForm.SelectedUser = localUser;
				loginForm.DialogResult = DialogResult.OK;
				loginForm.Close();
			});
		};
		panel.Controls.Add(silverButton2);
		AcceptButton = silverButton2;
		ToolTip tip = new ToolTip();
		tip.SetToolTip(label, "Konto koordynatora DEMO / demo. Obserwator PODGLAD / demo.");
		Disposed += (object s, EventArgs e) =>
		{
			tip.Dispose();
		};
		Shown += (object s, EventArgs e) =>
		{
			password.Focus();
		};
	}
}

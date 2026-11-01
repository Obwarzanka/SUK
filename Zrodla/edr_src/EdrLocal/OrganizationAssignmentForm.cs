using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EdrLocal;

public sealed class OrganizationAssignmentForm : ReferenceForm
{
	private sealed class Choice
	{
		public string Id;

		public string Caption;

		public override string ToString()
		{
			return Caption;
		}
	}

	private readonly ConfiguratorRepository repository;

	private readonly ComboBox departments;

	private readonly ComboBox sections;

	private readonly Button next;

	private bool binding;

	public ConnectionOrganizationSelection Selection { get; private set; }

	private static string Id(ComboBox box)
	{
		if (box.SelectedItem is Choice choice)
		{
			return choice.Id;
		}
		return null;
	}

	private static void Select(ComboBox box, string id)
	{
		box.SelectedIndex = Math.Max(0, box.Items.Cast<Choice>().ToList().FindIndex((Choice x) => x.Id == id));
	}

	public OrganizationAssignmentForm(ConfiguratorRepository registry, string station)
	{
		repository = registry;
		Text = "Przypisanie posterunku do IZ / ISE";
		Font = ReferenceTheme.Dialog;
		ClientSize = new Size(640, 225);
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = false;
		MinimizeBox = false;
		ShowInTaskbar = false;
		StartPosition = FormStartPosition.CenterParent;
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			Padding = new Padding(18),
			ColumnCount = 3,
			RowCount = 3
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92f));
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
		Label control = new Label
		{
			Text = "Posterunek: " + station,
			Dock = DockStyle.Fill,
			Font = new Font(Font, FontStyle.Bold)
		};
		tableLayoutPanel.Controls.Add(control, 0, 0);
		tableLayoutPanel.SetColumnSpan(control, 3);
		departments = new ComboBox
		{
			Dock = DockStyle.Fill,
			DropDownStyle = ComboBoxStyle.DropDownList,
			Margin = new Padding(3, 10, 3, 8)
		};
		sections = new ComboBox
		{
			Dock = DockStyle.Fill,
			DropDownStyle = ComboBoxStyle.DropDownList,
			Margin = new Padding(3, 10, 3, 8)
		};
		tableLayoutPanel.Controls.Add(new Label
		{
			Text = "Zakład IZ",
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft
		}, 0, 1);
		tableLayoutPanel.Controls.Add(departments, 1, 1);
		tableLayoutPanel.Controls.Add(new Label
		{
			Text = "Sekcja ISE",
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft
		}, 0, 2);
		tableLayoutPanel.Controls.Add(sections, 1, 2);
		LinkLabel linkLabel = new LinkLabel
		{
			Text = "Dodaj IZ…",
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleCenter
		};
		LinkLabel linkLabel2 = new LinkLabel
		{
			Text = "Dodaj ISE…",
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleCenter
		};
		LinkLabelLinkClickedEventHandler value = (object s, LinkLabelLinkClickedEventArgs e) =>
		{
			Ui.Run(this, AddDepartment);
		};
		linkLabel.LinkClicked += value;
		linkLabel2.LinkClicked += (object s, LinkLabelLinkClickedEventArgs e) =>
		{
			Ui.Run(this, AddSection);
		};
		tableLayoutPanel.Controls.Add(linkLabel, 2, 1);
		tableLayoutPanel.Controls.Add(linkLabel2, 2, 2);
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Bottom,
			Height = 43,
			FlowDirection = FlowDirection.RightToLeft,
			Padding = new Padding(4)
		};
		Button button = Ui.Button("Anuluj", () =>
		{
			DialogResult = DialogResult.Cancel;
			Close();
		});
		next = Ui.Button("Dalej", Continue);
		flowLayoutPanel.Controls.Add(button);
		flowLayoutPanel.Controls.Add(next);
		CancelButton = button;
		AcceptButton = next;
		Controls.Add(tableLayoutPanel);
		Controls.Add(flowLayoutPanel);
		tableLayoutPanel.BringToFront();
		departments.SelectedIndexChanged += (object s, EventArgs e) =>
		{
			if (!binding)
			{
				ReloadSections(null);
			}
		};
		sections.SelectedIndexChanged += (object s, EventArgs e) =>
		{
			UpdateNext();
		};
		ContextMenuStrip izMenu = new ContextMenuStrip();
		izMenu.Items.Add(Ui.Menu("Dodaj zakład IZ…", AddDepartment));
		departments.ContextMenuStrip = izMenu;
		ContextMenuStrip iseMenu = new ContextMenuStrip();
		iseMenu.Items.Add(Ui.Menu("Dodaj sekcję ISE…", AddSection));
		sections.ContextMenuStrip = iseMenu;
		Disposed += (object s, EventArgs e) =>
		{
			izMenu.Dispose();
			iseMenu.Dispose();
		};
		ReloadChoices(repository.DefaultSectionId);
	}

	private void ReloadChoices(string selected)
	{
		ConfigurationUnit configurationUnit = repository.Data.Units.FirstOrDefault((ConfigurationUnit x) => x.Id == selected && x.Kind == "ISE");
		binding = true;
		try
		{
			departments.Items.Clear();
			departments.Items.Add(new Choice
			{
				Caption = "(wybierz zakład IZ)"
			});
			foreach (ConfigurationUnit item in from x in repository.Data.Units
				where x.Kind == "IZ"
				orderby x.Name
				select x)
			{
				departments.Items.Add(new Choice
				{
					Id = item.Id,
					Caption = ConfiguratorRepository.UnitCaption(item)
				});
			}
			Select(departments, configurationUnit?.ParentId);
		}
		finally
		{
			binding = false;
		}
		ReloadSections(configurationUnit?.Id);
	}

	private void ReloadSections(string selected)
	{
		binding = true;
		try
		{
			string parent = Id(departments);
			sections.Items.Clear();
			sections.Items.Add(new Choice
			{
				Caption = "(wybierz sekcję ISE)"
			});
			foreach (ConfigurationUnit item in from x in repository.Data.Units
				where x.Kind == "ISE" && x.ParentId == parent
				orderby x.Name
				select x)
			{
				sections.Items.Add(new Choice
				{
					Id = item.Id,
					Caption = ConfiguratorRepository.UnitCaption(item)
				});
			}
			Select(sections, selected);
		}
		finally
		{
			binding = false;
		}
		UpdateNext();
	}

	private void UpdateNext()
	{
		next.Enabled = repository.Data.Units.Any((ConfigurationUnit x) => x.Id == Id(sections) && x.Kind == "ISE" && x.ParentId == Id(departments));
	}

	private void AddDepartment()
	{
		string id = null;
		using (new ConfigurationSessionLock(repository))
		{
			repository.Load();
			using Editor editor = new Editor("Dodaj zakład IZ", 580, 210);
			TextBox name = editor.Add("Nazwa zakładu", Ui.Text("", multiline: false), 35);
			editor.Save(() =>
			{
				id = repository.AddDepartment(Ui.Required(name, "Nazwa zakładu"));
			});
			if (editor.ShowDialog(this) != DialogResult.OK)
			{
				return;
			}
		}
		ReloadChoices(null);
		Select(departments, id);
	}

	private void AddSection()
	{
		string parent = Id(departments);
		if (parent == null)
		{
			throw new InvalidOperationException("Najpierw wybierz zakład IZ.");
		}
		string id = null;
		using (new ConfigurationSessionLock(repository))
		{
			repository.Load();
			using Editor editor = new Editor("Dodaj sekcję ISE", 580, 210);
			TextBox name = editor.Add("Nazwa sekcji", Ui.Text("", multiline: false), 35);
			editor.Save(() =>
			{
				id = repository.AddSection(parent, Ui.Required(name, "Nazwa sekcji"));
			});
			if (editor.ShowDialog(this) != DialogResult.OK)
			{
				return;
			}
		}
		ReloadChoices(id);
	}

	private void Continue()
	{
		string parent = Id(departments);
		string section = Id(sections);
		repository.Load();
		if (!repository.Data.Units.Any((ConfigurationUnit x) => x.Id == section && x.Kind == "ISE" && x.ParentId == parent))
		{
			throw new InvalidOperationException("Wybierz zakład IZ i podległą sekcję ISE. Jeśli strukturę zmieniono, zamknij okno i wybierz ponownie.");
		}
		Selection = new ConnectionOrganizationSelection(parent, section, existing: false);
		DialogResult = DialogResult.OK;
		Close();
	}
}

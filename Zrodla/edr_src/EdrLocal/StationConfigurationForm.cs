using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EdrLocal;

public sealed class StationConfigurationForm : ReferenceForm
{
	private class Choice
	{
		public string Id;

		public string Name;

		public override string ToString()
		{
			return Name;
		}
	}

	private readonly Engine engine;

	private readonly Station copy;

	private readonly BindingList<Section> sections;

	private readonly BindingList<StationTrackSetting> tracks;

	private readonly BindingList<RequiredCheckSetting> checks;

	private readonly ConfigurationGrid sectionGrid;

	private readonly ConfigurationGrid lineGrid;

	private readonly ConfigurationGrid crossingGrid;

	private readonly ConfigurationGrid trackGrid;

	private readonly ConfigurationGrid tabsGrid;

	private ConfigurationGrid checkGrid;

	private readonly ComboBox left;

	private readonly ComboBox right;

	private int editingTabIndex = -1;

	private bool tabChoicesDirty;

	private TextBox name;

	private readonly Label lineTitle;

	private readonly Label crossingTitle;

	private bool binding;

	private readonly Action<Station> externalSave;

	private string initialSignature;

	private readonly List<CheckSchedulePanel> schedulePanels = new List<CheckSchedulePanel>();

	private RadioButton defaultRelease;

	private RadioButton customRelease;

	private NumericUpDown releaseSeconds;

	private NumericUpDown trackRefreshMinutes;

	private CheckBox refreshTrackControl;

	private CheckBox writeSepe;

	private RadioButton[] visibility;

	private ComboBox postType;

	private RadioButton telephoneMonthly;

	private RadioButton telephoneAnnual;

	private NumericUpDown telephoneNext;

	private CheckedListBox td2AreaList;

	public Station SavedStation { get; private set; }

	public bool EmbeddedEditor => externalSave != null;

	public bool HasPendingChanges
	{
		get
		{
			try
			{
				return tabChoicesDirty || DraftSignature() != initialSignature;
			}
			catch
			{
				return true;
			}
		}
	}

	public event Action DraftSaved;

	public event Action DraftDiscarded;

	public StationConfigurationForm(Engine value)
		: this(value, null)
	{
	}

	public StationConfigurationForm(Engine value, Action<Station> onSave)
	{
		externalSave = onSave;
		engine = value;
		copy = Json.Clone(engine.Station);
		StationSettings.Normalize(copy);
		sections = new BindingList<Section>(copy.Sections);
		tracks = new BindingList<StationTrackSetting>(copy.TrackSettings);
		checks = new BindingList<RequiredCheckSetting>(copy.RequiredChecks);
		if (copy.JournalTabs.Count == 0)
		{
			for (int i = 0; i < sections.Count; i += 2)
			{
				List<Section> list = sections.Skip(i).Take(2).Where(StationSettings.Conducted)
					.ToList();
				if (list.Count > 0)
				{
					copy.JournalTabs.Add(new JournalTabDefinition
					{
						LeftId = list[0].Id,
						RightId = ((list.Count > 1) ? list[1].Id : null)
					});
				}
			}
		}
		Text = "Konfiguracja posterunku - " + copy.Name;
		Font = new Font("Tahoma", 8.25f);
		ClientSize = new Size(1240, 780);
		MinimumSize = new Size(1020, 650);
		StartPosition = FormStartPosition.CenterParent;
		MinimizeBox = false;
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Bottom,
			Height = 35,
			FlowDirection = FlowDirection.RightToLeft
		};
		flowLayoutPanel.Controls.Add(Ui.Button("Anuluj", () =>
		{
			if (EmbeddedEditor)
			{
				if (DraftDiscarded != null)
				{
					DraftDiscarded();
				}
			}
			else
			{
				Close();
			}
		}));
		flowLayoutPanel.Controls.Add(Ui.Button("Zapisz", Save));
		Controls.Add(flowLayoutPanel);
		TabControl tabControl = new TabControl
		{
			Dock = DockStyle.Fill,
			Font = Font
		};
		Controls.Add(tabControl);
		tabControl.BringToFront();
		TabPage tabPage = new TabPage(engine.IsTd2 ? "Szlaki TD2 - Dzienniki ruchu" : "Szlaki przyległe - Dzienniki ruchu");
		tabControl.TabPages.Add(tabPage);
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 1,
			RowCount = 3,
			Padding = Padding.Empty,
			Margin = Padding.Empty
		};
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 39f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 23f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 38f));
		tabPage.Controls.Add(tableLayoutPanel);
		sectionGrid = new ConfigurationGrid();
		if (engine.IsTd2)
		{
			Column(sectionGrid, "Td2LocalLine", "Szlak w TD2", 130);
			Column(sectionGrid, "Td2Kind", "Rodzaj szlaku", 115);
			Column(sectionGrid, "Td2PeerName", "Posterunek wewnętrzny", 175);
			Column(sectionGrid, "Line", "Nr linii", 70);
			Column(sectionGrid, "Tracks", "Liczba torów", 90);
			Column(sectionGrid, "OddDirection", "Kierunek nieparzysty", 135);
			Column(sectionGrid, "EvenDirection", "Kierunek parzysty", 135);
			Bool(sectionGrid, "Conducted", "Czy prowadzony", 70);
			Column(sectionGrid, "JournalName", "Nazwa dziennika", 200);
			Groups(sectionGrid, new string[2] { "Szlak TD2 / linia kolejowa", "Dziennik ruchu" }, new int[2] { 0, 7 }, new int[2] { 6, 8 });
		}
		else
		{
			Column(sectionGrid, "Line", "Nr", 45);
			Column(sectionGrid, "LineName", "Nazwa", 230);
			Column(sectionGrid, "Name", "Z / Do", 210);
			Column(sectionGrid, "Tracks", "Rodzaj linii\n(ilość torów)", 100);
			Column(sectionGrid, "OddDirection", "Kierunek nieparzysty", 170);
			Column(sectionGrid, "EvenDirection", "Kierunek parzysty", 170);
			Bool(sectionGrid, "Conducted", "Czy prowadzony", 70);
			Column(sectionGrid, "JournalName", "Nazwa", 140);
			Groups(sectionGrid, new string[3] { "Linia kolejowa", "Szlak / Odstęp", "Dziennik ruchu" }, new int[3] { 0, 2, 6 }, new int[3] { 1, 5, 7 });
		}
		sectionGrid.DataSource = sections;
		sectionGrid.ReadOnly = false;
		foreach (DataGridViewColumn column in sectionGrid.Columns)
		{
			column.ReadOnly = column.Name != "Conducted";
		}
		sectionGrid.CellFormatting += (object s, DataGridViewCellFormattingEventArgs e) =>
		{
			if (e.RowIndex >= 0 && sectionGrid.Rows[e.RowIndex].DataBoundItem is Section section)
			{
				string text = sectionGrid.Columns[e.ColumnIndex].Name;
				if (engine.IsTd2)
				{
					if (text == "JournalName")
					{
						e.Value = StationSettings.Title(section);
					}
					if (text == "Td2Kind")
					{
						e.Value = (section.Td2AreaHidden ? "W obszarze posterunku" : (section.Td2Archived ? "Archiwalny" : (string.IsNullOrEmpty(section.Td2Kind) ? "Nieustalony" : section.Td2Kind)));
					}
					if (text == "Td2PeerName" && section.Td2Kind == "Zewnętrzny")
					{
						e.Value = "Według rozkładu pociągu";
					}
				}
				if (text == "Tracks")
				{
					e.Value = ((section.Tracks == 2) ? "Dwutorowa" : "Jednotorowa");
				}
				if (text == "OddDirection")
				{
					e.Value = Direction(!section.ArrivalEven);
				}
				if (text == "EvenDirection")
				{
					e.Value = Direction(section.ArrivalEven);
				}
			}
		};
		Block(tableLayoutPanel, 0, engine.IsTd2 ? ("Sceneria: " + (copy.Td2SceneName ?? "nieustalona") + "    Stacja / posterunek: " + copy.Name + "    Nazwy i linie ustawiane dla każdego dziennika") : "Szlaki przyległe - Dzienniki ruchu", sectionGrid, new Button[3]
		{
			Ui.Button("Konfiguruj", () =>
			{
				EditSection(create: false);
			}),
			Ui.Button("Utwórz nowy", () =>
			{
				EditSection(create: true);
			}),
			Ui.Button("Usuń", () =>
			{
				Section sec = Selected();
				if (sec != null)
				{
					if (engine.State.Entries.Any((JournalEntry x) => x.StationId == copy.Id && x.SectionId == sec.Id))
					{
						throw new Exception("Dziennik zawiera wpisy i nie może zostać usunięty.");
					}
					sections.Remove(sec);
					copy.JournalTabs.RemoveAll((JournalTabDefinition x) => x.LeftId == sec.Id || x.RightId == sec.Id);
					RefreshTabChoices();
					BindDetails();
				}
			})
		});
		lineGrid = new ConfigurationGrid();
		Bool(lineGrid, "Right", "Prawy", 50);
		Bool(lineGrid, "Left", "Lewy", 50);
		Column(lineGrid, "Number", "Numer", 60);
		Column(lineGrid, "TrafficType", "Zasadniczy rodzaj ruchu", 150);
		Column(lineGrid, "NormalDirection", "Zasadniczy kierunek ruchu", 170);
		Column(lineGrid, "OddBasis", "Kierunek nieparzysty", 320);
		Column(lineGrid, "EvenBasis", "Kierunek parzysty", 320);
		Groups(lineGrid, new string[2] { "Tor szlakowy", "Podstawa prowadzenia ruchu pociągów w warunkach normalnych" }, new int[2] { 0, 5 }, new int[2] { 4, 6 });
		lineGrid.CellFormatting += (object s, DataGridViewCellFormattingEventArgs e) =>
		{
			if (e.RowIndex >= 0)
			{
				LineTrackSetting lineTrackSetting = lineGrid.Rows[e.RowIndex].DataBoundItem as LineTrackSetting;
				Section section = Selected();
				if (lineTrackSetting != null && section != null)
				{
					string text = lineGrid.Columns[e.ColumnIndex].Name;
					int num = section.RightTrackNumber ?? ((section.Tracks != 2) ? 1 : 2);
					if (text == "Right")
					{
						e.Value = lineTrackSetting.Number == num;
					}
					if (text == "Left")
					{
						e.Value = lineTrackSetting.Number != num;
					}
					if (text == "TrafficType")
					{
						e.Value = lineTrackSetting.TrafficType ?? "Jednokierunkowy";
					}
					if (text == "NormalDirection")
					{
						e.Value = Direction(lineTrackSetting.NormalIncoming);
					}
					if (text == "OddBasis")
					{
						e.Value = (section.ArrivalEven ? (lineTrackSetting.BasisOut ?? lineTrackSetting.Basis) : lineTrackSetting.Basis);
					}
					if (text == "EvenBasis")
					{
						e.Value = (section.ArrivalEven ? lineTrackSetting.Basis : (lineTrackSetting.BasisOut ?? lineTrackSetting.Basis));
					}
				}
			}
		};
		lineTitle = Block(tableLayoutPanel, 1, "Tory szlakowe", lineGrid, new Button[1] { Ui.Button("Konfiguruj", () =>
		{
			EditSection(create: false);
		}) });
		crossingGrid = new ConfigurationGrid();
		Column(crossingGrid, "Kilometre", "Km na linii", 90);
		Column(crossingGrid, "Number", "Numer na linii", 85);
		Column(crossingGrid, "Name", "Nazwa", 210);
		Column(crossingGrid, "Mark", "Oznaczenie w Dzienniku Ruchu", 155);
		Column(crossingGrid, "AlongTrack", "Według szlaku", 150);
		Column(crossingGrid, "Trains", "Dla pociągów", 120);
		Bool(crossingGrid, "RecordClosure", "Rejestrowanie godziny zamknięcia rogatek", 155);
		Bool(crossingGrid, "InJournal", "W Dzienniku Ruchu", 120);
		Groups(crossingGrid, new string[2] { "Posterunek dróżnika przejazdowego", "Powiadamianie o odjeździe pociągów" }, new int[2] { 0, 4 }, new int[2] { 3, 7 });
		crossingTitle = Block(tableLayoutPanel, 2, "Posterunki dróżników przejazdowych", crossingGrid, new Button[3]
		{
			Ui.Button("Konfiguruj", () =>
			{
				EditCrossing(create: false);
			}),
			Ui.Button("Utwórz nowy", () =>
			{
				EditCrossing(create: true);
			}),
			Ui.Button("Usuń", () =>
			{
				Section section = Selected();
				if (section != null && crossingGrid.CurrentRow != null)
				{
					section.Crossings.Remove((CrossingSetting)crossingGrid.CurrentRow.DataBoundItem);
					BindDetails();
				}
			})
		});
		sectionGrid.CurrentCellChanged += (object s, EventArgs e) =>
		{
			if (!binding)
			{
				BindDetails();
			}
		};
		sectionGrid.CellDoubleClick += (object s, DataGridViewCellEventArgs e) =>
		{
			if (e.RowIndex >= 0 && e.ColumnIndex != sectionGrid.Columns["Conducted"].Index)
			{
				EditSection(create: false);
			}
		};
		lineGrid.CellDoubleClick += (object s, DataGridViewCellEventArgs e) =>
		{
			if (e.RowIndex >= 0)
			{
				EditSection(create: false);
			}
		};
		crossingGrid.CellDoubleClick += (object s, DataGridViewCellEventArgs e) =>
		{
			if (e.RowIndex >= 0)
			{
				EditCrossing(create: false);
			}
		};
		TabPage tabPage2 = new TabPage("Układ dzienników");
		tabControl.TabPages.Add(tabPage2);
		TableLayoutPanel tableLayoutPanel2 = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 2,
			RowCount = 1
		};
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57f));
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43f));
		tabPage2.Controls.Add(tableLayoutPanel2);
		Panel panel = new Panel
		{
			Dock = DockStyle.Fill
		};
		tableLayoutPanel2.Controls.Add(panel, 0, 0);
		tabsGrid = new ConfigurationGrid
		{
			ColumnHeadersHeight = 28
		};
		Column(tabsGrid, "Index", "Nr", 45);
		Column(tabsGrid, "Left", "Dziennik ruchu (lewa strona)", 240);
		Column(tabsGrid, "Right", "Dziennik ruchu (prawa strona)", 240);
		panel.Controls.Add(tabsGrid);
		FlowLayoutPanel flowLayoutPanel2 = new FlowLayoutPanel
		{
			Dock = DockStyle.Top,
			Height = 38
		};
		flowLayoutPanel2.Controls.Add(Ui.Button("Zamień strony", SwapTab));
		flowLayoutPanel2.Controls.Add(Ui.Button("W górę", () =>
		{
			MoveTab(-1);
		}));
		flowLayoutPanel2.Controls.Add(Ui.Button("W dół", () =>
		{
			MoveTab(1);
		}));
		flowLayoutPanel2.Controls.Add(Ui.Button("Odłącz", DetachTab));
		panel.Controls.Add(flowLayoutPanel2);
		tabsGrid.BringToFront();
		FlowLayoutPanel flowLayoutPanel3 = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			Padding = new Padding(16)
		};
		tableLayoutPanel2.Controls.Add(flowLayoutPanel3, 1, 0);
		flowLayoutPanel3.Controls.Add(new Label
		{
			Text = "Dodaj nową zakładkę / zmień zaznaczoną",
			AutoSize = true,
			Font = new Font(Font, FontStyle.Bold)
		});
		left = new ComboBox
		{
			Width = 440,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		right = new ComboBox
		{
			Width = 440,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		flowLayoutPanel3.Controls.Add(new Label
		{
			Text = "Dziennik ruchu (lewa strona):",
			AutoSize = true
		});
		flowLayoutPanel3.Controls.Add(left);
		flowLayoutPanel3.Controls.Add(new Label
		{
			Text = "Dziennik ruchu (prawa strona):",
			AutoSize = true
		});
		flowLayoutPanel3.Controls.Add(right);
		flowLayoutPanel3.Controls.Add(Ui.Button("Dodaj zakładkę", () =>
		{
			SetTab(replace: false);
		}));
		flowLayoutPanel3.Controls.Add(Ui.Button("Zastosuj do zaznaczonej", () =>
		{
			SetTab(replace: true);
		}));
		left.SelectedIndexChanged += (object s, EventArgs e) =>
		{
			MarkTabChoices();
		};
		right.SelectedIndexChanged += (object s, EventArgs e) =>
		{
			MarkTabChoices();
		};
		tabsGrid.CurrentCellChanged += (object s, EventArgs e) =>
		{
			SelectTabChoices();
		};
		TabPage tabPage3 = new TabPage("Tory stacyjne");
		tabControl.TabPages.Add(tabPage3);
		trackGrid = new ConfigurationGrid
		{
			ReadOnly = false,
			ColumnHeadersHeight = 28
		};
		Column(trackGrid, "Number", "Numer toru", 90);
		Column(trackGrid, "LineNumber", "Nr", 60);
		Column(trackGrid, "LineName", "Nazwa", 450);
		Column(trackGrid, "Kind", "Rodzaj", 200);
		Bool(trackGrid, "Visible", "Czy wyświetlać", 120);
		Groups(trackGrid, new string[4] { "Tor stacyjny", "Przypisany do linii kolejowej", "Rodzaj toru", "Widoczność" }, new int[4] { 0, 1, 3, 4 }, new int[4] { 0, 2, 3, 4 });
		trackGrid.ColumnHeadersHeight = 54;
		trackGrid.DataSource = tracks;
		tabPage3.Controls.Add(trackGrid);
		TopTools(tabPage3, new Button[2]
		{
			Ui.Button("Dodaj tor", () =>
			{
				tracks.Add(new StationTrackSetting
				{
					Number = "",
					Visible = true
				});
			}),
			Ui.Button("Usuń tor", () =>
			{
				if (trackGrid.CurrentRow != null)
				{
					tracks.Remove((StationTrackSetting)trackGrid.CurrentRow.DataBoundItem);
				}
			})
		});
		TabPage tabPage4 = new TabPage("Wymagane sprawdzenia");
		tabControl.TabPages.Add(tabPage4);
		BuildRequired(tabPage4);
		TabPage tabPage5 = new TabPage("Inne");
		tabControl.TabPages.Add(tabPage5);
		BuildOther(tabPage5);
		if (engine.IsTd2)
		{
			BuildTd2Area(tabControl);
		}
		RefreshTabChoices();
		BindDetails();
		initialSignature = DraftSignature();
	}

	private static string Direction(bool incoming)
	{
		if (!incoming)
		{
			return "DO posterunku sąsiedniego";
		}
		return "Z posterunku sąsiedniego";
	}

	private Section Selected()
	{
		if (sectionGrid.CurrentRow != null)
		{
			return sectionGrid.CurrentRow.DataBoundItem as Section;
		}
		return null;
	}

	private void BindDetails()
	{
		if (binding || lineGrid == null)
		{
			return;
		}
		binding = true;
		try
		{
			Section section = Selected();
			lineGrid.DataSource = ((section == null) ? null : new BindingList<LineTrackSetting>(section.LineTracks));
			crossingGrid.DataSource = ((section == null) ? null : new BindingList<CrossingSetting>(section.Crossings));
			if (lineTitle != null)
			{
				Label label = lineTitle;
				string text;
				if (section == null)
				{
					text = "";
				}
				else
				{
					text = (engine.IsTd2 ? ("    Szlak: " + section.Td2LocalLine + "    " + section.Td2Kind + (string.IsNullOrWhiteSpace(section.Line) ? "" : ("    Linia: " + section.Line))) : ("    Szlak z/do: " + section.Name + "    Linia kolejowa: " + section.Line + "  " + section.LineName));
				}
				label.Text = "Tory szlakowe" + text;
			}
			if (crossingTitle != null)
			{
				crossingTitle.Text = "Posterunki dróżników przejazdowych" + ((section == null) ? "" : ("    Szlak: " + (engine.IsTd2 ? section.Td2LocalLine : section.Name)));
			}
		}
		finally
		{
			binding = false;
		}
	}

	private void CommitGrids()
	{
		ConfigurationGrid[] array = new ConfigurationGrid[3] { sectionGrid, trackGrid, checkGrid };
		foreach (ConfigurationGrid configurationGrid in array)
		{
			configurationGrid.EndEdit();
			if (configurationGrid.DataSource != null && BindingContext[configurationGrid.DataSource] != null)
			{
				BindingContext[configurationGrid.DataSource].EndCurrentEdit();
			}
		}
	}

	private void RefreshTabChoices()
	{
		if (left == null)
		{
			return;
		}
		copy.Sections = sections.ToList();
		SectionRouting.LabelDuplicates(copy);
		CommitTabChoices();
		binding = true;
		ComboBox[] array = new ComboBox[2] { left, right };
		foreach (ComboBox comboBox in array)
		{
			string id = ((!(comboBox.SelectedItem is Choice)) ? null : ((Choice)comboBox.SelectedItem).Id);
			comboBox.Items.Clear();
			if (comboBox == right)
			{
				comboBox.Items.Add(new Choice
				{
					Id = null,
					Name = "(brak drugiego dziennika)"
				});
			}
			foreach (Section item in sections.Where(StationSettings.Conducted))
			{
				comboBox.Items.Add(new Choice
				{
					Id = item.Id,
					Name = StationSettings.Title(item)
				});
			}
			comboBox.SelectedIndex = Math.Max(0, comboBox.Items.Cast<Choice>().ToList().FindIndex((Choice x) => x.Id == id));
		}
		binding = false;
		RefreshTabList(-1);
	}

	private int SelectedTabIndex()
	{
		if (tabsGrid.CurrentRow != null)
		{
			return tabsGrid.CurrentRow.Index;
		}
		return -1;
	}

	private void RefreshTabList(int selected)
	{
		if (tabsGrid != null)
		{
			binding = true;
			tabsGrid.Rows.Clear();
			for (int i = 0; i < copy.JournalTabs.Count; i++)
			{
				JournalTabDefinition journalTabDefinition = copy.JournalTabs[i];
				tabsGrid.Rows.Add(i + 1, Title(journalTabDefinition.LeftId), Title(journalTabDefinition.RightId));
			}
			if (tabsGrid.Rows.Count > 0)
			{
				tabsGrid.CurrentCell = tabsGrid.Rows[Math.Max(0, Math.Min(selected, tabsGrid.Rows.Count - 1))].Cells[1];
			}
			binding = false;
			SelectTabChoices();
		}
	}

	private string Title(string id)
	{
		Section section = sections.FirstOrDefault((Section x) => x.Id == id);
		if (section != null)
		{
			return StationSettings.Title(section);
		}
		return "";
	}

	private void MarkTabChoices()
	{
		if (!binding && editingTabIndex >= 0 && editingTabIndex < copy.JournalTabs.Count)
		{
			Choice choice = left.SelectedItem as Choice;
			Choice choice2 = right.SelectedItem as Choice;
			JournalTabDefinition journalTabDefinition = copy.JournalTabs[editingTabIndex];
			tabChoicesDirty = choice == null || journalTabDefinition.LeftId != choice.Id || journalTabDefinition.RightId != choice2?.Id;
		}
	}

	private void CommitTabChoices()
	{
		if (tabChoicesDirty && editingTabIndex >= 0)
		{
			StoreTabChoices(editingTabIndex, replace: true);
			tabChoicesDirty = false;
			if (editingTabIndex < tabsGrid.Rows.Count)
			{
				JournalTabDefinition journalTabDefinition = copy.JournalTabs[editingTabIndex];
				tabsGrid.Rows[editingTabIndex].Cells["Left"].Value = Title(journalTabDefinition.LeftId);
				tabsGrid.Rows[editingTabIndex].Cells["Right"].Value = Title(journalTabDefinition.RightId);
			}
		}
	}

	private void SelectTabChoices()
	{
		if (binding || left == null)
		{
			return;
		}
		int num = SelectedTabIndex();
		if (tabChoicesDirty && num != editingTabIndex)
		{
			try
			{
				CommitTabChoices();
			}
			catch (ArgumentException ex)
			{
				binding = true;
				try
				{
					tabsGrid.CurrentCell = tabsGrid.Rows[editingTabIndex].Cells[1];
				}
				finally
				{
					binding = false;
				}
				MessageBox.Show(this, ex.Message, "Układ dzienników", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
		}
		if (num < 0 || num >= copy.JournalTabs.Count)
		{
			editingTabIndex = -1;
			return;
		}
		JournalTabDefinition tab = copy.JournalTabs[num];
		binding = true;
		try
		{
			left.SelectedIndex = left.Items.Cast<Choice>().ToList().FindIndex((Choice x) => x.Id == tab.LeftId);
			right.SelectedIndex = right.Items.Cast<Choice>().ToList().FindIndex((Choice x) => x.Id == tab.RightId);
			editingTabIndex = num;
			tabChoicesDirty = false;
		}
		finally
		{
			binding = false;
		}
	}

	private void StoreTabChoices(int at, bool replace)
	{
		Choice choice = left.SelectedItem as Choice;
		Choice choice2 = right.SelectedItem as Choice;
		if (choice == null)
		{
			throw new ArgumentException("Wybierz dziennik po lewej stronie.");
		}
		string text = choice2?.Id;
		if (choice.Id == text)
		{
			throw new ArgumentException("Wybierz dwa różne dzienniki.");
		}
		for (int i = 0; i < copy.JournalTabs.Count; i++)
		{
			if (!replace || i != at)
			{
				JournalTabDefinition old = copy.JournalTabs[i];
				if (new string[2] { choice.Id, text }.Where((string x) => x != null).Any((string x) => old.LeftId == x || old.RightId == x))
				{
					throw new ArgumentException("Dziennik jest już na liście. Zaznacz jego zakładkę i użyj Zastosuj do zaznaczonej albo Odłącz.");
				}
			}
		}
		JournalTabDefinition journalTabDefinition = new JournalTabDefinition();
		journalTabDefinition.LeftId = choice.Id;
		journalTabDefinition.RightId = text;
		JournalTabDefinition journalTabDefinition2 = journalTabDefinition;
		if (replace)
		{
			copy.JournalTabs[at] = journalTabDefinition2;
		}
		else
		{
			copy.JournalTabs.Add(journalTabDefinition2);
		}
	}

	private void SetTab(bool replace)
	{
		int num = (replace ? SelectedTabIndex() : copy.JournalTabs.Count);
		if (num >= 0)
		{
			StoreTabChoices(num, replace);
			tabChoicesDirty = false;
			editingTabIndex = num;
			RefreshTabList(num);
		}
	}

	private void SwapTab()
	{
		CommitTabChoices();
		int num = SelectedTabIndex();
		if (num >= 0)
		{
			JournalTabDefinition journalTabDefinition = copy.JournalTabs[num];
			if (!string.IsNullOrEmpty(journalTabDefinition.RightId))
			{
				string leftId = journalTabDefinition.LeftId;
				journalTabDefinition.LeftId = journalTabDefinition.RightId;
				journalTabDefinition.RightId = leftId;
				RefreshTabList(num);
			}
		}
	}

	private void MoveTab(int delta)
	{
		CommitTabChoices();
		int num = SelectedTabIndex();
		int num2 = num + delta;
		if (num >= 0 && num2 >= 0 && num2 < copy.JournalTabs.Count)
		{
			JournalTabDefinition item = copy.JournalTabs[num];
			copy.JournalTabs.RemoveAt(num);
			copy.JournalTabs.Insert(num2, item);
			RefreshTabList(num2);
		}
	}

	private void DetachTab()
	{
		CommitTabChoices();
		int num = SelectedTabIndex();
		if (num >= 0)
		{
			JournalTabDefinition journalTabDefinition = copy.JournalTabs[num];
			copy.JournalTabs.RemoveAt(num);
			if (!string.IsNullOrEmpty(journalTabDefinition.RightId))
			{
				copy.JournalTabs.Insert(num, new JournalTabDefinition
				{
					LeftId = journalTabDefinition.LeftId
				});
				copy.JournalTabs.Insert(num + 1, new JournalTabDefinition
				{
					LeftId = journalTabDefinition.RightId
				});
			}
			RefreshTabList(num);
		}
	}

	private string DraftSignature()
	{
		ReadTd2Area();
		CommitGrids();
		ReadConfigurationOptions();
		copy.Name = name.Text.Trim();
		copy.Sections = sections.ToList();
		SectionRouting.LabelDuplicates(copy);
		copy.TrackSettings = tracks.ToList();
		copy.RequiredChecks = checks.ToList();
		return Json.Text(copy);
	}

	public bool ConfirmPending(IWin32Window owner)
	{
		if (!HasPendingChanges)
		{
			return true;
		}
		switch (MessageBox.Show(owner, "Zapisać zmiany konfiguracji posterunku " + copy.Name + "?", "Niezapisane zmiany", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question))
		{
		case DialogResult.Cancel:
			return false;
		case DialogResult.No:
			return true;
		default:
			try
			{
				Save();
				return true;
			}
			catch (Exception ex)
			{
				MessageBox.Show(owner, ex.Message, "Konfiguracja", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return false;
			}
		}
	}

	public void SaveDraft()
	{
		Save();
	}

	private void Save()
	{
		CommitTabChoices();
		ApplyTd2Area();
		CommitGrids();
		ReadConfigurationOptions();
		copy.Name = name.Text.Trim();
		copy.Sections = sections.ToList();
		SectionRouting.LabelDuplicates(copy);
		copy.TrackSettings = tracks.ToList();
		copy.RequiredChecks = checks.ToList();
		foreach (Section section2 in copy.Sections)
		{
			section2.ManualConfiguration = true;
			section2.NotifyCrossings = section2.Crossings.Any((CrossingSetting x) => x.InJournal != false);
			section2.NormalIncomingTrack = (from x in section2.LineTracks
				where x.NormalIncoming
				select x.Number).FirstOrDefault();
		}
		foreach (JournalTabDefinition tab in copy.JournalTabs)
		{
			List<Section> source = copy.Sections;
			Func<Section, bool> predicate = (Section x) => x.Id == tab.LeftId && StationSettings.Conducted(x);
			if (!source.Any(predicate))
			{
				tab.LeftId = tab.RightId;
				tab.RightId = null;
			}
			if (!copy.Sections.Any((Section x) => x.Id == tab.RightId && StationSettings.Conducted(x)))
			{
				tab.RightId = null;
			}
		}
		copy.JournalTabs.RemoveAll((JournalTabDefinition x) => string.IsNullOrEmpty(x.LeftId) || !copy.Sections.Any((Section s) => s.Id == x.LeftId && StationSettings.Conducted(s)));
		StationSettings.Validate(copy, engine.State);
		if (engine.SimRail != null)
		{
			copy.SimulatorReviewed = true;
			copy.SimulatorTopologyVersion = 2;
			copy.RouteNeighboursVersion = 2;
		}
		if (externalSave != null)
		{
			externalSave(copy);
		}
		engine.Change("Konfiguracja posterunku", copy.Id, (AppState state) =>
		{
			Station station = state.Stations.First((Station x) => x.Id == copy.Id);
			foreach (Section sec in copy.Sections)
			{
				List<Section> source2 = station.Sections;
				Func<Section, bool> predicate2 = (Section x) => x.Id == sec.Id;
				Section section = source2.FirstOrDefault(predicate2);
				if (section != null)
				{
					foreach (Train item in state.Trains.Where((Train x) => x.StationId == copy.Id))
					{
						if (!engine.IsTd2 && item.From == section.Name && item.LineIn == section.Line)
						{
							item.From = sec.Name;
							item.LineIn = sec.Line;
						}
						if (!engine.IsTd2 && item.To == section.Name && item.LineOut == section.Line)
						{
							item.To = sec.Name;
							item.LineOut = sec.Line;
						}
					}
				}
			}
			if (StationSettings.NoStationTracks(copy))
			{
				foreach (Train item2 in state.Trains.Where((Train x) => x.StationId == copy.Id))
				{
					item2.Track = "";
					item2.Platform = "";
				}
			}
			state.Stations[state.Stations.FindIndex((Station x) => x.Id == copy.Id)] = Json.Clone(copy);
		});
		if (engine.SimRail != null)
		{
			engine.PersistStationProfile();
			engine.ApplySimRailSnapshot(engine.SimRail.Snapshot);
		}
		SavedStation = Json.Clone(copy);
		initialSignature = DraftSignature();
		if (EmbeddedEditor)
		{
			if (DraftSaved != null)
			{
				DraftSaved();
			}
		}
		else
		{
			DialogResult = DialogResult.OK;
			Close();
		}
	}

	private static void Column(DataGridView g, string prop, string text, int width)
	{
		g.Columns.Add(new DataGridViewTextBoxColumn
		{
			Name = prop,
			DataPropertyName = prop,
			HeaderText = text,
			Width = width,
			SortMode = DataGridViewColumnSortMode.NotSortable
		});
	}

	private static void Bool(DataGridView g, string prop, string text, int width)
	{
		g.Columns.Add(new DataGridViewCheckBoxColumn
		{
			Name = prop,
			DataPropertyName = prop,
			HeaderText = text,
			Width = width,
			SortMode = DataGridViewColumnSortMode.NotSortable
		});
	}

	private static void Groups(ConfigurationGrid g, string[] labels, int[] first, int[] last)
	{
		for (int i = 0; i < labels.Length; i++)
		{
			g.Groups.Add(Tuple.Create(labels[i], first[i], last[i]));
		}
	}

	private static Label Block(TableLayoutPanel parent, int row, string text, DataGridView grid, Button[] buttons)
	{
		Panel panel = new Panel();
		panel.Dock = DockStyle.Fill;
		panel.Margin = new Padding(1);
		Panel panel2 = panel;
		parent.Controls.Add(panel2, 0, row);
		Panel panel3 = new Panel();
		panel3.Dock = DockStyle.Top;
		panel3.Height = 29;
		Panel panel4 = panel3;
		Label label = new Label();
		label.Text = text;
		label.AutoEllipsis = true;
		label.Dock = DockStyle.Fill;
		label.TextAlign = ContentAlignment.MiddleLeft;
		label.Font = new Font("Tahoma", 8.25f, FontStyle.Bold | FontStyle.Underline);
		Label label2 = label;
		panel4.Controls.Add(label2);
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel();
		flowLayoutPanel.Dock = DockStyle.Right;
		flowLayoutPanel.Width = buttons.Length * 92;
		flowLayoutPanel.FlowDirection = FlowDirection.RightToLeft;
		FlowLayoutPanel flowLayoutPanel2 = flowLayoutPanel;
		foreach (Button button in buttons)
		{
			button.AutoSize = false;
			button.MinimumSize = Size.Empty;
			button.Size = new Size(87, 23);
			button.Font = new Font("Tahoma", 8.25f);
			button.Margin = new Padding(2, 2, 1, 0);
			flowLayoutPanel2.Controls.Add(button);
		}
		panel4.Controls.Add(flowLayoutPanel2);
		label2.BringToFront();
		panel2.Controls.Add(grid);
		panel2.Controls.Add(panel4);
		grid.BringToFront();
		return label2;
	}

	private static void TopTools(Control parent, Button[] buttons)
	{
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel();
		flowLayoutPanel.Dock = DockStyle.Top;
		flowLayoutPanel.Height = 38;
		FlowLayoutPanel flowLayoutPanel2 = flowLayoutPanel;
		foreach (Button value in buttons)
		{
			flowLayoutPanel2.Controls.Add(value);
		}
		parent.Controls.Add(flowLayoutPanel2);
		foreach (DataGridView item in parent.Controls.OfType<DataGridView>())
		{
			item.BringToFront();
		}
	}

	private void EditSection(bool create)
	{
		if (engine.IsTd2)
		{
			EditTd2Section(create);
			return;
		}
		CommitGrids();
		Section section = Selected();
		if (!create && section == null)
		{
			return;
		}
		Section sec = (create ? new Section
		{
			Id = Guid.NewGuid().ToString("N"),
			Name = "",
			Line = (engine.IsTd2 ? "" : "1"),
			Tracks = (engine.IsTd2 ? 1 : 2),
			NormalIncomingTrack = 1,
			ArrivalEven = true,
			Conducted = true,
			JournalName = "",
			Basis = (engine.IsTd2 ? "" : "Blokada samoczynna"),
			Crossings = new List<CrossingSetting>(),
			LineTracks = new List<LineTrackSetting>()
		} : Json.Clone(section));
		SectionTrackDirections.Normalize(sec);
		using Editor editor = new Editor(create ? "Dodawanie nowego dziennika ruchu" : "Ustawienia zasadnicze dziennika ruchu", 1020, 650);
		Panel panel = editor.AbsoluteBody();
		GroupBox groupBox = new GroupBox();
		groupBox.Text = "Podstawowe ustawienia";
		groupBox.Bounds = new Rectangle(9, 8, 990, 152);
		GroupBox groupBox2 = groupBox;
		panel.Controls.Add(groupBox2);
		TextBox line = Field(groupBox2, engine.IsTd2 ? "Oznaczenie szlaku" : "Linia kolejowa - nr", new TextBox
		{
			Text = sec.Line,
			ReadOnly = (engine.IsTd2 && !create && !string.IsNullOrEmpty(sec.Td2RouteKey))
		}, 10, 22, engine.IsTd2 ? 150 : 75);
		Control lineName = (engine.IsTd2 ? ((Control)Field(groupBox2, "Rodzaj szlaku", Ui.Combo(new string[3] { "Nieustalony", "Wewnętrzny", "Zewnętrzny" }, string.IsNullOrEmpty(sec.Td2Kind) ? "Nieustalony" : sec.Td2Kind, editable: false), 170, 22, 230)) : ((Control)Field(groupBox2, "Nazwa linii", new TextBox
		{
			Text = sec.LineName
		}, 90, 22, 310)));
		if (engine.IsTd2 && !create && !string.IsNullOrEmpty(sec.Td2RouteKey))
		{
			lineName.Enabled = false;
		}
		ComboBox count = Field(groupBox2, "Rodzaj linii na szlaku", Ui.Combo(new string[2] { "Jednotorowa", "Dwutorowa" }, (sec.Tracks == 2) ? "Dwutorowa" : "Jednotorowa", editable: false), 412, 22, 215);
		TextBox journal = Field(groupBox2, "Nazwa dziennika ruchu", new TextBox
		{
			Text = (engine.IsTd2 ? sec.Line : sec.JournalName),
			ReadOnly = engine.IsTd2
		}, 640, 22, 330);
		TextBox neighbour = Field(groupBox2, engine.IsTd2 ? "Posterunek wewnętrzny (zewnętrzny: wg RJ)" : "Szlak Z / Do", new TextBox
		{
			Text = (engine.IsTd2 ? sec.Td2PeerName : sec.Name)
		}, 10, 82, 390);
		if (engine.IsTd2)
		{
			line.TextChanged += (object s, EventArgs e) =>
			{
				journal.Text = line.Text;
			};
			neighbour.ReadOnly = !create;
		}
		ComboBox odd = Field(groupBox2, "Kierunek nieparzysty", Ui.Combo(new string[2]
		{
			Direction(incoming: true),
			Direction(incoming: false)
		}, Direction(!sec.ArrivalEven), editable: false), 412, 82, 215);
		CheckBox conducted = new CheckBox
		{
			Text = "Dziennik prowadzony",
			Checked = (sec.Conducted != false),
			Bounds = new Rectangle(640, 108, 300, 25)
		};
		groupBox2.Controls.Add(conducted);
		if (sec.Td2Archived)
		{
			conducted.Enabled = false;
		}
		ComboBox[] nums = new ComboBox[2];
		ComboBox[] dirs = new ComboBox[2];
		ComboBox[] traffic = new ComboBox[2];
		ComboBox[] bases = new ComboBox[2];
		ComboBox[] outBases = new ComboBox[2];
		string[] choices = TrafficBasis.Choices;
		for (int num = 0; num < 2; num++)
		{
			int index = num;
			int number;
			if (sec.Tracks == 1)
			{
				number = num + 1;
			}
			else
			{
				number = ((num == 0) ? ((sec.RightTrackNumber != 1) ? 1 : 2) : (sec.RightTrackNumber ?? 2));
			}
			LineTrackSetting lineTrackSetting = sec.LineTracks.FirstOrDefault((LineTrackSetting x) => x.Number == number);
			GroupBox group = new GroupBox
			{
				Text = ((num == 0) ? "Tor szlakowy lewy" : "Tor szlakowy prawy"),
				Bounds = new Rectangle(9 + num * 500, 174, 490, 350)
			};
			panel.Controls.Add(group);
			nums[num] = Field(group, "Numer toru", Ui.Combo(new string[2] { "1", "2" }, number.ToString(), editable: false), 12, 22, 215);
			dirs[num] = Field(group, "Zasadniczy kierunek ruchu", Ui.Combo(new string[2]
			{
				Direction(incoming: true),
				Direction(incoming: false)
			}, Direction(lineTrackSetting?.NormalIncoming ?? (number == sec.NormalIncomingTrack)), editable: false), 12, 83, 460);
			traffic[num] = Field(group, "Zasadniczy rodzaj ruchu", Ui.Combo(new string[2] { "Jednokierunkowy", "Dwukierunkowy" }, (lineTrackSetting == null) ? "Jednokierunkowy" : (lineTrackSetting.TrafficType ?? "Jednokierunkowy"), editable: false), 12, 142, 460);
			bases[num] = Field(group, "Podstawa ruchu Z posterunku sąsiedniego", Ui.Combo(choices, TrafficBasis.Display((lineTrackSetting == null) ? sec.Basis : lineTrackSetting.Basis), editable: true), 12, 202, 460);
			outBases[num] = Field(group, "Podstawa ruchu DO posterunku sąsiedniego", Ui.Combo(choices, TrafficBasis.Display((lineTrackSetting == null) ? sec.Basis : (lineTrackSetting.BasisOut ?? lineTrackSetting.Basis)), editable: true), 12, 262, 460);
			count.SelectedIndexChanged += (object s, EventArgs e) =>
			{
				group.Enabled = index == 0 || count.SelectedIndex == 1;
			};
			group.Enabled = num == 0 || sec.Tracks == 2;
		}
		Action defaultDirections = () =>
		{
			if (count.SelectedIndex == 1)
			{
				int num3 = ((odd.SelectedIndex != 1) ? 1 : 2);
				for (int i = 0; i < 2; i++)
				{
					dirs[i].SelectedIndex = ((int.Parse(nums[i].Text) != num3) ? 1 : 0);
				}
			}
		};
		odd.SelectedIndexChanged += (object s, EventArgs e) =>
		{
			defaultDirections();
		};
		count.SelectedIndexChanged += (object s, EventArgs e) =>
		{
			defaultDirections();
		};
		ComboBox[] array = nums;
		foreach (ComboBox comboBox in array)
		{
			comboBox.SelectedIndexChanged += (object s, EventArgs e) =>
			{
				defaultDirections();
			};
		}
		editor.Save(() =>
		{
			sec.Line = Ui.Required(line, "Numer linii");
			if (engine.IsTd2)
			{
				sec.Td2Kind = ((lineName.Text == "Nieustalony") ? "" : lineName.Text);
				sec.Td2PeerName = ((sec.Td2Kind == "Zewnętrzny") ? "" : neighbour.Text.Trim());
				sec.Name = (string.IsNullOrEmpty(sec.Td2PeerName) ? sec.Line : sec.Td2PeerName);
				sec.JournalName = sec.Line;
				if (create)
				{
					Section section2 = sec;
					string text = ((sec.Td2Kind == "Zewnętrzny") ? "E:" : ((sec.Td2Kind == "Wewnętrzny") ? "I:" : "U:"));
					section2.Td2RouteKey = text + sec.Line.ToUpperInvariant();
				}
			}
			else
			{
				sec.LineName = lineName.Text.Trim();
				sec.Name = Ui.Required(neighbour, "Szlak Z / Do");
				sec.JournalName = (string.IsNullOrWhiteSpace(journal.Text) ? sec.Name : journal.Text.Trim());
			}
			sec.Tracks = count.SelectedIndex + 1;
			sec.ArrivalEven = odd.SelectedIndex == 1;
			sec.Conducted = conducted.Checked;
			List<LineTrackSetting> list = new List<LineTrackSetting>();
			for (int i = 0; i < sec.Tracks; i++)
			{
				list.Add(new LineTrackSetting
				{
					Number = int.Parse(nums[i].Text),
					NormalIncoming = (dirs[i].SelectedIndex == 0),
					TrafficType = traffic[i].Text,
					Basis = bases[i].Text,
					BasisOut = outBases[i].Text
				});
			}
			if (list.Select((LineTrackSetting x) => x.Number).Distinct().Count() != sec.Tracks || list.Any((LineTrackSetting x) => x.Number > sec.Tracks))
			{
				throw new Exception("Podaj różne numery torów od 1 do liczby torów na szlaku.");
			}
			if (Enumerable.Count(list, (LineTrackSetting x) => x.NormalIncoming) > 1)
			{
				throw new Exception("Tory nie mogą mieć jednocześnie zasadniczego kierunku do naszego posterunku.");
			}
			sec.LineTracks = list;
			sec.NormalIncomingTrack = (from x in list
				where x.NormalIncoming
				select x.Number).FirstOrDefault();
			sec.RightTrackNumber = ((sec.Tracks == 2) ? new int?(int.Parse(nums[1].Text)) : new int?(1));
			sec.Basis = list[0].Basis;
		});
		if (editor.ShowDialog(this) == DialogResult.OK)
		{
			binding = true;
			if (create)
			{
				sections.Add(sec);
			}
			else
			{
				sections[sections.IndexOf(section)] = sec;
			}
			binding = false;
			sectionGrid.CurrentCell = sectionGrid.Rows[sections.IndexOf(sec)].Cells[0];
			RefreshTabChoices();
			BindDetails();
		}
	}

	private void EditCrossing(bool create)
	{
		Section sec = Selected();
		if (sec == null)
		{
			return;
		}
		CrossingSetting crossingSetting = ((crossingGrid.CurrentRow == null) ? null : (crossingGrid.CurrentRow.DataBoundItem as CrossingSetting));
		if (!create && crossingSetting == null)
		{
			return;
		}
		CrossingSetting item = (create ? new CrossingSetting
		{
			Id = Guid.NewGuid().ToString("N"),
			NotificationMode = ((sec.Tracks == 1) ? "Znak" : "Godzina"),
			InJournal = true,
			AlongTrack = ((sec.Tracks == 1) ? "Jednotorowych" : "Dwutorowych"),
			Trains = "Wszystkich"
		} : Json.Clone(crossingSetting));
		using Editor editor = new Editor("Posterunek dróżnika przejazdowego", 720, 540);
		TextBox km = editor.Add("Km na linii", Ui.Text(item.Kilometre, multiline: false), 35);
		TextBox nr = editor.Add("Numer na linii", Ui.Text(item.Number, multiline: false), 35);
		TextBox title = editor.Add("Nazwa", Ui.Text(item.Name, multiline: false), 35);
		TextBox mark = editor.Add("Oznaczenie w dzienniku", Ui.Text(item.Mark, multiline: false), 35);
		ComboBox along = editor.Add("Według szlaku", Ui.Combo(new string[3] { "Jednotorowych", "Dwutorowych", "Wszystkich" }, item.AlongTrack, editable: true), 35);
		ComboBox trains = editor.Add("Dla pociągów", Ui.Combo(new string[3] { "Wszystkich", "Parzystych", "Nieparzystych" }, item.Trains, editable: false), 35);
		CheckBox clock = editor.Add("Rejestrowanie godziny", new CheckBox
		{
			Checked = (item.RecordClosure == true),
			Text = "Np. zamknięcia rogatek, przewidywanego wyjazdu"
		}, 35);
		CheckBox visible = editor.Add("W Dzienniku Ruchu", new CheckBox
		{
			Checked = (item.InJournal != false)
		}, 35);
		editor.Save(() =>
		{
			item.Kilometre = Ui.Required(km, "Km na linii");
			item.Number = nr.Text.Trim();
			item.Name = Ui.Required(title, "Nazwa");
			item.Mark = mark.Text.Trim();
			item.AlongTrack = along.Text;
			item.Trains = trains.Text;
			item.RecordClosure = clock.Checked;
			item.NotificationMode = (CrossingRules.UsesTime(sec, item) ? "Godzina" : "Znak");
			item.InJournal = visible.Checked;
		});
		if (editor.ShowDialog(this) == DialogResult.OK)
		{
			if (create)
			{
				sec.Crossings.Add(item);
			}
			else
			{
				sec.Crossings[sec.Crossings.IndexOf(crossingSetting)] = item;
			}
			BindDetails();
		}
	}

	private static T Field<T>(Control parent, string label, T field, int x, int y, int width) where T : Control
	{
		parent.Controls.Add(new Label
		{
			Text = label,
			Bounds = new Rectangle(x, y, width, 22)
		});
		field.Dock = DockStyle.None;
		field.SetBounds(x, y + 23, width, 25);
		parent.Controls.Add(field);
		return field;
	}

	private static GroupBox Box(string text)
	{
		GroupBox groupBox = new GroupBox();
		groupBox.Text = text;
		groupBox.Dock = DockStyle.Fill;
		groupBox.Padding = new Padding(12);
		groupBox.Margin = new Padding(8);
		return groupBox;
	}

	private void BuildRequired(TabPage page)
	{
		string[] standards = new string[4] { "Łączność z dróżnikami przejazdowymi", "Oświetlenie semaforów i wskaźników", "Urządzenia radiołączności", "Rejestrator rozmów telefonicznych" };
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
		tableLayoutPanel.Dock = DockStyle.Fill;
		tableLayoutPanel.ColumnCount = 2;
		tableLayoutPanel.RowCount = 2;
		tableLayoutPanel.Padding = new Padding(4);
		TableLayoutPanel tableLayoutPanel2 = tableLayoutPanel;
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
		page.Controls.Add(tableLayoutPanel2);
		for (int i = 0; i < standards.Length; i++)
		{
			string title = standards[i];
			RequiredCheckSetting requiredCheckSetting = checks.FirstOrDefault((RequiredCheckSetting x) => x.Name == title);
			if (requiredCheckSetting == null)
			{
				RequiredCheckSetting requiredCheckSetting2 = new RequiredCheckSetting();
				requiredCheckSetting2.Name = title;
				requiredCheckSetting2.Enabled = false;
				requiredCheckSetting2.DailyChecks = false;
				requiredCheckSetting2.Hours = "";
				requiredCheckSetting = requiredCheckSetting2;
				checks.Add(requiredCheckSetting);
			}
			CheckSchedulePanel checkSchedulePanel = new CheckSchedulePanel(requiredCheckSetting);
			checkSchedulePanel.Dock = DockStyle.Fill;
			checkSchedulePanel.Margin = new Padding(7);
			CheckSchedulePanel checkSchedulePanel2 = checkSchedulePanel;
			schedulePanels.Add(checkSchedulePanel2);
			tableLayoutPanel2.Controls.Add(checkSchedulePanel2, i % 2, i / 2);
		}
		GroupBox groupBox = new GroupBox();
		groupBox.Text = "Pozostałe sprawdzenia";
		groupBox.Dock = DockStyle.Bottom;
		groupBox.Height = 150;
		groupBox.Padding = new Padding(7);
		GroupBox groupBox2 = groupBox;
		page.Controls.Add(groupBox2);
		tableLayoutPanel2.BringToFront();
		checkGrid = new ConfigurationGrid
		{
			ReadOnly = false,
			ColumnHeadersHeight = 25
		};
		Column(checkGrid, "Name", "Rodzaj sprawdzenia", 370);
		Column(checkGrid, "Hours", "Godziny HH:mm; HH:mm", 240);
		Column(checkGrid, "AfterDutyMinutes", "Po przejęciu dyżuru (min)", 170);
		Bool(checkGrid, "Enabled", "Aktywne", 90);
		Action bind = () =>
		{
			checkGrid.DataSource = new BindingList<RequiredCheckSetting>(checks.Where((RequiredCheckSetting x) => !standards.Contains(x.Name)).ToList());
		};
		bind();
		groupBox2.Controls.Add(checkGrid);
		TopTools(groupBox2, new Button[2]
		{
			Ui.Button("Dodaj", () =>
			{
				checks.Add(new RequiredCheckSetting
				{
					Name = "Nowe sprawdzenie",
					Enabled = true,
					Hours = ""
				});
				bind();
			}),
			Ui.Button("Usuń", () =>
			{
				if (checkGrid.CurrentRow != null)
				{
					checks.Remove((RequiredCheckSetting)checkGrid.CurrentRow.DataBoundItem);
					bind();
				}
			})
		});
	}

	private void BuildOther(TabPage page)
	{
		StationOptions options = copy.Options;
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
		tableLayoutPanel.Dock = DockStyle.Fill;
		tableLayoutPanel.ColumnCount = 2;
		tableLayoutPanel.RowCount = 5;
		tableLayoutPanel.Padding = new Padding(5);
		TableLayoutPanel tableLayoutPanel2 = tableLayoutPanel;
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
		float[] array = new float[5] { 24f, 27f, 22f, 14f, 13f };
		foreach (float num in array)
		{
			tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, num));
		}
		page.Controls.Add(tableLayoutPanel2);
		GroupBox groupBox = Box("Numeracja dziennika telefonicznego");
		tableLayoutPanel2.Controls.Add(groupBox, 0, 0);
		tableLayoutPanel2.SetRowSpan(groupBox, 2);
		telephoneMonthly = new RadioButton
		{
			Text = "Numeracja miesięczna",
			Checked = !options.TelephoneAnnual,
			AutoSize = true,
			Location = new Point(18, 32)
		};
		telephoneAnnual = new RadioButton
		{
			Text = "Numeracja roczna",
			Checked = options.TelephoneAnnual,
			AutoSize = true,
			Location = new Point(18, 60)
		};
		telephoneNext = new NumericUpDown
		{
			Minimum = 1m,
			Maximum = 2147483646m,
			Value = TelephoneNumbering.Next(engine.State, copy, engine.Now),
			Location = new Point(290, 104),
			Width = 105
		};
		groupBox.Controls.Add(telephoneMonthly);
		groupBox.Controls.Add(telephoneAnnual);
		groupBox.Controls.Add(telephoneNext);
		groupBox.Controls.Add(new Label
		{
			Text = "Nowy numer w dzienniku telefonicznym:",
			AutoSize = true,
			Location = new Point(18, 108)
		});
		groupBox.Controls.Add(new Label
		{
			Text = "Numeracja miesięczna: od 1 pierwszego dnia miesiąca.\nNumeracja roczna: od 1 dnia 1 stycznia.\nDotychczasowe numery pozostają zachowane.",
			Location = new Point(18, 150),
			Size = new Size(450, 65)
		});
		telephoneAnnual.CheckedChanged += (object s, EventArgs e) =>
		{
			Station station = Json.Clone(copy);
			station.Options.TelephoneAnnual = telephoneAnnual.Checked;
			station.Options.TelephoneNextPeriod = null;
			telephoneNext.Value = TelephoneNumbering.Next(engine.State, station, engine.Now);
		};
		GroupBox groupBox2 = Box("Usunięcie zajętości toru szlakowego dla wyprawienia pociągu na blokadzie samoczynnej");
		tableLayoutPanel2.Controls.Add(groupBox2, 0, 2);
		tableLayoutPanel2.SetColumnSpan(groupBox2, 2);
		defaultRelease = new RadioButton
		{
			Text = "Użyj ustawień systemowych — usunięcie po 60 sekundach",
			Checked = !options.AutomaticReleaseSeconds.HasValue,
			Location = new Point(18, 32),
			AutoSize = true
		};
		customRelease = new RadioButton
		{
			Text = "Na wszystkich szlakach bieżącego posterunku — usunięcie po",
			Checked = options.AutomaticReleaseSeconds.HasValue,
			Location = new Point(18, 69),
			AutoSize = true
		};
		releaseSeconds = new NumericUpDown
		{
			Minimum = 1m,
			Maximum = 86400m,
			Value = (options.AutomaticReleaseSeconds ?? 60),
			Location = new Point(472, 67),
			Width = 90
		};
		groupBox2.Controls.Add(defaultRelease);
		groupBox2.Controls.Add(customRelease);
		groupBox2.Controls.Add(releaseSeconds);
		groupBox2.Controls.Add(new Label
		{
			Text = "sekundach",
			Location = new Point(570, 71),
			AutoSize = true
		});
		customRelease.CheckedChanged += (object s, EventArgs e) =>
		{
			releaseSeconds.Enabled = customRelease.Checked;
		};
		releaseSeconds.Enabled = customRelease.Checked;
		GroupBox groupBox3 = Box("Zajętość torów stacyjnych");
		tableLayoutPanel2.Controls.Add(groupBox3, 1, 0);
		refreshTrackControl = new CheckBox
		{
			Text = "Odświeżaj okresowo kontrolkę zajętości torów",
			Checked = (options.RefreshStationTracks != false),
			AutoSize = true,
			Location = new Point(18, 30)
		};
		trackRefreshMinutes = new NumericUpDown
		{
			Minimum = 1m,
			Maximum = 1440m,
			Value = Math.Max(1, options.StationTrackRefreshMinutes),
			Location = new Point(300, 76),
			Width = 85
		};
		groupBox3.Controls.Add(refreshTrackControl);
		groupBox3.Controls.Add(new Label
		{
			Text = "Częstotliwość odświeżania w minutach:",
			AutoSize = true,
			Location = new Point(18, 80)
		});
		groupBox3.Controls.Add(trackRefreshMinutes);
		refreshTrackControl.CheckedChanged += (object s, EventArgs e) =>
		{
			trackRefreshMinutes.Enabled = refreshTrackControl.Checked;
		};
		trackRefreshMinutes.Enabled = refreshTrackControl.Checked;
		GroupBox journals = Box("Widoczność dzienników ruchu w trakcie prowadzenia dyżuru");
		tableLayoutPanel2.Controls.Add(journals, 1, 1);
		visibility = new RadioButton[3];
		string[] array2 = new string[3] { "Użyj ustawień systemowych", "Pokazuj tylko prowadzone dzienniki ruchu", "Pokazuj również nieprowadzone dzienniki w trybie tylko do odczytu" };
		for (int num2 = 0; num2 < 3; num2++)
		{
			visibility[num2] = new RadioButton
			{
				Text = array2[num2],
				Checked = (options.JournalVisibility == num2),
				AutoSize = false,
				Location = new Point(18, 30 + num2 * 42),
				Size = new Size(520, 40)
			};
			journals.Controls.Add(visibility[num2]);
		}
		journals.Resize += (object s, EventArgs e) =>
		{
			RadioButton[] array3 = visibility;
			foreach (RadioButton radioButton in array3)
			{
				radioButton.Width = Math.Max(200, journals.ClientSize.Width - 36);
			}
		};
		GroupBox groupBox4 = Box("Zapis danych do SEPE");
		tableLayoutPanel2.Controls.Add(groupBox4, 0, 3);
		tableLayoutPanel2.SetColumnSpan(groupBox4, 2);
		writeSepe = new CheckBox
		{
			Text = "Wysyłaj przyjazd / odjazd z dzienników ruchu i formularza godzin",
			Checked = (options.WriteSepe != false),
			AutoSize = true,
			Location = new Point(18, 32)
		};
		groupBox4.Controls.Add(writeSepe);
		groupBox4.Controls.Add(new Label
		{
			Text = "Potwierdzenie oraz kolor kategorii są pobierane z SEPE/API.",
			AutoSize = true,
			Location = new Point(18, 54)
		});
		GroupBox groupBox5 = Box("Posterunek ruchu");
		tableLayoutPanel2.Controls.Add(groupBox5, 0, 4);
		tableLayoutPanel2.SetColumnSpan(groupBox5, 2);
		name = new TextBox
		{
			Text = copy.Name,
			ReadOnly = (engine.SimRail != null),
			Location = new Point(18, 29),
			Width = 430
		};
		groupBox5.Controls.Add(name);
		groupBox5.Controls.Add(new Label
		{
			Text = "Typ posterunku (ustawienie lokalne):",
			Location = new Point(480, 14),
			AutoSize = true
		});
		postType = Ui.Combo(StationIdentity.Types, StationIdentity.Type(copy), editable: false);
		postType.Dock = DockStyle.None;
		postType.SetBounds(480, 32, 340, 25);
		groupBox5.Controls.Add(postType);
	}

	private void ReadConfigurationOptions()
	{
		copy.Options.TelephoneAnnual = telephoneAnnual.Checked;
		copy.Options.TelephoneNextPeriod = TelephoneNumbering.Period(engine.Now, telephoneAnnual.Checked);
		copy.Options.TelephoneNextNumber = (int)telephoneNext.Value;
		Station station = Json.Clone(copy);
		station.Options.TelephoneNextPeriod = null;
		if (copy.Options.TelephoneNextNumber < TelephoneNumbering.Next(engine.State, station, engine.Now))
		{
			throw new Exception("Nowy numer musi być większy od numerów już użytych w bieżącym okresie.");
		}
		copy.PostType = postType.Text;
		foreach (CheckSchedulePanel schedulePanel in schedulePanels)
		{
			schedulePanel.Save();
		}
		copy.Options.AutomaticReleaseSeconds = (customRelease.Checked ? new int?((int)releaseSeconds.Value) : ((int?)null));
		copy.Options.RefreshStationTracks = refreshTrackControl.Checked;
		copy.Options.StationTrackRefreshMinutes = (int)trackRefreshMinutes.Value;
		copy.Options.WriteSepe = writeSepe.Checked;
		copy.Options.JournalVisibility = Array.FindIndex(visibility, (RadioButton x) => x.Checked);
	}

	private void BuildTd2Area(TabControl pages)
	{
		TabPage tabPage = new TabPage("Obszar posterunku");
		pages.TabPages.Add(tabPage);
		Label label = new Label();
		label.Text = "Posterunek główny: " + copy.Name + "\nZaznacz części tej samej stacji. EDR obejmie je wspólnym obszarem i wyznaczy kierunki poza nim.\nSąsiednie samodzielne stacje pozostaw niezaznaczone. Zmiany zachowują dotychczasowe dzienniki i wpisy.";
		label.Dock = DockStyle.Top;
		label.Height = 82;
		label.Padding = new Padding(12);
		Label label2 = label;
		td2AreaList = new CheckedListBox
		{
			Dock = DockStyle.Fill,
			CheckOnClick = true,
			IntegralHeight = false,
			HorizontalScrollbar = true
		};
		HashSet<string> hashSet = new HashSet<string>((copy.Td2AreaPosts ?? new List<Td2AreaPost>()).Select((Td2AreaPost x) => x.Id));
		List<Td2AreaPost> list = (from x in Td2Area.Choices(engine.SimRail, copy).Concat(copy.Td2AreaPosts ?? new List<Td2AreaPost>())
			where x.Id != copy.SimRailPointId
			group x by x.Id into x
			select x.First() into x
			orderby x.Name
			select x).ToList();
		foreach (Td2AreaPost item in list)
		{
			td2AreaList.Items.Add(item, hashSet.Contains(item.Id));
		}
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel();
		flowLayoutPanel.Dock = DockStyle.Bottom;
		flowLayoutPanel.Height = 44;
		flowLayoutPanel.Padding = new Padding(7);
		FlowLayoutPanel flowLayoutPanel2 = flowLayoutPanel;
		flowLayoutPanel2.Controls.Add(Ui.Button("Przelicz dzienniki dla obszaru", () =>
		{
			ApplyTd2Area();
		}));
		tabPage.Controls.Add(td2AreaList);
		tabPage.Controls.Add(label2);
		tabPage.Controls.Add(flowLayoutPanel2);
		if (list.Count == 0)
		{
			label2.Text += "\nLista części pojawi się po pobraniu rozkładu tej scenerii.";
		}
	}

	private void ReadTd2Area()
	{
		if (td2AreaList != null)
		{
			copy.Td2AreaPosts = td2AreaList.CheckedItems.Cast<Td2AreaPost>().Select(Json.Clone).ToList();
		}
	}

	private void ApplyTd2Area()
	{
		if (!engine.IsTd2)
		{
			return;
		}
		ReadTd2Area();
		if (engine.SimRail == null)
		{
			return;
		}
		CommitGrids();
		copy.Sections = sections.ToList();
		Station proposed = Td2Projection.SuggestStation(engine.SimRail, copy);
		Td2Topology.Reconcile(copy, proposed, engine.State.Entries);
		List<Section> list = copy.Sections.ToList();
		binding = true;
		sections.RaiseListChangedEvents = false;
		try
		{
			sections.Clear();
			foreach (Section item in list)
			{
				sections.Add(item);
			}
		}
		finally
		{
			sections.RaiseListChangedEvents = true;
			sections.ResetBindings();
			binding = false;
		}
		copy.Sections = sections.ToList();
		RefreshTabChoices();
		BindDetails();
	}

	private void EditTd2Section(bool create)
	{
		CommitGrids();
		Section section = Selected();
		if (!create && section == null)
		{
			return;
		}
		Section sec = (create ? new Section
		{
			Id = Guid.NewGuid().ToString("N"),
			Name = "",
			Td2LocalLine = "",
			Td2SettingsVersion = 1,
			Line = "",
			Tracks = 1,
			NormalIncomingTrack = 1,
			ArrivalEven = true,
			Conducted = true,
			Basis = "",
			Crossings = new List<CrossingSetting>(),
			LineTracks = new List<LineTrackSetting>()
		} : Json.Clone(section));
		SectionTrackDirections.Normalize(sec);
		Td2SectionSettings.Normalize(sec);
		using Editor editor = new Editor(create ? "Nowy dziennik TD2" : "Ustawienia szlaku i dziennika TD2", 1020, 748);
		Panel panel = editor.AbsoluteBody();
		GroupBox groupBox = new GroupBox();
		groupBox.Text = "Szlak źródłowy i ustawienia dziennika";
		groupBox.Bounds = new Rectangle(9, 8, 990, 218);
		GroupBox groupBox2 = groupBox;
		panel.Controls.Add(groupBox2);
		TextBox local = Field(groupBox2, "Oznaczenie szlaku w TD2", new TextBox
		{
			Text = sec.Td2LocalLine,
			ReadOnly = !create
		}, 10, 22, 220);
		ComboBox kind = Field(groupBox2, "Rodzaj szlaku", Ui.Combo(new string[3] { "Nieustalony", "Wewnętrzny", "Zewnętrzny" }, string.IsNullOrEmpty(sec.Td2Kind) ? "Nieustalony" : sec.Td2Kind, editable: false), 250, 22, 170);
		kind.Enabled = create;
		TextBox line = Field(groupBox2, "Nr linii", new TextBox
		{
			Text = sec.Line
		}, 440, 22, 110);
		TextBox lineName = Field(groupBox2, "Nazwa linii (opcjonalnie)", new TextBox
		{
			Text = sec.LineName
		}, 570, 22, 390);
		TextBox peer = Field(groupBox2, "Posterunek wewnętrzny", new TextBox
		{
			Text = sec.Td2PeerName,
			ReadOnly = !create
		}, 10, 82, 260);
		CheckBox usePeer = new CheckBox
		{
			Text = "Używaj nazwy posterunku wewnętrznego",
			Checked = (sec.Td2UsePeerName != false),
			Bounds = new Rectangle(290, 102, 270, 37)
		};
		groupBox2.Controls.Add(usePeer);
		TextBox journal = Field(groupBox2, "Nazwa dziennika ruchu", new TextBox
		{
			Text = StationSettings.Title(sec)
		}, 570, 82, 390);
		local.Name = "td2SourceLine";
		line.Name = "td2AssignedLine";
		journal.Name = "td2JournalName";
		usePeer.Name = "td2UsePeerName";
		bool custom = sec.Td2CustomJournalName;
		bool changing = false;
		Action automatic = () =>
		{
			if (custom)
			{
				return;
			}
			Section section2 = Json.Clone(sec);
			section2.Td2LocalLine = local.Text.Trim();
			section2.Td2Kind = kind.Text;
			section2.Td2PeerName = peer.Text.Trim();
			section2.Td2UsePeerName = usePeer.Checked;
			changing = true;
			try
			{
				journal.Text = Td2SectionSettings.AutomaticTitle(section2);
			}
			finally
			{
				changing = false;
			}
		};
		journal.TextChanged += (object s, EventArgs e) =>
		{
			if (!changing)
			{
				custom = true;
			}
		};
		local.TextChanged += (object s, EventArgs e) =>
		{
			automatic();
		};
		peer.TextChanged += (object s, EventArgs e) =>
		{
			automatic();
		};
		usePeer.CheckedChanged += (object s, EventArgs e) =>
		{
			automatic();
		};
		Action kindChanged = () =>
		{
			usePeer.Enabled = kind.Text == "Wewnętrzny";
			peer.Enabled = kind.Text != "Zewnętrzny";
			automatic();
		};
		kind.SelectedIndexChanged += (object s, EventArgs e) =>
		{
			kindChanged();
		};
		kindChanged();
		Button button = Ui.Button("Nazwa domyślna", () =>
		{
			custom = false;
			automatic();
		});
		button.AutoSize = false;
		button.SetBounds(815, 137, 145, 25);
		groupBox2.Controls.Add(button);
		ComboBox odd = Field(groupBox2, "Kierunek nieparzysty", Ui.Combo(new string[2]
		{
			Direction(incoming: true),
			Direction(incoming: false)
		}, Direction(!sec.ArrivalEven), editable: false), 10, 152, 260);
		ComboBox count = Field(groupBox2, "Liczba torów na szlaku", Ui.Combo(new string[2] { "Jednotorowa", "Dwutorowa" }, (sec.Tracks == 2) ? "Dwutorowa" : "Jednotorowa", editable: false), 290, 152, 260);
		CheckBox conducted = new CheckBox
		{
			Text = "Dziennik prowadzony",
			Checked = (sec.Conducted != false),
			Enabled = (!sec.Td2Archived && !sec.Td2AreaHidden),
			Bounds = new Rectangle(570, 175, 300, 25)
		};
		groupBox2.Controls.Add(conducted);
		ComboBox[] nums = new ComboBox[2];
		ComboBox[] dirs = new ComboBox[2];
		ComboBox[] traffic = new ComboBox[2];
		ComboBox[] bases = new ComboBox[2];
		ComboBox[] outBases = new ComboBox[2];
		string[] choices = TrafficBasis.Choices;
		for (int num = 0; num < 2; num++)
		{
			int index = num;
			int number;
			if (sec.Tracks == 1)
			{
				number = num + 1;
			}
			else
			{
				number = ((num == 0) ? ((sec.RightTrackNumber != 1) ? 1 : 2) : (sec.RightTrackNumber ?? 2));
			}
			LineTrackSetting lineTrackSetting = sec.LineTracks.FirstOrDefault((LineTrackSetting x) => x.Number == number);
			GroupBox group = new GroupBox
			{
				Text = ((num == 0) ? "Tor szlakowy lewy" : "Tor szlakowy prawy"),
				Bounds = new Rectangle(9 + num * 500, 238, 490, 350)
			};
			panel.Controls.Add(group);
			nums[num] = Field(group, "Numer toru", Ui.Combo(new string[2] { "1", "2" }, number.ToString(), editable: false), 12, 22, 215);
			dirs[num] = Field(group, "Zasadniczy kierunek ruchu", Ui.Combo(new string[2]
			{
				Direction(incoming: true),
				Direction(incoming: false)
			}, Direction(lineTrackSetting?.NormalIncoming ?? (number == sec.NormalIncomingTrack)), editable: false), 12, 83, 460);
			traffic[num] = Field(group, "Zasadniczy rodzaj ruchu", Ui.Combo(new string[2] { "Jednokierunkowy", "Dwukierunkowy" }, (lineTrackSetting == null) ? "Jednokierunkowy" : (lineTrackSetting.TrafficType ?? "Jednokierunkowy"), editable: false), 12, 142, 460);
			bases[num] = Field(group, "Podstawa ruchu Z posterunku sąsiedniego", Ui.Combo(choices, TrafficBasis.Display((lineTrackSetting == null) ? sec.Basis : lineTrackSetting.Basis), editable: true), 12, 202, 460);
			outBases[num] = Field(group, "Podstawa ruchu DO posterunku sąsiedniego", Ui.Combo(choices, TrafficBasis.Display((lineTrackSetting == null) ? sec.Basis : (lineTrackSetting.BasisOut ?? lineTrackSetting.Basis)), editable: true), 12, 262, 460);
			count.SelectedIndexChanged += (object s, EventArgs e) =>
			{
				group.Enabled = index == 0 || count.SelectedIndex == 1;
			};
			group.Enabled = num == 0 || sec.Tracks == 2;
		}
		Action defaultDirections = () =>
		{
			if (count.SelectedIndex == 1)
			{
				int num3 = ((odd.SelectedIndex != 1) ? 1 : 2);
				for (int i = 0; i < 2; i++)
				{
					dirs[i].SelectedIndex = ((int.Parse(nums[i].Text) != num3) ? 1 : 0);
				}
			}
		};
		odd.SelectedIndexChanged += (object s, EventArgs e) =>
		{
			defaultDirections();
		};
		count.SelectedIndexChanged += (object s, EventArgs e) =>
		{
			defaultDirections();
		};
		ComboBox[] array = nums;
		foreach (ComboBox comboBox in array)
		{
			comboBox.SelectedIndexChanged += (object s, EventArgs e) =>
			{
				defaultDirections();
			};
		}
		editor.Save(() =>
		{
			sec.Td2LocalLine = Ui.Required(local, "Oznaczenie szlaku TD2");
			sec.Td2Kind = ((kind.Text == "Nieustalony") ? "" : kind.Text);
			sec.Td2PeerName = ((sec.Td2Kind == "Zewnętrzny") ? "" : peer.Text.Trim());
			sec.Td2UsePeerName = usePeer.Checked;
			if (create)
			{
				Section section2 = sec;
				string text = ((sec.Td2Kind == "Zewnętrzny") ? "E:" : ((sec.Td2Kind == "Wewnętrzny") ? "I:" : "U:"));
				section2.Td2RouteKey = text + sec.Td2LocalLine.ToUpperInvariant();
			}
			sec.Name = (string.IsNullOrWhiteSpace(sec.Td2PeerName) ? sec.Td2LocalLine : sec.Td2PeerName);
			sec.Line = line.Text.Trim();
			sec.LineName = lineName.Text.Trim();
			sec.JournalName = (string.IsNullOrWhiteSpace(journal.Text) ? Td2SectionSettings.AutomaticTitle(sec) : journal.Text.Trim());
			sec.Td2CustomJournalName = custom && sec.JournalName != Td2SectionSettings.AutomaticTitle(sec);
			sec.Tracks = count.SelectedIndex + 1;
			sec.ArrivalEven = odd.SelectedIndex == 1;
			sec.Conducted = conducted.Checked;
			List<LineTrackSetting> list = new List<LineTrackSetting>();
			for (int i = 0; i < sec.Tracks; i++)
			{
				list.Add(new LineTrackSetting
				{
					Number = int.Parse(nums[i].Text),
					NormalIncoming = (dirs[i].SelectedIndex == 0),
					TrafficType = traffic[i].Text,
					Basis = bases[i].Text,
					BasisOut = outBases[i].Text
				});
			}
			if (list.Select((LineTrackSetting x) => x.Number).Distinct().Count() != sec.Tracks || list.Any((LineTrackSetting x) => x.Number > sec.Tracks))
			{
				throw new Exception("Podaj różne numery torów od 1 do liczby torów na szlaku.");
			}
			if (Enumerable.Count(list, (LineTrackSetting x) => x.NormalIncoming) > 1)
			{
				throw new Exception("Tory nie mogą mieć jednocześnie zasadniczego kierunku do naszego posterunku.");
			}
			sec.LineTracks = list;
			sec.NormalIncomingTrack = (from x in list
				where x.NormalIncoming
				select x.Number).FirstOrDefault();
			sec.RightTrackNumber = ((sec.Tracks == 2) ? new int?(int.Parse(nums[1].Text)) : new int?(1));
			sec.Basis = list[0].Basis;
		});
		if (editor.ShowDialog(this) == DialogResult.OK)
		{
			binding = true;
			if (create)
			{
				sections.Add(sec);
			}
			else
			{
				sections[sections.IndexOf(section)] = sec;
			}
			binding = false;
			sectionGrid.CurrentCell = sectionGrid.Rows[sections.IndexOf(sec)].Cells[0];
			RefreshTabChoices();
			BindDetails();
		}
	}
}

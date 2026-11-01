using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EdrLocal;

public sealed class AdministrationForm : ReferenceForm
{
	private class Item
	{
		public string Kind;

		public string Unit;

		public string Station;
	}

	private readonly ConfiguratorRepository repository;

	private readonly TreeView tree;

	private readonly Panel host;

	private readonly Label heading;

	private readonly Label status;

	private readonly ContextMenuStrip context;

	private readonly ImageList icons;

	private StationConfigurationForm editor;

	private bool rebuilding;

	private bool contextOnBackground;

	private Item Selected
	{
		get
		{
			if (tree.SelectedNode != null)
			{
				return tree.SelectedNode.Tag as Item;
			}
			return null;
		}
	}

	public AdministrationForm(ConfiguratorRepository value)
	{
		repository = value;
		Text = "EDR: Struktura organizacyjna i konfiguracja" + ((value.Simulator == "simrail") ? "" : " TD2");
		Font = new Font("Tahoma", 8.25f);
		StartPosition = FormStartPosition.CenterScreen;
		ClientSize = new Size(1510, 850);
		MinimumSize = new Size(1100, 720);
		status = new Label
		{
			Dock = DockStyle.Bottom,
			Height = 26,
			BorderStyle = BorderStyle.Fixed3D,
			TextAlign = ContentAlignment.MiddleLeft,
			Text = ((value.Simulator == "simrail") ? "Katalog posterunków: SimRail PL3" : "Konfigurator TD2")
		};
		SplitContainer split = new SplitContainer
		{
			Dock = DockStyle.Fill,
			FixedPanel = FixedPanel.Panel1,
			Panel1MinSize = 220
		};
		Label value2 = new Label
		{
			Text = "Struktura organizacyjna",
			Dock = DockStyle.Top,
			Height = 24,
			Font = new Font(Font, FontStyle.Bold),
			TextAlign = ContentAlignment.MiddleLeft
		};
		icons = StructureIcons.Create();
		tree = new TreeView
		{
			Dock = DockStyle.Fill,
			HideSelection = false,
			ShowNodeToolTips = true,
			ShowLines = false,
			ShowRootLines = true,
			ShowPlusMinus = true,
			FullRowSelect = true,
			ImageList = icons,
			ItemHeight = 23,
			Indent = 20
		};
		tree.HandleCreated += (object s, EventArgs e) =>
		{
			StructureIcons.Explorer(tree.Handle);
		};
		split.Panel1.Controls.Add(tree);
		split.Panel1.Controls.Add(value2);
		heading = new Label
		{
			Dock = DockStyle.Top,
			Height = 28,
			TextAlign = ContentAlignment.MiddleLeft,
			AutoEllipsis = true,
			Font = new Font(Font, FontStyle.Bold)
		};
		host = new Panel
		{
			Dock = DockStyle.Fill,
			AutoScroll = true
		};
		host.Resize += (object s, EventArgs e) =>
		{
			FitEditor();
		};
		split.Panel2.Controls.Add(host);
		split.Panel2.Controls.Add(heading);
		Controls.Add(split);
		Controls.Add(status);
		split.BringToFront();
		Shown += (object s, EventArgs e) =>
		{
			split.SplitterDistance = 300;
		};
		context = new ContextMenuStrip();
		context.Opening += (object s, CancelEventArgs e) =>
		{
			FillContextMenu();
		};
		tree.ContextMenuStrip = context;
		tree.NodeMouseClick += (object s, TreeNodeMouseClickEventArgs e) =>
		{
			if (e.Button == MouseButtons.Right)
			{
				tree.SelectedNode = e.Node;
			}
		};
		tree.MouseDown += (object s, MouseEventArgs e) =>
		{
			if (e.Button == MouseButtons.Right)
			{
				contextOnBackground = tree.GetNodeAt(e.Location) == null;
			}
		};
		tree.BeforeSelect += (object s, TreeViewCancelEventArgs e) =>
		{
			if (!rebuilding && editor != null && !editor.ConfirmPending(this))
			{
				e.Cancel = true;
			}
		};
		tree.AfterSelect += (object s, TreeViewEventArgs e) =>
		{
			if (!rebuilding)
			{
				ShowSelection();
			}
		};
		FormClosing += (object s, FormClosingEventArgs e) =>
		{
			if (editor != null && !editor.ConfirmPending(this))
			{
				e.Cancel = true;
				return;
			}
			try
			{
				SaveView();
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "Nie udało się zapisać widoku struktury: " + ex.Message, "EDR", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				e.Cancel = true;
			}
		};
		FormClosed += (object s, FormClosedEventArgs e) =>
		{
			ClearHost();
		};
		Disposed += (object s, EventArgs e) =>
		{
			icons.Dispose();
			context.Dispose();
		};
		BuildTree(null);
		if (repository.Warnings.Count > 0)
		{
			status.Text = "Pominięte pliki: " + string.Join("; ", repository.Warnings);
		}
	}

	private static TreeNode Node(TreeNode parent, string name, string text, string icon, Item item)
	{
		TreeNode treeNode = new TreeNode(text);
		treeNode.Name = name;
		treeNode.Tag = item;
		treeNode.ImageKey = icon;
		treeNode.SelectedImageKey = icon;
		treeNode.ToolTipText = text;
		TreeNode treeNode2 = treeNode;
		parent.Nodes.Add(treeNode2);
		return treeNode2;
	}

	private static IEnumerable<TreeNode> AllNodes(TreeNode root)
	{
		yield return root;
		foreach (TreeNode child in root.Nodes)
		{
			foreach (TreeNode item in AllNodes(child))
			{
				yield return item;
			}
		}
	}

	private IEnumerable<TreeNode> Nodes()
	{
		return tree.Nodes.Cast<TreeNode>().SelectMany(AllNodes);
	}

	private void SaveView()
	{
		repository.SaveTreeState(from x in Nodes()
			where x.IsExpanded
			select x.Name, (tree.SelectedNode == null) ? null : tree.SelectedNode.Name);
	}

	private void BuildTree(string select)
	{
		HashSet<string> expanded = new HashSet<string>((tree.Nodes.Count > 0) ? (from x in Nodes()
			where x.IsExpanded
			select x.Name) : (repository.Data.ExpandedNodes ?? new List<string> { "root" }));
		select = select ?? ((tree.SelectedNode == null) ? repository.Data.SelectedNode : tree.SelectedNode.Name);
		rebuilding = true;
		tree.BeginUpdate();
		try
		{
			tree.Nodes.Clear();
			TreeNode treeNode = new TreeNode("EDR — " + ((repository.Simulator == "simrail") ? "SimRail" : "TD2"));
			treeNode.Name = "root";
			treeNode.Tag = new Item
			{
				Kind = "root"
			};
			treeNode.ImageKey = "root";
			treeNode.SelectedImageKey = "root";
			TreeNode treeNode2 = treeNode;
			foreach (ConfigurationUnit department in from x in repository.Data.Units
				where x.Kind == "IZ"
				orderby x.Name
				select x)
			{
				TreeNode treeNode3 = Node(treeNode2, "u:" + department.Id, ConfiguratorRepository.UnitCaption(department), "iz", new Item
				{
					Kind = "IZ",
					Unit = department.Id
				});
				List<ConfigurationUnit> units = repository.Data.Units;
				Func<ConfigurationUnit, bool> predicate = (ConfigurationUnit x) => x.Kind == "ISE" && x.ParentId == department.Id;
				foreach (ConfigurationUnit section in from x in units.Where(predicate)
					orderby x.Name
					select x)
				{
					TreeNode treeNode4 = Node(treeNode3, "u:" + section.Id, ConfiguratorRepository.UnitCaption(section), "ise", new Item
					{
						Kind = "ISE",
						Unit = section.Id
					});
					TreeNode treeNode5 = Node(treeNode4, "p:" + section.Id, "Posterunki ruchu", "station", new Item
					{
						Kind = "stations",
						Unit = section.Id
					});
					List<ConfiguredStation> stations = repository.Data.Stations;
					Func<ConfiguredStation, bool> predicate2 = (ConfiguredStation x) => x.UnitId == section.Id && x.Registered != false;
					foreach (ConfiguredStation item in from x in stations.Where(predicate2)
						orderby x.Station.Name
						select x)
					{
						TreeNode treeNode6 = Node(treeNode5, "s:" + item.Key, StationCaption(item.Station), "station", new Item
						{
							Kind = "station",
							Unit = section.Id,
							Station = item.Key
						});
						treeNode6.ToolTipText = item.Station.Name + " [" + item.Key + "]" + (item.Station.SimulatorReviewed ? "" : " — do konfiguracji");
					}
				}
			}
			TreeNode[] array = treeNode2.Nodes.Cast<TreeNode>().ToArray();
			foreach (TreeNode treeNode7 in array)
			{
				treeNode7.Remove();
				tree.Nodes.Add(treeNode7);
			}
			foreach (TreeNode item2 in from x in Nodes()
				where expanded.Contains(x.Name)
				select x)
			{
				item2.Expand();
			}
			tree.SelectedNode = Nodes().FirstOrDefault((TreeNode x) => x.Name == select) ?? tree.Nodes.Cast<TreeNode>().FirstOrDefault();
			if (select != null && tree.SelectedNode != null)
			{
				tree.SelectedNode.EnsureVisible();
			}
		}
		finally
		{
			tree.EndUpdate();
			rebuilding = false;
		}
		ShowSelection();
	}

	private static string StationCaption(Station station)
	{
		return station.Name.ToUpper() + " (" + StationIdentity.Type(station) + ")";
	}

	private bool LeaveEditor()
	{
		if (editor != null)
		{
			return editor.ConfirmPending(this);
		}
		return true;
	}

	private void ClearHost()
	{
		if (editor != null)
		{
			editor.Dispose();
			editor = null;
		}
		Control[] array = host.Controls.Cast<Control>().ToArray();
		foreach (Control control in array)
		{
			control.Dispose();
		}
		host.Controls.Clear();
	}

	private void FitEditor()
	{
		if (editor != null && !editor.IsDisposed)
		{
			editor.Size = new Size(Math.Max(1120, host.ClientSize.Width - SystemInformation.VerticalScrollBarWidth), Math.Max(700, host.ClientSize.Height - SystemInformation.HorizontalScrollBarHeight));
		}
	}

	private void ShowSelection()
	{
		ClearHost();
		Item item = Selected;
		if (item == null)
		{
			Item item2 = new Item();
			item2.Kind = "root";
			item = item2;
		}
		Item item3 = item;
		if (item3.Kind == "station")
		{
			string key = item3.Station;
			ConfiguredStation configuredStation = repository.Data.Stations.First((ConfiguredStation x) => x.Key == key);
			heading.Text = "Posterunek ruchu: " + configuredStation.Station.Name + " | " + repository.SectionCaption(configuredStation.UnitId);
			editor = new StationConfigurationForm(repository.DraftEngine(key), (Station station) =>
			{
				repository.SaveStation(key, station);
			})
			{
				TopLevel = false,
				FormBorderStyle = FormBorderStyle.None,
				MinimumSize = Size.Empty,
				Location = Point.Empty
			};
			editor.DraftSaved += () =>
			{
				ConfiguredStation configuredStation2 = repository.Data.Stations.First((ConfiguredStation x) => x.Key == key);
				tree.SelectedNode.Text = StationCaption(configuredStation2.Station);
				tree.SelectedNode.ToolTipText = configuredStation2.Station.Name + " [" + key + "]";
				heading.Text = "Posterunek ruchu: " + configuredStation2.Station.Name + " | " + repository.SectionCaption(configuredStation2.UnitId);
				status.Text = "Zapisano konfigurację " + configuredStation2.Station.Name + ". " + ((repository.Simulator == "simrail") ? "EDR zastosuje ją przy kolejnym otwarciu posterunku." : "Profil zapisany lokalnie.");
			};
			editor.DraftDiscarded += ShowSelection;
			host.Controls.Add(editor);
			FitEditor();
			editor.Show();
		}
		else
		{
			heading.Text = ((tree.SelectedNode == null) ? "Struktura organizacyjna" : tree.SelectedNode.Text);
		}
	}

	private void FillContextMenu()
	{
		context.Items.Clear();
		Item item = (contextOnBackground ? null : Selected);
		contextOnBackground = false;
		if (item == null || item.Kind == "root")
		{
			context.Items.Add(Ui.Menu("Dodaj zakład IZ…", AddDepartment));
			return;
		}
		if (item.Kind == "IZ")
		{
			context.Items.Add(Ui.Menu("Dodaj sekcję ISE…", AddSection));
		}
		if (item.Kind == "ISE" || item.Kind == "stations")
		{
			context.Items.Add(Ui.Menu((repository.Simulator == "simrail") ? "Dodaj posterunki z PL3…" : "Dodaj posterunki…", RegisterStations));
		}
		if (item.Kind == "IZ" || item.Kind == "ISE")
		{
			context.Items.Add(Ui.Menu("Zmień nazwę…", Rename));
			if (item.Kind == "ISE")
			{
				ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem("Przenieś do zakładu");
				foreach (ConfigurationUnit item2 in from x in repository.Data.Units
					where x.Kind == "IZ" && x.Id != repository.Data.Units.First((ConfigurationUnit u) => u.Id == item.Unit).ParentId
					orderby x.Name
					select x)
				{
					string id = item2.Id;
					toolStripMenuItem.DropDownItems.Add(Ui.Menu(ConfiguratorRepository.UnitCaption(item2), () =>
					{
						MoveSection(id);
					}));
				}
				toolStripMenuItem.Enabled = toolStripMenuItem.DropDownItems.Count > 0;
				context.Items.Add(toolStripMenuItem);
			}
			context.Items.Add(new ToolStripSeparator());
			context.Items.Add(Ui.Menu((item.Kind == "IZ") ? "Usuń zakład IZ…" : "Usuń sekcję ISE…", RemoveUnit));
		}
		if (!(item.Kind == "station"))
		{
			return;
		}
		context.Items.Add(Ui.Menu("Konfiguruj posterunek", () =>
		{
			if (editor != null)
			{
				editor.Focus();
			}
			else
			{
				ShowSelection();
			}
		}));
		ToolStripMenuItem toolStripMenuItem2 = new ToolStripMenuItem("Przenieś do sekcji");
		foreach (ConfigurationUnit item3 in from x in repository.Data.Units
			where x.Kind == "ISE" && x.Id != item.Unit
			orderby repository.SectionCaption(x.Id)
			select x)
		{
			string id2 = item3.Id;
			toolStripMenuItem2.DropDownItems.Add(Ui.Menu(repository.SectionCaption(item3.Id), () =>
			{
				MoveStation(id2);
			}));
		}
		toolStripMenuItem2.Enabled = toolStripMenuItem2.DropDownItems.Count > 0;
		context.Items.Add(toolStripMenuItem2);
		context.Items.Add(new ToolStripSeparator());
		context.Items.Add(Ui.Menu("Usuń ze struktury…", Unregister));
	}

	private string Department()
	{
		Item item = Selected;
		if (item != null && item.Kind == "IZ")
		{
			return item.Unit;
		}
		if (item != null && item.Unit != null)
		{
			ConfigurationUnit configurationUnit = repository.Data.Units.First((ConfigurationUnit x) => x.Id == item.Unit);
			if (configurationUnit.Kind == "ISE")
			{
				return configurationUnit.ParentId;
			}
		}
		throw new Exception("Wybierz w drzewie zakład IZ.");
	}

	private string Section()
	{
		Item item = Selected;
		if (item != null && item.Unit != null && repository.Data.Units.Any((ConfigurationUnit x) => x.Id == item.Unit && x.Kind == "ISE"))
		{
			return item.Unit;
		}
		throw new Exception("Wybierz w drzewie sekcję ISE.");
	}

	private void AddDepartment()
	{
		if (!LeaveEditor())
		{
			return;
		}
		using Editor editor = new Editor("Dodaj zakład IZ", 580, 210);
		TextBox name = editor.Add("Nazwa zakładu", Ui.Text("", multiline: false), 35);
		string id = null;
		editor.Save(() =>
		{
			id = repository.AddDepartment(Ui.Required(name, "Nazwa zakładu"));
		});
		if (editor.ShowDialog(this) == DialogResult.OK)
		{
			BuildTree("u:" + id);
		}
	}

	private void AddSection()
	{
		string parent = Department();
		if (!LeaveEditor())
		{
			return;
		}
		using Editor editor = new Editor("Dodaj sekcję ISE — " + ConfiguratorRepository.UnitCaption(repository.Data.Units.First((ConfigurationUnit x) => x.Id == parent)), 620, 210);
		TextBox name = editor.Add("Nazwa sekcji", Ui.Text("", multiline: false), 35);
		string id = null;
		editor.Save(() =>
		{
			id = repository.AddSection(parent, Ui.Required(name, "Nazwa sekcji"));
		});
		if (editor.ShowDialog(this) == DialogResult.OK)
		{
			BuildTree("u:" + id);
		}
	}

	private void RegisterStations()
	{
		string text = Section();
		if (!LeaveEditor())
		{
			return;
		}
		using StationRegistrationForm stationRegistrationForm = new StationRegistrationForm(repository, text);
		if (stationRegistrationForm.ShowDialog(this) == DialogResult.OK)
		{
			BuildTree((stationRegistrationForm.SelectedKeys.Count > 0) ? ("s:" + stationRegistrationForm.SelectedKeys[0]) : ("u:" + text));
		}
	}

	private void Rename()
	{
		ConfigurationUnit unit = repository.Data.Units.First((ConfigurationUnit x) => x.Id == Selected.Unit);
		if (!LeaveEditor())
		{
			return;
		}
		using Editor editor = new Editor("Zmień nazwę — " + unit.Kind, 580, 210);
		TextBox name = editor.Add("Nazwa", Ui.Text(unit.Name, multiline: false), 35);
		editor.Save(() =>
		{
			repository.RenameUnit(unit.Id, Ui.Required(name, "Nazwa"));
		});
		if (editor.ShowDialog(this) == DialogResult.OK)
		{
			BuildTree("u:" + unit.Id);
		}
	}

	private void MoveSection(string id)
	{
		string unit = Selected.Unit;
		if (LeaveEditor())
		{
			repository.MoveSection(unit, id);
			BuildTree("u:" + unit);
			status.Text = "Przeniesiono sekcję. Struktura została zapisana.";
		}
	}

	private void MoveStation(string id)
	{
		string station = Selected.Station;
		if (LeaveEditor())
		{
			repository.MoveStation(station, id);
			BuildTree("s:" + station);
			status.Text = "Przeniesiono posterunek. Konfiguracja pozostała zachowana.";
		}
	}

	private void RemoveUnit()
	{
		string id = Selected.Unit;
		if (!LeaveEditor())
		{
			return;
		}
		ConfigurationUnit configurationUnit = repository.Data.Units.First((ConfigurationUnit x) => x.Id == id);
		string parentId = configurationUnit.ParentId;
		ConfigurationRemoval configurationRemoval = repository.PreviewUnitRemoval(id);
		using (BranchRemovalForm branchRemovalForm = new BranchRemovalForm(configurationRemoval))
		{
			if (branchRemovalForm.ShowDialog(this) != DialogResult.OK)
			{
				return;
			}
		}
		repository.RemoveUnit(id);
		BuildTree((parentId == null) ? "root" : ("u:" + parentId));
		status.Text = "Usunięto gałąź struktury: " + configurationRemoval.Caption + ". Profile i dzienniki pozostały zachowane.";
	}

	private void Unregister()
	{
		string station = Selected.Station;
		string unit = Selected.Unit;
		if (LeaveEditor() && MessageBox.Show(this, "Usunąć posterunek z tej sekcji?\nJego konfiguracja i dzienniki pozostaną zachowane.", "Struktura organizacyjna", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
		{
			repository.UnregisterStation(station);
			BuildTree("p:" + unit);
		}
	}
}

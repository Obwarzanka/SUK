using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace EdrLocal;

internal static class BranchRemoval31Tests
{
	private static IEnumerable<Control> Controls(Control c)
	{
		yield return c;
		foreach (Control child in c.Controls)
		{
			foreach (Control item in Controls(child))
			{
				yield return item;
			}
		}
	}

	private static IEnumerable<TreeNode> Nodes(TreeNode root)
	{
		yield return root;
		foreach (TreeNode child in root.Nodes)
		{
			foreach (TreeNode item in Nodes(child))
			{
				yield return item;
			}
		}
	}

	private static T Field<T>(object obj, string name)
	{
		return (T)obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
	}

	private static string Confirm(Action action, bool remove, string capture)
	{
		string text = null;
		Timer timer = new Timer
		{
			Interval = 80
		};
		try
		{
			timer.Tick += (object s, EventArgs e) =>
			{
				BranchRemovalForm branchRemovalForm = Application.OpenForms.OfType<BranchRemovalForm>().LastOrDefault();
				if (branchRemovalForm != null)
				{
					timer.Stop();
					text = string.Join("\n", from x in Controls(branchRemovalForm).OfType<Label>()
						select x.Text);
					if (capture != null)
					{
						Ui.Capture(branchRemovalForm, capture);
					}
					Controls(branchRemovalForm).OfType<Button>().First((Button x) => x.Text == (remove ? "Usuń ze struktury" : "Anuluj")).PerformClick();
				}
			};
			timer.Start();
			action();
		}
		finally
		{
			if (timer != null)
			{
				((IDisposable)timer).Dispose();
			}
		}
		if (text == null)
		{
			throw new Exception("Nie pokazano potwierdzenia usuwania gałęzi.");
		}
		return text;
	}

	public static void Run(string folder, List<string> log)
	{
		Action<bool, string> action = (bool ok, string text6) =>
		{
			if (!ok)
			{
				throw new Exception("FAIL: " + text6);
			}
			log.Add("PASS: " + text6);
		};
		string text = Path.Combine(folder, "cascade");
		ConfiguratorRepository repo = new ConfiguratorRepository("simrail", text);
		repo.Load();
		foreach (ConfigurationUnit item in repo.Data.Units.Where((ConfigurationUnit x) => x.Kind == "IZ").ToList())
		{
			repo.RemoveUnit(item.Id);
		}
		string a = repo.AddDepartment("Zakład A");
		string b = repo.AddDepartment("Zakład B");
		string a2 = repo.AddSection(a, "Sekcja A1");
		string a3 = repo.AddSection(a, "Sekcja A2");
		string b2 = repo.AddSection(b, "Sekcja B1");
		Store store = new Store(Path.Combine(text, "seed"), transient: true);
		store.Load();
		string[] array = new string[4] { "101", "102", "103", "104" };
		foreach (string key in array)
		{
			Station station = Json.Clone(store.State.Stations[0]);
			station.Id = "SR-" + key;
			station.Name = "Posterunek " + key;
			station.SimRailPointId = key;
			StationSettings.Normalize(station);
			ConfiguratorRepository configuratorRepository = repo;
			string unit;
			if (key == "102")
			{
				unit = a3;
			}
			else
			{
				unit = ((key == "103") ? b2 : a2);
			}
			configuratorRepository.Register(unit, key, station);
			repo.SaveStation(key, repo.Data.Stations.First((ConfiguredStation x) => x.Key == key).Station);
		}
		repo.UnregisterStation("104");
		repo.SaveTreeState(new string[4]
		{
			"u:" + a,
			"u:" + a2,
			"p:" + a2,
			"u:" + b
		}, "s:101");
		Dictionary<string, byte[]> source = repo.Data.Stations.ToDictionary((ConfiguredStation x) => x.Key, (ConfiguredStation x) => File.ReadAllBytes(Path.Combine(repo.Profiles, x.Key + ".json")));
		Store store2 = new Store(Path.Combine(text, "dzienniki"));
		store2.Load();
		store2.Commit((AppState s) =>
		{
			s.Stations = new List<Station> { Json.Clone(repo.Data.Stations.First((ConfiguredStation x) => x.Key == "101").Station) };
			s.Trains.Clear();
			s.Entries.Clear();
			s.Duties.Clear();
			s.Services.Clear();
			s.History.Clear();
			s.Entries.Add(new JournalEntry
			{
				Id = "history",
				StationId = s.Stations[0].Id,
				SectionId = s.Stations[0].Sections[0].Id,
				Time = DateTime.Now,
				Type = "Wpis",
				Text = "Zachowaj historię"
			});
		});
		byte[] second = File.ReadAllBytes(store2.FilePath);
		string text2 = Json.Text(repo.Data);
		ConfigurationRemoval configurationRemoval = repo.PreviewUnitRemoval(a);
		action(configurationRemoval.SectionCount == 2 && configurationRemoval.StationCount == 2 && configurationRemoval.StationKeys.Contains("104"), "Zakres usunięcia IZ obejmuje wszystkie sekcje i aktywne posterunki oraz odpina starsze nieaktywne przypisania.");
		action(text2 == Json.Text(repo.Data), "Sam podgląd usuwania nie zmienia rejestru.");
		AdministrationForm form = new AdministrationForm(repo);
		try
		{
			form.Show();
			Application.DoEvents();
			TreeView treeView = Field<TreeView>(form, "tree");
			treeView.SelectedNode = treeView.Nodes.Cast<TreeNode>().SelectMany(Nodes).First((TreeNode x) => x.Name == "u:" + a);
			action(!Controls(form).OfType<MenuStrip>().Any() && !Controls(form).OfType<Button>().Any(), "Panel struktury nie ma menu Aplikacja ani przycisków dodawania; pozostaje obsługa kontekstowa.");
			Action action2 = () =>
			{
				typeof(AdministrationForm).GetMethod("FillContextMenu", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
			};
			action2();
			ContextMenuStrip menu = Field<ContextMenuStrip>(form, "context");
			string text3 = Confirm(() =>
			{
				menu.Items.OfType<ToolStripMenuItem>().First((ToolStripMenuItem x) => x.Text == "Usuń zakład IZ…").PerformClick();
			}, remove: false, Path.Combine(folder, "usun-galaz-potwierdzenie.png"));
			action(text3.Contains("Sekcje ISE: 2") && text3.Contains("Posterunki ruchu: 2"), "Potwierdzenie podaje zakres usunięcia całego zakładu.");
			action(Json.Text(repo.Data) == text2, "Anulowanie usunięcia IZ zachowuje całą strukturę i przypisania.");
			action2();
			Confirm(() =>
			{
				menu.Items.OfType<ToolStripMenuItem>().First((ToolStripMenuItem x) => x.Text == "Usuń zakład IZ…").PerformClick();
			}, remove: true, null);
			action(!repo.Data.Units.Any((ConfigurationUnit x) => new string[3] { a, a2, a3 }.Contains(x.Id)), "Usunięcie IZ usuwa jednocześnie jego sekcje ISE.");
			action(repo.Data.Stations.Where((ConfiguredStation x) => new string[3] { "101", "102", "104" }.Contains(x.Key)).All((ConfiguredStation x) => x.UnitId == null && x.Registered == false), "Usunięcie IZ odpina wszystkie podległe posterunki ze struktury.");
			action(repo.Data.Units.Any((ConfigurationUnit x) => x.Id == b) && repo.Data.Units.Any((ConfigurationUnit x) => x.Id == b2) && repo.Data.Stations.First((ConfiguredStation x) => x.Key == "103").Registered != false, "Usunięcie zakładu nie zmienia sąsiedniej gałęzi.");
			action(!repo.Data.ExpandedNodes.Any((string x) => x == "u:" + a || x == "u:" + a2 || x == "p:" + a2) && repo.Data.SelectedNode == null, "Usuwanie czyści zapamiętane pozycje skasowanej gałęzi.");
			action(!treeView.Nodes.Cast<TreeNode>().SelectMany(Nodes).Any((TreeNode x) => x.Name == "u:" + a || x.Name == "s:101"), "Drzewo od razu usuwa całą gałąź bez ponownego uruchomienia.");
			Ui.Capture(form, Path.Combine(folder, "struktura-po-usunieciu.png"));
			treeView.SelectedNode = treeView.Nodes.Cast<TreeNode>().SelectMany(Nodes).First((TreeNode x) => x.Name == "u:" + b2);
			action2();
			Confirm(() =>
			{
				menu.Items.OfType<ToolStripMenuItem>().First((ToolStripMenuItem x) => x.Text == "Usuń sekcję ISE…").PerformClick();
			}, remove: true, null);
			action(repo.Data.Units.Count == 1 && repo.Data.Units[0].Id == b && repo.Data.Stations.First((ConfiguredStation x) => x.Key == "103").Registered == false, "Usunięcie ISE usuwa jej posterunki i zachowuje nadrzędny IZ.");
			form.Close();
		}
		finally
		{
			if (form != null)
			{
				((IDisposable)form).Dispose();
			}
		}
		action(source.All((KeyValuePair<string, byte[]> x) => File.ReadAllBytes(Path.Combine(repo.Profiles, x.Key + ".json")).SequenceEqual(x.Value)), "Usuwanie struktury nie modyfikuje zapisanych profili posterunków.");
		action(File.ReadAllBytes(store2.FilePath).SequenceEqual(second), "Usuwanie struktury nie zmienia pliku istniejącego dziennika i jego wpisów.");
		ConfiguratorRepository configuratorRepository2 = new ConfiguratorRepository("simrail", text);
		configuratorRepository2.Load();
		action(configuratorRepository2.Data.Units.Count == 1 && configuratorRepository2.Data.Units[0].Id == b && configuratorRepository2.Data.Stations.All((ConfiguredStation x) => x.UnitId == null && x.Registered == false), "Restart zachowuje usunięcie i nie odtwarza skasowanych gałęzi z profili.");
		string text4 = Json.Text(configuratorRepository2.Data);
		bool flag = false;
		try
		{
			configuratorRepository2.RemoveUnit("nieistniejący");
		}
		catch
		{
			flag = true;
		}
		action(flag && Json.Text(configuratorRepository2.Data) == text4, "Nieprawidłowe usunięcie nie zmienia danych.");
		configuratorRepository2.RemoveUnit(b);
		configuratorRepository2.Load();
		action(configuratorRepository2.Data.Units.Count == 0 && configuratorRepository2.Data.Stations.Count == 4, "Można usunąć ostatni IZ i zachować profile poza strukturą.");
		string department = configuratorRepository2.AddDepartment("Nowy zakład");
		string text5 = configuratorRepository2.AddSection(department, "Nowa sekcja");
		configuratorRepository2.Register(text5, "101", Json.Clone(configuratorRepository2.Data.Stations.First((ConfiguredStation x) => x.Key == "101").Station));
		action(configuratorRepository2.Data.Stations.First((ConfiguredStation x) => x.Key == "101").UnitId == text5 && configuratorRepository2.Data.Stations.Count == 4, "Zachowany posterunek można ponownie dodać bez duplikowania profilu.");
	}
}

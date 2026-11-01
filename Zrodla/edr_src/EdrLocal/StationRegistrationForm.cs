using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EdrLocal;

public sealed class StationRegistrationForm : ReferenceForm
{
	public const string CatalogServer = "pl3";

	private readonly ConfiguratorRepository repository;

	private readonly string unit;

	private readonly ISimulatorFeed feed;

	private readonly ListBox available;

	private readonly ListBox assigned;

	private readonly Label status;

	private readonly TextBox search;

	private readonly Button refresh;

	private readonly List<ConfiguredStation> pool = new List<ConfiguredStation>();

	private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

	private bool loading;

	public List<string> SelectedKeys { get; private set; }

	public StationRegistrationForm(ConfiguratorRepository repository, string unit)
		: this(repository, unit, new SimRailProvider(), autoLoad: true)
	{
	}

	public StationRegistrationForm(ConfiguratorRepository value, string unitId, ISimulatorFeed source, bool autoLoad)
	{
		repository = value;
		unit = unitId;
		feed = source;
		SelectedKeys = new List<string>();
		Text = "Dodaj posterunki — " + repository.SectionCaption(unit);
		ClientSize = new Size(1040, 600);
		MinimumSize = new Size(820, 480);
		StartPosition = FormStartPosition.CenterParent;
		Font = ReferenceTheme.Dialog;
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 3,
			RowCount = 2,
			Padding = new Padding(12)
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62f));
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		tableLayoutPanel.Controls.Add(new Label
		{
			Text = ((repository.Simulator == "simrail") ? "Dostępne posterunki — PL3 i lokalne profile" : "Dostępne profile"),
			Dock = DockStyle.Fill
		}, 0, 0);
		tableLayoutPanel.Controls.Add(new Label
		{
			Text = "Posterunki przypisane do tej sekcji",
			Dock = DockStyle.Fill
		}, 2, 0);
		available = List();
		assigned = List();
		tableLayoutPanel.Controls.Add(available, 0, 1);
		tableLayoutPanel.Controls.Add(assigned, 2, 1);
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.TopDown,
			Padding = new Padding(0, 100, 0, 0)
		};
		bool[] array = new bool[2] { true, false };
		foreach (bool flag in array)
		{
			bool choice = flag;
			Button button = Ui.Button(flag ? "→" : "←", () =>
			{
				Transfer(choice);
			});
			button.AutoSize = false;
			button.MinimumSize = Size.Empty;
			button.Size = new Size(46, 32);
			flowLayoutPanel.Controls.Add(button);
		}
		tableLayoutPanel.Controls.Add(flowLayoutPanel, 1, 1);
		available.DoubleClick += (object s, EventArgs e) =>
		{
			Transfer(add: true);
		};
		assigned.DoubleClick += (object s, EventArgs e) =>
		{
			Transfer(add: false);
		};
		TableLayoutPanel tableLayoutPanel2 = new TableLayoutPanel
		{
			Dock = DockStyle.Top,
			Height = 76,
			ColumnCount = 3,
			RowCount = 2,
			Padding = new Padding(12, 7, 12, 0)
		};
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66f));
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190f));
		tableLayoutPanel2.Controls.Add(new Label
		{
			Text = "Szukaj:",
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft
		}, 0, 0);
		search = new TextBox
		{
			Dock = DockStyle.Fill
		};
		search.TextChanged += (object s, EventArgs e) =>
		{
			BuildAvailable();
		};
		tableLayoutPanel2.Controls.Add(search, 1, 0);
		refresh = Ui.Button((repository.Simulator == "simrail") ? "Odśwież listę z PL3" : "Dodaj posterunek", () =>
		{
			if (repository.Simulator == "simrail")
			{
				LoadStations();
			}
			else
			{
				Manual();
			}
		});
		tableLayoutPanel2.Controls.Add(refresh, 2, 0);
		status = new Label
		{
			Dock = DockStyle.Fill,
			AutoEllipsis = true,
			Text = "Przenieś posterunki strzałką → i kliknij Zapisz."
		};
		tableLayoutPanel2.Controls.Add(status, 0, 1);
		tableLayoutPanel2.SetColumnSpan(status, 3);
		FlowLayoutPanel flowLayoutPanel2 = new FlowLayoutPanel
		{
			Dock = DockStyle.Bottom,
			Height = 40,
			FlowDirection = FlowDirection.RightToLeft
		};
		flowLayoutPanel2.Controls.Add(Ui.Button("Anuluj", () =>
		{
			Close();
		}));
		flowLayoutPanel2.Controls.Add(Ui.Button("Zapisz", Save));
		Controls.Add(tableLayoutPanel);
		Controls.Add(flowLayoutPanel2);
		Controls.Add(tableLayoutPanel2);
		tableLayoutPanel.BringToFront();
		foreach (ConfiguredStation item in from x in repository.Data.Stations
			where x.UnitId == unit && x.Registered != false
			orderby x.Station.Name
			select x)
		{
			assigned.Items.Add(Json.Clone(item));
		}
		AddPool(repository.Data.Stations.Where((ConfiguredStation x) => x.Registered == false || x.UnitId == unit));
		ConfigurationCatalogCache configurationCatalogCache = repository.ReadCatalog();
		if (configurationCatalogCache != null)
		{
			AddPool(configurationCatalogCache.Stations);
			status.Text = "Zapamiętana lista PL3: " + configurationCatalogCache.SavedUtc.ToLocalTime().ToString("dd-MM-yyyy HH:mm") + ". Możesz od razu wybrać posterunki.";
		}
		BuildAvailable();
		if (autoLoad && repository.Simulator == "simrail")
		{
			Shown += (object s, EventArgs e) =>
			{
				LoadStations();
			};
		}
		FormClosed += (object s, FormClosedEventArgs e) =>
		{
			cancellation.Cancel();
		};
	}

	private static ListBox List()
	{
		ListBox listBox = new ListBox();
		listBox.Dock = DockStyle.Fill;
		listBox.SelectionMode = SelectionMode.MultiExtended;
		listBox.HorizontalScrollbar = true;
		listBox.FormattingEnabled = true;
		ListBox listBox2 = listBox;
		listBox2.Format += (object s, ListControlConvertEventArgs e) =>
		{
			if (e.ListItem is ConfiguredStation configuredStation)
			{
				e.Value = configuredStation.Station.Name + " [" + configuredStation.Key + "]";
			}
		};
		return listBox2;
	}

	private bool Elsewhere(string key)
	{
		return repository.Data.Stations.Any((ConfiguredStation x) => x.Key == key && x.Registered != false && x.UnitId != unit);
	}

	private void AddPool(IEnumerable<ConfiguredStation> candidates)
	{
		foreach (ConfiguredStation candidate in candidates)
		{
			if (!Elsewhere(candidate.Key))
			{
				ConfiguredStation configuredStation = repository.Data.Stations.FirstOrDefault((ConfiguredStation x) => x.Key == candidate.Key);
				ConfiguredStation source = Json.Clone(configuredStation ?? candidate);
				int num = pool.FindIndex((ConfiguredStation x) => x.Key == source.Key);
				if (num < 0)
				{
					pool.Add(source);
				}
				else
				{
					pool[num] = source;
				}
			}
		}
	}

	private void BuildAvailable()
	{
		string query = (search.Text ?? "").Trim();
		HashSet<string> selected = new HashSet<string>(from ConfiguredStation x in assigned.Items
			select x.Key);
		available.BeginUpdate();
		try
		{
			available.Items.Clear();
			foreach (ConfiguredStation item in from x in pool
				where !selected.Contains(x.Key) && !Elsewhere(x.Key) && (query.Length == 0 || x.Station.Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0 || x.Key.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
				orderby x.Station.Name
				select x)
			{
				available.Items.Add(item);
			}
		}
		finally
		{
			available.EndUpdate();
		}
	}

	private void Transfer(bool add)
	{
		if (add)
		{
			ConfiguredStation[] array = available.SelectedItems.Cast<ConfiguredStation>().ToArray();
			foreach (ConfiguredStation item in array)
			{
				assigned.Items.Add(item);
			}
		}
		else
		{
			ConfiguredStation[] array2 = assigned.SelectedItems.Cast<ConfiguredStation>().ToArray();
			foreach (ConfiguredStation value in array2)
			{
				assigned.Items.Remove(value);
			}
		}
		BuildAvailable();
	}

	private async void LoadStations()
	{
		if (loading)
		{
			return;
		}
		loading = true;
		refresh.Enabled = false;
		try
		{
			status.Text = "Pobieranie listy posterunków i propozycji konfiguracji z PL3…";
			SimRailSnapshot snapshot = await feed.SnapshotAsync("pl3", cancellation.Token);
			if (!IsDisposed)
			{
				if (!string.Equals(snapshot.ServerCode, "pl3", StringComparison.OrdinalIgnoreCase))
				{
					throw new InvalidOperationException("Odpowiedź dotyczy innego serwera niż PL3.");
				}
				List<ConfiguredStation> candidates = await Task.Run(() => ConfigurationCatalog.Build(snapshot, null, cancellation.Token), cancellation.Token);
				if (!IsDisposed)
				{
					repository.SaveCatalog(candidates);
					AddPool(candidates);
					BuildAvailable();
					status.Text = "Lista PL3 zapamiętana lokalnie. Posterunków w katalogu: " + candidates.Count + ". Wybierz pozycje strzałką →.";
				}
			}
		}
		catch (Exception ex)
		{
			if (!IsDisposed)
			{
				status.Text = "Nie udało się odświeżyć PL3: " + ex.Message + " Zachowano lokalną listę i wybrane posterunki.";
			}
		}
		finally
		{
			loading = false;
			if (!IsDisposed)
			{
				refresh.Enabled = true;
			}
		}
	}

	private void Save()
	{
		List<ConfiguredStation> list = assigned.Items.Cast<ConfiguredStation>().ToList();
		repository.SetRegistration(unit, list);
		SelectedKeys = list.Select((ConfiguredStation x) => x.Key).ToList();
		DialogResult = DialogResult.OK;
		Close();
	}

	private void Manual()
	{
		using Editor editor = new Editor("Dodaj posterunek TD2", 650, 250);
		TextBox name = editor.Add("Nazwa", Ui.Text("", multiline: false), 35);
		ComboBox type = editor.Add("Typ posterunku", Ui.Combo(StationIdentity.Types, StationIdentity.Types[0], editable: false), 35);
		ConfiguredStation item = null;
		editor.Save(() =>
		{
			string text = Guid.NewGuid().ToString("N");
			Station station = new Station
			{
				Id = "cfg-" + text,
				Name = Ui.Required(name, "Nazwa"),
				PostType = type.Text,
				Tracks = new List<string> { "1" },
				Checks = new List<string>(),
				Sections = new List<Section>()
			};
			StationSettings.Normalize(station);
			item = new ConfiguredStation
			{
				Key = text,
				Station = station,
				UnitId = unit
			};
		});
		if (editor.ShowDialog(this) == DialogResult.OK && item != null)
		{
			pool.Add(item);
			assigned.Items.Add(item);
			BuildAvailable();
		}
	}
}

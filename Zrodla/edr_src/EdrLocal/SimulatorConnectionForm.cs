using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EdrLocal;

public sealed class SimulatorConnectionForm : ReferenceForm
{
	private readonly ISimulatorFeed feed;

	private readonly ConfiguratorRepository organization;

	private readonly string simulatorDataRoot;

	private readonly CancellationTokenSource cancel = new CancellationTokenSource();

	private readonly ComboBox servers;

	private readonly ListBox available;

	private readonly ListBox selected;

	private readonly TextBox operatorName;

	private readonly TextBox link;

	private readonly Label status;

	private readonly Button load;

	private readonly Button connect;

	private readonly Button add;

	private readonly Button remove;

	private readonly System.Windows.Forms.Timer linkTimer = new System.Windows.Forms.Timer
	{
		Interval = 350
	};

	private SimRailSnapshot snapshot;

	private bool loading;

	private bool serversReady;

	private bool linkPending;

	private string appliedLink;

	private bool Td2 => feed.ProviderId == "TD2";

	public bool NeedsConfiguration { get; private set; }

	public Engine ConnectedEngine { get; private set; }

	public SimulatorSessionLock DatabaseLock { get; private set; }

	private ConnectionOrganizationSelection ExistingOrganization(string point)
	{
		ConfiguredStation record = organization.Data.Stations.FirstOrDefault((ConfiguredStation x) => x.Key == point && x.Registered != false);
		if (record == null)
		{
			return null;
		}
		ConfigurationUnit configurationUnit = organization.Data.Units.FirstOrDefault((ConfigurationUnit x) => x.Id == record.UnitId && x.Kind == "ISE");
		if (configurationUnit != null)
		{
			return new ConnectionOrganizationSelection(configurationUnit.ParentId, configurationUnit.Id, existing: true);
		}
		return null;
	}

	private ConnectionOrganizationSelection SelectConnectionOrganization(SimRailLocation location)
	{
		organization.Load();
		ConnectionOrganizationSelection connectionOrganizationSelection = ExistingOrganization(location.Id);
		if (connectionOrganizationSelection != null)
		{
			return connectionOrganizationSelection;
		}
		using OrganizationAssignmentForm organizationAssignmentForm = new OrganizationAssignmentForm(organization, location.Name);
		return (organizationAssignmentForm.ShowDialog(this) == DialogResult.OK) ? organizationAssignmentForm.Selection : null;
	}

	private void PersistConnectionOrganization(Engine engine, ConnectionOrganizationSelection choice)
	{
		organization.Load();
		string point = engine.Station.SimRailPointId;
		if (ExistingOrganization(point) != null)
		{
			engine.SetOrganizationSnapshot(organization);
			return;
		}
		if (choice.Existing)
		{
			throw new InvalidOperationException("Przypisanie IZ / ISE zostało usunięte. Otwórz posterunek ponownie, aby wybrać jednostki.");
		}
		using (new ConfigurationSessionLock(organization))
		{
			organization.Load();
			if (ExistingOrganization(point) == null)
			{
				if (!organization.Data.Units.Any((ConfigurationUnit x) => x.Id == choice.SectionId && x.Kind == "ISE" && x.ParentId == choice.DepartmentId))
				{
					throw new InvalidOperationException("Wybrana sekcja IZ / ISE została zmieniona lub usunięta. Otwórz posterunek ponownie.");
				}
				ConfiguredStation configuredStation = organization.Data.Stations.FirstOrDefault((ConfiguredStation x) => x.Key == point);
				if (configuredStation == null)
				{
					organization.Register(choice.SectionId, point, engine.Station);
				}
				else
				{
					organization.MoveStation(point, choice.SectionId);
				}
			}
		}
		engine.SetOrganizationSnapshot(organization);
	}

	public SimulatorConnectionForm()
		: this(new SimRailProvider(), new ConfiguratorRepository("simrail", null), null)
	{
	}

	public SimulatorConnectionForm(string provider)
		: this((provider == "td2") ? ((ISimulatorFeed)new Td2Provider()) : ((ISimulatorFeed)new SimRailProvider()), new ConfiguratorRepository(provider, null), null)
	{
	}

	public SimulatorConnectionForm(ISimulatorFeed source, ConfiguratorRepository registry, string dataRoot)
	{
		feed = source;
		organization = registry;
		if (registry.Simulator != (Td2 ? "td2" : "simrail"))
		{
			throw new ArgumentException("Wybierz strukturę właściwego symulatora.");
		}
		simulatorDataRoot = dataRoot ?? Path.Combine(DataLocations.Root, Td2 ? "td2" : "simrail");
		Text = "Połączenie z " + (Td2 ? "TD2" : "SimRail");
		ClientSize = new Size(960, 620);
		MinimumSize = new Size(860, 600);
		StartPosition = FormStartPosition.CenterScreen;
		Font = ReferenceTheme.Dialog;
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 2,
			Padding = new Padding(18),
			RowCount = 8
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175f));
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		Controls.Add(tableLayoutPanel);
		servers = new ComboBox
		{
			Dock = DockStyle.Fill,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		operatorName = new TextBox
		{
			Dock = DockStyle.Fill,
			Text = Environment.UserName
		};
		link = new TextBox
		{
			Dock = DockStyle.Fill,
			UseSystemPasswordChar = true
		};
		if (Td2)
		{
			servers.DropDownStyle = ComboBoxStyle.DropDown;
		}
		Add(tableLayoutPanel, 0, Td2 ? "Serwer TD2" : "Serwer SimRail", servers);
		load = Ui.Button("Pobierz rozkład i posterunki", LoadSnapshot);
		Add(tableLayoutPanel, 1, "", load);
		Add(tableLayoutPanel, 2, "Operator EDR", operatorName);
		Add(tableLayoutPanel, 3, "Link SEPE z gry (opcjonalny)", link);
		if (Td2)
		{
			foreach (Control control3 in tableLayoutPanel.Controls)
			{
				if (tableLayoutPanel.GetRow(control3) == 3)
				{
					control3.Visible = false;
				}
			}
			tableLayoutPanel.RowStyles[3].SizeType = SizeType.Absolute;
			tableLayoutPanel.RowStyles[3].Height = 0f;
		}
		Label control2 = new Label
		{
			Text = (Td2 ? "Pobierz rozkład TD2 i wybierz posterunki. Godziny i wpisy dziennika zapisujesz w EDR." : "Link SEPE można uzyskać, wchodząc w dodatkowe opcje posterunku\noraz klikając otwarcie SEPE w przeglądarce.\nBez linku godziny nie będą wpisywane automatycznie."),
			Dock = DockStyle.Fill,
			AutoSize = true,
			MinimumSize = new Size(0, Font.Height * 3 + 6),
			Margin = new Padding(3, 0, 3, 10)
		};
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		tableLayoutPanel.Controls.Add(control2, 0, 4);
		tableLayoutPanel.SetColumnSpan(control2, 2);
		TableLayoutPanel tableLayoutPanel2 = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 3,
			RowCount = 2,
			Margin = new Padding(3, 0, 3, 8)
		};
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56f));
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		tableLayoutPanel2.Controls.Add(new Label
		{
			Text = (Td2 ? "Sceneria / stacja (posterunek)" : "Dostępne posterunki"),
			AutoSize = true,
			Margin = new Padding(0, 0, 0, 6)
		}, 0, 0);
		tableLayoutPanel2.Controls.Add(new Label
		{
			Text = "Wybrane posterunki",
			AutoSize = true,
			Margin = new Padding(0, 0, 0, 6)
		}, 2, 0);
		available = new ListBox
		{
			Dock = DockStyle.Fill,
			IntegralHeight = false,
			SelectionMode = SelectionMode.MultiExtended,
			HorizontalScrollbar = true,
			Margin = new Padding(0)
		};
		selected = new ListBox
		{
			Dock = DockStyle.Fill,
			IntegralHeight = false,
			SelectionMode = SelectionMode.MultiExtended,
			HorizontalScrollbar = true,
			Margin = new Padding(0)
		};
		tableLayoutPanel2.Controls.Add(available, 0, 1);
		tableLayoutPanel2.Controls.Add(selected, 2, 1);
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			Padding = new Padding(7, 50, 0, 0)
		};
		add = Ui.Button(">", AddSelected);
		remove = Ui.Button("<", RemoveSelected);
		Button[] array = new Button[2] { add, remove };
		foreach (Button button in array)
		{
			button.AutoSize = false;
			button.MinimumSize = new Size(36, 30);
			button.Size = new Size(36, 30);
			button.Margin = new Padding(0, 4, 0, 4);
			flowLayoutPanel.Controls.Add(button);
		}
		tableLayoutPanel2.Controls.Add(flowLayoutPanel, 1, 1);
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		tableLayoutPanel.Controls.Add(tableLayoutPanel2, 0, 5);
		tableLayoutPanel.SetColumnSpan(tableLayoutPanel2, 2);
		status = new Label
		{
			Dock = DockStyle.Fill,
			AutoSize = true,
			MinimumSize = new Size(0, Font.Height * 2 + 6),
			Margin = new Padding(3, 0, 3, 6)
		};
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		tableLayoutPanel.Controls.Add(status, 0, 6);
		tableLayoutPanel.SetColumnSpan(status, 2);
		FlowLayoutPanel flowLayoutPanel2 = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			AutoSize = true,
			FlowDirection = FlowDirection.RightToLeft,
			WrapContents = false
		};
		flowLayoutPanel2.Controls.Add(Ui.Button("Anuluj", () =>
		{
			Close();
		}));
		connect = Ui.Button("Otwórz posterunki", Connect);
		connect.Enabled = false;
		flowLayoutPanel2.Controls.Add(connect);
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		tableLayoutPanel.Controls.Add(flowLayoutPanel2, 0, 7);
		tableLayoutPanel.SetColumnSpan(flowLayoutPanel2, 2);
		servers.SelectedIndexChanged += (object s, EventArgs e) =>
		{
			snapshot = null;
			available.Items.Clear();
			selected.Items.Clear();
			UpdateConnectState();
		};
		available.SelectedIndexChanged += (object s, EventArgs e) =>
		{
			UpdateConnectState();
		};
		selected.SelectedIndexChanged += (object s, EventArgs e) =>
		{
			UpdateConnectState();
		};
		available.DoubleClick += (object s, EventArgs e) =>
		{
			AddSelected();
		};
		selected.DoubleClick += (object s, EventArgs e) =>
		{
			RemoveSelected();
		};
		link.TextChanged += (object s, EventArgs e) =>
		{
			linkTimer.Stop();
			linkTimer.Start();
			UpdateConnectState();
		};
		linkTimer.Tick += (object s, EventArgs e) =>
		{
			ApplyLink();
		};
		Shown += async (object s, EventArgs e) =>
		{
			int num = default;
			_ = num;
			_ = 0;
			try
			{
				status.Text = "Pobieranie serwerów…";
				List<SimRailServer> list = await feed.ServersAsync(cancel.Token);
				if (!IsDisposed)
				{
					servers.Items.AddRange(list.Cast<object>().ToArray());
					serversReady = true;
					if (servers.Items.Count > 0)
					{
						servers.SelectedIndex = 0;
					}
					status.Text = (Td2 ? "Pobierz rozkład i dodaj posterunki przyciskiem >." : "Wklej link SEPE albo pobierz rozkład i dodaj posterunki przyciskiem >.");
					if (!string.IsNullOrWhiteSpace(link.Text))
					{
						ApplyLink();
					}
				}
			}
			catch (Exception)
			{
				if (!IsDisposed)
				{
					status.Text = "Nie można pobrać serwerów. Sprawdź połączenie i uruchom ponownie.";
				}
			}
		};
		FormClosed += (object s, FormClosedEventArgs e) =>
		{
			linkTimer.Stop();
			linkTimer.Dispose();
			cancel.Cancel();
		};
		UpdateConnectState();
	}

	private static void Add(TableLayoutPanel t, int row, string title, Control control)
	{
		t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		t.Controls.Add(new Label
		{
			Text = title,
			AutoSize = true,
			Margin = new Padding(3, 7, 3, 8)
		}, 0, row);
		control.Margin = new Padding(3, 4, 3, 8);
		t.Controls.Add(control, 1, row);
	}

	private void FillLocations(IEnumerable<string> chosen)
	{
		HashSet<string> ids = new HashSet<string>(chosen);
		List<SimRailLocation> list = snapshot.Locations();
		List<SimRailLocation> list2 = list.Where((SimRailLocation x) => snapshot.DispatcherNames.Contains(x.Name)).ToList();
		if (list2.Count == 0)
		{
			list2 = list;
		}
		if (!string.IsNullOrWhiteSpace(link.Text))
		{
			try
			{
				SepeLinkInfo info = SepeLink.Inspect(link.Text);
				if (info.ServerCode == snapshot.ServerCode)
				{
					list2 = (from x in list2.Concat(list.Where((SimRailLocation x) => info.PointIds.Contains(x.Id)))
						group x by x.Id into x
						select x.First()).OrderBy((SimRailLocation x) => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
				}
			}
			catch (ArgumentException)
			{
			}
		}
		available.BeginUpdate();
		selected.BeginUpdate();
		available.Items.Clear();
		selected.Items.Clear();
		selected.Items.AddRange(list.Where((SimRailLocation x) => ids.Contains(x.Id)).Cast<object>().ToArray());
		available.Items.AddRange(list2.Where((SimRailLocation x) => !ids.Contains(x.Id)).Cast<object>().ToArray());
		selected.EndUpdate();
		available.EndUpdate();
		UpdateConnectState();
	}

	private void AddSelected()
	{
		if (!loading && snapshot != null)
		{
			string[] chosen = (from SimRailLocation x in selected.Items
				select x.Id).Concat(from SimRailLocation x in available.SelectedItems
				select x.Id).ToArray();
			FillLocations(chosen);
			ShowSelectionStatus();
		}
	}

	private void RemoveSelected()
	{
		if (!loading && snapshot != null)
		{
			HashSet<string> removed = new HashSet<string>(from SimRailLocation x in selected.SelectedItems
				select x.Id);
			FillLocations((from SimRailLocation x in selected.Items
				select x.Id into x
				where !removed.Contains(x)
				select x).ToArray());
			ShowSelectionStatus();
		}
	}

	private string LinkError()
	{
		if (string.IsNullOrWhiteSpace(link.Text))
		{
			return null;
		}
		try
		{
			SepeLinkInfo sepeLinkInfo = SepeLink.Inspect(link.Text);
			if (!(servers.SelectedItem is SimRailServer simRailServer) || sepeLinkInfo.ServerCode != simRailServer.ServerCode)
			{
				return "Serwer z linku nie jest wybrany.";
			}
			if (snapshot != null && sepeLinkInfo.PointIds.Any((string id) => !snapshot.Locations().Any((SimRailLocation x) => x.Id == id)))
			{
				return "Nie znaleziono wszystkich posterunków z linku. Pobierz rozkład ponownie lub popraw link.";
			}
			return null;
		}
		catch (ArgumentException ex)
		{
			return ex.Message;
		}
	}

	private void UpdateConnectState()
	{
		if (connect != null)
		{
			connect.Enabled = !loading && !linkTimer.Enabled && snapshot != null && selected.Items.Count > 0 && LinkError() == null;
		}
		if (add != null)
		{
			add.Enabled = !loading && available.SelectedItems.Count > 0;
		}
		if (remove != null)
		{
			remove.Enabled = !loading && selected.SelectedItems.Count > 0;
		}
	}

	private void ShowSelectionStatus()
	{
		string text = LinkError();
		status.Text = text ?? ("Wybrane posterunki: " + selected.Items.Count + ". " + (Td2 ? "Wpisy dziennika pozostaną w EDR." : (string.IsNullOrWhiteSpace(link.Text) ? "Możesz otworzyć je bez linku SEPE." : "Posterunki z linku dodano automatycznie.")));
		UpdateConnectState();
	}

	private async void LoadSnapshot()
	{
		int num = default;
		_ = num;
		_ = 0;
		try
		{
			await DownloadSnapshot(fromLink: false);
		}
		catch (Exception ex)
		{
			if (!IsDisposed)
			{
				status.Text = ex.Message;
			}
		}
	}

	private async Task DownloadSnapshot(bool fromLink)
	{
		if (Td2 && servers.SelectedItem == null)
		{
			string region = Td2Provider.Region(servers.Text);
			SimRailServer simRailServer = servers.Items.Cast<SimRailServer>().FirstOrDefault((SimRailServer x) => x.ServerCode == region);
			if (simRailServer == null)
			{
				SimRailServer simRailServer2 = new SimRailServer();
				simRailServer2.ServerCode = region;
				simRailServer2.ServerName = Td2Provider.ServerName(region);
				simRailServer2.IsActive = true;
				simRailServer = simRailServer2;
				servers.Items.Add(simRailServer);
			}
			servers.SelectedItem = simRailServer;
		}
		if (loading || servers.SelectedItem == null)
		{
			return;
		}
		SimRailServer server = (SimRailServer)servers.SelectedItem;
		string[] chosen = (from SimRailLocation x in selected.Items
			select x.Id).ToArray();
		loading = true;
		load.Enabled = false;
		servers.Enabled = false;
		UpdateConnectState();
		bool success = false;
		try
		{
			int num = default;
			_ = num;
			_ = 0;
			try
			{
				status.Text = "Pobieranie rozkładu serwera…";
				SimRailSnapshot downloaded = await feed.SnapshotAsync(server.ServerCode, cancel.Token);
				if (!IsDisposed)
				{
					if (servers.SelectedItem != server || downloaded.ServerCode != server.ServerCode)
					{
						throw new InvalidOperationException("Odpowiedź innego serwera.");
					}
					snapshot = downloaded;
					FillLocations(chosen);
					success = true;
					status.Text = "Rozkład pobrany. Wybierz posterunki i kliknij >. Czas serwera: " + snapshot.GameTime.ToString("dd-MM-yyyy HH:mm:ss");
				}
			}
			catch (Exception error)
			{
				if (!IsDisposed)
				{
					snapshot = null;
					available.Items.Clear();
					selected.Items.Clear();
					status.Text = "Nie udało się pobrać rozkładu. Możesz spróbować ponownie." + (Td2 ? (" " + SepeDiagnostics.Reason(error)) : "");
				}
			}
		}
		finally
		{
			loading = false;
			if (!IsDisposed)
			{
				load.Enabled = true;
				servers.Enabled = true;
				bool flag = linkPending;
				linkPending = false;
				bool flag2 = success && !string.IsNullOrWhiteSpace(link.Text) && link.Text != appliedLink && LinkError() == null;
				if (success && (fromLink || flag || flag2))
				{
					ApplyLink();
				}
				else if (success && !string.IsNullOrWhiteSpace(link.Text))
				{
					ShowSelectionStatus();
				}
				UpdateConnectState();
			}
		}
	}

	private async void ApplyLink()
	{
		linkTimer.Stop();
		if (string.IsNullOrWhiteSpace(link.Text))
		{
			if (!loading)
			{
				ShowSelectionStatus();
			}
			return;
		}
		SepeLinkInfo info;
		try
		{
			info = SepeLink.Inspect(link.Text);
		}
		catch (ArgumentException ex)
		{
			status.Text = ex.Message;
			UpdateConnectState();
			return;
		}
		if (!serversReady)
		{
			return;
		}
		if (loading)
		{
			linkPending = true;
			return;
		}
		SimRailServer server = servers.Items.Cast<SimRailServer>().FirstOrDefault((SimRailServer x) => x.ServerCode == info.ServerCode);
		if (server == null)
		{
			status.Text = "Serwer z linku nie jest dostępny na liście SimRail.";
			UpdateConnectState();
			return;
		}
		if (servers.SelectedItem != server)
		{
			servers.SelectedItem = server;
		}
		if (snapshot == null)
		{
			await DownloadSnapshot(fromLink: true);
			return;
		}
		string error = LinkError();
		if (error != null)
		{
			status.Text = error;
			UpdateConnectState();
			return;
		}
		FillLocations((from SimRailLocation x in selected.Items
			select x.Id).Concat(info.PointIds).ToArray());
		appliedLink = link.Text;
		ShowSelectionStatus();
	}

	private void Connect()
	{
		linkTimer.Stop();
		UpdateConnectState();
		if (!connect.Enabled)
		{
			return;
		}
		string name = Ui.Required(operatorName, "Operator");
		List<SimRailLocation> list = selected.Items.Cast<SimRailLocation>().ToList();
		SimRailLocation location = list[0];
		if (list.Any((SimRailLocation x) => string.IsNullOrEmpty(x.Id) || x.Id.Length > 20 || !x.Id.All((char c) => c >= '0' && c <= '9')))
		{
			throw new Exception("API zwróciło nieprawidłowy identyfikator posterunku.");
		}
		SimRailSession simRailSession = new SimRailSession(feed, snapshot, location.Id);
		if (!string.IsNullOrWhiteSpace(link.Text))
		{
			simRailSession.SetAccessLink(link.Text, allowOtherPoints: true);
		}
		simRailSession.SetControlledPoints(list.Select((SimRailLocation x) => x.Id));
		string text = Path.Combine(simulatorDataRoot, snapshot.ServerCode, simRailSession.StoragePointId);
		SimulatorSessionLock simulatorSessionLock = (simRailSession.SessionLock = new SimulatorSessionLock(snapshot.ServerCode, list.Select((SimRailLocation x) => x.Id), Td2 ? "td2" : "simrail"));
		try
		{
			bool flag = !LocalPersistence.Enabled || !File.Exists(Path.Combine(text, "edr.json"));
			Store store = new Store(text, !LocalPersistence.Enabled);
			store.Load();
			if (flag)
			{
				Station station = simRailSession.SuggestStation(location.Id);
				StationSettings.Normalize(station);
				store.Commit((AppState s) =>
				{
					s.Stations.Clear();
					s.Stations.Add(station);
					s.Trains.Clear();
					s.Entries.Clear();
					s.Duties.Clear();
					s.History.Clear();
					s.Services.Clear();
				});
			}
			LocalUser user = store.State.Users.First((LocalUser x) => x.Id == "DEMO");
			if (store.State.Duties.Any((Duty x) => !x.End.HasValue && x.UserId == user.Id && x.UserName != name))
			{
				throw new Exception("Pozostał otwarty dyżur innego operatora. Wpisz jego nazwę, aby go kontynuować i zdać.");
			}
			store.Commit((AppState s) =>
			{
				s.Users.First((LocalUser x) => x.Id == "DEMO").Name = name;
			});
			Engine engine = new Engine(store);
			engine.SimRail = simRailSession;
			engine.StationProfileDirectory = organization.Profiles;
			Engine engine2 = engine;
			engine2.StartSession(store.State.Users.First((LocalUser x) => x.Id == "DEMO"));
			engine2.ImportControlledStationData();
			if (engine2.State.Duties.Any((Duty x) => !x.End.HasValue && x.UserId == user.Id && x.UserName != name))
			{
				throw new Exception("Pozostał otwarty dyżur innego operatora na jednym z połączonych posterunków.");
			}
			engine2.EnsureControlledStations();
			engine2.StationId = engine2.State.Stations.First((Station x) => x.SimRailPointId == location.Id).Id;
			engine2.RepairSuggestedNeighbours();
			engine2.RepairRouteNeighbours();
			engine2.InitializeStationProfiles();
			if (!string.IsNullOrEmpty(engine2.ConfigurationProfileWarning))
			{
				MessageBox.Show(this, engine2.ConfigurationProfileWarning, "Konfiguracja posterunku", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
			engine2.RepairRouteNeighbours();
			engine2.ApplySimRailSnapshot(snapshot);
			ConnectedEngine = engine2;
			NeedsConfiguration = !engine2.Station.SimulatorReviewed;
			DatabaseLock = simulatorSessionLock;
			DialogResult = DialogResult.OK;
			Close();
		}
		catch
		{
			simulatorSessionLock.Dispose();
			throw;
		}
	}
}

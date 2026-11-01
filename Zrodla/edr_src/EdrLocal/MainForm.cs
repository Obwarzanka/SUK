using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EdrLocal;

public class MainForm : ReferenceForm
{
	private class ScaleSpec
	{
		public Rectangle Bounds;

		public Font Font;

		public Padding Padding;

		public Padding Margin;

		public Size Minimum;

		public Size Images;

		public int HeaderHeight;

		public int[] Columns;

		public float[] Rows = new float[0];

		public float[] Cols = new float[0];

		public ScaleSpec(Control c)
		{
			Bounds = c.Bounds;
			Font = c.Font;
			Padding = c.Padding;
			Margin = c.Margin;
			Minimum = ((c is MainForm) ? new Size(800, 500) : c.MinimumSize);
			if (c is DataGridView dataGridView)
			{
				HeaderHeight = dataGridView.ColumnHeadersHeight;
				Columns = (from DataGridViewColumn x in dataGridView.Columns
					select x.Width).ToArray();
			}
			if (c is ToolStrip toolStrip)
			{
				Images = toolStrip.ImageScalingSize;
			}
			if (c is TableLayoutPanel tableLayoutPanel)
			{
				Rows = (from RowStyle x in tableLayoutPanel.RowStyles
					select x.Height).ToArray();
				Cols = (from ColumnStyle x in tableLayoutPanel.ColumnStyles
					select x.Width).ToArray();
			}
		}
	}

	private class ItemSpec
	{
		public Font Font;

		public Size Size;

		public Padding Margin;

		public Padding Padding;

		public ItemSpec(ToolStripItem item)
		{
			Font = item.Font;
			Size = item.Size;
			Margin = item.Margin;
			Padding = item.Padding;
		}
	}

	private readonly CancellationTokenSource counterCancellation = new CancellationTokenSource();

	private readonly AccidentCounterClient accidentCounter;

	private TimetableFilterPanel filterPanel;

	private LinkLabel confirmationLink;

	private LinkLabel movementLink;

	private bool fittingColumns;

	private readonly Dictionary<DataGridViewColumn, float> manualColumnWidths = new Dictionary<DataGridViewColumn, float>();

	private bool descriptionReady;

	private bool arrangingDescription;

	private Dictionary<string, Label> profileLabels = new Dictionary<string, Label>();

	private Panel descriptionSurface;

	private Dictionary<string, string> profileTitles = new Dictionary<string, string>();

	private DataGridView trains;

	private TabControl topTabs;

	private TabControl journalTabs;

	private SplitContainer split;

	private FlowLayoutPanel tracks;

	private ComboBox stations;

	private ComboBox kindFilter;

	private TextBox search;

	private CompactDatePicker timetableDay;

	private CheckBox runningOnly;

	private CheckBox notConfirmed;

	private ToolStripStatusLabel statusUser;

	private ToolStripStatusLabel statusDuty;

	private ToolStripStatusLabel statusLogin;

	private ToolStripStatusLabel statusUnit;

	private ToolStripButton dutyButton;

	private List<JournalPane> panes = new List<JournalPane>();

	private string selectedTrainId;

	private bool filling;

	private Form detached;

	private System.Windows.Forms.Timer timer;

	private DataGridView servicesGrid;

	private readonly List<TabPage> hiddenTimetableTabs = new List<TabPage>();

	private Control headerClock;

	private Label filterInfo;

	private Control ownNotes;

	private Control operationsNotes;

	private Control carrierNotes;

	private Panel headerRow;

	private DateTime nextRefresh = DateTime.Now;

	private int refreshMinutes = 3;

	private bool useTimeRange = true;

	private int timeRangeHours = 6;

	private int confirmationFilter;

	private int movementFilter;

	private CheckBox currentData;

	private string currentLayout = "Dół";

	private string previousLayout = "Dół";

	internal Action<string, string> OperatorNoticeSink;

	private float fontDpi = 96f;

	private float currentZoom = 1f;

	private bool scalingReady;

	private bool scaleApplied;

	private bool applyingScale;

	private bool layoutQueued;

	private Dictionary<Control, ScaleSpec> scaleSpecs = new Dictionary<Control, ScaleSpec>();

	private Dictionary<ToolStripItem, ItemSpec> itemSpecs = new Dictionary<ToolStripItem, ItemSpec>();

	private Dictionary<string, Font> scaledFonts = new Dictionary<string, Font>();

	internal readonly Queue<SepeWriteRequest> outgoingHours = new Queue<SepeWriteRequest>();

	internal bool sendingHours;

	internal bool hoursPaused;

	internal bool closingSession;

	internal bool decidingExit;

	internal ISepeHoursWriter backgroundWriter;

	private string lastSepeError;

	internal Task hoursTask;

	internal CancellationTokenSource hoursCancellation;

	internal Func<ISepeHoursWriter> HoursWriterFactory;

	internal SessionWindows sessionWindows;

	internal bool additionalSessionView;

	internal string windowStationSignature;

	private bool sharedRefreshQueued;

	private readonly CancellationTokenSource simulatorCancellation = new CancellationTokenSource();

	internal bool simulatorRefreshing;

	internal ToolStripMenuItem simulatorState;

	private SepeBrowserForm sepeWindow;

	private bool savedTimetableView;

	private DateTime nextStationTrackRefresh;

	private StationTrackPopup stationTrackPopup;

	private bool trackPopupEvents;

	internal Func<string, bool> ConfirmStationTrackRemoval;

	private Dictionary<string, TextBox> profileBoxes = new Dictionary<string, TextBox>();

	private Dictionary<string, CheckBox> profileFlags = new Dictionary<string, CheckBox>();

	private Dictionary<string, DataGridView> detailGrids = new Dictionary<string, DataGridView>();

	private Dictionary<string, Label> detailHeadings = new Dictionary<string, Label>();

	private Label connectionSummary;

	private Label connectionWaiting;

	private Label connectionForecast;

	private Label extraRelation;

	private CheckBox td2RouteLines;

	private bool td2RouteBinding;

	private bool selectionDetailsQueued;

	public string AccidentCounterCaption => "Czas bez wypadku spow. przez pracownika PLK";

	public TimeSpan AccidentCounterDuration => accidentCounter.Duration;

	public Engine Engine { get; private set; }

	public Train SelectedTrain => Engine.State.Trains.FirstOrDefault((Train t) => t.Id == selectedTrainId);

	public float CurrentZoom
	{
		get
		{
			return currentZoom;
		}
		private set
		{
			currentZoom = value;
		}
	}

	internal bool IsApplyingScale => applyingScale;

	public Font ScaledBold => ZoomFont(ReferenceTheme.SmallBold, CurrentZoom);

	private bool CanSendSepeHours
	{
		get
		{
			if (Engine.SimRail != null && Engine.SimRail.HasWriteLink)
			{
				return Engine.SepeWritesEnabled;
			}
			return false;
		}
	}

	private bool SharedSessionBusy
	{
		get
		{
			if (sessionWindows != null)
			{
				return sessionWindows.Busy(this);
			}
			return false;
		}
	}

	private async void PollAccidentCounter()
	{
		if (Engine.SimRail != null && !Ui.Testing && !IsDisposed && !counterCancellation.IsCancellationRequested && await accidentCounter.PollIfDueAsync(counterCancellation.Token) && !IsDisposed && !counterCancellation.IsCancellationRequested && headerClock != null)
		{
			headerClock.Invalidate();
		}
	}

	private static AccidentCounterClient CreateAccidentCounter(string provider)
	{
		return new AccidentCounterClient(provider, new AccidentCounterHttpFeed(), (Ui.Testing || !LocalPersistence.Enabled) ? null : Path.Combine(DataLocations.Root, "liczniki-api", provider + ".json"));
	}

	public void DutyAction()
	{
		if (Engine.ReadOnlySession && Engine.ActiveDuty != null && Engine.ActiveDuty.UserId == Engine.User.Id)
		{
			Engine.ReadOnlySession = false;
			RefreshData();
			return;
		}
		if (Engine.ActiveDuty != null && !Engine.CanWrite)
		{
			throw new Exception("Dyżur prowadzi " + Engine.ActiveDuty.UserName + ". Zaloguj się na konto tego operatora, aby go kontynuować lub zdać.");
		}
		bool ending = Engine.CanWrite;
		using Editor editor = new Editor(ending ? "Zdanie dyżuru" : "Przyjęcie dyżuru", 580, 265);
		Panel panel = editor.AbsoluteBody();
		AtLabel(panel, ending ? "Zdanie dyżuru" : "Przyjęcie dyżuru", 7, 5, 540, 25, bold: true).Font = new Font("Segoe UI", 11f, FontStyle.Bold);
		AtLabel(panel, "Posterunek ruchu:", 7, 44, 177, 29, bold: false);
		TextBox textBox = Ui.Text(StationDisplay(Engine.Station), multiline: false);
		textBox.ReadOnly = true;
		textBox.Font = ReferenceTheme.Dialog;
		Place(panel, textBox, 185, 43, 380, 28);
		AtLabel(panel, "Dyżurny ruchu:", 7, 82, 177, 29, bold: false);
		TextBox textBox2 = Ui.Text(Engine.User.Name, multiline: false);
		textBox2.ReadOnly = true;
		textBox2.Font = ReferenceTheme.Dialog;
		Place(panel, textBox2, 185, 81, 380, 28);
		DateTime dateTime = Engine.Now;
		if (!ending)
		{
			Duty duty = (from x in Engine.State.Duties
				where x.StationId == Engine.StationId && x.End.HasValue
				orderby x.End descending
				select x).FirstOrDefault();
			if (duty != null && Engine.Now - duty.End.Value < TimeSpan.FromMinutes(30.0))
			{
				dateTime = duty.End.Value;
			}
		}
		AtLabel(panel, ending ? "Godzina zdania dyżuru:" : "Godzina przyjęcia dyżuru:", 7, 130, 179, 29, bold: false);
		ReferenceTimeInput time = new ReferenceTimeInput(dateTime, dateTime, blue: false, () => Engine.Now);
		time.Active.Visible = false;
		time.Input.BackColor = Ui.Cream;
		time.Input.Font = new Font("Segoe UI", 12f);
		Place(panel, time, 185, 129, 80, 30);
		editor.Save(() =>
		{
			DateTime? value = time.Value;
			if (!value.HasValue)
			{
				throw new Exception("Podaj godzinę dyżuru.");
			}
			if (ending)
			{
				Engine.EndDuty(value.Value);
				return;
			}
			bool readOnlySession = Engine.ReadOnlySession;
			Engine.ReadOnlySession = false;
			try
			{
				Engine.BeginDuty(value.Value);
			}
			catch
			{
				Engine.ReadOnlySession = readOnlySession;
				throw;
			}
		});
		DialogButtons(editor);
		editor.Buttons.Height = 49;
		if (editor.ShowDialog(this) == DialogResult.OK)
		{
			RefreshData();
			if (!ending)
			{
				Situation();
			}
		}
	}

	public void EditJournal(JournalEntry original, Train train, string direction, string sectionId)
	{
		if (!FlushJournals())
		{
			return;
		}
		if (original != null)
		{
			original = Engine.State.Entries.FirstOrDefault((JournalEntry x) => x.Id == original.Id) ?? original;
		}
		Engine.RequireWrite();
		if (original != null && original.Cancelled)
		{
			throw new Exception("Wykreślonego wpisu nie można zmieniać.");
		}
		if (train == null && original != null)
		{
			train = Engine.State.Trains.FirstOrDefault((Train t) => t.Id == original.TrainId);
		}
		List<Section> list = Engine.Station.Sections.Where(StationSettings.Conducted).ToList();
		if (list.Count == 0)
		{
			throw new Exception("Brak skonfigurowanych szlaków.");
		}
		JournalEntry journalEntry;
		if (original != null)
		{
			journalEntry = Json.Clone(original);
		}
		else
		{
			JournalEntry journalEntry2 = new JournalEntry();
			journalEntry2.Id = Guid.NewGuid().ToString("N");
			journalEntry2.StationId = Engine.StationId;
			journalEntry2.Type = "Pociąg";
			journalEntry2.Direction = direction;
			journalEntry2.Time = Engine.Now;
			journalEntry2.TrainId = train?.Id;
			journalEntry2.Number = ((train == null) ? "" : train.Number);
			journalEntry2.Track = ((train == null) ? "" : train.Track);
			journalEntry2.Text = "";
			journalEntry2.LineTrack = 0;
			journalEntry = journalEntry2;
		}
		JournalEntry journalEntry3 = journalEntry;
		Section inferred = ((train == null) ? null : SectionRouting.Find(list, train, direction));
		Section section = list.FirstOrDefault((Section s) => s.Id == (sectionId ?? ((original == null) ? null : original.SectionId))) ?? inferred;
		if (section == null && train != null && sectionId != "*")
		{
			throw new Exception("Nie można jednoznacznie wyznaczyć dziennika według sąsiedniego posterunku i numeru linii. Wybierz dziennik ręcznie.");
		}
		if (section == null)
		{
			section = list[0];
		}
		if (sectionId == "*")
		{
			section = list.FirstOrDefault((Section s) => inferred == null || s.Id != inferred.Id) ?? list[0];
		}
		journalEntry3.SectionId = section.Id;
		ShowReferenceTrainDialog(original, train, direction, section, journalEntry3);
	}

	public void Telephone(string section)
	{
		Telephone(section, null);
	}

	public void Telephone(string section, JournalEntry original)
	{
		ShowReferenceTelephone(section, original);
	}

	private static string Template(string effect)
	{
		return effect switch
		{
			"Zamknij" => "Zamknięcie toru", 
			"Otwórz" => "Otwarcie toru", 
			"Telefoniczne" => "Zapowiadanie telefoniczne", 
			"Normalne" => "Odwołanie zapowiadania", 
			_ => "Wpis własny", 
		};
	}

	public void FreeNote(string section)
	{
		FreeNote(section, null);
	}

	public void FreeNote(string section, JournalEntry original)
	{
		Engine.RequireWrite();
		if (original != null && (original.Cancelled || original.Type == "Służbowy"))
		{
			throw new Exception("Wpisu służbowego lub wykreślonego nie można edytować.");
		}
		JournalEntry en = ((original == null) ? Engine.Note(section, Engine.Now, "", "Wpis") : Json.Clone(original));
		using Editor editor = new Editor("Wpis do dziennika", 620, 360);
		DateTimePicker time = editor.Add("Data i godzina", Ui.Date(en.Time, nullable: false, active: true), 35);
		TextBox text = editor.Add("Treść wpisu", Ui.Text(en.Text, multiline: true), 170);
		editor.Save(() =>
		{
			en.Time = time.Value;
			en.Text = Ui.Required(text, "Treść");
			Engine.SaveJournal(en);
		});
		if (editor.ShowDialog(this) == DialogResult.OK)
		{
			RefreshData();
		}
	}

	public void CancelEntry(JournalEntry en)
	{
		Engine.RequireWrite();
		using Editor editor = new Editor("Wykreślenie błędnego wpisu", 590, 300);
		editor.Info("Oryginalny zapis pozostanie widoczny wraz z przyczyną wykreślenia.", 48);
		TextBox reason = editor.Add("Przyczyna", Ui.Text("", multiline: true), 95);
		editor.Save(() =>
		{
			Engine.Cancel(en.Id, Ui.Required(reason, "Przyczyna"));
		});
		if (editor.ShowDialog(this) == DialogResult.OK)
		{
			RefreshData();
		}
	}

	public void Situation()
	{
		using SituationForm situationForm = new SituationForm(Engine, CurrentZoom);
		situationForm.ShowDialog(this);
	}

	private void CheckAction()
	{
		Engine.RequireWrite();
		using Editor editor = new Editor("Wymagane sprawdzenie", 680, 390);
		editor.Info((Engine.DueChecks().Count == 0) ? "Brak zaległych sprawdzeń według skonfigurowanych godzin." : ("Oczekujące: " + string.Join(", ", Engine.DueChecks())), 65);
		ComboBox what = editor.Add("Rodzaj sprawdzenia", Ui.Combo(Engine.Station.Checks, null, editable: true), 35);
		DateTimePicker time = editor.Add("Data i godzina", Ui.Date(Engine.Now, nullable: false, active: true), 35);
		TextBox notes = editor.Add("Wynik / uwagi", Ui.Text("Bez uwag", multiline: false), 35);
		editor.Save(() =>
		{
			if (string.IsNullOrWhiteSpace(what.Text))
			{
				throw new Exception("Wpisz rodzaj sprawdzenia.");
			}
			JournalEntry journalEntry = Engine.Note("TEL", time.Value, what.Text + ": " + notes.Text, "Sprawdzenie");
			journalEntry.CheckName = what.Text;
			Engine.SaveJournal(journalEntry);
		});
		if (editor.ShowDialog(this) == DialogResult.OK)
		{
			RefreshData();
		}
	}

	private void TrainCargo()
	{
		Engine.RequireWrite();
		Train t = NeedTrain();
		using Editor editor = new Editor("Towary niebezpieczne i przesyłki - " + t.Number, 650, 440);
		CheckBox dangerous = editor.Add("TN", new CheckBox
		{
			Text = "Towary niebezpieczne",
			Checked = t.Dangerous
		}, 32);
		CheckBox risk = editor.Add("TWR", new CheckBox
		{
			Text = "Towary wysokiego ryzyka",
			Checked = t.HighRisk
		}, 32);
		CheckBox extra = editor.Add("Przesyłka nadzwyczajna", new CheckBox
		{
			Text = "Tak",
			Checked = t.Extraordinary
		}, 32);
		TextBox cargo = editor.Add("Opis / wagony / UN / RID", Ui.Text(t.Cargo, multiline: true), 175);
		editor.Save(() =>
		{
			Engine.Change("Dane ładunku", t.Id, (AppState s) =>
			{
				Train train = s.Trains.First((Train y) => y.Id == t.Id);
				if (train.Dangerous != dangerous.Checked)
				{
					if (train.DetailRows == null)
					{
						train.DetailRows = new List<DetailRow>();
					}
					train.DetailRows.Add(new DetailRow
					{
						Id = Guid.NewGuid().ToString("N"),
						Kind = "TN",
						Cells = new string[4]
						{
							Engine.Now.ToString("yyyy-MM-dd HH:mm:ss"),
							Engine.User.Name,
							Engine.Station.Name,
							dangerous.Checked ? "Wprowadzono oznaczenie TN" : "Usunięto oznaczenie TN"
						}
					});
				}
				train.Dangerous = dangerous.Checked;
				train.HighRisk = risk.Checked;
				train.Extraordinary = extra.Checked;
				train.Cargo = cargo.Text;
				s.Entries.Add(Engine.Note("TEL", Engine.Now, "Pociąg " + t.Number + ": TN=" + train.Dangerous + ", TWR=" + train.HighRisk + ", PN=" + train.Extraordinary + ". " + train.Cargo, "Ładunek"));
			});
		});
		if (editor.ShowDialog(this) == DialogResult.OK)
		{
			RefreshData();
		}
	}

	private void TrainNotes()
	{
		Engine.RequireWrite();
		Train t = NeedTrain();
		using Editor editor = new Editor("Uwagi lokalne - " + t.Number, 640, 370);
		editor.Info("Uwagi własne wyświetlane w opisie pociągu i nad wykazem pociągów.", 43);
		TextBox notes = editor.Add("Treść", Ui.Text(t.Notes, multiline: true), 190);
		editor.Save(() =>
		{
			Engine.Change("Uwagi pociągu", t.Id, (AppState s) =>
			{
				s.Trains.First((Train y) => y.Id == t.Id).Notes = notes.Text;
			});
		});
		if (editor.ShowDialog(this) == DialogResult.OK)
		{
			RefreshData();
		}
	}

	private void ServiceEdit(ServiceRecord original)
	{
		Engine.RequireWrite();
		ServiceRecord en = ((original == null) ? new ServiceRecord
		{
			Id = Guid.NewGuid().ToString("N"),
			StationId = Engine.StationId,
			Type = "Manewry",
			Start = Engine.Now,
			Status = "Zarejestrowano"
		} : Json.Clone(original));
		using Editor editor = new Editor("Rejestracja usługi", 640, 465);
		ComboBox kind = editor.Add("Rodzaj usługi", Ui.Combo(new string[4] { "Manewry", "Postój", "Rozrząd", "Szerokie tory" }, en.Type, editable: false), 35);
		TextBox train = editor.Add("Numer pociągu", Ui.Text(en.Train, multiline: false), 35);
		TextBox carrier = editor.Add("Przewoźnik", Ui.Text(en.Carrier, multiline: false), 35);
		ComboBox track = editor.Add("Tor", Ui.Combo(Engine.Station.Tracks, en.Track, editable: false), 35);
		DateTimePicker start = editor.Add("Rozpoczęcie", Ui.Date(en.Start, nullable: false, active: true), 35);
		DateTimePicker end = editor.Add("Zakończenie", Ui.Date(en.End ?? Engine.Now, nullable: true, en.End.HasValue), 35);
		TextBox notes = editor.Add("Uwagi", Ui.Text(en.Notes, multiline: true), 85);
		editor.Save(() =>
		{
			en.Type = kind.Text;
			en.Train = Ui.Required(train, "Numer pociągu");
			en.Carrier = Ui.Required(carrier, "Przewoźnik");
			en.Track = track.Text;
			en.Start = start.Value;
			en.End = Ui.Nullable(end);
			if (en.End.HasValue && en.End < en.Start)
			{
				throw new Exception("Zakończenie poprzedza rozpoczęcie.");
			}
			en.Notes = notes.Text;
			en.Status = (en.End.HasValue ? "Zakończono" : "W trakcie");
			Engine.Change("Rejestracja usługi", en.Id, (AppState s) =>
			{
				s.Services.RemoveAll((ServiceRecord x) => x.Id == en.Id);
				s.Services.Add(en);
			});
		});
		if (editor.ShowDialog(this) == DialogResult.OK)
		{
			RefreshData();
		}
	}

	private void ServiceReject()
	{
		Engine.RequireWrite();
		if (servicesGrid.CurrentRow == null)
		{
			throw new Exception("Wybierz usługę.");
		}
		ServiceRecord r = (ServiceRecord)servicesGrid.CurrentRow.Tag;
		using Editor editor = new Editor("Brak zgody na usługę", 570, 280);
		TextBox reason = editor.Add("Powód", Ui.Text("", multiline: true), 120);
		editor.Save(() =>
		{
			string text = Ui.Required(reason, "Powód");
			Engine.Change("Brak zgody na usługę", r.Id, (AppState s) =>
			{
				ServiceRecord serviceRecord = s.Services.First((ServiceRecord y) => y.Id == r.Id);
				serviceRecord.Status = "Brak zgody";
				serviceRecord.Notes = (serviceRecord.Notes ?? "") + "\nBrak zgody: " + text;
			});
		});
		if (editor.ShowDialog(this) == DialogResult.OK)
		{
			RefreshData();
		}
	}

	private void ImportTimetable()
	{
		Engine.RequireCoordinator();
		OpenFileDialog openFileDialog = new OpenFileDialog();
		openFileDialog.Filter = "Rozkład CSV (*.csv)|*.csv";
		openFileDialog.Title = "Import lokalnego rozkładu";
		using OpenFileDialog openFileDialog2 = openFileDialog;
		if (openFileDialog2.ShowDialog(this) == DialogResult.OK)
		{
			List<Train> list = Csv.Import(openFileDialog2.FileName, Engine.Station);
			if (list.Any((Train t) => Engine.State.Trains.Any((Train x) => x.StationId == t.StationId && x.Number == t.Number && x.PlanArrival.Date == t.PlanArrival.Date)))
			{
				throw new Exception("Pociąg o tym numerze już istnieje w wybranym dniu. Nie zaimportowano żadnych danych.");
			}
			Engine.Change("Import CSV", Engine.StationId, (AppState s) =>
			{
				s.Trains.AddRange(list);
			});
			timetableDay.Value = list[0].PlanArrival;
			RefreshData();
			MessageBox.Show(this, "Zaimportowano " + list.Count + " pociągów.", "Import zakończony");
		}
	}

	private void ExportTimetable()
	{
		LocalPersistence.RequireEnabled();
		SaveFileDialog saveFileDialog = new SaveFileDialog();
		saveFileDialog.Filter = "CSV (*.csv)|*.csv";
		saveFileDialog.FileName = "rozklad-" + timetableDay.Value.ToString("yyyy-MM-dd") + ".csv";
		using SaveFileDialog saveFileDialog2 = saveFileDialog;
		if (saveFileDialog2.ShowDialog(this) != DialogResult.OK)
		{
			return;
		}
		List<string[]> list = new List<string[]>();
		list.Add(new string[8] { "Numer", "Rodzaj", "Przyjazd", "Odjazd", "Z kierunku", "W kierunku", "Tor", "Przewoźnik" });
		List<string[]> list2 = list;
		foreach (DataGridViewRow row in (IEnumerable)trains.Rows)
		{
			List<Train> source = Engine.State.Trains;
			Func<Train, bool> predicate = (Train x) => x.Id == (string)row.Tag;
			Train train = source.First(predicate);
			list2.Add(new string[8]
			{
				train.Number,
				train.Kind,
				train.PlanArrival.ToString("yyyy-MM-dd HH:mm"),
				train.PlanDeparture.ToString("yyyy-MM-dd HH:mm"),
				train.From,
				train.To,
				train.Track,
				train.Carrier
			});
		}
		Csv.Write(saveFileDialog2.FileName, list2);
	}

	private void Backup()
	{
		LocalPersistence.RequireEnabled();
		if (!FlushJournals())
		{
			return;
		}
		SaveFileDialog saveFileDialog = new SaveFileDialog();
		saveFileDialog.Filter = "Kopia danych JSON (*.json)|*.json";
		saveFileDialog.FileName = "edr-kopia-" + Engine.Now.ToString("yyyyMMdd-HHmmss") + ".json";
		using SaveFileDialog saveFileDialog2 = saveFileDialog;
		if (saveFileDialog2.ShowDialog(this) == DialogResult.OK)
		{
			if (Path.GetFullPath(saveFileDialog2.FileName).Equals(Engine.Store.FilePath, StringComparison.OrdinalIgnoreCase))
			{
				throw new Exception("Wybierz inne miejsce niż aktywny plik danych.");
			}
			File.WriteAllBytes(saveFileDialog2.FileName, Json.Bytes(Engine.State));
			MessageBox.Show(this, "Zapisano kopię danych.", "Kopia danych");
		}
	}

	public void History(string entity)
	{
		ReferenceForm referenceForm = new ReferenceForm();
		referenceForm.Text = "Historia zmian lokalnych";
		referenceForm.Font = ReferenceTheme.Dialog;
		referenceForm.ClientSize = new Size(1050, 625);
		referenceForm.StartPosition = FormStartPosition.CenterParent;
		using ReferenceForm referenceForm2 = referenceForm;
		SplitContainer splitContainer = new SplitContainer();
		splitContainer.Dock = DockStyle.Fill;
		splitContainer.Orientation = Orientation.Horizontal;
		splitContainer.SplitterDistance = 300;
		SplitContainer splitContainer2 = splitContainer;
		DataGridView g = Ui.Grid(journal: false);
		Ui.Column(g, "time", "Data i godzina", 155);
		Ui.Column(g, "user", "Użytkownik", 110);
		Ui.Column(g, "action", "Czynność", 240);
		Ui.Column(g, "entity", "Identyfikator wpisu", 300);
		TextBox text = new TextBox
		{
			Dock = DockStyle.Fill,
			Multiline = true,
			ReadOnly = true,
			ScrollBars = ScrollBars.Both,
			Font = new Font("Consolas", 9f),
			WordWrap = false
		};
		foreach (Audit item in Engine.State.History.Where((Audit x) => entity == null || x.Entity == entity).Reverse())
		{
			int index = g.Rows.Add(item.Time.ToString("dd.MM.yyyy HH:mm:ss"), Engine.AuditUserName(item), item.Action, item.Entity);
			g.Rows[index].Tag = item;
		}
		g.SelectionChanged += (object s, EventArgs e) =>
		{
			if (g.CurrentRow != null && g.CurrentRow.Tag is Audit audit)
			{
				text.Text = "PRZED ZMIANĄ:\r\n" + audit.Before + "\r\n\r\nPO ZMIANIE:\r\n" + audit.After;
			}
		};
		splitContainer2.Panel1.Controls.Add(g);
		splitContainer2.Panel2.Controls.Add(text);
		referenceForm2.Controls.Add(splitContainer2);
		referenceForm2.ShowDialog(this);
	}

	private void Report(bool telephone)
	{
		using JournalReportForm journalReportForm = new JournalReportForm(Engine, telephone, timetableDay.Value.Date);
		journalReportForm.ShowDialog(this);
	}

	private void WriteReport(string path, List<JournalEntry> entries, bool withHistory, bool telephone)
	{
		LocalPersistence.RequireEnabled();
		Section section = Engine.Station.Sections.FirstOrDefault((Section x) => entries.Any((JournalEntry e) => e.SectionId == x.Id));
		DateTime dateTime = ((entries.Count == 0) ? timetableDay.Value.Date : entries.Min((JournalEntry e) => e.Time));
		DateTime to = ((entries.Count == 0) ? dateTime.AddDays(1.0).AddSeconds(-1.0) : entries.Max((JournalEntry e) => e.Time));
		using JournalReport journalReport = new JournalReport(Engine, entries, telephone, withHistory, section, dateTime, to);
		journalReport.Export(path);
	}

	private void Legend()
	{
		using LegendForm legendForm = new LegendForm();
		legendForm.ShowDialog(this);
	}

	private Label FilterText(string text)
	{
		Label label = new Label();
		label.Text = text;
		label.AutoSize = true;
		label.Font = ReferenceTheme.Small;
		label.TextAlign = ContentAlignment.MiddleLeft;
		label.Margin = new Padding(0);
		return label;
	}

	private TextBox NoteBox()
	{
		TextBox textBox = new TextBox();
		textBox.ReadOnly = true;
		textBox.Multiline = true;
		textBox.BorderStyle = BorderStyle.FixedSingle;
		textBox.Dock = DockStyle.Fill;
		textBox.BackColor = Ui.Cream;
		textBox.Font = ReferenceTheme.Small;
		textBox.Margin = new Padding(1, 0, 6, 0);
		textBox.ScrollBars = ScrollBars.None;
		return textBox;
	}

	private Control BuildTimetableFilters()
	{
		filterPanel = new TimetableFilterPanel(this);
		FlowLayoutPanel options = filterPanel.Options;
		FlowLayoutPanel searchRow = filterPanel.SearchRow;
		stations = Ui.Combo(Engine.State.Stations.Select((Station s) => StationDisplay(s)), null, editable: false);
		stations.Font = ReferenceTheme.SmallBold;
		stations.Width = 270;
		stations.BackColor = Ui.Cream;
		stations.SelectedIndex = Engine.State.Stations.FindIndex((Station s) => s.Id == Engine.StationId);
		options.Controls.Add(TimetableFilterPanel.Group(stations));
		NumericUpDown frequency = new NumericUpDown
		{
			Minimum = 1m,
			Maximum = 60m,
			Value = 3m,
			Width = 34,
			Font = ReferenceTheme.Small,
			BackColor = Ui.Cream
		};
		frequency.ValueChanged += (object s, EventArgs e) =>
		{
			refreshMinutes = (int)frequency.Value;
			nextRefresh = Engine.Now.AddMinutes(refreshMinutes);
		};
		options.Controls.Add(TimetableFilterPanel.Group(FilterText("Częstotliwość\nodświeżania (w min.):"), frequency));
		DomainUpDown hours = new DomainUpDown
		{
			ReadOnly = true,
			Width = 32,
			Font = ReferenceTheme.Small,
			BackColor = Ui.Cream
		};
		hours.Items.AddRange(new object[7] { 48, 24, 18, 12, 9, 6, 3 });
		hours.SelectedItem = 6;
		hours.SelectedItemChanged += (object s, EventArgs e) =>
		{
			if (hours.SelectedItem != null)
			{
				timeRangeHours = (int)hours.SelectedItem;
				RefreshTrains();
			}
		};
		options.Controls.Add(TimetableFilterPanel.Group(FilterText("-/+\ngodz."), hours));
		currentData = new CheckBox
		{
			Text = "Dane\nbieżące",
			AutoSize = true,
			Checked = true,
			Font = ReferenceTheme.Small
		};
		options.Controls.Add(TimetableFilterPanel.Group(currentData));
		timetableDay = new CompactDatePicker
		{
			CustomFormat = "dd-MM-yyyy HH:mm",
			Font = ReferenceTheme.SmallBold,
			Width = 128,
			Height = 19,
			BackColor = Ui.Cream,
			Value = Engine.Now,
			TimeSettings = true,
			Enabled = false
		};
		options.Controls.Add(TimetableFilterPanel.Group(FilterText("Czas pobrania\ndanych z bazy:"), timetableDay));
		options.Controls.Add(TimetableFilterPanel.Group(FilterText("Informacje dodatkowe:")));
		search = Ui.Text("", multiline: false);
		search.Font = ReferenceTheme.Small;
		search.Width = 56;
		SilverButton clear = new SilverButton
		{
			Text = "✎",
			Size = new Size(18, 18),
			Font = ReferenceTheme.Small
		};
		clear.Click += (object s, EventArgs e) =>
		{
			search.Clear();
		};
		FlowLayoutPanel flowLayoutPanel = TimetableFilterPanel.Group(FilterText("Nr pociągu:"), search, clear);
		flowLayoutPanel.Layout += (object s, LayoutEventArgs e) =>
		{
			if (clear.Height != search.Height || clear.Width != search.Height)
			{
				clear.Size = new Size(search.Height, search.Height);
			}
		};
		clear.Size = new Size(search.Height, search.Height);
		searchRow.Controls.Add(flowLayoutPanel);
		kindFilter = Ui.Combo(new string[3] { "Wszystkie", "Pasażerskie", "Towarowe" }, "Wszystkie", editable: false);
		runningOnly = new CheckBox();
		notConfirmed = new CheckBox();
		confirmationLink = new LinkLabel
		{
			Text = "Potwierdzenie: wszystkie",
			AutoSize = true,
			Font = ReferenceTheme.Small,
			LinkColor = Color.FromArgb(98, 98, 255)
		};
		confirmationLink.LinkClicked += (object s, LinkLabelLinkClickedEventArgs e) =>
		{
			ShowFilterMenu(confirmationLink, new string[3] { "wszystkie", "potwierdzone", "niepotwierdzone" }, (int choice) =>
			{
				confirmationFilter = choice;
			}, "Potwierdzenie: ");
		};
		searchRow.Controls.Add(TimetableFilterPanel.Group(confirmationLink));
		movementLink = new LinkLabel
		{
			Text = "Pociągi: wszystkie",
			AutoSize = true,
			Font = ReferenceTheme.Small,
			LinkColor = Color.FromArgb(98, 98, 255)
		};
		movementLink.LinkClicked += (object s, LinkLabelLinkClickedEventArgs e) =>
		{
			ShowFilterMenu(movementLink, new string[5] { "wszystkie", "uruch. + kończ.", "uruchamiane", "kończące", "kursujące" }, (int choice) =>
			{
				movementFilter = choice;
			}, "Pociągi: ");
		};
		searchRow.Controls.Add(TimetableFilterPanel.Group(movementLink));
		CheckBox presentDates = new CheckBox
		{
			Text = "Prezentuj daty",
			Checked = true,
			AutoSize = true,
			Font = ReferenceTheme.Small
		};
		presentDates.CheckedChanged += (object s, EventArgs e) =>
		{
			trains.Tag = presentDates.Checked;
			ApplyDateColumnWidths();
			RefreshTrains();
		};
		searchRow.Controls.Add(TimetableFilterPanel.Group(presentDates));
		BuildTd2DirectionFilter(searchRow);
		operationsNotes = NoteBox();
		ownNotes = NoteBox();
		carrierNotes = NoteBox();
		Control[] array = new Control[3] { operationsNotes, ownNotes, carrierNotes };
		string[] array2 = new string[3] { "Uwagi\nekspl.:", "Uwagi\nwłasne:", "Uwagi\nPrzew.:" };
		for (int num = 0; num < 3; num++)
		{
			filterPanel.Notes.Controls.Add(FilterText(array2[num]), num * 2, 0);
			filterPanel.Notes.Controls.Add(array[num], num * 2 + 1, 0);
		}
		filterInfo = new AdditionalTrainInfo
		{
			Dock = DockStyle.Fill,
			Font = ReferenceTheme.Small,
			ForeColor = EdrPalette.Navy,
			AutoEllipsis = true,
			TextAlign = ContentAlignment.TopLeft,
			Padding = new Padding(0, 2, 0, 0)
		};
		filterPanel.Info.Controls.Add(filterInfo);
		search.TextChanged += (object s, EventArgs e) =>
		{
			RefreshTrains();
		};
		kindFilter.SelectedIndexChanged += (object s, EventArgs e) =>
		{
			RefreshTrains();
		};
		runningOnly.CheckedChanged += (object s, EventArgs e) =>
		{
			RefreshTrains();
		};
		notConfirmed.CheckedChanged += (object s, EventArgs e) =>
		{
			RefreshTrains();
		};
		timetableDay.ValueChanged += (object s, EventArgs e) =>
		{
			foreach (JournalPane pane in panes)
			{
				if (pane.SelectedDay != timetableDay.Value.Date)
				{
					pane.SetDay(timetableDay.Value);
				}
			}
			RefreshTrains();
		};
		currentData.CheckedChanged += (object s, EventArgs e) =>
		{
			timetableDay.Enabled = !currentData.Checked;
			if (currentData.Checked)
			{
				timetableDay.Value = Engine.Now;
			}
		};
		stations.SelectedIndexChanged += (object s, EventArgs e) =>
		{
			if (!filling && stations.SelectedIndex >= 0)
			{
				if (!FlushJournals())
				{
					filling = true;
					stations.SelectedIndex = Engine.State.Stations.FindIndex((Station x) => x.Id == Engine.StationId);
					filling = false;
				}
				else
				{
					Station station = Engine.State.Stations[stations.SelectedIndex];
					if (Engine.SimRail != null)
					{
						if (sendingHours || outgoingHours.Count > 0 || simulatorRefreshing || SharedSessionBusy)
						{
							filling = true;
							stations.SelectedIndex = Engine.State.Stations.FindIndex((Station x) => x.Id == Engine.StationId);
							filling = false;
							return;
						}
						if (backgroundWriter != null)
						{
							backgroundWriter.Dispose();
							backgroundWriter = null;
						}
						if (!TrySwitchSimulatorStation(station.Id))
						{
							return;
						}
						if (currentData.Checked)
						{
							timetableDay.Value = Engine.Now;
						}
					}
					else
					{
						Engine.StationId = station.Id;
					}
					selectedTrainId = null;
					BuildJournals();
					RefreshData();
					if (sessionWindows != null)
					{
						sessionWindows.ResumeOrphans(this);
					}
				}
			}
		};
		return filterPanel;
	}

	private void ShowFilterMenu(LinkLabel link, string[] labels, Action<int> choose, string prefix)
	{
		ContextMenuStrip contextMenuStrip = link.ContextMenuStrip;
		if (contextMenuStrip == null)
		{
			contextMenuStrip = new ContextMenuStrip();
			link.ContextMenuStrip = contextMenuStrip;
			for (int i = 0; i < labels.Length; i++)
			{
				int value = i;
				contextMenuStrip.Items.Add(Ui.Menu(labels[i], () =>
				{
					choose(value);
					link.Text = prefix + labels[value];
					filterPanel.Reflow();
					RefreshTrains();
				}));
			}
			ContextMenuStrip owned = contextMenuStrip;
			link.Disposed += (object s, EventArgs e) =>
			{
				owned.Dispose();
			};
		}
		contextMenuStrip.Show(link, new Point(0, link.Height));
	}

	private void ApplyDateColumnWidths()
	{
		FitPhotoColumns();
	}

	private void RememberColumnWidths(DataGridView grid)
	{
		grid.ColumnWidthChanged += (object s, DataGridViewColumnEventArgs e) =>
		{
			if (scaleApplied && !applyingScale && !fittingColumns)
			{
				manualColumnWidths[e.Column] = (float)e.Column.Width / (CurrentZoom / 1.375f);
			}
		};
	}

	private void FitDefaultColumns(DataGridView grid, DataGridViewColumn[] columns, int[] defaults, int[] stretch)
	{
		if (applyingScale || fittingColumns || grid.IsDisposed || grid.ClientSize.Width <= 0)
		{
			return;
		}
		float num = CurrentZoom / 1.375f;
		int[] array = new int[columns.Length];
		for (int i = 0; i < columns.Length; i++)
		{
			float num2 = (manualColumnWidths.TryGetValue(columns[i], out var value) ? value : ((float)defaults[i]));
			array[i] = Math.Max(18, (int)Math.Round(num2 * num));
		}
		int num3 = grid.ClientSize.Width - 2 - SystemInformation.VerticalScrollBarWidth - (grid.RowHeadersVisible ? grid.RowHeadersWidth : 0);
		int num4 = Math.Max(0, num3 - array.Sum());
		int[] array2 = stretch.Where((int num13) => columns[num13].Visible && !manualColumnWidths.ContainsKey(columns[num13])).ToArray();
		int num5 = array2.Sum((int num13) => defaults[num13]);
		int num6 = 0;
		int num7 = 0;
		int[] array3 = array2;
		foreach (int num9 in array3)
		{
			num7 += defaults[num9];
			int num10 = (int)((long)num4 * (long)num7 / num5);
			array[num9] += num10 - num6;
			num6 = num10;
		}
		fittingColumns = true;
		try
		{
			for (int num11 = 0; num11 < columns.Length; num11++)
			{
				columns[num11].MinimumWidth = Math.Max(18, (int)Math.Round(18f * num));
				int num12 = Math.Max(columns[num11].MinimumWidth, array[num11]);
				if (columns[num11].Width != num12)
				{
					columns[num11].Width = num12;
				}
			}
		}
		finally
		{
			fittingColumns = false;
		}
	}

	private void FitRouteColumns()
	{
		if (scaleApplied && !applyingScale && detailGrids.TryGetValue("route", out var value) && scaleSpecs.TryGetValue(value, out var value2))
		{
			DataGridViewColumn[] array = value.Columns.Cast<DataGridViewColumn>().ToArray();
			FitDefaultColumns(value, array, value2.Columns.Select((int x) => (int)Math.Round((float)x * 1.25f)).ToArray(), Enumerable.Range(0, array.Length).ToArray());
		}
	}

	private void BuildDescription()
	{
		Panel panel = DetailBody("Opis pociągu");
		panel.AutoScroll = true;
		descriptionSurface = new Panel
		{
			Location = new Point(8, 5),
			Size = new Size(1020, 336),
			BackColor = Color.White,
			Margin = Padding.Empty
		};
		panel.Controls.Add(descriptionSurface);
		string[] array = new string[9] { "Number", "InternationalNumber", "Name", "OrderNumber", "Kind", "Carrier", "Type", "Origin", "Destination" };
		string[] array2 = new string[9] { "Numer pociągu:", "Numer międzynarodowy:", "Nazwa pociągu:", "Numer zamówienia:", "Rodzaj pociągu:", "Przewoźnik:", "Typ pociągu:", "Stacja początkowa:", "Stacja końcowa:" };
		for (int i = 0; i < array.Length; i++)
		{
			bool flag = i == 0 || i == 1 || i == 3 || i == 4;
			DescriptionField(array[i], array2[i], 14, 7 + i * 21, 174, flag ? 158 : 245);
		}
		DescriptionField("TimetableType", "Rodzaj rozkładu jazdy:", 14, 204, 174, 245);
		DescriptionField("ApplicationNumber", "Nr wniosku:", 14, 225, 174, 245);
		DescriptionField("ApplicationDate", "Data wniosku:", 14, 246, 174, 245);
		string[] array3 = new string[5] { "Additional", "Cargo", "CarrierNotes", "OperationalNotes", "Notes" };
		string[] array4 = new string[5] { "Informacje dodatkowe:", "Ładunek:", "Uwagi przewoźnika:", "Uwagi eksploatacyjne:", "Uwagi własne:" };
		for (int j = 0; j < array3.Length; j++)
		{
			int num = 5 + j * 46;
			DescriptionLabel(array3[j], array4[j], 480, num, 516, 15);
			DescriptionBox(array3[j], 480, num + 15, 516, 29, multiline: true);
		}
		string[] array5 = new string[7] { "Przekroczona skrajnia", "Przekroczony nacisk", "Inne nadzwyczajne", "TWR", "TN", "Zamknięciowy", "Śledzony" };
		int[,] array6 = new int[7, 2]
		{
			{ 480, 241 },
			{ 480, 258 },
			{ 480, 275 },
			{ 780, 241 },
			{ 913, 241 },
			{ 480, 317 },
			{ 780, 317 }
		};
		for (int k = 0; k < array5.Length; k++)
		{
			CheckBox checkBox = new CheckBox();
			checkBox.Text = array5[k];
			checkBox.AutoCheck = false;
			checkBox.AutoSize = false;
			CheckBox checkBox2 = checkBox;
			int num2 = array6[k, 0];
			int num3 = array6[k, 1];
			int num4;
			if (k < 3)
			{
				num4 = 230;
			}
			else
			{
				num4 = ((k == 4) ? 80 : 125);
			}
			checkBox2.Bounds = new Rectangle(num2, num3, num4, 17);
			checkBox.Font = ReferenceTheme.Small;
			checkBox.ForeColor = Color.Navy;
			checkBox.Margin = Padding.Empty;
			CheckBox value = checkBox;
			profileFlags[array5[k]] = value;
			descriptionSurface.Controls.Add(value);
		}
		DescriptionField("Applicant", "Wnioskodawca:", 14, 295, 174, 808);
		DescriptionField("Withdrawal", "Rezygnacja:", 14, 317, 89, 40);
		DescriptionField("WithdrawalDate", "Data rezygnacji:", 215, 317, 110, 108);
		TextBox textBox = profileBoxes["Withdrawal"];
		Color backColor = (profileBoxes["WithdrawalDate"].BackColor = Color.FromArgb(242, 212, 181));
		textBox.BackColor = backColor;
		ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
		contextMenuStrip.Items.Add(Ui.Menu("Edytuj lokalny opis pociągu…", () =>
		{
			EditProfile();
		}));
		panel.ContextMenuStrip = contextMenuStrip;
		descriptionSurface.ContextMenuStrip = contextMenuStrip;
		foreach (TextBox value2 in profileBoxes.Values)
		{
			value2.ContextMenuStrip = contextMenuStrip;
		}
		descriptionReady = true;
		panel.SizeChanged += (object sender, EventArgs args) =>
		{
			ReflowDescription();
		};
		ReflowDescription();
	}

	private void DescriptionField(string key, string title, int x, int y, int labelWidth, int valueWidth)
	{
		DescriptionLabel(key, title, x, y, labelWidth, 18);
		DescriptionBox(key, x + labelWidth + 5, y, valueWidth, 18, multiline: false);
	}

	private void DescriptionLabel(string key, string title, int x, int y, int width, int height)
	{
		profileTitles[key] = title;
		Label label = new Label();
		label.Text = title;
		label.Bounds = new Rectangle(x, y, width, height);
		label.Font = ReferenceTheme.SmallBold;
		label.ForeColor = Color.Navy;
		label.TextAlign = ContentAlignment.MiddleLeft;
		label.Margin = Padding.Empty;
		Label value = label;
		profileLabels[key] = value;
		descriptionSurface.Controls.Add(value);
	}

	private void DescriptionBox(string key, int x, int y, int width, int height, bool multiline)
	{
		TextBox textBox = new TextBox();
		textBox.ReadOnly = true;
		textBox.Multiline = multiline;
		textBox.Bounds = new Rectangle(x, y, width, height);
		textBox.BackColor = Ui.Cream;
		textBox.BorderStyle = BorderStyle.FixedSingle;
		textBox.Font = ReferenceTheme.Small;
		textBox.Margin = Padding.Empty;
		textBox.ScrollBars = ScrollBars.None;
		TextBox value = textBox;
		descriptionSurface.Controls.Add(value);
		profileBoxes[key] = value;
	}

	private void Configure()
	{
		if (!FlushJournals())
		{
			return;
		}
		Engine.RequireCoordinator();
		using StationConfigurationForm stationConfigurationForm = new StationConfigurationForm(Engine);
		if (stationConfigurationForm.ShowDialog(this) == DialogResult.OK)
		{
			filling = true;
			stations.Items.Clear();
			stations.Items.AddRange(Engine.State.Stations.Select(StationDisplay).Cast<object>().ToArray());
			stations.SelectedIndex = Engine.State.Stations.FindIndex((Station x) => x.Id == Engine.StationId);
			filling = false;
			BuildJournals();
			RefreshData();
		}
	}

	private void NewStation()
	{
		Engine.RequireCoordinator();
		using Editor editor = new Editor("Nowy posterunek ruchu", 620, 360);
		TextBox name = editor.Add("Nazwa posterunku", Ui.Text("", multiline: false), 35);
		TextBox first = editor.Add("Sąsiedni posterunek 1", Ui.Text("", multiline: false), 35);
		TextBox second = editor.Add("Sąsiedni posterunek 2", Ui.Text("", multiline: false), 35);
		TextBox line = editor.Add("Numer linii", Ui.Text("1", multiline: false), 35);
		editor.Save(() =>
		{
			string n = Ui.Required(name, "Nazwa");
			if (Engine.State.Stations.Any((Station x) => x.Name.Equals(n, StringComparison.CurrentCultureIgnoreCase)))
			{
				throw new Exception("Taki posterunek już istnieje.");
			}
			string text = Ui.Required(first, "Sąsiedni posterunek 1");
			string text2 = Ui.Required(second, "Sąsiedni posterunek 2");
			if (text == text2)
			{
				throw new Exception("Wpisz dwóch różnych sąsiadów.");
			}
			string line2 = Ui.Required(line, "Numer linii");
			Station st = new Station
			{
				Id = Guid.NewGuid().ToString("N"),
				Name = n,
				Tracks = new List<string> { "1", "2" },
				Checks = new List<string> { "Sprawdzenie łączności" },
				Sections = new List<Section>
				{
					new Section
					{
						Id = Guid.NewGuid().ToString("N"),
						Name = text,
						Line = line2,
						Tracks = 1,
						ArrivalEven = true,
						Basis = "Zapowiadanie telefoniczne"
					},
					new Section
					{
						Id = Guid.NewGuid().ToString("N"),
						Name = text2,
						Line = line2,
						Tracks = 1,
						ArrivalEven = false,
						Basis = "Zapowiadanie telefoniczne"
					}
				}
			};
			Engine.Change("Utworzenie posterunku", st.Id, (AppState s) =>
			{
				s.Stations.Add(st);
			});
			filling = true;
			stations.Items.Add(StationDisplay(st));
			filling = false;
		});
		editor.ShowDialog(this);
	}

	private void Users()
	{
		Engine.RequireCoordinator();
		ReferenceForm referenceForm = new ReferenceForm();
		referenceForm.Text = "Użytkownicy lokalni";
		referenceForm.Font = ReferenceTheme.Dialog;
		referenceForm.ClientSize = new Size(750, 415);
		referenceForm.StartPosition = FormStartPosition.CenterParent;
		using (ReferenceForm referenceForm2 = referenceForm)
		{
			DataGridView g = Ui.Grid(journal: false);
			Ui.Column(g, "id", "Identyfikator", 160);
			Ui.Column(g, "name", "Imię i nazwisko", 290);
			Ui.Column(g, "role", "Rola", 160);
			Action refresh = () =>
			{
				g.Rows.Clear();
				foreach (LocalUser user in Engine.State.Users)
				{
					int index = g.Rows.Add(user.Id, user.Name, user.Role);
					g.Rows[index].Tag = user;
				}
			};
			refresh();
			FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel();
			flowLayoutPanel.Dock = DockStyle.Top;
			flowLayoutPanel.Height = 38;
			FlowLayoutPanel flowLayoutPanel2 = flowLayoutPanel;
			flowLayoutPanel2.Controls.Add(Ui.Button("Dodaj…", () =>
			{
				EditUser(null);
				refresh();
			}));
			flowLayoutPanel2.Controls.Add(Ui.Button("Edytuj…", () =>
			{
				if (g.CurrentRow != null)
				{
					EditUser((LocalUser)g.CurrentRow.Tag);
					refresh();
				}
			}));
			referenceForm2.Controls.Add(g);
			referenceForm2.Controls.Add(flowLayoutPanel2);
			referenceForm2.ShowDialog(this);
		}
		Engine.User = Engine.State.Users.First((LocalUser x) => x.Id == Engine.User.Id);
		RefreshData();
	}

	private void EditUser(LocalUser original)
	{
		Engine.RequireCoordinator();
		using Editor editor = new Editor("Użytkownik lokalny", 610, 380);
		TextBox id = editor.Add("Identyfikator", Ui.Text((original == null) ? "" : original.Id, multiline: false), 35);
		id.ReadOnly = original != null;
		TextBox name = editor.Add("Imię i nazwisko", Ui.Text((original == null) ? "" : original.Name, multiline: false), 35);
		ComboBox role = editor.Add("Rola", Ui.Combo(new string[3] { "Koordynator", "Operator", "Obserwator" }, (original == null) ? "Operator" : original.Role, editable: false), 35);
		TextBox pw = editor.Add("Nowe hasło lokalne", Ui.Text("", multiline: false), 35);
		pw.UseSystemPasswordChar = true;
		editor.Info("Puste hasło przy edycji pozostawia dotychczasowe.\nRole i uprawnienia w tej wersji dotyczą wszystkich lokalnych posterunków.", 55);
		editor.Save(() =>
		{
			string identifier = Ui.Required(id, "Identyfikator");
			string name2 = Ui.Required(name, "Imię i nazwisko");
			if (original == null && Engine.State.Users.Any((LocalUser x) => x.Id.Equals(identifier, StringComparison.OrdinalIgnoreCase)))
			{
				throw new Exception("Ten identyfikator jest już zajęty.");
			}
			if (original != null && original.Role == "Koordynator" && role.Text != "Koordynator" && Enumerable.Count(Engine.State.Users, (LocalUser x) => x.Role == "Koordynator") == 1)
			{
				throw new Exception("Musi pozostać co najmniej jeden koordynator.");
			}
			if (original != null && Engine.State.Duties.Any((Duty x) => x.UserId == original.Id && !x.End.HasValue) && role.Text == "Obserwator")
			{
				throw new Exception("Najpierw zakończ dyżur tego użytkownika.");
			}
			LocalUser u = ((original == null) ? new LocalUser
			{
				Id = identifier
			} : Json.Clone(original));
			u.Name = name2;
			u.Role = role.Text;
			if (original == null || pw.Text.Length > 0)
			{
				u.SetPassword(pw.Text);
			}
			Engine.Change("Użytkownik lokalny", identifier, (AppState s) =>
			{
				s.Users.RemoveAll((LocalUser x) => x.Id == identifier);
				s.Users.Add(u);
			});
		});
		editor.ShowDialog(this);
	}

	private void ConfiguredCrossings(JournalEntry row, Section section)
	{
		using CrossingNotificationForm crossingNotificationForm = new CrossingNotificationForm(Engine, row, section);
		if (crossingNotificationForm.ShowDialog(this) == DialogResult.OK)
		{
			RefreshData();
		}
	}

	private bool TrySwitchSimulatorStation(string id)
	{
		try
		{
			Engine.SwitchSimulatorStation(id);
			if (!string.IsNullOrEmpty(Engine.ConfigurationProfileWarning))
			{
				MessageBox.Show(this, Engine.ConfigurationProfileWarning, "Konfiguracja posterunku", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
			return true;
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			filling = true;
			stations.SelectedIndex = Engine.State.Stations.FindIndex((Station x) => x.Id == Engine.StationId);
			filling = false;
			Ui.Run(this, () =>
			{
				throw new InvalidOperationException("Nie udało się przełączyć posterunku. " + ex2.Message);
			});
			return false;
		}
	}

	private static bool IsPassenger(string category)
	{
		string text = (category ?? "").Trim().ToUpperInvariant();
		if (!text.StartsWith("M") && !text.StartsWith("E"))
		{
			return text.StartsWith("R");
		}
		return true;
	}

	private static void Mark(DataGridViewCell cell, Color back, Color ink)
	{
		DataGridViewCellStyle style = cell.Style;
		Color backColor = (cell.Style.SelectionBackColor = back);
		style.BackColor = backColor;
		DataGridViewCellStyle style2 = cell.Style;
		Color foreColor = (cell.Style.SelectionForeColor = ink);
		style2.ForeColor = foreColor;
	}

	private void ApplyManualTimetablePalette(DataGridViewRow row, Train train)
	{
		row.DefaultCellStyle.ForeColor = Color.Black;
		row.DefaultCellStyle.SelectionBackColor = EdrPalette.TimetableBand(train, Engine.Station.Name);
		row.DefaultCellStyle.SelectionForeColor = ((row.DefaultCellStyle.SelectionBackColor == EdrPalette.Cyan || row.DefaultCellStyle.SelectionBackColor == Color.Lime) ? Color.Black : Color.White);
		string[] array = new string[3] { "to", "line", "track" };
		foreach (string text in array)
		{
			row.Cells[text].Style.SelectionBackColor = row.Cells[text].Style.BackColor;
			row.Cells[text].Style.SelectionForeColor = ((text == "track") ? Color.Black : Color.Red);
		}
		if (!train.Runs)
		{
			foreach (DataGridViewCell cell in row.Cells)
			{
				cell.ToolTipText = "Pociąg niekursujący - brak znacznika K.";
			}
		}
		if (train.SourceProvider != "SimRail" && (train.Arrival.HasValue || train.Departure.HasValue))
		{
			DataGridViewCell dataGridViewCell2 = row.Cells["kind"];
			dataGridViewCell2.Style.ForeColor = Color.White;
			dataGridViewCell2.Style.SelectionBackColor = Color.Green;
			dataGridViewCell2.Style.SelectionForeColor = Color.White;
		}
		if (train.Dangerous)
		{
			Mark(row.Cells["from"], EdrPalette.Dangerous, Color.White);
		}
		if (train.HighRisk)
		{
			Mark(row.Cells["to"], EdrPalette.HighRisk, Color.Black);
		}
		if (train.Extraordinary)
		{
			string[] array2 = new string[2] { "odd", "even" };
			foreach (string columnName in array2)
			{
				if (!string.IsNullOrWhiteSpace(Convert.ToString(row.Cells[columnName].Value)))
				{
					Mark(row.Cells[columnName], Color.Yellow, Color.Black);
				}
			}
		}
		if (train.QualityTracked)
		{
			Mark(row.Cells["da"], EdrPalette.Quality, Color.Black);
		}
		if (train.Stopped)
		{
			Mark(row.Cells["rd"], train.Forecast.HasValue ? Color.Orange : Color.Pink, Color.Black);
		}
		if (train.Profile != null && !string.IsNullOrWhiteSpace(train.Profile.CarrierNotes) && IsPassenger(train.Kind))
		{
			Mark(row.Cells["carrier"], Color.Yellow, Color.Black);
		}
		if (train.Profile != null && !string.IsNullOrWhiteSpace(train.Profile.OperationalNotes))
		{
			row.Cells["k"].Style.BackColor = Color.FromArgb(255, 83, 83);
			row.Cells["k"].Style.SelectionBackColor = Color.FromArgb(255, 83, 83);
		}
	}

	private void ResetLocalSession()
	{
		Engine.RequireCoordinator();
		if (sendingHours || simulatorRefreshing || outgoingHours.Count > 0)
		{
			throw new InvalidOperationException("Poczekaj na zakończenie odświeżania i wysyłania godzin.");
		}
		if (MessageBox.Show(this, "Wyczyścić wszystkie lokalne wpisy, dyżury i usługi bieżącego posterunku?\nPowstanie kopia sprzed resetu. Ustawienia i godziny w SEPE pozostaną.", "Wyczyść lokalną sesję", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != DialogResult.Yes)
		{
			return;
		}
		string text = Engine.ResetSimRailJournals();
		foreach (JournalPane pane in panes)
		{
			pane.DiscardPending();
		}
		outgoingHours.Clear();
		selectedTrainId = null;
		BuildJournals();
		RefreshData();
		MessageBox.Show(this, "Wyczyszczono sesję. Kopia odzyskiwania:\n" + text, "Wyczyść lokalną sesję");
	}

	private void SessionClosing(object sender, FormClosingEventArgs e)
	{
		if (closingSession)
		{
			return;
		}
		if (!LocalPersistence.Enabled)
		{
			if (!FlushJournals())
			{
				e.Cancel = true;
				return;
			}
			closingSession = true;
			simulatorCancellation.Cancel();
			if (backgroundWriter != null)
			{
				backgroundWriter.Dispose();
				backgroundWriter = null;
			}
			return;
		}
		if (Engine.SimRail == null)
		{
			if (!FlushJournals())
			{
				e.Cancel = true;
				return;
			}
			try
			{
				Engine.Store.Flush();
			}
			catch (Exception ex)
			{
				e.Cancel = true;
				MessageBox.Show(this, "Nie udało się zapisać danych: " + ex.Message, "Zapis danych", MessageBoxButtons.OK, MessageBoxIcon.Hand);
				return;
			}
			if (!Engine.Store.IsTransient && DialogResult != DialogResult.Retry && Engine.ActiveDuty != null && Engine.ActiveDuty.UserId == Engine.User.Id && MessageBox.Show(this, "Dyżur pozostaje otwarty i będzie można go kontynuować po ponownym uruchomieniu.\n\nZamknąć aplikację?", "Zakończenie pracy", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
			{
				e.Cancel = true;
			}
			return;
		}
		decidingExit = true;
		try
		{
			ExitSessionChoice choice = ExitSessionChoice.Save;
			if (!Ui.Testing)
			{
				using ExitSessionForm exitSessionForm = new ExitSessionForm(Engine.IsCoordinator, sendingHours || outgoingHours.Count > 0 || Engine.State.Trains.Any((Train x) => x.ExternalManualTimes));
				exitSessionForm.ShowDialog(this);
				choice = exitSessionForm.Choice;
			}
			if (!FinishSession(choice))
			{
				e.Cancel = true;
			}
		}
		catch (Exception ex2)
		{
			e.Cancel = true;
			MessageBox.Show(this, ex2.Message, "Zakończenie pracy", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
		finally
		{
			decidingExit = false;
			if (e.Cancel)
			{
				DialogResult = DialogResult.None;
				if (!hoursPaused && IsHandleCreated && !IsDisposed)
				{
					BeginInvoke(new Action(ProcessSepeWrites));
				}
			}
		}
	}

	internal bool FinishSession(ExitSessionChoice choice)
	{
		switch (choice)
		{
		case ExitSessionChoice.Cancel:
			return false;
		case ExitSessionChoice.Save:
			if (!FlushJournals())
			{
				return false;
			}
			Engine.Store.Flush();
			Engine.PersistStationProfile();
			break;
		case ExitSessionChoice.Discard:
			foreach (JournalPane pane in panes)
			{
				pane.DiscardPending();
			}
			break;
		default:
			if (sendingHours)
			{
				throw new InvalidOperationException("Trwa wysyłanie godzin. Poczekaj na zakończenie operacji przed resetem dzienników.");
			}
			Engine.ResetSimRailJournals();
			foreach (JournalPane pane2 in panes)
			{
				pane2.DiscardPending();
			}
			outgoingHours.Clear();
			break;
		}
		closingSession = true;
		simulatorCancellation.Cancel();
		if (backgroundWriter != null)
		{
			backgroundWriter.Dispose();
			backgroundWriter = null;
		}
		return true;
	}

	public void QuickRegister(Train t, string section)
	{
		if (!FlushJournals())
		{
			return;
		}
		JournalEntry row = Engine.RegisterScheduledTrain(t.Id, section);
		if (row != null)
		{
			RefreshData();
			JournalPane journalPane = panes.FirstOrDefault((JournalPane p) => p.SectionId == row.SectionId);
			if (journalPane != null)
			{
				journalPane.SetDay(row.Time);
				journalPane.FocusEntry(row.Id);
			}
		}
	}

	public bool FlushJournals()
	{
		foreach (JournalPane pane in panes)
		{
			if (!pane.CommitPending())
			{
				return false;
			}
		}
		return true;
	}

	public void JournalCommitted()
	{
		RefreshTrains();
		RefreshTracks();
		foreach (JournalPane pane in panes)
		{
			pane.RefreshSignals();
		}
	}

	public MainForm(Engine engine)
		: this(engine, null)
	{
	}

	public MainForm(Engine engine, AccidentCounterClient counter)
	{
		accidentCounter = counter ?? CreateAccidentCounter(engine.IsTd2 ? "td2" : "simrail");
		Disposed += (object s, EventArgs e) =>
		{
			counterCancellation.Cancel();
		};
		Engine = engine;
		Font = ReferenceTheme.Small;
		AutoScaleMode = AutoScaleMode.None;
		MinimumSize = new Size(800, 500);
		Size = new Size(1259, 708);
		StartPosition = FormStartPosition.CenterScreen;
		MenuStrip menuStrip = BuildMenu();
		AddSimulatorMenu(menuStrip);
		MainMenuStrip = menuStrip;
		menuStrip.Font = ReferenceTheme.Small;
		menuStrip.AutoSize = false;
		menuStrip.Height = 18;
		menuStrip.BackColor = Color.FromArgb(190, 190, 190);
		menuStrip.Padding = new Padding(0, 0, 260, 0);
		menuStrip.Renderer = new ReferenceStripRenderer();
		headerRow = new Panel
		{
			Dock = DockStyle.Top,
			Height = 18,
			BackColor = Color.FromArgb(190, 190, 190)
		};
		menuStrip.Dock = DockStyle.Fill;
		menuStrip.Padding = new Padding(0, 0, 198, 0);
		headerClock = new ReferenceClockBar(this);
		Controls.Add(headerClock);
		headerRow.Controls.Add(menuStrip);
		Resize += (object s, EventArgs e) =>
		{
			PositionPhotoHeader();
		};
		dutyButton = Ui.Tool("Przyjmij dyżur", "", () =>
		{
			DutyAction();
		});
		StatusStrip statusStrip = new StatusStrip
		{
			AutoSize = false,
			Height = 16,
			Font = ReferenceTheme.Small,
			SizingGrip = false,
			BackColor = Color.White,
			Padding = Padding.Empty
		};
		statusUser = new ToolStripStatusLabel();
		statusLogin = new ToolStripStatusLabel
		{
			TextAlign = ContentAlignment.MiddleLeft
		};
		statusDuty = new ToolStripStatusLabel
		{
			Spring = true,
			TextAlign = ContentAlignment.MiddleRight
		};
		statusUnit = new ToolStripStatusLabel();
		statusUnit.Click += (object s, EventArgs e) =>
		{
			Ui.Run(this, EditOrganizationUnit);
		};
		statusStrip.Items.Add(statusUser);
		statusStrip.Items.Add(statusUnit);
		statusStrip.Items.Add(statusLogin);
		statusStrip.Items.Add(statusDuty);
		tracks = new FlowLayoutPanel
		{
			Dock = DockStyle.Bottom,
			Height = 18,
			WrapContents = false,
			AutoScroll = false,
			BackColor = Color.FromArgb(196, 196, 196),
			Padding = Padding.Empty
		};
		tracks.SizeChanged += (object s, EventArgs e) =>
		{
			CenterTrackButtons();
		};
		split = new SplitContainer
		{
			Dock = DockStyle.Fill,
			Orientation = Orientation.Horizontal,
			SplitterWidth = 3,
			BackColor = Color.FromArgb(185, 185, 185),
			Margin = Padding.Empty
		};
		topTabs = ReferenceTheme.Tabs();
		split.Panel1.Controls.Add(topTabs);
		BuildTimetable();
		BuildDetails();
		BuildServices();
		HideSecondaryPages();
		topTabs.SelectedIndexChanged += (object s, EventArgs e) =>
		{
			PositionPhotoHeader();
			if (!filling)
			{
				RefreshDetails();
			}
		};
		journalTabs = ReferenceTheme.Tabs();
		split.Panel2.Controls.Add(journalTabs);
		BuildJournals();
		Controls.Add(split);
		Controls.Add(tracks);
		Controls.Add(statusStrip);
		Controls.Add(headerRow);
		RefreshData();
		HookSepeWrites();
		Shown += (object s, EventArgs e) =>
		{
			PollAccidentCounter();
			SetLayout(Engine.State.Layout ?? "Dół", persist: false);
			SetupScaling();
			PositionPhotoHeader();
			if (trains.CurrentCell != null && trains.DisplayedRowCount(includePartialRow: false) > 0)
			{
				trains.FirstDisplayedScrollingRowIndex = trains.CurrentCell.RowIndex;
			}
			if (!Ui.Testing && !additionalSessionView && Engine.SimRail != null && Engine.SimRail.HasWriteLink && Engine.SepeWritesEnabled && Engine.State.Trains.Any((Train x) => x.ExternalManualTimes))
			{
				RetrySepeWrites();
			}
		};
		nextRefresh = DateTime.Now.AddMinutes(refreshMinutes);
		timer = new System.Windows.Forms.Timer
		{
			Interval = 1000
		};
		timer.Tick += (object s, EventArgs e) =>
		{
			PollAccidentCounter();
			headerClock.Invalidate();
			if (outgoingHours.Count > 0 && !hoursPaused)
			{
				ProcessSepeWrites();
			}
			if (!closingSession && !decidingExit)
			{
				RefreshRuntimeIndicators();
			}
			if (!decidingExit && !closingSession && DateTime.Now >= nextRefresh)
			{
				Ui.Run(this, () =>
				{
					if (Engine.SimRail != null)
					{
						RefreshSimulator(interactive: false);
					}
					else
					{
						if (currentData.Checked)
						{
							timetableDay.Value = Engine.Now;
						}
						RefreshData();
					}
					nextRefresh = DateTime.Now.AddMinutes(refreshMinutes);
				});
			}
		};
		timer.Start();
		FormClosing += SessionClosing;
		FormClosed += (object s, FormClosedEventArgs e) =>
		{
			timer.Stop();
			timer.Dispose();
			simulatorCancellation.Cancel();
			if (detached != null)
			{
				detached.FormClosing -= DetachedClosing;
				detached.Close();
			}
		};
	}

	private MenuStrip BuildMenu()
	{
		MenuStrip menuStrip = new MenuStrip();
		ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem("Aplikacja");
		if (Engine.Store.IsTransient && Engine.SimRail == null)
		{
			toolStripMenuItem.DropDownItems.Add(Ui.Menu("Nowa sesja ćwiczeniowa", () =>
			{
				ResetTrainingSession();
			}));
		}
		if (Engine.SimRail == null)
		{
			toolStripMenuItem.DropDownItems.Add(Ui.Menu("Importuj rozkład z CSV…", () =>
			{
				ImportTimetable();
			}));
		}
		if (LocalPersistence.Enabled)
		{
			toolStripMenuItem.DropDownItems.Add(Ui.Menu("Eksportuj widoczny rozkład…", () =>
			{
				ExportTimetable();
			}));
		}
		if (Engine.SimRail == null)
		{
			toolStripMenuItem.DropDownItems.Add(Ui.Menu("Utwórz przykład na wybrany dzień", () =>
			{
				Engine.RequireCoordinator();
				Engine.Change("Dane przykładowe", Engine.StationId, (AppState s) =>
				{
					Store.AddDemo(s, Engine.StationId, timetableDay.Value);
				});
				RefreshData();
			}));
		}
		toolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
		if (LocalPersistence.Enabled)
		{
			AddPersistenceMenu(toolStripMenuItem);
			toolStripMenuItem.DropDownItems.Add(Ui.Menu("Kopia wszystkich danych…", () =>
			{
				Backup();
			}));
		}
		toolStripMenuItem.DropDownItems.Add(Ui.Menu("Wyloguj", LogoutSession));
		toolStripMenuItem.DropDownItems.Add(Ui.Menu("Zakończ", () =>
		{
			Close();
		}));
		menuStrip.Items.Add(toolStripMenuItem);
		ToolStripMenuItem toolStripMenuItem2 = new ToolStripMenuItem("Posterunek ruchu");
		toolStripMenuItem2.DropDownItems.Add(Ui.Menu("Przyjęcie / zdanie dyżuru…", () =>
		{
			DutyAction();
		}));
		toolStripMenuItem2.DropDownItems.Add(Ui.Menu("Bieżąca sytuacja ruchowa na szlakach", () =>
		{
			Situation();
		}));
		ToolStripMenuItem checks = Ui.Menu("Wymagane sprawdzenia…", () =>
		{
			CheckAction();
		});
		toolStripMenuItem2.DropDownItems.Add(checks);
		toolStripMenuItem2.DropDownOpening += (object s, EventArgs e) =>
		{
			int count = Engine.DueChecks().Count;
			checks.Text = "Wymagane sprawdzenia" + ((count > 0) ? (" (" + count + " oczekujących)") : "") + "…";
		};
		menuStrip.Items.Add(toolStripMenuItem2);
		ToolStripMenuItem toolStripMenuItem3 = new ToolStripMenuItem("Raporty");
		toolStripMenuItem3.DropDownItems.Add(Ui.Menu("Wyciąg z dziennika ruchu…", () =>
		{
			Report(telephone: false);
		}));
		toolStripMenuItem3.DropDownItems.Add(Ui.Menu("Wyciąg z dziennika telefonicznego…", () =>
		{
			Report(telephone: true);
		}));
		toolStripMenuItem3.DropDownItems.Add(Ui.Menu("Historia zmian", () =>
		{
			History(null);
		}));
		menuStrip.Items.Add(toolStripMenuItem3);
		ToolStripMenuItem toolStripMenuItem4 = new ToolStripMenuItem("Konfiguracja");
		toolStripMenuItem4.DropDownItems.Add(Ui.Menu("Konfiguracja posterunku…", () =>
		{
			Configure();
		}));
		if (Engine.SimRail == null)
		{
			toolStripMenuItem4.DropDownItems.Add(Ui.Menu("Nowy posterunek…", () =>
			{
				NewStation();
			}));
		}
		ToolStripMenuItem toolStripMenuItem5 = Ui.Menu("Użytkownicy lokalni…", () =>
		{
			Users();
		});
		toolStripMenuItem5.Visible = false;
		toolStripMenuItem4.DropDownItems.Add(toolStripMenuItem5);
		menuStrip.Items.Add(toolStripMenuItem4);
		ToolStripMenuItem toolStripMenuItem6 = new ToolStripMenuItem("Widok");
		toolStripMenuItem6.DropDownItems.Add(Ui.Menu("Dzienniki na dole", () =>
		{
			SetLayout("Dół", persist: true);
		}));
		toolStripMenuItem6.DropDownItems.Add(Ui.Menu("Dzienniki z prawej", () =>
		{
			SetLayout("Prawo", persist: true);
		}));
		toolStripMenuItem6.DropDownItems.Add(Ui.Menu("Dzienniki w osobnym oknie", () =>
		{
			SetLayout("Okno", persist: true);
		}));
		toolStripMenuItem6.DropDownItems.Add(new ToolStripSeparator());
		toolStripMenuItem6.DropDownItems.Add(Ui.Menu("Otwórz drugie okno", OpenSessionWindow));
		menuStrip.Items.Add(toolStripMenuItem6);
		ToolStripMenuItem toolStripMenuItem7 = new ToolStripMenuItem("Dodatki WRJ");
		toolStripMenuItem7.DropDownItems.Add(Ui.Menu("Wydawnictwa WRJ - otwórz lokalny PDF…", () =>
		{
			using OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Title = "Wybierz lokalne wydawnictwo WRJ",
				Filter = "Dokumenty PDF|*.pdf"
			};
			if (openFileDialog.ShowDialog(this) == DialogResult.OK)
			{
				Process.Start(openFileDialog.FileName);
			}
		}));
		menuStrip.Items.Add(toolStripMenuItem7);
		ToolStripMenuItem toolStripMenuItem8 = new ToolStripMenuItem("Komunikaty");
		toolStripMenuItem8.DropDownItems.Add(Ui.Menu("Informacje o środowisku", () =>
		{
			MessageBox.Show(this, "EDR\n\n" + ((Engine.SimRail == null) ? "Tryb lokalny / CSV." : (Engine.SimulatorCaption + ": " + Engine.SimulatorServerName + " / " + Engine.Station.Name + (Engine.IsTd2 ? ".\nAPI TD2 dostarcza rozkład jazdy. Wpisy dziennika pozostają w EDR." : ".\nAPI dostarcza rozkład i czas. Edycja zewnętrzna odbywa się w oficjalnym SEPE."))) + "\nBieżące dane EDR są przechowywane tylko w pamięci.\nBrak połączenia z systemami PKP PLK.\n\nFolder danych:\n" + Engine.Store.DirectoryPath, "Komunikaty", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}));
		menuStrip.Items.Add(toolStripMenuItem8);
		ToolStripMenuItem toolStripMenuItem9 = new ToolStripMenuItem("Pomoc");
		toolStripMenuItem9.DropDownItems.Add(Ui.Menu("Legenda", () =>
		{
			Legend();
		}));
		menuStrip.Items.Add(toolStripMenuItem9);
		return menuStrip;
	}

	public static string StationDisplay(Station station)
	{
		return station.Name.ToUpper() + " (" + StationIdentity.Type(station) + ")";
	}

	private void BuildTimetable()
	{
		TabPage tabPage = new TabPage("Wykaz pociągów");
		tabPage.Padding = Padding.Empty;
		tabPage.BackColor = Color.White;
		TabPage tabPage2 = tabPage;
		topTabs.TabPages.Add(tabPage2);
		Control value = BuildTimetableFilters();
		trains = Ui.Grid(journal: false);
		trains.ShowCellToolTips = false;
		((ReferenceGrid)trains).SelectWithWheel = true;
		ReferenceTheme.Grid(trains, journal: false);
		string[] array = new string[23]
		{
			"k", "nk", "pa", "da", "ra", "kind", "odd", "from", "even", "to",
			"track", "line", "orderedStay", "stay", "type", "pt", "pd", "dd", "rd", "origin",
			"destination", "carrier", "filler"
		};
		string[] array2 = new string[23]
		{
			"K", "NK", "Prz...", "+/-", "Prz...", "Rodz.", "Nr poc.", "Z kierunku post.", "Nr poc.", "W kierunku post.",
			"Tor", "Nr L.", "P. za...", "P. obl.", "Typ...", "P T", "Odj...", "+/-", "Odj...", "Stacja początkowa",
			"Stacja końcowa", "Przewoźnik", ""
		};
		int[] array3 = new int[23]
		{
			18, 18, 29, 24, 29, 26, 37, 98, 39, 98,
			20, 39, 27, 27, 25, 29, 29, 24, 29, 80,
			80, 80, 40
		};
		for (int i = 0; i < array.Length; i++)
		{
			if (i < 2)
			{
				trains.Columns.Add(new DataGridViewCheckBoxColumn
				{
					Name = array[i],
					HeaderText = array2[i],
					Width = array3[i],
					MinimumWidth = 16,
					ReadOnly = true,
					ThreeState = false,
					SortMode = DataGridViewColumnSortMode.NotSortable,
					FlatStyle = FlatStyle.Standard
				});
			}
			else
			{
				Ui.Column(trains, array[i], array2[i], array3[i]);
			}
		}
		trains.Columns["filler"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
		trains.Columns["filler"].DefaultCellStyle.SelectionBackColor = Color.FromArgb(255, 224, 160);
		trains.Columns["stay"].DefaultCellStyle.Format = "F1";
		trains.Columns["orderedStay"].DefaultCellStyle.Format = "F1";
		ConfigurePhotoTimetable();
		trains.CurrentCellChanged += (object s, EventArgs e) =>
		{
			if (!filling && trains.CurrentRow != null)
			{
				string text = (string)trains.CurrentRow.Tag;
				if (selectedTrainId != text)
				{
					selectedTrainId = text;
					QueueSelectionDetails();
				}
			}
		};
		trains.CellDoubleClick += (object s, DataGridViewCellEventArgs e) =>
		{
			if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && (trains.Columns[e.ColumnIndex].Name == "odd" || trains.Columns[e.ColumnIndex].Name == "even"))
			{
				Ui.Run(this, () =>
				{
					QuickRegister(Engine.State.Trains.First((Train t) => t.Id == (string)trains.Rows[e.RowIndex].Tag), null);
				});
			}
		};
		trains.KeyDown += (object s, KeyEventArgs e) =>
		{
			if (e.KeyCode == Keys.Return)
			{
				e.Handled = true;
				Ui.Run(this, () =>
				{
					TrainTimes();
				});
			}
		};
		trains.CellMouseDown += (object s, DataGridViewCellMouseEventArgs e) =>
		{
			if (e.RowIndex >= 0 && (e.Button == MouseButtons.Right || e.Button == MouseButtons.Left))
			{
				trains.CurrentCell = trains.Rows[e.RowIndex].Cells[Math.Max(0, e.ColumnIndex)];
				trains.ClearSelection();
				trains.Rows[e.RowIndex].Selected = true;
			}
		};
		ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
		contextMenuStrip.Items.Add(Ui.Menu("Przyjmij pociąg…", () =>
		{
			EditJournal(null, NeedTrain(), "Z", null);
		}));
		contextMenuStrip.Items.Add(Ui.Menu("Wypraw pociąg…", () =>
		{
			EditJournal(null, NeedTrain(), "DO", null);
		}));
		contextMenuStrip.Items.Add(Ui.Menu("Przyjmij z innego szlaku…", () =>
		{
			EditJournal(null, NeedTrain(), "Z", "*");
		}));
		contextMenuStrip.Items.Add(Ui.Menu("Wypraw na inny szlak…", () =>
		{
			EditJournal(null, NeedTrain(), "DO", "*");
		}));
		contextMenuStrip.Items.Add(new ToolStripSeparator());
		contextMenuStrip.Items.Add(Ui.Menu("Wprowadzanie godzin…", () =>
		{
			TrainTimes();
		}));
		if (Engine.SimRail != null)
		{
			ToolStripMenuItem send = Ui.Menu("Wyślij godziny do SEPE", () =>
			{
				TransferSepeHours();
			});
			send.Enabled = CanSendSepeHours;
			contextMenuStrip.Items.Add(send);
			contextMenuStrip.Opening += (object s, CancelEventArgs e) =>
			{
				send.Enabled = CanSendSepeHours;
			};
		}
		ToolStripMenuItem stopMenu = Ui.Menu("Poc. STÓJ + Prognoza…", () =>
		{
			TrainStop();
		});
		contextMenuStrip.Items.Add(stopMenu);
		contextMenuStrip.Opening += (object s, CancelEventArgs e) =>
		{
			Train selectedTrain = SelectedTrain;
			stopMenu.Text = ((selectedTrain != null && selectedTrain.Stopped) ? "Odwołaj pociąg STÓJ / Prognoza…" : "Poc. STÓJ + Prognoza…");
		};
		contextMenuStrip.Items.Add(Ui.Menu("Zgłoszenie gotowości…", () =>
		{
			TrainReady();
		}));
		contextMenuStrip.Items.Add(Ui.Menu("Analiza pociągu…", () =>
		{
			TrainAnalysis();
		}));
		contextMenuStrip.Items.Add(Ui.Menu("Towary niebezpieczne / TWR…", () =>
		{
			TrainCargo();
		}));
		contextMenuStrip.Items.Add(Ui.Menu("Uwagi własne…", () =>
		{
			TrainNotes();
		}));
		trains.ContextMenuStrip = contextMenuStrip;
		Point dragStart = Point.Empty;
		trains.MouseDown += (object s, MouseEventArgs e) =>
		{
			dragStart = e.Location;
		};
		trains.MouseMove += (object s, MouseEventArgs e) =>
		{
			if (e.Button == MouseButtons.Left && SelectedTrain != null && (Math.Abs(e.X - dragStart.X) > 8 || Math.Abs(e.Y - dragStart.Y) > 8))
			{
				DataGridView.HitTestInfo hitTestInfo = trains.HitTest(dragStart.X, dragStart.Y);
				if (hitTestInfo.RowIndex >= 0 && hitTestInfo.ColumnIndex >= 0 && (trains.Columns[hitTestInfo.ColumnIndex].Name == "odd" || trains.Columns[hitTestInfo.ColumnIndex].Name == "even"))
				{
					trains.DoDragDrop(SelectedTrain.Id, DragDropEffects.Copy);
				}
			}
		};
		tabPage2.Controls.Add(trains);
		tabPage2.Controls.Add(value);
	}

	private void HideSecondaryPages()
	{
		TabPage[] array = topTabs.TabPages.Cast<TabPage>().Skip(3).ToArray();
		foreach (TabPage tabPage in array)
		{
			topTabs.TabPages.Remove(tabPage);
			hiddenTimetableTabs.Add(tabPage);
		}
		Disposed += (object s, EventArgs e) =>
		{
			foreach (TabPage hiddenTimetableTab in hiddenTimetableTabs)
			{
				hiddenTimetableTab.Dispose();
			}
			hiddenTimetableTabs.Clear();
		};
	}

	private void BuildServices()
	{
		TabPage tabPage = new TabPage("Usługi");
		servicesGrid = Ui.Grid(journal: false);
		ReferenceTheme.Grid(servicesGrid, journal: false);
		servicesGrid.Tag = "detail";
		string[] array = new string[8] { "Rodzaj", "Pociąg", "Przewoźnik", "Tor", "Rozpoczęcie", "Zakończenie", "Status", "Uwagi" };
		foreach (string text in array)
		{
			Ui.Column(servicesGrid, text, text, (text == "Uwagi") ? 280 : 130);
		}
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel();
		flowLayoutPanel.Dock = DockStyle.Top;
		flowLayoutPanel.Height = 36;
		FlowLayoutPanel flowLayoutPanel2 = flowLayoutPanel;
		flowLayoutPanel2.Controls.Add(Ui.Button("Dodaj usługę…", () =>
		{
			ServiceEdit(null);
		}));
		flowLayoutPanel2.Controls.Add(Ui.Button("Edytuj…", () =>
		{
			if (servicesGrid.CurrentRow != null)
			{
				ServiceEdit((ServiceRecord)servicesGrid.CurrentRow.Tag);
			}
		}));
		flowLayoutPanel2.Controls.Add(Ui.Button("Brak zgody…", () =>
		{
			ServiceReject();
		}));
		servicesGrid.CellDoubleClick += (object s, DataGridViewCellEventArgs e) =>
		{
			if (e.RowIndex >= 0)
			{
				Ui.Run(this, () =>
				{
					ServiceEdit((ServiceRecord)servicesGrid.Rows[e.RowIndex].Tag);
				});
			}
		};
		tabPage.Controls.Add(servicesGrid);
		tabPage.Controls.Add(flowLayoutPanel2);
		topTabs.TabPages.Add(tabPage);
	}

	public void BuildJournals()
	{
		foreach (JournalPane pane in panes)
		{
			pane.Dispose();
		}
		panes.Clear();
		journalTabs.TabPages.Clear();
		Station station = Engine.Station;
		List<Section> sections = station.Sections.Where((Section x) => StationSettings.Conducted(x) || (station.Options != null && station.Options.JournalVisibility == 2)).ToList();
		List<List<Section>> list = new List<List<Section>>();
		HashSet<string> used = new HashSet<string>();
		foreach (JournalTabDefinition item in station.JournalTabs ?? new List<JournalTabDefinition>())
		{
			List<Section> list2 = (from id in new string[2] { item.LeftId, item.RightId }
				where !string.IsNullOrEmpty(id)
				select sections.FirstOrDefault((Section x) => x.Id == id) into x
				where x != null && used.Add(x.Id)
				select x).ToList();
			if (list2.Count > 0)
			{
				list.Add(list2);
			}
		}
		List<Section> list3 = sections.Where((Section x) => !used.Contains(x.Id)).ToList();
		int num = (((station.JournalTabs != null && station.JournalTabs.Count > 0) || split.Orientation == Orientation.Vertical) ? 1 : 2);
		for (int num2 = 0; num2 < list3.Count; num2 += num)
		{
			list.Add(list3.Skip(num2).Take(num).ToList());
		}
		foreach (List<Section> item2 in list)
		{
			TabPage tabPage = new TabPage(string.Join(" / ", item2.Select(StationSettings.Title)));
			TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
			tableLayoutPanel.Dock = DockStyle.Fill;
			tableLayoutPanel.ColumnCount = item2.Count;
			tableLayoutPanel.RowCount = 1;
			tableLayoutPanel.Margin = Padding.Empty;
			tableLayoutPanel.Padding = Padding.Empty;
			TableLayoutPanel tableLayoutPanel2 = tableLayoutPanel;
			for (int num3 = 0; num3 < item2.Count; num3++)
			{
				tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / (float)item2.Count));
				JournalPane journalPane = new JournalPane(this, item2[num3].Id);
				panes.Add(journalPane);
				tableLayoutPanel2.Controls.Add(journalPane, num3, 0);
			}
			tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
			tabPage.Controls.Add(tableLayoutPanel2);
			journalTabs.TabPages.Add(tabPage);
		}
		TabPage tabPage2 = new TabPage("Dziennik telefoniczny");
		JournalPane journalPane2 = new JournalPane(this, "TEL");
		panes.Add(journalPane2);
		tabPage2.Controls.Add(journalPane2);
		journalTabs.TabPages.Add(tabPage2);
		if (scalingReady)
		{
			ScaleNewJournalControls();
		}
	}

	public void RefreshData()
	{
		SyncTd2DirectionFilter();
		Text = StationIdentity.Caption(Engine.Station);
		RefreshTrains();
		foreach (JournalPane pane in panes)
		{
			pane.RefreshData();
		}
		RefreshTracks();
		RefreshServices();
		statusUser.Text = ((Engine.SimRail == null) ? "" : (Engine.SimulatorCaption + " " + Engine.SimulatorServerName + " | ")) + "Użytkownik: " + Engine.User.Name;
		statusUnit.Text = "    Jednostka organizacyjna: " + Engine.OrganizationCaption + "    ";
		ToolStripStatusLabel toolStripStatusLabel = statusDuty;
		string text;
		if (Engine.ActiveDuty == null)
		{
			text = "TRYB PODGLĄDU";
		}
		else
		{
			text = (Engine.CanWrite ? "TRYB OPERATORA" : ("DYŻUR: " + Engine.ActiveDuty.UserName));
		}
		toolStripStatusLabel.Text = text;
		dutyButton.Text = (Engine.CanWrite ? "Zdaj dyżur" : "Przyjmij dyżur");
		statusLogin.Text = "Zalogowany od: " + Engine.LoggedInAt.ToString("HH:mm:ss  dd-MM-yyyy");
	}

	private void RefreshTrains()
	{
		if (trains == null)
		{
			return;
		}
		int num = trains.FirstDisplayedScrollingRowIndex;
		string columnName = ((trains.CurrentCell == null) ? "odd" : trains.CurrentCell.OwningColumn.Name);
		filling = true;
		trains.Rows.Clear();
		List<Train> list = (from t2 in Engine.State.Trains
			where t2.StationId == Engine.StationId && !t2.Td2OutsideArea
			where t2.RemovedFromSource || !useTimeRange || (t2.PlanArrival >= timetableDay.Value.AddHours(-timeRangeHours) && t2.PlanArrival <= timetableDay.Value.AddHours(timeRangeHours))
			where confirmationFilter == 0 || confirmationFilter == 1 == TimetableConfirmation.IsConfirmed(t2)
			where movementFilter == 0 || (movementFilter == 4 && t2.Runs) || (movementFilter == 1 && (TrainDirections.Starts(t2, Engine.Station) || TrainDirections.Ends(t2, Engine.Station))) || (movementFilter == 2 && TrainDirections.Starts(t2, Engine.Station)) || (movementFilter == 3 && TrainDirections.Ends(t2, Engine.Station))
			where !runningOnly.Checked || t2.Runs
			where !notConfirmed.Checked || !TimetableConfirmation.IsConfirmed(t2)
			where kindFilter.SelectedIndex == 0 || kindFilter.SelectedIndex == 2 == (t2.Kind ?? "").StartsWith("T")
			where string.IsNullOrWhiteSpace(search.Text) || (t2.Number + " " + t2.From + " " + t2.To + " " + t2.Carrier + " " + ((t2.Profile == null) ? "" : t2.Profile.Name)).IndexOf(search.Text.Trim(), StringComparison.CurrentCultureIgnoreCase) >= 0
			orderby TrainPrediction.Arrival(t2)
			select t2).ThenBy((Train train) => train.PlanArrival).ToList();
		foreach (Train t in list)
		{
			bool flag = int.TryParse(t.Number, out var result) && result % 2 == 0;
			int index = trains.Rows.Add(t.Runs, !t.Runs, TimetableTime(t.PlanArrival), Delay(TrainPrediction.Arrival(t), t.PlanArrival), TimetableTime(TrainPrediction.Arrival(t)), t.Kind, flag ? "" : t.Number, TrainDirections.Text(Engine, t, "Z"), flag ? t.Number : "", TrainDirections.Text(Engine, t, "DO"), "", TrainDirections.Lines(t), t.ExternalPlannedStop ?? Math.Round((t.PlanDeparture - t.PlanArrival).TotalMinutes), (t.Arrival.HasValue && t.Departure.HasValue) ? (t.Departure.Value - t.Arrival.Value).TotalMinutes : (t.ExternalPlannedStop ?? Math.Round((t.PlanDeparture - t.PlanArrival).TotalMinutes)), t.StopType ?? ((t.PlanArrival == t.PlanDeparture) ? "" : "ph"), Ui.PlatformText(t.Platform) + " " + t.Track, TimetableTime(t.PlanDeparture), Delay(TrainPrediction.Departure(t), t.PlanDeparture), TimetableTime(TrainPrediction.Departure(t)), t.Origin, t.Destination, t.Carrier);
			DataGridViewRow dataGridViewRow = trains.Rows[index];
			dataGridViewRow.Tag = t.Id;
			string[] array = new string[10] { "k", "pa", "da", "ra", "pd", "dd", "rd", "stay", "type", "pt" };
			foreach (string columnName2 in array)
			{
				dataGridViewRow.Cells[columnName2].Style.BackColor = Ui.Cream;
			}
			string[] array2 = new string[2] { "odd", "from" };
			foreach (string columnName3 in array2)
			{
				dataGridViewRow.Cells[columnName3].Style.BackColor = EdrPalette.Incoming;
			}
			dataGridViewRow.Cells["even"].Style.BackColor = EdrPalette.OutgoingNumber;
			dataGridViewRow.Cells["to"].Style.BackColor = EdrPalette.Outgoing;
			string[] array3 = new string[3] { "origin", "destination", "carrier" };
			foreach (string columnName4 in array3)
			{
				dataGridViewRow.Cells[columnName4].Style.BackColor = Ui.Cream;
			}
			DataGridViewCellStyle style = dataGridViewRow.Cells["orderedStay"].Style;
			Color backColor = (dataGridViewRow.Cells["stay"].Style.BackColor = ((t.PlanDeparture > t.PlanArrival) ? Color.FromArgb(255, 211, 168) : EdrPalette.NoStay));
			style.BackColor = backColor;
			dataGridViewRow.Cells["pt"].Style.BackColor = Color.FromArgb(166, 202, 240);
			dataGridViewRow.Cells["kind"].Style.Font = trains.Font;
			DataGridViewCellStyle style2 = dataGridViewRow.Cells["odd"].Style;
			Font font = (dataGridViewRow.Cells["even"].Style.Font = ScaledBold);
			style2.Font = font;
			dataGridViewRow.Cells["track"].Style.BackColor = Color.Silver;
			dataGridViewRow.Cells["type"].Style.BackColor = ((t.PlanDeparture > t.PlanArrival) ? Color.FromArgb(240, 251, 255) : EdrPalette.NoStay);
			dataGridViewRow.Cells["line"].Style.BackColor = EdrPalette.Outgoing;
			dataGridViewRow.Cells["kind"].Style.BackColor = TimetableConfirmation.KindColor(t);
			ApplyManualTimetablePalette(dataGridViewRow, t);
			TimetableConfirmation.Apply(dataGridViewRow, t);
			if (Engine.IsTd2)
			{
				Td2Projection.Presentation(Engine.SimRail, dataGridViewRow, t);
			}
			TrainDirections.Tooltips(Engine, dataGridViewRow, t);
			if (Engine.IsTd2)
			{
				dataGridViewRow.Cells["carrier"].ToolTipText = t.CarrierSource ?? "";
			}
			dataGridViewRow.Cells["ra"].ToolTipText = TrainPrediction.Hint(t, arrival: true);
			dataGridViewRow.Cells["rd"].ToolTipText = TrainPrediction.Hint(t, arrival: false);
			if (Engine.State.Entries.Any((JournalEntry e) => e.TrainId == t.Id && e.Direction == "Z" && !e.Cancelled))
			{
				dataGridViewRow.Cells["from"].Tag = "registered";
				dataGridViewRow.Cells["from"].ToolTipText = "Przyjęcie pociągu jest już wpisane do dziennika.";
			}
			if (Engine.State.Entries.Any((JournalEntry e) => e.TrainId == t.Id && e.Direction == "DO" && !e.Cancelled))
			{
				dataGridViewRow.Cells["to"].Tag = "registered";
				dataGridViewRow.Cells["to"].ToolTipText = "Wyprawienie pociągu jest już wpisane do dziennika.";
			}
		}
		DataGridViewRow dataGridViewRow2 = trains.Rows.Cast<DataGridViewRow>().FirstOrDefault((DataGridViewRow r) => (string)r.Tag == selectedTrainId);
		if (dataGridViewRow2 != null)
		{
			trains.CurrentCell = dataGridViewRow2.Cells[columnName];
		}
		else if (trains.Rows.Count > 0)
		{
			Train closest = list.OrderBy((Train t2) => Math.Abs(TrainPrediction.Arrival(t2).Subtract(Engine.Now).TotalSeconds)).ThenBy((Train train) => train.PlanArrival).First();
			DataGridViewRow dataGridViewRow3 = trains.Rows.Cast<DataGridViewRow>().First((DataGridViewRow r) => (string)r.Tag == closest.Id);
			trains.CurrentCell = dataGridViewRow3.Cells["odd"];
			selectedTrainId = closest.Id;
			num = dataGridViewRow3.Index;
		}
		else
		{
			selectedTrainId = null;
		}
		if (num >= 0 && num < trains.Rows.Count && trains.IsHandleCreated && trains.Visible && trains.ClientSize.Height > trains.ColumnHeadersHeight + trains.RowTemplate.Height)
		{
			trains.FirstDisplayedScrollingRowIndex = num;
		}
		filling = false;
		RefreshDetails();
	}

	private string TimetableTime(DateTime? time)
	{
		if (!time.HasValue)
		{
			return "";
		}
		return time.Value.ToString((trains.Tag as bool? == true) ? "dd-MM HH:mm" : "HH:mm");
	}

	private static string Delay(DateTime? actual, DateTime plan)
	{
		if (!actual.HasValue)
		{
			return "";
		}
		return ((int)(actual.Value - plan).TotalMinutes).ToString();
	}

	private void RefreshTracks()
	{
		CloseStationTrackPopup();
		Control[] array = tracks.Controls.Cast<Control>().ToArray();
		foreach (Control control in array)
		{
			control.Dispose();
		}
		tracks.Controls.Clear();
		foreach (string item in StationSettings.VisibleTracks(Engine.Station))
		{
			string status = Engine.TrackStatus(item);
			Button button = new Button();
			button.Text = item;
			button.Width = Z(27);
			button.Height = Z(17);
			button.Margin = Padding.Empty;
			button.Padding = Padding.Empty;
			button.Font = ScaledBold;
			button.FlatStyle = FlatStyle.Flat;
			button.BackColor = StationTrackColors.ForStatus(status);
			button.ForeColor = Color.White;
			Button button2 = button;
			button2.FlatAppearance.BorderColor = Color.FromArgb(70, 100, 50);
			AttachStationTrackButton(button2, item);
			tracks.Controls.Add(button2);
		}
		CenterTrackButtons();
	}

	private void CenterTrackButtons()
	{
		if (tracks != null)
		{
			int num = tracks.Controls.Cast<Control>().Sum((Control c) => c.Width + c.Margin.Horizontal);
			Padding padding = new Padding(Math.Max(0, (tracks.ClientSize.Width - num) / 2), 0, 0, 0);
			if (tracks.Padding != padding)
			{
				tracks.Padding = padding;
			}
		}
	}

	private void RefreshServices()
	{
		servicesGrid.Rows.Clear();
		foreach (ServiceRecord item in from x in Engine.State.Services
			where x.StationId == Engine.StationId
			orderby x.Start descending
			select x)
		{
			int index = servicesGrid.Rows.Add(item.Type, item.Train, item.Carrier, item.Track, Ui.FullTime(item.Start), Ui.FullTime(item.End), item.Status, item.Notes);
			servicesGrid.Rows[index].Tag = item;
		}
	}

	public void RenderReferenceOverlays(Graphics graphics)
	{
		if (!headerClock.Visible)
		{
			return;
		}
		using Bitmap image = new Bitmap(headerClock.Width, headerClock.Height);
		using (Graphics g = Graphics.FromImage(image))
		{
			((ReferenceClockBar)headerClock).Render(g);
		}
		Point point = PointToClient(headerClock.PointToScreen(Point.Empty));
		graphics.DrawImageUnscaled(image, point.X, point.Y);
	}

	public void RegisterCheck()
	{
		CheckAction();
	}

	public void ConfigureStation()
	{
		Configure();
	}

	public Train NeedTrain()
	{
		if (SelectedTrain == null)
		{
			throw new Exception("Wybierz pociąg na liście.");
		}
		return SelectedTrain;
	}

	private void SetLayout(string layout, bool persist)
	{
		if (!FlushJournals())
		{
			return;
		}
		string text = currentLayout;
		if (layout == "Okno" && currentLayout != "Okno")
		{
			previousLayout = currentLayout;
		}
		currentLayout = layout;
		if (detached != null)
		{
			detached.FormClosing -= DetachedClosing;
			detached.Controls.Remove(journalTabs);
			split.Panel2.Controls.Add(journalTabs);
			detached.Close();
			detached = null;
			split.Panel2Collapsed = false;
		}
		if (layout == "Okno")
		{
			split.Panel2Collapsed = true;
			split.Panel2.Controls.Remove(journalTabs);
			detached = new Form
			{
				Icon = ReferenceTheme.ApplicationIcon,
				Text = "EDR - Dzienniki ruchu",
				Font = Font,
				Size = new Size(1100, 470),
				StartPosition = FormStartPosition.CenterScreen
			};
			detached.Controls.Add(journalTabs);
			detached.FormClosing += DetachedClosing;
			detached.Show(this);
		}
		else
		{
			split.Panel2Collapsed = false;
			split.Orientation = ((layout == "Prawo") ? Orientation.Vertical : Orientation.Horizontal);
			if (text == "Prawo" != (layout == "Prawo"))
			{
				BuildJournals();
			}
			split.SplitterDistance = Math.Max(100, (int)((double)((layout == "Prawo") ? split.Width : split.Height) * ((layout == "Prawo") ? 0.9 : 0.58)));
		}
		if (persist)
		{
			Engine.Change("Układ okna", "Widok", (AppState s) =>
			{
				s.Layout = layout;
			});
		}
	}

	private void DetachedClosing(object sender, FormClosingEventArgs e)
	{
		if (!IsDisposed)
		{
			e.Cancel = true;
			BeginInvoke((Action)(() =>
			{
				SetLayout(previousLayout, persist: true);
			}));
		}
	}

	public void SelectTrain(string id)
	{
		selectedTrainId = id;
		RefreshTrains();
	}

	internal void PostOperatorNotice(string title, string message)
	{
		if (IsDisposed || closingSession || !IsHandleCreated)
		{
			return;
		}
		BeginInvoke((Action)(() =>
		{
			if (!IsDisposed && !closingSession)
			{
				string text = SepeDiagnostics.Clean(title);
				string arg = SepeDiagnostics.Clean(message);
				if (OperatorNoticeSink != null)
				{
					OperatorNoticeSink(text, arg);
				}
				else if (!Ui.Testing)
				{
					MessageBox.Show(this, arg, text, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				}
			}
		}));
	}

	public void PositionPhotoHeader()
	{
		if (headerClock == null)
		{
			return;
		}
		headerClock.Visible = topTabs != null && topTabs.SelectedIndex == 0 && ClientSize.Width >= Z(1030);
		int num = ClientSize.Width - Padding.Right - Z(245);
		int val = headerRow.Bottom;
		if (topTabs != null && topTabs.IsHandleCreated && topTabs.TabPages.Count > 0)
		{
			Rectangle tabRect = topTabs.GetTabRect(topTabs.TabPages.Count - 1);
			if (PointToClient(topTabs.PointToScreen(new Point(tabRect.Right, tabRect.Bottom))).X > num)
			{
				val = Math.Max(val, PointToClient(topTabs.PointToScreen(new Point(0, topTabs.DisplayRectangle.Top))).Y + Z(2));
			}
		}
		headerClock.Bounds = new Rectangle(num, val, Z(245), Z(82));
		headerClock.BringToFront();
	}

	private void ConfigurePhotoTimetable()
	{
		trains.Tag = true;
		trains.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
		string[] array = new string[23]
		{
			"k", "nk", "pa", "da", "ra", "kind", "odd", "from", "even", "to",
			"track", "line", "orderedStay", "stay", "type", "pt", "pd", "dd", "rd", "origin",
			"destination", "carrier", "filler"
		};
		int[] array2 = new int[23]
		{
			18, 20, 95, 28, 95, 36, 51, 128, 51, 128,
			26, 40, 40, 40, 32, 30, 95, 28, 95, 116,
			116, 110, 1
		};
		for (int i = 0; i < array.Length; i++)
		{
			DataGridViewColumn dataGridViewColumn = trains.Columns[array[i]];
			dataGridViewColumn.Frozen = false;
			dataGridViewColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
			dataGridViewColumn.FillWeight = array2[i];
		}
		trains.Columns["filler"].Visible = false;
		trains.Columns["carrier"].Visible = true;
		trains.ColumnHeadersHeight = 18;
		trains.RowTemplate.Height = 19;
		trains.Columns["pa"].HeaderText = "Przyjazd plan.";
		trains.Columns["ra"].HeaderText = "Przyjazd rzecz.";
		trains.Columns["pd"].HeaderText = "Odjazd plan.";
		trains.Columns["rd"].HeaderText = "Odjazd rzecz.";
		trains.Columns["orderedStay"].HeaderText = "P. zam.";
		trains.Columns["stay"].HeaderText = "P. obl.";
		trains.Columns["type"].HeaderText = "Typ...";
		RememberColumnWidths(trains);
		trains.SizeChanged += (object s, EventArgs e) =>
		{
			if (!applyingScale)
			{
				FitPhotoColumns();
			}
		};
		ApplyDateColumnWidths();
	}

	private void FitPhotoColumns()
	{
		if (trains == null || trains.Columns.Count == 0)
		{
			return;
		}
		bool flag = trains.Tag as bool? == true;
		string[] array = new string[22]
		{
			"k", "nk", "pa", "da", "ra", "kind", "odd", "from", "even", "to",
			"track", "line", "orderedStay", "stay", "type", "pt", "pd", "dd", "rd", "origin",
			"destination", "carrier"
		};
		int[] array2 = new int[22]
		{
			24, 24, 102, 35, 102, 45, 65, 170, 65, 170,
			30, 60, 55, 55, 48, 38, 102, 35, 102, 152,
			152, 170
		};
		for (int i = 0; i < array.Length; i++)
		{
			if (!flag && (array[i] == "pa" || array[i] == "ra" || array[i] == "pd" || array[i] == "rd"))
			{
				array2[i] = 52;
			}
		}
		FitDefaultColumns(trains, array.Select((string x) => trains.Columns[x]).ToArray(), array2, new int[1] { 21 });
	}

	private void EditOrganizationUnit()
	{
		using Editor editor = new Editor("Jednostka organizacyjna", 500, 210);
		TextBox name = editor.Add("Nazwa jednostki", Ui.Text(Engine.UnitCaption, multiline: false), 35);
		editor.Info("Pusta nazwa przywraca " + Engine.DefaultOrganization(Engine.UnitProvider) + ".", 40);
		editor.Save(() =>
		{
			Engine.SetUnitCaption(name.Text);
		});
		if (editor.ShowDialog(this) == DialogResult.OK)
		{
			RefreshData();
		}
	}

	private void AddPersistenceMenu(ToolStripMenuItem app)
	{
		app.DropDownItems.Add(Ui.Menu("Zapisz konfigurację posterunku…", () =>
		{
			PortableDialog("posterunek", read: false);
		}));
		app.DropDownItems.Add(Ui.Menu("Wczytaj konfigurację posterunku…", () =>
		{
			PortableDialog("posterunek", read: true);
		}));
		app.DropDownItems.Add(Ui.Menu("Zapisz rozkład…", () =>
		{
			PortableDialog("rozklad", read: false);
		}));
		app.DropDownItems.Add(Ui.Menu("Wczytaj rozkład…", () =>
		{
			PortableDialog("rozklad", read: true);
		}));
		app.DropDownItems.Add(Ui.Menu("Zapisz sesję z dziennikami…", () =>
		{
			PortableDialog("sesja", read: false);
		}));
		app.DropDownItems.Add(Ui.Menu("Wczytaj sesję z dziennikami…", () =>
		{
			PortableDialog("sesja", read: true);
		}));
		app.DropDownItems.Add(new ToolStripSeparator());
	}

	internal void LoadPortable(string kind, string path)
	{
		if (sendingHours || simulatorRefreshing || outgoingHours.Count > 0)
		{
			throw new InvalidOperationException("Poczekaj na zakończenie odświeżania i wysyłania godzin.");
		}
		if (!FlushJournals())
		{
			return;
		}
		if (kind == "posterunek")
		{
			Engine.ImportStation(path);
		}
		else
		{
			Engine.ImportWorking(path, kind == "sesja");
		}
		if (kind != "posterunek")
		{
			hoursPaused = true;
			savedTimetableView = true;
			if (simulatorState != null)
			{
				simulatorState.Text = "Dane z pliku — automatyczne odświeżanie wstrzymane";
			}
			currentData.Checked = false;
			Train train = (from x in Engine.State.Trains
				where x.StationId == Engine.StationId
				orderby x.PlanArrival
				select x).FirstOrDefault();
			if (train != null)
			{
				timetableDay.Value = train.PlanArrival;
			}
		}
		selectedTrainId = null;
		BuildJournals();
		RefreshData();
	}

	private void PortableDialog(string kind, bool read)
	{
		if (sendingHours || simulatorRefreshing || outgoingHours.Count > 0)
		{
			throw new InvalidOperationException("Poczekaj na zakończenie odświeżania i wysyłania godzin.");
		}
		if (!FlushJournals())
		{
			return;
		}
		string text;
		if (kind == "posterunek")
		{
			text = "Konfiguracja posterunku";
		}
		else
		{
			text = ((kind == "sesja") ? "Sesja z dziennikami" : "Rozkład EDR");
		}
		string filter = text + "|*." + kind + ".json|Pliki JSON|*.json";
		string fileName;
		if (read)
		{
			Engine.RequireCoordinator();
			OpenFileDialog openFileDialog = new OpenFileDialog();
			openFileDialog.Filter = filter;
			openFileDialog.Title = "Wczytaj " + kind;
			using (OpenFileDialog openFileDialog2 = openFileDialog)
			{
				if (openFileDialog2.ShowDialog(this) != DialogResult.OK)
				{
					return;
				}
				fileName = openFileDialog2.FileName;
			}
			MainForm owner = this;
			string text2;
			if (kind == "posterunek")
			{
				text2 = "Zostaną zmienione ustawienia posterunku. Wpisy dzienników pozostaną.";
			}
			else
			{
				text2 = ((kind == "rozklad") ? "Zostanie zastąpiony rozkład. Wpisy dzienników i ustawienia pozostaną." : "Zostaną zastąpione rozkład, wpisy i dyżury bieżącego posterunku. Ustawienia pozostaną.");
			}
			if (MessageBox.Show(owner, "Wczytać wybrany plik?\n" + text2 + "\nTa operacja nie wysyła godzin do SEPE.", "Wczytywanie danych", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
			{
				LoadPortable(kind, fileName);
			}
			return;
		}
		SaveFileDialog saveFileDialog = new SaveFileDialog();
		saveFileDialog.Filter = filter;
		saveFileDialog.FileName = Engine.Station.Name + "-" + Engine.Now.ToString("yyyyMMdd-HHmmss") + "." + kind + ".json";
		saveFileDialog.AddExtension = true;
		saveFileDialog.DefaultExt = kind + ".json";
		using (SaveFileDialog saveFileDialog2 = saveFileDialog)
		{
			if (saveFileDialog2.ShowDialog(this) != DialogResult.OK)
			{
				return;
			}
			fileName = saveFileDialog2.FileName;
		}
		if (kind == "posterunek")
		{
			Engine.ExportStation(fileName);
		}
		else
		{
			Engine.ExportWorking(fileName, kind == "sesja");
		}
		MessageBox.Show(this, "Zapisano plik:\n" + fileName, "Zapis danych");
	}

	private static Label AtLabel(Control parent, string text, int x, int y, int width, int height, bool bold)
	{
		Label label = new Label();
		label.Text = text;
		label.Bounds = new Rectangle(x, y, width, height);
		label.Font = (bold ? new Font("Segoe UI", 9f, FontStyle.Bold) : ReferenceTheme.Dialog);
		label.TextAlign = ContentAlignment.MiddleLeft;
		Label label2 = label;
		parent.Controls.Add(label2);
		return label2;
	}

	private static void Place(Control parent, Control child, int x, int y, int width, int height)
	{
		child.Dock = DockStyle.None;
		child.Bounds = new Rectangle(x, y, width, height);
		parent.Controls.Add(child);
	}

	private static void DialogButtons(Editor dialog)
	{
		foreach (Button control in dialog.Buttons.Controls)
		{
			control.AutoSize = false;
			control.Size = new Size(100, 34);
			control.MinimumSize = Size.Empty;
			control.Font = new Font("Segoe UI", 10f);
			control.Margin = new Padding(3, 1, 3, 1);
		}
		dialog.Buttons.Height = 43;
	}

	private void ShowReferenceTrainDialog(JournalEntry original, Train train, string direction, Section target, JournalEntry en)
	{
		List<Section> sections = Engine.Station.Sections.Where(StationSettings.Conducted).ToList();
		Editor d = new Editor((direction == "Z") ? "Przyjęcie pociągu" : "Wyprawienie pociągu", 371, 520);
		try
		{
			Panel panel = d.AbsoluteBody();
			Label heading = AtLabel(panel, "Pociąg " + en.Number + "  " + direction + "  " + target.Name.ToUpper(), 8, 10, 345, 28, bold: true);
			heading.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
			heading.ForeColor = Color.Green;
			heading.TextAlign = ContentAlignment.MiddleCenter;
			AtLabel(panel, "Dziennik ruchu:", 13, 45, 140, 25, bold: false);
			ComboBox sec = Ui.Combo(sections.Select(SectionRouting.Display), null, editable: false);
			sec.SelectedIndex = sections.FindIndex((Section x) => x.Id == target.Id);
			sec.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
			Place(panel, sec, 157, 46, 185, 24);
			sec.FlatStyle = FlatStyle.Flat;
			sec.BackColor = panel.BackColor;
			AtLabel(panel, "Nr pociągu (niep./parz.):", 13, 78, 145, 26, bold: false);
			TextBox num = Ui.Text((original == null && train != null) ? Engine.TrainNumber(train, target, direction) : en.Number, multiline: false);
			num.Font = new Font("Segoe UI", 10f);
			Place(panel, num, 157, 77, 185, 27);
			AtLabel(panel, "Tor stacyjny:", 13, 112, 140, 26, bold: false);
			StationTrackComboBox track = new StationTrackComboBox(Engine, en.Track);
			track.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
			Place(panel, track, 157, 111, 185, 27);
			AtLabel(panel, "Droga wolna:", 13, 145, 140, 26, bold: false);
			ReferenceTimeInput permission = new ReferenceTimeInput(en.Permission, Engine.Now, blue: false, () => Engine.Now);
			Place(panel, permission, 157, 144, 110, 27);
			AtLabel(panel, "Pociąg odjechał:", 13, 177, 140, 26, bold: false);
			ReferenceTimeInput left = new ReferenceTimeInput(en.Left, Engine.Now, blue: true, () => Engine.Now);
			Place(panel, left, 157, 176, 110, 27);
			AtLabel(panel, "Pociąg przyjechał:", 13, 209, 140, 26, bold: false);
			ReferenceTimeInput arrived = new ReferenceTimeInput(en.ReturnFromLine ? en.ReturnArrival : en.Arrived, Engine.Now, blue: true, () => Engine.Now);
			Place(panel, arrived, 157, 208, 110, 27);
			SilverButton silverButton = new SilverButton();
			silverButton.Text = "?";
			silverButton.BackColor = Color.CornflowerBlue;
			silverButton.ForeColor = Color.RoyalBlue;
			silverButton.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
			SilverButton silverButton2 = silverButton;
			Place(panel, silverButton2, 24, 252, 23, 23);
			silverButton2.Click += (object s, EventArgs e) =>
			{
				MessageBox.Show(d, "Lewy tor jest rozpoznawany na podstawie uwag, np. „po torze lewym”.\nDatę każdego czasu można zmienić prawym przyciskiem w jego polu.\nŻądanie pozwolenia jest dostępne w menu dodatkowym numeru pociągu.", "Przyjęcie / wyprawienie pociągu");
			};
			CheckBox leftTrack = new CheckBox
			{
				Text = "Po torze lewym",
				AutoCheck = false,
				TabStop = false,
				Font = ReferenceTheme.Dialog
			};
			Place(panel, leftTrack, 54, 252, 235, 25);
			CheckBox back = new CheckBox
			{
				Text = "Powrót ze szlaku",
				Font = ReferenceTheme.Dialog,
				Checked = (en.ReturnFromLine || (en.Text ?? "").Contains("Powrót ze szlaku")),
				Enabled = (direction == "DO")
			};
			Place(panel, back, 54, 283, 230, 24);
			if (!string.IsNullOrEmpty(en.ReturnOfId))
			{
				sec.Enabled = false;
				num.ReadOnly = true;
				permission.Enabled = false;
				left.Enabled = false;
			}
			AtLabel(panel, "Uwagi:", 13, 313, 61, 22, bold: false);
			TextBox notes = Ui.Text(en.Text, multiline: true);
			notes.Font = ReferenceTheme.Dialog;
			Place(panel, notes, 13, 337, 329, 102);
			ComboBox suggestions = Ui.Combo(WorkTrip.Templates(Engine.Station.Name, target.Name), null, editable: false);
			suggestions.SelectedIndex = -1;
			Place(panel, suggestions, 75, 310, 267, 24);
			suggestions.SelectedIndexChanged += (object s, EventArgs e) =>
			{
				if (suggestions.SelectedIndex >= 0)
				{
					notes.Text = suggestions.Text;
					if (notes.Text.Contains("i z powrotem"))
					{
						back.Checked = true;
					}
				}
			};
			back.CheckedChanged += (object s, EventArgs e) =>
			{
				if (back.Checked && (string.IsNullOrWhiteSpace(notes.Text) || notes.Text.Trim() == "Powrót ze szlaku"))
				{
					notes.Text = "do km ... i z powrotem";
				}
			};
			Action update = () =>
			{
				Section section = sections[sec.SelectedIndex];
				heading.Text = "Pociąg " + num.Text + "  " + direction + "  " + section.Name.ToUpper();
				try
				{
					JournalEntry journalEntry = Json.Clone(en);
					journalEntry.SectionId = section.Id;
					journalEntry.Direction = direction;
					journalEntry.Text = notes.Text;
					leftTrack.Checked = JournalLineTracks.IsLeft(section, direction, Engine.JournalTrack(journalEntry));
				}
				catch (InvalidOperationException)
				{
					leftTrack.Checked = false;
				}
				leftTrack.ForeColor = (leftTrack.Checked ? Color.Red : Color.Black);
			};
			sec.SelectedIndexChanged += (object s, EventArgs e) =>
			{
				if (original == null && train != null)
				{
					num.Text = Engine.TrainNumber(train, sections[sec.SelectedIndex], direction);
				}
				update();
			};
			num.TextChanged += (object s, EventArgs e) =>
			{
				update();
			};
			notes.TextChanged += (object s, EventArgs e) =>
			{
				update();
			};
			update();
			ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
			contextMenuStrip.Items.Add(Ui.Menu("Żądanie pozwolenia / data wpisu…", () =>
			{
				using Editor editor = new Editor("Dodatkowe pola wpisu", 520, 240);
				DateTimePicker request = editor.Add("Żądanie pozwolenia", Ui.Date(en.Request ?? Engine.Now, nullable: true, en.Request.HasValue), 35);
				DateTimePicker date = editor.Add("Data wpisu", Ui.Date(en.Time, nullable: false, active: true), 35);
				editor.Save(() =>
				{
					en.Request = Ui.Nullable(request);
					en.Time = date.Value;
				});
				editor.ShowDialog(d);
			}));
			num.ContextMenuStrip = contextMenuStrip;
			d.Save(() =>
			{
				en.SectionId = sections[sec.SelectedIndex].Id;
				en.Number = Ui.Required(num, "Numer pociągu");
				en.Track = track.Text;
				en.Permission = permission.Value;
				en.Left = left.Value;
				en.ReturnFromLine = back.Checked;
				if (en.ReturnFromLine)
				{
					en.ReturnArrival = arrived.Value;
					en.Arrived = null;
				}
				else
				{
					en.Arrived = arrived.Value;
				}
				en.Text = notes.Text.Trim();
				Engine.SaveJournal(en);
			});
			DialogButtons(d);
			if (d.ShowDialog(this) == DialogResult.OK)
			{
				RefreshData();
				JournalPane journalPane = panes.FirstOrDefault((JournalPane x) => x.SectionId == en.SectionId);
				if (journalPane != null)
				{
					journalPane.SetDay(en.Time);
					journalPane.FocusEntry(en.Id);
				}
			}
		}
		finally
		{
			if (d != null)
			{
				((IDisposable)d).Dispose();
			}
		}
	}

	private void ShowReferenceTelephone(string section, JournalEntry original)
	{
		if (!FlushJournals())
		{
			return;
		}
		Engine.RequireWrite();
		if (original != null && original.Cancelled)
		{
			throw new Exception("Wpis został wykreślony.");
		}
		if (original != null && original.Telephone == null && section != "TEL")
		{
			using (Editor editor = new Editor("Podgląd wcześniejszego telefonogramu", 690, 470))
			{
				editor.Info("Wcześniejszy zapis jest dostępny do odczytu. Poprawkę zapisz jako nowy telefonogram.", 55);
				TextBox textBox = Ui.Text(original.Text, multiline: true);
				textBox.ReadOnly = true;
				editor.Add("Treść", textBox, 270);
				editor.Buttons.Controls[0].Text = "Zamknij";
				editor.ShowDialog(this);
				return;
			}
		}
		using TelephoneEditor telephoneEditor = new TelephoneEditor(Engine, section, original);
		if (telephoneEditor.ShowDialog(this) == DialogResult.OK)
		{
			RefreshData();
			JournalPane journalPane = panes.FirstOrDefault((JournalPane x) => x.SectionId == section);
			if (journalPane != null && telephoneEditor.SavedEntry != null)
			{
				journalPane.SetDay(telephoneEditor.SavedEntry.Time);
				journalPane.FocusEntry(telephoneEditor.SavedEntry.Id);
			}
		}
	}

	private void ReflowDescription()
	{
		if (!descriptionReady || arrangingDescription || descriptionSurface == null || descriptionSurface.IsDisposed)
		{
			return;
		}
		arrangingDescription = true;
		descriptionSurface.SuspendLayout();
		try
		{
			Control control = descriptionSurface.Parent;
			float num = CurrentZoom;
			int num2 = Z(8);
			int num3 = Math.Max(400, (int)((float)(control.Width - SystemInformation.VerticalScrollBarWidth - num2 * 2) / num));
			bool flag = num3 >= 850;
			int num4 = (flag ? Math.Max(430, Math.Min(540, (int)((double)num3 * 0.43))) : num3);
			int num5 = 175;
			int num6 = 14 + num5 + 5;
			int num7 = num4 - num6 - 12;
			int num8 = Math.Max(22, (int)Math.Ceiling((float)profileBoxes["Number"].Font.Height / num) + 7);
			int field = num8 - 1;
			Action<Control, int, int, int, int> place = (Control control2, int x, int y, int w, int h) =>
			{
				control2.SetBounds(Z(x), Z(y), Z(w), Z(h));
			};
			Action<string, int, int, int, int> action = (string key, int x, int y, int l, int w) =>
			{
				place(profileLabels[key], x, y, l, field);
				place(profileBoxes[key], x + l + 5, y, w, field);
			};
			string[] array = new string[9] { "Number", "InternationalNumber", "Name", "OrderNumber", "Kind", "Carrier", "Type", "Origin", "Destination" };
			for (int num9 = 0; num9 < array.Length; num9++)
			{
				bool flag2 = num9 == 0 || num9 == 1 || num9 == 3 || num9 == 4;
				action(array[num9], 14, 7 + num9 * num8, num5, flag2 ? Math.Min(158, num7) : num7);
			}
			int num10 = 7 + array.Length * num8 + 10;
			string[] array2 = new string[3] { "TimetableType", "ApplicationNumber", "ApplicationDate" };
			for (int num11 = 0; num11 < array2.Length; num11++)
			{
				action(array2[num11], 14, num10 + num11 * num8, num5, num7);
			}
			int num12 = num10 + array2.Length * num8;
			int num13 = (flag ? (num4 + 28) : 14);
			int num14 = (flag ? 7 : (num12 + 14));
			int num15 = num3 - num13 - 14;
			string[] array3 = new string[5] { "Additional", "Cargo", "CarrierNotes", "OperationalNotes", "Notes" };
			int num16 = Math.Max(16, (int)Math.Ceiling((float)profileLabels["Additional"].Font.Height / num));
			int num17 = Math.Max(26, field + 5);
			int num18 = num16 + num17 + 3;
			for (int num19 = 0; num19 < array3.Length; num19++)
			{
				int num20 = num14 + num19 * num18;
				place(profileLabels[array3[num19]], num13, num20, num15, num16);
				place(profileBoxes[array3[num19]], num13, num20 + num16, num15, num17);
			}
			int num21 = num14 + array3.Length * num18 + 5;
			int num22 = Math.Max(19, (int)Math.Ceiling((float)profileFlags["TN"].Font.Height / num) + 3);
			string[] array4 = new string[3] { "Przekroczona skrajnia", "Przekroczony nacisk", "Inne nadzwyczajne" };
			for (int num23 = 0; num23 < array4.Length; num23++)
			{
				place(profileFlags[array4[num23]], num13, num21 + num23 * num22, Math.Min(250, num15 / 2), num22);
			}
			place(profileFlags["TWR"], num13 + (int)((double)num15 * 0.58), num21, 85, num22);
			place(profileFlags["TN"], num13 + (int)((double)num15 * 0.82), num21, 70, num22);
			int num24 = Math.Max(num12, num21 + 3 * num22) + 12;
			action("Applicant", 14, num24, num5, num3 - num6 - 14);
			int num25 = num24 + num8 + 7;
			action("Withdrawal", 14, num25, 89, 40);
			action("WithdrawalDate", 215, num25, 110, Math.Max(60, Math.Min(160, (flag ? num13 : num3) - 340)));
			int num26 = (flag ? num25 : (num25 + num8 + 4));
			place(profileFlags["Zamknięciowy"], num13, num26, Math.Min(180, num15 / 2), num22);
			place(profileFlags["Śledzony"], num13 + (int)((double)num15 * 0.58), num26, 100, num22);
			descriptionSurface.SetBounds(num2, num2, Z(num3), Z(Math.Max(num25 + field, num26 + num22) + 8));
		}
		finally
		{
			descriptionSurface.ResumeLayout(performLayout: false);
			arrangingDescription = false;
		}
	}

	private void BuildRoute()
	{
		Panel panel = DetailBody("Trasa pociągu");
		detailHeadings["route"] = Band("Pociąg", 22);
		string[] array = new string[29]
		{
			"Przyjazd plan.", "Godz.", "Przyjazd rzecz.", "Godz.", "+/-", "Z", "Lp.", "Stacja", "Post. zam.", "P. obl.",
			"Typ postoju", "*", "P T", "Odjazd plan.", "Godz.", "Odjazd rzecz.", "Godz.", "+/-", "Rodz.", "Nr poc.",
			"Got. do odj.", "Czas wpisu", "Maszynista", "Seria pl.", "Dł. pl.", "Brutto pl.", "Seria rz.", "Dł. rz.", "Brutto rz."
		};
		int[] array2 = new int[29]
		{
			51, 35, 51, 35, 29, 23, 25, 138, 39, 39,
			55, 18, 30, 51, 35, 51, 35, 29, 36, 48,
			68, 68, 90, 65, 45, 52, 82, 45, 52
		};
		DataGridView dataGridView = DetailGrid("route", array, array2);
		dataGridView.Tag = "route";
		dataGridView.ColumnHeadersHeight = 18;
		dataGridView.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
		dataGridView.Columns[dataGridView.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
		dataGridView.Columns[dataGridView.Columns.Count - 1].MinimumWidth = 5;
		dataGridView.Columns[dataGridView.Columns.Count - 1].Width = array2[array2.Length - 1];
		dataGridView.DefaultCellStyle.BackColor = EdrPalette.Cream;
		dataGridView.AlternatingRowsDefaultCellStyle.BackColor = EdrPalette.Cream;
		dataGridView.DefaultCellStyle.ForeColor = Color.Black;
		dataGridView.DefaultCellStyle.SelectionBackColor = Color.FromArgb(51, 153, 255);
		dataGridView.DefaultCellStyle.SelectionForeColor = Color.White;
		dataGridView.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
		dataGridView.Columns[7].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
		for (int i = 0; i < dataGridView.Columns.Count; i++)
		{
			dataGridView.Columns[i].ToolTipText = array[i] + (new int[4] { 0, 2, 13, 15 }.Contains(i) ? " - data" : "");
		}
		RememberColumnWidths(dataGridView);
		dataGridView.SizeChanged += (object s, EventArgs e) =>
		{
			FitRouteColumns();
		};
		panel.Controls.Add(dataGridView);
		panel.Controls.Add(detailHeadings["route"]);
	}

	private static string RouteDate(DateTime? t)
	{
		if (!t.HasValue)
		{
			return "";
		}
		return t.Value.ToString("dd-MM-yy");
	}

	private static string RouteTime(DateTime? t)
	{
		if (!t.HasValue)
		{
			return "";
		}
		return t.Value.ToString("HH:mm");
	}

	private static void LegacyDate(string value, out string day, out string time)
	{
		day = "";
		time = "";
		if (!string.IsNullOrWhiteSpace(value))
		{
			if (DateTime.TryParse(value, out var result))
			{
				day = RouteDate(result);
				time = RouteTime(result);
			}
			else
			{
				time = value;
			}
		}
	}

	private object[] RouteCells(DetailRow point)
	{
		object[] array = new object[29];
		if (point.Cells != null && point.Cells.Length >= 29)
		{
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = Cell(point, i);
			}
			array[12] = Ui.PlatformTrackText(Convert.ToString(array[12]));
			return array;
		}
		LegacyDate(Cell(point, 0), out var day, out var time);
		array[0] = day;
		array[1] = time;
		LegacyDate(Cell(point, 1), out day, out time);
		array[2] = day;
		array[3] = time;
		int[] array2 = new int[15]
		{
			2, 3, 4, 5, 6, 7, 8, 11, 12, 13,
			14, 15, 16, 17, 18
		};
		int[] array3 = new int[15]
		{
			4, 5, 6, 7, 8, 9, 10, 13, 15, 17,
			18, 19, 20, 28, 27
		};
		for (int j = 0; j < array2.Length; j++)
		{
			array[array3[j]] = Cell(point, array2[j]);
		}
		array[12] = (Ui.PlatformText(Cell(point, 9)) + " " + Cell(point, 10)).Trim();
		LegacyDate(Cell(point, 11), out day, out time);
		array[13] = day;
		array[14] = time;
		LegacyDate(Cell(point, 12), out day, out time);
		array[15] = day;
		array[16] = time;
		return array;
	}

	private void FillRoute(Train t)
	{
		DataGridView dataGridView = detailGrids["route"];
		detailHeadings["route"].Text = "Pociąg: " + t.Number + "     relacji: " + t.Origin + " - " + t.Destination;
		List<DetailRow> list = Records(t, "route").ToList();
		int num = -1;
		if (list.Count > 0)
		{
			foreach (DetailRow item in list)
			{
				int num2 = dataGridView.Rows.Add(RouteCells(item));
				dataGridView.Rows[num2].Tag = item;
				if (Cell(item, (item.Cells != null && item.Cells.Length >= 29) ? 7 : 5) == Engine.Station.Name)
				{
					num = num2;
				}
			}
		}
		else
		{
			if (!string.IsNullOrEmpty(t.From) && t.From != Engine.Station.Name)
			{
				object[] array = new object[29];
				array[7] = t.From;
				dataGridView.Rows.Add(array);
			}
			TrainConsist c = t.Consist;
			string text = ((c == null || c.Series == null) ? "" : string.Join(", ", from x in c.Series.Select((string x, int n) => x + ((c.Numbers != null && n < c.Numbers.Length && !string.IsNullOrWhiteSpace(c.Numbers[n])) ? ("-" + c.Numbers[n]) : ""))
				where !string.IsNullOrWhiteSpace(x)
				select x));
			double totalMinutes = (t.PlanDeparture - t.PlanArrival).TotalMinutes;
			string text2 = ((t.Arrival.HasValue && t.Departure.HasValue) ? (t.Departure.Value - t.Arrival.Value).TotalMinutes.ToString("0.0") : totalMinutes.ToString("0.0"));
			num = dataGridView.Rows.Add(RouteDate(t.PlanArrival), RouteTime(t.PlanArrival), RouteDate(TrainPrediction.Arrival(t)), RouteTime(TrainPrediction.Arrival(t)), Delay(TrainPrediction.Arrival(t), t.PlanArrival), t.Arrival.HasValue ? "✓" : "", "", Engine.Station.Name, totalMinutes.ToString("0.0"), text2, (totalMinutes > 0.0) ? "ph" : "", "", (Ui.PlatformText(t.Platform) + " " + t.Track).Trim(), RouteDate(t.PlanDeparture), RouteTime(t.PlanDeparture), RouteDate(TrainPrediction.Departure(t)), RouteTime(TrainPrediction.Departure(t)), Delay(TrainPrediction.Departure(t), t.PlanDeparture), t.Kind, t.Number, t.ReadyReported.HasValue ? t.ReadyReported.Value.ToString("dd-MM HH:mm") : "", t.ReadyRecordedAt.HasValue ? t.ReadyRecordedAt.Value.ToString("dd-MM HH:mm") : "", t.ReadyDriver, (c == null || c.PlannedSeries == null) ? "" : string.Join(", ", c.PlannedSeries), (c == null) ? "" : Convert.ToString(c.PlannedLength), (c == null) ? "" : Convert.ToString(c.PlannedGross), text, (c == null) ? "" : Convert.ToString(c.Length), (c == null) ? "" : Convert.ToString(c.Gross));
			if (!string.IsNullOrEmpty(t.To) && t.To != Engine.Station.Name)
			{
				object[] array2 = new object[29];
				array2[7] = t.To;
				dataGridView.Rows.Add(array2);
			}
		}
		foreach (DataGridViewRow item2 in (IEnumerable)dataGridView.Rows)
		{
			item2.Height = dataGridView.RowTemplate.Height;
			if (!string.IsNullOrWhiteSpace(Convert.ToString(item2.Cells[5].Value)))
			{
				item2.Cells[4].Style.BackColor = Color.Green;
				item2.Cells[4].Style.ForeColor = Color.White;
			}
		}
		if (dataGridView.Rows.Count > 0)
		{
			dataGridView.CurrentCell = dataGridView.Rows[(num >= 0) ? num : 0].Cells[0];
			dataGridView.FirstDisplayedScrollingColumnIndex = 0;
			dataGridView.HorizontalScrollingOffset = 0;
			dataGridView.ClearSelection();
			dataGridView.Rows[(num >= 0) ? num : 0].Selected = true;
		}
		FitRouteColumns();
	}

	private Font ZoomFont(Font font, float scale)
	{
		string key = string.Concat(font.Name, "|", font.Size, "|", font.Style, "|", scale, "|", fontDpi);
		if (!scaledFonts.TryGetValue(key, out var value))
		{
			value = new Font(font.FontFamily, Math.Max(1f, (float)Math.Round(font.SizeInPoints * (scale / (fontDpi / 96f)) / 1.375f * fontDpi / 72f)) * 72f / fontDpi, font.Style, GraphicsUnit.Point);
			scaledFonts.Add(key, value);
		}
		return value;
	}

	private int Z(int value)
	{
		return (int)Math.Round((float)value * CurrentZoom);
	}

	private Padding Z(Padding value)
	{
		return new Padding(Z(value.Left), Z(value.Top), Z(value.Right), Z(value.Bottom));
	}

	private void SetupScaling()
	{
		CaptureScale(this);
		scalingReady = true;
		Resize += (object s, EventArgs e) =>
		{
			QueueWindowLayout();
		};
		FormClosed += (object s, FormClosedEventArgs e) =>
		{
			foreach (Font value in scaledFonts.Values)
			{
				value.Dispose();
			}
		};
		ApplyZoom();
	}

	private void QueueWindowLayout()
	{
		if (applyingScale || layoutQueued || IsDisposed || Disposing || !IsHandleCreated)
		{
			return;
		}
		layoutQueued = true;
		BeginInvoke((Action)(() =>
		{
			layoutQueued = false;
			if (!IsDisposed && !Disposing)
			{
				LayoutWindow();
			}
		}));
	}

	private void LayoutWindow()
	{
		if (!applyingScale)
		{
			ApplyDateColumnWidths();
			if (filterPanel != null)
			{
				filterPanel.Reflow();
			}
			ReflowDescription();
			PositionPhotoHeader();
			FitRouteColumns();
		}
	}

	public void ApplyZoom()
	{
		if (!scalingReady || applyingScale)
		{
			return;
		}
		float num = 1f;
		using (Graphics graphics = CreateGraphics())
		{
			fontDpi = graphics.DpiY;
			num = graphics.DpiX / 96f;
		}
		float num2 = num * 1.375f;
		if (scaleApplied && Math.Abs(CurrentZoom - num2) < 0.005f)
		{
			LayoutWindow();
			return;
		}
		Control[] array = scaleSpecs.Keys.Where((Control x) => !x.IsDisposed).ToArray();
		applyingScale = true;
		CurrentZoom = num2;
		Control[] array2 = array;
		foreach (Control control in array2)
		{
			control.SuspendLayout();
		}
		try
		{
			foreach (KeyValuePair<Control, ScaleSpec> item in scaleSpecs.Where((KeyValuePair<Control, ScaleSpec> x) => !x.Key.IsDisposed).Reverse())
			{
				ApplyControl(item.Key, item.Value);
			}
			PositionPhotoHeader();
			foreach (DataGridViewRow item2 in trains.Rows.Cast<DataGridViewRow>())
			{
				item2.Cells["kind"].Style.Font = trains.Font;
				item2.Cells["odd"].Style.Font = ScaledBold;
				item2.Cells["even"].Style.Font = ScaledBold;
			}
			RefreshTracks();
		}
		finally
		{
			foreach (Control item3 in array.Reverse())
			{
				if (!item3.IsDisposed)
				{
					item3.ResumeLayout(performLayout: true);
				}
			}
			applyingScale = false;
		}
		scaleApplied = true;
		ApplyDateColumnWidths();
		if (filterPanel != null)
		{
			filterPanel.Reflow();
		}
		PositionPhotoHeader();
		ReflowDescription();
		FitRouteColumns();
		Invalidate(invalidateChildren: true);
	}

	private void CaptureScale(Control control)
	{
		if (!scaleSpecs.ContainsKey(control))
		{
			scaleSpecs[control] = new ScaleSpec(control);
		}
		if (control is ToolStrip toolStrip)
		{
			foreach (ToolStripItem item in toolStrip.Items)
			{
				CaptureItem(item);
			}
		}
		if (control is DataGridView)
		{
			return;
		}
		foreach (Control control2 in control.Controls)
		{
			CaptureScale(control2);
		}
	}

	private void CaptureItem(ToolStripItem item)
	{
		if (!itemSpecs.ContainsKey(item))
		{
			itemSpecs[item] = new ItemSpec(item);
		}
		if (!(item is ToolStripDropDownItem toolStripDropDownItem))
		{
			return;
		}
		foreach (ToolStripItem dropDownItem in toolStripDropDownItem.DropDownItems)
		{
			CaptureItem(dropDownItem);
		}
	}

	private void ApplyControl(Control c, ScaleSpec b)
	{
		c.SuspendLayout();
		try
		{
			c.Font = ZoomFont(b.Font, CurrentZoom);
			c.Padding = Z(b.Padding);
			c.Margin = Z(b.Margin);
			if (c != this && !(c is TabPage) && !(c is SplitterPanel))
			{
				if (c.Dock == DockStyle.Top || c.Dock == DockStyle.Bottom)
				{
					c.Height = Z(b.Bounds.Height);
				}
				else if (c.Dock == DockStyle.Left || c.Dock == DockStyle.Right)
				{
					c.Width = Z(b.Bounds.Width);
				}
				else if (c.Dock == DockStyle.None)
				{
					c.Bounds = new Rectangle(Z(b.Bounds.Left), Z(b.Bounds.Top), Z(b.Bounds.Right) - Z(b.Bounds.Left), Z(b.Bounds.Bottom) - Z(b.Bounds.Top));
				}
			}
			if (c != this)
			{
				c.MinimumSize = new Size(Z(b.Minimum.Width), Z(b.Minimum.Height));
			}
			DataGridView dataGridView = c as DataGridView;
			if (dataGridView != null)
			{
				dataGridView.ColumnHeadersHeight = Math.Max(Z(b.HeaderHeight), c.Font.Height + 6);
				dataGridView.ColumnHeadersDefaultCellStyle.Font = ZoomFont(ReferenceTheme.SmallBold, CurrentZoom);
				bool flag = dataGridView.Columns.Contains("permission");
				dataGridView.RowTemplate.Height = Z((dataGridView == trains || flag) ? 19 : 16);
				foreach (DataGridViewRow item2 in (IEnumerable)dataGridView.Rows)
				{
					JournalEntry journalEntry = item2.Tag as JournalEntry;
					DataGridViewRow dataGridViewRow2 = item2;
					MainForm mainForm = this;
					int value;
					if (journalEntry != null)
					{
						value = ((journalEntry.Type != "Pociąg") ? 26 : 19);
					}
					else
					{
						value = ((dataGridView == trains) ? 19 : 16);
					}
					dataGridViewRow2.Height = mainForm.Z(value);
				}
				for (int i = 0; i < dataGridView.Columns.Count; i++)
				{
					if (dataGridView.Columns[i].AutoSizeMode != DataGridViewAutoSizeColumnMode.Fill)
					{
						dataGridView.Columns[i].Width = Math.Max(5, Z(b.Columns[i]));
					}
				}
			}
			if (dataGridView != null && dataGridView.Columns.Contains("permission"))
			{
				foreach (DataGridViewColumn column in dataGridView.Columns)
				{
					if (column.Visible && column.AutoSizeMode != DataGridViewAutoSizeColumnMode.Fill)
					{
						string text;
						if (column.Name == "odd")
						{
							text = "Nieparzysty";
						}
						else
						{
							text = ((column.Name == "even") ? "Parzysty" : column.HeaderText);
						}
						column.Width = Math.Max(column.Width, TextRenderer.MeasureText(text, dataGridView.Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width + Z(6));
					}
				}
			}
			if (dataGridView != null)
			{
				float num = fontDpi / 96f;
				bool flag2 = dataGridView.Columns.Contains("permission");
				int num2 = ((dataGridView == trains || flag2) ? 25 : 21);
				dataGridView.RowTemplate.Height = (int)Math.Round((float)num2 * num);
				foreach (DataGridViewRow item3 in (IEnumerable)dataGridView.Rows)
				{
					JournalEntry journalEntry2 = item3.Tag as JournalEntry;
					item3.Height = (int)Math.Round((float)((journalEntry2 != null && journalEntry2.Type != "Pociąg") ? 32 : num2) * num);
				}
				if (Convert.ToString(dataGridView.Tag) == "route")
				{
					dataGridView.Font = ZoomFont(ReferenceTheme.Small, CurrentZoom);
					dataGridView.ColumnHeadersDefaultCellStyle.Font = ZoomFont(ReferenceTheme.SmallBold, CurrentZoom);
					dataGridView.ColumnHeadersHeight = (int)Math.Round(24f * num);
					for (int j = 0; j < dataGridView.Columns.Count; j++)
					{
						dataGridView.Columns[j].Width = Math.Max(18, (int)Math.Round((float)b.Columns[j] * 1.25f * num));
					}
				}
			}
			if (c is TabControl tabControl)
			{
				tabControl.ItemSize = new Size(0, Math.Max(Z(16), c.Font.Height + 5));
				tabControl.Padding = new Point(Z(11), Z(1));
			}
			if (c is ComboBox comboBox)
			{
				comboBox.ItemHeight = Math.Max(12, Z(12));
			}
			if (c is TableLayoutPanel tableLayoutPanel)
			{
				for (int k = 0; k < tableLayoutPanel.RowStyles.Count && k < b.Rows.Length; k++)
				{
					if (tableLayoutPanel.RowStyles[k].SizeType == SizeType.Absolute)
					{
						tableLayoutPanel.RowStyles[k].Height = Z((int)b.Rows[k]);
					}
				}
				for (int l = 0; l < tableLayoutPanel.ColumnStyles.Count && l < b.Cols.Length; l++)
				{
					if (tableLayoutPanel.ColumnStyles[l].SizeType == SizeType.Absolute)
					{
						tableLayoutPanel.ColumnStyles[l].Width = Z((int)b.Cols[l]);
					}
				}
			}
			if (c is ToolStrip toolStrip)
			{
				if (toolStrip is MenuStrip)
				{
					toolStrip.Height = Math.Max(Z(b.Bounds.Height), c.Font.Height + 5);
				}
				toolStrip.ImageScalingSize = new Size(Z(b.Images.Width), Z(b.Images.Height));
				foreach (ToolStripItem item4 in toolStrip.Items)
				{
					ApplyItem(item4);
				}
			}
			if (c is JournalHeaderBar journalHeaderBar)
			{
				journalHeaderBar.ApplyDpi(fontDpi / 96f);
			}
		}
		finally
		{
			c.ResumeLayout(performLayout: true);
		}
	}

	private void ApplyItem(ToolStripItem item)
	{
		if (!itemSpecs.TryGetValue(item, out var value))
		{
			return;
		}
		item.Font = ZoomFont(value.Font, CurrentZoom);
		item.Margin = Z(value.Margin);
		item.Padding = Z(value.Padding);
		if (!item.AutoSize)
		{
			item.Size = new Size(Z(value.Size.Width), Z(value.Size.Height));
		}
		if (!(item is ToolStripDropDownItem toolStripDropDownItem))
		{
			return;
		}
		foreach (ToolStripItem dropDownItem in toolStripDropDownItem.DropDownItems)
		{
			ApplyItem(dropDownItem);
		}
	}

	private void ScaleNewJournalControls()
	{
		CaptureScale(journalTabs);
		foreach (Control item in Descendants(journalTabs).Reverse())
		{
			ApplyControl(item, scaleSpecs[item]);
		}
	}

	private static IEnumerable<Control> Descendants(Control parent)
	{
		yield return parent;
		if (parent is DataGridView)
		{
			yield break;
		}
		foreach (Control child in parent.Controls)
		{
			foreach (Control item in Descendants(child))
			{
				yield return item;
			}
		}
	}

	private void HookSepeWrites()
	{
		if (Engine.SimRail == null)
		{
			return;
		}
		Engine.SimRailTimeChanged += QueueSepeWrite;
		FormClosed += (object s, FormClosedEventArgs e) =>
		{
			closingSession = true;
			Engine.SimRailTimeChanged -= QueueSepeWrite;
			if (hoursCancellation != null)
			{
				hoursCancellation.Cancel();
			}
			if (backgroundWriter != null)
			{
				backgroundWriter.Dispose();
			}
		};
	}

	private static SepeWriteRequest MergeSepeHours(SepeWriteRequest first, SepeWriteRequest next)
	{
		SepeWriteRequest sepeWriteRequest = new SepeWriteRequest();
		sepeWriteRequest.TrainId = next.TrainId;
		sepeWriteRequest.Number = next.Number;
		sepeWriteRequest.ExternalNumber = next.ExternalNumber;
		sepeWriteRequest.Origin = next.Origin;
		sepeWriteRequest.Destination = next.Destination;
		sepeWriteRequest.PointIndex = next.PointIndex;
		sepeWriteRequest.PlanArrival = next.PlanArrival;
		sepeWriteRequest.PlanDeparture = next.PlanDeparture;
		sepeWriteRequest.ArrivalChanged = first.ArrivalChanged || next.ArrivalChanged;
		sepeWriteRequest.DepartureChanged = first.DepartureChanged || next.DepartureChanged;
		sepeWriteRequest.Arrival = (next.ArrivalChanged ? next.Arrival : first.Arrival);
		sepeWriteRequest.Departure = (next.DepartureChanged ? next.Departure : first.Departure);
		sepeWriteRequest.DepartureCorrection = (next.DepartureChanged ? next.DepartureCorrection : first.DepartureCorrection);
		sepeWriteRequest.AdjustDepartureForArrival = first.AdjustDepartureForArrival || next.AdjustDepartureForArrival;
		return sepeWriteRequest;
	}

	private SepeWriteRequest RestoreSepeWrite(SepeWriteRequest failed)
	{
		List<SepeWriteRequest> source = outgoingHours.ToList();
		outgoingHours.Clear();
		foreach (SepeWriteRequest item in source.Where((SepeWriteRequest x) => x.TrainId == failed.TrainId))
		{
			failed = MergeSepeHours(failed, item);
		}
		outgoingHours.Enqueue(failed);
		foreach (SepeWriteRequest item2 in source.Where((SepeWriteRequest x) => x.TrainId != failed.TrainId))
		{
			outgoingHours.Enqueue(item2);
		}
		return failed;
	}

	private SepeWriteRequest CollectPendingSepeHours(SepeWriteRequest request)
	{
		List<SepeWriteRequest> list = outgoingHours.ToList();
		outgoingHours.Clear();
		foreach (SepeWriteRequest item in list)
		{
			if (item.TrainId == request.TrainId)
			{
				request = MergeSepeHours(request, item);
			}
			else
			{
				outgoingHours.Enqueue(item);
			}
		}
		Train train = Engine.State.Trains.FirstOrDefault((Train x) => x.Id == request.TrainId && x.StationId == Engine.StationId && x.SourceProvider == "SimRail");
		if (train != null && train.ExternalManualTimes)
		{
			request = MergeSepeHours(request, SepeWriteRequest.From(train, train.ExternalManualArrival, train.ExternalManualDeparture));
		}
		bool flag = request.ArrivalChanged && request.Arrival.HasValue && Engine.CanAdjustSepeDepartureForArrival(train);
		if (request.DepartureCorrection && !flag)
		{
			request.DepartureCorrection = false;
			request.DepartureChanged = false;
			request.Departure = train?.Departure;
		}
		request.AdjustDepartureForArrival = flag && (!request.DepartureChanged || request.DepartureCorrection);
		return request;
	}

	private SepeWriteRequest PrepareSepeHours(SepeWriteRequest request)
	{
		if (!FlushJournals())
		{
			throw new TrainTimeException("Nie wysłano godzin: popraw bieżącą edycję w dzienniku ruchu.");
		}
		return CollectPendingSepeHours(request);
	}

	private void QueueSepeWrite(SepeWriteRequest request)
	{
		if (CanSendSepeHours)
		{
			outgoingHours.Enqueue(request);
			if (IsHandleCreated && !IsDisposed && !closingSession)
			{
				BeginInvoke(new Action(ProcessSepeWrites));
			}
		}
	}

	internal async void ProcessSepeWrites()
	{
		if (hoursTask == null || hoursTask.IsCompleted)
		{
			hoursTask = SendNextSepeHours();
			await hoursTask;
		}
	}

	private async Task SendNextSepeHours()
	{
		if (!CanSendSepeHours)
		{
			if (!sendingHours)
			{
				outgoingHours.Clear();
			}
		}
		else
		{
			if (sendingHours || simulatorRefreshing || SharedSessionBusy || hoursPaused || decidingExit || closingSession || outgoingHours.Count == 0 || IsDisposed)
			{
				return;
			}
			SepeWriteRequest request = outgoingHours.Dequeue();
			sendingHours = true;
			hoursCancellation = CancellationTokenSource.CreateLinkedTokenSource(simulatorCancellation.Token);
			CancellationToken token = hoursCancellation.Token;
			string notice = null;
			string noticeTitle = null;
			try
			{
				int num = default;
				_ = num;
				_ = 0;
				try
				{
					simulatorState.Text = "SEPE: wysyłanie godzin pociągu " + request.Number;
					if (simulatorState.OwnerItem != null)
					{
						simulatorState.OwnerItem.Text = "SimRail *";
					}
					await SepeChronologyRetry.Run(async (int retry, CancellationToken attemptToken) =>
					{
						request = PrepareSepeHours(request);
						SimRailSnapshot fresh = await Engine.SimRail.Feed.SnapshotAsync(Engine.SimRail.ServerCode, attemptToken);
						attemptToken.ThrowIfCancellationRequested();
						Engine.ApplySimRailSnapshot(fresh);
						request = PrepareSepeHours(request);
						if (!request.Confirmed(fresh, Engine.SimRail.PointId))
						{
							await SepeRetry.Run(async (int number) =>
							{
								attemptToken.ThrowIfCancellationRequested();
								if (number > 1 && backgroundWriter != null)
								{
									backgroundWriter.Dispose();
									backgroundWriter = null;
								}
								if (backgroundWriter == null)
								{
									backgroundWriter = ((HoursWriterFactory == null) ? new SepeBackgroundWriter(this, Engine.SimRail) : HoursWriterFactory());
								}
								ISepeHoursWriter writer = backgroundWriter;
								SepeBackgroundWriter official = writer as SepeBackgroundWriter;
								Exception sendError = null;
								try
								{
									await AwaitSepeSend((official == null) ? writer.Send(request) : official.Send(request, () => request = PrepareSepeHours(request)), attemptToken);
								}
								catch (Exception ex)
								{
									sendError = ex;
								}
								attemptToken.ThrowIfCancellationRequested();
								if (sendError != null && (!writer.SubmissionAttempted || sendError is TrainTimeException || sendError is SepeChronologyException))
								{
									throw sendError;
								}
								SepeConfirmationResult result = await SepeConfirmation.Read(request, Engine.SimRail, (SimRailSnapshot snapshot) =>
								{
									if (!closingSession && !IsDisposed)
									{
										Engine.ApplySimRailSnapshot(snapshot);
									}
								}, (int attempt) =>
								{
									if (!closingSession && !IsDisposed)
									{
										simulatorState.Text = "SEPE: odczyt potwierdzenia " + attempt + "/3 - " + request.Number;
									}
								}, attemptToken);
								if (!result.Confirmed)
								{
									throw new InvalidOperationException(((sendError == null) ? "" : (SepeDiagnostics.Reason(sendError) + "\r\n")) + result.Reason);
								}
							}, () => backgroundWriter == null || !backgroundWriter.SubmissionAttempted, (int attempt) =>
							{
								if (!closingSession && !IsDisposed)
								{
									simulatorState.Text = "SEPE: przygotowanie godzin, próba " + attempt + "/3 - " + request.Number;
								}
							}, attemptToken);
						}
					}, (int wait, int remaining) =>
					{
						if (backgroundWriter != null)
						{
							backgroundWriter.Dispose();
							backgroundWriter = null;
						}
						if (!closingSession && !IsDisposed)
						{
							simulatorState.Text = "SEPE: kolejna próba za " + wait + " s, pozostało " + remaining + " s - " + request.Number;
						}
					}, token);
					if (closingSession || IsDisposed)
					{
						return;
					}
					RefreshData();
					simulatorState.Text = "SEPE: potwierdzono zapis pociągu " + request.Number;
					if (simulatorState.OwnerItem != null)
					{
						simulatorState.OwnerItem.Text = "SimRail";
					}
				}
				catch (Exception error)
				{
					request = RestoreSepeWrite(request);
					if (closingSession || IsDisposed)
					{
						return;
					}
					hoursPaused = true;
					if (backgroundWriter != null)
					{
						backgroundWriter.Dispose();
						backgroundWriter = null;
					}
					if (token.IsCancellationRequested)
					{
						simulatorState.Text = "SEPE: wysyłanie przerwane; godziny pozostają do ponowienia.";
						return;
					}
					simulatorState.Text = "SEPE: nie potwierdzono godzin pociągu " + request.Number;
					if (simulatorState.OwnerItem != null)
					{
						simulatorState.OwnerItem.Text = "SimRail (! " + request.Number + ")";
					}
					string reason = SepeDiagnostics.Reason(error);
					lastSepeError = SepeDiagnostics.Report(request, Engine.SimRail, reason);
					notice = SepeDiagnostics.Notice(request, Engine.Station.Name, reason);
					noticeTitle = "SEPE - pociąg " + request.Number;
				}
			}
			finally
			{
				sendingHours = false;
				hoursCancellation.Dispose();
				hoursCancellation = null;
				if (!closingSession && !IsDisposed && !hoursPaused)
				{
					BeginInvoke(new Action(ProcessSepeWrites));
				}
			}
			if (notice != null)
			{
				PostOperatorNotice(noticeTitle, notice);
			}
		}
	}

	private static async Task AwaitSepeSend(Task send, CancellationToken token)
	{
		TaskCompletionSource<bool> cancelled = new TaskCompletionSource<bool>();
		Action callback = () =>
		{
			cancelled.TrySetResult(result: true);
		};
		using (token.Register(callback))
		{
			if (await Task.WhenAny(send, cancelled.Task) != send)
			{
				ObserveSepeSend(send);
				token.ThrowIfCancellationRequested();
			}
			await send;
		}
	}

	private static async void ObserveSepeSend(Task send)
	{
		int num = default;
		_ = num;
		_ = 0;
		try
		{
			await send;
		}
		catch
		{
		}
	}

	private void ShowSepeError()
	{
		if (string.IsNullOrEmpty(lastSepeError))
		{
			MessageBox.Show(this, "W tej sesji nie zarejestrowano błędu zapisu SEPE.", "SEPE");
			return;
		}
		using SepeErrorForm sepeErrorForm = new SepeErrorForm(lastSepeError);
		sepeErrorForm.ShowDialog(this);
	}

	private void RetrySepeWrites()
	{
		if (Engine.SimRail == null || !Engine.SimRail.HasWriteLink)
		{
			return;
		}
		if (!Engine.SepeWritesEnabled)
		{
			throw new InvalidOperationException("Zapis do SEPE jest wyłączony w konfiguracji posterunku, zakładka Inne.");
		}
		if (!FlushJournals() || sendingHours || closingSession)
		{
			return;
		}
		List<SepeWriteRequest> list = outgoingHours.ToList();
		outgoingHours.Clear();
		foreach (Train item2 in Engine.State.Trains.Where((Train x) => x.StationId == Engine.StationId && x.SourceProvider == "SimRail" && x.ExternalManualTimes))
		{
			list.Add(SepeWriteRequest.From(item2, item2.ExternalManualArrival, item2.ExternalManualDeparture));
		}
		foreach (IGrouping<string, SepeWriteRequest> item3 in from x in list
			group x by x.TrainId)
		{
			SepeWriteRequest item = item3.Aggregate(MergeSepeHours);
			outgoingHours.Enqueue(item);
		}
		hoursPaused = false;
		if (outgoingHours.Count == 0)
		{
			simulatorState.Text = "SEPE: brak lokalnych godzin oczekujących na wysłanie.";
		}
		else
		{
			ProcessSepeWrites();
		}
	}

	private void OpenSessionWindow()
	{
		if (sessionWindows == null)
		{
			new SessionWindows(this);
		}
		sessionWindows.Open(this);
	}

	private void LogoutSession()
	{
		if (sessionWindows != null)
		{
			sessionWindows.Logout();
			return;
		}
		DialogResult = DialogResult.Retry;
		Close();
	}

	private static IEnumerable<Control> WindowFields(Control root)
	{
		foreach (Control child in root.Controls)
		{
			yield return child;
			foreach (Control item in WindowFields(child))
			{
				yield return item;
			}
		}
	}

	internal void CopyWindowView(MainForm next)
	{
		next.StartPosition = FormStartPosition.Manual;
		next.Size = Size;
		Rectangle workingArea = Screen.FromControl(this).WorkingArea;
		next.Location = new Point(Math.Max(workingArea.Left, Math.Min(Left + 30, workingArea.Right - next.Width)), Math.Max(workingArea.Top, Math.Min(Top + 30, workingArea.Bottom - next.Height)));
		List<Control> source = WindowFields(filterPanel).ToList();
		List<Control> source2 = WindowFields(next.filterPanel).ToList();
		foreach (NumericUpDown[] item in source.OfType<NumericUpDown>().Zip(source2.OfType<NumericUpDown>(), (NumericUpDown a, NumericUpDown b) => new NumericUpDown[2] { a, b }))
		{
			item[1].Value = item[0].Value;
		}
		foreach (DomainUpDown[] item2 in source.OfType<DomainUpDown>().Zip(source2.OfType<DomainUpDown>(), (DomainUpDown a, DomainUpDown b) => new DomainUpDown[2] { a, b }))
		{
			item2[1].SelectedItem = item2[0].SelectedItem;
		}
		foreach (CheckBox[] item3 in source.OfType<CheckBox>().Zip(source2.OfType<CheckBox>(), (CheckBox a, CheckBox b) => new CheckBox[2] { a, b }))
		{
			item3[1].Checked = item3[0].Checked;
		}
		next.confirmationLink.Text = confirmationLink.Text;
		next.movementLink.Text = movementLink.Text;
		next.refreshMinutes = refreshMinutes;
		next.timeRangeHours = timeRangeHours;
		next.useTimeRange = useTimeRange;
		next.confirmationFilter = confirmationFilter;
		next.movementFilter = movementFilter;
		next.currentData.Checked = currentData.Checked;
		next.timetableDay.Value = timetableDay.Value;
		next.search.Text = search.Text;
		next.runningOnly.Checked = runningOnly.Checked;
		next.notConfirmed.Checked = notConfirmed.Checked;
		next.kindFilter.Text = kindFilter.Text;
		next.trains.Tag = trains.Tag;
		next.ApplyDateColumnWidths();
		next.selectedTrainId = selectedTrainId;
		next.SetLayout(currentLayout, persist: false);
		next.RefreshData();
		next.topTabs.SelectedIndex = Math.Min(topTabs.SelectedIndex, next.topTabs.TabCount - 1);
		next.journalTabs.SelectedIndex = Math.Min(journalTabs.SelectedIndex, next.journalTabs.TabCount - 1);
		foreach (JournalPane pane in next.panes)
		{
			List<JournalPane> source3 = panes;
			Func<JournalPane, bool> predicate = (JournalPane x) => x.SectionId == pane.SectionId;
			JournalPane journalPane = source3.FirstOrDefault(predicate);
			if (journalPane != null)
			{
				pane.SetDay(journalPane.SelectedDay);
			}
		}
		if (WindowState == FormWindowState.Maximized)
		{
			next.WindowState = FormWindowState.Maximized;
		}
	}

	internal void QueueSharedRefresh()
	{
		if (sharedRefreshQueued || IsDisposed || closingSession || !IsHandleCreated)
		{
			return;
		}
		sharedRefreshQueued = true;
		BeginInvoke((Action)(() =>
		{
			sharedRefreshQueued = false;
			if (!IsDisposed && !closingSession)
			{
				Ui.Run(this, RefreshSharedView);
			}
		}));
	}

	internal void RefreshSharedView()
	{
		if (!Engine.State.Stations.Any((Station x) => x.Id == Engine.StationId))
		{
			Engine.StationId = Engine.State.Stations[0].Id;
			selectedTrainId = null;
		}
		if (Engine.SimRail != null && Engine.LastAppliedSnapshot != Engine.SimRail.Snapshot)
		{
			Engine.ApplySimRailSnapshot(Engine.SimRail.Snapshot);
		}
		string[] array = Engine.State.Stations.Select(StationDisplay).ToArray();
		if (!stations.Items.Cast<string>().SequenceEqual(array))
		{
			filling = true;
			try
			{
				stations.Items.Clear();
				stations.Items.AddRange(array.Cast<object>().ToArray());
				stations.SelectedIndex = Engine.State.Stations.FindIndex((Station x) => x.Id == Engine.StationId);
			}
			finally
			{
				filling = false;
			}
		}
		string text = Json.Text(Engine.Station);
		if (windowStationSignature != text && !panes.Any((JournalPane x) => x.HasPendingWindowDraft))
		{
			BuildJournals();
			windowStationSignature = text;
		}
		RefreshData();
	}

	private void AddSimulatorMenu(MenuStrip menu)
	{
		if (Engine.SimRail == null)
		{
			return;
		}
		if (Engine.IsTd2)
		{
			AddTd2Menu(menu);
			return;
		}
		ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem("SimRail");
		toolStripMenuItem.DropDownItems.Add(Ui.Menu("Link SEPE z gry…", SetSepeLink));
		toolStripMenuItem.DropDownItems.Add(Ui.Menu("Otwórz oficjalne SEPE…", () =>
		{
			OpenSepe(null);
		}));
		ToolStripMenuItem send = Ui.Menu("Wyślij godziny wybranego pociągu", () =>
		{
			TransferSepeHours();
		});
		ToolStripMenuItem retry = Ui.Menu("Wyślij oczekujące godziny", RetrySepeWrites);
		ToolStripMenuItem toolStripMenuItem2 = send;
		bool enabled = (retry.Enabled = CanSendSepeHours);
		toolStripMenuItem2.Enabled = enabled;
		toolStripMenuItem.DropDownItems.Add(send);
		toolStripMenuItem.DropDownItems.Add(retry);
		toolStripMenuItem.DropDownOpening += (object s, EventArgs e) =>
		{
			ToolStripMenuItem toolStripMenuItem3 = send;
			bool enabled2 = (retry.Enabled = CanSendSepeHours);
			toolStripMenuItem3.Enabled = enabled2;
		};
		toolStripMenuItem.DropDownItems.Add(Ui.Menu("Ostatni błąd zapisu SEPE…", ShowSepeError));
		toolStripMenuItem.DropDownItems.Add(Ui.Menu("Wróć do danych SimRail / odśwież teraz", () =>
		{
			RefreshSimulator(interactive: true);
		}));
		simulatorState = new ToolStripMenuItem("Dane pobrane: " + Engine.Now.ToString("HH:mm:ss"))
		{
			Enabled = false
		};
		toolStripMenuItem.DropDownItems.Add(simulatorState);
		menu.Items.Add(toolStripMenuItem);
	}

	private async void SetSepeLink()
	{
		try
		{
			if (sendingHours && (hoursTask == null || hoursTask.IsCompleted))
			{
				throw new InvalidOperationException("Poczekaj na zakończenie operacji Pociąg STÓJ / prognozy przed zmianą linku SEPE.");
			}
			string value = null;
			using (Editor editor = new Editor("Aktualny link SEPE z gry", 720, 260))
			{
				TextBox field = editor.Add("Link SEPE", Ui.Text("", multiline: false), 35);
				field.UseSystemPasswordChar = true;
				editor.Info("Link jest używany tylko w tej sesji. Oczekujące godziny zostaną zachowane; po zmianie użyj Wyślij oczekujące godziny.", 70);
				editor.Save(() =>
				{
					ValidateReplacementLink(field.Text);
					value = field.Text;
				});
				if (editor.ShowDialog(this) != DialogResult.OK)
				{
					return;
				}
			}
			await ReplaceSepeLink(value);
			if (!closingSession && !IsDisposed)
			{
				Engine.ImportControlledStationData();
				Engine.EnsureControlledStations();
				filling = true;
				stations.Items.Clear();
				stations.Items.AddRange(Engine.State.Stations.Select(StationDisplay).Cast<object>().ToArray());
				stations.SelectedIndex = Engine.State.Stations.FindIndex((Station x) => x.Id == Engine.StationId);
				filling = false;
			}
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			if (!closingSession && !IsDisposed)
			{
				Ui.Run(this, () =>
				{
					throw ex2;
				});
			}
		}
	}

	internal void ValidateReplacementLink(string value)
	{
		if (Engine.IsTd2)
		{
			throw new InvalidOperationException("TD2 pobiera tylko rozkład jazdy.");
		}
		SimRailSession simRailSession = new SimRailSession(Engine.SimRail.Feed, Engine.SimRail.Snapshot, Engine.SimRail.PointId);
		simRailSession.SetAccessLink(value);
	}

	private async Task ReplaceSepeLink(string value)
	{
		if (sessionWindows != null)
		{
			await sessionWindows.ReplaceLink(this, value);
			return;
		}
		ValidateReplacementLink(value);
		if (sendingHours && (hoursTask == null || hoursTask.IsCompleted))
		{
			throw new InvalidOperationException("Trwa operacja Pociąg STÓJ / prognozy.");
		}
		hoursPaused = true;
		if (hoursCancellation != null)
		{
			hoursCancellation.Cancel();
		}
		if (backgroundWriter != null)
		{
			backgroundWriter.Dispose();
			backgroundWriter = null;
		}
		if (hoursTask != null)
		{
			await hoursTask;
		}
		if (!closingSession && !IsDisposed)
		{
			Engine.SimRail.SetAccessLink(value);
			simulatorState.Text = "SEPE: link zmieniony. Użyj Wyślij oczekujące godziny.";
		}
	}

	private void OpenSepe(Train train)
	{
		if (Engine.SimRail == null)
		{
			return;
		}
		if (sepeWindow == null || sepeWindow.IsDisposed)
		{
			sepeWindow = new SepeBrowserForm(Engine.SimRail, train?.Number);
			sepeWindow.FormClosed += (object s, FormClosedEventArgs e) =>
			{
				RefreshSimulator(interactive: false);
			};
			sepeWindow.Show(this);
		}
		else
		{
			sepeWindow.Activate();
			sepeWindow.SelectTrain(train?.Number);
		}
	}

	private void TransferSepeHours()
	{
		if (Engine.SimRail == null || !Engine.SimRail.HasWriteLink)
		{
			return;
		}
		Engine.RequireWrite();
		if (!Engine.SepeWritesEnabled)
		{
			throw new InvalidOperationException("Zapis do SEPE jest wyłączony w konfiguracji posterunku, zakładka Inne.");
		}
		string id = NeedTrain().Id;
		if (FlushJournals())
		{
			Train train = Engine.State.Trains.FirstOrDefault((Train x) => x.Id == id);
			if (train == null)
			{
				throw new Exception("Wybrany pociąg zniknął z rozkładu. Wybierz go ponownie.");
			}
			if (!train.Arrival.HasValue && !train.Departure.HasValue)
			{
				throw new Exception("Pociąg nie ma wpisanych godzin rzeczywistych. Uzupełnij je w dzienniku ruchu.");
			}
			hoursPaused = false;
			QueueSepeWrite(SepeWriteRequest.From(train, train.Arrival.HasValue, train.Departure.HasValue));
		}
	}

	private async void RefreshSimulator(bool interactive)
	{
		if (Engine.SimRail == null || simulatorRefreshing || sendingHours || SharedSessionBusy || decidingExit || closingSession || IsDisposed || (savedTimetableView && !interactive))
		{
			return;
		}
		simulatorRefreshing = true;
		try
		{
			int num = default;
			_ = num;
			_ = 0;
			try
			{
				simulatorState.Text = "Pobieranie danych…";
				SimRailSnapshot snapshot = await Engine.SimRail.Feed.SnapshotAsync(Engine.SimRail.ServerCode, simulatorCancellation.Token);
				if (closingSession || decidingExit || IsDisposed)
				{
					return;
				}
				if (!FlushJournals())
				{
					simulatorState.Text = "Odświeżanie wstrzymane: popraw edytowany wpis.";
					return;
				}
				Engine.ApplySimRailSnapshot(snapshot);
				savedTimetableView = false;
				if (currentData.Checked)
				{
					timetableDay.Value = Engine.Now;
				}
				RefreshData();
				simulatorState.Text = "Dane pobrane: " + Engine.Now.ToString("HH:mm:ss");
			}
			catch (Exception)
			{
				if (!IsDisposed)
				{
					simulatorState.Text = "Brak połączenia - zachowano ostatnie dane";
					if (interactive)
					{
						MessageBox.Show(this, "Nie udało się odświeżyć danych " + Engine.SimulatorCaption + ". Zachowano poprzedni rozkład i lokalne wpisy.", Engine.SimulatorCaption);
					}
				}
			}
		}
		finally
		{
			simulatorRefreshing = false;
		}
	}

	private void RefreshRuntimeIndicators()
	{
		foreach (JournalPane pane in panes)
		{
			pane.RefreshSignals();
			pane.RefreshClockSuggestions();
		}
		StationOptions options = Engine.Station.Options;
		if ((options == null || options.RefreshStationTracks != false) && !(DateTime.Now < nextStationTrackRefresh))
		{
			RefreshTracks();
			int num = ((options == null) ? 10 : Math.Max(1, options.StationTrackRefreshMinutes));
			nextStationTrackRefresh = DateTime.Now.AddMinutes(num);
		}
	}

	private void CloseStationTrackPopup()
	{
		if (stationTrackPopup != null)
		{
			stationTrackPopup.Dispose();
			stationTrackPopup = null;
		}
	}

	private void ShowStationTrackPopup(Button button, string track)
	{
		CloseStationTrackPopup();
		List<StationTrackItem> list = Engine.StationTrackItems(track);
		if (list.Count == 0)
		{
			return;
		}
		if (!trackPopupEvents)
		{
			trackPopupEvents = true;
			Deactivate += (object s, EventArgs e) =>
			{
				CloseStationTrackPopup();
			};
			LocationChanged += (object s, EventArgs e) =>
			{
				CloseStationTrackPopup();
			};
			SizeChanged += (object s, EventArgs e) =>
			{
				CloseStationTrackPopup();
			};
			Disposed += (object s, EventArgs e) =>
			{
				CloseStationTrackPopup();
			};
		}
		stationTrackPopup = new StationTrackPopup(button, Engine, list, RemoveStationTrackItem);
		stationTrackPopup.Show(this);
	}

	private void RemoveStationTrackItem(StationTrackItem item)
	{
		if (FlushJournals())
		{
			Engine.RequireWrite();
			string arg = "Usunąć pociąg / zajętość „" + item.Number + "” z toru " + item.Track + "?";
			if ((ConfirmStationTrackRemoval == null) ? (MessageBox.Show(this, arg, "Usunięcie zajętości toru", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) : ConfirmStationTrackRemoval(arg))
			{
				Engine.RemoveStationTrackOccupation(item.Key);
				RefreshTracks();
			}
		}
	}

	private void AddStationTrackItem(string track)
	{
		if (!Engine.CanWrite || !FlushJournals())
		{
			return;
		}
		using Editor editor = new Editor("Tor stacyjny - nowa zajętość", 405, 205);
		editor.Info("Tor stacyjny numer: " + track, 32);
		TextBox number = editor.Add("Pociąg numer / opis:", Ui.Text("", multiline: false), 35);
		number.MaxLength = 120;
		editor.Save(() =>
		{
			Engine.AddStationTrackOccupation(track, number.Text);
			RefreshTracks();
		});
		editor.Shown += (object s, EventArgs e) =>
		{
			number.Focus();
		};
		editor.ShowDialog(this);
	}

	private ContextMenuStrip StationTrackMenu(string track)
	{
		ContextMenuStrip menu = new ContextMenuStrip();
		menu.Opening += (object s, CancelEventArgs e) =>
		{
			menu.Items.Clear();
			foreach (StationTrackItem item in Engine.StationTrackItems(track))
			{
				StationTrackItem captured = item;
				ToolStripMenuItem toolStripMenuItem = Ui.Menu("Usuń pociąg " + item.Number, () =>
				{
					RemoveStationTrackItem(captured);
				});
				toolStripMenuItem.Enabled = Engine.CanWrite;
				menu.Items.Add(toolStripMenuItem);
			}
			if (menu.Items.Count == 0)
			{
				e.Cancel = true;
			}
		};
		return menu;
	}

	private bool CanDropStationTrack(IDataObject data)
	{
		if (!Engine.CanWrite || !data.GetDataPresent(typeof(StationTrackDrag)))
		{
			return false;
		}
		if (data.GetData(typeof(StationTrackDrag)) is StationTrackDrag stationTrackDrag && stationTrackDrag.Store == Engine.Store)
		{
			return stationTrackDrag.StationId == Engine.StationId;
		}
		return false;
	}

	private void AttachStationTrackButton(Button button, string track)
	{
		button.Name = "stationTrack_" + track;
		button.Tag = track;
		button.Click += (object s, EventArgs e) =>
		{
			Ui.Run(this, () =>
			{
				AddStationTrackItem(track);
			});
		};
		button.MouseEnter += (object s, EventArgs e) =>
		{
			ShowStationTrackPopup(button, track);
		};
		ContextMenuStrip menu = StationTrackMenu(track);
		button.ContextMenuStrip = menu;
		button.Disposed += (object s, EventArgs e) =>
		{
			menu.Dispose();
		};
		button.AllowDrop = true;
		button.DragEnter += (object s, DragEventArgs e) =>
		{
			e.Effect = (CanDropStationTrack(e.Data) ? DragDropEffects.Move : DragDropEffects.None);
		};
		button.DragOver += (object s, DragEventArgs e) =>
		{
			e.Effect = (CanDropStationTrack(e.Data) ? DragDropEffects.Move : DragDropEffects.None);
		};
		button.DragDrop += (object s, DragEventArgs e) =>
		{
			if (CanDropStationTrack(e.Data))
			{
				StationTrackDrag item = (StationTrackDrag)e.Data.GetData(typeof(StationTrackDrag));
				BeginInvoke((Action)(() =>
				{
					Ui.Run(this, () =>
					{
						if (FlushJournals())
						{
							Engine.MoveStationTrackOccupation(item.Key, track);
							RefreshTracks();
						}
					});
				}));
			}
		};
	}

	private void TrainStop()
	{
		Engine.RequireWrite();
		Train t = NeedTrain();
		if (Engine.SimRail != null && (sendingHours || simulatorRefreshing || SharedSessionBusy || outgoingHours.Count > 0))
		{
			throw new InvalidOperationException("Poczekaj na zakończenie wysyłania godzin do SEPE.");
		}
		using StopForecastForm stopForecastForm = new StopForecastForm(t, async (StopForecastOperation operation, DateTime arrival, DateTime departure) =>
		{
			if (Engine.SimRail == null)
			{
				Engine.SetLocalForecast(t.Id, operation, arrival, departure);
				return;
			}
			if (!Engine.SepeWritesEnabled)
			{
				throw new InvalidOperationException("Zapis do SEPE jest wyłączony w konfiguracji posterunku.");
			}
			if (Engine.State.Trains.Any((Train x) => x.Id == t.Id && x.ExternalManualTimes))
			{
				throw new InvalidOperationException("Najpierw wyślij oczekujące rzeczywiste godziny tego pociągu.");
			}
			if (sendingHours || simulatorRefreshing || SharedSessionBusy || outgoingHours.Count > 0 || closingSession || decidingExit)
			{
				throw new InvalidOperationException("Trwa inna operacja SEPE. Poczekaj na jej zakończenie.");
			}
			StopForecastRequest request = StopForecastRequest.From(t, Engine.SimRail, operation, arrival, departure);
			request.Validate(Engine.SimRail);
			sendingHours = true;
			try
			{
				SimRailSnapshot fresh = await Engine.SimRail.Feed.SnapshotAsync(Engine.SimRail.ServerCode, simulatorCancellation.Token);
				if (closingSession || IsDisposed)
				{
					throw new OperationCanceledException();
				}
				Engine.ApplySimRailSnapshot(fresh);
				request.Validate(Engine.SimRail);
				using SepeBackgroundWriter writer = new SepeBackgroundWriter(this, Engine.SimRail);
				Exception failure = null;
				try
				{
					await writer.SendOperation(request, (string message) => Task.FromResult(!Ui.Testing && MessageBox.Show(Form.ActiveForm ?? this, message, "Prognoza przyjazdu / odjazdu", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes));
				}
				catch (Exception ex)
				{
					failure = ex;
				}
				if (failure is ForecastDeclinedException)
				{
					throw failure;
				}
				if (failure != null && !writer.SubmissionAttempted)
				{
					throw failure;
				}
				if (!(await StopForecastRequest.Read(request, Engine.SimRail, (SimRailSnapshot snapshot) =>
				{
					if (!closingSession && !IsDisposed)
					{
						Engine.ApplySimRailSnapshot(snapshot);
					}
				}, simulatorCancellation.Token)))
				{
					throw new InvalidOperationException("Nie potwierdzono " + request.Description + " w trzech odczytach API. Odśwież dane przed ponowną próbą.\r\n" + request.LastConfirmationError + ((failure == null) ? "" : ("\r\n" + SepeDiagnostics.Reason(failure))));
				}
				simulatorState.Text = "SEPE: potwierdzenie " + request.Description + " pociągu " + t.Number;
			}
			finally
			{
				sendingHours = false;
				if (!closingSession && !IsDisposed && !hoursPaused && outgoingHours.Count > 0)
				{
					BeginInvoke(new Action(ProcessSepeWrites));
				}
			}
		});
		if (stopForecastForm.ShowDialog(this) == DialogResult.OK)
		{
			RefreshData();
		}
	}

	private void AddTd2Menu(MenuStrip menu)
	{
		ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem("TD2");
		toolStripMenuItem.DropDownItems.Add(Ui.Menu("Odśwież rozkład jazdy", () =>
		{
			RefreshSimulator(interactive: true);
		}));
		simulatorState = new ToolStripMenuItem("Dane pobrane: " + Engine.Now.ToString("HH:mm:ss"))
		{
			Enabled = false
		};
		toolStripMenuItem.DropDownItems.Add(simulatorState);
		menu.Items.Add(toolStripMenuItem);
	}

	private static string Cell(DetailRow row, int index)
	{
		if (row.Cells == null || index >= row.Cells.Length)
		{
			return "";
		}
		return row.Cells[index] ?? "";
	}

	private IEnumerable<DetailRow> Records(Train t, string kind)
	{
		if (kind == "route" && Engine.SimRail != null && (t.SourceProvider == "SimRail" || t.SourceProvider == "TD2"))
		{
			return Engine.SimRail.Route(t);
		}
		return (t.DetailRows ?? new List<DetailRow>()).Where((DetailRow x) => x.Kind == kind);
	}

	private Panel DetailBody(string title)
	{
		TabPage tabPage = new TabPage(title);
		tabPage.BackColor = Color.White;
		tabPage.Padding = new Padding(2);
		TabPage tabPage2 = tabPage;
		Panel panel = new Panel();
		panel.Dock = DockStyle.Fill;
		panel.BackColor = Color.White;
		Panel panel2 = panel;
		tabPage2.Controls.Add(panel2);
		topTabs.TabPages.Add(tabPage2);
		return panel2;
	}

	private Label Band(string text, int height)
	{
		Label label = new Label();
		label.Text = text;
		label.Dock = DockStyle.Top;
		label.Height = height;
		label.BackColor = Color.FromArgb(204, 204, 204);
		label.ForeColor = Color.Navy;
		label.TextAlign = ContentAlignment.MiddleCenter;
		label.Font = ReferenceTheme.SmallBold;
		return label;
	}

	private DataGridView DetailGrid(string kind, string[] headers, int[] widths)
	{
		DataGridView g = Ui.Grid(journal: false);
		ReferenceTheme.Grid(g, journal: false);
		g.Tag = "detail";
		if (kind == "TN")
		{
			g.DefaultCellStyle.BackColor = Color.FromArgb(240, 240, 240);
		}
		g.ColumnHeadersHeight = 26;
		g.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
		g.DefaultCellStyle.BackColor = Color.FromArgb(239, 239, 239);
		g.AlternatingRowsDefaultCellStyle.BackColor = Color.White;
		g.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
		for (int i = 0; i < headers.Length; i++)
		{
			Ui.Column(g, "c" + i, headers[i], (widths == null) ? 110 : widths[i]);
		}
		g.Columns[g.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
		g.Columns[g.Columns.Count - 1].MinimumWidth = 80;
		detailGrids[kind] = g;
		if (kind != "TN" && kind != "route")
		{
			ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
			contextMenuStrip.Items.Add(Ui.Menu("Dodaj dane lokalne…", () =>
			{
				EditDetailRow(kind, null);
			}));
			contextMenuStrip.Items.Add(Ui.Menu("Edytuj dane lokalne…", () =>
			{
				if (g.CurrentRow != null)
				{
					EditDetailRow(kind, g.CurrentRow.Tag as DetailRow);
				}
			}));
			contextMenuStrip.Items.Add(Ui.Menu("Usuń dane lokalne…", () =>
			{
				Engine.RequireWrite();
				Train t = NeedTrain();
				if (g.CurrentRow != null)
				{
					DetailRow row = g.CurrentRow.Tag as DetailRow;
					if (row != null)
					{
						Engine.Change("Usunięcie danych " + kind, t.Id, (AppState s) =>
						{
							s.Trains.First((Train x) => x.Id == t.Id).DetailRows.RemoveAll((DetailRow x) => x.Id == row.Id);
						});
						RefreshDetails();
					}
				}
			}));
			g.ContextMenuStrip = contextMenuStrip;
		}
		if (kind == "orders")
		{
			g.CellDoubleClick += (object s, DataGridViewCellEventArgs e) =>
			{
				if (e.RowIndex >= 0)
				{
					OpenOrder((DetailRow)g.Rows[e.RowIndex].Tag);
				}
			};
		}
		return g;
	}

	private void BuildDetails()
	{
		BuildDescription();
		BuildRoute();
		Panel panel = DetailBody("Kody opóźnień");
		panel.Controls.Add(DetailGrid("delays", new string[9] { "Winny", "Przyczyna", "Opóźnienie", "Lokalizacja", "Opis przyczyny", "Opis lokalizacji", "Stacja wprowadz.", "W", "IZ opóźn. pierwotnego" }, new int[9] { 42, 55, 60, 65, 330, 220, 105, 25, 115 }));
		panel.Controls.Add(Band("Kody opóźnień", 22));
		Panel panel2 = DetailBody("Przesyłki nadzwyczajne i TWR");
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
		tableLayoutPanel.Dock = DockStyle.Fill;
		tableLayoutPanel.ColumnCount = 1;
		tableLayoutPanel.RowCount = 2;
		tableLayoutPanel.Margin = Padding.Empty;
		TableLayoutPanel tableLayoutPanel2 = tableLayoutPanel;
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 62f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 38f));
		Panel panel3 = new Panel();
		panel3.Dock = DockStyle.Fill;
		Panel panel4 = panel3;
		panel4.Controls.Add(DetailGrid("extra", new string[8] { "Typ", "Przewóz od stacji", "Przewóz do stacji", "L. w.", "1. Wagon", "2. Wagon", "3. Wagon", "Opis" }, new int[8] { 50, 150, 150, 30, 120, 120, 120, 230 }));
		extraRelation = new Label
		{
			Dock = DockStyle.Top,
			Height = 20,
			BackColor = Color.FromArgb(255, 225, 199),
			TextAlign = ContentAlignment.MiddleLeft,
			Font = ReferenceTheme.Small
		};
		panel4.Controls.Add(extraRelation);
		panel4.Controls.Add(Band("Przesyłki nadzwyczajne lub inne wymagające szczególnego nadzoru", 22));
		TableLayoutPanel tableLayoutPanel3 = new TableLayoutPanel();
		tableLayoutPanel3.Dock = DockStyle.Fill;
		tableLayoutPanel3.ColumnCount = 2;
		tableLayoutPanel3.RowCount = 2;
		tableLayoutPanel3.Margin = Padding.Empty;
		TableLayoutPanel tableLayoutPanel4 = tableLayoutPanel3;
		tableLayoutPanel4.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60f));
		tableLayoutPanel4.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));
		tableLayoutPanel4.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
		tableLayoutPanel4.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		Label control = Band("Telegramy zarządzające przejazd przesyłek", 22);
		tableLayoutPanel4.Controls.Add(control, 0, 0);
		tableLayoutPanel4.SetColumnSpan(control, 2);
		tableLayoutPanel4.Controls.Add(DetailGrid("telegram-own", new string[3] { "IDDE własne / PNK / PNZ", "Nr telegramu", "Uwagi" }, new int[3] { 135, 125, 180 }), 0, 1);
		tableLayoutPanel4.Controls.Add(DetailGrid("telegram-other", new string[2] { "IDDE sąsiednie", "Nr telegramu" }, new int[2] { 180, 220 }), 1, 1);
		tableLayoutPanel2.Controls.Add(panel4, 0, 0);
		tableLayoutPanel2.Controls.Add(tableLayoutPanel4, 0, 1);
		panel2.Controls.Add(tableLayoutPanel2);
		Panel panel5 = DetailBody("Zarządzenie RJ");
		panel5.Controls.Add(DetailGrid("orders", new string[6] { "Nr zamówienia", "Numer zarządzenia", "Nadawca", "Data nadania", "Obszar", "Data zrzutu" }, new int[6] { 145, 230, 170, 165, 230, 165 }));
		panel5.Controls.Add(Band("Zarządzenie Rozkładu Jazdy Pociągu", 22));
		BuildConnections();
		Panel panel6 = DetailBody("Towary niebezpieczne");
		GroupBox groupBox = new GroupBox();
		groupBox.Text = "Historia TN";
		groupBox.Dock = DockStyle.Fill;
		groupBox.Padding = new Padding(5, 13, 5, 5);
		groupBox.Font = ReferenceTheme.Small;
		groupBox.ForeColor = Color.FromArgb(25, 65, 90);
		GroupBox groupBox2 = groupBox;
		groupBox2.Controls.Add(DetailGrid("TN", new string[4] { "Czas", "Imię i nazwisko", "Stacja", "Działanie" }, new int[4] { 170, 320, 320, 300 }));
		panel6.Controls.Add(groupBox2);
		panel6.Controls.Add(Band("Historia wprowadzania informacji o Towarach Niebezpiecznych", 27));
	}

	private void BuildConnections()
	{
		Panel panel = DetailBody("Skomunikowania / Przejścia składu");
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
		tableLayoutPanel.Dock = DockStyle.Fill;
		tableLayoutPanel.ColumnCount = 1;
		tableLayoutPanel.RowCount = 6;
		tableLayoutPanel.Margin = Padding.Empty;
		TableLayoutPanel tableLayoutPanel2 = tableLayoutPanel;
		int[] array = new int[6] { 23, 25, 22, 0, 22, 0 };
		foreach (int num in array)
		{
			tableLayoutPanel2.RowStyles.Add(new RowStyle((num != 0) ? SizeType.Absolute : SizeType.Percent, (num == 0) ? 50 : num));
		}
		connectionSummary = Band("", 23);
		tableLayoutPanel2.Controls.Add(connectionSummary, 0, 0);
		TableLayoutPanel tableLayoutPanel3 = new TableLayoutPanel();
		tableLayoutPanel3.Dock = DockStyle.Fill;
		tableLayoutPanel3.ColumnCount = 2;
		tableLayoutPanel3.Margin = Padding.Empty;
		TableLayoutPanel tableLayoutPanel4 = tableLayoutPanel3;
		tableLayoutPanel4.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
		tableLayoutPanel4.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
		connectionWaiting = new Label
		{
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft,
			Font = ReferenceTheme.Small
		};
		connectionForecast = new Label
		{
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft,
			Font = ReferenceTheme.Small
		};
		tableLayoutPanel4.Controls.Add(connectionWaiting, 0, 0);
		tableLayoutPanel4.Controls.Add(connectionForecast, 1, 0);
		tableLayoutPanel2.Controls.Add(tableLayoutPanel4, 0, 1);
		tableLayoutPanel2.Controls.Add(Band("SKOMUNIKOWANIA", 22), 0, 2);
		TableLayoutPanel control = PairGrids("Pociągi ZDĄŻAJĄCE na skomunikowanie", "Pociągi OCZEKUJĄCE na skomunikowanie", "connections-in", "connections-out", stock: false);
		tableLayoutPanel2.Controls.Add(control, 0, 3);
		tableLayoutPanel2.Controls.Add(Band("Plan przejścia składu pociągu / grup wagonów", 22), 0, 4);
		tableLayoutPanel2.Controls.Add(PairGrids("Przejście składu Z pociągu", "Przejście składu NA pociąg", "stock-in", "stock-out", stock: true), 0, 5);
		panel.Controls.Add(tableLayoutPanel2);
	}

	private TableLayoutPanel PairGrids(string left, string right, string kindLeft, string kindRight, bool stock)
	{
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
		tableLayoutPanel.Dock = DockStyle.Fill;
		tableLayoutPanel.ColumnCount = 2;
		tableLayoutPanel.RowCount = 1;
		tableLayoutPanel.Margin = Padding.Empty;
		TableLayoutPanel tableLayoutPanel2 = tableLayoutPanel;
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
		for (int i = 0; i < 2; i++)
		{
			Panel panel = new Panel();
			panel.Dock = DockStyle.Fill;
			panel.Margin = new Padding(1);
			Panel panel2 = panel;
			panel2.Controls.Add(DetailGrid((i == 0) ? kindLeft : kindRight, new string[6]
			{
				"Nr pociągu",
				stock ? "Nr wagonów" : "Przyjazd / czas",
				"Opóźnienie",
				"Rodz. poc.",
				"Przewoźnik",
				"Relacja"
			}, new int[6] { 65, 85, 50, 50, 110, 160 }));
			Label label = Band((i == 0) ? left : right, 20);
			label.BackColor = Color.FromArgb(255, 241, 201);
			panel2.Controls.Add(label);
			tableLayoutPanel2.Controls.Add(panel2, i, 0);
		}
		return tableLayoutPanel2;
	}

	private void RefreshDetails()
	{
		Train selectedTrain = SelectedTrain;
		foreach (TextBox value in profileBoxes.Values)
		{
			value.Text = "";
		}
		foreach (CheckBox value2 in profileFlags.Values)
		{
			value2.Checked = false;
		}
		foreach (DataGridView value3 in detailGrids.Values)
		{
			value3.Rows.Clear();
		}
		if (selectedTrain == null)
		{
			if (filterInfo != null)
			{
				filterInfo.Text = "";
			}
			return;
		}
		TrainProfile trainProfile = selectedTrain.Profile ?? new TrainProfile();
		Action<string, string> action = (string key, string value) =>
		{
			profileBoxes[key].Text = value ?? "";
		};
		action("Number", selectedTrain.Number);
		action("InternationalNumber", trainProfile.InternationalNumber);
		action("Name", trainProfile.Name);
		action("OrderNumber", trainProfile.OrderNumber);
		action("Kind", selectedTrain.Kind);
		action("Carrier", selectedTrain.Carrier);
		action("Type", trainProfile.Type);
		action("Origin", selectedTrain.Origin);
		action("Destination", selectedTrain.Destination);
		action("TimetableType", trainProfile.TimetableType);
		action("ApplicationNumber", trainProfile.ApplicationNumber);
		action("ApplicationDate", trainProfile.ApplicationDate);
		action("Applicant", trainProfile.Applicant);
		action("Withdrawal", trainProfile.Withdrawal);
		action("WithdrawalDate", trainProfile.WithdrawalDate);
		action("Additional", trainProfile.Additional);
		action("Cargo", selectedTrain.Cargo);
		action("CarrierNotes", trainProfile.CarrierNotes);
		action("OperationalNotes", trainProfile.OperationalNotes);
		action("Notes", selectedTrain.Notes);
		profileFlags["Przekroczona skrajnia"].Checked = trainProfile.GaugeExceeded;
		profileFlags["Przekroczony nacisk"].Checked = trainProfile.LoadExceeded;
		profileFlags["Inne nadzwyczajne"].Checked = selectedTrain.Extraordinary;
		profileFlags["TWR"].Checked = selectedTrain.HighRisk;
		profileFlags["TN"].Checked = selectedTrain.Dangerous;
		profileFlags["Zamknięciowy"].Checked = trainProfile.Closure;
		profileFlags["Śledzony"].Checked = trainProfile.Tracked;
		if (filterInfo != null)
		{
			filterInfo.Text = "Z:   " + selectedTrain.Origin + "\nDo: " + selectedTrain.Destination + "\nP:   " + ((selectedTrain.SourceProvider == "TD2") ? Td2Identity.CarrierCaption(selectedTrain.Carrier) : CarrierNames.Expanded(selectedTrain.Carrier)) + "\nN:   " + (trainProfile.Name ?? "");
		}
		ownNotes.Text = selectedTrain.Notes ?? "";
		operationsNotes.Text = trainProfile.OperationalNotes ?? "";
		carrierNotes.Text = trainProfile.CarrierNotes ?? "";
		foreach (KeyValuePair<string, DataGridView> detailGrid in detailGrids)
		{
			if (detailGrid.Key == "route")
			{
				continue;
			}
			foreach (DetailRow record in Records(selectedTrain, detailGrid.Key))
			{
				DataGridViewRowCollection rows = detailGrid.Value.Rows;
				IEnumerable<int> source = Enumerable.Range(0, detailGrid.Value.Columns.Count);
				Func<int, object> selector = (int x) => Cell(record, x);
				int index = rows.Add(source.Select(selector).ToArray());
				detailGrid.Value.Rows[index].Tag = record;
			}
		}
		if (!Records(selectedTrain, "delays").Any() && !string.IsNullOrEmpty(selectedTrain.DelayReason))
		{
			detailGrids["delays"].Rows.Add("", "", Delay(selectedTrain.Departure, selectedTrain.PlanDeparture), "", selectedTrain.DelayReason, "", Engine.Station.Name);
		}
		if (topTabs.SelectedIndex == 2)
		{
			FillRoute(selectedTrain);
		}
		extraRelation.Text = "Stacja nadania: " + selectedTrain.Origin + "      Stacja przeznaczenia: " + selectedTrain.Destination;
		connectionSummary.Text = "Skomunikowania pociągu " + selectedTrain.Number + "     " + selectedTrain.Kind + "     " + selectedTrain.Origin + " - " + selectedTrain.Destination + "     " + selectedTrain.Carrier;
		connectionWaiting.Text = "Max. czas oczekiwania bez uzgodnienia z IDDE: " + (trainProfile.MaxWaiting ?? "");
		connectionForecast.Text = "Prognoza opóźnienia poc. skomunikowanego: " + (trainProfile.ConnectionForecast ?? "");
	}

	private void EditProfile()
	{
		Engine.RequireWrite();
		Train t = NeedTrain();
		using Editor editor = new Editor("Lokalny opis pociągu " + t.Number, 760, 720);
		TrainProfile p = Json.Clone(t.Profile ?? new TrainProfile());
		Dictionary<string, TextBox> inputs = new Dictionary<string, TextBox>();
		foreach (PropertyInfo item in from x in typeof(TrainProfile).GetProperties()
			where x.PropertyType == typeof(string)
			select x)
		{
			string label;
			if (profileTitles.ContainsKey(item.Name))
			{
				label = profileTitles[item.Name];
			}
			else
			{
				label = ((item.Name == "MaxWaiting") ? "Czas oczekiwania (np. 15 min)" : "Prognoza skomunikowania");
			}
			inputs[item.Name] = editor.Add(label, Ui.Text((string)item.GetValue(p, null), multiline: false), 31);
		}
		editor.Save(() =>
		{
			foreach (KeyValuePair<string, TextBox> item2 in inputs)
			{
				typeof(TrainProfile).GetProperty(item2.Key).SetValue(p, item2.Value.Text, null);
			}
			Engine.Change("Opis pociągu", t.Id, (AppState s) =>
			{
				s.Trains.First((Train x) => x.Id == t.Id).Profile = p;
			});
		});
		if (editor.ShowDialog(this) == DialogResult.OK)
		{
			RefreshDetails();
		}
	}

	private void EditDetailRow(string kind, DetailRow source)
	{
		Engine.RequireWrite();
		Train t = NeedTrain();
		DataGridView dataGridView = detailGrids[kind];
		using Editor editor = new Editor("Dane lokalne - " + topTabs.SelectedTab.Text, 730, Math.Min(740, 125 + dataGridView.Columns.Count * 37));
		DetailRow row = ((source == null) ? new DetailRow
		{
			Id = Guid.NewGuid().ToString("N"),
			Kind = kind
		} : Json.Clone(source));
		List<TextBox> inputs = new List<TextBox>();
		for (int i = 0; i < dataGridView.Columns.Count; i++)
		{
			inputs.Add(editor.Add(dataGridView.Columns[i].HeaderText, Ui.Text(Cell(row, i), multiline: false), 36));
		}
		TextBox attachment = null;
		if (kind == "orders")
		{
			attachment = editor.Add("Plik telegramu PDF (ścieżka)", Ui.Text(row.Attachment, multiline: false), 36);
		}
		editor.Save(() =>
		{
			row.Cells = inputs.Select((TextBox x) => x.Text).ToArray();
			if (row.Cells.All(string.IsNullOrWhiteSpace))
			{
				throw new Exception("Wprowadź dane w co najmniej jednej kolumnie.");
			}
			if (attachment != null)
			{
				row.Attachment = attachment.Text.Trim();
				if (row.Attachment.Length > 0 && (!File.Exists(row.Attachment) || !row.Attachment.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)))
				{
					throw new Exception("Wskaż istniejący plik PDF telegramu.");
				}
			}
			Engine.Change("Dane " + kind, t.Id, (AppState s) =>
			{
				Train train = s.Trains.First((Train x) => x.Id == t.Id);
				if (train.DetailRows == null)
				{
					train.DetailRows = new List<DetailRow>();
				}
				train.DetailRows.RemoveAll((DetailRow x) => x.Id == row.Id);
				train.DetailRows.Add(row);
			});
		});
		if (editor.ShowDialog(this) == DialogResult.OK)
		{
			RefreshDetails();
		}
	}

	private void OpenOrder(DetailRow row)
	{
		if (row == null)
		{
			return;
		}
		ReferenceForm referenceForm = new ReferenceForm();
		referenceForm.Text = "Telegramy z zarządzeniem pociągu " + NeedTrain().Number;
		referenceForm.ClientSize = new Size(1000, 655);
		referenceForm.StartPosition = FormStartPosition.CenterParent;
		using ReferenceForm referenceForm2 = referenceForm;
		TabControl tabControl = ReferenceTheme.Tabs();
		tabControl.Font = ReferenceTheme.Dialog;
		TabPage tabPage = new TabPage("Telegramy");
		DataGridView dataGridView = Ui.Grid(journal: false);
		Ui.Column(dataGridView, "n", "Numer zarządzenia", 270);
		Ui.Column(dataGridView, "s", "Nadawca", 240);
		Ui.Column(dataGridView, "d", "Data nadania", 170);
		dataGridView.Rows.Add(Cell(row, 1), Cell(row, 2), Cell(row, 3));
		tabPage.Controls.Add(dataGridView);
		tabControl.TabPages.Add(tabPage);
		TabPage tabPage2 = new TabPage("Podgląd telegramu");
		Label label = new Label();
		label.Dock = DockStyle.Fill;
		label.TextAlign = ContentAlignment.MiddleCenter;
		label.Text = (string.IsNullOrEmpty(row.Attachment) ? "Do tego zarządzenia nie dołączono pliku telegramu PDF." : ("Telegram: " + Path.GetFileName(row.Attachment)));
		Label value = label;
		tabPage2.Controls.Add(value);
		if (!string.IsNullOrEmpty(row.Attachment))
		{
			Button button = Ui.Button("Otwórz PDF telegramu", () =>
			{
				Process.Start(row.Attachment);
			});
			button.Dock = DockStyle.Bottom;
			tabPage2.Controls.Add(button);
		}
		tabControl.TabPages.Add(tabPage2);
		referenceForm2.Controls.Add(tabControl);
		referenceForm2.ShowDialog(this);
	}

	private void BuildTd2DirectionFilter(FlowLayoutPanel row)
	{
		if (!Engine.IsTd2)
		{
			return;
		}
		td2RouteLines = new CheckBox
		{
			Text = "Pokaż szlaki",
			Checked = (Engine.Station.Td2ShowRouteLines != false),
			AutoSize = true,
			Font = ReferenceTheme.Small
		};
		row.Controls.Add(TimetableFilterPanel.Group(td2RouteLines));
		td2RouteLines.CheckedChanged += (object s, EventArgs e) =>
		{
			if (!td2RouteBinding)
			{
				bool show = td2RouteLines.Checked;
				Engine.Store.Commit((AppState state) =>
				{
					state.Stations.First((Station x) => x.Id == Engine.StationId).Td2ShowRouteLines = show;
				});
				Engine.PersistStationProfile();
				RefreshTrains();
			}
		};
	}

	private void SyncTd2DirectionFilter()
	{
		if (td2RouteLines == null)
		{
			return;
		}
		td2RouteBinding = true;
		try
		{
			td2RouteLines.Checked = Engine.Station.Td2ShowRouteLines != false;
		}
		finally
		{
			td2RouteBinding = false;
		}
	}

	public void ResetTrainingSession()
	{
		if (Engine.SimRail != null || !Engine.Store.IsTransient)
		{
			throw new Exception("To okno korzysta z trwałej bazy danych.");
		}
		bool canWrite = Engine.CanWrite;
		string user = Engine.User.Id;
		foreach (JournalPane pane in panes)
		{
			pane.DiscardPending();
		}
		Engine.Store.ResetSession();
		Engine.User = Engine.State.Users.FirstOrDefault((LocalUser x) => x.Id == user) ?? Engine.State.Users.First((LocalUser x) => x.Id == "DEMO");
		Engine.StationId = Engine.State.Stations[0].Id;
		if (canWrite)
		{
			Engine.BeginDuty(DateTime.Now);
		}
		selectedTrainId = null;
		filling = true;
		stations.Items.Clear();
		stations.Items.AddRange(Engine.State.Stations.Select((Station x) => StationDisplay(x)).Cast<object>().ToArray());
		stations.SelectedIndex = 0;
		filling = false;
		confirmationFilter = 0;
		movementFilter = 0;
		confirmationLink.Text = "Potwierdzenie: wszystkie";
		movementLink.Text = "Pociągi: wszystkie";
		search.Clear();
		timetableDay.Value = DateTime.Now;
		BuildJournals();
		RefreshData();
		ApplyZoom();
		filterPanel.Reflow();
	}

	private Label TrainHeading(Control body, Train t, int width)
	{
		Label label = AtLabel(body, "Pociąg " + t.Number + " / Relacja: " + t.Origin + " - " + t.Destination, 12, 10, width - 24, 40, bold: true);
		label.TextAlign = ContentAlignment.MiddleCenter;
		label.ForeColor = Color.Green;
		label.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
		return label;
	}

	private void TrainTimes()
	{
		Engine.RequireWrite();
		Train t = NeedTrain();
		using Editor editor = new Editor("Rzeczywisty czas przyjazdu i odjazdu pociągu", 710, 695);
		Panel panel = editor.AbsoluteBody();
		TrainHeading(panel, t, 700);
		OperationTimePanel arrival = new OperationTimePanel("Przyjazd:", TrainPrediction.Arrival(t))
		{
			Bounds = new Rectangle(20, 52, 664, 265)
		};
		OperationTimePanel departure = new OperationTimePanel("Odjazd:", TrainPrediction.Departure(t))
		{
			Bounds = new Rectangle(20, 327, 664, 265)
		};
		panel.Controls.Add(arrival);
		panel.Controls.Add(departure);
		SilverButton silverButton = new SilverButton();
		silverButton.Text = "Przepisz czas";
		silverButton.Bounds = new Rectangle(532, 213, 116, 36);
		silverButton.Font = ReferenceTheme.Dialog;
		SilverButton silverButton2 = silverButton;
		arrival.Controls.Add(silverButton2);
		silverButton2.Click += (object s, EventArgs e) =>
		{
			departure.Value = arrival.Value;
		};
		DateTime initialArrival = arrival.Value;
		DateTime initialDeparture = departure.Value;
		editor.Save(() =>
		{
			Engine.SetTrainTimes(t.Id, arrival.Value, departure.Value, arrival.Value != initialArrival && departure.Value == initialDeparture);
		});
		DialogButtons(editor);
		Button button = Ui.Button(t.Stopped ? "Odwołaj postój (pociąg STÓJ)" : "Wprowadź postój (pociąg STÓJ)", () =>
		{
			TrainStop();
		});
		if (Engine.IsTd2)
		{
			button.Enabled = false;
		}
		button.Size = new Size(300, 34);
		button.AutoSize = false;
		editor.Buttons.Controls.Add(button);
		if (editor.ShowDialog(this) == DialogResult.OK)
		{
			RefreshData();
		}
	}

	private void TrainReady()
	{
		Engine.RequireWrite();
		Train t = NeedTrain();
		Editor d = new Editor("Wprowadzanie gotowości pociągu do odjazdu", 515, 515);
		try
		{
			Panel panel = d.AbsoluteBody();
			TrainHeading(panel, t, 505);
			Label label = AtLabel(panel, "Gotowość pociągu do odjazdu zgłoszono:", 22, 77, 470, 30, bold: true);
			label.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
			label.ForeColor = Color.Navy;
			OperationTimePicker time = new OperationTimePicker(t.ReadyReported ?? DateTime.Now)
			{
				Bounds = new Rectangle(22, 112, 380, 39)
			};
			panel.Controls.Add(time);
			Label label2 = AtLabel(panel, "Nazwisko maszynisty:", 22, 195, 460, 30, bold: true);
			label2.Font = label.Font;
			label2.ForeColor = Color.Navy;
			TextBox driver = Ui.Text(t.ReadyDriver, multiline: false);
			driver.Font = new Font("Segoe UI", 13f);
			Place(panel, driver, 22, 227, 462, 39);
			Button button = Ui.Button("Wprowadź analizę pociągu", () =>
			{
				TrainAnalysis();
			});
			button.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
			Place(panel, button, 22, 304, 462, 50);
			Label state = AtLabel(panel, "", 22, 373, 470, 30, bold: false);
			state.ForeColor = Color.Green;
			state.Font = new Font("Segoe UI", 11f);
			Action refresh = () =>
			{
				state.Text = (Engine.State.Trains.First((Train x) => x.Id == t.Id).Dangerous ? "Pociąg przewozi towary niebezpieczne." : "Pociąg aktualnie nie przewozi towarów niebezp.");
			};
			refresh();
			TnButton tn = new TnButton
			{
				Text = "TN",
				Alert = t.Dangerous,
				Font = new Font("Segoe UI", 12f, FontStyle.Bold)
			};
			tn.Click += (object s, EventArgs e) =>
			{
				Ui.Run(d, () =>
				{
					bool flag = !Engine.State.Trains.First((Train x) => x.Id == t.Id).Dangerous;
					if (MessageBox.Show(d, flag ? "Wprowadzić oznaczenie przewozu towarów niebezpiecznych?" : "Usunąć oznaczenie przewozu towarów niebezpiecznych?", "Towary niebezpieczne", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
					{
						Engine.SetDangerous(t.Id, flag);
						refresh();
						tn.Alert = flag;
						tn.Invalidate();
						RefreshDetails();
					}
				});
			};
			tn.BackColor = Color.Lime;
			tn.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
			d.Save(() =>
			{
				string name = Ui.Required(driver, "Nazwisko maszynisty");
				Engine.Change("Gotowość pociągu", t.Id, (AppState s) =>
				{
					Train train = s.Trains.First((Train y) => y.Id == t.Id);
					train.Ready = true;
					train.ReadyReported = time.Value;
					train.ReadyRecordedAt = DateTime.Now;
					train.ReadyDriver = name;
					s.Entries.Add(Engine.Note("TEL", time.Value, "Pociąg " + train.Number + ": zgłoszono gotowość. Maszynista: " + name, "Gotowość"));
				});
			});
			DialogButtons(d);
			tn.AutoSize = false;
			tn.Size = new Size(145, 34);
			d.Buttons.Controls.Add(tn);
			if (d.ShowDialog(this) == DialogResult.OK)
			{
				RefreshData();
			}
		}
		finally
		{
			if (d != null)
			{
				((IDisposable)d).Dispose();
			}
		}
	}

	private void TrainAnalysis()
	{
		Engine.RequireWrite();
		Train t = NeedTrain();
		TrainConsist consist = Json.Clone(t.Consist ?? new TrainConsist());
		using Editor editor = new Editor("Wprowadzanie Analizy Pociągu", 600, 650);
		Panel panel = editor.AbsoluteBody();
		TrainHeading(panel, t, 590);
		Label label = AtLabel(panel, "Wprowadzanie danych o pociągu:", 22, 49, 540, 25, bold: true);
		label.ForeColor = Color.Navy;
		GroupBox groupBox = new GroupBox();
		groupBox.Text = "Rzeczywiste brutto składu i długość pociągu";
		groupBox.Bounds = new Rectangle(62, 79, 470, 127);
		groupBox.Font = ReferenceTheme.Dialog;
		GroupBox groupBox2 = groupBox;
		panel.Controls.Add(groupBox2);
		AtLabel(groupBox2, "Wartości planowe:", 312, 16, 150, 24, bold: false);
		TextBox gross = AnalysisNumber(groupBox2, "Brutto składu:", consist.Gross, 43, consist.PlannedGross);
		TextBox length = AnalysisNumber(groupBox2, "Długość pociągu:", consist.Length, 85, consist.PlannedLength);
		GroupBox groupBox3 = new GroupBox();
		groupBox3.Text = "Dane lokomotyw";
		groupBox3.Bounds = new Rectangle(22, 234, 552, 331);
		groupBox3.Font = ReferenceTheme.Dialog;
		GroupBox value = groupBox3;
		panel.Controls.Add(value);
		ComboBox[] series = new ComboBox[3];
		TextBox[] numbers = new TextBox[3];
		AtLabel(value, "Seria", 166, 17, 220, 24, bold: true).ForeColor = Color.Purple;
		AtLabel(value, "Numer", 414, 17, 120, 24, bold: true).ForeColor = Color.Purple;
		for (int i = 0; i < 3; i++)
		{
			int num = 46 + i * 97;
			AtLabel(value, "Lokomotywa " + (i + 1) + ":", 20, num + 1, 137, 27, bold: false).ForeColor = Color.Maroon;
			ref ComboBox reference = ref series[i];
			string[] choices = VehicleCatalog.Choices;
			string selected;
			if (consist.VehicleTypes != null && i < consist.VehicleTypes.Length)
			{
				selected = consist.VehicleTypes[i];
			}
			else
			{
				selected = ((consist.Series != null && i < consist.Series.Length) ? consist.Series[i] : "**BRAK**");
			}
			reference = Ui.Combo(choices, selected, editable: true);
			Place(value, series[i], 160, num, 222, 29);
			numbers[i] = Ui.Text((consist.Numbers != null && i < consist.Numbers.Length) ? consist.Numbers[i] : "", multiline: false);
			Place(value, numbers[i], 402, num, 122, 29);
			series[i].AutoCompleteMode = AutoCompleteMode.SuggestAppend;
			series[i].AutoCompleteSource = AutoCompleteSource.ListItems;
			series[i].DropDownWidth = 300;
			Label parameters = AtLabel(value, "", 160, num + 33, 365, 52, bold: false);
			parameters.AutoSize = false;
			ComboBox box = series[i];
			Action details = () =>
			{
				parameters.Text = VehicleCatalog.Description(box.Text);
			};
			box.TextChanged += (object sender, EventArgs args) =>
			{
				details();
			};
			details();
		}
		editor.Save(() =>
		{
			consist.ManualAnalysis = true;
			consist.Gross = ParsePositive(gross.Text, "Brutto składu");
			consist.Length = ParsePositive(length.Text, "Długość pociągu");
			consist.VehicleTypes = series.Select((ComboBox x) => (!(x.Text == "**BRAK**")) ? x.Text.Trim() : "").ToArray();
			consist.Series = consist.VehicleTypes.Select((string x) =>
			{
				VehicleSpec vehicleSpec = VehicleCatalog.Find(x);
				return (vehicleSpec != null && !string.IsNullOrWhiteSpace(vehicleSpec.SepeSeries)) ? vehicleSpec.SepeSeries : x;
			}).ToArray();
			consist.Numbers = numbers.Select((TextBox x) => x.Text).ToArray();
			Engine.Change("Analiza pociągu", t.Id, (AppState s) =>
			{
				Train train = s.Trains.First((Train y) => y.Id == t.Id);
				train.Consist = consist;
				train.Analysis = "Brutto: " + consist.Gross + "; długość: " + consist.Length + "; lokomotywy: " + string.Join(", ", consist.Series.Zip(consist.Numbers, (string a, string b) => a + " " + b));
				s.Entries.Add(Engine.Note("TEL", Engine.Now, "Analiza pociągu " + t.Number + ": " + train.Analysis, "Analiza"));
			});
		});
		DialogButtons(editor);
		if (editor.ShowDialog(this) == DialogResult.OK)
		{
			RefreshData();
		}
	}

	private TextBox AnalysisNumber(Control group, string title, decimal? value, int y, decimal? planned)
	{
		Label label = AtLabel(group, title, 14, y, 165, 30, bold: true);
		label.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
		label.ForeColor = Color.Maroon;
		TextBox textBox = Ui.Text(value.HasValue ? value.Value.ToString() : "", multiline: false);
		textBox.TextAlign = HorizontalAlignment.Right;
		textBox.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
		Place(group, textBox, 181, y, 121, 31);
		Label label2 = AtLabel(group, planned.HasValue ? planned.Value.ToString() : "", 315, y, 125, 31, bold: true);
		label2.ForeColor = Color.Navy;
		label2.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
		return textBox;
	}

	private static decimal? ParsePositive(string value, string field)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}
		if (!decimal.TryParse(value, out var result) || result <= 0m)
		{
			throw new Exception("Pole „" + field + "” wymaga liczby większej od zera albo pozostawienia pustego pola.");
		}
		return result;
	}

	public void NotifyCrossings(JournalEntry row)
	{
		Engine.RequireWrite();
		if (!FlushJournals())
		{
			return;
		}
		if (row != null)
		{
			row = Engine.State.Entries.FirstOrDefault((JournalEntry x) => x.Id == row.Id);
		}
		if (row == null || row.Type != "Pociąg" || row.Cancelled)
		{
			throw new Exception("Zaznacz aktywny wiersz pociągu w dzienniku ruchu.");
		}
		if (!Engine.CanEditJournal(row.SectionId))
		{
			throw new Exception("Dziennik jest dostępny tylko do odczytu.");
		}
		if (row.Direction != "DO" || !string.IsNullOrEmpty(row.ReturnOfId))
		{
			throw new Exception("Powiadomienie dróżników wprowadza się w wierszu wyprawienia pociągu.");
		}
		Section section = Engine.Station.Sections.First((Section x) => x.Id == row.SectionId);
		if (section.Crossings != null && section.Crossings.Count > 0)
		{
			ConfiguredCrossings(row, section);
			return;
		}
		using Editor editor = new Editor("Powiadomienie dróżników przejazdowych - " + row.Number, 530, 255);
		JournalEntry en = Json.Clone(row);
		editor.Info("O jeździe pociągu zawiadomiono", 40);
		Section section2 = Engine.Station.Sections.First((Section x) => x.Id == row.SectionId);
		CheckBox mark = new CheckBox
		{
			Checked = (en.Notification == "/"),
			Text = "Powiadomiono"
		};
		ClockTextBox time = new ClockTextBox
		{
			Text = ((en.Notification == "/") ? "" : en.Notification),
			Clock = () => Engine.Now
		};
		time.ValueProvider = () => ClockDates.Resolve(time.Text, en.Time, null, null, Engine.Now);
		if (section2.Tracks == 1)
		{
			editor.Add("Powiadomienie", mark, 36);
		}
		else
		{
			editor.Add("Godzina powiadomienia", time, 36);
		}
		editor.Save(() =>
		{
			if (section2.Tracks == 1)
			{
				en.Notification = (mark.Checked ? "/" : "");
			}
			else
			{
				en.Notification = Ui.Time(time.ChosenTime ?? ClockDates.Resolve(time.ValueText, en.Time, null, null, Engine.Now));
			}
			Engine.SaveJournal(en);
		});
		if (editor.ShowDialog(this) == DialogResult.OK)
		{
			RefreshData();
		}
	}

	private void QueueSelectionDetails()
	{
		if (selectionDetailsQueued || !IsHandleCreated || IsDisposed || Disposing)
		{
			return;
		}
		selectionDetailsQueued = true;
		BeginInvoke((Action)(() =>
		{
			selectionDetailsQueued = false;
			if (!IsDisposed && !Disposing && !filling)
			{
				RefreshSelectionDetails();
			}
		}));
	}

	private void RefreshSelectionDetails()
	{
		if (topTabs.SelectedIndex != 0)
		{
			RefreshDetails();
			return;
		}
		Train selectedTrain = SelectedTrain;
		TrainProfile trainProfile = selectedTrain?.Profile;
		string text = ((selectedTrain == null) ? "" : ("Z:   " + selectedTrain.Origin + "\nDo: " + selectedTrain.Destination + "\nP:   " + ((selectedTrain.SourceProvider == "TD2") ? Td2Identity.CarrierCaption(selectedTrain.Carrier) : CarrierNames.Expanded(selectedTrain.Carrier)) + "\nN:   " + ((trainProfile == null) ? "" : trainProfile.Name)));
		if (filterInfo != null && filterInfo.Text != text)
		{
			filterInfo.Text = text;
		}
		string text2 = ((selectedTrain == null) ? "" : (selectedTrain.Notes ?? ""));
		string text3 = ((trainProfile == null) ? "" : (trainProfile.OperationalNotes ?? ""));
		string text4 = ((trainProfile == null) ? "" : (trainProfile.CarrierNotes ?? ""));
		if (ownNotes.Text != text2)
		{
			ownNotes.Text = text2;
		}
		if (operationsNotes.Text != text3)
		{
			operationsNotes.Text = text3;
		}
		if (carrierNotes.Text != text4)
		{
			carrierNotes.Text = text4;
		}
	}
}

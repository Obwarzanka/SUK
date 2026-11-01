using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace EdrLocal;

public static class Fixes19Tests
{
	private static readonly List<string> log = new List<string>();

	[DllImport("user32.dll")]
	private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr w, IntPtr l);

	private static void Check(bool value, string label)
	{
		if (!value)
		{
			throw new Exception("FAIL: " + label);
		}
		log.Add("PASS: " + label);
	}

	private static T Field<T>(object owner, string name)
	{
		return (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
	}

	private static void Call(object owner, string name, params object[] args)
	{
		owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);
	}

	private static IEnumerable<Control> All(Control c)
	{
		yield return c;
		foreach (Control child in c.Controls)
		{
			foreach (Control item in All(child))
			{
				yield return item;
			}
		}
	}

	private static SimRailPoint Point(string id, string name, int line, double km)
	{
		SimRailPoint simRailPoint = new SimRailPoint();
		simRailPoint.pointId = id;
		simRailPoint.nameForPerson = name;
		simRailPoint.line = line;
		simRailPoint.mileage = km;
		simRailPoint.indexOfPoint = 0;
		simRailPoint.displayedTrainNumber = "414042";
		simRailPoint.arrivalTime = "2026-04-21 19:24:00";
		simRailPoint.departureTime = "2026-04-21 19:24:00";
		simRailPoint.actualArrivalTime = "2026-04-21 19:24:00";
		simRailPoint.actualDepartureTime = "2026-04-21 19:26:00";
		simRailPoint.isConfirmed = true;
		simRailPoint.confirmedBy = 3;
		simRailPoint.isActive = true;
		simRailPoint.track = 1;
		return simRailPoint;
	}

	private static SimRailTrain Train(string no, params SimRailPoint[] route)
	{
		for (int i = 0; i < route.Length; i++)
		{
			route[i].indexOfPoint = i;
		}
		SimRailTrain simRailTrain = new SimRailTrain();
		simRailTrain.trainNoLocal = no;
		simRailTrain.trainName = "";
		simRailTrain.startStation = route.First().nameForPerson;
		simRailTrain.endStation = route.Last().nameForPerson;
		simRailTrain.timetable = route.ToList();
		return simRailTrain;
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		try
		{
			SimRailTrain item = Train("100", Point("59", "Rokiciny", 1, 114.176), Point("60", "Baby", 1, 129.944), Point("61", "Piotrków Trybunalski", 1, 144.242));
			SimRailTrain simRailTrain = Train("414042", Point("90", "Zawiercie", 4, 1.9), Point("60", "Baby", 1, 129.944), Point("59", "Rokiciny", 1, 114.176));
			SimRailTrain item2 = Train("3730", Point("59", "Rokiciny", 1, 114.176), Point("60", "Baby", 1, 129.944), Point("90", "Zawiercie", 4, 1.9));
			SimRailSnapshot simRailSnapshot = new SimRailSnapshot();
			simRailSnapshot.ServerCode = "int2";
			simRailSnapshot.GameTime = new DateTime(2026, 4, 21, 19, 40, 0);
			simRailSnapshot.Trains = new List<SimRailTrain> { item, simRailTrain, item2 };
			simRailSnapshot.DispatcherNames = new HashSet<string> { "Rokiciny", "Baby", "Piotrków Trybunalski", "Zawiercie" };
			SimRailSnapshot simRailSnapshot2 = simRailSnapshot;
			SimRailSession simRailSession = new SimRailSession(new SimRailProvider(), simRailSnapshot2, "60");
			Station station = simRailSession.SuggestStation();
			StationSettings.Normalize(station);
			Check(station.Sections.Count == 2 && station.Sections.All((Section x) => x.Line == "1"), "Baby mają dwa szlaki na linii 1 mimo odległej linii 4 ekspresu.");
			Check(!station.Sections.Any((Section x) => x.Name == "Zawiercie"), "Sugestia konfiguracji nie dodaje szlaku Baby - Zawiercie.");
			List<Train> projected = simRailSession.Project(station);
			Train fast = projected.First((Train x) => x.ExternalTrainNo == "414042");
			Train train = projected.First((Train x) => x.ExternalTrainNo == "3730");
			Check(fast.From == "Piotrków Trybunalski" && fast.To == "Rokiciny", "Ekspres pomijający Piotrków otrzymuje właściwy szlak przyjęcia.");
			Check(train.From == "Rokiciny" && train.To == "Piotrków Trybunalski" && train.LineOut == "1", "Przeciwny kierunek nie przejmuje odległej linii 4.");
			Store store = new Store(Path.Combine(folder, "sim-data"));
			store.Load();
			store.Commit((AppState s) =>
			{
				s.Stations = new List<Station> { station };
				s.Trains = projected;
				s.Entries.Clear();
				s.Duties.Clear();
			});
			Engine engine = new Engine(store);
			engine.SimRail = simRailSession;
			engine.User = store.State.Users.First((LocalUser x) => x.Id == "DEMO");
			Engine engine2 = engine;
			engine2.BeginDuty(engine2.Now.AddMinutes(-2.0));
			JournalEntry journalEntry = engine2.RegisterScheduledTrain(fast.Id, null);
			Check(journalEntry.SectionId == station.Sections.First((Section x) => x.Name == "Piotrków Trybunalski").Id, "Dwuklik pociągu nie wywołuje błędu niejednoznacznego dziennika.");
			List<SepeWriteRequest> pending = new List<SepeWriteRequest>();
			engine2.SimRailTimeChanged += (SepeWriteRequest r) =>
			{
				pending.Add(r);
			};
			journalEntry.Arrived = new DateTime(2026, 4, 21, 19, 25, 0);
			engine2.SaveJournal(journalEntry);
			Check(pending.Count == 1 && pending[0].ArrivalChanged && !pending[0].DepartureChanged, "Zapis przyjazdu emituje jedną operację SEPE tylko dla przyjazdu.");
			journalEntry.Text = "Zmiana uwag";
			engine2.SaveJournal(journalEntry);
			Check(pending.Count == 1, "Edycja samych uwag nie wysyła ponownie godziny.");
			Check(!pending[0].Confirmed(simRailSnapshot2, "60"), "Stare godziny API nie potwierdzają nowego zapisu.");
			simRailTrain.timetable[1].actualArrivalTime = "2026-04-21 19:25:12";
			Check(pending[0].Confirmed(simRailSnapshot2, "60"), "Potwierdzenie API porównuje wpisane minuty mimo sekund serwera.");
			engine2.ApplySimRailSnapshot(simRailSnapshot2);
			Check(!engine2.State.Trains.First((Train x) => x.Id == fast.Id).ExternalManualArrival, "Potwierdzenie API kończy oczekiwanie lokalnej korekty.");
			using (MainForm mainForm = new MainForm(engine2))
			{
				mainForm.Show();
				Application.DoEvents();
				mainForm.SelectTrain(fast.Id);
				mainForm.RefreshData();
				DataGridView dataGridView = Field<DataGridView>(mainForm, "trains");
				DataGridViewRow dataGridViewRow = dataGridView.Rows.Cast<DataGridViewRow>().First((DataGridViewRow x) => (string)x.Tag == fast.Id);
				Check(dataGridViewRow.Cells["kind"].Style.BackColor == dataGridViewRow.Cells["kind"].Style.SelectionBackColor, "Zaznaczenie automatycznego potwierdzenia zachowuje jego kolor.");
				Check(dataGridViewRow.DefaultCellStyle.SelectionBackColor == Color.FromArgb(51, 153, 255), "Pociąg SimRail ma stabilne niebieskie zaznaczenie.");
				int rowIndex = dataGridView.CurrentCell.RowIndex;
				typeof(Control).GetMethod("OnMouseMove", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(dataGridView, new object[1]
				{
					new MouseEventArgs(MouseButtons.None, 0, 10, dataGridView.ColumnHeadersHeight + dataGridView.RowTemplate.Height * 2, 0)
				});
				Check(dataGridView.CurrentCell.RowIndex == rowIndex, "Samo najechanie nie wybiera innego pociągu.");
				Ui.Capture(mainForm, Path.Combine(folder, "wykaz.png"));
				engine2.EndDuty(engine2.Now);
				mainForm.Close();
			}
			Store store2 = new Store(Path.Combine(folder, "local"));
			store2.Load();
			Engine engine3 = new Engine(store2);
			engine3.User = store2.State.Users.First((LocalUser x) => x.Id == "DEMO");
			Engine engine4 = engine3;
			StationSettings.Normalize(engine4.Station);
			string id = engine4.Station.Sections[0].Id;
			string id2 = engine4.Station.Sections[1].Id;
			using (StationConfigurationForm stationConfigurationForm = new StationConfigurationForm(engine4))
			{
				stationConfigurationForm.Show();
				Application.DoEvents();
				ConfigurationGrid configurationGrid = Field<ConfigurationGrid>(stationConfigurationForm, "sectionGrid");
				Check(configurationGrid.Groups.Count == 3 && !configurationGrid.ReadOnly, "Konfiguracja ma trzy grupy nagłówków z aktywnym polem prowadzenia.");
				Check(All(stationConfigurationForm).OfType<Button>().Any((Button x) => x.Text == "Konfiguruj" && x.Visible), "Przycisk Konfiguruj jest widoczny nad tabelą.");
				DataGridViewCell dataGridViewCell = configurationGrid.Rows[0].Cells["Conducted"];
				Rectangle cellDisplayRectangle = configurationGrid.GetCellDisplayRectangle(dataGridViewCell.ColumnIndex, 0, cutOverflow: false);
				int value = (cellDisplayRectangle.Top + cellDisplayRectangle.Height / 2 << 16) | (cellDisplayRectangle.Left + cellDisplayRectangle.Width / 2);
				SendMessage(configurationGrid.Handle, 513, new IntPtr(1), new IntPtr(value));
				SendMessage(configurationGrid.Handle, 514, IntPtr.Zero, new IntPtr(value));
				Application.DoEvents();
				configurationGrid.EndEdit();
				Check(((Section)configurationGrid.Rows[0].DataBoundItem).Conducted == false, "Zmiana checkboxa prowadzenia trafia do modelu.");
				dataGridViewCell.Value = true;
				configurationGrid.EndEdit();
				Check((from x in All(stationConfigurationForm).OfType<Button>()
					where x.Visible && x.Text == "Usuń"
					select x).All((Button x) => x.Bottom <= x.Parent.ClientSize.Height), "Przyciski Usuń mieszczą się w nagłówkach tabel.");
				Timer timer = new Timer
				{
					Interval = 150
				};
				try
				{
					timer.Tick += (object a, EventArgs b) =>
					{
						Editor editor = Application.OpenForms.OfType<Editor>().FirstOrDefault();
						if (editor != null)
						{
							timer.Stop();
							Ui.Capture(editor, Path.Combine(folder, "edycja-szlaku.png"));
							TextBox textBox = All(editor).OfType<TextBox>().First((TextBox x) => string.IsNullOrEmpty(x.Text));
							textBox.Text = "Linia testowa";
							editor.Buttons.Controls.OfType<Button>().First((Button x) => x.Text == "Zapisz").PerformClick();
						}
					};
					timer.Start();
					Call(stationConfigurationForm, "EditSection", false);
				}
				finally
				{
					if (timer != null)
					{
						((IDisposable)timer).Dispose();
					}
				}
				Check(((Section)configurationGrid.Rows[0].DataBoundItem).LineName == "Linia testowa", "Okno Konfiguruj zapisuje zmienione dane szlaku.");
				TabControl tabControl = All(stationConfigurationForm).OfType<TabControl>().First();
				tabControl.SelectedIndex = 1;
				Application.DoEvents();
				ConfigurationGrid configurationGrid2 = Field<ConfigurationGrid>(stationConfigurationForm, "tabsGrid");
				Check(configurationGrid2.Rows.Count == 2, "Aktualny automatyczny układ jest widoczny do edycji.");
				Call(stationConfigurationForm, "SwapTab");
				Check(Convert.ToString(configurationGrid2.Rows[0].Cells["Left"].Value) == StationSettings.Title(engine4.Station.Sections[1]), "Zamień strony odwraca istniejącą parę.");
				Ui.Capture(stationConfigurationForm, Path.Combine(folder, "uklad.png"));
				tabControl.SelectedIndex = 0;
				Ui.Capture(stationConfigurationForm, Path.Combine(folder, "konfiguracja.png"));
				Call(stationConfigurationForm, "Save");
				Check(engine4.Station.JournalTabs[0].LeftId == id2 && engine4.Station.JournalTabs[0].RightId == id, "Zapis utrwala odwróconą parę dzienników.");
			}
			Store store3 = new Store(store2.DirectoryPath);
			store3.Load();
			Check(store3.State.Stations[0].JournalTabs[0].LeftId == id2, "Odwrócona para przetrwała ponowne wczytanie bazy.");
			using (MainForm mainForm2 = new MainForm(engine4))
			{
				mainForm2.Show();
				Application.DoEvents();
				TabControl tabControl2 = Field<TabControl>(mainForm2, "journalTabs");
				TableLayoutPanel tableLayoutPanel = tabControl2.TabPages[0].Controls.OfType<TableLayoutPanel>().Single();
				Check(((JournalPane)tableLayoutPanel.GetControlFromPosition(0, 0)).SectionId == id2 && ((JournalPane)tableLayoutPanel.GetControlFromPosition(1, 0)).SectionId == id, "Główny widok faktycznie wyświetla dzienniki w zmienionej kolejności.");
				mainForm2.Close();
			}
			File.WriteAllText(Path.Combine(folder, "prepare.js"), SepeTimeTransfer.Script(new DateTime(2026, 4, 21, 19, 25, 0), null, simRailSnapshot2.GameTime));
			File.WriteAllText(Path.Combine(folder, "submit.js"), SepeTimeTransfer.Submit("414042", new DateTime(2026, 4, 21, 19, 25, 0), null, simRailSnapshot2.GameTime));
			log.Add("ALL CONFIGURATION, TOPOLOGY AND WRITE QUEUE TESTS PASSED");
			return true;
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			return false;
		}
		finally
		{
			File.WriteAllLines(Path.Combine(folder, "wyniki-poprawki19.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

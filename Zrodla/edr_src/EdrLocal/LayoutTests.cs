using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace EdrLocal;

internal static class LayoutTests
{
	private static List<string> log = new List<string>();

	private static void Check(bool value, string text)
	{
		if (!value)
		{
			throw new Exception("FAIL: " + text);
		}
		log.Add("PASS: " + text);
	}

	private static object Field(object instance, string name)
	{
		return instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
	}

	public static bool Run(string target)
	{
		Ui.Testing = true;
		Directory.CreateDirectory(target);
		bool success = false;
		string folder = Path.Combine(target, "session");
		Store store = new Store(folder, transient: true);
		store.Load();
		Engine engine = new Engine(store)
		{
			User = store.State.Users.First((LocalUser x) => x.Id == "DEMO")
		};
		engine.BeginDuty(DateTime.Now.AddMinutes(-1.0));
		MainForm main = new MainForm(engine);
		try
		{
			main.Shown += (object s, EventArgs e) =>
			{
				Ui.AfterFrame(() =>
				{
					try
					{
						main.MaximumSize = Size.Empty;
						TabControl tabControl = (TabControl)Field(main, "topTabs");
						TimetableFilterPanel timetableFilterPanel = (TimetableFilterPanel)Field(main, "filterPanel");
						int[][] array = new int[6][]
						{
							new int[3] { 1000, 650, 0 },
							new int[3] { 1259, 708, 0 },
							new int[3] { 1366, 768, 0 },
							new int[3] { 1920, 1040, 0 },
							new int[3] { 1600, 900, 0 },
							new int[3] { 800, 600, 0 }
						};
						foreach (int[] array2 in array)
						{
							string text = array2[0] + "x" + array2[1] + "-" + ((array2[2] == 0) ? "auto" : array2[2].ToString());
							main.Size = new Size(array2[0], array2[1]);
							main.ApplyZoom();
							timetableFilterPanel.Reflow();
							Application.DoEvents();
							tabControl.SelectedIndex = 0;
							Application.DoEvents();
							CheckGroups(timetableFilterPanel.Options);
							CheckGroups(timetableFilterPanel.SearchRow);
							Check(timetableFilterPanel.Options.Bottom <= timetableFilterPanel.SearchRow.Top && timetableFilterPanel.SearchRow.Bottom <= timetableFilterPanel.Notes.Top, "Brak nakładania wierszy filtrów: " + text);
							Check(timetableFilterPanel.Notes.Bottom <= timetableFilterPanel.ClientSize.Height, "Całe pola uwag mieszczą się nad tabelą: " + text);
							Label label = (Label)Field(main, "filterInfo");
							Check(label.ClientSize.Height >= label.Font.Height * 3, "Pełne trzy wiersze informacji dodatkowych: " + text);
							Control control = (Control)Field(main, "headerClock");
							Rectangle rect = new Rectangle(main.PointToClient(control.PointToScreen(Point.Empty)), control.Size);
							DataGridView dataGridView = (DataGridView)Field(main, "trains");
							int y = main.PointToClient(dataGridView.PointToScreen(Point.Empty)).Y;
							Check(!control.Visible || rect.Bottom <= y, "Duży zegar mieści się nad tabelą: " + text);
							if (control.Visible)
							{
								for (int j = 0; j < tabControl.TabPages.Count; j++)
								{
									Rectangle tabRect = tabControl.GetTabRect(j);
									tabRect.Offset(main.PointToClient(tabControl.PointToScreen(Point.Empty)));
									Check(!tabRect.IntersectsWith(rect), "Zegar nie zasłania nazwy zakładki: " + text + " / " + j);
								}
							}
							for (int k = 0; k < tabControl.TabPages.Count; k++)
							{
								Rectangle tabRect2 = tabControl.GetTabRect(k);
								int width = TextRenderer.MeasureText(tabControl.TabPages[k].Text, tabControl.Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
								Check(tabRect2.Width >= width, "Zakładka mieści pełną nazwę: " + text + " / " + k + " [" + tabRect2.Width + " >= " + width + "]");
							}
							Ui.Capture(main, Path.Combine(target, "wykaz-" + text + ".png"));
							tabControl.SelectedIndex = 1;
							Application.DoEvents();
							Panel surface = (Panel)Field(main, "descriptionSurface");
							CheckNoOverlap(surface);
							Check(surface.Controls.Cast<Control>().All((Control x) => x.Left >= 0 && x.Right <= surface.ClientSize.Width && x.Top >= 0 && x.Bottom <= surface.ClientSize.Height), "Pola opisu mieszczą się w stałym formularzu: " + text);
							Dictionary<string, TextBox> dictionary = (Dictionary<string, TextBox>)Field(main, "profileBoxes");
							Check((double)dictionary["Number"].Width < (double)dictionary["Applicant"].Width * 0.5, "Krótki numer bez rozciągania na całą szerokość: " + text);
							if (array2[0] == 1920 && array2[2] == 0)
							{
								Check(((ScrollableControl)surface.Parent).AutoScroll, "Opis daje się przewijać przy proporcjach ze zdjęcia.");
							}
							Ui.Capture(main, Path.Combine(target, "opis-" + text + ".png"));
						}
						tabControl.SelectedIndex = 0;
						string oldId = engine.State.Trains[0].Id;
						string text2 = string.Join(",", engine.State.Trains.Select((Train x) => x.Number));
						main.QuickRegister(engine.State.Trains[0], null);
						Check(engine.State.Entries.Any((JournalEntry x) => x.Type == "Pociąg"), "Sesja przechowuje wpisy podczas ćwiczenia.");
						main.ResetTrainingSession();
						Check(engine.State.Entries.All((JournalEntry x) => x.Type != "Pociąg") && !engine.State.Trains.Any((Train x) => x.Id == oldId) && engine.CanWrite, "Nowa sesja usuwa wpisy i pozwala kontynuować ćwiczenie.");
						Check(string.Join(",", engine.State.Trains.Select((Train x) => x.Number)) != text2, "Nowa sesja losuje inne numery pociągów.");
						Check(!Directory.Exists(folder), "Sesja ćwiczeniowa nie tworzy bazy na dysku.");
						Store store2 = new Store(folder, transient: true);
						store2.Load();
						Check(store2.State.Entries.Count == 0 && store2.State.Duties.Count == 0, "Ponowne uruchomienie rozpoczyna czyste dzienniki i dyżur.");
						main.Size = new Size(1920, 1040);
						main.ApplyZoom();
						main.SelectTrain(engine.State.Trains[0].Id);
						tabControl.SelectedIndex = 0;
						Ui.Capture(main, Path.Combine(target, "nowa-sesja-full-hd.png"));
						tabControl.SelectedIndex = 1;
						Ui.Capture(main, Path.Combine(target, "nowa-sesja-opis.png"));
						tabControl.SelectedIndex = 0;
						Application.DoEvents();
						DataGridView dataGridView2 = (DataGridView)Field(main, "trains");
						Check(dataGridView2.Columns["k"].Width < 50 && dataGridView2.Columns["nk"].Width < 50, "Kolumny K i NK zachowują wąskie proporcje.");
						Check(!dataGridView2.Columns["filler"].Visible && dataGridView2.Columns["carrier"].Visible, "Wykaz kończy się kolumną przewoźnika zamiast pustą kolumną.");
						Check(dataGridView2.Tag as bool? == true && dataGridView2.Columns["pa"].HeaderText == "Przyjazd plan.", "Daty i pełne nagłówki zgodne ze zdjęciem.");
						SplitContainer splitContainer = (SplitContainer)Field(main, "split");
						Check((double)splitContainer.SplitterDistance / (double)splitContainer.Height > 0.55 && (double)splitContainer.SplitterDistance / (double)splitContainer.Height < 0.61, "Podział wykazu i dzienników odpowiada proporcjom z instrukcji.");
						List<JournalPane> source = (List<JournalPane>)Field(main, "panes");
						Check(source.Where((JournalPane p) => p.SectionId != "TEL").All((JournalPane p) => !p.Grid.Columns["notify"].Visible), "Brak kolumny dróżników bez konfiguracji powiadomień.");
						Check(engine.State.Entries.All((JournalEntry x) => x.Type != "Doba"), "Nagłówek dnia jest elementem widoku i nie tworzy fikcyjnego zdarzenia.");
						engine.Change("Test konfiguracji dróżników", engine.StationId, (AppState state) =>
						{
							state.Stations[0].Sections[0].NotifyCrossings = true;
						});
						main.BuildJournals();
						source = (List<JournalPane>)Field(main, "panes");
						Check(source[0].Grid.Columns["notify"].Visible, "Konfiguracja przywraca kolumnę powiadomień.");
						engine.Change("Test konfiguracji dróżników", engine.StationId, (AppState state) =>
						{
							state.Stations[0].Sections[0].NotifyCrossings = false;
						});
						main.BuildJournals();
						BuildComparison(main);
						tabControl.SelectedIndex = 0;
						main.RefreshData();
						main.SelectTrain(engine.State.Trains[2].Id);
						Application.DoEvents();
						Ui.Capture(main, Path.Combine(target, "porownanie-wpisy-full-hd.png"));
						source = (List<JournalPane>)Field(main, "panes");
						JournalPane journalPane = source[0];
						DataGridViewRow dataGridViewRow = journalPane.Grid.Rows.Cast<DataGridViewRow>().First((DataGridViewRow r) => ((JournalEntry)r.Tag).Type == "Pociąg");
						typeof(DataGridView).GetMethod("OnCellMouseDown", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[1] { typeof(DataGridViewCellMouseEventArgs) }, null).Invoke(journalPane.Grid, new object[1]
						{
							new DataGridViewCellMouseEventArgs(journalPane.Grid.Columns["odd"].Index, dataGridViewRow.Index, 4, 4, new MouseEventArgs(MouseButtons.Right, 1, 4, 4, 0))
						});
						Check(journalPane.Grid.CurrentCell.Visible, "Menu wpisu działa przy ukrytej kolumnie technicznej.");
						log.Add("ALL LAYOUT AND SESSION TESTS PASSED");
						success = true;
					}
					catch (Exception ex)
					{
						log.Add(ex.ToString());
					}
					finally
					{
						File.WriteAllLines(Path.Combine(target, "wyniki-layout.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
						main.Hide();
						main.Dispose();
						Application.ExitThread();
					}
				});
			};
			Application.Run(main);
		}
		finally
		{
			if (main != null)
			{
				((IDisposable)main).Dispose();
			}
		}
		return success;
	}

	private static void BuildComparison(MainForm main)
	{
		Engine engine = main.Engine;
		engine.Change("Scenariusz porównawczy", engine.StationId, (AppState state) =>
		{
			state.Trains = state.Trains.Take(7).ToList();
			for (int i = 0; i < state.Trains.Count; i++)
			{
				Train train2 = state.Trains[i];
				train2.From = engine.Station.Sections[(i == 0 || i == 2) ? 1 : 0].Name;
				train2.To = engine.Station.Sections[(i != 0 && i != 2) ? 1 : 0].Name;
				string lineIn = (train2.LineOut = engine.Station.Sections[0].Line);
				train2.LineIn = lineIn;
				train2.PlanArrival = DateTime.Now.AddMinutes(-22 + i * 8);
				train2.PlanDeparture = train2.PlanArrival.AddMinutes(2.0);
				train2.Track = (1 + i % 4).ToString();
				train2.Runs = true;
			}
		});
		for (int num = 0; num < 4; num++)
		{
			Train train = engine.State.Trains[num];
			JournalEntry journalEntry = engine.RegisterScheduledTrain(train.Id, null);
			journalEntry.Arrived = ((num < 2) ? new DateTime?(train.PlanArrival.AddMinutes((num == 0) ? (-6) : 2)) : ((DateTime?)null));
			journalEntry.Left = null;
			engine.SaveJournal(journalEntry);
			if (num < 3)
			{
				JournalEntry journalEntry2 = engine.RegisterScheduledTrain(train.Id, null);
				journalEntry2.Left = ((num < 2) ? new DateTime?(journalEntry.Arrived.Value.AddMinutes(2.0)) : ((DateTime?)null));
				engine.SaveJournal(journalEntry2);
			}
		}
	}

	private static void CheckGroups(FlowLayoutPanel row)
	{
		CheckNoOverlap(row);
		foreach (Control group in row.Controls)
		{
			CheckNoOverlap(group);
			IEnumerable<Control> source = group.Controls.Cast<Control>();
			Func<Control, bool> predicate = (Control c) => c.Right > group.ClientSize.Width || c.Bottom > group.ClientSize.Height;
			if (source.Any(predicate))
			{
				throw new Exception("Kontrolka wychodzi poza grupę filtrów.");
			}
		}
		if (row.Controls.Cast<Control>().Any((Control c) => c.Bottom > row.ClientSize.Height))
		{
			throw new Exception("Grupa wychodzi poza wiersz filtrów.");
		}
	}

	private static void CheckNoOverlap(Control parent)
	{
		Control[] array = (from Control x in parent.Controls
			where x.Visible
			select x).ToArray();
		for (int num = 0; num < array.Length; num++)
		{
			for (int num2 = num + 1; num2 < array.Length; num2++)
			{
				Rectangle rectangle = Rectangle.Intersect(array[num].Bounds, array[num2].Bounds);
				if (rectangle.Width > 0 && rectangle.Height > 0)
				{
					throw new Exception(string.Concat("Nakładają się kontrolki: ", array[num].Text, " / ", array[num2].Text, " (", rectangle, ")"));
				}
			}
		}
	}
}

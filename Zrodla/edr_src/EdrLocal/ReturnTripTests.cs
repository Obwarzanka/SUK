using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace EdrLocal;

internal static class ReturnTripTests
{
	private static List<string> log = new List<string>();

	private static string progress;

	private static object Field(object o, string name)
	{
		return o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
	}

	private static void Check(bool value, string text)
	{
		if (!value)
		{
			throw new Exception(text);
		}
		log.Add("PASS: " + text);
		File.AppendAllText(progress, text + Environment.NewLine);
	}

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

	private static void Dialog(MainForm main, Action<Form> fill, Action open)
	{
		Exception error = null;
		bool ran = false;
		Timer timer = new Timer
		{
			Interval = 250
		};
		try
		{
			timer.Tick += (object s, EventArgs e) =>
			{
				File.AppendAllText(progress, "TICK " + string.Join(" / ", from Form x in Application.OpenForms
					select x.Text + ":" + x.Visible) + Environment.NewLine);
				Form form = Application.OpenForms.Cast<Form>().LastOrDefault((Form x) => x != main && x.Visible);
				if (form == null)
				{
					return;
				}
				timer.Stop();
				try
				{
					File.AppendAllText(progress, "FILL START\n");
					fill(form);
					File.AppendAllText(progress, "FILL END\n");
					ran = true;
				}
				catch (Exception ex)
				{
					error = ex;
					form.DialogResult = DialogResult.Cancel;
					form.Close();
				}
			};
			timer.Start();
			File.AppendAllText(progress, "OPEN START\n");
			open();
			File.AppendAllText(progress, "OPEN END\n");
		}
		finally
		{
			if (timer != null)
			{
				((IDisposable)timer).Dispose();
			}
		}
		if (error != null)
		{
			throw error;
		}
		Check(ran, "Formularz został wypełniony przez rzeczywiste kontrolki.");
	}

	private static void Type(JournalPane pane, DataGridViewRow row, string column, string value)
	{
		pane.Grid.CurrentCell = row.Cells[column];
		if (!pane.Grid.IsCurrentCellInEditMode)
		{
			pane.Grid.BeginEdit(selectAll: true);
		}
		((TextBox)pane.Grid.EditingControl).Text = value;
		if (!pane.Grid.EndEdit() || !pane.CommitPending())
		{
			throw new Exception(pane.LastEditError ?? "Nie zapisano pola.");
		}
	}

	public static bool Run(string folder)
	{
		Ui.Testing = true;
		Directory.CreateDirectory(folder);
		progress = Path.Combine(folder, "progress.txt");
		File.WriteAllText(progress, "START\n");
		bool ok = false;
		Store store = new Store(Path.Combine(folder, "session"), transient: true);
		store.Load();
		Engine engine = new Engine(store)
		{
			User = store.State.Users.First((LocalUser x) => x.Id == "DEMO")
		};
		engine.BeginDuty(DateTime.Now.AddHours(-1.0));
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
						main.Size = new Size(1920, 1040);
						main.ApplyZoom();
						List<JournalPane> source = (List<JournalPane>)Field(main, "panes");
						JournalPane journalPane = source.First((JournalPane x) => x.SectionId == "ZAR");
						Train train = engine.State.Trains[18];
						main.QuickRegister(train, "ZAR");
						DataGridViewRow dataGridViewRow = journalPane.Grid.Rows.Cast<DataGridViewRow>().First((DataGridViewRow x) => ((JournalEntry)x.Tag).TrainId == train.Id);
						journalPane.Grid.CurrentCell = dataGridViewRow.Cells["arrived"];
						journalPane.Grid.BeginEdit(selectAll: true);
						Control editingControl = journalPane.Grid.EditingControl;
						Check(editingControl is ClockEditingControl, "Scenariusz zaczyna się z otwartą maską godziny w dzienniku.");
						Dialog(main, (Form form) =>
						{
							TelephoneEditor telephoneEditor = (TelephoneEditor)form;
							telephoneEditor.TemplateBox.SelectedIndex = 0;
							telephoneEditor.Receive.Checked = true;
							telephoneEditor.SenderBox.Text = "Janowski";
							((ComboBox)Field(telephoneEditor, "track")).Text = "2";
							telephoneEditor.ReasonBox.Text = "usterki blokady";
							Button button = (Button)telephoneEditor.Buttons.Controls[1];
							Ui.Capture(telephoneEditor, Path.Combine(folder, "telefonogram-przed.png"));
							button.PerformClick();
							Check(telephoneEditor.SavedEntry != null, "Przycisk Zapisz wykonał zapis telefonogramu.");
							button.PerformClick();
						}, () =>
						{
							main.Telephone("ZAR");
						});
						List<JournalEntry> phones = engine.State.Entries.Where((JournalEntry x) => x.SectionId == "ZAR" && x.Type == "Telefonogram").ToList();
						Check(phones.Count == 1, "Jedno zapisanie TZP, także po podwójnym kliknięciu, tworzy jeden wpis.");
						Check(journalPane.Grid.Rows.Cast<DataGridViewRow>().Any((DataGridViewRow x) => ((JournalEntry)x.Tag).Id == phones[0].Id), "Pierwszy TZP jest widoczny natychmiast po zamknięciu formularza.");
						Check(engine.CurrentBasis("ZAR", 2) == "Zapowiadanie telefoniczne", "Pierwszy TZP zmienia podstawę ruchu na właściwym torze.");
						dataGridViewRow = journalPane.Grid.Rows.Cast<DataGridViewRow>().First((DataGridViewRow x) => ((JournalEntry)x.Tag).TrainId == train.Id);
						journalPane.Grid.CurrentCell = dataGridViewRow.Cells["notes"];
						journalPane.Grid.BeginEdit(selectAll: true);
						((TextBox)journalPane.Grid.EditingControl).Text = "Niezapisana uwaga";
						JournalEntry note = engine.Note("ZAR", DateTime.Now, "Drugi wpis widoczny bez zamykania maski", "Wpis");
						engine.SaveJournal(note);
						journalPane.RefreshData();
						Check(journalPane.Grid.IsCurrentCellInEditMode && ((TextBox)journalPane.Grid.EditingControl).Text == "Niezapisana uwaga", "Odświeżanie zachowuje treść aktywnego edytora.");
						Check(journalPane.Grid.Rows.Cast<DataGridViewRow>().Any((DataGridViewRow x) => ((JournalEntry)x.Tag).Id == note.Id), "Nowy wpis pojawia się również przy niezapisanej edycji innego wiersza.");
						Check(journalPane.CommitPending(), "Zachowany szkic można później zapisać.");
						Dialog(main, (Form form) =>
						{
							Editor editor = (Editor)form;
							List<Control> source2 = Controls(form).ToList();
							TextBox textBox = source2.OfType<TextBox>().Single((TextBox x) => x.Left == 157 && x.Top == 77);
							textBox.Text = "Rob2";
							ComboBox comboBox = source2.OfType<ComboBox>().Single((ComboBox x) => x.Left == 157 && x.Top == 111);
							comboBox.Text = "1";
							CheckBox checkBox = source2.OfType<CheckBox>().Single((CheckBox x) => x.Text == "Powrót ze szlaku");
							checkBox.Checked = true;
							TextBox textBox2 = source2.OfType<TextBox>().Single((TextBox x) => x.Multiline && x.Top == 337);
							Check(textBox2.Text == "do km ... i z powrotem", "Zaznaczenie powrotu wstawia wzór do uzupełnienia kilometra.");
							textBox2.Text = "Po zamkniętym torze nr 2 do kilometra 22,200 i z powrotem. Zjazd do Jaworzyny Śląskiej do godz. 14:40.";
							List<ReferenceTimeInput> list = (from x in source2.OfType<ReferenceTimeInput>()
								orderby x.Top
								select x).ToList();
							list[0].Input.Text = "12:26";
							list[0].Active.Checked = true;
							list[1].Input.Text = "12:31";
							list[1].Active.Checked = true;
							Ui.Capture(form, Path.Combine(folder, "formularz-roboczy.png"));
							((Button)editor.Buttons.Controls[1]).PerformClick();
						}, () =>
						{
							main.EditJournal(null, null, "DO", "ZAR");
						});
						JournalEntry root = engine.State.Entries.Single((JournalEntry x) => x.Number == "Rob2" && x.ReturnFromLine);
						JournalEntry child = engine.State.Entries.Single((JournalEntry x) => x.ReturnOfId == root.Id);
						Check(root.Direction == "DO" && child.Direction == "Z" && child.SectionId == root.SectionId, "Powrót tworzy atomowo dwa powiązane kierunki w tym samym dzienniku.");
						Check(!root.Arrived.HasValue && !child.Left.HasValue && string.IsNullOrEmpty(child.Track), "Drugi wiersz pozostawia do uzupełnienia tor i godzinę powrotu.");
						DataGridView grid = journalPane.Grid;
						DataGridViewRow dataGridViewRow2 = grid.Rows.Cast<DataGridViewRow>().Single((DataGridViewRow x) => ((JournalEntry)x.Tag).Id == root.Id);
						DataGridViewRow dataGridViewRow3 = grid.Rows.Cast<DataGridViewRow>().Single((DataGridViewRow x) => ((JournalEntry)x.Tag).Id == child.Id);
						Check(dataGridViewRow3.Index == dataGridViewRow2.Index + 1 && grid.Columns["indicator"].Visible, "Para jest widoczna od razu, obok siebie, z kolumną klamry.");
						Check(Convert.ToString(dataGridViewRow2.Cells["even"].Value) == "Rob2" && string.IsNullOrEmpty(Convert.ToString(dataGridViewRow3.Cells["even"].Value)) && string.IsNullOrEmpty(Convert.ToString(dataGridViewRow3.Cells["odd"].Value)), "Rob2 trafia do kolumny parzystej, a wiersz powrotu nie powtarza numeru.");
						Check(engine.LineStatus("ZAR", 2).StartsWith("ZAJĘTY"), "Wyjazd roboczego zajmuje właściwy tor szlakowy.");
						grid.EndEdit();
						grid.ClearSelection();
						grid.Rows[dataGridViewRow2.Index].Selected = true;
						grid.FirstDisplayedScrollingRowIndex = Math.Max(0, dataGridViewRow2.Index - 3);
						Ui.Capture(main, Path.Combine(folder, "klamra.png"));
						using (Bitmap bitmap = new Bitmap(grid.Width, grid.Height))
						{
							grid.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
							DataGridViewRow[] array = new DataGridViewRow[2] { dataGridViewRow2, dataGridViewRow3 };
							foreach (DataGridViewRow dataGridViewRow4 in array)
							{
								Rectangle cellDisplayRectangle = grid.GetCellDisplayRectangle(grid.Columns["indicator"].Index, dataGridViewRow4.Index, cutOverflow: false);
								int num2 = 0;
								for (int num3 = cellDisplayRectangle.Left + 2; num3 < cellDisplayRectangle.Right - 2; num3++)
								{
									for (int num4 = cellDisplayRectangle.Top + 2; num4 < cellDisplayRectangle.Bottom - 2; num4++)
									{
										Color pixel = bitmap.GetPixel(num3, num4);
										if (pixel.R < 100 && pixel.G < 100 && pixel.B < 100)
										{
											num2++;
										}
									}
								}
								Check(num2 > 8, "Klamra jest narysowana w " + ((dataGridViewRow4 == dataGridViewRow2) ? "górnym" : "dolnym") + " wierszu pary.");
							}
						}
						Type(journalPane, dataGridViewRow3, "track", "3");
						Type(journalPane, dataGridViewRow3, "arrived", "12:51");
						Check(engine.LineStatus("ZAR", 2) == "WOLNY" && engine.TrackStatus("3").StartsWith("ZAJĘTY"), "Powrót zwalnia szlak i zajmuje wybrany tor stacyjny.");
						Type(journalPane, dataGridViewRow2, "left", "12:32");
						Check(engine.State.Entries.Single((JournalEntry x) => x.Id == child.Id).Arrived.Value.Hour == 12 && engine.State.Entries.Single((JournalEntry x) => x.Id == child.Id).Arrived.Value.Minute == 51, "Poprawienie wyjazdu nie usuwa zarejestrowanego powrotu.");
						JournalEntry journalEntry = Json.Clone(engine.State.Entries.Single((JournalEntry x) => x.Id == child.Id));
						journalEntry.Arrived = journalEntry.Time.Date.AddHours(12.0);
						bool value = false;
						try
						{
							engine.SaveJournal(journalEntry);
						}
						catch
						{
							value = true;
						}
						Check(value, "Powrót wcześniejszy niż wyjazd jest odrzucany.");
						string text = Json.Text(engine.State);
						try
						{
							store.Commit((AppState x) =>
							{
								x.History[0].Before = "Zmieniona historia";
								x.Trains.Clear();
								throw new Exception("rollback");
							});
						}
						catch
						{
						}
						Check(Json.Text(engine.State) == text, "Szybka kopia transakcji zachowuje pełne wycofanie zmian także w audycie.");
						Check(engine.State.History.All((Audit x) => !x.Before.Contains(engine.User.Hash) && !x.After.Contains(engine.User.Hash)), "Migawki audytu nadal nie zawierają skrótów haseł.");
						Train marked = engine.State.Trains[7];
						engine.SetDangerous(marked.Id, value: true);
						main.SelectTrain(marked.Id);
						DataGridView dataGridView = (DataGridView)Field(main, "trains");
						DataGridViewRow dataGridViewRow5 = dataGridView.Rows.Cast<DataGridViewRow>().First((DataGridViewRow x) => Convert.ToString(x.Tag) == marked.Id);
						Check(dataGridViewRow5.Cells["from"].Style.BackColor == EdrPalette.Dangerous && dataGridViewRow5.Cells["from"].Style.SelectionBackColor == EdrPalette.Dangerous, "TN ma fioletowy kolor z podręcznika ERJ 2022 również przy zaznaczeniu.");
						engine.Cancel(root.Id, "Test wykreślenia błędnej pary");
						Check(engine.State.Entries.Where((JournalEntry x) => x.Id == root.Id || x.ReturnOfId == root.Id).All((JournalEntry x) => x.Cancelled), "Wykreślenie obejmuje całą powiązaną parę.");
						string text2 = new string('x', 100000);
						for (int num5 = 0; num5 < 100; num5++)
						{
							engine.State.History.Add(new Audit
							{
								Time = DateTime.Now,
								Before = text2,
								After = text2,
								Action = "Obciążenie testowe"
							});
						}
						Stopwatch stopwatch = Stopwatch.StartNew();
						engine.SaveJournal(engine.Note("TEL", DateTime.Now, "Test szybkiego zapisu", "Wpis"));
						stopwatch.Stop();
						File.WriteAllText(Path.Combine(folder, "zapis-ms.txt"), stopwatch.Elapsed.TotalMilliseconds.ToString("F2"));
						Check(engine.State.Entries.Any((JournalEntry x) => x.Text == "Test szybkiego zapisu"), "Zapis działa także z dużą historią bez zagnieżdżania jej w nowej migawce.");
						ok = true;
						log.Add("ALL RETURN TRIP AND IMMEDIATE REFRESH TESTS PASSED");
					}
					catch (Exception ex)
					{
						log.Add(ex.ToString());
					}
					finally
					{
						File.WriteAllLines(Path.Combine(folder, "wyniki-powrot-odswiezanie.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
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
		return ok;
	}
}

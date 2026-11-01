using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace EdrLocal;

internal static class InteractionTests
{
	private static List<string> log = new List<string>();

	private static object Field(object owner, string name)
	{
		return owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
	}

	private static void Check(bool ok, string name)
	{
		if (!ok)
		{
			throw new Exception("FAIL: " + name);
		}
		log.Add("PASS: " + name);
	}

	private static void Wheel(DataGridView grid, int delta)
	{
		typeof(ReferenceGrid).GetMethod("OnMouseWheel", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(grid, new object[1]
		{
			new HandledMouseEventArgs(MouseButtons.None, 0, 10, 10, delta)
		});
		Application.DoEvents();
	}

	public static bool Run(string target)
	{
		Ui.Testing = true;
		Directory.CreateDirectory(target);
		bool passed = false;
		Store store = new Store(Path.Combine(target, "session"), transient: true);
		store.Load();
		Engine engine = new Engine(store);
		engine.StartSession(engine.State.Users.First((LocalUser x) => x.Id == "DEMO"));
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
						typeof(MainForm).GetField("useTimeRange", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(main, false);
						engine.Change("Scenariusze wstęgi", engine.StationId, (AppState state) =>
						{
							for (int i = 0; i < 4; i++)
							{
								Train train3 = state.Trains[i];
								train3.Runs = true;
								train3.Origin = ((i == 1) ? engine.Station.Name : "A");
								train3.Destination = ((i == 0) ? engine.Station.Name : "B");
								train3.PlanArrival = DateTime.Now.AddMinutes(i * 8);
								train3.PlanDeparture = train3.PlanArrival.AddMinutes((i != 2) ? 2 : 0);
							}
						});
						main.RefreshData();
						DataGridView dataGridView = (DataGridView)Field(main, "trains");
						Color[] array = new Color[4]
						{
							Color.Magenta,
							Color.Lime,
							Color.Cyan,
							Color.FromArgb(51, 153, 255)
						};
						for (int num = 0; num < 4; num++)
						{
							Train train = engine.State.Trains[num];
							main.SelectTrain(train.Id);
							Application.DoEvents();
							DataGridViewRow currentRow = dataGridView.CurrentRow;
							if (!currentRow.Displayed)
							{
								dataGridView.FirstDisplayedScrollingRowIndex = currentRow.Index;
							}
							Application.DoEvents();
							Check(currentRow.DefaultCellStyle.SelectionBackColor.ToArgb() == array[num].ToArgb(), "Wstęga: " + (new string[4] { "koniec biegu", "początek biegu", "przejazd bez postoju", "przejazd z postojem" })[num]);
							using (Bitmap bitmap = new Bitmap(dataGridView.Width, dataGridView.Height))
							{
								dataGridView.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
								Rectangle cellDisplayRectangle = dataGridView.GetCellDisplayRectangle(dataGridView.Columns["pa"].Index, currentRow.Index, cutOverflow: false);
								bitmap.Save(Path.Combine(target, "grid-" + num + ".png"));
								Check(bitmap.GetPixel(cellDisplayRectangle.Left + 4, cellDisplayRectangle.Top + 4).ToArgb() == array[num].ToArgb(), string.Concat("Rzeczywisty render koloru wstęgi: ", num, " / ", bitmap.GetPixel(cellDisplayRectangle.Left + 4, cellDisplayRectangle.Top + 4), " rect=", cellDisplayRectangle, " selected=", currentRow.Selected));
							}
							Ui.Capture(main, Path.Combine(target, "wstega-" + num + ".png"));
						}
						dataGridView.CurrentCell = dataGridView.Rows[1].Cells["odd"];
						Check(main.SelectedTrain.Id == (string)dataGridView.Rows[1].Tag, "Wybór pociągu jest aktualny przed odświeżeniem opisu.");
						dataGridView.FirstDisplayedScrollingRowIndex = 0;
						int firstDisplayedScrollingRowIndex = dataGridView.FirstDisplayedScrollingRowIndex;
						Wheel(dataGridView, -120);
						Check(dataGridView.CurrentCell.RowIndex == 2 && main.SelectedTrain.Id == (string)dataGridView.Rows[2].Tag, "Kółko wybiera następny pociąg i aktualizuje opis.");
						Check(dataGridView.FirstDisplayedScrollingRowIndex == firstDisplayedScrollingRowIndex, "Kółko nie przesuwa widoku, gdy wybór mieści się na ekranie.");
						Wheel(dataGridView, 120);
						Check(dataGridView.CurrentCell.RowIndex == 1, "Kółko w górę wybiera poprzedni pociąg.");
						Wheel(dataGridView, -60);
						Check(dataGridView.CurrentCell.RowIndex == 1, "Mały obrót kółka nie pomija pociągu.");
						Wheel(dataGridView, -60);
						Check(dataGridView.CurrentCell.RowIndex == 2, "Dwa małe obroty składają się na jeden krok.");
						dataGridView.CurrentCell = dataGridView.Rows[0].Cells["odd"];
						Wheel(dataGridView, 240);
						Check(dataGridView.CurrentCell.RowIndex == 0, "Kółko zatrzymuje się na pierwszym pociągu.");
						dataGridView.CurrentCell = dataGridView.Rows[dataGridView.Rows.Count - 1].Cells["odd"];
						Wheel(dataGridView, -240);
						Check(dataGridView.CurrentCell.RowIndex == dataGridView.Rows.Count - 1, "Kółko zatrzymuje się na ostatnim pociągu.");
						Wheel(dataGridView, 120);
						Check(dataGridView.CurrentRow.Displayed, "Wybór poza poprzednim widokiem pozostaje widoczny.");
						using (ReferenceGrid referenceGrid = new ReferenceGrid
						{
							SelectWithWheel = true
						})
						{
							Wheel(referenceGrid, -120);
							Check(referenceGrid.CurrentCell == null, "Kółko działa bez błędu dla pustego wykazu.");
						}
						Train train2 = engine.State.Trains[3];
						main.QuickRegister(train2, null);
						main.QuickRegister(train2, null);
						main.SelectTrain(train2.Id);
						Application.DoEvents();
						DataGridViewRow currentRow2 = dataGridView.CurrentRow;
						if (!currentRow2.Displayed)
						{
							dataGridView.FirstDisplayedScrollingRowIndex = currentRow2.Index;
						}
						Application.DoEvents();
						Check((string)currentRow2.Cells["from"].Tag == "registered" && (string)currentRow2.Cells["to"].Tag == "registered", "Znaczniki rozróżniają zarejestrowane przyjęcie i wyprawienie.");
						using (Bitmap bitmap2 = new Bitmap(dataGridView.Width, dataGridView.Height))
						{
							dataGridView.DrawToBitmap(bitmap2, new Rectangle(Point.Empty, bitmap2.Size));
							Rectangle cellDisplayRectangle2 = dataGridView.GetCellDisplayRectangle(dataGridView.Columns["from"].Index, currentRow2.Index, cutOverflow: false);
							Rectangle cellDisplayRectangle3 = dataGridView.GetCellDisplayRectangle(dataGridView.Columns["to"].Index, currentRow2.Index, cutOverflow: false);
							Check(bitmap2.GetPixel(cellDisplayRectangle2.Right - 3, cellDisplayRectangle2.Top + 4).ToArgb() == Color.Gray.ToArgb(), "Znacznik wjazdu jest po prawej stronie kierunku przyjazdu.");
							Check(bitmap2.GetPixel(cellDisplayRectangle3.Left + 2, cellDisplayRectangle3.Top + 4).ToArgb() == Color.Gray.ToArgb() && bitmap2.GetPixel(cellDisplayRectangle3.Right - 3, cellDisplayRectangle3.Top + 4).ToArgb() != Color.Gray.ToArgb(), "Znacznik wyjazdu jest po lewej stronie kierunku wyjazdu.");
						}
						ToolStripStatusLabel toolStripStatusLabel = (ToolStripStatusLabel)Field(main, "statusLogin");
						string text = "Zalogowany od: " + engine.LoggedInAt.ToString("HH:mm:ss  dd-MM-yyyy");
						Check(toolStripStatusLabel.Text == text && engine.LoggedInAt > engine.ActiveDuty.Start, "Stopka pokazuje godzinę logowania, a nie przyjęcia dyżuru.");
						main.RefreshData();
						Check(toolStripStatusLabel.Text == text, "Odświeżenie po zapisie zachowuje godzinę logowania.");
						main.RefreshData();
						Check(toolStripStatusLabel.Text == text, "Odświeżenie nie przesuwa czasu logowania.");
						Ui.Capture(main, Path.Combine(target, "podglad-interakcji.png"));
						main.ResetTrainingSession();
						Check(toolStripStatusLabel.Text == text, "Reset danych ćwiczenia zachowuje czas logowania.");
						Check(!AllControls(main).Any((Control c) => c.Text == "SYMULATOR"), "W głównym oknie nie ma napisu SYMULATOR.");
						log.Add("ALL INTERACTION TESTS PASSED");
						passed = true;
					}
					catch (Exception ex)
					{
						log.Add(ex.ToString());
					}
					finally
					{
						File.WriteAllLines(Path.Combine(target, "wyniki-interakcji.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
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
		return passed;
	}

	private static IEnumerable<Control> AllControls(Control root)
	{
		yield return root;
		foreach (Control child in root.Controls)
		{
			foreach (Control item in AllControls(child))
			{
				yield return item;
			}
		}
	}
}

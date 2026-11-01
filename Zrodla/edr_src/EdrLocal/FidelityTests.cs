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

internal static class FidelityTests
{
	private static List<string> log = new List<string>();

	private static object Field(object owner, string name)
	{
		return owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
	}

	private static void Check(bool value, string text)
	{
		if (!value)
		{
			throw new Exception(text);
		}
		log.Add("PASS: " + text);
	}

	private static void Press(TextBox box, char digit)
	{
		typeof(Control).GetMethod("OnKeyPress", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(box, new object[1]
		{
			new KeyPressEventArgs(digit)
		});
	}

	private static void Key(TextBox box, Keys key)
	{
		typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(box, new object[1]
		{
			new KeyEventArgs(key)
		});
	}

	private static void Wheel(DataGridView g, int delta)
	{
		typeof(ReferenceGrid).GetMethod("OnMouseWheel", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(g, new object[1]
		{
			new HandledMouseEventArgs(MouseButtons.None, 0, 10, 10, delta)
		});
	}

	public static bool Run(string folder)
	{
		Ui.Testing = true;
		Directory.CreateDirectory(folder);
		bool success = false;
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
			main.Shown += (object sender, EventArgs args) =>
			{
				Ui.AfterFrame(() =>
				{
					try
					{
						Train named = engine.State.Trains.Single((Train x) => x.Number == "70413");
						Check(named.Profile != null && named.Profile.Name == "Janiczyk", "Nowa sesja zawiera pociąg 70413 z nazwą Janiczyk.");
						Check(Store.Seed().Trains.Single((Train x) => x.Number == "70413").Profile.Name == "Janiczyk", "Przykładowy rozkład trwały również zawiera nazwany pociąg.");
						Check(!named.Arrival.HasValue && !named.Departure.HasValue && TrainPrediction.Arrival(named) == named.PlanArrival && TrainPrediction.Departure(named) == named.PlanDeparture, "Dla planowego pociągu niepotwierdzone czasy wyświetlane są równe planowym.");
						named.ArrivalForecast = named.PlanArrival.AddMinutes(27.0);
						Check(TrainPrediction.Arrival(named) == named.PlanArrival.AddMinutes(27.0) && TrainPrediction.Departure(named) == named.PlanDeparture.AddMinutes(27.0), "Prognoza opóźnionego przyjazdu przesuwa lokalną prognozę odjazdu.");
						named.Forecast = named.PlanDeparture.AddMinutes(42.0);
						DateTime value = TrainPrediction.Departure(named);
						DateTime? forecast = named.Forecast;
						Check(value == forecast, "Jawna prognoza odjazdu ma pierwszeństwo przed lokalnym obliczeniem.");
						named.Departure = named.PlanDeparture.AddMinutes(12.0);
						DateTime value2 = TrainPrediction.Departure(named);
						DateTime? departure = named.Departure;
						Check(value2 == departure, "Zatwierdzony odjazd ma pierwszeństwo przed prognozą.");
						named.Departure = null;
						named.Forecast = null;
						named.Arrival = named.PlanArrival.AddMinutes(-5.0);
						Check(TrainPrediction.Departure(named) == named.PlanDeparture, "Wcześniejszy przyjazd nie przesuwa prognozy odjazdu przed godzinę planową.");
						named.Arrival = null;
						named.ArrivalForecast = null;
						int count = engine.State.History.Count;
						int count2 = engine.State.Entries.Count;
						main.SelectTrain(named.Id);
						DataGridView dataGridView = (DataGridView)Field(main, "trains");
						TabControl tabControl = (TabControl)Field(main, "topTabs");
						Application.DoEvents();
						DataGridViewRow dataGridViewRow = dataGridView.Rows.Cast<DataGridViewRow>().First((DataGridViewRow x) => Convert.ToString(x.Tag) == named.Id);
						Check(Convert.ToString(dataGridViewRow.Cells["rd"].Value).EndsWith(named.PlanDeparture.ToString("HH:mm")), "Wykaz pokazuje planową godzinę jako niepotwierdzony odjazd rzeczywisty.");
						Check(engine.State.History.Count == count && engine.State.Entries.Count == count2 && !named.Departure.HasValue, "Wyświetlanie prognozy nie dopisuje czasu ani wiersza do dziennika.");
						Check(((Label)Field(main, "filterInfo")).Text.Contains("Janiczyk"), "Nazwa Janiczyk jest widoczna w informacjach dodatkowych.");
						main.MaximumSize = Size.Empty;
						main.Size = new Size(1920, 1040);
						main.ApplyZoom();
						Ui.Capture(main, Path.Combine(folder, "Janiczyk.png"));
						Dictionary<string, DataGridView> dictionary = (Dictionary<string, DataGridView>)Field(main, "detailGrids");
						int hiddenRows = 0;
						foreach (DataGridView value3 in dictionary.Values)
						{
							value3.RowsAdded += (object s, DataGridViewRowsAddedEventArgs e) =>
							{
								hiddenRows += e.RowCount;
							};
						}
						count = engine.State.History.Count;
						Stopwatch stopwatch = Stopwatch.StartNew();
						for (int num = 0; num < 80; num++)
						{
							Wheel(dataGridView, (num / 20 % 2 == 0) ? (-120) : 120);
							Application.DoEvents();
						}
						stopwatch.Stop();
						Check(hiddenRows == 0, "Osiemdziesiąt kroków kółka w wykazie nie przebudowuje ukrytych tabel.");
						Check(engine.State.History.Count == count, "Przewijanie nie zapisuje audytu ani danych ruchowych.");
						File.WriteAllText(Path.Combine(folder, "nawigacja-ms.txt"), stopwatch.Elapsed.TotalMilliseconds.ToString("F2"));
						Train selectedTrain = main.SelectedTrain;
						tabControl.SelectedIndex = 2;
						Application.DoEvents();
						Check(((Dictionary<string, Label>)Field(main, "detailHeadings"))["route"].Text.Contains(selectedTrain.Number), "Wejście w trasę pokazuje aktualnie wybrany pociąg po szybkim przewijaniu.");
						tabControl.SelectedIndex = 0;
						Check((bool)typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(dataGridView, null), "Główna tabela korzysta z buforowanego rysowania.");
						main.SelectTrain(named.Id);
						main.QuickRegister(named, null);
						List<JournalPane> source = (List<JournalPane>)Field(main, "panes");
						JournalEntry en = engine.State.Entries.First((JournalEntry x) => x.TrainId == named.Id);
						JournalPane journalPane = source.First((JournalPane x) => x.SectionId == en.SectionId);
						DataGridView grid = journalPane.Grid;
						DataGridViewRow dataGridViewRow2 = grid.Rows.Cast<DataGridViewRow>().First((DataGridViewRow x) => ((JournalEntry)x.Tag).Id == en.Id);
						grid.CurrentCell = dataGridViewRow2.Cells["arrived"];
						if (!grid.IsCurrentCellInEditMode)
						{
							grid.BeginEdit(selectAll: true);
						}
						TextBox textBox = (TextBox)grid.EditingControl;
						Check(textBox is ClockEditingControl && textBox.Text == "__:__", "Puste pole godziny otwiera maskę __:__.");
						Ui.Capture(main, Path.Combine(folder, "maska-godziny.png"));
						count = engine.State.History.Count;
						grid.EndEdit();
						grid.CurrentCell = dataGridViewRow2.Cells["notes"];
						Check(journalPane.CommitPending() && engine.State.History.Count == count, "Samo wejście i wyjście z pustej maski nie zapisuje fałszywej godziny.");
						grid.CurrentCell = dataGridViewRow2.Cells["arrived"];
						if (!grid.IsCurrentCellInEditMode)
						{
							grid.BeginEdit(selectAll: true);
						}
						textBox = (TextBox)grid.EditingControl;
						textBox.Select(0, 0);
						Press(textBox, '0');
						Check(textBox.Text == "0_:__", "Pierwsza cyfra zastępuje pierwsze podkreślenie.");
						Press(textBox, '8');
						Press(textBox, '3');
						Press(textBox, '1');
						Check(textBox.Text == "08:31", "Kolejne cyfry zastępują podkreślenia, dwukropek pozostaje na miejscu.");
						Key(textBox, Keys.Back);
						Check(textBox.Text == "08:3_", "Backspace przywraca podkreślenie bez usuwania dwukropka.");
						Press(textBox, '2');
						Check(textBox.Text == "08:32", "Poprawiana cyfra nadpisuje właściwe miejsce.");
						textBox.SelectAll();
						Key(textBox, Keys.Delete);
						Check(textBox.Text == "__:__", "Delete zaznaczonej godziny przywraca pustą maskę.");
						textBox.Select(0, 0);
						Press(textBox, '1');
						Check(!grid.EndEdit(), "Niepełna godzina blokuje zatwierdzenie komórki.");
						Check(!engine.State.Entries.First((JournalEntry x) => x.Id == en.Id).Arrived.HasValue, "Niepełna maska nie trafia do dziennika.");
						textBox.Text = "0832";
						Check(textBox.Text == "08:32" && grid.EndEdit() && journalPane.CommitPending(), "Cztery cyfry z wklejenia/programowego wpisu są formatowane i poprawnie zapisywane.");
						Check(engine.State.Entries.First((JournalEntry x) => x.Id == en.Id).Arrived.Value.TimeOfDay == new TimeSpan(8, 32, 0), "Zapis maski zachowuje godzinę i minuty.");
						grid.CurrentCell = dataGridViewRow2.Cells["permission"];
						if (!grid.IsCurrentCellInEditMode)
						{
							grid.BeginEdit(selectAll: true);
						}
						Ui.Capture(main, Path.Combine(folder, "maska-pozostale.png"));
						grid.CancelEdit();
						using (ReferenceTimeInput referenceTimeInput = new ReferenceTimeInput(null, DateTime.Today, blue: false))
						{
							Check(referenceTimeInput.Input.Text == "__:__" && !referenceTimeInput.Active.Checked, "Formularz przyjęcia również ma pustą maskę bez automatycznej godziny.");
							referenceTimeInput.Input.Select(0, 0);
							string text = "0945";
							foreach (char digit in text)
							{
								Press(referenceTimeInput.Input, digit);
							}
							referenceTimeInput.Active.Checked = true;
							Check(referenceTimeInput.Value.Value.TimeOfDay == new TimeSpan(9, 45, 0), "Maska w formularzu zwraca podaną godzinę 09:45.");
						}
						using (RailSignal railSignal = new RailSignal
						{
							Status = "WOLNY",
							Number = 1,
							Incoming = true
						})
						{
							using Bitmap bitmap = new Bitmap(40, 30);
							railSignal.DrawToBitmap(bitmap, new Rectangle(0, 0, 40, 30));
							Check(bitmap.GetPixel(15, 18).R < 60, "Wolny tor ma czarne szyny.");
							bitmap.Save(Path.Combine(folder, "tor-wolny.png"));
						}
						using (RailSignal railSignal2 = new RailSignal
						{
							Status = "ZAMKNIĘTY",
							Number = 2,
							Incoming = false
						})
						{
							using Bitmap bitmap2 = new Bitmap(40, 30);
							railSignal2.DrawToBitmap(bitmap2, new Rectangle(0, 0, 40, 30));
							Check(bitmap2.GetPixel(30, 16).ToArgb() == Color.Red.ToArgb() && bitmap2.GetPixel(15, 18).R < 60, "Zamknięcie to czerwona prostokątna tablica obok czarnych szyn.");
							Check(bitmap2.GetPixel(19, 20).ToArgb() != Color.Red.ToArgb(), "Zamknięcia nie przedstawia czerwone koło na torze.");
							bitmap2.Save(Path.Combine(folder, "tor-zamkniety.png"));
						}
						bool[] array = new bool[2] { true, false };
						foreach (bool flag in array)
						{
							using RailSignal railSignal3 = new RailSignal
							{
								Status = "ZAJĘTY: 70413",
								MovementIncoming = flag,
								Number = 1,
								Incoming = flag
							};
							using Bitmap bitmap3 = new Bitmap(40, 30);
							railSignal3.DrawToBitmap(bitmap3, new Rectangle(0, 0, 40, 30));
							Check(bitmap3.GetPixel(23, 18).R > 200 && bitmap3.GetPixel(19, 20).ToArgb() == Color.Blue.ToArgb(), "Zajętość ma czerwone szyny i niebieską strzałkę: " + (flag ? "przyjazd" : "odjazd"));
							bitmap3.Save(Path.Combine(folder, flag ? "tor-zajety-wjazd.png" : "tor-zajety-wyjazd.png"));
						}
						using (RailSignal railSignal4 = new RailSignal
						{
							Status = "ZAMKNIĘTY",
							MovementIncoming = true
						})
						{
							using Bitmap bitmap4 = new Bitmap(40, 30);
							railSignal4.DrawToBitmap(bitmap4, new Rectangle(0, 0, 40, 30));
							Check(bitmap4.GetPixel(30, 16).ToArgb() == Color.Red.ToArgb() && bitmap4.GetPixel(23, 18).R > 200 && bitmap4.GetPixel(19, 20).ToArgb() == Color.Blue.ToArgb(), "Tor zamknięty i zajęty zachowuje jednocześnie tablicę oraz kierunek jazdy.");
						}
						List<RailSignal> list = (List<RailSignal>)Field(source.First((JournalPane x) => x.SectionId == "ZAR"), "signals");
						Check(list[0].Number == 1 && list[1].Number == 2, "Kierunki i numery torów Żarowa pochodzą z konfiguracji, lewy 1 i prawy 2.");
						JournalEntry journalEntry = engine.Note("ZAR", DateTime.Now, "Tor zamknięty", "Telefonogram");
						journalEntry.LineTrack = 2;
						journalEntry.Effect = "Zamknij";
						engine.SaveJournal(journalEntry);
						main.RefreshData();
						Check(list[1].Status == "ZAMKNIĘTY" && list[0].Status != "ZAMKNIĘTY", "Zamknięcie toru 2 zmienia ikonę właściwego toru.");
						Ui.Capture(main, Path.Combine(folder, "zamkniecie-w-programie.png"));
						success = true;
						log.Add("ALL MASK, SIGNAL AND PREDICTION TESTS PASSED");
					}
					catch (Exception ex)
					{
						log.Add(ex.ToString());
					}
					finally
					{
						File.WriteAllLines(Path.Combine(folder, "wyniki-dopasowanie.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
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
}

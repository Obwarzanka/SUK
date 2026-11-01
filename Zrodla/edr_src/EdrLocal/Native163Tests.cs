using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace EdrLocal;

internal static class Native163Tests
{
	private static readonly List<string> log = new List<string>();

	[DllImport("user32.dll")]
	private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

	private static object Field(object instance, string name)
	{
		return instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
	}

	private static void Check(bool ok, string text)
	{
		if (!ok)
		{
			throw new Exception("FAIL: " + text);
		}
		log.Add("PASS: " + text);
	}

	private static void ClickTab(TabControl tabs, int index)
	{
		Rectangle tabRect = tabs.GetTabRect(index);
		IntPtr lParam = new IntPtr((tabRect.Top + tabRect.Height / 2 << 16) | (tabRect.Left + tabRect.Width / 2));
		SendMessage(tabs.Handle, 513, new IntPtr(1), lParam);
		SendMessage(tabs.Handle, 514, IntPtr.Zero, lParam);
	}

	private static IEnumerable<Control> All(Control parent)
	{
		yield return parent;
		foreach (Control child in parent.Controls)
		{
			foreach (Control item in All(child))
			{
				yield return item;
			}
		}
	}

	public static bool Run(string folder)
	{
		Ui.Testing = true;
		LocalPersistence.Enabled = false;
		Directory.CreateDirectory(folder);
		bool success = false;
		Store store = new Store(Path.Combine(folder, "session"));
		store.Load();
		store.State.UiZoom = 2f;
		Engine engine = new Engine(store)
		{
			User = store.State.Users.First()
		};
		engine.BeginDuty(engine.Now.AddMinutes(-1.0));
		Train train = engine.State.Trains.First();
		train.DetailRows = new List<DetailRow>();
		for (int i = 0; i < 120; i++)
		{
			string[] array = new string[29];
			array[0] = (array[2] = (array[13] = (array[15] = engine.Now.ToString("dd-MM-yy"))));
			array[1] = (array[3] = (array[14] = (array[16] = engine.Now.AddMinutes(i).ToString("HH:mm"))));
			array[6] = i.ToString();
			array[7] = ((i == 60) ? engine.Station.Name : ("Posterunek " + i));
			array[19] = train.Number;
			train.DetailRows.Add(new DetailRow
			{
				Id = "route" + i,
				Kind = "route",
				Cells = array
			});
		}
		MainForm main = new MainForm(engine);
		try
		{
			Timer beat = new Timer
			{
				Interval = 15
			};
			try
			{
				Timer driver = new Timer
				{
					Interval = 25
				};
				try
				{
					int heartbeat = 0;
					int step = 0;
					Font tableFont = null;
					Font routeFont = null;
					float density = 0f;
					int cacheCount = 0;
					int manualColumnWidth = 0;
					int manualRouteWidth = 0;
					Stopwatch elapsed = new Stopwatch();
					TabControl tabs = (TabControl)Field(main, "topTabs");
					DataGridView grid = (DataGridView)Field(main, "trains");
					DataGridView route = ((Dictionary<string, DataGridView>)Field(main, "detailGrids"))["route"];
					SplitContainer split = (SplitContainer)Field(main, "split");
					Action finish = () =>
					{
						driver.Stop();
						beat.Stop();
						File.WriteAllLines(Path.Combine(folder, "wyniki-native163.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
						main.Hide();
						main.Dispose();
						Application.ExitThread();
					};
					beat.Tick += (object s, EventArgs e) =>
					{
						heartbeat++;
					};
					driver.Tick += (object s, EventArgs e) =>
					{
						try
						{
							if (step == 0)
							{
								density = main.CurrentZoom;
								tableFont = grid.Font;
								routeFont = route.Font;
								cacheCount = ((IDictionary)Field(main, "scaledFonts")).Count;
								float num;
								using (Graphics graphics = main.CreateGraphics())
								{
									num = graphics.DpiX / 96f;
								}
								Check((double)Math.Abs(density - num * 1.375f) < 0.005, "Większy stały rozmiar uwzględnia DPI Windows i ignoruje dawną preferencję 200%.");
								Check(typeof(MainForm).GetMethod("SetZoom", BindingFlags.Instance | BindingFlags.NonPublic) == null, "Usunięto mechanizm ręcznego i testowego zoomu.");
								Check((double)Math.Abs(grid.Font.SizeInPoints - ReferenceTheme.Small.SizeInPoints) < 0.5, "Czcionka ma stały rozmiar według wzoru, bez podwójnego powiększania przez DPI.");
								main.SelectTrain(engine.State.Trains.First().Id);
								manualColumnWidth = grid.Columns["from"].Width + 17;
								grid.Columns["from"].Width = manualColumnWidth;
								elapsed.Start();
							}
							if (step < 24)
							{
								Size[] array2 = new Size[6]
								{
									new Size(800, 600),
									new Size(1259, 708),
									new Size(1920, 1040),
									new Size(1366, 768),
									new Size(1000, 650),
									new Size(1600, 900)
								};
								main.Size = array2[step % array2.Length];
								Point point = new Point(10 + step % 5 * 6, 10 + step % 3 * 5);
								main.Location = point;
								main.ApplyZoom();
								Check(main.Location == point, "Okno zachowuje nowe położenie: " + step);
								int num2 = step % 3;
								ClickTab(tabs, num2);
								if (step == 2)
								{
									manualRouteWidth = route.Columns[7].Width + 9;
									route.Columns[7].Width = manualRouteWidth;
								}
								if (step >= 2)
								{
									Check(route.Columns[7].Width == manualRouteWidth, "Zmiana okna zachowuje ręczną szerokość kolumny trasy: " + step);
								}
								Check(tabs.SelectedIndex == num2, "Kliknięcie myszą przełącza zakładkę po zmianie okna: " + step);
								Check(route.Rows.Count == ((num2 == 2) ? 120 : 0), "Trasa 120 punktów jest budowana wyłącznie przy jej oglądaniu: " + step);
								Check(object.ReferenceEquals(grid.Font, tableFont) && object.ReferenceEquals(route.Font, routeFont) && main.CurrentZoom == density, "Zmiana rozmiaru nie zmienia fontów ani ich wielkości: " + step);
								Check(grid.Columns["from"].Width == manualColumnWidth, "Zmiana okna zachowuje ręczną szerokość kolumny: " + step);
								int num3 = Math.Max(split.Panel1MinSize, Math.Min(split.Height - split.Panel2MinSize - split.SplitterWidth, 180 + step * 3));
								split.SplitterDistance = num3;
								ClickTab(tabs, (num2 + 1) % 3);
								Check(split.SplitterDistance == num3, "Przełączenie zakładki zachowuje ręcznie ustawiony podział: " + step);
								if (step == 0)
								{
									ClickTab(tabs, 1);
									Ui.Capture(main, Path.Combine(folder, "male-okno-opis.png"));
								}
								step++;
							}
							else
							{
								elapsed.Stop();
								Check(heartbeat >= 10, "Pompa komunikatów i timer pozostają aktywne podczas 24 zmian okna i 48 kliknięć.");
								Check(elapsed.Elapsed.TotalSeconds < 20.0, "Seria zmian i przełączeń kończy się bez zawieszenia.");
								Check(((IDictionary)Field(main, "scaledFonts")).Count == cacheCount, "Przeciąganie i resize nie tworzą kolejnych rozmiarów fontów.");
								main.Size = new Size(1920, 1040);
								main.Location = new Point(0, 0);
								main.ApplyZoom();
								split.SplitterDistance = (int)((double)split.Height * 0.58);
								for (int j = 0; j < tabs.TabPages.Count; j++)
								{
									ClickTab(tabs, j);
									Check(tabs.SelectedIndex == j, "Natywne kliknięcie otwiera zakładkę: " + tabs.TabPages[j].Text);
								}
								ClickTab(tabs, 2);
								Check((from DataGridViewColumn x in route.Columns
									where x.Visible
									select x).Sum((DataGridViewColumn x) => x.Width) <= route.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2, "Full HD mieści wszystkie 29 kolumn trasy, zgodnie z układem wzoru.");
								int num4 = route.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2 - route.Columns.Cast<DataGridViewColumn>().Sum((DataGridViewColumn x) => x.Width);
								Check(num4 >= 0 && num4 <= 2, "Trasa wykorzystuje szerokość Full HD bez pustego pasa i bez obcinania ostatniej kolumny.");
								ClickTab(tabs, 0);
								int num5 = grid.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2 - (from DataGridViewColumn x in grid.Columns
									where x.Visible
									select x).Sum((DataGridViewColumn x) => x.Width);
								Check(num5 >= 0 && num5 <= 2 && (float)grid.Columns["carrier"].Width > 170f * main.CurrentZoom / 1.375f, "Przewoźnik wykorzystuje wolne miejsce po prawej wykazu.");
								ClickTab(tabs, 2);
								Check(route.Font.SizeInPoints == grid.Font.SizeInPoints && route.RowTemplate.Height < grid.RowTemplate.Height, "Trasa ma zwykłą typografię i gęstsze wiersze, bez dodatkowego powiększania.");
								Check(main.CurrentZoom > 1f && grid.Font.SizeInPoints <= 9.5f, "Wymiary pól i ikon nie narzucają powiększenia tekstu całego formularza.");
								ClickTab(tabs, 0);
								Ui.Capture(main, Path.Combine(folder, "wykaz-full-hd.png"));
								ClickTab(tabs, 1);
								Ui.Capture(main, Path.Combine(folder, "opis-full-hd.png"));
								ClickTab(tabs, 2);
								Ui.Capture(main, Path.Combine(folder, "trasa-full-hd.png"));
								Train train2 = engine.State.Trains.First();
								Check(main.SelectedTrain.Id == train2.Id, "Zmiana zakładek zachowuje wybór pociągu.");
								using (SituationForm situationForm = new SituationForm(engine, density))
								{
									situationForm.Show(main);
									Label[] source = All(situationForm).OfType<Label>().ToArray();
									Check(source.Any((Label x) => x.Text.StartsWith("Status:")) && !source.Any((Label x) => x.Text.Contains("Blokada") || x.Text.Contains("kierunkowy")), "Widok sytuacji zachowuje wyłącznie dane ze wzoru.");
									Label label = source.Single((Label x) => x.Text.StartsWith("UWAGA!"));
									Check(TextRenderer.MeasureText(label.Text, label.Font, new Size(label.ClientSize.Width - label.Padding.Horizontal, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height <= label.ClientSize.Height - label.Padding.Vertical, "Pełny tekst uwagi mieści się w stopce sytuacji.");
									Ui.Capture(situationForm, Path.Combine(folder, "sytuacja.png"));
								}
								Check(!Directory.Exists(store.DirectoryPath), "Nawigacja i zmiana okna nie zapisują sesji na dysku.");
								success = true;
								finish();
							}
						}
						catch (Exception ex)
						{
							log.Add(ex.ToString());
							finish();
						}
					};
					main.Shown += (object s, EventArgs e) =>
					{
						beat.Start();
						driver.Start();
					};
					Application.Run(main);
				}
				finally
				{
					if (driver != null)
					{
						((IDisposable)driver).Dispose();
					}
				}
			}
			finally
			{
				if (beat != null)
				{
					((IDisposable)beat).Dispose();
				}
			}
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

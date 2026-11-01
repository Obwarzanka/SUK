using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace EdrLocal;

internal static class PresentationTests
{
	private static readonly List<string> log = new List<string>();

	private static object Field(object o, string key)
	{
		return o.GetType().GetField(key, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
	}

	private static void Check(bool value, string text)
	{
		if (!value)
		{
			throw new Exception(text);
		}
		log.Add("PASS: " + text);
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

	public static bool Run(string folder)
	{
		Ui.Testing = true;
		Directory.CreateDirectory(folder);
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
			main.Shown += (object sender, EventArgs args) =>
			{
				Ui.AfterFrame(() =>
				{
					try
					{
						main.MaximumSize = Size.Empty;
						main.Size = new Size(1920, 1040);
						main.ApplyZoom();
						using (LegendForm legendForm = new LegendForm())
						{
							legendForm.Show(main);
							Application.DoEvents();
							List<Label> labels = Controls(legendForm).OfType<Label>().ToList();
							List<string> list = (from x in Controls(legendForm).OfType<GroupBox>()
								select x.Text).ToList();
							Check(list.Count == 4 && list.Any((string x) => x == "Znaczenie kolorów") && list.Any((string x) => x.Contains("wstęga")) && list.Contains("Oznaczenie typu postoju"), "Legenda ma grupy kolorów, wstęg, typów postoju oraz dzienników i torów.");
							Check(Enumerable.Count(labels, (Label x) => Convert.ToString(x.Tag) == "LegendSample") == 27, "Legenda pokazuje 27 próbek, w tym potwierdzenia i cztery wstęgi.");
							Check(new string[6] { "ph", "pn", "pm", "pt", "zd", "zp" }.All((string code) => labels.Any((Label x) => x.Text.StartsWith(code + "     "))), "Legenda zawiera wszystkie sześć oznaczeń postoju.");
							Check(labels.Any((Label x) => x.Text == "Z kierunku post." && x.BackColor == EdrPalette.Dangerous) && labels.Any((Label x) => x.Text == "Odjazd rzecz." && x.BackColor == Color.Orange), "Legenda używa bieżącego koloru TN i uwzględnia STÓJ z prognozą.");
							Check(labels.All((Label x) => TextRenderer.MeasureText(x.Text, x.Font, new Size(Math.Max(1, x.Width), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height <= x.Height + 2), "Opisy legendy mieszczą się w swoich polach bez ucinania linii.");
							Check(legendForm.AcceptButton is Button && ((Button)legendForm.AcceptButton).Text == "OK", "Legenda ma przycisk OK.");
							Ui.Capture(legendForm, Path.Combine(folder, "legenda.png"));
							((Button)legendForm.AcceptButton).PerformClick();
						}
						DateTime dateTime = DateTime.Today.AddHours(8.0).AddMinutes(15.0);
						TelephoneData data = new TelephoneData
						{
							TemplateId = "21",
							SentAt = dateTime,
							EffectiveAt = dateTime.AddMinutes(-15.0),
							Track = 2,
							Receiver = "Jan Malinowski",
							Reason = "pęknięcie szyny",
							ClosureMode = ""
						};
						JournalEntry closed = engine.SaveTelephone("ZAR", null, data);
						string closeLine = TelephonePresentation.JournalLine(closed);
						Check(closeLine == "Nadał Jan Kowalski JAWORZYNA ŚLĄSKA, odebrał Jan Malinowski ŻARÓW godz. 08:15.", "Wychodzący telefonogram pokazuje nazwiska, posterunki i godzinę nadania zamiast godziny zamknięcia.");
						JournalEntry journalEntry = engine.SaveTelephone("ZAR", null, new TelephoneData
						{
							TemplateId = "22",
							Incoming = true,
							SentAt = dateTime.AddMinutes(10.0),
							EffectiveAt = dateTime.AddMinutes(10.0),
							Track = 2,
							Sender = "Jan Malinowski"
						});
						Check(TelephonePresentation.JournalLine(journalEntry) == "Nadał Jan Malinowski ŻARÓW, odebrał Jan Kowalski JAWORZYNA ŚLĄSKA godz. 08:25.", "Odebrany telefonogram poprawnie zamienia role i posterunki.");
						JournalEntry own = engine.SaveTelephone("TEL", null, new TelephoneData
						{
							TemplateId = "OWN",
							SentAt = dateTime,
							EffectiveAt = dateTime,
							Receiver = "Anna Nowak",
							FreeText = "Sprawdzono łączność telefoniczną."
						});
						main.RefreshData();
						List<JournalPane> source = (List<JournalPane>)Field(main, "panes");
						JournalPane journalPane = source.Single((JournalPane x) => x.SectionId == "ZAR");
						journalPane.SetDay(dateTime);
						journalPane.FocusEntry(closed.Id);
						Application.DoEvents();
						Check(journalPane.Grid.Rows.Cast<DataGridViewRow>().Any((DataGridViewRow x) => ((JournalEntry)x.Tag).Id == closed.Id), "Telefonogram jest widoczny w dzienniku ruchu.");
						using (Bitmap bitmap = new Bitmap(journalPane.Grid.Width, journalPane.Grid.Height))
						{
							journalPane.Grid.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
							DataGridViewRow dataGridViewRow = journalPane.Grid.Rows.Cast<DataGridViewRow>().Single((DataGridViewRow x) => ((JournalEntry)x.Tag).Id == closed.Id);
							Rectangle rowDisplayRectangle = journalPane.Grid.GetRowDisplayRectangle(dataGridViewRow.Index, cutOverflow: false);
							int num = 0;
							for (int num2 = Math.Max(0, rowDisplayRectangle.Top + rowDisplayRectangle.Height / 2); num2 < Math.Min(bitmap.Height, rowDisplayRectangle.Bottom - 1); num2++)
							{
								for (int num3 = Math.Max(0, rowDisplayRectangle.Left); num3 < Math.Min(bitmap.Width, rowDisplayRectangle.Right); num3++)
								{
									Color pixel = bitmap.GetPixel(num3, num2);
									if (pixel.R >= 100 && pixel.R <= 180 && pixel.R == pixel.G && pixel.G == pixel.B)
									{
										num++;
									}
								}
							}
							Check(num > 12, "W dolnej linii wiersza rzeczywiście rysowane są szare dane nadania i odbioru.");
						}
						Ui.Capture(main, Path.Combine(folder, "dziennik-ruchu.png"));
						JournalPane journalPane2 = source.Single((JournalPane x) => x.SectionId == "TEL");
						journalPane2.FocusEntry(own.Id);
						DataGridViewRow dataGridViewRow2 = journalPane2.Grid.Rows.Cast<DataGridViewRow>().Single((DataGridViewRow x) => ((JournalEntry)x.Tag).Id == own.Id);
						Check(Convert.ToString(dataGridViewRow2.Cells["recipient"].Value) == "Jan Kowalski / Anna Nowak", "Dziennik telefoniczny pokazuje obie osoby w kolumnie Nadał / odebrał.");
						Ui.Capture(main, Path.Combine(folder, "dziennik-telefoniczny.png"));
						using (JournalReport journalReport = new JournalReport(engine, new List<JournalEntry> { closed, journalEntry }, telephone: false, history: false, engine.Station.Sections[0], dateTime.Date, dateTime.Date.AddDays(1.0)))
						{
							Check(journalReport.Rows.SelectMany((ReportRow x) => x.Cells).Any((ReportCell x) => x.Text.Contains(closeLine)), "Wyciąg z dziennika ruchu również zawiera dane nadania i odbioru.");
						}
						int count = engine.State.History.Count;
						string text = Json.Text(engine.State.Entries);
						List<Train> trains = engine.State.Trains;
						MethodInfo method = typeof(MainForm).GetMethod("SetZoom", BindingFlags.Instance | BindingFlags.NonPublic);
						method.Invoke(main, new object[1] { 1.25f });
						Check(main.CurrentZoom == 1.25f, "Wybranie 125% od razu ustawia skalę interfejsu.");
						Check(engine.State.History.Count == count && Json.Text(engine.State.Entries) == text && object.ReferenceEquals(trains, engine.State.Trains), "Zmiana rozmiaru nie kopiuje pociągów i nie dodaje migawek operacyjnych.");
						method.Invoke(main, new object[1] { 0f });
						main.Size = new Size(1259, 708);
						Application.DoEvents();
						Check(main.CurrentZoom == 1f, "Automatyczna skala reaguje po zdarzeniu zmiany rozmiaru bez timera 160 ms.");
						Store store2 = new Store(Path.Combine(folder, "persistent"));
						store2.Load();
						string text2 = Json.Text(store2.State.Entries);
						store2.SetUiZoom(1.75f);
						Store store3 = new Store(store2.DirectoryPath);
						store3.Load();
						Check(store3.State.UiZoom == 1.75f && Json.Text(store3.State.Entries) == text2, "Tryb trwały zapisuje skalę, zachowując wpisy.");
						string text3 = File.ReadAllText(store2.FilePath);
						Directory.CreateDirectory(store2.FilePath + ".tmp");
						bool flag = false;
						try
						{
							store2.SetUiZoom(1.25f);
						}
						catch (IOException)
						{
							flag = true;
						}
						catch (UnauthorizedAccessException)
						{
							flag = true;
						}
						Check(flag && store2.State.UiZoom == 1.75f && File.ReadAllText(store2.FilePath) == text3, "Nieudany zapis preferencji przywraca poprzednią skalę i zachowuje plik bazy.");
						Directory.Delete(store2.FilePath + ".tmp");
						Check(TelephonePresentation.JournalLine(new JournalEntry
						{
							Type = "Służbowy"
						}) == "", "Wpisy służbowe nie dostają fikcyjnych danych rozmówców.");
						ok = true;
						log.Add("ALL LEGEND, PARTIES AND SCALING TESTS PASSED");
					}
					catch (Exception ex3)
					{
						log.Add(ex3.ToString());
					}
					finally
					{
						File.WriteAllLines(Path.Combine(folder, "wyniki-legenda-osoby-skala.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
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

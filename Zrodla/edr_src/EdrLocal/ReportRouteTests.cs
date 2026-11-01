using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace EdrLocal;

internal static class ReportRouteTests
{
	private static List<string> log = new List<string>();

	private static void Check(bool condition, string text)
	{
		if (!condition)
		{
			throw new Exception(text);
		}
		log.Add("PASS: " + text);
	}

	private static object Field(object owner, string key)
	{
		return owner.GetType().GetField(key, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		bool success = false;
		Store store = new Store(Path.Combine(folder, "case-" + Guid.NewGuid().ToString("N")));
		store.Load();
		Engine engine = new Engine(store)
		{
			User = store.State.Users.First((LocalUser x) => x.Id == "DEMO")
		};
		engine.SetUnitCaption("ISE KRAKÓW");
		DateTime day = DateTime.Today.AddDays(-1.0);
		engine.BeginDuty(day.AddHours(7.0));
		Section sec = engine.Station.Sections[0];
		Train train = engine.State.Trains[0];
		MainForm main = new MainForm(engine);
		try
		{
			main.Shown += (object sender, EventArgs args) =>
			{
				Ui.AfterFrame(() =>
				{
					try
					{
						typeof(MainForm).GetField("useTimeRange", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(main, false);
						JournalEntry entry = engine.Note(sec.Id, day.AddHours(8.0), "ETCS L1", "Pociąg");
						entry.TrainId = train.Id;
						entry.Number = "84108";
						entry.Direction = "Z";
						entry.Track = "1";
						entry.Permission = day.AddHours(7.0).AddMinutes(45.0);
						entry.Left = day.AddHours(7.0).AddMinutes(50.0);
						engine.SaveJournal(entry);
						entry.Track = "2";
						engine.SaveJournal(entry);
						engine.SetTrainTimes(train.Id, day.AddHours(7.0).AddMinutes(55.0), null);
						JournalEntry journalEntry = engine.State.Entries.First((JournalEntry x) => x.Id == entry.Id);
						Dictionary<string, List<ReportVersion>> dictionary = ReportHistory.Read(engine.State, new JournalEntry[1] { journalEntry });
						List<ReportVersion> list = dictionary[entry.Id];
						Check(list.Count == 3, "Historia rozróżnia trzy wersje wiersza, w tym zmianę przez operację na pociągu.");
						Check(list[0].Entry.Arrived.HasValue && !list[1].Entry.Arrived.HasValue, "Ostatnia wersja historii znajduje się na górze.");
						Check(!list[0].Previous && list[1].Previous && list[0].Changed.HasValue, "Wersja bieżąca i starsze mają właściwe znaczniki oraz rzeczywisty czas zapisu.");
						JournalEntry entry2 = engine.Note(engine.Station.Sections[1].Id, day.AddHours(8.0), "Nie ten dziennik", "Wpis");
						engine.SaveJournal(entry2);
						using (JournalReport journalReport = new JournalReport(engine, engine.State.Entries, telephone: false, history: true, sec, day, day.AddDays(1.0).AddSeconds(-1.0)))
						{
							Check(journalReport.OrganizationName == engine.OrganizationCaption && journalReport.OrganizationName == "ISE KRAKÓW", "Wyciąg ruchu pobiera jednostkę z tego samego źródła co stopka EDR.");
							Check(journalReport.Widths.Length == 13, "R-146 z historią ma kolumnę daty i godziny zmiany.");
							Check(!journalReport.Rows.Any((ReportRow r) => r.Cells.Any((ReportCell c) => c.Text.Contains("Nie ten dziennik"))), "Wyciąg obejmuje wybrany dziennik ruchu.");
							List<ReportRow> list2 = journalReport.Rows.Where((ReportRow r) => r.Cells.Any((ReportCell c) => c.Text == "84108")).ToList();
							Check(list2.Count == 3 && list2[0].Bold && !list2[1].Bold && list2[1].Previous, "Aktualny wiersz jest pogrubiony, wcześniejsze oznaczono szarym kolorem.");
							Check(list2[0].Cells.Any((ReportCell c) => c.Column == 1 && c.Text == "84108"), "Parzysty numer znajduje się we właściwej kolumnie.");
							Check(list2[0].Cells.Any((ReportCell c) => c.Column == 3 && c.Text == "07") && list2[0].Cells.Any((ReportCell c) => c.Column == 4 && c.Text == "45"), "Droga wolna zawiera godzinę pozwolenia, w rozdzielonych polach godzin i minut.");
							using (Bitmap bitmap = journalReport.RenderPage(0, 1))
							{
								bitmap.Save(Path.Combine(folder, "ruch-historia.png"), ImageFormat.Png);
							}
							journalReport.Export(Path.Combine(folder, "ruch-historia.pdf"));
							Check(new FileInfo(Path.Combine(folder, "ruch-historia.pdf")).Length > 10000, "Eksport PDF jest dostępny bez drukarki PDF.");
						}
						using (JournalReport journalReport2 = new JournalReport(engine, engine.State.Entries, telephone: false, history: false, sec, day, day.AddDays(1.0).AddSeconds(-1.0)))
						{
							Check(Enumerable.Count(journalReport2.Rows, (ReportRow r) => r.Cells.Any((ReportCell c) => c.Text == "84108")) == 1, "Wyciąg bez historii zawiera tylko aktualny wpis.");
							Check(journalReport2.Widths.Length == 12, "R-146 bez historii zachowuje osiem grup kolumn.");
							using Bitmap bitmap2 = journalReport2.RenderPage(0, 1);
							bitmap2.Save(Path.Combine(folder, "ruch.png"), ImageFormat.Png);
						}
						TelephoneData telephoneData = new TelephoneData
						{
							TemplateId = "OWN",
							SentAt = day.AddHours(8.0).AddMinutes(5.0),
							EffectiveAt = day.AddHours(8.0).AddMinutes(5.0),
							Sender = engine.User.Name,
							Receiver = "Nowak",
							FreeText = "Sprawdzenie łączności z dróżnikiem przejazdowym.\nŁączność prawidłowa.",
							LocalStation = engine.Station.Name,
							LocalParty = engine.User.Name
						};
						JournalEntry original = engine.SaveTelephone("TEL", null, telephoneData);
						telephoneData.Receiver = "Nowakowski";
						engine.SaveTelephone("TEL", original, telephoneData);
						using (JournalReport journalReport3 = new JournalReport(engine, engine.State.Entries, telephone: true, history: true, null, day, day.AddDays(1.0).AddSeconds(-1.0)))
						{
							Check(journalReport3.OrganizationName == "ISE KRAKÓW", "Wyciąg telefoniczny także używa wybranej jednostki.");
							Check(journalReport3.Telephone && journalReport3.Widths.Length == 7, "Dziennik telefoniczny ma odrębną tabelę i kolumnę historii.");
							Check(journalReport3.Rows.Any((ReportRow r) => r.Cells.Any((ReportCell c) => c.Column == 5 && c.Text.Contains("Nowakowski"))) && journalReport3.Rows.Any((ReportRow r) => r.Previous && r.Cells.Any((ReportCell c) => c.Text.Contains("Nowak"))), "Telefoniczny wyciąg pokazuje obu rozmówców oraz historię ich zmiany.");
							Check(!journalReport3.Rows.Any((ReportRow r) => r.Cells.Any((ReportCell c) => c.Text == "84108")), "Telefoniczny wyciąg nie zawiera wierszy pociągu z dziennika ruchu.");
							using Bitmap bitmap3 = journalReport3.RenderPage(0, 1);
							bitmap3.Save(Path.Combine(folder, "telefoniczny-historia.png"), ImageFormat.Png);
						}
						using (JournalReport journalReport4 = new JournalReport(engine, engine.State.Entries, telephone: true, history: false, null, day, day.AddDays(1.0).AddSeconds(-1.0)))
						{
							using Bitmap bitmap4 = journalReport4.RenderPage(0, 1);
							bitmap4.Save(Path.Combine(folder, "telefoniczny.png"), ImageFormat.Png);
						}
						JournalEntry journalEntry2 = Json.Clone(journalEntry);
						journalEntry2.Id = "long";
						journalEntry2.Text = string.Join("\n", from i in Enumerable.Range(1, 230)
							select "Wiersz telefonogramu " + i + ": zapis treści bez utraty końcowych fragmentów.");
						journalEntry2.Type = "Wpis";
						List<JournalEntry> list3 = (from i in Enumerable.Range(1, 140)
							select new JournalEntry
							{
								Id = "many" + i,
								StationId = engine.StationId,
								SectionId = sec.Id,
								Type = "Pociąg",
								Number = (10000 + i).ToString(),
								Time = day.AddHours(8.0).AddMinutes(i),
								Track = "1",
								Left = day.AddHours(8.0).AddMinutes(i)
							}).ToList();
						list3.Insert(0, journalEntry2);
						using (JournalReport journalReport5 = new JournalReport(engine, list3, telephone: false, history: false, sec, day, day.AddDays(1.0)))
						{
							Check(journalReport5.PageCount >= 7, "Długie wpisy i wiele pociągów są dzielone na kolejne strony.");
							List<List<ReportDrawRow>> source = (List<List<ReportDrawRow>>)Field(journalReport5, "pages");
							List<string> allLines = source.SelectMany((List<ReportDrawRow> x) => x).SelectMany((ReportDrawRow x) => x.Lines).SelectMany((string[] x) => x)
								.ToList();
							Check(Enumerable.Range(1, 230).All((int i) => allLines.Any((string x) => x.StartsWith("Wiersz telefonogramu " + i + ":"))), "Podział długiego wpisu zachowuje wszystkie 230 wierszy treści.");
							Check(allLines.Contains("10140"), "Ostatni z 140 pociągów trafia na stronę raportu.");
							journalReport5.Export(Path.Combine(folder, "wiele-stron.pdf"));
							for (int num = 0; num < journalReport5.PageCount; num++)
							{
								using Bitmap bitmap5 = journalReport5.RenderPage(num, 1);
								bitmap5.Save(Path.Combine(folder, "strona-" + (num + 1) + ".png"), ImageFormat.Png);
							}
							using PrintDocument printDocument = journalReport5.Printable();
							Check(!printDocument.DefaultPageSettings.Landscape && printDocument.DefaultPageSettings.PaperSize.Width == 827, "Dokument do drukowania ma pionowy format A4.");
						}
						using (JournalReport journalReport6 = new JournalReport(engine, new List<JournalEntry>(), telephone: false, history: false, sec, day, day.AddDays(1.0)))
						{
							Check(journalReport6.PageCount == 1 && journalReport6.Rows.Any((ReportRow r) => r.Cells.Any((ReportCell c) => c.Text.Contains("Brak wpisów"))), "Pusty zakres daje poprawny wyciąg z informacją o braku wpisów.");
						}
						using (JournalReport journalReport7 = new JournalReport(engine, new List<JournalEntry>(), telephone: false, history: false, sec, day, day.AddDays(1.0)))
						{
							engine.SetUnitCaption("ISE WARSZAWA");
							Check(journalReport7.OrganizationName == "ISE KRAKÓW", "Gotowy wyciąg zachowuje jednostkę z chwili sporządzenia.");
							using JournalReport journalReport8 = new JournalReport(engine, new List<JournalEntry>(), telephone: false, history: false, sec, day, day.AddDays(1.0));
							Check(journalReport8.OrganizationName == "ISE WARSZAWA", "Ponowne sporządzenie wyciągu uwzględnia zmianę jednostki.");
						}
						engine.SetUnitCaption("ISE " + new string('W', 116));
						using (JournalReport journalReport9 = new JournalReport(engine, list3, telephone: false, history: false, sec, day, day.AddDays(1.0)))
						{
							Check(journalReport9.OrganizationName.Length == 120, "Wyciąg zachowuje pełną dozwoloną nazwę jednostki.");
							using Bitmap bitmap6 = journalReport9.RenderPage(journalReport9.PageCount - 1, 1);
							bitmap6.Save(Path.Combine(folder, "dluga-jednostka.png"), ImageFormat.Png);
						}
						engine.SetUnitCaption("ISE KRAKÓW");
						engine.State.Trains[0].Runs = false;
						engine.State.Trains[0].PlanArrival = day.AddHours(8.0);
						engine.State.Trains[0].PlanDeparture = day.AddHours(8.0).AddMinutes(5.0);
						engine.State.Trains[0].ReadyReported = day.AddHours(7.0);
						engine.State.Trains[0].ReadyRecordedAt = day.AddHours(7.0).AddMinutes(1.0);
						engine.State.Trains[0].ReadyDriver = "Nowak";
						engine.State.Trains[0].Consist = new TrainConsist
						{
							PlannedSeries = new string[1] { "ET22" },
							PlannedGross = 1319m,
							PlannedLength = 399m,
							Gross = 1300m,
							Length = 397m,
							Series = new string[1] { "ET22" },
							Numbers = new string[1] { "1102" }
						};
						main.RefreshData();
						main.SelectTrain(train.Id);
						Application.DoEvents();
						DataGridView dataGridView = (DataGridView)Field(main, "trains");
						DataGridViewRow dataGridViewRow = dataGridView.Rows.Cast<DataGridViewRow>().First((DataGridViewRow x) => Convert.ToString(x.Tag) == train.Id);
						Check(dataGridViewRow.DefaultCellStyle.ForeColor == Color.Black, "Niekursujący pociąg ma czarny tekst, bez wyszarzania.");
						TabControl tabControl = (TabControl)Field(main, "topTabs");
						tabControl.SelectedIndex = 2;
						Application.DoEvents();
						Dictionary<string, DataGridView> dictionary2 = (Dictionary<string, DataGridView>)Field(main, "detailGrids");
						DataGridView dataGridView2 = dictionary2["route"];
						Check(dataGridView2.Columns.Count == 29, "Trasa ma oddzielne daty i godziny oraz pola gotowości, maszynisty i składu.");
						Check(dataGridView2.DefaultCellStyle.BackColor == EdrPalette.Cream && dataGridView2.DefaultCellStyle.SelectionBackColor == Color.FromArgb(51, 153, 255), "Trasa ma kremowe wiersze i niebieskie zaznaczenie zgodnie z wzorem ERJ.");
						DataGridViewRow dataGridViewRow2 = dataGridView2.Rows.Cast<DataGridViewRow>().First((DataGridViewRow x) => Convert.ToString(x.Cells[7].Value) == engine.Station.Name);
						Check(Convert.ToString(dataGridViewRow2.Cells[0].Value) == day.ToString("dd-MM-yy") && Convert.ToString(dataGridViewRow2.Cells[1].Value) == "08:00", "Data planowego przyjazdu i godzina są w osobnych kolumnach.");
						Check(Convert.ToString(dataGridViewRow2.Cells[12].Value) == "I " + train.Track, "Trasa pokazuje peron I i nie zmienia numeru toru.");
						Check(Convert.ToString(dataGridViewRow2.Cells[21].Value).Contains("07:01") && Convert.ToString(dataGridViewRow2.Cells[22].Value) == "Nowak", "Czas wpisu gotowości i nazwisko maszynisty trafiają do swoich kolumn.");
						Check(Convert.ToString(dataGridViewRow2.Cells[23].Value) == "ET22" && Convert.ToString(dataGridViewRow2.Cells[26].Value) == "ET22-1102", "Planowa i rzeczywista seria lokomotywy pozostają oddzielne.");
						Check(dataGridViewRow2.Cells[4].Style.BackColor == Color.Green, "Potwierdzony przyjazd ma zielone pole opóźnienia.");
						tabControl.SelectedTab = tabControl.TabPages.Cast<TabPage>().First((TabPage x) => x.Text == "Trasa pociągu");
						main.Refresh();
						Ui.Capture(main, Path.Combine(folder, "trasa.png"));
						DetailRow detailRow = new DetailRow
						{
							Cells = new string[19]
							{
								"18.10.2026 08:00", "18.10.2026 08:05", "5", "✓", "17", "Stara stacja", "0,0", "0,0", "ph", "I",
								"2", "18.10.2026 08:01", "18.10.2026 08:06", "5", "ROJ", "12345", "18.10.2026 07:59", "1000", "300"
							}
						};
						MethodInfo method = typeof(MainForm).GetMethod("RouteCells", BindingFlags.Instance | BindingFlags.NonPublic);
						object[] array = (object[])method.Invoke(main, new object[1] { detailRow });
						Check(Convert.ToString(array[7]) == "Stara stacja" && Convert.ToString(array[19]) == "12345" && Convert.ToString(array[28]) == "1000", "Dawne lokalne dane trasy zachowują właściwe znaczenie po zmianie kolumn.");
						Check(TelephoneTemplates.Titles.All((string x) => !x.Contains("—") && !x.Contains("–")), "Nazwy telefonogramów zawierają zwykły myślnik.");
						using (JournalReportForm journalReportForm = new JournalReportForm(engine, tel: false, day))
						{
							journalReportForm.Show(main);
							journalReportForm.ShowReport();
							Application.DoEvents();
							Ui.Capture(journalReportForm, Path.Combine(folder, "podglad-ruch.png"));
							ReportCanvas reportCanvas = (ReportCanvas)Field(journalReportForm, "canvas");
							Check(reportCanvas.Report != null && reportCanvas.Report.PageCount > 0, "Przycisk Pokaż uruchamia podgląd wewnątrz programu.");
							((CheckBox)Field(journalReportForm, "history")).Checked = true;
							journalReportForm.ShowReport();
							Application.DoEvents();
							Check(reportCanvas.Report.WithHistory, "Ponowne Pokaż przełącza podgląd na wariant z historią.");
							Ui.Capture(journalReportForm, Path.Combine(folder, "podglad-ruch-historia.png"));
							reportCanvas.Zoom(2f);
							Check(reportCanvas.AutoScrollMinSize.Width > reportCanvas.ClientSize.Width, "Powiększenie podglądu umożliwia przewijanie całej strony.");
							journalReportForm.Close();
						}
						using (JournalReportForm journalReportForm2 = new JournalReportForm(engine, tel: true, day))
						{
							journalReportForm2.Show(main);
							journalReportForm2.ShowReport();
							Application.DoEvents();
							Ui.Capture(journalReportForm2, Path.Combine(folder, "podglad-telefoniczny.png"));
							Check(!((ComboBox)Field(journalReportForm2, "section")).Visible, "Telefoniczny wyciąg nie ma wyboru dziennika szlakowego.");
							journalReportForm2.Close();
						}
						success = true;
						log.Add("ALL REPORT AND ROUTE TESTS PASSED");
					}
					catch (Exception ex)
					{
						log.Add(ex.ToString());
					}
					finally
					{
						File.WriteAllLines(Path.Combine(folder, "wyniki-raporty-trasa.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
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

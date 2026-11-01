using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace EdrLocal;

internal static class PopupTests
{
	private static readonly List<string> log = new List<string>();

	private static object Field(object o, string name)
	{
		return o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
	}

	private static void Check(bool condition, string message)
	{
		if (!condition)
		{
			throw new Exception(message);
		}
		log.Add("PASS: " + message);
	}

	private static void Open(CompactDatePicker date)
	{
		typeof(CompactDatePicker).GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(date, new object[1]
		{
			new MouseEventArgs(MouseButtons.Left, 1, 4, 4, 0)
		});
		Application.DoEvents();
	}

	private static void Select(MonthCalendar calendar, DateTime day)
	{
		typeof(MonthCalendar).GetMethod("OnDateSelected", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(calendar, new object[1]
		{
			new DateRangeEventArgs(day, day)
		});
		Application.DoEvents();
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
		Exception asyncError = null;
		ThreadExceptionEventHandler onError = (object s, ThreadExceptionEventArgs e) =>
		{
			asyncError = e.Exception;
		};
		Application.ThreadException += onError;
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
						typeof(MainForm).GetField("useTimeRange", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(main, false);
						main.RefreshData();
						TelephoneEditor phone = new TelephoneEditor(engine, "TEL", null);
						try
						{
							phone.Show(main);
							CompactDatePicker compactDatePicker = (CompactDatePicker)Field(phone, "date");
							Open(compactDatePicker);
							ToolStripDropDown popup = (ToolStripDropDown)Field(compactDatePicker, "calendarPopup");
							MonthCalendar monthCalendar = (MonthCalendar)Field(compactDatePicker, "calendar");
							Check(popup.Visible && monthCalendar.IsHandleCreated, "Kalendarz otwiera rzeczywiste okno w formularzu dziennika telefonicznego.");
							bool aliveDuringClose = false;
							popup.Closed += (object a, ToolStripDropDownClosedEventArgs b) =>
							{
								aliveDuringClose = !popup.IsDisposed;
							};
							for (int num = 0; num < 40; num++)
							{
								if (!popup.Visible)
								{
									Open(compactDatePicker);
								}
								DateTime dateTime = DateTime.Today.AddDays(num % 3);
								Select(monthCalendar, dateTime);
								if (popup.Visible || compactDatePicker.Value != dateTime || popup.IsDisposed)
								{
									throw new Exception("Niepoprawny wybór daty w cyklu " + num);
								}
								phone.FreeBox.Text = "Treść po wyborze daty " + num;
								phone.FreeBox.Focus();
								Application.DoEvents();
								Open(compactDatePicker);
								typeof(ToolStripDropDown).GetMethod("ProcessDialogKey", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(popup, new object[1] { Keys.Escape });
								Application.DoEvents();
								if (popup.Visible || popup.IsDisposed)
								{
									throw new Exception("Niepoprawne zamknięcie Escape w cyklu " + num);
								}
								Open(compactDatePicker);
								popup.Close(ToolStripDropDownCloseReason.AppClicked);
								Application.DoEvents();
							}
							Check(aliveDuringClose && asyncError == null, "40 cykli: wybór daty, wpisywanie treści, Escape i kliknięcie poza kalendarzem bez błędu usuniętego obiektu.");
							Check(object.ReferenceEquals(popup, Field(compactDatePicker, "calendarPopup")), "Ponowne otwarcie używa tego samego aktywnego kalendarza.");
							compactDatePicker.Value = DateTime.Today;
							Open(compactDatePicker);
							Check(monthCalendar.SelectionStart.Date == DateTime.Today, "Ponowne otwarcie synchronizuje kalendarz z datą pola.");
							popup.Close();
							phone.FreeBox.Text = "Własny telefonogram po wielokrotnym użyciu kalendarza";
							phone.ReceiverBox.Text = "Jan Nowak";
							((Button)phone.Buttons.Controls[1]).PerformClick();
							Application.DoEvents();
							Check(phone.SavedEntry != null && engine.State.Entries.Any((JournalEntry x) => x.Id == phone.SavedEntry.Id && x.Text.Contains("Własny telefonogram")), "Po użyciu kalendarza wpis telefoniczny zapisuje się poprawnie.");
							phone.Dispose();
							Check(popup.IsDisposed && monthCalendar.IsDisposed, "Zamknięcie formularza zwalnia kalendarz i jego okno.");
						}
						finally
						{
							if (phone != null)
							{
								((IDisposable)phone).Dispose();
							}
						}
						using (Form form = new Form())
						{
							CompactDatePicker compactDatePicker2 = new CompactDatePicker();
							form.Controls.Add(compactDatePicker2);
							form.Show(main);
							Open(compactDatePicker2);
							ToolStripDropDown toolStripDropDown = (ToolStripDropDown)Field(compactDatePicker2, "calendarPopup");
							form.Dispose();
							Application.DoEvents();
							Check(toolStripDropDown.IsDisposed && !toolStripDropDown.IsHandleCreated, "Zamknięcie właściciela z otwartym kalendarzem zwalnia okno.");
						}
						Form owner = new Form();
						try
						{
							CompactDatePicker compactDatePicker3 = new CompactDatePicker();
							owner.Controls.Add(compactDatePicker3);
							owner.Show(main);
							Open(compactDatePicker3);
							ToolStripDropDown toolStripDropDown2 = (ToolStripDropDown)Field(compactDatePicker3, "calendarPopup");
							MonthCalendar calendar = (MonthCalendar)Field(compactDatePicker3, "calendar");
							compactDatePicker3.ValueChanged += (object a, EventArgs b) =>
							{
								owner.Dispose();
							};
							Select(calendar, DateTime.Today.AddDays(1.0));
							Check(owner.IsDisposed && toolStripDropDown2.IsDisposed, "Zmiana daty może zamknąć formularz bez odwołania do usuniętego okna.");
						}
						finally
						{
							if (owner != null)
							{
								((IDisposable)owner).Dispose();
							}
						}
						LinkLabel linkLabel = (LinkLabel)Field(main, "confirmationLink");
						MethodInfo method = typeof(MainForm).GetMethod("ShowFilterMenu", BindingFlags.Instance | BindingFlags.NonPublic);
						Action<int> action = (int x) =>
						{
						};
						for (int num2 = 0; num2 < 20; num2++)
						{
							method.Invoke(main, new object[4]
							{
								linkLabel,
								new string[2] { "wszystkie", "potwierdzone" },
								action,
								"Potwierdzenie: "
							});
							ContextMenuStrip contextMenuStrip = linkLabel.ContextMenuStrip;
							contextMenuStrip.Items[num2 % 2].PerformClick();
							if (contextMenuStrip.Visible)
							{
								contextMenuStrip.Close();
							}
							Application.DoEvents();
							if (contextMenuStrip.IsDisposed)
							{
								throw new Exception("Filtr usunął menu podczas zamykania.");
							}
						}
						Check(asyncError == null, "20 wyborów filtra bez usuwania menu w zdarzeniu Closed.");
						ContextMenuStrip contextMenuStrip2 = linkLabel.ContextMenuStrip;
						Train named = engine.State.Trains.Single((Train x) => x.Number == "70413");
						main.SelectTrain(named.Id);
						Application.DoEvents();
						AdditionalTrainInfo additionalTrainInfo = (AdditionalTrainInfo)Field(main, "filterInfo");
						Check(additionalTrainInfo.Text.Contains("N:   Janiczyk") && additionalTrainInfo.FontForLine("N:   Janiczyk").Bold, "Informacje dodatkowe pokazują pogrubioną nazwę Janiczyk.");
						Check(additionalTrainInfo.PrefixFont.Bold && !additionalTrainInfo.FontForLine("Z:   Wrocław Główny").Bold && !additionalTrainInfo.FontForLine("Do: Jelenia Góra").Bold && !additionalTrainInfo.FontForLine("P:   Polregio").Bold, "Etykiety Z:, Do:, P: są pogrubione, a treść relacji i przewoźnika zachowuje zwykłą grubość pisma.");
						Font font = additionalTrainInfo.Font;
						using (Font font2 = new Font(additionalTrainInfo.Font.FontFamily, additionalTrainInfo.Font.Size + 3f))
						{
							additionalTrainInfo.Font = font2;
							Check(additionalTrainInfo.FontForLine("N:   Janiczyk").Bold && (double)Math.Abs(additionalTrainInfo.FontForLine("N:   Janiczyk").Size - font2.Size) < 0.01, "Pogrubienie dopasowuje się do zmiany skali czcionki.");
							additionalTrainInfo.Font = font;
						}
						main.ApplyZoom();
						DataGridView dataGridView = (DataGridView)Field(main, "trains");
						dataGridView.FirstDisplayedScrollingRowIndex = dataGridView.CurrentRow.Index;
						Ui.Capture(main, Path.Combine(folder, "nazwa-i-stopka.png"));
						TabControl tabControl = (TabControl)Field(main, "topTabs");
						Control control = (Control)Field(main, "headerClock");
						Check(control.Visible && control.Top == 0 && control.Right == main.ClientSize.Width, "Zegar z licznikiem przylega do górnej i prawej krawędzi obszaru pod paskiem tytułu.");
						bool flag = true;
						for (int num3 = 1; num3 < tabControl.TabCount; num3++)
						{
							tabControl.SelectedIndex = num3;
							Application.DoEvents();
							flag &= !control.Visible;
							if (num3 == 2)
							{
								Ui.Capture(main, Path.Combine(folder, "trasa-bez-zegara.png"));
							}
						}
						Check(flag, "Zegar i licznik są ukryte we wszystkich zakładkach poza wykazem pociągów.");
						tabControl.SelectedIndex = 0;
						Application.DoEvents();
						Check(control.Visible, "Powrót do wykazu ponownie pokazuje zegar i licznik.");
						string[] roman = new string[3] { "I", "II", "III" };
						Check(dataGridView.Rows.Cast<DataGridViewRow>().All((DataGridViewRow trainRow) =>
						{
							Train train2 = engine.State.Trains.First((Train x) => x.Id == Convert.ToString(trainRow.Tag));
							return Convert.ToString(trainRow.Cells["pt"].Value) == roman[int.Parse(train2.Platform) - 1] + " " + train2.Track;
						}), "Wykaz wyświetla perony I/II/III, zachowując arabskie numery torów.");
						Check(engine.State.Trains.All((Train x) => string.IsNullOrEmpty(x.Notes)), "Nowa sesja nie dopisuje przykładowych uwag do pociągów.");
						StatusStrip statusStrip = Controls(main).OfType<StatusStrip>().Single();
						Check(statusStrip.Items.Count == 4 && statusStrip.Items.Cast<ToolStripItem>().All((ToolStripItem x) => !x.Text.Contains("Dane ćwiczeniowe") && !x.Text.Contains("Przyjazd nie")), "Stopka zawiera użytkownika, klikalną jednostkę, zalogowany od i tryb pracy.");
						ToolStripStatusLabel toolStripStatusLabel = (ToolStripStatusLabel)Field(main, "statusLogin");
						string text = toolStripStatusLabel.Text;
						main.QuickRegister(named, "ZAR");
						JournalPane journalPane = ((List<JournalPane>)Field(main, "panes")).Single((JournalPane x) => x.SectionId == "ZAR");
						DataGridViewRow dataGridViewRow = journalPane.Grid.Rows.Cast<DataGridViewRow>().Single((DataGridViewRow x) => ((JournalEntry)x.Tag).TrainId == named.Id);
						journalPane.Grid.EndEdit();
						journalPane.CommitPending();
						string[][] array = new string[2][]
						{
							new string[2] { "left", "12:00" },
							new string[2] { "arrived", "12:05" }
						};
						foreach (string[] array2 in array)
						{
							journalPane.Grid.CurrentCell = dataGridViewRow.Cells[array2[0]];
							journalPane.Grid.BeginEdit(selectAll: true);
							((TextBox)journalPane.Grid.EditingControl).Text = array2[1];
							journalPane.Grid.EndEdit();
							if (!journalPane.CommitPending())
							{
								throw new Exception(journalPane.LastEditError);
							}
						}
						JournalEntry en = Json.Clone((JournalEntry)dataGridViewRow.Tag);
						journalPane.Grid.CurrentCell = dataGridViewRow.Cells["arrived"];
						journalPane.Grid.BeginEdit(selectAll: true);
						((TextBox)journalPane.Grid.EditingControl).Text = "11:59";
						journalPane.Grid.EndEdit();
						Check(!journalPane.CommitPending() && engine.State.Entries.Single((JournalEntry x) => x.Id == en.Id).Arrived == en.Arrived, "Błędna chronologia nadal blokuje zapis i pozostawia poprawne dane.");
						Check(!string.IsNullOrEmpty(dataGridViewRow.ErrorText) || !string.IsNullOrEmpty(dataGridViewRow.Cells["arrived"].ErrorText), "Błąd pozostaje przy edytowanym wpisie.");
						Check(toolStripStatusLabel.Text == text && statusStrip.Items.Cast<ToolStripItem>().All((ToolStripItem x) => !x.Text.Contains("Przyjazd nie") && !x.Text.Contains("wpisz godziny")), "Błąd i instrukcja wpisywania nie trafiają do stopki; godzina logowania pozostaje.");
						journalPane.DiscardPending();
						journalPane.RefreshData();
						engine.Change("Test uwagi", named.Id, (AppState s) =>
						{
							s.Trains.First((Train x) => x.Id == named.Id).Notes = "Moja własna uwaga";
						});
						main.RefreshData();
						Check(engine.State.Trains.First((Train x) => x.Id == named.Id).Notes == "Moja własna uwaga", "Odświeżenie nie usuwa własnych uwag użytkownika.");
						Train train = engine.State.Trains[5];
						string id = train.Id;
						engine.Change("Test STÓJ", id, (AppState s) =>
						{
							s.Trains.First((Train x) => x.Id == id).Stopped = true;
						});
						JournalEntry journalEntry = engine.Note("ZAR", DateTime.Now, "", "Pociąg");
						journalEntry.TrainId = id;
						journalEntry.Number = train.Number;
						journalEntry.Direction = "DO";
						engine.SaveJournal(journalEntry);
						Check(engine.State.Trains.First((Train x) => x.Id == id).Stopped, "Pusty wpis wyprawienia nie zdejmuje STÓJ.");
						engine.Change("Prognoza", id, (AppState s) =>
						{
							s.Trains.First((Train x) => x.Id == id).Forecast = DateTime.Now.AddMinutes(10.0);
						});
						Check(engine.State.Trains.First((Train x) => x.Id == id).Stopped, "Sama prognoza zachowuje STÓJ.");
						journalEntry.Left = DateTime.Now;
						engine.SaveJournal(journalEntry);
						Check(!engine.State.Trains.First((Train x) => x.Id == id).Stopped, "Potwierdzony odjazd w dzienniku automatycznie zdejmuje STÓJ (ERJ s.21).");
						engine.Change("Test STÓJ", named.Id, (AppState s) =>
						{
							s.Trains.First((Train x) => x.Id == named.Id).Stopped = true;
						});
						engine.SetTrainTimes(named.Id, en.Arrived, en.Arrived.Value.AddMinutes(2.0));
						Check(!engine.State.Trains.First((Train x) => x.Id == named.Id).Stopped, "Potwierdzony odjazd w oknie godzin także zdejmuje STÓJ.");
						Check(asyncError == null, "Brak nieobsłużonych wyjątków WinForms w całym przebiegu.");
						main.Dispose();
						Check(contextMenuStrip2.IsDisposed, "Menu filtrów jest zwalniane z właścicielem.");
						ok = true;
						log.Add("ALL POPUP AND CLEAN INTERFACE TESTS PASSED");
					}
					catch (Exception ex)
					{
						log.Add(ex.ToString());
					}
					finally
					{
						Application.ThreadException -= onError;
						File.WriteAllLines(Path.Combine(folder, "wyniki-okna-stopka.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
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

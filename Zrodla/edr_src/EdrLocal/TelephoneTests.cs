using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace EdrLocal;

internal static class TelephoneTests
{
	private static List<string> log = new List<string>();

	private static void Check(bool ok, string label)
	{
		if (!ok)
		{
			throw new Exception("FAIL: " + label);
		}
		log.Add("PASS: " + label);
	}

	private static void Reject(Action action, string label)
	{
		try
		{
			action();
		}
		catch
		{
			Check(ok: true, label);
			return;
		}
		Check(ok: false, label);
	}

	private static object Field(object x, string name)
	{
		return x.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(x);
	}

	private static TelephoneData Data(string id)
	{
		TelephoneData telephoneData = new TelephoneData();
		telephoneData.TemplateId = id;
		telephoneData.Track = 1;
		telephoneData.SentAt = DateTime.Now;
		telephoneData.EffectiveAt = DateTime.Now.AddMinutes(-2.0);
		telephoneData.Sender = "Fałszywy nadawca";
		telephoneData.Receiver = "Jan Malinowski";
		telephoneData.LocalStation = "ZMIANA STACJI";
		telephoneData.NeighbourStation = "INNY SĄSIAD";
		telephoneData.Reason = "roboty torowe";
		telephoneData.TrainNumber = "12345";
		telephoneData.ClosureMode = "planowo";
		telephoneData.Notice = "Potwierdzenie zakończenia robót.";
		telephoneData.AdditionalNote = "Dodatkowa informacja.";
		telephoneData.FreeText = "Własna rozmowa.";
		return telephoneData;
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
		Store store = new Store(Path.Combine(target, "dane"));
		store.Load();
		Engine e = new Engine(store);
		e.StartSession(store.State.Users.First((LocalUser x) => x.Id == "DEMO"));
		e.BeginDuty(DateTime.Now.AddMinutes(-5.0));
		MainForm main = new MainForm(e);
		try
		{
			main.Shown += (object s, EventArgs a) =>
			{
				Ui.AfterFrame(() =>
				{
					try
					{
						string sec = e.Station.Sections[0].Id;
						JournalEntry journalEntry = e.SaveTelephone(sec, null, Data("21"));
						Check(journalEntry.Text.Contains("JAWORZYNA ŚLĄSKA") && journalEntry.Text.Contains("ŻARÓW") && !journalEntry.Text.Contains("ZMIANA STACJI"), "Nazwy stacji pochodzą z konfiguracji, nie z edytowanego tekstu.");
						Check(journalEntry.Telephone.Sender == e.User.Name && journalEntry.Recipient == "Jan Malinowski", "Nadawca lokalny i rozmówca są zapisani poprawnie.");
						Check(e.LineStatus(sec, 1) == "ZAMKNIĘTY" && journalEntry.Text.Contains("planowo"), "Wzór 21 zapisuje zamknięcie i jego rodzaj.");
						TelephoneData telephoneData = Json.Clone(journalEntry.Telephone);
						telephoneData.Reason = "przegląd";
						telephoneData.LocalStation = "PODMIANA";
						JournalEntry saved = e.SaveTelephone(sec, journalEntry, telephoneData);
						Check(saved.Text.Contains("przegląd") && !saved.Text.Contains("PODMIANA"), "Edycja zmienia parametr, zachowując stałe stacje.");
						JournalEntry forged = Json.Clone(saved);
						forged.Text = "Dowolna zmieniona treść";
						e.SaveJournal(forged);
						Check(!e.State.Entries.First((JournalEntry x) => x.Id == saved.Id).Text.Contains("Dowolna"), "Zapis odtwarza stały wzór i nie przyjmuje dowolnej treści.");
						forged.Telephone = null;
						Reject(() =>
						{
							e.SaveJournal(forged);
						}, "Nie można usunąć parametrów zapisanego wzoru.");
						TelephoneData changed = Data("22");
						Reject(() =>
						{
							e.SaveTelephone(sec, saved, changed);
						}, "Nie można zamienić wzoru istniejącego telefonogramu.");
						Reject(() =>
						{
							e.SaveTelephone(sec, null, Data("OWN"));
						}, "Wpis własny nie występuje w katalogu dziennika ruchu.");
						Reject(() =>
						{
							e.SaveTelephone("TEL", null, Data("21"));
						}, "Dziennik telefoniczny nie przyjmuje wzoru ruchowego.");
						TelephoneData unknown = Data("1a");
						Reject(() =>
						{
							e.SaveTelephone(sec, null, unknown);
						}, "Wzór zapowiadania konkretnego pociągu nie trafia do listy okna.");
						TelephoneData telephoneData2 = Data("22");
						telephoneData2.Incoming = true;
						telephoneData2.Sender = "Jan Malinowski";
						JournalEntry journalEntry2 = e.SaveTelephone(sec, null, telephoneData2);
						Check(journalEntry2.Text.Contains("od ŻARÓW do JAWORZYNA ŚLĄSKA") && journalEntry2.Telephone.Receiver == e.User.Name, "Odbierany telefonogram ma prawidłowy kierunek i lokalnego odbiorcę.");
						Check(e.LineStatus(sec, 1) == "WOLNY", "Wzór 22 otwiera tor.");
						e.SaveTelephone(sec, null, Data("16"));
						Check(e.CurrentBasis(sec, 1) == "Zapowiadanie telefoniczne", "Wzór 16 zmienia podstawę prowadzenia ruchu.");
						e.SaveTelephone(sec, null, Data("17"));
						Check(e.CurrentBasis(sec, 1) == e.Station.Sections[0].Basis, "Wzór 17 przywraca normalną podstawę.");
						string[] array = new string[3] { "28", "29", "30" };
						foreach (string text in array)
						{
							Check(e.SaveTelephone(sec, null, Data(text)).Text.Length > 40, "Zapis potwierdzonego wzoru " + text);
						}
						TelephoneData telephoneData3 = Data("OWN");
						telephoneData3.FreeText = "Tor numer 1 zamknięty - notatka z rozmowy, bez skutku dla szlaku.";
						JournalEntry journalEntry3 = e.SaveTelephone("TEL", null, telephoneData3);
						Check(journalEntry3.Text.Contains(telephoneData3.FreeText) && journalEntry3.Effect == "" && e.LineStatus(sec, 1) == "WOLNY", "Własny wpis telefoniczny nie zmienia stanu toru.");
						TelephoneData telephoneData4 = Data("21");
						telephoneData4.EffectiveAt = DateTime.Now.AddHours(1.0);
						e.SaveTelephone(sec, null, telephoneData4);
						Check(e.LineStatus(sec, 1) == "WOLNY", "Zamknięcie od przyszłej godziny nie działa przed jej nadejściem.");
						Store store2 = new Store(store.DirectoryPath);
						store2.Load();
						Check(store2.State.Entries.First((JournalEntry x) => x.Id == saved.Id).Telephone.TemplateId == "21", "Parametry wzoru przetrwały zapis i ponowne wczytanie.");
						string[] ids = TelephoneTemplates.Ids;
						foreach (string id in ids)
						{
							TelephoneEditor editor = new TelephoneEditor(e, sec, null);
							try
							{
								editor.TemplateBox.SelectedIndex = Array.IndexOf(TelephoneTemplates.Ids, id);
								editor.ReceiverBox.Text = "Jan Malinowski";
								editor.ReasonBox.Text = "roboty torowe";
								editor.TrainBox.Text = "12345";
								editor.PassingTrainBox.Text = "12346";
								editor.WorkNameBox.Text = "Jan Nowak";
								editor.WorkRoleBox.Text = "kierownik robót";
								editor.SpeedBox.Text = "Brak";
								editor.Shown += (object sender, EventArgs args) =>
								{
									Ui.AfterFrame(() =>
									{
										try
										{
											Ui.Capture(editor, Path.Combine(target, "wzor-" + id + ".png"));
											FlowLayoutPanel sentence = All(editor).OfType<FlowLayoutPanel>().First((FlowLayoutPanel p) => p.BackColor == Ui.Cream);
											Check(sentence.Controls.Cast<Control>().All((Control c) => c.Bottom <= sentence.ClientSize.Height && c.Right <= sentence.ClientSize.Width), "Cały szablon mieści się w formularzu: " + id);
											Check((from Control c in sentence.Controls
												where Convert.ToString(c.Tag) == "FixedStation"
												select c).All((Control c) => c is Label), "Stacje są nieedytowalnymi polami: " + id);
											((Button)editor.Buttons.Controls[1]).PerformClick();
										}
										catch (Exception ex2)
										{
											log.Add(ex2.ToString());
											editor.DialogResult = DialogResult.Abort;
											editor.Close();
										}
									});
								};
								Check(editor.ShowDialog(main) == DialogResult.OK, "Zapis formularza wzoru " + id);
							}
							finally
							{
								if (editor != null)
								{
									((IDisposable)editor).Dispose();
								}
							}
						}
						TelephoneEditor editor2 = new TelephoneEditor(e, "TEL", null);
						try
						{
							Check(editor2.TemplateBox.Parent == null, "Dziennik telefoniczny nie pokazuje listy wzorów ruchowych.");
							editor2.ReceiverBox.Text = "Jan Malinowski";
							editor2.FreeBox.Text = "Dowolna treść rozmowy telefonicznej.";
							editor2.Shown += (object sender, EventArgs args) =>
							{
								Ui.AfterFrame(() =>
								{
									Check(editor2.FreeBox.Visible, "Własna treść jest dostępna w formularzu telefonicznym.");
									Ui.Capture(editor2, Path.Combine(target, "dziennik-telefoniczny.png"));
									((Button)editor2.Buttons.Controls[1]).PerformClick();
								});
							};
							Check(editor2.ShowDialog(main) == DialogResult.OK, "Zapis własnej treści przez formularz telefoniczny.");
						}
						finally
						{
							if (editor2 != null)
							{
								((IDisposable)editor2).Dispose();
							}
						}
						using (TelephoneEditor telephoneEditor = new TelephoneEditor(e, sec, saved))
						{
							Check(!telephoneEditor.TemplateBox.Enabled, "Przy ponownej edycji wybrany wzór pozostaje stały.");
						}
						TelephoneEditor editor3 = new TelephoneEditor(e, sec, null);
						try
						{
							editor3.ReceiverBox.Text = "Jan Malinowski";
							editor3.ReasonBox.Text = "zakończenie robót";
							editor3.Shown += (object sender, EventArgs args) =>
							{
								Ui.AfterFrame(() =>
								{
									CheckBox checkBox = All(editor3).OfType<CheckBox>().Single((CheckBox c) => c.Text == "Zawiadomienie / Powiadomienie");
									checkBox.Checked = true;
									editor3.NoticeBox.Text = "Roboty zakończono, tor zdatny do ruchu.";
									editor3.NoteBox.Text = "Dyżurny powiadomiony.";
									Check(editor3.NoticeBox.Visible && editor3.NoteBox.Bottom <= editor3.NoteBox.Parent.ClientSize.Height, "Rozwinięcie powiadomienia nie zasłania notatki ani przycisków.");
									Ui.Capture(editor3, Path.Combine(target, "powiadomienie.png"));
									((Button)editor3.Buttons.Controls[1]).PerformClick();
								});
							};
							Check(editor3.ShowDialog(main) == DialogResult.OK && e.State.Entries.Last().Text.Contains("Roboty zakończono"), "Zapis powiadomienia i notatki obok stałego wzoru.");
						}
						finally
						{
							if (editor3 != null)
							{
								((IDisposable)editor3).Dispose();
							}
						}
						Train t = e.State.Trains[4];
						main.QuickRegister(t, null);
						JournalEntry entry = e.State.Entries.First((JournalEntry x) => x.TrainId == t.Id && x.Type == "Pociąg");
						JournalPane journalPane = ((List<JournalPane>)Field(main, "panes")).First((JournalPane p) => p.SectionId == entry.SectionId);
						DataGridView grid = journalPane.Grid;
						DataGridViewRow dataGridViewRow = grid.Rows.Cast<DataGridViewRow>().First((DataGridViewRow r) => ((JournalEntry)r.Tag).Id == entry.Id);
						grid.CurrentCell = dataGridViewRow.Cells["notes"];
						grid.BeginEdit(selectAll: true);
						((TextBox)grid.EditingControl).Text = "Wpis zapisany kółkiem";
						Wheel(grid, 120);
						Check(e.State.Entries.First((JournalEntry x) => x.Id == entry.Id).Text == "Wpis zapisany kółkiem", "Kółko zatwierdza edytowany wiersz dziennika przed zmianą wyboru.");
						JournalEntry journalEntry4 = Json.Clone(e.State.Entries.First((JournalEntry x) => x.Id == entry.Id));
						journalEntry4.Left = DateTime.Today.AddHours(10.0);
						journalEntry4.Arrived = DateTime.Today.AddHours(10.0).AddMinutes(5.0);
						e.SaveJournal(journalEntry4);
						main.RefreshData();
						dataGridViewRow = grid.Rows.Cast<DataGridViewRow>().First((DataGridViewRow r) => ((JournalEntry)r.Tag).Id == entry.Id);
						grid.CurrentCell = dataGridViewRow.Cells["arrived"];
						grid.BeginEdit(selectAll: true);
						((TextBox)grid.EditingControl).Text = "09:00";
						Wheel(grid, 120);
						Check(grid.CurrentCell.RowIndex == dataGridViewRow.Index && e.State.Entries.First((JournalEntry x) => x.Id == entry.Id).Arrived == journalEntry4.Arrived, "Błędny czas zatrzymuje wybór i nie nadpisuje danych przy użyciu kółka.");
						if (!grid.IsCurrentCellInEditMode)
						{
							grid.BeginEdit(selectAll: true);
						}
						((TextBox)grid.EditingControl).Text = "10:05";
						Check(journalPane.CommitPending(), "Po poprawieniu czasu można kontynuować zapis.");
						TabControl tabControl = (TabControl)Field(main, "topTabs");
						main.RefreshData();
						tabControl.SelectedIndex = 2;
						Application.DoEvents();
						Dictionary<string, DataGridView> dictionary = (Dictionary<string, DataGridView>)Field(main, "detailGrids");
						DataGridView dataGridView = dictionary["route"];
						dataGridView.CurrentCell = dataGridView.Rows[0].Cells[0];
						Wheel(dataGridView, -120);
						Check(dataGridView.CurrentCell.RowIndex == 1, "Kółko w trasie wybiera następną stację.");
						Wheel(dataGridView, 120);
						Check(dataGridView.CurrentCell.RowIndex == 0, "Kółko w trasie wybiera poprzednią stację.");
						tabControl.SelectedIndex = 3;
						DataGridView dataGridView2 = dictionary["delays"];
						dataGridView2.Rows.Add("1", "2", "3");
						dataGridView2.Rows.Add("4", "5", "6");
						dataGridView2.CurrentCell = dataGridView2.Rows[0].Cells[0];
						Wheel(dataGridView2, -120);
						Check(dataGridView2.CurrentCell.RowIndex == 1, "Kółko działa także w pozostałych tabelach.");
						using (ReferenceListBox referenceListBox = new ReferenceListBox())
						{
							referenceListBox.Items.AddRange(new object[3] { "A", "B", "C" });
							referenceListBox.SelectedIndex = 0;
							typeof(ReferenceListBox).GetMethod("OnMouseWheel", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(referenceListBox, new object[1]
							{
								new HandledMouseEventArgs(MouseButtons.None, 0, 1, 1, -120)
							});
							Check(referenceListBox.SelectedIndex == 1, "Kółko zmienia wybór na liście konfiguracji.");
						}
						log.Add("ALL TELEPHONE AND WHEEL TESTS PASSED");
						passed = true;
					}
					catch (Exception ex)
					{
						log.Add(ex.ToString());
					}
					finally
					{
						File.WriteAllLines(Path.Combine(target, "wyniki-telefonogramy.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
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

	private static IEnumerable<Control> All(Control root)
	{
		yield return root;
		foreach (Control c in root.Controls)
		{
			foreach (Control item in All(c))
			{
				yield return item;
			}
		}
	}
}

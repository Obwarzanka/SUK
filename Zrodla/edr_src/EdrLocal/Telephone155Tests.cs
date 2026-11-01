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

public static class Telephone155Tests
{
	private struct Rect
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private struct ComboInfo
	{
		public int Size;

		public Rect Item;

		public Rect Button;

		public int State;

		public IntPtr Combo;

		public IntPtr Edit;

		public IntPtr List;
	}

	private static readonly List<string> log = new List<string>();

	private static void Check(bool ok, string text)
	{
		if (!ok)
		{
			throw new Exception("FAIL: " + text);
		}
		log.Add("PASS: " + text);
	}

	private static void Reject(Action a, string text)
	{
		bool ok = false;
		try
		{
			a();
		}
		catch
		{
			ok = true;
		}
		Check(ok, text);
	}

	private static TelephoneData Read(TelephoneEditor f)
	{
		try
		{
			return (TelephoneData)typeof(TelephoneEditor).GetMethod("ReadData", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(f, null);
		}
		catch (TargetInvocationException ex)
		{
			throw ex.InnerException;
		}
	}

	private static TelephoneData Data(string id)
	{
		TelephoneData telephoneData = new TelephoneData();
		telephoneData.TemplateId = id;
		telephoneData.Track = 1;
		telephoneData.SentAt = DateTime.Today.AddHours(12.0);
		telephoneData.EffectiveAt = DateTime.Today.AddHours(11.0).AddMinutes(55.0);
		telephoneData.Receiver = "Anna";
		telephoneData.TrainNumber = "12345";
		telephoneData.PassingTrainNumber = "67890";
		return telephoneData;
	}

	[DllImport("user32.dll")]
	private static extern bool GetComboBoxInfo(IntPtr h, ref ComboInfo info);

	[DllImport("user32.dll")]
	private static extern int GetWindowLong(IntPtr h, int index);

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(IntPtr h, out Rect rect);

	[DllImport("user32.dll")]
	private static extern IntPtr SendMessage(IntPtr h, int message, IntPtr w, IntPtr l);

	[DllImport("user32.dll")]
	private static extern bool PrintWindow(IntPtr h, IntPtr dc, int flags);

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		LocalPersistence.Enabled = false;
		try
		{
			Store store = new Store(Path.Combine(folder, "session"));
			store.Load();
			Engine engine = new Engine(store);
			engine.StartSession(store.State.Users.First((LocalUser x) => x.Id == "DEMO"));
			engine.BeginDuty(engine.Now.AddMinutes(-5.0));
			string section = engine.Station.Sections[0].Id;
			string[] array = new string[4] { "Nie wyprawiać pociągów od 11:55 aż do odwołania.", "Wstrzymanie wyprawienia pociągów odwołuję o 11:55.", "Nie wyprawiać pociągu 12345 do czasu przejazdu pociągu 67890.", "Nie wyprawiać żadnego pociągu do czasu przejazdu pociągu 67890." };
			for (int num = 0; num < 4; num++)
			{
				JournalEntry journalEntry = engine.SaveTelephone(section, null, Data((9 + num).ToString()));
				Check(journalEntry.Text == array[num], "Treść wzoru " + (9 + num) + " odpowiada zestawieniu.");
				Check(journalEntry.Effect == "" && engine.LineStatus(section, 1) == "WOLNY", "Wzór " + (9 + num) + " pozostaje dokumentacją bez fikcyjnej zajętości lub sterowania grą.");
			}
			TelephoneData bad = Data("11");
			bad.TrainNumber = "";
			Reject(() =>
			{
				engine.SaveTelephone(section, null, bad);
			}, "Wzór 11 wymaga numeru pociągu wstrzymywanego.");
			bad = Data("11");
			bad.PassingTrainNumber = "";
			Reject(() =>
			{
				engine.SaveTelephone(section, null, bad);
			}, "Wzór 11 wymaga także niezależnego numeru pociągu oczekiwanego.");
			bad = Data("12");
			bad.PassingTrainNumber = "";
			Reject(() =>
			{
				engine.SaveTelephone(section, null, bad);
			}, "Wzór 12 wymaga numeru pociągu oczekiwanego.");
			using (MainForm mainForm = new MainForm(engine))
			{
				mainForm.Show();
				Application.DoEvents();
				string[] array2 = new string[4] { "9", "10", "11", "12" };
				foreach (string text in array2)
				{
					using TelephoneEditor telephoneEditor = new TelephoneEditor(engine, section, null);
					telephoneEditor.Show(mainForm);
					Application.DoEvents();
					telephoneEditor.TemplateBox.SelectedIndex = Array.IndexOf(TelephoneTemplates.Ids, text);
					telephoneEditor.ReceiverBox.Text = "Anna";
					telephoneEditor.TrainBox.Text = "12345";
					telephoneEditor.PassingTrainBox.Text = "67890";
					telephoneEditor.EventTimeBox.Text = ((text == "11" || text == "12") ? "" : "11:55");
					Application.DoEvents();
					TelephoneData telephoneData = Read(telephoneEditor);
					Check(telephoneData.TemplateId == text && ((text == "11" || text == "12") ? (telephoneData.EffectiveAt == telephoneData.SentAt) : (telephoneData.EffectiveAt.ToString("HH:mm") == "11:55")), "Formularz " + text + " odczytuje właściwe pola i godziny.");
					JournalEntry journalEntry2 = engine.SaveTelephone(section, null, telephoneData);
					Check(journalEntry2.Text == array[int.Parse(text) - 9], "Zapis z formularza " + text + " zachowuje właściwą treść.");
					Ui.Capture(telephoneEditor, Path.Combine(folder, "telefonogram-" + text + ".png"));
					telephoneEditor.Close();
				}
				JournalEntry original = engine.State.Entries.First((JournalEntry x) => x.Telephone != null && x.Telephone.TemplateId == "11");
				using (TelephoneEditor telephoneEditor2 = new TelephoneEditor(engine, section, original))
				{
					telephoneEditor2.Show(mainForm);
					Application.DoEvents();
					Check(telephoneEditor2.TrainBox.Text == "12345" && telephoneEditor2.PassingTrainBox.Text == "67890" && !telephoneEditor2.TemplateBox.Enabled, "Ponowna edycja wzoru 11 zachowuje oba numery i wybrany wzór.");
					telephoneEditor2.PassingTrainBox.Text = "67892";
					engine.SaveTelephone(section, original, Read(telephoneEditor2));
					Check(engine.State.Entries.First((JournalEntry x) => x.Id == original.Id).Text.Contains("pociągu 67892"), "Edycja numeru oczekiwanego pociągu aktualizuje zapisaną treść.");
					telephoneEditor2.Close();
				}
				using (TelephoneEditor telephoneEditor3 = new TelephoneEditor(engine, section, null))
				{
					telephoneEditor3.Show(mainForm);
					Application.DoEvents();
					telephoneEditor3.TemplateBox.SelectedIndex = 0;
					telephoneEditor3.TemplateBox.DroppedDown = true;
					Application.DoEvents();
					ComboInfo info = new ComboInfo
					{
						Size = Marshal.SizeOf(typeof(ComboInfo))
					};
					Check(GetComboBoxInfo(telephoneEditor3.TemplateBox.Handle, ref info) && info.List != IntPtr.Zero, "Otwarta lista korzysta z systemowego okna ComboLBox.");
					Check(telephoneEditor3.TemplateBox.Items.Count == 22 && telephoneEditor3.TemplateBox.MaxDropDownItems == 8 && !telephoneEditor3.TemplateBox.IntegralHeight && (GetWindowLong(info.List, -16) & 0x200000) != 0, "Lista 22 wzorów ma ograniczoną wysokość i prawdziwy pionowy pasek przewijania.");
					GetWindowRect(info.List, out var rect);
					Check(rect.Bottom - rect.Top <= telephoneEditor3.TemplateBox.ItemHeight * 9, "Rozwijana lista nie rośnie do wysokości całego katalogu.");
					SendMessage(info.List, 277, new IntPtr(7), IntPtr.Zero);
					Application.DoEvents();
					Check(SendMessage(info.List, 398, IntPtr.Zero, IntPtr.Zero).ToInt32() > 0, "Systemowy scroller pozwala przewinąć do dalszych telefonogramów.");
					using (Bitmap bitmap = new Bitmap(rect.Right - rect.Left, rect.Bottom - rect.Top))
					{
						using Graphics graphics = Graphics.FromImage(bitmap);
						IntPtr hdc = graphics.GetHdc();
						bool ok;
						try
						{
							ok = PrintWindow(info.List, hdc, 0);
						}
						finally
						{
							graphics.ReleaseHdc(hdc);
						}
						Check(ok, "Zapisano podgląd rzeczywistej rozwiniętej listy.");
						bitmap.Save(Path.Combine(folder, "lista-z-przewijaniem.png"));
					}
					SendMessage(info.List, 277, new IntPtr(6), IntPtr.Zero);
					Application.DoEvents();
					Check(SendMessage(info.List, 398, IntPtr.Zero, IntPtr.Zero).ToInt32() == 0, "Listę można przewinąć z powrotem na początek.");
					telephoneEditor3.TemplateBox.DroppedDown = false;
					telephoneEditor3.TemplateBox.SelectedIndex = 21;
					Check(telephoneEditor3.TemplateBox.Text.StartsWith("[32]"), "Po przewinięciu można wybrać ostatni wzór.");
					telephoneEditor3.Close();
				}
				mainForm.Close();
			}
			Check(!Directory.Exists(store.DirectoryPath), "Nowe telefonogramy nie utrwalają sesji na dysku.");
			return true;
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			return false;
		}
		finally
		{
			File.WriteAllLines(Path.Combine(folder, "wyniki-telefonogramy155.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

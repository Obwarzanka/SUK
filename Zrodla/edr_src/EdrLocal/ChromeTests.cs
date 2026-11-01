using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace EdrLocal;

internal static class ChromeTests
{
	private static readonly List<string> log = new List<string>();

	[DllImport("user32.dll")]
	private static extern int GetWindowLong(IntPtr window, int index);

	[DllImport("user32.dll")]
	private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wparam, IntPtr lparam);

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	private static extern uint ExtractIconEx(string file, int index, IntPtr[] large, IntPtr[] small, uint count);

	[DllImport("user32.dll")]
	private static extern bool DestroyIcon(IntPtr icon);

	private static void Check(bool value, string message)
	{
		if (!value)
		{
			throw new Exception(message);
		}
		log.Add("PASS: " + message);
	}

	private static void Command(Form form, int value)
	{
		SendMessage(form.Handle, 274, (IntPtr)value, IntPtr.Zero);
		Application.DoEvents();
	}

	private static bool Style(Form form, int flag)
	{
		return (GetWindowLong(form.Handle, -16) & flag) == flag;
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
		MainForm main = new MainForm(engine);
		try
		{
			main.Shown += (object sender, EventArgs args) =>
			{
				Ui.AfterFrame(() =>
				{
					try
					{
						Check(Style(main, 12582912) && Style(main, 524288), "Główne okno ma natywny pasek tytułu i menu systemowe Windows.");
						Check(Style(main, 65536) && Style(main, 131072) && Style(main, 262144), "Windows udostępnia maksymalizację, minimalizację i zmianę rozmiaru ramki.");
						Check(SendMessage(main.Handle, 127, (IntPtr)1, IntPtr.Zero) != IntPtr.Zero, "Główne okno ma ikonę zarejestrowaną w Windows.");
						IntPtr[] array = new IntPtr[1];
						IntPtr[] array2 = new IntPtr[1];
						uint num = ExtractIconEx(Application.ExecutablePath, 0, array, array2, 1u);
						try
						{
							Check(num != 0 && array[0] != IntPtr.Zero && array2[0] != IntPtr.Zero, "EXE zawiera ikonę rozpoznawaną przez powłokę Windows w małym i dużym rozmiarze.");
						}
						finally
						{
							if (array[0] != IntPtr.Zero)
							{
								DestroyIcon(array[0]);
							}
							if (array2[0] != IntPtr.Zero)
							{
								DestroyIcon(array2[0]);
							}
						}
						Command(main, 61472);
						Check(main.WindowState == FormWindowState.Minimized, "Systemowe polecenie Minimalizuj działa.");
						Command(main, 61728);
						Check(main.WindowState == FormWindowState.Normal, "Systemowe polecenie Przywróć działa po minimalizacji.");
						Command(main, 61488);
						Check(main.WindowState == FormWindowState.Maximized, "Systemowe polecenie Maksymalizuj działa.");
						Ui.Capture(main, Path.Combine(folder, "okno-zmaksymalizowane.png"));
						Command(main, 61728);
						Check(main.WindowState == FormWindowState.Normal, "Systemowe polecenie Przywróć działa po maksymalizacji.");
						using (LoginForm loginForm = new LoginForm(store))
						{
							loginForm.Show(main);
							Application.DoEvents();
							Check(Style(loginForm, 12582912) && Style(loginForm, 524288) && !Style(loginForm, 262144), "Logowanie ma stałą systemową ramkę z przyciskiem zamykania.");
							Check(!Style(loginForm, 65536) && !Style(loginForm, 131072), "Okno logowania zachowuje wyłączoną minimalizację i maksymalizację.");
							Ui.Capture(loginForm, Path.Combine(folder, "logowanie.png"));
							Command(loginForm, 61536);
							Check(loginForm.IsDisposed || !loginForm.Visible, "Systemowy przycisk Zamknij zamyka logowanie.");
						}
						using (TelephoneEditor telephoneEditor = new TelephoneEditor(engine, "TEL", null))
						{
							telephoneEditor.Show(main);
							Application.DoEvents();
							Check(Style(telephoneEditor, 12582912) && Style(telephoneEditor, 524288) && !Style(telephoneEditor, 262144), "Telefonogram korzysta z systemowej ramki dialogowej.");
							Check(telephoneEditor.NoteBox.Parent != null && telephoneEditor.FreeBox.Bottom <= telephoneEditor.FreeBox.Parent.ClientSize.Height, "Treść telefonicznego formularza mieści się po zmianie ramki.");
							Ui.Capture(telephoneEditor, Path.Combine(folder, "telefonogram.png"));
							Command(telephoneEditor, 61536);
							Check(telephoneEditor.IsDisposed || !telephoneEditor.Visible, "Systemowe zamknięcie działa także dla telefonogramu.");
						}
						bool closing = false;
						FormClosingEventHandler value = (object a, FormClosingEventArgs b) =>
						{
							closing = true;
							b.Cancel = true;
						};
						main.FormClosing += value;
						Command(main, 61536);
						Check(closing && !main.IsDisposed && main.Visible, "Zamknięcie przez Windows respektuje anulowanie w FormClosing.");
						main.FormClosing -= value;
						main.MaximumSize = Size.Empty;
						main.Size = new Size(1920, 1040);
						main.ApplyZoom();
						Train train = engine.State.Trains.Single((Train x) => x.Number == "70413");
						main.SelectTrain(train.Id);
						Application.DoEvents();
						Ui.Capture(main, Path.Combine(folder, "pasek-windows.png"));
						Command(main, 61536);
						Check(main.IsDisposed, "Systemowe Zamknij kończy główne okno po zaakceptowaniu zamknięcia.");
						ok = true;
						log.Add("ALL NATIVE CHROME AND ICON TESTS PASSED");
					}
					catch (Exception ex)
					{
						log.Add(ex.ToString());
					}
					finally
					{
						File.WriteAllLines(Path.Combine(folder, "wyniki-ramka-ikona.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
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

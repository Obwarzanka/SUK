using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace EdrLocal;

internal static class UiTests
{
	private class Case
	{
		public string Name;

		public string Method;

		public object[] Args;

		public Case(string name, string method, object[] args)
		{
			Name = name;
			Method = method;
			Args = args;
		}
	}

	public static bool Run(string target)
	{
		Ui.Testing = true;
		Directory.CreateDirectory(target);
		Store store = new Store(Path.Combine(target, "dane-" + Guid.NewGuid().ToString("N")));
		store.Load();
		Engine engine = new Engine(store)
		{
			User = store.State.Users.First((LocalUser x) => x.Id == "DEMO")
		};
		engine.BeginDuty(DateTime.Now.AddMinutes(-1.0));
		List<string> log = new List<string>();
		bool success = false;
		MainForm main = new MainForm(engine);
		try
		{
			main.Shown += (object s, EventArgs e) =>
			{
				Ui.AfterFrame(() =>
				{
					try
					{
						BehaviourTests.Run(main, target, log);
						Save(main, Path.Combine(target, "glowne.png"));
						Case[] array = new Case[17]
						{
							new Case("01-przyjecie", "EditJournal", new object[4]
							{
								null,
								engine.State.Trains[1],
								"Z",
								null
							}),
							new Case("02-telefonogram", "Telephone", new object[1] { engine.Station.Sections[0].Id }),
							new Case("03-wpis", "FreeNote", new object[1] { "TEL" }),
							new Case("04-czasy", "TrainTimes", new object[0]),
							new Case("05-prognoza", "TrainStop", new object[0]),
							new Case("06-gotowosc", "TrainReady", new object[0]),
							new Case("07-analiza", "TrainAnalysis", new object[0]),
							new Case("08-ladunek", "TrainCargo", new object[0]),
							new Case("09-uwagi", "TrainNotes", new object[0]),
							null,
							null,
							null,
							null,
							null,
							null,
							null,
							null
						};
						object[] args = new object[1];
						array[9] = new Case("10-usluga", "ServiceEdit", args);
						array[10] = new Case("11-raport", "Report", new object[1] { false });
						array[11] = new Case("12-konfiguracja", "Configure", new object[0]);
						array[12] = new Case("13-uzytkownicy", "Users", new object[0]);
						object[] args2 = new object[1];
						array[13] = new Case("14-historia", "History", args2);
						array[14] = new Case("15-sytuacja", "Situation", new object[0]);
						array[15] = new Case("16-legenda", "Legend", new object[0]);
						array[16] = new Case("17-dyzur", "DutyAction", new object[0]);
						Case[] array2 = array;
						Case[] array3 = array2;
						foreach (Case test in array3)
						{
							bool captured = false;
							Exception captureError = null;
							Timer timer = new Timer
							{
								Interval = 180
							};
							try
							{
								timer.Tick += (object a, EventArgs b) =>
								{
									Form form2 = Application.OpenForms.Cast<Form>().LastOrDefault((Form f) => f != main && f.Visible);
									if (form2 != null)
									{
										timer.Stop();
										try
										{
											Save(form2, Path.Combine(target, test.Name + ".png"));
											if (form2 is Editor)
											{
												Editor editor = (Editor)form2;
												if (editor.Buttons.Bottom > editor.ClientSize.Height + 1)
												{
													throw new Exception("Przyciski wychodzą poza okno.");
												}
											}
											captured = true;
										}
										catch (Exception ex2)
										{
											captureError = ex2;
										}
										if (test.Method == "EditJournal" || test.Method == "FreeNote" || test.Method == "DutyAction")
										{
											Editor editor2 = (Editor)form2;
											if (test.Method == "FreeNote")
											{
												((TextBox)editor2.Fields.GetControlFromPosition(1, 1)).Text = "Test zapisu z okna edycji <script>";
											}
											((Button)editor2.Buttons.Controls[1]).PerformClick();
										}
										else
										{
											form2.DialogResult = DialogResult.Cancel;
											form2.Close();
										}
									}
								};
								timer.Start();
								MethodInfo methodInfo = typeof(MainForm).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single((MethodInfo m) => m.Name == test.Method && m.GetParameters().Length == test.Args.Length);
								methodInfo.Invoke(main, test.Args);
							}
							finally
							{
								if (timer != null)
								{
									((IDisposable)timer).Dispose();
								}
							}
							if (captureError != null)
							{
								throw captureError;
							}
							if (!captured)
							{
								throw new Exception("Nie otwarto okna: " + test.Name);
							}
							log.Add("PASS: " + test.Name);
							if (test.Method == "EditJournal" && !engine.State.Entries.Any((JournalEntry x) => x.TrainId == engine.State.Trains[1].Id))
							{
								throw new Exception("Nie zapisano wiersza z okna edycji.");
							}
							if (test.Method == "FreeNote" && !engine.State.Entries.Any((JournalEntry x) => (x.Text ?? "").Contains("Test zapisu z okna edycji")))
							{
								throw new Exception("Nie zapisano treści z okna edycji.");
							}
							if (test.Method == "DutyAction" && engine.CanWrite)
							{
								throw new Exception("Nie zakończono dyżuru z okna edycji.");
							}
						}
						Form[] array4 = new Form[3]
						{
							new LoginForm(store),
							new LocationForm(engine),
							new StartupForm(engine)
						};
						foreach (Form form in array4)
						{
							using (form)
							{
								form.Shown += (object sender, EventArgs e2) =>
								{
									Ui.AfterFrame(() =>
									{
										Save(form, Path.Combine(target, form.GetType().Name + ".png"));
										form.DialogResult = DialogResult.Cancel;
										form.Close();
									});
								};
								form.ShowDialog(main);
								log.Add("PASS: " + form.GetType().Name);
							}
						}
						MethodInfo method = typeof(MainForm).GetMethod("SetLayout", BindingFlags.Instance | BindingFlags.NonPublic);
						method.Invoke(main, new object[2] { "Prawo", false });
						main.Refresh();
						Save(main, Path.Combine(target, "18-dzienniki-prawo.png"));
						method.Invoke(main, new object[2] { "Dół", false });
						log.Add("PASS: Zmiana układu dzienników.");
						MethodInfo method2 = typeof(MainForm).GetMethod("WriteReport", BindingFlags.Instance | BindingFlags.NonPublic);
						string text = Path.Combine(target, "raport.html");
						method2.Invoke(main, new object[4]
						{
							text,
							engine.State.Entries.ToList(),
							true,
							false
						});
						if (File.ReadAllText(text).Contains("<script>"))
						{
							throw new Exception("Treść raportu nie została zakodowana.");
						}
						log.Add("PASS: Raport HTML z historią i bezpieczne kodowanie treści.");
						File.WriteAllLines(Path.Combine(target, "wyniki-ui.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
						success = true;
					}
					catch (Exception ex)
					{
						log.Add(ex.ToString());
						File.WriteAllLines(Path.Combine(target, "wyniki-ui.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
					}
					finally
					{
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

	private static void Save(Form form, string path)
	{
		Ui.Capture(form, path);
	}
}

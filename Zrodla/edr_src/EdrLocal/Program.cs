using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace EdrLocal;

internal static class Program
{
	[STAThread]
	private static int Main(string[] args)
	{
		try
		{
			BundledWebView2.Initialize();
		}
		catch (Exception ex)
		{
			MessageBox.Show("Nie udało się załadować składników programu. Pobierz EDR ponownie.\n\n" + ex.Message, "EDR", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			return 1;
		}
		return Run(args);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static int Run(string[] args)
	{
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
		Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("pl-PL");
		string text = Argument(args, "--data") ?? ((args.Contains("--demo") || Argument(args, "--capture") != null) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dane") : Path.Combine(DataLocations.Root, "local"));
		LocalPersistence.Enabled = args.Any((string x) => x.EndsWith("-tests", StringComparison.Ordinal) || x == "--self-test");
		try
		{
			string text2 = Argument(args, "--update199-tests");
			if (text2 != null)
			{
				return (!Update199Tests.Run(text2)) ? 2 : 0;
			}
			string text3 = Argument(args, "--identity199-vectors");
			if (text3 != null)
			{
				return (!Update199Tests.Compare(text3)) ? 2 : 0;
			}
			string text4 = Argument(args, "--input198-tests");
			if (text4 != null)
			{
				return (!Input198Tests.Run(text4)) ? 2 : 0;
			}
			string text5 = Argument(args, "--input197-tests");
			if (text5 != null)
			{
				return (!Input197Tests.Run(text5)) ? 2 : 0;
			}
			string text6 = Argument(args, "--input196-tests");
			if (text6 != null)
			{
				return (!Input196Tests.Run(text6)) ? 2 : 0;
			}
			string text7 = Argument(args, "--td2194-tests");
			if (text7 != null)
			{
				return (!Td2194Tests.Run(text7)) ? 2 : 0;
			}
			string text8 = Argument(args, "--td2193-tests");
			if (text8 != null)
			{
				return (!Td2193Tests.Run(text8)) ? 2 : 0;
			}
			string text9 = Argument(args, "--td2192-tests");
			if (text9 != null)
			{
				return (!Td2192Tests.Run(text9)) ? 2 : 0;
			}
			string text10 = Argument(args, "--td2191-tests");
			if (text10 != null)
			{
				return (!Td2191Tests.Run(text10)) ? 2 : 0;
			}
			string text11 = Argument(args, "--td2190-tests");
			if (text11 != null)
			{
				return (!Td2190Tests.Run(text11)) ? 2 : 0;
			}
			string text12 = Argument(args, "--windows188-tests");
			if (text12 != null)
			{
				return (!Windows188Tests.Run(text12)) ? 2 : 0;
			}
			string text13 = Argument(args, "--crossings186-tests");
			if (text13 != null)
			{
				return (!Crossings186Tests.Run(text13)) ? 2 : 0;
			}
			string text14 = Argument(args, "--arrival185-tests");
			if (text14 != null)
			{
				return (!Arrival185Tests.Run(text14)) ? 2 : 0;
			}
			string text15 = Argument(args, "--clock184-tests");
			if (text15 != null)
			{
				return (!Clock184Tests.Run(text15)) ? 2 : 0;
			}
			string text16 = Argument(args, "--dependencies183-render-tests");
			if (text16 != null)
			{
				return (!Dependencies183Tests.Run(text16, render: true)) ? 2 : 0;
			}
			string text17 = Argument(args, "--dependencies183-tests");
			if (text17 != null)
			{
				return (!Dependencies183Tests.Run(text17)) ? 2 : 0;
			}
			string text18 = Argument(args, "--neighbours182-tests");
			if (text18 != null)
			{
				return (!Neighbours182Tests.Run(text18)) ? 2 : 0;
			}
			string text19 = Argument(args, "--errors180-tests");
			if (text19 != null)
			{
				return (!Errors180Tests.Run(text19)) ? 2 : 0;
			}
			string text20 = Argument(args, "--sepe179-tests");
			if (text20 != null)
			{
				return (!Sepe179Tests.Run(text20)) ? 2 : 0;
			}
			string text21 = Argument(args, "--retry176-tests");
			if (text21 != null)
			{
				return (!Retry176Tests.Run(text21)) ? 2 : 0;
			}
			string text22 = Argument(args, "--forecast173-tests");
			if (text22 != null)
			{
				return (!Forecast173Tests.Run(text22)) ? 2 : 0;
			}
			string text23 = Argument(args, "--retry172-tests");
			if (text23 != null)
			{
				return (!Retry172Tests.Run(text23)) ? 2 : 0;
			}
			string text24 = Argument(args, "--configuration171-tests");
			if (text24 != null)
			{
				return (!Configuration171Tests.Run(text24)) ? 2 : 0;
			}
			string text25 = Argument(args, "--configuration170-tests");
			if (text25 != null)
			{
				return (!Configuration170Tests.Run(text25)) ? 2 : 0;
			}
			string text26 = Argument(args, "--midnight169-tests");
			if (text26 != null)
			{
				return (!Midnight169Tests.Run(text26)) ? 2 : 0;
			}
			string text27 = Argument(args, "--stop167-tests");
			if (text27 != null)
			{
				return (!Stop167Tests.Run(text27)) ? 2 : 0;
			}
			string text28 = Argument(args, "--telephone156-tests");
			if (text28 != null)
			{
				return (!Telephone156Tests.Run(text28)) ? 2 : 0;
			}
			string text29 = Argument(args, "--telephone155-tests");
			if (text29 != null)
			{
				return (!Telephone155Tests.Run(text29)) ? 2 : 0;
			}
			string text30 = Argument(args, "--native163-tests");
			if (text30 != null)
			{
				return (!Native163Tests.Run(text30)) ? 2 : 0;
			}
			string text31 = Argument(args, "--view161-tests");
			if (text31 != null)
			{
				return (!View161Tests.Run(text31)) ? 2 : 0;
			}
			string text32 = Argument(args, "--traffic153-tests");
			if (text32 != null)
			{
				return (!Traffic153Tests.Run(text32)) ? 2 : 0;
			}
			string text33 = Argument(args, "--tzp151-tests");
			if (text33 != null)
			{
				return (!Tzp151Tests.Run(text33)) ? 2 : 0;
			}
			string text34 = Argument(args, "--no-link150-tests");
			if (text34 != null)
			{
				return (!NoLink150Tests.Run(text34)) ? 2 : 0;
			}
			string text35 = Argument(args, "--tracks49-tests");
			if (text35 != null)
			{
				return (!Tracks49Tests.Run(text35)) ? 2 : 0;
			}
			string text36 = Argument(args, "--appearance48-tests");
			if (text36 != null)
			{
				return (!Appearance48Tests.Run(text36)) ? 2 : 0;
			}
			string text37 = Argument(args, "--operations47-tests");
			if (text37 != null)
			{
				return (!Operations47Tests.Run(text37)) ? 2 : 0;
			}
			string text38 = Argument(args, "--documentation46-tests");
			if (text38 != null)
			{
				return (!Documentation46Tests.Run(text38)) ? 2 : 0;
			}
			string text39 = Argument(args, "--directions45-tests");
			if (text39 != null)
			{
				return (!Directions45Tests.Run(text39)) ? 2 : 0;
			}
			string text40 = Argument(args, "--connection44-tests");
			if (text40 != null)
			{
				return (!Connection44Tests.Run(text40)) ? 2 : 0;
			}
			string text41 = Argument(args, "--multiline43-tests");
			if (text41 != null)
			{
				return (!Multiline43Tests.Run(text41)) ? 2 : 0;
			}
			string text42 = Argument(args, "--configuration42-tests");
			if (text42 != null)
			{
				return (!Configuration42Tests.Run(text42)) ? 2 : 0;
			}
			string text43 = Argument(args, "--carriers41-tests");
			if (text43 != null)
			{
				return (!Carriers41Tests.Run(text43)) ? 2 : 0;
			}
			string text44 = Argument(args, "--carriers40-tests");
			if (text44 != null)
			{
				return (!Carriers40Tests.Run(text44)) ? 2 : 0;
			}
			string text45 = Argument(args, "--carriers39-tests");
			if (text45 != null)
			{
				return (!Carriers39Tests.Run(text45)) ? 2 : 0;
			}
			string text46 = Argument(args, "--carriers38-tests");
			if (text46 != null)
			{
				return (!Carriers38Tests.Run(text46)) ? 2 : 0;
			}
			string text47 = Argument(args, "--memory36-tests");
			if (text47 != null)
			{
				return (!Memory36Tests.Run(text47, Argument(args, "--simrail-fixture"))) ? 2 : 0;
			}
			string text48 = Argument(args, "--persistence35-tests");
			if (text48 != null)
			{
				return (!Memory36Tests.Run(text48)) ? 2 : 0;
			}
			string text49 = Argument(args, "--connection34-tests");
			if (text49 != null)
			{
				return (!Memory36Tests.Run(text49)) ? 2 : 0;
			}
			string text50 = Argument(args, "--defaults33-tests");
			if (text50 != null)
			{
				return (!Defaults33Tests.Run(text50)) ? 2 : 0;
			}
			string text51 = Argument(args, "--connection32-tests");
			if (text51 != null)
			{
				return (!Memory36Tests.Run(text51)) ? 2 : 0;
			}
			string text52 = Argument(args, "--organization29-tests");
			if (text52 != null)
			{
				return (!Organization29Tests.Run(text52)) ? 2 : 0;
			}
			string text53 = Argument(args, "--configurator28-tests");
			if (text53 != null)
			{
				return (!Organization29Tests.Run(text53)) ? 2 : 0;
			}
			string text54 = Argument(args, "--configurator");
			if (text54 != null)
			{
				LocalPersistence.Enabled = false;
				return RunConfigurator(text54);
			}
			string text55 = Argument(args, "--profiles27-tests");
			if (text55 != null)
			{
				return (!Profiles27Tests.Run(text55)) ? 2 : 0;
			}
			string text56 = Argument(args, "--counter26-tests");
			if (text56 != null)
			{
				return (!Counter26Tests.Run(text56)) ? 2 : 0;
			}
			string text57 = Argument(args, "--telephone25-tests");
			if (text57 != null)
			{
				return (!Telephone25Tests.Run(text57)) ? 2 : 0;
			}
			string text58 = Argument(args, "--stations24-tests");
			if (text58 != null)
			{
				return (!Stations24Tests.Run(text58)) ? 2 : 0;
			}
			string text59 = Argument(args, "--fixes23-tests");
			if (text59 != null)
			{
				return (!Retry23Tests.Run(text59)) ? 2 : 0;
			}
			string text60 = Argument(args, "--fixes22-tests");
			if (text60 != null)
			{
				return (!Integration22Tests.Run(text60)) ? 2 : 0;
			}
			string text61 = Argument(args, "--session21-tests");
			if (text61 != null)
			{
				return (!Session21Tests.Run(text61)) ? 2 : 0;
			}
			string text62 = Argument(args, "--confirmation20-tests");
			if (text62 != null)
			{
				return (!Confirmation20Tests.Run(text62)) ? 2 : 0;
			}
			string text63 = Argument(args, "--fixes19-tests");
			if (text63 != null)
			{
				return (!Fixes19Tests.Run(text63)) ? 2 : 0;
			}
			string text64 = Argument(args, "--sepe-browser-tests");
			if (text64 != null)
			{
				return (!SepeBrowserTests.Run(text64)) ? 2 : 0;
			}
			string text65 = Argument(args, "--simrail-tests");
			if (text65 != null)
			{
				return (!SimRailTests.Run(text65, Argument(args, "--simrail-fixture") ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src", "testdata", "simrail.json"), args.Contains("--live-api"))) ? 2 : 0;
			}
			string text66 = Argument(args, "--self-test");
			if (text66 != null)
			{
				SelfTests.Run(text66);
				return 0;
			}
			string text67 = Argument(args, "--presentation-tests");
			if (text67 != null)
			{
				return (!PresentationTests.Run(text67)) ? 2 : 0;
			}
			string text68 = Argument(args, "--chrome-tests");
			if (text68 != null)
			{
				return (!ChromeTests.Run(text68)) ? 2 : 0;
			}
			string text69 = Argument(args, "--popup-tests");
			if (text69 != null)
			{
				return (!PopupTests.Run(text69)) ? 2 : 0;
			}
			string text70 = Argument(args, "--return-tests");
			if (text70 != null)
			{
				return (!ReturnTripTests.Run(text70)) ? 2 : 0;
			}
			string text71 = Argument(args, "--fidelity-tests");
			if (text71 != null)
			{
				return (!FidelityTests.Run(text71)) ? 2 : 0;
			}
			string text72 = Argument(args, "--report-tests");
			if (text72 != null)
			{
				return (!ReportRouteTests.Run(text72)) ? 2 : 0;
			}
			string text73 = Argument(args, "--telephone-tests");
			if (text73 != null)
			{
				return (!TelephoneTests.Run(text73)) ? 2 : 0;
			}
			string text74 = Argument(args, "--interaction-tests");
			if (text74 != null)
			{
				return (!InteractionTests.Run(text74)) ? 2 : 0;
			}
			string text75 = Argument(args, "--layout-tests");
			if (text75 != null)
			{
				return (!LayoutTests.Run(text75)) ? 2 : 0;
			}
			string text76 = Argument(args, "--ui-tests");
			if (text76 != null)
			{
				return (!UiTests.Run(text76)) ? 2 : 0;
			}
			LocalPersistence.Enabled = false;
			if (!args.Contains("--demo") && Argument(args, "--capture") == null)
			{
				using ModeForm modeForm = new ModeForm();
				if (modeForm.ShowDialog() != DialogResult.OK)
				{
					return 0;
				}
				if (modeForm.ConfiguratorProvider != null)
				{
					return RunConfigurator(modeForm.ConfiguratorProvider);
				}
				if (modeForm.Simulator)
				{
					return RunSimulator(modeForm.Provider ?? "simrail");
				}
			}
			string text77;
			using (SHA256 sHA = SHA256.Create())
			{
				text77 = BitConverter.ToString(sHA.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(text).ToUpperInvariant()))).Replace("-", "");
			}
			bool createdNew;
			using (new Mutex(initiallyOwned: true, "Local\\EDRLocal_" + text77, out createdNew))
			{
				if (!createdNew)
				{
					MessageBox.Show("Ta baza EDR jest już otwarta w innym oknie.", "EDR");
					return 1;
				}
				Store store = new Store(text, !LocalPersistence.Enabled || args.Contains("--training"));
				store.Load();
				Engine engine = new Engine(store);
				string capture = Argument(args, "--capture");
				if (capture != null)
				{
					engine.User = store.State.Users.First((LocalUser x) => x.Id == "DEMO");
					if (engine.ActiveDuty == null)
					{
						engine.BeginDuty(DateTime.Now.AddMinutes(-30.0));
					}
					Train t = engine.State.Trains[0];
					if (!engine.State.Entries.Any((JournalEntry x) => x.Type == "Pociąg"))
					{
						JournalEntry journalEntry = engine.Note(engine.Station.Sections[0].Id, DateTime.Now.AddMinutes(-6.0), "", "Pociąg");
						journalEntry.TrainId = t.Id;
						journalEntry.Number = engine.TrainNumber(t, engine.Station.Sections[0], "Z");
						journalEntry.Direction = "Z";
						journalEntry.Track = "1";
						journalEntry.Left = DateTime.Now.AddMinutes(-5.0);
						journalEntry.Arrived = DateTime.Now.AddMinutes(-2.0);
						engine.SaveJournal(journalEntry);
						JournalEntry journalEntry2 = engine.Note(engine.Station.Sections[1].Id, DateTime.Now.AddMinutes(-3.0), "Po torze lewym", "Pociąg");
						journalEntry2.TrainId = engine.State.Trains[1].Id;
						journalEntry2.Number = "69001";
						journalEntry2.Direction = "Z";
						journalEntry2.LineTrack = 2;
						journalEntry2.Track = "2";
						journalEntry2.Left = DateTime.Now.AddMinutes(-2.0);
						engine.SaveJournal(journalEntry2);
					}
					MainForm f = new MainForm(engine);
					try
					{
						f.Shown += (object s, EventArgs e) =>
						{
							Ui.AfterFrame(() =>
							{
								try
								{
									f.SelectTrain(t.Id);
									f.Refresh();
									Ui.Capture(f, capture);
									File.WriteAllText(capture + ".txt", "OK " + f.Width + "x" + f.Height);
								}
								catch (Exception ex2)
								{
									File.WriteAllText(capture + ".error.txt", ex2.ToString());
								}
								f.Hide();
								f.Dispose();
								Application.ExitThread();
							});
						};
						Application.Run(f);
					}
					finally
					{
						if (f != null)
						{
							((IDisposable)f).Dispose();
						}
					}
					return 0;
				}
				if (args.Contains("--demo"))
				{
					engine.User = store.State.Users.First((LocalUser x) => x.Id == "DEMO");
					SessionWindows.Run(new MainForm(engine));
					return 0;
				}
				while (true)
				{
					using (LoginForm loginForm = new LoginForm(store))
					{
						if (loginForm.ShowDialog() != DialogResult.OK)
						{
							break;
						}
						engine.StartSession(loginForm.SelectedUser);
						goto IL_1061;
					}
					IL_1061:
					engine.ReadOnlySession = false;
					bool configure = false;
					bool flag = false;
					using (LocationForm locationForm = new LocationForm(engine))
					{
						if (locationForm.ShowDialog() != DialogResult.OK)
						{
							break;
						}
						configure = locationForm.ConfigureAfterOpening;
						goto IL_10a8;
					}
					IL_10a8:
					if (!configure)
					{
						using StartupForm startupForm = new StartupForm(engine);
						if (startupForm.ShowDialog() != DialogResult.OK)
						{
							break;
						}
						flag = startupForm.BeginDuty;
						engine.ReadOnlySession = startupForm.ObserveOnly;
						goto IL_10ee;
					}
					goto IL_10ee;
					IL_10ee:
					MainForm main = new MainForm(engine);
					try
					{
						if (configure || flag)
						{
							main.Shown += (object s, EventArgs e) =>
							{
								Ui.AfterFrame(() =>
								{
									Ui.Run(main, () =>
									{
										if (configure)
										{
											main.ConfigureStation();
										}
										else
										{
											main.DutyAction();
										}
									});
								});
							};
						}
						if (SessionWindows.Run(main) != DialogResult.Retry)
						{
							break;
						}
					}
					finally
					{
						if (main != null)
						{
							((IDisposable)main).Dispose();
						}
					}
				}
				return 0;
			}
		}
		catch (Exception ex)
		{
			try
			{
				if (LocalPersistence.Enabled)
				{
					Directory.CreateDirectory(text);
					File.WriteAllText(Path.Combine(text, "ostatni-blad.txt"), DateTime.Now.ToString("s") + "\r\n" + ex.ToString(), Encoding.UTF8);
				}
			}
			catch
			{
			}
			if (Argument(args, "--self-test") != null || Argument(args, "--capture") != null)
			{
				return 2;
			}
			MessageBox.Show("Nie udało się uruchomić EDR.\n\n" + ex.Message + "\n\nDane: " + text + "\nIstniejący plik danych nie został zastąpiony.", "EDR", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			return 1;
		}
	}

	private static int RunConfigurator(string provider)
	{
		if (!LocalPersistence.Enabled)
		{
			MessageBox.Show("Konfigurator jest tymczasowo wyłączony.", "EDR");
			return 0;
		}
		provider = ConfiguratorRepository.Provider(provider);
		bool createdNew;
		using (new Mutex(initiallyOwned: true, "Local\\SimKol_EDR_Configurator_" + provider, out createdNew))
		{
			if (!createdNew)
			{
				MessageBox.Show("Konfigurator tego symulatora jest już otwarty.", "EDR");
				return 1;
			}
			ConfiguratorRepository configuratorRepository = new ConfiguratorRepository(provider, null);
			configuratorRepository.Load();
			using (AdministrationForm mainForm = new AdministrationForm(configuratorRepository))
			{
				Application.Run(mainForm);
			}
			return 0;
		}
	}

	private static int RunSimulator(string provider)
	{
		while (true)
		{
			using SimulatorConnectionForm simulatorConnectionForm = new SimulatorConnectionForm(provider);
			if (simulatorConnectionForm.ShowDialog() != DialogResult.OK)
			{
				return 0;
			}
			Engine connectedEngine = simulatorConnectionForm.ConnectedEngine;
			using (simulatorConnectionForm.DatabaseLock)
			{
				if (simulatorConnectionForm.NeedsConfiguration)
				{
					using StationConfigurationForm stationConfigurationForm = new StationConfigurationForm(connectedEngine);
					if (stationConfigurationForm.ShowDialog() != DialogResult.OK)
					{
						return 0;
					}
				}
				bool beginDuty;
				using (StartupForm startupForm = new StartupForm(connectedEngine))
				{
					if (startupForm.ShowDialog() != DialogResult.OK)
					{
						return 0;
					}
					beginDuty = startupForm.BeginDuty;
					connectedEngine.ReadOnlySession = startupForm.ObserveOnly;
				}
				MainForm main = new MainForm(connectedEngine);
				try
				{
					if (beginDuty)
					{
						main.Shown += (object s, EventArgs e) =>
						{
							Ui.AfterFrame(() =>
							{
								Ui.Run(main, () =>
								{
									main.DutyAction();
								});
							});
						};
					}
					if (SessionWindows.Run(main) != DialogResult.Retry)
					{
						return 0;
					}
				}
				finally
				{
					if (main != null)
					{
						((IDisposable)main).Dispose();
					}
				}
			}
		}
	}

	private static string Argument(string[] args, string name)
	{
		int num = Array.IndexOf(args, name);
		if (num < 0 || num + 1 >= args.Length)
		{
			return null;
		}
		return args[num + 1];
	}
}

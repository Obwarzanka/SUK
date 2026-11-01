using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace EdrLocal;

public static class Td2193Tests
{
	private static readonly List<string> log = new List<string>();

	private static void Check(bool ok, string text)
	{
		if (!ok)
		{
			throw new Exception("FAIL: " + text);
		}
		log.Add("PASS: " + text);
	}

	private static T Field<T>(object obj, string name)
	{
		return (T)obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
	}

	private static void Call(object obj, string name)
	{
		try
		{
			obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, null);
		}
		catch (TargetInvocationException ex)
		{
			throw ex.InnerException;
		}
	}

	private static void Wait(Func<bool> done)
	{
		DateTime dateTime = DateTime.UtcNow.AddSeconds(10.0);
		while (!done() && DateTime.UtcNow < dateTime)
		{
			Application.DoEvents();
			Thread.Sleep(5);
		}
		if (!done())
		{
			throw new Exception("Przekroczono czas testu wyboru serwera.");
		}
	}

	private static byte[] Payload(string code)
	{
		Td2Envelope td2Envelope = new Td2Envelope();
		td2Envelope.success = true;
		td2Envelope.respCode = 10;
		td2Envelope.message = new List<Td2Schedule>
		{
			new Td2Schedule
			{
				trainInfo = new Td2TrainInfo
				{
					timetableId = 100,
					trainNo = 10000,
					region = code
				},
				timetablePath = ",Sceneria aaaaaaaa.sc,",
				stopPoints = new List<Td2Stop>
				{
					new Td2Stop
					{
						pointId = "101",
						entryId = 1,
						pointNameRAW = "Stacja",
						departureTime = "2026-10-24T07:00:00Z",
						departureLine = "S-P",
						comments = "I,1"
					},
					new Td2Stop
					{
						pointId = "102",
						entryId = 2,
						pointNameRAW = "Posterunek",
						arrivalTime = "2026-10-24T07:05:00Z",
						arrivalLine = "S-P"
					}
				}
			}
		};
		return Json.Bytes(td2Envelope);
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		bool enabled = LocalPersistence.Enabled;
		LocalPersistence.Enabled = false;
		try
		{
			string[] codes = new string[5] { "eu", "cae", "usw", "us", "ru" };
			string[] array = new string[5] { "PL1", "PL2", "DE", "CZ", "ENG" };
			string requested = null;
			Td2Provider td2Provider = new Td2Provider((string address, CancellationToken token) =>
			{
				if (address == "https://api.td2.info.pl/?method=getTrainsOnline")
				{
					return Json.Bytes(new Td2OnlineEnvelope
					{
						success = true,
						message = new List<Td2OnlineTrain>()
					});
				}
				requested = address;
				return Payload(address.Substring(address.LastIndexOf(';') + 1));
			});
			List<SimRailServer> result = td2Provider.ServersAsync(CancellationToken.None).GetAwaiter().GetResult();
			Check(result.Select((SimRailServer x) => x.ServerCode).SequenceEqual(codes) && result.Select((SimRailServer x) => x.ToString()).SequenceEqual(array), "Lista zawiera PL1, PL2, DE, CZ, ENG w uzgodnionej kolejności i z właściwymi kodami.");
			for (int num = 0; num < codes.Length; num++)
			{
				Check(Td2Provider.Region(array[num]) == codes[num] && Td2Provider.ServerName(codes[num]) == array[num], array[num] + " odpowiada kodowi " + codes[num] + " również przy wpisaniu nazwy ręcznie.");
				SimRailSnapshot result2 = td2Provider.SnapshotAsync(codes[num], CancellationToken.None).GetAwaiter().GetResult();
				Check(requested == "https://api.td2.info.pl/?method=readFromSWDR&value=getTimetables;" + codes[num] && result2.ServerCode == codes[num], array[num] + ": zapytanie i sesja zachowują kod API " + codes[num] + ".");
				Store store = new Store(Path.Combine(folder, "unused"), transient: true);
				store.Load();
				Engine engine = new Engine(store);
				engine.SimRail = new SimRailSession(td2Provider, result2, "101");
				Engine engine2 = engine;
				Check(engine2.SimulatorServerName == array[num], array[num] + ": nazwa sesji przeznaczona do stopki jest przyjazną nazwą.");
				HttpWebRequest httpWebRequest = Td2Provider.CreateRequest(Td2Provider.Address(codes[num]));
				Check(httpWebRequest.Method == "GET" && string.IsNullOrEmpty(httpWebRequest.UserAgent), array[num] + ": żądanie pozostaje GET bez User-Agent.");
				httpWebRequest.Abort();
			}
			SimulatorConnectionForm form = new SimulatorConnectionForm(td2Provider, new ConfiguratorRepository("td2", Path.Combine(folder, "configuration")), Path.Combine(folder, "session"));
			try
			{
				form.Show();
				Wait(() => Field<ComboBox>(form, "servers").Items.Count == 5);
				ComboBox comboBox = Field<ComboBox>(form, "servers");
				Check((from SimRailServer x in comboBox.Items
					select x.ToString()).SequenceEqual(array), "Rzeczywiste okno połączenia pokazuje pięć przyjaznych nazw.");
				for (int num2 = 0; num2 < codes.Length; num2++)
				{
					comboBox.SelectedIndex = num2;
					Call(form, "LoadSnapshot");
					int index = num2;
					Wait(() => !Field<bool>(form, "loading") && Field<SimRailSnapshot>(form, "snapshot") != null && Field<SimRailSnapshot>(form, "snapshot").ServerCode == codes[index]);
					Check(requested.EndsWith(";" + codes[num2]) && Field<ListBox>(form, "available").Items.Count == 2, "Wybranie " + array[num2] + " pobiera posterunki właściwego serwera.");
				}
				Ui.Capture(form, Path.Combine(folder, "td2-serwery.png"));
				form.Close();
				Application.DoEvents();
			}
			finally
			{
				if (form != null)
				{
					((IDisposable)form).Dispose();
				}
			}
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			File.WriteAllLines(Path.Combine(folder, "wyniki-td2193.txt"), log, Encoding.UTF8);
			return false;
		}
		finally
		{
			LocalPersistence.Enabled = enabled;
		}
		File.WriteAllLines(Path.Combine(folder, "wyniki-td2193.txt"), log, Encoding.UTF8);
		return true;
	}
}

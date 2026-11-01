using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EdrLocal;

public static class Counter26Tests
{
	private sealed class Feed : IAccidentCounterFeed
	{
		public int Calls;

		public AccidentCounterReading Reading;

		public bool Fail;

		public Task<AccidentCounterReading> Pending;

		public string Requested;

		public Task<AccidentCounterReading> FetchAsync(string simulator, CancellationToken token)
		{
			Calls++;
			Requested = simulator;
			if (Fail)
			{
				throw new IOException("test");
			}
			return Pending ?? Task.FromResult(Reading);
		}
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

	private static void Reject(Action action, string text)
	{
		bool ok = false;
		try
		{
			action();
		}
		catch
		{
			ok = true;
		}
		Check(ok, text);
	}

	private static AccidentCounterReading Reading(long epoch, long reset, long revision, string simulator)
	{
		AccidentCounterReading accidentCounterReading = new AccidentCounterReading();
		accidentCounterReading.SchemaVersion = 1;
		accidentCounterReading.Simulator = simulator;
		accidentCounterReading.ServerEpochSeconds = epoch;
		accidentCounterReading.LastResetEpochSeconds = reset;
		accidentCounterReading.Revision = revision;
		return accidentCounterReading;
	}

	private static bool Poll(AccidentCounterClient client)
	{
		return client.PollIfDueAsync(CancellationToken.None).GetAwaiter().GetResult();
	}

	private static long Unix(DateTime date)
	{
		return (long)(date - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		try
		{
			DateTime utc = default;
			ref DateTime reference = ref utc;
			reference = new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
			TimeSpan elapsed = TimeSpan.Zero;
			long now = Unix(utc);
			Feed feed = new Feed();
			feed.Reading = Reading(now, now - 172800, 3L, "simrail");
			Feed feed2 = feed;
			string path = Path.Combine(folder, "cache", "simrail.json");
			AccidentCounterClient accidentCounterClient = new AccidentCounterClient("simrail", feed2, path, () => utc, () => elapsed);
			Check(accidentCounterClient.ResetUtc == new DateTime(2026, 10, 17, 22, 0, 0, DateTimeKind.Utc) && accidentCounterClient.Source == "Data domyślna", "Przed pierwszym odczytem używana jest północ 18.10.2026 Europe/Warsaw.");
			Check(Poll(accidentCounterClient) && feed2.Calls == 1 && feed2.Requested == "simrail" && accidentCounterClient.Revision == 3, "Pierwszy odczyt pobiera od razu licznik SimRail.");
			Check(File.Exists(path) && accidentCounterClient.Source == "API" && accidentCounterClient.Duration.TotalDays == 2.0, "Poprawna data jest zapisana na dysku i pokazuje 2 dni.");
			elapsed = TimeSpan.FromSeconds(59.0);
			Check(!Poll(accidentCounterClient) && feed2.Calls == 1, "Przed upływem 60 sekund nie ma ponownego żądania.");
			elapsed = TimeSpan.FromSeconds(60.0);
			feed2.Reading = Reading(now + 60, now + 50, 4L, "simrail");
			Check(Poll(accidentCounterClient) && feed2.Calls == 2 && accidentCounterClient.Duration.TotalSeconds == 10.0, "Po 60 sekundach reset w API zmienia licznik.");
			elapsed += TimeSpan.FromSeconds(5.0);
			Check(accidentCounterClient.Duration.TotalSeconds == 15.0, "Między odczytami licznik rośnie lokalnie co sekundę.");
			utc = utc.AddYears(10);
			Check(accidentCounterClient.Duration.TotalSeconds == 15.0, "Zmiana zegara systemowego po synchronizacji nie przestawia czasu licznika.");
			elapsed = TimeSpan.FromSeconds(120.0);
			feed2.Reading = Reading(now + 120, now - 3600, 5L, "simrail");
			Check(Poll(accidentCounterClient) && accidentCounterClient.Duration.TotalSeconds == 3720.0, "Ręczne cofnięcie daty w bocie jest uwzględniane przez EDR.");
			DateTime resetUtc = accidentCounterClient.ResetUtc;
			string text = File.ReadAllText(path);
			elapsed = TimeSpan.FromSeconds(180.0);
			feed2.Fail = true;
			Check(!Poll(accidentCounterClient) && accidentCounterClient.ResetUtc == resetUtc && File.ReadAllText(path) == text, "Błąd sieci zachowuje datę w pamięci i na dysku.");
			elapsed = TimeSpan.FromSeconds(239.0);
			Check(!Poll(accidentCounterClient) && feed2.Calls == 4, "Po błędzie kolejne sprawdzenie nadal czeka do następnej minuty.");
			ref DateTime reference2 = ref utc;
			reference2 = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
			AccidentCounterClient accidentCounterClient2 = new AccidentCounterClient("simrail", feed2, path, () => utc, () => elapsed);
			Check(accidentCounterClient2.Source == "Zapamiętana data" && accidentCounterClient2.ResetUtc == resetUtc && accidentCounterClient2.Duration.TotalHours > 24.0, "Restart wczytuje datę i uwzględnia czas, gdy EDR był wyłączony.");
			Check(!Poll(accidentCounterClient2) && accidentCounterClient2.ResetUtc == resetUtc, "Brak sieci po restarcie nie zeruje zapisanej daty.");
			elapsed = TimeSpan.FromSeconds(240.0);
			feed2.Fail = false;
			feed2.Reading = Reading(now + 240, now + 230, 6L, "simrail");
			Check(Poll(accidentCounterClient) && accidentCounterClient.LastError == null && accidentCounterClient.Revision == 6, "Powrót połączenia przywraca aktualny stan API.");
			resetUtc = accidentCounterClient.ResetUtc;
			elapsed = TimeSpan.FromSeconds(300.0);
			feed2.Reading = Reading(now + 300, now + 280, 7L, "td2");
			Check(!Poll(accidentCounterClient) && accidentCounterClient.ResetUtc == resetUtc && accidentCounterClient.Revision == 6, "Odpowiedź drugiego symulatora jest odrzucona bez zmiany licznika.");
			elapsed = TimeSpan.FromSeconds(360.0);
			feed2.Reading = Reading(now + 360, now + 355, 8L, "simrail");
			feed2.Reading.SchemaVersion = 2;
			Check(!Poll(accidentCounterClient) && accidentCounterClient.ResetUtc == resetUtc, "Nieobsługiwana wersja API zachowuje ostatnią poprawną datę.");
			Reject(() =>
			{
				AccidentCounterClient.Parse(Encoding.UTF8.GetBytes("{\"schemaVersion\":1}"));
			}, "Niepełny JSON bez wymaganych pól jest odrzucony.");
			Reject(() =>
			{
				AccidentCounterClient.Validate(Reading(now, long.MaxValue, 1L, "simrail"), "simrail");
			}, "Nieprawidłowy epoch poza zakresem dat jest odrzucony.");
			Reject(() =>
			{
				AccidentCounterClient.Validate(Reading(now, now + 1000, 1L, "simrail"), "simrail");
			}, "Nieprawidłowy początek w dalekiej przyszłości jest odrzucony.");
			AccidentCounterClient accidentCounterClient3 = new AccidentCounterClient("td2", new Feed
			{
				Reading = Reading(now, 0L, 1L, "td2")
			}, null, () => utc, () => TimeSpan.Zero);
			Check(Poll(accidentCounterClient3) && accidentCounterClient3.ResetUtc.Year == 1970, "Epoch 0 jest poprawną datą i nie uruchamia wartości domyślnej.");
			AccidentCounterClient accidentCounterClient4 = new AccidentCounterClient("td2", new Feed
			{
				Fail = true
			}, path, () => utc, () => TimeSpan.Zero);
			Check(!accidentCounterClient4.HasReading && accidentCounterClient4.ResetUtc == new DateTime(2026, 10, 17, 22, 0, 0, DateTimeKind.Utc), "Cache SimRail nie może zasilić licznika TD2.");
			string path2 = Path.Combine(folder, "corrupt.json");
			File.WriteAllText(path2, "broken");
			AccidentCounterClient accidentCounterClient5 = new AccidentCounterClient("simrail", new Feed
			{
				Fail = true
			}, path2, () => utc, () => TimeSpan.Zero);
			Check(!Poll(accidentCounterClient5) && !accidentCounterClient5.HasReading && accidentCounterClient5.ResetUtc == new DateTime(2026, 10, 17, 22, 0, 0, DateTimeKind.Utc) && File.ReadAllText(path2) == "broken", "Uszkodzony cache bez sieci daje datę domyślną bez niszczenia pliku.");
			TaskCompletionSource<AccidentCounterReading> taskCompletionSource = new TaskCompletionSource<AccidentCounterReading>();
			Feed feed3 = new Feed();
			feed3.Pending = taskCompletionSource.Task;
			Feed feed4 = feed3;
			AccidentCounterClient accidentCounterClient6 = new AccidentCounterClient("simrail", feed4, null, () => utc, () => TimeSpan.Zero);
			Task<bool> task = accidentCounterClient6.PollIfDueAsync(CancellationToken.None);
			Check(!task.IsCompleted && !Poll(accidentCounterClient6) && feed4.Calls == 1, "Odczyt jest asynchroniczny i nie uruchamia równoległych żądań.");
			taskCompletionSource.SetResult(Reading(now, now - 60, 1L, "simrail"));
			Check(task.GetAwaiter().GetResult(), "Zakończenie odczytu w tle przyjmuje wynik.");
			CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
			cancellationTokenSource.Cancel();
			Feed feed5 = new Feed();
			feed5.Reading = Reading(now, now, 1L, "simrail");
			Feed feed6 = feed5;
			AccidentCounterClient accidentCounterClient7 = new AccidentCounterClient("simrail", feed6, null, () => utc, () => TimeSpan.Zero);
			Check(!accidentCounterClient7.PollIfDueAsync(cancellationTokenSource.Token).GetAwaiter().GetResult() && feed6.Calls == 0, "Zamknięcie okna anuluje sprawdzanie przed transportem.");
			CancellationTokenSource cancellationTokenSource2 = new CancellationTokenSource();
			taskCompletionSource = new TaskCompletionSource<AccidentCounterReading>();
			AccidentCounterClient accidentCounterClient8 = new AccidentCounterClient("simrail", new Feed
			{
				Pending = taskCompletionSource.Task
			}, null, () => utc, () => TimeSpan.Zero);
			Task<bool> task2 = accidentCounterClient8.PollIfDueAsync(cancellationTokenSource2.Token);
			cancellationTokenSource2.Cancel();
			taskCompletionSource.SetResult(Reading(now, now, 1L, "simrail"));
			Check(!task2.GetAwaiter().GetResult() && !accidentCounterClient8.HasReading, "Wynik otrzymany po anulowaniu nie zmienia daty.");
			string text2 = Path.Combine(folder, "not-a-folder");
			File.WriteAllText(text2, "keep");
			AccidentCounterClient accidentCounterClient9 = new AccidentCounterClient("simrail", new Feed
			{
				Reading = Reading(now, now - 60, 1L, "simrail")
			}, Path.Combine(text2, "cache.json"), () => utc, () => TimeSpan.Zero);
			Check(Poll(accidentCounterClient9) && accidentCounterClient9.HasReading && accidentCounterClient9.LastError != null && accidentCounterClient9.Duration.TotalSeconds == 60.0, "Brak możliwości zapisu cache nie blokuje poprawnego odczytu API.");
			Check(AccidentCounterHttpFeed.Address("simrail").AbsoluteUri == "https://edr-licznik.jever.pl/api/v1/counters/simrail" && AccidentCounterHttpFeed.Address("td2").AbsoluteUri.EndsWith("/td2"), "Oba adresy wskazują wdrożone publiczne API bez sekretów.");
			AccidentCounterReading accidentCounterReading = AccidentCounterClient.Parse(Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"simulator\":\"simrail\",\"serverEpochSeconds\":" + now + ",\"lastResetEpochSeconds\":1792274400,\"revision\":0,\"source\":\"default\",\"elapsedSeconds\":136800,\"serverTime\":\"2026-10-19T12:00:00.000Z\"}"));
			AccidentCounterClient.Validate(accidentCounterReading, "simrail");
			Check(accidentCounterReading.LastResetEpochSeconds == 1792274400, "Kontrakt JSON bota jest odczytywany wraz z dodatkowymi polami.");
			AccidentCounterClient accidentCounterClient10 = new AccidentCounterClient("simrail", new Feed
			{
				Reading = Reading(now, now - 172800, 11L, "simrail")
			}, null, () => utc, () => TimeSpan.Zero);
			Poll(accidentCounterClient10);
			Store store = new Store(Path.Combine(folder, "ui"), transient: true);
			store.Load();
			Engine engine = new Engine(store);
			engine.User = store.State.Users.First((LocalUser x) => x.Id == "DEMO");
			Engine engine2 = engine;
			engine2.SimRail = new SimRailSession(null, new SimRailSnapshot
			{
				ServerCode = "test",
				Trains = new List<SimRailTrain>(),
				GameTime = new DateTime(2050, 1, 1),
				DownloadedUtc = DateTime.UtcNow
			}, "60");
			using (MainForm mainForm = new MainForm(engine2, accidentCounterClient10))
			{
				mainForm.Show();
				Application.DoEvents();
				Check(mainForm.AccidentCounterCaption == "Czas bez wypadku spow. przez pracownika PLK" && mainForm.AccidentCounterDuration.TotalDays == 2.0 && engine2.Now.Year == 2050, "Nagłówek SimRail liczy rzeczywiste 2 dni mimo innego zegara gry.");
				TabControl tabControl = (TabControl)typeof(MainForm).GetField("topTabs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mainForm);
				tabControl.SelectedIndex = 1;
				Application.DoEvents();
				Control control = (Control)typeof(MainForm).GetField("headerClock", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mainForm);
				Check(!control.Visible, "Licznik i zegar pozostają widoczne tylko w wykazie pociągów.");
				tabControl.SelectedIndex = 0;
				Application.DoEvents();
				Ui.Capture(mainForm, Path.Combine(folder, "licznik-simrail.png"));
				mainForm.RefreshData();
				Check(accidentCounterClient10.Duration.TotalDays == 2.0, "Odświeżenie rozkładu nie zeruje licznika.");
				mainForm.Dispose();
			}
			log.Add("ALL TESTS PASSED: " + log.Count);
			File.WriteAllLines(Path.Combine(folder, "wyniki-licznik26.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
			return true;
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			File.WriteAllLines(Path.Combine(folder, "wyniki-licznik26.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
			return false;
		}
	}
}

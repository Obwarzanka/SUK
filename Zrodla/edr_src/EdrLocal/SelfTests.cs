using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace EdrLocal;

internal static class SelfTests
{
	private static List<string> results = new List<string>();

	private static void Assert(bool condition, string message)
	{
		if (!condition)
		{
			throw new Exception("FAIL: " + message);
		}
		results.Add("PASS: " + message);
	}

	private static void Rejected(Action action, string message)
	{
		bool condition = false;
		try
		{
			action();
		}
		catch
		{
			condition = true;
		}
		Assert(condition, message);
	}

	public static void Run(string target)
	{
		Directory.CreateDirectory(target);
		string text = Path.Combine(target, "case-" + Guid.NewGuid().ToString("N"));
		Store store = new Store(text);
		store.Load();
		Engine e = new Engine(store)
		{
			User = store.State.Users.First((LocalUser x) => x.Id == "DEMO")
		};
		try
		{
			Assert(e.State.Trains.Count == 38, "Przykładowy rozkład zawiera 38 pociągów.");
			Assert(e.User.Verify("demo") && !e.User.Verify("zle"), "Sprawdzanie hasła lokalnego.");
			Train t = e.State.Trains[0];
			Section section = e.Station.Sections[0];
			JournalEntry entry = e.Note(section.Id, DateTime.Now, "Test", "Pociąg");
			entry.TrainId = t.Id;
			entry.Number = e.TrainNumber(t, section, "Z");
			entry.Direction = "Z";
			entry.Track = "1";
			entry.Left = DateTime.Now.AddMinutes(-2.0);
			Rejected(() =>
			{
				e.SaveJournal(entry);
			}, "Zapis przed przyjęciem dyżuru jest odrzucany.");
			e.BeginDuty(DateTime.Now.AddMinutes(-15.0));
			Assert(e.CanWrite && e.State.Entries.Count == 4, "Przyjęcie dyżuru dodaje wpis do trzech dzienników i telefonicznego.");
			Rejected(() =>
			{
				e.BeginDuty(DateTime.Now);
			}, "Nie można przyjąć drugiego równoległego dyżuru.");
			e.ReadOnlySession = true;
			Rejected(() =>
			{
				e.SaveJournal(entry);
			}, "Wybór obserwowania dyżuru blokuje zapis także operatorowi.");
			e.ReadOnlySession = false;
			e.SaveJournal(entry);
			Assert(e.LineStatus(section.Id, 1).StartsWith("ZAJĘTY"), "Odjazd bez przyjazdu zajmuje tor szlakowy.");
			Assert(e.TrackStatus("1").StartsWith("REZERWACJA"), "Przyjęcie bez przyjazdu rezerwuje tor stacyjny.");
			JournalEntry duplicate = Json.Clone(entry);
			duplicate.Id = Guid.NewGuid().ToString("N");
			Rejected(() =>
			{
				e.SaveJournal(duplicate);
			}, "Duplikat aktywnego przyjęcia jest odrzucany.");
			entry.Arrived = DateTime.Now.AddMinutes(-1.0);
			e.SaveJournal(entry);
			Assert(e.LineStatus(section.Id, 1) == "WOLNY" && e.TrackStatus("1").StartsWith("ZAJĘTY"), "Przyjazd zwalnia szlak i zajmuje tor stacyjny.");
			Assert(e.State.Trains.First((Train x) => x.Id == t.Id).Arrival.HasValue, "Czas przyjazdu synchronizuje się z rozkładem.");
			JournalEntry journalEntry = e.Note(e.Station.Sections[1].Id, DateTime.Now, "", "Pociąg");
			journalEntry.TrainId = t.Id;
			journalEntry.Number = e.TrainNumber(t, e.Station.Sections[1], "DO");
			journalEntry.Direction = "DO";
			journalEntry.Track = "1";
			journalEntry.Left = DateTime.Now;
			e.SaveJournal(journalEntry);
			Assert(e.TrackStatus("1") == "WOLNY", "Wyjazd pociągu zwalnia tor stacyjny pomimo zmiany parzystości numeru.");
			JournalEntry telephone = e.Note(section.Id, DateTime.Now, "Tor zamknięty", "Telefonogram");
			telephone.Effect = "Zamknij";
			e.SaveJournal(telephone);
			Assert(e.LineStatus(section.Id, 1) == "ZAMKNIĘTY", "Telefonogram zamykający zmienia stan toru.");
			e.Cancel(telephone.Id, "Błędny tor");
			Assert(e.LineStatus(section.Id, 1) == "WOLNY", "Wykreślenie telefonogramu przywraca stan toru.");
			Assert(e.State.Entries.First((JournalEntry x) => x.Id == telephone.Id).Text == "Tor zamknięty", "Wykreślenie zachowuje oryginalną treść.");
			JournalEntry bad = Json.Clone(entry);
			bad.Arrived = bad.Left.Value.AddMinutes(-1.0);
			Rejected(() =>
			{
				e.SaveJournal(bad);
			}, "Odwrócona chronologia jazdy jest odrzucana.");
			JournalEntry journalEntry2 = Json.Clone(entry);
			journalEntry2.Left = DateTime.Today.AddHours(23.0).AddMinutes(58.0);
			journalEntry2.Arrived = DateTime.Today.AddDays(1.0).AddMinutes(3.0);
			Engine.ValidateTimes(journalEntry2);
			Assert(condition: true, "Jazda przez północ jest poprawnie akceptowana.");
			string csv = Path.Combine(text, "test.csv");
			List<string[]> list = new List<string[]>();
			list.Add(new string[8] { "Numer", "Rodzaj", "Przyjazd", "Odjazd", "Z kierunku", "W kierunku", "Tor", "Przewoźnik" });
			list.Add(new string[8]
			{
				"12345",
				"ROJ",
				DateTime.Today.ToString("yyyy-MM-dd") + " 12:00",
				DateTime.Today.ToString("yyyy-MM-dd") + " 12:02",
				section.Name,
				e.Station.Sections[1].Name,
				"1",
				"Przewoźnik; \"test\""
			});
			List<string[]> rows = list;
			Csv.Write(csv, rows);
			List<Train> list2 = Csv.Import(csv, e.Station);
			Assert(list2.Count == 1 && list2[0].Carrier == "Przewoźnik; \"test\"", "CSV obsługuje średnik i cudzysłowy wewnątrz pola.");
			File.AppendAllText(csv, "\r\nnieprawidłowy;wiersz");
			int count = e.State.Trains.Count;
			Rejected(() =>
			{
				Csv.Import(csv, e.Station);
			}, "Błędny CSV odrzucany w całości.");
			Assert(e.State.Trains.Count == count, "Nieudany import nie zmienia rozkładu.");
			string text2 = Json.Text(e.State);
			Rejected(() =>
			{
				store.Commit((AppState s) =>
				{
					s.Trains.Clear();
					throw new Exception("symulowany błąd");
				});
			}, "Transakcja przerywa nieudaną zmianę.");
			Assert(Json.Text(e.State) == text2, "Nieudana zmiana nie narusza danych w pamięci.");
			Train manual = e.State.Trains[2];
			DateTime realArrival = DateTime.Now.AddMinutes(-10.0);
			e.Change("Test czasu wpisanego w rozkładzie", manual.Id, (AppState s) =>
			{
				s.Trains.First((Train x) => x.Id == manual.Id).Arrival = realArrival;
			});
			JournalEntry journalEntry3 = e.Note(section.Id, DateTime.Now, "", "Pociąg");
			journalEntry3.TrainId = manual.Id;
			journalEntry3.Number = manual.Number;
			journalEntry3.Direction = "DO";
			journalEntry3.Left = DateTime.Now;
			journalEntry3.Track = "3";
			e.SaveJournal(journalEntry3);
			Assert(e.State.Trains.First((Train x) => x.Id == manual.Id).Arrival.HasValue, "Zapis wyjazdu nie usuwa przyjazdu wpisanego wcześniej w rozkładzie.");
			entry.Arrived = DateTime.Now.AddMinutes(-10.0);
			entry.Left = DateTime.Now.AddMinutes(-12.0);
			e.SaveJournal(entry);
			journalEntry.Left = DateTime.Now.AddMinutes(-5.0);
			e.SaveJournal(journalEntry);
			Assert(e.TrackStatus("1") == "WOLNY", "Zajętość uwzględnia rzeczywiste czasy, także wpisane z opóźnieniem.");
			JournalEntry journalEntry4 = e.Note(section.Id, DateTime.Now, "Zamknięcie", "Telefonogram");
			journalEntry4.Effect = "Zamknij";
			e.SaveJournal(journalEntry4);
			JournalEntry journalEntry5 = e.Note(section.Id, journalEntry4.Time, "Otwarcie", "Telefonogram");
			journalEntry5.Effect = "Otwórz";
			e.SaveJournal(journalEntry5);
			Assert(e.LineStatus(section.Id, 1) != "ZAMKNIĘTY", "Przy jednakowym czasie decyduje ostatni wpis o otwarciu toru.");
			Store store2 = new Store(text);
			store2.Load();
			Assert(store2.State.Entries.Count == e.State.Entries.Count && store2.State.Duties.Any((Duty x) => !x.End.HasValue), "Dzienniki i aktywny dyżur przetrwały ponowne wczytanie.");
			Assert(File.Exists(store.FilePath + ".bak"), "Poprzednia wersja danych jest zachowana jako kopia.");
			Assert(e.State.History.All((Audit x) => !x.Before.Contains(e.User.Hash) && !x.After.Contains(e.User.Hash)), "Historia nie zawiera skrótów haseł.");
			Assert(e.State.History.All((Audit x) => !x.Before.Contains("Przyjęcie dyżuru\\\"") && !x.After.Contains("Przyjęcie dyżuru\\\"")), "Migawki audytu nie zagnieżdżają poprzednich historii.");
			LocalUser user = e.User;
			e.User = e.State.Users.First((LocalUser x) => x.Role == "Obserwator");
			Rejected(() =>
			{
				e.SaveJournal(entry);
			}, "Obserwator nie może modyfikować dziennika.");
			Rejected(() =>
			{
				e.BeginDuty(DateTime.Now);
			}, "Obserwator nie może przyjąć dyżuru.");
			Rejected(() =>
			{
				e.RequireCoordinator();
			}, "Obserwator nie ma dostępu do konfiguracji.");
			e.User = user;
			e.EndDuty(DateTime.Now);
			Assert(!e.CanWrite, "Zdanie dyżuru wyłącza zapis.");
			string corrupted = Path.Combine(text, "corrupt");
			Directory.CreateDirectory(corrupted);
			File.WriteAllText(Path.Combine(corrupted, "edr.json"), "broken");
			Rejected(() =>
			{
				new Store(corrupted).Load();
			}, "Uszkodzona baza nie jest automatycznie nadpisywana.");
			Assert(File.ReadAllText(Path.Combine(corrupted, "edr.json")) == "broken", "Oryginał uszkodzonej bazy pozostał bez zmian.");
			results.Add("ALL TESTS PASSED: " + results.Count);
			File.WriteAllLines(Path.Combine(target, "wyniki-testow.txt"), results, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
		catch (Exception ex)
		{
			results.Add(ex.ToString());
			File.WriteAllLines(Path.Combine(target, "wyniki-testow.txt"), results, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
			throw;
		}
	}
}

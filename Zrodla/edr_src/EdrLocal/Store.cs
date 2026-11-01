using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EdrLocal;

public class Store
{
	public string DirectoryPath { get; private set; }

	public string FilePath => Path.Combine(DirectoryPath, "edr.json");

	public AppState State { get; private set; }

	public bool IsTransient { get; private set; }

	internal event Action Committed;

	public Store(string directory)
	{
		DirectoryPath = Path.GetFullPath(directory);
		IsTransient = !LocalPersistence.Enabled;
	}

	public void Load()
	{
		if (!LocalPersistence.Enabled)
		{
			IsTransient = true;
		}
		if (IsTransient)
		{
			ResetSession();
			return;
		}
		Directory.CreateDirectory(DirectoryPath);
		if (File.Exists(FilePath))
		{
			State = Json.Read<AppState>(File.ReadAllBytes(FilePath));
			Validate(State);
		}
		else
		{
			State = Seed();
			Save(State);
		}
	}

	public static void Validate(AppState s)
	{
		if (s == null || s.Version != 1 || s.Stations == null || s.Users == null || s.Entries == null || s.Trains == null || s.History == null || s.Duties == null || s.Services == null)
		{
			throw new Exception("Nieobsługiwany lub uszkodzony plik danych. Zachowano oryginał. Kopia: edr.json.bak.");
		}
		if (s.Users.Count == 0 || s.Stations.Count == 0)
		{
			throw new Exception("Brak użytkowników lub posterunków w bazie.");
		}
	}

	public void Commit(Action<AppState> action)
	{
		AppState appState = StateCopies.Transaction(State);
		TelephoneNumbering.Ensure(appState);
		action(appState);
		TelephoneNumbering.Ensure(appState);
		Validate(appState);
		if (!IsTransient)
		{
			Save(appState);
		}
		State = appState;
		Committed?.Invoke();
	}

	private void Save(AppState state)
	{
		if (!LocalPersistence.Enabled)
		{
			return;
		}
		string text = FilePath + ".tmp";
		try
		{
			using (FileStream fileStream = new FileStream(text, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				byte[] array = Json.Bytes(state);
				fileStream.Write(array, 0, array.Length);
				fileStream.Flush(flushToDisk: true);
			}
			if (File.Exists(FilePath))
			{
				File.Replace(text, FilePath, FilePath + ".bak", ignoreMetadataErrors: true);
			}
			else
			{
				File.Move(text, FilePath);
			}
		}
		catch
		{
			if (File.Exists(text))
			{
				File.Delete(text);
			}
			throw;
		}
	}

	public void Flush()
	{
		Validate(State);
		if (!IsTransient)
		{
			Save(State);
		}
	}

	public static AppState Seed()
	{
		LocalUser localUser = new LocalUser();
		localUser.Id = "DEMO";
		localUser.Name = "Jan Kowalski";
		localUser.Role = "Koordynator";
		LocalUser localUser2 = localUser;
		localUser2.SetPassword("demo");
		LocalUser localUser3 = new LocalUser();
		localUser3.Id = "PODGLAD";
		localUser3.Name = "Obserwator szkoleniowy";
		localUser3.Role = "Obserwator";
		LocalUser localUser4 = localUser3;
		localUser4.SetPassword("demo");
		Station station = new Station();
		station.Id = "JS";
		station.Name = "Jaworzyna Śląska";
		station.Tracks = new List<string> { "1", "2", "3", "4", "5", "6", "7", "8" };
		station.Checks = new List<string> { "Sprawdzenie łączności", "Sprawdzenie urządzeń" };
		station.Sections = new List<Section>
		{
			new Section
			{
				Id = "ZAR",
				Name = "Żarów",
				Line = "274",
				Tracks = 2,
				NormalIncomingTrack = 1,
				ArrivalEven = true,
				Basis = "Blokada samoczynna"
			},
			new Section
			{
				Id = "SWI",
				Name = "Świebodzice",
				Line = "274",
				Tracks = 2,
				NormalIncomingTrack = 2,
				ArrivalEven = false,
				Basis = "Blokada samoczynna"
			},
			new Section
			{
				Id = "SWM",
				Name = "Świdnica Miasto",
				Line = "137",
				Tracks = 1,
				ArrivalEven = true,
				Basis = "Zapowiadanie telefoniczne"
			}
		};
		Station station2 = station;
		AppState appState = new AppState();
		appState.Version = 1;
		appState.Users = new List<LocalUser> { localUser2, localUser4 };
		appState.Stations = new List<Station> { station2 };
		appState.Trains = new List<Train>();
		appState.Entries = new List<JournalEntry>();
		appState.Duties = new List<Duty>();
		appState.History = new List<Audit>();
		appState.Services = new List<ServiceRecord>();
		appState.Layout = "Dół";
		AppState appState2 = appState;
		AddDemo(appState2, station2.Id, DateTime.Today);
		return appState2;
	}

	public static void AddDemo(AppState s, string stationId, DateTime day)
	{
		if (s.Trains.Any((Train t) => t.StationId == stationId && t.PlanArrival.Date == day.Date))
		{
			throw new Exception("Na ten dzień istnieją już pociągi. Wybierz inny dzień albo zaimportuj rozkład.");
		}
		Station station = s.Stations.First((Station x) => x.Id == stationId);
		if (station.Sections.Count < 1)
		{
			throw new Exception("Najpierw skonfiguruj szlaki.");
		}
		string[] array = new string[8] { "ROJ", "MPE", "ROJ", "TME", "EIP", "ROJ", "TME", "MPE" };
		string[] array2 = new string[4] { "Koleje Dolnośląskie", "PKP Intercity", "Polregio", "PKP CARGO" };
		for (int num = 0; num < 38; num++)
		{
			Section section = station.Sections[num % station.Sections.Count];
			Section section2 = station.Sections[(num + 1) % station.Sections.Count];
			DateTime planArrival = day.Date.AddHours(5.0).AddMinutes(num * 23 + 7);
			s.Trains.Add(new Train
			{
				Id = Guid.NewGuid().ToString("N"),
				StationId = stationId,
				Number = ((num == 18) ? "70413" : ((num % 4 == 3) ? (66000 + num) : (69000 + num)).ToString()),
				Kind = array[num % 8],
				From = section.Name,
				To = section2.Name,
				Origin = ((num % 2 == 0) ? "Wrocław Główny" : "Jelenia Góra"),
				Destination = ((num % 2 == 0) ? "Jelenia Góra" : "Wrocław Główny"),
				Carrier = array2[num % 4],
				LineIn = section.Line,
				LineOut = section2.Line,
				Track = ((station.Tracks.Count == 0) ? "" : station.Tracks[num % station.Tracks.Count]),
				Platform = (1 + num % 3).ToString(),
				PlanArrival = planArrival,
				PlanDeparture = planArrival.AddMinutes((num % 3 != 0) ? 2 : 0),
				Profile = ((num == 18) ? new TrainProfile
				{
					Name = "Janiczyk"
				} : null),
				Runs = (num % 13 != 0),
				Dangerous = (num == 7),
				HighRisk = (num == 15),
				Extraordinary = (num == 11),
				Cargo = ((num == 7) ? "Przykładowe dane szkoleniowe TN" : ""),
				Notes = "",
				Analysis = "",
				DelayReason = ""
			});
		}
	}

	public Store(string directory, bool transient)
		: this(directory)
	{
		IsTransient = transient || !LocalPersistence.Enabled;
	}

	public void ResetSession()
	{
		if (!IsTransient)
		{
			throw new Exception("Reset sesji jest dostępny w trybie ćwiczeniowym.");
		}
		State = SeedSession(DateTime.Now);
	}

	public static AppState SeedSession(DateTime now)
	{
		AppState appState = Seed();
		Random random = new Random(Guid.NewGuid().GetHashCode());
		HashSet<string> hashSet = new HashSet<string>();
		hashSet.Add("70413");
		HashSet<string> hashSet2 = hashSet;
		DateTime dateTime = now.AddHours(-3.0);
		for (int i = 0; i < appState.Trains.Count; i++)
		{
			Train train = appState.Trains[i];
			string text;
			do
			{
				text = (((train.Kind ?? "").StartsWith("T") ? 66000 : 69000) + random.Next(1, 750) * 2 + i % 2).ToString();
			}
			while (!hashSet2.Add(text));
			train.Number = ((i == 18) ? "70413" : text);
			train.PlanArrival = dateTime.AddMinutes(i * 10 + random.Next(0, 4));
			train.PlanDeparture = train.PlanArrival.AddMinutes(random.Next(0, 3) * 2);
			train.Track = ((appState.Stations[0].Tracks.Count == 0) ? "" : appState.Stations[0].Tracks[random.Next(appState.Stations[0].Tracks.Count)]);
			train.Notes = "";
		}
		return appState;
	}

	public void SetUiZoom(float value)
	{
		if (float.IsNaN(value) || float.IsInfinity(value) || (value != 0f && (value < 0.75f || value > 2f)))
		{
			throw new ArgumentOutOfRangeException("value");
		}
		float uiZoom = State.UiZoom;
		State.UiZoom = value;
		try
		{
			if (!IsTransient)
			{
				Save(State);
			}
		}
		catch
		{
			State.UiZoom = uiZoom;
			throw;
		}
	}
}

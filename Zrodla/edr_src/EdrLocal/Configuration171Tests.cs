using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace EdrLocal;

internal static class Configuration171Tests
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

	private static StationProfile Profile(Station station, string point, string title)
	{
		Station station2 = Json.Clone(station);
		station2.SimRailPointId = point;
		station2.Name = title;
		StationProfile stationProfile = new StationProfile();
		stationProfile.SchemaVersion = 1;
		stationProfile.PointId = point;
		stationProfile.SavedUtc = DateTime.UtcNow;
		stationProfile.Station = station2;
		return stationProfile;
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		LocalPersistence.Enabled = false;
		try
		{
			string text = Path.Combine(folder, "case-" + Guid.NewGuid().ToString("N"));
			string text2 = Path.Combine(text, "SimKol", "EDR", "posterunki", "simrail");
			string text3 = Path.Combine(text, "EDR", "posterunki", "simrail");
			Directory.CreateDirectory(text2);
			Directory.CreateDirectory(text3);
			Store store = new Store(Path.Combine(text, "session"), transient: true);
			store.Load();
			Station station = Json.Clone(store.State.Stations[0]);
			StationSettings.Normalize(station);
			station.SimulatorReviewed = true;
			station.SimulatorTopologyVersion = 2;
			station.RouteNeighboursVersion = 1;
			foreach (Section section in station.Sections)
			{
				section.ManualConfiguration = true;
			}
			station.Options.AutomaticReleaseSeconds = 123;
			StationProfile stationProfile = Profile(station, "1852", "Korytów — zachowana konfiguracja");
			byte[] array = Json.Bytes(stationProfile);
			File.WriteAllBytes(Path.Combine(text2, "1852.json"), array);
			File.WriteAllBytes(Path.Combine(text2, "1853.json"), Json.Bytes(Profile(station, "1853", "Starsza konfiguracja")));
			byte[] array2 = Json.Bytes(Profile(station, "1853", "Nowsza konfiguracja EDR"));
			File.WriteAllBytes(Path.Combine(text3, "1853.json"), array2);
			StationProfile value = Profile(station, "999", "Inny posterunek");
			File.WriteAllBytes(Path.Combine(text2, "9999.json"), Json.Bytes(value));
			File.WriteAllText(Path.Combine(text2, "9998.json"), "broken");
			File.WriteAllText(Path.Combine(text2, "edr.json"), "ENTRY_SHOULD_NOT_MIGRATE_171 key=TEST_ONLY_MIGRATION_171");
			int num = ConfigurationProfileMigration.Copy("simrail", text2, text3);
			Check(num == 1 && File.Exists(Path.Combine(text3, "1852.json")), "Profil ze starego folderu SimKol/EDR jest kopiowany do EDR.");
			StationProfile stationProfile2 = Json.Read<StationProfile>(File.ReadAllBytes(Path.Combine(text3, "1852.json")));
			Check(stationProfile2.PointId == "1852" && stationProfile2.Station.Name == stationProfile.Station.Name && stationProfile2.Station.Options.AutomaticReleaseSeconds == 123 && stationProfile2.Station.Sections.All((Section x) => x.ManualConfiguration), "Migracja zachowuje identyfikator posterunku oraz ręczne ustawienia.");
			Check(File.ReadAllBytes(Path.Combine(text2, "1852.json")).SequenceEqual(array), "Oryginalny profil nie jest usuwany ani zmieniany.");
			Check(File.ReadAllBytes(Path.Combine(text3, "1853.json")).SequenceEqual(array2), "Starszy profil nie nadpisuje nowszej konfiguracji zapisanej pod EDR.");
			Check(Directory.GetFiles(text3).Length == 2 && !Directory.GetFiles(text3).Any((string x) => x.EndsWith(".tmp")), "Uszkodzone profile, inny identyfikator i baza sesji są pomijane; brak plików tymczasowych.");
			Check(!Directory.GetFiles(text3).Select(File.ReadAllText).Any((string x) => x.Contains("ENTRY_SHOULD_NOT_MIGRATE_171") || x.Contains("TEST_ONLY_MIGRATION_171")), "Migracja nie przenosi dzienników ani kluczy gry.");
			byte[] second = File.ReadAllBytes(Path.Combine(text3, "1852.json"));
			Check(ConfigurationProfileMigration.Copy("simrail", text2, text3) == 0 && File.ReadAllBytes(Path.Combine(text3, "1852.json")).SequenceEqual(second), "Ponowne uruchomienie migracji nie tworzy duplikatów i nie przestawia ustawień.");
			Check(ConfigurationProfileMigration.Copy("simrail", Path.Combine(text, "missing"), Path.Combine(text, "unused")) == 0 && !Directory.Exists(Path.Combine(text, "unused")), "Brak starego katalogu nie tworzy pustych folderów.");
			Check(DataLocations.ConfigurationRoot == Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EDR"), "Folder ustawień aplikacji nazywa się EDR.");
			string text4 = DataLocations.StationProfiles("simrail");
			string text5 = DataLocations.StationProfiles("td2");
			Check(text4 == Path.Combine(DataLocations.ConfigurationRoot, "posterunki", "simrail") && text5 == Path.Combine(DataLocations.ConfigurationRoot, "posterunki", "td2") && text4 != text5, "Profile są dzielone według symulatora, bez poziomu serwera.");
			Check(Engine.DefaultOrganization("simrail") == "EDR", "Domyślna nazwa jednostki SimRail w stopce to EDR.");
			Check(!LocalPersistence.Enabled && !Directory.Exists(store.DirectoryPath), "Zmiana lokalizacji profili nie uruchamia zapisu sesji.");
			return true;
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			return false;
		}
		finally
		{
			File.WriteAllLines(Path.Combine(folder, "wyniki-konfiguracja171.txt"), log, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		}
	}
}

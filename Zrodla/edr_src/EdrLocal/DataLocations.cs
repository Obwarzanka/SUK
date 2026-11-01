using System;
using System.IO;

namespace EdrLocal;

public static class DataLocations
{
	public static string ConfigurationRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EDR");

	public static string Root => Path.Combine(ConfigurationRoot, "dane");

	public static string StationProfiles(string simulator)
	{
		simulator = ConfiguratorRepository.Provider(simulator);
		string text = Path.Combine(ConfigurationRoot, "posterunki", simulator);
		if (!Ui.Testing)
		{
			ConfigurationProfileMigration.Copy(simulator, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SimKol", "EDR", "posterunki", simulator), text);
		}
		return text;
	}

	public static void MigratePortableSession(string destination, string server, string point)
	{
		if (LocalPersistence.Enabled && !Ui.Testing && !File.Exists(Path.Combine(destination, "edr.json")))
		{
			string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dane", "simrail", server, point, "edr.json");
			if (File.Exists(path))
			{
				AppState appState = Json.Read<AppState>(File.ReadAllBytes(path));
				Store.Validate(appState);
				ConfiguratorRepository.AtomicWrite(Path.Combine(destination, "edr.json"), Json.Bytes(appState));
			}
		}
	}
}

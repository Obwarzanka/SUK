using System;
using System.IO;
using System.Linq;

namespace EdrLocal;

public static class ConfigurationProfileMigration
{
	public static int Copy(string simulator, string source, string destination)
	{
		simulator = ConfiguratorRepository.Provider(simulator);
		if (!Directory.Exists(source))
		{
			return 0;
		}
		int num = 0;
		foreach (string item in Directory.GetFiles(source, "*.json").Take(1000))
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(item);
			string text = Path.Combine(destination, fileNameWithoutExtension + ".json");
			if (File.Exists(text))
			{
				continue;
			}
			StationProfile stationProfile;
			try
			{
				bool num2;
				if (!string.IsNullOrEmpty(fileNameWithoutExtension))
				{
					if (!(simulator == "simrail"))
					{
						num2 = !Guid.TryParseExact(fileNameWithoutExtension, "N", out var _);
						goto IL_00aa;
					}
					if (fileNameWithoutExtension.Length <= 20)
					{
						num2 = !fileNameWithoutExtension.All(char.IsDigit);
						goto IL_00aa;
					}
				}
				continue;
				IL_00aa:
				if (num2 || new FileInfo(item).Length > 4194304)
				{
					continue;
				}
				stationProfile = Json.Read<StationProfile>(File.ReadAllBytes(item));
				if (stationProfile != null && stationProfile.SchemaVersion == 1 && !(stationProfile.PointId != fileNameWithoutExtension) && stationProfile.Station != null && stationProfile.Station.SimulatorReviewed)
				{
					Station station = stationProfile.Station;
					if ((!(simulator == "simrail") || !(station.SimRailPointId != fileNameWithoutExtension)) && !string.IsNullOrWhiteSpace(station.Id) && station.Sections != null && !station.Sections.Any((Section x) => x == null || string.IsNullOrWhiteSpace(x.Id)) && station.Sections.Select((Section x) => x.Id).Distinct().Count() == station.Sections.Count)
					{
						StationSettings.Normalize(station);
						StationSettings.Validate(station, ConfiguratorRepository.EmptyState(station));
						goto IL_01de;
					}
				}
			}
			catch
			{
			}
			continue;
			IL_01de:
			Directory.CreateDirectory(destination);
			string text2 = text + "." + Guid.NewGuid().ToString("N") + ".tmp";
			try
			{
				using (FileStream fileStream = new FileStream(text2, FileMode.CreateNew, FileAccess.Write, FileShare.None))
				{
					byte[] array = Json.Bytes(stationProfile);
					fileStream.Write(array, 0, array.Length);
					fileStream.Flush(flushToDisk: true);
				}
				try
				{
					File.Move(text2, text);
					num++;
				}
				catch (IOException)
				{
					if (!File.Exists(text))
					{
						throw;
					}
				}
			}
			finally
			{
				if (File.Exists(text2))
				{
					File.Delete(text2);
				}
			}
		}
		return num;
	}
}

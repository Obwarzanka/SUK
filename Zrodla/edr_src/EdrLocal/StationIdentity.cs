using System;

namespace EdrLocal;

public static class StationIdentity
{
	public static readonly string[] Types = new string[6] { "STACJA", "STACJA WĘZŁOWA", "POSTERUNEK ODGAŁĘŹNY", "POSTERUNEK ODSTĘPOWY", "BOCZNICA", "STACJA STYCZNA" };

	public static string Type(Station station)
	{
		if (!string.IsNullOrEmpty(station.PostType))
		{
			return station.PostType;
		}
		object obj = KnownType(station.Name);
		if (obj == null)
		{
			if (station.Sections.Count <= 2)
			{
				return Types[0];
			}
			obj = Types[1];
		}
		return (string)obj;
	}

	public static string KnownType(string name)
	{
		if (!string.Equals(name, "Starzyny", StringComparison.OrdinalIgnoreCase) && !string.Equals(name, "Sprowa", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		return Types[2];
	}

	public static string Caption(Station station)
	{
		return station.Name.ToUpper() + " (" + Type(station) + ") - Elektroniczny Dziennik Ruchu - EDR Produkcja (wersja " + typeof(StationIdentity).Assembly.GetName().Version.ToString(4) + ")";
	}
}

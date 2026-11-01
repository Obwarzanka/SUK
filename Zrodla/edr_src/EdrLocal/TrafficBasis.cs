using System;

namespace EdrLocal;

public static class TrafficBasis
{
	public const string Automatic = "Wieloodstępowa (samoczynna) blokada liniowa";

	public const string SemiAutomatic = "Jednoodstępowa (półsamoczynna) blokada liniowa";

	public const string Telephone = "Zapowiadanie telefoniczne";

	public const string RadioTelephone = "Zapowiadanie radiotelefoniczne";

	public static readonly string[] Choices = new string[3] { "Wieloodstępowa (samoczynna) blokada liniowa", "Jednoodstępowa (półsamoczynna) blokada liniowa", "Zapowiadanie telefoniczne" };

	public static bool IsSemiAutomatic(string value)
	{
		if ((value ?? "").IndexOf("półsamoczynn", StringComparison.CurrentCultureIgnoreCase) < 0)
		{
			return (value ?? "").IndexOf("jednoodstęp", StringComparison.CurrentCultureIgnoreCase) >= 0;
		}
		return true;
	}

	public static bool IsAutomatic(string value)
	{
		if (!IsSemiAutomatic(value))
		{
			return (value ?? "").IndexOf("samoczynn", StringComparison.CurrentCultureIgnoreCase) >= 0;
		}
		return false;
	}

	public static bool IsTelephone(string value)
	{
		if (!string.Equals((value ?? "").Trim(), "Zapowiadanie telefoniczne", StringComparison.CurrentCultureIgnoreCase))
		{
			return string.Equals((value ?? "").Trim(), "Zapowiadanie radiotelefoniczne", StringComparison.CurrentCultureIgnoreCase);
		}
		return true;
	}

	public static string Display(string value)
	{
		string text = (value ?? "").Trim();
		if (string.Equals(text, "Blokada samoczynna", StringComparison.CurrentCultureIgnoreCase))
		{
			return "Wieloodstępowa (samoczynna) blokada liniowa";
		}
		if (string.Equals(text, "Blokada półsamoczynna", StringComparison.CurrentCultureIgnoreCase) || string.Equals(text, "Jednoodstępowa blokada liniowa", StringComparison.CurrentCultureIgnoreCase))
		{
			return "Jednoodstępowa (półsamoczynna) blokada liniowa";
		}
		return text;
	}
}

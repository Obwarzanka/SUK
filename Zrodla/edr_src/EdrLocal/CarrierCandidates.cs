using System;
using System.Collections.Generic;
using System.Linq;

namespace EdrLocal;

public static class CarrierCandidates
{
	private static readonly HashSet<string> freight = Names(new string[11]
	{
		"Cargo", "PCC", "CEMET", "CIECH Cargo", "TRANSCHEM", "ORLEN Kolej", "ČD Cargo", "ITL", "LTE", "DB",
		"KOLPREM"
	});

	private static readonly HashSet<string> passenger = Names(new string[6] { "Intercity", "POLREGIO", "KS", "KM", "ŁKA", "České dráhy" });

	private static HashSet<string> Names(IEnumerable<string> names)
	{
		return new HashSet<string>(names.Select(CarrierNames.Full), StringComparer.OrdinalIgnoreCase);
	}

	public static string Select(IEnumerable<string> candidates, string category)
	{
		HashSet<string> hashSet = Names((candidates ?? Enumerable.Empty<string>()).Where((string x) => !string.IsNullOrWhiteSpace(x)));
		if (hashSet.Count == 1)
		{
			return hashSet.Single();
		}
		if (hashSet.Count == 0)
		{
			return "";
		}
		string text = (category ?? "").Trim().ToUpperInvariant();
		HashSet<string> hashSet2 = null;
		if (text.StartsWith("T", StringComparison.Ordinal))
		{
			hashSet2 = freight;
		}
		else if (text.StartsWith("PW", StringComparison.Ordinal) || new string[10] { "MPE", "MHE", "MOJ", "MOE", "EIJ", "EIE", "ECE", "ROJ", "ROE", "RPJ" }.Contains(text))
		{
			hashSet2 = passenger;
		}
		if (hashSet2 == null)
		{
			return "";
		}
		List<string> list = hashSet.Where(hashSet2.Contains).ToList();
		if (list.Count != 1)
		{
			return "";
		}
		return list[0];
	}
}

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace EdrLocal;

public static class CarrierNames
{
	private static readonly Dictionary<string, string> names = Build();

	private static string Key(string value)
	{
		return Regex.Replace((value ?? "").ToUpperInvariant(), "[^\\p{L}\\p{N}]", "");
	}

	private static void Add(Dictionary<string, string> result, string full, params string[] aliases)
	{
		result[Key(full)] = full;
		foreach (string value in aliases)
		{
			result[Key(value)] = full;
		}
	}

	private static Dictionary<string, string> Build()
	{
		Dictionary<string, string> result = new Dictionary<string, string>();
		Add(result, "PKP Intercity S.A.", "IC", "Intercity", "PKP Intercity", "PKP Intercity Spółka Akcyjna");
		Add(result, "PKP Cargo S.A. w restrukturyzacji", "Cargo", "PKP Cargo", "PKP CARGO S.A.", "PKP Cargo Spółka Akcyjna", "PKP Cargo Spółka Akcyjna w restrukturyzacji", "Akiem");
		Add(result, "PCC Intermodal S.A.", "PCC", "PCC Intermodal");
		Add(result, "Koleje Śląskie Sp. z o.o.", "KS", "Koleje Śląskie");
		Add(result, "Koleje Mazowieckie - KM Sp. z o.o.", "KM", "Koleje Mazowieckie", "Koleje Mazowieckie Sp. z o.o.");
		Add(result, "POLREGIO S.A.", "PR", "Polregio", "Przewozy Regionalne", "Przewozy Regionalne Sp. z o.o.");
		Add(result, "Łódzka Kolej Aglomeracyjna sp. z o.o.", "ŁKA", "LKA", "Łódzka Kolej Aglomeracyjna");
		Add(result, "CARGOUNIT Sp. z o.o.", "CargoUnit");
		Add(result, "Przedsiębiorstwo Usług Kolejowych KOLPREM Sp. z o.o.", "PUK Kolprem", "Kolprem");
		Add(result, "CEMET S.A.", "CEMET");
		Add(result, "CIECH Cargo Sp. z o.o.", "CIECH Cargo");
		Add(result, "TRANSCHEM Sp. z o.o.", "Transchem");
		Add(result, "ORLEN Kolej Sp. z o.o.", "Orlen Kolej", "ORKOL", "LOTOS", "LOTOS Kolej", "LOTOS Kolej Sp. z o.o.");
		Add(result, "ČD Cargo, a.s.", "ČD Cargo");
		Add(result, "České dráhy, a.s.", "České dráhy");
		Add(result, "Alpha Trains Luxembourg S.à r.l.", "Alpha Trains", "Alpha Trains Luxembourg", "ATLU");
		Add(result, "RAILPOOL GmbH", "Railpool", "Rpool");
		Add(result, "ITL Eisenbahngesellschaft mbH", "Import Transport Logistik", "ITL");
		Add(result, "LTE Logistik- und Transport-GmbH", "LTE Logistik and Transport", "LTE");
		Add(result, "DB Cargo AG", "Deutsche Bahn", "DB");
		return result;
	}

	public static bool IsAkiem(string value)
	{
		return Key(value) == "AKIEM";
	}

	public static string Expanded(string value)
	{
		string input = Full(value);
		input = Regex.Replace(input, "\\bS\\.A\\.(?= w restrukturyzacji$|$)", "Spółka Akcyjna", RegexOptions.IgnoreCase);
		return Regex.Replace(input, "\\bSp\\. z o\\.o\\.$", "Spółka z o.o.", RegexOptions.IgnoreCase);
	}

	public static string Full(string value)
	{
		string text = Regex.Replace(value ?? "", "\\s+", " ").Trim();
		if (!names.TryGetValue(Key(text), out var value2))
		{
			return text;
		}
		return value2;
	}
}

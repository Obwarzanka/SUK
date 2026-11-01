using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace EdrLocal;

public static class VehicleCarriers
{
	private static readonly Dictionary<string, string> entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		{ "4E/EP08-001", "PKP Intercity S.A." },
		{ "4E/EP08-008", "PKP Intercity S.A." },
		{ "4E/EP08-013", "PKP Intercity S.A." },
		{ "201E/ET22-243", "PKP CARGO S.A." },
		{ "201E/ET22-256", "PKP CARGO S.A." },
		{ "201E/ET22-644", "PKP CARGO S.A." },
		{ "201E/ET22-836", "PKP CARGO S.A." },
		{ "201E/ET22-911", "PKP CARGO S.A." },
		{ "201E/ET22-1163", "PKP CARGO S.A." },
		{ "Impuls/36wed-001", "Łódzka Kolej Aglomeracyjna sp. z o.o." },
		{ "Impuls/36wed-002", "Łódzka Kolej Aglomeracyjna sp. z o.o." },
		{ "Impuls/36wed-003", "Łódzka Kolej Aglomeracyjna sp. z o.o." },
		{ "Impuls/36wed-004", "Łódzka Kolej Aglomeracyjna sp. z o.o." },
		{ "Impuls/36wed-005", "Łódzka Kolej Aglomeracyjna sp. z o.o." },
		{ "Impuls/36wed-006", "Łódzka Kolej Aglomeracyjna sp. z o.o." },
		{ "Impuls/36wed-007", "Łódzka Kolej Aglomeracyjna sp. z o.o." },
		{ "Impuls/36wed-008", "Łódzka Kolej Aglomeracyjna sp. z o.o." },
		{ "Impuls/36wed-009", "Łódzka Kolej Aglomeracyjna sp. z o.o." },
		{ "Impuls/36wed-010", "Łódzka Kolej Aglomeracyjna sp. z o.o." },
		{ "Impuls/36wed-011", "Łódzka Kolej Aglomeracyjna sp. z o.o." },
		{ "Impuls/36wed-012", "Łódzka Kolej Aglomeracyjna sp. z o.o." },
		{ "Impuls/36wed-013", "Łódzka Kolej Aglomeracyjna sp. z o.o." },
		{ "Impuls/36wed-014", "Łódzka Kolej Aglomeracyjna sp. z o.o." },
		{ "4E/EU07-001", "PKP CARGO S.A." },
		{ "4E/EU07-005", "PKP Intercity S.A." },
		{ "4E/EU07-068", "CARGOUNIT Sp. z o.o." },
		{ "4E/EU07-070", "Przedsiębiorstwo Usług Kolejowych KOLPREM Sp. z o.o." },
		{ "4E/EU07-085", "PKP Intercity S.A." },
		{ "4E/EU07-092", "PKP Intercity S.A." },
		{ "4E/EU07-096", "PKP CARGO S.A." },
		{ "4E/EU07-153", "Przedsiębiorstwo Usług Kolejowych KOLPREM Sp. z o.o." },
		{ "4E/EU07-193", "PKP CARGO S.A." },
		{ "4E/EP07-135", "PKP Intercity S.A." },
		{ "4E/EP07-174", "PKP Intercity S.A." },
		{ "CD163/163_021-9", "POLREGIO S.A." },
		{ "CD163/163_029-2", "POLREGIO S.A." },
		{ "CD163/163_030-0", "POLREGIO S.A." },
		{ "CD163/163_034-2", "ČD Cargo, a.s." },
		{ "CD163/163_035-9", "České dráhy, a.s." },
		{ "CD163/163_040-9", "ČD Cargo, a.s." },
		{ "CD163/163_041-7", "POLREGIO S.A." },
		{ "CD163/163_042-5", "POLREGIO S.A." },
		{ "CD163/163_043-3", "ČD Cargo, a.s." },
		{ "CD163/163_045-8", "ČD Cargo, a.s." },
		{ "CD163/163_046-6", "ČD Cargo, a.s." },
		{ "BR285/BR285-121", "ORLEN Kolej Sp. z o.o." },
		{ "BR285/BR285-122", "ORLEN Kolej Sp. z o.o." },
		{ "BR285/BR285-123", "ORLEN Kolej Sp. z o.o." },
		{ "BR285/BR285-124", "ORLEN Kolej Sp. z o.o." },
		{ "BR285/BR285-125", "ORLEN Kolej Sp. z o.o." },
		{ "BR285/BR285-126", "ORLEN Kolej Sp. z o.o." },
		{ "BR285/BR285-127", "ORLEN Kolej Sp. z o.o." },
		{ "BR285/BR285-128", "ORLEN Kolej Sp. z o.o." },
		{ "BR285/BR285-129", "ORLEN Kolej Sp. z o.o." },
		{ "BR285/BR285-130", "ORLEN Kolej Sp. z o.o." },
		{ "Traxx/E186-134", "Alpha Trains Luxembourg S.à r.l." },
		{ "Traxx/E186-136", "RAILPOOL GmbH" },
		{ "Traxx/E186-138", "ITL Eisenbahngesellschaft mbH" },
		{ "Traxx/E186-241", "DB Cargo AG" },
		{ "Traxx/E186-242", "PCC Intermodal S.A." },
		{ "Traxx/E186-245", "Alpha Trains Luxembourg S.à r.l." },
		{ "Traxx/E186-246", "PCC Intermodal S.A." },
		{ "Traxx/E186-248", "PCC Intermodal S.A." },
		{ "Traxx/E186-250", "TRANSCHEM Sp. z o.o." },
		{ "Traxx/E186-928", "CARGOUNIT Sp. z o.o." },
		{ "Traxx/E186-929", "CARGOUNIT Sp. z o.o." },
		{ "Traxx/E186-930", "CARGOUNIT Sp. z o.o." },
		{ "Traxx/E186-941", "LTE Logistik- und Transport-GmbH" },
		{ "Traxx/E186-942", "LTE Logistik- und Transport-GmbH" },
		{ "Traxx/EU43-001", "PKP CARGO S.A." },
		{ "Traxx/EU43-002", "PKP CARGO S.A." },
		{ "Traxx/EU43-003", "PKP CARGO S.A." },
		{ "Traxx/EU43-004", "PKP CARGO S.A." },
		{ "Traxx/EU43-005", "PKP CARGO S.A." },
		{ "Dragon2/ET25-002", "PKP CARGO S.A." },
		{ "Dragon2/E6ACTa-014", "CIECH Cargo Sp. z o.o." },
		{ "Dragon2/E6ACTa-016", "CARGOUNIT Sp. z o.o." },
		{ "Dragon2/E6ACTadb-027", "CEMET S.A." },
		{ "Elf/EN76-006", "Koleje Śląskie Sp. z o.o." },
		{ "Elf/EN76-022", "Koleje Mazowieckie - KM Sp. z o.o." },
		{ "Elf/EN96-001", "POLREGIO S.A." },
		{ "EN57/EN57-009", "POLREGIO S.A." },
		{ "EN57/EN57-038", "Koleje Mazowieckie - KM Sp. z o.o." },
		{ "EN57/EN57-047", "POLREGIO S.A." },
		{ "EN57/EN57-206", "POLREGIO S.A." },
		{ "EN57/EN57-612", "POLREGIO S.A." },
		{ "EN57/EN57-614", "POLREGIO S.A." },
		{ "EN57/EN57-650", "POLREGIO S.A." },
		{ "EN57/EN57-919", "POLREGIO S.A." },
		{ "EN57/EN57-1000", "Koleje Mazowieckie - KM Sp. z o.o." },
		{ "EN57/EN57-1003", "POLREGIO S.A." },
		{ "EN57/EN57-1051", "POLREGIO S.A." },
		{ "EN57/EN57-1181", "POLREGIO S.A." },
		{ "EN57/EN57-1219", "POLREGIO S.A." },
		{ "EN57/EN57-1316", "POLREGIO S.A." },
		{ "EN57/EN57-1331", "POLREGIO S.A." },
		{ "EN57/EN57-1458", "POLREGIO S.A." },
		{ "EN57/EN57-1567", "Koleje Mazowieckie - KM Sp. z o.o." },
		{ "EN57/EN57-1571", "POLREGIO S.A." },
		{ "EN57/EN57-1752", "Koleje Mazowieckie - KM Sp. z o.o." },
		{ "EN57/EN57-1755", "POLREGIO S.A." },
		{ "EN57/EN57-1796", "Koleje Mazowieckie - KM Sp. z o.o." },
		{ "EN57/EN57-1821", "POLREGIO S.A." },
		{ "EN57/EN71-002", "POLREGIO S.A." },
		{ "EN57/EN71-004", "POLREGIO S.A." },
		{ "EN57/EN71-005", "POLREGIO S.A." },
		{ "EN57/EN71-011", "POLREGIO S.A." },
		{ "EN57/EN71-015", "POLREGIO S.A." },
		{ "Pendolino/ED250-001", "PKP Intercity S.A." },
		{ "Pendolino/ED250-009", "PKP Intercity S.A." },
		{ "Pendolino/ED250-015", "PKP Intercity S.A." },
		{ "Pendolino/ED250-018", "PKP Intercity S.A." }
	};

	private static readonly Dictionary<string, string[]> candidates = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) { 
	{
		"4E/EU07-241",
		new string[2] { "PKP Cargo", "PKP Intercity" }
	} };

	private static readonly Dictionary<string, string> overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "Traxx/E186-139", "PKP Cargo S.A. w restrukturyzacji" } };

	private static readonly HashSet<string> traction = new HashSet<string>(new string[11]
	{
		"4E", "201E", "Traxx", "Dragon2", "EN57", "Elf", "Impuls", "Pendolino", "CD163", "BR285",
		"Ty2"
	}, StringComparer.OrdinalIgnoreCase);

	private static readonly HashSet<string> emu = new HashSet<string>(new string[4] { "EN57", "Elf", "Impuls", "Pendolino" }, StringComparer.OrdinalIgnoreCase);

	public static int Count => entries.Count;

	public static bool HasCandidates(IEnumerable<string> vehicles)
	{
		return TractionCodes(vehicles).Any((string x) => candidates.ContainsKey(x));
	}

	public static bool HasAkiemOverride(IEnumerable<string> vehicles)
	{
		return LocomotiveCodes(vehicles).Any((string x) => overrides.ContainsKey(x));
	}

	public static IEnumerable<string> TractionCodes(IEnumerable<string> vehicles)
	{
		return (from x in (vehicles ?? Enumerable.Empty<string>()).Select(Key)
			where x.Contains("/") && traction.Contains(x.Split('/')[0])
			select x).Distinct(StringComparer.OrdinalIgnoreCase);
	}

	public static string Key(string vehicle)
	{
		string text = (vehicle ?? "").Trim();
		int num = text.IndexOf(':');
		if (num >= 0)
		{
			text = text.Substring(0, num);
		}
		return Regex.Replace(text, "\\s+Variant$", "", RegexOptions.IgnoreCase).Trim();
	}

	private static bool IsEmu(string key)
	{
		int num = key.IndexOf('/');
		if (num >= 0)
		{
			return emu.Contains(key.Substring(0, num));
		}
		return false;
	}

	public static IEnumerable<string> EmuCodes(IEnumerable<string> vehicles)
	{
		return TractionCodes(vehicles).Where(IsEmu);
	}

	public static IEnumerable<string> LocomotiveCodes(IEnumerable<string> vehicles)
	{
		return from x in TractionCodes(vehicles)
			where !IsEmu(x)
			select x;
	}

	public static string ResolveEmu(IEnumerable<string> vehicles)
	{
		return Resolve(EmuCodes(vehicles));
	}

	public static string ResolveLocomotive(IEnumerable<string> vehicles)
	{
		return ResolveLocomotive(vehicles, null);
	}

	public static string ResolveLocomotive(IEnumerable<string> vehicles, string category)
	{
		return Resolve(LocomotiveCodes(vehicles), category);
	}

	public static string Resolve(IEnumerable<string> vehicles)
	{
		return Resolve(vehicles, null);
	}

	public static string Resolve(IEnumerable<string> vehicles, string category)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		bool flag = false;
		foreach (string item in vehicles ?? Enumerable.Empty<string>())
		{
			string text = Key(item);
			int num = text.IndexOf('/');
			if (num < 0 || !traction.Contains(text.Substring(0, num)))
			{
				continue;
			}
			string[] value2;
			if (overrides.TryGetValue(text, out var value) || entries.TryGetValue(text, out value))
			{
				hashSet.Add(CarrierNames.Full(value));
			}
			else if (candidates.TryGetValue(text, out value2))
			{
				value = CarrierCandidates.Select(value2, category);
				if (value.Length > 0)
				{
					hashSet.Add(value);
				}
				else
				{
					flag = true;
				}
			}
			else
			{
				flag = true;
			}
		}
		if (flag || hashSet.Count != 1)
		{
			return "";
		}
		return hashSet.Single();
	}
}

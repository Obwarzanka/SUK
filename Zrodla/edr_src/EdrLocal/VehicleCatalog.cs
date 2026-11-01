using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace EdrLocal;

public static class VehicleCatalog
{
	private static List<VehicleSpec> entries;

	public static List<VehicleSpec> Entries
	{
		get
		{
			if (entries == null)
			{
				using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EdrLocal.Vehicles.json");
				using MemoryStream memoryStream = new MemoryStream();
				stream.CopyTo(memoryStream);
				entries = Json.Read<List<VehicleSpec>>(memoryStream.ToArray());
			}
			return entries;
		}
	}

	public static string[] Choices => new string[1] { "**BRAK**" }.Concat(Entries.Select((VehicleSpec x) => x.Type).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy((string x) => x, StringComparer.CurrentCultureIgnoreCase)).ToArray();

	public static VehicleSpec Find(string value)
	{
		List<VehicleSpec> list = Entries.Where((VehicleSpec x) => string.Equals(x.Type, value, StringComparison.OrdinalIgnoreCase)).ToList();
		if (list.Count == 1)
		{
			return list[0];
		}
		if (list.Count > 1)
		{
			return null;
		}
		List<VehicleSpec> list2 = Entries.Where((VehicleSpec x) => string.Equals(x.SepeSeries, value, StringComparison.OrdinalIgnoreCase)).ToList();
		if (list2.Count != 1)
		{
			return null;
		}
		return list2[0];
	}

	private static string N(decimal? n, string unit)
	{
		if (!n.HasValue)
		{
			return "—";
		}
		return ConsistPlan.Number(n) + unit;
	}

	public static string Description(string value)
	{
		VehicleSpec vehicleSpec = Find(value);
		if (vehicleSpec != null)
		{
			return "Brutto: " + N(vehicleSpec.Gross, " t") + "   Długość: " + N(vehicleSpec.Length, " m") + "   VMax: " + N(vehicleSpec.MaxSpeed, " km/h") + "\nLiczba osi: " + N(vehicleSpec.Axles, "") + "   Trakcja: " + vehicleSpec.Traction;
		}
		return "Brutto: —    Długość: —    VMax: —\nLiczba osi: —    Trakcja: —";
	}
}

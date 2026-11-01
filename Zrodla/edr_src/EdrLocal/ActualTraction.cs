using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace EdrLocal;

public sealed class ActualTraction
{
	public string Series;

	public string Number;

	public static List<ActualTraction> Read(IEnumerable<string> vehicles)
	{
		List<ActualTraction> list = new List<ActualTraction>();
		foreach (string item in VehicleCarriers.TractionCodes(vehicles))
		{
			string input = item.Substring(item.IndexOf('/') + 1);
			Match match = Regex.Match(input, "^([A-Za-z0-9]+)[-_](\\d+(?:-\\d+)?)$");
			if (match.Success)
			{
				string value = match.Groups[1].Value;
				VehicleSpec vehicleSpec = VehicleCatalog.Find(value);
				list.Add(new ActualTraction
				{
					Series = ((vehicleSpec != null && !string.IsNullOrWhiteSpace(vehicleSpec.SepeSeries)) ? vehicleSpec.SepeSeries : value),
					Number = match.Groups[2].Value
				});
			}
		}
		return list;
	}
}

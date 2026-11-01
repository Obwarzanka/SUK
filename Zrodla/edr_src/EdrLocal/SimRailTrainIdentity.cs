using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace EdrLocal;

public static class SimRailTrainIdentity
{
	public static string Name(SimRailTrain source, string category)
	{
		string raw = Clean(source.trainName);
		if (source.timetable != null && source.timetable.Any((SimRailPoint x) => string.Equals(x.trainType, raw, StringComparison.OrdinalIgnoreCase)))
		{
			return "";
		}
		return Name(raw, category);
	}

	public static string Name(string raw, string category)
	{
		string text = Clean(raw);
		Match match = Regex.Match(text, "^\\S+\\s+[-–]\\s+(\\S+)(?:\\s+(.*))?$");
		if (match.Success)
		{
			return match.Groups[2].Value.Trim();
		}
		if (string.Equals(text, category, StringComparison.OrdinalIgnoreCase) || new string[17]
		{
			"TLK", "IC", "ICN", "EIC", "EIP", "EC", "IR", "R", "R1", "R3",
			"RE13", "S1", "S3", "S41", "K3", "Ł", "ŁS"
		}.Contains(text.ToUpperInvariant()))
		{
			return "";
		}
		return text;
	}

	private static string Clean(string raw)
	{
		string input = Regex.Replace(raw ?? "", "[\"„“”«»]", "").Trim();
		return Regex.Replace(input, "\\s+", " ");
	}

	private static string Observation(SimRailTrain train)
	{
		if (!train.VehiclesFromObservation)
		{
			return "";
		}
		return " Ostatnio zaobserwowany skład tego kursu: " + train.VehiclesObservedUtc.GetValueOrDefault().ToString("yyyy-MM-dd HH:mm:ss") + " UTC. Bieżący odczyt nie dostarczył składu.";
	}

	public static string Carrier(SimRailTrain train, string category)
	{
		return CarrierInfo(train, category).Name;
	}

	public static CarrierResult CarrierInfo(SimRailTrain train, string category)
	{
		string text = VehicleCarriers.ResolveEmu(train.Vehicles);
		if (text.Length > 0)
		{
			CarrierResult carrierResult = new CarrierResult();
			carrierResult.Name = text;
			carrierResult.Evidence = "Przypisanie wg wariantu EZT w oficjalnej wiki (pierwszeństwo przed carrierName i kategorią): " + string.Join(", ", VehicleCarriers.EmuCodes(train.Vehicles)) + "." + Observation(train);
			return carrierResult;
		}
		if (!string.IsNullOrWhiteSpace(train.carrierName))
		{
			CarrierResult carrierResult2 = new CarrierResult();
			carrierResult2.Name = CarrierNames.Full(train.carrierName);
			carrierResult2.Evidence = (CarrierNames.IsAkiem(train.carrierName) ? "Umowne przypisanie Akiem do PKP Cargo na życzenie użytkownika (carrierName)." : "Przewoźnik podany przez SimRail (carrierName).");
			return carrierResult2;
		}
		string input = Clean(train.trainName);
		Match match = Regex.Match(input, "^\\S+\\s*[-–]\\s*(\\S+)");
		string text2 = (match.Success ? match.Groups[1].Value.ToUpperInvariant() : "");
		string text3 = "";
		if (new string[7] { "TLK", "IC", "ICN", "EIC", "EIP", "EC", "MHE" }.Contains(text2) || category == "MHE")
		{
			text3 = "Intercity";
		}
		else if (new string[3] { "R1", "R3", "RE13" }.Contains(text2))
		{
			text3 = "Koleje Mazowieckie";
		}
		else if (new string[2] { "R", "K3" }.Contains(text2))
		{
			text3 = "POLREGIO";
		}
		else if (new string[2] { "Ł", "ŁS" }.Contains(text2))
		{
			text3 = "Łódzka Kolej Aglomeracyjna";
		}
		if (text3.Length > 0)
		{
			CarrierResult carrierResult3 = new CarrierResult();
			carrierResult3.Name = CarrierNames.Full(text3);
			carrierResult3.Evidence = "Przypisanie wg kategorii " + ((text2.Length > 0) ? text2 : category) + ".";
			return carrierResult3;
		}
		string text4 = VehicleCarriers.ResolveLocomotive(train.Vehicles, category);
		if (text4.Length > 0)
		{
			CarrierResult carrierResult4 = new CarrierResult();
			carrierResult4.Name = text4;
			carrierResult4.Evidence = (VehicleCarriers.HasAkiemOverride(train.Vehicles) ? "Umowne przypisanie Akiem do PKP Cargo na życzenie użytkownika; wiki podaje Akiem: " : "Przypisanie wg wariantu lokomotywy w oficjalnej wiki, przy braku przewoźnika z API i kategorii: ") + string.Join(", ", VehicleCarriers.LocomotiveCodes(train.Vehicles)) + (VehicleCarriers.HasCandidates(train.Vehicles) ? (". Spośród przewoźników wariantu wybrano zgodnego z rodzajem " + category + ".") : ".") + " Dla taboru wynajmowanego może to być właściciel pojazdu." + Observation(train);
			return carrierResult4;
		}
		CarrierResult carrierResult5 = new CarrierResult();
		carrierResult5.Name = "";
		carrierResult5.Evidence = ((train.Vehicles == null) ? "Brak carrierName i jednoznacznej kategorii. Brak danych konkretnego pojazdu tego kursu; bieżące API nie obejmuje wszystkich przyszłych i zakończonych kursów." : "Brak jednoznacznego przypisania wariantu pojazdu do przewoźnika w katalogu wiki.");
		return carrierResult5;
	}
}

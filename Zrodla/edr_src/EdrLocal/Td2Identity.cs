using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;

namespace EdrLocal;

public static class Td2Identity
{
	private static readonly Td2IdentityCatalog catalog = Load();

	private static readonly Dictionary<string, List<Td2NamedTrain>> byNumber = Index();

	private static Td2IdentityCatalog Load()
	{
		using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EdrLocal.Td2Identity.json");
		if (stream == null)
		{
			throw new InvalidOperationException("Brak katalogu nazw i pojazdów TD2.");
		}
		Td2IdentityCatalog td2IdentityCatalog = (Td2IdentityCatalog)new DataContractJsonSerializer(typeof(Td2IdentityCatalog), new DataContractJsonSerializerSettings
		{
			UseSimpleDictionaryFormat = true,
			MaxItemsInObjectGraph = 1000000
		}).ReadObject(stream);
		td2IdentityCatalog.Operators = new Dictionary<string, string[]>(td2IdentityCatalog.Operators, StringComparer.OrdinalIgnoreCase);
		td2IdentityCatalog.WagonRules = new Dictionary<string, Dictionary<string, string[]>>(td2IdentityCatalog.WagonRules, StringComparer.OrdinalIgnoreCase);
		td2IdentityCatalog.Traction = new Dictionary<string, Td2TractionRule>(td2IdentityCatalog.Traction, StringComparer.OrdinalIgnoreCase);
		return td2IdentityCatalog;
	}

	private static string Op(string value)
	{
		return (value ?? "").Trim().ToUpperInvariant() switch
		{
			"KS" => "KŚ", 
			"SKM" => "SKMT", 
			"LKA" => "ŁKA", 
			"KML" => "KMŁ", 
			_ => (value ?? "").Trim().ToUpperInvariant(), 
		};
	}

	private static string Key(string value)
	{
		return Regex.Replace((value ?? "").ToUpperInvariant(), "[^\\p{L}\\p{N}]", "");
	}

	public static string Number(string value)
	{
		value = (value ?? "").Trim();
		if (!Regex.IsMatch(value, "^\\d+$"))
		{
			return value;
		}
		if (value.TrimStart('0').Length != 0)
		{
			return value.TrimStart('0');
		}
		return "0";
	}

	public static string[] Vehicles(string consist)
	{
		return (from s in (consist ?? "").Split(';')
			select Td2Names.Plain(s.Split(':')[0]) into s
			where s.Length > 0
			select s).ToArray();
	}

	public static bool Passenger(string category)
	{
		return new string[11]
		{
			"EC", "EN", "EI", "MM", "MP", "MH", "MO", "RM", "RP", "RO",
			"RA"
		}.Any((string p) => (category ?? "").StartsWith(p, StringComparison.OrdinalIgnoreCase));
	}

	private static string OwnerClass(string code)
	{
		if (!catalog.OwnerClasses.TryGetValue(Op(code), out var value))
		{
			return "";
		}
		return value;
	}

	private static string OwnerName(string code)
	{
		if (!catalog.OwnerNames.TryGetValue(Op(code), out var value))
		{
			return Op(code);
		}
		return value;
	}

	public static string CarrierCaption(string carrier)
	{
		string input = Regex.Replace(carrier ?? "", "\\bS\\.A\\.(?= w restrukturyzacji$|$)", "Spółka Akcyjna", RegexOptions.IgnoreCase);
		return Regex.Replace(input, "\\bSp\\. z o\\.o\\.$", "Spółka z o.o.", RegexOptions.IgnoreCase);
	}

	public static Td2TractionRule Traction(string vehicle)
	{
		if (string.IsNullOrWhiteSpace(vehicle))
		{
			return null;
		}
		if (catalog.Traction.TryGetValue(vehicle, out var value))
		{
			return value;
		}
		string text = Regex.Replace(vehicle ?? "", "[_-]?v\\d+$", "", RegexOptions.IgnoreCase);
		if (text != vehicle && catalog.Traction.TryGetValue(text, out value))
		{
			return value;
		}
		return catalog.Prefixes.FirstOrDefault((Td2TractionRule p) => Regex.IsMatch(vehicle ?? "", "^" + Regex.Escape(p.Prefix) + "(?:[-_]|$)", RegexOptions.IgnoreCase));
	}

	private static Dictionary<string, List<Td2NamedTrain>> Index()
	{
		Dictionary<string, List<Td2NamedTrain>> dictionary = new Dictionary<string, List<Td2NamedTrain>>();
		foreach (Td2NamedTrain train in catalog.Trains)
		{
			string[] array = train.Numbers ?? new string[0];
			foreach (string text in array)
			{
				string[] array2 = (from s in text.Split('/')
					select s.Trim() into s
					where s.Length > 0
					select s).ToArray();
				if (array2.Length == 0)
				{
					continue;
				}
				string text2 = array2[0];
				string[] array3 = array2;
				foreach (string text3 in array3)
				{
					string value = text3;
					if (Regex.IsMatch(text2, "^\\d+$") && Regex.IsMatch(text3, "^\\d+$") && text3.Length < text2.Length)
					{
						value = text2.Substring(0, text2.Length - text3.Length) + text3;
					}
					value = Number(value);
					if (!dictionary.TryGetValue(value, out var value2))
					{
						value2 = (dictionary[value] = new List<Td2NamedTrain>());
					}
					if (!value2.Contains(train))
					{
						value2.Add(train);
					}
				}
			}
		}
		return dictionary;
	}

	private static Td2NamedTrain Named(string number, string preferred)
	{
		if (!byNumber.TryGetValue(Number(number), out var value))
		{
			return null;
		}
		Td2NamedTrain td2NamedTrain = null;
		foreach (Td2NamedTrain item in value)
		{
			if (preferred.Length > 0 && Op(item.Operator) == preferred)
			{
				return item;
			}
			if (td2NamedTrain != null && Op(td2NamedTrain.Operator) != Op(item.Operator))
			{
				return null;
			}
			td2NamedTrain = item;
		}
		return td2NamedTrain;
	}

	private static void Score(Dictionary<string, double> scores, string op, double amount)
	{
		op = Op(op);
		if (op.Length != 0)
		{
			scores.TryGetValue(op, out var value);
			scores[op] = value + amount;
		}
	}

	private static bool PassengerOwner(string op)
	{
		if (op != "")
		{
			return !new string[18]
			{
				"TUR", "PKPC", "KSK", "CTL", "DLA", "POLTRANS", "ALZA", "ECCO", "TRACKTEC", "ITL",
				"ZIK", "INDUSTRIAL", "ORION", "MAJKOL", "PKPE", "PPMT", "LOTOS", "R4C"
			}.Contains(op);
		}
		return false;
	}

	private static string Legacy(string[] vehicles)
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		List<string> list = new List<string>();
		foreach (string key in vehicles)
		{
			if (!catalog.Operators.TryGetValue(key, out var value))
			{
				continue;
			}
			string[] array = value;
			foreach (string value2 in array)
			{
				string text = Op(value2);
				if (!dictionary.ContainsKey(text))
				{
					dictionary[text] = 0;
					list.Add(text);
				}
				dictionary[text]++;
			}
		}
		string result = "";
		int num = 0;
		foreach (string item in list)
		{
			if (dictionary[item] >= num)
			{
				result = item;
				num = dictionary[item];
			}
		}
		return result;
	}

	private static string ApiOwner(string text)
	{
		string text2 = Key(text);
		foreach (KeyValuePair<string, string> ownerName in catalog.OwnerNames)
		{
			if (Key(ownerName.Value) == text2)
			{
				return ownerName.Key;
			}
		}
		if (text2 == "PRZEWOZYREGIONALNESPZOO" || text2 == "PRZEWOZYREGIONALNE")
		{
			return "PR";
		}
		return "";
	}

	public static Td2IdentityResult Resolve(Td2Record record)
	{
		Td2TrainInfo trainInfo = record.Schedule.trainInfo;
		Td2IdentityResult td2IdentityResult = new Td2IdentityResult();
		td2IdentityResult.Carrier = Td2Names.Plain(trainInfo.carrierName);
		td2IdentityResult.Name = Td2Names.Plain(trainInfo.trainName);
		Td2IdentityResult td2IdentityResult2 = td2IdentityResult;
		if (td2IdentityResult2.Carrier.Length > 0)
		{
			td2IdentityResult2.CarrierSource = "API TD2";
		}
		if (td2IdentityResult2.Name.Length > 0)
		{
			td2IdentityResult2.NameSource = "API TD2";
		}
		string consist = ((!string.IsNullOrWhiteSpace(record.RuntimeConsist)) ? record.RuntimeConsist : trainInfo.dataCon);
		string[] array = Vehicles(consist);
		string text = "";
		string text2 = "";
		string[] array2 = array;
		foreach (string vehicle in array2)
		{
			Td2TractionRule td2TractionRule = Traction(vehicle);
			if (td2TractionRule != null)
			{
				text = Op(td2TractionRule.Owner);
				text2 = OwnerClass(text);
				break;
			}
		}
		bool flag = Passenger(trainInfo.trainCategoryCode);
		bool flag2 = (trainInfo.trainCategoryCode ?? "").StartsWith("PW", StringComparison.OrdinalIgnoreCase) || (trainInfo.trainCategoryCode ?? "").StartsWith("PX", StringComparison.OrdinalIgnoreCase);
		string text3 = "";
		string text4 = "";
		string carrierSource = "";
		if (flag || flag2 || string.IsNullOrWhiteSpace(trainInfo.trainCategoryCode))
		{
			Dictionary<string, double> dictionary = new Dictionary<string, double>();
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			string[] array3 = array;
			foreach (string key in array3)
			{
				string[] value3;
				if (catalog.WagonRules.TryGetValue(key, out var value))
				{
					num++;
					if (value.ContainsKey("TUR"))
					{
						num2++;
						if (value.TryGetValue("IC", out var value2) && value2.Any((string c) => c.Equals("ICN", StringComparison.OrdinalIgnoreCase)) && !value.ContainsKey("PR"))
						{
							num3++;
						}
					}
					string[] array4 = value.Keys.Where((string k) => k != "TUR").Select(Op).ToArray();
					string[] array5 = array4;
					foreach (string op in array5)
					{
						Score(dictionary, op, (array4.Length == 1) ? 1.25 : 0.6);
					}
				}
				else if (catalog.Operators.TryGetValue(key, out value3))
				{
					string[] array6 = value3;
					foreach (string op2 in array6)
					{
						Score(dictionary, op2, (value3.Length == 1) ? 0.8 : 0.35);
					}
				}
			}
			string[] array7 = array;
			foreach (string vehicle2 in array7)
			{
				Td2TractionRule td2TractionRule2 = Traction(vehicle2);
				if (td2TractionRule2 != null && OwnerClass(td2TractionRule2.Owner) == "passenger")
				{
					Score(dictionary, td2TractionRule2.Owner, string.Equals(td2TractionRule2.Kind, "unit", StringComparison.OrdinalIgnoreCase) ? 12.0 : 4.5);
				}
			}
			text3 = (from p in (from p in dictionary
					where PassengerOwner(p.Key)
					orderby p.Value descending
					select p).ThenBy((KeyValuePair<string, double> p) => p.Key, StringComparer.Ordinal)
				select p.Key).FirstOrDefault() ?? "";
			if (text3.Length == 0 && array.Length > 0)
			{
				text3 = Legacy(array);
			}
			if (text2 == "tourist" || (num3 > 0 && (text2 == "cargo" || text2 == "private_cargo") && (num3 >= 2 || (num3 == 1 && (double)num3 / (double)Math.Max(1, num) >= 0.6))) || (text3.Length == 0 && num2 > 0))
			{
				text3 = (new string[3] { "tourist", "cargo", "private_cargo" }.Contains(text2) ? text : "TUR");
			}
			if (text3.Length > 0)
			{
				carrierSource = "TD2: algorytm składu SAZ";
			}
			if (flag)
			{
				string text5 = ApiOwner(td2IdentityResult2.Carrier);
				if (text5.Length > 0)
				{
					text3 = text5;
				}
				string number = Number(Td2Provider.Text(trainInfo.trainNo));
				foreach (Td2NumberOverride @override in catalog.Overrides)
				{
					if (!(Op(@override.Operator) != text3) && (@override.Prefixes ?? new string[0]).Any((string prefix) => prefix.Length > 0 && number.StartsWith(prefix, StringComparison.Ordinal)))
					{
						if (!string.IsNullOrWhiteSpace(@override.OperatorOverwrite))
						{
							text3 = Op(@override.OperatorOverwrite);
							carrierSource = "TD2: reguła numeru SAZ " + number;
						}
						if (!string.IsNullOrWhiteSpace(@override.Name))
						{
							text4 = @override.Name;
						}
					}
				}
				Td2NamedTrain td2NamedTrain = Named(number, text3);
				if (td2NamedTrain != null)
				{
					text3 = Op(td2NamedTrain.Operator);
					carrierSource = "TD2: dokładny numer w katalogu SAZ " + number;
					if (!string.IsNullOrWhiteSpace(td2NamedTrain.Name))
					{
						text4 = td2NamedTrain.Name;
					}
				}
				if (text5.Length > 0 && text5 != text3)
				{
					text4 = "";
				}
			}
		}
		else
		{
			string[] array8 = array;
			foreach (string text6 in array8)
			{
				Td2TractionRule td2TractionRule3 = Traction(text6);
				if (td2TractionRule3 != null && string.Equals(td2TractionRule3.Kind, "loco", StringComparison.OrdinalIgnoreCase))
				{
					text3 = Op(td2TractionRule3.Owner);
					carrierSource = "TD2: lokomotywa " + text6 + " (wnioskowanie z katalogu pojazdów)";
					break;
				}
			}
		}
		td2IdentityResult2.Operator = text3;
		if (td2IdentityResult2.Carrier.Length == 0 && text3.Length > 0)
		{
			td2IdentityResult2.Carrier = OwnerName(text3);
			td2IdentityResult2.CarrierSource = carrierSource;
		}
		if (td2IdentityResult2.Name.Length == 0 && text4.Length > 0)
		{
			td2IdentityResult2.Name = text4;
			td2IdentityResult2.NameSource = "TD2: katalog numerów SAZ";
		}
		return td2IdentityResult2;
	}
}

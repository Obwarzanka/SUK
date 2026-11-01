using System;
using System.Collections.Generic;
using System.Linq;

namespace EdrLocal;

public static class SepeLink
{
	public static SepeLinkInfo Inspect(string text)
	{
		if (!Uri.TryCreate((text ?? "").Trim(), UriKind.Absolute, out var result) || !Trusted(result) || result.UserInfo.Length > 0)
		{
			throw new ArgumentException("Wklej link HTTPS do oficjalnego SEPE SimRail.");
		}
		Dictionary<string, string> dictionary = Query(result.Query);
		if (!dictionary.TryGetValue("serverCode", out var value) || string.IsNullOrWhiteSpace(value) || value.Length > 40 || !value.All((char c) => char.IsLetterOrDigit(c) || c == '-' || c == '_'))
		{
			throw new ArgumentException("Link nie zawiera prawidłowego serwera SimRail.");
		}
		if (!dictionary.TryGetValue("stationId", out var value2))
		{
			throw new ArgumentException("Link nie zawiera posterunków.");
		}
		string[] array = (from x in value2.Split(',')
			select x.Trim()).Distinct().ToArray();
		if (array.Length == 0 || array.Any((string x) => x.Length == 0 || x.Length > 20 || !x.All((char c) => c >= '0' && c <= '9')))
		{
			throw new ArgumentException("Link zawiera nieprawidłowy identyfikator posterunku.");
		}
		if (!dictionary.TryGetValue("key", out var value3) || string.IsNullOrWhiteSpace(value3) || value3.Contains(","))
		{
			throw new ArgumentException("Link nie zawiera jednego klucza dostępu. Skopiuj aktualny link SEPE z gry.");
		}
		SepeLinkInfo sepeLinkInfo = new SepeLinkInfo();
		sepeLinkInfo.ServerCode = value;
		sepeLinkInfo.PointIds = array;
		return sepeLinkInfo;
	}

	public static Uri Validate(string text, string server, string point)
	{
		SepeLinkInfo sepeLinkInfo = Inspect(text);
		if (sepeLinkInfo.ServerCode != server || !sepeLinkInfo.PointIds.Contains(point))
		{
			throw new ArgumentException("Link musi dotyczyć wybranego serwera i posterunku.");
		}
		Uri uri = new Uri(text.Trim());
		Dictionary<string, string> dictionary = Query(uri.Query);
		return new Uri(uri.GetLeftPart(UriPartial.Authority) + "/?serverCode=" + Uri.EscapeDataString(server) + "&stationId=" + Uri.EscapeDataString(point) + "&key=" + Uri.EscapeDataString(dictionary["key"]) + "&lang=pl");
	}

	public static bool Trusted(Uri uri)
	{
		if (uri != null && uri.Scheme == "https" && (uri.Host == "api.simrail.eu" || uri.Host == "api1.aws.simrail.eu" || uri.Host == "panel.simrail.eu"))
		{
			if (uri.Port != 8093)
			{
				return uri.Port == 8092;
			}
			return true;
		}
		return false;
	}

	public static Dictionary<string, string> Query(string query)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		string[] array = query.TrimStart('?').Split('&');
		foreach (string text in array)
		{
			string[] array2 = text.Split(new char[1] { '=' }, 2);
			if (array2.Length == 2)
			{
				string text2 = Uri.UnescapeDataString(array2[0]);
				string text3 = Uri.UnescapeDataString(array2[1]);
				if (dictionary.ContainsKey(text2))
				{
					Dictionary<string, string> dictionary2;
					string key;
					(dictionary2 = dictionary)[key = text2] = dictionary2[key] + "," + text3;
				}
				else
				{
					dictionary[text2] = text3;
				}
			}
		}
		return dictionary;
	}
}

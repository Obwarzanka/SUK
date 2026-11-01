using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace EdrLocal;

public static class Td2Names
{
	public static string Plain(string value)
	{
		return WebUtility.HtmlDecode(Regex.Replace(value ?? "", "<[^>]*>", "")).Trim();
	}

	public static string Raw(Td2Stop stop)
	{
		return Plain(stop.pointNameRAW ?? stop.pointName ?? stop.pointNamePassenger);
	}

	public static string Name(Td2Stop stop)
	{
		return Regex.Replace(Raw(stop), ",\\s*(podg|po|pe)\\.?$", "", RegexOptions.IgnoreCase).Trim();
	}

	public static bool Sbl(Td2Stop stop)
	{
		return Regex.IsMatch(Raw(stop), "\\bSBL(?:[_\\-:0-9A-Z]*)\\b", RegexOptions.IgnoreCase);
	}

	public static bool Operating(Td2Stop stop)
	{
		if (!Sbl(stop))
		{
			return !Regex.IsMatch(Raw(stop), ",\\s*(po|pe)\\.?$", RegexOptions.IgnoreCase);
		}
		return false;
	}

	public static string[] Lane(Td2Stop stop)
	{
		string[] array = (from x in (stop.comments ?? "").Split(',')
			select x.Trim()).ToArray();
		bool flag = array.Length >= 2 && array.Take(2).All((string x) => Regex.IsMatch(x, "^(\\d+[A-Za-z]?|[IVXLCDM]+)$", RegexOptions.IgnoreCase));
		return new string[3]
		{
			Plain(stop.platform ?? (flag ? array[0] : "")),
			Plain(stop.track ?? (flag ? array[1] : "")),
			Plain(flag ? string.Join(", ", array.Skip(2)) : stop.comments)
		};
	}
}

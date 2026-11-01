using System;
using System.Text.RegularExpressions;

namespace EdrLocal;

public sealed class SepeChronologyException : InvalidOperationException
{
	public SepeChronologyException(string message)
		: base(SepeDiagnostics.Clean(message))
	{
	}

	public static Exception FromPage(string message)
	{
		string input = Regex.Replace(message ?? "", "\\s+", " ");
		if (!Regex.IsMatch(input, "(?i)(odjazd\\w*.*wcze[śs]niej\\w*.*przyjazd\\w*|przyjazd\\w*.*p[oó][źz]niej\\w*.*odjazd\\w*|departure.*(?:before|earlier than).*arrival|arrival.*(?:after|later than).*departure)"))
		{
			return new InvalidOperationException(SepeDiagnostics.Clean(message));
		}
		return new SepeChronologyException(message);
	}
}

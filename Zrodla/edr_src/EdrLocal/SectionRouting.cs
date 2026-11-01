using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace EdrLocal;

public static class SectionRouting
{
	private static readonly Regex suffix = new Regex("\\s+(?:[-–/,]\\s*)?(?:[\\[(]\\s*)?(?:L\\.|LK\\.?|linia(?:\\s+kolejowa)?)\\s*(?<line>\\d+)\\s*[\\])]?\\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static string Clean(string value)
	{
		return Regex.Replace(value ?? "", "\\s+", " ").Trim();
	}

	public static string Neighbour(string name)
	{
		return suffix.Replace(Clean(name), "").Trim();
	}

	public static string SuffixLine(string name)
	{
		Match match = suffix.Match(Clean(name));
		if (!match.Success)
		{
			return "";
		}
		return Line(match.Groups["line"].Value);
	}

	public static string Line(string value)
	{
		string input = Clean(value);
		input = Regex.Replace(input, "^(?:L\\.|LK\\.?|linia(?:\\s+kolejowa)?)\\s*", "", RegexOptions.IgnoreCase);
		if (!int.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out var result))
		{
			return input.ToUpperInvariant();
		}
		return result.ToString(CultureInfo.InvariantCulture);
	}

	public static bool SameLine(string a, string b)
	{
		return string.Equals(Line(a), Line(b), StringComparison.OrdinalIgnoreCase);
	}

	public static bool SameNeighbour(string a, string b)
	{
		if (Neighbour(a).Length > 0)
		{
			return string.Equals(Neighbour(a), Neighbour(b), StringComparison.CurrentCultureIgnoreCase);
		}
		return false;
	}

	public static List<Section> Candidates(IEnumerable<Section> sections, string neighbour, string line)
	{
		return sections.Where((Section x) => StationSettings.Conducted(x) && SameNeighbour(x.Name, neighbour) && (string.IsNullOrWhiteSpace(line) || SameLine(x.Line, line))).ToList();
	}

	public static Section Find(IEnumerable<Section> sections, string neighbour, string line)
	{
		List<Section> list = Candidates(sections, neighbour, line);
		if (list.Count != 1)
		{
			return null;
		}
		return list[0];
	}

	public static List<Section> Candidates(IEnumerable<Section> sections, Train train, string direction)
	{
		if (train.SourceProvider != "TD2")
		{
			return Candidates(sections, (direction == "Z") ? train.From : train.To, (direction == "Z") ? train.LineIn : train.LineOut);
		}
		string key = ((direction == "Z") ? train.Td2IncomingKey : train.Td2OutgoingKey);
		return sections.Where((Section x) => StationSettings.Conducted(x) && !string.IsNullOrEmpty(key) && x.Td2RouteKey == key).ToList();
	}

	public static Section Find(IEnumerable<Section> sections, Train train, string direction)
	{
		List<Section> list = Candidates(sections, train, direction);
		if (list.Count != 1)
		{
			return null;
		}
		return list[0];
	}

	public static string Display(Section section)
	{
		if (!string.IsNullOrEmpty(section.Td2RouteKey))
		{
			return StationSettings.Title(section) + (string.IsNullOrWhiteSpace(section.Line) ? "" : (" L. " + section.Line));
		}
		string text = StationSettings.Title(section);
		if (!SameLine(SuffixLine(text), section.Line) || SuffixLine(text).Length <= 0)
		{
			return text + " L. " + Line(section.Line);
		}
		return text;
	}

	public static void LabelDuplicates(Station station)
	{
		if (station.Sections == null)
		{
			return;
		}
		foreach (IGrouping<string, Section> item in station.Sections.Where((Section x) => string.IsNullOrEmpty(x.Td2RouteKey)).GroupBy((Section x) => Neighbour(x.Name), StringComparer.CurrentCultureIgnoreCase))
		{
			if (item.Key.Length == 0 || (from x in item
				select Line(x.Line) into x
				where x.Length > 0
				select x).Distinct(StringComparer.OrdinalIgnoreCase).Count() < 2)
			{
				continue;
			}
			foreach (Section item2 in item)
			{
				if (string.IsNullOrWhiteSpace(item2.JournalName) || SameNeighbour(item2.JournalName, item2.Name))
				{
					item2.JournalName = Neighbour(item2.Name) + " L. " + Line(item2.Line);
				}
			}
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace EdrLocal;

public static class JournalLineTracks
{
	private static readonly Regex Left = new Regex("\\bpo\\s+(?:lewym\\b|torze\\s+lewym\\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex Right = new Regex("\\bpo\\s+(?:prawym\\b|torze\\s+prawym\\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex Number = new Regex("\\bpo\\s+(?:zamkniętym\\s+)?torze\\s+(?:zamkniętym\\s+)?(?:nr\\.?|numer)\\s*\\[?\\s*(\\d+)\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static bool Directive(string text)
	{
		if (!Left.IsMatch(text ?? "") && !Right.IsMatch(text ?? ""))
		{
			return Number.IsMatch(text ?? "");
		}
		return true;
	}

	public static bool IsLeft(Section section, string direction, int track)
	{
		if (section.Tracks > 1)
		{
			return SectionTrackDirections.Incoming(section, track) != (direction == "Z");
		}
		return false;
	}

	private static int Normal(Section section, string direction)
	{
		if (section.Tracks == 1)
		{
			return 1;
		}
		if (direction != "Z" && direction != "DO")
		{
			throw new InvalidOperationException("Wpis pociągu wymaga kierunku Z albo DO.");
		}
		List<int> list = (from n in Enumerable.Range(1, section.Tracks)
			where SectionTrackDirections.Incoming(section, n) == (direction == "Z")
			select n).ToList();
		if (list.Count != 1)
		{
			throw new InvalidOperationException("Nie można jednoznacznie wyznaczyć toru szlakowego według zasadniczych kierunków. Wskaż w uwagach „Po torze nr [numer]” albo popraw konfigurację szlaku.");
		}
		return list[0];
	}

	public static int Resolve(Section section, string direction, string text, int fallback = 0)
	{
		text = text ?? "";
		List<string> list = (from Match x in Number.Matches(text)
			select x.Groups[1].Value).Distinct().ToList();
		if (list.Count > 1)
		{
			throw new InvalidOperationException("Uwagi wskazują różne numery torów szlakowych. Podaj jeden tor dla tego przejazdu.");
		}
		bool flag = Left.IsMatch(text);
		bool flag2 = Right.IsMatch(text);
		if (flag && flag2)
		{
			throw new InvalidOperationException("Uwagi wskazują jednocześnie tor lewy i prawy. Popraw opis przejazdu.");
		}
		if (list.Count == 1)
		{
			if (!int.TryParse(list[0], out var result) || result < 1 || result > section.Tracks)
			{
				throw new InvalidOperationException("Numer toru szlakowego w uwagach nie należy do tego szlaku.");
			}
			if (section.Tracks > 1 && ((flag && !IsLeft(section, direction, result)) || (flag2 && IsLeft(section, direction, result))))
			{
				throw new InvalidOperationException("Numer toru szlakowego w uwagach jest sprzeczny z opisem toru lewego/prawego dla tego kierunku jazdy.");
			}
			return result;
		}
		if (section.Tracks == 1)
		{
			return 1;
		}
		if (flag || flag2)
		{
			int normal = Normal(section, direction);
			if (!flag)
			{
				return normal;
			}
			return Enumerable.Range(1, section.Tracks).Single((int n) => n != normal);
		}
		if (fallback <= 0)
		{
			return Normal(section, direction);
		}
		return fallback;
	}

	public static int ForEntry(Section section, JournalEntry entry, JournalEntry previous)
	{
		int fallback = entry.LineTrack;
		if (previous != null && (previous.SectionId != entry.SectionId || previous.Direction != entry.Direction || (Directive(previous.Text) && !Directive(entry.Text))))
		{
			fallback = 0;
		}
		return Resolve(section, entry.Direction, entry.Text, fallback);
	}
}

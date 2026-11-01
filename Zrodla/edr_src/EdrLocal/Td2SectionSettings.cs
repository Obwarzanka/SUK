using System.Linq;

namespace EdrLocal;

public static class Td2SectionSettings
{
	public static void Normalize(Section section)
	{
		if (string.IsNullOrEmpty(section.Td2RouteKey))
		{
			return;
		}
		if (section.Td2SettingsVersion == 0)
		{
			string text = section.Line ?? "";
			if (string.IsNullOrWhiteSpace(section.Td2LocalLine))
			{
				section.Td2LocalLine = ((text.Length > 0) ? text : section.Td2RouteKey.Substring(section.Td2RouteKey.IndexOf(':') + 1));
			}
			section.Td2CustomJournalName = !string.IsNullOrWhiteSpace(section.JournalName) && section.JournalName != text && section.JournalName != section.Td2LocalLine && section.JournalName != section.Td2PeerName && section.JournalName != section.Name;
			section.Line = "";
			section.Td2SettingsVersion = 1;
		}
		if (!section.Td2CustomJournalName)
		{
			section.JournalName = AutomaticTitle(section);
		}
	}

	public static string AutomaticTitle(Section section)
	{
		string text;
		if (!(section.Td2Kind == "Wewnętrzny") || section.Td2UsePeerName == false || string.IsNullOrWhiteSpace(section.Td2PeerName))
		{
			text = section.Td2LocalLine;
			if (text == null)
			{
				return section.Name;
			}
		}
		else
		{
			text = section.Td2PeerName;
		}
		return text;
	}

	public static string Title(Section section)
	{
		if (!section.Td2CustomJournalName || string.IsNullOrWhiteSpace(section.JournalName))
		{
			return AutomaticTitle(section);
		}
		return section.JournalName;
	}

	public static string AssignedLine(Station station, string key)
	{
		Section section = station.Sections.FirstOrDefault((Section x) => !x.Td2Archived && !x.Td2AreaHidden && x.Td2RouteKey == key);
		string text;
		if (section != null)
		{
			text = section.Line;
			if (text == null)
			{
				return "";
			}
		}
		else
		{
			text = "";
		}
		return text;
	}
}

using System;
using System.Linq;
using System.Windows.Forms;

namespace EdrLocal;

public static class TrainDirections
{
	public const string EndMarker = "----------";

	public static bool Starts(Train train, Station station)
	{
		if (!train.StartsHere)
		{
			return string.Equals(train.Origin, station.Name, StringComparison.CurrentCultureIgnoreCase);
		}
		return true;
	}

	public static bool Ends(Train train, Station station)
	{
		if (!train.EndsHere)
		{
			return string.Equals(train.Destination, station.Name, StringComparison.CurrentCultureIgnoreCase);
		}
		return true;
	}

	public static string Text(Engine engine, Train train, string direction)
	{
		bool flag = direction == "Z";
		if (flag ? Starts(train, engine.Station) : Ends(train, engine.Station))
		{
			return "----------";
		}
		string text = (flag ? train.From : train.To) ?? "";
		if (train.SourceProvider != "TD2")
		{
			return text;
		}
		string key = (flag ? train.Td2IncomingKey : train.Td2OutgoingKey);
		Section section = engine.Station.Sections.FirstOrDefault((Section x) => !x.Td2Archived && !x.Td2AreaHidden && x.Td2RouteKey == key);
		if (section != null && section.Td2Kind == "Wewnętrzny")
		{
			string text2;
			if (section.Td2UsePeerName == false)
			{
				text2 = section.Td2LocalLine;
				if (text2 == null)
				{
					return "";
				}
			}
			else
			{
				text2 = text;
			}
			return text2;
		}
		string text3 = (flag ? train.Td2IncomingPair : train.Td2OutgoingPair);
		return text + ((engine.Station.Td2ShowRouteLines != false && !string.IsNullOrWhiteSpace(text3) && text.Length > 0) ? (" (" + text3 + ")") : "");
	}

	public static string Lines(Train train)
	{
		return string.Join("/", new string[2] { train.LineIn, train.LineOut }.Where((string x) => !string.IsNullOrWhiteSpace(x)).Distinct());
	}

	public static void Tooltips(Engine engine, DataGridViewRow row, Train train)
	{
		if (train.SourceProvider != "TD2")
		{
			return;
		}
		string[] array = new string[2] { "Z", "DO" };
		foreach (string text in array)
		{
			string key = ((text == "Z") ? train.Td2IncomingKey : train.Td2OutgoingKey);
			Section section = engine.Station.Sections.FirstOrDefault((Section x) => !x.Td2Archived && !x.Td2AreaHidden && x.Td2RouteKey == key);
			row.Cells[(text == "Z") ? "from" : "to"].ToolTipText = ((section == null) ? "" : ("Szlak TD2: " + section.Td2LocalLine + "\nDziennik: " + StationSettings.Title(section) + (string.IsNullOrWhiteSpace(section.Line) ? "" : ("\nLinia: " + section.Line))));
		}
	}
}

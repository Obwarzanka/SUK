using System;
using System.Drawing;

namespace EdrLocal;

public static class EdrPalette
{
	public static readonly Color Dangerous = Color.FromArgb(190, 86, 133);

	public static readonly Color HighRisk = Color.FromArgb(255, 128, 64);

	public static readonly Color Quality = Color.FromArgb(192, 220, 192);

	public static readonly Color Cream = Color.FromArgb(255, 255, 225);

	public static readonly Color Incoming = Color.FromArgb(215, 228, 242);

	public static readonly Color OutgoingNumber = Color.FromArgb(192, 220, 192);

	public static readonly Color Outgoing = Color.FromArgb(255, 243, 232);

	public static readonly Color Stay = Color.FromArgb(255, 211, 168);

	public static readonly Color NoStay = Color.FromArgb(227, 227, 227);

	public static readonly Color Platform = Color.FromArgb(166, 202, 240);

	public static readonly Color Required = Color.FromArgb(151, 186, 205);

	public static readonly Color Navy = Color.FromArgb(0, 0, 128);

	public static readonly Color Cyan = Color.FromArgb(0, 255, 255);

	public static Color TimetableBand(Train train, string station)
	{
		if (string.Equals(train.Destination, station, StringComparison.CurrentCultureIgnoreCase))
		{
			return Color.Magenta;
		}
		if (string.Equals(train.Origin, station, StringComparison.CurrentCultureIgnoreCase))
		{
			return Color.Lime;
		}
		if (!(train.PlanDeparture > train.PlanArrival))
		{
			return Cyan;
		}
		return Color.FromArgb(51, 153, 255);
	}

	public static Color Header(string name, bool detail)
	{
		if (detail)
		{
			return Color.FromArgb(0, 0, 75);
		}
		switch (name)
		{
		case "k":
		case "nk":
			return Color.FromArgb(75, 0, 75);
		case "odd":
		case "from":
			return Color.FromArgb(0, 200, 200);
		case "even":
		case "to":
			return Color.FromArgb(0, 75, 0);
		case "line":
			return Color.FromArgb(200, 200, 0);
		case "pa":
		case "ra":
		case "pd":
		case "rd":
			return Color.FromArgb(75, 75, 0);
		case "da":
		case "dd":
		case "kind":
			return Color.FromArgb(75, 0, 0);
		case "origin":
		case "destination":
		case "carrier":
			return Color.FromArgb(0, 0, 75);
		case "pt":
			return Color.FromArgb(0, 77, 150);
		case "track":
			return Color.Silver;
		case "orderedStay":
		case "stay":
		case "type":
			return Color.FromArgb(153, 153, 153);
		default:
			return Color.Black;
		}
	}
}

using System;
using System.Linq;

namespace EdrLocal;

public static class TelephonePresentation
{
	public static string Sender(JournalEntry entry)
	{
		if (entry.Telephone == null)
		{
			if (!(entry.Direction == "Odbieram"))
			{
				return entry.User;
			}
			return entry.Recipient;
		}
		return entry.Telephone.Sender;
	}

	public static string Receiver(JournalEntry entry)
	{
		if (entry.Telephone == null)
		{
			if (!(entry.Direction == "Odbieram"))
			{
				return entry.Recipient;
			}
			return entry.User;
		}
		return entry.Telephone.Receiver;
	}

	public static string Column(JournalEntry entry)
	{
		if (entry.Type != "Telefonogram")
		{
			if (!string.IsNullOrWhiteSpace(entry.Recipient))
			{
				return entry.Recipient;
			}
			return entry.User;
		}
		return string.Join(" / ", new string[2]
		{
			Sender(entry),
			Receiver(entry)
		}.Where((string x) => !string.IsNullOrWhiteSpace(x)));
	}

	public static string JournalLine(JournalEntry entry)
	{
		if (entry.Type != "Telefonogram")
		{
			return "";
		}
		TelephoneData telephone = entry.Telephone;
		string station = "";
		string station2 = "";
		DateTime dateTime = entry.Time;
		if (telephone != null)
		{
			station = (telephone.Incoming ? telephone.NeighbourStation : telephone.LocalStation);
			station2 = (telephone.Incoming ? telephone.LocalStation : telephone.NeighbourStation);
			dateTime = telephone.SentAt;
		}
		return "Nadał " + Party(Sender(entry), station) + (string.IsNullOrWhiteSpace(Receiver(entry)) ? "" : (", odebrał " + Party(Receiver(entry), station2))) + " godz. " + dateTime.ToString("HH:mm") + ".";
	}

	private static string Party(string name, string station)
	{
		return (name ?? "") + (string.IsNullOrWhiteSpace(station) ? "" : (" " + station));
	}
}

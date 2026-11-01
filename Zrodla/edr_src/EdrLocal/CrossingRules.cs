using System;
using System.Collections.Generic;
using System.Linq;

namespace EdrLocal;

public static class CrossingRules
{
	public static List<CrossingSetting> All(Section section)
	{
		return section.Crossings ?? new List<CrossingSetting>();
	}

	public static List<CrossingSetting> Visible(Section section)
	{
		return (from x in All(section)
			where x.InJournal != false
			select x).ToList();
	}

	public static bool HasCrossings(Section section)
	{
		if (!section.NotifyCrossings)
		{
			return All(section).Count > 0;
		}
		return true;
	}

	public static bool UsesTime(Section section, CrossingSetting crossing)
	{
		if (crossing.AlongTrack == "Jednotorowych")
		{
			return false;
		}
		if (crossing.AlongTrack == "Dwutorowych")
		{
			return true;
		}
		if (crossing.AlongTrack == "Wszystkich")
		{
			return section.Tracks > 1;
		}
		return crossing.NotificationMode == "Godzina";
	}

	public static string Label(CrossingSetting crossing)
	{
		if (string.IsNullOrWhiteSpace(crossing.Mark))
		{
			if (string.IsNullOrWhiteSpace(crossing.Kilometre))
			{
				return crossing.Name;
			}
			return crossing.Kilometre;
		}
		return crossing.Mark;
	}

	public static bool Required(JournalEntry entry, Section section, CrossingSetting crossing)
	{
		if (entry == null || entry.Type != "Pociąg" || entry.Cancelled || entry.Direction != "DO" || !string.IsNullOrEmpty(entry.ReturnOfId))
		{
			return false;
		}
		char[] array = (entry.Number ?? "").Where(char.IsDigit).ToArray();
		if (crossing.Trains == "Parzystych" || crossing.Trains == "Nieparzystych")
		{
			if (array.Length == 0)
			{
				return false;
			}
			bool flag = (array[array.Length - 1] - 48) % 2 == 0;
			return flag == (crossing.Trains == "Parzystych");
		}
		return true;
	}

	public static CrossingNotification Find(JournalEntry entry, Section section, CrossingSetting crossing)
	{
		CrossingNotification crossingNotification = (entry.CrossingNotifications ?? new List<CrossingNotification>()).FirstOrDefault((CrossingNotification x) => x.CrossingId == crossing.Id);
		if (crossingNotification != null)
		{
			return Json.Clone(crossingNotification);
		}
		string text = entry.Notification ?? "";
		string text2 = "";
		List<CrossingSetting> list = (from x in All(section)
			where x.Name == crossing.Name
			select x).ToList();
		if (list.Count == 1)
		{
			string prefix = crossing.Name + ": ";
			string text3 = text.Split(new string[1] { "; " }, StringSplitOptions.None).FirstOrDefault((string x) => x.StartsWith(prefix, StringComparison.Ordinal));
			if (text3 != null)
			{
				text2 = text3.Substring(prefix.Length);
			}
		}
		if (text2.Length == 0 && All(section).Count == 1 && (text == "/" || Time(text, entry.Time).HasValue))
		{
			text2 = text;
		}
		CrossingNotification crossingNotification2 = new CrossingNotification();
		crossingNotification2.CrossingId = crossing.Id;
		crossingNotification2.Name = crossing.Name;
		crossingNotification2.Value = text2;
		crossingNotification2.RecordedAt = Time(text2, entry.Time);
		return crossingNotification2;
	}

	public static DateTime? Time(string value, DateTime day)
	{
		if (string.IsNullOrWhiteSpace(value) || value == "/")
		{
			return null;
		}
		try
		{
			return Engine.JournalTime(value, day, null, null);
		}
		catch
		{
			return null;
		}
	}

	public static string Value(JournalEntry entry, Section section, CrossingSetting crossing)
	{
		CrossingNotification crossingNotification = Find(entry, section, crossing);
		if (string.IsNullOrEmpty(crossingNotification.Value))
		{
			return "";
		}
		if (!UsesTime(section, crossing))
		{
			return "/";
		}
		return Ui.Time(crossingNotification.RecordedAt ?? Time(crossingNotification.Value, entry.Time));
	}

	public static void ValidateValue(Section section, CrossingSetting crossing, string value, DateTime day)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			if (UsesTime(section, crossing))
			{
				Engine.JournalTime(value, day, null, null);
			}
			else if (value != "/")
			{
				throw new Exception("Zaznacz powiadomienie dróżnika " + Label(crossing) + " ukośnikiem /.");
			}
		}
	}

	public static void Set(JournalEntry entry, Section section, CrossingSetting crossing, string value, DateTime? recorded)
	{
		value = (value ?? "").Trim();
		ValidateValue(section, crossing, value, entry.Time);
		if (entry.CrossingNotifications == null)
		{
			entry.CrossingNotifications = new List<CrossingNotification>();
		}
		foreach (CrossingSetting post in All(section))
		{
			if (!entry.CrossingNotifications.Any((CrossingNotification x) => x.CrossingId == post.Id))
			{
				entry.CrossingNotifications.Add(Find(entry, section, post));
			}
		}
		CrossingNotification crossingNotification = entry.CrossingNotifications.Single((CrossingNotification x) => x.CrossingId == crossing.Id);
		crossingNotification.Name = crossing.Name;
		crossingNotification.Value = value;
		crossingNotification.RecordedAt = ((value.Length == 0) ? ((DateTime?)null) : (recorded ?? (UsesTime(section, crossing) ? Time(value, entry.Time) : crossingNotification.RecordedAt)));
		entry.Notification = string.Join("; ", from x in entry.CrossingNotifications
			where !string.IsNullOrEmpty(x.Value)
			select x.Name + ": " + x.Value);
	}
}

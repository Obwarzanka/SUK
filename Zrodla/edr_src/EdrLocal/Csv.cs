using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace EdrLocal;

public static class Csv
{
	public static string Cell(object value)
	{
		return "\"" + ((value == null) ? "" : value.ToString()).Replace("\"", "\"\"") + "\"";
	}

	public static void Write(string path, IEnumerable<string[]> rows)
	{
		LocalPersistence.RequireEnabled();
		File.WriteAllText(path, string.Join("\r\n", rows.Select((string[] r) => string.Join(";", r.Select((string c) => Cell(c)).ToArray()))), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
	}

	public static List<string[]> Read(string path)
	{
		string text = File.ReadAllText(path, Encoding.UTF8);
		List<string[]> list = new List<string[]>();
		List<string> list2 = new List<string>();
		StringBuilder stringBuilder = new StringBuilder();
		bool flag = false;
		for (int i = 0; i < text.Length; i++)
		{
			char c = text[i];
			if (c == '"')
			{
				if (flag && i + 1 < text.Length && text[i + 1] == '"')
				{
					stringBuilder.Append('"');
					i++;
				}
				else
				{
					flag = !flag;
				}
			}
			else if (!flag && c == ';')
			{
				list2.Add(stringBuilder.ToString());
				stringBuilder.Clear();
			}
			else if (!flag && (c == '\r' || c == '\n'))
			{
				if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
				{
					i++;
				}
				list2.Add(stringBuilder.ToString());
				stringBuilder.Clear();
				if (list2.Any((string x) => x.Length > 0))
				{
					list.Add(list2.ToArray());
				}
				list2.Clear();
			}
			else
			{
				stringBuilder.Append(c);
			}
		}
		if (flag)
		{
			throw new Exception("Niezamknięty cudzysłów w pliku CSV.");
		}
		if (stringBuilder.Length > 0 || list2.Count > 0)
		{
			list2.Add(stringBuilder.ToString());
			list.Add(list2.ToArray());
		}
		return list;
	}

	public static List<Train> Import(string path, Station st)
	{
		List<string[]> list = Read(path);
		if (list.Count < 2)
		{
			throw new Exception("CSV nie zawiera danych.");
		}
		string[] second = new string[8] { "Numer", "Rodzaj", "Przyjazd", "Odjazd", "Z kierunku", "W kierunku", "Tor", "Przewoźnik" };
		if (!list[0].SequenceEqual(second))
		{
			throw new Exception("Nieprawidłowy nagłówek. Użyj wzoru CSV dołączonego do aplikacji.");
		}
		List<Train> list2 = new List<Train>();
		for (int i = 1; i < list.Count; i++)
		{
			string[] r = list[i];
			if (r.Length != 8)
			{
				throw new Exception("Nieprawidłowa liczba kolumn w wierszu " + (i + 1));
			}
			if (!DateTime.TryParseExact(r[2], "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result) || !DateTime.TryParseExact(r[3], "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result2) || result2 < result)
			{
				throw new Exception("Nieprawidłowe daty w wierszu " + (i + 1));
			}
			Section section = st.Sections.FirstOrDefault((Section s) => s.Name == r[4]);
			Section section2 = st.Sections.FirstOrDefault((Section s) => s.Name == r[5]);
			if (section == null || section2 == null || !StationSettings.TrackAllowed(st, r[6], null) || string.IsNullOrWhiteSpace(r[0]))
			{
				throw new Exception("Nieznany szlak, tor lub pusty numer w wierszu " + (i + 1));
			}
			list2.Add(new Train
			{
				Id = Guid.NewGuid().ToString("N"),
				StationId = st.Id,
				Number = r[0],
				Kind = r[1],
				PlanArrival = result,
				PlanDeparture = result2,
				From = r[4],
				To = r[5],
				Track = r[6],
				Carrier = r[7],
				Runs = true,
				LineIn = section.Line,
				LineOut = section2.Line,
				Origin = r[4],
				Destination = r[5],
				Platform = "",
				Notes = "Import lokalny",
				Analysis = "",
				Cargo = "",
				DelayReason = ""
			});
		}
		if ((from t in list2
			group t by t.Number + "|" + t.PlanArrival.Date.ToString("s")).Any((IGrouping<string, Train> g) => g.Count() > 1))
		{
			throw new Exception("Powtórzony numer pociągu w tym samym dniu w CSV.");
		}
		return list2;
	}
}

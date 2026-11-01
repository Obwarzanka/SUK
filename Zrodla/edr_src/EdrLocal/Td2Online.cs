using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace EdrLocal;

public static class Td2Online
{
	public const string Address = "https://api.td2.info.pl/?method=getTrainsOnline";

	internal static List<Td2OnlineTrain> Read(Func<string, CancellationToken, byte[]> download, CancellationToken token)
	{
		try
		{
			byte[] array = download("https://api.td2.info.pl/?method=getTrainsOnline", token);
			token.ThrowIfCancellationRequested();
			if (array == null || array.Length == 0 || array.Length > 67108864)
			{
				return new List<Td2OnlineTrain>();
			}
			string text = Encoding.UTF8.GetString(array, 0, Math.Min(array.Length, 12)).TrimStart('\ufeff', ' ', '\r', '\n', '\t');
			if (text.StartsWith("[", StringComparison.Ordinal))
			{
				return SimRailProvider.Parse<List<Td2OnlineTrain>>(array) ?? new List<Td2OnlineTrain>();
			}
			Td2OnlineEnvelope td2OnlineEnvelope = SimRailProvider.Parse<Td2OnlineEnvelope>(array);
			return (td2OnlineEnvelope != null && td2OnlineEnvelope.success && td2OnlineEnvelope.message != null) ? td2OnlineEnvelope.message : new List<Td2OnlineTrain>();
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch
		{
			token.ThrowIfCancellationRequested();
			return new List<Td2OnlineTrain>();
		}
	}

	internal static void Attach(List<Td2Record> records, string region, List<Td2OnlineTrain> online)
	{
		List<Td2Record> list = records.Where((Td2Record r) => !r.Removed).ToList();
		List<Td2OnlineTrain> source = (online ?? new List<Td2OnlineTrain>()).Where((Td2OnlineTrain t) => t != null && t.Region == region && (t.isOnline == null || Td2Provider.Flag(t.isOnline))).ToList();
		foreach (Td2Record record in list)
		{
			string number = Td2Identity.Number(Td2Provider.Text(record.Schedule.trainInfo.trainNo));
			List<Td2OnlineTrain> list2 = source.Where((Td2OnlineTrain t) => t.Number == number && (t.Timetable.Length == 0 || t.Timetable == record.Id)).ToList();
			Func<Td2OnlineTrain, bool> predicate = (Td2OnlineTrain t) => t.Timetable == record.Id;
			List<Td2OnlineTrain> list3 = list2.Where(predicate).ToList();
			Td2OnlineTrain td2OnlineTrain = ((list3.Count == 1) ? list3[0] : null);
			if (td2OnlineTrain == null && list3.Count == 0)
			{
				string driver = Td2Provider.Text(record.Schedule.trainInfo.driverId);
				List<Td2OnlineTrain> list4 = list2.Where((Td2OnlineTrain t) => driver.Length > 0 && t.Driver == driver).ToList();
				if (list4.Count == 1 && Enumerable.Count(list, (Td2Record r) => Td2Identity.Number(Td2Provider.Text(r.Schedule.trainInfo.trainNo)) == number && Td2Provider.Text(r.Schedule.trainInfo.driverId) == driver) == 1)
				{
					td2OnlineTrain = list4[0];
				}
				else if (Enumerable.Count(list, (Td2Record r) => Td2Identity.Number(Td2Provider.Text(r.Schedule.trainInfo.trainNo)) == number) == 1 && list2.Count == 1)
				{
					td2OnlineTrain = list2[0];
				}
			}
			if (td2OnlineTrain != null && td2OnlineTrain.Consist.Length > 0)
			{
				record.RuntimeConsist = td2OnlineTrain.Consist;
			}
		}
	}
}

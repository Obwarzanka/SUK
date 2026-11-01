using System.Collections.Generic;
using System.Linq;

namespace EdrLocal;

public static class SectionTrackDirections
{
	private static bool Complete(Section section)
	{
		if (section.LineTracks != null && section.LineTracks.Count == section.Tracks)
		{
			return Enumerable.Range(1, section.Tracks).All((int n) => Enumerable.Count(section.LineTracks, (LineTrackSetting x) => x.Number == n) == 1);
		}
		return false;
	}

	private static bool DefaultIncoming(Section section, int number)
	{
		if (section.Tracks != 2)
		{
			return number == section.NormalIncomingTrack;
		}
		return number == ((!section.ArrivalEven) ? 1 : 2);
	}

	public static bool Incoming(Section section, int number)
	{
		if (!Complete(section))
		{
			return DefaultIncoming(section, number);
		}
		return section.LineTracks.First((LineTrackSetting x) => x.Number == number).NormalIncoming;
	}

	public static IEnumerable<int> DisplayOrder(Section section)
	{
		if (section.Tracks == 1)
		{
			return new int[1] { 1 };
		}
		int num = section.RightTrackNumber ?? ((!DefaultIncoming(section, 1)) ? 1 : 2);
		return new int[2]
		{
			(num != 1) ? 1 : 2,
			num
		};
	}

	public static void Normalize(Section section)
	{
		if (!Complete(section))
		{
			List<LineTrackSetting> previous = section.LineTracks ?? new List<LineTrackSetting>();
			section.LineTracks = Enumerable.Range(1, section.Tracks).Select((int n) =>
			{
				LineTrackSetting lineTrackSetting = previous.FirstOrDefault((LineTrackSetting x) => x.Number == n) ?? new LineTrackSetting
				{
					Number = n,
					Basis = section.Basis
				};
				lineTrackSetting.NormalIncoming = DefaultIncoming(section, n);
				return lineTrackSetting;
			}).ToList();
		}
		section.NormalIncomingTrack = (from x in section.LineTracks
			where x.NormalIncoming
			select x.Number).FirstOrDefault();
	}
}

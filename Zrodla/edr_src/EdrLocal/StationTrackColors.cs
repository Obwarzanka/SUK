using System.Drawing;

namespace EdrLocal;

public static class StationTrackColors
{
	public static Color ForStatus(string status)
	{
		if (!(status ?? "").StartsWith("ZAJĘTY"))
		{
			if (!(status ?? "").StartsWith("REZERWACJA"))
			{
				return Color.FromArgb(25, 134, 18);
			}
			return Color.Orange;
		}
		return Color.Firebrick;
	}

	public static Color ForTrack(Engine engine, string track)
	{
		if (!string.IsNullOrEmpty(track))
		{
			return ForStatus(engine.TrackStatus(track));
		}
		return SystemColors.WindowText;
	}
}

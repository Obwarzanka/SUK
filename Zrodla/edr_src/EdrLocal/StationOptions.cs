namespace EdrLocal;

public sealed class StationOptions
{
	public int? AutomaticReleaseSeconds { get; set; }

	public bool? WriteSepe { get; set; }

	public bool? RefreshStationTracks { get; set; }

	public int StationTrackRefreshMinutes { get; set; }

	public bool TelephoneAnnual { get; set; }

	public int TelephoneNextNumber { get; set; }

	public string TelephoneNextPeriod { get; set; }

	public int JournalVisibility { get; set; }

	public StationOptions()
	{
		StationTrackRefreshMinutes = 10;
	}
}

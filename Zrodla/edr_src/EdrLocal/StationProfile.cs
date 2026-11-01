using System;

namespace EdrLocal;

public sealed class StationProfile
{
	public int SchemaVersion { get; set; }

	public string PointId { get; set; }

	public DateTime SavedUtc { get; set; }

	public Station Station { get; set; }
}

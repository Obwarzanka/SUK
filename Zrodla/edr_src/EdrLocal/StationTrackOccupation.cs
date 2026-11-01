using System;
using System.Collections.Generic;

namespace EdrLocal;

public sealed class StationTrackOccupation
{
	public string Id { get; set; }

	public string StationId { get; set; }

	public string EntryId { get; set; }

	public string SourceTrack { get; set; }

	public string Track { get; set; }

	public string Number { get; set; }

	public string TrainId { get; set; }

	public bool Removed { get; set; }

	public DateTime AddedAt { get; set; }

	public List<string> KnownDepartures { get; set; }
}

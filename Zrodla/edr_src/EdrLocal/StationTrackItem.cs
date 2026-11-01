using System;

namespace EdrLocal;

public sealed class StationTrackItem
{
	public string Key { get; set; }

	public string Track { get; set; }

	public string Number { get; set; }

	public string TrainId { get; set; }

	public bool Reserved { get; set; }

	public DateTime Order { get; set; }
}

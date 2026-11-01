using System;

namespace EdrLocal;

public class CrossingNotification
{
	public string CrossingId { get; set; }

	public string Name { get; set; }

	public string Value { get; set; }

	public DateTime? RecordedAt { get; set; }
}

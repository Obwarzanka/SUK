using System;
using System.Collections.Generic;

namespace EdrLocal;

public sealed class WorkingDocument
{
	public int Version { get; set; }

	public string Kind { get; set; }

	public string Provider { get; set; }

	public string Server { get; set; }

	public string Point { get; set; }

	public string StationId { get; set; }

	public string StationName { get; set; }

	public DateTime SavedUtc { get; set; }

	public List<SavedSection> Sections { get; set; }

	public List<Train> Trains { get; set; }

	public List<JournalEntry> Entries { get; set; }

	public List<Duty> Duties { get; set; }

	public List<ServiceRecord> Services { get; set; }

	public List<Audit> History { get; set; }
}

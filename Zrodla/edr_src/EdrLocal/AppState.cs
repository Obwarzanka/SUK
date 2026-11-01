using System.Collections.Generic;

namespace EdrLocal;

public class AppState
{
	public List<StationTrackOccupation> StationTrackOccupations { get; set; }

	public int Version { get; set; }

	public List<Station> Stations { get; set; }

	public List<LocalUser> Users { get; set; }

	public List<Train> Trains { get; set; }

	public List<JournalEntry> Entries { get; set; }

	public List<Duty> Duties { get; set; }

	public List<Audit> History { get; set; }

	public List<ServiceRecord> Services { get; set; }

	public string Layout { get; set; }

	public float UiZoom { get; set; }
}

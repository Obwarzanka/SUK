using System.Collections.Generic;

namespace EdrLocal;

public class Section
{
	public string Td2LocalLine { get; set; }

	public int Td2SettingsVersion { get; set; }

	public bool? Td2UsePeerName { get; set; }

	public bool Td2CustomJournalName { get; set; }

	public bool Td2AreaHidden { get; set; }

	public string Td2RouteKey { get; set; }

	public string Td2Kind { get; set; }

	public string Td2PeerName { get; set; }

	public bool Td2Archived { get; set; }

	public bool ManualConfiguration { get; set; }

	public int? RightTrackNumber { get; set; }

	public string LineName { get; set; }

	public string JournalName { get; set; }

	public bool? Conducted { get; set; }

	public List<CrossingSetting> Crossings { get; set; }

	public List<LineTrackSetting> LineTracks { get; set; }

	public string Id { get; set; }

	public string Name { get; set; }

	public string Line { get; set; }

	public int NormalIncomingTrack { get; set; }

	public bool NotifyCrossings { get; set; }

	public int Tracks { get; set; }

	public bool ArrivalEven { get; set; }

	public string Basis { get; set; }
}
